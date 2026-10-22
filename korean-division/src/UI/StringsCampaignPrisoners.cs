namespace KoreanDivision.UI;

/// <summary>[T42c] 탭 7 외교 · 포로 처리 문구.</summary>
public static partial class S
{
    public static string CmDebriefPrisoners => KoreanDivision.Core.Loc.T("S.CmDebriefPrisoners", "포로 {0:N0}명 (장부 기록 · [외교] 탭에서 송환 · 수용)");
    public static string CmPrisTitle => KoreanDivision.Core.Loc.T("S.CmPrisTitle", "7. 외교 · 포로");
    public static string CmPrisLedger => KoreanDivision.Core.Loc.T("S.CmPrisLedger", "포로 장부: {0}명 (미처리)");
    public static string CmPrisTotals => KoreanDivision.Core.Loc.T("S.CmPrisTotals", "지금까지 송환 {0}명 · 수용 {1}명");
    public static string CmPrisLast => KoreanDivision.Core.Loc.T("S.CmPrisLast", "최근 작전 포로 발생 {0}명");
    public static string CmPrisEmpty => KoreanDivision.Core.Loc.T("S.CmPrisEmpty", "처리할 포로가 없습니다. 포로는 적이 항복했을 때만 생깁니다 (항복 규칙은 기본 꺼짐).");
    public static string CmPrisDuringOp => KoreanDivision.Core.Loc.T("S.CmPrisDuringOp", "작전 진행 중에는 포로를 처리할 수 없습니다 (작전 사이에만).");
    public static string CmPrisRepHead => KoreanDivision.Core.Loc.T("S.CmPrisRepHead", "송환 (무료)");
    public static string CmPrisAccHead => KoreanDivision.Core.Loc.T("S.CmPrisAccHead", "수용 (1인당 보급 {0:0.#})");
    public static string CmPrisOne => KoreanDivision.Core.Loc.T("S.CmPrisOne", "[1명]"); public static string CmPrisAll => KoreanDivision.Core.Loc.T("S.CmPrisAll", "[전부]");
    public static string CmPrisAccAvail => KoreanDivision.Core.Loc.T("S.CmPrisAccAvail", "보급 {0:0}으로 수용 가능 {1}명");
    public static string CmPrisLater => KoreanDivision.Core.Loc.T("S.CmPrisLater", "선택지 · 대민 지원 · 용병 고용 · 편입(병력화): 후속 (확정 뒤).");
}
