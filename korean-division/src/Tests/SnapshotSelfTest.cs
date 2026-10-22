using System.Diagnostics;
using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 저장 / 불러오기 검증 (창 없이). 핵심 기준 = 동등성:
///  같은 시나리오를 (P) 한 번도 저장하지 않고 끝까지 돌린 것, (X) 중간에 저장하고 계속 돌린 것, (Y) 그 저장 파일을 새 World 로 불러와 이어 돌린 것이
///  100틱마다 체크섬이 모두 같고, 중간 · 끝 시점의 본문 전체(길찾기 캐시 · 이동 그룹 · AI 내부 상태 · 안개 · 외교 기록 포함)가 바이트 단위로 같아야 한다.
/// 체크섬은 상태의 일부만 보므로 (안개 격자 · 길찾기 캐시 · AI 상태는 제외) 본문 비교가 진짜 기준이다.
/// </summary>
public static class SnapshotSelfTest
{
    sealed record Scn(string Name, Func<World> Build, int Pre, int Post, Action<World, long>? Script, bool Pure = true, bool ChildProcess = false);

    static Defs Defs => KoreanDivision.Core.Defs.Current;
    static readonly SnapshotMeta Meta = new("test", 1, false, "standard");

    // ---------------------------------------------------------------- 시나리오

    static World AiWorld(GameMap map, string[] ais, bool fog = true, int human = 0)
    {
        var w = World.CreateStandard(map, Defs);
        MapFile.ApplyFactions(w);
        w.Fog.Enabled = fog;
        for (int f = 1; f <= map.Players; f++)
        {
            if (f == human) continue;
            var ai = AiRegistry.Create(ais[(f - 1) % ais.Length])!;
            w.Controllers.Add((f, ai));
            w.Faction(f).IsAi = true;
        }
        return w;
    }

    internal static World ProbeGen(int players) => Gen(Config.MapSizeFor(players), players, players == 2 ? new[] { "standard" } : new[] { "standard", "rush", "passive" });

    static World Gen(int size, int players, string[] ais, int human = 0, int seed = 20261009) =>
        AiWorld(GameMap.Generate(size, size, seed, players), ais, true, human);

    static World Battle(string a, string b, string map, int seed, string ai = "battle")
    {
        var setup = new BattleSetup { SideA = a, SideB = b, Map = map, Distance = 40, AiVsAi = true, AiA = ai, AiB = ai };
        var w = setup.CreateWorld(Defs, seed);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, AiRegistry.Create(ai)!));
        w.Controllers.Add((2, AiRegistry.Create(ai)!));
        w.Faction(1).IsAi = w.Faction(2).IsAi = true;
        return w;
    }

    static Building HqOf(World w, int f) => w.Buildings.First(b => b.Owner == f && b.Def.Headquarters);

    /// <summary>사람이 하는 법한 조작 모음 (세력 1): 진형 이동 · 사수 · 어택땅 · 참호 · 점령 · 외교. 저장 시점(Pre)에도 명령이 대기 중이도록 Pre 틱에 조작이 있다.</summary>
    static Action<World, long> Human(int pre)
    {
        return (w, t) =>
        {
            var hq = HqOf(w, 1);
            var mine = w.Units.Where(u => u.Owner == 1 && u.Alive).ToList();
            var fighters = mine.Where(u => u.Weapon != null).ToList();
            Vector2 enemy = AiHelpers.StartOf(w, 2);
            Vector2 mid = new(w.Map.Width / 2f, w.Map.Height / 2f);
            if (t == 40)
            {
                w.Issue(new DebugSpawnCommand(1, "infantry", hq.Pos + new Vector2(0, 7), 45));
                w.Issue(new DebugSpawnCommand(1, "tank_ft_male", hq.Pos + new Vector2(6, 7), 8));
                w.Issue(new DebugSpawnCommand(1, "heavy_at_rifleman", hq.Pos + new Vector2(-6, 7), 8));
            }
            else if (t == 120)
            {
                // 참호 자동 점유: 앞쪽에 참호선 9칸을 그리고 12명을 그리로 (가는 중 · 들어간 상태가 저장에 걸치게)
                var grp = fighters.Skip(60).Take(12).ToList();
                if (grp.Count > 0)
                {
                    var c0 = grp[0].Pos + new Vector2(4, 0);
                    for (int k = -4; k <= 4; k++)
                    {
                        var (tx, ty) = GameMap.TileOf(c0 + new Vector2(0, k));
                        if (w.Map.InBounds(tx, ty) && w.Map.Get(tx, ty) is TerrainType.Grass or TerrainType.Dirt or TerrainType.Sand) w.MutateTerrain(tx, ty, TerrainType.Trench);
                    }
                    w.Move(1, grp, c0, new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, new Vector2(1, 0)));
                }
            }
            else if (t == 160)
            {
                w.Move(1, fighters.Take(40), mid, new FormSpec((int)FormType.Wedge, (int)FormOrder.ArmorFront, Vector2.Zero));
                w.Move(1, fighters.Skip(40), mid + new Vector2(6, 0), new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, Vector2.Zero));
            }
            else if (t == 260)
            {
                w.QuickStep(1, fighters.Take(10), hq.Pos + new Vector2(14, 14));
                w.Hold(1, fighters.Skip(10).Take(5));
            }
            else if (t == 420)
            {
                var site = w.Buildings.Where(b => b.Def.Capturable && b.Owner == 0).OrderBy(b => Vector2.DistanceSquared(b.Pos, hq.Pos)).FirstOrDefault();
                if (site != null) w.AttackMove(1, fighters.Skip(20).Take(14), site.Pos);
            }
            else if (t == 520) w.Stop(1, fighters.Take(3));
            else if (t == 600)
            {
                w.Hold(1, fighters.Skip(60).Take(6));   // 참호 안 사수 유지 · 가는 중 사수
            }
            else if (t == 700)
            {
                w.AttackMove(1, fighters, enemy, new FormSpec((int)FormType.Box, (int)FormOrder.InfantryFront, Vector2.Zero));
            }
            else if (t == 900)
            {
                var tgt = w.Units.FirstOrDefault(u => u.Owner == 2 && u.Alive);
                if (tgt != null) w.Attack(1, fighters.Take(30), tgt);   // 강제 지정 공격 (추격)
            }
            else if (t == pre)
            {
                // 저장 직전: 명령 몇 개를 막 넣었다 (대기열에 남은 채 저장)
                w.Move(1, fighters.Take(25), mid + new Vector2(-8, 4), new FormSpec((int)FormType.Column, (int)FormOrder.Mixed, Vector2.Zero));
                w.QuickStep(1, fighters.Skip(25).Take(6), hq.Pos + new Vector2(-12, 6));
            }
            else if (t == pre + 150) w.AttackMove(1, fighters.Take(50), enemy);
            else if (t == pre + 400) w.Hold(1, fighters.Skip(50).Take(10));
        };
    }

    static Action<World, long> BattleScript(int pre) => (w, t) =>
    {
        var a = w.Units.Where(u => u.Owner == 1 && u.Alive).ToList();
        if (t == 90) w.Move(1, a.Where((_, i) => i % 3 == 0), new Vector2(w.Map.Width / 2f - 10, w.Map.Height / 2f), new FormSpec((int)FormType.Line, (int)FormOrder.ArmorFront, Vector2.Zero));
        if (t == pre) w.Hold(1, a.Where((_, i) => i % 7 == 0));
        if (t == pre + 100) w.AttackMove(1, a.Where((_, i) => i % 5 == 0), new Vector2(w.Map.Width / 2f + 20, w.Map.Height / 2f));
    };

    const string TacMix = "infantry:150,officer:4,heavy_machinegunner:8,heavy_at_rifleman:4,cavalry_default:20,heavy_howitzer:2,supply_wagon:2,scout:3";

    static List<Scn> Scenarios(bool quick) => new()
    {
        new("2인 생성맵: 사람(진형 · 추격 · 건설 · 점령) + 표준 AI", () => Gen(100, 2, new[] { "standard" }, human: 1), 3000, 3000, Human(3000)),
        new("corners4 4인: 표준 + 러시 AI", () => AiWorld(MapFile.Load("corners4", 7), new[] { "standard", "rush" }), quick ? 4800 : 9000, quick ? 2400 : 5400, null, ChildProcess: true),
        new("corners4 대군 혼전 (세력당 +400기)", () => Mass(), 1500, 2400, MassScript()),
        new("10인 224²: 표준 · 러시 · 수비 AI (점령)", () => Gen(Config.MapSizeFor(10), 10, new[] { "standard", "rush", "passive" }), quick ? 3600 : 7200, quick ? 1200 : 3000, null, Pure: false),
        new("16인: 표준 AI", () => Gen(Config.MapSizeFor(16), 16, new[] { "standard" }), quick ? 3600 : 7200, quick ? 1200 : 2400, null, Pure: false),
        new("교전 500 대 500 (BattleAi, 진형)", () => Battle("recruit:150,infantry:150,veteran:80,heavy_at_rifleman:60,tank_ft_male:60", "recruit:150,infantry:150,veteran:80,heavy_at_rifleman:60,tank_ft_male:60", "terrain", 11), 900, 1200, BattleScript(900)),
        new("교전 소규모 승패까지 (전멸 · 패배 처리)", () => Battle("infantry:40,tank_ft_male:3", "infantry:30", "flat", 5), 500, 2600, null),
        new("corners4 4인: 전술 + 표준 AI (중대 · 대대 과제)", () => AiWorld(MapFile.Load("corners4", 13), new[] { "tactical", "standard" }), quick ? 3600 : 6000, quick ? 1800 : 3000, null),
        new("교전 전술 AI 혼성 (기관총 · 기병 · 곡사포 · 장교)", () => Battle(TacMix, TacMix, "terrain", 17, "tactical"), 900, 1500, null),
        new("교전 전술 AI 편성 직후 (FormCompany 대기 · 중대 확인 전)", () => Battle(TacMix, "infantry:120,officer:3", "flat", 19, "tactical"), 12, 1200, null),
        new("corners4 시작 직후 (틱 0 · AI 상태 미초기화)", () => AiWorld(MapFile.Load("corners4", 9), new[] { "standard" }), 0, 2400, null),
        new("원시 월드 (길찾기 그래프 아직 없음)", RawWorld, 0, 900, RawScript()),
    };

    /// <summary>corners4 에 각 세력 400기를 더 풀어 큰 부대끼리 부딪치게 한다 (그룹 · 진형 · 길찾기 캐시를 많이 채움).</summary>
    static World Mass() => AiWorld(MapFile.Load("corners4", 21), new[] { "standard" });

    static Action<World, long> MassScript() => (w, t) =>
    {
        if (t == 30)
            for (int f = 1; f <= 4; f++)
            {
                var hq = HqOf(w, f);
                Vector2 toward = Vector2.Normalize(new Vector2(w.Map.Width / 2f, w.Map.Height / 2f) - hq.Pos);
                w.Issue(new DebugSpawnCommand(f, "infantry", hq.Pos + toward * 12, 200));
                w.Issue(new DebugSpawnCommand(f, "recruit", hq.Pos + toward * 14 + new Vector2(0, 5), 120));
                w.Issue(new DebugSpawnCommand(f, "tank_ft_male", hq.Pos + toward * 10 + new Vector2(4, 0), 40));
                w.Issue(new DebugSpawnCommand(f, "heavy_at_rifleman", hq.Pos + toward * 10 - new Vector2(4, 0), 40));
            }
        if (t == 200)
            for (int f = 1; f <= 4; f++)
            {
                int enemy = f % 4 + 1;
                var mine = w.Units.Where(u => u.Owner == f && u.Weapon != null).ToList();
                var form = f % 2 == 0 ? new FormSpec((int)FormType.Wedge, (int)FormOrder.ArmorFront, Vector2.Zero) : null;
                w.AttackMove(f, mine.Take(mine.Count / 2), AiHelpers.StartOf(w, enemy), form);
                w.Move(f, mine.Skip(mine.Count / 2), new Vector2(w.Map.Width / 2f, w.Map.Height / 2f), new FormSpec((int)FormType.Box, (int)FormOrder.Mixed, Vector2.Zero));
            }
        if (t == 1500)   // 저장 시점의 대기 명령
            for (int f = 1; f <= 4; f++)
                w.AttackMove(f, w.Units.Where(u => u.Owner == f && u.Weapon != null && u.Order == UnitOrder.Idle).Take(80), AiHelpers.StartOf(w, f % 4 + 1));
    };

    /// <summary>CreateStandard 를 거치지 않은 월드: 길찾기 그래프(Nav)가 아직 만들어지지 않은 채 저장된다 (불러온 뒤 첫 이동에서 만들어져도 같아야 한다).</summary>
    static World RawWorld()
    {
        var map = GameMap.Generate(64, 64, 5, 2);
        var w = new World(map, Defs, 2);
        w.Fog.Enabled = false;
        for (int i = 0; i < 12; i++)
        {
            w.SpawnUnit(Defs.Units["infantry"], 1, new Vector2(10 + i % 4, 10 + i / 4));
            w.SpawnUnit(Defs.Units["infantry"], 2, new Vector2(50 - i % 4, 50 - i / 4));
        }
        w.SpawnUnit(Defs.Units["tank_ft_male"], 1, new Vector2(14, 16));
        return w;
    }

    static Action<World, long> RawScript() => (w, t) =>
    {
        if (t == 0) w.AttackMove(1, w.Units.Where(u => u.Owner == 1), new Vector2(50, 50), new FormSpec((int)FormType.Wedge, (int)FormOrder.Mixed, Vector2.Zero));
        if (t == 200) w.AttackMove(2, w.Units.Where(u => u.Owner == 2), new Vector2(12, 12));
    };

    // ---------------------------------------------------------------- 실행

    static void Advance(World w, long to, Action<World, long>? script, Dictionary<long, ulong> sums, long scriptFrom)
    {
        while (w.Tick < to)
        {
            if (w.Tick >= scriptFrom) script?.Invoke(w, w.Tick);
            w.Step();
            if (w.Tick % 100 == 0) sums[w.Tick] = w.ComputeChecksum();
        }
    }

    /// <summary>본문의 offset 바이트가 어느 구역(태그)에 속하는지 — 태그를 순서대로 찾아 가늠한다 (진단용).</summary>
    static string SectionAt(byte[] body, int offset)
    {
        string[] tags = { "MAP_", "WRLD", "CMDS", "ENTS", "TOMB", "ROUT", "GRPS", "LINK", "NAV_", "FOG_", "AIS_", "SUPL", "CMB2", "CMDX", "END_" };
        string cur = "?"; int pos = 0;
        foreach (var t in tags)
        {
            int at = -1;
            for (int i = pos; i + 4 <= body.Length; i++)
                if (body[i] == t[0] && body[i + 1] == t[1] && body[i + 2] == t[2] && body[i + 3] == t[3]) { at = i; break; }
            if (at < 0 || at > offset) break;
            cur = t; pos = at + 4;
        }
        return cur;
    }

    static bool SameBytes(byte[] a, byte[] b, out int firstDiff)
    {
        firstDiff = -1;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++) if (a[i] != b[i]) { firstDiff = i; return false; }
        if (a.Length != b.Length) { firstDiff = n; return false; }
        return true;
    }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            bool quick = Environment.GetEnvironmentVariable("KD_SNAP_QUICK") == "1";
            World.DebugBreak = Environment.GetEnvironmentVariable("KD_SNAP_BREAK");   // 음성 대조 (개발용)
            string only = Environment.GetEnvironmentVariable("KD_SNAP_ONLY") ?? "";
            foreach (var s in Scenarios(quick))
                if (only == "" || s.Name.Contains(only)) Equivalence(s, check);
            Audit(check);
            Format(check);
            Corruption(check);
            Policy(check);
            ReplayFromSnapshot(check);
        }
        catch (Exception e)
        {
            check(false, $"스냅샷 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>다른 테스트 묶음이 자기 시나리오로 동등성 검사(P/X/Y 체크섬 · 본문 바이트 비교)를 돌릴 때 (SupplySelfTest 등).</summary>
    public static void Equivalence(string name, Func<World> build, int pre, int post, Action<World, long>? script, Action<bool, string> check) =>
        Equivalence(new Scn(name, build, pre, post, script), check);

    static void Equivalence(Scn s, Action<bool, string> check)
    {
        var sw = Stopwatch.StartNew();
        long N = s.Pre, end = s.Pre + s.Post, mid = s.Pre + s.Post / 2;
        mid -= mid % 100;

        // P: 저장 없이 끝까지
        var pSums = new Dictionary<long, ulong>();
        if (s.Pure)
        {
            var p = s.Build();
            Advance(p, end, s.Script, pSums, 0);
        }
        double tP = sw.Elapsed.TotalSeconds;

        // X: 저장 지점까지 돌리고 (그 틱의 조작까지 넣은 뒤) 저장, 이어서 계속
        var xSums = new Dictionary<long, ulong>();
        var x = s.Build();
        Advance(x, N, s.Script, xSums, 0);
        s.Script?.Invoke(x, N);   // 저장 시점의 조작 = 대기열에 남은 명령
        var swSave = Stopwatch.StartNew();
        byte[] bytesN = Snapshot.Save(x, Meta);
        double saveMs = swSave.Elapsed.TotalMilliseconds;
        string path = TestTemp.Child($"kd_snaptest_{Math.Abs(s.Name.GetHashCode())}.kds");
        File.WriteAllBytes(path, bytesN);
        string stats = Coverage(x);
        Advance(x, mid, s.Script, xSums, N + 1);
        byte[] xMid = Snapshot.RawBody(Snapshot.Save(x, Meta));
        Advance(x, end, s.Script, xSums, N + 1);
        byte[] xEnd = Snapshot.RawBody(Snapshot.Save(x, Meta));
        string endInfo = $"끝(틱 {x.Tick}): 유닛 {x.Units.Count}, 승자 {x.Winner}, 패배 세력 {x.Factions.Count(f => f.Defeated)}";
        double tX = sw.Elapsed.TotalSeconds - tP;

        // Y: 파일에서 새 World 로 불러와 이어서
        var swLoad = Stopwatch.StartNew();
        var loaded = Snapshot.LoadFile(path, Defs);
        double loadMs = swLoad.Elapsed.TotalMilliseconds;
        var y = loaded.World;
        var ySums = new Dictionary<long, ulong>();
        bool pendingOk = y.Tick == N;
        Advance(y, mid, s.Script, ySums, N + 1);
        byte[] yMid = Snapshot.RawBody(Snapshot.Save(y, Meta));
        Advance(y, end, s.Script, ySums, N + 1);
        byte[] yEnd = Snapshot.RawBody(Snapshot.Save(y, Meta));
        double tY = sw.Elapsed.TotalSeconds - tP - tX;

        // 저장 → 불러오기 → 저장 = 같은 본문
        var again = Snapshot.RawBody(Snapshot.Save(Snapshot.Load(bytesN, Defs).World, Meta));
        bool roundTrip = SameBytes(Snapshot.RawBody(bytesN), again, out int rt);

        // 비교
        int cmp = 0, bad = 0;
        long firstBad = -1;
        foreach (var (t, sum) in ySums)
        {
            cmp++;
            if (!xSums.TryGetValue(t, out var xs) || xs != sum || (s.Pure && (!pSums.TryGetValue(t, out var ps) || ps != sum))) { bad++; if (firstBad < 0) firstBad = t; }
        }
        bool pureEqX = true;
        if (s.Pure) foreach (var (t, sum) in pSums) if (!xSums.TryGetValue(t, out var xs2) || xs2 != sum) { pureEqX = false; break; }
        bool midEq = SameBytes(xMid, yMid, out int dm), endEq = SameBytes(xEnd, yEnd, out int de);

        check(pendingOk && cmp > 0 && bad == 0, $"[{s.Name}] 불러온 월드를 {s.Post}틱 이어 돌림: 100틱 체크섬 {cmp}곳 모두 일치" + (bad > 0 ? $" (첫 불일치 틱 {firstBad})" : ""));
        if (s.Pure) check(pureEqX, $"[{s.Name}] 중간에 저장해도 진행은 그대로 (저장 없이 돌린 것과 체크섬 동일)");
        check(midEq && endEq, $"[{s.Name}] 본문 전체 바이트 동일 (중간 {xMid.Length / 1024}KB · 끝 {xEnd.Length / 1024}KB){(midEq ? "" : $" — 중간 {dm}번째 바이트부터 다름 ({SectionAt(xMid, dm)} 구역)")}{(endEq ? "" : $" — 끝 {de}번째 바이트부터 다름 ({SectionAt(xEnd, de)} 구역)")}");
        check(roundTrip, $"[{s.Name}] 저장 → 불러오기 → 저장 = 같은 본문{(roundTrip ? "" : $" (첫 차이 {rt})")}");
        Console.WriteLine($"    {s.Name}: {stats} → {endInfo}; 파일 {bytesN.Length / 1024}KB, 저장 {saveMs:0}ms, 불러오기 {loadMs:0}ms ({tP:0.0}+{tX:0.0}+{tY:0.0}s)");

        if (s.ChildProcess) ChildCheck(s, path, N, end, xSums, check);
        try { File.Delete(path); } catch (IOException) { }
    }

    static string Coverage(World w)
    {
        var groups = w.Units.SelectMany(u => new[] { u.Group, u.LastGroup }).Where(g => g != null).Distinct().ToList();
        var ns = w.NavStats;
        int plans = groups.Count(g => g!.Plan != null);
        return $"틱 {w.Tick}, 유닛 {w.Units.Count}, 건물 {w.Buildings.Count}, 그룹 {groups.Count}(진형 {plans}), 길찾기 경로 {ns.Routes}/흐름장 {ns.Fields}/대기 {ns.Pending}, AI {w.Controllers.Count}";
    }

    /// <summary>새 프로세스에서 같은 파일을 불러와 이어 돌려 체크섬을 비교 (정적 캐시 · 초기화 순서에 기대지 않는지).</summary>
    static void ChildCheck(Scn s, string path, long n, long end, Dictionary<long, ulong> xSums, Action<bool, string> check)
    {
        try
        {
            var psi = new ProcessStartInfo(Environment.ProcessPath!)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory,
            };
            // dotnet KoreanDivision.dll 로 실행된 경우 (Environment.ProcessPath = dotnet) 를 위해 진입 dll 도 넘긴다
            string? entry = Environment.GetCommandLineArgs().FirstOrDefault(a => a.EndsWith("KoreanDivision.dll", StringComparison.OrdinalIgnoreCase));
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                psi.ArgumentList.Add(entry ?? Path.Combine(AppContext.BaseDirectory, "KoreanDivision.dll"));
            foreach (var a in new[] { "--snap-run", path, (end - n).ToString() }) psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi)!;
            string outp = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(120_000);
            int ok = 0, mismatch = 0, lines = 0;
            foreach (var line in outp.Split('\n'))
            {
                if (!line.StartsWith("SNAPSUM ")) continue;
                var p = line.Trim().Split(' ');
                if (p.Length != 3 || !long.TryParse(p[1], out long t) || !ulong.TryParse(p[2], System.Globalization.NumberStyles.HexNumber, null, out ulong sum)) continue;
                lines++;
                if (xSums.TryGetValue(t, out var xs) && xs == sum) ok++; else mismatch++;
            }
            check(proc.ExitCode == 0 && lines > 0 && mismatch == 0 && ok == lines, $"[{s.Name}] 새 프로세스에서 파일을 불러와 이어 돌림: 체크섬 {ok}/{lines}곳 일치 (종료코드 {proc.ExitCode})");
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            check(false, $"[{s.Name}] 새 프로세스 실행 실패: {e.Message}");
        }
    }

    /// <summary>CLI 보조: --snap-run 파일 틱 → 불러와서 그만큼 돌리며 100틱마다 "SNAPSUM 틱 체크섬" 출력 (새 프로세스 검증용).</summary>
    public static int Cli(string[] args)
    {
        KoreanDivision.Core.Defs.Load();
        var loaded = Snapshot.LoadFile(args[1], KoreanDivision.Core.Defs.Current);
        var w = loaded.World;
        long to = w.Tick + long.Parse(args[2]);
        while (w.Tick < to)
        {
            w.Step();
            if (w.Tick % 100 == 0) Console.WriteLine($"SNAPSUM {w.Tick} {w.ComputeChecksum():X16}");
        }
        return 0;
    }

    // ---------------------------------------------------------------- 형식 · 오류

    static void Format(Action<bool, string> check)
    {
        var w = Gen(100, 2, new[] { "standard" });
        for (int i = 0; i < 600; i++) w.Step();
        var bytes = Snapshot.Save(w, new SnapshotMeta("머리말 확인", 1, false, "standard"));
        var h = Snapshot.ReadHeader(bytes);
        check(h.Format == Snapshot.FormatVersion && h.GameVersion == Config.Version && h.BalanceHash == Defs.Hash && h.Tick == 600 && h.Name == "머리말 확인"
              && h.Factions.Count == 2 && h.MapWidth == 100 && h.Seed == w.Map.Seed && h.Checksum == w.ComputeChecksum().ToString("X16") && h.Units == w.Units.Count,
            "머리말: 형식 · 게임 · 밸런스 버전, 틱, 지도, 세력, 체크섬, 이름");
        string tmp = TestTemp.Child("kd_hdr.kds");
        File.WriteAllBytes(tmp, bytes);
        var h2 = Snapshot.ReadHeaderFile(tmp);
        check(h2.Tick == 600 && h2.Name == h.Name, "머리말만 읽기 (파일 앞부분만)");
        File.Delete(tmp);

        // 첫 번째 월드 (생성 맵 · 안개 · 전쟁) 의 체크섬이 불러온 뒤에도 같다
        var l = Snapshot.Load(bytes, Defs);
        check(l.World.ComputeChecksum() == w.ComputeChecksum() && l.World.Tick == w.Tick && l.World.Units.Count == w.Units.Count && l.Warnings.Count == 0, "불러온 직후 체크섬 · 틱 · 유닛 수 동일, 경고 없음");
        check(l.World.Fog.Enabled == w.Fog.Enabled && l.World.Faction(1).Name == w.Faction(1).Name && l.World.Controllers.Count == 2 && l.World.Controllers.All(c => c.Ai is StandardAi), "안개 설정 · 세력 이름 · AI 컨트롤러 복원");
    }

    static void Corruption(Action<bool, string> check)
    {
        var w = Gen(100, 2, new[] { "standard" });
        for (int i = 0; i < 300; i++) w.Step();
        var good = Snapshot.Save(w, Meta);

        bool Rejects(byte[] data, out string msg)
        {
            msg = "";
            try { Snapshot.Load(data, Defs); return false; }
            catch (SnapshotException e) { msg = e.Message; return true; }
            catch (Exception e) { msg = "엉뚱한 예외 " + e.GetType().Name + ": " + e.Message; return false; }
        }
        var trunc = good[..(good.Length / 2)];
        check(Rejects(trunc, out var m1), $"잘린 파일은 SnapshotException 으로 거부 ({m1})");
        var flip = (byte[])good.Clone();
        flip[flip.Length / 2 + 7] ^= 0x5A;   // 압축 본문 가운데 (끝 쪽 바이트는 Brotli 의 쓰이지 않는 비트일 수 있어 내용에 따라 그대로 풀림 — 2026-10-19 formations 병합 때 확인)
        check(Rejects(flip, out var m2), $"본문 한 바이트가 바뀐 파일은 거부 ({m2})");
        bool r3 = Rejects(Array.Empty<byte>(), out var m3), r4 = Rejects(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, out var m4);
        check(r3 && r4, $"빈 파일 · 쓰레기 파일은 거부 ({m3} / {m4})");
        var oldFmt = (byte[])good.Clone();
        oldFmt[4] = 99;
        check(Rejects(oldFmt, out var m5) && m5.Contains("v99"), $"다른 형식 버전은 명확한 메시지로 거부 ({m5})");
        // 무작위 훼손 (퍼즈): 예외는 항상 SnapshotException 하나로만 나와야 한다
        var rng = new Random(5);
        int tried = 0, wrongKind = 0;
        for (int i = 0; i < 40; i++)
        {
            var d = (byte[])good.Clone();
            int k = rng.Next(1, 4);
            for (int j = 0; j < k; j++) d[rng.Next(d.Length)] = (byte)rng.Next(256);
            tried++;
            try { Snapshot.Load(d, Defs); }
            catch (SnapshotException) { }
            catch (Exception) { wrongKind++; }
        }
        check(wrongKind == 0, $"무작위 훼손 {tried}회 모두 SnapshotException 으로만 실패 (엉뚱한 예외 {wrongKind}회)");
        // 압축 안쪽까지 훼손: 본문을 바꾼 뒤 다시 압축하고 검사값도 맞춰 넣어 압축 해제 · 본문 검사를 통과해 파서 · 복원 · 최종 체크섬 확인까지 닿게 한다.
        // 어떤 훼손이든 결과는 '정상 불러오기' (훼손이 상태에 영향이 없는 곳) 아니면 SnapshotException 이어야 한다 — 다른 예외 · 멈춤 · 거대 할당은 실패.
        int parserRejected = 0, parserAccepted = 0, parserWrong = 0;
        string firstWrong = "";
        var raw = Snapshot.RawBody(good);
        var swFuzz = Stopwatch.StartNew();
        for (int i = 0; i < 400; i++)
        {
            var r2 = (byte[])raw.Clone();
            int k = rng.Next(1, 4);
            for (int j = 0; j < k; j++)
            {
                int at = rng.Next(r2.Length);
                // 가끔은 값을 키워 보는 (개수 · 길이 · 번호 칸을 노린) 훼손
                r2[at] = rng.Next(3) == 0 ? (byte)(r2[at] | 0x80) : (byte)rng.Next(256);
            }
            try { Snapshot.Load(Snapshot.Rebuild(good, r2), Defs); parserAccepted++; }
            catch (SnapshotException) { parserRejected++; }
            catch (Exception e) { parserWrong++; if (firstWrong == "") firstWrong = $"{e.GetType().Name}: {e.Message}"; }
        }
        check(parserWrong == 0 && swFuzz.Elapsed.TotalSeconds < 60,
            $"본문 안쪽 훼손 400회: SnapshotException {parserRejected}회 · 영향 없는 훼손으로 정상 로드 {parserAccepted}회 · 엉뚱한 예외 {parserWrong}회 {firstWrong} ({swFuzz.Elapsed.TotalSeconds:0.0}초)");
    }

    static void Policy(Action<bool, string> check)
    {
        // 밸런스 파일이 달라지면 경고만 하고 불러온다 (정책: 거부하지 않음 — 유닛 종류가 없어진 경우만 거부)
        var w = Gen(100, 2, new[] { "standard" });
        for (int i = 0; i < 200; i++) w.Step();
        var bytes = Snapshot.Save(w, Meta);
        string src = KoreanDivision.Core.Defs.DefaultPath;
        string tmp = TestTemp.Child("kd_balance.json");
        File.WriteAllText(tmp, File.ReadAllText(src) + "\n");   // 내용이 달라 해시가 다르다
        var cur = KoreanDivision.Core.Defs.Current;
        try
        {
            var other = KoreanDivision.Core.Defs.Load(tmp);
            var l = Snapshot.Load(bytes, other);
            check(other.Hash != cur.Hash && l.Warnings.Any(x => x.Contains("balance.json")) && l.World.Tick == 200, "밸런스 파일이 다르면: 경고와 함께 불러옴 (거부하지 않음)");
        }
        finally
        {
            KoreanDivision.Core.Defs.Load(src);   // 원래 밸런스로 (Defs.Current 복구)
            File.Delete(tmp);
        }
        check(ReferenceEquals(KoreanDivision.Core.Defs.Current, cur) || KoreanDivision.Core.Defs.Current.Hash == cur.Hash, "Defs.Current 복구");
    }

    // ---------------------------------------------------------------- 크기 · 속도 측정 (--snapbench)

    /// <summary>
    /// 16인 · 약 8000기 후반전 월드를 만들어 저장 / 불러오기 시간과 파일 크기를 잰다.
    ///   KoreanDivision --snapbench [k=8000] [players=16] [pre=2400] [post=600] [keep=파일경로]
    /// 이어서 불러온 월드를 post 틱 돌려 저장 없이 돌린 월드와 본문 · 체크섬이 같은지도 확인한다.
    /// </summary>
    public static int Bench(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { }
        KoreanDivision.Core.Defs.Load();
        int k = 8000, players = 16, pre = 2400, post = 600;
        string? keep = null;
        foreach (var a in args.Skip(1))
        {
            int eq = a.IndexOf('=');
            if (eq < 0) continue;
            string key = a[..eq], val = a[(eq + 1)..];
            if (key == "k") k = int.Parse(val); else if (key == "players") players = int.Parse(val);
            else if (key == "pre") pre = int.Parse(val); else if (key == "post") post = int.Parse(val); else if (key == "keep") keep = val;
        }
        int size = Math.Clamp(Math.Max(Config.MapSizeFor(players), (int)(MathF.Sqrt(k) * 2.4f) / 8 * 8), Config.MinMapSize, Config.MaxMapSize);
        var sw = Stopwatch.StartNew();
        var w = Gen(size, players, new[] { "standard" });
        BenchSpawn(w, k, players);
        Console.WriteLine($"[snapbench] {players}인 {size}² 맵, 유닛 {w.Units.Count}기 생성 ({sw.Elapsed.TotalSeconds:0.0}초)");
        sw.Restart();
        for (int t = 0; t < pre; t++)
        {
            if (t % 600 == 60) BenchOrders(w, players, t);
            w.Step();
        }
        var ns = w.NavStats;
        Console.WriteLine($"[snapbench] {pre}틱 진행 ({sw.Elapsed.TotalSeconds:0.0}초): 유닛 {w.Units.Count}, 건물 {w.Buildings.Count}, 길찾기 경로 {ns.Routes}/흐름장 {ns.Fields}, {Coverage(w)}");

        byte[]? bytes = null;
        var saveMs = new List<double>();
        for (int i = 0; i < 4; i++)
        {
            var s = Stopwatch.StartNew();
            bytes = Snapshot.Save(w, Meta);
            saveMs.Add(s.Elapsed.TotalMilliseconds);
        }
        var raw = Snapshot.RawBody(bytes!);
        Console.WriteLine($"[snapbench] 저장 {string.Join(" / ", saveMs.Select(x => $"{x:0}ms"))} (첫 호출 포함, 이후는 워밍업 후) — 파일 {bytes!.Length / 1024.0 / 1024:0.00}MB (본문 원본 {raw.Length / 1024.0 / 1024:0.0}MB, 압축 {100.0 * bytes.Length / raw.Length:0}%)");
        string path = keep ?? TestTemp.Child("kd_bench.kds");
        File.WriteAllBytes(path, bytes);
        var loadMs = new List<double>();
        LoadedSnapshot? l = null;
        for (int i = 0; i < 3; i++)
        {
            var s = Stopwatch.StartNew();
            l = Snapshot.LoadFile(path, Defs);
            loadMs.Add(s.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine($"[snapbench] 불러오기 {string.Join(" / ", loadMs.Select(x => $"{x:0}ms"))}");

        // 동등성: 저장한 월드와 불러온 월드를 post 틱 이어 돌려 비교
        var y = l!.World;
        bool ok = true;
        for (int t = 0; t < post; t++)
        {
            if ((pre + t) % 600 == 60) { BenchOrders(w, players, pre + t); BenchOrders(y, players, pre + t); }
            w.Step(); y.Step();
            if (w.Tick % 100 == 0 && w.ComputeChecksum() != y.ComputeChecksum()) { Console.WriteLine($"[snapbench] 체크섬 불일치 틱 {w.Tick}"); ok = false; break; }
        }
        bool body = ok && Snapshot.RawBody(Snapshot.Save(w, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(Snapshot.Save(y, Meta)));
        Console.WriteLine($"[snapbench] 이어 돌린 {post}틱: 체크섬 {(ok ? "일치" : "불일치")}, 본문 전체 {(body ? "바이트 동일" : "다름")}");
        if (keep == null) File.Delete(path);
        return ok && body ? 0 : 1;
    }

    static void BenchSpawn(World w, int k, int players)
    {
        string[] mix = { "infantry", "infantry", "infantry", "recruit", "recruit", "veteran", "heavy_at_rifleman", "tank_ft_male", "scout", "infantry" };
        int per = k / players;
        for (int f = 1; f <= players; f++)
        {
            var hq = HqOf(w, f);
            Vector2 toward = Vector2.Normalize(new Vector2(w.Map.Width / 2f, w.Map.Height / 2f) - hq.Pos);
            Vector2 side = new(-toward.Y, toward.X);
            for (int i = 0; i < per; i++)
            {
                var def = Defs.Units[mix[i % mix.Length]];
                float r = MathF.Sqrt(i) * 1.05f, a = i * 2.3999632f;
                w.SpawnUnit(def, f, hq.Pos + toward * 11 + (toward * DetMath.Cos(a) + side * DetMath.Sin(a)) * r);
            }
        }
        w.Fog.Update();
    }

    /// <summary>세력마다 병력을 25기씩 부대로 나눠 이웃 · 맞은편 세력의 본진으로 (일부는 진형으로) 보낸다.</summary>
    static void BenchOrders(World w, int players, long t)
    {
        for (int f = 1; f <= players; f++)
        {
            var army = w.Units.Where(u => u.Owner == f && u.Alive && u.Weapon != null).ToList();
            int target = (f + 1 + (int)(t / 600) * 3) % players + 1;
            if (target == f) target = target % players + 1;
            var dest = AiHelpers.StartOf(w, target);
            for (int i = 0, g = 0; i < army.Count; i += 25, g++)
            {
                var chunk = army.Skip(i).Take(25).ToList();
                FormSpec? form = g % 3 == 0 ? new FormSpec((int)FormType.Wedge, (int)FormOrder.ArmorFront, Vector2.Zero) : g % 3 == 1 ? new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, Vector2.Zero) : null;
                w.AttackMove(f, chunk, dest + new Vector2(g % 5 - 2, g % 7 - 3) * 2, form);
            }
        }
    }

    // ---------------------------------------------------------------- 저장 완전성 점검

    /// <summary>
    /// 상태를 들고 있는 클래스의 모든 인스턴스 필드를 훑어, 스냅샷이 '저장한다 / 저장하지 않는 이유가 있다' 로 분류해 둔 것만 있는지 확인한다.
    /// 새 필드를 추가하고 저장 코드를 빠뜨리면 여기서 걸린다 (그 필드가 시뮬레이션에 영향을 준다면 WorldSnapshot.cs 등에 저장을 추가하고 saved 에 이름을 올릴 것).
    /// </summary>
    static void Audit(Action<bool, string> check)
    {
        static string Norm(string n) => n.StartsWith('<') ? n[1..n.IndexOf('>')] : n;
        void Audit1(Type t, string saved, string notSaved)
        {
            var known = new HashSet<string>((saved + " " + notSaved).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var have = new List<Type>();
            for (var c = t; c != null && c != typeof(object); c = c.BaseType) have.Add(c);
            var fields = have.SelectMany(c => c.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)).ToList();
            // [SnapExt] 표시 필드(자동 속성이면 그 속성)는 v0.4 확장 구역에서 저장 → 인정
            foreach (var f in fields)
            {
                bool ext = f.IsDefined(typeof(SnapExtAttribute), false);
                if (!ext && f.Name.StartsWith('<'))
                {
                    var prop = f.DeclaringType!.GetProperty(Norm(f.Name), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    ext = prop != null && prop.IsDefined(typeof(SnapExtAttribute), false);
                }
                if (ext) known.Add(Norm(f.Name));
            }
            var names = fields.Select(f => Norm(f.Name)).Distinct().ToList();
            var unknown = names.Where(n => !known.Contains(n)).ToList();
            var gone = known.Where(n => !names.Contains(n)).ToList();
            check(unknown.Count == 0 && gone.Count == 0, $"저장 완전성: {t.Name} 필드 {names.Count}개 모두 분류됨" + (unknown.Count > 0 ? $" — 분류 안 된 새 필드: {string.Join(", ", unknown)}" : "") + (gone.Count > 0 ? $" — 목록에만 있고 코드에 없는 이름: {string.Join(", ", gone)}" : ""));
        }
        Audit1(typeof(Unit),
            "Id Owner Pos Hp LastDamagedTick Def Weapon Armor Velocity Facing Order Group LastGroup NavToken NavRoute NavSeg NavEpoch NavRawX NavRawY NavGX NavGY NextLosTick NextPersonalTick LosOk LosGoal FormGroup FormOff Target Cooldown NextAcquireTick AimTarget AimLeft HoldLeft EngagedTick LastMoveTick LeashOrigin LastShotTick LastOrderTick ReactDelay NavKind ProgPos ProgTick ProgLast NudgeUntil StuckLevel NudgeDir Company Squad ScoutCompany Role",
            "Selected MarchShot RetreatShot RenderPrev");   // RetreatShot = 후퇴 사격 한 발 순간 값 (pursuit2)
        Audit1(typeof(Building),
            "Id Owner Pos Hp LastDamagedTick Def TileX TileY CaptureFaction CaptureProgress Contested",
            "Selected");
        Audit1(typeof(MoveGroup), "Target Goal Field ArriveRadius MemberCount Big Inited Members Leader Route RouteL Plan", "");
        Audit1(typeof(WeatherState), "Kind Time StartKind StartTime Schedule Next TimeStepTicks Changes", "");   // WTHR
        Audit1(typeof(Faction), "Id Name Lang IsAi RevealAll Defeated Revealed NoHqTime Team", "");
        Audit1(typeof(FogOfWar), "world width height own memory Enabled Version seeded stampEpoch",
            "grids circles forget stampMark vision due fversion smokeNear smokeMark");   // smokeNear · smokeMark = 연막 찍기 작업 버퍼 · 표시 (N59)   // grids = own 의 동맹 합침 (불러올 때 다시 만듦) · 원 표 캐시 · 작업 버퍼 · 표시 마크 (epoch 로 무효화) · 지형 시야 메모 (지형만의 순수 함수)
        Audit1(typeof(GameMap),
            "Width Height Seed Id DisplayName FileHash Mirrored tiles walkable occupant passable high Version StartTiles ResourceSites UnitSpawns",
            "ChangeLog terrainDirty terrainHashDirty TerrainVersion TrenchVersion terrainLock forestDepth tileFlags terrainHash terrainEdits");   // terrainEdits = 렌더러용 편집 기록   // 숲 깊이 · 칸 표시 · 지형 해시 = 지형 · 고지에서 다시 계산   // ChangeLog 는 길찾기가 소비하는 기록 — 불러올 때 길찾기 그래프를 현재 지도에서 새로 만든다
        Audit1(typeof(World),
            "Map Defs Factions Units Buildings Fog Controllers Tick nextId lastMapVersion byId navEpoch nav pending CheatsUsed CommandLog RecordCommands " +
            "LastAttacked Blasts Shells Winner Winners ChecksumLog BattleMode Battle OriginSnapshot OriginTick Companies Battalions nextCompanyId nextBattalionId nextSquadId Weather OrderQueues nextQueueId",
            "executing CommandFeed Stats IssueSel execSel queueExec queueNoChain " +                                              // 틱 안에서만 쓰는 목록 · 리플레이 공급 훅 · IssueSel/execSel = 명령 발행/실행 중 선택 인원 (N37, 명령의 Sel 이 원본) (스냅샷은 틱 경계에서 찍고, 리플레이 월드는 따로 만든다)
            "cavThreatTick cavThreats cellStart cellCount cellCursor occCells occCount sortTmp coarseMask coarseW cellMask unitMask bldgMask trainMask sortedIdx unitCell sepX sepY sepR sepR0 sepDX sepDY sepN sepFlag sepOwner sepHostile sepGrp sepLast hashDirty fIndex indexAllRebuild " +   // 공간 해시 · 충돌 해소: 틱 경계에서 항상 다시 만든다
            "jamBuf queryBuffer chokeCache chokeCacheVersion captureCount aliveCount supplyCities supplyWagons trenchComp trenchCompVer trenchOwners trenchOwnersTick actingCache " +       // 작업 버퍼 (쓰기 전에 항상 비우거나 덮어씀; supplyCities · supplyWagons = 탄약 회복 틱 안 목록; trenchComp = 지형 해시로 다시 계산하는 참호 묶음, trenchOwners = 틱마다 유닛에서 다시 모으는 참호 자리 표)
            "Shots Deaths Notices Impacts splashBuffer MeleeStrikes sprayBuffer officers bearers spotters grenBuffer cmdOfficers cmdOfficersTick taGun taAs taOff taTmp taMv taTiles drillBuf drillRole heavyCompany heavyGroup heavyScan closeChargers coverBuf occCand nearMissBuf dangerBuffer cavGateBuf heavyGapBuf");   // heavyGapBuf = heavy 간격 작업 버퍼 (Arty3.cs)   // dangerBuffer = 포수 규율 작업 버퍼 (Pursuit.cs, pursuit2)   // nearMissBuf = 근접 탄착 제압 작업 버퍼 (Suppression.cs)   // coverBuf · occCand = 엄폐 찾기 · 진지 점유 작업 버퍼 (CoverSeek.cs, engage)   // closeChargers = 대열 정비 돌격 감지 목록 (감지 틱마다 다시 모음, CloseRanks.cs)   // heavy* = 중화기 보폭 기준 (틱마다 다시 계산, HeavyPacing.cs)   // drillBuf · drillRole = 틀 행동 판단 작업 버퍼 (TacticDrill.cs)   // 교전 2차: 근접 효과 · 흩뿌림 버퍼 · 장교 목록 (RebuildHash 가 다시 모은다)                                                // 화면 효과 · 알림 (시뮬레이션과 무관)
        Audit1(typeof(Nav),
            "map SW SH sectors portals freePortal built logRead cellsW routes pending fields pool routeSeq MinClearance ok large",
            "heap astar sdist gdist tmpDist gScore prev gStamp closedStamp searchId floodStack FieldBudget FieldsPerTick RoutesComputed FieldsComputed Rebuilds WorkTotal");
        // (출입구 · 구역 안 비용 · 연결 성분은 지도에서 다시 만든다 — sectors 의 Version 만 저장. pool 은 메모리 재사용 풀)
        Audit1(typeof(NavRoute), "Key Valid Pending Failed Approx SX SY GX GY GoalX GoalY GoalSector GoalRegion Segs Sectors StartRegion LastUsed Seq Work", "");
        Audit1(typeof(NavField), "Sector TX TY Version Cost LastUsed", "Dead");
        Audit1(typeof(FormPlan), "Off Speed Facing Dir Extent", "");
        Audit1(typeof(Company), "Id Owner Number Form Tactic ActiveTactic Drill InitialStrength Facing Members Battalion Anchor Squads Holes Scouts MarchUntil MarchFireOn MarchAdvUntil Commander HandoverUntil CloseAlarm CloseAt CloseUntil Posture ContactTick ContactSince KeepFormation HasRally RallyPoint TA ChokeHold", "");
        Audit1(typeof(TrenchAssaultState), "Stage Sub Result Flags Target Dir FirePoint StartTick StageTick ReadyTick SubTick BoundTick CooldownUntil BoundGroup", "");   // N32 (TrenchAssault.cs)
        Audit1(typeof(Squad), "Id Number Company Members MarchUntil Form", "");
        Audit1(typeof(OrderQueue), "Id Faction Sel Units Legs HasCurrent Current", "");   // N74 (OrderQueue.cs)
        Audit1(typeof(Train), "Id Owner Tag Track Cars WaitTicks S V Manual ManualDir Next ScriptDir WaitLeft Blocked", "buf");   // 열차 (Train.cs, T30a · T30b FV59 — ENTS 끝 WriteTrains); buf = 선로 감지 작업 버퍼
        Audit1(typeof(TrainCar), "Id Owner Hp LastDamagedTick Kind Def Index Guns", "Selected Pos Dir");
        Audit1(typeof(TrainGun), "W TargetId Cooldown AimLeft", "");   // T30b 객차 무기 (W = 무기 키로 저장)   // Def = Kind 로 다시 찾음, Index = 목록 순서, Pos · Dir = 열차 호장에서 다시 계산
        Audit1(typeof(TrainTrack), "TX TY Waypoints Loop", "Cum Length");   // 누적 호장 = 칸 열에서 다시 계산
        Audit1(typeof(DrillState), "Phase Passive PhaseTick ContactTick Enemy Home BoundSquad BoundLast BoundTick Man ManTick ManGoal ManWay FollowGoal LiftUntil LiftPos", "");   // ORG_ 전술 틀 구역 (TacticDrill.cs)
        Audit1(typeof(Battalion), "Id Owner Number Form Companies Commander HandoverUntil", "");
        Audit1(typeof(MissionState), "Def Started Deployment ForceUnits Tags StartRank ObjState ObjProgress ObjRevealed EventFired ReinforcementSpawned ReinforcementDue PatrolLeg Spawned Messages Weather TimeOfDay Result EndTick FailReason OfficerKey", "Alive AliveCars TotalCars");   // Alive/AliveCars/TotalCars = 매 틱 다시 세는 작업 버퍼
        Audit1(typeof(CompanyAnchor), "Pos Heading Goal FinalDir Arrived Column Close ClearTicks WaitTicks HalfWidth Spacing SpeedMul Slots ColumnSlots FinalOff Trail Group Flag", "RowPos RowTan RowReach");   // 종대 줄 위치 = 매 틱 자취에서 다시 계산
    }

    // ---------------------------------------------------------------- 리플레이

    static void ReplayFromSnapshot(Action<bool, string> check)
    {
        var w = AiWorld(MapFile.Load("corners4", 3), new[] { "standard" });
        w.RecordCommands = true;
        for (int i = 0; i < 1200; i++) w.Step();
        var bytes = Snapshot.Save(w, Meta);
        var l = Snapshot.Load(bytes, Defs);
        var y = l.World;   // 불러온 월드에서 이어서 기록하며 플레이 (AI 가 계속 명령을 낸다)
        for (int i = 0; i < 7200 && (i < 1800 || !y.CommandLog.Any(c => c.Tick >= 1200)); i++) y.Step();   // 지도 ×1.5 (2026-10-19): 표준 AI 가 첫 명령까지 더 걸릴 수 있다
        var rep = ReplayFile.FromWorld(y, 1);
        check(rep.StartSnapshot != null && rep.StartTick == 1200 && rep.Commands.Count > 0 && rep.Commands.All(c => c.Tick >= 1200),
            $"리플레이가 시작 스냅샷(틱 {rep.StartTick})을 첨부하고, 그 뒤의 명령 {rep.Commands.Count}개만 담음");
        string tmp = TestTemp.Child("kd_rep.kdr.json");
        rep.Save(tmp);
        var back = ReplayFile.Load(tmp);
        long bad = back.Verify(Defs, out string report);
        check(bad < 0, $"스냅샷에서 시작하는 리플레이 검증 일치: {report}");
        var tampered = ReplayFile.Load(tmp);
        tampered.EndChecksum ^= 1;
        check(tampered.Verify(Defs, out _) >= 0, "스냅샷 리플레이도 변조 감지");
        File.Delete(tmp);
    }
}
