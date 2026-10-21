using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>진형(부대별 설정 · 슬롯 배정 · 좁은 길 통과) + 전차 좁은 길(여유 공간 그래프 · 정체 해소) 검증. 창 없이. SelfTest 가 호출한다.</summary>
public static class FormationSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[진형 · 전차 좁은 길]");
        TestClearanceValues();
        TestLargeGraphIncremental();
        TestControlGroupForms();
        TestFormShapes();
        TestOrdering();
        TestBlockedSlots();
        TestNarrowPassage();
        TestTankCorridor();
        TestTankDetour();
        TestTankEscapesTightSpot();
        TestMixedGroupNarrow();
        TestFormationReplay();
    }

    // ---------------------------------------------------------------- 도우미

    static GameMap Open(int w, int h)
    {
        var m = new GameMap(w, h, 1);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) m.Set(x, y, TerrainType.Grass);
        return m;
    }

    static void Rock(GameMap m, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++) m.Set(x, y, TerrainType.Rock);
    }

    static World Bare(GameMap map, bool record = false)
    {
        var w = new World(map, defs, 2) { RecordCommands = record };
        var hq = defs.Headquarters;
        for (int f = 1; f <= 2; f++)
        {
            bool done = false;
            for (int r = 0; r < map.Width / 2 && !done; r++)
            for (int k = 0; k <= r && !done; k++)
            {
                int x = f == 1 ? 1 + k : map.Width - 1 - hq.Size - k, y = f == 1 ? 1 + r - k : map.Height - 1 - hq.Size - (r - k);
                if (map.CanPlace(x, y, hq.Size)) { w.PlaceBuilding(hq, f, x, y); done = true; }
            }
        }
        return w;
    }

    static List<Unit> Spawn(World w, string def, int n, Vector2 center, float spread = 2.5f, int seed = 5)
    {
        var rng = new Random(seed);
        var list = new List<Unit>();
        for (int i = 0; i < n; i++)
        {
            var p = center + new Vector2((float)(rng.NextDouble() - 0.5), (float)(rng.NextDouble() - 0.5)) * spread * 2;
            list.Add(w.SpawnUnit(defs.Units[def], 1, p));
        }
        return list;
    }

    static bool RunUntilIdle(World w, List<Unit> us, float seconds)
    {
        int max = (int)(seconds * 60);
        for (int t = 0; t < max; t++)
        {
            w.Step();
            if (t > 30 && us.All(u => !u.Alive || u.Order == UnitOrder.Idle)) { for (int k = 0; k < 20; k++) w.Step(); return true; }
        }
        return false;
    }

    static (float X0, float X1, float Y0, float Y1) Bounds(IEnumerable<Unit> us)
    {
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        foreach (var u in us) { x0 = MathF.Min(x0, u.Pos.X); x1 = MathF.Max(x1, u.Pos.X); y0 = MathF.Min(y0, u.Pos.Y); y1 = MathF.Max(y1, u.Pos.Y); }
        return (x0, x1, y0, y1);
    }

    static float MinPair(List<Unit> us)
    {
        float m = float.MaxValue;
        for (int i = 0; i < us.Count; i++)
        for (int j = i + 1; j < us.Count; j++) m = MathF.Min(m, Vector2.Distance(us[i].Pos, us[j].Pos));
        return m;
    }

    static float AngDiff(float a, float b)
    {
        float d = a - b;
        while (d > MathF.PI) d -= 2 * MathF.PI;
        while (d < -MathF.PI) d += 2 * MathF.PI;
        return MathF.Abs(d);
    }

    // ---------------------------------------------------------------- 여유 공간

    static void TestClearanceValues()
    {
        var m = Open(20, 20);
        Rock(m, 10, 10, 10, 10);
        float open = m.TileClearance(3, 3, 3f), adj = m.TileClearance(9, 10), diag = m.TileClearance(9, 9), self = m.TileClearance(10, 10);
        check(open >= 3f - 1e-4f && MathF.Abs(adj - 0.5f) < 1e-4f && MathF.Abs(diag - 0.70710678f) < 1e-3f && self == 0,
            $"칸 여유 거리: 열린 곳 {open:0.0}, 벽 바로 옆 {adj:0.00}, 모서리 옆 {diag:0.00}, 막힌 칸 {self}");
        // 큰 그래프: 벽 옆(4방향 이웃이 막힌 칸)은 통행 불가, 모서리만 닿으면 가능
        var nav = new Nav(m, World.LargeClearance);
        nav.Sync();
        check(!nav.Walk(9, 10) && nav.Walk(9, 9) && nav.Walk(3, 3) && !nav.Walk(10, 10), "큰 유닛 그래프: 벽 바로 옆 칸은 제외, 모서리 옆 · 열린 칸은 통행");
    }

    static void TestLargeGraphIncremental()
    {
        var m = Open(70, 70);
        Rock(m, 20, 20, 24, 30);
        var w = Bare(m);
        var big = w.Nav.Large;
        ulong before = big.Signature();
        w.PlaceBuilding(TestDefs.Wall(3), 1, 40, 40);
        w.PlaceBuilding(TestDefs.Wall(3), 1, 15, 15);
        w.Nav.Sync();
        ulong inc = big.Signature();
        ulong fresh = new Nav(m, World.LargeClearance).Signature();
        check(before != inc && inc == fresh, "큰 유닛 그래프: 건물을 놓은 뒤 증분 갱신 == 처음부터 구축");
    }

    // ---------------------------------------------------------------- 대형 순환 (UI 순수 로직)

    static void TestControlGroupForms()
    {
        bool a = FormCycle.NextType(FormType.Box) == FormType.Spread && FormCycle.NextType(FormType.Spread) == FormType.Free;
        bool b = FormCycle.NextCompanyType(FormType.Spread) == FormType.Line && FormCycle.NextCompanyType(FormType.Line) == FormType.Column;
        bool c = FormCycle.NextBatForm(BatForm.Waves) == BatForm.Line && FormCycle.NextOrder(FormOrder.InfantryFront) == FormOrder.Mixed;   // 파상 (2026-10-19) 이 마지막
        check(a && b && c, $"대형 순환: 기본 {a}, 중대(자유 건너뜀) {b}, 대대 · 병과 배치 {c}");
    }

    // ---------------------------------------------------------------- 진형 모양

    static void TestFormShapes()
    {
        var results = new Dictionary<FormType, (float W, float H, bool Idle, float MinD, float Face)>();
        foreach (var type in new[] { FormType.Line, FormType.Column, FormType.Wedge, FormType.Box })
        {
            var w = Bare(Open(90, 50));
            var us = Spawn(w, "infantry", 12, new Vector2(12, 25));
            w.Move(1, us, new Vector2(60, 25), new FormSpec((int)type, 0, new Vector2(1, 0)));
            bool idle = RunUntilIdle(w, us, 50);
            var (x0, x1, y0, y1) = Bounds(us);
            float face = us.Max(u => AngDiff(u.Facing, 0));
            results[type] = (x1 - x0, y1 - y0, idle, MinPair(us), face);
        }
        var L = results[FormType.Line]; var C = results[FormType.Column]; var Wd = results[FormType.Wedge]; var B = results[FormType.Box];
        check(results.Values.All(r => r.Idle), "진형 이동: 4종 모두 도착(Idle)");
        check(L.H > 6 && L.W < 2.6f, $"횡대: 진행 방향에 수직으로 넓게 (폭 {L.H:0.0}, 깊이 {L.W:0.0})");
        check(C.W > 5 && C.H < 3.5f, $"종대: 진행 방향으로 길게 (길이 {C.W:0.0}, 폭 {C.H:0.0})");
        check(Wd.W > 2.2f && Wd.H > 3 && Wd.H > Wd.W * 0.7f, $"쐐기: 앞이 좁고 뒤가 넓은 삼각 (깊이 {Wd.W:0.0}, 폭 {Wd.H:0.0})");
        check(B.W > 2 && B.H > 2 && B.W < 6 && B.H < 6, $"방진: 정사각에 가까운 덩어리 ({B.W:0.0} × {B.H:0.0})");
        check(results.Values.All(r => r.MinD > 0.55f), $"진형 도착 후 겹침 없음 (최소 간격 {results.Values.Min(r => r.MinD):0.00})");
        check(results.Values.All(r => r.Face < 0.25f), $"도착한 유닛은 이동 방향을 바라봄 (최대 오차 {results.Values.Max(r => r.Face):0.00} rad)");

        // 쐐기 꼭짓점이 진행 방향 맨 앞이고 중앙 근처
        var ww = Bare(Open(90, 50));
        var wu = Spawn(ww, "infantry", 10, new Vector2(12, 25));
        ww.Move(1, wu, new Vector2(60, 25), new FormSpec((int)FormType.Wedge, 0, new Vector2(1, 0)));
        RunUntilIdle(ww, wu, 50);
        var apex = wu.OrderByDescending(u => u.Pos.X).First();
        float cy = wu.Average(u => u.Pos.Y);
        check(MathF.Abs(apex.Pos.Y - cy) < 1.0f, $"쐐기 꼭짓점이 진행 방향 맨 앞 · 중앙 (y 차이 {MathF.Abs(apex.Pos.Y - cy):0.00})");

        // 방향(Dir)이 다르면 진형도 회전: 아래쪽으로 가는 횡대 = 가로로 넓다
        var w2 = Bare(Open(60, 90));
        var u2 = Spawn(w2, "infantry", 12, new Vector2(30, 12));
        w2.Move(1, u2, new Vector2(30, 60), new FormSpec((int)FormType.Line, 0, new Vector2(0, 1)));
        RunUntilIdle(w2, u2, 50);
        var b2 = Bounds(u2);
        check(b2.X1 - b2.X0 > 6 && b2.Y1 - b2.Y0 < 2.6f, $"남쪽으로 가는 횡대 = 가로로 넓게 (폭 {b2.X1 - b2.X0:0.0})");
    }

    static void TestOrdering()
    {
        foreach (var armorFront in new[] { true, false })
        {
            var w = Bare(Open(90, 50));
            var tanks = Spawn(w, "tank_ft_male", 4, new Vector2(12, 25), 3f, 7);
            var inf = Spawn(w, "infantry", 8, new Vector2(12, 25), 3f, 8);
            var all = tanks.Concat(inf).ToList();
            w.Move(1, all, new Vector2(65, 25), new FormSpec((int)FormType.Column, armorFront ? 1 : 2, new Vector2(1, 0)));
            bool idle = RunUntilIdle(w, all, 70);
            float tx = tanks.Average(u => u.Pos.X), ix = inf.Average(u => u.Pos.X);
            check(idle && (armorFront ? tx > ix + 2 : ix > tx + 2), $"{(armorFront ? "기갑 앞 · 보병 뒤" : "보병 앞 · 기갑 뒤")}: 종대에서 전차 평균 x {tx:0.0}, 보병 평균 x {ix:0.0} (진행 방향 +x)");
        }
    }

    static void TestBlockedSlots()
    {
        var m = Open(90, 50);
        Rock(m, 57, 20, 61, 30);         // 목표 둘레가 바위
        var w = Bare(m);
        var us = Spawn(w, "infantry", 14, new Vector2(12, 25));
        w.Move(1, us, new Vector2(59, 25), new FormSpec((int)FormType.Box, 0, new Vector2(1, 0)));
        bool idle = RunUntilIdle(w, us, 60);
        int bad = us.Count(u => { var (tx, ty) = GameMap.TileOf(u.Pos); return !m.IsWalkable(tx, ty); });
        check(idle && bad == 0, $"슬롯이 바위 위여도 가까운 통행 칸으로 옮겨 도착 (바위 위 {bad}기, 도착 {idle})");
    }

    static void TestNarrowPassage()
    {
        var m = Open(110, 50);
        Rock(m, 50, 0, 50, 49); m.Set(50, 24, TerrainType.Grass); m.Set(50, 25, TerrainType.Grass); m.Set(50, 26, TerrainType.Grass);   // 3칸 틈
        var w = Bare(m);
        var us = Spawn(w, "infantry", 16, new Vector2(20, 25), 3f);
        w.Move(1, us, new Vector2(85, 25), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        bool idle = RunUntilIdle(w, us, 80);
        var (x0, x1, y0, y1) = Bounds(us);
        check(idle && x0 > 51, $"횡대가 3칸 틈을 지나 전원 반대편에 도착 (최소 x {x0:0.0})");
        check(y1 - y0 > 5, $"틈을 지난 뒤 다시 횡대로 펼쳐짐 (폭 {y1 - y0:0.0})");
    }

    // ---------------------------------------------------------------- 전차 좁은 길

    static GameMap WallMap(int gapY0, int gapY1, int gap2Y0 = -1, int gap2Y1 = -1)
    {
        var m = Open(90, 50);
        Rock(m, 40, 0, 40, 49);
        for (int y = gapY0; y <= gapY1; y++) m.Set(40, y, TerrainType.Grass);
        for (int y = gap2Y0; y <= gap2Y1 && gap2Y0 >= 0; y++) m.Set(40, y, TerrainType.Grass);
        return m;
    }

    static void TestTankCorridor()
    {
        var m = WallMap(20, 20);   // 1칸 틈 하나뿐
        var w = Bare(m);
        var inf = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30, 20.5f));
        var tank = w.SpawnUnit(defs.Units["tank_ft_male"], 1, new Vector2(30, 24.5f));
        w.Move(1, new[] { inf }, new Vector2(60, 20.5f));
        w.Move(1, new[] { tank }, new Vector2(60, 20.5f));
        bool idle = RunUntilIdle(w, new List<Unit> { inf, tank }, 60);
        check(inf.Pos.X > 55, $"1칸 통로: 보병은 통과 (x {inf.Pos.X:0.0})");
        check(idle && tank.Pos.X < 40 && tank.Order == UnitOrder.Idle && Vector2.Distance(tank.Pos, new Vector2(39.3f, 20.5f)) < 6,
            $"1칸 통로: 전차는 통과하지 못하고 가장 가까운 지점에서 멈춤 (x {tank.Pos.X:0.0}, y {tank.Pos.Y:0.0}, {tank.Order})");
    }

    static void TestTankDetour()
    {
        var m = WallMap(20, 20, 40, 42);   // 1칸 틈 + 멀리 3칸 틈
        var w = Bare(m);
        var tank = w.SpawnUnit(defs.Units["tank_ft_male"], 1, new Vector2(30, 20.5f));
        w.Move(1, new[] { tank }, new Vector2(60, 20.5f));
        float traveled = 0; Vector2 last = tank.Pos;
        int stuck = 0, maxStuck = 0;
        for (int t = 0; t < 60 * 90 && !(t > 30 && tank.Order == UnitOrder.Idle); t++)
        {
            w.Step();
            traveled += Vector2.Distance(tank.Pos, last);
            stuck = Vector2.Distance(tank.Pos, last) < 0.002f ? stuck + 1 : 0;
            maxStuck = Math.Max(maxStuck, stuck);
            last = tank.Pos;
        }
        check(Vector2.Distance(tank.Pos, new Vector2(60, 20.5f)) < 2.5f && traveled > 38, $"전차가 1칸 통로를 피해 3칸 틈으로 우회해 도착 (남은 {Vector2.Distance(tank.Pos, new Vector2(60, 20.5f)):0.0}, 이동 {traveled:0}칸)");
        check(maxStuck < 300, $"우회 중 5초 넘게 멈추지 않음 (최대 정지 {maxStuck / 60f:0.0}초)");
    }

    static void TestTankEscapesTightSpot()
    {
        var m = Open(60, 40);
        Rock(m, 10, 10, 30, 10);     // 벽
        Rock(m, 10, 11, 10, 14);
        var w = Bare(m);
        // 벽 바로 옆 (여유 부족 칸) 에서 시작
        var tank = w.SpawnUnit(defs.Units["tank_ft_male"], 1, new Vector2(11.5f, 11.5f));
        w.Move(1, new[] { tank }, new Vector2(45, 30));
        bool idle = RunUntilIdle(w, new List<Unit> { tank }, 40);
        check(idle && Vector2.Distance(tank.Pos, new Vector2(45, 30)) < 3, $"벽에 바짝 붙은 전차도 빠져나와 목적지에 도착 (남은 {Vector2.Distance(tank.Pos, new Vector2(45, 30)):0.0})");
    }

    static void TestMixedGroupNarrow()
    {
        // 보병 · 전차 혼성 큰 그룹이 3칸 틈을 지나 도착
        var m = WallMap(20, 22);
        var w = Bare(m);
        var inf = Spawn(w, "infantry", 14, new Vector2(28, 21), 3f, 3);
        var tanks = Spawn(w, "tank_ft_male", 5, new Vector2(26, 21), 3f, 4);
        var all = inf.Concat(tanks).ToList();
        w.Move(1, all, new Vector2(65, 21.5f));
        bool idle = RunUntilIdle(w, all, 90);
        check(idle && all.All(u => u.Pos.X > 41), $"혼성 19기(보병 14 · 전차 5)가 3칸 틈을 지나 도착 (최소 x {all.Min(u => u.Pos.X):0.0}, 도착 {idle})");
    }

    // ---------------------------------------------------------------- 리플레이 · 결정성

    static ulong RunFormScenario(IEnumerable<CommandRecord>? feed, out List<CommandRecord> log)
    {
        var m = Open(100, 60);
        Rock(m, 45, 0, 45, 59); for (int y = 28; y <= 31; y++) m.Set(45, y, TerrainType.Grass);
        var w = Bare(m, record: true);
        var a = Spawn(w, "infantry", 12, new Vector2(15, 30), 3f, 1);
        var b = Spawn(w, "tank_ft_male", 3, new Vector2(12, 32), 3f, 2);
        var c = Spawn(w, "infantry", 10, new Vector2(15, 45), 3f, 3);
        if (feed != null)
        {
            var byTick = feed.GroupBy(r => r.Tick).ToDictionary(g => g.Key, g => g.Select(r => r.Command).ToList());
            w.CommandFeed = t => byTick.TryGetValue(t, out var l) ? l : Array.Empty<GameCommand>();
        }
        else
        {
            w.Move(1, a.Concat(b), new Vector2(80, 30), new FormSpec((int)FormType.Wedge, 1, new Vector2(1, 0)));
            w.AttackMove(1, c, new Vector2(80, 20), new FormSpec((int)FormType.Box, 0, Vector2.Zero));
        }
        for (int t = 0; t < 60 * 40; t++) w.Step();
        log = w.CommandLog.ToList();
        return w.ComputeChecksum();
    }

    static void TestFormationReplay()
    {
        ulong a = RunFormScenario(null, out var log1);
        ulong b = RunFormScenario(null, out _);
        // JSON 왕복: 진형이 명령에 실려 저장 · 복원되는가
        var round = new List<CommandRecord>();
        bool formKept = true;
        foreach (var r in log1)
        {
            string json = JsonSerializer.Serialize(r.Command, CommandJson.Options);
            var back = JsonSerializer.Deserialize<GameCommand>(json, CommandJson.Options)!;
            round.Add(new CommandRecord(r.Tick, back));
            if (r.Command is MoveCommand { Form: { } f1 }) formKept &= back is MoveCommand { Form: { } f2 } && f2.Type == f1.Type && f2.Order == f1.Order;
        }
        ulong c = RunFormScenario(round, out _);
        check(a == b, $"진형 이동 40초: 같은 입력 → 같은 체크섬 ({a:x})");
        check(formKept && c == a, $"진형 명령 JSON 저장 → 복원 후 재생하면 같은 체크섬 (진형 보존 {formKept})");
        // 옛 리플레이 호환: Form 없는 이동 명령도 읽힌다
        var old = JsonSerializer.Deserialize<GameCommand>("{\"$t\":\"move\",\"Faction\":1,\"Units\":[1,2],\"Target\":[3,4]}", CommandJson.Options);
        check(old is MoveCommand { Form: null }, "Form 이 없는 옛 이동 명령도 그대로 읽힘 (자유 진형)");
    }
}
