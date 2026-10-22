using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T30c 화물열차 = 미션 목표: 미션 json 검증 (오류 메시지), escort (화차 도착 · 화차 파괴 실패 · 기관차 파괴 실패), eliminate (화차 파괴 · percent),
/// capture (화차 둘레 단독 확보 · 적 있으면 정지 · 소유 불변 · 화차 전멸 실패), 결과 손실 기록, AI 과제, 저장 왕복, 결정성. KD_SELFTEST_ONLY=freight</summary>
public static class FreightSelfTest
{
    // 100×48: 직선 선로 y=20 (x 2..97). 세력 1 = 플레이어, 세력 2 = 적. 목적 구역 dest = x 78..94.
    static string Json(string trains, string objectives, int seed = 7) => $$"""
    { "id": "t_freight", "title": "T", "player": 1, "seed": {{seed}},
      "factions": [ { "name": "A", "team": 1, "posture": "hold" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "freight", "width": 100, "height": 48, "base": "grass", "starts": [ [5, 45], [95, 45] ],
        "paint": [ { "terrain": "rail", "rect": [2, 20, 96, 1] } ] },
      "areas": { "dest": { "rect": [78, 15, 16, 10] } },
      "forces": [ { "id": "a", "faction": 1, "at": [4, 45], "units": { "infantry": 1 }, "company": false },
                  { "id": "b", "faction": 2, "at": [96, 45], "units": { "infantry": 1 }, "company": false } ],
      "trains": [ {{trains}} ],
      "objectives": [ {{objectives}} ] }
    """;

    const string Mine = """{ "tag": "fr", "faction": 1, "cars": ["locomotive", "wagon", "wagon"], "path": [[10, 20], [90, 20]] }""";
    const string Enemy = """{ "tag": "fr", "faction": 2, "cars": ["locomotive", "wagon", "wagon"], "path": [[30, 20], [70, 20]], "script": false }""";
    const string Escort = """{ "id": "o", "type": "escort", "train": "fr", "area": "dest", "count": 2 }""";

    static World Make(string trains, string obj, int seed = 7) =>
        World.CreateMissionWorld(MissionDef.Parse(Json(trains, obj, seed), "t_freight"), Defs.Current);

    static void Steps(World w, int n) { for (int i = 0; i < n && w.Winner == 0; i++) w.Step(); }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;

        // ---- 1. 정의 · 검증: 화차 = freight, 잘못된 화물 목표 → InvalidDataException (메시지 확인)
        check(defs.Trains.Cars["wagon"].Freight && !defs.Trains.Cars["locomotive"].Freight && !defs.Trains.Cars["guncar"].Freight, "balance.json: 화차(wagon) = 화물 객차, 기관차 · 포차 아님");
        {
            (string Trains, string Obj, string Want)[] bad =
            {
                (Mine, """{ "id": "o", "type": "escort", "train": "nope", "area": "dest" }""", "열차 tag 'nope' 없음"),
                ("""{ "tag": "fr", "faction": 1, "cars": ["locomotive", "guncar"], "path": [[10, 20], [90, 20]] }""", Escort, "화물 객차 없음"),
                (Mine, """{ "id": "o", "type": "escort", "train": "fr", "area": "dest", "count": 3 }""", "count 3"),
                (Mine, """{ "id": "o", "type": "escort", "train": "fr", "area": "dest", "count": 0 }""", "count 0"),
                (Mine, """{ "id": "o", "type": "escort", "train": "fr" }""", "구역"),
                (Mine, """{ "id": "o", "type": "capture", "train": "fr", "area": "dest", "seconds": 5 }""", "area 없이"),
                (Mine, """{ "id": "o", "type": "capture", "train": "fr", "seconds": 5, "radius": 0 }""", "radius 0"),
                (Mine, """{ "id": "o", "type": "capture", "train": "fr", "seconds": -1 }""", "seconds -1"),
                (Mine, """{ "id": "o", "type": "eliminate", "train": "fr", "percent": 0 }""", "percent 0"),
                (Mine, """{ "id": "o", "type": "eliminate", "train": "fr", "percent": 150 }""", "percent 150"),
                (Mine, """{ "id": "o", "type": "defend", "train": "fr", "seconds": 10 }""", "escort · capture · eliminate"),
                (Mine, """{ "id": "o", "type": "eliminate", "train": "fr", "tag": "x" }""", "tag 는 함께"),
                (Mine, """{ "id": "o", "type": "eliminate", "train": "", "percent": 100 }""", "train 이 비었음"),
                (Mine, """{ "id": "o", "type": "eliminate", "train": "fr", "faction": 9 }""", "faction 9"),
                ("null", Escort, "trains[0]"),
            };
            int ok = 0; var miss = new List<string>();
            foreach (var (tr, ob, want) in bad)
            {
                try { MissionDef.Parse(Json(tr, ob), "t"); miss.Add(want + " (통과됨)"); }
                catch (InvalidDataException e) { if (e.Message.Contains(want)) ok++; else miss.Add($"{want} ≠ {e.Message}"); }
            }
            check(ok == bad.Length, $"화물 목표 검증 {ok}/{bad.Length} 오류 메시지 일치{(miss.Count > 0 ? ": " + string.Join(" | ", miss) : "")}");
            bool threw = false;
            try { MissionDef.Parse(Json(Mine, Escort).Replace("\"trains\": [", "\"trains\": null, \"x\": ["), "t"); }
            catch (InvalidDataException e) { threw = e.Message.Contains("trains"); }
            check(threw, "trains: null → InvalidDataException");
        }

        // ---- 2. escort: 아군 화물열차 (스크립트) 가 화차 2량을 목적 구역에 → 완료 · 승리; 화차 1량 파괴 → 실패; 기관차 파괴 → 실패
        {
            var w = Make(Mine, Escort);
            var m = w.Mission!;
            int k = 0;
            for (; k < 60 * 90 && w.Winner == 0; k++) { w.Step(); }
            var t = w.Trains[0];
            check(m.ObjState[0] == MissionState.Done && m.Result == MissionState.Won && m.ObjProgress[0] == 2 && w.Winner == 1,
                  $"escort + train: 화차 2량 구역 도착 → 완료 · 승리 ({k / 60}초, 진행 {m.ObjProgress[0]}, 선두 x {t.Cars[0].Pos.X:0.0})");

            var w2 = Make(Mine, Escort);
            Steps(w2, 300);
            w2.Damage(w2.Trains[0].Cars[2], 100000, null);
            Steps(w2, 2);
            check(w2.Mission!.ObjState[0] == MissionState.Failed && w2.Mission.Result == MissionState.Lost && w2.Winner == 2, "escort + train: 화차 1량 파괴 (2량 필요) → 실패 · 패배");

            var w3 = Make(Mine, Escort);
            Steps(w3, 300);
            w3.Damage(w3.Trains[0].Cars[0], 100000, null);
            Steps(w3, 2);
            check(w3.Trains[0].Stalled && w3.Mission!.ObjState[0] == MissionState.Failed, "escort + train: 기관차 파괴 (열차 정지, 화차 구역 밖) → 실패");

            var w4 = Make(Mine, """{ "id": "o", "type": "escort", "train": "fr", "area": "dest", "count": 1 }""");
            Steps(w4, 300);
            w4.Damage(w4.Trains[0].Cars[2], 100000, null);
            Steps(w4, 60 * 90);
            check(w4.Mission!.ObjState[0] == MissionState.Done && w4.Mission.ObjProgress[0] == 1, "escort + train count 1: 화차 1량 잃어도 남은 1량 도착 → 완료");
        }

        // ---- 3. eliminate: 적 화물열차 화차 파괴 (100% / 50%), 결과 손실 기록
        {
            var w = Make(Enemy, """{ "id": "o", "type": "eliminate", "train": "fr" }""");
            var t = w.Trains[0];
            Steps(w, 30);
            w.Damage(t.Cars[1], 100000, null);
            Steps(w, 2);
            float p1 = w.Mission!.ObjProgress[0];
            bool active = w.Mission.ObjState[0] == MissionState.Active;
            w.Damage(t.Cars[2], 100000, null);
            Steps(w, 2);
            check(active && MathF.Abs(p1 - 0.5f) < 1e-6f && w.Mission.ObjState[0] == MissionState.Done && w.Winner == 1, $"eliminate + train: 화차 1/2 파괴 = 진행 {p1:0.00} · 진행 중, 2/2 → 완료 · 승리");
            var res = MissionResults.From(w);
            check(res.Trains.Count == 1 && res.Trains[0].Tag == "fr" && res.Trains[0].Owner == 2 && res.Trains[0].Cars == 3 && res.Trains[0].CarsLost == 2 && res.Trains[0].Freight == 2 && res.Trains[0].FreightLost == 2,
                  $"결과: 열차 손실 기록 (객차 {res.Trains[0].CarsLost}/{res.Trains[0].Cars} · 화차 {res.Trains[0].FreightLost}/{res.Trains[0].Freight})");

            var w2 = Make(Enemy, """{ "id": "o", "type": "eliminate", "train": "fr", "percent": 50 }""");
            Steps(w2, 30);
            w2.Damage(w2.Trains[0].Cars[2], 100000, null);
            Steps(w2, 2);
            check(w2.Mission!.ObjState[0] == MissionState.Done, "eliminate + train percent 50: 화차 1/2 파괴 → 완료");
            var w3 = Make(Enemy, """{ "id": "o", "type": "eliminate", "train": "fr" }""");
            Steps(w3, 30);
            w3.Damage(w3.Trains[0].Cars[0], 100000, null);
            Steps(w3, 2);
            check(w3.Mission!.ObjState[0] == MissionState.Active && w3.Mission.ObjProgress[0] == 0, "eliminate + train: 기관차 파괴는 화차 파괴가 아님 (진행 0)");
        }

        // ---- 4. capture: 적 화차 둘레 radius 3 단독 확보 2초 → 완료 (열차 소유 불변), 적 유닛 함께면 정지, 화차 전멸 → 실패
        const string Cap = """{ "id": "o", "type": "capture", "train": "fr", "seconds": 2 }""";
        {
            var w = Make(Enemy, Cap);
            var t = w.Trains[0];
            var wagon = t.Cars[2];
            Steps(w, 10);
            var foe = w.SpawnUnit(defs.Units["infantry"], 2, wagon.Pos + new Vector2(0, -2f));
            var me = w.SpawnUnit(defs.Units["infantry"], 1, wagon.Pos + new Vector2(0, 2f));
            Steps(w, 30);
            float pBoth = w.Mission!.ObjProgress[0];
            w.Damage(foe, 100000, null);
            Steps(w, 60);
            float p1 = w.Mission.ObjProgress[0];
            Steps(w, 120);
            check(pBoth == 0 && p1 > 0.9f && p1 < 1.1f && w.Mission.ObjState[0] == MissionState.Done && w.Winner == 1 && t.Owner == 2 && t.Cars.All(c => c.Owner == 2),
                  $"capture + train: 적 함께 → 진행 {pBoth:0.00}, 단독 1초 → {p1:0.00}, 2초 → 완료 · 승리, 열차 소유 그대로 (세력 {t.Owner})");

            var w2 = Make(Enemy, Cap);
            Steps(w2, 10);
            var far = w2.SpawnUnit(defs.Units["infantry"], 1, w2.Trains[0].Cars[2].Pos + new Vector2(0, 4f));
            Steps(w2, 60);
            check(w2.Mission!.ObjProgress[0] == 0, "capture + train: 반경 3 밖 (4칸) 유닛은 확보 안 됨");
            w2.Damage(w2.Trains[0].Cars[1], 100000, null);
            w2.Damage(w2.Trains[0].Cars[2], 100000, null);
            Steps(w2, 2);
            check(w2.Mission.ObjState[0] == MissionState.Failed && w2.Winner == 2, "capture + train: 화차 모두 파괴 → 실패 · 패배");
        }

        // ---- 5. AI 과제 재사용: 아군 화물 호송 = Escort (기준 = 화차 중심 → 목적지), 적 화차 탈취 = Take (화차 중심 · 반경)
        {
            var w = Make(Mine, Escort);
            Steps(w, 5);
            var o1 = TacticalAi.ObjectiveFor(w, 1);
            var o2 = TacticalAi.ObjectiveFor(w, 2);
            var mid = (w.Trains[0].Cars[1].Pos + w.Trains[0].Cars[2].Pos) / 2;
            check(o1.Kind == AiObjectiveKind.Escort && Vector2.Distance(o1.Point, mid) < 0.01f && o1.Aux == w.Mission!.Def.Area("dest")!.Center
                  && o2.Kind == AiObjectiveKind.Intercept, $"AI: 화물 호송 = Escort (화차 중심) · 적 = Intercept ({o1.Kind}/{o2.Kind})");
            var wc = Make(Enemy, Cap);
            Steps(wc, 5);
            var c1 = TacticalAi.ObjectiveFor(wc, 1);
            check(c1.Kind == AiObjectiveKind.Take && MathF.Abs(c1.Radius - 3) < 1e-6f, $"AI: 화차 탈취 = Take 반경 3 ({c1.Kind})");
        }

        // ---- 6. 저장 → 불러오기 (탈취 진행 중): 바이트 동일 · 체크섬 동일 · 이어 돌려도 같은 결과
        {
            var w = Make(Enemy, """{ "id": "o", "type": "capture", "train": "fr", "seconds": 5 }""");
            Steps(w, 10);
            w.SpawnUnit(defs.Units["infantry"], 1, w.Trains[0].Cars[2].Pos + new Vector2(0, 2f));
            Steps(w, 90);
            var meta = new SnapshotMeta("freight", 1, false, "none");
            var bytes = Snapshot.Save(w, meta);
            var l = Snapshot.Load(bytes, defs).World;
            var again = Snapshot.Save(l, meta);
            check(l.Mission!.ObjProgress[0] == w.Mission!.ObjProgress[0] && w.Mission.ObjProgress[0] > 1 && l.Mission.Def.Objectives[0].Train == "fr" && l.ComputeChecksum() == w.ComputeChecksum(),
                  $"불러오기: 화물 목표 정의 · 진행 {l.Mission.ObjProgress[0]:0.00} 복원, 체크섬 같음");
            check(Snapshot.RawBody(bytes).AsSpan().SequenceEqual(Snapshot.RawBody(again)), "저장 → 불러오기 → 저장 본문 바이트 동일 (화물 목표)");
            bool same = true;
            for (int i = 0; i < 600; i++) { w.Step(); l.Step(); if (i % 60 == 0) same &= w.ComputeChecksum() == l.ComputeChecksum(); }
            check(same && w.Mission.ObjState[0] == MissionState.Done && l.Mission.ObjState[0] == MissionState.Done && l.Winner == w.Winner,
                  "불러온 월드 이어 돌림: 체크섬 동일 · 같은 틱에 완료");
            ulong h = w.ComputeChecksum();
            w.Mission.ObjProgress[0] += 0.5f;
            check(w.ComputeChecksum() != h, "체크섬에 화물 목표 진행 포함");
        }

        // ---- 7. 결정성: AI 붙인 호송 미션 2회 체크섬 동일
        {
            World A() { var w = Make(Mine, Escort, 3); World.AttachMissionAi(w, 0, all: true); return w; }
            var a = A(); var b = A();
            bool same = true;
            for (int i = 0; i < 60 * 40; i++) { a.Step(); b.Step(); if (i % 300 == 0) same &= a.ComputeChecksum() == b.ComputeChecksum(); }
            check(same && a.ComputeChecksum() == b.ComputeChecksum(), "같은 시드 화물 호송 (AI 전 세력) 2회: 체크섬 동일");
        }
    }
}
