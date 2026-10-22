using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MapEditor.Model;

/// <summary>칠하기 한 줄 (MapFile.Paint 와 같은 의미: rect, circle, tiles 순서로 같은 지형을 덮어쓴다).</summary>
public sealed class PaintOp
{
    /// <summary>지형 이름 또는 높이만 바꾸는 "high" / "low".</summary>
    public string Terrain = "grass";
    public int[]? Rect;
    public int[]? Circle;
    public List<int[]>? Tiles;
    /// <summary>꺾은선 [x0, y0, x1, y1, ...] (게임 MapFile paint "line", rail 브랜치): 1칸 폭 8방향 직선으로 이어 칠한다.</summary>
    public int[]? Line;
    /// <summary>꺾은선 폭 (칸, 1~6, 생략 = 1; river-r2 의 paint "width"). 편집기 UI 는 없고 파일의 값을 읽고 그대로 쓰며 래스터에만 반영 -- line 에만 영향.</summary>
    public int? Width;
    public bool? High;
    /// <summary>이 줄 바로 앞의 // 주석 (파일에서 읽은 것, 다시 쓸 때 보존).</summary>
    public List<string> Comments = new();
    /// <summary>이 단계가 모르는 키 (rail 의 line / width, 강의 ford 등) 의 원문. 읽은 순서대로 그대로 다시 쓴다.</summary>
    public List<ExtraField> Extra = new();

    public PaintOp Clone() => new()
    {
        Terrain = Terrain, Rect = Rect?.ToArray(), Circle = Circle?.ToArray(),
        Tiles = Tiles?.Select(t => t.ToArray()).ToList(), Line = Line?.ToArray(), Width = Width, High = High, Comments = Comments.ToList(), Extra = Extra.ToList(),
    };
}

/// <summary>Extra = 이 단계가 모르는 키의 원문 (그대로 다시 씀).</summary>
public sealed record SiteDef(int X, int Y, string Type, int Owner, IReadOnlyList<ExtraField>? Extra = null);
public sealed record UnitDef(string Unit, double X, double Y, int Faction, int Count, IReadOnlyList<ExtraField>? Extra = null);

/// <summary>이 단계에서 편집하지 않는 최상위 필드 (factions, weather, time, timeStep, schedule, 알 수 없는 키) 의 원문.</summary>
public sealed record ExtraField(string Name, string RawJson);

/// <summary>래스터 결과: 칸별 지형 번호 (TerrainNames 인덱스) + 고지.</summary>
public sealed class MapGrid
{
    public const byte RailId = 11;   // TerrainNames 의 "rail"

    /// <summary>선로 선분 (칸 중심 기준 이웃 연결): 게임 RailRenderer.Segments 와 같은 규칙 -- 동 · 남, 대각(두 직각 이웃이 모두 선로가 아닐 때만), 외톨이는 가로 토막. 위 줄 → 아래, 왼쪽 → 오른쪽 순.</summary>
    public List<(int X, int Y, float Ax, float Ay, float Bx, float By)> RailSegments()
    {
        var res = new List<(int, int, float, float, float, float)>();
        bool R(int x, int y) => InBounds(x, y) && At(x, y) == RailId;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (At(x, y) != RailId) continue;
            float cx = x + 0.5f, cy = y + 0.5f;
            bool e = R(x + 1, y), s = R(x, y + 1), w = R(x - 1, y), n = R(x, y - 1);
            bool any = e || s || w || n;
            if (e) res.Add((x, y, cx, cy, cx + 1, cy));
            if (s) res.Add((x, y, cx, cy, cx, cy + 1));
            if (R(x + 1, y + 1) && !e && !s) { res.Add((x, y, cx, cy, cx + 1, cy + 1)); any = true; }
            if (R(x - 1, y + 1) && !w && !s) { res.Add((x, y, cx, cy, cx - 1, cy + 1)); any = true; }
            any |= R(x - 1, y - 1) && !w && !n || R(x + 1, y - 1) && !e && !n;
            if (!any) res.Add((x, y, cx - 0.5f, cy, cx + 0.5f, cy));
        }
        return res;
    }

    public readonly int Width, Height;
    public readonly byte[] Terrain;
    public readonly bool[] High;
    public MapGrid(int w, int h) { Width = w; Height = h; Terrain = new byte[w * h]; High = new bool[w * h]; }
    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;
    public byte At(int x, int y) => Terrain[y * Width + x];
}

/// <summary>
/// 지도 한 장의 편집 문서 (MapFile JSON 과 같은 필드). 모든 변경은 가역 명령 (Undo / Redo).
/// 직렬화는 정규화 (키 순서 고정, paint 순서 보존) 라 같은 문서는 같은 바이트다.
/// 선(L 도구) 과 채우기는 tiles / 1행 rect 로, 꺾은선(N 도구, 선로) 은 rail 브랜치의 paint `line` 으로 저장한다.
/// </summary>
public sealed class MapDoc
{
    public static readonly string[] TerrainNames = { "grass", "dirt", "sand", "water", "rock", "forest", "rubble", "house", "wire", "trench", "crater", "rail", "ford", "bridge" };   // 번호 = 게임 TerrainType 바이트 (rail 11 = rail 브랜치, ford 12 = river-r1, bridge 13 = river-r4-bridge); 앞 브랜치가 안 합쳐진 게임에서도 편집기는 이 이름들을 안다

    public string Name { get; private set; } = "";
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string Base { get; private set; } = "grass";
    public IReadOnlyList<PaintOp> Paint => paint;
    public IReadOnlyList<int[]> Starts => starts;
    public IReadOnlyList<SiteDef> Sites => sites;
    public IReadOnlyList<UnitDef> Units => units;
    public IReadOnlyList<ExtraField> Extras => extras;
    /// <summary>읽을 때 버려진 // 주석 수 (paint 줄 앞 주석만 보존). 0 이 아니면 저장 시 경고 대상.</summary>
    public int DroppedComments { get; private set; }

    readonly List<PaintOp> paint = new();
    readonly List<int[]> starts = new();
    readonly List<SiteDef> sites = new();
    readonly List<UnitDef> units = new();
    readonly List<ExtraField> extras = new();
    int paintVersion;
    MapGrid? cache;
    int cacheVersion = -1;

    readonly List<EditCommand> undo = new(), redo = new();
    List<EditCommand>? stroke;
    string strokeName = "";

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public int UndoDepth => undo.Count;

    public MapDoc(string name, int width, int height, string baseTerrain = "grass")
    { Name = name; Width = width; Height = height; Base = baseTerrain; }

    MapDoc() { }

    // ---------------------------------------------------------------- 읽기
    static readonly JsonDocumentOptions DocOpts = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>게임 Config.MaxMapSize 와 같은 값 (모델은 게임 비의존이라 값을 베낌; MapEditor.Tests 가 대조해 게임 상수가 바뀌면 실패).</summary>
    public const int MaxDim = 1500;
    /// <summary>게임 Config.MaxPlayers 와 같은 값 (시험이 대조).</summary>
    public const int MaxStarts = 16;
    readonly List<string> loadIssues = new();

    /// <summary>Parse 의 예외 없는 판: 성공하면 (문서, 빈 목록), 실패하면 (null, 오류 문장).</summary>
    public static (MapDoc? Doc, List<string> Errors) TryParse(string? text)
    {
        try { return (Parse(text ?? ""), new List<string>()); }
        catch (Exception e) { return (null, new List<string> { e.Message }); }
    }

    /// <summary>지도 JSON 텍스트 (// 주석, 끝 쉼표 허용) -> 문서. 어떤 쓰레기 입력도 InvalidDataException 하나로만 실패한다.</summary>
    public static MapDoc Parse(string text)
    {
        try { return ParseCore(text); }
        catch (InvalidDataException) { throw; }
        catch (Exception e) { throw new InvalidDataException("지도 JSON 필드 형식 오류: " + e.Message, e); }
    }

    static MapDoc ParseCore(string text)
    {
        JsonDocument jd;
        try { jd = JsonDocument.Parse(text, DocOpts); }
        catch (JsonException e) { throw new InvalidDataException("지도 JSON 오류: " + e.Message, e); }
        using (jd)
        {
            var root = jd.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("지도 JSON 최상위가 객체가 아님");
            var d = new MapDoc();
            var commentsByPaint = ScanPaintComments(text);
            int kept = 0;
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            {
                foreach (var p in root.EnumerateObject())
                {
                    bool dup = !seenKeys.Add(p.Name);
                    string lname = p.Name.ToLowerInvariant();
                    if (dup)
                    {
                        d.loadIssues.Add($"최상위 키 '{p.Name}' 중복 (대소문자 무시)");
                        if (lname is "name" or "width" or "height" or "base" or "paint" or "starts" or "sites" or "units") continue;
                    }
                    switch (lname)
                    {
                        case "name": d.Name = p.Value.GetString() ?? ""; break;
                        case "width": d.Width = p.Value.GetInt32(); break;
                        case "height": d.Height = p.Value.GetInt32(); break;
                        case "base": d.Base = p.Value.GetString() ?? "grass"; break;
                        case "paint":
                            int i = 0;
                            foreach (var e in p.Value.EnumerateArray())
                            {
                                var op = ReadPaint(e);
                                foreach (var x in op.Extra) kept += CountComments(x.RawJson);
                                if (commentsByPaint.TryGetValue(i, out var cs)) { op.Comments = cs; kept += cs.Count; }
                                d.paint.Add(op);
                                i++;
                            }
                            break;
                        case "starts":
                            foreach (var e in p.Value.EnumerateArray()) d.starts.Add(new[] { e[0].GetInt32(), e[1].GetInt32() });
                            break;
                        case "sites":
                            foreach (var e in p.Value.EnumerateArray())
                            {
                                var at = Find(e, "at");
                                var ex = ReadExtras(e, "at", "type", "owner");
                                foreach (var x in ex) kept += CountComments(x.RawJson);
                                d.sites.Add(new SiteDef(at[0].GetInt32(), at[1].GetInt32(),
                                    Find(e, "type") is { ValueKind: JsonValueKind.String } t ? t.GetString()! : "village",
                                    Find(e, "owner") is { ValueKind: JsonValueKind.Number } o ? o.GetInt32() : 0, ex.Count > 0 ? ex : null));
                            }
                            break;
                        case "units":
                            foreach (var e in p.Value.EnumerateArray())
                            {
                                var at = Find(e, "at");
                                var ex = ReadExtras(e, "unit", "at", "faction", "count");
                                foreach (var x in ex) kept += CountComments(x.RawJson);
                                d.units.Add(new UnitDef(Find(e, "unit").GetString() ?? "", at[0].GetDouble(), at[1].GetDouble(),
                                    Find(e, "faction") is { ValueKind: JsonValueKind.Number } f ? f.GetInt32() : 1,
                                    Find(e, "count") is { ValueKind: JsonValueKind.Number } c ? c.GetInt32() : 1, ex.Count > 0 ? ex : null));
                            }
                            break;
                        default:
                            d.extras.Add(new ExtraField(p.Name, p.Value.GetRawText()));
                            kept += CountComments(p.Value.GetRawText());
                            break;
                    }
                }
            }
            d.DroppedComments = CountComments(text) - kept;
            return d;
        }
    }

    static JsonElement Find(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return default;
    }

    static List<ExtraField> ReadExtras(JsonElement obj, params string[] known)
    {
        var list = new List<ExtraField>();
        foreach (var p in obj.EnumerateObject())
            if (Array.FindIndex(known, k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase)) < 0)
                list.Add(new ExtraField(p.Name, p.Value.GetRawText()));
        return list;
    }

    static PaintOp ReadPaint(JsonElement e)
    {
        var op = new PaintOp();
        foreach (var p in e.EnumerateObject())
        {
            switch (p.Name.ToLowerInvariant())
            {
                case "terrain": op.Terrain = p.Value.GetString() ?? "grass"; break;
                case "rect": op.Rect = p.Value.EnumerateArray().Select(v => v.GetInt32()).ToArray(); break;
                case "circle": op.Circle = p.Value.EnumerateArray().Select(v => v.GetInt32()).ToArray(); break;
                case "tiles": op.Tiles = p.Value.EnumerateArray().Select(t => t.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToList(); break;
                case "line" when TryFlatInts(p.Value, out var ln): op.Line = ln; break;
                case "width" when p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int wd): op.Width = wd; break;
                case "high": op.High = p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetBoolean(); break;
                default: op.Extra.Add(new ExtraField(p.Name, p.Value.GetRawText())); break;
            }
        }
        return op;
    }

    /// <summary>[정수, 정수, ...] 평평한 배열이면 true (중첩 · 실수 · 문자열이면 false: 그대로 여분 키로 보존).</summary>
    static bool TryFlatInts(JsonElement e, out int[] r)
    {
        r = Array.Empty<int>();
        if (e.ValueKind != JsonValueKind.Array) return false;
        var l = new List<int>();
        foreach (var v in e.EnumerateArray())
        {
            if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int i)) return false;
            l.Add(i);
        }
        r = l.ToArray();
        return true;
    }

    /// <summary>paint 배열 원소 바로 앞의 // 주석을 원소 번호별로 모은다.</summary>
    static Dictionary<int, List<string>> ScanPaintComments(string text)
    {
        var result = new Dictionary<int, List<string>>();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
        bool inPaint = false;
        int idx = 0;
        var pending = new List<string>();
        string? lastProp = null;
        while (reader.Read())
        {
            var tt = reader.TokenType;
            if (tt == JsonTokenType.PropertyName) { lastProp = reader.GetString(); continue; }
            if (!inPaint)
            {
                if (tt == JsonTokenType.StartArray && reader.CurrentDepth == 1 && string.Equals(lastProp, "paint", StringComparison.OrdinalIgnoreCase))
                { inPaint = true; idx = 0; pending.Clear(); }
                continue;
            }
            if (tt == JsonTokenType.Comment && reader.CurrentDepth == 2) pending.Add(CleanComment(reader.GetComment()));
            else if (tt == JsonTokenType.EndArray && reader.CurrentDepth == 1) inPaint = false;
            else if (tt == JsonTokenType.StartObject && reader.CurrentDepth == 2)
            {
                if (pending.Count > 0) { result[idx] = pending.ToList(); pending.Clear(); }
                idx++;
                reader.Skip();
            }
        }
        return result;
    }

    static int CountComments(string text)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
        int n = 0;
        while (reader.Read()) if (reader.TokenType == JsonTokenType.Comment) n++;
        return n;
    }

    static string CleanComment(string c) => c.Replace("\r", "").Replace("\n", " ").Trim();

    // ---------------------------------------------------------------- 래스터 (MapFile.Build 와 같은 규칙; 시험이 게임 격자와 대조)
    public static int TerrainIndex(string name)
    {
        int i = Array.IndexOf(TerrainNames, name.ToLowerInvariant());
        if (i < 0) throw new InvalidDataException($"알 수 없는 지형 '{name}'");
        return i;
    }

    static bool KnownTerrain(string name) { string t = name.ToLowerInvariant(); return t is "high" or "low" || Array.IndexOf(TerrainNames, t) >= 0; }

    /// <summary>현재 문서를 칸 격자로 만든다 (캐시: paint 가 바뀔 때만 다시 계산).</summary>
    public MapGrid Raster()
    {
        if (cache != null && cacheVersion == paintVersion) return cache;
        if (Width < 1 || Height < 1 || Width > MaxDim || Height > MaxDim)
            throw new InvalidDataException($"지도 크기 {Width}x{Height} (1..{MaxDim})");
        var g = new MapGrid(Width, Height);
        Array.Fill(g.Terrain, (byte)Math.Max(0, Array.IndexOf(TerrainNames, Base.ToLowerInvariant())));   // 모르는 base 는 Validate 오류, 래스터는 grass
        foreach (var p in paint) Apply(g, p);
        cache = g; cacheVersion = paintVersion;
        return g;
    }

    static void Apply(MapGrid g, PaintOp p)
    {
        if (!KnownTerrain(p.Terrain)) return;   // 이 단계가 모르는 지형 (rail 등): 래스터에서만 건너뜀, 파일에는 그대로 (Validate 경고)
        string tn = p.Terrain.ToLowerInvariant();
        bool heightOnly = tn is "high" or "low";
        bool? hi = heightOnly ? tn == "high" : p.High;
        byte t = heightOnly ? (byte)0 : (byte)TerrainIndex(p.Terrain);
        void Put(int x, int y)
        {
            if (!heightOnly) g.Terrain[y * g.Width + x] = t;
            if (hi is bool h) g.High[y * g.Width + x] = h;
        }
        if (p.Rect is { Length: 4 } r)
        {
            long y1 = Math.Min(g.Height, (long)r[1] + r[3]), x1 = Math.Min(g.Width, (long)r[0] + r[2]);
            for (long y = Math.Max(0, r[1]); y < y1; y++)
            for (long x = Math.Max(0, r[0]); x < x1; x++) Put((int)x, (int)y);
        }
        if (p.Circle is { Length: 3 } c && c[2] >= 0)
        {
            long rr = c[2], r2 = rr * rr;
            long ya = Math.Max(0, c[1] - rr), yb = Math.Min(g.Height - 1, c[1] + rr);
            long xa = Math.Max(0, c[0] - rr), xb = Math.Min(g.Width - 1, c[0] + rr);
            for (long y = ya; y <= yb; y++)
            for (long x = xa; x <= xb; x++)
                if ((x - c[0]) * (x - c[0]) + (y - c[1]) * (y - c[1]) <= r2) Put((int)x, (int)y);
        }
        if (p.Tiles != null)
            foreach (var tp in p.Tiles)
                if (tp is { Length: 2 } && g.InBounds(tp[0], tp[1])) Put(tp[0], tp[1]);
        if (p.Line is { Length: >= 4 } ln)   // 게임 MapFile 과 같은 순서 (rect, circle, tiles, line) · 같은 브레즌햄 · 같은 폭 확장
        {
            int wd = Math.Clamp(p.Width ?? 1, 1, 6), lo = -((wd - 1) / 2), hiOff = lo + wd - 1;
            for (int k = 0; k + 3 < ln.Length; k += 2)
                foreach (var (lx, ly) in LineCells(ln[k], ln[k + 1], ln[k + 2], ln[k + 3]))
                    for (int oy = lo; oy <= hiOff; oy++)
                    for (int ox = lo; ox <= hiOff; ox++)
                        if (g.InBounds(lx + ox, ly + oy)) Put(lx + ox, ly + oy);
        }
    }

    // ---------------------------------------------------------------- 검사
    /// <summary>기본 검사 (게임의 정식 검증은 MapFile.LoadText). 오류 문장 목록, 비면 통과. warnings 를 주면 경고 (모르는 지형 · 키) 를 거기 담는다.</summary>
    public List<string> Validate(List<string>? warnings = null)
    {
        var errs = new List<string>(loadIssues);
        if (Width < 1 || Height < 1 || Width > MaxDim || Height > MaxDim) errs.Add($"지도 크기 {Width}x{Height} (1..{MaxDim})");
        if (Array.IndexOf(TerrainNames, Base.ToLowerInvariant()) < 0) errs.Add($"base '{Base}'");
        int idx = 0;
        foreach (var p in paint)
        {
            if (!KnownTerrain(p.Terrain))
                warnings?.Add($"paint[{idx}] 지형 '{p.Terrain}' 은 이 편집기가 모름: 래스터에서 건너뜀, 파일에는 그대로 보존");
            else if (p.Extra.Count > 0)
                warnings?.Add($"paint[{idx}] 모르는 키 {string.Join(", ", p.Extra.Select(x => x.Name))}: 래스터에 반영 안 됨, 파일에는 그대로 보존");
            if (p.Rect != null && p.Rect.Length != 4) errs.Add("rect 는 [x,y,w,h]");
            if (p.Circle != null)
            {
                if (p.Circle.Length != 3) errs.Add("circle 은 [cx,cy,r]");
                else if (p.Circle[2] < 0 || p.Circle[2] > MaxDim) errs.Add($"circle 반지름 {p.Circle[2]} (0..{MaxDim})");
            }
            if (p.Tiles != null && p.Tiles.Any(t => t.Length != 2)) errs.Add("tiles 원소는 [x,y]");
            if (p.Width is int pw && (pw < 1 || pw > 6)) errs.Add($"paint width {pw} 는 1~6 (칸)");
            if (p.Line != null && (p.Line.Length < 4 || p.Line.Length % 2 != 0)) errs.Add("line 은 [x0,y0,x1,y1,...] (점 2개 이상, 짝수 개)");
            idx++;
        }
        if (starts.Count > MaxStarts) errs.Add($"시작 위치는 최대 {MaxStarts}개 (현재 {starts.Count})");
        foreach (var s in starts) if (s.Length != 2 || !InMap(s[0], s[1])) errs.Add("시작 위치가 지도 밖");
        foreach (var s in sites)
        {
            if (!InMap(s.X, s.Y)) errs.Add($"시설 {s.Type} 이 지도 밖");
            if (s.Owner < 0 || s.Owner > starts.Count) errs.Add($"시설 {s.Type} 소유 세력 {s.Owner} 이 범위 밖 (0~{starts.Count})");
        }
        foreach (var u in units)
        {
            if (!(u.X >= 0 && u.Y >= 0 && u.X < Width && u.Y < Height)) errs.Add($"유닛 {u.Unit} 이 지도 밖");
            if (u.Faction < 1 || u.Faction > starts.Count) errs.Add($"유닛 {u.Unit} 세력 {u.Faction} 이 범위 밖 (1~{starts.Count})");
        }
        return errs;
    }

    bool InMap(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    // ---------------------------------------------------------------- 직렬화 (정규화)
    static readonly JsonSerializerOptions StrOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static string Q(string s) => JsonSerializer.Serialize(s, StrOpts);
    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>정규화 JSON. indent = 바깥 들여쓰기 칸 수 (미션 mapData 안에 넣을 때). 끝 줄바꿈 없음.</summary>
    public string ToJson(int indent = 0)
    {
        string i0 = new(' ', indent), i1 = new(' ', indent + 2), i2 = new(' ', indent + 4);
        var props = new List<string>
        {
            $"{i1}\"name\": {Q(Name)}",
            $"{i1}\"width\": {Width}, \"height\": {Height}, \"base\": {Q(Base)}",
        };
        var s = new StringBuilder($"{i1}\"paint\": [");
        if (paint.Count > 0)
        {
            s.Append('\n');
            for (int k = 0; k < paint.Count; k++)
            {
                foreach (var c in paint[k].Comments) s.Append(i2).Append("// ").Append(c).Append('\n');
                s.Append(i2).Append(PaintJson(paint[k])).Append(k + 1 < paint.Count ? "," : "").Append('\n');
            }
            s.Append(i1);
        }
        s.Append(']');
        props.Add(s.ToString());
        props.Add($"{i1}\"starts\": [{string.Join(", ", starts.Select(a => $"[{a[0]}, {a[1]}]"))}]");
        if (sites.Count > 0)
            props.Add($"{i1}\"sites\": [\n" + string.Join(",\n", sites.Select(x => $"{i2}{{ \"at\": [{x.X}, {x.Y}], \"type\": {Q(x.Type)}, \"owner\": {x.Owner}{ExtraJson(x.Extra)} }}")) + $"\n{i1}]");
        if (units.Count > 0)
            props.Add($"{i1}\"units\": [\n" + string.Join(",\n", units.Select(u => $"{i2}{{ \"unit\": {Q(u.Unit)}, \"at\": [{N(u.X)}, {N(u.Y)}], \"faction\": {u.Faction}, \"count\": {u.Count}{ExtraJson(u.Extra)} }}")) + $"\n{i1}]");
        foreach (var e in extras) props.Add($"{i1}{Q(e.Name)}: {e.RawJson}");
        return "{\n" + string.Join(",\n", props) + "\n" + i0 + "}";
    }

    static string ExtraJson(IReadOnlyList<ExtraField>? ex) =>
        ex == null ? "" : string.Concat(ex.Select(x => $", {Q(x.Name)}: {x.RawJson}"));

    static string PaintJson(PaintOp p)
    {
        var parts = new List<string> { $"\"terrain\": {Q(p.Terrain)}" };
        if (p.Rect != null) parts.Add($"\"rect\": [{string.Join(", ", p.Rect)}]");
        if (p.Circle != null) parts.Add($"\"circle\": [{string.Join(", ", p.Circle)}]");
        if (p.Tiles != null) parts.Add($"\"tiles\": [{string.Join(", ", p.Tiles.Select(t => $"[{string.Join(", ", t)}]"))}]");
        if (p.Line != null) parts.Add($"\"line\": [{string.Join(", ", p.Line)}]");
        if (p.Width is int pw) parts.Add($"\"width\": {pw}");
        if (p.High is bool h) parts.Add($"\"high\": {(h ? "true" : "false")}");
        foreach (var x in p.Extra) parts.Add($"{Q(x.Name)}: {x.RawJson}");
        return "{ " + string.Join(", ", parts) + " }";
    }

    /// <summary>파일용 텍스트 (끝에 줄바꿈 하나).</summary>
    public string ToFileText() => ToJson() + "\n";

    /// <summary>정규화 텍스트의 SHA256 앞 16자 (골든 비교용).</summary>
    public string CanonicalHash() => Hash(ToJson());

    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    // ---------------------------------------------------------------- 편집 (모두 가역)
    public sealed class EditCommand
    {
        public readonly string Name;
        internal readonly Action Do, Undo;
        internal EditCommand(string name, Action d, Action u) { Name = name; Do = d; Undo = u; }
    }

    void Push(string name, Action d, Action u)
    {
        d();
        var c = new EditCommand(name, d, u);
        if (stroke != null) { stroke.Add(c); return; }
        undo.Add(c);
        redo.Clear();
    }

    /// <summary>여러 변경을 한 번의 undo 로 묶는다 (마우스 드래그 한 획).</summary>
    public void BeginStroke(string name)
    {
        if (stroke != null) throw new InvalidOperationException("획이 이미 열려 있음");
        stroke = new List<EditCommand>(); strokeName = name;
    }

    public void EndStroke()
    {
        if (stroke == null) throw new InvalidOperationException("열린 획 없음");
        var list = stroke; stroke = null;
        if (list.Count == 0) return;
        undo.Add(new EditCommand(strokeName, () => { foreach (var x in list) x.Do(); }, () => { for (int i = list.Count - 1; i >= 0; i--) list[i].Undo(); }));
        redo.Clear();
    }

    public bool Undo()
    {
        if (stroke != null || undo.Count == 0) return false;
        var c = undo[^1]; undo.RemoveAt(undo.Count - 1);
        c.Undo(); redo.Add(c);
        return true;
    }

    public bool Redo()
    {
        if (stroke != null || redo.Count == 0) return false;
        var c = redo[^1]; redo.RemoveAt(redo.Count - 1);
        c.Do(); undo.Add(c);
        return true;
    }

    void Touch() => paintVersion++;

    /// <summary>문서 밖 층 (미션 소품 · 구역 등) 이 같은 되돌리기 스택을 쓰도록 하는 가역 명령 하나. 획 안이면 획에 묶인다.</summary>
    public void Exec(string name, Action doIt, Action undoIt) => Push(name, doIt, undoIt);

    public bool InStroke => stroke != null;

    /// <summary>paint 줄을 끝에 더한다 (뒤가 앞을 덮음).</summary>
    public void AddPaint(PaintOp op, string name = "칠하기")
    {
        var o = op.Clone();
        Push(name, () => { paint.Add(o); Touch(); }, () => { paint.RemoveAt(paint.Count - 1); Touch(); });
    }

    public void PaintRect(string terrain, int x, int y, int w, int h, bool? high = null) =>
        AddPaint(new PaintOp { Terrain = terrain, Rect = new[] { x, y, w, h }, High = high }, "사각");

    public void PaintCircle(string terrain, int cx, int cy, int r, bool? high = null) =>
        AddPaint(new PaintOp { Terrain = terrain, Circle = new[] { cx, cy, r }, High = high }, "원");

    /// <summary>꺾은선 칠하기 (선로 · 길): 점 2개 이상, 인접한 같은 점은 합친다. 파일에는 게임 paint "line" 그대로 저장 (한 줄 = undo 한 번).</summary>
    public void PaintPolyline(string terrain, IReadOnlyList<(int X, int Y)> pts, bool? high = null)
    {
        var l = new List<int>();
        (int X, int Y)? prev = null;
        foreach (var q in pts) { if (prev == q) continue; l.Add(q.X); l.Add(q.Y); prev = q; }
        if (l.Count < 4) return;   // 점 하나는 선이 아님
        if (pts.Any(q => !InMap(q.X, q.Y))) throw new ArgumentException("꺾은선 점이 지도 밖");
        AddPaint(new PaintOp { Terrain = terrain, Line = l.ToArray(), High = high }, "꺾은선");
    }

    /// <summary>낱칸 칠하기 (자유 획). 지도 밖 칸은 버리고 중복 칸은 하나로 (입력 순서 유지).</summary>
    public void PaintTiles(string terrain, IEnumerable<(int X, int Y)> cells, bool? high = null)
    {
        var seen = new HashSet<(int, int)>();
        var list = new List<int[]>();
        foreach (var c in cells) if (InMap(c.X, c.Y) && seen.Add(c)) list.Add(new[] { c.X, c.Y });
        if (list.Count == 0) return;
        AddPaint(new PaintOp { Terrain = terrain, Tiles = list, High = high }, "낱칸");
    }

    /// <summary>두 점 사이 선 (Bresenham, 굵기 = size x size 사각 붓). tiles 로 저장.</summary>
    public void PaintLine(string terrain, int x0, int y0, int x1, int y1, int size = 1, bool? high = null) =>
        PaintTiles(terrain, LineCells(x0, y0, x1, y1, size), high);

    public static IEnumerable<(int X, int Y)> LineCells(int x0, int y0, int x1, int y1, int size = 1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        int lo = -((Math.Max(1, size) - 1) / 2), hi = lo + Math.Max(1, size);
        while (true)
        {
            for (int by = lo; by < hi; by++) for (int bx = lo; bx < hi; bx++) yield return (x0 + bx, y0 + by);
            if (x0 == x1 && y0 == y1) yield break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>(x,y) 에서 이어진 같은 지형 (높이만 칠하면 같은 고지 상태) 칸을 채운다 (4방향). 1행 rect 들로 저장, 한 번의 undo. 바꿀 칸이 없으면 명령 없음. 바뀐 칸 수 반환.</summary>
    public int Fill(string terrain, int x, int y)
    {
        var g = Raster();
        if (!g.InBounds(x, y)) return 0;
        string tn = terrain.ToLowerInvariant();
        bool heightOnly = tn is "high" or "low";
        bool want = tn == "high";
        byte newT = heightOnly ? (byte)0 : (byte)TerrainIndex(terrain);
        int seedIx = y * g.Width + x;
        if (heightOnly ? g.High[seedIx] == want : g.Terrain[seedIx] == newT) return 0;
        byte seedT = g.Terrain[seedIx];
        bool seedH = g.High[seedIx];
        bool Same(int ix) => heightOnly ? g.High[ix] == seedH : g.Terrain[ix] == seedT;
        var mark = new bool[g.Width * g.Height];
        var q = new Queue<int>();
        q.Enqueue(seedIx); mark[seedIx] = true;
        while (q.Count > 0)
        {
            int c = q.Dequeue(), cx = c % g.Width, cy = c / g.Width;
            // 이웃 4칸 (배열 할당 없이: 1500x1500 에서 칸마다 배열을 만들면 느림)
            if (cx + 1 < g.Width && !mark[c + 1] && Same(c + 1)) { mark[c + 1] = true; q.Enqueue(c + 1); }
            if (cx > 0 && !mark[c - 1] && Same(c - 1)) { mark[c - 1] = true; q.Enqueue(c - 1); }
            if (cy + 1 < g.Height && !mark[c + g.Width] && Same(c + g.Width)) { mark[c + g.Width] = true; q.Enqueue(c + g.Width); }
            if (cy > 0 && !mark[c - g.Width] && Same(c - g.Width)) { mark[c - g.Width] = true; q.Enqueue(c - g.Width); }
        }
        int count = 0;
        BeginStroke("채우기");
        try
        {
            for (int ry = 0; ry < g.Height; ry++)
                for (int rx = 0; rx < g.Width;)
                {
                    if (!mark[ry * g.Width + rx]) { rx++; continue; }
                    int s = rx;
                    while (rx < g.Width && mark[ry * g.Width + rx]) rx++;
                    // Raster() 캐시는 AddPaint 마다 무효화되지만 위 마크는 이미 계산됨
                    AddPaint(new PaintOp { Terrain = terrain, Rect = new[] { s, ry, rx - s, 1 } }, "채우기 행");
                    count += rx - s;
                }
        }
        finally { EndStroke(); }   // 중간에 예외가 나도 획이 열린 채 남지 않는다
        return count;
    }

    public void SetBase(string terrain)
    {
        TerrainIndex(terrain);
        string old = Base;
        Push("기본 지형", () => { Base = terrain; Touch(); }, () => { Base = old; Touch(); });
    }

    public void SetName(string name)
    {
        string old = Name;
        Push("이름", () => Name = name, () => Name = old);
    }

    public void AddStart(int x, int y) { var s = new[] { x, y }; Push("시작 추가", () => starts.Add(s), () => starts.RemoveAt(starts.Count - 1)); }

    public void MoveStart(int index, int x, int y)
    {
        var old = starts[index]; var nw = new[] { x, y };
        Push("시작 이동", () => starts[index] = nw, () => starts[index] = old);
    }

    public void RemoveStart(int index)
    {
        var old = starts[index];
        Push("시작 삭제", () => starts.RemoveAt(index), () => starts.Insert(index, old));
    }

    public void AddSite(SiteDef s) => Push("시설 추가", () => sites.Add(s), () => sites.RemoveAt(sites.Count - 1));

    public void ReplaceSite(int index, SiteDef s)
    {
        var old = sites[index];
        Push("시설 변경", () => sites[index] = s, () => sites[index] = old);
    }

    public void RemoveSite(int index)
    {
        var old = sites[index];
        Push("시설 삭제", () => sites.RemoveAt(index), () => sites.Insert(index, old));
    }

    public void AddUnit(UnitDef u) => Push("유닛 추가", () => units.Add(u), () => units.RemoveAt(units.Count - 1));

    public void RemoveUnit(int index)
    {
        var old = units[index];
        Push("유닛 삭제", () => units.RemoveAt(index), () => units.Insert(index, old));
    }
}
