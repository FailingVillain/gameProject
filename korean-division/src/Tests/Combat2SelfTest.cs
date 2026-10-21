using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// v0.4 전투 검증 (ROADMAP §2 · §3 · §5): 거리 명중 곡선 · 유닛 명중률 · 상한, 포탑 추적 · 발사 각 제한, 경계 지도(확산 · 만료 · 동맹 공유),
/// 안개 2단계, 은폐 탐지 확률 · 결정성 · 사격 노출, 포병(최소 사거리 · 관측 지연 · 퍼짐 분포 · 범위 감소 · 아군 피해 없음), 경계 · 조준 중 저장 동일성.
/// </summary>
public static class Combat2SelfTest
{
    static readonly SnapshotMeta Meta = new("combat2", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> check) => LegacyDeploy.Wrap(() => RunImpl(opt, check));
    static void RunImpl(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            Curves(opt, check);
            FarHitRate(opt, check);
            Turret(opt, check);
            AlertGrid(opt, check);
            Concealment(opt, check);
            SplashFalloff(opt, check);
            DeployPack(opt, check);
            DirectFire(opt, check);
            SaveLoad(opt, check);
        }
        catch (Exception e) { check(false, $"전투 v0.4 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World NewWorld(int seed, bool fog)
    {
        var w = TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(100, 100, seed)), Defs.Current));
        w.Fog.Enabled = fog;
        return w;
    }

    static Vector2 Center(World w)
    {
        var (cx, cy) = w.Map.NearestWalkable(w.Map.Width / 2, w.Map.Height / 2);
        return new Vector2(cx + 0.5f, cy + 0.5f);
    }

    /// <summary>무기 없는 정지 유닛 (표적용).</summary>
    static Unit Dummy(World w, int faction, Vector2 pos, string kind = "infantry")
    {
        var u = w.SpawnUnit(Defs.Current.Units[kind], faction, pos);
        u.Weapon = null;
        w.Hold(faction, new[] { u });
        return u;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static bool Near(float a, float b, float eps = 1e-3f) => MathF.Abs(a - b) <= eps;
    const float Deg = DetMath.Pi / 180f;

    // ---------------------------------------------------------------- §2 명중 곡선 · 유닛 명중률 · 상한

    static void Curves(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var rifle = defs.Weapons["rifle"];
        float R = rifle.Range;
        // 연속 곡선 (ROADMAP §2-3): p = near − (near − far)·r², r = 거리/사거리. 50% 거리에서 감소분의 1/4, 사거리 밖은 far 고정.
        float drop = rifle.AccNear - rifle.AccFar;
        float p25 = World.AccuracyCurve(rifle, 0.25f * R), p50 = World.AccuracyCurve(rifle, 0.5f * R), p75 = World.AccuracyCurve(rifle, 0.75f * R);
        bool curve = Near(World.AccuracyCurve(rifle, 0), rifle.AccNear)
                  && Near(p25, rifle.AccNear - drop * 0.0625f) && Near(p50, rifle.AccNear - drop * 0.25f) && Near(p75, rifle.AccNear - drop * 0.5625f)
                  && p25 > p50 && p50 > p75 && p75 > rifle.AccFar
                  && Near(World.AccuracyCurve(rifle, R), rifle.AccFar) && Near(World.AccuracyCurve(rifle, R + 3), rifle.AccFar)
                  && rifle.AccNear == 0.35f && rifle.AccFar == 0.08f;   // 교전 2차 추가 A: 0.30/0.04 → 0.35/0.06, 2026-10-19 장거리 0.06 → 0.08
        check(curve, $"소총 거리 곡선 (거리 제곱 감소): 0% {rifle.AccNear} · 25% {p25:0.000} · 50% {p50:0.000} · 75% {p75:0.000} · 100% {rifle.AccFar}");
        var carb = defs.Weapons["carbine"];
        check(carb.AccNear > 0 && carb.AccFar > 0 && World.AccuracyCurve(carb, 0.5f * carb.Range) < carb.AccNear && World.AccuracyCurve(carb, 0.5f * carb.Range) > carb.AccFar, "카빈도 같은 곡선 (accNear/accFar)");
        bool cdRange = rifle.RandomCooldown && rifle.CooldownMin == 1.8f && rifle.CooldownMax == 2.4f && Near(rifle.CooldownMean, 2.1f) && carb.RandomCooldown && !defs.Weapons["ft_gun"].RandomCooldown;
        check(cdRange, $"발사 간격 난수 범위: 소총 {rifle.CooldownMin}~{rifle.CooldownMax}초 · 카빈 {carb.CooldownMin}~{carb.CooldownMax}초 · 전차포 고정 {defs.Weapons["ft_gun"].Cooldown}초");
        bool basics = rifle.Damage == 40 && rifle.Range == 24 && rifle.Ammo == 60 && defs.Units["infantry"].Hp == 100 && defs.Armor(defs.Units["infantry"].Armor)!.Armor < 40 / 3f
                      && 3 * (rifle.Damage - defs.Armor("basic")!.Armor) >= 100 && 2 * (rifle.Damage - defs.Armor("basic")!.Armor) < 100;
        check(basics, $"소총 피해 {rifle.Damage} · 사거리 {rifle.Range} · 탄약 {rifle.Ammo}: 기본 보병(체력 {defs.Units["infantry"].Hp}, 장갑 {defs.Armor("basic")!.Armor}) 은 3발에 죽고 2발은 버팀");
        var tool = defs.Weapons["bayonet"];
        check(Near(World.AccuracyCurve(tool, 0.1f), tool.Accuracy) && Near(World.AccuracyCurve(tool, 5f), tool.Accuracy), $"곡선 없는 무기(착검)는 accuracy 고정 {tool.Accuracy}");

        var w = NewWorld(opt.Seed, fog: false);
        var c = Center(w);
        var tgt = Dummy(w, 2, c + new Vector2(3, 0));
        var recruit = w.SpawnUnit(defs.Units["recruit"], 1, c);
        var inf = w.SpawnUnit(defs.Units["infantry"], 1, c);
        var vet = w.SpawnUnit(defs.Units["veteran"], 1, c);
        float pr = w.HitChance(recruit, tgt), pi = w.HitChance(inf, tgt), pv = w.HitChance(vet, tgt);
        float pc = World.AccuracyCurve(rifle, MathF.Max(0, tgt.EdgeDistance(inf.Pos) - inf.Radius));   // 3칸 거리의 곡선값 (연속 곡선이라 accNear 보다 조금 작다)
        check(Near(pr, pc * defs.Units["recruit"].Accuracy) && Near(pi, pc) && Near(pv, pc * defs.Units["veteran"].Accuracy) && pc < rifle.AccNear && pc > rifle.AccNear - 0.02f,
              $"유닛 명중률 배수: 3칸 소총 곡선 {pc:0.000} × 징집병 {pr:0.000} · 정규군 {pi:0.000} · 정예 {pv:0.000}");
        var sharp = new WeaponDef { Key = "test_sharp", Range = 10, Accuracy = 0.9f, AccNear = 0.9f, AccFar = 0.9f };
        check(Near(w.HitChance(vet, tgt, sharp, 1f), World.HitCap) && w.HitChance(inf, tgt, sharp, 1f) < World.HitCap,
              $"명중률 상한 {World.HitCap}: 정예 × 0.9 곡선 = {0.9f * defs.Units["veteran"].Accuracy:0.00} → {w.HitChance(vet, tgt, sharp, 1f):0.00}");
    }

    /// <summary>최대 사거리 부근 실측 명중률 ≈ accFar (사격 판정이 거리 곡선을 쓰는지).</summary>
    static void FarHitRate(GameOptions opt, Action<bool, string> check)
    {
        var w = NewWorld(opt.Seed, fog: false);
        var c = Center(w);
        var shooter = w.SpawnUnit(Defs.Current.Units["infantry"], 1, c);
        w.Hold(1, new[] { shooter });
        var dummy = Dummy(w, 2, c + new Vector2(8.3f, 0));
        int shots = 0, hits = 0;
        for (int i = 0; i < 60 * 1200; i++)   // N34: 표본 2배 (첫 사격 반응으로 틱이 밀리면 600초 표본은 ±6% 경계를 오갔다)
        {
            dummy.Hp = 1000;
            shooter.Ammo = shooter.AmmoMax;   // 통계 표본만 본다 (탄약 회복 대기 없이)
            w.Step();
            int fired = w.Shots.Count(s => s.Tick == w.Tick - 1 && s.Faction == 1);
            shots += fired;
            if (fired > 0 && dummy.Hp < 1000) hits++;
        }
        float expect = w.HitChance(shooter, dummy);
        double rate = shots > 0 ? (double)hits / shots : 0;
        check(shots > 100 && Math.Abs(rate - expect) < 0.06, $"원거리(사거리 {dummy.EdgeDistance(shooter.Pos) - shooter.Radius:0.00}칸) 실측 명중 {rate * 100:0.0}% ≈ 곡선 {expect * 100:0.0}% (사격 {shots}회)");
    }

    // ---------------------------------------------------------------- §2 포탑

    static void Turret(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed, fog: false);
        var c = Center(w);
        var tank = w.SpawnUnit(defs.Units["tank_ft_male"], 1, c);
        w.Hold(1, new[] { tank });
        var dummy = Dummy(w, 2, c + new Vector2(0, 5));   // 아래쪽 (+π/2): 처음 포탑(-π/2)에서 180°
        float rate = defs.Units["tank_ft_male"].TurretTurnRate;
        int turn180 = (int)MathF.Ceiling(180f / rate / Config.SimDt);   // 120틱
        long first = -1; float worstErr = 0; int shots = 0;
        for (int i = 0; i < 60 * 8; i++)
        {
            dummy.Hp = 100000;
            w.Step();
            if (tank.LastShotTick == w.Tick - 1)
            {
                shots++;
                if (first < 0) first = w.Tick - 1;
                worstErr = MathF.Max(worstErr, w.AimError(tank));
            }
        }
        check(first >= turn180 - 10 && first <= turn180 + 20, $"포탑 180° 회전({rate}°/s = {turn180}틱) 뒤에야 첫 발 (첫 발 틱 {first}, 조준 {tank.Weapon!.AimTicks}틱)");
        check(shots > 1 && worstErr <= World.FireAngleTolerance + 1.5f * Deg, $"모든 사격은 포탑 오차 ≤ 5° (최대 {worstErr / Deg:0.0}°, {shots}발)");
        float trackErr = MathF.Abs(DetMath.WrapPi(tank.TurretFacing - DetMath.HalfPi));
        check(trackErr < 2 * Deg && MathF.Abs(DetMath.WrapPi(tank.Facing - tank.TurretFacing)) < 2 * Deg, $"교전 중 포탑은 표적을 겨눔 (오차 {trackErr / Deg:0.0}°)");
        dummy.Hp = 0;
        Steps(w, 2);
        w.Move(1, new[] { tank }, c + new Vector2(-6, 0));   // 서쪽으로 이동: 차체가 돌고 포탑은 차체 정면으로 복귀
        float maxGap = 0;
        for (int i = 0; i < 60 * 4; i++) { w.Step(); maxGap = MathF.Max(maxGap, MathF.Abs(DetMath.WrapPi(tank.Facing - tank.TurretFacing))); }
        float idleGap = MathF.Abs(DetMath.WrapPi(tank.Facing - tank.TurretFacing));
        check(tank.Target == null && idleGap < 2 * Deg && maxGap > 20 * Deg, $"표적이 없으면 포탑은 차체 정면으로 (이동 중 최대 벌어짐 {maxGap / Deg:0}°, 4초 뒤 {idleGap / Deg:0.0}°)");

        // 추격(차체 이동) 중에도 포탑은 표적 추적
        var w2 = NewWorld(opt.Seed, fog: false);
        var c2 = Center(w2);
        var t2 = w2.SpawnUnit(defs.Units["tank_ft_male"], 1, c2);
        var far = Dummy(w2, 2, c2 + new Vector2(26, 6));
        w2.Attack(1, new[] { t2 }, far);
        bool walkedTracking = false; int walkTicks = 0;
        for (int i = 0; i < 60 * 6; i++)
        {
            w2.Step();
            if (t2.Walking && t2.Target != null) { walkTicks++; if (walkTicks > 90 && w2.AimError(t2) < 6 * Deg) walkedTracking = true; }
        }
        check(walkTicks > 90 && walkedTracking, $"추격 이동 중에도 포탑이 표적을 추적 (이동 {walkTicks}틱, 오차 {w2.AimError(t2) / Deg:0.0}°)");

        // 보병은 발사 각 제한 없음 (포탑 없음) — TurretFacing 은 Facing 과 같다
        var inf = w2.SpawnUnit(defs.Units["infantry"], 1, c2 + new Vector2(-10, 0));
        Steps(w2, 5);
        check(!inf.HasTurret && Near(inf.TurretFacing, inf.Facing), "포탑 없는 유닛의 TurretFacing = Facing");
    }

    // ---------------------------------------------------------------- §3 경계 지도 · 안개 2단계

    static void AlertGrid(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed, fog: true);
        var c = Center(w);
        var inf = defs.Units["infantry"];
        var me = w.SpawnUnit(inf, 1, c);
        float mid = (inf.SightNear + inf.SightFar) / 2f;   // 평소 시야(14) 밖 · 최대 시야(24) 안 — 19칸
        var foe = Dummy(w, 2, c + new Vector2(0, mid));   // 남쪽 (동쪽 19칸에 마을 — 사선이 막힌다)
        int farTileX = (int)c.X, farTileY = (int)c.Y + inf.SightNear + 6;   // 20칸 자리
        Steps(w, 120);
        bool calm = me.Target == null && !w.IsAlerted(me) && w.SightOf(me) == inf.SightNear && w.Fog.State(1, farTileX, farTileY) == 1 && !w.Fog.CanSee(1, foe);
        check(calm, $"평소: 유효 시야 {w.SightOf(me)} (sightNear {inf.SightNear}) — {mid}칸 적은 식별 못 하고 {inf.SightNear + 6}칸 자리는 안개(상태 {w.Fog.State(1, farTileX, farTileY)})");

        w.MarkAlert(1, c);
        Steps(w, 60);
        bool alerted = w.IsAlerted(me) && w.SightOf(me) == inf.SightFar && w.Fog.State(1, farTileX, farTileY) == 2 && w.Fog.CanSee(1, foe) && me.Target == foe;
        check(alerted, $"경계: 시야 {w.SightOf(me)} (sightFar {inf.SightFar}) → {inf.SightNear + 6}칸 자리 밝아짐(상태 {w.Fog.State(1, farTileX, farTileY)}) · {mid}칸 적 식별 {me.Target == foe}");
        check(w.Fog.Version > 0 && w.IsAlertedAt(1, c) && !w.IsAlertedAt(2, c), "경계는 세력별 (세력 2 는 같은 구역이 경계 아님)");

        // 만료: 적이 사라지고 10초 지나면 평소로
        foe.Hp = 0;
        w.Stop(1, new[] { me });
        Steps(w, World.AlertTicks + 30);
        bool expired = !w.IsAlerted(me) && w.SightOf(me) == inf.SightNear;
        check(expired, $"마지막 식별 후 {World.AlertTicks / 60}초 지나면 경계 해제 (시야 {w.SightOf(me)})");
        Steps(w, 12);
        check(w.Fog.State(1, farTileX, farTileY) == 1, "경계 해제 뒤 안개도 유효 시야로 줄어듦");

        // 피격 → 피격자 구역 경계 (사격자는 멀리서 쏨)
        var w2 = NewWorld(opt.Seed, fog: true);
        var c2 = Center(w2);
        var victim = Dummy(w2, 1, c2);
        var sniper = w2.SpawnUnit(inf, 2, c2 + new Vector2(5, 0));
        w2.Hold(2, new[] { sniper });
        Steps(w2, 90);
        check(sniper.LastShotTick > 0 && w2.IsAlertedAt(1, c2) && w2.IsAlertedAt(2, sniper.Pos), "피격 · 사격은 각자 세력의 그 구역을 경계 상태로");

        // 동맹 공유: 1·3 같은 정적 팀 → 1 이 식별하면 3 의 같은 구역도 경계, 2 는 아님. 구역 단위(8×8)라 이웃 구역은 아님.
        var w3 = World.CreateStandard(GameMap.Generate(120, 120, opt.Seed, 4), defs);
        w3.AssignTeam(1, 1); w3.AssignTeam(3, 1);
        var p = new Vector2(40.5f, 40.5f);
        w3.MarkAlert(1, p);
        bool shared = w3.IsAlertedAt(1, p) && w3.IsAlertedAt(3, p) && !w3.IsAlertedAt(2, p) && w3.IsAlertedAt(1, new Vector2(47.9f, 47.9f)) && !w3.IsAlertedAt(1, new Vector2(48.5f, 40.5f));
        check(shared, "경계는 동맹과 공유되고 8×8 구역 단위");

        // 확산: 혼전에서 교전 중인 세력의 경계 구역이 여러 개로 늘어난다
        var w4 = NewWorld(opt.Seed + 1, fog: true);
        var c4 = Center(w4);
        // 시야 24 · 사거리 16 밖(±25)에서 출발해 서로 다가가며 교전 → 지나온 구역들이 경계 상태
        var a = Enumerable.Range(0, 24).Select(i => w4.SpawnUnit(inf, 1, c4 + new Vector2(-25 + (i / 4) * 0.9f, (i % 4) * 3.5f - 5))).ToList();   // 세로 10.5칸 → 8×8 구역 2개 이상에 걸침
        var b = Enumerable.Range(0, 24).Select(i => w4.SpawnUnit(inf, 2, c4 + new Vector2(25 - (i / 4) * 0.9f, (i % 4) * 3.5f - 5))).ToList();
        w4.AttackMove(1, a, c4 + new Vector2(28, 0));
        w4.AttackMove(2, b, c4 + new Vector2(-28, 0));
        Steps(w4, 60 * 10);
        int cells1 = w4.Alert.CountAlerted(1, w4.Tick, World.AlertTicks), cells2 = w4.Alert.CountAlerted(2, w4.Tick, World.AlertTicks);
        check(cells1 >= 2 && cells2 >= 2 && a.Concat(b).Where(u => u.Alive).Count(u => w4.IsAlerted(u)) * 100 >= 80 * a.Concat(b).Count(u => u.Alive), $"혼전 10초: 경계 구역 세력1 {cells1}개 · 세력2 {cells2}개, 생존 80% 이상 경계 상태 (미경계 {a.Concat(b).Count(u => u.Alive && !w4.IsAlerted(u))}/{a.Concat(b).Count(u => u.Alive)}; 교전 거리 100% 라 후열은 멈춰 쏜다)");
    }

    // ---------------------------------------------------------------- §3 은폐 탐지

    static void Concealment(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var scoutDef = defs.Units["scout"];
        float saved = scoutDef.Concealment;
        try
        {
            scoutDef.Concealment = 40;   // 탐지 확률 60%
            int Visible(World w, List<Unit> scouts) => scouts.Count(s => w.Fog.CanSee(1, s));
            (World W, List<Unit> Scouts, Unit Observer) Build(int seed)
            {
                var w = NewWorld(seed, fog: true);
                var c = Center(w);
                var obs = Dummy(w, 1, c);
                var list = new List<Unit>();
                for (int i = 0; i < 40; i++)
                {
                    float ang = i * 2.3999632f, r = 1.5f + (i % 5) * 0.5f;
                    list.Add(Dummy(w, 2, c + new Vector2(DetMath.Cos(ang) * r, DetMath.Sin(ang) * r), "scout"));
                }
                return (w, list, obs);
            }
            var (w1, s1, obs1) = Build(opt.Seed);
            var (w2, s2, _) = Build(opt.Seed);
            Steps(w1, 61); Steps(w2, 61);
            check(s1.All(s => s.DetectedUntil != null) && w1.Fog.IsVisible(1, s1[0].Pos), "60틱 안에 모든 은폐 유닛이 한 번씩 판정됨 (칸은 보이는 상태)");
            long sum = 0, n = 0; bool same = true;
            for (int i = 0; i < 1500; i++)
            {
                w1.Step(); w2.Step();
                int v1 = Visible(w1, s1), v2 = Visible(w2, s2);
                if (v1 != v2) same = false;
                sum += v1; n += s1.Count;
            }
            double frac = sum / (double)n;
            check(Math.Abs(frac - 0.6) < 0.06, $"은폐 40 → 보이는 시간 비율 {frac * 100:0.0}% ≈ 탐지 확률 60% (유닛 40 × 25초)");
            check(same && w1.ComputeChecksum() == w2.ComputeChecksum(), "탐지 판정은 결정적 (같은 시나리오 두 월드가 틱마다 같은 결과 · 같은 체크섬)");
            check(s1.All(s => w1.Fog.CanSee(2, s)) && w1.Fog.CanSee(1, obs1), "자기 유닛은 항상 보임 · 은폐 0 인 유닛은 칸만 보이면 보임");
            check(!s1.Any(s => w1.Fog.CanSee(1, s) && !w1.Fog.IsVisible(1, s.Pos)), "안개 속(칸 안 보임) 유닛은 탐지와 무관하게 안 보임");

            // 사격 → 3초 노출 (은폐 100 = 평소엔 절대 탐지 안 됨)
            scoutDef.Concealment = 100;
            var w3 = NewWorld(opt.Seed, fog: true);
            var c3 = Center(w3);
            var bait = Dummy(w3, 1, c3);
            var sniper = Dummy(w3, 2, c3 + new Vector2(4, 0), "scout");   // 먼저 비무장으로 세워 '쏘기 전' 상태 확인
            Steps(w3, 61);
            bool hiddenBefore = !w3.Fog.CanSee(1, sniper) && w3.Fog.IsVisible(1, sniper.Pos);
            sniper.Weapon = defs.Weapons["carbine"];
            long revealedTicks = 0, firingTicks = 0;
            for (int i = 0; i < 60 * 4; i++)
            {
                bait.Hp = 100000;
                w3.Step();
                firingTicks++;
                if (w3.Fog.CanSee(1, sniper)) revealedTicks++;
            }
            check(hiddenBefore && sniper.LastShotTick > 0 && revealedTicks > firingTicks - 90, $"은폐 100 정찰병: 쏘기 전엔 안 보이다가 사격 중엔 계속 보임 ({revealedTicks}/{firingTicks}틱)");
            sniper.Weapon = null;
            long lastShot = sniper.LastShotTick;
            long hiddenAt = -1;
            for (int i = 0; i < World.RevealTicks + 120 && hiddenAt < 0; i++) { w3.Step(); if (!w3.Fog.CanSee(1, sniper)) hiddenAt = w3.Tick - 1; }
            check(hiddenAt > 0 && hiddenAt - lastShot >= World.RevealTicks && hiddenAt - lastShot <= World.RevealTicks + 2, $"마지막 사격 후 {World.RevealTicks}틱(3초) 지나면 다시 은폐 (실제 {hiddenAt - lastShot}틱)");
            check(w3.ConcealmentOf(sniper) == 100 && w3.IsAlertedAt(1, c3), "사격 노출이 끝나면 은폐 계수 복귀 · 피격 세력은 경계 상태");
        }
        finally { scoutDef.Concealment = saved; }
    }

    // ---------------------------------------------------------------- §5 포병 범위 피해 (자주포 삭제 2026-10-19: 곡사포(heavy_howitzer) 몸체에 시험 무기를 달아 검증)

    static void SplashFalloff(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var artDef = defs.Units["heavy_howitzer"];
        // 범위 감소 · 아군도 피해 (포병만 아군 오사, pursuit2 — 퍼짐 0 시험용 무기로 착탄점 고정; 아군이 표적 옆이라 포수 규율 때문에 명시 공격 명령)
        var w4 = NewWorld(opt.Seed, fog: false);
        var c4 = Center(w4);
        var g4 = w4.SpawnUnit(artDef, 1, c4);
        g4.Facing = 0;   // 방열 사계 (traverseArc): 적 쪽 (+x) 을 보고 방열
        var tw = new WeaponDef { Key = "test_howitzer", Name = "시험 곡사포", Damage = 40, Range = 26, MinRange = 6, Cooldown = 6, Accuracy = 1, AimTime = 0.5f, HoldTime = 0.5f, Splash = 2, SplashEdge = 0.3f, Scatter = 0 };
        g4.Weapon = tw;
        w4.Hold(1, new[] { g4 });
        var tPos = c4 + new Vector2(12, 0);
        var t4 = Dummy(w4, 2, tPos);
        var e2 = Dummy(w4, 2, tPos + new Vector2(0, 1.0f));     // 중심 거리 1.0 − 반경 0.35 = 0.65 → 비율 1 − 0.7 × 0.325
        var e3 = Dummy(w4, 2, tPos + new Vector2(0, 2.6f));     // 2.25 > 2 → 피해 없음
        var ally = Dummy(w4, 1, tPos + new Vector2(0, -1.0f));  // 아군: e2 와 같은 거리 → 같은 피해 (pursuit2)
        w4.Attack(1, new[] { g4 }, t4);
        for (int i = 0; i < 60 * 15 && g4.LastShotTick < 0; i++) w4.Step();   // 방열 6초 + 조준
        float fTarget = World.SplashFactor(tw, 0), fE2 = World.SplashFactor(tw, Vector2.Distance(tPos, e2.Pos) - e2.Radius);
        float ar = t4.ArmorValue;   // 보병 장갑 0 (교전 2차 추가 D)
        float expT = 100 - (40 * fTarget - ar), expE2 = 100 - (40 * fE2 - ar);
        bool fall = g4.LastShotTick > 0 && Near(t4.Hp, expT, 0.6f) && Near(e2.Hp, expE2, 0.6f) && e3.Hp == 100 && Near(ally.Hp, expE2, 0.6f);
        check(fall, $"범위 피해: 표적 {t4.Hp:0.0} (기대 {expT:0.0}) · 0.65칸 옆 {e2.Hp:0.0} (기대 {expE2:0.0}) · 2.25칸 밖 {e3.Hp:0} · 아군 {ally.Hp:0.0} (기대 {expE2:0.0}, 포병 오사)");
        check(Near(World.SplashFactor(tw, 0), 1) && Near(World.SplashFactor(tw, 1), 0.65f) && Near(World.SplashFactor(tw, 2), 0.3f) && Near(World.SplashFactor(tw, 5), 0.3f),
              "범위 감소 곡선: 중심 1.0 → 반경 절반 0.65 → 가장자리 0.3");
        check(w4.Impacts.Count == 1 && w4.Impacts[0].Impact == tPos, "퍼짐 0 이면 착탄점 = 표적 위치");
    }

    // ---------------------------------------------------------------- §5.1 야전포 방열 · 철수 (견인포 삭제 후 heavy_field_gun)

    static void DeployPack(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var towedDef = defs.Units["heavy_field_gun"];
        var how = defs.Weapons[towedDef.Weapon!];
        int setup = World.SetupTicks(towedDef), pack = World.PackTicks(towedDef), aim = how.AimTicks;
        check(towedDef.SetupTime > 0 && towedDef.PackTime > 0 && !towedDef.IsArmored && !World.IsIndirect(how),
              $"야전포 정의: 방열 {towedDef.SetupTime}초 · 철수 {towedDef.PackTime}초 · 병과 {towedDef.Category}(기갑 아님 · 연료 없음) · 사거리 {how.Range}");
        check(defs.Units["tank_ft_male"].SetupTime == 0 && defs.Units["infantry"].SetupTime == 0, "전차 · 보병은 방열 없음");

        // 사수: 표적이 사거리에 있어도 방열(4초)이 끝난 뒤 조준(3초) → 첫 발. 방열 전엔 절대 안 쏜다.
        var w = NewWorld(opt.Seed, fog: false);
        var c = Center(w);
        var gun = w.SpawnUnit(towedDef, 1, c);
        setup = World.SetupTicksOf(gun) + World.DeployDelayTicksOf(gun);   // arty3: 대포 방렬 = 포마다 고정 ±15% + 시작 지연 0~3초
        gun.Facing = 0;   // 방열 사계 (traverseArc): 적 쪽을 보고 시작
        var foe = Dummy(w, 2, c + new Vector2(14, 0));
        w.Hold(1, new[] { gun });
        long deployedAt = -1, first = -1; bool firedUndeployed = false, progressOk = true; float lastProg = -1;
        for (int i = 0; i < 60 * 12 && first < 0; i++)
        {
            foe.Hp = 100000;
            bool wasDeployed = gun.Deployed;
            w.Step();
            if (gun.Deployed && deployedAt < 0) deployedAt = w.Tick - 1;
            if (!gun.Deployed) { if (gun.DeployProgress < lastProg - 1e-4f) progressOk = false; lastProg = gun.DeployProgress; }
            if (gun.LastShotTick == w.Tick - 1) { first = w.Tick - 1; if (!wasDeployed) firedUndeployed = true; }
        }
        check(deployedAt >= setup - 1 && deployedAt <= setup + 2 && progressOk, $"사수 명령 뒤 방열 {setup}틱 (완료 틱 {deployedAt}, 진행도 단조 증가 {progressOk})");
        check(first > 0 && !firedUndeployed && first >= deployedAt + aim - 1 && first <= deployedAt + aim + 3, $"첫 발은 방열 완료 + 조준 {aim}틱 뒤 (첫 발 틱 {first}), 방열 전 사격 없음");
        check(gun.Velocity == Vector2.Zero && gun.Deploy == DeployPhase.Deployed && gun.DeployProgress == 1f, "방열 상태: 정지 · 진행도 1");

        // 이동 명령 → 철수(3초) 동안 움직이지 않고 쏘지도 않다가 출발
        long moveAt = w.Tick, firstWalk = -1; bool shotWhilePacking = false, packingSeen = false;
        w.Move(1, new[] { gun }, c + new Vector2(2, 0));
        for (int i = 0; i < 60 * 6 && firstWalk < 0; i++)
        {
            foe.Hp = 100000;
            w.Step();
            if (gun.Deploy == DeployPhase.Packing) packingSeen = true;
            if (gun.LastShotTick == w.Tick - 1) shotWhilePacking = true;
            if (gun.Walking) firstWalk = w.Tick - 1;
        }
        long packDelay = firstWalk - moveAt;
        check(packingSeen && firstWalk > 0 && packDelay >= pack && packDelay <= pack + 14 && !shotWhilePacking && gun.Deploy == DeployPhase.Packed,
              $"이동 명령: 철수 {pack}틱 뒤 출발 (실제 {packDelay}틱, 명령 반응 지연 포함) · 철수 중 사격 없음 · 출발 자세 Packed");

        // 도착(이동 자세) 뒤 다시 사거리에 적이 있으면 → 다시 방열해야 쏜다 (이동 자세로는 사격 불가)
        for (int i = 0; i < 60 * 12 && gun.Order != UnitOrder.Idle; i++) { foe.Hp = 100000; w.Step(); }
        long arrived = w.Tick; long reacq = -1, shot2 = -1;
        for (int i = 0; i < 60 * 12 && shot2 < 0; i++)
        {
            foe.Hp = 100000;
            w.Step();
            if (gun.Target == foe && reacq < 0) reacq = w.Tick - 1;
            if (gun.LastShotTick == w.Tick - 1) shot2 = w.Tick - 1;
        }
        check(gun.Order == UnitOrder.Idle || shot2 > 0, $"도착 후 자동 교전 (명령 {gun.Order})");
        int spot2 = w.InOwnSight(gun, foe) ? 0 : (int)MathF.Round(how.SpotterDelay / Config.SimDt);   // 8칸 물러났으니 자기 시야(14) 밖 → 관측 지연이 붙는다
        int turn = (int)MathF.Ceiling(DetMath.Pi / (14f * Config.SimDt));   // 사계 대포는 표적 쪽으로 다 돈 뒤 방열 (뒤로 물러났으니 최대 180°)
        check(reacq >= 0 && shot2 >= reacq + setup + aim + spot2 - 2 && shot2 <= reacq + setup + aim + spot2 + 4 + turn, $"이동 자세에서 적 식별 → 방열 {setup} + 조준 {aim} + 관측 {spot2}틱 뒤 사격 (식별 {reacq}, 사격 {shot2}, 도착 {arrived})");

        // 방열 중 이동 명령 → 진행한 만큼만 비례해 짧게 철수
        var w2 = NewWorld(opt.Seed, fog: false);
        var c2 = Center(w2);
        var g2 = w2.SpawnUnit(towedDef, 1, c2);
        w2.Hold(1, new[] { g2 });
        Steps(w2, World.DeployDelayTicksOf(g2) + World.SetupTicksOf(g2) / 2);   // arty3: 시작 지연 뒤 방렬 절반
        bool halfway = g2.Deploy == DeployPhase.Deploying && g2.DeployProgress > 0.4f && g2.DeployProgress < 0.6f;
        w2.Move(1, new[] { g2 }, c2 + new Vector2(8, 0));
        long m0 = w2.Tick, walk2 = -1;
        for (int i = 0; i < 60 * 5 && walk2 < 0; i++) { w2.Step(); if (g2.Walking) walk2 = w2.Tick - 1; }
        check(halfway && walk2 > 0 && walk2 - m0 <= pack / 2 + 14 && walk2 - m0 >= pack / 2 - 2, $"방열 절반에서 이동 명령 → 철수는 절반({pack / 2}틱)만 (실제 {walk2 - m0}틱)");
    }

    // ---------------------------------------------------------------- 직사 · 곡사 구분 · 사선 검사 (ROADMAP §2-3 대포 2종)

    static Building PlaceNear(World w, string key, int owner, Vector2 near)
    {
        var def = Defs.Current.Buildings[key];
        for (int r = 0; r < 20; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
            if (w.PlaceBuilding(def, owner, (int)near.X + dx, (int)near.Y + dy) is { } b) return b;
        throw new InvalidOperationException("빈 자리 없음");
    }

    /// <summary>건물에서 dist 칸 떨어진, 사선이 트인(지형 · 다른 건물에 안 막힌) 지점 (4방향 중 처음 것).</summary>
    public static Vector2 ClearSpot(World w, Building b, float dist)
    {
        foreach (var dir in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
        {
            var p = b.Pos + dir * dist;
            var (tx, ty) = GameMap.TileOf(p);
            if (w.Map.IsWalkable(tx, ty) && w.Map.HasLineOfSight(b.Pos, p, b.Id, 0)) return p;
        }
        return b.Pos + new Vector2(dist, 0);
    }

    static void DirectFire(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var rifle = defs.Weapons["rifle"]; var tankGun = defs.Weapons["ft_gun"]; var how = defs.Weapons["howitzer_heavy"];
        check(!rifle.Indirect && rifle.Los && !tankGun.Indirect && tankGun.Los && how.Indirect && !how.Los && World.IsIndirect(how) && !World.IsIndirect(rifle),
              "무기 종류: 소총 · 전차포 · 포탑 직사 + 사선 검사 (교전 2차: 소총도 켬) · 곡사포 곡사");

        // 벽(아군 보급소 2×2) 뒤의 적: 사선 검사 무기(전차포 · 소총)는 사수 상태에서 쏘지 못하고 표적도 잡지 않는다 (교전 2차: 소총도 사선 검사). 유닛은 사선을 막지 않는다.
        var w = NewWorld(opt.Seed, fog: false);
        var c = Center(w);
        var (wx, wy) = GameMap.TileOf(c + new Vector2(5, -1));
        var wall = w.PlaceBuilding(TestDefs.Wall(2), 1, wx, wy);
        check(wall != null && !w.Map.HasLineOfSight(c, c + new Vector2(10, 0)) && w.Map.HasLineOfSight(c, c + new Vector2(10, 0), wall!.Id, 0),
              "사선 검사: 건물이 막는다 · 그 건물 Id 를 무시하면 뚫린다 (사격자 자신의 발자국)");
        var tank = w.SpawnUnit(defs.Units["tank_ft_male"], 1, c);
        var inf = w.SpawnUnit(defs.Units["infantry"], 1, c);   // 전차와 같은 자리 (벽 뒤)
        var flank = w.SpawnUnit(defs.Units["infantry"], 1, c + new Vector2(0, 4));   // 벽 아래쪽 — 사선 트임, 사이에 아군 유닛이 있어도 막지 않음
        var blocker = Dummy(w, 1, c + new Vector2(5, 2));
        w.Hold(1, new[] { tank, inf, flank });
        var foe = Dummy(w, 2, c + new Vector2(10, 0));
        for (int i = 0; i < 60 * 8; i++) { foe.Hp = 100000; w.Step(); }
        check(tank.LastShotTick < 0 && tank.Target == null && inf.LastShotTick < 0 && inf.Target == null && !w.HasFiringLine(tank, foe, tankGun) && !w.HasFiringLine(inf, foe, rifle),
              $"벽 뒤 적: 전차포 · 소총 모두 사격 없음 · 표적 없음 ({tank.Target?.Id}/{inf.Target?.Id})");
        check(flank.LastShotTick > 0 && w.HasFiringLine(flank, foe, rifle) && blocker.Alive, $"벽 옆 소총병은 사이에 아군 유닛({blocker.Id})이 있어도 사선이 트여 사격 (틱 {flank.LastShotTick})");
        // 공격 명령 → 사선이 트일 때까지 접근 (벽을 돌아) → 사격
        w.Attack(1, new[] { tank }, foe);
        long shot = -1; bool moved = false;
        for (int i = 0; i < 60 * 30 && shot < 0; i++) { foe.Hp = 100000; w.Step(); if (tank.Walking) moved = true; if (tank.LastShotTick == w.Tick - 1) shot = w.Tick - 1; }
        check(moved && shot > 0 && w.HasFiringLine(tank, foe, tankGun), $"공격 명령: 사선이 막히면 다가가서 트인 뒤 사격 (이동 {moved}, 사격 틱 {shot})");
    }

    // ---------------------------------------------------------------- 저장 동일성 (경계 · 조준 · 포탑 · 탐지 표 중)

    static void SaveLoad(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var scoutDef = defs.Units["scout"];
        float saved = scoutDef.Concealment;
        try
        {
            scoutDef.Concealment = 40;
            var w = NewWorld(opt.Seed + 7, fog: true);
            var c = Center(w);
            var tank = w.SpawnUnit(defs.Units["tank_ft_male"], 1, c + new Vector2(-6, 0));
            w.Hold(1, new[] { tank });
            Dummy(w, 2, c + new Vector2(-6, 5));
            var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, c + new Vector2(0, -8));
            gun.Facing = 0;   // 방열 사계 (traverseArc): 적 쪽 (+x) 을 보고 방열
            w.Hold(1, new[] { gun });
            Dummy(w, 1, c + new Vector2(20, -8), "scout");
            var ft = Dummy(w, 2, c + new Vector2(22, -8));
            var towed = w.SpawnUnit(defs.Units["heavy_field_gun"], 1, c + new Vector2(-4, -12));
            w.Hold(1, new[] { towed });   // 방열 중(240틱) 에 저장
            for (int i = 0; i < 12; i++) Dummy(w, 2, c + new Vector2(6 + (i % 4) * 0.8f, 4 + (i / 4) * 0.8f), "scout");
            var squad = Enumerable.Range(0, 8).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, c + new Vector2(2, 2 + i * 0.7f))).ToList();
            var foes = Enumerable.Range(0, 8).Select(i => w.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(14, 2 + i * 0.7f))).ToList();
            w.AttackMove(1, squad, c + new Vector2(14, 4));
            w.AttackMove(2, foes, c + new Vector2(2, 4));
            Steps(w, 200);   // 포탑 회전 중 · 자주포 방열(180틱) 끝나고 관측 조준 중 · 견인포 방열 중(240틱) · 탐지 표 생성 · 경계 구역 여럿
            bool midState = tank.AimLeft > 0 || tank.Target != null;
            bool midAim = gun.Deploy == DeployPhase.Deploying;   // (더 가까운 보병을 잡을 수도 있다 — 조준 중이면 충분)
            int cells = w.Alert.CountAlerted(1, w.Tick, World.AlertTicks);
            bool hasDet = w.Units.Any(u => u.DetectedUntil != null);
            bool midDeploy = towed.Deploy == DeployPhase.Deploying && towed.DeployLeft > 0;
            check(midState && midAim && cells > 0 && hasDet && midDeploy, $"저장 시점 상태: 전차 교전 {midState} · 포병 조준 중 {midAim} (남은 {gun.AimLeft}틱) · 견인포 방열 중 {midDeploy} · 경계 구역 {cells} · 탐지 표 {hasDet}");

            var bytes = Snapshot.Save(w, Meta);
            var y = Snapshot.Load(bytes, defs).World;
            var gunY = (Unit)y.Find(gun.Id)!;
            var tankY = (Unit)y.Find(tank.Id)!;
            var towedY = (Unit)y.Find(towed.Id)!;
            bool restored = gunY.AimLeft == gun.AimLeft && tankY.TurretFacing == tank.TurretFacing && y.Alert.CountAlerted(1, y.Tick, World.AlertTicks) == cells
                            && towedY.Deploy == towed.Deploy && towedY.DeployLeft == towed.DeployLeft
                            && y.ComputeChecksum() == w.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
            check(restored, "불러온 월드: 조준 틱 · 포탑 각 · 경계 구역 · 체크섬 같음, 다시 저장하면 바이트 동일");
            bool same = true; long diverge = -1;
            for (int i = 0; i < 600; i++)
            {
                w.Step(); y.Step();
                if (i % 25 == 24 && w.ComputeChecksum() != y.ComputeChecksum()) { same = false; diverge = w.Tick; break; }
            }
            check(same && w.ComputeChecksum() == y.ComputeChecksum(), $"저장 → 불러오기 뒤 10초 이어 돌려도 체크섬 동일 (사격 {w.Shots.Count + y.Shots.Count > 0}{(same ? "" : $", 어긋남 틱 {diverge}")})");
        }
        finally { scoutDef.Concealment = saved; }
    }
}
