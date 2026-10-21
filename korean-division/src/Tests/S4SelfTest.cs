using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>S4: 돌격 중 제압이 chargeBreakSuppress(기병 Cav) 이상이면 돌격 중단 (유예 후). 새 상태 없음.</summary>
public static class S4SelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            var c = Defs.Current.CombatRules;
            check(c.ChargeBreakSuppressCav > 0 && c.ChargeBreakGraceSec >= 0, $"S4 balance chargeBreakSuppressCav {c.ChargeBreakSuppressCav} 설정됨");
            var w = TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(100, 100, 7)), Defs.Current));
            w.Fog.Enabled = false;
            var (cx, cy) = w.Map.NearestWalkable(50, 50);
            var u = w.SpawnUnit(Defs.Current.Units["cavalry_default"], 0, new Vector2(cx + 0.5f, cy + 0.5f));
            int graceTicks = (int)MathF.Ceiling(c.ChargeBreakGraceSec / Config.SimDt) + 1;
            // 1) 제압 낮음 → 유지
            u.ChargeState = 2; u.ChargeRun = graceTicks + 5; u.Suppression = (short)(c.ChargeBreakSuppressCav - 10);
            w.UpdatePinnedForTest(u);
            check(u.ChargeState == 2, "S4 기준 미만 제압 = 돌격 유지");
            // 2) 유예 안 = 유지
            u.Suppression = (short)c.ChargeBreakSuppressCav; u.ChargeRun = 0;
            w.UpdatePinnedForTest(u);
            check(u.ChargeState == 2, "S4 돌격 시작 직후 유예 = 유지");
            // 3) 기준 이상 + 유예 뒤 = 중단 + 집계
            u.ChargeRun = graceTicks; long before = w.Stats.ChargesBroken;
            w.UpdatePinnedForTest(u);
            check(u.ChargeState == 0 && w.Stats.ChargesBroken == before + 1, "S4 제압 기준 이상 = 돌격 중단 + 카운터");
            // 4) 돌격 아니면 변화 없음
            w.UpdatePinnedForTest(u);
            check(w.Stats.ChargesBroken == before + 1, "S4 돌격 아님 = 집계 안 함");
        }
        catch (Exception e) { check(false, $"S4 테스트 예외: {e.GetType().Name}: {e.Message}"); }
    }
}
