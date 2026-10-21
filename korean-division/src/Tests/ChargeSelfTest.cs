using System.Numerics;
using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.Tests;

/// <summary>
/// charge (2026-10-20): N46 분대 따라 쏘기 · N47 행진 사격 → 돌격 전환 (5칸 + 돌격 허가) · N57 착검 · 칼 뽑기 (시간 · 한 번 꽂으면 유지 · 저장).
/// KD_SELFTEST_ONLY=charge.
/// </summary>
public static class ChargeSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("charge", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[charge 분대 따라 쏘기 · 행진 돌격 허가 · 착검]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestFollowRange();
            TestFollowFireHold();
            TestMarchChargeGate();
            TestFixTimes();
            TestOrderedChargePause();
            TestAutoChargeSwitch();
            TestFixInterrupt();
            TestFixSnapshot();
        }
        catch (Exception e) { check(false, $"charge 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static List<Unit> Spawn(World w, int n, Vector2 c, int faction, string def = "infantry", int cols = 6, float gap = 1.1f)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++) list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * gap, (i / cols) * gap)));
        return list;
    }

    static int Ticks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));

    static (World W, Company C) Company(int n = 24, string def = "infantry")
    {
        var w = Bare();
        var us = Spawn(w, n, new Vector2(60, 70), 1, def, cols: 12);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 74)));
        w.FormCompany(1, us);
        w.Step();
        return (w, us[0].Company!);
    }

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var c = defs.CombatRules; var b = defs.Tactics.Behaviour;
        check(c.SquadFollowFireSec > 0 && c.MarchChargeRange == 5f, $"balance.json squadFollowFireSec {c.SquadFollowFireSec}초 · marchChargeRange {c.MarchChargeRange}칸 (N46 · N47)");
        check(c.BayonetFixSec is [>= 1f, <= 1.5f] && c.BayonetFixSec[0] <= c.BayonetFixSec[1] && c.BladeDrawSec is [>= 0.3f, <= 0.5f] && c.CavalryDrawSec == 0.5f
              && c.FixVeteranMul == 0.7f && c.FixRecruitMul > 1f,
            $"balance.json 착검 {c.BayonetFixSec[0]}~{c.BayonetFixSec[1]}초 (노병 ×{c.FixVeteranMul} · 신병 ×{c.FixRecruitMul}) · 칼 뽑기 {c.BladeDrawSec[0]}~{c.BladeDrawSec[1]}초 · 기병 군도 {c.CavalryDrawSec}초");
        check(c.SquadFollowFireAttackMove, $"balance.json squadFollowFireAttackMove {c.SquadFollowFireAttackMove} (N46b 사용자 결정: 대형 공격 이동도 따라 쏘기)");
        check(c.BayonetFixOrderedOnly && b.MarchChargeSince == "phase",
            $"스위치 기본값: bayonetFixOrderedOnly {c.BayonetFixOrderedOnly} (돌격 명령에만) · tactics.json marchChargeSince \"{b.MarchChargeSince}\" (전술 틀 국면 틱, 없으면 명령 틱)");
        check(Snapshot.FormatVersion >= 36 && LodSprite.UsedAnimNames.Contains("fix_bayonet"), $"Snapshot v{Snapshot.FormatVersion} (≥36 착검) · fix_bayonet 애니메이션 아틀라스 포함");
    }

    // ---------------------------------------------------------------- N46

    static void TestFollowRange()
    {
        var (w, c) = Company();
        var sq = c.Squads.FirstOrDefault(s => s.Members.Count(m => m.Alive && !m.IsOfficer) >= 2);
        if (sq == null) { check(false, "분대 따라 쏘기: 분대 없음"); return; }
        var a = sq.Members.First(m => !m.IsOfficer); var mate = sq.Members.First(m => !m.IsOfficer && !ReferenceEquals(m, a));
        var other = c.Members.FirstOrDefault(m => !m.IsOfficer && m.Squad != null && !ReferenceEquals(m.Squad, sq));
        foreach (var m in c.Members) m.LastShotTick = -1000;
        float react = w.ReactRange(a), range = a.Weapon!.Range;
        float r0 = w.FollowRange(a);
        mate.LastShotTick = w.Tick;
        float r1 = w.FollowRange(a), r1o = other != null ? w.FollowRange(other) : react;
        mate.LastShotTick = w.Tick - Ticks(defs.CombatRules.SquadFollowFireSec) - 1;
        float r2 = w.FollowRange(a);
        check(react < range && r0 == react && r1 == range && r1o == w.ReactRange(other ?? a) && r2 == react,
            $"분대 따라 쏘기 반경: 평소 {r0:0.0} (반응 거리 {react:0.0}) · 분대원이 방금 쏨 {r1:0.0} (사거리 {range}) · 다른 분대 {r1o:0.0} · {defs.CombatRules.SquadFollowFireSec}초 지남 {r2:0.0}");
    }

    static void TestFollowFireHold()
    {
        int Run(bool fire, out int otherSquad)
        {
            var (w, c) = Company();
            var sq = c.Squads.First(s => s.Members.Count(m => !m.IsOfficer) >= 2);
            var shooter = sq.Members.First(m => !m.IsOfficer);
            float react = w.ReactRange(shooter), range = shooter.Weapon!.Range;
            var foe = Spawn(w, 6, new Vector2(60, 70 - (react + range) / 2f - 1), 2);   // 반응 거리 밖 · 사거리 안
            w.Hold(1, c.Members); w.Hold(2, foe);
            w.Step();
            foreach (var m in c.Members) { m.Target = null; m.LastShotTick = -1000; }
            for (int t = 0; t < 90; t++) { if (fire) shooter.LastShotTick = w.Tick; foreach (var f in foe) { f.Target = null; f.Suppression = 0; } w.Step(); }
            otherSquad = c.Members.Count(m => m.Squad != null && !ReferenceEquals(m.Squad, sq) && m.Target != null && m.Alive);
            return sq.Members.Count(m => !ReferenceEquals(m, shooter) && m.Alive && m.Target != null);
        }
        int off = Run(false, out _), on = Run(true, out int otherOn);
        check(off == 0 && on > 0, $"분대 따라 쏘기 (사수): 반응 거리 밖 · 사거리 안 적 — 아무도 안 쏘면 표적 {off}명, 분대원이 쏘면 같은 분대 {on}명이 따라 표적 (다른 분대 {otherOn}명 — 그 분대에서 따라 쏜 결과일 수 있음)");
    }

    // ---------------------------------------------------------------- N47

    static void TestMarchChargeGate()
    {
        var b = defs.Tactics.Behaviour;
        bool Try(Func<World, Company, Unit, List<Unit>, bool> setup, out World w, out Unit u)
        {
            (w, var c) = Company();
            var foe = Spawn(w, 4, new Vector2(60, 70 - 4.5f), 2, cols: 4);   // 앞줄에서 약 4칸
            w.Hold(2, foe);
            w.Step();
            u = c.Members.Where(m => !m.IsOfficer).OrderBy(m => Vector2.Distance(m.Pos, foe[0].Pos)).First();
            foreach (var m in c.Members) { m.LastShotTick = -1000; m.Target = null; m.Suppression = 0; }
            if (!setup(w, c, u, foe)) return false;
            return w.MarchChargeNow(u);
        }
        // 막힘: 국면 막 시작 (예비 타이머 안) · 신호 · 엄호 없음
        long blk0 = 0;
        bool blocked = Try((w, c, u, foe) => { blk0 = w.Stats.MarchChargeBlocked; u.LastOrderTick = w.Tick; c.Drill.Phase = DrillPhase.Idle; return true; }, out var w1, out var u1);
        check(!blocked && !u1.Charging && w1.Stats.MarchChargeBlocked > blk0, $"행진 돌격 허가: 적 4칸 (5칸 안) 이어도 신호 · 엄호 없고 교전 직후면 막힘 (돌격 {blocked}, 막힘 +{w1.Stats.MarchChargeBlocked - blk0})");
        // 신호: 적 제압
        bool sig = Try((w, c, u, foe) => { u.LastOrderTick = w.Tick; foreach (var f in foe) f.Suppression = (short)(defs.CombatRules.SuppressMax * 0.9f); return true; }, out var w2, out var u2);
        check(sig && u2.Charging && u2.BayonetFixed && u2.FixLeft == 0 && w2.Stats.MarchChargeGated > 0,
            $"행진 돌격 허가: 적이 제압되면 (신호) 돌격 {sig} · 자동 돌격이라 착검 멈춤 없음 (착검 {u2.BayonetFixed}, 남은 {u2.FixLeft}틱)");
        // 예비 타이머: 국면 없음 → 명령 틱 기준 assaultFallbackSec 지남
        long fbTicks = Ticks(b.AssaultFallbackSec) + 5;
        bool fb = Try((w, c, u, foe) => { u.LastOrderTick = w.Tick - fbTicks; c.Drill.Phase = DrillPhase.Idle; return true; }, out _, out _);
        // 스위치: 국면 막 시작 (PhaseTick 최근) + 명령 틱 오래전 → "phase" 는 막힘, "order" 는 예비 타이머 통과
        bool PhaseCase(string mode)
        {
            string saved = b.MarchChargeSince; b.MarchChargeSince = mode;
            try { return Try((w, c, u, foe) => { u.LastOrderTick = w.Tick - fbTicks; c.Drill.Phase = DrillPhase.Engage; c.Drill.PhaseTick = w.Tick; return true; }, out _, out _); }
            finally { b.MarchChargeSince = saved; }
        }
        bool ph = PhaseCase("phase"), od = PhaseCase("order");
        check(fb && !ph && od, $"행진 돌격 예비 타이머 ({b.AssaultFallbackSec}초): 국면 없고 명령 틱 오래전 = 돌격 {fb} · 국면 막 시작 + marchChargeSince phase = 돌격 {ph} · order = 돌격 {od}");
        // 거리: 6칸 밖은 허가와 무관하게 안 함
        bool far;
        {
            var (w, c) = Company();
            var foe = Spawn(w, 4, new Vector2(60, 70 - 7f), 2, cols: 4);
            w.Hold(2, foe); w.Step();
            var u = c.Members.Where(m => !m.IsOfficer).OrderBy(m => Vector2.Distance(m.Pos, foe[0].Pos)).First();
            foreach (var f in foe) f.Suppression = (short)(defs.CombatRules.SuppressMax * 0.9f);
            float d = foe.Min(f => f.EdgeDistance(u.Pos) - u.Radius);
            far = w.MarchChargeNow(u) || d <= defs.CombatRules.MarchChargeRange;
        }
        check(!far, $"행진 돌격 거리: 적이 marchChargeRange {defs.CombatRules.MarchChargeRange}칸 밖이면 돌격 안 함");
    }

    // ---------------------------------------------------------------- N57

    static Unit WithRank(World w, string def, Vector2 p, Func<int, bool> want)
    {
        var u = w.SpawnUnit(defs.Units[def], 1, p);
        for (int k = 0; k <= 2000; k++) { u.GrantKills(k); if (want(u.Rank)) return u; }
        return u;
    }

    static void TestFixTimes()
    {
        var w = Bare(); var c = defs.CombatRules;
        int lo = Ticks(c.BayonetFixSec[0]), hi = Ticks(c.BayonetFixSec[1]);
        var reg = Enumerable.Range(0, 12).Select(i => WithRank(w, "infantry", new Vector2(20 + i * 2, 20), r => r >= c.CloseRegularRank && r < c.CloseVeteranRank)).ToList();
        var regT = reg.Select(w.FixTicks).ToList();
        var vet = WithRank(w, "infantry", new Vector2(20, 30), r => r >= c.CloseVeteranRank);
        var rec = WithRank(w, "infantry", new Vector2(24, 30), r => r < c.CloseRegularRank);
        float raw(Unit u) => w.FixTicks(u) / (u.Rank >= c.CloseVeteranRank ? c.FixVeteranMul : u.Rank < c.CloseRegularRank ? c.FixRecruitMul : 1f);
        check(regT.All(t => t >= lo && t <= hi) && regT.Distinct().Count() > 1 && regT.SequenceEqual(reg.Select(w.FixTicks)),
            $"착검 시간 (정규 소총병 12명): {regT.Min()}~{regT.Max()}틱 (기준 {lo}~{hi}틱), 병사마다 다르고 고정");
        check(raw(vet) >= lo - 1 && raw(vet) <= hi + 1 && w.FixTicks(vet) < hi && raw(rec) >= lo - 1 && raw(rec) <= hi + 1 && w.FixTicks(rec) > lo,
            $"착검 숙련: 노병 {w.FixTicks(vet)}틱 (×{c.FixVeteranMul}) · 신병 {w.FixTicks(rec)}틱 (×{c.FixRecruitMul})");
        var blade = w.SpawnUnit(defs.Units["infantry_dadao_fengtian"], 1, new Vector2(30, 30));
        var officer = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(32, 30));
        var cav = w.SpawnUnit(defs.Units["cavalry_default"], 1, new Vector2(36, 30));
        int bl = Ticks(c.BladeDrawSec[0]), bh = Ticks(c.BladeDrawSec[1]);
        check(w.FixTicks(blade) >= bl && w.FixTicks(blade) <= bh && w.FixTicks(officer) >= bl && w.FixTicks(officer) <= bh && w.FixTicks(cav) == Ticks(c.CavalryDrawSec),
            $"칼 뽑기: 대도 {w.FixTicks(blade)}틱 · 장교 군도 {w.FixTicks(officer)}틱 (기준 {bl}~{bh}) · 기병 군도 {w.FixTicks(cav)}틱 ({c.CavalryDrawSec}초)");
    }

    /// <summary>명령 돌격: 반응 지연 뒤 착검 시간만큼 제자리 → 달림. 두 번째 돌격은 착검 유지라 바로. 선택 1명 (N37 최대 단축) 이어도 착검 시간 그대로.</summary>
    static void TestOrderedChargePause()
    {
        var w = Bare();
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 60));
        int fix = w.FixTicks(u);
        int StandTicks(Vector2 dest)
        {
            var p0 = u.Pos;
            w.Issue(new ChargeCommand(1, new[] { u.Id }, dest, 0) { Sel = 1 });
            for (int t = 0; t < 400; t++) { w.Step(); if (Vector2.Distance(u.Pos, p0) > 0.01f) return t; }
            return 999;
        }
        long fx0 = w.Stats.BayonetFixes;
        int first = StandTicks(new Vector2(40, 30));
        bool fixedAfter = u.BayonetFixed && u.FixLeft == 0;
        for (int t = 0; t < 30; t++) w.Step();
        w.Stop(1, new[] { u }); for (int t = 0; t < 10; t++) w.Step();
        int second = StandTicks(new Vector2(70, 30));
        check(first >= fix && first <= fix + 4 && fixedAfter && w.Stats.BayonetFixes == fx0 + 1,
            $"명령 돌격 착검: 착검 {fix}틱 ({fix * Config.SimDt:0.00}초) 뒤 출발 — 제자리 {first}틱 (반응 지연 포함, 선택 1명 N37 단축 제외), 착검 유지 {fixedAfter}");
        check(second <= 4 && u.BayonetFixed, $"한 번 꽂으면 유지: 두 번째 돌격은 {second}틱 만에 출발 (착검 {u.BayonetFixed})");
    }

    /// <summary>자동 돌격 (적이 1칸 안): 기본 (돌격 명령에만) = 멈춤 없이 착검 처리, bayonetFixOrderedOnly false = 자동 돌격도 멈춤.</summary>
    static void TestAutoChargeSwitch()
    {
        (bool Charged, int Left, bool Fixed) Run(bool orderedOnly)
        {
            var c = defs.CombatRules; bool saved = c.BayonetFixOrderedOnly; c.BayonetFixOrderedOnly = orderedOnly;
            try
            {
                var w = Bare();
                var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60, 60));
                var e = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 61.2f));
                w.AttackMove(1, new[] { u }, new Vector2(60, 50));
                w.Stop(2, new[] { e });
                for (int t = 0; t < 120 && !u.Charging; t++) { e.Target = null; w.Step(); }
                return (u.Charging, u.FixLeft, u.BayonetFixed);
            }
            finally { c.BayonetFixOrderedOnly = saved; }
        }
        var a = Run(true); var b = Run(false);
        check(a.Charged && a.Left == 0 && a.Fixed, $"자동 돌격 (기본 bayonetFixOrderedOnly true): 멈춤 없음 (남은 {a.Left}틱) · 착검 처리 {a.Fixed}");
        check(b.Charged && b.Left > 0 && !b.Fixed, $"자동 돌격 (bayonetFixOrderedOnly false): 착검 멈춤 {b.Left}틱");
    }

    static void TestFixInterrupt()
    {
        var w = Bare();
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 60));
        w.Charge(1, new[] { u }, new Vector2(40, 30));
        for (int t = 0; t < 10; t++) w.Step();
        bool mid = u.FixLeft > 0;
        w.Move(1, new[] { u }, new Vector2(60, 60));
        w.Step();
        check(mid && u.FixLeft == 0 && !u.BayonetFixed && !u.Charging, $"착검 중 새 명령: 착검 취소 (남은 {u.FixLeft}틱 · 착검 {u.BayonetFixed})");
    }

    static void TestFixSnapshot()
    {
        var w = Bare();
        var men = Spawn(w, 6, new Vector2(40, 60), 1);
        w.Charge(1, men.Take(3), new Vector2(40, 30));
        for (int t = 0; t < 8; t++) w.Step();
        men[5].BayonetFixed = true;
        bool state = men.Take(3).All(m => m.FixLeft > 0) && men[5].BayonetFixed;
        int left = men.Take(3).Sum(m => m.FixLeft);
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var ym = men.Select(m => y.Units.First(k => k.Id == m.Id)).ToList();
        bool fields = men.Zip(ym).All(p => p.First.FixLeft == p.Second.FixLeft && p.First.BayonetFixed == p.Second.BayonetFixed);
        bool cs = w.ComputeChecksum() == y.ComputeChecksum();
        for (int t = 0; t < 120; t++) { w.Step(); y.Step(); }
        bool after = w.ComputeChecksum() == y.ComputeChecksum() && ym.Take(3).All(m => m.BayonetFixed);
        ulong h0 = y.ComputeChecksum(); ym[4].BayonetFixed = !ym[4].BayonetFixed; ulong h1 = y.ComputeChecksum(); ym[4].BayonetFixed = !ym[4].BayonetFixed;
        check(state && same && fields && cs && after && h0 != h1,
            $"착검 저장: 착검 중 3명 {left}틱 남음 · 착검 유지 — 저장 → 불러오기 → 저장 바이트 동일 {same}, 필드 {fields}, 체크섬 {cs}, 120틱 뒤 같음 {after}, 체크섬에 반영 {h0 != h1} (형식 {Snapshot.FormatVersion})");
    }
}
