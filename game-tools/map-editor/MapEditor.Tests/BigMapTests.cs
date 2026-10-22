using System.Diagnostics;
using KoreanDivision;
using KoreanDivision.Core;
using MapEditor.Model;

// T33e: 지도 크기 상한을 게임과 맞춤 (1500) + 큰 지도 (에피소드 지도 · 합성 1200 / 1500) 열기 · 저장 · undo · 격자 · 게임 로드 시험과 시간 측정.
static partial class Tests
{
    static readonly System.Text.StringBuilder perf = new();

    static void BigMapAll()
    {
        // 상한: 편집기 상수가 게임 상수와 같아야 함 (게임이 바뀌면 여기서 실패 -> MapDoc.MaxDim 을 맞출 것)
        Check(MapDoc.MaxDim == Config.MaxMapSize, $"MapDoc.MaxDim({MapDoc.MaxDim}) == Config.MaxMapSize({Config.MaxMapSize})");

        // 에피소드 지도: 시험 폴더 사본 (라이브 에셋이 이 브랜치에 있으면 그것도). 파일이 없으면 건너뜀.
        foreach (var (file, w) in new[] { ("c1e1_map_a.json", 800), ("c1e1_map_b.json", 1200) })
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", file);
            if (!File.Exists(path)) path = Path.Combine(root, "..", "game-tools", "map-editor", "MapEditor.Tests", "fixtures", file);
            if (!File.Exists(path)) { Console.WriteLine($"  {file}: 사본 없음 -- 건너뜀"); continue; }
            string text = File.ReadAllText(path);
            var sw = Stopwatch.StartNew();
            var d = MapDoc.Parse(text);
            double tParse = sw.Elapsed.TotalMilliseconds; sw.Restart();
            var errs = d.Validate();
            var g = d.Raster();
            double tRaster = sw.Elapsed.TotalMilliseconds;
            Check(d.Width == w && d.Height == w && errs.Count == 0, $"{file}: {d.Width}x{d.Height} 검사 통과 ({string.Join("; ", errs)})");
            sw.Restart();
            var gm = Game(text, "ep");
            double tGame = sw.Elapsed.TotalMilliseconds;
            Check(RasterMatches(d, gm), $"{file}: 편집기 격자 == 게임 격자");
            string t1 = d.ToFileText();
            var d2 = MapDoc.Parse(t1);
            Check(d2.ToFileText() == t1 && d2.Raster().Terrain.SequenceEqual(g.Terrain), $"{file}: 저장 왕복 동일");
            Perf($"{file} {d.Width}x{d.Height}: parse {tParse:0}ms raster {tRaster:0}ms gameLoad {tGame:0}ms paintOps {d.Paint.Count}");
        }

        // 합성: 상한 크기에서 칠하기 · 채우기 · undo / redo · 왕복
        foreach (int n in new[] { 1200, 1500 })
        {
            var sw = Stopwatch.StartNew();
            var d = new MapDoc("big", n, n);
            d.AddStart(10, n / 2); d.AddStart(n - 10, n / 2);
            d.PaintRect("water", n / 3, 0, n / 8, n);                         // 강
            d.PaintCircle("forest", n / 2, n / 2, n / 6);
            d.PaintLine("sand", 0, n / 2, n / 2, n / 3, 3);   // tiles 로 저장 (paint line 은 rail 브랜치 게임만 앎)
            var tiles = new List<(int, int)>();
            for (int i = 0; i < 20000; i++) tiles.Add((i * 7 % n, i * 13 % n));
            d.PaintTiles("rock", tiles);
            double tBuild = sw.Elapsed.TotalMilliseconds; sw.Restart();
            Check(d.Validate().Count == 0, $"{n}: 검사 통과");
            var g = d.Raster();
            double tRaster = sw.Elapsed.TotalMilliseconds; sw.Restart();
            string text = d.ToFileText();
            double tSer = sw.Elapsed.TotalMilliseconds; sw.Restart();
            var d2 = MapDoc.Parse(text);
            double tParse = sw.Elapsed.TotalMilliseconds;
            Check(d2.ToFileText() == text && d2.Raster().Terrain.SequenceEqual(g.Terrain), $"{n}: 저장 왕복 동일");
            sw.Restart();
            var gm = Game(text, "big");
            double tGame = sw.Elapsed.TotalMilliseconds;
            Check(RasterMatches(d, gm), $"{n}: 편집기 격자 == 게임 격자");

            // 채우기 (전체 바다 지형 한 번: 최악 BFS), 그 뒤 undo 가 파일을 되돌림
            string before = d.CanonicalHash();
            long mem0 = GC.GetTotalMemory(true);
            sw.Restart();
            int filled = d.Fill("dirt", 2, 2);
            d.Raster();
            double tFill = sw.Elapsed.TotalMilliseconds;
            Check(filled > n * n / 10, $"{n}: 채우기 {filled}칸");
            sw.Restart();
            Check(d.Undo(), "undo");
            d.Raster();
            double tUndo = sw.Elapsed.TotalMilliseconds;
            Check(d.CanonicalHash() == before, $"{n}: 채우기 undo 가 문서를 그대로 되돌림");
            Check(d.Redo() && d.Undo() && d.CanonicalHash() == before, $"{n}: redo / undo 반복");
            // 긴 undo 스택 메모리: 획 200개 (각 사각 칠하기) 의 증가량
            long m1 = GC.GetTotalMemory(true);
            for (int i = 0; i < 200; i++) d.PaintRect(i % 2 == 0 ? "sand" : "dirt", i, i, 300, 300);
            d.Raster();
            long m2 = GC.GetTotalMemory(true);
            Perf($"{n}x{n}: build {tBuild:0}ms raster {tRaster:0}ms serialize {tSer:0}ms parse {tParse:0}ms gameLoad {tGame:0}ms fill {tFill:0}ms undo+raster {tUndo:0}ms json {text.Length / 1024}KB  undo200 +{(m2 - m1) / 1024}KB (raster 캐시 포함 {(m2 - mem0) / 1024}KB)");
            GC.KeepAlive(g);
        }
    }

    static void Perf(string s) { Console.WriteLine("  [perf] " + s); }
}
