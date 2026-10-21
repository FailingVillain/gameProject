using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N23 돌격보병 '난사로 누르고 근접 무기로 쓸어버리기' + N55 장교 경험 성향 (돌격 사격 대 교대 약진 · 기병 정면 돌격). 창 없이. KD_SELFTEST_ONLY=n23 로 단독 실행.
/// </summary>
public static class N23SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[n23 돌격보병 소탕 · 장교 경험 성향]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestSweep();
            TestStyleChance();
            TestStyleDeterministic();
        }
        catch (Exception e) { check(false, $"n23 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static void TestValues()
    {
        var c = defs.Weapons["carbine_assault"]; var u = defs.Units["infantry_assault"];
        check(MathF.Abs(c.Cooldown - 1.1f) < 0.01f && c.CooldownMin <= 1.1f && c.CooldownMax >= 1.1f && MathF.Abs(c.AccNear - 0.25f) < 0.001f && MathF.Abs(c.AccFar - 0.04f) < 0.001f && c.Suppress >= 45,
            $"돌격 카빈 난사: 간격 {c.Cooldown} (옛 1.7), 명중 {c.AccNear}/{c.AccFar} (옛 0.38/0.06), 발당 제압 {c.Suppress} (≥ 45)");
        var cr = defs.CombatRules;
        check(u.MarchFireDefault && u.MarchAccMul > cr.MarchAccMul && u.MarchIntervalMul > 0 && u.MarchIntervalMul < cr.MarchIntervalMul && u.MarchMoveMul > 0 && u.MarchGrenadeFrac is > 0 and <= 1 && u.Sidearm != null && u.SidearmRange >= 6,
            $"돌격보병 행진 사격 손해 작음: 명중 ×{u.MarchAccMul} (보통 ×{cr.MarchAccMul}) · 간격 ×{u.MarchIntervalMul} (보통 ×{cr.MarchIntervalMul}) · 속도 ×{u.MarchMoveMul}, 수류탄 비율 {u.MarchGrenadeFrac}, 권총 {u.SidearmRange}칸");
        var s = defs.Officers.Style;
        check(s.MarchByRank.Length == 7 && s.CavFrontByRank.Length == 7 && s.MarchByRank[1] >= s.MarchByRank[3] && s.MarchByRank[3] >= s.MarchByRank[6] && s.CavFrontByRank[1] >= s.CavFrontByRank[6],
            $"장교 성향 표 (balance.json officers.style): 돌격 사격 {string.Join("/", s.MarchByRank)}, 기병 정면 {string.Join("/", s.CavFrontByRank)} — 계급이 높을수록 낮음");
    }

    /// <summary>장교 없는 돌격보병 12 대 제자리 소총병 8 (평지 40칸): 걸으며 쏘고 (이동 중 사격) · 6칸 권총 · 수류탄 · 단검으로 쓸어버림.</summary>
    static void TestSweep()
    {
        var setup = new BattleSetup { SideA = "infantry_assault:12", SideB = "infantry:8", Map = "flat", AiVsAi = true, Distance = 40 };
        var w = setup.CreateWorld(defs, 23);
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        var bs = w.Units.Where(u => u.Owner == 2).ToList();
        w.Hold(2, bs);
        w.FlushCommands();
        int moveShots = 0, pistol = 0, gren = 0, melee = 0, firstGren = -1, firstMelee = -1;
        for (int t = 0; t < 60 * 150 && w.Winner == 0; t++)
        {
            w.Step();
            foreach (var sh in w.Shots)
                if (sh.Tick == w.Tick - 1 && w.Find(sh.ShooterId) is Unit su && su.Def.MarchFireDefault)
                { if (su.Velocity != Vector2.Zero) moveShots++; if (su.Weapon?.Key == su.Def.Sidearm) pistol++; }
            foreach (var im in w.Impacts) if (im.Tick == w.Tick - 1 && im.Weapon == "grenade" && w.Find(im.ShooterId) is Unit gu && gu.Owner == 1) { gren++; if (firstGren < 0) firstGren = (int)w.Tick; }
            foreach (var m in w.MeleeStrikes) if (m.Tick == w.Tick - 1 && w.Find(m.AttackerId) is Unit mu && mu.Owner == 1) { melee++; if (firstMelee < 0) firstMelee = (int)w.Tick; }
        }
        int aliveA = w.Units.Count(u => u.Owner == 1 && u.Alive), aliveB = w.Units.Count(u => u.Owner == 2 && u.Alive);
        check(moveShots > 0 && pistol > 0, $"장교 없이도 걸으며 쏨 (이동 중 사격 {moveShots}발) · 6칸 안 권총 {pistol}발");
        check(gren > 0 && melee > 0, $"근접: 수류탄 {gren}발 (첫 틱 {firstGren}) · 단검 타격 {melee}회 (첫 틱 {firstMelee})");
        check(aliveB == 0 && aliveA > 0, $"소탕: 제자리 소총 8 전멸, 돌격 {aliveA}/12 생존 ({w.Time:0}초)");
    }

    static Unit Officer(World w, int rank, int xp)
    {
        var o = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(10, 10));
        o.OfficerBase = (byte)rank; o.OfficerXp = xp;
        return o;
    }

    static void TestStyleChance()
    {
        var w = new BattleSetup { SideA = "infantry:1", SideB = "infantry:1" }.CreateWorld(defs, 1);
        var s = defs.Officers.Style;
        int lv3 = defs.Officers.XpLevels[2];
        float lt = w.MarchStyleChance(Officer(w, 1, 0)), capt = w.MarchStyleChance(Officer(w, 3, 0)), captVet = w.MarchStyleChance(Officer(w, 3, lv3)), none = w.MarchStyleChance(null);
        check(MathF.Abs(lt - 0.7f) < 0.01f && captVet <= 0.35f && captVet >= s.MarchMin && lt > capt && capt > captVet && MathF.Abs(none - s.MarchByRank[0]) < 0.001f,
            $"돌격 사격 성향: 소위 신참 {lt:0.00} (≈0.7) > 대위 신참 {capt:0.00} > 대위 경험 3단계 {captVet:0.00} (≈0.3), 지휘 장교 없음 {none:0.00}");
        float cl = w.CavFrontChance(Officer(w, 1, 0)), cv = w.CavFrontChance(Officer(w, 3, lv3));
        check(cl > cv * 3 && cv >= s.CavFrontMin, $"기병 정면 돌격 성향: 소위 신참 {cl:0.00} ≫ 대위 경험 3단계 {cv:0.00}");
        // 해시 분포: 지휘관 2000명 중 돌격 사격을 고르는 비율 ≈ 확률
        int n = 2000, hit = 0;
        for (int i = 0; i < n; i++) if (World.StyleHash(1000 + i, 7, 42) < lt) hit++;
        check(MathF.Abs(hit / (float)n - lt) < 0.04f, $"성향 굴림 분포: {hit}/{n} = {hit / (float)n:0.000} (확률 {lt:0.00})");
    }

    static void TestStyleDeterministic()
    {
        var setup = new BattleSetup { SideA = "infantry:20", SideB = "infantry:20", Map = "flat", AiVsAi = true, Distance = 60 };
        var w1 = setup.CreateWorld(defs, 5); var w2 = setup.CreateWorld(defs, 5);
        foreach (var w in new[] { w1, w2 }) { w.Controllers.Add((1, AiRegistry.Create("tactical")!)); for (int t = 0; t < 120; t++) w.Step(); }   // 중대 편성은 첫 판단 뒤
        bool same = w1.Companies.Count > 0 && w1.Companies.Count == w2.Companies.Count;
        for (int i = 0; same && i < w1.Companies.Count; i++) same = w1.PrefersMarchFire(w1.Companies[i]) == w2.PrefersMarchFire(w2.Companies[i]) && w1.PrefersMarchFire(w1.Companies[i]) == w1.PrefersMarchFire(w1.Companies[i]);
        var o1 = Officer(w1, 1, 0); var o2 = Officer(w2, 1, 0);
        int rashSame = 0;
        for (int g = 1; g <= 50; g++) if (w1.RashCavalryFront(o1, g) == w2.RashCavalryFront(o2, g)) rashSame++;
        check(same && rashSame == 50, $"성향 판정 결정적: 같은 시드 두 세계의 중대 돌격 사격 성향 일치 {same} (중대 {w1.Companies.Count}), 기병 정면 굴림 {rashSame}/50 일치");
    }
}
