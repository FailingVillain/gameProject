using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 거친 지형 점수 지도 (전술 AI 2단계, docs/TACTICAL_AI_DESIGN.md §7). 칸 = 8×8 타일 (TacGrid 와 같은 격자).
/// 지형 층 (평균 엄폐 · 고지 비율 · 통행 비율 · 칸 안 최고 엄폐 타일) 은 칸마다 처음 물을 때 계산하고, 지도 · TerrainHash 가 바뀌면 다시 계산한다
/// = 현재 지형의 순수 함수 (저장 불필요, 불러온 월드와 같은 값). 스레드 정적 하나를 세력 · 월드가 함께 쓴다 (다른 지도면 비우고 다시).
/// </summary>
sealed class TacMap
{
    public const int Cell = 8;
    [ThreadStatic] static TacMap? inst;
    GameMap? map;
    ulong hash;
    int stamp = 1;
    public int Cols, Rows;
    float[] cover = Array.Empty<float>(), high = Array.Empty<float>(), walk = Array.Empty<float>();
    int[] best = Array.Empty<int>(), cellStamp = Array.Empty<int>();

    public static TacMap For(GameMap m)
    {
        var t = inst ??= new TacMap();
        t.Bind(m);
        return t;
    }

    void Bind(GameMap m)
    {
        ulong h = (ulong)m.TerrainVersion;   // N16: 전체 지형 해시 대신 변경 번호 (포탄 구덩이마다 지도 전체 해시를 다시 계산하지 않게)
        if (!ReferenceEquals(m, map))
        {
            map = m;
            Cols = (m.Width + Cell - 1) / Cell; Rows = (m.Height + Cell - 1) / Cell;
            int n = Cols * Rows;
            if (cover.Length < n) { cover = new float[n]; high = new float[n]; walk = new float[n]; best = new int[n]; cellStamp = new int[n]; }
            else Array.Clear(cellStamp, 0, n);
            stamp = 1; hash = h;
            return;
        }
        if (h != hash) { hash = h; stamp++; }
    }

    public int CellOf(Vector2 p) => Math.Clamp((int)(p.Y / Cell), 0, Rows - 1) * Cols + Math.Clamp((int)(p.X / Cell), 0, Cols - 1);
    public Vector2 CellCenter(int c) => new((c % Cols + 0.5f) * Cell, (c / Cols + 0.5f) * Cell);

    /// <summary>타일 엄폐 점수 (고지 제외): (1 − 맞을 확률 배수) × 2, 철조망 −1.</summary>
    public static float TileCover(GameMap m, Vector2 q)
    {
        var (hit, _) = m.TerrainCover(q);
        float s = (1f - hit) * 2f;
        if (m.IsWire(q)) s -= 1f;
        return s;
    }

    void Ensure(int c)
    {
        if (cellStamp[c] == stamp) return;
        cellStamp[c] = stamp;
        var m = map!;
        int cx = c % Cols, cy = c / Cols;
        int n = 0, hi = 0, bestI = -1; float sum = 0, bestS = float.MinValue;
        for (int y = cy * Cell; y < Math.Min(m.Height, cy * Cell + Cell); y++)
        for (int x = cx * Cell; x < Math.Min(m.Width, cx * Cell + Cell); x++)
        {
            if (!m.IsWalkable(x, y)) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float tc = TileCover(m, q);
            bool h = m.IsHigh(x, y);
            n++; sum += tc; if (h) hi++;
            float sc = tc + (h ? 0.6f : 0);
            if (sc > bestS) { bestS = sc; bestI = y * m.Width + x; }
        }
        cover[c] = n > 0 ? sum / n : 0;
        high[c] = n > 0 ? hi / (float)n : 0;
        walk[c] = n / (float)(Cell * Cell);
        best[c] = bestI;
    }

    public float Cover(int c) { Ensure(c); return cover[c]; }
    public float High(int c) { Ensure(c); return high[c]; }
    public float Walk(int c) { Ensure(c); return walk[c]; }
    /// <summary>칸 안에서 엄폐 + 고지 점수가 가장 높은 통행 가능 타일의 중심 (없으면 칸 중심).</summary>
    public Vector2 BestTile(int c)
    {
        Ensure(c);
        int i = best[c];
        return i < 0 ? CellCenter(c) : new Vector2(i % map!.Width + 0.5f, i / map.Width + 0.5f);
    }

    /// <summary>
    /// 위치 고르기 (효용 점수): 적 enemy 에서 wantDist 떨어진 이상점 둘레 (옆 허용 폭 lateral) 의 칸마다
    /// 엄폐 · 고지 (가중치) − 위협 (보이는 적 수, 사거리 threatR 안) − 거리 어긋남, 상위 4개만 사선 (HasFiringLine) 검사 → 최고 칸의 최고 엄폐 타일.
    /// 결정적: 칸 순회 순서 고정, 동점이면 먼저 본 칸.
    /// </summary>
    public Vector2 PickPosition(Vector2 from, Vector2 enemy, float wantDist, float lateral, TacticsProfile T, TacGrid grid, float threatR, float losMul = 1f, float highMul = 1f)
    {
        var m = map!;
        Vector2 toFrom = from - enemy;
        float df = toFrom.Length();
        Vector2 back = df > 1e-3f ? toFrom / df : new Vector2(-1, 0);
        Vector2 side = new(-back.Y, back.X);
        Vector2 ideal = enemy + back * wantDist;   // 1단계 기준점: 지금 위치에서 적 쪽 직선 위 원하는 거리 (옆 위치 유지 — 중대 정면 배치를 흐트리지 않는다)
        float half = lateral * 0.5f + Cell;
        int rc = Math.Max(1, (int)MathF.Ceiling(half / Cell));
        int ic = CellOf(ideal), icx = ic % Cols, icy = ic / Cols;
        Span<int> topC = stackalloc int[4];
        Span<float> topS = stackalloc float[4];
        int nTop = 0;
        for (int y = Math.Max(0, icy - rc); y <= Math.Min(Rows - 1, icy + rc); y++)
        for (int x = Math.Max(0, icx - rc); x <= Math.Min(Cols - 1, icx + rc); x++)
        {
            int c = y * Cols + x;
            if (Walk(c) < 0.25f) continue;
            Vector2 bt = BestTile(c);
            float lat = MathF.Abs(Vector2.Dot(bt - ideal, side));
            if (lat > half) continue;
            float d = Vector2.Distance(bt, enemy);
            float sc = T.CoverWeight * cover[c] + T.HighWeight * highMul * high[c]
                       - T.ThreatWeight * MathF.Min(1.5f, grid.Stats(bt, threatR).Count / 20f)
                       - 0.05f * MathF.Max(0, MathF.Abs(d - wantDist) - 2f) - 0.03f * Vector2.Distance(bt, ideal);
            if (d < wantDist * 0.6f) sc -= 1f;   // 너무 가깝다
            // 상위 4개 (삽입 정렬, 동점이면 먼저 본 칸)
            int k = nTop;
            while (k > 0 && topS[k - 1] < sc) k--;
            if (k >= 4) continue;
            for (int j = Math.Min(nTop, 3); j > k; j--) { topS[j] = topS[j - 1]; topC[j] = topC[j - 1]; }
            topS[k] = sc; topC[k] = c;
            if (nTop < 4) nTop++;
        }
        if (nTop == 0) return ideal;
        // 기준점 자체의 점수 (타일 엄폐 · 고지 · 사선 · 위협): 후보 칸이 이보다 뚜렷이 (0.2) 낫지 않으면 기준점 그대로 —
        // 평지에서 칸 모서리 타일로 쏠려 중대가 뭉치던 문제 (2단계 실험: 평지 보병 1승 7패 → 기준점 유지)
        float idealSc = T.CoverWeight * TileCover(m, ideal) + T.HighWeight * highMul * (m.IsHigh(ideal) ? 1f : 0f)
                        - T.ThreatWeight * MathF.Min(1.5f, grid.Stats(ideal, threatR).Count / 20f)
                        + (m.HasFiringLine(ideal, enemy, 0, 0) ? T.LosWeight * losMul : -0.5f * T.LosWeight * losMul) + 0.2f;
        int bestC = topC[0]; float bestSc = float.MinValue;
        for (int i = 0; i < nTop; i++)
        {
            Vector2 bt = BestTile(topC[i]);
            float sc = topS[i] + (m.HasFiringLine(bt, enemy, 0, 0) ? T.LosWeight * losMul : -0.5f * T.LosWeight * losMul);
            if (sc > bestSc) { bestSc = sc; bestC = topC[i]; }
        }
        return bestSc > idealSc ? BestTile(bestC) : ideal;
    }

    /// <summary>위치 점수만 (연대가 화력 기반 대대를 고를 때): 기준점 칸의 엄폐 · 고지 + 적 사선.</summary>
    public float ScoreAt(Vector2 p, Vector2 enemy, TacticsProfile T)
    {
        int c = CellOf(p);
        Vector2 bt = BestTile(c);
        return T.CoverWeight * Cover(c) + T.HighWeight * high[c] + (map!.HasFiringLine(bt, enemy, 0, 0) ? T.LosWeight : 0);
    }
}

/// <summary>
/// 보이는 적의 거친 격자 (8칸). 질의는 목록 순서 동점 규칙 (같은 거리면 목록 앞쪽) 으로 결정적. 매 AI 호출마다 다시 만든다 (저장 안 함).
/// </summary>
sealed class TacGrid
{
    public const int Any = 0, Armored = 1, Infantry = 2;
    const int Cell = 8;
    List<Unit> list = new();
    int cols, rows;
    int[] start = Array.Empty<int>(), items = Array.Empty<int>(), cellOf = Array.Empty<int>(), fill = Array.Empty<int>();
    readonly List<int> mgs = new();

    public struct Stat { public int Count, Moving, Fleeing; public float Supp; public Vector2 Sum; }

    int CX(float x) => Math.Clamp((int)(x / Cell), 0, cols - 1);
    int CY(float y) => Math.Clamp((int)(y / Cell), 0, rows - 1);

    public void Build(GameMap m, List<Unit> enemies)
    {
        list = enemies;
        cols = (m.Width + Cell - 1) / Cell; rows = (m.Height + Cell - 1) / Cell;
        int cells = cols * rows;
        if (start.Length < cells + 1) { start = new int[cells + 1]; fill = new int[cells]; }
        else { Array.Clear(start, 0, cells + 1); Array.Clear(fill, 0, cells); }
        if (items.Length < enemies.Count) { items = new int[enemies.Count]; cellOf = new int[enemies.Count]; }
        mgs.Clear();
        for (int i = 0; i < enemies.Count; i++)
        {
            var u = enemies[i];
            int c = CY(u.Pos.Y) * cols + CX(u.Pos.X);
            cellOf[i] = c; start[c + 1]++;
            if (u.Weapon is { Spray: > 0 } && u.SuppressionRatio < 0.6f) mgs.Add(i);
        }
        for (int c = 0; c < cells; c++) start[c + 1] += start[c];
        for (int i = 0; i < enemies.Count; i++) items[start[cellOf[i]] + fill[cellOf[i]]++] = i;
    }

    static bool Pass(Unit u, int filter) => filter switch
    {
        Armored => u.Def.IsArmored,
        Infantry => !u.Def.IsArmored && u.Weapon != null,
        _ => true,
    };

    public Unit? Nearest(Vector2 c, int filter)
    {
        if (list.Count == 0) return null;
        int cx = CX(c.X), cy = CY(c.Y), maxR = Math.Max(cols, rows);
        int bestI = -1; float bestD = float.MaxValue;
        for (int r = 0; r <= maxR; r++)
        {
            if (bestI >= 0 && r >= 2) { float bound = (r - 1) * Cell * 0.99f; if (bestD < bound * bound) break; }
            for (int y = Math.Max(0, cy - r); y <= Math.Min(rows - 1, cy + r); y++)
            for (int x = Math.Max(0, cx - r); x <= Math.Min(cols - 1, cx + r); x++)
            {
                if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != r) continue;
                int cell = y * cols + x;
                for (int k = start[cell], e = start[cell + 1]; k < e; k++)
                {
                    int i = items[k];
                    if (!Pass(list[i], filter)) continue;
                    float d = Vector2.DistanceSquared(list[i].Pos, c);
                    if (d < bestD || (d == bestD && i < bestI)) { bestD = d; bestI = i; }
                }
            }
        }
        return bestI >= 0 ? list[bestI] : null;
    }

    public Stat Stats(Vector2 c, float radius)
    {
        var s = new Stat();
        if (list.Count == 0) return s;
        float r2 = radius * radius;
        int x0 = CX(c.X - radius), x1 = CX(c.X + radius), y0 = CY(c.Y - radius), y1 = CY(c.Y + radius);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int cell = y * cols + x;
            for (int k = start[cell], e = start[cell + 1]; k < e; k++)
            {
                var u = list[items[k]];
                if (Vector2.DistanceSquared(u.Pos, c) > r2) continue;
                s.Count++; s.Supp += u.SuppressionRatio; s.Sum += u.Pos;
                if (u.Walking) s.Moving++;
                if (World.IsFleeing(u)) s.Fleeing++;   // 후퇴 명령으로 이동 중 (Pursuit.cs)
            }
        }
        return s;
    }

    /// <summary>반경 안에서 dir 방향으로 가장 깊은 (적 쪽 뒤) 적 — 포병 연신 표적. 동점이면 목록 앞쪽.</summary>
    public Unit? Deepest(Vector2 c, float radius, Vector2 dir, Func<Unit, bool>? ok = null)
    {
        if (list.Count == 0) return null;
        float r2 = radius * radius;
        int x0 = CX(c.X - radius), x1 = CX(c.X + radius), y0 = CY(c.Y - radius), y1 = CY(c.Y + radius);
        Unit? best = null; float bd = float.MinValue; int bi = int.MaxValue;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int cell = y * cols + x;
            for (int k = start[cell], e = start[cell + 1]; k < e; k++)
            {
                int i = items[k];
                var u = list[i];
                if (Vector2.DistanceSquared(u.Pos, c) > r2 || (ok != null && !ok(u))) continue;
                float d = Vector2.Dot(u.Pos - c, dir);
                if (d > bd || (d == bd && i < bi)) { bd = d; best = u; bi = i; }
            }
        }
        return best;
    }

    /// <summary>반경 안에서 c 에 가장 가까운, 조건을 만족하는 적 (동점이면 목록 앞쪽).</summary>
    public Unit? NearestWhere(Vector2 c, float radius, Func<Unit, bool> ok)
    {
        if (list.Count == 0) return null;
        float r2 = radius * radius;
        int x0 = CX(c.X - radius), x1 = CX(c.X + radius), y0 = CY(c.Y - radius), y1 = CY(c.Y + radius);
        Unit? best = null; float bd = float.MaxValue; int bi = int.MaxValue;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int cell = y * cols + x;
            for (int k = start[cell], e = start[cell + 1]; k < e; k++)
            {
                int i = items[k];
                var u = list[i];
                float d = Vector2.DistanceSquared(u.Pos, c);
                if (d > r2 || !ok(u)) continue;
                if (d < bd || (d == bd && i < bi)) { bd = d; best = u; bi = i; }
            }
        }
        return best;
    }

    /// <summary>반경 안에서 점수 (NaN · 음의 무한 = 후보 아님) 가 가장 큰 적 (동점이면 목록 앞쪽) — 보병포 표적 (N22).</summary>
    public Unit? BestWhere(Vector2 c, float radius, Func<Unit, float> score)
    {
        if (list.Count == 0) return null;
        float r2 = radius * radius;
        int x0 = CX(c.X - radius), x1 = CX(c.X + radius), y0 = CY(c.Y - radius), y1 = CY(c.Y + radius);
        Unit? best = null; float bs = float.NegativeInfinity; int bi = int.MaxValue;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int cell = y * cols + x;
            for (int k = start[cell], e = start[cell + 1]; k < e; k++)
            {
                int i = items[k];
                var u = list[i];
                if (Vector2.DistanceSquared(u.Pos, c) > r2) continue;
                float sc = score(u);
                if (float.IsNaN(sc) || float.IsNegativeInfinity(sc)) continue;
                if (sc > bs || (sc == bs && i < bi)) { bs = sc; best = u; bi = i; }
            }
        }
        return best;
    }

    /// <summary>반경 안에 제압되지 않은 (비율 &lt; 0.6) 기관총 · 흩뿌림 무기 적이 있는가.</summary>
    public bool ActiveMgNear(Vector2 c, float radius)
    {
        float r2 = radius * radius;
        foreach (int i in mgs) if (Vector2.DistanceSquared(list[i].Pos, c) <= r2) return true;
        return false;
    }

    /// <summary>기병 후보 칸 (pursuit 2026-10-19): 보병 (장갑 아님 · 무장) 수 · 제압 · 이동 (후퇴 제외) · 후퇴 · 중심 · 방향 합 · c 에 가장 가까운 유닛.</summary>
    public struct CavCell { public int N, Moving, Fleeing; public float Supp; public Vector2 Cen, Face; public Unit Nearest; public float NearD; }

    public void CavalryCells(Vector2 c, float radius, List<CavCell> into)
    {
        into.Clear();
        if (list.Count == 0) return;
        float r2 = radius * radius;
        int x0 = CX(c.X - radius), x1 = CX(c.X + radius), y0 = CY(c.Y - radius), y1 = CY(c.Y + radius);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int cell = y * cols + x;
            var cc = new CavCell { NearD = float.MaxValue };
            Vector2 sum = Vector2.Zero;
            for (int k = start[cell], e = start[cell + 1]; k < e; k++)
            {
                var u = list[items[k]];
                if (u.Def.IsArmored || u.Weapon == null) continue;
                float d = Vector2.DistanceSquared(u.Pos, c);
                if (d > r2) continue;
                cc.N++; cc.Supp += u.SuppressionRatio; sum += u.Pos;
                if (World.IsFleeing(u)) cc.Fleeing++;
                else if (u.Walking) cc.Moving++;
                cc.Face += new Vector2(DetMath.Cos(u.Facing), DetMath.Sin(u.Facing));
                if (d < cc.NearD) { cc.NearD = d; cc.Nearest = u; }
            }
            if (cc.N == 0) continue;
            cc.Cen = sum / cc.N; cc.Supp /= cc.N; cc.Face /= cc.N; cc.NearD = MathF.Sqrt(cc.NearD);
            into.Add(cc);
        }
    }

    /// <summary>p 가 제압되지 않은 기관총 (반경 radius 안) 의 정면 사계 (방향과 이루는 각의 코사인 ≥ cosFront) 안인가.</summary>
    public bool MgCovers(Vector2 p, float radius, float cosFront)
    {
        float r2 = radius * radius;
        foreach (int i in mgs)
        {
            var m = list[i];
            Vector2 d = p - m.Pos;
            float l2 = d.LengthSquared();
            if (l2 > r2) continue;
            if (l2 < 1e-4f) return true;
            float cos = Vector2.Dot(d / MathF.Sqrt(l2), new Vector2(DetMath.Cos(m.Facing), DetMath.Sin(m.Facing)));
            if (cos >= cosFront) return true;
        }
        return false;
    }

    /// <summary>보이는 적 가운데 가장 긴 직사 사거리 (없으면 0).</summary>
    public float MaxRange()
    {
        float r = 0;
        foreach (var u in list) if (u.Weapon != null && !World.IsIndirect(u.Weapon)) r = MathF.Max(r, u.Weapon.Range);
        return r;
    }

    /// <summary>기병 돌격 표적: 반경 안 격자 칸마다 보병 (장갑 아님) 의 평균 제압 · 이동 비율로 점수, 살아 있는 기관총 10칸 안은 제외. 표적 = 그 칸에서 c 에 가장 가까운 보병.</summary>
    public Unit? BestCavalryCell(Vector2 c, float radius, float needSupp, int ourCount, float mgRadius = 10f)
    {
        if (list.Count == 0) return null;
        float r2 = radius * radius;
        int x0 = CX(c.X - radius), x1 = CX(c.X + radius), y0 = CY(c.Y - radius), y1 = CY(c.Y + radius);
        float bestScore = float.MinValue; Unit? best = null;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int cell = y * cols + x;
            int n = 0, moving = 0; float supp = 0; Vector2 sum = Vector2.Zero;
            Unit? nearest = null; float nd = float.MaxValue;
            for (int k = start[cell], e = start[cell + 1]; k < e; k++)
            {
                var u = list[items[k]];
                if (u.Def.IsArmored || u.Weapon == null) continue;
                float d = Vector2.DistanceSquared(u.Pos, c);
                if (d > r2) continue;
                n++; supp += u.SuppressionRatio; sum += u.Pos;
                if (u.Walking) moving++;
                if (d < nd) { nd = d; nearest = u; }
            }
            if (n == 0) continue;
            Vector2 cen = sum / n;
            if (ActiveMgNear(cen, mgRadius)) continue;
            float avg = supp / n, mf = moving / (float)n;
            bool ok = avg >= needSupp || (mf >= 0.6f && n <= ourCount);
            if (!ok) continue;
            float score = avg + 0.25f * mf + (n * 2 <= ourCount ? 0.2f : 0) - MathF.Sqrt(nd) / 120f;
            if (score > bestScore) { bestScore = score; best = nearest; }
        }
        return best;
    }
}
