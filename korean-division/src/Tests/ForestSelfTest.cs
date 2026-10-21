using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 숲 전투 (forest, 2026-10-19 사용자 결정) 검증: 숲 시야 깊이 값이 적용되는지 · 숲 방어 이점이 남는지 · 적이 안 보이는 동안 정찰조가 숲 속으로 들어가는지 (끄면 안 들어감).
/// KD_SELFTEST_ONLY=forest 로 단독 실행.
/// </summary>
public static class ForestSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[숲 전투]");
        try
        {
            TestOcclusion();
            TestDefenderAdvantage();
            TestProbe();
        }
        catch (Exception e) { check(false, $"숲 전투 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 160)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        for (int f = 1; f <= 2; f++)
        {
            var b = w.Buildings.First(x => x.Owner == f);
            var k = w.SpawnUnit(defs.Units["supply_wagon"], f, b.Pos + new Vector2(f == 1 ? 3 : -3, 0));
            w.Hold(f, new[] { k });
        }
        w.Fog.Enabled = false;
        return w;
    }

    static void Forest(World w, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) w.Map.Set(x, y, TerrainType.Forest);
    }

    static void TestOcclusion()
    {
        var t = defs.Terrain;
        check(t.ForestSeeDepth == 3, $"숲 시야 깊이 {t.ForestSeeDepth} (balance.json terrain.forestSeeDepth = 3, 옛 값 2)");
        var w = Bare();
        Forest(w, 60, 60, 80, 80);   // 21x21: 가운데 깊이 11
        int d3 = 60 + 2, d4 = 60 + 3;   // x=62 → 깊이 3, x=63 → 깊이 4 (왼쪽 가장자리 x=60 이 깊이 1)
        check(w.Map.ForestDepth(d3, 70) == 3 && !w.Map.IsDeepForest(d3, 70) && w.Map.ForestDepth(d4, 70) == 4 && w.Map.IsDeepForest(d4, 70),
            "깊이 3 칸은 보임 · 사격 가능, 깊이 4 칸부터 시야 · 사선을 막는 깊은 숲");
        var from = new Vector2(50.5f, 70.5f);
        bool line3 = w.Map.HasFiringLine(from, new Vector2(d3 + 0.5f, 70.5f), 0, 0);
        bool line5 = w.Map.HasFiringLine(from, new Vector2(65.5f, 70.5f), 0, 0);
        check(line3 && !line5, $"평지에서 숲 깊이 3 칸 사선 열림 ({line3}), 깊이 6 칸 사선 막힘 ({!line5})");
    }

    static void TestDefenderAdvantage()
    {
        var w = Bare();
        Forest(w, 60, 60, 80, 80);
        var forest = w.Map.TerrainCover(new Vector2(61.5f, 70.5f));
        var open = w.Map.TerrainCover(new Vector2(40.5f, 70.5f));
        check(forest.Hit <= 0.85f && forest.Hit >= 0.7f && forest.Hit < open.Hit && forest.Suppress < open.Suppress,
            $"숲 방어 이점 유지: 맞을 확률 ×{forest.Hit:0.00} (평지 ×{open.Hit:0.00}), 받는 제압 ×{forest.Suppress:0.00}");
    }

    static (World W, Company C, List<Unit> Scouts) Setup()
    {
        var w = Bare();
        Forest(w, 66, 66, 100, 94);
        var body = new List<Unit>();
        for (int i = 0; i < 24; i++) body.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40 + (i % 12) * 1f, 80 + (i / 12) * 1f)));
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(45, 78));
        var sc = new List<Unit>();
        for (int i = 0; i < 2; i++) sc.Add(w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(44 + i * 2, 76)));
        var all = new List<Unit>(body) { off };
        all.AddRange(sc);
        w.FormCompany(1, all);
        w.Step();
        off.Company!.Facing = new Vector2(1, 0);
        return (w, off.Company!, sc);
    }

    static void TestProbe()
    {
        using var nd = TestDefs.NoCommandDelay();
        // 켬: 적이 없고 앞에 숲이 있으면 중대가 서 있어도 (교착) 정찰조가 숲 속으로 들어간다
        var (w, c, sc) = Setup();
        int deepTicks = 0;
        float maxX = 0;
        for (int t = 0; t < 60 * 90; t++)
        {
            w.Step();
            foreach (var s in sc) { maxX = MathF.Max(maxX, s.Pos.X); if (w.Map.ForestDepth((int)s.Pos.X, (int)s.Pos.Y) > 0) deepTicks++; }
        }
        float cx = c.Members.Average(m => m.Pos.X);
        check(deepTicks > 60 && maxX >= 66f && maxX - cx >= 18f, $"숲 탐색 켬: 정찰조가 숲 속으로 (숲 안 {deepTicks} 틱, 최전방 x {maxX:0.0}, 중대 x {cx:0.0})");
        // 끔: 같은 상황에서 중대가 서 있으면 나가지 않는다
        defs.ScoutAi.ForestProbe = false;
        try
        {
            var (w2, c2, sc2) = Setup();
            for (int t = 0; t < 60 * 90; t++) w2.Step();
            check(sc2.All(s => s.ScoutState == ScoutState.With && s.Pos.X < 60f), $"숲 탐색 끔: 서 있는 중대의 정찰조는 나가지 않는다 (x {string.Join(", ", sc2.Select(s => s.Pos.X.ToString("0.0")))})");
        }
        finally { defs.ScoutAi.ForestProbe = true; }
        // 피격 엄폐 우선 (N22): 탐색 중 노려지면 곧바로 엄폐
        var (w3, c3, sc3) = Setup();
        for (int t = 0; t < 60 * 40; t++) w3.Step();
        var s0 = sc3[0];
        s0.LastFiredAtTick = w3.Tick;
        for (int t = 0; t < 30; t++) w3.Step();
        check(s0.ScoutState == ScoutState.Cover, $"탐색 중 노려지면 엄폐 상태 (N22 규칙 유지) — {s0.ScoutState}");
    }
}
