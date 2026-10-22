using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>
/// [T49b] 관대한 읽기 (ops/SAVE_COMPAT_POLICY.md 설정 · 사용자 작성 파일): settings.json 은 모르는 키 무시(경고) · 이상한 값은 그 키만 기본값 ·
/// 파일째 못 읽으면 .bak 보존 후 기본값, 크래시 없음. 사용자 작성 commanders.json 은 못 읽으면 건너뛰고(빈 풀) 한국어 사유, 항목 단위 거부. SaveCompatSelfTest 가 호출.
/// </summary>
public static class TolerantReadSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        string dir = TestTemp.Child("kd_tolerant");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        var (oldW, oldH, oldM, oldV, oldP, oldSfx) = (UserSettings.Width, UserSettings.Height, UserSettings.Mode, UserSettings.Volume, UserSettings.PathOverride, Sfx.Volume);
        UserSettings.PathOverride = path;
        var errOld = Console.Error; Console.SetError(TextWriter.Null);   // 안내 줄 소음 차단
        try
        {
            void Reset() { UserSettings.Width = 1280; UserSettings.Height = 720; UserSettings.Mode = UserSettings.WindowMode.Windowed; UserSettings.Volume = 0.8f; }
            void Write(string t) { File.WriteAllText(path, t); Reset(); UserSettings.Load(); }

            // 정상 + 모르는 키: 값은 읽고 모르는 키는 경고만, 주석 키 "_" 는 조용히
            Write("{\"Width\":1024,\"Height\":640,\"Mode\":\"windowed\",\"Volume\":0.5,\"FutureKey\":{\"a\":1},\"_note\":\"x\"}");
            check(UserSettings.Width == 1024 && UserSettings.Height == 640 && Math.Abs(UserSettings.Volume - 0.5f) < 1e-5 && UserSettings.LoadMessages.Count == 1 && UserSettings.LoadMessages[0].Contains("FutureKey") && UserSettings.LoadMessages[0].Contains("무시"),
                "설정 읽기: 모르는 키는 경고하고 무시, 나머지 값은 그대로 읽는다");

            // 이상한 값: 그 키만 기본값 (다른 키는 유지)
            Write("{\"Width\":\"wide\",\"Height\":640,\"Volume\":null,\"Mode\":\"fullscreen\"}");
            check(UserSettings.Width == Config.ScreenWidth && UserSettings.Height == 640 && UserSettings.Volume == 0.8f && UserSettings.Mode == UserSettings.WindowMode.Fullscreen && UserSettings.LoadMessages.Count == 2 && UserSettings.LoadMessages.All(m => m.Contains("기본값")),
                $"설정 읽기: 이상한 값은 그 키만 기본값 ({string.Join(" | ", UserSettings.LoadMessages)})");
            UserSettings.Mode = UserSettings.WindowMode.Windowed;   // 시험이 창 모드를 바꾸지 않게 원복

            // 범위 밖 값은 기존대로 클램프, 주석 · 끝 쉼표 허용
            Write("{\"Width\":99999,\"Volume\":7, /* c */ }");
            check(UserSettings.Width == 7680 && UserSettings.Volume == 1f && UserSettings.LoadMessages.Count == 0, "설정 읽기: 범위 밖 값은 클램프, 주석 · 끝 쉼표는 허용");

            // 파일째 못 읽음: 기본값 + .bak 보존 + 크래시 없음
            int bad = 0; string first = "";
            foreach (var t in new[] { "{\"Width\":", "[1,2]", "", "   ", "null", "garbage\u0001\u0002", "{\"Width\":1024" })
            {
                foreach (var f in Directory.GetFiles(dir, "settings.json.bak*")) File.Delete(f);
                try
                {
                    Write(t);
                    bool ok = UserSettings.Width == Config.ScreenWidth && UserSettings.Volume == 0.8f && UserSettings.LoadMessages.Count == 1 && UserSettings.LoadMessages[0].Contains(".bak")
                        && File.Exists(path + ".bak") && File.ReadAllText(path + ".bak") == t;
                    if (!ok) { bad++; first = first == "" ? $"'{t}' → {string.Join("|", UserSettings.LoadMessages)}" : first; }
                }
                catch (Exception e) { bad++; first = first == "" ? $"'{t}' {e.GetType().Name}" : first; }
            }
            check(bad == 0, $"설정 읽기: 못 읽는 파일은 크래시 없이 기본값 + .bak 보존 (벗어난 경우 {bad}) {first}");

            // 두 번째 깨진 파일도 앞선 .bak 를 덮어쓰지 않는다
            foreach (var f in Directory.GetFiles(dir, "settings.json.bak*")) File.Delete(f);
            File.WriteAllText(path + ".bak", "FIRST");
            Write("{ broken");
            check(File.ReadAllText(path + ".bak") == "FIRST" && File.Exists(path + ".bak.1") && File.ReadAllText(path + ".bak.1") == "{ broken", "설정 읽기: 기존 .bak 는 덮어쓰지 않고 번호를 붙여 따로 보관");

            // 파일이 없으면 조용히 기본값
            File.Delete(path); Reset(); UserSettings.Load();
            check(UserSettings.Width == 1280 && UserSettings.LoadMessages.Count == 0, "설정 읽기: 파일이 없으면 안내 없이 기본값");
        }
        finally
        {
            Console.SetError(errOld);
            (UserSettings.Width, UserSettings.Height, UserSettings.Mode, UserSettings.Volume, UserSettings.PathOverride, Sfx.Volume) = (oldW, oldH, oldM, oldV, oldP, oldSfx);
            UserSettings.LoadMessages.Clear();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        // 사용자 작성 commanders.json: 못 읽어도 크래시 없이 건너뜀 (빈 풀 + 한국어 사유), 항목 단위 거부
        string cd = TestTemp.Child("kd_tolerant_cmd");
        Directory.CreateDirectory(cd);
        try
        {
            string src = Path.Combine(AppContext.BaseDirectory, "assets", "data");
            foreach (var f in new[] { CommanderCatalog.TraitFile, CommanderCatalog.TagFile }) if (File.Exists(Path.Combine(src, f))) File.Copy(Path.Combine(src, f), Path.Combine(cd, f), true);
            string cp = Path.Combine(cd, CommanderCatalog.CommanderFile);
            int cBad = 0; string cFirst = "";
            var inputs = new List<string> { "", "   ", "garbage", "[]", "null", "{}", "{\"commanders\":5}", "{\"commanders\":[1,null,\"x\",[]]}", "{\"commanders\":[{\"id\":5}]}", "{\"commanders\":[{\"id\":\"a\",\"hire\":\"x\",\"traits\":{}}]}", "{\"commanders\":[{\"id\":\"a\"", "ÿþ\u0000" };
            for (int i = 0; i < 40; i++) inputs.Add("{\"commanders\":[{\"id\":\"c" + i + "\",\"name\":" + (i % 2 == 0 ? "null" : "\"n\"") + ",\"hire\":{\"cost\":" + (i * 1000 - 5000) + "},\"favor\":null,\"zz\":1}]}");
            foreach (var t in inputs)
            {
                File.WriteAllText(cp, t);
                try
                {
                    var r = CommanderCatalog.Load(cd);
                    bool koreanReason = r.Errors.Concat(r.Warnings).All(m => m.Length > 0 && m.Any(ch => ch >= '가'));
                    bool silentEmpty = r.Defs.Count == 0 && r.Errors.Count == 0;   // 못 읽었는데 사유가 없으면 안 된다
                    if (!koreanReason || silentEmpty) { cBad++; cFirst = cFirst == "" ? t : cFirst; }
                }
                catch (Exception e) { cBad++; cFirst = cFirst == "" ? t + " " + e.GetType().Name : cFirst; }
            }
            check(cBad == 0, $"지휘관 JSON: 빈 · 깨진 · 엉뚱한 형식 {inputs.Count}건이 크래시 없이 한국어 사유로 건너뛰어진다 (벗어난 경우 {cBad}) {cFirst}");

            File.WriteAllText(cp, "{\"commanders\":[{\"id\":\"ok_one\",\"name\":\"가\"},{\"id\":5},{\"id\":\"bad_cost\",\"hire\":{\"cost\":999999}}],\"futureTop\":1}");
            var mix = CommanderCatalog.Load(cd);
            check(mix.Errors.Count >= 1 && mix.Errors.All(e => e.Contains("건너뜀") || e.Contains("비워")), $"지휘관 JSON: 일부 항목만 틀리면 그 항목만 사유와 함께 건너뛴다 ({mix.Defs.Count}명 통과, 사유 {mix.Errors.Count}건)");
        }
        finally { try { Directory.Delete(cd, true); } catch (IOException) { } }
    }
}
