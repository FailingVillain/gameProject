using System.Diagnostics;
using System.Numerics;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>PR5 산포 시간 전후 측정 (창 없음): KoreanDivision --scatterbench map=battle_city mult=1,2,4 units=4640 [seed=1] [v2=1].
/// 옛 선형 순회 (GameMap.LegacyCrowded / LegacyAvoid) 대비 격자. 미션 설정 = 칸 묶음 6 · 밀도 4, mult = 군집당 소품 수 배수 (PR4 측정과 같은 방식).</summary>
public static class ScatterBench
{
    static string Sha(GameMap m)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, true))
            foreach (var p in m.PropSeeds) { bw.Write(BitConverter.SingleToInt32Bits(p.X)); bw.Write(BitConverter.SingleToInt32Bits(p.Y)); bw.Write(p.Type); }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ms.ToArray()))[..16];
    }

    public static int Run(string[] args)
    {
        string map = "battle_city"; int[] mults = { 1 }; int units = 0, seed = 1; bool v2 = false;
        foreach (var a in args)
        {
            int eq = a.IndexOf('='); if (eq < 0) continue;
            string k = a[..eq], v = a[(eq + 1)..];
            switch (k)
            {
                case "map": map = v; break;
                case "mult": mults = v.Split(',').Select(int.Parse).ToArray(); break;
                case "units": units = int.Parse(v); break;
                case "seed": seed = int.Parse(v); break;
                case "v2": v2 = v == "1"; break;
            }
        }
        var defs = Defs.Load();
        foreach (int mult in mults)
        {
            var pd = PropDefs.Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "props.json"));
            pd.Rules.Scatter.ClusterMin *= mult; pd.Rules.Scatter.ClusterMax *= mult;
            pd.Finish(); PropDefs.Override(pd);
            double Run1(bool legacyC, bool legacyA, out string sha, out int n)
            {
                GameMap.LegacyCrowded = legacyC; GameMap.LegacyAvoid = legacyA;
                var gm = MapFile.Load(map, seed); gm.ClearProps();
                World? w = null;
                if (units > 0)
                {
                    w = World.CreateStandard(gm, defs);
                    var rng = new Random(3);
                    for (int i = 0; i < units; i++)
                        for (int t = 0; t < 50; t++)
                        {
                            var p = new Vector2(rng.Next(gm.Width / 4, gm.Width * 3 / 4) + .5f, rng.Next(gm.Height / 4, gm.Height * 3 / 4) + .5f);
                            if (!gm.IsTerrainWalkable((int)p.X, (int)p.Y)) continue;
                            w.SpawnUnit(defs.Units["infantry"], i % 2 + 1, p); break;
                        }
                }
                var sw = Stopwatch.StartNew();
                if (w != null) n = w.ScatterMissionProps(new MissionDef { PropScatter = true, PropDensity = 4f, PropCell = 6, PropsV2 = v2 });
                else n = gm.ScatterProps(false, null, 4f, 6, v2);
                double ms = sw.Elapsed.TotalMilliseconds;
                sha = Sha(gm);
                GameMap.LegacyCrowded = GameMap.LegacyAvoid = false;
                return ms;
            }
            double both = Run1(true, true, out var s1, out int n1);      // 옛: Crowded 선형 + Avoid 선형
            double crowdedNew = Run1(false, true, out var s2, out _);    // Crowded 만 격자
            double avoidNew = Run1(true, false, out var s3, out _);      // Avoid 만 격자
            double allNew = Run1(false, false, out var s4, out _);       // 둘 다 격자
            Console.WriteLine($"BENCH map={map} units={units} mult={mult} props={n1} old={both:0}ms crowdedGrid={crowdedNew:0}ms avoidGrid={avoidNew:0}ms new={allNew:0}ms speedup={both / Math.Max(allNew, 0.01):0.0}x sameSha={s1 == s2 && s2 == s3 && s3 == s4} sha={s4}");
        }
        return 0;
    }
}
