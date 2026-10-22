using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>단축키 표 (콘솔 `keys` 와 docs/CONTROLS.md 의 근거). 카드 키는 Commands.UnitCommands 에서 생성해 어긋나지 않는다.</summary>
public static class Bindings
{
    /// <summary>카드 밖 키 (Game.HandleKeyboard / HandleGlobalKeys 기준).</summary>
    public static readonly (string Key, string Text)[] Extra =
    {
        ("F", "돌격 사격 (중대 토글)"), ("K", "사격 통제 (자유/금지/응사)"), ("L", "대열 정비"), ("O", "포격 종류"),
        ("J", "집결 지점 (전역)"), ("W", "참호 돌격 (전역)"), ("G", "중대 병합 (Ctrl+G 격자)"),
        ("Tab", "명령/전투 카드 탭"), (".", "카드 쪽 넘김 (명령이 9개 넘을 때)"), ("Space", "선택 부대로 이동"), ("Backspace", "최근 교전 알림 위치로 이동"),
        ("1~9", "연대 선택"), ("Alt+1~9", "대대 선택 (0 = 목록)"),
        ("Ctrl+Alt+1~9", "중대 편성 → 대대"), ("Ctrl+Shift+1~9", "대대 → 연대 편입"), ("Ctrl+1~9", "대대 번호 변경"),
        ("I", "분대 아이콘"), ("U", "머리 위 표시 단계 (간결/보통/상세)"), ("B", "사선 (누르는 동안)"), ("N", "소리"), ("[ ]", "게임 속도"), ("+ -", "확대/축소"),
        ("F1", "도움말 (이 목록)"), ("F2", "병종 도감 (전투 중 · 열려 있는 동안 정지)"), ("F3", "일시정지"), ("F6/F7", "빠른 저장/불러오기"), ("Enter", "콘솔"), ("Esc", "취소/선택 해제"),
    };

    /// <summary>표시용 일람 — 카드 키는 사용자 재지정(UserSettings.KeyMap)을 반영한다. 도움말 · 콘솔 `keys` 가 쓴다.</summary>
    public static IEnumerable<(string Key, string Text)> All() =>
        KoreanDivision.Core.Commands.UnitCommands.Select(c => (KeyOf(c).ToString(), c.Label)).Concat(Extra);

    /// <summary>기본 키만의 일람 — docs/CONTROLS.md 가 적는 것은 기본값이다 (문서 동기 테스트용).</summary>
    public static IEnumerable<(string Key, string Text)> AllDefaults() =>
        KoreanDivision.Core.Commands.UnitCommands.Select(c => (c.Hotkey.ToString(), c.Label)).Concat(Extra);

    // ---------------------------------------------------------------- 재지정 (T38a) — 대상 = 카드 명령 키 (A~Z). Extra 는 고정.

    /// <summary>명령의 현재 키 (재지정이 있으면 그것, 없으면 기본).</summary>
    public static char KeyOf(KoreanDivision.Core.CommandDef c) =>
        UserSettings.KeyMap.TryGetValue(c.Key, out var k) ? k : c.Hotkey;

    public static KeyboardKey RaylibKeyOf(KoreanDivision.Core.CommandDef c) => KoreanDivision.Core.Commands.Key(KeyOf(c));

    /// <summary>카드 밖 고정 키 중 한 글자 문자 키 (재지정 대상이 쓸 수 없음).</summary>
    public static bool IsReservedLetter(char ch) =>
        char.ToUpperInvariant(ch) == 'R' ||   // R = 미션 중 (GameMission) 취소/강제 해제용 고정 키 — 표에는 없다
        Extra.Any(e => e.Key.Length == 1 && char.ToUpperInvariant(e.Key[0]) == char.ToUpperInvariant(ch));

    /// <summary>ch 를 cmdKey 에 지정할 수 없는 이유 (가능하면 null). 거부 표시용 문구.</summary>
    public static string? Conflict(string cmdKey, char ch)
    {
        ch = char.ToUpperInvariant(ch);
        if (ch < 'A' || ch > 'Z') return S.KeysErrLetter;
        if (IsReservedLetter(ch)) return string.Format(S.KeysErrReserved, ch, Extra.FirstOrDefault(e => e.Key.Length == 1 && char.ToUpperInvariant(e.Key[0]) == ch).Text ?? "R");
        foreach (var c in KoreanDivision.Core.Commands.UnitCommands)
            if (c.Key != cmdKey && KeyOf(c) == ch) return string.Format(S.KeysErrUsed, ch, c.Label);
        return null;
    }

    /// <summary>재지정 시도 — 충돌이면 거부하고 이유를 돌려준다. 기본값과 같으면 재지정을 지운다.</summary>
    public static string? TrySet(string cmdKey, char ch)
    {
        var def = KoreanDivision.Core.Commands.Get(cmdKey);
        if (def == null) return S.KeysErrLetter;
        ch = char.ToUpperInvariant(ch);
        var err = Conflict(cmdKey, ch);
        if (err != null) return err;
        if (ch == def.Hotkey) UserSettings.KeyMap.Remove(cmdKey); else UserSettings.KeyMap[cmdKey] = ch;
        return null;
    }

    /// <summary>손으로 고친 settings.json 방어: 글자 아님 · 고정 키 · 중복인 재지정을 버린다 (기본 키가 이기고, 재지정끼리는 명령 Key 순).</summary>
    public static void Sanitize()
    {
        foreach (var (cmd, ch) in UserSettings.KeyMap.ToList())
            if (ch < 'A' || ch > 'Z' || IsReservedLetter(ch) || KoreanDivision.Core.Commands.Get(cmd)!.Hotkey == ch) UserSettings.KeyMap.Remove(cmd);
        var defaults = KoreanDivision.Core.Commands.UnitCommands.Where(c => !UserSettings.KeyMap.ContainsKey(c.Key)).Select(c => c.Hotkey).ToHashSet();
        var taken = new HashSet<char>(defaults);
        foreach (var cmd in UserSettings.KeyMap.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
            if (!taken.Add(UserSettings.KeyMap[cmd])) UserSettings.KeyMap.Remove(cmd);
        // 재지정을 버리면 그 명령의 기본 키가 되돌아와 다른 키와 겹칠 수 있다 — 겹치면 그 재지정도 버린다 (수렴할 때까지)
        for (bool again = true; again;)
        {
            again = false;
            var seen = new HashSet<char>();
            foreach (var c in KoreanDivision.Core.Commands.UnitCommands.OrderBy(c => UserSettings.KeyMap.ContainsKey(c.Key) ? 1 : 0))
                if (!seen.Add(KeyOf(c)) && UserSettings.KeyMap.Remove(c.Key)) { again = true; break; }
        }
    }

    public static void ResetAll() => UserSettings.KeyMap.Clear();

    /// <summary>docs/CONTROLS.md 「단축키 일람」 표의 한 줄 형식 (문서 동기 테스트와 문서 갱신 공용).</summary>
    public static string MarkdownRow((string Key, string Text) b) => $"| `{b.Key}` | {b.Text} |";
}
