using System.Numerics;
using System.Reflection;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T32b 전술 AI 연막 수류탄 (TacticalAi.Smoke.cs · TrenchAssault.cs TaSmokeHook): 자료 키 · 기본 꺼짐 · 개활 횡단 투척 · 엄폐 지형 무투척 · 사격 아래 투척 ·
/// 연막 0 무동작 · 쿨다운 · 키 꺼짐 무명령 · 참호 돌격 smokeHook (Ai 플래그만) · 리플레이 왕복 (IsAi false 여도 일치). 창 없이. 단독 KD_SELFTEST_ONLY=smokeai.
/// </summary>
public static class SmokeAiSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    static string Map(bool cover) => $$"""
    { "name": "smokeai test", "width": 200, "height": 100, "base": "grass",
      "starts": [ [10, 10], [190, 90] ],
      "paint": [ {{(cover ? """{ "terrain": "rubble", "rect": [30, 20, 60, 61] }, """ : "")}}{ "terrain": "trench", "rect": [100, 30, 1, 41] } ] }
    """;

    static Vector2 C(int x, int y) => new(x + 0.5f, y + 0.5f);
    static long Ticks(float s) => (long)MathF.Round(s / Config.SimDt);

    /// <summary>1세력 중대 (돌격병 assault + 소총 rifle + 장교) 를 (30, 50) 둘레에, 2세력 소총 foes 명을 (x 76) 에 사수로.</summary>
    static (World W, List<Unit> Att, List<Unit> Def) Setup(int seed, bool cover, int assault = 24, int rifle = 12, int foes = 8)
    {
        var w = World.CreateStandard(MapFile.LoadText(Map(cover), "smokeai", seed), defs);
        var orig = w.Units.ToList();   // 지도 기본 유닛: 우리 배치 뒤에 지운다 (먼저 지우면 세력이 패배 처리돼 AI 가 안 돈다)
        w.Fog.Enabled = false;
        w.RecordCommands = true;
        var att = new List<Unit>();
        int n = assault + rifle;
        for (int i = 0; i < n; i++) att.Add(w.SpawnUnit(defs.Units[i < assault ? "infantry_assault" : "infantry"], 1, new Vector2(24 + i % 12 * 0.9f, 46 + i / 12 * 1.2f)));
        att.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(22, 50)));
        var dfn = new List<Unit>();
        for (int i = 0; i < foes; i++) dfn.Add(w.SpawnUnit(defs.Units["infantry"], 2, C(76, 46 + i)));
        foreach (var u in orig) u.Hp = 0;
        w.Step();
        w.Hold(2, dfn);
        w.Step();
        return (w, att, dfn);
    }

    sealed class Res { public int Cmds, Batches, Open, Fire, Smokes; public long MinGap = long.MaxValue; public World W = null!; public List<CommandRecord> Log = new(); public long T0; public int Seed; public bool Cover; }

    /// <summary>AI (프로필) 로 secs 초 돌린다. hits = 매 틱 구성원 1/5 에 피격 시각을 찍어 '사격 아래' 를 흉내 (명중 판정 없이 RecentHitFrac 만).</summary>
    static Res Go(int seed, bool cover, string profile, float secs, int assault = 24, bool hits = false, bool keepLog = false)
    {
        var (w, att, _) = Setup(seed, cover, assault);
        var r = new Res { W = w, T0 = w.Tick, Seed = seed, Cover = cover };
        w.Faction(1).IsAi = true;
        w.Controllers.Add((1, new TacticalAi(profile)));
        int log0 = w.CommandLog.Count;
        long end = w.Tick + Ticks(secs);
        while (w.Tick < end)
        {
            if (hits) for (int i = 0; i < att.Count; i += 5) if (att[i].Alive) att[i].LastDamagedTick = w.Tick;
            w.Step();
        }
        long last = long.MinValue;
        foreach (var k in w.CommandLog.Skip(log0))
        {
            if (k.Command is not ThrowSmokeCommand { Faction: 1 }) continue;
            r.Cmds++;
            if (k.Tick != last) { if (last != long.MinValue) r.MinGap = Math.Min(r.MinGap, k.Tick - last); last = k.Tick; r.Batches++; }
        }
        r.Open = (int)w.Stats.AiSmokeOpen; r.Fire = (int)w.Stats.AiSmokeFire; r.Smokes = (int)w.Stats.SmokesSpawned;
        if (keepLog) r.Log = w.CommandLog.ToList();
        return r;
    }

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[T32b AI 연막]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestData();
            TestOpenAndReplay(opt.Seed);
            TestCover(opt.Seed);
            TestUnderFire(opt.Seed);
            TestNoSmoke(opt.Seed);
            TestKeyOff(opt.Seed);
            TestHook(opt.Seed);
        }
        catch (Exception e) { check(false, $"smokeai 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static void TestData()
    {
        // tactics.json breachAi 의 모든 키가 BreachAiDef 속성으로 읽힌다 (틀린 키는 TacticRules.Load 가 조용히 기본값으로 떨어뜨리므로 직접 대조)
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "data", "tactics.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var props = typeof(BreachAiDef).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name.ToLowerInvariant()).ToHashSet();
        bool has = doc.RootElement.TryGetProperty("breachAi", out var blk);
        var bad = has ? blk.EnumerateObject().Select(p => p.Name).Where(n => !props.Contains(n.ToLowerInvariant())).ToList() : new List<string> { "(블록 없음)" };
        int keys = has ? blk.EnumerateObject().Count() : 0;
        var B = defs.Tactics.BreachAi;
        check(has && bad.Count == 0 && keys == props.Count, $"tactics.json breachAi 키 {keys}/{props.Count} 모두 속성에 맞음 (모르는 키: {(bad.Count == 0 ? "없음" : string.Join(",", bad))})");
        check(B.SmokeMinMen == 8 && B.SmokeOpenMinDist == 18f && B.SmokeStandoffMin == 8f && B.SmokeStandoffMax == 20f && B.SmokeSpacing == 5f && B.SmokeMaxPerCompany == 4
              && B.SmokeCooldownSec == 20f && B.SmokeUnderFireSupp == 0.25f && B.SmokeUnderFireLoss == 0.05f && B.SmokeSelfBlockGuard,
            $"breachAi 초안 값 (카드): 보유 {B.SmokeMinMen} · 개활 {B.SmokeOpenMinDist} · 거리 {B.SmokeStandoffMin}~{B.SmokeStandoffMax} · 간격 {B.SmokeSpacing} · 동시 {B.SmokeMaxPerCompany} · 쉼 {B.SmokeCooldownSec}초");
        var dp = AiProfiles.Find("default")!; var kp = AiProfiles.Find("korean")!; var on = AiProfiles.Find("korean+smoke_on")!;
        check(!dp.Tactics.SmokeAi && !kp.Tactics.SmokeAi && on.Tactics.SmokeAi && !defs.Tactics.TrenchAssault.SmokeHook,
            $"기본 꺼짐: default {dp.Tactics.SmokeAi} · korean {kp.Tactics.SmokeAi} · smokeHook {defs.Tactics.TrenchAssault.SmokeHook}, 겹침 smoke_on {on.Tactics.SmokeAi}");
    }

    static void TestOpenAndReplay(int seed)
    {
        var r = Go(seed, false, "default+smoke_on", 90f, keepLog: true);
        long cd = Ticks(defs.Tactics.BreachAi.SmokeCooldownSec);
        check(r.Cmds > 0 && r.Open > 0 && r.Smokes > 0, $"개활 횡단 (풀밭 45칸, 적 사수 8): 연막 명령 {r.Cmds} (묶음 {r.Batches}, 개활 {r.Open} · 사격아래 {r.Fire}) · 피운 연막 {r.Smokes}");
        check(r.Batches <= 1 || r.MinGap >= cd, $"쿨다운: 투척 묶음 사이 최소 {(r.Batches <= 1 ? 0 : r.MinGap / 60.0):0.0}초 ≥ {defs.Tactics.BreachAi.SmokeCooldownSec}초 (묶음 {r.Batches})");
        int perBatch = r.Batches > 0 ? r.Log.Where(k => k.Tick >= r.T0 && k.Command is ThrowSmokeCommand { Faction: 1 }).GroupBy(k => k.Tick).Max(g => g.Count()) : 0;
        check(perBatch <= defs.Tactics.BreachAi.SmokeMaxPerCompany, $"한 번에 던지는 수 ≤ smokeMaxPerCompany: 최대 {perBatch}");
        // 리플레이: 같은 시작 + 기록된 명령만 (AI 없음, IsAi false) → 같은 체크섬
        var (w1, _, _) = Setup(seed, false);
        w1.RecordCommands = false;
        foreach (var f in w1.Factions) f.IsAi = false;
        long t0 = w1.Tick;
        var byTick = r.Log.Where(k => k.Tick >= t0).GroupBy(k => k.Tick).ToDictionary(g => g.Key, g => g.Select(k => JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(k.Command, CommandJson.Options), CommandJson.Options)!).ToArray());
        w1.CommandFeed = t => byTick.TryGetValue(t, out var l) ? l : Array.Empty<GameCommand>();
        while (w1.Tick < r.W.Tick) w1.Step();
        check(w1.ComputeChecksum() == r.W.ComputeChecksum() && w1.Stats.SmokesSpawned == r.Smokes,
            $"리플레이 왕복 (켠 상태 90초, 재생 쪽 AI 없음 · IsAi false): 체크섬 일치 {w1.ComputeChecksum() == r.W.ComputeChecksum()} · 연막 {w1.Stats.SmokesSpawned}/{r.Smokes}");
    }

    static void TestCover(int seed)
    {
        var r = Go(seed, true, "default+smoke_on", 90f);
        check(r.Open == 0, $"엄폐 많은 지형 (잔해 60칸): 개활 횡단 투척 없음 {r.Open} (사격아래 {r.Fire})");
    }

    static void TestUnderFire(int seed)
    {
        var r = Go(seed, true, "default+smoke_on", 90f, hits: true);
        check(r.Fire > 0 && r.Open == 0, $"사격 아래 (잔해 지형 + 최근 피격 20%): 투척 {r.Fire} (개활 {r.Open})");
    }

    static void TestNoSmoke(int seed)
    {
        var r = Go(seed, false, "default+smoke_on", 60f, assault: 0);
        check(r.Cmds == 0 && r.Smokes == 0, $"연막 보유 0 (소총 중대): 명령 {r.Cmds} · 연막 {r.Smokes}");
    }

    static void TestKeyOff(int seed)
    {
        var r = Go(seed, false, "default", 90f);
        check(r.Cmds == 0 && r.Open + r.Fire == 0, $"키 꺼짐 (default): CommandLog 연막 명령 {r.Cmds}");
    }

    static (long Hook, long Smokes, bool Broke) HookRun(int seed, bool hook, bool ai)
    {
        var R = defs.Tactics.TrenchAssault;
        bool old = R.SmokeHook;
        R.SmokeHook = hook;
        try
        {
            // N32SelfTest.Setup 과 같은 배치 (참호선 x=100, 공격 중대 = 돌격병 1/3 + 소총 + 경기관총 4 + 장교)
            var (w, att, _) = Setup(seed, false, 0, 0, 0);
            var co = new List<Unit>();
            for (int i = 0; i < 36; i++) co.Add(w.SpawnUnit(defs.Units[i % 3 == 0 ? "infantry_assault" : "infantry"], 1, new Vector2(30 + i % 12 * 0.9f, 44 + i / 12 * 1.2f)));
            for (int i = 0; i < 4; i++) co.Add(w.SpawnUnit(defs.Units["heavy_machinegunner"], 1, new Vector2(29, 44 + i * 1.2f)));
            co.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(28, 50)));
            w.FormCompany(1, co);
            var dfn = new List<Unit>();
            for (int i = 0; i < 20; i++) dfn.Add(w.SpawnUnit(defs.Units["infantry"], 2, C(100, 40 + i)));
            w.OccupyPositions(2, dfn, C(100, 50), new Vector2(-1, 0), 14, 0.9f);
            for (int i = 0; i < 3; i++) w.Step();
            foreach (var u in att) u.Hp = 0;   // Setup 의 장교 (빈 배치) 제거
            w.Step();
            var c = co[0].Company!;
            w.TrenchAssault(1, new[] { c.Id }, C(100, 50), ai);
            bool broke = false;
            for (long t = 0; t < Ticks(300) && !broke; t++) { w.Step(); if (c.TrenchAssaultStage >= TaStage.BreakIn) broke = true; }
            for (int i = 0; i < Ticks(3); i++) w.Step();   // 신관
            return (w.Stats.TaSmokeHook, w.Stats.SmokesSpawned, broke);
        }
        finally { R.SmokeHook = old; }
    }

    static void TestHook(int seed)
    {
        var on = HookRun(seed, true, true);
        var human = HookRun(seed, true, false);
        var off = HookRun(seed, false, true);
        check(on.Broke && on.Hook > 0 && on.Smokes >= on.Hook && on.Hook <= defs.Tactics.BreachAi.SmokeMaxPerCompany,
            $"참호 돌격 smokeHook (AI 플래그): 돌파 도달 {on.Broke} · 투척 {on.Hook} · 연막 {on.Smokes}");
        check(human.Hook == 0 && off.Hook == 0, $"smokeHook 은 AI 돌격만 · 키 꺼짐 무동작: 플레이어 돌격 {human.Hook} · 끔 {off.Hook}");
    }
}
