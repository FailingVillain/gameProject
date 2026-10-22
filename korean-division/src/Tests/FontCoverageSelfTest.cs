using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T38k: 번들 OFL 글꼴 3종이 있고, 게임이 쓰는 모든 코드포인트 (S · SnapMsg · 데이터 파일 글자 · 제목 3문자열) 가 3 글꼴 cmap 에 있다. 창 없이. KD_SELFTEST_ONLY=fonts</summary>
public static class FontCoverageSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[글꼴 커버리지 (T38k)]");
        try
        {
            bool prev = Ui.AllowSystemFonts;
            Ui.AllowSystemFonts = false;   // 시스템 글꼴 탐색을 끈 상태에서도 번들 글꼴이 3종 모두 잡힌다
            try
            {
                foreach (Ui.FontRole r in Enum.GetValues<Ui.FontRole>())
                {
                    string? p = Ui.ResolvePath(r);
                    check(p != null && p.Replace((char)92, '/').Contains("/assets/fonts/") && p.EndsWith(Ui.BundledRelPath(r).Split('/')[1]),
                        $"글꼴 {r}: 번들 {Ui.BundledRelPath(r)} (시스템 글꼴 꺼짐) -> {p}");
                }
            }
            finally { Ui.AllowSystemFonts = prev; }

            var defs = Defs.Current;
            int[] cps = Ui.CollectCodepoints(defs.AllText().Concat(MissionDef.AllText()));
            int hangul = cps.Count(c => c >= 0xAC00 && c <= 0xD7A3);
            Console.WriteLine($"  수집 코드포인트 {cps.Length} (한글 {hangul})");
            check(cps.Length > 800 && hangul > 500, "수집 코드포인트가 충분 (S + 데이터 + 제목)");
            foreach (var title in Ui.TitleStrings)
                check(title.All(ch => Array.BinarySearch(cps, (int)ch) >= 0), $"제목 문자열 포함: {title}");
            int[] disp = Ui.CollectCodepoints(Array.Empty<string>(), displayOnly: true);
            check(disp.Length < 120 && Ui.TitleStrings.Concat(new[] { S.GameTitle }).All(t => t.All(ch => Array.BinarySearch(disp, (int)ch) >= 0)),
                $"큰 제목 아틀라스 (>=40px) 글자 {disp.Length}개에 제목 문자열 모두 포함");
            string[] wanted = { "KOREAN DIVISION", "코리안 디비전", "제17 대한 사단" };
            check(wanted.All(w => Ui.TitleStrings.Contains(w)), "제목 3문자열 = KOREAN DIVISION · 코리안 디비전 · 제17 대한 사단");

            foreach (Ui.FontRole r in Enum.GetValues<Ui.FontRole>())
            {
                string? rel = Ui.BundledRelPath(r), p = Ui.FindBundled(rel);
                if (p == null) { check(false, $"글꼴 파일 없음: {rel}"); continue; }
                var cm = FontCmap.Load(p);
                var missing = cps.Where(c => c >= 32 && !cm.Contains(c)).ToList();
                check(missing.Count == 0, $"글꼴 {r} ({Path.GetFileName(p)}, cmap {cm.Count}자): 누락 {missing.Count}"
                    + (missing.Count > 0 ? " [" + string.Join(" ", missing.Take(20).Select(c => $"U+{c:X4}")) + "]" : ""));
                string ofl = Path.Combine(Path.GetDirectoryName(p)!, "OFL.txt");
                check(File.Exists(ofl), $"OFL.txt 동봉: {rel.Split('/')[0]}");
            }
        }
        catch (Exception e) { check(false, "글꼴 커버리지 시험 예외: " + e.Message); }
    }
}
