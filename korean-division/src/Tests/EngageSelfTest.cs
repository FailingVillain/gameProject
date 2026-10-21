using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 교전 거리 · 엄폐 (engage, 2026-10-19): 반응 거리 (사거리 × engageRangeFrac) · 반응 거리 밖 총격에 반격 · 진지 점유 (참호 · 엄폐 칸) ·
/// 피격 시 엄폐 찾기 · 참호 · 숲 명중 배수. SelfTest 가 호출, 단독 KD_SELFTEST_ONLY=engage.
/// </summary>
public static class EngageSelfTest
{
    // 풀밭 96×48: 참호 2줄 (x 40..63, y 10..11), 숲 3×3 (x 10..12, y 30..32), 참호 1칸 (70, 30)
    const string Map = """
    { "name": "engage test", "width": 96, "height": 48, "base": "grass",
      "starts": [ [4, 4], [92, 44] ],
      "paint": [
        { "terrain": "trench", "rect": [40, 10, 24, 2] },
        { "terrain": "forest", "rect": [10, 30, 3, 3] },
        { "terrain": "trench", "tiles": [[70, 30]] }
      ] }
    """;

    static World Fresh(int seed)
    {
        var w = World.CreateStandard(MapFile.LoadText(Map, "engagetest", seed), Defs.Current);
        foreach (var u in w.Units) u.Hp = 0;
        w.Step();
        return w;
    }

    static Vector2 C(float x, float y) => new(x + 0.5f, y + 0.5f);
    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var c = defs.CombatRules;
        var inf = defs.Units["infantry"];
        var sniper = defs.Units["infantry_sniper"];
        var hmg = defs.Units["heavy_machinegunner"];

        // ---- 반응 거리 = 사거리 × frac (소총), 기관총 · 저격총은 사거리 그대로
        {
            var w = Fresh(opt.Seed);
            var a = w.SpawnUnit(inf, 1, C(20, 20));
            var m = w.SpawnUnit(hmg, 1, C(22, 20));
            var s = w.SpawnUnit(sniper, 1, C(24, 20));
            float R = a.Weapon!.Range;
            check(MathF.Abs(w.ReactRange(a) - R * MathF.Min(1f, c.EngageRangeFrac)) < 1e-4f && w.ReactRange(m) == m.Weapon!.Range && w.ReactRange(s) == s.Weapon!.Range,
                  $"반응 거리: 소총 {w.ReactRange(a):0.0} (= {R} × {c.EngageRangeFrac}), 기관총 {w.ReactRange(m):0.0}, 저격총 {w.ReactRange(s):0.0}");
        }

        float frac = c.EngageRangeFrac;
        float range = defs.Weapons[inf.Weapon].Range;
        if (frac < 0.95f)
        {
            float mid = (range * frac + range) * 0.5f;   // 반응 거리 밖 · 사거리 안
            // ---- 사수끼리 반응 거리 밖이면 쏘지 않는다 (사거리 끝 소모전 방지)
            {
                var w = Fresh(opt.Seed);
                var a = w.SpawnUnit(inf, 1, C(20, 30));
                var b = w.SpawnUnit(inf, 2, C(20 + mid, 30));
                w.Hold(1, new[] { a }); w.Hold(2, new[] { b });
                Steps(w, 60 * 5);
                check(a.Target == null && b.Target == null && a.LastShotTick < 0 && b.LastShotTick < 0, $"반응 거리 밖 ({mid:0.0}칸 > {range * frac:0.0}) 사수끼리는 쏘지 않음");
                // 반응 거리 안으로 들어오면 쏜다
                w.Move(2, new[] { b }, C(20 + range * frac - 2f, 30));
                Steps(w, 60 * 6);
                check(a.LastShotTick > 0, $"반응 거리 안 ({Vector2.Distance(a.Pos, b.Pos):0.0}칸) 으로 들어오면 사수가 쏜다");
            }
            // ---- 반격: 반응 거리 밖 (사거리 안) 에서 쏘는 적에게 사수 · 대기 모두 반격, 대기 유닛은 다가가지 않고 그 자리에서
            foreach (bool hold in new[] { true, false })
            {
                var w = Fresh(opt.Seed);
                var a = w.SpawnUnit(inf, 1, C(20, 30));
                a.Hp = 100000;
                var s = w.SpawnUnit(sniper, 2, C(20 + mid, 30));
                s.Hp = 100000;
                if (hold) w.Hold(1, new[] { a });
                w.Hold(2, new[] { s });
                Vector2 p0 = a.Pos;
                bool aimed = false;   // N34: 첫 사격 반응으로 저격수 발사 주기가 밀려 10초 시점에 은폐로 표적을 놓을 수 있다 — 도중에 저격수를 노렸는지 본다
                for (int i = 0; i < 60 * 10; i++) { w.Step(); aimed |= a.Target == s; }
                check(s.LastShotTick > 0 && a.LastShotTick > 0 && aimed && Vector2.Distance(a.Pos, p0) < 1.5f,
                      $"반격 ({(hold ? "사수" : "대기")}): {mid:0.0}칸 저격수에게 맞받아 쏨 (표적 {aimed}, 이동 {Vector2.Distance(a.Pos, p0):0.0}칸)");
            }
        }
        else check(true, "반응 거리 = 사거리 (frac ≥ 0.95) — 반응 거리 밖 시험 생략");

        // ---- 진지 점유: 참호 2줄 앞 지점 → 병사가 참호 칸에 들어가 사수
        {
            var w = Fresh(opt.Seed);
            var us = new List<Unit>();
            for (int i = 0; i < 30; i++) us.Add(w.SpawnUnit(inf, 1, C(46 + i % 10, 22 + i / 10)));
            us.Add(w.SpawnUnit(defs.Units["officer"], 1, C(50, 25)));
            w.OccupyPositions(1, us, C(51, 10), new Vector2(0, -1), 16f, 0.85f);
            Steps(w, 60 * 20);
            int inT = 0, onT = 0;
            foreach (var u in us)
            {
                if (u.IsOfficer) continue;
                if (u.InTrench && u.Order == UnitOrder.Hold) inT++;
                var (x, y) = GameMap.TileOf(u.Pos);
                if (w.Map.IsTrench(x, y)) onT++;
            }
            var off = us[^1];
            check(inT >= 28 && onT >= 28, $"진지 점유: 소총수 30명 중 참호 안 사수 {inT} · 참호 칸 위 {onT}");
            check(!off.InTrench && off.Order == UnitOrder.Hold && off.Pos.Y > 11.5f, $"진지 점유: 장교는 참호 뒤에서 사수 ({off.Pos.X:0.0},{off.Pos.Y:0.0}, {off.Order})");
        }
        // ---- 진지 점유: 참호가 없으면 엄폐 칸 (숲)
        {
            var w = Fresh(opt.Seed);
            var us = new List<Unit>();
            for (int i = 0; i < 12; i++) us.Add(w.SpawnUnit(inf, 1, C(16 + i % 4, 36 + i / 4)));
            w.OccupyPositions(1, us, C(11, 31), new Vector2(0, -1), 6f, 0.85f);
            Steps(w, 60 * 15);
            int cov = 0, hold = 0;
            foreach (var u in us) { if (w.Map.TerrainCover(u.Pos).Hit < 1f) cov++; if (u.Order == UnitOrder.Hold) hold++; }
            check(cov >= 10 && hold == us.Count, $"진지 점유 (엄폐 칸): 12명 중 숲 칸 {cov} · 사수 {hold}");
        }

        // ---- 피격 시 엄폐 찾기: 개활지 사수 → 가까운 숲 칸 / 참호 자리, 도착 뒤 사수
        foreach (bool trench in new[] { false, true })
        {
            var w = Fresh(opt.Seed);
            Vector2 start = trench ? C(70, 32) : C(11, 34.2f);   // 숲 (y ≤ 32) · 참호 (70,30) 2칸 남쪽
            var a = w.SpawnUnit(inf, 1, start);
            a.Hp = 100000;
            var s = w.SpawnUnit(sniper, 2, start + new Vector2(0, -26f));
            s.Hp = 100000;
            w.Hold(1, new[] { a }); w.Hold(2, new[] { s });
            long seeks0 = w.Stats.CoverSeeks;
            Steps(w, 60 * 12);
            bool ok = trench ? a.InTrench : w.Map.TerrainCover(a.Pos).Hit < 1f && a.Order == UnitOrder.Hold;
            check(ok && w.Stats.CoverSeeks > seeks0, $"엄폐 찾기 ({(trench ? "참호" : "숲")}): 피격 뒤 {(trench ? "참호 자리" : "숲 칸")}로 ({a.Pos.X:0.0},{a.Pos.Y:0.0}, 명령 {a.Order}, 참호 {a.InTrench})");
        }
        // ---- 피격 안 하면 엄폐 찾기 없음
        {
            var w = Fresh(opt.Seed);
            var a = w.SpawnUnit(inf, 1, C(11, 34.2f));
            w.Hold(1, new[] { a });
            Vector2 p0 = a.Pos;
            Steps(w, 60 * 5);
            check(Vector2.Distance(a.Pos, p0) < 0.2f && w.Stats.CoverSeeks == 0, "피격이 없으면 엄폐를 찾아 움직이지 않음");
        }

        // ---- 명중 배수: 참호 안 (InTrench) 표적 = ×trenchHit, 숲 = ×forestHit (참호 칸 점유 여부와 지형이 같은 값을 준다)
        {
            var w = Fresh(opt.Seed);
            var tr = w.SpawnUnit(inf, 1, C(52, 14));
            w.OccupyPositions(1, new[] { tr }, C(52, 11), new Vector2(0, -1), 4f, 0.85f);
            Steps(w, 60 * 6);
            var sh = w.SpawnUnit(inf, 2, C(51, 25));   // 측정만 (스텝 없이 HitChance)
            var open = w.SpawnUnit(inf, 1, C(51, 16));
            var fo = w.SpawnUnit(inf, 1, C(11, 31));
            float d = 11f;
            float pOpen = w.HitChance(sh, open, sh.Weapon!, d), pTr = w.HitChance(sh, tr, sh.Weapon!, d), pFo = w.HitChance(sh, fo, sh.Weapon!, d);
            check(tr.InTrench && MathF.Abs(pTr / pOpen - defs.Terrain.TrenchHit) < 1e-3f && MathF.Abs(pFo / pOpen - defs.Terrain.ForestHit) < 1e-3f,
                  $"명중 배수: 참호 안 {pTr / pOpen:0.00} (= {defs.Terrain.TrenchHit}), 숲 {pFo / pOpen:0.00} (= {defs.Terrain.ForestHit}), 참호 점유 {tr.InTrench}");
        }
    }
}
