using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>[T48b2] 기용/해임/배정/참모 규칙 데이터 (assets/data/commander_assign.json). 파일 없음 · 깨짐 = 기본값 + 경고. 전부 임시값.</summary>
public sealed class CommanderAssignRules
{
    public sealed class TagRules
    {
        public int LowLossPermille { get; set; } = 100;
        public int FastSeconds { get; set; } = 900;
        public int HeavyLossPermille { get; set; } = 300;
    }
    public sealed class StaffRules
    {
        public int RankBase { get; set; } = 4;
        public int RankStepPermille { get; set; } = 100;
        public int XpTierStepPermille { get; set; } = 50;
        public int[] XpTierSteps { get; set; } = { 60, 180, 420, 900 };
        public int MinWeightPermille { get; set; } = 500;
        public int MaxWeightPermille { get; set; } = 2000;
        public int SupplyBasePermille { get; set; } = 50;
        public int IntelBaseInfo { get; set; } = 2;
        public int ResearchBasePermille { get; set; } = 100;
        public int DoctrineBasePermille { get; set; } = 50;
    }
    /// <summary>기용 비용(채용 비용 칸)을 보급으로 실제 차감할지. 비용 단위가 미정이라 기본 꺼짐 (1단계 비용 0 허용).</summary>
    public bool ChargeHireCost { get; set; }
    public int HireCostSupplyUnit { get; set; } = 100;
    /// <summary>true 면 그 연대에 중대가 하나 이상 있어야 기용 · minRegiments(편성된 연대 수) 를 지켜야 기용. 기본 꺼짐 (새 캠페인은 전 중대가 직할이라 막힘).</summary>
    public bool RequireRegimentCompanies { get; set; }
    public bool EnforceMinRegiments { get; set; }
    /// <summary>참모 -&gt; 대대장 복귀 허용 (Q-C7 기본 허용).</summary>
    public bool StaffBackToBattalion { get; set; } = true;
    public TagRules Tags { get; set; } = new();
    public StaffRules Staff { get; set; } = new();
    public List<string> Warnings { get; } = new();

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "assets", "data", "commander_assign.json");
    static CommanderAssignRules? current;
    public static CommanderAssignRules Current => current ??= Load(FilePath);
    public static void Override(CommanderAssignRules? r) => current = r;

    public static CommanderAssignRules Load(string path)
    {
        if (!File.Exists(path)) { var d = new CommanderAssignRules(); d.Warnings.Add("commander_assign.json 없음: 기본값 사용"); return d; }
        try
        {
            var r = JsonSerializer.Deserialize<CommanderAssignRules>(File.ReadAllText(path), DataJson.Opts) ?? new CommanderAssignRules();
            r.Tags ??= new(); r.Staff ??= new();
            var st = r.Staff;
            if (st.XpTierSteps == null || st.XpTierSteps.Length == 0) st.XpTierSteps = new[] { 60, 180, 420, 900 };
            st.MinWeightPermille = Math.Clamp(st.MinWeightPermille, 0, 1000);
            st.MaxWeightPermille = Math.Clamp(st.MaxWeightPermille, 1000, 10000);
            r.HireCostSupplyUnit = Math.Clamp(r.HireCostSupplyUnit, 0, 100000);
            r.Tags.LowLossPermille = Math.Clamp(r.Tags.LowLossPermille, 0, 1000);
            r.Tags.HeavyLossPermille = Math.Clamp(r.Tags.HeavyLossPermille, 0, 1000);
            r.Tags.FastSeconds = Math.Max(0, r.Tags.FastSeconds);
            st.SupplyBasePermille = Math.Clamp(st.SupplyBasePermille, 0, 1000); st.ResearchBasePermille = Math.Clamp(st.ResearchBasePermille, 0, 1000);
            st.DoctrineBasePermille = Math.Clamp(st.DoctrineBasePermille, 0, 1000); st.IntelBaseInfo = Math.Clamp(st.IntelBaseInfo, 0, 20);
            return r;
        }
        catch (JsonException e) { var d = new CommanderAssignRules(); d.Warnings.Add("commander_assign.json 깨짐: 기본값 사용 (" + e.Message + ")"); return d; }
    }
}

/// <summary>기용/해임/배정 시도 결과 (UI 가 사유 문구를 그대로 보여 준다).</summary>
public readonly record struct AssignResult(bool Ok, string Reason)
{
    public static AssignResult Yes => new(true, "");
    public static AssignResult No(string why) => new(false, why);
}

/// <summary>
/// [T48b2] 연대장 풀 상태 기계: locked -&gt; available -&gt; hired -&gt; dismissed (쿨다운 뒤 재기용). 작전 사이 (Pending == null) 에만. 정수 · 목록 순서 (결정적).
/// 호감: 해임 = <see cref="CommanderFavors.ApplyDismissPenalty"/> (상태 전이가 한 번만 일어나므로 하락도 한 번), 재기용 = <see cref="CommanderFavors.CanRehire"/>, 작전 종료 태그 = <see cref="OnSettle"/>.
/// </summary>
public static class CommanderPoolOps
{
    static CampaignState.CommanderSlot? Slot(CampaignState s, string id) => s.CommanderPool.FirstOrDefault(c => c.Id == id);
    static string NameOf(string id) => CommanderCatalog.Find(id)?.Name ?? id;

    /// <summary>locked -&gt; available (등장 조건: 작전 완료 · 사건 처리 · 플래그), until 사건이 일어나면 재직 중이 아닌 인물은 unavailable. 바뀐 수.</summary>
    public static int RefreshAvailability(CampaignState s)
    {
        int n = 0;
        foreach (var slot in s.CommanderPool)
        {
            var def = CommanderCatalog.Find(slot.Id);
            if (def == null) continue;
            var a = def.Availability;
            if (a.Until.Length > 0 && s.Flags.GetValueOrDefault("ev_" + a.Until, 0) != 0 && slot.Status is CommanderStatus.Locked or CommanderStatus.Available or CommanderStatus.Dismissed)
            { slot.Status = CommanderStatus.Unavailable; n++; continue; }
            if (slot.Status != CommanderStatus.Locked) continue;
            bool ok = (a.FromMission.Length == 0 || a.FromMission == "start" || s.Completed.Contains(a.FromMission))
                && a.RequireEvents.All(e => s.Flags.GetValueOrDefault("ev_" + e, 0) != 0)
                && a.RequireFlags.All(f => s.Flags.GetValueOrDefault(f, 0) != 0);
            if (ok) { slot.Status = CommanderStatus.Available; n++; }
        }
        return n;
    }

    /// <summary>이 연대에 재직 중인 연대장 id (없으면 null).</summary>
    public static string? HiredIn(CampaignState s, int regiment) => s.CommanderPool.FirstOrDefault(c => c.Status == CommanderStatus.Hired && c.Regiment == regiment)?.Id;

    public static AssignResult CanHire(CampaignState s, string id, int regiment)
    {
        var rules = CommanderAssignRules.Current;
        if (s.Pending != null) return AssignResult.No("작전 진행 중에는 연대장을 바꿀 수 없다");
        var slot = Slot(s, id); var def = CommanderCatalog.Find(id);
        if (slot == null || def == null) return AssignResult.No("명단에 없는 인물");
        switch (slot.Status)
        {
            case CommanderStatus.Hired: return AssignResult.No("이미 재직 중");
            case CommanderStatus.Locked: return AssignResult.No("아직 등장하지 않은 인물");
            case CommanderStatus.Unavailable: return AssignResult.No("더는 기용할 수 없는 인물");
            case CommanderStatus.Dismissed:
                if (!CommanderFavors.CanRehire(s, id, out string why)) return AssignResult.No(why);
                break;
        }
        if (regiment < 1 || regiment > CampaignRegiments.Max) return AssignResult.No($"연대 번호는 1~{CampaignRegiments.Max}");
        if (HiredIn(s, regiment) is { } other) return AssignResult.No($"{regiment}연대에는 이미 연대장({NameOf(other)})이 있다");
        if (rules.RequireRegimentCompanies && !s.Roster.Any(c => c.Regiment == regiment)) return AssignResult.No($"{regiment}연대에 편성된 중대가 없다");
        if (rules.EnforceMinRegiments && s.Roster.Where(c => c.Regiment > 0).Select(c => c.Regiment).Distinct().Count() < def.Hire.MinRegiments)
            return AssignResult.No($"편성된 연대가 {def.Hire.MinRegiments}개 이상이어야 한다");
        if (rules.ChargeHireCost && def.Hire.Cost > 0 && s.Supply < (long)def.Hire.Cost * rules.HireCostSupplyUnit) return AssignResult.No("보급이 모자라 기용할 수 없다");
        return AssignResult.Yes;
    }

    public static AssignResult Hire(CampaignState s, string id, int regiment)
    {
        var chk = CanHire(s, id, regiment);
        if (!chk.Ok) return chk;
        var slot = Slot(s, id)!; var def = CommanderCatalog.Find(id)!;
        var rules = CommanderAssignRules.Current;
        if (rules.ChargeHireCost && def.Hire.Cost > 0) s.Supply -= (long)def.Hire.Cost * rules.HireCostSupplyUnit;
        slot.Status = CommanderStatus.Hired; slot.Regiment = regiment; slot.HiredDay = Math.Max(s.Day, 1);
        slot.TimesHired = (int)Math.Min(100000, (long)slot.TimesHired + 1);
        s.AddTimeline("commander", "hire:" + id, def.Name, regiment);
        Journal(s, $"{def.Name} {regiment}연대장 기용");
        return AssignResult.Yes;
    }

    public static AssignResult CanDismiss(CampaignState s, string id)
    {
        if (s.Pending != null) return AssignResult.No("작전 진행 중에는 연대장을 바꿀 수 없다");
        var slot = Slot(s, id); var def = CommanderCatalog.Find(id);
        if (slot == null || def == null) return AssignResult.No("명단에 없는 인물");
        if (slot.Status != CommanderStatus.Hired) return AssignResult.No("재직 중이 아님");
        if (def.Unfireable) return AssignResult.No(def.UnfireableNote.Length > 0 ? def.UnfireableNote : "해임할 수 없는 인물");
        return AssignResult.Yes;
    }

    /// <summary>해임: hired -&gt; dismissed, 연대 배속 해제, 호감 하락 (한 번), 연표 + 일지.</summary>
    public static AssignResult Dismiss(CampaignState s, string id)
    {
        var chk = CanDismiss(s, id);
        if (!chk.Ok) return chk;
        var slot = Slot(s, id)!;
        int reg = slot.Regiment;
        slot.Status = CommanderStatus.Dismissed; slot.Regiment = 0; slot.DismissedDay = Math.Max(s.Day, 1);
        int fav = CommanderFavors.ApplyDismissPenalty(s, id);
        s.AddTimeline("commander", "dismiss:" + id, NameOf(id), reg);
        Journal(s, $"{NameOf(id)} 해임" + (fav < 0 ? $" (호감 {fav})" : ""));
        return AssignResult.Yes;
    }

    /// <summary>부대 일지 (보급 입고 로그와 같은 줄 목록, 최대 5건).</summary>
    internal static void Journal(CampaignState s, string text)
    {
        s.Stock.RecentLog.Add($"[Day {Math.Max(s.Day, 1)}] {text}");
        while (s.Stock.RecentLog.Count > 5) s.Stock.RecentLog.RemoveAt(0);
    }

    /// <summary>임시 태그 규칙 (commander_assign.json tags): 승리 작전에서 인명보존 (아군 손실 &lt;= lowLossPermille) · 속전속결 (fastSeconds 이내) · 공세 (손실 &gt;= heavyLossPermille). 호감 어휘는 commander_tags.json.</summary>
    public static List<string> DeriveTags(MissionResults r)
    {
        var tags = new List<string>();
        if (r.Result != "won") return tags;
        var t = CommanderAssignRules.Current.Tags;
        var pf = r.Factions.FirstOrDefault(f => f.Player);
        int loss = pf == null || pf.Spawned <= 0 ? 0 : (int)((long)pf.Lost * 1000 / pf.Spawned);
        if (loss <= t.LowLossPermille) tags.Add("인명보존");
        if (r.DurationSeconds <= t.FastSeconds) tags.Add("속전속결");
        if (loss >= t.HeavyLossPermille) tags.Add("공세");
        return tags;
    }

    /// <summary>첫 승리 정산 뒤 호출 (CampaignState.Apply): 풀 등장 갱신 · 작전 태그 호감 (작전당 1회) · 전사자 배정 해제 · 참모 보급 보너스.</summary>
    public static void OnSettle(CampaignState s, MissionResults r)
    {
        RefreshAvailability(s);
        CommanderFavors.ApplyMissionTags(s, r.MissionId, DeriveTags(r));
        OfficerAssign.ClearDead(s);
        StaffEffects.ApplySupplyBonus(s);
    }
}
