using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T19c 캠페인 홈: 연대 배속 (Q9, 연대 1~9 · 0 = 직할) 규칙 · 저장 왕복 · 옛 파일 기본값 · 범위 밖 값 정리, 투입 비용 공용 함수.
/// SaveCompatSelfTest 에서 부른다 (KD_SELFTEST_ONLY=savecompat). 디스크를 쓰지 않는다 (ToJson / FromJson).
/// </summary>
public static class CampaignHomeSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        // 옛 파일 (Regiment 없음) → 모두 직할 (0)
        var old = CampaignState.FromJson("{\"Version\":3,\"Roster\":[{\"Id\":\"b1_c1\",\"BattalionNumber\":1},{\"Id\":\"mg\",\"BattalionNumber\":0}]}");
        check(old != null && old.Roster.Count == 2 && old.Roster.All(c => c.Regiment == 0) && old.Version == CampaignState.SchemaVersion,
            "캠페인 홈: 옛 저장 (연대 필드 없음) 은 모두 직할 (0) 로 열린다, 스키마 번호 그대로");

        // 대대 단위 배속 · 직할 중대 하나씩 · 범위 밖 거부
        var s = new CampaignState();
        s.Roster.Add(new CampaignState.RosterCompany { Id = "b2_c1", BattalionNumber = 2, Name = "제1중대" });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "b2_c2", BattalionNumber = 2, Name = "제2중대" });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "cav", BattalionNumber = 0, Kind = "cavalry", Name = "기병중대" });
        int bn = CampaignRegiments.Attach(s, "b1_c2", 3);             // 1대대 3개 중대 → 3연대
        int bn2 = CampaignRegiments.Attach(s, "b2_c1", 9);            // 2대대 2개 중대 → 9연대
        int direct = CampaignRegiments.Attach(s, "mg", 1);            // 직할 기관총중대 하나만
        bool rejected = CampaignRegiments.Attach(s, "mg", 10) == 0 && CampaignRegiments.Attach(s, "mg", -1) == 0 && CampaignRegiments.Attach(s, "nope", 2) == 0;
        bool battalionWide = s.Roster.Where(c => c.BattalionNumber == 1).All(c => c.Regiment == 3) && s.Roster.Where(c => c.BattalionNumber == 2).All(c => c.Regiment == 9);
        bool directOnly = s.Roster.First(c => c.Id == "mg").Regiment == 1 && s.Roster.First(c => c.Id == "cav").Regiment == 0;
        bool step = CampaignRegiments.Step(0, -1) == 9 && CampaignRegiments.Step(9, 1) == 0 && CampaignRegiments.Step(4, 1) == 5;
        check(bn == 3 && bn2 == 2 && direct == 1 && rejected && battalionWide && directOnly && step,
            $"캠페인 홈: 연대 배속 대대 단위 {bn}/{bn2}, 직할 하나씩 {direct}, 범위 밖 · 모르는 id 거부 {rejected}, 0↔9 순환 {step}");

        // 연대 1~9 저장 왕복 (각 연대에 하나씩) + 범위 밖 값은 직할로 정리
        var r = new CampaignState();
        r.Roster.Clear();
        for (int i = 1; i <= CampaignRegiments.Max; i++)
            r.Roster.Add(new CampaignState.RosterCompany { Id = $"x{i}", BattalionNumber = 10 + i, Regiment = i });
        var back = CampaignState.FromJson(r.ToJson());
        int rt = back == null ? 0 : Enumerable.Range(1, CampaignRegiments.Max).Count(i => back.Roster.FirstOrDefault(c => c.Id == $"x{i}")?.Regiment == i);
        r.Roster[0].Regiment = -3; r.Roster[8].Regiment = 12;   // 손상 · 수작업 편집된 값
        var bad = CampaignState.FromJson(r.ToJson());
        bool sanitized = bad != null && bad.Roster.First(c => c.Id == "x9").Regiment == 0 && bad.Roster.First(c => c.Id == "x1").Regiment == 0 && bad.Roster.First(c => c.Id == "x5").Regiment == 5;
        var counts = CampaignRegiments.CompanyCounts(back!);
        check(rt == CampaignRegiments.Max && sanitized && counts.Skip(1).All(n => n == 1) && counts[0] == 0,
            $"캠페인 홈: 연대 1~9 저장 왕복 {rt}/9, 범위 밖 (12 · -3) → 직할 {sanitized}");

        // 투입 비용 공용 함수 (BeginOperation · 병참 미리보기) = 대규모 투입이면 규모 배수
        var e = CampaignEconomyDefs.Current;
        var big = new List<CampaignState.RosterCompany> { new() { Kind = "infantry", Strength = e.LargeDeployThreshold + 1 } };
        var small = new List<CampaignState.RosterCompany> { new() { Kind = "infantry", Strength = 10 } };
        bool cost = Math.Abs(CampaignEconomy.DeployCost(big, 500) - (e.LargeDeployThreshold + 1) * e.LargeDeployScaleMul) < 0.01f
            && Math.Abs(CampaignEconomy.DeployCost(small, 500) - 10f) < 0.01f && Math.Abs(CampaignEconomy.DeployCost(big, 0) - (e.LargeDeployThreshold + 1)) < 0.01f;
        check(cost, "캠페인 홈: 투입 비용 = 병력 × 병종 단가 × (대규모면 규모 배수), 미리보기 · 작전 개시 같은 함수");
    }
}
