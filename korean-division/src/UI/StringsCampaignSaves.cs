namespace KoreanDivision.UI;

/// <summary>캠페인 저장 슬롯 · 자동 저장 · 저장 실패 재시도 문자열 (T19a). S 의 일부.</summary>
public static partial class S
{
    public const string CmTopSaves = "저장 / 불러오기";
    public const string CmSlotsTitle = "캠페인 저장 (슬롯 9칸 · 자동 저장)";
    public const string CmSlotName = "슬롯 {0}";
    public const string CmSlotEmpty = "(비어 있음)";
    public const string CmSlotBroken = "읽을 수 없음: {0}";
    public const string CmSlotSummary = "{0:MM-dd HH:mm}  ·  다음 {1}  ·  끝낸 작전 {2}  ·  보급 {3:N0}";
    public const string CmAutoPreOp = "자동 — 작전 직전 (재도전)";
    public const string CmAutoSettle = "자동 — 정산 직후";
    public const string CmSlotSave = "저장";
    public const string CmSlotOverwrite = "덮어쓰기?";
    public const string CmSlotLoad = "불러오기";
    public const string CmSlotLoadConfirm = "불러올까요?";
    public const string CmSlotClose = "닫기";
    public const string CmSlotSaved = "슬롯 {0} 에 저장했습니다.";
    public const string CmSlotLoaded = "불러왔습니다: {0}";
    public const string CmSlotNoSaveBroken = "진행 저장을 읽지 못해 슬롯 저장은 잠겨 있습니다 (불러오기는 가능 — 원본은 .bak 로 보존).";
    public const string CmSaveFailedTitle = "저장 실패";
    public const string CmSaveFailedText = "캠페인을 저장하지 못했습니다 (디스크 · 권한 확인). 이전 저장 파일은 그대로입니다.";
    public const string CmSaveRetry = "다시 시도";
    public const string CmSaveGiveUp = "닫기 (저장 안 함)";
    public const string CmSaveFailedResult = "캠페인 저장 실패 — 다시 시도";
}
