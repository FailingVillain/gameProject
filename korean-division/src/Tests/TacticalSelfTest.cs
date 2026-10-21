using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 전술 AI 1단계 (ROADMAP ⑤) 검증: 프로필 · 등록 · 3층 결정 · 결정성 · 저장 이어 하기 · 안개 (CanSee 만) · 태세 · 병과 운용 (기관총 거치 · 기병 돌격 · 곡사포 관측 사격).
/// KD_SELFTEST_ONLY=tactical 로 단독 실행.
/// </summary>
public static class TacticalSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[전술 AI 1 · 2단계]");
        try
        {
            TestProfiles();
            TestRegistry();
            TestLayers();
            TestCommanderReserve();
            TestNoMutation();
            TestDeterminism();
            TestSnapshotContinue();
            TestFogOnly();
            TestPosture();
            TestArms();
            TestStage2Layers();
            TestDirectiveGroups();
            TestReinforcementIntake();
            TestTacMap();
            TestFireAndMovement();
            TestArtilleryPrep();
            TestDefenceInDepth();
        }
        catch (Exception e) { check(false, $"전술 AI 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World BattleWorld(string a, string b, string ai = "tactical", int seed = 3, string map = "flat", int distance = 40)
    {
        var setup = new BattleSetup { SideA = a, SideB = b, Map = map, Distance = distance, AiVsAi = true, AiA = ai, AiB = ai };
        var w = setup.CreateWorld(defs, seed);
        w.Fog.Enabled = true;
        if (ai != "none")
        {
            w.Controllers.Add((1, AiRegistry.Create(ai)!));
            w.Controllers.Add((2, AiRegistry.Create(ai)!));
        }
        return w;
    }

    static void Steps(World w, float seconds) { for (int i = 0, n = (int)MathF.Round(seconds / Config.SimDt); i < n && w.Winner == 0; i++) w.Step(); }

    static TacFaction St(World w, int f) => ((TacticalAi)w.Controllers.First(c => c.Faction == f).Ai).StateOf(f)!;

    static string Sig(GameCommand c)
    {
        static string Ids(int[] a) => string.Join(",", a);
        return c switch
        {
            MoveCommand m => $"M{m.Faction}:{Ids(m.Units)}>{m.Target.X:0.###},{m.Target.Y:0.###}",
            AttackMoveCommand m => $"A{m.Faction}:{Ids(m.Units)}>{m.Target.X:0.###},{m.Target.Y:0.###}",
            RetreatCommand m => $"R{m.Faction}:{Ids(m.Units)}>{m.Target.X:0.###},{m.Target.Y:0.###}",
            QuickStepCommand m => $"Q{m.Faction}:{Ids(m.Units)}>{m.Target.X:0.###},{m.Target.Y:0.###}",
            HoldCommand m => $"H{m.Faction}:{Ids(m.Units)}",
            ChargeCommand m => $"C{m.Faction}:{Ids(m.Units)}>{m.Target.X:0.###},{m.Target.Y:0.###}/{m.TargetId}",
            FormCompanyCommand m => $"F{m.Faction}:{Ids(m.Units)}",
            _ => c.GetType().Name + c.Faction,
        };
    }

    // ---------------------------------------------------------------- 프로필 · 등록

    static void TestProfiles()
    {
        AiProfiles.Load();
        var names = AiProfiles.Names;
        string[] need = { "default", "aggressive", "defensive", "korean", "japanese", "fengtian", "russian-white", "russian-red" };
        check(need.All(n => names.Contains(n)), "ai_profiles.json: default · aggressive · defensive · 세력 기본값 5종");
        check(AiProfiles.Resolve(null, null, "russian").Name == "russian-white" && AiProfiles.Resolve(null, null, "soviet").Name == "russian-red", "별칭: russian → russian-white, soviet → russian-red");
        check(AiProfiles.Resolve(null, null, "korean").Name == "korean" && AiProfiles.Resolve(null, null, "Blue").Name == "default", "세력 영문 id → 프로필, 모르는 id → default");
        check(AiProfiles.Resolve("aggressive", "defensive", "korean").Name == "aggressive" && AiProfiles.Resolve(null, "defensive", "korean").Name == "defensive", "우선순위: 지정 > 지도 aiProfile > 세력 id");
        var d = AiProfiles.Find("default")!; var a = AiProfiles.Find("aggressive")!; var k = AiProfiles.Find("korean")!;
        check(a.Strategy.DefendPatience == d.Strategy.DefendPatience && a.Tactics.Aggressiveness > d.Tactics.Aggressiveness && k.Strategy.Reserve == d.Strategy.Reserve && k.Tactics.ChargeRange == d.Tactics.ChargeRange, "프로필은 default 위에 절 (strategy · tactics) 마다 덮어쓴다 (빠진 값 = default)");
        check(a.Strategy.Scheme == "flank" && AiProfiles.Find("fengtian")!.Tactics.FireAndMovement == false && d.Tactics.FireAndMovement, "전략 절 (scheme) · 전술 절 (fireAndMovement) 이 따로 읽힌다");
        check(AiProfiles.Find("defensive")!.DefaultPosture == AiPosture.Defend && d.DefaultPosture == AiPosture.Attack, "프로필 태세 (defensive = 방어)");
        var copy = AiProfiles.Find("default")!; copy.Tactics.Trench = -1; copy.Strategy.Cavalry = -1;
        check(AiProfiles.Find("default")!.Tactics.Trench >= 0 && AiProfiles.Find("default")!.Strategy.Cavalry >= 0, "Find 는 깊은 복사본 (상태에 넣어도 원본 불변)");
    }

    static void TestRegistry()
    {
        check(AiRegistry.Default == "tactical" && AiRegistry.Create("tactical") is TacticalAi, "기본 AI = tactical");
        var t = AiRegistry.Create("tactical:aggressive");
        check(t is TacticalAi && t.Name == "tactical:aggressive" && AiRegistry.Create("tactical:없음") == null, "tactical:<프로필> 생성 · 이름 왕복, 모르는 프로필은 거부");
        check(AiRegistry.IsKnown("battle") && AiRegistry.IsKnown("standard") && AiRegistry.IsKnown("passive") && !AiRegistry.IsKnown("nope"), "옛 AI 는 비교용으로 남음");
        check(new BattleSetup().AiA == "tactical", "교전 테스트 기본 AI = tactical");
    }

    // ---------------------------------------------------------------- 층 결정

    static void TestLayers()
    {
        var w = BattleWorld("infantry:200", "infantry:200");
        Steps(w, 3);
        var s = St(w, 1);
        var lines = s.Groups.Where(g => g.Kind == TacKind.Line).ToList();
        check(s.Phase == 2 && lines.Count == 4 && lines.All(g => g.CompanyId != 0 && w.FindCompany(g.CompanyId)?.Members.Count == 50), $"편성: 보병 200 → 50명 중대 4개 (FormCompany 명령, 중대 {lines.Count})");
        check(s.Battalions.Count == 1 && s.Regiments.Count == 1 && s.Battalions[0].Regiment == s.Regiments[0].Id, "중대 4개 → 대대 1개 → 연대 1개 (세력 전략 → 연대 → 대대 → 중대)");
        check(w.CommandLog.Where(c => c.Command.Faction == 1).All(c => c.Command is FormCompanyCommand or FormBattalionCommand or AttackCommand or MoveCommand or AttackMoveCommand or QuickStepCommand or RetreatCommand or HoldCommand or ChargeCommand or SetMarchFireCommand or SetCompanyTacticCommand or SetPostureCommand or SetKeepFormationCommand),
            "전술 AI 는 공개 명령만 사용 (편성 · 이동 · 공격 이동 · 속보 · 사수 · 돌격 · 돌격 사격 · 중대 전술 틀 · 중대 자세)");
        var reserve = lines.Where(g => g.Task == TacTask.Reserve).ToList();
        var front = lines.Where(g => g.Task == TacTask.Attack).ToList();
        check(reserve.Count == 1 && front.Count == 3, $"중대 3개 이상 → 예비 중대 1 (예비 {reserve.Count}, 앞줄 {front.Count})");
        var side = new Vector2(-s.Dir.Y, s.Dir.X);
        var byPos = front.OrderBy(g => Vector2.Dot(g.Center, side)).Select(g => g.Id).ToList();
        var byTask = front.OrderBy(g => Vector2.Dot(g.TaskPoint, side)).Select(g => g.Id).ToList();
        check(byPos.SequenceEqual(byTask), "대대 → 중대 정면: 현재 좌우 순서대로 구역 배정 (교차 없음)");
        float gap = front.Count >= 2 ? front.Zip(front.Skip(1)).Min(p => Vector2.Distance(p.First.TaskPoint, p.Second.TaskPoint)) : 99;
        check(gap > 4f, $"앞줄 중대 구역이 서로 떨어져 있다 (최소 간격 {gap:0.0})");
        check(Vector2.Dot(reserve[0].TaskPoint - front.Aggregate(Vector2.Zero, (a, g) => a + g.TaskPoint) / front.Count, s.Dir) < -5f, "예비 중대는 앞줄 뒤");
        check(s.Dir.X > 0.8f, "세력 1 (왼쪽) 의 진행 방향 = 적 쪽 (+x)");
    }

    static void TestCommanderReserve()
    {
        var w = BattleWorld("infantry:500", "infantry:60", distance: 80);
        Steps(w, 3);
        var s = St(w, 1);
        check(s.Battalions.Count == 3, $"보병 500 → 중대 10 → 대대 3 (대대 {s.Battalions.Count})");
        check(s.Regiments.Count == 1 && s.Regiments[0].ReserveBattalion != 0 && s.Battalions.Count(b => b.Task == TacTask.Reserve) == 1 && s.Battalions.Count(b => b.Task == TacTask.Attack) == 2, "대대 3개 → 연대 1 (예비 대대 1, 공격 구역 2)");
        var mixed = BattleWorld("infantry:100,heavy_machinegunner:8,cavalry_default:12,heavy_howitzer:2,supply_wagon:2,scout:3", "infantry:60");
        Steps(mixed, 3);
        var m = St(mixed, 1);
        check(m.Groups.Count(g => g.Kind == TacKind.Fire) == 1 && m.Groups.Count(g => g.Kind == TacKind.Cavalry) == 1 && m.Groups.Count(g => g.Kind == TacKind.Artillery) == 1
              && m.Groups.Count(g => g.Kind == TacKind.Wagon) == 1 && m.Groups.Count(g => g.Kind == TacKind.Scout) == 1, "병과별 분견대: 화력 · 기병 · 포병 · 마차 · 정찰");
        check(m.Groups.Where(g => g.Kind is TacKind.Fire or TacKind.Wagon).All(g => g.Battalion != 0) && m.Groups.Where(g => g.Kind is TacKind.Cavalry or TacKind.Artillery).All(g => g.Battalion == 0),
            "화력 · 마차 = 대대에 붙음, 기병 · 포병 = 지휘관 직속");
    }

    static void TestNoMutation()
    {
        var w = BattleWorld("infantry:80,heavy_machinegunner:4,cavalry_default:6", "infantry:80", ai: "none");
        w.RecordCommands = false;
        Steps(w, 1);
        var ai = new TacticalAi();
        ulong before = w.ComputeChecksum();
        ai.Tick(w, 1); ai.Tick(w, 1);
        check(w.ComputeChecksum() == before, "AI.Tick 은 시뮬레이션 상태를 바꾸지 않는다 (명령 대기열에만)");
    }

    // ---------------------------------------------------------------- 결정성 · 저장

    const string Mix = "infantry:120,officer:3,heavy_machinegunner:6,cavalry_default:12,heavy_howitzer:2,supply_wagon:2,scout:2";

    static void TestDeterminism()
    {
        (ulong, int, string) RunOnce()
        {
            var w = BattleWorld(Mix, Mix, seed: 9, map: "terrain");
            Steps(w, 45);
            var s = St(w, 1);
            return (w.ComputeChecksum(), w.CommandLog.Count, string.Join("|", s.Groups.Select(g => $"{g.Id}{g.State}{g.Task}{g.TaskPoint}")));
        }
        var a = RunOnce(); var b = RunOnce();
        check(a == b, $"같은 입력 → 같은 결과 (체크섬 · 명령 수 {a.Item2} · 층 결정)");
    }

    static void TestSnapshotContinue()
    {
        var meta = new SnapshotMeta("tactical", 1, true, "tactical");
        var w = BattleWorld(Mix, Mix, seed: 21, map: "terrain");
        Steps(w, 25);
        var bytes = Snapshot.Save(w, meta);
        var l = Snapshot.Load(bytes, defs).World;
        Steps(w, 25); Steps(l, 25);
        check(w.ComputeChecksum() == l.ComputeChecksum() && l.Controllers.All(c => c.Ai is TacticalAi), "저장 → 불러오기 → 이어 하기: 저장 안 한 쪽과 체크섬 같음 (AI 상태 · 프로필 저장)");
        var sl = ((TacticalAi)l.Controllers[0].Ai).StateOf(1)!;
        var sw = St(w, 1);
        check(sl.Groups.Count == sw.Groups.Count && sl.Profile.Name == sw.Profile.Name && sl.Orders == sw.Orders, "불러온 AI 의 중대 · 프로필 · 누계가 같다");
    }

    // ---------------------------------------------------------------- 안개

    static void TestFogOnly()
    {
        // 같은 세력 1 · 같은 보이는 적, 보이지 않는 적 배치만 다른 두 월드 → 세력 1 의 명령이 같아야 한다
        World Make(bool extra)
        {
            var m = MapFile.Load("battle_flat", 1);
            var w = World.CreateBattleWorld(m, defs, new BattleSetup { SideA = "infantry:1", SideB = "infantry:1" });
            for (int i = 0; i < 60; i++) w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30 + i % 6, 40 + i / 6 * 1.2f));
            for (int i = 0; i < 5; i++) w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(232, 30 + i));
            if (extra) for (int i = 0; i < 30; i++) w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(215 + i % 5, 95 + i / 5));
            w.Fog.Enabled = true;
            w.Fog.Update();
            w.Nav.Sync();
            w.Controllers.Add((1, new TacticalAi()));
            return w;
        }
        var a = Make(false); var b = Make(true);
        var seen = new List<Unit>();
        TacticalAi.VisibleEnemies(a, 1, seen);
        check(seen.Count == 0, "멀리 있는 적은 VisibleEnemies 에 없다 (Fog.CanSee)");
        Steps(a, 20); Steps(b, 20);
        TacticalAi.VisibleEnemies(b, 1, seen);
        var sa = a.CommandLog.Where(c => c.Command.Faction == 1).Select(c => c.Tick + ":" + Sig(c.Command)).ToList();
        var sb = b.CommandLog.Where(c => c.Command.Faction == 1).Select(c => c.Tick + ":" + Sig(c.Command)).ToList();
        check(seen.Count == 0 && sa.Count > 3 && sa.SequenceEqual(sb), $"보이지 않는 적 배치가 달라도 AI 명령이 같다 (치트 없음, 명령 {sa.Count})");
        var s = ((TacticalAi)a.Controllers[0].Ai).StateOf(1)!;
        check(!s.HasEnemy && s.Target.X > 150, "적을 못 봤으면 공개 정보 (적 시작 위치) 로 진격");
    }

    // ---------------------------------------------------------------- 태세

    static void TestPosture()
    {
        // 방어: 지역 지시 (미션 접점) → 그 자리 근처의 (지도) 참호에 들어가 사수, 적이 다가와도 진지를 크게 벗어나지 않는다
        var w = BattleWorld("infantry:150", "infantry:120", distance: 60);
        var ai1 = (TacticalAi)w.Controllers[0].Ai;
        var start = Centroid(w, 1);
        var foe0 = Centroid(w, 2);
        // 진지 앞 3칸에 적 방향과 수직인 참호선 41칸 (지도 · 미션 배치 참호 대용)
        Vector2 fd = Vector2.Normalize(foe0 - start), side = new(-fd.Y, fd.X);
        for (int k = -20; k <= 20; k++)
        {
            var (tx, ty) = GameMap.TileOf(start + fd * 3f + side * k);
            if (w.Map.InBounds(tx, ty) && w.Map.Get(tx, ty) is TerrainType.Grass or TerrainType.Dirt or TerrainType.Sand) w.MutateTerrain(tx, ty, TerrainType.Trench);
        }
        ai1.SetDirective(1, AiPosture.Defend, "defensive", start, 20);
        Steps(w, 40);
        var s = St(w, 1);
        check(s.Profile.Name == "defensive" && s.Effective == AiPosture.Defend, "SetDirective: 프로필 · 태세 적용");
        int inTrench = w.Units.Count(u => u.Owner == 1 && u.InTrench);
        check(s.TrenchMoves > 0 && inTrench >= 20, $"방어 태세: 근처 참호로 진지 (참호 이동 {s.TrenchMoves}, 참호 안 {inTrench}명)");
        check(!w.CommandLog.Any(c => c.Command.GetType().Name.StartsWith("Dig")), "참호 파기 명령 없음 (건설 제거)");
        float moved = Vector2.Distance(Centroid(w, 1), start);
        check(moved < 18f, $"방어 태세: 진지에서 크게 벗어나지 않음 ({moved:0.0}칸)");
        // 공격: 같은 배치에서 공격이면 적 쪽으로 전진
        var w2 = BattleWorld("infantry:150", "infantry:120", distance: 60);
        var start2 = Centroid(w2, 1);
        ((TacticalAi)w2.Controllers[1].Ai).SetDirective(2, AiPosture.Defend, "defensive", Centroid(w2, 2), 20);
        Steps(w2, 20);
        check(Centroid(w2, 1).X - start2.X > 15f, $"공격 태세: 적 쪽으로 전진 ({Centroid(w2, 1).X - start2.X:0.0}칸)");
        // 지역 고수: 지역 밖 적에는 나가지 않고 진지
        var w3 = BattleWorld("infantry:100", "infantry:20", distance: 100);
        var start3 = Centroid(w3, 1);
        ((TacticalAi)w3.Controllers[0].Ai).SetDirective(1, AiPosture.HoldArea, null, start3, 15);
        w3.Controllers.RemoveAt(1);
        Steps(w3, 30);
        check(St(w3, 1).Effective == AiPosture.HoldArea && Vector2.Distance(Centroid(w3, 1), start3) < 15f, "지역 고수: 적이 지역 밖이면 그 자리");
    }

    static Vector2 Centroid(World w, int f)
    {
        Vector2 c = Vector2.Zero; int n = 0;
        foreach (var u in w.Units) if (u.Owner == f && u.Alive) { c += u.Pos; n++; }
        return n > 0 ? c / n : c;
    }

    // ---------------------------------------------------------------- 병과 운용

    static void TestArms()
    {
        // 기관총: 교전 전에 거치 (사수 → 자동 방열)
        var w = BattleWorld("infantry:80,heavy_machinegunner:8", "infantry:100", seed: 5);
        bool deployedBeforeFire = false; int mgShots = 0;
        var mgIds = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_machinegunner").Select(u => u.Id).ToHashSet();
        for (int i = 0; i < 90 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            foreach (var sh in w.Shots)
                if (sh.Tick == w.Tick - 1 && mgIds.Contains(sh.ShooterId))
                {
                    if (mgShots == 0 && w.Find(sh.ShooterId) is Unit mu) deployedBeforeFire = mu.Deployed;
                    mgShots++;
                }
        }
        check(mgShots > 50 && deployedBeforeFire && St(w, 1).FireHolds > 0, $"기관총: 거치한 뒤 사격 (사격 {mgShots}발)");

        // 기병: 제압 · 흐트러진 보병에게 돌격 (근접 타격 발생)
        var c = BattleWorld("infantry:120,officer:3,cavalry_default:24", "infantry:150,officer:3", seed: 2);
        var cav = c.Units.Where(u => u.Owner == 1 && u.Def.Mounted).Select(u => u.Id).ToHashSet();
        int strikes = 0;
        for (int i = 0; i < 150 * 60 && c.Winner == 0; i++)
        {
            c.Step();
            foreach (var m in c.MeleeStrikes) if (m.Tick == c.Tick - 1 && cav.Contains(m.AttackerId)) strikes++;
        }
        check(St(c, 1).CavalryCharges > 0 && strikes > 0, $"기병: 측면에서 돌격 (돌격 {St(c, 1).CavalryCharges}회, 근접 {strikes}, {c.Time:0}초 승자 {c.Winner}, 기병 생존 {c.Units.Count(u => cav.Contains(u.Id) && u.Alive)})");

        // 곡사포: 장교가 보는 적에게만 (관측 전용) — 방열 후 포격
        // arty3: 이 판은 보병선이 25~30초에 무너지는 임계 판이라 방렬 지연 (0~3초) + 방렬 ±15% 가 겹치면 (이 시드의 포 Id 들 = 최대 26초) 첫 발 전에 포가 유린됨 —
        // 여기서는 관측 전용 사격만 보므로 방렬 난수를 끄고 본다 (방렬 난수 자체는 Arty3SelfTest). 사용자 확인 대기 (docs/DECISIONS_PENDING.md).
        var cr = defs.CombatRules; float dMax = cr.GunDeployDelayMax, dVar = cr.GunDeployTimeVar;
        cr.GunDeployDelayMax = 0; cr.GunDeployTimeVar = 0;
        var a = BattleWorld("infantry:120,officer:6,heavy_howitzer:3", "infantry:130,officer:4", seed: 2);
        var guns = a.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_howitzer").Select(u => u.Id).ToHashSet();
        int impacts = 0; bool allSpotted = true; double gunLost = -1, gunSeen = -1;
        for (int i = 0; i < 150 * 60 && a.Winner == 0; i++)
        {
            a.Step();
            if (gunLost < 0 && a.Units.Any(u => guns.Contains(u.Id) && !u.Alive)) gunLost = a.Time;
            if (gunSeen < 0 && a.Units.Any(e => e.Alive && e.Owner == 2 && a.SeenBySpotter(1, e))) gunSeen = a.Time;
            foreach (var im in a.Impacts)
                if (im.Tick == a.Tick - 1 && guns.Contains(im.ShooterId))
                {
                    impacts++;
                    if (a.Find(im.ShooterId) is Unit g && g.Target != null && !a.SeenBySpotter(1, g.Target)) allSpotted = false;
                }
        }
        cr.GunDeployDelayMax = dMax; cr.GunDeployTimeVar = dVar;
        check(impacts > 0 && allSpotted, $"곡사포: 관측된 적에게 방열 · 포격 ({impacts}발, {a.Time:0}초 승자 {a.Winner}, 포 방열 {a.Units.Count(u => guns.Contains(u.Id) && u.Deployed)}, 포 표적 {a.Units.Count(u => guns.Contains(u.Id) && u.Target != null)}, 첫 포 손실 {gunLost:0}초, 첫 관측 {gunSeen:0}초)");
    }

    // ================================================================ 2단계 (docs/TACTICAL_AI_DESIGN.md)

    static World ManualWorld(string map = "battle_flat")
    {
        var m = MapFile.Load(map, 1);
        return World.CreateBattleWorld(m, defs, new BattleSetup { SideA = "infantry:1", SideB = "infantry:1" });
    }

    static void Ready(World w)
    {
        w.Fog.Enabled = true;
        w.Fog.Update();
        w.Nav.Sync();
    }

    static List<Unit> Block(World w, int f, string unit, int n, Vector2 at, int cols = 10)
    {
        var l = new List<Unit>();
        for (int i = 0; i < n; i++) l.Add(w.SpawnUnit(defs.Units[unit], f, at + new Vector2(i % cols * 1.1f, i / cols * 1.1f)));
        return l;
    }

    static Vector2 Centroid(List<Unit> l)
    {
        Vector2 c = Vector2.Zero; int n = 0;
        foreach (var u in l) if (u.Alive) { c += u.Pos; n++; }
        return n > 0 ? c / n : c;
    }

    static readonly bool Dbg = Environment.GetEnvironmentVariable("KD_TAC_DBG") == "1";

    static void TestStage2Layers()
    {
        // 세력 전략 → 연대 → 대대 → 중대: 보병 1000 → 중대 20 → 대대 5 → 연대 2 (3 + 2), 월드 대대 번호 연결
        var w = BattleWorld("infantry:1000", "infantry:100", distance: 90);
        Steps(w, 6);
        var s = St(w, 1);
        check(s.Battalions.Count == 5 && s.Regiments.Count == 2 && s.Battalions.All(b => s.Regiments.Exists(r => r.Id == b.Regiment)),
            $"층 매핑: 중대 {s.Groups.Count(g => g.Kind == TacKind.Line)} → 대대 {s.Battalions.Count} → 연대 {s.Regiments.Count} (연대당 대대 ≤ regimentSize)");
        check(s.Regiments.Count(r => r.Main) == 1, "주공 연대는 하나");
        var linked = s.Battalions.Where(b => b.WorldId != 0).ToList();
        check(linked.Count == 5 && linked.All(b => b.Number >= 1 && b.Number <= 9 && w.Battalions.Any(x => x.Id == b.WorldId && x.Number == b.Number))
              && s.Groups.Where(g => g.Kind == TacKind.Line).All(g => w.FindCompany(g.CompanyId)?.Battalion?.Id == s.Battalions.First(b => b.Id == g.Battalion).WorldId),
            $"AI 대대 ↔ 월드 대대 · 번호 연결 (FormBattalion, 연결 {linked.Count}/5: {string.Join(",", linked.Select(b => b.Number))})");
        check(s.Directives.Count == 1 && s.Directives[0].Alive > 0, "지시 그룹 0 (세력 기본) 하나");
        var bytes = Snapshot.Save(w, new SnapshotMeta("tactical", 1, true, "tactical"));
        var l = Snapshot.Load(bytes, defs).World;
        Steps(w, 10); Steps(l, 10);
        check(w.ComputeChecksum() == l.ComputeChecksum(), "2단계 상태 (연대 · 지시 그룹 · 역할) 저장 → 이어 하기 체크섬 같음");
    }

    static void TestDirectiveGroups()
    {
        // 흩어진 매복조 (구역 없는 hold) 두 무리: 지시 그룹마다 자기 자리를 지킨다 (1단계는 한 줄로 합쳐졌다)
        var w = ManualWorld();
        var a = Block(w, 2, "infantry", 20, new Vector2(150, 30), 5);
        var b = Block(w, 2, "infantry", 20, new Vector2(150, 110), 5);
        Block(w, 1, "infantry", 10, new Vector2(20, 70), 5);
        Ready(w);
        var ai = new TacticalAi();
        ai.SetDirective(2, AiPosture.HoldArea);
        ai.SetDirective(2, 1, AiPosture.HoldArea, null, 12, null, a.Select(u => u.Id).ToList());
        ai.SetDirective(2, 2, AiPosture.HoldArea, null, 12, null, b.Select(u => u.Id).ToList());
        w.Controllers.Add((2, ai));
        Vector2 ca = Centroid(a), cb = Centroid(b);
        Steps(w, 30);
        var s = ai.StateOf(2)!;
        check(s.Directives.Count == 3 && s.Groups.Where(g => g.Kind == TacKind.Line).Select(g => g.Directive).Distinct().OrderBy(x => x).SequenceEqual(new[] { 1, 2 }),
            "지시 그룹: 유닛 Id → 그룹별 편성 (그룹 1 · 2)");
        float da = Vector2.Distance(Centroid(a), ca), db = Vector2.Distance(Centroid(b), cb);
        check(da < 8f && db < 8f, $"매복조가 각자 자리를 지킨다 (이동 {da:0.0} · {db:0.0}칸, 사이 {Vector2.Distance(ca, cb):0}칸)");
        // 미션: supply_escort 마적단 (구역 없는 hold) → 흩어진 무리마다 그룹
        var def = MissionDef.Load("supply_escort");
        var mw = World.CreateMissionWorld(def, defs, def.Seed, null);
        World.AttachMissionAi(mw, def.Player, true);
        var mt = (TacticalAi)mw.Controllers.First(c => c.Faction == 2 && c.Ai is TacticalAi).Ai;
        Steps(mw, 3);
        var ms = mt.StateOf(2)!;
        check(ms.Directives.Count >= 4, $"미션 supply_escort: 매복조 · 증원 지시 그룹 (그룹 {ms.Directives.Count - 1})");
    }

    static void TestReinforcementIntake()
    {
        var w = BattleWorld("infantry:120", "infantry:60", distance: 90);
        Steps(w, 6);
        var s = St(w, 1);
        int before = s.Groups.Count;
        var fresh = Block(w, 1, "infantry", 40, Centroid(w, 1) - new Vector2(20, 0), 8);
        Steps(w, 6);
        bool inGroup = fresh.All(u => !u.Alive || s.Groups.Exists(g => (g.CompanyId != 0 && u.Company?.Id == g.CompanyId) || g.Units.Contains(u.Id)));
        check(s.Groups.Count > before && inGroup && s.Reinforced > 0 && fresh.Where(u => u.Alive).All(u => u.Company != null),
            $"증원 편입: 새 유닛 → 중대 편성 → 대대 · 연대 배정 (무리 {before} → {s.Groups.Count}, 편입 {s.Reinforced})");
        check(w.CommandLog.Count(c => c.Command is FormCompanyCommand { Faction: 1 }) >= 2, "증원 편성은 명령 (FormCompany) 으로");
    }

    static void TestTacMap()
    {
        // 엄폐 칸 선택: 평지 기준점 옆에 숲이 있으면 숲 칸, 없으면 기준점 그대로 (평지에서 뭉치지 않게)
        var w = ManualWorld();
        var m = w.Map;
        var grid = new TacGrid();
        grid.Build(m, new List<Unit>());
        var T = new TacticsProfile();
        Vector2 from = new(60, 70), enemy = new(100, 70);
        var p0 = TacMap.For(m).PickPosition(from, enemy, 17, 24, T, grid, 24);
        check(Vector2.Distance(p0, new Vector2(83, 70)) < 0.5f, $"평지: 기준점 (적에서 원하는 거리) 그대로 ({p0.X:0.0},{p0.Y:0.0})");
        for (int y = 72; y < 80; y++) for (int x = 80; x < 88; x++) m.Set(x, y, TerrainType.Forest);
        var p1 = TacMap.For(m).PickPosition(from, enemy, 17, 24, T, grid, 24);
        var tmd = TacMap.For(m); int fc = tmd.CellOf(new Vector2(84, 76)); var fbt = tmd.BestTile(fc);
        check(m.TerrainCover(p1).Hit < 1f, $"옆 숲 칸 → 숲 타일 선택 ({p1.X:0.0},{p1.Y:0.0}) — 지형이 바뀌면 캐시 다시 계산 (TerrainHash) [숲칸 엄폐 {tmd.Cover(fc):0.00} 최고타일 {fbt} 사선 {m.HasFiringLine(fbt, enemy, 0, 0)} 기준 사선 {m.HasFiringLine(new Vector2(83, 70), enemy, 0, 0)}]");
        var p2 = TacMap.For(m).PickPosition(from, enemy, 17, 24, T, grid, 24);
        check(p1 == p2, "같은 지형 · 같은 입력 → 같은 위치 (순수 함수)");
    }

    static void TestFireAndMovement()
    {
        // 앞 대대가 하나면 대대 안에서 화력 기반 · 기동 중대로 나누고, 화력 우세 뒤 사격선 전진은 분대 교대 약진
        var w = BattleWorld("infantry:200,officer:4", "infantry:70", seed: 4, distance: 60);
        bool split = false, roles = false;
        for (int i = 0; i < 120 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 120 != 0) continue;
            var st = ((TacticalAi)w.Controllers[0].Ai).StateOf(1);
            if (st == null) continue;
            split |= st.Battalions.Any(b => b.Split);
            roles |= st.Groups.Any(g => g.Role == TacRole.BaseOfFire) && st.Groups.Any(g => g.Role == TacRole.Manoeuvre);
        }
        var s = St(w, 1);
        check(split && roles, "접촉 후 역할: 대대 분할 → 화력 기반 중대 + 기동 중대");
        // N25: 적 제압이 실제로 오르면 화력 우세 뒤 약진은 중대 지휘관의 틀 행동 (TacticDrill 사격조 교대 약진, Stats.DrillBounds) 이 맡는 경우가 많다 — 둘 중 하나
        check(s.SquadBounds > 0 || w.Stats.DrillBounds > 0, $"사격선 전진: 분대 교대 약진 (전술 AI {s.SquadBounds}회 · 틀 행동 사격조 약진 {w.Stats.DrillBounds}회)");
    }

    static void TestArtilleryPrep()
    {
        var w = BattleWorld("infantry:150,officer:6,heavy_howitzer:3", "infantry:120,officer:3", seed: 2, distance: 70);
        bool prep = false, assault = false, sup = false;
        for (int i = 0; i < 150 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 60 != 0) continue;
            var st = ((TacticalAi)w.Controllers[0].Ai).StateOf(1);
            if (st == null) continue;
            if (Dbg && i % 600 == 0) Console.WriteLine($"    tac t={w.Time:0} A {w.Units.Count(u => u.Owner == 1)} B {w.Units.Count(u => u.Owner == 2)} " + TacticalAi.Describe(st));
            sup |= st.Groups.Any(g => g.Kind == TacKind.Artillery && g.Supports != 0 && st.Regiments.Exists(r => r.Id == g.Supports && r.Main));
            foreach (var r in st.Regiments) { if (r.Phase == RegPhase.Prep) prep = true; if (prep && r.Phase == RegPhase.Assault) assault = true; }
        }
        var s = St(w, 1);
        check(sup, "포병 → 주공 연대 지원 배정");
        check(prep && assault && s.Preps > 0, $"준비사격 → 돌격 국면 (준비사격 {s.Preps}, 연신 {s.Lifts}, 돌격 {s.Charges}, 분대약진 {s.SquadBounds})");
    }

    static void TestDefenceInDepth()
    {
        // 방어 지시 (보병 600 = 대대 3 → 연대 1): 앞 대대 2 = 주저항선, 예비 대대 = 역습, 전초 분대 → 접촉하면 철수
        var w = BattleWorld("infantry:600,officer:12", "infantry:400,officer:8", seed: 7, distance: 90);
        var ai1 = (TacticalAi)w.Controllers[0].Ai;
        ai1.SetDirective(1, AiPosture.Defend, null, Centroid(w, 1), 30);
        bool mainLine = false, counter = false;
        for (int i = 0; i < 150 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 120 != 0) continue;
            var st = ((TacticalAi)w.Controllers[0].Ai).StateOf(1);
            if (st == null) continue;
            if (Dbg && i % 600 == 0) Console.WriteLine($"    tac t={w.Time:0} A {w.Units.Count(u => u.Owner == 1)} B {w.Units.Count(u => u.Owner == 2)} " + TacticalAi.Describe(st));
            mainLine |= st.Battalions.Count(b => b.Role == TacRole.MainLine) == 2;
            counter |= st.Battalions.Any(b => b.Role == TacRole.Counter);
        }
        var s = St(w, 1);
        check(mainLine && counter, "종심 방어: 주저항선 대대 2 + 역습 예비 대대");
        check(s.Outposts > 0 && s.OutpostWithdrawals > 0, $"전초 분대 → 접촉 시 철수 (전초 {s.Outposts}, 철수 {s.OutpostWithdrawals}, 역습 {s.Counterattacks})");
        var badPick = s.Groups.Where(g => g.Task == TacTask.Defend && g.Kind == TacKind.Line && g.PickKind != 2).Select(g => $"#{g.Id} {g.Role} {g.State} 선택 {g.PickKind} 대대 {g.Battalion}").ToList();
        check(badPick.Count == 0, $"주저항선 중대 진지 = TacMap 깊이 선택{(badPick.Count > 0 ? " (아님: " + string.Join(", ", badPick) + ")" : "")}");
    }
}
