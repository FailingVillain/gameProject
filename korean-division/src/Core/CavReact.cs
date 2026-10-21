using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 보병 대기병 조기 사격 (n61, 사용자 승인): 돌격 중이거나 빠르게 다가오는 말 탄 기병은 보병의 우선 위협.
/// 보병 (직사 소총 · 기관총 등, 장교 제외) 은 이런 기병을 무기 사거리 끝 (engageFrac 1.0) 에서 바로 표적으로 삼고 (현재 표적보다 우선),
/// 첫 사격 지연 (N34 firstShot · 표적 탐색 주기) 대신 짧은 조준 (cavReactAimSec) 만 쓴다. 추가 상태 없음 — 틱마다 기병 목록을 다시 만든다 (결정적: Units 순서).
/// JSON combat.cavReact* — cavReactOn · cavReactAimSec · cavFastSpeed · cavReactInterval.
/// </summary>
public sealed partial class World
{
    long cavThreatTick = -1;
    readonly List<Unit> cavThreats = new();

    /// <summary>돌격 중이거나 cavFastSpeed 이상으로 달리는, 말 위의 기병.</summary>
    internal bool IsCavThreat(Entity? e)
    {
        if (e is not Unit c || !c.Alive || !c.Def.Mounted || c.Dismounted || c.MountLeft > 0) return false;
        return c.Charging || c.Velocity.LengthSquared() >= Defs.CombatRules.CavFastSpeed * Defs.CombatRules.CavFastSpeed;
    }

    List<Unit> CavThreats()
    {
        if (cavThreatTick == Tick) return cavThreats;
        cavThreatTick = Tick; cavThreats.Clear();
        foreach (var c in Units) if (IsCavThreat(c)) cavThreats.Add(c);
        return cavThreats;
    }

    /// <summary>표적 t 에 대한 반응 거리: 위협 기병이면 사거리 끝, 아니면 평소 (ReactRange).</summary>
    internal float ReactRangeVs(Unit u, Entity t) => Defs.CombatRules.CavReactOn && u.Def.IsInfantry && IsCavThreat(t) && u.Weapon != null ? u.Weapon.Range : ReactRange(u);

    /// <summary>위협 기병 표적 전환 (UpdateCombat 에서 사격 · 공격 이동 · 대기 중인 보병마다).</summary>
    void CavReact(Unit u)
    {
        var cr = Defs.CombatRules;
        if (!cr.CavReactOn || u.Weapon is not { } w || !u.Def.IsInfantry || u.Def.IsOfficer || u.Charging || u.CanDeploy || IsIndirect(w) || !u.HasAmmo) return;
        if (u.Order is not (UnitOrder.Idle or UnitOrder.Hold or UnitOrder.AttackMove)) return;
        if ((Tick + u.Id) % Math.Max(1, cr.CavReactInterval) != 0 || IsCavThreat(u.Target)) return;
        var list = CavThreats();
        if (list.Count == 0) return;
        Unit? best = null; float bd = float.MaxValue;
        foreach (var c in list)
        {
            if (!IsHostile(u.Owner, c.Owner)) continue;
            float d = c.EdgeDistance(u.Pos) - u.Radius;
            if (d > w.Range || d < w.MinRange || d >= bd) continue;
            if (!Fog.CanSee(u.Owner, c)) continue;
            if (w.Los && (!Map.HasFiringLine(u.Pos, c.Pos, u.Id, c.Id) || SmokeBlocks(u.Pos, c.Pos))) continue;
            (best, bd) = (c, d);
        }
        if (best == null) return;
        u.Target = best; u.LeashOrigin = u.Pos; MarkAlert(u.Owner, u.Pos);
        Stats.CavReactSwitches++;
    }
}
