namespace KoreanDivision.UI;

/// <summary>[T48d] 탭 8 참모진 (연대장 · 대대장/참모 · 후보 풀 · 학파) · 탭 9 종착 배정 · 연표 문구. 수치는 데이터/규칙 함수에서 오고 여기엔 문구만.</summary>
public static partial class S
{
    public const string CmdTitle = "8. 참모진 - 인사 카드";
    public const string CmdSubRegiment = "연대장", CmdSubStaff = "대대장 · 참모", CmdSubPool = "후보 풀", CmdSubSchool = "학파";
    public const string CmdRulesOff = "연대장 규칙이 꺼져 있습니다 (연대장 명단 없음) - 학파 목록만 보여 줍니다.";
    public const string CmdBetweenOnly = "작전 사이(홈)에서만 기용 · 해임 · 배정할 수 있습니다.";
    public const string CmdRegimentN = "연대 {0}";
    public const string CmdVacant = "(공석)";
    public const string CmdBtnHire = "기용", CmdBtnDismiss = "해임", CmdBtnDetail = "상세", CmdBtnYes = "확인", CmdBtnNo = "취소";
    public const string CmdStamp = "해임 불가";
    public const string CmdFavor = "호감";
    public const string CmdFavorLine = "{0} ({1})";
    public const string CmdUnknown = "(알 수 없음)", CmdLockedName = "???", CmdNone = "없음";
    public const string CmdStHired = "재직 (연대 {0})", CmdStAvailable = "기용 가능", CmdStDismissed = "해임됨", CmdStUnavailable = "불가", CmdStLocked = "잠김 ???";
    public const string CmdFilterAll = "전체", CmdFilterLabel = "상태";
    public const string CmdPoolEmpty = "(해당하는 인물이 없습니다)";
    public const string CmdMore = "아래로 {0}명 더 (마우스 휠)";
    public const string CmdDetailBio = "약력", CmdDetailLikes = "선호", CmdDetailDislikes = "기피", CmdDetailCond = "조건", CmdDetailCost = "비용 {0}", CmdDetailTraits = "특성";
    public const string CmdDetailPick = "카드를 눌러 상세를 봅니다.";
    public const string CmdHireInto = "기용 연대";
    public const string CmdNoVacant = "(빈 연대 없음)";
    public const string CmdHireAsk = "{0} 을(를) {1}연대장으로 기용합니다.";
    public const string CmdDismissAsk = "{0} 연대장({1}연대)을 해임합니다.";
    public const string CmdDismissPenalty = "해임되면 본인의 호감이 {0} 떨어집니다 (되돌릴 수 없음).";
    public const string CmdDismissNoPenalty = "호감 변화는 없습니다.";
    public const string CmdNotice = "알림: {0}";
    public const string CmdBattleLine = "전투 영향: {0}";
    // 대대장 · 참모
    public const string CmdStaffHead = "사단 참모 (슬롯당 1명, 즉시 적용 - 임시 수치)";
    public const string CmdBatHead = "대대장 (대대당 1명)";
    public const string CmdTermHead = "종착 대기 장교";
    public const string CmdSlotSupply = "보급", CmdSlotIntel = "정보", CmdSlotResearch = "연구", CmdSlotDoctrine = "학파";
    public const string CmdEffSupply = "정기 보급 +{0}%", CmdEffIntel = "적 정보 +{0}줄", CmdEffResearch = "연구 속도 +{0}%", CmdEffDoctrine = "학파 보정 +{0}% ({1})";
    public const string CmdSlotEmpty = "(비어 있음)", CmdBatEmpty = "(대대장 없음)";
    public const string CmdOfficerDetail = "{0} · 경험치 {1} · 학파 {2}", CmdWoundedUntil = "부상 (복귀 {0}작전째)";
    public const string CmdBtnBattalion = "대대장으로", CmdBtnStaff = "참모로", CmdBtnUnassign = "해제";
    public const string CmdTermNone = "(종착 대기 장교 없음)";
    public const string CmdWhereStaff = "참모({0})";
    public const string CmdTermWhere = "현재: {0}";
    public const string CmdBatRow = "제{0}대대";
    public const string CmdNoBattalions = "(대대가 없습니다)";
    // 연표 · 일지 줄
    public const string CmdTlHire = "연대장 {0} 기용 (연대 {1})", CmdTlDismiss = "연대장 {0} 해임 (연대 {1})", CmdTlDead = "대대장 {0} 전사", CmdTlWounded = "대대장 {0} 부상 (복귀 {1}작전째)";
    public const string CmdTlAssignBat = "{0} 제{1}대대장 배정", CmdTlAssignStaff = "{0} 참모({1}) 배정", CmdTlAssign = "{0} 배정", CmdTlUnassign = "{0} 배정 해제";
    public const string CmdRecHead = "인사 일지 (최근 {0}건)";
    public const string CmdRecNone = "(인사 기록 없음)";
    // 탭 9
    public const string CmdPromoTermHead = "종착 배정 대기 (중령 도달)";
    public const string CmdPromoBtnBat = "대대장", CmdPromoBtnStaff = "참모";
}
