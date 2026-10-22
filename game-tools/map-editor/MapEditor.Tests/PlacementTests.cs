using System.Text.Json;
using KoreanDivision.Core;
using MapEditor.Model;

// T33c: 미션 오버레이 (props / areas / deployZones / propScatter) 문서 + 검사기 + 테스트 실행 파일 왕복.
static partial class Tests
{
    static readonly string[] Editable = { "mapdata", "map", "props", "propscatter", "propdensity", "propcell", "areas", "deployzones" };

    static Func<string, string?> MapLoader => id =>
    {
        string p = Path.Combine(root, "assets", "maps", id + ".json");
        return File.Exists(p) ? File.ReadAllText(p) : null;
    };

    static bool PropKnown(string t) => PropDefs.Current.TryGet(t, out _);

    /// <summary>편집 대상이 아닌 최상위 속성의 (이름, 원문 값) 목록 -- 순서 포함.</summary>
    static List<(string Name, string Raw)> OtherProps(string text)
    {
        using var jd = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return jd.RootElement.EnumerateObject().Where(p => !Editable.Contains(p.Name.ToLowerInvariant())).Select(p => (p.Name, p.Value.GetRawText())).ToList();
    }

    static string Idx(string text, string prop)
    {
        using var jd = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var p in jd.RootElement.EnumerateObject()) if (string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase)) return p.Value.GetRawText();
        return "";
    }

    static void PlacementAll()
    {
        // 편집 가능 속성이 모두 미리 있는 미션에서는 편집 속성 값 밖 바이트가 전부 같아야 한다
        var files = Directory.GetFiles(Path.Combine(root, "assets", "missions"), "*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        int n = 0, withProps = 0;
        foreach (var f in files)
        {
            string id = Path.GetFileNameWithoutExtension(f), orig = File.ReadAllText(f);
            if (MissionMapData.Extract(orig) == null) continue;   // "map" 참조 미션 (예: test_river_crossing): mapData 없음 = 이 시험 대상 아님 (T33d 카드 병합 지시)
            MissionDoc m;
            try { m = MissionDoc.Parse(orig, MapLoader, id); }
            catch (InvalidDataException e) { Check(false, $"{id}: MissionDoc.Parse 실패 {e.Message}"); continue; }
            n++;
            Check(m.ToText() == orig, $"{id}: 편집 없이 ToText = 원문 바이트 동일");
            var warn = new List<string>();
            var errs = m.Validate(PropKnown, warn);
            Check(errs.Count == 0, $"{id}: 원본 미션 Validate 통과 ({string.Join("; ", errs.Take(2))})");
            Check(TryParse(orig, id), $"{id}: 원본이 MissionDef.Parse 통과");

            // 편집: 소품 2개 + 구역 하나 추가/하나 변경 + 배치 구역 하나 + 흩뿌리기 숫자
            m.Map.BeginStroke("시험");
            try
            {
                m.AddProp(new PropPlace("rock_m", 3.5, 3.5));
                m.AddProp(new PropPlace("tree_pine", 4.5, 3.5));
                m.SetArea("zz_edit", new double[] { 2, 2, 5, 5 }, null);
                if (m.Areas.Count > 1) m.SetArea(m.Areas[0].Name, null, new double[] { 20, 20, 4 });
                m.AddZone(new ZoneDef(1, new double[] { 1, 1, 9, 9 }, null));
                m.SetScatter(true, 2.5, 8);
            }
            finally { m.Map.EndStroke(); }
            Check(m.Map.UndoDepth == 1, $"{id}: 편집 묶음 = undo 1단계");
            string edited = m.ToText();
            Check(TryParse(edited, id), $"{id}: 편집본이 MissionDef.Parse 통과");
            var o1 = OtherProps(orig); var o2 = OtherProps(edited);
            Check(o1.SequenceEqual(o2), $"{id}: 편집 밖 최상위 속성 원문 · 순서 동일");
            Check(m.Map.CanonicalHash() == MapDoc.Parse(MissionMapData.Extract(orig)!).CanonicalHash(), $"{id}: 지도 문서 불변");
            Check(OuterBytesSame(orig, edited), $"{id}: 편집 속성 값 밖 바이트 동일 (주석 포함)");
            // 게임이 읽은 값 == 문서
            var def = MissionDef.Parse(edited, id);
            Check(def.Props.Count == m.Props.Count && def.Props.Any(p => p.Type == "tree_pine" && Math.Abs(p.X - 4.5) < 1e-6), $"{id}: 게임이 읽은 소품 = 문서");
            Check(def.Areas.ContainsKey("zz_edit") && def.Areas.Count == m.Areas.Count, $"{id}: 게임이 읽은 구역 = 문서");
            Check(def.DeployZones.Count == m.Zones.Count && def.PropScatter && def.PropCell == 8 && Math.Abs(def.PropDensity - 2.5) < 1e-6, $"{id}: 게임이 읽은 배치 구역 · 흩뿌리기 = 문서");
            if (def.Props.Count > 0) withProps++;
            // 다시 읽기: 같은 문서 상태
            var again = MissionDoc.Parse(edited, MapLoader, id);
            Check(again.StateKey() == m.StateKey() && again.ToText() == edited, $"{id}: 편집본 다시 열기 = 같은 상태 · 재저장 바이트 동일");
            // undo 전부 = 원문
            Check(m.Map.Undo() && m.ToText() == orig, $"{id}: undo 하면 원문 바이트로 돌아옴");
            Check(m.Map.Redo() && m.ToText() == edited, $"{id}: redo 하면 편집본");
        }
        Check(n >= 15, $"미션 {n}개 시험 (>=15)");
        Check(withProps == n, $"모든 미션 편집본에 소품 반영 ({withProps}/{n})");
        Console.WriteLine($"  MissionDoc 미션 {n}개");
    }

    /// <summary>두 텍스트에서 편집 대상 최상위 속성 (이름 ~ 값 끝) 을 지운 나머지가 같은가. 새로 끼운 속성의 앞 쉼표 하나는 허용 (공백 · 쉼표 정규화).</summary>
    static bool OuterBytesSame(string a, string b)
    {
        static string Strip(string t)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(t);
            var spans = new List<(int S, int E)>();
            var r = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
            while (r.Read())
                if (r.TokenType == JsonTokenType.PropertyName && r.CurrentDepth == 1 && Editable.Contains(r.GetString()!.ToLowerInvariant()))
                {
                    int s = (int)r.TokenStartIndex;
                    r.Read();
                    if (r.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) r.Skip();
                    spans.Add((s, (int)r.BytesConsumed));
                }
            var sb = new System.Text.StringBuilder();
            int pos = 0;
            foreach (var (s, e) in spans) { sb.Append(System.Text.Encoding.UTF8.GetString(bytes, pos, s - pos)); pos = e; }
            sb.Append(System.Text.Encoding.UTF8.GetString(bytes, pos, bytes.Length - pos));
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"[\s,]+", "");
        }
        return Strip(a) == Strip(b);
    }

    // 속성이 하나도 없는 미션 (map 참조): 새 속성 끼워 넣기 + 내장
    static void PlacementInsertAndEmbed()
    {
        string mission = "{\n  // 설명 주석\n  \"id\": \"t_ins\",\n  \"title\": \"t\",\n  \"seed\": 5,\n  \"map\": \"battle_flat\",\n  \"player\": 1,\n" +
            "  \"factions\": [ { \"name\": \"가\", \"en\": \"korean\" }, { \"name\": \"나\", \"en\": \"japanese\" } ],\n" +
            "  \"objectives\": [ { \"id\": \"o1\", \"type\": \"capture\", \"area\": \"goal\", \"primary\": true } ]\n}\n";
        var m = MissionDoc.Parse(mission, MapLoader, "t_ins");
        Check(m.NeedsEmbed && m.MapRef == "battle_flat" && m.FactionCount == 2 && m.Seed == 5, "map 참조 미션 읽기");
        Check(m.Refs.Count == 1 && m.Refs[0].IsObjective && m.Refs[0].Name == "goal", "목표의 area 참조 수집");
        var errs = m.Validate(PropKnown);
        Check(errs.Any(e => e.Contains("goal")), "없는 구역 참조는 오류: " + string.Join(";", errs));
        m.SetArea("goal", new double[] { 200, 50, 20, 20 }, null);
        m.AddProp(new PropPlace("rock_l", 100.5, 60.5));
        m.AddZone(new ZoneDef(1, null, new double[] { 30, 64, 10 }));
        m.SetScatter(true, 3, 0);
        Check(m.Validate(PropKnown).Count == 0, "구역 추가 후 Validate 통과: " + string.Join(";", m.Validate(PropKnown)));
        string t = m.ToText();
        Check(t.Contains("// 설명 주석") && t.Contains("\"seed\": 5") && !t.Contains("\"map\":") && t.Contains("\"mapData\""), "내장 + 나머지 보존");
        var def = MissionDef.Parse(t, "t_ins");
        Check(def.Props.Count == 1 && def.Areas.ContainsKey("goal") && def.DeployZones.Count == 1 && def.PropScatter && Math.Abs(def.PropDensity - 3) < 1e-9 && def.PropCell == 0, "끼워 넣은 속성을 게임이 읽음");
        Check(OtherProps(mission).Where(p => p.Name != "map").SequenceEqual(OtherProps(t)), "다른 속성 원문 동일");
        var back = MissionDoc.Parse(t, MapLoader);
        Check(!back.NeedsEmbed && back.StateKey() == m.StateKey() && back.ToText() == t, "내장본 다시 열기 · 재저장 동일");
        m.MarkSaved(t);
        Check(!m.NeedsEmbed && m.ToText() == t && !m.OverlayChanged, "저장 표시 뒤 ToText 불변");

        // 쉼표 끝 · 한 줄 미션
        var one = MissionDoc.Parse("{ \"id\": \"x\", \"map\": \"battle_flat\", \"factions\": [{},{}], }", MapLoader);
        one.AddProp(new PropPlace("rock_s", 1.5, 1.5));
        string o = one.ToText();
        Check(MissionMapData.Extract(o) != null && Idx(o, "props").Contains("rock_s"), "한 줄 · 끝 쉼표 미션도 끼워 넣기 성공");

        // 이름 규칙 · 중복 · 가역
        bool threw = false;
        try { m.SetArea("Bad Name", new double[] { 0, 0, 1, 1 }, null); } catch (InvalidDataException) { threw = true; }
        Check(threw, "구역 이름 규칙 위반은 오류");
        int depth = m.Map.UndoDepth;
        m.SetArea("goal", new double[] { 1, 1, 3, 3 }, null);
        Check(m.Areas.Count(a => a.Name == "goal") == 1 && m.Map.UndoDepth == depth + 1, "같은 이름 = 모양 교체 (중복 없음)");
        m.Map.Undo();
        Check(m.Areas.First(a => a.Name == "goal").Rect![0] == 200, "구역 교체 undo");
        m.RemoveArea("goal"); Check(m.Areas.Count == 0, "구역 삭제"); m.Map.Undo(); Check(m.Areas.Count == 1, "구역 삭제 undo");
        m.RemoveProp(0); m.Map.Undo(); Check(m.Props.Count == 1, "소품 삭제 undo");
        m.RemoveZone(0); m.Map.Undo(); Check(m.Zones.Count == 1, "배치 구역 삭제 undo");

        // 모르는 키 보존
        var ex = MissionDoc.Parse("{ \"id\": \"x\", \"mapData\": { \"width\": 8, \"height\": 8, \"starts\": [[1,1],[6,6]] }, \"props\": [ { \"type\": \"rock_s\", \"x\": 2, \"y\": 2, \"note\": [1,2] } ], " +
            "\"areas\": { \"a\": { \"rect\": [0,0,2,2], \"label\": \"x\" } }, \"deployZones\": [ { \"faction\": 1, \"rect\": [0,0,3,3], \"tag\": true } ], \"factions\": [{},{}] }", MapLoader);
        ex.AddProp(new PropPlace("rock_s", 5.5, 5.5)); ex.SetArea("a", new double[] { 0, 0, 4, 4 }, null); ex.ReplaceZone(0, new double[] { 0, 0, 4, 4 }, null);
        string et = ex.ToText();
        Check(et.Contains("\"note\": [1,2]") && et.Contains("\"label\": \"x\"") && et.Contains("\"tag\": true"), "모르는 키 (props · areas · deployZones) 원문 보존");

        // 쓰레기 입력은 InvalidDataException 하나
        foreach (var g in new[] { "", "[]", "{", "{\"id\":1}", "{\"mapData\":5}", "{\"map\":\"nope_zz\"}", "{\"mapData\":{\"width\":8,\"height\":8},\"props\":5}", "{\"mapData\":{\"width\":8,\"height\":8},\"areas\":[]}",
                     "{\"mapData\":{\"width\":8,\"height\":8},\"props\":[{\"type\":5}]}", "{\"mapData\":{\"width\":8,\"height\":8},\"propCell\":\"a\"}", "{\"mapData\":{\"width\":8,\"height\":8},\"deployZones\":[1]}" })
        {
            bool other = false;
            try { MissionDoc.Parse(g, MapLoader); } catch (InvalidDataException) { } catch { other = true; }
            Check(!other, $"MissionDoc.Parse 쓰레기 '{(g.Length > 24 ? g[..24] : g)}' 는 InvalidDataException 만");
        }
        Check(MissionDoc.LooksLikeMission(mission) && !MissionDoc.LooksLikeMission(File.ReadAllText(Path.Combine(root, "assets", "maps", "battle_flat.json"))), "미션 / 지도 파일 구별");

        // Validate 가 잡는 것
        var bad = MissionDoc.Parse("{ \"id\": \"x\", \"mapData\": { \"width\": 8, \"height\": 8, \"starts\": [[1,1],[6,6]] }, \"factions\": [{},{}], \"props\": [ { \"type\": \"nope\", \"x\": 99, \"y\": 1 } ], " +
            "\"areas\": { \"a\": { \"rect\": [0,0,0,2] } }, \"deployZones\": [ { \"faction\": 5, \"rect\": [0,0,3,3] } ], \"propDensity\": -1 }", MapLoader);
        var be = bad.Validate(PropKnown);
        Check(be.Any(e => e.Contains("nope")) && be.Any(e => e.Contains("지도 밖")) && be.Any(e => e.Contains("'a'")) && be.Any(e => e.Contains("세력 5")) && be.Any(e => e.Contains("propDensity")), "Validate: 종류 · 범위 · 구역 모양 · 세력 · 밀도: " + string.Join(";", be));
        var dupA = MissionDoc.Parse("{ \"id\": \"x\", \"mapData\": { \"width\": 8, \"height\": 8 }, \"areas\": { \"a\": { \"rect\": [0,0,1,1] }, \"a\": { \"rect\": [1,1,1,1] } } }", MapLoader);
        Check(dupA.Validate(PropKnown).Any(e => e.Contains("중복")), "구역 이름 중복은 오류");
    }

    // 검사기
    static void InspectorChecks()
    {
        // 48x48: 가운데 세로 물 띠 (x 14..17) 가 지도를 둘로 가름, 오른쪽 위 구석 물에 둘러싸인 3x3 섬, 시작 3개
        string txt = "{ \"width\": 48, \"height\": 48, \"base\": \"grass\", \"paint\": [ { \"terrain\": \"water\", \"rect\": [14, 0, 4, 48] }, " +
            "{ \"terrain\": \"water\", \"rect\": [24, 2, 7, 7] }, { \"terrain\": \"grass\", \"rect\": [26, 4, 3, 3] }, { \"terrain\": \"rock\", \"rect\": [5, 5, 2, 2] } ], " +
            "\"starts\": [[3, 20], [28, 28], [27, 5]] }";
        var doc = MapDoc.Parse(txt);
        var gm = Game(txt.Replace("\"starts\": [[3, 20], [28, 28], [27, 5]]", "\"starts\": [[3, 20], [28, 28], [27, 5]]"), "ins");
        var env = new InspectEnv { Walkable = (x, y) => gm.IsTerrainWalkable(x, y), PropRadius = t => PropDefs.Current.TryGet(t, out var d) ? d.Radius : null, SiteSize = k => Defs.Current.SiteKind(k)?.Size ?? 0, UnitExists = k => Defs.Current.Units.ContainsKey(k) };
        var iss = Inspector.Run(doc, null, env);
        Check(iss.Any(i => i.Error && i.Kind == "start" && i.Message.Contains("갇힘") && Math.Abs(i.X - 27) < 1e-9), "섬 위 시작은 갇힘 오류: " + string.Join("; ", iss.Select(i => i.Message)));
        Check(iss.Any(i => !i.Error && i.Message.Contains("시작 2") && i.Message.Contains("이어지지")), "강 건너 시작은 경고");
        Check(!iss.Any(i => i.Message.Contains("시작 1") && i.Error), "정상 시작 1 은 오류 없음");

        doc.AddStart(15, 10);   // 물 위
        doc.AddStart(60, 3);    // 지도 밖
        iss = Inspector.Run(doc, null, env);
        Check(iss.Any(i => i.Error && i.Message.Contains("시작 4") && i.Message.Contains("통행 불가")), "물 위 시작 오류");
        Check(iss.Any(i => i.Error && i.Message.Contains("시작 5") && i.Message.Contains("지도 밖")), "지도 밖 시작 오류");
        doc.AddStart(4, 21);    // 시작 1 과 가까움
        iss = Inspector.Run(doc, null, env);
        Check(iss.Any(i => !i.Error && i.Message.Contains("가까움")), "시작끼리 가까움 경고");

        // 시설: 겹침 · 물 위
        var kinds = Defs.Current.SiteKinds.Select(k => k.Key).ToList();
        string k0 = kinds[0];
        var d2 = MapDoc.Parse(txt);
        d2.AddSite(new SiteDef(8, 8, k0, 0)); d2.AddSite(new SiteDef(9, 9, k0, 0)); d2.AddSite(new SiteDef(15, 15, k0, 0)); d2.AddSite(new SiteDef(8, 8, "nope", 0)); d2.AddSite(new SiteDef(20, 20, k0, 9));
        iss = Inspector.Run(d2, null, env);
        Check(iss.Any(i => i.Kind == "site" && i.Message.Contains("겹침")), "시설 겹침 경고");
        Check(iss.Any(i => i.Kind == "site" && i.Message.Contains("통행 불가")), "물 위 시설 경고");
        Check(iss.Any(i => i.Error && i.Kind == "site" && i.Message.Contains("'nope'")), "없는 시설 종류 오류");
        Check(iss.Any(i => i.Error && i.Kind == "site" && i.Message.Contains("소유 세력 9")), "소유 세력 범위 오류");
        // 게임은 소유 세력 범위 밖 시설을 거부한다 (검사기와 일치)
        bool gameRejects = false;
        try { Game(d2.ToFileText(), "x"); } catch (InvalidDataException) { gameRejects = true; }
        Check(gameRejects && d2.Validate().Count > 0, "Validate 도 게임처럼 거부");

        // 미션 층: 소품 겹침 · 물 위 · 구역 끊김 · 목표 도달성
        string mt = "{ \"id\": \"m\", \"mapData\": " + txt + ", \"factions\": [{},{}], \"objectives\": [ { \"id\": \"o1\", \"type\": \"capture\", \"area\": \"far\", \"primary\": true }, { \"id\": \"o2\", \"type\": \"capture\", \"area\": \"missing\" } ], " +
            "\"areas\": { \"far\": { \"circle\": [28, 28, 3] }, \"off\": { \"rect\": [100, 100, 4, 4] }, \"edge\": { \"rect\": [45, 45, 5, 5] } }, \"props\": [ { \"type\": \"rock_m\", \"x\": 9.5, \"y\": 9.5 }, { \"type\": \"rock_m\", \"x\": 9.9, \"y\": 9.5 }, { \"type\": \"tree_pine\", \"x\": 15.5, \"y\": 3.5 } ], " +
            "\"deployZones\": [ { \"faction\": 3, \"rect\": [0, 0, 4, 4] } ] }";
        var mm = MissionDoc.Parse(mt, MapLoader);
        iss = Inspector.Run(mm.Map, mm, env);
        Check(iss.Any(i => i.Kind == "prop" && i.Message.Contains("겹침")), "소품 겹침 경고");
        Check(iss.Any(i => i.Kind == "prop" && i.Message.Contains("통행 불가")), "물 위 소품 경고");
        Check(iss.Any(i => i.Error && i.Kind == "ref" && i.Message.Contains("'missing'")), "참조 끊김 오류");
        Check(iss.Any(i => i.Kind == "ref" && !i.Error && i.Message.Contains("'far'") && i.Message.Contains("닿지")), "목표 구역 도달성 경고");
        Check(iss.Any(i => i.Kind == "area" && i.Message.Contains("'off'")) && iss.Any(i => i.Kind == "area" && i.Message.Contains("'edge'")), "구역 지도 밖 / 일부 밖");
        Check(iss.Any(i => i.Error && i.Kind == "zone" && i.Message.Contains("세력 3")), "배치 구역 세력 범위 오류");
        var again = Inspector.Run(mm.Map, mm, env);
        Check(again.Select(i => i.Message).SequenceEqual(iss.Select(i => i.Message)), "같은 문서 -> 같은 검사 목록 (순서 포함)");

        // 모든 지도 · 미션 원본은 검사 오류 0 (게임이 쓰는 에셋은 오류 없이 통과해야 한다)
        int checkedN = 0;
        foreach (var f in Directory.GetFiles(Path.Combine(root, "assets", "missions"), "*.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            string id = Path.GetFileNameWithoutExtension(f);
            var md = MissionDoc.Parse(File.ReadAllText(f), MapLoader, id);
            var g = Game(md.Map.ToFileText(), id);
            var e2 = new InspectEnv { Walkable = (x, y) => g.IsTerrainWalkable(x, y), PropRadius = env.PropRadius, SiteSize = env.SiteSize, UnitExists = env.UnitExists };
            var r = Inspector.Run(md.Map, md, e2);
            Check(!r.Any(i => i.Error), $"{id}: 원본 미션 검사 오류 0 ({string.Join("; ", r.Where(i => i.Error).Take(2).Select(i => i.Message))})");
            checkedN++;
        }
        foreach (var f in Directory.GetFiles(Path.Combine(root, "assets", "maps"), "*.json"))
        {
            string id = Path.GetFileNameWithoutExtension(f);
            var md = MapDoc.Parse(File.ReadAllText(f));
            var g = Game(File.ReadAllText(f), id);
            var r = Inspector.Run(md, null, new InspectEnv { Walkable = (x, y) => g.IsTerrainWalkable(x, y), PropRadius = env.PropRadius, SiteSize = env.SiteSize, UnitExists = env.UnitExists });
            Check(!r.Any(i => i.Error), $"{id}: 원본 지도 검사 오류 0 ({string.Join("; ", r.Where(i => i.Error).Take(2).Select(i => i.Message))})");
            checkedN++;
        }
        Console.WriteLine($"  검사 원본 {checkedN}개");
    }

    static void SourceGuardTests()
    {
        string src = Path.Combine(root, "assets", "maps", "battle_flat.json");
        Check(EditorPaths.IsProtectedSource(src, MapFile.Folder), "보호: 에셋 원본 경로");
        Check(EditorPaths.IsProtectedSource(Path.Combine(root, "assets", "missions", "x.json")), "보호: 미션 원본 경로");
        Check(!EditorPaths.IsProtectedSource(Path.Combine(Path.GetTempPath(), "maps_edit", "a.json"), MapFile.Folder), "비보호: 임시 저장 폴더");
        Check(EditorPaths.TestId != "_editor_test" && EditorPaths.TestId.EndsWith("_" + Environment.ProcessId), "테스트 id 는 프로세스 고유: " + EditorPaths.TestId);
    }

    // 테스트 실행 파일: 게임이 읽는 위치에 써서 게임 로더로 왕복 (새 게임 플래그 없이)
    static void TestPlayFiles()
    {
        string maps = MapFile.Folder, missions = MissionDef.Folder;
        Directory.CreateDirectory(maps); Directory.CreateDirectory(missions);
        string mp = Path.Combine(maps, EditorPaths.TestId + ".json"), sp = Path.Combine(missions, EditorPaths.TestId + ".json");
        try
        {
            // (1) 지도: 편집 -> 저장 텍스트 -> 게임 폴더 -> MapFile.Load
            var d = MapDoc.Parse(File.ReadAllText(Path.Combine(root, "assets", "maps", "battle_flat.json")));
            d.PaintRect("water", 10, 10, 5, 5); d.AddStart(100, 100); d.AddSite(new SiteDef(50, 50, Defs.Current.SiteKinds.First().Key, 1));
            File.WriteAllText(mp, d.ToFileText());
            var g = MapFile.Load(EditorPaths.TestId, 1920);
            Check(g.Get(12, 12) == TerrainType.Water && g.StartTiles.Count == 3 && g.ResourceSites.Count >= 1, "왕복 1 (지도): 편집 지도를 게임 폴더에서 MapFile.Load 로 읽음");
            Check(Semantic(g) == Semantic(Game(d.ToFileText(), "x")), "왕복 1: 게임이 읽은 의미 = 문서");

            // (2) 미션 mapData 편집 -> 게임 MissionDef.Load
            string orig = File.ReadAllText(Path.Combine(root, "assets", "missions", "c0_02_yalu.json"));
            var m = MissionDoc.Parse(orig, MapLoader, "c0_02_yalu");
            m.Map.PaintRect("sand", 5, 5, 4, 4); m.AddProp(new PropPlace("rock_l", 50.5, 40.5)); m.SetArea("zz_new", new double[] { 5, 5, 10, 10 }, null); m.AddZone(new ZoneDef(1, new double[] { 70, 140, 30, 10 }, null));
            File.WriteAllText(sp, m.ToText());
            var def = MissionDef.Load(EditorPaths.TestId);
            Check(def.Props.Count == 1 && def.Areas.ContainsKey("zz_new") && def.DeployZones.Count == 2 && def.MapData is { ValueKind: JsonValueKind.Object }, "왕복 2 (미션 mapData): 게임이 편집된 소품 · 구역 · 배치 구역 · 지도를 읽음");
            var gm = Game(def.MapData!.Value.GetRawText(), "m");
            Check(gm.Get(6, 6) == TerrainType.Sand, "왕복 2: 미션 지도에 칠한 모래가 있음");

            // (3) map 참조 미션 -> 내장 저장 -> 게임
            string refMission = "{\n  \"id\": \"t_ref\", \"title\": \"t\", \"map\": \"battle_flat\", \"seed\": 3,\n  \"factions\": [ { \"name\": \"가\", \"en\": \"korean\" }, { \"name\": \"나\", \"en\": \"japanese\" } ],\n  \"objectives\": [ { \"id\": \"o\", \"type\": \"capture\", \"area\": \"g\", \"primary\": true } ]\n}\n";
            var r = MissionDoc.Parse(refMission, MapLoader, "t_ref");
            r.Map.PaintCircle("rock", 100, 60, 4); r.SetArea("g", null, new double[] { 200, 64, 6 });
            File.WriteAllText(sp, r.ToText());
            var def3 = MissionDef.Load(EditorPaths.TestId);
            Check(def3.Areas.ContainsKey("g") && def3.MapData is { ValueKind: JsonValueKind.Object } && def3.Map == null, "왕복 3 (map 참조 -> 내장): 게임이 내장 지도 미션으로 읽음");
        }
        finally
        {
            if (File.Exists(mp)) File.Delete(mp);
            if (File.Exists(sp)) File.Delete(sp);
        }
    }
}
