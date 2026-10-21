using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 방향 변경 §0-1: 유닛별 탄약 회복 · 보급 마차 검증 (창 없이).
/// 사격 1회 = 탄약 1 · 0 이면 사격 불가 · 비전투 regenDelay 뒤 한도의 regenPct/초 · 피격(빗나감 포함)이 대기를 되돌림 ·
/// 아군 도시 근처 ×cityMul · 보급 마차 근처 ×wagonMul (적재량 감소 · 도시에서 재적재) · 참호 파기 시간 · 명중 × · 제압 × · 이동 시 해제 · 저장 동일성.
/// </summary>
public static class SupplySelfTest
{
    static Defs defs = null!;
    static Action<bool, string> check = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        defs = Defs.Current;
        check = chk;
        try
        {
            Ammo(opt);
            Regen(opt);
            Wagon(opt);
            SaveLoad(opt);
        }
        catch (Exception e)
        {
            check(false, $"보급 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    // ---------------------------------------------------------------- 도구

    /// <summary>표준 월드에서 시작 병력을 지운 빈 들판. 전멸 패배를 막으려고 세력마다 비무장 1기만 대도시 뒤에 남긴다.</summary>
    static World NewWorld(GameOptions opt, int seed = 0)
    {
        var w = World.CreateStandard(GameMap.Generate(100, 100, seed == 0 ? opt.Seed : seed), defs);
        w.Fog.Enabled = false;
        var keep = new HashSet<Unit>();
        for (int f = 1; f < w.Factions.Count; f++)
            if (w.Units.Where(u => u.Owner == f && u.Def.IsInfantry).OrderBy(u => u.Id).FirstOrDefault() is { } k) { k.Weapon = null; keep.Add(k); }
        foreach (var u in w.Units) if (!keep.Contains(u)) u.Hp = 0;
        w.Hold(1, keep.Where(u => u.Owner == 1)); w.Hold(2, keep.Where(u => u.Owner == 2));
        w.Step();
        return w;
    }

    static void Run(World w, float seconds)
    {
        for (int i = 0; i < (int)MathF.Round(seconds / Config.SimDt); i++) w.Step();
    }

    static Building Hq(World w, int f) => w.Buildings.First(b => b.Owner == f && b.Def.Headquarters);

    /// <summary>모든 도시에서 cityRadius + 4 칸 넘게 떨어진, 걸을 수 있는 지점 (anchor 에 가장 가까운 것).</summary>
    static Vector2 OpenSpot(World w, Vector2 anchor)
    {
        Vector2 best = anchor;
        float bestD = float.MaxValue;
        float need = defs.Supply.CityRadius + 4;
        for (int y = 4; y < w.Map.Height - 4; y++)
        for (int x = 4; x < w.Map.Width - 4; x++)
        {
            if (!w.Map.IsWalkable(x, y)) continue;
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Vector2.DistanceSquared(p, anchor);
            if (d >= bestD) continue;
            bool ok = true;
            foreach (var b in w.Buildings)
                if (b.EdgeDistance(p) < need) { ok = false; break; }
            for (int dy = -2; dy <= 2 && ok; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (!w.Map.IsWalkable(x + dx, y + dy)) { ok = false; break; }
            if (ok) { best = p; bestD = d; }
        }
        return best;
    }

    /// <summary>쏠 수 있는 과녁: 무기 없는 적 보병, 사수, 체력은 틱마다 되돌린다.</summary>
    static Unit Dummy(World w, int faction, Vector2 pos)
    {
        var d = w.SpawnUnit(defs.Units["infantry"], faction, pos);
        d.Weapon = null;
        w.Hold(faction, new[] { d });
        return d;
    }

    // ---------------------------------------------------------------- 탄약 소모

    static void Ammo(GameOptions opt)
    {
        var w = NewWorld(opt);
        var rifle = defs.Weapons["rifle"];
        var spot = OpenSpot(w, Hq(w, 1).Pos + new Vector2(30, 0));
        var shooter = w.SpawnUnit(defs.Units["infantry"], 1, spot);
        check(shooter.UsesAmmo && shooter.Ammo == rifle.Ammo && shooter.AmmoRatio == 1, $"생성 시 탄약 가득 ({rifle.Ammo})");
        var wagon = w.SpawnUnit(defs.Units["supply_wagon"], 1, spot + new Vector2(0, 30));
        check(wagon.Def.IsSupplyWagon && wagon.Cargo == wagon.Def.Cargo && !wagon.UsesAmmo && wagon.Weapon == null, $"보급 마차: 비무장 · 적재량 가득 ({wagon.Def.Cargo})");
        shooter.Ammo = 5;
        shooter.LastFiredAtTick = w.Tick;   // N34: 첫 사격 반응 (최대 1.2초) 동안 비전투 회복이 끼어들지 않게 — 교전 중으로 시작
        var dummy = Dummy(w, 2, spot + new Vector2(3, 0));
        int shots = 0;
        bool consistent = true;
        for (int i = 0; i < 60 * 40 && shots < 5; i++)
        {
            dummy.Hp = dummy.MaxHp;
            w.Step();
            if (shooter.LastShotTick == w.Tick - 1) { shots++; consistent &= shooter.Ammo == 5 - shots; }
        }
        check(shots == 5 && consistent && shooter.Ammo == 0 && !shooter.HasAmmo, $"사격 1회 = 탄약 1: {shots}발 (남은 5발) 뒤 탄약 0 → 사격 불가");
        long dry = w.Tick;
        for (int i = 0; i < (int)((defs.Supply.RegenDelay - 1) / Config.SimDt); i++) { dummy.Hp = dummy.MaxHp; w.Step(); }
        bool silent = shooter.LastShotTick < dry && shooter.Ammo == 0;
        for (int i = 0; i < 60 * 50; i++) { dummy.Hp = dummy.MaxHp; w.Step(); }
        check(silent && shooter.LastShotTick > dry, $"탄약 0: 회복 대기 {defs.Supply.RegenDelay:0}초 동안 사격 없음 → 그 뒤 회복한 탄약으로 다시 사격");
        var from = shooter.Pos;
        w.Move(1, new[] { shooter }, spot + new Vector2(-6, 0));
        Run(w, 3);
        check(Vector2.Distance(shooter.Pos, from) > 3, "탄약 0 이어도 이동은 가능");
    }

    // ---------------------------------------------------------------- 회복 (기본 · 대기 · 도시)

    static void Regen(GameOptions opt)
    {
        var w = NewWorld(opt);
        var s = defs.Supply;
        var spot = OpenSpot(w, Hq(w, 1).Pos + new Vector2(25, 0));
        var u = w.SpawnUnit(defs.Units["infantry"], 1, spot);
        Run(w, 20);   // 생성 직후 LastShotTick 초기값 때문에 회복 대기 (30초) 가 덜 지난 상태를 넘긴다
        u.Ammo = 0;
        check(w.AmmoRegenMulAt(1, spot) == 1f && w.AmmoQuiet(u), "빈 들판: 회복 배수 1, 생성 직후는 비전투");
        Run(w, 200);
        float expect = u.AmmoMax * s.RegenPct * 200;
        check(Math.Abs(u.Ammo - expect) <= 2, $"기본 회복: 200초에 {u.Ammo}발 (기대 ≈ {expect:0}, 한도 {u.AmmoMax} × {s.RegenPct * 100:0.#}%/초)");

        // 공격받으면 (빗나감 포함 — 제압 판정 지점) 대기가 다시 시작된다
        int a0 = u.Ammo;
        u.LastFiredAtTick = w.Tick;
        Run(w, s.RegenDelay - 1);
        int a1 = u.Ammo;
        Run(w, 60);
        check(a1 == a0 && u.Ammo > a0, $"피격 뒤 {s.RegenDelay:0}초 동안 회복 없음 ({a0} → {a1}), 그 뒤 다시 회복 ({u.Ammo})");

        // 아군 도시 근처 ×cityMul
        var hq = Hq(w, 1);
        var near = w.SpawnUnit(defs.Units["infantry"], 1, hq.Pos + new Vector2(0, hq.Size / 2f + 2));
        near.Ammo = 0;
        Run(w, 0.05f);
        check(w.AmmoRegenMulAt(1, near.Pos) == s.CityMul && w.AmmoRegenMulAt(2, near.Pos) == 1f, $"대도시 옆: 배수 ×{s.CityMul} (적 세력은 ×1)");
        Run(w, 100);
        float expectCity = near.AmmoMax * s.RegenPct * s.CityMul * 100;
        check(Math.Abs(near.Ammo - expectCity) <= 3, $"도시 근처 회복: 100초에 {near.Ammo}발 (기대 ≈ {expectCity:0})");
        Run(w, 600);
        check(near.Ammo == near.AmmoMax, "한도를 넘지 않음");

        // 교전 테스트(기본)는 탄약을 쓰지 않는다
        check(w.AmmoEnabled, "일반 게임은 탄약 사용");
    }

    // ---------------------------------------------------------------- 보급 마차

    static void Wagon(GameOptions opt)
    {
        var w = NewWorld(opt);
        var s = defs.Supply;
        var spot = OpenSpot(w, Hq(w, 1).Pos + new Vector2(25, 0));
        var wagon = w.SpawnUnit(defs.Units["supply_wagon"], 1, spot);
        var group = Enumerable.Range(0, 4).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, spot + new Vector2(2 + i * 0.9f, 0))).ToList();
        var far = w.SpawnUnit(defs.Units["infantry"], 1, spot + new Vector2(0, s.WagonRadius + 4));
        foreach (var u in group) u.Ammo = 0;
        far.Ammo = 0;
        Run(w, 0.05f);
        check(w.AmmoRegenMulAt(1, group[0].Pos) == s.WagonMul && w.AmmoRegenMulAt(1, far.Pos) == 1f && w.AmmoRegenMulAt(2, group[0].Pos) == 1f,
            $"보급 마차 {s.WagonRadius:0}칸 안 ×{s.WagonMul}, 밖 · 적 세력 ×1");
        float cargo0 = wagon.Cargo;
        Run(w, 100);
        float expect = group[0].AmmoMax * s.RegenPct * s.WagonMul * 100;
        check(group.All(u => Math.Abs(u.Ammo - expect) <= expect * 0.2f + 2) && Math.Abs(far.Ammo - expect / s.WagonMul) <= expect / s.WagonMul * 0.2f + 2,
            $"마차 근처 100초: {string.Join("/", group.Select(u => u.Ammo))}발 (기대 ≈ {expect:0}), 반경 밖 {far.Ammo}발");
        float used = cargo0 - wagon.Cargo;
        float expectUsed = group.Count * s.RegenPct * (s.WagonMul - 1) * 100;
        check(Math.Abs(used - expectUsed) < expectUsed * 0.3f, $"적재량 감소 = 가속분: {used:0.###}짐 (기대 {expectUsed:0.###})");

        // 빈 마차는 가속하지 않는다
        wagon.Cargo = 0;
        Run(w, 0.05f);
        check(w.AmmoRegenMulAt(1, group[0].Pos) == 1f, "적재량 0 이면 가속 없음");

        // 아군 도시 근처에서 다시 싣는다
        var hq = Hq(w, 1);
        var w2 = w.SpawnUnit(defs.Units["supply_wagon"], 1, hq.Pos + new Vector2(0, hq.Size / 2f + 2));
        w2.Cargo = 0;
        Run(w, 5);
        check(Math.Abs(w2.Cargo - s.WagonRefill * 5) <= s.WagonRefill * 1.01f, $"도시 근처 재적재: 5초에 {w2.Cargo:0.#}짐 (기대 ≈ {s.WagonRefill * 5:0.#})");
        Run(w, 120);
        check(w2.Cargo == w2.Def.Cargo, "재적재는 적재 한도까지");
    }

    // ---------------------------------------------------------------- 저장 · 불러오기 동일성

    static void SaveLoad(GameOptions opt)
    {
        World Build()
        {
            var w = NewWorld(opt, 20261018);
            var spot = OpenSpot(w, Hq(w, 1).Pos + new Vector2(25, 0));
            var us = new List<Unit>();
            for (int i = 0; i < 16; i++) { var u = w.SpawnUnit(defs.Units[i % 4 == 0 ? "heavy_at_rifleman" : "infantry"], 1, spot + new Vector2(i % 4 - 1.5f, i / 4 - 1.5f) * 0.9f); u.Ammo = i % 5; us.Add(u); }
            w.SpawnUnit(defs.Units["supply_wagon"], 1, spot + new Vector2(0, 3));
            w.Hold(1, us.Where((u, i) => i % 2 == 0));
            var foes = Enumerable.Range(0, 6).Select(i => w.SpawnUnit(defs.Units["infantry"], 2, spot + new Vector2(14, i - 2.5f))).ToList();
            w.AttackMove(2, foes, spot);
            return w;
        }
        // 파기 · 회복 · 교전 한가운데 (틱 300 · 중간) 저장 — 체크섬 · 본문 바이트 동일
        SnapshotSelfTest.Equivalence("보급: 탄약 회복 · 보급 마차 · 사수 · 교전 중", Build, 300, 1200, null, check);
    }
}
