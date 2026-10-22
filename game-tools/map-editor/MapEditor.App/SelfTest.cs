using System.Numerics;
using KoreanDivision;
using KoreanDivision.Core;
using MapEditor.Model;
using Raylib_cs;

namespace MapEditor.App;

/// <summary>창 없는 시험: 스크립트 재생(결정성) + 버튼 배치/덮개 + 저장 왕복 + 미션 왕복 + 테스트 실행 파일. 새 버튼은 scripts/*.kds 에서 클릭되지 않으면 여기서 실패한다.</summary>
public static class SelfTest
{
    static int pass, fail;
    static void Check(bool ok, string msg)
    {
        if (ok) pass++; else { fail++; Console.WriteLine("FAIL " + msg); }
    }

    static string Tmp(string name) => Path.Combine(Path.GetTempPath(), "mapeditor_selftest", name);

    static bool Overlap(Rectangle a, Rectangle c) => a.X < c.X + c.Width && a.X + a.Width > c.X && a.Y < c.Y + c.Height && a.Y + a.Height > c.Y;

    public static int Run(string scriptDir)
    {
        Editor.Launcher = Editor.RecordLaunch;   // 시험은 게임 창을 띄우지 않는다
        if (Directory.Exists(Tmp(""))) Directory.Delete(Tmp(""), true);
        Directory.CreateDirectory(Tmp(""));
        var files = Directory.Exists(scriptDir) ? Directory.GetFiles(scriptDir, "*.kds").OrderBy(f => f, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        Check(files.Length >= 10, $"스크립트 {files.Length}개 (>=10) in {scriptDir}");
        foreach (var f in files)
        {
            string n = Path.GetFileName(f);
            string h1 = RunScript(f, 1), h2 = RunScript(f, 2);
            Check(h1 == h2, $"{n}: 두 번 재생한 지도 해시 같음 ({h1} / {h2})");
            Console.WriteLine($"  {n}: 지도 해시 {h1}");
        }

        // 배치: 모든 도구 x 지도 / 미션 x 3 해상도에서 모든 버튼이 창 안, 최소 크기, 서로 겹치지 않음, 지도 영역과 안 겹침
        var allIds = new HashSet<string>();
        foreach (var (w, h) in new[] { (1280, 720), (960, 600), (1920, 1080) })
            foreach (bool withMission in new[] { false, true })
            {
                var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("lay"));
                if (withMission) Check(ed.OpenPath("c0_02_yalu") && ed.Mission != null, "배치 시험용 미션 열기");
                ed.WinW = w; ed.WinH = h;
                foreach (var tool in Enum.GetValues<Tool>())
                {
                    ed.Tool = tool; ed.Layout(w, h);
                    string tag = $"{w}x{h}{(withMission ? " 미션" : "")} {tool}";
                    var bs = ed.Buttons;
                    Check(bs.Count >= 30, $"{tag}: 버튼 {bs.Count}개");
                    Check(ed.MapView.Width >= 300 && ed.MapView.Height >= 300, $"{tag}: 지도 영역 {ed.MapView.Width}x{ed.MapView.Height}");
                    foreach (var b in bs)
                    {
                        allIds.Add(b.Id);
                        var r = b.Rect;
                        Check(r.X >= 0 && r.Y >= 0 && r.X + r.Width <= w && r.Y + r.Height <= h, $"{tag}: {b.Id} 창 안");
                        Check(r.Width >= 28 && r.Height >= 24, $"{tag}: {b.Id} 크기 {r.Width}x{r.Height} (클릭 가능한 최소)");
                        Check(!Overlap(r, ed.MapView), $"{tag}: {b.Id} 지도 영역과 안 겹침");
                    }
                    for (int i = 0; i < bs.Count; i++)
                        for (int j = i + 1; j < bs.Count; j++)
                            Check(!Overlap(bs[i].Rect, bs[j].Rect), $"{tag}: {bs[i].Id} / {bs[j].Id} 안 겹침");
                    foreach (var l in ed.Labels)
                        Check(l.X >= 0 && l.Y >= 0 && l.Y < h && l.X < w, $"{tag}: 글자 '{l.Text}' 창 안");
                }
            }
        // 팝업 (열기 목록): 창 안 + 서로 겹치지 않음 + 패널 안
        foreach (var (w, h) in new[] { (960, 600), (1280, 720) })
        {
            var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("pop"));
            ed.WinW = w; ed.WinH = h;
            for (int tab = 0; tab < 3; tab++)
            {
                ed.PopupOpen = true; ed.PopupTab = tab; ed.Layout(w, h);
                var ms = ed.Buttons.Where(b => b.Modal).ToList();
                var pr = ed.PopupRect;
                foreach (var b in ms)
                {
                    allIds.Add(b.Id);
                    Check(b.Rect.X >= pr.X && b.Rect.Y >= pr.Y && b.Rect.X + b.Rect.Width <= pr.X + pr.Width && b.Rect.Y + b.Rect.Height <= pr.Y + pr.Height, $"팝업 {w}x{h} 탭{tab}: {b.Id} 패널 안");
                    Check(b.Rect.X + b.Rect.Width <= w && b.Rect.Y + b.Rect.Height <= h, $"팝업 {w}x{h}: {b.Id} 창 안");
                }
                for (int i = 0; i < ms.Count; i++)
                    for (int j = i + 1; j < ms.Count; j++) Check(!Overlap(ms[i].Rect, ms[j].Rect), $"팝업 {w}x{h}: {ms[i].Id} / {ms[j].Id} 안 겹침");
            }
        }

        // 버튼 덮개: 모든 도구 · 미션 상태 · 팝업의 모든 id 가 스크립트에서 실제로 클릭됐나 (팝업 목록 item_1~7 은 item_0 이 같은 코드 경로라 제외)
        foreach (var id in allIds)
        {
            if (id.StartsWith("item_") && id != "item_0") continue;
            Check(ScriptPlayer.Clicked.Contains(id), $"버튼 {id} 가 scripts/*.kds 에서 클릭됨 (새 버튼은 scripts 에 추가)");
        }
        Console.WriteLine($"  버튼 id {allIds.Count}개 덮개 확인");

        // 저장 왕복 (지도): 편집 -> 저장 -> 게임 로더 재적재 -> 다시 열면 같은 문서 해시
        {
            var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("save"));
            var steps = new ScriptPlayer(ed, "inline", new[] { "tool rect", "terrain water", "drag 5 5 30 9", "tool circle", "terrain rock", "drag 40 40 45 40", "key ctrl+s" });
            foreach (var _ in steps.Steps()) ed.Update(_.Frame ?? new InputFrame());
            Check(steps.Failures == 0 && ed.SavePath != null && File.Exists(ed.SavePath), "Ctrl+S 가 파일을 씀: " + ed.SavePath);
            if (ed.SavePath != null && File.Exists(ed.SavePath))
            {
                var back = MapDoc.Parse(File.ReadAllText(ed.SavePath));
                Check(back.CanonicalHash() == ed.Doc.CanonicalHash(), "저장 파일을 다시 열면 같은 문서 해시");
                Check(!ed.Dirty, "저장 뒤 변경 표시 꺼짐");
                Check(!File.Exists(ed.SavePath + ".tmp"), "임시 파일 남지 않음");
            }
            ed.OpenPath(MapFile.Available().First());
            Check(ed.SavePath != null && !ed.SavePath.StartsWith(MapFile.Folder, StringComparison.OrdinalIgnoreCase), "게임 지도를 열면 저장 위치가 assets 밖으로 돌려짐: " + ed.SavePath);
        }

        // 여섯 지도 + 모든 미션 열기 -> 게임 지도 생성 + 검사 오류 없음
        foreach (var id in MapFile.Available().Where(i => !i.StartsWith("_editor_")))
        {
            var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("open"));
            Check(ed.OpenPath(id) && ed.PreviewMap != null && ed.PreviewMap.Width == ed.Doc.Width && ed.Mission == null, $"지도 {id} 열기 + 게임 지도 생성: {ed.Status}");
            Check(ed.ErrorCount == 0, $"지도 {id}: 검사 오류 0 ({string.Join("; ", ed.Issues.Where(i => i.Error).Take(2).Select(i => i.Message))})");
        }
        int missions = 0;
        foreach (var id in MissionDef.Available().Where(i => !i.StartsWith("_editor_")))
        {
            string assetPath = MissionDef.PathOf(id);
            string before = File.ReadAllText(assetPath);
            var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("openm"));
            Check(ed.OpenPath(id) && ed.Mission != null && ed.PreviewMap != null && !Directory.Exists(Tmp("openm") + "x"), $"미션 {id} 열기: {ed.Status}");
            Check(ed.ErrorCount == 0, $"미션 {id}: 검사 오류 0 ({string.Join("; ", ed.Issues.Where(i => i.Error).Take(2).Select(i => i.Message))})");
            Check(ed.SavePath != null && !ed.SavePath.StartsWith(MissionDef.Folder, StringComparison.OrdinalIgnoreCase), $"미션 {id}: 저장 위치가 assets 밖: {ed.SavePath}");
            Check(ed.Save(), $"미션 {id}: 저장 성공 {ed.Status}");
            Check(File.ReadAllText(ed.SavePath!) == before, $"미션 {id}: 편집 없이 저장하면 원문 바이트 동일");
            Check(File.ReadAllText(assetPath) == before, $"미션 {id}: assets 원본 불변");
            missions++;
        }
        Check(missions >= 15, $"미션 {missions}개 열기 시험 (>=15)");

        MissionEditRoundTrip();
        TestPlayChecks();

        Console.WriteLine($"MapEditor selftest: pass {pass} fail {fail}");
        return fail == 0 ? 0 : 1;
    }

    // 미션 편집 -> 저장 -> 다시 열기: 같은 상태, 편집 밖 바이트 불변, 게임이 읽음
    static void MissionEditRoundTrip()
    {
        string orig = File.ReadAllText(MissionDef.PathOf("c0_02_yalu"));
        var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("mrt"));
        var sp = new ScriptPlayer(ed, "mrt", new[]
        {
            "open c0_02_yalu",
            "tool prop", "pick rock_l", "click 100 150", "rclick 100 150", "click 101 151",
            "tool area", "name zz_new", "drag 10 10 20 18",
            "tool zone", "drag 70 140 90 150",
            "tool prop", "button scatter_toggle", "key ctrl+s",
        });
        foreach (var s in sp.Steps()) ed.Update(s.Frame ?? new InputFrame { Mouse = new Vector2(700, 300) });
        Check(sp.Failures == 0, "미션 편집 스크립트 실패 없음: " + string.Join("; ", sp.Log));
        Check(ed.SavePath != null && File.Exists(ed.SavePath), "미션 저장 파일: " + ed.SavePath);
        if (ed.SavePath == null || !File.Exists(ed.SavePath)) return;
        string saved = File.ReadAllText(ed.SavePath);
        var def = MissionDef.Parse(saved, "c0_02_yalu");
        Check(def.Props.Count == 1 && def.Props[0].Type == "rock_l" && Math.Abs(def.Props[0].X - 101.5) < 1e-6, "저장본: 소품 하나 (지우고 다시 놓음)");
        Check(def.Areas.ContainsKey("zz_new") && def.Areas.Count == ed.Mission!.Areas.Count && def.DeployZones.Count == 2, "저장본: 구역 · 배치 구역 추가");
        Check(!def.PropScatter, "저장본: 흩뿌리기 껐음 (원본은 켬)");
        var ed2 = new Editor(Editor.NewDoc(64, 64), "t", Tmp("mrt2"));
        Check(ed2.OpenPath(ed.SavePath) && ed2.Mission != null && ed2.Mission.StateKey() == ed.Mission!.StateKey(), "저장본 다시 열기 = 같은 상태");
        Check(ed2.Doc.CanonicalHash() == ed.Doc.CanonicalHash(), "저장본 다시 열기 = 같은 지도");
        Check(!ed2.Dirty && ed2.Mission!.ToText() == saved, "다시 열고 저장하면 같은 바이트");
        Check(File.ReadAllText(MissionDef.PathOf("c0_02_yalu")) == orig, "assets 원본 미션 불변");
        // 되돌리기 전부 -> 원문과 같은 바이트
        int guard = 0;
        while (ed.Doc.CanUndo && guard++ < 50) ed.Doc.Undo();
        {   // 저장 뒤라 기준이 저장본이다: 원문과 의미가 같은지 (바이트 동일은 저장 전 되돌리기를 Tests 가 검사)
            var back = MissionDef.Parse(ed.Mission!.ToText(), "c0_02_yalu");
            var o = MissionDef.Parse(orig, "c0_02_yalu");
            Check(back.Props.Count == 0 && back.Areas.Count == o.Areas.Count && back.DeployZones.Count == o.DeployZones.Count && back.PropScatter == o.PropScatter && ed.Doc.CanonicalHash() == MapDoc.Parse(MissionMapData.Extract(orig)!).CanonicalHash(), "전부 되돌리면 원본과 같은 의미 (소품 · 구역 · 배치 구역 · 흩뿌리기 · 지도)");
        }

        // map 참조 미션: 내장으로 저장
        string refMission = "{\n  \"id\": \"t_ref\", \"title\": \"t\", \"map\": \"battle_flat\", \"seed\": 3,\n  \"factions\": [ { \"name\": \"가\", \"en\": \"korean\" }, { \"name\": \"나\", \"en\": \"japanese\" } ],\n  \"objectives\": [ { \"id\": \"o\", \"type\": \"capture\", \"area\": \"g\", \"primary\": true } ]\n}\n";
        string rp = Path.Combine(Tmp("ref"), "t_ref.json");
        Directory.CreateDirectory(Tmp("ref")); File.WriteAllText(rp, refMission);
        var e3 = new Editor(Editor.NewDoc(64, 64), "t", Tmp("ref"));
        Check(e3.OpenPath(rp) && e3.Mission is { NeedsEmbed: true }, "map 참조 미션 열기");
        Check(e3.ErrorCount > 0 && e3.Issues.Any(i => i.Kind == "ref"), "없는 구역 참조 -> 오류 항목");
        e3.Tool = Tool.Area; e3.AreaName = "g";
        var sp3 = new ScriptPlayer(e3, "ref", new[] { "drag 200 60 210 70", "key ctrl+s" });
        foreach (var s in sp3.Steps()) e3.Update(s.Frame ?? new InputFrame { Mouse = new Vector2(700, 300) });
        Check(e3.ErrorCount == 0 && e3.Mission is { NeedsEmbed: false }, "구역을 만들면 오류 해소 · 저장하면 내장: " + e3.Status);
        string t3 = File.ReadAllText(rp);
        Check(t3.Contains("\"mapData\"") && !t3.Contains("\"map\":") && MissionDef.Parse(t3, "t_ref").MapData != null, "저장본에 mapData 내장 · map 참조 제거");
    }

    static void TestPlayChecks()
    {
        // 지도
        var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("tp"));
        var sp = new ScriptPlayer(ed, "tp", new[] { "tool rect", "terrain water", "drag 20 20 25 25", "key f5" });
        foreach (var s in sp.Steps()) ed.Update(s.Frame ?? new InputFrame());
        Check(ed.LastLaunch == "--map _editor_test", "F5 지도: 실행 인자 " + ed.LastLaunch);
        try
        {
            var g = MapFile.Load(Editor.TestId, Config.DefaultSeed);
            Check(g.Get(22, 22) == TerrainType.Water, "F5 지도: 게임이 같은 폴더에서 편집 지도를 읽음");
        }
        catch (Exception e) { Check(false, "F5 지도: 게임 로드 실패 " + e.Message); }
        // 미션
        var em = new Editor(Editor.NewDoc(64, 64), "t", Tmp("tpm"));
        var sm = new ScriptPlayer(em, "tpm", new[] { "open village_defense", "tool prop", "click 60 60", "button testplay" });
        foreach (var s in sm.Steps()) em.Update(s.Frame ?? new InputFrame { Mouse = new Vector2(700, 300) });
        Check(em.LastLaunch == "--mission _editor_test --mission-start", "F5 미션: 실행 인자 " + em.LastLaunch);
        try
        {
            var d = MissionDef.Load(Editor.TestId);
            Check(d.Props.Count == 1 && d.MapData != null, "F5 미션: 게임이 편집된 미션을 읽음");
        }
        catch (Exception e) { Check(false, "F5 미션: 게임 로드 실패 " + e.Message); }
        // 오류가 있으면 실행하지 않음
        var eb = new Editor(Editor.NewDoc(64, 64), "t", Tmp("tpb"));
        eb.LastLaunch = "";
        var sb = new ScriptPlayer(eb, "tpb", new[] { "tool start", "click 3 3", "click 4 4" });   // 첫 시작 위치 세 개
        foreach (var s in sb.Steps()) eb.Update(s.Frame ?? new InputFrame());
        eb.Tool = Tool.Brush; eb.Palette = Array.IndexOf(Editor.PaletteNames, "water");
        var sc = new ScriptPlayer(eb, "tpb2", new[] { "tool rect", "terrain water", "drag 0 0 63 63", "key f5" });
        foreach (var s in sc.Steps()) eb.Update(s.Frame ?? new InputFrame());
        Check(eb.ErrorCount > 0 && eb.LastLaunch == "", "시작이 물 위(오류)면 F5 는 실행하지 않음: " + eb.Status);
        foreach (var p in new[] { MapFile.PathOf(Editor.TestId), MissionDef.PathOf(Editor.TestId) }) if (File.Exists(p)) File.Delete(p);
    }

    static string RunScript(string path, int run)
    {
        var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("s" + run));
        var p = new ScriptPlayer(ed, Path.GetFileName(path), File.ReadAllLines(path));
        foreach (var s in p.Steps()) ed.Update(s.Frame ?? new InputFrame { Mouse = new Vector2(700, 300) });
        if (run == 1) foreach (var l in p.Log) Console.WriteLine("  " + l);
        Check(p.Failures == 0, $"{Path.GetFileName(path)}: 스크립트 내부 검사 실패 {p.Failures}");
        return ed.Doc.CanonicalHash() + (ed.Mission != null ? "/" + MapDoc.Hash(ed.Mission.StateKey()) : "");
    }
}
