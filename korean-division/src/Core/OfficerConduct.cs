using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// N35 장교 교전 회피 (사용자 결정 2026-10-19): 장교의 일은 지휘이지 싸움이 아니다.
/// (1) 표적을 찾아 다가가지 않고, 권총은 자위로만 — 적이 combat.officerDefendRange 칸 안이거나, 장교 officerMeleeRadius 칸 안에서 자기 중대가 근접전 (돌격) 중일 때 (그때는 권총 사거리까지).
/// (2) 혼자 돌격하지 않는다 — 자동 돌격 (1칸) · 발밑 적 살피기 · 행진 사격 3칸 돌격 · 기병 '돌격만' · 돌격 명령 (AI 중대 · 틀 · 플레이어) 모두
///     중대 보병 (장교 제외) 의 officerChargeWithFrac 이상이 돌격 중일 때만 (명령은 같은 중대 병사와 함께 받을 때만). 중대 돌격이 끝나면 장교 돌격도 끝.
/// (3) 중대 돌격에 함께할 때도 돌격하는 병사들의 앞줄 + officerChargeLeadTol 칸을 넘어 달려 나가지 않는다 (PaceOfficerCharge).
/// combat.officerAvoid = false 면 옛 동작 (측정 대조용). 모두 틱마다 다시 계산 (저장 · 체크섬 없음), 결정적 (중대 구성원 목록 순).
/// </summary>
public sealed partial class World
{
    /// <summary>장교 중대의 보병 (장교 제외, 살아 있는) 중 돌격 중인 비율이 officerChargeWithFrac 이상 (적어도 1명).</summary>
    internal bool CompanyCharging(Unit o)
    {
        if (o.Company is not { } c) return false;
        int n = 0, ch = 0;
        foreach (var m in c.Members) { if (!m.Alive || m.IsOfficer) continue; n++; if (m.Charging) ch++; }
        return ch > 0 && ch >= n * Defs.CombatRules.OfficerChargeWithFrac;
    }

    /// <summary>이 유닛이 지금 돌격을 시작해도 되나 (장교가 아니면 항상 true).</summary>
    internal bool OfficerChargeOk(Unit u) => !u.IsOfficer || !Defs.CombatRules.OfficerAvoid || CompanyCharging(u);

    /// <summary>장교 officerMeleeRadius 칸 안에 돌격 (근접전) 중인 자기 중대 병사가 있다.</summary>
    internal bool CompanyMeleeNear(Unit o)
    {
        if (o.Company is not { } c) return false;
        float r = Defs.CombatRules.OfficerMeleeRadius; r *= r;
        foreach (var m in c.Members) if (m.Alive && !m.IsOfficer && m.Charging && Vector2.DistanceSquared(m.Pos, o.Pos) <= r) return true;
        return false;
    }

    /// <summary>장교 자위 사격 반경 (가장자리 거리): 중대가 곁에서 근접전 중이면 무기 사거리, 아니면 min(사거리, officerDefendRange).</summary>
    internal float OfficerFireRange(Unit u)
    {
        float wr = u.Weapon?.Range ?? 0;
        if (!Defs.CombatRules.OfficerAvoid) return wr;
        return CompanyMeleeNear(u) ? wr : MathF.Min(wr, Defs.CombatRules.OfficerDefendRange);
    }

    // ---------------------------------------------------------------- 정찰병 자위 사격 (2026-10-19 사용자 추가 결정): 정찰병의 일은 관측 — 먼저 쏘지 않는다

    /// <summary>먼저 교전하지 않는 유닛: 장교 (돌격 중 제외) · 정찰병 (combat.scoutAvoid). 표적 찾기 = 자위 반경만, 추격 · 접근 없음.</summary>
    internal bool PassiveFire(Unit u) => OfficerPassive(u) || (u.Def.Scout && Defs.CombatRules.ScoutAvoid && !u.Charging && u.Weapon != null);

    /// <summary>자위 사격 반경: 장교 = OfficerFireRange, 정찰병 = min(사거리, scoutDefendRange).</summary>
    internal float PassiveFireRange(Unit u) => u.IsOfficer ? OfficerFireRange(u) : MathF.Min(u.Weapon?.Range ?? 0, Defs.CombatRules.ScoutDefendRange);

    /// <summary>
    /// 자위 반격 허용: 사격자가 자위 반경 안이면 언제나. 정찰병은 그 밖이라도 접촉을 끊을 수 없을 때 (멈춰 있음 — 엄폐 · 관측 자리에서 계속 맞음) scoutRetaliateRange 안이면 반격.
    /// </summary>
    bool PassiveRetaliate(Unit u, Entity attacker)
    {
        float d = attacker.EdgeDistance(u.Pos) - u.Radius;
        if (d <= PassiveFireRange(u)) return true;
        if (u.IsOfficer) return false;   // 장교: 자위 반경 (avoid 끔이면 사거리) 밖은 반격 없음
        return u.Velocity == Vector2.Zero && u.Group == null && d <= Defs.CombatRules.ScoutRetaliateRange;
    }

    /// <summary>돌격 명령 목록에서 장교를 넣어도 되나: 같은 중대의 돌격 가능한 병사가 함께 명령받을 때만.</summary>
    bool OfficerJoinsCharge(Unit u, List<Unit> list)
    {
        if (!u.IsOfficer || !Defs.CombatRules.OfficerAvoid) return true;
        if (u.Company is not { } c) return false;
        foreach (var m in list) if (!m.IsOfficer && m.Company == c && m.CanCharge && m.Alive) return true;
        return false;
    }

    /// <summary>중대 돌격이 끝났으면 장교 돌격도 끝 (자동 · 명령 모두 — 명령 돌격이면 이동은 공격 이동으로 이어 간다).</summary>
    void OfficerChargeEnd(Unit u)
    {
        if (!u.IsOfficer || !u.Charging || !Defs.CombatRules.OfficerAvoid || CompanyCharging(u)) return;
        u.ChargeState = 0;
        Stats.OfficerChargeEnded++;
    }

    /// <summary>돌격 중인 장교: 이번 틱 진행 방향으로 돌격하는 자기 중대 병사들의 앞줄 + officerChargeLeadTol 을 넘었으면 이동을 되돌린다.</summary>
    void PaceOfficerCharge(Unit u, Vector2 p0)
    {
        if (!Defs.CombatRules.OfficerAvoid || u.Company is not { } c) return;
        var mv = u.Pos - p0;
        if (mv.LengthSquared() < 1e-10f) return;
        var dir = Vector2.Normalize(mv);
        float front = float.MinValue;
        foreach (var m in c.Members) if (m.Alive && !m.IsOfficer && m.Charging) front = MathF.Max(front, Vector2.Dot(m.Pos, dir));
        if (front == float.MinValue || Vector2.Dot(u.Pos, dir) <= front + Defs.CombatRules.OfficerChargeLeadTol) return;
        u.Pos = p0;
        u.Velocity = Vector2.Zero;
        Stats.OfficerChargeHeld++;
    }

    // ---------------------------------------------------------------- [측정 전용] 장교 돌격 · 권총 사격 탐침 (시뮬은 읽지 않음)

    /// <summary>장교 중대 보병 (장교 제외) 무게중심까지 거리, 중대가 없거나 병사가 없으면 -1.</summary>
    internal float OfficerBodyDist(Unit o)
    {
        if (o.Company is not { } c) return -1;
        Vector2 s = Vector2.Zero; int n = 0;
        foreach (var m in c.Members) if (m.Alive && !m.IsOfficer) { s += m.Pos; n++; }
        return n == 0 ? -1 : Vector2.Distance(o.Pos, s / n);
    }

    void ProbeOfficerCharge(Unit u, string path, Entity? t)
    {
        if (!u.IsOfficer || !u.Charging) return;
        Stats.AddOfficer(Stats.OffCharge, path, !CompanyCharging(u), OfficerBodyDist(u), t != null ? t.EdgeDistance(u.Pos) - u.Radius : -1);
    }

    void ProbeOfficerShot(Unit u, Entity t, float dist)
    {
        if (u.Charging) return;
        string path = u.MarchShot ? "march" : u.Order.ToString();
        if (u.IsOfficer)
        {
            bool defence = dist <= Defs.CombatRules.OfficerDefendRange + 1e-3f || CompanyMeleeNear(u);
            Stats.AddOfficer(Stats.OffShot, path, !defence, OfficerBodyDist(u), dist);
        }
        else if (u.Def.Scout)
        {
            bool defence = dist <= Defs.CombatRules.ScoutDefendRange + 1e-3f || (UnderFire(u) && dist <= Defs.CombatRules.ScoutRetaliateRange + 1e-3f);
            Stats.AddOfficer(Stats.ScoutShot, path, !defence, -1, dist);
        }
    }
}
