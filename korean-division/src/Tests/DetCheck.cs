using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 크로스 머신 결정성 검사 (A-3). 고정 시나리오(corners4 4인 · 전술 AI 2 + 표준 AI 2, 5분 + 중앙 대규모 전투)를 돌려 체크섬을 낸다.
///   KoreanDivision --detcheck [--update] [--quiet]     체크섬 한 줄 출력 (--update: 기준값 파일 갱신)
///   tools/detcheck.ps1                          환경 변수 · 빌드 변형별로 돌려 전부 같은지 비교
/// 기준값 파일: assets/data/detcheck.golden (같은 값이면 어느 머신 · 런타임에서도 같아야 한다. 다른 에이전트가 시뮬 로직을 바꾸면 값이 바뀌는 게 정상 — --update 로 갱신).
/// </summary>
public static class DetCheck
{
    public const string GoldenFile = "detcheck.golden";
    public const int Minutes = 5;

    public readonly record struct Result(ulong Final, ulong Trace, long Ticks, int Units, int Buildings, long Shots)
    {
        public override string ToString() => $"{Final:x16} {Trace:x16} ticks={Ticks} units={Units} buildings={Buildings} shots={Shots}";
        public string Key => $"{Final:x16} {Trace:x16}";
    }

    /// <summary>시나리오 실행. 100틱마다 체크섬을 섞어 어느 시점에서든 어긋나면 잡는다.</summary>
    public static Result RunScenario(Defs defs, int seed, int minutes = Minutes)
    {
        var map = MapFile.Load("corners4", seed);
        var w = World.CreateStandard(map, defs);
        w.Fog.Enabled = true;
        for (int f = 1; f <= map.Players; f++) w.Controllers.Add((f, AiRegistry.Create(f <= 2 ? "tactical" : "standard")!));   // 전술 AI (세력 1 · 2) + 옛 표준 AI (3 · 4)
        ScriptBattle(w, defs);
        // 날씨 · 시간대 경로도 지나간다: 흐린 해질녘 → 2분 비 → 3.5분 밤
        w.SetupWeather(WeatherKind.Overcast, TimeOfDay.Dusk, new[] { new WeatherEvent(2 * 3600, (sbyte)WeatherKind.Rain, -1), new WeatherEvent(3 * 3600 + 1800, -1, (sbyte)TimeOfDay.Night) });

        ulong trace = 14695981039346656037UL;
        long shots = 0;
        int steps = minutes * 60 * 60;
        for (int i = 0; i < steps; i++)
        {
            w.Step();
            shots += w.Shots.Count(s => s.Tick == w.Tick - 1);
            if (i % 100 == 99) trace = (trace ^ w.ComputeChecksum()) * 1099511628211UL;
        }
        return new Result(w.ComputeChecksum(), trace, w.Tick, w.Units.Count, w.Buildings.Count, shots);
    }

    /// <summary>맵 중앙에서 두 대부대(세력 1 · 2 보병 + 전차 + 대전차병) 충돌, 세력 3 은 사수 · 순찰 분대, 건물 포탑 하나. 안개 · 반격 · 조준 · 추격 경로를 모두 지나간다.</summary>
    static void ScriptBattle(World w, Defs defs)
    {
        float cx = w.Map.Width / 2f, cy = w.Map.Height / 2f;
        string[] mix = { "infantry", "infantry", "recruit", "veteran", "infantry", "heavy_at_rifleman", "tank_ft_male", "recruit", "infantry", "scout" };
        List<Unit> Blob(int faction, Vector2 c, int n)
        {
            var l = new List<Unit>();
            for (int i = 0; i < n; i++)
            {
                float r = MathF.Sqrt(i * 0.8f / MathF.PI), a = i * 2.3999632f;
                l.Add(w.SpawnUnit(defs.Units[mix[i % mix.Length]], faction, c + new Vector2(DetMath.Cos(a), DetMath.Sin(a)) * r));
            }
            return l;
        }
        var a1 = Blob(1, new Vector2(cx - 14, cy), 60);
        var b1 = Blob(2, new Vector2(cx + 14, cy), 60);
        var c1 = Blob(3, new Vector2(cx, cy - 18), 12);
        var d1 = Blob(4, new Vector2(cx, cy + 18), 12);
        w.AttackMove(1, a1, new Vector2(cx + 14, cy));
        w.AttackMove(2, b1, new Vector2(cx - 14, cy));
        w.Hold(3, c1.Take(6));
        w.QuickStep(3, c1.Skip(6), new Vector2(cx, cy));
        w.AttackMove(4, d1, new Vector2(cx, cy - 18));
    }

    public static string FindDataDir()
    {
        string p = Path.Combine(AppContext.BaseDirectory, "assets", "data");
        return p;
    }

    /// <summary>소스 트리의 assets/data (있으면) — --update 가 소스 쪽도 같이 갱신한다.</summary>
    static string? FindSourceDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "KoreanDivision.csproj"))) return Path.Combine(d.FullName, "assets", "data");
        return null;
    }

    public static string? ReadGolden()
    {
        string p = Path.Combine(FindDataDir(), GoldenFile);
        return File.Exists(p) ? File.ReadAllText(p).Trim() : null;
    }

    public static void WriteGolden(Result r)
    {
        string text = r.Key + "\n";
        File.WriteAllText(Path.Combine(FindDataDir(), GoldenFile), text);
        var src = FindSourceDataDir();
        if (src != null && Directory.Exists(src)) File.WriteAllText(Path.Combine(src, GoldenFile), text);
    }

    /// <summary>CLI: --detcheck [--update] [--quiet]. 한 줄 "DETCHECK &lt;final&gt; &lt;trace&gt; ..." 를 출력한다. 기준값과 다르면 종료코드 2 (--update 가 아닐 때).</summary>
    public static int Run(string[] args)
    {
        var defs = Defs.Load();
        if (args.Contains("--spike"))   // 개발용: 16인 10분 Step 스파이크 추적
        {
            int pl = Config.MaxPlayers, size = Config.MapSizeFor(pl);
            var w = World.CreateStandard(GameMap.Generate(size, size, 20261009, pl), defs);
            for (int f = 1; f <= pl; f++) w.Controllers.Add((f, AiRegistry.Create("standard")!));
            World.Profile = true;
            for (int i = 0; i < 600 * 60; i++)
            {
                Array.Clear(World.Prof);
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                w.Step();
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (ms > 40) Console.WriteLine($"tick {w.Tick} {ms:0.0}ms phases: " + string.Join(" ", World.Prof.Take(14).Select(x => (x * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0"))));
            }
            return 0;
        }
        if (args.Contains("--detself"))   // 개발용: 결정성 셀프 테스트만
        {
            int bad = 0;
            SelfTest((ok, name) => { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name}"); if (!ok) bad++; });
            return bad;
        }
        if (args.Contains("--aimtest"))   // 개발용: 사격 지연 테스트만
        {
            int fails = 0;
            AimSelfTest.Run(new GameOptions { Seed = Config.DefaultSeed }, (ok, name) => { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name}"); if (!ok) fails++; });
            return fails;
        }
        var r = RunScenario(defs, Config.DefaultSeed);
        Console.WriteLine("DETCHECK " + r);
        if (args.Contains("--update")) { WriteGolden(r); Console.WriteLine("golden updated"); return 0; }
        string? g = ReadGolden();
        bool ok = g == r.Key;
        Console.WriteLine(g == null ? "GOLDEN missing" : ok ? "GOLDEN match" : $"GOLDEN MISMATCH (expected {g})");
        return g == null || ok ? 0 : 2;
    }

    /// <summary>셀프 테스트: 같은 프로세스에서 두 번 돌려 일치 + 기준값과 일치.</summary>
    public static void SelfTest(Action<bool, string> check)
    {
        var defs = Defs.Current;
        float eSin = 0, eAtan = 0;
        for (float x = -50f; x < 50f; x += 0.0137f)
        {
            eSin = MathF.Max(eSin, MathF.Abs(DetMath.Sin(x) - MathF.Sin(x)));
            eSin = MathF.Max(eSin, MathF.Abs(DetMath.Cos(x) - MathF.Cos(x)));
            float yy = MathF.Sin(x * 1.7f) * 3, xx = MathF.Cos(x * 0.9f) * 3;
            eAtan = MathF.Max(eAtan, MathF.Abs(DetMath.Atan2(yy, xx) - MathF.Atan2(yy, xx)));
        }
        check(eSin < 1e-5f && eAtan < 1e-6f && DetMath.Atan2(0, 0) == 0 && DetMath.Atan2(0, -1) == DetMath.Pi, $"DetMath 정확도: sin/cos 오차 {eSin:E1}, atan2 오차 {eAtan:E1}");
        var a = RunScenario(defs, Config.DefaultSeed);
        var b = RunScenario(defs, Config.DefaultSeed);
        check(a == b, $"결정성: 같은 시나리오 두 번 → 같은 체크섬 ({a.Key})");
        // 2026-10-19 정적 팀: 기본 = 모두 적 → 4세력이 처음부터 싸워 5분 뒤 생존이 적다 (예전 기준 40 → 20)
        // 2026-10-19 ④ 분대 · 지휘 계통 지연 뒤 생존 20 (기준 20 → 10)
        // 2026-10-19 정리 묶음 2 (표적 흔들림 6 → 5 · 장교 선제 교전 없음 · 견인포 회전) 뒤 생존 8 (기준 10 → 5)
        check(a.Shots > 500 && a.Units >= 5,$"시나리오가 실제로 교전함 (사격 {a.Shots}, 유닛 {a.Units})");
        string? g = ReadGolden();
        check(g != null, "기준 체크섬 파일 존재 (assets/data/detcheck.golden — 없으면 `--detcheck --update`)");
        if (g != null) check(g == a.Key, $"기준 체크섬과 일치 ({a.Key}{(g == a.Key ? "" : $", 기준 {g} — 시뮬 로직을 일부러 바꿨다면 `--detcheck --update`")})");
    }
}
