using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.Tests;

/// <summary>
/// N2 (HANDOFF 4o, 2026-10-19) 경기관총 사격 방향 · 스프라이트 정합:
/// (1) 거치한 경기관총은 표적 방위 ±5° 안에서만 쏜다 (16방위 + 칸 사이 방위), 거치 상태에서 조준 회전은 traverseRate (90°/s) 로 천천히 — 반대편 표적은 다시 거치하지 않고 돌려서 쏜다.
/// (2) 그림: 경기관총 시트는 9행 (22.5° · 반전 포함 16방향) · 넓은 칸 (192/96/48), 사격 순간 그려진 행 방향과 표적 방위 차이 ≤ 11.25° (+ 행 히스테리시스 4°).
/// (3) 행 히스테리시스 (UnitRenderer.SnapFacing): 경계에서 흔들려도 행이 뒤집히지 않고, 표시 방향 오차는 항상 반폭 + 4° 이하.
/// 총열 축 자체 (그림 속 총이 행 방향과 평행한지) 는 game-tools `python image/tools/mg_axis.py` (최대 오차 0°, 반동 칸 ≤ 1.3°).
/// </summary>
public static class LmgAimSelfTest
{
    const float Deg = MathF.PI / 180f;

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();
        try
        {
            Sheets(check);
            FireBearings(opt, check);
            Traverse(opt, check);
            Hysteresis(check);
        }
        catch (Exception e) { check(false, $"경기관총 조준 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World NewWorld(int seed)
    {
        var m = TestDefs.Flat(GameMap.Generate(100, 100, seed));
        for (int y = 30; y < 70; y++)
            for (int x = 30; x < 70; x++) m.Set(x, y, TerrainType.Grass);   // 가운데 40×40 = 풀밭 (사선 · 시야 막힘 없음)
        var w = TestDefs.Strip(World.CreateStandard(m, Defs.Current));
        w.Fog.Enabled = false;
        return w;
    }

    static Vector2 Center(World w)
    {
        var (cx, cy) = w.Map.NearestWalkable(w.Map.Width / 2, w.Map.Height / 2);
        return new Vector2(cx + 0.5f, cy + 0.5f);
    }

    static float Wrap(float a) => a - MathF.Tau * MathF.Floor((a + MathF.PI) / MathF.Tau);

    /// <summary>시트 행 (row, flip) 이 그리는 방향 (facing 라디안, 0 = 오른쪽, π/2 = 아래).</summary>
    static float RowFacing(int row, bool flip, int dirCount)
    {
        float yaw = row * 180f / (dirCount - 1);
        if (flip) yaw = 360f - yaw;
        return (90f - yaw) * Deg;
    }

    static readonly string[] Variants = { "heavy-machinegunner", "heavy-machinegunner-japanese", "heavy-machinegunner-chauchat-fengtian", "heavy-machinegunner-russian-white", "heavy-machinegunner-russian-red" };

    static void Sheets(Action<bool, string> check)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "assets", "themes", "kd1920", "units");
        foreach (var v in Variants)
        {
            var bad = new List<string>();
            foreach (var (tag, fw) in new[] { ("", 192), ("@64", 96), ("@32", 48) })
            {
                string p = Path.Combine(root, v, $"{v}{tag}_layout.json");
                if (!File.Exists(p)) { bad.Add($"{tag} 없음"); continue; }
                using var doc = JsonDocument.Parse(File.ReadAllText(p));
                var r = doc.RootElement;
                int dirs = r.GetProperty("dirs").GetArrayLength(), w = r.GetProperty("frame")[0].GetInt32();
                bool mirror = r.TryGetProperty("mirror", out var m) && m.ValueKind == JsonValueKind.True;
                if (dirs != 9 || w != fw || !mirror) bad.Add($"{tag}: 행 {dirs} · 폭 {w} · 반전 {mirror}");
                string an = Path.Combine(root, v, r.GetProperty("anchors").GetString()!);
                if (!File.Exists(an)) { bad.Add($"{tag} 기준점 없음"); continue; }
                using var adoc = JsonDocument.Parse(File.ReadAllText(an));
                int cols = r.GetProperty("columns").GetArrayLength();
                foreach (var d in r.GetProperty("dirs").EnumerateArray())
                    if (!adoc.RootElement.TryGetProperty(d.GetString()!, out var row) || row.GetArrayLength() != cols) bad.Add($"{tag} 기준점 행 {d.GetString()}");
            }
            check(bad.Count == 0, $"경기관총 시트 {v}: 9행 (16방향) · 칸 폭 192/96/48 · 칸별 기준점 {(bad.Count == 0 ? "" : string.Join(", ", bad))}");
        }
    }

    static void FireBearings(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var mgDef = defs.Units["heavy_machinegunner"];
        check(mgDef.TraverseRate > 0 && mgDef.SetupTime > 0, $"경기관총 정의: 거치 {mgDef.SetupTime}초 · 거치 조준 회전 {mgDef.TraverseRate}°/s");
        var lod = new LodSprite { DirCount = 9, Mirror = true };
        int n = 0, okFire = 0, okAim = 0, okRow = 0;
        float worstAim = 0, worstRow = 0;
        var fails = new List<string>();
        for (int k = 0; k < 32; k++)
        {
            float bearingDeg = k < 16 ? k * 22.5f : (k - 16) * 22.5f + 9f;   // 16 행 방향 + 칸 안쪽 (경계 근처) 방위
            float b = Wrap(bearingDeg * Deg);
            var w = NewWorld(opt.Seed + k);
            var c = Center(w);
            var mg = w.SpawnUnit(mgDef, 1, c);
            mg.Facing = Wrap(b + MathF.PI * 0.6f);   // 엉뚱한 쪽을 보고 시작
            var foe = w.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(MathF.Cos(b), MathF.Sin(b)) * 12f);
            foe.Weapon = null; w.Hold(2, new[] { foe });
            w.Hold(1, new[] { mg });
            bool fired = false, deployedAtShot = false; float aimErr = 0;
            for (int i = 0; i < 60 * 12 && !fired; i++)
            {
                foe.Hp = 100000;
                w.Step();
                if (mg.LastShotTick == w.Tick - 1)
                {
                    fired = true; deployedAtShot = mg.Deployed;
                    float want = DetMath.Atan2(foe.Pos.Y - mg.Pos.Y, foe.Pos.X - mg.Pos.X);
                    aimErr = MathF.Abs(Wrap(mg.Facing - want));
                }
            }
            n++;
            if (fired && deployedAtShot) okFire++; else fails.Add($"{bearingDeg}° 사격 {fired} 거치 {deployedAtShot} (표적 {(mg.Target?.Id.ToString() ?? "-")} 자세 {mg.Deploy} 방향 {mg.Facing / Deg:0} 거리 {Vector2.Distance(mg.Pos, foe.Pos):0.0} 사선 {w.Map.HasLineOfSight(mg.Pos, foe.Pos)})");
            if (fired && aimErr <= World.FireAngleTolerance + 1e-4f) okAim++; else if (fired) fails.Add($"{bearingDeg}° 조준 오차 {aimErr / Deg:0.0}°");
            worstAim = MathF.Max(worstAim, aimErr / Deg);
            // 그림: 처음 그리는 순간 (히스테리시스 없음) 의 행 · 행 방향 vs 표적 방위
            float shown = UnitRenderer.SnapFacing(false, 0, mg.Facing, lod.DirCount);
            int row = lod.RowOf(shown, out bool flip);
            float rowErr = MathF.Abs(Wrap(RowFacing(row, flip, lod.DirCount) - b)) / Deg;
            worstRow = MathF.Max(worstRow, rowErr);
            if (rowErr <= 11.25f + 5f + 0.01f) okRow++; else fails.Add($"{bearingDeg}° 행 {row}{(flip ? "R" : "")} 오차 {rowErr:0.0}°");
        }
        check(okFire == n, $"경기관총 16방위 + 칸 사이 {n}방위: 모두 거치한 뒤 사격 ({okFire}/{n}) {string.Join("; ", fails.Take(4))}");
        check(okAim == n, $"경기관총 사격 순간 몸 방향 = 표적 방위 ±5° ({okAim}/{n}, 최대 {worstAim:0.00}°)");
        check(okRow == n, $"경기관총 그려진 행 방향 vs 표적 방위 ≤ 11.25° + 조준 허용 5° ({okRow}/{n}, 최대 {worstRow:0.0}°)");
    }

    /// <summary>거치한 채 반대편 표적: 다시 거치하지 않고 traverseRate 로 돌려서 쏜다 (틱당 회전 ≤ 90°/60).</summary>
    static void Traverse(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var mgDef = defs.Units["heavy_machinegunner"];
        var w = NewWorld(opt.Seed);
        var c = Center(w);
        var mg = w.SpawnUnit(mgDef, 1, c);
        mg.Facing = 0;
        var east = w.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(10, 0));
        east.Weapon = null; w.Hold(2, new[] { east });
        w.Hold(1, new[] { mg });
        for (int i = 0; i < 60 * 10 && mg.LastShotTick < 0; i++) { east.Hp = 100000; w.Step(); }
        bool firstOk = mg.LastShotTick >= 0 && mg.Deployed;
        // 동쪽 표적 제거 → 서쪽에 새 표적
        east.Hp = 0;
        w.Step();
        var west = w.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(-10, 0.01f));
        west.Weapon = null; w.Hold(2, new[] { west });
        long t0 = w.Tick, shot = -1; float maxStep = 0, prev = mg.Facing; bool stayed = true;
        for (int i = 0; i < 60 * 8 && shot < 0; i++)
        {
            west.Hp = 100000;
            long before = mg.LastShotTick;
            w.Step();
            maxStep = MathF.Max(maxStep, MathF.Abs(Wrap(mg.Facing - prev))); prev = mg.Facing;
            if (mg.Deploy != DeployPhase.Deployed) stayed = false;
            if (mg.LastShotTick != before && mg.Target == west) shot = w.Tick - 1;
        }
        float perTick = mgDef.TraverseRadPerSec * Config.SimDt;
        float secs = shot < 0 ? -1 : (shot - t0) * Config.SimDt;
        check(firstOk && shot > 0 && stayed, $"거치 경기관총: 반대편 표적은 거치를 풀지 않고 돌려서 쏜다 (첫 발 {firstOk}, 서쪽 첫 발 {secs:0.00}초, 거치 유지 {stayed})");
        check(maxStep <= perTick + 1e-4f && secs >= MathF.PI / mgDef.TraverseRadPerSec - 0.1f,
              $"거치 조준 회전 ≤ {mgDef.TraverseRate}°/s (틱당 최대 {maxStep / Deg:0.00}° ≤ {perTick / Deg:0.00}°, 180° 회전 {secs:0.00}초 ≥ {180f / mgDef.TraverseRate:0.00}초)");
    }

    static void Hysteresis(Action<bool, string> check)
    {
        foreach (int dirs in new[] { 5, 9 })
        {
            var lod = new LodSprite { DirCount = dirs, Mirror = true };
            float step = 180f / (dirs - 1), bound = step * 0.5f + UnitRenderer.RowHysteresis / Deg + 1e-3f;
            bool has = false; float shown = 0, worst = 0; int flips = 0, lastRow = -1; bool lastFlip = false;
            // 천천히 한 바퀴 + 경계 위에서 ±2° 흔들기 (정남 · 정북 반전 경계 포함)
            var seq = new List<float>();
            for (float a = -180; a <= 180; a += 0.5f) seq.Add(a);
            foreach (float edge in new[] { 90f + step * 0.5f, 90f, -90f, 0f + step * 0.5f })
                for (int i = 0; i < 40; i++) seq.Add(edge + (i % 2 == 0 ? 2f : -2f));
            int jitterFlips = 0;
            for (int i = 0; i < seq.Count; i++)
            {
                float f = seq[i] * Deg;
                shown = UnitRenderer.SnapFacing(has, shown, f, dirs); has = true;
                worst = MathF.Max(worst, MathF.Abs(Wrap(shown - f)) / Deg);
                int row = lod.RowOf(shown, out bool flip);
                if (lastRow >= 0 && (row != lastRow || flip != lastFlip)) { flips++; if (i >= 721 && (i - 721) % 40 >= 2) jitterFlips++; }
                lastRow = row; lastFlip = flip;
            }
            check(worst <= bound && jitterFlips == 0,
                  $"행 히스테리시스 ({dirs}행): 표시 방향 오차 최대 {worst:0.0}° ≤ 반폭 + 4° ({bound:0.0}°), 경계 ±2° 흔들림에 행 바뀜 {jitterFlips}");
        }
    }
}
