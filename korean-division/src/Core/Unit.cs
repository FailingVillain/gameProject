using System.Numerics;

namespace KoreanDivision.Core;

public enum UnitOrder { Idle, Move, AttackMove, Attack, Hold, GroundFire }

/// <summary>
/// 같은 이동 명령을 받은 유닛 묶음. 경로(구역 회랑)를 공유하고, 인원수에 맞춰 도착 반경이 넓어진다.
/// 큰 그룹(FormationMin 기 이상)은 중심에 가장 가까운 유닛(리더)의 위치로 그룹 경로를 한 번만 계산하고,
/// 각 유닛은 시작 때의 상대 위치를 압축한 오프셋(FormOff)을 따라 목표 둘레에 퍼지도록 조준한다.
/// </summary>
public sealed partial class MoveGroup
{
    public const int FormationMin = 12;
    public readonly Vector2 Target;    // 요청된 목표 (맵이 바뀌면 이 값으로 목표 칸을 다시 해석한다)
    public Vector2 Goal;               // 실제 도착 지점 (막힌 칸이면 가장 가까운 열린 칸)
    public FlowField Field;            // 목표 표시용 껍데기 (Goal 과 같다)
    public float ArriveRadius;
    public readonly int MemberCount;
    internal bool Big, Inited;
    internal List<Unit>? Members;
    internal Unit? Leader;
    internal NavRoute? Route;          // 그룹 경로 (큰 그룹)

    public MoveGroup(World world, Vector2 target, int members)
    {
        Target = target;
        MemberCount = members;
        Goal = FlowField.ResolveGoal(world.Map, target).Goal;
        Field = new FlowField(Goal);
        ArriveRadius = members <= 1 ? 0.02f : 0.25f + 0.4f * MathF.Sqrt(members);
        if (members >= FormationMin) { Big = true; Members = new List<Unit>(members); }
    }

    internal void Retarget(World world)
    {
        Goal = FlowField.ResolveGoal(world.Map, Target).Goal;
        Field = new FlowField(Goal);
        Route = null;
        RouteL = null;
    }

    /// <summary>처음 이동하는 순간 한 번: 등록된 구성원의 중심 · 리더 · 압축 오프셋을 정한다.</summary>
    internal void Init()
    {
        Inited = true;
        if (!Big || Members == null || Members.Count < FormationMin / 2) { Big = false; Members = null; return; }
        Vector2 c = Vector2.Zero;
        int n = 0;
        foreach (var m in Members) if (m.Alive) { c += m.Pos; n++; }
        if (n == 0) { Big = false; Members = null; return; }
        c /= n;
        float best = float.MaxValue;
        float maxOff = ArriveRadius * 0.6f;
        const float k = 0.4f;
        foreach (var m in Members)
        {
            if (!m.Alive) continue;
            float d = Vector2.DistanceSquared(m.Pos, c);
            if (d < best) { best = d; Leader = m; }
            Vector2 off = (m.Pos - c) * k;
            float l = off.Length();
            if (l > maxOff) off *= maxOff / l;
            m.FormOff = off;
            m.FormGroup = this;
        }
        Members = null;
    }
}

public sealed partial class Unit : Entity
{
    public readonly UnitDef Def;

    // 장비 슬롯 (기본 장비로 시작, 추후 교체 가능)
    public WeaponDef? Weapon;
    public ArmorDef? Armor;

    public Vector2 Velocity;
    public float Facing = -MathF.PI / 2;
    /// <summary>[렌더 전용] 마지막 틱 직전 위치 — 화면이 틱 사이를 보간한다 (Game 이 Step 전에 씀). 시뮬레이션은 읽지 않음 · 저장 안 함.</summary>
    public Vector2 RenderPrev;

    public UnitOrder Order { get; internal set; } = UnitOrder.Idle;
    internal MoveGroup? Group;          // 현재 따라가는 이동 그룹
    internal MoveGroup? LastGroup;      // 마지막으로 도착한 그룹 (도착 판정 전파용)

    // 길찾기 상태 (Nav 가 쓴다 — 결정적 시뮬레이션 상태이므로 체크섬에는 들어가지 않지만 모두 틱 · 입력의 함수)
    internal object? NavToken;          // 이 상태가 속한 목적 (그룹 또는 추격 표적) — 바뀌면 초기화
    internal NavRoute? NavRoute;
    internal int NavSeg, NavEpoch = -1;
    internal int NavRawX = -1, NavRawY = -1, NavGX, NavGY;   // 목표(원본 타일 · 해석된 타일)
    internal long NextLosTick, NextPersonalTick;
    internal bool LosOk;
    internal Vector2 LosGoal;
    internal MoveGroup? FormGroup;
    internal Vector2 FormOff;

    // 전투
    public Entity? Target { get; internal set; }
    internal float Cooldown;
    /// <summary>점사 남은 발수 (0 = 점사 아님, 다음 사격이 새 점사 시작) — Bursts.cs, FormatVersion 24.</summary>
    [SnapExt] internal int BurstLeft;
    internal long NextAcquireTick;
    // 사격 전후 지연 (틱 단위, 결정적 상태): 조준 → 첫 발, 교전 종료 → 이동 시작
    internal int AimTarget;             // 조준을 마쳤거나 조준 중인 표적 Id (움직이면 0)
    internal int AimLeft;               // 첫 발까지 남은 조준 틱
    internal int HoldLeft;              // 이동 재개까지 남은 정지 틱
    internal long EngagedTick = -1000;  // 마지막으로 사거리 안에서 교전(조준 · 사격)한 틱
    public long LastMoveTick { get; internal set; } = -1000;   // 마지막으로 이동한 틱
    internal Vector2 LeashOrigin;       // 자동 교전을 시작한 지점 (추격 한계 기준)
    public long LastShotTick { get; internal set; } = -1000;   // 마지막 사격(공구 휘두르기 포함) — 사격 동작 표시용
    public long LastOrderTick { get; internal set; } = -1000;  // 마지막으로 명령을 받은 틱 — '움찔' 반응 표시용
    internal int ReactDelay;            // 명령을 받고 실제로 움직이기까지 남은 틱 (조작감: 짧은 반응 지연)

    public Unit(int id, UnitDef def, int owner, Vector2 pos) : base(id, owner, pos)
    {
        Def = def;
        RenderPrev = pos;
        Hp = def.Hp;
        Weapon = Defs.Current.Weapon(def.Weapon);
        Armor = Defs.Current.Armor(def.Armor);
        InitSupply();   // 탄약 가득 (SupplyUnit.cs)
        if (def.MeleeOnly) ChargeOnly = true;   // 마적 잡병: 사격 없이 근접만 (저장 상태 ChargeOnly 로 복원됨)
    }

    partial void InitSupply();

    public override string Name => Def.Name;
    public override float MaxHp => Def.Hp;
    public override float ArmorValue => Armor?.Armor ?? 0;
    public override int Sight => Def.Sight;
    public override bool Indestructible => false;
    public float Radius => Def.Radius;

    /// <summary>[렌더러] 조준 중 (표적을 겨누지만 아직 첫 발 전). 이 동안은 서서 표적 쪽을 본다.</summary>
    public bool Aiming => AimLeft > 0 && Target != null;
    /// <summary>[렌더러] 이번 틱 실제로 이동했는가 (추격 포함). 걷기 프레임 판정용 — IsMoving 은 그룹 이동 전용이라 추격은 false.</summary>
    public bool Walking => Velocity != Vector2.Zero;
    /// <summary>[렌더러] 교전 직후 정지 대기 중 (이동 재개 전).</summary>
    public bool HoldingPosition => HoldLeft > 0;
    public bool IsMoving => Group != null && Velocity != Vector2.Zero;
    /// <summary>사격 자세 (사거리 안에서 조준 중 · 사격 중). 겹침 해소에서 밀리지 않는다 (ROADMAP §2-1 ①).</summary>
    public bool Engaging => Target != null && AimTarget != 0;
    /// <summary>겹침 해소에서 밀리지 않는 유닛: 사수 (참호 안 포함) · 방열 상태(방열 · 철수 중 포함) · 사격 자세.</summary>
    public bool Immovable => Order == UnitOrder.Hold || Order == UnitOrder.GroundFire || DeployState != 0 || Engaging;
    public Vector2? Destination => Group?.Goal;

    public override float EdgeDistance(Vector2 p) => MathF.Max(0, Vector2.Distance(p, Pos) - Radius);
}
