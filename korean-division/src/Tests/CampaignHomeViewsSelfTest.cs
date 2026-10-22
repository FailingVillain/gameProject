using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T38f 읽기 전용 탭 (기록 · 장비 · 편제) 표시 자료: 빈 · null 상태 안전, 값 일치, 상태 불변 (ToJson 동일). SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignHomeViewsSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        // null · 빈 상태: 예외 없음
        var rn = CampaignHomeViews.Records(null); var en = CampaignHomeViews.Equipment(null); var on = CampaignHomeViews.Organization(null);
        check(rn.Missions == 0 && rn.Recent.Count == 0 && rn.Top.Count == 0 && rn.Debrief == null && en.Calibers.Count == StockKeys.AllCalibers.Length && en.Horses == 0 && on.Count == 0,
            "홈 읽기 전용 탭: state null 이어도 빈 뷰 (예외 없음)");
        var empty = new CampaignState { Roster = new(), History = new(), Units = new() };
        check(CampaignHomeViews.Records(empty).Recent.Count == 0 && CampaignHomeViews.Organization(empty).Count == 0, "홈 읽기 전용 탭: 빈 기록 · 빈 편성");

        // 손상 목록 (null 원소) 안전
        var bad = new CampaignState();
        bad.History.Add(null!); bad.Units.Add(null!); bad.Roster.Add(null!);
        bad.Stock.Heavy = null!; bad.Stock.Ammo = null!;
        bool ok = true;
        try { CampaignHomeViews.Records(bad); CampaignHomeViews.Equipment(bad); CampaignHomeViews.Organization(bad); } catch (Exception) { ok = false; }
        check(ok, "홈 읽기 전용 탭: 목록 null 원소 · 빈 재고 사전에도 예외 없음");

        // 값 일치
        var s = new CampaignState();
        for (int i = 1; i <= 10; i++) s.History.Add(new CampaignState.MissionRecord { MissionId = "m" + i, Title = "작전" + i, EnemyKills = i * 10, Lost = i, Survivors = 100 - i });
        s.Units.Add(new MissionResults.UnitLine { Id = 1, Name = "갑", Kills = 5 });
        s.Units.Add(new MissionResults.UnitLine { Id = 2, Name = "을", Kills = 9 });
        s.Units.Add(new MissionResults.UnitLine { Id = 3, Name = "병", Kills = 0 });
        var r = CampaignHomeViews.Records(s);
        check(r.Missions == 10 && r.TotalKills == 550 && r.TotalLost == 55 && r.Recent.Count == CampaignHomeViews.RecentMax && r.Recent[0].Title == "작전10" && r.Top.Count == 2 && r.Top[0].Name == "을",
            $"홈 읽기 전용 탭 기록: 합계 · 최근순 {r.Recent[0].Title} · 사살 상위 (0 제외)");

        s.Stock.Rifles[StockKeys.Jp] = 40; s.Stock.Heavy["mg"] = 3;
        s.Roster.Clear();
        s.Roster.Add(new CampaignState.RosterCompany { Id = "a", BattalionNumber = 1, BattalionName = "제1대대", Strength = 50, MaxStrength = 60, Wounded = 4, Regiment = 2, Caliber = StockKeys.Jp });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "b", BattalionNumber = 1, BattalionName = "제1대대", Strength = 40, MaxStrength = 60, Regiment = 2, Available = false });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "c", BattalionNumber = 0, Name = "기병중대", Strength = 30, MaxStrength = 30 });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "d", BattalionNumber = 3, BattalionName = "제3대대", Strength = 60, MaxStrength = 60, Regiment = 99 });   // 범위 밖 → 직할
        var e = CampaignHomeViews.Equipment(s);
        var jp = e.Calibers.First(c => c.Key == StockKeys.Jp);
        check(jp.Rifles == 40 && jp.Companies == 1 && e.Calibers.First(c => c.Key == StockKeys.Ru).Companies == 3 && e.Heavy.Count == 1 && e.Shells.Count == 3,
            "홈 읽기 전용 탭 장비: 구경별 소총 · 편성 중대 수 · 중장비 · 포탄 3종");
        var o = CampaignHomeViews.Organization(s);
        var reg2 = o.FirstOrDefault(n => n.Regiment == 2); var direct = o.LastOrDefault();
        check(o.Count == 2 && reg2 != null && reg2.Companies == 2 && reg2.Strength == 90 && reg2.Battalions[0].Wounded == 4 && reg2.Battalions[0].Available == 1
              && direct!.Regiment == 0 && direct.Companies == 2 && direct.Battalions.Count == 2 && direct.Battalions[0].Number == 3 && direct.Battalions[1].Number == 0,
            $"홈 읽기 전용 탭 편제: 연대 {o.Count}개, 직할 마지막 (대대 + 직할 중대), 범위 밖 연대는 직할");

        // 읽기 전용: 뷰를 만들어도 상태 불변
        string before = s.ToJson();
        CampaignHomeViews.Records(s); CampaignHomeViews.Equipment(s); CampaignHomeViews.Organization(s);
        check(before == s.ToJson(), "홈 읽기 전용 탭: 뷰 생성이 상태를 바꾸지 않음 (저장 형식 불변)");
    }
}
