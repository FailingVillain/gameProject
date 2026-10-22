namespace KoreanDivision.Core;

/// <summary>
/// [T19c · Q9] 연대 배속 규칙 (캠페인 홈 2 신병 · 부대 탭). 연대 1~9, 0 = 사단 직할 (미배속).
/// 대대 소속 중대 (BattalionNumber &gt; 0) 는 대대 단위로 함께 배속되고, 직할 중대 (BattalionNumber 0) 는 중대 하나씩 배속된다.
/// 캠페인 저장 데이터만 바꾼다 (시뮬 상태 아님). 전투 중 연대 숫자키 (규칙 9) 는 별 작업.
/// </summary>
public static class CampaignRegiments
{
    public const int Max = 9;

    /// <summary>companyId 가 속한 대대 (직할이면 그 중대 하나) 를 regiment 로 배속. 바뀐 중대 수 (모르는 id · 범위 밖 = 0).</summary>
    public static int Attach(CampaignState s, string companyId, int regiment)
    {
        if (regiment < 0 || regiment > Max) return 0;
        var c = s.Roster.FirstOrDefault(r => r.Id == companyId);
        if (c == null) return 0;
        int n = 0;
        foreach (var r in s.Roster)
        {
            bool same = c.BattalionNumber > 0 ? r.BattalionNumber == c.BattalionNumber : ReferenceEquals(r, c);
            if (!same || r.Regiment == regiment) continue;
            r.Regiment = regiment; n++;
        }
        return n;
    }

    /// <summary>다음 / 이전 연대 번호 (0 → 1 → … → 9 → 0 순환).</summary>
    public static int Step(int regiment, int delta) => ((regiment + delta) % (Max + 1) + Max + 1) % (Max + 1);

    /// <summary>연대별 중대 수 (index 0 = 직할).</summary>
    public static int[] CompanyCounts(CampaignState s)
    {
        var n = new int[Max + 1];
        foreach (var r in s.Roster) n[Math.Clamp(r.Regiment, 0, Max)]++;
        return n;
    }
}
