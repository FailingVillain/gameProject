using System.Diagnostics;
using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>지형 · 높이 · 지형 시야 (ROADMAP '지형 · 높이', GameMapTerrain.cs · FogVision.cs). SelfTest 가 호출, 단독 KD_SELFTEST_ONLY=terrain.</summary>
public static class TerrainSelfTest
{
    // 숲 12×12 (x 30~41, y 10~21) · 잔해 (10,40) 6×6 · 참호 (20,40) 6×2 · 가옥 2×2 (40,40) · 철조망 띠 (x 50~52, y 30~49) · 고지 10×10 (60~69)
    const string TerrainMap = """
    { "name": "terrain test", "width": 96, "height": 96, "base": "grass",
      "starts": [ [10, 10], [86, 86] ],
      "paint": [
        { "terrain": "forest", "rect": [30, 10, 12, 12] },
        { "terrain": "rubble", "rect": [10, 40, 6, 6] },
        { "terrain": "trench", "rect": [20, 40, 6, 2] },
        { "terrain": "house", "tiles": [[40, 40], [41, 40], [40, 41], [41, 41]] },
        { "terrain": "wire", "rect": [50, 30, 3, 20] },
        { "terrain": "high", "rect": [60, 60, 10, 10] }
      ] }
    """;

    static World Fresh(int seed)
    {
        var w = World.CreateStandard(MapFile.LoadText(TerrainMap, "terraintest", seed), Defs.Current);
        foreach (var u in w.Units) u.Hp = 0;
        w.Step();
        return w;
    }

    static Vector2 C(int x, int y) => new(x + 0.5f, y + 0.5f);

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var tr = defs.Terrain;
        var inf = defs.Units["infantry"];
        var w = Fresh(opt.Seed);
        var m = w.Map;

        // ---- 지도 파일 · 구조
        check(m.Get(35, 15) == TerrainType.Forest && m.Get(12, 42) == TerrainType.Rubble && m.Get(22, 40) == TerrainType.Trench
              && m.Get(40, 40) == TerrainType.House && m.Get(51, 35) == TerrainType.Wire && m.IsHigh(65, 65) && !m.IsHigh(65, 75),
              "지도 json: forest · rubble · trench · house(tiles) · wire · high 칠하기");
        check(!m.IsWalkable(40, 40) && m.IsWalkable(35, 15) && !m.IsWalkable(51, 35), "민간 가옥 · 철조망 진입 불가 (철조망 = 임시 장애물), 숲 통행 가능");
        check(m.IsSlope(60, 65) && m.IsSlope(65, 69) && !m.IsSlope(65, 65) && !m.IsSlope(59, 65), "경사 = 평지와 맞닿은 고지 칸");
        check(m.ForestDepth(30, 15) == 1 && m.ForestDepth(31, 15) == 2 && m.ForestDepth(32, 15) == 3 && m.ForestDepth(33, 15) == 4 && !m.IsDeepForest(32, 15) && m.IsDeepForest(33, 15),
              $"숲 깊이: 가장자리 1, {tr.ForestSeeDepth}칸 넘게 들어가면 깊은 숲");
        check(m.IsHouseAdjacent(42, 40) && m.IsHouseAdjacent(39, 39) && !m.IsHouseAdjacent(43, 40), "가옥 옆 칸 (8방향)");

        // ---- 이동
        float Near(float a, float b) => MathF.Abs(a - b);
        check(Near(m.MoveMul(C(5, 50)), 1f) < 1e-4f && Near(m.MoveMul(C(35, 15)), tr.ForestMove) < 1e-4f && Near(m.MoveMul(C(12, 42)), tr.RubbleMove) < 1e-4f
              && Near(m.MoveMul(C(51, 35)), tr.WireMove) < 1e-4f && Near(m.MoveMul(C(60, 65)), tr.SlopeMove) < 1e-4f && Near(m.MoveMul(C(65, 65)), 1f) < 1e-4f,
              $"이동 배수: 숲 ×{tr.ForestMove} · 잔해 ×{tr.RubbleMove} · 철조망 ×{tr.WireMove} · 경사 ×{tr.SlopeMove} · 고지 안쪽 ×1");
        var ug = w.SpawnUnit(inf, 1, C(5, 50));
        var uf = w.SpawnUnit(inf, 1, C(35, 15));
        check(Near(w.UnitSpeedForTest(uf) / w.UnitSpeedForTest(ug), tr.ForestMove) < 1e-3f, "유닛 이동 속도에 지형 배수 적용 (숲)");
        ug.Hp = 0; uf.Hp = 0; w.Step();

        // ---- 엄폐 · 높이 (같은 거리 10칸에서 명중률 비)
        float P(Vector2 s, Vector2 t)
        {
            var a = w.SpawnUnit(inf, 1, s);
            var b = w.SpawnUnit(inf, 2, t);
            a.Pos = s; b.Pos = t;
            float p = w.HitChance(a, b);
            a.Hp = 0; b.Hp = 0; w.Step();
            return p;
        }
        float pRef = P(C(11, 30), C(21, 30));
        check(pRef > 0, $"(전제) 평지 10칸 명중률 {pRef:0.000}");
        check(Near(P(C(21, 15), C(31, 15)) / pRef, tr.ForestHit) < 0.01f, $"숲 엄폐: 명중 ×{tr.ForestHit}");
        check(Near(P(C(12, 52), C(12, 42)) / pRef, tr.RubbleHit) < 0.01f, $"건물 잔해 엄폐: 명중 ×{tr.RubbleHit}");
        check(Near(P(C(22, 50), C(22, 40)) / pRef, tr.TrenchHit) < 0.01f, $"참호 지형: 명중 ×{tr.TrenchHit}");
        check(Near(P(C(42, 30), C(42, 40)) / pRef, tr.HouseAdjHit) < 0.01f, $"가옥에 붙은 칸: 명중 ×{tr.HouseAdjHit}");
        check(Near(P(C(65, 65), C(65, 75)) / pRef, tr.HighToLowHit) < 0.01f, $"고지 → 평지 명중 ×{tr.HighToLowHit}");
        check(Near(P(C(65, 75), C(65, 65)) / pRef, tr.LowToHighHit) < 0.01f, $"평지 → 고지 명중 ×{tr.LowToHighHit}");
        {
            var t = w.SpawnUnit(inf, 2, C(31, 15)); t.Pos = C(31, 15);
            var o = w.SpawnUnit(inf, 2, C(5, 60)); o.Pos = C(5, 60);
            check(Near(w.CoverSuppressMul(t), tr.ForestSuppress) < 1e-4f && Near(w.CoverSuppressMul(o), 1f) < 1e-4f && w.CoverLevelOf(t) == 1 && w.CoverLevelOf(o) == 0,
                  $"숲 제압 ×{tr.ForestSuppress}, 엄폐 단계 1 (평지 0)");
            t.Hp = 0; o.Hp = 0; w.Step();
        }

        // ---- 직사 사선
        check(!m.HasFiringLine(C(25, 15), C(46, 15), 0, 0) && m.HasLineOfSight(C(25, 15), C(46, 15)), "깊은 숲을 가로지르는 사선은 막힘 (이동용 직선 검사는 그대로)");
        check(m.HasFiringLine(C(25, 15), C(32, 15), 0, 0) && !m.HasFiringLine(C(25, 15), C(34, 15), 0, 0), $"숲 가장자리 {tr.ForestSeeDepth}칸까지는 쏠 수 있고 더 깊으면 막힘");
        check(!m.HasFiringLine(C(36, 40), C(46, 40), 0, 0), "민간 가옥이 사선을 막음");
        check(m.HasFiringLine(C(65, 52), C(65, 60), 0, 0) && !m.HasFiringLine(C(65, 52), C(65, 64), 0, 0) && !m.HasFiringLine(C(65, 52), C(65, 78), 0, 0),
              "평지 사격자: 고지 첫 칸은 보이고 그 너머 (고지 안쪽 · 고지 뒤 평지) 는 막힘");
        check(m.HasFiringLine(C(65, 65), C(65, 78), 0, 0) && m.HasFiringLine(C(65, 65), C(65, 52), 0, 0), "고지 사격자는 높이 제약 없음");

        // ---- 지형 시야 (shadowcasting)
        var tv = w.Fog.Vision;
        check(tv != null, "(전제) 지형 시야 켜짐 (vision.terrainBlocking)");
        if (tv != null)
        {
            check(tv.Sees(12, 30, 14, 22, 30) && !tv.Sees(12, 30, 14, 30, 30), "평지: 반경 안은 보이고 밖은 안 보임");
            check(tv.Sees(34, 40, 14, 39, 40) && !tv.Sees(34, 40, 14, 46, 40) && tv.Sees(34, 40, 14, 40, 40), "가옥: 벽은 보이지만 너머는 안 보임");
            check(tv.Sees(25, 15, 24, 31, 15) && !tv.Sees(25, 15, 24, 34, 15) && !tv.Sees(25, 15, 24, 45, 15), "숲: 가장자리 2칸까지 보이고 깊은 곳 · 숲 너머는 안 보임");
            check(tv.Sees(35, 15, 24, 36, 15) && !tv.Sees(35, 15, 24, 39, 15), "깊은 숲 안의 관측자는 바로 옆만 봄");
            check(tv.Sees(65, 52, 24, 65, 60) && !tv.Sees(65, 52, 24, 65, 64) && !tv.Sees(65, 52, 24, 65, 72), "평지 관측자: 고지 첫 줄만 보이고 고지 너머는 안 보임");
            check(tv.Sees(65, 62, 24, 65, 72) && tv.Sees(65, 62, 24, 65, 52), "고지 관측자는 고지 너머 평지를 봄");
        }
        {
            var eye = w.SpawnUnit(inf, 1, C(25, 15));
            var edge = w.SpawnUnit(inf, 2, C(31, 15));
            var deep = w.SpawnUnit(inf, 2, C(34, 15));
            eye.Weapon = edge.Weapon = deep.Weapon = null;
            w.Fog.Update();
            check(w.Fog.CanSee(1, edge) && !w.Fog.CanSee(1, deep), "안개: 숲 가장자리 적은 보이고 깊은 숲의 적은 안 보임 (CanSee)");
            eye.Hp = edge.Hp = deep.Hp = 0; w.Step();
        }

        // ---- 역할 · 고지 시야
        {
            var off = w.SpawnUnit(defs.Units["officer"], 1, C(5, 60));
            var lo = w.SpawnUnit(inf, 1, C(5, 62));
            var hi = w.SpawnUnit(inf, 1, C(65, 65));
            hi.Pos = C(65, 65);
            int baseOff = (off.Def.SightNear > 0 ? off.Def.SightNear : off.Def.Sight);
            int baseInf = inf.SightNear > 0 ? inf.SightNear : inf.Sight;
            check(w.SightOf(off) == (int)MathF.Round(baseOff * defs.Vision.OfficerSightMul), $"장교 쌍안경: 시야 {baseOff} → {w.SightOf(off)} (×{defs.Vision.OfficerSightMul})");
            check(w.SightOf(lo) == baseInf && w.SightOf(hi) == (int)MathF.Round(baseInf * tr.HighSightMul), $"고지 시야 ×{tr.HighSightMul}: {baseInf} → {w.SightOf(hi)}");
            off.Hp = lo.Hp = hi.Hp = 0; w.Step();
        }

        // ---- 철조망 = 통과 불가 장애물 (멈칫 코드는 보류) · 우회
        {
            var ws = Fresh(opt.Seed);
            var squad = Enumerable.Range(0, 8).Select(i => ws.SpawnUnit(inf, 1, C(46, 32 + i * 2))).ToList();
            foreach (var u in squad) u.Weapon = null;
            ws.Move(1, squad, C(57, 39));
            int hes = 0;
            for (int i = 0; i < 1800; i++)
            {
                ws.Step();
                foreach (var u in squad) if (ws.Map.IsWire(u.Pos)) hes++;
            }
            check(hes == 0, $"철조망은 장애물: 칸 위에 선 유닛 없음 ({hes} 유닛·틱)");
            check(squad.Count(u => u.Pos.X > 53f) >= 6, $"철조망을 우회해 도착 ({squad.Count(u => u.Pos.X > 53f)}/8)");
        }

        // ---- 시차 갱신: 세력 2 는 6틱 늦게
        {
            var ws = Fresh(opt.Seed);
            while (ws.Tick % FogOfWar.Period != 1) ws.Step();
            var u2 = ws.SpawnUnit(inf, 2, C(20, 70));
            var u1 = ws.SpawnUnit(inf, 1, C(70, 20));
            u1.Weapon = u2.Weapon = null;
            bool before = ws.Fog.IsVisible(2, u2.Pos);
            while (ws.Tick % FogOfWar.Period != 6) ws.Step();
            bool f2Before = ws.Fog.IsVisible(2, u2.Pos), f1Mid = ws.Fog.IsVisible(1, u1.Pos);
            ws.Step();   // 틱 6: 세력 2 갱신
            bool f2After = ws.Fog.IsVisible(2, u2.Pos), f1Still = ws.Fog.IsVisible(1, u1.Pos);
            while (ws.Tick % FogOfWar.Period != 1) ws.Step();   // 틱 0 지남: 세력 1 갱신
            check(!before && !f2Before && f2After && !f1Mid && !f1Still && ws.Fog.IsVisible(1, u1.Pos),
                  "안개 시차 갱신: 세력 1 = 틱 0, 세력 2 = 틱 6 (각자 12틱 간격)");
        }

        // ---- 저장 · 불러오기
        {
            var ws = Fresh(opt.Seed);
            ws.SpawnUnit(inf, 1, C(31, 15));
            for (int i = 0; i < 30; i++) ws.Step();
            var bytes = Snapshot.Save(ws, new SnapshotMeta("terrain", 1, false, "none"));
            var lw = Snapshot.Load(bytes, defs).World;
            check(lw.Map.IsHigh(65, 65) && !lw.Map.IsHigh(65, 75) && lw.Map.Get(35, 15) == TerrainType.Forest && lw.Map.TerrainHash == ws.Map.TerrainHash
                  && lw.ComputeChecksum() == ws.ComputeChecksum(), "저장 · 불러오기: 지형 · 고지 · 체크섬 유지");
            for (int i = 0; i < 60; i++) { ws.Step(); lw.Step(); }
            check(lw.ComputeChecksum() == ws.ComputeChecksum() && lw.Fog.Grid(1).SequenceEqual(ws.Fog.Grid(1)), "불러온 뒤 이어 돌려도 체크섬 · 안개 일치");
        }

        // ---- 건물 크기 (2026-10-22): 가옥 칸 → 건물 묶음 (렌더) · 미션 지도 가옥 = 민가 3×3~4×3 · 큰 건물 6×4
        {
            const string HouseMap = """
            { "name": "house test", "width": 48, "height": 48, "base": "grass", "starts": [ [2, 2], [45, 45] ],
              "paint": [
                { "terrain": "house", "rect": [5, 5, 3, 3] },
                { "terrain": "house", "rect": [10, 5, 4, 3] },
                { "terrain": "house", "rect": [5, 12, 6, 4] },
                { "terrain": "house", "rect": [20, 5, 3, 4] },
                { "terrain": "house", "rect": [20, 20, 8, 3] }
              ] }
            """;
            var hm = MapFile.LoadText(HouseMap, "housetest", opt.Seed);
            var hb = KoreanDivision.Render.HouseBlocks.Build(hm);
            string got = string.Join(" ", hb.Select(b => $"{b.X},{b.Y},{b.W}x{b.H}"));
            check(got == "5,5,3x3 10,5,4x3 20,5,3x4 5,12,6x4 20,20,6x3 26,20,2x3", $"가옥 묶음: 칠한 사각형 그대로 · 긴 줄은 6칸에서 끊음 ({got})");
            var tall = KoreanDivision.Render.HouseBlocks.Pieces(new KoreanDivision.Render.HouseBlocks.Block(0, 0, 3, 4)).ToList();
            check(tall.Count == 2 && tall.Sum(p => p.H) == 4 && KoreanDivision.Render.HouseBlocks.Pieces(new KoreanDivision.Render.HouseBlocks.Block(0, 0, 4, 3)).Count() == 1,
                  "세로로 긴 묶음 (3×4) 은 그림 두 채, 가로 묶음 (4×3) 은 한 채");
            check(!hm.IsWalkable(6, 6) && hm.IsHouseAdjacent(4, 6) && hm.IsHouseAdjacent(8, 6) && !hm.IsHouseAdjacent(9, 9), "큰 가옥: 발자국 전체 통행 불가 · 둘레 칸 가옥 엄폐");
            var bad = new List<string>();
            int nMis = 0, nHouse = 0;
            foreach (var id in MissionDef.Available())
            {
                var md = MissionDef.Load(id);
                var mm = md.MapData is { ValueKind: System.Text.Json.JsonValueKind.Object } mj ? MapFile.LoadText(mj.GetRawText(), "m_" + id, opt.Seed) : MapFile.Load(md.Map!, opt.Seed);
                nMis++;
                foreach (var b in KoreanDivision.Render.HouseBlocks.Build(mm))
                {
                    nHouse++;
                    if (Math.Min(b.W, b.H) < 3 || Math.Max(b.W, b.H) > 6) bad.Add($"{id}:{b.X},{b.Y},{b.W}x{b.H}");
                }
            }
            Console.WriteLine($"  [houses] 미션 {nMis}개 · 가옥 {nHouse}채");
            check(bad.Count == 0, $"미션 지도 가옥은 모두 3~6칸 건물 (어긋남 {bad.Count}: {string.Join(" ", bad.Take(6))})");
        }

        // ---- 철도 선로 (2026-10-22): 지도 json "rail" (rect · line) · 통행 · 이동 · 엄폐 없음 · 사선/시야 안 막음 · 저장 · 선로 그림 선분
        {
            const string RailMap = """
            { "name": "rail test", "width": 64, "height": 64, "base": "grass", "starts": [ [4, 4], [60, 60] ],
              "paint": [
                { "terrain": "rail", "rect": [0, 30, 64, 1] },
                { "terrain": "rail", "line": [10, 5, 20, 15, 30, 15] }
              ] }
            """;
            var rw = World.CreateStandard(MapFile.LoadText(RailMap, "railtest", opt.Seed), defs);
            foreach (var u in rw.Units) u.Hp = 0;
            rw.Step();
            var rm = rw.Map;
            bool lineOk = true;
            for (int k = 0; k <= 10; k++) lineOk &= rm.Get(10 + k, 5 + k) == TerrainType.Rail && rm.Get(20 + k, 15) == TerrainType.Rail;
            check(rm.Get(0, 30) == TerrainType.Rail && rm.Get(63, 30) == TerrainType.Rail && rm.Get(5, 29) == TerrainType.Grass && lineOk && rm.Get(11, 5) == TerrainType.Grass,
                  "지도 json: rail rect · line (대각선 → 직선 꺾은선)");
            var ct = rm.TerrainCover(C(20, 30));
            check(rm.IsWalkable(20, 30) && MathF.Abs(rm.MoveMul(C(20, 30)) - 1f) < 1e-4f && ct.Hit == 1f && ct.Suppress == 1f,
                  "선로: 통행 가능 · 이동 배수 1 · 지형 엄폐 없음");
            check(rm.HasFiringLine(C(20, 25), C(20, 36), 0, 0) && rm.HasLineOfSight(C(20, 25), C(20, 36)), "선로를 가로지르는 사선 · 직선 이동 검사 막지 않음");
            var a = rw.SpawnUnit(inf, 1, C(20, 26));
            var b = rw.SpawnUnit(inf, 2, C(20, 35));
            for (int i = 0; i < FogOfWar.Period + 1; i++) rw.Step();
            check(rw.Fog.CanSee(1, b) && rw.Fog.CanSee(2, a), "선로 건너편 적이 보임 (지형 시야 안 막음)");
            var segs = KoreanDivision.Render.RailRenderer.Segments(rm);
            int straight = segs.Count(s => s.Y == 30), diag = segs.Count(s => s.S.B.X - s.S.A.X == 1 && s.S.B.Y - s.S.A.Y == 1);
            check(straight == 63 && diag == 10, $"선로 그림 선분: 가로 63 (= 64칸 − 1, 받은 {straight}) · 대각선 10 (받은 {diag})");
            var bytes = Snapshot.Save(rw, new SnapshotMeta("rail", 1, false, "none"));
            var lw = Snapshot.Load(bytes, defs).World;
            check(lw.Map.Get(15, 10) == TerrainType.Rail && lw.Map.TerrainHash == rm.TerrainHash && lw.ComputeChecksum() == rw.ComputeChecksum(), "저장 · 불러오기: 선로 칸 유지");
        }

        // ---- 여울 · 하천 (T39a, 2026-10-22): 지도 json "ford" · line "width" · 통행 · 이동 배수 · 사선 불변 · 깊은 물 통행 불가 · 저장
        {
            const string FordMap = """
            { "name": "ford test", "width": 64, "height": 64, "base": "grass", "starts": [ [4, 4], [60, 60] ],
              "paint": [
                { "terrain": "water", "line": [5, 20, 30, 20, 40, 40], "width": 4 },
                { "terrain": "water", "line": [5, 50, 60, 50] },
                { "terrain": "ford", "rect": [10, 18, 4, 5] }
              ] }
            """;
            var fw = World.CreateStandard(MapFile.LoadText(FordMap, "fordtest", opt.Seed), defs);
            foreach (var u in fw.Units) u.Hp = 0;
            fw.Step();
            var fm = fw.Map;
            // 폭 4: 중심 y=20 에서 -1~+2 → y 19~22 (x 가 중간인 칸, 여울 rect 밖)
            check(fm.Get(20, 19) == TerrainType.Water && fm.Get(20, 22) == TerrainType.Water && fm.Get(20, 18) == TerrainType.Grass && fm.Get(20, 23) == TerrainType.Grass,
                  "지도 json: water line width 4 (정사각 확장, 중심 -1~+2)");
            check(fm.Get(20, 50) == TerrainType.Water && fm.Get(20, 49) == TerrainType.Grass && fm.Get(20, 51) == TerrainType.Grass, "width 생략 = 1칸 폭 (현행과 동일)");
            check(fm.Get(11, 20) == TerrainType.Ford && fm.Get(11, 18) == TerrainType.Ford && fm.Get(11, 22) == TerrainType.Ford && fm.Get(14, 20) == TerrainType.Water,
                  "지도 json: ford rect 가 하천 위에 덮어 칠해짐");
            check(!fm.IsWalkable(20, 20) && fm.IsWalkable(11, 20), "깊은 물 통행 불가 · 여울 통행 가능");
            var fct = fm.TerrainCover(C(11, 20));
            check(MathF.Abs(fm.MoveMul(C(11, 20)) - tr.FordMove) < 1e-4f && MathF.Abs(tr.FordMove - 0.6f) < 1e-4f && fct.Hit == 1f && fct.Suppress == 1f,
                  $"여울: 이동 ×{tr.FordMove} · 지형 엄폐 없음");
            check(fm.HasFiringLine(C(11, 15), C(11, 26), 0, 0) && fm.HasLineOfSight(C(11, 15), C(11, 26)), "여울을 가로지르는 사선 · 직선 이동 검사 막지 않음");
            bool tooWide = false, tooNarrow = false, oddLine = false;
            try { MapFile.LoadText(FordMap.Replace("\"width\": 4", "\"width\": 7"), "badw", opt.Seed); } catch (Exception) { tooWide = true; }
            try { MapFile.LoadText(FordMap.Replace("\"width\": 4", "\"width\": 0"), "badw", opt.Seed); } catch (Exception) { tooNarrow = true; }
            try { MapFile.LoadText(FordMap.Replace("5, 20, 30, 20, 40, 40", "5, 20, 30, 20, 40"), "badl", opt.Seed); } catch (Exception) { oddLine = true; }
            check(tooWide && tooNarrow && oddLine, "지도 json 검증: width 0 · 7, 홀수 개 line 은 거부");
            var fbytes = Snapshot.Save(fw, new SnapshotMeta("ford", 1, false, "none"));
            var flw = Snapshot.Load(fbytes, defs).World;
            check(flw.Map.Get(11, 20) == TerrainType.Ford && flw.Map.TerrainHash == fm.TerrainHash && flw.ComputeChecksum() == fw.ComputeChecksum(), "저장 · 불러오기: 여울 칸 유지 · 체크섬 일치");
        }

        // ---- 다리 (T39d, 2026-10-22): 지도 json "bridge" · 통행 x1 · 엄폐 없음 · 사선 불변 · 깊은 물 대비 · 판자 방향 · 저장
        {
            const string BrMap = """
            { "name": "bridge test", "width": 64, "height": 64, "base": "grass", "starts": [ [4, 4], [60, 60] ],
              "paint": [
                { "terrain": "water", "rect": [30, 0, 5, 64] },
                { "terrain": "bridge", "rect": [30, 20, 5, 3] },
                { "terrain": "water", "rect": [0, 40, 64, 4] },
                { "terrain": "bridge", "rect": [10, 40, 3, 4] }
              ] }
            """;
            var bw = World.CreateStandard(MapFile.LoadText(BrMap, "brtest", opt.Seed), defs);
            foreach (var u in bw.Units) u.Hp = 0;
            bw.Step();
            var bm = bw.Map;
            check(bm.Get(32, 21) == TerrainType.Bridge && bm.Get(32, 19) == TerrainType.Water && bm.Get(32, 23) == TerrainType.Water, "지도 json: bridge rect 가 하천 위에 덮어 칠해짐");
            check(!bm.IsWalkable(32, 19) && bm.IsWalkable(32, 21) && bm.IsWalkable(30, 20) && bm.IsWalkable(34, 22), "다리 통행 가능 (깊은 물 불가) - 전 칸");
            var bct = bm.TerrainCover(C(32, 21));
            check(MathF.Abs(bm.MoveMul(C(32, 21)) - 1f) < 1e-4f && bct.Hit == 1f && bct.Suppress == 1f, "다리: 이동 x1 · 지형 엄폐 없음");
            check(bm.HasLineOfSight(C(28, 21), C(37, 21)) && bm.HasFiringLine(C(28, 21), C(37, 21), 0, 0), "다리를 가로지르는 직선 이동 검사 · 사선 막지 않음 (다리 길이 방향)");
            var tl = KoreanDivision.Render.BridgeRenderer.Tiles(bm);
            var tH = tl.Find(t => t.X == 32 && t.Y == 21); var tV = tl.Find(t => t.X == 11 && t.Y == 41);
            check(tl.Count == 15 + 12 && tH.Vertical && !tV.Vertical, $"다리 렌더 정보: {tl.Count}칸, 세로 강 위 다리 = 세로 널빤지 {tH.Vertical} · 가로 강 위 다리 = 가로 널빤지 {!tV.Vertical}");
            var bbytes = Snapshot.Save(bw, new SnapshotMeta("bridge", 1, false, "none"));
            var blw = Snapshot.Load(bbytes, defs).World;
            check(blw.Map.Get(32, 21) == TerrainType.Bridge && blw.Map.TerrainHash == bm.TerrainHash && blw.ComputeChecksum() == bw.ComputeChecksum(), "저장 · 불러오기: 다리 칸 유지 · 체크섬 일치");
        }

        // ---- 생성 맵: 숲 · 언덕 흩뿌리기 (결정적 · 2인 대칭)
        {
            var g1 = GameMap.Generate(128, 128, opt.Seed);
            var g2 = GameMap.Generate(128, 128, opt.Seed);
            var g3 = GameMap.Generate(128, 128, opt.Seed + 1);
            int forest = 0, high = 0, asym = 0;
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                if (g1.Get(x, y) == TerrainType.Forest) forest++;
                if (g1.IsHigh(x, y)) high++;
                if ((g1.Get(x, y) == TerrainType.Forest) != (g1.Get(127 - x, 127 - y) == TerrainType.Forest) || g1.IsHigh(x, y) != g1.IsHigh(127 - x, 127 - y)) asym++;
            }
            check(forest > 50 && high > 50, $"생성 맵에 숲 {forest}칸 · 고지 {high}칸");
            check(g1.TerrainHash == g2.TerrainHash && g1.TerrainHash != g3.TerrainHash, "같은 시드 = 같은 지형 해시, 다른 시드 = 다름");
            check(asym == 0, $"2인 생성 맵의 숲 · 고지도 180° 대칭 (어긋난 칸 {asym})");
        }

        // ---- 성능 (참고): 생성 맵 200×200 · 유닛 2000, 전 세력 갱신
        {
            var pw = World.CreateStandard(GameMap.Generate(200, 200, opt.Seed), defs);
            var rng = new Random(opt.Seed);
            for (int i = 0; i < 2000; i++)
            {
                var (tx, ty) = pw.Map.NearestWalkable(rng.Next(200), rng.Next(200));
                pw.SpawnUnit(inf, 1 + i % 2, C(tx, ty));
            }
            var sw = Stopwatch.StartNew();
            pw.Fog.Update();
            double cold = sw.Elapsed.TotalMilliseconds;
            for (int i = 0; i < 5; i++) pw.Fog.Update();
            sw.Restart();
            for (int i = 0; i < 30; i++) pw.Fog.Update();
            double warm = sw.Elapsed.TotalMilliseconds / 30;
            var st = pw.Fog.VisionStats;
            Console.WriteLine($"  [perf] 지형 시야 200x200 · 유닛 2000: 첫 갱신 {cold:0.00}ms, 이후 {warm:0.000}ms/전체 갱신 (시야 계산 {st.Casts} · 메모 적중 {st.Hits} · 메모 {st.Memo})");
            check(warm < 5, $"지형 시야 전체 갱신 200x200 · 2000기 {warm:0.000}ms (< 5ms)");
        }
    }
}
