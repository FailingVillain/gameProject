using System.Numerics;
using System.Reflection;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// F1 2차 (ContactF2.cs, docs/ISSUES_2026-10-20.md 4~6단계): 제자리 방향 전환 · 접적 중 선까지 전진 · 대형 유지 명령 · 접적 AI 재명령 제한. 창 없이. KD_SELFTEST_ONLY=f2 로 단독 실행.
/// </summary>
public static class F2SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("f2", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[F1 2차 회전 · 선까지 전진]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestRotateInPlace();
            TestRotateInContact();
            TestLooseAdvance();
            TestKeepFormation();
            TestAiReorder();
            TestSnapshot();
        }
        catch (Exception e) { check(false, $"f2 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

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

    /// <summary>소총 n (+ 장교) 중대를 (60,60) 에 북쪽 (−Y) 을 향해 횡대로 세운 상태.</summary>
    static (World W, Company C, List<Unit> Men) Stationed(int n = 20, int cols = 10)
    {
        var w = Bare();
        var us = new List<Unit>();
        for (int i = 0; i < n; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % cols - (cols - 1) * 0.5f), 70 + i / cols)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 72)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        w.Move(1, us, new Vector2(60, 60), Line(new Vector2(0, -1)));
        Steps(w, 2);
        Idle(w, c, us, 60 * 40);
        return (w, c, us.Where(u => !u.IsOfficer).OrderBy(u => u.Id).ToList());
    }

    static void Idle(World w, Company c, List<Unit> us, int max, bool contact = false)
    {
        for (int t = 0; t < max && !(c.Anchor == null && us.All(u => u.Order == UnitOrder.Idle)); t++) { if (contact) c.ContactTick = w.Tick; w.Step(); }
    }

    static float FacingErr(Unit u, Vector2 dir)
    {
        float want = DetMath.Atan2(dir.Y, dir.X);
        float d = MathF.Abs(u.Facing - want) % (2 * MathF.PI);
        return MathF.Min(d, 2 * MathF.PI - d);
    }

    // ---------------------------------------------------------------- 시험

    static void TestValues()
    {
        var cr = defs.CombatRules;
        check(cr.RotateInPlace && cr.RotateInPlaceDist > 0 && cr.RotateMinDeg is > 0 and < 90 && cr.RotateStayRadius > 0 && cr.LooseAdvance && cr.LooseDepth > 0
              && cr.AiContactReorderDist > 0 && cr.AiContactReorderSec > 0 && !cr.ContactArriveZone,
            $"F1 2차 수치 (balance.json): 제자리 전환 {cr.RotateInPlaceDist}칸 · {cr.RotateMinDeg}도 · 제자리 {cr.RotateStayRadius}칸, 선까지 전진 앞뒤 {cr.LooseDepth}칸, AI 재명령 {cr.AiContactReorderDist}칸 · {cr.AiContactReorderSec}초, contactArriveZone 끔");
    }

    static void TestRotateInPlace()
    {
        // 북쪽 횡대 → 같은 자리에서 동쪽: 깃발 행군 없음 (바로 도착), 대형을 통째로 돌릴 때보다 훨씬 적게 걷고, 일부는 제자리에서 방향만
        var (w, c, men) = Stationed();
        var all = c.Members.ToList();
        var cen = Cen(men);
        var p0 = men.ToDictionary(u => u.Id, u => u.Pos);
        // 강체 회전 (무게중심 둘레 90°) 이었다면 걸었을 거리
        float rigid = men.Sum(u => { var o = u.Pos - cen; return Vector2.Distance(u.Pos, cen + new Vector2(-o.Y, o.X)); });
        long rot0 = w.Stats.Rotations, stay0 = w.Stats.RotateStays;
        w.Move(1, all, cen, Line(new Vector2(1, 0)));
        w.Step();
        bool arrived = c.Anchor is { Arrived: true };
        Idle(w, c, all, 60 * 30);
        float walked = men.Sum(u => Vector2.Distance(p0[u.Id], u.Pos));
        long stays = w.Stats.RotateStays - stay0;
        int stillSame = men.Count(u => Vector2.Distance(p0[u.Id], u.Pos) < 0.05f);
        float maxErr = men.Max(u => FacingErr(u, new Vector2(1, 0)));
        check(w.Stats.Rotations - rot0 == 1 && arrived && stays >= 1 && stillSame >= stays && walked < rigid * 0.85f && maxErr < 0.2f && c.Facing == new Vector2(1, 0),
            $"제자리 방향 전환 (북 → 동): 깃발 즉시 도착 {arrived}, 제자리 병사 {stays} (안 움직임 {stillSame}), 걸은 거리 {walked:0} < 강체 회전 {rigid:0} × 0.85, 모두 동쪽 (최대 오차 {maxErr:0.00} rad)");
    }

    static void TestRotateInContact()
    {
        // 접적 중 방향 전환: 새 슬롯 구역 반경 안 병사는 모두 제자리, 구역 중심 = 새 슬롯
        var (w, c, men) = Stationed();
        var all = c.Members.ToList();
        for (int t = 0; t < 30; t++) { c.ContactTick = w.Tick; w.Step(); }
        var cen = Cen(men);
        var p0 = men.ToDictionary(u => u.Id, u => u.Pos);
        long stay0 = w.Stats.RotateStays;
        w.Move(1, all, cen, Line(new Vector2(1, 0)));
        c.ContactTick = w.Tick; w.Step();
        Idle(w, c, all, 60 * 20, contact: true);
        long stays = w.Stats.RotateStays - stay0;
        int moved = men.Count(u => Vector2.Distance(p0[u.Id], u.Pos) > 0.6f);
        bool zones = men.Where(u => Vector2.Distance(p0[u.Id], u.Pos) < 0.6f).All(u => u.HasZone && Vector2.Distance(u.ZoneCenter, u.Pos) <= w.Defs.CombatRules.ContactZoneRadius + 0.6f);
        check(stays >= men.Count / 2 && moved <= men.Count - stays + 2 && zones,
            $"접적 중 방향 전환: 구역 안 병사 {stays}/{men.Count} 제자리 (걸은 병사 {moved}), 제자리 병사의 구역 중심 = 새 슬롯 (구역 반경 안) {zones}");
    }

    static void TestLooseAdvance()
    {
        // 접적 중 이동 = 선까지 전진: 지금 배치 (좌우 순서 · 앞뒤) 그대로 앞으로, 대형을 다시 짜지 않음
        var (w, c, men) = Stationed();
        var all = c.Members.ToList();
        for (int i = 0; i < men.Count; i += 3) men[i].Pos += new Vector2(0, i % 2 == 0 ? -1.5f : 1.5f);   // 배치를 흩뜨림 (몇 명을 앞뒤로)
        for (int t = 0; t < 30; t++) { c.ContactTick = w.Tick; w.Step(); }
        var cen = Cen(men);
        var off0 = men.ToDictionary(u => u.Id, u => u.Pos - cen);
        long la0 = w.Stats.LooseAdvances;
        w.Move(1, all, cen + new Vector2(0, -12), Line(new Vector2(0, -1)));
        c.ContactTick = w.Tick; w.Step();
        Idle(w, c, all, 60 * 40, contact: true);
        var cen1 = Cen(men);
        float err = men.Average(u => Vector2.Distance(u.Pos - cen1, off0[u.Id]));
        var byX0 = men.OrderBy(u => off0[u.Id].X).Select(u => u.Id).ToList();
        var byX1 = men.OrderBy(u => u.Pos.X).Select(u => u.Id).ToList();
        int sameOrder = byX0.Zip(byX1).Count(p => p.First == p.Second);
        float adv = cen.Y - cen1.Y;
        check(w.Stats.LooseAdvances - la0 == 1 && adv > 9f && err < 1.2f && sameOrder >= men.Count * 0.6f,
            $"접적 중 선까지 전진: 선까지 전진 {w.Stats.LooseAdvances - la0}회, 전진 {adv:0.0}칸, 배치 오차 평균 {err:0.00}칸 (< 1.2), 좌우 순서 유지 {sameOrder}/{men.Count}");
    }

    static void TestKeepFormation()
    {
        // 대형 유지 명령 (리플레이 기록): 켜면 접적 중에도 깃발 대형 (선까지 전진 안 함), 남의 중대는 못 바꿈
        var (w, c, men) = Stationed();
        var all = c.Members.ToList();
        w.SetKeepFormation(1, new[] { c.Id }, true);
        w.SetKeepFormation(2, new[] { c.Id }, false);   // 다른 세력 — 무시
        w.Step();
        bool on = c.KeepFormation;
        bool logged = w.CommandLog.Any(k => k.Command is SetKeepFormationCommand { On: true } kc && kc.Companies.Contains(c.Id));
        for (int t = 0; t < 30; t++) { c.ContactTick = w.Tick; w.Step(); }
        long la0 = w.Stats.LooseAdvances;
        w.Move(1, all, Cen(men) + new Vector2(0, -12), Line(new Vector2(0, -1)));
        c.ContactTick = w.Tick; w.Step();
        bool formation = w.Stats.LooseAdvances == la0 && c.Anchor != null;
        w.SetKeepFormation(1, new[] { c.Id }, false);
        w.Step();
        check(on && logged && formation && !c.KeepFormation,
            $"대형 유지 명령: 켜짐 {on} · 리플레이 기록 {logged} · 접적 중에도 깃발 대형 {formation} · 다른 세력 무시 · 끄기 {!c.KeepFormation}");
    }

    static void TestAiReorder()
    {
        // 전술 AI Retarget: 접적 중대에는 aiContactReorderDist 칸 · aiContactReorderSec 초 넘어야 같은 동작 재명령
        var (w, c, men) = Stationed();
        var m = typeof(TacticalAi).GetMethod("Retarget", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cr = w.Defs.CombatRules;
        var g = new TacGroup { CompanyId = c.Id, State = TacState.Approach, OrderPoint = new Vector2(60, 60), OrderTick = w.Tick - (long)((6f + defs.CombatRules.AiContactReorderSec) * 30) };   // 6초 넘고 aiContactReorderSec 안
        g.Center = new Vector2(60, 60);
        bool Re(Vector2 p) => (bool)m.Invoke(null, new object[] { w, g, TacState.Approach, p, 4f, true })!;
        c.ContactTick = -1_000_000;
        bool freeBefore = Re(new Vector2(60, 54));   // 비접적: 6칸 · 6초 넘음 = 재명령
        c.ContactTick = w.Tick;
        long h0 = w.Stats.AiContactHolds;
        bool heldNear = !Re(new Vector2(60, 54));    // 접적: 6칸 < 8칸 = 보류
        bool heldSoon = !Re(new Vector2(60, 40));    // 접적: 20칸이지만 aiContactReorderSec 안 = 보류
        g.OrderTick = w.Tick - (long)((cr.AiContactReorderSec + 1) * 60);
        bool freeLate = Re(new Vector2(60, 40));
        check(freeBefore && heldNear && heldSoon && freeLate && w.Stats.AiContactHolds - h0 == 2,
            $"접적 AI 재명령 제한: 비접적 6칸 재명령 {freeBefore} · 접적 6칸 보류 {heldNear} · 접적 간격 안 보류 {heldSoon} · {cr.AiContactReorderSec}초 뒤 재명령 {freeLate}");
    }

    static void TestSnapshot()
    {
        var (x, c, men) = Stationed();
        x.SetKeepFormation(1, new[] { c.Id }, true);
        x.Step();
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool field = y.Companies.First(k => k.Id == c.Id).KeepFormation;
        var h0 = x.ComputeChecksum();
        c.KeepFormation = false; bool h1 = x.ComputeChecksum() != h0; c.KeepFormation = true;
        Steps(x, 300); Steps(y, 300);
        check(same && field && h1 && x.ComputeChecksum() == y.ComputeChecksum(),
            $"F1 2차 스냅샷: 저장 바이트 동일 {same} · 대형 유지 필드 {field} · 체크섬 반영 {h1} · 300틱 이어 돌려도 같은 체크섬 (형식 {Snapshot.FormatVersion})");
    }
}
