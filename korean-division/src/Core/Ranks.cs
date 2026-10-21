using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>세력별 계급 이름 한 벌 (사병 9단계 · 장교 [0] 기본 + 6단계 · 장성). null/빈 항목 = 미사용 계급.</summary>
public sealed class RankNameSet
{
    public string?[] Enlisted { get; set; } = Array.Empty<string?>();
    public string?[] Officer { get; set; } = Array.Empty<string?>();
    /// <summary>장성 (통합 색인 16, 표시 · 브리핑 전용).</summary>
    public string?[] General { get; set; } = Array.Empty<string?>();
    public string?[] EnlistedEn { get; set; } = Array.Empty<string?>();
    public string?[] OfficerEn { get; set; } = Array.Empty<string?>();
    public string?[] GeneralEn { get; set; } = Array.Empty<string?>();
}

/// <summary>
/// 계급 이름 (assets/data/ranks.json, 세력 키 = 지도 factions[].en, 없으면 "default" = 대한제국). 표시 전용 — 시뮬레이션은 계급 번호만 쓴다.
/// 미사용 계급 (null): 승진은 건너뛰고 (Next), 지정 계급은 아래로 맞춘다 (Snap). 파일이 없거나 깨졌으면 기본 이름 (대한제국식).
/// </summary>
public static class RankNames
{
    /// <summary>통합 색인 수 (0-4 사병 · 5-8 부사관 · 9-11 위관 · 12-14 영관 · 15 미사용 · 16 장성).</summary>
    public const int UnifiedLevels = 17;

    static readonly RankNameSet Builtin = new()
    {
        Enlisted = new[] { "훈련병", "이등병", "일등병", "상등병", null, "참교", "부교", "정교", "특무정교" },
        Officer = new[] { "장교", "참위", "부위", "정위", "참령", "부령", "정령" },
        General = new[] { "참장", "부장", "대장" },
    };
    static Dictionary<string, RankNameSet> sets = new(StringComparer.OrdinalIgnoreCase) { ["default"] = Builtin };

    /// <summary>지도 세력 키 별칭 (지도 키 → ranks.json 키).</summary>
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["russian"] = "russian-white", ["soviet"] = "russian-red" };

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "assets", "data", "ranks.json");

    /// <summary>ranks.json 읽기 (Defs.Load 가 부른다). 실패하면 기본 이름 유지.</summary>
    public static void Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            var d = JsonSerializer.Deserialize<Dictionary<string, RankNameSet>>(File.ReadAllText(path), opts);
            if (d == null || d.Count == 0) return;
            var res = new Dictionary<string, RankNameSet>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in d) res[k] = v;
            if (!res.ContainsKey("default")) res["default"] = Builtin;
            sets = res;
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
    }

    static RankNameSet Set(string? faction)
    {
        if (faction != null)
        {
            if (sets.TryGetValue(faction, out var s)) return s;
            if (Aliases.TryGetValue(faction, out var a) && sets.TryGetValue(a, out s)) return s;
        }
        return sets["default"];
    }

    static string? At(string?[] a, int i) => i >= 0 && i < a.Length && !string.IsNullOrEmpty(a[i]) ? a[i] : null;

    // ---------------------------------------------------------------- 미사용 계급 (사병)

    /// <summary>사병 계급 rank (0..8) 가 이 세력에서 쓰이는가.</summary>
    public static bool EnlistedUsed(string? faction, int rank) => rank >= 0 && rank < 9 && At(Set(faction).Enlisted, rank) != null;

    /// <summary>미사용 계급이면 아래로 내려 맞춘 사병 계급 (아래에 쓰이는 계급이 없으면 위로). 쓰이는 계급이면 그대로. 0 이하 = 그대로 (계급 없음).</summary>
    public static int Snap(string? faction, int rank)
    {
        if (rank <= 0 || EnlistedUsed(faction, rank)) return rank;
        rank = Math.Min(rank, 8);
        for (int r = rank; r >= 0; r--) if (EnlistedUsed(faction, r)) return r;
        for (int r = rank; r <= 8; r++) if (EnlistedUsed(faction, r)) return r;
        return rank;
    }

    /// <summary>한 계급 승진한 계급 (미사용 건너뜀). 더 오를 곳이 없으면 그대로.</summary>
    public static int Next(string? faction, int rank)
    {
        for (int r = Math.Max(0, rank + 1); r <= 8; r++) if (EnlistedUsed(faction, r)) return r;
        return rank;
    }

    // ---------------------------------------------------------------- 이름

    static string Pick(string?[] own, string?[] fallback, int i)
    {
        if (At(own, i) is { } n) return n;
        if (At(fallback, i) is { } f) return f;
        return "?";
    }

    /// <summary>사병 계급 이름 (0..8). 미사용 계급은 아래 계급 이름으로 (Snap).</summary>
    public static string Enlisted(string? faction, int rank)
    {
        var set = Set(faction);
        int r = Math.Min(Math.Max(0, rank), 8);
        if (At(set.Enlisted, r) == null) { r = Snap(faction, r); while (r < 8 && At(set.Enlisted, r) == null) r++; }   // 0 이 미사용 (마적 · 적군) 이면 위로
        return Pick(set.Enlisted, Builtin.Enlisted, r);
    }

    /// <summary>장교 계급 이름 (0 = 기본 '장교').</summary>
    public static string Officer(string? faction, int rank) => Pick(Set(faction).Officer, Builtin.Officer, Math.Min(Math.Max(0, rank), 6));

    /// <summary>유닛의 계급 이름.</summary>
    public static string Of(string? faction, Unit u) => u.IsOfficer ? Officer(faction, u.OfficerRank) : Enlisted(faction, u.Rank);

    /// <summary>장성 이름 (통합 색인 16, 표시 · 브리핑 전용 — 전투 유닛은 쓰지 않음). 세력에 장성이 없으면 null.</summary>
    public static string? General(string? faction, int index) => At(Set(faction).General, index);

    public static int GeneralCount(string? faction) => Set(faction).General.Count(g => !string.IsNullOrEmpty(g));

    /// <summary>통합 색인 0..16 의 이름 (문서 기준). 미사용 (15 · 세력별 null · 장성 없음) = null. 16 = 첫 장성.</summary>
    public static string? Unified(string? faction, int level)
    {
        var s = Set(faction);
        if (level >= 0 && level <= 8) return At(s.Enlisted, level);
        if (level >= 9 && level <= 14) return At(s.Officer, level - 8);
        return level == 16 ? At(s.General, 0) : null;
    }

    /// <summary>폰트 아틀라스용 모든 이름.</summary>
    public static IEnumerable<string> AllText() =>
        sets.Values.SelectMany(s => s.Enlisted.Concat(s.Officer).Concat(s.General)).Concat(Builtin.Enlisted).Concat(Builtin.Officer).Concat(Builtin.General)
            .Where(x => !string.IsNullOrEmpty(x)).Select(x => x!);
}
