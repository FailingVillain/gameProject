using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 고정 지도 파일 (assets/maps/&lt;이름&gt;.json). 직접 그린 지도 · 세계지도 등은 이 형식으로 만든다.
/// <code>
/// {
///   "name": "4인 평지", "width": 128, "height": 128, "base": "grass",
///   "paint": [ { "terrain": "water", "rect": [x, y, w, h] }, { "terrain": "rock", "circle": [cx, cy, r] }, { "terrain": "rail", "line": [x0, y0, x1, y1, ...] } ],
///   "starts": [ [16, 112], ... ],                 // 세력 1, 2, ... 의 대도시 중심 타일
///   "factions": [ { "name": "한국", "en": "korean", "lang": "ko", "color": [r, g, b], "team": 1 }, ... ],   // 선택: 세력 N = N번째 정의 (표시 이름 · id(en, --faction 용) · 보이스 언어 · 색). id: korean japanese fengtian russian soviet
///   "sites":  [ { "at": [26, 102], "type": "city", "owner": 0 } ], // 점령 시설 중심 타일, type = 건물 키 (기본 village), owner = 시작 소유 세력 (0 = 중립)
///   "weather": "fog", "time": "night", "timeStep": 20,   // 선택: 날씨 · 시간대 (기본 clear · day), timeStep = 분마다 시간대 한 단계 진행
///   "schedule": [ { "at": 15, "weather": "rain" }, { "at": 30, "time": "dawn" } ],   // 선택: 분 단위 날씨 · 시간대 변화
///   "units":  [ { "unit": "infantry", "at": [30, 100], "faction": 1, "count": 20 } ] // 선택: 주어진 병력 배치 (count 기는 at 주위 격자로). 없으면 balance.json start.force 를 대도시 앞에
/// }
/// </code>
/// 인원 = starts 개수. 칠하기는 순서대로 덮어쓴다. factions[].team: 같은 번호(1 이상) = 고정 동맹 (외교 없음, 실행 중 불변). 없으면 모두가 서로 적.
/// </summary>
public sealed partial class MapFile
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Base { get; set; } = "grass";
    public List<Paint> Paint { get; set; } = new();
    public List<int[]> Starts { get; set; } = new();
    public List<Site> Sites { get; set; } = new();
    public List<UnitPlacement> Units { get; set; } = new();
    /// <summary>세력 정의 (선택). 인덱스 = 시작 위치 인덱스 (0 → 세력 1). 없으면 "세력 N" + 기본 색.</summary>
    public List<FactionDef> Factions { get; set; } = new();
    /// <summary>날씨 (clear · overcast · fog · rain · snow, 생략 = clear) · 시간대 (dawn · day · dusk · night, 생략 = day).</summary>
    public string? Weather { get; set; }
    public string? Time { get; set; }
    /// <summary>0 이 아니면 이 분마다 시간대가 한 단계 진행.</summary>
    public float TimeStep { get; set; }
    /// <summary>날씨 일정: [{ "at": 분, "weather": "rain", "time": "dusk" }, ...] (둘 중 하나만 써도 됨).</summary>
    public List<WeatherScheduleEntry> Schedule { get; set; } = new();

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameMap, WeatherSetup> weatherTable = new();

    /// <summary>이 지도 파일의 날씨 설정 (없으면 null = 맑은 낮). World.CreateStandard 가 적용한다.</summary>
    public static WeatherSetup? WeatherOf(GameMap map) => weatherTable.TryGetValue(map, out var w) ? w : null;

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameMap, List<FactionDef>> factionTable = new();

    /// <summary>이 지도 파일이 정의한 세력 (없으면 빈 목록).</summary>
    public static IReadOnlyList<FactionDef> FactionsOf(GameMap map) => factionTable.TryGetValue(map, out var l) ? l : Array.Empty<FactionDef>();

    /// <summary>월드의 세력 이름을 지도 파일 정의로 바꾼다 (표시용 — 시뮬레이션 · 체크섬과 무관). 이름은 항상 지도에서 오므로 리플레이도 같다.</summary>
    public static void ApplyFactions(World world)
    {
        var defs = FactionsOf(world.Map);
        for (int i = 0; i < defs.Count && i + 1 < world.Factions.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(defs[i].Name)) world.Faction(i + 1).Name = defs[i].Name;
            if (!string.IsNullOrWhiteSpace(defs[i].Lang)) world.Faction(i + 1).Lang = defs[i].Lang.Trim().ToLowerInvariant();
        }
    }

    /// <summary>시작 화면용: 지도를 만들지 않고 인원 · 세력 정의만 읽는다.</summary>
    public static MapFile? Peek(string id)
    {
        try { return Deserialize(File.ReadAllText(PathOf(id))); }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { return null; }
    }

    static MapFile? Deserialize(string text) => JsonSerializer.Deserialize<MapFile>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "assets", "maps");
    public static string PathOf(string id) => Path.Combine(Folder, id + ".json");
    public static IEnumerable<string> Available() =>
        Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(n => n) : Array.Empty<string>();

    /// <summary>지도 파일을 읽어 GameMap 을 만든다. seed 는 지형 무늬 · 결정적 난수용.</summary>
    public static GameMap Load(string id, int seed)
    {
        string path = PathOf(id);
        if (!File.Exists(path)) throw new FileNotFoundException($"지도 파일 없음: {path}");
        return LoadText(File.ReadAllText(path), id, seed, path);
    }

    /// <summary>지도 JSON 텍스트에서 바로 GameMap 을 만든다 (Load 와 같은 경로 — 테스트용으로도 쓴다).</summary>
    internal static GameMap LoadText(string text, string id, int seed, string? path = null)
    {
        path ??= id;
        var mf = Deserialize(text) ?? throw new InvalidDataException($"빈 지도 파일: {path}");
        var map = mf.Build(seed);
        map.Id = id;
        map.DisplayName = mf.Name;
        if (mf.Factions.Count > 0) factionTable.Add(map, mf.Factions);
        if (mf.BuildWeather() is { } ws) weatherTable.Add(map, ws);
        map.FileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];
        return map;
    }

    GameMap Build(int seed)
    {
        Require(Width >= Config.MinMapSize && Width <= Config.MaxMapSize && Height >= Config.MinMapSize && Height <= Config.MaxMapSize, $"지도 크기 {Width}x{Height} 는 {Config.MinMapSize}~{Config.MaxMapSize} 이어야 함");
        Require(Starts.Count >= 2 && Starts.Count <= Config.MaxPlayers, $"시작 위치는 2~{Config.MaxPlayers}개 (현재 {Starts.Count})");
        var map = new GameMap(Width, Height, seed);
        var baseT = Terrain(Base);
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            map.Set(x, y, baseT);
        foreach (var p in Paint)
        {
            // terrain "high" / "low" = 고지 표시만 바꾼다 (지형은 그대로). 다른 지형 + "high": true/false 면 둘 다.
            string tn = p.Terrain.ToLowerInvariant();
            bool heightOnly = tn is "high" or "low";
            bool? hi = heightOnly ? tn == "high" : p.High;
            TerrainType t = heightOnly ? default : Terrain(p.Terrain);
            void Put(int x, int y)
            {
                if (!heightOnly) map.Set(x, y, t);
                if (hi is bool h) map.SetHigh(x, y, h);
            }
            bool organic = !heightOnly && t == TerrainType.Forest && PropDefs.Current.Rules.OrganicForest;   // T28 (기본 꺼짐)
            if (organic && p.Rect is { Length: 4 } or1)
            {
                float amp = Math.Clamp(Math.Min(or1[2], or1[3]) / 5f, 1.5f, 4.5f);
                int pad = (int)MathF.Ceiling(amp) + 2;
                for (int y = or1[1] - pad; y < or1[1] + or1[3] + pad; y++)
                for (int x = or1[0] - pad; x < or1[0] + or1[2] + pad; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    float ox = Math.Max(or1[0] - (x + 0.5f), x + 0.5f - (or1[0] + or1[2])), oy = Math.Max(or1[1] - (y + 0.5f), y + 0.5f - (or1[1] + or1[3]));
                    float sd = ox > 0 && oy > 0 ? MathF.Sqrt(ox * ox + oy * oy) : Math.Max(ox, oy);   // 안쪽 음수 / 바깥 양수
                    if (map.OrganicForestTile(x, y, sd, amp) && (sd <= 0 || map.Get(x, y) == baseT)) Put(x, y);
                }
            }
            else if (p.Rect is { Length: 4 } r)
                for (int y = Math.Max(0, r[1]); y < Math.Min(Height, r[1] + r[3]); y++)
                for (int x = Math.Max(0, r[0]); x < Math.Min(Width, r[0] + r[2]); x++)
                    Put(x, y);
            if (organic && p.Circle is { Length: 3 } oc)
            {
                float amp = Math.Clamp(oc[2] * 0.4f, 1.5f, 4.5f);
                int pad = (int)MathF.Ceiling(amp) + 2;
                for (int y = oc[1] - oc[2] - pad; y <= oc[1] + oc[2] + pad; y++)
                for (int x = oc[0] - oc[2] - pad; x <= oc[0] + oc[2] + pad; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    float dx = x - oc[0], dy = y - oc[1], sd = MathF.Sqrt(dx * dx + dy * dy) - oc[2];
                    if (map.OrganicForestTile(x, y, sd, amp) && (sd <= 0 || map.Get(x, y) == baseT)) Put(x, y);
                }
            }
            else if (p.Circle is { Length: 3 } c)
                for (int y = c[1] - c[2]; y <= c[1] + c[2]; y++)
                for (int x = c[0] - c[2]; x <= c[0] + c[2]; x++)
                    if (map.InBounds(x, y) && (x - c[0]) * (x - c[0]) + (y - c[1]) * (y - c[1]) <= c[2] * c[2]) Put(x, y);
            if (p.Tiles != null)
                foreach (var tp in p.Tiles)
                    if (tp is { Length: 2 } && map.InBounds(tp[0], tp[1])) Put(tp[0], tp[1]);
            Require(p.Width == null || (p.Width >= 1 && p.Width <= 6), $"paint width {p.Width} 는 1~6 (칸)");
            if (p.Line != null) Require(p.Line.Length % 2 == 0, "paint line 은 [x0, y0, x1, y1, ...] 짝수 개");
            if (p.Line is { Length: >= 4 } ln)
            {
                int wd = p.Width ?? 1, lo = -((wd - 1) / 2), hiOff = lo + wd - 1;   // 폭 N: 중심에서 -(N-1)/2 ~ +N/2 정사각 확장
                for (int k = 0; k + 3 < ln.Length; k += 2)   // 꺾은선 [x0, y0, x1, y1, x2, y2, ...] — 8방향 이어진 선 (기본 1칸 폭: 선로 · 길 · 하천)
                    foreach (var (lx, ly) in LineTiles(ln[k], ln[k + 1], ln[k + 2], ln[k + 3]))
                        for (int oy = lo; oy <= hiOff; oy++)
                        for (int ox = lo; ox <= hiOff; ox++)
                            if (map.InBounds(lx + ox, ly + oy)) Put(lx + ox, ly + oy);
            }
        }
        Require(Factions.Count <= Starts.Count, $"세력 정의 {Factions.Count}개가 시작 위치 {Starts.Count}개보다 많음");
        foreach (var f in Factions) Require(f.Color == null || f.Color.Length == 3, "세력 색은 [r, g, b]");
        foreach (var f in Factions) Require(f.Team >= 0, "세력 팀 번호는 0 이상 (0 = 팀 없음)");
        foreach (var s in Starts)
        {
            Require(s.Length == 2 && map.InBounds(s[0], s[1]), "시작 위치는 [x, y] (지도 안)");
            map.StartTiles.Add((s[0], s[1]));
        }
        foreach (var site in Sites)
        {
            Require(site.At is { Length: 2 } && map.InBounds(site.At[0], site.At[1]), "점령 시설 위치는 [x, y] (지도 안)");
            Require(site.Owner >= 0 && site.Owner <= Starts.Count, $"점령 시설 소유 세력 {site.Owner} 이 범위 밖");
            var kind = Defs.Current.SiteKind(site.Type);
            Require(kind != null, $"점령 시설 종류 '{site.Type}' 없음 (capturable 건물만 가능: {string.Join(", ", Defs.Current.SiteKinds.Select(k => k.Key))})");
            map.ResourceSites.Add((site.At[0] - kind!.Size / 2, site.At[1] - kind.Size / 2, site.Owner, kind.Key));
        }
        foreach (var up in Units)
        {
            Require(up.At is { Length: 2 } && map.InBounds((int)up.At[0], (int)up.At[1]), "유닛 배치 위치는 [x, y] (지도 안)");
            Require(up.Faction >= 1 && up.Faction <= Starts.Count, $"유닛 배치 세력 {up.Faction} 이 범위 밖");
            Require(Defs.Current.Units.ContainsKey(up.Unit), $"유닛 배치: 유닛 '{up.Unit}' 없음");
            int n = Math.Max(1, up.Count), cols = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(n)));
            for (int i = 0; i < n; i++)
                map.UnitSpawns.Add(new UnitSpawn(up.Unit, up.Faction, new System.Numerics.Vector2(up.At[0] + (i % cols - (cols - 1) / 2f) * 0.9f, up.At[1] + (i / cols - (cols - 1) / 2f) * 0.9f)));
        }
        return map;
    }

    WeatherSetup? BuildWeather()
    {
        if (Weather == null && Time == null && TimeStep <= 0 && Schedule.Count == 0) return null;
        var ws = new WeatherSetup
        {
            Kind = Weather != null ? WeatherState.ParseKind(Weather) : WeatherKind.Clear,
            Time = Time != null ? WeatherState.ParseTime(Time) : TimeOfDay.Day,
            TimeStepTicks = TimeStep > 0 ? Math.Max(1, (int)MathF.Round(TimeStep * 60f / Config.SimDt)) : 0,
        };
        foreach (var e in Schedule)
        {
            Require(e.At > 0, "날씨 일정 at (분) 은 0 보다 커야 함");
            Require(e.Weather != null || e.Time != null, "날씨 일정 한 줄에 weather 또는 time 이 있어야 함");
            ws.Schedule.Add(new WeatherEvent((long)MathF.Round(e.At * 60f / Config.SimDt),
                e.Weather != null ? (sbyte)WeatherState.ParseKind(e.Weather) : (sbyte)-1,
                e.Time != null ? (sbyte)WeatherState.ParseTime(e.Time) : (sbyte)-1));
        }
        return ws;
    }

    static TerrainType Terrain(string name) => name.ToLowerInvariant() switch
    {
        "grass" => TerrainType.Grass,
        "dirt" => TerrainType.Dirt,
        "sand" => TerrainType.Sand,
        "water" => TerrainType.Water,
        "rock" => TerrainType.Rock,
        "forest" => TerrainType.Forest,
        "rubble" => TerrainType.Rubble,
        "house" => TerrainType.House,
        "wire" => TerrainType.Wire,
        "trench" => TerrainType.Trench,
        "crater" => TerrainType.Crater,
        "rail" => TerrainType.Rail,
        "ford" => TerrainType.Ford,
        "bridge" => TerrainType.Bridge,
        _ => throw new InvalidDataException($"알 수 없는 지형 '{name}' (grass, dirt, sand, water, rock, forest, rubble, house, wire, trench, crater, rail, ford, bridge / 높이만: high, low)"),
    };

    static void Require(bool ok, string msg)
    {
        if (!ok) throw new InvalidDataException(msg);
    }

    /// <summary>두 칸 사이 직선 (브레즌햄, 8방향 이웃으로 이어짐, 양 끝 포함).</summary>
    internal static IEnumerable<(int X, int Y)> LineTiles(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1) yield break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}

public sealed class FactionDef
{
    /// <summary>표시 이름 (한글은 Strings.cs 상수와 같은 글자여야 폰트에 들어간다).</summary>
    public string Name { get; set; } = "";
    /// <summary>영문 id (JSON 키는 "en" 유지, --faction 옵션용: korean · japanese · fengtian · russian · soviet).</summary>
    public string En { get; set; } = "";
    public int[]? Color { get; set; }
    /// <summary>언어 코드 (en / ru / de ...) — 이 세력의 보이스 언어 (assets/sounds/&lt;테마&gt;/voice/&lt;lang&gt;/). 비우면 en.</summary>
    public string Lang { get; set; } = "";
    /// <summary>정적 팀 번호 (선택). 같은 번호(1 이상)의 세력끼리 동맹 — 서로 싸우지 않고 시야 공유, 실행 중 불변. 0 · 생략 = 팀 없음 (모두와 적).</summary>
    public int Team { get; set; }
    /// <summary>전술 AI 프로필 이름 (assets/data/ai_profiles.json, 선택). 비우면 영문 id (en) → default.</summary>
    public string? AiProfile { get; set; }
    /// <summary>전술 AI 기본 태세 (attack · defend · hold, 선택). 비우면 프로필의 posture.</summary>
    public string? AiPosture { get; set; }
}

public sealed class Paint
{
    /// <summary>지형 이름 (grass · dirt · sand · water · rock · forest · rubble · house · wire · trench · crater · rail — 선로는 1칸 폭 rect · tiles 로 이어 칠한다, 꺾임 · 대각선 연결은 RailRenderer 가 이웃으로 정함) 또는 높이만 바꾸는 "high" · "low".</summary>
    public string Terrain { get; set; } = "grass";
    public int[]? Rect { get; set; }
    public int[]? Circle { get; set; }
    /// <summary>낱칸 목록 [[x, y], ...] (가옥 · 철조망 · 참호선을 한 칸씩 그릴 때).</summary>
    public int[][]? Tiles { get; set; }
    /// <summary>꺾은선 [x0, y0, x1, y1, ...] — 1칸 폭으로 이어 칠한다 (선로 · 길).</summary>
    public int[]? Line { get; set; }
    /// <summary>꺾은선 폭 (칸, 1~6, 생략 = 1 = 현행). 정사각 확장 — 하천 (T39a). rect · circle · tiles 에는 영향 없음.</summary>
    public int? Width { get; set; }
    /// <summary>지형과 함께 고지 표시도 바꾼다 (생략 = 그대로).</summary>
    public bool? High { get; set; }
}

public sealed class Site
{
    public int[] At { get; set; } = Array.Empty<int>();
    /// <summary>건물 키 (capturable). 기본 village.</summary>
    public string Type { get; set; } = "village";
    public int Owner { get; set; }
}

/// <summary>지도 파일의 유닛 배치 한 줄 ("units").</summary>
public sealed class UnitPlacement
{
    public string Unit { get; set; } = "";
    public float[] At { get; set; } = Array.Empty<float>();
    public int Faction { get; set; } = 1;
    public int Count { get; set; } = 1;
}
