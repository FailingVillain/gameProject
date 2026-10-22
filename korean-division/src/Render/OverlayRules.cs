using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// 머리 위 표시 가시성 규칙 (T38m, 순수 함수 · 렌더 전용 — 시뮬 · 저장 · 체크섬과 무관).
/// 간결 = 선택 · 마우스오버 · 방금 피격 · 위험 상태만. 보통 = 예전 동작. 상세 = 아군 체력 · 탄약 · 적재량 상시.
/// </summary>
public static class OverlayRules
{
    /// <summary>체력 막대: 선택 · 마우스오버 · 최근 피격은 모든 단계, 상세는 아군 상시.</summary>
    public static bool ShowHp(OverlayLevel l, bool selected, bool hovered, bool recentlyDamaged, bool own) =>
        selected || hovered || recentlyDamaged || (l == OverlayLevel.Detailed && own);

    /// <summary>탄약 · 적재량 막대 (아군만): 간결 = 선택만, 보통 = 선택 또는 부족, 상세 = 항상.</summary>
    public static bool ShowAmmoCargo(OverlayLevel l, bool selected, bool own, bool low) =>
        own && l switch { OverlayLevel.Concise => selected, OverlayLevel.Normal => selected || low, _ => true };

    /// <summary>색각 대응 세력 도형 (유닛 머리 위): 간결 = 선택 · 마우스오버만 (색 구분은 발밑 고리 · 군복이 대신), 그 밖 단계 = 항상.</summary>
    public static bool ShowTeamMarker(OverlayLevel l, bool selected, bool hovered) =>
        l != OverlayLevel.Concise || selected || hovered;

    /// <summary>진행 막대 (거치 · 방열 · 철수): 동작이 진행 중일 때만 (모든 단계).</summary>
    public static bool ShowProgress(OverlayLevel l, bool running) => running;

    /// <summary>분대 상태 아이콘: 간결 = 선택한 분대이거나 위험 상태 (고정 · 제압 · 후퇴 · 탄약 없음/부족) 만, 그 밖 단계 = 모든 상태.</summary>
    public static bool ShowSquadIcon(OverlayLevel l, SquadIcons.Status st, bool squadSelected) =>
        st != SquadIcons.Status.None && (l != OverlayLevel.Concise || squadSelected || IsUrgent(st));

    public static bool IsUrgent(SquadIcons.Status st) =>
        st is SquadIcons.Status.Pinned or SquadIcons.Status.Suppressed or SquadIcons.Status.Retreating or SquadIcons.Status.OutOfAmmo or SquadIcons.Status.LowAmmo;
}
