using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MapEditor.Model;

/// <summary>손으로 놓은 소품 (미션 props[] 한 줄). Extra = 모르는 키의 원문 (그대로 다시 씀).</summary>
public sealed record PropPlace(string Type, double X, double Y, IReadOnlyList<ExtraField>? Extra = null);

/// <summary>이름 구역 (미션 areas{} 한 항목). 사각 [x,y,w,h] 또는 원 [cx,cy,r] 중 하나.</summary>
public sealed record AreaDef(string Name, double[]? Rect, double[]? Circle, IReadOnlyList<ExtraField>? Extra = null)
{
    public bool IsValid => Rect is { Length: 4 } r && r[2] > 0 && r[3] > 0 || Circle is { Length: 3 } c && c[2] > 0;
    public (double X, double Y) Center => Rect is { Length: 4 } r ? (r[0] + r[2] / 2, r[1] + r[3] / 2) : Circle is { Length: 3 } c ? (c[0], c[1]) : (0, 0);
}

/// <summary>배치 구역 (미션 deployZones[] 한 줄).</summary>
public sealed record ZoneDef(int Faction, double[]? Rect, double[]? Circle, IReadOnlyList<ExtraField>? Extra = null)
{
    public bool IsValid => Rect is { Length: 4 } r && r[2] > 0 && r[3] > 0 || Circle is { Length: 3 } c && c[2] > 0;
    public (double X, double Y) Center => Rect is { Length: 4 } r ? (r[0] + r[2] / 2, r[1] + r[3] / 2) : Circle is { Length: 3 } c ? (c[0], c[1]) : (0, 0);
}

/// <summary>미션 JSON 안의 구역 이름 참조 한 건 (읽기 전용 표시 · 검사용).</summary>
public sealed record AreaRef(string Where, string Name, bool IsObjective);

/// <summary>읽기 전용: 미션 병력의 at 위치 (편성은 이 편집기 범위 밖 -- 위치만 참고용으로 그림).</summary>
public sealed record ForcePoint(string Id, int Faction, double X, double Y);

/// <summary>
/// 미션 한 개의 편집 문서. 지도 (mapData 또는 map 참조) = MapDoc, 그 위에 props / propScatter / propDensity / propCell / areas / deployZones 만 편집한다.
/// 저장은 원문 텍스트에서 바뀐 최상위 속성의 값만 치환 (MissionMapData) -- 나머지 바이트 (주석 · 키 순서 · 들여쓰기) 는 그대로.
/// 모든 편집은 지도 문서의 되돌리기 스택에 들어간다 (한 스택).
/// </summary>
public sealed class MissionDoc
{
    public MapDoc Map { get; }
    public string Id { get; private set; } = "";
    /// <summary>현재 원문 (저장 때 갱신).</summary>
    public string Text { get; private set; }
    /// <summary>지도를 `map` 참조로 가지고 있었다면 그 id (저장 때 mapData 로 내장).</summary>
    public string? MapRef { get; private set; }
    public bool NeedsEmbed => MapRef != null;
    public int Seed { get; private set; } = 1;
    public int FactionCount { get; private set; }

    public IReadOnlyList<PropPlace> Props => props;
    public bool PropScatter { get; private set; }
    public double PropDensity { get; private set; } = 1;
    public int PropCell { get; private set; }
    public IReadOnlyList<AreaDef> Areas => areas;
    public IReadOnlyList<ZoneDef> Zones => zones;
    public IReadOnlyList<AreaRef> Refs => refs;
    public IReadOnlyList<ForcePoint> ForcePoints => forcePoints;
    /// <summary>읽을 때 발견한 문제 (이름 중복 등). Validate 가 오류로 낸다.</summary>
    public IReadOnlyList<string> LoadIssues => loadIssues;

    readonly List<PropPlace> props = new();
    readonly List<AreaDef> areas = new();
    readonly List<ZoneDef> zones = new();
    readonly List<AreaRef> refs = new();
    readonly List<ForcePoint> forcePoints = new();
    readonly List<string> loadIssues = new();

    // 읽은 그대로의 정규화 문자열 (바뀌지 않은 속성은 원문 바이트를 건드리지 않기 위한 기준)
    string origProps = "", origAreas = "", origZones = "", origMap = "";
    bool origScatter; double origDensity; int origCell;

    static readonly JsonDocumentOptions DocOpts = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly JsonSerializerOptions StrOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static string Q(string s) => JsonSerializer.Serialize(s, StrOpts);
    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    MissionDoc(MapDoc map, string text) { Map = map; Text = text; }

    /// <summary>미션 텍스트 -> 문서. mapText(id) = `map` 참조의 지도 JSON (없으면 null). 어떤 쓰레기 입력도 InvalidDataException 하나로만 실패.</summary>
    public static MissionDoc Parse(string text, Func<string, string?> mapText, string? fallbackId = null)
    {
        try { return ParseCore(text, mapText, fallbackId); }
        catch (InvalidDataException) { throw; }
        catch (Exception e) { throw new InvalidDataException("미션 JSON 필드 형식 오류: " + e.Message, e); }
    }

    /// <summary>텍스트가 미션 파일인가 (지도 파일은 width / paint 가 최상위에 있음).</summary>
    public static bool LooksLikeMission(string text)
    {
        try
        {
            using var jd = JsonDocument.Parse(text, DocOpts);
            if (jd.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var p in jd.RootElement.EnumerateObject())
            {
                string n = p.Name.ToLowerInvariant();
                if (n is "mapdata" or "objectives" or "forces") return true;
                if (n is "paint" or "width") return false;
            }
            return false;
        }
        catch (JsonException) { return false; }
    }

    static MissionDoc ParseCore(string text, Func<string, string?> mapText, string? fallbackId)
    {
        JsonDocument jd;
        try { jd = JsonDocument.Parse(text, DocOpts); }
        catch (JsonException e) { throw new InvalidDataException("미션 JSON 오류: " + e.Message, e); }
        using (jd)
        {
            var root = jd.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("미션 JSON 최상위가 객체가 아님");
            string? mapDataText = MissionMapData.Extract(text);
            string? mapRef = null;
            JsonElement propsEl = default, areasEl = default, zonesEl = default;
            bool scatter = false; double density = 1; int cell = 0, seed = 1, factions = 0;
            string id = fallbackId ?? "mission";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dupes = new List<string>();

            foreach (var p in root.EnumerateObject())
            {
                if (!seen.Add(p.Name)) dupes.Add(p.Name);
                switch (p.Name.ToLowerInvariant())
                {
                    case "id": if (p.Value.ValueKind == JsonValueKind.String) id = p.Value.GetString() ?? id; break;
                    case "map": if (p.Value.ValueKind == JsonValueKind.String) mapRef = p.Value.GetString(); break;
                    case "seed": if (p.Value.ValueKind == JsonValueKind.Number) seed = p.Value.GetInt32(); break;
                    case "props": propsEl = p.Value; break;
                    case "areas": areasEl = p.Value; break;
                    case "deployzones": zonesEl = p.Value; break;
                    case "propscatter": scatter = p.Value.ValueKind == JsonValueKind.True; break;
                    case "propdensity": density = p.Value.GetDouble(); break;
                    case "propcell": cell = p.Value.GetInt32(); break;
                    case "factions": if (p.Value.ValueKind == JsonValueKind.Array) factions = p.Value.GetArrayLength(); break;
                }
            }
            MapDoc map;
            if (mapDataText != null) { map = MapDoc.Parse(mapDataText); mapRef = null; }
            else if (mapRef != null)
            {
                string mt = mapText(mapRef) ?? throw new InvalidDataException($"참조 지도 '{mapRef}' 를 찾을 수 없음");
                map = MapDoc.Parse(mt);
            }
            else throw new InvalidDataException("미션에 지도가 없음 (map 또는 mapData)");

            var d = new MissionDoc(map, text) { Id = id, MapRef = mapRef, Seed = seed, FactionCount = factions };
            foreach (var n in dupes) d.loadIssues.Add($"최상위 키 '{n}' 중복 (대소문자 무시)");
            d.PropScatter = scatter; d.PropDensity = density; d.PropCell = cell;

            if (propsEl.ValueKind == JsonValueKind.Array)
                foreach (var e in propsEl.EnumerateArray())
                {
                    var ex = ReadExtras(e, "type", "x", "y");
                    d.props.Add(new PropPlace(Find(e, "type").GetString() ?? "", Find(e, "x").GetDouble(), Find(e, "y").GetDouble(), ex.Count > 0 ? ex : null));
                }
            else if (propsEl.ValueKind != JsonValueKind.Undefined) throw new InvalidDataException("props 는 배열이어야 함");

            if (areasEl.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var a in areasEl.EnumerateObject())
                {
                    if (!names.Add(a.Name)) d.loadIssues.Add($"구역 이름 '{a.Name}' 중복");
                    var ex = ReadExtras(a.Value, "rect", "circle");
                    d.areas.Add(new AreaDef(a.Name, Nums(Find(a.Value, "rect")), Nums(Find(a.Value, "circle")), ex.Count > 0 ? ex : null));
                }
            }
            else if (areasEl.ValueKind != JsonValueKind.Undefined) throw new InvalidDataException("areas 는 객체여야 함");

            if (zonesEl.ValueKind == JsonValueKind.Array)
                foreach (var z in zonesEl.EnumerateArray())
                {
                    var ex = ReadExtras(z, "faction", "rect", "circle");
                    d.zones.Add(new ZoneDef(Find(z, "faction") is { ValueKind: JsonValueKind.Number } f ? f.GetInt32() : 1, Nums(Find(z, "rect")), Nums(Find(z, "circle")), ex.Count > 0 ? ex : null));
                }
            else if (zonesEl.ValueKind != JsonValueKind.Undefined) throw new InvalidDataException("deployZones 는 배열이어야 함");

            foreach (var key in new[] { "forces", "reinforcements" })
                if (Find(root, key) is { ValueKind: JsonValueKind.Array } fa)
                    foreach (var f in fa.EnumerateArray())
                        if (f.ValueKind == JsonValueKind.Object && Nums(Find(f, "at")) is { Length: 2 } at)
                            d.forcePoints.Add(new ForcePoint(Find(f, "id").ValueKind == JsonValueKind.String ? Find(f, "id").GetString()! : "", Find(f, "faction") is { ValueKind: JsonValueKind.Number } ff ? ff.GetInt32() : 1, at[0], at[1]));

            foreach (var p in root.EnumerateObject())
            {
                string ln = p.Name.ToLowerInvariant();
                if (ln is "mapdata" or "areas" or "deployzones") continue;
                CollectRefs(p.Value, p.Name, d.refs);
            }

            d.origProps = d.PropsJson(0); d.origAreas = d.AreasJson(0); d.origZones = d.ZonesJson(0);
            d.origScatter = d.PropScatter; d.origDensity = d.PropDensity; d.origCell = d.PropCell;
            d.origMap = map.ToJson();
            return d;
        }
    }

    static JsonElement Find(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return default;
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return default;
    }

    static double[]? Nums(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(v => v.GetDouble()).ToArray() : null;

    static List<ExtraField> ReadExtras(JsonElement obj, params string[] known)
    {
        var list = new List<ExtraField>();
        if (obj.ValueKind != JsonValueKind.Object) return list;
        foreach (var p in obj.EnumerateObject())
            if (Array.FindIndex(known, k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase)) < 0)
                list.Add(new ExtraField(p.Name, p.Value.GetRawText()));
        return list;
    }

    /// <summary>area / aiArea 문자열 값을 모은다 (목표의 area 는 IsObjective).</summary>
    static void CollectRefs(JsonElement e, string path, List<AreaRef> into)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    string n = p.Name.ToLowerInvariant();
                    if ((n is "area" or "aiarea") && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } s)
                        into.Add(new AreaRef($"{path}.{p.Name}", s, path.StartsWith("objectives", StringComparison.OrdinalIgnoreCase)));
                    else CollectRefs(p.Value, $"{path}.{p.Name}", into);
                }
                break;
            case JsonValueKind.Array:
                int i = 0;
                foreach (var x in e.EnumerateArray()) CollectRefs(x, $"{path}[{i++}]", into);
                break;
        }
    }

    // ---------------------------------------------------------------- 검사 (구조)
    public static readonly System.Text.RegularExpressions.Regex NameRule = new("^[a-z0-9_]{1,24}$");

    /// <summary>props.json 종류 검사용: propKnown(type) 이 false 면 오류. 오류 문장 목록, 비면 통과.</summary>
    public List<string> Validate(Func<string, bool> propKnown, List<string>? warnings = null)
    {
        var errs = new List<string>(loadIssues);
        errs.AddRange(Map.Validate(warnings));
        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i];
            if (!propKnown(p.Type)) errs.Add($"props[{i}] 종류 '{p.Type}' 이 props.json 에 없음");
            if (!(p.X >= 0 && p.Y >= 0 && p.X < Map.Width && p.Y < Map.Height) || double.IsNaN(p.X + p.Y)) errs.Add($"props[{i}] ({N(p.X)},{N(p.Y)}) 가 지도 밖");
        }
        if (PropDensity < 0 || PropDensity > 100 || double.IsNaN(PropDensity)) errs.Add($"propDensity {N(PropDensity)} (0~100)");
        if (PropCell < 0 || PropCell > 256) errs.Add($"propCell {PropCell} (0~256)");
        foreach (var a in areas)
        {
            if (!a.IsValid) errs.Add($"구역 '{a.Name}' 은 rect [x,y,w,h] (w,h>0) 또는 circle [cx,cy,r] (r>0)");
            if (a.Rect != null && a.Circle != null) warnings?.Add($"구역 '{a.Name}' 에 rect 와 circle 이 둘 다 있음");
        }
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (!z.IsValid) errs.Add($"deployZones[{i}] 은 rect 또는 circle");
            if (FactionCount > 0 && (z.Faction < 1 || z.Faction > FactionCount)) errs.Add($"deployZones[{i}] 세력 {z.Faction} 이 범위 밖 (1~{FactionCount})");
        }
        foreach (var r in refs)
            if (!areas.Any(a => a.Name == r.Name)) errs.Add($"{r.Where} 이 없는 구역 '{r.Name}' 을 가리킴");
        return errs;
    }

    // ---------------------------------------------------------------- 편집 (모두 가역: 지도 문서의 스택)
    public void AddProp(PropPlace p) => Map.Exec("소품 추가", () => props.Add(p), () => props.RemoveAt(props.Count - 1));

    public void RemoveProp(int index)
    {
        var old = props[index];
        Map.Exec("소품 삭제", () => props.RemoveAt(index), () => props.Insert(index, old));
    }

    public void SetScatter(bool on, double density, int cell)
    {
        bool o1 = PropScatter; double o2 = PropDensity; int o3 = PropCell;
        Map.Exec("흩뿌리기", () => { PropScatter = on; PropDensity = density; PropCell = cell; }, () => { PropScatter = o1; PropDensity = o2; PropCell = o3; });
    }

    /// <summary>같은 이름이 있으면 모양을 바꾸고 (자리 · 모르는 키 유지), 없으면 끝에 더한다. 이름 규칙 위반은 InvalidDataException.</summary>
    public void SetArea(string name, double[]? rect, double[]? circle)
    {
        if (!NameRule.IsMatch(name)) throw new InvalidDataException($"구역 이름 '{name}' 은 영문 소문자 · 숫자 · _ 1~24자");
        int i = areas.FindIndex(a => a.Name == name);
        if (i < 0)
        {
            var na = new AreaDef(name, rect, circle);
            Map.Exec("구역 추가", () => areas.Add(na), () => areas.RemoveAt(areas.Count - 1));
        }
        else
        {
            var old = areas[i]; var na = new AreaDef(name, rect, circle, old.Extra);
            Map.Exec("구역 변경", () => areas[i] = na, () => areas[i] = old);
        }
    }

    public void RemoveArea(string name)
    {
        int i = areas.FindIndex(a => a.Name == name);
        if (i < 0) return;
        var old = areas[i];
        Map.Exec("구역 삭제", () => areas.RemoveAt(i), () => areas.Insert(i, old));
    }

    public void AddZone(ZoneDef z) => Map.Exec("배치 구역 추가", () => zones.Add(z), () => zones.RemoveAt(zones.Count - 1));

    public void ReplaceZone(int index, double[]? rect, double[]? circle)
    {
        var old = zones[index]; var nz = new ZoneDef(old.Faction, rect, circle, old.Extra);
        Map.Exec("배치 구역 변경", () => zones[index] = nz, () => zones[index] = old);
    }

    public void SetZoneFaction(int index, int faction)
    {
        var old = zones[index]; var nz = old with { Faction = faction };
        Map.Exec("배치 구역 세력", () => zones[index] = nz, () => zones[index] = old);
    }

    public void RemoveZone(int index)
    {
        var old = zones[index];
        Map.Exec("배치 구역 삭제", () => zones.RemoveAt(index), () => zones.Insert(index, old));
    }

    // ---------------------------------------------------------------- 직렬화
    string PropsJson(int ind)
    {
        if (props.Count == 0) return "[]";
        string i1 = new(' ', ind), i2 = new(' ', ind + 2);
        return "[\n" + string.Join(",\n", props.Select(p => $"{i2}{{ \"type\": {Q(p.Type)}, \"x\": {N(p.X)}, \"y\": {N(p.Y)}{ExtraJson(p.Extra)} }}")) + "\n" + i1 + "]";
    }

    static string Shape(double[]? rect, double[]? circle, IReadOnlyList<ExtraField>? ex)
    {
        var parts = new List<string>();
        if (rect != null) parts.Add($"\"rect\": [{string.Join(", ", rect.Select(N))}]");
        if (circle != null) parts.Add($"\"circle\": [{string.Join(", ", circle.Select(N))}]");
        if (ex != null) foreach (var x in ex) parts.Add($"{Q(x.Name)}: {x.RawJson}");
        return string.Join(", ", parts);
    }

    static string ExtraJson(IReadOnlyList<ExtraField>? ex) =>
        ex == null ? "" : string.Concat(ex.Select(x => $", {Q(x.Name)}: {x.RawJson}"));

    string AreasJson(int ind)
    {
        if (areas.Count == 0) return "{}";
        string i1 = new(' ', ind), i2 = new(' ', ind + 2);
        return "{\n" + string.Join(",\n", areas.Select(a => $"{i2}{Q(a.Name)}: {{ {Shape(a.Rect, a.Circle, a.Extra)} }}")) + "\n" + i1 + "}";
    }

    string ZonesJson(int ind)
    {
        if (zones.Count == 0) return "[]";
        string i1 = new(' ', ind), i2 = new(' ', ind + 2);
        return "[\n" + string.Join(",\n", zones.Select(z => $"{i2}{{ \"faction\": {z.Faction}, {Shape(z.Rect, z.Circle, z.Extra)} }}")) + "\n" + i1 + "]";
    }

    /// <summary>바뀐 속성만 원문에 끼워 넣은 미션 텍스트. 아무것도 안 바뀌었으면 원문 그대로 (바이트 동일).</summary>
    public string ToText()
    {
        string t = Text;
        bool mapChanged = Map.ToJson() != origMap;
        if (NeedsEmbed) t = MissionMapData.EmbedReference(t, Map);
        else if (mapChanged) t = MissionMapData.Replace(t, Map);
        if (PropsJson(0) != origProps) t = MissionMapData.SetTop(t, "props", PropsJson);
        if (PropScatter != origScatter) t = MissionMapData.SetTop(t, "propScatter", _ => PropScatter ? "true" : "false");
        if (PropDensity != origDensity) t = MissionMapData.SetTop(t, "propDensity", _ => N(PropDensity));
        if (PropCell != origCell) t = MissionMapData.SetTop(t, "propCell", _ => PropCell.ToString(CultureInfo.InvariantCulture));
        if (AreasJson(0) != origAreas) t = MissionMapData.SetTop(t, "areas", AreasJson);
        if (ZonesJson(0) != origZones) t = MissionMapData.SetTop(t, "deployZones", ZonesJson);
        return t;
    }

    /// <summary>저장이 끝났다: 지금 상태를 새 기준으로 삼는다 (원문 = 저장한 텍스트, map 참조는 내장됨).</summary>
    public void MarkSaved(string savedText)
    {
        Text = savedText; MapRef = null;
        origProps = PropsJson(0); origAreas = AreasJson(0); origZones = ZonesJson(0);
        origScatter = PropScatter; origDensity = PropDensity; origCell = PropCell; origMap = Map.ToJson();
    }

    /// <summary>저장 시 바뀐 속성 안에서 사라질 // 주석 수 (경고용; 바뀐 속성 값 안의 주석만 센다).</summary>
    public int CommentsAtRisk()
    {
        int n = 0;
        void Count(string name, bool changed)
        {
            if (!changed) return;
            if (MissionMapData.Find(Encoding.UTF8.GetBytes(Text), name) is { } f)
            {
                string span = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(Text), f.ValueStart, f.ValueEnd - f.ValueStart);
                var r = new Utf8JsonReader(Encoding.UTF8.GetBytes(span), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
                while (r.Read()) if (r.TokenType == JsonTokenType.Comment) n++;
            }
        }
        Count("props", PropsJson(0) != origProps);
        Count("areas", AreasJson(0) != origAreas);
        Count("deployZones", ZonesJson(0) != origZones);
        return n;
    }

    /// <summary>변경 표시용 상태 문자열 (지도 JSON 은 별도 해시).</summary>
    public string StateKey() => string.Join("|", PropsJson(0), AreasJson(0), ZonesJson(0), PropScatter, N(PropDensity), PropCell);

    public bool OverlayChanged => StateKey() != string.Join("|", origProps, origAreas, origZones, origScatter, N(origDensity), origCell);
}
