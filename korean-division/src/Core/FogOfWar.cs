using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 전장의 안개 (세력별). 상태: 0 = 미탐색, 1 = 탐색됨(지형·마지막으로 본 건물만), 2 = 현재 시야.
/// 지형은 처음부터 전부 탐색됨(1) — 지도는 모두가 안다. 중립 시설(도시 · 마을)도 시작 시점의 모습으로 기억하고 시작한다.
/// 유닛과 지은 건물은 시야(2) 안에서만 보인다.
/// 갱신 (지형 · 높이, 2026-10-19): 세력마다 0.2초(Period = 12틱) 간격이되 세력별로 시차를 두어 (UpdateStaggered) 틱마다 일이 고르게 나뉜다.
/// 한 세력 갱신 = 자기 격자(own)를 탐색됨으로 내린 뒤 살아 있는 유닛·건물의 시야를 찍는다 — 지형 시야 (TerrainVision: 굵은 묶음 shadowcasting,
/// 깊은 숲 · 가옥 · 바위 · 평지에서 본 고지가 막음) 가 켜져 있으면 그 결과를, 꺼져 있으면 원을 찍는다. 같은 묶음 · 같거나 작은 반경의 시야원은
/// 한 번만 계산 (밀집한 중대는 점유한 묶음 수만큼만 = 중대 단위 집계). 동맹이 있으면 보이는 격자 = 자기 · 동맹들의 자기 격자 최댓값.
/// 남의 건물은 발자국이 보인 순간의 모습을 기억해 두었다가 시야 밖이면 고스트로 돌려준다.
/// </summary>
public sealed partial class FogOfWar
{
    /// <summary>세력 한 곳의 갱신 간격 (틱).</summary>
    public const int Period = 12;

    readonly World world;
    readonly int width, height;
    readonly byte[]?[] grids;                               // [세력] → 보이는 격자 (동맹 없으면 own 과 같은 배열)
    readonly byte[]?[] own;                                 // [세력] → 자기 시야 격자 (저장)
    readonly Dictionary<int, BuildingGhost>[] memory;       // [세력] → 건물 Id → 마지막으로 본 모습
    readonly Dictionary<int, int[]> circles = new();        // 반경 → dy 별 반폭 (길이 2r+1)
    readonly List<int> forget = new();
    TerrainVision? vision;                                  // 지형 시야 메모 (지형만의 순수 함수, 저장 안 함)
    readonly bool[] due;                                    // 작업용: 이번 갱신 대상 세력

    public bool Enabled = true;

    /// <summary>갱신마다 1 증가 (어느 세력이든).</summary>
    public int Version { get; private set; }

    readonly int[] fversion;   // [세력] 보이는 격자가 다시 계산될 때마다 1 증가 (화면용, 저장 안 함)
    /// <summary>세력의 보이는 격자 판 번호 — 렌더러 · 마지막 목격 표시가 다시 그릴지 판단한다 (시차 갱신이라 Version 은 거의 매 틱 바뀐다).</summary>
    public int VersionOf(int faction) => faction > 0 && faction < fversion.Length ? fversion[faction] : Version;

    public FogOfWar(World world)
    {
        this.world = world;
        width = world.Map.Width;
        height = world.Map.Height;
        int n = world.Factions.Count;
        grids = new byte[n][];
        own = new byte[n][];
        due = new bool[n];
        fversion = new int[n];
        memory = new Dictionary<int, BuildingGhost>[n];
        for (int f = 1; f < n; f++)
        {
            own[f] = new byte[width * height];
            Array.Fill(own[f]!, (byte)1);   // 지형은 처음부터 탐색됨
            grids[f] = own[f];
            memory[f] = new();
        }
    }

    /// <summary>지형 시야 계산기 (vision.terrainBlocking 이 꺼져 있으면 null). 처음 쓸 때 지도에서 만든다.</summary>
    internal TerrainVision? Vision
    {
        get
        {
            if (!world.Defs.Vision.TerrainBlocking) return null;
            return vision ??= new TerrainVision(world.Map, world.Defs.Vision.BlockSize);
        }
    }

    /// <summary>모든 세력을 지금 갱신 (시작 · 치트 · 테스트). 안개가 꺼져 있어도 계산해 두어 다시 켤 때 탐색 상태가 이어진다.</summary>
    /// <summary>시뮬 중 지형 변경 (World.MutateTerrain): 지형 시야 메모 갱신.</summary>
    internal void TerrainChanged(int x, int y) => vision?.Refresh(x, y);

    public void Update()
    {
        for (int f = 1; f < grids.Length; f++) due[f] = true;
        Run();
    }

    /// <summary>World.Step 이 매 틱 호출: 세력 f 는 (틱 − 시차(f)) 가 Period 의 배수일 때 갱신. 시차 = (f−1)·Period/(세력 수) — 세력 1 은 예전과 같은 틱.</summary>
    public void UpdateStaggered()
    {
        int n = grids.Length - 1;
        if (n <= 0) return;
        long t = world.Tick;
        bool any = false;
        for (int f = 1; f < grids.Length; f++)
        {
            int phase = (f - 1) * Period / n;
            due[f] = (t - phase) % Period == 0;
            any |= due[f];
        }
        if (any) Run();
    }

    void Run()
    {
        if (!seeded) SeedSites();
        Version++;
        for (int f = 1; f < grids.Length; f++)
        {
            if (!due[f]) continue;
            stampEpoch = (stampEpoch + 1) & 0xFFFFFF;
            if (stampEpoch == 0) { stampEpoch = 1; Array.Clear(stampMark); }
            var g = own[f]!;
            Decay(g);
            // 시야 2단계 (v0.4 §3): 평소 sightNear, 경계 상태(World.IsAlerted) 면 sightFar, × 역할 · 고지 (World.SightOf)
            foreach (var u in world.Units)
                if (u.Alive && u.Owner == f) Stamp(g, u.Pos, world.SightOf(u));
            foreach (var b in world.Buildings)
                if (b.Alive && b.Owner == f) Stamp(g, b.Pos, world.BuildingSight(b));
            foreach (var t in world.Trains)   // T30b 열차 객차 시야 (잔해 제외)
                foreach (var c in t.Cars)
                    if (c.Alive && c.Owner == f) Stamp(g, c.Pos, world.TrainCarSight(c));
        }
        bool shared = ShareAlliedVision();
        for (int f = 1; f < grids.Length; f++)
            if (due[f] || (shared && !ReferenceEquals(grids[f], own[f]))) { Remember(f, grids[f]!); fversion[f]++; }
        Array.Clear(due);
    }

    bool seeded;

    /// <summary>첫 갱신: 모든 세력이 점령 시설(지형지물)의 위치 · 시작 소유자를 안다. 이후 변화는 직접 봐야 갱신된다.</summary>
    void SeedSites()
    {
        seeded = true;
        foreach (var b in world.Buildings)
        {
            if (!b.Alive || !b.Def.Capturable) continue;
            for (int f = 1; f < grids.Length; f++)
                if (b.Owner != f) memory[f][b.Id] = new BuildingGhost(b.Id, b.Def, b.Owner, b.TileX, b.TileY);
        }
    }

    /// <summary>
    /// 동맹 시야 공유: 동맹이 있는 세력의 보이는 격자 = 자기 · 동맹들의 자기 격자 최댓값 (동맹의 동맹은 포함하지 않음).
    /// 이번 갱신 대상과 동맹인 세력만 다시 합친다. 동맹이 없으면 grids = own. 합친 세력이 있으면 true.
    /// </summary>
    bool ShareAlliedVision()
    {
        var d = world;   // 정적 팀 (WorldTeams.cs)
        bool did = false;
        int step = Vector<byte>.Count;
        for (int a = 1; a < grids.Length; a++)
        {
            bool hasAlly = false, touched = due[a];
            for (int b = 1; b < grids.Length; b++)
                if (b != a && d.Allied(a, b)) { hasAlly = true; touched |= due[b]; }
            if (!hasAlly) { grids[a] = own[a]; continue; }
            bool fresh = ReferenceEquals(grids[a], own[a]);
            if (!touched && !fresh) continue;
            if (fresh) grids[a] = new byte[width * height];
            var dst = grids[a]!.AsSpan();
            own[a]!.AsSpan().CopyTo(dst);
            for (int b = 1; b < grids.Length; b++)
            {
                if (b == a || !d.Allied(a, b)) continue;
                var src = own[b]!.AsSpan();
                int i = 0;
                for (; i <= dst.Length - step; i += step)
                    Vector.Max(new Vector<byte>(dst.Slice(i)), new Vector<byte>(src.Slice(i))).CopyTo(dst.Slice(i));
                for (; i < dst.Length; i++)
                    if (src[i] > dst[i]) dst[i] = src[i];
            }
            did = true;
        }
        return did;
    }

    /// <summary>불러온 뒤: 보이는 격자를 자기 격자들에서 다시 만든다 (저장 시점에도 항상 같은 식으로 합쳐져 있었다).</summary>
    void RebuildShared()
    {
        for (int f = 1; f < grids.Length; f++) { grids[f] = own[f]; due[f] = true; }
        ShareAlliedVision();
        Array.Clear(due);
        for (int f = 1; f < fversion.Length; f++) fversion[f]++;
    }

    /// <summary>시야(2) → 탐색됨(1). 값이 0·1·2 뿐이므로 Min(v, 1) 로 벡터 처리.</summary>
    static void Decay(byte[] grid)
    {
        var span = grid.AsSpan();
        var one = new Vector<byte>(1);
        int i = 0, step = Vector<byte>.Count;
        for (; i <= span.Length - step; i += step)
            Vector.Min(new Vector<byte>(span.Slice(i)), one).CopyTo(span.Slice(i));
        for (; i < span.Length; i++)
            if (span[i] == 2) span[i] = 1;
    }

    // 같은 갱신 안에서 같은 칸 (지형 시야는 같은 묶음) · 같거나 작은 반경은 이미 찍힌 것의 부분집합 → 건너뛴다 (밀집 부대 · 중대 중복 제거). 결과는 그대로.
    uint[] stampMark = Array.Empty<uint>();
    uint stampEpoch;

    void Stamp(byte[] grid, Vector2 center, int radius)
    {
        if (radius < 0) return;
        int cx = (int)MathF.Floor(center.X), cy = (int)MathF.Floor(center.Y);
        bool inMap = (uint)cx < (uint)width && (uint)cy < (uint)height;
        var tv = Vision;
        if (inMap)
        {
            int cell = tv != null ? (cy / tv.Bs) * tv.Bw + cx / tv.Bs : cy * width + cx;
            if (stampMark.Length != width * height) stampMark = new uint[width * height];
            if (radius < 126)
            {
                uint r1 = (uint)radius + 1, hb = tv != null && world.Map.IsHigh(cx, cy) ? 1u : 0u;   // 같은 묶음이라도 고지/평지 관측자는 결과가 다르다
                ref uint m = ref stampMark[cell];
                if (m >> 8 == stampEpoch && (m & 1) == hb && ((m >> 1) & 127) >= r1) return;
                m = (stampEpoch << 8) | (r1 << 1) | hb;
            }
        }
        if (tv != null)
        {
            if (inMap) tv.Stamp(grid, cx, cy, radius);
            return;
        }
        int[] half = Circle(radius);
        int y0 = Math.Max(0, cy - radius), y1 = Math.Min(height - 1, cy + radius);
        for (int y = y0; y <= y1; y++)
        {
            int hw = half[y - cy + radius];
            int x0 = Math.Max(0, cx - hw), x1 = Math.Min(width - 1, cx + hw);
            if (x1 >= x0) Array.Fill(grid, (byte)2, y * width + x0, x1 - x0 + 1);
        }
    }

    /// <summary>반경 r 원의 dy 별 반폭. 타일 중심 기준이라 0.5 를 더해 모서리가 덜 각지게.</summary>
    int[] Circle(int r)
    {
        if (circles.TryGetValue(r, out var table)) return table;
        table = new int[2 * r + 1];
        float rr = (r + 0.5f) * (r + 0.5f);
        for (int dy = -r; dy <= r; dy++) table[dy + r] = (int)MathF.Sqrt(rr - dy * dy);
        circles[r] = table;
        return table;
    }

    /// <summary>발자국이 보이는 남의 건물은 기억 갱신, 자리가 보이는데 사라진 건물은 잊는다.</summary>
    void Remember(int faction, byte[] grid)
    {
        var mem = memory[faction];
        foreach (var b in world.Buildings)
        {
            if (!b.Alive) continue;
            if (b.Owner == faction) { mem.Remove(b.Id); continue; }
            if (Footprint(grid, b.TileX, b.TileY, b.Size))
                mem[b.Id] = new BuildingGhost(b.Id, b.Def, b.Owner, b.TileX, b.TileY);
        }
        forget.Clear();
        foreach (var g in mem.Values)
            if (world.Find(g.Id) is not Building && Footprint(grid, g.TileX, g.TileY, g.Def.Size)) forget.Add(g.Id);
        foreach (int id in forget) mem.Remove(id);
    }

    bool Footprint(byte[] grid, int tx, int ty, int size)
    {
        int x0 = Math.Max(0, tx), x1 = Math.Min(width - 1, tx + size - 1);
        int y0 = Math.Max(0, ty), y1 = Math.Min(height - 1, ty + size - 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (grid[y * width + x] == 2) return true;
        return false;
    }

    byte[]? GridOf(int faction) => faction > 0 && faction < grids.Length ? grids[faction] : null;

    /// <summary>세력의 격자 (y*Width+x). 중립이거나 없는 세력이면 비어 있음. 렌더러용.</summary>
    public ReadOnlySpan<byte> Grid(int faction) => GridOf(faction) ?? ReadOnlySpan<byte>.Empty;

    /// <summary>안개 없이 모두 보는지: 안개 꺼짐 · 지도 공개 치트 · 관전자(세력 0).</summary>
    public bool SeesAll(int faction) =>
        !Enabled || faction <= 0 || faction >= world.Factions.Count || world.Factions[faction].RevealAll;

    public byte State(int faction, int x, int y)
    {
        if (SeesAll(faction)) return 2;
        var grid = GridOf(faction);
        if (grid == null) return 2;
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return 0;
        return grid[y * width + x];
    }

    public bool IsVisible(int faction, int x, int y) => State(faction, x, y) == 2;

    /// <summary>칸 번호 (y*width+x, 맵 밖은 -1) — 같은 위치를 여러 세력에 대해 물을 때 (은폐 탐지).</summary>
    internal int TileIndex(Vector2 p)
    {
        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y);
        return (uint)x < (uint)width && (uint)y < (uint)height ? y * width + x : -1;
    }

    /// <summary>TileIndex 로 미리 구한 칸이 세력에게 보이는지 (IsVisible 과 같은 뜻, 세력 루프용).</summary>
    internal bool VisibleAt(int faction, int tile)
    {
        if (SeesAll(faction)) return true;
        var grid = GridOf(faction);
        if (grid == null) return true;
        return tile >= 0 && grid[tile] == 2;
    }
    public bool IsVisible(int faction, Vector2 p) => IsVisible(faction, (int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));
    public bool IsExplored(int faction, int x, int y) => State(faction, x, y) >= 1;

    /// <summary>
    /// faction 이 엔티티를 볼 수 있는지. 자기 것 · 위치 노출된 세력의 건물은 항상 보인다.
    /// 건물은 발자국 타일 중 하나라도 시야에 들면 보인다 (중립 자원 시설 포함).
    /// 유닛은 칸이 보이고 (은폐 0 이거나 탐지 유효 — World.Detected, v0.4 §3) 일 때. 자동 교전 · AI · 화면이 모두 이것을 쓴다.
    /// </summary>
    public bool CanSee(int faction, Entity e)
    {
        if (SeesAll(faction) || e.Owner == faction) return true;
        var grid = GridOf(faction);
        if (grid == null) return true;
        if (e is Building b)
        {
            if (b.Owner > 0 && b.Owner < world.Factions.Count && world.Factions[b.Owner].Revealed) return true;
            return Footprint(grid, b.TileX, b.TileY, b.Size);
        }
        if (!IsVisible(faction, e.Pos)) return false;
        return e is not Unit u || world.Detected(faction, u);
    }

    /// <summary>현재 시야 밖이지만 기억하고 있는 건물 (마지막으로 본 모습). 렌더링/미니맵용.</summary>
    public IEnumerable<BuildingGhost> Ghosts(int faction)
    {
        var grid = GridOf(faction);
        if (SeesAll(faction) || grid == null) yield break;
        foreach (var g in memory[faction].Values)
        {
            if (world.Find(g.Id) is Building b) { if (CanSee(faction, b)) continue; }
            else if (Footprint(grid, g.TileX, g.TileY, g.Def.Size)) continue;
            yield return g;
        }
    }

    /// <summary>[측정] 지형 시야 계산 수 · 메모 적중 수 · 메모 크기.</summary>
    public (long Casts, long Hits, int Memo) VisionStats => vision != null ? (vision.Casts, vision.Hits, vision.Memoized) : (0, 0, 0);
}

/// <summary>마지막으로 목격한 건물의 모습.</summary>
public readonly record struct BuildingGhost(int Id, BuildingDef Def, int Owner, int TileX, int TileY);
