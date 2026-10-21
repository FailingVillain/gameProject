using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N16 공간 해시 (World.RebuildHash, 점유 칸만 지우고 정렬하는 방식) 대 무차별 대입 기준: 무작위 배치 · 같은 칸에 여럿 · 지도 밖 좌표 (가장자리로 잘림) ·
/// 칸 사이 이동 · 유닛 수 증감을 여러 번 반복하며 (1) 칸별 목록과 순서 (칸 안 번호 내림차순), (2) 전체 순서 (칸 오름차순 → Separate 순서),
/// (3) UnitsInRadius 결과와 순서, (4) 칸 · 8×8 묶음 세력 비트가 기준과 정확히 같은지 본다. `KD_SELFTEST_ONLY=hash`.
/// </summary>
public static class SpatialHashSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            Layouts(check, 64, 48, 1);
            Layouts(check, 37, 91, 2);   // 8 의 배수가 아닌 크기 (묶음 가장자리)
        }
        catch (Exception e) { check(false, $"공간 해시 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static int CellOf(GameMap m, Vector2 p) => Math.Clamp((int)p.Y, 0, m.Height - 1) * m.Width + Math.Clamp((int)p.X, 0, m.Width - 1);

    static void Layouts(Action<bool, string> check, int W, int H, int seed)
    {
        var w = TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(W, H, seed)), Defs.Current));
        w.Fog.Enabled = false;
        w.Units.Clear();
        var rng = new Random(1234 + seed);
        var def = Defs.Current.Units["infantry"];
        var m = w.Map;
        Vector2 RandPos()
        {
            int r = rng.Next(10);
            if (r == 0) return new Vector2(rng.Next(-3, W + 4) + 0.3f, rng.Next(-3, H + 4) + 0.7f);   // 지도 밖 → 가장자리 칸
            if (r <= 3) return new Vector2(5.5f + (float)rng.NextDouble() * 0.9f, 7.2f);              // 같은 칸에 몰림
            return new Vector2((float)rng.NextDouble() * W, (float)rng.NextDouble() * H);
        }
        for (int i = 0; i < 250; i++) { var u = w.SpawnUnit(def, 1 + rng.Next(3) % 2, new Vector2(W / 2f, H / 2f)); u.Pos = RandPos(); }

        int bad = 0, rounds = 0, queries = 0;
        string firstBad = "";
        void Fail(string s) { if (bad++ == 0) firstBad = s; }
        var got = new List<int>();
        var near = new List<Unit>();
        for (int round = 0; round < 40; round++, rounds++)
        {
            // 이동: 일부는 작은 걸음 (같은 칸 · 옆 칸), 일부는 멀리 (다른 8×8 묶음), 가끔 유닛 증감
            foreach (var u in w.Units)
            {
                int r = rng.Next(4);
                if (r == 0) u.Pos = RandPos();
                else if (r == 1) u.Pos += new Vector2((float)rng.NextDouble() * 2.4f - 1.2f, (float)rng.NextDouble() * 2.4f - 1.2f);
            }
            if (round % 7 == 3) { int drop = Math.Min(w.Units.Count - 1, 40 + rng.Next(40)); w.Units.RemoveRange(w.Units.Count - drop, drop); }
            if (round % 7 == 5) for (int i = 0; i < 60 + rng.Next(60); i++) { var u = w.SpawnUnit(def, 1 + rng.Next(2), new Vector2(W / 2f, H / 2f)); u.Pos = RandPos(); }
            if (round == 30 || round == 31) { if (w.Units.Count > 20) w.Units.RemoveRange(20, w.Units.Count - 20); }   // 점유 칸 64 미만 (삽입 정렬 경로)
            if (round == 20) w.Units.Clear();   // 빈 월드 → 다음 라운드에 다시 채움
            if (round == 21) for (int i = 0; i < 200; i++) { var u = w.SpawnUnit(def, 1 + rng.Next(2), new Vector2(W / 2f, H / 2f)); u.Pos = RandPos(); }
            w.MarkHashDirtyForTest();

            int n = w.Units.Count;
            // 기준: (칸 오름차순, 번호 내림차순)
            var cellOf = new int[n];
            for (int i = 0; i < n; i++) cellOf[i] = CellOf(m, w.Units[i].Pos);
            var expOrder = Enumerable.Range(0, n).OrderBy(i => cellOf[i]).ThenByDescending(i => i).ToArray();
            var order = w.HashOrderForTest();
            if (!order.SequenceEqual(expOrder)) Fail($"라운드 {round}: 전체 순서 다름 (n={n})");
            // 칸별 목록 (모든 칸 — 빈 칸은 비어 있어야 함)
            var byCell = new Dictionary<int, List<int>>();
            foreach (int i in expOrder) { if (!byCell.TryGetValue(cellOf[i], out var l)) byCell[cellOf[i]] = l = new List<int>(); l.Add(i); }
            for (int c = 0; c < W * H; c++)
            {
                w.HashCellForTest(c, got);
                var exp = byCell.TryGetValue(c, out var l) ? l : new List<int>();
                if (!got.SequenceEqual(exp)) { Fail($"라운드 {round}: 칸 {c} 목록 다름 ({string.Join(",", got)} vs {string.Join(",", exp)})"); break; }
            }
            // 세력 비트
            var (cm, crs) = w.HashMasksForTest();
            var expCm = new uint[W * H];
            var cw = (W + 7) >> 3;
            var expCrs = new uint[cw * ((H + 7) >> 3)];
            for (int i = 0; i < n; i++)
            {
                uint bit = 1u << (w.Units[i].Owner & 31);
                int c = cellOf[i];
                expCm[c] |= bit;
                expCrs[(c / W >> 3) * cw + ((c % W) >> 3)] |= bit;
            }
            if (!cm.SequenceEqual(expCm)) Fail($"라운드 {round}: 칸 세력 비트 다름");
            if (!crs.SequenceEqual(expCrs)) Fail($"라운드 {round}: 8×8 묶음 세력 비트 다름");
            // 반경 질의 (결과 · 순서)
            for (int q = 0; q < 30; q++, queries++)
            {
                var center = new Vector2((float)rng.NextDouble() * (W + 4) - 2, (float)rng.NextDouble() * (H + 4) - 2);
                float rad = (float)rng.NextDouble() * 9f;
                w.UnitsInRadius(center, rad, near);
                float r2 = rad * rad;
                var exp = expOrder.Where(i => w.Units[i].Alive && Vector2.DistanceSquared(w.Units[i].Pos, center) <= r2).Select(i => w.Units[i]).ToList();
                if (!near.SequenceEqual(exp)) { Fail($"라운드 {round}: 반경 질의 다름 ({near.Count} vs {exp.Count})"); break; }
            }
        }
        check(bad == 0, $"공간 해시 = 무차별 기준 ({W}×{H}, {rounds}라운드 · 반경 질의 {queries}회, 칸별 순서 · 전체 순서 · 세력 비트 · 칸 사이 이동 · 유닛 증감){(bad > 0 ? $" — 불일치 {bad}: {firstBad}" : "")}");
    }
}
