namespace KoreanDivision.UI;

/// <summary>단축키 표 (콘솔 `keys` 와 docs/CONTROLS.md 의 근거). 카드 키는 Commands.UnitCommands 에서 생성해 어긋나지 않는다.</summary>
public static class Bindings
{
    /// <summary>카드 밖 키 (Game.HandleKeyboard / HandleGlobalKeys 기준).</summary>
    public static readonly (string Key, string Text)[] Extra =
    {
        ("F", "돌격 사격 (중대 토글)"), ("K", "사격 통제 (자유/금지/응사)"), ("L", "대열 정비"), ("O", "포격 종류"),
        ("J", "집결 지점 (전역)"), ("W", "참호 돌격 (전역)"), ("G", "중대 병합 (Ctrl+G 격자)"),
        ("Tab", "명령/전투 카드 탭"), ("Space", "선택 부대로 이동"),
        ("1~9", "연대 선택"), ("Alt+1~9", "대대 선택 (0 = 목록)"),
        ("Ctrl+Alt+1~9", "중대 편성 → 대대"), ("Ctrl+Shift+1~9", "대대 → 연대 편입"), ("Ctrl+1~9", "대대 번호 변경"),
        ("I", "분대 아이콘"), ("B", "사선 (누르는 동안)"), ("N", "소리"), ("[ ]", "게임 속도"), ("+ -", "확대/축소"),
        ("F3", "일시정지"), ("F6/F7", "빠른 저장/불러오기"), ("Enter", "콘솔"), ("Esc", "취소/선택 해제"),
    };

    public static IEnumerable<(string Key, string Text)> All() =>
        KoreanDivision.Core.Commands.UnitCommands.Select(c => (c.Hotkey.ToString(), c.Label)).Concat(Extra);
}
