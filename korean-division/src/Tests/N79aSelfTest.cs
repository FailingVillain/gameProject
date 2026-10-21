using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N79a 집결 지점 · 포격 종류 (Rally.cs · GroundFire.cs): 집결 지점 지정 (자기 중대만 · 리플레이 기록), 후퇴 → 집결 지점, 집결 지점 없는 중대는 기존 목표,
/// 준비 사격 N발 뒤 끝, 이동탄막 착탄선 전진, 연막 (N59 구현 — 곡사포 연막 실행), 탄약 한도, 지속 사격 그대로, 저장 · 체크섬 · 명령 직렬화. `KD_SELFTEST_ONLY=n79a`.
/// </summary>
public static class N79aSelfTest
{
    static readonly SnapshotMeta Meta = new("n79a", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> check) => LegacyDeploy.Wrap(() => RunImpl(opt, check));

    static void RunImpl(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[N79a 집결 지점 · 포격 종류]");
        try
        {
            using var _ = TestDefs.NoCommandDelay();
            TestRally(check);
            TestRallyRetreat(check);
            TestPrep(opt, check);
            TestBarrage(opt, check);
            TestSmoke(opt, check);
            TestAmmoCap(opt, check);
            TestSustained(opt, check);
            TestSaveLoad(opt, check);
            TestSerialization(check);
        }
        catch (Exception e) { check(false, $"n79a 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 120)
    {
        var defs = Defs.Current;
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = true };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    static Company MakeCompany(World w, Vector2 at, int faction = 1)
    {
        var us = new List<Unit>();
        for (int i = 0; i < 10; i++) us.Add(w.SpawnUnit(Defs.Current.Units["infantry"], faction, at + new Vector2(i % 5, i / 5)));
        us.Add(w.SpawnUnit(Defs.Current.Units["officer"], faction, at + new Vector2(2, 3)));
        w.FormCompany(faction, us);
        w.Step();
        return us[0].Company!;
    }

    static Vector2 Cen(IEnumerable<Unit> us) { var l = us.ToList(); return l.Aggregate(Vector2.Zero, (s, u) => s + u.Pos) / l.Count; }

    // ---------------------------------------------------------------- 집결 지점

    static void TestRally(Action<bool, string> check)
    {
        var w = Bare();
        var c = MakeCompany(w, new Vector2(60, 60));
        var p = new Vector2(30.5f, 80.5f);
        w.SetRally(2, new[] { c.Id }, p);   // 남의 중대 — 무시
        w.Step();
        bool foreign = !c.HasRally;
        w.SetRally(1, new[] { c.Id }, new Vector2(-5, 9999));   // 지도 밖 — 무시
        w.Step();
        bool outside = !c.HasRally;
        w.SetRally(1, new[] { c.Id }, p);
        w.Step();
        bool set = c.HasRally && c.RallyPoint == p;
        bool logged = w.CommandLog.Any(k => k.Command is SetRallyCommand { Clear: false } sc && sc.Companies.Contains(c.Id) && sc.Point == p);
        w.SetRally(1, new[] { c.Id }, Vector2.Zero, clear: true);
        w.Step();
        check(foreign && outside && set && logged && !c.HasRally, $"집결 지점 지정: 남의 중대 무시 {foreign} · 지도 밖 무시 {outside} · 지정 {set} · 리플레이 기록 {logged} · 해제 {!c.HasRally}");
    }

    static void TestRallyRetreat(Action<bool, string> check)
    {
        var w = Bare();
        var a = MakeCompany(w, new Vector2(40, 60));
        var b = MakeCompany(w, new Vector2(80, 60));
        var rally = new Vector2(40.5f, 100.5f);
        var def = new Vector2(80.5f, 100.5f);
        w.SetRally(1, new[] { a.Id }, rally);
        w.Step();
        // 후퇴 명령 (집결 지점 따름): a 는 집결 지점으로, b (지점 없음) 는 명령 목표로
        w.Retreat(1, a.Members.Concat(b.Members), def, null, true);
        Steps(w, 60 * 90);
        float da = Vector2.Distance(Cen(a.Members.Where(u => !u.IsOfficer)), rally);
        float db = Vector2.Distance(Cen(b.Members.Where(u => !u.IsOfficer)), def);
        check(da < 8f && db < 8f, $"후퇴 → 집결 지점: 지점 있는 중대는 집결 지점 ({da:0.0}칸 이내), 없는 중대는 명령 목표 ({db:0.0}칸 이내)");
        // useRally 꺼짐 (전술 AI 의 일반 후퇴) = 집결 지점 무시
        var w2 = Bare();
        var c2 = MakeCompany(w2, new Vector2(40, 60));
        w2.SetRally(1, new[] { c2.Id }, rally);
        w2.Step();
        w2.Retreat(1, c2.Members, def);
        Steps(w2, 60 * 90);
        float d2 = Vector2.Distance(Cen(c2.Members.Where(u => !u.IsOfficer)), def);
        check(d2 < 8f, $"useRally 꺼진 후퇴는 집결 지점을 무시 (명령 목표까지 {d2:0.0}칸)");
        // 플레이어 '집결 지점으로 후퇴'
        var w3 = Bare();
        var c3 = MakeCompany(w3, new Vector2(40, 60));
        var c4 = MakeCompany(w3, new Vector2(80, 60));
        int none = w3.RetreatToRally(1, c3.Members);
        w3.SetRally(1, new[] { c3.Id }, rally);
        w3.Step();
        int n = w3.RetreatToRally(1, c3.Members.Concat(c4.Members));
        Vector2 c4start = Cen(c4.Members);
        Steps(w3, 60 * 90);
        float d3 = Vector2.Distance(Cen(c3.Members.Where(u => !u.IsOfficer)), rally);
        bool c4still = Vector2.Distance(Cen(c4.Members), c4start) < 2f;
        check(none == 0 && n == 1 && d3 < 8f && c4still, $"집결 지점으로 후퇴 (UI): 지점 없으면 0 ({none}) · 지점 있는 중대만 {n} · 도착 {d3:0.0}칸 · 지점 없는 중대는 그대로 {c4still}");
    }

    // ---------------------------------------------------------------- 포격 종류

    static World ArtyWorld(int seed) => TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(100, 100, seed)), Defs.Current));

    static (World W, Unit Gun, Vector2 C) Battery(int seed)
    {
        var w = ArtyWorld(seed);
        w.Fog.Enabled = false;
        var (cx, cy) = w.Map.NearestWalkable(w.Map.Width / 2, w.Map.Height / 2);
        var c = new Vector2(cx + 0.5f, cy + 0.5f);
        var gun = w.SpawnUnit(Defs.Current.Units["heavy_howitzer"], 1, c - new Vector2(10, 0));
        var dummy = w.SpawnUnit(Defs.Current.Units["infantry"], 2, c + new Vector2(30, 20));
        dummy.Weapon = null; w.Hold(2, new[] { dummy }); dummy.Hp = 100000;
        w.Hold(1, new[] { gun });
        w.Step();
        return (w, gun, c);
    }

    /// <summary>n 틱 동안 이 포의 착탄 지점들 (Impacts 는 몇 틱 뒤 지워지므로 매 틱 센다).</summary>
    static List<(long Tick, Vector2 P)> Impacts(World w, Unit u, int ticks)
    {
        var res = new List<(long, Vector2)>();
        for (int i = 0; i < ticks; i++)
        {
            w.Step();
            foreach (var im in w.Impacts) if (im.ShooterId == u.Id && im.Tick == w.Tick - 1) res.Add((im.Tick, im.Impact));
        }
        return res;
    }

    static void TestPrep(GameOptions opt, Action<bool, string> check)
    {
        var cr = Defs.Current.CombatRules;
        int old = cr.PrepRounds;
        cr.PrepRounds = 3;
        try
        {
            var (w, gun, c) = Battery(opt.Seed + 11);
            var target = c + new Vector2(10, 0);
            w.GroundFire(1, new[] { gun }, target, GroundFireKind.Prep);
            var hits = Impacts(w, gun, 60 * 400);
            bool done = gun.Order != UnitOrder.GroundFire;
            check(hits.Count == 3 && done && gun.GroundRounds == 0, $"준비 사격: {cr.PrepRounds}발을 쏘고 끝 (착탄 {hits.Count}발 · 명령 끝 {done})");
        }
        finally { cr.PrepRounds = old; }
    }

    static void TestBarrage(GameOptions opt, Action<bool, string> check)
    {
        var (w, gun, c) = Battery(opt.Seed + 12);
        var cr = Defs.Current.CombatRules;
        var start = c + new Vector2(5, 0);
        w.GroundFire(1, new[] { gun }, start, GroundFireKind.Barrage, new Vector2(1, 0));
        Steps(w, 3);
        bool ordered = gun.Order == UnitOrder.GroundFire && gun.GroundKind == GroundFireKind.Barrage && MathF.Abs(gun.GroundDir.X - 1f) < 1e-4f;
        var hits = Impacts(w, gun, 60 * 160);
        bool ended = gun.Order != UnitOrder.GroundFire;
        float scatter = World.ScatterAt(Defs.Current.Weapons["howitzer_heavy"], 20f) * cr.GroundScatterMul + 3f;
        // 착탄 중심이 시간에 따라 +X 로 전진: 앞 절반 평균 < 뒤 절반 평균
        double early = hits.Count >= 4 ? hits.Take(hits.Count / 2).Average(h => h.P.X) : 0;
        double late = hits.Count >= 4 ? hits.Skip(hits.Count - hits.Count / 2).Average(h => h.P.X) : 0;
        bool advance = late - early > cr.BarrageStepTiles * 2;
        bool bounded = hits.All(h => h.P.X <= start.X + cr.BarrageMaxTiles + scatter);
        check(ordered && advance && bounded && ended, $"이동탄막: 착탄선이 진행 방향으로 전진 (앞 {early:0.0} → 뒤 {late:0.0}, {hits.Count}발) · 최대 전진 안 · 끝남 {ended}");
        // 이동탄막 중 정지 명령 = 취소
        var (w2, gun2, c2) = Battery(opt.Seed + 12);
        w2.GroundFire(1, new[] { gun2 }, c2 + new Vector2(5, 0), GroundFireKind.Barrage, new Vector2(1, 0));
        Steps(w2, 60 * 5);
        w2.Stop(1, new[] { gun2 });
        Steps(w2, 3);
        check(gun2.Order != UnitOrder.GroundFire && gun2.GroundKind == GroundFireKind.Sustained && gun2.GroundDir == Vector2.Zero, "이동탄막 취소: 정지 명령 = 종류 상태까지 해제");
    }

    static void TestSmoke(GameOptions opt, Action<bool, string> check)
    {
        // N59 (참호 돌파 묶음): 연막 종류가 구현됨 — 연막탄 있는 곡사포는 breach.smokeRounds 발을 쏘고 끝 (상세는 BreachSelfTest)
        var (w, gun, c) = Battery(opt.Seed + 13);
        int log0 = w.CommandLog.Count;
        w.GroundFire(1, new[] { gun }, c + new Vector2(10, 0), GroundFireKind.Smoke);
        Steps(w, 5);
        check(gun.Order == UnitOrder.GroundFire && gun.GroundKind == GroundFireKind.Smoke && gun.GroundRounds == Defs.Current.Breach.SmokeRounds && w.CommandLog.Count == log0 + 1,
            $"연막: 곡사포 연막 지점 포격 실행 (명령 {gun.Order} · 종류 {gun.GroundKind} · 발수 {gun.GroundRounds})");
        check(!string.IsNullOrEmpty(KoreanDivision.UI.S.FireKindSmokeNone) && KoreanDivision.UI.S.FireKindSmokeNone.Contains("연막탄"), "연막 안내 문구 (Strings)");
    }

    static void TestAmmoCap(GameOptions opt, Action<bool, string> check)
    {
        var (w, gun, c) = Battery(opt.Seed + 14);
        if (!w.AmmoEnabled) { check(true, "탄약 한도: 이 세계는 탄약 꺼짐 — 건너뜀"); return; }
        gun.Ammo = 2;
        w.GroundFire(1, new[] { gun }, c + new Vector2(10, 0), GroundFireKind.Prep);
        var hits = Impacts(w, gun, 60 * 400);
        check(hits.Count == 2 && gun.Order != UnitOrder.GroundFire, $"탄약 한도: 준비 사격 {Defs.Current.CombatRules.PrepRounds}발 중 탄약 2발뿐 → 2발 뒤 끝 (착탄 {hits.Count})");
    }

    static void TestSustained(GameOptions opt, Action<bool, string> check)
    {
        var (w, gun, c) = Battery(opt.Seed + 15);
        w.GroundFire(1, new[] { gun }, c + new Vector2(10, 0));   // 종류 생략 = 지속 (기존 GroundFireCommand)
        var hits = Impacts(w, gun, 60 * 120);
        bool logged = w.CommandLog.Any(k => k.Command is GroundFireCommand);
        check(hits.Count >= 4 && gun.Order == UnitOrder.GroundFire && gun.GroundKind == GroundFireKind.Sustained && logged, $"지속 사격: 120초 뒤에도 계속 (착탄 {hits.Count}발 · 기존 명령 {logged})");
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 · 직렬화

    static void TestSaveLoad(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var (x, gun, c) = Battery(opt.Seed + 16);
        var co = MakeCompany(x, new Vector2(20, 20));
        x.SetRally(1, new[] { co.Id }, new Vector2(15.5f, 30.5f));
        x.GroundFire(1, new[] { gun }, c + new Vector2(5, 0), GroundFireKind.Barrage, new Vector2(1, 0));
        Steps(x, 60 * 20);
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        var yg = y.Units.First(u => u.Id == gun.Id);
        var yc = y.Companies.First(k => k.Id == co.Id);
        bool fields = yg.GroundKind == GroundFireKind.Barrage && yg.GroundDir == gun.GroundDir && yg.GroundStart == gun.GroundStart && yc.HasRally && yc.RallyPoint == co.RallyPoint;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes)) && x.ComputeChecksum() == y.ComputeChecksum();
        var h0 = x.ComputeChecksum();
        co.RallyPoint += new Vector2(1, 0); bool h1 = x.ComputeChecksum() != h0; co.RallyPoint -= new Vector2(1, 0);
        gun.GroundRounds++; bool h2 = x.ComputeChecksum() != h0; gun.GroundRounds--;
        bool cont = true;
        for (int i = 0; i < 60 * 30; i++)
        {
            x.Step(); y.Step();
            if (i % 30 == 29 && x.ComputeChecksum() != y.ComputeChecksum()) { cont = false; break; }
        }
        check(fields && same && h1 && h2 && cont, $"N79a 저장: 필드 복원 {fields} · 바이트 · 체크섬 동일 {same} · 체크섬 반영 (집결 {h1} · 포격 {h2}) · 30초 이어 돌려도 같음 {cont} (형식 {Snapshot.FormatVersion})");
    }

    static void TestSerialization(Action<bool, string> check)
    {
        GameCommand a = new SetRallyCommand(1, new[] { 3, 4 }, new Vector2(10.5f, 20.5f), false);
        GameCommand b = new GroundFireExCommand(1, new[] { 5 }, new Vector2(1, 2), GroundFireKind.Barrage, new Vector2(0, 1));
        GameCommand r = new RetreatCommand(1, new[] { 7 }, new Vector2(3, 4), null, true);
        var ja = System.Text.Json.JsonSerializer.Serialize(a, CommandJson.Options);
        var jb = System.Text.Json.JsonSerializer.Serialize(b, CommandJson.Options);
        var jr = System.Text.Json.JsonSerializer.Serialize(r, CommandJson.Options);
        var ba = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(ja, CommandJson.Options);
        var bb = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(jb, CommandJson.Options);
        var br = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(jr, CommandJson.Options);
        // 옛 리플레이 (UseRally 없음) 는 기본값 (켜짐) — 집결 지점이 없던 때라 영향 없음
        var old = System.Text.Json.JsonSerializer.Deserialize<GameCommand>("{\"$t\":\"retreat\",\"Faction\":1,\"Units\":[1],\"Target\":[1,2]}", CommandJson.Options);
        check(ba is SetRallyCommand { Point.X: 10.5f, Clear: false } && bb is GroundFireExCommand { Kind: GroundFireKind.Barrage } && br is RetreatCommand { UseRally: true } && old is RetreatCommand && ReplayFile.FormatVersion >= 16,
            $"N79a 명령 직렬화 (rally · groundfireex · retreat UseRally), 리플레이 형식 {ReplayFile.FormatVersion}");
    }
}
