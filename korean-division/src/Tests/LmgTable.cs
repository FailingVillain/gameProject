using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>개발용 표: 경기관총 1 대 소총 N 승률 (전술 AI 양쪽, 평지). KD_SELFTEST_ONLY=lmgtable [KD_LMG_SEEDS=8] [KD_LMG_RIFLE=infantry].</summary>
public static class LmgTable
{
    public static int Run()
    {
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("KD_LMG_SEEDS"), out var s) ? s : 8;
        string rifle = Environment.GetEnvironmentVariable("KD_LMG_RIFLE") ?? "infantry";
        foreach (int n in new[] { 3, 5, 8 })
        {
            int wins = 0; double left = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var setup = new BattleSetup { SideA = "heavy_machinegunner:1", SideB = $"{rifle}:{n}", Map = "flat", Distance = 40, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
                var w = setup.CreateWorld(Defs.Current, seed);
                w.Fog.Enabled = false;
                w.Controllers.Add((1, AiRegistry.Create("tactical")!));
                w.Controllers.Add((2, AiRegistry.Create("tactical")!));
                while (w.Tick < 300 * 60 && w.Winner == 0) w.Step();
                if (w.Winner == 1) { wins++; left += w.Units.Where(u => u.Owner == 1).Sum(u => u.Hp); }
            }
            Console.WriteLine($"LMG 1 vs {rifle} {n}: {wins}/{seeds} ({100.0 * wins / seeds:0}%)");
        }
        return 0;
    }
}
