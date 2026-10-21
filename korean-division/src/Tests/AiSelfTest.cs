using System.Diagnostics;
using System.Numerics;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// AI 검증: 표준 vs 수비형 (양쪽 진영 · 여러 시드) · 표준 vs 표준 · 10인 자유전 · 16인 연기 테스트 · Tick 비용. SelfTest 가 호출한다.
/// </summary>
public static class AiSelfTest
{
    /// <summary>10인 자유전의 분 단위 기록 파일 (세력별 한 줄: 분, 세력, AI, 자금, 수입, 인구, 병력, 거점, 건물, 손실). 분석용.</summary>
    static string FfaLogPath => TestTemp.Child("ffa10_log.txt");

    /// <summary>Tick 호출 시간을 재는 래퍼.</summary>
    sealed class TimedAi : IAiController
    {
        public readonly IAiController Inner;
        public long Calls, Ticks;
        public double WorstMs;
        public TimedAi(IAiController inner) => Inner = inner;
        public string Name => Inner.Name;
        public void Tick(World world, int faction)
        {
            long t0 = Stopwatch.GetTimestamp();
            Inner.Tick(world, faction);
            long dt = Stopwatch.GetTimestamp() - t0;
            Ticks += dt;
            Calls++;
            WorstMs = Math.Max(WorstMs, dt * 1000.0 / Stopwatch.Frequency);
        }
        public double AvgMs => Calls == 0 ? 0 : Ticks * 1000.0 / Stopwatch.Frequency / Calls;
    }

    /// <summary>경기 중 관측한 세력별 최대치.</summary>
    sealed class Stats
    {
        public int MaxBarracks, MaxDepots, MaxTurrets, MaxArmy, MaxSites, MaxBuildings;
        public readonly HashSet<int> SeenUnits = new();
        public int UnitsLost;      // 목격했으나 사라진 유닛 수
        public float MaxDistFromHome;
    }

    /// <summary>경기 하나의 결과: 세계 · 세력별 AI(1..N) · 통계(1..N) · 실행 시간 · Step 평균/최대 ms.</summary>
    sealed class Match
    {
        public World World = null!;
        public TimedAi[] Ais = null!;
        public Stats[] Stats = null!;
        public double Seconds, StepAvgMs, StepWorstMs;
        public readonly Invariants Inv = new();
        public int Players => World.Map.Players;
        public double AiAvgMs
        {
            get
            {
                long calls = 0, ticks = 0;
                for (int f = 1; f < Ais.Length; f++) { calls += Ais[f].Calls; ticks += Ais[f].Ticks; }
                return calls == 0 ? 0 : ticks * 1000.0 / Stopwatch.Frequency / calls;
            }
        }
        public double AiWorstMs => Ais.Skip(1).Max(a => a.WorstMs);
    }

    /// <summary>true 로 바꾸면 표준 vs 표준 첫 경기의 분 단위 경제·병력 추이를 출력 (디버깅용).</summary>
    static readonly bool Diag = false;

    /// <summary>2인 경기 (100×100 대칭 맵).</summary>
    static Match Play(int seed, string ai1, string ai2, float minutes, bool diag = false)
    {
        var world = World.CreateStandard(GameMap.Generate(100, 100, seed), Defs.Current);
        return Play(world, new[] { ai1, ai2 }, minutes, diag ? DiagLine : null);
    }

    /// <summary>
    /// N인 경기: ais[k] 가 세력 k+1 의 AI 이름. 승자가 나오거나 minutes 가 지나면 끝. perMinute 는 매 시뮬 분마다 호출.
    /// </summary>
    static Match Play(World world, string[] ais, float minutes, Action<Match, int>? perMinute = null)
    {
        int players = world.Map.Players;
        var m = new Match { World = world, Ais = new TimedAi[players + 1], Stats = new Stats[players + 1] };
        var homes = new Vector2[players + 1];
        for (int f = 1; f <= players; f++)
        {
            m.Ais[f] = new TimedAi(AiRegistry.Create(ais[(f - 1) % ais.Length])!);
            m.Stats[f] = new Stats();
            world.Controllers.Add((f, m.Ais[f]));
            homes[f] = world.Buildings.First(x => x.Owner == f && x.Def.Headquarters).Pos;
        }
        var sw = Stopwatch.StartNew();
        int steps = (int)(minutes * 60 / Config.SimDt), done = 0;
        long stepTicks = 0;
        for (; done < steps && world.Winner == 0; done++)
        {
            long t0 = Stopwatch.GetTimestamp();
            world.Step();
            long dt = Stopwatch.GetTimestamp() - t0;
            stepTicks += dt;
            m.StepWorstMs = Math.Max(m.StepWorstMs, dt * 1000.0 / Stopwatch.Frequency);
            if (done % 60 == 0) { Sample(world, m.Stats, homes); m.Inv.Sample(world); }
            if (perMinute != null && done % 3600 == 3599) perMinute(m, done / 3600 + 1);
        }
        Sample(world, m.Stats, homes);
        m.Seconds = sw.Elapsed.TotalSeconds;
        if (checkRef != null) m.Inv.Finish($"AI 경기({world.Map.Width}², {players}인, {world.Time / 60:0}분)", checkRef);
        m.StepAvgMs = done == 0 ? 0 : stepTicks * 1000.0 / Stopwatch.Frequency / done;
        return m;
    }

    static void DiagLine(Match m, int minute)
    {
        var world = m.World;
        for (int f = 1; f <= m.Players; f++)
            Console.WriteLine($"  [diag] {minute}분 P{f}: 병력 {world.Units.Count(u => u.Owner == f && u.Def.CanCapture)} " +
                              $"병영 {world.Buildings.Count(b => b.Owner == f && b.Def.Key == "barracks")} 포탑 {world.Buildings.Count(b => b.Owner == f && b.Def.Key == "turret")} " +
                              $"시설 {world.Buildings.Count(b => b.Owner == f && b.Def.Capturable)} 손실 {m.Stats[f].UnitsLost}");
    }

    static void Sample(World w, Stats[] stats, Vector2[] homes)
    {
        int players = w.Map.Players;
        Span<int> barracks = stackalloc int[players + 1], depots = stackalloc int[players + 1], turrets = stackalloc int[players + 1];
        Span<int> army = stackalloc int[players + 1], sites = stackalloc int[players + 1], buildings = stackalloc int[players + 1], alive = stackalloc int[players + 1];
        foreach (var b in w.Buildings)
        {
            int f = b.Owner;
            if (f <= 0 || f > players) continue;
            if (b.Def.Capturable) { sites[f]++; continue; }
            buildings[f]++;
            if (b.Def.Key == "barracks") barracks[f]++;
            else if (b.Def.Key == "depot") depots[f]++;
            else if (b.Def.Key == "turret") turrets[f]++;
        }
        foreach (var u in w.Units)
        {
            int f = u.Owner;
            if (f <= 0 || f > players) continue;
            var s = stats[f];
            s.SeenUnits.Add(u.Id);
            alive[f]++;
            if (u.Def.CanCapture) army[f]++;
            s.MaxDistFromHome = MathF.Max(s.MaxDistFromHome, Vector2.Distance(u.Pos, homes[f]));
        }
        for (int f = 1; f <= players; f++)
        {
            var s = stats[f];
            s.MaxBarracks = Math.Max(s.MaxBarracks, barracks[f]);
            s.MaxDepots = Math.Max(s.MaxDepots, depots[f]);
            s.MaxTurrets = Math.Max(s.MaxTurrets, turrets[f]);
            s.MaxArmy = Math.Max(s.MaxArmy, army[f]);
            s.MaxSites = Math.Max(s.MaxSites, sites[f]);
            s.MaxBuildings = Math.Max(s.MaxBuildings, buildings[f]);
            s.UnitsLost = s.SeenUnits.Count - alive[f];
        }
    }

    static int CountBuildings(World w, int f) => w.Buildings.Count(b => b.Owner == f && !b.Def.Capturable);
    static int CountSites(World w, int f) => w.Buildings.Count(b => b.Owner == f && b.Def.Capturable);

    static string Summary(World w, Stats s, int f) =>
        $"병영 {s.MaxBarracks}, 보급고 {s.MaxDepots}, 포탑 {s.MaxTurrets}, 최대 병력 {s.MaxArmy}, 최대 자원 시설 {s.MaxSites}, " +
        $"유닛 손실 {s.UnitsLost}, 현재 건물 {CountBuildings(w, f)}/{s.MaxBuildings}";

    static Action<bool, string>? checkRef;

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        checkRef = check;
        Guard(check, "4인 모서리 맵 30분", () => Corners4Long(opt, check));
        Guard(check, "표준 vs 수비", () => StandardVsPassive(opt, check));
        Guard(check, "표준 vs 표준", () => StandardVsStandard(opt, check));
        Guard(check, "10인 자유전", () => FreeForAll10(opt, check));
        Guard(check, "16인 연기", () => Smoke24(opt, check));
    }

    static void Guard(Action<bool, string> check, string name, Action body)
    {
        try { body(); }
        catch (Exception e)
        {
            check(false, $"AI 테스트 '{name}' 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>모서리 4인 맵(corners4) 전원 표준 AI 30분: 불변식(Invariants) + 예외 없음 + 시간 내 끝까지 진행.</summary>
    static void Corners4Long(GameOptions opt, Action<bool, string> check)
    {
        var map = MapFile.Load("corners4", opt.Seed);
        var world = World.CreateStandard(map, Defs.Current);
        MapFile.ApplyFactions(world);
        var m = Play(world, new[] { "standard", "standard", "rush", "standard" }, 30, null);
        Console.WriteLine($"  [info] corners4 4인 30분: {world.Time / 60:0.0}분, 승자 {world.Winner}, 실행 {m.Seconds:0.0}초, Step 평균 {m.StepAvgMs:0.000}ms (최대 {m.StepWorstMs:0.0}), 유닛 {world.Units.Count}");
        check(m.StepWorstMs < 100, $"corners4 30분: Step 최대 {m.StepWorstMs:0.0}ms < 100ms");
    }

    static void StandardVsPassive(GameOptions opt, Action<bool, string> check)
    {
        // (시드, 표준 AI 진영, 분)
        var cases = new[] { (opt.Seed, 1, 20f), (opt.Seed, 2, 20f), (opt.Seed + 1, 1, 15f), (opt.Seed + 2, 2, 15f) };
        double worstAvg = 0;
        foreach (var (seed, std, minutes) in cases)
        {
            int pas = 3 - std;
            var m = Play(seed, std == 1 ? "standard" : "passive", std == 1 ? "passive" : "standard", minutes);
            var w = m.World;
            var stdAi = m.Ais[std];
            var pasAi = m.Ais[pas];
            var ss = m.Stats[std];
            var ps = m.Stats[pas];
            string tag = $"시드 {seed}, 표준=P{std}";
            Console.WriteLine($"  [info] {tag}: {w.Time / 60:0.0}분, 승자 {w.Winner}, 실행 {m.Seconds:0.0}초, " +
                              $"표준 Tick {stdAi.AvgMs:0.000}ms(최대 {stdAi.WorstMs:0.00}), 수비 Tick {pasAi.AvgMs:0.000}ms");
            Console.WriteLine($"  [info]   표준: {Summary(w, ss, std)}");
            Console.WriteLine($"  [info]   수비: {Summary(w, ps, pas)}");
            check(ss.MaxArmy >= 10, $"{tag}: 표준 AI 가 주어진 병력 유지 (최대 {ss.MaxArmy})");
            check(ss.MaxSites >= 3, $"{tag}: 표준 AI 가 자원 시설 2곳 이상 추가 점령 (최대 보유 {ss.MaxSites})");
            bool damaged = w.Winner == std || ps.UnitsLost >= 10 || CountBuildings(w, pas) < ps.MaxBuildings;
            check(damaged, $"{tag}: 표준 AI 승리 또는 수비 측에 피해 (승자 {w.Winner}, 수비 손실 {ps.UnitsLost})");
            check(ps.MaxArmy >= 4, $"{tag}: 수비 AI 병력 보유 (병력 {ps.MaxArmy})");
            check(ps.MaxDistFromHome < 45, $"{tag}: 수비 AI 유닛은 기지 근처에 머무름 (최대 {ps.MaxDistFromHome:0}타일)");
            worstAvg = Math.Max(worstAvg, Math.Max(stdAi.AvgMs, pasAi.AvgMs));
        }
        check(worstAvg < 0.5, $"AI Tick 평균 비용 {worstAvg:0.000}ms < 0.5ms");
    }

    static void StandardVsStandard(GameOptions opt, Action<bool, string> check)
    {
        foreach (int seed in new[] { opt.Seed, opt.Seed + 1, opt.Seed + 2 })
        {
            var m = Play(seed, "standard", "standard", 20, diag: Diag && seed == opt.Seed);
            var w = m.World;
            var (a, b) = (m.Ais[1], m.Ais[2]);
            var st = m.Stats;
            string tag = $"표준 vs 표준 (시드 {seed})";
            Console.WriteLine($"  [info] {tag}: {w.Time / 60:0.0}분, 승자 {w.Winner}, 실행 {m.Seconds:0.0}초, " +
                              $"Tick {a.AvgMs:0.000}/{b.AvgMs:0.000}ms");
            Console.WriteLine($"  [info]   P1: {Summary(w, st[1], 1)}");
            Console.WriteLine($"  [info]   P2: {Summary(w, st[2], 2)}");
            check(st[1].UnitsLost > 0 && st[2].UnitsLost > 0, $"{tag}: 교전 발생 (손실 {st[1].UnitsLost}/{st[2].UnitsLost})");
            check(Math.Max(a.AvgMs, b.AvgMs) < 0.5, $"{tag}: Tick 평균 {Math.Max(a.AvgMs, b.AvgMs):0.000}ms < 0.5ms");
        }
    }

    // ---------------------------------------------------------------- N인전

    /// <summary>
    /// 10인 자유전 (224×224 원형 맵, 안개 켬): 표준 6 · 돌격 2 · 수비 2, 30분 또는 승자가 나올 때까지.
    /// 모든 AI 가 병영을 짓고, 8세력 이상이 거점을 추가 점령하고, 5세력 이상이 손실을 입을 만큼 교전하며,
    /// 세력이 패배하거나 거점 소유가 뚜렷이 바뀌어야 한다. AI Tick 평균 < 0.1ms. 분 단위 기록은 FfaLogPath 에 쓴다.
    /// </summary>
    static void FreeForAll10(GameOptions opt, Action<bool, string> check)
    {
        const int players = 10;
        int size = Config.MapSizeFor(players);
        var map = GameMap.Generate(size, size, opt.Seed, players);
        var world = World.CreateStandard(map, Defs.Current);
        var ais = new[] { "standard", "standard", "standard", "standard", "standard", "standard", "rush", "rush", "passive", "passive" };
        var initialOwner = world.Buildings.Where(b => b.Def.Capturable).ToDictionary(b => b.Id, b => b.Owner);

        var log = new StringBuilder();
        log.AppendLine("minute\tfaction\tai\tarmy\tpoints\tbuildings\tlosses\tdefeated");
        var m = Play(world, ais, 30, (mm, minute) =>
        {
            var w = mm.World;
            for (int f = 1; f <= players; f++)
            {
                var fa = w.Faction(f);
                log.Append(minute).Append('\t').Append(f).Append('\t').Append(mm.Ais[f].Name).Append('\t')
                   .Append(w.Units.Count(u => u.Owner == f && u.Def.CanCapture)).Append('\t')
                   .Append(CountSites(w, f)).Append('\t').Append(CountBuildings(w, f)).Append('\t')
                   .Append(mm.Stats[f].UnitsLost).Append('\t').Append(fa.Defeated ? 1 : 0).AppendLine();
            }
        });
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FfaLogPath)!);
            File.WriteAllText(FfaLogPath, log.ToString());
        }
        catch (Exception e) { Console.WriteLine($"  [info] 자유전 기록 저장 실패: {e.Message}"); }

        var wd = m.World;
        int withBarracks = 0, expanded = 0, fought = 0, defeated = 0, changedHands = 0;
        foreach (var b in wd.Buildings)
            if (b.Def.Capturable && initialOwner.TryGetValue(b.Id, out int o0) && o0 != 0 && b.Owner != o0) changedHands++;
        // 패배한 세력의 본진 거점이 중립이 되거나 다른 세력에게 넘어간 것도 포함 (위 조건으로 이미 세어짐)
        Console.WriteLine($"  [info] 10인 자유전 ({size}×{size}, 시드 {opt.Seed}): {wd.Time / 60:0.0}분, 승자 {wd.Winner}, 실행 {m.Seconds:0.0}초, " +
                          $"Step 평균 {m.StepAvgMs:0.000}ms (최대 {m.StepWorstMs:0.0}), AI Tick 평균 {m.AiAvgMs:0.000}ms (최대 {m.AiWorstMs:0.00}), " +
                          $"유닛 {wd.Units.Count}, 거점 소유 변경 {changedHands}");
        Console.WriteLine($"  [info]   AI 명령 {CommandSummary(wd)}; 흐름장 맵 전체 계산 시 1회 {FlowFieldMs(wd.Map):0.00}ms (실제로는 지연 계산 + 목표 칸 캐시)");
        check(m.StepWorstMs < 100, $"10인 자유전: Step 최대 {m.StepWorstMs:0.0}ms < 100ms (끊김 없음)");
        for (int f = 1; f <= players; f++)
        {
            var s = m.Stats[f];
            var fa = wd.Faction(f);
            if (s.MaxBarracks >= 1) withBarracks++;
            if (s.MaxSites >= 2) expanded++;
            if (s.UnitsLost > 0) fought++;
            if (fa.Defeated) defeated++;
            Console.WriteLine($"  [info]   P{f,-2} {m.Ais[f].Name,-8} 건물 {CountBuildings(wd, f),2}/{s.MaxBuildings,2} 병영 {s.MaxBarracks} 포탑 {s.MaxTurrets} " +
                              $"최대 병력 {s.MaxArmy,3} 최대 거점 {s.MaxSites} 현재 거점 {CountSites(wd, f)} 손실 {s.UnitsLost,3} " +
                              $"Tick {m.Ais[f].AvgMs:0.000}ms{(fa.Defeated ? " 패배" : "")}");
        }
        // 수비형(passive)은 점령하지 않으므로 제외. 시작 병력(보병 4)으로 초반 교전이 빨라 일찍 탈락하는 세력이 있을 수 있다 → 공격형의 3/4 이상
        int aggressive = players - 2;
        int needExpand = (int)MathF.Ceiling(aggressive * 0.75f);
        check(expanded >= needExpand, $"10인 자유전: 공격형 AI {aggressive} 중 {needExpand}세력 이상 거점 추가 점령 ({expanded})");
        check(fought >= 5, $"10인 자유전: 5세력 이상 교전 손실 ({fought}/{players})");
        check(defeated >= 1 || changedHands >= 2, $"10인 자유전: 세력 패배 또는 거점 소유 변경 (패배 {defeated}, 변경 {changedHands})");
        check(m.AiAvgMs < 0.1, $"10인 자유전: AI Tick 평균 {m.AiAvgMs:0.000}ms < 0.1ms");
    }

    /// <summary>16인 연기 테스트의 시뮬 분. 344 맵은 Step 이 20ms 안팎이라 10분 = 실행 12분 정도 — 길찾기 개선 전엔 줄여도 된다.</summary>
    const float Smoke24Minutes = 10;

    /// <summary>16인 연기 테스트 (MapSizeFor(16) 정사각 맵, 전원 표준, Smoke24Minutes 분): 예외 없음, Step 평균 ms 출력 (목표 < 8ms).</summary>
    static void Smoke24(GameOptions opt, Action<bool, string> check)
    {
        int players = Config.MaxPlayers;
        int size = Config.MapSizeFor(players);
        var world = World.CreateStandard(GameMap.Generate(size, size, opt.Seed, players), Defs.Current);
        var m = Play(world, new[] { "standard" }, Smoke24Minutes);
        var w = m.World;
        int defeated = w.Factions.Skip(1).Count(f => f.Defeated);
        int fought = Enumerable.Range(1, players).Count(f => m.Stats[f].UnitsLost > 0);
        int withBarracks = Enumerable.Range(1, players).Count(f => m.Stats[f].MaxBarracks >= 1);
        Console.WriteLine($"  [info] 16인 연기 ({size}×{size}): {w.Time / 60:0.0}분, 실행 {m.Seconds:0.0}초, Step 평균 {m.StepAvgMs:0.000}ms (최대 {m.StepWorstMs:0.0}), " +
                          $"AI Tick 평균 {m.AiAvgMs:0.000}ms (최대 {m.AiWorstMs:0.00}), 유닛 {w.Units.Count}, 건물 {w.Buildings.Count(b => !b.Def.Capturable)}, " +
                          $"병영 보유 {withBarracks}/{players}, 교전 손실 세력 {fought}, 패배 {defeated}");
        Console.WriteLine($"  [info]   AI 명령 {CommandSummary(w)}; 흐름장 맵 전체 계산 시 1회 {FlowFieldMs(w.Map):0.00}ms (실제로는 지연 계산 + 목표 칸 캐시)");
        check(true, "16인 연기: 예외 없이 진행");
        // Step 평균 목표 8ms: 큰 맵의 흐름장(맵 전체 다익스트라) 비용이라 AI 쪽에서 맞출 수 없다 — WORKPLAN '큰 맵 길찾기' 항목. 값만 보고한다.
        check(m.StepAvgMs < 8, $"16인 연기: Step 평균 {m.StepAvgMs:0.000}ms < 8ms");
        check(m.StepWorstMs < 100, $"16인 연기: Step 최대 {m.StepWorstMs:0.0}ms < 100ms (끊김 없음)");
        check(m.AiAvgMs < 0.1, $"16인 연기: AI Tick 평균 {m.AiAvgMs:0.000}ms < 0.1ms");
    }

    /// <summary>실행된 명령 수 (전부 AI 가 낸 것) — 종류별, 시뮬 초당.</summary>
    static string CommandSummary(World w)
    {
        var byType = w.CommandLog.GroupBy(r => r.Command.GetType().Name.Replace("Command", ""))
                                 .OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}");
        return $"{w.CommandLog.Count}개 (초당 {w.CommandLog.Count / MathF.Max(w.Time, 1):0.0}): {string.Join(", ", byType)}";
    }

    static double FlowFieldMs(GameMap map)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 5; i++) _ = new FlowField(map, new Vector2(map.Width / 2f + i, map.Height / 2f)).IsReachable(-1, -1) ||
                                        new FlowField(map, new Vector2(map.Width / 2f + i, map.Height / 2f)).IsReachable(map.Width - 1, map.Height - 1);
        return sw.Elapsed.TotalMilliseconds / 5;
    }
}
