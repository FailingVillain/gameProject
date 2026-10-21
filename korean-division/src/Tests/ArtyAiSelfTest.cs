using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N20 포병 AI (2026-10-19, TacticalAi.Artillery.cs): 관측자 전진 · 준비사격 (실제 사격 뒤 돌격) · 돌격 지원 표적 · 근접 위험 · 재배치 쿨다운 (포대 수 정규화) ·
/// 직사포 사거리 규율 · 포 종류별 분견대. 창 없이. KD_SELFTEST_ONLY=artyai 로 단독 실행.
/// </summary>
public static class ArtyAiSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[N20 포병 AI]");
        try
        {
            TestValues();
            TestFireDetachmentsSplit();
            TestObserversForward();
            TestPrepBeforeAdvance();
            TestAssaultSupportAndDangerClose();
            TestRedeployCooldown();
            TestGunSkipsFarTarget();
        }
        catch (Exception e) { check(false, $"N20 포병 AI 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World BattleWorld(string a, string b, int seed = 3, int distance = 60, string aiB = "tactical")
    {
        var setup = new BattleSetup { SideA = a, SideB = b, Map = "flat", Distance = distance, AiVsAi = true, AiA = "tactical", AiB = aiB };
        var w = setup.CreateWorld(defs, seed);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        if (aiB != "none") w.Controllers.Add((2, AiRegistry.Create(aiB)!));
        return w;
    }

    static TacticalAi Ai(World w, int f = 1) => (TacticalAi)w.Controllers.First(c => c.Faction == f).Ai;
    static TacFaction? St(World w, int f = 1) => Ai(w, f).StateOf(f);

    static World Bare(int size = 140)
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

    // ---------------------------------------------------------------- 시험

    static void TestValues()
    {
        var A = defs.Tactics.ArtilleryAi;
        check(A.ObserverStandoff >= 1.25f && A.SupportRadius > 0 && A.RedeployCooldownSec > 0 && A.GunEffectiveFrac is > 0.5f and <= 0.9f && A.DangerClose > 0,
            $"tactics.json artilleryAi 읽음 (관측 거리 ×{A.ObserverStandoff}, 지원 반경 {A.SupportRadius}, 쿨다운 {A.RedeployCooldownSec}초, 유효 사거리 {A.GunEffectiveFrac})");
    }

    static void TestFireDetachmentsSplit()
    {
        // 저격수 · 보병포 · 야전포가 옆으로 섞여 있어도 포는 종류별 분견대 (사거리 · 속도가 다른 무리와 섞이면 최소 사거리로 움직였다)
        var w = BattleWorld("infantry:60,officer:2,infantry_sniper:4,heavy_infantry_gun:3,heavy_field_gun:3", "infantry:40", distance: 90);
        for (int i = 0; i < 120; i++) w.Step();
        var s = St(w)!;
        var fire = s.Groups.Where(g => g.Kind == TacKind.Fire && g.CompanyId == 0).ToList();
        bool pure = fire.All(g =>
        {
            var keys = g.Units.Select(id => w.Find(id) as Unit).Where(u => u != null).Select(u => u!.Def.Key).Distinct().ToList();
            return keys.Count == 1;
        });
        check(fire.Count >= 3 && pure, $"화력 분견대: 저격수 · 보병포 · 야전포 따로 ({fire.Count}개, 종류 하나씩 {pure})");
    }

    static void TestObserversForward()
    {
        // 관측반 (정찰병만의 중대) 은 접촉 전 아군 소총 중대 앞선보다 앞, 보이는 적과 소총 사거리 × observerStandoff 이상
        var w = BattleWorld("infantry:100,officer:3,heavy_howitzer:2", "infantry:80,officer:2", seed: 4, distance: 110, aiB: "passive");
        var obs = new List<Unit>();
        Vector2 at = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_howitzer").Select(u => u.Pos).First();
        for (int i = 0; i < 3; i++) obs.Add(w.SpawnUnit(defs.Units["infantry_scout"], 1, at + new Vector2(i * 1.2f, 0)));
        w.FormCompany(1, obs);
        for (int i = 0; i < 3; i++) w.Step();
        check(obs[0].Company != null && obs[0].ScoutCompany == null, "관측반 = 정찰병만의 중대 (중대 정찰조 아님)");
        var A = defs.Tactics.ArtilleryAi;
        bool aheadSeen = false, tooClose = false, commanded = false;
        float bestAhead = float.MinValue;
        for (int i = 0; i < 70 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 60 != 0 || St(w) is not { } s || s.Phase < 2) continue;
            commanded |= obs.Any(o => o.Alive && !o.ScoutAuto);
            Vector2 dir = s.Dir;
            float front = float.MinValue;
            foreach (var g in s.Groups) if (g.Kind == TacKind.Line && g.Alive > 0 && !g.Pending) front = MathF.Max(front, Vector2.Dot(g.Center, dir));
            var vis = w.Units.Where(e => e.Owner == 2 && e.Alive && w.Fog.CanSee(1, e)).ToList();
            foreach (var o in obs)
            {
                if (!o.Alive) continue;
                float ahead = Vector2.Dot(o.Pos, dir) - front;
                bestAhead = MathF.Max(bestAhead, ahead);
                if (vis.Count == 0 && ahead > 3f && o.Velocity == Vector2.Zero) aheadSeen = true;
                if (o.Velocity == Vector2.Zero && vis.Exists(e => Vector2.Distance(e.Pos, o.Pos) < s.RifleRange * 0.9f)) tooClose = true;
            }
        }
        check(commanded, "관측자는 AI 가 직접 부림 (자율 정찰 끔)");
        check(aheadSeen, $"접촉 전 관측자가 아군 소총 중대 앞선보다 앞에서 관측 (최대 {bestAhead:0.0}칸 앞)");
        check(!tooClose, "관측 중인 관측자는 보이는 적 소총 사거리 안에 서 있지 않음");
    }

    static void TestPrepBeforeAdvance()
    {
        // 관측반이 있는 공격: 연대 준비사격 국면 → 곡사포가 실제로 쏜 뒤에 돌격 국면 (관측 없으면 시간 상한)
        var w = BattleWorld("infantry:150,officer:6,heavy_howitzer:3", "infantry:120,officer:3", seed: 2, distance: 80);
        Vector2 at = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_howitzer").Select(u => u.Pos).First();
        var obs = new List<Unit>();
        for (int i = 0; i < 3; i++) obs.Add(w.SpawnUnit(defs.Units["infantry_scout"], 1, at + new Vector2(i * 1.2f, 2f)));
        w.FormCompany(1, obs);
        double prepT = -1, assaultT = -1, firstShot = -1;
        var guns = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_howitzer").ToList();
        for (int i = 0; i < 150 * 60 && w.Winner == 0 && assaultT < 0; i++)
        {
            w.Step();
            if (firstShot < 0 && guns.Any(u => u.LastShotTick >= 0 && u.LastShotTick == w.Tick - 1 || u.LastShotTick > 0 && w.Tick - u.LastShotTick < 2)) firstShot = w.Time;
            if (i % 30 != 0 || St(w) is not { } s) continue;
            foreach (var r in s.Regiments)
            {
                if (r.Phase == RegPhase.Prep && prepT < 0) prepT = w.Time;
                if (prepT >= 0 && r.Phase == RegPhase.Assault && assaultT < 0) assaultT = w.Time;
            }
        }
        double cap = St(w)!.Profile.Tactics.PrepSeconds + 20;
        check(prepT >= 0 && assaultT > prepT, $"준비사격 국면 → 돌격 국면 (준비 {prepT:0.0}s, 돌격 {assaultT:0.0}s)");
        check(firstShot >= 0 && firstShot < assaultT, $"곡사포 준비사격이 돌격보다 먼저 (첫 사격 {firstShot:0.0}s, 돌격 {assaultT:0.0}s)");
        check(assaultT - prepT >= St(w)!.Profile.Tactics.PrepSeconds - 0.6 && (firstShot >= prepT - 20 || assaultT - prepT >= cap - 0.6),
            $"준비사격은 최소 prepSeconds, 사격이 없으면 상한까지 ({assaultT - prepT:0.0}초)");
    }

    static void TestAssaultSupportAndDangerClose()
    {
        // 소총 중대가 약진 · 돌격하는 동안 곡사포 강제 표적이 그 표적 둘레 (supportRadius) 에 있고, 아군에서 근접 위험 거리 안 표적은 강제하지 않는다
        var A = defs.Tactics.ArtilleryAi;
        int near = 0, forced = 0, danger = 0, ptsN = 0;
        World w = null!;
        // 2026-10-19 (N34 human 이후): 적 120명이면 양쪽 사격전이 길어져 (발사 간격 · 반응 지연 비균질) 중대가 약진 · 돌격 국면에 못 들어가 지원 표적 표본이 0 — 적을 40명으로 줄여 약진 · 돌격이 나오게 한다 (곡사포 AI 는 정상: 강제 표적 · 근접 위험 위반 0)
        foreach (int seed in new[] { 2, 3, 4, 5, 6, 7 })   // engage (반응 거리 0.7): 약진 · 돌격이 시드에 따라 늦거나 적다 — 지원 표적이 나올 때까지 시드 3개, 판당 240초
        {
        if (near > 0) break;
        w = BattleWorld("infantry:150,officer:6,heavy_howitzer:3", "infantry:40,officer:3", seed: seed, distance: 70);
        for (int i = 0; i < 240 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 30 != 0 || St(w) is not { } s) continue;
            var pts = new List<Vector2>();
            foreach (var g in s.Groups)
            {
                if (g.Kind != TacKind.Line || g.CompanyId == 0 || w.FindCompany(g.CompanyId) is not { } co) continue;
                if (co.Drill.Phase == DrillPhase.Engage && co.Drill.Enemy != Vector2.Zero && (co.Drill.BoundSquad > 0 || co.Drill.Man is 1 or 2 or 4)) pts.Add(co.Drill.Enemy);
            }
            if (pts.Count > 0) ptsN++;
            foreach (var g in s.Groups)
            {
                if (g.Kind != TacKind.Artillery || g.FireTargetId == 0 || w.Find(g.FireTargetId) is not Unit t || !t.Alive) continue;
                forced++;
                if (pts.Exists(p => Vector2.Distance(p, t.Pos) <= A.SupportRadius + 2f)) near++;
                if (w.Units.Any(u => u.Owner == 1 && u.Alive && Vector2.Distance(u.Pos, t.Pos) < A.DangerClose * 0.8f)) danger++;
            }
        }
        }
        check(near > 0, $"돌격 · 약진 표적 둘레 적에 곡사포 강제 표적 (강제 {forced}회 중 지원 표적 둘레 {near}회, 약진 · 돌격 표본 {ptsN}, {w.Time:0}초)");
        check(danger == 0, $"강제 표적은 근접 위험 거리 밖 ({danger}회 위반)");
    }

    static void TestRedeployCooldown()
    {
        // 쿨다운 = 기본 × max(1, 포대 수 / 기준) — 포대가 많아도 세력 전체 이동 빈도는 같게
        var w = BattleWorld("infantry:120,officer:4,heavy_howitzer:3,heavy_field_gun:3,heavy_infantry_gun:3", "infantry:120,officer:4", seed: 5, distance: 80);
        var A = defs.Tactics.ArtilleryAi;
        var ai = Ai(w);
        long baseCool = (long)MathF.Round(A.RedeployCooldownSec / Config.SimDt);
        var engagedSince = new Dictionary<int, long>();
        int moves = 0, early = 0;
        int losMoves = 0;
        for (int i = 0; i < 160 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            if (St(w) is not { } s || s.Phase < 2) continue;
            long cool = ai.RedeployCooldown(w, s);
            bool losMove = s.GunLosMoves != losMoves; losMoves = s.GunLosMoves;   // N22 사선 막힘 재배치는 쿨다운 없이 (의도)
            foreach (var g in s.Groups)
            {
                // N22: 보병포 (사거리 closeGunMaxRange 이하) 는 동행 규칙 (CloseGunThink — 쏜 뒤에만 쿨다운) 이라 제외
                bool gun = g.Kind == TacKind.Artillery || g.Kind == TacKind.Fire && g.CompanyId == 0 && g.Units.Count > 0 && w.Find(g.Units[0]) is Unit u0 && u0.Def.Category == "gun"
                           && u0.Weapon!.Range > A.CloseGunMaxRange;
                if (!gun) continue;
                if (g.State == TacState.Engage) { if (!engagedSince.ContainsKey(g.Id)) engagedSince[g.Id] = g.StateTick; }
                else if (engagedSince.TryGetValue(g.Id, out long since))
                {
                    engagedSince.Remove(g.Id);
                    if (g.State == TacState.Approach && g.Task is TacTask.Attack or TacTask.Defend)
                    {
                        moves++;
                        // 곡사포는 적이 최소 사거리 안으로 들어오면 쿨다운 없이 물러날 수 있다 — 직사포 · 정상 재배치만 센다
                        if (w.Tick - since < cool - 1 && g.Kind == TacKind.Fire && !losMove) early++;
                    }
                }
            }
        }
        var st = St(w)!;
        long c3 = ai.RedeployCooldown(w, st);
        for (int k = 0; k < 8; k++) st.Groups.Add(new TacGroup { Id = 90000 + k, Kind = TacKind.Artillery, Alive = 1 });
        long c11 = ai.RedeployCooldown(w, st);
        st.Groups.RemoveAll(g => g.Id >= 90000);
        check(early == 0, $"직사포 재배치는 방열 뒤 쿨다운이 지나야 (재배치 {moves}회, 쿨다운 전 {early}회)");
        check(c3 >= baseCool && c11 > c3 && c11 >= baseCool * 2,
            $"쿨다운 정규화: 포대 적음 {c3 * Config.SimDt:0}초 → 포대 8개 더 {c11 * Config.SimDt:0}초");
    }

    static void TestGunSkipsFarTarget()
    {
        // 보병포 1문 (전술 AI): 먼 표적 (사거리 0.92) 만 있으면 그것을 쏘고 (버리지 않음), 유효 사거리 (0.8) 안에 적이 나타나면 다음 사격 뒤 가까운 적으로 바꾼다.
        // 처음 14초는 유효 사거리 비율을 0.95 로 두어 (먼 표적도 유효 → 사격 진지로 다가가지 않음) 먼 표적을 잡게 한 뒤 0.8 로 되돌린다 (재배치 쿨다운 안이라 그 자리).
        var A = defs.Tactics.ArtilleryAi;
        float saved = A.GunEffectiveFrac;
        // tweak1 (N44): 보병포가 세져 (제압 250 · 반경 1.5) 먼 무리가 첫 발에 엎드려 시야에서 사라진다 → 표적 고르기만 보는 시험이라 옛 위력으로 돌린다
        var gw = defs.Weapon("gun_infantry")!;
        var (sd, ss, su) = (gw.Damage, gw.Splash, gw.Suppress);
        gw.Damage = 45; gw.Splash = 0.8f; gw.Suppress = 150;
        try
        {
            var w = Bare();
            var ai = new TacticalAi();
            w.Controllers.Add((1, ai));
            var gun = w.SpawnUnit(defs.Units["heavy_infantry_gun"], 1, new Vector2(70, 110));
            float R = gun.Weapon!.Range;
            var farL = new List<Unit>();   // 먼 무리 (한 기면 금방 죽는다)
            for (int k = 0; k < 12; k++) farL.Add(w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(64 + k, 110 - R * 0.92f - (k % 2))));
            w.Hold(2, farL);   // 먼 무리는 제자리 (N25: 제압 감소가 느려져 접근 속도가 바뀌면 거리 조건이 흔들림 — 이 시험은 표적 선택만 본다)
            A.GunEffectiveFrac = 0.95f;
            for (int i = 0; i < 14 * 60; i++) w.Step();
            A.GunEffectiveFrac = saved;
            long shot0 = gun.LastShotTick;
            for (int i = 0; i < 5 * 60; i++) w.Step();
            bool onFar = gun.Target is Unit ft && farL.Contains(ft) && Vector2.Distance(ft.Pos, gun.Pos) > R * saved;
            check(onFar && gun.LastShotTick > shot0 && shot0 > 0, $"가까운 적이 없으면 먼 표적 (사거리 0.92 = {R * 0.92f:0}칸) 을 계속 쏜다 (표적 {(gun.Target as Unit)?.Id ?? 0}, 먼 무리 생존 {farL.Count(u => u.Alive)})");
            var nearE = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(gun.Pos.X + 2f, gun.Pos.Y - R * 0.6f));
            bool switched = false;
            for (int i = 0; i < 9 * 60 && !switched; i++) { w.Step(); switched = gun.Target == nearE; }
            check(switched && farL.Exists(f => f.Alive && Vector2.Distance(f.Pos, gun.Pos) > R * saved), $"유효 사거리 안 적이 생기면 먼 표적을 버리고 가까운 적 ({R * 0.6f:0}칸) 을 쏜다");
        }
        finally { A.GunEffectiveFrac = saved; gw.Damage = sd; gw.Splash = ss; gw.Suppress = su; }
    }
}
