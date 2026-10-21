using System.Globalization;
using System.Numerics;
using KoreanDivision.Core;
namespace KoreanDivision.Tests;
/// <summary>--balance sim 측정 보조 (치명성 비교용): 120/300초 손실, 50% 손실 시각, 사격 · 명중 · 평균 거리를 한 줄 "MEAS ..." 로 낸다. 시뮬 동작은 건드리지 않는다.</summary>
public static class MeasX
{
    static int sA, sB; static double t50A = -1, t50B = -1, dSum; static long dN, shots, hits;
    static double hDist; static long hN;
    // N21: 돌격 사격 사용량 (돌격보병만, 측정 전용)
    static long aTicks, aMove, aMoveOn, aShots, aMoveShots, aPistol, aGren, aMelee; static readonly Dictionary<int, int> aKills = new();
    static int a120 = -1, b120 = -1, a300 = -1, b300 = -1;
    public static void Init(World w) { sA = w.Units.Count(u => u.Owner == 1); sB = w.Units.Count(u => u.Owner == 2); t50A = t50B = -1; dSum = 0; dN = shots = hits = 0; a120 = b120 = a300 = b300 = -1; hDist = 0; hN = 0; aTicks = aMove = aMoveOn = aShots = aMoveShots = aPistol = aGren = aMelee = 0; aKills.Clear(); if (ArtyFx.On) ArtyFx.Init(w); ApplyTerrain(w); }
    /// <summary>N18 측정 전용: KD_X_TER=trench|forest → B 진영(처음 위치 ±2칸) 지형을 참호/숲으로 바꾼다 (측정 도구, 게임 로직 아님).</summary>
    static void ApplyTerrain(World w)
    {
        var mode = Environment.GetEnvironmentVariable("KD_X_TER"); if (mode is not ("trench" or "forest")) return;
        var bs = w.Units.Where(u => u.Owner == 2).ToList(); if (bs.Count == 0) return;
        var (x0, y0) = GameMap.TileOf(new Vector2(bs.Min(u => u.Pos.X) - 2, bs.Min(u => u.Pos.Y) - 2)); var (x1, y1) = GameMap.TileOf(new Vector2(bs.Max(u => u.Pos.X) + 2, bs.Max(u => u.Pos.Y) + 2));
        for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++)
            if (w.Map.Get(x, y) is TerrainType.Grass or TerrainType.Dirt or TerrainType.Sand or TerrainType.Crater) w.MutateTerrain(x, y, mode == "trench" ? TerrainType.Trench : TerrainType.Forest);
    }
    public static void Tick(World w)
    {
        if (ArtyFx.On) ArtyFx.Tick(w);
        foreach (var s in w.Shots) if (s.Tick == w.Tick - 1) { shots++; if (s.Hit) { hits++; hDist += Vector2.Distance(s.From, s.To); hN++; } dSum += Vector2.Distance(s.From, s.To); dN++; }
        foreach (var u in w.Units)
            if (u.Alive && u.Def.MarchFireDefault)
            {
                aTicks++; aKills[u.Id] = u.Kills;
                if (u.Velocity != Vector2.Zero) { aMove++; if (u.Company is { MarchFireOn: true }) aMoveOn++; }
            }
        foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && w.Find(s.ShooterId) is Unit su && su.Def.MarchFireDefault) { aShots++; if (su.Velocity != Vector2.Zero) aMoveShots++; if (su.Weapon?.Key == su.Def.Sidearm) aPistol++; }
        foreach (var im in w.Impacts) if (im.Tick == w.Tick - 1 && im.Weapon == "grenade" && w.Find(im.ShooterId) is Unit gu && gu.Def.MarchFireDefault) aGren++;   // N23: 돌격보병 수류탄 폭발
        foreach (var ms in w.MeleeStrikes) if (ms.Tick == w.Tick - 1 && w.Find(ms.AttackerId) is Unit mu && mu.Def.MarchFireDefault) aMelee++;   // N23: 돌격보병 근접 타격
        if (w.Tick % 30 != 0) return;
        int a = w.Units.Count(u => u.Owner == 1), b = w.Units.Count(u => u.Owner == 2);
        if (t50A < 0 && a * 2 <= sA) t50A = w.Time;
        if (t50B < 0 && b * 2 <= sB) t50B = w.Time;
        if (a120 < 0 && w.Time >= 120) { a120 = sA - a; b120 = sB - b; }
        if (a300 < 0 && w.Time >= 300) { a300 = sA - a; b300 = sB - b; }
    }
    public static void Report(World w)
    {
        if (ArtyFx.On) ArtyFx.Report(w);
        int a = w.Units.Count(u => u.Owner == 1), b = w.Units.Count(u => u.Owner == 2);
        string T(double t) => t < 0 ? "-" : t.ToString("0", CultureInfo.InvariantCulture);
        Console.WriteLine($"MARCH ticks={aTicks} move={aMove} moveOn={aMoveOn} shots={aShots} moveShots={aMoveShots} pistol={aPistol} gren={aGren} melee={aMelee}");
        Console.WriteLine($"MEAS start={sA}/{sB} end_t={w.Time:0} winner={w.Winner} lossEnd={sA - a}/{sB - b} loss120={a120}/{b120} loss300={a300}/{b300} t50={T(t50A)}/{T(t50B)} shots={shots} hits={hits} hitpct={(shots > 0 ? 100.0 * hits / shots : 0):0.0} meanDist={(dN > 0 ? dSum / dN : 0):0.0} hitDist={(hN > 0 ? hDist / hN : 0):0.0} ta={w.Stats.TaStarts}/{w.Stats.TaDone}/{w.Stats.TaFail}");
    }
}
