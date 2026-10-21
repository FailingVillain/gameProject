using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 유닛 수 스트레스 테스트 (창 없이): 유닛 K 기를 만들어 World.Step 을 돌리고 틱당 시간(avg / p95 / p99 / max)과 메모리를 잰다.
///   KoreanDivision --stress [A|B|C|D|E|F ...] [k=1000,2000,5000] [ticks=900] [cap=90] [players=10] [map=N] [seed=N] [stop=1000] [weather=fog,night]
/// 시나리오:
///   A 대기   : 유닛이 맵에 흩어져 서 있음 (중립 · 전투 없음, 안개 켬)
///   B 이동   : 세력당 5개 분대가 흐름장으로 맵을 가로지르고 10초마다 새 목표를 받음 (중립)
///   C 전투   : 두 큰 덩어리(세력 1·2)가 서로 공격 이동으로 충돌 (그 외 세력은 사령부만)
///   D 혼전   : 세력 전원 전쟁, 세력당 K/세력수 기, 표준 AI 가동 + 사령부 앞에서 중앙으로 공격 이동 (AI 가 만든 병력이 아니라 생성한 병력)
///   F 도시전 : 모든 도시 둘레에서 두 세력씩 마주 보고 도시 중심으로 공격 이동 (도시 위 충돌 반경 2배 · 통행)
///   E 난전   : 전원 전쟁, 모든 세력이 같은 곳에 섞여 시작 (최악의 표적 탐색 밀도)
///   ---- v0.4 (ROADMAP §3 · §5 · §9, 기본 묶음 ABCDEF 에는 없음 — 글자로 지정)
///   G 은폐탐지: A 처럼 흩어진 평화 상태 + 모든 유닛 은폐 30 → 세력 16개가 1초마다 탐지 판정 (K = 10000,16000 권장)
///   H 경계확산: 전원 전쟁, 세력마다 사령부 앞 덩어리가 중앙으로 공격 이동 (AI 없음) — 경계 지도 표시 · 2단계 안개 · 끝에 경계 구역 수 보고
///   I 포병50문: 세력 1 곡사포 50문이 세력 2 보병 K 기 덩어리를 일제 포격 (6초마다 50발) — 범위 피해 · 착탄 비용
///   R 재보급러시: 대전투 직후 — 세력마다 사령부 앞 22~34칸(보급 범위 밖)에 K/세력수 기가 탄약 0 으로 서 있고 같은 틱에 전원 재보급 여정 출발 →
///              가장 가까운 보급소/대도시로 이동 · 충전 · 복귀 (§1 부하 목표: 1만 기에서 보급 처리 ≤ 0.1 ms/틱; K = 5000,10000,16000 권장)
///   J 중대행군: 세력 1 의 100기 덩어리 K/100 개가 10초마다 횡대로 새 목표 (편제 · 깃발 대형 경로 비용 — K = 2000,8000 권장)
///   개발용: --stress combat2test (Combat2SelfTest 만), --stress navtest
/// 생성은 디버그 생성 명령과 같은 World.SpawnUnit. 구간별 시간은 World.Prof 측정 훅이 있으면(리플렉션) 같이 보여준다.
/// </summary>
public static partial class StressTest
{
    sealed class Result
    {
        public string Scenario = "";
        public int K, Alive, Ticks, MapSize;
        public double Avg, P50, P95, P99, Max, WorstSec, HeapMb, PeakWsMb, AllocKbPerTick, GcPauseMs;
        public double[]? Phase;      // 구간별 틱당 평균 ms (Prof 훅이 있을 때)
        public long Grids, Fields;   // 경로 · 지역 흐름장을 새로 계산한 횟수
        public (int Routes, int Fields, int Pending, int Pooled, int RoutesComputed, int FieldsComputed) Nav;
        public double SpawnSec;
        public double DetectMs, SplashMs;   // v0.4: 은폐 탐지 · 포병 범위 피해 (틱당 ms)
        public long Shells; public int AlertCells;
        public double SupplyMs; public int Trips, Refilled;   // v0.4 B: 보급 처리(틱당 ms) · 시작된 재보급 여정 · 끝에 탄약 가득한 유닛 수
    }

    // Prof 훅 (없으면 null). World.Profile / World.Prof — 리플렉션이라 훅 없는 빌드에서도 컴파일된다.
    static readonly FieldInfo? ProfFlag = typeof(World).GetField("Profile", BindingFlags.Public | BindingFlags.Static);
    static readonly FieldInfo? ProfArr = typeof(World).GetField("Prof", BindingFlags.Public | BindingFlags.Static);
    static readonly string[] PhaseNames = { "AI+cmd+nav", "hash1", "UpdateUnit", "hash2", "Separate", "Terrain", "Bldg+Econ", "RemoveDead", "Fog", "Victory+prune", "Checksum" };
    // 구간 안에 포함된 부분 (별도 표시): 11 = FindTarget, 12 = 경로(HPA*) 계산, 13 = 지역 흐름장 계산, 14 = 경로 수, 15 = 흐름장 수

    static (WeatherKind, TimeOfDay)? weatherArg;

    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        if (args.Length > 0 && args[0] == "combat2test")   // 개발용: v0.4 전투 검증만 빠르게
        {
            Defs.Load();
            var o = new GameOptions { MapWidth = 100, MapHeight = 100, Players = 2 };
            int fails = 0;
            Combat2SelfTest.Run(o, (ok, name) => { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name}"); if (!ok) fails++; });
            Console.WriteLine(fails == 0 ? "COMBAT2 PASS" : $"COMBAT2 FAIL ({fails})");
            return fails;
        }
        if (args.Length > 0 && args[0] == "navtest")   // 개발용: 길찾기 검증만 빠르게
        {
            Defs.Load();
            var o = new GameOptions { MapWidth = 100, MapHeight = 100, Players = 2 };
            int fails = 0;
            NavSelfTest.Run(o, (ok, name) => { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name}"); if (!ok) fails++; });
            return fails;
        }
        string scen = "ABCDEF";
        int[] ks = { 1000, 2000, 5000, 10000, 20000, 50000 };
        int ticks = 900, players = 16, seed = Config.DefaultSeed, mapSize = 0;
        double capSec = 90, stopMs = 1000;
        foreach (var a in args)
        {
            int eq = a.IndexOf('=');
            if (eq < 0) { scen = a.ToUpperInvariant(); continue; }
            string key = a[..eq].ToLowerInvariant(), val = a[(eq + 1)..];
            switch (key)
            {
                case "k": ks = val.Split(',').Select(int.Parse).ToArray(); break;
                case "ticks": ticks = int.Parse(val); break;
                case "cap": capSec = double.Parse(val); break;
                case "players": players = int.Parse(val); break;
                case "map": mapSize = int.Parse(val); break;
                case "seed": seed = int.Parse(val); break;
                case "stop": stopMs = double.Parse(val); break;
                case "weather":   // weather=fog,night (날씨 · 시간대 — 시야 계산 비용 비교)
                    var wp = val.Split(',');
                    weatherArg = (WeatherState.ParseKind(wp[0]), wp.Length > 1 ? WeatherState.ParseTime(wp[1]) : TimeOfDay.Day);
                    break;
            }
        }
        var defs = Defs.Load();
        if (scen == "AI") return AiStress(defs, args, players, ticks == 900 ? 450 : ticks, seed);
        Console.WriteLine($"[stress] {Environment.ProcessorCount} 코어, {(ProfArr != null ? "구간 측정 훅 있음" : "구간 측정 훅 없음")}, 서버GC={System.Runtime.GCSettings.IsServerGC}, 시나리오 {scen}, 인원 {players}, 틱 {ticks} (+워밍업 60), 상한 {capSec}초");
        var all = new List<Result>();
        foreach (char s in scen)
        {
            foreach (int k in ks)
            {
                var r = RunOneV04(defs, s, k, players, mapSize, seed, ticks, capSec);
                all.Add(r);
                Print(r);
                if (r.Avg > stopMs) { Console.WriteLine($"  -> {s}: 평균 {r.Avg:0} ms > {stopMs:0} ms, 더 큰 K 는 건너뜀"); break; }
            }
        }
        Console.WriteLine();
        PrintTable(all);
        return 0;
    }

    // ---------------------------------------------------------------- 한 번 실행

    /// <summary>v0.4 장면 G 는 밸런스 정의의 은폐 계수를 잠시 30 으로 올려 돌린다 (끝나면 복구).</summary>
    static Result RunOneV04(Defs defs, char scenario, int k, int players, int mapSize, int seed, int ticks, double capSec)
    {
        if (scenario != 'G') return RunOne(defs, scenario, k, players, mapSize, seed, ticks, capSec);
        var saved = defs.Units.Values.Select(u => (u, u.Concealment)).ToList();
        try
        {
            foreach (var u in defs.Units.Values) u.Concealment = 30;
            return RunOne(defs, scenario, k, players, mapSize, seed, ticks, capSec);
        }
        finally { foreach (var (u, c) in saved) u.Concealment = c; }
    }

    static Result RunOne(Defs defs, char scenario, int k, int players, int mapSize, int seed, int ticks, double capSec)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long heapBefore = GC.GetTotalMemory(true);
        // 기본 맵 크기: 인원 기준. 유닛이 많으면 밀도가 너무 높지 않게 키운다 (최대 MaxMapSize)
        int size = mapSize > 0 ? mapSize : Math.Clamp(Math.Max(Config.MapSizeFor(players), (int)(MathF.Sqrt(k) * 2.4f) / 8 * 8), Config.MinMapSize, Config.MaxMapSize);
        var map = GameMap.Generate(size, size, seed, players);
        var world = World.CreateStandard(map, defs);
        if (weatherArg is var (wk, wt)) world.SetupWeather(wk, wt);
        StripStartForce(world);
        var rng = new Random(seed ^ k);
        var sw = Stopwatch.StartNew();
        var squads = Setup(world, defs, scenario, k, players, rng);
        double spawnSec = sw.Elapsed.TotalSeconds;
        if (scenario == 'D')
            for (int f = 1; f <= players; f++) world.Controllers.Add((f, AiRegistry.Create("standard")!));
        world.Fog.Update();

        // 워밍업 (JIT · 첫 명령 실행) — 통계에서 제외
        for (int i = 0; i < 60; i++) world.Step();
        if (ProfArr?.GetValue(null) is long[] pw) { Array.Clear(pw); ProfFlag?.SetValue(null, true); }
        World.ProfDetect = World.ProfSplash = 0;
        World.SupplyProf = 0;
        long shells = 0;

        var ms = new List<double>(ticks);
        long gcPause0 = (long)GC.GetTotalPauseDuration().TotalMilliseconds;
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        var proc = Process.GetCurrentProcess();
        long peakWs = 0;
        var wall = Stopwatch.StartNew();
        for (int t = 0; t < ticks; t++)
        {
            Reorder(world, scenario, squads, rng, t);
            long t0 = Stopwatch.GetTimestamp();
            world.Step();
            ms.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            if (scenario == 'I') foreach (var imp in world.Impacts) if (imp.Tick == world.Tick - 1) shells++;
            if (t % 120 == 0) { proc.Refresh(); peakWs = Math.Max(peakWs, proc.WorkingSet64); }
            if (wall.Elapsed.TotalSeconds > capSec && t >= 60) break;
        }
        proc.Refresh();
        peakWs = Math.Max(peakWs, proc.PeakWorkingSet64);
        var r = new Result
        {
            Scenario = scenario.ToString(), K = k, MapSize = size, Ticks = ms.Count, Alive = world.Units.Count, SpawnSec = spawnSec,
            AllocKbPerTick = (GC.GetAllocatedBytesForCurrentThread() - alloc0) / 1024.0 / Math.Max(1, ms.Count),
            GcPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds - gcPause0,
            PeakWsMb = peakWs / 1048576.0,
        };
        var sorted = ms.OrderBy(x => x).ToArray();
        r.Avg = ms.Average();
        r.P50 = sorted[sorted.Length / 2];
        r.P95 = sorted[(int)(sorted.Length * 0.95)];
        r.P99 = sorted[(int)(sorted.Length * 0.99)];
        r.Max = sorted[^1];
        // 가장 느린 1초(60틱) 구간 평균 — 지속적으로 느린 순간
        double win = 0, worst = 0;
        int span = Math.Min(60, ms.Count);
        for (int i = 0; i < ms.Count; i++)
        {
            win += ms[i];
            if (i >= span) win -= ms[i - span];
            if (i >= span - 1) worst = Math.Max(worst, win / span);
        }
        r.WorstSec = worst;
        r.HeapMb = (GC.GetTotalMemory(false) - heapBefore) / 1048576.0;
        double toMsAll = 1000.0 / Stopwatch.Frequency / Math.Max(1, ms.Count);
        r.DetectMs = World.ProfDetect * toMsAll; r.SplashMs = World.ProfSplash * toMsAll; r.Shells = shells;
        r.SupplyMs = World.SupplyProf * toMsAll;
        foreach (var u in world.Units) if (u.Def.IsSupplyWagon && u.Cargo < u.Def.Cargo) r.Trips++;   // (방향 변경) 적재량을 쓴 보급 마차 수
        foreach (var u in world.Units) if (u.UsesAmmo && u.Ammo == u.AmmoMax) r.Refilled++;
        for (int f = 1; f < world.Factions.Count; f++) r.AlertCells += world.Alert.CountAlerted(f, world.Tick, World.AlertTicks);
        if (ProfArr?.GetValue(null) is long[] p)
        {
            ProfFlag?.SetValue(null, false);
            double toMs = 1000.0 / Stopwatch.Frequency / Math.Max(1, ms.Count);
            r.Phase = p.Select(x => x * toMs).ToArray();
            r.Grids = p[14]; r.Fields = p[15];
            Array.Clear(p);
        }
        r.Nav = world.NavStats;
        GC.KeepAlive(world);
        return r;
    }

    // ---------------------------------------------------------------- 시나리오 구성

    static readonly string[] Mix = { "infantry", "infantry", "infantry", "infantry", "infantry", "infantry", "infantry", "infantry", "infantry", "infantry",
                                     "infantry", "infantry", "infantry", "infantry", "infantry", "scout", "tank_ft_male", "tank_ft_male", "tank_ft_male", "tank_ft_male" };   // 보병 75% · 정찰 5% · 전차 20%

    static string KindOf(int i) => Mix[i % Mix.Length];

    /// <summary>덩어리(원형, 유닛당 0.7 타일² 밀도)로 n 기 생성.</summary>
    static List<Unit> SpawnBlob(World w, Defs defs, int faction, Vector2 center, int n, int kindOffset = 0)
    {
        var list = new List<Unit>(n);
        for (int i = 0; i < n; i++)
        {
            float r = MathF.Sqrt(i * 0.7f / MathF.PI), a = i * 2.3999632f;
            var pos = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            pos = new Vector2(Math.Clamp(pos.X, 1, w.Map.Width - 1), Math.Clamp(pos.Y, 1, w.Map.Height - 1));
            list.Add(w.SpawnUnit(defs.Units[KindOf(i + kindOffset)], faction, pos));
        }
        return list;
    }

    static Vector2 RandomWalkable(World w, Random rng)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            int x = rng.Next(2, w.Map.Width - 2), y = rng.Next(2, w.Map.Height - 2);
            if (w.Map.IsWalkable(x, y)) return new Vector2(x + 0.5f, y + 0.5f);
        }
        return new Vector2(w.Map.Width / 2f, w.Map.Height / 2f);
    }

    /// <summary>시나리오별 초기 배치. 돌려주는 분대 목록은 Reorder 가 사용 (세력, 유닛들).</summary>
    static List<(int Faction, List<Unit> Units)> Setup(World w, Defs defs, char sc, int k, int players, Random rng)
    {
        var squads = new List<(int, List<Unit>)>();
        var center = new Vector2(w.Map.Width / 2f, w.Map.Height / 2f);
        switch (sc)
        {
            case 'A':   // 맵 곳곳에 흩어짐 (세력 번갈아)
                for (int i = 0; i < k; i++)
                    w.SpawnUnit(defs.Units[KindOf(i)], i % players + 1, RandomWalkable(w, rng));
                break;
            case 'B':   // 세력당 5개 분대 덩어리
            {
                int per = Math.Max(1, k / (players * 5));
                for (int f = 1; f <= players; f++)
                    for (int s = 0; s < 5; s++)
                        squads.Add((f, SpawnBlob(w, defs, f, RandomWalkable(w, rng), per, s)));
                break;
            }
            case 'C':   // 두 덩어리 정면 충돌
            {
                int per = k / 2;
                float rad = MathF.Sqrt(per * 0.7f / MathF.PI);
                float d = Math.Min(w.Map.Width / 2f - 6, rad + 25);
                var a = SpawnBlob(w, defs, 1, center + new Vector2(-d, 0), per);
                var b = SpawnBlob(w, defs, 2, center + new Vector2(d, 0), k - per);
                w.AttackMove(1, a, center + new Vector2(d, 0));
                w.AttackMove(2, b, center + new Vector2(-d, 0));
                break;
            }
            case 'D':   // 세력당 K/인원, 사령부 앞 덩어리에서 시작 → 중앙으로 공격 이동 (AI 도 가동)
            {
                int per = k / players;
                for (int f = 1; f <= players; f++)
                {
                    var hq = w.Buildings.First(x => x.Owner == f && x.Def.Headquarters).Pos;
                    var toward = Vector2.Normalize(center - hq);
                    var list = SpawnBlob(w, defs, f, hq + toward * (8 + MathF.Sqrt(per * 0.7f / MathF.PI)), per);
                    squads.Add((f, list));
                    w.AttackMove(f, list, center);
                }
                break;
            }
            case 'F':   // 도시 안팎 전투: 도시(대도시 · 소도시)마다 두 세력이 마주 보고 도시 중심으로 공격 이동
            {
                var cities = w.Buildings.Where(b => b.Def.City).ToList();
                int per = Math.Max(2, k / (cities.Count * 2));
                int ci = 0;
                foreach (var city in cities)
                {
                    int fa = ci % players + 1, fb = (ci + 1) % players + 1;
                    float rad = MathF.Sqrt(per * 0.7f / MathF.PI);
                    var a = SpawnBlob(w, defs, fa, city.Pos + new Vector2(-city.Size * 0.5f - rad, 0), per, ci);
                    var b = SpawnBlob(w, defs, fb, city.Pos + new Vector2(city.Size * 0.5f + rad, 0), per, ci + 1);
                    w.AttackMove(fa, a, city.Pos);
                    w.AttackMove(fb, b, city.Pos);
                    squads.Add((fa, a)); squads.Add((fb, b));
                    ci++;
                }
                break;
            }
            case 'G':   // v0.4 은폐 탐지: A 처럼 흩어짐 (평화) — 은폐 계수는 RunOneV04 가 올린다
                for (int i = 0; i < k; i++)
                    w.SpawnUnit(defs.Units[KindOf(i)], i % players + 1, RandomWalkable(w, rng));
                break;
            case 'H':   // v0.4 경계 확산: D 와 같은 배치, AI 없음
            {
                int per = k / players;
                for (int f = 1; f <= players; f++)
                {
                    var hq = w.Buildings.First(x => x.Owner == f && x.Def.Headquarters).Pos;
                    var toward = Vector2.Normalize(center - hq);
                    var list = SpawnBlob(w, defs, f, hq + toward * (8 + MathF.Sqrt(per * 0.7f / MathF.PI)), per);
                    squads.Add((f, list));
                    w.AttackMove(f, list, center);
                }
                break;
            }
            case 'I':   // v0.4 포병 일제 사격: 세력 1 곡사포 50문 (사수) ← 덩어리 가장자리에서 14~20칸 → 세력 2 보병 K 기 덩어리 (사수). 안개 끔 (관측 문제가 아니라 범위 피해 비용 측정)
            {
                w.Fog.Enabled = false;
                var art = defs.Units["heavy_howitzer"];
                float blobR = MathF.Sqrt(k * 0.7f / MathF.PI);
                var guns = new List<Unit>();
                for (int i = 0; i < 50; i++)
                    guns.Add(w.SpawnUnit(art, 1, center + new Vector2(-blobR - 14 - (i / 10) * 1.5f, (i % 10) * 1.4f - 6.3f)));
                w.Hold(1, guns);
                var inf = new List<Unit>();
                for (int i = 0; i < k; i++)
                {
                    float rr = MathF.Sqrt(i * 0.7f / MathF.PI), a = i * 2.3999632f;
                    inf.Add(w.SpawnUnit(defs.Units["infantry"], 2, center + new Vector2(MathF.Cos(a) * rr, MathF.Sin(a) * rr)));
                }
                w.Hold(2, inf);
                squads.Add((1, guns));
                break;
            }
            case 'J':   // 중대 행군 (부대 편제): 세력 1 보병 100기 덩어리 K/100 개, 10초마다 횡대 대형으로 새 목표 (경로 비용: 유닛별 vs 중대 깃발 1개)
            {
                int per = 100;
                for (int i = 0; i < Math.Max(1, k / per); i++)
                {
                    var list = SpawnBlob(w, defs, 1, RandomWalkable(w, rng), per, 0);
                    squads.Add((1, list));
                    FormCompanyForStress(w, list);
                }
                break;
            }
            case 'E':   // 전원 섞여 한 곳 — 세력마다 같은 중심 근처에서 생성 (최악 밀도)
            {
                int per = k / players;
                for (int f = 1; f <= players; f++)
                {
                    var list = SpawnBlob(w, defs, f, center + new Vector2((f % 3 - 1) * 4, (f % 4 - 1.5f) * 4), per, f);
                    squads.Add((f, list));
                    w.AttackMove(f, list, center);
                }
                break;
            }
            // ================================================================ v0.4 B (보급 · 연료, Supply.cs)
            case 'R':   // 탄약 회복 러시 (방향 변경 §0-1): 세력마다 사령부 앞 덩어리 전원 탄약 0 + 보급 마차 4대 → 매 간격 회복 · 마차 가속
            {
                int per = k / players;
                for (int f = 1; f <= players; f++)
                {
                    var hq = w.Buildings.First(x => x.Owner == f && x.Def.Headquarters).Pos;
                    var toward = Vector2.Normalize(center - hq);
                    float rad = MathF.Sqrt(per * 0.7f / MathF.PI);
                    var blobC = hq + toward * (30 + rad);
                    var list = SpawnBlob(w, defs, f, blobC, per);
                    foreach (var u in list) if (u.UsesAmmo) u.Ammo = 0;
                    if (defs.Units.TryGetValue("supply_wagon", out var wd))
                        for (int i = 0; i < 4; i++) list.Add(w.SpawnUnit(wd, f, blobC + new Vector2(i % 2 - 0.5f, i / 2 - 0.5f) * rad));
                    squads.Add((f, list));
                }
                break;
            }
        }
        return squads;
    }

    /// <summary>J: 100기 덩어리를 중대로 편성 (편제 시스템이 있을 때만 — 구현은 StressTestOrg.cs).</summary>
    static partial void FormCompanyForStress(World w, List<Unit> list);

    /// <summary>진행 중 명령: B = 10초마다 새 목표 (분대별), D = 20초마다 무작위 지점으로 다시 공격 이동.</summary>
    static void Reorder(World w, char sc, List<(int Faction, List<Unit> Units)> squads, Random rng, int t)
    {
        if (sc == 'J' && t % 600 == 0)
        {
            foreach (var (f, list) in squads)
            {
                list.RemoveAll(u => !u.Alive);
                w.Move(f, list, RandomWalkable(w, rng), new FormSpec((int)FormType.Line, 0, Vector2.Zero));
            }
        }
        else if (sc == 'B' && t % 600 == 0)
        {
            foreach (var (f, list) in squads)
            {
                list.RemoveAll(u => !u.Alive);
                w.Move(f, list, RandomWalkable(w, rng));
            }
        }
        else if (sc == 'I')
        {
            foreach (var u in w.Units) if (u.Owner == 2 && u.Alive) u.Hp = 100000;   // 표적이 한 일제 사격(50발)에도 안 죽어야 같은 표적에 6초마다 계속 쏜다 (표적이 바뀌면 조준 5초를 다시 한다)
        }
        else if (sc == 'D' && t > 0 && t % 1200 == 0)
        {
            foreach (var (f, list) in squads)
            {
                list.RemoveAll(u => !u.Alive);
                if (list.Count > 0) w.AttackMove(f, list, RandomWalkable(w, rng));
            }
        }
    }

    // ---------------------------------------------------------------- AI 16 x 1000 (H1: AI 사고 비용)
    //   KoreanDivision --stress ai [players=16] [n=1000] [ticks=450] [ai=standard|tactical]   세력당 n 기를 사령부 앞에 생성, AI 전원 가동, AI 사고 시간만 따로 잰다.

    internal sealed class TimedAi : IAiController
    {
        readonly IAiController inner;
        public static IAiController Unwrap(IAiController ai) => ai is TimedAi t ? t.inner : ai;
        public static double TickMs;          // 이번 틱에 쓴 AI 시간 합
        public static long Calls;
        public static double TotalMs;
        public TimedAi(IAiController inner) => this.inner = inner;
        public string Name => inner.Name;
        public void Tick(World w, int faction)
        {
            long t0 = Stopwatch.GetTimestamp();
            inner.Tick(w, faction);
            double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            TickMs += ms; TotalMs += ms; Calls++;
        }
    }

    static int AiStress(Defs defs, string[] args, int players, int ticks, int seed)
    {
        int n = 1000;
        string aiName = "standard";   // ai=tactical (⑤ 전술 AI 사고 비용 비교)
        foreach (var a in args)
        {
            if (a.StartsWith("n=", StringComparison.OrdinalIgnoreCase)) n = int.Parse(a[2..]);
            if (a.StartsWith("ai=", StringComparison.OrdinalIgnoreCase)) aiName = a[3..].ToLowerInvariant();
        }
        int size = Math.Clamp(Math.Max(Config.MapSizeFor(players), (int)(MathF.Sqrt(n * players) * 2.4f) / 8 * 8), Config.MinMapSize, Config.MaxMapSize);
        var map = GameMap.Generate(size, size, seed, players);
        var world = World.CreateStandard(map, defs);
        if (weatherArg is var (wk, wt)) world.SetupWeather(wk, wt);
        StripStartForce(world);
        var center = new Vector2(size / 2f, size / 2f);
        for (int f = 1; f <= players; f++)
        {
            Building? hq = null;
            foreach (var b in world.Buildings) if (b.Owner == f && b.Def.Headquarters) { hq = b; break; }
            var home = hq?.Pos ?? center;
            var dir = center - home;
            dir = dir.LengthSquared() > 1e-3f ? Vector2.Normalize(dir) : new Vector2(1, 0);
            SpawnBlob(world, defs, f, home + dir * 14, n);
            world.Controllers.Add((f, new TimedAi(AiRegistry.Create(aiName) ?? throw new ArgumentException("알 수 없는 AI " + aiName))));
        }
        world.Fog.Update();
        for (int i = 0; i < 60; i++) world.Step();
        TimedAi.TickMs = 0; TimedAi.Calls = 0; TimedAi.TotalMs = 0;
        var think = new List<double>(ticks); var step = new List<double>(ticks);
        for (int t = 0; t < ticks; t++)
        {
            TimedAi.TickMs = 0;
            long t0 = Stopwatch.GetTimestamp();
            world.Step();
            double total = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            think.Add(TimedAi.TickMs); step.Add(total);
        }
        var s = think.OrderBy(x => x).ToArray();
        Console.WriteLine($"[AI {aiName} {players} x {n}] map={size}² 틱={ticks} 유닛={world.Units.Count} 건물={world.Buildings.Count} AI호출={TimedAi.Calls} (호출당 평균 {TimedAi.TotalMs / Math.Max(1, TimedAi.Calls):0.000} ms)");
        Console.WriteLine($"  AI 사고 ms/틱: avg {think.Average():0.000} p95 {s[(int)(s.Length * 0.95)]:0.000} max {s[^1]:0.000} | 전체 Step avg {step.Average():0.00} max {step.Max():0.0} ms | 체크섬 {world.ComputeChecksum():x16}");
        return 0;
    }

    // ---------------------------------------------------------------- 출력

    static void Print(Result r)
    {
        Console.WriteLine($"{r.Scenario} K={r.K,6} map={r.MapSize}² 틱={r.Ticks,4} 생존={r.Alive,6} | avg {r.Avg,7:0.00} p50 {r.P50,7:0.00} p95 {r.P95,7:0.00} p99 {r.P99,7:0.00} max {r.Max,8:0.0} ms | 최악1초 {r.WorstSec:0.00} | 힙+{r.HeapMb:0}MB 작업집합 {r.PeakWsMb:0}MB 할당 {r.AllocKbPerTick:0}KB/틱 GC정지 {r.GcPauseMs:0}ms (생성 {r.SpawnSec:0.0}s)");
        if (r.Phase != null)
        {
            var sb = new StringBuilder("    구간(ms/틱): ");
            for (int i = 0; i < PhaseNames.Length; i++) sb.Append($"{PhaseNames[i]} {r.Phase[i]:0.00}  ");
            sb.Append($"| 포함: FindTarget {r.Phase[11]:0.00}, 경로계산 {r.Phase[12]:0.00} ({r.Grids}회), 지역흐름장 {r.Phase[13]:0.00} ({r.Fields}회)");
            Console.WriteLine(sb.ToString());
        }
        if (r.Scenario is "G" or "H" or "I")
            Console.WriteLine($"    v0.4: 은폐 탐지 {r.DetectMs:0.000} ms/틱 · 포병 범위 피해 {r.SplashMs:0.000} ms/틱 · 포탄 {r.Shells}발 · 경계 구역(전 세력 합) {r.AlertCells}");
        if (r.Scenario == "R")
            Console.WriteLine($"    v0.4 보급: 보급 처리(범위 지도 · 충전 · 여정 · 연료) {r.SupplyMs:0.000} ms/틱 (목표 ≤ 0.1 @1만) · 여정 출발 {r.Trips}기 · 끝에 탄약 가득 {r.Refilled}/{r.Alive}");
    }

    static void PrintTable(List<Result> all)
    {
        Console.WriteLine("| 시나리오 | K | 맵 | 틱 | 생존 | avg ms | p95 ms | p99 ms | max ms | 최악1초 avg | 힙 MB | 작업집합 MB | 할당 KB/틱 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in all)
            Console.WriteLine($"| {r.Scenario} | {r.K} | {r.MapSize} | {r.Ticks} | {r.Alive} | {r.Avg:0.00} | {r.P95:0.00} | {r.P99:0.00} | {r.Max:0.0} | {r.WorstSec:0.00} | {r.HeapMb:0} | {r.PeakWsMb:0} | {r.AllocKbPerTick:0} |");
    }

    /// <summary>방향 변경 §0-1: 표준 월드의 시작 병력(start.force) 을 걷어낸다 — 시나리오가 직접 병력을 놓는다. 전멸 패배를 막으려고 비무장 보급 마차만 남긴다 (옛 시작 공병 자리).</summary>
    static void StripStartForce(World w)
    {
        foreach (var u in w.Units) if (u.Weapon != null) u.Hp = 0;
        w.Step();
    }
}
