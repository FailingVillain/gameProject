namespace MapEditor.Model;

/// <summary>
/// 편집기 파일 안전 규칙 (창 없이 시험 가능하게 모델에 둠).
/// 1) 보호 경로: 게임 에셋 원본 (경로에 korean-division/assets/ 가 들어가거나 assets/maps · assets/missions 아래) 은 --allow-source-overwrite 없이는 저장으로 덮어쓰지 않는다.
/// 2) 테스트 실행 임시 id 는 프로세스마다 고유 (_editor_test_&lt;pid&gt;) -- 편집기를 둘 띄워도 서로의 사본을 덮지 않는다.
/// </summary>
public static class EditorPaths
{
    public static readonly string TestId = "_editor_test_" + Environment.ProcessId;

    /// <summary>true 면 보호 경로 덮어쓰기 허용 (--allow-source-overwrite).</summary>
    public static bool AllowSourceOverwrite;

    public static bool IsProtectedSource(string path, params string[] extraProtectedFolders)
    {
        string full = Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, '/');
        if (full.Contains("/korean-division/assets/", StringComparison.OrdinalIgnoreCase)) return true;
        if (full.Contains("/assets/maps/", StringComparison.OrdinalIgnoreCase) || full.Contains("/assets/missions/", StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var f in extraProtectedFolders)
        {
            if (string.IsNullOrEmpty(f)) continue;
            string folder = Path.GetFullPath(f).Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/') + "/";
            if (full.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>거부 메시지 (한/영).</summary>
    public static string RefuseMessage(string path) =>
        $"저장 거부: 게임 원본 에셋 경로입니다 / Refused: source asset path ({path}). 덮어쓰려면 --allow-source-overwrite 로 편집기를 다시 실행";
}
