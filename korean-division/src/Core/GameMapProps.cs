using System.Numerics;

namespace KoreanDivision.Core;

public sealed partial class GameMap
{
    /// <summary>모든 소품을 걷는다 (막힌 칸도 푼다) - World 를 만들기 전에. 옛 교전 테스트의 평지 가정 (TestDefs.Flat) 용.</summary>
    public void ClearProps()
    {
        PropSeeds.Clear();
        if (propBlock == null) return;
        for (int i = 0; i < propBlock.Length; i++)
            if (propBlock[i]) { propBlock[i] = false; Set(i % Width, i / Width, Get(i % Width, i / Width)); }
        propBlock = null;
        Version++;
    }

    static readonly string[] TreeKinds = { "tree_birch", "tree_larch", "tree_pine", "tree_oak" };

    /// <summary>
    /// F6 소품 흩뿌리기 (결정적: 좌표 해시 + 시드, 사칙연산만). 칸 묶음 (props.json rules.scatter.cell) 마다 군집을 정한다:
    /// 숲 가장자리 (숲 2칸 안) 군집 = 나무 (종 하나가 우세) + 가끔 작은 바위, 들판 군집 = 바위 + 가끔 나무 한 그루.
    /// 풀 · 흙 칸에만, 시작점 · 시설 둘레 · avoid 가 true 인 곳은 비운다. 막는 바위 (blocksMove) 는 8방향 이웃이 모두 걸을 수 있고
    /// 둘레 2칸에 다른 막는 바위가 없을 때만 (아니면 작은 바위로) - 길이 끊기지 않는다. mirror 면 180 도 회전 대칭 (지도 앞쪽 절반만 만들고 복사).
    /// 돌려주는 값 = 놓은 소품 수.
    /// </summary>
    internal int ScatterProps(bool mirror, Func<float, float, bool>? avoid = null, float density = 1f, int cellOverride = 0)
    {
        var pd = PropDefs.Current;
        if (!pd.Rules.Enabled || pd.Types.Count == 0) return 0;
        var sr = pd.Rules.Scatter;
        int cell = Math.Max(4, cellOverride > 0 ? cellOverride : sr.Cell);
        int seed = Seed;
        int gw = Width / cell + 1, gh = Height / cell + 1;
        int before = PropSeeds.Count;
        uint H(int a, int b, int salt)
        {
            uint v = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791) ^ (uint)seed * 2654435761u ^ 0x7F4A7C15u;
            v ^= v >> 13; v *= 0x5bd1e995; v ^= v >> 15;
            v *= 0x27d4eb2d; v ^= v >> 14;
            return v;
        }
        bool Protected(int x, int y)
        {
            float sc = sr.StartClear;
            foreach (var (sx, sy) in StartTiles)
                if ((x - sx) * (x - sx) + (y - sy) * (y - sy) <= sc * sc) return true;
            foreach (var r in ResourceSites)
            {
                int s = SiteSize(r.Kind);
                float cx = r.X + s / 2f, cy = r.Y + s / 2f, rad = s / 2f + 4f;
                if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= rad * rad) return true;
            }
            return false;
        }
        bool Plantable(int x, int y)
        {
            if (!InBounds(x, y) || Protected(x, y)) return false;
            var t = Get(x, y);
            if (t != TerrainType.Grass && t != TerrainType.Dirt) return false;
            return IsTerrainWalkable(x, y) && OccupantAt(x, y) == 0;
        }
        bool NearForest(int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (InBounds(x + dx, y + dy) && Get(x + dx, y + dy) == TerrainType.Forest) return true;
            return false;
        }
        bool CanBlockHere(int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!InBounds(nx, ny)) { if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) return false; continue; }
                if (IsPropBlocked(nx, ny)) return false;
                if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && (!IsTerrainWalkable(nx, ny) || OccupantAt(nx, ny) != 0)) return false;
            }
            return true;
        }
        bool Crowded(float x, float y)
        {
            float sp2 = sr.Spacing * sr.Spacing;
            foreach (var p in PropSeeds)
                if ((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < sp2) return true;
            return false;
        }
        bool Put(float x, float y, string type)
        {
            int tx = (int)MathF.Floor(x), ty = (int)MathF.Floor(y);
            if (!Plantable(tx, ty) || Crowded(x, y) || (avoid != null && avoid(x, y))) return false;
            if (!pd.TryGet(type, out var def)) return false;
            if (def.BlocksMove && !CanBlockHere(tx, ty))
            {
                type = "rock_s";
                if (!pd.TryGet(type, out def) || Crowded(x, y)) return false;
            }
            AddPropSeed(x, y, type, def.BlocksMove);
            return true;
        }
        for (int gy = 0; gy < gh; gy++)
        for (int gx = 0; gx < gw; gx++)
        {
            int cxBase = gx * cell, cyBase = gy * cell;
            if (mirror)
            {
                int ccx = cxBase + cell / 2, ccy = cyBase + cell / 2;
                if (ccy * Width + ccx >= (Height - 1 - ccy) * Width + (Width - 1 - ccx)) continue;   // 앞쪽 절반만 (중심 칸은 건너뜀)
            }
            if (H(gx, gy, 101) % 100 >= (uint)MathF.Min(100f, sr.ClusterPct * density)) continue;   // density 1 = 예전 그대로
            // 군집 중심: 칸 묶음 안 후보 8개 중 숲 가장자리를 우선
            int fx = -1, fy = -1, ox = -1, oy = -1;
            for (int k = 0; k < 8; k++)
            {
                int x = cxBase + (int)(H(gx, gy, 200 + k) % (uint)cell), y = cyBase + (int)(H(gx, gy, 220 + k) % (uint)cell);
                if (!Plantable(x, y)) continue;
                if (NearForest(x, y)) { if (fx < 0) { fx = x; fy = y; } }
                else if (ox < 0) { ox = x; oy = y; }
            }
            bool forestCluster = fx >= 0 && H(gx, gy, 102) % 100 < (uint)sr.ForestEdgePct;
            bool fieldCluster = !forestCluster && ox >= 0 && H(gx, gy, 103) % 100 < (uint)MathF.Min(100f, sr.FieldPct * density);   // density 1 = 예전 그대로
            if (!forestCluster && !fieldCluster) continue;
            int cx = forestCluster ? fx : ox, cy = forestCluster ? fy : oy;
            int count = sr.ClusterMin + (int)(H(gx, gy, 104) % (uint)Math.Max(1, sr.ClusterMax - sr.ClusterMin + 1));
            int species = (int)(H(gx, gy, 105) % (uint)TreeKinds.Length);
            for (int j = 0; j < count; j++)
            {
                float dx = ((int)(H(gx, gy, 300 + j) % 2001) - 1000) / 1000f * sr.ClusterRadius;
                float dy = ((int)(H(gx, gy, 330 + j) % 2001) - 1000) / 1000f * sr.ClusterRadius;
                float x = cx + 0.5f + dx, y = cy + 0.5f + dy;
                uint roll = H(gx, gy, 360 + j) % 100;
                string type;
                if (forestCluster)
                {
                    if (roll < (uint)sr.RockPct / 3) type = "rock_s";
                    else type = TreeKinds[(H(gx, gy, 390 + j) % 4 < 3) ? species : (int)(H(gx, gy, 410 + j) % (uint)TreeKinds.Length)];
                }
                else
                {
                    if (j == count - 1 && H(gx, gy, 106) % 100 < 30) type = TreeKinds[species];
                    else if (roll < (uint)sr.BigRockPct / 2) type = "rock_l";
                    else if (roll < (uint)sr.BigRockPct / 2 + 30) type = "rock_m";
                    else type = "rock_s";
                }
                if (!Put(x, y, type)) continue;
                if (mirror) Put(Width - x, Height - y, type);
            }
        }
        return PropSeeds.Count - before;
    }
}
