using System.Numerics;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>
/// 게임 내 도움말 오버레이 (T38b, F1). 내용은 <see cref="Bindings.All"/> 하나뿐 (복붙 문구 없음).
/// 화면 전용 — 시뮬 · 저장 무관. 일시정지 · 게임 종료 화면에서도 열린다 (Game.HandleGlobalKeys). 열려 있는 동안 게임 입력은 막힌다.
/// 해상도가 낮으면 한 단, 충분하면 두 단. 넘치면 스크롤 (휠 · ↑↓ · PgUp/PgDn · Home/End).
/// </summary>
public sealed class HelpOverlay
{
    const float RowH = 24, Pad = 16;
    public bool IsOpen { get; private set; }
    float scroll;
    Rectangle panel, body;
    (string Key, string Text)[] rows = Array.Empty<(string, string)>();
    int cols = 1;
    float keyW;

    public void Toggle() { IsOpen = !IsOpen; scroll = 0; }
    public void Close() => IsOpen = false;

    float ContentH => (float)Math.Ceiling(rows.Length / (double)cols) * RowH;
    float MaxScroll => MathF.Max(0, ContentH - body.Height);

    void Layout(int sw, int sh)
    {
        float w = MathF.Min(sw - 24, 1100), h = sh - 24;
        panel = new Rectangle((sw - w) / 2, 12, w, h);
        body = new Rectangle(panel.X + Pad, panel.Y + 56, panel.Width - Pad * 2 - 12, panel.Height - 56 - 34);
        rows = Bindings.All().ToArray();
        keyW = 0;
        foreach (var r in rows) keyW = MathF.Max(keyW, Ui.Measure(r.Key, 16).X);
        keyW += 14;
        float textW = 0;
        foreach (var r in rows) textW = MathF.Max(textW, Ui.Measure(r.Text, 16).X);
        cols = body.Width >= 2 * (keyW + textW + 24) ? 2 : 1;
    }

    /// <summary>열려 있을 때 매 프레임 호출 (입력 처리). Esc 는 여기서 닫는다.</summary>
    public void Update(Vector2 mouse)
    {
        if (!IsOpen) return;
        Layout(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        if (Input.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }
        float step = RowH * 3;
        scroll -= Input.GetMouseWheelMove() * step;
        if (Input.IsKeyPressed(KeyboardKey.Up) || Input.IsKeyPressedRepeat(KeyboardKey.Up)) scroll -= RowH;
        if (Input.IsKeyPressed(KeyboardKey.Down) || Input.IsKeyPressedRepeat(KeyboardKey.Down)) scroll += RowH;
        if (Input.IsKeyPressed(KeyboardKey.PageUp)) scroll -= body.Height - RowH;
        if (Input.IsKeyPressed(KeyboardKey.PageDown)) scroll += body.Height - RowH;
        if (Input.IsKeyPressed(KeyboardKey.Home)) scroll = 0;
        if (Input.IsKeyPressed(KeyboardKey.End)) scroll = MaxScroll;
        scroll = Math.Clamp(scroll, 0, MaxScroll);
        if (Input.IsMouseButtonPressed(MouseButton.Left) && (Ui.Contains(CloseRect, mouse) || !Ui.Contains(panel, mouse))) Close();
    }

    Rectangle CloseRect => new(panel.X + panel.Width - 96, panel.Y + 10, 80, 32);

    public void Draw(Vector2 mouse)
    {
        if (!IsOpen) return;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        Layout(sw, sh);
        scroll = Math.Clamp(scroll, 0, MaxScroll);
        Raylib.DrawRectangle(0, 0, sw, sh, Palette.Alpha(Palette.Void, 190));
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 245), Palette.Accent);
        Ui.Text(S.HelpTitle, panel.X + Pad, panel.Y + 14, 24, Palette.Accent);
        Ui.Button(CloseRect, S.HelpClose, Ui.Contains(CloseRect, mouse), false);
        Raylib.DrawLineEx(new Vector2(panel.X + Pad, panel.Y + 50), new Vector2(panel.X + panel.Width - Pad, panel.Y + 50), 1, Palette.PanelBorder);

        Raylib.BeginScissorMode((int)body.X, (int)body.Y, (int)body.Width, (int)body.Height);
        int perCol = (int)Math.Ceiling(rows.Length / (double)cols);
        float colW = body.Width / cols;
        for (int i = 0; i < rows.Length; i++)
        {
            int c = i / perCol, r = i % perCol;
            float x = body.X + c * colW, y = body.Y + r * RowH - scroll;
            if (y + RowH < body.Y || y > body.Y + body.Height) continue;
            if (r % 2 == 0) Raylib.DrawRectangleRec(new Rectangle(x, y, colW - 8, RowH), Palette.Alpha(Palette.Panel, 90));
            Ui.Text(rows[i].Key, x + 6, y + 3, 16, Palette.Accent);
            Ui.Text(rows[i].Text, x + 6 + keyW, y + 3, 16, Palette.Text);
        }
        Raylib.EndScissorMode();

        if (MaxScroll > 0)   // 스크롤 막대
        {
            var track = new Rectangle(panel.X + panel.Width - Pad - 8, body.Y, 8, body.Height);
            Raylib.DrawRectangleRec(track, Palette.Alpha(Palette.Panel, 160));
            float th = MathF.Max(24, track.Height * body.Height / ContentH);
            float ty = track.Y + (track.Height - th) * (scroll / MaxScroll);
            Raylib.DrawRectangleRec(new Rectangle(track.X, ty, track.Width, th), Palette.HudTrim);
        }
        Ui.Text(S.HelpHint, panel.X + Pad, panel.Y + panel.Height - 26, 16, Palette.TextDim);
    }
}
