using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>C1E1-d1 노드 데이터 JSON 로드 검증 (null · 범위 · 참조 무결성 · g1 그래프 일치). SaveCompatSelfTest 에서 부른다.</summary>
public static class C1e1NodeDataSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "data", "episodes");
        var dd = CampaignDecisions.Current;
        var nd = EpisodeNodeData.Load(Path.Combine(dir, "c1e1_nodes.json"));
        var errs = nd.Validate(dd, CampaignVars.Has);
        check(errs.Count == 0 && nd.Episode == "c1e1" && nd.Nodes.Count == 7, $"C1E1-d1 노드 데이터: 로드 · 검증 통과 (노드 {nd.Nodes.Count}) {string.Join(";", errs)}");

        // 결정 16개가 정확히 한 노드에 배치, 배치 노드가 결정의 where 와 맞음
        var placedAt = nd.Placements.SelectMany(kv => kv.Value.Select(d => (d, node: kv.Key))).ToDictionary(x => x.d, x => x.node);
        bool where = dd.Decisions.All(d => placedAt.TryGetValue(d.Id, out var n) && (d.Where.StartsWith(n) || (n.StartsWith("SEL") && d.Where == "SELECT") || (n == "D14" && d.Where == "종료")));
        check(placedAt.Count == 16 && where, "C1E1-d1 결정 배치: 16개 전부 한 번씩, 노드가 결정 where 와 일치");

        // EV3: 결정 없음 + 시위대 파견 · 남 참장 편지 플래그
        var ev3 = nd.Find("EV3");
        check(ev3 != null && !nd.Placements.ContainsKey("EV3") && ev3.SetOnEnter.Count == 2 && ev3.SetOnEnter.Any(s => s.Var == "flag.siwidaeDispatched" && s.Value == 1) && ev3.SetOnEnter.Any(s => s.Var == "flag.namLetter" && s.Value == 1)
            && ev3.SetOnEnter.All(s => CampaignVars.Defs.First(v => v.Key == s.Var) is { } v && s.Value >= v.Min && s.Value <= v.Max), "C1E1-d1 EV3: 결정 없음, flag.siwidaeDispatched / flag.namLetter 진입 시 1 (범위 안)");
        check(nd.Texts.Values.All(t => t == "[층3]") && nd.Texts.Count == 14, "C1E1-d1 문구: 전부 [층3] 자리표시 (서사 문구 없음)");

        // g1 그래프가 있으면 (g1 병합 후) 노드 id · 종류 · 결정 위치 일치
        string gpath = Path.Combine(dir, "c1e1.json");
        if (File.Exists(gpath))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(gpath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var gnodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToDictionary(n => n.GetProperty("id").GetString()!, n => n);
            bool ok = nd.Nodes.All(n => gnodes.TryGetValue(n.Id, out var g) && g.GetProperty("kind").GetString() == n.Kind);
            foreach (var (id, g) in gnodes)
                if (g.TryGetProperty("decisions", out var ds))
                {
                    var gl = ds.EnumerateArray().Select(x => x.GetString()!).ToList();
                    // 그래프 노드의 결정 (D14 노드 포함)은 배치의 부분집합이어야 (D08 처럼 그래프가 빠뜨린 것은 d1 이 채운다)
                    ok &= nd.Placements.TryGetValue(id, out var pl) && gl.All(pl.Contains);
                }
            check(ok, "C1E1-d1 g1 그래프 일치: 노드 id/kind · 그래프의 결정 위치가 배치와 맞음");
        }

        // 거부: 잘못된 파일 (중복 배치 · 모르는 결정/변수/문구 키 · null · 빈 id · 모르는 kind · 없는 파일)
        string tmp = Path.Combine(Path.GetTempPath(), "kd_d1_nodes_" + Environment.ProcessId + ".json");
        try
        {
            File.WriteAllText(tmp, "{\"episode\":\"x\",\"placements\":{\"EV1\":[\"D01\",\"D01\"],\"CH1\":[\"D01\",\"D99\"],\"CH2\":[\"\"],\"BT1\":null},\"nodes\":[null,{\"id\":\"\",\"kind\":\"EV\"},{\"id\":\"A\",\"kind\":\"BT\"},{\"id\":\"B\",\"kind\":\"EV\",\"titleKey\":\"nope\",\"setOnEnter\":[{\"var\":\"zzz\",\"value\":1}]},{\"id\":\"B\",\"kind\":\"EV\"}],\"texts\":{\"k\":null}}");
            var bad = EpisodeNodeData.Load(tmp);
            var be = bad.Validate(dd, CampaignVars.Has);
            check(be.Count >= 8 && be.Any(x => x.Contains("D99")) && be.Any(x => x.Contains("두 곳")) && be.Any(x => x.Contains("zzz")) && be.Any(x => x.Contains("문구 키")) && be.Any(x => x.Contains("중복")) && be.Any(x => x.Contains("EV/CH/IL")) && be.Any(x => x.Contains("배치되지 않음")),
                $"C1E1-d1 검증 거부: 중복 배치 · 모르는 결정/변수/문구 키 · null · 빈 id · 모르는 kind · 미배치 결정 ({be.Count}건) {string.Join("/", be.Take(6))}");
            File.WriteAllText(tmp, "{ not json");
            check(EpisodeNodeData.Load(tmp).Errors.Count == 1 && EpisodeNodeData.Load(tmp + ".none").Errors.Count == 1, "C1E1-d1 로드: 깨진 JSON / 없는 파일은 오류 1건 (예외 없음)");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }
}
