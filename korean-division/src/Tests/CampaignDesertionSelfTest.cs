using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>M2 (BH2/BH3/BH5): 사기 · 호감 · 민심 0~100 / 초기 50, 탈영 (전투 종료 정산 · 이벤트 처리에서만, 전투 중 아님), 포/중화기 보급 단가 7. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignDesertionSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        var E = CampaignEconomyDefs.Current;

        // 1) 변수 범위 · 초기값: 호감 · 민심 · 사기 전부 0~100, 50
        var s0 = new CampaignState();
        bool ok = true;
        foreach (var k in new[] { "favor.hwangson", "favor.minDaejang", "favor.namChamjang", "favor.siwidae", "favor.yiSeok", "rep.local", "morale.troops" })
        {
            var d = CampaignVars.Def(k);
            ok &= d != null && d.Min == 0 && d.Max == 100 && d.Default == 50 && CampaignVars.Get(s0, k) == 50;
        }
        check(ok && s0.Flags["heir_mood"] == 50 && s0.Flags["cmd_favor"] == 50 && CampaignState.RosterCompany.MoraleStart == 50, "M2 BH3: 호감 · 민심 · 사기 = 범위 0~100, 초기 50");

        // 2) 옛 저장 (범위 밖) -> 안전 보정
        var old = CampaignState.FromJson("{\"Version\":13,\"Flags\":{\"rep.local\":-60,\"morale.troops\":170,\"favor.siwidae\":-3}}");
        check(old != null && CampaignVars.Get(old, "rep.local") == 0 && CampaignVars.Get(old, "morale.troops") == 100 && CampaignVars.Get(old, "favor.siwidae") == 0
            && old.Flags["rep.local"] == 0 && old.Flags["morale.troops"] == 100, "M2 옛 저장(v13): 범위 밖 민심 · 사기 · 호감은 0~100 으로 보정");

        // 3) 탈영률: 문턱 이상 0, 사기 0 = 최대, 중간 선형
        check(CampaignEconomy.DesertionRate(30) == 0f && CampaignEconomy.DesertionRate(80) == 0f && MathF.Abs(CampaignEconomy.DesertionRate(0) - E.DesertionMaxRate) < 1e-6f
            && MathF.Abs(CampaignEconomy.DesertionRate(15) - E.DesertionMaxRate / 2) < 1e-6f && E.DesertionMoraleBelow == 30 && MathF.Abs(E.DesertionMaxRate - 0.05f) < 1e-6f,
            "M2 BH2: 탈영률 = (30-사기)/30 x 5% (사기 >= 30 이면 0, 0 이면 5%)");

        // 4) 정산: 사기 0 중대 100명 -> 5명, 사기 15 -> 반올림 (60 x 2.5% = 1.5 -> 2), 사기 30 -> 0
        var s = new CampaignState();
        s.Roster.Clear();
        s.Roster.Add(new CampaignState.RosterCompany { Id = "a", Strength = 100, MaxStrength = 100, Morale = 0 });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "b", Strength = 60, MaxStrength = 60, Morale = 15 });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "c", Strength = 60, MaxStrength = 60, Morale = 30 });
        int n = CampaignEconomy.SettleDesertion(s);
        check(n == 7 && s.Roster[0].Strength == 95 && s.Roster[1].Strength == 58 && s.Roster[2].Strength == 60, $"M2 탈영 정산: 사기 0/15/30 중대 -> 5 + 2 + 0 = {n}");

        // 5) 전투 종료 정산 (SettleBattle): 미투입 사기 낮은 중대에서 탈영 + 보고 / 로그. 같은 입력 = 같은 결과 (난수 없음)
        string Run1(out int des, out string log)
        {
            var st = new CampaignState();
            st.Roster.Clear();
            st.Roster.Add(new CampaignState.RosterCompany { Id = "a", Strength = 100, MaxStrength = 100, Morale = 0 });
            var r = new MissionResults { MissionId = "m2_test", Title = "t", Result = "won" };
            CampaignEconomy.SettleBattle(st, r);
            des = st.LastDebrief!.Deserters; log = st.Stock.RecentLog.Last();
            return st.ToJson();
        }
        string j1 = Run1(out int d1, out string l1), j2 = Run1(out int d2, out _);
        check(d1 > 0 && d1 == d2 && j1 == j2 && l1.Contains("탈영"), $"M2 BH2: 전투 종료 정산에서 탈영 발생 · 보고(Deserters {d1}) · 로그 · 결정적");

        // 6) 이벤트 처리 시점: 사기 낮은 중대에서 탈영
        var se = new CampaignState();
        se.Roster.Clear();
        se.Roster.Add(new CampaignState.RosterCompany { Id = "a", Strength = 100, MaxStrength = 100, Morale = 0 });
        CampaignEvents.ApplyChoice(se, "zz_unknown", 0);
        check(se.Roster[0].Strength == 95, "M2 BH2: 이벤트 처리 시점에도 탈영 (사기 0 중대 100 -> 95)");

        // 7) 사기가 문턱 이상이면 병력 불변 (기본 새 캠페인에서 이벤트 처리 후)
        var sn = new CampaignState();
        int before = sn.Roster.Sum(c => c.Strength);
        CampaignEvents.ApplyChoice(sn, "zz_unknown", 0);
        check(sn.Roster.Sum(c => c.Strength) == before, "M2: 사기 50 (기본) 에서는 탈영 없음");

        // 8) 보급 단가: 포 · 중화기 7
        check(E.SupplyCostArtillery == 7f && E.SupplyCostHeavy == 7f, "M2 BH5: 포 · 중화기 보급 단가 = 7 (campaign_economy.json)");
    }
}
