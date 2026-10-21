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
