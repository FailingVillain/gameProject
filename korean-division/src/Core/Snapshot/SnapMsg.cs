namespace KoreanDivision.Core;

/// <summary>
/// 저장 / 불러오기 오류 메시지의 한글 틀. 화면에 그대로 표시되므로 (불러오기 목록 · 알림) 한글 글자가 폰트 아틀라스에 들어가야 한다 —
/// S 와 마찬가지로 const 로만 두고, Ui 가 이 클래스의 const 글자도 아틀라스에 넣는다. 기술적 세부는 {0} 자리에 영문으로 넣는다.
/// </summary>
public static class SnapMsg
{
    public const string Truncated = "파일이 중간에 끊겼습니다";
    public const string NotSave = "게임 저장 파일이 아닙니다";
    /// <summary>저장 형식 호환 정책 (ENGINE_NOTES '저장 호환'): 출시 전에는 다른 형식의 저장을 열지 않고 이유만 알린다.</summary>
    public const string OldFormat = "이 저장은 이전 버전에서 만들어져 열 수 없습니다 (저장 형식 v{0} · 현재 v{1})";
    public const string NewFormat = "이 저장은 더 새로운 버전에서 만들어져 열 수 없습니다 (저장 형식 v{0} · 현재 v{1})";
    public const string ReplayOldFormat = "이 리플레이는 이전 버전에서 만들어져 열 수 없습니다 (리플레이 형식 v{0} · 현재 v{1})";
    public const string ReplayNewFormat = "이 리플레이는 더 새로운 버전에서 만들어져 열 수 없습니다 (리플레이 형식 v{0} · 현재 v{1})";
    public const string CampaignNewer = "캠페인 저장이 더 새로운 버전에서 만들어져 열 수 없습니다 (저장 스키마 v{0} · 현재 v{1})";
    public const string CampaignBroken = "캠페인 저장이 손상되었거나 올바른 형식이 아니라 열 수 없습니다";
    public const string ReplayBroken = "리플레이 파일이 손상되었거나 올바른 형식이 아닙니다";
    public const string ReplayStartBroken = "리플레이의 시작 저장 지점을 읽을 수 없습니다: {0}";
    public const string BadHeader = "머리말이 깨졌습니다";
    public const string BadPack = "압축 본문이 깨졌습니다";
    public const string BadSum = "본문 검사값이 맞지 않습니다 (파일이 손상됨)";
    public const string Trailing = "본문 뒤에 남는 데이터가 있습니다";
    public const string BadChecksum = "복원한 상태의 체크섬이 저장 시점과 다릅니다 ({0} ≠ {1})";
    public const string CantRead = "파일을 읽을 수 없습니다: {0}";
    public const string ParseError = "저장 파일을 읽는 중 오류: {0}: {1}";
    /// <summary>구조 · 값 오류 (세부는 영문).</summary>
    public const string Invalid = "저장 데이터가 올바르지 않습니다 ({0})";
    /// <summary>밸런스에 없는 항목 — balance.json 이 저장 당시와 달라진 경우.</summary>
    public const string MissingDef = "밸런스 파일에 없는 {0} '{1}' (balance.json 이 바뀜)";
    public const string DefBuilding = "건물";
    public const string DefUnit = "유닛";
    public const string DefWeapon = "무기";
    public const string DefArmor = "장갑";
    public const string ResourceMismatch = "자원 종류 수가 밸런스와 맞지 않습니다 (balance.json 이 바뀜)";
    public const string UnknownAi = "AI '{0}' 은(는) 등록된 AI 가 아니라 저장할 수 없습니다";
    public const string UnsupportedType = "저장할 수 없는 형식 ({0})";
    public const string WarnVersion = "게임 버전이 다릅니다 (저장 {0} / 현재 {1}) — 진행이 원래와 달라질 수 있습니다";
    public const string WarnBalance = "밸런스 파일(balance.json)이 저장 당시와 다릅니다 — 불러오지만 이후 진행이 원래와 달라질 수 있습니다";
}
