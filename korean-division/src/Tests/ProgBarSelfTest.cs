using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>progbar: 일본 보병은 처음부터 착검 (돌격 명령에도 멈춤 없음), 한국 소총병은 그대로 착검 멈춤. KD_SELFTEST_ONLY=progbar.</summary>
public static class ProgBarSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        Console.WriteLine("[progbar 일본군 처음부터 착검]");
        using var nd = TestDefs.NoCommandDelay();
        World Mk()
        {
            var m = new GameMap(120, 120, 1);
            for (int y = 0; y < 120; y++) for (int x = 0; x < 120; x++) m.Set(x, y, TerrainType.Grass);
            var w = new World(m, defs, 2) { RecordCommands = false };
            w.PlaceBuilding(defs.Headquarters, 1, 1, 1);
            w.PlaceBuilding(defs.Headquarters, 2, 119 - defs.Headquarters.Size, 119 - defs.Headquarters.Size);
            w.Fog.Enabled = false;
            return w;
        }
        (int fixMax, bool fixedNow, int stand) Run1(string def)
        {
            var w = Mk();
            var u = w.SpawnUnit(defs.Units[def], 1, new Vector2(40, 60));
            var p0 = u.Pos; int fixMax = 0, stand = 999;
            w.Issue(new ChargeCommand(1, new[] { u.Id }, new Vector2(40, 30), 0) { Sel = 1 });
            for (int t = 0; t < 400; t++)
            {
                w.Step(); fixMax = Math.Max(fixMax, u.FixLeft);
                if (stand == 999 && Vector2.Distance(u.Pos, p0) > 0.01f) stand = t;
            }
            return (fixMax, u.BayonetFixed, stand);
        }
        var jp = Run1("infantry_japanese"); var kr = Run1("infantry");
        check(jp.fixMax == 0 && jp.fixedNow && jp.stand <= 4, $"일본 보병 돌격 명령: 착검 대기 {jp.fixMax}틱 · 출발 {jp.stand}틱 · 착검됨 {jp.fixedNow}");
        check(kr.fixMax > 0 && kr.fixedNow && kr.stand > 4, $"한국 소총병 돌격 명령: 착검 {kr.fixMax}틱 · 출발 {kr.stand}틱");
    }
}
