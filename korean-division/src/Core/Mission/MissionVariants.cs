using System.Text.Json;
using System.Text.Json.Nodes;

namespace KoreanDivision.Core;

/// <summary>
/// [TC-F] 미션 데이터 변형. 미션 JSON 의 "variants" 절 (조건 + 조작 목록) 을 캠페인 상태로 평가해 미션 텍스트를 미리 고친다 (MissionDef.Load, 캠페인 이월과 같은 자리).
/// 시뮬 상태가 아니라 '적용이 끝난 미션 텍스트' 가 스냅샷 · 리플레이에 실린다 — 시뮬 · 저장 형식 변화 없음. 캠페인 상태가 없거나 조건이 안 맞으면 텍스트는 그대로 (variants 절만 지워짐).
/// 조건(when, 전부 만족): {var, op(>= 기본), value} (var = campaign_vars 키 또는 "negotiation") · {decision, has:"B", outcome?:"ok"|"fail"} (그 결정의 최근 응답에 선택지 포함).
/// 조작(ops): removeUnits {faction, units[우선순위], count: 정수 | {decision, choice, per, each, max}, forces?[id]} · scaleUnits {faction, units[], percent, scope?: forces|reinforcements|all}
///   · removeForces {ids[]} · addDeployZone {faction, rect|circle} · removeProps {area}. 비는 병력은 지우고 그 증원을 부르는 사건 줄도 지운다.
/// 정수만 (반올림 = 반올림 올림), 목록 순서대로 — 결정적. 모르는 조작 · 잘못된 값은 InvalidDataException (미션 데이터 오류로 즉시 드러냄).
/// </summary>
public static class MissionVariants
{
    static readonly JsonDocumentOptions DocOpts = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly JsonSerializerOptions Out = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static bool Wants(string text) => text.Contains("\"variants\"", StringComparison.Ordinal);

    /// <summary>변형 적용. <paramref name="s"/> 가 null 이면 어떤 변형도 적용하지 않는다 (조건 불성립).</summary>
    public static string Patch(string text, CampaignState? s) => Patch(text, s, out _);

    public static string Patch(string text, CampaignState? s, out List<string> applied)
    {
        applied = new List<string>();
        if (!Wants(text)) return text;
        JsonNode? root;
        try { root = JsonNode.Parse(text, null, DocOpts); }
        catch (JsonException e) { throw new InvalidDataException("미션 JSON 오류: " + e.Message, e); }
        if (root is not JsonObject obj || obj["variants"] is not JsonArray variants) return text;
        var seen = new HashSet<string>();
        foreach (var vn in variants)
        {
            if (vn is not JsonObject v) throw new InvalidDataException("variants 항목은 객체");
            string id = Str(v, "id");
            if (id.Length == 0 || !seen.Add(id)) throw new InvalidDataException($"variant id '{id}' 가 비었거나 중복");
            var when = v["when"] as JsonArray ?? new JsonArray();
            var ops = v["ops"] as JsonArray ?? throw new InvalidDataException($"variant '{id}': ops 배열 필요");
            foreach (var c in when) ValidateCond(id, c);
            bool ok = s != null && when.All(c => Cond(s, (JsonObject)c!));
            if (!ok) { foreach (var o in ops) ValidateOp(id, o, obj); continue; }   // 안 쓰는 변형도 형식은 검사
            foreach (var o in ops) ApplyOp(id, (JsonObject)(o ?? throw new InvalidDataException($"variant '{id}': 조작이 null")), obj, s!);
            applied.Add(id);
        }
        obj.Remove("variants");
        if (applied.Count > 0) obj["variantsApplied"] = new JsonArray(applied.Select(a => (JsonNode)JsonValue.Create(a)!).ToArray());
        return obj.ToJsonString(Out);
    }

    // ---------------------------------------------------------------- 조건

    static void ValidateCond(string vid, JsonNode? c)
    {
        if (c is not JsonObject o) throw new InvalidDataException($"variant '{vid}': 조건은 객체");
        bool isVar = o["var"] != null, isDec = o["decision"] != null;
        if (isVar == isDec) throw new InvalidDataException($"variant '{vid}': 조건은 var 또는 decision 중 하나");
        if (isVar)
        {
            string k = Str(o, "var");
            if (k != "negotiation" && !CampaignVars.Has(k)) throw new InvalidDataException($"variant '{vid}': 모르는 변수 '{k}'");
            string op = o["op"] != null ? Str(o, "op") : ">=";
            if (Array.IndexOf(new[] { ">=", "<=", "==", "!=", ">", "<" }, op) < 0) throw new InvalidDataException($"variant '{vid}': 연산자 '{op}'");
        }
        else if (Str(o, "has").Length == 0) throw new InvalidDataException($"variant '{vid}': decision 조건에 has 필요");
    }

    static bool Cond(CampaignState s, JsonObject c)
    {
        if (c["var"] != null)
        {
            string k = Str(c, "var");
            int have = k == "negotiation" ? CampaignDecisions.Current.Negotiation(s) : CampaignVars.Get(s, k);
            int want = Int(c, "value", 0);
            return (c["op"] != null ? Str(c, "op") : ">=") switch { ">=" => have >= want, "<=" => have <= want, "==" => have == want, "!=" => have != want, ">" => have > want, "<" => have < want, _ => false };
        }
        var a = s.Decisions.Answers.LastOrDefault(x => x.Id == Str(c, "decision"));
        if (a == null || !a.Choices.Contains(Str(c, "has"))) return false;
        return c["outcome"] == null || a.Outcome == Str(c, "outcome");
    }

    // ---------------------------------------------------------------- 조작

    static void ValidateOp(string vid, JsonNode? n, JsonObject root)
    {
        if (n is not JsonObject o) throw new InvalidDataException($"variant '{vid}': 조작은 객체");
        switch (Str(o, "op"))
        {
            case "removeUnits": Faction(vid, o, root); NeedUnits(vid, o); if (o["count"] == null) throw new InvalidDataException($"variant '{vid}': removeUnits 에 count 필요"); break;
            case "scaleUnits": Faction(vid, o, root); NeedUnits(vid, o); if (Int(o, "percent", -1) is < 0 or > 1000) throw new InvalidDataException($"variant '{vid}': percent 0~1000"); break;
            case "removeForces": if (o["ids"] is not JsonArray) throw new InvalidDataException($"variant '{vid}': removeForces 에 ids 필요"); break;
            case "addDeployZone": Faction(vid, o, root); if (o["rect"] == null && o["circle"] == null) throw new InvalidDataException($"variant '{vid}': addDeployZone 에 rect 또는 circle"); break;
            case "removeProps": if (AreaOf(root, Str(o, "area")) == null) throw new InvalidDataException($"variant '{vid}': 구역 '{Str(o, "area")}' 없음"); break;
            default: throw new InvalidDataException($"variant '{vid}': 모르는 조작 '{Str(o, "op")}'");
        }
    }

    static void Faction(string vid, JsonObject o, JsonObject root)
    {
        int f = Int(o, "faction", 0), n = (root["factions"] as JsonArray)?.Count ?? 0;
        if (f < 1 || f > n) throw new InvalidDataException($"variant '{vid}': 세력 {f} 이 범위 밖");
    }

    static void NeedUnits(string vid, JsonObject o)
    {
        if (o["units"] is not JsonArray { Count: > 0 }) throw new InvalidDataException($"variant '{vid}': units 목록 필요");
    }

    static void ApplyOp(string vid, JsonObject o, JsonObject root, CampaignState s)
    {
        ValidateOp(vid, o, root);
        switch (Str(o, "op"))
        {
            case "removeUnits":
            {
                int left = Count(vid, o["count"]!, s);
                var only = (o["forces"] as JsonArray)?.Select(x => x?.GetValue<string>() ?? "").ToHashSet();
                foreach (var key in Keys(o))
                    foreach (var f in Lists(root, "forces"))
                    {
                        if (left <= 0) break;
                        if (Int(f, "faction", 1) != Int(o, "faction", 0) || (only != null && !only.Contains(Str(f, "id")))) continue;
                        foreach (var holder in Holders(f))
                        {
                            if (left <= 0) break;
                            if (holder["units"] is not JsonObject u || u[key] == null) continue;
                            int have = u[key]!.GetValue<int>(), take = Math.Min(have, left);
                            left -= take;
                            if (have - take > 0) u[key] = have - take; else u.Remove(key);
                        }
                    }
                Cleanup(root);
                break;
            }
            case "scaleUnits":
            {
                string scope = o["scope"] != null ? Str(o, "scope") : "all";
                if (scope is not ("forces" or "reinforcements" or "all")) throw new InvalidDataException($"variant '{vid}': scope '{scope}'");
                int pct = Int(o, "percent", 100);
                var keys = Keys(o).ToHashSet();
                foreach (var listName in new[] { "forces", "reinforcements" })
                {
                    if (scope != "all" && scope != listName) continue;
                    foreach (var f in Lists(root, listName))
                    {
                        if (Int(f, "faction", 1) != Int(o, "faction", 0)) continue;
                        foreach (var holder in Holders(f))
                        {
                            if (holder["units"] is not JsonObject u) continue;
                            foreach (var k in u.Select(p => p.Key).ToList())
                            {
                                if (!keys.Contains(k)) continue;
                                int n = (int)(((long)u[k]!.GetValue<int>() * pct + 50) / 100);
                                if (n > 0) u[k] = n; else u.Remove(k);
                            }
                        }
                    }
                }
                Cleanup(root);
                break;
            }
            case "removeForces":
            {
                foreach (var idn in (JsonArray)o["ids"]!) RemoveForce(root, idn?.GetValue<string>() ?? "");
                break;
            }
            case "addDeployZone":
            {
                var zones = root["deployZones"] as JsonArray ?? new JsonArray();
                var z = new JsonObject { ["faction"] = Int(o, "faction", 1) };
                if (o["rect"] != null) z["rect"] = o["rect"]!.DeepClone(); else z["circle"] = o["circle"]!.DeepClone();
                zones.Add(z);
                root["deployZones"] = zones;
                break;
            }
            case "removeProps":
            {
                var area = AreaOf(root, Str(o, "area"))!;
                if (root["props"] is JsonArray props)
                    for (int i = props.Count - 1; i >= 0; i--)
                        if (props[i] is JsonObject p && InArea(area, (float)(p["x"]?.GetValue<double>() ?? 0), (float)(p["y"]?.GetValue<double>() ?? 0))) props.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>count: 정수 · 또는 {decision, choice, per, each, max} = 그 결정 최근 응답(그 선택지 포함)의 Quantity / per * each, 최대 max (블록 단위, 내림).</summary>
    static int Count(string vid, JsonNode n, CampaignState s)
    {
        if (n is JsonValue) return Math.Max(0, n.GetValue<int>());
        if (n is not JsonObject c) throw new InvalidDataException($"variant '{vid}': count 형식");
        int per = Int(c, "per", 0), each = Int(c, "each", 0), max = Int(c, "max", int.MaxValue);
        if (per <= 0 || each < 0 || max < 0) throw new InvalidDataException($"variant '{vid}': count per>0 each>=0 max>=0");
        var a = s.Decisions.Answers.LastOrDefault(x => x.Id == Str(c, "decision"));
        if (a == null || !a.Choices.Contains(Str(c, "choice"))) return 0;
        return (int)Math.Min(max, (long)(a.Quantity / per) * each);
    }

    // ---------------------------------------------------------------- JSON 도우미

    static string Str(JsonObject o, string k) { try { return o[k]?.GetValue<string>() ?? ""; } catch (InvalidOperationException) { return ""; } }
    static int Int(JsonObject o, string k, int d) { try { return o[k] == null ? d : o[k]!.GetValue<int>(); } catch (Exception e) when (e is InvalidOperationException or FormatException) { return d; } }
    static IEnumerable<string> Keys(JsonObject o) => ((JsonArray)o["units"]!).Select(x => x?.GetValue<string>() ?? "");
    static IEnumerable<JsonObject> Lists(JsonObject root, string name) => root[name] is JsonArray a ? a.OfType<JsonObject>() : Enumerable.Empty<JsonObject>();

    /// <summary>병력 하나의 유닛 표 보유자: 병력 자신 (units) + 각 중대.</summary>
    static IEnumerable<JsonObject> Holders(JsonObject force)
    {
        yield return force;
        if (force["companies"] is JsonArray cs) foreach (var c in cs.OfType<JsonObject>()) yield return c;
    }

    /// <summary>유닛이 0 이 된 중대 · 병력을 지우고, 지운 병력을 부르는 사건 줄도 지운다.</summary>
    static void Cleanup(JsonObject root)
    {
        foreach (var listName in new[] { "forces", "reinforcements" })
        {
            if (root[listName] is not JsonArray list) continue;
            foreach (var f in list.OfType<JsonObject>().ToList())
            {
                if (f["units"] is JsonObject fu && fu.Count == 0) f.Remove("units");
                if (f["companies"] is JsonArray cs)
                {
                    for (int i = cs.Count - 1; i >= 0; i--)
                        if (cs[i] is JsonObject c && (c["units"] is not JsonObject cu || cu.Count == 0)) cs.RemoveAt(i);
                    if (cs.Count == 0) f.Remove("companies");
                }
                if (f["units"] == null && f["companies"] == null) RemoveForce(root, Str(f, "id"));
            }
        }
    }

    static void RemoveForce(JsonObject root, string id)
    {
        foreach (var listName in new[] { "forces", "reinforcements" })
            if (root[listName] is JsonArray list)
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i] is JsonObject f && Str(f, "id") == id) list.RemoveAt(i);
        if (root["events"] is JsonArray evs)
            foreach (var e in evs.OfType<JsonObject>())
                if (e["do"] is JsonArray acts)
                    for (int i = acts.Count - 1; i >= 0; i--)
                        if (acts[i] is JsonObject a && Str(a, "spawn") == id) { if (a.Count == 1) acts.RemoveAt(i); else a.Remove("spawn"); }
    }

    static JsonObject? AreaOf(JsonObject root, string name) => root["areas"] is JsonObject areas && areas[name] is JsonObject a ? a : null;

    static bool InArea(JsonObject a, float x, float y)
    {
        if (a["rect"] is JsonArray { Count: 4 } r) { float rx = (float)r[0]!.GetValue<double>(), ry = (float)r[1]!.GetValue<double>(), rw = (float)r[2]!.GetValue<double>(), rh = (float)r[3]!.GetValue<double>(); return x >= rx && x < rx + rw && y >= ry && y < ry + rh; }
        if (a["circle"] is JsonArray { Count: 3 } c) { float dx = x - (float)c[0]!.GetValue<double>(), dy = y - (float)c[1]!.GetValue<double>(), rr = (float)c[2]!.GetValue<double>(); return dx * dx + dy * dy <= rr * rr; }
        return false;
    }
}
