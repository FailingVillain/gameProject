using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 참호 돌파 묶음 (N59 · N60 · N65, 사용자 승인 2026-10-19 · 2026-10-20):
///  · 연막 (SmokeCloud): 연막 수류탄 (돌격보병, unit.smokeGrenades) · 연막탄 (지점 포격 '연막' 종류, weapon.smokeRadius 가 있는 박격포 · 곡사포) 이 떨어진 곳에
///    반경 r 의 원형 연막이 breach.*Sec 초 동안 그대로 있다 (바람 · 번짐 · 옅어짐 없음). 연막을 지나는 선분 길이 합이 breach.smokeSeeDepth 칸을 넘으면
///    시야 (안개 찍기 · 관측자) 와 직사 사선 (World.HasFiringLine · 자동 표적) 이 막힌다 — 연막 가장자리 근처끼리 · 연막 안 바로 옆끼리는 보인다. 곡사 사격은 사선과 무관.
///  · 철조망 절단 (CutWireCommand): unit.wireCutter (돌격보병) 가 지점 둘레 철조망 칸 하나씩을 맡아 옆 칸으로 가서 breach.wireCutSec 초 → 그 칸이 흙 (통과 가능) 이 된다.
///    새 명령 · 정지 = 취소. 선로 파괴는 나중.
/// 모두 틱 · 입력의 함수 (명령 · 목록 순서). 상태 = Smokes · 유닛 SmokeGrenades · WireCutTile · WireCutTicks · 수류탄 · 포탄의 연막 표시
/// (CMB2 끝 WriteBreach, FormatVersion 53), 체크섬 HashBreach.
/// </summary>
public sealed record ThrowSmokeCommand(int Faction, int[] Units, Vector2 Target) : GameCommand(Faction);

/// <summary>철조망 절단 명령 (N59): Target 둘레 breach.wireCutSearch 칸 안 철조망. 리플레이 v20.</summary>
public sealed record CutWireCommand(int Faction, int[] Units, Vector2 Target) : GameCommand(Faction);

/// <summary>연막 한 덩이 (원형, 크기 고정). EndTick 이 되면 사라진다.</summary>
public sealed class SmokeCloud
{
    public int Owner;
    public Vector2 Pos;
    public float Radius;
    public long StartTick, EndTick;
}

/// <summary>balance.json "breach" — 연막 · 철조망 절단 수치.</summary>
public sealed class BreachDef
{
    /// <summary>연막 수류탄 연막 반경 (칸) · 지속 (초). 사거리 · 신관 · 흩어짐은 grenade 와 같다.</summary>
    public float SmokeGrenadeRadius { get; set; } = 2.5f;
    public float SmokeGrenadeSec { get; set; } = 30f;
    /// <summary>연막탄 지속 (초). 반경은 무기마다 weapon.smokeRadius.</summary>
    public float SmokeShellSec { get; set; } = 45f;
    /// <summary>지점 포격 '연막' 종류 한 번에 쏘는 발수.</summary>
    public int SmokeRounds { get; set; } = 3;
    /// <summary>시선이 연막 속을 이 길이 (칸) 넘게 지나면 막힌다.</summary>
    public float SmokeSeeDepth { get; set; } = 1.0f;
    /// <summary>철조망 한 칸 절단 시간 (초) · 절단 중 칸 중심까지 최대 거리 (칸) · 명령 지점 둘레 철조망 찾기 반경 (칸).</summary>
    public float WireCutSec { get; set; } = 4f;
    public float WireCutReach { get; set; } = 2.0f;
    public float WireCutSearch { get; set; } = 4f;
}

public sealed partial class Unit
{
    /// <summary>남은 연막 수류탄 (시작 = Def.SmokeGrenades, N65).</summary>
    [SnapExt] public int SmokeGrenades { get; internal set; }
    /// <summary>자르러 가는 · 자르는 철조망 칸 (y × 지도 폭 + x), -1 = 없음. 새 명령이면 해제 (ResetOrder).</summary>
    [SnapExt] internal int WireCutTile = -1;
    /// <summary>이번 칸 절단 진행 (틱).</summary>
    [SnapExt] internal int WireCutTicks;
    /// <summary>[표시] 철조망을 자르러 가는 중이거나 자르는 중.</summary>
    public bool CuttingWire => WireCutTile >= 0;
    /// <summary>[표시] 절단 진행 0..1.</summary>
    public float WireCutProgress => WireCutTile >= 0 && Defs.Current.Breach.WireCutSec > 0 ? Math.Clamp(WireCutTicks * Config.SimDt / Defs.Current.Breach.WireCutSec, 0f, 1f) : 0f;
}

public sealed partial class GameMap
{
    /// <summary>철조망 칸을 흙으로 (통과 가능): 통행 정보가 바뀌므로 Version · ChangeLog (길찾기가 그 칸만 다시 읽음).</summary>
    internal void OpenWire(int x, int y)
    {
        if (!InBounds(x, y) || Get(x, y) != TerrainType.Wire) return;
        EditTerrain(x, y, TerrainType.Dirt);
        Version++;
        ChangeLog.Add((x, y, 1, 1));
    }
}

public sealed partial class World
{
    /// <summary>떠 있는 연막 (생긴 순서). 저장 · 체크섬.</summary>
    [SnapExt] public readonly List<SmokeCloud> Smokes = new();

    // ---------------------------------------------------------------- 연막

    void SpawnSmoke(int owner, Vector2 pos, float radius, float sec)
    {
        if (radius <= 0 || sec <= 0 || !float.IsFinite(pos.X) || !float.IsFinite(pos.Y)) return;
        Smokes.Add(new SmokeCloud { Owner = owner, Pos = pos, Radius = radius, StartTick = Tick, EndTick = Tick + Math.Max(1, (int)MathF.Round(sec / Config.SimDt)) });
        Stats.SmokesSpawned++;
    }

    /// <summary>다 탄 연막 지우기 (UpdateBlasts 앞).</summary>
    void UpdateSmokes()
    {
        if (Smokes.Count == 0) return;
        Smokes.RemoveAll(s => s.EndTick <= Tick);
    }

    /// <summary>선분 a → b 가 연막 속을 지나는 길이 합이 breach.smokeSeeDepth 를 넘는가 (시야 · 사선 차단). 연막이 없으면 즉시 false.</summary>
    public bool SmokeBlocks(Vector2 a, Vector2 b)
    {
        if (Smokes.Count == 0) return false;
        return SmokeBlocksAmong(Smokes, a.X, a.Y, b.X, b.Y, Defs.Breach.SmokeSeeDepth);
    }

    /// <summary>직선 (ax, ay) → (bx, by) 가 clouds 속을 지나는 길이 합 &gt; depth 인가. 스칼라 연산만 (결정적).</summary>
    internal static bool SmokeBlocksAmong(List<SmokeCloud> clouds, float ax, float ay, float bx, float by, float depth)
    {
        float dx = bx - ax, dy = by - ay;
        float len2 = dx * dx + dy * dy;
        float len = MathF.Sqrt(len2);
        float sum = 0;
        for (int i = 0; i < clouds.Count; i++)
        {
            var s = clouds[i];
            float fx = ax - s.Pos.X, fy = ay - s.Pos.Y;
            float c = fx * fx + fy * fy - s.Radius * s.Radius;
            if (len2 < 1e-8f) { if (c < 0 && depth <= 0) return true; continue; }
            float bb = fx * dx + fy * dy;
            float disc = bb * bb - len2 * c;
            if (disc <= 0) continue;
            float sq = MathF.Sqrt(disc);
            float t0 = (-bb - sq) / len2, t1 = (-bb + sq) / len2;
            if (t0 < 0) t0 = 0;
            if (t1 > 1) t1 = 1;
            if (t1 <= t0) continue;
            sum += (t1 - t0) * len;
            if (sum > depth) return true;
        }
        return false;
    }

    /// <summary>[안개] center 둘레 radius 시야와 겹치는 연막을 buf 에 모은다. 하나라도 있으면 true.</summary>
    internal bool SmokesNear(Vector2 center, float radius, List<SmokeCloud> buf)
    {
        buf.Clear();
        for (int i = 0; i < Smokes.Count; i++)
        {
            var s = Smokes[i];
            float dx = s.Pos.X - center.X, dy = s.Pos.Y - center.Y, rr = radius + s.Radius + 3f;   // 지형 시야는 묶음 (2칸) 단위라 원보다 조금 넓게 찍힌다
            if (dx * dx + dy * dy < rr * rr) buf.Add(s);
        }
        return buf.Count > 0;
    }

    // ---------------------------------------------------------------- 연막 수류탄 (N65)

    void ThrowSmoke(Unit u, Vector2 at)
    {
        var g = Defs.Grenade;
        u.SmokeGrenades--;
        float d = Vector2.Distance(u.Pos, at);
        float maxErr = g.Range > 0 ? g.Scatter * MathF.Min(1f, d / g.Range) : 0f;
        float err = maxErr * MathF.Sqrt(Roll(u.Id, 0x5A01));
        float ang = Roll(u.Id, 0x5A02) * DetMath.Tau;
        Vector2 land = at + new Vector2(DetMath.Cos(ang) * err, DetMath.Sin(ang) * err);
        Blasts.Add(new GrenadeBlast { ThrowerId = u.Id, Owner = u.Owner, Pos = land, ExplodeTick = Tick + Math.Max(1, (int)MathF.Round(g.Fuse / Config.SimDt)), Smoke = true });
        FaceToward(u, at, 1f);
    }

    void ExecThrowSmoke(List<Unit> list, Vector2 target)
    {
        float range = Defs.Grenade.Range;
        foreach (var u in list)
        {
            if (u.SmokeGrenades <= 0 || !u.Alive || u.Surrendered) continue;
            if (Vector2.Distance(u.Pos, target) > range + 0.01f) continue;   // 사거리 밖은 던지지 않는다
            ThrowSmoke(u, target);
        }
    }

    /// <summary>연막 수류탄 투척 (플레이어 · 참호 돌격). 보유한 유닛만.</summary>
    public void ThrowSmokeAt(int faction, IEnumerable<Unit> units, Vector2 target) =>
        IssueUnits(faction, units.Where(u => u.SmokeGrenades > 0), ids => new ThrowSmokeCommand(faction, ids, target));

    /// <summary>수류탄 폭발 처리에서: 연막 수류탄이면 연막을 피우고 true.</summary>
    bool BlastSmoke(GrenadeBlast b)
    {
        if (!b.Smoke) return false;
        var bd = Defs.Breach;
        SpawnSmoke(b.Owner, b.Pos, bd.SmokeGrenadeRadius, bd.SmokeGrenadeSec);
        return true;
    }

    /// <summary>포탄 착탄 처리에서: 연막탄이면 연막을 피우고 true.</summary>
    bool ShellSmoke(ShellFlight s)
    {
        if (!s.Smoke) return false;
        SpawnSmoke(s.Owner, s.Pos, s.Weapon.SmokeRadius, Defs.Breach.SmokeShellSec);
        return true;
    }

    // ---------------------------------------------------------------- 철조망 절단 (N59)

    /// <summary>철조망 절단 명령 (wireCutter 유닛만).</summary>
    public void CutWire(int faction, IEnumerable<Unit> units, Vector2 target) =>
        IssueUnits(faction, units.Where(u => u.Def.WireCutter), ids => new CutWireCommand(faction, ids, target));

    /// <summary>지점 둘레 radius 칸 안 철조망 칸 (거리², 칸 번호 순). 철조망이 없으면 빈 목록.</summary>
    public List<(float D2, int Tile)> WireTilesNear(Vector2 target, float radius)
    {
        var wireCand = new List<(float D2, int Tile)>();
        int W = Map.Width;
        int x0 = Math.Max(0, (int)MathF.Floor(target.X - radius)), x1 = Math.Min(W - 1, (int)MathF.Floor(target.X + radius));
        int y0 = Math.Max(0, (int)MathF.Floor(target.Y - radius)), y1 = Math.Min(Map.Height - 1, (int)MathF.Floor(target.Y + radius));
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (Map.Get(x, y) != TerrainType.Wire) continue;
                float dx = x + 0.5f - target.X, dy = y + 0.5f - target.Y, d2 = dx * dx + dy * dy;
                if (d2 <= r2) wireCand.Add((d2, y * W + x));
            }
        wireCand.Sort((p, q) => p.D2 != q.D2 ? p.D2.CompareTo(q.D2) : p.Tile.CompareTo(q.Tile));
        return wireCand;
    }

    /// <summary>철조망 칸 옆 설 자리: 상하좌우 → 대각 이웃 중 걸을 수 있는 칸, from 에 가장 가까운 칸 중심 (없으면 null).</summary>
    Vector2? WireStandSpot(int tile, Vector2 from)
    {
        int W = Map.Width, tx = tile % W, ty = tile / W;
        Vector2? best = null; float bd = float.MaxValue;
        for (int pass = 0; pass < 2 && best == null; pass++)
            for (int k = 0; k < 4; k++)
            {
                int dx = pass == 0 ? (k == 0 ? 1 : k == 1 ? -1 : 0) : (k < 2 ? 1 : -1);
                int dy = pass == 0 ? (k == 2 ? 1 : k == 3 ? -1 : 0) : (k % 2 == 0 ? 1 : -1);
                int x = tx + dx, y = ty + dy;
                if (!Map.IsWalkable(x, y)) continue;
                var c = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.DistanceSquared(c, from);
                if (d < bd) { bd = d; best = c; }
            }
        return best;
    }

    void ExecCutWire(List<Unit> list, Vector2 target)
    {
        var cutters = list.Where(u => u.Alive && u.Def.WireCutter && !u.Surrendered).ToList();
        if (cutters.Count == 0) return;
        var tiles = WireTilesNear(target, Defs.Breach.WireCutSearch).Select(t => t.Tile).ToList();
        if (tiles.Count == 0) return;
        for (int k = 0; k < cutters.Count; k++)
        {
            var u = cutters[k];
            int tile = tiles[k % tiles.Count];   // 가까운 칸부터 한 사람씩 (칸보다 사람이 많으면 다시 처음부터)
            if (WireStandSpot(tile, u.Pos) is not Vector2 spot) continue;
            ResetOrder(u);
            u.WireCutTile = tile;
            u.WireCutTicks = 0;
            u.Group = new MoveGroup(this, spot, 1);
            u.Order = UnitOrder.Move;
            u.CoverHold = true;   // 도착하면 사수 (자리를 지키며 자른다, CoverSeek.cs)
            u.LeashOrigin = spot;
            Acknowledge(u);
        }
    }

    /// <summary>철조망 절단 한 틱 (UpdateUnitCore): 걸어가는 중이면 기다리고, 칸 옆에 섰으면 진행, 다 되면 칸을 연다. 멀어지면 · 칸이 이미 열렸으면 끝.</summary>
    void UpdateWireCut(Unit u)
    {
        if (u.WireCutTile < 0) return;
        int W = Map.Width, x = u.WireCutTile % W, y = u.WireCutTile / W;
        if (!Map.InBounds(x, y) || Map.Get(x, y) != TerrainType.Wire) { u.WireCutTile = -1; u.WireCutTicks = 0; return; }
        if (u.Group != null) return;   // 아직 가는 중
        var bd = Defs.Breach;
        float dx = x + 0.5f - u.Pos.X, dy = y + 0.5f - u.Pos.Y;
        if (dx * dx + dy * dy > bd.WireCutReach * bd.WireCutReach) { u.WireCutTile = -1; u.WireCutTicks = 0; return; }   // 밀려났거나 못 닿음
        if (++u.WireCutTicks < Math.Max(1, (int)MathF.Round(bd.WireCutSec / Config.SimDt))) return;
        Map.OpenWire(x, y);
        Fog.TerrainChanged(x, y);
        Stats.WiresCut++;
        u.WireCutTile = -1; u.WireCutTicks = 0;
    }

    // ---------------------------------------------------------------- 저장 (CMB2 끝, WriteCombat3 뒤) · 체크섬

    void WriteBreach(SnapWriter w)
    {
        foreach (var u in Units) { w.I(u.SmokeGrenades); w.I(u.WireCutTile); w.I(u.WireCutTicks); }
        w.I(Blasts.Count);
        foreach (var b in Blasts) w.Bool(b.Smoke);
        w.I(Shells.Count);
        foreach (var s in Shells) w.Bool(s.Smoke);
        w.I(Smokes.Count);
        foreach (var s in Smokes) { w.I(s.Owner); w.F(s.Pos.X); w.F(s.Pos.Y); w.F(s.Radius); w.L(s.StartTick); w.L(s.EndTick); }
    }

    void ReadBreach(SnapReader r)
    {
        int n = Map.Width * Map.Height;
        foreach (var u in Units)
        {
            u.SmokeGrenades = r.I(); u.WireCutTile = r.I(); u.WireCutTicks = r.I();
            if (u.SmokeGrenades < 0 || u.SmokeGrenades > Math.Max(1, u.Def.SmokeGrenades)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "smoke grenades"));
            if (u.WireCutTile < -1 || u.WireCutTile >= n || u.WireCutTicks < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "wire cut"));
        }
        if (r.Count() != Blasts.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "smoke blasts"));
        foreach (var b in Blasts) b.Smoke = r.Bool();
        if (r.Count() != Shells.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "smoke shells"));
        foreach (var s in Shells) s.Smoke = r.Bool();
        Smokes.Clear();
        int m = r.Count(24);
        for (int i = 0; i < m; i++)
        {
            var s = new SmokeCloud { Owner = r.I(), Pos = new Vector2(r.F(), r.F()), Radius = r.F(), StartTick = r.L(), EndTick = r.L() };
            if (!float.IsFinite(s.Pos.X) || !float.IsFinite(s.Pos.Y) || !(s.Radius > 0) || s.EndTick < s.StartTick) throw new SnapshotException(string.Format(SnapMsg.Invalid, "smoke"));
            Smokes.Add(s);
        }
    }

    void HashBreach(ref Hasher h)
    {
        foreach (var u in Units) { h.Add(u.SmokeGrenades); h.Add(u.WireCutTile); h.Add(u.WireCutTicks); }
        foreach (var b in Blasts) h.Add(b.Smoke ? 1 : 0);
        foreach (var s in Shells) h.Add(s.Smoke ? 1 : 0);
        h.Add(Smokes.Count);
        foreach (var s in Smokes) { h.Add(s.Owner); h.Add(s.Pos.X); h.Add(s.Pos.Y); h.Add(s.Radius); h.Add(s.StartTick); h.Add(s.EndTick); }
    }
}
