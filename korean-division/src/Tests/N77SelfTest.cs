using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>N77: 모든 미션 파일이 읽히고 생성되며 ~300틱 예외 없이 도는지 + 세력별 병력 상한 + 관전 미션 존재. KD_SELFTEST_ONLY=n77.</summary>
public static class N77SelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        Console.WriteLine("[N77 미션 전체 로드 + 300틱]");
        var ids = MissionDef.Available().ToList();
        foreach (var need in new[] { "trench_assault", "forest_ambush", "artillery_spotters", "ai_kr_vs_jp_trench" })
            check(ids.Contains(need), $"미션 목록에 '{need}'");
        int spectate = 0;
        foreach (var id in ids)
        {
            try
            {
                var d = MissionDef.Load(id);
                if (d.Spectate) spectate++;
                var w = World.CreateMissionWorld(d, defs);
                var perFaction = w.Units.GroupBy(u => u.Owner).Select(g => g.Count()).ToList();
                bool cap = perFaction.All(n => n <= 1000);
                if (id is "trench_assault" or "forest_ambush" or "artillery_spotters" or "ai_kr_vs_jp_trench") cap = perFaction.All(n => n <= 800);
                for (int i = 0; i < 300; i++) w.Step();
                check(cap && w.Units.Count > 0, $"미션 '{id}': 300틱 예외 없음, 세력별 {string.Join("/", perFaction)}기{(d.Spectate ? " (관전)" : "")}");
            }
            catch (Exception e) { check(false, $"미션 '{id}' 실패: {e.GetType().Name}: {e.Message}"); }
        }
        check(spectate >= 2, $"관전 미션 {spectate}개 (목록에 자동 표시)");
    }
}
