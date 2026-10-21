using System.Diagnostics;
using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>구역(섹터) 경계의 출입구. 한 경계 양쪽에 짝(Peer)으로 하나씩 만들어진다.</summary>
internal sealed class NavPortal
{
    public int Index;               // 전역 번호 (A* 배열 인덱스)
    public NavSector S = null!;     // 이 출입구가 속한 구역
    public int X, Y;                // 이 구역 안의 타일
    public NavPortal? Peer;         // 맞은편 구역의 짝 (바로 옆 타일)
    public int Slot;                // S.All 에서의 위치
}

internal sealed class NavSector
{
    public int Id, X0, Y0, W, H;
    public int Version;             // 이 구역 타일의 통행 정보가 바뀔 때마다 증가 (지역 흐름장 무효화)
    public readonly List<NavPortal>[] BySide = { new(), new(), new(), new() };   // 0 왼쪽, 1 오른쪽, 2 위, 3 아래
    public NavPortal[] All = Array.Empty<NavPortal>();
    public float[] Intra = Array.Empty<float>();   // All.Length² — 구역 안 출입구끼리의 이동 비용
    public byte[] Comp = Array.Empty<byte>();      // 구역 안 연결 성분 번호 (1부터, 0 = 막힌 칸) — 벽으로 갈라진 같은 구역의 조각이 섞이지 않게
}

internal struct NavSeg
{
    public int Region, ExitX, ExitY, PeerX, PeerY;   // Region = (구역 번호 << 8) | 연결 성분
    public int Sector => Region >> 8;
}

/// <summary>구역 단위 경로(회랑): 출구 목록. 같은 (출발 구역, 목표 4×4 칸)을 요청한 그룹이 공유한다.</summary>
internal sealed class NavRoute
{
    public long Key;
    public bool Valid = true, Pending = true, Failed, Approx;
    public int SX, SY, GX, GY;           // 요청 출발 타일 · 요청(해석된) 목표 타일
    public int GoalX, GoalY, GoalSector, GoalRegion; // 실제 목표 (도달 불가면 가장 가까운 도달 가능 칸)
    public NavSeg[] Segs = Array.Empty<NavSeg>();
    public int[] Sectors = Array.Empty<int>();
    public int StartRegion;
    public long LastUsed, Seq;
    public int Work;

    public bool Covers(int region)
    {
        if (Pending) return StartRegion == region;
        foreach (int s in Sectors) if (s == region) return true;
        return false;
    }
}

/// <summary>한 구역 안에서 한 타일(출구 · 목표)로 향하는 비용 격자 (최대 16×16). 많은 그룹이 공유한다.</summary>
internal sealed class NavField
{
    public int Sector, TX, TY, Version;
    public float[] Cost = Array.Empty<float>();
    public long LastUsed;
    public bool Dead;
}

/// <summary>
/// 계층형 길찾기 (HPA*). 맵을 16×16 구역으로 나누고 구역 경계의 통로(출입구)를 노드로 하는 추상 그래프를 만든다.
/// 긴 경로 = 추상 그래프 A* 로 얻은 출구 열 → 각 구역 안에서는 출구/목표로 향하는 <b>지역 흐름장</b>(최대 256칸 Dijkstra)을 따라간다.
/// 맵 전체 Dijkstra 가 필요 없고, 같은 출구로 가는 모든 그룹이 지역 흐름장을 공유한다.
/// 결정적: 모든 계산은 정수 · 고정 순서의 단일 스레드, 작업량 한도는 노드 수(시간 아님).
/// 건물이 놓이거나 파괴되면 영향받은 구역(과 이웃)의 출입구 · 구역 내 비용만 다시 계산한다.
/// </summary>
internal sealed partial class Nav
{
    public const int SS = 16;
    const int ChunkMax = 8;           // 한 통로가 이보다 길면 여러 출입구로 나눈다
    const float Inf = float.PositiveInfinity;
    const int MaxFields = 6000, MaxRoutes = 768;

    static readonly (int Dx, int Dy, float Cost)[] Dirs =
    {
        (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
        (1, 1, 1.41421356f), (1, -1, 1.41421356f), (-1, 1, 1.41421356f), (-1, -1, 1.41421356f),
    };

    readonly GameMap map;
    public readonly int SW, SH;
    readonly NavSector[] sectors;
    readonly List<NavPortal?> portals = new();
    readonly Stack<int> freePortal = new();
    bool built;
    int logRead;
    readonly int cellsW;

    readonly Dictionary<long, NavRoute> routes = new();
    readonly Queue<NavRoute> pending = new();
    readonly Dictionary<long, NavField> fields = new();
    readonly Stack<float[]> pool = new();
    long routeSeq;

    // 스크래치 (단일 스레드)
    readonly PriorityQueue<int, float> heap = new();
    readonly PriorityQueue<int, float> astar = new();
    readonly float[] sdist = new float[SS * SS], gdist = new float[SS * SS], tmpDist = new float[SS * SS];
    float[] gScore = new float[256];
    int[] prev = new int[256], gStamp = new int[256], closedStamp = new int[256];
    int searchId;

    // 통계 · 한도
    public int FieldBudget;
    public int FieldsPerTick = 64;
    public int RoutesComputed, FieldsComputed, Rebuilds;
    public long WorkTotal;
    public int RouteCount => routes.Count;
    public int FieldCount => fields.Count;
    public int PooledArrays => pool.Count;

    /// <summary>이 길찾기 그래프의 통행 기준: 0 = 일반(칸이 열려 있으면 통행), >0 = 큰 유닛용 (칸 중심에서 막힌 칸 가장자리까지 이 거리 이상이어야 통행).</summary>
    public readonly float MinClearance;
    readonly bool[]? ok;            // 큰 유닛용 통행 가능 격자 (MinClearance > 0 일 때만)
    Nav? large;

    /// <summary>큰 유닛(전차 등) 전용 그래프. 처음 부를 때 만든다. 일반 그래프의 BeginTick/Process/Sync 가 함께 돌려준다.</summary>
    public Nav Large => large ??= new Nav(map, World.LargeClearance);

    public bool HasLarge => large != null;

    /// <summary>칸 (x,y) 를 이 그래프가 통행 가능하다고 보는가 (큰 그래프는 여유 공간 포함). 큰 그래프는 Sync 뒤에 쓴다.</summary>
    public bool Walk(int x, int y) => ok == null ? map.IsWalkable(x, y) : map.InBounds(x, y) && ok[y * map.Width + x];

    void FillOk(int x0, int y0, int x1, int y1)
    {
        if (ok == null) return;
        int R = (int)MathF.Ceiling(MinClearance + 0.5f) - 1;
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(map.Width - 1, x1); y1 = Math.Min(map.Height - 1, y1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            ok[y * map.Width + x] = map.IsWalkable(x, y) && map.TileClearance(x, y, MinClearance, R) >= MinClearance;
    }

    /// <summary>(x,y) 에서 가장 가까운 통행 가능 칸 (이 그래프 기준). maxRadius 안에 없으면 null.</summary>
    public (int X, int Y)? NearestOk(int x, int y, int maxRadius = 64)
    {
        x = Math.Clamp(x, 0, map.Width - 1); y = Math.Clamp(y, 0, map.Height - 1);
        if (Walk(x, y)) return (x, y);
        for (int r = 1; r <= maxRadius; r++)
        {
            int best = int.MaxValue, bx = 0, by = 0;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !Walk(x + dx, y + dy)) continue;
                int d = dx * dx + dy * dy;
                if (d < best) (best, bx, by) = (d, x + dx, y + dy);
            }
            if (best != int.MaxValue) return (bx, by);
        }
        return null;
    }

    public Nav(GameMap map, float minClearance = 0f)
    {
        this.map = map;
        MinClearance = minClearance;
        if (minClearance > 0) ok = new bool[map.Width * map.Height];
        SW = (map.Width + SS - 1) / SS;
        SH = (map.Height + SS - 1) / SS;
        cellsW = (map.Width + 3) >> 2;
        sectors = new NavSector[SW * SH];
        for (int sy = 0; sy < SH; sy++)
        for (int sx = 0; sx < SW; sx++)
        {
            int x0 = sx * SS, y0 = sy * SS;
            sectors[sy * SW + sx] = new NavSector { Id = sy * SW + sx, X0 = x0, Y0 = y0, W = Math.Min(SS, map.Width - x0), H = Math.Min(SS, map.Height - y0) };
        }
    }

    public int SectorId(int x, int y) => (y / SS) * SW + x / SS;
    /// <summary>영역 = 구역 안의 연결 성분 (구역 번호 &lt;&lt; 8 | 성분). 벽으로 갈라진 같은 구역의 두 조각은 다른 영역이다.</summary>
    public int RegionOf(int x, int y)
    {
        var s = sectors[(y / SS) * SW + x / SS];
        return (s.Id << 8) | s.Comp[(y - s.Y0) * s.W + (x - s.X0)];
    }
    public NavSector Sector(int id) => sectors[id];

    // ---------------------------------------------------------------- 그래프 구축 · 갱신

    /// <summary>처음이면 전체를 만들고, 이후로는 맵 변경 기록을 읽어 영향받은 구역만 다시 만든다.</summary>
    public void Sync()
    {
        large?.Sync();
        if (!built)
        {
            built = true;
            logRead = map.ChangeLog.Count;
            FillOk(0, 0, map.Width - 1, map.Height - 1);
            for (int sy = 0; sy < SH; sy++)
            for (int sx = 0; sx < SW; sx++)
            {
                if (sx + 1 < SW) RegenBorder(sectors[sy * SW + sx], sectors[sy * SW + sx + 1], true);
                if (sy + 1 < SH) RegenBorder(sectors[sy * SW + sx], sectors[(sy + 1) * SW + sx], false);
            }
            foreach (var s in sectors) ComputeIntra(s);
            if (ok == null) Large.Sync();   // 큰 유닛용 그래프도 미리 만든다 (첫 전차 이동에서 끊기지 않게)
            return;
        }
        if (logRead == map.ChangeLog.Count) return;
        var dirty = new SortedSet<int>();
        for (; logRead < map.ChangeLog.Count; logRead++)
        {
            var (x, y, w, h) = map.ChangeLog[logRead];
            if (ok != null)
            {
                int m = (int)MathF.Ceiling(MinClearance + 0.5f) - 1;
                FillOk(x - m, y - m, x + w - 1 + m, y + h - 1 + m);
                x -= m; y -= m; w += 2 * m; h += 2 * m;
                if (x < 0) { w += x; x = 0; }
                if (y < 0) { h += y; y = 0; }
            }
            int sx0 = Math.Clamp(x / SS, 0, SW - 1), sx1 = Math.Clamp((x + w - 1) / SS, 0, SW - 1);
            int sy0 = Math.Clamp(y / SS, 0, SH - 1), sy1 = Math.Clamp((y + h - 1) / SS, 0, SH - 1);
            for (int sy = sy0; sy <= sy1; sy++)
            for (int sx = sx0; sx <= sx1; sx++) dirty.Add(sy * SW + sx);
        }
        Rebuild(dirty);
    }

    void Rebuild(SortedSet<int> dirty)
    {
        Rebuilds++;
        var affected = new SortedSet<int>(dirty);
        foreach (int id in dirty)
        {
            int sx = id % SW, sy = id / SW;
            if (sx > 0) affected.Add(id - 1);
            if (sx + 1 < SW) affected.Add(id + 1);
            if (sy > 0) affected.Add(id - SW);
            if (sy + 1 < SH) affected.Add(id + SW);
        }
        // 경계 재생성: 한쪽이라도 변경된 구역이면
        foreach (int id in affected)
        {
            int sx = id % SW, sy = id / SW;
            if (sx + 1 < SW && (dirty.Contains(id) || dirty.Contains(id + 1))) RegenBorder(sectors[id], sectors[id + 1], true);
            if (sy + 1 < SH && (dirty.Contains(id) || dirty.Contains(id + SW))) RegenBorder(sectors[id], sectors[id + SW], false);
        }
        foreach (int id in affected) ComputeIntra(sectors[id]);
        foreach (int id in dirty) sectors[id].Version++;
        // 경로 무효화: 영향받은 구역을 지나는 경로, 근사(도달 불가) · 실패 경로 (새 통로가 생겼을 수 있음)
        foreach (var r in routes.Values)
        {
            if (!r.Valid) continue;
            bool hit = !r.Pending && (r.Approx || r.Failed);
            if (!hit && !r.Pending) foreach (int s in r.Sectors) if (affected.Contains(s >> 8)) { hit = true; break; }
            if (!hit && r.Pending && affected.Contains(r.StartRegion >> 8)) hit = true;
            if (hit) r.Valid = false;
        }
        foreach (var k in routes.Where(kv => !kv.Value.Valid).Select(kv => kv.Key).ToList()) routes.Remove(k);
    }

    int NewPortal(NavSector s, int x, int y)
    {
        var p = new NavPortal { S = s, X = x, Y = y };
        if (freePortal.Count > 0) { p.Index = freePortal.Pop(); portals[p.Index] = p; }
        else { p.Index = portals.Count; portals.Add(p); }
        return p.Index;
    }

    void FreePortals(List<NavPortal> list)
    {
        foreach (var p in list) { portals[p.Index] = null; freePortal.Push(p.Index); p.Peer = null; }
        list.Clear();
    }

    /// <summary>a(왼쪽/위)와 b(오른쪽/아래) 사이 경계의 통로를 찾아 출입구를 만든다. 긴 통로는 ChunkMax 단위로 나눠 각 조각 가운데에.</summary>
    void RegenBorder(NavSector a, NavSector b, bool vertical)
    {
        FreePortals(a.BySide[vertical ? 1 : 3]);
        FreePortals(b.BySide[vertical ? 0 : 2]);
        int len = vertical ? a.H : a.W;
        int run = -1;
        for (int k = 0; k <= len; k++)
        {
            bool open = false;
            if (k < len)
            {
                if (vertical) open = Walk(a.X0 + a.W - 1, a.Y0 + k) && Walk(b.X0, a.Y0 + k);
                else open = Walk(a.X0 + k, a.Y0 + a.H - 1) && Walk(a.X0 + k, b.Y0);
            }
            if (open) { if (run < 0) run = k; continue; }
            if (run < 0) continue;
            int L = k - run, n = (L + ChunkMax - 1) / ChunkMax;
            for (int c = 0; c < n; c++)
            {
                int lo = run + c * L / n, hi = run + (c + 1) * L / n - 1, mid = (lo + hi) / 2;
                int ax, ay, bx, by;
                if (vertical) { ax = a.X0 + a.W - 1; ay = a.Y0 + mid; bx = b.X0; by = ay; }
                else { ax = a.X0 + mid; ay = a.Y0 + a.H - 1; bx = ax; by = b.Y0; }
                int ia = NewPortal(a, ax, ay), ib = NewPortal(b, bx, by);
                var pa = portals[ia]!; var pb = portals[ib]!;
                pa.Peer = pb; pb.Peer = pa;
                a.BySide[vertical ? 1 : 3].Add(pa);
                b.BySide[vertical ? 0 : 2].Add(pb);
            }
            run = -1;
        }
    }

    void ComputeIntra(NavSector s)
    {
        var all = new List<NavPortal>();
        for (int side = 0; side < 4; side++) all.AddRange(s.BySide[side]);
        s.All = all.ToArray();
        int n = s.All.Length;
        for (int i = 0; i < n; i++) s.All[i].Slot = i;
        LabelComponents(s);
        s.Intra = new float[n * n];
        for (int i = 0; i < n; i++)
        {
            LocalDijkstra(s, s.All[i].X, s.All[i].Y, tmpDist);
            for (int j = 0; j < n; j++) s.Intra[i * n + j] = tmpDist[(s.All[j].Y - s.Y0) * s.W + (s.All[j].X - s.X0)];
        }
    }

    /// <summary>그래프 전체의 지문 (출입구 위치 · 짝 · 구역 내 비용) — 증분 갱신 결과가 처음부터 만든 것과 같은지 테스트한다.</summary>
    public ulong Signature()
    {
        Sync();
        ulong h = 1469598103934665603UL;
        void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }
        foreach (var s in sectors)
        {
            Mix((ulong)s.All.Length);
            foreach (var p in s.All) { Mix((ulong)p.X); Mix((ulong)p.Y); Mix((ulong)(p.Peer?.X ?? -1)); Mix((ulong)(p.Peer?.Y ?? -1)); }
            foreach (float c in s.Intra) Mix(BitConverter.SingleToUInt32Bits(c));
        }
        return h;
    }

    readonly Stack<int> floodStack = new();

    /// <summary>구역 안 연결 성분 라벨 (LocalDijkstra 와 같은 이동 규칙). 255개를 넘으면 나머지는 255 로 합친다 (드문 예외).</summary>
    void LabelComponents(NavSector s)
    {
        int W = s.W, H = s.H;
        if (s.Comp.Length != W * H) s.Comp = new byte[W * H]; else Array.Clear(s.Comp);
        int label = 0;
        for (int start = 0; start < W * H; start++)
        {
            if (s.Comp[start] != 0 || !Walk(s.X0 + start % W, s.Y0 + start / W)) continue;
            byte l = (byte)(label < 255 ? ++label : 255);
            s.Comp[start] = l;
            floodStack.Push(start);
            while (floodStack.Count > 0)
            {
                int i = floodStack.Pop();
                int x = s.X0 + i % W, y = s.Y0 + i / W;
                foreach (var (dx, dy, _) in Dirs)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < s.X0 || nx >= s.X0 + W || ny < s.Y0 || ny >= s.Y0 + H || !CanStep(x, y, dx, dy)) continue;
                    int ni = (ny - s.Y0) * W + (nx - s.X0);
                    if (s.Comp[ni] == 0) { s.Comp[ni] = l; floodStack.Push(ni); }
                }
            }
        }
    }

    // ---------------------------------------------------------------- 구역 안 Dijkstra

    bool CanStep(int x, int y, int dx, int dy) =>
        Walk(x + dx, y + dy) &&
        (dx == 0 || dy == 0 || (Walk(x + dx, y) && Walk(x, y + dy)));

    /// <summary>구역 s 안에서 (tx,ty) 로부터의 비용 (구역 밖으로 나가지 않음). 확정한 칸 수를 돌려준다 (작업량).</summary>
    int LocalDijkstra(NavSector s, int tx, int ty, float[] dist)
    {
        int W = s.W, H = s.H, x0 = s.X0, y0 = s.Y0;
        Array.Fill(dist, Inf, 0, W * H);
        if (!Walk(tx, ty)) return 0;
        heap.Clear();
        int start = (ty - y0) * W + (tx - x0);
        dist[start] = 0;
        heap.Enqueue(start, 0);
        int settled = 0;
        while (heap.TryDequeue(out int i, out float c))
        {
            if (c > dist[i]) continue;
            settled++;
            int x = x0 + i % W, y = y0 + i / W;
            foreach (var (dx, dy, dc) in Dirs)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < x0 || nx >= x0 + W || ny < y0 || ny >= y0 + H) continue;
                if (!CanStep(x, y, dx, dy)) continue;
                int ni = (ny - y0) * W + (nx - x0);
                float nc = c + dc;
                if (nc < dist[ni]) { dist[ni] = nc; heap.Enqueue(ni, nc); }
            }
        }
        return settled;
    }

    // ---------------------------------------------------------------- 지역 흐름장

    public void BeginTick(long tick)
    {
        FieldBudget = FieldsPerTick;
        if (fields.Count > MaxFields) EvictFields();
        large?.BeginTick(tick);
    }

    void EvictFields()
    {
        var order = fields.OrderBy(kv => kv.Value.LastUsed).ThenBy(kv => kv.Key).Take(fields.Count / 4 + 1).ToList();
        foreach (var kv in order)
        {
            kv.Value.Dead = true;
            if (kv.Value.Cost.Length == SS * SS) pool.Push(kv.Value.Cost);
            kv.Value.Cost = Array.Empty<float>();
            fields.Remove(kv.Key);
        }
    }

    /// <summary>구역 sid 안에서 (tx,ty) 로 향하는 흐름장. 이번 틱 한도를 넘으면 null (호출자는 직진 등으로 대체).</summary>
    public NavField? GetField(int sid, int tx, int ty, long tick)
    {
        var s = sectors[sid];
        long key = ((long)sid << 32) | (uint)(ty * map.Width + tx);
        fields.TryGetValue(key, out var f);
        if (f != null && f.Version == s.Version) { f.LastUsed = tick; return f; }
        if (FieldBudget <= 0) return null;
        FieldBudget--;
        long t0 = World.Profile ? Stopwatch.GetTimestamp() : 0;
        if (f == null)
        {
            f = new NavField { Sector = sid, TX = tx, TY = ty, Cost = pool.Count > 0 ? pool.Pop() : new float[SS * SS] };
            fields[key] = f;
        }
        f.Version = s.Version;
        f.LastUsed = tick;
        int work = LocalDijkstra(s, tx, ty, f.Cost);
        WorkTotal += work;
        FieldsComputed++;
        if (World.Profile) { World.Prof[13] += Stopwatch.GetTimestamp() - t0; World.Prof[15]++; }
        return f;
    }

    /// <summary>f 안의 (x,y) 에서 목표 쪽으로 가장 비용이 낮은 이웃 타일. 목표 칸이거나 도달 불가면 false.</summary>
    public bool Waypoint(NavField f, int x, int y, out int wx, out int wy)
    {
        wx = wy = 0;
        var s = sectors[f.Sector];
        if (x < s.X0 || y < s.Y0 || x >= s.X0 + s.W || y >= s.Y0 + s.H) return false;
        float best = f.Cost[(y - s.Y0) * s.W + (x - s.X0)];
        if (best >= Inf) return false;
        bool found = false;
        foreach (var (dx, dy, _) in Dirs)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < s.X0 || nx >= s.X0 + s.W || ny < s.Y0 || ny >= s.Y0 + s.H) continue;
            if (!CanStep(x, y, dx, dy)) continue;
            float c = f.Cost[(ny - s.Y0) * s.W + (nx - s.X0)];
            if (c < best) { best = c; wx = nx; wy = ny; found = true; }
        }
        return found;
    }

    public float CostAt(NavField f, int x, int y)
    {
        var s = sectors[f.Sector];
        if (x < s.X0 || y < s.Y0 || x >= s.X0 + s.W || y >= s.Y0 + s.H) return Inf;
        return f.Cost[(y - s.Y0) * s.W + (x - s.X0)];
    }

    // ---------------------------------------------------------------- 경로 요청 · 시간 분할

    /// <summary>
    /// (sx,sy) → (gx,gy) 경로 요청. 같은 (출발 구역, 목표 4×4 칸) 경로가 있으면 공유한다 (목표 합치기).
    /// 계산은 Process 가 틱당 작업량 한도 안에서 요청 순서대로 한다 (Pending 동안 호출자는 직진).
    /// personal=true 면 공유하지 않고 이 출발 타일 기준의 새 경로.
    /// </summary>
    public NavRoute GetRoute(int sx, int sy, int gx, int gy, long tick, bool personal = false)
    {
        Sync();
        int ss = RegionOf(sx, sy);
        long key = ((long)ss << 32) | (uint)((gy >> 2) * cellsW + (gx >> 2));
        if (!personal && routes.TryGetValue(key, out var r) && r.Valid) { r.LastUsed = tick; return r; }
        r = new NavRoute { Key = key, SX = sx, SY = sy, GX = gx, GY = gy, StartRegion = ss, LastUsed = tick, Seq = routeSeq++ };
        if (!personal)
        {
            if (routes.Count >= MaxRoutes) EvictRoutes();
            routes[key] = r;
        }
        pending.Enqueue(r);
        return r;
    }

    void EvictRoutes()
    {
        var order = routes.OrderBy(kv => kv.Value.LastUsed).ThenBy(kv => kv.Value.Seq).Where(kv => !kv.Value.Pending).Take(MaxRoutes / 4).ToList();
        foreach (var kv in order) routes.Remove(kv.Key);   // 이미 들고 있는 유닛은 계속 쓸 수 있다 (Valid 는 유지)
    }

    public int PendingCount => pending.Count;

    /// <summary>대기 중인 경로를 작업량(확정 타일 + A* 노드 수) 한도 안에서 계산. 항상 하나는 끝낸다.</summary>
    public int Process(int budget)
    {
        int used = 0;
        while (pending.Count > 0)
        {
            if (used >= budget) break;
            var r = pending.Dequeue();
            if (!r.Valid) { r.Pending = false; r.Failed = true; continue; }
            used += Compute(r);
        }
        if (large != null) used += large.Process(Math.Max(1, budget - used));
        return used;
    }

    /// <summary>테스트 · 도구용: 즉시 계산해서 돌려준다.</summary>
    public NavRoute RouteNow(int sx, int sy, int gx, int gy, long tick = 0)
    {
        var r = GetRoute(sx, sy, gx, gy, tick, personal: true);
        Process(int.MaxValue);
        return r;
    }

    int Compute(NavRoute r)
    {
        long t0 = World.Profile ? Stopwatch.GetTimestamp() : 0;
        int work = 0;
        var ss = sectors[SectorId(r.SX, r.SY)];
        work += LocalDijkstra(ss, r.SX, r.SY, sdist);
        int gx = r.GX, gy = r.GY;
        var gs = sectors[SectorId(gx, gy)];
        work += LocalDijkstra(gs, gx, gy, gdist);
        List<int>? chain = Search(ss, gs, r.SX, r.SY, gx, gy, ref work, out bool direct);
        if (chain == null)
        {
            // 도달 불가: 도달 가능한 칸 중 목표에 가장 가까운 곳으로 (근사)
            var (ax, ay) = NearestReachable(ss, gx, gy, ref work);
            r.Approx = true;
            gx = ax; gy = ay;
            gs = sectors[SectorId(gx, gy)];
            work += LocalDijkstra(gs, gx, gy, gdist);
            chain = Search(ss, gs, r.SX, r.SY, gx, gy, ref work, out direct);
        }
        r.Pending = false;
        r.Work = work;
        WorkTotal += work;
        RoutesComputed++;
        if (chain == null) { r.Failed = true; r.Sectors = new[] { r.StartRegion }; }
        else
        {
            r.GoalX = gx; r.GoalY = gy; r.GoalSector = gs.Id; r.GoalRegion = RegionOf(gx, gy);
            var segs = new List<NavSeg>();
            var secs = new List<int> { r.StartRegion };
            for (int i = 0; i < chain.Count; i++)
            {
                var p = portals[chain[i]]!;
                if (i + 1 < chain.Count && portals[chain[i + 1]] == p.Peer)
                {
                    segs.Add(new NavSeg { Region = RegionOf(p.X, p.Y), ExitX = p.X, ExitY = p.Y, PeerX = p.Peer!.X, PeerY = p.Peer.Y });
                    secs.Add(RegionOf(p.Peer.X, p.Peer.Y));
                }
            }
            if (!secs.Contains(r.GoalRegion)) secs.Add(r.GoalRegion);
            r.Segs = segs.ToArray();
            r.Sectors = secs.Distinct().ToArray();
        }
        if (World.Profile) { World.Prof[12] += Stopwatch.GetTimestamp() - t0; World.Prof[14]++; }
        return work;
    }

    float H(NavPortal p, int gx, int gy)
    {
        float dx = MathF.Abs(p.X - gx), dy = MathF.Abs(p.Y - gy);
        return MathF.Max(dx, dy) + 0.41421356f * MathF.Min(dx, dy);
    }

    void EnsureArrays(int n)
    {
        if (gScore.Length >= n) return;
        int m = Math.Max(n, gScore.Length * 2);
        Array.Resize(ref gScore, m);
        Array.Resize(ref prev, m);
        Array.Resize(ref gStamp, m);
        Array.Resize(ref closedStamp, m);
    }

    /// <summary>출입구 그래프 A*. 성공하면 출입구 번호 열(출발 쪽부터; 같은 구역 직진이면 빈 목록), 도달 불가면 null.</summary>
    List<int>? Search(NavSector ss, NavSector gs, int sx, int sy, int gx, int gy, ref int work, out bool direct)
    {
        direct = false;
        searchId++;
        EnsureArrays(portals.Count + 1);
        int pseudo = portals.Count;
        float pseudoG = Inf;
        int pseudoVia = -1;
        astar.Clear();
        if (ss == gs)
        {
            float dd = sdist[(gy - gs.Y0) * gs.W + (gx - gs.X0)];
            if (dd < Inf) { pseudoG = dd; astar.Enqueue(pseudo, dd); }
        }
        foreach (var p in ss.All)
        {
            float d = sdist[(p.Y - ss.Y0) * ss.W + (p.X - ss.X0)];
            if (d >= Inf) continue;
            gStamp[p.Index] = searchId; gScore[p.Index] = d; prev[p.Index] = -1;
            astar.Enqueue(p.Index, d + H(p, gx, gy));
        }
        while (astar.TryDequeue(out int ni, out float f))
        {
            work++;
            if (ni == pseudo)
            {
                if (f > pseudoG + 1e-3f) continue;
                var chain = new List<int>();
                if (pseudoVia < 0) { direct = true; return chain; }
                for (int c = pseudoVia; c >= 0; c = prev[c]) chain.Add(c);
                chain.Reverse();
                return chain;
            }
            if (closedStamp[ni] == searchId) continue;
            closedStamp[ni] = searchId;
            var pt = portals[ni]!;
            float gp = gScore[ni];
            var S = pt.S;
            if (S == gs)
            {
                float gdv = gdist[(pt.Y - gs.Y0) * gs.W + (pt.X - gs.X0)];
                if (gdv < Inf && gp + gdv < pseudoG) { pseudoG = gp + gdv; pseudoVia = ni; astar.Enqueue(pseudo, pseudoG); }
            }
            int n = S.All.Length;
            for (int q = 0; q < n; q++)
            {
                if (q == pt.Slot) continue;
                float c = S.Intra[pt.Slot * n + q];
                if (c >= Inf) continue;
                Relax(S.All[q], gp + c, ni, gx, gy);
            }
            if (pt.Peer != null) Relax(pt.Peer, gp + 1f, ni, gx, gy);
        }
        return null;
    }

    void Relax(NavPortal q, float ng, int from, int gx, int gy)
    {
        int qi = q.Index;
        if (closedStamp[qi] == searchId) return;
        if (gStamp[qi] == searchId && gScore[qi] <= ng) return;
        gStamp[qi] = searchId; gScore[qi] = ng; prev[qi] = from;
        astar.Enqueue(qi, ng + H(q, gx, gy));
    }

    /// <summary>직전 Search 가 도달 불가로 끝난 뒤 호출: 닫힌(도달 가능) 출입구와 출발 구역 칸 중 (gx,gy) 에 가장 가까운 칸.</summary>
    (int X, int Y) NearestReachable(NavSector ss, int gx, int gy, ref int work)
    {
        float bestD = Inf;
        int bx = -1, by = -1;
        NavSector? viaSector = null;
        // 출발 구역 안에서 도달 가능한 칸
        for (int i = 0; i < ss.W * ss.H; i++)
        {
            if (sdist[i] >= Inf) continue;
            int x = ss.X0 + i % ss.W, y = ss.Y0 + i / ss.W;
            float d = (x - gx) * (x - gx) + (y - gy) * (y - gy);
            if (d < bestD) { bestD = d; bx = x; by = y; viaSector = null; }
        }
        NavPortal? bestPortal = null;
        for (int i = 0; i < portals.Count; i++)
        {
            var p = portals[i];
            if (p == null || i >= closedStamp.Length || closedStamp[i] != searchId) continue;
            float d = (p.X - gx) * (p.X - gx) + (p.Y - gy) * (p.Y - gy);
            // 같은 거리면 출입구 번호(증분 갱신 이력에 따라 달라질 수 있다)가 아니라 (구역 · 구역 안 순서) 로 결정 — 저장 / 불러오기 뒤에도 같은 결과
            if (d < bestD || (d == bestD && bestPortal != null && (long)p.S.Id * 4096 + p.Slot < (long)bestPortal.S.Id * 4096 + bestPortal.Slot)) { bestD = d; bestPortal = p; viaSector = p.S; }
        }
        if (bestPortal != null)
        {
            // 그 출입구가 속한 구역 안에서 출입구로부터 닿는 칸 중 목표에 가장 가까운 칸
            var S = bestPortal.S;
            work += LocalDijkstra(S, bestPortal.X, bestPortal.Y, tmpDist);
            float bd = Inf;
            for (int i = 0; i < S.W * S.H; i++)
            {
                if (tmpDist[i] >= Inf) continue;
                int x = S.X0 + i % S.W, y = S.Y0 + i / S.W;
                float d = (x - gx) * (x - gx) + (y - gy) * (y - gy);
                if (d < bd) { bd = d; bx = x; by = y; }
            }
        }
        if (bx < 0) { bx = ss.X0; by = ss.Y0; }
        return (bx, by);
    }
}
