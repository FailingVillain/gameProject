using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T32a 전투공병 (infantry_pioneer): 철조망 절단 전용 병종 데이터 시험. 로드 · 필드, 공병 1명이 CutWire 로 한 칸을 wireCutSec 안에 엶,
/// 일반 소총병은 CutWire 무시, 돌격보병 절단 회귀, 공병 연막 수류탄 0, 한국군 전용. `KD_SELFTEST_ONLY=pioneer`.
/// </summary>
public static class PioneerSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[T32a 전투공병 — 철조망 절단 전용]");
        try
        {
            using var _ = TestDefs.NoCommandDelay();
            var defs = Defs.Current;
            bool has = defs.Units.TryGetValue("infantry_pioneer", out var p);
            check(has && p!.WireCutter && p.Category == "infantry" && p.CanCapture && p.Weapon == "rifle_mosin" && p.KoreaOnly && p.SmokeGrenades == 0 && p.Grenades == 2,
                $"전투공병 정의: 로드 {has} · WireCutter · 소총 · 한국군 전용 · 연막 수류탄 0 · 수류탄 2");
            check(has && p!.AllowedFor("korean") && !p.AllowedFor("japanese") && !p.AllowedFor("fengtian"), "전투공병: 한국군만 편성 가능");
            if (!has) return;

            // (a) 공병 1명이 철조망 1칸을 연다
            var w = Bare(wireRow: 40);
            var pi = w.SpawnUnit(p!, 1, new Vector2(30.5f, 35.5f));
            w.Hold(1, new[] { pi });
            w.Step();
            bool before = w.Map.Get(30, 40) == TerrainType.Wire && !w.Map.IsWalkable(30, 40);
            w.CutWire(1, new[] { pi }, new Vector2(30.5f, 40.5f));
            Steps(w, 3);
            bool assigned = pi.CuttingWire;
            int t = 0;
            while (t < Sec(20) && w.Map.Get(30, 40) == TerrainType.Wire) { w.Step(); t++; }
            bool open = w.Map.Get(30, 40) != TerrainType.Wire && w.Map.IsWalkable(30, 40);
            float sec = t * Config.SimDt;
            bool time = sec >= defs.Breach.WireCutSec - 0.1f && sec < defs.Breach.WireCutSec + 8f;
            bool others = w.Map.Get(29, 40) == TerrainType.Wire && w.Map.Get(31, 40) == TerrainType.Wire && !pi.CuttingWire;
            check(before && assigned && open && time && others, $"공병 절단: 맡음 {assigned} · {sec:0.0}초 뒤 칸 통과 가능 {open} (시간 {time}, wireCutSec {defs.Breach.WireCutSec}) · 옆 칸 그대로 · 끝나면 해제 {others}");

            // (b) 일반 소총병은 CutWire 를 받아도 무시
            var w2 = Bare(wireRow: 40);
            var rifle = w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30.5f, 35.5f));
            w2.Hold(1, new[] { rifle });
            w2.Step();
            w2.CutWire(1, new[] { rifle }, new Vector2(30.5f, 40.5f));
            Steps(w2, Sec(defs.Breach.WireCutSec + 4));
            check(!rifle.CuttingWire && w2.Map.Get(30, 40) == TerrainType.Wire, "소총병: 절단 명령 무시 (칸 그대로)");

            // (c) 돌격보병 회귀
            var w3 = Bare(wireRow: 40);
            var asl = w3.SpawnUnit(defs.Units["infantry_assault"], 1, new Vector2(30.5f, 35.5f));
            w3.Hold(1, new[] { asl });
            w3.Step();
            w3.CutWire(1, new[] { asl }, new Vector2(30.5f, 40.5f));
            int t3 = 0;
            while (t3 < Sec(20) && w3.Map.Get(30, 40) == TerrainType.Wire) { w3.Step(); t3++; }
            check(w3.Map.Get(30, 40) != TerrainType.Wire, $"돌격보병 절단 회귀: {t3 * Config.SimDt:0.0}초 뒤 열림");

            // (d) 공병은 연막 수류탄 0 — 던지기 명령을 받아도 연막이 안 생긴다
            var w4 = Bare();
            var pi4 = w4.SpawnUnit(p!, 1, new Vector2(20.5f, 40.5f));
            w4.Hold(1, new[] { pi4 });
            w4.Step();
            w4.ThrowSmokeAt(1, new[] { pi4 }, new Vector2(24.5f, 40.5f));
            Steps(w4, Sec(3));
            check(pi4.SmokeGrenades == 0 && w4.Smokes.Count == 0, $"공병 연막 수류탄 {pi4.SmokeGrenades} · 연막 {w4.Smokes.Count}");
        }
        catch (Exception e) { check(false, $"pioneer 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 90, int wireRow = -1)
    {
        var defs = Defs.Current;
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, y == wireRow && x >= 8 && x < size - 8 ? TerrainType.Wire : TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = true };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Round(s / Config.SimDt);
}
