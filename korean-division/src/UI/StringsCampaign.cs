namespace KoreanDivision.UI;

/// <summary>캠페인 (시작 화면 캠페인 페이지 · 캠페인 홈 UI · 결과 화면 저장 알림) 한글 문자열. S 의 일부.</summary>
public static partial class S
{
    // ---- 기존 최소판 상수 (호환성 유지)
    public const string MenuCampaign = "캠페인";
    public const string CmTitle = "캠페인 — 간도 (1920)";
    public const string CmNewTitle = "새 캠페인";
    public const string CmOverwriteWarn = "새로 시작하면 이 캠페인 저장은 덮어쓰입니다.";
    public const string CmNewText = "1920년 4월, 제1사단 북진. 참장이 되어 회령에서 첫 밤을 맞는다.";
    public const string CmStart = "1-1 회령 초소 시작";
    public const string CmRetry = "{0} 다시 하기";
    public const string CmCompleted = "끝낸 미션";
    public const string CmHistory = "{0}  ·  {1}  ·  사살 {2}  ·  손실 {3}  ·  생존 {4}";
    public const string CmCarry = "이월 병력: 사병 {0}명 · 장교 {1}명";
    public const string CmNext = "다음 미션: {0} (준비 중)";
    public const string CmNextReady = "다음 미션: {0}";
    public const string CmNextStart = "다음 미션 시작";
    public const string CmHowitzer = "편입 부대: 곡사포 중대 (정식)";
    public const string CmReset = "캠페인 초기화";
    public const string CmResetConfirm = "정말 초기화? (한 번 더)";
    public const string CmSaved = "캠페인 저장됨 (생존 병력 이월)";
    public const string CmBack = "뒤로";

    // ---- 상단 바 및 프레임
    public const string CmDivisionName = "대한제국 제17사단 '대한'";
    public const string CmTopSupply = "보급: {0:N0}";
    public const string CmTopManpower = "병력: {0:N0} / 10,000";
    public const string CmTopRecruits = "신병: {0:N0} / {1:N0}";
    public const string CmTopWounded = "부상: {0:N0}";
    public const string CmTopNextOp = "차기 작전: {0}";
    public const string CmTopEvents = "본국 전문: {0}건";
    public const string CmTopDebriefBtn = "[직전 전과 보고]";

    // ---- 좌측 5개 탭
    public const string CmTabSummary = "[요약]";
    public const string CmTabRoster = "[부대]";
    public const string CmTabLogistics = "[병참]";
    public const string CmTabMap = "[지도]";
    public const string CmTabEvents = "[본국]";
    public const string CmBtnOpStart = "[작전 개시]";
    public const string CmBtnOpPrepare = "[출격 준비]";

    // ---- 1. 요약 탭 (일일 보고서)
    public const string CmSumTitle = "사단 일일 보고서";
    public const string CmSumSectionState = "사단 병력 및 보급 현황";
    public const string CmSumSectionPolitics = "지휘부 및 황실 동향";
    public const string CmSumSectionEvent = "최신 본국 전갈 요약";
    public const string CmSumSectionHistory = "역대 작전 일지";
    public const string CmSumRifleRate = "소총 충족률: {0}% ({1:N0} / {2:N0})";
    public const string CmSumNoEvents = "도착한 미확인 전문이 없습니다.";
    public const string CmSumNoHistory = "아직 수행한 작전이 없습니다.";
    public const string CmSumSupplyPenalty = "총사령부 보급 패널티: -{0}%";
    public const string CmSumRecruitPenalty = "황손 신병 패널티: -{0}%";

    // 황손 심기 대사 단서
    public const string CmHeirMood80 = "황손 전하의 신임과 기대가 각별하여 하사품이 넉넉합니다.";
    public const string CmHeirMood50 = "황손 전하께서는 사단의 동향을 무난히 지켜보고 계십니다.";
    public const string CmHeirMood30 = "황손 전하의 심기가 편치 않으니 각별한 주의가 요구됩니다.";
    public const string CmHeirMood0 = "황손 전하의 노여움이 극에 달해 신병 징집에 차질이 빚어지고 있습니다.";

    // 총사령관 신임 대사 단서
    public const string CmCmdFavor80 = "총사령관 민 대장의 두터운 신임을 받고 있어 보급 배분이 최우선입니다.";
    public const string CmCmdFavor50 = "총사령부와의 군사 교신은 정례적인 수준을 유지하고 있습니다.";
    public const string CmCmdFavor30 = "총사령관이 사단의 독자 행동에 불편한 기색을 감추지 않고 있습니다.";
    public const string CmCmdFavor0 = "총사령부의 깊은 불신으로 차기 정기 보급의 대폭 삭감이 경고되었습니다.";

    // ---- 2. 부대 탭 (부대 관리)
    public const string CmRosterTitle = "사단 부대 편성 및 신병 충원";
    public const string CmColBattalion = "소속 / 대대";
    public const string CmColCompany = "중대명";
    public const string CmColKind = "병종";
    public const string CmColStrength = "편성 인원";
    public const string CmColWounded = "부상";
    public const string CmColStatus = "상태 / 임무";
    public const string CmColAction = "신병 배치";

    public const string CmStatusAvailable = "투입 가능";
    public const string CmStatusUnavailable = "출전 불가";
    public const string CmStatusMandatory = "[의무 출격]";

    public const string CmBtnAssign100 = "[+100 배치]";
    public const string CmBtnAssignRem = "[정원 배치]";
    public const string CmAssignCostHint = "소모: 보급 {0:0.0} · 소총 {1}정";
    public const string CmAssignLackRes = "보급 또는 소총 부족";
    public const string CmAssignFull = "정원 충족 또는 신병 없음";

    // ---- 3. 병참 탭 (자원 및 치료)
    public const string CmLogTitle = "사단 병참 및 군수 재고";
    public const string CmSupplyTotal = "보급품 총량: {0:N0}";
    public const string CmRiflesTotal = "소총 총 재고: {0:N0}정";
    public const string CmRifleRu = "노식(러시아) 소총: {0:N0}정";
    public const string CmRifleJp = "일식(일본) 소총: {0:N0}정";
    public const string CmRifleDe = "독식(독일) 소총: {0:N0}정";
    public const string CmRifleFr = "불식(프랑스) 소총: {0:N0}정";
    public const string CmRifleJpOld = "구일식(구식) 소총: {0:N0}정";

    public const string CmAmmoSecTitle = "구경별 탄약 재고";
    public const string CmAmmoRu = "노식 소총/기관총탄: {0:N0}발";
    public const string CmAmmoJp = "일식 소총/기관총탄: {0:N0}발";
    public const string CmAmmoDe = "독식 소총탄: {0:N0}발";
    public const string CmAmmoFr = "불식 소총탄: {0:N0}발";
    public const string CmAmmoJpOld = "구일식 소총탄: {0:N0}발";

    public const string CmShellsSecTitle = "포탄 재고";
    public const string CmShellInfGun = "보병포탄: {0:N0}발";
    public const string CmShellField = "야전포탄: {0:N0}발";
    public const string CmShellHowitzer = "곡사포탄: {0:N0}발";
    public const string CmHorses = "군마: {0:N0}두";

    public const string CmHospitalSecTitle = "야전 병원 및 부상자 치료";
    public const string CmHospitalWait = "치료 대기 부상병: {0:N0}명 (비용: 1인당 보급 0.5)";
    public const string CmBtnHeal = "[부상자 전원 치료]";
    public const string CmHospitalNone = "치료 대기 중인 부상병이 없습니다.";
    public const string CmHospitalLackSupply = "치료에 필요한 보급품이 부족합니다.";
    public const string CmHospitalDone = "부상병 {0}명이 완치되어 복귀했습니다.";

    public const string CmRecentLogTitle = "최근 군수 및 보급 입출고 내역";
    public const string CmRecentLogEmpty = "최근 정산 기록이 없습니다.";

    // ---- 4. 지도 탭 (작전 지도)
    public const string CmMapTitle = "만주 북진 작전 지도 및 진로";
    public const string CmNodeAndong = "안동";
    public const string CmNodeFenghuang = "봉황성";
    public const string CmNodeAnshan = "안산";
    public const string CmNodeLiaoyang = "요양";
    public const string CmNodeFengtian = "봉천";
    public const string CmNodeChangchun = "장춘";
    public const string CmNodeHarbin = "하얼빈";
    public const string CmNodeHoeryong = "회령";
    public const string CmNodeTumen = "두만강";

    public const string CmMissionHoeryongName = "1-1 회령 초소";
    public const string CmMissionTumenName = "1-2 두만강 도하";
    public const string CmMissionC103Name = "1-3 미개척 전선";

    public const string CmMapStatusDone = "작전 완료";
    public const string CmMapStatusCurrent = "작전 준비 중";
    public const string CmMapStatusLocked = "차후 진로";

    // ---- 5. 본국 탭 (이벤트)
    public const string CmEventTitle = "본국 전문 및 군령";
    public const string CmEventSender = "발신: {0}";
    public const string CmEventVia = "경유: {0}";
    public const string CmEventBody = "전문 본문:";
    public const string CmEventChoices = "지휘관 결재:";
    public const string CmEventApplied = "결재 완료: {0}";
    public const string CmEventNone = "현재 접수된 미확인 전문이 없습니다.";
    public const string CmEventPreview = "예상 효과: {0}";

    // ---- 작전 전 화면 (S3 Pre-Operation)
    public const string CmPreTitle = "작전 출격 부대 편성 및 보급 검토";
    public const string CmPreBriefing = "작전 브리핑:";
    public const string CmPreDeploySelection = "투입 부대 선택:";
    public const string CmPreDeployCap = "투입 병력: {0}명 / 상한: {1}명";
    public const string CmPreBtnSelectAll = "[전체 선택]";
    public const string CmPreBtnDeselectAll = "[전체 해제]";
    public const string CmPreSupplyGauge = "소요 보급: {0:0.0} / 보유 보급: {1:0.0}";
    public const string CmPreBtnDeploy = "[작전 출격]";
    public const string CmPreBtnCancel = "[취소 / 뒤로]";

    public const string CmWarnOverSupply = "경고: 보유 보급품을 초과하여 부대를 투입할 수 없습니다!";
    public const string CmWarnOverCap = "경고: 작전 투입 상한 병력을 초과하였습니다!";
    public const string CmWarnNoSelection = "경고: 투입할 부대를 최소 1개 이상 선택하십시오.";
    public const string CmWarnMandatoryRefused = "경고: 황손 시위대 의무 투입을 제외하면 황손 심기 하락 및 보급 불이익이 발생합니다!";
    public const string CmWarnAmmoShortage = "경고: 탄약 재고 부족으로 기본 휴대량이 감축({0}%)되어 지급됩니다.";

    // ---- 작전 후 전과 보고 모달 (S4 Debrief)
    public const string CmDebriefTitle = "작전 전과 보고 및 군수 정산";
    public const string CmDebriefLosses = "아군 사상자: {0}명 (전사 {1}명 · 부상 복귀 {2}명 · 미치료 전역 {3}명)";
    public const string CmDebriefKills = "적군 사살: {0}명";
    public const string CmDebriefLoot = "노획 전리품: 보급 +{0:N0} · 소총 +{1:N0}정 · 탄약 +{2:N0}발 · 군마 +{3:N0}두";
    public const string CmDebriefAmmoRefund = "미소비 탄약 군수 환원: +{0:N0}발";
    public const string CmDebriefRegular = "본국 정기 보급 도착: 보급 +{0:N0} · 신병 +{1:N0}명";
    public const string CmDebriefSupplyChange = "사단 보급고 정산: {0:N0} → {1:N0}";
    public const string CmDebriefNewEvent = "통신 참모: 새로운 본국 전문이 접수되었습니다! ([본국] 탭 확인)";
    public const string CmDebriefClose = "[홈으로 (확인)]";

    // ---- 폰트 아틀라스용 이벤트 대사 및 인명/직책 글리프 포함용 상수
    public const string EvAuthorHeir = "황손 이태";
    public const string EvAuthorCmd = "총사령관 민 대장";
    public const string EvAuthorCorpse = "제2군단장 황자 이석";
    public const string EvAuthorGuard = "시위대장";
    public const string EvAuthorKim = "참모장 김주현";
    public const string EvAuthorNam = "11사단장 남 참장";

    public const string EvE1Title = "시위대 전진 배치";
    public const string EvE1Sender = "황손 이태 (시위대장 배서)";
    public const string EvE1Body = "황손 전하 하명. 시위대 1개 중대를 선봉에 세워 전공을 올리게 하라. 시위대장 배서.";
    public const string EvE1ChoiceA = "따름: 다음 작전에 시위대 중대 의무 포함, 심기 상승 및 하사품 보급 획득";
    public const string EvE1ChoiceB = "설득: 참모장 김주현 쪽지 '형님, 후위로 돌리심이…' 절충 시도";
    public const string EvE1ChoiceC = "무시: 전선의 냉혹함을 이유로 하명을 반려합니다.";

    public const string EvE2Title = "공적 가로채기";
    public const string EvE2Sender = "총사령관 민 대장 (제2군단장 황자 이석 경유)";
    public const string EvE2Body = "17사단의 전과는 군단 지휘 하의 성과로 보고함. 훈장 상신은 본관이 정리. 민.";
    public const string EvE2ChoiceA = "수용: 총사령부의 공적 독점을 묵인하여 신임을 얻습니다.";
    public const string EvE2ChoiceB = "항의: 부하들의 피땀 어린 전과를 지키기 위해 정식 항의합니다.";
    public const string EvE2ChoiceC = "11사단장 남 참장에게 군단의 전횡을 알리고 연대를 타진합니다.";

    public const string EvE3Title = "출혈 강요";
    public const string EvE3Sender = "총사령관 민 대장";
    public const string EvE3Body = "금번 작전 기한 내 필히 확보. 손실은 불문. 증원 불가.";
    public const string EvE3ChoiceA = "수락: 무리한 일정이지만 결사 공세를 감행합니다.";
    public const string EvE3ChoiceB = "사정 상신: 병사들의 무의미한 희생을 막기 위해 연기를 청원합니다.";
    public const string EvE3ChoiceC = "참모장 건의: 최소한의 희생으로 목표를 선별 타격합니다.";

    public const string EvE4Title = "황손의 훈수";
    public const string EvE4Sender = "황손 이태";
    public const string EvE4Body = "본관의 방침과 다르오. 사단장은 황실의 체면을 아시오.";
    public const string EvE4ChoiceA = "사과: 황손의 권위를 세워드리고 심기를 달랩니다.";
    public const string EvE4ChoiceB = "타협: 시위대 소수를 의장대로 동행시키는 방안을 제시합니다.";
    public const string EvE4ChoiceC = "무시: 군령권은 전선 지휘관에게 있음을 분명히 합니다.";

    public const string EvE5Title = "군단장의 만류";
    public const string EvE5Sender = "제2군단장 황자 이석";
    public const string EvE5Body = "무리한 공세는 삼가시오. 방어선을 굳히는 것이 상책이오.";
    public const string EvE5ChoiceA = "수용: 군단장의 방침을 받들어 전선을 공고히 다집니다.";
    public const string EvE5ChoiceB = "공세 유지: 적의 취약점을 파고들어 공세 주도권을 장악합니다.";
}
