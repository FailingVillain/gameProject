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
            if (File.Exists(path) && JsonSerializer.Deserialize<CampaignEconomyDefs>(File.ReadAllText(path), DataJson.Opts) is { } r) return r;
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
        return new CampaignEconomyDefs();
    }
}
