using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>C1E1-g4 목표 확장: surrender (항복 비율) · nOfM (N of M 조합 / 거점 N점 점령 / 'A 또는 B') · losses% 덮어쓰기 (maxCasualtyFrac) · 세력별 항복 키 (기본 꺼짐).
/// 각 새 목표: 달성 / 미달 / 저장 왕복. 기존 4종 동작 불변은 미션 시험이 지킨다. KD_SELFTEST_ONLY=objectives</summary>
public static class ObjectivesSelfTest
{
    // 100×48 풀밭. 세력 1 = 플레이어(아군), 2 = 적 (둘 다 hold). 구역 c1~c3 = 아군 쪽, 적은 (96,45) 에 보병 1.
    static string Json(string objectives, string failures = "", string fac2Extra = "", int allies = 1) => $$"""
    { "id": "t_obj", "title": "T", "player": 1, "seed": 7,
      "factions": [ { "name": "A", "team": 1, "posture": "hold" }, { "name": "B", "team": 2, "posture": "hold"{{fac2Extra}} } ],
      "mapData": { "name": "obj", "width": 100, "height": 48, "base": "grass", "starts": [ [5, 45], [95, 45] ] },
      "areas": { "c1": { "rect": [10, 10, 8, 8] }, "c2": { "rect": [30, 10, 8, 8] }, "c3": { "rect": [50, 10, 8, 8] } },
      "forces": [ { "id": "a", "faction": 1, "at": [4, 45], "units": { "infantry": {{allies}} }, "company": false },
                  { "id": "b", "faction": 2, "at": [96, 45], "units": { "infantry": 1 }, "company": false } ],
      "failures": [ {{failures}} ],
      "objectives": [ {{objectives}} ] }
    """;

    const string Caps = """
        { "id": "c1", "type": "capture", "area": "c1", "seconds": 1, "primary": false },
        { "id": "c2", "type": "capture", "area": "c2", "seconds": 1, "primary": false },
        { "id": "c3", "type": "capture", "area": "c3", "seconds": 1, "primary": false },
    """;

    static World Make(string json) => World.CreateMissionWorld(MissionDef.Parse(json, "t_obj"), Defs.Current);
    static void Steps(World w, int n) { for (int i = 0; i < n && w.Winner == 0; i++) w.Step(); }
    static Unit Put(World w, int owner, float x, float y) { var u = w.SpawnUnit(Defs.Current.Units["infantry"], owner, new Vector2(x, y)); u.Hp = 1e6f; return u; }

    static bool Throws(string json, string want)
    {
        try { MissionDef.Parse(json, "t"); return false; }
        catch (InvalidDataException e) { return e.Message.Contains(want); }
    }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;

        // ---- 1. 검증
        {
            string n3(string extra) => Json(Caps + $$"""{ "id": "n", "type": "nOfM", {{extra}} }""");
            int ok = 0; var miss = new List<string>();
            foreach (var (js, want) in new[]
            {
                (n3(""), "of"), (n3("\"of\": []"), "of"), (n3("\"of\": [\"zz\"]"), "zz"), (n3("\"of\": [\"c1\", \"c1\"]"), "중복"),
                (n3("\"of\": [\"c1\", \"n\"]"), "앞에 정의"), (n3("\"of\": [\"c1\", \"c2\"], \"count\": 3"), "count 3"), (n3("\"of\": [\"c1\"], \"count\": 0"), "count 0"),
                (Json("""{ "id": "s", "type": "surrender", "percent": 0 }"""), "percent"), (Json("""{ "id": "s", "type": "surrender", "percent": 101 }"""), "percent"),
                (Json("""{ "id": "s", "type": "surrender", "faction": 9 }"""), "세력"),
                (Json("""{ "id": "e", "type": "eliminate" }""", fac2Extra: ", \"surrenderChance\": 2"), "surrender"),
            })
            {
                if (Throws(js, want)) ok++; else miss.Add(want);
            }
            check(ok == 11, $"새 목표 검증 {ok}/11 오류 메시지 일치{(miss.Count > 0 ? ": " + string.Join(" | ", miss) : "")}");
            check(!Throws(n3("\"of\": [\"c1\", \"c2\", \"c3\"], \"count\": 2"), "x"), "정상 nOfM 은 통과");
        }

        // ---- 2. nOfM (거점 N점 점령): 3 구역 중 2 점령 → 달성 · 승리 / 1 점령 → 미달 / 하위 둘 실패 → 실패
        const string N2 = """{ "id": "n2", "type": "nOfM", "of": ["c1", "c2", "c3"], "count": 2 }""";
        {
            var w = Make(Json(Caps + N2));
            var m = w.Mission!;
            Put(w, 1, 14, 14); Put(w, 1, 34, 14);
            Steps(w, 60 * 6);
            check(m.ObjState[0] == MissionState.Done && m.ObjState[1] == MissionState.Done && m.ObjState[2] == MissionState.Active && m.ObjState[3] == MissionState.Done
                  && m.Result == MissionState.Won && m.ObjProgress[3] == 2, $"nOfM 2/3: 두 구역 점령 → 달성 · 승리 (진행 {m.ObjProgress[3]})");

            var w1 = Make(Json(Caps + N2));
            Put(w1, 1, 14, 14);
            Steps(w1, 60 * 8);
            check(w1.Mission!.ObjState[0] == MissionState.Done && w1.Mission.ObjState[3] == MissionState.Active && w1.Mission.ObjProgress[3] == 1 && w1.Mission.Result == MissionState.Running,
                  "nOfM 2/3: 한 구역만 점령 → 미달 (진행 1, 계속)");

            var w2 = Make(Json(Caps + N2));
            Steps(w2, 5);
            w2.Mission!.ObjState[1] = MissionState.Failed; w2.Mission.ObjState[2] = MissionState.Failed;
            Steps(w2, 3);
            check(w2.Mission.ObjState[3] == MissionState.Failed && w2.Mission.Result == MissionState.Lost, "nOfM: 달성 + 진행 중 < N 이면 실패 → 패배");
        }

        // ---- 3. 'A 또는 B' (OR) + surrender: 구역 점령 또는 적 항복 (nOfM count 1)
        {
            const string Or = """
                { "id": "cap", "type": "capture", "area": "c1", "seconds": 1, "primary": false },
                { "id": "sur", "type": "surrender", "faction": 2, "percent": 50, "primary": false },
                { "id": "any", "type": "nOfM", "of": ["cap", "sur"], "count": 1 }
            """;
            var w = Make(Json(Or));
            Steps(w, 30);
            check(w.Mission!.ObjState[2] == MissionState.Active && w.Mission.ObjProgress[1] == 0, "OR: 항복도 점령도 없으면 계속 (적 항복 비율 0)");
            foreach (var u in w.Units) if (u.Owner == 2) u.Surrendered = true;
            Steps(w, 3);
            check(w.Mission.ObjState[1] == MissionState.Done && w.Mission.ObjState[0] == MissionState.Active && w.Mission.ObjState[2] == MissionState.Done && w.Mission.Result == MissionState.Won,
                  "OR: 적 항복 (100% ≥ 50%) → surrender 달성 → nOfM(1) 달성 · 승리 (점령 없이)");
        }

        // ---- 4. surrender 단독: 비율 문턱 · 최소 항복자 수 · 대상 세력 · 저장 왕복
        {
            string Sur(string extra) => Json($$"""{ "id": "s", "type": "surrender", {{extra}} }""");
            World Foes(string extra, int surrendered)
            {
                var w = Make(Sur(extra));
                var foes = new List<Unit> { w.Units.First(u => u.Owner == 2) };
                for (int i = 0; i < 3; i++) foes.Add(Put(w, 2, 90 + i, 40));   // 적 4
                for (int i = 0; i < surrendered; i++) foes[i].Surrendered = true;
                Steps(w, 3);
                return w;
            }
            var a = Foes("\"percent\": 70", 3);
            check(a.Mission!.ObjState[0] == MissionState.Done && a.Mission.Result == MissionState.Won && MathF.Abs(a.Mission.ObjProgress[0] - 75f) < 1e-4f, $"surrender 70%: 4명 중 3명 항복 (75%) → 달성 · 승리 ({a.Mission.ObjProgress[0]:0}%)");
            var b = Foes("\"percent\": 80", 3);
            check(b.Mission!.ObjState[0] == MissionState.Active && b.Mission.Result == MissionState.Running, "surrender 80%: 75% 는 미달 (계속)");
            var c = Foes("\"percent\": 10, \"count\": 4", 3);
            check(c.Mission!.ObjState[0] == MissionState.Active, "surrender count 4: 항복자 3명 < 최소 4명 → 미달 (비율은 충분)");
            var d = Foes("\"percent\": 10", 0);
            check(d.Mission!.ObjState[0] == MissionState.Active && d.Mission.ObjProgress[0] == 0, "surrender: 항복자 0 → 미달");
            // 대상 세력 지정: faction 1(아군) 지정 + 적 항복 → 영향 없음. 기본(생략) = 플레이어의 적
            var f = Make(Sur("\"percent\": 50, \"faction\": 1"));
            foreach (var u in f.Units) if (u.Owner == 2) u.Surrendered = true;
            Steps(f, 3);
            check(f.Mission!.ObjState[0] == MissionState.Active, "surrender faction 1: 적이 항복해도 지정 세력(아군) 비율만 봄 → 미달");
            var g = Make(Sur("\"percent\": 50"));
            foreach (var u in g.Units) if (u.Owner == 2) u.Surrendered = true;
            Steps(g, 3);
            check(g.Mission!.ObjState[0] == MissionState.Done, "surrender faction 생략: 플레이어의 적 전체를 대상으로 달성");

            // 저장 왕복 (진행 중 nOfM + surrender)
            var w = Make(Json(Caps + N2 + """, { "id": "s", "type": "surrender", "percent": 50, "primary": false }"""));
            Put(w, 1, 14, 14);
            Steps(w, 60 * 3);
            var meta = new SnapshotMeta("obj", 1, false, "none");
            var bytes = Snapshot.Save(w, meta);
            var l = Snapshot.Load(bytes, defs).World;
            var again = Snapshot.Save(l, meta);
            check(l.Mission!.Def.Objectives[3].Of!.Count == 3 && l.Mission.Def.Objectives[3].OfIdx.Length == 3 && l.Mission.ObjProgress[3] == w.Mission!.ObjProgress[3] && l.ComputeChecksum() == w.ComputeChecksum(),
                  $"저장 왕복: nOfM 정의(of) · 진행 {l.Mission.ObjProgress[3]} 복원, 체크섬 같음");
            check(Snapshot.RawBody(bytes).AsSpan().SequenceEqual(Snapshot.RawBody(again)), "저장 → 불러오기 → 저장 본문 바이트 동일 (새 목표)");
            Put(w, 1, 34, 14); Put(l, 1, 34, 14);
            bool same = true;
            for (int i = 0; i < 360; i++) { w.Step(); l.Step(); if (i % 60 == 0) same &= w.ComputeChecksum() == l.ComputeChecksum(); }
            check(same && w.Mission.ObjState[3] == MissionState.Done && l.Mission.ObjState[3] == MissionState.Done && l.Winner == w.Winner,
                  "불러온 월드 이어 돌림: 체크섬 동일 · 같은 틱에 nOfM 달성");
        }

        // ---- 5. losses% 덮어쓰기 (maxCasualtyFrac)
        {
            const string Cap = """{ "id": "c1", "type": "capture", "area": "c1", "seconds": 1 }""";
            const string L80 = """{ "type": "losses", "percent": 80 }""";
            string baseJson = Json(Cap, L80, allies: 10);
            // 50% 사망: 기본 80 이면 계속, 0.5 덮어쓰면 패배
            World Kill5(string json)
            {
                var w = Make(json);
                Steps(w, 2);
                int n = 0;
                foreach (var u in w.Units.Where(u => u.Owner == 1).ToList()) if (n++ < 5) w.Damage(u, 1e6f, null);
                Steps(w, 3);
                return w;
            }
            var w0 = Kill5(baseJson);
            check(w0.Mission!.Result == MissionState.Running, "losses 80 (기본): 아군 50% 사망 → 계속");
            string patched = MissionDef.WithLossesFrac(baseJson, 0.5f);
            var pd = MissionDef.Parse(patched, "t_obj");
            check(pd.Failures.Count == 1 && pd.Failures[0].Type == "losses" && MathF.Abs(pd.Failures[0].Percent - 50f) < 1e-4f, $"덮어쓰기 0.5 → losses.percent {pd.Failures[0].Percent}");
            var w1 = Kill5(patched);
            check(w1.Mission!.Result == MissionState.Lost && w1.Mission.FailReason == 0, "losses 0.5 덮어쓰기: 아군 50% 사망 → 패배 (사유 losses)");
            var added = MissionDef.Parse(MissionDef.WithLossesFrac(Json(Cap, "", allies: 10), 0.4f), "t_obj");
            check(added.Failures.Count == 1 && added.Failures[0].Type == "losses" && MathF.Abs(added.Failures[0].Percent - 40f) < 1e-4f, "losses 조건이 없는 미션: 덮어쓰기가 losses 40 을 추가");
            bool thr = true;
            foreach (float bad in new[] { 0f, -0.1f, 1.01f, float.NaN }) { try { MissionDef.WithLossesFrac(baseJson, bad); thr = false; } catch (InvalidDataException) { } }
            check(thr, "maxCasualtyFrac 범위 밖 (0 · 음수 · 1 초과 · NaN) → InvalidDataException");
            // 인자 없음 = 기존 값 (미션 파일 로드 결과 동일)
            string id = MissionDef.Available().First(n => MissionDef.Peek(n)?.Failures.Any(f => f.Type == "losses") == true);
            var plain = MissionDef.Load(id); var viaNull = MissionDef.Load(id, null); var over = MissionDef.Load(id, 0.5f);
            check(plain.Json == viaNull.Json && plain.Hash == viaNull.Hash, $"인자 없음(null) = 기존 로드와 텍스트 동일 ('{id}')");
            check(over.Failures.Where(f => f.Type == "losses").All(f => MathF.Abs(f.Percent - 50f) < 1e-4f) && over.Objectives.Count == plain.Objectives.Count && over.Forces.Count == plain.Forces.Count,
                  "덮어쓴 로드: 모든 losses 50, 그 밖의 정의 불변");
            // 같은 덮어쓴 텍스트 → 스냅샷에 그대로 실려 복원
            var ws = Make(patched); Steps(ws, 5);
            var meta = new SnapshotMeta("obj", 1, false, "none");
            var ls = Snapshot.Load(Snapshot.Save(ws, meta), defs).World;
            check(MathF.Abs(ls.Mission!.Def.Failures[0].Percent - 50f) < 1e-4f && ls.ComputeChecksum() == ws.ComputeChecksum(), "저장 왕복: 덮어쓴 losses 50 유지");
        }

        // ---- 6. 세력별 항복 키: 기본 꺼짐 (전역 chance 0 이면 항복 없음), surrenderChance 를 적은 세력만 켜짐
        {
            var C = defs.CombatRules;
            float chance = C.SurrenderChance;
            try
            {
                C.SurrenderChance = 0f;
                bool Run(string fac2Extra)
                {
                    var w = Make(Json("""{ "id": "c1", "type": "capture", "area": "c1", "seconds": 999 }""", fac2Extra: fac2Extra));
                    var c = new Vector2(50, 30);
                    var list = Enumerable.Range(0, 3).Select(i => Put(w, 2, c.X + i * 1.3f, c.Y)).ToList();
                    w.Step();
                    w.FormCompany(2, list, 1); w.Step();
                    list[0].Company!.InitialStrength = 30;
                    for (int k = 0; k < 8; k++) { float a = k * MathF.PI / 4; Put(w, 1, c.X + MathF.Cos(a) * 8, c.Y + MathF.Sin(a) * 8); }
                    for (int i = 0; i < (int)(16f / Config.SimDt); i++) { list[0].Suppression = (short)(C.SuppressMax * 0.95f); w.Step(); if (list[0].Surrendered) return true; }
                    return false;
                }
                check(!Run(""), "세력 항복 키 없음 + 전역 chance 0: 포위 · 제압 · 소탕이어도 항복 안 함 (기본 꺼짐)");
                check(Run(", \"surrenderChance\": 1"), "세력 2 surrenderChance 1: 같은 상황에서 항복 (미션 데이터가 세력별로 켬)");
                check(!Run(", \"surrenderChance\": 1, \"surrenderCasualty\": 0.99"), "surrenderCasualty 0.99: 사상 부족 (27/30 사상 < 99%) → 항복 안 함");
            }
            finally { C.SurrenderChance = chance; }
        }

        // ---- 7. 패배 조건 groupWiped (태그 그룹 전멸) · losses + tag (태그 유닛만 집계). keyUnit 은 불변 (한 명만 죽어도 발동)
        {
            const string Cap = """{ "id": "c1", "type": "capture", "area": "c1", "seconds": 999 }""";
            // 병력 a (아군 4, tag sc) + 병력 x (아군 6, 태그 없음) - x 는 실패 판정에 안 섞여야 함
            string With(string failure) => Json(Cap, failure, allies: 4).Replace("\"id\": \"a\", \"faction\": 1,", "\"id\": \"a\", \"tag\": \"sc\", \"faction\": 1,")
                .Replace("{ \"id\": \"b\"", "{ \"id\": \"x\", \"faction\": 1, \"at\": [6, 45], \"units\": { \"infantry\": 6 }, \"company\": false }, { \"id\": \"b\"");
            World Kill(string failure, int n)
            {
                var w = Make(With(failure));
                Steps(w, 2);
                int k = 0;
                foreach (var id in w.Mission!.TagUnits("sc").ToList()) if (k++ < n && w.Find(id) is Unit u) w.Damage(u, 1e6f, null);
                Steps(w, 3);
                return w;
            }
            const string Wiped = """{ "type": "groupWiped", "tag": "sc" }""", Key = """{ "type": "keyUnit", "tag": "sc" }""";
            check(Make(With(Wiped)).Mission!.TagUnits("sc").Count == 4, "(전제) 태그 sc = 4명");
            check(Kill(Wiped, 3).Mission!.Result == MissionState.Running, "groupWiped: 4명 중 3명 사망 → 계속");
            var w4 = Kill(Wiped, 4);
            check(w4.Mission!.Result == MissionState.Lost && w4.Mission.FailReason == 0, "groupWiped: 4명 전원 사망 → 패배 (사유 0)");
            check(Kill(Key, 1).Mission!.Result == MissionState.Lost, "(기존) keyUnit: 1명만 죽어도 패배 - 동작 불변");
            check(Throws(With("""{ "type": "groupWiped" }"""), "tag"), "groupWiped 는 tag 필요");
            // losses + tag: 태그 4명 중 50% 이상 사망 → 패배, 태그 밖(6명) 사망은 무관
            const string Lt = """{ "type": "losses", "tag": "sc", "percent": 50 }""";
            check(Kill(Lt, 1).Mission!.Result == MissionState.Running, "losses tag: 태그 4명 중 1명(25%) → 계속");
            check(Kill(Lt, 2).Mission!.Result == MissionState.Lost, "losses tag 50: 태그 4명 중 2명(50%) → 패배");
            var wo = Make(With(Lt)); Steps(wo, 2);
            foreach (var u in wo.Units.Where(u => u.Owner == 1 && !wo.Mission!.TagUnits("sc").Contains(u.Id)).ToList()) wo.Damage(u, 1e6f, null);
            Steps(wo, 3);
            check(wo.Mission!.Result == MissionState.Running, "losses tag: 태그 밖 아군 6명 전멸해도 패배 아님 (태그만 집계)");
            // 태그 없는 losses 는 기존대로 (전체 10명 중 50% = 5)
            const string L50 = """{ "type": "losses", "percent": 50 }""";
            var wl = Make(With(L50)); Steps(wl, 2);
            int q = 0; foreach (var u in wl.Units.Where(u => u.Owner == 1).ToList()) if (q++ < 5) wl.Damage(u, 1e6f, null);
            Steps(wl, 3);
            check(wl.Mission!.Result == MissionState.Lost, "(기존) losses 태그 없음: 전체 기준 50% → 패배");
            // 저장 왕복
            var meta = new SnapshotMeta("obj", 1, false, "none");
            var l = Snapshot.Load(Snapshot.Save(w4, meta), defs).World;
            check(l.Mission!.Result == MissionState.Lost && l.Mission.Def.Failures[0].Type == "groupWiped" && l.ComputeChecksum() == w4.ComputeChecksum(), "저장 왕복: groupWiped 정의 · 결과 유지");
        }
    }
}
