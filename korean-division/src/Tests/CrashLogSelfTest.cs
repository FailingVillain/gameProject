using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T41 충돌 로그: KD_TEST_CRASH=1 가짜 충돌 → 파일 내용 · 10개 순환 · 쓰기 실패 무예외. KD_SELFTEST_ONLY=crashlog</summary>
public static class CrashLogSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), "kd_crashlog_" + Environment.ProcessId);
        string? oldDir = Environment.GetEnvironmentVariable("KD_CRASH_DIR"), oldFlag = Environment.GetEnvironmentVariable("KD_TEST_CRASH");
        var oldCtx = CrashLog.Context;
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Environment.SetEnvironmentVariable("KD_CRASH_DIR", dir);
            CrashLog.Context = () => ("game", "m01_test", 1234, 77);
            for (int i = 0; i < 60; i++) CrashLog.Note("notice " + i);

            Environment.SetEnvironmentVariable("KD_TEST_CRASH", null);
            var none = CrashLog.Guarded(CrashLog.MaybeInjectTestCrash, "test");
            check(none.Error == null && !Directory.Exists(dir), "crashlog: KD_TEST_CRASH 없으면 던지지 않고 파일도 없음");

            Environment.SetEnvironmentVariable("KD_TEST_CRASH", "1");
            var r = CrashLog.Guarded(CrashLog.MaybeInjectTestCrash, "test");
            check(r.Error is InvalidOperationException && r.Path != null && File.Exists(r.Path), "crashlog: 가짜 충돌이 던져지고 로그 파일이 생김");
            string txt = r.Path != null && File.Exists(r.Path) ? File.ReadAllText(r.Path) : "";
            check(txt.Contains("InvalidOperationException") && txt.Contains("KD_TEST_CRASH") && txt.Contains("   at "), "crashlog: 예외 형식 · 메시지 · 스택");
            check(txt.Contains("game version: " + Config.Version) && txt.Contains("snapshot FormatVersion: " + Snapshot.FormatVersion) && txt.Contains("replay FormatVersion: " + ReplayFile.FormatVersion), "crashlog: 버전 · FormatVersion");
            check(txt.Contains("screen: game") && txt.Contains("mission: m01_test") && txt.Contains("tick: 1234") && txt.Contains("seed: 77") && txt.Contains("autosave:"), "crashlog: 화면 · 미션 · 틱 · 시드 · 자동 저장 경로");
            var lines = txt.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            check(lines.Any(l => l.EndsWith(" notice 59")) && lines.Any(l => l.EndsWith(" notice 10")) && !lines.Any(l => l.EndsWith(" notice 9")), "crashlog: 최근 알림 50줄만 (10..59)");

            for (int i = 0; i < 12; i++) CrashLog.Write(new Exception("e" + i), "rot");
            var files = Directory.GetFiles(dir, "crash_*.log");
            check(files.Length == CrashLog.Keep, $"crashlog: 10개만 유지 ({files.Length})");
            check(r.Path != null && !File.Exists(r.Path), "crashlog: 가장 오래된 로그가 지워짐");

            string blocker = Path.Combine(Path.GetTempPath(), "kd_crashlog_file_" + Environment.ProcessId);
            File.WriteAllText(blocker, "x");
            Environment.SetEnvironmentVariable("KD_CRASH_DIR", Path.Combine(blocker, "sub"));
            string? bad = "unset";
            bool threw = false;
            try { bad = CrashLog.Write(new Exception("x"), "bad"); } catch { threw = true; }
            check(!threw && bad == null, "crashlog: 쓰기 실패(폴더 못 만듦)는 예외 없이 null");
            File.Delete(blocker);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KD_CRASH_DIR", oldDir);
            Environment.SetEnvironmentVariable("KD_TEST_CRASH", oldFlag);
            CrashLog.Context = oldCtx;
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }
}
