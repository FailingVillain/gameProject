using System.Numerics;
using System.Reflection;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>폰트 · 텍스트 · 패널 그리기 유틸.</summary>
public static class Ui
{
    /// <summary>글꼴 역할 (T38k): 제목 = 나눔명조 Bold, 본문 = 나눔고딕, 숫자 = 나눔고딕코딩 (고정폭). 호출부 기본값은 본문.</summary>
    public enum FontRole { Body, Heading, Numeric }

    // 번들 글꼴 (assets/fonts/<가족>/<파일>, OFL). 역할 순서 = FontRole 순서.
    static readonly string[] BundledFile =
    {
        "nanumgothic/NanumGothic-Regular.ttf",
        "nanummyeongjo/NanumMyeongjo-Bold.ttf",
        "nanumgothiccoding/NanumGothicCoding-Regular.ttf",
    };
    // 역할별 아틀라스 크기 단계. 요청 크기는 가장 가까운 (비율) 단계로 그려진다. 처음 쓸 때 LoadFontEx (지연 로드).
    static readonly int[][] Tiers =
    {
        new[] { 16, 26 },
        new[] { 26, 40, 72 },
        new[] { 16, 22 },
    };
    // 게임 제목 문자열 (T38k 카드; 교체 카드가 S 에 넣기 전에도 아틀라스 · 셀프테스트에 포함)
    public static readonly string[] TitleStrings = { "KOREAN DIVISION", "코리안 디비전", "제17 대한 사단" };

    /// <summary>true 면 번들 글꼴이 없을 때 Windows 시스템 글꼴 (맑은 고딕 등) 을 폴백으로 쓴다. KD_FONT_NO_SYSTEM=1 이면 꺼짐 (시험용).</summary>
    public static bool AllowSystemFonts = Environment.GetEnvironmentVariable("KD_FONT_NO_SYSTEM") != "1";
    /// <summary>글꼴 로드에 쓴 누적 시간 (ms) · 로드한 아틀라스 수 (T38k 측정).</summary>
    public static double FontLoadMs; public static int FontAtlasCount;

    static readonly string?[] rolePath = new string?[3];
    static readonly Dictionary<(FontRole, int), Font> atlases = new();
    static int[] codepoints = Array.Empty<int>();
    const int DisplayTier = 40;   // 이 크기 이상 단계는 제목 전용: ASCII + TitleStrings + S.GameTitle 글자만 (본문을 40px 이상으로 그리면 두부가 된다) (큰 아틀라스 로드 시간 절약)
    static int[] displayCodepoints = Array.Empty<int>();
    static bool customFont;

    /// <summary>
    /// 글꼴 탐색 순서 (역할마다): assets/fonts 번들 글꼴 → (본문 번들 글꼴) → Windows 시스템 글꼴 (개발 PC 폴백) → raylib 기본 폰트.
    /// 아틀라스에는 ASCII + Strings(S) 클래스 + extraText(데이터 파일의 이름 등)의 글자만 포함한다. 본문 16/26 만 시작 때 로드, 나머지는 처음 쓸 때.
    /// </summary>
    public static void LoadFonts(IEnumerable<string>? extraText = null)
    {
        codepoints = CollectCodepoints(extraText ?? Array.Empty<string>());
        displayCodepoints = CollectCodepoints(Array.Empty<string>(), displayOnly: true);
        foreach (FontRole r in Enum.GetValues<FontRole>()) rolePath[(int)r] = ResolvePath(r);
        customFont = rolePath.Any(p => p != null);
        if (!customFont) Console.Error.WriteLine("[font] 한글 글꼴을 찾지 못함 - raylib 기본 폰트 (한글이 깨짐)");
        Console.WriteLine($"[font] 본문={Short(rolePath[0])} 제목={Short(rolePath[1])} 숫자={Short(rolePath[2])}");
        foreach (int t in new[] { 16, 26 }) FontFor(FontRole.Body, t);
        if (Environment.GetEnvironmentVariable("KD_FONT_TIME") == "1")   // 측정용: 모든 단계를 미리 로드
            foreach (FontRole r in Enum.GetValues<FontRole>()) foreach (int t in Tiers[(int)r]) FontFor(r, t);
        Console.WriteLine($"[font] 아틀라스 {FontAtlasCount}개 로드 {FontLoadMs:0}ms (글자 {codepoints.Length})");
    }

    static string Short(string? p) => p == null ? "(기본)" : p.Replace((char)92, '/');

    /// <summary>역할의 글꼴 파일 경로 (없으면 null). 셀프테스트도 사용.</summary>
    public static string? ResolvePath(FontRole role)
    {
        string? own = FindBundled(BundledFile[(int)role]) ?? FindBundled(BundledFile[0]);
        if (own != null || !AllowSystemFonts) return own;
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        return new[] { "malgun.ttf", "NanumGothic.ttf" }.Select(f => Path.Combine(fonts, f)).FirstOrDefault(File.Exists);
    }

    /// <summary>번들 글꼴 절대 경로 (실행 폴더 또는 현재 폴더의 assets/fonts).</summary>
    public static string? FindBundled(string rel)
    {
        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            string p = Path.Combine(root, "assets", "fonts", rel);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static string BundledRelPath(FontRole role) => BundledFile[(int)role];

    public static int[] CollectCodepoints(IEnumerable<string> extraText, bool displayOnly = false)
    {
        var set = new SortedSet<int>();
        for (int c = 32; c < 127; c++) set.Add(c);
        foreach (var s in extraText.Concat(TitleStrings))
            foreach (char ch in s) set.Add(ch);
        if (displayOnly) { foreach (char ch in S.GameTitle) set.Add(ch); return set.ToArray(); }
        foreach (var type in new[] { typeof(S), typeof(KoreanDivision.Core.SnapMsg) })   // SnapMsg = 저장 파일 오류 메시지 (불러오기 목록 · 알림에 그대로 표시)
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.GetRawConstantValue() is string s)
                    foreach (char ch in s) set.Add(ch);
        return set.ToArray();
    }

    public static void Unload()
    {
        if (!customFont) return;
        foreach (var f in atlases.Values) Raylib.UnloadFont(f);
        atlases.Clear();
        Paper.Unload();
    }

    static int PickTier(FontRole role, float size)
    {
        int best = 0; double bestD = double.MaxValue;
        foreach (int t in Tiers[(int)role])
        {
            double d = Math.Abs(Math.Log(t / Math.Max(1f, size)));
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    static Font FontFor(FontRole role, float size)
    {
        string? path = rolePath[(int)role];
        if (path == null) return Raylib.GetFontDefault();
        int tier = PickTier(role, size);
        if (atlases.TryGetValue((role, tier), out var f)) return f;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int[] cps = tier >= DisplayTier ? displayCodepoints : codepoints;
        f = Raylib.LoadFontEx(path, tier, cps, cps.Length);
        Raylib.SetTextureFilter(f.Texture, TextureFilter.Bilinear);
        FontLoadMs += System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        FontAtlasCount++;
        atlases[(role, tier)] = f;
        return f;
    }

    static float Spacing(FontRole role) => rolePath[(int)role] != null ? 0 : 1;

    public static void Text(string text, float x, float y, float size, Color color, FontRole role = FontRole.Body) =>
        Raylib.DrawTextEx(FontFor(role, size), text, new Vector2(MathF.Round(x), MathF.Round(y)), size, Spacing(role), color);

    /// <summary>폭 maxW 에 맞춰 글자 크기를 (최소 minSize 까지) 줄여 그린다 — 소해상도에서 잘림 방지 (T38d).</summary>
    public static void TextFit(string text, float x, float y, float size, Color color, float maxW, float minSize = 12, FontRole role = FontRole.Body)
    {
        float w = Measure(text, size, role).X;
        if (w > maxW && w > 0) size = MathF.Max(minSize, MathF.Floor(size * maxW / w));
        Text(text, x, y, size, color, role);
    }

    public static Vector2 Measure(string text, float size, FontRole role = FontRole.Body) =>
        Raylib.MeasureTextEx(FontFor(role, size), text, size, Spacing(role));

    public static void TextCentered(string text, Rectangle r, float size, Color color, FontRole role = FontRole.Body)
    {
        Vector2 m = Measure(text, size, role);
        Text(text, r.X + (r.Width - m.X) / 2, r.Y + (r.Height - m.Y) / 2, size, color, role);
    }

    public static void Panel(Rectangle r, Color fill, Color border)
    {
        Raylib.DrawRectangleRec(r, fill);
        Paper.Grain(r);   // T08 종이 질감
        Raylib.DrawRectangleLinesEx(r, 1, border);
    }

    /// <summary>
    /// 클릭 가능 사각형 등록부 (T38g, UI 하네스 전용): 퍼즈가 켜면 (<see cref="HitRecording"/>) 이번 프레임에 그려진 버튼의 사각형을 모아
    /// 다음 프레임에 작은 버튼도 겨냥해 누를 수 있게 한다. 꺼져 있으면 아무것도 하지 않음 (게임 동작 무영향).
    /// </summary>
    public static bool HitRecording;
    public static readonly List<(Rectangle R, string Label)> Hits = new();
    public static void RegHit(Rectangle r, string label, bool enabled = true)
    {
        if (HitRecording && enabled && r.Width > 0 && r.Height > 0) Hits.Add((r, label));
    }

    public static void Button(Rectangle r, string label, bool hover, bool active)
    {
        RegHit(r, label);
        Color fill = active ? Palette.BtnActive : hover ? Palette.BtnHover : Palette.Btn;
        Raylib.DrawRectangleRounded(r, 0.2f, 4, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, Palette.PanelBorder);
        Paper.Grain(new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4));
        TextCentered(label, r, 16, Palette.Text);
    }

    public static bool Contains(Rectangle r, Vector2 p) => Raylib.CheckCollisionPointRec(p, r);
}
