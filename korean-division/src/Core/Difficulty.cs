using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 난이도 구조 (사용자 2026-10-22 승인, docs/COMMS_ALPHA_PLAN.md): assets/data/difficulty.json 한 곳에서 난이도 (easy · normal · hard) 마다
/// 통신 현실성 (comms) 과 AI 배수 (ai: 정확도 · 탄약 · 증원) 를 관리한다. 미션 · 캠페인 · 교전 테스트는 난이도 키 (World.Difficulty) 만 받는다.
/// 기본 = easy (유선 안 끊김, AI 배수 1.0 = 현재 동작).
/// </summary>
public sealed class DifficultyDefs
{
    public string Default { get; set; } = "easy";
    public Dictionary<string, DifficultyLevel> Levels { get; set; } = new();

    static DifficultyDefs? current;
    public static DifficultyDefs Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "difficulty.json"));
    /// <summary>[테스트] 정의를 갈아 끼운다 (null = 파일 다시 읽기).</summary>
    public static void Override(DifficultyDefs? d) => current = d;

    public static DifficultyDefs Load(string path)
    {
        DifficultyDefs d = new();
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<DifficultyDefs>(File.ReadAllText(path), DataJson.Opts) is { } r) d = r;
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
        // 키는 소문자로 통일 (JSON 대소문자 무시와 맞춤)
        d.Levels = d.Levels.ToDictionary(kv => kv.Key.ToLowerInvariant(), kv => kv.Value);
        d.Default = (d.Default ?? "easy").ToLowerInvariant();
        if (!d.Levels.ContainsKey(d.Default)) d.Levels[d.Default] = new DifficultyLevel();
        return d;
    }

    /// <summary>키 → 난이도 (모르는 키 · null = 기본 난이도).</summary>
    public DifficultyLevel Get(string? key) =>
        key != null && Levels.TryGetValue(key.ToLowerInvariant(), out var l) ? l : Levels[Default];

    /// <summary>키 정규화: 아는 키면 소문자, 모르면 기본 키.</summary>
    public string Normalize(string? key) =>
        key != null && Levels.ContainsKey(key.ToLowerInvariant()) ? key.ToLowerInvariant() : Default;

    public bool IsKnown(string? key) => key != null && Levels.ContainsKey(key.ToLowerInvariant());
}

public sealed class DifficultyLevel
{
    public CommsLevelDef Comms { get; set; } = new();
    public AiLevelDef Ai { get; set; } = new();
}

/// <summary>통신 현실성 (사용자 2026-10-21 확정): 유선 명령 1~3초 · 전령 통상 10~20초 · 쉬움 = 유선 안 끊김.</summary>
public sealed class CommsLevelDef
{
    public float WireMinSec { get; set; } = 1f;
    public float WireMaxSec { get; set; } = 3f;
    public float CourierMinSec { get; set; } = 10f;
    public float CourierMaxSec { get; set; } = 20f;
    /// <summary>유선이 포격 · 적에게 끊길 수 있는가 (쉬움 = false).</summary>
    public bool WireCut { get; set; }
    /// <summary>기마 전령이 사로잡힐 수 있는가 (조각 ②).</summary>
    public bool CourierCapture { get; set; } = true;
    /// <summary>전달 · 분실된 항목을 '전달 중' 목록에 남겨 두는 시간 (초, 표시 · 알림용).</summary>
    public float KeepSec { get; set; } = 10f;
}

/// <summary>AI 배수 (값은 사용자 승인 뒤 — 지금은 모두 1.0).</summary>
public sealed class AiLevelDef
{
    public float AccuracyMul { get; set; } = 1f;
    public float AmmoMul { get; set; } = 1f;
    public float ReinforceMul { get; set; } = 1f;
}
