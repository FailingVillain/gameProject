namespace KoreanDivision.UI;

/// <summary>캠페인 홈 3화면 · 15탭 · 연대 배속 · 보급 미리보기 문자열 (T19c, 설계 CAMPAIGN_HOME_DESIGN §2 · §11-1). S 의 일부.</summary>
public static partial class S
{
    // ---- 3화면 (Q1: 중앙 9 · 좌 3 · 우 3)
    public const string CmScreenLeft = "< 좌: 기록 · 정보";
    public const string CmScreenCenter = "중앙: 작전 준비";
    public const string CmScreenRight = "우: 인물 · 정치 >";

    // ---- 후속 탭 버튼 이름 (MVP 5탭은 StringsCampaign 의 CmTab*)
    public const string CmTabOrg = "[편제]";
    public const string CmTabEquip = "[장비]";
    public const string CmTabTraining = "[훈련]";
    public const string CmTabDiplomacy = "[외교]";
    public const string CmTabStaff = "[참모진]";
    public const string CmTabPromotion = "[진급]";
    public const string CmTabIntel = "[정보]";
    public const string CmTabRecords = "[기록]";
    public const string CmTabSchedule = "[일정]";
    public const string CmTabResearch = "[연구]";

    // ---- 후속 탭 자리 (틀만 — 본문은 후속 작업)
    public const string CmPlaceholderNote = "이 탭은 후속 작업에서 채웁니다 (MVP 범위 밖).";
    public const string CmPhOrgTitle = "3. 편제 관리";
    public const string CmPhOrgDesc = "분대 · 중대 편제 틀 (소대 · 여단 없음), 편제 틀 편집 · 자동 편성.";
    public const string CmPhEquipTitle = "5. 장비 관리";
    public const string CmPhEquipDesc = "장비 개량 · 도입 (본국 · 협력 세력). 연구 탭 해금과 연결.";
    public const string CmPhTrainingTitle = "6. 훈련";
    public const string CmPhTrainingDesc = "대대 숙련 (신병 → 정규 → 노병) 상향, 대가 = 시간 · 탄약.";
    public const string CmPhDiplomacyTitle = "7. 가벼운 외교";
    public const string CmPhDiplomacyDesc = "선택지 · 대민 지원 · 용병 고용 · 포로 처리 (송환 · 수용 · 편입).";
    public const string CmPhStaffTitle = "8. 참모진";
    public const string CmPhStaffDesc = "작전 참모 · 연대장 · 협력 세력 리더 (학파 · 성격) 인물 카드.";
    public const string CmPhPromotionTitle = "9. 진급 · 군령권";
    public const string CmPhPromotionDesc = "전과 보고에서 산출한 진급 후보 목록 · 군령권.";
    public const string CmPhIntelTitle = "11. 정보 (첩보)";
    public const string CmPhIntelDesc = "적 병력 추정 · 정찰 보고 목록.";
    public const string CmPhRecordsTitle = "12. 부대 기록";
    public const string CmPhRecordsDesc = "부대 일지 · 전사자 명부 · 훈장.";
    public const string CmPhScheduleTitle = "14. 일정 · 이벤트 기록";
    public const string CmPhScheduleDesc = "다음 작전까지 · 명령 기한 · 이벤트 이력 (시간 · 계절 규칙은 추후 설계).";
    public const string CmPhResearchTitle = "15. 연구";
    public const string CmPhResearchDesc = "진행 중 연구 1개 · 완료 목록 (세 갈래).";

    // ---- 2 신병 · 부대: 연대 배속 (Q9, 연대 ≤9)
    public const string CmColRegiment = "연대";
    public const string CmRegimentName = "{0}연대";
    public const string CmRegimentNone = "직할";
    public const string CmRegimentSummary = "연대 배속: {0}";
    public const string CmRegimentSummaryPart = "{0}연대 {1}개 중대";
    public const string CmRegimentSummaryDirect = "직할 {0}개 중대";
    public const string CmRegimentHint = "대대 중대는 대대 단위로 함께 배속 · 직할 중대는 하나씩";

    // ---- 4 병참: 보급 미리보기 (가용 중대 전원 투입 기준)
    public const string CmPreviewTitle = "다음 작전 보급 미리보기";
    public const string CmPreviewBasis = "{0} · 가용 {1}개 중대 · {2:N0}명 전원 기준";
    public const string CmPreviewSupply = "보급품 소모 {0:N0} → 잔고 {1:N0}";
    public const string CmPreviewSupplyLack = "보급품 소모 {0:N0} (잔고 {1:N0} 부족)";
    public const string CmPreviewLoad = "{0}: 필요 {1:N0} · 적재 {2:N0}발 (재고 {3:N0})";
    public const string CmPreviewNoGuns = "포탄: 이 작전에는 포가 없음";
    public const string CmPreviewNoAmmo = "탄약: 소총 · 기관총 중대 없음";

    /// <summary>재고 키 → 짧은 이름 (보급 미리보기 줄).</summary>
    public static string CmStockShort(string key) => key switch
    {
        "ru" => "노식탄", "jp" => "일식탄", "de" => "독식탄", "fr" => "불식탄", "jpOld" => "구일식탄",
        "infGun" => "보병포탄", "field" => "야전포탄", "howitzer" => "곡사포탄", _ => key
    };
}

public static partial class S
{
    // ---- 8 참모진 탭 (T19d, 읽기 전용: 장교 학파 · 인물)
    public const string CmStaffTitle = "8. 참모진 - 장교 학파";
    public const string CmStaffLoadFail = "학파 자료(doctrines.json)를 읽지 못했습니다.";
    public const string CmStaffSchoolHead = "{0} 학파";
    public const string CmStaffOfficer = "장교 기질";
    public const string CmStaffTactics = "전술";
    public const string CmStaffAi = "경향";
    public const string CmStaffResearch = "연구 연결";
    public const string CmStaffFigures = "소속 인물";
    public const string CmStaffNoFigures = "(아직 없음)";
    public const string CmStaffNote = "읽기 전용 - 학파는 표시와 연구 보정 근거로만 쓰이며 전투 AI 에는 아직 적용되지 않습니다.";
}
