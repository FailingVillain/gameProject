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

    public static bool WantsPending(string missionId, CampaignState? s) =>
        s?.Pending != null && s.Pending.MissionId == missionId && s.Pending.DeployedCompanyIds.Count > 0;

    /// <summary>이월 소총병 (이 미션이 쓸 병력): 소총병만, 계급 높은 순 → 이전 중대 id → 사살 → id.</summary>
    public static List<MissionResults.UnitLine> Infantry(CampaignState? s, string missionId) =>
        (s?.CarryFor(missionId).Where(u => u.OfficerRank == null && u.Unit == "infantry") ?? Enumerable.Empty<MissionResults.UnitLine>())
            .OrderByDescending(u => u.Rank).ThenBy(u => u.Force ?? "", StringComparer.Ordinal).ThenByDescending(u => u.Kills).ThenBy(u => u.Id).ToList();

    public static List<MissionResults.UnitLine> Officers(CampaignState? s, string missionId) =>
        (s?.CarryFor(missionId).Where(u => u.OfficerRank != null && u.Post == "company") ?? Enumerable.Empty<MissionResults.UnitLine>())
            .OrderByDescending(u => u.OfficerRank).ThenByDescending(u => u.Xp ?? 0).ThenBy(u => u.Id).ToList();

    /// <summary>carry 상태 및 출격 편성(Pending)을 써 넣은 새 텍스트.</summary>
    public static string Patch(string text, string missionId, CampaignState? s)
    {
        if (s == null) return text;
        bool wantCarry = WantsCarry(text);
        bool wantPending = WantsPending(missionId, s);
        if (!wantCarry && !wantPending) return text;

        var root = JsonNode.Parse(text, null, DocOpts);
        if (root == null) return text;

        var forces = root["forces"] as JsonArray;
        var force = forces?.FirstOrDefault(f => f?["id"]?.GetValue<string>() == ForceId);

        if (wantPending && s.Pending != null)
        {
            // b1 대대의 companies 배열이 있을 때, 선택된 보병 중대(b1_c1, b1_c2, b1_c3 등) 수만큼 companies 개수를 조절(단 최소 1개 중대는 유지하여 미션 검증이 깨지지 않게 함)
            if (force?["companies"] is JsonArray comps)
            {
                int deployedInf = s.Pending.DeployedCompanyIds.Count(id => id.StartsWith("b1_c") || (s.Roster != null && s.Roster.Any(r => r.Id == id && r.Kind == "infantry")));
                int target = Math.Max(1, deployedInf);
                while (comps.Count > target && comps.Count > 1)
                {
                    comps.RemoveAt(comps.Count - 1);
                }
            }

            // s.Pending.AmmoScale < 1f일 때 플레이어 세력(faction == 1)의 모든 병력(forces) 및 소속 companies JSON 노드에 "ammoScale": Math.Round(s.Pending.AmmoScale, 3)을 기록.
            if (s.Pending.AmmoScale < 1f)
            {
                double scale = Math.Round((double)s.Pending.AmmoScale, 3);
                int playerFaction = root["player"]?.GetValue<int>() ?? 1;
                void ApplyAmmoScale(JsonArray? list)
                {
                    if (list == null) return;
                    foreach (var f in list)
                    {
                        if (f == null) continue;
                        int faction = f["faction"]?.GetValue<int>() ?? 1;
                        if (faction == playerFaction)
                        {
                            f["ammoScale"] = scale;
                            if (f["companies"] is JsonArray cList)
                            {
                                foreach (var c in cList)
                                {
                                    if (c != null) c["ammoScale"] = scale;
                                }
                            }
                        }
                    }
                }
                ApplyAmmoScale(forces);
                ApplyAmmoScale(root["reinforcements"] as JsonArray);
            }

            // [T19b Q6] 포탄 종류별 비율 → 무기 키별 (플레이어 포에만 적용, 중대 ammoScale 보다 우선). 소총탄만 모자라도 포는 포탄 비율 (가득이면 1) 을 쓴다.
            // 포 없는 미션은 ShellScale 이 비어 있어 아무것도 쓰지 않는다.
            if (s.Pending.ShellScale.Count > 0 && (s.Pending.AmmoScale < 1f || s.Pending.ShellScale.Values.Any(v => v < 1f)))
            {
                var byWeapon = new JsonObject();
                foreach (var (wk, shell) in CampaignEconomyDefs.Current.ShellWeapons.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    if (s.Pending.ShellScale.TryGetValue(shell, out float sc)) byWeapon[wk] = Math.Round((double)sc, 3);
                if (byWeapon.Count > 0) root["ammoScaleByWeapon"] = byWeapon;
            }
        }

        if (wantCarry && force?["companies"] is JsonArray compsForCarry)
        {
            var vets = Infantry(s, missionId);
            var offs = Officers(s, missionId);
            // 같은 중대 출신이 한 중대에 모이도록 이전 중대 id 순으로 정렬 뒤 정원만큼 자른다 (계급 높은 순으로 자른 선발 → 중대 순 배치)
            int cap = compsForCarry.Sum(c => c?["units"]?["infantry"]?.GetValue<int>() ?? 0);
            var picked = vets.Take(cap).OrderBy(u => u.Force ?? "", StringComparer.Ordinal).ThenByDescending(u => u.Rank).ThenBy(u => u.Id).ToList();
            int at = 0, oi = 0;
            foreach (var c in compsForCarry)
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
        }

        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, Out)) root.WriteTo(w);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
