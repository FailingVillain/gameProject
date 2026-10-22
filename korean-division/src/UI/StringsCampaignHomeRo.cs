namespace KoreanDivision.UI;

public static partial class S
{
    // ---- 읽기 전용 탭 3종 (T38f): 12 부대 기록 · 5 장비 · 3 편제
    public const string CmRoNote = "읽기 전용 - 이미 있는 캠페인 자료만 보여 주며 이 화면에서는 아무것도 바뀌지 않습니다.";

    public const string CmRecTitle = "12. 부대 기록";
    public const string CmRecSummary = "치른 작전 {0}회 · 누적 적 사살 {1:N0} · 누적 손실 {2:N0}";
    public const string CmRecRecentHead = "작전 일지 (최근 {0}건)";
    public const string CmRecRow = "{0}  -  사살 {1:N0} · 손실 {2:N0} · 생존 {3:N0}";
    public const string CmRecEmpty = "(아직 치른 작전이 없습니다)";
    public const string CmRecTopHead = "이월 사병 사살 상위";
    public const string CmRecTopRow = "{0}  -  사살 {1:N0}";
    public const string CmRecTopEmpty = "(기록 없음)";
    public const string CmRecDebriefHead = "마지막 전과 보고";
    public const string CmRecDebrief = "{0}: 전사 {1:N0} · 부상 복귀 {2:N0} · 제대 {3:N0} · 적 사살 {4:N0}";
    public const string CmRecDebriefEmpty = "(보고 없음)";
    public const string CmRecLater = "전사자 명부 · 훈장은 후속 (기록 자료가 아직 없음).";

    public const string CmEqTitle = "5. 장비 관리 - 재고 현황";
    public const string CmEqColCaliber = "구경", CmEqColRifle = "소총", CmEqColAmmo = "탄약", CmEqColCo = "편성 중대";
    public const string CmEqCaliberRow = "{0}";
    public const string CmEqShellsHead = "포탄";
    public const string CmEqHeavyHead = "중장비";
    public const string CmEqHeavyNone = "(없음)";
    public const string CmEqHorses = "군마 {0:N0}두 · 소총 합계 {1:N0}정";
    public const string CmEqLater = "장비 개량 · 도입은 연구 탭과 함께 후속.";
    public static string CmCaliberName(string key) => key switch
    {
        "ru" => "노식", "jp" => "일식", "de" => "독식", "fr" => "불식", "jpOld" => "구일식", _ => key
    };

    public const string CmOrgTitle = "3. 편제 - 연대 · 대대 · 중대";
    public const string CmOrgEmpty = "(편성된 중대가 없습니다)";
    public const string CmOrgRegHead = "{0}연대 - {1}개 중대 · 병력 {2:N0}";
    public const string CmOrgDirectHead = "직할 - {0}개 중대 · 병력 {1:N0}";
    public const string CmOrgBat = "{0}  {1}개 중대 · 병력 {2:N0}/{3:N0} · 부상 {4:N0} · 가용 {5}";
    public const string CmOrgDirectCo = "{0}  병력 {1:N0}/{2:N0} · 부상 {3:N0}{4}";
    public const string CmOrgDirectUnavail = " · 출전 불가";
    // ---- [T42a] 피로 · 사기 (읽기 표시)
    public const string CmSumFatigueMorale = "피로 평균 {0} · 사기 평균 {1} · 지침 {2}개 중대";
    public const string CmOrgBarFatigue = "피로", CmOrgBarMorale = "사기";
    public const string CmOrgTired = "지침 {0}";
    public const string CmStatusTired = "지침 (피로)";
    public const string CmOrgMore = "... 외 {0}줄 (화면 밖)";
    public const string CmOrgLater = "분대 · 중대 편제 틀 편집은 후속 (연대 배속은 2 신병/부대 탭).";
}
