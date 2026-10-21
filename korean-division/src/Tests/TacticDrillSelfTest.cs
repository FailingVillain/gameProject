using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 중대 전술 틀 2단계 (역할별 행동 · 전환 · 지휘관 판단, src/Core/TacticDrill.cs) + 1단계 겹침 수정 + 자료 해시.
/// 창 없이. KD_SELFTEST_ONLY=drill 로 단독 실행.
/// </summary>
public static class TacticDrillSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("drill", 1, false, "standard");
    static readonly Vector2 North = new(0, -1);

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[전술 틀 2단계 — 역할 행동 · 전환 · 지휘관]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestDataHash();
            TestFrontage();
            TestFireHoldAndBound();
            TestManoeuvreFlank();
            TestChargeAndLift();
            TestSupportThreat();
            TestDefenceDepth();
            TestArtilleryDeploy();
            TestCommanderGating();
            TestDecideTiming();
            TestPassiveAndDisengage();
            TestSaveMidDrill();
            TestHeavyPacing();
        }
        catch (Exception e) { check(false, $"전술 틀 2단계 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World Bare(int size = 160)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static List<Unit> Spawn(World w, string def, int n, Vector2 c, int cols = 6, int faction = 1)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++) list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * 1.1f, (i / cols) * 1.1f)));
        return list;
    }

    /// <summary>소총 rifles + 기관총 mgs + 장교 officers = 중대 하나.</summary>
    static Company Rifles(World w, Vector2 at, int rifles = 36, int mgs = 4, int officers = 1, string rifle = "infantry")
    {
        var us = Spawn(w, rifle, rifles, at, 12);
        if (mgs > 0) us.AddRange(Spawn(w, "heavy_machinegunner", mgs, at + new Vector2(0, 4), 4));
        for (int i = 0; i < officers; i++) us.Add(w.SpawnUnit(defs.Units["officer"], 1, at + new Vector2(i * 1.2f, 6)));
        w.FormCompany(1, us);
        w.Step();
        return us[0].Company!;
    }

    static List<Unit> Foes(World w, int n, Vector2 at, string def = "infantry")
    {
        var f = Spawn(w, def, n, at, 10, 2);
        w.Hold(2, f);
        return f;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    /// <summary>화력 우세를 흉내: 적을 매 틱 제압 0.9 로 (behaviour.superioritySupp · 개활지 우회 manoeuvreOpenSupp 이상).</summary>
    static void Pin(IEnumerable<Unit> foes) { foreach (var f in foes) if (f.Alive) f.Suppression = (short)(defs.CombatRules.SuppressMax * 0.9f); }
    static void PinSteps(World w, List<Unit> foes, int n) { for (int i = 0; i < n; i++) { Pin(foes); w.Step(); } }

    static Vector2 Mean(IEnumerable<Vector2> ps) { Vector2 s = Vector2.Zero; int n = 0; foreach (var p in ps) { s += p; n++; } return n > 0 ? s / n : s; }

    static List<Unit> Role(Company c, UnitRole r) => c.Members.Where(u => u.Role == r).ToList();

    static Company Placed(World w, CompanyTactic t, Vector2 at)
    {
        var c = Rifles(w, at);
        w.SetCompanyTactic(1, new[] { c }, t);
        w.Step();
        return c;
    }

    // ---------------------------------------------------------------- 자료 해시

    static void TestDataHash()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "data");
        string bal = File.ReadAllText(Path.Combine(dir, "balance.json"));
        string fo = File.Exists(Path.Combine(dir, "formations.json")) ? File.ReadAllText(Path.Combine(dir, "formations.json")) : "";
        string ta = File.Exists(Path.Combine(dir, "tactics.json")) ? File.ReadAllText(Path.Combine(dir, "tactics.json")) : "";
        string h = Defs.HashOf(bal, fo, ta);
        bool same = defs.Hash == h;
        bool tac = Defs.HashOf(bal, fo, ta + " ") != h && Defs.HashOf(bal, fo + " ", ta) != h && Defs.HashOf(bal + " ", fo, ta) != h;
        bool bound = Defs.HashOf("ab", "c", "") != Defs.HashOf("a", "bc", "");
        check(same && tac && bound, $"Defs.Hash = balance + formations + tactics ({defs.Hash}): 현재 값 일치 {same}, 대형 · 틀 자료만 바뀌어도 해시 바뀜 {tac}, 파일 경계 구분 {bound}");
    }

    // ---------------------------------------------------------------- 1단계 겹침 수정 (전선 폭 몫)

    static void TestFrontage()
    {
        var tr = defs.Tactics;
        foreach (var (rifles, mgs) in new[] { (36, 4), (87, 12) })
        {
            var w = Bare(200);
            var c = Rifles(w, new Vector2(100, 150), rifles, mgs);
            float worst = 0; string worstRole = ""; float spanMax = 0, Fmax = 0, spanExcess = 0;
            foreach (var t in new[] { CompanyTactic.FireManoeuvre, CompanyTactic.FlankLeft, CompanyTactic.Assault })
            {
                w.SetCompanyTactic(1, new[] { c }, t);
                w.Step();
                var units = c.Members.ToList();
                float s = OrgLayout.SpacingFor(units);
                var lay = CompanyLayout.Build(c, units, c.Form, s, 0, defs.Formations, tr);
                int n = units.Count;
                float F = MathF.Max(8f, (n + 1) / 2 * s), D = tr.DepthUnit;
                var T = tr.Template(t, out bool mir)!;
                // 역할별 슬롯 무게중심 vs 틀 위치 (전체 평균 차이를 뺀 뒤)
                var byRole = new Dictionary<UnitRole, (Vector2 Act, Vector2 Exp, int N)>();
                for (int i = 0; i < n; i++)
                {
                    var r = units[i].Role == UnitRole.None ? UnitRole.Rifle : units[i].Role;
                    var slots = Enumerable.Range(0, lay.Local.Length).Where(k => lay.SlotPool[k] == lay.UnitPool[i]).Select(k => lay.Local[k]).ToList();
                    if (!byRole.ContainsKey(r))
                    {
                        T.Groups.TryGetValue(TacticRules.KeyOf(r), out var gd);
                        float gx = (gd?.X ?? 0) * (mir ? -1 : 1);
                        byRole[r] = (Vector2.Zero, new Vector2(gx * F, (gd?.Y ?? -1.5f) * D), 0);
                    }
                }
                foreach (var r in byRole.Keys.ToList())
                {
                    var idx = Enumerable.Range(0, n).Where(i => (units[i].Role == UnitRole.None ? UnitRole.Rifle : units[i].Role) == r).ToList();
                    var pools = idx.Select(i => lay.UnitPool[i]).Distinct().ToHashSet();
                    var ps = Enumerable.Range(0, lay.Local.Length).Where(k => pools.Contains(lay.SlotPool[k])).Select(k => lay.Local[k]).ToList();
                    byRole[r] = (Mean(ps), byRole[r].Exp, ps.Count);
                }
                Vector2 off = Vector2.Zero; int tot = 0;
                foreach (var v in byRole.Values) { off += (v.Act - v.Exp) * v.N; tot += v.N; }
                off /= Math.Max(1, tot);
                foreach (var (r, v) in byRole)
                {
                    float dev = Vector2.Distance(v.Act - off, v.Exp) / F;
                    if (dev > worst) { worst = dev; worstRole = $"{t}/{r}"; }
                }
                float span = lay.Local.Max(p => p.X) - lay.Local.Min(p => p.X);
                float tspan = byRole.Values.Max(v => v.Exp.X) - byRole.Values.Min(v => v.Exp.X);   // 틀 자체의 역할군 x 범위
                spanMax = MathF.Max(spanMax, span / F); Fmax = F;
                spanExcess = MathF.Max(spanExcess, (span - tspan) / F);
            }
            check(worst <= 0.22f && spanExcess <= 0.8f, $"역할군 전선 폭 몫 ({c.Members.Count}명, 전선 폭 {Fmax:0}칸): 틀 위치와 역할군 무게중심 차 최대 {worst:0.00}×폭 ({worstRole}) ≤ 0.22 (1단계 100명 = 화력조 19칸 ≈ 0.36), 중대 폭 {spanMax:0.00}×폭 (틀 x 범위 + {spanExcess:0.00} ≤ 0.8)");
        }
        // 종심 방어: 기관총 = 양 측면 두 덩어리
        {
            var w = Bare(200);
            var c = Rifles(w, new Vector2(100, 150), 36, 4);
            w.SetCompanyTactic(1, new[] { c }, CompanyTactic.DefenceDepth);
            w.Step();
            var units = c.Members.ToList();
            float s = OrgLayout.SpacingFor(units);
            var lay = CompanyLayout.Build(c, units, c.Form, s, 0, defs.Formations, tr);
            var fireX = Enumerable.Range(0, units.Count).Where(i => units[i].Role == UnitRole.Fire)
                .SelectMany(i => Enumerable.Range(0, lay.Local.Length).Where(k => lay.SlotPool[k] == lay.UnitPool[i]).Select(k => lay.Local[k].X)).Distinct().ToList();
            var rifX = Enumerable.Range(0, units.Count).Where(i => units[i].Role == UnitRole.Rifle)
                .SelectMany(i => Enumerable.Range(0, lay.Local.Length).Where(k => lay.SlotPool[k] == lay.UnitPool[i]).Select(k => lay.Local[k].X)).ToList();
            float mid = rifX.Count > 0 ? rifX.Average() : 0;
            bool both = fireX.Any(x => x < mid - 4) && fireX.Any(x => x > mid + 4) && !fireX.Any(x => MathF.Abs(x - mid) < 2);
            check(both, $"종심 방어: 화력조 (기관총) 를 좌우 측면 두 덩어리로 (x {fireX.Min():0.0} … {fireX.Max():0.0}, 사격조 중심 {mid:0.0})");
        }
    }

    // ---------------------------------------------------------------- 화력조 사수 · 기관총 거치 · 사격조 교대 약진

    static void TestFireHoldAndBound()
    {
        var w = Bare();
        var c = Placed(w, CompanyTactic.FireManoeuvre, new Vector2(80, 130));
        var foe = Foes(w, 16, new Vector2(80, 92));
        w.AttackMove(1, c.Members, new Vector2(80, 70), new FormSpec(0, 0, North));
        long t0 = w.Tick;
        bool engaged = false, oneBounding = false, mgDeployed = false, fireHold = false;
        // 화력 우세 전 (적 제압 낮음): 약진 · 우회 없이 중대 명령대로 사격 (2026-10-19 측정 — 대등한 개활지 사격전의 약진 · 우회는 손해)
        for (int i = 0; i < 900 && c.DrillPhase != DrillPhase.Engage; i++) { foreach (var f in foe) f.Suppression = 0; w.Step(); }
        for (int i = 0; i < 420; i++) { foreach (var f in foe) f.Suppression = 0; w.Step(); }   // N25: 근접 탄착 제압으로 적 제압이 빨리 오르므로 '우세 전' 을 고정
        bool patientOk = c.DrillPhase == DrillPhase.Engage && w.Stats.DrillBounds == 0 && c.Drill.Man == 0;
        check(patientOk, $"화력 우세 전 (적 제압 {foe.Where(f => f.Alive).Select(f => f.SuppressionRatio).DefaultIfEmpty(0).Average():0.00} < {defs.Tactics.Behaviour.SuperioritySupp}): 약진 · 기동조 우회 없음 {patientOk}");
        var boundSquads = new HashSet<int>();
        float rif0 = 0, rifMin = float.MaxValue;
        for (int i = 0; i < 1500; i++)
        {
            Pin(foe);
            w.Step();
            if (!engaged && c.DrillPhase == DrillPhase.Engage) { engaged = true; rif0 = Mean(Role(c, UnitRole.Rifle).Select(u => u.Pos)).Y; }
            if (!engaged) continue;
            var fire = Role(c, UnitRole.Fire);
            fireHold |= fire.Count > 0 && fire.Count(u => u.Order == UnitOrder.Hold) * 10 >= fire.Count * 8;
            mgDeployed |= fire.Any(u => u.Def.Weapon == "lmg" && u.Deploy == DeployPhase.Deployed);
            if (c.Drill.BoundSquad != 0)
            {
                boundSquads.Add(c.Drill.BoundSquad);
                var rif = Role(c, UnitRole.Rifle);
                var movingSquads = rif.Where(u => u.Order == UnitOrder.Move && u.Group != null).Select(u => u.Squad?.Id ?? 0).Distinct().ToList();
                var holdOthers = rif.Where(u => u.Squad?.Id != c.Drill.BoundSquad).All(u => u.Order != UnitOrder.Move || u.Charging);
                if (movingSquads.Count == 1 && movingSquads[0] == c.Drill.BoundSquad && holdOthers) oneBounding = true;
            }
            var rc = Role(c, UnitRole.Rifle);
            if (rc.Count > 0) rifMin = MathF.Min(rifMin, Mean(rc.Select(u => u.Pos)).Y);
            if (foe.All(f => !f.Alive)) break;
        }
        check(engaged && fireHold && mgDeployed, $"화력-기동 접촉 → 행동: 국면 Engage {engaged}, 화력조 사수 {fireHold}, 기관총 거치 {mgDeployed}");
        check(oneBounding && boundSquads.Count >= 2 && rifMin < rif0 - 3, $"사격조 교대 약진: 한 분대만 이동 · 나머지 사격 {oneBounding}, 약진한 분대 {boundSquads.Count}개 (≥ 2), 사격조 전진 {rif0 - rifMin:0.0}칸, 약진 {w.Stats.DrillBounds}회");
    }

    // ---------------------------------------------------------------- 기동조: 화력-기동 = 오른쪽 비스듬히, 측면 우회 = 옆

    static (Vector2 Rel, bool Moved) ManGoal(CompanyTactic t)
    {
        var w = Bare();
        var c = Placed(w, t, new Vector2(80, 130));
        var foe = Foes(w, 16, new Vector2(80, 92));
        w.AttackMove(1, c.Members, new Vector2(80, 70), new FormSpec(0, 0, North));
        for (int i = 0; i < 900 && c.Drill.Man == 0; i++) { Pin(foe); w.Step(); }
        var d = c.Drill;
        // 적 기준 (북쪽 진행: 오른쪽 = +X, 우리 쪽 = +Y)
        Vector2 rel = d.ManGoal - d.Enemy;
        var man = Role(c, UnitRole.Manoeuvre);
        Vector2 m0 = Mean(man.Select(u => u.Pos));
        PinSteps(w, foe, 360);
        Vector2 m1 = Mean(Role(c, UnitRole.Manoeuvre).Select(u => u.Pos));
        bool moved = Vector2.Distance(m0, m1) > 4f && (Role(c, UnitRole.Manoeuvre).Any(u => u.QuickStep) || c.Drill.Man >= 2);
        return (rel, moved);
    }

    static void TestManoeuvreFlank()
    {
        {
            // 개활지 (풀밭) 우회는 적이 크게 제압됐을 때만 (manoeuvreOpenSupp): 화력 우세 (0.6) 만으로는 우회하지 않음
            var w = Bare();
            var c = Placed(w, CompanyTactic.FlankLeft, new Vector2(80, 130));
            var foe = Foes(w, 16, new Vector2(80, 92));
            w.AttackMove(1, c.Members, new Vector2(80, 70), new FormSpec(0, 0, North));
            for (int i = 0; i < 1200; i++) { foreach (var f in foe) if (f.Alive) f.Suppression = (short)(defs.CombatRules.SuppressMax * 0.6f); w.Step(); }
            check(c.DrillPhase == DrillPhase.Engage && c.Drill.Man == 0 && w.Stats.DrillManoeuvres == 0, $"개활지 우회 제한: 적 제압 0.6 (< {defs.Tactics.Behaviour.ManoeuvreOpenSupp}) 풀밭에서는 기동조가 우회하지 않음 (기동 {w.Stats.DrillManoeuvres}회)");
        }
        var (fm, fmMoved) = ManGoal(CompanyTactic.FireManoeuvre);
        var (fl, flMoved) = ManGoal(CompanyTactic.FlankLeft);
        var (fr, frMoved) = ManGoal(CompanyTactic.FlankRight);
        bool fmOk = fm.X > 3 && fm.Y > 3;            // 오른쪽 앞 (적 기준 우리 쪽 비스듬히)
        bool flOk = fl.X < -7 && MathF.Abs(fl.Y) < MathF.Abs(fl.X);   // 왼쪽 옆
        bool frOk = fr.X > 7 && MathF.Abs(fr.Y) < MathF.Abs(fr.X);
        check(fmOk && flOk && frOk && fmMoved && flMoved && frMoved,
            $"기동조 목표 (적 기준, +X 오른쪽 · +Y 우리 쪽): 화력-기동 ({fm.X:0.0},{fm.Y:0.0}) 비스듬히 {fmOk}, 좌측 우회 ({fl.X:0.0},{fl.Y:0.0}) {flOk}, 우측 우회 ({fr.X:0.0},{fr.Y:0.0}) {frOk}, 엄폐 · 속보 이동 {fmMoved && flMoved && frMoved}");
    }

    // ---------------------------------------------------------------- 돌격 (가까움 · 제압) + 연신

    static void TestChargeAndLift()
    {
        var w = Bare();
        var c = Placed(w, CompanyTactic.Assault, new Vector2(80, 120));
        var foe = Foes(w, 8, new Vector2(80, 96));
        w.AttackMove(1, c.Members, new Vector2(80, 70), new FormSpec(0, 0, North));
        bool charged = false, lifted = false, liftOk = true;
        for (int i = 0; i < 2400; i++)
        {
            w.Step();
            charged |= Role(c, UnitRole.Manoeuvre).Any(u => u.Charging);
            if (w.Tick < c.Drill.LiftUntil)
            {
                lifted = true;
                float r = defs.Tactics.Behaviour.LiftRadius;
                foreach (var u in c.Members)
                    if (u.Role is UnitRole.Fire or UnitRole.Support && !u.Charging && u.Target != null && Vector2.Distance(u.Target.Pos, c.Drill.LiftPos) < r - 0.01f) liftOk = false;
            }
            if (foe.All(f => !f.Alive)) break;
        }
        check(charged && w.Stats.DrillCharges >= 1, $"돌격 틀: 기동조가 앞장서 가까워지면 착검 돌격 {charged} (돌격 판단 {w.Stats.DrillCharges}회)");
        check(lifted && liftOk && w.Stats.DrillLifts >= 1, $"연신: 기동조가 적에게 다가가면 화력조 · 지원은 그 둘레를 쏘지 않음 (연신 {w.Stats.DrillLifts}회, 위반 없음 {liftOk})");

        // 화력-기동: 적이 제압되면 (평균 ≥ chargeSuppression) chargeMaxDist 안에서 돌격
        var w2 = Bare();
        for (int x = 70; x <= 90; x++) { w2.Map.Set(x, 97, TerrainType.Trench); w2.Map.Set(x, 98, TerrainType.Trench); }   // 참호 속 적 (사격만으로는 오래 버팀)
        var c2 = Placed(w2, CompanyTactic.FireManoeuvre, new Vector2(80, 124));
        var foe2 = Foes(w2, 12, new Vector2(80, 97));
        w2.AttackMove(1, c2.Members, new Vector2(80, 70), new FormSpec(0, 0, North));
        bool charge2 = false;
        for (int i = 0; i < 3000 && !charge2; i++)
        {
            Pin(foe2);
            w2.Step();
            charge2 = Role(c2, UnitRole.Manoeuvre).Any(u => u.Charging);
            if (foe2.All(f => !f.Alive)) break;
        }
        float supp2 = foe2.Where(f => f.Alive).Select(f => f.SuppressionRatio).DefaultIfEmpty(0).Average();
        float dm = Vector2.Distance(Mean(Role(c2, UnitRole.Manoeuvre).Select(u => u.Pos)), c2.Drill.Enemy);
        check(charge2, $"화력-기동: 기동조가 자리에서 제압된 · 가까운 적에게 돌격 {charge2} (판단 {w2.Stats.DrillCharges}회, 적 생존 {foe2.Count(f => f.Alive)}, 적 제압 {supp2:0.00}, 기동조 상태 {c2.Drill.Man} 거리 {dm:0.0}, 목표까지 {Vector2.Distance(Mean(Role(c2, UnitRole.Manoeuvre).Select(u => u.Pos)), c2.Drill.ManGoal):0.0}, 적 {c2.Drill.Enemy} 목표 {c2.Drill.ManGoal} 기동조 {Mean(Role(c2, UnitRole.Manoeuvre).Select(u => u.Pos))} 국면 {c2.DrillPhase}, {w2.Tick}틱)");
    }

    // ---------------------------------------------------------------- 지원 = 위협 우선

    static void TestSupportThreat()
    {
        var w = Bare();
        var us = Spawn(w, "infantry", 12, new Vector2(80, 120), 12);
        var at = Spawn(w, "heavy_at_rifleman", 3, new Vector2(80, 124), 3);
        us.AddRange(at);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(80, 126)));
        w.FormCompany(1, us); w.Step();
        var c = us[0].Company!;
        w.SetCompanyTactic(1, new[] { c }, CompanyTactic.FireManoeuvre);
        w.Step();
        var near = Foes(w, 6, new Vector2(80, 107));         // 가까운 소총수
        var mg = Foes(w, 1, new Vector2(84, 105), "heavy_machinegunner");   // 조금 먼 기관총 (사거리 안)
        w.Hold(1, c.Members);
        bool onMg = false;
        for (int i = 0; i < 400 && !onMg; i++)
        {
            w.Step();
            onMg = at.Any(u => u.Alive && u.Target == mg[0]);
        }
        bool roles = at.All(u => u.Role == UnitRole.Support);
        check(roles && onMg, $"지원 (대전차): 가까운 소총수보다 위협 (기관총) 을 우선 표적 {onMg}");
    }

    // ---------------------------------------------------------------- 종심 방어: 참호 · 예비 역습

    static void TestDefenceDepth()
    {
        var w = Bare();
        // 방어 위치 앞 (북쪽) 참호선
        for (int x = 66; x <= 94; x++) w.Map.Set(x, 96, TerrainType.Trench);
        var c = Placed(w, CompanyTactic.DefenceDepth, new Vector2(80, 130));
        w.AttackMove(1, c.Members, new Vector2(80, 100), new FormSpec(0, 0, North));
        Steps(w, 1500);
        bool defend = c.DrillPhase == DrillPhase.Defend;
        int inTrench = Role(c, UnitRole.Rifle).Count(u => u.InTrench || u.TrenchSpot >= 0);
        int rif = Role(c, UnitRole.Rifle).Count;
        check(defend && inTrench * 2 >= rif, $"종심 방어 도착 → 국면 Defend {defend}, 앞 분대 (사격조) 참호 점유 {inTrench}/{rif} (참호 이동 {w.Stats.DrillTrench}회)");
        // 적이 앞 분대 둘레로 밀고 들어오면 예비 (기동조) 역습
        var front = Mean(Role(c, UnitRole.Rifle).Select(u => u.Pos));
        var foe = Spawn(w, "infantry", 6, front + new Vector2(0, -4), 6, 2);
        w.Hold(2, foe);
        bool counter = false;
        for (int i = 0; i < 600 && !counter; i++) { w.Step(); counter = w.Stats.DrillCounters > 0 && Role(c, UnitRole.Manoeuvre).Any(u => u.Charging); }
        check(counter, $"종심 방어: 전선이 밀리면 예비 역습 (착검 돌격) {counter}");
    }

    // ---------------------------------------------------------------- 포병 대형 → 화력-기동 전개 (지휘 없으면 횡대)

    static void TestArtilleryDeploy()
    {
        foreach (bool officer in new[] { true, false })
        {
            var w = Bare();
            var c = Rifles(w, new Vector2(80, 130), 36, 4, officer ? 1 : 0);
            w.SetCompanyTactic(1, new[] { c }, CompanyTactic.Artillery);
            w.Step();
            if (!officer) { w.SetCompanyTactic(1, new[] { c }, CompanyTactic.Artillery); w.Step(); }
            w.AttackMove(1, c.Members, new Vector2(80, 50), new FormSpec(0, 0, North));
            Steps(w, 120);
            bool marching = c.Marching && (c.ActiveTactic == CompanyTactic.Artillery || !officer);
            var foe = Foes(w, 6, (c.FlagPos ?? new Vector2(80, 110)) + new Vector2(0, -18));
            long t0 = w.Tick;
            for (int i = 0; i < 600 && w.Stats.ArtilleryDeploys == 0 && (officer || c.Members.All(u => u.Target == null)); i++) w.Step();
            if (officer)
            {
                bool fm = c.Tactic == CompanyTactic.FireManoeuvre && c.ActiveTactic == CompanyTactic.FireManoeuvre && w.Stats.ArtilleryDeploys == 1;
                check(marching && fm, $"포병 대형 접촉 → 화력-기동 틀로 전개 (장교 지휘): {fm} ({w.Tick - t0}틱)");
            }
            else
            {
                // 지휘 없음 (장교 · 부사관 없음): 포병 대형도 실행 안 됨 → 기본 대형
                bool none = c.ActiveTactic == CompanyTactic.None && c.Tactic == CompanyTactic.Artillery && w.CommandLevelOf(c) == CommandLevel.None;
                check(none, $"지휘 없는 중대: 포병 대형을 골라도 실행 틀 없음 (기본 대형) {none}");
            }
        }
    }

    // ---------------------------------------------------------------- 지휘관: 인계 중 멈춤 · 부사관 = 화력-기동만 · 지휘 없음 = 틀 없음

    static void TestCommanderGating()
    {
        var w = Bare();
        var c = Rifles(w, new Vector2(80, 120));
        var nco = c.Members.Where(u => !u.IsOfficer && u.Squad?.Leader == u).OrderBy(u => u.Id).First();
        nco.GrantKills(defs.CombatRules.RankKills[defs.CombatRules.NcoRank - 1]);
        w.SetCompanyTactic(1, new[] { c }, CompanyTactic.FlankLeft);
        w.Step();
        bool officerAll = c.ActiveTactic == CompanyTactic.FlankLeft;
        // 장교 전사 → 부사관 인계 중: 판단 멈춤 (실행 틀 그대로)
        long paused0 = w.Stats.DrillPaused;
        foreach (var o in c.Members.Where(u => u.IsOfficer).ToList()) o.Hp = 0;
        w.Step();
        bool hand = w.InHandover(c);
        Steps(w, 120);
        bool keep = c.ActiveTactic == CompanyTactic.FlankLeft && w.Stats.DrillPaused > paused0;
        while (w.InHandover(c)) w.Step();
        Steps(w, 61);
        bool ncoFm = w.CommandLevelOf(c) == CommandLevel.Nco && c.ActiveTactic == CompanyTactic.FireManoeuvre && c.Tactic == CompanyTactic.FlankLeft;
        // 부사관 지휘 중 돌격 틀 명령 → 거부 알림 + 화력-기동
        w.Notices.Clear();
        w.SetCompanyTactic(1, new[] { c }, CompanyTactic.Assault);
        w.Step();
        bool refused = c.ActiveTactic == CompanyTactic.FireManoeuvre && w.Notices.Any(n => n.Result == CommandResult.NotAllowed);
        // 부사관도 전사 → 지휘 없음: 틀 없음
        nco.Hp = 0;
        Steps(w, 61);
        bool none = w.CommandLevelOf(c) == CommandLevel.None && c.ActiveTactic == CompanyTactic.None;
        check(officerAll && hand && keep && ncoFm && refused && none,
            $"지휘관 판단: 장교 = 측면 우회 {officerAll}, 인계 중 전환 멈춤 {hand && keep}, 부사관 대리 = 화력-기동 (명령은 기억) {ncoFm}, 부사관에게 돌격 틀 거부 {refused}, 지휘 없음 = 틀 없음 {none}");
    }

    // ---------------------------------------------------------------- 판단 간격 = DecideInterval (경험치)

    static void TestDecideTiming()
    {
        long Count(int xp)
        {
            var w = Bare();
            var c = Rifles(w, new Vector2(80, 120));
            foreach (var o in c.Members.Where(u => u.IsOfficer)) o.OfficerXp = xp;
            w.SetCompanyTactic(1, new[] { c }, CompanyTactic.FireManoeuvre);
            w.Step();
            long d0 = w.Stats.DrillDecisions;
            Steps(w, 1200);
            return w.Stats.DrillDecisions - d0;
        }
        long lo = Count(0), hi = Count(100000);
        int iv = defs.Tactics.Behaviour.DecideTicks;
        int ivHi = (int)MathF.Round(iv * MathF.Max(0.5f, 1f - defs.Officers.XpDecidePerLevel * defs.Officers.XpLevels.Length));
        bool ok = MathF.Abs(lo - 1200f / iv) <= 1 && MathF.Abs(hi - 1200f / ivHi) <= 1 && hi > lo;
        check(ok, $"판단 간격: 경험치 0 = {iv}틱 ({lo}회/1200틱), 최고 단계 = {ivHi}틱 ({hi}회) — 지휘관 경험치로 더 자주");
    }

    // ---------------------------------------------------------------- 일반 이동 (후퇴) 중에는 행동 안 함 · 접촉이 끊기면 정렬

    static void TestPassiveAndDisengage()
    {
        var w = Bare();
        var c = Placed(w, CompanyTactic.FireManoeuvre, new Vector2(80, 120));
        var foe = Foes(w, 10, new Vector2(80, 96));
        w.AttackMove(1, c.Members, new Vector2(80, 70), new FormSpec(0, 0, North));
        for (int i = 0; i < 900 && c.DrillPhase != DrillPhase.Engage; i++) w.Step();
        bool engaged = c.DrillPhase == DrillPhase.Engage;
        // 후퇴 (일반 이동): 행동 멈춤, 도착할 때까지 다시 안 함
        w.Move(1, c.Members, new Vector2(80, 150), new FormSpec(0, 0, new Vector2(0, 1)));
        bool passive = true;
        for (int i = 0; i < 240; i++) { w.Step(); if (c.Marching && c.DrillPhase != DrillPhase.Idle) passive = false; }
        // 적이 사라지면 disengageSec 뒤 Idle
        foreach (var f in foe) f.Hp = 0;
        w.AttackMove(1, c.Members, new Vector2(80, 110), new FormSpec(0, 0, North));
        var foe2 = Foes(w, 4, new Vector2(80, 88));
        for (int i = 0; i < 1500 && c.DrillPhase != DrillPhase.Engage; i++) w.Step();
        bool again = c.DrillPhase == DrillPhase.Engage;
        foreach (var f in foe2) f.Hp = 0;
        int ticks = 0;
        for (; ticks < 1500 && c.DrillPhase != DrillPhase.Idle; ticks++) w.Step();
        float dis = defs.Tactics.Behaviour.DisengageSec;
        bool ended = c.DrillPhase == DrillPhase.Idle && ticks >= dis * 60 - 60;
        check(engaged && passive && again && ended, $"전환: 접촉 → Engage {engaged}, 후퇴 (일반 이동) 중 행동 없음 {passive}, 공격 이동 재접촉 → Engage {again}, 접촉 끊김 {ticks}틱 (≈ {dis}초) 뒤 Idle {ended}");
    }

    // ---------------------------------------------------------------- 저장 · 결정성 (행동 중)

    static World Scenario()
    {
        var w = Bare();
        var c = Placed(w, CompanyTactic.FlankRight, new Vector2(80, 120));
        var foe = Foes(w, 12, new Vector2(80, 94));
        w.AttackMove(1, c.Members, new Vector2(80, 70), new FormSpec(0, 0, North));
        PinSteps(w, foe, 700);
        return w;
    }

    static void TestSaveMidDrill()
    {
        var x = Scenario();
        var y0 = Scenario();
        check(x.ComputeChecksum() == y0.ComputeChecksum(), "행동 중 같은 명령 → 같은 체크섬");
        var c = x.Companies[0];
        bool mid = c.DrillPhase != DrillPhase.Idle && c.Drill.Man != 0;
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        var yc = y.Companies.First(k => k.Id == c.Id);
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool state = yc.ActiveTactic == c.ActiveTactic && yc.DrillPhase == c.DrillPhase && yc.Drill.Man == c.Drill.Man && yc.Drill.ManGoal == c.Drill.ManGoal && yc.Drill.ContactTick == c.Drill.ContactTick;
        check(mid && same && state && x.ComputeChecksum() == y.ComputeChecksum(), $"행동 중 저장 → 불러오기 (국면 {c.DrillPhase}, 기동조 {c.Drill.Man}): 바이트 동일 {same}, 행동 상태 복원 {state}, 체크섬 동일");
        Steps(x, 900); Steps(y, 900);
        check(x.ComputeChecksum() == y.ComputeChecksum(), "불러온 월드를 900틱 이어 돌려도 같은 체크섬 (행동 · 돌격 · 연신 포함)");
        var z = Scenario();
        z.Companies[0].Drill.Man ^= 1;
        check(z.ComputeChecksum() != y0.ComputeChecksum(), "체크섬에 행동 상태 포함");
    }

    // ---------------------------------------------------------------- 중화기는 보병 앞으로 나가지 않는다 (사용자 규칙 2026-10-19)

    /// <summary>매 틱: 중화기가 진행 방향 (북쪽) 으로 나아가면서 보병 앞선 + 여유를 넘은 횟수 · 최대 앞섬.</summary>
    static (int Violations, float MaxLead) Track(World w, List<Unit> inf, List<Unit> heavy, int ticks)
    {
        int bad = 0; float maxLead = float.MinValue;
        var prev = heavy.ToDictionary(u => u.Id, u => u.Pos);
        for (int i = 0; i < ticks; i++)
        {
            float front = inf.Where(u => u.Alive).Select(u => -u.Pos.Y).DefaultIfEmpty(float.MinValue).Max();
            w.Step();
            foreach (var h in heavy.Where(u => u.Alive))
            {
                float lead = -h.Pos.Y - front;
                bool fwd = -h.Pos.Y > -prev[h.Id].Y + 0.02f;   // 걷기 (≈0.05칸/틱) — 겹침 해소로 밀린 작은 이동 (≤0.004) 은 제외
                if (fwd && lead > World.HeavyLeadTol + 0.06f) bad++;
                prev[h.Id] = h.Pos;
            }
            float f2 = inf.Where(u => u.Alive).Select(u => -u.Pos.Y).Max();
            foreach (var h in heavy.Where(u => u.Alive)) maxLead = MathF.Max(maxLead, -h.Pos.Y - f2);
        }
        return (bad, maxLead);
    }

    static void TestHeavyPacing()
    {
        // 틀 배치: 화력조 · 지원 (중화기 역할) 은 사격조 줄과 같거나 뒤
        var tr = defs.Tactics;
        bool tpl = true;
        foreach (var (k, t) in tr.Templates)
        {
            if (t.Mirror != null || !t.Groups.TryGetValue("rifle", out var rg)) continue;
            foreach (var key in new[] { "fire", "support" })
                if (t.Groups.TryGetValue(key, out var g) && g.Y > rg.Y + 1e-4f) tpl = false;
        }
        check(tpl, "틀 배치: 화력조 · 지원 (중화기) 위치는 모든 틀에서 사격조 줄과 같거나 뒤");

        // 섞인 중대 공격 이동 (적 없음): 중화기를 앞줄에 세워 두고 시작해도 보병 앞선을 넘어 전진하지 않음, 도착 슬롯도 보병 뒤
        var w = Bare();
        var inf = Spawn(w, "infantry", 30, new Vector2(80, 130), 10);
        var heavy = Spawn(w, "heavy_machinegunner", 4, new Vector2(80, 124), 4).Concat(Spawn(w, "heavy_at_rifleman", 2, new Vector2(84, 124), 2)).ToList();
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(80, 134));
        w.FormCompany(1, inf.Concat(heavy).Append(off).ToList()); w.Step();
        var c = inf[0].Company!;
        w.AttackMove(1, c.Members, new Vector2(80, 60), new FormSpec(0, 0, North));
        var (bad, lead) = Track(w, inf, heavy, 1800);
        float endLead = heavy.Max(h => -h.Pos.Y) - inf.Max(i => -i.Pos.Y);
        check(bad == 0 && endLead <= World.HeavyLeadTol + 0.1f, $"섞인 중대 공격 이동: 중화기가 보병 앞선 + {World.HeavyLeadTol}칸을 넘어 전진한 틱 {bad} (0), 도착 뒤 앞섬 {endLead:0.00}칸, 보폭 기다림 {w.Stats.HeavyHeld}회");

        // 중대 밖 섞인 묶음 (일반 이동) · 대대의 중화기 전용 중대
        var w2 = Bare();
        var inf2 = Spawn(w2, "infantry", 10, new Vector2(60, 130), 10);
        var hv2 = Spawn(w2, "heavy_machinegunner", 3, new Vector2(60, 126), 3);
        w2.Move(1, inf2.Concat(hv2), new Vector2(60, 70), new FormSpec(0, 0, North));
        var (bad2, _) = Track(w2, inf2, hv2, 1500);
        var w3 = Bare(200);
        var i3 = Spawn(w3, "infantry", 24, new Vector2(80, 170), 12); i3.Add(w3.SpawnUnit(defs.Units["officer"], 1, new Vector2(80, 174)));
        var h3 = Spawn(w3, "heavy_machinegunner", 6, new Vector2(100, 164), 6); h3.Add(w3.SpawnUnit(defs.Units["officer"], 1, new Vector2(100, 168)));
        w3.FormCompany(1, i3); w3.Step(); w3.FormCompany(1, h3); w3.Step();
        w3.FormBattalion(1, new[] { i3[0].Company!, h3[0].Company! });
        w3.Step();
        var bat = i3[0].Company!.Battalion ?? h3[0].Company!.Battalion;
        bool sameBat = bat != null && i3[0].Company!.Battalion == h3[0].Company!.Battalion;
        w3.AttackMove(1, i3.Concat(h3), new Vector2(90, 80), new FormSpec(0, 0, North));
        var (bad3, _) = Track(w3, i3.Where(u => !u.IsOfficer).ToList(), h3.Where(u => u.Def.Heavy).ToList(), 1800);
        check(bad2 == 0 && (!sameBat || bad3 == 0), $"중대 밖 섞인 묶음 이동: 앞선 넘은 전진 {bad2} (0), 대대 안 기관총 중대 (대대 편성 {sameBat}): {bad3} (0)");

        // 예외: 보병이 사수 중이면 앞에 있는 중화기도 옆으로 다시 거치할 수 있다
        var w4 = Bare();
        var i4 = Spawn(w4, "infantry", 12, new Vector2(80, 120), 12);
        var h4 = Spawn(w4, "heavy_machinegunner", 2, new Vector2(80, 110), 2);
        w4.FormCompany(1, i4.Concat(h4).ToList()); w4.Step();
        w4.Hold(1, i4);
        var x0 = h4[0].Pos.X;
        w4.Move(1, h4, new Vector2(95, 110));
        Steps(w4, 600);
        check(h4[0].Pos.X > x0 + 8, $"예외: 보병 사수 중 앞쪽 중화기의 옆 이동은 허용 ({h4[0].Pos.X - x0:0.0}칸)");
    }
}
