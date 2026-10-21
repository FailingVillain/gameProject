using System.Text.Json;
using System.Text.Json.Nodes;

namespace KoreanDivision.Core;

/// <summary>
/// 캠페인 병력 이월 (미션 JSON 에 "carry": true 인 미션). 이전 미션에서 살아남은 소총병 · 장교를 미션 텍스트의 병력 "b1" 중대에 써 넣는다:
/// 소총병 계급 → 중대 "infantryRanks" (계급 높은 순 · 이전 중대 id 순, 중대 정원까지), 모자란 자리는 보충병 (계급 1 이등병 = 기본),
/// 장교 → 중대 "officerRanks" · "officerXp". 시뮬 상태가 아니라 미션 텍스트가 바뀌는 것이라 스냅샷 · 리플레이는 써 넣은 텍스트를 그대로 싣는다 (결정적).
/// </summary>
public static class CampaignCarry
{
    public const string ForceId = "b1";
    static readonly JsonDocumentOptions DocOpts = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly JsonWriterOptions Out = new() { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static bool WantsCarry(string text)
    {
        if (!text.Contains("\"carry\"")) return false;
        try { return JsonNode.Parse(text, null, DocOpts)?["carry"]?.GetValue<bool>() == true; }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return false; }
    }

    /// <summary>이월 소총병 (이 미션이 쓸 병력): 소총병만, 계급 높은 순 → 이전 중대 id → 사살 → id.</summary>
    public static List<MissionResults.UnitLine> Infantry(CampaignState? s, string missionId) =>
        (s?.CarryFor(missionId).Where(u => u.OfficerRank == null && u.Unit == "infantry") ?? Enumerable.Empty<MissionResults.UnitLine>())
            .OrderByDescending(u => u.Rank).ThenBy(u => u.Force ?? "", StringComparer.Ordinal).ThenByDescending(u => u.Kills).ThenBy(u => u.Id).ToList();

    public static List<MissionResults.UnitLine> Officers(CampaignState? s, string missionId) =>
        (s?.CarryFor(missionId).Where(u => u.OfficerRank != null && u.Post == "company") ?? Enumerable.Empty<MissionResults.UnitLine>())
            .OrderByDescending(u => u.OfficerRank).ThenByDescending(u => u.Xp ?? 0).ThenBy(u => u.Id).ToList();

    /// <summary>carry 상태 (없으면 그대로 = 전부 보충병) 를 써 넣은 새 텍스트. 병력 b1 이 없으면 원문.</summary>
    public static string Patch(string text, string missionId, CampaignState? s)
    {
        if (s == null) return text;
        var root = JsonNode.Parse(text, null, DocOpts);
        var force = (root?["forces"] as JsonArray)?.FirstOrDefault(f => f?["id"]?.GetValue<string>() == ForceId);
        if (force?["companies"] is not JsonArray comps) return text;
        var vets = Infantry(s, missionId);
        var offs = Officers(s, missionId);
        // 같은 중대 출신이 한 중대에 모이도록 이전 중대 id 순으로 정렬 뒤 정원만큼 자른다 (계급 높은 순으로 자른 선발 → 중대 순 배치)
        int cap = comps.Sum(c => c?["units"]?["infantry"]?.GetValue<int>() ?? 0);
        var picked = vets.Take(cap).OrderBy(u => u.Force ?? "", StringComparer.Ordinal).ThenByDescending(u => u.Rank).ThenBy(u => u.Id).ToList();
        int at = 0, oi = 0;
        foreach (var c in comps)
        {
            if (c == null) continue;
            int n = c["units"]?["infantry"]?.GetValue<int>() ?? 0;
            var ranks = new JsonArray();
            for (int i = 0; i < n && at < picked.Count; i++, at++) ranks.Add(picked[at].Rank);
            if (ranks.Count > 0) c["infantryRanks"] = ranks;
            if (oi < offs.Count && (c["units"]?["officer"]?.GetValue<int>() ?? 0) > 0)
            {
                c["officerRanks"] = new JsonArray(Math.Clamp(offs[oi].OfficerRank ?? 1, 1, 6));
                c["officerXp"] = Math.Max(0, offs[oi].Xp ?? 0);
                oi++;
            }
        }
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, Out)) root!.WriteTo(w);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
