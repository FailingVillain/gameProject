namespace KoreanDivision.UI;

/// <summary>미션 틀 (미션 선택 · 브리핑 · 배치 · 미션 HUD · 결과) 의 한글 문자열. S 의 일부 (폰트 아틀라스에 같이 들어간다).</summary>
public static partial class S
{
    // 시작 화면 · 미션 선택
    public const string MenuMissions = "미션";
    public const string MsSelectTitle = "미션 선택";
    public const string MsNone = "미션 파일이 없습니다 (assets/missions)";
    public const string MsBroken = "읽을 수 없는 미션 파일";
    public const string MsBriefing = "브리핑";
    public const string MsBack = "뒤로";
    public const string MsDeployStart = "배치 시작";
    public const string MsQuickStart = "바로 시작";
    // 브리핑
    public const string MsObjectives = "목표";
    public const string MsPrimary = "주 목표";
    public const string MsSecondary = "부 목표";
    public const string MsHiddenObj = "(숨은 목표가 더 있을 수 있음)";
    public const string MsFailures = "패배 조건";
    public const string MsForces = "병력";
    public const string MsOwnForces = "아군";
    public const string MsEnemyForces = "적군 (첩보)";
    public const string MsUnitsCount = "{0}기";
    public const string MsWeatherTime = "날씨 {0} · 시간 {1}";
    public const string MsLegend = "파랑 = 배치 구역 · 노랑 = 목표 구역 · 점 = 부대";
    // 목표 · 패배 조건 기본 문구
    public const string ObjCapture = "{0} 확보 ({1}초 유지)";
    public const string ObjEliminate = "적 {0}% 섬멸";
    public const string ObjEliminateTag = "표적 '{0}' 제거";
    public const string ObjDefend = "{0}초 동안 버티기";
    public const string ObjDefendArea = "{1} 을(를) {0}초 동안 지키기";
    public const string ObjEscort = "'{0}' {1}기를 {2} 까지 호위";
    // T30c 화물열차 목표 (train)
    public const string ObjTrainEscort = "화물열차 '{0}' 화차 {1}량을 {2} 까지 호송";
    public const string ObjTrainCapture = "화물열차 '{0}' 화차 탈취 ({1}초 확보)";
    public const string ObjTrainDestroy = "화물열차 '{0}' 화차 파괴";
    public const string ObjTrainDestroyPct = "화물열차 '{0}' 화차 {1}% 파괴";
    public const string RsTrains = "열차 손실 (객차 · 화차)";
    public const string RsTrainLine = "'{0}'  객차 {1}/{2} · 화차 {3}/{4}";
    public const string FailLosses = "아군 손실 {0}% 이상";
    public const string FailKeyUnit = "'{0}' 전사";
    public const string FailTimeLimit = "{0} 안에 주 목표 미달성";
    public const string ObjStateDone = "완료";
    public const string ObjStateFailed = "실패";
    public const string ObjStateActive = "진행 중";
    // 배치 단계
    public const string DpTitle = "배치 단계";
    public const string DpHint = "부대를 끌어 배치 구역 (파란 틀) 안에 놓으세요 · R / 우클릭 = 45° 회전 · 휠 · 방향키 = 화면 이동";
    public const string DpStart = "작전 개시 (Enter)";
    public const string DpOutside = "배치 구역 밖에는 놓을 수 없습니다";
    public const string DpMoved = "{0} 배치";
    public const string DpLocked = "고정 부대 (옮길 수 없음)";
    // 미션 HUD
    public const string MsHudTime = "경과 {0}";
    public const string MsHudLeft = "남은 시간 {0}";
    public const string MsReinforce = "증원 도착: {0}";
    public const string MsNewObjective = "새 목표: {0}";
    public const string MsHudCapture = "{0:0}/{1:0}초";
    public const string MsHudEliminate = "{0:0}%";
    public const string MsHudEscort = "{0}/{1}";
    // 결과
    public const string RsVictory = "미션 성공";
    public const string RsDefeat = "미션 실패";
    public const string RsDuration = "작전 시간";
    public const string RsLosses = "손실 (생성 / 손실)";
    public const string RsCauses = "사상 원인 (아군 / 적)";
    public const string RsCauseFmt = "{0} 사격 {1}% · 근접 {2}% · 포격 {3}%";
    public const string RsCompanies = "중대별 (잔존 / 편성 · 사살)";
    public const string RsCompanyFmt = "제{0}중대  {1} / {2}  · {3}";
    public const string RsKills = "적 사살";
    public const string RsTopUnits = "최다 사살 부대원";
    public const string RsPromotions = "승진";
    public const string RsPromotionsN = "{0}명 승진";
    public const string RsObjectives = "목표 결과";
    public const string RsSaved = "결과 저장: {0}";
    public const string RsRetry = "다시 하기 (Enter)";
    public const string RsMenu = "나가기";
    public const string RsFailReason = "실패 사유: {0}";
    public const string RsNone = "없음";
    public const string RsCheat = "(치트 사용)";
}
