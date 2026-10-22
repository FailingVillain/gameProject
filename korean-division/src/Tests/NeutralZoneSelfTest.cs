using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>TC-G 중립 구역 (NeutralZone.cs): neutralZones 검증 · 구역 안 / 구역을 가로지르는 플레이어 이동 거절 (우회 경유점은 허용) · AI 세력 이동은 제한 없음 ·
/// 구역 안 표적 공격 · 자동 사격 · 지점 포격 거부 (구역 밖 대조군은 정상) · 기능 꺼짐 (neutralZones 없음) 에서는 거절 없음 · 저장 왕복. KD_SELFTEST_ONLY=neutralzone</summary>
public static class NeutralZoneSelfTest
{
    // 100×48 풀밭. 구역 annex = x 40..56, y 10..26. 세력 1 = 플레이어 (왼쪽), 세력 2 = 적.
    static string Json(string zones) => $$"""
    { "id": "t_neutral", "title": "T", "player": 1, "seed": 5,
      "factions": [ { "name": "A", "team": 1, "posture": "hold" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "neutral", "width": 100, "height": 48, "base": "grass", "starts": [ [5, 18], [95, 18] ] },
      "areas": { "annex": { "rect": [40, 10, 16, 16] } },
      {{zones}}
      "forces": [ { "id": "a", "faction": 1, "at": [10, 18], "units": { "infantry": 1 }, "company": false },
                  { "id": "b", "faction": 2, "at": [90, 18], "units": { "infantry": 1 }, "company": false } ],
      "objectives": [ { "id": "o", "type": "eliminate", "primary": true } ] }
    """;

    static World Make(string zones) => World.CreateMissionWorld(MissionDef.Parse(Json(zones), "t_neutral"), Defs.Current);
    static void Steps(World w, int n) { for (int i = 0; i < n && w.Winner == 0; i++) w.Step(); }

    static Unit Mine(World w, int faction) => w.Units.First(u => u.Owner == faction);

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var _ = TestDefs.NoCommandDelay();
        const string On = "\"neutralZones\": [\"annex\"],";
        try
        {
            // ---- 1. 검증
            int bad = 0;
            foreach (var z in new[] { "\"neutralZones\": [\"nope\"],", "\"neutralZones\": [\"annex\", \"annex\"]," })
            {
                try { MissionDef.Parse(Json(z), "t"); } catch (InvalidDataException e) when (e.Message.Contains("neutralZones")) { bad++; }
            }
            check(bad == 2, $"neutralZones 검증: 없는 구역 · 중복 거부 ({bad}/2)");

            // ---- 2. 플레이어 이동: 구역 안 · 가로지르기 거절, 우회 경유점 허용
            var w = Make(On);
            var me = Mine(w, 1);
            void Try(Vector2 to, out bool moving)
            {
                w.Stop(1, new[] { me }); Steps(w, 2);
                w.Move(1, new[] { me }, to); Steps(w, 2);
                moving = me.Order == UnitOrder.Move;
            }
            Try(new Vector2(48, 18), out bool inside);
            Try(new Vector2(80, 18), out bool across);
            Try(new Vector2(80, 40), out bool around);    // (10,18)→(80,40): 구역 아래로 지나감
            Try(new Vector2(30, 30), out bool near);
            check(!inside && !across && around && near, $"플레이어 이동: 구역 안 거절 {!inside}, 가로지르기 거절 {!across}, 구역 밖 직선 허용 {around}/{near}");
            w.Notices.Clear(); w.Stop(1, new[] { me }); Steps(w, 2);
            w.AttackMove(1, new[] { me }, new Vector2(48, 18)); w.QuickStep(1, new[] { me }, new Vector2(80, 18)); Steps(w, 2);
            check(me.Order != UnitOrder.AttackMove && me.Order != UnitOrder.Move && w.Notices.Count(n => n.Result == CommandResult.NotAllowed) >= 2, $"공격 이동 · 속보도 거절 (알림 {w.Notices.Count})");

            // AI 세력(2)은 제한 없음, 구역 안에서 나가는 이동 허용
            var foe = Mine(w, 2);
            w.Move(2, new[] { foe }, new Vector2(10, 18)); Steps(w, 2);
            check(foe.Order == UnitOrder.Move, "미션 플레이어가 아닌 세력의 이동은 제한 없음 (1단계)");
            me.Pos = new Vector2(48, 18); w.Stop(1, new[] { me }); Steps(w, 2);
            w.Move(1, new[] { me }, new Vector2(48, 40)); Steps(w, 2);
            check(me.Order == UnitOrder.Move, "구역 안에서 밖으로 나가는 이동은 허용");

            // ---- 3. 사격: 구역 안 표적
            string Fire(bool inZone)
            {
                var f = Make(On);
                var a = Mine(f, 1); var e = Mine(f, 2);
                a.Pos = new Vector2(36, 18); e.Pos = inZone ? new Vector2(44, 18) : new Vector2(36, 27);
                a.Order = UnitOrder.Hold; e.Hp = 100000; e.Order = UnitOrder.Hold;
                int shots = 0;
                for (int i = 0; i < 60 * 10; i++) { f.Step(); shots += f.Shots.Count(s => s.ShooterId == a.Id && s.Tick == f.Tick - 1); }
                f.Attack(1, new[] { a }, e); Steps(f, 2);
                return $"{shots}|{a.Order == UnitOrder.Attack}";
            }
            // 거리 8~9 칸 (소총 사거리 안): 구역 안 표적 (44,18) vs 구역 밖 대조군 (36,27)
            var inR = Fire(true).Split('|'); var outR = Fire(false).Split('|');
            check(int.Parse(inR[0]) == 0 && inR[1] == "False", $"구역 안 표적: 자동 사격 0발 · 공격 명령 거절 (사격 {inR[0]}, 명령 {inR[1]})");
            check(int.Parse(outR[0]) > 0 && outR[1] == "True", $"대조군 (구역 밖 표적): 자동 사격 {outR[0]}발 · 공격 명령 수락 {outR[1]}");

            // ---- 4. 지점 포격
            {
                var g = Make(On);
                var gun = g.SpawnUnit(Defs.Current.Units["heavy_howitzer"], 1, new Vector2(20, 30));
                g.GroundFire(1, new[] { gun }, new Vector2(48, 18)); Steps(g, 2);
                bool refused = gun.Order != UnitOrder.GroundFire;
                g.GroundFire(1, new[] { gun }, new Vector2(48, 40)); Steps(g, 2);
                check(refused && gun.Order == UnitOrder.GroundFire, $"지점 포격: 구역 안 거절 {refused}, 구역 밖 수락 {gun.Order == UnitOrder.GroundFire}");
            }

            // ---- 4b. 후퇴 · 집결지 지정 · 참호 돌격 (구역 안 / 가로지르기 거절, 구역 밖 허용, 구역 안에서 나가는 후퇴 허용)
            {
                var r = Make(On);
                var m1 = Mine(r, 1);
                bool Moving() { Steps(r, 2); return m1.Order == UnitOrder.Move; }
                r.Retreat(1, new[] { m1 }, new Vector2(48, 18)); bool rIn = Moving(); r.Stop(1, new[] { m1 }); Steps(r, 2);
                r.Retreat(1, new[] { m1 }, new Vector2(80, 18)); bool rAcross = Moving(); r.Stop(1, new[] { m1 }); Steps(r, 2);
                r.Retreat(1, new[] { m1 }, new Vector2(30, 30)); bool rOut = Moving();
                m1.Pos = new Vector2(48, 18); r.Stop(1, new[] { m1 }); Steps(r, 2);
                r.Retreat(1, new[] { m1 }, new Vector2(48, 40)); bool rLeave = Moving();
                check(!rIn && !rAcross && rOut && rLeave, $"후퇴 명령: 구역 안 거절 {!rIn}, 가로지르기 거절 {!rAcross}, 밖 허용 {rOut}, 구역에서 나가기 허용 {rLeave}");

                const string ForceA = "{ \"id\": \"a\", \"faction\": 1, \"at\": [10, 18], \"units\": { \"infantry\": 1 }, \"company\": false }";
                var rc = World.CreateMissionWorld(MissionDef.Parse(Json(On).Replace(ForceA, "{ \"id\": \"a\", \"faction\": 1, \"at\": [10, 18], \"companies\": [ { \"units\": { \"officer\": 1, \"infantry\": 8 } } ] }"), "t_neutral"), Defs.Current);
                var co = rc.Units.FirstOrDefault(u => u.Owner == 1 && u.Company != null)?.Company;
                if (co != null)
                {
                    rc.SetRally(1, new[] { co.Id }, new Vector2(48, 18)); Steps(rc, 2); bool inRej = !co.HasRally;
                    rc.SetRally(1, new[] { co.Id }, new Vector2(80, 18)); Steps(rc, 2); bool acrossRej = !co.HasRally;
                    int n0 = rc.Notices.Count(n => n.Result == CommandResult.NotAllowed);
                    rc.Issue(new TrenchAssaultCommand(1, new[] { co.Id }, new Vector2(48, 18), true)); Steps(rc, 2);   // 시험 지도에 참호가 없어 World.TrenchAssault 래퍼는 못 쓴다 — 명령을 직접 넣는다
                    bool taRej = rc.Notices.Count(n => n.Result == CommandResult.NotAllowed) == n0 + 1 && !rc.TrenchAssaultActive(co);
                    rc.SetRally(1, new[] { co.Id }, new Vector2(30, 30)); Steps(rc, 2); bool okSet = co.HasRally;
                    check(inRej && acrossRej && okSet && taRej, $"집결지 지정: 구역 안 {inRej} · 가로지르기 {acrossRej} 거절, 밖 허용 {okSet}; 참호 돌격 구역 안 거절 {taRej}");
                }
                else check(false, "집결지 시험: 중대 생성 실패");
                var offw = Make("");
                var o1 = Mine(offw, 1);
                offw.Retreat(1, new[] { o1 }, new Vector2(48, 18)); Steps(offw, 2);
                check(o1.Order == UnitOrder.Move && offw.Notices.Count == 0, "기능 꺼짐: 후퇴 거절 없음 (결과 불변)");
            }

            // ---- 5. 기능 꺼짐 (neutralZones 없음) = 거절 없음
            {
                var off = Make("");
                var o = Mine(off, 1);
                off.Move(1, new[] { o }, new Vector2(48, 18)); Steps(off, 2);
                check(o.Order == UnitOrder.Move && off.Notices.Count == 0, "기능 꺼짐: 구역이 있어도 neutralZones 가 없으면 이동 거절 없음");
            }

            // ---- 6. 저장 왕복 + 결정성
            {
                var a = Make(On); var b = Make(On);
                var meta = new SnapshotMeta("neutral", 1, false, "none");
                var l = Snapshot.Load(Snapshot.Save(a, meta), Defs.Current).World;
                a.Move(1, new[] { Mine(a, 1) }, new Vector2(80, 18)); b.Move(1, new[] { Mine(b, 1) }, new Vector2(80, 18));
                l.Move(1, new[] { Mine(l, 1) }, new Vector2(80, 18));
                bool same = true;
                for (int i = 0; i < 300; i++) { a.Step(); b.Step(); l.Step(); same &= a.ComputeChecksum() == b.ComputeChecksum() && a.ComputeChecksum() == l.ComputeChecksum(); }
                check(same && Mine(l, 1).Order != UnitOrder.Move && l.Mission!.Def.NeutralZones.Count == 1, "저장 → 불러오기 후에도 같은 거절 · 체크섬 동일 (미션 텍스트로 복원)");
            }
        }
        catch (Exception e) { check(false, $"중립 구역 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }
}
