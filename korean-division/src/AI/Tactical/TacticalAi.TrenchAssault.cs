using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// N32 참호 돌격 (사용자 승인 2026-10-20 "AI 역시 이 순서를 사용한다"): 공격 과제 소총 중대가 보이는 적 참호선 (참호 안 방어병) 을 만나면 플레이어와 같은 명령
/// (World.TrenchAssault) 을 낸다 — 순서 · 수치는 World 쪽 (TrenchAssault.cs · tactics.json trenchAssault ai*), 프로필 tactics.trenchAssault 로 켜고 끈다.
/// 돌격 중에는 이 중대에 다른 명령을 내지 않는다 (순서가 끝나거나 후퇴 명령이 오면 이어서).
/// </summary>
public sealed partial class TacticalAi
{
    readonly List<Unit> taBuf = new();

    /// <summary>true = 이 틱 이 중대는 더 판단하지 않는다 (참호 돌격 중이거나 방금 시작).</summary>
    bool TrenchAssaultThink(World w, TacFaction s, TacGroup g)
    {
        if (g.CompanyId == 0) return false;
        var co = w.FindCompany(g.CompanyId);
        if (co == null) return false;
        // 기동 계획 (stage 3A 고정 · 기동 제대) · 도하 · 역습 중인 중대는 그 계획대로 (계획이 뒤늦게 정해지면 그 명령이 돌격을 취소한다)
        if (g.Element != ManElement.None || g.CrossStage != 0 || g.CounterAssaultTick != 0) return false;
        if (co.TrenchAssaultStage != TaStage.None)
        {
            if (g.State != TacState.Engage) { g.State = TacState.Engage; g.StateTick = w.Tick; }
            g.SquadBound = 0;
            return true;
        }
        var R = w.TrenchAssaultRules;
        if (!R.Enabled || !R.AiEnabled || !s.Profile.Tactics.TrenchAssault || g.Task != TacTask.Attack) return false;
        if (g.State is TacState.Fallback or TacState.Reserve or TacState.Assault || w.Tick < co.TA.CooldownUntil) return false;

        if (mem.Count < R.AiMinMen) return false;
        // 보이는 적 참호 방어병 중 가장 가까운 것
        w.UnitsInRadius(g.Center, R.AiTriggerDist, taBuf);
        Unit? best = null; float bd = float.MaxValue;
        foreach (var u in taBuf)
        {
            if (!u.Alive || u.Surrendered || !u.InTrench || !w.IsHostile(s.Faction, u.Owner) || !w.Fog.CanSee(s.Faction, u)) continue;
            float d = Vector2.Distance(u.Pos, g.Center);
            if (d < bd) { bd = d; best = u; }
        }
        if (best == null || bd < R.AiMinDist) return false;
        int defenders = 0;
        foreach (var u in taBuf)
            if (u.Alive && !u.Surrendered && u.InTrench && w.IsHostile(s.Faction, u.Owner) && w.Fog.CanSee(s.Faction, u) && Vector2.DistanceSquared(u.Pos, best.Pos) <= 14f * 14f) defenders++;
        if (defenders < R.AiMinDefenders || mem.Count < defenders * R.AiMinOdds) return false;
        if (!w.TrenchAssault(s.Faction, new[] { co.Id }, best.Pos, ai: true)) return false;
        w.Stats.TaAiStarts++;
        g.State = TacState.Engage; g.StateTick = w.Tick; g.OrderTick = w.Tick; g.SquadBound = 0;
        s.Orders++;
        return true;
    }
}
