namespace KoreanDivision.Core;

/// <summary>
/// [T42b] 캠페인 진급 후보 · 적용 (설계 docs/CAMPAIGN_FOLLOWUP_RULES.md §3). 작전 사이 홈에서만 일어나는 데이터 처리 — 전투 시뮬 · 체크섬 무관.
/// 기본 꺼짐 (campaign_economy.json promotionManual=false): 꺼져 있으면 후보도 적용도 없고 이월은 기존 동작 (사살 환산 자동 계급).
/// 켜면: 이월 때 사병 계급은 작전 시작 계급에 머물고 (<see cref="CarriedRank"/>), 후보 = 이월 UnitLine 중 JSON 문턱 이상 (<see cref="Candidates"/>),
/// [진급] (<see cref="Promote"/>) 이 계급을 한 단계 올려 다음 미션 CampaignCarry 에 반영된다. 장교 상한 = 중령 (balance.json officers.maxPromotionRank). 군령권 = TBD(스토리), 여기 없음.
/// 모든 판정은 정수 · 목록 순서 (결정적, 난수 없음).
/// </summary>
public static class CampaignPromotion
{
    public sealed record Candidate(bool Officer, int UnitId, string Name, int FromRank, int ToRank, int Score, int Need);

    public enum Block { None, Off, Operation, Quota }

    public static bool Enabled => CampaignEconomyDefs.Current.PromotionManual;

    /// <summary>이월 때 쓸 계급: 수동 진급이 켜져 있으면 사병은 작전 시작 계급 (사살 환산 자동 진급 보류), 아니면 기존 값.</summary>
    public static int CarriedRank(MissionResults.UnitLine u) =>
        u.OfficerRank == null && Enabled ? Math.Max(0, u.StartRank) : u.Rank;

    /// <summary>[진급] 을 지금 누를 수 있는가: 규칙 꺼짐 · 작전 진행 중 (Pending) · 정원 소진이면 사유.</summary>
    public static Block BlockOf(CampaignState? s)
    {
        if (s == null || !Enabled) return Block.Off;
        if (s.Pending != null) return Block.Operation;
        if (s.PromotionsThisBreak >= CampaignEconomyDefs.Current.PromoMaxPerBreak) return Block.Quota;
        return Block.None;
    }

    static int SoldierCap => Math.Clamp(CampaignEconomyDefs.Current.PromoSoldierMaxRank, 0, 8);
    static int OfficerCap => Math.Min(Defs.Current.Officers.MaxPromotionRank, Defs.Current.Officers.MaxRank);

    static int At(int[] a, int i) => i >= 0 && i < a.Length ? a[i] : 0;

    /// <summary>한 줄의 후보 판정 (후보 아니면 null).</summary>
    static Candidate? Judge(MissionResults.UnitLine u)
    {
        var d = CampaignEconomyDefs.Current;
        if (u.OfficerRank is int o)
        {
            int need = At(d.PromoOfficerXp, o);
            int xp = u.Xp ?? 0;
            if (o < 1 || o >= OfficerCap || need <= 0 || xp < need) return null;
            return new Candidate(true, u.Id, u.Name, o, o + 1, xp, need);
        }
        int r = u.Rank;
        int to = RankNames.Next(null, r);
        int needK = At(d.PromoSoldierKills, r);
        if (r < 0 || r >= SoldierCap || to <= r || to > SoldierCap || needK <= 0 || u.Kills < needK) return null;
        return new Candidate(false, u.Id, u.Name, r, to, u.Kills, needK);
    }

    /// <summary>후보 목록: 장교 먼저 (계급 높은 순 → 경험치 → id), 그다음 사병 (계급 높은 순 → 사살 → id). 규칙이 꺼져 있으면 빈 목록. 정원과 무관하게 전부.</summary>
    public static List<Candidate> Candidates(CampaignState? s)
    {
        var res = new List<Candidate>();
        if (s == null || !Enabled) return res;
        foreach (var u in s.Officers ?? new()) if (u != null && Judge(u) is { } c) res.Add(c);
        foreach (var u in s.Units ?? new()) if (u != null && Judge(u) is { } c) res.Add(c);
        return res.OrderBy(c => c.Officer ? 0 : 1).ThenByDescending(c => c.FromRank).ThenByDescending(c => c.Score).ThenBy(c => c.UnitId).ToList();
    }

    /// <summary>후보 한 명을 한 계급 올린다. 작전 사이 + 정원 안 + 지금도 후보일 때만 (아니면 false, 상태 불변). 기록은 PromotionLog.</summary>
    public static bool Promote(CampaignState? s, bool officer, int unitId)
    {
        if (BlockOf(s) != Block.None) return false;
        var line = (officer ? s!.Officers : s!.Units).FirstOrDefault(u => u != null && u.Id == unitId && (u.OfficerRank != null) == officer);
        if (line == null || Judge(line) is not { } c) return false;
        if (officer) { line.OfficerRank = c.ToRank; }
        else { line.Rank = c.ToRank; line.StartRank = c.ToRank; line.Kills = 0; }   // 사살은 이번 진급에 쓴 것으로 친다
        s.PromotionsThisBreak++;
        s.PromotionLog.Add(new CampaignState.PromotionRecord { AfterMission = s.EntryMission ?? "", Officer = officer, UnitId = unitId, FromRank = c.FromRank, ToRank = c.ToRank });
        return true;
    }

    /// <summary>표시용 계급 이름 (캠페인 주인공 세력 = 기본 이름 한 벌).</summary>
    public static string RankName(bool officer, int rank) => officer ? RankNames.Officer(null, rank) : RankNames.Enlisted(null, rank);
}
