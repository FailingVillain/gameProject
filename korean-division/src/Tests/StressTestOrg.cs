using KoreanDivision.Core;

namespace KoreanDivision.Tests;

static partial class StressTest
{
    /// <summary>J: 100기 덩어리를 중대 하나로 편성 (깃발 이동 — 경로 계산이 중대당 1개).</summary>
    static partial void FormCompanyForStress(World w, List<Unit> list) => w.FormCompany(1, list);
}
