using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// 지형을 청크 텍스처로 구워 그린다. 줌 LOD 3단 (타일 32 · 64 · 128px, 청크 = 1024px 정사각 = 32 · 16 · 8 타일).
/// 지형 경계는 굽는 시점에 섞는다: 2×2 이웃 타일의 가중치 장 (중앙 절반은 순수, 경계 쪽 절반에서 선형 전이)
/// + 지형별 월드 좌표 값 노이즈로 경계를 울퉁불퉁하게, 물가엔 젖은 모래 띠 · 얕은 물 · 거품, 바위엔 그늘.
/// 노이즈는 타일 좌표 기준이라 LOD 사이에서 모양이 같고, 해시만 쓰므로 결정적.
/// 굽기는 백그라운드 스레드 (순수 함수, 지형 사본만 읽음), GPU 업로드만 메인 스레드에서 프레임당 몇 개.
/// 청크가 아직 없으면 1타일=1px 개관 텍스처로 임시 표시. 청크는 LOD 별 상한 + 오래 안 쓴 것 해제.
/// </summary>
public sealed partial class MapRenderer : IDisposable
{
    const int TS = Config.TileSize, ChunkPx = 1024;
    static readonly int NT = Terrains.Count;
    // 그림 슬롯 = 지형 종류 NT 개 + 이웃으로 정해지는 파생 타일 (숲 가장자리/깊은 숲, 풀 고지, 참호 조각 10종).
    static readonly string[] TrenchPieces = { "h", "v", "ne", "nw", "se", "sw", "end_n", "end_e", "end_s", "end_w" };
    static readonly int SlotEdge = NT, SlotDeep = NT + 1, SlotHigh = NT + 2, SlotTrench = NT + 3;
    static readonly int NS = NT + 3 + TrenchPieces.Length;
    static readonly string[] RimSides = { "n", "e", "s", "w" };
    static readonly int[] Res = { 32, 64, 128 };
    static readonly int[] Caps = { Config.MaxLoadedChunks, 24, 24 };   // 1024² 청크: 32px 5.3MB (밉맵) · 64/128px 4MB
    public const float Lod64Zoom = 1.15f, Lod128Zoom = 2.3f;
    const float K = 1.5f;      // 전이 기울기: 타일 경계 양쪽 1/3 타일에서 섞임
    const float Amp = 1.0f;    // 노이즈 진폭 (가중치 단위) → 경계 ±0.3 타일 흔들림
    const int MaxUploadsPerFrame = 3;
    const int GNMax = 17;      // 타일당 노이즈 격자 (64·128px = 17×17, 32px = 9×9)

    sealed class Lod
    {
        public int R, CT, CX, CY, Loaded;
        public Texture2D?[] Tex = Array.Empty<Texture2D?>();
        public long[] Used = Array.Empty<long>();
        public bool[] Pending = Array.Empty<bool>();
        public Color[][][]? Src;   // [슬롯][변형] → R×R 픽셀
        public Color[][]? Rim;     // [n,e,s,w] → R×R 알파 오버레이
        public int[] Gen = Array.Empty<int>(), BuiltGen = Array.Empty<int>();   // 지형 편집 세대 (MapRenderer.Edits.cs): 다르면 다시 굽는다
    }

    readonly GameMap map;
    readonly int W, H, seed;
    readonly byte[] ter;
    byte[]? slot;              // 칸마다 그림 슬롯 (EnsureSources(0) 가 한 번 채움)
    bool useRim;               // 테마에 고지 가장자리 오버레이가 있음 (없으면 옛 밝기 선)
    readonly bool[] hi;        // 고지 (굽기 때 밝게 + 평지 쪽 경계선)
    readonly Lod[] lods = new Lod[3];
    readonly ConcurrentQueue<(int lod, int idx, Color[]? px, int w, int h, int gen)> done = new();
    readonly int maxWorkers = int.TryParse(Environment.GetEnvironmentVariable("KD_MAPWORKERS"), out int mw) && mw > 0 ? mw : Math.Clamp(Environment.ProcessorCount - 1, 1, 6);
    int inFlight;
    long frame;
    Texture2D overview;
    bool disposed;

    // 측정 (KD_MAPSTATS=1 이면 종료 시 출력)
    long drawTicks, draws, builds, buildTicks, uploads, bytesNow, bytesPeak;
    readonly long[] lodTicks = new long[3], lodBuilds = new long[3];

    public MapRenderer(GameMap map)
    {
        this.map = map;
        W = map.Width; H = map.Height; seed = map.Seed;
        ter = new byte[W * H];
        hi = new bool[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) { ter[y * W + x] = (byte)map.Get(x, y); hi[y * W + x] = map.IsHigh(x, y); }
        for (int l = 0; l < 3; l++)
        {
            int ct = ChunkPx / Res[l], cx = (W + ct - 1) / ct, cy = (H + ct - 1) / ct;
            lods[l] = new Lod { R = Res[l], CT = ct, CX = cx, CY = cy, Tex = new Texture2D?[cx * cy], Used = new long[cx * cy], Pending = new bool[cx * cy], Gen = new int[cx * cy], BuiltGen = new int[cx * cy] };
        }
    }

    /// <summary>BeginMode2D 안에서 호출.</summary>
    public void Draw(RtsCamera cam, bool showGrid)
    {
        long t0 = Stopwatch.GetTimestamp();
        frame++;
        ApplyEdits();   // 시뮬 중 바뀐 칸 (참호 완성 · 포탄 구덩이) → 지형 사본 · 개관 · 청크 세대 (MapRenderer.Edits.cs)
        PumpUploads();
        var (min, max) = cam.VisibleWorld();
        int want = cam.Zoom >= Lod128Zoom ? 2 : cam.Zoom >= Lod64Zoom ? 1 : 0;

        if (frame == 1)
        {
            EnsureSources(0);   // GPU · 이미지 로딩은 창이 생긴 뒤 첫 Draw 에서
            BuildOverview();
            if (Environment.GetEnvironmentVariable("KD_MAPFOCUS") == "shore") FocusShore(cam);   // 스크린샷 · 측정용
            long s0 = Stopwatch.GetTimestamp();
            BuildVisibleNow(lods[0], min, max);   // 첫 화면은 기다려서라도 완성본
            syncTicks = Stopwatch.GetTimestamp() - s0;
        }
        bool hiComplete = want > 0 && AllLoaded(lods[want], min, max);
        if (!hiComplete) DrawLod(0, min, max, true);
        if (want > 0) { EnsureSources(want); DrawLod(want, min, max, false); }
        EvictIfNeeded(want);

        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, W * TS, H * TS), 2f / cam.Zoom, Palette.Alpha(Color.Black, 160));
        if (showGrid) DrawGrid(cam, min, max);
        drawTicks += Stopwatch.GetTimestamp() - t0; draws++;
    }

    void FocusShore(RtsCamera cam)
    {
        var (vmin, vmax) = cam.VisibleWorld();
        Vector2 c = (vmin + vmax) / 2;
        float best = float.MaxValue; Vector2 pick = c;
        for (int y = 1; y < H - 1; y++)
        for (int x = 1; x < W - 1; x++)
        {
            if (ter[y * W + x] != (byte)TerrainType.Water || ter[y * W + x + 1] == (byte)TerrainType.Water) continue;
            float d = Vector2.DistanceSquared(c, new Vector2(x, y));
            if (d < best) { best = d; pick = new Vector2(x + 1, y + 0.5f); }
        }
        cam.CenterOn(pick);
    }

    long syncTicks;

    public string StatsLine =>
        $"draws {draws} avg {Ms(drawTicks - syncTicks) / Math.Max(1, draws - 1):0.000}ms (frame-1 sync bake {Ms(syncTicks):0}ms) · builds {builds} avg {Ms(buildTicks) / Math.Max(1, builds):0.0}ms (worker; per LOD {Ms(lodTicks[0]) / Math.Max(1, lodBuilds[0]):0}/{Ms(lodTicks[1]) / Math.Max(1, lodBuilds[1]):0}/{Ms(lodTicks[2]) / Math.Max(1, lodBuilds[2]):0}ms) · uploads {uploads} · " +
        $"chunks {lods[0].Loaded}/{lods[1].Loaded}/{lods[2].Loaded} · cache {bytesNow / 1048576.0:0.0}MB peak {bytesPeak / 1048576.0:0.0}MB";

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    // ───────── 그리기 · 캐시 ─────────

    static (int x0, int y0, int x1, int y1) Range(Lod L, Vector2 min, Vector2 max) =>
        (Math.Max(0, (int)MathF.Floor(min.X / L.CT)), Math.Max(0, (int)MathF.Floor(min.Y / L.CT)),
         Math.Min(L.CX - 1, (int)MathF.Floor(max.X / L.CT)), Math.Min(L.CY - 1, (int)MathF.Floor(max.Y / L.CT)));

    bool AllLoaded(Lod L, Vector2 min, Vector2 max)
    {
        var (x0, y0, x1, y1) = Range(L, min, max);
        for (int cy = y0; cy <= y1; cy++)
        for (int cx = x0; cx <= x1; cx++)
            if (L.Tex[cy * L.CX + cx] == null) return false;
        return true;
    }

    void DrawLod(int l, Vector2 min, Vector2 max, bool placeholder)
    {
        Lod L = lods[l];
        var (x0, y0, x1, y1) = Range(L, min, max);
        for (int cy = y0; cy <= y1; cy++)
        for (int cx = x0; cx <= x1; cx++)
        {
            int i = cy * L.CX + cx;
            int tx = cx * L.CT, ty = cy * L.CT, tw = Math.Min(L.CT, W - tx), th = Math.Min(L.CT, H - ty);
            var dest = new Rectangle(tx * TS, ty * TS, tw * TS, th * TS);
            if (L.Tex[i] is Texture2D t)
            {
                L.Used[i] = frame;
                if (L.BuiltGen[i] != L.Gen[i]) Request(l, i);   // 낡은 청크: 새로 굽는 동안 옛 그림을 그대로 쓴다
                Raylib.DrawTexturePro(t, new Rectangle(0, 0, t.Width, t.Height), dest, Vector2.Zero, 0f, Color.White);
            }
            else
            {
                Request(l, i);
                if (placeholder) Raylib.DrawTexturePro(overview, new Rectangle(tx, ty, tw, th), dest, Vector2.Zero, 0f, Color.White);
            }
        }
    }

    void Request(int l, int i)
    {
        Lod L = lods[l];
        if (L.Pending[i] || inFlight >= maxWorkers || L.Src == null) return;
        L.Pending[i] = true;
        Interlocked.Increment(ref inFlight);
        int cx = i % L.CX, cy = i / L.CX, gen = L.Gen[i];
        Task.Run(() =>
        {
            Color[]? px = null; int w = 0, h = 0;
            try
            {
                px = ArrayPool<Color>.Shared.Rent(ChunkPx * ChunkPx);
                long b0 = Stopwatch.GetTimestamp();
                BuildPixels(L, cx, cy, px, out w, out h);
                long dtk = Stopwatch.GetTimestamp() - b0;
                Interlocked.Add(ref buildTicks, dtk); Interlocked.Add(ref lodTicks[l], dtk); Interlocked.Increment(ref lodBuilds[l]);
            }
            catch (Exception ex) { Console.Error.WriteLine("[map] chunk build failed: " + ex.Message); }
            finally
            {
                done.Enqueue((l, i, px, w, h, gen));
                Interlocked.Decrement(ref inFlight);
            }
        });
    }

    void BuildVisibleNow(Lod L, Vector2 min, Vector2 max)
    {
        var (x0, y0, x1, y1) = Range(L, min, max);
        var list = new List<int>();
        for (int cy = y0; cy <= y1; cy++)
        for (int cx = x0; cx <= x1; cx++) list.Add(cy * L.CX + cx);
        var results = new (Color[] px, int w, int h)[list.Count];
        Parallel.For(0, list.Count, k =>
        {
            var px = ArrayPool<Color>.Shared.Rent(ChunkPx * ChunkPx);
            long b0 = Stopwatch.GetTimestamp();
            BuildPixels(L, list[k] % L.CX, list[k] / L.CX, px, out int w, out int h);
            long dtk = Stopwatch.GetTimestamp() - b0;
            Interlocked.Add(ref buildTicks, dtk); Interlocked.Add(ref lodTicks[0], dtk); Interlocked.Increment(ref lodBuilds[0]);
            results[k] = (px, w, h);
        });
        for (int k = 0; k < list.Count; k++) Upload(0, list[k], results[k].px, results[k].w, results[k].h, L.Gen[list[k]]);
    }

    void PumpUploads()
    {
        for (int n = 0; n < MaxUploadsPerFrame && done.TryDequeue(out var r); n++)
        {
            lods[r.lod].Pending[r.idx] = false;
            if (r.px != null && r.w > 0) Upload(r.lod, r.idx, r.px, r.w, r.h, r.gen);
            else if (r.px != null) ArrayPool<Color>.Shared.Return(r.px);
        }
    }

    unsafe void Upload(int l, int i, Color[] px, int w, int h, int gen)
    {
        Lod L = lods[l];
        builds++;
        // 굽는 사이 지형이 또 바뀌었으면 버린다 (다음 프레임에 다시 요청). 같은 세대인데 이미 있으면 중복.
        if (disposed || gen != L.Gen[i] || (L.Tex[i] != null && L.BuiltGen[i] == gen)) { ArrayPool<Color>.Shared.Return(px); return; }
        if (L.Tex[i] != null) Unload(L, i);   // 낡은 청크 교체
        L.BuiltGen[i] = gen;
        Texture2D tex;
        fixed (Color* p = px)
        {
            var img = new Image { Data = p, Width = w, Height = h, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
            tex = Raylib.LoadTextureFromImage(img);
        }
        ArrayPool<Color>.Shared.Return(px);
        long bytes = (long)w * h * 4;
        if (l == 0)
        {
            // 축소 (줌 < 1) 용 밉맵. 확대 LOD 는 점 필터 (픽셀 그림 그대로).
            Raylib.GenTextureMipmaps(ref tex);
            Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
            bytes = bytes * 4 / 3;
        }
        else Raylib.SetTextureFilter(tex, TextureFilter.Point);
        Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
        L.Tex[i] = tex; L.Used[i] = frame; L.Loaded++;
        uploads++;
        bytesNow += bytes; bytesPeak = Math.Max(bytesPeak, bytesNow);
    }

    void Unload(Lod L, int i)
    {
        var t = L.Tex[i]!.Value;
        bytesNow -= (long)t.Width * t.Height * 4 * (t.Mipmaps > 1 ? 4 : 3) / 3;
        Raylib.UnloadTexture(t);
        L.Tex[i] = null; L.Loaded--;
    }

    void EvictIfNeeded(int want)
    {
        for (int l = 0; l < 3; l++)
        {
            Lod L = lods[l];
            if (L.Loaded == 0) continue;
            bool over = L.Loaded > Caps[l];
            bool idle = l != want && l != 0 && frame % 30 == 0;   // 안 쓰는 고해상도 LOD 는 서서히 비운다
            if (!over && !idle) continue;
            int oldest = -1;
            for (int i = 0; i < L.Tex.Length; i++)
                if (L.Tex[i] != null && L.Used[i] < frame && (oldest < 0 || L.Used[i] < L.Used[oldest])) oldest = i;
            if (oldest >= 0 && (over || L.Used[oldest] < frame - 600)) Unload(L, oldest);
        }
    }

    void DrawGrid(RtsCamera cam, Vector2 min, Vector2 max)
    {
        int x0 = Math.Max(0, (int)min.X), x1 = Math.Min(W, (int)max.X + 1);
        int y0 = Math.Max(0, (int)min.Y), y1 = Math.Min(H, (int)max.Y + 1);
        float thick = 1f / cam.Zoom;
        for (int x = x0; x <= x1; x++)
            Raylib.DrawLineEx(new Vector2(x * TS, y0 * TS), new Vector2(x * TS, y1 * TS), thick, Palette.Grid);
        for (int y = y0; y <= y1; y++)
            Raylib.DrawLineEx(new Vector2(x0 * TS, y * TS), new Vector2(x1 * TS, y * TS), thick, Palette.Grid);
    }

    // ───────── 원본 타일 픽셀 ─────────

    /// <summary>LOD 의 원본 변형 픽셀을 준비 (메인 스레드). 테마 그림이 없으면 절차 생성 타일.</summary>
    unsafe void EnsureSources(int l)
    {
        Lod L = lods[l];
        if (L.Src != null) return;
        var th = Theme.Current;
        slot ??= BuildSlots(th);
        var src = new Color[NS][][];
        for (int t = 0; t < NS; t++)
        {
            var (name, fb) = SlotInfo(t, houseYard);
            var imgs = th?.TerrainImages(name, L.R) ?? th?.TerrainImages(fb, L.R);
            bool own = imgs == null;
            imgs ??= ProceduralTiles(fb, L.R);
            src[t] = new Color[imgs.Length][];
            for (int v = 0; v < imgs.Length; v++)
            {
                var a = new Color[L.R * L.R];
                Color* p = Raylib.LoadImageColors(imgs[v]);
                new ReadOnlySpan<Color>(p, a.Length).CopyTo(a);
                Raylib.UnloadImageColors(p);
                src[t][v] = a;
                if (own) Raylib.UnloadImage(imgs[v]);
            }
        }
        L.Src = src;   // CPU 메모리 ≈ 슬롯 23 × ≤6변형 × R² × 4B (128px 에서 ≈ 9MB)
        if (useRim)
        {
            L.Rim = new Color[4][];
            for (int k = 0; k < 4; k++)
            {
                var imgs = th!.TerrainImages("high_ground_rim_" + RimSides[k], L.R)!;
                var a = new Color[L.R * L.R];
                Color* p = Raylib.LoadImageColors(imgs[0]);
                new ReadOnlySpan<Color>(p, a.Length).CopyTo(a);
                Raylib.UnloadImageColors(p);
                L.Rim[k] = a;
            }
        }
    }

    /// <summary>슬롯의 테마 이름과 대체 지형 (테마에 그 이름이 없을 때).</summary>
    /// houseYard: 가옥 그림이 있으면 가옥 칸 바닥은 마당 흙 (집은 BuildingRenderer 가 건물 단위로 덮어 그림).
    static (string name, TerrainType fb) SlotInfo(int t, bool houseYard)
    {
        if (t < NT)
        {
            var ty = (TerrainType)t;
            string n = ty switch { TerrainType.Forest => "forest_deep", TerrainType.Wire => "barbed_wire", TerrainType.Trench => "trench_h", TerrainType.House when houseYard => "dirt", _ => ty.ToString().ToLowerInvariant() };
            return (n, ty);
        }
        if (t == SlotEdge) return ("forest_edge", TerrainType.Forest);
        if (t == SlotDeep) return ("forest_deep", TerrainType.Forest);
        if (t == SlotHigh) return ("high_ground", TerrainType.Grass);
        return ("trench_" + TrenchPieces[t - SlotTrench], TerrainType.Trench);
    }

    bool slotSplit, slotPieces, slotHg, houseYard;

    /// <summary>칸마다 그림 슬롯: 숲 = 8방향 이웃이 모두 숲이면 깊은 숲 · 아니면 가장자리, 참호 = 4방향 참호 이웃 마스크로 조각, 풀 고지 = 고지 타일.</summary>
    byte[] BuildSlots(Theme? th)
    {
        slotSplit = th?.HasTerrain("forest_edge") == true && th.HasTerrain("forest_deep");
        slotPieces = th?.HasTerrain("trench_h") == true;
        slotHg = th?.HasTerrain("high_ground") == true;
        houseYard = BuildingRenderer.HasHouseArt(th) && th!.HasTerrain("dirt");
        useRim = th?.HasTerrain("high_ground_rim_n") == true && th.HasTerrain("high_ground_rim_e") && th.HasTerrain("high_ground_rim_s") && th.HasTerrain("high_ground_rim_w");
        var sl = new byte[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) sl[y * W + x] = SlotAt(x, y);
        return sl;
    }

    /// <summary>한 칸의 슬롯 (BuildSlots · 지형 편집 때 이웃 재계산 공용 — 참호 그림을 고르는 유일한 길).</summary>
    byte SlotAt(int x, int y)
    {
        byte F = (byte)TerrainType.Forest, Tr = (byte)TerrainType.Trench;
        int t = ter[y * W + x], r = t;
        if (t == F && slotSplit)
        {
            bool deep = true;
            for (int k = 0; k < 9 && deep; k++)
            {
                int nx = x + k % 3 - 1, ny = y + k / 3 - 1;
                if ((uint)nx < (uint)W && (uint)ny < (uint)H && ter[ny * W + nx] != F) deep = false;
            }
            r = deep ? SlotDeep : SlotEdge;
        }
        else if (t == Tr && slotPieces)
        {
            int m = (TrenchAt(x, y - 1) ? 1 : 0) | (TrenchAt(x + 1, y) ? 2 : 0) | (TrenchAt(x, y + 1) ? 4 : 0) | (TrenchAt(x - 1, y) ? 8 : 0);   // n e s w
            // 조각 번호 = TrenchPieces 순서. T · 십자 · 외톨이는 직선으로 대체 (가로 우선).
            int piece = m switch
            {
                5 or 7 or 13 => 1,                          // v (세로 + 세로 T)
                3 => 2, 9 => 3, 6 => 4, 12 => 5,            // ne nw se sw
                1 => 6, 2 => 7, 4 => 8, 8 => 9,             // end_n end_e end_s end_w
                _ => 0,                                     // h (0 · 가로 · 가로 T · 십자)
            };
            r = SlotTrench + piece;
        }
        else if (hi[y * W + x] && t == (byte)TerrainType.Grass && slotHg) r = SlotHigh;
        return (byte)r;
    }

    bool TrenchAt(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H && ter[y * W + x] == (byte)TerrainType.Trench;

    void BuildOverview()
    {
        // 1타일 = 1px, 각 지형 변형 0 의 평균색. 청크 굽는 동안 잠깐 보이는 자리표시.
        var avg = avgColors = new Color[NS];
        for (int t = 0; t < NS; t++)
        {
            var a = lods[0].Src![t][0];
            long r = 0, g = 0, b = 0;
            foreach (var c in a) { r += c.R; g += c.G; b += c.B; }
            avg[t] = new Color((byte)(r / a.Length), (byte)(g / a.Length), (byte)(b / a.Length), (byte)255);
        }
        Image img = Raylib.GenImageColor(W, H, Color.Black);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) Raylib.ImageDrawPixel(ref img, x, y, hi[y * W + x] && slot![y * W + x] != SlotHigh ? HighTint(avg[slot![y * W + x]]) : avg[slot![y * W + x]]);
        overview = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(overview, TextureFilter.Point);
        bytesNow += (long)W * H * 4;
    }

    /// <summary>테마 없음 (--theme none): 예전 절차 타일을 변형 4개로 그려 원본으로 쓴다.</summary>
    static Image[] ProceduralTiles(TerrainType t, int res)
    {
        var arr = new Image[4];
        for (int k = 0; k < 4; k++)
        {
            Image img = Raylib.GenImageColor(TS, TS, Color.Black);
            DrawProcedural(ref img, t, Hash(k, (int)t, 977));
            if (res != TS) Raylib.ImageResizeNN(ref img, res, res);
            arr[k] = img;
        }
        return arr;
    }

    static void DrawProcedural(ref Image img, TerrainType t, uint h)
    {
        Color baseColor = Terrains.Get(t).Color;
        Color c = Palette.Shade(baseColor, (int)(h % 13) - 6);
        Raylib.ImageDrawRectangle(ref img, 0, 0, TS, TS, c);
        switch (t)
        {
            case TerrainType.Grass:
                for (int k = 0; k < 5; k++)
                {
                    int hx = 2 + (int)((h >> (k * 5)) % 27), hy = 6 + (int)((h >> (k * 5 + 2)) % 22);
                    Color tuft = Palette.Shade(c, k % 2 == 0 ? -20 : 16);
                    Raylib.ImageDrawLine(ref img, hx, hy, hx - 1, hy - 4, tuft);
                    Raylib.ImageDrawLine(ref img, hx + 1, hy, hx + 2, hy - 3, tuft);
                }
                break;
            case TerrainType.Dirt:
                for (int k = 0; k < 3; k++)
                {
                    int hx = 4 + (int)((h >> (k * 7)) % 24), hy = 4 + (int)((h >> (k * 7 + 3)) % 24);
                    Raylib.ImageDrawCircle(ref img, hx, hy, 1 + (int)((h >> k) % 2), Palette.Shade(c, -24));
                }
                break;
            case TerrainType.Sand:
                for (int k = 0; k < 7; k++)
                    Raylib.ImageDrawPixel(ref img, (int)((h >> (k * 4)) % 32), (int)((h >> (k * 4 + 2)) % 32), Palette.Shade(c, 22));
                break;
            case TerrainType.Water:
                for (int k = 0; k < 2; k++)
                {
                    int hx = 4 + (int)((h >> (k * 8)) % 18), hy = 6 + (int)((h >> (k * 8 + 4)) % 20);
                    Raylib.ImageDrawLine(ref img, hx, hy, hx + 8, hy, Palette.Shade(c, 26));
                }
                break;
            case TerrainType.Forest:
                for (int k = 0; k < 4; k++)
                {
                    int hx = 6 + (int)((h >> (k * 6)) % 20), hy = 6 + (int)((h >> (k * 6 + 3)) % 20);
                    Raylib.ImageDrawCircle(ref img, hx + 1, hy + 2, 6, Palette.Shade(c, -30));
                    Raylib.ImageDrawCircle(ref img, hx, hy, 6, Palette.Shade(c, 10 + k * 4));
                }
                break;
            case TerrainType.Rubble:
                for (int k = 0; k < 6; k++)
                {
                    int hx = 3 + (int)((h >> (k * 5)) % 25), hy = 3 + (int)((h >> (k * 5 + 2)) % 25);
                    Raylib.ImageDrawRectangle(ref img, hx, hy, 3 + k % 3, 2 + k % 2, Palette.Shade(c, k % 2 == 0 ? -28 : 22));
                }
                break;
            case TerrainType.House:
                Raylib.ImageDrawRectangle(ref img, 0, 0, TS, TS, new Color(70, 100, 56, 255));
                Raylib.ImageDrawRectangle(ref img, 5, 6, 23, 22, Palette.Shade(c, -45));
                Raylib.ImageDrawRectangle(ref img, 4, 4, 23, 21, c);
                Raylib.ImageDrawRectangle(ref img, 4, 14, 23, 2, Palette.Shade(c, -25));
                break;
            case TerrainType.Rail:
                // 선로 자갈 바닥 (테마 그림 없음 → 절차 타일). 침목 · 레일은 RailRenderer 가 이웃 방향으로 덧그린다.
                for (int k = 0; k < 14; k++)
                    Raylib.ImageDrawPixel(ref img, (int)((h >> (k * 2)) % 32), (int)((h >> (k * 2 + 7)) % 32), Palette.Shade(c, k % 2 == 0 ? -26 : 20));
                break;
            case TerrainType.Wire:
                for (int k = 0; k < 3; k++)
                {
                    int yy = 6 + k * 10;
                    Raylib.ImageDrawLine(ref img, 0, yy, TS, yy + 2, new Color(60, 56, 52, 255));
                    for (int xx = 2; xx < TS; xx += 6) Raylib.ImageDrawLine(ref img, xx, yy - 2, xx + 3, yy + 3, new Color(40, 38, 36, 255));
                }
                break;
            case TerrainType.Trench:
                Raylib.ImageDrawRectangle(ref img, 0, 11, TS, 10, Palette.Shade(c, -45));
                Raylib.ImageDrawRectangle(ref img, 0, 9, TS, 2, Palette.Shade(c, 25));
                Raylib.ImageDrawRectangle(ref img, 0, 21, TS, 2, Palette.Shade(c, 25));
                break;
            case TerrainType.Crater:
            {
                // 포탄 구덩이 (임시 절차 타일): 그을린 자국 + 흙 둔덕 테 + 어두운 구덩이, 위치 · 크기는 해시로 변형
                int cx = 16 + (int)(h % 5) - 2, cy = 16 + (int)((h >> 4) % 5) - 2, r = 9 + (int)((h >> 8) % 3);
                Raylib.ImageDrawCircle(ref img, cx, cy, r + 4, Palette.Shade(c, -14));
                Raylib.ImageDrawCircle(ref img, cx, cy, r + 1, Palette.Shade(c, 18));
                Raylib.ImageDrawCircle(ref img, cx + 1, cy + 1, r - 1, Palette.Shade(c, -38));
                Raylib.ImageDrawCircle(ref img, cx + 1, cy + 2, r / 2, Palette.Shade(c, -55));
                for (int k = 0; k < 5; k++)
                {
                    float a = (h >> (k * 3)) % 628 / 100f;
                    int px = cx + (int)(MathF.Cos(a) * (r + 3)), py = cy + (int)(MathF.Sin(a) * (r + 3));
                    Raylib.ImageDrawRectangle(ref img, px, py, 2, 2, Palette.Shade(c, 30));
                }
                break;
            }
            case TerrainType.Rock:
            {
                Raylib.ImageDrawRectangle(ref img, 0, 0, TS, TS, Palette.Shade(c, -12));
                int bx = 16 + (int)(h % 7) - 3, by = 16 + (int)((h >> 3) % 7) - 3, r = 11 + (int)((h >> 6) % 3);
                Raylib.ImageDrawCircle(ref img, bx + 2, by + 3, r, Palette.Shade(c, -40));
                Raylib.ImageDrawCircle(ref img, bx, by, r, Palette.Shade(c, 14));
                Raylib.ImageDrawCircle(ref img, bx - 3, by - 3, r / 2, Palette.Shade(c, 34));
                break;
            }
        }
    }

    // ───────── 굽기 (워커 스레드, 순수) ─────────

    static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }

    int T(int x, int y) => ter[Math.Clamp(y, 0, H - 1) * W + Math.Clamp(x, 0, W - 1)];

    Color[] Variant(Lod L, int t, int x, int y)
    {
        var v = L.Src![t];
        return v[(int)(Hash(x, y, seed) % (uint)Math.Min(8, v.Length))];
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    static float ValueNoise(float x, float y, int salt)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = Smooth(x - ix), fy = Smooth(y - iy);
        const float inv = 1f / 65535f;
        float a = (Hash(ix, iy, salt) & 0xFFFF) * inv, b = (Hash(ix + 1, iy, salt) & 0xFFFF) * inv;
        float c = (Hash(ix, iy + 1, salt) & 0xFFFF) * inv, d = (Hash(ix + 1, iy + 1, salt) & 0xFFFF) * inv;
        return a + (b - a) * fx + (c - a + (a - b + d - c) * fx) * fy;
    }

    /// <summary>타일 키 (지형·변형) 별 경계 노이즈 [0,1] (타일 좌표, 2옥타브).</summary>
    float EdgeNoise(int key, float x, float y)
    {
        int s = seed * 31 + (key >> 3) * 7919 + (key & 7) * 104729;
        return (ValueNoise(x * 3.7f, y * 3.7f, s) * 2f + ValueNoise(x * 8.3f + 17.1f, y * 8.3f - 5.3f, s + 1)) * (1f / 3f);
    }

    static float Sharp(float t) => Math.Clamp((t - 0.5f) * K + 0.5f, 0f, 1f);

    static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)255);
    }

    static Color Mul(Color a, float f) =>
        new((byte)Math.Min(255f, a.R * f), (byte)Math.Min(255f, a.G * f), (byte)Math.Min(255f, a.B * f), (byte)255);

    /// <summary>타일 키 = 지형 × 8 + 변형. 변형끼리도 경계를 섞어 같은 지형의 격자 무늬를 깬다.</summary>
    int Key(Lod L, int x, int y)
    {
        x = Math.Clamp(x, 0, W - 1); y = Math.Clamp(y, 0, H - 1);
        int t = slot![y * W + x];
        return t * 8 + (int)(Hash(x, y, seed) % (uint)Math.Min(8, L.Src![t].Length));
    }

    void BuildPixels(Lod L, int cx, int cy, Color[] outPx, out int w, out int h)
    {
        int R = L.R, tx0 = cx * L.CT, ty0 = cy * L.CT;
        int tw = Math.Min(L.CT, W - tx0), th = Math.Min(L.CT, H - ty0);
        w = tw * R; h = th * R;
        Span<int> keys = stackalloc int[9];
        for (int ly = 0; ly < th; ly++)
        for (int lx = 0; lx < tw; lx++)
        {
            int x = tx0 + lx, y = ty0 + ly;
            bool uniform = true;
            for (int k = 0; k < 9; k++)
            {
                keys[k] = Key(L, x + k % 3 - 1, y + k / 3 - 1);
                if (keys[k] != keys[0]) uniform = false;
            }
            int dst = ly * R * w + lx * R;
            if (uniform)
            {
                var s = L.Src![keys[4] >> 3][keys[4] & 7];
                for (int row = 0; row < R; row++) Array.Copy(s, row * R, outPx, dst + row * w, R);
            }
            else BlendTile(L, x, y, keys, outPx, dst, w);
            if (hi[y * W + x]) ShadeHigh(L, x, y, R, outPx, dst, w);
        }
    }

    static Color HighTint(Color c) => new((byte)Math.Min(255, c.R * 1.10f + 10), (byte)Math.Min(255, c.G * 1.08f + 8), (byte)Math.Min(255, c.B * 0.98f), (byte)255);

    bool LowAt(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H && !hi[y * W + x];

    /// <summary>고지 칸: 밝게 + 평지와 맞닿은 변에 경계선 (남 · 동 = 어두운 절벽, 북 · 서 = 밝은 턱).</summary>
    void ShadeHigh(Lod L, int x, int y, int R, Color[] px, int dst, int stride)
    {
        int band = Math.Max(1, R / 16);
        bool n = LowAt(x, y - 1), s = LowAt(x, y + 1), wv = LowAt(x - 1, y), e = LowAt(x + 1, y);
        if (slot![y * W + x] != SlotHigh)   // 고지 전용 타일은 이미 밝은 색
            for (int j = 0; j < R; j++)
            for (int i = 0; i < R; i++) { ref Color c = ref px[dst + j * stride + i]; c = HighTint(c); }
        if (L.Rim != null)
        {
            // 평지와 맞닿은 변마다 가장자리 오버레이 (남 · 동 = 어두운 절벽면, 북 · 서 = 밝은 턱)
            for (int k = 0; k < 4; k++)
            {
                if (!(k == 0 ? n : k == 1 ? e : k == 2 ? s : wv)) continue;
                var rim = L.Rim[k];
                for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    Color o = rim[j * R + i];
                    if (o.A == 0) continue;
                    ref Color c = ref px[dst + j * stride + i];
                    c = Lerp(c, o, o.A / 255f);
                }
            }
            return;
        }
        for (int j = 0; j < R; j++)
        for (int i = 0; i < R; i++)
        {
            ref Color c = ref px[dst + j * stride + i];
            if ((s && j >= R - band) || (e && i >= R - band)) c = Mul(c, 0.55f);
            else if ((n && j < band) || (wv && i < band)) c = Mul(c, 1.25f);
        }
    }

    void BlendTile(Lod L, int x, int y, ReadOnlySpan<int> keys9, Color[] outPx, int dst, int stride)
    {
        int R = L.R, half = R / 2;
        float invR = 1f / R, aa = 2f * K * invR;   // 가중치 차 1px ≈ 2K/R
        const int Water = (int)TerrainType.Water, Rock = (int)TerrainType.Rock, Sand = (int)TerrainType.Sand;
        var src = L.Src!;
        var sand = Variant(L, Sand, x, y);
        Span<int> qk = stackalloc int[4];      // 사분면의 서로 다른 키
        Span<int> qi = stackalloc int[4];      // 2×2 각 타일 → qk 인덱스
        Span<float> wk = stackalloc float[4];
        Span<float> sk = stackalloc float[4];
        Span<float> ts = stackalloc float[NS]; // 슬롯별 최고 점수
        // 경계 노이즈는 타일당 GN×GN 격자에서만 계산하고 쌍선형 보간 (128px 에서도 싸다)
        int GN = R >= 64 ? GNMax : 9;
        Span<int> gKey = stackalloc int[9];
        Span<float> grid = stackalloc float[9 * GNMax * GNMax];
        int ng = 0;
        Span<int> qg = stackalloc int[4];      // qk 인덱스 → 노이즈 격자 번호
        Span<int> colC = stackalloc int[R];
        Span<float> colF = stackalloc float[R];
        for (int i = 0; i < R; i++)
        {
            float g = (i + 0.5f) * invR * (GN - 1);
            colC[i] = Math.Min(GN - 2, (int)g); colF[i] = g - colC[i];
        }
        // 사분면마다 섞을 2×2 타일 묶음이 고정 → 키가 하나뿐이면 행 복사
        for (int q = 0; q < 4; q++)
        {
            int ox = q & 1, oy = q >> 1;          // 3×3 안에서 2×2 묶음의 왼쪽 위 (0 또는 1)
            int i0 = ox * half, j0 = oy * half, nk = 0;
            for (int c = 0; c < 4; c++)
            {
                int key = keys9[(oy + (c >> 1)) * 3 + ox + (c & 1)], found = -1;
                for (int e = 0; e < nk; e++) if (qk[e] == key) { found = e; break; }
                if (found < 0) { found = nk; qk[nk++] = key; }
                qi[c] = found;
            }
            if (nk == 1)
            {
                var s = src[qk[0] >> 3][qk[0] & 7];
                for (int j = j0; j < j0 + half; j++) Array.Copy(s, j * R + i0, outPx, dst + j * stride + i0, half);
                continue;
            }
            for (int e = 0; e < nk; e++)
            {
                int gi = -1;
                for (int z = 0; z < ng; z++) if (gKey[z] == qk[e]) { gi = z; break; }
                if (gi < 0)
                {
                    gi = ng++; gKey[gi] = qk[e];
                    var gsp = grid.Slice(gi * GN * GN, GN * GN);
                    for (int r = 0; r < GN; r++)
                    for (int c = 0; c < GN; c++)
                        gsp[r * GN + c] = EdgeNoise(qk[e], x + c / (GN - 1f), y + r / (GN - 1f));
                }
                qg[e] = gi;
            }
            for (int j = j0; j < j0 + half; j++)
            {
                float v = (j + 0.5f) * invR;
                int gr = colC[j]; float gf = colF[j];
                float wy1 = Sharp(oy == 0 ? v + 0.5f : v - 0.5f), wy0 = 1f - wy1;
                for (int i = i0; i < i0 + half; i++)
                {
                    float u = (i + 0.5f) * invR;
                    float wx1 = Sharp(ox == 0 ? u + 0.5f : u - 0.5f), wx0 = 1f - wx1;
                    int pi = dst + j * stride + i, li = j * R + i;
                    wk.Clear();
                    wk[qi[0]] += wx0 * wy0; wk[qi[1]] += wx1 * wy0; wk[qi[2]] += wx0 * wy1; wk[qi[3]] += wx1 * wy1;
                    int gc = colC[i]; float gcf = colF[i];
                    int k1 = -1, k2 = -1;
                    float s1 = float.MinValue, s2 = float.MinValue;
                    ts.Fill(float.MinValue);
                    int pos = 0;
                    for (int e = 0; e < nk; e++) if (wk[e] > 0f) { pos++; k1 = e; }
                    if (pos == 1) { outPx[pi] = src[qk[k1] >> 3][qk[k1] & 7][li]; continue; }
                    k1 = -1;
                    for (int e = 0; e < nk; e++)
                    {
                        if (wk[e] <= 0f) continue;
                        int o = qg[e] * GN * GN + gr * GN + gc;
                        float n0 = grid[o] + (grid[o + 1] - grid[o]) * gcf, n1 = grid[o + GN] + (grid[o + GN + 1] - grid[o + GN]) * gcf;
                        float s = wk[e] + Amp * (n0 + (n1 - n0) * gf - 0.5f);
                        int t = qk[e] >> 3;
                        if (s > ts[t]) ts[t] = s;
                        if (s > s1) { s2 = s1; k2 = k1; s1 = s; k1 = e; }
                        else if (s > s2) { s2 = s; k2 = e; }
                    }
                    int key1 = qk[k1], t1 = key1 >> 3;
                    Color c = src[t1][key1 & 7][li];
                    if (k2 < 0) { outPx[pi] = c; continue; }
                    int key2 = qk[k2], t2 = key2 >> 3;
                    float m = s1 - s2;
                    if (t1 == t2) { if (m < 0.6f) c = Lerp(src[t2][key2 & 7][li], c, 0.5f + 0.5f * m / 0.6f); }   // 같은 지형 변형끼리는 넓게 섞기
                    else if (t1 != Water && t2 != Water && m < aa)
                        c = Lerp(src[t2][key2 & 7][li], c, 0.5f + 0.5f * m / aa);   // 다른 지형은 1px 만 부드럽게
                    float landBest = MathF.Max(MathF.Max(MathF.Max(ts[0], ts[1]), MathF.Max(ts[2], ts[4])), ts[SlotHigh]);
                    if (t1 != Water && ts[Water] > float.MinValue)
                    {
                        // 물가: 젖은 모래 띠 (물 쪽으로 갈수록 어둡게)
                        float mw = s1 - ts[Water], band = 0.45f;
                        if (mw < band)
                        {
                            float k = 1f - mw / band;
                            Color wet = Mul(sand[li], 1f - 0.22f * k);
                            c = Lerp(c, wet, t1 == Sand ? k * 0.6f : MathF.Min(1f, k * 1.3f));
                        }
                    }
                    else if (t1 == Water && landBest > float.MinValue)
                    {
                        // 물 쪽: 얕은 물 (모래 비침) + 물가 거품
                        float ml = s1 - landBest;
                        if (ml < 0.8f) c = Lerp(c, Lerp(c, Mul(sand[li], 0.9f), 0.6f), (1f - ml / 0.8f) * 0.7f);
                        if (ml < aa * 1.5f) c = Lerp(c, new Color(214, 226, 220, 255), 0.55f);
                    }
                    if (t1 == Rock && t2 != Rock && m < 0.3f) c = Mul(c, 1f - 0.35f * (1f - m / 0.3f));   // 바위 가장자리 그늘
                    else if (t1 != Rock && ts[Rock] > float.MinValue)
                    {
                        float mr = s1 - ts[Rock];
                        if (mr < 0.25f) c = Mul(c, 1f - 0.22f * (1f - mr / 0.25f));                     // 바위 발치 그림자
                    }
                    outPx[pi] = c;
                }
            }
        }
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("KD_MAPSTATS") != null) Console.WriteLine("[mapstats] " + StatsLine);
        disposed = true;
        foreach (var L in lods)
            for (int i = 0; i < L.Tex.Length; i++)
                if (L.Tex[i] != null) Unload(L, i);
        if (overview.Id != 0) Raylib.UnloadTexture(overview);
        overview = default;
        while (done.TryDequeue(out var r)) if (r.px != null) ArrayPool<Color>.Shared.Return(r.px);
    }
}
