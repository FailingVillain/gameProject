using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 행진 (돌격) 사격 지휘 (ROADMAP 1-1, 사용자 확정 2026-10-19, docs/COLLISION_ANALYSIS.md 행진 사격):
///  · 보병의 행진 사격은 **장교가 중대 단위로** 한 번에 시작 · 종료한다 — 중대 전원이 같은 순간 같은 속도 (×marchMoveMul).
///    장교 (중대 소속 장교 → 지휘 범위 안 같은 세력 장교, Squads.CompanyOfficer) 가 marchDecideTicks 마다 판단:
///    중대가 걷는 중 (행진 사격 가능 구성원의 1/3 이상이 이동 명령으로 걷는 중) + 장교 제압 &lt; marchMaxSuppress +
///    무게중심 앞쪽 (진행 방향 반평면) 유효 사격 반경 (+ 중대 퍼짐) 안에 보이는 적 → Company.MarchUntil = 지금 + marchHoldSec.
///    걷지 않으면 즉시 끝, 적이 안 보이면 marchHoldSec 뒤 끝.
///  · 분대 단위 (부사관) 는 드물게만: 중대에 지휘 장교가 없을 때, 분대장이 공식 부사관 · 구성원 marchNcoMinMembers 이상 · 절반 이상 걷는 중 ·
///    적이 유효 반경 × marchNcoRangeFrac 안 — 판단도 marchDecideTicks × 3 간격.
///  · 지휘자 없는 보병 (중대 · 분대 지휘 없음) 은 개별 행진 사격을 하지 않는다. 차량 (기관총 전차 · 장갑차) 은 승무원 지휘 — 예전처럼 개별.
///  · 중대 명령 카드 '돌격 사격' (Company.MarchFireOn, 기본 꺼짐) 이 켜져 있고 공격 이동 중일 때만 판단한다 (일반 이동 · 후퇴 중 없음). 전술 AI 는 공격 이동 명령에만 켜고 이동 · 속보에는 끈다.
///  · 행진 사격 중 적이 marchChargeRange 칸 (가장자리) 안이면 그 병사는 자동 돌격으로 전환.
///  · 돌격보병 (MarchFireDefault, N23 '난사로 누르고 근접 무기로 쓸어버리기'): 돌격 사격 카드가 켜진 공격 이동이면 지휘자 없이도 개별 행진 사격 · 표적을 잡아도 멈추지 않고 전진
///    (제압 &lt; marchMaxSuppress 동안), 6칸 안 권총 (Combat4 UpdateSidearm), marchChargeRange 안에서 수류탄 한 발 → 단검 돌격.
/// 상태 = Company.MarchUntil · Squad.MarchUntil (틱, ORG_ 저장 · 체크섬). 판단은 틱 · 상태의 함수 (중대 목록 순서) — 결정적.
/// </summary>
public sealed partial class World
{
    void StepMarchFire()
    {
        if (Companies.Count == 0) return;
        var c = Defs.CombatRules;
        int iv = Math.Max(1, c.MarchDecideTicks);
        for (int ci = 0; ci < Companies.Count; ci++)
        {
            var co = Companies[ci];
            bool due = (Tick + co.Id * 7L) % DecideInterval(co, iv) == 0, dueNco = (Tick + co.Id * 7L) % (iv * 3) == 0;   // 경험치 많은 지휘관은 더 자주 판단 (CommandRanks.cs)
            if (!due && !dueNco) continue;
            DecideCompanyMarch(co, c, due, dueNco);
        }
    }

    /// <summary>행진 사격을 할 수 있는 상태로 걷는 중인 병사 (이동 · 공격 이동 명령, 돌격 · 속보 · 참호 아님).</summary>
    bool MarchWalker(Unit u) =>
        u.Alive && u.Def.MarchFire && u.Weapon != null && !u.Charging && !u.QuickStep && !u.CanDeploy && u.Group != null && u.Velocity != Vector2.Zero
        && u.Order == UnitOrder.AttackMove && !u.HasTrenchSpot && !Map.IsTrench((int)MathF.Floor(u.Pos.X), (int)MathF.Floor(u.Pos.Y));

    void DecideCompanyMarch(Company co, CombatRulesDef c, bool due, bool dueNco)
    {
        if (!co.MarchFireOn)   // 돌격 사격 꺼짐 (기본) — 중대 · 분대 모두 행진 사격 없음
        {
            co.MarchUntil = 0; co.MarchAdvUntil = 0;
            foreach (var sq in co.Squads) sq.MarchUntil = 0;
            return;
        }
        var cen = Centroid(co.Members);
        var officer = CompanyOfficer(co, cen);
        if (officer == null)
        {
            co.MarchUntil = 0; co.MarchAdvUntil = 0;
            if (dueNco) foreach (var sq in co.Squads) DecideSquadMarch(sq, c);
            return;
        }
        foreach (var sq in co.Squads) sq.MarchUntil = 0;   // 장교 지휘 아래에서는 분대 단위 판단 없음
        if (!due) return;
        co.MarchAdvUntil = officer.Suppression < c.MarchMaxSuppress ? Tick + Math.Max(1, c.MarchDecideTicks) + 2 : 0;   // 건재한 장교 아래에서는 공격 이동 중 멈추지 않고 전진
        Unit? rep = null; int able = 0, walking = 0;
        Vector2 vel = Vector2.Zero;
        foreach (var u in co.Members)
        {
            if (!u.Alive || !u.Def.MarchFire || u.Weapon == null || u.Def.IsOfficer) continue;
            able++;
            if (!MarchWalker(u)) continue;
            walking++;
            vel += u.Velocity;
            rep ??= u;
        }
        if (rep == null || walking * 3 < able) { co.MarchUntil = 0; return; }   // 중대가 서 있음 → 즉시 끝
        if (officer.Suppression >= c.MarchMaxSuppress) return;                 // 제압당한 장교는 새로 명령하지 못함 (하던 것은 marchHoldSec 뒤 끝)
        Vector2 head = co.Anchor is { Arrived: false } a ? a.Heading : vel;
        if (EnemyAhead(co.Owner, co.Members, cen, head, MarchRadius(rep, rep.Weapon!, c))) co.MarchUntil = Tick + HoldTicks(c);
    }

    void DecideSquadMarch(Squad sq, CombatRulesDef c)
    {
        sq.MarchUntil = Tick < sq.MarchUntil ? sq.MarchUntil : 0;
        var lead = sq.Leader;
        if (lead == null || !lead.IsNco || sq.Members.Count < c.MarchNcoMinMembers || lead.Suppression >= c.MarchMaxSuppress) { sq.MarchUntil = 0; return; }
        Unit? rep = null; int walking = 0;
        Vector2 vel = Vector2.Zero;
        foreach (var u in sq.Members)
        {
            if (!MarchWalker(u)) continue;
            walking++; vel += u.Velocity; rep ??= u;
        }
        if (rep == null || walking * 2 < sq.Members.Count) { sq.MarchUntil = 0; return; }
        var cen = Centroid(sq.Members);
        if (EnemyAhead(sq.Owner, sq.Members, cen, vel, MarchRadius(rep, rep.Weapon!, c) * c.MarchNcoRangeFrac)) sq.MarchUntil = Tick + HoldTicks(c);
    }

    static long HoldTicks(CombatRulesDef c) => Math.Max(1, (long)MathF.Round(c.MarchHoldSec / Config.SimDt));

    /// <summary>무게중심 앞쪽 반평면 (head 방향) 에서 radius + 부대 퍼짐 안에 보이는 적이 있는가.</summary>
    bool EnemyAhead(int owner, List<Unit> members, Vector2 cen, Vector2 head, float radius)
    {
        float spread = 0;
        foreach (var u in members) if (u.Alive) spread = MathF.Max(spread, Vector2.Distance(u.Pos, cen));
        spread = MathF.Min(spread, 15f);
        if (FindTarget(owner, cen, radius + spread) is not { } t) return false;
        if (head.LengthSquared() < 1e-8f) return true;
        return Vector2.Dot(head, t.Pos - cen) > 0;
    }

    /// <summary>이 병사가 지휘받는 행진 사격 상태인가: 보병 = 소속 중대 (장교 판단) 또는 분대 (부사관 판단) 가 행진 사격 중. 차량은 승무원 지휘 (항상).</summary>
    internal bool MarchCommanded(Unit u)
    {
        if (!u.Def.MarchFire) return false;
        if (u.Def.Category != "infantry") return true;
        if (u.Company is not { } co || !co.MarchFireOn || u.Order != UnitOrder.AttackMove) return false;   // 켜져 있고 공격 이동 중일 때만 (일반 이동 · 후퇴 중에는 안 함)
        if (Tick < co.MarchUntil) return true;
        if (u.Squad is { } sq && Tick < sq.MarchUntil) return true;
        return u.Def.MarchFireDefault;   // 돌격보병 (N23): 걸으며 쏘기가 기본 훈련 — 장교 · 부사관 명령 없이도 개별 행진 사격
    }

    /// <summary>장교 (중대) · 부사관 (분대) 이 지금 행진 사격을 명령 중인가 (돌격보병 개별 행진 사격 제외).</summary>
    bool LeaderMarch(Unit u) => u.Company is { } co && (Tick < co.MarchUntil || u.Squad is { } sq && Tick < sq.MarchUntil);

    /// <summary>돌격 사격을 켠 중대의 보병 (공격 이동 중, 지휘 장교 건재): 표적을 잡아도 멈춰 서지 않고 대열과 함께 전진한다 — 적이 행진 사격 반경 안에 들면 장교가 행진 사격을 명령한다.</summary>
    internal bool MarchAdvancing(Unit u) =>
        u.Company is { MarchFireOn: true } co && (Tick < co.MarchAdvUntil || u.Def.MarchFireDefault && u.Suppression < Defs.CombatRules.MarchMaxSuppress) && u.Def.MarchFire && u.Def.Category == "infantry" && !u.Def.IsOfficer
        && !u.Charging && !u.CanDeploy && u.Weapon != null && u.Order == UnitOrder.AttackMove && u.Group != null;

    /// <summary>지휘받는 행진 사격으로 걷는 중 (보병: 표적 유무와 무관하게 중대 전원이 같은 속도 ×marchMoveMul). 차량은 표적을 잡았을 때만.</summary>
    internal bool MarchSlowed(Unit u)
    {
        if (u.Def.Category != "infantry") return u.MarchTarget != 0;
        return u.Def.MarchFire && !u.Charging && !u.QuickStep && u.Group != null && (u.Order == UnitOrder.Move || u.Order == UnitOrder.AttackMove)
            && u.Suppression < Defs.CombatRules.MarchMaxSuppress && !u.HasTrenchSpot && MarchCommanded(u)
            && (!u.Def.MarchFireDefault || u.MarchTarget != 0 || LeaderMarch(u));   // 돌격보병 개별 행진 사격 (N23) 은 표적을 잡았을 때만 느려진다
    }

    /// <summary>행진 사격 중 가까운 적 (가장자리 marchChargeRange 칸 안) → 자동 돌격. 10틱 간격 (Id 분산). 돌격하면 true.</summary>
    bool MarchCharge(Unit u) => (Tick + u.Id) % 10 == 0 && MarchChargeNow(u);

    /// <summary>MarchCharge 의 판단 본문 (10틱 간격 확인 뺌 — 테스트에서 직접 부름).</summary>
    internal bool MarchChargeNow(Unit u)
    {
        if (!u.CanCharge || !AutoChargeAllowed(u) || !OfficerChargeOk(u)) return false;   // N35: 장교 혼자 행진 사격 돌격 없음
        float r = Defs.CombatRules.MarchChargeRange;
        if (FindTarget(u.Owner, u.Pos, r + u.Radius, 0, null) is not Unit near || !CanMelee(u, near)) return false;
        if (near.EdgeDistance(u.Pos) - u.Radius > r || ChargeBlockedFleeing(u, near) || CavAutoChargeBlocked(u, near)) return false;   // 후퇴하는 적에게는 사격만 (Pursuit.cs)
        // N47: 돌격 허가 (ai412 결정 4 — 화력 우세 신호 · 엄호 확인 · 예비 타이머). 교전 시작 = 전술 틀 국면 틱, 없으면 공격 이동 명령 틱
        if (u.Company is { } mco)
        {
            var mates = new List<Unit>();
            foreach (var m in mco.Members) if (m.Alive && m.CanCharge && !m.Pinned) mates.Add(m);
            long since = DrillRules.MarchChargeSince != "order" && mco.Drill.Phase != DrillPhase.Idle ? mco.Drill.PhaseTick : u.LastOrderTick;   // tactics.json behaviour.marchChargeSince
            var why = AssaultClearance(u.Owner, Centroid(mco.Members), u.Weapon?.Range ?? 16f, near.Pos, mates, mates.Count, since);
            if (why == AssaultWhy.Blocked) { Stats.MarchChargeBlocked++; return false; }
            Stats.MarchChargeGated++;
        }
        // 돌격보병 (N23): 단검 돌격 직전 수류탄 한 발 (marchGrenadeFrac 비율의 병사, 이번 교전에서 아직 안 던졌으면 — 보유 = 최대) → 다음 판단 (10틱 뒤) 에 돌격
        if (u.Def.MarchFireDefault && u.Grenades > 0 && u.Grenades >= u.Def.Grenades && Vector2.Distance(u.Pos, near.Pos) <= Defs.Grenade.Range
            && StyleHash(u.Id, Map.Seed, 0x6E23) < u.Def.MarchGrenadeFrac)
        {
            ThrowGrenade(u, near.Pos);
            u.MarchTarget = 0;
            return true;
        }
        CountChargeAt(u, near);
        u.Target = near;
        u.ChargeState = AutoChargeState(near);
        BeginFix(u, false);   // N57
        u.LeashOrigin = u.Pos;
        u.MarchTarget = 0;
        ProbeOfficerCharge(u, "march", near);
        return true;
    }
}
