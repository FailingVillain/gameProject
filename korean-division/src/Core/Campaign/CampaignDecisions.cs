using System.Text.Json;

namespace KoreanDivision.Core;

// ---------- 정의 (assets/data/campaign_decisions.json) ----------
public sealed class DecisionCond
{
    /// <summary>변수 키 (레지스트리) 또는 "negotiation" (계산값).</summary>
    public string Var { get; set; } = "";
    public string Op { get; set; } = ">=";
    public int Value { get; set; }
}

public sealed class DecisionEffect
{
    /// <summary>set · add (var + value) / spendFunds (econ: volunteer · bandit · fairy · sword, men = 인원, 0 = 응답의 quantity) / permanentSupplyLoss (econ: civilianRelief).</summary>
    public string Op { get; set; } = "";
    public string Var { get; set; } = "";
    public int Value { get; set; }
    public string Econ { get; set; } = "";
    public int Men { get; set; }
}

public sealed class DecisionChoiceDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>모두 참이어야 고를 수 있다 (거짓이면 비활성 — D09 B · D13). 설득(D10)처럼 "성립 여부만 갈리는" 선택지는 이게 아니라 SuccessIf 를 쓴다 (R7: 비활성 금지).</summary>
    public List<DecisionCond> EnableIf { get; set; } = new();
    /// <summary>비어 있지 않으면 선택은 항상 가능하되 결과가 "ok" / "fail" 로 갈린다 (D10 설득 = intel.andong).</summary>
    public List<DecisionCond> SuccessIf { get; set; } = new();
    public List<DecisionEffect> Effects { get; set; } = new();
}

public sealed class DecisionDef
{
    public string Id { get; set; } = "";
    /// <summary>major · std · minor.</summary>
    public string Tier { get; set; } = "std";
    /// <summary>CHOICE · APPROACH · SELECT · INMISSION · ORDER · DEPLOY · REPORT.</summary>
    public string Kind { get; set; } = "CHOICE";
    public string Where { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Multi { get; set; }
    /// <summary>미션 중 일시정지 후 응답 필수 (minor INMISSION 포함).</summary>
    public bool PausesMission { get; set; }
    /// <summary>소비처 (필수, 비어 있으면 거부).</summary>
    public List<string> Consumers { get; set; } = new();
    public List<DecisionChoiceDef> Choices { get; set; } = new();
}

public sealed class NegotiationSource
{
    public string Decision { get; set; } = "";
    public List<string> All { get; set; } = new();
    public string Var { get; set; } = "";
    public int AtLeast { get; set; }
    public int Delta { get; set; }
}

// ---------- 저장 상태 (CampaignState.Decisions, 스키마 v13) ----------
public sealed class DecisionAnswer
{
    public string Id { get; set; } = "";
    public List<string> Choices { get; set; } = new();
    /// <summary>"" · "ok" · "fail" (SuccessIf 가 있는 선택지만).</summary>
    public string Outcome { get; set; } = "";
    public int Quantity { get; set; }
    /// <summary>응답이 확정된 캠페인 일 (미션 중 응답은 승리 시점에 그날로 다시 찍는다).</summary>
    public int Day { get; set; }
}

/// <summary>미션 시작 때 찍는 복원점 (재도전 = 그 상태로). 응답은 개수만 (뒤에 쌓인 것을 버림).</summary>
public sealed class DecisionCheckpoint
{
    public List<CampaignVarEntry> Vars { get; set; } = new();
    public int Answers { get; set; }
    public int Funds { get; set; }
    public float PermanentLoss { get; set; }
    public float Supply { get; set; }
    public List<string> Pending { get; set; } = new();
}

public sealed class DecisionLog
{
    public List<DecisionAnswer> Answers { get; set; } = new();
    /// <summary>제기됐지만 아직 응답하지 않은 결정 (응답 필수 — 미션 중이면 일시정지 사유).</summary>
    public List<string> Pending { get; set; } = new();
    public DecisionCheckpoint? Checkpoint { get; set; }

    public void Sanitize()
    {
        Answers ??= new(); Pending ??= new();
        Answers.RemoveAll(a => a == null || string.IsNullOrEmpty(a.Id));
        foreach (var a in Answers) { a.Choices ??= new(); a.Choices.RemoveAll(c => string.IsNullOrEmpty(c)); a.Outcome ??= ""; a.Quantity = Math.Clamp(a.Quantity, 0, 1_000_000); a.Day = Math.Clamp(a.Day, 0, CampaignState.DayMax); }
        if (Answers.Count > CampaignDecisions.AnswerMax) Answers.RemoveRange(0, Answers.Count - CampaignDecisions.AnswerMax);
        Pending.RemoveAll(p => string.IsNullOrEmpty(p));
        if (Checkpoint != null)
        {
            Checkpoint.Vars ??= new(); Checkpoint.Vars.RemoveAll(v => v == null || string.IsNullOrEmpty(v.Key)); Checkpoint.Pending ??= new();
            Checkpoint.Answers = Math.Clamp(Checkpoint.Answers, 0, Answers.Count);
            Checkpoint.Funds = Math.Clamp(Checkpoint.Funds, 0, 1_000_000);
            if (!float.IsFinite(Checkpoint.PermanentLoss) || Checkpoint.PermanentLoss < 0f) Checkpoint.PermanentLoss = 0f;
            if (!float.IsFinite(Checkpoint.Supply) || Checkpoint.Supply < 0f) Checkpoint.Supply = 0f;
        }
    }
}

/// <summary>[TC-E] 결정 데이터 + 런타임. 호감 등 증감 크기는 [보류] — 이 클래스는 데이터의 effects 를 그대로 적용할 뿐 값을 정하지 않는다.</summary>
public sealed class CampaignDecisions
{
    public const int AnswerMax = 500;
    public static readonly string[] Kinds = { "CHOICE", "APPROACH", "SELECT", "INMISSION", "ORDER", "DEPLOY", "REPORT" };
    public static readonly string[] Tiers = { "major", "std", "minor" };
    static readonly string[] Ops = { ">=", "<=", "==", "!=", ">", "<" };

    public List<DecisionDef> Decisions { get; set; } = new();
    public int NegotiationMax { get; set; } = 3;
    public List<NegotiationSource> NegotiationSources { get; set; } = new();
    /// <summary>불러올 때 버린 결정의 사유 (한국어, 정상이면 비어 있음).</summary>
    public List<string> LoadErrors { get; } = new();

    public DecisionDef? Find(string id) => Decisions.FirstOrDefault(d => d.Id == id);

    sealed class FileModel
    {
        public List<DecisionDef>? Decisions { get; set; }
        public NegModel? Negotiation { get; set; }
    }
    sealed class NegModel { public int Max { get; set; } = 3; public List<NegotiationSource>? Sources { get; set; } }

    static CampaignDecisions? current;
    public static CampaignDecisions Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "campaign_decisions.json"));
    /// <summary>[테스트] 정의를 갈아 끼운다 (null = 파일 다시 읽기).</summary>
    public static void Override(CampaignDecisions? d) => current = d;

    public static CampaignDecisions Load(string path)
    {
        var r = new CampaignDecisions();
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), DataJson.Opts) is { } m)
            {
                r.NegotiationMax = Math.Clamp(m.Negotiation?.Max ?? 3, 0, 100);
                if (m.Negotiation?.Sources != null) r.NegotiationSources = m.Negotiation.Sources.Where(x => x != null).ToList();
                var seen = new HashSet<string>();
                foreach (var d in m.Decisions ?? new())
                {
                    string? err = Validate(d, seen);
                    if (err != null) { r.LoadErrors.Add(err); continue; }
                    seen.Add(d!.Id); r.Decisions.Add(d);
                }
            }
            else r.LoadErrors.Add("결정 파일 없음 또는 비어 있음: " + path);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { r.LoadErrors.Add("결정 파일 읽기 실패: " + ex.Message); }
        return r;
    }

    /// <summary>한 결정의 형식 검사 (null = 정상). 유일한 id · 7종 중 하나의 kind · 3등급 · 소비처 필수 · 선택지(SELECT 제외 1개 이상, id 유일) · 조건/효과의 변수 키가 레지스트리에 있음.</summary>
    public static string? Validate(DecisionDef? d, ISet<string> seenIds)
    {
        if (d == null || string.IsNullOrWhiteSpace(d.Id)) return "결정 id 없음";
        if (seenIds.Contains(d.Id)) return d.Id + ": id 중복";
        if (Array.IndexOf(Kinds, d.Kind) < 0) return d.Id + ": 모르는 kind " + d.Kind;
        if (Array.IndexOf(Tiers, d.Tier) < 0) return d.Id + ": 모르는 tier " + d.Tier;
        if (d.Consumers == null || d.Consumers.Count == 0 || d.Consumers.Any(string.IsNullOrWhiteSpace)) return d.Id + ": 소비처 없음 (필수)";
        d.Choices ??= new();
        if (d.Choices.Count == 0 && d.Kind != "SELECT") return d.Id + ": 선택지 없음";
        var ids = new HashSet<string>();
        foreach (var c in d.Choices)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || !ids.Add(c.Id)) return d.Id + ": 선택지 id 없음/중복";
            c.EnableIf ??= new(); c.SuccessIf ??= new(); c.Effects ??= new();
            foreach (var k in c.EnableIf.Concat(c.SuccessIf)) { if (k == null || !CondOk(k)) return d.Id + "/" + c.Id + ": 잘못된 조건"; }
            foreach (var e in c.Effects)
            {
                if (e == null) return d.Id + "/" + c.Id + ": 효과 null";
                bool ok = e.Op switch
                {
                    "set" or "add" => CampaignVars.Has(e.Var),
                    "spendFunds" => e.Econ is "volunteer" or "bandit" or "fairy" or "sword" && e.Men >= 0,
                    "permanentSupplyLoss" => e.Econ == "civilianRelief",
                    _ => false,
                };
                if (!ok) return d.Id + "/" + c.Id + ": 잘못된 효과 " + e.Op;
            }
        }
        return null;
    }

    static bool CondOk(DecisionCond k) => Array.IndexOf(Ops, k.Op) >= 0 && (CampaignVars.Has(k.Var) || k.Var == "negotiation");

    // ---------- 평가 ----------
    /// <summary>협상 점수 0..Max (계산값): 데이터의 출처 표 — 결정 응답에 선택지가 모두 들어 있으면 +delta, 변수가 atLeast 이상이면 +delta.</summary>
    public int Negotiation(CampaignState s)
    {
        int sum = 0;
        foreach (var src in NegotiationSources)
        {
            if (src.Decision.Length > 0)
            {
                var a = s.Decisions.Answers.LastOrDefault(x => x.Id == src.Decision);
                if (a != null && src.All.Count > 0 && src.All.All(c => a.Choices.Contains(c))) sum += src.Delta;
            }
            else if (src.Var.Length > 0 && CampaignVars.Get(s, src.Var) >= src.AtLeast) sum += src.Delta;
        }
        return Math.Clamp(sum, 0, NegotiationMax);
    }

    int ValueOf(CampaignState s, string key) => key == "negotiation" ? Negotiation(s) : CampaignVars.Get(s, key);

    bool Eval(CampaignState s, IEnumerable<DecisionCond> conds)
    {
        foreach (var c in conds)
        {
            int v = ValueOf(s, c.Var);
            bool ok = c.Op switch { ">=" => v >= c.Value, "<=" => v <= c.Value, "==" => v == c.Value, "!=" => v != c.Value, ">" => v > c.Value, "<" => v < c.Value, _ => false };
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>선택지를 지금 고를 수 있는가 (EnableIf).</summary>
    public bool IsEnabled(CampaignState s, string decisionId, string choiceId) =>
        Find(decisionId)?.Choices.FirstOrDefault(c => c.Id == choiceId) is { } ch && Eval(s, ch.EnableIf);

    /// <summary>성립 조건이 있는 선택지의 지금 예상 결과 (R7 표시용): "ok" · "fail" · "" (조건 없음).</summary>
    public string PreviewOutcome(CampaignState s, string decisionId, string choiceId)
    {
        var ch = Find(decisionId)?.Choices.FirstOrDefault(c => c.Id == choiceId);
        return ch == null || ch.SuccessIf.Count == 0 ? "" : Eval(s, ch.SuccessIf) ? "ok" : "fail";
    }

    public bool IsAnswered(CampaignState s, string decisionId) => s.Decisions.Answers.Any(a => a.Id == decisionId);
    public bool HasPending(CampaignState s) => s.Decisions.Pending.Count > 0;

    // ---------- 런타임 ----------
    /// <summary>결정을 제기한다 (응답 전까지 Pending — 미션 중이면 일시정지 사유). 이미 응답했거나 모르는 id 면 오류 문장, 정상이면 "".</summary>
    public string Raise(CampaignState s, string decisionId)
    {
        if (Find(decisionId) == null) return "모르는 결정: " + decisionId;
        if (IsAnswered(s, decisionId)) return "이미 응답한 결정: " + decisionId;
        if (!s.Decisions.Pending.Contains(decisionId)) s.Decisions.Pending.Add(decisionId);
        return "";
    }

    /// <summary>응답. 오류면 문장을 돌려주고 아무것도 바뀌지 않는다 (원자적): 모르는 결정/선택지 · 이미 응답 · 단일 결정에 복수 선택 · 비활성 선택지 · 자금 부족.
    /// <paramref name="quantity"/> = 마적 인원 등 선택지 효과가 쓰는 수량. 정상이면 "" 와 기록(<paramref name="answer"/>).</summary>
    public string Answer(CampaignState s, string decisionId, IReadOnlyList<string> choiceIds, int quantity, out DecisionAnswer? answer)
    {
        answer = null;
        var d = Find(decisionId);
        if (d == null) return "모르는 결정: " + decisionId;
        if (IsAnswered(s, decisionId)) return "이미 응답한 결정: " + decisionId;
        var chosen = choiceIds.Distinct().ToList();
        if (chosen.Count == 0) return "선택이 없습니다";
        if (chosen.Count > 1 && !d.Multi) return "하나만 고를 수 있습니다";
        var defs = new List<DecisionChoiceDef>();
        foreach (var id in chosen)
        {
            var c = d.Choices.FirstOrDefault(x => x.Id == id);
            if (c == null) return "모르는 선택지: " + id;
            if (!Eval(s, c.EnableIf)) return "지금은 고를 수 없는 선택지: " + id;
            defs.Add(c);
        }
        quantity = Math.Clamp(quantity, 0, 1_000_000);
        int cost = 0;
        foreach (var c in defs) foreach (var e in c.Effects) if (e.Op == "spendFunds") cost += Price(e, quantity);
        if (cost > s.Funds) return string.Format("자금이 모자랍니다 (필요 {0}, 보유 {1})", cost, s.Funds);

        string outcome = "";
        foreach (var c in defs) if (c.SuccessIf.Count > 0) outcome = Eval(s, c.SuccessIf) ? "ok" : "fail";   // 평가는 효과 적용 전 상태로
        foreach (var c in defs)
            foreach (var e in c.Effects)
                switch (e.Op)
                {
                    case "set": CampaignVars.Set(s, e.Var, e.Value); break;
                    case "add": CampaignVars.Add(s, e.Var, e.Value); break;
                    case "spendFunds": CampaignFunds.TrySpend(s, Price(e, quantity), out _); break;
                    case "permanentSupplyLoss": CampaignFunds.AddPermanentLoss(s, CampaignEconomyDefs.Current.CivilianReliefSupplyLoss); break;
                }
        answer = new DecisionAnswer { Id = decisionId, Choices = chosen, Outcome = outcome, Quantity = quantity, Day = Math.Max(s.Day, 1) };
        s.Decisions.Answers.Add(answer);
        if (s.Decisions.Answers.Count > AnswerMax) s.Decisions.Answers.RemoveAt(0);
        s.Decisions.Pending.Remove(decisionId);
        return "";
    }

    static int Price(DecisionEffect e, int quantity)
    {
        int men = e.Men > 0 ? e.Men : quantity;
        var d = CampaignEconomyDefs.Current;
        return e.Econ switch { "volunteer" => CampaignFunds.VolunteerPrice(men), "bandit" => CampaignFunds.BanditPrice(men), "fairy" => d.FairyFunds, "sword" => d.SwordFunds, _ => 0 };
    }

    // ---------- 미션 복원점 (재도전) ----------
    /// <summary>미션 시작 때: 변수 · 응답 개수 · 자금 · 영구 소모 · 보급 · 대기 결정을 찍어 둔다.</summary>
    public static void BeginMission(CampaignState s)
    {
        s.Decisions.Checkpoint = new DecisionCheckpoint
        {
            Vars = CampaignVars.SnapshotAll(s), Answers = s.Decisions.Answers.Count, Funds = s.Funds, PermanentLoss = s.EpisodePermanentSupplyLoss, Supply = s.Supply,
            Pending = s.Decisions.Pending.ToList(),
        };
    }

    /// <summary>패배 후 재도전: 복원점으로 되돌린다 (미션 중 한 응답은 사라지고, 일수는 건드리지 않는다 — 재도전은 일수를 쓰지 않음). 복원점이 없으면 false.</summary>
    public static bool RetryMission(CampaignState s)
    {
        var cp = s.Decisions.Checkpoint; if (cp == null) return false;
        CampaignVars.RestoreAll(s, cp.Vars);
        if (cp.Answers < s.Decisions.Answers.Count) s.Decisions.Answers.RemoveRange(cp.Answers, s.Decisions.Answers.Count - cp.Answers);
        s.Funds = cp.Funds; s.EpisodePermanentSupplyLoss = cp.PermanentLoss; s.Supply = cp.Supply;
        s.Decisions.Pending = cp.Pending.ToList();
        s.Decisions.Checkpoint = null;
        return true;
    }

    /// <summary>승리 확정: 미션 중 응답의 날짜를 확정된 오늘(<see cref="CampaignState.Day"/>, 일수 가산 뒤에 부를 것)로 찍고 복원점을 버린다.</summary>
    public static void CommitMission(CampaignState s)
    {
        var cp = s.Decisions.Checkpoint; if (cp == null) return;
        for (int i = cp.Answers; i < s.Decisions.Answers.Count; i++) s.Decisions.Answers[i].Day = Math.Max(s.Day, 1);
        s.Decisions.Checkpoint = null;
    }
}
