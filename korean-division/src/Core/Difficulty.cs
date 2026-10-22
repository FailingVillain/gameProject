using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// <summary>전령 사로잡힘 · 유선 끊김 판정 규칙 (T10b, 초안 — 기본 끔). 난이도의 courierCapture · wireCut 은 이 규칙이 켜졌을 때만 소비된다.</summary>
    public CommsRulesDef CommsRules { get; set; } = new();
    /// <summary>로드 경고 (파싱 실패 · 범위 밖 값). 비어 있으면 정상. 데이터 파일 쪽이라 JSON 에 쓰지 않는다.</summary>
    [JsonIgnore] public List<string> Warnings { get; set; } = new();

    static DifficultyDefs? current;
    public static DifficultyDefs Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "difficulty.json"));
    /// <summary>[테스트] 정의를 갈아 끼운다 (null = 파일 다시 읽기).</summary>
    public static void Override(DifficultyDefs? d) => current = d;

    public static DifficultyDefs Load(string path)
    {
        string? text = null;
        try { if (File.Exists(path)) text = File.ReadAllText(path); }
        catch (IOException) { }
        return Parse(text, path);
    }

    /// <summary>JSON 문자열 → 정의 (Load · 테스트 공용). 파싱 실패 = 기본값 + 경고 · commsRules 범위 밖 값 = 범위 안으로 고정 + 경고 (Warnings · stderr). 던지지 않는다.</summary>
    public static DifficultyDefs Parse(string? json, string src = "difficulty.json")
    {
        DifficultyDefs d = new();
        if (json != null)
        {
            try
            {
                if (JsonSerializer.Deserialize<DifficultyDefs>(json, DataJson.Opts) is { } r) d = r;
            }
            catch (JsonException ex)
            {
                d = new DifficultyDefs();
                d.Warn($"{src} 파싱 실패 (기본값 사용): {ex.Message}");
            }
        }
        // 키는 소문자로 통일 (JSON 대소문자 무시와 맞춤)
        d.Levels = d.Levels.ToDictionary(kv => kv.Key.ToLowerInvariant(), kv => kv.Value);
        d.Default = (d.Default ?? "easy").ToLowerInvariant();
        if (!d.Levels.ContainsKey(d.Default)) d.Levels[d.Default] = new DifficultyLevel();
        d.CommsRules ??= new CommsRulesDef();
        var c = d.CommsRules;
        var def = new CommsRulesDef();
        c.CaptureRadius = Fix(d, "captureRadius", c.CaptureRadius, def.CaptureRadius, 1f, 50f);
        c.CaptureChance = Fix(d, "captureChance", c.CaptureChance, def.CaptureChance, 0f, 1f);
        c.CheckSec = Fix(d, "checkSec", c.CheckSec, def.CheckSec, 1f, float.MaxValue);
        c.CutRadius = Fix(d, "cutRadius", c.CutRadius, def.CutRadius, 1f, 50f);
        c.CutChance = Fix(d, "cutChance", c.CutChance, def.CutChance, 0f, 1f);
        return d;
    }

    /// <summary>범위 검사 · 고정. NaN = 기본값, 범위 밖 = 가장 가까운 경계 (둘 다 경고).</summary>
    static float Fix(DifficultyDefs d, string key, float v, float def, float lo, float hi)
    {
        if (float.IsNaN(v)) { d.Warn($"commsRules.{key} = NaN → 기본값 {def}"); return def; }
        float c = Math.Clamp(v, lo, hi);
        if (c != v) d.Warn($"commsRules.{key} = {v} 범위 밖 → {c} (허용 {lo}~{hi})");
        return c;
    }

    /// <summary>경고 기록 + stderr 한 줄 (기존 AiProfile · MissionDef 와 같은 방식).</summary>
    void Warn(string msg)
    {
        Warnings.Add(msg);
        Console.Error.WriteLine("[난이도 경고] " + msg);
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

/// <summary>
/// 전령 사로잡힘 · 유선 끊김 판정 (T10b 초안, 시간 모형 — 실제 기마 전령 유닛 · 전선 칸은 조각 ②). enabled=false = 기존 동작.
/// 전령: 출발점 = 보내는 세력의 첫 사령부 건물 (Buildings 순서, 살아 있는 것), 도착점 = 받는 대대의 현재 중심. 진행률 (보낸 틱 → 도착 틱) 로 선형 보간한 위치에서
///   checkSec 마다 captureRadius 칸 안에 적대 유닛이 있으면 captureChance (해시 난수) 로 사로잡힘 → 분실 (CourierCaptured). 사령부가 없으면 판정 안 함.
/// 유선: 발송 순간 받는 대대 중심에서 cutRadius 칸 안에 적대 유닛이 있으면 cutChance 로 끊김 → 즉시 분실 (WireCut).
/// </summary>
public sealed class CommsRulesDef
{
    public bool Enabled { get; set; }
    public float CaptureRadius { get; set; } = 8f;
    public float CaptureChance { get; set; } = 0.25f;
    public float CheckSec { get; set; } = 1f;
    public float CutRadius { get; set; } = 15f;
    public float CutChance { get; set; } = 0.5f;
}

/// <summary>AI 배수 (값은 사용자 승인 뒤 — 지금은 모두 1.0).</summary>
public sealed class AiLevelDef
{
    public float AccuracyMul { get; set; } = 1f;
    public float AmmoMul { get; set; } = 1f;
    public float ReinforceMul { get; set; } = 1f;
}
