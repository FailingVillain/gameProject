using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>장교 학파 (대한제국군) 정의 — 읽기 전용 자료 (docs/OFFICER_DOCTRINES.md). 시뮬레이션이 아직 쓰지 않음.</summary>
public sealed class DoctrineSchool
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Officer { get; set; } = "";
    public string Tactics { get; set; } = "";
    public List<string> Ai { get; set; } = new();
    public List<string> Research { get; set; } = new();
    public List<string> Figures { get; set; } = new();
    /// <summary>AI 경향 축 수치 (문서에 없음 → null, DECISIONS_PENDING).</summary>
    public Dictionary<string, double>? Axes { get; set; }
}

/// <summary>적 세력 성향 하나.</summary>
public sealed class Disposition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Character { get; set; } = "";
    public List<string> Ai { get; set; } = new();
    public Dictionary<string, double>? Axes { get; set; }
}

public sealed class FactionDoctrine
{
    public string Name { get; set; } = "";
    public string? SharesWith { get; set; }
    public List<Disposition> Dispositions { get; set; } = new();
}

/// <summary>assets/data/doctrines.json 로더 · 검증. 읽기 전용 정의, Defs.Hash 와 무관 (시뮬 미사용).</summary>
public sealed class Doctrines
{
    public int Version { get; set; }
    public string Source { get; set; } = "";
    public double[] RetreatCasualtyBasis { get; set; } = Array.Empty<double>();
    public List<string> AiAxes { get; set; } = new();
    public List<DoctrineSchool> Schools { get; set; } = new();
    public Dictionary<string, FactionDoctrine> Factions { get; set; } = new();

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "assets", "data", "doctrines.json");

    public static Doctrines Load(string? path = null)
    {
        path ??= DefaultPath;
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var d = JsonSerializer.Deserialize<Doctrines>(File.ReadAllText(path), opts) ?? throw new InvalidDataException($"빈 데이터 파일: {path}");
        var errs = d.Validate();
        if (errs.Count > 0) throw new InvalidDataException($"doctrines.json: {string.Join("; ", errs)}");
        return d;
    }

    public DoctrineSchool? School(string key) => Schools.FirstOrDefault(s => s.Key == key);

    /// <summary>세력 성향 목록 (SharesWith 따라감, 예: bandit → fengtian.toby).</summary>
    public IReadOnlyList<Disposition> DispositionsOf(string faction)
    {
        if (!Factions.TryGetValue(faction, out var f)) return Array.Empty<Disposition>();
        if (f.SharesWith == null) return f.Dispositions;
        var parts = f.SharesWith.Split('.');
        return Factions.TryGetValue(parts[0], out var o) ? o.Dispositions.Where(x => parts.Length < 2 || x.Key == parts[1]).ToList() : Array.Empty<Disposition>();
    }

    /// <summary>스키마 · 키 중복 · 공유 참조 검사. 오류 문장 목록 (비면 정상).</summary>
    public List<string> Validate()
    {
        var e = new List<string>();
        if (Version < 1) e.Add("version");
        if (Schools == null || Schools.Count == 0) { e.Add("schools 비어 있음"); Schools = new(); }
        if (Factions == null) { e.Add("factions 없음"); Factions = new(); }
        var seen = new HashSet<string>();
        foreach (var s in Schools)
        {
            if (s == null) { e.Add("학파 항목이 null"); continue; }
            if (string.IsNullOrWhiteSpace(s.Key) || !seen.Add(s.Key)) e.Add($"학파 키 비었거나 중복: '{s.Key}'");
            if (string.IsNullOrEmpty(s.Name) || string.IsNullOrEmpty(s.Officer) || string.IsNullOrEmpty(s.Tactics) || s.Ai == null || s.Ai.Count == 0) e.Add($"학파 {s.Key}: 필수 항목 누락");
            if (s.Research == null) e.Add($"학파 {s.Key}: research 가 null (빈 목록 [] 으로 쓸 것)");
            if (s.Figures == null) e.Add($"학파 {s.Key}: figures 가 null (빈 목록 [] 으로 쓸 것)");
        }
        foreach (var (fk, f) in Factions)
        {
            if (f == null) { e.Add($"세력 {fk}: null"); continue; }
            if (string.IsNullOrEmpty(f.Name)) e.Add($"세력 {fk}: 이름 없음");
            if (f.Dispositions == null) { e.Add($"세력 {fk}: dispositions 가 null"); f.Dispositions = new(); }
            var keys = new HashSet<string>();
            foreach (var x in f.Dispositions)
            {
                if (x == null) { e.Add($"성향 항목이 null: {fk}"); continue; }
                if (string.IsNullOrWhiteSpace(x.Key) || !keys.Add(x.Key)) e.Add($"성향 키 비었거나 중복: {fk}.'{x.Key}'");
                if (string.IsNullOrEmpty(x.Name) || string.IsNullOrEmpty(x.Character) || x.Ai == null || x.Ai.Count == 0) e.Add($"성향 {fk}.{x.Key}: 필수 항목 누락");
            }
            if (f.SharesWith != null)
            {
                var p = f.SharesWith.Split('.');
                bool ok = p.Length == 2 && Factions.TryGetValue(p[0], out var o) && o != null && o.SharesWith == null && o.Dispositions != null && o.Dispositions.Any(x => x.Key == p[1]);
                if (!ok) e.Add($"세력 {fk}: sharesWith '{f.SharesWith}' 참조 오류");
                if (f.Dispositions.Count > 0) e.Add($"세력 {fk}: sharesWith 와 자체 성향 동시 지정");
            }
        }
        return e;
    }
}
