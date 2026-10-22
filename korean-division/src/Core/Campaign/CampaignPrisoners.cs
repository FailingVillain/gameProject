namespace KoreanDivision.Core;

/// <summary>
/// [T42c] 캠페인 포로 장부 · 처리 (설계 docs/CAMPAIGN_FOLLOWUP_RULES.md §4). 발생 수 = MissionResults.EnemyCaptured (항복한 적 — 항복 규칙이 꺼져 있으면 0)
/// 를 정산에서 Stock.Prisoners 에 더한다. 처리는 작전 사이 홈에서만: 송환 (무료) 또는 수용 (1인당 보급 campaign_economy.json prisonerAcceptSupply, 임시).
/// 편입 (병력화) · 민심 연동은 없음 (새 메커닉 — 범위 밖). 정수 · 순서 없음 (결정적), 전투 시뮬 · 체크섬 무관.
/// </summary>
public static class CampaignPrisoners
{
    public enum Block { None, Operation, Empty }

    /// <summary>처리 버튼을 지금 쓸 수 있는가: 작전 진행 중 (Pending) 이거나 장부가 비어 있으면 사유.</summary>
    public static Block BlockOf(CampaignState? s)
    {
        if (s == null || s.Stock.Prisoners <= 0) return Block.Empty;
        if (s.Pending != null) return Block.Operation;
        return Block.None;
    }

    /// <summary>보급품으로 지금 수용할 수 있는 최대 수 (장부 한도 안).</summary>
    public static int MaxAcceptable(CampaignState s)
    {
        float per = CampaignEconomyDefs.Current.PrisonerAcceptSupply;
        int n = s.Stock.Prisoners;
        if (per <= 0f) return n;
        return Math.Clamp((int)MathF.Floor(s.Supply / per), 0, n);
    }

    /// <summary>송환: 최대 n 명 (장부 한도 안), 실제 처리 수를 돌려준다. 작전 사이에만.</summary>
    public static int Repatriate(CampaignState s, int n)
    {
        if (BlockOf(s) != Block.None || n <= 0) return 0;
        int k = Math.Min(n, s.Stock.Prisoners);
        s.Stock.Prisoners -= k; s.PrisonersRepatriated += k;
        return k;
    }

    /// <summary>수용: 최대 n 명 (장부 · 보급 한도 안), 보급품을 소모하고 실제 처리 수를 돌려준다. 작전 사이에만.</summary>
    public static int Accept(CampaignState s, int n)
    {
        if (BlockOf(s) != Block.None || n <= 0) return 0;
        int k = Math.Min(n, MaxAcceptable(s));
        if (k <= 0) return 0;
        s.Supply = Math.Max(0f, s.Supply - k * CampaignEconomyDefs.Current.PrisonerAcceptSupply);
        s.Stock.Prisoners -= k; s.PrisonersInterned += k;
        return k;
    }
}
