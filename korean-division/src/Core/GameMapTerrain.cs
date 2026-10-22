using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 지형 시뮬 (ROADMAP '지형 · 높이'): 고지 (칸마다 bool) · 지형별 이동 배수 · 엄폐 · 숲 깊이 · 가옥 옆 · 경사, 직사 사선 (시야 규칙 포함).
/// 구조 정보 (숲 깊이 · 가옥 옆 · 경사 · 지형 해시) 는 지형이 바뀐 뒤 처음 물을 때 다시 만든다 (지도는 생성 · 불러오기 때만 바뀐다).
/// 수치는 balance.json "terrain" 을 물을 때마다 읽는다.
/// </summary>
public sealed partial class GameMap
{
    readonly bool[] high;
    volatile bool terrainDirty = true;       // 구조 정보 (숲 깊이 · 칸 표시) 재계산 필요
    volatile bool terrainHashDirty = true;   // 지형 해시 재계산 필요 (지형 · 고지가 바뀐 모든 경우)
    /// <summary>지형 · 고지가 바뀔 때마다 오른다 (파생 캐시 무효화 열쇠 — 전체 해시를 다시 계산하지 않는다). 시뮬 상태 아님 (저장 · 체크섬 제외).</summary>
    public int TerrainVersion { get; private set; }
    /// <summary>참호 칸이 생기거나 없어질 때마다 오른다 (참호 묶음 캐시 열쇠).</summary>
    public int TrenchVersion { get; private set; }
    readonly object terrainLock = new();
    byte[] forestDepth = Array.Empty<byte>();   // 숲 칸: 가장 가까운 숲 아닌 칸까지 체비셰프 거리 (1 = 가장자리), 숲 아님 = 0
    byte[] tileFlags = Array.Empty<byte>();     // FlagSlope | FlagHouseAdj
    ulong terrainHash;
    const byte FlagSlope = 1, FlagHouseAdj = 2;

    public bool IsHigh(int x, int y) => InBounds(x, y) && high[y * Width + x];
    public bool IsHigh(Vector2 p) { var (x, y) = TileOf(p); return IsHigh(x, y); }

    public void SetHigh(int x, int y, bool on)
    {
        if (!InBounds(x, y)) return;
        high[y * Width + x] = on;
        terrainDirty = true;
        terrainHashDirty = true;
        TerrainVersion++;
    }

    /// <summary>고지 칸이 하나라도 있는지 (저장 · 렌더 최적화용).</summary>
    public bool AnyHigh => Array.IndexOf(high, true) >= 0;

    /// <summary>지형 · 고지 내용 해시 (체크섬용 — 같은 시드 · 지도면 같은 값).</summary>
    public ulong TerrainHash { get { EnsureTerrainHash(); return terrainHash; } }

    void EnsureTerrainHash()
    {
        if (!terrainHashDirty) return;
        lock (terrainLock)
        {
            if (!terrainHashDirty) return;
            var h = 14695981039346656037UL;
            for (int i = 0, n = Width * Height; i < n; i++)
            {
                h = (h ^ (byte)tiles[i]) * 1099511628211UL;
                h = (h ^ (high[i] ? 1UL : 0UL)) * 1099511628211UL;
            }
            terrainHash = h;
            terrainHashDirty = false;
        }
    }

    void EnsureTerrain()
    {
        if (!terrainDirty) return;
        lock (terrainLock)
        {
            if (!terrainDirty) return;
            BuildTerrain();
            terrainDirty = false;
        }
    }

    void BuildTerrain()
    {
        int n = Width * Height;
        var depth = new byte[n];
        var flags = new byte[n];
        // 숲 깊이: 8방향 체비셰프 거리 변환 (두 번 훑기). 지도 밖은 숲 아님 (가장자리 칸 = 1).
        const int Inf = 250;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int i = y * Width + x;
            if (tiles[i] != TerrainType.Forest) continue;
            int d = Inf;
            if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1) d = 1;
            else
            {
                d = Math.Min(d, depth[i - 1] + 1);
                d = Math.Min(d, depth[i - Width] + 1);
                d = Math.Min(d, depth[i - Width - 1] + 1);
                d = Math.Min(d, depth[i - Width + 1] + 1);
            }
            depth[i] = (byte)d;
        }
        for (int y = Height - 1; y >= 0; y--)
        for (int x = Width - 1; x >= 0; x--)
        {
            int i = y * Width + x;
            if (depth[i] == 0) continue;
            int d = depth[i];
            if (x < Width - 1) d = Math.Min(d, depth[i + 1] + 1);
            if (y < Height - 1)
            {
                d = Math.Min(d, depth[i + Width] + 1);
                if (x > 0) d = Math.Min(d, depth[i + Width - 1] + 1);
                if (x < Width - 1) d = Math.Min(d, depth[i + Width + 1] + 1);
            }
            depth[i] = (byte)d;
        }
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int i = y * Width + x;
            byte f = 0;
            bool hi = high[i];
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (!InBounds(nx, ny)) continue;
                int j = ny * Width + nx;
                if (tiles[j] == TerrainType.House) f |= FlagHouseAdj;
                if (hi && !high[j] && (dx == 0 || dy == 0)) f |= FlagSlope;   // 평지와 4방향으로 맞닿은 고지 칸 = 경사
            }
            flags[i] = f;
        }
        forestDepth = depth;
        tileFlags = flags;
    }

    /// <summary>숲 깊이 (칸, 가장자리 = 1). 숲이 아니거나 지도 밖이면 0.</summary>
    public int ForestDepth(int x, int y) { if (!InBounds(x, y)) return 0; EnsureTerrain(); return forestDepth[y * Width + x]; }

    /// <summary>시야 · 사선을 막는 깊은 숲 칸인지 (깊이 &gt; terrain.forestSeeDepth).</summary>
    public bool IsDeepForest(int x, int y) => ForestDepth(x, y) > Defs.Current.Terrain.ForestSeeDepth;

    public bool IsSlope(int x, int y) { if (!InBounds(x, y)) return false; EnsureTerrain(); return (tileFlags[y * Width + x] & FlagSlope) != 0; }
    public bool IsHouseAdjacent(int x, int y) { if (!InBounds(x, y)) return false; EnsureTerrain(); return (tileFlags[y * Width + x] & FlagHouseAdj) != 0; }

    /// <summary>칸의 이동 속도 배수 (지형 × 경사). 지도 밖 1.</summary>
    public float MoveMul(Vector2 p)
    {
        var (x, y) = TileOf(p);
        if (!InBounds(x, y)) return 1f;
        EnsureTerrain();
        int i = y * Width + x;
        var r = Defs.Current.Terrain;
        float m = tiles[i] switch
        {
            TerrainType.Forest => r.ForestMove,
            TerrainType.Rubble => r.RubbleMove,
            TerrainType.Ford => r.FordMove,
            TerrainType.Wire => r.WireMove,
            TerrainType.Trench => r.TrenchMove,
            TerrainType.Crater => r.CraterMove,
            _ => 1f,
        };
        if ((tileFlags[i] & FlagSlope) != 0) m *= r.SlopeMove;
        return m;
    }

    /// <summary>칸이 주는 지형 엄폐 (맞을 확률 ×, 받는 제압 ×) — 숲 · 잔해 · 참호 · 가옥 옆 중 가장 좋은 값. 엄폐 없음 = (1, 1).</summary>
    public (float Hit, float Suppress) TerrainCover(Vector2 p)
    {
        var (x, y) = TileOf(p);
        if (!InBounds(x, y)) return (1f, 1f);
        EnsureTerrain();
        int i = y * Width + x;
        var r = Defs.Current.Terrain;
        (float hit, float sup) = tiles[i] switch
        {
            TerrainType.Forest => (r.ForestHit, r.ForestSuppress),
            TerrainType.Rubble => (r.RubbleHit, r.RubbleSuppress),
            TerrainType.Trench => (r.TrenchHit, r.TrenchSuppress),
            TerrainType.Crater => (r.CraterHit, r.CraterSuppress),
            _ => (1f, 1f),
        };
        if ((tileFlags[i] & FlagHouseAdj) != 0) { hit = MathF.Min(hit, r.HouseAdjHit); sup = MathF.Min(sup, r.HouseAdjSuppress); }
        return (hit, sup);
    }

    /// <summary>철조망 칸인지.</summary>
    public bool IsWire(Vector2 p) { var (x, y) = TileOf(p); return InBounds(x, y) && tiles[y * Width + x] == TerrainType.Wire; }

    /// <summary>
    /// 직사 사선 (무기 los): HasLineOfSight (통행 불가 지형 · 건물 점유 칸) 에 더해, 사이 칸 (양 끝 제외) 의 깊은 숲이 막고,
    /// 평지 사격자는 사이에 있는 고지 칸 너머를 못 본다 (첫 고지 칸 자체는 보인다). 고지 사격자는 높이 제약 없음.
    /// </summary>
    public bool HasFiringLine(Vector2 a, Vector2 b, int ignoreA, int ignoreB)
    {
        EnsureTerrain();
        int seeDepth = Defs.Current.Terrain.ForestSeeDepth;
        var (x, y) = TileOf(a);
        var (ex, ey) = TileOf(b);
        if (!ClearFor(x, y, ignoreA, ignoreB)) return false;
        bool shooterLow = !high[y * Width + x];
        bool passedHigh = false;
        Vector2 d = b - a;
        int stepX = Math.Sign(d.X), stepY = Math.Sign(d.Y);
        float tDeltaX = d.X != 0 ? MathF.Abs(1f / d.X) : float.MaxValue;
        float tDeltaY = d.Y != 0 ? MathF.Abs(1f / d.Y) : float.MaxValue;
        float tMaxX = d.X > 0 ? (x + 1 - a.X) * tDeltaX : d.X < 0 ? (a.X - x) * tDeltaX : float.MaxValue;
        float tMaxY = d.Y > 0 ? (y + 1 - a.Y) * tDeltaY : d.Y < 0 ? (a.Y - y) * tDeltaY : float.MaxValue;
        int guard = Math.Abs(ex - x) + Math.Abs(ey - y) + 2;
        while ((x != ex || y != ey) && guard-- > 0)
        {
            if (tMaxX < tMaxY) { x += stepX; tMaxX += tDeltaX; }
            else if (tMaxY < tMaxX) { y += stepY; tMaxY += tDeltaY; }
            else
            {
                if (!ClearFor(x + stepX, y, ignoreA, ignoreB) || !ClearFor(x, y + stepY, ignoreA, ignoreB)) return false;
                x += stepX; y += stepY; tMaxX += tDeltaX; tMaxY += tDeltaY;
            }
            if (!ClearFor(x, y, ignoreA, ignoreB)) return false;
            if (passedHigh) return false;                                  // 평지 사격자: 고지 칸 너머
            int i = y * Width + x;
            if ((x != ex || y != ey) && forestDepth[i] > seeDepth) return false;   // 깊은 숲 (사이 칸)
            if (shooterLow && high[i]) passedHigh = true;
        }
        return true;
    }

    bool ClearFor(int x, int y, int ignoreA, int ignoreB)
    {
        if (!InBounds(x, y)) return false;
        int i = y * Width + x;
        if (!walkable[i] && tiles[i] != TerrainType.Wire) return false;   // N38: 철조망은 통행만 막고 사선은 막지 않는다 (Terrain.cs) — 물 · 바위 · 집은 그대로 막음
        int o = occupant[i];
        return o == 0 || passable[i] || o == ignoreA || o == ignoreB;
    }

    // ---------------------------------------------------------------- 생성 맵 장식 (숲 · 언덕)

    /// <summary>
    /// 생성 맵에 숲 덩어리와 고지(언덕)를 결정적으로 흩뿌린다 (좌표 해시 + 시드, mirror 면 180° 회전 대칭).
    /// 풀 · 흙 칸에만, 시작점 · 시설 근처 (clear 반경) 는 비운다. 통행 불가 지형을 만들지 않으므로 연결성은 그대로다.
    /// </summary>
    internal void SprinkleForestsAndHills(bool mirror)
    {
        int seed = Seed;
        int cell = 18;
        int gw = Width / cell + 1, gh = Height / cell + 1;
        uint H(int a, int b, int salt)
        {
            uint v = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791) ^ (uint)seed * 2654435761u;
            v ^= v >> 13; v *= 0x5bd1e995; v ^= v >> 15;
            return v;
        }
        bool Protected(int x, int y)
        {
            foreach (var (sx, sy) in StartTiles)
                if ((x - sx) * (x - sx) + (y - sy) * (y - sy) <= 12 * 12) return true;
            foreach (var r in ResourceSites)
            {
                int s = SiteSize(r.Kind);
                float cx = r.X + s / 2f, cy = r.Y + s / 2f, rad = s / 2f + 4f;
                if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= rad * rad) return true;
            }
            return false;
        }
        void Blob(int cx, int cy, int r, bool forest)
        {
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!InBounds(x, y)) continue;
                int dx = x - cx, dy = y - cy;
                // 가장자리를 해시로 울퉁불퉁하게 (반경 ±1)
                int jitter = (int)(H(x, y, forest ? 11 : 12) % 3) - 1;
                if (dx * dx + dy * dy > (r + jitter) * (r + jitter)) continue;
                Apply(x, y, forest);
                if (mirror) Apply(Width - 1 - x, Height - 1 - y, forest);
            }
        }
        void Apply(int x, int y, bool forest)
        {
            if (!InBounds(x, y) || Protected(x, y)) return;
            var t = Get(x, y);
            if (t != TerrainType.Grass && t != TerrainType.Dirt) return;
            if (forest) Set(x, y, TerrainType.Forest);
            else SetHigh(x, y, true);
        }
        for (int gy = 0; gy < gh; gy++)
        for (int gx = 0; gx < gw; gx++)
        {
            int cxBase = gx * cell, cyBase = gy * cell;
            if (mirror)
            {
                // 절반만 만들고 회전 복사 (칸 중심이 지도 앞쪽 절반인 것만)
                int ccx = cxBase + cell / 2, ccy = cyBase + cell / 2;
                if (ccy * Width + ccx > (Height - 1 - ccy) * Width + (Width - 1 - ccx)) continue;
            }
            uint h = H(gx, gy, 1);
            int ox = cxBase + (int)(h % (uint)cell), oy = cyBase + (int)((h >> 8) % (uint)cell);
            uint kind = (h >> 16) % 100;
            if (kind < 20) Blob(ox, oy, 3 + (int)((h >> 24) % 4), forest: true);        // 숲 20%
            else if (kind < 38) Blob(ox, oy, 4 + (int)((h >> 24) % 3), forest: false);  // 언덕 18%
        }
    }
}
