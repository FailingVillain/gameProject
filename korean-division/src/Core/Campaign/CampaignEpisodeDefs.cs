using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>[C1E1-g1] 에피소드 노드 종류 (docs/C1E1_EPISODE_SCHEMA.md §3).</summary>
public enum EpisodeNodeKind { EV, CH, IL, BT, SK, OP, SELECT, REPORT }

/// <summary>[C1E1-g1] OP 노드의 변형 (강공 / 포위 등): 미션 파일 · 일수.</summary>
public sealed class EpisodeVariant
{
    public string Key = "";
    public string Mission = "";
    public int Days;
}

/// <summary>[C1E1-g1] 에피소드 그래프 노드 (데이터 전용, 불변으로 취급).</summary>
public sealed class EpisodeNode
{
    public string Id = "";
    public EpisodeNodeKind Kind;
    /// <summary>다음 노드 id ("" = 에피소드 끝). SK 는 쓰지 않는다 (부른 SELECT 의 Next 로 돌아간다).</summary>
    public string Next = "";
    public bool HasNext;
    /// <summary>이 노드에서 묻는 결정 id (정보용 — g2/TC-E 가 읽는다).</summary>
    public List<string> Decisions = new();
    public string Mission = "";
    public int Days;
    public List<EpisodeVariant> Variants = new();
    public List<string> Options = new();
    public int WaitDays;
    public int FailDays;
    /// <summary>true = 실패 확정 허용 (SK 기본), false = 재도전만 (BT/OP 기본).</summary>
    public bool CanConfirmFail;
    public bool CheckDeadline;
    /// <summary>결과 키 → 기한 일수 (키 정렬 순서로 보관 — 열거 순서 결정적).</summary>
    public SortedDictionary<string, int> Deadline = new(StringComparer.Ordinal);

    public bool IsMission => Kind is EpisodeNodeKind.BT or EpisodeNodeKind.SK or EpisodeNodeKind.OP;
    public EpisodeVariant? FindVariant(string key) => Variants.FirstOrDefault(v => v.Key == key);
}

/// <summary>[C1E1-g1] 에피소드 정의 (assets/data/episodes/&lt;id&gt;.json).</summary>
public sealed class EpisodeDef
{
    public string Id = "";
    public int DayCap;
    public string Start = "";
    public List<EpisodeNode> Nodes = new();
    readonly Dictionary<string, EpisodeNode> byId = new(StringComparer.Ordinal);

    public EpisodeNode? Find(string id) => id != null && byId.TryGetValue(id, out var n) ? n : null;
    internal void Index() { byId.Clear(); foreach (var n in Nodes) byId[n.Id] = n; }
}

/// <summary>[C1E1-g1] 에피소드 정의 로더 + 검증. 실패 = null + 한국어 오류 목록 (크래시 없음).</summary>
public static class EpisodeDefs
{
    public static string PathFor(string episodeId) => PathGuard.Resolve(Path.Combine(AppContext.BaseDirectory, "assets", "data", "episodes"), episodeId);   // T51a: id 검사 + 폴더 밖 거부

    public static EpisodeDef? Load(string path, List<string> errors)
    {
        if (!File.Exists(path)) { errors.Add($"에피소드 파일 없음: {Path.GetFileName(path)}"); return null; }
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { errors.Add($"에피소드 파일을 읽을 수 없음: {e.Message}"); return null; }
        return Parse(text, errors);
    }

    public static EpisodeDef? Parse(string text, List<string> errors)
    {
        EpisodeDef def;
        try
        {
            using var doc = JsonDocument.Parse(text ?? "", new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            def = Read(doc.RootElement, errors);
        }
        catch (JsonException e) { errors.Add($"에피소드 JSON 형식 오류: {e.Message}"); return null; }
        if (errors.Count > 0) return null;
        def.Index();
        Validate(def, errors);
        return errors.Count > 0 ? null : def;
    }

    static string Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static int Int(JsonElement o, string name, List<string> errors, string where, int dflt = 0)
    {
        if (!o.TryGetProperty(name, out var v)) return dflt;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i)) return i;
        errors.Add($"{where}: {name} 는 정수여야 함"); return dflt;
    }

    static List<string> StrList(JsonElement o, string name, List<string> errors, string where)
    {
        var l = new List<string>();
        if (!o.TryGetProperty(name, out var v)) return l;
        if (v.ValueKind != JsonValueKind.Array) { errors.Add($"{where}: {name} 는 배열이어야 함"); return l; }
        foreach (var e in v.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(e.GetString())) { errors.Add($"{where}: {name} 에 빈 값/문자열 아닌 값"); continue; }
            l.Add(e.GetString()!);
        }
        return l;
    }

    static EpisodeDef Read(JsonElement root, List<string> errors)
    {
        var def = new EpisodeDef();
        if (root.ValueKind != JsonValueKind.Object) { errors.Add("에피소드: 최상위가 객체가 아님"); return def; }
        def.Id = Str(root, "id");
        def.Start = Str(root, "start");
        def.DayCap = Int(root, "dayCap", errors, "에피소드");
        if (!root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) { errors.Add("에피소드: nodes 배열 없음"); return def; }
        int idx = 0;
        foreach (var e in nodes.EnumerateArray())
        {
            string where = $"노드 #{idx++}";
            if (e.ValueKind != JsonValueKind.Object) { errors.Add($"{where}: 객체가 아님"); continue; }
            var n = new EpisodeNode { Id = Str(e, "id") };
            if (n.Id != "") where = $"노드 {n.Id}";
            string kind = Str(e, "kind");
            if (!Enum.TryParse(kind, false, out EpisodeNodeKind k) || !Enum.IsDefined(k) || kind != k.ToString()) { errors.Add($"{where}: 모르는 kind '{kind}'"); continue; }
            n.Kind = k;
            n.HasNext = e.TryGetProperty("next", out var nx);
            if (n.HasNext && nx.ValueKind != JsonValueKind.String) errors.Add($"{where}: next 는 문자열이어야 함");
            n.Next = n.HasNext && nx.ValueKind == JsonValueKind.String ? nx.GetString() ?? "" : "";
            n.Decisions = StrList(e, "decisions", errors, where);
            n.Mission = Str(e, "mission");
            n.Days = Int(e, "days", errors, where);
            n.WaitDays = Int(e, "waitDays", errors, where);
            n.FailDays = Int(e, "failDays", errors, where);
            n.Options = StrList(e, "options", errors, where);
            n.CheckDeadline = e.TryGetProperty("checkDeadline", out var cd) && cd.ValueKind == JsonValueKind.True;
            string onLose = Str(e, "onLose");
            if (onLose == "") n.CanConfirmFail = k == EpisodeNodeKind.SK;
            else if (onLose == "retry") n.CanConfirmFail = false;
            else if (onLose == "retryOrFail") n.CanConfirmFail = true;
            else errors.Add($"{where}: 모르는 onLose '{onLose}'");
            if (e.TryGetProperty("variants", out var vs))
            {
                if (vs.ValueKind != JsonValueKind.Array) errors.Add($"{where}: variants 는 배열이어야 함");
                else foreach (var ve in vs.EnumerateArray())
                {
                    if (ve.ValueKind != JsonValueKind.Object) { errors.Add($"{where}: variants 원소가 객체가 아님"); continue; }
                    n.Variants.Add(new EpisodeVariant { Key = Str(ve, "key"), Mission = Str(ve, "mission"), Days = Int(ve, "days", errors, where) });
                }
            }
            if (e.TryGetProperty("deadline", out var dl))
            {
                if (dl.ValueKind != JsonValueKind.Object) errors.Add($"{where}: deadline 은 객체여야 함");
                else foreach (var p in dl.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int d) && p.Name != "") n.Deadline[p.Name] = d;
                    else errors.Add($"{where}: deadline.{p.Name} 는 정수여야 함");
                }
            }
            def.Nodes.Add(n);
        }
        return def;
    }

    static void Validate(EpisodeDef def, List<string> errors)
    {
        if (def.Id == "") errors.Add("에피소드: id 없음");
        if (def.DayCap < 1) errors.Add("에피소드: dayCap 은 1 이상");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in def.Nodes)
        {
            string w = $"노드 {(n.Id == "" ? "(빈 id)" : n.Id)}";
            if (n.Id == "") errors.Add("노드: 빈 id");
            else if (!seen.Add(n.Id)) errors.Add($"{w}: id 중복");
            if (n.Days < 0 || n.WaitDays < 0 || n.FailDays < 0) errors.Add($"{w}: 일수는 음수일 수 없음");
            if (n.Kind == EpisodeNodeKind.SK)
            {
                if (n.HasNext) errors.Add($"{w}: SK 는 next 를 쓰지 않음 (SELECT 로 돌아감)");
            }
            else if (!n.HasNext) errors.Add($"{w}: next 없음 (끝이면 \"\")");
            else if (n.Next != "" && def.Find(n.Next) == null) errors.Add($"{w}: 끊긴 전이 → 없는 노드 '{n.Next}'");
            else if (n.Next != "" && def.Find(n.Next)!.Kind == EpisodeNodeKind.SK) errors.Add($"{w}: next 가 SK 를 직접 가리킴 (SELECT 로만)");
            if ((n.Kind is EpisodeNodeKind.BT or EpisodeNodeKind.SK) && n.Mission == "") errors.Add($"{w}: mission 없음");
            if (n.Kind == EpisodeNodeKind.OP)
            {
                if (n.Variants.Count == 0 && n.Mission == "") errors.Add($"{w}: mission 또는 variants 필요");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var v in n.Variants)
                {
                    if (v.Key == "" || !keys.Add(v.Key)) errors.Add($"{w}: 변형 key 비었거나 중복 '{v.Key}'");
                    if (v.Mission == "") errors.Add($"{w}: 변형 {v.Key} mission 없음");
                    if (v.Days < 0) errors.Add($"{w}: 변형 {v.Key} 일수 음수");
                }
            }
            else if (n.Variants.Count > 0) errors.Add($"{w}: variants 는 OP 만");
            if (n.CanConfirmFail && n.Kind != EpisodeNodeKind.SK) errors.Add($"{w}: 실패 확정(retryOrFail)은 SK 만");
            if (n.Kind == EpisodeNodeKind.SELECT)
            {
                if (n.Options.Count == 0) errors.Add($"{w}: options 비었음");
                if (n.Options.Distinct(StringComparer.Ordinal).Count() != n.Options.Count) errors.Add($"{w}: options 중복");
                foreach (var o in n.Options)
                    if (def.Find(o) is not { Kind: EpisodeNodeKind.SK }) errors.Add($"{w}: 선택지 '{o}' 가 없거나 SK 가 아님");
            }
            else if (n.Options.Count > 0) errors.Add($"{w}: options 는 SELECT 만");
            foreach (var kv in n.Deadline)
                if (kv.Value < 1 || kv.Value > Math.Max(1, def.DayCap)) errors.Add($"{w}: deadline.{kv.Key}={kv.Value} 는 1..dayCap 밖");
        }
        if (errors.Count > 0) return;
        if (def.Find(def.Start) == null) { errors.Add($"에피소드: start '{def.Start}' 노드 없음"); return; }
        if (def.Find(def.Start)!.Kind == EpisodeNodeKind.SK) { errors.Add("에피소드: start 가 SK"); return; }
        // next 사슬: 순환 없이 끝("")에 닿아야 한다. 닿는 노드 = 사슬 + SELECT 의 선택지.
        var reach = new HashSet<string>(StringComparer.Ordinal);
        string cur = def.Start;
        while (cur != "")
        {
            if (!reach.Add(cur)) { errors.Add($"에피소드: next 사슬 순환 ('{cur}')"); return; }
            var n = def.Find(cur)!;
            foreach (var o in n.Options) reach.Add(o);
            cur = n.Next;
        }
        foreach (var n in def.Nodes) if (!reach.Contains(n.Id)) errors.Add($"노드 {n.Id}: 시작에서 닿지 않음");
    }
}
