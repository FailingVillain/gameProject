using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>사격 지연 (조준 · 정지 대기) 검증: 움직이는 중에는 쏘지 않는다, 서서 조준한 뒤 첫 발, 서 있는 결투의 DPS 는 첫 발 지연 외에 그대로.</summary>
public static class AimSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();   // 지휘 계통 지연 도입 전 이동 · 시간 기대값 (지연 자체는 SquadSelfTest)
        try
        {
            StandingDuel(opt, check);
            ChaseAndHold(opt, check);
            BigBattleInvariant(opt, check);
        }
        catch (Exception e) { check(false, $"사격 지연 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World NewWorld(int seed)
    {
        var w = TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(100, 100, seed)), Defs.Current));
        w.Fog.Enabled = false;
        return w;
    }

    static Vector2 Center(World w)
    {
        var (cx, cy) = w.Map.NearestWalkable(w.Map.Width / 2, w.Map.Height / 2);
        return new Vector2(cx + 0.5f, cy + 0.5f);
    }

    /// <summary>한 틱 뒤: 방금 쏜 유닛이 이동 중이 아니고 마지막 이동 뒤 조준 시간이 지났는지.</summary>
    static string? ShotViolation(World w, Unit u)
    {
        if (u.LastShotTick != w.Tick - 1 || u.Weapon == null) return null;
        if (w.MeleeStrikes.Any(m => m.AttackerId == u.Id && m.Tick == w.Tick - 1)) return null;   // 근접 공격 (돌격) 은 조준 없이 친다
        if (u.Velocity != Vector2.Zero)
            return u.Def.MarchFire && u.MarchShot == false && u.MarchTarget != 0 ? null : $"U{u.Id} 이동 중 사격 (틱 {w.Tick - 1})";   // 행진 사격 (교전 4차) 은 허용
        long sinceMove = (w.Tick - 1) - u.LastMoveTick;
        if (sinceMove < u.Weapon.AimTicks) return $"U{u.Id} 정지 {sinceMove}틱 만에 사격 (조준 {u.Weapon.AimTicks}틱, 틱 {w.Tick - 1})";
        return null;
    }

    static void StandingDuel(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed);
        var inf = defs.Units["infantry"];
        var c = Center(w);
        var a = w.SpawnUnit(inf, 1, c);
        var d = w.SpawnUnit(inf, 2, c + new Vector2(3, 0));
        w.Hold(2, new[] { d }); d.Weapon = null; w.Hold(1, new[] { a });
        var weapon = a.Weapon!;
        // 재장전: 고정 cooldown 또는 [cooldownMin, cooldownMax] 결정적 난수 (소총 1.8~2.4초) — 간격은 그 범위 안, 평균은 중앙값 근처
        float cdLo = weapon.RandomCooldown ? weapon.CooldownMin : weapon.Cooldown, cdHi = weapon.RandomCooldown ? weapon.CooldownMax : weapon.Cooldown;
        int aim = weapon.AimTicks, cdMin = (int)MathF.Ceiling(cdLo / Config.SimDt - 1e-3f), cdMax = (int)MathF.Ceiling(cdHi / Config.SimDt - 1e-3f);
        float cdMean = weapon.CooldownMean / Config.SimDt;
        var shotTicks = new List<long>();
        const int Secs = 120;
        for (int i = 0; i < 60 * Secs; i++)
        {
            d.Hp = 100000; a.Ammo = a.AmmoMax;   // 탄약 천천히 회복: 사격 간격 검증이라 탄은 채워 둔다
            w.Step();
            if (a.LastShotTick == w.Tick - 1 && (shotTicks.Count == 0 || shotTicks[^1] != a.LastShotTick)) shotTicks.Add(a.LastShotTick);
        }
        // 첫 발: 조준 틱(aim) 뒤 (명령 처리 1틱 포함). 이후는 재장전 간격 (재장전은 조준 중에도 흐른다)
        int aimHi = Math.Max(aim, (int)MathF.Round(defs.CombatRules.FirstShotMax / Config.SimDt));   // N34 첫 사격 반응 (쉬던 보병의 첫 발 = max(조준, 0.2~1.2초))
        bool first = shotTicks.Count > 10 && shotTicks[0] >= aim - 1 && shotTicks[0] <= aimHi + 2;
        var gaps = shotTicks.Zip(shotTicks.Skip(1), (x, y) => y - x).ToList();
        bool steady = gaps.Count > 0 && gaps.All(g => g >= cdMin - 1 && g <= cdMax + 1);
        bool varied = !weapon.RandomCooldown || (gaps.Distinct().Count() >= 5 && gaps.Min() < cdMean - 6 && gaps.Max() > cdMean + 6);
        check(first, $"서 있는 결투: 첫 발은 조준 시간({aim}틱) ~ 첫 사격 반응({aimHi}틱) 뒤 (첫 발 틱 {(shotTicks.Count > 0 ? shotTicks[0] : -1)})");
        check(steady && varied, $"서 있는 결투: 사격 간격은 재장전 범위 {cdMin}~{cdMax}틱 안에서 흩어짐 (실제 {(gaps.Count > 0 ? gaps.Min() : -1)}~{(gaps.Count > 0 ? gaps.Max() : -1)}, 종류 {gaps.Distinct().Count()})");
        int expected = (int)((60 * Secs - 1 - aim) / cdMean) + 1;
        check(Math.Abs(shotTicks.Count - expected) <= Math.Max(2, expected / 10), $"서 있는 결투 {Secs}초 사격 수 {shotTicks.Count} ≈ 기대 {expected} (평균 간격 {cdMean:0}틱)");
    }

    static void ChaseAndHold(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed);
        var inf = defs.Units["infantry"];
        var c = Center(w);
        var a = w.SpawnUnit(inf, 1, c);
        var d = w.SpawnUnit(defs.Units["recruit"], 2, c + new Vector2(0, 4));   // 남쪽으로 도망 (동쪽 19칸에 마을 — 사거리 24 면 사선이 막힌다)
        d.Weapon = null;
        int hold = a.Weapon!.HoldTicks;
        // 1) 교전 → 표적이 도망 → 정지 대기 후 추격 → 다시 사거리 → 조준 후 사격
        var violations = new List<string>();
        bool engaged = false, left = false, chased = false;
        long lastInRange = -1, firstWalk = -1;
        int shotsAfterChase = 0;
        for (int i = 0; i < 60 * 40; i++)
        {
            d.Hp = 100000; a.Ammo = a.AmmoMax;   // 탄약 천천히 회복: 사격 간격 검증이라 탄은 채워 둔다
            if (i == 60 * 4) { w.Move(2, new[] { d }, c + new Vector2(0, 40)); left = true; }
            if (i == 60 * 12) { w.Hold(2, new[] { d }); }
            w.Step();
            if (ShotViolation(w, a) is string v) violations.Add(v);
            float dist = d.EdgeDistance(a.Pos) - a.Radius;
            if (dist <= a.Weapon!.Range && !left) engaged = true;
            if (left && !chased)
            {
                if (dist <= a.Weapon.Range) lastInRange = w.Tick;
                if (a.Walking && firstWalk < 0) { firstWalk = w.Tick; chased = true; }
            }
            if (chased && a.LastShotTick == w.Tick - 1) shotsAfterChase++;
        }
        check(engaged, "추격 시험: 서로 사거리 안에서 교전 시작");
        check(violations.Count == 0, $"추격 시험: 이동 중 · 정지 직후 사격 없음 ({violations.Count}건 {violations.FirstOrDefault()})");
        check(chased && firstWalk - lastInRange >= hold - 2, $"표적이 사거리를 벗어난 뒤 {hold}틱 정지했다가 추격 (정지 {firstWalk - lastInRange}틱)");
        check(shotsAfterChase > 3, $"추격해서 따라잡으면 다시 사격 ({shotsAfterChase}발)");

        // 2) 이동 명령은 사격 후 대기 없이 곧바로 이동
        var w2 = NewWorld(opt.Seed);
        var c2 = Center(w2);
        var a2 = w2.SpawnUnit(inf, 1, c2);
        var d2 = w2.SpawnUnit(inf, 2, c2 + new Vector2(3, 0)); d2.Weapon = null; w2.Hold(2, new[] { d2 });
        for (int i = 0; i < 60 * 2; i++) { d2.Hp = 100000; w2.Step(); }
        int cdWindow = (int)MathF.Ceiling(MathF.Max(a2.Weapon!.CooldownMax, a2.Weapon.Cooldown) / Config.SimDt) + 20;   // 발사 간격 난수(최대 2.4초) 안에 쏜 적이 있으면 교전 중
        bool wasFiring = a2.Target != null && a2.LastShotTick > w2.Tick - cdWindow;
        w2.Move(1, new[] { a2 }, c2 + new Vector2(-20, 0));
        long t0 = w2.Tick, walk = -1;
        for (int i = 0; i < 60 && walk < 0; i++) { w2.Step(); if (a2.Walking) walk = w2.Tick - t0; }
        check(wasFiring && walk >= 0 && walk <= 12, $"이동 명령은 대기 없이 즉시 (교전 중이었음 {wasFiring}, {walk}틱 뒤 이동 시작)");

        // 3) 공격 이동: 사거리 안 적 → 서서 사격, 적 처치 후 정지 대기 → 다시 이동
        var w3 = NewWorld(opt.Seed);
        var c3 = Center(w3);
        var sq = Enumerable.Range(0, 5).Select(i => w3.SpawnUnit(inf, 1, c3 + new Vector2(-10, i * 0.8f))).ToList();
        var tg = w3.SpawnUnit(inf, 2, c3 + new Vector2(2, 0)); tg.Weapon = null; w3.Hold(2, new[] { tg });
        w3.AttackMove(1, sq, c3 + new Vector2(25, 0));
        var vio = new List<string>();
        for (int i = 0; i < 60 * 30; i++) { w3.Step(); foreach (var u in sq) if (ShotViolation(w3, u) is string v) vio.Add(v); }
        check(!tg.Alive && vio.Count == 0 && sq.Any(u => u.Alive), $"공격 이동: 사격은 항상 서서 ({vio.Count}건 {vio.FirstOrDefault()}), 표적 처치 {!tg.Alive}");
    }

    static void BigBattleInvariant(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 3);
        var c = Center(w);
        string[] mix = { "infantry", "recruit", "veteran", "heavy_at_rifleman", "tank_ft_male", "scout", "infantry" };
        var a = new List<Unit>(); var b = new List<Unit>();
        for (int i = 0; i < 40; i++)
        {
            a.Add(w.SpawnUnit(defs.Units[mix[i % mix.Length]], 1, c + new Vector2(-12 + (i % 8) * 0.8f, (i / 8) * 0.8f - 2)));
            b.Add(w.SpawnUnit(defs.Units[mix[(i + 2) % mix.Length]], 2, c + new Vector2(12 - (i % 8) * 0.8f, (i / 8) * 0.8f - 2)));
        }
        w.AttackMove(1, a, c + new Vector2(14, 0));
        w.AttackMove(2, b, c + new Vector2(-14, 0));
        var vio = new List<string>();
        long shots = 0;
        var all = a.Concat(b).ToList();
        for (int i = 0; i < 60 * 60; i++)
        {
            w.Step();
            foreach (var u in all) if (u.Alive) { if (ShotViolation(w, u) is string v) vio.Add(v); if (u.LastShotTick == w.Tick - 1) shots++; }
        }
        check(shots > 200 && vio.Count == 0, $"대규모 전투 1분: 이동 중 · 조준 전 사격 0건 ({vio.Count}건 {vio.FirstOrDefault()}), 사격 {shots}회");
    }
}
