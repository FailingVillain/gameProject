using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// [N68] 기수 (연대기, 전 세력 공용 `standard_bearer`): 깃발이 보이는 반경 (combat.bearerRadius) 안 같은 세력 병사는 붕괴 확률이 줄고
/// (BearerBreakMul, BreakChanceOf), AI 중대의 제압 후퇴 굴림도 중대에 산 기수가 있으면 줄어든다 (BearerRetreatMul).
/// 기수가 쓰러지면 (RemoveDead) 반경 안 같은 세력 병사에게 BearerFellSec 초 동안 BearerFellUntil 표시 → 붕괴 · 후퇴 굴림이 오히려 늘어난다 (BearerFellMul).
/// 붕괴 (breakChance) 가 꺼져 있으면 붕괴 쪽 효과는 없고 AI 후퇴 굴림만 영향. 상태 = Unit.BearerFellUntil (저장 · 체크섬, FormatVersion 43).
/// </summary>
public sealed partial class World
{
    /// <summary>u 근처 (반경 안) 같은 세력의 산 기수가 있는가.</summary>
    public bool NearBearer(Unit u)
    {
        if (bearers.Count == 0) return false;
        float r = Defs.CombatRules.BearerRadius, r2 = r * r;
        for (int i = 0; i < bearers.Count; i++)
        {
            var b = bearers[i];
            if (ReferenceEquals(b, u) || !b.Alive || b.Owner != u.Owner) continue;
            if (Vector2.DistanceSquared(b.Pos, u.Pos) <= r2) return true;
        }
        return false;
    }

    /// <summary>붕괴 · 후퇴 배수: 기수가 근처에 있으면 aliveMul (&lt; 1), 기수가 근처에서 쓰러진 지 얼마 안 됐으면 BearerFellMul (&gt; 1), 둘 다면 쓰러짐이 우선.</summary>
    public float BearerMoraleMul(Unit u, float aliveMul)
    {
        if (Tick < u.BearerFellUntil) return Defs.CombatRules.BearerFellMul;
        return NearBearer(u) ? aliveMul : 1f;
    }

    /// <summary>AI 중대 제압 후퇴 굴림 배수: 중대원 중 쓰러짐 표시가 있으면 BearerFellMul, 산 기수가 있으면 BearerRetreatMul.</summary>
    public float BearerRetreatMulOf(Company? co)
    {
        if (co == null) return 1f;
        bool alive = false;
        foreach (var m in co.Members)
        {
            if (!m.Alive) continue;
            if (Tick < m.BearerFellUntil) return Defs.CombatRules.BearerFellMul;
            if (m.Def.Bearer) alive = true;
        }
        return alive ? Defs.CombatRules.BearerRetreatMul : 1f;
    }

    /// <summary>기수 전사 (RemoveDead): 반경 안 같은 세력 산 병사에게 쓰러짐 표시.</summary>
    void BearerFell(Unit dead)
    {
        var c = Defs.CombatRules;
        if (c.BearerFellSec <= 0f) return;
        long until = Tick + (long)MathF.Round(c.BearerFellSec / Config.SimDt);
        float r2 = c.BearerRadius * c.BearerRadius;
        foreach (var u in Units)
            if (u.Alive && u.Owner == dead.Owner && !u.Def.Bearer && Vector2.DistanceSquared(u.Pos, dead.Pos) <= r2) u.BearerFellUntil = until;
    }
}
