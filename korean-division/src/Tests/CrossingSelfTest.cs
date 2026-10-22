using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 도하 · 역습 (2026-10-19, TacticalAi.Crossing.cs · TacticalAi.Counter.cs): 병목 찾기 (지형 순수 함수) · 단일 다리 도하 (화력 기반 → 도하 → 교두보, 화력 지원 있음) ·
/// 도하 중 저장 → 이어 하기 · 역습 posture · 1.5:1 역습의 예비 투입과 일제 돌격. 창 없이. KD_SELFTEST_ONLY=crossing 으로 단독 실행.
/// 시나리오는 tools/bn_campaign/gen_scen3.py 의 RIV_br1_r2_fs · CTR_r1.5 와 같은 배치 (이름만 ASCII).
/// </summary>
public static class CrossingSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk) => LegacyDeploy.Wrap(() => RunImpl(opt, chk), 3.0f);   // 맥심 속도 3 으로: 시험 시나리오 (맥심 중대 포함 교두보) 는 옛 속도 기준
    static void RunImpl(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[도하 · 역습]");
        try
        {
            TestValues();
            TestFindCrossing();
            TestRiverFire();
            TestRiverCrossingLanes();
            TestFordSlows();
            TestCounterPosture();
            TestBridgeCrossing();
            TestCrossingSnapshot();
            TestCounterattack();
        }
        catch (Exception e) { check(false, $"도하 · 역습 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static int Ticks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));

    // ---------------------------------------------------------------- 시나리오 (gen_scen3 와 같은 배치)

    // 도하 시나리오는 훈련병 (rank 0) 으로 고정: 기본 계급 1 (2026-10-20) 로는 70초 교두보 창 안에 못 건너는 편차가 생김 (튜닝 대상 아님).
    const string RifleCo = "{\"units\":{\"officer\":3,\"infantry\":84,\"infantry_scout\":2},\"formation\":\"line\",\"rank\":0}";
    const string FtCo = "{\"units\":{\"officer_fengtian\":3,\"infantry_fengtian\":84,\"infantry_scout\":2},\"formation\":\"line\",\"rank\":0}";

    static string Fac(string name, string en, int team, string posture) =>
        $"{{\"name\":\"{name}\",\"en\":\"{en}\",\"lang\":\"ko\",\"color\":[60,130,235],\"team\":{team},\"posture\":\"{posture}\",\"area\":\"zone\"}}";

    static string Bn(string id, int side, int x, int y, int facing, string co, int n) =>
        $"{{\"id\":\"{id}\",\"faction\":{side},\"name\":\"{id}\",\"at\":[{x},{y}],\"facing\":{facing},\"formation\":\"line\",\"battalion\":{side},\"battalionForm\":\"line\",\"companies\":[{string.Join(",", Enumerable.Repeat(co, n))}]}}";

    static string Mission(string id, string f1, string f2, int[] zone, string paint, string forces) =>
        $"{{\"id\":\"{id}\",\"title\":\"{id}\",\"seed\":1920,\"weather\":\"clear\",\"time\":\"day\",\"player\":1,\"spectate\":true,\"factions\":[{f1},{f2}]," +
        $"\"areas\":{{\"zone\":{{\"rect\":[{string.Join(",", zone)}]}}}},\"mapData\":{{\"name\":\"{id}\",\"width\":160,\"height\":120,\"base\":\"grass\",\"paint\":[{paint}],\"starts\":[[80,100],[80,30]]}}," +
        "\"objectives\":[{\"id\":\"crush\",\"type\":\"eliminate\",\"percent\":97,\"faction\":2,\"text\":\"crush\"}],\"failures\":[{\"type\":\"losses\",\"percent\":97},{\"type\":\"timeLimit\",\"seconds\":900}],\"events\":[]," +
        $"\"forces\":[{forces}]}}";

    /// <summary>강 (y 52~57, 폭 6) · 다리 1개 (x 78~81), 방어 소총 2 중대 북안, 공격 소총 4 중대 + 기관총 중대 (맥심 5 · 경기 8) + 곡사포 2 · 관측반.</summary>
    static MissionDef River() => MissionDef.Parse(Mission("t_cross_br1", Fac("A", "korean", 1, "attack"), Fac("B", "fengtian", 2, "defend"), new[] { 56, 28, 48, 22 },
        "{\"terrain\":\"dirt\",\"rect\":[78,0,4,120]},{\"terrain\":\"water\",\"rect\":[0,52,160,6]},{\"terrain\":\"dirt\",\"rect\":[78,52,4,6]}",
        Bn("f1_bn", 1, 80, 100, 0, RifleCo, 4) + "," + Bn("f2_bn", 2, 80, 46, 180, FtCo, 2) + "," +
        "{\"id\":\"f1_mg0\",\"faction\":1,\"name\":\"mg\",\"at\":[77,108],\"facing\":0,\"units\":{\"officer\":1,\"heavy_maxim\":5,\"heavy_machinegunner\":8,\"infantry\":4},\"company\":true,\"formation\":\"line\"}," +
        "{\"id\":\"f1_how\",\"faction\":1,\"name\":\"how\",\"at\":[80,114],\"facing\":0,\"units\":{\"heavy_howitzer\":2,\"officer\":1},\"company\":false}," +
        "{\"id\":\"f1_obs\",\"faction\":1,\"name\":\"obs\",\"at\":[80,111],\"facing\":0,\"units\":{\"infantry_scout\":3},\"company\":true}"), "t_cross_br1");

    /// <summary>참호선 (gen_scen trench) 을 1측 2 중대가 점령, 2측 3 중대 (1.5:1) 가 북쪽에서 posture counter 로 탈환.</summary>
    static MissionDef Counter(string posture = "counter") => MissionDef.Parse(Mission("t_counter15", Fac("A", "korean", 1, "defend"), Fac("B", "fengtian", 2, posture), new[] { 56, 34, 48, 16 },
        "{\"terrain\":\"dirt\",\"rect\":[78,0,4,120]},{\"terrain\":\"trench\",\"rect\":[52,40,24,2]},{\"terrain\":\"trench\",\"rect\":[84,40,24,2]},{\"terrain\":\"trench\",\"rect\":[52,40,2,8]},{\"terrain\":\"trench\",\"rect\":[106,40,2,8]}," +
        "{\"terrain\":\"wire\",\"rect\":[48,50,28,1]},{\"terrain\":\"wire\",\"rect\":[84,50,28,1]}",
        Bn("f1_bn", 1, 80, 41, 180, RifleCo, 2) + "," + Bn("f2_bn", 2, 80, 12, 0, FtCo, 3)), "t_counter15");

    static World MissionWorld(MissionDef d, int seed)
    {
        var w = World.CreateMissionWorld(d, defs, seed);
        FactionsSelfTest.PinRifles(w);   // N54
        World.AttachMissionAi(w, 0, all: true);
        return w;
    }

    static TacFaction? St(World w, int f)
    {
        foreach (var (ff, a) in w.Controllers) if (ff == f && a is TacticalAi t) return t.StateOf(f);
        return null;
    }

    // ---------------------------------------------------------------- 시험

    static void TestValues()
    {
        var C = defs.Tactics.Crossing; var K = defs.Tactics.Counter;
        check(C.Enabled && C.MaxLaneWidth >= 4 && C.MinBarrier > C.MaxLaneWidth && C.FireMaxSec >= C.FireMinSec && C.BridgeheadDepth > 0 && K.Enabled && K.MinOdds > 1f && K.AssaultFallbackSec < defs.Tactics.Behaviour.AssaultFallbackSec,
            $"tactics.json crossing · counter 읽음 (건널 곳 폭 ≤{C.MaxLaneWidth}, 화력 기반 {C.FireMinSec}~{C.FireMaxSec}초, 역습 병력비 {K.MinOdds}, 역습 허가 타이머 {K.AssaultFallbackSec}초)");
    }

    static GameMap RiverMap(params (int X, int W)[] bridges)
    {
        var m = new GameMap(160, 120, 1);
        for (int y = 0; y < 120; y++)
        for (int x = 0; x < 160; x++) m.Set(x, y, y is >= 52 and <= 57 ? TerrainType.Water : TerrainType.Grass);
        foreach (var (bx, bw) in bridges)
            for (int y = 52; y <= 57; y++) for (int x = bx; x < bx + bw; x++) m.Set(x, y, TerrainType.Dirt);
        return m;
    }

    static void TestFindCrossing()
    {
        var C = defs.Tactics.Crossing;
        var m1 = RiverMap((78, 4));
        bool f1 = TacticalAi.FindCrossing(m1, new Vector2(60.5f, 70.5f), new Vector2(100.5f, 38.5f), C, out var en, out var ex, out var n, out float wd);
        check(f1 && wd == 4 && MathF.Abs(en.X - 80f) < 0.6f && en.Y > 57 && ex.Y < 52 && n == new Vector2(0, -1),
            $"병목 찾기: 강 + 다리 1개 → 건널 곳 폭 {wd} 입구 ({en.X:0.0},{en.Y:0.0}) 출구 ({ex.X:0.0},{ex.Y:0.0}) 축 {n}");
        var m2 = RiverMap((56, 4), (100, 4));
        bool f2 = TacticalAi.FindCrossing(m2, new Vector2(95.5f, 70.5f), new Vector2(90.5f, 38.5f), C, out var en2, out _, out _, out _, out var en2b, out _, out float wd2);
        check(f2 && MathF.Abs(en2.X - 102f) < 0.6f && wd2 == 4 && MathF.Abs(en2b.X - 58f) < 0.6f, $"병목 찾기: 다리 2개 → 직선에 가까운 다리 (입구 x {en2.X:0.0}) + 둘째 건널 곳 (x {en2b.X:0.0}, 폭 {wd2}) — twoLanes {C.TwoLanes} 면 계획 안 함");
        var m3 = RiverMap((70, 20));
        check(!TacticalAi.FindCrossing(m3, new Vector2(80.5f, 70.5f), new Vector2(60.5f, 38.5f), C, out _, out _, out _, out _), "병목 찾기: 넓은 여울 (폭 20) 은 병목 아님");
        var m4 = new GameMap(160, 120, 1);
        for (int y = 0; y < 120; y++) for (int x = 0; x < 160; x++) m4.Set(x, y, x is >= 75 and <= 82 && y is >= 50 and <= 56 ? TerrainType.Water : TerrainType.Grass);
        check(!TacticalAi.FindCrossing(m4, new Vector2(79.5f, 70.5f), new Vector2(79.5f, 38.5f), C, out _, out _, out _, out _), "병목 찾기: 작은 못 (옆 8칸) 은 돌아감 — 병목 아님");
        check(!TacticalAi.FindCrossing(RiverMap((78, 4)), new Vector2(60.5f, 90.5f), new Vector2(100.5f, 70.5f), C, out _, out _, out _, out _), "병목 찾기: 직선이 강을 안 지나면 없음");
    }

    // ---------------------------------------------------------------- 하천 (T39b): 물 위 사선 · 여울 도하 병목 · NearWater

    static GameMap RiverMapText(int riverW, bool ford)
    {
        string paint = $"{{\"terrain\":\"water\",\"rect\":[30,0,{riverW},64]}}" + (ford ? ",{\"terrain\":\"ford\",\"rect\":[30,20,6,4]}" : "");
        string json = $"{{\"name\":\"rv\",\"width\":64,\"height\":64,\"base\":\"grass\",\"starts\":[[4,4],[60,60]],\"paint\":[{paint}]}}";
        var w = TestDefs.Strip(World.CreateStandard(MapFile.LoadText(json, "rv", 1), defs));
        return w.Map;
    }

    /// <summary>측정 (T39b 선행, 특성화 시험): 현재 물 칸은 직사 사선 (HasFiringLine, rifle los) 을 막는다 (ClearFor: 통행 불가 = 사선 불가). Q6(건너 사격 허용) 적용 시 detcheck 골든이 바뀌어 보류 (DECISIONS_PENDING). 허용으로 바뀌면 이 시험의 기대값을 뒤집는다.</summary>
    static void TestRiverFire()
    {
        foreach (int rw in new[] { 1, 3, 6 })
        {
            var m = RiverMapText(rw, false);
            var a = new Vector2(28.5f, 32.5f); var b = new Vector2(30 + rw + 1.5f, 32.5f);
            bool fl = m.HasFiringLine(a, b, 0, 0), los = m.HasLineOfSight(a, b);
            check(!fl && !los, $"하천 폭 {rw}: 물 건너 사선 {(fl ? "트임" : "막힘")} · 이동용 직선 검사 {(los ? "트임" : "막힘")} (현행: 둘 다 막힘)");
        }
        {
            var m = RiverMapText(1, false);
            m.Set(40, 32, TerrainType.Rock);
            check(!m.HasFiringLine(new Vector2(38.5f, 32.5f), new Vector2(42.5f, 32.5f), 0, 0), "바위는 사선을 막음");
        }
        var w = TestDefs.Strip(World.CreateStandard(MapFile.LoadText("{\"name\":\"rv\",\"width\":64,\"height\":64,\"base\":\"grass\",\"starts\":[[4,4],[60,60]],\"paint\":[{\"terrain\":\"water\",\"rect\":[30,0,1,64]}]}", "rv", 1), defs));
        w.Fog.Enabled = false;
        var s = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(27.5f, 32.5f));
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(33.5f, 32.5f));
        foe.Weapon = null; w.Hold(1, new[] { s }); w.Hold(2, new[] { foe });
        for (int i = 0; i < 60 * 8; i++) { foe.Hp = 100000; w.Step(); }
        check(s.LastShotTick < 0 && s.Target == null, $"물 한 칸 건너 두 사수 (현행): 사격 없음 · 표적 없음 (사격 틱 {s.LastShotTick} 표적 {s.Target?.Id})");
    }

    /// <summary>폭 1~6 하천 + 여울(폭 6): FindCrossing 이 건널 곳을 잡는지 · NearWater 는 Water 만.</summary>
    static void TestRiverCrossingLanes()
    {
        var C = defs.Tactics.Crossing;
        for (int rw = 1; rw <= 6; rw++)
        {
            var m = RiverMapText(rw, true);
            bool f = TacticalAi.FindCrossing(m, new Vector2(20.5f, 40.5f), new Vector2(45.5f, 40.5f), C, out var en, out var ex, out var n, out float wd);
            if (rw >= defs.Tactics.Crossing.MinObstacle) check(f && wd == 4 && MathF.Abs(en.Y - 22f) < 0.6f, $"하천 폭 {rw} + 여울(폭 4): 건널 곳 폭 {wd} 입구 ({en.X:0.0},{en.Y:0.0})");
            else check(!f, $"하천 폭 {rw} (< minObstacle {defs.Tactics.Crossing.MinObstacle}): 병목 아님 (현행; 좁은 띠는 일반 이동 · 여울을 길찾기가 알아서 사용)");
        }
    }

    /// <summary>여울에서 실제로 느려진다 (MoveMul 값만이 아니라 이동 거리): 같은 시간 여울 띠 위 보병 이동량 ≈ 풀밭 × fordMove. 길찾기 비용에는 MoveMul 을 쓰지 않는다 (속도만) — 카드 허용.</summary>
    static void TestFordSlows()
    {
        const string J = "{\"name\":\"fs\",\"width\":64,\"height\":64,\"base\":\"grass\",\"starts\":[[4,4],[60,60]],\"paint\":[{\"terrain\":\"ford\",\"rect\":[5,40,50,3]}]}";
        var w = TestDefs.Strip(World.CreateStandard(MapFile.LoadText(J, "fs", 1), defs));
        w.Fog.Enabled = false;
        var g = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(8.5f, 20.5f));
        var f = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(8.5f, 41.5f));
        w.Move(1, new[] { g }, new Vector2(55.5f, 20.5f)); w.Move(1, new[] { f }, new Vector2(55.5f, 41.5f));
        for (int i = 0; i < 60 * 10; i++) w.Step();
        float dg = g.Pos.X - 8.5f, df = f.Pos.X - 8.5f, ratio = df / MathF.Max(dg, 0.001f);
        float fm = defs.Terrain.FordMove;
        check(dg > 3f && df > 1f && df < dg && MathF.Abs(ratio - fm) < 0.12f, $"여울 감속 실측: 10초 이동 풀밭 {dg:0.0} · 여울 {df:0.0} (비 {ratio:0.00} ≈ fordMove {fm})");
    }

    static void TestCounterPosture()
    {
        var d = Counter();
        check(d.Factions[1].Posture == "counter" && AiProfile.ParsePosture("counter") == AiPosture.Counter, "미션 posture counter 검증 통과 · 역습 태세 파싱");
        var w = MissionWorld(d, 1);
        for (int i = 0; i < 40; i++) w.Step();
        var s = St(w, 2);
        bool ok = s != null && s.Directives.Exists(x => x.Posture == AiPosture.Counter) && s.Effective == AiPosture.Attack;
        check(ok, $"역습 지시 그룹: 태세 Counter → 실제 과제 공격 ({s?.Effective})");
    }

    static void TestBridgeCrossing()
    {
        var w = MissionWorld(River(), 1);
        long plan = 0, fireSet = 0, cross = 0, bridge = 0; int fireAtCross = 0, nearAtCross = 0, farAtBridge = 0; bool phaseOrder = true; int lastPhase = 0;
        Vector2 exit = default, nn = default;
        int max = Ticks(600);
        for (int i = 0; i < max && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 15 != 0 || St(w, 1) is not { Phase: 2 } s) continue;
            var r = s.Regiments.FirstOrDefault(x => x.CrossPhase != 0);
            if (r == null) continue;
            if (r.CrossPhase < lastPhase && r.CrossPhase != 0) phaseOrder = false;
            lastPhase = r.CrossPhase;
            if (plan == 0) { plan = w.Tick; exit = r.CrossExit; nn = r.CrossDir; }
            if (cross == 0 && r.CrossStartTick > 0)
            {
                cross = r.CrossStartTick; fireSet = r.CrossFireTick;
                foreach (var g in s.Groups)
                    if (g.CrossRegiment == r.Id && g.CrossStage is TacticalAi.CsFireBase or TacticalAi.CsAssemble)
                    {
                        if (g.State == TacState.Engage) fireAtCross++;
                        if (Vector2.Dot(g.Center - r.CrossEntry, r.CrossDir) < 0) nearAtCross++;
                    }
            }
            if (bridge == 0 && r.CrossBridgeTick > 0)
            {
                bridge = r.CrossBridgeTick;
                farAtBridge = w.Units.Count(u => u.Alive && u.Owner == 1 && u.Def.IsInfantry && Vector2.Dot(u.Pos - exit, nn) >= 0);
            }
        }
        int far = w.Units.Count(u => u.Alive && u.Owner == 1 && u.Def.IsInfantry && Vector2.Dot(u.Pos - exit, nn) >= 0);
        check(plan > 0 && exit.Y < 52 && exit.X is > 77 and < 82, $"단일 다리: 도하 계획 ({plan / 60}초, 출구 ({exit.X:0},{exit.Y:0}))");
        check(fireSet > 0 && fireSet <= cross && fireAtCross >= 1 && nearAtCross >= 1, $"화력 기반이 먼저: 자리 잡음 {fireSet / 60}초 ≤ 도하 시작 {cross / 60}초, 그때 강둑에서 사격 중인 중대 {fireAtCross} (이쪽 둑 {nearAtCross})");
        check(bridge > cross && farAtBridge >= 60 && phaseOrder, $"교두보 성립 {bridge / 60}초 (건너편 보병 {farAtBridge}명), 국면 순서 유지 {phaseOrder}");
        check(w.Winner == 1 || far >= 100, $"단일 다리 도하 완료 (화력 지원): 승자 {w.Winner} · {w.Tick / 60}초, 건너편 보병 {far}명");
    }

    static void TestCrossingSnapshot()
    {
        var w = MissionWorld(River(), 2);
        int max = Ticks(300);
        for (int i = 0; i < max; i++)
        {
            w.Step();
            if (i % 15 == 0 && St(w, 1) is { } s && s.Regiments.Exists(r => r.CrossPhase is TacticalAi.CpCrossing or TacticalAi.CpBridgehead)) break;
        }
        var s0 = St(w, 1);
        int phase = s0?.Regiments.Max(r => r.CrossPhase) ?? 0;
        var bytes = Snapshot.Save(w, new SnapshotMeta("tactical", 1, true, "tactical"));
        var l = Snapshot.Load(bytes, defs).World;
        for (int i = 0; i < Ticks(20); i++) { w.Step(); l.Step(); }
        var sl = St(l, 1); var sw = St(w, 1);
        bool same = sl != null && sw != null && sl.Regiments.Select(r => (r.CrossPhase, r.CrossNext, r.CrossQueue.Count)).SequenceEqual(sw.Regiments.Select(r => (r.CrossPhase, r.CrossNext, r.CrossQueue.Count)))
                    && sl.Groups.Select(g => g.CrossStage).SequenceEqual(sw.Groups.Select(g => g.CrossStage));
        check(phase >= TacticalAi.CpCrossing && w.ComputeChecksum() == l.ComputeChecksum() && same, $"도하 중 (국면 {phase}) 저장 → 불러오기 → 20초: 체크섬 · 도하 상태 같음");
    }

    static void TestCounterattack()
    {
        var w = MissionWorld(Counter(), 1);
        int stage1 = 0, stage2 = 0; bool committed = false; int assaultGroups = 0; float closest = float.MaxValue;
        int max = Ticks(600);
        for (int i = 0; i < max && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 15 != 0 || St(w, 2) is not { Phase: 2 } s) continue;
            foreach (var r in s.Regiments)
            {
                if (r.CounterStage >= 1 && stage1 == 0) { stage1 = (int)(w.Tick / 60); committed = r.ReserveCommitted && s.Battalions.Where(b => b.Regiment == r.Id).All(b => b.ReserveCommitted || b.ReserveGroup == 0); }
                if (r.CounterStage == 2 && stage2 == 0) stage2 = (int)(w.Tick / 60);
            }
            // 돌격 중대 (CounterAssaultTick) 가 점령측 보병에 얼마나 다가갔나 (일제 돌격 = 속보로 돌격 거리까지 → 착검 돌격)
            int ag = 0;
            foreach (var g in s.Groups)
            {
                if (g.CounterAssaultTick == 0 || g.Kind != TacKind.Line) continue;
                ag++;
                foreach (var u in w.Units) if (u.Alive && u.Owner == 1 && u.Def.IsInfantry) closest = MathF.Min(closest, Vector2.Distance(u.Pos, g.Center));
            }
            assaultGroups = Math.Max(assaultGroups, ag);
        }
        var st = St(w, 2)!;
        float cr = St(w, 2)!.Profile.Tactics.ChargeRange * (St(w, 2)!.RifleRange / 16f);
        int holder = w.Units.Count(u => u.Alive && u.Owner == 1), counter = w.Units.Count(u => u.Alive && u.Owner == 2);
        check(stage1 > 0 && committed, $"1.5:1 역습: 접촉 → 고정 사격 {stage1}초, 예비 투입 {committed}");
        check(stage2 > stage1 && st.CounterAssaults >= 1 && assaultGroups >= 1 && (st.Charges >= 1 || closest <= cr + 2f),
            $"1.5:1 역습: 일제 돌격 {stage2}초 (돌격 국면 {st.CounterAssaults}회, 돌격 중대 최대 {assaultGroups}, 점령측과 최소 거리 {closest:0.0} ≤ 돌격 거리 {cr:0.0}+2 또는 착검 돌격 명령 {st.Charges}) — 결과 승자 {w.Winner}, 점령측 {holder} · 역습측 {counter}명");
    }
}
