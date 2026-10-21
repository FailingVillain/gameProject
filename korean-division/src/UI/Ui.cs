using System.Numerics;
using System.Reflection;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>폰트 · 텍스트 · 패널 그리기 유틸.</summary>
public static class Ui
{
    static Font small, large;
    static bool customFont;

    /// <summary>
    /// 폰트 탐색 순서: assets/fonts/font.ttf (배포용 동봉 폰트) → Windows 맑은 고딕 → raylib 기본 폰트.
    /// 아틀라스에는 ASCII + Strings(S) 클래스 + extraText(데이터 파일의 이름 등)의 글자만 포함한다.
    /// </summary>
    public static void LoadFonts(IEnumerable<string>? extraText = null)
    {
        int[] codepoints = CollectCodepoints(extraText ?? Array.Empty<string>());
        string? path = FindFont();
        if (path != null)
        {
            small = Raylib.LoadFontEx(path, 16, codepoints, codepoints.Length);
            large = Raylib.LoadFontEx(path, 26, codepoints, codepoints.Length);
            Raylib.SetTextureFilter(small.Texture, TextureFilter.Bilinear);
            Raylib.SetTextureFilter(large.Texture, TextureFilter.Bilinear);
            customFont = true;
        }
        else small = large = Raylib.GetFontDefault();
    }

    static string? FindFont()
    {
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "assets", "fonts", "font.ttf"),
            Path.Combine(fonts, "malgun.ttf"),
            Path.Combine(fonts, "NanumGothic.ttf"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    static int[] CollectCodepoints(IEnumerable<string> extraText)
    {
        var set = new SortedSet<int>();
        for (int c = 32; c < 127; c++) set.Add(c);
        foreach (var s in extraText)
            foreach (char ch in s) set.Add(ch);
        foreach (var type in new[] { typeof(S), typeof(KoreanDivision.Core.SnapMsg) })   // SnapMsg = 저장 파일 오류 메시지 (불러오기 목록 · 알림에 그대로 표시)
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.GetRawConstantValue() is string s)
                    foreach (char ch in s) set.Add(ch);
        return set.ToArray();
    }

    public static void Unload()
    {
        if (!customFont) return;
        Raylib.UnloadFont(small);
        Raylib.UnloadFont(large);
    }

    static Font FontFor(float size) => size >= 20 ? large : small;

    public static void Text(string text, float x, float y, float size, Color color) =>
        Raylib.DrawTextEx(FontFor(size), text, new Vector2(MathF.Round(x), MathF.Round(y)), size, customFont ? 0 : 1, color);

    public static Vector2 Measure(string text, float size) =>
        Raylib.MeasureTextEx(FontFor(size), text, size, customFont ? 0 : 1);

    public static void TextCentered(string text, Rectangle r, float size, Color color)
    {
        Vector2 m = Measure(text, size);
        Text(text, r.X + (r.Width - m.X) / 2, r.Y + (r.Height - m.Y) / 2, size, color);
    }

    public static void Panel(Rectangle r, Color fill, Color border)
    {
        Raylib.DrawRectangleRec(r, fill);
        Raylib.DrawRectangleLinesEx(r, 1, border);
    }

    public static void Button(Rectangle r, string label, bool hover, bool active)
    {
        Color fill = active ? Palette.BtnActive : hover ? Palette.BtnHover : Palette.Btn;
        Raylib.DrawRectangleRounded(r, 0.2f, 4, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, Palette.PanelBorder);
        TextCentered(label, r, 16, Palette.Text);
    }

    public static bool Contains(Rectangle r, Vector2 p) => Raylib.CheckCollisionPointRec(p, r);
}
