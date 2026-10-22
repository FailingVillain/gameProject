using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T48c 연대장 호감: 해임 하락 (JSON 값 · 클램프 · 해임 불가 불변) · 작전 태그 ±1 (작전당 1회 · 재직만) · 이벤트 favorDelta · 단계 문턱/배율 · 재기용 판정 · 규칙 JSON 검증 · v7 로드 영향 없음. 그룹 KD_SELFTEST_ONLY=commanders.</summary>
public static class CommanderFavorSelfTest
{
    static CommanderDef Def(string id, bool unfireable = false, int? pen = null, bool rehirable = true, string[]? likes = null, string[]? dislikes = null) => new()
    {
        Id = id, Unfireable = unfireable, DismissFavorPenalty = pen, Rehirable = rehirable,
        Favor = new CommanderFavor { Likes = (likes ?? Array.Empty<string>()).ToList(), Dislikes = (dislikes ?? Array.Empty<string>()).ToList() },
    };

    static CampaignState.CommanderSlot Slot(CampaignState s, string id) => s.CommanderPool.First(c => c.Id == id);

    public static void Run(Action<bool, string> check)
    {
        var savedEco = CampaignEconomyDefs.Current;
        var savedCmd = CommanderCatalog.Current;
        var savedRules = CommanderFavorRules.Current;
        try
        {
            CampaignEconomyDefs.Override(null);
            CommanderFavorRules.Override(null);
            var res = new CommanderLoadResult();
            res.Defs.Add(Def("a", likes: new[] { "수비" }, dislikes: new[] { "공세" }));
            res.Defs.Add(Def("b", unfireable: true));
            res.Defs.Add(Def("c", pen: 5));
            res.Defs.Add(Def("d", rehirable: false));
            CommanderCatalog.Override(res);
            var s = new CampaignState();
            CommanderPools.Sync(s, res.Defs);
            foreach (var c in s.CommanderPool) { c.Status = CommanderStatus.Hired; c.Favor = 30; }

            // 0) 규칙 JSON (실제 파일)
            var rules = CommanderFavorRules.Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "commander_favor.json"));
            check(rules.Warnings.Count == 0 && rules.Tiers.Count == 4 && rules.DeltaSizes.SequenceEqual(new[] { 1, 2, 3, 5 }) && rules.TagMatchDelta == 1 && rules.ScalingEnabled, $"T48c commander_favor.json 로드 (경고 {string.Join("/", rules.Warnings)})");

            // 1) 해임 하락: JSON 공통값 2 · 인물별 5 · 해임 불가 불변
            int da = CommanderFavors.ApplyDismissPenalty(s, "a"), dc = CommanderFavors.ApplyDismissPenalty(s, "c"), db = CommanderFavors.ApplyDismissPenalty(s, "b");
            check(da == -2 && Slot(s, "a").Favor == 28 && dc == -5 && Slot(s, "c").Favor == 25 && db == 0 && Slot(s, "b").Favor == 30, "T48c 해임 하락: 공통 -2 · 인물별 -5 · 해임 불가 불변");
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { CommanderDismissFavorPenalty = 7 }.Sanitized());
            CommanderFavors.ApplyDismissPenalty(s, "a");
            check(Slot(s, "a").Favor == 21, "T48c 해임 하락값은 경제 JSON 에서 (코드 박기 없음)");
            Slot(s, "a").Favor = -19;
            int clamp = CommanderFavors.ApplyDismissPenalty(s, "a");
            check(Slot(s, "a").Favor == -20 && clamp == -1, "T48c 하한 클램프 (-20)");
            CampaignEconomyDefs.Override(null);
            check(CommanderFavors.ApplyDismissPenalty(s, "none") == 0, "T48c 모르는 id = 무시");

            // 2) 작전 태그 ±1: 재직만 · 작전당 1회
            foreach (var c in s.CommanderPool) c.Favor = 30;
            Slot(s, "d").Status = CommanderStatus.Dismissed;
            int ch1 = CommanderFavors.ApplyMissionTags(s, "m1", new[] { "수비" });
            check(ch1 == 1 && Slot(s, "a").Favor == 31 && Slot(s, "d").Favor == 30 && Slot(s, "b").Favor == 30 && Slot(s, "a").FavorLastMission == "m1", "T48c 선호 태그 +1 (일치한 재직 연대장만)");
            int ch2 = CommanderFavors.ApplyMissionTags(s, "m1", new[] { "수비" });
            check(ch2 == 0 && Slot(s, "a").Favor == 31, "T48c 같은 작전 중복 반영 안 함 (FavorLastMission)");
            CommanderFavors.ApplyMissionTags(s, "m2", new[] { "공세" });
            check(Slot(s, "a").Favor == 30, "T48c 기피 태그 -1");
            CommanderFavors.ApplyMissionTags(s, "m3", new[] { "수비", "공세" });
            check(Slot(s, "a").Favor == 30, "T48c 선호+기피 동시 = 상쇄");
            Slot(s, "a").Favor = 100;
            CommanderFavors.ApplyMissionTags(s, "m4", new[] { "수비" });
            check(Slot(s, "a").Favor == 100, "T48c 상한 클램프 (100)");

            // 3) 이벤트 favorDelta: 허용 크기 {1,2,3,5} 만
            foreach (var c in s.CommanderPool) c.Favor = 30;
            int n = CommanderFavors.ApplyEventDeltas(s, new Dictionary<string, int> { ["a"] = 3, ["c"] = -5, ["b"] = 4, ["zz"] = 1, ["d"] = 0 });
            check(n == 2 && Slot(s, "a").Favor == 33 && Slot(s, "c").Favor == 25 && Slot(s, "b").Favor == 30, "T48c 이벤트 증감: +3 / -5 적용, 크기 4 · 모르는 id · 0 은 건너뜀");
            string evFile = Path.Combine(TestTemp.Child("kd_favor_ev"), "ev.json");
            Directory.CreateDirectory(Path.GetDirectoryName(evFile)!);
            File.WriteAllText(evFile, "[{\"id\":\"x\",\"choices\":[{\"text\":\"t\",\"effect\":\"e\",\"favorDelta\":{\"a\":2}},{\"text\":\"u\",\"effect\":\"f\"}]}]");
            var evs = CampaignEvents.Load(evFile);
            check(evs.Count == 1 && evs[0].Choices[0].FavorDelta?["a"] == 2 && evs[0].Choices[1].FavorDelta == null, "T48c 이벤트 JSON 선택지 favorDelta 읽기 (없으면 null)");

            // 4) 단계 · 배율 (경계값)
            var names = new[] { -20, 9, 10, 39, 40, 69, 70, 100 }.Select(f => CommanderFavors.TierOf(f).Id).ToArray();
            check(names.SequenceEqual(new[] { "cold", "cold", "normal", "normal", "good", "good", "trusted", "trusted" }), $"T48c 단계 문턱 (냉담<10 · 보통 10~39 · 호의 40~69 · 신뢰>=70): {string.Join(",", names)}");
            check(CommanderFavors.Scale(10, 0) == 5 && CommanderFavors.Scale(10, 20) == 10 && CommanderFavors.Scale(10, 50) == 11 && CommanderFavors.Scale(10, 80) == 13 && CommanderFavors.Scale(-10, 80) == -13 && CommanderFavors.Scale(-3, 50) == -3, "T48c 효과 배율 0.5/1.0/1.15/1.3 (정수, 음수 대칭)");
            CommanderFavorRules.Override(new CommanderFavorRules { ScalingEnabled = false });
            check(CommanderFavors.Scale(10, 0) == 10 && CommanderFavors.Scale(10, 99) == 10, "T48c scalingEnabled=false 면 배율 1.0 고정");
            CommanderFavorRules.Override(null);

            // 5) 재기용: 쿨다운 1작전 (= missionDays 일) · 최소 호감 · 불가 인물 · 해임 상태만
            foreach (var c in s.CommanderPool) { c.Status = CommanderStatus.Dismissed; c.Favor = 10; c.DismissedDay = 5; }
            s.Day = 5;
            bool r0 = CommanderFavors.CanRehire(s, "a", out string why0);
            s.Day = 6;
            bool r1 = CommanderFavors.CanRehire(s, "a", out _);
            check(!r0 && why0.Contains("작전") && r1, "T48c 재기용 쿨다운 1작전: 해임 당일 불가, 다음 작전(Day+1) 가능");
            Slot(s, "a").Favor = -5;
            bool rLow = CommanderFavors.CanRehire(s, "a", out string whyLow);
            Slot(s, "a").Favor = -4;
            check(!rLow && whyLow.Contains("호감") && CommanderFavors.CanRehire(s, "a", out _), "T48c 재기용 최소 호감: -5 이하 불가, -4 가능");
            Slot(s, "a").Status = CommanderStatus.Hired;
            check(!CommanderFavors.CanRehire(s, "a", out _) && !CommanderFavors.CanRehire(s, "d", out _) && !CommanderFavors.CanRehire(s, "zz", out _), "T48c 재기용: 재직 중 · rehirable=false · 모르는 id 불가");

            // 6) 규칙 JSON 검증: 증감 크기 밖이면 경고, 깨지면 기본값
            string rf = Path.Combine(TestTemp.Child("kd_favor_rules"), "r.json");
            Directory.CreateDirectory(Path.GetDirectoryName(rf)!);
            File.WriteAllText(rf, "{\"tagMatchDelta\":4,\"deltaSizes\":[1,2,3,5],\"tiers\":[{\"id\":\"x\",\"minFavor\":0,\"effectPermille\":900},{\"bad\":1}]}");
            var badRules = CommanderFavorRules.Load(rf);
            check(badRules.TagMatchDelta == 1 && badRules.Warnings.Count == 2 && badRules.Tiers.Count == 1, $"T48c 증감 크기가 {{1,2,3,5}} 밖이면 로드 경고 + 되돌림 (경고 {badRules.Warnings.Count})");
            File.WriteAllText(rf, "{ not json");
            var broken = CommanderFavorRules.Load(rf);
            check(broken.Warnings.Count == 1 && broken.Tiers.Count == 4 && CommanderFavorRules.Load(rf + ".none").Tiers.Count == 4, "T48c 규칙 파일 깨짐/없음 = 기본값 + 경고");

            // 7) 저장: v7 파일 로드는 호감 규칙과 무관, 호감은 v8 왕복에 실림
            var v7 = CampaignState.FromJson("{\"Version\":7,\"Day\":4}");
            check(v7 != null && v7.Version == CampaignState.SchemaVersion && v7.CommanderPool.All(c => c.Favor >= -20 && c.Favor <= 100), "T48c v7 로드 영향 없음 (풀은 T48a 동기화로 초기 호감)");
            var rt = CampaignState.FromJson(s.ToJson());
            check(rt != null && Slot(rt, "a").Favor == -4 && Slot(rt, "c").Favor == 10 && Slot(rt, "a").FavorLastMission == "m4", "T48c 호감 · FavorLastMission 저장 왕복");
        }
        finally { CampaignEconomyDefs.Override(savedEco); CommanderCatalog.Override(savedCmd); CommanderFavorRules.Override(savedRules); }
    }
}
