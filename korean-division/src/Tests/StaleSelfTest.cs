using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>④ 교착 해소 (stale 2026-10-20): 프로필 stalemateSeconds · 교착 단계 N/2N/3N. KD_SELFTEST_ONLY=stale.</summary>
public static class StaleSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[stale 교착 해소]");
        var d = AiProfiles.Find("default")!;
        check(d.Tactics.StalemateSeconds == 30f, "default 프로필 stalemateSeconds = 30 (ai_profiles.json)");
        long n = (long)MathF.Round(30f / Config.SimDt);
        check(TacticalAi.StaleLevel(1000, 1000, 30f) == 0 && TacticalAi.StaleLevel(1000 + n - 1, 1000, 30f) == 0, "접촉 직후 · N초 전 = 교착 0단계");
        check(TacticalAi.StaleLevel(1000 + n, 1000, 30f) == 1 && TacticalAi.StaleLevel(1000 + 2 * n, 1000, 30f) == 2 && TacticalAi.StaleLevel(1000 + 3 * n, 1000, 30f) == 3, "N · 2N · 3N 초 = 1 · 2 · 3단계");
        check(TacticalAi.StaleLevel(1000 + 10 * n, 1000, 0f) == 0, "stalemateSeconds 0 = 끔");
    }
}
