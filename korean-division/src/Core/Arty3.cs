using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// arty3 포병 · 중화기 묶음 (사용자 2026-10-21, docs/ROADMAP_LONG.md '3. 포병 · 중화기' · 9 · 13 · 14):
///  · 방렬 지연 · 방렬 난수 (Combat2.cs SetupTicksOf · DeployDelayTicksOf — 포마다 고정, 상태 없음).
///  · 방렬 자리 유지: 방렬한 대포 (공격 명령 아님) 는 사선 막힘으로 철수하지 않고, 사거리 밖 자동 표적은 gunOutOfRangeHoldSec 기다린 뒤 · 방렬 뒤 gunRepackCooldownSec 지나야 철수 · 추격.
///    사계 안이면 포신만 (기존 N10 ShiftTraverse), 사계를 크게 벗어나면 (traverseRedeployDeg) 공격 명령만 재방렬 (기존), 명시 이동 명령은 곧바로 철수 (기존).
///  · heavy 간격: 방렬 · 거치를 시작할 때 방렬/거치 (중) 아군 중화기와 heavyGapMin 칸 미만이면 옆으로 비켜서서 가능하면 heavyGapWant 칸까지 (상태 없음 — 최소 간격이 늘어나지 않으면 그 자리에서 방렬). 참호 자리 제외.
///  · 야전포 직사 + 간접 (weapon.spotterIndirect): 사선이 막혔거나 직사 표적이 없으면 관측 유닛이 보는 사거리 안 표적에 간접 사격.
///  · 곡사포 조정 사격 (weapon.adjustFire*): 같은 표적 구역 연속 사격 시 퍼짐이 발마다 줄어듦 — 상태 Unit.AdjustShots · AdjustAim (저장 · 체크섬, FireControl.cs 구역).
///  · 중기관총 최후 저지선 (FireMode.FinalLine, FireControl.cs): 사격선 띠 안의 적만 쏜다 — 상태 Unit.FinalLinePoint.
/// 모두 틱 · 입력 · 목록 순의 함수 — 결정적.
/// </summary>
public sealed partial class Unit
{
    /// <summary>arty3 곡사포 조정 사격: 같은 구역에 쏜 연속 발수 (0 = 없음).</summary>
    [SnapExt] internal int AdjustShots;
    /// <summary>arty3 곡사포 조정 사격: 마지막 조준점.</summary>
    [SnapExt] internal Vector2 AdjustAim;
    /// <summary>arty3 최후 저지선: 사격선 방향 지점 (FireMode.FinalLine 일 때만 의미).</summary>
    [SnapExt] public Vector2 FinalLinePoint { get; internal set; }
}

public sealed partial class World
{
    readonly List<Unit> heavyGapBuf = new();

    /// <summary>arty3 방렬 자리 유지 대상: 방렬 (중) 인 대포, 명시 공격 명령이 아님.</summary>
    internal bool GunKeepsPlace(Unit u) =>
        Defs.CombatRules.GunOutOfRangeHoldSec > 0 && u.CanDeploy && u.Def.Category == "gun" && u.Order != UnitOrder.Attack
        && u.Deploy is DeployPhase.Deployed or DeployPhase.Deploying;

    /// <summary>arty3 재방렬 냉각 중 (방렬을 마친 뒤 gunRepackCooldownSec 안, 방렬 중이면 항상). Deployed 동안 DeployLeft = 방렬 완료 틱 (Combat2.cs).</summary>
    internal bool GunRepackCooling(Unit u)
    {
        float cd = Defs.CombatRules.GunRepackCooldownSec;
        if (cd <= 0) return false;
        if (u.Deploy == DeployPhase.Deploying) return true;
        return u.Deploy == DeployPhase.Deployed && Tick - u.DeployLeft < (long)MathF.Round(cd / Config.SimDt);
    }

    /// <summary>arty3 야전포 간접: 사선이 없어도 아군 관측 유닛 (장교 · 정찰병) 이 지금 보는 표적이면 쏠 수 있다.</summary>
    internal bool SpotterIndirectOk(Unit u, Entity t, WeaponDef w) => w.SpotterIndirect && SeenBySpotter(u.Owner, t);

    /// <summary>
    /// arty3 heavy 간격: 이동 자세 중화기가 방렬 · 거치를 시작하려는 틱. 방렬 (중) 인 아군 중화기와 heavyGapWant 칸 미만이고 옆으로 비켜서면 최소 간격이 늘어나면
    /// 차체를 돌리지 않고 옆 (사계 중심에 수직, 가장 가까운 중화기 반대쪽) 으로 한 걸음 옮기고 true (그 틱은 방렬 시작 안 함). 늘어나지 않으면 (막힘 · 좁은 길 · 이미 충분) false.
    /// 참호 자리 · 중대 깃발 대형 밖 지형 제외. 상태 없음.
    /// </summary>
    internal bool HeavyGapStep(Unit u)
    {
        var c = Defs.CombatRules;
        if (c.HeavyGapWant <= 0 || !u.Def.Heavy || u.HasTrenchSpot || u.InTrench) return false;
        float want = MathF.Max(c.HeavyGapWant, c.HeavyGapMin);
        float gap = HeavyGap(u, u.Pos, want, out Vector2 near);
        if (gap >= want) return false;
        Vector2 fwd = new(DetMath.Cos(u.Facing), DetMath.Sin(u.Facing));
        Vector2 side = new(-fwd.Y, fwd.X);
        float s = Vector2.Dot(u.Pos - near, side);
        if (s == 0) s = (u.Id & 1) == 0 ? 1 : -1;
        Vector2 dir = s > 0 ? side : -side;
        float step = MathF.Max(0.01f, UnitSpeed(u) * c.HeavyGapSpeedMul * Config.SimDt);
        Vector2 to = u.Pos + dir * step;
        if (!GapCellFree(u, to + dir * u.Radius)) return false;
        if (HeavyGap(u, to, want, out _) <= gap + 1e-4f) return false;   // 간격이 늘지 않음 (양쪽에 중화기) — 그 자리에서 방렬
        u.Pos = to;
        u.Velocity = dir * (step / Config.SimDt);
        Stats.HeavyGapSteps++;
        return true;
    }

    /// <summary>p 에서 방렬 (중) 인 아군 중화기까지 가장 가까운 거리 (중심 간, radius 밖은 radius). near = 그 유닛 위치.</summary>
    float HeavyGap(Unit u, Vector2 p, float radius, out Vector2 near)
    {
        near = p;
        float best = radius;
        UnitsInRadius(p, radius + Defs.MaxUnitRadius, heavyGapBuf);
        for (int i = 0; i < heavyGapBuf.Count; i++)
        {
            var o = heavyGapBuf[i];
            if (ReferenceEquals(o, u) || !o.Alive || o.Owner != u.Owner || !o.Def.Heavy || !o.CanDeploy) continue;
            if (o.Deploy is not (DeployPhase.Deployed or DeployPhase.Deploying)) continue;
            float d = Vector2.Distance(p, o.Pos);
            if (d < best) { best = d; near = o.Pos; }
        }
        return best;
    }

    bool GapCellFree(Unit u, Vector2 p)
    {
        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y);
        return Map.IsWalkable(x, y);
    }

    /// <summary>
    /// arty3 곡사포 조정 사격: 퍼짐 배수. 앞 조준점에서 adjustFireZone 칸 안이면 연속 발수 +1, 아니면 1 부터 — 배수 = 1 − (1 − min) × (n − 1) / (shots − 1), 최소 min.
    /// 예 (shots 5, min 1/3): 9 → 7.5 → 6 → 4.5 → 3칸. 발사 때 한 번 호출 (상태 갱신).
    /// </summary>
    internal float AdjustFireMul(Entity shooter, Vector2 aim, WeaponDef w)
    {
        if (w.AdjustFireShots <= 1 || shooter is not Unit u) return 1f;
        if (u.AdjustShots > 0 && Vector2.DistanceSquared(aim, u.AdjustAim) <= w.AdjustFireZone * w.AdjustFireZone) u.AdjustShots = Math.Min(u.AdjustShots + 1, w.AdjustFireShots);
        else u.AdjustShots = 1;
        u.AdjustAim = aim;
        float min = Math.Clamp(w.AdjustFireMinMul, 0f, 1f);
        return 1f - (1f - min) * (u.AdjustShots - 1) / (w.AdjustFireShots - 1);
    }

    /// <summary>arty3 최후 저지선: p 가 u 의 사격선 (u → FinalLinePoint 방향, 사거리까지) 에서 finalLineBand 칸 안인가.</summary>
    internal bool OnFinalLine(Unit u, Vector2 p)
    {
        Vector2 d = u.FinalLinePoint - u.Pos;
        float len = d.Length();
        float band = Defs.CombatRules.FinalLineBand;
        if (len < 1e-3f) return Vector2.DistanceSquared(p, u.Pos) <= band * band;
        Vector2 dir = d / len;
        float range = u.Weapon?.Range ?? len;
        float t = Math.Clamp(Vector2.Dot(p - u.Pos, dir), 0f, range);
        return Vector2.DistanceSquared(p, u.Pos + dir * t) <= band * band;
    }
}
