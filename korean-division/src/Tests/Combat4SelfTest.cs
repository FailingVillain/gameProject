using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 교전 4차 검증 (ROADMAP 포병 · 수류탄 · 추가 병종 · 행진 사격 · 기병 · 시위대 · 봉천 · 중기관총 · 이름 규칙):
/// 유닛 정의 수치, 저격수 장교 우선 · 발사 간격 · 은폐, 곡사포 관측 (장교/정찰병), 직사포 폭발, 기병 (속도 · 돌격만 · 충격 · 마비 · 한 번), 전차 근접 불가,
/// 수류탄 (명령 · 사거리 · 피해 · 엄폐 무시 · 제압 · 전차 · 자동 투척 · 회복), 행진 사격 (원뿔 · 간격 · 명중 · 속보/제압 제외 · 차량), 저장.
/// </summary>
public static class Combat4SelfTest
{
    static readonly SnapshotMeta Meta = new("combat4", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();   // 지휘 계통 지연 도입 전 이동 · 시간 기대값 (지연 자체는 SquadSelfTest)
        try
        {
            Data(check);
            Sniper(opt, check);
            Howitzer(opt, check);
            Guns(opt, check);
            Cavalry(opt, check);
            NoMeleeVsArmor(opt, check);
            Grenades(opt, check);
            MarchingFire(opt, check);
            Bursts(opt, check);
            SaveLoad(opt, check);
        }
        catch (Exception e) { check(false, $"교전 4차 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World NewWorld(int seed)
    {
        var w = TestDefs.Strip(World.CreateStandard(MapFile.Load("flat4", seed), Defs.Current));
        w.Fog.Enabled = false;
        return w;
    }

    /// <summary>flat4 맵의 시설 없는 가로 띠 한가운데 (y=56.5 근처, x 40~90 사이는 시설 · 사선 방해 없음).</summary>
    static Vector2 Center(World w) => new(64.5f, 56.5f);

    /// <summary>무기 없는 정지 표적 (체력 1000).</summary>
    static Unit Dummy(World w, int faction, Vector2 pos, string kind = "infantry")
    {
        var u = w.SpawnUnit(Defs.Current.Units[kind], faction, pos);
        u.Weapon = null;
        u.Hp = 1000;
        w.Hold(faction, new[] { u });
        return u;
    }

    static Unit Spawn(World w, string kind, int faction, Vector2 pos) => w.SpawnUnit(Defs.Current.Units[kind], faction, pos);
    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    // ---------------------------------------------------------------- 정의 · 수치

    static void Data(Action<bool, string> check)
    {
        var d = Defs.Current;
        var U = d.Units; var W = d.Weapons;
        check(!U.ContainsKey("machinegunner") && !U.ContainsKey("at_infantry") && !U.ContainsKey("armored_car")
              && U.ContainsKey("heavy_machinegunner") && U.ContainsKey("heavy_at_rifleman") && U.ContainsKey("tank_armored_car"),
              "이름 규칙: machinegunner/at_infantry/armored_car 가 heavy_*/tank_* 로 바뀜");
        var inf = U["infantry"];
        check(U["infantry_regular"].Hp == inf.Hp && U["infantry_regular"].Weapon == inf.Weapon && U["infantry_regular"].Speed == inf.Speed, "정규병 = 기본 보병 스펙");
        var guard = U["infantry_guard"];
        check(guard.Hp == 125 && guard.Weapon == "rifle_mauser98" && W[guard.Weapon!].Range == 25 && W[guard.Weapon!].Damage == 45 && W.ContainsKey("rifle_long"), "시위대: 체력 125, 98식 마우저 (피해 45 · 사거리 25), 장총 정의는 유지");
        var feng = U["infantry_fengtian"];
        check(feng.Melee == "dadao" && feng.Melee != "bayonet" && feng.Hp == inf.Hp && feng.Weapon == inf.Weapon && U["officer_fengtian"].Melee == "sabre" && U["officer_fengtian"].IsOfficer,
              "봉천 보병: 근접 = 대도 (착검 없음), 나머지는 일반 보병과 같음 / 봉천 장교 = 일반 장교");
        var sn = W["sniper_rifle"];
        check(sn.Range == 32 && sn.AccNear == 0.45f && sn.AccFar == 0.35f && sn.CooldownMin == 7 && sn.CooldownMax == 9 && sn.PreferOfficer && U["infantry_sniper"].Concealment > 0,
              "저격수: 사거리 32, 명중 0.45 → 0.35, 간격 7~9초, 장교 우선, 은폐 > 0");
        check(U["heavy_maxim"].Hp == 250 && U["heavy_maxim"].Speed == 2.0f && U["heavy_maxim"].SetupTime == 7 && U["heavy_maxim"].PackTime == 5 && W[U["heavy_maxim"].Weapon!].Range == 32 && W[U["heavy_maxim"].Weapon!].Damage == W["lmg"].Damage && U["heavy_maxim"].SetupTime > 0, "중기관총 heavy_maxim: 체력 250 · 속도 2 · 방열 7/5, 사거리 32 (arty3) · 경기관총과 같은 피해, 거치 필요");
        check(U["heavy_maxim"].MinDeployStay >= 30f && U["heavy_maxim"].StayLeaveDist > 0 && Defs.Current.CombatRules.CavMeleeMaxTargets == 0, "bal2: 맥심 방열 유지 (최소 대기 30초 · 이탈 거리) + 기병 근접 이탈 제한 해제");
        var gi = W["gun_infantry"]; var gf = W["gun_field"]; var hw = W["howitzer_heavy"];
        check(gi.Range == 40 && gi.CooldownMin == 3.0f && gi.CooldownMax == 3.6f && gi.Damage == 70 && gi.Splash == 1.5f && gi.Suppress == 250 && U["heavy_infantry_gun"].SetupTime == 9 && U["heavy_infantry_gun"].PackTime == 7 && U["heavy_infantry_gun"].Hp == 200,
              "보병포: 사거리 40 (N11), 간격 3.0~3.6, 피해 70 · 반경 1.5 · 제압 250 (tweak1 N44), 방열 9/7, 체력 200");
        check(gf.Range == 80 && gf.CooldownMin == 5 && gf.CooldownMax == 6 && gf.Damage == 100 && gf.Splash == 1.5f && U["heavy_field_gun"].SetupTime == 15 && U["heavy_field_gun"].PackTime == 12 && U["heavy_field_gun"].Hp == 300,
              "야전포: 사거리 80 (N11), 간격 5~6, 피해 100 (arty3), 반경 1.5, 방열 15/12, 체력 300");
        check(hw.Range == 120 && hw.CooldownMin == 14 && hw.CooldownMax == 17 && hw.Damage == 100 && hw.Splash == 3f && hw.SpotterOnly && U["heavy_howitzer"].SetupTime == 20 && U["heavy_howitzer"].PackTime == 15 && U["heavy_howitzer"].Hp == 350,
              "곡사포: 사거리 120 · 간격 14~17 (N11), 피해 100, 반경 3 (arty3), 방열 20/15, 체력 350, 관측 전용");
        check(!IsInd(gi) && !IsInd(gf) && gi.Los && gf.Los && IsInd(hw) && gi.Range > W["rifle"].Range && hw.Range > gf.Range && gf.Range > gi.Range
              && gi.CooldownMean < gf.CooldownMean && gf.CooldownMean < hw.CooldownMean && hw.Damage >= gf.Damage && gf.Damage >= gi.Damage && gf.Splash >= gi.Splash,
              "포 순서: 직사(보병포·야전포, 사선) / 곡사(곡사포), 사거리 곡사포 > 야전포 > 보병포, 위력 곡사포 ≥ 야전포 ≥ 보병포 (tweak1 · arty3 야전포 100), 연사 보병포 > 야전포 > 곡사포");
        var lmg = W["lmg"]; var hmg = W["hmg"]; var rifle = W["rifle"];
        check(U["heavy_machinegunner"].Weapon == "lmg" && U["heavy_maxim"].Weapon == "hmg" && lmg.Cooldown == 0.26f && !lmg.RandomCooldown && hmg.Cooldown == 0.125f && !hmg.RandomCooldown
              && MathF.Abs(lmg.AccNear - 0.09f) < 1e-4f && MathF.Abs(lmg.AccFar - 0.04f) < 1e-4f && MathF.Abs(hmg.AccNear - 0.3f) < 1e-4f && MathF.Abs(hmg.AccFar - 0.12f) < 1e-4f
              && lmg.Suppress == 24 && hmg.Suppress == 36 && lmg.Ammo == 300 && hmg.Ammo == 600 && lmg.Spray == 1.5f && hmg.Spray == 1.5f,
              "기관총: 경기관총 0.26초 (점사 5~10발 + 휴지 2.5~4.5초, N58 하향) · 제압 24 · 탄 80 / 중기관총 0.125초 · 제압 36 · 탄 320, 경기관총 명중 근 0.09 · 원 0.04 (N58 하향), 흩뿌림 유지");
        var cav = U["cavalry_default"];
        check(cav.Mounted && MathF.Abs(cav.Speed / inf.Speed - 1.5f) < 0.01f && cav.ChargeSpeedMul == 1.6f && cav.StaminaMul == 3 && cav.Weapon == "carbine" && cav.Melee == "sabre",
              "기병: 속도 보병 ×1.5, 돌격 ×1.6, 스태미나 ×3, 카빈 + 곡도 (돌격 충격 피해 보너스 없음)");
        var withGren = U.Values.Where(u => u.Grenades > 0).Select(u => u.Key).ToList();
        check(withGren.Count == 1 && withGren[0] == "infantry_assault" && U["infantry_assault"].Grenades == 2, $"수류탄 보유: 돌격보병 2개만 ({string.Join(",", withGren)})");
        var g = d.Grenade;
        check(g.Range == 5 && g.Fuse == 1.5f && g.Radius == 1.5f && g.Damage == 60 && g.EdgeDamage == 20 && g.Suppress == 300 && g.TankDamage == 40 && g.AutoCoverRange == 4 && g.AutoTankRange == 3 && g.RegenSeconds == 180,
              "수류탄 수치: 사거리 5 · 신관 1.5 · 반경 1.5 · 피해 60→20 · 제압 300 · 전차 40 · 자동 4/3 · 회복 180초");
        var c = d.CombatRules;
        check(c.MarchArcDeg == 45 && c.MarchSightFrac == 0.7f && c.MarchIntervalMul == 1.5f && c.MarchAccMul == 0.4f && c.MarchSmgIntervalMul == 1.2f && c.MarchSmgAccMul == 0.7f && c.MarchMaxSuppress == 400,
              "행진 사격 수치: ±45°, 시야 절반, 간격 ×1.5, 명중 ×0.4 (기관단총 ×1.2/×0.7), 제압 400");
        check(d.Terrain.TrenchHit == 0.5f && d.Terrain.TrenchSuppress == 0.5f, "참호 지형: 맞을 확률 ×0.5 (tweak1), 제압 ×0.5");
        check(U["tank_ft_female"].MarchFire && U["tank_armored_car"].MarchFire && !U["tank_ft_male"].MarchFire && !U["heavy_machinegunner"].MarchFire && !U["heavy_howitzer"].MarchFire && !cav.MarchFire && U["infantry"].MarchFire,
              "행진 사격 대상: 보병 계열 + 기관총 전차 (수컷형 · 중화기 · 포 · 기병 제외)");
    }

    static bool IsInd(WeaponDef w) => World.IsIndirect(w);

    // ---------------------------------------------------------------- 저격수

    static void Sniper(GameOptions opt, Action<bool, string> check)
    {
        var w = NewWorld(opt.Seed + 41);
        var c = Center(w);
        var sn = Spawn(w, "infantry_sniper", 1, c + new Vector2(-20, 0));
        w.Hold(1, new[] { sn });
        var near = Dummy(w, 2, c + new Vector2(-10, 0));        // 가까운 보병 (사거리 안)
        var off = Dummy(w, 2, c + new Vector2(-3, 1), "officer");   // 먼 장교 (사거리 24 안, 거리 ≈ 17.1)
        var shotTicks = new List<long>(); int offShots = 0;
        for (int t = 0; t < 60 * 70; t++)
        {
            w.Step();
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == sn.Id) { shotTicks.Add(s.Tick); if (Vector2.Distance(s.To, off.Pos) < 1f) offShots++; }
            near.Hp = 1000; off.Hp = 1000;
        }
        check(sn.Target is { } tg && tg.Id == off.Id && offShots >= shotTicks.Count - 1 && shotTicks.Count >= 5, $"저격수: 더 가까운 보병 대신 장교를 노림 (총 {shotTicks.Count}발, 장교 쪽 {offShots}발)");
        var gaps = shotTicks.Zip(shotTicks.Skip(1), (a, b) => (b - a) * Config.SimDt).ToList();
        check(gaps.Count >= 4 && gaps.All(g => g >= 7f - 0.1f && g <= 9f + 0.1f), $"저격수 발사 간격 7~9초: {string.Join(", ", gaps.Select(g => g.ToString("0.00")))}");
        check(w.BaseConcealment(sn) == sn.Def.Concealment && w.TerrainConcealment(sn) == 0f, $"저격수 은폐 {w.BaseConcealment(sn)} (숲/고지 보정 훅 = {w.TerrainConcealment(sn)}, 지형 추가 시 채움)");
        // 명중 곡선 (먼 거리도 높다): 24칸에서 0.35, 소총은 0.06
        float snR = sn.Weapon!.Range, rfR = Defs.Current.Weapons["rifle"].Range;
        var ac = World.AccuracyCurve(sn.Weapon!, snR);
        check(MathF.Abs(ac - 0.35f) < 1e-4f && World.AccuracyCurve(sn.Weapon!, 0f) == 0.45f && ac > World.AccuracyCurve(Defs.Current.Weapons["rifle"], rfR) * 3, $"저격 명중 곡선 {snR}칸 {ac:0.00} (소총 {rfR}칸 {World.AccuracyCurve(Defs.Current.Weapons["rifle"], rfR):0.00})");
    }

    // ---------------------------------------------------------------- 곡사포 관측

    static void Howitzer(GameOptions opt, Action<bool, string> check)
    {
        foreach (var spotterKind in new[] { "officer", "scout" })
        {
            var w = NewWorld(opt.Seed + 42);
            var c = Center(w);
            var h = Spawn(w, "heavy_howitzer", 1, c + new Vector2(-15, 0));
            h.Facing = 0;   // 방열 사계 (traverseArc): 적 쪽 (+x) 을 보고 방열
            w.Hold(1, new[] { h });
            var foe = Dummy(w, 2, c + new Vector2(15, 0));   // 거리 30 (사거리 40 안, 곡사포 시야 밖)
            int ammo0 = h.Ammo;
            Steps(w, 60 * 25);
            bool silent = h.Ammo == ammo0 && foe.Hp == 1000 && w.Impacts.Count == 0;
            var sp = Dummy(w, 1, c + new Vector2(5, 0), spotterKind);   // 적과 10칸 (관측 유닛 시야 안)
            Steps(w, 60 * 25);
            bool fired = h.Ammo < ammo0 && foe.Hp < 1000;
            // 관측 유닛이 죽으면 다시 침묵
            sp.Hp = 0; Steps(w, 5 + World.FlightTicks(h.Weapon!, 40));   // 이미 날아가는 포탄은 떨어진다 (ShellFlight.cs)
            int ammo1 = h.Ammo; float hp1 = foe.Hp; Steps(w, 60 * 25);
            check(silent && fired && foe.Hp == hp1, $"곡사포 + {spotterKind}: 관측 유닛 없으면 침묵 ({silent}), 있으면 사격 ({fired}, 탄 {ammo0}→{ammo1}), 죽으면 다시 침묵 ({foe.Hp == hp1})");
        }
    }

    // ---------------------------------------------------------------- 직사포

    static void Guns(GameOptions opt, Action<bool, string> check)
    {
        var w = NewWorld(opt.Seed + 43);
        var c = Center(w);
        var gun = Spawn(w, "heavy_infantry_gun", 1, c + new Vector2(-12, 0));
        w.Hold(1, new[] { gun });
        var a = Dummy(w, 2, c + new Vector2(0, 0));
        var b = Dummy(w, 2, c + new Vector2(0.6f, 0.2f));    // 폭발 반경 0.8 안
        var far = Dummy(w, 2, c + new Vector2(0, 4));         // 반경 밖
        Steps(w, 60 * 30);
        bool splash = a.Hp < 1000 && far.Hp == 1000 && w.Impacts.Count > 0 || (a.Hp < 1000 && b.Hp < 1000);
        check(a.Hp < 1000 && far.Hp == 1000 && gun.Ammo < gun.AmmoMax && gun.Deployed, $"보병포: 방열 후 직사 사격 (표적 체력 {a.Hp:0}, 반경 밖 체력 {far.Hp:0}, 탄 {gun.Ammo}/{gun.AmmoMax})");
        // 전차에도 피해
        var w2 = NewWorld(opt.Seed + 44);
        var c2 = Center(w2);
        var fg = Spawn(w2, "heavy_field_gun", 1, c2 + new Vector2(-20, 0));
        w2.Hold(1, new[] { fg });
        var tank = Dummy(w2, 2, c2, "tank_ft_male");
        Steps(w2, 60 * 40);
        check(tank.Hp < 1000, $"야전포: 전차에 피해 (체력 {tank.Hp:0}/1000)");
    }

    // ---------------------------------------------------------------- 기병

    static void Cavalry(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 45);
        var c = Center(w);
        var cav = Spawn(w, "cavalry_default", 1, c + new Vector2(-30, 0));
        check(cav.StaminaCap == 3 * defs.CombatRules.StaminaMax && w.UnitSpeedForTest(cav) / defs.Units["infantry"].Speed > 1.49f, $"기병: 스태미나 한도 {cav.StaminaCap} (보병의 3배), 이동 속도 보병의 {w.UnitSpeedForTest(cav) / defs.Units["infantry"].Speed:0.00}배");
        // 긴 돌격 (2초 이상): 피해 보너스 없음 (모든 적중 40) · 충격 이벤트 = 마비 1.5초 · 쿨다운 10~15초 · 충격 지점 2칸 안 적 보병만 제압
        var foe = Dummy(w, 2, c);
        foe.Hp = 5000;
        var near = Dummy(w, 2, c + new Vector2(-1.2f, 1.2f)); near.Hp = 5000;   // 약 1.7칸
        var far = Dummy(w, 2, c + new Vector2(0, 4)); far.Hp = 5000;           // 4칸
        w.Charge(1, new[] { cav }, foe.Pos, foe);
        float prev = foe.Hp; var drops = new List<float>(); int impactDelay = -1, impactCd = -1, nearSup = -1, farSup = -1; bool impacted = false;
        for (int t = 0; t < 60 * 12; t++)
        {
            w.Step();
            if (foe.Hp < prev) { drops.Add(prev - foe.Hp); prev = foe.Hp; }
            if (!impacted && cav.ImpactSpent) { impacted = true; impactDelay = cav.ReactDelay; impactCd = cav.ChargeCdLeft; nearSup = near.Suppression; farSup = far.Suppression; }
        }
        int stunTicks = (int)Math.Round(Defs.Current.CombatRules.ChargeStunMin / Config.SimDt);
        int cdMin = (int)(Defs.Current.CombatRules.ChargeCooldownMin / Config.SimDt) - 2, cdMax = (int)(Defs.Current.CombatRules.ChargeCooldownMax / Config.SimDt);
        check(drops.Count >= 2 && drops.All(d => d == 40) && impacted && Math.Abs(impactDelay - stunTicks) <= 1 && impactCd >= cdMin && impactCd <= cdMax,
              $"기병 돌격 (2초 이상): 적중 {string.Join("/", drops.Take(3).Select(d => d.ToString("0")))} (보너스 없음), 충격 {impacted}, 마비 {impactDelay}틱 (1.5초 = {stunTicks}), 쿨다운 {impactCd}틱 ({cdMin}~{cdMax})");
        check(nearSup >= 100 && farSup == 0,
              $"돌격 충격 제압 (충격 순간): 2칸 안 보병 {nearSup} (>= 100), 4칸 밖 {farSup} (0)");
        // 짧은 돌격 (2초 미만) = 일반 근접 피해
        var w2 = NewWorld(opt.Seed + 46);
        var c2 = Center(w2);
        var cav2 = Spawn(w2, "cavalry_default", 1, c2 + new Vector2(-6, 0));
        var foe2 = Dummy(w2, 2, c2); foe2.Hp = 5000;
        w2.Charge(1, new[] { cav2 }, foe2.Pos, foe2);
        float first = 0; float p2 = foe2.Hp;
        for (int t = 0; t < 60 * 6 && first == 0; t++) { w2.Step(); if (foe2.Hp < p2) first = p2 - foe2.Hp; }
        check(first == 40, $"짧은 돌격 (2초 미만): 일반 근접 피해 {first:0} (충격 없음)");
        // 돌격만: 사격 없이 돌격
        foreach (bool only in new[] { false, true })
        {
            var w3 = NewWorld(opt.Seed + 47);
            var c3 = Center(w3);
            var cv = Spawn(w3, "cavalry_default", 1, c3 + new Vector2(-12, 0));
            var ft = Dummy(w3, 2, c3); ft.Hp = 5000;
            if (only) w3.SetChargeOnly(1, new[] { cv }, true);
            int shots = 0; bool melee = false;
            for (int t = 0; t < 60 * 10; t++)
            {
                w3.Step();
                foreach (var s in w3.Shots) if (s.Tick == w3.Tick - 1 && s.ShooterId == cv.Id) shots++;
                if (w3.MeleeStrikes.Any(m => m.AttackerId == cv.Id)) melee = true;
            }
            if (only) check(shots == 0 && melee && cv.ChargeOnly && ft.Hp < 5000, $"돌격만 켬: 사격 {shots}발, 근접 {melee}, 표적 체력 {ft.Hp:0}");
            else check(shots > 0, $"기본 기병은 카빈을 쏜다 ({shots}발)");
        }
        // 토글 해제
        var cw = Spawn(w, "cavalry_default", 1, c + new Vector2(5, 5));
        w.SetChargeOnly(1, new[] { cw }, true); Steps(w, 2); bool on = cw.ChargeOnly;
        w.SetChargeOnly(1, new[] { cw }, false); Steps(w, 2);
        check(on && !cw.ChargeOnly, "돌격만 토글: 켜고 끌 수 있다");
    }

    // ---------------------------------------------------------------- 전차 근접 불가

    static void NoMeleeVsArmor(GameOptions opt, Action<bool, string> check)
    {
        foreach (var kind in new[] { "infantry", "infantry_fengtian", "officer", "infantry_assault", "cavalry_default" })
        {
            foreach (var tk in new[] { "tank_ft_male", "tank_armored_car" })
            {
                var w = NewWorld(opt.Seed + 48);
                var c = Center(w);
                var u = Spawn(w, kind, 1, c + new Vector2(-5, 0));
                u.Weapon = null; u.Grenades = 0;
                var tank = Dummy(w, 2, c, tk);
                w.Charge(1, new[] { u }, tank.Pos, tank);
                Steps(w, 60 * 12);
                bool melee = w.MeleeStrikes.Any(m => m.AttackerId == u.Id) || tank.Hp < 1000;
                if (kind == "infantry" || tk == "tank_ft_male") check(!melee && !u.Charging, $"{kind} → {tk} 돌격: 근접 불가 (체력 {tank.Hp:0}, 돌격 상태 {u.Charging})");
                else check(!melee, $"{kind} → {tk}: 근접 불가");
            }
        }
        // 자동 돌격도 없다: 전차가 바로 옆에 있어도 (무기 없는 보병)
        var w2 = NewWorld(opt.Seed + 49);
        var c2 = Center(w2);
        var inf = Spawn(w2, "infantry", 1, c2); inf.Weapon = null;
        var t2 = Dummy(w2, 2, c2 + new Vector2(1, 0), "tank_ft_male");
        Steps(w2, 60 * 6);
        check(t2.Hp == 1000 && !inf.Charging, "전차가 코앞에 있어도 자동 돌격하지 않는다");
    }

    // ---------------------------------------------------------------- 수류탄

    static void Grenades(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current; var g = defs.Grenade;
        var w = NewWorld(opt.Seed + 50);
        var c = Center(w);
        var th = Spawn(w, "infantry_assault", 1, c + new Vector2(-4, 0));
        th.Weapon = null;
        var mid = Dummy(w, 2, c + new Vector2(0, 0), "recruit");
        TrenchUnder(w, mid);                                       // 엄폐 무시 확인 (참호 지형)
        var edge = Dummy(w, 2, c + new Vector2(1.2f, 0), "recruit");   // 중심에서 1.2 (가장자리 쪽)
        var out_ = Dummy(w, 2, c + new Vector2(0, 3.6f), "recruit");  // 반경 밖 + 근접 탄착 띠 (1.5칸) 밖 (N25)
        var tank = Dummy(w, 2, c + new Vector2(-0.5f, -1.0f), "tank_ft_male");
        // 사거리 밖 (7칸) → 던지지 않음
        w.ThrowGrenadeAt(1, new[] { th }, c + new Vector2(3, 0)); Steps(w, 2);
        check(th.Grenades == 2 && w.Blasts.Count == 0, "사거리 5칸 밖 지점 = 던지지 않음 (보유 유지)");
        w.ThrowGrenadeAt(1, new[] { th }, mid.Pos); Steps(w, 2);
        check(th.Grenades == 1 && w.Blasts.Count == 1, "수류탄 명령: 사거리 안 지점에 투척 (보유 2 → 1)");
        th.Grenades = 0;   // 이하 폭발 수치 검사는 자동 투척이 끼지 않게 한 발만 쓴다
        int fuse = (int)MathF.Round(g.Fuse / Config.SimDt);
        Steps(w, fuse - 6);
        bool early = mid.Hp < 1000;
        Steps(w, 12);
        float dm = 1000 - mid.Hp, de = 1000 - edge.Hp;
        check(!early && dm >= 40 && dm <= 60 && de > 20 - 1 && de < dm && out_.Hp == 1000 && w.Blasts.Count == 0, $"신관 {g.Fuse}초 뒤 폭발: 중심 {dm:0} / 가장자리쪽 {de:0} (60→20 감소), 반경 밖 {1000 - out_.Hp:0}, 엄폐 중인 표적도 피해");
        check(mid.Suppression >= 280 && edge.Suppression >= 280 && out_.Suppression == 0, $"제압 +300 (엄폐 무시): 중심 {mid.Suppression}, 가장자리 {edge.Suppression}, 반경 밖 {out_.Suppression}");
        check(tank.Hp < 1000 && 1000 - tank.Hp <= g.TankDamage && 1000 - tank.Hp >= g.TankDamage - 3, $"전차: 묶음 수류탄 고정 피해 {1000 - tank.Hp:0} (40 - 장갑)");
        // 아군 피해 없음
        var w2 = NewWorld(opt.Seed + 51);
        var c2 = Center(w2);
        var th2 = Spawn(w2, "infantry_assault", 1, c2 + new Vector2(-3, 0)); th2.Weapon = null;
        var friend = Dummy(w2, 1, c2 + new Vector2(0.3f, 0), "recruit");
        var foe = Dummy(w2, 2, c2, "recruit");
        w2.ThrowGrenadeAt(1, new[] { th2 }, foe.Pos); Steps(w2, 120);
        check(friend.Hp == 1000 && friend.Suppression == 0 && foe.Hp < 1000, "수류탄 아군 오사 없음");
        // 자동 투척: 엄폐 중 4칸 이내
        var w3 = NewWorld(opt.Seed + 52);
        var c3 = Center(w3);
        var th3 = Spawn(w3, "infantry_assault", 1, c3 + new Vector2(-3.5f, 0)); th3.Weapon = null;
        var open = Dummy(w3, 2, c3 + new Vector2(0, 8), "recruit");   // 엄폐 아님, 멀다
        var covered = Dummy(w3, 2, c3, "recruit"); TrenchUnder(w3, covered);
        Steps(w3, 60 * 3);
        check(th3.Grenades == 0 && covered.Hp < 1000 && open.Hp == 1000, $"자동 투척: 엄폐 중인 적 3.5칸 → 던짐 (보유 {th3.Grenades}, 표적 체력 {covered.Hp:0})");
        var w4 = NewWorld(opt.Seed + 53);
        var c4 = Center(w4);
        var th4 = Spawn(w4, "infantry_assault", 1, c4 + new Vector2(-3.5f, 0)); th4.Weapon = null;
        var plain = Dummy(w4, 2, c4, "recruit");
        Steps(w4, 60 * 3);
        check(th4.Grenades == 2, "엄폐 안 한 적에게는 자동 투척하지 않는다");
        var w5 = NewWorld(opt.Seed + 54);
        var c5 = Center(w5);
        var th5 = Spawn(w5, "infantry_assault", 1, c5 + new Vector2(-2.6f, 0)); th5.Weapon = null;
        var tk5 = Dummy(w5, 2, c5, "tank_ft_male");
        Steps(w5, 60 * 3);
        var th6 = Spawn(w5, "infantry_assault", 1, c5 + new Vector2(-12f, 0)); th6.Weapon = null;
        Steps(w5, 60 * 3);
        check(th5.Grenades == 0 && tk5.Hp < 1000 && th6.Grenades == 2, $"자동 투척: 전차 3칸 이내 → 던짐 (전차 체력 {tk5.Hp:0}), 멀면 안 던짐");
        // 회복: 비전투 3분에 1개
        th5.Grenades = 0; th5.LastShotTick = -1000; th5.GrenadeRegen = 0;
        var calm = NewWorld(opt.Seed + 55);
        var cu = Spawn(calm, "infantry_assault", 1, Center(calm)); cu.Grenades = 0;
        Steps(calm, 60 * 400);
        check(cu.Grenades == 0, "수류탄: 아군 도시 · 보급 마차에서 먼 들판에서는 회복하지 않는다 (400초)");
        var hq = calm.Buildings.First(bd => bd.Def.City && bd.Owner == 1);
        cu.Pos = hq.Pos + new Vector2(0, hq.Size / 2f + 2);
        Steps(calm, 60 * 40);
        bool none = cu.Grenades == 0;
        Steps(calm, 60 * 80);
        check(none && cu.Grenades >= 1, $"수류탄 회복: 도시 옆 ×{defs.Supply.CityMul} — 약 {g.RegenSeconds / defs.Supply.CityMul:0}초마다 1개 ({cu.Grenades}개)");
        Steps(calm, 60 * 400);
        check(cu.Grenades == 2, "수류탄 보유 상한(2) 까지 회복, 초과 안 함");
        // 다른 병종은 수류탄 없음
        check(Spawn(calm, "infantry", 1, Center(calm)).Grenades == 0 && Spawn(calm, "infantry_regular", 1, Center(calm)).Grenades == 0, "정규병 · 보병은 수류탄 없음 (돌격보병만)");
    }

    // ---------------------------------------------------------------- 행진 사격

    static void MarchingFire(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current; var cr = defs.CombatRules;
        // 기본: 이동 중 앞쪽 적에게 사격 (간격 ≥ 1.5 × 1.8)
        (int shots, List<long> ticks, bool walkingAll) Run(string kind, Vector2 foeOff, Action<World, Unit>? setup, string foeKind = "infantry", bool quick = false, bool lateral = false)
        {
            var w = NewWorld(opt.Seed + 60);
            var c = Center(w);
            var u = Spawn(w, kind, 1, c + new Vector2(-10, 0));
            if (Defs.Current.Units[kind].Category == "infantry")   // 보병 행진 사격 = 장교 지휘 아래에서만 (MarchCommand.cs) — 장교와 2인 중대
            {
                var off = Spawn(w, "officer", 1, c + new Vector2(-11, 1.2f));
                w.FormCompany(1, new[] { u, off });
                w.Step();
                w.SetMarchFire(1, new[] { u }, true);   // 돌격 사격 켬 (기본 꺼짐) — 공격 이동 중에만
                w.Step();
            }
            var f = Dummy(w, 2, c + foeOff, foeKind);
            setup?.Invoke(w, u);
            if (quick) w.QuickStep(1, new[] { u }, c + new Vector2(10, 0)); else if (Defs.Current.Units[kind] is { Category: "infantry", MarchFire: true }) w.AttackMove(1, new[] { u }, c + new Vector2(10, 0)); else w.Move(1, new[] { u }, c + new Vector2(10, 0));
            var ticks = new List<long>(); bool walking = true;
            for (int t = 0; t < 60 * 5; t++)
            {
                w.Step();
                foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == u.Id) { ticks.Add(s.Tick); if (!u.Walking) walking = false; }
                f.Hp = 1000;
            }
            return (ticks.Count, ticks, walking);
        }
        var basic = Run("infantry", new Vector2(4.5f, 0), null);   // 적 4.5칸 (행진 사격 중 3칸 안이면 자동 돌격)
        var gaps = basic.ticks.Zip(basic.ticks.Skip(1), (a, b) => (b - a) * Config.SimDt).ToList();
        float minGap = defs.Weapons["rifle"].CooldownMin * cr.MarchIntervalMul - 0.1f;
        check(basic.shots >= 2 && basic.walkingAll && gaps.All(g => g >= minGap), $"행진 사격: 이동 중 사격 {basic.shots}발, 모두 걸으며 쏨, 최소 간격 {(gaps.Count > 0 ? gaps.Min() : 0):0.00}초 ≥ {minGap:0.00} (소총 1.8 × 1.5)");
        var side = Run("infantry", new Vector2(0, 5), null);
        check(side.shots == 0 || side.ticks.All(t => true) && side.shots <= 2, $"옆(90°) 적에게는 쏘지 않는다 (원뿔 밖, {side.shots}발: 원뿔 진입 순간만 허용)");
        var back = Run("infantry", new Vector2(-14, 0), null);
        check(back.shots == 0, $"뒤쪽 적에게는 쏘지 않는다 ({back.shots}발)");
        var far = Run("infantry", new Vector2(14, 0), (w, u) => { });
        check(far.ticks.Count == 0 || far.ticks.Min() > 60 * 2, $"유효 시야 절반 (7칸) 밖에서는 쏘지 않는다 (첫 사격 {(far.ticks.Count > 0 ? far.ticks.Min() : -1)}틱)");
        var qs = Run("infantry", new Vector2(4.5f, 0), null, quick: true);
        check(qs.shots == 0, $"속보 중에는 행진 사격 불가 ({qs.shots}발)");
        var sup = Run("infantry", new Vector2(4.5f, 0), (w, u) => u.Suppression = 1000);
        check(sup.ticks.Count == 0 || sup.ticks.Min() > 60 * 1, $"제압 400 이상이면 행진 사격 불가 (첫 사격 {(sup.ticks.Count > 0 ? sup.ticks.Min() : -1)}틱)");
        var chg = NewWorld(opt.Seed + 61);
        var cc = Center(chg);
        var cu = Spawn(chg, "infantry", 1, cc + new Vector2(-10, 0)); Dummy(chg, 2, cc + new Vector2(2, 0));
        chg.Charge(1, new[] { cu }, cc + new Vector2(10, 0));
        int cs = 0;
        for (int t = 0; t < 60 * 3; t++) { chg.Step(); foreach (var s in chg.Shots) if (s.Tick == chg.Tick - 1 && s.ShooterId == cu.Id) cs++; }
        check(cs == 0, $"돌격 중에는 행진 사격 없음 ({cs}발)");
        // 명중 ×0.4 / SMG ×0.7 / 간격
        var w5 = NewWorld(opt.Seed + 62);
        var a = Spawn(w5, "infantry", 1, Center(w5)); var b = Dummy(w5, 2, Center(w5) + new Vector2(4, 0));
        float pn = w5.HitChance(a, b); a.MarchShot = true; float pm = w5.HitChance(a, b); a.MarchShot = false;
        var s5 = Spawn(w5, "infantry_assault", 1, Center(w5) + new Vector2(0, 1)); var b2 = Dummy(w5, 2, Center(w5) + new Vector2(0, 3));
        float sn = w5.HitChance(s5, b2); s5.MarchShot = true; float sm = w5.HitChance(s5, b2); s5.MarchShot = false;
        check(MathF.Abs(pm / pn - 0.4f) < 0.01f && MathF.Abs(sm / sn - defs.Units["infantry_assault"].MarchAccMul) < 0.01f && defs.Units["infantry_assault"].MarchAccMul > cr.MarchAccMul, $"행진 사격 명중: 소총 ×{pm / pn:0.00}, 돌격 카빈 ×{sm / sn:0.00}");
        // 기관총 전차 가능, 수컷형 불가, 장갑차 가능, 중기관총 불가
        var tf = Run("tank_ft_female", new Vector2(3, 0), null);
        var tm = Run("tank_ft_male", new Vector2(3, 0), null);
        var ac = Run("tank_armored_car", new Vector2(3, 0), null);
        var hm = Run("heavy_machinegunner", new Vector2(2, 0), null);
        check(tf.shots > 0 && ac.shots > 0 && tm.shots == 0 && hm.shots == 0, $"행진 사격 차량: 암컷형 {tf.shots}발 · 장갑차 {ac.shots}발 · 수컷형 {tm.shots}발 · 중화기(기관총병) {hm.shots}발");
        var cvr = Run("cavalry_default", new Vector2(2, 0), null);
        check(cvr.shots == 0, $"기병은 행진 사격 없음 ({cvr.shots}발)");
        var smg = Run("infantry_assault", new Vector2(4.5f, 0), null);
        var sg = smg.ticks.Zip(smg.ticks.Skip(1), (x, y) => (y - x) * Config.SimDt).ToList();
        float smgMin = defs.Weapons["carbine_assault"].CooldownMin * defs.Units["infantry_assault"].MarchIntervalMul - 0.1f;
        check(smg.shots >= 2 && sg.All(g => g >= smgMin), $"돌격 카빈 행진 사격: 간격 ×1.2 (유닛별 배수) ({smg.shots}발, 최소 간격 {(sg.Count > 0 ? sg.Min() : 0):0.00} ≥ {smgMin:0.00})");
        {   // 돌격 사격 기본값 (N21): 돌격보병 중대 = 켜진 채 시작, 소총 중대 = 꺼짐
            var wc = NewWorld(opt.Seed + 80); var cq = Center(wc);
            var asl2 = Enumerable.Range(0, 8).Select(i => Spawn(wc, "infantry_assault", 1, cq + new Vector2(-10, i * 0.9f))).Concat(new[] { Spawn(wc, "officer", 1, cq + new Vector2(-12, 0)) }).ToList();
            var rif = Enumerable.Range(0, 8).Select(i => Spawn(wc, "infantry", 1, cq + new Vector2(-10, 12 + i * 0.9f))).ToList();
            wc.FormCompany(1, asl2); wc.FormCompany(1, rif);
            wc.Step();
            check(asl2[0].Company!.MarchFireOn && !rif[0].Company!.MarchFireOn && defs.Units["infantry_assault"].MarchFireDefault && !defs.Units["infantry"].MarchFireDefault,
                  $"돌격 사격 기본: 돌격보병 중대 켜짐 ({asl2[0].Company!.MarchFireOn}), 소총 중대 꺼짐 ({rif[0].Company!.MarchFireOn}), 행진 명중 ×{defs.Units["infantry_assault"].MarchAccMul} (소총 ×{cr.MarchAccMul})");
        }
    }

    // ---------------------------------------------------------------- 저장

    // ---------------------------------------------------------------- 경기관총 점사 (Bursts.cs)

    static (World w, Unit lmg, Unit foe) BurstSetup(GameOptions opt, string kind = "heavy_machinegunner")
    {
        var w = NewWorld(opt.Seed + 80);
        var c = Center(w);
        var u = Spawn(w, kind, 1, c);
        var f = Dummy(w, 2, c + new Vector2(12, 0));
        w.Hold(1, new[] { u });
        return (w, u, f);
    }

    static List<long> BurstTicks(World w, Unit u, Unit f, int steps)
    {
        var ticks = new List<long>();
        for (int i = 0; i < steps; i++)
        {
            w.Step();
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == u.Id) ticks.Add(s.Tick);
            f.Hp = 1000;
        }
        return ticks;
    }

    static void Bursts(GameOptions opt, Action<bool, string> check)
    {
        var lw = Defs.Current.Weapons["lmg"];
        check(lw.BurstMin == 5 && lw.BurstMax == 10 && lw.BurstPauseMin >= 2.5f && lw.BurstPauseMax <= 4.5f && !Defs.Current.Weapons["hmg"].Bursty, "점사 정의: lmg 5~10발 · 휴지 2.5~4.5초, hmg 는 지속 사격");
        var (w, u, f) = BurstSetup(opt);
        var ticks = BurstTicks(w, u, f, 60 * 60);
        var lens = new List<int>(); var pauses = new List<double>(); int run = 1; double cd = lw.Cooldown;
        for (int i = 1; i < ticks.Count; i++)
        {
            double gap = (ticks[i] - ticks[i - 1]) / 60.0;
            if (gap > cd * 3 + 0.5) { lens.Add(run); run = 1; pauses.Add(gap); } else run++;
        }
        check(lens.Count >= 8 && lens.All(l => l >= 5 && l <= 10), $"점사 길이 5~10발 ({lens.Count}번: {lens.Min()}~{lens.Max()})");
        check(lens.Distinct().Count() >= 3, "점사 길이가 매번 달라진다 (결정적 난수)");
        check(pauses.All(p => p >= 2.5 - 0.02 && p <= 4.5 + cd * 3 + 0.2) && pauses.Distinct().Count() >= 3, $"점사 사이 휴지 약 2.5~4.5초 ({pauses.Min():F2}~{pauses.Max():F2}초)");
        var (w2, u2, f2) = BurstSetup(opt, "heavy_machinegunner");
        var t2 = BurstTicks(w2, u2, f2, 60 * 60);
        check(t2.SequenceEqual(ticks) && w2.ComputeChecksum() == w.ComputeChecksum(), "점사 결정성: 같은 설정 두 번 = 사격 틱 · 체크섬 동일");
        // 표적을 놓으면 점사 끝 + 휴지
        var (w3, u3, f3) = BurstSetup(opt);
        for (int i = 0; i < 400 && u3.BurstLeft == 0; i++) { w3.Step(); f3.Hp = 1000; }
        for (int i = 0; i < 25; i++) { w3.Step(); f3.Hp = 1000; }
        bool mid = u3.BurstLeft > 0;
        f3.Pos += new Vector2(200, 0);   // 사거리 밖 (시야 밖)으로 치운다
        for (int i = 0; i < 5; i++) w3.Step();
        check(mid && u3.BurstLeft == 0 && u3.Cooldown >= 0.5f, "표적을 놓으면 점사를 끊고 휴지");
        // 점사 중 저장 / 불러오기
        var (w4, u4, f4) = BurstSetup(opt);
        for (int i = 0; i < 600 && !(u4.BurstLeft is > 3 and < 9); i++) { w4.Step(); f4.Hp = 1000; }
        bool inBurst = u4.BurstLeft is > 3 and < 9;
        var bytes = Snapshot.Save(w4, Meta);
        var y = Snapshot.Load(bytes, Defs.Current).World;
        var yu = y.Units.First(x => x.Id == u4.Id);
        bool same = yu.BurstLeft == u4.BurstLeft && y.ComputeChecksum() == w4.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool cont = true;
        for (int i = 0; i < 600; i++) { w4.Step(); y.Step(); w4.Units.First(x => x.Owner == 2).Hp = 1000; y.Units.First(x => x.Owner == 2).Hp = 1000; if (i % 20 == 19 && w4.ComputeChecksum() != y.ComputeChecksum()) { cont = false; break; } }
        check(inBurst && same && cont, $"점사 도중 저장 (남은 {u4.BurstLeft}발): 불러온 월드 동일 · 10초 이어 돌려도 체크섬 동일");
    }

    static void SaveLoad(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 70);
        var c = Center(w);
        var asl = Enumerable.Range(0, 4).Select(i => Spawn(w, "infantry_assault", 1, c + new Vector2(-6, i * 0.8f))).ToList();
        var cav = Spawn(w, "cavalry_default", 1, c + new Vector2(-25, 0));
        var tf = Spawn(w, "tank_ft_female", 1, c + new Vector2(-12, 3));
        var foes = Enumerable.Range(0, 6).Select(i => Spawn(w, "recruit", 2, c + new Vector2(2 + i % 2, -2 + i * 0.7f))).ToList();
        foreach (var f in foes) { f.Hp = 400; if (i0(f)) TrenchUnder(w, f); }
        w.SetChargeOnly(1, new[] { cav }, true);
        w.Move(1, asl.Cast<Unit>().Append(tf).ToList(), c + new Vector2(8, 0));
        Steps(w, 100);
        w.ThrowGrenadeAt(1, asl, c + new Vector2(0, 0)); Steps(w, 5);
        bool inFlight = w.Blasts.Count > 0;
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = y.Blasts.Count == w.Blasts.Count && y.Units.Zip(w.Units, (a, b) => a.Grenades == b.Grenades && a.ChargeOnly == b.ChargeOnly && a.ChargeRun == b.ChargeRun && a.MarchTarget == b.MarchTarget).All(x => x)
                    && y.ComputeChecksum() == w.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        check(inFlight && same, $"수류탄 비행 중 저장 (날아가는 수류탄 {w.Blasts.Count}, 돌격만 · 충격 · 행진 사격 표적 포함): 불러온 월드 동일");
        bool cont = true;
        for (int i = 0; i < 600; i++) { w.Step(); y.Step(); if (i % 25 == 24 && w.ComputeChecksum() != y.ComputeChecksum()) { cont = false; break; } }
        check(cont, "저장 → 불러오기 뒤 10초 이어 돌려도 체크섬 동일 (교전 4차)");
    }

    static bool i0(Unit u) => u.Id % 2 == 0;
    /// <summary>유닛 발밑 칸을 참호 지형으로 (엄폐 시험 — 건설 제거 뒤 엄폐 = 지형만).</summary>
    static void TrenchUnder(World w, Unit u) { var (x, y) = GameMap.TileOf(u.Pos); w.MutateTerrain(x, y, TerrainType.Trench); }
}
