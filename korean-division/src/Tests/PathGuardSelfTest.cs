using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T51a: PathOf 경로 탈출 방지 + JsonLimits 한도. KD_SELFTEST_ONLY=pathguard</summary>
public static class PathGuardSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        // 동봉 id: 결과가 예전 Path.Combine 과 문자 그대로 같다 (동작 불변)
        bool same = true; int n = 0;
        foreach (var id in MissionDef.Available()) { n++; same &= MissionDef.PathOf(id) == Path.Combine(MissionDef.Folder, id + ".json") && File.Exists(MissionDef.PathOf(id)); }
        foreach (var id in MapFile.Available()) { n++; same &= MapFile.PathOf(id) == Path.Combine(MapFile.Folder, id + ".json") && File.Exists(MapFile.PathOf(id)); }
        check(n > 20 && same, $"pathguard: 동봉 미션 · 지도 {n}개 PathOf 불변");

        // PathFor (에피소드) · SaveManager.Sanitize 재사용
        string epDir = Path.Combine(AppContext.BaseDirectory, "assets", "data", "episodes");
        check(EpisodeDefs.PathFor("c1e1") == Path.Combine(epDir, "c1e1.json") && Path.GetFileName(EpisodeDefs.PathFor("../x")) == PathGuard.InvalidFileName
              && Path.GetFileName(EpisodeDefs.PathFor("CON")) == PathGuard.InvalidFileName, "pathguard: EpisodeDefs.PathFor 정상 id 불변 · 탈출 · 예약 이름 거부");
        check(UI.SaveManager.Sanitize("autosave_1") == "autosave_1" && UI.SaveManager.Sanitize("my.save-2") == "my.save-2" && UI.SaveManager.Sanitize(null) == "save"
              && UI.SaveManager.Sanitize("CON") == "_CON" && UI.SaveManager.Sanitize("nul.x") == "_nul.x" && UI.SaveManager.Sanitize("COM1") == "_COM1" && UI.SaveManager.Sanitize("console") == "console",
              "pathguard: SaveManager.Sanitize 정상 이름 불변 · 예약 이름 거름");

        var evil = new List<string?> { "..", "../x", "..\\x", "..\\..\\Windows\\win", "a/b", "a\\b", "/etc/passwd", "C:\\Windows\\win", "C:foo", "\\\\server\\share\\x",
            "", null, " ", "a b", "a.b", "x.", ".hidden", "a\0b", "m01\u202e", "미션", new string('a', 65), new string('a', 5000),
            "CON", "con", "NUL", "aux", "PRN", "COM1", "lpt9", "mission%2e%2e", "a:stream", "x\n" };
        int bad = 0; var firstBad = "";
        foreach (var id in evil)
        {
            bool ok = true;
            foreach (var (folder, of) in new (string, Func<string, string>)[] { (MissionDef.Folder, MissionDef.PathOf!), (MapFile.Folder, MapFile.PathOf!) })
            {
                string p = "";
                try { p = of(id!); } catch (ArgumentNullException) when (id == null) { continue; } catch (Exception e) { ok = false; firstBad = $"예외 {e.GetType().Name} '{id}'"; break; }
                if (File.Exists(p) || !Path.GetFullPath(p).StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase) || p.Contains("..")) { ok = false; firstBad = $"'{id}' → {p}"; }
                if (Path.GetFileName(p) != PathGuard.InvalidFileName) { ok = false; firstBad = $"거부 안 됨 '{id}' → {p}"; }
            }
            if (!ok) bad++;
        }
        check(bad == 0, $"pathguard: 악성 id {evil.Count}종(.. · 절대경로 · 슬래시 · 빈 · 긴 · 예약 이름) 전부 거부, 폴더 밖 없음, 예외 없음 {firstBad}");
        check(!PathGuard.TryCombine(MissionDef.Folder, "../x", out _, out string why) && why.Length > 0, "pathguard: TryCombine 은 한국어 사유를 돌려줌");
        check(PathGuard.IsSafeId(new string('a', 64), out _) && PathGuard.IsSafeId("m_01_Test", out _) && !PathGuard.IsSafeId("COM10x", out _) == false, "pathguard: 64자 · 영숫자 밑줄 허용, COM10x 는 예약 아님");
        bool loadFail = false;
        try { MissionDef.Load("../x"); } catch (FileNotFoundException) { loadFail = true; }
        check(loadFail && MissionDef.Peek("..\\..\\x") == null && MapFile.Peek("../../x") == null, "pathguard: Load 는 FileNotFound, Peek 는 null (크래시 없음)");

        // JsonLimits
        JsonLimitResult C(string t) => JsonLimits.Check(t);
        check(C("{\"a\":[1,2,{\"b\":\"x\"}], /*c*/ \"d\":null,}").Ok, "jsonlimits: 정상 + 주석 · 끝 쉼표 통과");
        check(C("").Code == "E_JSON_EMPTY" && JsonLimits.Check(null).Code == "E_JSON_EMPTY" && C("   ").Code == "E_JSON_EMPTY", "jsonlimits: 빈 입력 거부");
        check(C("{{{").Code == "E_JSON_SYNTAX" && C("\0\u0001garbage").Code == "E_JSON_SYNTAX", "jsonlimits: 깨진 입력 → 코드, 예외 없음");
        string Deep(int d) => new string('[', d) + new string(']', d);
        check(C(Deep(32)).Ok && C(Deep(33)).Code == "E_JSON_DEPTH" && C(Deep(40)).Code == "E_JSON_DEPTH" && C(Deep(100000)).Code is "E_JSON_DEPTH", "jsonlimits: 깊이 32 통과 · 33 · 40 · 100000 거부");
        check(C("{\"a\":1,\"a\":2}").Code == "E_JSON_DUP_KEY" && C("{\"Id\":1,\"id\":2}").Code == "E_JSON_DUP_KEY" && C("{\"a\":{\"a\":1},\"b\":{\"a\":1}}").Ok, "jsonlimits: 같은 객체 중복 키 거부 (대소문자 무시), 다른 객체는 허용");
        check(C("{\"s\":\"" + new string('x', 4096) + "\"}").Ok && C("{\"s\":\"" + new string('x', 4097) + "\"}").Code == "E_JSON_STRING" && C("{\"" + new string('k', 4097) + "\":1}").Code == "E_JSON_STRING", "jsonlimits: 문자열 4096 통과 · 4097 거부 (키 포함)");
        check(C("{\"s\":\"" + string.Concat(Enumerable.Repeat("\\u0041", 4097)) + "\"}").Code == "E_JSON_STRING", "jsonlimits: 이스케이프로 부풀린 긴 문자열도 거부");
        string Arr(int k) => "[" + string.Join(",", Enumerable.Repeat("0", k)) + "]";
        check(C(Arr(20000)).Ok && C(Arr(20001)).Code == "E_JSON_ARRAY", "jsonlimits: 배열 20000 통과 · 20001 거부");
        check(C("[NaN]").Code == "E_JSON_NONFINITE" && C("[Infinity]").Code == "E_JSON_NONFINITE" && C("[-Infinity]").Code == "E_JSON_NONFINITE" && C("[1e999]").Code == "E_JSON_NONFINITE" && C("{\"s\":\"NaN\"}").Ok, "jsonlimits: NaN · Infinity · 범위 초과 숫자 거부, 문자열 'NaN' 은 허용");
        check(C("[\"" + new string('a', 4 * 1024 * 1024) + "\"]").Code == "E_JSON_SIZE", "jsonlimits: 4MB 초과 거부");

        // 동봉 자료는 한도 안 (검사기가 정상 자료를 막지 않는다)
        var tooBig = new List<string>();
        foreach (var f in Directory.GetFiles(MissionDef.Folder, "*.json").Concat(Directory.GetFiles(MapFile.Folder, "*.json")))
            if (!JsonLimits.Check(File.ReadAllText(f)).Ok) tooBig.Add(Path.GetFileName(f) + ":" + JsonLimits.Check(File.ReadAllText(f)).Code);
        check(tooBig.Count == 0, $"jsonlimits: 동봉 미션 · 지도 전부 통과 {string.Join(",", tooBig)}");
    }
}
