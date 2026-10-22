using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 전술 AI 3단계 A (stage 3A, docs/TACTICAL_AI_STAGE3_REVIEW_2026-10-19.md S1 · S14): 기동 계획 선택 결정성 · 측면 우회 상한 · 고정 → 측면 동기 ·
/// 미션 목표 → 과제 · 미션 철수 문턱이 프로필을 덮어씀 · 겹침 프로필 (stage2) · 저장 이어 하기. 창 없이. KD_SELFTEST_ONLY=s3a 로 단독 실행.
/// </summary>
public static class S3aSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("s3a", 1, true, "tactical");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[s3a 기동 계획 · 미션 목표 · 철수]");
        try
        {
            TestProfiles();
            TestPlanDeterministic();
            TestDetourLimit();
            TestSingleCompany();
            TestSequencing();
            TestObjectiveMapping();
            TestMissionWithdraw();
            TestSnapshot();
            TestManReplace();
        }
        catch (Exception e) { check(false, $"s3a 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    /// <summary>참호선 공격 (bn_campaign T_r1.5_RM 축소판): 한국군 소총 중대 3 + 기관총 중대 vs 봉천 소총 중대 2 (참호), 160×120.</summary>
    static string TrenchMission(string objectives = "[{\"id\":\"crush\",\"type\":\"eliminate\",\"percent\":97,\"faction\":2}]", string f1Extra = "", string f2Extra = "", string? prof1 = null, int atk = 3)
    {
        string co(string off, string inf, int n) => $"{{\"units\":{{\"{off}\":3,\"{inf}\":{n},\"infantry_scout\":2}},\"formation\":\"line\"}}";
        var atkCos = string.Join(",", Enumerable.Range(0, atk).Select(_ => co("officer", "infantry", 84)));
        var defCos = string.Join(",", Enumerable.Range(0, 2).Select(_ => co("officer_fengtian", "infantry_fengtian", 92)));
        string p1 = prof1 != null ? $",\"aiProfile\":\"{prof1}\"" : "";
        return $$$"""
        {"id":"s3a_trench","title":"s3a","date":"1920","location":"t","briefing":"s3a","seed":1920,"player":1,"spectate":true,
         "factions":[{"name":"한국군","en":"korean","lang":"ko","color":[60,130,235],"team":1,"posture":"attack","area":"zone"{{{p1}}}{{{f1Extra}}}},
                     {"name":"봉천 군벌","en":"fengtian","lang":"zh","color":[165,95,225],"team":2,"posture":"defend","area":"zone"{{{f2Extra}}}}],
         "areas":{"zone":{"rect":[56,16,48,30]},"cap":{"rect":[60,36,40,12]},"dest":{"rect":[140,100,10,10]}},
         "mapData":{"name":"s3a","width":160,"height":120,"base":"grass","starts":[[80,100],[80,30]],
           "paint":[{"terrain":"dirt","rect":[78,0,4,120]},{"terrain":"forest","rect":[16,50,24,18]},{"terrain":"forest","rect":[120,50,24,18]},
                    {"terrain":"trench","rect":[52,40,24,2]},{"terrain":"trench","rect":[84,40,24,2]},{"terrain":"wire","rect":[48,50,28,1]},{"terrain":"wire","rect":[84,50,28,1]}]},
         "objectives":{{{objectives}}},
         "failures":[{"type":"timeLimit","seconds":900}],
         "events":[],
         "forces":[
           {"id":"f1_bn","faction":1,"at":[80,100],"facing":0,"formation":"line","battalion":1,"battalionForm":"line","companies":[{{{atkCos}}}]},
           {"id":"f1_mg","faction":1,"at":[77,108],"facing":0,"company":true,"formation":"line","units":{"officer":1,"heavy_maxim":5,"heavy_machinegunner":8,"infantry":4}},
           {"id":"f1_wagon","faction":1,"at":[90,108],"facing":0,"tag":"convoy","company":false,"units":{"supply_wagon":1}},
           {"id":"f2_mg","faction":2,"at":[77,35],"facing":180,"company":true,"formation":"line","units":{"officer_fengtian":1,"heavy_maxim":2,"heavy_machinegunner_fengtian":4,"infantry_fengtian":3}},
           {"id":"f2_bn","faction":2,"at":[80,41],"facing":180,"formation":"line","battalion":1,"battalionForm":"line","companies":[{{{defCos}}}]}]}
        """;
    }

    static World MissionWorld(string json, int seed = 1)
    {
        var def = MissionDef.Parse(json, "s3a_trench");
        var w = World.CreateMissionWorld(def, defs, seed, null);
        FactionsSelfTest.PinRifles(w);   // N54
        World.AttachMissionAi(w, def.Player, true);
        return w;
    }

    static void Steps(World w, float seconds) { for (int i = 0, n = (int)MathF.Round(seconds / Config.SimDt); i < n && w.Winner == 0; i++) w.Step(); }

    static TacFaction St(World w, int f) => ((TacticalAi)w.Controllers.First(c => c.Faction == f && c.Ai is TacticalAi).Ai).StateOf(f)!;

    static TacRegiment? PlanReg(World w) => St(w, 1).Regiments.FirstOrDefault(r => r.Plan != ManPlan.None);

    /// <summary>계획이 정해질 때까지 (최대 limit 초) 진행.</summary>
    static TacRegiment? StepToPlan(World w, float limit = 60)
    {
        for (int i = 0, n = (int)MathF.Round(limit / Config.SimDt); i < n && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 30 == 0 && PlanReg(w) is { } r) return r;
        }
        return PlanReg(w);
    }

    // ---------------------------------------------------------------- 3B2 대체 중대 (strategy.manReplace)

    static void TestManReplace()
    {
        foreach (bool on in new[] { true, false })
        {
            var w = MissionWorld(TrenchMission(prof1: on ? "korean+3b_manrepl" : "korean", atk: 4), 3);
            var r = StepToPlan(w);
            var s = St(w, 1);
            if (r == null || r.Plan is not (ManPlan.Flank or ManPlan.Pincer) || r.PlanStage != 1) { check(false, $"대체 중대: 측면 · 협공 계획이 안 생김 ({r?.Plan})"); return; }
            var gone = s.Groups.Where(g => g.Element == ManElement.Manoeuvre).Select(g => g.Id).ToList();
            foreach (var g in s.Groups) if (g.Element == ManElement.Manoeuvre) { g.Element = ManElement.None; g.ManStage = 0; }   // 기동 제대 후퇴 (AI 상태만 — 시험용)
            int aborts = s.PlanAborts;
            Steps(w, 3);
            var nm = s.Groups.Where(g => g.Element == ManElement.Manoeuvre).ToList();
            if (on)
                check(s.ManReplaces == 1 && r.PlanStage == 1 && nm.Count == 1 && !gone.Contains(nm[0].Id) && nm[0].ManStage == 1 && nm[0].ManWay == r.PlanWay && s.PlanAborts == aborts,
                    $"manReplace: 기동 제대가 빠지면 고정 중대 L{(nm.Count > 0 ? nm[0].Id : 0)} 를 같은 경유점으로 (대체 {s.ManReplaces}, 계획 단계 {r.PlanStage})");
            else
                check(s.ManReplaces == 0 && r.PlanStage == 2 && nm.Count == 0 && s.PlanAborts == aborts + 1, $"manReplace 끔: 기동 제대가 빠지면 계획 취소 (옛 동작, 단계 {r.PlanStage})");
        }
    }

    // ---------------------------------------------------------------- 프로필 · 겹침

    static void TestProfiles()
    {
        var k = AiProfiles.Find("korean")!; var k2 = AiProfiles.Find("korean+stage2");
        var d = AiProfiles.Find("default")!;
        check(d.Strategy.Manoeuvre && d.Strategy.Objectives && !d.Strategy.PrePlan && d.Strategy.PlanFrontal == 0 && d.Strategy.PlanFlank == 0 && d.Strategy.PlanPincer == 0 && d.Strategy.PlanSchwerpunkt == 0
              && d.Strategy.MaxDetour == 2f && d.Strategy.MaxArrive == 120f && d.Strategy.WithdrawAt == 0f && d.Strategy.Commitment == 0.5f,
            $"기본 프로필: 3단계 A 켬 · 계획 편향 0 (중립, 성격 보류) · 우회 상한 ×{d.Strategy.MaxDetour} / {d.Strategy.MaxArrive}초 (결정 4) · 철수 0 · 고수 0.5");
        check(k2 != null && !k2.Strategy.Manoeuvre && !k2.Strategy.Objectives && k2.Strategy.Aggressiveness == k.Strategy.Aggressiveness && k2.Tactics.Trench == k.Tactics.Trench && k2.Name == "korean+stage2",
            "겹침 korean+stage2 = 한국군 값 + 3단계 A 끔 (같은 실행 파일 2단계 비교)");
        check(AiProfiles.Find("korean+없음") == null && AiRegistry.Create("tactical:korean+stage2") is TacticalAi, "모르는 겹침은 거부 · tactical:korean+stage2 생성");
    }

    // ---------------------------------------------------------------- 계획 선택 결정성

    static void TestPlanDeterministic()
    {
        (ManPlan, int, long, Vector2, Vector2, int, ulong) Once()
        {
            var w = MissionWorld(TrenchMission(), 3);
            var r = StepToPlan(w);
            Steps(w, 20);
            r = PlanReg(w);
            var s = St(w, 1);
            return r == null ? default : (r.Plan, r.PlanSide, r.PlanTick, r.PlanWay, r.PlanAxis, s.Groups.Count(g => g.Element == ManElement.Manoeuvre), w.ComputeChecksum());
        }
        var a = Once(); var b = Once();
        check(a.Item1 is ManPlan.Flank or ManPlan.Pincer, $"참호 1.5:1 (측면 길 있음): AI 가 스스로 측면 · 협공을 고름 ({a.Item1}, 쪽 {a.Item2}, 기동 중대 {a.Item6})");
        check(a == b, $"계획 선택 결정적: 같은 입력 → 같은 계획 · 경유점 · 체크섬 ({a.Item1}{(a.Item2 > 0 ? "+" : "-")} t={a.Item3})");
        // 2단계 겹침: 계획 없음
        var w2 = MissionWorld(TrenchMission(prof1: "korean+stage2"), 3);
        Steps(w2, 60);
        var s2 = St(w2, 1);
        check(s2.PlansFlank + s2.PlansPincer + s2.PlansFrontal + s2.PlansSchwerpunkt == 0 && s2.Regiments.All(r => r.Plan == ManPlan.None) && s2.Groups.All(g => g.Element == ManElement.None),
            "manoeuvre 끔 (korean+stage2): 계획 · 제대 없음 (2단계 그대로)");
    }

    // ---------------------------------------------------------------- 우회 상한

    static void TestDetourLimit()
    {
        var w = MissionWorld(TrenchMission(), 5);
        var r = StepToPlan(w);
        var S = St(w, 1).Profile.Strategy;
        check(r != null && r.Plan is ManPlan.Flank or ManPlan.Pincer && r.PlanDetour <= S.MaxDetour * r.PlanFront + 0.01f && r.PlanEta <= S.MaxArrive + 0.01f,
            $"측면 우회 상한: 경로 {r?.PlanDetour:0} ≤ 정면 거리 {r?.PlanFront:0} × {S.MaxDetour}, 도착 예정 {r?.PlanEta:0}초 ≤ {S.MaxArrive}초");
        // 상한을 좁히면 (도착 5초) 측면 · 협공 후보가 없다 → 정면 또는 중점
        var w2 = MissionWorld(TrenchMission(), 5);
        Steps(w2, 0.5f);
        St(w2, 1).Profile.Strategy.MaxArrive = 5f;
        var r2 = StepToPlan(w2, 90);
        check(r2 != null && r2.Plan is ManPlan.Frontal or ManPlan.Schwerpunkt && St(w2, 1).Groups.All(g => g.Element == ManElement.None),
            $"상한을 넘는 우회는 고르지 않음 (maxArrive 5초 → {r2?.Plan})");
        var w3 = MissionWorld(TrenchMission(), 5);
        Steps(w3, 0.5f);
        St(w3, 1).Profile.Strategy.MaxDetour = 0.5f;
        var r3 = StepToPlan(w3, 90);
        check(r3 != null && r3.Plan is ManPlan.Frontal or ManPlan.Schwerpunkt, $"우회 경로 상한 (maxDetour 0.5 → {r3?.Plan})");
    }

    static void TestSingleCompany()
    {
        // 중대 하나 (나눌 수 없음) — 측면 · 협공 후보 없이 정면 (F_trench s7 의 Clamp 예외 회귀)
        var w = MissionWorld(TrenchMission(atk: 1), 7);
        var r = StepToPlan(w, 90);
        check(r != null && r.Plan is ManPlan.Frontal or ManPlan.Schwerpunkt && St(w, 1).Groups.All(g => g.Element == ManElement.None), $"소총 중대 1개: 예외 없이 정면 ({r?.Plan})");
    }

    // ---------------------------------------------------------------- 고정 → 측면 동기

    static void TestSequencing()
    {
        bool scoutOld = defs.CombatRules.ScoutAvoid; defs.CombatRules.ScoutAvoid = false;   // N35: 정찰병 자위 사격은 이 대본 (적 AI 조우 시점) 을 바꾸므로 동기 규칙만 보려고 끔
        var w = MissionWorld(TrenchMission(), 3);
        var r = StepToPlan(w);
        bool earlyAssault = false, fixCharge = false; long releaseSeen = 0;
        int manIds = 0;
        for (int i = 0, n = (int)MathF.Round(180 / Config.SimDt); i < n && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 15 != 0 || r == null) continue;
            var s = St(w, 1);
            foreach (var g in s.Groups)
            {
                if (g.Element == ManElement.Manoeuvre) { manIds |= 1; if (g.State is TacState.Assault or TacState.Charge or TacState.Bound) earlyAssault = true; }
                if (g.Element == ManElement.Fix && g.State is TacState.Assault or TacState.Charge or TacState.Bound) fixCharge = true;
            }
            if (r.PlanStage != 1) { releaseSeen = r.ReleaseTick; break; }
        }
        check(r != null && r.FixTick > 0 && r.ArriveTick > 0 && r.ReleaseTick >= r.FixTick && r.ReleaseTick >= r.ArriveTick && releaseSeen > 0,
            $"동기: 고정 교전 t={r?.FixTick / 60f:0} · 기동 도착 t={r?.ArriveTick / 60f:0} → 돌격 허가 t={r?.ReleaseTick / 60f:0} (계획 t={r?.PlanTick / 60f:0})");
        check(manIds == 1 && !earlyAssault && !fixCharge, "풀리기 전에는 기동 제대 · 고정 제대 모두 약진 · 돌격 없음 (고정 = 사격만, 기동 = 이동 · 대기)");
        check(r != null && (r.ArriveTick - r.PlanTick) / 60f <= St(w, 1).Profile.Strategy.MaxArrive * 1.25f + 10f, $"기동 제대 도착 {(r == null ? 0 : (r.ArriveTick - r.PlanTick) / 60f):0}초 (기한 안)");
        var s1 = St(w, 1);
        check(s1.Groups.All(g => g.Element == ManElement.None) && s1.PlanLog.Count >= TacticalAi.PlanLogStride && s1.PlanReleases >= 1, $"풀린 뒤 제대 표시 지움 · 계획 기록 {s1.PlanLog.Count / TacticalAi.PlanLogStride}줄");
        defs.CombatRules.ScoutAvoid = scoutOld;
    }

    // ---------------------------------------------------------------- 미션 목표 → 과제

    static void TestObjectiveMapping()
    {
        var w = MissionWorld(TrenchMission("[{\"id\":\"take\",\"type\":\"capture\",\"area\":\"cap\",\"seconds\":30},{\"id\":\"crush\",\"type\":\"eliminate\",\"percent\":97,\"faction\":2}]"));
        var o1 = TacticalAi.ObjectiveFor(w, 1); var o2 = TacticalAi.ObjectiveFor(w, 2);
        check(o1.Kind == AiObjectiveKind.Take && o2.Kind == AiObjectiveKind.Hold && Vector2.Distance(o1.Point, new Vector2(80, 42)) < 0.01f && o1.Index == 0,
            $"capture (플레이어 쪽 1): 세력 1 = 점령 {o1.Kind} ({o1.Point.X:0},{o1.Point.Y:0}), 세력 2 = 고수 {o2.Kind}");
        w.Mission!.ObjState[0] = MissionState.Done;
        var o1b = TacticalAi.ObjectiveFor(w, 1);
        check(o1b.Kind == AiObjectiveKind.Destroy && TacticalAi.ObjectiveFor(w, 2).Kind == AiObjectiveKind.None, $"달성한 목표는 건너뜀 → 격멸 ({o1b.Kind}) · 표적 세력은 과제 없음");
        var wd = MissionWorld(TrenchMission("[{\"id\":\"hold\",\"type\":\"defend\",\"area\":\"cap\",\"seconds\":300,\"faction\":2}]"));
        check(TacticalAi.ObjectiveFor(wd, 2).Kind == AiObjectiveKind.Hold && TacticalAi.ObjectiveFor(wd, 1).Kind == AiObjectiveKind.Take,
            "defend (세력 2, 300초 버티기): 세력 2 = 고수, 세력 1 = 점령");
        var we = MissionWorld(TrenchMission("[{\"id\":\"esc\",\"type\":\"escort\",\"area\":\"dest\",\"tag\":\"convoy\"}]"));
        var e1 = TacticalAi.ObjectiveFor(we, 1); var e2 = TacticalAi.ObjectiveFor(we, 2);
        check(e1.Kind == AiObjectiveKind.Escort && Vector2.Distance(e1.Aux, new Vector2(145, 105)) < 0.01f && e2.Kind == AiObjectiveKind.Intercept,
            $"escort: 세력 1 = 호송 (호송대 ({e1.Point.X:0},{e1.Point.Y:0}) → 목적지), 세력 2 = 차단 {e2.Kind}");
        var wh = MissionWorld(TrenchMission("[{\"id\":\"take\",\"type\":\"capture\",\"area\":\"cap\",\"seconds\":30,\"hidden\":true}]"));
        check(TacticalAi.ObjectiveFor(wh, 1).Kind == AiObjectiveKind.None, "숨긴 목표는 공개 전까지 과제 아님");
        // 고수 과제: 방어 세력의 지킬 곳 = 목표 구역 (세력 구역 zone 대신 cap)
        var wc = MissionWorld(TrenchMission("[{\"id\":\"take\",\"type\":\"capture\",\"area\":\"cap\",\"seconds\":30}]"));
        Steps(wc, 6);
        var d2 = St(wc, 2).Directives[0];
        check(d2.Effective == AiPosture.Defend && Vector2.Distance(d2.Target, new Vector2(80, 42)) < 0.01f, $"고수 과제 → 방어 기준점 = 목표 구역 중심 ({d2.Target.X:0},{d2.Target.Y:0})");
        var wc2 = MissionWorld(TrenchMission("[{\"id\":\"take\",\"type\":\"capture\",\"area\":\"cap\",\"seconds\":30}]").Replace("\"en\":\"fengtian\",", "\"en\":\"fengtian\",\"aiProfile\":\"fengtian+stage2\","));
        Steps(wc2, 6);
        check(Vector2.Distance(St(wc2, 2).Directives[0].Target, new Vector2(80, 31)) < 0.01f, "objectives 끔 (stage2): 세력 구역 그대로 (80,31)");
    }

    // ---------------------------------------------------------------- 미션 철수 문턱

    static void TestMissionWithdraw()
    {
        var bad = TrenchMission(f2Extra: ",\"aiWithdrawAt\":1.5");
        bool rejected = false;
        try { MissionDef.Parse(bad, "bad"); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "미션 검증: aiWithdrawAt 1.5 거부 (0~1 미만)");
        var w = MissionWorld(TrenchMission(f2Extra: ",\"aiWithdrawAt\":0.85,\"aiCommitment\":0.8"));
        var s2 = St(w, 2);
        check(s2.MissionWithdrawAt == 0.85f && s2.MissionCommitment == 0.8f && s2.Profile.Strategy.WithdrawAt == 0f, "미션 aiWithdrawAt 0.85 · aiCommitment 0.8 → AI 상태 (프로필 withdrawAt 0 을 덮어씀)");
        Steps(w, 240);
        for (int k = 0; k < 8 && !St(w, 2).Directives[0].Withdrawn; k++) Steps(w, 30);   // pursuit2 (후퇴 뒤 재정비 · 후퇴 사격): 공격이 느려져 85% 에 늦게 닿음 — 최대 480초
        var d = St(w, 2).Directives[0];
        check(d.Withdrawn && St(w, 2).Withdrawals == 1 && d.Effective == AiPosture.Defend, $"방어 세력 병력 85% 아래 → 목표 포기 · 철수 (t={d.WithdrawTick / 60f:0}초, 지점 ({d.WithdrawPoint.X:0},{d.WithdrawPoint.Y:0}))");
        var wn = MissionWorld(TrenchMission());
        Steps(wn, 240);
        check(!St(wn, 2).Directives[0].Withdrawn && St(wn, 2).Withdrawals == 0, "미션 값 없음 = 프로필 withdrawAt 0 (끝까지 — 2단계와 같음)");
    }

    // ---------------------------------------------------------------- 저장 이어 하기

    static void TestSnapshot()
    {
        var w = MissionWorld(TrenchMission(), 3);
        var r = StepToPlan(w);
        Steps(w, 8);
        r = PlanReg(w);
        bool mid = r != null && r.PlanStage == 1 && St(w, 1).Groups.Any(g => g.Element == ManElement.Manoeuvre);
        var bytes = Snapshot.Save(w, Meta);
        var l = Snapshot.Load(bytes, defs).World;
        var rl = ((TacticalAi)l.Controllers.First(c => c.Faction == 1 && c.Ai is TacticalAi).Ai).StateOf(1)!.Regiments.FirstOrDefault(x => x.Plan != ManPlan.None);
        check(mid && rl != null && rl.Plan == r!.Plan && rl.PlanStage == r.PlanStage && rl.PlanWay == r.PlanWay && rl.PlanDeadline == r.PlanDeadline && rl.PlanPre == r.PlanPre,
            $"저장 · 불러오기: 진행 중 계획 ({r?.Plan}/{r?.PlanStage}) · 경유점 · 기한이 같다");
        Steps(w, 40); Steps(l, 40);
        var a = St(w, 1); var b = ((TacticalAi)l.Controllers.First(c => c.Faction == 1 && c.Ai is TacticalAi).Ai).StateOf(1)!;
        check(w.ComputeChecksum() == l.ComputeChecksum() && a.PlanLog.SequenceEqual(b.PlanLog) && a.Orders == b.Orders, $"이어 하기 40초: 체크섬 · 계획 기록 · 명령 수 같음 (기록 {a.PlanLog.Count / TacticalAi.PlanLogStride}줄)");
    }
}
