using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>[T48d] 버튼 상태 = T48b 함수 결과 그대로 (UI 에 규칙 없음). Visible=false 면 버튼 자리를 그리지 않는다 (해임 불가 인물의 [해임]).</summary>
public readonly record struct CmdBtn(bool Visible, bool Enabled, string Reason)
{
    public static CmdBtn Hidden => new(false, false, "");
    public static CmdBtn From(AssignResult r) => new(true, r.Ok, r.Reason);
}

/// <summary>[T48d] 연대장 · 장교 관리 화면의 표시 자료 (Raylib 없이 시험 가능). 상태를 바꾸지 않는다 — 바꾸는 일은 CommanderPoolOps / OfficerAssign 호출뿐.</summary>
public static class CommanderHomeModel
{
    public sealed record Card(string Id, string Name, string Rank, string School, string Origin, string Status, string StatusLabel, int Regiment, int Favor, string TierId, string TierName,
        bool Unfireable, string UnfireableNote, bool Locked, string Summary, string Portrait, List<string> TraitNames);

    public sealed record StaffRow(string Slot, string SlotLabel, int OfficerKey, string OfficerName, string Effect);
    public sealed record BattalionRow(int Battalion, int OfficerKey, string OfficerName, string Detail);
    public sealed record OfficerRow(int Key, string Name, int Rank, int Xp, string School, string Where, string Status);

    /// <summary>연대장 규칙이 켜져 있나: 연대장 명단 데이터가 있을 때만 (없으면 기존 학파 화면 + '규칙 꺼짐' 안내).</summary>
    public static bool RulesOn => CommanderCatalog.Current.Defs.Count > 0;

    static CommanderTraitDef? Trait(string id) => Vocab.Traits.TryGetValue(id, out var t) ? t : null;
    static CommanderCatalog.Vocab? vocab;
    static CommanderCatalog.Vocab Vocab => vocab ??= LoadVocab();
    static CommanderCatalog.Vocab LoadVocab()
    {
        try { return CommanderCatalog.LoadVocab(Path.Combine(AppContext.BaseDirectory, "assets", "data"), new CommanderLoadResult()); }
        catch (Exception) { return new CommanderCatalog.Vocab(); }
    }

    public static string StatusLabel(CampaignState.CommanderSlot slot) => slot.Status switch
    {
        CommanderStatus.Hired => string.Format(S.CmdStHired, slot.Regiment),
        CommanderStatus.Available => S.CmdStAvailable,
        CommanderStatus.Dismissed => S.CmdStDismissed,
        CommanderStatus.Unavailable => S.CmdStUnavailable,
        _ => S.CmdStLocked,
    };

    public static Card? CardOf(CampaignState s, string id)
    {
        var slot = s.CommanderPool.FirstOrDefault(c => c.Id == id);
        if (slot == null) return null;
        var def = CommanderCatalog.Find(id);
        bool locked = slot.Status == CommanderStatus.Locked;
        var tier = CommanderFavors.TierOf(slot.Favor);
        if (def == null) return new Card(id, S.CmdUnknown, "", "", "", slot.Status, StatusLabel(slot), slot.Regiment, slot.Favor, tier.Id, tier.Name, false, "", locked, "", "", new());
        return new Card(id, locked ? S.CmdLockedName : def.Name, locked ? "" : def.Rank, locked ? "" : def.School, locked ? "" : def.Origin, slot.Status, StatusLabel(slot), slot.Regiment, slot.Favor,
            tier.Id, tier.Name, def.Unfireable, def.UnfireableNote, locked, locked ? "" : def.BattleSummary, def.Portrait,
            locked ? new() : def.Traits.Select(t => Trait(t)?.Name ?? t).ToList());
    }

    /// <summary>연대 1~9 별 재직 연대장 카드 (공석 = null). 이 연대는 연대장이 붙는 번호 (중대가 아님).</summary>
    public static List<(int Regiment, Card? Card)> RegimentBoard(CampaignState s)
    {
        var rows = new List<(int, Card?)>();
        for (int r = 1; r <= CampaignRegiments.Max; r++)
            rows.Add((r, CommanderPoolOps.HiredIn(s, r) is { } id ? CardOf(s, id) : null));
        return rows;
    }

    /// <summary>후보 풀 필터: all / available / hired / dismissed / locked. 순서 = 풀 순서 (결정적).</summary>
    public static readonly string[] Filters = { "all", CommanderStatus.Available, CommanderStatus.Hired, CommanderStatus.Dismissed, CommanderStatus.Locked };
    public static List<Card> Pool(CampaignState s, string filter)
    {
        var list = new List<Card>();
        foreach (var slot in s.CommanderPool)
        {
            if (filter != "all" && slot.Status != filter) continue;
            if (CardOf(s, slot.Id) is { } c) list.Add(c);
        }
        return list;
    }

    /// <summary>[기용] 버튼: 그 연대에 기용이 되나 (T48b CanHire 결과 그대로).</summary>
    public static CmdBtn HireBtn(CampaignState s, string id, int regiment) => CmdBtn.From(CommanderPoolOps.CanHire(s, id, regiment));

    /// <summary>[해임] 버튼: 해임 불가 인물에게는 버튼 자체가 없다 (Visible=false). 재직 중이 아니면 그리지 않는다.</summary>
    public static CmdBtn DismissBtn(CampaignState s, string id)
    {
        var slot = s.CommanderPool.FirstOrDefault(c => c.Id == id);
        var def = CommanderCatalog.Find(id);
        if (slot == null || def == null || slot.Status != CommanderStatus.Hired || def.Unfireable) return CmdBtn.Hidden;
        return CmdBtn.From(CommanderPoolOps.CanDismiss(s, id));
    }

    /// <summary>비어 있는 첫 연대 번호 (없으면 0). 후보 풀의 [기용] 이 이 연대로 기용한다 (연대 선택은 ◀ ▶ 로 바꿀 수 있다).</summary>
    public static int FirstVacantRegiment(CampaignState s)
    {
        for (int r = 1; r <= CampaignRegiments.Max; r++) if (CommanderPoolOps.HiredIn(s, r) == null) return r;
        return 0;
    }

    /// <summary>다음 빈 연대 (from 다음부터 한 바퀴, 없으면 0).</summary>
    public static int NextVacantRegiment(CampaignState s, int from, int dir)
    {
        for (int i = 1; i <= CampaignRegiments.Max; i++)
        {
            int r = ((from - 1 + dir * i) % CampaignRegiments.Max + CampaignRegiments.Max) % CampaignRegiments.Max + 1;
            if (CommanderPoolOps.HiredIn(s, r) == null) return r;
        }
        return 0;
    }

    /// <summary>해임 확인 대화상자 본문: 호감 변화 예고 필수 (해임 때 깎는 값은 CommanderCatalog.DismissPenalty, 같은 값을 T48b 가 적용).</summary>
    public static string DismissNotice(CampaignState s, string id)
    {
        var def = CommanderCatalog.Find(id);
        var slot = s.CommanderPool.FirstOrDefault(c => c.Id == id);
        if (def == null || slot == null) return "";
        int p = CommanderCatalog.DismissPenalty(def);
        return string.Format(S.CmdDismissAsk, def.Name, slot.Regiment) + "\n" + (p > 0 ? string.Format(S.CmdDismissPenalty, p) : S.CmdDismissNoPenalty);
    }

    public static string HireNotice(CampaignState s, string id, int regiment) =>
        string.Format(S.CmdHireAsk, CommanderCatalog.Find(id)?.Name ?? id, regiment);

    // ---------------------------------------------------------------- 대대장 · 참모

    public static string SlotLabel(string slot) => slot switch
    {
        StaffEffects.Supply => S.CmdSlotSupply, StaffEffects.Intel => S.CmdSlotIntel, StaffEffects.Research => S.CmdSlotResearch, StaffEffects.Doctrine => S.CmdSlotDoctrine, _ => slot,
    };

    /// <summary>참모 4슬롯 한 줄씩: 배정된 장교 + StaffEffects.Compute 의 효과 수치 (천분율 → %, 정보는 줄 수). 비어 있으면 효과 0 표시.</summary>
    public static List<StaffRow> StaffBoard(CampaignState s)
    {
        var e = StaffEffects.Compute(s);
        var rows = new List<StaffRow>();
        foreach (var slot in StaffEffects.Slots)
        {
            var o = StaffEffects.In(s, slot);
            string eff = slot switch
            {
                StaffEffects.Supply => string.Format(S.CmdEffSupply, Pct(e.SupplyBonusPermille)),
                StaffEffects.Intel => string.Format(S.CmdEffIntel, e.IntelInfo),
                StaffEffects.Research => string.Format(S.CmdEffResearch, Pct(e.ResearchSpeedPermille)),
                _ => string.Format(S.CmdEffDoctrine, Pct(e.DoctrinePermille), o == null ? S.CmdNone : e.DoctrineSchool),
            };
            rows.Add(new StaffRow(slot, SlotLabel(slot), o?.Key ?? 0, o?.Name ?? "", eff));
        }
        return rows;
    }

    /// <summary>천분율 → "5.0" 형 문자열 (정수 연산만; 표시 전용).</summary>
    public static string Pct(int permille) => $"{permille / 10}.{Math.Abs(permille % 10)}";

    public static List<BattalionRow> BattalionBoard(CampaignState s) =>
        OfficerAssign.Battalions(s).Select(b =>
        {
            var o = b.Key > 0 ? OfficerIdentity.Find(s, b.Key) : null;
            return new BattalionRow(b.Battalion, b.Key, o?.Name ?? "", o == null ? "" : OfficerDetail(o));
        }).ToList();

    public static string OfficerDetail(CampaignState.CampaignOfficer o) =>
        string.Format(S.CmdOfficerDetail, CampaignPromotion.RankName(true, o.Rank), o.Xp, o.School.Length > 0 ? o.School : S.CmdNone) +
        (o.Status == OfficerIdentity.Wounded ? " " + string.Format(S.CmdWoundedUntil, o.WoundedUntilMission) : "");

    /// <summary>종착 대기 장교 (OfficerAssign.Candidates) 한 줄 자료.</summary>
    public static List<OfficerRow> Terminal(CampaignState s) =>
        OfficerAssign.Candidates(s).Select(o => new OfficerRow(o.Key, o.Name, o.Rank, o.Xp, o.School, o.Assignment == "staff" ? string.Format(S.CmdWhereStaff, SlotLabel(o.StaffSlot)) : OfficerAssign.Describe(o), o.Status)).ToList();

    public static CmdBtn BattalionBtn(CampaignState s, int key, int battalion) => CmdBtn.From(OfficerAssign.CanAssignBattalion(s, key, battalion));
    public static CmdBtn StaffBtn(CampaignState s, int key, string slot) => CmdBtn.From(OfficerAssign.CanAssignStaff(s, key, slot));

    /// <summary>[대대장으로] 버튼이 향할 대대: 대대장이 없는 첫 대대 (없으면 이미 그 자리가 아닌 첫 대대, 그것도 없으면 0). 규칙 판정은 CanAssignBattalion.</summary>
    public static int TargetBattalion(CampaignState s, int key)
    {
        var bs = OfficerAssign.Battalions(s);
        foreach (var b in bs) if (b.Key == 0) return b.Battalion;
        foreach (var b in bs) if (b.Key != key) return b.Battalion;
        return bs.Count > 0 ? bs[0].Battalion : 0;
    }

    // ---------------------------------------------------------------- 연표 (T42d 방식: id 로 문구를 찾고 저장 안 함)

    /// <summary>commander 종류 연표 줄의 문구. 모르는 id 는 id 그대로. 예외 없음.</summary>
    public static string TimelineText(CampaignState? s, CampaignState.TimelineEntry e)
    {
        string kind = e.Id, arg = "";
        int cut = e.Id.IndexOf(':');
        if (cut >= 0) { kind = e.Id[..cut]; arg = e.Id[(cut + 1)..]; }
        string name = e.Title.Length > 0 ? e.Title : arg;
        switch (kind)
        {
            case "hire": return string.Format(S.CmdTlHire, name, e.Choice);
            case "dismiss": return string.Format(S.CmdTlDismiss, name, e.Choice);
            case "dead": return string.Format(S.CmdTlDead, name);
            case "wounded": return string.Format(S.CmdTlWounded, name, e.Choice);
            case "unassign": return string.Format(S.CmdTlUnassign, name);
            case "assign":
            {
                if (int.TryParse(arg, out int key) && s != null && OfficerIdentity.Find(s, key) is { } o)
                {
                    if (o.Assignment == "battalion" && o.Battalion == e.Choice) return string.Format(S.CmdTlAssignBat, name, e.Choice);
                    if (o.Assignment == "staff" && Array.IndexOf(StaffEffects.Slots, o.StaffSlot) + 1 == e.Choice) return string.Format(S.CmdTlAssignStaff, name, SlotLabel(o.StaffSlot));
                }
                return string.Format(S.CmdTlAssign, name);
            }
            default: return e.Id;
        }
    }

    /// <summary>글꼴 아틀라스에 넣을 데이터 글자 (연대장 이름 · 약력 · 출신 · 특성 이름 등 — S 상수 밖의 글자).</summary>
    public static IEnumerable<string> AllText()
    {
        foreach (var d in CommanderCatalog.Current.Defs)
        {
            yield return d.Name; yield return d.Rank; yield return d.Origin; yield return d.School; yield return d.Bio; yield return d.BattleSummary;
            yield return d.UnfireableNote; yield return d.Hire.Condition;
            foreach (var t in d.Favor.Likes) yield return t;
            foreach (var t in d.Favor.Dislikes) yield return t;
        }
        foreach (var t in Vocab.Traits.Values) { yield return t.Name; yield return t.Desc; }
        foreach (var t in Vocab.Tags) yield return t;
        foreach (var t in CommanderFavorRules.Current.Tiers) yield return t.Name;
    }
}
