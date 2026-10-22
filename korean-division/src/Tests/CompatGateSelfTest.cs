using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// [T49c] 저장 호환 출시 게이트: tests/fixtures/campaign/campaign_v1..vN.json (스키마 번호별 고정 샘플, 실제 옛 파일 모양)이 전부 열리고
/// (이행 · 빠진 필드 기본값), 더 새 버전은 한국어 사유로 거부되며 원본은 그대로 남는다. KD_SELFTEST_ONLY=compatgate.
/// 스키마를 올리는 카드는 campaign_v{새 번호}.json 을 추가해야 한다 (없으면 이 시험이 실패). 샘플 재생성: tests/fixtures/gen_campaign_fixtures.py.
/// </summary>
public static class CompatGateSelfTest
{
    /// <summary>tests/fixtures/campaign 폴더 (실행 파일 위로 올라가며 찾는다).</summary>
    public static string? FixtureDir()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            {
                string p = Path.Combine(d.FullName, "tests", "fixtures", "campaign");
                if (Directory.Exists(p)) return p;
            }
        return null;
    }

    public static void Run(Action<bool, string> check)
    {
        // 재생성용: KD_COMPAT_DUMP=<폴더> 이면 현재 스키마의 채워진 샘플을 거기에 쓴다 (tests/fixtures/gen_campaign_fixtures.py)
        if (Environment.GetEnvironmentVariable("KD_COMPAT_DUMP") is { Length: > 0 } dump) { Directory.CreateDirectory(dump); File.WriteAllText(Path.Combine(dump, "populated_current.json"), Populated().ToJson()); }
        int cur = CampaignState.SchemaVersion;
        string? dir = FixtureDir();
        check(dir != null, "호환 게이트: tests/fixtures/campaign 샘플 폴더를 찾는다");
        if (dir == null) return;

        // 1..현재 스키마까지 빠짐없이 (스키마를 올린 카드가 샘플을 안 더하면 여기서 걸린다)
        var missing = Enumerable.Range(1, cur).Where(v => !File.Exists(Path.Combine(dir, $"campaign_v{v}.json"))).ToList();
        check(missing.Count == 0, $"호환 게이트: 스키마 v1..v{cur} 샘플이 모두 있다 (없는 것: {string.Join(",", missing)})");

        string cdir = TestTemp.Child("kd_compat_gate");
        string? oldEnv = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", cdir);
        try
        {
            Directory.CreateDirectory(cdir);
            for (int v = 1; v <= cur; v++)
            {
                string f = Path.Combine(dir, $"campaign_v{v}.json");
                if (!File.Exists(f)) continue;
                string text = File.ReadAllText(f);
                bool tagged = text.Contains($"\"Version\": {v}") || text.Contains($"\"Version\":{v}");
                var s = CampaignState.Parse(text, out var err);
                bool basic = s != null && err == null && s.Version == cur && s.NextMission.Length > 0 && s.Completed.Count == 2 && s.History.Count == 1;
                bool filled = s != null && s.Roster.Count > 0 && s.Stock.Ammo.Count >= StockKeys.AllCalibers.Count() && s.Stock.Rifles.ContainsKey(StockKeys.Ru)
                    && s.Flags.ContainsKey("cmd_favor") && s.Day >= 1 && s.CommanderPool != null && s.CampaignOfficers != null && s.PromotionLog != null && s.Timeline != null
                    && s.Roster.All(c => c.Caliber.Length > 0 && c.Morale > 0);
                check(tagged && basic && filled, $"호환 게이트: campaign_v{v}.json 이 열려 현재 v{cur} 로 이행되고 빠진 필드가 기본값으로 채워진다 {err}");

                // 파일 경로로도 (Load) + 이행본을 저장해 다시 읽어도 같다
                File.WriteAllText(CampaignState.FilePath, text);
                var l = CampaignState.Load();
                bool rt = false;
                if (l != null && CampaignState.LastLoadError == null)
                {
                    var again = CampaignState.Parse(l.ToJson(), out var e2);
                    rt = again != null && e2 == null && again.ToJson() == l.ToJson();
                }
                check(rt, $"호환 게이트: campaign_v{v}.json Load → 저장 → 다시 읽기 왕복이 같다");
            }

            // 더 새 버전: 거부 + 한국어 사유 + 원본 보존 (현재 샘플에서 번호만 올린 파일)
            string curText = File.Exists(Path.Combine(dir, $"campaign_v{cur}.json")) ? File.ReadAllText(Path.Combine(dir, $"campaign_v{cur}.json")) : "{}";
            bool newerOk = true; string last = "";
            foreach (int nv in new[] { cur + 1, cur + 5, 999 })
            {
                string nt = curText.Replace($"\"Version\": {cur}", $"\"Version\": {nv}").Replace($"\"Version\":{cur}", $"\"Version\":{nv}");
                if (nt == curText) nt = $"{{\"Version\":{nv}}}";
                var r = CampaignState.Parse(nt, out var err);
                File.WriteAllText(CampaignState.FilePath, nt);
                var loaded = CampaignState.Load();
                string? le = CampaignState.LastLoadError;
                last = err ?? "(사유 없음)";
                newerOk &= r == null && err != null && err.Contains("더 새로운 버전") && err.Contains("열 수 없습니다") && err.Contains($"v{nv}") && err.Contains($"v{cur}")
                    && loaded == null && le == err && File.ReadAllText(CampaignState.FilePath) == nt;
            }
            check(newerOk, $"호환 게이트: 더 새 스키마(v{cur + 1}..)는 한국어 사유로 거부되고 파일은 그대로 남는다 — {last}");
        }
        finally { Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", oldEnv); try { Directory.Delete(cdir, true); } catch (IOException) { } }
    }

    /// <summary>모든 필드가 값을 가진 현재 스키마 상태 (옛 샘플 생성의 원본).</summary>
    static CampaignState Populated()
    {
        var s = new CampaignState { NextMission = "c1_02_tumen", Completed = new() { "c0_01_bandits", "c1_01_hoeryong" }, Day = 12, Supply = 2500f, Difficulty = "normal", HowitzerCompany = true, EntryMission = "c1_01_hoeryong", PrisonersRepatriated = 3, PrisonersInterned = 2, PromotionsThisBreak = 1 };
        s.History.Add(new CampaignState.MissionRecord { MissionId = "c1_01_hoeryong", Title = "1-1", DurationSeconds = 931.5f, EnemyKills = 40, Lost = 5, Survivors = 55, SavedAt = "2026-10-22T10:00:00" });
        var u1 = new MissionResults.UnitLine { Id = 7, Unit = "rifleman", Name = "김 일병", Kills = 4, Rank = 1, StartRank = 0 };
        var o1 = new MissionResults.UnitLine { Id = 2, Unit = "officer", Name = "박 참위", Kills = 1, Rank = 0, StartRank = 0, OfficerRank = 2, Xp = 30, Post = "company", Key = 1 };
        s.Units.Add(u1); s.Officers.Add(o1); s.EntryUnits.Add(u1); s.EntryOfficers.Add(o1);
        foreach (var c in s.Roster) { c.Regiment = 1; c.Foreign = true; c.Fatigue = 20; c.Morale = 65; c.Wounded = 2; c.UnhealedOps = 1; }
        s.PromotionLog.Add(new CampaignState.PromotionRecord { AfterMission = "c1_01_hoeryong", Officer = false, UnitId = 7, FromRank = 0, ToRank = 1 });
        s.Timeline.Add(new CampaignState.TimelineEntry { Day = 6, Kind = "mission", Id = "c1_01_hoeryong", Title = "1-1" });
        s.CommanderPool.Add(new CampaignState.CommanderSlot { Id = "sample_a", Status = "hired", Regiment = 1, Favor = 40, FavorLastMission = "c1_01_hoeryong", HiredDay = 3, TimesHired = 1 });
        s.CampaignOfficers.Add(new CampaignState.CampaignOfficer { Key = 1, Name = "이 참위", Merits = 12, Traits = new() { "brave" } });
        s.Stock.Prisoners = 4; s.Stock.RecentLog.Add("보급 +100");
        s.Pending = new CampaignState.PendingOperation { MissionId = "c1_02_tumen", DeployedCompanyIds = new() { "b1_c1" }, SupplySpent = 100f, AmmoLoaded = 6000, AmmoByCaliber = new() { ["ru"] = 6000 }, ShellsLoaded = new() { ["field"] = 30 }, ShellScale = new() { ["field"] = 1f } };
        s.NextOfficerKey = 2;
        s.Funds = 7; s.EpisodePermanentSupplyLoss = 3f; s.Pending.EpisodeSupplyReserved = 50f;   // v11 (TC-D)
        s.Episode = new EpisodeState { EpisodeId = "c1e1", NodeId = "OP1", SelectNode = "sk1", DaysUsed = 2, Selected = new() { "sk1" }, FailedSk = new() { "sk2" }, Variant = "a", Waited = true, DeadlineDay = 14, Outcomes = new() { new EpisodeOutcome { Node = "BT1", Outcome = "win" } }, Checkpoint = new EpisodeCheckpoint { Supply = 1200f } };   // v12 (C1E1-g1)
        s.Decisions = new DecisionLog { Answers = new() { new DecisionAnswer { Id = "D01", Choices = new() { "a" }, Outcome = "ok", Quantity = 2, Day = 5 } }, Pending = new() { "D02" }, Checkpoint = new DecisionCheckpoint { Vars = new() { new CampaignVarEntry { Key = "rep.local", Value = 3 } }, Answers = 1, Funds = 7, PermanentLoss = 1f, Supply = 900f, Pending = new() { "D02" } } };   // v13 (TC-E)
        s.LastDebrief = new CampaignState.DebriefReport { MissionId = "c1_01_hoeryong", Title = "1-1", TotalLost = 5, EnemyKills = 40, ShellsFired = new() { ["field"] = 10 }, LootedShells = new() { ["field"] = 2 }, PrisonersTaken = 4 };
        return s;
    }
}
