using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 중대 전술 틀 2단계 — 역할별 행동 · 전환 (docs/TACTICAL_AI_STAGE3_PLAN.md '전술 대형 (역할군) 결정' ②, N4).
/// 판단은 중대 지휘관 (N3 CommandRanks.cs): DecideInterval (경험치) 간격으로, 인계 중이면 멈추고, 지휘 능력이 허용하는 틀만 (TacticAllowed —
/// 부사관 대리 = 화력-기동, 지휘 없음 = 틀 없음). 행동은 자료 (tactics.json templates.*.groups.*.act + behaviour) 로 정한다:
///   hold 서서 사격 (기관총 거치) · bound 사격조 분대 교대 약진 · manoeuvre 엄폐를 따라 각도 위치로 → 제압 · 근접 시 돌격 (+ 연신)
///   follow 공격 이동으로 뒤따름 · support 위협 우선 사격 · command 화력조 뒤 · trench 참호 자리 점유 · reserve 예비 역습.
/// 국면: Idle (행군 · 대기 — 1단계 정지 배치 그대로) → 접촉하면 Engage (종심 방어는 멈춰 있으면 Defend) → 접촉이 disengageSec 끊기면 Idle (중대 대형으로 다시 정렬).
/// 일반 이동 명령 (Move · 속보 — 후퇴 등) 으로 걷는 중대는 도착할 때까지 행동하지 않는다 (Passive). 공격 이동 · 사수 = 행동 허용.
/// 엔진 자동 행동이라 명령 기록 없이 상태에서만 정해진다 (결정적: 중대 목록 순 · 구성원 Id 순, 상태는 저장 · 체크섬).
/// </summary>
public sealed class DrillDef
{
    public int DecideTicks { get; set; } = 30;
    public float ContactSec { get; set; } = 1.5f;
    public float DisengageSec { get; set; } = 10f;
    public float BoundLength { get; set; } = 6f;
    public float BoundMaxSec { get; set; } = 10f;
    public float RifleStopFrac { get; set; } = 0.55f;
    public float BoundSuppression { get; set; } = 0.6f;
    public float BoundCoverGain { get; set; } = 0.3f;
    public float ManoeuvreCover { get; set; } = 0.5f;
    public float ManoeuvreOpenSupp { get; set; } = 0.8f;
    public int CoverSearch { get; set; } = 3;
    public float ChargeRange { get; set; } = 6f;
    public float ChargeSuppression { get; set; } = 0.4f;
    public float ChargeMaxDist { get; set; } = 12f;
    public float ChargeOdds { get; set; } = 1.6f;
    /// <summary>T24 ③ (D08): 기동조 돌격이 가깝지 않을 때 (chargeRange 밖) 수 우세 (chargeOdds) 만으로 넘어도 표적 둘레 적 평균 제압이 이 이상이어야. 0 = 끔.</summary>
    public float ChargeMinSuppression { get; set; } = 0f;
    public float ChargeStatsRadius { get; set; } = 10f;
    public float ChargeMgRadius { get; set; } = 8f;
    public float ReplanDist { get; set; } = 5f;
    public float ChargeSec { get; set; } = 12f;
    public float LiftDist { get; set; } = 8f;
    public float LiftRadius { get; set; } = 5f;
    public float CounterRange { get; set; } = 6f;
    public int CounterMin { get; set; } = 3;
    public float TrenchSeek { get; set; } = 6f;
    public float CommandBack { get; set; } = 4f;
    public float CommandSlack { get; set; } = 5f;
    public float FollowFrac { get; set; } = 0.35f;
    public float SuperioritySupp { get; set; } = 0.30f;
    public float SuperiorityOdds { get; set; } = 2f;
    public float SuperiorityRadius { get; set; } = 4f;
    public int SuperiorityFew { get; set; } = 0;
    public float SuperiorityDelaySec { get; set; } = 12f;
    public float PatienceSec { get; set; } = 90f;
    public float ApproachFrac { get; set; } = 0.85f;
    public float EngageFrac { get; set; } = 0.72f;
    // 결정 4 (사용자 승인 2026-10-19): 공격 돌격 허가 — 화력 우세 신호 (보이는 적 제압 ≥ superioritySupp) 또는 엄호 확인 (돌격 표적 둘레를 최근 overwatchSec 초 안에 쏜
    // 아군 overwatchMin 명 이상) + 병력비 (돌격 인원 ≥ 표적 둘레 적 × assaultMinOdds). 아니면 교전 assaultFallbackSec 초 뒤에만 옛 조건으로 (예비 타이머).
    public bool AssaultGate { get; set; } = true;
    public float AssaultMinOdds { get; set; } = 1.0f;
    public float OverwatchSec { get; set; } = 4f;
    public float OverwatchRadius { get; set; } = 8f;
    public int OverwatchMin { get; set; } = 3;
    public float AssaultFallbackSec { get; set; } = 150f;
    /// <summary>N47 행진 사격 → 돌격 허가의 예비 타이머 시작: "phase" = 중대 전술 틀 국면 틱 (국면 없으면 병사의 공격 이동 명령 틱), "order" = 늘 명령 틱.</summary>
    public string MarchChargeSince { get; set; } = "phase";
}

/// <summary>돌격 허가 이유 (결정 4 — 측정 · 테스트용, 상태 아님).</summary>
public enum AssaultWhy : byte { Blocked = 0, Signal = 1, Overwatch = 2, Fallback = 3, Ungated = 4 }

public enum DrillPhase : byte { Idle = 0, Engage = 1, Defend = 2 }

/// <summary>역할 행동 (자료 act 문자열).</summary>
public enum DrillAct : byte { Hold = 0, Bound = 1, Manoeuvre = 2, Follow = 3, Support = 4, Command = 5, Trench = 6, Reserve = 7 }

/// <summary>중대 행동 상태 (저장 · 체크섬 — Company.Drill).</summary>
public sealed class DrillState
{
    public DrillPhase Phase;
    /// <summary>일반 이동 명령으로 걷는 중 (도착할 때까지 행동 안 함).</summary>
    public bool Passive;
    public long PhaseTick, ContactTick;
    /// <summary>적 위치 (구성원 표적의 평균, 마지막 값 유지) · 종심 방어 예비의 원위치.</summary>
    public Vector2 Enemy, Home;
    /// <summary>약진 중인 사격조 분대 Id (0 = 없음) · 마지막으로 약진한 분대 Id · 시작 틱.</summary>
    public int BoundSquad, BoundLast;
    public long BoundTick;
    /// <summary>기동조 · 예비 상태: 0 없음 · 1 경유점으로 · 2 목표로 · 3 자리 (사격) · 4 돌격 · 5 (예비) 복귀.</summary>
    public byte Man;
    public long ManTick;
    public Vector2 ManGoal, ManWay, FollowGoal;
    /// <summary>연신: 이 틱 전까지 화력조 · 지원 · 사격조는 LiftPos 둘레 (behaviour.liftRadius) 를 쏘지 않는다.</summary>
    public long LiftUntil;
    public Vector2 LiftPos;

    /// <summary>행동 상태만 지움 (Passive 는 그대로).</summary>
    public void Clear()
    {
        Phase = DrillPhase.Idle; PhaseTick = 0;
        BoundSquad = 0; BoundTick = 0; Man = 0; ManTick = 0; ManGoal = ManWay = FollowGoal = Vector2.Zero; LiftUntil = 0;
    }

    public bool Used => Phase != DrillPhase.Idle || Passive || ContactTick != 0 || BoundLast != 0 || Man != 0 || LiftUntil != 0 || Enemy != Vector2.Zero || Home != Vector2.Zero;

    internal void Write(SnapWriter w)
    {
        w.Byte((byte)Phase); w.Byte(Passive ? (byte)1 : (byte)0); w.L(PhaseTick); w.L(ContactTick);
        w.F(Enemy.X); w.F(Enemy.Y); w.F(Home.X); w.F(Home.Y);
        w.I(BoundSquad); w.I(BoundLast); w.L(BoundTick);
        w.Byte(Man); w.L(ManTick); w.F(ManGoal.X); w.F(ManGoal.Y); w.F(ManWay.X); w.F(ManWay.Y); w.F(FollowGoal.X); w.F(FollowGoal.Y);
        w.L(LiftUntil); w.F(LiftPos.X); w.F(LiftPos.Y);
    }

    internal void Read(SnapReader r)
    {
        byte ph = r.Byte();
        if (!Enum.IsDefined(typeof(DrillPhase), ph)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "drill phase"));
        Phase = (DrillPhase)ph; Passive = r.Byte() != 0; PhaseTick = r.L(); ContactTick = r.L();
        Enemy = new Vector2(r.F(), r.F()); Home = new Vector2(r.F(), r.F());
        BoundSquad = r.I(); BoundLast = r.I(); BoundTick = r.L();
        Man = r.Byte(); ManTick = r.L(); ManGoal = new Vector2(r.F(), r.F()); ManWay = new Vector2(r.F(), r.F()); FollowGoal = new Vector2(r.F(), r.F());
        LiftUntil = r.L(); LiftPos = new Vector2(r.F(), r.F());
    }
}

public sealed partial class World
{
    DrillDef DrillRules => TacRules.Behaviour;
    readonly List<Unit> drillBuf = new();
    readonly List<Unit>[] drillRole = { new(), new(), new(), new(), new(), new() };

    static int SecTicks(float s) => Math.Max(1, (int)MathF.Round(s / Config.SimDt));

    // ---------------------------------------------------------------- 지휘 능력 → 실행 틀

    /// <summary>지휘 능력이 허용하는 틀 (부사관 대리 = 화력-기동, 지휘 없음 = 틀 없음).</summary>
    internal CompanyTactic GateTactic(Company c, CompanyTactic t)
    {
        var lvl = CommandLevelOf(c);
        if (TacticAllowed(lvl, t)) return t;
        return lvl == CommandLevel.Nco && t != CompanyTactic.None ? CompanyTactic.FireManoeuvre : CompanyTactic.None;
    }

    /// <summary>실행 틀 바꾸기 (전환 = 행동 상태 초기화).</summary>
    internal void SetActiveTactic(Company c, CompanyTactic t)
    {
        if (c.ActiveTactic == t) return;
        c.ActiveTactic = t;
        if (c.Drill.Phase != DrillPhase.Idle) EndDrill(c, reform: false);
        c.Drill.Clear();
        Stats.TacticTransitions++;
    }

    /// <summary>중대 깃발 이동 시작 (외부 명령 — 플레이어 · AI): 행동 상태를 지우고, 일반 이동이면 도착까지 행동 안 함.</summary>
    internal void DrillOnCompanyOrder(Company c, UnitOrder order)
    {
        var d = c.Drill;
        if (order == UnitOrder.Move) { d.Clear(); d.Passive = true; return; }
        // 공격 이동: 국면 (접촉 · 화력 우세 대기 시간) 은 이어 가고, 진행 중이던 약진 · 기동은 새 명령으로 끝
        d.Passive = false;
        d.BoundSquad = 0; d.Man = 0; d.LiftUntil = 0;
    }

    static DrillAct ActOf(string? key, UnitRole role) => key?.ToLowerInvariant() switch
    {
        "hold" => DrillAct.Hold, "bound" => DrillAct.Bound, "manoeuvre" => DrillAct.Manoeuvre, "follow" => DrillAct.Follow, "support" => DrillAct.Support,
        "command" => DrillAct.Command, "trench" => DrillAct.Trench, "reserve" => DrillAct.Reserve,
        _ => role switch { UnitRole.Manoeuvre => DrillAct.Manoeuvre, UnitRole.Support => DrillAct.Support, UnitRole.Command => DrillAct.Command, _ => DrillAct.Hold },
    };

    // ---------------------------------------------------------------- 매 틱 (StepFormationTriggers 뒤, StepAnchors 앞)

    void StepTactics()
    {
        if (Companies.Count == 0) return;
        var R = DrillRules;
        int baseIv = Math.Max(1, R.DecideTicks);
        foreach (var c in Companies)
        {
            if (c.TA.Stage != TaStage.None) continue;   // N32: 참호 돌격 중에는 틀 행동 안 함
            if (c.Tactic == CompanyTactic.None && c.ActiveTactic == CompanyTactic.None && c.Drill.Phase == DrillPhase.Idle) continue;
            if ((Tick + c.Id) % DecideInterval(c, baseIv) != 0) continue;   // 지휘관 경험치가 높으면 더 자주 (CommandRanks.cs)
            if (InHandover(c)) { Stats.DrillPaused++; continue; }          // 인계 중: 전환 · 행동 판단 멈춤 (병사는 하던 일을 계속)
            SetActiveTactic(c, GateTactic(c, c.Tactic));
            if (c.ActiveTactic is CompanyTactic.None or CompanyTactic.Artillery || c.Members.Count == 0)
            {
                if (c.Drill.Phase != DrillPhase.Idle) EndDrill(c, reform: false);
                continue;
            }
            Stats.DrillDecisions++;
            DecideDrill(c);
        }
    }

    void EndDrill(Company c, bool reform)
    {
        var d = c.Drill;
        d.Phase = DrillPhase.Idle; d.BoundSquad = 0; d.Man = 0; d.LiftUntil = 0;
        Stats.DrillEnds++;
        if (!reform || c.Anchor != null || c.Members.Count < 2) return;
        // 중대 대형으로 다시 정렬: 행동이 세운 사수를 풀고 (참호 안은 그대로) 빈자리 점검과 같은 재배치
        foreach (var u in c.Members)
            if (u.Order == UnitOrder.Hold && !u.InTrench && u.TrenchSpot < 0 && !u.Charging) u.Order = UnitOrder.Idle;
        if (c.Facing == Vector2.Zero) c.Facing = new Vector2(0, -1);
        RefillCompany(c, Centroid(c.Members));
    }

    void DecideDrill(Company c)
    {
        var R = DrillRules;
        var d = c.Drill;
        // 접촉 · 적 위치
        bool contact = false;
        Vector2 es = Vector2.Zero; int ek = 0;
        int contactTicks = SecTicks(R.ContactSec);
        foreach (var u in c.Members)
        {
            if (u.Target is Unit t && t.Alive && IsHostile(c.Owner, t.Owner)) { es += t.Pos; ek++; contact = true; }
            else if (u.LastShotTick > 0 && Tick - u.LastShotTick < contactTicks) contact = true;
        }
        if (ek > 0) d.Enemy = es / ek;
        if (contact) d.ContactTick = Tick;
        bool marching = c.Anchor != null && !c.Anchor.Arrived;
        if (d.Passive)
        {
            if (marching) { if (d.Phase != DrillPhase.Idle) EndDrill(c, reform: false); return; }
            d.Passive = false;
        }
        bool defence = c.ActiveTactic == CompanyTactic.DefenceDepth;
        var T = TacRules.Template(c.ActiveTactic, out bool mirror);
        if (T == null) return;
        Vector2 cen = Centroid(c.Members);
        float range = 0;
        foreach (var u in c.Members)
            if (!u.IsOfficer && u.Weapon != null && u.Role is UnitRole.Rifle or UnitRole.Manoeuvre or UnitRole.None) range = MathF.Max(range, u.Weapon.Range);
        if (range <= 0) foreach (var u in c.Members) if (!u.IsOfficer && u.Weapon != null) range = MathF.Max(range, u.Weapon.Range);
        if (range <= 0) range = 16f;
        if (d.Phase == DrillPhase.Idle)
        {
            // 공격 틀은 접촉 + 적이 유효 사거리 (사거리 × approachFrac) 안이거나 중대가 서 있을 때 시작 (먼 접촉은 행군 · 공격 이동 대형이 그대로 다가감)
            bool inRange = d.Enemy != Vector2.Zero && Vector2.Distance(cen, d.Enemy) <= range * R.ApproachFrac;
            bool start = defence ? !marching || (contact && ek > 0) : contact && (ek > 0 || d.Enemy != Vector2.Zero) && (inRange || !marching || c.Anchor!.SpeedMul < 0.05f);
            if (!start) return;
            d.Phase = defence ? DrillPhase.Defend : DrillPhase.Engage;
            d.PhaseTick = Tick; d.BoundSquad = 0; d.Man = 0; d.FollowGoal = Vector2.Zero;
            if (ek == 0 && d.Enemy == Vector2.Zero) d.Enemy = cen + (c.Facing == Vector2.Zero ? new Vector2(0, -1) : c.Facing) * 20f;
            if (defence) d.Home = Vector2.Zero;
            Stats.DrillStarts++;
        }
        else
        {
            bool lost = Tick - d.ContactTick > SecTicks(R.DisengageSec);
            if (defence ? marching : lost) { EndDrill(c, reform: !defence); return; }
        }

        // 역할군 나누기 (구성원 Id 순)
        foreach (var l in drillRole) l.Clear();
        foreach (var u in c.Members) drillRole[(int)(u.Role == UnitRole.None ? UnitRole.Rifle : u.Role)].Add(u);
        Vector2 dir = d.Enemy - cen;
        dir = dir.LengthSquared() > 1e-3f ? Vector2.Normalize(dir) : (c.Facing == Vector2.Zero ? new Vector2(0, -1) : c.Facing);
        if (defence && c.Facing != Vector2.Zero && ek == 0) dir = c.Facing;   // 적이 아직 없으면 방어 정면
        // 화력 우세: 우리를 쏠 수 있는 적 (중대 둘레 사거리 + superiorityRadius 의 보이는 적) 이 제압됐거나, 우리가 수로 크게 앞서거나,
        // 오래 (patienceSec) 사격전을 했으면 — 그 전에는 약진 · 우회하지 않고 사격 (적 위치 둘레만 세면 넓은 적 전선의 일부만 보고 일찍 나갔다)
        var (esupp, ecount) = EnemyAround(c.Owner, cen, range + R.SuperiorityRadius);
        // 수 우세는 남은 적이 적을 때 (≤ superiorityFew) 만, 교전 superiorityDelaySec 뒤에 — 안개 속 첫 접촉은 적 전선의 일부만 보인다
        bool superior = ecount > 0 && (esupp >= R.SuperioritySupp || (ecount <= R.SuperiorityFew && c.Members.Count >= R.SuperiorityOdds * ecount && Tick - d.PhaseTick > SecTicks(R.SuperiorityDelaySec)));
        bool patient = superior || Tick - d.PhaseTick > SecTicks(R.PatienceSec);   // 오래 버틴 사격전: 사격조 약진만 (기동조 우회는 화력 우세가 있어야)

        // 기동조 (연신 · 뒤따름 판단에 쓰므로 먼저)
        foreach (var role in new[] { UnitRole.Manoeuvre, UnitRole.Fire, UnitRole.Rifle, UnitRole.Support, UnitRole.Command })
        {
            var list = drillRole[(int)role];
            if (list.Count == 0) continue;
            T.Groups.TryGetValue(TacticRules.KeyOf(role), out var gd);
            switch (ActOf(gd?.Act, role))
            {
                case DrillAct.Hold: DrillHold(list); break;
                case DrillAct.Bound: DrillBound(c, list, dir, range, patient || gd is { Wait: false }, superior || gd is { Wait: false }); break;
                case DrillAct.Manoeuvre:
                    if (SweepList(c, list)) DrillSweep(c, list, dir);   // 돌격보병 (N23): 우회 대신 걸으며 쏘며 정면 소탕
                    else
                    {
                        if (c.Drill.Man == 6) c.Drill.Man = 0;   // 소탕을 그만둠 (기관총 · 카드 꺼짐) → 기동조 판단 처음부터
                        DrillManoeuvre(c, list, gd, mirror, dir, range, superior || gd is { Wait: false }, esupp >= R.ManoeuvreOpenSupp);
                    }
                    break;
                case DrillAct.Follow: DrillFollow(c, list, dir, range); break;
                case DrillAct.Support: DrillSupport(list); break;
                case DrillAct.Command: DrillCommand(c, list, dir); break;
                case DrillAct.Trench: DrillTrench(list); break;
                case DrillAct.Reserve: DrillReserve(c, list, dir); break;
            }
        }
    }

    // ---------------------------------------------------------------- 행동

    /// <summary>서서 사격 (참호 안 · 자리로 가는 중이면 그 자리, 표적 유지). 돌격 중인 병사는 그대로.</summary>
    void DrillHold(List<Unit> list)
    {
        foreach (var u in list) DrillHoldUnit(u);
    }

    void DrillHoldUnit(Unit u)
    {
        if (u.Charging || u.Order == UnitOrder.Hold || u.InTrench) return;
        if (u.Group != null && u.Company?.Anchor is { Close: true } ca && ReferenceEquals(ca.Group, u.Group)) return;   // 대열 정비로 슬롯에 가는 중 (gapfill2) — 도착하면 다음 판단에서 사수
        if (u.CoverHold || (u.TrenchSpot >= 0 && Moving(u))) return;   // 엄폐 칸 · 참호 자리로 가는 중 (engage — 진지 점유 · 엄폐 찾기): 도착하면 사수
        var t = u.Target;
        int spot = u.TrenchSpot; bool inT = u.InTrench;
        ResetOrder(u);
        if (!HoldKeepsTrench(u, spot, inT)) u.Order = UnitOrder.Hold;
        if (t != null && IsValidTarget(u, t)) u.Target = t;
    }

    static bool Moving(Unit u) => u.Order is UnitOrder.Move or UnitOrder.AttackMove && u.Group != null;

    /// <summary>사격조 분대 교대 약진: 한 분대가 엄폐 칸으로 boundLength 전진 (일반 이동 — 쏘지 않고 빠르게), 나머지는 사수 사격. 도착 (또는 boundMaxSec) 하면 다음 분대.</summary>
    void DrillBound(Company c, List<Unit> list, Vector2 dir, float range, bool superior, bool strong)
    {
        var R = DrillRules;
        var d = c.Drill;
        var squads = new List<Squad>();
        foreach (var q in c.Squads) if (list.Exists(u => u.Squad == q)) squads.Add(q);
        float stop = range * R.RifleStopFrac;
        bool march = c.MarchFireOn && PrefersMarchFire(c);   // N55: 경험 적은 지휘관 = 사격조 전체 돌격 사격 전진, 많은 지휘관 = 분대 교대 약진 (OfficerStyle.cs)
        if (d.BoundSquad == -1)
        {
            // 사격조 전체가 유효 사거리로 다가가는 중 (공격 이동 — 표적을 잡으면 서서 쏜다)
            if (list.Exists(Moving) && Tick - d.BoundTick < SecTicks(20))
            {
                foreach (var u in list) if (!Moving(u)) DrillHoldUnit(u);   // 먼저 닿은 병사는 서서 쏜다 (대기 = 추격)
                return;
            }
            d.BoundSquad = 0;
        }
        if (!superior && d.BoundSquad <= 0)
        {
            // 화력 우세 전: 사격조는 중대 명령 (플레이어 · 전술 AI 의 접근 · 사수) 그대로 사격 — 약진은 화력 우세 (또는 오래 버틴 사격전) 뒤에만
            // (먼저 사수 · 접근을 직접 걸었더니 AI 의 사격 진지 접근을 막아 평지 맞대결에서 손해였다, 2026-10-19 측정)
            d.BoundSquad = 0;
            return;
        }
        if (d.BoundSquad > 0)
        {
            var bq = squads.Find(q => q.Id == d.BoundSquad);
            bool arrived = bq == null || Tick - d.BoundTick > SecTicks(R.BoundMaxSec) || !list.Exists(u => u.Squad == bq && Moving(u));
            if (!arrived) { if (!march) foreach (var u in list) if (u.Squad != bq) DrillHoldUnit(u); return; }
            d.BoundLast = d.BoundSquad; d.BoundSquad = 0;
        }
        float supp = 0;
        foreach (var u in list) supp += u.SuppressionRatio;
        supp /= list.Count;
        Squad? pick = null;
        Vector2 dest = default;
        if (march && supp < R.BoundSuppression && squads.Count > 0)
        {
            // 돌격 사격 전진: 사격조 전원이 boundLength 앞 (엄폐 규칙 같음) 으로 공격 이동 — 걸으며 쏜다 (MarchCommand.cs, 장교 판단)
            Vector2 lc = Centroid(list);
            float dq = Vector2.Distance(lc, d.Enemy);
            if (dq > stop + 1f)
            {
                Vector2 p = DrillCover(lc + dir * MathF.Max(1f, MathF.Min(R.BoundLength, dq - stop)), R.CoverSearch, d.Enemy);
                if (strong || DrillCoverScore(p) >= DrillCoverScore(lc) + R.BoundCoverGain) { pick = squads[0]; dest = p; }
            }
        }
        else if (supp < R.BoundSuppression && squads.Count > 0)
        {
            int start = Math.Max(0, squads.FindIndex(q => q.Id == d.BoundLast) + 1);
            for (int k = 0; k < squads.Count && pick == null; k++)
            {
                var q = squads[(start + k) % squads.Count];
                drillBuf.Clear();
                foreach (var u in list) if (u.Squad == q) drillBuf.Add(u);
                if (drillBuf.Count == 0) continue;
                Vector2 qc = Centroid(drillBuf);
                float dq = Vector2.Distance(qc, d.Enemy);
                if (dq <= stop + 1f) continue;
                Vector2 p = DrillCover(qc + dir * MathF.Max(1f, MathF.Min(R.BoundLength, dq - stop)), R.CoverSearch, d.Enemy);
                // 엄폐 없는 곳으로 걸어 나가는 약진은 적이 뚜렷이 제압됐거나 (strong) 할 때만 — 대등한 사격전에서 개활지 약진 = 손해 (전술 AI 사격선 전진과 같은 규칙)
                if (!strong && DrillCoverScore(p) < DrillCoverScore(qc) + R.BoundCoverGain) continue;
                pick = q; dest = p;
            }
        }
        if (pick != null && march)
        {
            drillBuf.Clear();
            foreach (var u in list) if (!u.Charging) drillBuf.Add(u);
            if (drillBuf.Count > 0) DrillMove(new List<Unit>(drillBuf), dest, UnitOrder.AttackMove, dir, MathF.Max(3f, drillBuf.Count * 1.2f), FormType.Line, quick: false);
            d.BoundSquad = pick.Id; d.BoundTick = Tick;
            Stats.DrillMarchAdvances++;
            return;
        }
        if (pick != null)
        {
            drillBuf.Clear();
            foreach (var u in list) if (u.Squad == pick) drillBuf.Add(u);
            DrillMove(new List<Unit>(drillBuf), dest, UnitOrder.Move, dir, MathF.Max(3f, drillBuf.Count * 1.2f), FormType.Line, quick: false);
            d.BoundSquad = pick.Id; d.BoundTick = Tick;
            Stats.DrillBounds++;
        }
        if (pick != null) foreach (var u in list) if (u.Squad != pick) DrillHoldUnit(u);   // 약진 중: 나머지 분대는 서서 엄호 사격 (약진이 없으면 중대 명령 그대로)
    }

    /// <summary>
    /// 기동조: 엄폐 경유점 → 틀 각도 위치 (적 기준 angle, 거리 standoff × 사거리; 측면 우회 = 옆) 로 일반 이동 (속보), 도착하면 사격.
    /// 적이 chargeRange 안이거나 적이 제압됐고 (평균 ≥ chargeSuppression) chargeMaxDist 안이면 착검 돌격. 적에게 liftDist 안이면 화력조 연신.
    /// </summary>
    void DrillManoeuvre(Company c, List<Unit> list, RoleSlotDef? gd, bool mirror, Vector2 dir, float range, bool superior, bool openOk = false)
    {
        var R = DrillRules;
        var d = c.Drill;
        Vector2 mc = Centroid(list);
        float ang = (gd?.Angle ?? 0) * (mirror ? -1f : 1f) * (MathF.PI / 180f);
        float standoff = MathF.Max(2f, (gd?.Standoff ?? 0.5f) * range);
        float chargeR = gd != null && gd.ChargeRange > 0 ? gd.ChargeRange : R.ChargeRange;
        var form = FormationRules.TypeOf(gd?.SquadForm) ?? FormType.Wedge;
        Vector2 right = new(-dir.Y, dir.X);
        if (d.Man == 0 && !superior)
        {
            // 화력 우세 전: 우회하지 않고 중대 명령대로 사격 (개활지 우회 = 적 사거리 안을 쏘지 않고 걷는 것). 적이 아주 가까우면 돌격만
            TryDrillCharge(c, list, mc, chargeR, range, closeOnly: true);
            return;
        }
        switch (d.Man)
        {
            case 0:
            {
                Vector2 away = -dir * DetMath.Cos(ang) + right * DetMath.Sin(ang);
                Vector2 goal = DrillCover(d.Enemy + away * standoff, R.CoverSearch, d.Enemy);
                d.ManGoal = goal;
                // 경유점: 지금 적과의 거리 (적 사거리 + 3 이하) 를 유지한 채 틀 각도 쪽으로 돌아 들어간 점 (개활지에서 적 사격권을 가로지르지 않게) — 엄폐 칸으로
                float arc = MathF.Min(Vector2.Distance(mc, d.Enemy), range + 3f);
                Vector2 way = DrillCover(d.Enemy + away * MathF.Max(arc, standoff), R.CoverSearch, d.Enemy);
                bool far = Vector2.Distance(mc, goal) > 10f && Vector2.Distance(way, goal) > 4f && Vector2.Distance(mc, way) > 4f;
                // 엄폐 없는 우회 (목표 · 경유점이 개활지) 는 적이 크게 제압됐을 때만 (manoeuvreOpenSupp) — 평지 맞대결 측정에서 개활지 우회는 손해였다
                if (gd is not { Wait: false } && !openOk && (DrillCoverScore(goal) < R.ManoeuvreCover || (far && DrillCoverScore(way) < R.ManoeuvreCover))) { d.ManGoal = Vector2.Zero; break; }
                d.ManWay = far ? way : goal;
                d.Man = far ? (byte)1 : (byte)2;
                d.ManTick = Tick;
                DrillMove(list, d.Man == 1 ? d.ManWay : d.ManGoal, UnitOrder.Move, dir, MathF.Max(3f, list.Count * 1.2f), form, quick: true);
                Stats.DrillManoeuvres++;
                break;
            }
            case 1:
                if (Vector2.Distance(mc, d.ManWay) < 2.5f || !list.Exists(Moving) || Tick - d.ManTick > SecTicks(25))
                {
                    d.Man = 2; d.ManTick = Tick;
                    DrillMove(list, d.ManGoal, UnitOrder.Move, dir, MathF.Max(3f, list.Count * 1.2f), form, quick: true);
                }
                break;
            case 2:
                if (Vector2.Distance(mc, d.ManGoal) < 2.5f || !list.Exists(Moving) || Tick - d.ManTick > SecTicks(30)) { d.Man = 3; d.ManTick = Tick; DrillHold(list); }
                break;
            case 3:
                // 적이 자리를 옮겼으면 (적과의 거리가 standoff + replanDist 넘음) 새 위치로 다시
                if (Vector2.Distance(mc, d.Enemy) > standoff + R.ReplanDist && Tick - d.ManTick > SecTicks(3) && list.Average(u => u.SuppressionRatio) < 0.5f)
                {
                    d.Man = 0;
                    DrillManoeuvre(c, list, gd, mirror, dir, range, superior, openOk);
                    return;
                }
                DrillHold(list);
                break;
            case 4:
            {
                bool charging = list.Exists(u => u.Charging);
                if ((!charging && Tick - d.ManTick > SecTicks(3)) || Tick - d.ManTick > SecTicks(R.ChargeSec)) { d.Man = 3; d.ManTick = Tick; DrillHold(list); }
                break;
            }
        }
        if (d.Man is >= 1 and <= 3 && TryDrillCharge(c, list, mc, chargeR, range)) { }
        if (d.Man >= 2 && Vector2.Distance(mc, d.Enemy) <= R.LiftDist) Lift(c);
    }

    /// <summary>돌격보병 소탕 대상: 돌격 사격 카드가 켜진 중대의 역할군 과반이 돌격보병 (MarchFireDefault) 이고 적 둘레에 살아 쏘는 기관총이 없음.</summary>
    bool SweepList(Company c, List<Unit> list)
    {
        if (!c.MarchFireOn || c.Drill.Enemy == Vector2.Zero) return false;
        int n = 0;
        foreach (var u in list) if (u.Def.MarchFireDefault) n++;
        return n * 2 > list.Count && !ActiveMgNear(c.Owner, c.Drill.Enemy, DrillRules.ChargeMgRadius);
    }

    /// <summary>
    /// 돌격보병 소탕 (N23 '난사로 누르고 근접 무기로 쓸어버리기'): 적 위치 너머 4칸으로 공격 이동 — 표적을 잡아도 멈추지 않고 걸으며 쏘고 (MarchCommand.cs),
    /// 6칸 안 권총 · marchChargeRange 안 수류탄 → 단검 돌격은 병사 단위 (엔진). 적이 옮겨 가면 (목표가 4칸 · 거리 30% 넘게 바뀜) 다시 명령. 돌격 중인 병사는 건드리지 않는다. 상태 Man = 6.
    /// </summary>
    void DrillSweep(Company c, List<Unit> list, Vector2 dir)
    {
        var d = c.Drill;
        Vector2 mc = Centroid(list);
        Vector2 goal = ClampToMap(d.Enemy + dir * 4f);
        bool walking = list.Exists(u => u.Order == UnitOrder.AttackMove && u.Group != null && !u.Charging);
        bool moved = Vector2.Distance(goal, d.ManGoal) > MathF.Max(4f, 0.3f * Vector2.Distance(mc, goal));
        if (d.Man != 6 || (!walking || moved) && Tick - d.ManTick > SecTicks(3))
        {
            drillBuf.Clear();
            foreach (var u in list) if (!u.Charging) drillBuf.Add(u);
            d.Man = 6; d.ManGoal = goal; d.ManTick = Tick;
            if (drillBuf.Count > 0) { DrillMove(new List<Unit>(drillBuf), goal, UnitOrder.AttackMove, dir, MathF.Max(3f, drillBuf.Count * 1.2f), FormType.Line, quick: false); Stats.DrillSweeps++; }
        }
        if (Vector2.Distance(mc, d.Enemy) <= DrillRules.LiftDist) Lift(c);
    }

    /// <summary>돌격 판단 (기동조 · 예비 역습 공용 조건): 가까운 적 (chargeRange) 또는 제압된 적 (chargeMaxDist 안). 돌격했으면 true.</summary>
    internal bool TryDrillCharge(Company c, List<Unit> list, Vector2 mc, float chargeR, float range, bool closeOnly = false)
    {
        var R = DrillRules;
        var d = c.Drill;
        int chargers = 0;
        foreach (var u in list) if (u.CanCharge) chargers++;
        if (chargers * 2 < list.Count || chargers == 0) return false;
        // 돌격 표적 = 기동조에 가장 가까운 (chargeMaxDist 안) 보이는 적. 가까우면 (chargeRange) 바로, 아니면 그 둘레 (chargeStatsRadius) 적이
        // 제압됐거나 (평균 ≥ chargeSuppression) 돌격 인원이 적의 chargeOdds 배 이상이고, 둘레에 살아 쏘는 기관총이 없고, 우리 제압이 낮을 때 (전술 AI TryAssault 와 같은 조건)
        var near = FindTarget(c.Owner, mc, MathF.Max(chargeR, R.ChargeMaxDist)) as Unit;
        if (near == null || near.Def.IsArmored) return false;
        if (PursuitRules.NoChargeFleeing && IsFleeing(near)) { Stats.FleeingChargeBlocked++; return false; }   // 후퇴하는 적에게는 사격만 (Pursuit.cs)
        bool close = Vector2.Distance(near.Pos, mc) <= chargeR;
        bool pinned = false;
        if (!close && !closeOnly)
        {
            float own = 0;
            foreach (var u in list) own += u.SuppressionRatio;
            var (supp, cnt) = EnemyAround(c.Owner, near.Pos, R.ChargeStatsRadius);
            pinned = cnt > 0 && own / list.Count < 0.5f && (supp >= R.ChargeSuppression || chargers >= R.ChargeOdds * cnt && supp >= R.ChargeMinSuppression) && !ActiveMgNear(c.Owner, near.Pos, R.ChargeMgRadius);   // T24 ③
        }
        if (!close && !pinned) return false;
        // 결정 4: 거리 · 제압 조건을 넘어도 화력 우세 신호 · 엄호 확인 (+ 병력비) 이 있어야, 아니면 교전 assaultFallbackSec 뒤에만
        var why = AssaultClearance(c.Owner, Centroid(c.Members), range, near.Pos, list, chargers, d.PhaseTick);
        if (!CountAssault(why)) return false;
        Vector2 to = near.Pos - mc;
        to = to.LengthSquared() > 1e-3f ? Vector2.Normalize(to) : Vector2.Zero;
        foreach (var u in list) if (u.CanCharge) { CountChargeAt(u, near); break; }
        ExecCharge(c.Owner, list, ClampToMap(near.Pos + to * 3f), null);
        d.Man = 4; d.ManTick = Tick;
        Stats.DrillCharges++;
        return true;
    }

    /// <summary>연신: 화력조 · 지원 · 사격조는 적 위치 둘레를 잠시 쏘지 않는다 (판단 두 번 동안, 기동조가 가까이 있는 한 갱신).</summary>
    void Lift(Company c)
    {
        var d = c.Drill;
        if (Tick >= d.LiftUntil) Stats.DrillLifts++;
        d.LiftUntil = Tick + DecideInterval(c, Math.Max(1, DrillRules.DecideTicks)) * 2 + 1;
        d.LiftPos = d.Enemy;
    }

    /// <summary>[사격 규칙] 연신 중인 중대의 화력조 · 지원 · 사격조 병사는 그 둘레의 적을 표적으로 삼지 않는다.</summary>
    bool LiftActive(Unit u, out Vector2 pos, out float r)
    {
        pos = default; r = 0;
        if (u.Company is not { } co || Tick >= co.Drill.LiftUntil || u.Charging || u.Role is not (UnitRole.Fire or UnitRole.Support or UnitRole.Rifle)) return false;
        pos = co.Drill.LiftPos; r = DrillRules.LiftRadius;
        return r > 0;
    }

    bool LiftBlocks(Unit u, Entity t) => LiftActive(u, out var p, out float r) && Vector2.DistanceSquared(t.Pos, p) < r * r;

    /// <summary>[사격 규칙] 틀을 실행 중인 중대의 지원 역할 = 위협 (전차 · 기관총 · 장교) 우선.</summary>
    static bool ThreatPriority(Unit u) => u.Role == UnitRole.Support && u.Company is { ActiveTactic: not CompanyTactic.None };

    internal static bool IsThreat(Entity e) => e is Unit t && (t.Def.IsArmored || t.Def.IsOfficer || t.Def.Weapon is "lmg" or "lmg_chauchat" or "hmg" or "hmg_type3");

    /// <summary>앞 역할군 뒤를 공격 이동 (적과 사거리 × followFrac 까지). 기동조가 돌격하면 가까울 때 함께 돌격.</summary>
    void DrillFollow(Company c, List<Unit> list, Vector2 dir, float range)
    {
        var R = DrillRules;
        var d = c.Drill;
        Vector2 fc = Centroid(list);
        if (d.Man == 4 && Vector2.Distance(fc, d.Enemy) <= R.ChargeMaxDist && list.Exists(u => u.CanCharge && !u.Charging))
        {
            var chargers = list.Where(u => u.CanCharge && !u.Charging).ToList();
            ExecCharge(c.Owner, chargers, ClampToMap(d.Enemy + dir * 3f), null);
            return;
        }
        Vector2 goal = ClampToMap(d.Enemy - dir * (range * R.FollowFrac));
        bool idle = !list.Exists(Moving);
        if (Vector2.Distance(goal, d.FollowGoal) > 4f || (idle && Vector2.Distance(fc, goal) > 3f && list.Exists(u => u.Target == null)))
        {
            if (Vector2.Distance(fc, goal) <= 2.5f) { DrillHold(list); d.FollowGoal = goal; return; }
            DrillMove(list, goal, UnitOrder.AttackMove, dir, MathF.Max(3f, list.Count * 1.1f), FormType.Line, quick: false);
            d.FollowGoal = goal;
        }
    }

    /// <summary>지원: 사수 + 위협 우선 표적 (지금 표적이 위협이 아니면 사거리 안 위협으로 바꿈).</summary>
    void DrillSupport(List<Unit> list)
    {
        foreach (var u in list)
        {
            DrillHoldUnit(u);
            if (u.Charging || u.Weapon == null || IsThreat(u.Target!)) continue;
            var t = FindTarget(u.Owner, u.Pos, u.Weapon.Range + u.Radius, u.Weapon.MinRange, u.Weapon.Los ? u : null, u.Id, 0f, u.Weapon, null, threat: true);
            if (t != null && IsThreat(t) && !LiftBlocks(u, t)) { u.Target = t; u.LeashOrigin = u.Pos; }
        }
    }

    /// <summary>지휘 · 정찰: 화력조 (없으면 중대) 무게중심 뒤 commandBack 에 머묾.</summary>
    void DrillCommand(Company c, List<Unit> list, Vector2 dir)
    {
        var R = DrillRules;
        var fire = drillRole[(int)UnitRole.Fire];
        Vector2 basePos = fire.Count > 0 ? Centroid(fire) : Centroid(c.Members);
        Vector2 post = ClampToMap(basePos - dir * R.CommandBack);
        Vector2 lc = Centroid(list);
        // 공격 틀에서 약진 · 기동이 없을 때는 중대 명령대로 (장교가 중대와 함께 움직여야 지휘 범위 · 행진 사격이 산다 — 사수를 걸었더니 장교만 뒤에 남았다)
        var d = c.Drill;
        if (d.Phase == DrillPhase.Engage && d.BoundSquad <= 0 && d.Man == 0) return;
        if (list.Exists(Moving)) return;
        if (Vector2.Distance(lc, post) > R.CommandSlack) DrillMove(list, post, UnitOrder.Move, dir, MathF.Max(2f, list.Count * 1.2f), FormType.Box, quick: false);
    }

    /// <summary>앞 분대: trenchSeek 안 빈 참호 자리를 맡아 들어감 (참호 자동 점유와 같은 자리 표), 없으면 사수.</summary>
    void DrillTrench(List<Unit> list)
    {
        float r = DrillRules.TrenchSeek;
        foreach (var u in list)
        {
            if (u.InTrench || u.TrenchSpot >= 0 || u.Charging) continue;
            int spot = u.Def.CanOccupyTrench ? FindTrenchSpot(u.Pos, r, -1) : -1;
            if (spot < 0) { DrillHoldUnit(u); continue; }
            ResetOrder(u);
            ClaimTrenchSpot(u, spot);
            WalkToTrenchSpot(u);
            Stats.DrillTrench++;
        }
    }

    /// <summary>종심 방어 예비: 제자리 사수, 전선이 밀리면 (사격조 둘레 counterRange 안 적 ≥ counterMin, 또는 적이 예비에 전선보다 가까움) 착검 역습 → 끝나면 원위치.</summary>
    void DrillReserve(Company c, List<Unit> list, Vector2 dir)
    {
        var R = DrillRules;
        var d = c.Drill;
        Vector2 rc = Centroid(list);
        if (d.Home == Vector2.Zero) d.Home = rc;
        switch (d.Man)
        {
            case 0: case 3:
            {
                if (d.Man == 0) { d.Man = 3; d.ManTick = Tick; }
                DrillHold(list);
                var front = drillRole[(int)UnitRole.Rifle];
                Vector2 fc = front.Count > 0 ? Centroid(front) : Centroid(c.Members);
                int near = 0;
                Unit? closest = null; float best = float.MaxValue;
                foreach (var e in UnitsInRadius(fc, R.CounterRange, drillBuf))
                {
                    if (!IsHostile(c.Owner, e.Owner) || !Fog.CanSee(c.Owner, e) || e.Def.IsArmored) continue;
                    near++;
                    float dd = Vector2.DistanceSquared(e.Pos, fc);
                    if (dd < best || (dd == best && closest != null && e.Id < closest.Id)) { best = dd; closest = e; }
                }
                var e2 = FindTarget(c.Owner, d.Home, Vector2.Distance(fc, d.Home)) as Unit;   // 적이 예비에 전선보다 가까움 = 전선이 뚫림
                bool pushed = near >= R.CounterMin || (e2 != null && !e2.Def.IsArmored && front.Count > 0);
                if (!pushed) break;
                var tgt = closest ?? e2!;
                if (PursuitRules.NoChargeFleeing && IsFleeing(tgt)) { Stats.FleeingChargeBlocked++; break; }   // 물러나는 적에게 역습 돌격 안 함 (사격만)
                int chargers = list.Count(u => u.CanCharge);
                if (chargers == 0) break;
                ExecCharge(c.Owner, list, ClampToMap(tgt.Pos), null);
                d.Man = 4; d.ManTick = Tick;
                Stats.DrillCounters++; Stats.DrillCharges++;
                break;
            }
            case 4:
            {
                bool charging = list.Exists(u => u.Charging);
                if ((!charging && Tick - d.ManTick > SecTicks(3)) || Tick - d.ManTick > SecTicks(R.ChargeSec))
                {
                    d.Man = 5; d.ManTick = Tick;
                    DrillMove(list, d.Home, UnitOrder.Move, dir, MathF.Max(3f, list.Count * 1.2f), FormType.Box, quick: true);   // 역습 뒤 원위치로 물러남 = 속보 (pursuit 2026-10-19)
                }
                break;
            }
            case 5:
                if (!list.Exists(Moving) || Vector2.Distance(rc, d.Home) < 2.5f || Tick - d.ManTick > SecTicks(30)) { d.Man = 3; d.ManTick = Tick; DrillHold(list); }
                break;
            default: d.Man = 3; break;
        }
    }

    // ---------------------------------------------------------------- 도우미

    /// <summary>중대 일부 이동 (행동 명령 — 명령 기록 없음, 반응 지연 · 지휘 계통 지연은 그대로, 명령 표시 · 음성은 안 냄).</summary>
    void DrillMove(List<Unit> list, Vector2 target, UnitOrder order, Vector2 dir, float width, FormType form, bool quick)
    {
        if (list.Count == 0) return;
        target = ClampToMap(target);
        var group = new MoveGroup(this, target, list.Count);
        var spec = list.Count >= 2 ? new FormSpec((int)form, 0, dir, form == FormType.Line ? width : 0) : null;
        var (pu, spots) = ApplyFormation(group, list, group.Goal, spec, null, order == UnitOrder.AttackMove);
        foreach (var u in list)
        {
            ResetOrder(u);
            u.Order = order;
            u.Group = group;
            u.QuickStep = quick && u.CanQuickStep;
            u.ReactDelay = ReactDelayFor(u) + ChainDelayTicks(u);
        }
        RegisterBig(group, list);
        if (pu != null && spots != null) ClaimSnapped(pu, spots);
    }


    /// <summary>엄폐 점수 (지형 명중 배수 · 철조망 · 고지) — 전술 AI TacMap.TileCover 와 같은 식.</summary>
    float DrillCoverScore(Vector2 q)
    {
        var (hit, _) = Map.TerrainCover(q);
        float s = (1f - hit) * 2f;
        if (Map.IsWire(q)) s -= 1f;
        if (Map.IsHigh(q)) s += 0.6f;
        return s;
    }

    /// <summary>p 둘레 r 칸 안에서 엄폐가 좋은 칸 (멀어질수록 · 적 쪽으로 나갈수록 감점). 나아지는 게 없으면 p.</summary>
    Vector2 DrillCover(Vector2 p, int r, Vector2 enemy)
    {
        p = ClampToMap(p);
        float best = DrillCoverScore(p) + 0.2f;
        Vector2 bestP = p;
        var (px, py) = GameMap.TileOf(p);
        float dpe = Vector2.Distance(p, enemy);
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            int x = px + dx, y = py + dy;
            if (!Map.IsWalkable(x, y)) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float sc = DrillCoverScore(q) - 0.15f * Vector2.Distance(q, p);
            if (Vector2.Distance(q, enemy) < dpe - 1.5f) sc -= 0.3f;
            if (sc > best) { best = sc; bestP = q; }
        }
        return bestP;
    }

    /// <summary>
    /// 결정 4 (2026-10-19): 공격 돌격 허가. 병력비 (돌격 인원 ≥ 표적 둘레 chargeStatsRadius 의 보이는 적 × assaultMinOdds) 를 갖추고
    /// 화력 우세 신호 (cen 둘레 사거리 + superiorityRadius 의 보이는 적 평균 제압 ≥ superioritySupp) 또는 엄호 확인 (HasOverwatch) 이 있으면 허가.
    /// 아니면 교전 시작 (since) 뒤 assaultFallbackSec 이 지났을 때만 (예비 타이머 — 돌격이 불가능해지지 않게). 읽기 전용 · 보이는 적만 (전술 AI 도 씀).
    /// </summary>
    internal AssaultWhy AssaultClearance(int owner, Vector2 cen, float range, Vector2 target, List<Unit> chargers, int chargerCount, long since)
    {
        var R = DrillRules;
        if (!R.AssaultGate) return AssaultWhy.Ungated;
        var (_, near) = EnemyAround(owner, target, R.ChargeStatsRadius);
        if (chargerCount > 0 && chargerCount >= R.AssaultMinOdds * near)
        {
            var (es, ec) = EnemyAround(owner, cen, range + R.SuperiorityRadius);
            if (ec > 0 && es >= R.SuperioritySupp) return AssaultWhy.Signal;
            if (HasOverwatch(owner, chargers, target)) return AssaultWhy.Overwatch;
        }
        if (Tick - since >= SecTicks(R.AssaultFallbackSec)) return AssaultWhy.Fallback;
        return AssaultWhy.Blocked;
    }

    /// <summary>엄호 확인: 돌격조 밖 아군 (장교 · 돌격 중 제외) 중 최근 overwatchSec 초 안에 쏘았고 표적이 target 둘레 overwatchRadius 안인 병사가 overwatchMin 명 이상.</summary>
    internal bool HasOverwatch(int owner, List<Unit> chargers, Vector2 target)
    {
        var R = DrillRules;
        int win = SecTicks(R.OverwatchSec), need = Math.Max(1, R.OverwatchMin), n = 0;
        float r2 = R.OverwatchRadius * R.OverwatchRadius;
        foreach (var u in Units)
        {
            if (u.Owner != owner || !u.Alive || u.IsOfficer || u.Charging || u.LastShotTick <= 0 || Tick - u.LastShotTick > win) continue;
            if (u.Target is not Unit t || !t.Alive || Vector2.DistanceSquared(t.Pos, target) > r2 || chargers.Contains(u)) continue;
            if (++n >= need) return true;
        }
        return false;
    }

    /// <summary>[측정] 허가 이유를 세고 허가됐으면 true.</summary>
    internal bool CountAssault(AssaultWhy why)
    {
        switch (why)
        {
            case AssaultWhy.Signal: Stats.AssaultSignal++; return true;
            case AssaultWhy.Overwatch: Stats.AssaultOverwatch++; return true;
            case AssaultWhy.Fallback: Stats.AssaultFallback++; return true;
            case AssaultWhy.Ungated: return true;
            default: Stats.AssaultBlocked++; return false;
        }
    }

    /// <summary>pos 둘레 r 에 보이는 적 기관총 (거치했거나 1초 안에 쏨) 이 있나.</summary>
    bool ActiveMgNear(int faction, Vector2 pos, float r)
    {
        foreach (var e in UnitsInRadius(pos, r, drillBuf))
            if (IsHostile(faction, e.Owner) && e.Def.Weapon is "lmg" or "lmg_chauchat" or "hmg" or "hmg_type3" && (e.Deploy == DeployPhase.Deployed || Tick - e.LastShotTick < 60) && Fog.CanSee(faction, e)) return true;
        return false;
    }

    /// <summary>보이는 적 (pos 둘레 r, 장갑 제외) 의 평균 제압 비율 · 수.</summary>
    (float Supp, int Count) EnemyAround(int faction, Vector2 pos, float r)
    {
        float s = 0; int n = 0;
        foreach (var e in UnitsInRadius(pos, r, drillBuf))
        {
            if (!IsHostile(faction, e.Owner) || e.Def.IsArmored || !Fog.CanSee(faction, e)) continue;
            s += e.SuppressionRatio; n++;
        }
        return (n > 0 ? s / n : 0f, n);
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 (WriteFormationState 안)

    void WriteDrill(SnapWriter w, Company c) { w.I((int)c.ActiveTactic); c.Drill.Write(w); }

    void HashDrill(ref Hasher h, Company c)
    {
        var d = c.Drill;
        h.Add((int)d.Phase); h.Add(d.Passive ? 1 : 0); h.Add(d.PhaseTick); h.Add(d.ContactTick);
        h.Add(d.Enemy.X); h.Add(d.Enemy.Y); h.Add(d.Home.X); h.Add(d.Home.Y);
        h.Add(d.BoundSquad); h.Add(d.BoundLast); h.Add(d.BoundTick);
        h.Add(d.Man); h.Add(d.ManTick); h.Add(d.ManGoal.X); h.Add(d.ManGoal.Y); h.Add(d.ManWay.X); h.Add(d.ManWay.Y); h.Add(d.FollowGoal.X); h.Add(d.FollowGoal.Y);
        h.Add(d.LiftUntil); h.Add(d.LiftPos.X); h.Add(d.LiftPos.Y);
    }

    void ReadDrill(SnapReader r, Company c)
    {
        int t = r.I();
        if (!Enum.IsDefined(typeof(CompanyTactic), t)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "active tactic"));
        c.ActiveTactic = (CompanyTactic)t;
        c.Drill.Read(r);
    }
}
