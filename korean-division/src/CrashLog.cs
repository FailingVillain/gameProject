using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace KoreanDivision;

/// <summary>처리 안 된 예외 → logs/crash-날짜.txt (시각 · 예외 · 스택 · 버전/커밋 · FormatVersion · 미션/시드 · 마지막 로그 줄) + 짧은 한글 안내. 시뮬과 무관.</summary>
public static class CrashLog
{
    public static string? Context;   // 현재 게임 (미션 · 시드), Program 이 갱신
    public static string? FolderOverride;
    static readonly Queue<string> tail = new();
    static int installed;

    public static string Folder => FolderOverride ?? Path.Combine(AppContext.BaseDirectory, "logs");

    sealed class Tee : TextWriter
    {
        readonly TextWriter inner;
        readonly StringBuilder line = new();
        public Tee(TextWriter inner) { this.inner = inner; }
        public override Encoding Encoding => inner.Encoding;
        public override void Write(char c) { inner.Write(c); Capture(c); }
        public override void Write(string? s) { inner.Write(s); if (s != null) foreach (char c in s) Capture(c); }
        public override void WriteLine(string? s) { inner.WriteLine(s); Write(s); Capture('\n'); }
        public override void Flush() => inner.Flush();
        void Capture(char c)
        {
            if (c != '\n') { if (line.Length < 400) line.Append(c); return; }
            lock (tail) { tail.Enqueue(line.ToString()); while (tail.Count > 20) tail.Dequeue(); }
            line.Clear();
        }
    }

    public static void Install()
    {
        if (Interlocked.Exchange(ref installed, 1) == 1) return;
        try { Console.SetError(new Tee(Console.Error)); Console.SetOut(new Tee(Console.Out)); } catch (Exception) { }
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);
    }

    public static string? Report(Exception? ex)
    {
        string? path = null;
        try
        {
            var asm = typeof(CrashLog).Assembly;
            string rev = asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "GitRev")?.Value ?? "?";
            var sb = new StringBuilder();
            sb.AppendLine($"시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"버전: {Config.Version} (커밋 {(rev.Length == 0 ? "?" : rev)})  FormatVersion(저장) {Core.Snapshot.FormatVersion}  FormatVersion(리플레이) {Core.ReplayFile.FormatVersion}");
            sb.AppendLine($"게임: {Context ?? "(시작 화면)"}");
            sb.AppendLine($"OS: {RuntimeInformation.OSDescription}  .NET {Environment.Version}");
            sb.AppendLine();
            sb.AppendLine(ex?.ToString() ?? "(알 수 없는 예외)");
            sb.AppendLine();
            sb.AppendLine("마지막 로그 줄:");
            lock (tail) foreach (var l in tail) sb.AppendLine("  " + l);
            Directory.CreateDirectory(Folder);
            path = Path.Combine(Folder, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }
        catch (Exception) { }
        ShowMessage(path);
        return path;
    }

    static void ShowMessage(string? path)
    {
        string msg = path != null ? $"예기치 않은 오류로 게임이 멈췄습니다.\n오류 기록을 저장했습니다:\n{path}" : "예기치 않은 오류로 게임이 멈췄습니다.";
        try { Console.Error.WriteLine(msg); } catch (Exception) { }
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("KD_NO_MSGBOX") == "1") return;
        try { MessageBoxW(IntPtr.Zero, msg, "Korean Division", 0x10); } catch (Exception) { }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
