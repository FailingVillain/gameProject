using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T48b2 연대장 기용/해임 · 장교 배정 · 참모 효과: Hire/Dismiss 흐름 (해임 불가 · 쿨다운 · 재기용 불가 · 호감 하락 1회) · 등장 갱신 · 배정/재배정/겸임 불가/자리당 1명 ·
/// 참모 4슬롯 효과 계산 · 작전 태그 도출 + 정산 연결 (호감 작전당 1회 · 보급 보너스 · 전사자 자리 비움) · 저장 왕복 · 옛 저장 v3~v9. 그룹 KD_SELFTEST_ONLY=commanders (SaveCompat 에서도 부름).</summary>
public static class CommanderPoolSelfTest
{
    static CommanderDef Def(string id, string name, bool unfireable = false, string note = "", bool rehirable = true, string from = "start", string[]? likes = null) => new()
    {
        Id = id, Name = name, Unfireable = unfireable, UnfireableNote = note, Rehirable = rehirable,
        Availability = new CommanderAvailability { FromMission = from },
        Favor = new CommanderFavor { Initial = 30, Likes = (likes ?? Array.Empty<string>()).ToList() },
    };

    static CampaignState.CommanderSlot Slot(CampaignState s, string id) => s.CommanderPool.First(c => c.Id == id);

    static CampaignState.CampaignOfficer Off(CampaignState s, int key, int rank = 5, int xp = 0, bool terminal = true, string school = "")
    {
        var o = new CampaignState.CampaignOfficer { Key = key, Name = "장교" + key, Rank = rank, Xp = xp, TerminalReady = terminal, School = school };
        s.CampaignOfficers.Add(o);
        return o;
    }

    static MissionResults Won(string mission, int spawned, int lost, float seconds)
    {
        var r = new MissionResults { MissionId = mission, Result = "won", Title = mission, DurationSeconds = seconds };
        r.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = spawned, Lost = lost, Alive = spawned - lost });
        return r;
    }

    public static void Run(Action<bool, string> check)
    {
        var savedEco = CampaignEconomyDefs.Current;
        var savedCmd = CommanderCatalog.Current;
        var savedFav = CommanderFavorRules.Current;
        var savedAsg = CommanderAssignRules.Current;
        try
        {
            CampaignEconomyDefs.Override(null); CommanderFavorRules.Override(null); CommanderAssignRules.Override(null);
            var eco = CampaignEconomyDefs.Current;

            // 0) 규칙 JSON (실제 파일)
            var rules = CommanderAssignRules.Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "commander_assign.json"));
            check(rules.Warnings.Count == 0 && !rules.ChargeHireCost && rules.StaffBackToBattalion && rules.Staff.XpTierSteps.Length == 4 && rules.Tags.LowLossPermille == 100,
                $"T48b2 commander_assign.json 로드 (경고 {string.Join("/", rules.Warnings)})");
            var broken = Path.Combine(Path.GetTempPath(), "kd_cassign_" + Environment.ProcessId + ".json");
            File.WriteAllText(broken, "{ not json");
            var br = CommanderAssignRules.Load(broken);
            try { File.Delete(broken); } catch (IOException) { }
            check(br.Warnings.Count == 1 && br.Staff.SupplyBasePermille == 50, "T48b2 규칙 JSON 깨짐 -> 기본값 + 경고 1줄");

            var res = new CommanderLoadResult();
            res.Defs.Add(Def("a", "갑", likes: new[] { "인명보존" }));
            res.Defs.Add(Def("b", "을", unfireable: true, note: "황실 추천 인물이라 해임할 수 없다"));
            res.Defs.Add(Def("c", "병", rehirable: false));
            res.Defs.Add(Def("d", "정", from: "m_later"));
            CommanderCatalog.Override(res);

            // 1) 기용: 상태 전이 · 연대 1명 · 범위 · 작전 중 불가 · 연표/일지
            var s = new CampaignState { Day = 5 };
            CommanderPools.Sync(s, res.Defs);
            check(Slot(s, "a").Status == CommanderStatus.Available && Slot(s, "d").Status == CommanderStatus.Locked, "T48b2 풀 초기 상태 (처음부터 = available, 조건부 = locked)");
            check(!CommanderPoolOps.Hire(s, "d", 1).Ok, "T48b2 locked 인물은 기용 거부");
            var h = CommanderPoolOps.Hire(s, "a", 1);
            var sa = Slot(s, "a");
            check(h.Ok && sa.Status == CommanderStatus.Hired && sa.Regiment == 1 && sa.HiredDay == 5 && sa.TimesHired == 1
                && s.Timeline.Any(t => t.Kind == "commander" && t.Id == "hire:a" && t.Choice == 1) && s.Stock.RecentLog.Any(l => l.Contains("갑") && l.Contains("기용")), "T48b2 기용 (재직 · 연대 1 · 연표 · 일지)");
            check(!CommanderPoolOps.Hire(s, "b", 1).Ok && CommanderPoolOps.Hire(s, "b", 1).Reason.Contains("이미 연대장")
                && !CommanderPoolOps.Hire(s, "b", 0).Ok && !CommanderPoolOps.Hire(s, "b", 10).Ok && !CommanderPoolOps.Hire(s, "a", 2).Ok, "T48b2 연대당 1명 · 범위 1~9 · 재직자 재기용 불가");
            s.Pending = new CampaignState.PendingOperation();
            check(!CommanderPoolOps.Hire(s, "b", 2).Ok && !CommanderPoolOps.Dismiss(s, "a").Ok, "T48b2 작전 진행 중에는 기용/해임 불가");
            s.Pending = null;
            check(CommanderPoolOps.Hire(s, "b", 2).Ok && CommanderPoolOps.Hire(s, "c", 3).Ok, "T48b2 다른 연대에는 기용");

            // 2) 해임: 해임 불가 거부 + 사유 · 호감 하락 정확히 1회 · 연대 해제
            var bad = CommanderPoolOps.Dismiss(s, "b");
            check(!bad.Ok && bad.Reason.Contains("황실") && Slot(s, "b").Status == CommanderStatus.Hired && Slot(s, "b").Favor == 30, "T48b2 해임 불가 거부 (사유 문구 · 상태/호감 불변)");
            s.Day = 8;
            int favBefore = Slot(s, "a").Favor;
            var d1 = CommanderPoolOps.Dismiss(s, "a");
            int favAfter = Slot(s, "a").Favor;
            var d2 = CommanderPoolOps.Dismiss(s, "a");
            check(d1.Ok && sa.Status == CommanderStatus.Dismissed && sa.Regiment == 0 && sa.DismissedDay == 8 && favAfter == favBefore - eco.CommanderDismissFavorPenalty
                && !d2.Ok && sa.Favor == favAfter && s.Timeline.Count(t => t.Id == "dismiss:a") == 1, "T48b2 해임 (호감 하락 1회 · 두 번째 해임 거부 · 연표 1줄)");

            // 3) 재기용: 쿨다운 · 재기용 불가 · 빈 연대
            var early = CommanderPoolOps.Hire(s, "a", 1);
            check(!early.Ok && early.Reason.Contains("작전") && sa.Status == CommanderStatus.Dismissed, "T48b2 쿨다운 안에는 재기용 거부");
            s.Day = 8 + eco.CommanderRehireCooldownMissions * eco.MissionDays;
            var again = CommanderPoolOps.Hire(s, "a", 1);
            check(again.Ok && sa.Status == CommanderStatus.Hired && sa.TimesHired == 2, "T48b2 쿨다운 지난 뒤 재기용");
            CommanderPoolOps.Dismiss(s, "c");
            s.Day += 50;
            var nr = CommanderPoolOps.Hire(s, "c", 3);
            check(!nr.Ok && nr.Reason.Contains("불가") && Slot(s, "c").Status == CommanderStatus.Dismissed, "T48b2 재기용 불가 인물은 영영 거부");

            // 4) 등장 갱신 (작전 완료 -> available) · until -> unavailable
            check(CommanderPoolOps.RefreshAvailability(s) == 0 && Slot(s, "d").Status == CommanderStatus.Locked, "T48b2 조건 미충족이면 locked 유지");
            s.Completed.Add("m_later");
            check(CommanderPoolOps.RefreshAvailability(s) == 1 && Slot(s, "d").Status == CommanderStatus.Available && CommanderPoolOps.Hire(s, "d", 4).Ok, "T48b2 작전 완료로 available -> 기용");

            // 5) 참모 효과 계산 (가중 = 1000 + 100*(계급-4) + 50*경험치단계)
            check(StaffEffects.XpTier(0) == 0 && StaffEffects.XpTier(60) == 1 && StaffEffects.XpTier(500) == 3 && StaffEffects.XpTier(9999) == 4
                && StaffEffects.WeightPermille(4, 0) == 1000 && StaffEffects.WeightPermille(5, 500) == 1250 && StaffEffects.WeightPermille(1, 0) == 700 && StaffEffects.WeightPermille(0, 0) == 600
                && StaffEffects.WeightPermille(-9, 0) == 500 && StaffEffects.WeightPermille(99, 99999) == 2000, "T48b2 참모 가중 (계급 · 경험치 단계 · 클램프)");
            var st = new CampaignState();
            var none = StaffEffects.Compute(st);
            check(none.SupplyBonusPermille == 0 && none.IntelInfo == 0 && none.ResearchSpeedPermille == 0 && none.DoctrinePermille == 0, "T48b2 참모 없음 = 효과 0");
            Off(st, 1, 5, 500); Off(st, 2, 4, 0); Off(st, 3, 5, 500, school: "독일"); Off(st, 4, 1, 0); Off(st, 9, 5, 0, terminal: false);
            check(OfficerAssign.AssignStaff(st, 1, "supply").Ok && OfficerAssign.AssignStaff(st, 2, "intel").Ok && OfficerAssign.AssignStaff(st, 3, "doctrine").Ok && OfficerAssign.AssignStaff(st, 4, "research").Ok, "T48b2 참모 4슬롯 배정");
            var ef = StaffEffects.Compute(st);
            check(ef.SupplyBonusPermille == 62 && ef.IntelInfo == 2 && ef.ResearchSpeedPermille == 70 && ef.DoctrinePermille == 62 && ef.DoctrineSchool == "독일",
                $"T48b2 슬롯별 효과 (보급 62 · 정보 2 · 연구 70 · 학파 62 독일) 실제 {ef.SupplyBonusPermille}/{ef.IntelInfo}/{ef.ResearchSpeedPermille}/{ef.DoctrinePermille}/{ef.DoctrineSchool}");
            st.Supply = 100; st.LastDebrief = new CampaignState.DebriefReport { RegularSupplyAdded = 1500, SupplyAfter = 100 };
            int bonus = StaffEffects.ApplySupplyBonus(st);
            check(bonus == 93 && st.Supply == 193 && st.LastDebrief.SupplyAfter == 193, "T48b2 보급 보너스 = 정기 보급 1500 x 62/1000 = 93 (내림)");

            // 6) 배정 규칙: 종착만 · 빈 대대 · 자리당 1명 · 겸임 불가(이동) · 참모 복귀 · 전사/부상
            var a = new CampaignState();
            a.Roster.Add(new CampaignState.RosterCompany { Id = "b1_c1", BattalionNumber = 1, BattalionName = "제1대대", Kind = "infantry", Name = "1중대", Available = true });
            a.Roster.Add(new CampaignState.RosterCompany { Id = "b2_c1", BattalionNumber = 2, BattalionName = "제2대대", Kind = "infantry", Name = "2중대", Available = true });
            Off(a, 1); Off(a, 2); Off(a, 3, terminal: false); var w = Off(a, 4); var dead = Off(a, 5); dead.Status = OfficerIdentity.Dead;
            check(OfficerAssign.Candidates(a).Select(o => o.Key).SequenceEqual(new[] { 1, 2, 4 }), "T48b2 후보 = 종착 + 생존 (비종착 · 전사 제외)");
            check(!OfficerAssign.AssignBattalion(a, 3, 1).Ok && !OfficerAssign.AssignBattalion(a, 5, 1).Ok && !OfficerAssign.AssignBattalion(a, 1, 3).Ok && !OfficerAssign.AssignBattalion(a, 99, 1).Ok, "T48b2 배정 거부 (비종착 · 전사 · 없는 대대 · 없는 키)");
            check(OfficerAssign.AssignBattalion(a, 1, 1).Ok && !OfficerAssign.AssignBattalion(a, 2, 1).Ok && OfficerAssign.AssignBattalion(a, 2, 2).Ok, "T48b2 빈 대대에만 대대장 (자리당 1명)");
            check(!OfficerAssign.AssignBattalion(a, 1, 1).Ok, "T48b2 같은 자리 재배정 거부");
            check(OfficerAssign.AssignStaff(a, 1, "supply").Ok && a.CampaignOfficers.First(o => o.Key == 1).Assignment == "staff" && a.CampaignOfficers.First(o => o.Key == 1).Battalion == 0
                && OfficerAssign.Battalions(a).First(b => b.Battalion == 1).Key == 0, "T48b2 대대장 -> 참모 재배정 = 이동 (겸임 불가, 대대 비움)");
            check(!OfficerAssign.AssignStaff(a, 2, "supply").Ok && !OfficerAssign.AssignStaff(a, 2, "nope").Ok, "T48b2 참모 자리당 1명 · 없는 슬롯 거부");
            check(OfficerAssign.AssignBattalion(a, 1, 1).Ok && StaffEffects.In(a, "supply") == null, "T48b2 참모 -> 대대장 복귀 (기본 허용)");
            CommanderAssignRules.Override(new CommanderAssignRules { StaffBackToBattalion = false });
            OfficerAssign.AssignStaff(a, 1, "intel");
            check(!OfficerAssign.AssignBattalion(a, 1, 1).Ok, "T48b2 복귀 스위치(JSON) 끄면 참모 -> 대대장 거부");
            CommanderAssignRules.Override(null);
            w.Status = OfficerIdentity.Wounded; w.WoundedUntilMission = 5;
            check(!OfficerAssign.AssignBattalion(a, 4, 1).Ok && OfficerAssign.AssignStaff(a, 4, "research").Ok, "T48b2 부상자: 새 대대장 배정 거부 · 참모는 가능");
            check(OfficerAssign.Unassign(a, 4).Ok && !OfficerAssign.Unassign(a, 4).Ok && a.CampaignOfficers.First(o => o.Key == 4).Assignment == "", "T48b2 배정 해제");
            a.Pending = new CampaignState.PendingOperation();
            check(!OfficerAssign.AssignStaff(a, 4, "research").Ok && !OfficerAssign.Unassign(a, 2).Ok, "T48b2 작전 중 배정/해제 거부");
            a.Pending = null;
            check(OfficerAssign.Describe(a.CampaignOfficers.First(o => o.Key == 2)) == "제2대대장" && OfficerAssign.Describe(a.CampaignOfficers.First(o => o.Key == 1)) == "참모(intel)", "T48b2 배정 표기 (UI 카드용)");
            // 참전 연결: 대대장은 출전 가능, 참모는 불가 (T48b1 규칙)
            check(OfficerIdentity.CanDeploy(a, new MissionResults.UnitLine { Key = 2 }) && !OfficerIdentity.CanDeploy(a, new MissionResults.UnitLine { Key = 1 }), "T48b2 대대장만 참전 (참모 불가)");

            // 7) 태그 도출 + 정산 연결 (호감 작전당 1회, 전사자 자리 비움)
            check(CommanderPoolOps.DeriveTags(Won("m", 100, 5, 600)).SequenceEqual(new[] { "인명보존", "속전속결" })
                && CommanderPoolOps.DeriveTags(Won("m", 100, 40, 2000)).SequenceEqual(new[] { "공세" })
                && CommanderPoolOps.DeriveTags(Won("m", 100, 20, 2000)).Count == 0
                && CommanderPoolOps.DeriveTags(new MissionResults { Result = "lost" }).Count == 0, "T48b2 작전 태그 도출 (인명보존 <=10% · 속전속결 <=900s · 공세 >=30%)");
            var f = new CampaignState();
            CommanderPools.Sync(f, res.Defs);
            Slot(f, "a").Status = CommanderStatus.Hired; Slot(f, "a").Regiment = 1; Slot(f, "a").Favor = 30;
            var fo = Off(f, 7); fo.Status = OfficerIdentity.Dead; fo.Assignment = "staff"; fo.StaffSlot = "supply";
            var win = Won("mX", 100, 5, 600);
            CommanderPoolOps.OnSettle(f, win);
            int fav1 = Slot(f, "a").Favor;
            CommanderPoolOps.OnSettle(f, win);
            check(fav1 == 31 && Slot(f, "a").Favor == 31 && fo.Assignment == "" && fo.StaffSlot == "", "T48b2 정산: 선호 태그 호감 +1 (같은 작전 1회) · 전사자 자리 비움");

            // 8) 저장 왕복 + Sanitize (자리 중복 · 모르는 값)
            var p = new CampaignState { Day = 9 };
            p.Roster.Add(new CampaignState.RosterCompany { Id = "b1_c1", BattalionNumber = 1, BattalionName = "제1대대", Kind = "infantry", Name = "1중대", Available = true });
            CommanderPools.Sync(p, res.Defs);
            CommanderPoolOps.Hire(p, "a", 2); CommanderPoolOps.Dismiss(p, "a");
            Off(p, 1, 5, 200, school: "독일"); Off(p, 2); OfficerAssign.AssignBattalion(p, 1, 1); OfficerAssign.AssignStaff(p, 2, "doctrine");
            var q = CampaignState.FromJson(p.ToJson());
            check(q != null && Slot(q, "a").Status == CommanderStatus.Dismissed && Slot(q, "a").DismissedDay == 9 && Slot(q, "a").Favor == Slot(p, "a").Favor
                && q.CampaignOfficers.First(o => o.Key == 1).Assignment == "battalion" && q.CampaignOfficers.First(o => o.Key == 1).Battalion == 1
                && q.CampaignOfficers.First(o => o.Key == 2).StaffSlot == "doctrine" && q.Timeline.Count == p.Timeline.Count, "T48b2 저장 왕복 (풀 · 배정 · 연표 유지)");
            var dup = CampaignState.FromJson("{\"Version\":9,\"CampaignOfficers\":[{\"Key\":5,\"TerminalReady\":true,\"Assignment\":\"staff\",\"StaffSlot\":\"intel\"},{\"Key\":3,\"TerminalReady\":true,\"Assignment\":\"staff\",\"StaffSlot\":\"intel\"},"
                + "{\"Key\":6,\"Assignment\":\"battalion\",\"Battalion\":0},{\"Key\":8,\"Assignment\":\"staff\",\"StaffSlot\":\"zzz\"},{\"Key\":9,\"Status\":\"dead\",\"Assignment\":\"battalion\",\"Battalion\":2}]}");
            check(dup != null && dup.CampaignOfficers.First(o => o.Key == 3).StaffSlot == "intel" && dup.CampaignOfficers.First(o => o.Key == 5).Assignment == ""
                && dup.CampaignOfficers.First(o => o.Key == 6).Assignment == "" && dup.CampaignOfficers.First(o => o.Key == 8).Assignment == "" && dup.CampaignOfficers.First(o => o.Key == 9).Assignment == "",
                "T48b2 손상 저장 보정 (자리 중복 -> Key 작은 쪽 유지 · 모르는 자리/대대 · 전사자 해제)");

            // 9) 옛 저장 v3~v9: 빈 풀/명부로 이행, 배정 없음, 현재 스키마
            bool allOld = true; string why2 = "";
            for (int v = 3; v <= 9; v++)
            {
                var o = CampaignState.FromJson("{\"Version\":" + v + (v >= 8 ? ",\"CampaignOfficers\":[{\"Key\":7,\"Name\":\"갑\",\"TerminalReady\":true}]" : "") + "}");
                bool ok = o != null && o.CampaignOfficers.All(x => x.Assignment == "" && x.StaffSlot == "") && StaffEffects.Compute(o).SupplyBonusPermille == 0 && (v < 8 || OfficerAssign.Candidates(o).Count == 1);
                if (!ok) { allOld = false; why2 += " v" + v; }
            }
            check(allOld, "T48b2 옛 저장 v3~v9 로드 (배정 없음 · 참모 효과 0 · 종착 후보 유지)" + why2);

            // 10) 실제 정산 경로 (Apply): 첫 승리에서 OnSettle 이 연결되어 태그 호감 +1 · 재승리는 반복 반영 없음
            var chain = new[] { new { Id = CampaignState.FirstMission }, new { Id = CampaignState.Tutorial2 } };
            var c1 = CampaignState.Apply(null, new MissionResults { MissionId = chain[0].Id, Result = "won", Title = "t", DurationSeconds = 100, Factions = { new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 50, Lost = 0, Alive = 50 } } });
            check(c1 != null, "T48b2 Apply 첫 승리 (테스트 준비)");
            if (c1 != null)
            {
                CommanderPools.Sync(c1, res.Defs);
                Slot(c1, "a").Status = CommanderStatus.Hired; Slot(c1, "a").Regiment = 1; Slot(c1, "a").Favor = 30; Slot(c1, "a").FavorLastMission = "";
                var c2 = CampaignState.Apply(c1, new MissionResults { MissionId = chain[1].Id, Result = "won", Title = "t2", DurationSeconds = 100, Factions = { new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 50, Lost = 0, Alive = 50 } } });
                check(c2 != null && Slot(c2, "a").Favor == 31 && Slot(c2, "a").FavorLastMission == chain[1].Id, "T48b2 Apply 정산 -> 작전 태그 호감 +1 (실제 경로)");
            }
        }
        finally
        {
            CampaignEconomyDefs.Override(savedEco); CommanderCatalog.Override(savedCmd); CommanderFavorRules.Override(savedFav); CommanderAssignRules.Override(savedAsg);
        }
    }
}
