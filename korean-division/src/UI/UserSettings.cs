using System.Text.Json;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>사용자 설정 (해상도 · 창 모드 · 음량). 실행 파일 옆 settings.json — 저장 형식 · 시뮬과 무관.</summary>
/// <summary>유닛 머리 위 표시 단계 (T38m): 간결 = 선택 · 마우스오버 · 위험한 것만, 보통 = 예전 동작, 상세 = 아군 체력 · 탄약 · 적재량 상시. 렌더 전용.</summary>
public enum OverlayLevel { Concise = 0, Normal = 1, Detailed = 2 }

public static class UserSettings
{
    public enum WindowMode { Windowed, Borderless, Fullscreen }

    public static readonly (int W, int H)[] Resolutions = { (1024, 640), (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) };

    public static string? PathOverride;
    public static string FilePath => PathOverride ?? Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static int Width = Config.ScreenWidth, Height = Config.ScreenHeight;
    public static WindowMode Mode = WindowMode.Windowed;
    public static bool ColorBlind = false;   // 색각 대응 표식 (T27 ⑧, 기본 꺼짐)
    public static OverlayLevel Overlay = OverlayLevel.Concise;   // 머리 위 표시 단계 (T38m, 기본 간결)
    public static float Volume = 0.8f;   // 마스터 음량 (효과음 · 음성 공용 — Sfx.Volume)

    /// <summary>카드 명령 키 재지정 (명령 Key -> 대문자 A~Z). 기본과 같은 것은 넣지 않는다. UI 전용 — 시뮬 · 저장 형식 무관 (T38a).</summary>
    public static Dictionary<string, char> KeyMap = new();

    sealed class Data
    {
        public int Width { get; set; } = Config.ScreenWidth;
        public int Height { get; set; } = Config.ScreenHeight;
        public string Mode { get; set; } = "windowed";
        public float Volume { get; set; } = 0.8f;
        public bool ColorBlind { get; set; } = false;
        public string Overlay { get; set; } = "concise";
        public Dictionary<string, string>? Keys { get; set; }
    }

    static WindowMode applied = WindowMode.Windowed;   // 지금 창이 실제로 있는 모드

    /// <summary>파일을 읽는다 (없거나 깨졌으면 기본값). 창을 만들기 전에 호출.</summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var d = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath));
            if (d == null) return;
            Width = Math.Clamp(d.Width, Config.MinScreenWidth, 7680);
            Height = Math.Clamp(d.Height, Config.MinScreenHeight, 4320);
            Mode = d.Mode switch { "borderless" => WindowMode.Borderless, "fullscreen" => WindowMode.Fullscreen, _ => WindowMode.Windowed };
            Volume = Math.Clamp(float.IsNaN(d.Volume) ? 0.8f : d.Volume, 0f, 1f);
            ColorBlind = d.ColorBlind;
            Overlay = ParseOverlay(d.Overlay);
            Sfx.Volume = Volume;
            KeyMap = new();
            if (d.Keys != null)
                foreach (var (cmd, v) in d.Keys)
                    if (!string.IsNullOrEmpty(v) && KoreanDivision.Core.Commands.Get(cmd) != null) KeyMap[cmd] = char.ToUpperInvariant(v[0]);
            Bindings.Sanitize();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
    }

    /// <summary>설정 문자열 -> 단계 (없거나 모르는 값 = 간결).</summary>
    public static OverlayLevel ParseOverlay(string? s) => s?.Trim().ToLowerInvariant() switch { "normal" => OverlayLevel.Normal, "detailed" => OverlayLevel.Detailed, _ => OverlayLevel.Concise };
    public static string OverlayName(OverlayLevel l) => l switch { OverlayLevel.Normal => "normal", OverlayLevel.Detailed => "detailed", _ => "concise" };
    public static OverlayLevel NextOverlay(OverlayLevel l) => (OverlayLevel)(((int)l + 1) % 3);

    public static void Save()
    {
        try
        {
            var d = new Data { Width = Width, Height = Height, Volume = Volume, ColorBlind = ColorBlind, Overlay = OverlayName(Overlay), Keys = KeyMap.Count == 0 ? null : KeyMap.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value.ToString()), Mode = Mode switch { WindowMode.Borderless => "borderless", WindowMode.Fullscreen => "fullscreen", _ => "windowed" } };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>창 크기 · 모드를 지금 창에 적용 (InitWindow 뒤).</summary>
    public static void Apply()
    {
        if (applied == WindowMode.Fullscreen && Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();
        else if (applied == WindowMode.Borderless) Raylib.ToggleBorderlessWindowed();
        applied = WindowMode.Windowed;
        switch (Mode)
        {
            case WindowMode.Windowed:
                Raylib.SetWindowSize(Width, Height);
                int mon = Raylib.GetCurrentMonitor();
                Raylib.SetWindowPosition(Math.Max(0, (Raylib.GetMonitorWidth(mon) - Width) / 2), Math.Max(30, (Raylib.GetMonitorHeight(mon) - Height) / 2));
                break;
            case WindowMode.Borderless:
                Raylib.ToggleBorderlessWindowed();
                applied = WindowMode.Borderless;
                break;
            case WindowMode.Fullscreen:
                Raylib.SetWindowSize(Width, Height);
                Raylib.ToggleFullscreen();
                applied = WindowMode.Fullscreen;
                break;
        }
    }

    public static void SetVolume(float v) { Volume = Math.Clamp(MathF.Round(v * 10) / 10f, 0f, 1f); Sfx.Volume = Volume; }
}
