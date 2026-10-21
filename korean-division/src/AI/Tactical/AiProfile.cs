using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KoreanDivision.Core;

/// <summary>AI 태세 (미션 · 지도가 세력마다 정한다). HoldArea = 지역 고수 (SetDirective 의 area · radius).</summary>
/// <summary>Counter = 역습 (공격 + 역습 교리: 고정 사격 → 일제 돌격, TacticalAi.Counter.cs — 미션 posture "counter", 2026-10-19).</summary>
public enum AiPosture { Attack = 0, Defend = 1, HoldArea = 2, Counter = 3 }

/// <summary>
/// 미션 프레임워크용 작은 접점: AI 컨트롤러가 이것을 구현하면 미션 파일의 세력별 · 병력별 태세 · 프로필 이름을 넘길 수 있다.
/// 게임 시작 전 (또는 시뮬레이션 안의 결정적 시점) 에 부른다 — 명령이 아니므로 리플레이에는 기록되지 않는다 (같은 미션 = 같은 호출).
/// 값은 AI 상태로 저장된다 (스냅샷).
/// </summary>
public interface IAiDirectable
{
    /// <summary>세력 기본 지시 (지시 그룹 0).</summary>
    /// <param name="profile">ai_profiles.json 프로필 이름 (null = 그대로).</param>
    /// <param name="area">HoldArea · Defend 의 중심 (null = 시작 위치).</param>
    void SetDirective(int faction, AiPosture posture, string? profile = null, Vector2? area = null, float radius = 0);

    /// <summary>
    /// 지시 그룹 (전술 AI 2단계): group ≥ 1 의 태세 · 구역. 구성원 = 미션 병력 id (forces — 증원은 생길 때 Mission.ForceUnits 로 찾는다) 와 유닛 id (units).
    /// area 가 없으면 hold · defend 는 그룹 유닛의 처음 무게중심을 지킨다 (흩어진 매복조가 각자 자리를 지킴).
    /// </summary>
    void SetDirective(int faction, int group, AiPosture posture, Vector2? area, float radius, IReadOnlyList<string>? forces, IReadOnlyList<int>? units = null);
}

/// <summary>
/// 전술 AI 프로필 (assets/data/ai_profiles.json). 두 절: <see cref="Strategy"/> (세력 전략 층) · <see cref="Tactics"/> (연대 전술 · 대대 · 중대 실행 층).
/// 필드 기본값 = 파일이 없을 때의 "default". AI 상태에 복사본으로 저장된다 (스냅샷 — 파일이 바뀌어도 이어 하기는 같다).
/// </summary>
public sealed class AiProfile
{
    public string Name = "default";
    public StrategyProfile Strategy = new();
    public TacticsProfile Tactics = new();

    public AiPosture DefaultPosture => ParsePosture(Strategy.Posture) ?? (Strategy.Scheme == "defensive" ? AiPosture.Defend : AiPosture.Attack);

    public static AiPosture? ParsePosture(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "attack" => AiPosture.Attack,
        "defend" => AiPosture.Defend,
        "hold" or "holdarea" or "hold-area" => AiPosture.HoldArea,
        "counter" or "counterattack" => AiPosture.Counter,
        _ => null,
    };

    public AiProfile Clone() => new() { Name = Name, Strategy = (StrategyProfile)Strategy.Clone(), Tactics = (TacticsProfile)Tactics.Clone() };
}

/// <summary>세력 전략 층 프로필 (태세 · 예비 · 주공 · 포병/기병 배정 · 태세 변경).</summary>
public sealed class StrategyProfile
{
    public string Posture = "attack";
    /// <summary>N73 매복: 방어 · 예비 과제 소총 · 화력 중대를 '응사만' 으로 (적이 가까이 오거나 맞을 때까지 쏘지 않음), 그 밖의 과제는 자유 사격. 기본 끔.</summary>
    public bool AmbushHoldFire = false;
    public float Aggressiveness = 0.5f;
    public bool Reserve = true;
    public float DefendPatience = 90f;
    public float Artillery = 0.7f;
    public float Cavalry = 0.6f;
    public string Scheme = "frontal";
    public float MainEffort = 0.5f;
    public int RegimentSize = 3;
    public float CounterattackOdds = 2.2f;
    public float Culminate = 0.35f;
    // ---- 3단계 A (stage 3A, docs/TACTICAL_AI_STAGE3_REVIEW_2026-10-19.md S1 · S14): 모든 새 행동은 이 키 뒤 — 2단계 비교 = 겹침 프로필 "stage2" (예: korean+stage2).
    //      성격 (세력별 편향) 은 보류 (사용자 결정 7) — plan* 편향 기본 0 (중립). 난이도가 같은 키를 다시 쓴다.
    /// <summary>S1 기동 계획: 접촉 때 연대가 정면 · 측면 (고정 + 측면 제대) · 협공 (두 방향 수렴) · 중점 (좁은 정면 집중) 중 하나를 점수로 고른다. false = 2단계 (scheme 만).</summary>
    public bool Manoeuvre = true;
    /// <summary>접촉 전 계획: 미션 점령 목표 · 공격 구역이 있으면 그곳을 적 진지로 보고 접근 중에 측면 · 협공을 미리 정한다 (접촉하면 실제 적 정면 끝에 맞춤).</summary>
    public bool PrePlan = false;   // 측정 (PIN_T_front4 · T_r1.5 8시드): 켬 = 손실 33.6% · 승 3/8, 끔 = 27.4% · 7/8 → 기본 끔
    /// <summary>계획 종류별 점수 편향 (성격 · 난이도용, 기본 0 = 중립).</summary>
    public float PlanFrontal = 0f, PlanFlank = 0f, PlanPincer = 0f, PlanSchwerpunkt = 0f;
    /// <summary>측면 우회 상한 (사용자 결정 4): 우회 경로 ≤ 정면 거리 × maxDetour, 도착 ≤ maxArrive 초.</summary>
    public float MaxDetour = 2f;
    public float MaxArrive = 120f;
    /// <summary>측면 계획의 기동 제대 몫 (중대 수 비율) · 협공의 기동 제대 몫.</summary>
    public float FlankShare = 0.34f;
    public float PincerShare = 0.5f;
    /// <summary>제대를 나눌 최소 병력비 (우리 / 보이는 적, 질 가중 없음): 측면 · 협공.</summary>
    public float FlankMinOdds = 1.2f;
    public float PincerMinOdds = 1.8f;
    /// <summary>보이는 기관총 하나가 그 축 점수에서 빼는 값.</summary>
    public float MgWeight = 0.35f;
    /// <summary>고정 제대는 기동 제대 도착 예정이 이 초 안이 될 때까지 적 사거리 밖 (×1.15) 에서 대기, 그 뒤 교전 (사격만) — 혼자 오래 맞지 않게. 0 = 곧바로 교전.</summary>
    public float FixLead = 20f;
    /// <summary>S14 미션 목표 연결: 미션 objectives 에서 과제 (점령 · 고수 · 호송 · 차단) 를 끌어낸다.</summary>
    public bool Objectives = true;
    /// <summary>계획 점수에서 목표 지점에 가까운 축의 가산 (소총 사거리당).</summary>
    public float ObjectiveWeight = 0.3f;
    /// <summary>철수 (목표 포기, 사용자 결정 5): 지시 그룹 병력 비율이 이 아래면 목표를 버리고 물러나 방어. 0 = 끝까지 (미션 aiWithdrawAt 이 덮어씀).</summary>
    public float WithdrawAt = 0f;
    /// <summary>목표 고수 정도 0~1 (0.5 = 프로필 값 그대로): 공격 한계 · 철수 문턱 배수 = 2 × (1 − 값). 미션 aiCommitment 가 덮어씀.</summary>
    public float Commitment = 0.5f;
    /// <summary>3B (3A 남은 일 1): 기동 제대가 도착했는데 고정 제대가 fixPushSec 초 동안 교전을 못 하면 고정 중대를 표적 사거리 ×0.85 까지 밀어 올린다. fixWait = 도착 뒤 돌격 허가까지 최대 대기 (초, 옛 20).</summary>
    public bool FixPush = false;
    public float FixPushSec = 8f;
    public float FixWait = 20f;
    public object Clone() => MemberwiseClone();
}

/// <summary>연대 전술 · 대대 · 중대 실행 층 프로필 (사격과 기동 · 준비사격 · 종심 방어 · 위치 점수 · 돌격 · 후퇴).</summary>
public sealed class TacticsProfile
{
    public float Aggressiveness = 0.5f;
    /// <summary>교착 해소 (stale 2026-10-20): 공격측이 이 초 동안 보이는 적이 없으면 (1단계) 진지 사수 대기 해제 → 2N 예비 투입 → 3N 적 진지 쪽 전면 전진. 방어 과제는 영향 없음. 0 = 끔.</summary>
    public float StalemateSeconds = 30f;
    public float RetreatStrength = 0.67f;
    /// <summary>제압 후퇴 판단 (N25): 중대 평균 제압이 retreatSuppStart 를 넘으면 장교 판단마다 확률 굴림 — 위험률 (초당) = retreatSuppRate × sf × (1 + retreatCasualtyWeight × 사상률/사상 임계),
    /// sf = (제압 − start)/(retreatSuppression − start) (0..2). 장교 지휘 × max(0.3, 1 − retreatXpPerLevel × 경험 단계 − retreatRankPer × (계급 − 1)), 부사관 대리 × retreatNcoMul, 지휘 없음 × retreatLeaderlessMul.</summary>
    public float RetreatSuppression = 0.6f;
    public float RetreatSuppStart = 0.3f;
    public float RetreatSuppRate = 0.10f;
    public float RetreatCasualtyWeight = 1.5f;
    public float RetreatXpPerLevel = 0.1f;
    public float RetreatRankPer = 0.05f;
    public float RetreatNcoMul = 1.5f;
    public float RetreatLeaderlessMul = 1.5f;
    public float Trench = 0.6f;
    public float ChargeRange = 7f;
    public float ChargeSuppression = 0.45f;
    public float ChargeOdds = 1.6f;
    /// <summary>T24 ③ (D08): 공격 돌격이 수 우세 (chargeOdds) 로만 조건을 넘을 때도 표적 둘레 적 평균 제압이 이 이상이어야. 0 = 끔.</summary>
    public float ChargeMinSuppression = 0f;
    public float EngageFrac = 0.6f;   // engage 2026-10-19 (반응 거리 0.7 안), tweak1 0.65 → 0.6
    public bool FireAndMovement = true;
    public float BoundLength = 0.35f;
    public float PrepSeconds = 20f;
    public float LiftDistance = 10f;
    public bool Outposts = true;
    public float OutpostDistance = 0.6f;
    public float Depth = 0.8f;
    public float CoverWeight = 1f;
    public float HighWeight = 0.7f;
    public float LosWeight = 0.8f;
    public float ThreatWeight = 0.8f;
    // ---- 방어 지형 활용 (engage, 2026-10-19 사용자 결정): 방어 과제 = 참호선 → 가옥 · 숲 가장자리 거점 점유, 기관총도 진지로, 후퇴는 다음 엄폐선, 예비는 엄폐 뒤
    /// <summary>방어 위치에서 참호를 찾는 반경 (소총 사거리 배수).</summary>
    public float TrenchSearch = 0.5f;
    /// <summary>참호가 없으면 엄폐 칸 (가옥 옆 · 숲 가장자리 · 잔해 · 포탄 구덩이) 거점을 찾아 점유.</summary>
    public bool DefendCover = true;
    /// <summary>거점으로 치는 엄폐 칸의 명중 배수 상한 (숲 0.7 · 잔해 0.5 · 가옥 옆 0.8 · 포탄 구덩이 0.8 · 참호 0.6).</summary>
    public float CoverMaxHit = 0.85f;
    /// <summary>방어 중대 간격 상한 (중대 정면 폭, 소총 사거리 배수) — 이웃 중대 중심 거리 ≈ 이 값 × 사거리 + 2칸 ≤ 유효 사거리 (상호 지원). 0 = 제한 없음.</summary>
    public float DefendSpacing = 0.65f;
    /// <summary>소총 중대 전술 틀 사용 (방어 = 종심 방어, 공격 = 화력-기동 — CombatFormations.cs, 1단계 정지 배치).</summary>
    /// <summary>N32: 공격 과제 소총 중대가 보이는 적 참호선을 만나면 참호 돌격 순서 (TrenchAssault.cs) 를 쓴다. 수치는 tactics.json trenchAssault (ai*). 측정용 겹침 "notrenchassault".</summary>
    public bool TrenchAssault = true;
    public bool Templates = true;    // 기본 켜짐 (사용자 결정 2026-10-19, N4 2단계 행동)
    // ---- 3단계 B (방어 · 예비 · 역습, docs/TACTICAL_AI_STAGE3_REVIEW_2026-10-19.md S3 · S4 · S8 — TacticalAi.Reserve3B.cs). 2단계 · 3A 와 비교 = 겹침 "no3b".
    /// <summary>저탄약 교대: 대대 예비 중대 (탄약 ≥ ammoRotateReady) 와 탄약 평균이 ammoRotateAt 아래인 전선 중대를 맞바꾼다 (나가는 중대는 예비 위치에서 회복). 대대마다 ammoRotateCooldown 초에 한 번.</summary>
    public bool AmmoRotate = true;
    public float AmmoRotateAt = 0.3f;
    public float AmmoRotateReady = 0.6f;
    public float AmmoRotateCooldown = 40f;
    /// <summary>S3 사건 투입 (방어 과제): 전선 중대 붕괴 (틈) · 주저항선을 넘은 적 (돌파 — 국지 역습) · 측면 끝의 적 → 예비 중대를 그 지점으로 (plugSeconds 뒤 전선 합류).</summary>
    public bool ReserveEvents = true;
    public float PlugSeconds = 40f;
    /// <summary>S3 예비 재지정: 예비를 투입한 대대가 reserveCalm 초 동안 조용하면 (사거리 ×1.5 안 적 없음) 가장 성한 중대를 다시 예비로 뺀다.</summary>
    public bool ReserveRedesignate = true;
    public float ReserveCalm = 30f;
    /// <summary>S8 역습 병력비: 연대 예비 대대 역습 = 예비 ≥ 표적 둘레 적 × counterOdds (제압된 적이면 × 0.6). 0 = 2단계 문턱 (돌파 1/1.2 · 붕괴 1.25).</summary>
    public float CounterOdds = 0f;
    /// <summary>S4 진지 히스테리시스: 방어 중대가 진지를 옮기는 최소 거리 (소총 사거리 배수, 하한 8칸). 0 = 8칸 (옛 값).</summary>
    public float DefendRepick = 0f;
    public object Clone() => MemberwiseClone();
}

/// <summary>ai_profiles.json 읽기 (처음 쓸 때 한 번, 실패하면 내장 기본값). 프로필은 "default" 위에 덮어쓴다.</summary>
public static class AiProfiles
{
    static readonly object gate = new();
    static Dictionary<string, AiProfile>? profiles;
    static Dictionary<string, string> aliases = new();
    static Dictionary<string, JsonObject> raw = new(StringComparer.OrdinalIgnoreCase);   // 파일에 적힌 그대로 (겹침용)

    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        IncludeFields = true,
    };

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "assets", "data", "ai_profiles.json");

    /// <summary>이름 목록 (테스트 · 표시용, 정렬).</summary>
    public static IReadOnlyList<string> Names { get { Ensure(); return profiles!.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(); } }

    /// <summary>파일을 다시 읽는다 (테스트용 · 경로 지정 가능).</summary>
    public static void Load(string? path = null)
    {
        lock (gate)
        {
            var result = new Dictionary<string, AiProfile>(StringComparer.OrdinalIgnoreCase);
            var al = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rw = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
            path ??= DefaultPath;
            if (File.Exists(path))
            {
                var root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })?.AsObject()
                           ?? throw new InvalidDataException("ai_profiles.json: 빈 파일");
                if (root["aliases"] is JsonObject ao)
                    foreach (var (k, v) in ao) if (v != null) al[k] = v.GetValue<string>();
                var ps = root["profiles"] as JsonObject ?? new JsonObject();
                var baseNode = ps["default"] as JsonObject ?? new JsonObject();
                var def = baseNode.Deserialize<AiProfile>(Opts) ?? new AiProfile();
                def.Name = "default";
                result["default"] = def;
                foreach (var (name, node) in ps)
                {
                    if (node is JsonObject ro) rw[name] = (JsonObject)ro.DeepClone();
                    if (name == "default" || node is not JsonObject po) continue;
                    var merged = JsonNode.Parse(baseNode.ToJsonString())!.AsObject();
                    foreach (var (k, v) in po)
                    {
                        // 절 (strategy · tactics) 은 키 단위로 덮어쓴다 — 빠진 값 = default 값
                        if (v is JsonObject sec && merged[k] is JsonObject baseSec)
                            foreach (var (k2, v2) in sec) baseSec[k2] = v2?.DeepClone();
                        else merged[k] = v?.DeepClone();
                    }
                    var p = merged.Deserialize<AiProfile>(Opts) ?? new AiProfile();
                    p.Name = name;
                    result[name] = p;
                }
            }
            if (!result.ContainsKey("default")) result["default"] = new AiProfile();
            profiles = result;
            aliases = al;
            raw = rw;
        }
    }

    static void Ensure()
    {
        if (profiles != null) return;
        try { Load(); }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or InvalidOperationException)
        {
            Console.Error.WriteLine("ai_profiles.json 읽기 실패 (내장 기본값 사용): " + e.Message);
            lock (gate) { profiles = new(StringComparer.OrdinalIgnoreCase) { ["default"] = new AiProfile() }; aliases = new(); raw = new(StringComparer.OrdinalIgnoreCase); }
        }
    }

    /// <summary>[측정 도구 전용] 읽어 둔 모든 프로필을 메모리에서 고친다 (TacticsObs KD_OBS_VARIANT — 파일 · 정상 게임은 영향 없음).</summary>
    public static void Tweak(Action<AiProfile> f) { Ensure(); lock (gate) foreach (var k in profiles!.Keys.OrderBy(x => x, StringComparer.Ordinal)) f(profiles[k]); }

    /// <summary>
    /// 이름 (또는 별칭 · 세력 영문 id) → 프로필 복사본. 없으면 null.
    /// 겹침 (stage 3A): "바탕+겹침[+겹침…]" = 바탕 프로필 위에 겹침 프로필이 파일에 직접 적은 키만 덮어쓴다 (예: korean+stage2 = 한국군 값 + 3단계 끔).
    /// 난이도 · 비교 실험이 같은 길을 쓴다. 이름은 그대로 저장되지 않고 결과 값이 저장된다 (스냅샷).
    /// </summary>
    public static AiProfile? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Ensure();
        name = name.Trim();
        if (name.Contains('+'))
        {
            var parts = name.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || Find(parts[0]) is not { } baseP) return null;
            for (int i = 1; i < parts.Length; i++)
            {
                string on = aliases.TryGetValue(parts[i], out var oa) ? oa : parts[i];
                if (!raw.TryGetValue(on, out var node)) return null;
                baseP = Overlay(baseP, node);
            }
            baseP.Name = string.Join("+", parts);
            return baseP;
        }
        if (aliases.TryGetValue(name, out var a)) name = a;
        return profiles!.TryGetValue(name, out var p) ? p.Clone() : null;
    }

    /// <summary>겹침: p 의 값을 JSON 으로 바꿔 node 가 적은 키만 덮어쓰고 다시 읽는다.</summary>
    static AiProfile Overlay(AiProfile p, JsonObject node)
    {
        var merged = JsonSerializer.SerializeToNode(p, Opts)!.AsObject();
        foreach (var (k, v) in node)
        {
            string key = merged.Select(kv => kv.Key).FirstOrDefault(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase)) ?? k;
            if (v is JsonObject sec && merged[key] is JsonObject baseSec)
                foreach (var (k2, v2) in sec)
                {
                    string key2 = baseSec.Select(kv => kv.Key).FirstOrDefault(x => string.Equals(x, k2, StringComparison.OrdinalIgnoreCase)) ?? k2;
                    baseSec[key2] = v2?.DeepClone();
                }
            else merged[key] = v?.DeepClone();
        }
        var r = merged.Deserialize<AiProfile>(Opts) ?? p.Clone();
        r.Name = p.Name;
        return r;
    }

    /// <summary>고르는 순서: 지정 이름 &gt; 지도 세력 aiProfile &gt; 세력 영문 id &gt; default.</summary>
    public static AiProfile Resolve(string? requested, string? mapProfile, string? factionEn)
    {
        var p = Find(requested) ?? Find(mapProfile) ?? Find(factionEn) ?? Find("default")!;
        // [측정 전용] KD_AI_OVERLAY=<겹침> — 모든 세력 프로필에 같은 겹침 (예 no3b: 같은 실행 파일로 3B 끔 비교). 정상 게임은 설정하지 않는다.
        if (Environment.GetEnvironmentVariable("KD_AI_OVERLAY") is { Length: > 0 } ov && Find(p.Name + "+" + ov) is { } po) p = po;
        return p;
    }
}
