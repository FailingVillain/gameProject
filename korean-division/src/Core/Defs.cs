using System.Text.Json;
using System.Text.Json.Serialization;

namespace KoreanDivision.Core;

/// <summary>
/// 밸런스 데이터 정의 (assets/data/balance.json). 키(key)는 JSON 객체의 이름이며 로드 시 채워진다.
/// 새 유닛/건물/무기는 JSON 에만 추가하면 된다 (명령 버튼 · 단축키 · 툴팁 자동 생성).
/// </summary>
public sealed class WeaponDef
{
    [JsonIgnore] public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public float Damage { get; set; }
    /// <summary>탄약 종류 (노식 · 일식 · 구식 일본 · 독식 · 불식 소총탄). 자료만 — 보급 규칙은 나중.</summary>
    public string? AmmoType { get; set; }
    public float Range { get; set; }
    public float Cooldown { get; set; } = 1;
    /// <summary>발사 간격 범위(초): 사격마다 [min, max] 사이 결정적 난수 (ROADMAP §2-3). 둘 다 0 이면 Cooldown 고정.</summary>
    public float CooldownMin { get; set; }
    public float CooldownMax { get; set; }
    /// <summary>발사 간격이 난수인가.</summary>
    [JsonIgnore] public bool RandomCooldown => CooldownMax > CooldownMin && CooldownMax > 0;
    /// <summary>평균 발사 간격 (UI · 밸런스 도구).</summary>
    [JsonIgnore] public float CooldownMean => RandomCooldown ? (CooldownMin + CooldownMax) * 0.5f : Cooldown;
    public float Accuracy { get; set; } = 1;   // 명중률 0..1 (곡선이 없을 때)
    /// <summary>대상 카테고리(유닛 category) → 피해 배수. 예: 바주카 { "armored": 3.5 }.</summary>
    public Dictionary<string, float>? Bonus { get; set; }
    /// <summary>우선 표적 병과 (예: 대전차소총 "armored"): 사거리 안 · 보이는 · 사선이 트인 그 병과 표적을 다른 표적보다 먼저 노린다.</summary>
    public string? PreferCategory { get; set; }
    /// <summary>이 병과에 피해를 줄 수 있나 (bonus 배수 0 = 면역).</summary>
    public bool CanDamage(string? category) => BonusAgainst(category) > 0;
    /// <summary>기갑 표적에 보너스(&gt;1)가 있는 대전차 무기인가.</summary>
    [JsonIgnore] public bool IsAntiArmor => Bonus != null && Bonus.Values.Any(m => m > 1f);
    float aimTime = 0.25f, holdTime = 0.25f;
    /// <summary>조준 시간(초): 표적이 사거리에 들어오고 멈춘 뒤 첫 발까지 (같은 표적을 이미 조준하고 서 있으면 없음).</summary>
    public float AimTime { get => aimTime; set { aimTime = value; AimTicks = Ticks(value); } }
    /// <summary>사격 후 정지 시간(초): 표적이 사거리를 벗어나거나 사라진 뒤, 이동 · 추격을 시작하기 전 대기.</summary>
    public float HoldTime { get => holdTime; set { holdTime = value; HoldTicks = Ticks(value); } }
    [JsonIgnore] public int AimTicks { get; private set; } = 15;
    [JsonIgnore] public int HoldTicks { get; private set; } = 15;
    static int Ticks(float sec) => Math.Max(0, (int)MathF.Round(sec / Config.SimDt));
    public float BonusAgainst(string? category) => category != null && Bonus != null && Bonus.TryGetValue(category, out var m) ? m : 1f;

    // ---- v0.4 (docs/ROADMAP_v0.4.md §1 · §2 · §5)
    /// <summary>탄약 칸 (사격 1회 = 1). 0 = 무한. 비전투 중 서서히 회복 (balance.json "supply", Supply.cs).</summary>
    public int Ammo { get; set; }
    /// <summary>거리별 명중률 곡선: 밀착 accNear → 최대 사거리 accFar, 거리 제곱에 비례해 감소 (World.AccuracyCurve). accNear 0 이면 Accuracy 고정.</summary>
    public float AccNear { get; set; }
    public float AccFar { get; set; }
    /// <summary>점사 (경기관총 lmg): 한 점사 발수 [burstMin, burstMax] (사격마다 점사 시작 때 결정적 난수), 점사 사이 휴지 [burstPauseMin, burstPauseMax] 초. burstMax 0 = 지속 사격 (Bursts.cs).</summary>
    public int BurstMin { get; set; }
    public int BurstMax { get; set; }
    public float BurstPauseMin { get; set; }
    public float BurstPauseMax { get; set; }
    [JsonIgnore] public bool Bursty => BurstMax > 0;
    /// <summary>사격 종류 "direct"(직사: 단일 표적, 선택적 사선 검사) | "indirect"(곡사: 범위 포격, 장애물 무시). 생략하면 splash 가 있을 때 indirect.</summary>
    public string? Fire { get; set; }
    [JsonIgnore] public bool Indirect => Fire != null ? Fire == "indirect" : Splash > 0;
    /// <summary>직사 무기의 사선 검사: 사격자 → 표적 직선이 막힌 칸(건물 · 지형)을 지나면 쏘지 못한다 (GameMap.HasLineOfSight).</summary>
    public bool Los { get; set; }
    /// <summary>최소 사거리 (포병).</summary>
    public float MinRange { get; set; }
    /// <summary>범위 피해 반경(칸, 0 = 단일 표적) · 가장자리 피해 비율.</summary>
    public float Splash { get; set; }
    public float SplashEdge { get; set; } = 0.3f;
    /// <summary>착탄 퍼짐: 최대 사거리에서의 오차 반경(칸). 거리별 곡선 = World.ScatterAt (combat.scatterNearFrac · scatterQuad). 직사포 = 빗나간 탄 · 지점 포격.</summary>
    public float Scatter { get; set; }
    /// <summary>포탄 구덩이: 착탄 1회마다 착탄 칸이 crater 지형이 될 확률 (0 = 없음, 결정적 Roll).</summary>
    public float CraterChance { get; set; }
    /// <summary>자기 시야 밖(아군 관측) 표적에 대한 추가 조준 시간(초).</summary>
    public float SpotterDelay { get; set; }
    // ---- 교전 개편 2차 (ROADMAP §2-4b · §2-4e)
    /// <summary>공격 1회가 표적(포격 · 흩뿌림은 반경 안 전원)에 주는 제압 증가량 (명중 여부 무관). 0 = 없음.</summary>
    public float Suppress { get; set; }
    /// <summary>흩뿌림 반경(칸, 기관총): 착탄점 주변 이 반경 안의 적이 대신 맞을 수 있고 전원 제압. 0 = 단일 표적.</summary>
    public float Spray { get; set; }
    // ---- 교전 4차 (ROADMAP 추가 병종 · 포병)
    /// <summary>자동 표적 탐색에서 장교를 먼저 노린다 (저격수).</summary>
    public bool PreferOfficer { get; set; }
    /// <summary>반응 거리 비율 (engage, 2026-10-19): 자동 표적 · 사수 표적 탐색 · 접근 거리 = 사거리 × 이 값. 0 = combat.engageRangeFrac (전역). 기관총 · 저격총 · 근거리 무기는 1.0 (사거리 끝까지).</summary>
    public float EngageFrac { get; set; }
    /// <summary>장교 또는 정찰병(unit.spotter)이 지금 보고 있는 표적에만 사격 (곡사포).</summary>
    public bool SpotterOnly { get; set; }
    /// <summary>기관단총 (행진 사격 보정이 다르다).</summary>
    public bool Submachine { get; set; }
    // ---- 포탄 비행 시간 (ShellFlight.cs): 비행(초) = clamp(flightBase + 거리 / shellSpeed, flightMin, flightMax). shellSpeed 0 = 즉발.
    /// <summary>포탄 속도 (칸/초). 0 = 즉발 (비행 없음).</summary>
    public float ShellSpeed { get; set; }
    /// <summary>비행 시간 기본값 (초, 거리와 무관한 몫 — 곡사 탄도).</summary>
    public float FlightBase { get; set; }
    /// <summary>비행 시간 하한 · 상한 (초). flightMax 0 = 상한 없음.</summary>
    public float FlightMin { get; set; }
    public float FlightMax { get; set; }
    /// <summary>방열 사계 (도, 전체 폭): 방열한 대포는 방열 때 정한 중심 ± 절반 안만 쏜다. 밖이면 방열한 채 포가 회전 (옮길 각 < combat.traverseRedeployDeg) 또는 철수 → 회전 → 재방열 (Combat2.cs CanTraverseTo · ShiftTraverse). 0 = 제한 없음.</summary>
    public float TraverseArc { get; set; }
    // ---- arty3 포병 · 중화기 묶음 (사용자 2026-10-21, ROADMAP_LONG 3 · 9 · 14)
    /// <summary>직사 + 간접 (야전포): 사선이 막혔거나 직사 표적이 없을 때, 아군 관측 유닛 (장교 · 정찰병) 이 보는 사거리 안 표적에 간접 사격 (퍼짐 = ScatterAt, 사선 무시).</summary>
    public bool SpotterIndirect { get; set; }
    /// <summary>곡사포 조정 사격: 같은 표적 구역 연속 사격 시 퍼짐이 발마다 줄어 이 발째에 adjustFireMinMul 배 (0 = 끔). 구역 = 앞 조준점에서 adjustFireZone 칸 안, 벗어나면 초기화.</summary>
    public int AdjustFireShots { get; set; }
    public float AdjustFireMinMul { get; set; } = 1f;
    public float AdjustFireZone { get; set; }
    /// <summary>최후 저지선 (사격 통제 K 확장) 을 쓸 수 있는 무기 (중기관총).</summary>
    public bool FinalLine { get; set; }
}

public sealed class ArmorDef
{
    [JsonIgnore] public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public float Armor { get; set; }
}

public sealed class UnitDef
{
    [JsonIgnore] public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float Hp { get; set; } = 40;
    public float Speed { get; set; } = 3;
    public float Radius { get; set; } = 0.35f;
    public int Sight { get; set; } = 8;
    // ---- v0.4 (ROADMAP §2 · §3 · §4 · §1)
    /// <summary>유닛 기본 명중률 배수 (정규군 = 1.0).</summary>
    public float Accuracy { get; set; } = 1;
    /// <summary>유효 시야 (평소) / 최대 시야 (경계 시). 0 이면 Sight.</summary>
    public int SightNear { get; set; }
    public int SightFar { get; set; }
    /// <summary>은폐 계수 0~100 (탐지 확률 = 1 − 은폐/100).</summary>
    public float Concealment { get; set; }
    /// <summary>포탑 회전 속도 (도/초, 0 = 포탑 없음).</summary>
    public float TurretTurnRate { get; set; }
    /// <summary>차체 회전 속도 (도/초, 0 = 기본 — 사실상 즉시). 견인포처럼 느리게 도는 유닛만 지정 (이동 · 조준 · 정렬 회전 공통).</summary>
    public float TurnRate { get; set; }
    /// <summary>차체 회전 한계 (라디안/초): TurnRate 지정 시 그 값, 아니면 기본 14 rad/s.</summary>
    [JsonIgnore] public float TurnRadPerSec => TurnRate > 0 ? TurnRate * (MathF.PI / 180f) : 14f;
    /// <summary>방열 중 · 방열 완료 상태의 조준 회전 속도 (도/초, 0 = TurnRate 와 같음). 경기관총: 거치한 채 모래주머니 흉벽 뒤에서 천천히 돌려 겨눈다 (N2, 2026-10-19).</summary>
    public float TraverseRate { get; set; }
    /// <summary>방열 상태 회전 한계 (라디안/초): TraverseRate 지정 시 그 값, 아니면 TurnRadPerSec.</summary>
    [JsonIgnore] public float TraverseRadPerSec => TraverseRate > 0 ? TraverseRate * (MathF.PI / 180f) : TurnRadPerSec;
    /// <summary>방열 시간(초, 사격 전) · 철수 시간(초, 이동 전). 0 = 없음 (견인포 §5.1).</summary>
    public float SetupTime { get; set; }
    public float PackTime { get; set; }
    /// <summary>bal2: 방열 완료 뒤 최소 대기 (초, 0 = 없음) · 중대 깃발 목표가 이 거리(칸) 안이면 계속 방열 유지.</summary>
    public float MinDeployStay { get; set; }
    public float StayLeaveDist { get; set; }
    public string? Weapon { get; set; }
    public string? Armor { get; set; }
    // ---- 교전 개편 2차 (ROADMAP §2-4b · §2-4c)
    /// <summary>근접 무기 키 (착검 · 곡도). 있으면 돌격 명령 · 자동 돌격 가능.</summary>
    public string? Melee { get; set; }
    /// <summary>제압 증가 배수 (징집 1.0 · 정규 0.75 · 정예 0.5 · 장교 0 = 면역).</summary>
    public float SuppressResist { get; set; } = 1;
    /// <summary>장교 지휘 범위(칸). 0 = 장교 아님. 범위 안 같은 세력 병사: 제압 감소 가속 · 사격 간격 단축. 장교는 계급 없음.</summary>
    public float CommandRadius { get; set; }
    [JsonIgnore] public bool IsOfficer => CommandRadius > 0;
    public bool CanCapture { get; set; }
    /// <summary>보급 마차 적재량 (탄약 '한 짐' 단위 = 병사 1명의 탄약 한도 전체). 0 = 보급 마차 아님. 주변 아군의 탄약 회복을 가속하고 그만큼 줄어든다 (Supply.cs).</summary>
    public float Cargo { get; set; }
    [JsonIgnore] public bool IsSupplyWagon => Cargo > 0;
    public bool AutoAttack { get; set; } = true;
    /// <summary>병과: infantry | armored | gun | wagon (무기 bonus 판정). 보병만 참호를 판다.</summary>
    public string Category { get; set; } = "infantry";
    public bool IsArmored => Category == "armored";
    public bool IsInfantry => Category == "infantry";
    /// <summary>중화기 (경기관총 · 맥심 · 대전차소총 · 보병포 · 야전포 · 곡사포 — 유닛 키 heavy_* 또는 병과 gun): 보병보다 앞으로 나가지 않는다 (HeavyPacing.cs).</summary>
    [JsonIgnore] public bool Heavy => Key.StartsWith("heavy_", StringComparison.Ordinal) || Category == "gun";
    /// <summary>지도의 참호 지형을 자동 점유하는 유닛 (보병, 기병 · 정찰병 제외 — Trenches.cs).</summary>
    [JsonIgnore] public bool CanOccupyTrench => IsInfantry && !Mounted && !Scout;
    // ---- 교전 4차 (ROADMAP 기병 · 수류탄 · 행진 사격 · 곡사포 관측)
    /// <summary>수류탄 보유 수 (시작 · 최대). 0 = 없음. grenade 규칙은 balance.json "grenade".</summary>
    public int Grenades { get; set; }
    /// <summary>한국군 전용 유닛 (N21): 미션 로드 시 en = "korean" 이 아닌 세력의 편성에 있으면 오류 (MissionDef.Validate).</summary>
    public bool KoreaOnly { get; set; }
    /// <summary>[N26] 세력 제한 목록 (map factions[].en, 대소문자 무시). 있으면 이 세력만 편성 가능 (koreaOnly 의 일반화: koreaOnly = ["korean"]). 예: 마적 유닛 ["bandit"].</summary>
    public string[]? Factions { get; set; }
    /// <summary>[N53] 공용(무소속) 유닛: 어떤 세력도 편성할 수 있다 (koreaOnly · factions 보다 우선). 간도 한인 의용병 · 몽골 기병대 등.</summary>
    public bool Common { get; set; }
    /// <summary>[N26] 이 유닛의 전용 그림이 테마에 없을 때 대신 쓸 유닛 키 (artVariant 가 있으면 그 진영 변형 우선). 표시 전용.</summary>
    public string? ArtAs { get; set; }
    public string? ArtVariant { get; set; }
    /// <summary>[N26] 근접 전용 (마적 잡병): 생성 시 '돌격만' 켜짐 — 사격하지 않고 근접 무기로만 싸운다. 주무기는 근접 무기 키 (사거리 0.3).</summary>
    public bool MeleeOnly { get; set; }
    /// <summary>[N54] 포격 · 수류탄 (범위 피해) 받는 피해 배수 (철모 0.85). 소총 · 기관총 · 근접에는 적용 안 함. 기본 1.</summary>
    public float ShellDamageMul { get; set; } = 1f;
    /// <summary>[assaultinf] 제압 감소 배수 (돌격보병 1.5). 기본 1.</summary>
    public float SuppressRecoverMul { get; set; } = 1f;
    /// <summary>[assaultinf] 이동 중 (지난 틱에 걸음) 직사 피격 명중 배수 (돌격보병 0.75). 기본 1.</summary>
    public float MovingHitMul { get; set; } = 1f;
    /// <summary>[assaultinf] 포탄 착탄점 중심에서 이 반경 (칸, 몸 중심 기준) 안이면 체력과 무관하게 사망. 0 = 끔.</summary>
    public float ShellCenterKillRadius { get; set; }
    /// <summary>[assaultinf] 돌격 접근 중 이 반경 (칸) 안 엄폐 칸을 경유해 약진 (ChargeHop.cs). 0 = 끔.</summary>
    public float ChargeHopRange { get; set; }
    /// <summary>[assaultinf] 약진 엄폐 칸 최대 명중 배수 · 적과 이 거리 (칸) 이하면 약진 없이 곧장 · 한 번 약진 상한 (초).</summary>
    public float ChargeHopMaxHit { get; set; } = 0.85f;
    public float ChargeHopMinDist { get; set; } = 9f;
    public float ChargeHopMaxSec { get; set; } = 4f;
    /// <summary>세력 en 이 이 유닛을 편성할 수 있는가 (미션 검증 · 편성 UI 공통).</summary>
    public bool AllowedFor(string? en)
    {
        if (Common) return true;
        if (KoreaOnly && !string.Equals(en, "korean", StringComparison.OrdinalIgnoreCase)) return false;
        return Factions == null || Factions.Any(f => string.Equals(f, en, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>중대 돌격 사격 기본값 ON (돌격보병, N21). 중대 편성 시 구성원 과반이 이 유닛이면 켜진 채 시작, 전술 AI 도 항상 켠다.</summary>
    public bool MarchFireDefault { get; set; }
    /// <summary>처음부터 착검됨 (일본군, progbar) — 돌격 명령에도 착검 멈춤 없음.</summary>
    public bool BayonetPreFixed { get; set; }
    /// <summary>행진 사격 명중 배수 (0 = combat.marchAccMul / 기관단총 marchSmgAccMul). 클수록 페널티가 작다.</summary>
    public float MarchAccMul { get; set; }
    /// <summary>행진 사격 사격 간격 배수 (0 = combat.marchIntervalMul / marchSmgIntervalMul).</summary>
    public float MarchIntervalMul { get; set; }
    /// <summary>행진 사격 중 이동 속도 배수 (0 = combat.marchMoveMul). 돌격보병 (N23) = 걸으며 쏘기가 기본이라 손해가 작다.</summary>
    public float MarchMoveMul { get; set; }
    /// <summary>돌격보병 (N23): 행진 사격 중 단검 돌격 직전 수류탄을 던지는 병사 비율 (병사 Id · 시드 해시로 고정, 1 = 전원).</summary>
    public float MarchGrenadeFrac { get; set; } = 1f;
    /// <summary>근접 보조 무기 (N21): 표적이 sidearmRange 안이면 이 무기로 쏘고, 밖이면 주무기. 매 틱 거리로 결정 (히스테리시스 0.5칸) — 상태 추가 없음 (Unit.Weapon 이 이미 저장 · 체크섬).</summary>
    public string? Sidearm { get; set; }
    public float SidearmRange { get; set; }
    /// <summary>기병 (말 탄 보병 계열): 참호 · 속보 불가, 돌격 충격 이벤트 가능 (피해 보너스 없음), 행진 사격 없음.</summary>
    public bool Mounted { get; set; }
    /// <summary>승마 사격 명중 배수 (0 = combat.mountedFireAccMul). 몽골 기병 등 말 위 사격에 능한 병종 자리 (N61).</summary>
    public float MountedFireAccMul { get; set; }
    /// <summary>돌격 속도 배수 (0 = combat.chargeSpeedMul).</summary>
    public float ChargeSpeedMul { get; set; }
    /// <summary>스태미나 한도 배수 (기병 3).</summary>
    public float StaminaMul { get; set; } = 1;
    /// <summary>행진 사격 가능 (보병 계열 · 기관총형 전차).</summary>
    public bool MarchFire { get; set; }
    /// <summary>곡사포 관측 유닛 (장교 · 정찰병): 이 유닛이 보는 표적에만 SpotterOnly 무기가 쏜다.</summary>
    public bool Spotter { get; set; }
    /// <summary>정찰병 역할 (시야 × vision.scoutSightMul, 중대 정찰조 자율 행동 — Scouts.cs).</summary>
    public bool Scout { get; set; }
    /// <summary>[N68] 기수 (연대기): 반경 combat.bearerRadius 안 같은 세력 병사의 붕괴 · AI 제압 후퇴 확률 감소, 쓰러지면 잠시 오히려 증가 (Bearer.cs).</summary>
    public bool Bearer { get; set; }
    /// <summary>[N72] 하마 관측: 내린 기병 정찰병의 시야 배수 (0 = 1, 변화 없음).</summary>
    public float DismountSightMul { get; set; }
}

/// <summary>도시 · 점령지 정의 (방향 변경 §0-1: 건설 · 생산 · 수입 없음 — 지도에 놓이는 점령 대상만).</summary>
public sealed class BuildingDef
{
    [JsonIgnore] public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float Hp { get; set; } = 500;
    public string? Armor { get; set; }
    public int Size { get; set; } = 2;
    public int Sight { get; set; } = 6;
    public bool Headquarters { get; set; }
    public bool Capturable { get; set; }
    public bool Indestructible { get; set; }
    /// <summary>도시 (대도시 · 소도시): 하나라도 가지고 있으면 패배하지 않는다, 근처(supply.cityRadius) 아군 탄약 회복 가속.</summary>
    public bool City { get; set; }
    /// <summary>점령 시간 (초, 유닛 1기 기준). 0 = capture.time 사용.</summary>
    public float CaptureTime { get; set; }
}

/// <summary>시작 설정 (balance.json "start"): 위치 공개 지연, 지도에 유닛 배치가 없을 때 세력마다 주는 기본 병력.</summary>
public sealed class StartDef
{
    public float RevealDelay { get; set; } = 30;
    /// <summary>기본 병력: 한 항목 = 한 묶음 (유닛 키 → 수). company=true 면 그 묶음을 중대로 편성 (battalion = 대대 번호, 0 = 대대 없음).</summary>
    public List<StartGroupDef> Force { get; set; } = new();
}

public sealed class StartGroupDef
{
    public Dictionary<string, int> Units { get; set; } = new();
    public bool Company { get; set; }
    public int Battalion { get; set; }
}

/// <summary>탄약 회복 (balance.json "supply", ROADMAP §0-1): 비전투 regenDelay 초 뒤 한도의 regenPct/초, 아군 도시 cityRadius 칸 안 · 보급 마차 wagonRadius 칸 안이면 ×mul.</summary>
public sealed class SupplyRulesDef
{
    public float RegenDelay { get; set; } = 10;
    public float RegenPct { get; set; } = 0.01f;
    public float Interval { get; set; } = 1;
    public float CityRadius { get; set; } = 8;
    public float CityMul { get; set; } = 3;
    public float WagonRadius { get; set; } = 6;
    public float WagonMul { get; set; } = 3;
    /// <summary>보급 마차가 아군 도시 cityRadius 안에서 다시 싣는 속도 (짐/초).</summary>
    public float WagonRefill { get; set; } = 1;
}

/// <summary>
/// 지형 규칙 (balance.json "terrain", ROADMAP '지형 · 높이'): 칸 지형별 이동 배수 · 엄폐 (맞을 확률 × · 받는 제압 ×), 고지 · 경사 · 철조망 멈칫.
/// 엄폐는 겹치지 않는다 — 지형 · 가옥 옆 중 가장 좋은 (작은) 값 하나.
/// </summary>
public sealed class TerrainRulesDef
{
    public float ForestMove { get; set; } = 0.7f;
    public float ForestHit { get; set; } = 0.7f;
    public float ForestSuppress { get; set; } = 0.8f;
    /// <summary>숲 가장자리에서 이 깊이(칸)까지만 보이고 쏠 수 있다. 더 깊은 숲 칸은 시야 · 사선을 막는다.</summary>
    public int ForestSeeDepth { get; set; } = 2;
    public float RubbleMove { get; set; } = 0.6f;
    public float RubbleHit { get; set; } = 0.5f;
    public float RubbleSuppress { get; set; } = 0.6f;
    /// <summary>민간 가옥 (진입 불가) 에 붙은 칸 (8방향 이웃) 의 엄폐.</summary>
    public float HouseAdjHit { get; set; } = 0.7f;
    public float HouseAdjSuppress { get; set; } = 0.7f;
    public float WireMove { get; set; } = 0.3f;
    /// <summary>철조망 칸에서 걷는 보병 · 기병: wireHesitateInterval 초마다 확률 wireHesitateChance 로 wireHesitateMin~Max 초 멈칫 (장갑 차량 제외).</summary>
    public float WireHesitateChance { get; set; } = 0.35f;
    public float WireHesitateMin { get; set; } = 0.3f;
    public float WireHesitateMax { get; set; } = 0.8f;
    public float WireHesitateInterval { get; set; } = 1.0f;
    public float TrenchMove { get; set; } = 1.0f;
    public float TrenchHit { get; set; } = 0.5f;
    public float TrenchSuppress { get; set; } = 0.5f;
    /// <summary>참호 자동 점유 (Trenches.cs): 참호 칸 하나에 들어가는 병사 수 (1~4).</summary>
    public int TrenchPerTile { get; set; } = 2;
    /// <summary>이동 명령의 대형 슬롯을 이 거리 (칸) 안의 빈 참호 자리로 옮긴다 (0 = 끔).</summary>
    public float TrenchSnapRadius { get; set; } = 2f;
    /// <summary>걷다가 참호 칸을 밟은 보병이 이 거리 (칸) 안의 빈 자리를 찾아 들어간다.</summary>
    public float TrenchCaptureRadius { get; set; } = 1.5f;
    /// <summary>포탄 구덩이 (포격 착탄으로 생기는 지형): 가벼운 엄폐 · 약간 느림.</summary>
    public float CraterMove { get; set; } = 0.85f;
    public float CraterHit { get; set; } = 0.8f;
    public float CraterSuppress { get; set; } = 0.8f;
    /// <summary>고지: 시야 ×, 고지 → 평지 사격 명중 ×, 평지 → 고지 ×. 경사 칸 (평지와 맞닿은 고지 칸) 이동 ×.</summary>
    public float HighSightMul { get; set; } = 1.25f;
    public float HighToLowHit { get; set; } = 1.15f;
    public float LowToHighHit { get; set; } = 0.85f;
    public float SlopeMove { get; set; } = 0.8f;
}

/// <summary>
/// 지형 시야 (balance.json "vision"): 굵은 칸 묶음 (blockSize × blockSize 타일) 위 shadowcasting. 역할별 시야 배수 (장교 쌍안경 · 정찰병).
/// </summary>
public sealed class VisionDef
{
    /// <summary>시야 계산 칸 묶음 한 변 (타일). 1 = 타일 단위 (정밀 · 느림), 2 = 기본.</summary>
    public int BlockSize { get; set; } = 2;
    /// <summary>false 면 지형 시야 차단 없이 예전 원형 시야.</summary>
    public bool TerrainBlocking { get; set; } = true;
    public float OfficerSightMul { get; set; } = 1.25f;
    /// <summary>정찰병 (unit "scout": true) 시야 배수.</summary>
    public float ScoutSightMul { get; set; } = 1.5f;
}

/// <summary>정찰병 자율 행동 수치 (balance.json "scoutAi", Scouts.cs).</summary>
public sealed class ScoutAiDef
{
    public float AheadMin { get; set; } = 6;
    public float AheadMax { get; set; } = 10;
    /// <summary>정찰조 안 가로 간격 (칸).</summary>
    public float Spread { get; set; } = 5;
    /// <summary>중대가 행군 중일 때 기준점을 더 앞에 두는 칸 수 (건너뛰기 주기 동안 중대가 따라오는 몫).</summary>
    public float MarchLead { get; set; } = 2;
    /// <summary>관측 지점 탐색 반경 (칸, 정수).</summary>
    public int SearchRadius { get; set; } = 3;
    public int ReturnSuppression { get; set; } = 400;
    public int ResumeSuppression { get; set; } = 200;
    public float ReturnHpFrac { get; set; } = 0.5f;
    public float ReportSec { get; set; } = 5;
    public float MaxDist { get; set; } = 15;
    public float CooldownSec { get; set; } = 30;
    public float FollowDist { get; set; } = 8;
    public float ArriveDist { get; set; } = 4;
    public int PerCompany { get; set; } = 2;
    public float AttachRadius { get; set; } = 40;
    /// <summary>N22 피격 엄폐: 정찰병 (중대 정찰조 · 정찰병만의 중대 = 관측반) 이 노려지면 (명중 · 빗나감 · 폭발 · 흩뿌림 — Unit.LastFiredAtTick) 곧바로 가까운 엄폐 · 사격자 사선 밖으로. false = 끔.</summary>
    public bool CoverOnFire { get; set; } = true;
    /// <summary>엄폐 칸 탐색 반경 (칸, 정수).</summary>
    public int CoverRadius { get; set; } = 6;
    /// <summary>이 초 동안 다시 노려지지 않으면 (조용해지면) 관측을 다시 한다.</summary>
    public float CoverCalmSec { get; set; } = 8;
    /// <summary>엄폐에 든 뒤 최소 머무는 초.</summary>
    public float CoverMinSec { get; set; } = 3;
    /// <summary>엄폐 칸에서도 다시 노려지면 이 초마다 다른 칸을 고른다.</summary>
    public float CoverRepickSec { get; set; } = 2;
    /// <summary>사격자 (이 정찰병을 표적으로 잡은 적, 없으면 가장 가까운 적) 를 찾는 반경 (칸).</summary>
    public float CoverThreatRadius { get; set; } = 60;
    /// <summary>숲 탐색 (forest): 앞에 숲이 있고 적이 안 보이면 정찰조가 깊은 숲도 들어가 가로지른다 (중대가 서 있어도 조용하면 내보낸다). false = 끔.</summary>
    public bool ForestProbe { get; set; } = true;
    /// <summary>앞 이 칸 안에 숲이 있어야 탐색.</summary>
    public float ForestProbeLook { get; set; } = 24;
    /// <summary>전 구성원이 이 초 이상 교전 · 피격이 없어야 탐색 (중대가 선 채 교착일 때도).</summary>
    public float ForestProbeQuietSec { get; set; } = 25;
    /// <summary>탐색 때 기준점 · 복귀 거리에 더하는 칸.</summary>
    public float ForestProbeExtra { get; set; } = 8;
    /// <summary>탐색 때 깊은 숲 칸 점수 (보통은 −40).</summary>
    public int ForestProbeDeepBonus { get; set; } = 25;
}

public sealed class CaptureDef
{
    public float Radius { get; set; } = 4;
    public float Time { get; set; } = 10;
    public float MaxMultiplier { get; set; } = 3;
}

/// <summary>교전 규칙 수치 (balance.json "combat", ROADMAP §2-4b~e). 제압 · 계급 · 장교 지휘 · 돌격 · 반응 지연 · 멍칫 · 표적 분산 · 흩뿌림.</summary>
public sealed class CombatRulesDef
{
    // ---- 표적 분산 (§2-4b): 보병의 자동 표적 점수 = 거리 + jitter × 해시(사격자, 표적) ∈ [0, jitter)
    public float TargetSpreadJitter { get; set; } = 4;
    /// <summary>플레이어 돌격 명령의 지휘 계통 지연 배수 (착검 시간은 따로 더해짐). AI 명령은 영향 없음.</summary>
    public float ChargeOrderDelayMul { get; set; } = 1;
    // ---- 제압 (§2-4b 6차): 0~max, 초당 decay 감소 (계급 단계당 +rankDecayBonus, 장교 지휘 범위 안 ×officerDecayMul)
    public int SuppressMax { get; set; } = 1000;
    public float SuppressDecay { get; set; } = 80;
    public float SuppressRankDecayBonus { get; set; } = 0.1f;
    /// <summary>제압 최대일 때 사격 간격 배수 증가분 (1.0 → ×2) · 이동 속도 감소분 (0.5 → −50%).</summary>
    public float SuppressFireSlow { get; set; } = 1.0f;
    public float SuppressMoveSlow { get; set; } = 0.5f;
    /// <summary>제압 감소 판정 간격(틱, 유닛 Id 로 분산).</summary>
    public int SuppressDecayInterval { get; set; } = 15;
    // ---- 장교 (§2-4c 12차): 지휘 범위 안 아군 병사의 제압 감소 ×, 사격 간격 ×. 중첩 없음.
    public float OfficerDecayMul { get; set; } = 3;
    public float OfficerFireMul { get; set; } = 0.9f;
    // ---- 제압 재설계 (N25, 2026-10-19, Suppression.cs): 비례 감소 · 근접 탄착 · 명중률 · 고정 (엎드림)
    /// <summary>초당 감소에 더하는 비례 항 (현재 제압 × 값). 지속 사격에서 상한에 붙지 않고 평형.</summary>
    public float SuppressDecayProp { get; set; } = 0f;
    /// <summary>가산 포화: 제압 가산 × (1 − 값 × 제압비). 1 = 상한에서 0 (상한에 붙지 않음).</summary>
    public float SuppressGainSat { get; set; } = 0f;
    /// <summary>N39 (2026-10-19 사용자 결정: 제압이 너무 빨리 빠짐): 최근 calmSec 초 안에 노려진 (Unit.LastFiredAtTick — 명중 · 빗나감 · 흩뿌림 · 폭발) 병사의 감소 × fireDecayMul. 0 = 끔.</summary>
    public float SuppressCalmSec { get; set; } = 0f;
    public float SuppressFireDecayMul { get; set; } = 1f;
    /// <summary>빗나간 탄 근접 제압: 사선 · 착탄점 radius 칸 안 적에게 suppress × frac × (1 − d/radius). 사선 구간 = 착탄점 앞 pathLen 칸 + 너머 overshoot 칸.</summary>
    public float NearMissFrac { get; set; } = 0.5f;
    public float NearMissRadius { get; set; } = 1.5f;
    public float NearMissPathLen { get; set; } = 6f;
    public float NearMissOvershoot { get; set; } = 3f;
    /// <summary>사격자 명중률 × (1 − suppressAccMul × 제압비).</summary>
    public float SuppressAccMul { get; set; } = 0.5f;
    /// <summary>고정 (엎드림): 제압비 ≥ pinAt 이면 고정, &lt; pinRelease 면 풀림. 고정 사격자 명중률 × pinnedAccMul, 고정 표적 엄폐 배수 = min(지형, proneHitMul).</summary>
    public float PinAt { get; set; } = 0.6f;
    public float PinRelease { get; set; } = 0.4f;
    public float PinnedAccMul { get; set; } = 0.75f;
    public float ProneHitMul { get; set; } = 0.6f;
    // ---- 제압 재설계 2 (supp 2026-10-20): 엎드림 · 엄폐 회복 · 붕괴 (Suppression.cs). 0 = 끔.
    /// <summary>엎드림 (고정 아님): 서 있는 (proneSettleSec 초 안 움직이지 않음 · 돌격 아님) 도보 보병이 제압비 ≥ proneAt 이면 엎드리고 &lt; proneRelease 면 일어난다. 피격 = min(지형, proneHitMul). 0 = 고정만 엎드림.</summary>
    public float ProneAt { get; set; } = 0.35f;   // supp2: 기본 켬
    public float ProneRelease { get; set; } = 0.2f;
    public float ProneSettleSec { get; set; } = 0.5f;
    /// <summary>pronemove: 엎드린 (제압) 병사는 대열 (중대 깃발) 이동에 끌려가지 않고 제자리에서 쏜다 (제압이 풀리면 일어나 합류). false = 옛 동작.</summary>
    public bool ProneHoldInFormation { get; set; } = true;
    /// <summary>pronemove: 개별 (대열 밖) 이동 명령 — 고정 병사는 기어서 (속도 × crawlSpeedMul), 엎드린 병사는 proneStandSec 초 일어선 뒤 걷는다.</summary>
    public float CrawlSpeedMul { get; set; } = 0.3f;
    public float ProneStandSec { get; set; } = 0.8f;
    /// <summary>pronemove 상한 (2026-10-21 사용자 승인): 대열 이동 중 엎드린 채 버티는 최대 초 — 지나면 아직 제압 중이어도 대열을 따라 기어간다 (속도 × crawlSpeedMul). 0 = 상한 없음.</summary>
    public float ProneHoldMaxSec { get; set; } = 8f;
    /// <summary>pronemove 상한: 돌격 · 강행 (속보 이동 · 참호 돌격 접근 · 돌파) 대열이면 이 초 뒤 (고정이어도) 일어나 대열을 따라 걷는다. 0 = 상한 없음.</summary>
    public float ProneHoldMaxAssaultSec { get; set; } = 3f;
    /// <summary>pronemove 도하 면제 (2026-10-21 사용자 승인): 전술 AI 도하 강행 (속보 Cross) 병사는 위 엎드림 이동 규칙을 받지 않는다. false = 도하도 같은 규칙.</summary>
    public bool ProneCrossExempt { get; set; } = true;
    /// <summary>엄폐 (지형 제압 배수 &lt; 1) 안 병사의 제압 감소 배수.</summary>
    public float CoverDecayMul { get; set; } = 1.25f;
    /// <summary>붕괴: 고정된 병사의 제압비 ≥ breakAt 이면 breakInterval 초마다 확률 breakChance × (s − breakAt)/(1 − breakAt) × 사기 배수 (계급 × (1 − breakRankFactor), 장교 근처 breakOfficerMul / 부사관 breakNcoMul, 엄폐 breakCoverMul, × (1 + breakCasualtyWeight × 중대 사상률)) 로 무너져
    /// 가장 가까운 적 반대쪽 breakDistance 칸으로 달아난다 (사격 · 명령 무시, 후퇴 명령은 따름). breakMinSec 뒤 제압비 &lt; breakRallyAt 이면 회복 (명령으로 복귀), breakMaxSec 지나면 무조건 회복. breakChance 0 = 끔.</summary>
    public float BreakAt { get; set; } = 0.85f;
    public float BreakChance { get; set; } = 0f;
    public float BreakInterval { get; set; } = 1f;
    public float BreakRankFactor { get; set; } = 0.08f;
    public float BreakOfficerMul { get; set; } = 0.4f;
    public float BreakNcoMul { get; set; } = 0.6f;
    /// <summary>[N68] 기수 (Bearer.cs): 반경 BearerRadius 칸 안 기수 근처 붕괴 확률 ×BearerBreakMul · AI 중대 제압 후퇴 굴림 ×BearerRetreatMul (중대에 산 기수가 있을 때). 기수가 쓰러지면 BearerFellSec 초 동안 반경 안 병사 붕괴 ×BearerFellMul · 후퇴 굴림 ×BearerFellMul (BearerBreakMul = 1 · BearerFellMul = 1 이면 끔).</summary>
    public float BearerRadius { get; set; } = 10f;
    public float BearerBreakMul { get; set; } = 0.7f;
    public float BearerRetreatMul { get; set; } = 0.85f;
    public float BearerFellSec { get; set; } = 20f;
    public float BearerFellMul { get; set; } = 1.3f;
    public float BreakCoverMul { get; set; } = 0.5f;
    public float BreakCasualtyWeight { get; set; } = 2f;
    public float BreakDistance { get; set; } = 10f;
    /// <summary>supp2: 달아날 길이 물 (강 · 다리) 에 막힌 병사 (배수진 · 교두보) 의 붕괴 확률 배수 — 도주 목표는 물가 앞에서 끊긴다 (다리를 건너 되돌아가지 않음).</summary>
    public float BreakCorneredMul { get; set; } = 0.5f;
    // ---- 항복 (N79b, Surrender.cs) — 아주 까다로운 조건, 확률 낮음. surrenderChance 0 = 끔
    public float SurrenderChance { get; set; } = 0.05f;
    public float SurrenderPlayerMul { get; set; } = 0.3f;
    public float SurrenderCasualty { get; set; } = 0.8f;
    public int SurrenderMaxLeft { get; set; } = 10;
    public float SurrenderSuppAt { get; set; } = 0.85f;
    public float SurrenderSuppSec { get; set; } = 10f;
    public float SurrenderInterval { get; set; } = 2f;
    public float SurrenderThreatRadius { get; set; } = 20f;
    public float SurrenderProbeLen { get; set; } = 10f;
    public float SurrenderProbeDeg { get; set; } = 60f;
    public float SurrenderBlockRadius { get; set; } = 4f;
    public float BreakMinSec { get; set; } = 8f;
    public float BreakMaxSec { get; set; } = 40f;
    public float BreakRallyAt { get; set; } = 0.3f;
    // ---- 계급 (2026-10-19 확정, ROADMAP §2-4c): 사병 9단계 — 누적 사살 수 기준 승급 (rankKills[i] = 계급 i+1 기준, 이름은 assets/data/ranks.json 세력별). 사살 수 상한 killsCap.
    public int[] RankKills { get; set; } = { 2, 5, 10, 18, 30, 48, 75, 110 };
    public int KillsCap { get; set; } = 128;
    // ---- 분대 · 부사관 (2026-10-19 확정): 분대 최대 squadMax 명 (고정 편성). squadMergeBelow 명 미만이 되면 같은 중대의 다른 분대와 자동 합치기 (자리가 없으면 나눠 넣기).
    // 공식 부사관 = 사병 계급 ≥ ncoRank (하사). 분대장(분대 안 최고 계급)이 부사관이면 분대원 (분대장에서 ncoRadius 칸 안, 0 = 분대 전체): 멍칫 ×ncoHesitateMul · 제압 감소 ×ncoDecayMul. 장교 효과와 중첩 안 함 (장교 쪽 우선).
    public int SquadMax { get; set; } = 12;
    public int SquadMergeBelow { get; set; } = 6;
    public int NcoRank { get; set; } = 5;
    public float NcoHesitateMul { get; set; } = 0.5f;
    public float NcoDecayMul { get; set; } = 1.5f;
    public float NcoRadius { get; set; } = 12;
    // 부사관 기본 편성 (2026-10-19 사용자 승인): 미션 보병 분대의 분대장을 이 계급으로 (0 = 끔). 미션 병력 squadLeaderRank 로 덮어씀 (WorldMission.AssignSquadLeaders).
    public int SquadLeaderRank { get; set; } = 5;
    // ---- 장교 빈자리 점검 (§2-4c 12차 · 15차): 장교가 있는 중대는 gapCheckSec 초마다 (중대 Id 분산) 빈자리가 있으면 대형을 느슨하게 다시 채운다. 교전 중이면 장교가 swordWaveChance 확률로 칼 흔들기.
    public float GapCheckSec { get; set; } = 25;
    public float GapMoveMin { get; set; } = 2.75f;
    // ---- N12 대형 강박 줄이기 (2026-10-19 사용자 승인): 빈자리 점검 간격 하한 (경험치로 짧아져도) · 교전 판정 창 (구성원 표적 또는 이 초 안 사격 = 교전 → 점검 안 함),
    // 대형 복귀 금지 (이 초 안에 쏜 병사 · 엄폐 (참호 · 숲 · 가옥) 중인 병사는 슬롯으로 돌려보내지 않음), 깃발 대형 도착 여유 반경, 행군 중 무시할 슬롯 오차.
    public float GapCheckMinSec { get; set; } = 20;
    public float GapCombatSec { get; set; } = 10;
    public float RegroupFireSec { get; set; } = 5;
    public float FormArriveSlack { get; set; } = 0.6f;
    public float FormMoveTolerance { get; set; } = 0.5f;
    // ---- bunchfix (2026-10-19, docs/BUNCHING_DIAG_2026-10-19.md): 행군 중 깃발 폭 선의 이 비율 이상이 막히면 (지금 · 3칸 앞 · 8칸 앞 중 하나) 2열 종대, 셋 다 트인 채 이 틱이 지나면 원래 대형.
    public float NarrowBlockFrac { get; set; } = 0.25f;
    public int NarrowClearTicks { get; set; } = 120;
    // ---- 대형 완화 (formation, 2026-10-21 사용자 결정 "대형 관련으로는 네 제안대로 한다", src/Core/FormRelax.cs). formRelax = 전체 켜고 끄기 (끄면 옛 동작 그대로).
    public bool FormRelax { get; set; } = true;
    public float ShortMoveDist { get; set; } = 18f;          // 짧은 이동 (무게중심 → 목표 이 칸 안, 방향 그대로) = 슬롯 재배정 없는 평행이동 · 즉시 출발 (0 = 끔)
    public float ShortMoveFaceDeg { get; set; } = 20f;       // 평행이동 조건: 새 방향이 지금 중대 방향에서 이 각도 안
    public float RelaxMoveTolerance { get; set; } = 1.5f;    // 행군 슬롯 오차 허용 (formMoveTolerance 0.5 대신) — 이 안이면 슬롯으로 꺾어 걷지 않고 깃발과 나란히
    public float FormDeadband { get; set; } = 0.25f;         // 행군 중 이 칸 아래 오차는 아예 고치지 않음 (작은 오차 데드밴드)
    public float FormArriveZone { get; set; } = 1.5f;        // 구역 도착: 최종 슬롯 이 칸 안 (뒤쪽으로는 formArriveSlack 까지만) 이면 그 자리에서 도착 (0 = 끔)
    public float RestAlignSec { get; set; } = 8f;            // 비접적 휴식 정렬: 중대가 이 초 동안 서 있고 접적 · 교전이 아니면 한 번 정확히 재정렬 (대열 정비 한 번, 0 = 끔)
    public float NarrowExitFrac { get; set; } = 0.12f;       // 종대 → 횡대 복귀 문턱 (히스테리시스): 폭 선 막힘이 이 비율 아래로 narrowClearTicks 연속일 때만 (진입은 narrowBlockFrac)
    public bool FormStandHold { get; set; } = true;          // 깃발이 선 동안 (교전 정지 · 기다림) 슬롯 오차가 작으면 (접적 = 행군 허용 오차, 아니면 데드밴드 × 2) 그 자리에 선다 (N33/N51 비빔)
    // ---- choke (2026-10-21): 좁은 길목 통과 질서 (대대 통과 큐 · 종대 중 슬롯 재배정 · 빈자리 점검 끔 · 밀어내기 약화).
    public bool ChokeEnabled { get; set; } = false;   // WIP: 다중 중대 큐가 아직 통과 시간을 늘린다 → 기본 끔
    public int ChokeWidth { get; set; } = 4;            // 이 칸 수 이하로 좁은 곳 = 길목 칸
    public float ChokeLookahead { get; set; } = 14f;    // 이 거리 안에 길목이 보이면 통과 큐 대상
    public int ChokeHoldMaxTicks { get; set; } = 1200;  // 앞 중대를 기다리는 최대 틱 (교착 방지)
    public float ChokePushScale { get; set; } = 0.5f;   // 길목 칸 위 대형 이동 병사의 겹침 밀림 배수
    // ---- N12 장교 위치: 틀 없는 중대 대형에서 장교 슬롯 = 대열 중앙 뒤 officerBack 칸 (장교 여럿이면 옆으로 officerSpread 칸 간격),
    // 돌격 중이 아닌 장교는 중대 보병 앞선 + officerLeadTol 칸을 넘어 앞으로 걷지 않는다 (중화기 보폭과 같은 방식).
    public float OfficerBack { get; set; } = 3f;
    public float OfficerSpread { get; set; } = 3f;
    public float OfficerLeadTol { get; set; } = 0f;
    // ---- N35 장교 교전 회피 (2026-10-19 사용자 결정, OfficerConduct.cs): officerAvoid = 켬. 장교는 혼자 돌격하지 않는다 — 중대 보병 (장교 제외) 의
    // officerChargeWithFrac 이상이 돌격 중일 때만 함께 돌격하고, 돌격 중에도 돌격하는 병사들의 앞줄 + officerChargeLeadTol 칸을 넘지 않는다.
    // 자위 사격만: 적이 officerDefendRange 칸 안이거나, 장교 officerMeleeRadius 칸 안에서 중대 병사가 근접전 (돌격) 중이면 권총 사거리까지.
    public bool OfficerAvoid { get; set; } = true;
    public float OfficerChargeWithFrac { get; set; } = 0.3f;
    public float OfficerChargeLeadTol { get; set; } = 0f;
    public float OfficerDefendRange { get; set; } = 4f;
    public float OfficerMeleeRadius { get; set; } = 6f;
    // 정찰병 자위 사격 (2026-10-19 사용자 추가 결정): scoutAvoid = 켬. 표적은 scoutDefendRange 칸 안 적만 (찾기 · 추격 없음),
    // 맞으면서 접촉을 끊을 수 없을 때 (멈춰 있음 — 엄폐 · 관측 자리) 는 scoutRetaliateRange 칸 안 사격자에게 반격.
    public bool ScoutAvoid { get; set; } = true;
    public float ScoutDefendRange { get; set; } = 4f;
    public float ScoutRetaliateRange { get; set; } = 10f;
    public float SwordWaveChance { get; set; } = 0.33f;
    // ---- 대열 정비 (gapfill, 2026-10-19 사용자 결정): 교전 중 빈자리 메우기는 명령으로만 — 플레이어 '대열 정비' (CloseRanksCommand) 또는 장교 판단.
    // 장교 (또는 승계한 지휘관 · 대리 부사관) 는 closeCheckSec 초마다 (DecideInterval) 교전 중인 중대의 대열을 보고, 앞줄에 구멍이 나면 (gapfill2 — 아래 closeHole*, 보조로 벗어난 병사 비율 ≥ closeOnFrac)
    // 대열 정비를 명령. 히스테리시스: 모든 지표가 문턱 × closeHysteresis 아래로 내려가야 경보 해제, 경보 중 재명령은 closeCooldownSec 간격. closeFrontTol = 측정용 옛 '앞줄 빈 슬롯' 반경.
    // 명령은 closeWindowSec 동안 유효 (closePassSec 마다 다시 살펴 늦게 따르는 병사를 보냄), 슬롯에서 closeSlack 칸 넘게 벗어난 병사만 이동.
    // 병사 숙련 (사병 계급): < closeRegularRank = 신병, < closeVeteranRank = 정규, 이상 = 노병. 교전 중 (표적 · regroupFireSec 안 사격) 인 병사는 closeObey[숙련] 확률로 곧장,
    // 아니면 closeDelaySec[숙련] 초 뒤 (또는 교전이 끝나면) 따른다. 교전 중이 아닌 병사는 모두 곧장. 참호 안 · 참호 자리 병사는 움직이지 않음.
    public float CloseCheckSec { get; set; } = 6;
    public float CloseDispTiles { get; set; } = 2f;
    /// <summary>앞줄 슬롯을 '채웠다' 고 보는 반경 = 이 값 × 대형 간격 (1 미만 — 바로 뒷줄 병사는 앞줄을 채운 것이 아님).</summary>
    public float CloseFrontTol { get; set; } = 0.75f;
    /// <summary>[보조 문턱, gapfill2] 배정 슬롯에서 closeDispTiles 넘게 벗어난 병사 비율 — 교전 중 대열은 원래 60% 안팎이 벗어나 있어 훨씬 높게 둔다 (≥ 1 이면 끔).</summary>
    public float CloseOnFrac { get; set; } = 0.9f;
    // ---- gapfill2 (2026-10-19 사용자 승인): 장교 판단 = 앞줄 구멍. 앞줄 슬롯 (메울 수 있는 병사의 슬롯 중 가장 앞 줄) 마다 옆으로 closeHoleLat × 간격 안,
    // 슬롯보다 closeHoleDepth 칸 넘게 뒤가 아닌 곳에 병사가 하나도 없으면 '구멍'. 구멍 ≥ max(closeHoles, closeHoleFrac × 앞줄 슬롯) · 가장 넓은 연속 구멍 ≥ closeHoleGap ·
    // 앞줄 배정 병사 중 closeBehindTiles 칸 넘게 뒤처진 병사 ≥ closeBehindMen (0 = 끔) 중 하나면 경보.
    public float CloseHoleLat { get; set; } = 0.6f;
    public float CloseHoleDepth { get; set; } = 2f;
    public int CloseHoles { get; set; } = 5;
    public float CloseHoleFrac { get; set; } = 0.5f;
    public int CloseHoleGap { get; set; } = 6;
    public float CloseBehindTiles { get; set; } = 3f;
    public int CloseBehindMen { get; set; } = 0;
    /// <summary>적 돌격 감지 (봉천 대도 · 착검 · 기병 돌격 — 돌격 상태 1 · 2): closeChargePollSec 마다 중대 병사 closeChargeRange 칸 안에서 다가오는 돌격 적을 찾으면
    /// 점검 간격과 무관하게 바로 판단 (재명령 간격은 지킴). 그때 문턱 = 평소 × closeChargeThrMul.</summary>
    public float CloseChargeRange { get; set; } = 12f;
    public float CloseChargePollSec { get; set; } = 0.25f;
    public float CloseChargeThrMul { get; set; } = 0.5f;
    /// <summary>tweak1: 자기 중대 돌격 중 (틀 돌격 · 명령 돌격 병사, 또는 자동 돌격 병사가 이 비율 이상) 에는 대열 정비 명령 · 정비를 미룬다 (0 = 자동 돌격으로는 미루지 않음).</summary>
    public float CloseAssaultAutoFrac { get; set; } = 0.25f;
    public float CloseHysteresis { get; set; } = 0.6f;
    public float CloseCooldownSec { get; set; } = 20;
    public float CloseWindowSec { get; set; } = 20;
    public float ClosePassSec { get; set; } = 1;
    public float CloseSlack { get; set; } = 1f;
    public int CloseRegularRank { get; set; } = 2;
    public int CloseVeteranRank { get; set; } = 4;
    /// <summary>숙련 (신병 · 정규 · 노병) 별: 교전 중인 병사가 대열 정비 명령에 곧장 따를 확률.</summary>
    public float[] CloseObey { get; set; } = { 1f, 0.5f, 0.15f };
    /// <summary>숙련별: 곧장 따르지 않은 교전 중 병사가 그래도 따르기까지의 지연 (초).</summary>
    public float[] CloseDelaySec { get; set; } = { 0f, 6f, 12f };
    // ---- 착검 돌격 · 근접전 (§2-4b): 돌격 중 속도 배수, 자동 돌격 거리(칸), 근접 판정 거리(가장자리, 칸)
    public float ChargeSpeedMul { get; set; } = 1.3f;
    public float ChargeAutoRange { get; set; } = 2;
    /// <summary>플레이어 지점 포격 (관측자 없음) 의 착탄 퍼짐 배수.</summary>
    public float GroundScatterMul { get; set; } = 1.5f;
    /// <summary>포격 종류 (N79a, GroundFire.cs): 준비 사격 = 포마다 쏘는 발수 · 이동탄막 = 착탄선이 barrageStepSec 초마다 barrageStepTiles 칸 전진, 시작점에서 barrageMaxTiles 칸까지.</summary>
    public int PrepRounds { get; set; } = 8;
    public float BarrageStepTiles { get; set; } = 2.5f;
    public float BarrageStepSec { get; set; } = 5f;
    public float BarrageMaxTiles { get; set; } = 40f;
    /// <summary>포탄 착탄 폭발 (Explode) 이 아군 · 동맹도 맞힌다 (pursuit2 — 포병만 아군 오사, 쏜 포 자신 제외).</summary>
    public bool ArtilleryFriendlyFire { get; set; } = true;
    /// <summary>포수 규율 (World.CrewDangerClose): 자동 표적 둘레 splash × 1.8 + 퍼짐 + 이 칸 안에 아군이 있으면 쏘지 않음 (&lt; 0 = 끔).</summary>
    public float CrewDangerMargin { get; set; } = 1f;
    /// <summary>포수 규율 반경에 넣는 거리 퍼짐의 비율 (1 = 최대 오차 전부 — 곡사포 장거리 지원이 아군 앞 10칸 적도 못 쏨; crossing 병합 때 0.5).</summary>
    public float CrewDangerScatterMul { get; set; } = 0.5f;
    /// <summary>N11 거리별 착탄 퍼짐 (World.ScatterAt): 거리 0 에서 무기 scatter 의 이 비율.</summary>
    public float ScatterNearFrac { get; set; } = 0.15f;
    /// <summary>N11 거리별 착탄 퍼짐 곡선: 0 = 직선, 1 = 제곱 (가까이 정확, 멀리 급격히 넓어짐). 그 사이는 섞음.</summary>
    public float ScatterQuad { get; set; } = 0.5f;
    /// <summary>N10 재조준: 사계 밖 표적까지 사계 중심을 옮길 각이 이 값 (도) 이상이면 철수 → 회전 → 재방열, 미만이면 방열한 채 포가를 돌린다.</summary>
    public float TraverseRedeployDeg { get; set; } = 90;
    /// <summary>N10 방열한 채 포가 돌리는 속도 = 유닛 turnRate × 이 배수.</summary>
    public float TraverseShiftMul { get; set; } = 0.5f;
    /// <summary>제압이 이 이상이면 자동 돌격 (적 근접 · 발밑 적 살피기) 금지 (2026-10-19). 명령 돌격 · 기병 '돌격만' 은 그대로.</summary>
    public int ChargeAutoMaxSuppress { get; set; } = 400;
    /// <summary>S4: 돌격 중 제압이 이 이상이면 돌격이 꺾인다 (보병 · 기병). 0 = 끔. 돌격 시작 뒤 chargeBreakGraceSec 동안은 유예.</summary>
    public int ChargeBreakSuppress { get; set; } = 0;
    public int ChargeBreakSuppressCav { get; set; } = 0;
    public float ChargeBreakGraceSec { get; set; } = 0.5f;
    public float MeleeRange { get; set; } = 0.3f;
    // ---- 스태미나 · 속보 (ROADMAP §4): 보병 · 장교만. 돌격 · 속보 중 소모, 쉬면 회복. 돌격 · 속보 속도 보너스는 스태미나 ≥ staminaFullSpeed 에서 전부, 0 에서 0 (선형). 스태미나 < staminaLow 면 모든 이동 ×staminaLowMoveMul.
    public float StaminaMax { get; set; } = 100;
    public float StaminaChargeDrain { get; set; } = 8;
    public float StaminaQuickDrain { get; set; } = 3;
    public float StaminaRegenMove { get; set; } = 2;
    public float StaminaRegenIdle { get; set; } = 5;
    public float StaminaFullSpeed { get; set; } = 50;
    public float StaminaLow { get; set; } = 25;
    public float StaminaLowMoveMul { get; set; } = 0.85f;
    public float QuickStepSpeedMul { get; set; } = 1.15f;
    // ---- 반응 지연 (§2-4d 14차): 명령 → 출발 0~reactDelayMaxTicks 틱
    public int ReactDelayMaxTicks { get; set; } = 2;
    // ---- 첫 사격 반응 (N34 2026-10-19): firstShotIdleSec 넘게 쏘지 않은 보병 (직사 · 거치 무기 제외) 이 새 표적을 잡으면 조준 = max(무기 조준, firstShotMin~Max 초 × 숙련 배수 [신병, 정규, 노병]) — 첫 일제 사격이 1~1.5초에 흩어진다
    public float FirstShotMin { get; set; } = 0.2f;
    public float FirstShotMax { get; set; } = 1.2f;
    public float FirstShotIdleSec { get; set; } = 5f;
    public float[] FirstShotSkillMul { get; set; } = { 1f, 0.85f, 0.7f };
    // ---- 포 발사 속도 (N34): 포 (곡사 · 방열 폭발 무기) 마다 고정 배수 artyRateMin~Max (유닛 Id · 지도 시드 해시, 상태 없음) — 포대가 일제히 쏘지 않게
    public float ArtyRateMin { get; set; } = 0.93f;
    public float ArtyRateMax { get; set; } = 1.07f;
    // ---- arty3 (사용자 2026-10-21): 대포 (category gun) 방렬 시작 지연 0~gunDeployDelayMax 초 + 방렬 시간 ±gunDeployTimeVar (PairNoise, 상태 없음)
    public float GunDeployDelayMax { get; set; }
    public float GunDeployTimeVar { get; set; }
    /// <summary>arty3 방렬 자리 유지: 방렬한 대포의 자동 표적이 사거리 밖이면 이 초 동안 기다린 뒤에야 철수 · 추격 (명시 공격 명령은 무기 holdTime). 0 = 옛 규칙.</summary>
    public float GunOutOfRangeHoldSec { get; set; }
    /// <summary>arty3 재방렬 냉각: 방렬을 마친 뒤 이 초 안에는 자동 표적 때문에 철수하지 않는다 (명시 이동 · 공격 명령은 예외). 0 = 끔.</summary>
    public float GunRepackCooldownSec { get; set; }
    /// <summary>arty3 heavy 간격: 방렬 · 거치를 시작할 때 방렬/거치한 아군 중화기와 최소 heavyGapMin, 가능하면 heavyGapWant 칸 (옆으로 비켜선 뒤 방렬). 0 = 끔. 참호 자리 제외.</summary>
    public float HeavyGapMin { get; set; }
    public float HeavyGapWant { get; set; }
    /// <summary>arty3 비켜서기 속도 = 유닛 속도 × 이 배수 (차체를 돌리지 않고 옆으로 밀어 옮김).</summary>
    public float HeavyGapSpeedMul { get; set; } = 0.5f;
    /// <summary>arty3 최후 저지선: 사격선 (중기관총 → 지정 지점 방향, 사거리까지) 에서 이 칸 안의 적만 쏜다.</summary>
    public float FinalLineBand { get; set; } = 2f;
    // ---- 멍칫 (§2-4d 14차): 제압 ≥ hesitateMinSuppress 일 때만, hesitateInterval 초마다 최대 hesitateChance 확률 (제압에 비례), 멈춤 hesitateMin~Max 초.
    // 계급 단계당 ×(1 − hesitateRankFactor), 장교 근처 ×hesitateOfficerMul. 평균 속도 손실 상한 hesitateMaxLoss (멈춘 뒤 그 비율만큼 다시 멈추지 않음). 이동 명령 뒤 hesitateGraceSec 초 · 돌격 중에는 없음.
    public int HesitateMinSuppress { get; set; } = 400;
    public float HesitateChance { get; set; } = 0.15f;
    public float HesitateMin { get; set; } = 0.2f;
    public float HesitateMax { get; set; } = 0.6f;
    public float HesitateInterval { get; set; } = 1.0f;
    public float HesitateRankFactor { get; set; } = 0.1f;
    public float HesitateOfficerMul { get; set; } = 0.5f;
    public float HesitateMaxLoss { get; set; } = 0.05f;
    public float HesitateGraceSec { get; set; } = 3;
    // ---- 교전 거리 (2차 추가 A): 공격 이동 · 공격 · 자동 교전 유닛은 사거리의 이 비율까지 다가가서 사격 (사수는 최대 사거리에서). 곡사 · 대포 (category gun) 는 제외 — 기관총 · 대전차소총 (거치 보병) 은 2026-10-19 부터 포함.
    public float EngageRangeFrac { get; set; } = 0.7f;
    // ---- 반격 · 엄폐 찾기 (engage, 2026-10-19): 최근 underFireSec 초 안에 총격을 받은 (LastFiredAtTick) 유닛 = 피격 중.
    // 피격 중인 대기 유닛은 다가가지 않고 그 자리에서 반격, 공격 이동 대형 구성원도 쏜 적에게 반격 (반응 거리 밖이어도 사거리 안이면).
    // 피격 중인 대기 · 사수 보병 (참호 밖, 이동 그룹 없음) 은 coverSeekTicks 틱마다 반경 coverSeekRadius 칸 안의 빈 참호 자리 → 더 좋은 엄폐 칸 (명중 배수 coverSeekGain 이상 개선, 칸당 coverSeekPerTile 명) 으로 옮긴다.
    public float UnderFireSec { get; set; } = 2.5f;
    /// <summary>N73 사격 통제 '응사만': 자기 · 분대원이 이 초 안에 총격을 받았거나, 적이 이 거리 (칸) 안에 들어오면 쏜다.</summary>
    public float FireControlReturnSec { get; set; } = 6f;
    public float FireControlCloseRange { get; set; } = 6f;
    public float CoverSeekRadius { get; set; } = 3f;
    public float CoverSeekGain { get; set; } = 0.15f;
    public int CoverSeekTicks { get; set; } = 30;
    public int CoverSeekPerTile { get; set; } = 2;
    // ---- F1 접적 (2026-10-20 사용자 승인, Contact.cs): 중대 접적 = 구성원 표적 · contactTriggerSec 안 사격 · 피격. 마지막 트리거 뒤 contactDisengageSec 넘게 조용하면 이탈, 거기서 contactReformSec 더 조용하면 비접적.
    // contactLoose = 접적 · 이탈 중 빈자리 점검 · 장교 대열 정비 · 도착 깃발 슬롯 복귀를 끄고, 병사는 슬롯 둘레 contactZoneRadius 칸 구역 안에서 사격 위치를 잡는다 (spotSearchRadius 안, 엄폐 · 고지 선호).
    // 사격 위치 고정: spotDwellSec 동안은 움직이지 않고, 그 뒤에도 spotMoveMin 칸 넘는 이동만 (제압 spotReleaseSupp 이상 · 후퇴 · 새 명령 · 구역 밖이면 해제).
    public float ContactTriggerSec { get; set; } = 3f;
    public float ContactDisengageSec { get; set; } = 10f;
    public float ContactReformSec { get; set; } = 15f;
    public bool ContactLoose { get; set; } = true;
    public float ContactZoneRadius { get; set; } = 5f;
    public float SpotSearchRadius { get; set; } = 2.5f;
    public float SpotDwellSec { get; set; } = 12f;
    public float SpotMoveMin { get; set; } = 1.5f;
    public float SpotReleaseSupp { get; set; } = 0.5f;
    public float SpotHeightGain { get; set; } = 0.1f;
    // [측정 토글] F1 부분 끄기: contactHoldChase = 접적 중 대기 병사 추격 제한, contactArriveZone = 도착 깃발 구역 도착 (기본 끔), spotEnabled = 구역 · 사격 위치 고정
    public bool ContactHoldChase { get; set; } = true;
    public bool ContactArriveZone { get; set; } = false;   // 측정 (docs/f1_raw TA): 켜면 A/B 승 · 교환비 · 사격 비율이 나빠짐 → 기본 끔
    public bool SpotEnabled { get; set; } = true;
    public bool SpotAttackCover { get; set; } = false;   // true = 공격 자세 (tweak1) 중대도 구역 안 엄폐 칸으로 옮김
    // ---- F1 2차 (F2 회전 · 선까지 전진 · AI 재명령 제한, ContactF2.cs)
    // rotateInPlace: 중대 목표가 무게중심 rotateInPlaceDist 칸 안이고 방향이 rotateMinDeg 도 넘게 바뀌면 = 제자리 방향 전환 — 대형을 통째로 돌리지 않고
    // 새 방향의 슬롯을 놓고 가까운 병사부터 다시 배정 (CompanyLayout.Match), 새 슬롯 rotateStayRadius 칸 (접적 중 = contactZoneRadius) 안 병사는 제자리에서 방향만 바꾼다.
    public bool RotateInPlace { get; set; } = true;
    public float RotateInPlaceDist { get; set; } = 6f;
    public float RotateMinDeg { get; set; } = 20f;
    public float RotateStayRadius { get; set; } = 1.5f;
    // looseAdvance: 접적 · 이탈 중 (contactLoose) 대형 유지 (KeepFormation) 가 아닌 중대의 이동 명령 = 선까지 전진 — 지금 배치 (좌우 · 앞뒤 자리) 그대로 목표 선으로, 틀 · 대형 모양 강제 없음.
    // 앞뒤 차이는 looseDepth 칸, 좌우는 대형 반폭 × 1.5 로 자름.
    public bool LooseAdvance { get; set; } = true;
    public float LooseDepth { get; set; } = 3f;
    // looseAdvanceAi: AI 세력 (World.Controllers) 중대에도 선까지 전진 — false = 플레이어 명령만 (AI 는 재명령 제한 · 제자리 방향 전환만).
    public bool LooseAdvanceAi { get; set; } = false;   // 측정 (docs/f2_raw): AI 에도 켜면 A 조금 좋지만 도하 교두보 테스트가 깨짐 (방어 역습이 달라짐) → 기본 끔
    // AI 재명령 제한 (접적 중대): 같은 동작의 이동 명령은 목표가 aiContactReorderDist 칸 넘게 바뀌고 aiContactReorderSec 초 지났을 때만 (전술 AI Retarget).
    public float AiContactReorderDist { get; set; } = 8f;
    public float AiContactReorderSec { get; set; } = 10f;   // 15초는 s3a 철수 테스트 (참호 공격) 가 멈춤
    // aiContactTemplates: 공격 중대의 접적 뒤 화력-기동 틀 (tactics.json 행동) — false = 틀은 접근 국면 (배치) 에만, 접적 뒤 AI 는 구역 · 목표만 (전술 AI 접근 · 사격선 · 약진 규칙).
    public bool AiContactTemplates { get; set; } = true;
    // ---- 표적 탐색 비용 (2차 추가 C): 재탐색 간격(틱, + Id 분산 acquireStagger), 탐색 반경 상한(칸)
    public int AcquireInterval { get; set; } = 45;
    public int AcquireStagger { get; set; } = 15;
    public float AcquireRadiusCap { get; set; } = 32;
    public float SquadFollowFireSec { get; set; } = 2f;
    public bool SquadFollowFireAttackMove { get; set; } = true;   // N46 을 대형 공격 이동에도 (true) — 기본 사수만 (공격 대열이 사거리 끝에서 멈춰 s3a · 도하 공격이 막힘)   // N46: 분대원이 이 초 안에 쐈으면 반응 거리 밖 · 사거리 안 적도 쏜다 (분대 안만)

    // ---- 교전 4차: 행진 사격 (이동 중, 앞 ±marchArcDeg 안 · 유효 시야 marchSightFrac 이내 · 사거리 안) · 기병 돌격 충격
    public float MarchArcDeg { get; set; } = 45;
    public float MarchSightFrac { get; set; } = 0.7f;
    public float MarchIntervalMul { get; set; } = 1.5f;
    public float MarchAccMul { get; set; } = 0.4f;
    public float MarchSmgIntervalMul { get; set; } = 1.2f;
    public float MarchSmgAccMul { get; set; } = 0.7f;
    public int MarchMaxSuppress { get; set; } = 400;     // 제압 이 이상이면 행진 사격 불가
    public float MarchMoveMul { get; set; } = 0.5f;      // 행진 사격 중 이동 속도 × (2026-10-19) — 보병은 중대 (장교) 단위로 전원 같은 순간 (MarchCommand.cs)
    public int MarchDecideTicks { get; set; } = 10;      // 장교의 행진 사격 판단 간격 (틱; 분대 · 부사관 판단은 ×3)
    public float MarchHoldSec { get; set; } = 1f;        // 적이 안 보여도 이 시간 동안 행진 사격 유지 (깜빡임 방지)
    public float MarchNcoRangeFrac { get; set; } = 0.6f; // 분대 (부사관) 행진 사격: 적이 유효 반경 × 이 값 안일 때만 (드물게)
    public int MarchNcoMinMembers { get; set; } = 3;     // 분대 행진 사격 최소 구성원
    public float MarchChargeRange { get; set; } = 5f;    // 행진 사격 중 적이 이 거리 (칸, 가장자리) 안이면 자동 돌격
    // ---- N57 착검 · 칼 뽑기 (Bayonet.cs): 착검 안 된 병사가 돌격을 시작하면 이 시간 동안 제자리 (한 번 꽂으면 유지). [최소, 최대] 초, 병사마다 해시
    public float[] BayonetFixSec { get; set; } = { 1.0f, 1.5f };   // 소총 착검 (melee = bayonet)
    public float[] BladeDrawSec { get; set; } = { 0.3f, 0.5f };    // 칼 병과 (대도 · 군도 · 단검) 뽑기
    public float CavalryDrawSec { get; set; } = 0.5f;              // 기병 군도
    public float FixVeteranMul { get; set; } = 0.7f;               // 착검: 노병 (사병 계급 ≥ closeVeteranRank) ×
    public float FixRecruitMul { get; set; } = 1.3f;               // 착검: 신병 (사병 계급 < closeRegularRank) ×
    public bool BayonetFixOrderedOnly { get; set; } = true;        // true = 돌격 명령에만 멈춤 (자동 · 행진 · 역돌격은 즉시 착검 처리), false = 모든 돌격
    // ---- 기병 돌격 충격 (2026-10-19 개정): 피해 보너스 없음 · 이동 · 근접은 일반. 2초 이상 달린 뒤 첫 적중 (보병) = 충격 이벤트
    // ---- N61 기병 하마 사격 (Dismount.cs)
    public bool AutoDismount { get; set; } = true;             // 사격하려 멈춘 기병은 자동 하마
    public float DismountSec { get; set; } = 3f;               // 하마 시간 (0 = 기능 끔)
    public float MountSec { get; set; } = 4f;                  // 승마 시간
    public float DismountedSpeedMul { get; set; } = 0.67f;     // 내린 기병 이동 속도 배수 (보병 수준)
    public bool CavReactOn { get; set; } = true;       // n61 보병 대기병 조기 사격: 돌격 · 빠른 기병은 사거리 끝에서 우선 표적
    public float CavReactAimSec { get; set; } = 0.15f; // 그 표적의 조준 (첫 사격 지연 N34 대신)
    public float CavFastSpeed { get; set; } = 6f;      // 이 속도 (칸/초) 이상으로 달리는 말 탄 기병도 위협 (돌격 중이면 속도 무관)
    public int CavReactInterval { get; set; } = 5;     // 위협 기병 표적 전환 검사 주기 (틱, 유닛 Id 분산)
    public float MountedFireAccMul { get; set; } = 0.4f;       // 승마 사격 명중 배수 (UnitDef.mountedFireAccMul 이 0 이 아니면 그 값 - 몽골 기병 0.7 자리)
    public float MountRangeFrac { get; set; } = 1.5f;          // 내린 기병은 표적이 사거리 x 이 값 밖이어야 다시 타고 추격
    public float ChargeImpactSeconds { get; set; } = 2;
    public float ChargeStunMin { get; set; } = 1.5f;     // 충격 후 기병 마비 (고정 1.5초, N61)
    public float ChargeStunMax { get; set; } = 1.5f;
    public int CavMeleeMaxTargets { get; set; } = 3;      // 기병 한 돌격에서 근접으로 칠 수 있는 서로 다른 보병 수 (0 = 제한 없음, N61)
    public float ChargeCooldownMin { get; set; } = 10;   // 충격 뒤 다음 충격까지 기병마다 10~15초 (결정적 난수)
    public float ChargeCooldownMax { get; set; } = 15;
    public int ChargeSuppress { get; set; } = 300;       // 충격 지점 반경 안 적 보병에게 제압 (평면, 저항 · 엄폐 적용)
    public float ChargeSuppressRange { get; set; } = 2;  // 칸 (충격 지점 중심 거리)

    [JsonIgnore] public int MaxRank => RankKills.Length;
    /// <summary>사살 수 → 계급 0..MaxRank.</summary>
    public int RankOf(int kills)
    {
        int r = 0;
        for (int i = 0; i < RankKills.Length; i++) if (kills >= RankKills[i]) r = i + 1;
        return r;
    }
}

/// <summary>수류탄 규칙 (balance.json "grenade"): 사거리 · 신관 · 반경 · 피해(중심 → 가장자리, 엄폐 무시) · 제압 · 전차 묶음 피해 · 자동 투척 거리 · 회복.</summary>
public sealed class GrenadeDef
{
    public float Range { get; set; } = 5;
    public float Fuse { get; set; } = 1.5f;
    public float Radius { get; set; } = 1.5f;
    public float Damage { get; set; } = 60;
    public float EdgeDamage { get; set; } = 20;
    public float Suppress { get; set; } = 300;
    public float TankDamage { get; set; } = 40;
    public float AutoCoverRange { get; set; } = 4;
    public float AutoTankRange { get; set; } = 3;
    /// <summary>비전투 상태에서 1개 회복에 걸리는 시간 (초). 0 = 회복 없음.</summary>
    public float RegenSeconds { get; set; } = 180;
    /// <summary>던진 점의 최대 사거리에서의 착탄 오차 (칸, 거리에 비례).</summary>
    public float Scatter { get; set; } = 0.5f;
}

public sealed class Defs
{
    public GrenadeDef Grenade { get; set; } = new();
    public StartDef Start { get; set; } = new();
    public SupplyRulesDef Supply { get; set; } = new();
    public TerrainRulesDef Terrain { get; set; } = new();
    public VisionDef Vision { get; set; } = new();
    public ScoutAiDef ScoutAi { get; set; } = new();
    public WeatherDef Weather { get; set; } = new();
    public CaptureDef Capture { get; set; } = new();
    [JsonPropertyName("combat")] public CombatRulesDef CombatRules { get; set; } = new();
    /// <summary>지휘 계통 지연 (balance.json "command", CommandDelay.cs).</summary>
    public CommandDelayDef Command { get; set; } = new();
    /// <summary>장교 직책 계급 · 임시 진급 · 승계 · 경험치 (balance.json "officers", CommandRanks.cs).</summary>
    public OfficerRulesDef Officers { get; set; } = new();
    public Dictionary<string, WeaponDef> Weapons { get; set; } = new();
    /// <summary>[N54] 세력별 소총 분배 (map factions[].en → {무기 키 → 가중치}). 소총 칸 = 유닛 weapon "rifle_mosin".</summary>
    public Dictionary<string, Dictionary<string, int>> FactionRifles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public const string RifleSlot = "rifle_mosin";
    /// <summary>유닛이 실제로 드는 주무기 키: 소총 칸 보병(공용 제외)은 세력 분배표대로 유닛 id · 시드 해시로 결정적으로 고른다. 분배표 없으면 def.Weapon.</summary>
    public string? WeaponKeyFor(UnitDef def, string? en, int unitId, int seed)
    {
        if (def.Weapon != RifleSlot || def.Common || en == null || !FactionRifles.TryGetValue(en, out var m) || m.Count == 0) return def.Weapon;
        int total = m.Values.Sum();
        // 황금비 수열(Weyl) — 연속한 id 가 분배표에 고르게 퍼진다 (표본 1000명 오차 1% 미만). 시드는 시작점만 옮긴다.
        uint seedOff = (uint)seed * 0x85EBCA6Bu; seedOff ^= seedOff >> 13; seedOff *= 0xC2B2AE35u;
        uint u32 = (uint)unitId * 2654435769u + seedOff;
        int r = (int)(((ulong)u32 * (ulong)total) >> 32);
        foreach (var (k, wgt) in m) { if (r < wgt) return k; r -= wgt; }
        return def.Weapon;
    }
    public Dictionary<string, ArmorDef> Armors { get; set; } = new();
    public Dictionary<string, UnitDef> Units { get; set; } = new();
    public Dictionary<string, BuildingDef> Buildings { get; set; } = new();

    /// <summary>전투 대형 자료 (assets/data/formations.json — balance.json 옆, 없으면 내장 기본값).</summary>
    [JsonIgnore] public FormationRules Formations { get; set; } = FormationRules.Default;
    /// <summary>역할군 · 중대 전술 틀 자료 (assets/data/tactics.json).</summary>
    [JsonIgnore] public TacticRules Tactics { get; set; } = TacticRules.Default;

    /// <summary>원본 파일 내용의 해시 (리플레이가 같은 밸런스로 기록됐는지 확인용).</summary>
    [JsonIgnore] public string Hash { get; private set; } = "";

    /// <summary>현재 로드된 데이터. 시작 시 Load() 로 채운다.</summary>
    public static Defs Current { get; private set; } = null!;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "assets", "data", "balance.json");

    public static Defs Load(string? path = null)
    {
        path ??= DefaultPath;
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        string text = File.ReadAllText(path);
        var defs = JsonSerializer.Deserialize<Defs>(text, opts)
                   ?? throw new InvalidDataException($"빈 데이터 파일: {path}");
        foreach (var (k, v) in defs.Weapons) v.Key = k;
        foreach (var (k, v) in defs.Armors) v.Key = k;
        foreach (var (k, v) in defs.Units) v.Key = k;
        foreach (var (k, v) in defs.Buildings) v.Key = k;
        defs.Validate();
        string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        RankNames.Load(Path.Combine(dir, "ranks.json"));   // 계급 이름 (표시 전용)
        defs.Formations = FormationRules.Load(Path.Combine(dir, "formations.json"));   // 전투 대형 · 전술 틀 (시뮬레이션 자료 — 없으면 내장 기본값)
        defs.Tactics = TacticRules.Load(Path.Combine(dir, "tactics.json"));
        // 해시 = balance.json + formations.json + tactics.json (대형 · 전술 틀 자료도 시뮬레이션을 바꾼다 — 저장 · 리플레이 불일치 경고용). 없는 파일은 빈 내용.
        defs.Hash = HashOf(text, ReadOrEmpty(Path.Combine(dir, "formations.json")), ReadOrEmpty(Path.Combine(dir, "tactics.json")));
        Current = defs;
        return defs;
    }

    static string ReadOrEmpty(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
        catch (IOException) { return ""; }
    }

    /// <summary>자료 해시: 파일 내용을 길이 머리 + 내용으로 이어 SHA-256 앞 16자 (파일 경계가 섞이지 않게).</summary>
    internal static string HashOf(params string[] texts)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in texts) sb.Append(t.Length).Append('|').Append(t);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }

    void Validate()
    {
        foreach (var u in Units.Values)
        {
            Require(u.Weapon == null || Weapons.ContainsKey(u.Weapon), $"유닛 {u.Key}: 무기 '{u.Weapon}' 없음");
            Require(u.Sidearm == null || (Weapons.ContainsKey(u.Sidearm) && u.SidearmRange > 0), $"유닛 {u.Key}: 보조 무기 '{u.Sidearm}' 없음 또는 sidearmRange <= 0");
            Require(u.Armor == null || Armors.ContainsKey(u.Armor), $"유닛 {u.Key}: 장갑 '{u.Armor}' 없음");
            Require(u.Melee == null || Weapons.ContainsKey(u.Melee), $"유닛 {u.Key}: 근접 무기 '{u.Melee}' 없음");
        }
        foreach (var (en, m) in FactionRifles)
            foreach (var (k, wgt) in m) Require(Weapons.ContainsKey(k) && wgt > 0, $"factionRifles.{en}: 무기 '{k}' 없음 또는 가중치 <= 0");
        foreach (var u in Units.Values)
        {
            Require(u.ArtAs == null || Units.ContainsKey(u.ArtAs), $"유닛 {u.Key}: artAs '{u.ArtAs}' 없음");
            Require(!u.MeleeOnly || (u.Melee != null && u.Weapon == u.Melee), $"유닛 {u.Key}: meleeOnly 는 주무기 = 근접 무기여야 함");
        }
        Require(CombatRules.SquadMax >= 2 && CombatRules.SquadMergeBelow >= 0 && CombatRules.SquadMergeBelow <= CombatRules.SquadMax, "combat.squadMax >= 2, 0 <= squadMergeBelow <= squadMax");
Require(CombatRules.SquadLeaderRank >= 0 && CombatRules.SquadLeaderRank <= CombatRules.RankKills.Length, "combat.squadLeaderRank 는 0..계급 수");
                Require(CombatRules.RankKills.Length > 0 && CombatRules.RankKills.Zip(CombatRules.RankKills.Skip(1), (a, b) => a < b).All(x => x), "combat.rankKills 는 오름차순");
        Require(Buildings.Values.Any(b => b.Headquarters), "시작 거점(headquarters) 건물이 하나는 있어야 함");
        foreach (var b in Buildings.Values.Where(b => b.Headquarters))
            Require(b.Capturable, $"건물 {b.Key}: headquarters 는 capturable 이어야 함 (세력이 점령한 채로 시작)");
        foreach (var g in Start.Force)
            foreach (var k in g.Units.Keys) Require(Units.ContainsKey(k), $"시작 병력 유닛 '{k}' 없음");
        Require(Buildings.Values.Any(b => b.Capturable), "점령 가능(capturable) 건물이 하나는 있어야 함");
        Require(Vision.BlockSize is >= 1 and <= 4, "vision.blockSize 는 1~4");
    }

    static void Require(bool ok, string msg)
    {
        if (!ok) throw new InvalidDataException(msg);
    }

    public WeaponDef? Weapon(string? key) => key != null && Weapons.TryGetValue(key, out var w) ? w : null;
    public ArmorDef? Armor(string? key) => key != null && Armors.TryGetValue(key, out var a) ? a : null;
    /// <summary>시작 거점 (대도시): 세력마다 하나씩 소유하고 시작한다.</summary>
    public BuildingDef Headquarters => Buildings.Values.First(b => b.Headquarters);
    /// <summary>지도에 놓을 수 있는 점령 시설 종류 (키 순). 지도 파일 site.type 의 후보.</summary>
    public IEnumerable<BuildingDef> SiteKinds => Buildings.Values.Where(b => b.Capturable);
    /// <summary>키로 점령 시설 정의 찾기 (없거나 점령 불가면 null).</summary>
    public BuildingDef? SiteKind(string? key) => key != null && Buildings.TryGetValue(key, out var b) && b.Capturable ? b : null;

    /// <summary>폰트 아틀라스용: 데이터에 들어있는 모든 표시 문자열.</summary>
    /// <summary>가장 큰 유닛 반경 (충돌 검사 범위 계산용).</summary>
    public float MaxUnitRadius => Units.Values.Max(u => u.Radius);

    public IEnumerable<string> AllText() =>
        Weapons.Values.Select(w => w.Name)
            .Concat(Armors.Values.Select(a => a.Name))
            .Concat(Units.Values.SelectMany(u => new[] { u.Name, u.Description }))
            .Concat(Buildings.Values.SelectMany(b => new[] { b.Name, b.Description }))
            .Concat(RankNames.AllText());
}
