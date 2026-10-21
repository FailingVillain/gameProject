using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N35 장교 교전 회피 (2026-10-19 사용자 결정, OfficerConduct.cs): 혼자 돌격 없음 · 표적 찾기 없음 · 자위 사격 · 중대 몸체 곁 유지 · 중대 돌격 때 앞줄 뒤.
/// 창 없이. KD_SELFTEST_ONLY=officer 로 단독 실행.
/// </summary>
public static class OfficerSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[N35 장교 교전 회피]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestNoSoloAutoChargeSelfDefenceFire();
            TestNoTargetSeeking();
            TestChargeOrderNeedsCompany();
            TestChargeOkRule();
            TestCompanyChargeOfficerBehindFront();
            TestOfficerStaysWithBody();
            TestScoutNoFirstFire();
        }
        catch (Exception e) { check(false, $"N35 장교 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

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

    static List<Unit> Grid(World w, int n, Vector2 c, int cols, string def = "infantry", int faction = 1)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++)
            list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * 1f, (i / cols) * 1f)));
        return list;
    }

    /// <summary>소총 n + 장교 1 중대 (장교 = officerPos). 병사들은 사수.</summary>
    static (Company C, List<Unit> Men, Unit Off) Company(World w, int n, Vector2 men, Vector2 officerPos, bool hold = true)
    {
        var us = Grid(w, n, men, 10);
        var off = w.SpawnUnit(defs.Units["officer"], 1, officerPos);
        var all = new List<Unit>(us) { off };
        w.FormCompany(1, all);
        w.Step();
        if (hold) w.Hold(1, us);
        return (us[0].Company!, us, off);
    }

    static void TestValues()
    {
        var cr = defs.CombatRules;
        float pistol = defs.Units["officer"].Weapon is { } wk ? defs.Weapon(wk)?.Range ?? 0 : 0;
        check(cr.OfficerAvoid && cr.OfficerDefendRange > 0 && cr.OfficerDefendRange <= pistol && cr.OfficerChargeWithFrac > 0 && cr.OfficerChargeWithFrac <= 1 && cr.OfficerMeleeRadius > 0,
            $"N35 수치 (balance.json): 회피 {cr.OfficerAvoid}, 자위 {cr.OfficerDefendRange}칸 (권총 {pistol}), 함께 돌격 비율 {cr.OfficerChargeWithFrac}, 근접전 반경 {cr.OfficerMeleeRadius}, 앞줄 여유 {cr.OfficerChargeLeadTol}");
    }

    /// <summary>중대와 떨어진 장교 바로 옆 (1칸 안) 에 적: 장교는 돌격하지 않고 권총으로 자위 사격한다.</summary>
    static void TestNoSoloAutoChargeSelfDefenceFire()
    {
        var w = Bare();
        var (_, _, off) = Company(w, 10, new Vector2(70, 110), new Vector2(70, 60));
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(70, 59.1f));
        w.Hold(2, new[] { foe });   // 사수 = 적은 자동 돌격하지 않음
        bool charged = false;
        for (int t = 0; t < 60 * 4 && off.Alive; t++) { w.Step(); charged |= off.Charging; }
        var shot = w.Stats.OffShot.Values.Sum(v => v.N);
        check(!charged && shot > 0 && off.LastShotTick > 0,
            $"중대 없이 1칸 안 적: 장교 돌격 없음 ({charged}), 자위 권총 사격 {shot}발 (자동 돌격 경로 막힘)");
    }

    /// <summary>자위 반경 밖 (권총 사거리 안) · 멀리 있는 적: 장교는 표적을 잡지 않고 다가가지 않는다 (맞아도 반격 · 추격 없음).</summary>
    static void TestNoTargetSeeking()
    {
        var w = Bare();
        var (_, _, off) = Company(w, 10, new Vector2(70, 110), new Vector2(70, 60));
        float d = (defs.CombatRules.OfficerDefendRange + (defs.Weapon(defs.Units["officer"].Weapon!)!.Range)) * 0.5f + 0.5f;
        var foes = new List<Unit> { w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(70, 60 - d)), w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(70, 48)) };
        w.Hold(2, foes);
        var p0 = off.Pos;
        bool targeted = false;
        for (int t = 0; t < 60 * 3 && off.Alive; t++) { w.Step(); targeted |= off.Target != null; }
        float moved = Vector2.Distance(off.Pos, p0);
        long shots = w.Stats.OffShot.Values.Sum(v => v.N);
        check(!targeted && shots == 0 && moved < 0.5f,
            $"자위 반경 밖 적 ({d:0.0}칸 · 12칸): 장교 표적 없음 ({targeted}), 사격 {shots}, 이동 {moved:0.00}칸 (표적 찾기 · 반격 추격 없음)");
    }

    /// <summary>돌격 명령: 장교 혼자면 돌격하지 않고, 같은 중대 병사와 함께 받으면 함께 돌격.</summary>
    static void TestChargeOrderNeedsCompany()
    {
        var w = Bare();
        var (_, men, off) = Company(w, 10, new Vector2(70, 70), new Vector2(70, 74));
        w.Charge(1, new[] { off }, new Vector2(70, 50));
        w.Step(); w.Step();
        bool solo = off.Charging;
        long blocked = w.Stats.OfficerChargeBlocked;
        var all = new List<Unit>(men) { off };
        w.Charge(1, all, new Vector2(70, 50));
        w.Step(); w.Step();
        bool with = off.Charging && men.Count(u => u.Charging) >= men.Count / 2;
        check(!solo && blocked > 0 && with,
            $"돌격 명령: 장교 혼자 → 돌격 안 함 ({solo}, 막음 {blocked}), 중대와 함께 → 함께 돌격 ({with})");
    }

    /// <summary>판정 규칙: 중대 보병 돌격 비율 &lt; officerChargeWithFrac 이면 장교 돌격 불가, 이상이면 가능. 중대 돌격이 끝나면 장교 돌격도 끝.</summary>
    static void TestChargeOkRule()
    {
        var w = Bare();
        var (_, men, off) = Company(w, 10, new Vector2(70, 70), new Vector2(70, 74));
        bool none = w.OfficerChargeOk(off);
        men[0].ChargeState = 1;   // 10% — 몇 명의 자동 돌격은 중대 돌격이 아님
        bool few = w.OfficerChargeOk(off);
        for (int i = 0; i < 5; i++) men[i].ChargeState = 2;
        bool many = w.OfficerChargeOk(off);
        off.ChargeState = 2;
        foreach (var u in men) u.ChargeState = 0;
        w.Step();
        bool ended = !off.Charging;
        check(!none && !few && many && ended && w.OfficerChargeOk(men[0]),
            $"장교 돌격 허가 = 중대 돌격 중일 때만 (없음 {none} · 1/10 {few} · 5/10 {many}), 중대 돌격 끝 → 장교 돌격 끝 ({ended})");
    }

    /// <summary>중대 돌격 (장교 포함 명령): 장교는 돌격하는 병사들의 앞줄을 넘어 달려 나가지 않는다.</summary>
    static void TestCompanyChargeOfficerBehindFront()
    {
        var w = Bare();
        var (c, men, off) = Company(w, 20, new Vector2(70, 80), new Vector2(70, 78f), hold: false);   // 장교를 앞줄보다 2칸 앞에 세움 (북쪽이 적)
        var foes = Grid(w, 10, new Vector2(70, 66), 10, "infantry", 2);
        w.Hold(2, foes);
        var all = new List<Unit>(men) { off };
        w.Charge(1, all, new Vector2(70, 62));
        Vector2 dir = new(0, -1);
        float maxLead = float.MinValue; int n = 0, fixEnd = -1;
        for (int t = 0; t < 60 * 14 && off.Alive; t++)
        {
            w.Step();
            if (fixEnd < 0 && !all.Any(u => u.Alive && u.FixLeft > 0)) fixEnd = t;   // N57: 착검이 끝난 뒤부터 (착검 중에는 모두 제자리)
            if (!off.Charging || fixEnd < 0 || t < fixEnd + 60) continue;   // 착검 뒤 처음 1초 = 앞에 서 있던 장교가 병사들에게 따라잡히는 중
            float front = men.Where(u => u.Alive && u.Charging).Select(u => Vector2.Dot(u.Pos, dir)).DefaultIfEmpty(float.MinValue).Max();
            if (front == float.MinValue) continue;
            maxLead = MathF.Max(maxLead, Vector2.Dot(off.Pos, dir) - front); n++;
        }
        check(n > 0 && maxLead <= defs.CombatRules.OfficerChargeLeadTol + 0.6f,   // 병사 반칸 안팎 (앞줄 병사가 멈추거나 쓰러져 앞줄이 물러나는 틱 포함)
            $"중대 돌격: 장교는 돌격 병사 앞줄 뒤 (앞줄 대비 최대 {maxLead:+0.00;-0.00}칸, 표본 {n}틱, 앞줄 대기 {w.Stats.OfficerChargeHeld}틱)");
    }

    /// <summary>공격 이동 → 교전: 장교는 중대 몸체 (보병 무게중심) 에서 8칸 안에 머문다.</summary>
    static void TestOfficerStaysWithBody()
    {
        var w = Bare();
        var (c, men, off) = Company(w, 30, new Vector2(70, 100), new Vector2(70, 97.5f), hold: false);
        var foes = Grid(w, 15, new Vector2(70, 76), 15, "infantry", 2);
        w.Hold(2, foes);
        var all = new List<Unit>(men) { off };
        w.AttackMove(1, all, new Vector2(70, 70));
        float max = 0;
        for (int t = 0; t < 60 * 40 && off.Alive; t++)
        {
            w.Step();
            if (t > 60 * 6 && w.OfficerBodyDist(off) is float d and >= 0) max = MathF.Max(max, d);
        }
        bool solo = w.Stats.OffCharge.Values.Sum(v => v.Bad) > 0;
        check(max > 0 && max <= 8f && !solo,
            $"교전 중 장교 ↔ 중대 무게중심 최대 {max:0.0}칸 (≤ 8), 혼자 돌격 {solo}");
    }

    /// <summary>정찰병 (2026-10-19 추가 결정): 자위 반경 밖 적에게 먼저 쏘지 않고, 적이 자위 반경 안에 들어오면 쏜다.</summary>
    static void TestScoutNoFirstFire()
    {
        var w = Bare();
        var sc = w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(70, 60));
        w.SetScoutAuto(1, new[] { sc }, false);
        w.Step();
        w.Hold(1, new[] { sc });
        var foe = w.SpawnUnit(defs.Units["officer"], 2, new Vector2(70, 52));   // 8칸 (카빈 사거리 안, 자위 4칸 밖) — 장교라 먼저 쏘지 않음
        w.Hold(2, new[] { foe });
        bool targeted = false;
        for (int t = 0; t < 60 * 3; t++) { w.Step(); targeted |= sc.Target != null; }
        long first = w.Stats.ScoutShot.Values.Sum(v => v.N);
        foe.Pos = new Vector2(70, 57.5f);   // 2.5칸 = 자위 반경 안
        for (int t = 0; t < 60 * 4 && sc.Alive; t++) w.Step();
        long after = w.Stats.ScoutShot.Values.Sum(v => v.N);
        check(!targeted && first == 0 && after > 0,
            $"정찰병은 먼저 쏘지 않음 (8칸 적: 표적 {targeted} · 사격 {first}), 2.5칸 적에게 자위 사격 {after}발");
    }
}
