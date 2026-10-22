using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T42d 일정 · 연표: v3~v6 -> v7 이행 (Day 역산) · 왕복 · 정산 일수/연표 (첫 승리만) · 이벤트 발동 기록 · 연표 상한 · 표시 자료. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignScheduleSelfTest
{
    static MissionResults Won(string id, string result = "won")
    {
        var r = new MissionResults { MissionId = id, Title = "T-" + id, Result = result, EnemyKills = 5 };
        r.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 60, Alive = 60 });
        return r;
    }

    public static void Run(Action<bool, string> check)
    {
        var saved = CampaignEconomyDefs.Current;
        try
        {
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { MissionDays = 2 }.Sanitized());

            // 1) 옛 저장 -> v7: Day = 1 + 기록 수 x missionDays, 빈 연표
            var v3 = CampaignState.FromJson("{\"Version\":3}");
            var v6 = CampaignState.FromJson("{\"Version\":6,\"History\":[{\"MissionId\":\"a\"},{\"MissionId\":\"b\"}],\"PrisonersInterned\":4}");
            check(v3 != null && v3.Version == 7 && v3.Day == 1 && v3.Timeline.Count == 0 && v6 != null && v6.Version == 7 && v6.Day == 5 && v6.PrisonersInterned == 4,
                "T42d 저장 이행: v3 -> Day 1, v6 -> Day 1+2x2=5 (다른 필드 보존)");

            // 2) 왕복 + 깨진 값
            var s = new CampaignState { Day = 9 };
            s.AddTimeline("mission", "m1", "첫 작전"); s.AddTimeline("event", "e1_guard_front", "", 2);
            var back = CampaignState.FromJson(s.ToJson());
            check(back != null && back.Day == 9 && back.Timeline.Count == 2 && back.Timeline[0].Title == "첫 작전" && back.Timeline[1].Choice == 2 && back.Timeline[1].Day == 9, "T42d 저장 왕복: Day · 연표");
            var broken = CampaignState.FromJson("{\"Version\":7,\"Day\":-4,\"Timeline\":[null]}");
            check(broken != null && broken.Day >= 1 && broken.Timeline.Count == 0, "T42d 손상 값: Day 음수 -> 보정, null 연표 줄 제거");

            // 3) 정산: 첫 승리만 일수 + 연표, 반복 승리/패배는 불변
            var w = new CampaignState();
            w.EnsureInitialized();
            int d0 = w.Day;
            CampaignEconomy.SettleBattle(w, Won("m1"));  // 새 상태 Day 0 -> 1일째 시작 + 2
            check(w.Day == 3 && w.Timeline.Count == 1 && w.Timeline[0].Kind == "mission" && w.Timeline[0].Id == "m1" && w.Timeline[0].Day == 3, $"T42d 정산: 1일째 시작 + 2 = 3일 · 연표 한 줄 (Day {d0}->{w.Day})");
            CampaignEconomy.RecordRepeatVictory(w, Won("m1"));
            check(w.Day == 3 && w.Timeline.Count == 1, "T42d 반복 승리: 일수 · 연표 불변");
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { MissionDays = 0 }.Sanitized());
            CampaignEconomy.SettleBattle(w, Won("m2"));
            check(w.Day == 3 && w.Timeline.Count == 2, "T42d missionDays 0: 일수 안 늘고 연표만");
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { MissionDays = 2 }.Sanitized());

            // 4) 이벤트 발동 기록
            var e = new CampaignState(); e.EnsureInitialized();
            CampaignEvents.ApplyChoice(e, "e1_guard_front", 0);
            check(e.Timeline.Count == 1 && e.Timeline[0].Kind == "event" && e.Timeline[0].Id == "e1_guard_front" && e.Timeline[0].Choice == 1, "T42d 이벤트 발동: 연표에 id · 선택 기록");

            // 5) 상한
            var cap = new CampaignState();
            for (int i = 0; i < CampaignState.TimelineMax + 25; i++) { cap.Day = i + 1; cap.AddTimeline("mission", "m" + i); }
            check(cap.Timeline.Count == CampaignState.TimelineMax && cap.Timeline[0].Id == "m25", "T42d 연표 상한: 오래된 것부터 버림");

            // 6) 표시 자료: 최신 위 · 예외 없음 · 일지 일수
            var ev = CampaignHomeViews.Schedule(null);
            check(ev.Day == 1 && ev.Rows.Count == 0, "T42d 일정 표시: null 상태도 안전");
            var both = new CampaignState(); both.EnsureInitialized();
            both.History.Add(new CampaignState.MissionRecord { MissionId = "m1", Title = "T-m1" });
            both.Day = 3; both.AddTimeline("mission", "m1", "T-m1");
            both.Day = 4; both.AddTimeline("event", "e1_guard_front", "", 1);
            both.Timeline.Add(null!);
            var sv = CampaignHomeViews.Schedule(both);
            var rv = CampaignHomeViews.Records(both);
            check(sv.Rows.Count == 2 && sv.Rows[0].IsEvent && sv.Rows[0].Day == 4 && !sv.Rows[1].IsEvent && sv.Missions == 1 && sv.Events == 1 && rv.Recent[0].Day == 3, "T42d 일정 표시: 최신 위 · 이벤트/작전 구분 · 부대 기록 일수");
        }
        finally { CampaignEconomyDefs.Override(saved); }
    }
}
