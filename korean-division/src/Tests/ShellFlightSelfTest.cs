using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 포탄 비행 시간 (ShellFlight.cs): 거리별 비행 틱, 발사 → 착탄 지연 (피해 · 제압 · 구덩이 · 폭발은 착탄 때), 움직인 표적은 안 맞음,
/// 비행 중 저장 · 불러오기 동일, 리플레이 결정성. 방열 사계 (traverseArc): 사계 안 · 밖, 공격 명령 = 철수 → 재방열. `KD_SELFTEST_ONLY=shells`.
/// </summary>
public static class ShellFlightSelfTest
{
    static readonly SnapshotMeta Meta = new("shells", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> check) => LegacyDeploy.Wrap(() => RunImpl(opt, check));
    static void RunImpl(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            FlightTimes(check);
            RealHowitzer(opt, check);
            MovingTarget(opt, check);
            CraterAtImpact(opt, check);
            SaveLoad(opt, check);
            Replay(opt, check);
            TraverseArc(opt, check);
            RangesAndScatter(opt, check);
        }
        catch (Exception e) { check(false, $"포탄 비행 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static Unit Dummy(World w, int faction, Vector2 pos)
    {
        var u = w.SpawnUnit(Defs.Current.Units["infantry"], faction, pos);
        u.Weapon = null;
        w.Hold(faction, new[] { u });
        return u;
    }

    /// <summary>곡사포(관측 전용) 용 관측 유닛: 포 옆의 아군 정찰병.</summary>
    static void Spotter(World w, Vector2 c) { var sp = w.SpawnUnit(Defs.Current.Units["scout"], 1, c + new Vector2(5, 3)); w.Hold(1, new[] { sp }); }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    /// <summary>시험 곡사포: 퍼짐 0, 비행 flight 초 고정, 한 발만 (쿨다운 100초).</summary>
    static WeaponDef TestGun(float flight, float crater = 0) => new()
    {
        Key = "howitzer_heavy", Name = "시험 곡사포", TraverseArc = 60, Damage = 40, Range = 26, MinRange = 4, Cooldown = 100, Accuracy = 1, AimTime = 0.3f, HoldTime = 0.3f,
        Fire = "indirect", Splash = 2, SplashEdge = 0.3f, Scatter = 0, Suppress = 300, CraterChance = crater,
        ShellSpeed = 1000, FlightBase = flight, FlightMin = flight, FlightMax = flight,
    };

    /// <summary>포탄이 뜰 때까지 진행 (최대 maxTicks). 뜬 포탄 또는 null.</summary>
    static ShellFlight? UntilLaunch(World w, int maxTicks, Action? each = null)
    {
        for (int i = 0; i < maxTicks; i++) { each?.Invoke(); w.Step(); if (w.Shells.Count > 0) return w.Shells[0]; }
        return null;
    }

    // ---------------------------------------------------------------- 거리별 비행 시간

    static void FlightTimes(Action<bool, string> check)
    {
        var d = Defs.Current;
        WeaponDef heavy = d.Weapons["howitzer_heavy"], inf = d.Weapons["gun_infantry"], field = d.Weapons["gun_field"], tank = d.Weapons["ft_gun"], at = d.Weapons["at_rifle"];
        int h6 = World.FlightTicks(heavy, 6), h14 = World.FlightTicks(heavy, 14), h26 = World.FlightTicks(heavy, 26), hv40 = World.FlightTicks(heavy, 1000), hvMax = World.FlightTicks(heavy, heavy.Range), hv1 = World.FlightTicks(heavy, 0);
        int g2 = World.FlightTicks(inf, 2), g18 = World.FlightTicks(inf, 18), f26 = World.FlightTicks(field, 26);
        int sec = (int)MathF.Round(1f / Config.SimDt);
        check(h6 < h14 && h14 < h26 && h6 >= sec && h26 <= 3 * sec && hv40 == (int)MathF.Round(heavy.FlightMax / Config.SimDt) && hvMax >= 6 * sec && hvMax <= 8 * sec && hvMax <= hv40 && hv1 >= sec,
              $"곡사 비행 (N11): 6칸 {h6} < 14칸 {h14} < 26칸 {h26}틱 (1~3초), 최대 사거리 {heavy.Range}칸 {hvMax}틱 (6~8초 · 상한 이하), 상한 {hv40}틱, 0칸 = 하한 {hv1}틱");
        int gMax = World.FlightTicks(inf, inf.Range), fMax = World.FlightTicks(field, field.Range);
        check(g2 < g18 && g18 > 0 && g18 <= (int)MathF.Round(0.4f / Config.SimDt) && f26 < fMax && gMax >= sec / 2 && gMax <= sec && fMax >= (int)(0.8f * sec) && fMax <= (int)(1.25f * sec),
              $"직사 범위포 비행 (N11): 보병포 2칸 {g2} < 18칸 {g18}틱 (≤0.4초), 최대 사거리 {inf.Range}칸 {gMax}틱 (0.5~1초); 야전포 26칸 {f26} < {field.Range}칸 {fMax}틱 (≈1초)");
        check(World.FlightTicks(tank, 20) == 0 && World.FlightTicks(at, 12) == 0, "37mm 단포 · 대전차소총 = 즉발 (shellSpeed 없음)");
    }

    // ---------------------------------------------------------------- 실제 곡사포: 발사 → 비행 → 착탄

    static void RealHowitzer(GameOptions opt, Action<bool, string> check)
    {
        var w = NewWorld(opt.Seed);
        var c = Center(w);
        var gun = w.SpawnUnit(Defs.Current.Units["heavy_howitzer"], 1, c);
        Spotter(w, c);
        w.Hold(1, new[] { gun });
        var foe = Dummy(w, 2, c + new Vector2(12, 0));
        var s = UntilLaunch(w, 60 * 12, () => foe.Hp = 100000);
        if (s == null) { check(false, "곡사포가 발사하지 않음"); return; }
        int expect = World.FlightTicks(gun.Weapon!, Vector2.Distance(s.From, s.Pos));
        bool atFire = w.Impacts.All(i => i.ShooterId != gun.Id) && foe.Hp == 100000 && w.Shots.Any(x => x.ShooterId == gun.Id) && s.ImpactTick - s.FireTick == expect;
        long impactTick = s.ImpactTick;
        float hp0 = foe.Hp;
        while (w.Tick <= impactTick) { foe.Hp = hp0; w.Step(); }
        bool hit = w.Impacts.Any(i => i.ShooterId == gun.Id && i.Tick == impactTick && i.Impact == s.Pos) && !w.Shells.Contains(s);
        check(atFire && hit, $"곡사포: 발사 틱에 Shots (포구) 만, 비행 {expect}틱 ({expect * Config.SimDt:0.00}초) 뒤 착탄 틱 {impactTick} 에 Impacts (폭발 · 피해)");
    }

    // ---------------------------------------------------------------- 움직인 표적은 맞지 않는다 (착탄 = 지점)

    static void MovingTarget(GameOptions opt, Action<bool, string> check)
    {
        using var _ = TestDefs.NoCommandDelay();
        var w = NewWorld(opt.Seed + 1);
        var c = Center(w);
        var gun = w.SpawnUnit(Defs.Current.Units["heavy_howitzer"], 1, c);
        var tw = TestGun(3f);
        gun.Weapon = tw;
        var runner = Dummy(w, 2, c + new Vector2(12, 0));
        var stayer = Dummy(w, 2, c + new Vector2(12, 1.0f));
        w.Attack(1, new[] { gun }, runner);
        var s = UntilLaunch(w, 60 * 12);
        if (s == null) { check(false, "시험 곡사포가 발사하지 않음"); return; }
        Vector2 aim = s.Pos;
        w.Move(2, new[] { runner }, runner.Pos + new Vector2(0, -12));
        bool noEarly = true;
        while (w.Tick < s.ImpactTick) { if (stayer.Hp < 100 || stayer.Suppression > 0 || runner.Hp < 100) noEarly = false; w.Step(); }
        w.Step();
        float runDist = Vector2.Distance(runner.Pos, aim) - runner.Radius;
        check(noEarly && runDist > tw.Splash && runner.Hp == 100 && stayer.Hp < 100 && stayer.Suppression > 0,
              $"착탄 전엔 피해 · 제압 없음 ({noEarly}); 3초 동안 {runDist:0.0}칸 피한 표적 = 무피해 ({runner.Hp:0}), 지점 옆에 남은 적 = 피해 {100 - stayer.Hp:0} · 제압 {stayer.Suppression}");
    }

    // ---------------------------------------------------------------- 포탄 구덩이는 착탄 때

    static void CraterAtImpact(GameOptions opt, Action<bool, string> check)
    {
        var w = NewWorld(opt.Seed + 2);
        var c = Center(w);
        var gun = w.SpawnUnit(Defs.Current.Units["heavy_howitzer"], 1, c);
        gun.Weapon = TestGun(1f, crater: 1f);
        var foe = Dummy(w, 2, c + new Vector2(10, 0));
        w.Attack(1, new[] { gun }, foe);
        var s = UntilLaunch(w, 60 * 12, () => foe.Hp = 100000);
        if (s == null) { check(false, "구덩이 시험: 발사 없음"); return; }
        var (tx, ty) = GameMap.TileOf(s.Pos);
        var before = w.Map.Get(tx, ty);
        bool notYet = before != TerrainType.Crater;
        while (w.Tick < s.ImpactTick) { if (w.Map.Get(tx, ty) == TerrainType.Crater) notYet = false; foe.Hp = 100000; w.Step(); }
        w.Step();
        check(before == TerrainType.Grass && notYet && w.Map.Get(tx, ty) == TerrainType.Crater, $"포탄 구덩이: 비행 중엔 {before}, 착탄 틱에 구덩이 ({w.Map.Get(tx, ty)})");
    }

    // ---------------------------------------------------------------- 비행 중 저장 · 불러오기

    static void SaveLoad(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 3);
        var c = Center(w);
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, c);
        Spotter(w, c);
        w.Hold(1, new[] { gun });
        var foes = Enumerable.Range(0, 3).Select(i => Dummy(w, 2, c + new Vector2(14, i * 1.5f))).ToList();
        foreach (var f in foes) f.Hp = 1000;
        var s = UntilLaunch(w, 60 * 15);
        Steps(w, 20);
        bool inFlight = s != null && w.Shells.Count > 0;
        int nFlight = w.Shells.Count;
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = y.Shells.Count == w.Shells.Count && y.Shells.Zip(w.Shells, (a, b) => a.Pos == b.Pos && a.ImpactTick == b.ImpactTick && a.Weapon == b.Weapon && a.ShooterId == b.ShooterId).All(x => x)
                    && y.ComputeChecksum() == w.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool cont = true;
        int impactsW = 0, impactsY = 0;
        for (int i = 0; i < 600; i++)
        {
            w.Step(); y.Step();
            impactsW += w.Impacts.Count(x => x.Tick == w.Tick - 1); impactsY += y.Impacts.Count(x => x.Tick == y.Tick - 1);
            if (i % 25 == 24 && w.ComputeChecksum() != y.ComputeChecksum()) { cont = false; break; }
        }
        check(inFlight && same && cont && impactsW > 0 && impactsW == impactsY,
              $"포탄 비행 중 저장 (날아가는 포탄 {nFlight}): 불러온 월드 동일 · 600틱 이어서 체크섬 같음 ({cont}) · 착탄 {impactsW}/{impactsY}");
    }

    // ---------------------------------------------------------------- 리플레이 결정성

    static void Replay(GameOptions opt, Action<bool, string> check)
    {
        (World W, Unit Gun, Unit Foe) Make()
        {
            var w = NewWorld(opt.Seed + 4);
            var c = Center(w);
            var gun = w.SpawnUnit(Defs.Current.Units["heavy_howitzer"], 1, c);
            Spotter(w, c);
            var foe = w.SpawnUnit(Defs.Current.Units["infantry"], 2, c + new Vector2(13, 2));
            foe.Weapon = null; foe.Hp = 1000;
            return (w, gun, foe);
        }
        var (a, ga, fa) = Make();
        a.Attack(1, new[] { ga }, fa);
        int maxInFlight = 0;
        for (int i = 0; i < 60 * 25; i++) { a.Step(); maxInFlight = Math.Max(maxInFlight, a.Shells.Count); if (i == 60 * 6) a.Move(2, new[] { fa }, fa.Pos + new Vector2(3, 6)); }
        var (b, _, _) = Make();
        var log = a.CommandLog.ToList();
        b.CommandFeed = t => log.Where(r => r.Tick == t).Select(r => r.Command);
        Steps(b, 60 * 25);
        check(maxInFlight > 0 && a.ComputeChecksum() == b.ComputeChecksum() && Math.Abs(a.Find(fa.Id)?.Hp ?? -1) == Math.Abs(b.Find(fa.Id)?.Hp ?? -1),
              $"리플레이: 포격 + 표적 이동 (비행 중 포탄 최대 {maxInFlight}) → 기록 명령만으로 같은 체크섬");
    }

    // ---------------------------------------------------------------- N11 사거리 · 거리별 퍼짐

    static void RangesAndScatter(GameOptions opt, Action<bool, string> check)
    {
        var d = Defs.Current;
        WeaponDef inf = d.Weapons["gun_infantry"], field = d.Weapons["gun_field"], how = d.Weapons["howitzer_heavy"];
        check(inf.Range == 40 && field.Range == 80 && how.Range == 120 && how.MinRange == 6 && inf.MinRange == 0 && field.MinRange == 0,
              $"N11 사거리: 보병포 {inf.Range} · 야전포 {field.Range} · 곡사포 {how.Range} (최소 {how.MinRange})");
        float h10 = World.ScatterAt(how, 10), h30 = World.ScatterAt(how, 30), h60 = World.ScatterAt(how, 60), h120 = World.ScatterAt(how, 120), h200 = World.ScatterAt(how, 200);
        float f10 = World.ScatterAt(field, 10), f80 = World.ScatterAt(field, 80), i40 = World.ScatterAt(inf, 40);
        check(h10 < h30 && h30 < h60 && h60 < h120 && h120 == how.Scatter && h200 == h120 && h10 < how.Splash && h120 >= 3 * how.Splash && f10 < 1f && f80 == field.Scatter && i40 == inf.Scatter && field.Scatter > inf.Scatter,
              $"N11 거리별 퍼짐: 곡사포 10칸 {h10:0.00} < 30칸 {h30:0.00} < 60칸 {h60:0.00} < 120칸 {h120:0.00} (= scatter, 반경 {how.Splash} 의 3배 이상, arty3), 야전포 10칸 {f10:0.00} · 80칸 {f80:0.00}, 보병포 40칸 {i40:0.00}");
        // 실제 곡사포 지점 포격: 가까운 지점 (25칸) 과 먼 지점 (110칸) 의 착탄 오차
        float Spread(float dist, out int n, out float worst)
        {
            var w = TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(160, 160, opt.Seed + 31)), d));
            w.Fog.Enabled = false;
            foreach (var x in w.Units) if (x.Owner != 1) x.Hp = 0;
            var (gx, gy) = w.Map.NearestWalkable(20, 80);
            var gp = new Vector2(gx + 0.5f, gy + 0.5f);
            var gun = w.SpawnUnit(d.Units["heavy_howitzer"], 1, gp);
            gun.Facing = 0;
            var target = gp + new Vector2(dist, 0);
            w.GroundFire(1, new[] { gun }, target);
            var errs = new List<float>();
            for (int i = 0; i < 60 * 150 && errs.Count < 8; i++)
            {
                w.Step();
                foreach (var im in w.Impacts) if (im.ShooterId == gun.Id && im.Tick == w.Tick - 1) errs.Add(Vector2.Distance(im.Impact, target));
            }
            n = errs.Count; worst = errs.Count > 0 ? errs.Max() : 0;
            return errs.Count > 0 ? errs.Average() : 0;
        }
        float nearAvg = Spread(25, out int nn, out float nw), farAvg = Spread(110, out int nf, out float fw);
        float mul = d.CombatRules.GroundScatterMul;
        check(nn >= 6 && nf >= 6 && nearAvg < farAvg && nw <= World.ScatterAt(how, 25) * mul + 1e-3f && fw <= World.ScatterAt(how, 110) * mul + 1e-3f && farAvg > how.Splash,
              $"N11 곡사포 지점 포격 착탄 오차: 25칸 평균 {nearAvg:0.00} (최대 {nw:0.00}, {nn}발) < 110칸 평균 {farAvg:0.00} (최대 {fw:0.00}, {nf}발) — 먼 거리는 반경 {how.Splash} 밖이 평균");
    }

    // ---------------------------------------------------------------- 방열 사계 (traverseArc)

    static void TraverseArc(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        check(defs.Weapons["gun_infantry"].TraverseArc == 45 && defs.Weapons["gun_field"].TraverseArc == 45 && defs.Weapons["howitzer_heavy"].TraverseArc == 60 && defs.Weapons["ft_gun"].TraverseArc == 0,
              "balance.json traverseArc: 보병포 · 야전포 45° · 중곡사포 60° · 37mm 단포 없음");
        using var _ = TestDefs.NoCommandDelay();
        var towed = defs.Units["heavy_howitzer"];
        int setup = World.SetupTicks(towed), pack = World.PackTicks(towed);
        var w = NewWorld(opt.Seed + 5);
        foreach (var x in w.Units) if (x.Owner != 1) x.Hp = 0;   // 지도에 남은 적 (보급 마차 등) 이 사계 회전 표적이 되지 않게
        var c = Center(w);
        var gun = w.SpawnUnit(towed, 1, c);
        Spotter(w, c);
        w.Hold(1, new[] { gun });
        var a = Dummy(w, 2, c + new Vector2(14, 0));
        float half = World.TraverseHalf(gun);
        bool inArc = true;
        void Tick(params Unit[] keep)
        {
            foreach (var k in keep) if (k.Alive) k.Hp = 100000;
            w.Step();
            if (World.TraverseLocked(gun) && MathF.Abs(DetMath.WrapPi(gun.Facing - gun.DeployCenter)) > half + 1e-3f) inArc = false;
        }
        for (int i = 0; i < 60 * 12 && gun.LastShotTick < 0; i++) Tick(a);
        bool first = gun.LastShotTick > 0 && gun.Deployed && MathF.Abs(DetMath.WrapPi(gun.DeployCenter)) < 0.05f;
        Vector2 At(float deg) => c + new Vector2(MathF.Cos(deg * DetMath.Pi / 180f), MathF.Sin(deg * DetMath.Pi / 180f)) * 14f;
        check(first && MathF.Abs(half - 30f * DetMath.Pi / 180f) < 1e-4f && World.CanTraverseTo(gun, At(25)) && World.CanTraverseTo(gun, At(-25)) && !World.CanTraverseTo(gun, At(35)) && !World.CanTraverseTo(gun, At(90)),
              $"방열 사계 60°: 표적 쪽 (중심 {gun.DeployCenter / DetMath.Pi * 180f:0.0}°) 으로 방열 후 사격, ±25° 안 · ±35° · 90° 밖");
        // N10: 사계 밖 (90° = 중심을 60° 옮겨야 함, 문턱 90° 미만) 적만 남음 → 사수 대포가 자동 표적으로 잡고 방열한 채 포가를 돌려 사격 (철수 없음)
        a.Hp = 0;
        var b = Dummy(w, 2, At(90));
        long shot0 = gun.LastShotTick, t0 = w.Tick;
        bool everPacked = false; long shot1 = -1;
        for (int i = 0; i < 60 * 20 && shot1 < 0; i++) { Tick(b); if (gun.Deploy != DeployPhase.Deployed) everPacked = true; if (gun.LastShotTick > shot0) shot1 = w.Tick - 1; }
        float shiftRate = towed.TurnRadPerSec * defs.CombatRules.TraverseShiftMul;
        int minShift = (int)(60f * DetMath.Pi / 180f / shiftRate / Config.SimDt) - 2;
        float c1 = gun.DeployCenter / DetMath.Pi * 180f;
        check(shot1 > 0 && !everPacked && gun.Deployed && c1 > 55f && c1 < 95f && shot1 - t0 >= minShift && shot1 - t0 < pack + setup + minShift && w.Stats.TraverseShiftTicks > 0 && inArc,
              $"N10 사계 밖 60° (문턱 {defs.CombatRules.TraverseRedeployDeg}° 미만): 사수 대포가 방열한 채 포가 회전 (중심 0 → {c1:0.0}°, 회전 {w.Stats.TraverseShiftTicks}틱 ≥ {minShift}) 후 사격 ({shot1 - t0}틱, 철수 없음 {!everPacked}), 차체는 늘 사계 안 ({inArc})");
        // 문턱 이상 (중심 반대편, 옮길 각 150°): 자동 · 사수 표적은 잡지 않음
        b.Hp = 0;
        float back = c1 + 180f;
        var e = Dummy(w, 2, At(back));
        long shotB = gun.LastShotTick;
        bool grabbed = false;
        for (int i = 0; i < 60 * 6; i++) { Tick(e); if (gun.Target == e) grabbed = true; }
        check(!grabbed && gun.LastShotTick == shotB && gun.Deployed && !World.CanTraverseInPlace(gun, e.Pos), $"문턱 이상 (150°) 적: 사수 대포는 표적으로 잡지 않고 쏘지 않음, 방열 유지 (잡음 {grabbed}, 사격 {gun.LastShotTick != shotB}, 방열 {gun.Deploy}, 중심 {gun.DeployCenter / DetMath.Pi * 180f:0.0}°, 제자리 {World.CanTraverseInPlace(gun, e.Pos)})");
        // 공격 명령: 철수 → 회전 → 재방열 → 사격
        long cmd = w.Tick, shot0b = gun.LastShotTick;
        w.Attack(1, new[] { gun }, e);
        bool packed = false; long shot2 = -1;
        for (int i = 0; i < 60 * 30 && shot2 < 0; i++) { Tick(e); if (gun.Deploy == DeployPhase.Packing) packed = true; if (gun.LastShotTick > shot0b) shot2 = w.Tick - 1; }
        bool redeployed = shot2 > 0 && gun.Deployed && MathF.Abs(DetMath.WrapPi(gun.DeployCenter - back * DetMath.Pi / 180f)) < 0.05f;
        check(packed && redeployed && shot2 - cmd >= pack + setup && inArc && w.Stats.TraverseRepacks >= 1,
              $"문턱 이상 공격 명령: 철수 {pack} + 재방열 {setup}틱 뒤 사격 ({shot2 - cmd}틱), 새 중심 {gun.DeployCenter / DetMath.Pi * 180f:0.0}° (표적 {DetMath.WrapPi(back * DetMath.Pi / 180f) / DetMath.Pi * 180f:0.0}°), 방열 중 차체는 늘 사계 안 ({inArc})");
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        var yg = y.Units.First(u => u.Id == gun.Id);
        check(yg.DeployCenter == gun.DeployCenter && yg.Deployed && y.ComputeChecksum() == w.ComputeChecksum(), "방열 사계 중심 저장 · 불러오기 동일");
    }
}
