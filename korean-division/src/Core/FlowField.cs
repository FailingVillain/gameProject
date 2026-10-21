using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 목표 지점 하나에 대한 흐름장(flow field). 같은 명령을 받은 유닛들이 공유한다.
/// 비용 계산(FlowGrid)은 <b>필요한 만큼만</b> 한다 — 유닛이 묻는 칸이 확정될 때까지만 Dijkstra 를 진행하므로
/// 가까운 이동 · 집결 · 추격은 맵 크기와 무관하게 싸다. 확정된 값은 맵 전체를 계산한 것과 똑같다.
/// World.FieldTo 는 같은 목표 칸의 FlowGrid 를 캐시해 여러 그룹이 재사용한다.
/// </summary>
public sealed class FlowField
{
    public readonly Vector2 Goal;
    readonly FlowGrid? grid;

    /// <summary>단독 흐름장 (캐시 없이). 게임 내부는 World.FieldTo 를 쓴다.</summary>
    public FlowField(GameMap map, Vector2 target, float goalMargin = 0.35f)
    {
        var (gx, gy, goal) = ResolveGoal(map, target, goalMargin);
        Goal = goal;
        grid = new FlowGrid(map, gx, gy);
    }

    internal FlowField(Vector2 goal, FlowGrid grid)
    {
        Goal = goal;
        this.grid = grid;
    }

    /// <summary>목표 표시만 하는 가벼운 껍데기 — 게임 안의 이동은 계층형 길찾기(Nav)가 하므로 격자를 만들지 않는다.</summary>
    internal FlowField(Vector2 goal) => Goal = goal;

    /// <summary>목표까지의 비용 (도달 불가면 무한대). 전체 Dijkstra 기준값 — 테스트에서 계층형 경로와 비교한다.</summary>
    public float CostAt(int x, int y) => grid == null ? float.PositiveInfinity : grid.CostAt(x, y);

    /// <summary>목표 칸과 실제 도착 지점. 막힌 칸이면 가장 가까운 열린 칸의 중심.</summary>
    public static (int X, int Y, Vector2 Goal) ResolveGoal(GameMap map, Vector2 target, float goalMargin = 0.35f)
    {
        var (tx, ty) = GameMap.TileOf(target);
        tx = Math.Clamp(tx, 0, map.Width - 1);
        ty = Math.Clamp(ty, 0, map.Height - 1);
        if (map.IsWalkable(tx, ty)) return (tx, ty, map.ClampIntoTile(target, tx, ty, goalMargin));
        (tx, ty) = map.NearestWalkable(tx, ty);
        return (tx, ty, new Vector2(tx + 0.5f, ty + 0.5f));
    }

    public bool IsReachable(int x, int y) => grid != null && grid.IsReachable(x, y);

    /// <summary>타일 (x,y) 에서 목표 쪽으로 가장 비용이 낮은 이웃 타일의 중심.</summary>
    public bool TryGetWaypoint(int x, int y, out Vector2 waypoint) { waypoint = default; return grid != null && grid.TryGetWaypoint(x, y, out waypoint); }

    /// <summary>지금까지 확정된 칸 수 (테스트 · 성능 확인용).</summary>
    public int SettledCount => grid?.Settled ?? 0;
}

/// <summary>목표 칸까지의 비용 격자. 지연 Dijkstra — 질의한 칸이 확정될 때까지만 확장한다.</summary>
internal sealed class FlowGrid
{
    static readonly (int Dx, int Dy, float Cost)[] Dirs =
    {
        (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
        (1, 1, 1.41421356f), (1, -1, 1.41421356f), (-1, 1, 1.41421356f), (-1, -1, 1.41421356f),
    };

    readonly GameMap map;
    readonly float[] cost;
    readonly bool[] done;
    readonly PriorityQueue<int, float> open = new();
    bool exhausted;
    public int Settled { get; private set; }

    public FlowGrid(GameMap map, int goalX, int goalY)
    {
        this.map = map;
        cost = new float[map.Width * map.Height];
        done = new bool[cost.Length];
        Array.Fill(cost, float.PositiveInfinity);
        int g = goalY * map.Width + goalX;
        cost[g] = 0;
        open.Enqueue(g, 0);
    }

    /// <summary>대각선 이동 시 모서리를 깎지 않도록 양옆 타일도 열려 있어야 한다.</summary>
    bool CanStep(int x, int y, int dx, int dy) =>
        map.IsWalkable(x + dx, y + dy) &&
        (dx == 0 || dy == 0 || (map.IsWalkable(x + dx, y) && map.IsWalkable(x, y + dy)));

    /// <summary>index 칸이 확정될 때까지 (또는 도달 가능한 칸을 다 볼 때까지) 확장.</summary>
    void SettleUntil(int index)
    {
        int w = map.Width;
        while (!done[index] && !exhausted)
        {
            if (!open.TryDequeue(out int i, out float c))
            {
                exhausted = true;
                break;
            }
            if (done[i] || c > cost[i]) continue;
            done[i] = true;
            Settled++;
            int x = i % w, y = i / w;
            foreach (var (dx, dy, dc) in Dirs)
            {
                if (!CanStep(x, y, dx, dy)) continue;
                int ni = (y + dy) * w + x + dx;
                float nc = c + dc;
                if (!done[ni] && nc < cost[ni])
                {
                    cost[ni] = nc;
                    open.Enqueue(ni, nc);
                }
            }
        }
    }

    public bool IsReachable(int x, int y)
    {
        if (!map.InBounds(x, y)) return false;
        int i = y * map.Width + x;
        SettleUntil(i);
        return done[i];
    }

    public float CostAt(int x, int y)
    {
        if (!IsReachable(x, y)) return float.PositiveInfinity;
        return cost[y * map.Width + x];
    }

    public bool TryGetWaypoint(int x, int y, out Vector2 waypoint)
    {
        waypoint = default;
        if (!IsReachable(x, y)) return false;
        // Dijkstra 성질: 이 칸보다 비용이 낮은 이웃은 이미 확정돼 있다 → 확정된 이웃만 보면 된다
        float best = cost[y * map.Width + x];
        int bx = -1, by = -1;
        foreach (var (dx, dy, _) in Dirs)
        {
            if (!CanStep(x, y, dx, dy)) continue;
            int ni = (y + dy) * map.Width + x + dx;
            if (done[ni] && cost[ni] < best) (best, bx, by) = (cost[ni], x + dx, y + dy);
        }
        if (bx < 0) return false;
        waypoint = new Vector2(bx + 0.5f, by + 0.5f);
        return true;
    }
}
