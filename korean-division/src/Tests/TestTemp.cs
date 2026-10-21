namespace KoreanDivision.Tests;

/// 테스트 임시 폴더 (병렬 에이전트 · 병렬 실행끼리 파일이 부딪치지 않도록 실행마다 따로 쓴다).
/// 기본: %TEMP%/KoreanDivision/run-<pid>-<ticks>/   · 환경변수 KD_TMP 가 있으면 그 폴더를 그대로 쓴다.
/// 이 프로세스가 기본 폴더를 만들었으면: 정상 종료(exit 0) 시 삭제, 실패 시 보존하고 경로를 출력 (디버깅용).
/// 기본 폴더 경로는 KD_TMP 로 자식 프로세스(--balance-worker 등)에 전달된다. 자식은 같은 폴더를 쓰고 지우지 않는다.
/// 골든 파일 · 에셋은 임시가 아니므로 여기 쓰지 않는다.
public static class TestTemp
{
    static readonly object gate = new();
    static string? dir;
    static bool owned;

    /// 이 실행의 임시 폴더 (처음 쓸 때 만든다).
    public static string Dir
    {
        get
        {
            lock (gate)
            {
                if (dir != null) { Directory.CreateDirectory(dir); return dir; }
                string env = (Environment.GetEnvironmentVariable("KD_TMP") ?? "").Trim();
                if (env.Length > 0) dir = Path.GetFullPath(env);
                else
                {
                    dir = Path.Combine(Path.GetTempPath(), "KoreanDivision", $"run-{Environment.ProcessId}-{DateTime.UtcNow.Ticks}");
                    owned = true;
                    Environment.SetEnvironmentVariable("KD_TMP", dir);   // 자식 프로세스가 같은 폴더를 쓰도록
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => Finish();
                }
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }

    /// 임시 폴더 안의 파일 경로.
    public static string Child(string name) => Path.Combine(Dir, name);

    static void Finish()
    {
        if (!owned || dir == null) return;
        if (Environment.ExitCode == 0)
        {
            try { Directory.Delete(dir, recursive: true); } catch (Exception) { }   // 잠긴 파일이 남으면 그냥 둔다
        }
        else Console.Error.WriteLine($"[TestTemp] 실패 - 임시 파일 보존: {dir}");
    }
}
