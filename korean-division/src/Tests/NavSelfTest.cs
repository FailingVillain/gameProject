using System.Diagnostics;
using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>계층형 길찾기 · 도시 통행 · 충돌 · 추격 메모리 검증 (창 없이). SelfTest 가 호출한다.</summary>
public static class NavSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        using var noDelay = TestDefs.NoCommandDelay();   // 지휘 계통 지연 도입 전 이동 · 시간 기대값 (지연 자체는 SquadSelfTest)
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[길찾기 · 도시 · 성능]");
        TestPathQuality();
        TestUnreachable();
        TestReroute();
        TestIncrementalGraph();
        TestBigGroup();
        TestWalkOnCity(opt);
        TestSeparate();
        TestChaseMemory();
        TestSpikes();
        TestDeterminism();
    }

    // ---------------------------------------------------------------- 지도 · 도우미

    /// <summary>사행 미로 + 호수 + 흩어진 바위 (결정적). 벽은 12칸마다, 구멍은 번갈아 양끝.</summary>
    static GameMap MazeMap(int size, int seed)
    {
        var m = new GameMap(size, size, seed);
        var rng = new Random(seed);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            m.Set(x, y, TerrainType.Grass);
        for (int wall = 1, k = 0; wall * 14 < size - 4; wall++, k++)
        {
            int y = wall * 14;
            int gap = k % 2 == 0 ? size - 8 : 4;
            for (int x = 0; x < size; x++)
                if (Math.Abs(x - gap) > 2) m.Set(x, y, TerrainType.Rock);
        }
        for (int i = 0; i < size * size / 30; i++) m.Set(rng.Next(size), rng.Next(size), TerrainType.Rock);
        // 호수
        int cx = size / 2, cy = size / 3;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < 90) m.Set(x, y, TerrainType.Water);
        return m;
    }

    /// <summary>유지비로 유닛이 굶어 죽지 않게 자원을 넉넉히 (이 테스트는 이동 · 충돌만 본다).</summary>
    static void Rich(World w)
    {
    }

    static World Bare(GameMap map)
    {
        var w = new World(map, defs, 2) { RecordCommands = false };
        Rich(w);
        // 사령부가 없으면 세력이 패배해 유닛이 사라진다 — 구석에 대도시(통행 가능)를 하나씩
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

    static (int X, int Y) RandomOpen(GameMap m, Random rng)
    {
        for (int i = 0; i < 10000; i++)
        {
            int x = rng.Next(m.Width), y = rng.Next(m.Height);
            if (m.IsWalkable(x, y)) return (x, y);
        }
        return (1, 1);
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    // ---------------------------------------------------------------- 경로 품질: 전체 Dijkstra 와 비교

    static void TestPathQuality()
    {
        var map = MazeMap(192, 5);
        var rng = new Random(11);
        int tested = 0, within = 0, arrived = 0;
        double worstRatio = 0;
        for (int attempt = 0; attempt < 400 && tested < 14; attempt++)
        {
            var (sx, sy) = RandomOpen(map, rng);
            var (gx, gy) = RandomOpen(map, rng);
            if (Math.Abs(sx - gx) + Math.Abs(sy - gy) < 60) continue;
            var reference = new FlowField(map, new Vector2(gx + 0.5f, gy + 0.5f));
            float refCost = reference.CostAt(sx, sy);
            if (float.IsInfinity(refCost)) continue;
            var w = Bare(map);
            var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(sx + 0.5f, sy + 0.5f));
            w.Move(1, new[] { u }, new Vector2(gx + 0.5f, gy + 0.5f));
            float traveled = 0;
            Vector2 last = u.Pos;
            int ticks = 0;
            int maxTicks = (int)(refCost * 2.2f / u.Def.Speed * 60) + 600;
            while ((u.Order != UnitOrder.Idle || ticks < 20) && ticks++ < maxTicks)
            {
                w.Step();
                traveled += Vector2.Distance(u.Pos, last);
                last = u.Pos;
            }
            tested++;
            bool ok = u.Order == UnitOrder.Idle && Vector2.Distance(u.Pos, new Vector2(gx + 0.5f, gy + 0.5f)) < 1.5f;
            if (ok) arrived++;
            double ratio = traveled / Math.Max(1, refCost);
            worstRatio = Math.Max(worstRatio, ratio);
            if (ok && traveled <= refCost * 1.15f + 3) within++;
        }
        check(tested >= 10 && arrived == tested, $"계층형 길찾기: 미로 · 호수 맵에서 도달 가능한 목표 {tested}개 모두 도착 ({arrived}/{tested})");
        check(within == tested, $"계층형 경로 길이가 전체 Dijkstra 기준의 115% 이내 ({within}/{tested}, 최악 {worstRatio * 100:0}%)");
    }

    // ---------------------------------------------------------------- 도달 불가 목표 → 가장 가까운 도달 가능 지점

    static void TestUnreachable()
    {
        var map = new GameMap(128, 128, 1);
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++) map.Set(x, y, TerrainType.Grass);
        // 80..100 구역을 바위 벽으로 둘러싼 방 (안쪽은 걸을 수 있지만 닿을 수 없음)
        for (int i = 70; i <= 100; i++)
        {
            map.Set(i, 70, TerrainType.Rock); map.Set(i, 100, TerrainType.Rock);
            map.Set(70, i, TerrainType.Rock); map.Set(100, i, TerrainType.Rock);
        }
        var w = Bare(map);
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.5f, 20.5f));
        var goal = new Vector2(85.5f, 85.5f);
        w.Move(1, new[] { u }, goal);
        Steps(w, 60 * 70);
        // 닿을 수 있는 가장 가까운 지점: 방 바깥쪽 모서리 (69,69) 근처
        float d = Vector2.Distance(u.Pos, goal);
        float best = float.MaxValue;
        var reach = new FlowField(map, new Vector2(20.5f, 20.5f));
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
            if (reach.IsReachable(x, y)) best = MathF.Min(best, Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), goal));
        check(u.Order == UnitOrder.Idle && d <= best + 3f && d < Vector2.Distance(new Vector2(20.5f, 20.5f), goal) - 30,
            $"도달 불가 목표 → 가장 가까운 도달 가능 지점까지 가서 멈춤 (남은 거리 {d:0.0}, 최선 {best:0.0})");
        // 섬 (호수 한가운데)도 마찬가지
        var w2 = Bare(MazeMap(128, 9));
        var u2 = w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(10.5f, 3.5f));
        w2.Move(1, new[] { u2 }, new Vector2(64.5f, 42.5f));   // 호수 중심 (물)
        Steps(w2, 60 * 90);
        check(u2.Order == UnitOrder.Idle && w2.Map.IsWalkable((int)u2.Pos.X, (int)u2.Pos.Y), "물 한가운데 목표 → 가까운 땅으로 이동 후 정지");
    }

    // ---------------------------------------------------------------- 경로 도중에 건물 → 우회

    static void TestReroute()
    {
        var map = new GameMap(128, 128, 1);
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++) map.Set(x, y, TerrainType.Grass);
        for (int y = 0; y < 128; y++)
            if (!(y >= 38 && y <= 41) && !(y >= 108 && y <= 111)) map.Set(64, y, TerrainType.Rock);   // 가까운 구멍(38..41) + 먼 구멍(108..111)
        var w = Bare(map);
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(10.5f, 40.5f));
        var goal = new Vector2(118.5f, 40.5f);
        w.Move(1, new[] { u }, goal);
        Steps(w, 60 * 10);
        bool before = u.Pos.X > 25 && u.Pos.X < 62;
        // 가까운 구멍을 건물 둘(2×2)로 막는다
        var depot = TestDefs.Wall(2);
        var b1 = w.PlaceBuilding(depot, 1, 63, 38);
        var b2 = w.PlaceBuilding(depot, 1, 63, 40);
        check(before && b1 != null && b2 != null, "경로 도중 (x≈30..60) 가까운 구멍에 건물 두 채를 놓음");
        bool inside = false;
        for (int t = 0; t < 60 * 120 && u.Order != UnitOrder.Idle; t++)
        {
            w.Step();
            inside |= !w.Map.IsWalkable((int)u.Pos.X, (int)u.Pos.Y);
        }
        check(u.Order == UnitOrder.Idle && Vector2.Distance(u.Pos, goal) < 1.5f && !inside, $"막힌 길을 피해 먼 구멍으로 우회해서 도착 (남은 {Vector2.Distance(u.Pos, goal):0.0})");
        // 건물이 파괴되면 다시 열린다
        b1!.Hp = 0; b2!.Hp = 0;
        Steps(w, 5);
        var w2 = w.Nav.RouteNow(10, 40, 118, 40);
        check(!w2.Failed && w2.Segs.Length >= 6 && w2.Segs.Length <= 8, $"건물 파괴 후 가까운 구멍이 다시 열림 (구역 경로 출구 {w2.Segs.Length}개)");
    }

    // ---------------------------------------------------------------- 증분 갱신 == 처음부터 구축

    static void TestIncrementalGraph()
    {
        var map = MazeMap(160, 3);
        var w = Bare(map);
        ulong s0 = w.Nav.Signature();
        var rng = new Random(2);
        var placed = new List<Building>();
        for (int i = 0; i < 30; i++)
        {
            var (x, y) = RandomOpen(map, rng);
            var b = w.PlaceBuilding(TestDefs.Wall(2), 1, x, y);
            if (b != null) placed.Add(b);
        }
        w.Step();
        ulong inc = w.Nav.Signature();
        ulong fresh = new Nav(map).Signature();
        check(inc == fresh && inc != s0, $"건물 {placed.Count}채 배치 후 증분 갱신한 그래프 == 처음부터 구축한 그래프");
        foreach (var b in placed.Take(placed.Count / 2)) b.Hp = 0;
        Steps(w, 3);
        check(w.Nav.Signature() == new Nav(map).Signature(), "건물 일부 파괴 후에도 증분 갱신 == 처음부터 구축");
    }

    // ---------------------------------------------------------------- 큰 그룹

    static void TestBigGroup()
    {
        var map = MazeMap(128, 7);
        var w = Bare(map);
        var units = new List<Unit>();
        var start = new Vector2(20.5f, 6.5f);
        for (int i = 0; i < 100; i++)
        {
            float r = MathF.Sqrt(i * 0.7f / MathF.PI), a = i * 2.3999632f;
            units.Add(w.SpawnUnit(defs.Units[i % 5 == 0 ? "heavy_at_rifleman" : "infantry"], 1, start + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r));
        }
        // 도달 가능한 곳 중 출발점에서 가장 먼 칸 (사행 경로를 끝까지)
        var from = new FlowField(map, start);
        int bx = 0, by = 0; float bc = 0;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            if (map.IsWalkable(x, y) && from.IsReachable(x, y) && from.CostAt(x, y) > bc && from.CostAt(x, y) < 700) { bc = from.CostAt(x, y); bx = x; by = y; }
        var goal = new Vector2(bx + 0.5f, by + 0.5f);
        w.Move(1, units, goal);
        w.Step();   // 명령은 다음 틱에 실행
        double worst = 0;
        int ticks = 0;
        while (units.Any(u => u.Order == UnitOrder.Move) && ticks++ < 60 * 600)
        {
            long t0 = Stopwatch.GetTimestamp();
            w.Step();
            worst = Math.Max(worst, Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        int near = units.Count(u => Vector2.Distance(u.Pos, goal) < 16);
        int idle = units.Count(u => u.Order == UnitOrder.Idle);
        check(idle == 100 && near == 100, $"유닛 100기 큰 그룹이 미로를 지나 도착 ({near}/100이 목표 반경 16 안, 경로 {bc:0}칸, {ticks / 60f:0}초, 최악 틱 {worst:0.0}ms)");
    }

    // ---------------------------------------------------------------- 도시 위 통행 · 충돌 2배 · 건물 배치 금지

    static void TestWalkOnCity(GameOptions opt)
    {
        var w = World.CreateStandard(GameMap.Generate(opt.MapWidth, opt.MapHeight, opt.Seed), defs);
        w.Fog.Enabled = false;
        var city = w.Buildings.First(b => b.Def.City && b.Def.Headquarters);
        var small = w.Buildings.FirstOrDefault(b => b.Def.City && !b.Def.Headquarters);
        bool walk = true;
        for (int y = city.TileY; y < city.TileY + city.Size; y++)
        for (int x = city.TileX; x < city.TileX + city.Size; x++) walk &= w.Map.IsWalkable(x, y) && !w.Map.IsBuildable(x, y);
        check(walk, "대도시 발자국은 통행 가능하지만 건물 배치는 불가 (예약)");
        check(!w.Map.CanPlace(city.TileX, city.TileY, 2) && !w.Map.CanPlace(city.TileX + 1, city.TileY + 1, 2),
            "도시 발자국에는 다른 건물을 놓을 수 없음 (CanPlace)");
        if (small != null) check(w.Map.IsWalkable(small.TileX, small.TileY) && !w.Map.IsBuildable(small.TileX, small.TileY), "소도시도 통행 가능 · 배치 불가");
        var village = w.Buildings.FirstOrDefault(b => !b.Def.City && b.Def.Capturable);
        if (village != null) check(!w.Map.IsWalkable(village.TileX, village.TileY), "마을은 여전히 통행 불가");

        // 통과: 도시 한쪽에서 반대쪽으로 직선 이동 — 중간에 발자국 안을 지난다
        var from = new Vector2(city.TileX - 3, city.TileY + city.Size / 2f);
        var to = new Vector2(city.TileX + city.Size + 3, city.TileY + city.Size / 2f);
        var u = w.SpawnUnit(defs.Units["infantry"], 1, from);
        w.Move(1, new[] { u }, to);
        bool crossed = false;
        for (int t = 0; t < 60 * 20 && u.Order != UnitOrder.Idle || t < 20; t++)
        {
            w.Step();
            crossed |= u.Pos.X > city.TileX + 0.5f && u.Pos.X < city.TileX + city.Size - 0.5f && Math.Abs(u.Pos.Y - city.Pos.Y) < city.Size / 2f;
        }
        check(crossed && Vector2.Distance(u.Pos, to) < 1.5f, $"유닛이 도시 위를 가로질러 지나감 (남은 {Vector2.Distance(u.Pos, to):0.0})");

        // 흐름: 도시 뒤편 목표 경로가 도시를 돌아가지 않고 지나감 (직선 거리에 가깝다)
        var u2 = w.SpawnUnit(defs.Units["infantry"], 1, from + new Vector2(0, 0.7f));
        float len = 0; Vector2 last = u2.Pos;
        w.Move(1, new[] { u2 }, to + new Vector2(0, 0.7f));
        for (int t = 0; t < 60 * 20 && (u2.Order != UnitOrder.Idle || t < 20); t++) { w.Step(); len += Vector2.Distance(u2.Pos, last); last = u2.Pos; }
        check(len < Vector2.Distance(from, to) * 1.15f, $"도시 반대편 이동 거리 {len:0.0} ≈ 직선 {Vector2.Distance(from, to):0.0} (우회 없음)");
    }

    static void TestSeparate()
    {
        var w = World.CreateStandard(GameMap.Generate(100, 100, 42), defs);
        w.Fog.Enabled = false;
        var city = w.Buildings.First(b => b.Def.City && b.Def.Headquarters);
        var inf = defs.Units["infantry"];
        float gap = inf.Radius * 2 + 0.1f;     // 밖에서는 겹치지 않는 간격
        var a = w.SpawnUnit(inf, 1, city.Pos + new Vector2(-gap / 2, 0));
        var b = w.SpawnUnit(inf, 1, city.Pos + new Vector2(gap / 2, 0));
        a.Weapon = null; b.Weapon = null;
        Steps(w, 3);
        float onCity = Vector2.Distance(a.Pos, b.Pos);
        var out1 = new Vector2(w.Map.Width - 8, 8);
        var c = w.SpawnUnit(inf, 1, out1 + new Vector2(-gap / 2, 0));
        var d = w.SpawnUnit(inf, 1, out1 + new Vector2(gap / 2, 0));
        c.Weapon = null; d.Weapon = null;
        Steps(w, 3);
        float off = Vector2.Distance(c.Pos, d.Pos);
        check(onCity > gap + 0.2f && MathF.Abs(off - gap) < 0.01f, $"도시 위 충돌 반경 2배: 도시 위 간격 {gap:0.00} → {onCity:0.00}, 밖은 그대로 {off:0.00}");

        // 겹침 해소의 대칭 · 결정성: 전차가 보병을 밀어낸다 (작은 유닛이 좁게 스캔해도 쌍을 놓치지 않음)
        var tank = w.SpawnUnit(defs.Units["tank_ft_male"], 1, out1 + new Vector2(-20, 0));
        var foot = w.SpawnUnit(inf, 1, out1 + new Vector2(-20 + tank.Radius + inf.Radius - 0.2f, 0));
        tank.Weapon = null; foot.Weapon = null;
        float before = Vector2.Distance(tank.Pos, foot.Pos);
        Steps(w, 4);
        check(Vector2.Distance(tank.Pos, foot.Pos) > before + 0.05f, "전차와 보병이 겹치면 서로 밀려남");

        // 밀집 덩어리가 겹침 없이 풀리는지
        var w2 = Bare(MazeMap(96, 1));
        var list = new List<Unit>();
        for (int i = 0; i < 300; i++) list.Add(w2.SpawnUnit(defs.Units[i % 6 == 0 ? "tank_ft_male" : "infantry"], 1, new Vector2(48, 48) + new Vector2(i % 3 * 0.05f, i / 3 % 5 * 0.04f) + Vector2.One * 0.01f * (i % 7)));
        foreach (var u in list) u.Weapon = null;
        Steps(w2, 200);
        int overlaps = 0;
        for (int i = 0; i < list.Count; i++)
        for (int j = i + 1; j < list.Count; j++)
            if (Vector2.Distance(list[i].Pos, list[j].Pos) < (list[i].Radius + list[j].Radius) * 0.7f) overlaps++;
        check(overlaps < 5, $"300기 밀집 덩어리가 풀린 뒤 심한 겹침 {overlaps}쌍");
    }

    // ---------------------------------------------------------------- 추격 메모리 상한

    static void TestChaseMemory()
    {
        var map = GameMap.Generate(200, 200, 8, 4);
        var w = World.CreateStandard(map, defs);
        w.Fog.Enabled = false;
        w.RecordCommands = false;
        Rich(w);
        var center = new Vector2(100, 100);
        var rng = new Random(3);
        for (int f = 1; f <= 4; f++)
        {
            var list = new List<Unit>();
            for (int i = 0; i < 600; i++)
            {
                float r = MathF.Sqrt(i * 0.7f / MathF.PI), a = i * 2.3999632f;
                list.Add(w.SpawnUnit(defs.Units[i % 5 == 4 ? "tank_ft_male" : "infantry"], f, center + new Vector2((f % 2 - 0.5f) * 12, (f / 3 - 0.5f) * 12) + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r));
            }
            w.AttackMove(f, list, center + new Vector2(rng.Next(-30, 30), rng.Next(-30, 30)));
        }
        GC.Collect();
        long mem0 = GC.GetTotalMemory(true);
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        int ticks = 60 * 25;
        for (int i = 0; i < ticks; i++) w.Step();
        long allocPerTick = (GC.GetAllocatedBytesForCurrentThread() - alloc0) / ticks;
        long mem1 = GC.GetTotalMemory(true);
        var st = w.NavStats;
        check(st.Routes <= 768 && st.Fields <= 6000 + 64, $"추격 · 이동 후 길찾기 캐시가 상한 이내 (경로 {st.Routes} ≤ 768, 지역 흐름장 {st.Fields} ≤ 6000)");
        check(mem1 - mem0 < 150L * 1024 * 1024 && allocPerTick < 2_000_000, $"밀집 교전 2400기: 힙 증가 {(mem1 - mem0) / 1048576.0:0.0}MB, 할당 {allocPerTick / 1024}KB/틱 (유닛 {w.Units.Count}기 생존)");
    }

    // ---------------------------------------------------------------- 스파이크: 여러 분대에 동시에 다른 목표

    static void TestSpikes()
    {
        var map = GameMap.Generate(280, 280, 5, 16);
        var w = World.CreateStandard(map, defs);
        w.Fog.Enabled = false;
        w.RecordCommands = false;
        Rich(w);
        var rng = new Random(9);
        var squads = new List<List<Unit>>();
        for (int s = 0; s < 50; s++)
        {
            var (x, y) = RandomOpen(map, rng);
            var list = new List<Unit>();
            for (int i = 0; i < 30; i++) list.Add(w.SpawnUnit(defs.Units["infantry"], s % 16 + 1, new Vector2(x + 0.5f + i % 6 * 0.6f, y + 0.5f + i / 6 * 0.6f)));
            squads.Add(list);
        }
        Steps(w, 30);
        double worst = 0, total = 0;
        for (int round = 0; round < 3; round++)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                var (gx, gy) = RandomOpen(map, rng);
                w.Move(s % 16 + 1, squads[s], new Vector2(gx + 0.5f, gy + 0.5f));
            }
            for (int t = 0; t < 600; t++)
            {
                long t0 = Stopwatch.GetTimestamp();
                w.Step();
                double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                worst = Math.Max(worst, ms);
                total += ms;
            }
        }
        Console.WriteLine($"  [perf] 분대 50개(1500기) 동시 재명령 ×3: 틱 평균 {total / 1800:0.00}ms, 최악 {worst:0.0}ms");
        check(worst < 100, $"50개 분대가 같은 틱에 서로 다른 목표를 받아도 최악 틱 < 100ms ({worst:0.0}ms)");
    }

    // ---------------------------------------------------------------- 결정성: 모든 기능을 켠 같은 시드 두 번

    static ulong RunScenario(int seed)
    {
        var map = GameMap.Generate(144, 144, seed, 4);
        var w = World.CreateStandard(map, defs);
        w.Fog.Enabled = true;
        for (int f = 1; f <= 4; f++) w.Controllers.Add((f, AiRegistry.Create("standard")!));
        var rng = new Random(seed);
        var groups = new List<(int F, List<Unit> L)>();
        for (int f = 1; f <= 4; f++)
        {
            var hq = w.Buildings.First(b => b.Owner == f && b.Def.Headquarters).Pos;
            var list = new List<Unit>();
            for (int i = 0; i < 60; i++) list.Add(w.SpawnUnit(defs.Units[i % 4 == 3 ? "tank_ft_male" : "infantry"], f, hq + new Vector2(6 + i % 10 * 0.8f, i / 10 * 0.8f)));
            groups.Add((f, list));
            w.AttackMove(f, list, new Vector2(72, 72));
        }
        for (int t = 0; t < 60 * 60; t++)
        {
            if (t % 600 == 300)
                foreach (var (f, l) in groups)
                {
                    l.RemoveAll(u => !u.Alive);
                    if (l.Count > 0) w.Move(f, l, new Vector2(rng.Next(4, 140), rng.Next(4, 140)));   // 일부는 도달 불가(물/바위) → 근사 경로
                }
            w.Step();
        }
        return w.ComputeChecksum();
    }

    static void TestDeterminism()
    {
        ulong a = RunScenario(21), b = RunScenario(21), c = RunScenario(22);
        check(a == b && a != c, $"계층 길찾기 · 직선 · 큰 그룹 · 추격 · 도시 통행을 모두 켠 4인 60초: 같은 시드 → 같은 체크섬 ({a:x}), 다른 시드는 다름");
    }
}
