using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>중대 단위 행진 사격 · 자동 돌격 거리 검증 (Stage 2, docs/COLLISION_ANALYSIS.md · src/Core/MarchCommand.cs). CollisionSelfTest 가 부른다.</summary>
internal static class CollisionSelfTest2
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        TestValues();
        TestCompanyTogether();
        TestNoCommander();
        TestMarchFireToggle();
        TestNcoSquad();
        TestMarchToCharge();
        TestAutoChargeRange();
    }

    static World Bare(int w, int h) { var x = CollisionSelfTest.Bare(CollisionSelfTest.Open(w, h)); x.Fog.Enabled = false; return x; }

    /// <summary>적 (무기 없음, 체력 1000, 사수) 줄.</summary>
    static List<Unit> Dummies(World w, int n, Vector2 c)
    {
        var l = CollisionSelfTest.Grid(w, n, c, n, faction: 2);
        foreach (var u in l) { u.Weapon = null; u.Hp = 1000; }
        w.Hold(2, l);
        return l;
    }

    static void TestValues()
    {
        var c = defs.CombatRules;
        check(c.ChargeAutoRange == 1f && c.MarchChargeRange == 5f && c.MarchMoveMul == 0.5f && c.MarchNcoRangeFrac > 0 && c.MarchNcoRangeFrac < 1,
            $"수치: 근접 자동 돌격 {c.ChargeAutoRange}칸 · 행진 사격 중 돌격 전환 {c.MarchChargeRange}칸 · 행진 사격 속도 ×{c.MarchMoveMul} · 분대 반경 ×{c.MarchNcoRangeFrac}");
    }

    static void TestCompanyTogether()
    {
        // 장교 + 29명 중대가 서 있는 적 (무기 없음) 에게 이동 명령: 장교가 행진 사격을 켜면 걷는 전원이 같은 틱부터 같은 속도 배수
        var w = Bare(120, 70);
        var us = CollisionSelfTest.Grid(w, 29, new Vector2(30, 33), 15);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(30, 36)));
        w.FormCompany(1, us);
        var foe = Dummies(w, 12, new Vector2(52, 34));
        w.Step();
        var co = us[0].Company!;
        w.SetMarchFire(1, us, true);
        w.AttackMove(1, us, new Vector2(80, 34), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        long firstOn = -1; int mixed = 0, onTicks = 0, shots = 0;
        for (int t = 0; t < 60 * 12; t++)
        {
            w.Step();
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && us.Any(u => u.Id == s.ShooterId)) shots++;
            var walkers = us.Where(u => u.Alive && !u.Def.IsOfficer && u.Walking && !u.Charging).ToList();
            if (walkers.Count == 0) continue;
            int slowed = walkers.Count(u => w.MarchSlowed(u));
            if (slowed > 0 && firstOn < 0) firstOn = w.Tick;
            if (slowed > 0) onTicks++;
            if (slowed > 0 && slowed < walkers.Count) mixed++;
        }
        check(firstOn > 0 && onTicks > 30 && shots > 0, $"중대 행진 사격: 장교 판단으로 시작 (틱 {firstOn}, 유지 {onTicks}틱, 사격 {shots}발)");
        check(mixed == 0, $"중대 행진 사격 = 걷는 전원이 같은 순간 같은 속도 (일부만 느린 틱 {mixed})");
    }

    static void TestNoCommander()
    {
        // 지휘자 없는 보병: 중대 없는 병사 · 장교 없고 부사관 없는 중대 → 행진 사격 없음
        int Shots(bool company)
        {
            var w = Bare(120, 70);
            var us = CollisionSelfTest.Grid(w, 12, new Vector2(30, 33), 6);
            if (company) w.FormCompany(1, us);
            Dummies(w, 8, new Vector2(48, 34));
            w.Step();
            w.Move(1, us, new Vector2(80, 34), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
            int n = 0;
            for (int t = 0; t < 60 * 8; t++) { w.Step(); foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && us.Any(u => u.Id == s.ShooterId)) n++; }
            return n;
        }
        int loose = Shots(false), noOfficer = Shots(true);
        check(loose == 0 && noOfficer == 0, $"지휘자 없으면 개별 행진 사격 안 함 (중대 없음 {loose}발 · 장교 · 부사관 없는 중대 {noOfficer}발)");
    }

    static void TestMarchFireToggle()
    {
        // 돌격 사격 토글: 기본 꺼짐 · 켜도 일반 이동 중에는 안 함 · 켜고 공격 이동일 때만 (장교 판단), 상태는 저장 · 리플레이 명령
        int Run(bool on, bool attackMove, out bool defaultOff)
        {
            var w = Bare(120, 70);
            var us = CollisionSelfTest.Grid(w, 15, new Vector2(30, 33), 15);
            us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(30, 35)));
            w.FormCompany(1, us);
            Dummies(w, 10, new Vector2(52, 33));
            w.Step();
            defaultOff = !us[0].Company!.MarchFireOn;
            if (on) w.SetMarchFire(1, us, true);
            var dst = new Vector2(80, 33);
            var fs = new FormSpec((int)FormType.Line, 0, new Vector2(1, 0));
            if (attackMove) w.AttackMove(1, us, dst, fs); else w.Move(1, us, dst, fs);
            int slowedTicks = 0;
            for (int t = 0; t < 60 * 10; t++) { w.Step(); if (us.Any(u => w.MarchSlowed(u))) slowedTicks++; }
            if (on && attackMove)
            {
                var y = Snapshot.Load(Snapshot.Save(w, new SnapshotMeta("mf", 1, false, "standard")), defs).World;
                check(y.Companies.Count > 0 && y.Companies[0].MarchFireOn, "돌격 사격 토글 상태 저장 · 불러오기");
            }
            return slowedTicks;
        }
        int off = Run(false, true, out bool d), onMove = Run(true, false, out _), onAttack = Run(true, true, out _);
        check(d && off == 0, $"돌격 사격 기본 꺼짐: 공격 이동이라도 행진 사격 없음 ({off}틱)");
        check(onMove == 0, $"켜도 일반 이동 중에는 안 함 ({onMove}틱)");
        check(onAttack > 30, $"켜고 공격 이동 중 적이 보이면 장교가 중대 행진 사격 ({onAttack}틱)");
    }

    static void TestNcoSquad()
    {
        // 장교 없는 중대 · 분대장이 공식 부사관: 돌격 사격을 켜도 공격 이동 중 표적을 잡으면 서서 싸운다 — 행진 사격 지휘 없음 (장교만 중대 단위 판단, 2026-10-19 돌격 사격 토글)
        var w = Bare(140, 70);
        var us = CollisionSelfTest.Grid(w, 8, new Vector2(30, 33), 8);
        w.FormCompany(1, us);
        foreach (var u in us) u.GrantKills(defs.CombatRules.RankKills[Math.Max(0, defs.CombatRules.NcoRank - 1)]);   // 전원 부사관 계급 → 분대장 = 부사관
        Dummies(w, 8, new Vector2(70, 34));
        w.Step();
        w.SetMarchFire(1, us, true);
        w.AttackMove(1, us, new Vector2(110, 34), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        int slowed = 0;
        for (int t = 0; t < 60 * 20; t++) { w.Step(); if (us.Any(u => w.MarchSlowed(u))) slowed++; }
        check(slowed == 0, $"장교 없는 중대는 돌격 사격을 켜도 행진 사격 지휘 없음 ({slowed}틱)");
    }

    static void TestMarchToCharge()
    {
        // 행진 사격 중 적이 3칸 안 → 자동 돌격
        var w = Bare(120, 70);
        var us = CollisionSelfTest.Grid(w, 15, new Vector2(30, 33), 15);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(30, 35)));
        w.FormCompany(1, us);
        var foe = Dummies(w, 10, new Vector2(44, 33));
        w.Step();
        w.SetMarchFire(1, us, true);
        w.AttackMove(1, us, new Vector2(60, 33), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        float worst = 0; bool any = false;
        var bh = defs.Tactics.Behaviour; bool gate = bh.AssaultGate; bh.AssaultGate = false;   // N47 돌격 허가는 ChargeSelfTest 에서 — 여기는 거리만
        for (int t = 0; t < 60 * 10 && !any; t++)
        {
            w.Step();
            foreach (var u in us)
                if (u.Alive && u.ChargeState == 1 && u.Target is Unit tu)
                {
                    any = true;
                    worst = MathF.Max(worst, tu.EdgeDistance(u.Pos) - u.Radius);
                }
        }
        bh.AssaultGate = gate;
        check(any && worst <= defs.CombatRules.MarchChargeRange + 0.4f, $"행진 사격 중 {defs.CombatRules.MarchChargeRange}칸 안 적 → 자동 돌격 (돌격 시작 거리 최대 {worst:0.00}칸)");
    }

    static void TestAutoChargeRange()
    {
        // 서 있는 소총병: 적 가장자리 거리 1.3칸 → 자동 돌격 안 함 (예전 1.5칸이면 함), 0.8칸 → 돌격
        bool Charges(float gap)
        {
            var w = Bare(60, 40);
            var me = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30, 20));
            var f = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(30 + 0.7f + gap, 20));
            f.Weapon = null; f.Hp = 1000;
            w.Hold(2, new[] { f });
            for (int t = 0; t < 120; t++) { w.Step(); if (me.ChargeState != 0) return true; me.Pos = new Vector2(30, 20); }
            return false;
        }
        bool near = Charges(0.8f), mid = Charges(1.3f);
        check(near && !mid, $"근접 자동 돌격 거리 1칸: 0.8칸 돌격 {near} · 1.3칸 돌격 {mid}");
    }
}
