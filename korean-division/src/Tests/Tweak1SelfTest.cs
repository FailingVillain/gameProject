using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// tweak1 (2026-10-19 사용자 결정 묶음): ① 대열 정비 — 화력조 · 지원 역할 제외, 자기 돌격 중 보류 ② 피격 엄폐 찾기는 방어 유닛만 (중대 자세) · 참호 명중 0.5 ·
/// trenchPerTile 2 · 공격 사격 진지 0.6 ③ 보병포 수치 · 관측자 노출 지점 회피 · 야전포 도착 시 표적 쪽 방열.
/// 창 없이. KD_SELFTEST_ONLY=tweak1 로 단독 실행.
/// </summary>
public static class Tweak1SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("tweak1", 1, false, "standard");

    // 보병포 채택 수치 (ARTILLERY_EFFECT 'tweak1' 감도 시험)
    const float IgDamage = 70f, IgSplash = 1.5f, IgSuppress = 250f;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[tweak1 대열 정비 · 엄폐 · 포병 묶음]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestFireTeamExcluded();
            TestAssaultHold();
            TestCoverPosture();
            TestAiPosture();
            TestObserverExposure();
            TestFieldGunFacing();
        }
        catch (Exception e) { check(false, $"tweak1 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Ceiling(s * 60);

    /// <summary>소총 20 (10열) 중대 (장교 없음) 를 남쪽으로 20칸 옮겨 세운 상태. 앞 = +y.</summary>
    static (World W, Company C, List<Unit> Men) Stationed()
    {
        var w = Bare();
        var us = new List<Unit>();
        for (int i = 0; i < 20; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % 10 - 4.5f), 40 + i / 10)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        w.Move(1, us, new Vector2(60, 60));
        Steps(w, 2);
        for (int t = 0; t < 60 * 40 && !(c.Anchor == null && us.All(u => u.Order == UnitOrder.Idle)); t++) w.Step();
        return (w, c, us.OrderBy(u => u.Id).ToList());
    }

    static List<Unit> RearFirst(Company c, List<Unit> men) => men.OrderBy(u => Vector2.Dot(u.Pos, c.Facing)).ThenBy(u => u.Pos.X).ThenBy(u => u.Id).ToList();

    static void Fight(World w, Unit firer, int n, Action? each = null)
    {
        for (int i = 0; i < n; i++) { each?.Invoke(); firer.LastShotTick = w.Tick; w.Step(); }
    }

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var t = defs.Terrain;
        check(t.TrenchHit == 0.5f, $"참호 명중 배수 0.6 → 0.5 ({t.TrenchHit})");
        check(t.TrenchPerTile == 2, $"trenchPerTile 은 2 그대로 ({t.TrenchPerTile})");
        var prof = AiProfiles.Find("default")!;
        var agg = AiProfiles.Find("aggressive")!;
        check(MathF.Abs(prof.Tactics.EngageFrac - 0.6f) < 1e-4f && MathF.Abs(agg.Tactics.EngageFrac - 0.6f) < 1e-4f && prof.Tactics.EngageFrac < defs.CombatRules.EngageRangeFrac,
            $"공격 AI 사격 진지 engageFrac 0.65 → 0.6 (기본 {prof.Tactics.EngageFrac}, aggressive {agg.Tactics.EngageFrac}, 반응 거리 {defs.CombatRules.EngageRangeFrac} 안)");
        var ig = defs.Weapon("gun_infantry")!;
        check(ig.Damage == IgDamage && ig.Splash == IgSplash && ig.Suppress == IgSuppress,
            $"보병포 위력 (감도 시험 채택): 피해 {ig.Damage} (= {IgDamage}) · 반경 {ig.Splash} (= {IgSplash}) · 제압 {ig.Suppress} (= {IgSuppress})");
        var fg = defs.Weapon("gun_field")!;
        check(ig.Damage <= fg.Damage && ig.Splash <= fg.Splash, $"보병포는 야전포를 넘지 않음 (피해 {ig.Damage} ≤ {fg.Damage}, 반경 {ig.Splash} ≤ {fg.Splash})");
    }

    // ---------------------------------------------------------------- ① 대열 정비

    static void TestFireTeamExcluded()
    {
        // 뒤로 흩어진 6명 중 3명은 화력조 · 지원 역할 — 대열 정비 명령을 받아도 그 3명은 움직이지 않고, 소총 3명만 자리로
        var (w, c, men) = Stationed();
        var rear = RearFirst(c, men);
        var fire = new List<Unit>(); var rifle = new List<Unit>();
        for (int i = 0; i < 6; i++)
        {
            var u = rear[i];
            u.Pos -= c.Facing * 4f;
            if (i % 2 == 0) { u.Role = i == 0 ? UnitRole.Support : UnitRole.Fire; fire.Add(u); } else rifle.Add(u);
        }
        var at = fire.Select(u => u.Pos).ToArray();
        var atR = rifle.Select(u => u.Pos).ToArray();
        check(fire.All(u => !w.CloseCandidate(c, u, false)) && rifle.All(u => w.CloseCandidate(c, u, false)),
            "대열 정비 대상: 화력조 · 지원 역할 병사는 제외, 소총 병사는 대상");
        w.CloseRanks(1, men.Take(2));
        Fight(w, rear[^1], Sec(8));
        float fireMove = fire.Select((u, i) => Vector2.Distance(u.Pos, at[i])).Max();
        float rifleBack = rifle.Select((u, i) => Vector2.Dot(u.Pos - atR[i], c.Facing)).Min();
        check(fireMove < 0.3f && rifleBack > 2.5f, $"대열 정비 명령: 화력조 · 지원은 그 자리 (최대 이동 {fireMove:0.00}칸), 소총은 앞으로 {rifleBack:0.0}칸 이상 돌아감");
    }

    static void TestAssaultHold()
    {
        // 자기 중대가 돌격 중 (틀 돌격 Drill.Man 4) 이면 대열 정비 명령을 받아도 보내지 않고, 돌격이 끝난 뒤 명령은 그대로 따른다
        var (w, c, men) = Stationed();
        var rear = RearFirst(c, men);
        var moved = rear.Take(4).ToList();
        foreach (var u in moved) u.Pos -= c.Facing * 4f;
        var at = moved.Select(u => u.Pos).ToArray();
        c.Drill.Man = 4;
        check(w.CompanyAssaulting(c), "틀 돌격 (기동조 돌격 상태 4) 중 = 자기 돌격");
        long movers0 = w.Stats.CloseMovers;
        w.CloseRanks(1, men.Take(1));
        Fight(w, rear[^1], Sec(4), () => c.Drill.Man = 4);
        float held = moved.Select((u, i) => Vector2.Distance(u.Pos, at[i])).Max();
        check(held < 0.3f && w.Stats.CloseMovers == movers0, $"자기 돌격 중에는 대열 정비 보류 (보낸 병사 {w.Stats.CloseMovers - movers0}, 최대 이동 {held:0.00}칸)");
        c.Drill.Man = 0;
        // 명령 돌격 병사 (상태 2) 하나만 있어도 돌격 중, 역돌격 (3) 은 아님
        var x = rear[^2];
        x.ChargeState = 2;
        bool cmd = w.CompanyAssaulting(c);
        x.ChargeState = 3;
        bool counter = w.CompanyAssaulting(c);
        x.ChargeState = 0;
        check(cmd && !counter, $"명령 돌격 병사 = 돌격 중 ({cmd}), 역돌격 = 아님 ({counter})");
        // 돌격이 끝나면: 같은 명령 유효 시간 (20초) 안 다음 정비 또는 새 명령으로 자리 잡음
        if (!w.ClosingRanks(c)) w.CloseRanks(1, men.Take(1));
        Fight(w, rear[^1], Sec(8));
        float back = moved.Select((u, i) => Vector2.Dot(u.Pos - at[i], c.Facing)).Min();
        check(back > 2.5f && w.Stats.CloseMovers > movers0, $"돌격이 끝나면 대열 정비 (앞으로 {back:0.0}칸, 보낸 병사 {w.Stats.CloseMovers - movers0})");
    }

    // ---------------------------------------------------------------- ② 엄폐 찾기 = 방어 유닛만

    static void TestCoverPosture()
    {
        // 숲 3×3 (x 10..12, y 30..32) 바로 남쪽 개활지 사수 2명 중대, 북쪽 26칸 저격수가 쏨. 자세 Attack = 그 자리, Defend · 미지정 (플레이어 사수) = 숲으로
        foreach (var posture in new[] { CompanyPosture.Attack, CompanyPosture.Defend, CompanyPosture.None })
        {
            var w = Bare(64);
            for (int y = 30; y <= 32; y++) for (int x = 10; x <= 12; x++) w.Map.Set(x, y, TerrainType.Forest);
            var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(11.5f, 34.7f));
            var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(16.5f, 34.7f));
            a.Hp = b.Hp = 100000;
            w.FormCompany(1, new[] { a, b });
            w.Step();
            var c = a.Company!;
            if (posture != CompanyPosture.None) w.SetPosture(1, new[] { c.Id }, posture);
            w.Hold(1, new[] { a, b });
            var s = w.SpawnUnit(defs.Units["infantry_sniper"], 2, new Vector2(11.5f, 34.7f - 26f));
            s.Hp = 100000;
            w.Hold(2, new[] { s });
            Vector2 p0 = a.Pos;
            Steps(w, Sec(12));
            bool inCover = w.Map.TerrainCover(a.Pos).Hit < 1f;
            if (posture == CompanyPosture.Attack)
                check(c.Posture == CompanyPosture.Attack && !inCover && Vector2.Distance(a.Pos, p0) < 0.5f && w.Stats.CoverSeeks == 0 && w.Stats.CoverSeekSkipAttack > 0 && a.LastFiredAtTick > 0,
                    $"공격 자세 중대: 맞아도 엄폐로 빠지지 않음 (이동 {Vector2.Distance(a.Pos, p0):0.0}칸, 엄폐 찾기 {w.Stats.CoverSeeks}, 건너뜀 {w.Stats.CoverSeekSkipAttack})");
            else
                check(c.Posture == posture && inCover && a.Order == UnitOrder.Hold && w.Stats.CoverSeeks > 0,
                    $"{(posture == CompanyPosture.Defend ? "방어 자세" : "자세 미지정 (플레이어 사수)")} 중대: 피격 뒤 숲 칸으로 ({a.Pos.X:0.0},{a.Pos.Y:0.0}, 명령 {a.Order})");
        }
        // 미지정 중대라도 공격 틀 행동 (Engage) 중이면 엄폐 찾기 안 함, 틀 방어 (Defend) 이면 함
        {
            var w = Bare(64);
            var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.5f, 20.5f));
            var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(22.5f, 20.5f));
            w.FormCompany(1, new[] { a, b });
            w.Step();
            var c = a.Company!;
            c.Drill.Phase = DrillPhase.Engage;
            bool eng = World.DefendsForCover(a);
            c.Drill.Phase = DrillPhase.Defend;
            bool def = World.DefendsForCover(a);
            c.Drill.Phase = DrillPhase.Idle;
            c.Posture = CompanyPosture.Attack;
            c.Drill.Phase = DrillPhase.Defend;
            bool attDef = World.DefendsForCover(a);
            c.Drill.Phase = DrillPhase.Idle; c.Posture = CompanyPosture.None;
            check(!eng && def && attDef, $"방어 판정: 공격 틀 행동 중 {eng} · 틀 방어 국면 {def} · 공격 자세라도 틀 방어 국면 {attDef}");
            // 저장 · 체크섬: 자세가 저장되고 체크섬에 들어간다
            c.Posture = CompanyPosture.Defend;
            var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
            ulong h1 = w.ComputeChecksum();
            c.Posture = CompanyPosture.Attack;
            ulong h2 = w.ComputeChecksum();
            check(y.Companies.First(k => k.Id == c.Id).Posture == CompanyPosture.Defend && h1 != h2, "중대 자세: 저장 · 불러오기 유지, 체크섬에 포함");
        }
    }

    static void TestAiPosture()
    {
        // 전술 AI: 공격 과제 소총 중대 = Attack 자세, 방어 과제 = Defend (참호 진지 미션 B — 한국 공격 · 봉천 방어)
        var setup = new BattleSetup { SideA = "infantry:60,officer:2", SideB = "infantry:60,officer:2", Map = "flat", Distance = 90, AiVsAi = true, AiA = "tactical", AiB = "tactical:defensive" };
        var w = setup.CreateWorld(defs, 3);
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical:defensive")!));
        Steps(w, Sec(20));
        var ai1 = (TacticalAi)w.Controllers.First(k => k.Faction == 1).Ai;
        var ai2 = (TacticalAi)w.Controllers.First(k => k.Faction == 2).Ai;
        bool ok = true; int n = 0; var seen = new List<string>();
        foreach (var (ai, f) in new[] { (ai1, 1), (ai2, 2) })
        {
            var st = ai.StateOf(f);
            if (st == null) { ok = false; continue; }
            foreach (var g in st.Groups)
            {
                if (g.CompanyId == 0 || g.Kind != TacKind.Line || w.FindCompany(g.CompanyId) is not { } co) continue;
                var want = g.Task == TacTask.Attack ? CompanyPosture.Attack : g.Task is TacTask.Defend or TacTask.Reserve or TacTask.Fallback ? CompanyPosture.Defend : CompanyPosture.None;
                n++; seen.Add($"{f}:{g.Task}/{co.Posture}");
                if (co.Posture != want) ok = false;
            }
        }
        check(ok && n > 0, $"전술 AI 가 과제에 맞춰 중대 자세를 지정 ({string.Join(", ", seen.Distinct())})");
    }

    // ---------------------------------------------------------------- ③ 관측자 · 야전포

    static void TestObserverExposure()
    {
        // 관측반이 노려져 엄폐하면 노출 지점 · 사격자 위치를 기억하고, AI 는 같은 노출 지점으로 돌려보내지 않는다 (다른 엄폐 · 은폐 칸, 사격자에게서 더 멀리)
        var w = Bare(140);
        for (int fy = 50; fy <= 55; fy++) for (int fx = 40; fx <= 80; fx++) w.Map.Set(fx, fy, TerrainType.Forest);
        var obs = new List<Unit> { w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(60.5f, 61.5f)), w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(62.5f, 61.5f)) };
        w.FormCompany(1, obs);
        Steps(w, 3);
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(61.5f, 80.5f));
        Unit? o = null; Vector2 start = Vector2.Zero;
        for (int i = 0; i < 20 * 60 && o == null; i++)
        {
            var before = obs.ToDictionary(x => x.Id, x => x.Pos);
            w.Step();
            o = obs.FirstOrDefault(x => x.ScoutState == ScoutState.Cover);
            if (o != null) start = before[o.Id];
        }
        check(o != null && o.ScoutExposedTick > 0 && Vector2.Distance(o.ScoutExposed, start) < 1f && Vector2.Distance(o.ScoutThreat, foe.Pos) < 1.5f,
            $"노려져 엄폐: 노출 지점 · 사격자 위치 기억 (노출 {o?.ScoutExposed.X:0.0},{o?.ScoutExposed.Y:0.0} · 사격자 {o?.ScoutThreat.X:0.0},{o?.ScoutThreat.Y:0.0} · 틱 {o?.ScoutExposedTick})");
        if (o == null) return;
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        var oy = y.Find(o.Id) as Unit;
        check(oy != null && oy.ScoutExposed == o.ScoutExposed && oy.ScoutThreat == o.ScoutThreat && oy.ScoutExposedTick == o.ScoutExposedTick, "노출 기억: 저장 · 불러오기 유지");
        // AI 가 계산한 관측 지점이 노출 지점 그 자리여도 → 다른 칸
        var A = defs.Tactics.ArtilleryAi;
        float standoff = 24f * A.ObserverStandoff;
        Vector2 q = TacticalAi.TestAvoidExposure(w, o, o.ScoutExposed, standoff);
        float dX = Vector2.Distance(q, o.ScoutExposed), dT = Vector2.Distance(q, o.ScoutThreat), need = Vector2.Distance(o.ScoutExposed, o.ScoutThreat) + A.ObserverAvoidStandoff;
        bool hidden = w.Map.TerrainCover(q).Hit < 1f || !w.Map.HasFiringLine(o.ScoutThreat, q, 0, o.Id);
        check(dX >= A.ObserverAvoidRadius - 0.01f && dT >= need - 0.01f && hidden,
            $"같은 노출 지점으로 돌아가지 않음: 새 지점 ({q.X:0.0},{q.Y:0.0}) 노출 지점에서 {dX:0.0}칸 ≥ {A.ObserverAvoidRadius}, 사격자에서 {dT:0.0}칸 ≥ {need:0.0}, 엄폐 · 은폐 · 사선 밖 {hidden}");
        // 이미 멀고 숨은 지점이면 그대로
        Vector2 far = new(45.5f, 52.5f);   // 숲 안, 노출 지점 17칸 · 사격자 31칸
        Vector2 q2 = TacticalAi.TestAvoidExposure(w, o, far, standoff);
        check(q2 == far, $"노출 지점과 멀고 숨은 지점은 그대로 ({q2.X:0.0},{q2.Y:0.0})");
    }

    static void TestFieldGunFacing()
    {
        // 야전포: 도착해 방열할 때 표적 쪽으로 (마지막 이동 방향이 아니라) — 첫 방열 때 가장 가까운 보이는 적이 사계 안
        var setup = new BattleSetup { SideA = "infantry:60,officer:2,heavy_field_gun:3", SideB = "infantry:60,officer:2", Map = "flat", Distance = 100, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 4);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        var guns = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_field_gun").ToList();
        var first = new Dictionary<int, bool>();
        int shots = 0;
        for (int i = 0; i < 150 * 60 && w.Winner == 0 && first.Count < guns.Count; i++)
        {
            w.Step();
            foreach (var g in guns)
            {
                if (!g.Alive || first.ContainsKey(g.Id) || !g.Deployed) continue;
                var vis = w.Units.Where(e => e.Owner == 2 && e.Alive && w.Fog.CanSee(1, e) && w.Map.HasFiringLine(g.Pos, e.Pos, g.Id, e.Id)).ToList();
                if (vis.Count == 0) continue;
                Vector2 tp = g.Target is Unit t && t.Alive ? t.Pos : vis.OrderBy(e => Vector2.DistanceSquared(e.Pos, g.Pos)).ThenBy(e => e.Id).First().Pos;
                first[g.Id] = World.CanTraverseInPlace(g, tp) && Vector2.Distance(tp, g.Pos) <= g.Weapon!.Range;
            }
        }
        for (int i = 0; i < 30 * 60 && w.Winner == 0 && !guns.Any(g => g.LastShotTick > 0); i++) w.Step();
        foreach (var g in guns) if (g.LastShotTick > 0) shots++;
        var st = ((TacticalAi)w.Controllers.First(k => k.Faction == 1).Ai).StateOf(1);
        int faced = first.Values.Count(v => v);
        check(first.Count >= 2 && faced * 3 >= first.Count * 2 && (st?.GunFaceDeploys ?? 0) >= 1 && shots >= 1,
            $"야전포 도착 방열 = 표적 쪽: 첫 방열 때 표적이 사계 안 {faced}/{first.Count}, 도착 공격 명령 {st?.GunFaceDeploys ?? 0}회, 쏜 포 {shots}");
    }
}
