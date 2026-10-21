using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// ai412 (2026-10-19 사용자 승인): 결정 4 돌격 허가 (화력 우세 신호 · 엄호 확인 + 병력비, 예비 타이머) · 결정 12 대대 역할 (1명 분견대는 기동 대대가 아님,
/// 전투 대대 하나 = 대대 안 분할) · 부사관 기본 편성 (미션 보병 분대장 = 하사). 창 없이. KD_SELFTEST_ONLY=ai412 로 단독 실행.
/// </summary>
public static class Ai412SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[ai412 돌격 허가 · 대대 역할 · 부사관 기본 편성]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestClearance();
            TestDrillChargeBlocked();
            TestBattalionRoles();
            TestMissionBattalionRoles();
            TestDefaultNco();
        }
        catch (Exception e) { check(false, $"ai412 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static List<Unit> Spawn(World w, int n, Vector2 c, int faction, string def = "infantry", int cols = 6)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++) list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * 1.1f, (i / cols) * 1.1f)));
        return list;
    }

    static void Pin(IEnumerable<Unit> us, float r) { foreach (var u in us) if (u.Alive) u.Suppression = (short)(defs.CombatRules.SuppressMax * r); }

    static int Ticks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var b = defs.Tactics.Behaviour;
        bool ok = b.AssaultGate && b.AssaultMinOdds > 0 && b.OverwatchSec > 0 && b.OverwatchRadius > 0 && b.OverwatchMin >= 1 && b.AssaultFallbackSec > b.PatienceSec;
        check(ok, $"tactics.json 돌격 허가 값: assaultGate {b.AssaultGate} · assaultMinOdds {b.AssaultMinOdds} · overwatch {b.OverwatchSec}s/{b.OverwatchRadius}칸/{b.OverwatchMin}명 · assaultFallbackSec {b.AssaultFallbackSec} (> patienceSec {b.PatienceSec})");
        check(defs.Tactics.BattalionRoles.MinStrength > 1, $"tactics.json battalionRoles.minStrength {defs.Tactics.BattalionRoles.MinStrength} (> 1 — 1명 분견대 제외)");
        check(defs.CombatRules.SquadLeaderRank == defs.CombatRules.NcoRank, $"balance.json combat.squadLeaderRank {defs.CombatRules.SquadLeaderRank} = 부사관 계급 ncoRank {defs.CombatRules.NcoRank}");
    }

    // ---------------------------------------------------------------- 결정 4: 허가 판단

    static void TestClearance()
    {
        var w = Bare();
        var ch = Spawn(w, 12, new Vector2(60, 64), 1);
        var foe = Spawn(w, 6, new Vector2(60, 56), 2);
        w.Hold(1, ch); w.Hold(2, foe);
        w.Step();
        Vector2 cen = new(60, 64), tgt = foe[0].Pos;
        float range = 24f;
        long now = w.Tick;
        var none = w.AssaultClearance(1, cen, range, tgt, ch, ch.Count, now);
        Pin(foe, 0.9f);
        var sig = w.AssaultClearance(1, cen, range, tgt, ch, ch.Count, now);
        var weak = w.AssaultClearance(1, cen, range, tgt, ch, 3, now);   // 돌격 3명 대 적 6 (병력비 미달)
        Pin(foe, 0f);
        // 엄호: 돌격조 밖 아군 4명이 방금 표적 둘레를 쏨
        var cover = Spawn(w, 4, new Vector2(48, 64), 1);
        foreach (var u in cover) { u.Target = foe[0]; u.LastShotTick = w.Tick; }
        var ow = w.AssaultClearance(1, cen, range, tgt, ch, ch.Count, now);
        foreach (var u in cover) u.LastShotTick = w.Tick - Ticks(defs.Tactics.Behaviour.OverwatchSec) - 1;   // 오래전 사격 = 엄호 아님
        var stale = w.AssaultClearance(1, cen, range, tgt, ch, ch.Count, now);
        var fb = w.AssaultClearance(1, cen, range, tgt, ch, ch.Count, now - Ticks(defs.Tactics.Behaviour.AssaultFallbackSec));
        var b = defs.Tactics.Behaviour;
        b.AssaultGate = false;
        AssaultWhy off;
        try { off = w.AssaultClearance(1, cen, range, tgt, ch, ch.Count, now); }
        finally { b.AssaultGate = true; }
        check(none == AssaultWhy.Blocked, $"돌격 허가: 신호 · 엄호 없으면 막힘 ({none})");
        check(sig == AssaultWhy.Signal, $"돌격 허가: 보이는 적 제압 ≥ superioritySupp = 신호 ({sig})");
        check(weak == AssaultWhy.Blocked, $"돌격 허가: 신호가 있어도 병력비 (돌격 3 대 적 6 × {b.AssaultMinOdds}) 미달이면 막힘 ({weak})");
        check(ow == AssaultWhy.Overwatch && stale == AssaultWhy.Blocked, $"돌격 허가: 다른 아군이 최근 {b.OverwatchSec}초 안에 표적 둘레를 쏘면 엄호 ({ow}), 오래전 사격은 아님 ({stale})");
        check(fb == AssaultWhy.Fallback && off == AssaultWhy.Ungated, $"돌격 허가: 교전 {b.AssaultFallbackSec}초 뒤 예비 타이머 ({fb}), assaultGate false = 옛 동작 ({off})");
    }

    /// <summary>엔진 기동조 돌격 (TryDrillCharge): 적이 돌격 거리 안이어도 신호 · 엄호가 없으면 돌격하지 않고, 제압되면 돌격.</summary>
    static void TestDrillChargeBlocked()
    {
        var w = Bare();
        var us = Spawn(w, 24, new Vector2(60, 66), 1, cols: 12);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 70)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        var foe = Spawn(w, 6, new Vector2(60, 62), 2);
        w.Hold(1, c.Members); w.Hold(2, foe);
        w.Step();
        foreach (var u in c.Members) { u.Target = null; u.LastShotTick = -1000; }
        c.Drill.PhaseTick = w.Tick;
        var men = c.Members.Where(u => !u.IsOfficer).ToList();
        Vector2 mc = new(men.Average(u => u.Pos.X), men.Average(u => u.Pos.Y));
        long blocked0 = w.Stats.AssaultBlocked;
        bool first = w.TryDrillCharge(c, men, mc, 6f, 24f, closeOnly: true);
        bool charging0 = men.Any(u => u.Charging);
        Pin(foe, 0.9f);
        bool second = w.TryDrillCharge(c, men, mc, 6f, 24f, closeOnly: true);
        check(!first && !charging0 && w.Stats.AssaultBlocked > blocked0, $"기동조 돌격: 적 4칸 (돌격 거리 안) 이어도 신호 · 엄호 없으면 돌격 안 함 (돌격 {first}, 막힘 {w.Stats.AssaultBlocked - blocked0})");
        check(second && c.Drill.Man == 4 && w.Stats.AssaultSignal >= 1, $"기동조 돌격: 적이 제압되면 (신호) 돌격 {second} (Man {c.Drill.Man})");
    }

    // ---------------------------------------------------------------- 결정 12: 대대 역할

    static (TacFaction S, TacBattalion A, TacBattalion B) Fac(params (int Bat, TacKind Kind, int Alive)[] groups)
    {
        var s = new TacFaction { Faction = 1 };
        var a = new TacBattalion { Id = 1, Point = new Vector2(40, 60), Role = TacRole.Line };
        var b = new TacBattalion { Id = 2, Point = new Vector2(80, 60), Role = TacRole.Line };
        s.Battalions.Add(a); s.Battalions.Add(b);
        int id = 1;
        foreach (var (bat, kind, alive) in groups) s.Groups.Add(new TacGroup { Id = id++, Kind = kind, Battalion = bat, Alive = alive, Initial = alive });
        return (s, a, b);
    }

    static void TestBattalionRoles()
    {
        var w = Bare();
        var ai = new TacticalAi();
        Vector2 enemy = new(60, 20);
        // 261명 소총 대대 + 1명 분견대 대대 (관찰 B 의 편성)
        var (s1, big, one) = Fac((1, TacKind.Line, 87), (1, TacKind.Line, 87), (1, TacKind.Line, 87), (2, TacKind.Line, 1));
        ai.AssignAttackRoles(w, s1, new List<TacBattalion> { big, one }, enemy);
        check(one.Role != TacRole.Manoeuvre && one.Role != TacRole.BaseOfFire, $"대대 역할: 1명 분견대 대대는 기동 · 화력 기반이 아님 ({one.Role})");
        check(big.Split && big.Role == TacRole.Line, $"대대 역할: 전투 대대가 하나뿐이면 대대 안에서 중대를 나눔 (분할 {big.Split}, 역할 {big.Role})");
        // 다시 불러도 (새 대대 검사) 1명 대대가 기동이 되지 않음 — 기존 화력 기반이 있어도
        big.Role = TacRole.BaseOfFire; big.Split = false;
        ai.AssignAttackRoles(w, s1, new List<TacBattalion> { big, one }, enemy);
        check(one.Role == TacRole.Line, $"대대 역할: 화력 기반이 이미 있어도 1명 대대는 기동으로 바뀌지 않음 ({one.Role})");
        // 전문 병과 (기관총) 만 있는 대대도 제외
        var (s2, big2, mg) = Fac((1, TacKind.Line, 150), (2, TacKind.Fire, 60));
        ai.AssignAttackRoles(w, s2, new List<TacBattalion> { big2, mg }, enemy);
        check(mg.Role == TacRole.Line && big2.Split, $"대대 역할: 화력 묶음만 있는 대대 제외 ({mg.Role}), 소총 대대 분할 {big2.Split}");
        // 전투 대대 둘: 하나 화력 기반 · 하나 기동 (분할 없음)
        var (s3, x, y) = Fac((1, TacKind.Line, 120), (2, TacKind.Line, 90));
        ai.AssignAttackRoles(w, s3, new List<TacBattalion> { x, y }, enemy);
        bool pair = (x.Role == TacRole.BaseOfFire && y.Role == TacRole.Manoeuvre) || (y.Role == TacRole.BaseOfFire && x.Role == TacRole.Manoeuvre);
        check(pair && !x.Split && !y.Split, $"대대 역할: 전투 대대 둘 = 화력 기반 + 기동 ({x.Role}/{y.Role})");
    }

    /// <summary>미션 ai_bn_assault (관찰 B): 접촉 뒤 한국군 261명 대대가 분할되고, 작은 대대는 기동 대대가 아님.</summary>
    static void TestMissionBattalionRoles()
    {
        var w = World.CreateMissionWorld(MissionDef.Load("ai_bn_assault"), defs, 1);
        World.AttachMissionAi(w, 0, all: true);
        var ai = w.Controllers.Select(c => c.Ai).OfType<TacticalAi>().FirstOrDefault(a => true)!;
        foreach (var (f, a) in w.Controllers) if (f == 1 && a is TacticalAi t) ai = t;
        TacBattalion? split = null; bool badMan = false; int maxTicks = Ticks(120);
        for (int i = 0; i < maxTicks; i++)
        {
            w.Step();
            if (i % 60 != 0 || ai.StateOf(1) is not { Phase: 2 } s) continue;
            foreach (var b in s.Battalions)
            {
                if (b.Split) split = b;
                if (b.Role is TacRole.Manoeuvre or TacRole.BaseOfFire && !TacticalAi.CombatBattalion(s, b, defs.Tactics.BattalionRoles)) badMan = true;
            }
            if (split != null && i > Ticks(30)) break;
        }
        check(split != null && !badMan, $"미션 B: 한국군 소총 대대 분할 {(split != null ? $"B{split.Id} {split.Alive}명" : "없음")} · 작은 대대가 기동/화력 기반 역할 {badMan} ({w.Tick}틱)");
    }

    // ---------------------------------------------------------------- 부사관 기본 편성

    static void TestDefaultNco()
    {
        var def = MissionDef.Load("ai_bn_assault");
        var w = World.CreateMissionWorld(def, defs, 1);
        int sq = 0, nco = 0, extra = 0;
        foreach (var c in w.Companies)
            foreach (var q in c.Squads)
            {
                if (q.Leader is not { } l || l.Def.Category != "infantry") continue;
                sq++; if (l.IsNco) nco++;
                if (c.Members.Count(u => u.IsOfficer) > 1) extra += q.Members.Count(u => u != l && u.IsNco);   // S2: 장교 ≤1 소형 중대는 부분대장 부사관 한 명 더
            }
        check(sq > 0 && nco == sq && extra == 0, $"부사관 기본 편성: 미션 보병 분대 {sq}개 중 분대장 부사관 {nco}, 분대장 외 부사관 {extra}");

        // 미션이 분대장 계급 0 또는 병력 계급 (rank) 을 정하면 기본 규칙 안 씀
        var def2 = MissionDef.Load("ai_bn_assault");
        var bn = def2.Forces.First(f => f.Id == "f1_bn");
        bn.Companies![0].SquadLeaderRank = 0;
        bn.Companies[1].Rank = 2;
        bn.Companies[2].SquadLeaderRank = 6;
        var w2 = World.CreateMissionWorld(def2, defs, 1);
        var cos = w2.Companies.Where(c => c.Owner == 1 && c.Battalion != null && c.Members.Count > 80).OrderBy(c => c.Id).ToList();
        string Desc(Company c) => string.Join(",", c.Squads.Select(q => q.Leader?.Rank ?? -1));
        bool off = cos.Count == 3 && cos[0].Squads.All(q => q.Leader is not { IsNco: true });
        bool ranked = cos.Count == 3 && cos[1].Squads.All(q => q.Leader is { Rank: 2 });
        bool six = cos.Count == 3 && cos[2].Squads.All(q => q.Leader is { Rank: 6 });
        check(off && ranked && six, $"부사관 기본 편성: squadLeaderRank 0 = 없음 {off} [{(cos.Count > 0 ? Desc(cos[0]) : "-")}], 병력 rank 2 = 기본 규칙 안 씀 {ranked} [{(cos.Count > 1 ? Desc(cos[1]) : "-")}], squadLeaderRank 6 {six} [{(cos.Count > 2 ? Desc(cos[2]) : "-")}]");
    }
}
