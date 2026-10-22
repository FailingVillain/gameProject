using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T42a 피로 · 사기 계정: v3 -> v4 이행 · v4 왕복 · 정산 증감 (투입 + / 미투입 −, 수치는 JSON 임시) · 홈 표시 자료 읽기 전용. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignFatigueSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        var E = CampaignEconomyDefs.Current;

        // 1) v3 저장 (피로 · 사기 필드 없음) -> v4 자동 이행: 피로 0 · 사기 기본값, 범위 밖 값은 자른다
        var v3 = CampaignState.FromJson("{\"Version\":3,\"Roster\":[{\"Id\":\"a\",\"Kind\":\"infantry\",\"Strength\":60,\"MaxStrength\":60}]}");
        check(v3 != null && CampaignState.SchemaVersion == 6 && v3.Version == 6 && v3.Roster[0].Fatigue == 0 && v3.Roster[0].Morale == CampaignState.RosterCompany.MoraleStart,
            "T42a 저장 이행: v3 파일 -> v5, 피로 0 · 사기 기본값");
        var wild = CampaignState.FromJson("{\"Version\":4,\"Roster\":[{\"Id\":\"a\",\"Fatigue\":250,\"Morale\":-5,\"MaxStrength\":60}]}");
        check(wild != null && wild.Roster[0].Fatigue == 100 && wild.Roster[0].Morale == 0, "T42a 저장: 범위 밖 피로 · 사기는 0..100 으로 자름");

        // 2) v4 왕복
        var s = new CampaignState();
        s.Roster.Clear();
        s.Roster.Add(new CampaignState.RosterCompany { Id = "a", Strength = 60, MaxStrength = 60, Fatigue = 33, Morale = 41 });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "b", Strength = 60, MaxStrength = 60, Fatigue = 90, Morale = 95 });
        var back = CampaignState.FromJson(s.ToJson());
        check(back != null && back.Roster[0].Fatigue == 33 && back.Roster[0].Morale == 41 && back.Roster[1].Fatigue == 90 && back.Roster[1].Morale == 95 && back.Version == 6,
            "T42a 저장 왕복: v4 피로 · 사기 보존");

        // 3) 정산: a 투입 (승리, 손실 10/100) -> 피로 +x · 사기 +win -round(0.1*scale); b 미투입 -> 피로 -y · 사기 +rest
        s.Roster[1].Fatigue = 90;
        CampaignEconomy.BeginOperation(s, "m", new[] { "a" }, shellBase: new Dictionary<string, int>());
        var r = new MissionResults { MissionId = "m", Result = "won" };
        r.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 100, Alive = 90, Lost = 10 });
        CampaignEconomy.SettleBattle(s, r);
        int dm = E.MoraleWin - (int)MathF.Round(0.1f * E.MoraleCasualtyScale);
        check(s.Roster[0].Fatigue == 33 + E.FatiguePerDeploy && s.Roster[0].Morale == 41 + dm
            && s.Roster[1].Fatigue == 90 - E.FatigueRestRecovery && s.Roster[1].Morale == 95 + E.MoraleRestRecovery,
            $"T42a 정산: 투입 피로 +{E.FatiguePerDeploy} · 사기 {dm:+#;-#;0} / 미투입 피로 -{E.FatigueRestRecovery} · 사기 +{E.MoraleRestRecovery} (a {s.Roster[0].Fatigue}/{s.Roster[0].Morale} b {s.Roster[1].Fatigue}/{s.Roster[1].Morale})");

        // 4) 패배 + 상한/하한 clamp, 출격 편성 없으면 무변화
        var t = new CampaignState();
        t.Roster.Clear();
        t.Roster.Add(new CampaignState.RosterCompany { Id = "a", Strength = 60, MaxStrength = 60, Fatigue = 95, Morale = 3 });
        CampaignEconomy.BeginOperation(t, "m", new[] { "a" }, shellBase: new Dictionary<string, int>());
        var lost = new MissionResults { MissionId = "m", Result = "lost" };
        lost.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 100, Alive = 20, Lost = 80 });
        CampaignEconomy.SettleBattle(t, lost);
        check(t.Roster[0].Fatigue == 100 && t.Roster[0].Morale == 0, "T42a 정산: 패배 사기 하한 0 · 피로 상한 100");
        var u = new CampaignState(); u.Roster.Clear();
        u.Roster.Add(new CampaignState.RosterCompany { Id = "a", Strength = 60, MaxStrength = 60, Fatigue = 50, Morale = 50 });
        CampaignEconomy.SettleBattle(u, new MissionResults { MissionId = "x", Result = "won" });
        check(u.Roster[0].Fatigue == 50 && u.Roster[0].Morale == 50, "T42a 정산: 출격 편성 없으면 피로 · 사기 불변");

        // 5) 홈 표시: 평균 · 지침 수, 읽기 전용 (경고만 - 잠금 없음)
        var fm = CampaignHomeViews.FatigueMorale(s);
        var empty = CampaignHomeViews.FatigueMorale(null);
        int tiredWant = s.Roster.Count(c => c.Fatigue >= E.FatigueWarnThreshold);
        check(fm.Companies == 2 && fm.AvgFatigue == (int)MathF.Round((s.Roster[0].Fatigue + s.Roster[1].Fatigue) / 2f) && fm.Tired == tiredWant && empty.Companies == 0 && empty.Tired == 0,
            $"T42a 홈 요약: 평균 피로 {fm.AvgFatigue} · 사기 {fm.AvgMorale} · 지침 {fm.Tired}");
        string before = s.ToJson();
        CampaignHomeViews.FatigueMorale(s); CampaignHomeViews.Organization(s);
        check(before == s.ToJson() && s.Roster.All(c => c.Available || !CampaignEconomy.IsTired(c)), "T42a 홈 표시: 상태 불변 · 지침은 경고만 (Available 잠금 없음)");
    }
}
