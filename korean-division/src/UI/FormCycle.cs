using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>대형 순환 (Ctrl+F · 단추). 화면 쪽 순수 로직 — 창 없이 테스트할 수 있다.</summary>
public static class FormCycle
{
    /// <summary>기본 대형 종류 순환 (자유 포함).</summary>
    /// <summary>기본 대형 종류 순환 (자유 · 횡대 · 종대 · 쐐기 · 밀집 · 산개 — 사선은 기병 중대 · 분대에서만).</summary>
    public static FormType NextType(FormType t) => (int)t >= (int)FormType.Spread ? FormType.Free : (FormType)((int)t + 1);
    /// <summary>중대 대형 순환 — 중대는 항상 대형을 가지므로 자유(Free)를 건너뛴다. 기병 중대는 좌 · 우 사선까지.</summary>
    public static FormType NextCompanyType(FormType t, bool cavalry = false)
    {
        if (t == FormType.Spread && cavalry) return FormType.EchelonLeft;
        if (t == FormType.EchelonLeft) return cavalry ? FormType.EchelonRight : FormType.Line;
        if (t == FormType.EchelonRight) return FormType.Line;
        var n = NextType(t); return n == FormType.Free ? NextType(n) : n;
    }
    /// <summary>분대 대형 순환: 자동 → 밀집 → 산개 → 종대 → 횡대 → 쐐기 (→ 기병 좌 · 우 사선) → 자동.</summary>
    public static FormType NextSquadForm(FormType t, bool cavalry = false) => t switch
    {
        FormType.Free => FormType.Box, FormType.Box => FormType.Spread, FormType.Spread => FormType.Column, FormType.Column => FormType.Line,
        FormType.Line => FormType.Wedge, FormType.Wedge => cavalry ? FormType.EchelonLeft : FormType.Free,
        FormType.EchelonLeft => cavalry ? FormType.EchelonRight : FormType.Free, _ => FormType.Free,
    };
    public static FormOrder NextOrder(FormOrder o) => (FormOrder)(((int)o + 1) % Enum.GetValues<FormOrder>().Length);
    public static BatForm NextBatForm(BatForm f) => (BatForm)(((int)f + 1) % Enum.GetValues<BatForm>().Length);
    /// <summary>중대 전술 틀 순환 (틀 없음 → 화력-기동 → 좌 · 우 우회 → 종심 방어 → 포병 대형 → 돌격 → 틀 없음).</summary>
    public static CompanyTactic NextTactic(CompanyTactic t) => (CompanyTactic)(((int)t + 1) % Enum.GetValues<CompanyTactic>().Length);
    /// <summary>역할 순환 (화력조 → 사격조 → 기동조 → 지원 → 지휘 · 정찰 → 자동).</summary>
    public static UnitRole NextRole(UnitRole r) => r == UnitRole.Command ? UnitRole.None : (UnitRole)((int)r + 1);
}
