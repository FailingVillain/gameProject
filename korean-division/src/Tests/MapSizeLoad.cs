using System.Diagnostics;
using System.Numerics;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N13 지도 크기별 부하 측정 (읽기 전용 측정 도구, 시뮬 변경 없음). 창 없음.
///   KoreanDivision --mapload size=800 k=1000 scene=A|C|E ticks=300 [seed=N]
/// 장면: E = 빈 지도 (시작 병력 그대로, k 무시) · A = 보병 위주 혼성 k 기 흩어져 서 있음 (전투 없음) · C = 두 덩어리 공격 이동 충돌.
/// 구성: 보병 82 · 정찰 2 · 기병 8 · 기관총 3 · 야전포 3 · 곡사포 2 (%) — 전차 · 대전차소총 없음.
/// 프로세스당 한 칸(크기 × k × 장면)만 잰다 (작업 집합을 칸마다 따로 보려고).
/// </summary>
public static class MapSizeLoad
{
    static readonly string[] Mix = BuildMix();
    static string[] BuildMix()
    {
        var l = new List<string>();
        void Add(string id, int n) { for (int i = 0; i < n; i++) l.Add(id); }
        Add("infantry", 82); Add("infantry_scout", 2); Add("cavalry_default", 8); Add("heavy_machinegunner", 3); Add("heavy_field_gun", 3); Add("heavy_howitzer", 2);
        // 섞어 놓기 (결정적)
        var r = new Random(7);
        return l.OrderBy(_ => r.Next()).ToArray();
    }

    static double Mb(long b) => b / 1048576.0;

    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        int size = 400, k = 1000, ticks = 300, seed = Config.DefaultSeed; char scene = 'A';
        foreach (var a in args)
        {
            int eq = a.IndexOf('='); if (eq < 0) continue;
            string key = a[..eq].ToLowerInvariant(), val = a[(eq + 1)..];
            switch (key)
            {
                case "size": size = int.Parse(val); break;
                case "k": k = int.Parse(val); break;
                case "scene": scene = char.ToUpperInvariant(val[0]); break;
                case "ticks": ticks = int.Parse(val); break;
                case "seed": seed = int.Parse(val); break;
            }
        }
        var defs = Defs.Load();
        var proc = Process.GetCurrentProcess();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long heap0 = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        var map = GameMap.Generate(size, size, seed, 2);
        double genMs = sw.Elapsed.TotalMilliseconds;
        long heapMap = GC.GetTotalMemory(true);
        sw.Restart();
        var world = World.CreateStandard(map, defs);
        double createMs = sw.Elapsed.TotalMilliseconds;
        long heapWorld = GC.GetTotalMemory(true);
        int startUnits = world.Units.Count;
        var rng = new Random(seed ^ k);
        sw.Restart();
        if (scene != 'E')
        {
            foreach (var u in world.Units) if (u.Weapon != null) u.Hp = 0;
            world.Step();
            var center = new Vector2(size / 2f, size / 2f);
            if (scene == 'A')
                for (int i = 0; i < k; i++) world.SpawnUnit(defs.Units[Mix[i % Mix.Length]], i % 2 + 1, Walkable(world, rng));
            else
            {
                int per = k / 2;
                float rad = MathF.Sqrt(per * 0.7f / MathF.PI);
                float d = Math.Min(size / 2f - 6, rad + 25);
                var a = Blob(world, defs, 1, center + new Vector2(-d, 0), per);
                var b = Blob(world, defs, 2, center + new Vector2(d, 0), k - per);
                world.AttackMove(1, a, center + new Vector2(d, 0));
                world.AttackMove(2, b, center + new Vector2(-d, 0));
            }
        }
        double spawnMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        world.Fog.Update();
        double fogInitMs = sw.Elapsed.TotalMilliseconds;
        for (int i = 0; i < 60; i++) world.Step();
        Array.Clear(World.Prof); World.Profile = true;
        var ms = new List<double>(ticks);
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        long gc0 = GC.CollectionCount(0);
        // N16: 틱마다 구간별 증분 (최대 · p99 · 최악 틱 분해) + GC 세대별 횟수
        const int NS = 11;
        var secTick = new double[ticks, NS];
        var gcTick = new int[ticks, 3];
        var simTick = new long[ticks];
        var editTick = new int[ticks];
        var prev = new long[NS];
        double tickToMs = 1000.0 / Stopwatch.Frequency;
        for (int t = 0; t < ticks; t++)
        {
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            simTick[t] = world.Tick;
            int ed0 = world.Map.TerrainEditCount;
            long t0 = Stopwatch.GetTimestamp();
            world.Step();
            ms.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            for (int s = 0; s < NS; s++) { secTick[t, s] = (World.Prof[s] - prev[s]) * tickToMs; prev[s] = World.Prof[s]; }
            editTick[t] = world.Map.TerrainEditCount - ed0;
            gcTick[t, 0] = GC.CollectionCount(0) - g0; gcTick[t, 1] = GC.CollectionCount(1) - g1; gcTick[t, 2] = GC.CollectionCount(2) - g2;
        }
        World.Profile = false;
        proc.Refresh();
        double ws = Mb(proc.WorkingSet64), peak = Mb(proc.PeakWorkingSet64);
        long heapEnd = GC.GetTotalMemory(true);
        var sorted = ms.OrderBy(x => x).ToArray();
        string[] names = { "AI+cmd+nav", "hash1", "UpdateUnit", "hash2", "Separate", "Terrain", "Capt+Supply", "RemoveDead", "Fog", "Victory+prune", "Checksum", "(FindTarget)", "(route)", "(field)" };
        double toMs = 1000.0 / Stopwatch.Frequency / ticks;
        var ph = new StringBuilder();
        for (int i = 0; i < names.Length; i++) ph.Append($" {names[i]}={World.Prof[i] * toMs:0.000}");
        // 저장
        double saveMs = -1, saveMb = -1;
        if (size <= 1500)
        {
            try
            {
                var s2 = Stopwatch.StartNew();
                var bytes = Snapshot.Save(world, new SnapshotMeta("n13", 1, false, "standard"));
                saveMs = s2.Elapsed.TotalMilliseconds; saveMb = Mb(bytes.Length);
            }
            catch (Exception e) { Console.WriteLine("save fail " + e.Message); }
        }
        Console.WriteLine($"RESULT size={size} scene={scene} k={k} units={world.Units.Count} start={startUnits} genMs={genMs:0} createMs={createMs:0} spawnMs={spawnMs:0} fogInitMs={fogInitMs:0} " +
            $"mean={ms.Average():0.00} p50={sorted[sorted.Length / 2]:0.00} p95={sorted[(int)(sorted.Length * 0.95)]:0.00} max={sorted[^1]:0.00} " +
            $"mapMB={Mb(heapMap - heap0):0} worldMB={Mb(heapWorld - heapMap):0} heapEndMB={Mb(heapEnd):0} wsMB={ws:0} peakWsMB={peak:0} allocKBtick={(GC.GetAllocatedBytesForCurrentThread() - alloc0) / 1024.0 / ticks:0} gen0={GC.CollectionCount(0) - gc0} saveMs={saveMs:0} saveMB={saveMb:0.0}");
        Console.WriteLine("PHASE size=" + size + " scene=" + scene + " k=" + k + ph);
        var mx = new StringBuilder();
        for (int s = 0; s < NS; s++)
        {
            var col = new double[ticks];
            for (int t = 0; t < ticks; t++) col[t] = secTick[t, s];
            Array.Sort(col);
            mx.Append($" {names[s]}={col[(int)(ticks * 0.99)]:0.00}/{col[^1]:0.00}");
        }
        Console.WriteLine("SECMAX(p99/max) size=" + size + " scene=" + scene + " k=" + k + mx);
        int gcs0 = 0, gcs1 = 0, gcs2 = 0;
        for (int t = 0; t < ticks; t++) { gcs0 += gcTick[t, 0]; gcs1 += gcTick[t, 1]; gcs2 += gcTick[t, 2]; }
        int edTot = 0, edTicks = 0; for (int t = 0; t < ticks; t++) if (editTick[t] > 0) { edTot += editTick[t]; edTicks++; }
        Console.WriteLine($"TERRAIN edits={edTot} in {edTicks} ticks");
        Console.WriteLine($"GC gen0={gcs0} gen1={gcs1} gen2={gcs2} (measure window)");
        var order = Enumerable.Range(0, ticks).OrderByDescending(t => ms[t]).Take(5);
        foreach (int t in order)
        {
            var w = new StringBuilder();
            double secSum = 0;
            for (int s = 0; s < NS; s++) { secSum += secTick[t, s]; if (secTick[t, s] >= 0.5) w.Append($" {names[s]}={secTick[t, s]:0.0}"); }
            Console.WriteLine($"WORST t={t} simTick={simTick[t]} ms={ms[t]:0.00} secSum={secSum:0.00} gc={gcTick[t, 0]}/{gcTick[t, 1]}/{gcTick[t, 2]} terrainEdits={editTick[t]}{w}");
        }
        GC.KeepAlive(world);
        return 0;
    }

    static Vector2 Walkable(World w, Random rng)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            int x = rng.Next(2, w.Map.Width - 2), y = rng.Next(2, w.Map.Height - 2);
            if (w.Map.IsWalkable(x, y)) return new Vector2(x + 0.5f, y + 0.5f);
        }
        return new Vector2(w.Map.Width / 2f, w.Map.Height / 2f);
    }

    static List<Unit> Blob(World w, Defs defs, int faction, Vector2 center, int n)
    {
        var list = new List<Unit>(n);
        for (int i = 0; i < n; i++)
        {
            float r = MathF.Sqrt(i * 0.7f / MathF.PI), a = i * 2.3999632f;
            var pos = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            pos = new Vector2(Math.Clamp(pos.X, 1, w.Map.Width - 1), Math.Clamp(pos.Y, 1, w.Map.Height - 1));
            list.Add(w.SpawnUnit(defs.Units[Mix[i % Mix.Length]], faction, pos));
        }
        return list;
    }
}
