namespace KoreanDivision.Core;

/// <summary>
/// [C1E1-g6] unlock.* 불리언 (예: unlock.fieldGun) -> 편성 가능 병종. 값은 CampaignState.Flags["unlock.xxx"] (0/1) 에 저장 -
/// 이미 저장되는 사전이라 저장 형식 변화 없음 (TC-E 변수 레지스트리의 같은 키와 같은 자리). 표는 campaign_economy.json:
/// unlockKinds (병종 Kind -> 해금 키), unlockOnWin (미션 id -> 첫 승리 정산 때 켜는 해금 키). 표에 없는 병종은 항상 편성 가능.
/// </summary>
public static class CampaignUnlocks
{
    public const string Prefix = "unlock.";

    public static bool IsUnlocked(CampaignState s, string key) => s.Flags != null && s.Flags.TryGetValue(key, out var v) && v != 0;

    /// <summary>해금을 켠다 (unlock. 접두어가 아니면 무시, 이미 켜져 있으면 무영향). 켰으면 true.</summary>
    public static bool Grant(CampaignState s, string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix, StringComparison.Ordinal) || IsUnlocked(s, key)) return false;
        (s.Flags ??= new())[key] = 1;
        return true;
    }

    /// <summary>이 병종이 편성 가능한가: 해금 표에 없으면 true, 있으면 해당 unlock 이 켜져 있어야 true.</summary>
    public static bool KindUnlocked(CampaignState s, string kind)
        => kind == null || !CampaignEconomyDefs.Current.UnlockKinds.TryGetValue(kind, out var key) || IsUnlocked(s, key);

    public static bool CanDeploy(CampaignState s, CampaignState.RosterCompany c) => KindUnlocked(s, c.Kind);

    /// <summary>편성 화면용: 미해금 병종을 뺀 명부.</summary>
    public static IEnumerable<CampaignState.RosterCompany> Deployable(CampaignState s) => s.Roster.Where(c => CanDeploy(s, c));

    /// <summary>미션 첫 승리 정산 때 호출: unlockOnWin 표의 해금을 켠다. 새로 켠 키 수.</summary>
    public static int OnWon(CampaignState s, string missionId)
    {
        int n = 0;
        if (missionId != null && CampaignEconomyDefs.Current.UnlockOnWin.TryGetValue(missionId, out var keys))
            foreach (var k in keys) if (Grant(s, k)) n++;
        return n;
    }
}
