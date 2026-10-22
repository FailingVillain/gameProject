using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T42b 진급 후보 · 적용: v3/v4 -> v5 이행 · 왕복 · 후보 산출(문턱 JSON 임시, 상한) · 작전 사이에만 · 정원 · 기록 · 이월 반영 · 기본 꺼짐 불변. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignPromotionSelfTest
{
    static MissionResults.UnitLine Sol(int id, int rank, int kills) => new() { Id = id, Unit = "infantry", Name = "소총병", Rank = rank, StartRank = rank, Kills = kills, Force = "b1_c1" };
    static MissionResults.UnitLine Off(int id, int rank, int xp) => new() { Id = id, Unit = "officer", Name = "장교", OfficerRank = rank, Xp = xp, Post = "company", Force = "b1_c1" };

    public static void Run(Action<bool, string> check)
    {
        var saved = CampaignEconomyDefs.Current;
        try
        {
            // 1) 옛 저장 (v3 · v4) -> v5: 진급 기록 빈 목록 · 정원 0, 진급 필드가 있는 v5 왕복
            var v3 = CampaignState.FromJson("{\"Version\":3,\"Roster\":[{\"Id\":\"a\",\"Kind\":\"infantry\",\"Strength\":60,\"MaxStrength\":60}]}");
            var v4 = CampaignState.FromJson("{\"Version\":4,\"PromotionLog\":null,\"Roster\":[{\"Id\":\"a\",\"Fatigue\":10,\"MaxStrength\":60}]}");
            check(v3 != null && v3.Version == 9 && v3.PromotionLog.Count == 0 && v3.PromotionsThisBreak == 0 && v4 != null && v4.Version == 9 && v4.PromotionLog.Count == 0 && v4.Roster[0].Fatigue == 10,
                "T42b 저장 이행: v3 · v4 파일 -> v5, 진급 기록 빈 목록 · 정원 0");

            var d = new CampaignEconomyDefs { PromotionManual = true }.Sanitized();
            CampaignEconomyDefs.Override(d);
            int offCap = Defs.Current.Officers.MaxPromotionRank;
            var s = new CampaignState { EntryMission = "m1" };
            s.Units = new() { Sol(1, 1, 2), Sol(2, 1, 1), Sol(3, d.PromoSoldierMaxRank, 99), Sol(4, 3, 4), Sol(5, 0, 0) };
            s.Officers = new() { Off(10, 2, 200), Off(11, offCap, 99999), Off(12, 3, 100) };

            // 2) 후보: 문턱 이상만, 사병 최고 계급 · 장교 상한 (중령) 초과 불가, 장교 먼저
            var c = CampaignPromotion.Candidates(s);
            string ids = string.Join(",", c.Select(x => (x.Officer ? "o" : "s") + x.UnitId));
            check(ids == "o10,s4,s1", $"T42b 후보: 장교(문턱 넘음) 먼저 · 사병 문턱 이상만, 상한 계급 · 중령 이상 제외 (got {ids})");
            check(c.All(x => x.ToRank > x.FromRank) && (!c.Any(x => x.Officer && x.ToRank > offCap)), "T42b 후보: 올린 계급은 항상 위 · 장교 상한 초과 없음");

            // 3) 적용: 계급 +1 (사병은 다음 사용 계급), 사살 소진, 기록, 정원 +1, 후보 사라짐
            bool ok = CampaignPromotion.Promote(s, true, 10) && CampaignPromotion.Promote(s, false, 1);
            check(ok && s.Officers[0].OfficerRank == 3 && s.Units[0].Rank == RankNames.Next(null, 1) && s.Units[0].Kills == 0 && s.PromotionsThisBreak == 2
                && s.PromotionLog.Count == 2 && s.PromotionLog[0].Officer && s.PromotionLog[0].FromRank == 2 && s.PromotionLog[0].ToRank == 3 && s.PromotionLog[0].AfterMission == "m1",
                "T42b 적용: 장교 +1 · 사병 다음 계급 · 사살 소진 · 기록 · 정원 +1");
            check(!CampaignPromotion.Promote(s, false, 1) && !CampaignPromotion.Promote(s, false, 3) && !CampaignPromotion.Promote(s, true, 11) && !CampaignPromotion.Promote(s, true, 999),
                "T42b 적용: 이미 올린 사람 · 상한 계급 · 중령 · 없는 id 는 거부 (상태 불변)");

            // 4) 저장 왕복 (기록 · 정원 · 계급 보존)
            var back = CampaignState.FromJson(s.ToJson());
            check(back != null && back.PromotionLog.Count == 2 && back.PromotionsThisBreak == 2 && back.Officers[0].OfficerRank == 3 && back.PromotionLog[1].UnitId == 1 && back.Version == 9,
                "T42b 저장 왕복: 진급 기록 · 정원 · 계급 보존");

            // 5) 작전 사이에만 + 정원
            s.Pending = new CampaignState.PendingOperation { MissionId = "m2", DeployedCompanyIds = new() { "a" } };
            check(CampaignPromotion.BlockOf(s) == CampaignPromotion.Block.Operation && !CampaignPromotion.Promote(s, false, 4) && s.Units[3].Rank == 3, "T42b 작전 진행 중(Pending)에는 진급 거부");
            s.Pending = null;
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { PromotionManual = true, PromoMaxPerBreak = 2 }.Sanitized());
            check(CampaignPromotion.BlockOf(s) == CampaignPromotion.Block.Quota && !CampaignPromotion.Promote(s, false, 4), "T42b 정원(작전 사이 최대 N명) 소진 시 거부");

            // 6) 이월 반영: 진급한 UnitLine 계급이 다음 미션 CampaignCarry 의 이월 목록에 나온다
            var carried = CampaignCarry.Infantry(s, "next").Select(u => u.Rank).ToList();
            check(carried.Contains(RankNames.Next(null, 1)) && CampaignCarry.Officers(s, "next").Any(u => u.OfficerRank == 3), "T42b 이월: 진급한 계급이 다음 미션 이월 목록에 반영");

            // 7) 정산 -> 이월: 켜짐 = 사병 계급은 작전 시작 계급에 머문다 (사살은 후보 근거로 남음) / 기본 꺼짐 = 기존 자동 계급
            var survivor = new MissionResults.UnitLine { Id = 7, Unit = "infantry", Rank = 3, StartRank = 1, Kills = 6 };
            var offOn = new MissionResults { MissionId = "c1_01_yalu", Result = "won" };
            offOn.Survivors.Add(survivor);
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { PromotionManual = true }.Sanitized());
            var on = CampaignState.Apply(new CampaignState(), offOn);
            check(on != null && on.Units.Count == 1 && on.Units[0].Rank == 1 && on.Units[0].Kills == 6 && CampaignPromotion.Candidates(on).Count == 1 && on.PromotionsThisBreak == 0,
                "T42b 켜짐: 이월 사병 계급은 작전 시작 계급 · 사살은 보존 -> 후보");
            CampaignEconomyDefs.Override(new CampaignEconomyDefs().Sanitized());
            var off = CampaignState.Apply(new CampaignState(), offOn);
            check(off != null && off.Units[0].Rank == 3 && CampaignPromotion.Candidates(off).Count == 0 && CampaignPromotion.BlockOf(off) == CampaignPromotion.Block.Off && !CampaignPromotion.Promote(off, false, 7),
                "T42b 기본 꺼짐: 이월 계급 기존대로(자동) · 후보 없음 · 진급 거부");

            // 7b) 마지막 미션 재승리: 이월 병력 · 적용한 진급 · 정원 카운터 유지 (정원 우회 · 진급 소실 방지)
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { PromotionManual = true }.Sanitized());
            var rp = CampaignState.Apply(new CampaignState(), offOn)!;
            bool pr = CampaignPromotion.Promote(rp, false, 7);
            int rankAfter = rp.Units[0].Rank, quota = rp.PromotionsThisBreak;
            var again = CampaignState.Apply(rp, offOn)!;   // 같은 마지막 미션 다시 승리
            check(pr && quota == 1 && again.PromotionsThisBreak == 1 && again.Units.Count == 1 && again.Units[0].Rank == rankAfter && again.Units[0].Kills == 0 && again.PromotionLog.Count == 1,
                "T42 수정: 마지막 미션 재승리는 정원 · 적용한 진급 · 이월 병력을 건드리지 않는다");
            CampaignEconomyDefs.Override(new CampaignEconomyDefs().Sanitized());

            // 8) 손상 입력: null 목록 · 이상한 JSON 배열은 크래시 없이 기본값
            var odd = CampaignEconomyDefs.Load("nonexistent.json");
            var weird = new CampaignEconomyDefs { PromoSoldierKills = new[] { -5, 9_999_999 }, PromoOfficerXp = null!, PromoSoldierMaxRank = 99, PromoMaxPerBreak = -3 }.Sanitized();
            check(!odd.PromotionManual && weird.PromoSoldierKills.Length == 9 && weird.PromoSoldierKills[0] == 0 && weird.PromoSoldierKills[1] == 1_000_000 && weird.PromoOfficerXp.Length == 7 && weird.PromoSoldierMaxRank == 8 && weird.PromoMaxPerBreak == 0,
                "T42b 정의 보정: 기본 꺼짐 · 배열 길이/범위 보정");
            check(CampaignPromotion.Candidates(null).Count == 0 && CampaignPromotion.BlockOf(null) == CampaignPromotion.Block.Off, "T42b null 상태에서도 예외 없음");
        }
        finally { CampaignEconomyDefs.Override(saved); }
    }
}
