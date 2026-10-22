using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 전술 AI 3C-1 (T34a, TacticalAi.Coop3C.cs): 겹침 3c · no3c · 3c_suppthr · 3c_coverwd, 키 끔 = 동작 불변 (같은 시드 체크섬), 문턱 경계, 엄호 분대 수 · 엄호 철수 결정성. 창 없이. KD_SELFTEST_ONLY=s3c1.
/// </summary>
public static class S3c1SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[s3c1 전술 AI 3C-1 제압 문턱 · 엄호 철수]");
        try
        {
            TestProfiles();
            TestThresholds();
            TestCoverWithdraw();
        }
        catch (Exception e) { check(false, $"s3c1 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static void TestProfiles()
    {
        var d = AiProfiles.Find("default")!.Tactics;
        check(d.SuperioritySupp == 0 && d.BoundSuppression == 0 && d.PinnedTargetWeight == 0 && d.OwnPronePivot == 0 && !d.CoverWithdraw && d.CoverSec == 15f,
            "default: 3C-1 키 전부 끔 (superioritySupp · boundSuppression · pinnedTargetWeight · ownPronePivot 0, coverWithdraw false, coverSec 15)");
        var k = AiProfiles.Find("korean")!;
        var on = AiProfiles.Find("korean+3c")!;
        check(on.Tactics.CoverWithdraw && on.Tactics.PinnedTargetWeight > 0 && on.Tactics.OwnPronePivot > 0 && on.Tactics.SuperioritySupp > 0 && on.Tactics.Trench == k.Tactics.Trench,
            "겹침 korean+3c: 3C-1 키 켬, 세력 값 그대로");
        var off = AiProfiles.Find("korean+3c+no3c")!;
        check(!off.Tactics.CoverWithdraw && off.Tactics.PinnedTargetWeight == 0 && off.Tactics.OwnPronePivot == 0 && off.Tactics.SuperioritySupp == 0 && off.Tactics.BoundSuppression == 0, "겹침 no3c: 3C-1 전부 끔");
        var cw = AiProfiles.Find("korean+3c_coverwd")!;
        check(cw.Tactics.CoverWithdraw && cw.Tactics.PinnedTargetWeight == 0 && cw.Tactics.ChargeSuppression == k.Tactics.ChargeSuppression, "겹침 3c_coverwd: 엄호 철수만");
        var st = AiProfiles.Find("korean+3c_suppthr")!;
        check(!st.Tactics.CoverWithdraw && st.Tactics.SuperioritySupp > 0, "겹침 3c_suppthr: 문턱만");
    }

    static void TestThresholds()
    {
        var T = new TacticsProfile();
        check(TacticalAi.BoundSuppLimit(T) == 0.4f, "boundSuppression 0 = 옛 상수 0.4");
        T.BoundSuppression = 0.6f;
        check(TacticalAi.BoundSuppLimit(T) == 0.6f, "boundSuppression 0.6 = 상한 0.6");
        check(!TacticalAi.PronePivot(T, 10, 10), "ownPronePivot 0 = 끔 (전원 엎드려도)");
        T.OwnPronePivot = 0.4f;
        check(TacticalAi.PronePivot(T, 4, 10) && !TacticalAi.PronePivot(T, 3, 10) && !TacticalAi.PronePivot(T, 0, 0), "ownPronePivot 0.4: 4/10 전환, 3/10 아님 (경계), 병사 0 아님");
        check(TacticalAi.CoverCount(0.34f, 3) == 1 && TacticalAi.CoverCount(0.34f, 1) == 0 && TacticalAi.CoverCount(0.9f, 3) == 2 && TacticalAi.CoverCount(0.5f, 4) == 2 && TacticalAi.CoverCount(0f, 4) == 0,
            "엄호 분대 수: 몫 × 분대 수 반올림, 1 이상 분대 수 − 1 이하, 분대 2 미만 · 몫 0 = 없음");
    }

    /// <summary>한국군 소총 중대 1 (공격) 대 참호 없는 봉천 방어 2중대 — 중대가 후퇴할 때까지.</summary>
    static string Mission(string prof)
    {
        return $$$"""
        {"id":"s3c1_att","title":"s3c1","date":"1920","location":"t","briefing":"s3c1","seed":1920,"player":1,"spectate":true,
         "factions":[{"name":"한국군","en":"korean","lang":"ko","color":[60,130,235],"team":1,"posture":"attack","aiProfile":"{{{prof}}}"},
                     {"name":"봉천 군벌","en":"fengtian","lang":"zh","color":[165,95,225],"team":2,"posture":"defend","area":"hold"}],
         "areas":{"hold":{"rect":[50,40,60,16]}},
         "mapData":{"name":"s3c1","width":160,"height":120,"base":"grass","starts":[[80,100],[80,48]]},
         "objectives":[{"id":"crush","type":"eliminate","percent":97,"faction":2}],
         "failures":[{"type":"timeLimit","seconds":900}],
         "events":[],
         "forces":[
           {"id":"f1_co","faction":1,"at":[80,100],"facing":0,"company":true,"formation":"line","units":{"officer":1,"infantry":40}},
           {"id":"f2_co","faction":2,"at":[80,48],"facing":180,"company":true,"formation":"line","units":{"officer_fengtian":1,"infantry_fengtian":45}},
           {"id":"f2_co2","faction":2,"at":[96,50],"facing":180,"company":true,"formation":"line","units":{"officer_fengtian":1,"infantry_fengtian":45}}]}
        """;
    }

    static World MissionWorld(string json, int seed = 1)
    {
        var def = MissionDef.Parse(json, "s3c1_att");
        var w = World.CreateMissionWorld(def, defs, seed, null);
        FactionsSelfTest.PinRifles(w);
        World.AttachMissionAi(w, def.Player, true);
        return w;
    }

    static void Steps(World w, float seconds) { for (int i = 0, n = (int)MathF.Round(seconds / Config.SimDt); i < n && w.Winner == 0; i++) w.Step(); }

    static void TestCoverWithdraw()
    {
        // 끔: 같은 미션 (aiProfile korean) 을 겹침 없이 대 KD_AI_OVERLAY=no3c 로 — 120초 체크섬 같음 (미션 문자열은 같게: 시드 · 해시 영향 배제)
        var a = MissionWorld(Mission("korean"));
        Steps(a, 120);
        string? old = Environment.GetEnvironmentVariable("KD_AI_OVERLAY");
        World b;
        try { Environment.SetEnvironmentVariable("KD_AI_OVERLAY", "no3c"); b = MissionWorld(Mission("korean")); Steps(b, 120); }
        finally { Environment.SetEnvironmentVariable("KD_AI_OVERLAY", old); }
        var sb = b.Controllers.Select(c => c.Ai).OfType<TacticalAi>().First().StateOf(1);
        check(a.ComputeChecksum() == b.ComputeChecksum() && a.Stats.Cover3cWithdraws == 0 && a.Stats.Pinned3cRetargets == 0 && a.Stats.Prone3cPivots == 0 && sb?.Profile.Name == "korean+no3c",
            $"키 끔: 겹침 없음 대 KD_AI_OVERLAY=no3c ({sb?.Profile.Name}) 120초 체크섬 같음, 3C-1 계수 0 (후퇴 {sb?.Fallbacks})");

        // 켬: 엄호 철수가 일어나고 (엄호조 사수 · 본대 후퇴), coverSec 뒤 이탈. 두 번 돌려 같은 체크섬 (결정성).
        var c1 = MissionWorld(Mission("korean+3c_coverwd"));
        var c2 = MissionWorld(Mission("korean+3c_coverwd"));
        long t0 = -1; int holding = 0, fleeing = 0;
        for (int i = 0, n = (int)MathF.Round(400 / Config.SimDt); i < n && c1.Winner == 0; i++)
        {
            c1.Step(); c2.Step();
            if (t0 < 0 && c1.Stats.Cover3cWithdraws > 0) t0 = c1.Tick;
            if (t0 >= 0 && c1.Tick == t0 + (long)MathF.Round(2 / Config.SimDt))   // 명령은 다음 틱에 실행 — 2초 뒤 확인
            {
                foreach (var u in c1.Units) if (u.Owner == 1 && u.Alive && !u.IsOfficer && u.Def.IsInfantry) { if (World.IsFleeing(u)) fleeing++; else holding++; }
            }
            if (t0 >= 0 && c1.Tick - t0 > (long)MathF.Round(17 / Config.SimDt)) break;
        }
        check(t0 >= 0, $"엄호 철수 발생 (후퇴 결정 때 엄호조) — 엄호 병사 {c1.Stats.Cover3cMen}");
        check(t0 < 0 || holding > 0 && fleeing > 0, $"후퇴 직후: 엄호조 남음 {holding} · 본대 후퇴 {fleeing}");
        check(t0 < 0 || c1.Stats.Cover3cReleases > 0, $"coverSec (15초) 뒤 엄호조 이탈 {c1.Stats.Cover3cReleases}");
        check(c1.ComputeChecksum() == c2.ComputeChecksum() && c1.Stats.Cover3cMen == c2.Stats.Cover3cMen, "엄호 철수 결정성: 두 번 같은 체크섬 · 같은 엄호 병사 수");
    }
}
