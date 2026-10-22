using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// TC-B SK2 경보 상태 검증 (KD_SELFTEST_ONLY=tcb): 감지 경보 조건 (seenBy · seenTag) · 지연 증원 (spawn delay) · 순찰 (patrol) ·
/// 저장 / 불러오기 중간 이어 돌리기 · 같은 시드 두 번 = 같은 체크섬 · 기능 안 쓰면 새 상태 0 (기존 미션 불변).
/// </summary>
public static class AlertSelfTest
{
    const string Template = """
    { "id": "t_alert", "title": "T", "player": 1, "seed": 7,
      "factions": [ { "name": "A", "team": 1, "posture": "attack", "area": "goal" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "t", "width": 64, "height": 64, "base": "grass", "starts": [ [10, 32], [50, 32] ] },
      "areas": { "goal": { "rect": [28, 26, 10, 12] } },
      "deployZones": [ { "faction": 1, "rect": [2, 20, 20, 24] } ],
      "forces": [
        { "id": "p1", "faction": 1, "at": [10, 28], "facing": 90, "units": { "officer": 1, "infantry": 10 } },
        { "id": "sc", "faction": 1, "at": [8, 36], "facing": 90, "tag": "scouts", "units": { "scout": 3 }, "company": false },
        { "id": "e1", "faction": 2, "at": [56, 32], "facing": 270, "units": { "infantry": 6 } },
        { "id": "pt", "faction": 2, "at": [56, 8], "facing": 270, PATROL "units": { "infantry": 4 } }
      ],
      "reinforcements": [ { "id": "r_alarm", "faction": 2, "at": [60, 60], "units": { "infantry": 5 } } ],
      "objectives": [ { "id": "o", "type": "defend", "seconds": 9999 } ],
      "failures": [ ],
      "events": [ EVT ] }
    """;

    static MissionDef Make(string evt, string patrol = "") =>
        MissionDef.Parse(Template.Replace("EVT", evt).Replace("PATROL", patrol), "t_alert");

    static World W(MissionDef d, bool fog) { var w = World.CreateMissionWorld(d, Defs.Current); w.Fog.Enabled = fog; return w; }
    static void Steps(World w, float sec) { for (int i = 0; i < (int)MathF.Round(sec / Config.SimDt) && w.Winner == 0; i++) w.Step(); }
    static List<Unit> Force(World w, string id) => w.Mission!.ForceUnits[id].Select(i => w.Find(i)).OfType<Unit>().ToList();
    static void Teleport(IEnumerable<Unit> us, Vector2 c) { int k = 0; foreach (var u in us) { u.Pos = c + new Vector2(k % 4 * 0.9f, k / 4 * 0.9f); k++; } }
    static bool Rejects(string evt, string patrol = "") { try { Make(evt, patrol); return false; } catch (InvalidDataException) { return true; } }
    static int PatrolIdx(World w) => w.Mission!.Def.Forces.FindIndex(f => f.Id == "pt");

    const string SeenEvt = """{ "id": "sk2_alarm", "when": { "seenBy": 2 }, "do": [ { "message": "alarm" }, { "spawn": "r_alarm", "delay": 5 } ] }""";
    const string Pat3 = "\"patrol\": [[56, 8], [40, 8], [40, 20]],";

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        // ---- 검증 (Validate): 범위 밖 값 거부
        check(Rejects("""{ "id": "a", "when": { "seenBy": 3 }, "do": [ ] }""") && Rejects("""{ "id": "a", "when": { "time": 1 }, "do": [ { "spawn": "r_alarm", "delay": -1 } ] }""")
              && Rejects("""{ "id": "a", "when": { "time": 1 }, "do": [ { "spawn": "r_alarm", "delay": 99999 } ] }""")
              && Rejects(SeenEvt, "\"patrol\": [[1, 1]],") && Rejects(SeenEvt, "\"patrol\": [[1, 1], [2, -5]],"),
            "검증: seenBy 세력 범위 · delay 0~3600 · patrol 두 점 이상 + 음수 거부");
        check(!Rejects(SeenEvt, "\"patrol\": [[50, 8], [58, 8]],"), "검증: 정상 값 통과");

        // ---- 감지 경보: 멀리 있으면 조용, 적 시야 안에 들어가면 발동. 안개 꺼짐 = 발동 안 함.
        {
            var w = W(Make(SeenEvt), fog: true);
            var m = w.Mission!;
            Steps(w, 3f);
            check(!m.EventFired[0] && m.ReinforcementDue[0] == 0, "경보: 멀리 떨어진 동안 조용 (3초)");
            Teleport(Force(w, "p1").Take(2), new Vector2(46, 32));
            Steps(w, 3f);
            check(m.EventFired[0] && m.Messages.Any(x => x.Text == "alarm"), "경보: 정찰 대상이 적 시야에 들면 sk2_alarm 발동");
            long due = m.ReinforcementDue[0];
            check(due > 0 && !m.ReinforcementSpawned[0], $"지연 증원: 예약만 됨 (도착 틱 {due}), 아직 안 나옴");
            int before = w.Units.Count(u => u.Owner == 2);
            while (w.Tick + 2 < due) w.Step();
            check(!m.ReinforcementSpawned[0] && w.Units.Count(u => u.Owner == 2) == before, "지연 증원: 도착 직전까지 없음");
            Steps(w, 0.2f);
            check(m.ReinforcementSpawned[0] && w.Units.Count(u => u.Owner == 2) == before + 5, "지연 증원: 5초 뒤 등장 (+5)");
            check(m.Spawned[2] == 15, "지연 증원: 생성 집계 반영");
        }
        {
            var w = W(Make(SeenEvt), fog: false);
            Teleport(Force(w, "p1").Take(2), new Vector2(46, 32));
            Steps(w, 3f);
            check(!w.Mission!.EventFired[0], "경보: 안개 꺼짐이면 발동 안 함 (전지 시야 오발 방지)");
        }
        {   // seenTag: 정찰 분대만 대상 — 보병이 보여도 안 울리고, 정찰병이 보이면 울린다 (은폐 탐지는 확률이라 오래 본다).
            string evt = """{ "id": "sk2_alarm", "when": { "seenBy": 2, "seenTag": "scouts" }, "do": [ { "message": "alarm" } ] }""";
            var w = W(Make(evt), fog: true);
            Teleport(Force(w, "p1").Take(2), new Vector2(46, 32));
            Steps(w, 3f);
            check(!w.Mission!.EventFired[0], "경보 seenTag: 대상 아닌 보병이 보여도 조용");
            Teleport(Force(w, "sc"), new Vector2(46, 34));
            Steps(w, 30f);
            check(w.Mission.EventFired[0], "경보 seenTag: 태그 정찰병이 시야 · 은폐 탐지에 걸리면 발동 (30초 안)");
        }

        // ---- 지연 0 = 기존처럼 즉시 + 쓰지 않으면 새 상태 0
        {
            var w = W(Make("""{ "id": "x", "when": { "time": 1 }, "do": [ { "spawn": "r_alarm" } ] }"""), fog: false);
            Steps(w, 1.2f);
            check(w.Mission!.ReinforcementSpawned[0] && w.Mission.ReinforcementDue[0] == 0 && w.Mission.PatrolLeg.All(x => x == 0), "delay 없는 spawn = 즉시, 새 상태 0 (기존 미션 불변)");
        }

        // ---- 순찰: 경유점을 돌고, 경계 상태가 되면 멈춘다
        {
            var w = W(Make(SeenEvt, Pat3), fog: false);
            var pt = Force(w, "pt");
            int idx = PatrolIdx(w);
            var start = pt[0].Pos;
            Steps(w, 20f);
            check(w.Mission!.PatrolLeg[idx] >= 2 && Vector2.Distance(start, pt[0].Pos) > 1f, $"순찰: 경유점을 따라 이동 (이동 {Vector2.Distance(start, pt[0].Pos):0.0}칸, 다음 점 번호 {w.Mission.PatrolLeg[idx]})");
            foreach (var u in pt) u.Order = UnitOrder.Idle;
            w.MarkAlert(2, pt[0].Pos);
            int leg = w.Mission.PatrolLeg[idx];
            Steps(w, 3f);   // 경계 유지 10초 안
            check(w.Mission.PatrolLeg[idx] == leg, "순찰: 경계 상태에서는 새 순찰 명령 없음");
            Steps(w, 12f);
            check(w.Mission.PatrolLeg[idx] != leg && pt.Any(u => u.Order == UnitOrder.Move), "순찰: 경계가 풀리면 다시 순찰 (다음 점으로 이동 명령)");
        }
        {   // 미션 AI 와 함께 (실제 플레이): 순찰조가 AI 에 덮어써지지 않고 움직이는지
            var w = W(Make(SeenEvt, Pat3), fog: true);
            World.AttachMissionAi(w, 1, true);
            var pt = Force(w, "pt");
            var start = pt[0].Pos;
            Steps(w, 25f);
            check(Vector2.Distance(start, pt[0].Pos) > 5f || w.Mission!.PatrolLeg[PatrolIdx(w)] >= 2, $"순찰 + 미션 AI: 순찰 유지 (이동 {Vector2.Distance(start, pt[0].Pos):0.0}칸)");
        }

        // ---- 결정성 · 저장
        {
            World Build()
            {
                var w = W(Make(SeenEvt, Pat3), fog: true);
                World.AttachMissionAi(w, 1, true);
                Teleport(Force(w, "p1").Take(2), new Vector2(46, 32));
                return w;
            }
            var a = Build(); var b = Build();
            Steps(a, 2f); Steps(b, 2f);
            check(a.Mission!.ReinforcementDue[0] > 0 && !a.Mission.ReinforcementSpawned[0], "저장 시험 전제: 경보 예약 중 (증원 대기)");
            var meta = new SnapshotMeta("t", 1, false, "mission");
            var bytes = Snapshot.Save(a, meta);
            var l = Snapshot.Load(bytes, Defs.Current).World;
            check(l.Mission!.ReinforcementDue.SequenceEqual(a.Mission.ReinforcementDue) && l.Mission.PatrolLeg.SequenceEqual(a.Mission.PatrolLeg) && l.ComputeChecksum() == a.ComputeChecksum(), "저장 → 불러오기: 예약 · 순찰 상태 · 체크섬 복원");
            check(Snapshot.RawBody(Snapshot.Save(l, meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes)), "저장 → 불러오기 → 저장 바이트 동일");
            Steps(a, 20f); Steps(b, 20f); Steps(l, 20f);
            check(a.ComputeChecksum() == b.ComputeChecksum(), "같은 시드 두 번 = 같은 체크섬 (경보 · 지연 증원 · 순찰 포함)");
            check(a.ComputeChecksum() == l.ComputeChecksum() && l.Mission.ReinforcementSpawned[0], "예약 중 저장한 월드를 이어 돌린 결과 = 원래 (증원 등장 포함)");
        }
    }
}
