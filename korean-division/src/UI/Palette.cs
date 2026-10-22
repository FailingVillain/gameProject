using Raylib_cs;

namespace KoreanDivision.UI;

public static class Palette
{
    // ---- 종이 UI (T38d): 밝은 종이 + 어두운 잉크. 글자 · 패널 색은 이 의미 토큰만 거친다 (호출부에서 직접 new Color 금지).
    // 책상(바깥 배경) = 어두운 나무색; 패널 = 종이. 어두운 면 위에 직접 쓰는 글자는 ...OnDark 토큰.
    /// <summary>책상 · 화면 바깥 배경 (어두움 — 그 위 글자는 TextOnDark 계열).</summary>
    public static readonly Color Void = new(26, 20, 15, 255);
    /// <summary>시작 화면 배경 위쪽 색 (책상 어둠 -> Void 로 내려가는 그라데이션).</summary>
    public static readonly Color DeskTop = new(22, 26, 34, 255);
    public static readonly Color TopBar = new(214, 197, 160, 255);
    public static readonly Color HudBg = new(220, 204, 168, 255);
    public static readonly Color HudTrim = new(112, 84, 50, 255);
    /// <summary>종이 패널 바탕.</summary>
    public static readonly Color Panel = new(232, 217, 182, 255);
    /// <summary>안쪽 칸 · 반투명 상자 바탕 (패널보다 한 단계 짙은 종이).</summary>
    public static readonly Color PanelDark = new(218, 201, 164, 255);
    public static readonly Color PanelBorder = new(124, 100, 66, 255);

    /// <summary>잉크 (종이 위 본문).</summary>
    public static readonly Color Text = new(42, 33, 26, 255);
    public static readonly Color TextDim = new(78, 64, 48, 255);
    /// <summary>제목 · 강조 (붉은 도장 잉크).</summary>
    public static readonly Color Accent = new(120, 28, 22, 255);
    public static readonly Color Warning = new(128, 48, 0, 255);

    /// <summary>어두운 면(책상 · 월드 위 · 어두운 배경) 위에 쓰는 글자 색.</summary>
    public static readonly Color TextOnDark = new(236, 229, 212, 255);
    public static readonly Color TextDimOnDark = new(176, 164, 140, 255);
    public static readonly Color AccentOnDark = new(240, 200, 90, 255);
    public static readonly Color WarningOnDark = new(255, 190, 90, 255);

    /// <summary>종이 위 글자로 쓰는 금색 · 초록 (밝은 종이에서 읽히는 어두운 변형; Gold · Select 는 월드 · 어두운 면용).</summary>
    public static readonly Color GoldInk = new(92, 64, 0, 255);
    public static readonly Color SelectInk = new(16, 80, 32, 255);
    public static readonly Color GoodInk = new(16, 80, 32, 255);
    public static readonly Color MidInk = new(92, 64, 0, 255);
    public static readonly Color BadInk = new(136, 24, 18, 255);

    public static readonly Color Btn = new(208, 190, 150, 255);
    public static readonly Color BtnHover = new(224, 207, 168, 255);
    public static readonly Color BtnActive = new(158, 190, 138, 255);

    public static readonly Color Select = new(80, 230, 110, 255);
    /// <summary>교전 중 (경계 상태) 선택 표시 — 녹색 선택 선 대신 같은 굵기로.</summary>
    public static readonly Color SelectCombat = new(255, 170, 50, 255);
    /// <summary>머리띠 (팀 층) 를 숨길 때 팀 픽셀에 칠하는 천 색 (군복이 헷갈리지 않는 교전 — UnitRenderer.HeadbandOn).</summary>
    public static readonly Color BandHidden = new(92, 86, 66, 255);
    public static readonly Color SelectFill = new(80, 230, 110, 40);
    public static readonly Color HpGood = new(70, 210, 90, 255);
    public static readonly Color HpMid = new(230, 200, 70, 255);
    public static readonly Color HpLow = new(220, 70, 60, 255);
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

    /// <summary>WCAG 상대 휘도 대비비 (T38d 대비 시험용).</summary>
    public static double Contrast(Color a, Color b)
    {
        static double Lin(byte v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        static double L(Color c) => 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
        double la = L(a), lb = L(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static Color Shade(Color c, int delta) =>
        new(Math.Clamp(c.R + delta, 0, 255), Math.Clamp(c.G + delta, 0, 255), Math.Clamp(c.B + delta, 0, 255), (int)c.A);

    public static Color Alpha(Color c, int a) => new(c.R, c.G, c.B, (byte)Math.Clamp(a, 0, 255));

    public static Color HpColor(float ratio) => ratio > 0.6f ? HpGood : ratio > 0.3f ? HpMid : HpLow;
}
