using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>N74 대기열 명령 종류 (이동 경로 · 공격 · 행동).</summary>
public enum QueuedKind : byte { Move = 0, AttackMove = 1, QuickStep = 2, Attack = 3, Hold = 4, Charge = 5 }

/// <summary>
/// N74 명령 대기열 (Shift + 명령, 사용자 승인 2026-10-20 · 지휘 편의 ④). 대기열 = (유닛 묶음, 다리 목록). 리플레이 기록 (Replay 21).
/// 규칙:
///  · 같은 유닛 묶음 (살아 있는 구성원 Id 집합이 같음) 의 대기열이 있으면 끝에 덧붙인다. 아니면 그 유닛들을 다른 대기열에서 빼고 새 대기열을 만든다.
///  · 새 대기열의 구성원이 모두 할 일이 없으면 (이동 · 공격 이동 · 공격 중이 아님) 첫 다리를 바로 실행 — 보통 명령과 같다 (N37 지연 곡선 · 지휘 계통 지연 그대로).
///    아니면 지금 하던 일이 끝난 뒤 실행.
///  · 다리 진행: 구성원 전원 (살아 있는) 이 지금 다리를 끝내면 (도착 · 표적 소멸 = Idle/Hold) 다음 다리를 실행 (대기열마다 한 틱에 한 다리).
///    뒤 다리는 이미 받은 명령이므로 지휘 계통 지연 없음 (반응 지연 0~2틱 · N37 배수만) — 결정 2026-10-22.
///  · 취소: 그 유닛에 Shift 없이 내린 이동 · 공격 · 정지 · 사수 · 돌격 · 후퇴 · 포격 · 진지 점유 · 참호 돌격 명령 = 대기열에서 빠짐 (남은 다리 버림). 죽으면 빠짐.
///  · 공격 다리의 표적이 실행 때 죽었거나 안 보이면 (적대 아님 포함) 건너뜀.
/// 상태 = World.OrderQueues (ORG_ 끝, WriteComms 뒤 — FormatVersion 54). 틱 · 명령의 함수 — 결정적.
/// </summary>
public sealed record QueueOrderCommand(int Faction, int[] Units, QueuedKind Kind, Vector2 Target, int TargetId = 0, FormSpec? Form = null) : GameCommand(Faction);

public readonly record struct QueuedLeg(QueuedKind Kind, Vector2 Target, int TargetId, FormSpec? Form);

public sealed class OrderQueue
{
    public int Id;
    public int Faction;
    public int Sel;                                // 처음 명령의 선택 인원 (N37 배수 — 뒤 다리도 같은 배수)
    public readonly List<int> Units = new();       // 구성원 Id (오름차순)
    public readonly List<QueuedLeg> Legs = new();  // 아직 실행 안 한 다리
    public bool HasCurrent;                        // 지금 실행 중인 다리 (표시용 · 진행 판정)
    public QueuedLeg Current;
}

public sealed partial class World
{
    /// <summary>N74 명령 대기열 (Id 순). 저장 · 체크섬.</summary>
    public readonly List<OrderQueue> OrderQueues = new();
    int nextQueueId = 1;
    /// <summary>[작업값] 대기열 다리를 실행하는 중 (취소 훅 건너뜀 · 뒤 다리는 지휘 계통 지연 없음). 틱 안에서만.</summary>
    bool queueExec, queueNoChain;

    public void QueueOrder(int faction, IEnumerable<Unit> units, QueuedKind kind, Vector2 target, int targetId = 0, FormSpec? form = null) =>
        IssueUnits(faction, units, ids => new QueueOrderCommand(faction, ids, kind, target, targetId, NormForm(form)));

    /// <summary>[읽기] 이 유닛이 든 대기열 (없으면 null).</summary>
    public OrderQueue? QueueOf(Unit u)
    {
        foreach (var q in OrderQueues) if (q.Faction == u.Owner && q.Units.BinarySearch(u.Id) >= 0) return q;
        return null;
    }

    static bool Busy(Unit u) => u.Order is UnitOrder.Move or UnitOrder.AttackMove or UnitOrder.Attack;

    void ExecQueueOrder(QueueOrderCommand c)
    {
        int f = c.Faction;
        if (!Enum.IsDefined(typeof(QueuedKind), c.Kind) || !float.IsFinite(c.Target.X) || !float.IsFinite(c.Target.Y)) return;
        var list = Resolve(f, c.Units).Where(u => u.Alive).Select(u => u.Id).Distinct().ToList();
        if (list.Count == 0) return;
        list.Sort();
        var leg = new QueuedLeg(c.Kind, c.Target, c.TargetId, c.Form);
        OrderQueue? q = null;
        foreach (var e in OrderQueues)
            if (e.Faction == f && e.Units.Count == list.Count && e.Units.SequenceEqual(list)) { q = e; break; }
        if (q != null) { q.Legs.Add(leg); return; }
        RemoveFromQueues(f, list);
        q = new OrderQueue { Id = nextQueueId++, Faction = f, Sel = c.Sel };
        q.Units.AddRange(list);
        OrderQueues.Add(q);
        bool idle = true;
        foreach (int id in list) if (Find(id) is Unit u && Busy(u)) { idle = false; break; }
        if (idle) RunLeg(q, leg, chain: true);
        else q.Legs.Add(leg);
    }

    void RemoveFromQueues(int faction, IEnumerable<int> ids)
    {
        if (OrderQueues.Count == 0) return;
        foreach (int id in ids)
            foreach (var q in OrderQueues)
                if (q.Faction == faction) { int i = q.Units.BinarySearch(id); if (i >= 0) q.Units.RemoveAt(i); }
        OrderQueues.RemoveAll(q => q.Units.Count == 0);
    }

    static int[]? CmdUnits(GameCommand command) => command switch
    {
        MoveCommand c => c.Units, AttackMoveCommand c => c.Units, QuickStepCommand c => c.Units, RetreatCommand c => c.Units,
        AttackCommand c => c.Units, StopCommand c => c.Units, HoldCommand c => c.Units, ChargeCommand c => c.Units,
        OccupyPositionsCommand c => c.Units, GroundFireCommand c => c.Units, GroundFireExCommand c => c.Units, CutWireCommand c => c.Units, _ => null,   // 철조망 절단은 Order=Move (ThrowSmoke 는 Order 를 안 바꿔 제외)
    };

    /// <summary>Shift 없는 유닛 명령 = 그 유닛은 대기열에서 빠진다 (대기열 다리 실행 중에는 건너뜀).</summary>
    void QueueCancelByOrder(GameCommand command)
    {
        if (queueExec || OrderQueues.Count == 0) return;
        int[]? ids = CmdUnits(command);
        if (command is TrenchAssaultCommand { On: true } ta)
            ids = Companies.Where(co => co.Owner == ta.Faction && ta.Companies.Contains(co.Id)).SelectMany(co => co.Members).Select(u => u.Id).ToArray();
        if (ids != null) RemoveFromQueues(command.Faction, ids);
    }

    /// <summary>매 틱 (명령 실행 뒤): 죽은 구성원 정리, 다리가 끝난 대기열은 다음 다리 실행 (대기열마다 한 틱에 하나).</summary>
    void StepOrderQueues()
    {
        if (OrderQueues.Count == 0) return;
        for (int qi = 0; qi < OrderQueues.Count; qi++)
        {
            var q = OrderQueues[qi];
            q.Units.RemoveAll(id => Find(id) is not Unit { Alive: true } u || u.Owner != q.Faction);
            if (q.Units.Count == 0) continue;
            bool done = true;
            foreach (int id in q.Units) if (Find(id) is Unit u && (Busy(u) || u.ReactDelay > 0)) { done = false; break; }
            if (!done) continue;
            if (q.Legs.Count == 0) { q.Units.Clear(); continue; }   // 마지막 다리까지 끝 = 대기열 끝
            var leg = q.Legs[0];
            q.Legs.RemoveAt(0);
            RunLeg(q, leg, chain: false);
        }
        OrderQueues.RemoveAll(q => q.Units.Count == 0);
    }

    /// <summary>다리 하나를 보통 명령처럼 실행 (명령 기록은 안 함 — 대기열 상태가 저장 · 체크섬되므로 리플레이는 같은 다리를 다시 실행).</summary>
    void RunLeg(OrderQueue q, QueuedLeg leg, bool chain)
    {
        q.HasCurrent = true; q.Current = leg;
        int f = q.Faction;
        var units = new List<Unit>();
        foreach (int id in q.Units) if (Find(id) is Unit { Alive: true } u && u.Owner == f) units.Add(u);
        if (units.Count == 0) return;
        int[] all = units.Select(u => u.Id).ToArray();
        var cmds = new List<GameCommand>(2);
        switch (leg.Kind)
        {
            case QueuedKind.Move: cmds.Add(new MoveCommand(f, all, leg.Target, leg.Form)); break;
            case QueuedKind.AttackMove: cmds.Add(new AttackMoveCommand(f, all, leg.Target, leg.Form)); break;
            case QueuedKind.QuickStep: cmds.Add(new QuickStepCommand(f, units.Where(u => u.CanQuickStep).Select(u => u.Id).ToArray(), leg.Target, leg.Form)); break;
            case QueuedKind.Hold: cmds.Add(new HoldCommand(f, all)); break;
            case QueuedKind.Attack:
                if (Find(leg.TargetId) is not Entity t || !t.Alive || t.Indestructible || !IsHostile(f, t.Owner) || !Fog.CanSee(f, t)) break;   // 표적 소멸 · 안 보임 = 건너뜀
                cmds.Add(new AttackCommand(f, units.Where(u => u.Weapon != null).Select(u => u.Id).ToArray(), t.Id));
                cmds.Add(new MoveCommand(f, units.Where(u => u.Weapon == null).Select(u => u.Id).ToArray(), t.Pos));   // 비전투 유닛은 따라 이동 (UI 우클릭 공격과 같음)
                break;
            case QueuedKind.Charge:
            {
                if (!units.Exists(u => u.CanCharge)) break;
                int tid = 0; Vector2 p = leg.Target;
                if (leg.TargetId != 0 && Find(leg.TargetId) is Entity ct && ct.Alive && !ct.Indestructible && IsHostile(f, ct.Owner) && ct is not Unit { Def.IsArmored: true } && Fog.CanSee(f, ct)) { tid = ct.Id; p = ct.Pos; }
                cmds.Add(new ChargeCommand(f, units.Where(u => u.CanCharge).Select(u => u.Id).ToArray(), p, tid));
                cmds.Add(new HoldCommand(f, units.Where(u => !u.CanCharge).Select(u => u.Id).ToArray()));   // N36 섞인 선택 돌격과 같음
                break;
            }
        }
        bool e0 = queueExec, c0 = queueNoChain;
        queueExec = true; queueNoChain = !chain;
        try { foreach (var c in cmds) if (CmdUnits(c) is { Length: > 0 }) Execute(c with { Sel = q.Sel }); }
        finally { queueExec = e0; queueNoChain = c0; }
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 (ORG_ 끝, WriteComms 뒤 — FormatVersion 54)

    static void WriteLeg(SnapWriter w, in QueuedLeg l)
    {
        w.Byte((byte)l.Kind); w.Vec(l.Target); w.I(l.TargetId);
        w.Bool(l.Form != null);
        if (l.Form is { } fs) { w.I(fs.Type); w.I(fs.Order); w.Vec(fs.Dir); w.F(fs.Width); }
    }

    static QueuedLeg ReadLeg(SnapReader r)
    {
        byte k = r.Byte();
        if (!Enum.IsDefined(typeof(QueuedKind), k)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "queued order kind"));
        var t = r.Vec(); int tid = r.I();
        FormSpec? fs = r.Bool() ? new FormSpec(r.I(), r.I(), r.Vec(), r.F()) : null;
        if (!float.IsFinite(t.X) || !float.IsFinite(t.Y)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "queued order target"));
        return new QueuedLeg((QueuedKind)k, t, tid, fs);
    }

    void WriteOrderQueues(SnapWriter w)
    {
        w.I(nextQueueId);
        w.I(OrderQueues.Count);
        foreach (var q in OrderQueues)
        {
            w.I(q.Id); w.I(q.Faction); w.I(q.Sel);
            w.I(q.Units.Count); foreach (int id in q.Units) w.I(id);
            w.I(q.Legs.Count); foreach (var l in q.Legs) WriteLeg(w, l);
            w.Bool(q.HasCurrent); if (q.HasCurrent) WriteLeg(w, q.Current);
        }
    }

    void ReadOrderQueues(SnapReader r)
    {
        OrderQueues.Clear();
        nextQueueId = r.I();
        int n = r.Count(4);
        for (int i = 0; i < n; i++)
        {
            var q = new OrderQueue { Id = r.I(), Faction = r.I(), Sel = r.I() };
            int nu = r.Count(1);
            for (int k = 0; k < nu; k++) q.Units.Add(r.I());
            int nl = r.Count(4);
            for (int k = 0; k < nl; k++) q.Legs.Add(ReadLeg(r));
            q.HasCurrent = r.Bool();
            if (q.HasCurrent) q.Current = ReadLeg(r);
            if (q.Faction <= 0 || q.Faction >= Factions.Count || q.Id <= 0 || q.Id >= nextQueueId) throw new SnapshotException(string.Format(SnapMsg.Invalid, "order queue"));
            OrderQueues.Add(q);
        }
    }

    static void HashLeg(ref Hasher h, in QueuedLeg l)
    {
        h.Add((int)l.Kind); h.Add(l.Target.X); h.Add(l.Target.Y); h.Add(l.TargetId);
        if (l.Form is { } fs) { h.Add(1); h.Add(fs.Type); h.Add(fs.Order); h.Add(fs.Dir.X); h.Add(fs.Dir.Y); h.Add(fs.Width); } else h.Add(0);
    }

    void HashOrderQueues(ref Hasher h)
    {
        if (OrderQueues.Count == 0 && nextQueueId == 1) return;   // 대기열을 쓰지 않은 판 = 체크섬 그대로 (골든 불변)
        h.Add(nextQueueId);
        foreach (var q in OrderQueues)
        {
            h.Add(q.Id); h.Add(q.Faction); h.Add(q.Sel);
            h.Add(q.Units.Count);
            foreach (int id in q.Units) h.Add(id);
            h.Add(q.Legs.Count); foreach (var l in q.Legs) HashLeg(ref h, l);
            h.Add(q.HasCurrent ? 1 : 0); if (q.HasCurrent) HashLeg(ref h, q.Current);
        }
    }
}
