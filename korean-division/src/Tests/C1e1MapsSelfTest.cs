using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>[C1E1-m1] MAP-A 지도 적재 + 강 도하 경로 시험: 여울 3곳으로만 건널 수 있고 그 밖의 강 구간은 통행 불가.</summary>
public static class C1e1MapsSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try { MapA(check); }
        catch (Exception e) { check(false, $"C1E1 지도 시험 중 예외: {e.GetType().Name}: {e.Message}"); }
    }

    static void MapA(Action<bool, string> check)
    {
        var map = MapFile.Load("c1e1_map_a", 1);
        check(map.Width == 800 && map.Height == 800, $"MAP-A 크기 {map.Width}x{map.Height} (800x800 기대)");
        check(map.StartTiles.Count == 2, "MAP-A 시작 위치 2개");
        var s = map.StartTiles[0]; var n = map.StartTiles[1];
        check(map.IsTerrainWalkable(s.X, s.Y) && map.IsTerrainWalkable(n.X, n.Y), "MAP-A 시작 타일 통행 가능");
        // 여울 열 범위(x) = 생성기 값: 중심 570 / 180 / 388, 폭 14
        int[] fordX = { 570 - 7, 180 - 7, 388 - 7 };
        bool Reach(bool[]? blockedFord)
        {
            int W = map.Width, H = map.Height; var seen = new bool[W * H]; var q = new Queue<int>();
            bool Ok(int x, int y)
            {
                if (!map.IsTerrainWalkable(x, y)) return false;
                for (int i = 0; i < 3; i++) if (blockedFord != null && blockedFord[i] && x >= fordX[i] && x < fordX[i] + 14 && Math.Abs(y - 380) <= 30) return false;
                return true;
            }
            q.Enqueue(s.Y * W + s.X); seen[s.Y * W + s.X] = true;
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % W, cy = c / W;
                if (cx == n.X && cy == n.Y) return true;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = cx + dx, y = cy + dy;
                    if (!map.InBounds(x, y) || seen[y * W + x] || !Ok(x, y)) continue;
                    seen[y * W + x] = true; q.Enqueue(y * W + x);
                }
            }
            return false;
        }
        check(Reach(null), "MAP-A 남 시작 -> 북 시작 도달 (여울 통해)");
        for (int i = 0; i < 3; i++)
        {
            var b = new bool[3]; b[i] = true;
            check(Reach(b), $"MAP-A 여울 {i} 하나만 막아도 나머지로 도달");
        }
        check(!Reach(new[] { true, true, true }), "MAP-A 여울 3곳을 모두 막으면 도달 불가 (그 밖의 강 구간 통행 불가)");
        // 여울 3곳 각각 강 전폭(남둑~북둑) 통행 가능
        for (int i = 0; i < 3; i++)
        {
            int x = fordX[i] + 7, y0 = -1, y1 = -1;
            for (int y = 330; y < 430; y++) if (map.Get(x, y) is TerrainType.Water) { if (y0 < 0) y0 = y; y1 = y; }
            check(y0 < 0, $"MAP-A 여울 {i} 열(x={x}) 에 water 없음");
        }
        // 여울 밖 강 열: 어떤 열이든 water 로 막혀 있음
        int blocked = 0, total = 0;
        for (int x = 0; x < map.Width; x++)
        {
            if (fordX.Any(f => x >= f && x < f + 14)) continue;
            total++; bool water = false;
            for (int y = 350; y < 410; y++) if (map.Get(x, y) is TerrainType.Water) { water = true; break; }
            if (water) blocked++;
        }
        check(blocked == total, $"MAP-A 여울 밖 모든 열이 water 로 막힘 ({blocked}/{total})");
    }
}
