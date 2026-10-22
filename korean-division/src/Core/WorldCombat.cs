using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>유닛 행동 갱신 + 전투 (표적 획득 · 추격 · 사격 · 피해).</summary>
public sealed partial class World
{
    // 표적 재탐색 간격 · Id 분산 · 반경 상한은 balance.json combat.acquire* (2차 추가 C — 1차 측정: E 1만 기 FindTarget 40 ms/틱)

    /// <summary>렌더링용 이펙트. (시작, 끝, 사격 세력, 틱)</summary>
    public readonly List<(Vector2 From, Vector2 To, int Faction, long Tick, bool Hit, int ShooterId)> Shots = new();
    public readonly List<(Vector2 Pos, long Tick, bool Building)> Deaths = new();
    /// <summary>세력별 마지막 피격 알림 (위치, 틱) — UI 경고용.</summary>
    public readonly Dictionary<int, (Vector2 Pos, long Tick)> LastAttacked = new();

    readonly List<Unit> queryBuffer = new();

    void PruneEffects()
    {
        Shots.RemoveAll(s => Tick - s.Tick > 10);
        if (MeleeStrikes.Count > 0) MeleeStrikes.RemoveAll(s => Tick - s.Tick > 10);
        Deaths.RemoveAll(d => Tick - d.Tick > 40);
        if (Impacts.Count > 0) Impacts.RemoveAll(i => Tick - i.Tick > 40);
    }

    void UpdateUnit(Unit u, float dt)
    {
        UpdateDetection(u);          // 은폐 탐지 판정 (Detection.cs, 60틱마다 Id 분산)
        var p0 = u.Pos;
        UpdateUnitCore(u, dt);
        if (u.Def.Heavy && u.Velocity != Vector2.Zero) PaceHeavy(u, p0);   // 중화기는 보병 앞으로 나가지 않는다 (HeavyPacing.cs)
        else if (u.IsOfficer && u.Velocity != Vector2.Zero) PaceOfficer(u, p0);   // N12: 장교도 중대 앞선을 넘지 않는다 (돌격 제외)
        UpdateTurretFacing(u, dt);   // 포탑 회전 (Combat2.cs) — 표적 · 차체 방향이 정해진 뒤
    }

    void UpdateUnitCore(Unit u, float dt)
    {
        bool moved = u.Velocity != Vector2.Zero;   // 지난 틱에 움직였나 (스태미나 소모 · 회복 판정)
        u.Velocity = Vector2.Zero;
        UpdateStamina(u, moved, dt);   // 스태미나 (Combat3.cs)
        if (!u.Charging) { u.HopUntil = 0; u.ChargeRun = 0; u.ImpactSpent = false; u.MeleeVictims = 0; u.MeleeLastId = 0; } else if (moved) u.ChargeRun++;   // 돌격 충격 (Combat3.cs)
        if (u.ChargeCdLeft > 0) u.ChargeCdLeft--;   // 돌격 충격 쿨다운 (Combat3.cs)
        if (u.Def.Grenades > 0) RegenGrenade(u, dt);   // 수류탄 회복 (Combat4.cs)
        if (u.Cooldown > 0) u.Cooldown -= dt;
        DecaySuppression(u);   // 제압 감소 (Combat3.cs, 15틱마다 Id 분산)
        UpdatePinned(u);       // 고정 (엎드림) 이력 pinAt / pinRelease (Suppression.cs)
        UpdateBreak(u);        // 붕괴 굴림 · 회복 (Suppression.cs, supp 2026-10-20)
        UpdateSurrender(u);    // 항복 판정 (Surrender.cs, N79b)
        if (u.Surrendered) { HoldSurrendered(u); return; }
        UpdateProne(u);        // 엎드림 (서서 사격받는 병사, Suppression.cs)
        if (u.ReactDelay == 0 && MaybeHesitate(u)) { }   // 이동 중 멍칫 (제압 ≥ 기준일 때만, Combat3.cs) → 아래 ReactDelay 경로로 선다
        if (u.ReactDelay > 0)
        {
            // 명령 직후(또는 멍칫) 잠깐 멈칫: 목표 쪽으로 몸만 돌린다
            u.ReactDelay--;
            if (u.Group is { Big: true, Inited: false } bg && !ReferenceEquals(u.FormGroup, bg)) { bg.Members!.Add(u); u.FormGroup = bg; }   // 큰 그룹 구성원 등록 (리더 · 오프셋 계산용)
            if ((u.Target?.Pos ?? u.Destination) is Vector2 look) FaceToward(u, look, dt);
            return;
        }
        if (StepMount(u, dt)) return;   // N61 하마 · 승마 전환 (Dismount.cs)
        if (StepFix(u, dt)) return;   // N57 착검 · 칼 뽑기: 반응 지연 뒤 따로 (N37 단축 없음, Bayonet.cs)
        if (u.Target != null && (!IsValidTarget(u, u.Target) || (u.Company != null && LiftBlocks(u, u.Target)))) u.Target = null;   // 연신 (TacticDrill.cs)
        if (u.BurstLeft > 0 && u.Target == null && u.MarchTarget == 0) BurstTargetLost(u);   // 표적을 놓으면 점사 끝 + 휴지 (Bursts.cs)
        if (u.Target == null && u.ChargeState is 1 or 3) u.ChargeState = 0;   // 자동 돌격 · 역돌격은 표적이 사라지면 끝 (명령 돌격은 도착 · 새 명령까지)
        else if (u.ChargeState is 1 or 3 && FleeingBlocks(u, u.Target)) u.ChargeState = 0;   // 표적이 후퇴를 시작하면 자동 돌격을 그치고 사격 (Pursuit.cs)
        if (u.IsOfficer && u.Charging) OfficerChargeEnd(u);   // N35: 중대 돌격이 끝나면 장교 돌격도 끝 (OfficerConduct.cs)
        if (u.Def.Sidearm != null && u.Weapon != null) UpdateSidearm(u);   // 근접 보조 무기 (Combat4.cs)
        if (u.Grenades > 0) AutoGrenade(u);   // 엄폐 중인 적 · 전차가 가까우면 자동 투척 (Combat4.cs)
        if (BrokenFlee(u, dt)) return;   // 붕괴: 쏘지 않고 적 반대쪽으로 달아남 (Suppression.cs)
        if (ProneMoveHold(u, dt)) { u.MarchTarget = 0; return; }   // pronemove: 엎드린 병사는 대열 이동에 끌려가지 않음 (Suppression.cs)
        if (PinnedHold(u, dt)) { u.MarchTarget = 0; return; }   // 고정 (엎드림): 후퇴 명령이 아니면 제자리 사수 (Suppression.cs)
        UpdateCoverSeek(u);   // 피격 중인 대기 · 사수 보병: 가까운 참호 자리 · 엄폐 칸으로 (CoverSeek.cs)
        UpdateSpot(u);        // F1 접적 중 구역 · 사격 위치 (Contact.cs)
        CavReact(u);          // n61 돌격 기병 우선 표적 (CavReact.cs)

        switch (u.Order)
        {
            case UnitOrder.Idle:
                if (u.Target == null && u.Def.AutoAttack) TryAcquire(u, PassiveFire(u) ? PassiveFireRange(u) + u.Radius : AcquireRadius(u));   // N35: 장교 · 정찰병은 자위 반경만
                if (u.Target != null && !Engage(u, u.Target, dt, chase: !PassiveFire(u) && MayChase(u), leash: u.Sight + 2)) u.Target = null;   // F1: 접적 중 대기 병사는 쫓지 않음
                break;

            case UnitOrder.Hold:
                if (u.Target == null && u.Weapon != null) TryAcquire(u, (PassiveFire(u) ? MathF.Min(ReactRange(u), PassiveFireRange(u)) : FollowRange(u)) + u.Radius);   // 반응 거리 (engage): 사거리 × engageRangeFrac — 그 밖에서 쏘는 적에게는 반격 (Retaliate)
                if (u.Target != null) { if (!Engage(u, u.Target, dt, chase: false, leash: 0)) u.Target = null; }
                else if (u.CanDeploy) AdvanceDeploy(u);   // 견인포: 사수 명령이면 표적이 없어도 방열해 둔다
                break;

            case UnitOrder.Move:
                u.HoldLeft = 0; u.EngagedTick = -1000;   // 이동 명령은 사격 후 대기 없이 바로
                if (u.Target != null && u.ChargeState is 1 or 3)   // 행진 사격 중 가까운 적에게 자동 돌격 (MarchCommand.cs) — 표적이 쓰러지면 이동을 이어 간다
                {
                    if (!Engage(u, u.Target, dt, chase: true, leash: u.Sight + 3)) u.Target = null;
                    break;
                }
                if (u.Large && u.Target != null)   // 길을 막은 적 보병 (CheckProgress, WorldNav.cs): 쓰러뜨릴 때까지 서서 쏜다 — 표적이 죽거나 못 쏘면 이동을 이어 간다
                {
                    if (!Engage(u, u.Target, dt, chase: false, leash: 0)) u.Target = null;
                    break;
                }
                if (u.CanDeploy && (HoldDeployedStay(u) || !AdvancePack(u))) break;   // 견인포: 철수가 끝나야 출발 (맥심은 방열 유지 — bal2)
                if (u.Group == null || FollowGroup(u, dt)) ArriveGroup(u);
                break;

            case UnitOrder.AttackMove:
            {
                if (MarchAdvancing(u))   // 돌격 사격 켠 중대: 멈추지 않고 전진 (MarchCommand.cs)
                {
                    u.Target = null;
                    if (!HoldWait(u) && FollowGroup(u, dt)) ArriveGroup(u);
                    break;
                }
                if (CloseWalking(u))   // 대열 정비 (gapfill2): 공격 이동 그대로 슬롯까지 멈추지 않고 걸어감 (행진 사격), 슬롯에 닿으면 아래 보통 공격 이동 (서서 싸움)
                {
                    u.Target = null;
                    if (!HoldWait(u) && FollowGroup(u, dt)) ArriveGroup(u);
                    break;
                }
                // 중대 깃발 대형 구성원 (공격 이동): 대형을 떠나 쫓아가지 않는다 — 사거리 안 적만 서서 쏘고, 대열은 깃발이 함께 멈춰 싸운다 (StepAnchors, (a)-1 · (c)-2)
                bool inForm = !u.Charging && u.Company?.Anchor is { } fa && ReferenceEquals(fa.Group, u.Group);
                // 대형 구성원의 표적 탐색 반경 = 교전 거리 (사거리 × engageRangeFrac, 직사) — 대열은 그 거리까지 다가가 멈춰 쏜다 (교전 2차 '접근 80%' 규칙 유지)
                if (u.Target == null && u.Weapon != null)
                    TryAcquire(u, PassiveFire(u) ? PassiveFireRange(u) + u.Radius : inForm ? (Defs.CombatRules.SquadFollowFireAttackMove ? FollowRange(u) : ReactRange(u)) + u.Radius : AcquireRadius(u));   // N46: 대형 공격 이동은 기본 끔 (켜면 대열이 사거리 끝에서 멈춰 공격이 막힘)
                if (u.Target != null && inForm && Defs.CombatRules.SquadFollowFireAttackMove && !u.Charging && !PassiveFire(u) && u.Company?.Anchor is { Arrived: false }
                    && u.Target.EdgeDistance(u.Pos) - u.Radius > ReactRange(u) && !SquadUnderFire(u))
                    u.Target = null;   // N46b: 반응 거리 밖 따라 쏘기 표적은 총격을 받을 때만 · 깃발이 도착한 뒤에만 서서 쏜다 — 아니면 멈추지 않고 전진 (행진 사격)
                if (u.Target != null)
                {
                    bool hold = !u.Charging && (PassiveFire(u) || inForm);
                    if (!Engage(u, u.Target, dt, chase: !hold, leash: u.Sight + 3)) u.Target = null;
                }
                else if (u.CanDeploy && (HoldDeployedStay(u) || !AdvancePack(u))) { }   // 견인포: 철수 중 (맥심 방열 유지 — bal2)
                else if (u.Group == null || (!HoldWait(u) && FollowGroup(u, dt))) ArriveGroup(u);
                break;
            }

            case UnitOrder.GroundFire:   // 플레이어 지점 포격 (GroundFire.cs)
                UpdateGroundFire(u, dt);
                break;

            case UnitOrder.Attack:
                if (u.Target == null || !Engage(u, u.Target, dt, chase: true, leash: float.MaxValue)) ResetOrder(u);
                break;

        }
        if (u.Velocity != Vector2.Zero) UpdateTrenchCapture(u);   // 걷다가 참호 칸을 밟으면 빈 자리로 들어간다 (Trenches.cs)
        if (u.Velocity != Vector2.Zero) { u.LastMoveTick = Tick; u.AimTarget = 0; u.AimLeft = 0; if (IsFleeing(u)) RetreatFire(u); else MarchFire(u); }   // 움직이면 조준이 풀린다 (행진 사격은 Combat4.cs, 후퇴 사격은 Pursuit.cs)
        else u.MarchTarget = 0;
    }

    /// <summary>
    /// 장교는 먼저 교전하지 않는다 (사용자 결정 2026-10-19): 대기 · 공격 이동 중 자동 표적은 자기 무기 사거리 안의 적만, 추격 · 접근 없음.
    /// 돌격 중 (칼 돌격 명령 · 근접 자동 돌격) 과 명시적 공격 명령 (Order Attack) 은 예외.
    /// </summary>
    static bool OfficerPassive(Unit u) => u.Def.IsOfficer && !u.Charging && u.Weapon != null;

    /// <summary>
    /// 반응 거리 (engage, 2026-10-19 사용자 결정 1): 자동 · 사수 · 공격 이동 대형의 표적 탐색 반경과 추격 유닛의 접근 거리 = 사거리 × (무기 engageFrac, 없으면 combat.engageRangeFrac).
    /// 곡사 · 대포 (category gun) 는 사거리 그대로. 이 밖에서 총격을 받으면 반격 (Retaliate — 사격한 적을 표적, 사거리 안이면 쏜다).
    /// </summary>
    internal float ReactRange(Unit u)
    {
        var w = u.Weapon;
        if (w == null) return 0;
        if (IsIndirect(w) || (u.CanDeploy && u.Def.Category == "gun")) return w.Range;
        float f = w.EngageFrac > 0 ? w.EngageFrac : Defs.CombatRules.EngageRangeFrac;
        return w.Range * MathF.Min(1f, f);
    }

    /// <summary>N46 분대 따라 쏘기: 같은 분대원이 최근 squadFollowFireSec 안에 쐈으면 탐색 반경 = 사거리 (반응 거리 대신). 분대 안만 · 구성원 목록 순 (결정적).</summary>
    internal float FollowRange(Unit u)
    {
        float react = ReactRange(u);
        if (u.Squad is not { } sq || u.Weapon == null) return react;
        long win = (long)MathF.Round(Defs.CombatRules.SquadFollowFireSec / Config.SimDt);
        foreach (var m in sq.Members)
            if (!ReferenceEquals(m, u) && m.Alive && m.LastShotTick > 0 && Tick - m.LastShotTick <= win && !m.Charging) return MathF.Max(react, u.Weapon.Range);
        return react;
    }

    /// <summary>N46b: 자기 또는 같은 분대원이 최근 총격을 받았다 (구성원 목록 순 — 결정적).</summary>
    internal bool SquadUnderFire(Unit u)
    {
        if (UnderFire(u)) return true;
        if (u.Squad is not { } sq) return false;
        foreach (var m in sq.Members) if (m.Alive && UnderFire(m)) return true;
        return false;
    }

    /// <summary>최근 combat.underFireSec 초 안에 총격을 받았다 (빗나감 포함 — LastFiredAtTick, Supply.cs).</summary>
    internal bool UnderFire(Unit u) => Tick - u.LastFiredAtTick <= (long)MathF.Round(Defs.CombatRules.UnderFireSec / Config.SimDt);

    const int HoldWindow = 20;   // 교전이 끝난 뒤 이 틱 안에 이동을 시작하려 하면 정지 대기를 건다

    /// <summary>교전(조준 · 사격)을 막 마친 유닛은 무기의 holdTime 동안 이동 · 추격을 미룬다. 기다려야 하면 true.</summary>
    bool HoldWait(Unit u)
    {
        if (Tick - u.EngagedTick <= HoldWindow)
        {
            u.EngagedTick = -1000;
            u.HoldLeft = u.Weapon?.HoldTicks ?? 0;
            if (GunKeepsPlace(u)) u.HoldLeft = Math.Max(u.HoldLeft, (int)MathF.Round(Defs.CombatRules.GunOutOfRangeHoldSec / Config.SimDt));   // arty3 방렬 자리 유지: 사거리 밖 오래 (Arty3.cs)
        }
        if (u.HoldLeft <= 0) return false;
        u.HoldLeft--;
        return true;
    }

    internal void ArriveGroup(Unit u)
    {
        u.LastGroup = u.Group;
        u.Group = null;
        u.Order = UnitOrder.Idle;
        u.QuickStep = false; u.ForceCross = false;
        u.Retreating = false;   // 후퇴 도착 = 더는 후퇴하는 적이 아님 (Pursuit.cs)
        if (u.ChargeState == 2) u.ChargeState = 0;   // 돌격 이동 도착 → 돌격 끝
        TrenchOnArrive(u);   // 맡은 참호 자리 · 참호 칸에 도착하면 들어가 사수 (Trenches.cs)
        CoverOnArrive(u);    // 엄폐 찾기 이동 끝: 사수였으면 다시 사수 (CoverSeek.cs)
    }

    /// <summary>
    /// 표적 유효성. 공격 명령(강제 지정)은 자기 · 동맹만 아니면 계속 노리고 (중립도 가능),
    /// 자동으로 잡은 표적은 적대(전쟁 · 교전 중)일 때만 유지한다.
    /// </summary>
    bool IsValidTarget(Unit u, Entity t) =>
        t.Alive && !t.Indestructible && t is not Unit { Surrendered: true } && Fog.CanSee(u.Owner, t) &&
        (u.Weapon is not { SpotterOnly: true } || SeenBySpotter(u.Owner, t)) &&   // 곡사포: 장교 · 정찰병이 보는 곳만
        IsHostile(u.Owner, t.Owner);

    /// <summary>
    /// 표적과 교전. 사거리 안이면 사격, 밖이면 (chase 시) 추격.
    /// leash: 교전을 시작한 지점(LeashOrigin)에서 이 거리보다 멀어지면 추격 포기. 교전을 계속할 수 없으면 false.
    /// </summary>
    bool Engage(Unit u, Entity t, float dt, bool chase, float leash)
    {
        if (u.Charging) return EngageMelee(u, t, dt, chase, leash);   // 돌격 중: 사격 없이 근접전 (Combat3.cs)
        var w = u.Weapon;
        if (w == null) return false;
        float dist = t.EdgeDistance(u.Pos) - u.Radius;
        // 자동 돌격 (§2-4b): 근접 무기가 있고 사수가 아니면 적이 chargeAutoRange 안에 들어온 순간 돌격. 멀리 있는 표적을 노리는 중에도 가끔(15틱) 발밑의 적을 살핀다.
        if (u.ChargeOnly && u.CanCharge && u.Order != UnitOrder.Hold && OfficerChargeOk(u))   // N35: 장교는 중대 돌격 중일 때만 (아니면 아래 자위 사격)
        {
            // 기병 '돌격만': 사격하지 않는다. 근접 가능한 표적이면 돌격, 아니면 (전차 등) 표적을 놓는다.
            if (!CanMelee(u, t)) return false;
            if (!u.Charging && ChargeBlockedFleeing(u, t)) return false;   // 추격 금지 설정이면 후퇴하는 적은 놓는다 (Pursuit.cs)
            if (!u.Charging) { u.ChargeState = AutoChargeState(t); u.LeashOrigin = u.Pos; CountChargeAt(u, t); BeginFix(u, false); ProbeOfficerCharge(u, "chargeOnly", t); }
            return EngageMelee(u, t, dt, chase, leash);
        }
        if (u.CanCharge && u.Order != UnitOrder.Hold && u.FireMode != FireMode.Hold && OfficerChargeOk(u))   // N73: 사격 금지는 자동 돌격도 안 함 · N35: 장교 자동 돌격 · 발밑 적 살피기는 중대 돌격 중일 때만
        {
            AutoCharge(u, t, dist);
            if (!u.Charging && u.Order != UnitOrder.Attack && (Tick + u.Id) % 15 == 0 && dist > Defs.CombatRules.ChargeAutoRange && AutoChargeAllowed(u)
                && FindTarget(u.Owner, u.Pos, Defs.CombatRules.ChargeAutoRange, 0, null) is Unit near && CanMelee(u, near) && !ChargeBlockedFleeing(u, near) && !CavAutoChargeBlocked(u, near))
            {
                u.Target = near; u.ChargeState = AutoChargeState(near); u.LeashOrigin = u.Pos; CountChargeAt(u, near); BeginFix(u, false); ProbeOfficerCharge(u, "autoNear", near);
                return EngageMelee(u, near, dt, chase, leash);
            }
            if (u.Charging) return EngageMelee(u, t, dt, chase, leash);
        }
        if (!FireAllowed(u, t)) { u.AimLeft = 0; u.AimTarget = 0; return false; }   // N73 사격 통제: 사격 금지 · 응사만 (조건 밖) 이면 표적을 놓음 — 접적 트리거가 되지 않음
        if (!u.HasAmmo) return false;   // 보급 (Supply.cs): 탄약 0 → 교전 불가 (조준 · 추격도 안 함, 이동은 그대로)
        MarkAlert(u.Owner, u.Pos);   // 표적을 노리는 동안(추격 포함) 지금 있는 구역이 경계 상태 — 추격해 구역을 옮겨도 최대 시야 유지
        // 교전 거리 (2차 추가 A): 추격 가능한 직사 유닛은 사거리 끝에서 멈추지 않고 engageRangeFrac × 사거리까지 다가간 뒤 사격한다.
        // 같은 표적을 이미 조준 · 사격 중이면(AimTarget) 표적이 조금 물러나도 사거리 안인 한 계속 쏜다 (왕복 방지). 사수 · 곡사 · 방열 무기는 예외.
        // 기관총 · 대전차소총 (거치 보병) 도 같은 규칙 (2026-10-19) — 대포 (category gun) 만 최대 사거리에서 방열.
        // 피격 중인 대기 유닛 (engage, 2026-10-19) 은 다가가지 않고 그 자리에서 반격한다 (사거리 안이면).
        bool closeIn = chase && !IsIndirect(w) && !(u.CanDeploy && u.Def.Category == "gun") && u.AimTarget != t.Id && dist > ReactRangeVs(u, t)
                       && !(u.Order == UnitOrder.Idle && UnderFire(u));
        if (dist <= w.Range && !closeIn)
        {
            // 최소 사거리 안(포병): 쏠 수 없다 — 표적을 놓는다 (자동 표적은 다른 표적을 찾고, 공격 명령은 해제된다)
            if (dist < w.MinRange) { u.AimLeft = 0; u.AimTarget = 0; return false; }
            // 직사 사선 막힘 (weapon.los): 추격이면 표적 쪽으로 다가가 사선을 트고, 아니면 표적을 놓는다 (자동 표적 탐색도 사선 있는 표적만 고른다)
            if (!HasFiringLine(u, t, w) && !SpotterIndirectOk(u, t, w))   // arty3: 야전포는 관측 유닛이 보면 간접 사격 (Arty3.cs)
            {
                u.AimLeft = 0; u.AimTarget = 0;
                if (!chase) return false;
                if (GunKeepsPlace(u) || (w.SpotterIndirect && dist > SightOf(u))) return false;   // arty3 자리 유지: 방렬한 대포는 사선 때문에 철수하지 않음 · 관측으로 잡은 먼 표적은 쫓지 않음
                if (!AdvancePack(u)) return true;
                return StepToward(u, t.Pos, null, dt, chase: true);
            }
            // 방열 사계 밖 (Combat2.cs, N10): 옮길 각 < traverseRedeployDeg 면 방열한 채 포가를 돌린다 (모든 명령 · 자동 표적).
            // 그 이상이면 공격 명령만 철수 → (이동 자세에서) 표적 쪽으로 돌아 재방열, 자동 · 사수 표적은 놓는다
            if (!CanTraverseTo(u, t.Pos))
            {
                if (CanTraverseInPlace(u, t.Pos)) { ShiftTraverse(u, t.Pos, dt); u.HoldLeft = 0; u.EngagedTick = Tick; return true; }
                u.AimLeft = 0; u.AimTarget = 0;
                if (u.Order != UnitOrder.Attack) return false;
                if (u.Deploy != DeployPhase.Packing) Stats.TraverseRepacks++;
                AdvancePack(u);
                return true;
            }
            // 사거리 안: 멈춰서 조준 (같은 표적을 이미 조준했다면 곧바로), 조준이 끝나고 재장전도 끝나고 포탑/차체가 표적을 겨누면 사격
            FaceToward(u, t.Pos, dt);
            if (MustDismountToFire(u)) { u.AimLeft = 0; u.AimTarget = 0; return true; }   // N61 기병은 내려서 쏜다 (Dismount.cs)
            u.HoldLeft = 0; u.EngagedTick = Tick;
            if (u.Deploy == DeployPhase.Packed && TraverseHalf(u) > 0
                && MathF.Abs(DetMath.WrapPi(DetMath.Atan2(t.Pos.Y - u.Pos.Y, t.Pos.X - u.Pos.X) - u.Facing)) > TraverseHalf(u)) { u.AimTarget = 0; u.AimLeft = 0; return true; }   // 사계 대포: 표적 쪽으로 다 돈 뒤 방열 시작
            if (!AdvanceDeploy(u, DetMath.Atan2(t.Pos.Y - u.Pos.Y, t.Pos.X - u.Pos.X))) { u.AimTarget = 0; u.AimLeft = 0; return true; }   // 대포: 방열이 끝난 뒤에야 조준 시작 (자동), 사계 중심 = 표적 방향
            if (u.AimTarget != t.Id) { u.AimTarget = t.Id; u.AimLeft = AimTicksFor(u, t, w); u.BurstLeft = 0; }   // 새 표적 = 새 점사
            if (u.AimLeft > 0) u.AimLeft--;
            if (u.AimLeft == 0 && u.Cooldown <= 0 && AimedAt(u, t, w))
            {
                // ---- 유닛 사격 지점 (하나뿐): 탄약 소모 검사는 여기 앞에 (에이전트 B)
                if (CrewDangerClose(u, t, w, dist)) return false;   // 포병 아군 오사 (pursuit2): 자동 표적 근처에 아군 → 쏘지 않고 표적을 놓음 (명시 공격 명령은 쏨)
                if (!ConsumeAmmo(u)) return false;   // 보급 (Supply.cs): 사격 1회 = 탄약 1
                u.Cooldown = NextCooldown(u, w) * FireIntervalMul(u); BurstAfterShot(u, w);   // 점사 (Bursts.cs) · 고정 또는 [min, max] 결정적 난수 (Combat2.cs) × 제압(최대 ×2) · 장교 근처 ×0.9 (Combat3.cs)
                Fire(u, u.Pos, t, w, dist);
            }
            return true;
        }
        u.AimLeft = 0; u.AimTarget = 0;
        if (!chase || Vector2.Distance(u.Pos, u.LeashOrigin) > leash) return false;
        if (MountToChase(u, dist, w.Range)) return true;   // N61 멀리 추격이면 다시 탄다
        if (HoldWait(u)) { FaceToward(u, t.Pos, dt); return true; }   // 표적이 사거리를 벗어나도 잠깐 멈춰 있다가 추격
        if (GunKeepsPlace(u) && GunRepackCooling(u)) return false;   // arty3 재방렬 냉각: 방렬 직후에는 자동 표적을 쫓아 철수하지 않음
        if (!AdvancePack(u)) return true;   // 대포: 추격 전 철수 (자동)
        return StepToward(u, t.Pos, null, dt, chase: true);
    }

    void TryAcquire(Unit u, float radius)
    {
        if (Tick < u.NextAcquireTick) return;
        var c = Defs.CombatRules;
        u.NextAcquireTick = Tick + c.AcquireInterval + u.Id % Math.Max(1, c.AcquireStagger);   // 분산 (2차 추가 C: 0.75초 + 0~14틱)
        if (!IsIndirect(u.Weapon) && radius > c.AcquireRadiusCap) radius = Math.Max(c.AcquireRadiusCap, (u.Weapon?.Range ?? 0) + u.Radius);   // 직사 유닛 탐색 반경 상한 (곡사는 사거리 그대로 — 관측 사격)
        Vector2 avoid = default; float avoidR = 0;
        bool threat = false;
        if (u.Company != null) { LiftActive(u, out avoid, out avoidR); threat = ThreatPriority(u); }   // 틀 행동: 연신 · 지원 위협 우선 (TacticDrill.cs)
        Unit? fpl = u.FireMode == FireMode.FinalLine ? u : null;   // arty3 최후 저지선: 사격선 띠 안의 적만 (FireControl.cs)
        u.Target = FindTarget(u.Owner, u.Pos, radius, u.Weapon?.MinRange ?? 0, u.Weapon is { Los: true } ? u : null, u.Id, TargetJitter(u), u.Weapon, TraverseLocked(u) ? u : null, threat, avoid, avoidR, fpl: fpl);   // 방열한 대포 = 사계 안 우선, 포가를 조금 돌리면 되는 표적까지 (N10)
        if (u.Target == null && u.Weapon is { SpotterIndirect: true } sw && u.FireMode != FireMode.FinalLine)   // arty3 야전포 간접: 직사 표적이 없으면 관측 유닛이 보는 사거리 안 표적 (사선 무시)
            u.Target = FindTarget(u.Owner, u.Pos, sw.Range + u.Radius, sw.MinRange, null, u.Id, 0f, sw, TraverseLocked(u) ? u : null, threat, avoid, avoidR, needSpotter: true);

        if (u.Target != null && !FireAllowed(u, u.Target)) u.Target = null;   // N73 사격 통제: 표적을 잡지 않음 (접적 트리거 아님)
        if (u.Target != null && u.Weapon != null && CrewDangerClose(u, u.Target, u.Weapon, Vector2.Distance(u.Pos, u.Target.Pos))) u.Target = null;   // pursuit2: 아군 근처 표적은 자동으로 잡지 않음 (포탄)
        if (u.Target != null)
        {
            u.LeashOrigin = u.Pos; MarkAlert(u.Owner, u.Pos);   // 적 식별 → 이 구역 경계
            if (u.Order != UnitOrder.Hold) AutoCharge(u, u.Target, u.Target.EdgeDistance(u.Pos) - u.Radius);
        }
    }

    /// <summary>
    /// 반경 안의 적 표적 선택. 우선순위: 공격 가능한 유닛 → 기타 유닛 → 무장 건물 → 건물. 같은 순위는 가까운 순.
    /// minRange 보다 가까운 표적은 제외 (포병). 보이는지는 Fog.CanSee (은폐 탐지 포함).
    /// </summary>
    public Entity? FindTarget(int faction, Vector2 pos, float radius) => FindTarget(faction, pos, radius, 0, null);

    public Entity? FindTarget(int faction, Vector2 pos, float radius, float minRange) => FindTarget(faction, pos, radius, minRange, null);

    /// <summary>losFrom: 직사 사선 검사가 있는 사격자 (weapon.los) — 사선이 막힌 후보는 건너뛴다 (후보가 '지금까지 최선' 일 때만 검사하므로 비용은 작다).</summary>
    public Entity? FindTarget(int faction, Vector2 pos, float radius, float minRange, Entity? losFrom) => FindTarget(faction, pos, radius, minRange, losFrom, 0, 0f);

    /// <summary>
    /// 표적 분산 (ROADMAP §2-4b): jitter > 0 이면 유닛 후보 점수 = 거리 + jitter × 잡음(shooterId, 후보 Id) ∈ [0, jitter) — 가까운 쪽을 선호하되
    /// 사격자마다 다른 후보를 고르게 되어 앞줄 · 돌출 유닛에 사격이 몰리지 않는다. 잡음은 틱과 무관해 재탐색해도 같은 선호가 유지된다. 후보 순회 비용은 그대로.
    /// </summary>
    public Entity? FindTarget(int faction, Vector2 pos, float radius, float minRange, Entity? losFrom, int shooterId, float jitter, WeaponDef? weapon = null, Unit? arcFrom = null,
        bool threat = false, Vector2 avoid = default, float avoidR = 0, bool needSpotter = false, Unit? fpl = null)
    {
        // arty3: needSpotter = 관측 유닛이 보는 표적만 (야전포 간접), fpl = 최후 저지선 띠 안의 표적만
        // threat: 위협 (전차 · 기관총 · 장교) 점수 −1000 (틀 행동 지원 역할). avoid/avoidR: 그 둘레 유닛 제외 (연신).
        float avoidR2 = avoidR * avoidR;
        // weapon: 사격자 무기 — 피해를 줄 수 없는 병과(면역)는 표적에서 제외, PreferCategory 는 점수 −1000 (사거리 · 시야 · 사선을 통과한 후보 중 우선)
        string? prefer = weapon?.PreferCategory;
        bool filter = weapon?.Bonus != null;
        bool preferOfficer = weapon is { PreferOfficer: true }, spotterOnly = weapon is { SpotterOnly: true } || needSpotter;
        long pt0 = Profile ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        try
        {
            if (hashDirty) RebuildHash();
            // 적대 세력 비트 — 적이 한 기도 없으면 바로 끝 (대기 중인 대군이 가장 흔한 경우)
            uint hm = 0;
            for (int f = 0; f < Factions.Count; f++)
                if (IsHostile(faction, f)) hm |= 1u << (f & 31);
            if (((unitMask | bldgMask | trainMask) & hm) == 0) return null;

            Entity? best = null;
            float bestScore = float.MaxValue;
            if ((unitMask & hm) != 0)
            {
                int x0 = Math.Max(0, (int)(pos.X - radius - 2)), x1 = Math.Min(Map.Width - 1, (int)(pos.X + radius + 2));
                int y0 = Math.Max(0, (int)(pos.Y - radius - 2)), y1 = Math.Min(Map.Height - 1, (int)(pos.Y + radius + 2));
                for (int y = y0; y <= y1; y++)
                {
                    int crow = (y >> 3) * coarseW;
                    for (int x = x0; x <= x1;)
                    {
                        int xe = Math.Min(x1, x | 7);
                        if ((coarseMask[crow + (x >> 3)] & hm) == 0) { x = xe + 1; continue; }   // 8×8 묶음에 적이 없으면 건너뜀 (순서는 그대로)
                        for (; x <= xe; x++)
                        {
                            int c = y * Map.Width + x;
                            if ((cellMask[c] & hm) == 0) continue;
                            for (int k = cellStart[c], ke = k + cellCount[c]; k < ke; k++)
                            {
                                int j = sortedIdx[k];
                                if (j >= Units.Count) continue;
                                var e = Units[j];
                                if (!e.Alive || e.Surrendered) continue;   // N79b 항복자는 표적 아님
                                float d = e.EdgeDistance(pos);
                                if (d > radius || d < minRange) continue;
                                if (!IsHostile(faction, e.Owner)) continue;
                                if (filter && !weapon!.CanDamage(e.Def.Category)) continue;
                                if (avoidR2 > 0 && Vector2.DistanceSquared(e.Pos, avoid) < avoidR2) continue;
                                if (fpl != null && !OnFinalLine(fpl, e.Pos)) continue;
                                float score = d + (e.Weapon != null ? 0 : 100);
                                if (arcFrom != null)   // 방열 사계 (N10): 사계 안 우선, 문턱 미만으로 포가를 돌려야 하는 표적은 뒤로 (+5000 + 각), 문턱 이상은 제외
                                {
                                    float sh = TraverseShiftTo(arcFrom, e.Pos);
                                    if (sh > 0) { if (sh >= RedeployRad) continue; score += 5000 + sh; }
                                }
                                if (threat && IsThreat(e)) score -= 1000;
                                if (prefer != null && e.Def.Category == prefer) score -= 1000;
                                if (preferOfficer && e.Def.IsOfficer) score -= 1000;   // 저격수: 장교 우선
                                if (jitter > 0) score += jitter * PairNoise(shooterId, e.Id, Map.Seed);
                                if (score >= bestScore) continue;
                                if (!Fog.CanSee(faction, e)) continue;
                                if (spotterOnly && !SeenBySpotter(faction, e)) continue;
                                if (losFrom != null && !Map.HasFiringLine(losFrom.Pos, e.Pos, losFrom.Id, e.Id)) continue;
                                (best, bestScore) = (e, score);
                            }
                        }
                    }
                }
            }
            if ((trainMask & hm) != 0) FindTrainTarget(faction, pos, radius, minRange, losFrom, weapon, arcFrom, spotterOnly, fpl, ref best, ref bestScore);   // T30b 열차 객차 (Train.cs)
            if ((bldgMask & hm) != 0)
            {
                foreach (var b in Buildings)
                {
                    if ((hm & (1u << (b.Owner & 31))) == 0 || b.Indestructible) continue;
                    float d = b.EdgeDistance(pos);
                    if (d > radius || d < minRange) continue;
                    if (!IsHostile(faction, b.Owner) || !Fog.CanSee(faction, b)) continue;
                    if (fpl != null && !OnFinalLine(fpl, b.Pos)) continue;
                    if (spotterOnly && !SeenBySpotter(faction, b)) continue;
                    float score = d + 300;
                    if (arcFrom != null) { float sh = TraverseShiftTo(arcFrom, b.Pos); if (sh > 0) { if (sh >= RedeployRad) continue; score += 5000 + sh; } }
                    if (score >= bestScore) continue;
                    if (losFrom != null && !Map.HasFiringLine(losFrom.Pos, b.Pos, losFrom.Id, b.Id)) continue;
                    (best, bestScore) = (b, score);
                }
            }
            return best;
        }
        finally { if (Profile) Prof[11] += System.Diagnostics.Stopwatch.GetTimestamp() - pt0; }
    }

    /// <summary>피해 적용: 장갑만큼 감소, 최소 1.</summary>
    public void Damage(Entity target, float amount, Entity? source, string? weapon = null)
    {
        if (target.Indestructible || !target.Alive) return;
        if (amount <= 0) return;   // 면역 (무기 bonus 0 — 소형화기 vs 장갑 차량): 피해 · 반격 · 경보 없음
        float dealt = MathF.Max(1, amount - target.ArmorValue);
        target.Hp -= dealt;
        if (source != null && source.Owner > 0 && (source.Owner == target.Owner || Allied(source.Owner, target.Owner)))   // [측정 전용] 아군 오사 계수 (MoveStats.FriendlyFire)
        {
            string wk = weapon ?? (source is Unit fu ? fu.Def.Weapon : null) ?? "?";
            if (!Stats.FriendlyFire.TryGetValue(wk, out var ff)) Stats.FriendlyFire[wk] = ff = new MoveStats.FriendlyFireLine();
            ff.Hits++; ff.Damage += dealt; if (target.Hp <= 0) ff.Kills++;
        }
        target.LastDamagedTick = Tick;
        CountKill(target, source, weapon);   // 계급 (Combat3.cs)
        LastAttacked[target.Owner] = (target.Pos, Tick);
        MarkAlert(target.Owner, target.Pos);   // 피격 → 그 구역 경계

        // 반격: 대기 중이던 무장 유닛은 공격자를 노린다
        if (source != null) Retaliate(target, source);
    }

    /// <summary>
    /// 사격 1회: 명중률(거리 곡선 × 유닛 명중률, Combat2.cs) 판정 후 피해. 판정은 (틱, 사격자, 표적) 해시로 결정적이다
    /// — 같은 입력이면 항상 같은 결과 (리플레이 · 멀티플레이 동기화 대비). 범위 무기(포병)는 FireArtillery 로.
    /// 사격자는 3초간 은폐 0 (총구 섬광) 이고 자기 구역을 경계 상태로 만든다.
    /// </summary>
    void Fire(Entity shooter, Vector2 from, Entity target, WeaponDef w, float dist)
    {
        if (shooter is Unit su) { su.LastShotTick = Tick; su.RevealUntil = Tick + RevealTicks; if (su.IsOfficer || su.Def.Scout) ProbeOfficerShot(su, target, dist); }
        MarkAlert(shooter.Owner, from);
        if (IsIndirect(w)) { FireArtillery(shooter, from, target, w); return; }
        if (w.Splash > 0 && w.SpotterIndirect && shooter is Unit gu && !HasFiringLine(gu, target, w)) { Stats.SpotterIndirectShots++; FireArtillery(shooter, from, target, w); return; }   // arty3 야전포 간접 사격: 퍼짐 = ScatterAt (Combat2.cs)
        if (w.Splash > 0) { FireGun(shooter, from, target, w, dist); return; }   // 직사포 폭발 (Combat4.cs)
        if (w.Spray > 0) { FireSpray(shooter, from, target, w, dist); return; }   // 기관총 흩뿌림 (Combat3.cs)
        Suppress(target, w, from);   // 제압: 자신을 노린 공격 (명중 무관, Combat3.cs)
        bool hit = Roll(shooter.Id, target.Id) < ShotChance(shooter, target, w, dist);
        Vector2 to = target.Pos;
        if (!hit)
        {
            // 빗나간 탄: 표적 옆으로 비껴감
            float ang = Roll(target.Id, shooter.Id) * MathF.Tau;
            to += new Vector2(DetMath.Cos(ang), DetMath.Sin(ang)) * 0.6f;
            NearMissPath(shooter.Owner, from, to, w.Suppress, target.Id);   // 근접 탄착 제압: 사선 · 착탄점 근처 다른 적 (Suppression.cs)
        }
        Shots.Add((from, to, shooter.Owner, Tick, hit, shooter.Id));
        // 대상 병과 보너스 (바주카 vs 차량 등) 는 장갑 감소 전에 곱한다. 건물에는 보너스 없음.
        if (hit) Damage(target, w.Damage * w.BonusAgainst(CategoryOf(target)), shooter, w.Key);   // CategoryOf: 유닛 · 열차 객차 병과, 건물 null = 1
        else
        {
            MarkAlert(target.Owner, target.Pos);   // 총격을 받으면 (빗나가도) 경계
            Retaliate(target, shooter);   // 빗나가도 반격은 한다
        }
    }

    /// <summary>대기 · 사수 중인 자동 공격 유닛은 공격자를 노린다 (일꾼처럼 autoAttack=false 면 반격하지 않음).</summary>
    void Retaliate(Entity target, Entity attacker)
    {
        if (target is not Unit tu || !tu.Alive || tu.Weapon == null || !tu.Def.AutoAttack || tu.Target != null) return;
        // 공격 이동 중대 대형 구성원 (engage, 2026-10-19): 반응 거리 밖에서 쏘는 적에게도 사거리 안이면 서서 반격 — 맞으면서 그냥 걷거나 서 있지 않는다
        bool formMember = tu.Order == UnitOrder.AttackMove && !tu.Charging && tu.Company?.Anchor is { } fa && ReferenceEquals(fa.Group, tu.Group)
                          && !PassiveFire(tu) && attacker.EdgeDistance(tu.Pos) - tu.Radius <= tu.Weapon.Range;
        if (tu.Order != UnitOrder.Idle && tu.Order != UnitOrder.Hold && !formMember) return;
        if (PassiveFire(tu) && !PassiveRetaliate(tu, attacker)) return;   // N35: 장교 · 정찰병은 자위 조건 밖 사격자에게 반격하지 않음 (OfficerConduct.cs)
        if (!IsHostile(tu.Owner, attacker.Owner)) return;
        if (!FireAllowed(tu, attacker)) return;   // N73: 사격 금지 · 응사만 (피격 직후에는 허용됨)
        tu.Target = attacker;
        tu.LeashOrigin = tu.Pos;
    }

    /// <summary>[0,1) 결정적 난수.</summary>
    float Roll(int a, int b)
    {
        ulong h = (ulong)Tick * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)a * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)b * 0x165667B19E3779F9UL ^ (ulong)(uint)Map.Seed;
        h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33;
        return (h >> 40) / (float)(1UL << 24);
    }
}
