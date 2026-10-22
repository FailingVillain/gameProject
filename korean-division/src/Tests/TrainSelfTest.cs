using System.Diagnostics;
using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T30a 열차: 선로 위 이동 (조종 · 스크립트 · 순환선) · 선로 끝 정지 · 객차 간격 · 선로 위 유닛 정지 · 저장 왕복 · 리플레이 · 결정성. KD_SELFTEST_ONLY=train</summary>
public static class TrainSelfTest
{
    // 100×40: 직선 선로 y=20 (x 2..97) + 순환선 사각형 (x 10..50, y 4..14 테두리)
    static string Json(string trains, string extraForces = "") => $$"""
    { "id": "t_train", "title": "T", "player": 1, "seed": 7,
      "factions": [ { "name": "A", "team": 1, "posture": "hold" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "train", "width": 100, "height": 48, "base": "grass", "starts": [ [5, 35], [95, 35] ],
        "paint": [ { "terrain": "rail", "rect": [2, 20, 96, 1] },
                   { "terrain": "rail", "rect": [10, 4, 41, 1] }, { "terrain": "rail", "rect": [10, 14, 41, 1] },
                   { "terrain": "rail", "rect": [10, 4, 1, 11] }, { "terrain": "rail", "rect": [50, 4, 1, 11] } ] },
      "forces": [ { "id": "a", "faction": 1, "at": [5, 35], "units": { "infantry": 2 }, "company": false },
                  { "id": "b", "faction": 2, "at": [95, 35], "units": { "infantry": 2 }, "company": false }{{extraForces}} ],
      "trains": [ {{trains}} ],
      "objectives": [ { "id": "o", "type": "defend", "seconds": 9999 } ] }
    """;

    const string Line3 = """{ "tag": "t1", "faction": 1, "cars": ["locomotive", "wagon", "wagon"], "path": [[2, 20], [97, 20]], "script": false }""";
    const string Shuttle = """{ "tag": "s", "faction": 2, "cars": ["locomotive", "wagon"], "path": [[2, 20], [50, 20], [97, 20]], "waits": [2, 3, 2] }""";
    const string Loop = """{ "tag": "l", "faction": 1, "cars": ["locomotive", "wagon", "wagon", "wagon"], "path": [[10, 4], [50, 4], [50, 14], [10, 14]], "loop": true, "waits": [1, 0, 0, 0] }""";

    static World Make(string trains, string extra = "")
    {
        var w = World.CreateMissionWorld(MissionDef.Parse(Json(trains, extra), "t_train"), Defs.Current);
        w.Fog.Enabled = false;
        return w;
    }

    static bool OnRail(World w, Train t) => t.Cars.All(c => w.Map.Get((int)MathF.Floor(c.Pos.X), (int)MathF.Floor(c.Pos.Y)) == TerrainType.Rail);
    static bool SpacingOk(Train t, float pitch, float tol) =>
        t.Cars.Zip(t.Cars.Skip(1), (a, b) => Vector2.Distance(a.Pos, b.Pos)).All(d => d <= pitch + 1e-3f && d >= pitch - tol);

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var r = Defs.Current.Trains;
        float pitch = r.CarSpacing;

        // ---- 1. 조종: 전진 → 선로 끝 정지 (관통 없음) · 후진 → 반대 끝 · 정지 명령 · 남의 열차 조종 거부
        {
            var w = Make(Line3);
            var t = w.Trains[0];
            check(w.Trains.Count == 1 && t.Cars.Count == 3 && t.Manual && t.S == t.TailOffset(r) && OnRail(w, t) && SpacingOk(t, pitch, 0.01f),
                  $"열차 생성: 객차 3량, 선로 시작에 꼬리 (S={t.S / (float)TrainTrack.Unit:0.00}칸), 객차 간격 {pitch}");
            check(w.TrainMove(2, t.Id, 1) == CommandResult.NotAllowed, "남의 열차 조종 거부");
            w.Issue(new TrainMoveCommand(2, t.Id, 1));
            for (int i = 0; i < 60; i++) w.Step();
            check(t.S == t.TailOffset(r) && t.V == 0, "남의 세력 명령은 실행 단계에서도 무시");
            w.TrainMove(1, t.Id, 1);
            int maxV = 0; bool rail = true, spacing = true; long prevS = t.S; bool mono = true;
            for (int i = 0; i < 60 * 60; i++)
            {
                w.Step();
                maxV = Math.Max(maxV, Math.Abs(t.V)); rail &= OnRail(w, t); spacing &= SpacingOk(t, pitch, 0.01f);
                mono &= t.S >= prevS; prevS = t.S;
            }
            check(t.S == t.Track.Length && t.V == 0, $"전진: 선로 끝에서 정지 (S={t.S} = 길이 {t.Track.Length}, V={t.V})");
            check(maxV <= r.VMax && maxV == r.VMax && mono, $"최고 속도 {maxV} ≤ {r.VMax} (호장/틱) · 뒤로 가지 않음");
            check(rail && spacing, "이동 중 모든 객차가 선로 칸 위 · 간격 유지");
            var head = t.Cars[0].Pos;
            check(head.X <= 98f && head.X >= 97f, $"선두 객차가 선로 마지막 칸 안 (x={head.X:0.00})");
            w.TrainMove(1, t.Id, -1);
            for (int i = 0; i < 60 * 60; i++) w.Step();
            check(t.S == t.TailOffset(r) && t.V == 0 && OnRail(w, t), "후진: 반대 끝에서 정지 (꼬리 = 선로 시작)");
            w.TrainMove(1, t.Id, 1);
            for (int i = 0; i < 600; i++) w.Step();
            long s0 = t.S;
            w.TrainMove(1, t.Id, 0);
            for (int i = 0; i < 400; i++) w.Step();
            long s1 = t.S;
            for (int i = 0; i < 120; i++) w.Step();
            float brakeTiles = (s1 - s0) / (float)TrainTrack.Unit;
            check(t.V == 0 && t.S == s1 && s1 > s0 && brakeTiles <= r.MaxSpeed * r.MaxSpeed / (2 * r.Brake) + 0.5f,
                  $"정지 명령: 제동 거리 {brakeTiles:0.00}칸 뒤 정지, 이후 움직이지 않음");
        }

        // ---- 2. 스크립트: 경유점 · 정차 · 끝에서 되돌아 반복, 같은 시드 2회 체크섬 동일
        {
            var a = Make(Shuttle); var b = Make(Shuttle);
            var t = a.Trains[0];
            var arrivals = new List<(long Tick, int Wp)>();
            int lastNext = t.Next;
            bool same = true, rail = true;
            for (int i = 0; i < 60 * 200; i++)
            {
                a.Step(); b.Step();
                if (t.Next != lastNext) { arrivals.Add((a.Tick, lastNext)); lastNext = t.Next; }
                if (i % 300 == 0) same &= a.ComputeChecksum() == b.ComputeChecksum();
                rail &= OnRail(a, t);
            }
            same &= a.ComputeChecksum() == b.ComputeChecksum();
            string seq = string.Join(",", arrivals.Select(x => x.Wp));
            check(seq.StartsWith("0,1,2,1,0,1,2"), $"스크립트 경유점 순서 왕복 반복 ({seq})");
            check(rail, "스크립트 이동 중 객차는 선로 위");
            // 같은 구간 왕복 주기가 같다 (0 → 2 → 0 을 두 번)
            var zeros = arrivals.Where(x => x.Wp == 0).Select(x => x.Tick).ToList();
            bool periodic = zeros.Count >= 3 && Math.Abs((zeros[2] - zeros[1]) - (zeros[1] - zeros[0])) <= 1;
            check(periodic, $"반복 재생 주기 일정 (경유점 0 도착 틱 {string.Join(",", zeros.Take(4))})");
            check(same, "같은 미션 2회: 체크섬 동일 (열차 포함)");
            // 정차 시간: 경유점 1 에서 3초 정차
            var w2 = Make(Shuttle);
            var t2 = w2.Trains[0];
            long stopTicks = 0; bool counting = false; long maxStop = 0;
            for (int i = 0; i < 60 * 80; i++)
            {
                w2.Step();
                if (t2.V == 0 && t2.S == t2.StopArc(1, r)) { stopTicks++; counting = true; }
                else if (counting) { maxStop = Math.Max(maxStop, stopTicks); stopTicks = 0; counting = false; }
            }
            check(maxStop >= 180 && maxStop <= 183, $"경유점 1 정차 ≈ 3초 ({maxStop}틱)");
        }

        // ---- 3. 순환선: 계속 돈다 · 선로 위 · 간격
        {
            var w = Make(Loop);
            var t = w.Trains[0];
            long travelled = 0, prev = t.S; bool rail = true, spacing = true;
            for (int i = 0; i < 60 * 120; i++)
            {
                w.Step();
                travelled += t.Track.Wrap(t.S - prev); prev = t.S;
                rail &= OnRail(w, t); spacing &= SpacingOk(t, pitch, 0.45f);   // 모서리에서는 현(弦) 거리가 짧아짐
            }
            float laps = travelled / (float)t.Track.Length;
            check(t.Track.Loop && laps > 1.5f, $"순환선: 2분 동안 {laps:0.00}바퀴 (길이 {t.Track.Length / (float)TrainTrack.Unit:0}칸)");
            check(rail && spacing, "순환선: 객차는 선로 위 · 간격 유지 (모서리 현 거리 허용)");
        }

        // ---- 4. 선로 위 유닛: 앞에 보병이 있으면 멈추고, 비키면 다시 간다
        {
            var w = Make(Line3);
            var t = w.Trains[0];
            var inf = w.SpawnUnit(Defs.Current.Units["infantry"], 2, new Vector2(40.5f, 20.5f));
            w.TrainMove(1, t.Id, 1);
            for (int i = 0; i < 60 * 40; i++) w.Step();
            float gap = inf.Pos.X - t.Cars[0].Pos.X;
            check(t.V == 0 && t.Blocked && gap > 0.5f && gap < r.Lookahead + pitch, $"선로 위 보병 앞에서 정지 (선두와 간격 {gap:0.00}칸, 막힘 {t.Blocked})");
            inf.Hp = 0;
            for (int i = 0; i < 60 * 5; i++) w.Step();
            check(t.V > 0 && !t.Blocked && t.Cars[0].Pos.X > inf.Pos.X - 0.5f, $"보병이 없어지면 다시 출발 (V={t.V})");
        }
        // ---- 4b. 막혀 멈춘 뒤 반대 방향 명령: 막힘 값이 남지 않고 바로 후진
        {
            var w = Make(Line3);
            var t = w.Trains[0];
            w.SpawnUnit(Defs.Current.Units["infantry"], 2, new Vector2(40.5f, 20.5f));
            w.TrainMove(1, t.Id, 1);
            for (int i = 0; i < 60 * 40; i++) w.Step();
            bool was = t.Blocked && t.V == 0;
            w.TrainMove(1, t.Id, -1);
            w.Step(); w.Step();
            check(was && !t.Blocked && t.V < 0, $"막혀 멈춘 뒤 후진 명령 → 2틱 안에 막힘 해제 · 후진 (막힘 {t.Blocked}, V={t.V})");
        }

        // ---- 5. 저장 → 불러오기 왕복: 바이트 동일 + 이어 돌린 체크섬 동일 (움직이는 중 · 정차 중 · 순환)
        {
            var w = Make(Shuttle + ", " + Loop.Replace("\"tag\": \"l\"", "\"tag\": \"l2\""));
            for (int i = 0; i < 1000; i++) w.Step();
            var meta = new SnapshotMeta("train", 1, false, "none");
            var bytes = Snapshot.Save(w, meta);
            var l = Snapshot.Load(bytes, Defs.Current).World;
            var again = Snapshot.Save(l, meta);
            check(l.Trains.Count == 2 && l.ComputeChecksum() == w.ComputeChecksum() && l.Trains.Zip(w.Trains).All(p => p.First.S == p.Second.S && p.First.V == p.Second.V && p.First.Cars[1].Pos == p.Second.Cars[1].Pos),
                  $"불러오기: 열차 2대 상태 · 객차 위치 · 체크섬 같음 (V={w.Trains[0].V})");
            check(Snapshot.RawBody(bytes).AsSpan().SequenceEqual(Snapshot.RawBody(again)), "저장 → 불러오기 → 저장 본문 바이트 동일");
            bool same = true;
            for (int i = 0; i < 3000; i++) { w.Step(); l.Step(); if (i % 100 == 0) same &= w.ComputeChecksum() == l.ComputeChecksum(); }
            check(same && w.ComputeChecksum() == l.ComputeChecksum(), "불러온 월드를 이어 돌려도 체크섬 동일 (3000틱)");
        }

        // ---- 6. 리플레이: 열차 조종 명령 포함 기록 → 파일 → 재생 일치
        {
            var w = Make(Line3 + ", " + Shuttle);
            w.RecordCommands = true;
            var t = w.Trains[0];
            for (int i = 0; i < 60 * 50; i++)
            {
                if (i == 10) w.TrainMove(1, t.Id, 1);
                if (i == 900) w.TrainMove(1, t.Id, 0);
                if (i == 1500) w.TrainMove(1, t.Id, -1);
                if (i == 2400) w.TrainMove(1, t.Id, 1);
                w.Step();
            }
            var rep = ReplayFile.FromWorld(w, 1);
            string tmp = TestTemp.Child("kd_train.kdr.json");
            rep.Save(tmp);
            var back = ReplayFile.Load(tmp);
            long bad = back.Verify(Defs.Current, out string report);
            check(bad < 0 && back.Commands.Count(c => c.Command is TrainMoveCommand) == 4, $"리플레이 (열차 명령 4개) 재생 일치: {report}");
            File.Delete(tmp);
        }

        // ---- 7. 잘못된 경로 거부 · 열차 없는 미션은 체크섬에 열차 항목 없음
        {
            bool threw = false;
            try { Make("""{ "faction": 1, "cars": ["locomotive"], "path": [[2, 20], [60, 30]] }"""); }
            catch (InvalidDataException) { threw = true; }
            check(threw, "선로가 아닌 경유점 → 미션 오류 (InvalidDataException)");
            threw = false;
            try { Make("""{ "faction": 1, "cars": ["locomotive", "tank"], "path": [[2, 20], [97, 20]] }"""); }
            catch (InvalidDataException) { threw = true; }
            check(threw, "모르는 객차 종류 → 미션 오류");
            // 입력 방어: 전부 InvalidDataException 으로만 거부 (다른 예외 · 통과 금지)
            static string Rej(Func<World> f) { try { f(); return "통과"; } catch (InvalidDataException e) { return e.Message.Length > 0 ? "" : "빈 메시지"; } catch (Exception e) { return e.GetType().Name; } }
            check(Rej(() => Make("""{ "faction": 1, "cars": null, "path": [[2, 20], [97, 20]] }""")) == "", "cars: null → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": [], "path": [[2, 20], [97, 20]] }""")) == "", "객차 0량 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon","wagon","wagon","wagon","wagon","wagon","wagon","wagon","wagon"], "path": [[2, 20], [97, 20]] }""")) == "", "객차 최대(8) 초과 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon", null], "path": [[2, 20], [97, 20]] }""")) == "", "객차 종류 null → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": null }""")) == "", "path: null → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20], null] }""")) == "", "path 원소 null → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20]] }""")) == "", "경유점 1개 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20], [97, 20]], "start": 2 }""")) == "", "start 범위 밖 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20], [97, 20]], "start": -1 }""")) == "", "start 음수 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20], [97, 20]], "waits": [-1, 0] }""")) == "", "waits 음수 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20], [97, 20]], "waits": [1, 2, 3] }""")) == "", "waits 가 경유점보다 많음 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20], [97, 20]], "waits": [1e30, 0] }""")) == "", "waits 거대 값 → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "loop": true, "path": [[2, 20], [50, 20]] }""")) == "", "닫히지 않는 순환선(직선) → 거부");
            check(Rej(() => Make("""{ "faction": 1, "cars": ["wagon"], "path": [[2, 20], [97, 20]] }, null""")) == "", "trains 원소 null → 거부");
            {
                var w0 = Make("");
                string nan = "";
                try { w0.SpawnTrain(new MissionTrain { Faction = 1, Cars = new() { "wagon" }, Path = new[] { new[] { 2, 20 }, new[] { 97, 20 } }, Waits = new[] { float.NaN, 0f } }); nan = "통과"; }
                catch (InvalidDataException) { } catch (Exception e) { nan = e.GetType().Name; }
                check(nan == "" && w0.Trains.Count == 0, "waits NaN → 거부 (열차 추가 안 됨)");
            }
            var w = Make(Line3);
            ulong h1 = w.ComputeChecksum();
            w.Trains[0].S += 1;
            check(w.ComputeChecksum() != h1, "체크섬에 열차 호장 포함");
        }

        // ---- 8. 틱 비용 (보고용): 같은 병력 · 같은 지도, 열차 3대 있음 / 없음 번갈아 30틱씩 20번
        {
            string forces = """, { "id": "c1", "faction": 1, "at": [30, 32], "units": { "officer": 4, "infantry": 240 } }, { "id": "c2", "faction": 2, "at": [70, 32], "units": { "officer": 4, "infantry": 240 } }""";
            var a = Make(Line3 + ", " + Shuttle + ", " + Loop, forces);
            var b = Make("", forces);
            a.TrainMove(1, a.Trains[0].Id, 1);
            for (int i = 0; i < 120; i++) { a.Step(); b.Step(); }
            var swA = new Stopwatch(); var swB = new Stopwatch();
            for (int k = 0; k < 20; k++)
            {
                swA.Start(); for (int i = 0; i < 30; i++) a.Step(); swA.Stop();
                swB.Start(); for (int i = 0; i < 30; i++) b.Step(); swB.Stop();
            }
            double ta = swA.Elapsed.TotalMilliseconds / 600 * 1000, tb = swB.Elapsed.TotalMilliseconds / 600 * 1000;
            Console.WriteLine($"  [측정] 유닛 {a.Units.Count}/{b.Units.Count}: 열차 3대 {ta:0.0}µs/틱 vs 없음 {tb:0.0}µs/틱 → {(ta - tb) / tb * 100:+0.00;-0.00}%");
            check(b.Trains.Count == 0 && a.Trains.Count == 3, "틱 비용 측정 (보고용)");
        }
    }
}
