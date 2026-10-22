using System.Numerics;
using KoreanDivision;
using KoreanDivision.Core;
using KoreanDivision.Render;
using KoreanDivision.UI;
using MapEditor.Model;
using Raylib_cs;

namespace MapEditor.App;

/// <summary>편집기 화면 그리기. 지도는 게임의 MapRenderer 그대로 (같은 색 · 무늬), 그 위에 편집 표시만 얹는다.</summary>
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
        if (i < MapDoc.TerrainNames.Length) return Terrains.Get((TerrainType)i).Color;
        return i == 11 ? new Color(230, 220, 120, 255) : new Color(110, 140, 170, 255);
    }

    /// <summary>드래그 중인 획이 확정되면 칠해질 칸 (미리보기). 확정 코드와 같은 함수로 만든다.</summary>
    public static IEnumerable<(int X, int Y)> PreviewCells(Editor ed)
    {
        if (!ed.Dragging) yield break;
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

    void DrawOverlays(Editor ed)
    {
        var (vmin, vmax) = ed.Cam.VisibleWorld();
        int x0 = Math.Max(0, (int)vmin.X), y0 = Math.Max(0, (int)vmin.Y), x1 = Math.Min(ed.Doc.Width - 1, (int)vmax.X + 1), y1 = Math.Min(ed.Doc.Height - 1, (int)vmax.Y + 1);
        if (ed.ShowHigh)
        {
            var g = ed.Doc.Raster();
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (g.High[y * g.Width + x]) Raylib.DrawRectangleLinesEx(new Rectangle(x * TS + 1, y * TS + 1, TS - 2, TS - 2), 2, new Color(255, 240, 120, 170));
        }
        for (int i = 0; i < ed.Doc.Starts.Count; i++)   // 시작 위치 (T33c 에서 편집; 여기서는 표시만)
        {
            var s = ed.Doc.Starts[i];
            var c = new Vector2((s[0] + 0.5f) * TS, (s[1] + 0.5f) * TS);
            Raylib.DrawCircleLinesV(c, TS * 0.7f, Palette.Select);
            Raylib.DrawText((i + 1).ToString(), (int)c.X - 4, (int)c.Y - 8, 18, Palette.Select);
        }
        var cells = PreviewCells(ed).ToList();
        if (cells.Count > 0)
        {
            Color col = TerrainColor(ed.Tool == Tool.Eraser || ed.DragButton == 1 ? ed.Doc.Base : ed.CurrentTerrain);
            if (cells.Count <= 30000)
                foreach (var (x, y) in cells) Raylib.DrawRectangle(x * TS, y * TS, TS, TS, Palette.Alpha(col, 150));
            else { var a = ed.DragStart; Raylib.DrawCircleLinesV(new Vector2((a.X + 0.5f) * TS, (a.Y + 0.5f) * TS), 8, col); }
        }
        if (ed.HoverIn && !ed.PopupOpen)
        {
            int n = ed.Tool is Tool.Brush or Tool.Eraser or Tool.Line ? ed.BrushSize : 1;
            int lo = -((n - 1) / 2);
            Raylib.DrawRectangleLinesEx(new Rectangle((ed.Hover.X + lo) * TS, (ed.Hover.Y + lo) * TS, n * TS, n * TS), 2 / ed.Cam.Zoom + 1, Palette.Accent);
        }
    }

    void DrawChrome(Editor ed, Vector2 mouse)
    {
        int w = ed.WinW, h = ed.WinH;
        Raylib.DrawRectangle(0, 0, w, Editor.TopH, Palette.TopBar);
        Raylib.DrawRectangle(0, 0, Editor.LeftW, h, Palette.Panel);
        Raylib.DrawRectangle(0, Editor.TopH, Editor.LeftW, h - Editor.TopH, Palette.Panel);
        Raylib.DrawRectangle(w - Editor.RightW, Editor.TopH, Editor.RightW, h - Editor.TopH, Palette.Panel);
        Raylib.DrawRectangle(0, h - Editor.StatusH, w, Editor.StatusH, Palette.TopBar);
        Raylib.DrawRectangle(0, 0, w, Editor.TopH, Palette.TopBar);
        Raylib.DrawLine(0, Editor.TopH, w, Editor.TopH, Palette.PanelBorder);

        var popupBtns = new HashSet<string>();
        foreach (var b in ed.Buttons)
        {
            if (b.Id.StartsWith("item_") || b.Id == "popup_close") { popupBtns.Add(b.Id); continue; }
            DrawButton(b, !ed.PopupOpen && Ui.Contains(b.Rect, mouse));
        }
        // 이름 / 변경 표시
        string title = $"{ed.SourceLabel}{(ed.Dirty ? " *" : "")}  {ed.Doc.Width}x{ed.Doc.Height}";
        var fit = ed.ButtonById("fit");
        Ui.Text(title, (fit?.Rect.X + fit?.Rect.Width ?? 600) + 16, 11, 16, ed.Dirty ? Palette.Warning : Palette.Text);
        // 왼쪽 패널 라벨
        float sy = Editor.TopH + 10 + 6 * 38 + 8;
        Ui.Text($"크기 {ed.BrushSize}", 10, sy, 16, Palette.Text);
        var dec = ed.ButtonById("size_dec"); var inc = ed.ButtonById("size_inc");
        if (dec != null && inc != null) Ui.TextCentered(ed.BrushSize.ToString(), new Rectangle(dec.Rect.X + dec.Rect.Width, dec.Rect.Y, inc.Rect.X - dec.Rect.X - dec.Rect.Width, dec.Rect.Height), 16, Palette.Accent);
        // 오른쪽 패널 정보
        float iy = Editor.TopH + 10 + 13 * 30 + 6, ix = w - Editor.RightW + 10;
        Raylib.DrawLine((int)ix - 2, (int)iy - 3, w - 8, (int)iy - 3, Palette.PanelBorder);
        Ui.Text($"{ed.Doc.Width}x{ed.Doc.Height}  기본 {ed.Doc.Base}", ix, iy, 16, Palette.TextDim);
        Ui.Text($"연산 {ed.Doc.Paint.Count}  되돌리기 {ed.Doc.UndoDepth}", ix, iy + 20, 16, Palette.TextDim);
        Ui.Text($"해시 {ed.LastHash}", ix, iy + 40, 16, Palette.TextDim);
        Ui.Text($"시작 {ed.Doc.Starts.Count}  {(ed.Dirty ? "변경됨" : "저장됨")}", ix, iy + 60, 16, ed.Dirty ? Palette.Warning : Palette.TextDim);
        // 상태줄
        string cursor = ed.HoverIn ? $"({ed.Hover.X},{ed.Hover.Y}) {ed.TerrainUnder(ed.Hover.X, ed.Hover.Y)}" : "";
        Ui.Text(cursor, 8, h - Editor.StatusH + 4, 16, Palette.Text);
        Ui.Text(ed.Status, 200, h - Editor.StatusH + 4, 16, Palette.TextDim);
        Ui.Text($"배율 {ed.Cam.Zoom:0.00}", w - 90, h - Editor.StatusH + 4, 16, Palette.TextDim);

        if (ed.PopupOpen)
        {
            Raylib.DrawRectangle(0, Editor.TopH, w, h - Editor.TopH, new Color(0, 0, 0, 120));
            var pr = ed.PopupRect;
            Ui.Panel(pr, Palette.Panel, Palette.PanelBorder);
            Ui.Text("지도 열기", pr.X + 12, pr.Y + 10, 16, Palette.Accent);
            foreach (var b in ed.Buttons) if (popupBtns.Contains(b.Id)) DrawButton(b, Ui.Contains(b.Rect, mouse));
        }
    }

    static void DrawButton(Button b, bool hover)
    {
        Ui.Button(b.Rect, b.Swatch == null ? b.Label : "", hover, b.Active());
        if (b.Swatch is Color c)
        {
            Raylib.DrawRectangle((int)b.Rect.X + 5, (int)b.Rect.Y + 4, 22, (int)b.Rect.Height - 8, c);
            Raylib.DrawRectangleLines((int)b.Rect.X + 5, (int)b.Rect.Y + 4, 22, (int)b.Rect.Height - 8, Palette.PanelBorder);
            Ui.Text(b.Label, b.Rect.X + 34, b.Rect.Y + 4, 16, Palette.Text);
        }
    }
}
