using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// 충돌 로그 (T41): 처리 안 된 예외 · 셀프테스트 치명 오류를 사용자 데이터 폴더의 crash/ 에 시각 이름 파일로 남긴다.
/// 렌더 / UI 스레드 전용 — 시뮬레이션은 건드리지 않으며(벽시계는 파일 이름 · 머리말에만), 쓰기는 절대 예외를 던지지 않는다.
/// 폴더: KD_CRASH_DIR → (KD_CAMPAIGN_DIR 가 있으면 그 아래 crash) → %LOCALAPPDATA%/KoreanDivision/crash. 최근 10개만 남긴다.
/// </summary>
public static class CrashLog
{
    public const int Keep = 10;
    public const int RingSize = 50;
    const string Prefix = "crash_", Ext = ".log";

    /// <summary>화면 · 미션 · 틱 · 시드 (게임 / 메뉴가 등록). 호출이 예외를 던져도 Write 가 삼킨다.</summary>
    public static Func<(string Screen, string? Mission, long Tick, int Seed)>? Context;

    static readonly object gate = new();
    static readonly Queue<string> ring = new();
    static int seq;

    public static string Folder
    {
        get
        {
            if (Environment.GetEnvironmentVariable("KD_CRASH_DIR") is { Length: > 0 } d) return d;
            if (Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR") is { Length: > 0 } c) return Path.Combine(c, "crash");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoreanDivision", "crash");
        }
    }

    /// <summary>최근 알림 / 로그 한 줄 기록 (최근 50줄만).</summary>
    public static void Note(string line)
    {
        try
        {
            lock (gate)
            {
                ring.Enqueue($"{DateTime.Now:HH:mm:ss} {line}");
                while (ring.Count > RingSize) ring.Dequeue();
            }
        }
        catch { }
    }

    /// <summary>처리 안 된 예외 훅 등록 (프로세스 종료 · 종료 코드는 그대로 — 기록만 한다).</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "unknown"), "unhandled");
    }

    /// <summary>KD_TEST_CRASH=1 이면 가짜 예외를 던진다 (시험 전용 훅).</summary>
    public static void MaybeInjectTestCrash()
    {
        if (Environment.GetEnvironmentVariable("KD_TEST_CRASH") == "1") throw new InvalidOperationException("KD_TEST_CRASH: injected fake crash");
    }

    /// <summary>동작을 돌리다 예외가 나면 로그를 쓰고 (예외, 로그 경로) 를 돌려준다 — 시험 · 치명 오류 경로용 (다시 던지지는 않는다).</summary>
    public static (Exception? Error, string? Path) Guarded(Action act, string kind)
    {
        try { act(); return (null, null); }
        catch (Exception e) { return (e, Write(e, kind)); }
    }

    /// <summary>로그 파일 쓰기. 실패해도 예외 없이 null.</summary>
    public static string? Write(Exception e, string kind)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Korean Division crash log ({kind})");
            sb.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"game version: {Config.Version}");
            sb.AppendLine($"snapshot FormatVersion: {Snapshot.FormatVersion}");
            sb.AppendLine($"replay FormatVersion: {ReplayFile.FormatVersion}");
            try
            {
                var c = Context?.Invoke();
                if (c != null) sb.AppendLine($"screen: {c.Value.Screen}\nmission: {c.Value.Mission ?? "(none)"}\ntick: {c.Value.Tick}\nseed: {c.Value.Seed}");
                else sb.AppendLine("screen: (unknown)\nmission: (unknown)\ntick: (unknown)\nseed: (unknown)");
            }
            catch (Exception ce) { sb.AppendLine($"context: unavailable ({ce.GetType().Name})"); }
            sb.AppendLine($"autosave: {NewestAutosave() ?? "(none)"}");
            sb.AppendLine();
            sb.AppendLine("exception:");
            sb.AppendLine(e.ToString());
            sb.AppendLine();
            sb.AppendLine($"recent notices (last {RingSize}):");
            lock (gate) foreach (var l in ring) sb.AppendLine(l);

            string dir = Folder;
            Directory.CreateDirectory(dir);
            string name;
            lock (gate) name = $"{Prefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}_{seq++ % 1000:000}{Ext}";
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Rotate(dir);
            return path;
        }
        catch { return null; }
    }

    static string? NewestAutosave()
    {
        try
        {
            string dir = SaveManager.Folder;
            if (!Directory.Exists(dir)) return null;
            return Directory.GetFiles(dir, "autosave_*" + Snapshot.Extension).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
        catch { return null; }
    }

    static void Rotate(string dir)
    {
        try
        {
            var files = Directory.GetFiles(dir, Prefix + "*" + Ext).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToList();
            for (int i = 0; i < files.Count - Keep; i++) { try { File.Delete(files[i]); } catch { } }
        }
        catch { }
    }
}
