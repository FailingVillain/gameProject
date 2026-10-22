namespace KoreanDivision.Core;

/// <summary>[C1E1-g1] 캠페인 변수 (정수). 지금은 CampaignState.Flags 위의 얇은 층 — TC-E 가 레지스트리로 바꿔 끼운다.</summary>
public interface ICampaignVars
{
    /// <summary>없는 키 = 0.</summary>
    int Get(string key);
    void Set(string key, int value);
    bool Has(string key);
    /// <summary>전체 복사 (키 서수 정렬 — 결정적).</summary>
    List<EpisodeVar> SnapshotAll();
    /// <summary>전체를 복사본으로 바꾼다 (복사본에 없는 키는 지운다).</summary>
    void RestoreAll(IReadOnlyList<EpisodeVar> vars);
}

/// <summary>[C1E1-g1] 변수 한 칸 (저장 · 체크포인트용).</summary>
public sealed class EpisodeVar
{
    public string Key { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>[C1E1-g1] CampaignState.Flags 를 변수 저장소로 쓰는 임시 구현.</summary>
public sealed class FlagsCampaignVars : ICampaignVars
{
    readonly CampaignState s;
    public FlagsCampaignVars(CampaignState s) { this.s = s; s.Flags ??= new(); }
    public int Get(string key) => s.Flags.TryGetValue(key, out int v) ? v : 0;
    public void Set(string key, int value) => s.Flags[key] = value;
    public bool Has(string key) => s.Flags.ContainsKey(key);
    public List<EpisodeVar> SnapshotAll() =>
        s.Flags.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new EpisodeVar { Key = kv.Key, Value = kv.Value }).ToList();
    public void RestoreAll(IReadOnlyList<EpisodeVar> vars)
    {
        s.Flags.Clear();
        foreach (var v in vars) if (v != null && !string.IsNullOrEmpty(v.Key)) s.Flags[v.Key] = v.Value;
    }
}

/// <summary>[C1E1-g1] 결정 노드의 결과 기록.</summary>
public sealed class EpisodeOutcome
{
    public string Node { get; set; } = "";
    public string Outcome { get; set; } = "";
}

/// <summary>[C1E1-g1] 미션 진입 체크포인트 (재도전 복원용).</summary>
public sealed class EpisodeCheckpoint
{
    public List<EpisodeVar> Vars { get; set; } = new();
    public float Supply { get; set; }
}

/// <summary>[C1E1-g1 · 캠페인 스키마 v12] 진행 중 에피소드 상태 (docs/C1E1_EPISODE_SCHEMA.md §9). 캠페인 저장 전용 — 시뮬레이션 스냅샷 밖.</summary>
public sealed class EpisodeState
{
    public string EpisodeId { get; set; } = "";
    /// <summary>현재 노드 ("" = 에피소드 끝).</summary>
    public string NodeId { get; set; } = "";
    /// <summary>SK 진행 중일 때 그 SK 를 고른 SELECT 노드 (아니면 "").</summary>
    public string SelectNode { get; set; } = "";
    public int DaysUsed { get; set; }
    public List<string> Selected { get; set; } = new();
    public List<string> FailedSk { get; set; } = new();
    public string Variant { get; set; } = "";
    public bool Waited { get; set; }
    /// <summary>기한 일수 (0 = 아직 없음).</summary>
    public int DeadlineDay { get; set; }
    /// <summary>기한/상한 초과로 승리 (승리 후 페널티 대상).</summary>
    public bool Late { get; set; }
    public List<EpisodeOutcome> Outcomes { get; set; } = new();
    /// <summary>미션 진행 중일 때만 (BeginMission ~ 승리/패배).</summary>
    public EpisodeCheckpoint? Checkpoint { get; set; }

    public bool Finished => NodeId == "";

    /// <summary>불러온 뒤 정리 (null 목록 · null 원소 · 범위).</summary>
    public void Sanitize()
    {
        EpisodeId ??= ""; NodeId ??= ""; SelectNode ??= ""; Variant ??= "";
        Selected ??= new(); Selected.RemoveAll(x => x == null);
        FailedSk ??= new(); FailedSk.RemoveAll(x => x == null);
        Outcomes ??= new(); Outcomes.RemoveAll(x => x == null);
        foreach (var o in Outcomes) { o.Node ??= ""; o.Outcome ??= ""; }
        DaysUsed = Math.Clamp(DaysUsed, 0, CampaignState.DayMax);
        DeadlineDay = Math.Clamp(DeadlineDay, 0, CampaignState.DayMax);
        if (Checkpoint != null)
        {
            Checkpoint.Vars ??= new(); Checkpoint.Vars.RemoveAll(v => v == null || string.IsNullOrEmpty(v.Key));
            if (!float.IsFinite(Checkpoint.Supply) || Checkpoint.Supply < 0f) Checkpoint.Supply = 0f;
        }
    }
}

/// <summary>[C1E1-g1] 에피소드 노드 전이기 (헤드리스). 모든 함수는 거부 시 한국어 사유를 돌려주고 상태를 바꾸지 않는다 (null = 성공).</summary>
public static class EpisodeRuntime
{
    public static void Start(CampaignState s, EpisodeDef def) =>
        s.Episode = new EpisodeState { EpisodeId = def.Id, NodeId = def.Start };

    /// <summary>지금 노드 (끝 · 에피소드 없음 · 다른 에피소드 · 정의에 없는 노드 = null).</summary>
    public static EpisodeNode? Current(CampaignState s, EpisodeDef def) =>
        s.Episode is { } e && e.EpisodeId == def.Id && e.NodeId != "" ? def.Find(e.NodeId) : null;

    static string? Need(CampaignState s, EpisodeDef def, out EpisodeNode node, params EpisodeNodeKind[] kinds)
    {
        node = null!;
        if (s.Episode == null || s.Episode.EpisodeId != def.Id) return "에피소드가 시작되지 않음";
        if (s.Episode.NodeId == "") return "에피소드가 끝남";
        if (def.Find(s.Episode.NodeId) is not { } n) return $"정의에 없는 노드 '{s.Episode.NodeId}'";
        node = n;
        if (Array.IndexOf(kinds, n.Kind) < 0) return $"지금 노드 {n.Id} ({n.Kind}) 에서 할 수 없음";
        return null;
    }

    static void Advance(EpisodeState e, string next) { e.NodeId = next; e.SelectNode = ""; }

    /// <summary>EV · CH · REPORT 결정 확정. 노드에 deadline 표가 있으면 outcome 이 그 키여야 하고 기한을 정한다.</summary>
    public static string? Resolve(CampaignState s, EpisodeDef def, string outcome = "")
    {
        if (Need(s, def, out var n, EpisodeNodeKind.EV, EpisodeNodeKind.CH, EpisodeNodeKind.REPORT) is { } err) return err;
        outcome ??= "";
        int deadline = 0;
        if (n.Deadline.Count > 0 && !n.Deadline.TryGetValue(outcome, out deadline)) return $"노드 {n.Id}: 기한 표에 없는 결과 '{outcome}'";
        var e = s.Episode!;
        if (deadline > 0) e.DeadlineDay = deadline;
        if (outcome != "") e.Outcomes.Add(new EpisodeOutcome { Node = n.Id, Outcome = outcome });
        Advance(e, n.Next);
        return null;
    }

    /// <summary>IL 편성 확정. wait = 대기(D16) — 확정 시점에 waitDays 를 쓴다.</summary>
    public static string? ConfirmFormation(CampaignState s, EpisodeDef def, bool wait)
    {
        if (Need(s, def, out var n, EpisodeNodeKind.IL) is { } err) return err;
        if (wait && n.WaitDays <= 0) return $"노드 {n.Id}: 대기할 수 없음";
        var e = s.Episode!;
        if (wait) { e.DaysUsed = (int)Math.Min(CampaignState.DayMax, (long)e.DaysUsed + n.WaitDays); e.Waited = true; }
        Advance(e, n.Next);
        return null;
    }

    /// <summary>SELECT 한 칸: skId = 고를 SK (이미 고른 것 불가), null/"" = 건너뛰기.</summary>
    public static string? Select(CampaignState s, EpisodeDef def, string? skId)
    {
        if (Need(s, def, out var n, EpisodeNodeKind.SELECT) is { } err) return err;
        var e = s.Episode!;
        if (string.IsNullOrEmpty(skId)) { Advance(e, n.Next); return null; }
        if (!n.Options.Contains(skId)) return $"노드 {n.Id}: 선택지에 없는 '{skId}'";
        if (e.Selected.Contains(skId)) return $"{skId} 는 이미 골랐음";
        e.Selected.Add(skId);
        e.NodeId = skId; e.SelectNode = n.Id;
        return null;
    }

    /// <summary>OP 변형 선택 (D11). 미션 시작 전에만.</summary>
    public static string? ChooseVariant(CampaignState s, EpisodeDef def, string key)
    {
        if (Need(s, def, out var n, EpisodeNodeKind.OP) is { } err) return err;
        if (s.Episode!.Checkpoint != null) return "미션 진행 중에는 바꿀 수 없음";
        if (n.FindVariant(key ?? "") == null) return $"노드 {n.Id}: 없는 변형 '{key}'";
        s.Episode.Variant = key!;
        return null;
    }

    /// <summary>지금 미션 노드의 (미션 id, 일수). 변형이 필요한데 안 골랐으면 null.</summary>
    public static (string Mission, int Days)? MissionOf(EpisodeNode n, string variant)
    {
        if (!n.IsMission) return null;
        if (n.Variants.Count > 0) return n.FindVariant(variant) is { } v ? (v.Mission, v.Days) : null;
        return (n.Mission, n.Days);
    }

    /// <summary>미션 진입: 체크포인트 (변수 전체 + 보급) 를 잡는다. missionId = 띄울 미션 파일.</summary>
    public static string? BeginMission(CampaignState s, EpisodeDef def, ICampaignVars vars, out string missionId)
    {
        missionId = "";
        if (Need(s, def, out var n, EpisodeNodeKind.BT, EpisodeNodeKind.SK, EpisodeNodeKind.OP) is { } err) return err;
        var e = s.Episode!;
        if (e.Checkpoint != null) return "이미 미션 진행 중";
        if (MissionOf(n, e.Variant) is not { } m) return $"노드 {n.Id}: 변형을 먼저 골라야 함";
        e.Checkpoint = new EpisodeCheckpoint { Vars = vars.SnapshotAll(), Supply = s.Supply };
        missionId = m.Mission;
        return null;
    }

    /// <summary>미션 승리: 일수 확정 (이 시점에만), 기한 판정, 다음 노드 (SK 는 부른 SELECT 의 next).</summary>
    public static string? MissionWon(CampaignState s, EpisodeDef def)
    {
        if (Need(s, def, out var n, EpisodeNodeKind.BT, EpisodeNodeKind.SK, EpisodeNodeKind.OP) is { } err) return err;
        var e = s.Episode!;
        if (e.Checkpoint == null) return "진행 중인 미션 없음";
        if (MissionOf(n, e.Variant) is not { } m) return $"노드 {n.Id}: 변형 없음";
        string? next = NextAfter(def, n, e);
        if (next == null) return $"노드 {n.Id}: 돌아갈 SELECT 없음";
        e.DaysUsed = (int)Math.Min(CampaignState.DayMax, (long)e.DaysUsed + m.Days);
        if (n.CheckDeadline) e.Late = IsLate(def, e.DaysUsed, e.DeadlineDay);
        e.Checkpoint = null;
        Advance(e, next);
        return null;
    }

    /// <summary>미션 패배: 변수 · 보급을 진입 시점으로 되돌린다 (일수 불변). confirmFail = SK 실패 확정 (failDays 소모 후 SELECT 의 next).
    /// 재도전이면 같은 노드에 남고, OP 는 변형 선택도 지워 IL 확정 직후 상태로 돌아간다.</summary>
    public static string? MissionLost(CampaignState s, EpisodeDef def, ICampaignVars vars, bool confirmFail)
    {
        if (Need(s, def, out var n, EpisodeNodeKind.BT, EpisodeNodeKind.SK, EpisodeNodeKind.OP) is { } err) return err;
        var e = s.Episode!;
        if (e.Checkpoint == null) return "진행 중인 미션 없음";
        string? next = null;
        if (confirmFail)
        {
            if (!n.CanConfirmFail) return $"노드 {n.Id}: 실패 확정 불가 (재도전만)";
            next = NextAfter(def, n, e);
            if (next == null) return $"노드 {n.Id}: 돌아갈 SELECT 없음";
        }
        vars.RestoreAll(e.Checkpoint.Vars);
        s.Supply = e.Checkpoint.Supply;
        e.Checkpoint = null;
        if (n.Kind == EpisodeNodeKind.OP) e.Variant = "";
        if (confirmFail)
        {
            e.FailedSk.Add(n.Id);
            e.DaysUsed = (int)Math.Min(CampaignState.DayMax, (long)e.DaysUsed + n.FailDays);
            Advance(e, next!);
        }
        return null;
    }

    static string? NextAfter(EpisodeDef def, EpisodeNode n, EpisodeState e) =>
        n.Kind == EpisodeNodeKind.SK ? def.Find(e.SelectNode)?.Next : n.Next;

    /// <summary>기한/상한 초과 판정 (R1): 기한이 있으면 총일수 > 기한, 또는 총일수 > dayCap.</summary>
    public static bool IsLate(EpisodeDef def, int total, int deadline) =>
        (deadline > 0 && total > deadline) || total > def.DayCap;

    /// <summary>IL 화면 예고 (R1 "대기 시 N일째"): 지금 일수 + 남은 next 사슬의 대기 (wait 일 때, 지금 IL 만) + 미션 일수 (OP 는 variantKey) 를
    /// checkDeadline 노드까지 더한다. SELECT 는 0 으로 친다. 계산 불가 (변형 미지정 등) = -1.</summary>
    public static int ProjectDays(CampaignState s, EpisodeDef def, string variantKey, bool wait)
    {
        if (s.Episode is not { } e || e.EpisodeId != def.Id) return -1;
        long total = e.DaysUsed;
        string cur = e.NodeId;
        bool first = true;
        for (int guard = 0; cur != "" && guard <= def.Nodes.Count; guard++)
        {
            if (def.Find(cur) is not { } n) return -1;
            if (n.Kind == EpisodeNodeKind.IL && first && wait) total += n.WaitDays;
            if (n.IsMission)
            {
                if (MissionOf(n, n.Variants.Count > 0 ? (variantKey ?? "") : "") is not { } m) return -1;
                total += m.Days;
                if (n.CheckDeadline) return (int)Math.Min(CampaignState.DayMax, total);
            }
            first = false;
            cur = n.Kind == EpisodeNodeKind.SK ? (def.Find(e.SelectNode)?.Next ?? "") : n.Next;
        }
        return -1;
    }

    /// <summary>예고 일수가 기한/상한을 넘는가 (IL 화면 ⚠).</summary>
    public static bool WouldBeLate(CampaignState s, EpisodeDef def, int projected) =>
        projected >= 0 && IsLate(def, projected, s.Episode?.DeadlineDay ?? 0);
}
