using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>S2: 소형 중대 (기관총 · 돌격 · 기병) 도 분대마다 부사관 분대장 → 장교 전사 뒤 ActingNco 가 지휘. KD_SELFTEST_ONLY=s2.</summary>
public static class S2SelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        Console.WriteLine("[s2 소형 중대 부사관 분대장]");
        try
        {
            var w = World.CreateMissionWorld(MissionDef.Load("ai_kr_vs_ft_1000"), defs, 1);
            int cos = 0, noNco = 0, mg = 0;
            foreach (var c in w.Companies)
            {
                if (c.Commander == null) continue;
                cos++;
                bool hasMg = c.Members.Exists(u => u.Def.Key == "heavy_maxim");
                if (hasMg) mg++;
                if (w.ActingNco(c) == null) noNco++;
                if (c.Members.Count(u => u.IsOfficer) <= 1) check(c.Squads.All(q => q.Leader is { IsNco: true } && (q.Members.Count < 4 || q.Members.Count(u => u.IsNco) >= 2)), $"소형 중대 {c.Id} 분대 {c.Squads.Count}개 모두 부사관 2명 (분대장 + 부분대장)");
            }
            check(mg >= 6 && noNco == 0, $"장교 있는 중대 {cos}개 (기관총 {mg}) 중 부사관 대리 후보 없는 중대 {noNco}");
            var mgc = w.Companies.First(c => c.Owner == 1 && c.Members.Exists(u => u.Def.Key == "heavy_maxim"));
            mgc.Commander!.Hp = 0;
            w.Step();
            check(mgc.Commander == null && w.ActingNco(mgc) != null, "기관총 중대 장교 전사 → 부사관 대리 있음");
            var lead = w.ActingNco(mgc)!;
            lead.Hp = 0;
            w.Step();
            check(w.ActingNco(mgc) != null && w.ActingNco(mgc) != lead, "분대장 부사관도 전사 → 부분대장 부사관이 대리");
        }
        catch (Exception e) { check(false, $"s2 테스트 중 예외: {e.GetType().Name}: {e.Message}"); }
    }
}
