using KoreanDivision.UI;
using Raylib_cs;

namespace KoreanDivision.Tests;

/// <summary>T38d 종이 UI: 글자 토큰 x 종이 면 토큰 대비비 >= 4.5:1, 어두운 면 위 글자 토큰, 그리고 종이 면이 실제로 밝은지.</summary>
public static class PaperUiSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[T38d 종이 UI 대비]");
        var faces = new (string, Color)[]
        {
            ("Panel", Palette.Panel), ("PanelDark", Palette.PanelDark), ("HudBg", Palette.HudBg), ("TopBar", Palette.TopBar),
            ("Btn", Palette.Btn), ("BtnHover", Palette.BtnHover),
        };
        var inks = new (string, Color)[]
        {
            ("Text", Palette.Text), ("TextDim", Palette.TextDim), ("Accent", Palette.Accent), ("Warning", Palette.Warning),
            ("GoldInk", Palette.GoldInk), ("GoodInk", Palette.GoodInk), ("MidInk", Palette.MidInk), ("BadInk", Palette.BadInk), ("SelectInk", Palette.SelectInk),
        };
        int bad = 0; double min = 99;
        foreach (var (fn, f) in faces)
            foreach (var (tn, t) in inks)
            {
                double c = Palette.Contrast(t, f);
                min = Math.Min(min, c);
                if (c < 4.5) { bad++; Console.WriteLine($"  낮음: {tn} on {fn} = {c:0.00}"); }
            }
        check(bad == 0, $"잉크 {inks.Length} x 종이 면 {faces.Length} 대비비 >= 4.5 (최소 {min:0.00})");
        check(Palette.Contrast(Palette.Text, Palette.BtnActive) >= 4.5, "Text on BtnActive >= 4.5");
        bool dk = true;
        foreach (var t in new[] { Palette.TextOnDark, Palette.TextDimOnDark, Palette.AccentOnDark, Palette.WarningOnDark })
            dk &= Palette.Contrast(t, Palette.Void) >= 4.5 && Palette.Contrast(t, Palette.DeskTop) >= 4.5;
        check(dk, "어두운 면 위 글자 토큰 >= 4.5 (Void · DeskTop)");
        check(Palette.Contrast(Palette.Panel, Palette.Void) >= 6, "종이 패널이 책상(Void)보다 충분히 밝음");
    }
}
