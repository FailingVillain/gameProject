using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// pursuit (2026-10-19 사용자 승인): 후퇴 = 속보 + 후퇴 표시, 보병은 후퇴하는 적에게 돌격하지 않고 사격만 (기병 추격은 cavalryChargeFleeing, 기본 true),
/// 전술 AI 사격 추격 한계, 기병 측면 · 후방 우선 · 측면 경유 · 소규모 기병 보류 · 기관총 앞 교착 방지. 창 없이. KD_SELFTEST_ONLY=pursuit 로 단독 실행.
/// </summary>
public static class PursuitSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("pursuit", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[pursuit 추격 · 후퇴 속보 · 기병 측면]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestRetreatQuick();
            TestNoChargeFleeing();
            TestCavalryFlag();
            TestDrillChargeFleeing();
            TestPursuitLimit();
            TestAiRetreatQuick();
            TestCavalryPlan();
            TestSmallCavalry();
            TestMgStalemate();
        }
        catch (Exception e) { check(false, $"pursuit 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World Bare(int size = 120)
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

    static List<Unit> Spawn(World w, int n, Vector2 c, int faction, string def = "infantry", int cols = 6, float gap = 1.1f)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++) list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * gap, (i / cols) * gap)));
        return list;
    }

    static void Pin(IEnumerable<Unit> us, float r) { foreach (var u in us) if (u.Alive) u.Suppression = (short)(defs.CombatRules.SuppressMax * r); }

    static int Ticks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));

    static TacFaction St(World w, int f) => ((TacticalAi)w.Controllers.First(c => c.Faction == f).Ai).StateOf(f)!;

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var p = defs.Tactics.Pursuit; var c = defs.Tactics.Cavalry;
        check(p.NoChargeFleeing && p.CavalryChargeFleeing && p.Enabled && p.MaxDist > 0 && p.MaxSec > 0 && p.RegroupSec > 0 && p.RangeFrac is > 0 and <= 1,
            $"tactics.json pursuit: noChargeFleeing {p.NoChargeFleeing} · cavalryChargeFleeing {p.CavalryChargeFleeing} (기본 true, 사용자 2026-10-19) · rangeFrac {p.RangeFrac} · maxDist {p.MaxDist} · maxSec {p.MaxSec} · regroupSec {p.RegroupSec}");
        check(c.MinRiders > 1 && c.MinOdds > 0 && c.FlankScore > 0 && c.RearScore > 0 && c.FrontPenalty > 0 && c.StallSec > 0 && !c.Legacy,
            $"tactics.json cavalry: minRiders {c.MinRiders} · minOdds {c.MinOdds} · flank/rear {c.FlankScore}/{c.RearScore} · frontPenalty {c.FrontPenalty} · stallSec {c.StallSec} · legacy {c.Legacy}");
    }

    // ---------------------------------------------------------------- 후퇴 명령 = 속보 + 후퇴 표시 (저장 포함)

    static void TestRetreatQuick()
    {
        var w = Bare();
        var inf = Spawn(w, 6, new Vector2(60, 60), 1);
        var cav = Spawn(w, 3, new Vector2(60, 66), 1, "cavalry_default");
        var co = Spawn(w, 12, new Vector2(30, 60), 1);
        co.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(30, 64)));
        w.FormCompany(1, co);
        w.Step();
        w.Retreat(1, inf.Concat(cav), new Vector2(60, 100));
        w.Retreat(1, co, new Vector2(30, 100));
        w.Step(); w.Step();
        bool flags = inf.Concat(cav).Concat(co).All(u => u.Retreating && World.IsFleeing(u));
        bool quick = inf.All(u => u.QuickStep) && co.All(u => u.QuickStep) && cav.All(u => !u.QuickStep);
        check(flags && quick, $"후퇴 명령: 전원 후퇴 표시 {flags}, 보병 · 중대 (깃발) = 속보, 기병 = 보통 이동 {quick}");
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        bool saved = y.Units.Where(u => u.Owner == 1).All(u => w.Find(u.Id) is Unit o && u.Retreating == o.Retreating) && y.ComputeChecksum() == w.ComputeChecksum();
        check(saved, "후퇴 표시: 저장 · 불러오기 · 체크섬 일치");
        var h0 = w.ComputeChecksum(); inf[0].Retreating = !inf[0].Retreating; var h1 = w.ComputeChecksum(); inf[0].Retreating = !inf[0].Retreating;
        check(h0 != h1, "후퇴 표시: 체크섬에 들어감");
        for (int i = 0; i < Ticks(40); i++) w.Step();
        bool cleared = inf.Concat(cav).All(u => !u.Retreating && u.Order != UnitOrder.Move);
        check(cleared, $"후퇴 도착 = 후퇴 표시 해제 ({inf.Concat(cav).Count(u => u.Retreating)}명 남음)");
    }

    // ---------------------------------------------------------------- 보병: 후퇴하는 적에게 자동 돌격 없음 · 사격은 계속

    static (int Charges, int Shots) NearbyFoe(bool retreat, string attacker = "infantry")
    {
        var w = Bare();
        var a = Spawn(w, 10, new Vector2(60, 64), 1, attacker, cols: 10);
        a.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 65.2f)));   // 장교 자동 돌격도
        var b = Spawn(w, 10, new Vector2(60, 62.6f), 2, cols: 10);
        foreach (var u in a) u.BayonetFixed = true;   // T25 (D02 A): 착검 멈춤은 이 시험 대상이 아님 — 첫 틱에 시작된 돌격이 멈춤 동안 '돌격 중'으로 남아 후퇴 표시 뒤 횟수에 섞이는 것을 막는다
        w.Step();
        if (retreat) w.Retreat(2, b, new Vector2(60, 5)); else w.Move(2, b, new Vector2(60, 5));
        var ids = a.Select(u => u.Id).ToHashSet();
        int charges = 0, shots = 0;
        for (int i = 0; i < Ticks(12); i++)
        {
            w.Step();
            charges += a.Count(u => u.Alive && u.Charging);
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && ids.Contains(s.ShooterId)) shots++;
        }
        return (charges, shots);
    }

    static void TestNoChargeFleeing()
    {
        long inf0 = Defs.Current.Tactics.Pursuit.NoChargeFleeing ? 0 : -1;
        var (c1, s1) = NearbyFoe(retreat: true);
        var (c0, _) = NearbyFoe(retreat: false);
        check(c1 == 0 && s1 > 0, $"보병 · 장교: 후퇴하는 적 (1칸 안에서 출발) 에게 자동 돌격 없음 (돌격 기-틱 {c1}), 사격 계속 ({s1}발)");
        check(c0 > 0, $"대조: 같은 적이 보통 이동으로 물러나면 자동 돌격 (돌격 기-틱 {c0})");
        _ = inf0;
    }

    static void TestCavalryFlag()
    {
        var p = defs.Tactics.Pursuit;
        var (on, _) = NearbyFoe(retreat: true, attacker: "cavalry_default");
        bool keep = p.CavalryChargeFleeing;
        int off;
        try { p.CavalryChargeFleeing = false; (off, _) = NearbyFoe(retreat: true, attacker: "cavalry_default"); }
        finally { p.CavalryChargeFleeing = keep; }
        check(on > 0 && off == 0, $"기병 추격 (cavalryChargeFleeing): 기본 true = 후퇴하는 적에게 돌격 (기-틱 {on}), false = 보병과 같이 돌격 안 함 ({off})");
    }

    static void TestDrillChargeFleeing()
    {
        bool Try(bool retreat)
        {
            var w = Bare();
            var us = Spawn(w, 24, new Vector2(60, 66), 1, cols: 12);
            us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 70)));
            w.FormCompany(1, us);
            w.Step();
            var c = us[0].Company!;
            var foe = Spawn(w, 6, new Vector2(60, 62), 2);
            w.Hold(1, c.Members);
            if (retreat) w.Retreat(2, foe, new Vector2(60, 5)); else w.Hold(2, foe);
            w.Step(); w.Step();
            Pin(foe, 0.9f);
            c.Drill.PhaseTick = w.Tick;
            var men = c.Members.Where(u => !u.IsOfficer).ToList();
            Vector2 mc = new(men.Average(u => u.Pos.X), men.Average(u => u.Pos.Y));
            return w.TryDrillCharge(c, men, mc, 6f, 24f, closeOnly: true);
        }
        bool fleeing = Try(true), held = Try(false);
        check(!fleeing && held, $"기동조 돌격 (틀): 후퇴하는 적에게 안 함 ({fleeing}), 대조 = 멈춘 적에게는 돌격 ({held})");
    }

    // ---------------------------------------------------------------- 전술 AI 사격 추격: 한계 거리 · 시간에서 멈춤, 돌격 없음

    static void TestPursuitLimit()
    {
        var w = Bare(160);
        var a = Spawn(w, 40, new Vector2(80, 140), 1, cols: 20);
        a.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(80, 144)));
        var b = Spawn(w, 30, new Vector2(80, 112), 2, cols: 15);
        w.Controllers.Add((1, new TacticalAi()));
        w.Hold(2, b);
        var aIds = a.Select(u => u.Id).ToHashSet();
        float maxD = defs.Tactics.Pursuit.MaxDist * (defs.Weapon("rifle")?.Range ?? 16f);
        bool ordered = false; float worst = 0; int shots = 0; long fleeingKills0 = 0;
        var origins = new Dictionary<int, (Vector2 O, long T)>();
        for (int i = 0; i < Ticks(150); i++)
        {
            w.Step();
            if (!ordered && w.Time > 15) { w.Retreat(2, b.Where(u => u.Alive), new Vector2(80, 3)); ordered = true; fleeingKills0 = w.Stats.KillsFleeingFire; }
            if (ordered && i % 30 == 0) { var idle = b.Where(u => u.Alive && !u.Retreating).ToList(); if (idle.Count > 0 && w.Time < 80) w.Retreat(2, idle, new Vector2(80, 3)); }
            foreach (var sh in w.Shots) if (ordered && sh.Tick == w.Tick - 1 && aIds.Contains(sh.ShooterId)) shots++;
            if (w.Controllers.Count == 0 || ((TacticalAi)w.Controllers[0].Ai).StateOf(1) is not { Phase: 2 } s) continue;
            foreach (var g in s.Groups)
            {
                if (g.Kind != TacKind.Line) continue;
                if (g.PursuitTick != 0)
                {
                    if (!origins.TryGetValue(g.Id, out var o) || o.T != g.PursuitTick) origins[g.Id] = o = (g.PursuitOrigin, g.PursuitTick);
                    var mem = a.Where(u => u.Alive && u.Company?.Id == g.CompanyId).ToList();
                    if (mem.Count > 0) worst = MathF.Max(worst, o.O.Y - mem.Average(u => u.Pos.Y));   // 적 쪽 = −y
                }
            }
        }
        var st = St(w, 1);
        check(st.Pursuits > 0 && st.PursuitHalts > 0, $"사격 추격: 시작 {st.Pursuits}회 · 한계 정지 {st.PursuitHalts}회");
        check(worst <= maxD + 8f, $"사격 추격 한계: 추격 시작 위치에서 앞으로 최대 {worst:0.0}칸 (한계 {maxD:0} + 깃발 여유 8)");
        check(st.Charges == 0 && w.Stats.InfChargeFleeing == 0 && shots > 0, $"사격 추격: 돌격 {st.Charges} · 후퇴하는 적 보병 돌격 {w.Stats.InfChargeFleeing}, 사격 {shots}발 (후퇴 중 사살 {w.Stats.KillsFleeingFire - fleeingKills0})");
    }

    // ---------------------------------------------------------------- 후퇴 경로 = 속보 (전술 AI 실전)

    static void TestAiRetreatQuick()
    {
        var setup = new BattleSetup { SideA = "infantry:60,officer:2,cavalry_default:10", SideB = "infantry:110,officer:3,heavy_machinegunner:6", Map = "flat", Distance = 40, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 4);
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        w.RecordCommands = true;
        int samples = 0, slow = 0;
        for (int i = 0; i < Ticks(200) && w.Winner == 0; i++)
        {
            w.Step();
            foreach (var u in w.Units)
                if (u.Alive && u.Retreating && u.Order == UnitOrder.Move && u.Owner > 0) { samples++; if (u.CanQuickStep && !u.QuickStep) slow++; }
        }
        int retreats = w.CommandLog.Count(c => c.Command is RetreatCommand);
        int fallbacks = St(w, 1).Fallbacks + St(w, 2).Fallbacks;
        check(fallbacks > 0 && retreats > 0, $"전술 AI 후퇴 = 후퇴 명령 (후퇴 {fallbacks}회, 후퇴 명령 {retreats}개)");
        check(samples > 0 && slow == 0, $"후퇴 중 속보 가능한 병사는 모두 속보 (표본 {samples} 기-틱, 걷기 {slow})");
    }

    // ---------------------------------------------------------------- 기병 계획: 측면 우선 · 측면 경유 · 기관총

    static (World W, TacticalAi Ai, TacFaction S, TacGroup G) CavWorld()
    {
        var w = Bare();
        var ai = new TacticalAi();
        var s = new TacFaction { Faction = 1, RifleRange = defs.Weapon("rifle")?.Range ?? 16f, Dir = new Vector2(0, -1) };
        var g = new TacGroup { Id = 1, Kind = TacKind.Cavalry, Center = new Vector2(60, 95), TaskDir = new Vector2(0, -1), Alive = 24, Initial = 24 };
        return (w, ai, s, g);
    }

    static List<Unit> Block(World w, Vector2 c, float facing, float supp)
    {
        var b = Spawn(w, 10, c, 2, cols: 5);
        foreach (var u in b) u.Facing = facing;
        Pin(b, supp);
        return b;
    }

    static void TestCavalryPlan()
    {
        const float South = MathF.PI / 2, North = -MathF.PI / 2;
        // A = 정면 (기병 쪽을 봄), B = 측면을 보임 — 둘 다 제압 0.45 (정면 허용 0.5 미만)
        var (w, ai, s, g) = CavWorld();
        var A = Block(w, new Vector2(60, 60), South, 0.45f);
        var B = Block(w, new Vector2(92, 82), North, 0.45f);
        var p = ai.TestCavalryPlan(w, s, g, 24);
        check(p is { Route: false, Angle: 1 } pp && B.Contains(pp.Target), $"기병 표적: 정면 대형보다 측면을 보인 대형 (표적 {(p is { } q1 ? (B.Contains(q1.Target) ? "B 측면" : "A 정면") : "없음")}, 각 {p?.Angle})");
        // 정면 대형만: 측면 경유점으로 돌아감
        var (w2, ai2, s2, g2) = CavWorld();
        var A2 = Block(w2, new Vector2(60, 60), South, 0.45f);
        var p2 = ai2.TestCavalryPlan(w2, s2, g2, 24);
        Vector2 cen = new(A2.Average(u => u.Pos.X), A2.Average(u => u.Pos.Y));
        bool flankWay = p2 is { Route: true } r && Vector2.Dot(r.Way - cen, new Vector2(0, 1)) <= 0.5f && MathF.Abs(r.Way.X - cen.X) >= defs.Tactics.Cavalry.FlankOffset * 0.8f;
        check(flankWay, $"기병: 형성된 정면 보병 → 측면 경유점 ({(p2 is { } q2 ? $"경유 {q2.Route} {q2.Way.X:0},{q2.Way.Y:0} / 중심 {cen.X:0},{cen.Y:0}" : "계획 없음")})");
        // 정면이라도 크게 제압된 적이면 바로 돌격
        Pin(A2, 0.7f);
        var p3 = ai2.TestCavalryPlan(w2, s2, g2, 24);
        check(p3 is { Route: false, Angle: 0 }, $"기병: 제압된 (0.7) 정면 대형은 바로 돌격 (경유 {p3?.Route}, 각 {p3?.Angle})");
        // 기관총이 정면을 덮으면 정면 돌격하지 않음 (측면 경유)
        var (w4, ai4, s4, g4) = CavWorld();
        var A4 = Block(w4, new Vector2(60, 60), South, 0.7f);
        var mg = w4.SpawnUnit(defs.Units["heavy_machinegunner"], 2, new Vector2(60, 63)); mg.Facing = South;
        var p4 = ai4.TestCavalryPlan(w4, s4, g4, 24);
        check(p4 == null || p4.Value.Route || p4.Value.Angle != 0, $"기병: 기관총 정면 사계로는 돌격하지 않음 ({(p4 is { } q4 ? $"경유 {q4.Route} 각 {q4.Angle}" : "계획 없음")})");
        // 측면 기관총은 표적이 될 수 있음 (N18 교착 해소)
        var (w5, ai5, s5, g5) = CavWorld();
        var mgs = Spawn(w5, 6, new Vector2(60, 62), 2, "heavy_machinegunner", cols: 6, gap: 1.5f);
        foreach (var u in mgs) u.Facing = 0f;   // 동쪽을 봄 → 기병 (남쪽) 은 측면
        var p5 = ai5.TestCavalryPlan(w5, s5, g5, 24);
        check(p5 == null, $"기병: 제압되지 않은 기관총 무리는 측면이어도 표적 아님 — 경기관총은 바로 돌려 쏜다 ({(p5 is { } q6 ? $"각 {q6.Angle} 경유 {q6.Route}" : "없음")})");
        Pin(mgs, 0.7f);
        var p5b = ai5.TestCavalryPlan(w5, s5, g5, 24);
        check(p5b is { } q5b && mgs.Contains(q5b.Target), $"기병: 제압된 (0.7) 기관총 무리는 표적 ({(p5b is { } q6b ? $"각 {q6b.Angle} 경유 {q6b.Route}" : "없음")})");
        Pin(mgs, 0f);
        // 같은 기관총이 기병 쪽을 보면 정면 돌격 표적 아님 (경유 또는 없음)
        foreach (var u in mgs) u.Facing = South;
        var p6 = ai5.TestCavalryPlan(w5, s5, g5, 24);
        check(p6 == null || p6.Value.Route, $"기병: 기병 쪽을 본 기관총에는 정면 돌격 없음 ({(p6 is { } q7 ? $"경유 {q7.Route}" : "없음")})");
    }

    // ---------------------------------------------------------------- 소규모 기병: 돌격 안 함, 큰 무리는 돌격

    static (int Charges, long Held) CavSim(int riders)
    {
        var w = Bare();
        Spawn(w, riders, new Vector2(60, 100), 1, "cavalry_default", cols: 8);
        int nFoe = Math.Max(1, (int)MathF.Floor(riders / (defs.Tactics.Cavalry.MinOdds * defs.Tactics.Cavalry.FlankOddsMul)));   // 데이터의 minOdds 를 따라 — 돌격 계획이 서는 최대 적 수 (소규모 판단까지 가게)
        var foe = Spawn(w, nFoe, new Vector2(85, 68), 2, cols: 5);   // pursuit2: flankOddsMul 0.7 → 4기 × 국지 병력비가 맞는 적 수 (소규모 판단까지 가게)
        foreach (var u in foe) u.Facing = 0;   // 기병 쪽에 측면
        w.Controllers.Add((1, new TacticalAi()));
        w.Hold(2, foe);
        for (int i = 0; i < Ticks(45); i++)
        {
            w.Step();
            Pin(foe, 0.55f);
        }
        return (St(w, 1).CavalryCharges, w.Stats.CavSmallHeld);
    }

    static void TestSmallCavalry()
    {
        int min = defs.Tactics.Cavalry.MinRiders;
        var (cs, held) = CavSim(Math.Max(1, min / 2));
        var (cl, _) = CavSim(min + 8);
        check(cs == 0 && held > 0, $"소규모 기병 ({Math.Max(1, min / 2)}기 < {min}): 돌격 안 함 ({cs}회), 정찰 · 차장 보류 {held}회");
        check(cl > 0, $"큰 기병 무리 ({min + 8}기): 돌격 ({cl}회)");
    }

    // ---------------------------------------------------------------- 기관총 앞 교착 (N18)

    static void TestMgStalemate()
    {
        var setup = new BattleSetup { SideA = "cavalry_default:30", SideB = "heavy_machinegunner:30", Map = "flat", Distance = 60, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 1);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        long st0 = w.Stats.CavScreens, fl0 = w.Stats.CavChargeFlank + w.Stats.CavChargeRear + w.Stats.CavFlankRoutes;
        int idleFront = 0, maxIdle = 0;
        for (int i = 0; i < Ticks(240) && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 60 != 0) continue;
            var cav = w.Units.Where(u => u.Owner == 1 && u.Alive && u.Def.Mounted).ToList();
            var mg = w.Units.Where(u => u.Owner == 2 && u.Alive).ToList();
            if (cav.Count == 0 || mg.Count == 0) break;
            Vector2 cc = new(cav.Average(u => u.Pos.X), cav.Average(u => u.Pos.Y)), mc = new(mg.Average(u => u.Pos.X), mg.Average(u => u.Pos.Y));
            float d = Vector2.Distance(cc, mc);
            bool idle = d is > 15 and < 32 && cav.All(u => !u.Charging && u.Velocity == Vector2.Zero);
            idleFront = idle ? idleFront + 1 : 0; maxIdle = Math.Max(maxIdle, idleFront);
        }
        long acts = w.Stats.CavScreens - st0 + w.Stats.CavChargeFlank + w.Stats.CavChargeRear + w.Stats.CavFlankRoutes - fl0;
        int mgLost = 30 - w.Units.Count(u => u.Owner == 2 && u.Alive);
        int cavLost = 30 - w.Units.Count(u => u.Owner == 1 && u.Alive);
        check(maxIdle < 60 && cavLost < 15, $"기병 대 기관총: 정면에 서 있지 않고 (차장 · 측면 이동 · 물러남 {acts}회) 기관총에 돌격해 녹지 않음 — 적 앞 15~32칸 정지 최장 {maxIdle}초 (< 60), 기관총 손실 {mgLost} · 기병 손실 {cavLost}");
    }
}
