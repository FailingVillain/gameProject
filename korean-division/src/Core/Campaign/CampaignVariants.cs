using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>[TC-F] 미션 결과 → 캠페인 변수 표의 한 줄: 그 미션이 그 결과("won" · "lost" · "failConfirmed" = 실패 확정) 로 끝나면 변수들을 정해진 값으로.</summary>
public sealed class VariantResultRule
{
    public string Mission { get; set; } = "";
    public string Result { get; set; } = "won";
    public List<DecisionEffect> Set { get; set; } = new();
}

/// <summary>[TC-F] 포로 합산: 이 미션들이 승리로 끝나면, 항복 상태로 남은 적 중 Factions(세력 en) 인원을 Var 에 더한다 (pow.bandit = BT1 + SK1; 소비처 D09 B 포로 처형, 처형 후 0).</summary>
public sealed class VariantPowRule
{
    public string Var { get; set; } = "";
    public List<string> Factions { get; set; } = new();
    public List<string> Missions { get; set; } = new();
}

/// <summary>
/// [TC-F] 캠페인 변형 데이터 (assets/data/campaign_variants.json): SK 성공 플래그 3종 · SK4 실패 확정 · pow.bandit 합산을 코드가 아니라 표로 정한다.
/// 상태는 기존 CampaignState.Flags (변수 레지스트리) 만 쓴다 — 저장 필드 신규 없음. 미션 쪽 변형 적용은 MissionVariants. 파일이 없거나 깨지면 빈 표 (아무 일도 안 함).
/// </summary>
public sealed class CampaignVariants
{
    public List<VariantResultRule> OnResultRules { get; set; } = new();
    public List<VariantPowRule> Pow { get; set; } = new();

    sealed class FileModel { public List<VariantResultRule>? OnResult { get; set; } public List<VariantPowRule>? Pow { get; set; } }

    static CampaignVariants? current;
    public static CampaignVariants Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "campaign_variants.json"));
    public static void Override(CampaignVariants? v) => current = v;

    public static CampaignVariants Load(string path)
    {
        var r = new CampaignVariants();
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), DataJson.Opts) is { } m)
            {
                r.OnResultRules = (m.OnResult ?? new()).Where(x => x != null && x.Mission.Length > 0 && x.Set != null && x.Set.All(e => e != null && CampaignVars.Has(e.Var))).ToList();
                r.Pow = (m.Pow ?? new()).Where(x => x != null && x.Missions != null && x.Factions != null && CampaignVars.Has(x.Var)).ToList();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { }
        return r;
    }

    /// <summary>미션이 끝났을 때 (SettleBattle) 또는 SK 실패 확정 때 (결과 "failConfirmed", <paramref name="r"/> = null) 호출. 해당 표 줄만 적용하고 적용한 줄 수를 돌려준다.</summary>
    public static int OnResult(CampaignState s, string missionId, string result, MissionResults? r) => Current.Apply(s, missionId, result, r);

    public int Apply(CampaignState s, string missionId, string result, MissionResults? r)
    {
        int n = 0;
        foreach (var rule in OnResultRules)
        {
            if (rule.Mission != missionId || rule.Result != result) continue;
            foreach (var e in rule.Set) { if (e.Op == "add") CampaignVars.Add(s, e.Var, e.Value); else CampaignVars.Set(s, e.Var, e.Value); }
            n++;
        }
        if (result == "won" && r != null)
            foreach (var p in Pow)
            {
                if (!p.Missions.Contains(missionId)) continue;
                int men = 0;
                foreach (var f in p.Factions) men += r.EnemyCapturedByFaction.GetValueOrDefault(f);
                if (men > 0) { CampaignVars.Add(s, p.Var, men); n++; }
            }
        return n;
    }
}
