using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 참호 지형 (지도 · 미션에 미리 그린 것만 — 건설(참호 파기)은 2026-10-19 제거) 자동 점유 + 포탄 구덩이 지형 변경.
///  · 참호 칸마다 자리 terrain.trenchPerTile 개 (참호 방향을 따라 나란히). 자리 번호 = 칸 × 4 + k.
///  · 이동 · 공격 이동 중인 보병 (Def.CanOccupyTrench) 이 참호 칸을 밟으면 그 참호 묶음 (4방향 연결) 의 가장 가까운 빈 자리
///    (terrain.trenchCaptureRadius 안) 를 맡아 들어가고, 닿으면 사수 (Hold) — InTrench. 빈 자리가 없으면 그냥 지나간다.
///  · 이동 명령의 대형 슬롯이 참호 칸 terrain.trenchSnapRadius 안이면 슬롯을 빈 참호 자리로 옮긴다 (참호선에 중대를 세우는 이동이 곧장 되게).
///    이미 자리를 맡은 (예약) 병사는 도중의 다른 참호에 붙잡히지 않는다.
///  · 새 명령 (ResetOrder) 이면 자리를 놓고, 그 명령 동안 방금 떠난 참호 묶음은 도중 점유하지 않는다 (TrenchIgnore). 도착 지점이 참호 칸이면 그 묶음이어도 들어간다.
///    사수 명령은 참호 안 병사 · 자리로 가는 중인 병사의 자리를 유지한다.
///  · 참호 안 · 참호 칸 위 보병은 행진 사격하지 않는다 (Combat4.MarchFire).
///  · 참호 묶음 번호는 지도에서 다시 계산하는 파생 값 (지형 해시가 바뀌면), 자리 점유 표는 틱마다 유닛 목록에서 다시 모은다 — 저장하는 것은 유닛의 세 값뿐.
/// 지형 변경은 모두 World.MutateTerrain 한 곳 (포탄 구덩이 TryCrater). 모두 틱 · 입력의 함수, 목록 순 — 결정적.
/// </summary>
public sealed partial class Unit
{
    /// <summary>맡은 참호 자리 (칸 × 4 + k, -1 = 없음) — 가는 중 (예약) · 들어감 공통.</summary>
    [SnapExt] internal int TrenchSpot = -1;
    /// <summary>참호 자리에 들어가 사수 중.</summary>
    [SnapExt] public bool InTrench { get; internal set; }
    /// <summary>이번 명령 동안 도중 점유하지 않는 참호 묶음 번호 (방금 떠난 참호, -1 = 없음).</summary>
    [SnapExt] internal int TrenchIgnore = -1;
    /// <summary>참호 자리를 맡았다 (가는 중 포함).</summary>
    public bool HasTrenchSpot => TrenchSpot >= 0;
}

public sealed partial class GameMap
{
    readonly List<int> terrainEdits = new();   // 실행 중 바뀐 칸 (렌더러가 읽음, 저장 안 함 — 불러오면 렌더러를 새로 만든다)

    /// <summary>[렌더러] 실행 중 지형이 바뀐 칸 수 (늘어나기만 한다).</summary>
    public int TerrainEditCount => terrainEdits.Count;
    /// <summary>[렌더러] k 번째로 바뀐 칸 번호 (y × Width + x).</summary>
    public int TerrainEditAt(int k) => terrainEdits[k];

    internal void EditTerrain(int x, int y, TerrainType t)
    {
        Set(x, y, t);
        terrainEdits.Add(y * Width + x);
    }

    /// <summary>칸 (x, y) 가 참호 지형인가 (지도 밖 = false).</summary>
    public bool IsTrench(int x, int y) => InBounds(x, y) && Get(x, y) == TerrainType.Trench;
}

public sealed partial class World
{
    // ---------------------------------------------------------------- 참호 묶음 (파생, 저장 안 함)
    int[]? trenchComp;
    int trenchCompVer;

    int[] TrenchComps()
    {
        int h = Map.TrenchVersion;   // N16: 참호 칸이 바뀔 때만 다시 계산 (포탄 구덩이마다 지도 전체 BFS + 큰 배열 할당을 하던 것이 1200²~ 스파이크 원인)
        if (trenchComp != null && trenchCompVer == h && trenchComp.Length == Map.Width * Map.Height) return trenchComp;
        int w = Map.Width, n = w * Map.Height;
        var comp = new int[n];
        Array.Fill(comp, -1);
        var q = new Queue<int>();
        int next = 0;
        void Visit(int x, int y)
        {
            if (!Map.IsTrench(x, y)) return;
            int j = y * w + x;
            if (comp[j] >= 0) return;
            comp[j] = next;
            q.Enqueue(j);
        }
        for (int i = 0; i < n; i++)
        {
            if (comp[i] >= 0 || !Map.IsTrench(i % w, i / w)) continue;
            comp[i] = next;
            q.Enqueue(i);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % w, cy = c / w;
                Visit(cx + 1, cy); Visit(cx - 1, cy); Visit(cx, cy + 1); Visit(cx, cy - 1);
            }
            next++;
        }
        trenchComp = comp;
        trenchCompVer = h;
        return comp;
    }

    /// <summary>칸의 참호 묶음 번호 (참호가 아니면 -1).</summary>
    public int TrenchCompAt(int x, int y) => Map.InBounds(x, y) ? TrenchComps()[y * Map.Width + x] : -1;

    int TrenchPerTile => Math.Clamp(Defs.Terrain.TrenchPerTile, 1, 4);

    /// <summary>참호 자리의 위치 (칸 중심 + 참호 방향을 따라 나란히).</summary>
    public Vector2 TrenchSpotPos(int spot)
    {
        int tile = spot >> 2, k = spot & 3, x = tile % Map.Width, y = tile / Map.Width;
        var c = new Vector2(x + 0.5f, y + 0.5f);
        int cap = TrenchPerTile;
        if (cap <= 1) return c;
        float off = (k + 0.5f) / cap - 0.5f;
        bool horiz = Map.IsTrench(x - 1, y) || Map.IsTrench(x + 1, y) || !(Map.IsTrench(x, y - 1) || Map.IsTrench(x, y + 1));
        return horiz ? c + new Vector2(off, 0) : c + new Vector2(0, off);
    }

    // ---------------------------------------------------------------- 자리 점유 표 (틱마다 유닛 목록에서, 저장 안 함)
    readonly Dictionary<int, int> trenchOwners = new();   // 자리 → 유닛 Id
    long trenchOwnersTick = long.MinValue;

    Dictionary<int, int> TrenchOwners()
    {
        if (trenchOwnersTick == Tick) return trenchOwners;
        trenchOwnersTick = Tick;
        trenchOwners.Clear();
        foreach (var u in Units)
            if (u.Alive && u.TrenchSpot >= 0) trenchOwners.TryAdd(u.TrenchSpot, u.Id);
        return trenchOwners;
    }

    void ClaimTrenchSpot(Unit u, int spot)
    {
        ReleaseTrenchSpot(u);
        u.TrenchSpot = spot;
        TrenchOwners()[spot] = u.Id;
    }

    void ReleaseTrenchSpot(Unit u)
    {
        u.InTrench = false;
        if (u.TrenchSpot < 0) return;
        var o = TrenchOwners();
        if (o.TryGetValue(u.TrenchSpot, out int id) && id == u.Id) o.Remove(u.TrenchSpot);
        u.TrenchSpot = -1;
    }

    /// <summary>pos 에서 radius 안의 가장 가까운 빈 참호 자리 (comp ≥ 0 이면 그 묶음만). 없으면 -1.</summary>
    int FindTrenchSpot(Vector2 pos, float radius, int comp)
    {
        if (radius <= 0) return -1;
        var comps = TrenchComps();
        var owners = TrenchOwners();
        int cap = TrenchPerTile, w = Map.Width;
        int x0 = Math.Max(0, (int)MathF.Floor(pos.X - radius)), x1 = Math.Min(w - 1, (int)MathF.Floor(pos.X + radius));
        int y0 = Math.Max(0, (int)MathF.Floor(pos.Y - radius)), y1 = Math.Min(Map.Height - 1, (int)MathF.Floor(pos.Y + radius));
        float r2 = radius * radius, best = float.MaxValue;
        int bestSpot = -1;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int c = comps[y * w + x];
                if (c < 0 || (comp >= 0 && c != comp)) continue;
                for (int k = 0; k < cap; k++)
                {
                    int spot = (y * w + x) * 4 + k;
                    if (owners.ContainsKey(spot)) continue;
                    float d = Vector2.DistanceSquared(pos, TrenchSpotPos(spot));
                    if (d > r2 || d >= best) continue;   // 같은 거리면 앞 칸 (y, x, k 순)
                    best = d; bestSpot = spot;
                }
            }
        return bestSpot;
    }

    /// <summary>
    /// 대형 슬롯 → 참호 자리 배정 (이동 명령 실행 때): 참호를 차지할 수 있는 보병 슬롯마다 trenchSnapRadius 안의 빈 자리 후보를 모아
    /// (거리², 유닛 Id, 자리) 순 탐욕 배정. 결과 = 유닛별 자리 (-1 = 그대로). 명령받은 유닛이 지금 맡은 자리는 빈 것으로 본다.
    /// </summary>
    internal int[] SnapSlotsToTrenches(IReadOnlyList<Unit> units, IReadOnlyList<Vector2> slots, bool attackMove = false)
    {
        var res = new int[units.Count];
        Array.Fill(res, -1);
        if (attackMove) return res;   // 공격 이동은 참호에 잡히지 않는다 (일반 이동 · 대형 이동만 슬롯을 참호로)
        float r = Defs.Terrain.TrenchSnapRadius;
        if (r <= 0 || units.Count == 0) return res;
        var comps = TrenchComps();
        var owners = TrenchOwners();
        HashSet<int>? mine = null;
        int cap = TrenchPerTile, w = Map.Width;
        float r2 = r * r;
        List<(float D, int Id, int Spot, int I)>? cand = null;
        for (int i = 0; i < units.Count; i++)
        {
            if (!units[i].Def.CanOccupyTrench) continue;
            Vector2 p = slots[i];
            int x0 = Math.Max(0, (int)MathF.Floor(p.X - r)), x1 = Math.Min(w - 1, (int)MathF.Floor(p.X + r));
            int y0 = Math.Max(0, (int)MathF.Floor(p.Y - r)), y1 = Math.Min(Map.Height - 1, (int)MathF.Floor(p.Y + r));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (comps[y * w + x] < 0) continue;
                    if (mine == null) { mine = new HashSet<int>(); foreach (var u in units) mine.Add(u.Id); }
                    for (int k = 0; k < cap; k++)
                    {
                        int spot = (y * w + x) * 4 + k;
                        if (owners.TryGetValue(spot, out int id) && !mine.Contains(id)) continue;
                        float d = Vector2.DistanceSquared(p, TrenchSpotPos(spot));
                        if (d <= r2) (cand ??= new()).Add((d, units[i].Id, spot, i));
                    }
                }
        }
        if (cand == null) return res;
        cand.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.Id != b.Id ? a.Id.CompareTo(b.Id) : a.Spot.CompareTo(b.Spot));
        var used = new HashSet<int>();
        foreach (var c in cand)
        {
            if (res[c.I] >= 0 || used.Contains(c.Spot)) continue;
            res[c.I] = c.Spot;
            used.Add(c.Spot);
        }
        return res;
    }

    /// <summary>명령 실행 뒤 (ResetOrder 다음): 배정된 자리를 맡긴다.</summary>
    void ClaimSnapped(IReadOnlyList<Unit> units, int[] spots)
    {
        for (int i = 0; i < units.Count; i++)
            if (spots[i] >= 0) ClaimTrenchSpot(units[i], spots[i]);
    }

    /// <summary>새 명령 (ResetOrder): 자리를 놓고, 이번 명령 동안 방금 떠난 (또는 지금 서 있는) 참호 묶음은 도중 점유하지 않는다.</summary>
    void LeaveTrench(Unit u)
    {
        if (!u.Def.CanOccupyTrench) return;
        int ign;
        if (u.InTrench && u.TrenchSpot >= 0) ign = TrenchComps()[u.TrenchSpot >> 2];
        else { var (x, y) = GameMap.TileOf(u.Pos); ign = TrenchCompAt(x, y); }
        ReleaseTrenchSpot(u);
        u.TrenchIgnore = ign;
    }

    void EnterTrench(Unit u)
    {
        u.InTrench = true;
        u.Order = UnitOrder.Hold;
        u.Group = null;
        u.QuickStep = false; u.Retreating = false;
        u.TrenchIgnore = -1;
    }

    /// <summary>맡은 자리로 한 사람 이동 (짧은 거리 — 일반 이동 그룹).</summary>
    void WalkToTrenchSpot(Unit u)
    {
        u.Group = new MoveGroup(this, TrenchSpotPos(u.TrenchSpot), 1);
        u.LastGroup = null;
        u.Order = UnitOrder.Move;
        u.QuickStep = false; u.Retreating = false;
    }

    const float TrenchEnterDist = 0.45f;

    /// <summary>매 틱 (UpdateUnitCore 이동 뒤): 걷는 중인 보병이 참호 칸을 밟으면 빈 자리를 맡아 들어간다.</summary>
    void UpdateTrenchCapture(Unit u)
    {
        if (u.TrenchSpot >= 0 || u.Velocity == Vector2.Zero || u.Charging || !u.Def.CanOccupyTrench) return;
        if (u.Order != UnitOrder.Move || u.Group == null) return;   // 공격 이동은 지나간다
        var (x, y) = GameMap.TileOf(u.Pos);
        int comp = TrenchCompAt(x, y);
        if (comp < 0 || comp == u.TrenchIgnore) return;
        int spot = FindTrenchSpot(u.Pos, Defs.Terrain.TrenchCaptureRadius, comp);
        if (spot < 0) return;   // 빈 자리 없음: 지나간다
        ClaimTrenchSpot(u, spot);
        WalkToTrenchSpot(u);
    }

    /// <summary>이동 도착 (ArriveGroup): 맡은 자리에 닿았으면 들어가고, 참호 칸에 도착했으면 (묶음 무시 규칙 없이) 빈 자리를 찾아 들어간다.</summary>
    void TrenchOnArrive(Unit u)
    {
        if (!u.Def.CanOccupyTrench) return;
        if (u.TrenchSpot >= 0)
        {
            if (Vector2.Distance(u.Pos, TrenchSpotPos(u.TrenchSpot)) <= TrenchEnterDist) { EnterTrench(u); return; }
            ReleaseTrenchSpot(u);   // 닿지 못함 (막힘 · 붐빔) — 서 있는 칸이 참호면 아래에서 다시
        }
        if (u.Charging) return;
        var (x, y) = GameMap.TileOf(u.Pos);
        int comp = TrenchCompAt(x, y);
        if (comp < 0) return;
        int spot = FindTrenchSpot(u.Pos, Defs.Terrain.TrenchCaptureRadius, comp);
        if (spot < 0) return;
        ClaimTrenchSpot(u, spot);
        if (Vector2.Distance(u.Pos, TrenchSpotPos(spot)) <= TrenchEnterDist) EnterTrench(u);
        else WalkToTrenchSpot(u);
    }

    /// <summary>사수 명령: 참호 안 병사는 그대로, 자리로 가는 중 (2.5칸 안) 이면 자리까지 가서 사수. 자리를 지켰으면 true.</summary>
    bool HoldKeepsTrench(Unit u, int spot, bool inTrench)
    {
        if (spot < 0 || !u.Def.CanOccupyTrench) return false;
        if (inTrench) { ClaimTrenchSpot(u, spot); EnterTrench(u); return true; }
        if (Vector2.Distance(u.Pos, TrenchSpotPos(spot)) > 2.5f) return false;
        ClaimTrenchSpot(u, spot);
        WalkToTrenchSpot(u);
        return true;
    }

    /// <summary>[AI] point 에서 radius 안의 가장 가까운 참호 칸 중심 (없으면 null). 같은 거리면 앞 칸.
    /// front (단위 벡터, engage 2026-10-19) 가 있으면 점수 = 거리 − 0.5 × 앞쪽 거리 — 적이 다가올 쪽 참호선을 먼저.</summary>
    public Vector2? NearestTrench(Vector2 point, float radius, Vector2 front = default)
    {
        if (front != Vector2.Zero) return FrontTrench(point, radius, Vector2.Normalize(front));
        var comps = TrenchComps();
        int w = Map.Width;
        int x0 = Math.Max(0, (int)MathF.Floor(point.X - radius)), x1 = Math.Min(w - 1, (int)MathF.Floor(point.X + radius));
        int y0 = Math.Max(0, (int)MathF.Floor(point.Y - radius)), y1 = Math.Min(Map.Height - 1, (int)MathF.Floor(point.Y + radius));
        float best = radius * radius;
        Vector2? res = null;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (comps[y * w + x] < 0) continue;
                var c = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.DistanceSquared(point, c);
                if (d < best) { best = d; res = c; }
            }
        return res;
    }

    Vector2? FrontTrench(Vector2 point, float radius, Vector2 front)
    {
        var comps = TrenchComps();
        int w = Map.Width;
        int x0 = Math.Max(0, (int)MathF.Floor(point.X - radius)), x1 = Math.Min(w - 1, (int)MathF.Floor(point.X + radius));
        int y0 = Math.Max(0, (int)MathF.Floor(point.Y - radius)), y1 = Math.Min(Map.Height - 1, (int)MathF.Floor(point.Y + radius));
        float r2 = radius * radius, best = float.MaxValue;
        Vector2? res = null;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (comps[y * w + x] < 0) continue;
                var c = new Vector2(x + 0.5f, y + 0.5f);
                float d2 = Vector2.DistanceSquared(point, c);
                if (d2 >= r2) continue;
                float sc = MathF.Sqrt(d2) - 0.5f * Vector2.Dot(c - point, front);
                if (sc < best) { best = sc; res = c; }
            }
        return res;
    }

    /// <summary>시뮬 중 지형 변경 (포탄 구덩이): 지도 + 렌더 편집 기록 + 지형 시야 갱신. 통행 여부가 같은 지형끼리만 바꾼다 (길찾기 무관).</summary>
    internal void MutateTerrain(int x, int y, TerrainType t)
    {
        if (!Map.InBounds(x, y) || Map.Get(x, y) == t) return;
        System.Diagnostics.Debug.Assert(Terrains.Get(Map.Get(x, y)).Walkable == Terrains.Get(t).Walkable);
        Map.EditTerrain(x, y, t);
        Fog.TerrainChanged(x, y);
    }

    // ---------------------------------------------------------------- 포탄 구덩이

    /// <summary>포탄 착탄 (Explode): 무기 craterChance 확률로 착탄 칸이 포탄 구덩이가 된다. 풀 · 흙 · 모래 칸만 (가옥 · 물 · 참호 · 숲 등은 그대로, 겹치지 않음).</summary>
    internal void TryCrater(Entity shooter, Vector2 impact, WeaponDef w) => TryCrater(shooter.Id, impact, w);
    internal void TryCrater(int shooterId, Vector2 impact, WeaponDef w)
    {
        if (w.CraterChance <= 0) return;
        var (x, y) = GameMap.TileOf(impact);
        if (!Map.InBounds(x, y) || Map.Get(x, y) is not (TerrainType.Grass or TerrainType.Dirt or TerrainType.Sand) || Map.OccupantAt(x, y) != 0) return;
        if (Roll(shooterId, 0x0C4A7E5) >= w.CraterChance) return;
        MutateTerrain(x, y, TerrainType.Crater);
    }

    // ---------------------------------------------------------------- 체크섬 · 저장 (SUPL 구역 끝, FormatVersion 18)

    void HashTrenches(ref Hasher h)
    {
        foreach (var u in Units) { h.Add(u.TrenchSpot); h.Add(u.InTrench ? 1 : 0); h.Add(u.TrenchIgnore); h.Add(u.CoverHold ? 1 : 0); }   // CoverHold = FormatVersion 28 (CoverSeek.cs)
    }

    void WriteTrenches(SnapWriter w)
    {
        w.I(Units.Count);
        foreach (var u in Units) { w.I(u.TrenchSpot); w.Bool(u.InTrench); w.I(u.TrenchIgnore); w.Bool(u.CoverHold); }
    }

    void ReadTrenches(SnapReader r)
    {
        if (r.Count() != Units.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "trench units"));
        int n = Map.Width * Map.Height * 4;
        foreach (var u in Units)
        {
            u.TrenchSpot = r.I();
            u.InTrench = r.Bool();
            u.TrenchIgnore = r.I();
            u.CoverHold = r.Bool();
            if (u.TrenchSpot >= n || u.TrenchSpot < -1) throw new SnapshotException(string.Format(SnapMsg.Invalid, "trench spot"));
        }
    }
}
