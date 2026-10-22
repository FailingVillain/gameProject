using System.Text.Json;
namespace KoreanDivision.Core;

// C1E1-d1 노드 데이터 (assets/data/episodes/c1e1_nodes.json): 노드별 화면 키 · 결정 배치 · 진입 시 변수. 구조(전이/일수)는 g1 그래프 몫.
public sealed class EpisodeNodeVarSet { public string Var { get; set; } = ""; public int Value { get; set; } }

public sealed class EpisodeNodeInfo
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string TitleKey { get; set; } = "";
    public string BodyKey { get; set; } = "";
    public bool SupplyGauge { get; set; }
    public bool DeadlineWarning { get; set; }
    public List<EpisodeNodeVarSet> SetOnEnter { get; set; } = new();
}

public sealed class EpisodeNodeData
{
    public string Episode { get; private set; } = "";
    public Dictionary<string, List<string>> Placements { get; private set; } = new();
    public List<EpisodeNodeInfo> Nodes { get; private set; } = new();
    public Dictionary<string, string> Texts { get; private set; } = new();
    public List<string> Errors { get; } = new();

    public EpisodeNodeInfo? Find(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    sealed class Model
    {
        public string? Episode { get; set; }
        public Dictionary<string, List<string?>?>? Placements { get; set; }
        public List<EpisodeNodeInfo?>? Nodes { get; set; }
        public Dictionary<string, string?>? Texts { get; set; }
    }

    static readonly string[] NodeKinds = { "EV", "CH", "IL" };

    public static EpisodeNodeData Load(string path)
    {
        var r = new EpisodeNodeData();
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<Model>(File.ReadAllText(path), DataJson.Opts) is { } m) r.Fill(m);
            else r.Errors.Add("노드 파일 없음 또는 비어 있음: " + path);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { r.Errors.Add("노드 파일 읽기 실패: " + ex.Message); }
        return r;
    }

    void Fill(Model m)
    {
        Episode = m.Episode ?? "";
        if (Episode.Length == 0) Errors.Add("episode 없음");
        foreach (var kv in m.Placements ?? new())
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null || kv.Value.Any(string.IsNullOrEmpty)) { Errors.Add($"placements '{kv.Key}': 빈 키/값"); continue; }
            Placements[kv.Key] = kv.Value!.Select(x => x!).ToList();
        }
        foreach (var n in m.Nodes ?? new())
        {
            if (n == null || string.IsNullOrEmpty(n.Id)) { Errors.Add("노드 id 없음/null"); continue; }
            if (Nodes.Any(x => x.Id == n.Id)) { Errors.Add($"노드 id 중복: {n.Id}"); continue; }
            if (Array.IndexOf(NodeKinds, n.Kind) < 0) { Errors.Add($"노드 {n.Id}: kind '{n.Kind}' 는 EV/CH/IL 만"); continue; }
            n.SetOnEnter ??= new();
            Nodes.Add(n);
        }
        foreach (var kv in m.Texts ?? new())
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) { Errors.Add($"texts '{kv.Key}': 빈 키/null 값"); continue; }
            Texts[kv.Key] = kv.Value;
        }
    }

    /// <summary>참조 무결성 검사 (오류 목록, 비면 정상). 결정 16개 각각이 정확히 한 노드에 배치 · 배치 결정이 실존 · 변수/문구 키 실존.</summary>
    public List<string> Validate(CampaignDecisions decisions, Func<string, bool> hasVar)
    {
        var e = new List<string>(Errors);
        var placed = new Dictionary<string, string>();
        foreach (var kv in Placements)
            foreach (var d in kv.Value)
            {
                if (decisions.Find(d) == null) e.Add($"배치 {kv.Key}: 모르는 결정 {d}");
                else if (!placed.TryAdd(d, kv.Key)) e.Add($"결정 {d} 가 {placed[d]} 와 {kv.Key} 두 곳에 배치됨");
            }
        foreach (var d in decisions.Decisions) if (!placed.ContainsKey(d.Id)) e.Add($"결정 {d.Id} 가 어느 노드에도 배치되지 않음");
        foreach (var n in Nodes)
        {
            foreach (var k in new[] { n.TitleKey, n.BodyKey })
                if (string.IsNullOrEmpty(k) || !Texts.ContainsKey(k)) e.Add($"노드 {n.Id}: 문구 키 '{k}' 없음");
            foreach (var s in n.SetOnEnter)
                if (string.IsNullOrEmpty(s.Var) || !hasVar(s.Var)) e.Add($"노드 {n.Id}: 모르는 변수 '{s.Var}'");
        }
        return e;
    }
}
