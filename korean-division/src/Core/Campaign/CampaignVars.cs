using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>[TC-E] 캠페인 변수 한 칸 정의 (assets/data/campaign_vars.json). 값은 CampaignState.Flags 에 저장 — alias 가 있으면 옛 키에 (heir_mood 등), 없으면 key 그대로.</summary>
public sealed class CampaignVarDef
{
    public string Key { get; set; } = "";
    /// <summary>옛 Flags 키 (있으면 그 키에 저장 — 기존 경제·이벤트 코드와 같은 값을 공유).</summary>
    public string? Alias { get; set; }
    public int Min { get; set; }
    public int Max { get; set; } = 100;
    public int Default { get; set; }
    /// <summary>"rosterMoraleAvg" = 저장 변수가 아니라 중대별 사기(RosterCompany.Morale)에서 계산되는 파생값 (병력 0 중대 제외 평균, 내림 / 중대 없으면 Default). 쓰기 · 증감은 모든 중대에 적용.</summary>
    public string? Derived { get; set; }
    public bool IsRosterMorale => Derived == "rosterMoraleAvg";
    public string StoreKey => string.IsNullOrEmpty(Alias) ? Key : Alias!;
}

public sealed class CampaignVarEntry
{
    public string Key { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>
/// [TC-E] 변수 레지스트리: favor.* · rep.* · morale.* · intel.* · pow.* · flag.* · unlock.* · maxCasualtyFrac. 정의 (범위 · 초기값) 는 JSON, 값은 CampaignState.Flags.
/// 모르는 키는 읽으면 0 / 쓰면 거부. 값은 항상 [Min, Max] 로 자른다. 호감 등의 증감 크기는 [보류]라 이 클래스는 증감 값을 정하지 않는다.
/// 저장 형식은 그대로 (Flags 가 이미 저장됨) — 결정 응답 기록만 CampaignState.Decisions (v13).
/// </summary>
public static class CampaignVars
{
    static List<CampaignVarDef>? defs;
    static Dictionary<string, CampaignVarDef>? byKey;

    public static IReadOnlyList<CampaignVarDef> Defs { get { Ensure(); return defs!; } }

    static void Ensure()
    {
        if (defs != null) return;
        SetDefs(Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "campaign_vars.json")));
    }

    static void SetDefs(List<CampaignVarDef> d)
    {
        defs = d;
        byKey = new Dictionary<string, CampaignVarDef>();
        foreach (var x in d) byKey[x.Key] = x;
    }

    /// <summary>[테스트] 정의를 갈아 끼운다 (null = 파일 다시 읽기).</summary>
    public static void Override(List<CampaignVarDef>? d) { if (d == null) { defs = null; byKey = null; } else SetDefs(Sanitized(d)); }

    sealed class FileModel { public List<CampaignVarDef>? Vars { get; set; } }

    /// <summary>파일이 없거나 깨지면 빈 목록 (변수 없음 — 모든 읽기 0). 부분적으로 깨진 항목만 버린다.</summary>
    public static List<CampaignVarDef> Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), DataJson.Opts)?.Vars is { } v) return Sanitized(v);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { }
        return new();
    }

    /// <summary>키 없음 · 중복 · 같은 저장 키를 쓰는 항목 · min &gt; max 인 항목은 버리고, 초기값은 범위로 자른다. 입력 순서 유지.</summary>
    public static List<CampaignVarDef> Sanitized(IEnumerable<CampaignVarDef?> src)
    {
        var res = new List<CampaignVarDef>();
        var keys = new HashSet<string>(); var stores = new HashSet<string>();
        foreach (var d in src)
        {
            if (d == null || string.IsNullOrWhiteSpace(d.Key) || d.Min > d.Max) continue;
            if (!keys.Add(d.Key) || !stores.Add(d.StoreKey)) continue;
            d.Default = Math.Clamp(d.Default, d.Min, d.Max);
            res.Add(d);
        }
        return res;
    }

    public static bool Has(string key) { Ensure(); return key != null && byKey!.ContainsKey(key); }
    public static CampaignVarDef? Def(string key) { Ensure(); return key != null && byKey!.TryGetValue(key, out var d) ? d : null; }

    /// <summary>값 읽기: 저장돼 있지 않으면 초기값, 모르는 키는 0. 손상된 저장값도 범위로 잘라 돌려준다.</summary>
    public static int Get(CampaignState s, string key)
    {
        var d = Def(key); if (d == null) return 0;
        if (d.IsRosterMorale) return RosterMoraleAverage(s, d);
        return s.Flags != null && s.Flags.TryGetValue(d.StoreKey, out var v) ? Math.Clamp(v, d.Min, d.Max) : d.Default;
    }

    /// <summary>값 쓰기 (범위로 자름). 모르는 키면 false (아무것도 안 바뀜).</summary>
    public static bool Set(CampaignState s, string key, int value)
    {
        var d = Def(key); if (d == null) return false;
        if (d.IsRosterMorale) { int v = Math.Clamp(value, d.Min, d.Max); foreach (var c in s.Roster) c.Morale = v; return true; }
        (s.Flags ??= new())[d.StoreKey] = Math.Clamp(value, d.Min, d.Max);
        return true;
    }

    /// <summary>증감 (long 으로 더해 넘침 방지, 범위로 자름). 모르는 키면 false.</summary>
    public static bool Add(CampaignState s, string key, int delta)
    {
        var d = Def(key); if (d == null) return false;
        if (d.IsRosterMorale) { foreach (var c in s.Roster) c.Morale = (int)Math.Clamp((long)c.Morale + delta, d.Min, d.Max); return true; }
        return Set(s, key, (int)Math.Clamp((long)Get(s, key) + delta, d.Min, d.Max));
    }

    /// <summary>중대 사기 평균: 병력 0 중대 제외, 정수 합 / 개수 (내림, 결정적). 대상 중대가 없으면 d.Default (50).</summary>
    public static int RosterMoraleAverage(CampaignState s, CampaignVarDef d)
    {
        long sum = 0; int n = 0;
        if (s.Roster != null) foreach (var c in s.Roster) if (c != null && c.Strength > 0) { sum += Math.Clamp(c.Morale, 0, 100); n++; }
        return n == 0 ? d.Default : (int)Math.Clamp(sum / n, d.Min, d.Max);
    }

    /// <summary>전체 변수의 현재 값 (정의 순서) — 재도전 복원 체크포인트용.</summary>
    public static List<CampaignVarEntry> SnapshotAll(CampaignState s)
    {
        Ensure();
        return defs!.Where(d => !d.IsRosterMorale).Select(d => new CampaignVarEntry { Key = d.Key, Value = Get(s, d.Key) }).ToList();
    }

    /// <summary>체크포인트로 되돌린다 (모르는 키는 무시).</summary>
    public static void RestoreAll(CampaignState s, IEnumerable<CampaignVarEntry>? snap)
    {
        if (snap == null) return;
        foreach (var e in snap) if (e != null && e.Key != null && Def(e.Key) is { IsRosterMorale: false }) Set(s, e.Key, e.Value);
    }

    /// <summary>저장된 Flags 의 등록 변수 값을 범위로 되돌린다 (손상 · 수작업 편집).</summary>
    public static void Sanitize(CampaignState s)
    {
        if (s.Flags == null) return;
        Ensure();
        foreach (var d in defs!)
            if (!d.IsRosterMorale && s.Flags.TryGetValue(d.StoreKey, out var v)) s.Flags[d.StoreKey] = Math.Clamp(v, d.Min, d.Max);
    }

    // ---------- [g3] 미션 변수 ↔ 캠페인 변수 ----------
    /// <summary>미션 사건이 읽거나 쓰는 변수 중 레지스트리에 있는 것의 지금 값 (World.CreateMissionWorld 의 startVars). 이름 순.</summary>
    public static SortedDictionary<string, int> StartVarsFor(CampaignState s, MissionDef def)
    {
        var r = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in def.VarNames()) if (Has(n)) r[n] = Get(s, n);
        return r;
    }

    /// <summary>미션 끝의 변수를 캠페인에 반영한다 (대입 — 시작값을 받은 변수라 미션이 곧 최종값). 모르는 키는 무시. 반영한 개수.</summary>
    public static int ApplyMissionVars(CampaignState s, IReadOnlyDictionary<string, int>? vars)
    {
        int n = 0;
        if (vars != null) foreach (var kv in vars.OrderBy(k => k.Key, StringComparer.Ordinal)) if (Set(s, kv.Key, kv.Value)) n++;
        return n;
    }
}
