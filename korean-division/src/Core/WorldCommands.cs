using System.Numerics;

namespace KoreanDivision.Core;

public enum CommandResult { Ok, NotAllowed }

/// <summary>
/// 명령 API. 사람(Game)과 AI 가 똑같이 이 메서드만 사용한다.
///  · 공개 메서드(Move, Train, ...)는 즉시 검증 결과를 돌려주고 명령 객체(GameCommand)를 대기열에 넣는다.
///  · 실제 상태 변경은 다음 틱 시작에 Execute 가 한다 — 이때 다시 검증하고, 실패하면 Notices 로 알린다.
///  · 실행된 명령은 CommandLog 에 (틱, 명령) 으로 기록된다 → 리플레이 · 멀티플레이 동기화의 기반.
/// faction 인자로 호출자를 검증해 남의 유닛/건물을 조작할 수 없게 한다.
/// </summary>
public sealed partial class World
{
    readonly List<GameCommand> pending = new();
    readonly List<GameCommand> executing = new();

    /// <summary>치트 명령(디버그 생성 · 지도 공개)이 한 번이라도 실행됐는지 — 리플레이에 표시.</summary>
    public bool CheatsUsed { get; private set; }

    /// <summary>실행된 모든 명령 (틱 순). RecordCommands=false 면 기록하지 않는다.</summary>
    public readonly List<CommandRecord> CommandLog = new();
    public bool RecordCommands = true;

    /// <summary>리플레이 재생 시: 매 틱 시작에 그 틱의 기록된 명령을 넣어준다.</summary>
    public Func<long, IEnumerable<GameCommand>>? CommandFeed;

    /// <summary>명령을 대기열에 넣는다 (다음 틱 시작에 실행).</summary>
    public void Issue(GameCommand command) => pending.Add(command);

    /// <summary>대기 중인 명령을 지금 실행. Step 이 매 틱 시작에 호출 (테스트에서 직접 호출해도 된다).</summary>
    public void FlushCommands()
    {
        if (CommandFeed != null) pending.AddRange(CommandFeed(Tick));
        if (pending.Count == 0) return;
        cmdOfficersTick = long.MinValue;   // 지휘 계통 지연의 장교 목록은 명령 묶음마다 새로 (CommandDelay.cs)
        executing.AddRange(pending);
        pending.Clear();
        foreach (var c in executing)
        {
            if (c.Faction <= 0 || c.Faction >= Factions.Count) continue;
            if (RecordCommands) CommandLog.Add(new CommandRecord(Tick, c));
            Execute(c);
        }
        executing.Clear();
    }

    void Execute(GameCommand command)
    {
        int f = command.Faction;
        execSel = command.Sel;
        try { ExecuteInner(command, f); } finally { execSel = 0; }
    }

    void ExecuteInner(GameCommand command, int f)
    {
        TaCancelByOrder(command);   // N32: 이 중대 유닛에 내린 다른 명령 = 참호 돌격 취소
        QueueCancelByOrder(command);   // N74: Shift 없는 명령 = 대기열에서 빠짐 (OrderQueue.cs)
        switch (command)
        {
            case MoveCommand c: ExecGroupOrder(f, ResolveOrder(f, c.Units), c.Target, UnitOrder.Move, c.Form); break;
            case AttackMoveCommand c: ExecGroupOrder(f, ResolveOrder(f, c.Units), c.Target, UnitOrder.AttackMove, c.Form); break;
            case QuickStepCommand c: ExecGroupOrder(f, ResolveOrder(f, c.Units).Where(u => u.CanQuickStep).ToList(), c.Target, UnitOrder.Move, c.Form, quick: true, cross: c.Cross); break;
            case RetreatCommand c: ExecRetreat(f, c); break;   // 집결 지점 반영 (Rally.cs, N79a)
            case SetRallyCommand c: ExecSetRally(f, c.Companies, c.Point, c.Clear); break;
            case GroundFireExCommand c: ExecGroundFire(f, ResolveOrder(f, c.Units), c.Target, c.Kind, c.Dir); break;   // 포격 종류 (GroundFire.cs, N79a)
            case AttackCommand c: if (Find(c.TargetId) is Entity t) ExecAttack(f, ResolveOrder(f, c.Units), t); break;
            case GroundFireCommand c: ExecGroundFire(f, ResolveOrder(f, c.Units), c.Target, GroundFireKind.Sustained, Vector2.Zero); break;
            case StopCommand c: foreach (var u in ResolveOrder(f, c.Units)) ResetOrder(u); break;
            case HoldCommand c:
                foreach (var u in ResolveOrder(f, c.Units))
                {
                    int spot = u.TrenchSpot; bool inTrench = u.InTrench;
                    ResetOrder(u);
                    if (!HoldKeepsTrench(u, spot, inTrench)) u.Order = UnitOrder.Hold;   // 참호 안 · 자리로 가는 중이면 그 자리를 지킨다 (Trenches.cs)
                }
                break;
            case DebugSpawnCommand c:
                CheatsUsed = true;
                if (Defs.Units.TryGetValue(c.Unit, out var sd))
                    for (int i = 0; i < c.Count; i++)
                        SpawnUnit(sd, f, c.Center + new Vector2(i % 5 - 2, i / 5 - 2) * 0.8f);
                break;
            case RevealMapCommand c:
                CheatsUsed = true;
                Factions[f].RevealAll = c.On;
                Fog.Update();
                break;
            case SetCombatSettingCommand c: ExecSetCombatSetting(f, c.Setting, c.Value); break;
            case ChargeCommand c: ExecCharge(f, ResolveOrder(f, c.Units), c.Target, c.TargetId != 0 ? Find(c.TargetId) : null); break;
            case ThrowGrenadeCommand c: ExecThrowGrenade(f, Resolve(f, c.Units), c.Target); break;   // 수류탄 (Combat4.cs)
            case ThrowSmokeCommand c: ExecThrowSmoke(Resolve(f, c.Units), c.Target); break;   // 연막 수류탄 (Breach.cs, N65)
            case CutWireCommand c: ExecCutWire(ResolveOrder(f, c.Units), c.Target); break;   // 철조망 절단 (Breach.cs, N59)
            case MountCommand mc: foreach (var u in Resolve(f, mc.Units)) ExecMount(u, mc.Mount); break;   // N61
            case ChargeOnlyCommand c: foreach (var u in Resolve(f, c.Units)) if (u.CanCharge && u.Def.Mounted) u.ChargeOnly = c.On; break;
            case SetScoutAutoCommand c: ExecSetScoutAuto(f, Resolve(f, c.Units), c.On); break;   // 정찰병 자율 토글 (Scouts.cs)
            case MissionEndCommand c: ExecMissionEnd(c.Won); break;   // [치트] 미션 즉시 종료 (Mission/WorldMission.cs)
            case CloseRanksCommand c: ExecCloseRanks(f, c.Companies); break;   // 대열 정비 (CloseRanks.cs)
            case QueueOrderCommand c: ExecQueueOrder(c); break;   // N74 명령 대기열 (OrderQueue.cs)
            case SetFireModeCommand c: ExecSetFireMode(f, c.Units, c.Mode); break;   // N73 사격 통제 (FireControl.cs)
            case SetFinalLineCommand c: ExecSetFinalLine(f, c.Units, c.Point); break;   // arty3 최후 저지선 (FireControl.cs)
            case SetPostureCommand c: ExecSetPosture(f, c.Companies, c.Posture); break;   // 중대 자세 (CoverSeek.cs, tweak1)
            case TrenchAssaultCommand c: ExecTrenchAssault(f, c.Companies, c.Target, c.On, c.Ai); break;   // 참호 돌격 (TrenchAssault.cs, N32)
            case SetKeepFormationCommand c: ExecKeepFormation(f, c.Companies, c.On); break;   // 중대 대형 유지 (ContactF2.cs, F1 2차)
            case OccupyPositionsCommand c: ExecOccupyPositions(f, ResolveOrder(f, c.Units), c.Target, c.Dir, c.Radius, c.CoverMaxHit); break;   // 진지 점유 (CoverSeek.cs, engage)
            case DispatchOrderCommand c: ExecDispatchOrder(f, c); break;   // 대대장 앞 명령 전달 지연 (Comms.cs)
            default:
                ExecuteOrganisation(command);   // 편제 (Organisation.cs)
                break;
        }
    }

    void Notify(int faction, CommandResult r)
    {
        if (r != CommandResult.Ok) Notices.Add((faction, r));
    }

    /// <summary>Id 목록 → 살아 있는 자기 유닛.</summary>
    List<Unit> Resolve(int faction, int[] ids)
    {
        var list = new List<Unit>(ids.Length);
        foreach (int id in ids)
            if (Find(id) is Unit u && u.Owner == faction) list.Add(u);
        return list;
    }

    static int[] Ids(int faction, IEnumerable<Unit> units) =>
        units.Where(u => u.Alive && u.Owner == faction).Select(u => u.Id).ToArray();

    // ---------------------------------------------------------------- 공개 API (검증 + 대기열)

    /// <summary>form = 진형 (null · Free = 자유). 진행 방향 Dir 이 비어 있으면 실행 시 무게중심 → 목표로 정한다.</summary>
    public void Move(int faction, IEnumerable<Unit> units, Vector2 target, FormSpec? form = null) => IssueUnits(faction, units, ids => new MoveCommand(faction, ids, target, NormForm(form)));
    public void AttackMove(int faction, IEnumerable<Unit> units, Vector2 target, FormSpec? form = null) => IssueUnits(faction, units, ids => new AttackMoveCommand(faction, ids, target, NormForm(form)));
    public void QuickStep(int faction, IEnumerable<Unit> units, Vector2 target, FormSpec? form = null, bool cross = false) => IssueUnits(faction, units.Where(u => u.CanQuickStep), ids => new QuickStepCommand(faction, ids, target, NormForm(form), cross));   // cross = 도하 강행 (pronemove 면제)

    /// <summary>자유 진형이라도 우클릭 드래그(방향 · 폭)가 있으면 남긴다 — 중대 깃발 이동 · 폭 지정 횡대에 쓰인다.</summary>
    static FormSpec? NormForm(FormSpec? f) => f == null || (f.IsFree && f.Width <= 0 && f.Dir.LengthSquared() < 1e-6f) ? null : f;
    public void Stop(int faction, IEnumerable<Unit> units) => IssueUnits(faction, units, ids => new StopCommand(faction, ids));
    public void Hold(int faction, IEnumerable<Unit> units) => IssueUnits(faction, units, ids => new HoldCommand(faction, ids));
    /// <summary>진지 점유 (engage): 지점 둘레 radius 안 참호 자리 → 엄폐 칸 (명중 배수 ≤ coverMaxHit) 에 병사 배정, 도착하면 사수. dir = 적 쪽.</summary>
    public void OccupyPositions(int faction, IEnumerable<Unit> units, Vector2 target, Vector2 dir, float radius, float coverMaxHit) =>
        IssueUnits(faction, units, ids => new OccupyPositionsCommand(faction, ids, target, dir, radius, coverMaxHit));

    /// <summary>강제 지정 공격: 전쟁 · 중립 상대 모두 가능, 자기 세력 · 동맹은 불가.</summary>
    public void Attack(int faction, IEnumerable<Unit> units, Entity target)
    {
        if (!target.Alive || target.Indestructible || !IsHostile(faction, target.Owner)) return;
        IssueUnits(faction, units.Where(u => u.Weapon != null), ids => new AttackCommand(faction, ids, target.Id));
    }

    /// <summary>착검 돌격 (ROADMAP §2-4b): 근접 무기가 있는 유닛만. target 이 공격 가능한 적이면 그 적에게, 아니면 point 로 돌격 이동.</summary>
    public void Charge(int faction, IEnumerable<Unit> units, Vector2 point, Entity? target = null)
    {
        int tid = target != null && target.Alive && !target.Indestructible && IsHostile(faction, target.Owner) && target is not Unit { Def.IsArmored: true } ? target.Id : 0;
        IssueUnits(faction, units.Where(u => u.CanCharge), ids => new ChargeCommand(faction, ids, tid != 0 ? target!.Pos : point, tid));
    }

    /// <summary>N36 섞인 선택 돌격 (플레이어): 돌격 가능한 유닛 (근접 무기) 은 돌격, 나머지는 사수 (그 자리에서 엄호 사격 — 공격 명령의 '비전투는 따라 이동' 과 달리
    /// 기관총 · 포가 돌격 대열을 쫓아가며 철수 · 재방열하지 않게). 돌격 가능한 유닛이 없으면 아무것도 안 한다.</summary>
    public void ChargeMixed(int faction, IEnumerable<Unit> units, Vector2 point, Entity? target = null)
    {
        var list = units.ToList();
        if (!list.Any(u => u.CanCharge)) return;
        Charge(faction, list, point, target);
        Hold(faction, list.Where(u => !u.CanCharge));
    }

    void IssueUnits(int faction, IEnumerable<Unit> units, Func<int[], GameCommand> make)
    {
        var ids = Ids(faction, units);
        if (ids.Length == 0) return;
        var cmd = make(ids);
        if (IssueSel > 0) cmd = cmd with { Sel = IssueSel };   // 플레이어 명령: 선택 인원을 명령에 싣는다 (N37)
        Issue(cmd);
    }

    /// <summary>[UI 문맥 — 시뮬 상태 아님, 저장 안 함] 플레이어 (Game) 가 명령을 내리는 동안의 선택 인원. 0 이면 AI · 스크립트 명령.
    /// 공개 명령 API 가 만든 명령의 Sel 에 복사될 뿐이고, 시뮬은 명령에 실린 Sel 만 읽는다 (리플레이 결정성).</summary>
    public int IssueSel;

    /// <summary>[작업값] 지금 실행 중인 명령의 Sel (Execute 동안만, Acknowledge 의 지연 배수).</summary>
    int execSel;

    // ---------------------------------------------------------------- 실행 (다음 틱 시작, 재검증 포함)

    void ExecGroupOrder(int faction, List<Unit> list, Vector2 target, UnitOrder order, FormSpec? form = null, bool quick = false, bool retreat = false, bool cross = false)
    {
        if (list.Count == 0) return;
        var all = list;
        list = StartCompanyOrders(list, target, order, form);   // 중대 소속은 깃발 대형 이동 (OrgFormation.cs), 나머지는 아래 옛 방식
        if (quick) foreach (var u in all) if (u.Company != null) { u.QuickStep = !retreat || u.CanQuickStep; u.ForceCross = cross && u.QuickStep; }   // 깃발 대형 이동 중대도 속보 (깃발 속도 = 가장 느린 구성원), 후퇴는 속보 가능한 병사만
        if (retreat) foreach (var u in all) u.Retreating = true;   // 후퇴 표시 (Pursuit.cs)
        if (list.Count == 0) return;
        // 한 명 · 진형 없는 이동: 목표가 참호 근처면 빈 참호 자리로 (Trenches.cs)
        int single = list.Count == 1 && form == null ? SnapSlotsToTrenches(list, new[] { target }, order == UnitOrder.AttackMove)[0] : -1;
        var group = new MoveGroup(this, single >= 0 ? TrenchSpotPos(single) : target, list.Count);
        var (planUnits, spots) = ApplyFormation(group, list, group.Goal, form, null, order == UnitOrder.AttackMove);
        foreach (var u in list)
        {
            ResetOrder(u);
            u.Order = order;
            u.Group = group;
            u.QuickStep = quick && (!retreat || u.CanQuickStep);
            u.ForceCross = cross && u.QuickStep;   // pronemove 도하 면제
            u.Retreating = retreat;
            Acknowledge(u);
        }
        RegisterBig(group, list);
        if (planUnits != null && spots != null) ClaimSnapped(planUnits, spots);   // 진형 슬롯 → 참호 자리 (슬롯 오프셋은 ApplyFormation 이 이미 옮김)
        else if (single >= 0) ClaimTrenchSpot(list[0], single);
    }

    /// <summary>진형 지정이 있으면(2기 이상) 슬롯을 배정해 그룹에 붙인다. 구성원 등록(Init)은 건너뛰고 슬롯이 오프셋을 대신한다.
    /// 참호 근처 슬롯은 빈 참호 자리로 옮기고 (Id 순 유닛, 유닛별 자리) 를 돌려준다 — 자리는 ResetOrder 뒤에 맡긴다.</summary>
    (List<Unit>? Units, int[]? Spots) ApplyFormation(MoveGroup g, List<Unit> list, Vector2 center, FormSpec? form, Vector2[]? from, bool attackMove = false)
    {
        form = LooseForm(list, form, out var shapeLocal);   // 분대만 따로 = 분대 대형, 사선은 기병만 (CombatFormations.cs)
        if (form == null || list.Count < 2) return (null, null);
        if (form.IsFree)
        {
            if (form.Width <= 0) return (null, null);
            form = form with { Type = (int)FormType.Line };   // 자유 + 우클릭 드래그 폭 = 그 폭의 횡대
        }
        // 같은 입력 → 같은 배정: 유닛은 Id 순으로 (명령의 Id 배열 순서와 무관)
        var sorted = list.OrderBy(u => u.Id).ToList();
        Vector2[]? f2 = null;
        if (from != null) f2 = sorted.Select(u => from[list.IndexOf(u)]).ToArray();
        var plan = Formations.Build(sorted, center, form, ShiftSlot, f2, shapeLocal);
        g.Plan = plan;
        g.ArriveRadius = 0.6f;
        g.Inited = true;                // Init(압축 오프셋)은 쓰지 않는다
        if (g.Big)
        {
            // 큰 그룹 경로 공유용 리더: 무게중심에 가장 가까운 유닛
            Vector2 c = Vector2.Zero;
            foreach (var u in sorted) c += u.Pos;
            c /= sorted.Count;
            g.Leader = sorted.OrderBy(u => Vector2.DistanceSquared(u.Pos, c)).ThenBy(u => u.Id).First();
        }
        g.Members = null;
        // 참호 근처 슬롯 → 빈 참호 자리 (Trenches.cs)
        var slots = new Vector2[sorted.Count];
        for (int i = 0; i < sorted.Count; i++) slots[i] = g.Goal + (plan.Off.TryGetValue(sorted[i].Id, out var o) ? o : Vector2.Zero);
        var spots = SnapSlotsToTrenches(sorted, slots, attackMove);
        bool any = false;
        for (int i = 0; i < sorted.Count; i++)
            if (spots[i] >= 0) { plan.Off[sorted[i].Id] = TrenchSpotPos(spots[i]) - g.Goal; any = true; }
        return any ? (sorted, spots) : (null, null);
    }

    void ExecAttack(int faction, List<Unit> list, Entity target)
    {
        if (!target.Alive || target.Indestructible || !IsHostile(faction, target.Owner)) return;
        foreach (var u in list)
        {
            if (u.Weapon == null) continue;
            ResetOrder(u);
            u.Order = UnitOrder.Attack;
            u.Target = target;
            u.LeashOrigin = u.Pos;
            Acknowledge(u);
        }
    }

    void ExecCharge(int faction, List<Unit> list, Vector2 point, Entity? target)
    {
        bool entity = target != null && target.Alive && !target.Indestructible && IsHostile(faction, target.Owner) && target is not Unit { Def.IsArmored: true };   // 전차 · 장갑차에는 근접 불가
        var chargers = list.Where(u => u.CanCharge && OfficerJoinsCharge(u, list)).ToList();   // N35: 장교는 같은 중대 병사와 함께일 때만 돌격 (OfficerConduct.cs)
        foreach (var u in list) if (u.IsOfficer && u.CanCharge && !chargers.Contains(u)) Stats.OfficerChargeBlocked++;
        if (chargers.Count == 0) return;
        if (entity)
        {
            foreach (var u in chargers)
            {
                ResetOrder(u);
                u.Order = UnitOrder.Attack;
                u.Target = target;
                u.LeashOrigin = u.Pos;
                u.ChargeState = 2;
                Acknowledge(u);
                BeginFix(u, true);   // N57 착검 (Bayonet.cs)
            }
            foreach (var u in chargers) ProbeOfficerCharge(u, "order", target);
            return;
        }
        var group = new MoveGroup(this, point, chargers.Count);
        foreach (var u in chargers)
        {
            ResetOrder(u);
            u.Order = UnitOrder.AttackMove;
            u.Group = group;
            u.ChargeState = 2;
            Acknowledge(u);
            BeginFix(u, true);   // N57 착검 (Bayonet.cs)
        }
        foreach (var u in chargers) ProbeOfficerCharge(u, "order", null);
        RegisterBig(group, chargers);
    }

    /// <summary>
    /// 진형 없는 큰 그룹: 구성원을 명령 때 모두 등록한다 (리더 · 압축 오프셋 계산용). 예전에는 반응 지연 중인 틱에만 등록해서
    /// 지연이 0 인 병사는 빠지고, 등록이 적으면 큰 그룹이 꺼져 전원이 목표 한 점으로 몰렸다 (docs/COLLISION_ANALYSIS.md (c)-4).
    /// </summary>
    static void RegisterBig(MoveGroup g, List<Unit> list)
    {
        if (!g.Big || g.Inited || g.Members == null) return;
        foreach (var u in list)
            if (!ReferenceEquals(u.FormGroup, g)) { g.Members.Add(u); u.FormGroup = g; }
    }

    /// <summary>
    /// 명령 반응 지연 (ROADMAP §2-4d 14차): 이동 · 공격 명령을 받으면 0~reactDelayMaxTicks 틱 뒤 출발 (유닛 · 명령마다 결정적 해시 — 부대가 일제히 튀어나가지 않음).
    /// 정지 · 사수는 즉시. 명령 표시 · 음성(LastOrderTick)은 즉시. 시뮬레이션 값이라 리플레이에서도 똑같다.
    /// </summary>
    void Acknowledge(Unit u)
    {
        float mul = PlayerSelDelayMul(execSel);   // 플레이어 명령: 선택 인원이 적을수록 짧게 (N37, CommandDelay.cs)
        u.ReactDelay = (int)(ReactDelayFor(u) * mul) + ChainDelayTicks(u, mul * (execSel > 0 && u.ChargeState == 2 ? Defs.CombatRules.ChargeOrderDelayMul : 1f));   // 출발 지연 0~2틱 (플레이어 돌격 = 지휘 지연 ×chargeOrderDelayMul) + 지휘 계통 지연 (CommandDelay.cs)
        u.LastOrderTick = Tick;
    }

    void ResetOrder(Unit u)
    {
        LeaveTrench(u);   // 참호 자리 해제 + 이번 명령 동안 그 참호 묶음은 도중 점유 안 함 (Trenches.cs)
        u.CoverHold = false;   // 엄폐 찾기 이동 취소 (CoverSeek.cs)
        u.WireCutTile = -1; u.WireCutTicks = 0;   // 철조망 절단 취소 (Breach.cs)
        if (u.SpotTick >= 0) Stats.SpotReleases++;
        u.SpotTick = -1; u.HasZone = false;   // F1: 새 명령 = 사격 위치 · 구역 해제 (Contact.cs)
        u.ReactDelay = 0;
        u.FixLeft = 0;   // 착검 중 새 명령 = 착검 취소 (BayonetFixed 는 그대로)
        u.ChargeState = 0;
        u.Order = UnitOrder.Idle;
        u.GroundPoint = Vector2.Zero;
        u.GroundKind = GroundFireKind.Sustained; u.GroundRounds = 0; u.GroundDir = Vector2.Zero; u.GroundStart = 0;   // 포격 종류 (N79a)
        u.Group = u.LastGroup = null;
        u.QuickStep = false; u.ForceCross = false;
        u.Retreating = false;
        u.Target = null;
        u.Velocity = Vector2.Zero;
    }
}
