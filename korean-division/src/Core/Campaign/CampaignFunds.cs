namespace KoreanDivision.Core;

/// <summary>
/// [TC-D · C1E1 R5] 자금 · 외인 슬롯 · 통제 한도 · 에피소드 가용 보급 순수 규칙. 모든 수치는 campaign_economy.json (CampaignEconomyDefs).
/// 후속 카드(TC-E 호감/결정, d2 시작 상태, g6 해금, g2 화면)가 쓰는 공개 API 는 이 클래스뿐이다.
/// </summary>
public static class CampaignFunds
{
    /// <summary>자금 → 은 냥 (표시 단위). 10 = 5,000냥.</summary>
    public static int ToSilver(int funds) => funds * CampaignEconomyDefs.Current.SilverPerFund;

    static int Blocks(int men, int block) => men <= 0 ? 0 : (men + block - 1) / block;
    /// <summary>의용군 men 명 가격 (50명당 2, 올림; 150명 = 6).</summary>
    public static int VolunteerPrice(int men) { var d = CampaignEconomyDefs.Current; return Blocks(men, d.VolunteerBlockMen) * d.VolunteerFundsPerBlock; }
    /// <summary>마적 men 명 가격 (100명당 3, 올림).</summary>
    public static int BanditPrice(int men) { var d = CampaignEconomyDefs.Current; return Blocks(men, d.BanditBlockMen) * d.BanditFundsPerBlock; }

    /// <summary>자금을 쓴다. 모자라거나 음수면 상태 불변 + 한국어 사유.</summary>
    public static bool TrySpend(CampaignState s, int price, out string error)
    {
        error = "";
        if (price < 0) { error = "가격이 올바르지 않습니다."; return false; }
        if (price > s.Funds) { error = $"자금이 모자랍니다 (필요 {price} · 보유 {s.Funds})."; return false; }
        s.Funds -= price;
        return true;
    }

    /// <summary>자금을 더한다 (보상 등; 상한 1,000,000).</summary>
    public static void Gain(CampaignState s, int amount) => s.Funds = (int)Math.Clamp((long)s.Funds + amount, 0, 1_000_000);

    /// <summary>편성(투입 선택)의 통제 사용량: 정규 = 외인 아닌 중대 병력 합, 외인 = Foreign 중대 병력 합.</summary>
    public static (int Regular, int Foreign) ControlUsage(IEnumerable<CampaignState.RosterCompany> selected)
    {
        int reg = 0, frn = 0;
        foreach (var c in selected) { if (c.Foreign) frn += c.Strength; else reg += c.Strength; }
        return (reg, frn);
    }

    /// <summary>통제 한도 검사. null = 통과, 아니면 한국어 사유. regularCap 생략 = JSON 기본 (E1 3,000; E2 는 호출자가 5,000~6,000 을 넘긴다).</summary>
    public static string? CheckControl(IEnumerable<CampaignState.RosterCompany> selected, int? regularCap = null)
    {
        var d = CampaignEconomyDefs.Current;
        var (reg, frn) = ControlUsage(selected);
        int cap = regularCap ?? d.RegularControlCap;
        if (reg > cap) return $"정규군 통제 한도 초과 ({reg} / {cap}).";
        if (frn > d.ForeignSlotCap) return $"외인 슬롯 한도 초과 ({frn} / {d.ForeignSlotCap}).";
        return null;
    }

    /// <summary>가용 보급 = 에피소드 시작 보급 − 누적 영구 소모 (대민 구제 · 앞 미션 소모) − 진행 중 출격의 예약. 0 미만이면 0.</summary>
    public static float AvailableSupply(CampaignState s)
    {
        float reserved = s.Pending?.EpisodeSupplyReserved ?? 0f;
        return Math.Max(0f, CampaignEconomyDefs.Current.EpisodeSupplyStart - s.EpisodePermanentSupplyLoss - reserved);
    }

    /// <summary>영구 소모를 직접 기록 (D02 C 대민 구제 등; 음수는 무시).</summary>
    public static void AddPermanentLoss(CampaignState s, float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return;
        s.EpisodePermanentSupplyLoss = Math.Min(1e8f, s.EpisodePermanentSupplyLoss + amount);
    }

    /// <summary>한 작전의 영구 소모 = 투입 비용 x permanentDeployFrac (탄약) + 전사자 x casualtyReplaceCost (보충).</summary>
    public static float PermanentLoss(float deployCost, int dead)
    {
        var d = CampaignEconomyDefs.Current;
        return Math.Max(0f, deployCost) * d.PermanentDeployFrac + Math.Max(0, dead) * d.CasualtyReplaceCost;
    }

    /// <summary>정산: 예약을 풀고 (생존분 환원 = 영구 소모만 남김) 영구 소모를 누적. 예약이 없으면 무영향.</summary>
    public static void SettleReservation(CampaignState s, int dead)
    {
        if (s.Pending == null) return;
        float reserved = s.Pending.EpisodeSupplyReserved;
        if (reserved <= 0f && dead <= 0) return;
        AddPermanentLoss(s, PermanentLoss(reserved, dead));
        s.Pending.EpisodeSupplyReserved = 0f;
    }
}
