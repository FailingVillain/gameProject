using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 캠페인 경제 밸런스 숫자 (assets/data/campaign_economy.json, T29 / D24). 기본값 = 이전에 코드에 박혀 있던 값 (파일이 없거나 깨져도 같은 동작).
/// </summary>
public sealed class CampaignEconomyDefs
{
    public float RegularSupply { get; set; } = 1500f;
    public int RegularRecruits { get; set; } = 1000;
    public float WoundReturnRatio { get; set; } = 0.25f;
    public int AmmoPerInfantry { get; set; } = 60;
    public int AmmoPerMg { get; set; } = 200;
    public float LootSupplyPerKill { get; set; } = 0.3f;
    public float LootRiflesPerKill { get; set; } = 0.5f;
    public int LootAmmoPerKill { get; set; } = 20;
    public float PenaltyCap { get; set; } = 0.30f;
    public int LargeDeployThreshold { get; set; } = 180;
    public float LargeDeployScaleMul { get; set; } = 1.5f;
    public float HealCostPerMan { get; set; } = 0.5f;
    public float RecruitAssignCostPerMan { get; set; } = 0.5f;
    public int HowitzerCompanyStrength { get; set; } = 40;
    /// <summary>[T19b Q5 · Q6] 재고가 기본 휴대량보다 모자랄 때 싣는 비율 (재고가 그보다도 적으면 남은 만큼). 탄약 구경별 · 포탄 종류별 같은 규칙.</summary>
    public float ShortLoadRatio { get; set; } = 0.5f;
    /// <summary>[T19b Q6] 포 무기 키 (balance.json weapons) → 포탄 재고 키 (StockKeys.InfGun · Field · Howitzer). 기본 휴대량 = 그 무기의 ammo.</summary>
    public Dictionary<string, string> ShellWeapons { get; set; } = new() { ["gun_infantry"] = StockKeys.InfGun, ["gun_field"] = StockKeys.Field, ["howitzer_heavy"] = StockKeys.Howitzer };
    /// <summary>[T19b Q6] 승리 시 적 손실 1명당 노획 포탄 (종류별, 내림).</summary>
    public Dictionary<string, float> LootShellsPerKill { get; set; } = new() { [StockKeys.InfGun] = 0.1f, [StockKeys.Field] = 0.05f, [StockKeys.Howitzer] = 0.02f };

    // ---- [T42a] 피로 · 사기 계정 — 모든 수치 임시 (0.3 밸런스에서 확정). 홈 계정만, 전투 시뮬과 무관.
    /// <summary>[임시] 작전에 투입된 중대의 피로 증가.</summary>
    public int FatiguePerDeploy { get; set; } = 20;
    /// <summary>[임시] 작전에 투입되지 않은 중대의 피로 회복 (쉼).</summary>
    public int FatigueRestRecovery { get; set; } = 15;
    /// <summary>[임시] 피로가 이 값 이상이면 '지침' 표시 + 투입 경고 (잠금 없음 — Q1 답 전).</summary>
    public int FatigueWarnThreshold { get; set; } = 70;
    /// <summary>[임시] 투입 중대 사기: 승리 + / 패배 −.</summary>
    public int MoraleWin { get; set; } = 5;
    public int MoraleLoss { get; set; } = 10;
    /// <summary>[임시] 투입 중대 사기에서 손실률 (잃은 수 / 투입 수, 0..1) 1.0 당 추가로 빼는 값.</summary>
    public int MoraleCasualtyScale { get; set; } = 20;
    /// <summary>[임시] 미투입 중대 사기 회복 (쉼).</summary>
    public int MoraleRestRecovery { get; set; } = 3;

    // ---- [T42b] 진급 후보 · 적용 — 모든 수치 임시 (0.3 밸런스에서 확정). 홈 계정만, 전투 시뮬과 무관.
    /// <summary>[기본 꺼짐] true = 작전 사이 수동 진급: 이월 때 사병 계급은 작전 시작 계급에 머물고 (사살 환산 자동 진급 중지), 홈 [진급] 탭에서 후보를 눌러 올린다. false = 기존 동작 (자동 이월).</summary>
    public bool PromotionManual { get; set; }
    /// <summary>[임시] 사병 후보 문턱: 현재 계급 색인 (0..8) → 마지막 작전 사살 수가 이 값 이상이면 후보. 0 이하 = 그 계급에서는 후보 없음.</summary>
    public int[] PromoSoldierKills { get; set; } = { 1, 2, 3, 4, 4, 5, 6, 8, 0 };
    /// <summary>[임시] 사병이 작전 사이 진급으로 오를 수 있는 최고 계급 색인 (7 = 정교 · 상사급).</summary>
    public int PromoSoldierMaxRank { get; set; } = 7;
    /// <summary>[임시] 장교 후보 문턱: 현재 정식 계급 (0..6) → 경험치가 이 값 이상이면 후보. 0 이하 = 후보 없음. 상한 = 중령 (balance.json officers.maxPromotionRank).</summary>
    public int[] PromoOfficerXp { get; set; } = { 0, 60, 180, 420, 900, 0, 0 };
    /// <summary>[임시] 작전 사이 한 번에 낼 수 있는 진급 수 (사병 + 장교 합계 정원).</summary>
    public int PromoMaxPerBreak { get; set; } = 8;

    // ---- [T42c] 포로 장부 · 처리 — 임시 (0.3 밸런스에서 확정). 홈 계정만.
    /// <summary>[임시] 포로 1명을 수용할 때 드는 보급품 (송환은 무료). 편입(병력화)은 없음.</summary>
    public float PrisonerAcceptSupply { get; set; } = 2f;

    // ---- [T42d] 일정 — 임시 (Q6 가정: 작전당 고정 일수). 카운터만, 계절 · 시간 효과 없음.
    /// <summary>[임시] 첫 승리 정산 한 번에 늘어나는 작전 일수 (0 = 일수 안 늘림).</summary>
    public int MissionDays { get; set; } = 1;

    // ---- [T48a] 연대장 — 전부 임시 (0.3 밸런스에서 확정, 사용자 결정 AU/AW/AY: 해임 시 호감 소폭 하락 · 재기용 쿨다운 1작전).
    /// <summary>[임시] 연대장을 해임할 때 그 인물의 호감에서 깎는 값 (양수; 인물별 dismissFavorPenalty 가 있으면 그쪽, 해임 불가는 0).</summary>
    public int CommanderDismissFavorPenalty { get; set; } = 2;
    /// <summary>[임시] 호감 범위.</summary>
    public int CommanderFavorMin { get; set; } = -20;
    public int CommanderFavorMax { get; set; } = 100;
    /// <summary>[임시] 해임 뒤 이 작전 수가 지나야 다시 기용 가능 (사용자 AY: 1작전).</summary>
    public int CommanderRehireCooldownMissions { get; set; } = 1;
    /// <summary>[임시] 해임 뒤 호감이 이 값 이하로 내려가 있으면 재기용 불가 (설계 3.4, 이 값 초과여야 함).</summary>
    public int CommanderRehireMinFavor { get; set; } = -5;

    // ---- [M2 · 사용자 BH2/BH5] 탈영 · 포/중화기 보급 단가 — 전부 자리표시 (0.3 밸런스에서 확정). 홈 계정만, 전투 시뮬과 무관.
    /// <summary>[임시] 중대 사기가 이 값 미만이면 정산마다 탈영병 발생 (이상이면 0).</summary>
    public int DesertionMoraleBelow { get; set; } = 30;
    /// <summary>[임시] 사기 0 일 때의 탈영률 (병력 비율, 0..1). 사기가 문턱에 가까울수록 선형으로 0 에 수렴: 률 = (문턱 - 사기) / 문턱 x 이 값. 정수 반올림.</summary>
    public float DesertionMaxRate { get; set; } = 0.05f;
    /// <summary>[임시 · BH5] 포(보병포 · 야포) · 중화기(곡사포) 보급 단가. 값만 정의 — 사용처(투입 비용 환산)는 d2(kd/c1e1-d2-econ)의 supplyCost* 와 함께 연결된다.</summary>
    public float SupplyCostArtillery { get; set; } = 7f;
    public float SupplyCostHeavy { get; set; } = 7f;

    // ---- [TC-D · C1E1 R5] 자금 · 외인 슬롯 · 통제 한도 · 에피소드 보급 풀 — 모든 수치는 spec R5/사용자 결정(2026-10-22), 가격 일부 임시 (균형 단계에서 조정).
    /// <summary>에피소드 시작 자금 (10 = 은 5,000냥).</summary>
    public int FundsStart { get; set; } = 10;
    /// <summary>자금 1 = 은 몇 냥 (표시 단위).</summary>
    public int SilverPerFund { get; set; } = 500;
    /// <summary>의용군: 이 인원(묶음)마다 자금 VolunteerFundsPerBlock (150명 = 6).</summary>
    public int VolunteerBlockMen { get; set; } = 50;
    public int VolunteerFundsPerBlock { get; set; } = 2;
    /// <summary>마적: 이 인원(묶음)마다 자금 BanditFundsPerBlock (100명당 3).</summary>
    public int BanditBlockMen { get; set; } = 100;
    public int BanditFundsPerBlock { get; set; } = 3;
    /// <summary>[임시] D09 요정 · 보검 가격.</summary>
    public int FairyFunds { get; set; } = 2;
    public int SwordFunds { get; set; } = 2;
    /// <summary>한국 정규군 통제 한도 (E1 3,000; E2 는 데이터에서 5,000~6,000 로 바꾼다) · 외인(의용군 · 마적 · 의용병) 별도 슬롯 합계.</summary>
    public int RegularControlCap { get; set; } = 3000;
    public int ForeignSlotCap { get; set; } = 800;
    /// <summary>에피소드 시작 보급 (투입 비용 단위) · 영구 소모 = 투입 비용 x 이 비율(탄약) + 전사자 x 보충 비용.</summary>
    public int EpisodeSupplyStart { get; set; } = 4000;
    public float PermanentDeployFrac { get; set; } = 0.10f;
    public float CasualtyReplaceCost { get; set; } = 1f;
    /// <summary>D02 C 대민 구제의 영구 보급 소모 (이벤트 런타임은 TC-E 가 이 값을 쓴다).</summary>
    public int CivilianReliefSupplyLoss { get; set; } = 400;

    static CampaignEconomyDefs? current;
    public static CampaignEconomyDefs Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "campaign_economy.json"));
    /// <summary>[테스트] 정의를 갈아 끼운다 (null = 파일 다시 읽기).</summary>
    public static void Override(CampaignEconomyDefs? d) => current = d;

    public static CampaignEconomyDefs Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<CampaignEconomyDefs>(File.ReadAllText(path), DataJson.Opts) is { } r) return r.Sanitized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { }
        return new CampaignEconomyDefs();
    }

    /// <summary>[T42b] 배열 정리: null 이면 기본값, 모자라면 0 (= 후보 없음) 으로 채우고 남는 것은 버리며, 값은 0..1,000,000.</summary>
    static int[] FixArray(int[]? v, int[] def, int len)
    {
        if (v == null) return def;
        var r = new int[len];
        for (int i = 0; i < len && i < v.Length; i++) r[i] = Math.Clamp(v[i], 0, 1_000_000);
        return r;
    }

    static float F(float v, float lo, float hi, float def) => float.IsFinite(v) ? Math.Clamp(v, lo, hi) : def;

    /// <summary>모든 값을 정상 범위로 자른다 (비율 0..1, 비용 최소 치료 · 배치 0.01 / 공급 0 이상, 개수 0 이상, 치료비 0 금지). NaN · 무한대는 기본값.</summary>
    public CampaignEconomyDefs Sanitized()
    {
        var d = new CampaignEconomyDefs();
        RegularSupply = F(RegularSupply, 0f, 1e7f, d.RegularSupply);
        RegularRecruits = Math.Clamp(RegularRecruits, 0, 1_000_000);
        WoundReturnRatio = F(WoundReturnRatio, 0f, 1f, d.WoundReturnRatio);
        AmmoPerInfantry = Math.Clamp(AmmoPerInfantry, 0, 100_000);
        AmmoPerMg = Math.Clamp(AmmoPerMg, 0, 100_000);
        LootSupplyPerKill = F(LootSupplyPerKill, 0f, 1000f, d.LootSupplyPerKill);
        LootRiflesPerKill = F(LootRiflesPerKill, 0f, 1000f, d.LootRiflesPerKill);
        LootAmmoPerKill = Math.Clamp(LootAmmoPerKill, 0, 100_000);
        PenaltyCap = F(PenaltyCap, 0f, 1f, d.PenaltyCap);
        LargeDeployThreshold = Math.Clamp(LargeDeployThreshold, 0, 1_000_000);
        LargeDeployScaleMul = F(LargeDeployScaleMul, 1f, 100f, d.LargeDeployScaleMul);
        HealCostPerMan = F(HealCostPerMan, 0.01f, 1000f, d.HealCostPerMan);
        RecruitAssignCostPerMan = F(RecruitAssignCostPerMan, 0.01f, 1000f, d.RecruitAssignCostPerMan);
        HowitzerCompanyStrength = Math.Clamp(HowitzerCompanyStrength, 1, 100_000);
        ShortLoadRatio = F(ShortLoadRatio, 0f, 1f, d.ShortLoadRatio);
        FatiguePerDeploy = Math.Clamp(FatiguePerDeploy, 0, 100);
        FatigueRestRecovery = Math.Clamp(FatigueRestRecovery, 0, 100);
        FatigueWarnThreshold = Math.Clamp(FatigueWarnThreshold, 1, 100);
        MoraleWin = Math.Clamp(MoraleWin, 0, 100);
        MoraleLoss = Math.Clamp(MoraleLoss, 0, 100);
        MoraleCasualtyScale = Math.Clamp(MoraleCasualtyScale, 0, 100);
        MoraleRestRecovery = Math.Clamp(MoraleRestRecovery, 0, 100);
        MissionDays = Math.Clamp(MissionDays, 0, 365);
        CommanderFavorMin = Math.Clamp(CommanderFavorMin, -1000, 0);
        CommanderFavorMax = Math.Clamp(CommanderFavorMax, 1, 1000);
        CommanderDismissFavorPenalty = Math.Clamp(CommanderDismissFavorPenalty, 0, 100);
        CommanderRehireCooldownMissions = Math.Clamp(CommanderRehireCooldownMissions, 0, 100);
        CommanderRehireMinFavor = Math.Clamp(CommanderRehireMinFavor, CommanderFavorMin, CommanderFavorMax);
        DesertionMoraleBelow = Math.Clamp(DesertionMoraleBelow, 0, 100);
        DesertionMaxRate = F(DesertionMaxRate, 0f, 1f, d.DesertionMaxRate);
        SupplyCostArtillery = F(SupplyCostArtillery, 0f, 100000f, d.SupplyCostArtillery);
        SupplyCostHeavy = F(SupplyCostHeavy, 0f, 100000f, d.SupplyCostHeavy);
        PrisonerAcceptSupply = F(PrisonerAcceptSupply, 0f, 1000f, d.PrisonerAcceptSupply);
        FundsStart = Math.Clamp(FundsStart, 0, 1_000_000);
        SilverPerFund = Math.Clamp(SilverPerFund, 1, 1_000_000);
        VolunteerBlockMen = Math.Clamp(VolunteerBlockMen, 1, 100_000); VolunteerFundsPerBlock = Math.Clamp(VolunteerFundsPerBlock, 0, 1_000_000);
        BanditBlockMen = Math.Clamp(BanditBlockMen, 1, 100_000); BanditFundsPerBlock = Math.Clamp(BanditFundsPerBlock, 0, 1_000_000);
        FairyFunds = Math.Clamp(FairyFunds, 0, 1_000_000); SwordFunds = Math.Clamp(SwordFunds, 0, 1_000_000);
        RegularControlCap = Math.Clamp(RegularControlCap, 0, 1_000_000); ForeignSlotCap = Math.Clamp(ForeignSlotCap, 0, 1_000_000);
        EpisodeSupplyStart = Math.Clamp(EpisodeSupplyStart, 0, 10_000_000);
        PermanentDeployFrac = F(PermanentDeployFrac, 0f, 1f, d.PermanentDeployFrac);
        CasualtyReplaceCost = F(CasualtyReplaceCost, 0f, 1000f, d.CasualtyReplaceCost);
        CivilianReliefSupplyLoss = Math.Clamp(CivilianReliefSupplyLoss, 0, 10_000_000);
        PromoSoldierMaxRank = Math.Clamp(PromoSoldierMaxRank, 0, 8);
        PromoMaxPerBreak = Math.Clamp(PromoMaxPerBreak, 0, 10_000);
        PromoSoldierKills = FixArray(PromoSoldierKills, d.PromoSoldierKills, 9);
        PromoOfficerXp = FixArray(PromoOfficerXp, d.PromoOfficerXp, 7);
        // [T19b] 포탄 사전: null 이면 기본값, 값은 알려진 포탄 재고 키만 (모르는 키 · null 항목은 버림), 노획은 0..1000 으로 자름.
        if (ShellWeapons == null) ShellWeapons = d.ShellWeapons;
        else ShellWeapons = ShellWeapons.Where(kv => kv.Key != null && kv.Value != null && Array.IndexOf(StockKeys.AllShells, kv.Value) >= 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        if (LootShellsPerKill == null) LootShellsPerKill = d.LootShellsPerKill;
        else LootShellsPerKill = LootShellsPerKill.Where(kv => kv.Key != null && Array.IndexOf(StockKeys.AllShells, kv.Key) >= 0)
            .ToDictionary(kv => kv.Key, kv => F(kv.Value, 0f, 1000f, 0f));
        return this;
    }
}
