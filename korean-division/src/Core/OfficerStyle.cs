namespace KoreanDivision.Core;

/// <summary>
/// 장교 경험 성향 (N55, 사용자 승인 2026-10-19; balance.json officers.style). 확률 = 계급표[지휘자 계급] − 경험치 단계 × 단계당 감소, [min, max] 로 자름.
/// 계급 색인 0 = 지휘 장교 없음 (부사관 대리 · 지휘 없음 · 계급 미정), 1 소위 … 6 대령 (CommandRanks.cs).
///  · 행진 사격 성향 (march): 경험 적은 지휘관은 돌격 사격 (걸으며 쏘기) 전진, 경험 많은 지휘관은 분대 교대 약진 (사격과 기동). 굴림 = 지휘관 · 중대 · 지도 시드의 해시 (사람 성격 — 판마다 같음),
///    확률이 경험치로 바뀌면 같은 굴림으로 다시 판정 (전투 중 경험치 단계가 오르면 약진 쪽으로 넘어갈 수 있음).
///  · 기병 정면 돌격 성향 (cavFront): 무능한 (경험 적은) 지휘관은 형성된 보병 정면도 우회 없이 돌격. 굴림 = 지휘관 · 그룹 · 시드 · cavFrontWindowSec 구간의 해시.
/// 새 상태 없음 — 계급 · 경험치 · Id · 틱의 함수 (결정적).
/// </summary>
public sealed class OfficerStyleDef
{
    public float[] MarchByRank { get; set; } = { 0.7f, 0.7f, 0.7f, 0.5f, 0.4f, 0.35f, 0.3f };
    public float MarchXpPerLevel { get; set; } = 0.08f;
    public float MarchMin { get; set; } = 0.15f;
    public float MarchMax { get; set; } = 0.85f;
    public float[] CavFrontByRank { get; set; } = { 0.5f, 0.5f, 0.45f, 0.3f, 0.2f, 0.15f, 0.1f };
    public float CavFrontXpPerLevel { get; set; } = 0.08f;
    public float CavFrontMin { get; set; } = 0.03f;
    public float CavFrontMax { get; set; } = 0.8f;
    public float CavFrontWindowSec { get; set; } = 20f;
}

public sealed partial class World
{
    static float StyleChance(float[] byRank, float perLevel, float min, float max, int rank, int xpLevel)
    {
        if (byRank.Length == 0) return min;
        float p = byRank[Math.Clamp(rank, 0, byRank.Length - 1)] - perLevel * xpLevel;
        return Math.Clamp(p, min, max);
    }

    /// <summary>[0,1) 결정적 해시 (성향 굴림 전용 — 틱 무관).</summary>
    internal static float StyleHash(int a, int b, int c)
    {
        ulong h = (ulong)(uint)a * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)b * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)c * 0x165667B19E3779F9UL ^ 0x5EED_A55AUL;
        h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33;
        return (h >> 40) / (float)(1UL << 24);
    }

    static int StyleRank(Unit? cmd) => cmd != null && cmd.IsOfficer ? Math.Max(0, cmd.OfficerRank) : 0;

    /// <summary>지휘관 (장교 · 대리 부사관 · null) 의 돌격 사격 성향 확률.</summary>
    public float MarchStyleChance(Unit? cmd)
    {
        var s = Defs.Officers.Style;
        return StyleChance(s.MarchByRank, s.MarchXpPerLevel, s.MarchMin, s.MarchMax, StyleRank(cmd), XpLevel(cmd));
    }

    /// <summary>이 중대 지휘관이 돌격 사격 전진을 고르는가 (false = 분대 교대 약진). 지휘관 · 중대 · 시드 해시 &lt; 성향 확률.</summary>
    public bool PrefersMarchFire(Company c)
    {
        var cmd = CommanderOf(c);
        return StyleHash(cmd?.Id ?? 0, c.Id, Map.Seed) < MarchStyleChance(cmd);
    }

    /// <summary>지휘관의 기병 정면 돌격 성향 확률.</summary>
    public float CavFrontChance(Unit? cmd)
    {
        var s = Defs.Officers.Style;
        return StyleChance(s.CavFrontByRank, s.CavFrontXpPerLevel, s.CavFrontMin, s.CavFrontMax, StyleRank(cmd), XpLevel(cmd));
    }

    /// <summary>이 기병 그룹 지휘관이 지금 (cavFrontWindowSec 구간) 형성된 정면에도 돌격하는가.</summary>
    public bool RashCavalryFront(Unit? cmd, int groupId)
    {
        long win = Math.Max(1, (long)MathF.Round(Defs.Officers.Style.CavFrontWindowSec / Config.SimDt));
        return StyleHash((cmd?.Id ?? 0) ^ 0x3C5A, groupId * 31 + Map.Seed, (int)(Tick / win)) < CavFrontChance(cmd);
    }
}
