using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 참호 돌파 묶음 (N59 연막 · 철조망 절단, N60 경박격포, N65 연막 수류탄 — Breach.cs): 연막이 사선 · 안개 시야 · 관측을 막음 (옆 선은 그대로),
/// 지속 뒤 사라짐, 연막 수류탄 (보유자만 · 피해 없음), 연막탄 지점 포격 (발수 · 연막탄 있는 포만), 경박격포 세력별 변형 · 자동 곡사 (사거리 36),
/// 철조망 절단 (돌격보병만 · 몇 초 뒤 통과 · 정지 = 취소 · 통로로 건넘), 저장 · 체크섬 · 명령 직렬화. `KD_SELFTEST_ONLY=breach`.
/// </summary>
public static class BreachSelfTest
{
    static readonly SnapshotMeta Meta = new("breach", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[참호 돌파 묶음 — 연막 · 철조망 절단 · 경박격포]");
        try
        {
            using var _ = TestDefs.NoCommandDelay();
            TestSmokeLine(check);
            TestSmokeFog(check);
            TestSmokeGrenade(check);
            TestSmokeShell(check);
            TestMortar(check);
            TestWireCut(check);
            TestSaveLoad(check);
            TestSerialization(check);
        }
        catch (Exception e) { check(false, $"breach 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 90, bool fog = false, int wireRow = -1)
    {
        var defs = Defs.Current;
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, y == wireRow && x >= 8 && x < size - 8 ? TerrainType.Wire : TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = true };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = fog;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Round(s / Config.SimDt);

    static Unit Dummy(World w, int faction, Vector2 at)
    {
        var d = w.SpawnUnit(Defs.Current.Units["infantry"], faction, at);
        d.Weapon = null; d.Hp = 100000;
        w.Hold(faction, new[] { d });
        return d;
    }

    static SmokeCloud Cloud(World w, Vector2 at, float r, float sec) =>
        new() { Owner = 1, Pos = at, Radius = r, StartTick = w.Tick, EndTick = w.Tick + Sec(sec) };

    // ---------------------------------------------------------------- 연막: 사선 · 시야

    static void TestSmokeLine(Action<bool, string> check)
    {
        var w = Bare();
        var rifle = w.SpawnUnit(Defs.Current.Units["infantry"], 1, new Vector2(20.5f, 40.5f));
        var foe = Dummy(w, 2, new Vector2(36.5f, 40.5f));
        w.Hold(1, new[] { rifle });
        w.Step();
        var wd = rifle.Weapon!;
        bool before = w.HasFiringLine(rifle, foe, wd);
        w.Smokes.Add(Cloud(w, new Vector2(28.5f, 40.5f), 2.5f, 20));
        bool blocked = !w.HasFiringLine(rifle, foe, wd);
        bool side = !w.SmokeBlocks(new Vector2(20.5f, 45.5f), new Vector2(36.5f, 45.5f));   // 연막에서 5칸 옆 선
        bool edge = !w.SmokeBlocks(new Vector2(20.5f, 40.5f), new Vector2(26.5f, 40.5f));   // 가장자리 0.5칸 안 = 보임 (깊이 1칸 이하)
        check(before && blocked && side && edge, $"연막 사선: 연막 전 사격선 {before} → 연막이 가로막음 {blocked} · 옆 선은 그대로 {side} · 가장자리 얕은 곳은 보임 {edge}");
        long shot0 = rifle.LastShotTick;
        Steps(w, Sec(15));
        bool noShot = rifle.LastShotTick == shot0;
        Steps(w, Sec(6));   // 20초 지속 → 사라짐
        bool gone = w.Smokes.Count == 0;
        Steps(w, Sec(10));
        bool shotAfter = rifle.LastShotTick > shot0;
        check(noShot && gone && shotAfter, $"연막 사격: 연막 동안 쏘지 못함 {noShot} · 지속 뒤 사라짐 {gone} · 그 뒤 사격 {shotAfter}");
    }

    static void TestSmokeFog(Action<bool, string> check)
    {
        var w = Bare(fog: true);
        var obs = w.SpawnUnit(Defs.Current.Units["infantry"], 1, new Vector2(20.5f, 40.5f));
        var t1 = Dummy(w, 2, new Vector2(32.5f, 40.5f));
        var t2 = Dummy(w, 2, new Vector2(32.5f, 48.5f));
        w.Hold(1, new[] { obs });
        w.Step();
        w.Fog.Update();
        bool seen = w.Fog.CanSee(1, t1) && w.Fog.CanSee(1, t2);
        w.Smokes.Add(Cloud(w, new Vector2(26.5f, 40.5f), 2.5f, 30));
        w.Fog.Update();
        bool hidden = !w.Fog.CanSee(1, t1), other = w.Fog.CanSee(1, t2);
        // 관측자 (곡사포 관측) 도 연막 너머를 못 본다
        var off = w.SpawnUnit(Defs.Current.Units["officer"], 1, new Vector2(20.5f, 41.5f));
        w.Step();
        bool spot = !w.SeenBySpotter(1, t1) && w.SeenBySpotter(1, t2);
        check(seen && hidden && other && spot, $"연막 시야: 연막 전 보임 {seen} → 연막 너머 안 보임 {hidden} · 옆 표적은 보임 {other} · 관측자도 막힘 {spot}");
        _ = off;
    }

    // ---------------------------------------------------------------- 연막 수류탄 · 연막탄

    static void TestSmokeGrenade(Action<bool, string> check)
    {
        var w = Bare();
        var a = w.SpawnUnit(Defs.Current.Units["infantry_assault"], 1, new Vector2(20.5f, 40.5f));
        var rifle = w.SpawnUnit(Defs.Current.Units["infantry"], 1, new Vector2(20.5f, 42.5f));
        var foe = Dummy(w, 2, new Vector2(24.5f, 40.5f));
        w.Hold(1, new[] { a, rifle });
        w.Step();
        int g0 = a.Grenades, s0 = a.SmokeGrenades;
        float hp0 = foe.Hp;
        int log0 = w.CommandLog.Count;
        w.ThrowSmokeAt(1, new[] { a, rifle }, foe.Pos);
        Steps(w, Sec(2.5f));
        bool one = w.Smokes.Count == 1 && a.SmokeGrenades == s0 - 1 && s0 == Defs.Current.Units["infantry_assault"].SmokeGrenades && s0 > 0;
        bool logged = w.CommandLog.Skip(log0).Any(k => k.Command is ThrowSmokeCommand c && c.Units.Length == 1 && c.Units[0] == a.Id);
        bool harmless = foe.Hp == hp0 && a.Grenades == g0;
        var s = w.Smokes.Count > 0 ? w.Smokes[0] : null;
        bool size = s != null && MathF.Abs(s.Radius - Defs.Current.Breach.SmokeGrenadeRadius) < 1e-4f && Vector2.Distance(s.Pos, foe.Pos) < 1f;
        Steps(w, Sec(Defs.Current.Breach.SmokeGrenadeSec));
        check(one && logged && harmless && size && w.Smokes.Count == 0,
            $"연막 수류탄: 돌격보병만 ({logged}) 1발 → 연막 1 ({one}) · 피해 · 수류탄 소모 없음 {harmless} · 반경 · 위치 {size} · {Defs.Current.Breach.SmokeGrenadeSec}초 뒤 사라짐 (남은 {w.Smokes.Count})");
    }

    static void TestSmokeShell(Action<bool, string> check)
    {
        var w = Bare();
        var c = new Vector2(30.5f, 40.5f);
        var mortar = w.SpawnUnit(Defs.Current.Units["heavy_mortar_korean"], 1, c);
        var pt = c + new Vector2(20, 0);   // 적 없는 지점 (명령이 끝난 뒤 자동 사격이 섞이지 않게)
        var foe = Dummy(w, 1, c + new Vector2(0, 6));   // 아군 더미 (연막탄은 피해 없음)
        w.Hold(1, new[] { mortar });
        w.Step();
        int ammo0 = mortar.Ammo;
        w.GroundFire(1, new[] { mortar }, pt, GroundFireKind.Smoke);
        int clouds = 0;
        for (int i = 0; i < Sec(40); i++) { w.Step(); clouds = Math.Max(clouds, w.Smokes.Count); }
        int rounds = Defs.Current.Breach.SmokeRounds;
        bool ended = mortar.Order != UnitOrder.GroundFire;
        bool ammo = !w.AmmoEnabled || mortar.Ammo == ammo0 - rounds;
        bool near = w.Smokes.All(s => Vector2.Distance(s.Pos, pt) < 8f && MathF.Abs(s.Radius - mortar.Weapon!.SmokeRadius) < 1e-4f);
        check(clouds == rounds && ended && ammo && near && foe.Hp == 100000,
            $"연막탄: 지점 포격 '연막' = {rounds}발 연막 ({clouds}) · 명령 끝 {ended} · 탄약 {ammo} · 목표 근처 · 반경 {near} · 피해 없음 {foe.Hp == 100000}");
        var u = Defs.Current.Units;
        var how = w.SpawnUnit(u["heavy_howitzer"], 1, c + new Vector2(0, 10));
        var fg = w.SpawnUnit(u["heavy_field_gun"], 1, c + new Vector2(0, -10));
        bool can = World.CanSmoke(how) && !World.CanSmoke(fg) && !World.CanSmoke(foe);
        int log0 = w.CommandLog.Count;
        w.GroundFire(1, new[] { fg }, pt, GroundFireKind.Smoke);
        w.Step();
        check(can && w.CommandLog.Count == log0, $"연막탄 있는 포만: 곡사포 · 박격포 가능, 야전포 불가 {can} · 야전포만 고르면 명령 없음 {w.CommandLog.Count == log0}");
    }

    // ---------------------------------------------------------------- 경박격포 (N60)

    static void TestMortar(Action<bool, string> check)
    {
        var u = Defs.Current.Units;
        var keys = new[] { ("heavy_mortar_korean", "korean"), ("heavy_mortar_japanese", "japanese"), ("heavy_mortar", "fengtian") };
        bool defs = keys.All(k => u.TryGetValue(k.Item1, out var d) && d.AllowedFor(k.Item2) && Defs.Current.Weapon(d.Weapon) is { Indirect: true, SpotterOnly: false, Range: 36f } wd && wd.SmokeRadius > 0);
        bool excl = !u["heavy_mortar_korean"].AllowedFor("japanese") && !u["heavy_mortar_japanese"].AllowedFor("korean");
        check(defs && excl, $"경박격포 세력별 변형 3종: 곡사 · 관측자 불필요 · 사거리 36 · 연막탄 {defs} · 세력 전용 {excl}");
        foreach (var (key, dist, want) in new[] { ("heavy_mortar_korean", 30f, true), ("heavy_mortar_japanese", 30f, true), ("heavy_mortar_korean", 42f, false) })
        {
            var w = Bare();
            var m = w.SpawnUnit(u[key], 1, new Vector2(20.5f, 40.5f));
            var foe = Dummy(w, 2, new Vector2(20.5f + dist, 40.5f));
            w.Step();
            int booms = 0;
            for (int i = 0; i < Sec(40); i++) { w.Step(); foreach (var im in w.Impacts) if (im.ShooterId == m.Id && im.Tick == w.Tick - 1) booms++; }
            check((booms > 0) == want, $"{key} 자동 곡사 {dist}칸 (사거리 36): 착탄 {booms}발 — 기대 {(want ? "쏨" : "안 쏨")}");
        }
    }

    // ---------------------------------------------------------------- 철조망 절단 (N59)

    static void TestWireCut(Action<bool, string> check)
    {
        var w = Bare(wireRow: 40);
        var a = w.SpawnUnit(Defs.Current.Units["infantry_assault"], 1, new Vector2(30.5f, 35.5f));
        var rifle = w.SpawnUnit(Defs.Current.Units["infantry"], 1, new Vector2(33.5f, 35.5f));
        w.Hold(1, new[] { a, rifle });
        w.Step();
        bool blockedBefore = !w.Map.IsWalkable(30, 40);
        w.CutWire(1, new[] { a, rifle }, new Vector2(30.5f, 40.5f));
        Steps(w, 3);
        bool assigned = a.CuttingWire && !rifle.CuttingWire;
        int t = 0;
        while (t < Sec(20) && w.Map.Get(30, 40) == TerrainType.Wire) { w.Step(); t++; }
        bool open = w.Map.Get(30, 40) != TerrainType.Wire && w.Map.IsWalkable(30, 40);
        float sec = t * Config.SimDt;
        bool time = sec >= Defs.Current.Breach.WireCutSec - 0.1f && sec < Defs.Current.Breach.WireCutSec + 8f;
        bool others = w.Map.Get(29, 40) == TerrainType.Wire && w.Map.Get(31, 40) == TerrainType.Wire && !a.CuttingWire;
        check(blockedBefore && assigned && open && time && others, $"철조망 절단: 돌격보병만 맡음 {assigned} · {sec:0.0}초 뒤 칸 통과 가능 {open} (시간 {time}) · 옆 칸은 그대로 · 끝나면 해제 {others}");
        // 통로로 건너기
        var dest = new Vector2(30.5f, 46.5f);
        w.Move(1, new[] { a }, dest);
        Steps(w, Sec(25));
        float dd = Vector2.Distance(a.Pos, dest);
        check(dd < 1.5f, $"절단한 통로로 철조망 건너기: 목표까지 {dd:0.0}칸");
        // 정지 = 취소
        var w2 = Bare(wireRow: 40);
        var b = w2.SpawnUnit(Defs.Current.Units["infantry_assault"], 1, new Vector2(50.5f, 35.5f));
        w2.Step();
        w2.CutWire(1, new[] { b }, new Vector2(50.5f, 40.5f));
        Steps(w2, Sec(4));
        bool was = b.CuttingWire;
        w2.Stop(1, new[] { b });
        Steps(w2, Sec(8));
        check(was && !b.CuttingWire && w2.Map.Get(50, 40) == TerrainType.Wire, $"철조망 절단 취소: 정지 명령이면 그만둠 (절단 중 {was} → {b.CuttingWire}) · 칸은 그대로");
        // 철조망 없는 곳은 명령 무시
        var w3 = Bare();
        var c3 = w3.SpawnUnit(Defs.Current.Units["infantry_assault"], 1, new Vector2(50.5f, 35.5f));
        w3.Step();
        w3.CutWire(1, new[] { c3 }, new Vector2(50.5f, 40.5f));
        Steps(w3, 3);
        check(!c3.CuttingWire && w3.WireTilesNear(new Vector2(50.5f, 40.5f), 4).Count == 0, "철조망 없는 지점: 절단 명령 무시");
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 · 직렬화

    static void TestSaveLoad(Action<bool, string> check)
    {
        var defs = Defs.Current;
        var x = Bare(wireRow: 40);
        var a = x.SpawnUnit(defs.Units["infantry_assault"], 1, new Vector2(30.5f, 35.5f));
        var a2 = x.SpawnUnit(defs.Units["infantry_assault"], 1, new Vector2(40.5f, 35.5f));
        var m = x.SpawnUnit(defs.Units["heavy_mortar_korean"], 1, new Vector2(20.5f, 30.5f));
        Dummy(x, 2, new Vector2(40.5f, 50.5f));
        x.Step();
        x.CutWire(1, new[] { a }, new Vector2(30.5f, 40.5f));
        x.ThrowSmokeAt(1, new[] { a2 }, new Vector2(42.5f, 37.5f));
        x.GroundFire(1, new[] { m }, new Vector2(40.5f, 50.5f), GroundFireKind.Smoke);
        int k = 0;
        while (k < Sec(30) && !(a.WireCutTicks > 0 && x.Smokes.Count > 0)) { x.Step(); k++; }
        bool mid = a.WireCutTicks > 0 && x.Smokes.Count > 0;
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        var ya = y.Units.First(u => u.Id == a.Id);
        bool fields = ya.WireCutTile == a.WireCutTile && ya.WireCutTicks == a.WireCutTicks && y.Smokes.Count == x.Smokes.Count
            && y.Units.First(u => u.Id == a2.Id).SmokeGrenades == a2.SmokeGrenades && y.Shells.Count(s => s.Smoke) == x.Shells.Count(s => s.Smoke);
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes)) && x.ComputeChecksum() == y.ComputeChecksum();
        var h0 = x.ComputeChecksum();
        a.WireCutTicks++; bool h1 = x.ComputeChecksum() != h0; a.WireCutTicks--;
        x.Smokes[0].Radius += 0.5f; bool h2 = x.ComputeChecksum() != h0; x.Smokes[0].Radius -= 0.5f;
        bool cont = true;
        for (int i = 0; i < Sec(30); i++)
        {
            x.Step(); y.Step();
            if (i % 30 == 29 && x.ComputeChecksum() != y.ComputeChecksum()) { cont = false; break; }
        }
        bool cut = x.Map.Get(30, 40) != TerrainType.Wire && y.Map.Get(30, 40) != TerrainType.Wire;
        check(mid && fields && same && h1 && h2 && cont && cut,
            $"참호 돌파 저장: 진행 중 상태 {mid} · 필드 복원 {fields} · 바이트 · 체크섬 동일 {same} · 체크섬 반영 (절단 {h1} · 연막 {h2}) · 30초 이어 돌려도 같음 {cont} · 절단 완료 {cut} (형식 {Snapshot.FormatVersion})");
    }

    static void TestSerialization(Action<bool, string> check)
    {
        GameCommand a = new ThrowSmokeCommand(1, new[] { 3, 4 }, new Vector2(10.5f, 20.5f));
        GameCommand b = new CutWireCommand(2, new[] { 5 }, new Vector2(1.25f, 2));
        var ja = System.Text.Json.JsonSerializer.Serialize(a, CommandJson.Options);
        var jb = System.Text.Json.JsonSerializer.Serialize(b, CommandJson.Options);
        var ba = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(ja, CommandJson.Options);
        var bb = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(jb, CommandJson.Options);
        check(ba is ThrowSmokeCommand { Target.X: 10.5f } sa && sa.Units.Length == 2 && bb is CutWireCommand { Faction: 2, Target.X: 1.25f } && ReplayFile.FormatVersion >= 19,
            $"참호 돌파 명령 직렬화 (smokegrenade · cutwire), 리플레이 형식 {ReplayFile.FormatVersion}");
    }
}
