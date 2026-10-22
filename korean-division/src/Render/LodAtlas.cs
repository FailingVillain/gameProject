using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Raylib_cs;

namespace KoreanDivision.Render;

/// <summary>
/// LOD 시트 아틀라스 (2026-10-19 art-memory). 칸마다 투명 여백을 잘라 담고 (기준점은 칸 기준 그대로 — <see cref="LodLevel.COX"/>),
/// 애니메이션 매핑이 쓰지 않는 칸은 뺀다 (<see cref="LodSprite.Used"/>).
///  · LOD 1 이상 (작은 해상도) = 같은 LOD 번호끼리 공유 페이지 → 축소 화면은 한 텍스처 = 한 배치. 한 번 만들면 상주.
///  · LOD 0 (최대 확대) = 종류 × 진영 변형마다 전용 페이지, 처음 그릴 때 만든다. <see cref="EvictSec"/> 초 안 쓰면 내리고,
///    상주 합이 <see cref="Lod0BudgetBytes"/> 를 넘으면 오래 안 쓴 것부터 내린다.
///  · 가장 작은 LOD 는 처음 그릴 때 그 자리에서 (동기) 만들고, 나머지는 작업자 스레드가 PNG 읽기 · 팀색 접기 · 자르기 · 배치를 하고
///    본 스레드가 프레임마다 <see cref="UploadBytesPerFrame"/> 만큼씩 줄 단위로 올린다. 다 올라갈 때까지는 다음 작은 LOD 로 그린다.
/// 팀색 레이어(_team.png, 회색조)는 기본 시트에 접어 넣는다: 팀 픽셀 = (회색, 회색, 회색, 알파 254). 점 필터 · 밉맵 없음.
/// </summary>
internal sealed class LodAtlas : IDisposable
{
    public const byte TeamAlpha = 254;
    const int Gutter = 1;   // 칸 테두리가 불투명 픽셀이면 (칸 끝까지 그린 그림) 이웃 칸 번짐 방지
    const int Pad = 1;
    const int MaxPage = 4096;

    /// <summary>LOD 0 전용 페이지 상주 한도 (기본 768 MB — 2026-10-19 256 → 768: uifuzz 30초에 예산 초과 내림 65 · 물러서 그림 8762 → 최대 확대 그림 누락의 원인, <c>KD_LOD0_BUDGET_MB</c>).</summary>
    public static long Lod0BudgetBytes = EnvMb("KD_LOD0_BUDGET_MB", 768);
    /// <summary>LOD 0 페이지를 이만큼 (초) 안 그리면 내린다 (<c>KD_LOD_EVICT_SEC</c>, 기본 30).</summary>
    public static double EvictSec = EnvF("KD_LOD_EVICT_SEC", 30);
    /// <summary>프레임당 GPU 업로드 한도 (<c>KD_LOD_UPLOAD_MB</c>, 기본 6 MB).</summary>
    public static long UploadBytesPerFrame = EnvMb("KD_LOD_UPLOAD_MB", 6);
    /// <summary>false = 전부 그 자리에서 만든다 (<c>KD_LOD_SYNC=1</c> · 비교용).</summary>
    public static bool Async = Environment.GetEnvironmentVariable("KD_LOD_SYNC") == null;

    static long EnvMb(string name, long def) =>
        (long.TryParse(Environment.GetEnvironmentVariable(name), out long v) && v > 0 ? v : def) * 1048576L;
    static double EnvF(string name, double def) =>
        double.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0 ? v : def;

    // ── [측정] 모든 테마 합: LOD 번호별 현재 · 최대 GPU 바이트
    public static readonly long[] GroupBytes = new long[4], GroupPeak = new long[4];
    public static long TotalBytes, TotalPeak;
    public static int Evictions, AsyncBuilds, SyncBuilds;
    /// <summary>[측정] 업로드가 있었던 프레임의 Pump 최대 시간 (ms) — 끊김 확인용.</summary>
    public static double PumpMsMax;
    static readonly List<LodAtlas> live = new();
    static readonly Stopwatch clock = Stopwatch.StartNew();
    /// <summary>마지막 Pump 시각 (초) — LastUsed 기준.</summary>
    public static double Now;
    static long lastPumpTicks = -(1L << 40);

    readonly string dir;
    readonly Dictionary<int, List<LodLevel>> groups = new();
    readonly Dictionary<int, List<Texture2D>> shared = new();
    readonly ConcurrentQueue<Job> done = new();
    readonly Queue<Job> uploads = new();
    bool disposed;
    public long Bytes { get; private set; }
    /// <summary>[측정] 원하는 LOD 가 없어 작은 LOD 로 물러서 그린 횟수.</summary>
    public static long Fallbacks;
    public string Info { get; private set; } = "";

    public LodAtlas(string dir) { this.dir = dir; lock (live) live.Add(this); }

    public void Register(LodSprite s)
    {
        s.Atlas = this;
        foreach (var lv in s.Levels)
        {
            if (!groups.TryGetValue(lv.Group, out var list)) groups[lv.Group] = list = new List<LodLevel>();
            list.Add(lv);
        }
    }

    // ───────── 그리기 쪽 ─────────

    /// <summary>
    /// 그릴 LOD: 원하는 것이 준비됐으면 그것, 아니면 만들기를 요청하고 다음 작은 준비된 LOD (없으면 가장 작은 LOD 를 동기로 만든다).
    /// </summary>
    public LodLevel? Resolve(LodSprite s, int want)
    {
        PumpAll();
        var lv = s.Levels[want];
        lv.LastUsed = Now;
        if (lv.Ready) return lv;
        Request(lv);
        if (lv.Ready) return lv;
        Fallbacks++;
        // 가운데 LOD 도 같이 만들어 둔다: 안 그러면 LOD0 가 만들어지는 동안 (처음 확대 · 30초 뒤 다시 확대) 가장 작은 LOD 로만 보인다
        for (int i = want + 1; i < s.Levels.Length - 1; i++) if (!s.Levels[i].Ready) Request(s.Levels[i]);
        for (int i = want + 1; i < s.Levels.Length; i++)
            if (s.Levels[i].Ready) { s.Levels[i].LastUsed = Now; return s.Levels[i]; }
        var low = s.Levels[^1];
        if (low.State == 0) { Request(low); if (low.Ready) { low.LastUsed = Now; return low; } }
        for (int i = want - 1; i >= 0; i--)
            if (s.Levels[i].Ready) { s.Levels[i].LastUsed = Now; return s.Levels[i]; }
        return null;
    }

    /// <summary>ci 칸이 그려지는 다른 준비된 LOD (from 보다 작은 쪽 먼저, 그다음 큰 쪽). 없으면 null.</summary>
    public LodLevel? FindCell(LodSprite s, int from, int ci)
    {
        for (int i = from + 1; i < s.Levels.Length; i++)
            if (s.Levels[i].Ready && ci < s.Levels[i].CW.Length && s.Levels[i].CW[ci] > 0) { s.Levels[i].LastUsed = Now; return s.Levels[i]; }
        for (int i = from - 1; i >= 0; i--)
            if (s.Levels[i].Ready && ci < s.Levels[i].CW.Length && s.Levels[i].CW[ci] > 0) { s.Levels[i].LastUsed = Now; return s.Levels[i]; }
        return null;
    }

    void Request(LodLevel lv)
    {
        if (lv.State != 0 || disposed) return;
        bool isShared = lv.Group > 0;
        var levels = isShared ? groups[lv.Group].Where(l => l.State == 0).ToList() : new List<LodLevel> { lv };
        // 가장 작은 LOD (아래로 물러설 곳이 없음) 는 동기
        bool sync = !Async || levels.Any(l => l.Group == l.Owner.Levels.Length - 1);
        foreach (var l in levels) l.State = 1;
        var job = new Job { Group = lv.Group, Shared = isShared, Levels = levels };
        if (sync)
        {
            SyncBuilds++;
            Build(job, dir);
            UploadAll(job);
            Apply(job);
        }
        else
        {
            AsyncBuilds++;
            var d = dir;
            Task.Run(() =>
            {
                try { Build(job, d); }
                catch (Exception e) { job.Error = e.Message; }
                done.Enqueue(job);
            });
        }
    }

    // ───────── 프레임마다: 업로드 · 내리기 ─────────

    /// <summary>프레임마다 한 번 (그리기에서 불러도 됨 — 6ms 안에 다시 부르면 무시).</summary>
    public static void PumpAll()
    {
        long t = clock.ElapsedTicks;
        if (t - lastPumpTicks < Stopwatch.Frequency * 6 / 1000) return;
        lastPumpTicks = t;
        Now = clock.Elapsed.TotalSeconds;
        LodAtlas[] atlases;
        lock (live) atlases = live.ToArray();
        long budget = UploadBytesPerFrame;
        long t0 = Stopwatch.GetTimestamp();
        foreach (var a in atlases) budget = a.Pump(budget);
        EvictLod0(atlases);
        if (budget < UploadBytesPerFrame) PumpMsMax = Math.Max(PumpMsMax, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
    }

    long Pump(long budget)
    {
        while (done.TryDequeue(out var j))
        {
            if (j.Error != null) { Console.Error.WriteLine($"[lod] 만들기 실패 L{j.Group}: {j.Error}"); foreach (var l in j.Levels) l.State = 0; continue; }
            uploads.Enqueue(j);
        }
        while (budget > 0 && uploads.Count > 0)
        {
            var j = uploads.Peek();
            budget = UploadSome(j, budget);
            if (j.UpPage >= j.Pages.Count) { uploads.Dequeue(); Apply(j); }
        }
        return budget;
    }

    /// <summary>LOD 0 전용 페이지: EvictSec 동안 안 쓴 것, 그리고 예산 초과분을 오래된 것부터 (0.5초 안에 쓴 것은 남김).</summary>
    static void EvictLod0(LodAtlas[] atlases)
    {
        long resident = 0;
        foreach (var a in atlases)
            if (a.groups.TryGetValue(0, out var l0))
                foreach (var lv in l0)
                {
                    if (!lv.Ready || lv.OwnPages == null) continue;
                    if (Now - lv.LastUsed > EvictSec) a.Evict(lv, "안 쓴 지 오래");
                    else resident += lv.Bytes;
                }
        while (resident > Lod0BudgetBytes)
        {
            LodAtlas? ba = null; LodLevel? best = null;
            foreach (var a in atlases)
                if (a.groups.TryGetValue(0, out var l0))
                    foreach (var lv in l0)
                        if (lv.Ready && lv.OwnPages != null && Now - lv.LastUsed > 0.5 && (best == null || lv.LastUsed < best.LastUsed)) { best = lv; ba = a; }
            if (best == null) break;   // 화면에 보이는 것만으로 예산 초과 → 그대로 둔다
            resident -= best.Bytes;
            ba!.Evict(best, "예산 초과");
        }
    }

    void Evict(LodLevel lv, string why)
    {
        foreach (var t in lv.OwnPages!) Raylib.UnloadTexture(t);
        Account(lv.Group, -lv.Bytes);
        Console.Error.WriteLine($"[lod] L{lv.Group} {lv.Owner.Key} 내림 ({why}): -{lv.Bytes / 1048576.0:0.0} MB → LOD0 {GroupBytes[0] / 1048576.0:0.0} MB");
        lv.OwnPages = null; lv.State = 0; lv.Bytes = 0;
        lv.CellTex = Array.Empty<Texture2D>();
        Evictions++;
    }

    void Account(int group, long delta)
    {
        Bytes += delta; TotalBytes += delta;
        TotalPeak = Math.Max(TotalPeak, TotalBytes);
        if (group < GroupBytes.Length) { GroupBytes[group] += delta; GroupPeak[group] = Math.Max(GroupPeak[group], GroupBytes[group]); }
    }

    // ───────── 만들기 (작업자 스레드에서도 돎: 레벨의 읽기 전용 정보만 읽는다) ─────────

    sealed class Crop { public LodLevel Lv = null!; public int Ci, W, H, OX, OY, Page, X, Y; public byte[]? Px; }
    sealed class Job
    {
        public int Group; public bool Shared; public List<LodLevel> Levels = null!;
        public List<Crop> Crops = new();
        public List<(int W, int H, byte[] Px)> Pages = new();
        public long Raw; public int Sheets; public string? Error;
        public Texture2D[] Tex = Array.Empty<Texture2D>();
        public int UpPage, UpRow;
    }

    static unsafe void Build(Job job, string dir)
    {
        foreach (var lv in job.Levels)
        {
            var s = lv.Owner;
            int nf = s.FrameNames.Length, nd = Math.Max(1, s.DirCount);
            for (int i = 0; i < lv.SheetFiles.Length; i++)
            {
                var img = Raylib.LoadImage(Path.Combine(dir, lv.SheetFiles[i]));
                if (img.Width == 0) { Console.Error.WriteLine($"[lod] 시트 읽기 실패: {lv.SheetFiles[i]}"); continue; }
                Raylib.ImageFormat(ref img, PixelFormat.UncompressedR8G8B8A8);
                FoldTeam(ref img, dir, lv.TeamFiles[i]);
                job.Raw += (long)img.Width * img.Height * 4; job.Sheets++;
                byte* b = (byte*)img.Data;
                int iw = img.Width, ih = img.Height;
                for (int f = 0; f < nf; f++)
                {
                    if (lv.CellSheet[f] != i || !s.Used[f]) continue;
                    int fw = lv.CellFW[f], fh = lv.CellFH[f];
                    int x0 = lv.CellCol[f] * fw;
                    for (int r = 0; r < nd; r++)
                    {
                        int y0 = r * fh;
                        if (x0 + fw > iw || y0 + fh > ih) continue;
                        // 불투명 픽셀의 경계 상자
                        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                        for (int y = 0; y < fh; y++)
                        {
                            byte* row = b + ((long)(y0 + y) * iw + x0) * 4;
                            for (int x = 0; x < fw; x++)
                                if (row[x * 4 + 3] != 0)
                                {
                                    if (x < minX) minX = x;
                                    if (x > maxX) maxX = x;
                                    if (y < minY) minY = y;
                                    maxY = y;
                                }
                        }
                        if (maxX < 0) continue;   // 빈 칸
                        // 투명 1px 테두리를 남긴다: 사각형 변이 불투명 픽셀 경계와 겹치지 않게 (래스터화가 잘라내기 전과 같도록) — 칸 사이 틈 역할도 함
                        minX = Math.Max(0, minX - Pad); minY = Math.Max(0, minY - Pad);
                        maxX = Math.Min(fw - 1, maxX + Pad); maxY = Math.Min(fh - 1, maxY + Pad);
                        int cw = maxX - minX + 1, ch = maxY - minY + 1;
                        var px = GC.AllocateUninitializedArray<byte>(cw * ch * 4);
                        fixed (byte* d = px)
                            for (int y = 0; y < ch; y++)
                                Buffer.MemoryCopy(b + ((long)(y0 + minY + y) * iw + x0 + minX) * 4, d + (long)y * cw * 4, (long)cw * 4, (long)cw * 4);
                        job.Crops.Add(new Crop { Lv = lv, Ci = r * nf + f, W = cw, H = ch, OX = minX, OY = minY, Px = px });
                    }
                }
                Raylib.UnloadImage(img);
            }
        }
        if (job.Crops.Count == 0) return;
        // 선반(shelf) 배치: 높이 순, 페이지 폭 ≈ √면적 (≤ 4096), 높이 4096 을 넘으면 다음 페이지
        job.Crops.Sort((a, c) => c.H != a.H ? c.H.CompareTo(a.H) : c.W.CompareTo(a.W));
        long area = job.Crops.Sum(c => (long)(c.W + Gutter) * (c.H + Gutter));
        int maxW = job.Crops.Max(c => c.W);
        int pw = Math.Clamp((int)Math.Ceiling(Math.Sqrt(area) * 1.04), maxW, MaxPage);
        pw = (pw + 3) & ~3;
        int px0 = 0, py = 0, shelf = 0, page = 0;
        var heights = new List<int>();
        foreach (var c in job.Crops)
        {
            if (px0 > 0 && px0 + c.W > pw) { px0 = 0; py += shelf + Gutter; shelf = 0; }
            if (py > 0 && py + c.H > MaxPage) { heights.Add(py - Gutter); page++; px0 = 0; py = 0; shelf = 0; }
            c.Page = page; c.X = px0; c.Y = py;
            px0 += c.W + Gutter; shelf = Math.Max(shelf, c.H);
        }
        heights.Add(py + shelf);
        for (int p = 0; p <= page; p++)
        {
            int h = heights[p];
            var buf = new byte[(long)pw * h * 4];   // 0 = 투명
            fixed (byte* d = buf)
                foreach (var c in job.Crops)
                {
                    if (c.Page != p) continue;
                    fixed (byte* sp = c.Px)
                        for (int y = 0; y < c.H; y++)
                            Buffer.MemoryCopy(sp + (long)y * c.W * 4, d + ((long)(c.Y + y) * pw + c.X) * 4, (long)c.W * 4, (long)c.W * 4);
                }
            job.Pages.Add((pw, h, buf));
        }
        foreach (var c in job.Crops) c.Px = null;
    }

    static unsafe void FoldTeam(ref Image img, string dir, string? teamFile)
    {
        if (teamFile == null || !File.Exists(Path.Combine(dir, teamFile))) return;
        var team = Raylib.LoadImage(Path.Combine(dir, teamFile));
        if (team.Width == img.Width && team.Height == img.Height)
        {
            Raylib.ImageFormat(ref team, PixelFormat.UncompressedR8G8B8A8);
            byte* b = (byte*)img.Data, t = (byte*)team.Data;
            int n = img.Width * img.Height;
            for (int p = 0; p < n; p++)
            {
                byte* tp = t + p * 4;
                if (tp[3] == 0) { if (b[p * 4 + 3] == TeamAlpha) b[p * 4 + 3] = 255; continue; }   // 우연한 254 는 255 로
                byte g = tp[0] == 0 ? (byte)1 : tp[0];
                byte* bp = b + p * 4;
                bp[0] = g; bp[1] = g; bp[2] = g; bp[3] = TeamAlpha;
            }
        }
        else Console.Error.WriteLine($"[lod] 팀 시트 크기가 다름: {teamFile}");
        Raylib.UnloadImage(team);
    }

    // ───────── 업로드 (본 스레드) ─────────

    static unsafe Texture2D NewPage(int w, int h, void* data)
    {
        uint id = Rlgl.LoadTexture(data, w, h, PixelFormat.UncompressedR8G8B8A8, 1);
        var tex = new Texture2D { Id = id, Width = w, Height = h, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
        return tex;
    }

    static unsafe void UploadAll(Job j)
    {
        j.Tex = new Texture2D[j.Pages.Count];
        for (int p = 0; p < j.Pages.Count; p++)
            fixed (byte* d = j.Pages[p].Px) j.Tex[p] = NewPage(j.Pages[p].W, j.Pages[p].H, d);
        j.UpPage = j.Pages.Count;
    }

    /// <summary>예산만큼 줄 단위로 올린다 (페이지는 빈 텍스처로 먼저 만들고 glTexSubImage). 남은 예산을 돌려준다.</summary>
    static unsafe long UploadSome(Job j, long budget)
    {
        if (j.Tex.Length != j.Pages.Count) j.Tex = new Texture2D[j.Pages.Count];
        while (budget > 0 && j.UpPage < j.Pages.Count)
        {
            var (w, h, px) = j.Pages[j.UpPage];
            if (j.UpRow == 0) j.Tex[j.UpPage] = NewPage(w, h, null);
            int rows = (int)Math.Clamp(budget / ((long)w * 4), 1, h - j.UpRow);
            fixed (byte* d = px)
                Rlgl.UpdateTexture(j.Tex[j.UpPage].Id, 0, j.UpRow, w, rows, PixelFormat.UncompressedR8G8B8A8, d + (long)j.UpRow * w * 4);
            budget -= (long)rows * w * 4;
            j.UpRow += rows;
            if (j.UpRow >= h) { j.Pages[j.UpPage] = (w, h, Array.Empty<byte>()); j.UpPage++; j.UpRow = 0; }
        }
        return budget;
    }

    void Apply(Job j)
    {
        if (disposed)
        {
            foreach (var t in j.Tex) if (t.Id != 0) Raylib.UnloadTexture(t);
            return;
        }
        foreach (var lv in j.Levels)
        {
            int n = lv.Owner.FrameNames.Length * Math.Max(1, lv.Owner.DirCount);
            lv.CellTex = new Texture2D[n];
            lv.CX = new int[n]; lv.CY = new int[n]; lv.CW = new int[n]; lv.CH = new int[n]; lv.COX = new int[n]; lv.COY = new int[n];
            lv.Bytes = 0; lv.RawBytes = 0;
        }
        foreach (var c in j.Crops)
        {
            var lv = c.Lv;
            lv.CellTex[c.Ci] = j.Tex[c.Page];
            lv.CX[c.Ci] = c.X; lv.CY[c.Ci] = c.Y; lv.CW[c.Ci] = c.W; lv.CH[c.Ci] = c.H;
            lv.COX[c.Ci] = c.OX; lv.COY[c.Ci] = c.OY;
            if (j.Shared) lv.Bytes += (long)c.W * c.H * 4;
        }
        foreach (var lv in j.Levels)
            lv.RawBytes = lv.SheetFiles.Length > 0 ? RawOf(lv) : 0;
        long bytes = j.Pages.Sum(p => (long)p.W * p.H * 4);
        if (j.Shared)
        {
            if (!shared.TryGetValue(j.Group, out var list)) shared[j.Group] = list = new List<Texture2D>();
            list.AddRange(j.Tex);
        }
        else
        {
            var lv = j.Levels[0];
            lv.OwnPages = j.Tex.ToList();
            lv.Bytes = bytes;
        }
        foreach (var lv in j.Levels) lv.State = 2;
        Account(j.Group, bytes);
        for (int p = 0; p < j.Pages.Count; p++) Info += (Info.Length > 0 ? ", " : "") + $"L{j.Group}:{j.Pages[p].W}x{j.Pages[p].H}";
        if (Info.Length > 300) Info = Info[^300..];
        string what = j.Shared ? $"공유 {j.Levels.Count}종" : j.Levels[0].Owner.Key;
        Console.Error.WriteLine($"[lod] L{j.Group} {what}: 시트 {j.Sheets}장 칸 {j.Crops.Count} -> 페이지 {j.Pages.Count}장 {bytes / 1048576.0:0.0} MB (자르기 전 {j.Raw / 1048576.0:0.0} MB) · LOD {GroupBytes[0] / 1048576.0:0}/{GroupBytes[1] / 1048576.0:0}/{GroupBytes[2] / 1048576.0:0} MB");
    }

    /// <summary>자르기 전 시트 바이트 (측정용, PNG 머리에서 크기만 읽는다).</summary>
    long RawOf(LodLevel lv)
    {
        long sum = 0;
        Span<byte> hd = stackalloc byte[24];   // 루프 밖: 스택 할당 1회 (CA2014)
        foreach (var f in lv.SheetFiles)
        {
            try
            {
                using var fs = File.OpenRead(Path.Combine(dir, f));
                if (fs.Read(hd) == 24) sum += 4L * ((hd[16] << 24) | (hd[17] << 16) | (hd[18] << 8) | hd[19]) * ((hd[20] << 24) | (hd[21] << 16) | (hd[22] << 8) | hd[23]);
            }
            catch (IOException) { }
        }
        return sum;
    }

    // ───────── 보고 ─────────

    /// <summary>한 줄: HUD 성능 표시용 (ASCII).</summary>
    public static string ShortInfo =>
        $"tex L0 {GroupBytes[0] / 1048576.0:0}/{Lod0BudgetBytes / 1048576}MB L1 {GroupBytes[1] / 1048576.0:0} L2 {GroupBytes[2] / 1048576.0:0} (peak {TotalPeak / 1048576.0:0})";

    /// <summary>여러 줄: LOD 별 현재 · 최대, 종류별 상주 바이트 (자르기 전 대비).</summary>
    public static string Summary()
    {
        var sb = new StringBuilder();
        sb.Append($"[lod] 메모리: 현재 {TotalBytes / 1048576.0:0.0} MB (최대 {TotalPeak / 1048576.0:0.0} MB)");
        for (int g = 0; g < 3; g++) sb.Append($" · L{g} {GroupBytes[g] / 1048576.0:0.0} (최대 {GroupPeak[g] / 1048576.0:0.0})");
        sb.Append($" · 비동기 {AsyncBuilds} 동기 {SyncBuilds} 내림 {Evictions} · 물러서 그림 {Fallbacks} · 업로드 프레임 최대 {PumpMsMax:0.0}ms · LOD0 예산 {Lod0BudgetBytes / 1048576} MB");
        LodAtlas[] atlases;
        lock (live) atlases = live.ToArray();
        foreach (var a in atlases)
            foreach (var (g, levels) in a.groups.OrderBy(k => k.Key))
            {
                var ready = levels.Where(l => l.Ready).OrderByDescending(l => l.Bytes).ToList();
                if (ready.Count == 0) continue;
                sb.Append($"\n[lod]  L{g}: ");
                sb.Append(string.Join(", ", ready.Select(l => $"{l.Owner.Key.Replace("unit.", "")} {l.Bytes / 1048576.0:0.0}/{l.RawBytes / 1048576.0:0.0}")));
            }
        return sb.ToString();
    }

    public void Dispose()
    {
        disposed = true;
        foreach (var (g, list) in shared)
            foreach (var t in list) { Raylib.UnloadTexture(t); Account(g, -(long)t.Width * t.Height * 4); }
        foreach (var (g, levels) in groups)
            foreach (var lv in levels)
            {
                if (lv.OwnPages != null) { foreach (var t in lv.OwnPages) Raylib.UnloadTexture(t); Account(g, -lv.Bytes); }
                lv.OwnPages = null; lv.State = 0; lv.Bytes = 0;
            }
        foreach (var j in uploads) foreach (var t in j.Tex) if (t.Id != 0) Raylib.UnloadTexture(t);
        uploads.Clear();
        shared.Clear(); Bytes = 0; Info = "";
        lock (live) live.Remove(this);
    }
}
