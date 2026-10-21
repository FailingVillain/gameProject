using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>F4: 기병 돌격 제한 — 형성된 제압 안 된 정면 보병에는 자동 돌격 안 함, 측면 · 후방 · 후퇴 · 제압 · 고립 · 무방비 승무원이면 허용. 새 상태 없음.</summary>
public static class F4SelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            var defs = Defs.Current;
            var C = defs.Tactics.Cavalry;
            check(C.StrictCharge && C.RechargeCooldownSec > 0 && C.AutoMinTargetSupp > 0 && C.IsolatedMax >= 0, $"F4 tactics.json cavalry 설정 (strict {C.StrictCharge}, 냉각 {C.RechargeCooldownSec}s)");
            var w = TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(100, 100, 7)), defs));
            w.Fog.Enabled = false;
            var (cx, cy) = w.Map.NearestWalkable(50, 50);
            var o = new Vector2(cx + 0.5f, cy + 0.5f);
            var cav = w.SpawnUnit(defs.Units["cavalry_default"], 0, o + new Vector2(0, 3));   // 표적 아래 (+y) 쪽
            var front = w.SpawnUnit(defs.Units["infantry"], 1, o);
            front.Facing = MathF.PI / 2;   // +y 를 봄 = 기병 쪽 (정면)
            for (int i = 1; i <= 5; i++) w.SpawnUnit(defs.Units["infantry"], 1, o + new Vector2(i * 0.8f - 2.4f, -0.5f));   // 둘레 아군 (고립 아님)
            check(w.CavChargeWhy(cav, front) == 0 && w.CavAutoChargeBlocked(cav, front), "F4 형성 · 제압 안 된 정면 보병 = 자동 돌격 막음");
            front.Facing = -MathF.PI / 2;   // 등을 돌림
            check(w.CavChargeWhy(cav, front) == 5 && !w.CavAutoChargeBlocked(cav, front), "F4 후방 = 허용");
            front.Facing = 0;   // 옆을 봄
            check(w.CavChargeWhy(cav, front) == 5, "F4 측면 = 허용");
            front.Facing = MathF.PI / 2;
            front.Suppression = (short)(defs.CombatRules.SuppressMax * 0.8f);
            check(w.CavChargeWhy(cav, front) == 2, "F4 크게 제압된 표적 = 허용");
            front.Suppression = 0; front.Retreating = true; front.Order = UnitOrder.Move;
            check(w.CavChargeWhy(cav, front) == 1, "F4 후퇴 중 표적 = 허용");
            front.Retreating = false; front.Order = UnitOrder.Hold;
            cav.Suppression = (short)(C.AutoMaxOwnSupp + 1); front.Facing = -MathF.PI / 2;
            check(w.CavAutoChargeBlocked(cav, front), "F4 기병 자신이 제압되면 (꺾인 직후) 자동 돌격 냉각");
            cav.Suppression = 0;
            bool keep = C.StrictCharge;
            try { C.StrictCharge = false; front.Facing = MathF.PI / 2; check(!w.CavAutoChargeBlocked(cav, front), "F4 strictCharge=false = S4 동작"); }
            finally { C.StrictCharge = keep; }
            var lone = w.SpawnUnit(defs.Units["infantry"], 1, o + new Vector2(30, 30)); lone.Facing = MathF.PI / 2;
            var cav2 = w.SpawnUnit(defs.Units["cavalry_default"], 0, lone.Pos + new Vector2(0, 3));
            check(w.CavChargeWhy(cav2, lone) == 4, "F4 고립 소수 = 허용");
        }
        catch (Exception e) { check(false, $"F4 테스트 예외: {e.GetType().Name}: {e.Message}"); }
    }
}
