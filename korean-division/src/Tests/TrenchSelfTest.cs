using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>참호 지형 자동 점유 (Trenches.cs) · 포탄 구덩이 지형. SelfTest 가 호출, 단독 KD_SELFTEST_ONLY=trench.</summary>
public static class TrenchSelfTest
{
    // 풀밭 64², 참호선 A = x 30 (y 10..50), 참호선 B = x 40 (y 10..50, A 와 떨어짐), 가옥 1칸 (30,5) · 물 1칸 (31,5) · 참호 1칸 (32,5)
    const string Map = """
    { "name": "trench test", "width": 64, "height": 64, "base": "grass",
      "starts": [ [6, 6], [58, 58] ],
      "paint": [
        { "terrain": "trench", "rect": [30, 10, 1, 41] },
        { "terrain": "trench", "rect": [40, 10, 1, 41] },
        { "terrain": "house", "tiles": [[30, 5]] },
        { "terrain": "water", "tiles": [[31, 5]] },
        { "terrain": "trench", "tiles": [[32, 5]] }
      ] }
    """;

    static World Fresh(int seed)
    {
        var w = World.CreateStandard(MapFile.LoadText(Map, "trenchtest", seed), Defs.Current);
        foreach (var u in w.Units) u.Hp = 0;
        w.Step();
        return w;
    }

    static Vector2 C(int x, int y) => new(x + 0.5f, y + 0.5f);
    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static bool OnTrench(World w, Unit u) { var (x, y) = GameMap.TileOf(u.Pos); return w.Map.IsTrench(x, y); }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var tr = defs.Terrain;
        var inf = defs.Units["infantry"];

        // ---- 묶음 · 자리
        {
            var w = Fresh(opt.Seed);
            int a = w.TrenchCompAt(30, 20), b = w.TrenchCompAt(40, 20);
            check(a >= 0 && b >= 0 && a != b && w.TrenchCompAt(30, 49) == a && w.TrenchCompAt(31, 20) == -1, $"참호 묶음: 4방향 연결 (A {a} · B {b} 따로, 풀밭 -1)");
            check(!defs.Units["cavalry_default"].CanOccupyTrench && !defs.Units["infantry_scout"].CanOccupyTrench && inf.CanOccupyTrench && !defs.Units["tank_ft_male"].CanOccupyTrench,
                  "참호 점유: 보병만 (기병 · 정찰병 · 차량 제외)");
        }

        // ---- 이동 중 참호를 만나면 들어가 사수 → 재명령이면 해제 · 방금 나온 참호는 다시 안 붙잡음 → 다른 참호는 붙잡음
        {
            var w = Fresh(opt.Seed);
            var u = w.SpawnUnit(inf, 1, C(22, 30));
            w.Move(1, new[] { u }, C(36, 30));
            Steps(w, 60 * 5);
            check(u.InTrench && u.Order == UnitOrder.Hold && OnTrench(w, u) && GameMap.TileOf(u.Pos).X == 30, $"이동 중 참호 A 를 만나 들어가 사수 (자리 {u.Pos.X:0.00},{u.Pos.Y:0.00}, 명령 {u.Order})");
            w.Move(1, new[] { u }, C(36, 30));
            Steps(w, 60 * 4);
            check(!u.InTrench && Vector2.Distance(u.Pos, C(36, 30)) < 0.6f, $"재명령 → 해제 후 이동, 같은 참호에 다시 붙잡히지 않음 ({u.Pos.X:0.0},{u.Pos.Y:0.0})");
            w.Move(1, new[] { u }, C(46, 30));
            Steps(w, 60 * 4);
            check(u.InTrench && GameMap.TileOf(u.Pos).X == 40, $"다른 참호 B 는 붙잡음 ({u.Pos.X:0.0},{u.Pos.Y:0.0})");
            w.Hold(1, new[] { u });
            Steps(w, 30);
            check(u.InTrench && u.Order == UnitOrder.Hold, "사수 명령은 참호 자리를 유지");
            w.Stop(1, new[] { u });
            Steps(w, 2);
            check(!u.InTrench && !u.HasTrenchSpot, "정지 명령 → 참호 해제");
        }

        // ---- 공격 이동은 참호에 잡히지 않고 지나간다 (한 명 · 진형 모두)
        {
            var w = Fresh(opt.Seed);
            var u = w.SpawnUnit(inf, 1, C(22, 30));
            w.AttackMove(1, new[] { u }, C(36, 30));
            Steps(w, 60 * 5);
            check(!u.InTrench && !u.HasTrenchSpot && u.Pos.X > 33f, $"공격 이동은 참호 A 를 지나간다 ({u.Pos.X:0.0},{u.Pos.Y:0.0}, 참호 {u.InTrench})");
            var w2 = Fresh(opt.Seed);
            var us = new List<Unit>();
            for (int i = 0; i < 8; i++) us.Add(w2.SpawnUnit(inf, 1, C(22 + i % 2, 26 + i)));
            w2.AttackMove(1, us, C(36, 30), new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, new Vector2(1, 0)));
            Steps(w2, 2);
            int claimed = us.Count(x => x.HasTrenchSpot);
            Steps(w2, 60 * 8);
            check(claimed == 0 && us.Count(x => x.InTrench) == 0, $"진형 공격 이동도 참호 슬롯 배정 없음 (배정 {claimed}, 참호 안 {us.Count(x => x.InTrench)})");
        }

        // ---- 진형 이동: 슬롯이 참호 자리로 붙어 참호선에 한 번에 선다 (칸당 자리 수)
        {
            var w = Fresh(opt.Seed);
            var us = new List<Unit>();
            for (int i = 0; i < 16; i++) us.Add(w.SpawnUnit(inf, 1, C(18 + i % 4, 24 + i / 4 * 3)));
            w.Move(1, us, C(30, 30), new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, new Vector2(1, 0)));
            Steps(w, 2);
            int claimed = us.Count(u => u.HasTrenchSpot);
            Steps(w, 60 * 10);
            int inT = us.Count(u => u.InTrench && OnTrench(w, u));
            var spots = us.Where(u => u.InTrench).Select(u => (MathF.Round(u.Pos.X * 4), MathF.Round(u.Pos.Y * 4))).Distinct().Count();
            check(claimed >= 12 && inT >= 14 && spots == us.Count(u => u.InTrench), $"횡대 이동 → 참호선 자리 배정 {claimed}/16, 도착 후 참호 안 {inT}/16 (자리 겹침 없음)");
            var perTile = us.Where(u => u.InTrench).GroupBy(u => GameMap.TileOf(u.Pos)).Max(g => g.Count());
            check(perTile <= Math.Max(1, tr.TrenchPerTile), $"칸당 최대 {perTile}명 (trenchPerTile {tr.TrenchPerTile})");
            // 같은 참호선을 따라 옆으로 옮기는 이동도 참호에 선다 (도착 지점이 참호면 무시 묶음 규칙 없음)
            w.Move(1, us, C(30, 40), new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, new Vector2(1, 0)));
            Steps(w, 60 * 8);
            int inT2 = us.Count(u => u.InTrench && OnTrench(w, u) && u.Pos.Y > 32);
            check(inT2 >= 12, $"같은 참호선 옆으로 이동 → 다시 참호 안 {inT2}/16");
        }

        // ---- 한 명 이동: 참호 근처를 찍으면 빈 자리로
        {
            var w = Fresh(opt.Seed);
            var u = w.SpawnUnit(inf, 1, C(20, 20));
            w.Move(1, new[] { u }, new Vector2(31.6f, 20.5f));   // 참호 칸 옆 풀밭
            Steps(w, 60 * 5);
            check(u.InTrench && OnTrench(w, u), $"참호 근처 지점 이동 → 참호 자리로 ({u.Pos.X:0.00},{u.Pos.Y:0.00})");
        }

        // ---- 기병 · 돌격은 붙잡히지 않음, 참호 칸 위 · 자리로 가는 보병은 행진 사격 안 함
        {
            var w = Fresh(opt.Seed);
            var cav = w.SpawnUnit(defs.Units["cavalry_default"], 1, C(22, 20));
            var ch = w.SpawnUnit(inf, 1, C(22, 36));
            w.Move(1, new[] { cav }, C(36, 20));
            w.Charge(1, new[] { ch }, C(36, 36));
            Steps(w, 60 * 6);
            check(!cav.InTrench && cav.Pos.X > 33 && !ch.InTrench && ch.Pos.X > 33, $"기병 이동 · 보병 돌격은 참호를 지나간다 (기병 x {cav.Pos.X:0.0}, 돌격 x {ch.Pos.X:0.0})");

            var w2 = Fresh(opt.Seed);
            var m = w2.SpawnUnit(inf, 1, C(24, 30));
            var foe = w2.SpawnUnit(inf, 2, C(36, 30)); foe.Weapon = null; foe.Hp = 100000;
            w2.Move(1, new[] { m }, C(38, 30));
            int bad = 0, onTrenchWalk = 0;
            for (int i = 0; i < 60 * 5; i++)
            {
                w2.Step();
                bool tr2 = m.HasTrenchSpot || OnTrench(w2, m);
                if (m.Walking && tr2) { onTrenchWalk++; if (m.LastShotTick == w2.Tick - 1) bad++; }
            }
            check(onTrenchWalk > 0 && bad == 0 && m.InTrench, $"참호로 들어가는 동안 행진 사격 없음 (참호 위 걸음 {onTrenchWalk}틱, 사격 {bad})");
        }

        // ---- 저장 · 불러오기 (참호로 가는 중 · 들어간 상태) · 동등성
        SnapshotSelfTest.Equivalence("참호 자동 점유: 가는 중 · 사수 중 저장", () =>
        {
            var w = Fresh(opt.Seed);
            var us = new List<Unit>();
            for (int i = 0; i < 10; i++) us.Add(w.SpawnUnit(inf, 1, C(20 + i % 2, 22 + i)));
            w.Move(1, us, C(30, 27), new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, new Vector2(1, 0)));
            var solo = w.SpawnUnit(inf, 1, C(22, 45));
            w.Move(1, new[] { solo }, C(36, 45));
            w.TryCrater(us[0], C(50, 10), new WeaponDef { CraterChance = 1f });
            return w;
        }, 60 * 2, 60 * 10, (w, t) => { if (t == 60 * 6) w.Move(1, w.Units.Where(u => u.Owner == 1).Take(3), C(36, 15)); }, check);

        // ---- 리플레이 결정성: 기록한 명령만으로 같은 결과
        {
            World Make()
            {
                var w = Fresh(opt.Seed);
                for (int i = 0; i < 8; i++) w.SpawnUnit(inf, 1, C(20, 24 + i));
                return w;
            }
            var a = Make();
            var mine = a.Units.Where(u => u.Owner == 1).ToList();
            a.Move(1, mine, C(30, 28), new FormSpec((int)FormType.Line, (int)FormOrder.Mixed, new Vector2(1, 0)));
            Steps(a, 60 * 8);
            a.Move(1, mine, C(46, 28));
            Steps(a, 60 * 8);
            var b = Make();
            var log = a.CommandLog.ToList();
            b.CommandFeed = t => log.Where(r => r.Tick == t).Select(r => r.Command);
            Steps(b, 60 * 16);
            check(a.ComputeChecksum() == b.ComputeChecksum() && a.Units.Count(u => u.InTrench) == b.Units.Count(u => u.InTrench) && a.Units.Any(u => u.InTrench),
                  $"리플레이: 같은 체크섬 · 같은 참호 점유 ({a.Units.Count(u => u.InTrench)}명)");
        }

        // ---- 포탄 구덩이
        {
            var w = Fresh(opt.Seed);
            var shooter = w.SpawnUnit(inf, 1, C(5, 5));
            var always = new WeaponDef { CraterChance = 1f };
            int e0 = w.Map.TerrainEditCount;
            w.TryCrater(shooter, C(20, 8), always);
            check(w.Map.Get(20, 8) == TerrainType.Crater && w.Map.TerrainEditCount == e0 + 1, "착탄 (확률 1): 풀밭 칸 → 포탄 구덩이");
            w.TryCrater(shooter, C(20, 8), always);
            check(w.Map.TerrainEditCount == e0 + 1, "구덩이는 겹치지 않음");
            w.TryCrater(shooter, C(30, 5), always); w.TryCrater(shooter, C(31, 5), always); w.TryCrater(shooter, C(32, 5), always);
            check(w.Map.Get(30, 5) == TerrainType.House && w.Map.Get(31, 5) == TerrainType.Water && w.Map.Get(32, 5) == TerrainType.Trench, "가옥 · 물 · 참호는 구덩이가 되지 않음");
            w.TryCrater(shooter, C(22, 8), new WeaponDef { CraterChance = 0f });
            check(w.Map.Get(22, 8) == TerrainType.Grass, "확률 0 무기 (소총 등) 는 구덩이 없음");
            var cov = w.Map.TerrainCover(C(20, 8));
            check(MathF.Abs(cov.Hit - tr.CraterHit) < 1e-5f && MathF.Abs(cov.Suppress - tr.CraterSuppress) < 1e-5f && MathF.Abs(w.Map.MoveMul(C(20, 8)) - tr.CraterMove) < 1e-5f,
                  $"구덩이: 명중 ×{tr.CraterHit} · 제압 ×{tr.CraterSuppress} · 이동 ×{tr.CraterMove}");
            var tcov = w.Map.TerrainCover(C(30, 20));
            check(MathF.Abs(tcov.Hit - tr.TrenchHit) < 1e-5f && MathF.Abs(tcov.Suppress - tr.TrenchSuppress) < 1e-5f, $"참호 지형: 명중 ×{tr.TrenchHit} · 제압 ×{tr.TrenchSuppress} (누구나)");
            // 확률 0.5 근처: 여러 칸에 쏘면 일부만 (결정적 Roll)
            var half = new WeaponDef { CraterChance = 0.5f };
            int made = 0;
            for (int k = 0; k < 40; k++) { w.TryCrater(shooter, C(5 + k, 58), half); if (w.Map.Get(5 + k, 58) == TerrainType.Crater) made++; w.Step(); }
            check(made > 5 && made < 35, $"확률 0.5: 40발 중 {made}칸 구덩이");
            var hz = defs.Weapon("howitzer_heavy");
            check(hz != null && hz.CraterChance > (defs.Weapon("gun_infantry")?.CraterChance ?? 1f), $"balance.json craterChance: 곡사포 {hz?.CraterChance} > 보병포 {defs.Weapon("gun_infantry")?.CraterChance}");
        }
    }
}
