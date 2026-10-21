using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 방열 메커니즘 시험용: 중화기 무게 조정 (2026-10-20, 맥심 9/7초 · 보병포 9/7 · 야전포 15/12 · 곡사포 20/15) 전의 짧은 방열 · 철수 시간으로 잠깐 되돌린다.
/// 시험 시간 상한이 짧은 옛 값에 맞춰져 있어서다. 새 실제 값은 Combat4SelfTest 가 확인한다. using 이 끝나면 원래 값을 복원한다.
/// </summary>
public sealed class LegacyDeploy : IDisposable
{
    static readonly (string Key, float Setup, float Pack)[] Old =
    {
        ("heavy_maxim", 3f, 2f), ("heavy_infantry_gun", 2f, 1.5f), ("heavy_field_gun", 4f, 3f), ("heavy_howitzer", 6f, 4f),
    };
    readonly List<(UnitDef D, float S, float P)> saved = new();
    readonly List<(UnitDef D, float V)> savedSpeed = new();

    /// <param name="maximSpeed">0 이 아니면 맥심 속도도 되돌린다 (도하 시험: 느린 맥심 중대가 교두보 비율을 못 채움).</param>
    public LegacyDeploy(float maximSpeed = 0f)
    {
        if (maximSpeed > 0 && Defs.Current.Units.TryGetValue("heavy_maxim", out var mx)) { savedSpeed.Add((mx, mx.Speed)); mx.Speed = maximSpeed; }
        foreach (var (key, s, p) in Old)
            if (Defs.Current.Units.TryGetValue(key, out var d)) { saved.Add((d, d.SetupTime, d.PackTime)); d.SetupTime = s; d.PackTime = p; }
    }

    public void Dispose() { foreach (var (d, v) in savedSpeed) d.Speed = v; foreach (var (d, s, p) in saved) { d.SetupTime = s; d.PackTime = p; } }

    public static void Wrap(Action body, float maximSpeed = 0f) { using var _ = new LegacyDeploy(maximSpeed); body(); }
}
