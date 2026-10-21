using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>theme.json 의 스프라이트 항목. 프레임은 목록 (지금은 첫 장만 사용, 애니메이션은 나중에).</summary>
public sealed class ThemeEntry
{
    [JsonPropertyName("base")] public string? Base { get; set; }                  // 한 장짜리 축약 표기
    [JsonPropertyName("team")] public string? Team { get; set; }
    [JsonPropertyName("frames")] public JsonElement Frames { get; set; }          // 기본(비팀색) 프레임 파일 목록 (문자열 배열), 시트 항목에서는 프레임 수(정수)
    [JsonPropertyName("teamFrames")] public List<string>? TeamFrames { get; set; } // 팀색 프레임들 (회색조)
    [JsonPropertyName("variants")] public List<string>? Variants { get; set; }    // 지형: 변형 목록
    [JsonPropertyName("size")] public int[]? Size { get; set; }                   // 줌 1 에서의 픽셀 크기

    // ---- 방향 · 동작 시트 (game-tools/image 규격: 행 = 방향, 열 = 동작 프레임) ----
    [JsonPropertyName("sheet")] public string? Sheet { get; set; }
    [JsonPropertyName("teamSheet")] public string? TeamSheet { get; set; }
    [JsonPropertyName("frame")] public JsonElement Frame { get; set; }            // 프레임 픽셀 크기 [w, h] (정수 하나면 정사각)
    [JsonPropertyName("fps")] public float Fps { get; set; }
    [JsonPropertyName("variantRows")] public int VariantRows { get; set; }          // 효과 시트: 변형 수 (행 = 변형, 무작위로 하나 재생)
    public int[]? FrameSize => Frame.ValueKind == JsonValueKind.Array && Frame.GetArrayLength() == 2 ? new[] { Frame[0].GetInt32(), Frame[1].GetInt32() }
        : Frame.ValueKind == JsonValueKind.Number ? new[] { Frame.GetInt32(), Frame.GetInt32() } : null;
    public List<string>? FrameFiles => Frames.ValueKind == JsonValueKind.Array ? Frames.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : null;
    public int FrameCount => Frames.ValueKind == JsonValueKind.Number ? Frames.GetInt32() : 0;
    [JsonPropertyName("dirs")] public List<string>? Dirs { get; set; }            // 행 순서: s, se, e, ne, n
    [JsonPropertyName("mirror")] public bool Mirror { get; set; }                 // sw · w · nw 는 se · e · ne 좌우 반전
    [JsonPropertyName("frameNames")] public List<string>? FrameNames { get; set; }
    [JsonPropertyName("anims")] public Dictionary<string, JsonElement>? Anims { get; set; }   // 동작 → 열 번호들 (shootVariants 는 [[조준,발사],..] 이중 배열)
    [JsonPropertyName("anchor")] public int[]? Anchor { get; set; }               // 발 위치 (프레임 안)
    [JsonPropertyName("anchors")] public string? Anchors { get; set; }            // 칸별 발 위치 json {방향: [[x,y] × 열]} (선택)
    [JsonPropertyName("scale")] public float Scale { get; set; } = 1;             // 월드 표시 배율 (줌 1 기준)
    [JsonPropertyName("portrait")] public string? Portrait { get; set; }
    [JsonPropertyName("portraitTeam")] public string? PortraitTeam { get; set; }
    /// <summary>다른 키의 스프라이트를 그대로 쓴다 (예: "unit.recruit": { "alias": "unit.infantry" }). 전용 그림이 생기면 이 항목만 교체.</summary>
    [JsonPropertyName("alias")] public string? Alias { get; set; }

    // ---- 새 유닛 시트 형식 (game-tools v0.5: 128×160 + @64 + @32 / 차량 192² · 160² + LOD, _layout.json) ----
    /// <summary>LOD 마다 레이아웃 json 하나 (크기 순서 무관). 있으면 <see cref="LodSprite"/> 로 읽는다. scale = LOD 0 시트 1px 의 월드 px.</summary>
    [JsonPropertyName("layouts")] public List<string>? Layouts { get; set; }
    /// <summary>작은 LOD 를 이 배율까지 확대해 쓰고, 넘으면 다음 큰 LOD (기본 1.15).</summary>
    [JsonPropertyName("lodUpscale")] public float LodUpscale { get; set; } = 1.15f;
}

public sealed class ThemeFile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    /// <summary>이 테마에 없는 키 · 이름 · 지형은 이 테마에서 가져온다 (예: kd1920 → ww2).</summary>
    [JsonPropertyName("inherit")] public string? Inherit { get; set; }
    [JsonPropertyName("names")] public Dictionary<string, string> Names { get; set; } = new();  // 표시 이름 덮어쓰기
    [JsonPropertyName("sprites")] public Dictionary<string, ThemeEntry> Sprites { get; set; } = new();
}

/// <summary>불러온 스프라이트 한 종류 (프레임별 텍스처).</summary>
public sealed class ThemeSprite
{
    public Texture2D[] Base = Array.Empty<Texture2D>();
    public Texture2D?[] Team = Array.Empty<Texture2D?>();
    public Vector2 Size;   // 줌 1 에서의 픽셀 크기 (첫 프레임 기준)

    // 방향 · 동작 시트 (있으면 회전 대신 방향별 그림)
    public bool Directional;
    public Texture2D Sheet, SheetSmooth;             // 아틀라스 페이지: 확대용(점 필터) / 축소용(밉맵 · 부드러운 필터). 모든 시트가 같은 페이지를 공유
    public int AtlasX, AtlasY;                       // 페이지 안에서 이 시트(기본 | 팀색 가로 이음)의 왼쪽 위 픽셀
    public int FrameW, FrameH, DirCount;
    public bool Mirror;
    /// <summary>합친 시트(기본 | 팀색)에서 팀색 절반의 가로 시작 위치 (0 이면 팀색 없음). 한 텍스처라서 몸체 + 팀색 그리기가 배치를 끊지 않는다.</summary>
    public int TeamX;
    public string[] Dirs = Array.Empty<string>();
    /// <summary>행 방향 anchor (전차: 차체 접지점 / 포탑 pivot). 칸별 anchor 가 없을 때 쓴다.</summary>
    public Vector2[]? RowAnchor;
    /// <summary>전차 차체의 포탑 장착점 [행].</summary>
    public Vector2[]? Mounts;
    public bool IsTurret;
    public Dictionary<string, int[]> Anims = new();
    /// <summary>사격 변형 [[조준, 발사], ...] (새 16칸 시트). 없으면 null — Anims["shoot"] 한 가지.</summary>
    public int[][]? ShootVariants;
    public float Fps;
    /// <summary>효과 시트의 변형 수 (행). 1 = 변형 없음.</summary>
    public int Variants = 1;
    public Vector2 Anchor;
    /// <summary>칸별 발 위치 [행][열] (없으면 Anchor). 조준 · 발사처럼 몸을 뒤로 뺀 칸에서도 발이 튀지 않게.</summary>
    public Vector2[][]? CellAnchors;
    public float Scale = 1;
    public Vector2 AnchorAt(int row, int col) =>
        CellAnchors != null && row < CellAnchors.Length && col < CellAnchors[row].Length ? CellAnchors[row][col]
        : RowAnchor != null && row < RowAnchor.Length ? RowAnchor[row] : Anchor;
    public int RowByName(string name) => Array.IndexOf(Dirs, name);
    public Texture2D? Portrait, PortraitTeam;
    /// <summary>새 형식 (LOD 시트 · 레이아웃). 있으면 Sheet · Anims 대신 이것으로 그린다.</summary>
    public LodSprite? Lod;

    public int[] Anim(string name) => Anims.TryGetValue(name, out var a) && a.Length > 0 ? a : Anims.TryGetValue("idle", out var i) ? i : new[] { 0 };
}

/// <summary>
/// 교체 가능한 이미지 테마 (assets/themes/&lt;id&gt;/theme.json). 키: unit.&lt;key&gt; · unit.&lt;key&gt;.turret ·
/// building.&lt;key&gt; · building.&lt;key&gt;.gun · terrain.&lt;grass|dirt|sand|water|rock&gt;.
/// 항목이 없거나 테마가 없으면 호출 측이 기존 벡터 그림으로 되돌아간다. 텍스처는 처음 쓸 때 불러온다 (창 생성 이후).
/// </summary>
public sealed class Theme : IDisposable
{
    /// <summary>현재 테마. null 이면 벡터 그림.</summary>
    public static Theme? Current { get; private set; }

    /// <summary>테마 폴더 id 로 교체. 없거나 읽기 실패면 null (벡터 그림). 창 생성 이후 호출.</summary>
    public static void Use(string? id)
    {
        Current?.Dispose();
        Current = null;
        if (string.IsNullOrWhiteSpace(id) || id == "none" || id == "vector") return;
        try { Current = Open(id, 0); }
        catch (Exception e) { Console.Error.WriteLine($"테마 '{id}' 읽기 실패: {e.Message}"); }
        if (Current == null) Console.Error.WriteLine($"테마 '{id}' 없음 — 벡터 그림 사용");
    }

    static Theme? Open(string id, int depth)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "themes", id);
        string file = Path.Combine(dir, "theme.json");
        if (!File.Exists(file)) return null;
        var data = JsonSerializer.Deserialize<ThemeFile>(File.ReadAllText(file)) ?? new ThemeFile();
        var t = new Theme(id, dir, data);
        if (data.Inherit is string pid && pid != id && depth < 3)
        {
            t.parent = Open(pid, depth + 1);
            if (t.parent != null) t.parent.shadowed = new HashSet<string>(data.Sprites.Keys);
        }
        return t;
    }

    /// <summary>[측정] 테마 텍스처 메모리 (바이트): 옛 아틀라스 (점 필터본 + 밉맵본, 상속 포함) + 새 LOD 페이지.</summary>
    public long AtlasBytes => OldAtlasBytes + (lodAtlas?.Bytes ?? 0);
    long OldAtlasBytes => atlasBytes + (parent?.OldAtlasBytes ?? 0);
    public string MemoryInfo => $"테마 {Id}: 옛 아틀라스 {OldAtlasBytes / 1048576.0:0.0} MB + LOD 페이지 {(lodAtlas?.Bytes ?? 0) / 1048576.0:0.0} MB ({lodAtlas?.Info})";
    long atlasBytes;
    Theme? parent;
    HashSet<string>? shadowed;   // 자식 테마가 덮어쓴 키 (아틀라스에 넣지 않음)
    LodAtlas? lodAtlas;

    public string Id { get; }
    public string Name => data.Name;
    readonly string dir;
    readonly ThemeFile data;
    readonly Dictionary<string, ThemeSprite?> sprites = new();
    readonly Dictionary<string, Image[]?> terrain = new();
    readonly HashSet<string> aliased = new();   // 별칭 키 (텍스처를 소유하지 않음)
    int aliasDepth;

    Theme(string id, string dir, ThemeFile data) { Id = id; this.dir = dir; this.data = data; }

    public string UnitName(UnitDef d) => data.Names.TryGetValue(d.Key, out var n) ? n : parent?.UnitName(d) ?? d.Name;
    public string BuildingName(BuildingDef d) => data.Names.TryGetValue(d.Key, out var n) ? n : parent?.BuildingName(d) ?? d.Name;

    /// <summary>키의 스프라이트 (없으면 null). 처음 부르면 텍스처를 불러온다.</summary>
    public ThemeSprite? Get(string key)
    {
        if (sprites.TryGetValue(key, out var cached)) return cached;
        ThemeSprite? s = null;
        if (data.Sprites.TryGetValue(key, out var e))
        {
            // 별칭: 대상 스프라이트를 공유 (Dispose 에서 중복 해제하지 않도록 aliases 에만 기록)
            if (e.Alias != null && e.Alias != key && aliasDepth < 4)
            {
                aliasDepth++;
                try { s = Get(e.Alias); }
                finally { aliasDepth--; }
                sprites[key] = s;
                if (s != null) aliased.Add(key);
                return s;
            }
            try { s = LoadSprite(e, key); }
            catch (Exception ex) { Console.Error.WriteLine($"테마 스프라이트 '{key}' 실패: {ex.Message}"); }
        }
        else if (key.EndsWith(".turret") && data.Sprites.TryGetValue(key[..^7], out var be) && be.Alias != null && aliasDepth < 4)
        {
            // 별칭 차체의 포탑: 별칭 키 "unit.X" → "unit.Y" 이면 "unit.X.turret" 도 "unit.Y.turret" 을 쓴다 (kd1920 은 이제 tank_ft_male 이 원본이라 별칭 없음)
            aliasDepth++;
            try { s = Get(be.Alias + ".turret"); }
            finally { aliasDepth--; }
            sprites[key] = s;
            if (s != null) aliased.Add(key);
            return s;
        }
        else if (parent != null)
        {
            // 상속: 부모 테마의 스프라이트 (부모가 소유 — 여기서는 해제하지 않음)
            s = parent.Get(key);
            sprites[key] = s;
            if (s != null) aliased.Add(key);
            return s;
        }
        sprites[key] = s;
        return s;
    }

    ThemeSprite? LoadSprite(ThemeEntry e, string key)
    {
        if (e.Layouts is { Count: > 0 }) return LoadLod(e, key);
        if (e.Sheet != null) return LoadSheet(e, key, key.EndsWith(".turret"));
        var frames = e.FrameFiles ?? (e.Base != null ? new List<string> { e.Base } : null);
        if (frames == null || frames.Count == 0) return null;
        var teams = e.TeamFrames ?? (e.Team != null ? new List<string> { e.Team } : new List<string>());
        var s = new ThemeSprite { Base = new Texture2D[frames.Count], Team = new Texture2D?[frames.Count] };
        for (int i = 0; i < frames.Count; i++)
        {
            s.Base[i] = LoadTex(frames[i]);
            if (i < teams.Count) s.Team[i] = LoadTex(teams[i]);
        }
        s.Size = e.Size is { Length: 2 } ? new Vector2(e.Size[0], e.Size[1]) : new Vector2(s.Base[0].Width, s.Base[0].Height);
        return s;
    }

    /// <summary>방향 · 동작 시트 불러오기. 기본 시트와 팀색 시트를 가로로 이어 한 텍스처로 만든다 (배치 유지).
    /// 축소 표시용으로 밉맵 + 부드러운 필터 사본을 하나 더 만든다.</summary>
    ThemeSprite LoadSheet(ThemeEntry e, string key, bool isTurret)
    {
        var s = new ThemeSprite { Directional = true, Mirror = e.Mirror, Scale = e.Scale, IsTurret = isTurret };
        ParseAnims(e.Anims, s);
        EnsureAtlas();
        if (!placed.TryGetValue(key, out var pl)) throw new FileNotFoundException(e.Sheet);
        s.Sheet = pages[pl.Page].Point; s.SheetSmooth = pages[pl.Page].Smooth;
        s.AtlasX = pl.X; s.AtlasY = pl.Y; s.TeamX = pl.TeamX;
        s.FrameW = e.FrameSize is { } f ? f[0] : 64;
        s.FrameH = e.FrameSize is { } g ? g[1] : 80;
        if (e.FrameCount > 0 && !s.Anims.ContainsKey("play")) s.Anims["play"] = Enumerable.Range(0, e.FrameCount).ToArray();   // 효과 시트: 한 줄 재생
        s.Fps = e.Fps;
        s.Variants = Math.Max(1, e.VariantRows);
        s.Dirs = (e.Dirs ?? new List<string> { "s", "se", "e", "ne", "n" }).ToArray();
        s.DirCount = s.Dirs.Length;
        s.Anchor = e.Anchor is { Length: 2 } a ? new Vector2(a[0], a[1]) : new Vector2(s.FrameW / 2f, s.FrameH - 8);
        if (e.Anchors != null && File.Exists(Path.Combine(dir, e.Anchors)))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, e.Anchors)));
            var root = doc.RootElement;
            Vector2 P(JsonElement arr) => new(arr[0].GetSingle(), arr[1].GetSingle());
            if (root.TryGetProperty("hull", out var hull) && root.TryGetProperty("turret", out var tur))
            {
                // 전차 형식: 행마다 차체 접지점(ground) · 포탑 장착점(mount) / 포탑 pivot
                s.RowAnchor = s.Dirs.Select(d => isTurret
                    ? (tur.TryGetProperty(d, out var t) && t.TryGetProperty("pivot", out var pv) ? P(pv) : s.Anchor)
                    : (hull.TryGetProperty(d, out var h) && h.TryGetProperty("ground", out var gr) ? P(gr) : s.Anchor)).ToArray();
                s.Mounts = s.Dirs.Select(d => hull.TryGetProperty(d, out var h) && h.TryGetProperty("mount", out var mt) ? P(mt) : s.Anchor).ToArray();
            }
            else if (root.TryGetProperty("ground", out var gnd) && gnd.ValueKind == JsonValueKind.Array && gnd.GetArrayLength() == 2)
            {
                s.Anchor = P(gnd);   // 포탑 없는 차량 (자주포): 행 · 열 공통 접지점
            }
            else
            {
                // 칸별 형식: {방향: [[x, y] × 열]}
                s.CellAnchors = s.Dirs.Select(d => root.TryGetProperty(d, out var row)
                    ? row.EnumerateArray().Select(p => p.GetArrayLength() == 2 ? P(p) : s.Anchor).ToArray()
                    : Array.Empty<Vector2>()).ToArray();
            }
        }
        s.Size = new Vector2(s.FrameW * s.Scale, s.FrameH * s.Scale);
        s.Base = new[] { s.Sheet };
        s.Team = new Texture2D?[] { null };
        if (e.Portrait != null) s.Portrait = LoadTex(e.Portrait, smooth: true);
        if (e.PortraitTeam != null) s.PortraitTeam = LoadTex(e.PortraitTeam, smooth: true);
        return s;
    }

    /// <summary>새 형식: 레이아웃들을 읽어 LOD 스프라이트로. 텍스처는 그 LOD 를 처음 그릴 때 아틀라스로 만든다.</summary>
    ThemeSprite LoadLod(ThemeEntry e, string key)
    {
        var lod = LodSprite.Load(key, dir, e.Layouts!, e.Scale, e.LodUpscale);
        (lodAtlas ??= new LodAtlas(dir)).Register(lod);
        var l0 = lod.Levels[0];
        var s = new ThemeSprite
        {
            Directional = true, Lod = lod, Mirror = lod.Mirror, Scale = lod.Scale, IsTurret = key.EndsWith(".turret"),
            FrameW = l0.FrameW, FrameH = l0.FrameH, Anchor = l0.Anchor, Dirs = lod.Dirs, DirCount = lod.DirCount,
        };
        s.Size = new Vector2(s.FrameW * s.Scale, s.FrameH * s.Scale);
        if (e.Portrait != null) s.Portrait = LoadTex(e.Portrait, smooth: true);
        if (e.PortraitTeam != null) s.PortraitTeam = LoadTex(e.PortraitTeam, smooth: true);
        return s;
    }

    /// <summary>옛 형식 아틀라스에 넣을 항목인가: 자식 테마가 덮어쓴 키 · 지금 규칙에 없는 유닛은 뺀다 (메모리).</summary>
    bool AtlasWanted(string key)
    {
        if (shadowed != null && shadowed.Contains(key)) return false;
        if (!key.StartsWith("unit.")) return true;
        static string UnitOf(string k) { var u = k.Substring(5); return u.EndsWith(".turret") ? u[..^7] : u; }
        if (Defs.Current.Units.ContainsKey(UnitOf(key))) return true;
        // 다른 유닛 항목이 별칭으로 가리키면 필요
        return data.Sprites.Any(p => p.Value.Alias == key && p.Key.StartsWith("unit.") && Defs.Current.Units.ContainsKey(UnitOf(p.Key)));
    }

    // ------------------------------------------------------------ 텍스처 아틀라스
    // 방향 시트(기본 | 팀색 가로 이음) 전부를 페이지 몇 장(기본 2048², 시트가 더 넓으면 4096²)에 한꺼번에 담는다.
    // 종류가 섞여도 텍스처 전환이 없다. 페이지마다 점 필터본과 밉맵(부드러운) 본 두 벌. 시트 사이는 밉맵 번짐 방지용 여백.
    sealed class AtlasPage { public Texture2D Point, Smooth; }
    readonly record struct Placement(int Page, int X, int Y, int TeamX);
    readonly List<AtlasPage> pages = new();
    readonly Dictionary<string, Placement> placed = new();
    bool atlasBuilt;
    const int AtlasGutter = 8;
    public int AtlasPageCount => pages.Count;
    public string AtlasInfo { get; private set; } = "";

    sealed class Packable { public string Key = ""; public Image Base, Team; public int W, H; public bool HasTeam; public int Page, X, Y; }

    void EnsureAtlas()
    {
        if (atlasBuilt) return;
        atlasBuilt = true;
        var items = new List<Packable>();
        foreach (var (key, e) in data.Sprites)
        {
            if (e.Sheet == null || e.Alias != null || !AtlasWanted(key)) continue;
            var b = Raylib.LoadImage(Path.Combine(dir, e.Sheet));
            if (b.Width == 0) { Console.Error.WriteLine($"테마 시트 '{e.Sheet}' 읽기 실패"); continue; }
            var it = new Packable { Key = key, Base = b, W = b.Width, H = b.Height };
            if (e.TeamSheet != null)
            {
                var t = Raylib.LoadImage(Path.Combine(dir, e.TeamSheet));
                if (t.Width > 0) { it.Team = t; it.HasTeam = true; it.W = b.Width * 2; }
                else Raylib.UnloadImage(t);
            }
            items.Add(it);
        }
        if (items.Count == 0) return;
        items.Sort((a, b) => b.H.CompareTo(a.H));
        // 선반(shelf) 배치. 2048² 한 장에 안 들어가면 4096² 로 올려 가능하면 한 장에 (그래도 넘치면 여러 장)
        int size = items.Max(i => i.W) + AtlasGutter > 2048 ? 4096 : 2048, page = 0;
        List<int> pageH;
        while (true)
        {
            int x = 0, y = 0, shelfH = 0; page = 0; pageH = new List<int>();
            foreach (var it in items)
            {
                if (x + it.W + AtlasGutter > size) { x = 0; y += shelfH + AtlasGutter; shelfH = 0; }
                if (y + it.H + AtlasGutter > size) { pageH.Add(y + shelfH + AtlasGutter); page++; x = 0; y = 0; shelfH = 0; }
                it.Page = page; it.X = x + AtlasGutter / 2; it.Y = y + AtlasGutter / 2;
                x += it.W + AtlasGutter; shelfH = Math.Max(shelfH, it.H);
            }
            pageH.Add(y + shelfH + AtlasGutter);
            if (page == 0 || size >= 4096) break;
            size = 4096;
        }
        var clear = new Color((byte)0, (byte)0, (byte)0, (byte)0);
        for (int p = 0; p <= page; p++)
        {
            var img = Raylib.GenImageColor(size, Math.Min(size, pageH[p]), clear);
            foreach (var it in items.Where(i => i.Page == p))
            {
                Raylib.ImageDraw(ref img, it.Base, new Rectangle(0, 0, it.Base.Width, it.Base.Height), new Rectangle(it.X, it.Y, it.Base.Width, it.Base.Height), Color.White);
                if (it.HasTeam)
                    Raylib.ImageDraw(ref img, it.Team, new Rectangle(0, 0, it.Team.Width, it.Team.Height), new Rectangle(it.X + it.Base.Width, it.Y, it.Team.Width, it.Team.Height), Color.White);
                placed[it.Key] = new Placement(p, it.X, it.Y, it.HasTeam ? it.Base.Width : 0);
            }
            pages.Add(new AtlasPage { Point = TexFromImage(img, false), Smooth = TexFromImage(img, true) });
            atlasBytes += (long)img.Width * img.Height * 4 * 7 / 3;   // 점 필터본 1 + 밉맵본 4/3
            AtlasInfo += (p > 0 ? ", " : "") + $"{img.Width}x{img.Height}";
            Raylib.UnloadImage(img);
        }
        foreach (var it in items) { Raylib.UnloadImage(it.Base); if (it.HasTeam) Raylib.UnloadImage(it.Team); }
        Console.Error.WriteLine($"[atlas] {Id}: {items.Count}시트 -> 페이지 {pages.Count}장 ({AtlasInfo}), {atlasBytes / 1048576.0:0.0} MB (점 필터본 + 밉맵본)");
    }

    static void ParseAnims(Dictionary<string, JsonElement>? raw, ThemeSprite s)
    {
        if (raw == null) return;
        foreach (var (name, el) in raw)
        {
            if (el.ValueKind != JsonValueKind.Array) continue;
            if (el.GetArrayLength() > 0 && el[0].ValueKind == JsonValueKind.Array)
            {
                if (name == "shootVariants")
                    s.ShootVariants = el.EnumerateArray().Select(v => v.EnumerateArray().Select(n => n.GetInt32()).ToArray()).Where(v => v.Length > 0).ToArray();
            }
            else s.Anims[name] = el.EnumerateArray().Select(n => n.GetInt32()).ToArray();
        }
    }

    static Texture2D TexFromImage(Image img, bool smooth)
    {
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
        if (smooth)
        {
            Raylib.GenTextureMipmaps(ref tex);
            Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
        }
        else Raylib.SetTextureFilter(tex, TextureFilter.Point);
        return tex;
    }

    Texture2D LoadTex(string rel, bool smooth)
    {
        var tex = LoadTex(rel);
        if (!smooth) return tex;
        Raylib.GenTextureMipmaps(ref tex);
        Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
        return tex;
    }

    Texture2D LoadTex(string rel)
    {
        var tex = Raylib.LoadTexture(Path.Combine(dir, rel));
        if (tex.Id == 0) throw new FileNotFoundException(rel);
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
        return tex;
    }

    /// <summary>지형 타일 이미지 변형들 (CPU 이미지, 청크 굽기용). 없으면 null.</summary>
    public Image[]? TerrainImages(TerrainType t, int size = Config.TileSize) => TerrainImages(t.ToString().ToLowerInvariant(), size);

    /// <summary>이 테마(또는 부모)에 "terrain.&lt;name&gt;" 그림이 있는지.</summary>
    public bool HasTerrain(string name) => data.Sprites.ContainsKey("terrain." + name) || (parent?.HasTerrain(name) ?? false);

    /// <summary>이름으로 찾는 지형 타일 (forest_edge · trench_h · high_ground_rim_n …).</summary>
    public Image[]? TerrainImages(string name, int size = Config.TileSize)
    {
        string baseKey = "terrain." + name;
        if (!data.Sprites.ContainsKey(baseKey) && parent != null) return parent.TerrainImages(name, size);
        string key = size == Config.TileSize ? baseKey : baseKey + "@" + size;
        if (terrain.TryGetValue(key, out var cached)) return cached;
        Image[]? imgs = null;
        if (data.Sprites.TryGetValue(baseKey, out var e) && e.Variants is { Count: > 0 } v)
        {
            var list = new List<Image>();
            foreach (var rel in v)
            {
                // 줌 LOD: "x.png" 옆의 "x@64.png" · "x@128.png", 없으면 기본 그림을 NN 확대.
                string hi = size == Config.TileSize ? rel : Path.ChangeExtension(rel, null) + "@" + size + Path.GetExtension(rel);
                var img = Raylib.LoadImage(Path.Combine(dir, File.Exists(Path.Combine(dir, hi)) ? hi : rel));
                if (img.Width == 0) continue;
                if (img.Width != size || img.Height != size)
                    Raylib.ImageResizeNN(ref img, size, size);
                list.Add(img);
            }
            if (list.Count > 0) imgs = list.ToArray();
        }
        terrain[key] = imgs;
        return imgs;
    }

    /// <summary>
    /// 스프라이트를 center 에 그린다 (회전 중심 = 이미지 중심). scale 은 줌 1 픽셀 크기 대비 배율,
    /// rotationDeg 0 = 위쪽을 향함. 팀 레이어는 team 색을 곱해 덧그린다.
    /// </summary>
    public static void Draw(ThemeSprite s, Vector2 center, float scale, float rotationDeg, Color team, int alpha = 255, int frame = 0)
    {
        int f = s.Base.Length == 0 ? 0 : ((frame % s.Base.Length) + s.Base.Length) % s.Base.Length;
        var tex = s.Base[f];
        float w = tex.Width * scale, h = tex.Height * scale;
        var dest = new Rectangle(center.X, center.Y, w, h);
        var origin = new Vector2(w / 2, h / 2);
        var src = new Rectangle(0, 0, tex.Width, tex.Height);
        Raylib.DrawTexturePro(tex, src, dest, origin, rotationDeg, new Color((byte)255, (byte)255, (byte)255, (byte)alpha));
        if (s.Team[f] is Texture2D tt)
            Raylib.DrawTexturePro(tt, new Rectangle(0, 0, tt.Width, tt.Height), dest, origin, rotationDeg, new Color(team.R, team.G, team.B, (byte)alpha));
    }

    /// <summary>
    /// facing(라디안, 0 = 오른쪽, π/2 = 아래) 에 맞는 시트 행과 좌우 반전. 행은 남(s)에서 동을 거쳐 북(n)까지 반원을 균등 분할
    /// (5행 = 45도 · 9행 = 22.5도 간격), 서쪽 반원은 반전 → 8 · 16 방향.
    /// </summary>
    public static int RowOf(ThemeSprite s, float facing, out bool flip)
    {
        float yaw = (90f - facing * (180f / MathF.PI)) % 360f;
        if (yaw < 0) yaw += 360f;
        flip = false;
        if (yaw > 180f) { yaw = 360f - yaw; flip = true; }
        int rows = s.DirCount;
        if (rows <= 1) { flip = flip && s.Mirror; return 0; }
        int row = (int)MathF.Round(yaw / (180f / (rows - 1)));
        if (!s.Mirror) flip = false;
        return Math.Min(row, rows - 1);
    }

    /// <summary>시트의 한 칸을 그린다. 팀색 레이어는 같은 텍스처의 오른쪽 절반이라 배치가 끊기지 않는다.</summary>
    public static void DrawCell(ThemeSprite s, int row, bool flip, int column, Vector2 feet, float scale, bool smooth, Color team, int alpha = 255)
    {
        var tex = smooth ? s.SheetSmooth : s.Sheet;
        var an = s.AnchorAt(row, column);
        float ax = flip ? s.FrameW - an.X : an.X;
        var src = new Rectangle(s.AtlasX + column * s.FrameW, s.AtlasY + row * s.FrameH, flip ? -s.FrameW : s.FrameW, s.FrameH);
        var dest = new Rectangle(feet.X - ax * scale, feet.Y - an.Y * scale, s.FrameW * scale, s.FrameH * scale);
        Raylib.DrawTexturePro(tex, src, dest, Vector2.Zero, 0, new Color((byte)255, (byte)255, (byte)255, (byte)alpha));
        if (s.TeamX > 0)
        {
            src.X += s.TeamX;
            Raylib.DrawTexturePro(tex, src, dest, Vector2.Zero, 0, new Color(team.R, team.G, team.B, (byte)alpha));
        }
    }

    /// <summary>
    /// 방향 · 동작 시트 그리기. feet = 발 위치(월드/화면 좌표), scale = 시트 픽셀 → 좌표 배율 (줌은 Camera2D 가 따로 곱함),
    /// screenScale = 실제 화면에서의 배율 (0.75 미만이면 축소용 부드러운 텍스처 사용).
    /// </summary>
    public static void DrawDirectional(ThemeSprite s, Vector2 feet, float scale, float screenScale, float facing, int column,
        Color team, int alpha = 255)
    {
        int row = RowOf(s, facing, out bool flip);
        DrawCell(s, row, flip, column, feet, scale, screenScale < 0.75f, team, alpha);
    }

    /// <summary>초상화를 사각형에 그린다 (세력 색 레이어 포함). 초상화가 없으면 false.</summary>
    public static bool DrawPortrait(ThemeSprite s, Rectangle r, Color team)
    {
        if (s.Portrait is not Texture2D p) return false;
        var src = new Rectangle(0, 0, p.Width, p.Height);
        Raylib.DrawTexturePro(p, src, r, Vector2.Zero, 0, Color.White);
        if (s.PortraitTeam is Texture2D pt) Raylib.DrawTexturePro(pt, src, r, Vector2.Zero, 0, team);
        return true;
    }

    /// <summary>회전 없이 사각형 안에 늘여 그린다 (건물 몸체, 화면/월드 공용).</summary>
    public static void DrawRect(ThemeSprite s, Rectangle r, Color team, int alpha = 255, int frame = 0)
    {
        float scale = r.Width / s.Size.X;
        Draw(s, new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), scale, 0, team, alpha, frame);
    }

    public void Dispose()
    {
        foreach (var (key, s) in sprites)
        {
            if (s == null || aliased.Contains(key)) continue;
            if (!s.Directional) foreach (var t in s.Base) Raylib.UnloadTexture(t);
            if (!s.Directional) foreach (var t in s.Team) if (t is Texture2D tt) Raylib.UnloadTexture(tt);
            if (s.Portrait is Texture2D p) Raylib.UnloadTexture(p);
            if (s.PortraitTeam is Texture2D pt) Raylib.UnloadTexture(pt);
        }
        foreach (var pg in pages) { Raylib.UnloadTexture(pg.Point); Raylib.UnloadTexture(pg.Smooth); }
        pages.Clear(); placed.Clear(); atlasBuilt = false; atlasBytes = 0;
        lodAtlas?.Dispose(); lodAtlas = null;
        parent?.Dispose(); parent = null;
        sprites.Clear();
        aliased.Clear();
        foreach (var imgs in terrain.Values)
            if (imgs != null) foreach (var i in imgs) Raylib.UnloadImage(i);
        terrain.Clear();
    }
}
