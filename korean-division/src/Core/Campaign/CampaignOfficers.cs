namespace KoreanDivision.Core;

/// <summary>[T48b2] 참모 효과 계산 결과 (캠페인 층만 — 전투 시뮬 입력 아님; 전투 반영은 T48e). 천분율 정수.</summary>
public sealed class StaffEffect
{
    /// <summary>작전 정산 때 정기 보급에 더하는 비율 (supply 슬롯).</summary>
    public int SupplyBonusPermille { get; set; }
    /// <summary>다음 작전 브리핑에 더 보이는 적 정보 줄 수 (intel 슬롯; 표시는 UI).</summary>
    public int IntelInfo { get; set; }
    /// <summary>연구 속도 보정 (research 슬롯; 연구 탭은 후속).</summary>
    public int ResearchSpeedPermille { get; set; }
    /// <summary>학파 보정 (doctrine 슬롯): 해당 학파 효과에 더하는 비율. 학파가 없으면 "없음".</summary>
    public int DoctrinePermille { get; set; }
    public string DoctrineSchool { get; set; } = "";
}

/// <summary>[T48b2] 참모 슬롯 4종 효과 계산. 가중 = 1000 + 100*(계급-4) + 50*경험치단계 (JSON, 임시), 정수 나눗셈 (0 쪽으로).</summary>
public static class StaffEffects
{
    public const string Supply = "supply", Intel = "intel", Research = "research", Doctrine = "doctrine";
    public static readonly string[] Slots = { Supply, Intel, Research, Doctrine };

    public static int XpTier(int xp)
    {
        int t = 0;
        foreach (int step in CommanderAssignRules.Current.Staff.XpTierSteps) if (xp >= step) t++;
        return t;
    }

    public static int WeightPermille(int rank, int xp)
    {
        var st = CommanderAssignRules.Current.Staff;
        long w = 1000 + (long)st.RankStepPermille * (rank - st.RankBase) + (long)st.XpTierStepPermille * XpTier(xp);
        return (int)Math.Clamp(w, st.MinWeightPermille, st.MaxWeightPermille);
    }

    /// <summary>그 슬롯에 배정된 (전사하지 않은) 참모 장교.</summary>
    public static CampaignState.CampaignOfficer? In(CampaignState s, string slot) =>
        s.CampaignOfficers.Where(o => o.Assignment == "staff" && o.StaffSlot == slot && o.Status != OfficerIdentity.Dead).OrderBy(o => o.Key).FirstOrDefault();

    public static StaffEffect Compute(CampaignState s)
    {
        var st = CommanderAssignRules.Current.Staff;
        var e = new StaffEffect();
        if (In(s, Supply) is { } a) e.SupplyBonusPermille = (int)((long)st.SupplyBasePermille * WeightPermille(a.Rank, a.Xp) / 1000);
        if (In(s, Intel) is { } b) e.IntelInfo = (int)((long)st.IntelBaseInfo * WeightPermille(b.Rank, b.Xp) / 1000);
        if (In(s, Research) is { } c) e.ResearchSpeedPermille = (int)((long)st.ResearchBasePermille * WeightPermille(c.Rank, c.Xp) / 1000);
        if (In(s, Doctrine) is { } d) { e.DoctrinePermille = (int)((long)st.DoctrineBasePermille * WeightPermille(d.Rank, d.Xp) / 1000); e.DoctrineSchool = d.School.Length > 0 ? d.School : "없음"; }
        return e;
    }

    /// <summary>작전 정산 때 정기 보급 (LastDebrief.RegularSupplyAdded) 의 supply 보너스 비율만큼 보급을 더한다 (정수 내림). 더한 양.</summary>
    public static int ApplySupplyBonus(CampaignState s)
    {
        int p = Compute(s).SupplyBonusPermille;
        if (p <= 0 || s.LastDebrief == null) return 0;
        int add = (int)((long)s.LastDebrief.RegularSupplyAdded * p / 1000);
        if (add <= 0) return 0;
        s.Supply += add; s.LastDebrief.SupplyAfter = s.Supply;
        return add;
    }
}

/// <summary>
/// [T48b2] 장교 배정: 종착(TerminalReady) 장교를 대대장 (빈 대대) 또는 사단 참모 4슬롯 (supply intel research doctrine) 에 배정. 겸임 불가 (한 사람 = 한 자리, 새 배정 = 이동),
/// 한 자리 = 한 사람. 작전 사이 (Pending == null) 에만. 대대장만 참전 (OfficerIdentity.CanDeploy), 전사 = 영구 상실 (자리는 정산 때 비워짐).
/// </summary>
public static class OfficerAssign
{
    /// <summary>배정 후보 = 종착 장교 중 전사하지 않은 사람 (현재 배정과 무관 — 재배정 가능). Key 순.</summary>
    public static List<CampaignState.CampaignOfficer> Candidates(CampaignState s) =>
        s.CampaignOfficers.Where(o => o.TerminalReady && o.Status != OfficerIdentity.Dead).OrderBy(o => o.Key).ToList();

    /// <summary>대대 번호 (직할 0 제외) 목록과 그 대대장 Key (없으면 0).</summary>
    public static List<(int Battalion, int Key)> Battalions(CampaignState s) =>
        s.Roster.Where(c => c.BattalionNumber > 0).Select(c => c.BattalionNumber).Distinct().OrderBy(b => b)
            .Select(b => (b, s.CampaignOfficers.Where(o => o.Assignment == "battalion" && o.Battalion == b && o.Status != OfficerIdentity.Dead).Select(o => o.Key).DefaultIfEmpty(0).Min())).ToList();

    static AssignResult Common(CampaignState s, int key, out CampaignState.CampaignOfficer? o)
    {
        o = OfficerIdentity.Find(s, key);
        if (s.Pending != null) return AssignResult.No("작전 진행 중에는 배정할 수 없다");
        if (o == null) return AssignResult.No("명부에 없는 장교");
        if (o.Status == OfficerIdentity.Dead) return AssignResult.No("전사한 장교");
        if (!o.TerminalReady) return AssignResult.No("아직 진급 종착(중령)에 이르지 않았다");
        return AssignResult.Yes;
    }

    public static AssignResult CanAssignBattalion(CampaignState s, int key, int battalion)
    {
        var c = Common(s, key, out var o);
        if (!c.Ok) return c;
        if (!s.Roster.Any(r => r.BattalionNumber == battalion && battalion > 0)) return AssignResult.No("없는 대대");
        if (o!.Status == OfficerIdentity.Wounded) return AssignResult.No("부상 중에는 대대장에 새로 배정할 수 없다");
        if (o.Assignment == "staff" && !CommanderAssignRules.Current.StaffBackToBattalion) return AssignResult.No("참모는 대대장으로 돌아갈 수 없다");
        if (o.Assignment == "battalion" && o.Battalion == battalion) return AssignResult.No("이미 그 대대장");
        if (Battalions(s).FirstOrDefault(b => b.Battalion == battalion).Key is int other and > 0 && other != key) return AssignResult.No($"제{battalion}대대에는 이미 대대장이 있다");
        return AssignResult.Yes;
    }

    public static AssignResult AssignBattalion(CampaignState s, int key, int battalion)
    {
        var chk = CanAssignBattalion(s, key, battalion);
        if (!chk.Ok) return chk;
        var o = OfficerIdentity.Find(s, key)!;
        o.Assignment = "battalion"; o.Battalion = battalion; o.StaffSlot = "";
        s.AddTimeline("commander", "assign:" + key, o.Name, battalion);
        CommanderPoolOps.Journal(s, $"{o.Name} 제{battalion}대대장 배정");
        return AssignResult.Yes;
    }

    public static AssignResult CanAssignStaff(CampaignState s, int key, string slot)
    {
        var c = Common(s, key, out var o);
        if (!c.Ok) return c;
        if (Array.IndexOf(StaffEffects.Slots, slot) < 0) return AssignResult.No("없는 참모 자리");
        if (o!.Assignment == "staff" && o.StaffSlot == slot) return AssignResult.No("이미 그 자리");
        if (StaffEffects.In(s, slot) is { } other && other.Key != key) return AssignResult.No($"그 참모 자리에는 이미 {other.Name} 이(가) 있다");
        return AssignResult.Yes;
    }

    public static AssignResult AssignStaff(CampaignState s, int key, string slot)
    {
        var chk = CanAssignStaff(s, key, slot);
        if (!chk.Ok) return chk;
        var o = OfficerIdentity.Find(s, key)!;
        o.Assignment = "staff"; o.StaffSlot = slot; o.Battalion = 0;
        s.AddTimeline("commander", "assign:" + key, o.Name, Array.IndexOf(StaffEffects.Slots, slot) + 1);
        CommanderPoolOps.Journal(s, $"{o.Name} 참모({slot}) 배정");
        return AssignResult.Yes;
    }

    /// <summary>배정 해제 (작전 사이만). 대기 상태로.</summary>
    public static AssignResult Unassign(CampaignState s, int key)
    {
        if (s.Pending != null) return AssignResult.No("작전 진행 중에는 배정할 수 없다");
        var o = OfficerIdentity.Find(s, key);
        if (o == null) return AssignResult.No("명부에 없는 장교");
        if (o.Assignment.Length == 0) return AssignResult.No("배정되어 있지 않다");
        o.Assignment = ""; o.Battalion = 0; o.StaffSlot = "";
        s.AddTimeline("commander", "unassign:" + key, o.Name);
        return AssignResult.Yes;
    }

    /// <summary>전사한 장교의 자리를 비운다 (정산 뒤). 비운 수.</summary>
    public static int ClearDead(CampaignState s)
    {
        int n = 0;
        foreach (var o in s.CampaignOfficers)
            if (o.Status == OfficerIdentity.Dead && (o.Assignment.Length > 0 || o.Battalion != 0 || o.StaffSlot.Length > 0)) { o.Assignment = ""; o.Battalion = 0; o.StaffSlot = ""; n++; }
        return n;
    }

    /// <summary>[UI 카드 T48d] 배정 한 줄: 종류 / 위치 문구.</summary>
    public static string Describe(CampaignState.CampaignOfficer o) =>
        o.Assignment == "battalion" ? $"제{o.Battalion}대대장" : o.Assignment == "staff" ? $"참모({o.StaffSlot})" : o.TerminalReady ? "대기" : "중대";

    /// <summary>저장 불러오기 보정 (Sanitize): 모르는 배정 · 자리는 해제, 같은 자리에 둘이면 Key 가 작은 쪽만 유지 (겸임 불가 · 자리당 1명).</summary>
    public static void Sanitize(CampaignState s)
    {
        var battalions = new HashSet<int>(); var slots = new HashSet<string>();
        foreach (var o in s.CampaignOfficers.OrderBy(o => o.Key))
        {
            bool ok = o.Status != OfficerIdentity.Dead && o.Assignment switch
            {
                "battalion" => o.Battalion > 0 && battalions.Add(o.Battalion),
                "staff" => Array.IndexOf(StaffEffects.Slots, o.StaffSlot) >= 0 && slots.Add(o.StaffSlot),
                _ => false,
            };
            if (!ok) { o.Assignment = ""; o.Battalion = 0; o.StaffSlot = ""; }
            else if (o.Assignment == "battalion") o.StaffSlot = ""; else o.Battalion = 0;
        }
    }
}
