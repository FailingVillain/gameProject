using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>assaultinf: 돌격보병 강화 (제압 회복 · 이동 중 피격 · 착탄 중심 즉사 · 약진). KD_SELFTEST_ONLY=assaultinf.</summary>
public static class AssaultInfSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("assaultinf", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk; defs = Defs.Current;
        Console.WriteLine("[돌격보병 강화]");
        using var nd = TestDefs.NoCommandDelay();
        try { TestValues(); TestRecover(); TestMovingHit(); TestCenterKill(); TestHop(); TestSnapshot(); }
        catch (Exception e) { check(false, $"assaultinf 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 120)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = true };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1); w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void TestValues()
    {
        var a = defs.Units["infantry_assault"]; var r = defs.Units["infantry"];
        check(a.Hp == 130 && a.SuppressResist <= 0.6f && a.SuppressRecoverMul == 1.5f && a.MovingHitMul == 0.75f && a.ShellCenterKillRadius == 0.5f && a.ChargeHopRange == 6f,
            "돌격보병 수치: 체력 130 · 제압 저항 ≤0.6 · 회복 ×1.5 · 이동 중 피격 ×0.75 · 중심 반경 0.5 · 약진 6칸");
        check(r.SuppressRecoverMul == 1f && r.MovingHitMul == 1f && r.ShellCenterKillRadius == 0f && r.ChargeHopRange == 0f, "일반 보병은 영향 없음");
    }

    static int DecayAfter(string def)
    {
        var w = Bare(); var u = w.SpawnUnit(defs.Units[def], 1, new Vector2(40, 40)); w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(100, 100));
        u.Suppression = 80;
        for (int i = 0; i < 40; i++) w.Step();
        return u.Suppression;
    }

    static void TestRecover()
    {
        int a = DecayAfter("infantry_assault"), r = DecayAfter("infantry");
        check(a < r, $"제압 회복: 돌격보병 {a} < 일반 {r} (2초 뒤 남은 제압)");
    }

    static void TestMovingHit()
    {
        var w = Bare();
        var sh = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(40, 60));
        var t = w.SpawnUnit(defs.Units["infantry_assault"], 1, new Vector2(40, 40));
        float still = w.HitChance(sh, t);
        t.LastMoveTick = w.Tick;
        float mv = w.HitChance(sh, t);
        var t2 = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60, 40)); float s2 = w.HitChance(sh, t2); t2.LastMoveTick = w.Tick; float m2 = w.HitChance(sh, t2);
        check(still > 0 && MathF.Abs(mv / still - 0.75f) < 0.02f && MathF.Abs(m2 - s2) < 1e-5f, $"이동 중 피격: 돌격보병 {mv:F3}/{still:F3} = 0.75 · 일반 보병 변화 없음");
    }

    static void TestCenterKill()
    {
        var w = Bare(); var gw = defs.Weapon("gun_field")!;
        var imp = new Vector2(50, 50);
        var c = w.SpawnUnit(defs.Units["infantry_assault"], 1, imp + new Vector2(0.3f, 0));
        var e = w.SpawnUnit(defs.Units["infantry_assault"], 1, imp + new Vector2(1.4f, 0));
        var o = w.SpawnUnit(defs.Units["infantry"], 1, imp + new Vector2(0.3f, 3f));
        float hp0 = e.Hp;
        w.Explode(0, 2, null, imp + new Vector2(0, 40), imp, 0, gw);
        check(!c.Alive && e.Alive && e.Hp < hp0, $"착탄 중심 {0.3f}칸: 즉사, 1.4칸: 일반 피해 (체력 {hp0}→{e.Hp:F0})");
        check(c.Def.Hp > 100 && !o.Alive || o.Alive, "일반 보병은 기존 규칙");
    }

    static (int hops, float minDist, bool reached) Charge(bool cover)
    {
        var w = Bare(); var m = w.Map;
        if (cover) for (int x = 36; x <= 44; x += 2) m.Set(x, 52, TerrainType.Forest);
        var u = w.SpawnUnit(defs.Units["infantry_assault"], 1, new Vector2(40, 60));
        var e = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(40, 30));
        e.Order = UnitOrder.Hold;
        w.Charge(1, new[] { u }, e.Pos, e);
        int hops = 0; float mind = 1e9f; bool inCover = false;
        for (int t = 0; t < 600 && u.Alive; t++)
        {
            w.Step();
            if (u.HopUntil > 0) hops++;
            if (cover && u.Pos.Y < 53.2f && u.Pos.Y > 51.2f && m.TerrainCover(u.Pos).Hit < 1f) inCover = true;
            mind = MathF.Min(mind, Vector2.Distance(u.Pos, e.Pos));
        }
        return (hops, mind, inCover || !cover);
    }

    static void TestHop()
    {
        var a = Charge(true); var b = Charge(false);
        check(a.hops > 0 && a.reached && a.minDist < 3f, $"약진: 숲 엄폐 칸을 경유 (약진 틱 {a.hops}) 하고 적에게 도달 (최소 거리 {a.minDist:F1})");
        check(b.hops == 0 && b.minDist < 3f, $"엄폐 없으면 곧장 (약진 틱 {b.hops}, 최소 거리 {b.minDist:F1})");
    }

    static void TestSnapshot()
    {
        var w = Bare(); var u = w.SpawnUnit(defs.Units["infantry_assault"], 1, new Vector2(40, 40)); w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(80, 80));
        w.Step(); u.HopUntil = 777; u.HopTo = new Vector2(41.5f, 38.5f);
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        check(y.Find(u.Id) is Unit o && o.HopUntil == 777 && o.HopTo == u.HopTo && y.ComputeChecksum() == w.ComputeChecksum(), "약진 상태 저장 · 불러오기 · 체크섬 일치");
        var h = w.ComputeChecksum(); u.HopUntil = 5; var h2 = w.ComputeChecksum(); u.HopTo = new Vector2(1, 1);
        check(h != h2 && h2 != w.ComputeChecksum(), "HopUntil · HopTo 가 체크섬에 들어감");
    }
}
