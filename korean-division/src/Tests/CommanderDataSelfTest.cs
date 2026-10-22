using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T48a 연대장 데이터: 샘플 3명 로드 · 항목 단위 검증 (중복 id · 알 수 없는 학파/특성/태그 · 범위 밖 호감) · 깨진 파일 · 파일 없음 = 빈 풀 · 해임 호감 하락값 (JSON) · 저장 v8 이행/왕복 · 풀 동기화. 그룹 KD_SELFTEST_ONLY=commanders.</summary>
public static class CommanderDataSelfTest
{
    static string Entry(string id, string extra = "") =>
        "{\"id\":\"" + id + "\",\"name\":\"가나다\",\"rank\":\"정령\",\"school\":\"평양\",\"traits\":[\"iron_wall\"],\"favor\":{\"initial\":30,\"likes\":[\"수비\"],\"dislikes\":[]}" + extra + "}";

    static string Dir(string name, string? commanders, bool vocab = true)
    {
        string d = TestTemp.Child("kd_cmd_" + name);
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(d)) File.Delete(f);
        if (vocab)
        {
            string src = Path.Combine(AppContext.BaseDirectory, "assets", "data");
            File.Copy(Path.Combine(src, CommanderCatalog.TraitFile), Path.Combine(d, CommanderCatalog.TraitFile));
            File.Copy(Path.Combine(src, CommanderCatalog.TagFile), Path.Combine(d, CommanderCatalog.TagFile));
        }
        if (commanders != null) File.WriteAllText(Path.Combine(d, CommanderCatalog.CommanderFile), commanders);
        return d;
    }

    public static void Run(Action<bool, string> check)
    {
        var savedEco = CampaignEconomyDefs.Current;
        var savedCmd = CommanderCatalog.Current;
        try
        {
            // 1) 샘플 (assets/data 의 실제 파일) 3명
            var real = CommanderCatalog.Load(Path.Combine(AppContext.BaseDirectory, "assets", "data"));
            check(real.Defs.Count == 3 && real.Errors.Count == 0 && real.Defs[2].Unfireable && real.Defs[2].DismissFavorPenalty == 0 && real.Defs[1].Hire.MinRegiments == 2 && real.Defs[0].Favor.Likes.Count == 2,
                $"T48a 샘플 JSON 3명 로드 (오류 {real.Errors.Count}: {string.Join(" / ", real.Errors)})");

            // 2) 잘못된 항목 3종 + 형식 틀림: 그 항목만 거부, 나머지 로드, 사유에 id
            var bad = CommanderCatalog.Load(Dir("bad", "{\"commanders\":[" + string.Join(",",
                Entry("ok_one"),
                Entry("ok_one"),                                                            // 중복 id
                Entry("bad_school").Replace("\"평양\"", "\"화성\""),                         // 알 수 없는 학파
                Entry("bad_favor").Replace("\"initial\":30", "\"initial\":999"),             // 범위 밖 호감
                Entry("bad_trait").Replace("iron_wall", "no_such_trait"),                    // 알 수 없는 특성
                Entry("bad_tag").Replace("수비", "도망"),                                     // 알 수 없는 태그
                Entry("bad_type").Replace("\"rank\":\"정령\"", "\"rank\":5"),                // 형식 틀림
                Entry("ok_two", ",\"mystery\":1,\"_note\":\"x\"")) + "]}"));                  // 알 수 없는 필드 = 경고
            string errAll = string.Join("\n", bad.Errors);
            check(bad.Defs.Count == 2 && bad.Defs[0].Id == "ok_one" && bad.Defs[1].Id == "ok_two", $"T48a 잘못된 항목만 거부, 나머지 로드 (통과 {string.Join(",", bad.Defs.Select(d => d.Id))})");
            check(bad.Errors.Count == 6 && new[] { "ok_one: id 중복", "bad_school: school(학파) '화성'", "bad_favor: favor.initial", "bad_trait: traits(특성) 'no_such_trait'", "bad_tag", "bad_type: 필드 형식" }.All(errAll.Contains), $"T48a 거부 사유: 연대장 <id>: 한국어 사유 ({errAll.Replace("\n", " | ")})");
            check(bad.Warnings.Any(w => w.Contains("ok_two") && w.Contains("mystery")) && !bad.Warnings.Any(w => w.Contains("_note")), "T48a 모르는 필드 = 경고 (_ 로 시작하면 주석이라 조용히)");

            // 3) 깨진 파일 · 구조 오류 · 파일 없음 = 빈 풀, 크래시 없음
            var broken = CommanderCatalog.Load(Dir("broken", "{\"commanders\":[ {"));
            var noArr = CommanderCatalog.Load(Dir("noarr", "{\"list\":[]}"));
            var none = CommanderCatalog.Load(Dir("none", null));
            var noVocab = CommanderCatalog.Load(Dir("novocab", "{\"commanders\":[" + Entry("a") + "]}", vocab: false));
            check(broken.Defs.Count == 0 && broken.Errors.Count == 1 && broken.Errors[0].Contains("줄") && noArr.Defs.Count == 0 && noArr.Errors.Count == 1, "T48a 깨진 JSON · commanders 배열 없음: 빈 풀 + 한국어 오류 한 줄 (줄 번호)");
            check(none.FileMissing && none.Defs.Count == 0 && none.Errors.Count == 0 && noVocab.Defs.Count == 0 && noVocab.Warnings.Count >= 2, "T48a 파일 없음 = 풀 0명 (오류 없음); 어휘 파일이 없으면 경고 + 해당 항목 거부");

            // 4) 해임 호감 하락값 = JSON (D24)
            var eco = CampaignEconomyDefs.Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "campaign_economy.json"));
            check(eco.CommanderDismissFavorPenalty == 2 && eco.CommanderRehireCooldownMissions == 1 && eco.CommanderFavorMin == -20 && eco.CommanderFavorMax == 100, "T48a campaign_economy.json 에서 임시값 읽기 (해임 -2 · 쿨다운 1 · 호감 -20~100)");
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { CommanderDismissFavorPenalty = 4 }.Sanitized());
            var a = new CommanderDef(); var b = new CommanderDef { DismissFavorPenalty = 3 }; var c = new CommanderDef { Unfireable = true };
            check(CommanderCatalog.DismissPenalty(a) == 4 && CommanderCatalog.DismissPenalty(b) == 3 && CommanderCatalog.DismissPenalty(c) == 0, "T48a 해임 하락: 공통값 (JSON) · 인물별 덮어쓰기 · 해임 불가 0");
            CampaignEconomyDefs.Override(null);

            // 5) 저장 v7 -> v8 (빈 목록) · 왕복 · 손상 값
            var v7 = CampaignState.FromJson("{\"Version\":7,\"Day\":4,\"Completed\":[\"c1_01\"]}");
            check(v7 != null && v7.Version == CampaignState.SchemaVersion && v7.CommanderPool.Count == 0 && v7.CampaignOfficers.Count == 0 && v7.Day == 4, "T48a 저장 이행: v7 -> v8 빈 풀 · 빈 장교 명부 (다른 필드 보존)");
            var v3 = CampaignState.FromJson("{\"Version\":3}");
            check(v3 != null && v3.Version == CampaignState.SchemaVersion && v3.CommanderPool.Count == 0, "T48a 저장 이행: v3 -> v8");
            var s = new CampaignState();
            s.CommanderPool.Add(new CampaignState.CommanderSlot { Id = "x", Status = CommanderStatus.Hired, Regiment = 2, Favor = 41, FavorLastMission = "m1", HiredDay = 3, DismissedDay = 0, TimesHired = 1 });
            s.CampaignOfficers.Add(new CampaignState.CampaignOfficer { Key = 7, Name = "오", Rank = 2, Xp = 90, School = "독일", Assignment = "battalion", Battalion = 3, Status = "wounded", WoundedUntilMission = 5, AlwaysWounded = true, TerminalReady = true });
            var back = CampaignState.FromJson(s.ToJson());
            var p = back?.CommanderPool.FirstOrDefault(); var o = back?.CampaignOfficers.FirstOrDefault();
            check(back != null && back.Version == CampaignState.SchemaVersion && p != null && p.Id == "x" && p.Status == "hired" && p.Regiment == 2 && p.Favor == 41 && p.FavorLastMission == "m1" && p.HiredDay == 3 && p.TimesHired == 1
                && o != null && o.Key == 7 && o.Name == "오" && o.Rank == 2 && o.Xp == 90 && o.School == "독일" && o.Assignment == "battalion" && o.Battalion == 3 && o.Status == "wounded" && o.WoundedUntilMission == 5 && o.AlwaysWounded && o.TerminalReady, "T48a 저장 v8 왕복: 풀 · 장교 명부 모든 필드");
            var wild = CampaignState.FromJson("{\"Version\":8,\"CommanderPool\":[{\"Id\":\"a\",\"Status\":\"zzz\",\"Favor\":9999,\"TimesHired\":-4},{\"Id\":\"a\"},{\"Id\":\"\"}],\"CampaignOfficers\":[{\"Key\":1,\"Xp\":-5,\"Name\":null}]}");
            check(wild != null && wild.CommanderPool.Count == 1 && wild.CommanderPool[0].Status == "locked" && wild.CommanderPool[0].Favor == 100 && wild.CommanderPool[0].TimesHired == 0 && wild.CampaignOfficers[0].Xp == 0 && wild.CampaignOfficers[0].Name == "",
                "T48a 손상 값 보정: 중복/빈 id 제거 · 모르는 상태 locked · 호감 범위 · 음수 0 · null 이름");
            check(CampaignState.FromJson("{\"Version\":8,\"CommanderPool\":[null]}") == null && CampaignState.FromJson("{\"Version\":8,\"CommanderPool\":null,\"CampaignOfficers\":null}") is { } nn && nn.CommanderPool.Count == 0, "T48a null 원소는 거부, null 목록은 빈 목록");

            // 6) 풀 동기화: 새 id 추가 (시작부터 = available, 조건 있음 = locked), 사라진 id 보존, 반복해도 같음
            var st = new CampaignState();
            int added = CommanderPools.Sync(st, real.Defs);
            check(added == 3 && st.CommanderPool.Select(x => x.Status).SequenceEqual(new[] { "available", "locked", "locked" }) && st.CommanderPool[0].Favor == 30 && st.CommanderPool[1].Favor == 20 && st.CommanderPool[2].Favor == 50,
                $"T48a 풀 동기화: 샘플 3명 -> 풀 (상태 {string.Join(",", st.CommanderPool.Select(x => x.Status))})");
            st.CommanderPool[0].Status = CommanderStatus.Hired; st.CommanderPool[0].Favor = 77;
            check(CommanderPools.Sync(st, real.Defs) == 0 && st.CommanderPool[0].Favor == 77 && st.CommanderPool[0].Status == "hired", "T48a 동기화 반복: 기존 상태 불변");
            int kept = CommanderPools.Sync(st, real.Defs.Skip(1)) + st.CommanderPool.Count;
            check(kept == 3 && st.CommanderPool[0].Id == "cmd_placeholder_a", "T48a 데이터에서 사라진 id 도 풀에 보존 (삭제 없음)");
            var emptySt = new CampaignState();
            CommanderPools.Sync(emptySt, Array.Empty<CommanderDef>());
            check(emptySt.CommanderPool.Count == 0 && emptySt.ToJson().Length > 0, "T48a 데이터 0명: 풀 0명으로 정상 동작");

            // 7) 연표 'commander' 줄은 일정 탭에 보이되 작전/이벤트로 세지 않음 (T48d)
            var tl = new CampaignState { Day = 3 };
            tl.AddTimeline("mission", "m1", "첫 작전"); tl.AddTimeline("commander", "cmd_placeholder_a");
            var view = CampaignHomeViews.Schedule(tl);
            check(view.Missions == 1 && view.Events == 0 && view.Rows.Count == 2, "T48a 연표 Kind=\"commander\" 허용: 일정 탭은 줄로 표시하되 작전/이벤트 건수에는 안 셈 (T48d)");
        }
        finally { CampaignEconomyDefs.Override(savedEco); CommanderCatalog.Override(savedCmd); }
    }
}
