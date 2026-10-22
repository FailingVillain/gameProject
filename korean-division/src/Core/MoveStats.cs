namespace KoreanDivision.Core;

/// <summary>
/// [측정 전용] 이동 · 겹침 해소 계수기 (docs/COLLISION_ANALYSIS.md, --collide). 시뮬레이션은 이 값을 읽지 않는다 — 저장 · 체크섬 없음.
/// </summary>
public sealed class MoveStats
{
    public long SepPushUnits;      // 겹침 해소로 위치가 바뀐 유닛 수 (누계)
    public long ChargesBroken, NearMissHits, PinEvents, PinnedTicks;   // [측정] 근접 탄착 제압 횟수 · 고정 시작 · 고정 사수 유닛-틱 (Suppression.cs)
    // [측정] 제압 재설계 2 (supp 2026-10-20, Suppression.cs): 세력별 (0..MaxF-1) 엎드림 · 고정 · 붕괴 유닛-틱, 붕괴 · 회복 수, 받은 제압 점수 (출처 SupSrc)
    public const int MaxF = 16;
    public readonly long[] ProneTicks = new long[MaxF], PinTicksF = new long[MaxF], BrokenTicks = new long[MaxF], Breaks = new long[MaxF], Surrenders = new long[MaxF], Rallies = new long[MaxF];
    public readonly long[] SuppBy = new long[MaxF * SupSrc.Count];
    public void AddSupp(int owner, int src, int amount) { if (amount > 0 && (uint)owner < MaxF) SuppBy[owner * SupSrc.Count + src] += amount; }
    public double SepPushSum;      // 그 밀림 거리 합 (칸)
    public long SepPairs;          // 겹친 쌍 수 (누계)
    public long SepClamped;        // 틱당 상한에 걸린 밀림 수
    public long CompanyMoves;      // 중대 깃발 이동 시작 (슬롯 배정) 수
    public long SlotAssign;        // 그때 배정된 슬롯 수
    public long ColumnEnter;       // 좁은 길 종대 전환 수
    public long FlagMulChange;     // 깃발 속도 배수 (0 · 0.5 · 1) 가 바뀐 횟수
    public long ArtilleryDeploys;  // 포병 대형 → 횡대 자동 전개 수 (CombatFormations.cs)
    public long TraverseShiftTicks;  // N10: 방열한 채 사계 중심을 돌린 틱 수 (Combat2.cs ShiftTraverse)
    public long TraverseRepacks;     // N10: 사계 밖 큰 각 (≥ traverseRedeployDeg) 으로 철수를 시작한 횟수
    public long HeavyGapSteps;       // arty3: heavy 간격으로 방렬 전 옆으로 비켜선 틱 수 (Arty3.cs)
    public long SpotterIndirectShots; // arty3: 야전포 간접 사격 발수 (관측 유닛 · 사선 없음)
    // 전술 틀 2단계 행동 (TacticDrill.cs) 누계
    public long DrillStarts, DrillEnds, DrillBounds, DrillManoeuvres, DrillCharges, DrillCounters, DrillTrench, DrillLifts, DrillPaused, TacticTransitions, DrillDecisions;
    public long CoverSeeks, CoverSeekTrench, OccupyOrders, OccupyTrenchSpots, OccupyCoverSpots;   // engage (2026-10-19): 엄폐 찾기 · 진지 점유 (CoverSeek.cs)
    public long AssaultSignal, AssaultOverwatch, AssaultFallback, AssaultBlocked;   // 결정 4: 돌격 허가 이유 (TacticDrill.cs AssaultClearance — 엔진 기동조 + 전술 AI 중대 돌격)
    public long MarchChargeGated, MarchChargeBlocked;   // N47: 행진 사격 → 돌격 전환 허가 · 막힘
    public long Dismounts, Mounts;   // N61: 하마 · 승마 전환 시작 수 (Dismount.cs)
    public long BayonetFixes, BladeDraws;   // N57: 착검 · 칼 뽑기 멈춤 시작 수 (Bayonet.cs)
    public long HeavyHeld;   // 중화기가 보병 앞선을 넘으려다 기다린 유닛 · 틱 (HeavyPacing.cs)
    public long OfficerHeld; // N12: 장교가 중대 보병 앞선을 넘으려다 기다린 틱 (HeavyPacing.cs PaceOfficer)
    public long GapRefills;        // 빈자리 점검으로 재배치된 병사 수
    public long ShortShifts, ZoneArrivals, RestAligns, ColumnExits;   // 대형 완화 (FormRelax.cs): 짧은 이동 평행이동 · 구역 도착 병사 · 비접적 휴식 정렬 · 종대 해제
    public long ContactCompanyMoves, Rotations, RotateStays, LooseAdvances, AiContactHolds;   // F1 2차: 접적 중대 이동 명령 · 제자리 방향 전환 · 그중 제자리 병사 · 선까지 전진 · 접적 AI 재명령 보류 (ContactF2.cs)
    public long PropShots, PropCoverShots, PropsBroken;   // F6 [측정]: 보병 표적 사격 수 · 그중 소품 엄폐가 걸린 수 (Props.cs)
    public long SlotReturns, ContactEnters, SpotPicks, SpotMoves, ZoneReturns, SpotReleases;   // F1: 도착 깃발 슬롯으로 다시 걷기 시작 · 접적 진입 · 사격 위치 고정 · 위치로 이동 · 구역 밖 복귀 · 고정 해제 (Contact.cs)
    public long CloseManual, CloseOrders;   // 대열 정비: 플레이어 명령 (중대 수) · 장교 판단 명령 수 (CloseRanks.cs)
    public long CloseChargeSeen, CloseChargeOrders, CloseMoveAttack;   // gapfill2: 돌격 감지로 바로 판단한 수 · 그 명령 수 · 공격 이동 그대로 슬롯에 보낸 병사
    public long CloseAssaultHeld;   // tweak1: 자기 돌격 중이라 미룬 장교 판단 수
    public long CoverSeekSkipAttack;   // tweak1: 공격 자세라 엄폐 찾기를 하지 않은 피격 병사 판단 수
    public long CloseMovers, CloseLate;     // 대열 정비로 슬롯에 보낸 병사 수 · 교전 중이라 미룬 병사 (정비 한 번마다 셈)
    public long FleeingChargeBlocked;               // 추격: 후퇴하는 적이라 막은 돌격 판단 수 (Pursuit.cs)
    public long[] CavWhy = new long[5];   // F4: 돌격 개시 사유 (후퇴 · 제압 · 승무원 · 고립 · 측면)
    public long CavReactSwitches;   // n61 측정
    public long CavChargeStarts, CavChargePointless, CavAutoBlocked, CavMeleeKills;   // F4 측정
    public long InfChargeFleeing, CavChargeFleeing; // 추격: 후퇴 중인 표적에 실제로 개시한 돌격 (보병 = 0 이어야 함)
    public long KillsFire, KillsMelee, KillsFleeingFire, KillsFleeingMelee;   // 사살 (근접 = 돌격 중인 공격자) · 그중 후퇴 중이던 희생자
    public long RetreatShots, KillsRetreatFire, RegroupHolds, Readvances, RegroupRejoins, CrewHoldFire;   // pursuit2: 후퇴 사격 발수 · 사살, 재정비 사수 판단 · 후퇴 뒤 재전진 (옛 = 곧바로 · 새 = 조건 충족), 포수 근접 위험 사격 보류
    public long CavChargeFront, CavChargeFlank, CavChargeRear, CavSmallHeld, CavScreens, CavFlankRoutes, CavRashFront, DrillMarchAdvances, DrillSweeps;   // 기병 AI: 돌격 접근 각 · 소규모 보류 · 물러남 · 측면 경유
    public long TaStarts, TaDone, TaFail, TaCancel, TaBounds, TaGrenades, TaCharges, TaConsolidations, TaFireOrders, TaAiStarts;   // N32 참호 돌격
    public long[] TaStageEntered = new long[6];
    public readonly SortedDictionary<string, long> TaCancelWhy = new(StringComparer.Ordinal);   // 취소 원인 (명령 형식 / 세력 / 단계) — 측정용
    public long CavOfficerHeldBack;   // cavcmd: AI 기병 돌격에서 장교를 뒤에 남긴 횟수
    public long OfficerChargeHeld, OfficerChargeEnded, OfficerChargeBlocked;   // N35: 돌격 중 장교가 병사 앞줄을 넘으려다 기다린 틱 · 중대 돌격이 끝나 장교 돌격을 끝낸 수 · 혼자라 막은 장교 돌격 (명령 · 자동)
    /// <summary>N35 탐침: 장교 돌격 개시 (경로별, Bad = 중대가 돌격 중이 아님 = 혼자) · 장교 권총 사격 (경로별, Bad = 자위 조건 밖).</summary>
    public readonly SortedDictionary<string, OfficerLine> OffCharge = new(StringComparer.Ordinal), OffShot = new(StringComparer.Ordinal), ScoutShot = new(StringComparer.Ordinal);   // ScoutShot: 정찰병 사격 (Bad = 자위 조건 밖)
    public sealed class OfficerLine { public long N, Bad; public double BodyDist, EnemyDist, BodyMax; }
    internal void AddOfficer(SortedDictionary<string, OfficerLine> d, string path, bool bad, float body, float enemy)
    {
        if (!d.TryGetValue(path, out var l)) d[path] = l = new OfficerLine();
        l.N++; if (bad) l.Bad++;
        l.BodyDist += Math.Max(0, body); l.EnemyDist += Math.Max(0, enemy); l.BodyMax = Math.Max(l.BodyMax, body);
    }
    public long StuckUps;          // 정체 단계 상승 (경로 다시 구함) 수
    public long MarchStart, MarchStop;   // 행진 사격 시작 · 종료 (유닛 또는 중대 단위)
    public readonly Dictionary<int, float> lastFlagMul = new();
    /// <summary>[측정 전용] 아군 오사 (같은 팀 · 동맹이 가한 피해) — 무기 종류별 (명중 수 · 피해량 · 사살 수). 시뮬은 읽지 않음 — 저장 · 체크섬 없음 (불러오면 0 부터).</summary>
    public readonly SortedDictionary<string, int> KillSources = new(StringComparer.Ordinal);   // [측정 전용] 사살 원인 (Combat3.CountKill)
    public readonly SortedDictionary<string, FriendlyFireLine> FriendlyFire = new(StringComparer.Ordinal);
    public sealed class FriendlyFireLine { public int Hits; public int Kills; public double Damage; }
    /// <summary>[측정 전용 · T19b] 포 (병과 gun) 발사 수 — 세력별 (0..MaxF-1) 무기 키 → 발수. 캠페인 포탄 정산 (MissionResults.PlayerGunShots) 이 읽는다. 시뮬은 읽지 않음 — 저장 · 체크섬 없음 (불러오면 0 부터, FriendlyFire 와 같음).</summary>
    public readonly SortedDictionary<string, int>[] GunShots = Enumerable.Range(0, MaxF).Select(_ => new SortedDictionary<string, int>(StringComparer.Ordinal)).ToArray();
    internal void AddGunShot(Unit u)
    {
        if (u.Def.Category != "gun" || u.Weapon == null || (uint)u.Owner >= MaxF) return;
        var d = GunShots[u.Owner];
        d[u.Weapon.Key] = d.GetValueOrDefault(u.Weapon.Key) + 1;
    }
}

public sealed partial class World
{
    /// <summary>[측정 전용] 이동 · 겹침 해소 계수기 — 시뮬레이션 상태 아님.</summary>
    public readonly MoveStats Stats = new();
}
