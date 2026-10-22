using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T30b 장갑열차 무기 · 객차 피해: 포차(야전포) · 기관총차(중기관총 2정) 사격, 객차 HP · 장갑 (장갑차 규칙), 잔해, 기관차 파괴 → 정지,
/// 유닛 표적 = 객차 저장 왕복, 결정성, 소규모 교전 (보병 중대 vs 장갑열차, 시드 8개 — 승패 비율만 보고). KD_SELFTEST_ONLY=trainweapons</summary>
public static class TrainWeaponsSelfTest
{
    // 100×48: 직선 선로 y=20 (x 2..97). 세력 1 = 열차, 세력 2 = 보병. 안개 켬 (객차 시야 검사).
    static string Json(string trains, int seed = 7, string forces = "") => $$"""
    { "id": "t_trainw", "title": "T", "player": 1, "seed": {{seed}},
      "factions": [ { "name": "A", "team": 1, "posture": "hold" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "trainw", "width": 100, "height": 48, "base": "grass", "starts": [ [5, 45], [95, 45] ],
        "paint": [ { "terrain": "rail", "rect": [2, 20, 96, 1] } ] },
      "forces": [ { "id": "a", "faction": 1, "at": [4, 45], "units": { "infantry": 1 }, "company": false },
                  { "id": "b", "faction": 2, "at": [96, 45], "units": { "infantry": 1 }, "company": false }{{forces}} ],
      "trains": [ {{trains}} ],
      "objectives": [ { "id": "o", "type": "defend", "seconds": 9999 } ] }
    """;

    const string Armored = """{ "tag": "ar", "faction": 1, "cars": ["locomotive", "guncar", "mgcar", "wagon"], "path": [[30, 20], [70, 20]], "script": false }""";
    const string Plain = """{ "tag": "pl", "faction": 1, "cars": ["locomotive", "wagon", "wagon"], "path": [[30, 20], [70, 20]], "script": false }""";

    static World Make(string trains, int seed = 7, string forces = "") =>
        World.CreateMissionWorld(MissionDef.Parse(Json(trains, seed, forces), "t_trainw"), Defs.Current);

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var r = defs.Trains;

        // ---- 1. 정의: 포차 = gun_field, 기관총차 = hmg ×2, 병과 armored, 기관차 engine
        {
            var w = Make(Armored);
            var t = w.Trains[0];
            check(t.Cars[1].Guns.Length == 1 && t.Cars[1].Guns[0].W.Key == "gun_field" && t.Cars[2].Guns.Length == 2 && t.Cars[2].Guns.All(g => g.W.Key == "hmg")
                  && t.Cars[0].Guns.Length == 0 && t.Cars[0].Def.Engine && t.Cars.All(c => c.Def.Category == "armored"),
                  "객차 무기: 포차 = 야전포 1문, 기관총차 = 중기관총 2정 (기존 무기 정의), 기관차 = engine, 병과 armored");
            bool threw = false;
            try { var w2 = Make(Plain); defs.Trains.Cars["wagon"].Weapons.Add("no_such_gun"); w2.SpawnTrain(MissionDef.Parse(Json(Plain), "t").Trains[0]); }
            catch (InvalidDataException) { threw = true; }
            finally { defs.Trains.Cars["wagon"].Weapons.Remove("no_such_gun"); }
            check(threw && defs.Trains.Cars["wagon"].Weapons.Count == 0, "모르는 객차 무기 → InvalidDataException");
        }

        // ---- 2. 장갑 판정 = 장갑차 규칙: 소총 면역 (표적 안 잡음), 대전차소총 1발 = 10×3.5 − 장갑, 같은 공식으로 tank_armored_car 와 일치
        {
            var w = Make(Plain);
            var t = w.Trains[0];
            var rifles = new List<Unit>();
            for (int i = 0; i < 6; i++) rifles.Add(w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(44.5f + i, 26.5f)));
            bool targeted = false;
            for (int i = 0; i < 60 * 30; i++) { w.Step(); targeted |= rifles.Any(u => u.Target is TrainCar); }
            check(t.Cars.All(c => c.Hp == c.MaxHp) && !targeted, $"소총 보병 6명 30초: 객차 피해 0 · 객차를 표적으로 잡지 않음 (bonus armored 0)");

            var w3 = Make(Plain);
            var t3 = w3.Trains[0];
            var at = w3.SpawnUnit(defs.Units["heavy_at_rifleman"], 2, new Vector2(46.5f, 30.5f));
            float hit1 = 0; bool sawCarTarget = false;
            for (int i = 0; i < 60 * 90 && hit1 == 0; i++)
            {
                w3.Step();
                sawCarTarget |= at.Target is TrainCar;
                foreach (var c in t3.Cars) if (c.Hp < c.MaxHp) { hit1 = c.MaxHp - c.Hp; break; }
            }
            var atw = defs.Weapons["at_rifle"];
            var hitCar = t3.Cars.First(c => c.Hp < c.MaxHp);
            float expect = MathF.Max(1, atw.Damage * atw.BonusAgainst("armored") - hitCar.ArmorValue);
            check(sawCarTarget && MathF.Abs(hit1 - expect) < 0.01f, $"대전차소총 → 객차 ({hitCar.Name}, 장갑 {hitCar.ArmorValue}): 1발 피해 {hit1:0.0} = 10×3.5 − 장갑 = {expect:0.0}");
            // 같은 피해량을 장갑차 · 같은 장갑의 객차에 주면 같은 공식 (Damage: 장갑만큼 감소, 최소 1)
            var car = w3.SpawnUnit(defs.Units["tank_armored_car"], 1, new Vector2(80.5f, 40.5f));
            var wagon = t3.Cars[2];
            float c0 = car.Hp, g0 = wagon.Hp, amt = atw.Damage * atw.BonusAgainst(World.CategoryOf(car));
            w3.Damage(car, amt, null); w3.Damage(wagon, atw.Damage * atw.BonusAgainst(World.CategoryOf(wagon)), null);
            check(car.ArmorValue == wagon.ArmorValue && MathF.Abs((c0 - car.Hp) - (g0 - wagon.Hp)) < 0.001f,
                  $"장갑차(장갑 {car.ArmorValue}) vs 화차(장갑 {wagon.ArmorValue}): 대전차소총 피해 {c0 - car.Hp:0.0} = {g0 - wagon.Hp:0.0}");
            // 근접 불가 (장갑)
            check(!World.CanMelee(w3.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60.5f, 40.5f)), wagon), "보병 착검 근접 → 객차 불가 (장갑 병과)");
        }

        // ---- 3. 열차 사격: 기관총차 · 포차가 안개 속 자기 시야로 적 보병을 잡아 쏜다 (객차 Shots · 포탄), 적 보병 피해
        {
            var w = Make(Armored);
            var t = w.Trains[0];
            var foes = new List<Unit>();
            for (int i = 0; i < 8; i++) foes.Add(w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(46.5f + i, 34.5f)));   // 선로에서 14칸
            int mgShots = 0, gunImpacts = 0;
            for (int i = 0; i < 60 * 40; i++)
            {
                w.Step();
                foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == t.Cars[2].Id) mgShots++;
                foreach (var s in w.Impacts) if (s.Tick == w.Tick - 1 && s.ShooterId == t.Cars[1].Id) gunImpacts++;
            }
            int dead = foes.Count(u => !u.Alive);
            float hurt = foes.Sum(u => u.MaxHp - MathF.Max(0, u.Hp));
            check(w.Fog.Enabled && mgShots > 20 && gunImpacts > 0 && hurt > 0, $"객차 사격 40초 (안개 켬 — 객차 시야): 기관총차 사격 {mgShots}발 · 포차 착탄 {gunImpacts} · 적 보병 피해 {hurt:0} (사망 {dead}/8)");
        }

        // ---- 4. 객차 파괴: 포차 HP 0 → 잔해 (표적 · 사격에서 빠짐, 선로 위 끌려감), 기관차 HP 0 → 열차 전체 정지 · 명령 무시
        {
            var w = Make(Armored);
            var t = w.Trains[0];
            w.TrainMove(1, t.Id, 1);
            for (int i = 0; i < 300; i++) w.Step();
            var gun = t.Cars[1];
            w.Damage(gun, 100000, null);
            long s0 = t.S;
            for (int i = 0; i < 120; i++) w.Step();
            check(gun.Wreck && !t.Stalled && t.S > s0 && w.Map.Get((int)gun.Pos.X, (int)gun.Pos.Y) == TerrainType.Rail,
                  $"포차 파괴: 잔해로 선로 위에 남아 끌려감 (열차 계속 이동, S {s0}→{t.S})");
            var foe = w.SpawnUnit(defs.Units["heavy_at_rifleman"], 2, new Vector2(gun.Pos.X, 26.5f));
            var pick = w.FindTarget(2, foe.Pos, 30f, 0, null, 0, 0f, defs.Weapons["at_rifle"]);
            check(pick is not TrainCar { Wreck: true } && w.Find(gun.Id) == null, $"잔해는 표적 아님 (FindTarget → {pick?.Name ?? "없음"}), Find(잔해) = null");
            int wreckShots = 0;
            for (int i = 0; i < 600; i++) { w.Step(); foreach (var s in w.Impacts) if (s.Tick == w.Tick - 1 && s.ShooterId == gun.Id) wreckShots++; }
            check(wreckShots == 0 && gun.Guns[0].TargetId == 0, $"잔해 포차는 쏘지 않음 (적 대전차소총수 옆, 10초 착탄 {wreckShots})");
            w.Damage(t.Cars[0], 100000, null);
            for (int i = 0; i < 400; i++) w.Step();
            long s1 = t.S;
            w.TrainMove(1, t.Id, -1);
            for (int i = 0; i < 300; i++) w.Step();
            check(t.Stalled && t.V == 0 && t.S == s1, $"기관차 파괴: 제동 뒤 정지 · 후진 명령에도 움직이지 않음 (V={t.V})");
            check(t.Cars[2].Alive && t.Cars[2].Guns.Length == 2, "기관차 파괴 뒤에도 살아 있는 기관총차는 그대로 (사격 가능)");
        }

        // ---- 5. 저장 → 불러오기: 교전 중 (유닛 표적 = 객차, 객차 무기 표적 · 냉각 · 조준, 잔해 1량) 바이트 동일 + 이어 돌린 체크섬 동일
        {
            var w = Make(Armored);
            var t = w.Trains[0];
            var foes = new List<Unit>();
            for (int i = 0; i < 4; i++) foes.Add(w.SpawnUnit(defs.Units["heavy_at_rifleman"], 2, new Vector2(46.5f + 2 * i, 30.5f)));
            for (int i = 0; i < 4; i++) foes.Add(w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(46.5f + 2 * i, 36.5f)));
            w.Damage(t.Cars[3], 100000, null);   // 화차 잔해
            w.Issue(new AttackCommand(2, foes.Skip(4).Select(u => u.Id).ToArray(), t.Cars[1].Id));   // T36: 소총병은 스스로 객차를 노리지 않음 → 플레이어 명령으로 유닛→객차 표적 참조를 만든다
            int k = 0;
            for (; k < 60 * 60; k++) { w.Step(); if (k > 300 && foes.Any(u => u.Alive && u.Target is TrainCar) && t.Cars.Any(c => c.Guns.Any(g => g.TargetId != 0 && g.Cooldown > 0))) break; }
            bool refCar = foes.Any(u => u.Alive && u.Target is TrainCar);
            var meta = new SnapshotMeta("trainw", 1, false, "none");
            var bytes = Snapshot.Save(w, meta);
            var l = Snapshot.Load(bytes, defs).World;
            var again = Snapshot.Save(l, meta);
            var lt = l.Trains[0];
            check(refCar && l.ComputeChecksum() == w.ComputeChecksum() && lt.Cars[3].Wreck
                  && lt.Cars.Zip(t.Cars).All(p => p.First.Hp == p.Second.Hp && p.First.Guns.Zip(p.Second.Guns).All(g => g.First.TargetId == g.Second.TargetId && g.First.Cooldown == g.Second.Cooldown && g.First.AimLeft == g.Second.AimLeft))
                  && l.Units.Any(u => u.Target is TrainCar tc && lt.Cars.Contains(tc)),
                  $"불러오기 ({k}틱 교전 중): 객차 HP · 무기 상태 · 잔해 · 유닛→객차 표적 참조 복원, 체크섬 같음");
            check(Snapshot.RawBody(bytes).AsSpan().SequenceEqual(Snapshot.RawBody(again)), "저장 → 불러오기 → 저장 본문 바이트 동일 (열차 무기)");
            bool same = true;
            for (int i = 0; i < 3000; i++) { w.Step(); l.Step(); if (i % 100 == 0) same &= w.ComputeChecksum() == l.ComputeChecksum(); }
            check(same && w.ComputeChecksum() == l.ComputeChecksum(), "불러온 월드를 이어 돌려도 체크섬 동일 (교전 3000틱)");
            ulong h = w.ComputeChecksum();
            w.Trains[0].Cars[2].Guns[0].Cooldown += 1;
            check(w.ComputeChecksum() != h, "체크섬에 객차 무기 상태 포함");
        }

        // ---- 6. 결정성: 같은 교전 2회 체크섬 동일
        {
            string forces = """, { "id": "c", "faction": 2, "at": [50, 34], "units": { "officer": 1, "infantry": 24, "heavy_at_rifleman": 4 } }""";
            var a = Make(Armored, 8, forces); var b = Make(Armored, 8, forces);
            bool same = true;
            for (int i = 0; i < 60 * 60; i++) { a.Step(); b.Step(); if (i % 300 == 0) same &= a.ComputeChecksum() == b.ComputeChecksum(); }
            check(same && a.ComputeChecksum() == b.ComputeChecksum(), "같은 시드 교전 2회: 체크섬 동일 (열차 사격 포함)");
        }

        // ---- 8. T36 면역 표적: 소총 보병은 객차에게 맞아도 (반격) 객차를 표적으로 잡지 않음, 플레이어 공격 명령은 그대로 유지, 대전차소총은 반격
        {
            var w = Make(Armored);
            var riflemen = new List<Unit>();
            for (int i = 0; i < 6; i++) riflemen.Add(w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(46.5f + i, 33.5f)));
            bool rifleTargetedCar = false;
            for (int i = 0; i < 60 * 25; i++) { w.Step(); rifleTargetedCar |= riflemen.Any(u => u.Alive && u.Target is TrainCar); }
            check(!rifleTargetedCar && riflemen.Any(u => u.Hp < u.MaxHp), "T36: 객차에게 맞는 소총 보병은 객차를 표적으로 잡지 않음 (반격 · 자동 표적 모두 면역 표적 제외)");

            var w2 = Make(Armored);
            var rf = w2.SpawnUnit(defs.Units["infantry"], 2, new Vector2(46.5f, 33.5f));
            w2.Issue(new AttackCommand(2, new[] { rf.Id }, w2.Trains[0].Cars[1].Id));
            w2.Step();
            check(rf.Order == UnitOrder.Attack && rf.Target is TrainCar, "T36: 플레이어 공격 명령은 면역 표적이어도 그대로 (명령 유지)");

            var w3 = Make(Armored);
            var at = w3.SpawnUnit(defs.Units["heavy_at_rifleman"], 2, new Vector2(46.5f, 33.5f));
            bool atTargetedCar = false;
            for (int i = 0; i < 60 * 25; i++) { w3.Step(); atTargetedCar |= at.Alive && at.Target is TrainCar; }
            check(atTargetedCar || !at.Alive, "T36: 대전차소총은 객차를 표적으로 잡음 (대조)");
        }

        // ---- 7. 소규모 교전 (측정 · 보고용): 보병 중대 (장교 1 · 소총 24 · 대전차소총 4) 가 장갑열차로 공격 이동, 시드 8개 × 3분
        {
            string forces = """, { "id": "c", "faction": 2, "at": [50, 40], "units": { "officer": 1, "infantry": 24, "heavy_at_rifleman": 4 } }""";
            int trainWins = 0, infWins = 0, draws = 0;
            var lines = new List<string>();
            for (int seed = 1; seed <= 8; seed++)
            {
                var w = Make(Armored, seed, forces);
                var t = w.Trains[0];
                var inf = w.Units.Where(u => u.Owner == 2 && Vector2.Distance(u.Pos, new Vector2(50, 40)) < 15).ToList();
                w.Issue(new AttackMoveCommand(2, inf.Select(u => u.Id).ToArray(), new Vector2(50.5f, 21.5f)));
                for (int i = 0; i < 60 * 180; i++) w.Step();
                int alive = inf.Count(u => u.Alive);
                bool armedDead = t.Cars.Where(c => c.Guns.Length > 0).All(c => c.Wreck);
                if (armedDead) infWins++;
                else if (alive <= inf.Count / 3) trainWins++;
                else draws++;
                lines.Add($"s{seed}:{alive}/{inf.Count}{(armedDead ? "!" : "")}{(t.Stalled ? "S" : "")}");
            }
            Console.WriteLine($"  [측정] 보병 중대 vs 장갑열차 (시드 8개 × 3분): 열차 승 {trainWins} · 보병 승 {infWins} · 미결 {draws}  ({string.Join(" ", lines)}; ! = 무장 객차 전멸, S = 기관차 파괴)");
            check(trainWins + infWins + draws == 8, "소규모 교전 8판 (보고용)");
        }
    }
}
