using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T50d 반복 가능한 성능 벤치 (헤드리스, 창 없음): KoreanDivision --stress bench [scenes=ACE] [k=1000,5000,10000] [seeds=1,2,3] [repeats=1]
///   [ticks=600] [memevery=0] [cap=300] [out=perf.json] [label=name]
/// 장면 정의는 StressTest(A 대기 / C 전투 / E 난전 / B 이동 ...)와 같다 (Setup · Reorder 재사용, 시뮬 코드 무수정).
/// 실행(장면 x K x 시드 x 반복)마다: 틱 ms mean/p50/p95/p99/max, 구간별 ms/틱(World.Prof: FindTarget=[11], Fog=[8] 포함), 틱당 할당 KB,
/// 끝 체크섬(같은 장면/K/시드면 반복 · 빌드 사이에 같아야 함 = 측정이 시뮬을 안 바꿨다는 증거), memevery>0 이면 N틱마다 강제 GC 뒤 살아 있는 힙(MB).
/// 요약(summary)은 장면+K 별로 모든 실행의 중앙값. tools/perf_compare.ps1 이 이 요약을 비교한다.
/// 1시간 등가: scenes=B k=5000 seeds=1 ticks=216000 memevery=21600 cap=0 (cap=0 은 시간 상한 없음).
/// </summary>
public static partial class StressTest
{
    sealed class BenchRun
    {
        public string Scene = ""; public int K, Seed, Rep, Ticks, Alive;
        public double Mean, P50, P95, P99, Max, AllocKb, GcPauseMs;
        public double[] Phase = new double[16];
        public double[] Probe = new double[PerfProbe.Slots];
        public ulong Checksum;
        public List<(int Tick, double LiveMb, int Alive)> Mem = new();
    }

    static int Bench(string[] args)
    {
        string scenes = "ACE", outPath = "", label = "";
        int[] ks = { 1000, 5000, 10000 }, seeds = { 1, 2, 3 };
        int repeats = 1, ticks = 600, memEvery = 0, players = 16;
        double capSec = 300;
        foreach (var a in args)
        {
            int eq = a.IndexOf('='); if (eq < 0) continue;
            string key = a[..eq].ToLowerInvariant(), val = a[(eq + 1)..];
            switch (key)
            {
                case "scenes": scenes = val.ToUpperInvariant(); break;
                case "k": ks = val.Split(',').Select(int.Parse).ToArray(); break;
                case "seeds": seeds = val.Split(',').Select(int.Parse).ToArray(); break;
                case "repeats": repeats = int.Parse(val); break;
                case "ticks": ticks = int.Parse(val); break;
                case "memevery": memEvery = int.Parse(val); break;
                case "cap": capSec = double.Parse(val); break;
                case "players": players = int.Parse(val); break;
                case "out": outPath = val; break;
                case "label": label = val; break;
            }
        }
        var defs = Defs.Load();
        if (ProfArr == null) { Console.WriteLine("[bench] World.Prof 훅 없음 - 구간 측정 불가"); return 2; }
        Console.WriteLine($"[bench] scenes={scenes} k={string.Join(',', ks)} seeds={string.Join(',', seeds)} repeats={repeats} ticks={ticks} memevery={memEvery} cores={Environment.ProcessorCount} serverGC={System.Runtime.GCSettings.IsServerGC}");
        BenchOne(defs, 'C', 1000, players, 1, 0, 150, 30, 0);   // JIT 워밍업 (결과 버림): 첫 실행만 2~3배 느린 것을 막는다
        var runs = new List<BenchRun>();
        foreach (char sc in scenes)
            foreach (int k in ks)
                foreach (int seed in seeds)
                    for (int rep = 0; rep < repeats; rep++)
                    {
                        var r = BenchOne(defs, sc, k, players, seed, rep, ticks, capSec, memEvery);
                        runs.Add(r);
                        Console.WriteLine($"[bench] {sc}{k} seed={seed} rep={rep} ticks={r.Ticks} alive={r.Alive} mean={r.Mean:0.000} p95={r.P95:0.000} p99={r.P99:0.000} max={r.Max:0.00} findTarget={r.Phase[11]:0.000} fog={r.Phase[8]:0.000} sep={r.Phase[4]:0.000} alloc={r.AllocKb:0.0}KB cs={r.Checksum:x16}");
                        foreach (var (t, mb, al) in r.Mem) Console.WriteLine($"[mem] {sc}{k} seed={seed} t={t} live={mb:0.0}MB alive={al}");
                    }
        // 같은 (장면,K,시드) 반복의 체크섬이 같은지 (틱 수가 같을 때)
        int csBad = 0;
        foreach (var g in runs.GroupBy(r => (r.Scene, r.K, r.Seed)))
            if (g.Select(x => x.Checksum).Distinct().Count() > 1 && g.Select(x => x.Ticks).Distinct().Count() == 1) { csBad++; Console.WriteLine($"[bench] CHECKSUM MISMATCH between repeats: {g.Key}"); }

        var summary = new List<Dictionary<string, object>>();
        foreach (var g in runs.GroupBy(r => (r.Scene, r.K)))
        {
            var l = g.ToList();
            double Med(Func<BenchRun, double> f) { var v = l.Select(f).OrderBy(x => x).ToArray(); return Math.Round(v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2, 4); }
            summary.Add(new()
            {
                ["key"] = $"{g.Key.Scene}{g.Key.K}", ["scene"] = g.Key.Scene, ["k"] = g.Key.K, ["runs"] = l.Count,
                ["meanMs"] = Med(r => r.Mean), ["p50Ms"] = Med(r => r.P50), ["p95Ms"] = Med(r => r.P95), ["p99Ms"] = Med(r => r.P99), ["maxMs"] = Med(r => r.Max),
                ["findTargetMs"] = Med(r => r.Phase[11]), ["fogMs"] = Med(r => r.Phase[8]), ["separateMs"] = Med(r => r.Phase[4]),
                ["allocKbPerTick"] = Med(r => r.AllocKb),
            });
        }
        var doc = new Dictionary<string, object>
        {
            ["format"] = 1, ["label"] = label, ["commit"] = GitHead(), ["date"] = DateTime.Now.ToString("s"),
            ["machine"] = $"{Environment.MachineName}/{Environment.ProcessorCount}c", ["ticks"] = ticks, ["players"] = players,
            ["checksumRepeatMismatch"] = csBad,
            ["summary"] = summary,
            ["runs"] = runs.Select(r => new Dictionary<string, object>
            {
                ["scene"] = r.Scene, ["k"] = r.K, ["seed"] = r.Seed, ["rep"] = r.Rep, ["ticks"] = r.Ticks, ["alive"] = r.Alive,
                ["checksum"] = r.Checksum.ToString("x16"), ["meanMs"] = Math.Round(r.Mean, 4), ["p50Ms"] = Math.Round(r.P50, 4), ["p95Ms"] = Math.Round(r.P95, 4),
                ["p99Ms"] = Math.Round(r.P99, 4), ["maxMs"] = Math.Round(r.Max, 3), ["allocKbPerTick"] = Math.Round(r.AllocKb, 2), ["gcPauseMs"] = Math.Round(r.GcPauseMs, 1),
                ["phaseMs"] = PhaseNames.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => Math.Round(r.Phase[x.i], 4)),
                ["findTargetMs"] = Math.Round(r.Phase[11], 4), ["fogMs"] = Math.Round(r.Phase[8], 4),
                ["probe"] = Enumerable.Range(0, PerfProbe.Slots).Where(i => PerfProbe.Names[i] != "" && r.Probe[i] != 0).ToDictionary(i => PerfProbe.Names[i], i => Math.Round(r.Probe[i], 4)),
                ["liveHeapMb"] = r.Mem.Select(m => new Dictionary<string, object> { ["tick"] = m.Tick, ["mb"] = Math.Round(m.LiveMb, 2), ["alive"] = m.Alive }).ToList(),
            }).ToList(),
        };
        if (outPath != "")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            File.WriteAllText(outPath, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            Console.WriteLine($"[bench] wrote {outPath}");
        }
        Console.WriteLine("[bench] key      mean    p95     p99     findTgt fog     sep");
        foreach (var s in summary) Console.WriteLine($"[bench] {s["key"],-8} {s["meanMs"],-7} {s["p95Ms"],-7} {s["p99Ms"],-7} {s["findTargetMs"],-7} {s["fogMs"],-7} {s["separateMs"]}");
        return csBad == 0 ? 0 : 3;
    }

    static string GitHead()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            string s = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(3000); return s;
        }
        catch { return ""; }
    }

    /// <summary>RunOne 과 같은 구성 (맵 크기 · 시드 · 워밍업), 단 틱별 시간을 모두 모으고 체크섬 · 강제 GC 힙 표본을 낸다.</summary>
    static BenchRun BenchOne(Defs defs, char scenario, int k, int players, int seed, int rep, int ticks, double capSec, int memEvery)
    {
        int size = Math.Clamp(Math.Max(Config.MapSizeFor(players), (int)(MathF.Sqrt(k) * 2.4f) / 8 * 8), Config.MinMapSize, Config.MaxMapSize);
        var map = GameMap.Generate(size, size, seed, players);
        var world = World.CreateStandard(map, defs);
        StripStartForce(world);
        var rng = new Random(seed ^ k);
        var squads = Setup(world, defs, scenario, k, players, rng);
        if (scenario == 'D') for (int f = 1; f <= players; f++) world.Controllers.Add((f, AiRegistry.Create("standard")!));
        world.Fog.Update();
        for (int i = 0; i < 60; i++) world.Step();
        var prof = (long[])ProfArr!.GetValue(null)!;
        Array.Clear(prof); ProfFlag!.SetValue(null, true);
        PerfProbe.Reset(); PerfProbe.On = true;
        GC.Collect(); GC.WaitForPendingFinalizers();

        var ms = new List<double>(ticks);
        var r = new BenchRun { Scene = scenario.ToString(), K = k, Seed = seed, Rep = rep };
        long gc0 = (long)GC.GetTotalPauseDuration().TotalMilliseconds, alloc0 = GC.GetAllocatedBytesForCurrentThread();
        var wall = Stopwatch.StartNew();
        for (int t = 0; t < ticks; t++)
        {
            Reorder(world, scenario, squads, rng, t);
            long t0 = Stopwatch.GetTimestamp();
            world.Step();
            ms.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            if (memEvery > 0 && t % memEvery == 0) r.Mem.Add((t, GC.GetTotalMemory(true) / 1048576.0, world.Units.Count));   // 강제 GC 뒤 live (틱 시간 밖)
            if (capSec > 0 && wall.Elapsed.TotalSeconds > capSec && t >= 60) break;
        }
        ProfFlag.SetValue(null, false); PerfProbe.On = false;
        int n = Math.Max(1, ms.Count);
        r.Ticks = ms.Count; r.Alive = world.Units.Count;
        r.AllocKb = (GC.GetAllocatedBytesForCurrentThread() - alloc0) / 1024.0 / n;
        r.GcPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds - gc0;
        var sorted = ms.OrderBy(x => x).ToArray();
        r.Mean = ms.Average(); r.P50 = sorted[sorted.Length / 2];
        r.P95 = sorted[(int)(sorted.Length * 0.95)]; r.P99 = sorted[(int)(sorted.Length * 0.99)]; r.Max = sorted[^1];
        double toMs = 1000.0 / Stopwatch.Frequency / n;
        for (int i = 0; i < 16; i++) r.Phase[i] = prof[i] * toMs;
        r.Phase[14] = prof[14]; r.Phase[15] = prof[15];   // 경로 · 흐름장 횟수 (ms 아님)
        for (int i = 0; i < PerfProbe.Slots; i++) r.Probe[i] = PerfProbe.V[i] * (i >= 2 && i != 4 ? toMs : 1.0 / n);
        Array.Clear(prof);
        r.Checksum = world.ComputeChecksum();
        GC.KeepAlive(world);
        return r;
    }
}
