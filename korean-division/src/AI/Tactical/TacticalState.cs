using System.Numerics;

namespace KoreanDivision.Core;

// 전술 AI (TacticalAi) 의 세력별 상태 = 층 사이 블랙보드. 모든 필드는 AI 컨트롤러와 함께 스냅샷에 저장된다 (ObjCodec — 정수 · 실수 · 문자열 · 목록만).
// 작업용 목록 (List<Unit>) 은 저장에서 빠지고 매 호출 다시 만든다. 층: 세력 전략 → 지시 그룹 → 연대 (전술) → 대대 → 중대 (실행). docs/TACTICAL_AI_DESIGN.md.

/// <summary>AI 가 다루는 단위의 병과 역할.</summary>
public enum TacKind : byte
{
    Line = 0,       // 소총 중대 (월드 Company, 깃발 횡대)
    Fire = 1,       // 화력 지원: 기관총 · 대전차소총 · 직사포 · 저격수 (거리 두고 거치 · 사수)
    Artillery = 2,  // 곡사포 (관측된 적을 사거리에 넣고 방열, 주공 연대 지원 — 준비사격 · 연신)
    Cavalry = 3,    // 기병 (차장 · 측면 경계 → 제압 · 흐트러진 보병 돌격)
    Armor = 4,      // 전차 · 장갑차 (대대와 함께 전진)
    Wagon = 5,      // 보급 마차 · 비무장 (전선 뒤를 따라감)
    Scout = 6,      // 정찰병 (관측자 — 자율 정찰 유닛은 명령하지 않는다)
}

/// <summary>상위 층이 내린 과제.</summary>
public enum TacTask : byte { None = 0, Attack = 1, Defend = 2, Reserve = 3, Fallback = 4 }

/// <summary>중대 층의 현재 동작.</summary>
public enum TacState : byte { Idle = 0, Advance = 1, Approach = 2, Engage = 3, Bound = 4, Assault = 5, Defend = 6, Fallback = 7, Reserve = 8, Charge = 9 }

/// <summary>연대 전술이 대대 · 중대에 주는 역할.</summary>
public enum TacRole : byte
{
    Line = 0,        // 구분 없음 (접근 중 · 1단계 동작)
    BaseOfFire = 1,  // 화력 기반: 교전 거리에서 정지 사격만 (약진 · 돌격 안 함)
    Manoeuvre = 2,   // 기동: 약진 · 돌격 (준비사격 중에는 사격 진지에서 대기)
    MainLine = 3,    // 주저항선 (종심 방어)
    Counter = 4,     // 역습 예비 (종심 방어)
}

/// <summary>연대 국면.</summary>
public enum RegPhase : byte { None = 0, Approach = 1, Prep = 2, Assault = 3, Defend = 4, Counter = 5 }

/// <summary>S1 기동 계획 (stage 3A): 0 = 아직 안 고름.</summary>
public enum ManPlan : byte { None = 0, Frontal = 1, Flank = 2, Pincer = 3, Schwerpunkt = 4 }

/// <summary>기동 계획의 제대 (중대 단위).</summary>
public enum ManElement : byte { None = 0, Fix = 1, Manoeuvre = 2 }

/// <summary>미션 목표에서 끌어낸 과제 (S14, 저장 안 함 — 미션 상태의 순수 함수).</summary>
public enum AiObjectiveKind : byte { None = 0, Take = 1, Hold = 2, Escort = 3, Intercept = 4, Destroy = 5 }

/// <summary>중대 층 단위: 월드 중대 (CompanyId) 또는 중대 없는 분견대 (Units).</summary>
public sealed class TacGroup
{
    public int Id;
    public TacKind Kind;
    public int CompanyId;                      // 0 = 분견대
    public readonly List<int> Units = new();   // 분견대 구성원 · 중대 편성 대기 중인 구성원 (Id 순)
    public bool Pending;                       // FormCompany 명령의 실행을 기다림
    public int Directive;                      // 지시 그룹 (TacDirective.Id, 0 = 세력 기본)
    public int Battalion;                      // TacBattalion.Id (0 = 전략 직속)
    public int Supports;                       // 포병 · 기병: 지원하는 연대 (TacRegiment.Id, 0 = 없음)
    public int Initial;                        // 처음 병력
    public int Alive;                          // 마지막 평가 때 병력
    public Vector2 Center;                     // 마지막 평가 때 무게중심
    public TacTask Task;
    public TacRole Role;
    public bool HoldFire;                      // 준비사격 중: 약진 · 돌격 금지 (기동 제대는 사격 진지에서 대기)
    public Vector2 TaskPoint, TaskDir;
    public float TaskWidth;
    public TacState State;
    public long StateTick, OrderTick = -100000, FallbackUntil, NoRetreatUntil, NoChargeUntil;
    public Vector2 OrderPoint;
    public float BoundDist;
    /// <summary>Dug = 방어 진지를 (지도) 참호로 잡았다 (2026-10-19 건설 제거 전에는 개인 참호를 팠다).</summary>
    public bool Dug, Spent;
    /// <summary>마지막 스스로 후퇴의 원인: 0 없음 · 1 병력 (사상 임계) · 2 제압 (장교 판단 확률, N25). 관찰 · 측정용.</summary>
    public int RetreatCause;
    /// <summary>진지 점유 명령을 낸 지점 (engage — 참호 · 엄폐 거점, Dug 일 때).</summary>
    public Vector2 OccPost;
    // 분대 교대 약진 (사격과 기동): 0 = 안 함, 1 = 짝수 분대 이동 중, 2 = 홀수 분대 이동 중
    public int SquadBound;
    public long SquadBoundTick;
    public Vector2 SquadBoundPoint;
    // 전초 분대 (종심 방어): 분대 Id, 상태 0 없음 · 1 나감 · 2 철수함
    public int OutpostSquad, OutpostState;
    public long OutpostTick;
    // 포병: 강제 표적 (Unit Id, 0 = 엔진 자동) · 연신 여부
    public int FireTargetId;
    public bool Lifted;
    // 위치 고르기 기억 (TacMap — 기준점이 크게 바뀔 때만 다시 고른다)
    public Vector2 PickBase, PickResult;
    public int PickKind;               // 0 없음 · 1 공격 사격 진지 · 2 주저항선 · 3 화력 진지
    // 도하 (TacticalAi.Crossing.cs): 단계 0 없음 · 1 집결 · 2 도하 중 · 3 교두보 · 4 화력 기반 · 5 건넘 (보통 공격), 계획 연대 · 단계 지점 (집결 · 화력 기반 · 교두보 칸)
    public int CrossStage, CrossRegiment, CrossLane;   // CrossLane = 건널 곳 0 · 1 (둘째 건널 곳)
    public Vector2 CrossPoint;
    public float CrossSlot;            // 강둑 사격 칸의 옆 위치 (건널 곳 기준, 도하 순서 정렬용)
    // 역습 교리 (TacticalAi.Counter.cs): 돌격 국면 진입 틱 (0 = 아님)
    public long CounterAssaultTick;
    // 사격 추격 (pursuit 2026-10-19): 시작 틱 (0 = 아님) · 시작 위치 (자기 선) · 재정비 끝
    public long PursuitTick, PursuitHaltUntil;
    public Vector2 PursuitOrigin;
    // 기병: 측면 경유 단계 (0 없음 · 1 경유점으로) · 경유점 · 표적 Id · 표적 없이 적 앞에 머문 시작 틱 (0 = 아님)
    public byte CavStage;
    public Vector2 CavWay;
    public int CavTarget;
    public long CavStallSince;
    // 후퇴 뒤 재정비 (pursuit2): 재정비 끝 최소 틱 (0 = 재정비 아님) · 다음 판단 틱 · 재정비 시작 때 병력 (증원 판단)
    public long RegroupUntil, RegroupCheck;
    public int RegroupAlive;
    // S1 기동 계획 (stage 3A): 제대 · 측면 이동 단계 (0 없음 · 1 경유점으로 · 2 돌격 진지로 · 3 진지에서 대기 — 연대가 풀면 0) · 경유점 · 돌격 진지 · 진지에서 볼 방향 · 도착 틱
    public ManElement Element;
    public byte ManStage;
    public Vector2 ManWay, ManGoal, ManFace;
    public long ManArrive;
    // 3B 저탄약 교대 (TacticalAi.Reserve3B.cs): 교대 중 끝 틱 (0 = 아님) · 나가는 쪽 (예비 위치로 걸어감, 교전 안 함) — 들어오는 쪽은 RotateOut = false
    public long RotateUntil;
    public bool RotateOut;
}

/// <summary>대대 층 단위 (소총 중대 최대 4개 + 붙은 화력 · 기갑 · 마차). 월드 대대와 연결되면 WorldId · Number.</summary>
public sealed class TacBattalion
{
    public int Id;
    public int Regiment;               // TacRegiment.Id
    public int WorldId, Number;        // 월드 대대 (Battalion.Id · 번호, 0 = AI 내부 묶음 — 번호 1~9 를 넘으면)
    public int Initial;
    public Vector2 Center;
    public int Alive;
    public TacTask Task;
    public TacRole Role;
    public bool HoldFire;
    public bool Split;                 // 연대에 앞 대대가 하나뿐: 대대 안에서 중대를 화력 기반 · 기동으로 나눈다
    public int BaseGroup;              // Split 일 때 화력 기반 중대 (TacGroup.Id)
    public int FlankGroup;             // 대등전에서 측면 연장으로 투입한 예비 중대 (TacGroup.Id)
    public Vector2 Point, Dir;
    public float Width;
    public int ReserveGroup;           // 예비 중대 (TacGroup.Id, 0 = 없음)
    public bool ReserveCommitted;      // 예비 중대를 이미 투입함 (다시 고르지 않는다)
    public bool Spent;                 // 한 번 무너져 후퇴한 대대 (이후 예비)
    public long FallbackUntil;
    // 3B (TacticalAi.Reserve3B.cs): 다음 저탄약 교대 가능 틱 · 예비 재지정 판단용 조용함 시작 틱 (0 = 접촉 중)
    public long RotateTick, CalmSince;
    // 3B 사건 투입 (S3): 틈 · 돌파 · 측면 지점으로 보낸 예비 중대 (TacGroup.Id, 0 = 없음) · 지점 · 끝 틱 · 공격 (돌파 = 국지 역습) 여부
    public int PlugGroup;
    public Vector2 PlugPoint;
    public long PlugUntil;
    public bool PlugAttack;
}

/// <summary>연대 (AI 묶음: 대대 2~4 — 연대 전술 층). 캠페인 연대 편제가 생기면 연결한다.</summary>
public sealed class TacRegiment
{
    public int Id;
    public int Directive;
    public int Initial, Alive;
    public Vector2 Center;
    public TacTask Task;
    public bool Main;                  // 주공 (포병 지원 · 정면 집중)
    public Vector2 Point, Dir;
    public float Width;
    public RegPhase Phase;
    public long PhaseTick;
    public int ReserveBattalion;       // 예비 / 역습 대대 (TacBattalion.Id)
    public bool ReserveCommitted;
    public long CounterUntil;          // 역습 중 (방어)
    public Vector2 CounterPoint;
    public bool Spent;
    public long FallbackUntil;
    public bool HasFireTarget;         // 지원 포병의 표적 기준점 (적 전선 / 연신 뒤 후방)
    public Vector2 FireTarget;
    public bool Lifted;
    // 도하 계획 (TacticalAi.Crossing.cs): 국면 0 없음 · 1 화력 기반 · 2 도하 · 3 교두보 · 4 계속 (화력 기반도 건넘) · 5 끝
    public int CrossPhase;
    public bool CrossReslotted;        // 강둑 칸을 다시 골랐다 (한 번)
    public int CrossEnemyBest;         // 화력 기반 국면: 출구 둘레 보이는 적 기준 수 (더 보이면 올리고, progressDrop 넘게 줄면 진척 · 기준 갱신)
    public long CrossProgressTick;     // 마지막 진척 틱
    public long CrossTick, CrossCheckTick, CrossFireTick, CrossStartTick, CrossBridgeTick, CrossHoldUntil, CrossLaunchTick;
    public Vector2 CrossEntry, CrossExit, CrossDir;    // 건널 곳 입구 (이쪽 둑) · 출구 (건너편 둑) · 축
    public float CrossWidth;
    public Vector2 CrossEntry2, CrossExit2;             // 둘째 건널 곳 (같은 띠의 반대쪽 끝, 폭 CrossWidth2 > 0 일 때만)
    public float CrossWidth2;
    public readonly List<int> CrossQueue = new();      // 도하 순서 (TacGroup.Id): 도하 중대 → 화력 기반 중대 (끝 CrossFireBase 개)
    public int CrossNext, CrossFireBase, CrossBridgeSlots, CrossSide, CrossBridgeSlots2, CrossSide2;   // CrossSide = 교두보를 넓히는 쪽 (+1 / −1, 옆 방향)
    // 역습 교리 (TacticalAi.Counter.cs): 0 없음 · 1 고정 사격 · 2 일제 돌격, 국면 시작 틱
    public int CounterStage;
    public long CounterStageTick;
    // S1 기동 계획 (stage 3A, TacticalAi.Manoeuvre.cs): 종류 · 측면 쪽 (Perp(Dir) 부호) · 단계 (0 없음 · 1 진행 = 고정 + 기동 · 2 돌격 허가 (풀림) · 3 끝/취소)
    public ManPlan Plan;
    public int PlanSide;
    public byte PlanStage;
    public bool PlanPre, PlanPreTried;   // 접촉 전에 (미션 목표 · 공격 구역 기준으로) 정한 계획 (접촉하면 고침) · 이번 접근에서 이미 시도함
    public long PlanTick, FixTick, ArriveTick, ReleaseTick, PlanDeadline;
    public Vector2 PlanAxis, PlanWay, PlanEnemy;   // 측면: 돌격 진지 · 경유점 · 적 측면 끝 / 중점: 구역 중심
    public float PlanWidth, PlanDetour, PlanFront, PlanEta, PlanOdds;   // 중점 정면 폭 · 우회 경로 · 정면 거리 · 도착 예정 (초) · 고를 때 병력비
    public int PlanSeen;                                                // 고를 때 보인 적 수 (측정용)
}

/// <summary>지시 그룹 (미션 병력별 태세 · 구역; 0 = 세력 기본). 세력 전략이 그룹마다 따로 판단한다.</summary>
public sealed class TacDirective
{
    public int Id;
    public AiPosture Posture;
    public bool HasArea;
    public Vector2 Area;
    public float AreaRadius;
    public readonly List<string> Forces = new();
    public readonly List<int> Units = new();
    public bool HomeSet;
    public Vector2 Home, Dir;
    public int Initial, Alive;
    // 그룹 블랙보드 (전략이 2초마다 갱신)
    public AiPosture Effective;
    public bool HasEnemy, Culminated;
    public Vector2 EnemyPos, EnemyNear, LineCenter, Target;
    public float EnemyWidth;
    public long EnemySeenTick, ContactSince;
    public int ReserveRegiment;
    public bool ReserveRegimentCommitted;
    public int CounterVotes;            // 방어 → 반격: 연속 전략 판단 수 (안개로 보이는 적 수가 흔들려 한 번 반짝 반격하던 문제)
    public long CounterUntil;           // 반격 유지 시각
    public bool Withdrawn;              // 목표 포기 · 철수함 (stage 3A, 사용자 결정 5 — 다시 공격하지 않는다)
    public long WithdrawTick;
    public Vector2 WithdrawPoint;
}

/// <summary>세력 하나의 전술 AI 상태.</summary>
public sealed class TacFaction
{
    public int Faction;
    public int Phase;                   // 0 = 처음 · 1 = 중대 편성 명령 대기 · 2 = 운용
    public AiProfile Profile = new();
    public string? DirectiveProfile;    // 미션 지시 (SetDirective)
    public bool DirectiveSet, HasArea;
    public bool SkipUnarmed;            // 비무장 유닛 (호송 마차 등) 은 편성에서 뺀다 — 미션 보조 AI 가 맡는다
    public AiPosture Posture;
    public Vector2 Area;
    public float AreaRadius;
    public Vector2 Home, Front, Dir;
    public int FlankSign = 1;
    public float RifleRange;            // 소총 사거리 (거리 상수의 기준 — 정의에서 읽음)
    public int NextGroupId = 1, NextBattalionId = 1, NextRegimentId = 1;
    public readonly List<TacGroup> Groups = new();
    public readonly List<TacBattalion> Battalions = new();
    public readonly List<TacRegiment> Regiments = new();
    public readonly List<TacDirective> Directives = new();   // [0] = 세력 기본 (Id 0)
    public int LastUnitId;              // 편성에 넣은 가장 큰 유닛 Id (증원 편입 검사)
    public bool PendingIntake;          // 증원 중대 편성 명령을 기다림
    public long SetupTick, LastContactTick;
    public bool HasSpotted;
    public Vector2 SpottedPos;
    public long SpottedTick;
    // 세력 대표값 (가장 큰 지시 그룹 — 관찰 · 테스트용)
    public bool HasEnemy;
    public Vector2 EnemyPos, LineCenter, Target;
    public AiPosture Effective;
    // 관찰용 누계 (밸런스 · 테스트 보고)
    public int Orders, Charges, CavalryCharges, TrenchMoves, Fallbacks, ArtilleryMoves, FireHolds, ReserveCommits;
    /// <summary>[통계] N22 직사포 사선 막힘 재배치 수.</summary>
    public int GunLosMoves;
    /// <summary>[측정] 관측자 노출 회피로 지점을 바꾼 수 (tweak1).</summary>
    public int ObserverAvoids;
    /// <summary>[측정] 직사포 (야전포) 가 도착해 표적 쪽으로 방열한 수 (tweak1).</summary>
    public int GunFaceDeploys;
    public int Preps, Lifts, SquadBounds, Outposts, OutpostWithdrawals, Counterattacks, Reinforced, Culminations;
    public int CrossPlans, Crossings, CrossLaunches, CrossFireBases, Bridgeheads, CounterAssaults;
    public int Pursuits, PursuitHalts;   // 사격 추격 시작 · 한계 정지 (pursuit)
    public int Regroups, RegroupHolds, Readvances;   // 후퇴 뒤 재정비 시작 · 조건 미달 사수 판단 · 재전진 (pursuit2)
    // stage 3A: 미션 철수 문턱 · 목표 고수 정도 (음수 = 프로필 값), 계획 누계 · 계획 기록 (연대마다 끝날 때 PlanLogStride 개씩 — 측정용)
    public float MissionWithdrawAt = -1f, MissionCommitment = -1f;
    public int PlansFrontal, PlansFlank, PlansPincer, PlansSchwerpunkt, PlanReleases, PlanAborts, Withdrawals;
    public readonly List<int> PlanLog = new();
    public int FallbacksSupp;            // 중대 스스로 후퇴 중 제압 판단 (N25) 으로 난 것 (Fallbacks 에 포함)
    // 3B 누계 (측정용): 저탄약 교대 · 사건 투입 (틈 · 돌파 · 측면) · 예비 재지정 · 고정 제대 밀기 (3A 남은 일 1)
    public int AmmoRotations, PlugGaps, PlugBreaches, PlugFlanks, ReserveRedesignations, FixPushes;
}
