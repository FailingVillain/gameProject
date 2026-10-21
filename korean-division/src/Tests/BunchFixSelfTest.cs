using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// bunchfix (2026-10-19, docs/BUNCHING_DIAG_2026-10-19.md): 행군 종대의 자취 부족 뭉침 · 종대 깜빡임 · 막힌 슬롯 한 점 모임 · 지도 가장자리 배치.
/// 창 없이. KD_SELFTEST_ONLY=bunchfix 로 단독 실행.
/// </summary>
public static class BunchFixSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[bunchfix 행군 종대 뭉침]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestShortTrailDistinctRows();
            TestExtrapolationRespectsBlocked();
            TestNarrowIgnoresSmallObstacle();
            TestBlockedSlotFallbackSpread();
            TestDeploymentInsideMap();
        }
        catch (Exception e) { check(false, $"bunchfix 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    /// <summary>풀밭 지도 (block 이 주는 칸은 바위) + 소총 40 중대가 남쪽 (y+) 으로 행군을 시작한 직후의 깃발.</summary>
    static (World W, Company C, CompanyAnchor A, List<Unit> Us) Marching(Func<int, int, bool>? block = null)
    {
        const int size = 120;
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, block != null && block(x, y) ? TerrainType.Rock : TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        var us = new List<Unit>();
        for (int i = 0; i < 40; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(50.5f + i % 20, 20.5f + i / 20)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        w.Move(1, us, new Vector2(60, 110));
        for (int t = 0; t < 3 && c.Anchor == null; t++) w.Step();
        return (w, c, c.Anchor!, us);
    }

    /// <summary>깃발을 p 로 옮기고 (자취 = 그 한 점, 진행 방향 남쪽) — 자취가 막 시작된 상태.</summary>
    static void PlaceFlag(CompanyAnchor a, Vector2 p)
    {
        a.Pos = p; a.Flag.Pos = p; a.Heading = new Vector2(0, 1);
        a.Trail.Clear(); a.Trail.Add(p);
    }

    static float MinPairDist(IReadOnlyList<Vector2> ps)
    {
        float best = float.MaxValue;
        for (int i = 0; i < ps.Count; i++)
            for (int j = i + 1; j < ps.Count; j++) best = MathF.Min(best, Vector2.Distance(ps[i], ps[j]));
        return best;
    }

    static bool Walk(World w, Vector2 p)
    {
        var (x, y) = GameMap.TileOf(p);
        return w.Map.IsWalkable(x, y);
    }

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var cr = defs.CombatRules;
        check(cr.NarrowBlockFrac is > 0.2f and < 0.3f && cr.NarrowClearTicks >= 120,
            $"bunchfix 수치 (balance.json): 종대 진입 = 폭 선 {cr.NarrowBlockFrac:P0} 막힘, 해제 = {cr.NarrowClearTicks}틱 연속 트임 (옛 60)");
    }

    // ---------------------------------------------------------------- ① 자취가 짧아도 줄마다 다른 자리

    static void TestShortTrailDistinctRows()
    {
        var (w, c, a, us) = Marching();
        PlaceFlag(a, new Vector2(60, 60));
        w.EnterColumn(c, a);
        int rows = (a.Slots.Count + 1) / 2 + 1;
        var rp = Enumerable.Range(0, rows).Select(r => a.RowPos[r]).ToList();
        float rowMin = MinPairDist(rp);
        var slots = us.Select(u => w.MarchSlot(a, u)).ToList();
        float slotMin = MinPairDist(slots);
        float tail = Vector2.Distance(a.RowPos[rows - 1], a.Pos);
        check(a.Column && rowMin >= a.Spacing * 0.9f && slotMin >= 0.4f && MathF.Abs(tail - (rows - 1) * a.Spacing) < 0.05f && a.RowReach == rows,
            $"자취 1점 종대: 줄 {rows}개 최소 간격 {rowMin:0.00} (간격 {a.Spacing:0.00}), 병사 슬롯 최소 거리 {slotMin:0.00}, 꼬리 {tail:0.0}칸 뒤 (옛: 뒷줄 전원이 한 점)");
    }

    // ---------------------------------------------------------------- ① 외삽 줄은 막힌 칸을 넘지 않는다

    static void TestExtrapolationRespectsBlocked()
    {
        // 깃발 (60, 60) 7칸 뒤 (y = 53) 에 동서로 긴 바위 벽 — 21줄 (≈19칸) 종대의 꼬리는 벽 앞에서 흩어 선다
        var (w, c, a, us) = Marching((x, y) => y == 53 && x >= 30 && x <= 90);
        PlaceFlag(a, new Vector2(60, 60));
        w.EnterColumn(c, a);
        int rows = (a.Slots.Count + 1) / 2 + 1;
        bool rowsOk = true;
        for (int r = 0; r < rows; r++) rowsOk &= Walk(w, a.RowPos[r]) && a.RowPos[r].Y > 53.9f;
        var slots = us.Select(u => w.MarchSlot(a, u)).ToList();
        bool slotsOk = slots.TrueForAll(p => Walk(w, p));
        float slotMin = MinPairDist(slots);
        check(a.RowReach < rows && rowsOk && slotsOk && slotMin >= 0.3f,
            $"외삽 종대 + 뒤쪽 벽: 닿는 줄 {a.RowReach}/{rows}, 줄 위치 모두 벽 앞 · 설 수 있는 칸 {rowsOk}, 병사 슬롯 모두 설 수 있음 {slotsOk}, 최소 거리 {slotMin:0.00}");
    }

    // ---------------------------------------------------------------- ② 작은 장애물로는 종대가 되지 않는다

    static void TestNarrowIgnoresSmallObstacle()
    {
        // 깃발 (60, 60) 3칸 앞 폭 선 위에 2×2 바위 (집 한 채 크기)
        var (w, c, a, _) = Marching((x, y) => x is 56 or 57 && y is 62 or 63);
        PlaceFlag(a, new Vector2(60.5f, 60.5f));
        a.Column = false;
        float frac = w.NarrowBlockedFrac(a, a.Pos + a.Heading * 2f);
        w.CheckNarrow(c, a);
        bool small = !a.Column;
        // 대조: 앞 3칸에 폭 6칸 틈만 남은 동서 벽 → 종대
        var (w2, c2, a2, _) = Marching((x, y) => y == 63 && (x < 58 || x > 63));
        PlaceFlag(a2, new Vector2(60.5f, 60.5f));
        a2.Column = false;
        w2.CheckNarrow(c2, a2);
        check(small && frac > 0 && frac < defs.CombatRules.NarrowBlockFrac && a2.Column,
            $"좁은 길 판정: 2×2 장애물 (폭 선 {frac:P0} 막힘) → 종대 아님 {small}, 6칸 틈 벽 → 종대 {a2.Column}");
    }

    // ---------------------------------------------------------------- ④ 막힌 슬롯의 대체 위치는 흩어진다

    static void TestBlockedSlotFallbackSpread()
    {
        // 깃발 (60, 60) 횡대 슬롯 가운데 8칸을 덮는 바위 (깃발 칸은 비움)
        var (w, c, a, us) = Marching((x, y) => (x is >= 52 and <= 58 || x is >= 63 and <= 68) && y is >= 58 and <= 62);
        PlaceFlag(a, new Vector2(60.5f, 60.5f));
        a.Column = false;
        var right = new Vector2(-a.Heading.Y, a.Heading.X);
        var blocked = new List<Vector2>();
        foreach (var u in us)
        {
            var l = a.Slots[u.Id];
            var raw = a.Pos + right * l.X + a.Heading * l.Y;
            if (Walk(w, raw)) continue;
            blocked.Add(w.MarchSlot(a, u));
        }
        bool allWalk = blocked.TrueForAll(p => Walk(w, p));
        int atFlag = blocked.Count(p => Vector2.Distance(p, a.Pos) < 0.05f);
        int distinct = blocked.Select(p => ((int)MathF.Round(p.X * 10), (int)MathF.Round(p.Y * 10))).Distinct().Count();
        check(blocked.Count >= 4 && allWalk && atFlag == 0 && distinct >= blocked.Count * 0.6f,
            $"막힌 행군 슬롯 {blocked.Count}개의 대체 위치: 모두 설 수 있음 {allWalk}, 깃발 점 {atFlag}개 (옛: 전부), 서로 다른 자리 {distinct}개");
    }

    // ---------------------------------------------------------------- 시작 배치가 지도 안

    static void TestDeploymentInsideMap()
    {
        var def = MissionDef.Load("ai_kr_vs_ft_1000");
        var w = World.CreateMissionWorld(def, defs, 1, new List<DeployEntry>());
        float lo = World.DeployEdgeMargin - 0.01f;
        bool layoutIn = true;
        foreach (var f in def.Forces)
        {
            var at = f.At is { Length: 2 } p ? new Vector2(p[0], p[1]) : Vector2.Zero;
            foreach (var (_, pos, _) in w.LayoutForce(f, at, MissionDef.DirOf(f.Facing)))
                layoutIn &= pos.X >= lo && pos.Y >= lo && pos.X <= w.Map.Width - lo && pos.Y <= w.Map.Height - lo;
        }
        float worst = float.MaxValue; int worstId = 0;
        foreach (var c in w.Companies)
        {
            var ms = c.Members.Where(u => u.Alive && !u.IsOfficer && !u.Def.Mounted && !u.Large).ToList();
            if (ms.Count < 30) continue;
            double s = 0;
            foreach (var u in ms) s += ms.Where(o => o != u).Min(o => Vector2.Distance(u.Pos, o.Pos));
            float nn = (float)(s / ms.Count);
            if (nn < worst) { worst = nn; worstId = c.Id; }
        }
        check(layoutIn && worst >= 0.85f,
            $"ai_kr_vs_ft_1000 시작 배치: 모든 병력 대형이 지도 안 (여유 {World.DeployEdgeMargin}칸) {layoutIn}, 중대 최근접 이웃 평균 최소 {worst:0.00} (중대 {worstId}, 옛 가장자리 중대 ≈0.5)");
    }
}
