using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 타일 맵. 좌표계: 타일 (x, y) 는 월드 좌표 [x, x+1) × [y, y+1) 를 차지한다 (월드 단위 = 타일).
/// </summary>
public readonly record struct PropSeed(float X, float Y, string Type);

public sealed partial class GameMap
{
    public readonly int Width, Height, Seed;
    /// <summary>시작 위치 수 (= 세력 수).</summary>
    public int Players => StartTiles.Count;
    /// <summary>고정 지도 파일 id (assets/maps/&lt;id&gt;.json). 생성 맵이면 null.</summary>
    public string? Id { get; internal set; }
    public string? DisplayName { get; internal set; }
    public string? FileHash { get; internal set; }
    /// <summary>2인 생성 맵 (180° 회전 대칭) — 세력 2 사령부를 세력 1 의 정확한 대칭으로 놓는다.</summary>
    public bool Mirrored { get; private set; }
    readonly TerrainType[] tiles;
    readonly bool[] walkable;          // 지형 기준
    readonly int[] occupant;           // 타일을 차지한 건물 Id (0 = 없음) — 건물 예약 표시 (배치 검사용)
    [SnapExt] bool[]? propBlock;                 // F6 소품 발자국 (통행 불가, 지도 고정)
    readonly bool[] passable;          // 점유 건물이 통행 가능(도시 · 소도시) — 점유(예약)는 하되 막지는 않는다

    /// <summary>통행 정보가 바뀔 때마다 증가 (건물 건설/파괴). 흐름장 재계산 판단용.</summary>
    public int Version { get; private set; }

    /// <summary>세력별 시작 타일 (index 0 = 세력 1).</summary>
    public readonly List<(int X, int Y)> StartTiles = new();
    /// <summary>점령 시설 배치 (좌상단 타일, 시작 소유 세력 0=중립, 건물 키). 크기는 건물 정의(SiteSize)를 따른다.</summary>
    public readonly List<(int X, int Y, int Owner, string Kind)> ResourceSites = new();
    /// <summary>지도 파일이 준 병력 배치 (세력 · 위치). 비어 있으면 World.CreateStandard 가 기본 병력(start.force)을 준다.</summary>
    public readonly List<UnitSpawn> UnitSpawns = new();
    /// <summary>점령 시설 종류의 한 변 (타일). 정의가 없으면 3.</summary>
    public static int SiteSize(string kind) => Defs.Current != null && Defs.Current.Buildings.TryGetValue(kind, out var d) ? d.Size : 3;
    static (int X, int Y) SiteCenterTile((int X, int Y, int Owner, string Kind) r) => (r.X + SiteSize(r.Kind) / 2, r.Y + SiteSize(r.Kind) / 2);
    const float MinSiteGap = 10f;     // 시설 중심끼리 최소 거리 (점령 반경이 겹치지 않게)
    const float MinStartGap = 9f;     // 시작점(대도시)과 시설 최소 거리

    public (int X, int Y) StartTile => StartTiles.Count > 0 ? StartTiles[0] : (Width / 2, Height / 2);

    public GameMap(int width, int height, int seed = 0)
    {
        Width = width;
        Height = height;
        Seed = seed;
        tiles = new TerrainType[width * height];
        walkable = new bool[width * height];
        occupant = new int[width * height];
        passable = new bool[width * height];
        high = new bool[width * height];
        Array.Fill(walkable, true);
    }

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;
    public TerrainType Get(int x, int y) => tiles[y * Width + x];
    public bool IsWalkable(int x, int y) => InBounds(x, y) && walkable[y * Width + x] && (occupant[y * Width + x] == 0 || passable[y * Width + x]);
    /// <summary>통행은 되지만 건물이 점유한 타일 (도시 발자국). 그 위의 유닛은 충돌 반경이 2배.</summary>
    public bool IsWalkOnOccupied(int x, int y) => InBounds(x, y) && passable[y * Width + x] && occupant[y * Width + x] != 0;
    /// <summary>점령 시설을 새로 놓을 수 있는 타일인지 (지형 통행 가능 + 어떤 건물도 점유하지 않음 — 도시 발자국은 통행은 되지만 예약돼 있다).</summary>
    public bool IsBuildable(int x, int y) => InBounds(x, y) && walkable[y * Width + x] && occupant[y * Width + x] == 0;
    public bool IsTerrainWalkable(int x, int y) => InBounds(x, y) && walkable[y * Width + x];
    public int OccupantAt(int x, int y) => InBounds(x, y) ? occupant[y * Width + x] : 0;

    /// <summary>F6 소품 자리 (고정 배치; 상태는 World.Props). X · Y = 칸 좌표 실수.</summary>
    [SnapExt] public readonly List<PropSeed> PropSeeds = new();

    /// <summary>소품을 지도에 놓는다. blocksMove 면 그 칸을 통행 불가로 (이미 막혔거나 지형이 안 걷는 칸이면 막기만 건너뜀).</summary>
    public void AddPropSeed(float x, float y, string type, bool blocks)
    {
        PropSeeds.Add(new PropSeed(x, y, type));
        if (!blocks) return;
        int tx = (int)MathF.Floor(x), ty = (int)MathF.Floor(y);
        if (!InBounds(tx, ty)) return;
        propBlock ??= new bool[Width * Height];
        int i = ty * Width + tx;
        if (propBlock[i]) return;
        propBlock[i] = true;
        walkable[i] = false;
        Version++;
        ChangeLog.Add((tx, ty, 1, 1));
    }

    public bool IsPropBlocked(int x, int y) => propBlock != null && InBounds(x, y) && propBlock[y * Width + x];

    public void Set(int x, int y, TerrainType t)
    {
        int i = y * Width + x;
        var old = tiles[i];
        tiles[i] = t;
        walkable[i] = Terrains.Get(t).Walkable && (propBlock == null || !propBlock[i]);
        // N16: 숲 깊이 · 가옥 옆 표시는 숲 · 가옥 칸에만 의존 — 둘 다 아닌 칸끼리의 변경 (포탄 구덩이 등) 은 지도 전체 재계산을 부르지 않는다
        if (old != t && (old is TerrainType.Forest or TerrainType.House || t is TerrainType.Forest or TerrainType.House)) terrainDirty = true;
        if (old != t && (old == TerrainType.Trench || t == TerrainType.Trench)) TrenchVersion++;
        terrainHashDirty = true;
        TerrainVersion++;
    }

    /// <summary>건물 영역 점유/해제 (id = 0 이면 해제). walkOn=true 면 점유(예약)만 하고 통행은 막지 않는다 (도시).</summary>
    public void SetOccupant(int tx, int ty, int size, int id, bool walkOn = false)
    {
        for (int y = ty; y < ty + size; y++)
        for (int x = tx; x < tx + size; x++)
            if (InBounds(x, y)) { occupant[y * Width + x] = id; passable[y * Width + x] = id != 0 && walkOn; }
        Version++;
        if (!(walkOn && id != 0)) ChangeLog.Add((tx, ty, size, size));   // 도시를 놓는 것은 통행 정보를 바꾸지 않는다 (해제는 보수적으로 기록)
    }

    /// <summary>통행 정보가 바뀐 사각형(타일) 기록 — 계층 길찾기가 각자 읽은 위치부터 이어서 읽는다 (여러 World 가 한 맵을 공유해도 안전).</summary>
    internal readonly List<(int X, int Y, int W, int H)> ChangeLog = new();

    /// <summary>size×size 영역이 지형상 건설 가능하고 비어 있는지.</summary>
    public bool CanPlace(int tx, int ty, int size)
    {
        for (int y = ty; y < ty + size; y++)
        for (int x = tx; x < tx + size; x++)
            if (!IsBuildable(x, y)) return false;
        return true;
    }

    public static (int X, int Y) TileOf(Vector2 p) => ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));

    public (int X, int Y) NearestWalkable(int x, int y, int maxRadius = 64)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        if (IsWalkable(x, y)) return (x, y);
        for (int r = 1; r <= maxRadius; r++)
        {
            int best = int.MaxValue, bx = 0, by = 0;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !IsWalkable(x + dx, y + dy)) continue;
                int d = dx * dx + dy * dy;
                if (d < best) (best, bx, by) = (d, x + dx, y + dy);
            }
            if (best != int.MaxValue) return (bx, by);
        }
        return StartTile;
    }

    /// <summary>점 p 를 자신이 속한 타일 안으로, 막힌 이웃 쪽은 margin 만큼 떨어뜨려 고정.</summary>
    public Vector2 ClampIntoTile(Vector2 p, int tx, int ty, float margin)
    {
        float lx = tx + (IsWalkable(tx - 1, ty) ? 0.001f : margin);
        float hx = tx + 1 - (IsWalkable(tx + 1, ty) ? 0.001f : margin);
        float ly = ty + (IsWalkable(tx, ty - 1) ? 0.001f : margin);
        float hy = ty + 1 - (IsWalkable(tx, ty + 1) ? 0.001f : margin);
        return new Vector2(Math.Clamp(p.X, lx, hx), Math.Clamp(p.Y, ly, hy));
    }

    /// <summary>
    /// 타일 (x,y) 중심에서 가장 가까운 막힌 타일 가장자리까지의 거리 (max 이상이면 max). 타일 자신이 막혔으면 0. 맵 밖은 막힌 것으로 보지 않는다.
    /// window = 살펴볼 칸 반경 (기본: max 에 맞춤). 큰 유닛은 이 값이 반지름 이상인 칸만 지나간다 (Nav 의 큰 그래프).
    /// </summary>
    public float TileClearance(int x, int y, float max = 3f, int window = -1)
    {
        if (!IsWalkable(x, y)) return 0;
        int R = window >= 0 ? window : (int)MathF.Ceiling(max + 0.5f) - 1;
        float best = max;
        for (int dy = -R; dy <= R; dy++)
        for (int dx = -R; dx <= R; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            if (!InBounds(x + dx, y + dy) || IsWalkable(x + dx, y + dy)) continue;
            float ex = MathF.Max(0, MathF.Abs(dx) - 0.5f), ey = MathF.Max(0, MathF.Abs(dy) - 0.5f);
            float d = MathF.Sqrt(ex * ex + ey * ey);
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>두 점 사이 직선이 막힌 타일을 지나지 않는지 (격자 DDA).</summary>
    public bool HasLineOfSight(Vector2 a, Vector2 b) => HasLineOfSight(a, b, 0, 0);

    /// <summary>
    /// 사선 검사 (직사 무기): ignoreA · ignoreB 가 점유한 칸(사격자 · 표적 건물 자신의 발자국)은 막힌 것으로 치지 않는다. 0 = 없음.
    /// 지형이 통행 불가인 칸은 항상 막는다.
    /// </summary>
    public bool HasLineOfSight(Vector2 a, Vector2 b, int ignoreA, int ignoreB)
    {
        bool Clear(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            int i = y * Width + x;
            if (!walkable[i]) return false;
            int o = occupant[i];
            return o == 0 || passable[i] || o == ignoreA || o == ignoreB;   // o ≠ 0 이므로 ignore 0 은 아무것도 통과시키지 않는다
        }
        var (x, y) = TileOf(a);
        var (ex, ey) = TileOf(b);
        if (!Clear(x, y)) return false;
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
                // 정확히 모서리를 지나는 경우: 양 옆이 모두 열려 있어야 통과
                if (!Clear(x + stepX, y) || !Clear(x, y + stepY)) return false;
                x += stepX; y += stepY; tMaxX += tDeltaX; tMaxY += tDeltaY;
            }
            if (!Clear(x, y)) return false;
        }
        return true;
    }

    /// <summary>반지름 radius 의 몸이 a→b 직선으로 막힘 없이 지나갈 수 있는지 (중심선 + 양옆 평행선 DDA).</summary>
    public bool ClearLine(Vector2 a, Vector2 b, float radius)
    {
        if (!HasLineOfSight(a, b)) return false;
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 1e-4f || radius < 0.05f) return true;
        Vector2 off = new Vector2(-d.Y, d.X) / len * radius;   // 몸 전체 폭 (큰 유닛은 좁은 틈에 걸리지 않도록 반지름 그대로)
        return HasLineOfSight(a + off, b + off) && HasLineOfSight(a - off, b - off);
    }

    // ---------------------------------------------------------------- 생성

    /// <summary>
    /// 맵 생성. 2인 = 180° 회전 대칭 맵, 3인 이상 = 원형 배치 맵 (GenerateRadial).
    /// 세력마다 대도시 1(시작 소유) + 본진 근처 마을 3, 이웃과의 경계에 소도시 · 마을, 안쪽 고리의 마을.
    /// 모든 시작점 · 시설이 서로 연결되도록 보정한다. 간격이 모자라는 후보는 건너뛴다 (작은 맵).
    /// </summary>
    public static GameMap Generate(int width, int height, int seed, int players = 2)
    {
        if (players > 2) return GenerateRadial(width, height, seed, players);
        var map = new GameMap(width, height, seed) { Mirrored = true };
        var rng = new Random(seed);
        float[] elevation = Fractal(width, height, rng);
        float[] moisture = Fractal(width, height, rng);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            // 절반만 생성하고 나머지는 회전 복사
            int mx = width - 1 - x, my = height - 1 - y;
            bool firstHalf = y * width + x <= my * width + mx;
            int i = firstHalf ? y * width + x : my * width + mx;
            float e = elevation[i], m = moisture[i];
            TerrainType t = e < 0.26f ? TerrainType.Water
                : e < 0.32f ? TerrainType.Sand
                : e > 0.80f ? TerrainType.Rock
                : m > 0.68f ? TerrainType.Dirt
                : TerrainType.Grass;
            // 흩어진 바위도 대칭이 되도록 좌표 해시 사용
            uint h = (uint)(Math.Min(i, my * width + mx) * 2654435761u) ^ (uint)seed;
            if (t == TerrainType.Grass && h % 1000 < 6) t = TerrainType.Rock;
            map.Set(x, y, t);
        }

        // 배치 (맵 비율 기준). 세력 2 위치는 세력 1 의 회전 대칭.
        var start = map.Place(0.12f, 0.88f);
        map.StartTiles.Add(start);
        map.StartTiles.Add((width - 1 - start.X, height - 1 - start.Y));
        var mid = new Vector2(width / 2f, height / 2f);
        Vector2 F(float fx, float fy) => new(fx * width, fy * height);
        // 우선순위 순: 본진 근처 → 경계(좌상단/우하단 모서리, 두 본진에서 같은 거리) → 안쪽 → 중앙
        foreach (var (kind, c) in NearBase(new Vector2(start.X, start.Y), mid)) map.AddMirrored(kind, c);
        map.AddMirrored("city", F(0.14f, 0.14f));
        map.AddMirrored("village", F(0.28f, 0.28f));
        map.AddMirrored("village", F(0.13f, 0.50f));
        map.AddMirrored("village", F(0.30f, 0.50f));
        map.AddMirrored("village", F(0.38f, 0.66f));
        map.AddMirrored("city", mid);                       // 중앙 경합 (홀수 크기 맵에서는 대칭이 안 맞아 건너뜀)

        foreach (var (sx, sy) in map.StartTiles) map.ClearCircle(sx, sy, 8);
        foreach (var r in map.ResourceSites) map.ClearSite(r);
        map.SprinkleForestsAndHills(mirror: true);
        map.EnsureConnected();
        map.ScatterProps(mirror: true);   // F6 엄폐 소품 (GameMapProps.cs)
        return map;
    }

    /// <summary>
    /// N인 원형 배치: 맵 중심 둘레 원 위에 시작점을 같은 각도 간격으로 놓는다 (세력 1 = 좌하단부터).
    /// 세력마다: 본진 근처(약 11타일) 마을 2, 안쪽 약 19타일 마을, 이웃과의 경계에 소도시 · 마을, 안쪽 고리에 마을.
    /// 지형은 대칭이 아니다 (공정성은 배치 간격으로 맞추고, 나중에 직접 그린 지도로 보완).
    /// </summary>
    static GameMap GenerateRadial(int width, int height, int seed, int players)
    {
        var map = new GameMap(width, height, seed);
        var rng = new Random(seed);
        float[] elevation = Fractal(width, height, rng);
        float[] moisture = Fractal(width, height, rng);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            float e = elevation[i], m = moisture[i];
            TerrainType t = e < 0.26f ? TerrainType.Water
                : e < 0.32f ? TerrainType.Sand
                : e > 0.80f ? TerrainType.Rock
                : m > 0.68f ? TerrainType.Dirt
                : TerrainType.Grass;
            if (t == TerrainType.Grass && rng.Next(1000) < 6) t = TerrainType.Rock;
            map.Set(x, y, t);
        }

        var c = new Vector2(width / 2f, height / 2f);
        float radius = MathF.Min(width, height) * 0.38f;
        float step = MathF.Tau / players;
        Vector2 Polar(float angle, float r) => c + new Vector2(DetMath.Cos(angle), DetMath.Sin(angle)) * r;
        float AngleOf(int k) => 0.75f * MathF.PI + k * step;     // 세력 1 = 좌하단 (화면 좌표, y 아래)
        for (int k = 0; k < players; k++)
        {
            var (sx, sy) = map.Place((c.X + DetMath.Cos(AngleOf(k)) * radius) / width, (c.Y + DetMath.Sin(AngleOf(k)) * radius) / height);
            map.StartTiles.Add((sx, sy));
        }
        // 우선순위: 본진 근처 → 이웃 경계 (소도시 · 마을) → 안쪽 고리 (마을) → 안쪽 둘째 고리
        for (int k = 0; k < players; k++)
            foreach (var (kind, pos) in NearBase(new Vector2(map.StartTiles[k].X, map.StartTiles[k].Y), c)) map.AddSite(kind, pos);
        for (int k = 0; k < players; k++) map.AddSite("city", Polar(AngleOf(k) + step / 2, radius));
        for (int k = 0; k < players; k++) map.AddSite("village", Polar(AngleOf(k) + step / 2, radius - 19.5f));
        string[] ring = { "village", "village", "village" };
        for (int k = 0; k < players; k++) map.AddSite(ring[k % 3], Polar(AngleOf(k) + step / 2, radius * 0.5f));
        if (MathF.PI * radius * 0.5f / players >= 18f)
            for (int k = 0; k < players; k++) map.AddSite(ring[(k + 1) % 3], Polar(AngleOf(k), radius * 0.5f));
        foreach (var (sx, sy) in map.StartTiles) map.ClearCircle(sx, sy, 8);
        foreach (var r in map.ResourceSites) map.ClearSite(r);
        map.SprinkleForestsAndHills(mirror: false);
        map.EnsureConnected(mirror: false);
        map.ScatterProps(mirror: false);
        return map;
    }

    /// <summary>본진 근처 마을 3개 (약 16.5타일, 중심 방향에서 ±40° 둘 + 중심 방향 약 28.5타일).</summary>
    static IEnumerable<(string Kind, Vector2 Center)> NearBase(Vector2 start, Vector2 mapCenter)
    {
        var n = Vector2.Normalize(mapCenter - start);
        var t = new Vector2(-n.Y, n.X);
        const float cos40 = 0.766f, sin40 = 0.643f;
        yield return ("village", start + (n * cos40 + t * sin40) * 16.5f);   // 2026-10-19 지도 ×1.5 (11 → 16.5, 19 → 28.5)
        yield return ("village", start + (n * cos40 - t * sin40) * 16.5f);
        yield return ("village", start + n * 28.5f);
    }

    /// <summary>중심점(월드 좌표)을 시설 크기에 맞는 좌상단 타일로 (맵 안으로 고정).</summary>
    (int X, int Y) SiteTopLeft(Vector2 center, int size) =>
        (Math.Clamp((int)MathF.Round(center.X - size / 2f), 1, Width - size - 1), Math.Clamp((int)MathF.Round(center.Y - size / 2f), 1, Height - size - 1));

    /// <summary>다른 시설 · 시작점과 간격이 충분한지.</summary>
    bool SiteFree(int tx, int ty, int size)
    {
        var c = new Vector2(tx + size / 2f, ty + size / 2f);
        foreach (var r in ResourceSites)
            if (Vector2.Distance(c, new Vector2(r.X + SiteSize(r.Kind) / 2f, r.Y + SiteSize(r.Kind) / 2f)) < MinSiteGap) return false;
        foreach (var (sx, sy) in StartTiles)
            if (Vector2.Distance(c, new Vector2(sx, sy)) < MinStartGap) return false;
        return true;
    }

    void AddSite(string kind, Vector2 center)
    {
        int size = SiteSize(kind);
        var (tx, ty) = SiteTopLeft(center, size);
        if (SiteFree(tx, ty, size)) ResourceSites.Add((tx, ty, 0, kind));
    }

    /// <summary>180° 회전 대칭 쌍으로 추가 (둘 다 들어갈 때만). 자기 자신과 대칭이면 하나만.</summary>
    void AddMirrored(string kind, Vector2 center)
    {
        int size = SiteSize(kind);
        var (tx, ty) = SiteTopLeft(center, size);
        int mx = Width - tx - size, my = Height - ty - size;
        bool self = mx == tx && my == ty;
        if (!SiteFree(tx, ty, size)) return;
        if (!self && Vector2.Distance(new Vector2(tx, ty), new Vector2(mx, my)) < MinSiteGap) return;
        ResourceSites.Add((tx, ty, 0, kind));
        if (!self) ResourceSites.Add((mx, my, 0, kind));
    }

    /// <summary>시설 주변을 평지로 (본체 + 점령 반경 안쪽). 발자국 중심(짝수 크기면 타일 꼭짓점) 기준이라 180° 회전해도 대칭이다.</summary>
    void ClearSite((int X, int Y, int Owner, string Kind) r)
    {
        int size = SiteSize(r.Kind);
        float cx = r.X + size / 2f, cy = r.Y + size / 2f, rad = size / 2f + 2.5f;
        for (int y = (int)MathF.Floor(cy - rad); y <= (int)MathF.Ceiling(cy + rad); y++)
        for (int x = (int)MathF.Floor(cx - rad); x <= (int)MathF.Ceiling(cx + rad); x++)
            if (InBounds(x, y) && (x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= rad * rad)
                Set(x, y, TerrainType.Grass);
    }

    (int X, int Y) Place(float fx, float fy) =>
        (Math.Clamp((int)(fx * Width), 5, Width - 6), Math.Clamp((int)(fy * Height), 5, Height - 6));

    void ClearCircle(int cx, int cy, int r)
    {
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
            if (dx * dx + dy * dy <= r * r && InBounds(cx + dx, cy + dy))
                Set(cx + dx, cy + dy, TerrainType.Grass);
    }

    /// <summary>모든 시작점 · 시설이 세력 1 시작점과 연결되도록 끊긴 곳에 평지 길을 낸다 (mirror: 회전 대칭으로도).</summary>
    void EnsureConnected(bool mirror = true)
    {
        var targets = StartTiles.Concat(ResourceSites.Select(SiteCenterTile)).ToList();
        for (int attempt = 0; attempt < targets.Count; attempt++)
        {
            var (sx, sy) = StartTiles[0];
            var field = new FlowField(this, new Vector2(sx + 0.5f, sy + 0.5f));
            var cut = targets.FirstOrDefault(t => !field.IsReachable(t.Item1, t.Item2), (-1, -1));
            if (cut.Item1 < 0) return;
            CarveLine(cut.Item1, cut.Item2, sx, sy);
            if (mirror) CarveLine(Width - 1 - cut.Item1, Height - 1 - cut.Item2, Width - 1 - sx, Height - 1 - sy);
        }
    }

    void CarveLine(int x0, int y0, int x1, int y1)
    {
        int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (int i = 0; i <= steps; i++)
        {
            int x = x0 + (x1 - x0) * i / Math.Max(steps, 1), y = y0 + (y1 - y0) * i / Math.Max(steps, 1);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (InBounds(x + dx, y + dy)) Set(x + dx, y + dy, TerrainType.Dirt);
        }
    }

    static float[] Fractal(int w, int h, Random rng)
    {
        var total = new float[w * h];
        foreach (var (cell, weight) in new[] { (14, 0.6f), (7, 0.28f), (3, 0.12f) })
        {
            float[] layer = ValueNoise(w, h, cell, rng);
            for (int i = 0; i < total.Length; i++) total[i] += layer[i] * weight;
        }
        float min = total.Min(), max = total.Max(), range = MathF.Max(max - min, 1e-6f);
        for (int i = 0; i < total.Length; i++) total[i] = (total[i] - min) / range;
        return total;
    }

    static float[] ValueNoise(int w, int h, int cell, Random rng)
    {
        int gw = w / cell + 2, gh = h / cell + 2;
        var grid = new float[gw * gh];
        for (int i = 0; i < grid.Length; i++) grid[i] = rng.NextSingle();
        var outp = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            float fy = (float)y / cell;
            int iy = (int)fy;
            float ty = fy - iy;
            ty = ty * ty * (3 - 2 * ty);
            for (int x = 0; x < w; x++)
            {
                float fx = (float)x / cell;
                int ix = (int)fx;
                float tx = fx - ix;
                tx = tx * tx * (3 - 2 * tx);
                float g00 = grid[iy * gw + ix], g10 = grid[iy * gw + ix + 1];
                float g01 = grid[(iy + 1) * gw + ix], g11 = grid[(iy + 1) * gw + ix + 1];
                float a = g00 + (g10 - g00) * tx, b = g01 + (g11 - g01) * tx;
                outp[y * w + x] = a + (b - a) * ty;
            }
        }
        return outp;
    }
}

/// <summary>지도가 준 유닛 1기 배치.</summary>
public readonly record struct UnitSpawn(string Unit, int Faction, Vector2 Pos);
