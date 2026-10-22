using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>
/// T48d 연대장 관리 UI 의 표시 자료 (CommanderHomeModel): 버튼 상태가 T48b API 결과와 같은지 (CanHire / CanDismiss / CanAssign*), 해임 불가 버튼 없음,
/// 작전 중 전부 비활성, 기용 -> 해임 -> 재기용 흐름이 연표 문구 · 호감 막대 값에 반영, 해임 확인 문구에 호감 예고, 참모 효과 수치(StaffEffects), 연표 문구 id 조회.
/// 그룹 KD_SELFTEST_ONLY=commanders. KD_COMMANDERUI_SEED=폴더 이면 시험 대신 스크린샷용 예시 캠페인 파일을 그 폴더에 쓴다 (파일만).
/// </summary>
public static class CommanderUiSelfTest
{
    static CommanderDef Def(string id, string name, bool unfireable = false, string note = "", string from = "start") => new()
    {
        Id = id, Name = name, Rank = "정령", School = "평양", Origin = "평양 · 군관학교", Unfireable = unfireable, UnfireableNote = note,
        Availability = new CommanderAvailability { FromMission = from }, Favor = new CommanderFavor { Initial = 30 }, BattleSummary = "방어선을 지키는 노장",
    };

    static CampaignState.CampaignOfficer Off(CampaignState s, int key, int rank = 6, int xp = 120, bool terminal = true)
    {
        var o = new CampaignState.CampaignOfficer { Key = key, Name = "장교" + key, Rank = rank, Xp = xp, TerminalReady = terminal, School = "독일" };
        s.CampaignOfficers.Add(o);
        return o;
    }

    public static void Run(Action<bool, string> check)
    {
        string? seed = Environment.GetEnvironmentVariable("KD_COMMANDERUI_SEED");
        if (!string.IsNullOrEmpty(seed)) { Seed(seed); return; }
        var savedEco = CampaignEconomyDefs.Current; var savedCmd = CommanderCatalog.Current; var savedFav = CommanderFavorRules.Current; var savedAsg = CommanderAssignRules.Current;
        try
        {
            CampaignEconomyDefs.Override(null); CommanderFavorRules.Override(null); CommanderAssignRules.Override(null);
            var res = new CommanderLoadResult();
            res.Defs.Add(Def("a", "갑")); res.Defs.Add(Def("b", "을", unfireable: true, note: "황실 추천 인물")); res.Defs.Add(Def("c", "병")); res.Defs.Add(Def("d", "정", from: "m_later"));
            CommanderCatalog.Override(res);
            check(CommanderHomeModel.RulesOn, "T48d 명단이 있으면 규칙 켜짐 (연대장 화면)");

            var s = new CampaignState { Day = 3 };
            CommanderPools.Sync(s, res.Defs);
            s.Roster = new() { new CampaignState.RosterCompany { Id = "c1", BattalionNumber = 1 }, new CampaignState.RosterCompany { Id = "c2", BattalionNumber = 2 } };

            // 1) 기용 버튼 = CanHire 결과 그대로 · 후보 풀 필터 · 잠김 ???
            var hb = CommanderHomeModel.HireBtn(s, "a", 1);
            check(hb.Visible && hb.Enabled == CommanderPoolOps.CanHire(s, "a", 1).Ok && hb.Enabled, "T48d [기용] 활성 = CanHire 통과");
            var lockedBtn = CommanderHomeModel.HireBtn(s, "d", 1);
            check(!lockedBtn.Enabled && lockedBtn.Reason == CommanderPoolOps.CanHire(s, "d", 1).Reason && lockedBtn.Reason.Length > 0, "T48d 잠긴 인물 [기용] 비활성 + 사유 문구 그대로");
            var dc = CommanderHomeModel.CardOf(s, "d");
            check(dc != null && dc.Locked && dc.Name == S.CmdLockedName && dc.Rank.Length == 0 && dc.StatusLabel == S.CmdStLocked, "T48d 잠긴 인물 카드는 '???' (이름 · 계급 숨김)");
            check(CommanderHomeModel.Pool(s, "all").Count == 4 && CommanderHomeModel.Pool(s, CommanderStatus.Available).Count == 3 && CommanderHomeModel.Pool(s, CommanderStatus.Locked).Count == 1 && CommanderHomeModel.Pool(s, CommanderStatus.Hired).Count == 0, "T48d 후보 풀 필터 (전체 4 · 가능 3 · 잠김 1 · 재직 0)");

            // 2) 기용 -> 연대 판 · 해임 버튼 · 해임 불가 버튼 없음
            check(CommanderPoolOps.Hire(s, "a", 2).Ok && CommanderPoolOps.Hire(s, "b", 1).Ok, "T48d 기용 (a=2연대, b=1연대)");
            var board = CommanderHomeModel.RegimentBoard(s);
            check(board.Count == CampaignRegiments.Max && board[0].Card?.Id == "b" && board[1].Card?.Id == "a" && board[2].Card == null, "T48d 연대 판: 9칸 · 1=b · 2=a · 3=공석");
            check(!CommanderHomeModel.DismissBtn(s, "b").Visible, "T48d 해임 불가 인물에는 [해임] 버튼 없음");
            check(CommanderHomeModel.CardOf(s, "b")!.Unfireable && CommanderHomeModel.CardOf(s, "b")!.UnfireableNote == "황실 추천 인물", "T48d 해임 불가 도장 + 사유 자료");
            var db = CommanderHomeModel.DismissBtn(s, "a");
            check(db.Visible && db.Enabled && db.Enabled == CommanderPoolOps.CanDismiss(s, "a").Ok, "T48d [해임] 활성 = CanDismiss 통과");
            check(!CommanderHomeModel.HireBtn(s, "c", 2).Enabled && CommanderHomeModel.HireBtn(s, "c", 2).Reason.Contains("이미 연대장") && CommanderHomeModel.HireBtn(s, "c", 3).Enabled, "T48d 찬 연대에는 [기용] 비활성 (사유 = API 문구) · 빈 연대는 활성");
            check(CommanderHomeModel.FirstVacantRegiment(s) == 3 && CommanderHomeModel.NextVacantRegiment(s, 3, 1) == 4 && CommanderHomeModel.NextVacantRegiment(s, 3, -1) == 9, "T48d 빈 연대 순환 선택 (3 -> 4, 3 <- 9)");

            // 3) 해임 확인 문구에 호감 하락 예고
            int pen = CommanderCatalog.DismissPenalty(res.Defs[0]);
            string ask = CommanderHomeModel.DismissNotice(s, "a");
            check(pen > 0 && ask.Contains("갑") && ask.Contains("2연대") && ask.Contains(pen.ToString()), $"T48d 해임 확인 문구: 이름 · 연대 · 호감 -{pen} 예고");

            // 4) 기용 -> 해임 -> 재기용이 연표 문구 · 호감 값에 반영
            int fav0 = CommanderHomeModel.CardOf(s, "a")!.Favor;
            s.Day = 6;
            check(CommanderPoolOps.Dismiss(s, "a").Ok, "T48d 해임 실행 (T48b 함수)");
            var after = CommanderHomeModel.CardOf(s, "a")!;
            check(after.Favor == fav0 - pen && after.Status == CommanderStatus.Dismissed && CommanderHomeModel.RegimentBoard(s)[1].Card == null, "T48d 해임 -> 호감 막대 값 하락 · 상태 해임됨 · 연대 공석");
            var rehire = CommanderHomeModel.HireBtn(s, "a", 2);
            check(rehire.Enabled == CommanderPoolOps.CanHire(s, "a", 2).Ok, "T48d 해임 직후 재기용 버튼 = CanHire 결과 (쿨다운 반영)");
            s.Day = 20;
            check(CommanderHomeModel.HireBtn(s, "a", 2).Enabled && CommanderPoolOps.Hire(s, "a", 2).Ok, "T48d 쿨다운 뒤 재기용 가능");
            var tl = s.Timeline.Where(t => t.Kind == "commander").Select(t => CommanderHomeModel.TimelineText(s, t)).ToList();
            check(tl.Count >= 4 && tl.Any(t => t.Contains("연대장 갑 기용 (연대 2)")) && tl.Any(t => t.Contains("연대장 갑 해임 (연대 2)")), $"T48d 연표 문구 (id 로 조회: 기용 · 해임 · 재기용) [{string.Join(" / ", tl)}]");
            check(CommanderHomeModel.TimelineText(s, new CampaignState.TimelineEntry { Day = 1, Kind = "commander", Id = "zzz" }) == "zzz", "T48d 모르는 연표 id 는 id 그대로 (예외 없음)");
            var sched = CampaignHomeViews.Schedule(s);
            check(sched.Rows.Any(r => r.Text.Contains("연대장 갑 해임")) && sched.Missions == 0 && sched.Events == 0, "T48d 탭 14 일정에 연대장 줄 표시 (작전/이벤트 건수에는 안 셈)");

            // 5) 작전 중에는 전부 비활성 (사유 그대로)
            s.Pending = new CampaignState.PendingOperation();
            check(!CommanderHomeModel.HireBtn(s, "c", 3).Enabled && !CommanderHomeModel.DismissBtn(s, "a").Enabled && CommanderHomeModel.DismissBtn(s, "a").Visible && CommanderHomeModel.DismissBtn(s, "a").Reason.Contains("작전"), "T48d 작전 진행 중: [기용] [해임] 비활성 + 사유");
            s.Pending = null;

            // 6) 대대장 / 참모 배정 버튼 = CanAssign*, 참모 효과 수치 = StaffEffects
            Off(s, 1, rank: 6, xp: 200); Off(s, 2, rank: 4, xp: 0); Off(s, 3, terminal: false);
            check(CommanderHomeModel.Terminal(s).Count == 2 && CommanderHomeModel.Terminal(s).All(r => r.Key != 3), "T48d 종착 대기 목록 = OfficerAssign.Candidates (종착만)");
            int tb = CommanderHomeModel.TargetBattalion(s, 1);
            check(tb == 1 && CommanderHomeModel.BattalionBtn(s, 1, tb).Enabled == OfficerAssign.CanAssignBattalion(s, 1, tb).Ok && CommanderHomeModel.BattalionBtn(s, 1, tb).Enabled, "T48d [대대장으로] 활성 (빈 첫 대대)");
            check(OfficerAssign.AssignBattalion(s, 1, 1).Ok && OfficerAssign.AssignStaff(s, 2, StaffEffects.Supply).Ok, "T48d 배정 실행 (T48b 함수)");
            check(!CommanderHomeModel.BattalionBtn(s, 2, 1).Enabled && CommanderHomeModel.BattalionBtn(s, 2, 1).Reason == OfficerAssign.CanAssignBattalion(s, 2, 1).Reason, "T48d 대대장이 있는 대대 [대대장으로] 비활성 + 사유 그대로");
            check(!CommanderHomeModel.StaffBtn(s, 1, StaffEffects.Supply).Enabled && CommanderHomeModel.StaffBtn(s, 1, StaffEffects.Intel).Enabled, "T48d 찬 참모 슬롯 비활성 · 빈 슬롯 활성");
            check(CommanderHomeModel.TargetBattalion(s, 2) == 2, "T48d 대대장 없는 대대로 향함 (2대대)");
            var staff = CommanderHomeModel.StaffBoard(s);
            var eff = StaffEffects.Compute(s);
            check(staff.Count == 4 && staff[0].OfficerKey == 2 && staff[0].Effect.Contains(CommanderHomeModel.Pct(eff.SupplyBonusPermille)) && eff.SupplyBonusPermille > 0 && staff[1].OfficerKey == 0, $"T48d 참모 판: 보급 슬롯 효과 수치 = StaffEffects ({staff[0].Effect})");
            check(CommanderHomeModel.Pct(55) == "5.5" && CommanderHomeModel.Pct(0) == "0.0", "T48d 천분율 표시 (정수 연산)");
            var bat = CommanderHomeModel.BattalionBoard(s);
            check(bat.Count == 2 && bat[0].OfficerKey == 1 && bat[1].OfficerKey == 0, "T48d 대대장 판: 1대대=장교1 · 2대대 공석");
            var tlAssign = s.Timeline.Where(t => t.Id == "assign:1").Select(t => CommanderHomeModel.TimelineText(s, t)).FirstOrDefault();
            check(tlAssign == "장교1 제1대대장 배정", $"T48d 배정 연표 문구 ({tlAssign})");
            check(OfficerAssign.Unassign(s, 1).Ok, "T48d 해제 (T48b)");

            // 7) 규칙 꺼짐 (명단 없음) = 기존 학파 화면
            CommanderCatalog.Override(new CommanderLoadResult { FileMissing = true });
            check(!CommanderHomeModel.RulesOn, "T48d 명단 없음 -> 규칙 꺼짐 (기존 T19d 학파 화면 + 안내)");
            // 8) 데이터에서 사라진 인물 / 없는 id
            check(CommanderHomeModel.CardOf(s, "a")!.Name == S.CmdUnknown && CommanderHomeModel.CardOf(s, "nope") == null && !CommanderHomeModel.DismissBtn(s, "nope").Visible, "T48d 데이터에서 사라진 인물 '알 수 없음' · 없는 id 는 null (예외 없음)");
        }
        finally { CampaignEconomyDefs.Override(savedEco); CommanderCatalog.Override(savedCmd); CommanderFavorRules.Override(savedFav); CommanderAssignRules.Override(savedAsg); }
    }

    /// <summary>스크린샷용 예시 캠페인 파일 (KD_COMMANDERUI_SEED 폴더 = KD_CAMPAIGN_DIR). 현재 로드된 연대장 명단을 쓴다: 앞 몇 명 기용 · 하나 해임 · 장교 몇 명.</summary>
    static void Seed(string dir)
    {
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", dir);
        var s = new CampaignState { Day = 12 };
        s.EnsureInitialized();
        CommanderPools.Sync(s);
        s.Roster.Add(new CampaignState.RosterCompany { Id = "seed_c2", BattalionNumber = 2, BattalionName = "제2대대" });
        var defs = CommanderCatalog.Current.Defs;
        int hired = 0;
        for (int i = 0; i < defs.Count && hired < 6; i++)
        {
            var slot = s.CommanderPool.FirstOrDefault(c => c.Id == defs[i].Id);
            if (slot == null || slot.Status != CommanderStatus.Available) continue;
            if (CommanderPoolOps.Hire(s, slot.Id, hired + 1).Ok) { hired++; slot.Favor = Math.Clamp(slot.Favor + hired * 11 - 20, -20, 100); }
        }
        var hiredIds = s.CommanderPool.Where(c => c.Status == CommanderStatus.Hired).Select(c => c.Id).ToList();
        if (hiredIds.Count > 3) { s.Day = 14; CommanderPoolOps.Dismiss(s, hiredIds[3]); }
        Off(s, 1, 6, 200); Off(s, 2, 6, 40); Off(s, 3, 5, 10, terminal: false);
        s.NextOfficerKey = 4;
        OfficerAssign.AssignBattalion(s, 1, 1);
        OfficerAssign.AssignStaff(s, 2, StaffEffects.Intel);
        s.Save();
        Console.WriteLine("seeded " + CampaignState.FilePath);
    }
}
