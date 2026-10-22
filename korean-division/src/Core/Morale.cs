namespace KoreanDivision.Core;

/// <summary>
/// M1 아군 사기 ↔ 제압 연동 (ops/DECISIONS BH2 · BH3, 2026-10-22). 세력 사기 = 정수 0~100, 기본 50.
/// 효과는 하나뿐: 제압 하락(회복) 속도 배수 (Combat3.DecaySuppression). 50 = 정확히 1.0 (정수 비교로 건너뜀 → 기본 상태 결과 불변).
/// 0~50 · 50~100 구간을 combat.moraleSuppressDecayAt0 / At100 까지 선형 (기본 0.75 / 1.25 = 0.75 + 0.5 × 사기/100, 자리표시 — 0.3 밸런스).
/// 적 사기 시스템 · 패주 · 탈영은 없다 (BH1; 탈영은 전투 뒤 캠페인 층 M2). 사기는 전투 중 바뀌지 않는다 — 미션 시작(틱 0 전)에 SetFactionMorale 로만 정한다.
/// 상태 = Faction.Morale (FactionSnapshot, 체크섬은 50 이 아닐 때만), 리플레이 머리말 Morale.
/// </summary>
public sealed partial class Faction
{
    public const int MoraleMin = 0, MoraleMax = 100, MoraleDefault = 50;
    /// <summary>세력 사기 0~100 (기본 50 = 중립). 미션 시작 때만 World.SetFactionMorale 로 설정.</summary>
    public int Morale = MoraleDefault;
}

public sealed partial class World
{
    /// <summary>미션 · 캠페인 진입점: 세력 사기를 정한다 (0~100 으로 자름). 틱 0 전 설정 전용 — 전투 중 변경 명령은 없다. 리플레이는 머리말 Morale 로 재현.</summary>
    public void SetFactionMorale(int faction, int morale)
    {
        if (faction <= 0 || faction >= Factions.Count) throw new ArgumentOutOfRangeException(nameof(faction));
        Faction(faction).Morale = Math.Clamp(morale, Core.Faction.MoraleMin, Core.Faction.MoraleMax);
    }

    /// <summary>사기에 따른 제압 하락 속도 배수. 50 → 1 (정확히), 0 → at0, 100 → at100, 사이는 선형.</summary>
    public static float MoraleSuppressDecayMul(int morale, CombatRulesDef c)
    {
        int m = Math.Clamp(morale, Core.Faction.MoraleMin, Core.Faction.MoraleMax);
        if (m == Core.Faction.MoraleDefault) return 1f;
        return m < Core.Faction.MoraleDefault
            ? c.MoraleSuppressDecayAt0 + (1f - c.MoraleSuppressDecayAt0) * m / Core.Faction.MoraleDefault
            : 1f + (c.MoraleSuppressDecayAt100 - 1f) * (m - Core.Faction.MoraleDefault) / (Core.Faction.MoraleMax - Core.Faction.MoraleDefault);
    }
}
