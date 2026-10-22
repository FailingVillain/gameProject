using System.Text.Json;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>사용자 설정 (해상도 · 창 모드 · 음량). 실행 파일 옆 settings.json — 저장 형식 · 시뮬과 무관.</summary>
public static class UserSettings
{
    public enum WindowMode { Windowed, Borderless, Fullscreen }

    public static readonly (int W, int H)[] Resolutions = { (1024, 640), (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) };

    public static string? PathOverride;
    public static string FilePath => PathOverride ?? Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static int Width = Config.ScreenWidth, Height = Config.ScreenHeight;
    public static WindowMode Mode = WindowMode.Windowed;
    public static float Volume = 0.8f;   // 마스터 음량 (효과음 · 음성 공용 — Sfx.Volume)

    sealed class Data
    {
        public int Width { get; set; } = Config.ScreenWidth;
        public int Height { get; set; } = Config.ScreenHeight;
        public string Mode { get; set; } = "windowed";
        public float Volume { get; set; } = 0.8f;
    }

    static WindowMode applied = WindowMode.Windowed;   // 지금 창이 실제로 있는 모드

    /// <summary>[T49b] 마지막 Load 가 남긴 한국어 안내 (모르는 키 · 이상한 값 · 읽기 실패). 비면 문제 없음. 같은 줄을 콘솔(stderr)에도 낸다.</summary>
    public static readonly List<string> LoadMessages = new();
    static bool blockSave;   // 파일을 못 읽었는데 .bak 도 못 만들었다 = 원본을 덮어쓰지 않는다

    /// <summary>
    /// 파일을 읽는다 (창을 만들기 전에 호출). 관대하게: 파일이 없으면 기본값, 모르는 키는 경고만 하고 무시, 이상한 값은 그 키만 기본값,
    /// 파일째 못 읽으면 settings.json.bak 로 복사해 두고 기본값. 크래시하지 않는다. 안내문은 LoadMessages.
    /// </summary>
    public static void Load()
    {
        LoadMessages.Clear(); blockSave = false;
        string name = Path.GetFileName(FilePath);
        try
        {
            if (!File.Exists(FilePath)) return;
            string text = File.ReadAllText(FilePath);
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                root = doc.RootElement.Clone();
            }
            catch (JsonException ex) { Unreadable(name, $"JSON 문법 오류 (줄 {ex.LineNumber + 1}) - {ex.Message}"); return; }
            if (root.ValueKind != JsonValueKind.Object) { Unreadable(name, "최상위가 객체 { } 가 아니다"); return; }

            var d = new Data();
            var props = typeof(Data).GetProperties();
            foreach (var el in root.EnumerateObject())
            {
                var pi = props.FirstOrDefault(q => string.Equals(q.Name, el.Name, StringComparison.OrdinalIgnoreCase));
                if (pi == null) { if (!el.Name.StartsWith('_')) Note($"{name}: 알 수 없는 설정 '{el.Name}' 은(는) 무시한다"); continue; }
                try
                {
                    var v = el.Value.Deserialize(pi.PropertyType);
                    if (v == null) throw new JsonException("null");
                    pi.SetValue(d, v);
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
                { Note($"{name}: '{el.Name}' 값이 올바르지 않아 기본값을 쓴다"); }
            }
            Width = Math.Clamp(d.Width, Config.MinScreenWidth, 7680);
            Height = Math.Clamp(d.Height, Config.MinScreenHeight, 4320);
            Mode = d.Mode switch { "borderless" => WindowMode.Borderless, "fullscreen" => WindowMode.Fullscreen, _ => WindowMode.Windowed };
            Volume = Math.Clamp(float.IsNaN(d.Volume) ? 0.8f : d.Volume, 0f, 1f);
            Sfx.Volume = Volume;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Unreadable(name, e.Message); }
    }

    static void Note(string msg) { LoadMessages.Add(msg); Console.Error.WriteLine("[설정] " + msg); }

    /// <summary>읽을 수 없는 설정 파일: 기본값으로 시작하고, 원본은 .bak 로 복사해 둔다 (이미 있는 .bak 는 덮어쓰지 않는다).</summary>
    static void Unreadable(string name, string why)
    {
        Width = Config.ScreenWidth; Height = Config.ScreenHeight; Mode = WindowMode.Windowed; Volume = 0.8f; Sfx.Volume = Volume;
        string bak = FilePath + ".bak";
        try
        {
            for (int i = 1; File.Exists(bak); i++) bak = FilePath + ".bak." + i;
            if (File.Exists(FilePath)) File.Copy(FilePath, bak);
            Note($"{name} 을 읽을 수 없어 기본 설정으로 시작한다: {why} (원본은 {Path.GetFileName(bak)} 로 보관)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { blockSave = true; Note($"{name} 을 읽을 수 없어 기본 설정으로 시작한다: {why} (원본을 보관하지 못해 설정 저장을 건너뛴다)"); }
    }

    public static void Save()
    {
        if (blockSave) return;   // [T49b] 못 읽은 원본을 .bak 없이 덮어쓰지 않는다
        try
        {
            var d = new Data { Width = Width, Height = Height, Volume = Volume, Mode = Mode switch { WindowMode.Borderless => "borderless", WindowMode.Fullscreen => "fullscreen", _ => "windowed" } };
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
