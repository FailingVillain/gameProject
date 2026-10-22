using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KoreanDivision.Core;

/// <summary>
/// 미션 파일 (assets/missions/<id>.json) — 지도 참조(또는 인라인 지도) · 세력 · 병력 배치 · 증원 · 목표 · 실패 조건 · 사건표.
/// 형식 요약은 docs/ENGINE_NOTES.md '미션 틀'. 정의는 불변 데이터 (실행 상태는 MissionState).
/// 좌표 = 타일, 방향(facing) = 나침반 각도 (0 = 북/위, 90 = 동/오른쪽, 180 = 남, 270 = 서).
/// </summary>
public sealed partial class MissionDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>날짜 · 장소 (브리핑 표시).</summary>
    public string Date { get; set; } = "";
    public string Location { get; set; } = "";
    public string Briefing { get; set; } = "";
    /// <summary>기존 지도 id (assets/maps/<id>.json). MapData 가 있으면 무시.</summary>
    public string? Map { get; set; }
    /// <summary>인라인 지도 (지도 파일과 같은 형식의 객체).</summary>
    public JsonElement? MapData { get; set; }
    public int Seed { get; set; } = 1;
    /// <summary>날씨 · 시간대 (그대로 전달만 — 효과는 날씨 작업이 지도 json "weather"/"time" 과 같은 이름으로 구현).</summary>
    public string? Weather { get; set; }
    public string? Time { get; set; }
    /// <summary>민가 그림 양식 (korean | manchu | mixed, 생략 = 지도 파일의 houseStyle, 그것도 없으면 예전 4종). 렌더 전용 — 시뮬 · 체크섬 · 스냅샷과 무관 (T55s).</summary>
    public string? HouseStyle { get; set; }
    /// <summary>세력 정의 (세력 N = N번째). 지도 파일의 factions 를 대신한다.</summary>
    public List<MissionFaction> Factions { get; set; } = new();
    /// <summary>플레이어 세력 번호 (1부터).</summary>
    public int Player { get; set; } = 1;
    /// <summary>true = AI 대 AI 관전 미션 (게임이 관전 모드로 열린다, 배치 단계 없음). --mission-run 은 항상 모든 세력 AI.</summary>
    public bool Spectate { get; set; }
    /// <summary>true = 캠페인 이월 미션: Load 가 캠페인 상태의 생존 병력 (계급 · 장교 경험치) 을 병력 "b1" 의 중대에 써 넣은 텍스트로 읽는다 (CampaignCarry). 스냅샷 · 리플레이에는 써 넣은 텍스트가 그대로 실린다.</summary>
    public bool Carry { get; set; }
    /// <summary>배치 구역 (플레이어가 브리핑 배치 단계에서 자기 부대를 옮길 수 있는 곳).</summary>
    public List<MissionZone> DeployZones { get; set; } = new();
    /// <summary>이름 붙은 구역 (목표 · 사건 · 증원 위치가 이름으로 참조).</summary>
    public Dictionary<string, MissionArea> Areas { get; set; } = new();
    /// <summary>시작 병력 (배치 단위 = 항목 하나: 대대 / 중대 / 낱 유닛 묶음).</summary>
    public List<MissionForce> Forces { get; set; } = new();
    /// <summary>증원: time(초) 이 있으면 그 시각, 없으면 사건의 spawn 으로만.</summary>
    public List<MissionForce> Reinforcements { get; set; } = new();
    public List<MissionObjective> Objectives { get; set; } = new();
    /// <summary>패배 조건: losses (아군 손실 N%) · keyUnit (태그 유닛 사망) · timeLimit (N초 안에 주 목표 미달성).</summary>
    public List<MissionFailure> Failures { get; set; } = new();
    public List<MissionEvent> Events { get; set; } = new();
    /// <summary>F6 엄폐 소품 (assets/data/props.json 종류, 칸 좌표) - 손으로 놓는다.</summary>
    public List<MissionProp> Props { get; set; } = new();
    /// <summary>열차 (T30a, Train.cs): 객차 · 경유점 경로 · 정차 시간 · 스크립트/조종.</summary>
    public List<MissionTrain> Trains { get; set; } = new();
    /// <summary>true = 지도 생성기와 같은 규칙으로 소품을 흩뿌린다 (병력 둘레 · 배치 구역 · 증원 구역은 비움).</summary>
    public bool PropScatter { get; set; }
    /// <summary>propScatter 군집 확률 배수 (기본 1 = 예전과 같음). clusterPct x 값, 상한 100.</summary>
    public float PropDensity { get; set; } = 1f;
    /// <summary>propScatter 군집 칸 크기 (타일, 기본 0 = props.json scatter.cell). 작을수록 같은 지도에 군집이 많다.</summary>
    public int PropCell { get; set; }
    /// <summary>[Phase 2 S3] 최대 출격 중대 수 제한 (캠페인 출격 통제). 없으면 제한 없음.</summary>
    public int? MaxDeploy { get; set; }
    /// <summary>[T19b 캠페인 포탄] 플레이어 세력 유닛의 무기 키별 시작 탄약 비율 (0~1). 이 무기를 든 유닛은 중대 ammoScale 대신 이 값 (CampaignCarry 가 씀). 없으면 영향 없음.</summary>
    public Dictionary<string, float>? AmmoScaleByWeapon { get; set; }

    [JsonIgnore] public string Json { get; internal set; } = "";
    [JsonIgnore] public string Hash { get; internal set; } = "";

    public MissionArea? Area(string? name) => name != null && Areas.TryGetValue(name, out var a) ? a : null;
    public int FactionCount => Factions.Count;
    internal static Vector2 ForceCenterOf(MissionDef def, MissionForce f) =>
        f.At is { Length: 2 } a ? new Vector2(a[0], a[1]) : def.Area(f.Area)?.Center ?? Vector2.Zero;

    // ---------------------------------------------------------------- 파일

    static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "assets", "missions");
    public static string PathOf(string id) => Path.Combine(Folder, id + ".json");
    public static IEnumerable<string> Available() =>
        Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(n => n, StringComparer.Ordinal) : Array.Empty<string>();

    /// <summary>관전 미션 (spectate:true = AI 대 AI) id · 제목. 시작 화면 '관전' 탭의 목록 (T45). 읽기 실패한 파일은 뺀다 (미션 메뉴가 '고장'으로 보여 준다).</summary>
    public static List<(string Id, string Title)> AvailableSpectate()
    {
        var list = new List<(string, string)>();
        foreach (var id in Available())
        {
            try { var d = Load(id); if (d.Spectate) list.Add((id, d.Title)); }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException) { }
        }
        return list;
    }

    public static MissionDef Load(string id)
    {
        string path = PathOf(id);
        if (!File.Exists(path)) throw new FileNotFoundException($"미션 파일 없음: {path}");
        string text = File.ReadAllText(path);
        var cs = CampaignState.Load();
        if (CampaignCarry.WantsCarry(text) || CampaignCarry.WantsPending(id, cs)) text = CampaignCarry.Patch(text, id, cs);
        return Parse(text, id);
    }

    /// <summary>시작 화면 목록용 (깨진 파일은 null).</summary>
    public static MissionDef? Peek(string id)
    {
        try { return Load(id); }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or InvalidOperationException) { return null; }
    }

    /// <summary>JSON 텍스트 → 정의 (검증 포함). 세이브 · 리플레이는 이 텍스트를 그대로 들고 다닌다.</summary>
    public static MissionDef Parse(string text, string? id = null)
    {
        MissionDef def;
        try { def = JsonSerializer.Deserialize<MissionDef>(text, Opts) ?? throw new InvalidDataException("빈 미션 파일"); }
        catch (JsonException e) { throw new InvalidDataException($"미션 JSON 오류: {e.Message}", e); }
        if (string.IsNullOrWhiteSpace(def.Id)) def.Id = id ?? "mission";
        def.Json = text;
        def.Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];
        def.Validate(Defs.Current);
        return def;
    }

    /// <summary>폰트 아틀라스용: 모든 미션 파일의 표시 문자열.</summary>
    public static IEnumerable<string> AllText()
    {
        var list = new List<string>();
        foreach (var id in Available())
        {
            try
            {
                var d = JsonSerializer.Deserialize<MissionDef>(File.ReadAllText(PathOf(id)), Opts);
                if (d == null) continue;
                list.Add(d.Title); list.Add(d.Date); list.Add(d.Location); list.Add(d.Briefing);
                list.AddRange(d.Factions.Select(f => f.Name));
                list.AddRange(d.Forces.Concat(d.Reinforcements).Select(f => f.Name ?? ""));
                list.AddRange(d.Objectives.Select(o => o.Text ?? ""));
                list.AddRange(d.Failures.Select(o => o.Text ?? ""));
                foreach (var e in d.Events) list.AddRange(e.Do.Select(a => a.Message ?? ""));
                if (d.MapData is { ValueKind: JsonValueKind.Object } md && md.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String) list.Add(nm.GetString() ?? "");
            }
            catch (Exception e) when (e is IOException or JsonException) { }
        }
        return list;
    }

    // ---------------------------------------------------------------- 검증

    void Validate(Defs defs)
    {
        Require(Factions.Count >= 2 && Factions.Count <= Config.MaxPlayers, $"미션 세력은 2~{Config.MaxPlayers}개");
        Require(Player >= 1 && Player <= Factions.Count, $"플레이어 세력 {Player} 이 범위 밖");
        Require(Map != null || MapData is { ValueKind: JsonValueKind.Object }, "지도가 없음 (map 또는 mapData)");
        foreach (var f in Factions) Require(MissionFaction.Postures.Contains(f.Posture ?? "attack"), $"세력 자세 '{f.Posture}' (attack · defend · hold · counter)");
        foreach (var f in Factions) Require(f.AiWithdrawAt is null or (>= 0 and < 1) && f.AiCommitment is null or (>= 0 and <= 1), $"세력 '{f.Name}': aiWithdrawAt 0~1 미만 · aiCommitment 0~1");
        foreach (var pr in Props) Require(PropDefs.Current.TryGet(pr.Type, out _), $"소품 종류 '{pr.Type}' 없음 (props.json)");
        foreach (var (name, a) in Areas) Require(a.Valid, $"구역 '{name}' 은 rect [x,y,w,h] 또는 circle [cx,cy,r]");
        foreach (var z in DeployZones) Require(z.Faction >= 1 && z.Faction <= Factions.Count && z.Valid, "배치 구역: faction 범위 · rect/circle");
        var ids = new HashSet<string>();
        foreach (var f in Forces.Concat(Reinforcements))
        {
            Require(f.Posture == null || MissionFaction.Postures.Contains(f.Posture), $"병력 '{f.Id}' 자세 '{f.Posture}'");
            Require(f.AiArea == null || Area(f.AiArea) != null, $"병력 '{f.Id}': 구역 '{f.AiArea}' 없음");
            Require(!string.IsNullOrWhiteSpace(f.Id) && ids.Add(f.Id), $"병력 id '{f.Id}' 가 비었거나 중복");
            Require(f.Faction >= 1 && f.Faction <= Factions.Count, $"병력 '{f.Id}' 의 세력 {f.Faction} 이 범위 밖");
            Require(f.At is { Length: 2 } || Area(f.Area) != null, $"병력 '{f.Id}' 위치 (at [x,y] 또는 area)");
            foreach (var c in f.AllCompanies())
            {
                Require(c.Units.Count > 0, $"병력 '{f.Id}' 에 빈 중대");
                foreach (var (k, n) in c.Units) Require(defs.Units.ContainsKey(k) && n >= 0, $"병력 '{f.Id}': 유닛 '{k}' 없음");
                foreach (var (k, n) in c.Units) Require(n == 0 || !defs.Units.TryGetValue(k, out var ud) || ud.AllowedFor(Factions[f.Faction - 1].En), $"병력 '{f.Id}': 유닛 '{k}' 는 세력 '{Factions[f.Faction - 1].En}' 편성 불가 (전용 유닛: koreaOnly / factions)");
                Require(c.Weapon == null || defs.Weapons.ContainsKey(c.Weapon), $"병력 '{f.Id}': 무기 '{c.Weapon}' 없음");
                Require((c.Rank ?? 1) >= 0 && (c.Rank ?? 1) <= defs.CombatRules.MaxRank, $"병력 '{f.Id}': 계급 {c.Rank} (0~{defs.CombatRules.MaxRank})");
                if (c.Rank is > 0 && RankNames.Snap(Factions[f.Faction - 1].En, c.Rank.Value) != c.Rank)
                    Console.Error.WriteLine($"[계급 경고] 미션 '{Id}' 병력 '{f.Id}': 계급 {c.Rank} 는 세력 '{Factions[f.Faction - 1].En}' 에서 미사용 → {RankNames.Snap(Factions[f.Faction - 1].En, c.Rank.Value)} 로 맞춤");
                if (c.SquadLeaderRank is > 0 && RankNames.Snap(Factions[f.Faction - 1].En, c.SquadLeaderRank.Value) != c.SquadLeaderRank)
                    Console.Error.WriteLine($"[계급 경고] 미션 '{Id}' 병력 '{f.Id}': 분대장 계급 {c.SquadLeaderRank} 는 세력 '{Factions[f.Faction - 1].En}' 에서 미사용 → 맞춤");
                Require(c.OfficerRanks == null || c.OfficerRanks.All(r => r >= 1 && r <= defs.Officers.MaxRank), $"병력 '{f.Id}': 장교 계급 (1~{defs.Officers.MaxRank})");
                Require(c.InfantryRanks == null || c.InfantryRanks.All(r => r >= 0 && r <= defs.CombatRules.MaxRank), $"병력 '{f.Id}': 보병 계급 목록 (0~{defs.CombatRules.MaxRank})");
                Require(c.OfficerXp >= 0, $"병력 '{f.Id}': 장교 경험치 {c.OfficerXp}");
                Require(c.OfficerKeys == null || c.OfficerKeys.All(k => k >= 0), $"병력 '{f.Id}': 장교 신원 키 (0 이상)");   // T48b1
                Require(c.SquadLeaderRank is null || (c.SquadLeaderRank >= 0 && c.SquadLeaderRank <= defs.CombatRules.MaxRank), $"병력 '{f.Id}': 분대장 계급 {c.SquadLeaderRank} (0~{defs.CombatRules.MaxRank})");
                Require(MissionCompany.ParseForm(c.Formation ?? f.Formation) != null, $"병력 '{f.Id}': 대형 '{c.Formation ?? f.Formation}'");
            }
        }
        ValidateTrains();   // T30c (FreightObjective.cs)
        var oids = new HashSet<string>();
        foreach (var o in Objectives)
        {
            Require(!string.IsNullOrWhiteSpace(o.Id) && oids.Add(o.Id), $"목표 id '{o.Id}' 가 비었거나 중복");
            Require(MissionObjective.Types.Contains(o.Type), $"목표 '{o.Id}' 종류 '{o.Type}' (capture · eliminate · defend · escort)");
            if (o.Train != null) { ValidateFreightObjective(o, defs); continue; }   // T30c 화물열차 목표 (FreightObjective.cs)
            if (o.Type is "capture" or "escort") Require(Area(o.Area) != null, $"목표 '{o.Id}': 구역 '{o.Area}' 없음");
            if (o.Type == "defend") Require(o.Seconds > 0, $"목표 '{o.Id}': defend 는 seconds 필요");
            if (o.Type == "escort") Require(!string.IsNullOrEmpty(o.Tag), $"목표 '{o.Id}': escort 는 tag 필요");
            if (o.Type == "defend" && o.Area != null) Require(Area(o.Area) != null, $"목표 '{o.Id}': 구역 '{o.Area}' 없음");
        }
        foreach (var f in Failures) Require(MissionFailure.Types.Contains(f.Type), $"패배 조건 종류 '{f.Type}' (losses · keyUnit · timeLimit)");
        foreach (var e in Events)
        {
            Require(e.When.Area == null || Area(e.When.Area) != null, $"사건 '{e.Id}': 구역 '{e.When.Area}' 없음");
            Require(e.When.Objective == null || oids.Contains(e.When.Objective), $"사건 '{e.Id}': 목표 '{e.When.Objective}' 없음");
            foreach (var a in e.Do)
            {
                Require(a.Spawn == null || Reinforcements.Any(r => r.Id == a.Spawn), $"사건 '{e.Id}': 증원 '{a.Spawn}' 없음");
                Require(a.Reveal == null || oids.Contains(a.Reveal), $"사건 '{e.Id}': 목표 '{a.Reveal}' 없음");
            }
        }
        Require(Objectives.Any(o => o.Primary), "주 목표(primary)가 하나 이상 필요");
    }

    static void Require(bool ok, string msg)
    {
        if (!ok) throw new InvalidDataException(msg);
    }

    /// <summary>나침반 각도 → 단위 방향 (45° 배수는 정확한 표 값 — 결정적이고 깔끔하게).</summary>
    public static Vector2 DirOf(float degrees)
    {
        float d = degrees % 360f;
        if (d < 0) d += 360f;
        if (MathF.Abs(d / 45f - MathF.Round(d / 45f)) < 1e-4f)
        {
            const float h = 0.70710677f;
            return ((int)MathF.Round(d / 45f) % 8) switch
            {
                0 => new(0, -1), 1 => new(h, -h), 2 => new(1, 0), 3 => new(h, h),
                4 => new(0, 1), 5 => new(-h, h), 6 => new(-1, 0), _ => new(-h, -h),
            };
        }
        float r = d * (DetMath.Pi / 180f);
        return new Vector2(DetMath.Sin(r), -DetMath.Cos(r));
    }
}

public sealed class MissionFaction
{
    public static readonly string[] Postures = { "attack", "defend", "hold", "counter" };   // counter = 역습 (공격 + 역습 교리, TacticalAi.Counter.cs)
    public string Name { get; set; } = "";
    public string En { get; set; } = "";
    public int[]? Color { get; set; }
    public string Lang { get; set; } = "";
    public int Team { get; set; }
    /// <summary>AI 컨트롤러 이름 (기본 "mission"). 플레이어 세력은 무시 (관전 · 자동 실행에서는 사용).</summary>
    public string? Ai { get; set; }
    /// <summary>AI 자세: attack (목표/적으로 진격) · defend (배치 위치 주변 방어) · hold (holdArea 고수).</summary>
    public string? Posture { get; set; } = "attack";
    /// <summary>hold / defend 구역 이름 (없으면 배치 위치), attack 목표 구역 이름 (없으면 적 시작 위치).</summary>
    public string? Area { get; set; }
    /// <summary>[훅] 전술 AI 프로필 이름 (ROADMAP 전술 AI — 세력별 + 미션별 프로필 JSON). 지금은 저장만.</summary>
    public string? AiProfile { get; set; }
    /// <summary>전술 AI 철수 문턱 (stage 3A, 사용자 결정 5): 병력 비율 (남은 / 처음) 이 이 아래면 목표를 버리고 물러나 방어 (0 = 끝까지). 없으면 프로필 withdrawAt.</summary>
    public float? AiWithdrawAt { get; set; }
    /// <summary>전술 AI 목표 고수 정도 0~1 (0.5 = 프로필 그대로, 1 = 공격 한계 · 철수 없음, 0 = 문턱 2배). 없으면 프로필 commitment.</summary>
    public float? AiCommitment { get; set; }
}

/// <summary>사각형 또는 원 구역.</summary>
public class MissionArea
{
    public float[]? Rect { get; set; }
    public float[]? Circle { get; set; }
    [JsonIgnore] public bool Valid => Rect is { Length: 4 } || Circle is { Length: 3 };
    public bool Contains(Vector2 p)
    {
        if (Rect is { Length: 4 } r) return p.X >= r[0] && p.Y >= r[1] && p.X < r[0] + r[2] && p.Y < r[1] + r[3];
        if (Circle is { Length: 3 } c) { float dx = p.X - c[0], dy = p.Y - c[1]; return dx * dx + dy * dy <= c[2] * c[2]; }
        return false;
    }
    [JsonIgnore] public Vector2 Center => Rect is { Length: 4 } r ? new(r[0] + r[2] / 2, r[1] + r[3] / 2) : Circle is { Length: 3 } c ? new(c[0], c[1]) : Vector2.Zero;
    /// <summary>대략 반경 (AI 고수 · 표시용).</summary>
    [JsonIgnore] public float Radius => Rect is { Length: 4 } r ? MathF.Max(r[2], r[3]) / 2 : Circle is { Length: 3 } c ? c[2] : 0;
    /// <summary>점을 구역 안으로 (사각형 = 가장자리로 자르기, 원 = 중심 쪽으로).</summary>
    public Vector2 Clamp(Vector2 p)
    {
        if (Rect is { Length: 4 } r) return new(Math.Clamp(p.X, r[0] + 0.5f, r[0] + r[2] - 0.5f), Math.Clamp(p.Y, r[1] + 0.5f, r[1] + r[3] - 0.5f));
        if (Circle is { Length: 3 } c)
        {
            var d = p - new Vector2(c[0], c[1]);
            float l = d.Length(), m = MathF.Max(0, c[2] - 0.5f);
            return l <= m ? p : new Vector2(c[0], c[1]) + d / l * m;
        }
        return p;
    }
}

public sealed class MissionProp
{
    public string Type { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class MissionZone : MissionArea
{
    public int Faction { get; set; } = 1;
}

/// <summary>배치 단위 (대대 · 중대 · 낱 유닛 묶음). companies 가 있으면 대대 (중대 블록을 나란히), 없으면 units 를 한 묶음으로.</summary>
public sealed class MissionForce
{
    public string Id { get; set; } = "";
    public int Faction { get; set; } = 1;
    public string? Name { get; set; }
    public float[]? At { get; set; }
    /// <summary>증원 등장 구역 이름 (at 이 없을 때 그 중심).</summary>
    public string? Area { get; set; }
    public float Facing { get; set; }
    /// <summary>중대 대형 기본값: line · column · box · spread · wedge · free.</summary>
    public string? Formation { get; set; }
    /// <summary>대대 대형: line · column · echelon · reserve.</summary>
    public string? BattalionForm { get; set; }
    /// <summary>대대 번호 (1~9, 0 = 번호 없음). 중대가 2개 이상이면 기본 자동 번호.</summary>
    public int? Battalion { get; set; }
    public List<MissionCompany>? Companies { get; set; }
    // 낱 묶음 (companies 가 없을 때)
    public Dictionary<string, int>? Units { get; set; }
    public int? Rank { get; set; }
    /// <summary>분대장 계급 (부사관 기본 편성, 2026-10-19 사용자 승인). 없으면 combat.squadLeaderRank (계급 rank 를 정한 병력은 기본 적용 안 함), 0 = 분대장 계급 없음.</summary>
    public int? SquadLeaderRank { get; set; }
    /// <summary>낱 묶음의 장교 계급 · 시작 경험치 (MissionCompany 와 같음).</summary>
    public int[]? OfficerRanks { get; set; }
    /// <summary>[N54] 소총 칸 보병의 소총을 이 무기로 고정 (세력 분배 무시). 예 "rifle_mauser98".</summary>
    public string? Weapon { get; set; }
    public int OfficerXp { get; set; }
    public string? Tag { get; set; }
    /// <summary>낱 묶음을 중대로 편성할지 (기본: 보병이 섞이면 true, 마차 · 포만이면 false).</summary>
    public bool? Company { get; set; }
    /// <summary>플레이어가 배치 단계에서 옮길 수 있는지 (기본: 플레이어 세력이고 배치 구역 안이면 true).</summary>
    public bool? Deployable { get; set; }
    /// <summary>증원 시각 (초). null = 사건으로만.</summary>
    public float? Time { get; set; }
    /// <summary>AI 힌트: 이 병력만의 자세 · 구역 (없으면 세력의 posture · area).</summary>
    public string? Posture { get; set; }
    public string? AiArea { get; set; }
    /// <summary>[Phase 2 S3] 시작 탄약 비율 (0~1). 없으면 1.0 (완전 보급).</summary>
    public float? AmmoScale { get; set; }

    public IEnumerable<MissionCompany> AllCompanies()
    {
        if (Companies is { Count: > 0 })
        {
            if (AmmoScale != null)
                foreach (var c in Companies) c.AmmoScale ??= AmmoScale;
            return Companies;
        }
        if (Units is { Count: > 0 }) return new[] { new MissionCompany { Units = Units, Rank = Rank, SquadLeaderRank = SquadLeaderRank, OfficerRanks = OfficerRanks, OfficerXp = OfficerXp, Tag = Tag, Formation = Formation, Company = Company, Weapon = Weapon, AmmoScale = AmmoScale } };
        return Array.Empty<MissionCompany>();
    }
}

public sealed class MissionCompany
{
    /// <summary>[N54] 소총 칸 보병의 소총을 이 무기로 고정 (세력 분배 무시).</summary>
    public string? Weapon { get; set; }
    public Dictionary<string, int> Units { get; set; } = new();
    public string? Formation { get; set; }
    /// <summary>계급 단계 (없음 = 기본 1 이등병 — World.DefaultEnlistedRank, 명시 0 = 훈련병, 1~ = combat.rankKills 의 단계만큼 사살 수를 주고 시작).</summary>
    public int? Rank { get; set; }
    /// <summary>분대장 계급 (부사관 기본 편성, 2026-10-19 사용자 승인). 없으면 combat.squadLeaderRank (계급 rank 를 정한 병력은 기본 적용 안 함), 0 = 분대장 계급 없음.</summary>
    public int? SquadLeaderRank { get; set; }
    /// <summary>장교 정식 계급 (배치 순서대로, 1 소위 … 6 대령). 없으면 직책으로 자동 (첫 장교 대위 · 둘째 중위 · 나머지 소위, 대대장 소령 / 중령). 지정한 병력은 대대장 자동 진급도 없음 (임시 진급만).</summary>
    public int[]? OfficerRanks { get; set; }
    /// <summary>장교 시작 경험치 (캠페인 계승).</summary>
    public int OfficerXp { get; set; }
    /// <summary>[T48b1 캠페인 이월] 장교 신원 키 (officerRanks 와 나란히, 배치 순서대로; 0 = 키 없음). CampaignCarry 가 씀 — 시뮬 판정 무관, 결과 정산용.</summary>
    public int[]? OfficerKeys { get; set; }
    /// <summary>[캠페인 이월] 소총 보병 (장교 · 중화기 · 정찰병 제외) 의 개별 계급, 배치 순서대로. 목록보다 보병이 많으면 나머지는 rank (없으면 기본). CampaignCarry 가 씀.</summary>
    public int[]? InfantryRanks { get; set; }
    /// <summary>태그 (목표 · 사건이 참조: officer · guns · wagon ...).</summary>
    public string? Tag { get; set; }
    public bool? Company { get; set; }
    /// <summary>[Phase 2 S3] 시작 탄약 비율 (0~1). 없으면 1.0 (완전 보급).</summary>
    public float? AmmoScale { get; set; }

    public static FormType? ParseForm(string? s) => (s ?? "line").ToLowerInvariant() switch
    {
        "line" => FormType.Line, "column" => FormType.Column, "box" => FormType.Box,
        "spread" => FormType.Spread, "wedge" => FormType.Wedge, "free" => FormType.Free, "close" => FormType.Box, "open" => FormType.Spread,
        "echelon_left" => FormType.EchelonLeft, "echelon_right" => FormType.EchelonRight, _ => null,
    };
}

/// <summary>목표: capture (구역 N초 확보) · eliminate (적 전멸 / N% / 태그) · defend (N초 버티기, 구역 있으면 빼앗기면 실패) · escort (태그 유닛이 구역 도착).</summary>
public sealed class MissionObjective
{
    public static readonly string[] Types = { "capture", "eliminate", "defend", "escort" };
    public string Id { get; set; } = "";
    public string Type { get; set; } = "capture";
    public string? Text { get; set; }
    public bool Primary { get; set; } = true;
    /// <summary>처음엔 숨김 (사건의 reveal 로 공개). 숨긴 목표도 달성 판정은 계속한다.</summary>
    public bool Hidden { get; set; }
    public string? Area { get; set; }
    /// <summary>capture: 확보 유지 초 · defend: 버틸 초.</summary>
    public float Seconds { get; set; }
    /// <summary>eliminate: 처치 비율 (기본 100) · escort: 도착해야 할 수 (기본 1).</summary>
    public float Percent { get; set; } = 100;
    public int Count { get; set; } = 1;
    public string? Tag { get; set; }
    /// <summary>대상 세력 (eliminate: 기본 = 플레이어의 모든 적, capture/escort/defend: 기본 = 플레이어).</summary>
    public int Faction { get; set; }
    /// <summary>defend + area: 적이 구역을 이 초 동안 홀로 차지하면 실패 (기본 10).</summary>
    public float LoseAfter { get; set; } = 10;
    /// <summary>T30c 화물열차 목표: 열차 tag (미션 trains[].tag). 대상 = 그 열차들의 화물 객차 (balance.json trains.cars 의 freight).
    /// escort = 화물 객차 count 량이 area 도착 · capture = 살아 있는 화물 객차 radius 안을 seconds 초 단독 확보 (소유 변경 없음) · eliminate = 화물 객차 percent% 파괴. FreightObjective.cs.</summary>
    public string? Train { get; set; }
    /// <summary>T30c capture + train: 화물 객차 둘레 확보 반경 (칸, 기본 3).</summary>
    public float Radius { get; set; } = 3;
}

public sealed class MissionFailure
{
    public static readonly string[] Types = { "losses", "keyUnit", "timeLimit" };
    public string Type { get; set; } = "losses";
    public string? Text { get; set; }
    public float Percent { get; set; } = 80;
    public string? Tag { get; set; }
    public float Seconds { get; set; }
}

/// <summary>사건: when 의 조건이 모두 참이 되면 do 의 행동을 차례로 (한 번만).</summary>
public sealed class MissionEvent
{
    public string Id { get; set; } = "";
    public MissionCondition When { get; set; } = new();
    public List<MissionAction> Do { get; set; } = new();
}

public sealed class MissionCondition
{
    /// <summary>경과 시간 (초) 이상.</summary>
    public float? Time { get; set; }
    /// <summary>구역에 faction 의 유닛이 하나라도 들어감 (faction 기본 = 플레이어).</summary>
    public string? Area { get; set; }
    public int Faction { get; set; }
    /// <summary>목표 상태: objective + state (done · failed, 기본 done).</summary>
    public string? Objective { get; set; }
    public string? State { get; set; }
    /// <summary>태그 유닛이 모두 죽음.</summary>
    public string? Killed { get; set; }
}

public sealed class MissionAction
{
    public string? Spawn { get; set; }
    public string? Message { get; set; }
    public string? Weather { get; set; }
    public string? Time { get; set; }
    public string? Reveal { get; set; }
}
