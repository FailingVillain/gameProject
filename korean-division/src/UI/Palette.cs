using Raylib_cs;

namespace KoreanDivision.UI;

public static class Palette
{
    public static readonly Color Void = new(14, 12, 10, 255);
    public static readonly Color TopBar = new(40, 33, 26, 255);
    public static readonly Color HudBg = new(46, 38, 30, 255);
    public static readonly Color HudTrim = new(150, 126, 84, 255);
    public static readonly Color Panel = new(58, 49, 38, 255);
    public static readonly Color PanelDark = new(30, 25, 20, 255);
    public static readonly Color PanelBorder = new(116, 98, 70, 255);

    public static readonly Color Text = new(236, 229, 212, 255);
    public static readonly Color TextDim = new(176, 164, 140, 255);
    public static readonly Color Accent = new(240, 200, 90, 255);
    public static readonly Color Warning = new(255, 190, 90, 255);

    public static readonly Color Btn = new(72, 60, 45, 255);
    public static readonly Color BtnHover = new(100, 83, 58, 255);
    public static readonly Color BtnActive = new(70, 106, 64, 255);

    public static readonly Color Select = new(80, 230, 110, 255);
    /// <summary>교전 중 (경계 상태) 선택 표시 — 녹색 선택 선 대신 같은 굵기로.</summary>
    public static readonly Color SelectCombat = new(255, 170, 50, 255);
    /// <summary>머리띠 (팀 층) 를 숨길 때 팀 픽셀에 칠하는 천 색 (군복이 헷갈리지 않는 교전 — UnitRenderer.HeadbandOn).</summary>
    public static readonly Color BandHidden = new(92, 86, 66, 255);
    public static readonly Color SelectFill = new(80, 230, 110, 40);
    public static readonly Color HpGood = new(70, 210, 90, 255);
    public static readonly Color HpMid = new(230, 200, 70, 255);
    public static readonly Color HpLow = new(220, 70, 60, 255);
    public static readonly Color AmmoBar = new(225, 200, 90, 255);
    public static readonly Color CargoBar = new(90, 160, 230, 255);
    public static readonly Color BarLow = new(230, 90, 60, 255);
    public static readonly Color HpBg = new(16, 16, 18, 255);
    public static readonly Color MarkerMove = Select;
    public static readonly Color MarkerPatrol = Accent;
    public static readonly Color Grid = new(0, 0, 0, 55);

    public static readonly Color Neutral = new(170, 170, 160, 255);
    public static readonly Color Gold = new(236, 196, 72, 255);
    public static readonly Color Tracer = new(255, 236, 160, 255);
    public static readonly Color Ghost = new(150, 150, 150, 150);

    /// <summary>세력 색 (최대 24 세력). 1 = 파랑 · 2 = 빨강 · 이후 색상환에서 서로 멀게.</summary>
    static readonly Color[] FactionColors =
    {
        Neutral,
        new(70, 140, 235, 255), new(220, 70, 60, 255), new(80, 200, 110, 255), new(235, 200, 60, 255),
        new(170, 90, 220, 255), new(60, 210, 210, 255), new(240, 140, 50, 255), new(235, 110, 180, 255),
        new(150, 200, 60, 255), new(120, 120, 240, 255), new(200, 150, 100, 255), new(240, 240, 240, 255),
        new(40, 90, 170, 255), new(150, 40, 40, 255), new(40, 130, 70, 255), new(170, 140, 30, 255),
        new(110, 50, 150, 255), new(30, 140, 140, 255), new(170, 90, 30, 255), new(170, 60, 120, 255),
        new(100, 140, 40, 255), new(80, 80, 160, 255), new(130, 100, 70, 255), new(120, 120, 120, 255),
    };

    /// <summary>지도 파일이 정한 세력 색 (index = 세력 번호, 없으면 기본 팔레트). 게임을 만들 때마다 지도에 맞춰 다시 정한다.</summary>
    static Color?[] mapColors = Array.Empty<Color?>();

    public static void UseMapColors(IReadOnlyList<KoreanDivision.Core.FactionDef> defs)
    {
        mapColors = new Color?[defs.Count + 1];
        for (int i = 0; i < defs.Count; i++)
            if (defs[i].Color is { Length: 3 } c) mapColors[i + 1] = new Color(c[0], c[1], c[2], 255);
    }

    public static Color Player(int owner) =>
        (uint)owner < mapColors.Length && mapColors[owner] is Color mc ? mc
        : FactionColors[(uint)owner < FactionColors.Length ? owner : owner % (FactionColors.Length - 1) + 1];

    public static Color Shade(Color c, int delta) =>
        new(Math.Clamp(c.R + delta, 0, 255), Math.Clamp(c.G + delta, 0, 255), Math.Clamp(c.B + delta, 0, 255), (int)c.A);

    public static Color Alpha(Color c, int a) => new(c.R, c.G, c.B, (byte)Math.Clamp(a, 0, 255));

    public static Color HpColor(float ratio) => ratio > 0.6f ? HpGood : ratio > 0.3f ? HpMid : HpLow;
}
