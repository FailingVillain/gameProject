using System.Numerics;

namespace KoreanDivision.Core;

public sealed partial class Unit
{
    /// <summary>[assaultinf] 돌격 접근 약진: 0 = 없음, 아니면 이 틱까지 HopTo (엄폐 칸) 로 걷는다. 돌격이 끝나면 0. 저장 · 체크섬 (FormatVersion 49).</summary>
    [SnapExt] public long HopUntil { get; internal set; }
    [SnapExt] public Vector2 HopTo { get; internal set; }
}

/// <summary>
/// [assaultinf C] 돌격 접근 중 (표적이 chargeHopMinDist 보다 멀 때) 반경 chargeHopRange (6) 칸 안에서 표적 쪽으로 chargeHopMaxHit 이하 엄폐 칸이 있으면
/// 곧장 달리지 않고 그 칸을 거쳐 약진한다 (한 번 chargeHopMaxSec 상한). 도착하면 다시 찾는다 — 다음 칸은 현재 위치보다 1.5칸 이상 표적에 가까워야 하므로 되돌지 않는다.
/// 탐색은 10틱마다 (Id 분산), 칸 순 · 점수 동률이면 앞 칸 — 결정적. 대상 = Def.ChargeHopRange &gt; 0 인 유닛 (돌격보병).
/// </summary>
public sealed partial class World
{
    internal Vector2? ChargeHopGoal(Unit u, Entity t)
    {
        var d = u.Def;
        Vector2 tp = t.Pos;
        float dist = Vector2.Distance(u.Pos, tp);
        if (dist <= d.ChargeHopMinDist) { u.HopUntil = 0; return null; }
        if (u.HopUntil > 0)
        {
            if (Tick < u.HopUntil && Vector2.DistanceSquared(u.Pos, u.HopTo) > 0.36f) return u.HopTo;
            u.HopUntil = 0;
        }
        if ((Tick + u.Id) % 10 != 0) return null;
        float r = d.ChargeHopRange;
        var (px, py) = GameMap.TileOf(u.Pos);
        int ri = (int)MathF.Ceiling(r);
        float best = float.MaxValue, r2 = r * r;
        Vector2? res = null;
        for (int dy = -ri; dy <= ri; dy++)
        for (int dx = -ri; dx <= ri; dx++)
        {
            int x = px + dx, y = py + dy;
            if (!Map.IsWalkable(x, y) || Map.IsDeepForest(x, y)) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float dq = Vector2.DistanceSquared(q, u.Pos);
            if (dq > r2 || dq < 1f) continue;
            float dt2 = Vector2.Distance(q, tp);
            if (dt2 > dist - 1.5f || MathF.Sqrt(dq) + dt2 > dist + 1.5f) continue;   // 표적 쪽으로 나아가는 칸만, 우회 작게
            if (Map.IsWire(q)) continue;
            float hit = CoverHitAt(q, u);
            if (hit > d.ChargeHopMaxHit) continue;
            float sc = hit + 0.05f * dt2;
            if (sc >= best) continue;
            if (TileCrowd(q, u) >= Math.Max(1, Defs.CombatRules.CoverSeekPerTile)) continue;
            best = sc; res = q;
        }
        if (res is { } g)
        {
            u.HopTo = g;
            u.HopUntil = Tick + Math.Max(1, (long)MathF.Round(d.ChargeHopMaxSec / Config.SimDt));
        }
        return res;
    }
}
