using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.Tests;

/// <summary>N74 명령 대기열 (OrderQueue.cs): 이어 붙이기 · 진행 · 취소 · 중대 · 공격 다리 건너뜀 · 사수 끝 · 저장 왕복 · 리플레이. KD_SELFTEST_ONLY=orderq.</summary>
public static class OrderQueueSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("orderq", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[N74 명령 대기열]");
        try
        {
            using (TestDefs.NoCommandDelay())
            {
                TestLegs();
                TestCancel();
                TestWireSmoke();
                TestCompany();
                TestSkipDeadTarget();
                TestRoundTrip();
            }
            TestChainDelay();
        }
        catch (Exception e) { check(false, $"orderq 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(bool record = false)
    {
        const int size = 100;
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = record };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    static List<Unit> Squad(World w, int n, Vector2 at)
    {
        var us = new List<Unit>();
        for (int i = 0; i < n; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, at + new Vector2(i, 0)));
        w.Step();
        return us;
    }

    static Vector2 Cen(List<Unit> us) { var c = Vector2.Zero; foreach (var u in us) c += u.Pos; return c / us.Count; }

    /// <summary>다리 3개 (이동 → 이동 → 사수): 첫 다리 바로 실행, 도착하면 다음, 끝은 사수 · 대기열 사라짐.</summary>
    static void TestLegs()
    {
        var w = Bare();
        var us = Squad(w, 3, new Vector2(20, 20));
        var A = new Vector2(20, 35); var B = new Vector2(35, 35);
        w.QueueOrder(1, us, QueuedKind.Move, A); w.Step();
        bool first = us.All(u => u.Order == UnitOrder.Move) && w.OrderQueues.Count == 1;
        w.QueueOrder(1, us, QueuedKind.Move, B); w.Step();
        w.QueueOrder(1, us, QueuedKind.Hold, Vector2.Zero); w.Step();
        int legs = w.OrderQueues.Count == 1 ? w.OrderQueues[0].Legs.Count : -1;
        bool passedA = false;
        for (int i = 0; i < 60 * 40 && w.OrderQueues.Count > 0; i++) { w.Step(); if (Vector2.Distance(Cen(us), A) < 2.5f) passedA = true; }
        Steps(w, 5);
        float dB = Vector2.Distance(Cen(us), B);
        check(first && legs == 2 && passedA && dB < 3f && us.All(u => u.Order == UnitOrder.Hold) && w.OrderQueues.Count == 0,
            $"대기열: 첫 다리 바로 {first} · 남은 다리 {legs} (2) · A 경유 {passedA} · B 도착 거리 {dB:0.0} · 끝 사수 {us.Count(u => u.Order == UnitOrder.Hold)}/3 · 대기열 정리 {w.OrderQueues.Count == 0}");

        // 바쁜 유닛에 Shift = 지금 명령 끝난 뒤
        var w2 = Bare();
        var us2 = Squad(w2, 2, new Vector2(20, 20));
        w2.Move(1, us2, new Vector2(20, 32)); w2.Step();
        w2.QueueOrder(1, us2, QueuedKind.Move, new Vector2(32, 32)); w2.Step();
        bool waits = w2.OrderQueues.Count == 1 && w2.OrderQueues[0].Legs.Count == 1 && !w2.OrderQueues[0].HasCurrent;
        for (int i = 0; i < 60 * 30 && w2.OrderQueues.Count > 0; i++) w2.Step();
        Steps(w2, 5);
        check(waits && Vector2.Distance(Cen(us2), new Vector2(32, 32)) < 3f, $"대기열: 이동 중에 Shift = 지금 명령 뒤에 (대기 {waits}, 끝 거리 {Vector2.Distance(Cen(us2), new Vector2(32, 32)):0.0})");
    }

    /// <summary>Shift 없는 명령 = 대기열 버림. 다른 묶음 = 새 대기열 (그 유닛만 옛 대기열에서 빠짐). 죽으면 빠짐.</summary>
    static void TestWireSmoke()
    {
        var w = Bare(record: true);
        w.Map.Set(30, 40, TerrainType.Wire);
        var us = new List<Unit>();
        for (int i = 0; i < 2; i++) us.Add(w.SpawnUnit(defs.Units["infantry_assault"], 1, new Vector2(30.5f + i, 35.5f)));
        w.Step();
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(30, 30)); w.Step();
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(40, 30)); w.Step();
        bool queued = w.OrderQueues.Count == 1;
        w.ThrowSmokeAt(1, new[] { us[0] }, new Vector2(30.5f, 37.5f)); w.Step();
        bool smokeKeeps = w.OrderQueues.Count == 1 && w.OrderQueues[0].Units.Count == 2;
        w.CutWire(1, new[] { us[0] }, new Vector2(30.5f, 40.5f)); w.Step();
        bool cutLeaves = w.OrderQueues.Count == 1 && w.OrderQueues[0].Units.Count == 1 && w.OrderQueues[0].Units[0] == us[1].Id;
        check(queued && smokeKeeps && cutLeaves, $"철조망/연막: 대기열 {queued} · 연막 던져도 유지 {smokeKeeps} · 철조망 절단 = 그 유닛만 대기열에서 빠짐 {cutLeaves}");
    }

    static void TestCancel()
    {
        var w = Bare();
        var us = Squad(w, 3, new Vector2(20, 20));
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(20, 40)); w.Step();
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(40, 40)); w.Step();
        w.Move(1, us, new Vector2(10, 20)); w.Step();
        bool cancelled = w.OrderQueues.Count == 0;
        Steps(w, 60 * 20);
        check(cancelled && Vector2.Distance(Cen(us), new Vector2(10, 20)) < 3f, $"취소: Shift 없는 이동 = 대기열 버림 ({cancelled}) · 새 목표로");

        var w2 = Bare();
        var us2 = Squad(w2, 3, new Vector2(20, 20));
        w2.QueueOrder(1, us2, QueuedKind.Move, new Vector2(20, 40)); w2.Step();
        w2.QueueOrder(1, us2, QueuedKind.Move, new Vector2(40, 40)); w2.Step();
        w2.QueueOrder(1, us2.Take(1), QueuedKind.Hold, Vector2.Zero); w2.Step();
        bool split = w2.OrderQueues.Count == 2 && w2.QueueOf(us2[0]) is { Units.Count: 1 } && w2.QueueOf(us2[1]) is { Units.Count: 2, Legs.Count: 1 };
        w2.Stop(1, us2.Skip(1)); w2.Step();
        bool stop = w2.QueueOf(us2[1]) == null && w2.QueueOf(us2[2]) == null;
        check(split && stop, $"취소: 다른 묶음 Shift = 새 대기열 · 옛 대기열은 나머지만 ({split}) · 정지 = 버림 ({stop})");

        var w3 = Bare();
        var us3 = Squad(w3, 2, new Vector2(20, 20));
        w3.QueueOrder(1, us3, QueuedKind.Move, new Vector2(20, 40)); w3.Step();
        w3.QueueOrder(1, us3, QueuedKind.Move, new Vector2(40, 40)); w3.Step();
        us3[0].Hp = 0; Steps(w3, 3);
        bool dead = w3.OrderQueues.Count == 1 && w3.OrderQueues[0].Units.Count == 1 && w3.OrderQueues[0].Units[0] == us3[1].Id;
        check(dead, $"죽은 구성원은 대기열에서 빠짐 ({dead})");
    }

    /// <summary>중대 (깃발 대형 이동) 도 다리마다 함께 도착한 뒤 다음 다리.</summary>
    static void TestCompany()
    {
        var w = Bare();
        var us = Squad(w, 8, new Vector2(20, 20));
        w.FormCompany(1, us); w.Step();
        bool co = us.All(u => u.Company != null);
        var A = new Vector2(20, 40); var B = new Vector2(45, 40);
        w.QueueOrder(1, us, QueuedKind.Move, A); w.Step();
        w.QueueOrder(1, us, QueuedKind.AttackMove, B); w.Step();
        bool passedA = false; int ticks = 0;
        for (; ticks < 60 * 60 && w.OrderQueues.Count > 0; ticks++) { w.Step(); if (Vector2.Distance(Cen(us), A) < 3f) passedA = true; }
        Steps(w, 30);
        float dB = Vector2.Distance(Cen(us), B);
        check(co && passedA && dB < 4f && w.OrderQueues.Count == 0, $"중대 대기열: A 경유 {passedA} · B 거리 {dB:0.0} · {ticks / 60f:0.0}초");
    }

    /// <summary>공격 다리의 표적이 실행 전에 죽으면 건너뛰고 다음 다리.</summary>
    static void TestSkipDeadTarget()
    {
        var w = Bare();
        var us = Squad(w, 3, new Vector2(20, 20));
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(80, 80)); foe.FireMode = FireMode.Hold;
        w.Step();
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(20, 30)); w.Step();
        w.QueueOrder(1, us, QueuedKind.Attack, foe.Pos, foe.Id); w.Step();
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(30, 30)); w.Step();
        foe.Hp = 0;
        for (int i = 0; i < 60 * 30 && w.OrderQueues.Count > 0; i++) w.Step();
        Steps(w, 5);
        float d = Vector2.Distance(Cen(us), new Vector2(30, 30));
        check(d < 3f && us.All(u => u.Target == null), $"공격 다리 표적 소멸 = 건너뜀 → 다음 이동 (끝 거리 {d:0.0})");
    }

    /// <summary>뒤 다리는 지휘 계통 지연 없음 (첫 다리는 보통 명령과 같음).</summary>
    static void TestChainDelay()
    {
        var w = Bare();
        var us = Squad(w, 4, new Vector2(20, 20));
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(20, 26)); w.Step();
        int firstMax = us.Max(u => u.ReactDelay);
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(26, 26)); w.Step();
        int laterMax = -1;
        for (int i = 0; i < 60 * 30 && w.OrderQueues.Count > 0; i++)
        {
            w.Step();
            if (w.OrderQueues.Count > 0 && w.OrderQueues[0].Legs.Count == 0 && laterMax < 0) laterMax = us.Max(u => u.ReactDelay);
        }
        int basicMax = (int)MathF.Round(defs.CombatRules.ReactDelayMaxTicks) + 1;
        check(firstMax > 0 && laterMax >= 0 && laterMax <= basicMax, $"지연: 첫 다리 최대 {firstMax}틱 (지휘 계통 포함) · 뒤 다리 최대 {laterMax}틱 (≤ 반응 지연 {basicMax})");
    }

    static void TestRoundTrip()
    {
        var w = Bare(record: true);
        var us = Squad(w, 4, new Vector2(20, 20));
        w.QueueOrder(1, us, QueuedKind.Move, new Vector2(20, 35), 0, new FormSpec((int)FormType.Line, 0, Vector2.Zero, 6)); w.Step();
        w.QueueOrder(1, us, QueuedKind.QuickStep, new Vector2(35, 35)); w.Step();
        w.QueueOrder(1, us, QueuedKind.Hold, Vector2.Zero); w.Step();
        Steps(w, 60);
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool fields = y.OrderQueues.Count == 1 && y.OrderQueues[0].Legs.Count == w.OrderQueues[0].Legs.Count && y.OrderQueues[0].Units.SequenceEqual(w.OrderQueues[0].Units);
        var h0 = w.ComputeChecksum();
        w.OrderQueues[0].Legs.RemoveAt(0); bool hash = w.ComputeChecksum() != h0;
        var y2 = Snapshot.Load(bytes, defs).World;
        Steps(y, 900); Steps(y2, 900);
        check(same && fields && hash && y.ComputeChecksum() == y2.ComputeChecksum(), $"저장: 바이트 동일 {same} · 필드 {fields} · 체크섬 반영 {hash} · 이어 돌려도 같음 (형식 {Snapshot.FormatVersion})");

        var cmds = w.CommandLog.Select(c => c.Command).OfType<QueueOrderCommand>().ToList();
        bool json = cmds.Count == 3;
        if (json)
        {
            var s = JsonSerializer.Serialize<GameCommand>(cmds[0], CommandJson.Options);
            var back = JsonSerializer.Deserialize<GameCommand>(s, CommandJson.Options) as QueueOrderCommand;
            json = back != null && back.Kind == cmds[0].Kind && back.Target == cmds[0].Target && back.Form == cmds[0].Form && back.Units.SequenceEqual(cmds[0].Units);
        }
        ulong Run()
        {
            var a = Bare();
            var ua = Squad(a, 4, new Vector2(20, 20));
            a.QueueOrder(1, ua, QueuedKind.Move, new Vector2(20, 35)); a.Step();
            a.QueueOrder(1, ua, QueuedKind.AttackMove, new Vector2(35, 35)); a.Step();
            Steps(a, 900);
            return a.ComputeChecksum();
        }
        ulong r1 = Run(), r2 = Run();
        check(json && r1 == r2, $"리플레이: 대기열 명령 {cmds.Count}건 · JSON 왕복 {json} · 같은 명령 = 같은 체크섬 ({r1:X16}) (리플레이 형식 {ReplayFile.FormatVersion})");
    }
}

/// <summary>N73 사격 통제 + N75 사선 막힘 사수 표시 판정 (KD_SELFTEST_ONLY=firectl).</summary>
public static class FireCtlSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        N73SelfTest.Run(opt, check);
        Console.WriteLine("[N75 사선 막힘 사수 표시]");
        try { TestBlocker(check); }
        catch (Exception e) { check(false, $"firectl 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static void TestBlocker(Action<bool, string> check)
    {
        var defs = Defs.Current;
        const int size = 80;
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        for (int y = 10; y < 30; y++) for (int x = 30; x < 40; x++) m.Set(x, y, TerrainType.Forest);   // 깊은 숲 띠 (폭 10 — forestSeeDepth 3 보다 깊음)
        for (int y = 40; y < 60; y++) for (int x = 30; x < 33; x++) m.SetHigh(x, y, true);           // 고지 띠
        m.Set(34, 74, TerrainType.Rock); m.Set(34, 75, TerrainType.Rock); m.Set(34, 76, TerrainType.Rock);
        var w = new World(m, defs, 2);
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;

        // 1) 무작위 쌍: 표시 판정 (FiringLineBlock) 과 시뮬 판정 (HasFiringLine) 이 같음
        int n = 0, agree = 0;
        uint s = 12345;
        float R() { s = s * 1664525 + 1013904223; return (s >> 8) / 16777216f; }
        for (int i = 0; i < 4000; i++)
        {
            var a = new Vector2(1 + R() * (size - 2), 1 + R() * (size - 2));
            var b = new Vector2(1 + R() * (size - 2), 1 + R() * (size - 2));
            n++;
            if ((m.FiringLineBlock(a, b, 0, 0) == LosBlock.None) == m.HasFiringLine(a, b, 0, 0)) agree++;
        }
        check(agree == n, $"사선 막힘 판정: 표시용 FiringLineBlock 과 시뮬 HasFiringLine 일치 {agree}/{n}");

        // 2) 사수 분류: 숲 · 고지 · 바위 · 트임, 시뮬 상태 불변
        Unit Shooter(Vector2 p) { var u = w.SpawnUnit(defs.Units["infantry"], 1, p); u.FireMode = FireMode.Hold; return u; }
        Unit Foe(Vector2 p) { var u = w.SpawnUnit(defs.Units["infantry"], 2, p); u.FireMode = FireMode.Hold; u.Hp = 1e9f; return u; }
        var sForest = Shooter(new Vector2(28.5f, 20.5f)); Foe(new Vector2(41.5f, 20.5f));
        var sHigh = Shooter(new Vector2(27.5f, 50.5f)); Foe(new Vector2(36.5f, 50.5f));
        var sRock = Shooter(new Vector2(31.5f, 75.5f)); Foe(new Vector2(37.5f, 75.5f));
        var sOpen = Shooter(new Vector2(60.5f, 5.5f)); Foe(new Vector2(66.5f, 5.5f));
        w.Step();
        var h0 = w.ComputeChecksum();
        IEnumerable<Unit> Near(Vector2 p, float r) => w.Units.Where(e => Vector2.Distance(e.Pos, p) <= r);
        var f = OrderOverlay.Classify(w, sForest, 1, Near);
        var hi = OrderOverlay.Classify(w, sHigh, 1, Near);
        var rk = OrderOverlay.Classify(w, sRock, 1, Near);
        var op = OrderOverlay.Classify(w, sOpen, 1, Near);
        OrderOverlay.UpdateBlocked(w, 1, (Vector2.Zero, new Vector2(size, size)), 100000);
        int cnt = OrderOverlay.BlockedCount;
        bool pure = w.ComputeChecksum() == h0;
        check(f == LosBlock.Forest && hi == LosBlock.High && rk == LosBlock.Obstacle && op == LosBlock.None && cnt == 3 && pure,
            $"사선 막힘 사수: 숲 {f} · 고지 {hi} · 바위 {rk} · 트임 {op} · 표시 {cnt}명 (3) · 시뮬 체크섬 불변 {pure}");
    }
}
