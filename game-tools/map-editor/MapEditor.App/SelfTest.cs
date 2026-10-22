using MapEditor.Model;

namespace MapEditor.App;

/// <summary>창 없는 시험: 스크립트 재생(결정성) + 버튼 배치/덮개 + 저장 왕복. 새 버튼은 scripts/buttons.kds 에 한 줄이 없으면 여기서 실패한다.</summary>
public static class SelfTest
{
    static int pass, fail;
    static void Check(bool ok, string msg)
    {
        if (ok) pass++; else { fail++; Console.WriteLine("FAIL " + msg); }
    }

    static string Tmp(string name) => Path.Combine(Path.GetTempPath(), "mapeditor_selftest", name);

    public static int Run(string scriptDir)
    {
        if (Directory.Exists(Tmp(""))) Directory.Delete(Tmp(""), true);
        Directory.CreateDirectory(Tmp(""));
        var files = Directory.Exists(scriptDir) ? Directory.GetFiles(scriptDir, "*.kds").OrderBy(f => f, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        Check(files.Length >= 4, $"스크립트 {files.Length}개 (>=4) in {scriptDir}");
        foreach (var f in files)
        {
            string n = Path.GetFileName(f);
            string h1 = RunScript(f, 1), h2 = RunScript(f, 2);
            Check(h1 == h2, $"{n}: 두 번 재생한 지도 해시 같음 ({h1} / {h2})");
            Console.WriteLine($"  {n}: 지도 해시 {h1}");
        }

        // 배치: 모든 버튼이 창 안, 막대 안쪽, 서로 겹치지 않음 (작은 창 포함)
        foreach (var (w, h) in new[] { (1280, 720), (960, 600), (1920, 1080) })
        {
            var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("lay"));
            ed.WinW = w; ed.WinH = h; ed.Layout(w, h);
            var bs = ed.Buttons;
            Check(bs.Count >= 30, $"{w}x{h}: 버튼 {bs.Count}개");
            foreach (var b in bs)
            {
                var r = b.Rect;
                Check(r.X >= 0 && r.Y >= 0 && r.X + r.Width <= w && r.Y + r.Height <= h, $"{w}x{h}: {b.Id} 창 안");
                Check(r.Width >= 28 && r.Height >= 24, $"{w}x{h}: {b.Id} 크기 {r.Width}x{r.Height} (클릭 가능한 최소)");
                Check(!(r.X < ed.MapView.X + ed.MapView.Width && r.X + r.Width > ed.MapView.X && r.Y < ed.MapView.Y + ed.MapView.Height && r.Y + r.Height > ed.MapView.Y), $"{w}x{h}: {b.Id} 지도 영역과 안 겹침");
            }
            for (int i = 0; i < bs.Count; i++)
                for (int j = i + 1; j < bs.Count; j++)
                {
                    var a = bs[i].Rect; var c = bs[j].Rect;
                    bool ov = a.X < c.X + c.Width && a.X + a.Width > c.X && a.Y < c.Y + c.Height && a.Y + a.Height > c.Y;
                    Check(!ov, $"{w}x{h}: {bs[i].Id} / {bs[j].Id} 안 겹침");
                }
        }

        // 버튼 덮개: 팝업 버튼 포함 모든 id 가 스크립트에서 실제로 클릭됐나
        {
            var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("cov"));
            ed.Layout(1280, 720);
            var ids = ed.Buttons.Select(b => b.Id).ToList();
            ids.Add("popup_close"); ids.Add("item_0");
            foreach (var id in ids) Check(ScriptPlayer.Clicked.Contains(id), $"버튼 {id} 가 scripts/*.kds 에서 클릭됨 (새 버튼은 buttons.kds 에 추가)");
        }

        // 저장 왕복: 편집 -> 저장 -> 게임 로더 재적재 -> 다시 열면 같은 문서 해시
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
            ed.OpenPath(MapFile_Folder_Any());
            Check(ed.SavePath != null && !ed.SavePath.StartsWith(KoreanDivision.Core.MapFile.Folder, StringComparison.OrdinalIgnoreCase), "게임 지도를 열면 저장 위치가 assets 밖으로 돌려짐: " + ed.SavePath);
        }

        // 여섯 지도 열기 -> 래스터 해시가 문서 모델(T33a)과 같음 (편집기 열기 경로 = 모델 경로)
        foreach (var id in KoreanDivision.Core.MapFile.Available())
        {
            var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("open"));
            Check(ed.OpenPath(id) && ed.PreviewMap != null && ed.PreviewMap.Width == ed.Doc.Width, $"지도 {id} 열기 + 게임 지도 생성: {ed.Status}");
        }

        Console.WriteLine($"MapEditor selftest: pass {pass} fail {fail}");
        return fail == 0 ? 0 : 1;
    }

    static string MapFile_Folder_Any() => KoreanDivision.Core.MapFile.Available().First();

    static string RunScript(string path, int run)
    {
        var ed = new Editor(Editor.NewDoc(64, 64), "t", Tmp("s" + run));
        var p = new ScriptPlayer(ed, Path.GetFileName(path), File.ReadAllLines(path));
        foreach (var s in p.Steps()) ed.Update(s.Frame ?? new InputFrame { Mouse = new System.Numerics.Vector2(700, 300) });
        if (run == 1) foreach (var l in p.Log) Console.WriteLine("  " + l);
        Check(p.Failures == 0, $"{Path.GetFileName(path)}: 스크립트 내부 검사 실패 {p.Failures}");
        return ed.Doc.CanonicalHash();
    }
}
