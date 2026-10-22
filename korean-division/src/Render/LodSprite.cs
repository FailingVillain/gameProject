using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace KoreanDivision.Render;

/// <summary>이름 붙은 애니메이션: 프레임 번호(<see cref="LodSprite.FrameNames"/> 기준) + 프레임별 지속 시간.</summary>
public sealed class LodAnim
{
    public string Name = "";
    public int[] Frames = Array.Empty<int>();
    /// <summary>프레임별 누적 끝 시각 (ms).</summary>
    public int[] EndMs = Array.Empty<int>();
    public int TotalMs => EndMs.Length > 0 ? EndMs[^1] : 0;

    /// <summary>시작 후 ms 시점의 프레임. loop = 반복, 아니면 마지막 프레임에서 멈춤.</summary>
    public int FrameAt(long ms, bool loop)
    {
        if (Frames.Length == 0) return 0;
        int total = TotalMs;
        if (total <= 0) return Frames[0];
        long t = loop ? ((ms % total) + total) % total : Math.Clamp(ms, 0, total - 1);
        for (int i = 0; i < EndMs.Length; i++) if (t < EndMs[i]) return Frames[i];
        return Frames[^1];
    }
}

/// <summary>
/// 한 해상도(LOD)의 시트 묶음. game-tools 규격: 행 = 방향, 열 = 프레임, 4096px 를 넘으면 시트가 _1/_2 로 나뉘고
/// layout 의 cells{프레임: {sheet, col}} 이 위치를 가리킨다. 칸별 기준점(anchors json)은 이 해상도의 픽셀 단위.
/// </summary>
public sealed class LodLevel
{
    public int FrameW, FrameH;
    public Vector2 Anchor;
    /// <summary>이 시트 1px → 월드 px (LOD 0 의 scale × LOD0 폭 / 이 폭).</summary>
    public float Scale;
    /// <summary>칸별 기준점 [행][프레임] (없으면 Anchor).</summary>
    public Vector2[][] Anchors = Array.Empty<Vector2[]>();
    /// <summary>차체: 행별 포탑링 화면 오프셋 (접지점 기준, 이 LOD 의 px).</summary>
    public Vector2[]? Mount;
    /// <summary>차체: 포탑 장착점이 여럿이면 장착점마다 [행] 오프셋 (Mount = 첫째와 같음). 렌더 전용 — 시뮬은 포 하나.</summary>
    public Vector2[][]? Mounts;
    public int Group;
    internal int[] CellSheet = Array.Empty<int>();   // [프레임] → 시트 번호 (-1 = 이 LOD 에 없음)
    internal int[] CellCol = Array.Empty<int>();
    /// <summary>[프레임] → 그 칸의 크기 (시트 항목에 "frame" 이 있으면 그 크기, 아니면 FrameW/H) — 엎드림 등 넓은 칸용, 표시 전용.</summary>
    internal int[] CellFW = Array.Empty<int>(), CellFH = Array.Empty<int>();
    internal string[] SheetFiles = Array.Empty<string>();
    internal string?[] TeamFiles = Array.Empty<string?>();
    // ── 아틀라스 배치 (LodAtlas 가 채움). 칸 번호 ci = 행 × 프레임 수 + 프레임. 투명 여백을 잘라 담는다 → CW = 0 이면 빈 칸.
    internal LodSprite Owner = null!;
    internal Texture2D[] CellTex = Array.Empty<Texture2D>();
    internal int[] CX = Array.Empty<int>(), CY = Array.Empty<int>(), CW = Array.Empty<int>(), CH = Array.Empty<int>();
    /// <summary>잘라낸 사각형의 칸 안 위치 (이 LOD px) — 기준점은 그대로 칸 기준.</summary>
    internal int[] COX = Array.Empty<int>(), COY = Array.Empty<int>();
    /// <summary>0 = 없음, 1 = 만드는 중 (작업자 · 업로드 대기), 2 = 준비됨.</summary>
    internal int State;
    internal bool Ready => State == 2;
    /// <summary>LOD 0 전용 페이지 (종류마다 따로 — 안 쓰면 내린다). 공유 그룹이면 null.</summary>
    internal List<Texture2D>? OwnPages;
    /// <summary>[측정] GPU 바이트 (전용 = 페이지, 공유 = 잘린 칸 면적) · 자르기 전 시트 바이트.</summary>
    internal long Bytes, RawBytes;
    internal double LastUsed;

    public Vector2 AnchorAt(int row, int frame) =>
        row < Anchors.Length && frame < Anchors[row].Length ? Anchors[row][frame] : Anchor;
}

/// <summary>
/// 새 유닛 시트 형식 (128×160 · @64 · @32, 차량 차체 192² · @96 · @48 / 포탑 160² · @80 · @40) 한 종류.
/// 줌에 맞는 LOD 를 고르고 (점 필터, 밉맵 없음), 팀색은 시트 알파 254 표식 + <see cref="TeamShader"/> 로 칠한다.
/// </summary>
public sealed class LodSprite
{
    public string Key = "";
    public string[] FrameNames = Array.Empty<string>();
    readonly Dictionary<string, int> frameIndex = new();
    public readonly Dictionary<string, LodAnim> Anims = new();
    /// <summary>큰 해상도부터.</summary>
    public LodLevel[] Levels = Array.Empty<LodLevel>();
    public string[] Dirs = Array.Empty<string>();
    public int DirCount;
    public bool Mirror;
    /// <summary>LOD 0 의 월드 배율.</summary>
    public float Scale = 0.25f;
    /// <summary>작은 LOD 에서 다음 큰 LOD 로 넘어가는 확대 한도 (화면 칸 폭 &gt; 작은 LOD 폭 × 이 값이면 큰 LOD).</summary>
    public float Upscale = 1.15f;
    internal LodAtlas? Atlas;
    /// <summary>[프레임] 아틀라스에 담을지 (애니메이션 매핑이 쓰지 않는 칸은 뺀다 — <see cref="UsedAnimNames"/>).</summary>
    internal bool[] Used = Array.Empty<bool>();

    /// <summary>
    /// 유닛 시트에서 실제로 쓰는 애니메이션 이름 (<see cref="UnitArt"/> · UnitRenderer 가 부르는 것 전부) 과 이름으로 직접 찾는 칸.
    /// 여기에 없는 애니메이션에만 나오는 칸 (예: spare, go_prone) 은 담지 않는다. UnitArt 에 새 애니메이션을 쓰면 여기에도 추가.
    /// </summary>
    public static readonly HashSet<string> UsedAnimNames = new()
    {
        "idle", "idle_alt", "idle_lean", "idle_rest", "idle_look", "idle_adjust", "idle_stretch", "idle_3",
        "walk", "move", "travel", "walk_shoulder", "walk_quick", "walk_fire", "charge", "melee", "melee_b", "sword_wave", "fix_bayonet", "wave_flag",
        "fire", "pistol_fire", "fire_a", "fire_b", "fire_c", "fire_prone", "fire_kneel", "shoot", "deploy", "unpack", "pack",
        "binoculars", "binoculars_kneel", "crawl", "trench_fire", "grenade_throw", "drag_wounded", "salute", "present_arms", "surrender", "start", "hit_b", "supply", "gallop", "hitch", "unhitch", "death", "death_b", "death_c", "destroyed",
    };
    public static readonly string[] UsedFrameNames = { "idle", "hit", "aim_prone", "fire_prone", "dead" };

    public int Frame(string name) => frameIndex.TryGetValue(name, out var i) ? i : -1;
    public LodAnim? Anim(string name) => Anims.TryGetValue(name, out var a) && a.Frames.Length > 0 ? a : null;

    /// <summary>screenScale = LOD 0 시트 1px 가 화면에서 차지하는 px (scale × 카메라 줌). 그에 맞는 LOD 번호.</summary>
    public int LevelFor(float screenScale)
    {
        float sw = Levels[0].FrameW * screenScale;
        for (int i = Levels.Length - 1; i > 0; i--)
            if (sw <= Levels[i].FrameW * Upscale) return i;
        return 0;
    }

    /// <summary>facing(라디안, 0 = 오른쪽, π/2 = 아래) → 행 + 좌우 반전 (Theme.RowOf 와 같은 규칙: s→e→n 반원 균등 분할, 서쪽은 반전).</summary>
    public int RowOf(float facing, out bool flip)
    {
        if (!float.IsFinite(facing)) facing = 0;   // 방향 값이 깨져도 (NaN) 칸을 그린다 — 행 번호가 범위 밖이면 유닛이 통째로 사라진다
        float yaw = (90f - facing * (180f / MathF.PI)) % 360f;
        if (yaw < 0) yaw += 360f;
        flip = false;
        if (yaw > 180f) { yaw = 360f - yaw; flip = true; }
        if (!Mirror) flip = false;
        if (DirCount <= 1) return 0;
        int row = (int)MathF.Round(yaw / (180f / (DirCount - 1)));
        return Math.Clamp(row, 0, DirCount - 1);
    }

    /// <summary>
    /// 레이아웃 json 여러 장 (LOD 마다 하나) 을 읽는다. 첫 장의 columns 가 프레임 번호 기준.
    /// </summary>
    public static LodSprite Load(string key, string dir, IList<string> layouts, float scale, float upscale)
    {
        var s = new LodSprite { Key = key, Scale = scale, Upscale = upscale };
        var levels = new List<LodLevel>();
        foreach (var rel in layouts)
        {
            string path = Path.Combine(dir, rel);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            string ldir = Path.GetDirectoryName(rel) ?? "";
            var lv = new LodLevel();
            lv.FrameW = root.GetProperty("frame")[0].GetInt32();
            lv.FrameH = root.GetProperty("frame")[1].GetInt32();
            lv.Anchor = root.TryGetProperty("anchor", out var an) ? new Vector2(an[0].GetSingle(), an[1].GetSingle()) : new Vector2(lv.FrameW / 2f, lv.FrameH - 8);
            var dirs = root.GetProperty("dirs").EnumerateArray().Select(d => d.GetString() ?? "").ToArray();
            if (s.FrameNames.Length == 0)
            {
                // 첫 LOD: 프레임 이름 · 방향 · 애니메이션 기준
                s.FrameNames = root.GetProperty("columns").EnumerateArray().Select(c => c.GetString() ?? "").ToArray();
                for (int i = 0; i < s.FrameNames.Length; i++) s.frameIndex[s.FrameNames[i]] = i;
                s.Dirs = dirs; s.DirCount = dirs.Length;
                s.Mirror = root.TryGetProperty("mirror", out var mi) && mi.ValueKind == JsonValueKind.True;
                if (root.TryGetProperty("anims", out var anims))
                    foreach (var p in anims.EnumerateObject())
                    {
                        var frames = p.Value.GetProperty("frames").EnumerateArray().Select(f => s.Frame(f.GetString() ?? "")).ToArray();
                        if (frames.Any(f => f < 0)) continue;
                        var msEl = p.Value.TryGetProperty("ms", out var m) ? m : default;
                        var end = new int[frames.Length];
                        int acc = 0;
                        for (int i = 0; i < frames.Length; i++)
                        {
                            int d = msEl.ValueKind == JsonValueKind.Array ? (i < msEl.GetArrayLength() ? msEl[i].GetInt32() : 150)
                                  : msEl.ValueKind == JsonValueKind.Number ? msEl.GetInt32() : 150;
                            acc += Math.Max(1, d);
                            end[i] = acc;
                        }
                        s.Anims[p.Name] = new LodAnim { Name = p.Name, Frames = frames, EndMs = end };
                    }
            }
            // 시트 · 칸
            var sheets = root.GetProperty("sheets").EnumerateArray().ToArray();
            lv.SheetFiles = sheets.Select(x => Path.Combine(ldir, x.GetProperty("file").GetString()!)).ToArray();
            lv.TeamFiles = sheets.Select(x => x.TryGetProperty("team", out var t) && t.GetString() is string ts ? Path.Combine(ldir, ts) : null).ToArray();
            var sheetIdx = sheets.Select((x, i) => (x.GetProperty("file").GetString()!, i)).ToDictionary(p => p.Item1, p => p.i);
            lv.CellSheet = Enumerable.Repeat(-1, s.FrameNames.Length).ToArray();
            lv.CellCol = new int[s.FrameNames.Length];
            lv.CellFW = Enumerable.Repeat(lv.FrameW, s.FrameNames.Length).ToArray();
            lv.CellFH = Enumerable.Repeat(lv.FrameH, s.FrameNames.Length).ToArray();
            var cellAnchor = new Vector2?[s.FrameNames.Length];
            var cells = root.GetProperty("cells");
            for (int f = 0; f < s.FrameNames.Length; f++)
                if (cells.TryGetProperty(s.FrameNames[f], out var c) && sheetIdx.TryGetValue(c.GetProperty("sheet").GetString() ?? "", out int si))
                {
                    lv.CellSheet[f] = si;
                    lv.CellCol[f] = c.GetProperty("col").GetInt32();
                    var sh = sheets[si];
                    if (sh.TryGetProperty("frame", out var sfr) && sfr.GetArrayLength() == 2) { lv.CellFW[f] = sfr[0].GetInt32(); lv.CellFH[f] = sfr[1].GetInt32(); }
                    if (sh.TryGetProperty("anchor", out var san) && san.GetArrayLength() == 2) cellAnchor[f] = new Vector2(san[0].GetSingle(), san[1].GetSingle());
                }
            // 칸별 기준점 {방향: [[x,y] × 열]} — 열 순서 = 이 레이아웃의 columns
            var cols = root.GetProperty("columns").EnumerateArray().Select(c => c.GetString() ?? "").ToArray();
            lv.Anchors = new Vector2[s.DirCount][];
            for (int r = 0; r < s.DirCount; r++) lv.Anchors[r] = Enumerable.Range(0, s.FrameNames.Length).Select(i => cellAnchor[i] ?? lv.Anchor).ToArray();
            if (root.TryGetProperty("anchors", out var anf) && anf.GetString() is string anrel && File.Exists(Path.Combine(dir, ldir, anrel)))
            {
                using var adoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, ldir, anrel)));
                for (int r = 0; r < s.DirCount; r++)
                {
                    if (!adoc.RootElement.TryGetProperty(s.Dirs[r], out var row)) continue;
                    int k = 0;
                    foreach (var p in row.EnumerateArray())
                    {
                        if (k < cols.Length && s.Frame(cols[k]) is int fi && fi >= 0 && p.GetArrayLength() == 2)
                            lv.Anchors[r][fi] = new Vector2(p[0].GetSingle(), p[1].GetSingle());
                        k++;
                    }
                }
            }
            // 차체: 포탑 장착점 {방향: [dx, dy]}
            if (root.TryGetProperty("turret", out var tur) && tur.TryGetProperty("mount", out var mount))
                lv.Mount = s.Dirs.Select(d => mount.TryGetProperty(d, out var mv) ? new Vector2(mv[0].GetSingle(), mv[1].GetSingle()) : Vector2.Zero).ToArray();
            if (root.TryGetProperty("turret", out var tur2) && tur2.TryGetProperty("mounts", out var mounts) && mounts.ValueKind == JsonValueKind.Array && mounts.GetArrayLength() > 1)
                lv.Mounts = mounts.EnumerateArray().Select(mo => s.Dirs.Select(d => mo.TryGetProperty(d, out var mv) ? new Vector2(mv[0].GetSingle(), mv[1].GetSingle()) : Vector2.Zero).ToArray()).ToArray();
            levels.Add(lv);
        }
        s.Levels = levels.OrderByDescending(l => l.FrameW).ToArray();
        for (int i = 0; i < s.Levels.Length; i++)
        {
            s.Levels[i].Scale = scale * s.Levels[0].FrameW / s.Levels[i].FrameW;
            s.Levels[i].Group = i;
            s.Levels[i].Owner = s;
        }
        // 담을 칸: 유닛 시트이고 애니메이션이 있으면 매핑이 쓰는 칸만, 아니면 (건물 등 번호로 고르는 시트) 전부
        s.Used = new bool[s.FrameNames.Length];
        if (key.StartsWith("unit.") && s.Anims.Count > 0)
        {
            s.Used[0] = true;
            foreach (var (name, a) in s.Anims) if (UsedAnimNames.Contains(name)) foreach (int f in a.Frames) s.Used[f] = true;
            foreach (var n in UsedFrameNames) if (s.Frame(n) is int fi && fi >= 0) s.Used[fi] = true;
        }
        else Array.Fill(s.Used, true);
        return s;
    }
}

/// <summary>
/// 팀색 셰이더: 시트 알파 254 픽셀 = 회색조 팀 레이어 → 정점 색(세력 색) × 회색, 나머지 = 시트 색 그대로. 정점 알파 = 페이드.
/// raylib 의 배치는 추가 샘플러(texture1)를 배치를 비울 때마다 풀어 버려서 R8 팀 텍스처를 따로 두면 깨지기 쉽다 →
/// 팀 레이어를 기본 시트 알파에 접어 넣었다 (팀 텍스처 메모리 0, 유닛당 사각형 1개).
/// 셰이더를 못 만들면 팀 픽셀은 회색으로 보인다 (기본 셰이더).
/// </summary>
internal static class TeamShader
{
    const string Vs = @"#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec4 vertexColor;
uniform mat4 mvp;
out vec2 fragTexCoord;
out vec4 fragColor;
void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}";
    const string Fs = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
uniform sampler2D texture0;
out vec4 finalColor;
void main()
{
    vec4 c = texture(texture0, fragTexCoord);
    if (c.a < 0.5) discard;
    vec3 rgb = c.a < 0.999 ? fragColor.rgb * c.r : c.rgb;
    finalColor = vec4(rgb, fragColor.a);
}";
    static Shader shader;
    static bool tried, ok, active;

    static bool Ready()
    {
        if (!tried)
        {
            tried = true;
            shader = Raylib.LoadShaderFromMemory(Vs, Fs);
            ok = shader.Id != 0 && Raylib.IsShaderValid(shader) && shader.Id != Rlgl.GetShaderIdDefault();
            if (!ok) Console.Error.WriteLine("[lod] 팀색 셰이더를 만들 수 없음 — 팀 픽셀이 회색으로 보입니다");
        }
        return ok;
    }

    /// <summary>LOD 시트 그리기 전에 (이미 켜져 있으면 아무것도 안 함).</summary>
    public static void Enter()
    {
        if (active || !Ready()) return;
        Raylib.BeginShaderMode(shader);
        active = true;
    }

    /// <summary>다른 그림(옛 시트 · 도형) 전에, 그리고 그리기 묶음 끝에.</summary>
    public static void Leave()
    {
        if (!active) return;
        Raylib.EndShaderMode();
        active = false;
    }
}

/// <summary>LOD 시트 한 칸 그리기.</summary>
internal static class LodDraw
{
    /// <summary>[측정] 이번 실행에서 LOD 별로 그린 칸 수.</summary>
    public static readonly long[] Drawn = new long[4];

    /// <summary>
    /// feet = 기준점이 놓일 위치 (월드/화면 좌표), worldScale = LOD 0 시트 1px → 좌표 배율 (보통 sprite.Scale),
    /// screenScale = LOD 0 시트 1px 가 화면에서 차지하는 px (LOD 선택용). 칸이 없는 프레임이면 아무것도 그리지 않는다.
    /// </summary>
    public static void Cell(LodSprite s, int row, bool flip, int frame, Vector2 feet, float worldScale, float screenScale, Color team, int alpha = 255, bool plain = false)
    {
        if ((uint)frame >= (uint)s.FrameNames.Length || (uint)row >= (uint)Math.Max(1, s.DirCount)) { Miss(s, row, frame, -1, "범위 밖"); return; }
        // 원하는 LOD 가 아직 없으면 (작업자가 만드는 중 · 내려감) 다음 작은 LOD 로 그린다
        var lv = s.Atlas?.Resolve(s, s.LevelFor(screenScale));
        if (lv == null) { Miss(s, row, frame, s.LevelFor(screenScale), "LOD 없음"); return; }
        int ci = row * s.FrameNames.Length + frame;
        int cw = lv.CW[ci];
        if (cw == 0)
        {
            // 이 LOD 에 그 칸이 없으면 (빈 칸 · 담지 않은 칸) 사라지지 말고 다른 준비된 LOD 의 같은 칸으로 (작은 쪽 먼저, 그다음 큰 쪽)
            Miss(s, row, frame, lv.Group, "빈 칸 → 다른 LOD");
            var alt = s.Atlas?.FindCell(s, lv.Group, ci);
            if (alt == null) return;
            lv = alt; cw = lv.CW[ci];
        }
        float k = worldScale / s.Scale * lv.Scale;   // 이 LOD 1px → 좌표
        var an = lv.AnchorAt(row, frame);
        int fwc = lv.CellFW[frame];
        float ax = flip ? fwc - an.X : an.X;
        // 잘라낸 사각형의 칸 안 위치: 반전이면 칸 오른쪽 끝에서 잰다
        int ox = flip ? fwc - lv.COX[ci] - cw : lv.COX[ci];
        var src = new Rectangle(lv.CX[ci], lv.CY[ci], flip ? -cw : cw, lv.CH[ci]);
        var dest = new Rectangle(feet.X - ax * k + ox * k, feet.Y - an.Y * k + lv.COY[ci] * k, cw * k, lv.CH[ci] * k);
        if (plain) TeamShader.Leave(); else TeamShader.Enter();   // plain = 팀색 없는 그림 (민가): 기본 셰이더라 team 값이 곱해지는 색조가 된다
        Raylib.DrawTexturePro(lv.CellTex[ci], src, dest, Vector2.Zero, 0, new Color(team.R, team.G, team.B, (byte)alpha));
        if (lv.Group < Drawn.Length) Drawn[lv.Group]++;
    }

    /// <summary>
    /// 칸 (행 0) 의 그림 — 투명 여백을 잘라낸 부분 — 을 사각형 r (월드 좌표) 안에 비율을 지켜 맞춰 그린다: 가로 가운데 · 아래 맞춤.
    /// 여러 칸짜리 가옥 발자국 그리기용 (그림을 바꾸지 않고 배율로 크기를 맞춤). zoom = 카메라 줌 (LOD 선택).
    /// </summary>
    public static void CellFit(LodSprite s, int frame, Rectangle r, float zoom, Color team, bool mirror = false, bool plain = false)
    {
        if ((uint)frame >= (uint)s.FrameNames.Length || s.Levels.Length == 0) return;
        var l0 = s.Levels[0];
        float est = MathF.Min(r.Width / (l0.FrameW * 0.9f), r.Height / (l0.FrameH * 0.7f)) * zoom;   // LOD 0 1px → 화면 px (대략)
        var lv = s.Atlas?.Resolve(s, s.LevelFor(est));
        if (lv == null) return;
        int ci = frame;
        if (lv.CW[ci] == 0) { lv = s.Atlas?.FindCell(s, lv.Group, ci); if (lv == null) return; }
        int cw = lv.CW[ci], ch = lv.CH[ci];
        float k = MathF.Min(r.Width / cw, r.Height / ch);
        float w = cw * k, h = ch * k;
        var dest = new Rectangle(r.X + (r.Width - w) / 2, r.Y + r.Height - h, w, h);
        if (plain) TeamShader.Leave(); else TeamShader.Enter();
        // mirror = 가로로 뒤집어 그리기 (음수 원본 폭, 아틀라스 항목 증가 0). plain = 기본 셰이더 (team 값이 곱해지는 색조) — 팀 셰이더는 불투명 픽셀에 정점 색을 안 곱한다
        Raylib.DrawTexturePro(lv.CellTex[ci], new Rectangle(lv.CX[ci], lv.CY[ci], mirror ? -cw : cw, ch), dest, Vector2.Zero, 0, team);
        if (lv.Group < Drawn.Length) Drawn[lv.Group]++;
    }

    static readonly bool MissLog = Environment.GetEnvironmentVariable("KD_LODMISS") == "1";
    static readonly HashSet<string> missSeen = new();
    /// <summary>[확인용] KD_LODMISS=1: 그리지 못한 칸을 (종류 · 프레임 · 행 · LOD · 이유) 별로 한 번씩 기록.</summary>
    static void Miss(LodSprite s, int row, int frame, int lod, string why)
    {
        if (!MissLog) return;
        string fn = (uint)frame < (uint)s.FrameNames.Length ? s.FrameNames[frame] : frame.ToString();
        if (missSeen.Add($"{s.Key}|{fn}|{row}|{lod}|{why}")) Console.Error.WriteLine($"[lodmiss] {s.Key} frame={fn} row={row} L{lod} {why}");
    }

    /// <summary>방향으로 행을 골라 한 칸.</summary>
    public static void Directional(LodSprite s, Vector2 feet, float worldScale, float screenScale, float facing, int frame, Color team, int alpha = 255)
    {
        int row = s.RowOf(facing, out bool flip);
        Cell(s, row, flip, frame, feet, worldScale, screenScale, team, alpha);
    }

    /// <summary>
    /// 차체 + 포탑: 포탑 pivot(칸 기준점) 을 차체 기준점 + mount[차체 방향] 에 맞춘다 (반전이면 dx 반전). 포탑은 자기 방향 행.
    /// </summary>
    public static void Tank(LodSprite hull, LodSprite? turret, Vector2 feet, float worldScale, float screenScale, float hullFacing, float turretFacing,
        int hullFrame, int turretFrame, Color team, int alpha = 255, int fireMount = -1, int idleFrame = 0)
    {
        int hrow = hull.RowOf(hullFacing, out bool hflip);
        Cell(hull, hrow, hflip, hullFrame, feet, worldScale, screenScale, team, alpha);
        if (turret == null) return;
        var lv = hull.Levels[hull.LevelFor(screenScale)];
        float k = worldScale / hull.Scale * lv.Scale;
        int trow = turret.RowOf(turretFacing, out bool tflip);
        float ws = worldScale * turret.Scale / hull.Scale, ss = screenScale * turret.Scale / hull.Scale;
        if (lv.Mounts is { } many)
        {
            // 포탑 여럿: 같은 포탑 시트를 장착점마다 그린다 (멀리 = 화면 위쪽부터, 차체 위). fireMount ≥ 0 이면 그 포탑만 사격 칸, 나머지는 idleFrame.
            var order = Enumerable.Range(0, many.Length).OrderBy(i => hrow < many[i].Length ? many[i][hrow].Y : 0f).ToArray();
            foreach (int i in order)
            {
                Vector2 mi = hrow < many[i].Length ? many[i][hrow] : Vector2.Zero;
                int f = fireMount >= 0 && i != fireMount % many.Length ? idleFrame : turretFrame;
                Cell(turret, trow, tflip, f, feet + new Vector2(hflip ? -mi.X : mi.X, mi.Y) * k, ws, ss, team, alpha);
            }
            return;
        }
        Vector2 m = lv.Mount is { } mt && hrow < mt.Length ? mt[hrow] : Vector2.Zero;
        var pivot = feet + new Vector2(hflip ? -m.X : m.X, m.Y) * k;
        // 포탑 LOD 는 차체와 같은 번호를 쓴다 (포탑 LOD0 1px 의 화면 크기 = 차체와 같은 줌)
        Cell(turret, trow, tflip, turretFrame, pivot, ws, ss, team, alpha);
    }
}
