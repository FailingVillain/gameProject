using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T10 통신 알파 ②~④ (FieldOps.cs): 기본 끔 = 기존 동작 · ② 대포병 탐지 (관측 유닛 거리 · 안개 · 관측 · 만료, 전술 AI 표적) ·
/// ③ 조명탄 (밤 시야 · 명중 낮 값 · 장교 발수 · 낮엔 거부 · 꺼짐) · ④ 행군 피로 (걷기 소모) · 난이도 D15 값 · 난이도 3종 저장 왕복 · 리플레이 재현 · 명령 직렬화.
/// `KD_SELFTEST_ONLY=fieldops`.
/// </summary>
public static class FieldOpsSelfTest
{
    static readonly SnapshotMeta Meta = new("fieldops", 1, false, "standard");
    static Action<bool, string> check = (_, _) => { };
    static Defs defs => Defs.Current;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        Console.WriteLine("[T10 대포병 · 조명탄 · 행군 피로 · 난이도]");
        try
        {
            using var _ = TestDefs.NoCommandDelay();
            TestDefaults();
            TestOffIsNoop();
            WithAllOn(TestCounterBattery);
            WithAllOn(TestFlare);
            WithAllOn(TestMarchFatigue);
            WithAllOn(TestSaveLoad);
            WithAllOn(() => TestReplay(opt));
            WithAllOn(TestAiCounterBattery);
            TestSerialization();
        }
        catch (Exception e) { check(false, $"fieldops 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
        finally { FieldOpsDefs.Override(null); }
    }

    static void WithAllOn(Action a)
    {
        var d = new FieldOpsDefs();
        d.CounterBattery.Enabled = true; d.CounterBattery.DetectChance = 1f;
        d.Flare.Enabled = true; d.MarchFatigue.Enabled = true;
        FieldOpsDefs.Override(d);
        try { a(); } finally { FieldOpsDefs.Override(null); }
    }

    static World Bare(int size = 120, bool fog = true, TimeOfDay time = TimeOfDay.Day)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = true };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = fog;
        if (time != TimeOfDay.Day) w.SetupWeather(WeatherKind.Clear, time);
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Round(s / Config.SimDt);

    static Unit Dummy(World w, int faction, Vector2 at, string key = "infantry")
    {
        var d = w.SpawnUnit(defs.Units[key], faction, at);
        d.Weapon = null; d.Hp = 100000;
        w.Hold(faction, new[] { d });
        return d;
    }

    // ---------------------------------------------------------------- 기본값

    static void TestDefaults()
    {
        var f = FieldOpsDefs.Current;
        check(!f.CounterBattery.Enabled && !f.Flare.Enabled && !f.MarchFatigue.Enabled, "fieldops.json: 대포병 · 조명탄 · 행군 피로 기본 끔");
        var d = DifficultyDefs.Current;
        bool lv = d.IsKnown("easy") && d.IsKnown("normal") && d.IsKnown("hard");
        bool cap = !d.Get("easy").Comms.CourierCapture && d.Get("normal").Comms.CourierCapture && d.Get("hard").Comms.CourierCapture;
        bool ai = d.Levels.Values.All(l => l.Ai.AccuracyMul == 1f && l.Ai.AmmoMul == 1f && l.Ai.ReinforceMul == 1f);
        check(lv && cap && ai, $"난이도 D15 A: 3종 있음 {lv} · 쉬움 전령 사로잡힘 끔 (보통 · 어려움 켬) {cap} · AI 배수 모두 1.0 (어려움 후보는 승인 전) {ai}");
    }

    /// <summary>기본 끔: 포가 쏴도 탐지 없음, 조명탄 명령은 무시, 걷기는 회복.</summary>
    static void TestOffIsNoop()
    {
        var w = Bare(time: TimeOfDay.Night);
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, new Vector2(20.5f, 60.5f));
        Dummy(w, 2, new Vector2(70.5f, 60.5f), "officer");
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(20.5f, 50.5f));
        w.Step();
        w.GroundFire(1, new[] { gun }, new Vector2(70.5f, 70.5f));
        w.FireFlare(1, new[] { off }, new Vector2(30.5f, 50.5f));
        var walker = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(10.5f, 100.5f));
        walker.Fatigue = 30;
        w.Move(1, new[] { walker }, new Vector2(60.5f, 100.5f));
        Steps(w, Sec(45));
        bool shot = gun.LastShotTick > 0;
        check(shot && w.CbMarks.Count == 0 && w.Flares.Count == 0 && off.FlaresUsed == 0 && walker.Fatigue < 30 && w.Stats.CbDetections == 0,
            $"기본 끔: 포 사격 {shot} 뒤 탐지 {w.CbMarks.Count} · 조명탄 {w.Flares.Count} · 걷기 회복 (피로 30 → {walker.Fatigue:0.0})");
    }

    // ---------------------------------------------------------------- ② 대포병

    static void TestCounterBattery()
    {
        var w = Bare();
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, new Vector2(20.5f, 60.5f));
        var near = Dummy(w, 2, new Vector2(70.5f, 60.5f), "officer");        // 50 칸 (탐지 거리 60 안, 시야 밖)
        var tgt = Dummy(w, 2, new Vector2(80.5f, 80.5f));
        w.Step();
        bool hiddenBefore = !w.Fog.CanSee(2, gun) && !w.SeenBySpotter(2, gun);
        w.GroundFire(1, new[] { gun }, tgt.Pos);
        int k = 0;
        while (k < Sec(60) && gun.LastShotTick <= 0) { w.Step(); k++; }
        w.Step();
        bool marked = w.CbMarks.Any(m => m.Faction == 2 && m.GunId == gun.Id);
        bool seen = w.Fog.CanSee(2, gun) && w.SeenBySpotter(2, gun) && !w.Fog.CanSee(1, near);
        // 탐지한 세력의 곡사포가 그 포를 겨눌 수 있음 (엔진 표적 유효성)
        w.Stop(1, new[] { gun });
        Steps(w, Sec(FieldOpsDefs.Current.CounterBattery.RevealSec) + 2);
        bool expired = w.CbMarks.Count == 0 && !w.Fog.CanSee(2, gun);
        check(hiddenBefore && marked && seen && expired,
            $"대포병: 쏘기 전 안 보임 {hiddenBefore} → 사격 뒤 탐지 표시 {marked} · 적에게 보임 · 관측됨 {seen} → {FieldOpsDefs.Current.CounterBattery.RevealSec:0}초 뒤 만료 {expired}");

        // 관측 유닛이 탐지 거리 밖이면 탐지 없음 · 보병만 가까우면 탐지 없음
        var w2 = Bare();
        var g2 = w2.SpawnUnit(defs.Units["heavy_howitzer"], 1, new Vector2(10.5f, 60.5f));
        Dummy(w2, 2, new Vector2(80.5f, 60.5f), "officer");                  // 70 칸
        Dummy(w2, 2, new Vector2(40.5f, 60.5f));                             // 보병 30 칸 (관측 유닛 아님)
        var t2 = Dummy(w2, 2, new Vector2(70.5f, 80.5f));
        w2.Step();
        w2.GroundFire(1, new[] { g2 }, t2.Pos);
        k = 0;
        while (k < Sec(60) && g2.LastShotTick <= 0) { w2.Step(); k++; }
        w2.Step();
        check(g2.LastShotTick > 0 && w2.CbMarks.Count == 0, $"대포병: 관측 유닛 (장교 · 정찰병) 이 {FieldOpsDefs.Current.CounterBattery.DetectRange:0} 칸 밖이면 탐지 없음 (사격 {g2.LastShotTick > 0}, 표시 {w2.CbMarks.Count})");
    }

    /// <summary>전술 AI 대 전술 AI, 양쪽 곡사포: 탐지된 포를 AI 가 표적으로 고른다 (탐지 확률 1).</summary>
    static void TestAiCounterBattery()
    {
        var setup = new BattleSetup { SideA = "infantry:60,officer:4,heavy_howitzer:2", SideB = "infantry:60,officer:4,heavy_howitzer:2", Map = "flat", Distance = 70, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 3);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        int lim = Sec(6 * 60);
        for (int i = 0; i < lim && w.Winner == 0 && w.Stats.CbAiTargets == 0; i++) w.Step();
        check(w.Stats.CbDetections > 0 && w.Stats.CbAiTargets > 0, $"대포병 AI: 탐지 {w.Stats.CbDetections} · 전술 AI 대포병 표적 선택 {w.Stats.CbAiTargets} (시각 {w.Tick * Config.SimDt:0}초)");
    }

    // ---------------------------------------------------------------- ③ 조명탄

    static void TestFlare()
    {
        var w = Bare(time: TimeOfDay.Night);
        var obs = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.5f, 40.5f));
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(19.5f, 41.5f));
        var t = Dummy(w, 2, new Vector2(36.5f, 40.5f));
        w.Hold(1, new[] { obs, off });
        w.Step(); w.Fog.Update();
        bool dark = !w.Fog.CanSee(1, t);
        float pNight = w.HitChance(obs, t);
        w.FireFlare(1, new[] { off }, new Vector2(35.5f, 40.5f));
        w.Step();
        bool lit = w.Flares.Count == 1 && w.Fog.CanSee(1, t) && w.FlareLit(t.Pos) && off.FlaresUsed == 1;
        float pLit = w.HitChance(obs, t);
        float want = defs.Weather.HitMul(WeatherKind.Clear, TimeOfDay.Day) / defs.Weather.HitMul(WeatherKind.Clear, TimeOfDay.Night);
        bool hit = pNight > 0 && MathF.Abs(pLit / pNight - want) < 0.01f;
        // 발수 한도 (장교 2발) · 사거리 밖 거부
        w.FireFlare(1, new[] { off }, new Vector2(80.5f, 40.5f)); w.Step();
        bool farRefused = w.Flares.Count == 1 && off.FlaresUsed == 1;
        w.FireFlare(1, new[] { off }, new Vector2(30.5f, 45.5f)); w.Step();
        w.FireFlare(1, new[] { off }, new Vector2(30.5f, 35.5f)); w.Step();
        bool limit = off.FlaresUsed == FieldOpsDefs.Current.Flare.PerOfficer && w.Flares.Count == 2 && off.FlaresLeft == 0;
        // 보병은 조명탄 없음
        int before = w.Flares.Count;
        w.FireFlare(1, new[] { obs }, new Vector2(30.5f, 40.5f)); w.Step();
        bool onlyOfficer = w.Flares.Count == before;
        Steps(w, Sec(FieldOpsDefs.Current.Flare.Sec) + 2);
        w.Fog.Update();
        bool gone = w.Flares.Count == 0 && !w.Fog.CanSee(1, t);
        check(dark && lit && hit && farRefused && limit && onlyOfficer && gone,
            $"조명탄 (밤): 어두워 안 보임 {dark} → 밝힌 원 안 보임 {lit} · 명중 ×{pLit / MathF.Max(1e-6f, pNight):0.000} (낮/밤 {want:0.000}) {hit} · 사거리 밖 거부 {farRefused} · 장교 {FieldOpsDefs.Current.Flare.PerOfficer}발 한도 {limit} · 보병 불가 {onlyOfficer} · {FieldOpsDefs.Current.Flare.Sec:0}초 뒤 꺼짐 {gone}");

        var d = Bare();
        var o2 = d.SpawnUnit(defs.Units["officer"], 1, new Vector2(20.5f, 40.5f));
        d.Step();
        d.FireFlare(1, new[] { o2 }, new Vector2(30.5f, 40.5f)); d.Step();
        check(d.Flares.Count == 0 && o2.FlaresUsed == 0, "조명탄: 낮에는 쏘지 않음 (발수 그대로)");
    }

    // ---------------------------------------------------------------- ④ 행군 피로

    static void TestMarchFatigue()
    {
        var w = Bare(fog: false);
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(10.5f, 40.5f));
        var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(10.5f, 60.5f));
        w.Step();
        w.Move(1, new[] { a }, new Vector2(100.5f, 40.5f));
        Steps(w, Sec(10));
        float walked = a.Fatigue, rest0 = b.Fatigue;
        float want = 10 * FieldOpsDefs.Current.MarchFatigue.WalkDrain;
        w.Stop(1, new[] { a });
        Steps(w, Sec(4));
        bool regen = a.Fatigue < walked;
        check(MathF.Abs(walked - want) < want * 0.25f + 0.5f && rest0 == 0 && regen,
            $"행군 피로: 걷기 10초 피로 {walked:0.0} (≈{want:0.0}) · 정지 회복 {regen} ({a.Fatigue:0.0}) · 서 있는 병사 0");
    }

    // ---------------------------------------------------------------- 저장 · 난이도 3종 · 리플레이

    static void TestSaveLoad()
    {
        foreach (var lv in new[] { "easy", "normal", "hard" })
        {
            var x = Bare(time: TimeOfDay.Night);
            x.Difficulty = lv;
            var gun = x.SpawnUnit(defs.Units["heavy_howitzer"], 1, new Vector2(20.5f, 60.5f));
            Dummy(x, 2, new Vector2(70.5f, 60.5f), "officer");
            var tgt = Dummy(x, 2, new Vector2(80.5f, 80.5f));
            var off = x.SpawnUnit(defs.Units["officer"], 1, new Vector2(20.5f, 50.5f));
            var walker = x.SpawnUnit(defs.Units["infantry"], 1, new Vector2(10.5f, 100.5f));
            x.Step();
            x.GroundFire(1, new[] { gun }, tgt.Pos);
            x.FireFlare(1, new[] { off }, new Vector2(30.5f, 50.5f));
            x.Move(1, new[] { walker }, new Vector2(100.5f, 100.5f));
            int k = 0;
            while (k < Sec(60) && x.CbMarks.Count == 0) { x.Step(); k++; }
            bool mid = x.CbMarks.Count > 0 && x.Flares.Count > 0 && off.FlaresUsed == 1;
            var bytes = Snapshot.Save(x, Meta);
            var y = Snapshot.Load(bytes, defs).World;
            bool fields = y.Difficulty == lv && y.CbMarks.Count == x.CbMarks.Count && y.Flares.Count == x.Flares.Count
                && y.Units.First(u => u.Id == off.Id).FlaresUsed == 1 && y.CbMarks[0].Until == x.CbMarks[0].Until;
            bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes)) && x.ComputeChecksum() == y.ComputeChecksum();
            var h0 = x.ComputeChecksum();
            x.CbMarks[0].Until++; bool h1 = x.ComputeChecksum() != h0; x.CbMarks[0].Until--;
            x.Flares[0].Radius += 0.5f; bool h2 = x.ComputeChecksum() != h0; x.Flares[0].Radius -= 0.5f;
            off.FlaresUsed++; bool h3 = x.ComputeChecksum() != h0; off.FlaresUsed--;
            bool cont = true;
            for (int i = 0; i < Sec(30); i++)
            {
                x.Step(); y.Step();
                if (i % 30 == 29 && x.ComputeChecksum() != y.ComputeChecksum()) { cont = false; break; }
            }
            check(mid && fields && same && h1 && h2 && h3 && cont,
                $"저장 ({lv}): 진행 중 상태 {mid} · 필드 복원 {fields} · 바이트 · 체크섬 동일 {same} · 체크섬 반영 (탐지 {h1} · 조명탄 {h2} · 발수 {h3}) · 30초 이어 같음 {cont} (형식 {Snapshot.FormatVersion})");
        }
    }

    static void TestReplay(GameOptions opt)
    {
        foreach (var lv in new[] { "easy", "normal", "hard" })
        {
            var w = World.CreateStandard(GameMap.Generate(64, 64, opt.Seed, 2), defs);
            w.Difficulty = lv;
            w.SetupWeather(WeatherKind.Clear, TimeOfDay.Night);
            w.Issue(new DebugSpawnCommand(1, "officer", new Vector2(20, 20), 1));
            w.Issue(new DebugSpawnCommand(1, "infantry", new Vector2(22, 22), 6));
            w.Step();
            var off = w.Units.First(u => u.Owner == 1 && u.Def.Key == "officer" && Vector2.Distance(u.Pos, new Vector2(20, 20)) < 4);
            var men = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "infantry" && Vector2.Distance(u.Pos, new Vector2(22, 22)) < 4).ToList();
            w.FireFlare(1, new[] { off }, new Vector2(30, 30));
            w.Move(1, men, new Vector2(40, 40));
            Steps(w, Sec(20));
            bool fired = off.FlaresUsed == 1;
            var rep = ReplayFile.FromWorld(w);
            string path = TestTemp.Child($"fieldops-{lv}.kdr.json");
            rep.Save(path);
            var loaded = ReplayFile.Load(path);
            long bad = loaded.Verify(defs, out var report);
            check(fired && loaded.Difficulty == lv && bad < 0, $"리플레이 ({lv}, 밤): 조명탄 {fired} · 난이도 머리말 · 재현 ({report})");
        }
    }

    static void TestSerialization()
    {
        GameCommand a = new FireFlareCommand(1, new[] { 3, 4 }, new Vector2(10.5f, 20.5f));
        var ja = System.Text.Json.JsonSerializer.Serialize(a, CommandJson.Options);
        var ba = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(ja, CommandJson.Options);
        check(ba is FireFlareCommand { Target.X: 10.5f } fa && fa.Units.Length == 2 && ja.Contains("\"flare\"") && ReplayFile.FormatVersion >= 22,
            $"조명탄 명령 직렬화 (flare), 리플레이 형식 {ReplayFile.FormatVersion}");
    }
}
