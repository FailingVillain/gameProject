using System.Text.Json;
using System.Text.RegularExpressions;

namespace KoreanDivision.Core;

/// <summary>[T48a] 연대장 풀의 불변 데이터 한 명 (assets/data/commanders.json, 사용자가 직접 작성). 가변 상태는 CampaignState.CommanderPool. 수치 범위는 전부 임시 (0.3 확정).</summary>
public sealed class CommanderDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rank { get; set; } = "";
    public string Origin { get; set; } = "";
    public string School { get; set; } = "";
    public List<string> Traits { get; set; } = new();
    public CommanderAvailability Availability { get; set; } = new();
    public CommanderHire Hire { get; set; } = new();
    public bool Unfireable { get; set; }
    public string UnfireableNote { get; set; } = "";
    public bool Rehirable { get; set; } = true;
    /// <summary>null = 공통값 (campaign_economy.json commanderDismissFavorPenalty).</summary>
    public int? DismissFavorPenalty { get; set; }
    public string Bio { get; set; } = "";
    public CommanderFavor Favor { get; set; } = new();
    public string BattleSummary { get; set; } = "";
    public string Portrait { get; set; } = "";
}

/// <summary>풀에 나타나는 조건. FromMission 빈값 · "start" = 처음부터. Until = 이 사건이 일어나면 사라짐 (빈값 = 없음).</summary>
public sealed class CommanderAvailability
{
    public string FromMission { get; set; } = "start";
    public List<string> RequireEvents { get; set; } = new();
    public List<string> RequireFlags { get; set; } = new();
    public string Until { get; set; } = "";
    /// <summary>조건이 하나도 없다 = 처음부터 풀에 나타난다.</summary>
    public bool FromStart => (FromMission.Length == 0 || FromMission == "start") && RequireEvents.Count == 0 && RequireFlags.Count == 0;
}

public sealed class CommanderHire
{
    public int Cost { get; set; }
    public string Condition { get; set; } = "";
    public int MinRegiments { get; set; } = 1;
}

public sealed class CommanderFavor
{
    public int Initial { get; set; } = 30;
    public List<string> Likes { get; set; } = new();
    public List<string> Dislikes { get; set; } = new();
}

/// <summary>특성 카탈로그 한 줄 (commander_traits.json). 1단계는 표시만 — 효과 수치는 0.3 에서 JSON 으로 확정.</summary>
public sealed class CommanderTraitDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
}

/// <summary>연대장 로드 결과: 통과한 항목 + 거부 사유 (Errors, 항목 단위) + 경고 (Warnings). 로그 줄은 한국어 "연대장 &lt;id&gt;: 사유".</summary>
public sealed class CommanderLoadResult
{
    public List<CommanderDef> Defs { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool FileMissing { get; set; }
}

/// <summary>
/// [T48a] 연대장 데이터 로더. 파일이 없으면 빈 풀. 항목 단위 검증: 중복 id · 알 수 없는 학파/특성/태그 · 범위 밖 값은 그 항목만 거부하고 나머지는 읽는다.
/// 모르는 필드는 경고만 (오타 알림, "_" 로 시작하는 필드는 주석이라 조용히 무시).
/// </summary>
public static class CommanderCatalog
{
    public const string CommanderFile = "commanders.json", TraitFile = "commander_traits.json", TagFile = "commander_tags.json";
    public const int TraitsMax = 3, TagsMax = 4, IdMax = 40, NameMax = 12, SummaryMax = 60, DismissPenaltyMax = 5, CostMax = 20, RegimentsMax = 9;
    public static readonly string[] Ranks = { "참령", "부령", "정령" };
    static readonly Regex IdRx = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    static readonly HashSet<string> TopKeys = new() { "id", "name", "rank", "origin", "school", "traits", "availability", "hire", "unfireable", "unfireableNote", "rehirable", "dismissFavorPenalty", "bio", "favor", "battleSummary", "portrait" };
    static readonly HashSet<string> AvailKeys = new() { "fromMission", "requireEvents", "requireFlags", "until" };
    static readonly HashSet<string> HireKeys = new() { "cost", "condition", "minRegiments" };
    static readonly HashSet<string> FavorKeys = new() { "initial", "likes", "dislikes" };

    /// <summary>어휘 (특성 id · 태그 · 학파). 파일이 없으면 비어 있고, 그러면 그것을 쓰는 항목은 거부된다.</summary>
    public sealed class Vocab
    {
        public Dictionary<string, CommanderTraitDef> Traits { get; } = new();
        public HashSet<string> Tags { get; } = new();
        public HashSet<string> Schools { get; } = new();
    }

    static string Dir(string? dir) => dir ?? Path.Combine(AppContext.BaseDirectory, "assets", "data");

    /// <summary>어휘 파일 읽기. 문제는 res.Errors/Warnings 에 한 줄로 (파일 단위).</summary>
    public static Vocab LoadVocab(string dir, CommanderLoadResult res)
    {
        var v = new Vocab();
        string tp = Path.Combine(dir, TraitFile), gp = Path.Combine(dir, TagFile);
        if (!File.Exists(tp)) res.Warnings.Add($"{TraitFile}: 파일이 없다 (특성 목록이 비어 있어 특성을 쓰는 연대장은 모두 거부된다)");
        else if (TryRoot(tp, res, out var tr) && tr.TryGetProperty("traits", out var ta) && ta.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in ta.EnumerateArray())
            {
                try
                {
                    var d = t.Deserialize<CommanderTraitDef>(DataJson.Opts);
                    if (d == null || d.Id.Length == 0) { res.Warnings.Add($"{TraitFile}: id 없는 특성 항목은 무시한다"); continue; }
                    if (!v.Traits.TryAdd(d.Id, d)) res.Warnings.Add($"{TraitFile}: 특성 id '{d.Id}' 중복 (먼저 나온 것만 쓴다)");
                }
                catch (JsonException ex) { res.Warnings.Add($"{TraitFile}: 특성 항목을 읽지 못했다 ({ex.Message})"); }
            }
        }
        else if (File.Exists(tp)) res.Warnings.Add($"{TraitFile}: 'traits' 배열을 찾지 못했다");
        if (!File.Exists(gp)) res.Warnings.Add($"{TagFile}: 파일이 없다 (태그 · 학파 목록이 비어 있어 이를 쓰는 연대장은 모두 거부된다)");
        else if (TryRoot(gp, res, out var gr))
        {
            foreach (var (key, set) in new[] { ("tags", v.Tags), ("schools", v.Schools) })
                if (gr.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var e in arr.EnumerateArray()) if (e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s) set.Add(s);
        }
        return v;
    }

    static bool TryRoot(string path, CommanderLoadResult res, out JsonElement root)
    {
        root = default;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            root = doc.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object) { res.Warnings.Add($"{Path.GetFileName(path)}: 최상위가 객체 {{ }} 가 아니다"); return false; }
            return true;
        }
        catch (JsonException ex) { res.Warnings.Add($"{Path.GetFileName(path)}: JSON 문법 오류 (줄 {ex.LineNumber + 1}, 글자 {ex.BytePositionInLine + 1}) - {ex.Message}"); return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { res.Warnings.Add($"{Path.GetFileName(path)}: 읽을 수 없다 ({ex.Message})"); return false; }
    }

    /// <summary>commanders.json 을 읽는다 (dir = assets/data 폴더, null = 기본). 크래시하지 않는다.</summary>
    public static CommanderLoadResult Load(string? dir = null)
    {
        dir = Dir(dir);
        var res = new CommanderLoadResult();
        var vocab = LoadVocab(dir, res);
        string path = Path.Combine(dir, CommanderFile);
        if (!File.Exists(path)) { res.FileMissing = true; return res; }
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex) { res.Errors.Add($"{CommanderFile}: JSON 문법 오류 (줄 {ex.LineNumber + 1}, 글자 {ex.BytePositionInLine + 1}) - {ex.Message} → 연대장 풀을 비워 둔다"); return res; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { res.Errors.Add($"{CommanderFile}: 읽을 수 없다 ({ex.Message}) → 연대장 풀을 비워 둔다"); return res; }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("commanders", out var arr) || arr.ValueKind != JsonValueKind.Array)
        { res.Errors.Add($"{CommanderFile}: 최상위에 \"commanders\": [ ... ] 배열이 있어야 한다 → 연대장 풀을 비워 둔다"); return res; }

        var seen = new Dictionary<string, int>();
        int n = 0;
        foreach (var el in arr.EnumerateArray())
        {
            n++;
            string label = el.ValueKind == JsonValueKind.Object && el.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.String && idp.GetString() is { Length: > 0 } sid ? sid : $"#{n}";
            if (el.ValueKind != JsonValueKind.Object) { res.Errors.Add($"연대장 {label}: 항목이 객체 {{ }} 가 아니다 (건너뜀)"); continue; }
            foreach (var p in el.EnumerateObject()) if (!TopKeys.Contains(p.Name) && !p.Name.StartsWith('_')) res.Warnings.Add($"연대장 {label}: 알 수 없는 필드 '{p.Name}' (무시)");
            WarnUnknown(el, "availability", AvailKeys, label, res); WarnUnknown(el, "hire", HireKeys, label, res); WarnUnknown(el, "favor", FavorKeys, label, res);
            CommanderDef? d;
            try { d = el.Deserialize<CommanderDef>(DataJson.Opts); }
            catch (JsonException ex) { res.Errors.Add($"연대장 {label}: 필드 형식이 틀렸다 ({ex.Path} 칸의 값 종류가 맞지 않음) (건너뜀)"); continue; }
            if (d == null) { res.Errors.Add($"연대장 {label}: 읽지 못했다 (건너뜀)"); continue; }
            Normalize(d);
            var errs = Validate(d, vocab, res.Warnings);
            if (seen.TryGetValue(d.Id, out int first) && d.Id.Length > 0) errs.Insert(0, $"id 중복 (#{first} 항목과 같다)");
            if (errs.Count > 0) { foreach (var e in errs) res.Errors.Add($"연대장 {label}: {e} (건너뜀)"); continue; }
            seen[d.Id] = n;
            res.Defs.Add(d);
        }
        return res;
    }

    static void WarnUnknown(JsonElement el, string key, HashSet<string> known, string label, CommanderLoadResult res)
    {
        if (el.TryGetProperty(key, out var sub) && sub.ValueKind == JsonValueKind.Object)
            foreach (var p in sub.EnumerateObject()) if (!known.Contains(p.Name) && !p.Name.StartsWith('_')) res.Warnings.Add($"연대장 {label}: 알 수 없는 필드 '{key}.{p.Name}' (무시)");
    }

    /// <summary>null 이 들어온 칸을 기본값으로 (JSON 에서 "x": null).</summary>
    static void Normalize(CommanderDef d)
    {
        d.Id ??= ""; d.Name ??= ""; d.Rank ??= ""; d.Origin ??= ""; d.School ??= ""; d.UnfireableNote ??= ""; d.Bio ??= ""; d.BattleSummary ??= ""; d.Portrait ??= "";
        d.Traits ??= new(); d.Traits.RemoveAll(t => t == null);
        d.Availability ??= new(); d.Availability.FromMission ??= ""; d.Availability.Until ??= ""; d.Availability.RequireEvents ??= new(); d.Availability.RequireFlags ??= new();
        d.Hire ??= new(); d.Hire.Condition ??= "";
        d.Favor ??= new(); d.Favor.Likes ??= new(); d.Favor.Dislikes ??= new();
        d.Favor.Likes.RemoveAll(t => t == null); d.Favor.Dislikes.RemoveAll(t => t == null);
    }

    /// <summary>오류 사유 목록 (비면 통과). 경고는 warnings 에 더한다.</summary>
    static List<string> Validate(CommanderDef d, Vocab v, List<string> warnings)
    {
        var e = new List<string>();
        var eco = CampaignEconomyDefs.Current;
        if (d.Id.Length == 0) e.Add("id 가 없다");
        else if (d.Id.Length > IdMax || !IdRx.IsMatch(d.Id)) e.Add($"id '{d.Id}' 형식 오류 (영소문자·숫자·밑줄만, {IdMax}자 이내)");
        if (d.Name.Trim().Length == 0) e.Add("name(이름)이 비었다");
        else if (d.Name.Length > NameMax) e.Add($"name '{d.Name}' 이 너무 길다 ({NameMax}자 이내)");
        if (Array.IndexOf(Ranks, d.Rank) < 0) e.Add($"rank(계급) '{d.Rank}' 은 허용값이 아니다 ({string.Join(" · ", Ranks)})");
        if (d.School.Length == 0) e.Add("school(학파)가 비었다 (없으면 '없음')");
        else if (!v.Schools.Contains(d.School)) e.Add($"school(학파) '{d.School}' 은 알 수 없다 (commander_tags.json schools: {string.Join(" · ", v.Schools)})");
        if (d.Traits.Count > TraitsMax) e.Add($"traits(특성)는 최대 {TraitsMax}개 (현재 {d.Traits.Count}개)");
        if (d.Traits.Distinct().Count() != d.Traits.Count) e.Add("traits(특성)에 같은 것이 두 번 들어 있다");
        foreach (var t in d.Traits) if (!v.Traits.ContainsKey(t)) e.Add($"traits(특성) '{t}' 은 알 수 없다 (commander_traits.json 에 없음)");
        if (d.Hire.Cost < 0 || d.Hire.Cost > CostMax) e.Add($"hire.cost(채용 비용) {d.Hire.Cost} 이 범위 밖이다 (0~{CostMax})");
        if (d.Hire.MinRegiments < 1 || d.Hire.MinRegiments > RegimentsMax) e.Add($"hire.minRegiments {d.Hire.MinRegiments} 이 범위 밖이다 (1~{RegimentsMax})");
        if (d.DismissFavorPenalty is int p && (p < 0 || p > DismissPenaltyMax)) e.Add($"dismissFavorPenalty(해임 시 호감 하락) {p} 이 범위 밖이다 (0~{DismissPenaltyMax}, 비우면 공통값)");
        if (d.Favor.Initial < eco.CommanderFavorMin || d.Favor.Initial > eco.CommanderFavorMax) e.Add($"favor.initial(호감 초기값) {d.Favor.Initial} 이 범위 밖이다 ({eco.CommanderFavorMin}~{eco.CommanderFavorMax})");
        foreach (var (list, nm) in new[] { (d.Favor.Likes, "likes"), (d.Favor.Dislikes, "dislikes") })
        {
            if (list.Count > TagsMax) e.Add($"favor.{nm}(선호/기피 태그)는 최대 {TagsMax}개 (현재 {list.Count}개)");
            foreach (var t in list) if (!v.Tags.Contains(t)) e.Add($"favor.{nm} 태그 '{t}' 는 알 수 없다 (commander_tags.json tags)");
        }
        if (d.Favor.Likes.Intersect(d.Favor.Dislikes).Any()) e.Add("favor 선호와 기피에 같은 태그가 있다");
        if (d.Unfireable && d.DismissFavorPenalty is > 0) warnings.Add($"연대장 {d.Id}: 해임 불가(unfireable)라 dismissFavorPenalty 는 쓰이지 않는다");
        if (d.BattleSummary.Length > SummaryMax) warnings.Add($"연대장 {d.Id}: battleSummary 가 {SummaryMax}자를 넘는다 (표시가 잘릴 수 있음)");
        if (d.Portrait.Length > 0 && !d.Portrait.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) warnings.Add($"연대장 {d.Id}: portrait '{d.Portrait}' 는 .png 가 아니다");
        if (d.Unfireable && d.UnfireableNote.Length == 0) warnings.Add($"연대장 {d.Id}: 해임 불가인데 unfireableNote(사유 문구)가 비었다");
        return e;
    }

    // ---- 현재 정의 (게임 전체가 읽음). 테스트는 Override.
    static CommanderLoadResult? current;
    public static CommanderLoadResult Current => current ??= LogLoad(Load());
    public static void Override(CommanderLoadResult? r) => current = r;
    /// <summary>다시 읽는다 (파일 고친 뒤).</summary>
    public static CommanderLoadResult Reload() => current = LogLoad(Load());

    static CommanderLoadResult LogLoad(CommanderLoadResult r)
    {
        foreach (var m in r.Errors) Console.Error.WriteLine("[commanders] 오류: " + m);
        foreach (var m in r.Warnings) Console.Error.WriteLine("[commanders] 경고: " + m);
        return r;
    }

    public static CommanderDef? Find(string id) => Current.Defs.FirstOrDefault(d => d.Id == id);

    /// <summary>해임 때 깎는 호감 (양수). 해임 불가 = 0, 인물별 값이 있으면 그것, 없으면 공통값 (campaign_economy.json commanderDismissFavorPenalty).</summary>
    public static int DismissPenalty(CommanderDef d) => d.Unfireable ? 0 : d.DismissFavorPenalty ?? CampaignEconomyDefs.Current.CommanderDismissFavorPenalty;
}

/// <summary>[T48a] 풀 상태 문자열 (CampaignState.CommanderPool 의 Status). 전이 규칙은 T48b.</summary>
public static class CommanderStatus
{
    public const string Locked = "locked";             // 등장 조건 미충족 (UI 는 '???')
    public const string Available = "available";       // 기용 가능
    public const string Hired = "hired";               // 재직 중
    public const string Dismissed = "dismissed";       // 해임됨 (쿨다운 뒤 재기용 가능)
    public const string Unavailable = "unavailable";   // 돌이킬 수 없이 불가 (조건으로)
    public static readonly string[] All = { Locked, Available, Hired, Dismissed, Unavailable };
}

/// <summary>[T48a] 풀 상태 동기화: 데이터에 새로 생긴 id 를 상태에 더한다. 데이터에서 사라진 id 의 상태는 지우지 않고 보존한다 (표시만 '알 수 없음').</summary>
public static class CommanderPools
{
    /// <summary>defs 의 새 id 를 s.CommanderPool 에 추가 (정의 순서). 처음부터 나타나는 인물 = available, 조건이 있는 인물 = locked (전이는 T48b). 추가한 수.</summary>
    public static int Sync(CampaignState s, IEnumerable<CommanderDef> defs)
    {
        s.CommanderPool ??= new();
        var have = new HashSet<string>(s.CommanderPool.Select(c => c.Id));
        int added = 0;
        foreach (var d in defs)
        {
            if (!have.Add(d.Id)) continue;
            s.CommanderPool.Add(new CampaignState.CommanderSlot { Id = d.Id, Status = d.Availability.FromStart ? CommanderStatus.Available : CommanderStatus.Locked, Favor = d.Favor.Initial });
            added++;
        }
        return added;
    }

    public static int Sync(CampaignState s) => Sync(s, CommanderCatalog.Current.Defs);
}
