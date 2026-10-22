using System.Globalization;
using System.Numerics;
using MapEditor.Model;

namespace MapEditor.App;

public readonly record struct Step(InputFrame? Frame, string? Shot);

/// <summary>
/// 입력 재생 스크립트 (한 줄 한 명령, # 주석). 좌표는 전부 타일 좌표 -- 창 크기와 무관하게 같은 결과.
///  new W H · open ID|경로 (지도 · 미션 모두) · tool NAME · terrain NAME · size N · pick 종류 (현재 도구의 소품 · 시설 · 유닛 종류) · button ID (버튼 중앙을 실제로 클릭) · key K · drag x0 y0 x1 y1 · rdrag … (오른쪽 = 지우개 / 삭제)
///  path x y x y … (꺾은선 드래그) · click x y · rclick x y · type 글자 (이름 칸에 친 글자) · name 글자 (이름 칸에 바로 넣기) · zoom Z · center x y · wheel N x y · grid on|off · wait N · shot FILE · save PATH · hash · expect HASH · assert KEY=VALUE (KEY~=부분문자열)
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

    InputFrame Frame(bool l = false, bool r = false, bool m = false, float wheel = 0, string? key = null, string chars = "")
    {
        var f = new InputFrame { Mouse = mouse, Left = l, Right = r, Middle = m, Wheel = wheel, Chars = chars };
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
                        if (!Enum.TryParse<Tool>(a[1], true, out var t)) Fail(ln, "도구 이름 " + a[1]); else { ed.Tool = t; ed.Layout(ed.WinW, ed.WinH); } break;
                    case "terrain":
                        int pi = Array.IndexOf(Editor.PaletteNames, a[1].ToLowerInvariant());
                        if (pi < 0) Fail(ln, "지형 이름 " + a[1]); else ed.Palette = pi; break;
                    case "size": ed.BrushSize = Math.Clamp(int.Parse(a[1]), 1, 15); break;
                    case "grid": ed.ShowGrid = a[1] == "on"; break;
                    case "zoom": ed.SetZoom(F(a[1])); break;
                    case "center": ed.Cam.CenterOn(new Vector2(F(a[1]) + 0.5f, F(a[2]) + 0.5f)); break;
                    case "fit": ed.FitCamera(); break;
                    case "button":
                        var b = ed.ButtonById(a[1]);
                        if (b == null) { Fail(ln, "버튼 없음 " + a[1]); break; }
                        Clicked.Add(b.Id);
                        mouse = new Vector2(b.Rect.X + b.Rect.Width / 2, b.Rect.Y + b.Rect.Height / 2);
                        sub = ClickSteps(); break;
                    case "click": mouse = ScreenOf(F(a[1]), F(a[2])); sub = ClickSteps(); break;
                    case "rclick": mouse = ScreenOf(F(a[1]), F(a[2])); sub = ClickSteps(true); break;
                    case "type": sub = new[] { new Step(Frame(chars: a[1]), null) }; break;
                    case "name": ed.AreaName = a[1]; break;
                    case "pick":
                        {
                            string[] list = ed.Tool switch { Tool.Prop => Editor.PropTypes, Tool.Site => Editor.SiteTypes, Tool.Unit => Editor.UnitTypes, _ => Array.Empty<string>() };
                            int ix = Array.IndexOf(list, a[1]);
                            if (ix < 0) { Fail(ln, $"종류 '{a[1]}' 없음 (도구 {ed.Tool})"); break; }
                            if (ed.Tool == Tool.Prop) ed.PropSel = ix; else if (ed.Tool == Tool.Site) ed.SiteSel = ix; else ed.UnitSel = ix;
                            break;
                        }
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
                        foreach (var kv in a.Skip(1)) if (!Assert(kv)) Fail(ln, $"assert {kv} (실제: {Probe(kv.Split(new[] { '=', '~', '>' })[0])})");
                        break;
                    default: Fail(ln, "알 수 없는 명령 " + cmd); break;
                }
            }
            catch (Exception e) when (e is FormatException or IndexOutOfRangeException or OverflowException) { Fail(ln, $"인자 오류 '{raw.Trim()}'"); }
            if (sub != null) foreach (var s in sub) yield return s;
        }
    }

    IEnumerable<Step> ClickSteps(bool right = false)
    {
        yield return new Step(Frame(), null);
        yield return new Step(Frame(!right, right), null);
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
        if (key.StartsWith("area(")) { var ar = ed.Mission?.Areas.FirstOrDefault(x => x.Name == key[5..^1]); return ar == null ? "none" : Shape(ar.Rect, ar.Circle); }
        if (key.StartsWith("zone(")) { int i = int.Parse(key[5..^1]); return ed.Mission != null && i < ed.Mission.Zones.Count ? $"f{ed.Mission.Zones[i].Faction}:{Shape(ed.Mission.Zones[i].Rect, ed.Mission.Zones[i].Circle)}" : "none"; }
        if (key.StartsWith("prop(")) { int i = int.Parse(key[5..^1]); return ed.Mission != null && i < ed.Mission.Props.Count ? $"{ed.Mission.Props[i].Type}@{Num(ed.Mission.Props[i].X)},{Num(ed.Mission.Props[i].Y)}" : "none"; }
        if (key.StartsWith("start(")) { int i = int.Parse(key[6..^1]); return i < ed.Doc.Starts.Count ? $"{ed.Doc.Starts[i][0]},{ed.Doc.Starts[i][1]}" : "none"; }
        if (key.StartsWith("site(")) { int i = int.Parse(key[5..^1]); return i < ed.Doc.Sites.Count ? $"{ed.Doc.Sites[i].Type}@{ed.Doc.Sites[i].X},{ed.Doc.Sites[i].Y},{ed.Doc.Sites[i].Owner}" : "none"; }
        if (key.StartsWith("unit(")) { int i = int.Parse(key[5..^1]); return i < ed.Doc.Units.Count ? $"{ed.Doc.Units[i].Unit}@{Num(ed.Doc.Units[i].X)},{Num(ed.Doc.Units[i].Y)},{ed.Doc.Units[i].Faction},{ed.Doc.Units[i].Count}" : "none"; }
        if (key.StartsWith("issue(")) { int i = int.Parse(key[6..^1]); return i < ed.Issues.Count ? $"{(ed.Issues[i].Error ? "E" : "W")}:{ed.Issues[i].Kind}" : "none"; }
        return key switch
        {
            "hover" => $"{ed.Hover.X},{ed.Hover.Y},{ed.HoverIn}",
            "dragging" => ed.Dragging ? "1" : "0",
            "status" => ed.Status.Replace(' ', '_'),
            "props" => (ed.Mission?.Props.Count ?? -1).ToString(),
            "areas" => (ed.Mission?.Areas.Count ?? -1).ToString(),
            "zones" => (ed.Mission?.Zones.Count ?? -1).ToString(),
            "starts" => ed.Doc.Starts.Count.ToString(),
            "sites" => ed.Doc.Sites.Count.ToString(),
            "units" => ed.Doc.Units.Count.ToString(),
            "errors" => ed.ErrorCount.ToString(),
            "warnings" => ed.WarnCount.ToString(),
            "issues" => ed.Issues.Count.ToString(),
            "mission" => ed.Mission != null ? "1" : "0",
            "embed" => ed.Mission?.NeedsEmbed == true ? "1" : "0",
            "scatter" => ed.Mission == null ? "none" : ed.Mission.PropScatter ? "on" : "off",
            "scatterview" => ed.ScatterPreview ? "on" : "off",
            "scatterlist" => ed.ScatterList.Count.ToString(),
            "density" => ed.Mission == null ? "none" : Num(ed.Mission.PropDensity),
            "cell" => ed.Mission == null ? "none" : ed.Mission.PropCell.ToString(),
            "launch" => ed.LastLaunch.Replace(' ', '+'),
            "prop_sel" => Editor.PropTypes[Math.Clamp(ed.PropSel, 0, Editor.PropTypes.Length - 1)],
            "site_sel" => Editor.SiteTypes.Length == 0 ? "-" : Editor.SiteTypes[Math.Clamp(ed.SiteSel, 0, Editor.SiteTypes.Length - 1)],
            "unit_sel" => Editor.UnitTypes[Math.Clamp(ed.UnitSel, 0, Editor.UnitTypes.Length - 1)],
            "owner" => ed.OwnerSel.ToString(),
            "count" => ed.UnitCount.ToString(),
            "zfaction" => ed.ZoneFaction.ToString(),
            "area_name" => ed.AreaName,
            "area_sel" => ed.AreaSel ?? "none",
            "zone_sel" => ed.ZoneSel.ToString(),
            "shape" => ed.AreaCircle ? "circle" : "rect",
            "textfocus" => ed.TextFocus ? "1" : "0",
            "tab" => ed.PopupTab.ToString(),
            "page" => ed.PopupPage.ToString(),
            "listpage" => ed.ListPage.ToString(),
            "isspage" => ed.IssuePage.ToString(),
            "issue_sel" => ed.IssueSel.ToString(),
            "undodepth" => ed.Doc.UndoDepth.ToString(),
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

    static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    static string Shape(double[]? rect, double[]? circle) =>
        rect is { Length: 4 } r ? $"r:{Num(r[0])},{Num(r[1])},{Num(r[2])},{Num(r[3])}" : circle is { Length: 3 } c ? $"c:{Num(c[0])},{Num(c[1])},{Num(c[2])}" : "none";

    bool Assert(string kv)
    {
        int ge = kv.IndexOf(">=", StringComparison.Ordinal);
        if (ge > 0) return double.TryParse(Probe(kv[..ge]), NumberStyles.Float, CultureInfo.InvariantCulture, out var have) && double.TryParse(kv[(ge + 2)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var want) && have >= want;
        int ct = kv.IndexOf("~=", StringComparison.Ordinal);
        if (ct > 0) return Probe(kv[..ct]).Contains(kv[(ct + 2)..], StringComparison.Ordinal);
        int eq = kv.IndexOf('=');
        return eq > 0 && Probe(kv[..eq]) == kv[(eq + 1)..];
    }
}
