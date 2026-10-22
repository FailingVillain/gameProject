using KoreanDivision.Core;
using MapEditor.Model;

// T33d: 선로 꺾은선 (paint "line") + 새 지형 이름 (rail / ford / bridge) 시험.
// 게임 격자와 대조하는 시험은 게임이 그 지형을 아는 빌드에서만 돈다 (rail 브랜치 위). 모르면 "게임 대조 건너뜀" 만 출력하고 편집기 쪽 시험은 그대로 돈다.
static partial class Tests
{
    static void RailAll()
    {
        // 지형 번호 = 게임 TerrainType 바이트
        Check(MapDoc.TerrainIndex("rail") == 11 && MapDoc.TerrainIndex("ford") == 12 && MapDoc.TerrainIndex("bridge") == 13, "rail 11 / ford 12 / bridge 13");
        Check(MapDoc.Parse("{ \"width\": 8, \"height\": 8, \"paint\": [ { \"terrain\": \"bridge\", \"rect\": [1,1,2,2] } ] }").Validate().Count == 0, "bridge 는 알려진 지형");

        // 꺾은선 저장 · 로드 왕복 + 래스터
        var d = new MapDoc("rail", 48, 48);
        d.PaintPolyline("rail", new[] { (0, 30), (20, 30), (30, 20), (39, 20) });
        string t = d.ToFileText();
        Check(t.Contains("{ \"terrain\": \"rail\", \"line\": [0, 30, 20, 30, 30, 20, 39, 20] }"), "꺾은선은 게임 paint line 평평한 배열로 저장");
        var d2 = MapDoc.Parse(t);
        Check(d2.ToFileText() == t && d2.Validate().Count == 0, "line 왕복 바이트 동일");
        var g = d.Raster(); var g2 = d2.Raster();
        Check(g.Terrain.SequenceEqual(g2.Terrain), "로드 뒤 격자 동일");
        Check(g.At(0, 30) == 11 && g.At(20, 30) == 11 && g.At(25, 25) == 11 && g.At(30, 20) == 11 && g.At(39, 20) == 11 && g.At(5, 29) == 0, "격자: 꺾임점 · 대각 · 끝점 rail, 주변 grass");
        Check(g.Terrain.Count(b => b == 11) == 21 + 10 + 9, $"rail 칸 수 (실제 {g.Terrain.Count(b => b == 11)})");

        // undo 한 번 = 꺾은선 하나, 점 하나는 선이 아님, 중복 연속점은 합침, 지도 밖은 거부
        d.Undo();
        Check(d.Paint.Count == 0 && d.Raster().Terrain.All(b => b == 0), "꺾은선 undo 한 번으로 전부 사라짐");
        d.PaintPolyline("rail", new[] { (3, 3) });
        Check(d.Paint.Count == 0, "점 하나는 칠하지 않음");
        d.PaintPolyline("rail", new[] { (3, 3), (3, 3), (6, 3) });
        Check(d.Paint[0].Line!.SequenceEqual(new[] { 3, 3, 6, 3 }), "연속 중복점 합침");
        bool threw = false; try { d.PaintPolyline("rail", new[] { (0, 0), (99, 0) }); } catch (ArgumentException) { threw = true; }
        Check(threw && d.Paint.Count == 1, "지도 밖 점은 거부");

        // 파일의 line: 지도 밖으로 나가는 선은 잘라서 래스터 (게임과 같음), 홀수 개 · 중첩은 보존 + 검사
        var o = MapDoc.Parse("{ \"width\": 48, \"height\": 48, \"paint\": [ { \"terrain\": \"rail\", \"line\": [-3, 2, 10, 2] } ] }");
        Check(o.Validate().Count == 0 && Enumerable.Range(0, 11).All(x => o.Raster().At(x, 2) == 11) && o.Raster().At(11, 2) == 0, "지도 밖으로 나가는 line 은 안쪽만");
        Check(MapDoc.Parse("{ \"width\": 8, \"height\": 8, \"paint\": [ { \"terrain\": \"rail\", \"line\": [1, 2, 3] } ] }").Validate().Any(e => e.Contains("line")), "홀수 개 line 은 오류");
        var nested = MapDoc.Parse("{ \"width\": 8, \"height\": 8, \"paint\": [ { \"terrain\": \"rail\", \"line\": [[1, 2], [3, 2]] } ] }");
        Check(nested.Paint[0].Line == null && nested.ToFileText().Contains("\"line\": [[1, 2], [3, 2]]"), "중첩 line 은 모르는 키로 원문 보존");

        // 선분 규칙 (RailRenderer.Segments 와 같음): 직선 = 동 선분만, 계단식 대각선 = 삼각형 없음, 외톨이 = 가로 토막
        var h = new MapDoc("h", 48, 48); h.PaintPolyline("rail", new[] { (1, 1), (4, 1) });
        var sh = h.Raster().RailSegments();
        Check(sh.Count == 3 && sh.All(s => s.Bx - s.Ax == 1 && s.By == s.Ay), "가로선 4칸: 동 선분 3개 (끝 칸은 이웃이 있어 토막 없음)");
        var dg = new MapDoc("dg", 48, 48); dg.PaintPolyline("rail", new[] { (1, 1), (4, 4) });
        var sd = dg.Raster().RailSegments();
        Check(sd.Count == 3 && sd.All(s => s.Bx - s.Ax == 1 && s.By - s.Ay == 1), "대각선 4칸: 남동 선분 3개 (계단 삼각형 없음)");
        var lone = new MapDoc("l", 8, 8); lone.PaintTiles("rail", new[] { (2, 2) });
        Check(lone.Raster().RailSegments().Count == 1, "외톨이 칸 = 토막 하나");

        // 게임 대조 (rail 브랜치 위에서만)
        var cases = new[] { d, d2, h, dg, o };
        string? skip = null;
        foreach (var doc in cases)
        {
            if (doc.Starts.Count == 0) { doc.AddStart(45, 45); doc.AddStart(46, 46); }   // 게임 로드 조건 (시작 2개 이상)
            try
            {
                var gm = (GameMap)loadText.Invoke(null, new object?[] { doc.ToFileText(), "railcase", 1, null })!;
                Check(Same(gm, doc.Raster()), $"게임 격자 = 편집기 격자 ({doc.Name})");
            }
            catch (System.Reflection.TargetInvocationException e) when (e.InnerException is InvalidDataException ide && ide.Message.Contains("알 수 없는 지형")) { skip = ide.Message; }
        }
        if (skip != null) Console.WriteLine("  [rail] 게임 대조 건너뜀 (이 빌드의 게임은 rail 을 모름): " + skip);
    }

    static bool Same(GameMap m, MapGrid g)
    {
        if (g.Width != m.Width || g.Height != m.Height) return false;
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
                if (g.At(x, y) != (byte)m.Get(x, y)) return false;
        return true;
    }
}
