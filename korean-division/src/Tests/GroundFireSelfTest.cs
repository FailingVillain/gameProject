using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 플레이어 지점 포격 (GroundFire.cs): 관측자 없이도 지정 지점에 반복 사격 · 퍼짐 배수 · 사거리/최소사거리 거부 · 직사포 · 사계 밖 재방열 ·
/// 취소 (정지 · 다른 명령) · 탄약 소진 · AI 는 안 씀 · 저장 불러오기 · 명령 직렬화. `KD_SELFTEST_ONLY=groundfire`.
/// </summary>
public static class GroundFireSelfTest
{
    static readonly SnapshotMeta Meta = new("groundfire", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> check) => LegacyDeploy.Wrap(() => RunImpl(opt, check));
    static void RunImpl(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            using var _ = TestDefs.NoCommandDelay();
            NoSpotterNoFire(opt, check);
            FiresAtGround(opt, check);
            Rejections(opt, check);
            ArcRedeploy(opt, check);
            Cancel(opt, check);
            AmmoOut(opt, check);
            AiNeverUses(check);
            SaveLoad(opt, check);
            Serialization(check);
        }
        catch (Exception e) { check(false, $"지점 포격 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World NewWorld(int seed)
    {
        var w = TestDefs.Strip(World.CreateStandard(TestDefs.Flat(GameMap.Generate(100, 100, seed)), Defs.Current));
        w.Fog.Enabled = false;
        return w;
    }

    static Vector2 Center(World w)
    {
        var (cx, cy) = w.Map.NearestWalkable(w.Map.Width / 2, w.Map.Height / 2);
        return new Vector2(cx + 0.5f, cy + 0.5f);
    }

    static Unit Dummy(World w, int faction, Vector2 pos)
    {
        var u = w.SpawnUnit(Defs.Current.Units["infantry"], faction, pos);
        u.Weapon = null;
        w.Hold(faction, new[] { u });
        u.Hp = 100000;
        return u;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    /// <summary>n 틱 동안 이 유닛의 새 Shots 수 (Shots 는 10틱 뒤 지워지므로 매 틱 센다).</summary>
    static int CountShots(World w, Unit u, int ticks)
    {
        int n = 0;
        for (int i = 0; i < ticks; i++) { w.Step(); n += w.Shots.Count(s => s.ShooterId == u.Id && s.Tick == w.Tick - 1); }
        return n;
    }

    // ---------------------------------------------------------------- 기준선: 관측자 없으면 자동 사격 안 함

    static void NoSpotterNoFire(GameOptions opt, Action<bool, string> check)
    {
        var w = NewWorld(opt.Seed);
        var c = Center(w);
        var gun = w.SpawnUnit(Defs.Current.Units["heavy_howitzer"], 1, c);
        w.Hold(1, new[] { gun });
        Dummy(w, 2, c + new Vector2(20, 0));
        int n = CountShots(w, gun, 60 * 20);
        check(n == 0 && gun.Order == UnitOrder.Hold, $"기준선: 관측자 없는 곡사포는 보이는 적이 있어도 자동 사격 안 함 (20초 {n}발)");
    }

    // ---------------------------------------------------------------- 지점 포격: 관측자 없이 반복 사격

    static void FiresAtGround(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 1);
        var c = Center(w);
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, c);
        var target = c + new Vector2(20, 0);
        var foe = Dummy(w, 2, target);
        w.Hold(1, new[] { gun });
        w.GroundFire(1, new[] { gun }, target);
        Steps(w, 2);
        bool ordered = gun.Order == UnitOrder.GroundFire && gun.GroundPoint == target;
        var impacts = new List<Vector2>();
        float hp0 = foe.Hp;
        for (int i = 0; i < 60 * 90; i++)
        {
            w.Step();
            foreach (var im in w.Impacts) if (im.ShooterId == gun.Id && im.Tick == w.Tick - 1) impacts.Add(im.Impact);
        }
        var wp = defs.Weapons["howitzer_heavy"];
        float maxErr = World.ScatterAt(wp, Vector2.Distance(gun.Pos, target)) * defs.CombatRules.GroundScatterMul + 1e-3f;
        bool near = impacts.Count > 0 && impacts.All(p => Vector2.Distance(p, target) <= maxErr);
        check(ordered && impacts.Count >= 4 && near && foe.Hp < hp0 && gun.Order == UnitOrder.GroundFire,
              $"지점 포격: 관측자 없이 지정 지점에 반복 사격 (90초 {impacts.Count}발, 최대 오차 {(impacts.Count > 0 ? impacts.Max(p => Vector2.Distance(p, target)) : 0):0.00} ≤ {maxErr:0.00}), 지점의 적 피해 {hp0 - foe.Hp:0}, 명령 유지");
        check(defs.CombatRules.GroundScatterMul > 1f, $"balance.json combat.groundScatterMul = {defs.CombatRules.GroundScatterMul} (관측 없는 사격은 퍼짐이 큼)");
    }

    // ---------------------------------------------------------------- 거부: 사거리 밖 · 최소 사거리 안 · 대상 아닌 유닛 · 남의 포

    static void Rejections(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 2);
        var c = Center(w);
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, c);
        var rifle = w.SpawnUnit(defs.Units["infantry"], 1, c + new Vector2(0, 5));
        var wp = defs.Weapons["howitzer_heavy"];
        w.GroundFire(1, new[] { gun, rifle }, c + new Vector2(wp.Range + 10, 0));
        Steps(w, 3);
        bool far = gun.Order != UnitOrder.GroundFire;
        w.GroundFire(1, new[] { gun }, c + new Vector2(wp.MinRange * 0.5f, 0));
        Steps(w, 3);
        bool near = gun.Order != UnitOrder.GroundFire;
        w.GroundFire(1, new[] { rifle }, c + new Vector2(10, 0));
        Steps(w, 3);
        bool kind = rifle.Order != UnitOrder.GroundFire && !World.CanGroundFire(rifle) && World.CanGroundFire(gun);
        w.GroundFire(2, new[] { gun }, c + new Vector2(20, 0));
        Steps(w, 3);
        bool foreign = gun.Order != UnitOrder.GroundFire;
        check(far && near && kind && foreign, $"지점 포격 거부: 사거리 밖 ({far}) · 최소 사거리 안 ({near}) · 소총병 ({kind}) · 남의 포 ({foreign})");
        // 직사 범위포 (야전포): 대상이고 사선이 트인 지점은 허용
        var field = w.SpawnUnit(defs.Units["heavy_field_gun"], 1, c + new Vector2(-20, 0));
        bool direct = World.CanGroundFire(field) && !World.IsIndirect(field.Weapon);
        // 사선 검사: 주변 지점들을 훑어 사선이 트인 지점은 허용 · 막힌 지점은 거부 (생성 지도의 숲 · 언덕 위치에 맞춰)
        Vector2? open = null, blocked = null;
        for (int a = 0; a < 72; a++)
        {
            var q = field.Pos + new Vector2(MathF.Cos(a * 5 * DetMath.Pi / 180f), MathF.Sin(a * 5 * DetMath.Pi / 180f)) * 14f;
            bool los = w.Map.HasFiringLine(field.Pos, q, field.Id, 0);
            if (los) open ??= q; else blocked ??= q;
        }
        bool allowOpen = open is { } o && w.CanGroundFireAt(field, o);
        bool rejectBlocked = blocked is not { } bq || !w.CanGroundFireAt(field, bq);   // (막힌 지점이 없으면 통과)
        bool farField = !w.CanGroundFireAt(field, field.Pos + new Vector2(field.Weapon!.Range + 5, 0));
        if (open is { } oo) { w.GroundFire(1, new[] { field }, oo); Steps(w, 3); }
        check(direct && allowOpen && rejectBlocked && farField && field.Order == UnitOrder.GroundFire,
              $"직사 범위포 (야전포) 도 지점 포격 대상: 직사 {direct} · 사선 트인 지점 허용 {allowOpen} · 막힌 지점 {(blocked.HasValue ? "있음" : "없음")} 거부 {rejectBlocked} · 사거리 밖 거부 {farField}");
    }

    // ---------------------------------------------------------------- 사계 밖: 철수 → 회전 → 재방열 → 사격

    static void ArcRedeploy(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var towed = defs.Units["heavy_howitzer"];
        int setup = World.SetupTicks(towed), pack = World.PackTicks(towed);
        var w = NewWorld(opt.Seed + 3);
        var c = Center(w);
        var gun = w.SpawnUnit(towed, 1, c);
        Dummy(w, 2, c + new Vector2(20, 0));
        w.GroundFire(1, new[] { gun }, c + new Vector2(20, 0));
        bool inArc = true; float half = World.TraverseHalf(gun);
        void Tk() { w.Step(); if (World.TraverseLocked(gun) && MathF.Abs(DetMath.WrapPi(gun.Facing - gun.DeployCenter)) > half + 1e-3f) inArc = false; }
        for (int i = 0; i < 60 * 20 && gun.LastShotTick < 0; i++) Tk();
        long shot0 = gun.LastShotTick;
        bool first = shot0 > 0 && gun.Deployed && MathF.Abs(DetMath.WrapPi(gun.DeployCenter)) < 0.05f;
        // N10: 90° 지점 (중심을 60° 옮기면 됨, 문턱 미만) → 방열한 채 포가 회전 후 사격 (철수 없음)
        var p2 = c + new Vector2(0, 22);
        bool outside = !World.CanTraverseTo(gun, p2) && World.CanTraverseInPlace(gun, p2);
        long cmd = w.Tick;
        w.GroundFire(1, new[] { gun }, p2);
        bool packed = false; long shot2 = -1, laid = -1;
        for (int i = 0; i < 60 * 40 && shot2 < 0; i++) { Tk(); if (gun.Deploy != DeployPhase.Deployed) packed = true; if (laid < 0 && World.CanTraverseTo(gun, p2)) laid = w.Tick; if (gun.LastShotTick > shot0) shot2 = w.Tick - 1; }
        float c2 = gun.DeployCenter / DetMath.Pi * 180f;
        int rot60 = (int)(DetMath.Pi / 3f / towed.TurnRadPerSec / Config.SimDt);   // 이동 자세 회전 60°
        check(first && outside && !packed && shot2 > 0 && c2 > 55f && c2 < 95f && laid > 0 && laid - cmd < pack + setup + rot60 && inArc && gun.Order == UnitOrder.GroundFire,
              $"N10 사계 밖 지점 (옮길 각 60°): 방열한 채 포가 회전 ({laid - cmd}틱 < 철수 {pack} + 회전 {rot60} + 재방열 {setup}) → 사격 ({shot2 - cmd}틱, 재장전 포함), 새 중심 {c2:0.0}°, 철수 없음 ({!packed}), 차체는 늘 사계 안 ({inArc})");
        // 문턱 이상 (옮길 각 150°) → 철수 → 회전 → 재방열
        float back = (c2 + 180f) * DetMath.Pi / 180f;
        var p3 = c + new Vector2(MathF.Cos(back), MathF.Sin(back)) * 22f;
        bool far = !World.CanTraverseInPlace(gun, p3);
        long shot3s = gun.LastShotTick, cmd3 = w.Tick;
        w.GroundFire(1, new[] { gun }, p3);
        bool packed3 = false; long shot3 = -1;
        for (int i = 0; i < 60 * 40 && shot3 < 0; i++) { Tk(); if (gun.Deploy == DeployPhase.Packing) packed3 = true; if (gun.LastShotTick > shot3s) shot3 = w.Tick - 1; }
        bool redeployed = shot3 > 0 && gun.Deployed && MathF.Abs(DetMath.WrapPi(gun.DeployCenter - back)) < 0.05f;
        check(far && packed3 && redeployed && shot3 - cmd3 >= pack + setup && inArc && gun.Order == UnitOrder.GroundFire,
              $"사계 밖 지점 (옮길 각 150° ≥ 문턱): 철수 {pack} + 재방열 {setup}틱 뒤 사격 ({shot3 - cmd3}틱), 새 중심 {gun.DeployCenter / DetMath.Pi * 180f:0.0}°, 방열 중 차체는 늘 사계 안 ({inArc})");
    }

    // ---------------------------------------------------------------- 취소: 정지 · 이동 · 사수 · 공격

    static void Cancel(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 4);
        var c = Center(w);
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, c);
        var t = c + new Vector2(20, 0);
        var foe = Dummy(w, 2, t);
        bool all = true;
        string[] names = { "정지", "이동", "사수", "공격" };
        for (int k = 0; k < 4; k++)
        {
            w.GroundFire(1, new[] { gun }, t);
            Steps(w, 5);
            bool on = gun.Order == UnitOrder.GroundFire;
            switch (k)
            {
                case 0: w.Stop(1, new[] { gun }); break;
                case 1: w.Move(1, new[] { gun }, c + new Vector2(-6, 0)); break;
                case 2: w.Hold(1, new[] { gun }); break;
                case 3: w.Attack(1, new[] { gun }, foe); break;
            }
            Steps(w, 3);
            bool off = gun.Order != UnitOrder.GroundFire && gun.GroundPoint == Vector2.Zero;
            if (!(on && off)) { all = false; check(false, $"{names[k]} 명령이 지점 포격을 취소하지 못함 (켜짐 {on}, 꺼짐 {off})"); }
            w.Stop(1, new[] { gun });
            Steps(w, 2);
        }
        check(all, "정지 · 이동 · 사수 · 공격 명령이 지점 포격을 취소하고 목표를 지움");
    }

    // ---------------------------------------------------------------- 탄약 소진

    static void AmmoOut(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 5);
        var c = Center(w);
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, c);
        Dummy(w, 2, c + new Vector2(20, 0));
        if (!w.AmmoEnabled || World.DebugInfiniteAmmo) { check(true, "탄약 소진 테스트 건너뜀 (탄약 규칙 꺼짐)"); return; }
        w.GroundFire(1, new[] { gun }, c + new Vector2(20, 0));
        for (int i = 0; i < 60 * 30 && gun.LastShotTick < 0; i++) w.Step();
        int ammoAfter = gun.Ammo, max = gun.AmmoMax;
        gun.Ammo = 0;   // 막 쐈으므로 회복 대기 (regenDelay) 중 — 마지막 탄을 쏜 것으로 본다
        Steps(w, 3);
        bool ended = gun.Order == UnitOrder.Idle && gun.GroundPoint == Vector2.Zero;
        int more = CountShots(w, gun, 60 * 15);
        check(gun.LastShotTick > 0 && ammoAfter < max && ended && more == 0, $"탄약 소진: 쏠 때마다 탄약 소모 ({max} → {ammoAfter}), 탄약 0 이면 명령 해제 ({ended}) · 이후 사격 없음 ({more}발)");
    }

    // ---------------------------------------------------------------- AI 는 지점 포격을 쓰지 않는다

    static void AiNeverUses(Action<bool, string> check)
    {
        var defs = Defs.Current;
        var setup = new BattleSetup { SideA = "infantry:80,officer:4,heavy_howitzer:3", SideB = "infantry:70,officer:3", Map = "flat", Distance = 60, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 2);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        bool anyOrder = false;
        for (int i = 0; i < 60 * 40 && w.Winner == 0; i++)
        {
            w.Step();
            if (i % 30 == 0 && w.Units.Any(u => u.Alive && u.Order == UnitOrder.GroundFire)) anyOrder = true;
        }
        bool anyCmd = w.CommandLog.Any(r => r.Command is GroundFireCommand);
        int guns = w.Units.Count(u => u.Owner == 1 && u.Def.Key == "heavy_howitzer");
        check(!anyCmd && !anyOrder && guns > 0, $"AI (전술) 는 지점 포격을 안 씀: 40초 · 곡사포 {guns}기 · 명령 {w.CommandLog.Count}개 중 GroundFire {(anyCmd ? "있음" : "없음")}");
    }

    // ---------------------------------------------------------------- 저장 · 불러오기

    static void SaveLoad(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = NewWorld(opt.Seed + 6);
        var c = Center(w);
        var gun = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, c);
        var gun2 = w.SpawnUnit(defs.Units["heavy_field_gun"], 1, c + new Vector2(0, 6));
        Dummy(w, 2, c + new Vector2(20, 0));
        w.GroundFire(1, new[] { gun, gun2 }, c + new Vector2(20, 0));
        Steps(w, 60 * 14);
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        var yg = y.Units.First(u => u.Id == gun.Id);
        bool same = yg.Order == UnitOrder.GroundFire && yg.GroundPoint == gun.GroundPoint && y.ComputeChecksum() == w.ComputeChecksum()
                    && Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool cont = true;
        for (int i = 0; i < 60 * 25; i++)
        {
            w.Step(); y.Step();
            if (i % 30 == 29 && w.ComputeChecksum() != y.ComputeChecksum()) { cont = false; break; }
        }
        check(same && cont && gun.Order == UnitOrder.GroundFire, $"지점 포격 중 저장: 목표 · 명령 복원 ({same}), 25초 이어서 체크섬 같음 ({cont})");
    }

    // ---------------------------------------------------------------- 명령 직렬화 (리플레이)

    static void Serialization(Action<bool, string> check)
    {
        GameCommand cmd = new GroundFireCommand(1, new[] { 5, 6 }, new Vector2(12.5f, 33f));
        string json = System.Text.Json.JsonSerializer.Serialize(cmd, CommandJson.Options);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(json, CommandJson.Options);
        check(back is GroundFireCommand g && g.Faction == 1 && g.Units.SequenceEqual(new[] { 5, 6 }) && g.Target == new Vector2(12.5f, 33f) && json.Contains("groundfire"), $"GroundFireCommand 직렬화 왕복 ({json})");
    }
}
