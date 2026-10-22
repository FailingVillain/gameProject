using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 통신 · 지휘 구조 알파 조각 ① (Comms.cs · Difficulty.cs, docs/COMMS_ALPHA_PLAN.md): 난이도 데이터 · 유선/전령 지연 범위 · 도착 시 대대 구성원 실행 ·
/// 분실 (받는 대대 소멸) · 잘못된 발송 거부 · 저장 왕복 · 리플레이 재현 · 기본 상태 체크섬 불변. 창 없이. KD_SELFTEST_ONLY=comms 로 단독 실행.
/// </summary>
public static class CommsSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("comms", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[comms 통신 · 난이도]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestDelay(CommsVia.Wire);
            TestDelay(CommsVia.Courier);
            TestLoss();
            TestReject();
            TestDefaultHash();
            TestRoundTrip();
            TestReplay(opt);
            TestCaptureCut();
            TestAiIgnoresCouriers();
            TestRangeClamp();
            TestBrokenJson();
        }
        catch (Exception e) { check(false, $"comms 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(bool record = false, int size = 80)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = record };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Round(s / Config.SimDt);

    /// <summary>faction 의 보병 n 명 → 중대 → 대대 1.</summary>
    static (Battalion B, List<Unit> Men) Battalion(World w, int faction, Vector2 at, int n = 8)
    {
        var men = new List<Unit>();
        for (int i = 0; i < n; i++) men.Add(w.SpawnUnit(defs.Units["infantry"], faction, at + new Vector2(i % 4, i / 4)));
        w.FormCompany(faction, men, 1);
        w.Step();
        var b = w.Battalions.First(x => x.Owner == faction);
        return (b, men);
    }

    static void TestValues()
    {
        var d = DifficultyDefs.Current;
        var e = d.Get("easy").Comms; var nm = d.Get("normal").Comms; var h = d.Get("hard").Comms;
        check(d.Default == "easy" && d.IsKnown("easy") && d.IsKnown("Normal") && d.IsKnown("hard") && !d.IsKnown("insane"), $"difficulty.json: 기본 {d.Default} · 난이도 {string.Join("/", d.Levels.Keys)}");
        check(e.WireMinSec == 1 && e.WireMaxSec == 3 && e.CourierMinSec == 10 && e.CourierMaxSec == 20 && !e.WireCut && nm.WireCut && h.WireCut,
            $"통신 규칙: 유선 {e.WireMinSec}~{e.WireMaxSec}초 · 전령 {e.CourierMinSec}~{e.CourierMaxSec}초 · 쉬움 유선 안 끊김 ({e.WireCut}/{nm.WireCut}/{h.WireCut})");
        check(e.CourierCapture && nm.CourierCapture && h.CourierCapture && !d.CommsRules.Enabled,
            $"D15: 전 난이도 전령 사로잡힘 가능 ({e.CourierCapture}/{nm.CourierCapture}/{h.CourierCapture}) · commsRules 기본 끔 ({d.CommsRules.Enabled})");
        bool ai1 = d.Levels.Values.All(l => l.Ai.AccuracyMul == 1f && l.Ai.AmmoMul == 1f && l.Ai.ReinforceMul == 1f);
        check(ai1, "AI 배수 (정확도 · 탄약 · 증원) 모두 1.0 — 값은 사용자 승인 뒤");
        var w = Bare();
        check(w.Difficulty == "easy", $"새 월드 = 기본 난이도 ({w.Difficulty})");
        w.Difficulty = "HARD"; bool hard = w.Difficulty == "hard";
        w.Difficulty = "nope"; check(hard && w.Difficulty == "easy", "난이도 키 정규화 (대소문자 · 모르는 키 = 기본)");
    }

    static void TestDelay(CommsVia via)
    {
        var lv = DifficultyDefs.Current.Get("easy").Comms;
        float lo = via == CommsVia.Wire ? lv.WireMinSec : lv.CourierMinSec, hi = via == CommsVia.Wire ? lv.WireMaxSec : lv.CourierMaxSec;
        int okRange = 0, okWait = 0, okRun = 0, n = 0;
        long minT = long.MaxValue, maxT = 0;
        for (int k = 0; k < 6; k++)
        {
            var w = Bare();
            Steps(w, k * 7);   // 발송 틱을 바꿔 해시가 달라지게
            var (b, men) = Battalion(w, 1, new Vector2(20, 20));
            var goal = new Vector2(50, 20);
            w.DispatchOrder(1, b, via, new MoveCommand(1, Array.Empty<int>(), goal));
            w.Step();
            var d = w.Dispatches.Single();
            long t = d.ArriveTick - d.SentTick; n++;
            minT = Math.Min(minT, t); maxT = Math.Max(maxT, t);
            if (t >= Sec(lo) && t <= Sec(hi) && d.State == DispatchState.Transit) okRange++;
            Steps(w, (int)(d.ArriveTick - w.Tick) - 1);
            if (men.All(u => u.Order != UnitOrder.Move) && d.State == DispatchState.Transit) okWait++;
            Steps(w, 2);
            if (d.State == DispatchState.Delivered && men.Count(u => u.Alive && u.Order == UnitOrder.Move) == men.Count) okRun++;
        }
        check(okRange == n && okWait == n && okRun == n, $"{via}: 도착 {minT * Config.SimDt:0.0}~{maxT * Config.SimDt:0.0}초 (규칙 {lo}~{hi}) · 도착 전 대기 {okWait}/{n} · 도착 후 대대 전원 이동 {okRun}/{n}");
    }

    static void TestLoss()
    {
        var w = Bare();
        var (b, men) = Battalion(w, 1, new Vector2(20, 20));
        w.DispatchOrder(1, b, CommsVia.Courier, new MoveCommand(1, Array.Empty<int>(), new Vector2(50, 50)));
        w.Step();
        foreach (var u in men) u.Hp = 0;
        Steps(w, 3);
        var d = w.Dispatches.FirstOrDefault();
        bool lost = d != null && d.State == DispatchState.Lost && d.Loss == DispatchLoss.TargetGone;
        Steps(w, Sec(DifficultyDefs.Current.Get("easy").Comms.KeepSec) + 2);
        check(lost && w.Dispatches.Count == 0, $"분실: 받는 대대가 사라지면 Lost (TargetGone) · keepSec 뒤 목록에서 정리 (lost {lost}, 남음 {w.Dispatches.Count})");
    }

    static void TestReject()
    {
        var w = Bare();
        var (b, _) = Battalion(w, 2, new Vector2(60, 60));
        w.Notices.Clear();
        w.Issue(new DispatchOrderCommand(1, b.Id, CommsVia.Wire, new MoveCommand(1, Array.Empty<int>(), new Vector2(10, 10))));
        var (b1, _) = Battalion(w, 1, new Vector2(20, 20));
        w.Issue(new DispatchOrderCommand(1, b1.Id, CommsVia.Wire, new DispatchOrderCommand(1, b1.Id, CommsVia.Wire, new StopCommand(1, Array.Empty<int>()))));
        w.Step();
        check(w.Dispatches.Count == 0 && w.Notices.Count(x => x.Result == CommandResult.NotAllowed) >= 2, $"거부: 남의 대대 · 겹친 발송 (알림 {w.Notices.Count})");
    }

    static void TestDefaultHash()
    {
        var a = Bare(); var c = Bare();
        Battalion(a, 1, new Vector2(20, 20)); Battalion(c, 1, new Vector2(20, 20));
        ulong h0 = a.ComputeChecksum();
        c.Difficulty = "normal";
        check(a.ComputeChecksum() == h0 && c.ComputeChecksum() != h0, "체크섬: 기본 상태 (전달 없음 · 기본 난이도) 는 생략 · 난이도가 다르면 반영");
    }

    static void TestRoundTrip()
    {
        var w = Bare(record: true);
        w.Difficulty = "hard";
        var (b, _) = Battalion(w, 1, new Vector2(20, 20));
        var (b2, men2) = Battalion(w, 2, new Vector2(60, 60));
        w.DispatchOrder(1, b, CommsVia.Courier, new AttackMoveCommand(1, Array.Empty<int>(), new Vector2(55, 55)) { Sel = 8 });
        w.DispatchOrder(2, b2, CommsVia.Wire, new HoldCommand(2, Array.Empty<int>()));
        Steps(w, Sec(4));   // 유선은 도착 (keepSec 안), 전령은 전달 중
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool fields = y.Difficulty == "hard" && y.Dispatches.Count == 2 && y.Dispatches[0].State == DispatchState.Transit && y.Dispatches[1].State == DispatchState.Delivered
            && y.Dispatches[0].Order is AttackMoveCommand { Sel: 8 } && y.Dispatches[0].ArriveTick == w.Dispatches[0].ArriveTick;
        var h0 = w.ComputeChecksum();
        w.Dispatches[0].ArriveTick++; bool hash = w.ComputeChecksum() != h0; w.Dispatches[0].ArriveTick--;
        Steps(w, Sec(25)); Steps(y, Sec(25));
        check(same && fields && hash && w.ComputeChecksum() == y.ComputeChecksum() && w.Dispatches.Count == 0, $"저장: 바이트 동일 {same} · 필드 {fields} · 체크섬 반영 {hash} · 이어 돌려도 같음 (형식 {Snapshot.FormatVersion})");
        var cmd = w.CommandLog.Select(c => c.Command).OfType<DispatchOrderCommand>().FirstOrDefault();
        bool json = cmd != null && JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize<GameCommand>(cmd, CommandJson.Options), CommandJson.Options) is DispatchOrderCommand back
            && back.Battalion == cmd.Battalion && back.Via == cmd.Via && back.Order is AttackMoveCommand am && am.Target == new Vector2(55, 55) && am.Sel == 8;
        check(json, "리플레이: 발송 명령 (안의 명령 포함) JSON 왕복");
    }

    /// <summary>T10b: 전령 사로잡힘 · 유선 끊김 — commsRules 꺼짐 = 기존 동작 (보통에서도 전달), 켜짐 = 보통 사로잡힘/끊김 · 쉬움 (courierCapture · wireCut false) 전달.</summary>
    static void TestCaptureCut()
    {
        var dd = DifficultyDefs.Current;
        var saved = dd.CommsRules;
        try
        {
            (DispatchState St, DispatchLoss Loss) Run(string lv, CommsVia via, bool on)
            {
                dd.CommsRules = new CommsRulesDef { Enabled = on, CaptureChance = 1f, CutChance = 1f, CaptureRadius = 8f, CutRadius = 15f, CheckSec = 1f };
                var w = Bare();
                w.Difficulty = lv;
                var (b, _) = Battalion(w, 1, new Vector2(40, 40));
                // 적 1 명: 전령 길 (사령부 → 대대) 가운데 · 유선은 대대 둘레 15칸 안
                w.SpawnUnit(defs.Units["infantry"], 2, via == CommsVia.Courier ? new Vector2(20, 20) : new Vector2(40, 52));
                w.Step();
                w.DispatchOrder(1, b, via, new HoldCommand(1, Array.Empty<int>()));
                w.Step();
                var d = w.Dispatches.Single();
                for (int i = 0; i < Sec(22) && d.State == DispatchState.Transit; i++) w.Step();
                return (d.State, d.Loss);
            }
            var offC = Run("normal", CommsVia.Courier, false); var offW = Run("normal", CommsVia.Wire, false);
            check(offC.St == DispatchState.Delivered && offW.St == DispatchState.Delivered, $"commsRules 꺼짐 (기본) = 기존 동작: 보통 전령 {offC.St} · 유선 {offW.St}");
            var nC = Run("normal", CommsVia.Courier, true); var hC = Run("hard", CommsVia.Courier, true); var eC = Run("easy", CommsVia.Courier, true);
            check(nC == (DispatchState.Lost, DispatchLoss.CourierCaptured) && hC == (DispatchState.Lost, DispatchLoss.CourierCaptured) && eC == (DispatchState.Lost, DispatchLoss.CourierCaptured),
                $"전령 사로잡힘 (켜짐, D15 쉬움 포함): 보통 {nC.Loss} · 어려움 {hC.Loss} · 쉬움 {eC.Loss}");
            var nW = Run("normal", CommsVia.Wire, true); var eW = Run("easy", CommsVia.Wire, true);
            check(nW == (DispatchState.Lost, DispatchLoss.WireCut) && eW.St == DispatchState.Delivered, $"유선 끊김 (켜짐, 발송 시점 적 접촉): 보통 {nW.Loss} · 쉬움 wireCut=false {eW.St}");
            // 저장 · 이어 돌리기: 전달 중에 저장 → 불러온 월드도 같은 틱에 사로잡힘 (숨은 상태 없음)
            dd.CommsRules = new CommsRulesDef { Enabled = true, CaptureChance = 1f, CaptureRadius = 8f, CheckSec = 1f };
            var x = Bare(); x.Difficulty = "normal";
            var (xb, _) = Battalion(x, 1, new Vector2(40, 40));
            x.SpawnUnit(defs.Units["infantry"], 2, new Vector2(20, 20));
            x.Step();
            x.DispatchOrder(1, xb, CommsVia.Courier, new HoldCommand(1, Array.Empty<int>()));
            Steps(x, 3);
            var y = Snapshot.Load(Snapshot.Save(x, Meta), defs).World;
            for (int i = 0; i < Sec(22) && x.Dispatches[0].State == DispatchState.Transit; i++) { x.Step(); y.Step(); }
            var dx = x.Dispatches.FirstOrDefault(); var dy = y.Dispatches.FirstOrDefault();
            check(x.ComputeChecksum() == y.ComputeChecksum() && dx?.Loss == DispatchLoss.CourierCaptured && dx?.State == dy?.State && dx?.EndTick == dy?.EndTick,
                $"사로잡힘 판정 저장 왕복: 이어 돌려도 같음 ({dx?.State}/{dx?.Loss} 틱 {dx?.EndTick} vs {dy?.State} 틱 {dy?.EndTick})");
        }
        finally { dd.CommsRules = saved; }
    }

    /// <summary>D15: AI 는 전령을 찾아 사냥하지 않는다 — src/AI 소스에 전령 · 발송 (Courier · Dispatch · CommsVia) 참조가 없어야 한다 (AI 표적 · 과제 선택이 전령을 모른다). 소스 폴더를 못 찾으면 건너뜀.</summary>
    static void TestAiIgnoresCouriers()
    {
        string? dir = null;
        for (var d = new System.IO.DirectoryInfo(AppContext.BaseDirectory); d != null && dir == null; d = d.Parent)
            if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "src", "AI"))) dir = System.IO.Path.Combine(d.FullName, "src", "AI");
        if (dir == null) { check(true, "D15 AI 전령 비참조: src/AI 를 못 찾아 건너뜀"); return; }
        var hits = new List<string>();
        foreach (var f in System.IO.Directory.EnumerateFiles(dir, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            var t = System.IO.File.ReadAllText(f);
            foreach (var k in new[] { "Courier", "Dispatch", "CommsVia" }) if (t.Contains(k, StringComparison.OrdinalIgnoreCase)) hits.Add(System.IO.Path.GetFileName(f) + ":" + k);
        }
        check(hits.Count == 0, $"D15: AI (src/AI) 는 전령 · 발송을 참조하지 않음 — 전령 사냥 없음 ({(hits.Count == 0 ? "0" : string.Join(",", hits))})");
    }

    static void TestRangeClamp()
    {
        check(DifficultyDefs.Current.Warnings.Count == 0, "난이도 파일: 기본 파일은 경고 없음");
        var d = DifficultyDefs.Parse("{\"commsRules\": {\"enabled\": true, \"captureRadius\": 999, \"captureChance\": 3, \"checkSec\": 0, \"cutRadius\": -5, \"cutChance\": -1}}");
        var c = d.CommsRules;
        check(c.Enabled && c.CaptureRadius == 50f && c.CaptureChance == 1f && c.CheckSec == 1f && c.CutRadius == 1f && c.CutChance == 0f && d.Warnings.Count == 5,
            $"범위 밖 commsRules 고정: 반경 {c.CaptureRadius}/{c.CutRadius} 확률 {c.CaptureChance}/{c.CutChance} 점검 {c.CheckSec} (경고 {d.Warnings.Count})");
    }

    static void TestBrokenJson()
    {
        var d = DifficultyDefs.Parse("{ \"commsRules\": { \"enabled\": tru ");
        var c = d.CommsRules;
        check(d.Warnings.Count == 1 && d.Warnings[0].Contains("파싱 실패") && !c.Enabled && c.CaptureChance == 0.25f && c.CutRadius == 15f
            && d.Default == "easy" && d.Levels.ContainsKey("easy"),
            $"깨진 JSON: 경고 1 + 기본값 · 꺼짐 (경고 {d.Warnings.Count})");
    }

    static void TestReplay(GameOptions opt)
    {
        var w = World.CreateStandard(GameMap.Generate(64, 64, opt.Seed, 2), defs);
        w.Difficulty = "normal";
        w.Issue(new DebugSpawnCommand(1, "infantry", new Vector2(20, 20), 8));
        w.Step();
        var men = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "infantry" && Vector2.Distance(u.Pos, new Vector2(20, 20)) < 4).ToList();
        w.FormCompany(1, men, 1);
        w.Step();
        var b = w.Battalions.First(x => x.Owner == 1);
        w.DispatchOrder(1, b, CommsVia.Courier, new MoveCommand(1, Array.Empty<int>(), new Vector2(30, 30)));
        Steps(w, Sec(30));
        var rep = ReplayFile.FromWorld(w);
        string path = TestTemp.Child("comms.kdr.json");
        rep.Save(path);
        var loaded = ReplayFile.Load(path);
        long bad = loaded.Verify(defs, out var report);
        check(loaded.Difficulty == "normal" && bad < 0 && men.Count == 8, $"리플레이: 난이도 머리말 · 전달 명령 재현 ({report})");
    }
}
