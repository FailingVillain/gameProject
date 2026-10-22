using System.Globalization;
using System.Numerics;
using MapEditor.Model;

namespace MapEditor.App;

public readonly record struct Step(InputFrame? Frame, string? Shot);

/// <summary>
/// 입력 재생 스크립트 (한 줄 한 명령, # 주석). 좌표는 전부 타일 좌표 -- 창 크기와 무관하게 같은 결과.
///  new W H · open ID|경로 · tool NAME · terrain NAME · size N · button ID (버튼 중앙을 실제로 클릭) · key K · drag x0 y0 x1 y1 · rdrag … (오른쪽 = 지우개)
///  path x y x y … (꺾은선 드래그) · click x y · zoom Z · center x y · wheel N x y · grid on|off · wait N · shot FILE · save PATH · hash · expect HASH · assert KEY=VALUE
/// 명령은 지연 실행되는 반복자다: 버튼 위치 · 카메라는 명령이 시작될 때의 값을 쓴다.
/// </summary>
public sealed class ScriptPlayer
{
    readonly Editor ed;
    readonly string[] lines;
    readonly string name;
    Vector2 mouse = new(700, 300);
    public static readonly HashSet<string> Clicked = new();
    public int Failures;
    public readonly List<string> Log = new();

    public ScriptPlayer(Editor ed, string name, IEnumerable<string> lines) { this.ed = ed; this.name = name; this.lines = lines.ToArray(); }

    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    void Fail(int ln, string msg) { Failures++; Log.Add($"FAIL {name}:{ln + 1}: {msg}"); }

    InputFrame Frame(bool l = false, bool r = false, bool m = false, float wheel = 0, string? key = null)
    {
        var f = new InputFrame { Mouse = mouse, Left = l, Right = r, Middle = m, Wheel = wheel };
        if (key != null) f.Keys.Add(key);
        return f;
    }

    Vector2 ScreenOf(float tx, float ty) => ed.Cam.WorldToScreen(new Vector2(tx + 0.5f, ty + 0.5f));

    IEnumerable<Step> Drag(float[] pts, bool right)
    {
        mouse = ScreenOf(pts[0], pts[1]);
        yield return new Step(Frame(), null);                          // 호버
        yield return new Step(Frame(!right, right), null);             // 누름
        for (int i = 2; i + 1 < pts.Length; i += 2)
        {
            Vector2 a = ScreenOf(pts[i - 2], pts[i - 1]), b = ScreenOf(pts[i], pts[i + 1]);
            for (int s = 1; s <= 4; s++)
            {
                mouse = Vector2.Lerp(a, b, s / 4f);
                yield return new Step(Frame(!right, right), null);
            }
        }
        yield return new Step(Frame(), null);                          // 뗌
    }

    public IEnumerable<Step> Steps()
    {
        for (int ln = 0; ln < lines.Length; ln++)
        {
            string raw = lines[ln];
            int hash = raw.IndexOf('#'); if (hash >= 0) raw = raw[..hash];
            var a = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (a.Length == 0) continue;
            string cmd = a[0].ToLowerInvariant();
            IEnumerable<Step>? sub = null;
            try
            {
                switch (cmd)
                {
                    case "new": ed.LoadDoc(Editor.NewDoc(int.Parse(a[1]), int.Parse(a[2])), "새 지도", null); break;
                    case "open": if (!ed.OpenPath(a[1])) Fail(ln, ed.Status); break;
                    case "tool":
                        if (!Enum.TryParse<Tool>(a[1], true, out var t)) Fail(ln, "도구 이름 " + a[1]); else ed.Tool = t; break;
                    case "terrain":
                        int pi = Array.IndexOf(Editor.PaletteNames, a[1].ToLowerInvariant());
                        if (pi < 0) Fail(ln, "지형 이름 " + a[1]); else ed.Palette = pi; break;
                    case "size": ed.BrushSize = Math.Clamp(int.Parse(a[1]), 1, 15); break;
                    case "grid": ed.ShowGrid = a[1] == "on"; break;
                    case "zoom": ed.Cam.SetZoom(F(a[1])); break;
                    case "center": ed.Cam.CenterOn(new Vector2(F(a[1]) + 0.5f, F(a[2]) + 0.5f)); break;
                    case "fit": ed.FitCamera(); break;
                    case "button":
                        var b = ed.ButtonById(a[1]);
                        if (b == null) { Fail(ln, "버튼 없음 " + a[1]); break; }
                        Clicked.Add(b.Id);
                        mouse = new Vector2(b.Rect.X + b.Rect.Width / 2, b.Rect.Y + b.Rect.Height / 2);
                        sub = ClickSteps(); break;
                    case "click": mouse = ScreenOf(F(a[1]), F(a[2])); sub = ClickSteps(); break;
                    case "drag": sub = Drag(a.Skip(1).Take(4).Select(F).ToArray(), false); break;
                    case "rdrag": sub = Drag(a.Skip(1).Take(4).Select(F).ToArray(), true); break;
                    case "path": sub = Drag(a.Skip(1).Select(F).ToArray(), false); break;
                    case "wheel": mouse = ScreenOf(F(a[2]), F(a[3])); sub = new[] { new Step(Frame(wheel: F(a[1])), null) }; break;
                    case "key": sub = new[] { new Step(Frame(key: a[1].ToLowerInvariant()), null) }; break;
                    case "wait": sub = Enumerable.Range(0, int.Parse(a[1])).Select(_ => new Step(Frame(), null)); break;
                    case "shot": sub = new[] { new Step(Frame(), null), new Step(null, a[1]) }; break;
                    case "save": if (!ed.SaveTo(a[1])) Fail(ln, ed.Status); break;
                    case "hash": Log.Add($"{name}:{ln + 1} hash {ed.Doc.CanonicalHash()} paint {ed.Doc.Paint.Count} undo {ed.Doc.UndoDepth}"); break;
                    case "expect":
                        if (ed.Doc.CanonicalHash() != a[1].ToUpperInvariant()) Fail(ln, $"지도 해시 {ed.Doc.CanonicalHash()} != 기대 {a[1]}"); break;
                    case "assert":
                        foreach (var kv in a.Skip(1)) if (!Assert(kv)) Fail(ln, $"assert {kv} (실제: {Probe(kv.Split('=')[0])})");
                        break;
                    default: Fail(ln, "알 수 없는 명령 " + cmd); break;
                }
            }
            catch (Exception e) when (e is FormatException or IndexOutOfRangeException or OverflowException) { Fail(ln, $"인자 오류 '{raw.Trim()}'"); }
            if (sub != null) foreach (var s in sub) yield return s;
        }
    }

    IEnumerable<Step> ClickSteps()
    {
        yield return new Step(Frame(), null);
        yield return new Step(Frame(true), null);
        yield return new Step(Frame(), null);
    }

    string Probe(string key)
    {
        var g = ed.Doc.Raster();
        if (key.StartsWith("t(") )   // t(x,y) = 그 칸 지형 이름
        {
            var xy = key[2..^1].Split(','); int x = int.Parse(xy[0]), y = int.Parse(xy[1]);
            return g.InBounds(x, y) ? MapDoc.TerrainNames[g.At(x, y)] : "oob";
        }
        if (key.StartsWith("h(") )   // h(x,y) = 고지 여부
        {
            var xy = key[2..^1].Split(','); int x = int.Parse(xy[0]), y = int.Parse(xy[1]);
            return g.InBounds(x, y) ? (g.High[y * g.Width + x] ? "1" : "0") : "oob";
        }
        return key switch
        {
            "tool" => ed.Tool.ToString().ToLowerInvariant(),
            "terrain" => ed.CurrentTerrain,
            "size" => ed.BrushSize.ToString(),
            "undo" => ed.Doc.UndoDepth.ToString(),
            "paint" => ed.Doc.Paint.Count.ToString(),
            "grid" => ed.ShowGrid ? "on" : "off",
            "high" => ed.ShowHigh ? "on" : "off",
            "dirty" => ed.Dirty ? "1" : "0",
            "popup" => ed.PopupOpen ? "1" : "0",
            "width" => ed.Doc.Width.ToString(),
            _ => "?"
        };
    }

    bool Assert(string kv)
    {
        int eq = kv.IndexOf('=');
        return eq > 0 && Probe(kv[..eq]) == kv[(eq + 1)..];
    }
}
