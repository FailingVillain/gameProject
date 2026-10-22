using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>[TI1] Loc 기반: CSV 파서 (BOM · 따옴표 · \n), ko 무조회 / en 조회 / 누락 폴백, 설정 language 저장 · 관대한 읽기, assets/i18n/en 키 검사 (S 프로퍼티 존재 · kohash · 플레이스홀더 · 한글 0), 글꼴 코드포인트. 창 없이. KD_SELFTEST_ONLY=i18n</summary>
public static class I18nSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[i18n 기반 (TI1)]");
        string oldLang = Loc.Language; string? oldDir = Loc.DirOverride; string? oldPath = UserSettings.PathOverride;
        var errOld = Console.Error;
        try
        {
            // 1) CSV 파서
            string bs = ((char)92).ToString();
            string csv = "﻿key,ko,en,status,kohash\r\nA.x,\"한,글\",\"Hello, \"\"World\"\"\",draft,0\r\nA.y,줄,line1" + bs + "nline2,todo,0\r\nA.z,빈,,todo,0\r\n# c,a,b\r\nA.q,\"열\",\"multi\nline\",draft,0\r\n";
            var rows = Loc.ParseCsv(csv).ToDictionary(r => r.Key, r => r.En);
            check(rows.Count == 3 && rows["A.x"] == "Hello, \"World\"" && rows["A.y"] == "line1\nline2" && rows["A.q"] == "multi\nline" && !rows.ContainsKey("A.z"),
                $"CSV 파서: BOM · 따옴표 · 쉼표 · 줄바꿈 이스케이프 · 빈 en 건너뜀 ({rows.Count}행)");

            // 2) ko 는 조회 없음, en 은 조회, 누락은 원문
            string dir = TestTemp.Child("kd_i18n"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "t.csv"), "key,ko,en,status,kohash\r\nT.a,가,A,draft,0\r\n", new UTF8Encoding(true));
            Loc.DirOverride = dir; Loc.Reload();
            Loc.Language = "ko";
            check(Loc.T("T.a", "가") == "가" && Loc.T("T.none", "없음") == "없음", "ko: 조회 없이 원문");
            Loc.Language = "en";
            Console.SetError(new StringWriter());
            string miss = Loc.T("T.none", "없음");
            check(Loc.T("T.a", "가") == "A" && miss == "없음", "en: 사전 조회, 누락 키는 한국어 원문");
            check(Loc.WithKo(() => Loc.T("T.a", "가")) == "가" && Loc.T("T.a", "가") == "A", "WithKo: 범위 안에서만 ko");
            Console.SetError(errOld);
            Loc.Language = "zz"; check(Loc.Language == "ko", "모르는 언어 = ko");

            // 3) 설정 language 저장 / 읽기 / 관대함
            string sp = Path.Combine(dir, "settings.json"); UserSettings.PathOverride = sp;
            Console.SetError(TextWriter.Null);
            Loc.Language = "en"; UserSettings.Save();
            Loc.Language = "ko"; UserSettings.Load();
            check(Loc.Language == "en", "설정: language=en 저장 후 읽으면 en");
            File.WriteAllText(sp, "{ \"Language\": \"fr\" }"); UserSettings.Load();
            check(Loc.Language == "ko" && UserSettings.LoadMessages.Count == 1, "설정: 모르는 language 값 = ko + 안내 1줄");
            File.WriteAllText(sp, "{ \"Width\": 1280 }"); Loc.Language = "en"; UserSettings.Load();
            check(Loc.Language == "ko", "설정: language 키가 없으면 ko");
            Console.SetError(errOld);

            // 4) 실제 assets/i18n/en
            Loc.DirOverride = null; Loc.Reload(); Loc.Language = "ko";
            var table = Loc.EnTable;
            check(table.Count >= 3, $"assets/i18n/en 사전 로드 ({table.Count}키)");
            int noProp = 0, badPh = 0, hangul = 0; string first = "";
            var koOf = KoOf();
            Func<string, string> ph = s => string.Join(",", Regex.Matches(s, "\\{\\d[^}]*\\}").Select(m => m.Value).OrderBy(x => x, StringComparer.Ordinal));
            foreach (var (k, en) in table)
            {
                if (!koOf.TryGetValue(k, out var ko)) { noProp++; first = first == "" ? "프로퍼티 없음 " + k : first; continue; }
                if (Regex.IsMatch(en, "[가-힣]")) hangul++;
                if (ph(ko) != ph(en)) { badPh++; first = first == "" ? "플레이스홀더 " + k : first; }
            }
            check(noProp == 0 && badPh == 0 && hangul == 0, $"en 키 검사: S 프로퍼티 없음 {noProp} · 플레이스홀더 불일치 {badPh} · 한글 {hangul} {first}");
            int stale = StaleCount(koOf, out string staleFirst);
            check(stale == 0, $"en 행의 kohash = 현재 한국어 원문 (stale {stale}) {staleFirst}");

            // 5) 시범 변환: 한국어는 원문 그대로, en 은 시범 키 번역
            Loc.Language = "ko";
            check(S.CmPrisTitle == "7. 외교 · 포로" && S.SetLanguage == "언어", "ko: 변환된 S 프로퍼티 = 원문");
            Loc.Language = "en";
            check(S.CmPrisTitle == "7. Diplomacy / Prisoners" && S.CmPrisRepHead == "송환 (무료)", "en: 번역 있는 키는 영어, 없는 키는 한국어 폴백");

            // 6) 글꼴 코드포인트: ko 원문(프로퍼티) + en 사전 포함, 언어와 무관
            Loc.Language = "en"; int[] cpsEn = Ui.CollectCodepoints(Array.Empty<string>());
            Loc.Language = "ko"; int[] cpsKo = Ui.CollectCodepoints(Array.Empty<string>());
            check(cpsEn.SequenceEqual(cpsKo), $"코드포인트 수집은 언어와 무관 ({cpsKo.Length}자)");
            check("송환 (무료)".All(c => Array.BinarySearch(cpsKo, (int)c) >= 0) && "Prisoner ledger".All(c => Array.BinarySearch(cpsKo, (int)c) >= 0), "프로퍼티로 바뀐 한글 + en 사전 글자 포함");
        }
        catch (Exception e) { check(false, "i18n 시험 예외: " + e); }
        finally { Console.SetError(errOld); Loc.DirOverride = oldDir; Loc.Reload(); Loc.Language = oldLang; UserSettings.PathOverride = oldPath; }
    }

    /// <summary>S 의 모든 string 프로퍼티 -> 키 "S.이름" = 한국어 원문 (ko 강제).</summary>
    static Dictionary<string, string> KoOf()
    {
        var d = new Dictionary<string, string>();
        Loc.WithKo(() =>
        {
            foreach (var p in typeof(S).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0 && p.GetValue(null) is string s) d["S." + p.Name] = s;
            return 0;
        });
        return d;
    }

    static int StaleCount(Dictionary<string, string> koOf, out string first)
    {
        first = ""; int n = 0;
        string? dir = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Select(r => Path.Combine(r, "assets", "i18n", "en")).FirstOrDefault(Directory.Exists);
        if (dir == null) return 0;
        foreach (var f in Directory.GetFiles(dir, "*.csv"))
            foreach (var line in File.ReadAllLines(f, Encoding.UTF8).Skip(1))
            {
                var m = Regex.Match(line, "^([^,]+),\"([^\"]*)\",.*,([0-9a-f]{8})$");
                if (!m.Success || !koOf.TryGetValue(m.Groups[1].Value, out var ko)) continue;
                string h = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(ko))).ToLowerInvariant().Substring(0, 8);
                if (h != m.Groups[3].Value) { n++; first = first == "" ? "stale " + m.Groups[1].Value : first; }
            }
        return n;
    }
}
