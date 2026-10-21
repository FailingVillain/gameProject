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
        var feng = d.DispositionsOf("fengtian");
        check(bandit.Count == 1 && feng.Count > 3 && bandit[0].Key == feng[3].Key, "doctrines: 마적 = 봉천 최하위(4번) 성향 공유");
        check(d.Factions.Values.SelectMany(f => f.Dispositions).All(x => x.Axes == null) && d.Schools.All(s => s.Axes == null), "doctrines: 수치 축 미정 (null, DECISIONS_PENDING)");
        var bad = new Doctrines { Version = 1 };
        bad.Schools.Add(new DoctrineSchool { Key = "a", Name = "x", Officer = "x", Tactics = "x", Ai = { "x" } });
        bad.Schools.Add(new DoctrineSchool { Key = "a", Name = "x", Officer = "x", Tactics = "x", Ai = { "x" } });
        check(bad.Validate().Any(m => m.Contains("중복")), "doctrines: 키 중복 검출");
        // 참모진 탭 자료 경로 방어: 파일 없음 · null 목록 · 빈 학파가 NRE 없이 친절한 오류로 끝나야 한다 (UI 는 Load 예외를 잡아 안내 문구).
        check(LoadErr(Path.Combine(Path.GetTempPath(), "kd_no_such_doctrines.json")) is { } e1 && e1 is not NullReferenceException, "doctrines: 파일 없음 → 예외 (NRE 아님, UI 가 잡음)");
        const string nullLists = "{\"version\":1,\"schools\":[{\"key\":\"a\",\"name\":\"x\",\"officer\":\"x\",\"tactics\":\"x\",\"ai\":[\"x\"],\"research\":null,\"figures\":null}],\"factions\":{}}";
        var ex2 = LoadErr(WriteTmp(nullLists));
        check(ex2 is InvalidDataException && ex2.Message.Contains("null"), "doctrines: research/figures null → 검증 오류 (NRE 아님) " + ex2?.GetType().Name);
        var nullName = new Doctrines { Version = 1 };
        nullName.Schools.Add(new DoctrineSchool { Key = "a", Name = null!, Officer = null!, Tactics = null!, Ai = null!, Research = null!, Figures = null! });
        List<string>? ve = null; Exception? vx = null;
        try { ve = nullName.Validate(); } catch (Exception ex) { vx = ex; }
        check(vx == null && ve != null && ve.Count >= 3, "doctrines: Validate 가 null 이름 · 목록에 NRE 없이 오류 반환");
        var ex3 = LoadErr(WriteTmp("{\"version\":1,\"schools\":[],\"factions\":{}}"));
        check(ex3 is InvalidDataException && ex3.Message.Contains("schools"), "doctrines: 빈 schools → 검증 오류");
        var ex4 = LoadErr(WriteTmp("{\"version\":1,\"schools\":null,\"factions\":null}"));
        check(ex4 is InvalidDataException, "doctrines: schools/factions null → 검증 오류 " + ex4?.GetType().Name);
    }

    static Exception? LoadErr(string path)
    {
        try { Doctrines.Load(path); return null; } catch (Exception ex) { return ex; }
    }

    static string WriteTmp(string json)
    {
        var p = Path.Combine(Path.GetTempPath(), "kd_doctrines_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(p, json);
        return p;
    }
}
