using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 수비형 AI (방향 변경 §0-1: 생산 · 건설 없음): 주어진 병력을 기지 앞에 두고, 기지 근처가 공격받으면 반격하러 나갈 뿐
/// 먼저 공격하거나 점령하지 않는다. 기지에서 너무 멀어진 추격은 중단하고 돌아온다.
/// </summary>
public sealed class PassiveAi : AiBase<AiBaseState>
{
    public override string Name => "passive";

    protected override void Think(World w, AiBaseState s)
    {
        if (!Defend(w, s, s.Army, baseRadius: 24, pullRadius: 40, reactionDelay: 0.5f))
            ReturnToGather(w, s, s.Army, 12);
        Leash(w, s, s.Army, s.Home, 26, s.Gather);
    }
}
