using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>장교 학파 · 적 세력 성향 자료 (assets/data/doctrines.json): 스키마 · 키 중복 · 문서 항목 수 (학파 5 · 일본 3 · 봉천 4 · 백군 3 · 적군 3 · 마적 = 봉천 토비형 공유). KD_SELFTEST_ONLY=doctrines.</summary>
public static class DoctrinesSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Doctrines? d = null; string err = "";
        try { d = Doctrines.Load(); } catch (Exception ex) { err = ex.Message; }
        check(d != null, "doctrines: 로드 + 스키마 검증 " + err);
        if (d == null) return;
        check(d.Validate().Count == 0, "doctrines: 검증 오류 없음");
        check(d.Schools.Count == 5 && new[] { "german", "french", "russian", "pyongyang", "beijing" }.All(k => d.School(k) != null), "doctrines: 학파 5종 (독일 · 불란서 · 노서아 · 평양 · 북경)");
        check(d.Schools.Select(s => s.Key).Distinct().Count() == d.Schools.Count, "doctrines: 학파 키 중복 없음");
        check(d.DispositionsOf("japanese").Count == 3 && d.DispositionsOf("fengtian").Count == 4 && d.DispositionsOf("russian-white").Count == 3 && d.DispositionsOf("russian-red").Count == 3, "doctrines: 세력 성향 수 (일본 3 · 봉천 4 · 백군 3 · 적군 3)");
        var bandit = d.DispositionsOf("bandit");
        check(bandit.Count == 1 && bandit[0].Key == d.DispositionsOf("fengtian")[3].Key, "doctrines: 마적 = 봉천 최하위(4번) 성향 공유");
        check(d.Factions.Values.SelectMany(f => f.Dispositions).All(x => x.Axes == null) && d.Schools.All(s => s.Axes == null), "doctrines: 수치 축 미정 (null, DECISIONS_PENDING)");
        var bad = new Doctrines { Version = 1 };
        bad.Schools.Add(new DoctrineSchool { Key = "a", Name = "x", Officer = "x", Tactics = "x", Ai = { "x" } });
        bad.Schools.Add(new DoctrineSchool { Key = "a", Name = "x", Officer = "x", Tactics = "x", Ai = { "x" } });
        check(bad.Validate().Any(m => m.Contains("중복")), "doctrines: 키 중복 검출");
    }
}
