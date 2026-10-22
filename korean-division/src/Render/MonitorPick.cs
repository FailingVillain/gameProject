using Raylib_cs;

namespace KoreanDivision.Render;

/// <summary>T38n: 환경변수 KD_MONITOR(primary | secondary | 정수 인덱스)로 창을 놓을 모니터를 고른다. 렌더/OS 전용 (시뮬 무관).
/// 미설정 = 기존 동작(손대지 않음). 항상 창 모드: 전체화면 · 테두리 없음 · 최대화는 절대 켜지 않는다
/// (UserSettings 가 전체화면이어도 KD_MONITOR 가 있으면 무시 — 에이전트 규칙 "windowed only").</summary>
public static class MonitorPick
{
    public const int Margin = 80;   // 모니터 크기에서 뺄 여유 (작업표시줄 · 제목줄)

    public readonly record struct Rect(int X, int Y, int W, int H);
    public readonly record struct Pick(int Index, string Label, int WinW, int WinH, int PosX, int PosY);

    /// <summary>순수 함수. mode 가 null/빈 값/해석 불가면 null (= 옮기지 않음). 모니터가 없으면 null.</summary>
    public static Pick? Choose(IReadOnlyList<Rect> mons, int primary, string? mode, int reqW, int reqH)
    {
        if (mons.Count == 0 || string.IsNullOrWhiteSpace(mode)) return null;
        string m = mode.Trim().ToLowerInvariant();
        if (primary < 0 || primary >= mons.Count) primary = 0;
        int idx; string label;
        if (m == "primary") { idx = primary; label = "primary"; }
        else if (m == "secondary")
        {
            idx = primary;
            for (int i = 0; i < mons.Count; i++) if (i != primary) { idx = i; break; }   // 첫 비주 모니터, 하나뿐이면 주 모니터
            label = "secondary";
        }
        else if (int.TryParse(m, out int n)) { idx = Math.Clamp(n, 0, mons.Count - 1); label = "index"; }
        else return null;
        var r = mons[idx];
        int w = Math.Min(reqW, Math.Max(320, r.W - Margin)), h = Math.Min(reqH, Math.Max(240, r.H - Margin));
        return new Pick(idx, label, w, h, r.X + (r.W - w) / 2, r.Y + (r.H - h) / 2);
    }

    /// <summary>InitWindow 직후 호출. KD_MONITOR 미설정이면 아무것도 안 함. 옮겼으면 true.</summary>
    public static bool Apply()
    {
        string? mode = Environment.GetEnvironmentVariable("KD_MONITOR");
        if (string.IsNullOrWhiteSpace(mode)) return false;
        int n = Raylib.GetMonitorCount();
        var list = new List<Rect>(n);
        for (int i = 0; i < n; i++)
        {
            var p = Raylib.GetMonitorPosition(i);
            list.Add(new Rect((int)p.X, (int)p.Y, Raylib.GetMonitorWidth(i), Raylib.GetMonitorHeight(i)));
        }
        var pick = Choose(list, 0, mode, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        if (pick is not { } k) return false;
        if (Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();   // 안전망: 항상 창 모드
        Raylib.SetWindowSize(k.WinW, k.WinH);
        Raylib.SetWindowPosition(k.PosX, k.PosY);
        Console.WriteLine($"[monitor] {k.Label} #{k.Index} of {n} -> window {k.WinW}x{k.WinH} at ({k.PosX},{k.PosY}) actual {(int)Raylib.GetWindowPosition().X},{(int)Raylib.GetWindowPosition().Y}");
        return true;
    }
}
