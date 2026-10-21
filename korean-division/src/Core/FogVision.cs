namespace KoreanDivision.Core;

/// <summary>
/// 지형 시야 (ROADMAP '지형 · 높이', 경량판): 굵은 칸 묶음 (vision.blockSize × blockSize 타일) 위에서 재귀 shadowcasting.
/// 막는 칸 = 바위 · 민간 가옥 (보이지만 너머를 막음), 깊은 숲 (terrain.forestSeeDepth 보다 깊은 칸 — 붙어 있지 않으면 안 보이고 너머도 막음),
/// 평지 관측자에게는 고지 칸 (보이지만 너머를 막음). 묶음 분류 = 4칸 중 절반 이상이 그 종류.
/// 결과 (묶음 단위 가시 행 구간) 는 (원점 묶음, 반경, 관측자 고지) 로 메모한다 — 지형만의 순수 함수라 결정적이고,
/// 비워도 (상한 초과 · 불러오기) 결과가 같다. 저장하지 않는다.
/// </summary>
internal sealed class TerrainVision
{
    readonly GameMap map;
    public readonly int Bs, Bw, Bh;
    readonly byte[] cat;      // 묶음 분류: 0 열림, 1 막힘(보임), 2 깊은 숲(안 보임 · 막힘)
    readonly bool[] bhigh;    // 묶음 고지
    readonly Dictionary<long, int[]> memo = new();
    const int MemoCap = 40000;

    // 작업 버퍼 (계산 중에만)
    bool[] vis = Array.Empty<bool>();
    int visR = -1, ox, oy, rad;
    float rad2;
    bool obsHigh;

    public long Casts, Hits;   // 측정용

    public TerrainVision(GameMap map, int blockSize)
    {
        this.map = map;
        Bs = Math.Max(1, blockSize);
        Bw = (map.Width + Bs - 1) / Bs;
        Bh = (map.Height + Bs - 1) / Bs;
        cat = new byte[Bw * Bh];
        bhigh = new bool[Bw * Bh];
        for (int by = 0; by < Bh; by++)
        for (int bx = 0; bx < Bw; bx++) Classify(bx, by);
    }

    /// <summary>묶음 분류를 다시 계산. 바뀌었으면 true.</summary>
    bool Classify(int bx, int by)
    {
        int seeDepth = Defs.Current.Terrain.ForestSeeDepth;
        {
            int wall = 0, deep = 0, hi = 0, n = 0;
            for (int y = by * Bs; y < Math.Min(map.Height, by * Bs + Bs); y++)
            for (int x = bx * Bs; x < Math.Min(map.Width, bx * Bs + Bs); x++)
            {
                n++;
                var t = map.Get(x, y);
                if (t is TerrainType.Rock or TerrainType.House) wall++;
                else if (t == TerrainType.Forest && map.ForestDepth(x, y) > seeDepth) deep++;
                if (map.IsHigh(x, y)) hi++;
            }
            int i = by * Bw + bx;
            byte c = (byte)(wall * 2 >= n ? 1 : deep * 2 >= n ? 2 : (wall + deep) * 2 >= n ? 1 : 0);
            bool h = hi * 2 >= n;
            bool changed = cat[i] != c || bhigh[i] != h;
            cat[i] = c;
            bhigh[i] = h;
            return changed;
        }
    }

    /// <summary>시뮬 중 칸 (x, y) 지형이 바뀜 (참호 완성 · 포탄 구덩이): 주변 묶음을 다시 분류하고, 분류가 바뀌었으면 메모를 비운다 (숲 깊이가 번지는 범위까지).</summary>
    public void Refresh(int x, int y)
    {
        int r = Defs.Current.Terrain.ForestSeeDepth + 2;
        int bx0 = Math.Max(0, (x - r) / Bs), by0 = Math.Max(0, (y - r) / Bs);
        int bx1 = Math.Min(Bw - 1, (x + r) / Bs), by1 = Math.Min(Bh - 1, (y + r) / Bs);
        bool changed = false;
        for (int by = by0; by <= by1; by++)
        for (int bx = bx0; bx <= bx1; bx++) changed |= Classify(bx, by);
        if (changed) memo.Clear();
    }

    public int Memoized => memo.Count;

    /// <summary>타일 pos 에 선 관측자 (반경 r 타일) 의 가시 구간을 grid 에 2 로 찍는다.</summary>
    public void Stamp(byte[] grid, int tx, int ty, int r)
    {
        int bx = tx / Bs, by = ty / Bs;
        if ((uint)bx >= (uint)Bw || (uint)by >= (uint)Bh) return;
        bool hiObs = map.IsHigh(tx, ty);
        long key = ((long)(by * Bw + bx) << 16) | ((long)Math.Min(r, 32000) << 1) | (hiObs ? 1L : 0L);
        if (!memo.TryGetValue(key, out var runs))
        {
            if (memo.Count >= MemoCap) memo.Clear();
            runs = Compute(bx, by, r, hiObs);
            memo[key] = runs;
            Casts++;
        }
        else Hits++;
        int W = map.Width, H = map.Height;
        for (int k = 0; k < runs.Length; k += 3)
        {
            int rby = by + runs[k];
            int x0 = (bx + runs[k + 1]) * Bs, x1 = Math.Min(W - 1, (bx + runs[k + 2]) * Bs + Bs - 1);
            if (x0 < 0) x0 = 0;
            if (x1 < x0) continue;
            int y0 = rby * Bs, y1 = Math.Min(H - 1, y0 + Bs - 1);
            for (int y = Math.Max(0, y0); y <= y1; y++) Array.Fill(grid, (byte)2, y * W + x0, x1 - x0 + 1);
        }
    }

    /// <summary>[테스트] 타일 (tx, ty) 관측자가 타일 (qx, qy) 를 보는지 (같은 계산 경로).</summary>
    public bool Sees(int tx, int ty, int r, int qx, int qy)
    {
        var g = new byte[map.Width * map.Height];
        Stamp(g, tx, ty, r);
        return map.InBounds(qx, qy) && g[qy * map.Width + qx] == 2;
    }

    int[] Compute(int bx, int by, int r, bool high)
    {
        float rb = (r + 0.5f) / Bs;          // 묶음 단위 반경 (타일 원 r+0.5 와 같은 크기)
        int R = (int)MathF.Ceiling(rb);
        int side = 2 * R + 1;
        if (visR != R) { vis = new bool[side * side]; visR = R; } else Array.Clear(vis);
        ox = bx; oy = by; rad = R; rad2 = rb * rb; obsHigh = high;
        vis[R * side + R] = true;
        // 8 옥탄트 (xx, xy, yx, yy)
        Cast(1, 1f, 0f, 1, 0, 0, 1);
        Cast(1, 1f, 0f, 0, 1, 1, 0);
        Cast(1, 1f, 0f, 0, -1, 1, 0);
        Cast(1, 1f, 0f, -1, 0, 0, 1);
        Cast(1, 1f, 0f, -1, 0, 0, -1);
        Cast(1, 1f, 0f, 0, -1, -1, 0);
        Cast(1, 1f, 0f, 0, 1, -1, 0);
        Cast(1, 1f, 0f, 1, 0, 0, -1);
        var runs = new List<int>();
        for (int dy = -R; dy <= R; dy++)
        {
            int row = (dy + R) * side, start = int.MinValue;
            for (int dx = -R; dx <= R + 1; dx++)
            {
                bool v = dx <= R && vis[row + dx + R];
                if (v && start == int.MinValue) start = dx;
                else if (!v && start != int.MinValue) { runs.Add(dy); runs.Add(start); runs.Add(dx - 1); start = int.MinValue; }
            }
        }
        return runs.ToArray();
    }

    bool Opaque(int x, int y)
    {
        if ((uint)x >= (uint)Bw || (uint)y >= (uint)Bh) return true;
        int i = y * Bw + x;
        return cat[i] != 0 || (!obsHigh && bhigh[i]);
    }

    /// <summary>재귀 shadowcasting (RogueBasin 'FOV using recursive shadowcasting'), 한 옥탄트.</summary>
    void Cast(int row, float start, float end, int xx, int xy, int yx, int yy)
    {
        if (start < end) return;
        int side = 2 * rad + 1;
        float newStart = 0f;
        for (int j = row; j <= rad; j++)
        {
            int dx = -j - 1, dy = -j;
            bool blocked = false;
            while (dx <= 0)
            {
                dx++;
                int lx = dx * xx + dy * xy, ly = dx * yx + dy * yy;
                int X = ox + lx, Y = oy + ly;
                float lSlope = (dx - 0.5f) / (dy + 0.5f), rSlope = (dx + 0.5f) / (dy - 0.5f);
                if (start < rSlope) continue;
                if (end > lSlope) break;
                bool inMap = (uint)X < (uint)Bw && (uint)Y < (uint)Bh;
                if (inMap && lx * lx + ly * ly <= rad2)
                {
                    // 깊은 숲은 바로 옆 묶음이 아니면 보이지 않는다
                    if (cat[Y * Bw + X] != 2 || (Math.Abs(lx) <= 1 && Math.Abs(ly) <= 1))
                        vis[(ly + rad) * side + lx + rad] = true;
                }
                bool wall = Opaque(X, Y);
                if (blocked)
                {
                    if (wall) { newStart = rSlope; continue; }
                    blocked = false;
                    start = newStart;
                }
                else if (wall && j < rad)
                {
                    blocked = true;
                    Cast(j + 1, start, lSlope, xx, xy, yx, yy);
                    newStart = rSlope;
                }
            }
            if (blocked) break;
        }
    }
}
