using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>교전 테스트 모드 · BattleAi · 리플레이 · 세력 언어 코드 · 보이스 규칙 · 밸런스 도구 스모크.</summary>
public static class BattleSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Guard(check, "설정 · 파싱", () => Setup(check));
        Guard(check, "월드 생성 · 규칙", () => WorldRules(check));
        Guard(check, "BattleAi 교전", () => AiFight(check));
        Guard(check, "대규모 2000 대 2000", () => Big(check));
        Guard(check, "리플레이", () => ReplayCheck(check));
        Guard(check, "세력 언어", () => Languages(check));
        Guard(check, "밸런스 도구", () => BalanceSmoke(check));
    }

    static void Guard(Action<bool, string> check, string name, Action body)
    {
        try { body(); }
        catch (Exception e) { check(false, $"교전 테스트 '{name}' 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Make(string a, string b, string map = "flat", int distance = 40, int seed = 7, bool fog = false, bool ai = true)
    {
        var setup = new BattleSetup { SideA = a, SideB = b, Map = map, Distance = distance, AiVsAi = true };
        var w = setup.CreateWorld(Defs.Current, seed);
        w.Fog.Enabled = fog;
        if (ai)
        {
            w.Controllers.Add((1, new BattleAi()));
            w.Controllers.Add((2, new BattleAi()));
        }
        return w;
    }

    static void Setup(Action<bool, string> check)
    {
        var defs = Defs.Current;
        var p = BattleSetup.Parse("infantry:1500, tank_ft_male:300,infantry:100", defs);
        check(p.Count == 2 && p[0] == ("infantry", 1600) && p[1] == ("tank_ft_male", 300), "구성 문자열 파싱: 같은 종류는 합치고 balance.json 순서");
        check(BattleSetup.Format(p) == "infantry:1600,tank_ft_male:300", "구성 문자열 되돌리기");
        bool Throws(Action f) { try { f(); return false; } catch (ArgumentException) { return true; } }
        check(Throws(() => BattleSetup.Parse("zeppelin:3", defs)), "알 수 없는 유닛은 거부");
        check(Throws(() => BattleSetup.Parse("infantry", defs)) && Throws(() => BattleSetup.Parse("infantry:-5", defs)), "잘못된 형식 · 음수는 거부");
        check(Throws(() => new BattleSetup { SideA = "infantry:2001", SideB = "tank_ft_male:1" }.Validate(defs)), "한 쪽 2000기 초과는 거부");
        check(!Throws(() => new BattleSetup { SideA = "infantry:2000", SideB = "tank_ft_male:2000" }.Validate(defs)), "한 쪽 정확히 2000기는 허용");
        check(Throws(() => new BattleSetup { SideA = "", SideB = "tank_ft_male:1" }.Validate(defs)), "빈 세력은 거부");
        check(Throws(() => new BattleSetup { SideA = "infantry:1", SideB = "tank_ft_male:1", Map = "moon" }.Validate(defs)), "알 수 없는 맵은 거부");
        check(BattleSetup.Maps.All(m => File.Exists(MapFile.PathOf("battle_" + m))), "교전 맵 파일(flat · terrain · city) 존재");
    }

    static void WorldRules(Action<bool, string> check)
    {
        foreach (var map in BattleSetup.Maps)
        {
            var w = Make("recruit:120,infantry:80,tank_ft_male:12", "veteran:60,heavy_at_rifleman:40,scout:30", map, 40, ai: false);
            int a = w.Units.Count(u => u.Owner == 1), b = w.Units.Count(u => u.Owner == 2);
            check(w.BattleMode && a == 212 && b == 130, $"[{map}] 세력별 유닛 수 {a}/{b} = 212/130, 교전 모드");
            check(!w.Buildings.Any(x => !x.Def.Capturable), $"[{map}] 일반 건물 없음 (점령 시설만)");
            check(w.IsHostile(1, 2) && !w.Allied(1, 2), $"[{map}] 시작부터 적대");
            check(w.Units.All(u => w.Map.IsWalkable((int)u.Pos.X, (int)u.Pos.Y)), $"[{map}] 모든 유닛이 이동 가능한 타일에 생성");
            double ax = w.Units.Where(u => u.Owner == 1).Average(u => u.Pos.X), bx = w.Units.Where(u => u.Owner == 2).Average(u => u.Pos.X);
            check(ax < w.Map.Width / 2f && bx > w.Map.Width / 2f, $"[{map}] 세력 1 은 왼쪽({ax:0}), 세력 2 는 오른쪽({bx:0})");
            for (int i = 0; i < 30; i++) w.Step();
            check(w.Winner == 0 && !w.Faction(1).Defeated && !w.Faction(2).Defeated, $"[{map}] 건물이 없어도 시작 직후 패배 처리되지 않음 (교전 모드 승패)");
            check(!w.AmmoEnabled, $"[{map}] 교전 테스트(기본)는 탄약 무한");
        }
        // 한 쪽을 지우면 그 세력 패배 · 상대 승리
        var w2 = Make("infantry:10", "infantry:10", ai: false);
        foreach (var u in w2.Units.Where(u => u.Owner == 2)) u.Hp = 0;
        for (int i = 0; i < 5; i++) w2.Step();
        check(w2.Winner == 1 && w2.Faction(2).Defeated && !w2.Faction(1).Defeated, "한 쪽이 전멸하면 상대 승리 · 전멸한 쪽 패배");
        var w3 = Make("infantry:5", "infantry:5", ai: false);
        foreach (var u in w3.Units) u.Hp = 0;
        for (int i = 0; i < 5; i++) w3.Step();
        check(w3.Winner == -1, "동시 전멸은 무승부");
        // 사람 명령도 정상: 세력 1 이동 명령
        var w4 = Make("infantry:5", "infantry:5", ai: false);
        var mine = w4.Units.Where(u => u.Owner == 1).ToList();
        Vector2 before = mine[0].Pos;
        w4.Move(1, mine, before + new Vector2(0, 10));
        for (int i = 0; i < 120; i++) w4.Step();
        check(mine[0].Pos.Y > before.Y + 3, "사람(세력 1) 이동 명령이 교전 월드에서 동작");
    }

    static void AiFight(Action<bool, string> check)
    {
        // 안개 켠 평지 100 대 100: 서로 안 보이는 거리에서 시작해도 BattleAi 가 적 쪽으로 진격해 승부가 난다
        var w = Make("infantry:100", "infantry:100", fog: true);
        while (w.Tick < 6 * 60 * 60 && w.Winner == 0) w.Step();
        int a = w.Units.Count(u => u.Owner == 1), b = w.Units.Count(u => u.Owner == 2);
        check(w.Winner > 0 && (w.Winner == 1 ? b : a) == 0, $"안개 속 100 대 100: 승자 {w.Winner}, 남은 {a}/{b}, {w.Time:0}초");
        check(w.Units.Count < 200, "교전으로 실제 손실 발생");

        // 대전차보병은 기갑을 우선 지정 공격
        var w2 = Make("recruit:60,heavy_at_rifleman:30", "tank_ft_male:12,infantry:20");
        var tanks = w2.Units.Where(u => u.Def.IsArmored).Select(u => u.Id).ToHashSet();
        while (w2.Tick < 4 * 60 * 60 && w2.Winner == 0) w2.Step();
        int atOrders = w2.CommandLog.Count(c => c.Command is AttackCommand ac && ac.Faction == 1 && tanks.Contains(ac.TargetId));
        check(atOrders > 0, $"대전차보병이 기갑을 지정 공격 (명령 {atOrders}개)");
        check(w2.CommandLog.All(c => c.Command is MoveCommand or AttackMoveCommand or AttackCommand or RetreatCommand), "BattleAi 는 이동 · 어택땅 · 공격 · 후퇴 명령만 사용");

        // 결정성: 같은 설정 → 같은 체크섬
        ulong Run()
        {
            var x = Make("recruit:80,infantry:60,tank_ft_male:6", "veteran:50,heavy_at_rifleman:20,scout:15", "terrain", 36, 3, fog: true);
            for (int i = 0; i < 2400; i++) x.Step();
            return x.ComputeChecksum();
        }
        check(Run() == Run(), "교전 월드는 같은 설정이면 같은 체크섬 (결정적)");

        // 후퇴: 체력이 크게 줄고 수적으로 밀리는 부대는 Move(후퇴) 명령을 낸다
        var w3 = Make("infantry:30", "infantry:400", distance: 24);
        while (w3.Tick < 90 * 60 && w3.Winner == 0) w3.Step();
        check(w3.CommandLog.Any(c => c.Command is MoveCommand mc && mc.Faction == 1) || w3.Winner == 2, "수적으로 밀리는 부대가 후퇴 명령을 내거나 전멸함");
    }

    static void Big(Action<bool, string> check)
    {
        var w = Make("recruit:800,infantry:700,veteran:300,heavy_at_rifleman:200", "recruit:800,infantry:700,veteran:300,tank_ft_male:200", "terrain", fog: true);
        int total = w.Units.Count;
        check(total == 4000, $"2000 대 2000 생성: {total}기");
        var sw = Stopwatch.StartNew();
        double worst = 0;
        long guard = 0;
        while (w.Tick < 3 * 60 * 60 && w.Winner == 0)
        {
            long t0 = Stopwatch.GetTimestamp();
            w.Step();
            worst = Math.Max(worst, Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            guard++;
        }
        Console.WriteLine($"  [info] 2000 대 2000 (지형, 안개): {w.Time:0}초 결판 승자 {w.Winner}, 실행 {sw.Elapsed.TotalSeconds:0.0}초, Step 평균 {sw.Elapsed.TotalMilliseconds / Math.Max(1, guard):0.00}ms 최대 {worst:0}ms");
        // 2026-10-18 교전 개편(명중 0.30→0.04 곡선 · 사거리 16) 뒤 대규모 교전은 3분 안에 끝나지 않는다 — 진행(사상자) 만 확인
        int dead = total - w.Units.Count(u => u.Alive);
        check(w.Winner != 0 || dead >= total / 5, $"2000 대 2000 3분: 사상자 {dead}/{total} (승자 {w.Winner}; 20% 이상이면 정상 진행)");
        check(worst < 400, $"2000 대 2000: Step 최대 {worst:0}ms < 400ms");
    }

    static void ReplayCheck(Action<bool, string> check)
    {
        var defs = Defs.Current;
        var setup = new BattleSetup { SideA = "recruit:60,infantry:40,tank_ft_male:5", SideB = "veteran:40,heavy_at_rifleman:20,scout:10", Map = "city", Distance = 60, AiVsAi = true };
        var w = setup.CreateWorld(defs, 11);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, new BattleAi()));
        w.Controllers.Add((2, new BattleAi()));
        while (w.Tick < 100 * 60 && w.Winner == 0) w.Step();
        var rep = ReplayFile.FromWorld(w);
        check(rep.Battle != null && rep.Battle.SideA == setup.SideA && rep.Battle.Map == "city" && rep.Seed == 11, "리플레이 머리말에 교전 설정 기록");
        string path = TestTemp.Child("battle_selftest.kdr.json");
        rep.Save(path);
        var loaded = ReplayFile.Load(path);
        check(loaded.Battle != null && loaded.Battle.SideB == setup.SideB && loaded.Battle.Distance == 60, "저장 → 불러오기에 교전 설정 유지");
        long bad = loaded.Verify(defs, out string report);
        check(bad == -1, $"교전 리플레이 재생 검증: {report}");
        var t = ReplayFile.Load(path);
        t.EndChecksum ^= 1;
        check(t.Verify(defs, out _) != -1, "교전 리플레이 변조 감지");
        try { File.Delete(path); } catch (IOException) { }
    }

    static void Languages(Action<bool, string> check)
    {
        var w = World.CreateStandard(MapFile.Load("corners4", 1), Defs.Current);
        MapFile.ApplyFactions(w);
        var langs = Enumerable.Range(1, 4).Select(f => w.Faction(f).Lang).ToArray();
        check(langs.SequenceEqual(new[] { "ko", "ja", "zh", "ru" }), $"corners4 세력 언어: 한국 ko, 일본 ja, 봉천 zh, 러시아 백군 ru (실제 {string.Join(",", langs)})");
        var g = World.CreateStandard(GameMap.Generate(100, 100, 1), Defs.Current);
        check(g.Faction(1).Lang == "en" && g.Faction(2).Lang == "en", "생성 맵 세력 언어 기본 en");
        var b = Make("infantry:3", "infantry:3", ai: false);
        MapFile.ApplyFactions(b);
        check(b.Faction(1).Lang == "en" && b.Faction(2).Lang == "en", "교전 모드 세력 언어 en");
        // 언어는 시뮬레이션과 무관: 바꿔도 체크섬이 같다
        var x = World.CreateStandard(MapFile.Load("corners4", 1), Defs.Current);
        ulong c1 = x.ComputeChecksum();
        x.Faction(1).Lang = "ru";
        check(x.ComputeChecksum() == c1, "세력 언어는 체크섬에 영향 없음 (표시 정보)");
        // 보이스 문서가 모든 사건을 설명한다
        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "sounds", "ww2", "voice");
        string readme = File.Exists(Path.Combine(dir, "README.txt")) ? File.ReadAllText(Path.Combine(dir, "README.txt")) : "";
        check(UI.Sfx.VoiceEvents.Keys.All(k => readme.Contains(k)), $"voice/README.txt 가 모든 사건({string.Join(", ", UI.Sfx.VoiceEvents.Keys)})을 설명");
        // 보이스 파일은 의도적으로 제거됨 (README 만 남김) — 폴더 존재는 검사하지 않고, 없는 파일은 아래처럼 조용히 처리되는지만 본다
        check(!UI.Sfx.PlayVoice(1, "en", "select"), "보이스 파일 · 오디오 장치 없이 PlayVoice 는 조용히 false");
    }

    static void BalanceSmoke(Action<bool, string> check)
    {
        var defs = Defs.Current;
        var m = BalanceTool.RunMatch(defs, "recruit", "tank_ft_male", 1200, "flat", 1, swap: false);
        check(m.Winner != 0 && m.CostX0 > 0 && m.NX > 0 && m.NY > 0 && m.EndSec > 0, $"밸런스 교전 1판: 승자 {m.Winner}, {m.NX}대{m.NY}, {m.EndSec:0}초");
        var m2 = BalanceTool.RunMatch(defs, "recruit", "tank_ft_male", 1200, "flat", 1, swap: true);
        check(m2.Winner != 0, "좌우 교환 판도 결판");
        var g = BalanceTool.RunGame(defs, "gen2_std", 1, 2.5);
        check(g.Factions.Count == 2 && g.Factions.All(f => f.Series.Count >= 4 && f.Series[0].Length == BalanceTool.SeriesColumns.Length), $"AI 경기 기록: 세력 {g.Factions.Count}, 표본 {g.Factions[0].Series.Count}개");
        string dir = TestTemp.Child("balance_selftest");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "results_0.jsonl"), new[]
        {
            JsonSerializer.Serialize(m), JsonSerializer.Serialize(m2), JsonSerializer.Serialize(g),
        });
        BalanceTool.Report(defs, dir);
        string text = File.Exists(Path.Combine(dir, "report.md")) ? File.ReadAllText(Path.Combine(dir, "report.md")) : "";
        check(text.Contains("동일 비용 교전") && text.Contains("B-관찰") && File.Exists(Path.Combine(dir, "matchups.csv")) && File.Exists(Path.Combine(dir, "ai_series.csv")),
              "보고서 · CSV 생성");
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }
}
