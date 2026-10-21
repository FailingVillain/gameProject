using System.Diagnostics;
using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>전장의 안개 검증 (창 없이). SelfTest 가 호출한다.</summary>
public static class FogSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = World.CreateStandard(GameMap.Generate(opt.MapWidth, opt.MapHeight, opt.Seed), defs);
        var fog = w.Fog;
        var hq1 = Hq(w, 1);
        var hq2 = Hq(w, 2);
        var (ex, ey) = w.Map.StartTiles[1];

        // ---- 시작 상태
        check(fog.State(1, hq1.TileX, hq1.TileY) == 2 && fog.CanSee(1, hq1), "시작 시 아군 사령부 주변은 시야(2)");
        check(!fog.CanSee(1, hq2) && !fog.IsVisible(1, hq2.Pos), "적 사령부는 보이지 않음");
        check(fog.State(1, ex, ey) == 1 && fog.IsExplored(1, ex, ey), "지형은 처음부터 전부 탐색됨(1) — 적 시작 지점도 지형만 보임");
        var farSite = w.Buildings.Where(b => b.Def.Capturable && b.Owner == 0).OrderByDescending(b => Vector2.Distance(b.Pos, hq1.Pos)).First();
        check(!fog.CanSee(1, farSite) && fog.Ghosts(1).Any(g => g.Id == farSite.Id) && fog.Ghosts(1).Any(g => g.Id == hq2.Id && g.Owner == 2),
              "점령 시설(먼 중립 시설 · 적 대도시)은 처음부터 시작 모습으로 기억됨");
        check(fog.State(2, ex, ey) == 2 && fog.State(0, ex, ey) == 2, "적 세력은 자기 시작 지점이 보임 · 중립은 전부 보임");
        check(!fog.IsVisible(1, -1, 0) && !fog.IsExplored(1, w.Map.Width, 0), "맵 밖 타일은 미탐색");

        // ---- 정찰: 적 건물 옆에 유닛 → 보임, 떠나면 고스트
        var scout = w.SpawnUnit(defs.Units["infantry"], 1, Beside(hq2));
        scout.Weapon = null;
        w.Hold(1, new[] { scout });
        Vector2 home = w.Units.First(u => u.Owner == 1 && u != scout).Pos + new Vector2(0.6f, 0);
        Steps(w, 12);
        check(fog.CanSee(1, hq2) && fog.IsVisible(1, scout.Pos), "적 사령부 옆에 유닛을 두면 보임");
        check(!fog.Ghosts(1).Any(g => g.Id == hq2.Id), "보이는 동안엔 고스트가 아님");
        scout.Pos = home;
        Steps(w, 12);
        check(!fog.CanSee(1, hq2), "유닛이 떠나면 적 사령부는 다시 안 보임");
        check(fog.State(1, hq2.TileX, hq2.TileY) == 1 && fog.IsExplored(1, hq2.TileX, hq2.TileY), "떠난 자리는 탐색됨(1) 상태로 남음");
        var ghost = fog.Ghosts(1).FirstOrDefault(g => g.Id == hq2.Id);
        check(ghost.Id == hq2.Id && ghost.Owner == 2 && ghost.Def == hq2.Def && ghost.TileX == hq2.TileX && ghost.TileY == hq2.TileY,
              "마지막으로 본 적 사령부가 고스트로 남음");
        check(!fog.Ghosts(2).Any(g => g.Id == hq2.Id), "자기 건물은 고스트가 아님");

        // ---- 파괴된 건물: 자리를 다시 보면 고스트 삭제
        var depotDef = TestDefs.Wall(2);
        var (dx, dy) = FreeSpot(w, depotDef, new Vector2(w.Map.Width / 2f, w.Map.Height / 2f));
        var depot = w.PlaceBuilding(depotDef, 2, dx, dy)!;
        scout.Pos = Beside(depot);
        Steps(w, 12);
        check(fog.CanSee(1, depot), "중앙의 적 보급고를 정찰해 발견");
        scout.Pos = home;
        Steps(w, 12);
        check(!fog.CanSee(1, depot) && fog.Ghosts(1).Any(g => g.Id == depot.Id), "떠나면 보급고 고스트 생성");
        depot.Hp = 0;
        Steps(w, 12);
        check(fog.Ghosts(1).Any(g => g.Id == depot.Id), "안 보이는 곳에서 파괴되면 고스트는 유지");
        scout.Pos = new Vector2(dx + depotDef.Size / 2f + 1.5f, dy + depotDef.Size / 2f);
        Steps(w, 12);
        check(!fog.Ghosts(1).Any(g => g.Id == depot.Id), "파괴된 자리를 다시 보면 고스트 삭제");

        // ---- 자원 시설: 발자국이 보이면 보임, 탐색 후 고스트, 소유 변경은 다시 봤을 때 반영
        var site = w.Buildings.Where(b => b.Def.Capturable && b.Owner == 0)
                              .OrderByDescending(b => Vector2.Distance(b.Pos, hq1.Pos) + Vector2.Distance(b.Pos, hq2.Pos)).First();
        check(!fog.CanSee(1, site), "멀리 있는 중립 자원 시설은 처음엔 안 보임");
        scout.Pos = Beside(site);
        Steps(w, 12);
        check(fog.CanSee(1, site), "중립 자원 시설은 발자국이 보이면 보임");
        scout.Pos = home;
        Steps(w, 12);
        var siteGhost = fog.Ghosts(1).FirstOrDefault(g => g.Id == site.Id);
        check(siteGhost.Id == site.Id && siteGhost.Owner == 0, "탐색한 자원 시설은 고스트로 남음 (미니맵용)");
        site.Owner = 2;      // 시야 밖에서 적이 점령했다고 가정
        Steps(w, 12);
        check(fog.Ghosts(1).First(g => g.Id == site.Id).Owner == 0, "안 보이는 동안의 소유 변경은 고스트에 반영 안 됨");
        scout.Pos = Beside(site);
        Steps(w, 12);
        scout.Pos = home;
        Steps(w, 12);
        check(fog.Ghosts(1).First(g => g.Id == site.Id).Owner == 2, "다시 본 뒤에는 고스트 소유자 갱신");

        // ---- 안개 끄기
        fog.Enabled = false;
        check(fog.State(1, ex, ey) == 2 && fog.CanSee(1, hq2) && fog.IsExplored(1, ex, ey), "안개를 끄면 전부 보임");
        check(!fog.Ghosts(1).Any(), "안개를 끄면 고스트 없음");
        fog.Enabled = true;
        check(fog.State(1, hq2.TileX, hq2.TileY) == 1, "다시 켜면 이전 탐색 상태 유지");

        // ---- 위치 노출 세력
        var enemyUnit = w.SpawnUnit(defs.Units["infantry"], 2, Beside(hq2));
        Steps(w, 12);
        check(!fog.CanSee(1, hq2) && !fog.CanSee(1, enemyUnit), "노출 전: 적 사령부 · 적 유닛 모두 안 보임");
        w.Faction(2).Revealed = true;
        check(fog.CanSee(1, hq2) && !fog.IsVisible(1, hq2.Pos), "위치 노출된 세력의 건물은 시야 밖이어도 보임");
        check(!fog.CanSee(1, enemyUnit), "위치 노출은 건물만 — 유닛은 여전히 안 보임");
        check(!fog.Ghosts(1).Any(g => g.Id == hq2.Id), "노출되어 보이는 건물은 고스트로 중복 표시하지 않음");
        w.Faction(2).Revealed = false;

        // ---- 성능: 100x100 에 유닛 400
        var pw = World.CreateStandard(GameMap.Generate(100, 100, opt.Seed), defs);
        var rng = new Random(opt.Seed);
        var inf = defs.Units["infantry"];
        for (int i = 0; i < 400; i++)
        {
            var (tx, ty) = pw.Map.NearestWalkable(rng.Next(pw.Map.Width), rng.Next(pw.Map.Height));
            pw.SpawnUnit(inf, 1 + i % 2, new Vector2(tx + 0.5f, ty + 0.5f));
        }
        for (int i = 0; i < 20; i++) pw.Fog.Update();
        const int runs = 300;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++) pw.Fog.Update();
        double ms = sw.Elapsed.TotalMilliseconds / runs;
        check(ms < 0.3, $"안개 갱신 100x100 · 유닛 {pw.Units.Count}: 평균 {ms:0.000}ms (목표 < 0.3ms)");

        var bw = World.CreateStandard(GameMap.Generate(Config.MaxMapSize, Config.MaxMapSize, opt.Seed), defs);
        for (int i = 0; i < 400; i++)
        {
            var (tx, ty) = bw.Map.NearestWalkable(rng.Next(bw.Map.Width), rng.Next(bw.Map.Height));
            bw.SpawnUnit(inf, 1 + i % 2, new Vector2(tx + 0.5f, ty + 0.5f));
        }
        for (int i = 0; i < 5; i++) bw.Fog.Update();
        sw.Restart();
        for (int i = 0; i < 50; i++) bw.Fog.Update();
        Console.WriteLine($"  [perf] 안개 갱신 {Config.MaxMapSize}x{Config.MaxMapSize} · 유닛 {bw.Units.Count}: 평균 {sw.Elapsed.TotalMilliseconds / 50:0.000}ms");
    }

    static Building Hq(World w, int f) => w.Buildings.First(b => b.Owner == f && b.Def.Headquarters);

    static void Steps(World w, int n)
    {
        for (int i = 0; i < n; i++) w.Step();
    }

    /// <summary>건물 바로 오른쪽 옆 (SpawnUnit · 지형 처리가 통행 가능한 곳으로 보정).</summary>
    static Vector2 Beside(Building b) => b.Pos + new Vector2(b.Size / 2f + 1.5f, 0);

    /// <summary>near 근처에서 size×size 를 놓을 수 있는 좌상단 타일 (추가 조건 선택).</summary>
    static (int X, int Y) FreeSpot(World w, BuildingDef def, Vector2 near, Func<int, int, bool>? extra = null)
    {
        for (int r = 0; r < 40; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            int x = (int)near.X + dx, y = (int)near.Y + dy;
            if (w.Map.CanPlace(x, y, def.Size) && (extra == null || extra(x, y))) return (x, y);
        }
        throw new InvalidOperationException("빈 자리 없음");
    }
}
