using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// arty3 포병 · 중화기 묶음 (Arty3.cs, 사용자 2026-10-21): 수치 · 방렬 지연/난수 · 방렬 자리 유지 · heavy 간격 · 야전포 간접 · 곡사포 조정 사격 · 중기관총 최후 저지선 · 저장/리플레이.
/// 창 없이. KD_SELFTEST_ONLY=arty3 로 단독 실행.
/// </summary>
public static class Arty3SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("arty3", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[arty3 포병 · 중화기]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestDeployNoise();
            TestHoldPlaceLos();
            TestHoldPlaceRange();
            TestHeavyGap();
            TestFieldGunIndirect();
            TestAdjustFire();
            TestFinalLine();
            TestRoundTrip();
        }
        catch (Exception e) { check(false, $"arty3 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(bool record = false, int size = 140)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = record };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Round(s / Config.SimDt);

    /// <summary>불사 · 무장 없는 적 표적.</summary>
    static Unit Foe(World w, Vector2 p)
    {
        var f = w.SpawnUnit(defs.Units["infantry"], 2, p);
        f.Hp = 1e9f; f.Weapon = null;
        w.Hold(2, new[] { f });
        return f;
    }

    /// <summary>사선을 막는 바위 벽.</summary>
    static void Wall(World w, int x0, int x1, int y0, int y1)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) w.Map.Set(x, y, TerrainType.Rock);
    }

    static void TestValues()
    {
        WeaponDef hmg = defs.Weapons["hmg"], t3 = defs.Weapons["hmg_type3"], fg = defs.Weapons["gun_field"], hw = defs.Weapons["howitzer_heavy"];
        check(hmg.Range == 32 && t3.Range == 32 && hmg.FinalLine && t3.FinalLine, $"중기관총 사거리 24 → 32 · 최후 저지선 무기 (hmg {hmg.Range}, 3년식 {t3.Range})");
        check(fg.Damage == 100 && fg.SpotterIndirect && !fg.Indirect && fg.Los, $"야전포 피해 70 → 100 · 직사 + 간접 (피해 {fg.Damage})");
        check(hw.Splash == 3f && hw.AdjustFireShots == 5 && MathF.Abs(hw.Scatter * hw.AdjustFireMinMul - 3f) < 0.01f, $"곡사포 splash 2.5 → 3 · 조정 사격 5발째 퍼짐 {hw.Scatter} → {hw.Scatter * hw.AdjustFireMinMul:0.##}");
        var c = defs.CombatRules;
        check(c.GunDeployDelayMax == 3 && c.GunDeployTimeVar == 0.15f && c.HeavyGapMin == 3 && c.HeavyGapWant == 5, $"방렬 지연 0~{c.GunDeployDelayMax}초 · 방렬 ±{c.GunDeployTimeVar} · heavy 간격 {c.HeavyGapMin}/{c.HeavyGapWant}칸");
    }

    static void TestDeployNoise()
    {
        var w = Bare();
        var fd = defs.Units["heavy_field_gun"];
        int b = World.SetupTicks(fd), dmax = Sec(defs.CombatRules.GunDeployDelayMax);
        var guns = new List<Unit>();
        for (int i = 0; i < 12; i++) guns.Add(w.SpawnUnit(fd, 1, new Vector2(10 + i * 6, 20)));
        bool inRange = guns.All(g => World.SetupTicksOf(g) >= (int)(b * 0.85f) - 1 && World.SetupTicksOf(g) <= (int)(b * 1.15f) + 1 && World.DeployDelayTicksOf(g) >= 0 && World.DeployDelayTicksOf(g) <= dmax);
        int distinct = guns.Select(g => World.SetupTicksOf(g) + World.DeployDelayTicksOf(g)).Distinct().Count();
        var mx = w.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(60, 60));
        bool hmgPlain = World.SetupTicksOf(mx) == World.SetupTicks(mx.Def) && World.DeployDelayTicksOf(mx) == 0;
        check(inRange && distinct >= 6 && hmgPlain, $"방렬 난수: 대포 방렬 시간 {b}틱 ±15% · 시작 지연 0~{dmax}틱 (12문 중 서로 다른 값 {distinct}), 중기관총 거치는 그대로 {hmgPlain}");

        // 실제 방렬: 사수 명령 → 지연 + 방렬 시간 뒤 Deployed (±1틱)
        var g0 = guns[3];
        int want = World.SetupTicksOf(g0) + World.DeployDelayTicksOf(g0);
        w.Hold(1, new[] { g0 });
        int t = 0;
        while (!g0.Deployed && t < want + Sec(10)) { w.Step(); t++; }
        check(g0.Deployed && Math.Abs(t - want) <= 3 , $"방렬 지연 + 방렬: 사수 명령 뒤 {t}틱에 방렬 완료 (예상 {want}틱)");
    }

    /// <summary>방렬한 야전포 (대기) 의 자동 표적이 사선 막힘이면 철수하지 않고 표적을 놓는다.</summary>
    static void TestHoldPlaceLos()
    {
        var w = Bare();
        var g = w.SpawnUnit(defs.Units["heavy_field_gun"], 1, new Vector2(30, 70));
        g.Facing = 0;
        var f0 = Foe(w, new Vector2(48, 70));
        int t = 0;
        while (!g.Deployed && t < Sec(40)) { w.Step(); t++; }
        bool deployed = g.Deployed;
        Steps(w, Sec(25));   // 재방렬 냉각 지남
        // 사선을 막고 표적 지정
        Wall(w, 40, 41, 60, 80);
        f0.Pos = new Vector2(50, 66);
        g.Target = f0;
        Steps(w, Sec(6));
        check(deployed && g.Deploy == DeployPhase.Deployed, $"자리 유지: 사선이 막힌 자동 표적 때문에 철수하지 않음 (자세 {g.Deploy}, 표적 {(g.Target == null ? "놓음" : "유지")})");
    }

    /// <summary>방렬한 대포의 자동 표적이 사거리 밖으로 나가면 gunOutOfRangeHoldSec 동안은 방렬한 채 기다린다.</summary>
    static void TestHoldPlaceRange()
    {
        var w = Bare(size: 160);
        var g = w.SpawnUnit(defs.Units["heavy_infantry_gun"], 1, new Vector2(20, 80));
        g.Facing = 0;
        var f0 = Foe(w, new Vector2(36, 80));
        int t = 0;
        while (g.LastShotTick <= 0 && t < Sec(60)) { w.Step(); t++; }
        bool fired = g.LastShotTick > 0;
        Steps(w, Sec(22));   // 재방렬 냉각 (20초) 지남 — 계속 사격
        f0.Pos = new Vector2(20 + g.Weapon!.Range + 12, 80);   // 사거리 밖으로
        Steps(w, Sec(5));
        bool waited = g.Deploy == DeployPhase.Deployed;
        Steps(w, Sec(defs.CombatRules.GunOutOfRangeHoldSec + 6));
        bool moved = g.Deploy != DeployPhase.Deployed;
        check(fired && waited && moved, $"자리 유지: 사거리 밖 자동 표적 — 5초 뒤에도 방렬 유지 {waited}, {defs.CombatRules.GunOutOfRangeHoldSec}초 넘게 밖이면 철수 · 추격 {moved} (자세 {g.Deploy})");
    }

    /// <summary>나란히 선 중기관총 두 정: 먼저 거치한 쪽 옆에서 둘째는 옆으로 비켜서서 가능하면 5칸 간격으로 거치.</summary>
    static void TestHeavyGap()
    {
        var w = Bare();
        var a = w.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(60, 70));
        var b = w.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(60, 71.5f));
        a.Facing = 0; b.Facing = 0;
        w.Hold(1, new[] { a }); w.Step();
        w.Hold(1, new[] { b });
        int t = 0;
        while (!(a.Deployed && b.Deployed) && t < Sec(40)) { w.Step(); t++; }
        float d = Vector2.Distance(a.Pos, b.Pos);
        check(a.Deployed && b.Deployed && d >= defs.CombatRules.HeavyGapWant - 0.2f && w.Stats.HeavyGapSteps > 0, $"heavy 간격: 1.5칸 옆 중기관총이 비켜서서 거치 — 간격 {d:0.0}칸 (목표 {defs.CombatRules.HeavyGapWant}), {t}틱");

        // 막힌 쪽 (좁은 길): 비켜설 수 없으면 그 자리에서 거치
        var w2 = Bare();
        for (int y = 64; y <= 78; y++) for (int x = 50; x <= 70; x++) if (y != 71) w2.Map.Set(x, y, TerrainType.Water);   // 폭 1칸 둑길 (동서)
        var c1 = w2.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(60, 71.5f));
        var c2 = w2.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(61.6f, 71.5f));
        c1.Facing = 0; c2.Facing = 0;
        w2.Hold(1, new[] { c1 }); w2.Step(); w2.Hold(1, new[] { c2 });
        t = 0;
        while (!(c1.Deployed && c2.Deployed) && t < Sec(40)) { w2.Step(); t++; }
        check(c1.Deployed && c2.Deployed, $"heavy 간격: 좁은 길에서는 비켜서지 못해도 거치함 (간격 {Vector2.Distance(c1.Pos, c2.Pos):0.0}칸, {t}틱)");
    }

    /// <summary>숲 너머 적: 관측 유닛 (정찰병) 이 보면 야전포가 간접 사격, 없으면 안 쏜다.</summary>
    static void TestFieldGunIndirect()
    {
        (int Shots, long Ind) Run(bool spotter)
        {
            var w = Bare();
            Wall(w, 40, 41, 40, 100);
            var g = w.SpawnUnit(defs.Units["heavy_field_gun"], 1, new Vector2(25, 70));
            g.Facing = 0;
            var f0 = Foe(w, new Vector2(75, 70));
            if (spotter) { var s = w.SpawnUnit(defs.Units["scout"], 1, new Vector2(60, 60)); w.Hold(1, new[] { s }); }
            int ammo0 = g.Ammo;
            Steps(w, Sec(70));
            return (ammo0 - g.Ammo, w.Stats.SpotterIndirectShots);
        }
        var with = Run(true);
        var without = Run(false);
        check(with.Shots > 0 && with.Ind > 0 && without.Shots == 0, $"야전포 직사 + 간접: 숲 너머 50칸 적 — 관측 있으면 간접 {with.Ind}발 (사격 {with.Shots}), 관측 없으면 {without.Shots}발");
    }

    static void TestAdjustFire()
    {
        var w = Bare();
        var hw = defs.Weapons["howitzer_heavy"];
        var h = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, new Vector2(20, 20));
        var aim = new Vector2(80, 80);
        var seq = new List<float>();
        for (int i = 0; i < 6; i++) seq.Add(w.AdjustFireMul(h, aim + new Vector2(i * 0.5f, 0), hw) * hw.Scatter);
        float reset = w.AdjustFireMul(h, aim + new Vector2(20, 0), hw) * hw.Scatter;
        bool ok = MathF.Abs(seq[0] - 9) < 0.01f && MathF.Abs(seq[4] - 3) < 0.01f && seq[1] < seq[0] && seq[2] < seq[1] && seq[3] < seq[2] && MathF.Abs(seq[5] - 3) < 0.01f && MathF.Abs(reset - 9) < 0.01f;
        check(ok, $"곡사포 조정 사격: 같은 구역 퍼짐 {string.Join(" → ", seq.Select(v => v.ToString("0.#")))}칸, 다른 구역 → {reset:0.#}칸 (초기화)");
    }

    static void TestFinalLine()
    {
        var w = Bare(record: true);
        var mg = w.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(40, 70));
        mg.Facing = 0;
        var rifle = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(38, 60));
        var off = Foe(w, new Vector2(40, 80));     // 10칸 남쪽 (사격선 밖, 더 가까움)
        var on = Foe(w, new Vector2(56, 71));      // 16칸 동쪽 (사격선 위)
        w.SetFinalLine(1, new[] { mg, rifle }, new Vector2(70, 70)); w.Step();
        bool modes = mg.FireMode == FireMode.FinalLine && rifle.FireMode == FireMode.Free;
        int t = 0;
        while (mg.LastShotTick <= 0 && t < Sec(30)) { w.Step(); t++; }
        bool onlyLine = mg.LastShotTick > 0 && mg.Target == on;
        on.Pos = new Vector2(56, 88); w.Step();   // 사격선 밖으로
        Steps(w, Sec(3));
        bool drop = mg.Target != off && mg.Target != on;
        w.SetFireMode(1, new[] { mg }, FireMode.FinalLine); w.Step();   // 지점 없는 FinalLine 지정은 무시 (모드 유지)
        bool ignored = mg.FireMode == FireMode.FinalLine;
        w.SetFireMode(1, new[] { mg }, FireMode.Free); w.Step();
        bool back = mg.FireMode == FireMode.Free;
        check(modes && onlyLine && drop && ignored && back, $"최후 저지선: 중기관총만 지정 {modes} · 가까운 선 밖 적 대신 사격선 위 적 사격 {onlyLine} · 선 밖 적은 쏘지 않음 {drop} · 자유 사격 복귀 {back}");
    }

    static void TestRoundTrip()
    {
        var w = Bare(record: true);
        var mg = w.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(40, 70));
        var h = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, new Vector2(20, 20));
        Foe(w, new Vector2(62, 71));
        w.SetFinalLine(1, new[] { mg }, new Vector2(70, 70)); w.Step();
        w.AdjustFireMul(h, new Vector2(80, 80), defs.Weapons["howitzer_heavy"]);
        w.AdjustFireMul(h, new Vector2(80, 81), defs.Weapons["howitzer_heavy"]);
        Steps(w, 120);
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var ym = y.Units.First(v => v.Id == mg.Id); var yh = y.Units.First(v => v.Id == h.Id);
        bool fields = ym.FireMode == FireMode.FinalLine && ym.FinalLinePoint == mg.FinalLinePoint && yh.AdjustShots == 2 && yh.AdjustAim == h.AdjustAim;
        var h0 = w.ComputeChecksum();
        h.AdjustShots = 3; bool hash = w.ComputeChecksum() != h0; h.AdjustShots = 2;
        Steps(w, 300); Steps(y, 300);
        check(same && fields && hash && w.ComputeChecksum() == y.ComputeChecksum(), $"저장: 바이트 동일 {same} · 필드 {fields} · 체크섬 반영 {hash} · 이어 돌려도 같음 (형식 {Snapshot.FormatVersion})");
        var cmd = w.CommandLog.Select(c => c.Command).OfType<SetFinalLineCommand>().FirstOrDefault();
        bool json = cmd != null && JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize<GameCommand>(cmd, CommandJson.Options), CommandJson.Options) is SetFinalLineCommand back && back.Point == cmd.Point && back.Units.SequenceEqual(cmd.Units);
        check(json, "리플레이: 최후 저지선 명령 JSON 왕복");
    }
}
