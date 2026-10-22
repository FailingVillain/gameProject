using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using KoreanDivision.Core;
using MapEditor.Model;

// 헤드리스 지도 문서 모델 시험. 게임 소스는 읽기 전용 참조 (MapFile.LoadText 는 internal 이라 리플렉션).
// 사용: dotnet run --project game-tools/map-editor/MapEditor.Tests -- [korean-division 루트]
Console.OutputEncoding = System.Text.Encoding.UTF8;
return Tests.Run(args);

static class Tests
{
    static int fails, passes;
    static string root = "";
    static MethodInfo loadText = null!;

    // 고정 편집열 골든 (battle_flat 에 BuildEdit 적용 후 정규화 JSON 해시). 편집 의미나 직렬화 규칙을 바꾸면 의도적으로 갱신.
    const string GoldenEditHash = "5BDE9FE433DD6FD1";

    static void Check(bool ok, string what)
    {
        if (ok) passes++; else { fails++; Console.WriteLine("FAIL " + what); }
    }

    public static int Run(string[] args)
    {
        root = args.Length > 0 ? args[0] : FindRoot();
        Defs.Load(Path.Combine(root, "assets", "data", "balance.json"));
        loadText = typeof(MapFile).GetMethod("LoadText", BindingFlags.NonPublic | BindingFlags.Static)!;

        RoundTripMaps();
        EditDeterminismAndUndo();
        MissionSubstitution();
        EmbedReference();
        StrokeAndFill();

        Console.WriteLine($"map-editor tests: pass {passes} fail {fails}");
        return fails == 0 ? 0 : 1;
    }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string c = Path.Combine(d.FullName, "korean-division");
            if (Directory.Exists(Path.Combine(c, "assets", "maps"))) return c;
            if (Directory.Exists(Path.Combine(d.FullName, "assets", "maps")) && File.Exists(Path.Combine(d.FullName, "KoreanDivision.csproj"))) return d.FullName;
        }
        throw new DirectoryNotFoundException("korean-division 루트를 못 찾음 (인자로 지정)");
    }

    static GameMap Game(string text, string id)
    {
        try { return (GameMap)loadText.Invoke(null, new object?[] { text, id, 1920, null })!; }
        catch (TargetInvocationException e) { throw e.InnerException!; }
    }

    /// <summary>게임이 만든 지도의 의미 해시: 지형 · 고지 · 시작 · 시설 · 유닛 배치 (FileHash 는 텍스트 해시라 제외).</summary>
    static string Semantic(GameMap m)
    {
        var sb = new StringBuilder();
        sb.Append($"{m.Width}x{m.Height};");
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++) sb.Append((int)m.Get(x, y)).Append(m.IsHigh(x, y) ? 'h' : 'l');
        foreach (var s in m.StartTiles) sb.Append($";S{s.X},{s.Y}");
        foreach (var s in m.ResourceSites) sb.Append($";R{s.X},{s.Y},{s.Owner},{s.Kind}");
        foreach (var u in m.UnitSpawns) sb.Append($";U{u}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }

    static bool RasterMatches(MapDoc d, GameMap m)
    {
        var g = d.Raster();
        if (g.Width != m.Width || g.Height != m.Height) return false;
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
                if (g.At(x, y) != (byte)m.Get(x, y) || g.High[y * g.Width + x] != m.IsHigh(x, y)) return false;
        return true;
    }

    // 1. assets/maps/*.json 전부: 열기 -> 저장 -> 게임 로드 격자 동일, 재저장 바이트 동일
    static void RoundTripMaps()
    {
        var files = Directory.GetFiles(Path.Combine(root, "assets", "maps"), "*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Check(files.Length >= 6, $"지도 파일 {files.Length}개 (>=6)");
        foreach (var f in files)
        {
            string id = Path.GetFileNameWithoutExtension(f), orig = File.ReadAllText(f);
            var doc = MapDoc.Parse(orig);
            string saved = doc.ToFileText();
            var g0 = Game(orig, id);
            var g1 = Game(saved, id);
            Check(Semantic(g0) == Semantic(g1), $"{id}: 저장본 의미 해시가 원본과 같음");
            Check(RasterMatches(doc, g0), $"{id}: 문서 래스터 = 게임 격자");
            Check(doc.Validate().Count == 0, $"{id}: Validate 통과");
            var doc2 = MapDoc.Parse(saved);
            Check(doc2.ToFileText() == saved, $"{id}: 저장 -> 열기 -> 저장 바이트 동일");
            Check(doc2.DroppedComments == 0, $"{id}: 정규화 저장본에는 버려질 주석 없음");
            Console.WriteLine($"  {id}: {doc.Width}x{doc.Height} paint {doc.Paint.Count} 주석 버림 {doc.DroppedComments} hash {Semantic(g0)}");
        }
    }

    // 고정 편집열 (battle_flat 기준)
    static void BuildEdit(MapDoc d)
    {
        int w = d.Width, h = d.Height;
        d.PaintRect("water", 4, h / 2 - 2, w - 8, 4);
        d.PaintCircle("rock", w / 2, h / 4, 5);
        d.PaintLine("trench", 6, 6, w - 7, 20, 2);
        d.PaintTiles("wire", new[] { (10, 10), (11, 10), (12, 11), (10, 10), (-5, 3), (w + 2, 3) });
        d.PaintRect("high", 20, 20, 10, 10);
        d.PaintRect("forest", 30, 30, 6, 6, true);
        d.Fill("sand", 2, 2);
        d.BeginStroke("획");
        d.PaintLine("dirt", 3, h - 5, w - 4, h - 5);
        d.PaintLine("dirt", w / 2, h / 2 - 3, w / 2, h - 5);
        d.EndStroke();
        d.AddStart(w / 2, h - 8);
        d.MoveStart(0, 17, 18);
        d.AddSite(new SiteDef(w / 2, 8, "village", 0));
        d.SetName(d.Name + " (편집)");
    }

    // 2. 같은 연산열 -> 같은 바이트(골든), undo 전부 = 원본, 단계별 undo/redo 해시 일치, 편집 결과가 게임 로드와 같음
    static void EditDeterminismAndUndo()
    {
        string orig = File.ReadAllText(Path.Combine(root, "assets", "maps", "battle_flat.json"));
        var a = MapDoc.Parse(orig);
        var b = MapDoc.Parse(orig);
        string origHash = a.CanonicalHash();
        BuildEdit(a);
        BuildEdit(b);
        string edited = a.CanonicalHash();
        Check(edited == b.CanonicalHash() && a.ToFileText() == b.ToFileText(), "같은 연산열 -> 같은 출력 바이트");
        Check(edited != origHash, "편집 후 해시가 달라짐");
        Console.WriteLine($"  편집 골든 해시 {edited} (원본 {origHash})");
        Check(edited == GoldenEditHash, "편집 골든 해시 불변");
        Check(a.Validate().Count == 0, "편집 결과 Validate 통과");

        var ge = Game(a.ToFileText(), "edited");
        Check(RasterMatches(a, ge), "편집 결과 문서 래스터 = 게임이 읽은 격자");
        Check(ge.StartTiles.Count == 3 && ge.StartTiles[0] == (17, 18), "시작 이동/추가가 게임 지도에 반영");
        Check(ge.ResourceSites.Any(s => s.Kind == "village"), "시설이 게임 지도에 반영");

        // 단계별 undo: 한 단계씩 되돌리며 이전 해시와 일치 확인 (기록은 새 문서로 재생해 얻음)
        var steps = StepHashes(orig);
        Check(steps[0] == origHash, "단계 0 = 원본 해시");
        int n = a.UndoDepth;
        Check(n == steps.Count - 1, $"undo 깊이 {n} = 기록된 단계 {steps.Count - 1}");
        for (int i = steps.Count - 1; i >= 1; i--)
        {
            Check(a.CanUndo && a.CanonicalHash() == steps[i], $"undo 전 단계 {i} 해시");
            a.Undo();
        }
        Check(a.CanonicalHash() == origHash, "undo 전부 = 원본 해시");
        Check(!a.CanUndo && a.CanRedo, "undo 끝 상태");
        Check(Semantic(Game(a.ToFileText(), "x")) == Semantic(Game(orig, "x")), "undo 전부 = 원본 의미 해시 (게임 로드)");
        for (int i = 1; i < steps.Count; i++)
        {
            a.Redo();
            Check(a.CanonicalHash() == steps[i], $"redo 단계 {i} 해시");
        }
        Check(a.CanonicalHash() == edited, "redo 전부 = 편집 해시");
        // 새 편집은 redo 를 비움
        a.Undo(); a.PaintRect("rock", 0, 0, 2, 2);
        Check(!a.CanRedo, "새 편집 뒤 redo 비움");
    }

    /// <summary>BuildEdit 를 새 문서에 적용한 뒤 undo 를 끝까지 하며 단계별 해시를 모은다 (0 = 원본 ... 마지막 = 편집 완료).</summary>
    static List<string> StepHashes(string orig)
    {
        var probe = MapDoc.Parse(orig);
        BuildEdit(probe);
        var back = new List<string> { probe.CanonicalHash() };
        while (probe.Undo()) back.Add(probe.CanonicalHash());
        back.Reverse();
        return back;
    }

    // 3. 미션 mapData 치환 (전 미션): 나머지 바이트 불변, MissionDef.Parse 통과, 지도 의미 불변
    static void MissionSubstitution()
    {
        var files = Directory.GetFiles(Path.Combine(root, "assets", "missions"), "*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        int tested = 0;
        foreach (var f in files)
        {
            string id = Path.GetFileNameWithoutExtension(f), orig = File.ReadAllText(f);
            string? span = MissionMapData.Extract(orig);
            if (span == null) continue;
            tested++;
            var doc = MapDoc.Parse(span);
            string baseHashSem = Semantic(Game(span, id));

            string unchanged = MissionMapData.Replace(orig, doc);
            Check(OutsideSame(orig, span, unchanged), $"{id}: mapData 밖 바이트 불변");
            string? span2 = MissionMapData.Extract(unchanged);
            Check(span2 != null && Semantic(Game(span2, id)) == baseHashSem, $"{id}: 치환 후 지도 의미 해시 불변");
            Check(MapDoc.Parse(span2!).CanonicalHash() == doc.CanonicalHash(), $"{id}: 치환 mapData 문서 동일");
            Check(TryParse(orig, id) == TryParse(unchanged, id), $"{id}: MissionDef.Parse 결과 (원본과 같은 성공/실패)");

            // 편집 후 치환 (고지 한 칸 + 지형 한 칸 = 안전한 변경): 통과 + 밖 불변
            var ed = MapDoc.Parse(span);
            ed.PaintTiles("rock", new[] { (1, 1) });
            string edited = MissionMapData.Replace(orig, ed);
            Check(OutsideSame(orig, span, edited), $"{id}: 편집 치환 mapData 밖 바이트 불변");
            Check(TryParse(orig, id) == TryParse(edited, id), $"{id}: 편집 치환 MissionDef.Parse 같은 결과");
            ed.Undo();
            Check(ed.CanonicalHash() == doc.CanonicalHash(), $"{id}: 편집 undo = 원본 문서 해시");
        }
        Check(tested >= 15, $"mapData 미션 {tested}개 시험 (>=15)");
        Console.WriteLine($"  mapData 미션 {tested}개");
    }

    static bool OutsideSame(string orig, string origSpan, string now)
    {
        int i = orig.IndexOf(origSpan, StringComparison.Ordinal);
        string pre = orig[..i], post = orig[(i + origSpan.Length)..];
        return now.StartsWith(pre, StringComparison.Ordinal) && now.EndsWith(post, StringComparison.Ordinal);
    }

    /// <summary>MissionDef.Parse 성공 여부 (true / false). 예외 종류는 무시 (원본과 같은 결과만 본다).</summary>
    static bool TryParse(string text, string id)
    {
        try { MissionDef.Parse(text, id); return true; }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or ArgumentException) { return false; }
    }

    // 4. map 참조 -> 내장 mapData (내장으로 복사)
    static void EmbedReference()
    {
        string mapText = File.ReadAllText(Path.Combine(root, "assets", "maps", "battle_flat.json"));
        string mission = "{\n  // 설명 주석\n  \"id\": \"t\",\n  \"map\": \"battle_flat\",\n  \"seed\": 1\n}\n";
        var doc = MapDoc.Parse(mapText);
        string outp = MissionMapData.EmbedReference(mission, doc);
        Check(outp.Contains("// 설명 주석") && outp.Contains("\"seed\": 1") && !outp.Contains("\"map\":"), "참조 치환: 나머지 보존, map 제거");
        string? span = MissionMapData.Extract(outp);
        Check(span != null && MapDoc.Parse(span).CanonicalHash() == doc.CanonicalHash(), "참조 치환: mapData = 문서");
        Check(Semantic(Game(span!, "e")) == Semantic(Game(mapText, "e")), "참조 치환: 지도 의미 불변");
        bool threw = false;
        try { MissionMapData.Replace(mission, doc); } catch (InvalidDataException) { threw = true; }
        Check(threw, "mapData 없는 미션에 Replace 는 오류");
    }

    // 5. 획 묶음, 채우기 의미, 주석 보존
    static void StrokeAndFill()
    {
        var d = new MapDoc("t", 32, 32);
        d.PaintRect("water", 10, 0, 1, 32);              // 세로 벽
        int n = d.Fill("sand", 0, 0);                    // 왼쪽 영역만 (10열 x 32행)
        Check(n == 320, $"채우기 칸 수 {n} == 320");
        var g = d.Raster();
        Check(g.At(5, 5) == MapDoc.TerrainIndex("sand") && g.At(15, 5) == MapDoc.TerrainIndex("grass") && g.At(10, 5) == MapDoc.TerrainIndex("water"), "채우기가 벽에서 멈춤");
        Check(d.Fill("sand", 0, 0) == 0, "이미 같은 지형 채우기는 명령 없음");
        Check(d.UndoDepth == 2, "채우기 한 번 = undo 한 단계");
        d.Undo();
        Check(d.Raster().At(5, 5) == MapDoc.TerrainIndex("grass"), "채우기 undo");
        Check(MapDoc.LineCells(0, 0, 4, 2).SequenceEqual(MapDoc.LineCells(0, 0, 4, 2)) && MapDoc.LineCells(3, 3, 3, 3).Count() == 1, "선 래스터 결정적");

        string withComments = "{ \"name\": \"c\", \"width\": 48, \"height\": 48, \"base\": \"grass\",\n  \"paint\": [\n    // 강\n    { \"terrain\": \"water\", \"rect\": [0, 5, 16, 2] },\n    // 다리 1\n    // 다리 2\n    { \"terrain\": \"dirt\", \"rect\": [4, 5, 2, 2] }\n  ],\n  // 시작\n  \"starts\": [[1,1],[44,44]], \"weather\": \"fog\" }";
        var c = MapDoc.Parse(withComments);
        Check(c.Paint[0].Comments.SequenceEqual(new[] { "강" }) && c.Paint[1].Comments.Count == 2, "paint 줄 앞 주석 읽음");
        Check(c.DroppedComments == 1, $"paint 밖 주석 1개만 버림 (실제 {c.DroppedComments})");
        string saved = c.ToFileText();
        Check(saved.Contains("// 다리 1") && saved.Contains("\"weather\": \"fog\""), "주석 · 추가 필드 보존");
        Check(MapDoc.Parse(saved).ToFileText() == saved, "주석 포함 왕복 바이트 동일");
        Check(Semantic(Game(withComments, "c")) == Semantic(Game(saved, "c")), "주석 지도 의미 불변");
    }
}
