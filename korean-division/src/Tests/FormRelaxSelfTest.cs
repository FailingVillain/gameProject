using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 대형 완화 (formation 2026-10-21, src/Core/FormRelax.cs): 짧은 이동 평행이동 · 즉시 출발 · 긴 이동은 보통 대형 · 산개 파상 횡대 모양 · 비접적 휴식 정렬 (접적 중엔 안 함) ·
/// 끔 (formRelax false) 이면 옛 동작. 창 없이. KD_SELFTEST_ONLY=formrelax 로 단독 실행.
/// </summary>
public static class FormRelaxSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[대형 완화 · 산개 파상]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestLineWaves();
            TestShortShift();
            TestLongMove();
            TestRelaxOff();
            TestRestAlign();
        }
        catch (Exception e) { check(false, $"formrelax 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 120)
    {
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
    static FormSpec Line(Vector2 dir) => new((int)FormType.Line, 0, dir);
    static Vector2 Cen(IEnumerable<Unit> us) { var l = us.ToList(); return l.Aggregate(Vector2.Zero, (s, u) => s + u.Pos) / l.Count; }

    static void Idle(World w, Company c, List<Unit> us, int max, bool contact = false)
    {
        for (int t = 0; t < max && !(c.Anchor == null && us.All(u => u.Order == UnitOrder.Idle)); t++) { if (contact) c.ContactTick = w.Tick; w.Step(); }
    }

    /// <summary>소총 n + 장교 중대를 (60,80) 둘레에 북쪽 (−Y) 을 향해 세운 상태 (첫 이동 = 보통 깃발 대형).</summary>
    static (World W, Company C, List<Unit> All, List<Unit> Men) Stationed(int n = 24)
    {
        var w = Bare();
        var us = new List<Unit>();
        for (int i = 0; i < n; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % 12 - 5.5f), 88 + i / 12)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 91)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        w.Move(1, us, new Vector2(60, 80), Line(new Vector2(0, -1)));
        Steps(w, 2);
        Idle(w, c, us, 60 * 40);
        return (w, c, us, us.Where(u => !u.IsOfficer).OrderBy(u => u.Id).ToList());
    }

    static void TestValues()
    {
        var cr = defs.CombatRules;
        check(cr.FormRelax && cr.ShortMoveDist is >= 15 and <= 20 && cr.RelaxMoveTolerance is >= 1.5f and <= 2f && cr.FormDeadband is > 0 and < 0.5f
              && cr.FormArriveZone > cr.FormArriveSlack && cr.RestAlignSec > 0 && cr.NarrowExitFrac < cr.NarrowBlockFrac && defs.Formations.Company.ContainsKey("line_waves"),
            $"대형 완화 수치 (balance.json): 켬 {cr.FormRelax}, 짧은 이동 {cr.ShortMoveDist}칸 · {cr.ShortMoveFaceDeg}도, 행군 오차 {cr.RelaxMoveTolerance}칸 · 데드밴드 {cr.FormDeadband}, 구역 도착 {cr.FormArriveZone}칸, 휴식 정렬 {cr.RestAlignSec}초, 종대 복귀 {cr.NarrowExitFrac:P0} (< 진입 {cr.NarrowBlockFrac:P0}), 횡대 = '{defs.Formations.LineShape}' (산개 파상 기본 끔 — 사용자 결정 대기)");
    }

    static void TestLineWaves()
    {
        var fr = new FormationRules { LineShape = "line_waves" };   // 기본 끔 — 켰을 때 모양
        float s = 0.95f;
        var w90 = OrgLayout.CompanySlots(FormType.Line, 90, s, 0, fr);
        var ys = w90.Select(p => MathF.Round(p.Y * 10)).Distinct().Count();
        float width = w90.Max(p => p.X) - w90.Min(p => p.X);
        float gap = w90.Select(p => p.Y).Distinct().OrderByDescending(y => y).Take(2).Aggregate((a, b) => a - b);
        var w20 = OrgLayout.CompanySlots(FormType.Line, 20, s, 0, fr);
        int r20 = w20.Select(p => MathF.Round(p.Y * 10)).Distinct().Count();
        var old = new FormationRules { LineShape = "" };
        var o90 = OrgLayout.CompanySlots(FormType.Line, 90, s, 0, old);
        int oRows = o90.Select(p => MathF.Round(p.Y * 10)).Distinct().Count();
        float oWidth = o90.Max(p => p.X) - o90.Min(p => p.X);
        check(ys == 3 && width < oWidth && gap > s * 2.5f && r20 == 2 && oRows == 2,
            $"산개 파상 횡대: 90명 = {ys}줄 (옛 {oRows}줄) · 폭 {width:0.0}칸 (옛 {oWidth:0.0}) · 줄 사이 {gap:0.00}칸 (> 간격 × 2.5), 20명 = {r20}줄");
    }

    static void TestShortShift()
    {
        // 같은 방향으로 10칸 전진: 평행이동 (재배정 없음) — 깃발 첫 틱부터 제 속도, 도착 뒤 자기 자리 (무게중심 기준) 가 그대로, 걸은 거리 ≈ 이동 거리
        var (w, c, all, men) = Stationed();
        // 대열을 조금 흐트러뜨림 (병사 몇 명 옆으로 0.8칸) — 평행이동은 이 배치를 그대로 옮긴다
        for (int i = 0; i < men.Count; i += 5) men[i].Pos += new Vector2(0.8f, 0);
        w.Step();
        var cen0 = Cen(men);
        var off0 = men.ToDictionary(u => u.Id, u => u.Pos - cen0);
        var p0 = men.ToDictionary(u => u.Id, u => u.Pos);
        long sh0 = w.Stats.ShortShifts;
        w.Move(1, all, Cen(all) + new Vector2(0, -10), Line(new Vector2(0, -1)));
        w.Step();
        bool shifted = w.Stats.ShortShifts - sh0 == 1;
        bool fast = c.Anchor is { } a && a.SpeedMul >= 0.99f;
        Idle(w, c, all, 60 * 30);
        var cen1 = Cen(men);
        float err = men.Average(u => Vector2.Distance(u.Pos - cen1, off0[u.Id]));
        float walked = men.Average(u => Vector2.Distance(p0[u.Id], u.Pos));
        float adv = cen0.Y - cen1.Y;
        check(shifted && fast && adv > 8.5f && err < 0.5f && walked < 11.5f,
            $"짧은 이동 평행이동: 평행이동 {shifted}, 즉시 제 속도 {fast}, 전진 {adv:0.0}칸, 자기 자리 오차 평균 {err:0.00}칸 (< 0.5), 병사당 걸은 거리 {walked:0.0}칸 (< 11.5)");
    }

    static void TestLongMove()
    {
        var (w, c, all, men) = Stationed();
        long sh0 = w.Stats.ShortShifts;
        w.Move(1, all, Cen(all) + new Vector2(0, -30), Line(new Vector2(0, -1)));
        w.Step();
        bool shifted = w.Stats.ShortShifts != sh0;
        bool slots = c.Anchor is { } a && a.Slots.Count == all.Count;
        check(!shifted && slots, $"긴 이동 (30칸) = 보통 깃발 대형 (평행이동 아님 {!shifted}, 슬롯 배정 {slots})");
    }

    static void TestRelaxOff()
    {
        var cr = defs.CombatRules;
        bool was = cr.FormRelax;
        cr.FormRelax = false;
        try
        {
            var (w, c, all, men) = Stationed();
            long sh0 = w.Stats.ShortShifts;
            w.Move(1, all, Cen(all) + new Vector2(0, -10), Line(new Vector2(0, -1)));
            w.Step();
            bool shifted = w.Stats.ShortShifts != sh0;
            bool waits = c.Anchor is { } a && a.SpeedMul < 0.99f;
            check(!shifted && waits, $"대형 완화 끔 (formRelax false): 짧은 이동도 옛 깃발 대형 (평행이동 없음 {!shifted}, 부드러운 출발 {waits})");
        }
        finally { cr.FormRelax = was; }
    }

    static void TestRestAlign()
    {
        // 서 있는 중대에서 병사 3명을 대열 밖 3.5칸 (N12 문턱 2.75 위) 으로 옮김 → 휴식 restAlignSec 뒤 한 번 정렬 (돌아감). 접적 중이면 정렬 안 함.
        foreach (bool contact in new[] { false, true })
        {
            var (w, c, all, men) = Stationed();
            var moved = new[] { men[2], men[9], men[17] };
            var home = moved.ToDictionary(u => u.Id, u => u.Pos);
            var slots = men.Select(u => u.Pos).ToList();   // 대열 자리 전체 (정렬은 줄을 바꿔 배정할 수 있다 — 뒤로 밀린 앞줄 병사가 뒷줄 자리로)
            foreach (var u in moved) u.Pos += new Vector2(0, 3.5f);
            long r0 = w.Stats.RestAligns;
            int secs = (int)MathF.Ceiling(defs.CombatRules.RestAlignSec) + 6;
            for (int t = 0; t < 60 * secs; t++) { if (contact) c.ContactTick = w.Tick; w.Step(); }
            long aligns = w.Stats.RestAligns - r0;
            float back = moved.Average(u => MathF.Abs(u.Pos.Y - home[u.Id].Y));   // 자기 옛 자리 앞뒤 오차 (접적 = 그대로인지)
            float near = moved.Average(u => slots.Min(s => Vector2.Distance(u.Pos, s)));   // 가장 가까운 대열 자리까지
            if (!contact)
                check(aligns >= 1 && near < 1.2f, $"비접적 휴식 정렬: {defs.CombatRules.RestAlignSec}초 휴식 뒤 정렬 {aligns}회, 밀려난 병사 → 가장 가까운 대열 자리 평균 {near:0.00}칸 (< 1.2, 옛 자리 앞뒤 {back:0.00})");
            else
                check(aligns == 0 && back > 3f, $"접적 중에는 휴식 정렬 안 함: 정렬 {aligns}회, 밀려난 병사 그대로 ({back:0.00}칸)");
        }
    }
}
