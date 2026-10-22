using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>[T48c] 호감 단계 한 줄 (assets/data/commander_favor.json).</summary>
public sealed class FavorTier
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int MinFavor { get; set; }
    /// <summary>특성 효과 배율 (천분율 정수, 1000 = 1.0배).</summary>
    public int EffectPermille { get; set; } = 1000;
}

/// <summary>[T48c] 연대장 호감 규칙 데이터. 파일 없음 · 깨짐 = 기본값 + 경고. 전부 임시값.</summary>
public sealed class CommanderFavorRules
{
    public bool ScalingEnabled { get; set; } = true;
    public int[] DeltaSizes { get; set; } = { 1, 2, 3, 5 };
    public int TagMatchDelta { get; set; } = 1;
    public List<FavorTier> Tiers { get; set; } = Default();
    public List<string> Warnings { get; } = new();

    static List<FavorTier> Default() => new()
    {
        new() { Id = "cold", Name = "냉담", MinFavor = -1000, EffectPermille = 500 },
        new() { Id = "normal", Name = "보통", MinFavor = 10, EffectPermille = 1000 },
        new() { Id = "good", Name = "호의", MinFavor = 40, EffectPermille = 1150 },
        new() { Id = "trusted", Name = "신뢰", MinFavor = 70, EffectPermille = 1300 },
    };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "assets", "data", "commander_favor.json");
    static CommanderFavorRules? current;
    public static CommanderFavorRules Current => current ??= Load(FilePath);
    /// <summary>[테스트] 갈아 끼운다 (null = 다시 읽기).</summary>
    public static void Override(CommanderFavorRules? r) => current = r;

    public static CommanderFavorRules Load(string path)
    {
        var r = new CommanderFavorRules();
        if (!File.Exists(path)) { r.Warnings.Add("commander_favor.json 없음: 기본값 사용"); return r; }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException("최상위가 객체가 아님");
            if (root.TryGetProperty("scalingEnabled", out var se) && (se.ValueKind == JsonValueKind.True || se.ValueKind == JsonValueKind.False)) r.ScalingEnabled = se.GetBoolean();
            if (root.TryGetProperty("deltaSizes", out var ds) && ds.ValueKind == JsonValueKind.Array)
            {
                var l = new List<int>();
                foreach (var e in ds.EnumerateArray()) if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int v) && v > 0) l.Add(v);
                if (l.Count > 0) r.DeltaSizes = l.Distinct().OrderBy(x => x).ToArray();
            }
            if (root.TryGetProperty("tagMatchDelta", out var tm) && tm.ValueKind == JsonValueKind.Number && tm.TryGetInt32(out int tmv)) r.TagMatchDelta = tmv;
            if (!r.DeltaSizes.SequenceEqual(new[] { 1, 2, 3, 5 })) r.Warnings.Add($"deltaSizes {{{string.Join(",", r.DeltaSizes)}}} 는 사용자 결정 {{1,2,3,5}} 와 다름");
            if (!r.DeltaSizes.Contains(r.TagMatchDelta))
            {
                r.Warnings.Add($"tagMatchDelta {r.TagMatchDelta} 는 허용 증감 크기 {{{string.Join(",", r.DeltaSizes)}}} 밖: 1 로 되돌림");
                r.TagMatchDelta = 1;
            }
            if (root.TryGetProperty("tiers", out var tiers) && tiers.ValueKind == JsonValueKind.Array)
            {
                var l = new List<FavorTier>();
                foreach (var e in tiers.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("minFavor", out var mf) || !mf.TryGetInt32(out int min)
                        || !e.TryGetProperty("effectPermille", out var ep) || !ep.TryGetInt32(out int perm) || perm < 0 || perm > 5000)
                    { r.Warnings.Add("tiers 항목 형식 틀림: 건너뜀"); continue; }
                    string id = e.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : "";
                    string nm = e.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : id;
                    l.Add(new FavorTier { Id = id, Name = nm, MinFavor = min, EffectPermille = perm });
                }
                l = l.OrderBy(t => t.MinFavor).ToList();
                if (l.Count > 0) r.Tiers = l; else r.Warnings.Add("tiers 비어 있음: 기본 단계 사용");
            }
        }
        catch (JsonException e)
        {
            var d = new CommanderFavorRules();
            d.Warnings.Add("commander_favor.json 깨짐: 기본값 사용 (" + e.Message + ")");
            return d;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // 잠김 · 권한 없음: 정적 초기화에서 던지지 않는다
        {
            var d = new CommanderFavorRules();
            d.Warnings.Add("commander_favor.json 읽을 수 없음: 기본값 사용 (" + e.Message + ")");
            return d;
        }
        return r;
    }
}

/// <summary>[T48c] 연대장 호감 증감 · 단계 · 재기용 판정. 정수만 (결정론). 소비처 3: 해임 하락 · 작전 종료 선호/기피 태그 ±n(작전당 1회) · 이벤트 선택지 favorDelta.
/// 기용/해임 상태 전이 자체는 T48b — 여기서는 호감 값과 판정만 다룬다.</summary>
public static class CommanderFavors
{
    static CampaignState.CommanderSlot? Slot(CampaignState s, string id) => s.CommanderPool.FirstOrDefault(c => c.Id == id);

    /// <summary>호감 증감 (경제 JSON 범위로 클램프). 반환 = 실제 바뀐 값.</summary>
    public static int Add(CampaignState.CommanderSlot slot, int delta)
    {
        var eco = CampaignEconomyDefs.Current;
        int before = slot.Favor;
        slot.Favor = (int)Math.Clamp((long)slot.Favor + delta, eco.CommanderFavorMin, eco.CommanderFavorMax);
        return slot.Favor - before;
    }

    /// <summary>해임에 따른 호감 하락 (소비처 1). 해임 불가 인물은 0 (호감 불변). 반환 = 실제 바뀐 값 (&lt;= 0). T48b 의 해임 처리가 호출한다.</summary>
    public static int ApplyDismissPenalty(CampaignState s, string id)
    {
        var slot = Slot(s, id);
        if (slot == null) return 0;
        var def = CommanderCatalog.Find(id);
        int pen = def == null ? CampaignEconomyDefs.Current.CommanderDismissFavorPenalty : CommanderCatalog.DismissPenalty(def);
        return pen <= 0 ? 0 : Add(slot, -pen);
    }

    /// <summary>작전 종료 태그 반영 (소비처 2): 재직(hired) 연대장만, 같은 작전은 한 번만 (FavorLastMission). 결과 태그 중 선호가 있으면 +n, 기피가 있으면 -n (둘 다면 상쇄).
    /// tags = 호출자가 기존 입력 (사상률/승패/포로 정책/이벤트) 에서 뽑은 태그 어휘. 반환 = 호감이 바뀐 연대장 수.</summary>
    public static int ApplyMissionTags(CampaignState s, string missionId, IReadOnlyCollection<string> tags)
    {
        if (string.IsNullOrEmpty(missionId)) return 0;
        int n = CommanderFavorRules.Current.TagMatchDelta, changed = 0;
        foreach (var slot in s.CommanderPool)
        {
            if (slot.Status != CommanderStatus.Hired || slot.FavorLastMission == missionId) continue;
            var def = CommanderCatalog.Find(slot.Id);
            if (def == null) continue;
            slot.FavorLastMission = missionId;
            int d = (def.Favor.Likes.Any(tags.Contains) ? n : 0) - (def.Favor.Dislikes.Any(tags.Contains) ? n : 0);
            if (d != 0 && Add(slot, d) != 0) changed++;
        }
        return changed;
    }

    /// <summary>이벤트 선택지 favorDelta 적용 (소비처 3). |n| 이 허용 크기 밖이거나 모르는 id 는 건너뜀. 반환 = 적용 건수.</summary>
    public static int ApplyEventDeltas(CampaignState s, Dictionary<string, int> deltas)
    {
        int applied = 0;
        foreach (var kv in deltas.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var slot = Slot(s, kv.Key);
            if (slot == null || kv.Value == 0 || !CommanderFavorRules.Current.DeltaSizes.Contains(Math.Abs(kv.Value))) continue;
            Add(slot, kv.Value); applied++;
        }
        return applied;
    }

    public static FavorTier TierOf(int favor)
    {
        var tiers = CommanderFavorRules.Current.Tiers;
        var t = tiers[0];
        foreach (var x in tiers) if (favor >= x.MinFavor) t = x;
        return t;
    }

    /// <summary>특성 효과 배율 (천분율). 배율 기능이 꺼져 있으면 항상 1000.</summary>
    public static int EffectPermille(int favor) => CommanderFavorRules.Current.ScalingEnabled ? TierOf(favor).EffectPermille : 1000;

    /// <summary>호감 단계 배율을 정수 효과값에 곱한다 (정수 나눗셈, 0 쪽으로 버림; 음수도 대칭).</summary>
    public static int Scale(int baseValue, int favor) => (int)((long)baseValue * EffectPermille(favor) / 1000);

    /// <summary>재기용 가능? 해임(dismissed) 상태 + rehirable + 쿨다운 경과 + 호감이 최소값 초과. 쿨다운 = 해임일 + 작전 수 × missionDays 일 (missionDays 0 이면 날이 안 흐르므로 쿨다운 면제).</summary>
    public static bool CanRehire(CampaignState s, string id, out string reason)
    {
        reason = "";
        var slot = Slot(s, id); var def = CommanderCatalog.Find(id);
        if (slot == null || def == null) { reason = "명단에 없음"; return false; }
        if (slot.Status != CommanderStatus.Dismissed) { reason = "해임 상태가 아님"; return false; }
        if (!def.Rehirable) { reason = "재기용 불가 인물"; return false; }
        var eco = CampaignEconomyDefs.Current;
        long wait = (long)eco.CommanderRehireCooldownMissions * eco.MissionDays;
        if (eco.MissionDays > 0 && s.Day < slot.DismissedDay + wait) { reason = $"해임 뒤 {eco.CommanderRehireCooldownMissions}작전이 지나야 함"; return false; }
        if (slot.Favor <= eco.CommanderRehireMinFavor) { reason = "호감이 너무 낮음"; return false; }
        return true;
    }
}
