using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T38f] 캠페인 홈 읽기 전용 탭 (12 기록 · 5 장비 · 3 편제) 의 표시 자료. 이미 있는 CampaignState 만 읽고 아무것도 바꾸지 않는다
/// (읽기만 — T42a 부터 중대 피로 · 사기 평균도 읽는다). 그리기와 분리한 순수 함수라 selftest 가 직접 검사한다. state 가 비어 있거나 목록에 null 이 섞여도 예외 없음.
/// </summary>
public static class CampaignHomeViews
{
    // ---------------------------------------------------------------- 12 부대 기록

    public sealed record RecordRow(string Title, int Kills, int Lost, int Survivors, string SavedAt);
    public sealed record KillerRow(string Name, int Kills);
    public sealed record RecordsView(int Missions, int TotalKills, int TotalLost, List<RecordRow> Recent, List<KillerRow> Top, CampaignState.DebriefReport? Debrief);

    public const int RecentMax = 8, TopMax = 3;

    public static RecordsView Records(CampaignState? s)
    {
        var hist = (s?.History ?? new()).Where(h => h != null).ToList();
        var recent = hist.AsEnumerable().Reverse().Take(RecentMax)
            .Select(h => new RecordRow(string.IsNullOrEmpty(h.Title) ? h.MissionId ?? "" : h.Title, h.EnemyKills, h.Lost, h.Survivors, h.SavedAt ?? "")).ToList();
        var top = (s?.Units ?? new()).Where(u => u != null && u.Kills > 0).OrderByDescending(u => u.Kills).ThenBy(u => u.Id)
            .Take(TopMax).Select(u => new KillerRow(string.IsNullOrEmpty(u.Name) ? u.Unit ?? "" : u.Name, u.Kills)).ToList();
        return new RecordsView(hist.Count, hist.Sum(h => h.EnemyKills), hist.Sum(h => h.Lost), recent, top, s?.LastDebrief);
    }

    // ---------------------------------------------------------------- 5 장비 재고

    public sealed record CaliberRow(string Key, int Rifles, int Ammo, int Companies);
    public sealed record EquipView(List<CaliberRow> Calibers, List<(string Key, int Count)> Shells, List<(string Key, int Count)> Heavy, int Horses, int TotalRifles);

    public static EquipView Equipment(CampaignState? s)
    {
        var st = s?.Stock;
        var rf = st?.Rifles ?? new(); var am = st?.Ammo ?? new(); var sh = st?.Shells ?? new();
        var roster = (s?.Roster ?? new()).Where(c => c != null).ToList();
        var cal = StockKeys.AllCalibers.Select(k => new CaliberRow(k, rf.GetValueOrDefault(k), am.GetValueOrDefault(k), roster.Count(c => c.Caliber == k))).ToList();
        var shells = StockKeys.AllShells.Select(k => (k, sh.GetValueOrDefault(k))).ToList();
        var heavy = (st?.Heavy ?? new()).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, kv.Value)).ToList();
        return new EquipView(cal, shells, heavy, st?.Horses ?? 0, rf.Values.Sum());
    }

    // ---------------------------------------------------------------- 3 편제 트리 (연대 > 대대 > 중대)

    public sealed record BattalionNode(int Number, string Name, int Companies, int Strength, int MaxStrength, int Wounded, int Available, int Fatigue = 0, int Morale = 0, int Tired = 0);

    // ---------------------------------------------------------------- [T42a] 피로 · 사기 요약 (탭 1)

    public sealed record FatigueMoraleView(int Companies, int AvgFatigue, int AvgMorale, int Tired);

    /// <summary>중대 평균 피로 · 사기 (정수 반올림) + 지침 중대 수. 중대 없음 = 0 / 기본 사기.</summary>
    public static FatigueMoraleView FatigueMorale(CampaignState? s)
    {
        var cs = (s?.Roster ?? new()).Where(c => c != null).ToList();
        if (cs.Count == 0) return new FatigueMoraleView(0, 0, CampaignState.RosterCompany.MoraleStart, 0);
        return new FatigueMoraleView(cs.Count, (int)MathF.Round((float)cs.Average(c => c.Fatigue)), (int)MathF.Round((float)cs.Average(c => c.Morale)), cs.Count(CampaignEconomy.IsTired));
    }

    static int Avg(IEnumerable<int> v) { var l = v.ToList(); return l.Count == 0 ? 0 : (int)MathF.Round((float)l.Average()); }
    public sealed record RegimentNode(int Regiment, List<BattalionNode> Battalions)
    {
        public int Companies => Battalions.Sum(b => b.Companies);
        public int Strength => Battalions.Sum(b => b.Strength);
    }

    /// <summary>연대 1~9 (중대가 있는 것만, 번호순) + 직할 (Regiment 0, 마지막). 직할 중대 (BattalionNumber 0) 는 중대마다 한 줄 (Number 0, Name = 중대 이름).</summary>
    public static List<RegimentNode> Organization(CampaignState? s)
    {
        var res = new List<RegimentNode>();
        var roster = (s?.Roster ?? new()).Where(c => c != null).ToList();
        foreach (int r in Enumerable.Range(1, CampaignRegiments.Max).Append(0))
        {
            var cs = roster.Where(c => (c.Regiment is >= 1 and <= CampaignRegiments.Max ? c.Regiment : 0) == r).ToList();
            if (cs.Count == 0) continue;
            var bats = new List<BattalionNode>();
            foreach (var g in cs.Where(c => c.BattalionNumber > 0).GroupBy(c => c.BattalionNumber).OrderBy(g => g.Key))
                bats.Add(new BattalionNode(g.Key, g.First().BattalionName ?? "", g.Count(), g.Sum(c => c.Strength), g.Sum(c => c.MaxStrength), g.Sum(c => c.Wounded), g.Count(c => c.Available),
                    Avg(g.Select(c => c.Fatigue)), Avg(g.Select(c => c.Morale)), g.Count(CampaignEconomy.IsTired)));
            foreach (var c in cs.Where(c => c.BattalionNumber <= 0))
                bats.Add(new BattalionNode(0, c.Name ?? "", 1, c.Strength, c.MaxStrength, c.Wounded, c.Available ? 1 : 0, c.Fatigue, c.Morale, CampaignEconomy.IsTired(c) ? 1 : 0));
            res.Add(new RegimentNode(r, bats));
        }
        return res;
    }
}
