using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 밸런스 자동 대전 도구 (창 없음). 밸런스 값은 바꾸지 않고 보고서만 만든다.
///   KoreanDivision --balance [all|matchups|aigames|quick] [--out 폴더] [--procs N] [--seeds N] [--budgets 500,2000,6000]
///                     [--arenas flat,terrain,city] [--ai-seeds N] [--ai-minutes M] [--units a,b,c]
///   KoreanDivision --balance report &lt;폴더&gt;      이미 돌린 결과(results_*.jsonl)로 보고서만 다시 만든다
/// 결과: reports/balance/&lt;timestamp&gt;/ report.md + CSV.
///
/// (a) 동일 비용 교전: 전투 유닛 모든 쌍 × 비용 예산 × 전장 × 시드 × 양쪽 스폰 위치 교환. 양쪽 모두 BattleAi 가 조작한다 (안개 켬).
///     비용 환산(자금 상당 가중치): 자금 1 · 강철 2 · 연료 2 · 보급품 2(공장이 자금 2 → 보급품 1) · 인력 4. 유지비 · 인구는 비용에 넣지 않는다.
/// (b) AI 전체 경기: corners4 4인 · 생성 2인 · 생성 10인, 시드별 승자 · 길이 · 생산 유닛 · 자원 재고 추이 · 부족 시간 · 불변식.
/// 병렬: 작업 목록(jobs.txt)을 만들고 같은 exe 를 --balance-worker 로 N개 띄워 interleave 로 나눠 처리한다 (월드는 static 상태를 써서 스레드가 아니라 프로세스).
/// </summary>
public static class BalanceTool
{
    // ------------------------------------------------------------------ 비용 환산

    static readonly Dictionary<string, double> Weights = new() { ["funds"] = 1, ["steel"] = 2, ["fuel"] = 2, ["supplies"] = 2, ["manpower"] = 4 };

    /// <summary>비용 환산 (방향 변경 §0-1: 자원 · 생산 없음) — 비교 기준으로 체력 / 10 을 쓴다.</summary>
    static double CostEq(Defs defs, UnitDef u) => u.Hp / 10.0;

    /// <summary>전투 유닛 (무기 있고 일꾼 아님), balance.json 순서.</summary>
    static List<string> CombatUnits(Defs defs) => defs.Units.Values.Where(u => u.Weapon != null).Select(u => u.Key).ToList();

    // ------------------------------------------------------------------ 결과 형식 (JSON 한 줄)

    public sealed class MatchResult
    {
        public string Kind { get; set; } = "match";
        public string X { get; set; } = "";
        public string Y { get; set; } = "";
        public string Arena { get; set; } = "";
        public int Budget { get; set; }
        public int Seed { get; set; }
        public bool Swap { get; set; }
        public int NX { get; set; }
        public int NY { get; set; }
        public double CostX0 { get; set; }
        public double CostY0 { get; set; }
        public double CostXEnd { get; set; }       // 살아남은 유닛 수 기준 비용
        public double CostYEnd { get; set; }
        public double HpCostXEnd { get; set; }     // 체력 비례 잔존 비용
        public double HpCostYEnd { get; set; }
        public int AliveX { get; set; }
        public int AliveY { get; set; }
        public int Winner { get; set; }            // 월드 세력 번호: 1 · 2 = 승자, -1 = 동시 전멸, 0 = 시간 초과
        public bool XIsLeft => !Swap;
        public double EndSec { get; set; }
        public double T50X { get; set; }           // X 가 비용의 50% 를 잃은 시각 (초, 0 = 안 잃음)
        public double T50Y { get; set; }
        public double Ms { get; set; }
        public string? Error { get; set; }
        // v0.4: 정찰병 관측자 변형 · 사격 통계 (교전 테스트 모드는 탄약을 실제로 소모하지 않으므로 '발수 x 탄약비용' 으로 환산)
        public bool Spot { get; set; }             // 포병 쪽에 정찰병 관측자를 붙인 변형
        public int SpotN { get; set; }
        public int ShotsX { get; set; }
        public int ShotsY { get; set; }
        public int DirectX { get; set; }           // 단일 표적 사격 수 (명중률 분모; 범위 포격은 제외)
        public int DirectY { get; set; }
        public int HitsX { get; set; }
        public int HitsY { get; set; }
        public double DistSumX { get; set; }       // 사격 거리 합 (평균 교전 거리 = 합 / 발수)
        public double DistSumY { get; set; }
        public double SustainX { get; set; }       // 보급품 소모 환산 비용 (발수 x ammoCost x 보급품 가중치)
        public double SustainY { get; set; }
        public int DryX { get; set; }              // 탄창(weapon.ammo) 이상을 쏜 유닛 수 = 재보급 없이는 탄약이 바닥났을 유닛
        public int DryY { get; set; }
        public bool XWins => Winner == (Swap ? 2 : 1);
        public bool YWins => Winner == (Swap ? 1 : 2);
    }

    public sealed class FactionGame
    {
        public int Faction { get; set; }
        public string Ai { get; set; } = "";
        public bool Defeated { get; set; }
        public Dictionary<string, int> Built { get; set; } = new();
        public Dictionary<string, int> Lost { get; set; } = new();
        public List<double[]> Series { get; set; } = new();
        // v0.4: 탄약 · 재보급 통계 (표본 = 60틱마다). AmmoApi=false 면 sim 이 아직 노출하지 않음 → 보고서 n/a
        public bool AmmoApi { get; set; }
        public long AmmoUnitSamples { get; set; }  // 탄약 쓰는 유닛 수의 표본 합
        public long AmmoZeroSamples { get; set; }  // 그 중 탄약 0 인 유닛 수의 표본 합
        public double PeakDryFrac { get; set; }    // 한 표본에서 탄약 0 유닛 비율 최대
        public long TripSamples { get; set; }      // 재보급 여정 중인 유닛 수의 표본 합
        public int TripsStarted { get; set; }      // 여정 시작(None -> 다른 상태) 관측 횟수 (60틱 표본이라 근사)
        public long FuelEmptySamples { get; set; }
        public long FuelUnitSamples { get; set; }
        public int ShotsAmmo { get; set; }         // 탄약 쓰는 무기 사격 수
        public double SuppliesShot { get; set; }   // 사격 수 x ammoCost (재보급 소모 보급품 추정)
    }

    public sealed class GameResult
    {
        public string Kind { get; set; } = "game";
        public string Scenario { get; set; } = "";
        public int Seed { get; set; }
        public int Players { get; set; }
        public int Winner { get; set; }
        public double EndMin { get; set; }
        public double LimitMin { get; set; }
        public double Ms { get; set; }
        public List<FactionGame> Factions { get; set; } = new();
        public List<string> Violations { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>Series 한 행의 열 (GameResult.Factions[].Series).</summary>
    public static readonly string[] SeriesColumns =
    {
        "t_sec", "funds", "steel", "fuel", "supplies", "manpower", "supplies_net_per_s", "pop_used", "pop_cap", "army", "workers",
        "barracks", "depots", "factories", "plants", "plants_on", "turrets", "sites", "short_mask", "defeated",
        "artillery", "ammo_units", "ammo_zero", "on_trip", "fuel_units", "fuel_zero",   // v0.4 (뒤에 덧붙임 — 위 인덱스 유지)
    };

    // ---- 아직 없을 수 있는 sim 기능은 리플렉션으로 읽는다 (없으면 보고서 n/a)
    static readonly System.Reflection.PropertyInfo? PAmmo = typeof(Unit).GetProperty("Ammo");
    static readonly System.Reflection.PropertyInfo? PTrip = typeof(Unit).GetProperty("SupplyState");
    static bool AmmoApi => PAmmo != null;
    static int AmmoOf(Unit u) => PAmmo?.GetValue(u) is int a ? a : -1;
    static int TripState(Unit u) => PTrip?.GetValue(u) is { } v ? Convert.ToInt32(v) : 0;
    static bool IsArty(string key) => key == "heavy_howitzer";   // 곡사포 (관측자 필요)
    static bool UsesAmmo(Unit u) => u.Weapon is { Ammo: > 0 };

    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    // ------------------------------------------------------------------ 진입점

    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        if (args[0] == "--balance-worker") return Worker(args);
        if (args.Length > 1 && args[1] == "sim") return Sim(args);
        if (args.Length > 1 && args[1] == "stuck") return StuckProbe(args);
        if (args.Length > 1 && args[1] == "std") return StdProbe(args);
        var o = Options.Parse(args.Skip(1).ToArray());
        var defs = Defs.Load();
        if (o.ReportOnly != null)
        {
            Report(defs, o.ReportOnly);
            return 0;
        }
        // --repeat N: 시드를 바꿔 가며 N번 연속 실행 (계속 테스트). 매번 새 폴더 + reports/balance/latest/ 에 최신 보고서 사본
        for (int rep = 0; rep < o.Repeat; rep++)
        {
            RunOnce(defs, o, rep == 0 ? o.Out : null);
            o.SeedBase += Math.Max(o.Seeds, o.AiSeeds);
        }
        return 0;
    }

    static void RunOnce(Defs defs, Options o, string? outDir)
    {
        string dir = outDir ?? Path.Combine(ProjectRoot(), "reports", "balance", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dir);
        var jobs = BuildJobs(defs, o);
        File.WriteAllLines(Path.Combine(dir, "jobs.txt"), jobs);
        Console.WriteLine($"밸런스 자동 대전: {jobs.Count}개 작업, 프로세스 {o.Procs}개, 출력 {dir}");
        var sw = Stopwatch.StartNew();
        var procs = new List<Process>();
        string exe = Environment.ProcessPath ?? "KoreanDivision";
        bool viaDotnet = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        for (int i = 0; i < o.Procs; i++)
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Directory.GetCurrentDirectory() };
            if (viaDotnet) psi.ArgumentList.Add(typeof(BalanceTool).Assembly.Location);
            foreach (var a in new[] { "--balance-worker", Path.Combine(dir, "jobs.txt"), i.ToString(), o.Procs.ToString(), Path.Combine(dir, $"results_{i}.jsonl"), o.AiMinutes.ToString(CultureInfo.InvariantCulture) })
                psi.ArgumentList.Add(a);
            File.WriteAllText(Path.Combine(dir, $"results_{i}.jsonl"), "");
            procs.Add(Process.Start(psi)!);
        }
        int lastDone = -1;
        while (procs.Any(p => !p.HasExited))
        {
            Thread.Sleep(2000);
            int done = Directory.GetFiles(dir, "results_*.jsonl").Sum(f => CountLines(f));
            if (done != lastDone) { Console.WriteLine($"  진행 {done}/{jobs.Count}  ({sw.Elapsed.TotalMinutes:0.0}분)"); lastDone = done; }
        }
        Console.WriteLine($"모든 작업 종료 ({sw.Elapsed.TotalMinutes:0.0}분). 보고서 작성 중...");
        Report(defs, dir);
        Console.WriteLine($"보고서: {Path.Combine(dir, "report.md")}");
        if (o.Out == null)
        {
            string latest = Path.Combine(ProjectRoot(), "reports", "balance", "latest");
            Directory.CreateDirectory(latest);
            foreach (var f in Directory.GetFiles(dir)) if (!Path.GetFileName(f).StartsWith("results_")) File.Copy(f, Path.Combine(latest, Path.GetFileName(f)), true);
        }
    }

    /// <summary>--balance sim "A구성" "B구성" [flat|terrain|city] [시드] [안개 0|1] [최대초]: 교전 한 판을 창 없이 돌려 10초마다 현황을 출력 (디버깅 · 속도 확인).</summary>
    static int Sim(string[] a)
    {
        var defs = Defs.Load();
        var setup = new BattleSetup { SideA = a[2], SideB = a[3], Map = a.Length > 4 ? a[4] : "flat", AiVsAi = true, Distance = int.TryParse(Environment.GetEnvironmentVariable("KD_SIM_DIST"), out int simDist) ? simDist : (a.Length > 4 && a[4] == "city") ? 70 : 60 };   // KD_SIM_DIST = 시작 거리
        int seed = a.Length > 5 ? int.Parse(a[5]) : 1;
        var w = setup.CreateWorld(defs, seed); KoreanDivision.Tests.MeasX.Init(w);
        w.Fog.Enabled = a.Length <= 6 || a[6] != "0";
        // 방향 변경 §0-1 템포 비교: KD_SIM_DEFEND=1 → B 는 AI 없이 제자리 사수 (A 가 공격), KD_SIM_TRENCH=1 → 거기에 B 보병이 처음부터 참호 안
        bool defend = Environment.GetEnvironmentVariable("KD_SIM_DEFEND") == "1", trench = Environment.GetEnvironmentVariable("KD_SIM_TRENCH") == "1";
        // KD_SIM_AI = 양쪽 AI (기본 tactical), KD_SIM_AI_B = B 만 다르게 (예: 옛 battle 과 맞대결)
        string aiA = Environment.GetEnvironmentVariable("KD_SIM_AI") ?? AiRegistry.Default;
        string aiB = Environment.GetEnvironmentVariable("KD_SIM_AI_B") ?? aiA;
        StressTest.TimedAi.TickMs = 0; StressTest.TimedAi.Calls = 0; StressTest.TimedAi.TotalMs = 0;
        double aiMax = 0, aiSum = 0, stepSum = 0; int aiTicks = 0;
        // AI 사고 비용 (교전 중): 컨트롤러를 시간 재는 껍데기로 감싼다 (StressTest.TimedAi — 상태 출력은 Inner 로)
        w.Controllers.Add((1, new StressTest.TimedAi(AiRegistry.Create(aiA) ?? throw new ArgumentException("알 수 없는 AI " + aiA))));
        Console.WriteLine($"  [sim] AI A={aiA} B={(defend ? "사수" : aiB)}");
        if (!defend) w.Controllers.Add((2, new StressTest.TimedAi(AiRegistry.Create(aiB) ?? throw new ArgumentException("알 수 없는 AI " + aiB))));
        else
        {
            var bs = w.Units.Where(u => u.Owner == 2).ToList();
            w.Hold(2, bs);
            w.FlushCommands();
            if (trench) foreach (var u in bs) if (u.Def.CanOccupyTrench) { var (tx, ty) = GameMap.TileOf(u.Pos); if (w.Map.Get(tx, ty) is TerrainType.Grass or TerrainType.Dirt or TerrainType.Sand or TerrainType.Crater) w.MutateTerrain(tx, ty, TerrainType.Trench); }   // 발밑 칸을 참호 지형으로 (건설 제거 뒤 — 엄폐 = 참호 지형)
            Console.WriteLine($"  [sim] B 사수{(trench ? " + 참호" : "")} ({bs.Count}기)");
        }
        int limit = (int)((a.Length > 7 ? double.Parse(a[7], CultureInfo.InvariantCulture) : 600) / Config.SimDt);
        var sw = Stopwatch.StartNew();
        long shots = 0, hits = 0; int start = w.Units.Count;   // 관찰: 직사 사격 · 명중 수 (Shots 는 10틱 뒤 지워지므로 틱마다 센다)
        // 관찰 (⑤ 전술 AI): 유닛 종류별 사격 · 근접 · 포격 · 방열 시간 · 손실
        var defOf = new Dictionary<int, (int Owner, string Key)>();
        foreach (var u in w.Units) defOf[u.Id] = (u.Owner, u.Def.Key);
        var obs = new SortedDictionary<string, long[]>(StringComparer.Ordinal);   // "세력:종류" → [시작, 사격, 근접, 포격, 방열 표본, 표본, 돌격 표본]
        long[] Obs(int owner, string key) { string k = owner + ":" + key; if (!obs.TryGetValue(k, out var v)) obs[k] = v = new long[7]; return v; }
        foreach (var u in w.Units) Obs(u.Owner, u.Def.Key)[0]++;
        // 템포 관찰 (2026-10-19 사거리 · 지도 개편): 첫 사격 시각, 세력별 50% 손실 시각, 직사 사격 거리 (전체 평균 · 첫 100발 평균)
        int startA = w.Units.Count(u => u.Owner == 1), startB = w.Units.Count(u => u.Owner == 2);
        double infFirst = -1, firstMelee = -1; int cavAtMelee = -1;
        double firstShot = -1, t50A = -1, t50B = -1, distSum = 0, dist100 = 0; long distN = 0;
        while (w.Tick < limit && w.Winner == 0)
        {
            StressTest.TimedAi.TickMs = 0;
            long st0 = Stopwatch.GetTimestamp();
            w.Step(); KoreanDivision.Tests.MeasX.Tick(w);
            stepSum += Stopwatch.GetElapsedTime(st0).TotalMilliseconds;
            aiSum += StressTest.TimedAi.TickMs; aiTicks++; if (StressTest.TimedAi.TickMs > aiMax) aiMax = StressTest.TimedAi.TickMs;
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1)
            {
                shots++; if (s.Hit) hits++; if (defOf.TryGetValue(s.ShooterId, out var od)) Obs(od.Owner, od.Key)[1]++;
                if (firstShot < 0) firstShot = w.Time;
                if (infFirst < 0 && defOf.TryGetValue(s.ShooterId, out var of1) && of1.Owner == 1) infFirst = w.Time;
                float sd = Vector2.Distance(s.From, s.To); distSum += sd; if (distN < 100) dist100 += sd; distN++;
            }
            if (w.Tick % 30 == 0)
            {
                if (t50A < 0 && w.Units.Count(u => u.Owner == 1) * 2 <= startA) t50A = w.Time;
                if (t50B < 0 && w.Units.Count(u => u.Owner == 2) * 2 <= startB) t50B = w.Time;
            }
            foreach (var m in w.MeleeStrikes) if (m.Tick == w.Tick - 1 && defOf.TryGetValue(m.AttackerId, out var om)) { Obs(om.Owner, om.Key)[2]++; if (firstMelee < 0 && om.Owner == 2) { firstMelee = w.Time; cavAtMelee = w.Units.Count(u => u.Owner == 2); } }
            foreach (var im in w.Impacts) if (im.Tick == w.Tick - 1 && defOf.TryGetValue(im.ShooterId, out var oi)) Obs(oi.Owner, oi.Key)[3]++;
            if (w.Tick % 60 == 0)
                foreach (var u in w.Units) { var v = Obs(u.Owner, u.Def.Key); v[5]++; if (u.Deployed) v[4]++; if (u.Charging) v[6]++; }
            if (Environment.GetEnvironmentVariable("BAL_DBG") == "1" && w.Tick % 60 == 0 && w.Tick < 900)
            {
                var a1 = w.Units.First(u => u.Owner == 1); var b1 = w.Units.OrderBy(u => u.Owner == 2 ? Vector2.DistanceSquared(u.Pos, a1.Pos) : 1e9f).First();
                Console.WriteLine($"   dbg t={w.Time:0} a1 {a1.Pos} order {a1.Order} target {a1.Target?.Id} nearestB {b1.Pos} d={Vector2.Distance(a1.Pos, b1.Pos):0.0} see={w.Fog.CanSee(1, b1)} hostile={w.IsHostile(1, 2)} sight={a1.Sight}");
            }
            if (Environment.GetEnvironmentVariable("KD_TAC_DBG") == "1" && w.Tick % 300 == 0)
                foreach (var (f, ai) in w.Controllers)
                    if (StressTest.TimedAi.Unwrap(ai) is TacticalAi ta && ta.StateOf(f) is { } ts)
                        Console.WriteLine($"   tac{f} t={w.Time:0} " + TacticalAi.Describe(ts));
            if (w.Tick % 600 == 0)
            {
                int n1 = w.Units.Count(u => u.Owner == 1), n2 = w.Units.Count(u => u.Owner == 2);
                Vector2 c1 = Vector2.Zero, c2 = Vector2.Zero;
                foreach (var u in w.Units) { if (u.Owner == 1) c1 += u.Pos; else c2 += u.Pos; }
                Console.WriteLine($"  t={w.Time:0}s  A {n1} @({c1.X / Math.Max(1, n1):0},{c1.Y / Math.Max(1, n1):0})  B {n2} @({c2.X / Math.Max(1, n2):0},{c2.Y / Math.Max(1, n2):0})  wall {sw.Elapsed.TotalSeconds:0.0}s");
            }
        }
        int kills = start - w.Units.Count;
        KoreanDivision.Tests.MeasX.Report(w); Console.WriteLine($"결과: 승자 {w.Winner}, {w.Time:0.0}초, 실행 {sw.Elapsed.TotalSeconds:0.0}초, 명령 {w.CommandLog.Count}");
        var ms = w.Stats;
        foreach (var (kk, kn) in ms.KillSources) Console.WriteLine($"사살원인 {kk} = {kn}");
        Console.WriteLine($"틀 행동: 시작 {ms.DrillStarts} 끝 {ms.DrillEnds} 약진 {ms.DrillBounds} 기동 {ms.DrillManoeuvres} 돌격 {ms.DrillCharges} 역습 {ms.DrillCounters} 참호 {ms.DrillTrench} 연신 {ms.DrillLifts} 전환 {ms.TacticTransitions} 인계멈춤 {ms.DrillPaused} 포병전개 {ms.ArtilleryDeploys}");
        Console.WriteLine($"기병AI: 돌격 정면 {ms.CavChargeFront} 측면 {ms.CavChargeFlank} 후방 {ms.CavChargeRear} 측면경유 {ms.CavFlankRoutes} 소규모보류 {ms.CavSmallHeld} 물러남 {ms.CavScreens} | 추격: 후퇴중 희생 사격 {ms.KillsFleeingFire} 근접 {ms.KillsFleeingMelee} 후퇴하는 적 돌격 보병 {ms.InfChargeFleeing} 기병 {ms.CavChargeFleeing} | N55 기병 무모한 정면 {ms.CavRashFront} 돌격 사격 전진 {ms.DrillMarchAdvances} | N23 소탕 {ms.DrillSweeps} | S4 제압으로 꺾인 돌격 {ms.ChargesBroken}");   // pursuit (측정)
        Console.WriteLine($"F4 기병 돌격: 개시 {ms.CavChargeStarts} 무의미 정면 {ms.CavChargePointless} 자동 막음 {ms.CavAutoBlocked} 근접 사살 {ms.CavMeleeKills} 사유(후퇴/제압/승무원/고립/측면) {string.Join("/", ms.CavWhy)} · 하마 {ms.Dismounts} 승마 {ms.Mounts}");
        Console.WriteLine($"관찰: 사격 {shots}발 · 명중 {hits}발 ({(shots > 0 ? 100.0 * hits / shots : 0):0.0}%) · 사망 {kills}기 → 1킬당 {(kills > 0 ? (double)shots / kills : 0):0.0}발 (명중 {(kills > 0 ? (double)hits / kills : 0):0.0}발)");
        string T(double t) => t < 0 ? "-" : t.ToString("0.0", CultureInfo.InvariantCulture);
        Console.WriteLine($"N61 접근: 보병(1) 첫 사격 {T(infFirst)}초 · 기병(2) 첫 근접 {T(firstMelee)}초 · 전환 {ms.CavReactSwitches} · 그 시점 B 생존 {cavAtMelee}/{startB}");
        Console.WriteLine($"템포: 첫 사격 {T(firstShot)}초 · A 50% 손실 {T(t50A)}초 · B 50% 손실 {T(t50B)}초 · 사격 거리 평균 {(distN > 0 ? distSum / distN : 0):0.0} (첫 100발 {(distN > 0 ? dist100 / Math.Min(100, distN) : 0):0.0}) 칸");
        var alive = new Dictionary<string, int>();
        foreach (var u in w.Units) { string k = u.Owner + ":" + u.Def.Key; alive[k] = alive.GetValueOrDefault(k) + 1; }
        Console.WriteLine("  세력:종류            시작→남음   사격    근접   포격  방열%  돌격%");
        foreach (var (k, v) in obs)
            Console.WriteLine($"  {k,-20} {v[0],4}→{alive.GetValueOrDefault(k),-5} {v[1],7} {v[2],6} {v[3],6} {(v[5] > 0 ? 100.0 * v[4] / v[5] : 0),5:0} {(v[5] > 0 ? 100.0 * v[6] / v[5] : 0),5:0}");
        Console.WriteLine($"AI 비용: 사고 ms/틱 avg {aiSum / Math.Max(1, aiTicks):0.000} max {aiMax:0.00} (호출 {StressTest.TimedAi.Calls}, 호출당 {StressTest.TimedAi.TotalMs / Math.Max(1, StressTest.TimedAi.Calls):0.000} ms) · 전체 Step avg {stepSum / Math.Max(1, aiTicks):0.00} ms");
        foreach (var (f, ai) in w.Controllers)
            if (StressTest.TimedAi.Unwrap(ai) is TacticalAi ta && ta.StateOf(f) is { } ts)
                Console.WriteLine($"  [전술 AI {f}] 프로필 {ts.Profile.Name} 태세 {ts.Posture}/{ts.Effective} 중대 {ts.Groups.Count} 대대 {ts.Battalions.Count} · 명령 {ts.Orders} 돌격 {ts.Charges} 기병돌격 {ts.CavalryCharges} 참호 {ts.TrenchMoves} 후퇴 {ts.Fallbacks} 포병이동 {ts.ArtilleryMoves} 화력사수 {ts.FireHolds} 예비투입 {ts.ReserveCommits} · 연대 {ts.Regiments.Count} 준비사격 {ts.Preps} 연신 {ts.Lifts} 분대약진 {ts.SquadBounds} 전초 {ts.Outposts}/철수 {ts.OutpostWithdrawals} 역습 {ts.Counterattacks} 증원편입 {ts.Reinforced} 공격한계 {ts.Culminations}");
        return 0;
    }

    /// <summary>--balance std [지도|WxH] [AI1,AI2,..] [분] [시드]: 표준 게임 (점령 · 도시 패배) 을 창 없이 돌려 30초마다 세력 현황 · 전술 AI 중대 상태를 출력 (⑤ 관찰용).</summary>
    static int StdProbe(string[] a)
    {
        var defs = Defs.Load();
        string mapArg = a.Length > 2 ? a[2] : "100x100";
        string[] ais = (a.Length > 3 ? a[3] : "tactical").Split(',');
        double minutes = a.Length > 4 ? double.Parse(a[4], CultureInfo.InvariantCulture) : 15;
        int seed = a.Length > 5 ? int.Parse(a[5]) : Config.DefaultSeed;
        GameMap map = char.IsDigit(mapArg[0]) ? GameMap.Generate(int.Parse(mapArg.Split('x')[0]), int.Parse(mapArg.Split('x')[^1]), seed, 2) : MapFile.Load(mapArg, seed);
        var w = World.CreateStandard(map, defs);
        MapFile.ApplyFactions(w);
        w.Fog.Enabled = true;
        for (int f = 1; f <= map.Players; f++) w.Controllers.Add((f, AiRegistry.Create(ais[(f - 1) % ais.Length])!));
        var sw = Stopwatch.StartNew();
        while (w.Tick < minutes * 3600 && w.Winner == 0)
        {
            w.Step();
            if (w.Tick % 1800 != 0) continue;
            var line = new StringBuilder($"t={w.Time / 60:0.0}분");
            for (int f = 1; f < w.Factions.Count; f++)
                line.Append($"  F{f} {w.Controllers.First(c => c.Faction == f).Ai.Name} 유닛 {w.Units.Count(u => u.Owner == f)} 도시 {w.Buildings.Count(b => b.Owner == f && b.Def.City)} 점령지 {w.Buildings.Count(b => b.Owner == f)}{(w.Factions[f].Defeated ? " 패배" : "")}");
            Console.WriteLine(line);
            foreach (var (f, ai) in w.Controllers)
                if (ai is TacticalAi ta && ta.StateOf(f) is { } ts && !w.Factions[f].Defeated)
                    Console.WriteLine($"   tac{f} {ts.Effective} T=({ts.Target.X:0},{ts.Target.Y:0}) " + string.Join(" | ", ts.Groups.Select(g => $"{g.Kind.ToString()[0]}{g.Id} {g.Task}/{g.State} {g.Alive}/{g.Initial} @({g.Center.X:0},{g.Center.Y:0})")));
        }
        Console.WriteLine($"결과: 승자 {w.Winner}, {w.Time / 60:0.0}분, 실행 {sw.Elapsed.TotalSeconds:0.0}초");
        return 0;
    }

    /// <summary>--balance stuck [시드] [분]: 10인 자유전(selftest 와 같은 구성)을 돌리다 전차(큰 유닛)가 10초 이상 못 움직이면 주변 지형을 출력 (끼임 원인 조사용).</summary>
    static int StuckProbe(string[] a)
    {
        var defs = Defs.Load();
        int seed = a.Length > 2 ? int.Parse(a[2]) : 20261009;
        double minutes = a.Length > 3 ? double.Parse(a[3], CultureInfo.InvariantCulture) : 30;
        int size = Config.MapSizeFor(10);
        var w = World.CreateStandard(GameMap.Generate(size, size, seed, 10), defs);
        var ais = new[] { "standard", "standard", "standard", "standard", "standard", "standard", "rush", "rush", "passive", "passive" };
        for (int f = 1; f <= 10; f++) w.Controllers.Add((f, AiRegistry.Create(ais[f - 1])!));
        var last = new Dictionary<int, Vector2>();
        var count = new Dictionary<int, int>();
        while (w.Tick < minutes * 3600 && w.Winner == 0)
        {
            w.Step();
            if (w.Tick % 60 != 0) continue;
            foreach (var u in w.Units)
            {
                if (u.Def.Key != "tank_ft_male") continue;
                bool wants = u.Group != null && u.Order is UnitOrder.Move or UnitOrder.AttackMove && u.Target == null;
                if (wants && last.TryGetValue(u.Id, out var lp) && Vector2.DistanceSquared(lp, u.Pos) < 0.0025f) count[u.Id] = count.GetValueOrDefault(u.Id) + 1; else count.Remove(u.Id);
                last[u.Id] = u.Pos;
                if (count.GetValueOrDefault(u.Id) == 12)
                {
                    Console.WriteLine($"tank#{u.Id} owner {u.Owner} stuck at {u.Pos} order {u.Order} goal {u.Group?.Goal} tick {w.Tick}");
                    var (tx, ty) = GameMap.TileOf(u.Pos);
                    for (int y = ty - 6; y <= ty + 6; y++)
                    {
                        var sb = new StringBuilder();
                        for (int x = tx - 10; x <= tx + 10; x++)
                        {
                            char c = !w.Map.InBounds(x, y) ? ' ' : x == tx && y == ty ? 'T' : w.Map.OccupantAt(x, y) is int id && id != 0 ? (w.Find(id) is Building b ? (b.Def.City ? 'c' : b.Def.Capturable ? 's' : char.ToUpperInvariant(b.Def.Key[0])) : '?') : w.Map.IsWalkable(x, y) ? '.' : '#';
                            if (w.Units.Any(o => o != u && (int)o.Pos.X == x && (int)o.Pos.Y == y)) c = c == '.' ? 'u' : c;
                            sb.Append(c);
                        }
                        Console.WriteLine("  " + sb);
                    }
                    return 1;
                }
            }
        }
        Console.WriteLine("끼임 없음");
        return 0;
    }

    static int CountLines(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            int n = 0;
            while (sr.ReadLine() != null) n++;
            return n;
        }
        catch (IOException) { return 0; }
    }

    static string ProjectRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && d != null; i++, d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "KoreanDivision.csproj"))) return d.FullName;
        return Directory.GetCurrentDirectory();
    }

    sealed class Options
    {
        public string Suite = "all";
        public string? Out, ReportOnly;
        public int Procs = 4, Seeds = 3, AiSeeds = 3, SeedBase, Repeat = 1;
        public double AiMinutes = 30;
        public int[] Budgets = { 500, 2000, 6000 };
        public string[] Arenas = { "flat", "terrain", "city" };
        public string[]? Units;

        public static Options Parse(string[] a)
        {
            var o = new Options();
            for (int i = 0; i < a.Length; i++)
            {
                switch (a[i])
                {
                    case "--out": o.Out = Path.GetFullPath(a[++i]); break;
                    case "--seed-base": o.SeedBase = int.Parse(a[++i]); break;   // 시드를 SeedBase+1 부터
                    case "--repeat": o.Repeat = Math.Clamp(int.Parse(a[++i]), 1, 1000); break;
                    case "--procs": o.Procs = Math.Clamp(int.Parse(a[++i]), 1, 32); break;
                    case "--seeds": o.Seeds = Math.Max(1, int.Parse(a[++i])); break;
                    case "--ai-seeds": o.AiSeeds = Math.Max(0, int.Parse(a[++i])); break;
                    case "--ai-minutes": o.AiMinutes = double.Parse(a[++i], CultureInfo.InvariantCulture); break;
                    case "--budgets": o.Budgets = a[++i].Split(',').Select(int.Parse).ToArray(); break;
                    case "--arenas": o.Arenas = a[++i].Split(','); break;
                    case "--units": o.Units = a[++i].Split(','); break;
                    case "report": o.ReportOnly = Path.GetFullPath(a[++i]); break;
                    case "all" or "matchups" or "aigames" or "quick": o.Suite = a[i]; break;
                    default: throw new ArgumentException($"알 수 없는 --balance 인자: {a[i]}");
                }
            }
            if (o.Suite == "quick") { o.Budgets = new[] { 500 }; o.Seeds = 1; o.AiSeeds = 1; o.AiMinutes = Math.Min(o.AiMinutes, 10); }
            return o;
        }
    }

    // ------------------------------------------------------------------ 작업 목록

    static List<string> BuildJobs(Defs defs, Options o)
    {
        var jobs = new List<(double Cost, string Line)>();
        if (o.Suite is "all" or "matchups" or "quick")
        {
            var units = CombatUnits(defs);
            if (o.Units != null) units = units.Where(o.Units.Contains).ToList();
            for (int i = 0; i < units.Count; i++)
            for (int j = i + 1; j < units.Count; j++)
            foreach (int budget in o.Budgets)
            foreach (var arena in o.Arenas)
            for (int seed = o.SeedBase + 1; seed <= o.SeedBase + o.Seeds; seed++)
            for (int swap = 0; swap < 2; swap++)
            {
                jobs.Add((budget * (arena == "city" ? 1.3 : 1.0), $"M {units[i]} {units[j]} {budget} {arena} {seed} {swap} 0"));
                // 포병이 낀 쌍은 포병 쪽에 정찰병 관측자를 붙인 변형도 돌린다
                if (IsArty(units[i]) || IsArty(units[j]))
                    jobs.Add((budget * (arena == "city" ? 1.3 : 1.0), $"M {units[i]} {units[j]} {budget} {arena} {seed} {swap} 1"));
            }
        }
        if (o.Suite is "all" or "aigames" or "quick")
        {
            foreach (var sc in Scenarios)
                for (int seed = o.SeedBase + 1; seed <= o.SeedBase + o.AiSeeds; seed++)
                    jobs.Add((1e7 * (sc.Players >= 10 ? 3 : 1), $"G {sc.Name} {seed}"));
        }
        // 무거운 것부터 → 프로세스에 interleave 로 나눠도 끝나는 시각이 비슷하다
        return jobs.OrderByDescending(j => j.Cost).Select(j => j.Line).ToList();
    }

    sealed record Scenario(string Name, string Map, int Players, string[] Ais, string Desc);

    static readonly Scenario[] Scenarios =
    {
        new("corners4_std", "corners4", 4, new[] { "standard", "standard", "standard", "standard" }, "corners4 4인, 전원 standard"),
        new("corners4_rush", "corners4", 4, new[] { "standard", "rush", "standard", "rush" }, "corners4 4인, 1·3 standard / 2·4 rush"),
        new("gen2_std", "gen", 2, new[] { "standard", "standard" }, "생성 맵 2인 (100x100), standard 대 standard"),
        new("gen2_rush", "gen", 2, new[] { "standard", "rush" }, "생성 맵 2인 (100x100), standard 대 rush"),
        new("gen10_std", "gen", 10, new[] { "standard" }, "생성 맵 10인, 전원 standard"),
    };

    // ------------------------------------------------------------------ 하위 프로세스

    static int Worker(string[] a)
    {
        // --balance-worker <jobs> <index> <count> <out> <aiMinutes>
        var defs = Defs.Load();
        var lines = File.ReadAllLines(a[1]);
        int index = int.Parse(a[2]), count = int.Parse(a[3]);
        double aiMinutes = double.Parse(a[5], CultureInfo.InvariantCulture);
        using var outFile = new StreamWriter(new FileStream(a[4], FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        for (int i = index; i < lines.Length; i += count)
        {
            var t = lines[i].Split(' ');
            string json;
            var sw = Stopwatch.StartNew();
            try
            {
                if (t[0] == "M")
                {
                    var r = RunMatch(defs, t[1], t[2], int.Parse(t[3]), t[4], int.Parse(t[5]), t[6] == "1", t.Length > 7 && t[7] == "1");
                    r.Ms = sw.Elapsed.TotalMilliseconds;
                    json = JsonSerializer.Serialize(r, Json);
                }
                else
                {
                    var r = RunGame(defs, t[1], int.Parse(t[2]), aiMinutes);
                    r.Ms = sw.Elapsed.TotalMilliseconds;
                    json = JsonSerializer.Serialize(r, Json);
                }
            }
            catch (Exception e)
            {
                json = t[0] == "M"
                    ? JsonSerializer.Serialize(new MatchResult { X = t[1], Y = t[2], Budget = int.Parse(t[3]), Arena = t[4], Seed = int.Parse(t[5]), Swap = t[6] == "1", Spot = t.Length > 7 && t[7] == "1", Error = e.GetType().Name + ": " + e.Message })
                    : JsonSerializer.Serialize(new GameResult { Scenario = t[1], Seed = int.Parse(t[2]), Error = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace });
            }
            outFile.WriteLine(json);
        }
        return 0;
    }

    // ------------------------------------------------------------------ (a) 동일 비용 교전

    const float MatchLimitSec = 900;     // v0.4: 체력 x2.5 · 사거리 증가로 교전이 길어져 8분 -> 15분
    const int SpotterCount = 4;          // 포병 쪽에 붙이는 정찰병 수 (예산 밖의 보너스)

    internal static MatchResult RunMatch(Defs defs, string x, string y, int budget, string arena, int seed, bool swap, bool spot = false)
    {
        var dx = defs.Units[x];
        var dy = defs.Units[y];
        double cx = CostEq(defs, dx), cy = CostEq(defs, dy);
        int nx = Math.Clamp((int)Math.Round(budget / cx), 1, BattleSetup.MaxPerSide), ny = Math.Clamp((int)Math.Round(budget / cy), 1, BattleSetup.MaxPerSide);
        int spotN = spot && defs.Units.ContainsKey("scout") ? SpotterCount : 0;
        string sx = $"{x}:{nx}" + (spotN > 0 && IsArty(x) ? $",scout:{spotN}" : "");
        string sy = $"{y}:{ny}" + (spotN > 0 && IsArty(y) ? $",scout:{spotN}" : "");
        var setup = new BattleSetup
        {
            SideA = swap ? sy : sx,
            SideB = swap ? sx : sy,
            Map = arena,
            Distance = arena == "city" ? 80 : 36,
            AiVsAi = true,
        };
        var w = setup.CreateWorld(defs, 1000 + seed);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, new BattleAi()));
        w.Controllers.Add((2, new BattleAi()));
        int xf = swap ? 2 : 1, yf = 3 - xf;
        var r = new MatchResult { X = x, Y = y, Budget = budget, Arena = arena, Seed = seed, Swap = swap, NX = nx, NY = ny, CostX0 = nx * cx, CostY0 = ny * cy, Spot = spotN > 0, SpotN = spotN };
        int limit = (int)(MatchLimitSec / Config.SimDt);
        long lastShotTick = -1;
        var perUnitShots = new Dictionary<int, int>();
        double supW = Weights.GetValueOrDefault("supplies", 2);
        while (w.Tick < limit && w.Winner == 0)
        {
            w.Step();
            // 사격 통계 (World.Shots: 최근 10틱 보관 → 매 틱 새 항목만 집계)
            long maxTick = lastShotTick;
            foreach (var s in w.Shots)
            {
                if (s.Tick <= lastShotTick) continue;
                if (s.Tick > maxTick) maxTick = s.Tick;
                bool isX = s.Faction == xf;
                if (!isX && s.Faction != yf) continue;
                var wp = (w.Find(s.ShooterId) as Unit)?.Weapon;
                bool splash = World.IsIndirect(wp);
                double dist = Vector2.Distance(s.From, s.To);
                double sustain = wp is { Ammo: > 0 } ? supW : 0;   // 1발 = 탄약 1
                if (isX) { r.ShotsX++; r.DistSumX += dist; r.SustainX += sustain; if (!splash) { r.DirectX++; if (s.Hit) r.HitsX++; } }
                else { r.ShotsY++; r.DistSumY += dist; r.SustainY += sustain; if (!splash) { r.DirectY++; if (s.Hit) r.HitsY++; } }
                if (wp is { Ammo: > 0 })
                {
                    perUnitShots.TryGetValue(s.ShooterId, out int c);
                    perUnitShots[s.ShooterId] = ++c;
                    if (c == wp.Ammo + 1) { if (isX) r.DryX++; else r.DryY++; }   // 탄창을 넘겨 쏜 유닛 = 재보급 없이는 바닥났을 유닛
                }
            }
            lastShotTick = maxTick;
            if (w.Tick % 60 == 0)
            {
                Remaining(w, defs, xf, yf, dx, dy, cx, cy, out double rx, out double ry, out _, out _, out _, out _);
                if (r.T50X == 0 && rx <= r.CostX0 * 0.5) r.T50X = w.Time;
                if (r.T50Y == 0 && ry <= r.CostY0 * 0.5) r.T50Y = w.Time;
            }
        }
        Remaining(w, defs, xf, yf, dx, dy, cx, cy, out double fx, out double fy, out double hx, out double hy, out int ax, out int ay);
        r.CostXEnd = fx; r.CostYEnd = fy; r.HpCostXEnd = hx; r.HpCostYEnd = hy; r.AliveX = ax; r.AliveY = ay;
        r.Winner = w.Winner;
        r.EndSec = w.Time;
        return r;
    }

    static void Remaining(World w, Defs defs, int xf, int yf, UnitDef dx, UnitDef dy, double cx, double cy,
        out double costX, out double costY, out double hpX, out double hpY, out int aliveX, out int aliveY)
    {
        costX = costY = hpX = hpY = 0;
        aliveX = aliveY = 0;
        foreach (var u in w.Units)
        {
            if (u.Hp <= 0) continue;
            if (u.Def != dx && u.Def != dy) continue;   // 관측자(정찰병) 등 예산 밖 유닛은 비용 집계에서 제외
            if (u.Owner == xf) { costX += cx; hpX += cx * u.Hp / u.MaxHp; aliveX++; }
            else if (u.Owner == yf) { costY += cy; hpY += cy * u.Hp / u.MaxHp; aliveY++; }
        }
    }

    // ------------------------------------------------------------------ (b) AI 전체 경기

    internal static GameResult RunGame(Defs defs, string scenario, int seed, double minutes)
    {
        var sc = Scenarios.First(s => s.Name == scenario);
        World world;
        if (sc.Map == "corners4")
        {
            world = World.CreateStandard(MapFile.Load("corners4", seed), defs);
            MapFile.ApplyFactions(world);
        }
        else
        {
            int size = Config.MapSizeFor(sc.Players);
            world = World.CreateStandard(GameMap.Generate(size, size, seed, sc.Players), defs);
        }
        int n = sc.Players;
        var res = new GameResult { Scenario = scenario, Seed = seed, Players = n, LimitMin = minutes };
        var inv = new Invariants();
        var seen = new HashSet<int>[n + 1];
        var alive = new Dictionary<int, string>();   // 유닛 id → 종류 (전사 집계용)
        var owner = new Dictionary<int, int>();
        for (int f = 1; f <= n; f++)
        {
            string aiName = sc.Ais[(f - 1) % sc.Ais.Length];
            world.Controllers.Add((f, AiRegistry.Create(aiName)!));
            world.Faction(f).IsAi = true;
            res.Factions.Add(new FactionGame { Faction = f, Ai = aiName });
            seen[f] = new HashSet<int>();
        }
        foreach (var u in world.Units) { seen[u.Owner].Add(u.Id); alive[u.Id] = u.Def.Key; owner[u.Id] = u.Owner; }   // 시작 유닛은 '생산'에서 제외
        int limit = (int)(minutes * 60 / Config.SimDt);
        var curIds = new HashSet<int>();
        var tripState = new Dictionary<int, int>();
        long lastShotTick = -1;
        foreach (var fg in res.Factions) fg.AmmoApi = AmmoApi;
        while (world.Tick < limit && world.Winner == 0)
        {
            world.Step();
            if (world.Shots.Count > 0)   // 탄약 쓰는 무기 사격 수 · 보급품 소모 추정
            {
                long maxTick = lastShotTick;
                foreach (var sh in world.Shots)
                {
                    if (sh.Tick <= lastShotTick) continue;
                    if (sh.Tick > maxTick) maxTick = sh.Tick;
                    if (sh.Faction < 1 || sh.Faction > n) continue;
                    if ((world.Find(sh.ShooterId) as Unit)?.Weapon is { Ammo: > 0 } wp)
                    {
                        var fg = res.Factions[sh.Faction - 1];
                        fg.ShotsAmmo++; fg.SuppliesShot += 1;
                    }
                }
                lastShotTick = maxTick;
            }
            if (world.Tick % 60 == 0)
            {
                inv.Sample(world);
                SampleAmmo(world, res, tripState);
                curIds.Clear();
                foreach (var u in world.Units)
                {
                    curIds.Add(u.Id);
                    if (u.Owner > 0 && seen[u.Owner].Add(u.Id))
                    {
                        var b = res.Factions[u.Owner - 1].Built;
                        b[u.Def.Key] = b.GetValueOrDefault(u.Def.Key) + 1;
                        alive[u.Id] = u.Def.Key; owner[u.Id] = u.Owner;
                    }
                }
                foreach (var (id, key) in alive.ToList())
                    if (!curIds.Contains(id))
                    {
                        var l = res.Factions[owner[id] - 1].Lost;
                        l[key] = l.GetValueOrDefault(key) + 1;
                        alive.Remove(id); owner.Remove(id);
                    }
            }
            if (world.Tick % 1800 == 0) SampleSeries(world, defs, res);
        }
        SampleSeries(world, defs, res);
        res.Winner = world.Winner;
        res.EndMin = world.Time / 60.0;
        foreach (var f in res.Factions) f.Defeated = world.Faction(f.Faction).Defeated;
        res.Violations.AddRange(inv.Violations);
        return res;
    }

    /// <summary>60틱마다: 세력별 탄약 0 유닛 · 재보급 여정 · 연료 0 표본 (sim 이 노출하지 않으면 AmmoApi=false 로 n/a).</summary>
    static void SampleAmmo(World w, GameResult res, Dictionary<int, int> tripState)
    {
        if (!AmmoApi) return;
        var units = new int[res.Players + 1]; var zero = new int[res.Players + 1]; var trip = new int[res.Players + 1];
        foreach (var u in w.Units)
        {
            if (u.Owner < 1 || u.Owner > res.Players) continue;
            var f = res.Factions[u.Owner - 1];
            if (UsesAmmo(u))
            {
                units[u.Owner]++;
                if (AmmoOf(u) == 0) zero[u.Owner]++;
                int st = TripState(u);
                if (st != 0) trip[u.Owner]++;
                tripState.TryGetValue(u.Id, out int prev);
                if (prev == 0 && st != 0) f.TripsStarted++;
                tripState[u.Id] = st;
            }
        }
        for (int i = 1; i <= res.Players; i++)
        {
            var f = res.Factions[i - 1];
            f.AmmoUnitSamples += units[i]; f.AmmoZeroSamples += zero[i]; f.TripSamples += trip[i];
            if (units[i] > 0) f.PeakDryFrac = Math.Max(f.PeakDryFrac, zero[i] / (double)units[i]);
        }
    }

    static void SampleSeries(World w, Defs defs, GameResult res)
    {
        for (int f = 1; f <= res.Players; f++)
        {
            var fa = w.Faction(f);
            int army = 0, workers = 0;
            foreach (var u in w.Units)
            {
                if (u.Owner != f) continue;
                if (u.Weapon != null) army++;
            }
            int Count(string key, bool on = false) => 0;   // 건설 · 생산 건물 없음 (CSV 열은 호환용으로 유지)
            int sites = w.Buildings.Count(b => b.Owner == f && b.Def.Capturable);
            int arty = 0, ammoU = 0, ammoZ = 0, onTrip = 0, fuelU = 0, fuelZ = 0;
            foreach (var u in w.Units)
            {
                if (u.Owner != f) continue;
                if (IsArty(u.Def.Key)) arty++;
                if (AmmoApi && UsesAmmo(u)) { ammoU++; if (AmmoOf(u) == 0) ammoZ++; if (TripState(u) != 0) onTrip++; }
            }
            int mask = 0;
            double net = 0;
            double S(int r) => 0;   // 자원 없음
            res.Factions[f - 1].Series.Add(new[]
            {
                Math.Round(w.Time, 1), S(0), S(1), S(2), S(3), S(4), net, army, 0, army, workers,
                Count("barracks"), Count("depot"), Count("factory"), Count("supply_plant"), Count("supply_plant", on: true), Count("turret"), sites, mask, fa.Defeated ? 1 : 0,
                arty, ammoU, ammoZ, onTrip, fuelU, fuelZ,
            });
        }
    }

    // ------------------------------------------------------------------ 보고서

    static string F(double v, string fmt = "0.0") => v.ToString(fmt, CultureInfo.InvariantCulture);
    static string Pct(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    internal static void Report(Defs defs, string dir)
    {
        var matches = new List<MatchResult>();
        var games = new List<GameResult>();
        int errors = 0;
        foreach (var file in Directory.GetFiles(dir, "results_*.jsonl").OrderBy(f => f, StringComparer.Ordinal))
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var doc = JsonDocument.Parse(line);
                string kind = doc.RootElement.GetProperty("Kind").GetString() ?? "";
                if (kind == "match") { var m = JsonSerializer.Deserialize<MatchResult>(line, Json)!; if (m.Error != null) errors++; else matches.Add(m); }
                else { var g = JsonSerializer.Deserialize<GameResult>(line, Json)!; if (g.Error != null) { errors++; Console.Error.WriteLine($"경기 오류 {g.Scenario}/{g.Seed}: {g.Error}"); } else games.Add(g); }
            }
        var sb = new StringBuilder();
        string stamp = Path.GetFileName(dir.TrimEnd('\\', '/'));
        sb.AppendLine("# 밸런스 자동 대전 보고서");
        sb.AppendLine();
        sb.AppendLine($"- 실행: {stamp}  ·  밸런스 해시 `{defs.Hash}`  ·  게임 버전 {Config.Version}");
        sb.AppendLine($"- 교전 {matches.Count}판 (오류 {errors}) · AI 전체 경기 {games.Count}판");
        sb.AppendLine("- 이 보고서는 밸런스 값을 바꾸지 않고 관찰 결과만 정리한 것입니다.");
        sb.AppendLine();
        UnitTable(defs, sb);
        var units = CombatUnits(defs);
        if (matches.Count > 0) MatchupSections(defs, sb, matches, units, dir);
        if (games.Count > 0) GameSections(defs, sb, games, matches, units, dir);
        File.WriteAllText(Path.Combine(dir, "report.md"), sb.ToString(), new UTF8Encoding(true));
    }

    static void UnitTable(Defs defs, StringBuilder sb)
    {
        sb.AppendLine("## 0. 비용 환산 · 유닛 제원");
        sb.AppendLine();
        sb.AppendLine("자금 상당 가중치: " + string.Join(" · ", Weights.Select(kv => $"{kv.Key} x{F(kv.Value, "0.#")}")) +
                      " — 보급품 2 는 공장 변환비(자금 2 → 보급품 1), 강철 2 · 인력 4 는 도시/광산 수입 비율을 어림한 값. 유지비 · 인구 칸은 환산에 넣지 않았습니다.");
        sb.AppendLine();
        sb.AppendLine("| 유닛 | 환산 비용 | 체력 | 장갑 | DPS(명중 반영) | 대기갑 DPS | 사거리 | 속도 | 시야 | 체력x DPS / 비용 | 인구 | 유지비/s |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var u in defs.Units.Values)
        {
            double cost = CostEq(defs, u);
            var wp = defs.Weapon(u.Weapon);
            double dps = wp == null ? 0 : wp.Damage * wp.Accuracy / wp.CooldownMean;
            double dpsA = wp == null ? 0 : dps * (wp.Bonus?.GetValueOrDefault("armored", 1f) ?? 1f);
            double armor = defs.Armor(u.Armor)?.Armor ?? 0;
            sb.AppendLine($"| {u.Key} ({u.Name}) | {F(cost, "0")} | {F(u.Hp, "0")} | {F(armor, "0")} | {F(dps)} | {F(dpsA)} | {F(wp?.Range ?? 0)} | {F(u.Speed)} | {u.Sight} | {F(u.Hp * dps / Math.Max(1, cost), "0.0")} | - | 0 |");
        }
        sb.AppendLine();
    }

    sealed class Cell { public double Score, Games, Destroyed, Lost, EndSecSum; public int Decisive; public int Draws; }

    static void MatchupSections(Defs defs, StringBuilder sb, List<MatchResult> matches, List<string> units, string dir)
    {
        var budgets = matches.Select(m => m.Budget).Distinct().OrderBy(b => b).ToList();
        var arenas = matches.Select(m => m.Arena).Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList();
        sb.AppendLine("## A. 동일 비용 교전");
        sb.AppendLine();
        sb.AppendLine($"모든 전투 유닛 쌍을 같은 비용(예산 {string.Join(" / ", budgets)}, 환산 자금 기준 수량)으로 맞붙였습니다. 전장 {string.Join(", ", arenas)} · 시드 {matches.Select(m => m.Seed).Distinct().Count()}개 · 스폰 위치 좌우 교환(쌍마다 2판). " +
                      $"양쪽 모두 BattleAi 가 조작 (안개 켬, 시간 제한 {MatchLimitSec / 60:0}분(v0.4: 체력 x2.5 로 8분에서 상향), 시간 초과는 무승부 취급). 포병류(artillery · towed_artillery)는 관측자 없는 기본 판 + 정찰병 {SpotterCount}기를 붙인 변형(A-포병)을 따로 돌립니다. 공병 등 건설 유닛은 제외.");
        sb.AppendLine();
        sb.AppendLine("- **승률** = 행 유닛의 점수 (승 1 · 무 0.5 · 패 0). **효율** = 행이 파괴한 환산 비용 / 행이 잃은 환산 비용 (합계 비율, 99 상한). 1 이상이면 행이 비용 대비 이득.");
        sb.AppendLine();

        // CSV: 원본
        var csv = new StringBuilder("x,y,budget,arena,seed,swap,nx,ny,cost_x0,cost_y0,cost_x_end,cost_y_end,hp_cost_x_end,hp_cost_y_end,alive_x,alive_y,winner_faction,x_wins,end_sec,t50_x,t50_y,spot,spot_n,shots_x,shots_y,direct_x,direct_y,hits_x,hits_y,dist_sum_x,dist_sum_y,sustain_x,sustain_y,dry_x,dry_y\n");
        foreach (var m in matches)
            csv.AppendLine(string.Join(",", m.X, m.Y, m.Budget, m.Arena, m.Seed, m.Swap ? 1 : 0, m.NX, m.NY, F(m.CostX0), F(m.CostY0), F(m.CostXEnd), F(m.CostYEnd), F(m.HpCostXEnd), F(m.HpCostYEnd), m.AliveX, m.AliveY, m.Winner, m.XWins ? 1 : m.YWins ? 0 : 0.5, F(m.EndSec), F(m.T50X), F(m.T50Y), m.Spot ? 1 : 0, m.SpotN, m.ShotsX, m.ShotsY, m.DirectX, m.DirectY, m.HitsX, m.HitsY, F(m.DistSumX), F(m.DistSumY), F(m.SustainX), F(m.SustainY), m.DryX, m.DryY));
        File.WriteAllText(Path.Combine(dir, "matchups.csv"), csv.ToString());
        // 이후 행렬 · 집계는 관측자 없는 기본 판만 (관측자 변형은 A-포병 에서 따로)
        var spotMatches = matches.Where(m => m.Spot).ToList();
        matches = matches.Where(m => !m.Spot).ToList();
        if (matches.Count == 0) return;

        // 쌍별 집계: filter 로 (예산, 전장) 선택
        Dictionary<(string, string), Cell> Aggregate(Func<MatchResult, bool> filter)
        {
            var d = new Dictionary<(string, string), Cell>();
            foreach (var m in matches.Where(filter))
            {
                foreach (var (me, other, mine) in new[] { (m.X, m.Y, true), (m.Y, m.X, false) })
                {
                    if (!d.TryGetValue((me, other), out var c)) d[(me, other)] = c = new Cell();
                    bool win = mine ? m.XWins : m.YWins, loss = mine ? m.YWins : m.XWins;
                    c.Games++;
                    c.Score += win ? 1 : loss ? 0 : 0.5;
                    if (win || loss) { c.Decisive++; c.EndSecSum += m.EndSec; } else c.Draws++;
                    c.Destroyed += mine ? m.CostY0 - m.CostYEnd : m.CostX0 - m.CostXEnd;
                    c.Lost += mine ? m.CostX0 - m.CostXEnd : m.CostY0 - m.CostYEnd;
                }
            }
            return d;
        }
        double Eff(Cell c) => c.Lost <= 0 ? (c.Destroyed > 0 ? 99 : 1) : Math.Min(99, c.Destroyed / c.Lost);

        void Matrix(StringBuilder s, string title, Dictionary<(string, string), Cell> d, Func<Cell, string> fmt)
        {
            s.AppendLine($"**{title}**");
            s.AppendLine();
            s.AppendLine("| 행 \\ 열 | " + string.Join(" | ", units) + " |");
            s.AppendLine("|---|" + string.Join("|", units.Select(_ => "---:")) + "|");
            foreach (var r in units)
                s.AppendLine($"| **{r}** | " + string.Join(" | ", units.Select(c => r == c ? "-" : d.TryGetValue((r, c), out var cell) ? fmt(cell) : "")) + " |");
            s.AppendLine();
        }

        void MatrixCsv(string name, Dictionary<(string, string), Cell> d, Func<Cell, string> fmt)
        {
            var c = new StringBuilder("row\\col," + string.Join(",", units) + "\n");
            foreach (var r in units) c.AppendLine(r + "," + string.Join(",", units.Select(u => r == u ? "" : d.TryGetValue((r, u), out var cell) ? fmt(cell) : "")));
            File.WriteAllText(Path.Combine(dir, name), c.ToString());
        }

        var overall = new List<(string Scope, Dictionary<(string, string), Cell> D)>();
        foreach (int b in budgets)
        {
            var d = Aggregate(m => m.Budget == b);
            overall.Add(($"예산 {b}", d));
            sb.AppendLine($"### A-{budgets.IndexOf(b) + 1}. 예산 {b} (전 전장 합산)");
            sb.AppendLine();
            Matrix(sb, "승률 (행 기준)", d, c => Pct(c.Score / c.Games));
            Matrix(sb, "비용 효율 (파괴 비용 / 손실 비용)", d, c => F(Eff(c), "0.00"));
            Matrix(sb, "결판 평균 시간 (초, 무승부 제외)", d, c => c.Decisive > 0 ? F(c.EndSecSum / c.Decisive, "0") : "-");
            MatrixCsv($"winrate_budget{b}.csv", d, c => F(c.Score / c.Games, "0.000"));
            MatrixCsv($"efficiency_budget{b}.csv", d, c => F(Eff(c), "0.000"));
            MatrixCsv($"ttk_budget{b}.csv", d, c => c.Decisive > 0 ? F(c.EndSecSum / c.Decisive, "0.0") : "");
        }
        var all = Aggregate(_ => true);
        MatrixCsv("winrate_all.csv", all, c => F(c.Score / c.Games, "0.000"));
        MatrixCsv("efficiency_all.csv", all, c => F(Eff(c), "0.000"));

        // 전장별 유닛 종합 점수
        sb.AppendLine("### A-전장. 전장별 유닛 종합 승률 (전 예산 · 모든 상대 평균)");
        sb.AppendLine();
        sb.AppendLine("| 유닛 | " + string.Join(" | ", arenas) + " | 전체 | 전체 효율 |");
        sb.AppendLine("|---|" + string.Join("|", arenas.Select(_ => "---:")) + "|---:|---:|");
        var unitCsv = new StringBuilder("unit," + string.Join(",", arenas.Select(a => "winrate_" + a)) + ",winrate_all,efficiency_all\n");
        var score = new Dictionary<string, double>();
        var effAll = new Dictionary<string, double>();
        var perArena = arenas.ToDictionary(a => a, a => Aggregate(m => m.Arena == a));
        foreach (var u in units)
        {
            double Mean(Dictionary<(string, string), Cell> d) { var cs = d.Where(kv => kv.Key.Item1 == u).Select(kv => kv.Value).ToList(); return cs.Count == 0 ? 0 : cs.Sum(c => c.Score) / cs.Sum(c => c.Games); }
            var cellsAll = all.Where(kv => kv.Key.Item1 == u).Select(kv => kv.Value).ToList();
            double destroyed = cellsAll.Sum(c => c.Destroyed), lost = cellsAll.Sum(c => c.Lost);
            score[u] = Mean(all);
            effAll[u] = lost <= 0 ? 99 : Math.Min(99, destroyed / lost);
            sb.AppendLine($"| {u} | " + string.Join(" | ", arenas.Select(a => Pct(Mean(perArena[a])))) + $" | **{Pct(score[u])}** | {F(effAll[u], "0.00")} |");
            unitCsv.AppendLine($"{u},{string.Join(",", arenas.Select(a => F(Mean(perArena[a]), "0.000")))},{F(score[u], "0.000")},{F(effAll[u], "0.000")}");
        }
        File.WriteAllText(Path.Combine(dir, "unit_overall.csv"), unitCsv.ToString());
        sb.AppendLine();

        // 예산별 전체 점수 변화
        sb.AppendLine("### A-규모. 예산(병력 규모)에 따른 종합 승률 변화");
        sb.AppendLine();
        sb.AppendLine("| 유닛 | " + string.Join(" | ", budgets.Select(b => "예산 " + b)) + " |");
        sb.AppendLine("|---|" + string.Join("|", budgets.Select(_ => "---:")) + "|");
        var scaleScore = new Dictionary<string, List<double>>();
        foreach (var u in units)
        {
            var row = new List<double>();
            foreach (var (_, d) in overall)
            {
                var cs = d.Where(kv => kv.Key.Item1 == u).Select(kv => kv.Value).ToList();
                row.Add(cs.Count == 0 ? 0 : cs.Sum(c => c.Score) / cs.Sum(c => c.Games));
            }
            scaleScore[u] = row;
            sb.AppendLine($"| {u} | " + string.Join(" | ", row.Select(Pct)) + " |");
        }
        sb.AppendLine();

        sb.AppendLine("> 읽는 법: 사거리가 비슷한 총격전에서는 전투력이 대략 (수 x 개체 화력)^2 에 비례해 (Lanchester 제곱 법칙) 같은 비용이면 싼 유닛을 많이 뽑은 쪽이 유리해지는 경향이 있습니다. " +
                      "그래서 이 표는 '개체가 강한가' 보다 '비용 환산이 공정한가' 를 보는 지표에 가깝고, 환산 가중치(위 표)를 바꾸면 결과가 달라질 수 있습니다. 유지비 · 인구 · 생산 시간 · 시야 · 점령 능력은 반영되지 않았습니다.");
        sb.AppendLine();

        // 시간 초과 · 위치 편향
        int timeouts = matches.Count(m => m.Winner == 0), mutual = matches.Count(m => m.Winner == -1);
        int leftWins = matches.Count(m => m.Winner == 1), rightWins = matches.Count(m => m.Winner == 2);
        double leftRate = leftWins + rightWins == 0 ? 0.5 : leftWins / (double)(leftWins + rightWins);
        sb.AppendLine($"- 시간 초과 {timeouts}판 ({Pct(timeouts / (double)matches.Count)}), 동시 전멸 {mutual}판. 좌측(세력 1) 승리 {leftWins} / 우측(세력 2) 승리 {rightWins} → 좌측 승률 {Pct(leftRate)} (50% 에서 크게 벗어나면 배치 · 지형 편향).");
        sb.AppendLine();

        // 관찰
        var obs = new List<string>();
        var ranked = units.OrderByDescending(u => score[u]).ToList();
        if (ranked.Count > 0)
        {
            string top = ranked[0], bottom = ranked[^1];
            if (score[top] >= 0.65) obs.Add($"**지배적 유닛**: `{top}` 이(가) 동일 비용에서 평균 {Pct(score[top])} (효율 {F(effAll[top], "0.00")}) — 같은 돈이면 거의 항상 이득.");
            else obs.Add($"종합 1위는 `{top}` ({Pct(score[top])}), 2위 `{ranked[1]}` ({Pct(score[ranked[1]])}); 압도적 지배 유닛은 없음 (65% 기준).");
            if (score[bottom] <= 0.35) obs.Add($"**쓸모 없는 유닛 후보**: `{bottom}` 은(는) 동일 비용에서 평균 {Pct(score[bottom])} 로 거의 모든 상대에게 짐 (효율 {F(effAll[bottom], "0.00")}).");
            else obs.Add($"종합 최하위는 `{bottom}` ({Pct(score[bottom])}) 이지만 35% 이하는 아님 — 상성 · 용도(시야, 속도, 점령)로 설명될 수 있는 수준.");
        }
        // 극단적 상성
        var lopsided = new List<string>();
        for (int i = 0; i < units.Count; i++)
        for (int j = i + 1; j < units.Count; j++)
            if (all.TryGetValue((units[i], units[j]), out var cell))
            {
                double r = cell.Score / cell.Games;
                if (r >= 0.9 || r <= 0.1) lopsided.Add($"`{(r >= 0.5 ? units[i] : units[j])}` > `{(r >= 0.5 ? units[j] : units[i])}` ({Pct(Math.Max(r, 1 - r))})");
            }
        if (lopsided.Count > 0) obs.Add("**극단적 상성 (동일 비용 90%+ 승률)**: " + string.Join(", ", lopsided) + ".");
        // 가위바위보
        var cycles = new List<string>();
        for (int i = 0; i < units.Count; i++)
        for (int j = 0; j < units.Count; j++)
        for (int k = 0; k < units.Count; k++)
            if (i < j && i < k && j != k)
            {
                double ab = Rate(all, units[i], units[j]), bc = Rate(all, units[j], units[k]), ca = Rate(all, units[k], units[i]);
                if (ab > 0.6 && bc > 0.6 && ca > 0.6) cycles.Add($"{units[i]} > {units[j]} > {units[k]} > {units[i]}");
            }
        obs.Add(cycles.Count > 0 ? "가위바위보 순환 상성 (60%+): " + string.Join("; ", cycles) + "." : "60% 이상의 3-순환 상성은 없음 (상성이 한 방향 서열에 가까움).");
        // 규모 의존
        var scaling = new List<string>();
        foreach (var u in units)
        {
            var row = scaleScore[u];
            if (row.Count >= 2 && Math.Abs(row[^1] - row[0]) >= 0.12) scaling.Add($"`{u}` {Pct(row[0])} → {Pct(row[^1])}");
        }
        if (scaling.Count > 0) obs.Add($"규모 의존 (가장 작은 예산 → 가장 큰 예산 종합 승률이 12%p 이상 변함, 소수가 다수에 유리하거나 그 반대): {string.Join(", ", scaling)}.");
        // 전장 의존
        var arenaSwing = new List<string>();
        if (arenas.Count >= 2)
            foreach (var u in units)
            {
                var rates = arenas.Select(a => { var cs = perArena[a].Where(kv => kv.Key.Item1 == u).Select(kv => kv.Value).ToList(); return cs.Count == 0 ? 0 : cs.Sum(c => c.Score) / cs.Sum(c => c.Games); }).ToList();
                if (rates.Max() - rates.Min() >= 0.12) arenaSwing.Add($"`{u}` {arenas[rates.IndexOf(rates.Min())]} {Pct(rates.Min())} ~ {arenas[rates.IndexOf(rates.Max())]} {Pct(rates.Max())}");
            }
        if (arenaSwing.Count > 0) obs.Add($"지형 의존 (전장별 종합 승률 편차 12%p 이상): {string.Join(", ", arenaSwing)}.");
        if (Math.Abs(leftRate - 0.5) > 0.06) obs.Add($"배치 편향 의심: 좌측 승률 {Pct(leftRate)}.");
        // 기갑 대 대전차
        if (all.TryGetValue(("heavy_at_rifleman", "tank_ft_male"), out var atTank))
            obs.Add($"대전차보병 대 전차 (동일 비용): 대전차보병 승률 {Pct(atTank.Score / atTank.Games)}, 효율 {F(Eff(atTank), "0.00")}.");
        SustainSections(defs, sb, matches, spotMatches, units, dir);
        sb.AppendLine("### A-관찰");
        sb.AppendLine();
        foreach (var line in obs) sb.AppendLine("- " + line);
        sb.AppendLine();
    }

    /// <summary>한 판을 (내 유닛, 상대) 시점으로 펼친 행.</summary>
    sealed record Side(string Me, string Other, bool Win, bool Loss, bool Timeout, int N, double Cost0, double Destroyed, double Lost, int Shots, int Direct, int Hits, double DistSum, double Sustain, int Dry);

    static IEnumerable<Side> Sides(MatchResult m)
    {
        bool to = m.Winner == 0;
        yield return new Side(m.X, m.Y, m.XWins, m.YWins, to, m.NX, m.CostX0, m.CostY0 - m.CostYEnd, m.CostX0 - m.CostXEnd, m.ShotsX, m.DirectX, m.HitsX, m.DistSumX, m.SustainX, m.DryX);
        yield return new Side(m.Y, m.X, m.YWins, m.XWins, to, m.NY, m.CostY0, m.CostX0 - m.CostXEnd, m.CostY0 - m.CostYEnd, m.ShotsY, m.DirectY, m.HitsY, m.DistSumY, m.SustainY, m.DryY);
    }

    static void SustainSections(Defs defs, StringBuilder sb, List<MatchResult> matches, List<MatchResult> spot, List<string> units, string dir)
    {
        // ---- A-유지: 유닛별 사격 · 유지(탄약) 비용
        sb.AppendLine("### A-유지. 유닛별 사격 통계 · 유지비(탄약)");
        sb.AppendLine();
        sb.AppendLine("교전 테스트 모드는 탄약을 실제로 소모하지 않으므로 **유지비 = 사격 수 x weapon.ammoCost x 보급품 가중치(" + F(Weights.GetValueOrDefault("supplies", 2), "0.#") + ")** 로 환산한 추정값입니다 (탄약 쓰는 무기만; 포탑 등 무한 탄약은 0). " +
                      "'탄창 초과'는 한 전투에서 weapon.ammo(탄창)보다 많이 쏜 유닛의 비율 = 재보급이 없으면 탄약이 바닥났을 유닛. 명중률은 단일 표적 무기만 (범위 포격은 n/a). 평균 교전 거리 = 사격 시 포구~착탄(표적) 거리 평균.");
        sb.AppendLine();
        sb.AppendLine("| 유닛 | 사거리 | 최소 사거리 | 탄창 | 판수 | 유닛당 발사 | 명중률 | 평균 교전 거리 | 유닛당 유지비(자금환산) | 유지비 / 구매비 | 탄창 초과 유닛 | 효율(기본) | 효율(유지비 포함) | 시간 초과 |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        var csv = new StringBuilder("unit,games,shots_per_unit,hit_rate,avg_dist,sustain_per_unit,sustain_over_cost,dry_unit_frac,eff,eff_with_sustain,timeouts\n");
        var all = matches.SelectMany(Sides).ToList();
        foreach (var u in units)
        {
            var rows = all.Where(r => r.Me == u).ToList();
            if (rows.Count == 0) continue;
            var wp = defs.Weapon(defs.Units[u].Weapon);
            double nUnits = rows.Sum(r => r.N), shots = rows.Sum(r => r.Shots);
            int direct = rows.Sum(r => r.Direct);
            double hit = direct > 0 ? rows.Sum(r => r.Hits) / (double)direct : -1;
            double dist = shots > 0 ? rows.Sum(r => r.DistSum) / shots : -1;
            double sustain = rows.Sum(r => r.Sustain), destroyed = rows.Sum(r => r.Destroyed), lost = rows.Sum(r => r.Lost);
            double perUnit = sustain / Math.Max(1, nUnits), unitCost = CostEq(defs, defs.Units[u]);
            double dry = rows.Sum(r => r.Dry) / Math.Max(1, nUnits);
            double e1 = lost <= 0 ? (destroyed > 0 ? 99 : 1) : Math.Min(99, destroyed / lost);
            double e2 = lost + sustain <= 0 ? 1 : Math.Min(99, destroyed / (lost + sustain));
            int to = rows.Count(r => r.Timeout);
            sb.AppendLine($"| {u} | {F(wp?.Range ?? 0)} | {F(wp?.MinRange ?? 0)} | {(wp?.Ammo > 0 ? wp.Ammo.ToString() : "무한")} | {rows.Count} | {F(shots / Math.Max(1, nUnits), "0.0")} | {(hit < 0 ? "n/a" : Pct(hit))} | {(dist < 0 ? "n/a" : F(dist))} | {F(perUnit, "0.0")} | {Pct(perUnit / Math.Max(1, unitCost))} | {(wp?.Ammo > 0 ? Pct(dry) : "n/a")} | {F(e1, "0.00")} | {F(e2, "0.00")} | {to} |");
            csv.AppendLine($"{u},{rows.Count},{F(shots / Math.Max(1, nUnits), "0.00")},{(hit < 0 ? "" : F(hit, "0.000"))},{(dist < 0 ? "" : F(dist, "0.00"))},{F(perUnit, "0.00")},{F(perUnit / Math.Max(1, unitCost), "0.000")},{F(dry, "0.000")},{F(e1, "0.000")},{F(e2, "0.000")},{to}");
        }
        File.WriteAllText(Path.Combine(dir, "sustain_units.csv"), csv.ToString());
        sb.AppendLine();

        // ---- 시간 초과
        var toPairs = matches.Where(m => m.Winner == 0).GroupBy(m => (m.X, m.Y)).Select(g => (g.Key, N: g.Count(), Total: matches.Count(m => m.X == g.Key.X && m.Y == g.Key.Y))).OrderByDescending(t => t.N).ToList();
        sb.AppendLine($"### A-시간초과. 시간 제한({MatchLimitSec / 60:0}분) 초과 교전");
        sb.AppendLine();
        if (toPairs.Count == 0) sb.AppendLine("- 없음 (모든 교전이 제한 시간 안에 결판).");
        else
        {
            sb.AppendLine($"- 총 {matches.Count(m => m.Winner == 0)}판 / {matches.Count}판. 많은 쌍: " + string.Join(", ", toPairs.Take(12).Select(t => $"`{t.Key.X}` vs `{t.Key.Y}` {t.N}/{t.Total}")) + ".");
            sb.AppendLine("- 대개 한쪽이 사거리 밖에서 못 닿거나(포병 최소 사거리 · 관측 부재), 서로 못 찾는 경우입니다. 전장별 분포: " + string.Join(", ", matches.Where(m => m.Winner == 0).GroupBy(m => m.Arena).Select(g => $"{g.Key} {g.Count()}")) + ".");
        }
        sb.AppendLine();

        // ---- 포병 관측자 변형
        sb.AppendLine("### A-포병. 포병류 + 관측자(정찰병) 비교");
        sb.AppendLine();
        var arty = units.Where(IsArty).ToList();
        if (arty.Count == 0 || spot.Count == 0) { sb.AppendLine("- 포병류 교전이 없거나 관측자 변형이 실행되지 않음 (n/a)."); sb.AppendLine(); return; }
        sb.AppendLine($"포병 쪽에 정찰병 {SpotterCount}기를 예산과 별도로 추가한 변형과 기본 판(관측자 없음)의 행(포병) 승률. 최소 사거리: " + string.Join(", ", arty.Select(a => $"{a} {F(defs.Weapon(defs.Units[a].Weapon)?.MinRange ?? 0)}칸")) + " (안쪽의 적은 쏠 수 없음). 같은 쌍의 판 수가 달라 소수 표본일 수 있습니다.");
        sb.AppendLine();
        sb.AppendLine("| 포병 | 상대 | 관측자 없음 승률 | 관측자 있음 승률 | 없음 시간초과 | 있음 시간초과 | 있음 효율 | 있음 유닛당 발사 |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");
        var scsv = new StringBuilder("artillery,opponent,winrate_nospot,winrate_spot,timeouts_nospot,timeouts_spot,eff_spot\n");
        foreach (var a in arty)
            foreach (var o in units.Where(o => o != a))
            {
                var no = matches.SelectMany(Sides).Where(r => r.Me == a && r.Other == o).ToList();
                var sp = spot.SelectMany(Sides).Where(r => r.Me == a && r.Other == o).ToList();
                if (no.Count == 0 && sp.Count == 0) continue;
                double WR(List<Side> l) => l.Count == 0 ? double.NaN : l.Sum(r => r.Win ? 1 : r.Loss ? 0 : 0.5) / l.Count;
                string P(double v) => double.IsNaN(v) ? "n/a" : Pct(v);
                double d = sp.Sum(r => r.Destroyed), lo = sp.Sum(r => r.Lost);
                double eff = lo <= 0 ? (d > 0 ? 99 : 1) : Math.Min(99, d / lo);
                double spu = sp.Count == 0 ? 0 : sp.Sum(r => r.Shots) / (double)Math.Max(1, sp.Sum(r => r.N));
                sb.AppendLine($"| {a} | {o} | {P(WR(no))} | {P(WR(sp))} | {no.Count(r => r.Timeout)}/{no.Count} | {sp.Count(r => r.Timeout)}/{sp.Count} | {(sp.Count == 0 ? "n/a" : F(eff, "0.00"))} | {F(spu, "0.0")} |");
                scsv.AppendLine($"{a},{o},{(double.IsNaN(WR(no)) ? "" : F(WR(no), "0.000"))},{(double.IsNaN(WR(sp)) ? "" : F(WR(sp), "0.000"))},{no.Count(r => r.Timeout)},{sp.Count(r => r.Timeout)},{F(eff, "0.000")}");
            }
        File.WriteAllText(Path.Combine(dir, "artillery_spotters.csv"), scsv.ToString());
        sb.AppendLine();
    }

    static double Rate(Dictionary<(string, string), Cell> d, string a, string b) =>
        d.TryGetValue((a, b), out var c) ? c.Score / c.Games : 0.5;

    static void GameSections(Defs defs, StringBuilder sb, List<GameResult> games, List<MatchResult> matches, List<string> units, string dir)
    {
        sb.AppendLine("## B. AI 전체 경기");
        sb.AppendLine();
        sb.AppendLine("자원 없음 (방향 변경 §0-1) — 자원 칸은 0. 표본은 30초마다.");
        sb.AppendLine();
        var gcsv = new StringBuilder("scenario,seed,players,faction,ai,winner,end_min,limit_min,defeated," + string.Join(",", defs.Units.Keys.Select(k => "built_" + k)) + ",supplies_zero_frac,steel_zero_frac,fuel_zero_frac,funds_zero_frac,manpower_zero_frac,supplies_min,first_supplies_zero_min,max_plants,max_army,final_sites,ammo_api,ammo_zero_frac,peak_dry_frac,trips_started,trip_frac,fuel_zero_frac,shots_ammo,supplies_shot\n");
        var scsv = new StringBuilder("scenario,seed,faction,ai," + string.Join(",", SeriesColumns) + "\n");
        sb.AppendLine("### B-1. 경기 요약");
        sb.AppendLine();
        sb.AppendLine("| 시나리오 | 시드 | 승자 | 길이(분) | 불변식 위반 | 비고 |");
        sb.AppendLine("|---|---:|---|---:|---:|---|");
        foreach (var g in games.OrderBy(g => g.Scenario, StringComparer.Ordinal).ThenBy(g => g.Seed))
        {
            string winner = g.Winner > 0 ? $"P{g.Winner} ({g.Factions[g.Winner - 1].Ai})" : g.Winner < 0 ? "무승부" : $"미결 ({g.LimitMin:0}분 제한)";
            sb.AppendLine($"| {g.Scenario} | {g.Seed} | {winner} | {F(g.EndMin)} | {g.Violations.Count} | {string.Join("; ", g.Violations.Take(2))} |");
            foreach (var f in g.Factions)
            {
                var ser = f.Series;
                int ri(string col) => Array.IndexOf(SeriesColumns, col);
                double ZeroFrac(string col) { var xs = ser.Where(r => r[ri("defeated")] == 0).ToList(); return xs.Count == 0 ? 0 : xs.Count(r => r[ri(col)] < 1) / (double)xs.Count; }
                double minSup = ser.Count == 0 ? 0 : ser.Min(r => r[ri("supplies")]);
                var firstZero = ser.FirstOrDefault(r => r[ri("supplies")] < 1 && r[ri("defeated")] == 0);
                gcsv.AppendLine($"{g.Scenario},{g.Seed},{g.Players},{f.Faction},{f.Ai},{(g.Winner == f.Faction ? 1 : 0)},{F(g.EndMin)},{F(g.LimitMin, "0")},{(f.Defeated ? 1 : 0)}," +
                                string.Join(",", defs.Units.Keys.Select(k => f.Built.GetValueOrDefault(k))) +
                                $",{F(ZeroFrac("supplies"), "0.000")},{F(ZeroFrac("steel"), "0.000")},{F(ZeroFrac("fuel"), "0.000")},{F(ZeroFrac("funds"), "0.000")},{F(ZeroFrac("manpower"), "0.000")},{F(minSup, "0")}," +
                                $"{(firstZero == null ? "" : F(firstZero[0] / 60))},{(ser.Count == 0 ? 0 : ser.Max(r => r[ri("plants")]))},{(ser.Count == 0 ? 0 : ser.Max(r => r[ri("army")]))},{(ser.Count == 0 ? 0 : ser[^1][ri("sites")])}," +
                                $"{(f.AmmoApi ? 1 : 0)},{(f.AmmoUnitSamples > 0 ? F(f.AmmoZeroSamples / (double)f.AmmoUnitSamples, "0.000") : "")},{F(f.PeakDryFrac, "0.000")},{f.TripsStarted},{(f.AmmoUnitSamples > 0 ? F(f.TripSamples / (double)f.AmmoUnitSamples, "0.000") : "")},{(f.FuelUnitSamples > 0 ? F(f.FuelEmptySamples / (double)f.FuelUnitSamples, "0.000") : "")},{f.ShotsAmmo},{F(f.SuppliesShot, "0.0")}");
                foreach (var r in ser) scsv.AppendLine($"{g.Scenario},{g.Seed},{f.Faction},{f.Ai}," + string.Join(",", r.Select(v => F(v, "0.##"))));
            }
        }
        File.WriteAllText(Path.Combine(dir, "ai_games.csv"), gcsv.ToString());
        File.WriteAllText(Path.Combine(dir, "ai_series.csv"), scsv.ToString());
        sb.AppendLine();

        // 시나리오별 집계
        sb.AppendLine("### B-2. 시나리오별 집계 (세력 평균)");
        sb.AppendLine();
        var scen = games.GroupBy(g => g.Scenario).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        sb.AppendLine("| 시나리오 | 경기 | 평균 길이(분) | 승부 결정 | 생산 유닛 (세력당 평균) | 보급품 0 비율 | 강철 0 | 연료 0 | 자금 0 | 인력 0 | 보급품 최저 | 공장 최대 |");
        sb.AppendLine("|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|");
        var shortages = new List<string>();
        foreach (var grp in scen)
        {
            var fs = grp.SelectMany(g => g.Factions.Select(f => (g, f))).ToList();
            var built = defs.Units.Keys.Select(k => (k, fs.Average(x => x.f.Built.GetValueOrDefault(k)))).Where(t => t.Item2 > 0.05).ToList();
            double Zero(string col)
            {
                int ci = Array.IndexOf(SeriesColumns, col), di = Array.IndexOf(SeriesColumns, "defeated");
                var rows = fs.SelectMany(x => x.f.Series.Where(r => r[di] == 0)).ToList();
                return rows.Count == 0 ? 0 : rows.Count(r => r[ci] < 1) / (double)rows.Count;
            }
            double minSup = fs.Where(x => x.f.Series.Count > 0).Average(x => x.f.Series.Min(r => r[Array.IndexOf(SeriesColumns, "supplies")]));
            double maxPlants = fs.Where(x => x.f.Series.Count > 0).Average(x => x.f.Series.Max(r => r[Array.IndexOf(SeriesColumns, "plants")]));
            int decided = grp.Count(g => g.Winner != 0);
            sb.AppendLine($"| {grp.Key} | {grp.Count()} | {F(grp.Average(g => g.EndMin))} | {decided}/{grp.Count()} | {string.Join(", ", built.Select(t => $"{t.k} {F(t.Item2, "0.#")}"))} | {Pct(Zero("supplies"))} | {Pct(Zero("steel"))} | {Pct(Zero("fuel"))} | {Pct(Zero("funds"))} | {Pct(Zero("manpower"))} | {F(minSup, "0")} | {F(maxPlants, "0.0")} |");
            foreach (var (col, name) in new[] { ("supplies", "보급품"), ("steel", "강철"), ("fuel", "연료"), ("manpower", "인력") })
                if (Zero(col) > 0.05) shortages.Add($"`{grp.Key}`: {name} 재고 0 상태가 표본의 {Pct(Zero(col))}");
        }
        sb.AppendLine();

        // 탄약 · 재보급 · 포병
        sb.AppendLine("### B-탄약. 세력별 탄약 · 재보급 · 포병 (시나리오 평균, 세력당)");
        sb.AppendLine();
        sb.AppendLine("탄약 0 비율 = 60틱 표본에서 탄약 쓰는 유닛 중 탄약 0 인 비율. 재보급 여정 = 자동 재보급(SupplyState != 없음) 시작 횟수 (표본 근사) / 그 상태인 유닛 시간 비율. 소모 보급품 = 사격 수 x ammoCost 추정. sim 이 노출하지 않는 항목은 n/a.");
        sb.AppendLine();
        sb.AppendLine("| 시나리오 | 곡사포 생산 | 야전포 생산 | 탄약 0 비율 | 최대 탄약 0 비율 | 재보급 여정 수 | 여정 중 시간 비율 | 연료 0 비율 | 사격 수 | 소모 보급품(추정) |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var grp in scen)
        {
            var fl = grp.SelectMany(g => g.Factions).ToList();
            bool api = fl.All(f => f.AmmoApi);
            double AvgB(string k) => fl.Count == 0 ? 0 : fl.Average(f => f.Built.GetValueOrDefault(k));
            double ammoU = fl.Sum(f => f.AmmoUnitSamples), fuelU = fl.Sum(f => f.FuelUnitSamples);
            string A(Func<string> v) => api ? v() : "n/a";
            sb.AppendLine($"| {grp.Key} | {F(AvgB("heavy_howitzer"), "0.0")} | {F(AvgB("heavy_field_gun"), "0.0")} | {A(() => ammoU > 0 ? Pct(fl.Sum(f => f.AmmoZeroSamples) / ammoU) : "-")} | {A(() => Pct(fl.Max(f => f.PeakDryFrac)))} | {A(() => F(fl.Average(f => f.TripsStarted), "0.0"))} | {A(() => ammoU > 0 ? Pct(fl.Sum(f => f.TripSamples) / ammoU) : "-")} | {A(() => fuelU > 0 ? Pct(fl.Sum(f => f.FuelEmptySamples) / fuelU) : "-")} | {F(fl.Average(f => f.ShotsAmmo), "0")} | {F(fl.Average(f => f.SuppliesShot), "0")} |");
        }
        sb.AppendLine();

        // 시나리오별 재고 추이 (분 단위 평균, 생존 세력만)
        sb.AppendLine("### B-3. 자원 재고 추이 (시나리오 평균, 생존 세력, 5분마다)");
        sb.AppendLine();
        sb.AppendLine("| 시나리오 | 분 | 자금 | 강철 | 연료 | 보급품 | 인력 | 보급품 순수입/s | 인구 | 병력 | 공장(가동) | 거점 |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var grp in scen)
        {
            double maxT = grp.Max(g => g.EndMin);
            for (double mnt = 5; mnt <= maxT + 0.01; mnt += 5)
            {
                var rows = grp.SelectMany(g => g.Factions).Select(f => f.Series.FirstOrDefault(r => Math.Abs(r[0] / 60 - mnt) < 0.3)).Where(r => r != null && r[19] == 0).Cast<double[]>().ToList();
                if (rows.Count == 0) continue;
                double A(int c) => rows.Average(r => r[c]);
                sb.AppendLine($"| {grp.Key} | {mnt:0} | {F(A(1), "0")} | {F(A(2), "0")} | {F(A(3), "0")} | {F(A(4), "0")} | {F(A(5), "0")} | {F(A(6), "0.00")} | {F(A(7), "0")}/{F(A(8), "0")} | {F(A(9), "0")} | {F(A(14), "0.0")} ({F(A(15), "0.0")}) | {F(A(17), "0.0")} |");
            }
        }
        sb.AppendLine();

        // 관찰
        var obs = new List<string>();
        int totalFactions = games.Sum(g => g.Factions.Count);
        var builtTotals = defs.Units.Keys.ToDictionary(k => k, k => games.Sum(g => g.Factions.Sum(f => f.Built.GetValueOrDefault(k))));
        int allBuilt = builtTotals.Values.Sum();
        obs.Add("AI 가 만든 유닛 (전 경기 합): " + string.Join(", ", builtTotals.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value} ({Pct(kv.Value / (double)Math.Max(1, allBuilt))})")) + ".");
        foreach (var u in units.Where(u => builtTotals[u] == 0))
            obs.Add($"`{u}` 은(는) AI 경기에서 한 번도 생산되지 않음 — 동일 비용 교전에서의 종합 승률이 {(matches.Count > 0 ? "위 표를 참고" : "미측정")} (AI 가 안 쓰는 유닛은 실질적으로 '쓸모없는 유닛' 후보).");
        foreach (var s in shortages) obs.Add("자원 부족: " + s + ".");
        if (shortages.Count == 0) obs.Add("모든 시나리오에서 자원 재고 0 상태가 표본의 5% 이하 — 부족 문제 없음.");
        // 자원 병목: 20분 시점 평균 재고
        {
            var at20 = games.SelectMany(g => g.Factions).Select(f => f.Series.FirstOrDefault(r => Math.Abs(r[0] / 60 - 20) < 0.3)).Where(r => r != null && r[19] == 0).Cast<double[]>().ToList();
            if (at20.Count > 0)
            {
                double funds = at20.Average(r => r[1]);
                var others = new[] { ("강철", 2), ("연료", 3), ("보급품", 4), ("인력", 5) }.Select(t => (t.Item1, at20.Average(r => r[t.Item2]))).ToList();
                string list = $"자금 {F(funds, "0")}, " + string.Join(", ", others.Select(t => $"{t.Item1} {F(t.Item2, "0")}"));
                var idle = others.Where(t => t.Item2 > Math.Max(300, funds * 5)).Select(t => t.Item1).ToList();
                obs.Add($"20분 시점 평균 재고(생존 세력): {list}. " + (idle.Count > 0 ? $"**자금이 병목**이고 {string.Join(" · ", idle)} 은(는) 쓰지 못한 채 쌓임 — 자금 수입이 상대적으로 부족하거나 해당 자원의 사용처(지출)가 적다는 신호." : "자원 사이에 뚜렷한 병목 불균형은 없음."));
            }
        }
        // AI 종류별 승률 (혼합 시나리오)
        foreach (var grp in scen)
        {
            var kinds = grp.SelectMany(g => g.Factions.Select(f => f.Ai)).Distinct().ToList();
            if (kinds.Count < 2) continue;
            var parts = kinds.Select(k => $"{k} {grp.Count(g => g.Winner > 0 && g.Factions[g.Winner - 1].Ai == k)}승").ToList();
            obs.Add($"`{grp.Key}` AI 종류별 승수: {string.Join(", ", parts)} / {grp.Count()}판 (미결 {grp.Count(g => g.Winner == 0)}).");
        }
        var undecided = games.Count(g => g.Winner == 0);
        if (undecided > 0) obs.Add($"{undecided}/{games.Count} 경기는 제한 시간 안에 승부가 나지 않음 (교착 가능성).");
        var violations = games.SelectMany(g => g.Violations.Select(v => v.Split(':')[0])).GroupBy(v => v).Select(g => $"{g.Key} x{g.Count()}").ToList();
        obs.Add(violations.Count > 0 ? "불변식 위반: " + string.Join(", ", violations) + "." : "불변식 위반 없음 (모든 AI 경기).");
        // 눈덩이: 중반 병력 · 수입 선두가 이기는 비율
        var snow = new List<string>();
        foreach (var grp in scen)
        {
            int cases = 0, armyLeadWins = 0, siteLeadWins = 0, siteCases = 0;
            foreach (var g in grp.Where(g => g.Winner > 0 && g.Players >= 2))
            {
                double tMid = Math.Min(10 * 60, g.EndMin * 60 / 2);
                double ArmyAt(FactionGame f) { var r = f.Series.OrderBy(x => Math.Abs(x[0] - tMid)).First(); return r[9] + r[10] * 0.2; }
                double SitesAt(FactionGame f) { var r = f.Series.OrderBy(x => Math.Abs(x[0] - tMid)).First(); return r[17]; }
                var lead = g.Factions.OrderByDescending(ArmyAt).First();
                cases++;
                if (lead.Faction == g.Winner) armyLeadWins++;
                var sl = g.Factions.OrderByDescending(SitesAt).First();
                if (g.Factions.Count(f => SitesAt(f) == SitesAt(sl)) == 1) { siteCases++; if (sl.Faction == g.Winner) siteLeadWins++; }
            }
            if (cases > 0) snow.Add($"`{grp.Key}`: 중반(10분 또는 경기 절반) 병력 1위가 최종 승리 {armyLeadWins}/{cases}" + (siteCases > 0 ? $", 거점 단독 1위가 승리 {siteLeadWins}/{siteCases}" : ""));
        }
        if (snow.Count > 0) obs.Add("**눈덩이 지표** (중반 선두가 그대로 이기면 역전이 어렵다는 신호; 4인 이상은 우연 수준 25% / 10% 와 비교): " + string.Join("; ", snow) + ".");
        // 시작 위치 편향 (승리 분포)
        var slotWins = games.Where(g => g.Winner > 0).GroupBy(g => (g.Scenario, g.Winner)).Select(g => (g.Key, g.Count())).ToList();
        if (slotWins.Count > 0) obs.Add("승자 위치 분포: " + string.Join(", ", slotWins.OrderBy(s => s.Key.Scenario, StringComparer.Ordinal).ThenBy(s => s.Key.Winner).Select(s => $"{s.Key.Scenario} P{s.Key.Winner} {s.Item2}승")) + " (시드 수가 적으면 우연일 수 있음).");
        sb.AppendLine("### B-관찰");
        sb.AppendLine();
        foreach (var line in obs) sb.AppendLine("- " + line);
        sb.AppendLine();
        sb.AppendLine("## 파일");
        sb.AppendLine();
        sb.AppendLine("- `matchups.csv` 교전 원자료, `winrate_budget*.csv` / `efficiency_budget*.csv` / `ttk_budget*.csv` 행렬, `winrate_all.csv` / `efficiency_all.csv` 전체 합산, `unit_overall.csv` 유닛 종합");
        sb.AppendLine("- `ai_games.csv` 경기 · 세력별 요약, `ai_series.csv` 30초 간격 자원/병력 시계열 (열: " + string.Join(", ", SeriesColumns) + ")");
        sb.AppendLine("- `results_*.jsonl` 작업별 원자료, `jobs.txt` 작업 목록");
    }
}
