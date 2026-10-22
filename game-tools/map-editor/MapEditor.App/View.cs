using System.Numerics;
using KoreanDivision;
using KoreanDivision.Core;
using KoreanDivision.Render;
using KoreanDivision.UI;
using MapEditor.Model;
using Raylib_cs;

namespace MapEditor.App;

/// <summary>편집기 화면 그리기. 지도는 게임의 MapRenderer 그대로 (같은 색 · 무늬), 그 위에 편집 표시만 얹는다. 프레임마다 문서를 다시 래스터하지 않는다 (캐시된 격자 · 목록만 읽음).</summary>
public sealed class View : IDisposable
{
    MapRenderer? renderer;

    public void Dispose() { renderer?.Dispose(); renderer = null; }

    public void Draw(Editor ed, Vector2 mouse, string? shotPath)
    {
        if (ed.RenderDirty && ed.PreviewMap != null)
        {
            renderer?.Dispose();
            renderer = new MapRenderer(ed.PreviewMap);
            ed.RenderDirty = false;
        }
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Palette.Void);
        var mv = ed.MapView;
        Raylib.BeginScissorMode((int)mv.X, (int)mv.Y, (int)mv.Width, (int)mv.Height);
        Raylib.BeginMode2D(ed.Cam.Cam);
        renderer?.Draw(ed.Cam, ed.ShowGrid);
        DrawOverlays(ed);
        Raylib.EndMode2D();
        Raylib.EndScissorMode();
        DrawChrome(ed, mouse);
        if (shotPath != null)
        {
            Rlgl.DrawRenderBatchActive();
            Image img = Raylib.LoadImageFromScreen();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(shotPath))!);
            Raylib.ExportImage(img, shotPath);
            Raylib.UnloadImage(img);
        }
        Raylib.EndDrawing();
    }

    const int TS = Config.TileSize;

    static Color TerrainColor(string name)
    {
        int i = Array.IndexOf(Editor.PaletteNames, name);
        if (i < 0) return Color.White;
        return Editor.SwatchColor(name);
    }

    static Color Owner(int o) => o <= 0 ? Palette.Neutral : Palette.Player(o);
    static readonly Color ErrCol = new(255, 110, 100, 255);
    static readonly Color WarnCol = new(255, 190, 90, 255);

    static Color AreaColor(string name)
    {
        int h = 17;
        foreach (char c in name) h = h * 31 + c;
        Color[] cols = { new(90, 200, 255, 255), new(255, 140, 220, 255), new(170, 255, 120, 255), new(255, 170, 90, 255), new(190, 150, 255, 255), new(120, 255, 220, 255) };
        return cols[(h & 0x7fffffff) % cols.Length];
    }

    static Color PropColor(string type) => type.StartsWith("tree") ? new Color(70, 170, 80, 255) : new Color(175, 170, 160, 255);

    /// <summary>드래그 중인 획이 확정되면 칠해질 칸 (미리보기). 확정 코드와 같은 함수로 만든다.</summary>
    public static IEnumerable<(int X, int Y)> PreviewCells(Editor ed)
    {
        if (!ed.Dragging || !ed.IsTerrainTool) yield break;
        var a = ed.DragStart; var b = ed.DragCur;
        bool brushLike = ed.Tool is Tool.Brush or Tool.Eraser || ed.DragButton == 1;
        if (brushLike) { foreach (var c in ed.DragCells.Distinct()) yield return c; yield break; }
        switch (ed.Tool)
        {
            case Tool.Line: foreach (var c in MapDoc.LineCells(a.X, a.Y, b.X, b.Y, ed.BrushSize)) yield return c; break;
            case Tool.Rect:
                for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
                for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++) yield return (x, y);
                break;
            case Tool.Circle:
                int r = (int)MathF.Round(MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)));
                for (int y = a.Y - r; y <= a.Y + r; y++)
                for (int x = a.X - r; x <= a.X + r; x++)
                    if ((x - a.X) * (x - a.X) + (y - a.Y) * (y - a.Y) <= r * r) yield return (x, y);
                break;
        }
    }

    static void DrawShape(double[]? rect, double[]? circle, Color col, float lw, int fillAlpha)
    {
        if (rect is { Length: 4 } r)
        {
            var rc = new Rectangle((float)r[0] * TS, (float)r[1] * TS, (float)r[2] * TS, (float)r[3] * TS);
            if (fillAlpha > 0) Raylib.DrawRectangleRec(rc, Palette.Alpha(col, fillAlpha));
            Raylib.DrawRectangleLinesEx(rc, lw, col);
        }
        else if (circle is { Length: 3 } c)
        {
            var ctr = new Vector2((float)c[0] * TS, (float)c[1] * TS);
            float rad = (float)c[2] * TS;
            if (fillAlpha > 0) Raylib.DrawCircleV(ctr, rad, Palette.Alpha(col, fillAlpha));
            Raylib.DrawRing(ctr, Math.Max(0, rad - lw / 2), rad + lw / 2, 0, 360, 64, col);
        }
    }

    void DrawOverlays(Editor ed)
    {
        float z = ed.Cam.Zoom;
        float lw = Math.Max(1.5f, 2f / z);
        int fs = (int)Math.Clamp(14f / z, 8f, 220f);   // 화면에서 늘 14px 쯤 (멀리서 봐도 읽히게)
        float mr = 8f / z;                              // 멀리서도 보이는 최소 표식 반지름 (월드 px)
        var (vmin, vmax) = ed.Cam.VisibleWorld();
        int x0 = Math.Max(0, (int)vmin.X), y0 = Math.Max(0, (int)vmin.Y), x1 = Math.Min(ed.Doc.Width - 1, (int)vmax.X + 1), y1 = Math.Min(ed.Doc.Height - 1, (int)vmax.Y + 1);
        if (ed.ShowHigh)
        {
            var g = ed.Doc.Raster();
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (g.High[y * g.Width + x]) Raylib.DrawRectangleLinesEx(new Rectangle(x * TS + 1, y * TS + 1, TS - 2, TS - 2), 2, new Color(255, 240, 120, 170));
        }

        DrawRails(ed, x0, y0, x1, y1);
        DrawRailPending(ed, lw);

        var m = ed.Mission;
        if (m != null)
        {
            foreach (var a in m.Areas)
            {
                var col = AreaColor(a.Name);
                bool sel = a.Name == ed.AreaSel;
                DrawShape(a.Rect, a.Circle, sel ? Palette.Accent : col, sel ? lw * 2 : lw, 34);
                var (cx, cy) = a.Rect is { Length: 4 } r ? (r[0], r[1]) : a.Circle is { Length: 3 } c ? (c[0] - c[2], c[1] - c[2]) : (0.0, 0.0);
                Raylib.DrawText(a.Name, (int)(cx * TS) + 3, (int)(cy * TS) + 3, fs, sel ? Palette.Accent : col);
            }
            for (int i = 0; i < m.Zones.Count; i++)
            {
                var zn = m.Zones[i];
                var col = Owner(zn.Faction);
                bool sel = i == ed.ZoneSel;
                DrawShape(zn.Rect, zn.Circle, sel ? Palette.Accent : col, sel ? lw * 2 : lw, 22);
                var (cx, cy) = zn.Center;
                Raylib.DrawText($"deploy {zn.Faction}", (int)(cx * TS) - 6 * fs / 2, (int)(cy * TS) - fs / 2, fs, sel ? Palette.Accent : col);
            }
            foreach (var p in m.Props) DrawProp(p.Type, (float)p.X, (float)p.Y, 255, false);
            if (ed.ScatterPreview)
                foreach (var s in ed.ScatterList) DrawProp(s.Type, s.X, s.Y, 110, true);
            foreach (var fp in m.ForcePoints)   // 읽기 전용: 병력 위치 (편성은 이 편집기 범위 밖)
            {
                var ctr = new Vector2((float)fp.X * TS, (float)fp.Y * TS);
                Raylib.DrawPoly(ctr, 4, Math.Max(TS * 0.35f, mr * 0.7f), 45, Palette.Alpha(Owner(fp.Faction), 180));
                Raylib.DrawPolyLines(ctr, 4, Math.Max(TS * 0.35f, mr * 0.7f), 45, Palette.Alpha(Color.Black, 200));
            }
        }

        for (int i = 0; i < ed.Doc.Sites.Count; i++)
        {
            var s = ed.Doc.Sites[i];
            int size = Defs.Current.SiteKind(s.Type)?.Size ?? 3;
            var rc = new Rectangle((s.X - size / 2) * TS, (s.Y - size / 2) * TS, size * TS, size * TS);
            var col = Owner(s.Owner);
            Raylib.DrawRectangleRec(rc, Palette.Alpha(col, 70));
            Raylib.DrawRectangleLinesEx(rc, lw, col);
            Raylib.DrawText($"{s.Type} o{s.Owner}", (int)rc.X + 3, (int)rc.Y + 3, fs, col);
        }
        foreach (var u in ed.Doc.Units)
        {
            var ctr = new Vector2((float)u.X * TS, (float)u.Y * TS);
            var col = Owner(u.Faction);
            Raylib.DrawCircleV(ctr, Math.Max(TS * 0.4f, mr * 0.6f), Palette.Alpha(col, 200));
            Raylib.DrawCircleLinesV(ctr, Math.Max(TS * 0.4f, mr * 0.6f), Color.Black);
            Raylib.DrawText(u.Count > 1 ? $"{u.Unit}x{u.Count}" : u.Unit, (int)ctr.X + TS / 2, (int)ctr.Y - fs / 2, fs, col);
        }
        for (int i = 0; i < ed.Doc.Starts.Count; i++)
        {
            var s = ed.Doc.Starts[i];
            bool moving = ed.Dragging && ed.Tool == Tool.Start && ed.DragStartIdx == i;
            var c = new Vector2((s[0] + 0.5f) * TS, (s[1] + 0.5f) * TS);
            var col = moving ? Palette.Alpha(Palette.Select, 90) : Palette.Select;
            Raylib.DrawRing(c, Math.Max(TS * 0.7f, mr) - lw / 2, Math.Max(TS * 0.7f, mr) + lw / 2, 0, 360, 32, col);
            Raylib.DrawText((i + 1).ToString(), (int)c.X - fs / 4, (int)c.Y - fs / 2, fs, col);
        }
        foreach (var iss in ed.Issues.Take(300))
        {
            var c = new Vector2((float)(iss.X + 0.5) * TS, (float)(iss.Y + 0.5) * TS);
            Raylib.DrawRing(c, Math.Max(TS * 0.85f, mr * 1.1f), Math.Max(TS * 0.85f, mr * 1.1f) + lw, 0, 360, 24, iss.Error ? ErrCol : WarnCol);
        }
        if (ed.IssueSel >= 0 && ed.IssueSel < ed.Issues.Count)
        {
            var iss = ed.Issues[ed.IssueSel];
            var c = new Vector2((float)(iss.X + 0.5) * TS, (float)(iss.Y + 0.5) * TS);
            float sr = Math.Max(TS * 1.4f, mr * 2f);
            Raylib.DrawRing(c, sr, sr + lw * 2, 0, 360, 32, Palette.Accent);
            Raylib.DrawLineEx(c - new Vector2(sr * 1.6f, 0), c + new Vector2(sr * 1.6f, 0), lw, Palette.Accent);
            Raylib.DrawLineEx(c - new Vector2(0, sr * 1.6f), c + new Vector2(0, sr * 1.6f), lw, Palette.Accent);
        }

        var cells = PreviewCells(ed).ToList();
        if (cells.Count > 0)
        {
            Color col = TerrainColor(ed.Tool == Tool.Eraser || ed.DragButton == 1 ? ed.Doc.Base : ed.CurrentTerrain);
            if (cells.Count <= 30000)
                foreach (var (x, y) in cells) Raylib.DrawRectangle(x * TS, y * TS, TS, TS, Palette.Alpha(col, 150));
            else { var a = ed.DragStart; Raylib.DrawCircleLinesV(new Vector2((a.X + 0.5f) * TS, (a.Y + 0.5f) * TS), 8, col); }
        }
        DrawPlaceGhost(ed, lw, fs);
        if (ed.HoverIn && !ed.PopupOpen && ed.IsTerrainTool)
        {
            int n = ed.Tool is Tool.Brush or Tool.Eraser or Tool.Line ? ed.BrushSize : 1;
            int lo = -((n - 1) / 2);
            Raylib.DrawRectangleLinesEx(new Rectangle((ed.Hover.X + lo) * TS, (ed.Hover.Y + lo) * TS, n * TS, n * TS), 2 / ed.Cam.Zoom + 1, Palette.Accent);
        }
    }

    static readonly Color RailSleeper = new(78, 58, 40, 255), RailSleeperDark = new(52, 40, 30, 255), RailTop = new(168, 168, 172, 255), RailSide = new(58, 58, 62, 255);

    /// <summary>선로 침목 · 레일. 게임에서는 BuildingRenderer 가 RailRenderer.Draw 로 덧그리므로(지형 타일은 자갈 바닥만) 편집기도 같은 규칙 · 같은 치수로 직접 덧그린다. rail 이 합쳐진 뒤에는 RailRenderer.Draw(PreviewMap, ...) 로 바꿔도 된다 (모양 동일).</summary>
    void DrawRails(Editor ed, int x0, int y0, int x1, int y1)
    {
        var g = ed.Doc.Raster();
        var segs = g.RailSegments();
        if (segs.Count == 0) return;
        float zoom = ed.Cam.Zoom;
        bool detail = zoom >= 0.6f;
        float px = 1f / zoom;
        foreach (var (x, y, ax, ay, bx, by) in segs)
        {
            if (x < x0 - 2 || x > x1 + 2 || y < y0 - 2 || y > y1 + 2) continue;
            Vector2 a = new Vector2(ax, ay) * TS, b = new Vector2(bx, by) * TS, d = b - a;
            float len = d.Length();
            Vector2 u = d / len, nrm = new(-u.Y, u.X);
            if (!detail) { Raylib.DrawLineEx(a, b, MathF.Max(2.5f * px, TS * 0.22f), RailSleeperDark); continue; }
            int nSl = Math.Max(1, (int)MathF.Round(len / (TS * 0.25f)));
            float half = TS * 0.36f, thick = MathF.Max(1.5f * px, TS * 0.09f);
            float ang = MathF.Atan2(nrm.Y, nrm.X) * 180f / MathF.PI;
            for (int i = 0; i < nSl; i++)
            {
                var p = a + d * ((i + 0.5f) / nSl);
                Raylib.DrawRectanglePro(new Rectangle(p.X, p.Y, half * 2f, thick), new Vector2(half, thick / 2f), ang, RailSleeper);
            }
            float gg = TS * 0.2f, ext = TS * 0.04f;
            for (int k = -1; k <= 1; k += 2)
            {
                var o = nrm * (gg * k);
                Vector2 ra = a + o - u * ext, rb = b + o + u * ext;
                Raylib.DrawLineEx(ra, rb, MathF.Max(1.5f * px, TS * 0.07f), RailSide);
                Raylib.DrawLineEx(ra - nrm * (TS * 0.012f), rb - nrm * (TS * 0.012f), MathF.Max(1f * px, TS * 0.035f), RailTop);
            }
        }
    }

    /// <summary>선로 도구로 찍는 중인 꺾은선: 확정되면 칠해질 칸 + 점 표식 + 마지막 점에서 마우스까지 고무줄.</summary>
    static void DrawRailPending(Editor ed, float lw)
    {
        if (ed.Tool != Tool.Rail) return;
        var pts = ed.RailPts;
        var col = Editor.SwatchColor("rail");
        var all = new List<(int X, int Y)>(pts);
        if (ed.HoverIn && !ed.PopupOpen && pts.Count > 0 && pts[^1] != ed.Hover) all.Add(ed.Hover);
        for (int k = 0; k + 1 < all.Count; k++)
            foreach (var (x, y) in MapDoc.LineCells(all[k].X, all[k].Y, all[k + 1].X, all[k + 1].Y))
                Raylib.DrawRectangle(x * TS, y * TS, TS, TS, Palette.Alpha(col, 170));
        for (int k = 0; k < pts.Count; k++)
            Raylib.DrawCircleV(new Vector2((pts[k].X + 0.5f) * TS, (pts[k].Y + 0.5f) * TS), Math.Max(TS * 0.3f, 5f / ed.Cam.Zoom), k == pts.Count - 1 ? Palette.Accent : Palette.Select);
        if (ed.HoverIn && !ed.PopupOpen && pts.Count == 0)
            Raylib.DrawRectangleLinesEx(new Rectangle(ed.Hover.X * TS, ed.Hover.Y * TS, TS, TS), 2 / ed.Cam.Zoom + 1, Palette.Accent);
    }

    static void DrawProp(string type, float x, float y, int alpha, bool scatter)
    {
        float rad = (PropDefs.Current.TryGet(type, out var d) ? d.Radius : 0.4f) * TS;
        var ctr = new Vector2(x * TS, y * TS);
        var col = PropColor(type);
        Raylib.DrawCircleV(ctr, rad, Palette.Alpha(col, alpha));
        Raylib.DrawCircleLinesV(ctr, rad, scatter ? new Color((byte)255, (byte)170, (byte)60, (byte)alpha) : new Color((byte)20, (byte)20, (byte)20, (byte)alpha));
    }

    /// <summary>배치 도구의 마우스 위 고스트 + 끌기 모양 미리보기 (확정은 마우스를 뗄 때).</summary>
    static void DrawPlaceGhost(Editor ed, float lw, int fs)
    {
        if (ed.IsTerrainTool || ed.PopupOpen) return;
        var a = ed.DragStart; var b = ed.DragCur;
        if (ed.Dragging && ed.DragButton == 0 && ed.Tool is Tool.Area or Tool.Zone)
        {
            var col = ed.Tool == Tool.Area ? Palette.Accent : Palette.Select;
            if (ed.AreaCircle)
            {
                float r = MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                DrawShape(null, new double[] { a.X, a.Y, MathF.Round(r) }, col, lw, 40);
            }
            else DrawShape(new double[] { Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1 }, null, col, lw, 40);
            return;
        }
        if (ed.Dragging && ed.Tool == Tool.Start && ed.DragStartIdx >= 0)
        {
            var s = ed.Doc.Starts[ed.DragStartIdx];
            var from = new Vector2((s[0] + 0.5f) * TS, (s[1] + 0.5f) * TS);
            var to = new Vector2((b.X + 0.5f) * TS, (b.Y + 0.5f) * TS);
            Raylib.DrawLineEx(from, to, lw, Palette.Alpha(Palette.Select, 150));
            Raylib.DrawCircleLinesV(to, TS * 0.7f, Palette.Accent);
            Raylib.DrawText((ed.DragStartIdx + 1).ToString(), (int)to.X - 4, (int)to.Y - 8, 18, Palette.Accent);
            return;
        }
        if (!ed.HoverIn) return;
        var h = ed.Hover;
        var c = new Vector2((h.X + 0.5f) * TS, (h.Y + 0.5f) * TS);
        switch (ed.Tool)
        {
            case Tool.Prop:
                if (Editor.PropTypes.Length > 0) DrawProp(Editor.PropTypes[Math.Clamp(ed.PropSel, 0, Editor.PropTypes.Length - 1)], h.X + 0.5f, h.Y + 0.5f, 150, false);
                break;
            case Tool.Site:
                if (Editor.SiteTypes.Length > 0)
                {
                    int size = Defs.Current.SiteKind(Editor.SiteTypes[Math.Clamp(ed.SiteSel, 0, Editor.SiteTypes.Length - 1)])?.Size ?? 3;
                    var rc = new Rectangle((h.X - size / 2) * TS, (h.Y - size / 2) * TS, size * TS, size * TS);
                    Raylib.DrawRectangleRec(rc, Palette.Alpha(Owner(ed.OwnerSel), 80));
                    Raylib.DrawRectangleLinesEx(rc, lw, Palette.Accent);
                }
                break;
            case Tool.Start:
                Raylib.DrawCircleLinesV(c, TS * 0.7f, Palette.Alpha(Palette.Accent, 200));
                break;
            case Tool.Unit:
                Raylib.DrawCircleV(c, TS * 0.4f, Palette.Alpha(Owner(Math.Max(1, ed.OwnerSel)), 150));
                break;
            default:
                Raylib.DrawRectangleLinesEx(new Rectangle(h.X * TS, h.Y * TS, TS, TS), lw, Palette.Accent);
                break;
        }
    }

    void DrawChrome(Editor ed, Vector2 mouse)
    {
        int w = ed.WinW, h = ed.WinH;
        Raylib.DrawRectangle(0, 0, w, Editor.TopH, Palette.TopBar);
        Raylib.DrawRectangle(0, Editor.TopH, Editor.LeftW, h - Editor.TopH, Palette.Panel);
        Raylib.DrawRectangle(w - Editor.RightW, Editor.TopH, Editor.RightW, h - Editor.TopH, Palette.Panel);
        Raylib.DrawRectangle(0, h - Editor.StatusH, w, Editor.StatusH, Palette.TopBar);
        Raylib.DrawLine(0, Editor.TopH, w, Editor.TopH, Palette.PanelBorder);
        var ir = ed.IssueRect;
        Raylib.DrawRectangle((int)ir.X, (int)ir.Y, (int)ir.Width, (int)ir.Height, Palette.PanelDark);
        Raylib.DrawLine((int)ir.X, (int)ir.Y, (int)(ir.X + ir.Width), (int)ir.Y, Palette.PanelBorder);

        foreach (var b in ed.Buttons)
        {
            if (b.Modal) continue;
            DrawButton(b, !ed.PopupOpen && Ui.Contains(b.Rect, mouse));
        }
        // 이름 / 변경 표시
        string title = $"{ed.SourceLabel}{(ed.Dirty ? " *" : "")}  {ed.Doc.Width}x{ed.Doc.Height}";
        Ui.Text(title, ed.TitleX, 11, 16, ed.Dirty ? Palette.Warning : Palette.Text);
        // 왼쪽 패널 라벨
        Ui.Text($"크기 {ed.BrushSize}", 10, ed.SizeLabelY, 16, ed.IsTerrainTool ? Palette.Text : Palette.TextDim);
        var dec = ed.ButtonById("size_dec"); var inc = ed.ButtonById("size_inc");
        if (dec != null && inc != null) Ui.TextCentered(ed.BrushSize.ToString(), new Rectangle(dec.Rect.X + dec.Rect.Width, dec.Rect.Y, inc.Rect.X - dec.Rect.X - dec.Rect.Width, dec.Rect.Height), 16, Palette.Accent);
        // 패널 글자 (Layout 이 만든 목록)
        foreach (var l in ed.Labels)
        {
            Color col = l.Kind switch { 1 => Palette.TextDim, 2 => Palette.Warning, 3 => Palette.Accent, 4 => ErrCol, _ => Palette.Text };
            if (l.Centered) Ui.TextCentered(l.Text, new Rectangle(l.X, l.Y - 6, l.W, 28), 16, col);
            else Ui.Text(l.Text, l.X, l.Y, 16, col);
        }
        // 상태줄
        string cursor = ed.HoverIn ? $"({ed.Hover.X},{ed.Hover.Y}) {ed.TerrainUnder(ed.Hover.X, ed.Hover.Y)}" : "";
        Ui.Text(cursor, 8, h - Editor.StatusH + 4, 16, Palette.Text);
        Raylib.BeginScissorMode(190, h - Editor.StatusH, Math.Max(0, w - 190 - 90), Editor.StatusH);
        Ui.Text(ed.Status, 200, h - Editor.StatusH + 4, 16, Palette.TextDim);
        Raylib.EndScissorMode();
        Ui.Text($"배율 {ed.Cam.Zoom:0.00}", w - 90, h - Editor.StatusH + 4, 16, Palette.TextDim);

        if (ed.PopupOpen)
        {
            Raylib.DrawRectangle(0, Editor.TopH, w, h - Editor.TopH, new Color(0, 0, 0, 120));
            var pr = ed.PopupRect;
            Ui.Panel(pr, Palette.Panel, Palette.PanelBorder);
            Ui.Text("열기", pr.X + 12, pr.Y + 8, 16, Palette.Accent);
            foreach (var b in ed.Buttons) if (b.Modal) DrawButton(b, Ui.Contains(b.Rect, mouse));
        }
    }

    static void DrawButton(Button b, bool hover)
    {
        if (b.Row)
        {
            bool act = b.Active();
            if (b.Label.Length == 0) return;
            Raylib.DrawRectangleRec(b.Rect, act ? Palette.BtnActive : hover ? Palette.BtnHover : Palette.Btn);
            Raylib.BeginScissorMode((int)b.Rect.X, (int)b.Rect.Y, (int)b.Rect.Width, (int)b.Rect.Height);
            Ui.Text(b.Label, b.Rect.X + 6, b.Rect.Y + 4, 16, b.RowColor ?? Palette.Text);
            Raylib.EndScissorMode();
            return;
        }
        Ui.Button(b.Rect, b.Swatch == null ? b.Label : "", hover, b.Active());
        if (b.Swatch is Color c)
        {
            Raylib.DrawRectangle((int)b.Rect.X + 5, (int)b.Rect.Y + 4, 22, (int)b.Rect.Height - 8, c);
            Raylib.DrawRectangleLines((int)b.Rect.X + 5, (int)b.Rect.Y + 4, 22, (int)b.Rect.Height - 8, Palette.PanelBorder);
            Ui.Text(b.Label, b.Rect.X + 34, b.Rect.Y + 4, 16, Palette.Text);
        }
    }
}
