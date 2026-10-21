using System.Numerics;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>
/// 명령어 입력창 (Enter 로 열기). 한 줄 입력 → Game.RunConsoleCommand 로 넘긴다.
/// 입력은 영문 · 숫자 · 기호만 (raylib 은 한글 조합 미리보기를 지원하지 않음 — 한글 채팅은 추후).
/// 위 · 아래 화살표로 이전 명령, Esc 로 닫기. 결과 메시지는 화면 아래쪽에 잠시 표시.
/// </summary>
public sealed class CommandConsole
{
    const int MaxLength = 120, MaxLog = 8;
    public bool IsOpen { get; private set; }
    string text = "";
    readonly List<string> history = new();
    int historyIndex = -1;
    readonly List<(string Text, double Time, Color Color)> log = new();
    double openedAt;

    public void Open()
    {
        IsOpen = true;
        text = "";
        historyIndex = -1;
        openedAt = Raylib.GetTime();
        while (Input.GetCharPressed() != 0) { }   // 여는 순간 눌린 글자는 버림
    }

    public void Close() => IsOpen = false;

    public void Print(string line, Color? color = null)
    {
        log.Add((line, Raylib.GetTime(), color ?? Palette.Text));
        if (log.Count > MaxLog) log.RemoveAt(0);
    }

    /// <summary>열려 있을 때 매 프레임 호출. 입력을 마치면 그 줄을 돌려준다 (없으면 null).</summary>
    public string? Update()
    {
        if (!IsOpen) return null;
        for (int c = Input.GetCharPressed(); c != 0; c = Input.GetCharPressed())
            if (c >= 32 && c < 127 && text.Length < MaxLength) text += (char)c;
        if ((Input.IsKeyPressed(KeyboardKey.Backspace) || Input.IsKeyPressedRepeat(KeyboardKey.Backspace)) && text.Length > 0)
            text = text[..^1];
        if (Input.IsKeyPressed(KeyboardKey.Up) && history.Count > 0)
        {
            historyIndex = historyIndex < 0 ? history.Count - 1 : Math.Max(0, historyIndex - 1);
            text = history[historyIndex];
        }
        if (Input.IsKeyPressed(KeyboardKey.Down) && historyIndex >= 0)
        {
            historyIndex++;
            text = historyIndex < history.Count ? history[historyIndex] : "";
            if (historyIndex >= history.Count) historyIndex = -1;
        }
        if (Input.IsKeyPressed(KeyboardKey.Escape)) { Close(); return null; }
        if (Input.IsKeyPressed(KeyboardKey.Enter) || Input.IsKeyPressed(KeyboardKey.KpEnter))
        {
            if (Raylib.GetTime() - openedAt < 0.05) return null;   // 연 바로 그 Enter
            Close();
            string line = text.Trim();
            if (line.Length == 0) return null;
            if (history.Count == 0 || history[^1] != line) history.Add(line);
            Print("> " + line, Palette.TextDim);
            return line;
        }
        return null;
    }

    public void Draw(Rectangle vp)
    {
        double now = Raylib.GetTime();
        float y = vp.Y + vp.Height - 44;
        // 최근 결과 (열려 있으면 전부, 닫혀 있으면 8초 이내)
        var shown = log.Where(l => IsOpen || now - l.Time < 8).ToList();
        float ly = y - shown.Count * 22 - 4;
        foreach (var (line, time, color) in shown)
        {
            int alpha = IsOpen ? 255 : (int)Math.Clamp(255 * (8 - (now - time)) / 2, 0, 255);
            Vector2 m = Ui.Measure(line, 16);
            Raylib.DrawRectangleRec(new Rectangle(vp.X + 8, ly, m.X + 16, 20), Palette.Alpha(Palette.PanelDark, alpha * 200 / 255));
            Ui.Text(line, vp.X + 16, ly + 2, 16, Palette.Alpha(color, alpha));
            ly += 22;
        }
        if (!IsOpen) return;
        var box = new Rectangle(vp.X + 8, y, MathF.Min(640, vp.Width - 16), 32);
        Raylib.DrawRectangleRec(box, Palette.Alpha(Palette.PanelDark, 235));
        Raylib.DrawRectangleLinesEx(box, 1, Palette.Accent);
        string caret = (int)(now * 2) % 2 == 0 ? "_" : " ";
        Ui.Text($"> {text}{caret}", box.X + 8, box.Y + 7, 16, Palette.Text);
        Ui.Text(S.ConsoleHint, box.X + box.Width + 10, box.Y + 7, 16, Palette.TextDim);
    }
}
