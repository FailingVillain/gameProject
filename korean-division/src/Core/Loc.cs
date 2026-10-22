using System.Text;

namespace KoreanDivision.Core;

/// <summary>
/// [TI1] 표시 문구 다국어 (렌더 전용 · 시뮬은 읽지 않는다). 한국어 원문은 코드에 남고, 영어는 assets/i18n/en/*.csv 의 (key,en) 만 읽는다.
/// ko 이면 조회 없이 원문 반환(비용 0). en 이면 사전 조회, 없으면 원문 + KD_I18N_WARN=1 일 때 키당 1회 stderr.
/// CSV 열: key,ko,en,status,kohash (UTF-8 BOM 허용, RFC4180 따옴표, 줄바꿈 \n 이스케이프). 헤더가 있으면 열 이름으로, 없으면 0=key 2=en.
/// </summary>
public static class Loc
{
    public const string Ko = "ko", En = "en";

    static string language = Ko;
    static Dictionary<string, string>? enTable;
    static readonly HashSet<string> warned = new();
    [ThreadStatic] static bool forceKo;
    static bool warnMissing = Environment.GetEnvironmentVariable("KD_I18N_WARN") == "1";   // 한 번만 읽는다 (T() 는 프레임마다 수천 번 불린다)

    /// <summary>시험용: KD_I18N_WARN 을 다시 읽는다.</summary>
    public static void RereadEnv() => warnMissing = Environment.GetEnvironmentVariable("KD_I18N_WARN") == "1";

    /// <summary>테스트용: 이 경로(폴더)의 CSV 를 읽는다 (기본 = 실행 폴더 또는 현재 폴더의 assets/i18n/en).</summary>
    public static string? DirOverride;

    /// <summary>현재 언어 ("ko" | "en"). 모르는 값은 ko. 전환은 즉시 (재시작 불필요).</summary>
    public static string Language
    {
        get => language;
        set { language = value == En ? En : Ko; if (language == En) EnsureLoaded(); }
    }

    public static bool IsEn => language == En && !forceKo;

    public static string T(string key, string ko)
    {
        if (!IsEn) return ko;
        if (enTable != null && enTable.TryGetValue(key, out var en) && en.Length > 0) return en;
        if (warnMissing)
        {
            bool first; lock (warned) first = warned.Add(key);
            if (first) Console.Error.WriteLine($"[i18n] 키 누락: {key}");
        }
        return ko;
    }

    /// <summary>action 동안 언어를 ko 로 본다 (글꼴 코드포인트 수집 때 프로퍼티의 한국어 원문을 얻는 용도).</summary>
    public static T WithKo<T>(Func<T> f)
    {
        bool prev = forceKo; forceKo = true;
        try { return f(); } finally { forceKo = prev; }
    }

    /// <summary>모든 영어 문구 (글꼴 코드포인트 수집 · 시험용). 언어와 무관하게 사전을 읽는다.</summary>
    public static IEnumerable<string> AllText() { EnsureLoaded(); return enTable!.Values; }

    public static IReadOnlyDictionary<string, string> EnTable { get { EnsureLoaded(); return enTable!; } }

    public static void Reload() { enTable = null; lock (warned) warned.Clear(); if (language == En) EnsureLoaded(); }

    static void EnsureLoaded()
    {
        if (enTable != null) return;
        var t = new Dictionary<string, string>(StringComparer.Ordinal);
        string? dir = FindDir();
        if (dir != null)
            foreach (var f in Directory.GetFiles(dir, "*.csv").OrderBy(p => p, StringComparer.Ordinal))
            {
                try { foreach (var (k, v) in ParseCsv(File.ReadAllText(f, Encoding.UTF8), Path.GetFileName(f))) t[k] = v; }
                catch (IOException e) { Console.Error.WriteLine($"[i18n] {Path.GetFileName(f)} 을 읽지 못함: {e.Message}"); }
            }
        enTable = t;
    }

    static string? FindDir()
    {
        if (DirOverride != null) return Directory.Exists(DirOverride) ? DirOverride : null;
        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            string p = Path.Combine(root, "assets", "i18n", "en");
            if (Directory.Exists(p)) return p;
        }
        return null;
    }

    /// <summary>CSV 본문 -> (key, en) 목록. BOM · 따옴표("" 이스케이프, 따옴표 안 줄바꿈) · 역슬래시 이스케이프. en 이 빈 행은 건너뛴다.
    /// 따옴표는 필드 맨 앞에서만 열린다 (필드 중간의 " 는 문자 그대로). 닫히지 않은 따옴표는 경고 1줄(파일 · 줄) 후 그 행만 버리고 다음 줄부터 계속 읽는다.</summary>
    public static List<(string Key, string En)> ParseCsv(string text, string? source = null)
    {
        var rows = ParseRows(text, source);
        var res = new List<(string, string)>();
        int kc = 0, ec = 2, start = 0;
        if (rows.Count > 0 && rows[0].Count > 0 && rows[0][0] == "key")
        {
            kc = 0; ec = rows[0].IndexOf("en"); if (ec < 0) return res; start = 1;
        }
        for (int r = start; r < rows.Count; r++)
        {
            var rw = rows[r];
            if (rw.Count <= ec || rw.Count <= kc || rw[kc].Length == 0 || rw[kc].StartsWith('#')) continue;
            string en = Unescape(rw[ec]);
            if (en.Length > 0) res.Add((rw[kc], en));
        }
        return res;
    }

    /// <summary>CSV 본문 -> 행 목록 (이스케이프 해석 전의 필드 문자열). 따옴표 규칙은 ParseCsv 와 같다.</summary>
    public static List<List<string>> ParseRows(string text, string? source = null)
    {
        var rows = new List<List<string>>();
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
        var row = new List<string>(); var sb = new StringBuilder(); bool q = false, any = false, fieldStart = true;
        int rowStart = 0, closedAt = -2;
        for (int i = 0; i <= text.Length; i++)
        {
            bool malformed = false;
            if (!q && i > 0 && i <= text.Length && closedAt == i - 1 && i < text.Length && text[i] != ',' && text[i] != '\r' && text[i] != '\n') malformed = true;
            if (i == text.Length || malformed)
            {
                if (q || malformed)
                {
                    int line = 1; for (int k = 0; k < rowStart; k++) if (text[k] == '\n') line++;
                    Console.Error.WriteLine($"[i18n] {source ?? "CSV"} {line}줄: 닫히지 않은(또는 닫힌 뒤 글자가 붙은) 따옴표 - 그 행을 버린다");
                    int nl = text.IndexOf('\n', rowStart);
                    row = new(); sb.Clear(); any = false; q = false; fieldStart = true;
                    if (nl < 0) break;
                    i = nl; rowStart = nl + 1; closedAt = -2; continue;   // for 의 i++ 로 다음 줄 첫 글자
                }
                break;
            }
            char c = text[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; } else { q = false; closedAt = i; } }
                else sb.Append(c);
            }
            else if (c == '"' && fieldStart) { q = true; any = true; fieldStart = false; }
            else if (c == ',') { row.Add(sb.ToString()); sb.Clear(); any = true; fieldStart = true; }
            else if (c == '\r') { }
            else if (c == '\n') { if (any || sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); } row = new(); sb.Clear(); any = false; fieldStart = true; rowStart = i + 1; }
            else { sb.Append(c); any = true; fieldStart = false; }
        }
        if (any || sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); }

        return rows;
    }

    /// <summary>필드의 역슬래시 이스케이프(n, 역슬래시) 해석 (시험용 공개).</summary>
    public static string UnescapeField(string s) => Unescape(s);

    const char Bs = (char)92;   // 역슬래시
    static string Unescape(string s)
    {
        if (s.IndexOf(Bs) < 0) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == Bs && i + 1 < s.Length && (s[i + 1] == 'n' || s[i + 1] == Bs)) { sb.Append(s[i + 1] == 'n' ? '\n' : Bs); i++; }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }
}
