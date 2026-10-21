using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 교전 개편 2차 검증 (ROADMAP §2-4b~e): 표적 분산 분포, 제압(증가 · 저항 · 감소 · 계급 · 장교 · 효과 · 면역), 계급 승급, 착검 돌격(자동 · 명령 · 근접 2타 사살),
/// 기관총 흩뿌림(대신 맞음 · 범위 제압 · 아군 오사 없음), 멍칫 상한, 반응 지연 0~3틱, 교전 거리 80%, 대전차소총 이름, 저장 동일성.
/// </summary>
public static class Combat3SelfTest
{
    static readonly SnapshotMeta Meta = new("combat3", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> check) => LegacyDeploy.Wrap(() => RunImpl(opt, check));
    static void RunImpl(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();   // 지휘 계통 지연은 SquadSelfTest 에서 따로
        try
        {
            Data(check);
            ArmorRules(opt, check);
            TargetSpread(opt, check);
            Suppression(opt, check);
            Rank(opt, check);
            Charge(opt, check);
            Stamina(opt, check);
            Spray(opt, check);
            Hesitation(opt, check);
            ReactAndEngageRange(opt, check);
            SaveLoad(opt, check);
        }
        catch (Exception e) { check(false, $"교전 2차 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World NewWorld(int seed, bool fog = false)
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

    /// <summary>무기 · 근접 무기 없는 정지 유닛 (표적용). 사수 명령으로 고정.</summary>
    static Unit Dummy(World w, int faction, Vector2 pos, string kind = "infantry")
    {
        var u = w.SpawnUnit(Defs.Current.Units[kind], faction, pos);
        u.Weapon = null;
        w.Hold(faction, new[] { u });
        return u;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static bool Near(float a, float b, float eps = 1e-3f) => MathF.Abs(a - b) <= eps;

    // ---------------------------------------------------------------- 스태미나 · 속보 (ROADMAP §4)

    static void Stamina(GameOptions opt, Action<bool, string> check)
    {
        var rules = Defs.Current.CombatRules;
        var defs = Defs.Current;
        // 정찰 제거: 명령 카드 · 명령 형식 · 이동 상태 어디에도 없다
        bool jsonGone;
        try { System.Text.Json.JsonSerializer.Deserialize<GameCommand>("{\"$t\":\"patrol\",\"Faction\":1,\"Units\":[],\"Target\":{\"X\":0,\"Y\":0}}"); jsonGone = false; }
        catch (Exception) { jsonGone = true; }
        check(Commands.Get("patrol") == null && Commands.Get("quickstep") != null && !Enum.GetNames<UnitOrder>().Contains("Patrol") && jsonGone,
              "정찰 제거: 명령 카드 · UnitOrder · 명령 형식에 없음, 속보 명령 존재");
        // 속도: 걷기 < 속보(+15%) < 돌격(+30%) — 스태미나 충분할 때 최고 속도, 3초 뒤 스태미나
        float Run(string mode, int seed, out float stEnd)
        {
            var w = NewWorld(seed);
            var c = Center(w);
            var u = w.SpawnUnit(defs.Units["infantry"], 1, c + new Vector2(-8, 0));
            u.BayonetFixed = true;   // N57: 착검 멈춤은 빼고 속도 · 소모만 (ChargeSelfTest)
            var goal = c + new Vector2(30, 0);
            if (mode == "quick") w.QuickStep(1, new[] { u }, goal);
            else if (mode == "charge") w.Charge(1, new[] { u }, goal);
            else w.Move(1, new[] { u }, goal);
            float maxSpd = 0;
            for (int t = 0; t < 60 * 3; t++) { w.Step(); if (u.Stamina >= rules.StaminaFullSpeed) maxSpd = MathF.Max(maxSpd, u.Velocity.Length()); }
            stEnd = u.Stamina;
            return maxSpd;
        }
        float walk = Run("walk", opt.Seed + 31, out float sw), quick = Run("quick", opt.Seed + 31, out float sq), charge = Run("charge", opt.Seed + 31, out float sc);
        float sp = defs.Units["infantry"].Speed;
        check(Near(walk, sp, 0.01f) && Near(quick, sp * rules.QuickStepSpeedMul, 0.01f) && Near(charge, sp * rules.ChargeSpeedMul, 0.01f) && walk < quick && quick < charge,
              $"속도: 걷기 {walk:0.00} < 속보 {quick:0.00} (x{rules.QuickStepSpeedMul}) < 돌격 {charge:0.00} (x{rules.ChargeSpeedMul})");
        check(Near(sw, rules.StaminaMax) && sq < rules.StaminaMax && sc < sq, $"소모: 걷기 {sw:0} · 속보 3초 후 {sq:0.0} · 돌격 3초 후 {sc:0.0} (대략 {rules.StaminaMax - 3 * rules.StaminaQuickDrain:0.0} · {rules.StaminaMax - 3 * rules.StaminaChargeDrain:0.0})");
        // 회복 수치 · 차량은 스태미나 없음
        var w1 = NewWorld(opt.Seed + 32);
        var c1 = Center(w1);
        var a = w1.SpawnUnit(defs.Units["infantry"], 1, c1);
        var veh = w1.SpawnUnit(defs.Units["tank_ft_male"], 1, c1 + new Vector2(0, 3));
        a.Fatigue = 60;   // 스태미나 40
        Steps(w1, 60 * 4);
        check(Near(a.Stamina, 40 + 4 * rules.StaminaRegenIdle, 0.5f) && veh.Fatigue == 0 && !veh.HasStamina, $"정지 회복 +{rules.StaminaRegenIdle}/초: 40 → {a.Stamina:0.0} (차량은 스태미나 없음)");
        w1.Move(1, new[] { a }, c1 + new Vector2(30, 0));
        a.Fatigue = 60;
        Steps(w1, 60 * 2);
        check(Near(a.Stamina, 40 + 2 * rules.StaminaRegenMove, 1f), $"걸으며 회복 +{rules.StaminaRegenMove}/초: 40 → {a.Stamina:0.0}");
        // 속도 곡선
        var w2 = NewWorld(opt.Seed + 33);
        var c2 = Center(w2);
        var b = w2.SpawnUnit(defs.Units["infantry"], 1, c2);
        float Spd(float stamina, bool ch, bool qs) { b.Fatigue = rules.StaminaMax - stamina; b.ChargeState = (byte)(ch ? 2 : 0); b.QuickStep = qs; return w2.UnitSpeedForTest(b) / sp; }
        float c100 = Spd(100, true, false), c50 = Spd(50, true, false), c37 = Spd(37.5f, true, false), c24 = Spd(24, true, false), c0 = Spd(0, true, false), w30 = Spd(30, false, false), w10 = Spd(10, false, false), q0 = Spd(0, false, true), q50 = Spd(50, false, true);
        check(Near(c100, 1.3f) && Near(c50, 1.3f) && Near(c37, 1.225f) && Near(c24, (1 + 0.3f * 24 / 50) * 0.85f) && Near(c0, 0.85f),
              $"돌격 속도 배수: 100→{c100:0.000} 50→{c50:0.000} 37.5→{c37:0.000} 24→{c24:0.000} 0→{c0:0.000} (50 이상 +30%, 0 에서 +0%, 25 미만 x0.85)");
        check(Near(w30, 1f) && Near(w10, 0.85f) && Near(q50, 1.15f) && Near(q0, 0.85f), $"걷기 30→{w30:0.00} · 10→{w10:0.00} · 속보 50→{q50:0.00} · 0→{q0:0.00}");
        // 장거리 돌격: 후반이 초반보다 느리다
        var w3 = NewWorld(opt.Seed + 34);
        var c3 = Center(w3);
        var d = w3.SpawnUnit(defs.Units["infantry"], 1, c3 + new Vector2(-30, 0));
        d.BayonetFixed = true;   // N57
        w3.Charge(1, new[] { d }, c3 + new Vector2(40, 0));
        float first = 0, last = 0;
        for (int t = 0; t < 60 * 14; t++) { w3.Step(); float v = d.Velocity.Length(); if (t == 30) first = v; if (v > 0) last = v; }
        check(first > last && d.Stamina < rules.StaminaFullSpeed, $"장거리 돌격: 초반 {first:0.00} → 후반 {last:0.00} 칸/초, 스태미나 {d.Stamina:0.0}");
        // 저장
        var w4 = NewWorld(opt.Seed + 35);
        var c4 = Center(w4);
        var e = w4.SpawnUnit(defs.Units["infantry"], 1, c4);
        var f = w4.SpawnUnit(defs.Units["officer"], 1, c4 + new Vector2(0, 2));
        w4.QuickStep(1, new[] { e, f }, c4 + new Vector2(25, 0));
        Steps(w4, 60 * 3);
        var l = Snapshot.Load(Snapshot.Save(w4, Meta), Defs.Current).World;
        var e2 = l.Units.First(u => u.Id == e.Id);
        bool same = e2.Fatigue == e.Fatigue && e2.QuickStep && e.Fatigue > 0 && l.ComputeChecksum() == w4.ComputeChecksum();
        Steps(w4, 60 * 5); Steps(l, 60 * 5);
        check(same && l.ComputeChecksum() == w4.ComputeChecksum(), $"속보 저장 · 불러오기 동일 (피로 {e.Fatigue:0.0}, 체크섬 일치, 이어서 5초 뒤도 동일)");
    }

    // ---------------------------------------------------------------- 데이터

    static void Data(Action<bool, string> check)
    {
        var d = Defs.Current;
        var c = d.CombatRules;
        check(!d.Weapons.ContainsKey("bazooka") && d.Weapons["at_rifle"].Name == "대전차소총" && d.Weapons["at_rifle"].Damage == 10 && d.Weapons["at_rifle"].BonusAgainst("armored") == 3.5f && d.Units["heavy_at_rifleman"].Weapon == "at_rifle",
              "대전차소총: 바주카 키 · 이름 교체, 수치 · 보너스 그대로");
        var inf = d.Units["infantry"]; var off = d.Units["officer"]; var mg = d.Units["heavy_machinegunner"];
        check(off.Hp == 200 && off.IsOfficer && off.CommandRadius == 8 && off.SuppressResist == 0 && off.Melee == "sabre" && off.Weapon == "pistol",
              $"장교: 체력 {off.Hp} · 지휘 8칸 · 제압 면역 · 권총 + 곡도");
        var mgw = d.Weapons["lmg"]; var rifle = d.Weapons["rifle"];
        check(mg.Hp == 160 && mg.SetupTime == 3 && mg.PackTime == 2 && mgw.Ammo == 300 && mgw.Spray == 1.5f && mgw.Suppress == 24 && mgw.Damage == rifle.Damage && mgw.Range == rifle.Range
              && Near(mgw.Cooldown, 0.26f) && mgw.Ammo > rifle.Ammo,
              $"기관총병: 체력 160 · 거치 3/해제 2 · 탄약 80 · 간격 {mgw.Cooldown} · 흩뿌림 1.5 · 제압 24 (N39: 40 → 48, N58: 하향 24, 간격 0.26)");
        check(d.Weapons["bayonet"].Damage == 60 && Near(d.Weapons["bayonet"].CooldownMin, 1.5f) && Near(d.Weapons["bayonet"].CooldownMax, 2.0f) && d.Weapons["bayonet"].Accuracy == 0.6f
              && inf.Melee == "bayonet" && d.Units["recruit"].Melee == "bayonet" && d.Units["veteran"].Melee == "bayonet" && d.Units["scout"].Melee == null,
              "착검: 피해 60 · 간격 1.5~2.0 · 명중 0.6, 소총병 3종에 장착");
        bool armor0 = d.Units.Values.Where(u => u.IsInfantry).All(u => d.Armor(u.Armor)!.Armor == 0) && d.Armor(d.Units["tank_ft_male"].Armor)!.Armor > 0;
        check(armor0 && 3 * rifle.Damage >= inf.Hp && 2 * rifle.Damage < inf.Hp && 2 * d.Weapons["bayonet"].Damage >= inf.Hp,
              "보병 장갑 0 (2차 추가 D): 소총 3발 사망 · 2발 생존, 착검 2타 사망; 전차 장갑 유지");
        check(rifle.AccNear == 0.35f && rifle.AccFar == 0.08f && d.Weapons["carbine"].AccNear == 0.33f && d.Weapons["carbine"].AccFar == 0.08f && c.EngageRangeFrac == 0.7f,
              "소총 명중 0.35→0.08 · 카빈 0.33→0.08 · 반응 거리 70% (engage 2026-10-19)");
        check(rifle.Los && d.Weapons["carbine"].Los && d.Weapons["pistol"].Los && mgw.Los && d.Weapons["at_rifle"].Los && !d.Weapons["bayonet"].Los && !d.Weapons["howitzer_heavy"].Los, "보병 직사 무기 전부 사선 검사 (los), 근접 · 곡사는 아님 (2차 추가)");
        check(c.RankKills.SequenceEqual(new[] { 2, 5, 10, 18, 30, 48, 75, 110 }) && c.KillsCap == 128 && c.RankOf(0) == 0 && c.RankOf(1) == 0 && c.RankOf(2) == 1 && c.RankOf(29) == 4 && c.RankOf(30) == 5 && c.RankOf(109) == 7 && c.RankOf(110) == 8 && c.RankOf(128) == 8,
              "사병 9단계: 사살 0/2/5/10/18/30/48/75/110 → 삼등병 … 원사 (상한 128)");
        check(c.AcquireInterval >= 45 && c.AcquireRadiusCap == 32 && rifle.Suppress == 60 && d.Weapons["carbine"].Suppress == 50 && d.Weapons["at_rifle"].Suppress == 80 && d.Weapons["howitzer_heavy"].Suppress == 400,
              $"표적 재탐색 {c.AcquireInterval}틱(+{c.AcquireStagger} 분산) · 반경 상한 {c.AcquireRadiusCap} · 제압량 소총 60 · 카빈 50 · 대전차 80 · 기관총 150 · 포격 400");
    }

    // ---------------------------------------------------------------- 표적 분산 (§2-4b)

    /// <summary>횡대 20 vs 5×4 밀집 블록: 흔들림 없이는 앞줄 5기에 사격이 몰리고, 흔들림(4칸)이 있으면 뒷줄까지 고르게 퍼진다. 표적별 사격 수 분포를 보고한다.</summary>
    static void TargetSpread(GameOptions opt, Action<bool, string> check)
    {
        var rules = Defs.Current.CombatRules;
        float saved = rules.TargetSpreadJitter;
        try
        {
            (int distinct, int maxShots, int total, string hist) Run(float jitter)
            {
                rules.TargetSpreadJitter = jitter;
                var w = NewWorld(opt.Seed + 3);
                var c = Center(w);
                var shooters = Enumerable.Range(0, 20).Select(i => w.SpawnUnit(Defs.Current.Units["infantry"], 1, c + new Vector2(-6, -7.6f + i * 0.8f))).ToList();
                w.Hold(1, shooters);
                var targets = new List<Unit>();
                for (int i = 0; i < 20; i++) targets.Add(Dummy(w, 2, c + new Vector2(5 + i / 5 * 0.8f, -1.6f + i % 5 * 0.8f)));
                var count = new Dictionary<int, int>();
                for (int t = 0; t < 60 * 40; t++)
                {
                    foreach (var d in targets) d.Hp = 100000;
                    w.Step();
                    foreach (var s in shooters) if (s.LastShotTick == w.Tick - 1 && s.Target is { } tg) count[tg.Id] = count.GetValueOrDefault(tg.Id) + 1;
                }
                int total = count.Values.Sum();
                var rows = Enumerable.Range(0, 4).Select(r => Enumerable.Range(0, 5).Sum(k => count.GetValueOrDefault(targets[r * 5 + k].Id))).ToArray();
                string hist = $"열별(앞→뒤) {string.Join("/", rows)} · 표적 {count.Count}/20 · 최다 {(count.Count > 0 ? count.Values.Max() : 0)}발";
                return (count.Count, count.Count > 0 ? count.Values.Max() : 0, total, hist);
            }
            var before = Run(0f);
            var after = Run(saved);
            check(before.total > 200 && after.total > 200, $"표적 분산 표본: 흔들림 0 → {before.total}발, {saved}칸 → {after.total}발");
            check(after.distinct > before.distinct && after.maxShots < before.maxShots,
                  $"표적 분산 (흔들림 {saved}칸): 전 [{before.hist}] → 후 [{after.hist}]");
            // 결정성: 같은 시드면 같은 분포
            var again = Run(saved);
            check(again.hist == after.hist, "표적 분산은 결정적 (같은 시드 → 같은 분포)");
        }
        finally { rules.TargetSpreadJitter = saved; }
    }

    // ---------------------------------------------------------------- 제압 (§2-4b 6차 · §2-4c)

    static void Suppression(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current; var c = defs.CombatRules; var rifle = defs.Weapons["rifle"];
        // 징집병 · 정규군 · 정예 · 장교 표적 하나씩, 각각 사수 5기가 노린다. 줄마다 별도 월드 (같은 지도) — 다른 표적 · 분산과 무관, 저항만 잰다
        string[] kinds = { "recruit", "infantry", "veteran", "officer" };
        var ws = new World[4]; var tg = new Unit[4]; var sq = new List<Unit>[4]; var ctrs = new Vector2[4];
        for (int k = 0; k < 4; k++)
        {
            ws[k] = NewWorld(opt.Seed + 5);
            ctrs[k] = Center(ws[k]);
            Vector2 b = ctrs[k] + new Vector2(-6, -12);
            tg[k] = Dummy(ws[k], 2, b + new Vector2(0, 8), kinds[k]);
            sq[k] = Enumerable.Range(0, 5).Select(i => ws[k].SpawnUnit(defs.Units["infantry"], 1, b + new Vector2(-1.6f + i * 0.8f, 0))).ToList();
            ws[k].Hold(1, sq[k]);
        }
        int shotsAt0 = 0; var shotsBy = new int[4]; var peak = new int[4];   // peak: 6초 동안 표적별 최고 제압 (감소 후 최종값이 아니라 쌓이는 크기를 잰다)
        for (int t = 0; t < 60 * 6; t++)
        {
            for (int k = 0; k < 4; k++)
            {
                tg[k].Hp = 100000;
                ws[k].Step();
                peak[k] = Math.Max(peak[k], tg[k].Suppression);
                foreach (var s in sq[k]) if (s.LastShotTick == ws[k].Tick - 1) { shotsBy[k]++; if (k == 0) shotsAt0++; }
            }
        }
        var w = ws[1]; var ctr = ctrs[1];
        int s0 = peak[0], s1 = peak[1], s2 = peak[2], s3 = peak[3];
        check(shotsAt0 > 5 && s0 > 0 && s0 > s1 && s1 > s2 && s2 > 0 && s3 == 0,
              $"제압 증가 × 저항 (6초 최고치, 사수 5기): 징집 {s0} > 정규 {s1} > 정예 {s2} > 0, 장교 {s3} = 0 (면역) — 징집병 쪽 사격 {shotsAt0}발 (줄별 사격 {string.Join("/", shotsBy)})");
        check(s0 <= c.SuppressMax && tg[0].SuppressionRatio <= 1f, $"제압 상한 {c.SuppressMax}");
        // 효과: 사격 간격 · 이동 속도 (선형), 명중률 무관
        var probe = w.SpawnUnit(defs.Units["infantry"], 1, ctr + new Vector2(20, 20));
        float p0 = w.HitChance(probe, tg[1]);
        probe.Suppression = (short)c.SuppressMax;
        check(Near(w.UnitSpeed(probe), probe.Def.Speed * (1f - c.SuppressMoveSlow)) && Near(w.HitChance(probe, tg[1]), p0 * (1f - c.SuppressAccMul), 0.002f),
              $"제압 {c.SuppressMax}: 이동 속도 ×{1f - c.SuppressMoveSlow:0.00} ({w.UnitSpeed(probe):0.00}), 명중률 ×{1f - c.SuppressAccMul:0.00} (N25: {p0:0.000} → {w.HitChance(probe, tg[1]):0.000})");
        probe.Suppression = (short)(c.SuppressMax / 2);
        check(Near(w.UnitSpeed(probe), probe.Def.Speed * (1f - c.SuppressMoveSlow * 0.5f)), $"제압 절반: 이동 속도 ×{1f - c.SuppressMoveSlow * 0.5f:0.00} (선형)");
        // 사격 간격 배수: 표적을 반복 사격하는 유닛의 실측 쿨다운 평균이 제압 0 일 때의 ~2배
        float MeanCooldown(short sup)
        {
            var w2 = NewWorld(opt.Seed + 6);
            var c2 = Center(w2);
            var sh = w2.SpawnUnit(defs.Units["infantry"], 1, c2);
            w2.Hold(1, new[] { sh });
            var d = Dummy(w2, 2, c2 + new Vector2(4, 0));
            var ticks = new List<long>();
            for (int t = 0; t < 60 * 60; t++) { d.Hp = 100000; sh.Suppression = sup; w2.Step(); if (sh.LastShotTick == w2.Tick - 1) ticks.Add(w2.Tick - 1); }
            return ticks.Count < 3 ? 0 : (float)(ticks[^1] - ticks[0]) / (ticks.Count - 1) * Config.SimDt;
        }
        float brk = c.BreakChance; c.BreakChance = 0;   // supp: 붕괴 (제압 상한 병사가 달아나 사격 중단) 는 SuppressionSelfTest 에서 — 여기는 사격 간격 배수만
        float cd0 = MeanCooldown(0), cd1 = MeanCooldown((short)c.SuppressMax);
        c.BreakChance = brk;
        check(cd0 > rifle.CooldownMin - 0.05f && cd0 < rifle.CooldownMax + 0.05f && cd1 > cd0 * (1f + c.SuppressFireSlow) * 0.85f && cd1 < cd0 * (1f + c.SuppressFireSlow) * 1.15f,
              $"제압 최대 → 사격 간격 ×{1f + c.SuppressFireSlow:0.0}: 실측 {cd0:0.00}초 → {cd1:0.00}초");
        // 감소: 사격을 멈추면 초당 decay (0.25초마다) 로 0 까지. 계급 병장 = ×1.75, 장교 근처 ×3
        foreach (var k in sq) foreach (var s in k) s.Weapon = null;
        probe.Weapon = null;   // 기본 계급 1 은 멍칫이 줄어 probe 가 제압 측정 대상을 쏘게 됨
        var dec = new Unit[3];
        for (int i = 0; i < 3; i++) { dec[i] = Dummy(w, 2, ctr + new Vector2(10 + i * 3, 14), "infantry"); dec[i].Suppression = 800; dec[i].Hp = 100000; }   // 계급 1 기본: probe 등이 쏴서 죽지 않게 체력 고정 (제압 감소만 측정)
        dec[1].GrantKills(15);                                                     // 병장
        var officer = w.SpawnUnit(defs.Units["officer"], 2, dec[2].Pos + new Vector2(0, 2)); officer.Weapon = null; w.Hold(2, new[] { officer });   // dec[2] 만 지휘 범위 안 (dec[1] 과는 5칸 — 8칸 안이므로 더 멀리)
        officer.Pos = dec[2].Pos + new Vector2(6, 0); dec[0].Pos = ctr + new Vector2(-14, 14); dec[1].Pos = ctr + new Vector2(-6, 14);
        w.Step();
        bool aura = w.NearOfficer(dec[2]) && !w.NearOfficer(dec[1]) && !w.NearOfficer(dec[0]) && !w.NearOfficer(officer);
        int tick0 = (int)w.Tick;
        int reach0 = -1, reach1 = -1, reach2 = -1;
        for (int t = 0; t < 60 * 60; t++)
        {
            w.Step();
            if (reach0 < 0 && dec[0].Suppression == 0) reach0 = (int)(w.Tick - tick0);
            if (reach1 < 0 && dec[1].Suppression == 0) reach1 = (int)(w.Tick - tick0);
            if (reach2 < 0 && dec[2].Suppression == 0) reach2 = (int)(w.Tick - tick0);
        }
        // N25: 감소 = (decay + prop × 제압) × 배수, 0.25초마다 정수 (최소 1) — 같은 계단을 그대로 세어 기대값
        float Expect(float mul)
        {
            int iv = Math.Max(1, c.SuppressDecayInterval), s = 800, steps = 0;
            while (s > 0 && steps < 100000) { int dd = (int)MathF.Round((c.SuppressDecay + c.SuppressDecayProp * s) * iv * Config.SimDt * mul); s -= Math.Max(1, dd); steps++; }
            return steps * iv * Config.SimDt;
        }
        float expect0 = Expect(1f + dec[0].Rank * c.SuppressRankDecayBonus), expect1 = Expect(1f + 3 * c.SuppressRankDecayBonus), expect2 = Expect(c.OfficerDecayMul);
        check(aura && reach0 > 0 && reach1 > 0 && reach2 > 0 && Near(reach0 * Config.SimDt, expect0, 0.6f) && Near(reach1 * Config.SimDt, expect1, 0.6f) && Near(reach2 * Config.SimDt, expect2, 0.6f),
              $"제압 800 → 0 감소: 이병 {reach0 * Config.SimDt:0.0}초 (기대 {expect0:0.0}) · 병장 {reach1 * Config.SimDt:0.0}초 (기대 {expect1:0.0}) · 장교 근처 {reach2 * Config.SimDt:0.0}초 (기대 {expect2:0.0}), 지휘 범위 판정 {aura}");
        // 장교 근처 사격 간격 ×0.9 (FireIntervalMul)
        var w3 = NewWorld(opt.Seed + 8);
        var c3 = Center(w3);
        var rifleman = w3.SpawnUnit(defs.Units["infantry"], 1, c3);
        var ldr = w3.SpawnUnit(defs.Units["officer"], 1, c3 + new Vector2(3, 0));
        var far = w3.SpawnUnit(defs.Units["officer"], 2, c3 + new Vector2(30, 0));
        w3.Step();
        check(w3.NearOfficer(rifleman) && !w3.NearOfficer(ldr) && !w3.NearOfficer(far) && Near(w3.FireIntervalMul(rifleman), c.OfficerFireMul),
              $"장교 지휘 범위 안 병사: 사격 간격 ×{c.OfficerFireMul} (적 장교 · 장교 자신은 해당 없음)");
        ldr.Pos = c3 + new Vector2(9, 0); w3.Step();
        check(!w3.NearOfficer(rifleman) && Near(w3.FireIntervalMul(rifleman), 1f), "장교가 9칸 밖이면 효과 없음");
        // 포격: 폭발 반경 안 전원 +400 × 저항
        var w4 = NewWorld(opt.Seed + 9);
        var c4 = Center(w4);
        var gun = w4.SpawnUnit(defs.Units["heavy_howitzer"], 1, c4 + new Vector2(-12, 0));
        w4.Hold(1, new[] { gun });
        Dummy(w4, 1, c4 + new Vector2(-9, 4), "scout");   // 곡사포는 관측자 필요 (표적에서 포수 규율 반경 밖 — pursuit2 포병 오사)
        var cluster = Enumerable.Range(0, 5).Select(i => Dummy(w4, 2, c4 + new Vector2(i % 3 * 0.8f, i / 3 * 0.8f), "recruit")).ToList();
        int fired = -1;
        for (int t = 0; t < 60 * 20 && fired < 0; t++) { foreach (var u in cluster) u.Hp = 100000; w4.Step(); if (gun.LastShotTick == w4.Tick - 1) fired = (int)w4.Tick; }
        for (int t = 0; t < World.FlightTicks(gun.Weapon!, 12) + 2; t++) { foreach (var u in cluster) u.Hp = 100000; w4.Step(); }   // 포탄 비행 뒤 착탄 (ShellFlight.cs)
        int suppressed = cluster.Count(u => u.Suppression >= 300);
        check(fired > 0 && suppressed >= 3, $"포격 1발: 폭발 반경 안 징집병 {suppressed}/5 기 제압 ≥300 (+400 × 1.0, 퍼짐으로 일부 밖)");
    }

    // ---------------------------------------------------------------- 계급 (§2-4c 14차)

    static void Rank(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 11);
        var c = Center(w);
        var sh = w.SpawnUnit(defs.Units["veteran"], 1, c);
        w.Hold(1, new[] { sh });
        int kills = 0;
        for (int round = 0; round < 3; round++)
        {
            var d = Dummy(w, 2, c + new Vector2(2, 0)); d.Hp = 1;
            for (int t = 0; t < 60 * 20 && d.Alive; t++) w.Step();
            if (!d.Alive) kills++;
            Steps(w, 2);
        }
        int k0 = defs.CombatRules.RankKills[0];   // 기본 시작 계급 1 (이등병) = 사살 수 RankKills[0] 로 시작
        check(kills == 3 && sh.Kills == k0 + 3 && sh.Rank == 1 && Defs.Current.CombatRules.RankOf(0) == 0, $"사살 3 → 사살 수는 쌓이지만 전투 중 진급 없음 (기본 계급 1) (사살 {sh.Kills}, 계급 {sh.Rank})");
        sh.GrantKills(4); check(sh.Rank == 1, "배치 계급 4킬 = 이등병");
        { var rn = w.SpawnUnit(defs.Units["infantry"], 1, c); var rc = w.SpawnUnit(defs.Units["infantry"], 1, c); rc.GrantKills(0); check(rn.Rank == World.DefaultEnlistedRank && rc.Rank == 0, "사병 기본 계급 1 (이등병), 명시 0 (훈련병) 도 유효"); }
        sh.GrantKills(5); check(sh.Rank == 2, "배치 계급 5킬 = 일등병");
        sh.GrantKills(30); check(sh.Rank == 5 && sh.IsNco, "배치 계급 30킬 = 하사 (공식 부사관)");
        sh.GrantKills(29); check(sh.Rank == 4 && !sh.IsNco, "배치 계급 29킬 = 병장 (부사관 아님)");
        sh.GrantKills(110); check(sh.Rank == 8, "배치 계급 110킬 = 원사 (최고)");
        var off = w.SpawnUnit(defs.Units["officer"], 1, c + new Vector2(0, 3)); off.GrantKills(20);
        check(off.Rank == -1 && off.OfficerRank == 0 && off.IsOfficer && !off.IsNco, "장교는 사병 계급 없음 (-1), 장교 계급 0 = 기본 '장교'");
    }

    // ---------------------------------------------------------------- 착검 돌격 · 근접전 (§2-4b)

    static void Charge(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current; var rules = defs.CombatRules;
        // 자동 돌격: 대기 중 소총병 옆 1.5칸에 적 → 사격 없이 돌격 → 근접 2타(명중)에 사망
        var w = NewWorld(opt.Seed + 13);
        var c = Center(w);
        var inf = w.SpawnUnit(defs.Units["infantry"], 1, c);
        var foe = Dummy(w, 2, c + new Vector2(1.5f, 0));
        int shots = 0, strikes = 0, hits = 0; long died = -1; bool charged = false;
        for (int t = 0; t < 60 * 30 && foe.Alive; t++)
        {
            w.Step();
            if (inf.ChargeState == 1) charged = true;
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == inf.Id) shots++;
            foreach (var m in w.MeleeStrikes) if (m.Tick == w.Tick - 1 && m.AttackerId == inf.Id) { strikes++; if (m.Hit) hits++; }
            if (!foe.Alive) died = w.Tick;
        }
        check(charged && shots == 0 && hits == 2 && !foe.Alive && died > 0, $"자동 돌격 (적 1.5칸): 사격 {shots}발 · 근접 {strikes}회 (명중 {hits}) → 보병 100 사망 {died * Config.SimDt:0.0}초");
        Steps(w, 3);
        check(inf.ChargeState == 0 && inf.Target == null, "표적이 죽으면 자동 돌격 끝");
        // 사수 유닛은 자동 돌격하지 않음 (점사)
        var w1 = NewWorld(opt.Seed + 13);
        var c1 = Center(w1);
        var holder = w1.SpawnUnit(defs.Units["infantry"], 1, c1); w1.Hold(1, new[] { holder });
        var foe1 = Dummy(w1, 2, c1 + new Vector2(1.5f, 0));
        int hshots = 0;
        for (int t = 0; t < 60 * 6; t++) { foe1.Hp = 100000; w1.Step(); if (holder.LastShotTick == w1.Tick - 1 && w1.Shots.Any(s => s.Tick == w1.Tick - 1 && s.ShooterId == holder.Id)) hshots++; }
        check(holder.ChargeState == 0 && hshots > 0 && !holder.Walking, $"사수 명령 유닛은 돌격하지 않고 사격 ({hshots}발)");
        // 명령 돌격 (적 지정): 사거리 안이어도 쏘지 않고 +30% 속도로 달려가 근접
        var w2 = NewWorld(opt.Seed + 14);
        var c2 = Center(w2);
        var ch = w2.SpawnUnit(defs.Units["infantry"], 1, c2 + new Vector2(-10, 0));
        var tgt = Dummy(w2, 2, c2 + new Vector2(2, 0));
        w2.Charge(1, new[] { ch }, tgt.Pos, tgt);
        float maxSpd = 0; int shots2 = 0; long contact = -1;
        for (int t = 0; t < 60 * 12 && tgt.Alive; t++)
        {
            w2.Step();
            maxSpd = MathF.Max(maxSpd, ch.Velocity.Length());
            foreach (var s in w2.Shots) if (s.Tick == w2.Tick - 1 && s.ShooterId == ch.Id) shots2++;
            if (contact < 0 && ch.AimTarget == tgt.Id && ch.Charging) contact = w2.Tick;
        }
        check(ch.ChargeState is 2 or 0 && shots2 == 0 && Near(maxSpd, ch.Def.Speed * rules.ChargeSpeedMul, 0.01f) && contact > 0 && !tgt.Alive,
              $"명령 돌격 (적 지정): 사격 {shots2}발 · 최고 속도 {maxSpd:0.00} (= {ch.Def.Speed} × {rules.ChargeSpeedMul}) · 접촉 {contact * Config.SimDt:0.0}초 · 사살 {!tgt.Alive}");
        Steps(w2, 2);
        check(ch.ChargeState == 0 && ch.Order == UnitOrder.Idle, "지정 표적 사살 → 명령 끝 · 돌격 해제");
        // 명령 돌격 (지점): 도착까지 돌격 상태, 도착하면 해제. 근접 무기 없는 유닛(정찰병)은 명령을 받지 않음
        var w3 = NewWorld(opt.Seed + 15);
        var c3 = Center(w3);
        var a = w3.SpawnUnit(defs.Units["infantry"], 1, c3); var sc = w3.SpawnUnit(defs.Units["scout"], 1, c3 + new Vector2(0, 1));
        w3.Charge(1, new[] { a, sc }, c3 + new Vector2(6, 0));
        Steps(w3, 10);
        bool mid = a.ChargeState == 2 && a.Order == UnitOrder.AttackMove && sc.ChargeState == 0 && sc.Order == UnitOrder.Idle;
        Steps(w3, 60 * 6);
        check(mid && a.ChargeState == 0 && a.Order == UnitOrder.Idle && Vector2.Distance(a.Pos, c3 + new Vector2(6, 0)) < 1f, $"명령 돌격 (지점): 이동 중 돌격 {mid} → 도착 후 해제 (정찰병은 제외)");
        // 장교: 곡도 돌격, 피해 60 → 2타
        var w4 = NewWorld(opt.Seed + 16);
        var c4 = Center(w4);
        defs.CombatRules.OfficerAvoid = false;   // N35: 장교 단독 돌격은 기본 차단 — 곡도 규칙만 보려고 끔
        var off = w4.SpawnUnit(defs.Units["officer"], 1, c4);
        var vic = Dummy(w4, 2, c4 + new Vector2(1.2f, 0));
        int ohits = 0;
        for (int t = 0; t < 60 * 20 && vic.Alive; t++) { w4.Step(); foreach (var m in w4.MeleeStrikes) if (m.Tick == w4.Tick - 1 && m.AttackerId == off.Id && m.Hit) ohits++; }
        defs.CombatRules.OfficerAvoid = true;
        check(!vic.Alive && ohits == 3 && off.CanCharge && off.Melee!.Key == "sabre", $"장교 자동 돌격 (곡도 40): 3타 사살 (명중 {ohits})");
    }

    // ---------------------------------------------------------------- 장갑 · 소형화기 면역 · 대전차소총 우선 · 신규 유닛 (§2-4f)

    /// <summary>shooter(종류) 가 target(종류) 에게 명시 공격 명령으로 secs 초간 쏜 결과: (발사 수, 총 깎인 체력, 깎임 값 집합).</summary>
    static (int shots, float lost, HashSet<float> drops) Duel(GameOptions opt, string shooterKind, string targetKind, float dist, float secs)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 31);
        var c = Center(w);
        var s = w.SpawnUnit(defs.Units[shooterKind], 1, c);
        s.Grenades = 0;   // 소형화기 면역 검사 — 수류탄(묶음 폭약)은 제외
        var t = Dummy(w, 2, c + new Vector2(dist, 0), targetKind);
        w.Attack(1, new[] { s }, t);
        int shots = 0; float lost = 0; var drops = new HashSet<float>();
        for (int i = 0; i < (int)(secs * 60); i++)
        {
            t.Hp = 1000; w.Step();
            if (t.Hp < 1000) { lost += 1000 - t.Hp; drops.Add(1000 - t.Hp); }
            foreach (var sh in w.Shots) if (sh.Tick == w.Tick - 1 && sh.ShooterId == s.Id) shots++;
        }
        return (shots, lost, drops);
    }

    static void ArmorRules(GameOptions opt, Action<bool, string> check)
    {
        var d = Defs.Current;
        // 데이터: 신규 유닛 로드 · 소형화기 면역 · 대전차소총만 보너스
        string[] small = { "rifle", "carbine", "carbine_assault", "pistol", "mg", "bayonet", "sabre", "sabre_short" };
        check(small.All(k => !d.Weapons[k].CanDamage("armored") && d.Weapons[k].CanDamage("infantry")) && d.Weapons["at_rifle"].CanDamage("armored") && d.Weapons["ft_gun"].CanDamage("armored")
              && d.Weapons["ft_gun"].CanDamage("armored") && d.Weapons["howitzer_heavy"].CanDamage("armored"), "소형화기(소총·카빈·돌격 카빈·권총·기관총·착검·곡도·단검) 는 기갑 면역, 대전차소총 · 포는 기갑 가능");
        string[] fresh = { "tank_ft_female", "tank_ft_male", "tank_armored_car", "infantry_assault" };
        check(fresh.All(k => d.Units.ContainsKey(k) && d.Weapon(d.Units[k].Weapon) != null && d.Armor(d.Units[k].Armor) != null)
              && d.Units["tank_ft_female"].Weapon == "mg" && d.Units["tank_ft_female"].SetupTime == 0 && d.Units["tank_ft_male"].Weapon == "ft_gun" && d.Weapons["ft_gun"].Los
              && d.Units["tank_armored_car"].Weapon == "mg" && d.Units["tank_armored_car"].Speed > d.Units["infantry"].Speed && d.Units["tank_ft_male"].Speed < d.Units["infantry"].Speed
              && d.Units["infantry_assault"].Melee == "sabre_short" && fresh.All(k => d.Units.ContainsKey(k)), "신규 유닛 4종 로드 (FT 암컷 = 기관총 · 거치 없음, 수컷 = 37mm 직사 + 사선, 장갑차 빠름, 돌격보병 단검)");
        var at = d.Units["heavy_at_rifleman"];
        check(at.SetupTime == 3 && at.PackTime == 2 && d.Weapons["at_rifle"].PreferCategory == "armored", "대전차소총병: 거치 3 / 해제 2, 장갑 표적 우선");
        // 돌격 카빈 수치 (N21 → N23 난사): 사거리 16, 간격 1.1, 피해 35, 제압 50, 명중 0.25/0.04
        var smg = d.Weapons["carbine_assault"]; var rifle = d.Weapons["rifle"];
        check(Near(smg.Cooldown, 1.1f, 0.01f) && Near(smg.Range, 16f, 0.05f) && smg.Range < rifle.Range && smg.Cooldown < rifle.Cooldown && smg.Suppress == 50 && Near(smg.AccNear, 0.25f, 0.001f) && Near(smg.AccFar, 0.04f, 0.001f)
              && 4 * smg.Damage >= d.Units["infantry_assault"].Hp && 3 * smg.Damage < d.Units["infantry_assault"].Hp && 3 * smg.Damage >= d.Units["infantry"].Hp && 2 * smg.Damage < d.Units["infantry"].Hp && d.Units["infantry_assault"].Grenades == 2,
              $"돌격 카빈: 간격 {smg.Cooldown}, 사거리 {smg.Range} (소총 {rifle.Range}), 제압 {smg.Suppress}, 피해 {smg.Damage} → 돌격보병 4발 사망 · 3발 생존 (hp 130), 소총병 3발 사망 · 2발 생존, 수류탄 2");
        check(!d.Weapons.ContainsKey("smg") && d.Units["infantry_assault"].KoreaOnly, "기관단총(smg) 정의 제거, 돌격보병 = 한국군 전용");

        // 면역: 명시 공격을 해도 장갑 차량 피해 0 (사격은 함). 대전차소총은 깎는다
        var sb = new List<string>(); bool allZero = true;
        foreach (var tgt in new[] { "tank_armored_car", "tank_ft_male", "tank_ft_female" })
            foreach (var (sk, dist) in new[] { ("infantry", 3f), ("scout", 3f), ("officer", 3f), ("heavy_machinegunner", 3f), ("infantry_assault", 3f), ("tank_ft_female", 4f), ("tank_armored_car", 4f) })
            {
                var r = Duel(opt, sk, tgt, dist, sk == "heavy_machinegunner" ? 12 : 8);
                if (r.lost != 0 || r.shots == 0) { allZero = false; sb.Add($"{sk}→{tgt}: 발사 {r.shots} 피해 {r.lost}"); }
            }
        check(allZero, "소형화기 (소총 · 카빈 · 돌격 카빈 · 권총 · 기관총 · 차량 기관총) 는 전차 · 장갑차 · FT 에 피해 0 (사격은 함)" + (sb.Count > 0 ? " — 실패: " + string.Join("; ", sb) : ""));
        var atT = Duel(opt, "heavy_at_rifleman", "tank_ft_male", 3, 20);
        var atC = Duel(opt, "heavy_at_rifleman", "tank_armored_car", 3, 20);
        check(atT.lost > 0 && atT.drops.SetEquals(new[] { 34f }) && atC.lost > 0 && atC.drops.SetEquals(new[] { 34f }), $"대전차소총: FT 34 · 장갑차 34 (35-1) 씩 (측정 {string.Join(",", atT.drops)} / {string.Join(",", atC.drops)})");
        var ftm = Duel(opt, "tank_ft_male", "tank_armored_car", 6, 20);
        check(ftm.lost > 0 && ftm.drops.SetEquals(new[] { 24f }), $"FT 수컷 37mm: 장갑차에 25-1 = 24 (측정 {string.Join(",", ftm.drops)})");
        var ftmI = Duel(opt, "tank_ft_male", "infantry", 6, 20);
        check(ftmI.lost > 0 && ftmI.drops.SetEquals(new[] { 25f }), $"FT 수컷 37mm: 보병에 25 (측정 {string.Join(",", ftmI.drops)})");

        // 3발 사살 (돌격 카빈, 사거리 3)
        {
            var w = NewWorld(opt.Seed + 33); var c = Center(w);
            var s = w.SpawnUnit(d.Units["infantry_assault"], 1, c);
            var v = Dummy(w, 2, c + new Vector2(12, 0), "infantry");
            w.Attack(1, new[] { s }, v);
            int hits = 0; float dmgSum = 0; var used1 = new HashSet<string>();
            for (int t = 0; t < 60 * 120 && v.Alive; t++) { s.Ammo = s.AmmoMax; w.Step(); foreach (var sh in w.Shots) if (sh.Tick == w.Tick - 1 && sh.ShooterId == s.Id) { if (sh.Hit) { hits++; dmgSum += s.Weapon!.Damage; } used1.Add(s.Weapon!.Key); } }
            check(!v.Alive && dmgSum >= 100 && used1.Contains("pistol_assault") && hits >= 3, $"거리 12 에서 접근 → 6칸 안에서 권총으로 사살 (명중 {hits}발, 피해 합 {dmgSum}, 무기 {string.Join(",", used1)})");
            // 근접 보조 권총 (N21): 6칸 안 = 권총 (피해 20 → 5발), 밖 = 카빈, 결정적 전환
            var w2 = NewWorld(opt.Seed + 34); var c2 = Center(w2);
            var s2 = w2.SpawnUnit(d.Units["infantry_assault"], 1, c2);
            var v2 = Dummy(w2, 2, c2 + new Vector2(3, 0), "infantry");
            w2.Attack(1, new[] { s2 }, v2);
            int hits2 = 0; var used = new HashSet<string>();
            for (int t = 0; t < 60 * 120 && v2.Alive; t++) { w2.Step(); if (s2.Weapon != null) used.Add(s2.Weapon.Key); foreach (var sh in w2.Shots) if (sh.Tick == w2.Tick - 1 && sh.ShooterId == s2.Id && sh.Hit) hits2++; }
            check(!v2.Alive && hits2 == 5 && used.Contains("pistol_assault"), $"근접 보조 권총: 거리 3 = 권총 5발 사살 (명중 {hits2}발, 무기 {string.Join(",", used)})");
            var w3 = NewWorld(opt.Seed + 35); var c3 = Center(w3);
            var s3 = w3.SpawnUnit(d.Units["infantry_assault"], 1, c3);
            var v3 = Dummy(w3, 2, c3 + new Vector2(12, 0), "infantry");
            w3.Attack(1, new[] { s3 }, v3);
            for (int t = 0; t < 30; t++) w3.Step();
            check(s3.Weapon!.Key == "carbine_assault" && d.Weapons["pistol_assault"].Range == d.Units["infantry_assault"].SidearmRange && d.Units["infantry_assault"].Sidearm == "pistol_assault" && d.Units["infantry_assault"].Melee == "sabre_short", "6칸 밖에서는 카빈, 근접 무기는 단검");
        }

        // 대전차소총병 우선: 보병이 더 가깝고 전차가 더 멀어도 전차를 먼저
        {
            var w = NewWorld(opt.Seed + 35); var c = Center(w);
            var s = w.SpawnUnit(d.Units["heavy_at_rifleman"], 1, c);
            w.Hold(1, new[] { s });
            var near = Dummy(w, 2, c + new Vector2(4, 0.5f), "infantry");
            var far = Dummy(w, 2, c + new Vector2(8, -0.5f), "tank_ft_male");
            check(w.Map.HasLineOfSight(s.Pos, far.Pos, s.Id, far.Id) && w.Map.HasLineOfSight(s.Pos, near.Pos, s.Id, near.Id), "(전제) 두 표적 모두 사선 트임");
            Steps(w, 60 * 10);
            check(s.Target == far && far.Hp < far.Def.Hp && near.Hp == near.Def.Hp && s.Deployed, $"대전차소총병: 가까운 보병 대신 장갑 표적 우선 (표적 {(s.Target == far ? "전차" : s.Target == near ? "보병" : "없음")}, 전차 체력 {far.Hp}, 거치 {s.Deployed})");
            // 장갑이 없으면 보병을 쏜다
            far.Hp = 0; Steps(w, 60 * 6);
            check(near.Hp < near.Def.Hp || !near.Alive, "장갑 표적이 없으면 보병을 쏜다");
        }
        // 소총병은 면역 차량을 자동 표적으로 잡지 않고 보병을 쏜다
        {
            var w = NewWorld(opt.Seed + 37); var c = Center(w);
            var s = w.SpawnUnit(d.Units["infantry"], 1, c);
            w.Hold(1, new[] { s });
            var tk = Dummy(w, 2, c + new Vector2(4, 0), "tank_ft_male");
            var inf = Dummy(w, 2, c + new Vector2(12, 0.5f), "infantry");
            Steps(w, 60 * 8);
            check(tk.Hp == tk.Def.Hp && s.Target == inf && s.LastShotTick > 0, $"소총병: 면역 전차 대신 멀리 있는 보병을 자동 표적으로 사격 (표적 {(s.Target == inf ? "보병" : s.Target == tk ? "전차" : "없음")})");
        }
    }

    // ---------------------------------------------------------------- 기관총 흩뿌림 (§2-4e)

    static void Spray(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 17);
        var c = Center(w);
        var mg = w.SpawnUnit(defs.Units["heavy_machinegunner"], 1, c + new Vector2(-15, 0));   // 사거리 24 의 같은 비율 (옛 16 에서 -10)
        w.Hold(1, new[] { mg });
        var block = Enumerable.Range(0, 9).Select(i => Dummy(w, 2, c + new Vector2(i % 3 * 0.8f, -0.8f + i / 3 * 0.8f), "recruit")).ToList();
        var friend = Dummy(w, 1, c + new Vector2(0.8f, 1.6f), "recruit");   // 적 블록 바로 옆의 아군 (오사 없음)
        foreach (var u in block) u.Hp = 1000;
        long firstShot = -1; int shots = 0, hits = 0;
        var dmg = new Dictionary<int, float>();
        var peakSup = new Dictionary<int, int>();
        for (int t = 0; t < 60 * 20; t++)
        {
            foreach (var u in block) peakSup[u.Id] = Math.Max(peakSup.GetValueOrDefault(u.Id), u.Suppression);
            foreach (var u in block) if (u.Hp < 1000) { dmg[u.Id] = dmg.GetValueOrDefault(u.Id) + (1000 - u.Hp); u.Hp = 1000; }
            w.Step();
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == mg.Id) { shots++; if (s.Hit) hits++; if (firstShot < 0) firstShot = w.Tick - 1; }
        }
        foreach (var u in block) if (u.Hp < 1000) { dmg[u.Id] = dmg.GetValueOrDefault(u.Id) + (1000 - u.Hp); u.Hp = 1000; }
        int setup = World.SetupTicks(mg.Def);
        var lw = defs.Weapons["lmg"];
        float burstT = (lw.BurstMin + lw.BurstMax) * 0.5f * lw.CooldownMean, pauseT = (lw.BurstPauseMin + lw.BurstPauseMax) * 0.5f;
        float expectShots = (20f - setup * Config.SimDt) / lw.CooldownMean * burstT / (burstT + pauseT);   // 점사 평균 가동률 (간격 0.13초, 점사 15발 ≈ 2초 + 휴지 1.5초) ≈ 66
        check(firstShot >= setup && mg.Deployed && shots >= expectShots * 0.65f && shots <= expectShots * 1.35f, $"기관총: 거치 {setup}틱 뒤 첫 발 (틱 {firstShot}), 20초간 {shots}발 (기대 ≈{expectShots:0}, 명중 {hits})");
        int damaged = dmg.Count, suppressed = block.Count(u => u.Suppression > 0);
        check(damaged >= 3 && hits == (int)MathF.Round(dmg.Values.Sum() / defs.Weapons["lmg"].Damage), $"흩뿌림: 명중 {hits}발이 {damaged}/9 기에 분산 (표적 외 유닛도 맞음), 피해 합 = 명중 × 40");
        check(suppressed >= 0 && block.All(u => peakSup.GetValueOrDefault(u.Id) > 20), $"범위 제압: 블록 9기 전원 제압 (점사 휴지에는 감쇠하므로 최고치 기준: 최소 {block.Min(u => peakSup.GetValueOrDefault(u.Id))}, 최대 {block.Max(u => peakSup.GetValueOrDefault(u.Id))})");
        check(friend.Hp >= 100 && friend.Suppression == 0, $"아군 오사 없음 (체력 {friend.Hp:0}, 제압 {friend.Suppression})");
        // 이동 명령 → 해제 후 출발
        w.Move(1, new[] { mg }, mg.Pos + new Vector2(-6, 0));
        long m0 = w.Tick, walk = -1;
        for (int t = 0; t < 60 * 6 && walk < 0; t++) { w.Step(); if (mg.Walking) walk = w.Tick - 1; }
        int pack = World.PackTicks(mg.Def);
        check(walk > 0 && walk - m0 >= pack - 1 && walk - m0 <= pack + 6, $"기관총 이동 명령: 해제 {pack}틱 뒤 출발 (실제 {walk - m0}틱)");
    }

    // ---------------------------------------------------------------- 멍칫 (§2-4d 14차)

    static void Hesitation(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current; var c = defs.CombatRules;
        // 제압 최대로 고정한 보병 10기가 60칸 이동: 유예(3초) 뒤 멍칫이 생기되 평균 속도 손실 ≤ 상한(5%) + 여유
        var w = NewWorld(opt.Seed + 19);
        var ctr = Center(w);
        var sq = Enumerable.Range(0, 10).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, ctr + new Vector2(-30, -4 + i * 0.8f))).ToList();
        w.Move(1, sq, ctr + new Vector2(30, 0));
        int graceTicks = (int)(c.HesitateGraceSec / Config.SimDt);
        int moving = 0, stalled = 0, stalls = 0, graceStalls = 0;
        var prev = sq.ToDictionary(u => u.Id, u => 0);
        for (int t = 0; t < 60 * 40; t++)
        {
            foreach (var u in sq) u.Suppression = (short)(c.PinAt * c.SuppressMax - 1);   // N25: 고정 (≥ pinAt) 바로 아래 — 고정되면 서서 쏘므로 걷지 않는다
            w.Step();
            foreach (var u in sq)
            {
                if (u.Order != UnitOrder.Move) continue;
                bool stall = u.ReactDelay > 0 || (u.Velocity == Vector2.Zero && w.Tick - u.LastOrderTick > 4);
                bool start = u.ReactDelay > 0 && prev[u.Id] == 0;
                prev[u.Id] = u.ReactDelay;
                if (w.Tick - u.LastOrderTick <= graceTicks) { if (start && w.Tick - u.LastOrderTick > 4) graceStalls++; continue; }
                moving++;
                if (stall) stalled++;
                if (start) stalls++;
            }
        }
        float loss = moving > 0 ? (float)stalled / moving : 0;
        check(stalls > 0 && loss <= c.HesitateMaxLoss + 0.02f && graceStalls == 0,
              $"멍칫 (제압 {c.PinAt * c.SuppressMax - 1:0} = 고정 바로 아래, 10기 60칸): 발동 {stalls}회, 멈춘 틱 비율 {loss * 100:0.0}% ≤ {c.HesitateMaxLoss * 100:0}%+2, 이동 명령 뒤 {c.HesitateGraceSec}초 안 멈춤 {graceStalls}");
        // 제압이 기준 미만이면 멍칫 없음
        var w2 = NewWorld(opt.Seed + 19);
        var c2 = Center(w2);
        var sq2 = Enumerable.Range(0, 10).Select(i => w2.SpawnUnit(defs.Units["infantry"], 1, c2 + new Vector2(-30, -4 + i * 0.8f))).ToList();
        w2.Move(1, sq2, c2 + new Vector2(30, 0));
        int low = 0;
        for (int t = 0; t < 60 * 30; t++) { foreach (var u in sq2) u.Suppression = (short)(c.HesitateMinSuppress - 1); w2.Step(); foreach (var u in sq2) if (u.Order == UnitOrder.Move && u.ReactDelay > 0 && w2.Tick - u.LastOrderTick > 10) low++; }
        check(low == 0, $"제압 {c.HesitateMinSuppress - 1} (기준 미만): 멍칫 없음 ({low})");
        // 돌격 중에는 멍칫 없음
        var w3 = NewWorld(opt.Seed + 20);
        var c3 = Center(w3);
        var ch = w3.SpawnUnit(defs.Units["infantry"], 1, c3 + new Vector2(-20, 0));
        var tgt = Dummy(w3, 2, c3 + new Vector2(10, 0));
        w3.Charge(1, new[] { ch }, tgt.Pos, tgt);
        int chStall = 0;
        for (int t = 0; t < 60 * 15 && tgt.Alive; t++) { ch.Suppression = (short)(c.PinAt * c.SuppressMax - 1); tgt.Hp = 100000; w3.Step(); if (ch.Charging && ch.ReactDelay > 0 && w3.Tick - ch.LastOrderTick > 10) chStall++; }
        check(chStall == 0 && ch.Charging, $"돌격 중 멍칫 없음 ({chStall})");
    }

    // ---------------------------------------------------------------- 반응 지연 (§2-4d) · 교전 거리 (2차 추가 A)

    static void ReactAndEngageRange(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current; var c = defs.CombatRules;
        var w = NewWorld(opt.Seed + 21);
        var ctr = Center(w);
        var sq = Enumerable.Range(0, 40).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, ctr + new Vector2(-10 + i % 8 * 0.8f, -10 + i / 8 * 0.8f))).ToList();
        w.Move(1, sq, ctr + new Vector2(10, 10));
        w.Step();   // 명령 실행 틱 (ReactDelay 부여 → 이 틱에 이미 1 감소했을 수 있음)
        var delays = sq.Select(u => u.ReactDelay).ToList();
        bool range = delays.All(d => d >= 0 && d <= c.ReactDelayMaxTicks) && delays.Distinct().Count() >= 2 && sq.All(u => u.LastOrderTick == w.Tick - 1);
        check(range, $"반응 지연 0~{c.ReactDelayMaxTicks}틱 (유닛마다 다름: {string.Join(",", delays.Distinct().OrderBy(x => x))}), 명령 표시 틱은 즉시");
        // 같은 유닛 · 다른 명령 틱 → 다른 지연이 나올 수 있음 (결정적이지만 명령마다)
        // 교전 거리: 공격 이동 보병은 사거리 끝(16)에서 멈추지 않고 80%(12.8) 까지 접근한 뒤 첫 발
        var w2 = NewWorld(opt.Seed + 22);
        var c2 = Center(w2);
        var inf = w2.SpawnUnit(defs.Units["infantry"], 1, c2 + new Vector2(-1.25f * defs.Weapons["rifle"].Range, 0));   // 사거리 밖에서 출발 (교전 거리 100%)
        var tg = Dummy(w2, 2, c2 + new Vector2(0, 0));
        w2.AttackMove(1, new[] { inf }, c2 + new Vector2(4, 0));
        float firstDist = -1;
        for (int t = 0; t < 60 * 20 && firstDist < 0; t++) { tg.Hp = 100000; w2.Step(); if (inf.LastShotTick == w2.Tick - 1) firstDist = tg.EdgeDistance(inf.Pos) - inf.Radius; }
        float R = defs.Weapons["rifle"].Range;
        check(firstDist > 0 && firstDist <= R * c.EngageRangeFrac + 0.2f && firstDist > R * c.EngageRangeFrac - 8.0f, $"공격 이동: 첫 발 거리 {firstDist:0.00} (사거리 {R} × {c.EngageRangeFrac} = {R * c.EngageRangeFrac:0.0})");
        // 사수는 최대 사거리에서 쏜다
        var w3 = NewWorld(opt.Seed + 22);
        var c3 = Center(w3);
        var h = w3.SpawnUnit(defs.Units["infantry"], 1, c3 + new Vector2(-15, 0)); w3.Hold(1, new[] { h });
        var tg3 = Dummy(w3, 2, c3 + new Vector2(0, 0));
        int hs = 0;
        for (int t = 0; t < 60 * 8; t++) { tg3.Hp = 100000; w3.Step(); if (h.LastShotTick == w3.Tick - 1) hs++; }
        check(hs > 0 && !h.Walking && Near(h.Pos.X, c3.X - 15, 0.05f), $"사수: 15칸(사거리 안)에서 제자리 사격 {hs}발");
        // 표적 유지: 표적이 살아 있고 보이고 사거리 안이면 재탐색하지 않는다 (더 가까운 적이 나타나도 그대로)
        var w4 = NewWorld(opt.Seed + 23);
        var c4 = Center(w4);
        var sh = w4.SpawnUnit(defs.Units["infantry"], 1, c4); w4.Hold(1, new[] { sh });
        var first = Dummy(w4, 2, c4 + new Vector2(8, 0));
        for (int t = 0; t < 60 * 3; t++) { first.Hp = 100000; w4.Step(); }
        var closer = Dummy(w4, 2, c4 + new Vector2(3, 0));
        bool kept = true;
        for (int t = 0; t < 60 * 6; t++) { first.Hp = 100000; closer.Hp = 100000; w4.Step(); if (sh.Target != first) kept = false; }
        check(kept && sh.LastShotTick > 0, "표적 유지: 살아 있고 보이는 사거리 안 표적은 더 가까운 적이 나타나도 바꾸지 않음 (2차 추가 C)");
    }

    // ---------------------------------------------------------------- 저장 동일성

    static void SaveLoad(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 25);
        var c = Center(w);
        var sq = Enumerable.Range(0, 8).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, c + new Vector2(-8, -3 + i * 0.8f))).ToList();
        var off = w.SpawnUnit(defs.Units["officer"], 1, c + new Vector2(-9, 0));
        var mg = w.SpawnUnit(defs.Units["heavy_machinegunner"], 1, c + new Vector2(-10, 2));
        var foes = Enumerable.Range(0, 8).Select(i => w.SpawnUnit(defs.Units["recruit"], 2, c + new Vector2(8, -3 + i * 0.8f))).ToList();
        w.AttackMove(1, sq.Append(off).Append(mg).ToList(), c + new Vector2(8, 0));
        w.AttackMove(2, foes, c + new Vector2(-8, 0));
        Steps(w, 60 * 8);
        sq[0].GrantKills(9); sq[1].HesitateBlockUntil = w.Tick + 300;
        bool mid = w.Units.Any(u => u.Suppression > 0) && w.Units.Any(u => u.Charging || u.Kills > 0);
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = y.Units.Count == w.Units.Count && w.Units.Zip(y.Units, (a, b) => a.Suppression == b.Suppression && a.Kills == b.Kills && a.ChargeState == b.ChargeState && a.HesitateBlockUntil == b.HesitateBlockUntil).All(x => x)
                    && y.ComputeChecksum() == w.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        check(mid && same, $"교전 중 저장 (제압 · 사살 · 돌격 · 멍칫 상한 포함, 상태 {mid}): 불러온 월드 필드 · 체크섬 · 재저장 바이트 동일");
        bool cont = true; long div = -1;
        for (int i = 0; i < 600; i++) { w.Step(); y.Step(); if (i % 25 == 24 && w.ComputeChecksum() != y.ComputeChecksum()) { cont = false; div = w.Tick; break; } }
        check(cont, $"저장 → 불러오기 뒤 10초 이어 돌려도 체크섬 동일{(cont ? "" : $" (어긋남 틱 {div})")}");
    }
}
