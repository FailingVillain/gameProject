using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 전술 AI 3단계 B (방어 · 예비 · 역습, TacticalAi.Reserve3B.cs): 프로필 겹침 (3b · no3b) · KD_AI_OVERLAY · 저탄약 교대 · 사건 투입 (틈) · 예비 재지정 ·
/// 역습 병력비 문턱 · 끄면 옛 동작 · 저장 이어 하기. 창 없이. KD_SELFTEST_ONLY=ai3b.
/// </summary>
public static class Ai3bSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("ai3b", 1, true, "tactical");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[ai3b 전술 AI 3B 방어 · 예비 · 역습]");
        try
        {
            TestProfiles();
            TestCounterGo();
            TestAmmoRotation();
            TestGapPlugAndRedesignate();
            TestSnapshot();
        }
        catch (Exception e) { check(false, $"ai3b 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    /// <summary>방어 대대 (한국군 소총 중대 3, 주저항선 + 예비) vs 멀리 있는 봉천 (방어 태세 — 다가오지 않음), 160×120.</summary>
    static string DefendMission(string prof1)
    {
        string co(int n) => $"{{\"units\":{{\"officer\":2,\"infantry\":{n}}},\"formation\":\"line\"}}";
        var cos = string.Join(",", Enumerable.Range(0, 3).Select(_ => co(40)));
        return $$$"""
        {"id":"ai3b_def","title":"ai3b","date":"1920","location":"t","briefing":"ai3b","seed":1920,"player":1,"spectate":true,
         "factions":[{"name":"한국군","en":"korean","lang":"ko","color":[60,130,235],"team":1,"posture":"defend","area":"hold","aiProfile":"{{{prof1}}}"},
                     {"name":"봉천 군벌","en":"fengtian","lang":"zh","color":[165,95,225],"team":2,"posture":"defend","area":"far"}],
         "areas":{"hold":{"rect":[50,30,60,20]},"far":{"rect":[60,100,40,12]}},
         "mapData":{"name":"ai3b","width":160,"height":120,"base":"grass","starts":[[80,40],[80,106]]},
         "objectives":[{"id":"crush","type":"eliminate","percent":97,"faction":2}],
         "failures":[{"type":"timeLimit","seconds":900}],
         "events":[],
         "forces":[
           {"id":"f1_bn","faction":1,"at":[80,40],"facing":180,"formation":"line","battalion":1,"battalionForm":"line","companies":[{{{cos}}}]},
           {"id":"f2_co","faction":2,"at":[80,106],"facing":0,"company":true,"formation":"line","units":{"officer_fengtian":1,"infantry_fengtian":20}}]}
        """;
    }

    static World MissionWorld(string json, int seed = 1)
    {
        var def = MissionDef.Parse(json, "ai3b_def");
        var w = World.CreateMissionWorld(def, defs, seed, null);
        FactionsSelfTest.PinRifles(w);
        World.AttachMissionAi(w, def.Player, true);
        return w;
    }

    static void Steps(World w, float seconds) { for (int i = 0, n = (int)MathF.Round(seconds / Config.SimDt); i < n && w.Winner == 0; i++) w.Step(); }

    static TacFaction St(World w, int f) => ((TacticalAi)w.Controllers.First(c => c.Faction == f && c.Ai is TacticalAi).Ai).StateOf(f)!;

    /// <summary>예비를 둔 방어 대대가 생길 때까지 (최대 limit 초).</summary>
    static TacBattalion? StepToReserve(World w, float limit = 40)
    {
        for (int i = 0, n = (int)MathF.Round(limit / Config.SimDt); i < n; i++)
        {
            w.Step();
            if (i % 30 == 0 && St(w, 1).Battalions.FirstOrDefault(b => b.Task == TacTask.Defend && b.ReserveGroup != 0) is { } bb && i > 300) return bb;
        }
        return St(w, 1).Battalions.FirstOrDefault(b => b.Task == TacTask.Defend && b.ReserveGroup != 0);
    }

    static void Drain(World w, TacGroup g)
    {
        if (w.FindCompany(g.CompanyId) is { } co) foreach (var u in co.Members) if (u.UsesAmmo) u.Ammo = 0;
    }

    // ---------------------------------------------------------------- 테스트

    static void TestProfiles()
    {
        var d = AiProfiles.Find("default")!;
        check(d.Tactics.AmmoRotate && d.Tactics.ReserveEvents && d.Tactics.ReserveRedesignate && d.Tactics.CounterOdds == 0 && d.Tactics.DefendRepick == 0 && !d.Strategy.FixPush && d.Strategy.FixWait == 20f,
            "default 프로필: 저탄약 교대 · 사건 투입 · 재지정 켬, 역습 병력비 · 진지 히스테리시스 · 고정 밀기 끔 (측정 docs/ai3b_raw, ai_profiles.json)");
        var on = AiProfiles.Find("korean+3b");
        var k = AiProfiles.Find("korean")!;
        check(on != null && on.Tactics.AmmoRotate && on.Tactics.ReserveEvents && on.Tactics.ReserveRedesignate && on.Tactics.CounterOdds == 1.8f && on.Tactics.DefendRepick == 0.5f && on.Strategy.FixPush && on.Strategy.FixWait == 35f
              && on.Tactics.Trench == k.Tactics.Trench && on.Strategy.Aggressiveness == k.Strategy.Aggressiveness, "겹침 korean+3b: 3B 키만 켜고 세력 값은 그대로");
        var off = AiProfiles.Find("korean+3b+no3b");
        check(off != null && !off.Tactics.AmmoRotate && !off.Tactics.ReserveEvents && off.Tactics.CounterOdds == 0 && !off.Strategy.FixPush && off.Strategy.FixWait == 20f, "겹침 no3b: 3B 전부 끔");
        string? old = Environment.GetEnvironmentVariable("KD_AI_OVERLAY");
        try
        {
            Environment.SetEnvironmentVariable("KD_AI_OVERLAY", "no3b");
            var r = AiProfiles.Resolve(null, null, "korean");
            check(!r.Tactics.AmmoRotate && !r.Tactics.ReserveEvents && r.Tactics.DefendRepick == 0 && r.Name == "korean+no3b", $"KD_AI_OVERLAY=no3b → 모든 세력에 겹침 ({r.Name})");
            Environment.SetEnvironmentVariable("KD_AI_OVERLAY", null);
            check(AiProfiles.Resolve(null, null, "korean").Tactics.AmmoRotate, "KD_AI_OVERLAY 없음 = 프로필 그대로");
        }
        finally { Environment.SetEnvironmentVariable("KD_AI_OVERLAY", old); }
    }

    static void TestCounterGo()
    {
        // 2단계 문턱 (counterOdds 0): 돌파한 적 30 vs 예비 30 = 역습, 1.8 이면 54 필요
        check(TacticalAi.CounterGo(true, false, 30, 0, 0, 30, 0f, 0.45f) && !TacticalAi.CounterGo(true, false, 30, 0, 0, 30, 1.8f, 0.45f) && TacticalAi.CounterGo(true, false, 30, 0, 0, 54, 1.8f, 0.45f),
            "역습 병력비: 2단계 = 돌파 적 ≤ 예비 ×1.2, counterOdds 1.8 = 예비 ≥ 적 ×1.8");
        check(TacticalAi.CounterGo(true, false, 30, 30 * 0.4f, 0, 33, 1.8f, 0.45f) && !TacticalAi.CounterGo(true, false, 30, 30 * 0.4f, 0, 30, 1.8f, 0.45f),
            "제압된 (0.35 이상) 돌파 적: 문턱 ×0.6 (적 30 → 예비 33 이면 역습)");
        check(!TacticalAi.CounterGo(false, true, 20, 0, 40, 40, 1.8f, 0.45f) && TacticalAi.CounterGo(false, true, 20, 0, 40, 72, 1.8f, 0.45f) && TacticalAi.CounterGo(false, true, 20, 0, 40, 50, 0f, 0.45f),
            "공격이 꺾인 순간 (정면 적 40): 1.8 = 예비 72, 2단계 = 예비 50 (×0.8 문턱)");
        check(!TacticalAi.CounterGo(true, true, 0, 0, 0, 100, 1.8f, 0.45f), "표적 둘레 적 0 = 역습 없음");
    }

    static void TestAmmoRotation()
    {
        foreach (bool on in new[] { true, false })
        {
            var w = MissionWorld(DefendMission(on ? "korean+3b" : "korean+no3b"));
            var b = StepToReserve(w);
            var s = St(w, 1);
            if (b == null) { check(false, "방어 대대 예비 중대가 생기지 않음"); return; }
            int resId = b.ReserveGroup;
            Vector2 dir = b.Dir;
            var front = s.Groups.Where(g => g.Battalion == b.Id && g.Kind == TacKind.Line && g.Id != resId).OrderBy(g => g.Id).ToList();
            var low = front[0];
            Drain(w, low);
            float before = Vector2.Dot(low.Center, dir);
            Steps(w, 4);
            if (on)
            {
                check(s.AmmoRotations == 1 && b.ReserveGroup == low.Id && low.Task == TacTask.Reserve && low.RotateOut && !b.ReserveCommitted,
                    $"저탄약 교대: 탄약 0 중대 L{low.Id} → 예비 (교대 {s.AmmoRotations}, 예비 L{b.ReserveGroup})");
                var inc = s.Groups.First(g => g.Id == resId);
                check(inc.Task == TacTask.Defend && TacticalAi.GroupAmmo(w, s, inc) >= 0.99f, $"들어오는 중대 L{resId}: 방어 과제 (탄약 {TacticalAi.GroupAmmo(w, s, inc):P0})");
                Steps(w, 30);
                check(Vector2.Dot(low.Center, dir) < before - 4f, $"나가는 중대가 뒤로 걸어감 ({before:0.0} → {Vector2.Dot(low.Center, dir):0.0}, 정면 방향 기준)");
                int n1 = s.AmmoRotations;
                Steps(w, 10);
                check(s.AmmoRotations == n1, "쿨다운 · 예비 탄약 부족: 곧바로 다시 교대하지 않음");
            }
            else check(s.AmmoRotations == 0 && b.ReserveGroup == resId && low.Task != TacTask.Reserve, "no3b: 탄약이 바닥나도 교대 없음 (옛 동작)");
        }
    }

    static void TestGapPlugAndRedesignate()
    {
        var w = MissionWorld(DefendMission("korean+3b"));
        var b = StepToReserve(w);
        var s = St(w, 1);
        if (b == null) { check(false, "방어 대대 예비 중대가 생기지 않음 (틈)"); return; }
        int resId = b.ReserveGroup;
        var gap = s.Groups.Where(g => g.Battalion == b.Id && g.Kind == TacKind.Line && g.Id != resId).OrderByDescending(g => g.Id).First();
        Vector2 slot = gap.TaskPoint;
        gap.Spent = true;   // 붕괴한 전선 중대 (AI 상태만 — 시험용)
        Steps(w, 3);
        var pg = s.Groups.First(g => g.Id == resId);
        check(s.PlugGaps == 1 && b.PlugGroup == resId && b.ReserveCommitted && Vector2.Distance(b.PlugPoint, slot) < 6f && pg.Task == TacTask.Defend,
            $"틈 투입: 예비 L{resId} → 붕괴 중대 칸 ({b.PlugPoint.X:0},{b.PlugPoint.Y:0}) (틈 {s.PlugGaps})");
        gap.Spent = false;
        Steps(w, s.Profile.Tactics.PlugSeconds + 2);
        check(b.PlugGroup == 0, "plugSeconds 뒤 전선 합류");
        Steps(w, s.Profile.Tactics.ReserveCalm + 4);
        check(s.ReserveRedesignations == 1 && !b.ReserveCommitted && b.ReserveGroup != 0, $"조용해지면 예비 재지정 (L{b.ReserveGroup}, 재지정 {s.ReserveRedesignations})");
    }

    static void TestSnapshot()
    {
        var w = MissionWorld(DefendMission("korean+3b"), 2);
        var b = StepToReserve(w);
        var s = St(w, 1);
        if (b == null) { check(false, "방어 대대 예비 중대가 생기지 않음 (저장)"); return; }
        var low = s.Groups.First(g => g.Battalion == b.Id && g.Kind == TacKind.Line && g.Id != b.ReserveGroup);
        Drain(w, low);
        Steps(w, 3);
        var bytes = Snapshot.Save(w, Meta);
        var l = Snapshot.Load(bytes, defs).World;
        var s2 = ((TacticalAi)l.Controllers.First(c => c.Faction == 1 && c.Ai is TacticalAi).Ai).StateOf(1)!;
        var low2 = s2.Groups.First(g => g.Id == low.Id);
        check(low.RotateOut && low2.RotateOut && low2.RotateUntil == low.RotateUntil && s2.AmmoRotations == s.AmmoRotations && s2.Battalions.First(x => x.Id == b.Id).RotateTick == b.RotateTick
              && s2.Profile.Tactics.AmmoRotate, "저장 · 불러오기: 교대 상태 · 쿨다운 · 프로필 3B 키 같음");
        Steps(w, 30); Steps(l, 30);
        check(w.ComputeChecksum() == l.ComputeChecksum() && St(w, 1).Orders == s2.Orders, "이어 하기 30초: 체크섬 · 명령 수 같음");
    }
}
