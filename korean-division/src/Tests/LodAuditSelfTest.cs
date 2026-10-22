using System.Numerics;
using KoreanDivision.Core;
using KoreanDivision.Render;
using Raylib_cs;

namespace KoreanDivision.Tests;

/// <summary>
/// N19 (2026-10-19) 최대 확대 (LOD0) 포함 전 LOD 의 모션 누락 감사 — 읽기 전용 (그림 · 시뮬 값은 건드리지 않음). `KD_SELFTEST_ONLY=lodaudit`.
/// (1) 데이터: 전 유닛 정의 × 진영 변형 × LOD 레벨: 그림이 낼 수 있는 모든 프레임 (<see cref="UnitArt.ReachableFrames"/>) 이 아틀라스에 담기고
///     (LodSprite.Used) 레이아웃에 칸이 있고 모든 방향 행이 비어 있지 않은지 (시트 알파 검사). 없으면 "그릴 것이 없음" = 화면에서 사라지는 칸.
/// (2) 기능: 정의가 요구하는 모션 (걷기 · 사격 · 거치 · 사망) 에 프레임이 있는지 — 목록으로 보고만 한다 (새 렌더가 필요한 것).
/// (3) 동작 훑기: 월드를 돌리며 (대기 · 이동 · 공격 이동 · 사격 · 거치 · 철수 · 16방향 회전 · 피격) 매 틱 LodFrame 이 내는 프레임 · 행이 그려지는지.
/// </summary>
public static class LodAuditSelfTest
{
    static readonly string?[] Variants = { null, "japanese", "fengtian", "russian-white", "russian-red" };
    static readonly Dictionary<LodLevel, bool[]> filled = new();

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();
        try
        {
            bool loaded = false, window = false;
            if (!Raylib.IsWindowReady())   // 옛 형식 (ww2 상속) 시트는 GL 텍스처가 필요 — 숨긴 작은 창
            {
                Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
                Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
                Raylib.InitWindow(64, 64, "lodaudit"); window = true;
            }
            if (Theme.Current == null) { Theme.Use("kd1920"); loaded = true; }   // 선택 테스트는 창 없이 돈다 — 레이아웃 json 만 읽으면 되고 (GL 텍스처는 그릴 때) 시트는 CPU 로만 읽는다
            if (Theme.Current == null) { check(false, "lodaudit: kd1920 테마를 읽을 수 없음"); return; }
            try { DataAudit(check); Sweep(opt, check); }
            finally { if (loaded) Theme.Use(null); if (window) Raylib.CloseWindow(); }
        }
        catch (Exception e) { check(false, $"lodaudit 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    /// <summary>레벨의 [행 × 프레임 수 + 프레임] → 시트 칸에 불투명 픽셀이 하나라도 있는가 (LodAtlas.Build 와 같은 규칙).</summary>
    static unsafe bool[] Filled(LodLevel lv)
    {
        if (filled.TryGetValue(lv, out var cached)) return cached;
        var s = lv.Owner;
        int nf = s.FrameNames.Length, nd = Math.Max(1, s.DirCount);
        var res = new bool[nf * nd];
        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "themes", "kd1920");
        for (int i = 0; i < lv.SheetFiles.Length; i++)
        {
            var img = Raylib.LoadImage(Path.Combine(dir, lv.SheetFiles[i]));
            if (img.Width == 0) continue;
            Raylib.ImageFormat(ref img, PixelFormat.UncompressedR8G8B8A8);
            byte* b = (byte*)img.Data;
            for (int f = 0; f < nf; f++)
            {
                if (lv.CellSheet[f] != i) continue;
                int fw = lv.CellFW[f], fh = lv.CellFH[f];
                int x0 = lv.CellCol[f] * fw;
                for (int r = 0; r < nd; r++)
                {
                    int y0 = r * fh;
                    if (x0 + fw > img.Width || y0 + fh > img.Height) continue;
                    bool any = false;
                    for (int y = 0; y < fh && !any; y++)
                    {
                        byte* row = b + ((long)(y0 + y) * img.Width + x0) * 4;
                        for (int x = 0; x < fw; x++) if (row[x * 4 + 3] != 0) { any = true; break; }
                    }
                    res[r * nf + f] = any;
                }
            }
            Raylib.UnloadImage(img);
        }
        filled[lv] = res;
        return res;
    }

    /// <summary>한 시트 · 프레임 집합의 누락 사유 목록 (빈 목록 = 모든 LOD · 방향에서 그려짐).</summary>
    static List<string> Missing(LodSprite s, IEnumerable<int> frames, string what)
    {
        var bad = new List<string>();
        foreach (int f in frames.OrderBy(x => x))
        {
            string fn = (uint)f < (uint)s.FrameNames.Length ? s.FrameNames[f] : "#" + f;
            if ((uint)f >= (uint)s.FrameNames.Length) { bad.Add($"{what}{fn} 범위 밖"); continue; }
            if (!s.Used[f]) bad.Add($"{what}{fn} 아틀라스 제외 (UsedAnimNames)");
            for (int li = 0; li < s.Levels.Length; li++)
            {
                var lv = s.Levels[li];
                if (lv.CellSheet[f] < 0) { bad.Add($"{what}{fn} L{li} 레이아웃 칸 없음"); continue; }
                var fill = Filled(lv);
                int nf = s.FrameNames.Length;
                var rows = Enumerable.Range(0, Math.Max(1, s.DirCount)).Where(r => !fill[r * nf + f]).ToList();
                if (rows.Count > 0) bad.Add($"{what}{fn} L{li} 빈 행 {string.Join(",", rows)}");
            }
        }
        return bad;
    }

    static void DataAudit(Action<bool, string> check)
    {
        var hard = new List<string>();
        var soft = new List<string>();
        int sprites = 0;
        foreach (var (key, def) in Defs.Current.Units.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            foreach (var v in Variants)
            {
                var art = UnitArt.For(def, v);
                string tag = key + (v != null ? "@" + v : "");
                if (art.Lod == null) { if (v == null) soft.Add($"{tag}: LOD 그림 없음 (옛 형식 · 도형)"); continue; }
                sprites++;
                var (body, tur) = art.ReachableFrames();
                var bad = Missing(art.Lod, body, "");
                if (art.LodTurret != null) bad.AddRange(Missing(art.LodTurret, tur, "포탑 "));
                if (bad.Count > 0) hard.Add($"{tag}: {string.Join("; ", bad.Take(6))}{(bad.Count > 6 ? $" ... 외 {bad.Count - 6}" : "")}");
                if (v == null) foreach (var m in Capabilities(def, art)) soft.Add($"{tag}: {m}");
            }
        }
        foreach (var line in hard) Console.WriteLine("[lodaudit] 문제 " + line);
        foreach (var line in soft) Console.WriteLine("[lodaudit] 누락 " + line);
        check(hard.Count == 0, $"LOD 그림 감사: 그릴 수 없는 칸 · 레이아웃 누락 (스프라이트 {sprites}종, 문제 {hard.Count}건) {string.Join(" | ", hard.Take(3))}");
        Console.WriteLine($"[lodaudit] 모션 누락 (새 그림 필요, 합격 기준 아님): {soft.Count}건");
    }

    static IEnumerable<string> Capabilities(UnitDef d, UnitArt a)
    {
        var s = a.Lod!;
        bool vehicle = a.Tank || a.Vehicle;
        if (d.Speed > 0 && !vehicle && s.Anim("walk") == null && s.Anim("move") == null) yield return "걷기 (walk) 없음";
        if (d.Speed > 0 && vehicle && s.Anim("move") == null) yield return "차체 이동 (move) 없음";
        if (d.SetupTime > 0 && !vehicle && s.Anim("deploy") == null && s.Anim("unpack") == null) yield return "거치 · 방열 (deploy/unpack) 없음";
        if (d.SetupTime > 0 && !vehicle && s.Anim("pack") == null) yield return "철수 (pack) 없음";
        if (!a.HasDeath && !a.HasWreck && !a.Tank) yield return "사망 · 잔해 없음";
        if (d.Mounted && s.Anim("gallop") == null) yield return "질주 (gallop) 없음";
        if (d.Weapon != null && !vehicle && a.CanFireFrames == 0) yield return "사격 칸 (fire_a/b/c · fire · fire_prone) 없음";
    }

    // ───────── 동작 훑기 ─────────
    static World NewWorld(int seed)
    {
        var m = TestDefs.Flat(GameMap.Generate(100, 100, seed));
        for (int y = 30; y < 70; y++) for (int x = 30; x < 70; x++) m.Set(x, y, TerrainType.Grass);
        var w = TestDefs.Strip(World.CreateStandard(m, Defs.Current));
        w.Fog.Enabled = false;
        return w;
    }

    static void Sweep(GameOptions opt, Action<bool, string> check)
    {
        var problems = new List<string>();
        int ticks = 0, units = 0;
        foreach (var (key, def) in Defs.Current.Units.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var art = UnitArt.For(def, null);
            if (art.Lod == null || art.Tank || art.Vehicle) continue;
            units++;
            var w = NewWorld(opt.Seed);
            var (cx, cy) = w.Map.NearestWalkable(w.Map.Width / 2, w.Map.Height / 2);
            var c = new Vector2(cx + 0.5f, cy + 0.5f);
            var u = w.SpawnUnit(def, 1, c);
            var foe = w.SpawnUnit(Defs.Current.Units["infantry"], 2, c + new Vector2(0, -9f));
            foe.Weapon = null; w.Hold(2, new[] { foe });
            var s = art.Lod;
            int nf = s.FrameNames.Length;
            var fillL0 = Filled(s.Levels[0]);
            string phase = "";
            void Probe()
            {
                ticks++;
                if (!float.IsFinite(u.Facing)) { problems.Add($"{key} {phase}: 방향 값 {u.Facing}"); return; }
                float shown = UnitRenderer.SnapFacing(false, 0, u.Facing, s.DirCount);
                int row = s.RowOf(shown, out _);
                int f = art.LodFrame(u, w.Tick, false, false, w.MarchCommanded(u));
                if ((uint)f >= (uint)nf) problems.Add($"{key} {phase}: 프레임 {f} 범위 밖");
                else if ((uint)row >= (uint)Math.Max(1, s.DirCount)) problems.Add($"{key} {phase}: 행 {row} 범위 밖 (방향 {u.Facing})");
                else if (!s.Used[f] || !fillL0[row * nf + f]) problems.Add($"{key} {phase}: 프레임 {s.FrameNames[f]} 행 {row} LOD0 에서 그려지지 않음 (담김 {s.Used[f]})");
            }
            void Phase(string name, int n) { phase = name; for (int i = 0; i < n; i++) { foe.Hp = 1e9f; w.Step(); Probe(); } }
            Phase("대기", 120);
            w.AttackMove(1, new[] { u }, c + new Vector2(0, -3f));
            Phase("공격이동", 240);
            w.Hold(1, new[] { u });
            Phase("사격", 600);
            for (int k = 0; k < 16; k++) { u.Facing = k * MathF.Tau / 16f - MathF.PI; Phase($"방향{k}", 8); }
            w.Move(1, new[] { u }, c + new Vector2(6f, 4f));
            Phase("이동 (철수)", 300);
            w.Hold(1, new[] { u });
            Phase("정지", 120);
            u.LastDamagedTick = w.Tick; Phase("피격", 20);
        }
        foreach (var p in problems.Distinct().Take(40)) Console.WriteLine("[lodaudit] 문제 " + p);
        check(problems.Count == 0, $"LOD 동작 훑기: 보병 · 승무원 {units}종 × (대기 · 이동 · 사격 · 거치 · 철수 · 16방향 · 피격) {ticks}틱 중 그려지지 않은 틱 {problems.Count} {string.Join(" | ", problems.Distinct().Take(3))}");
    }
}
