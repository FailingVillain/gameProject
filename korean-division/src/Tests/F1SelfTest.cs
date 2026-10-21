using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// F1 접적 상태 · 구역 · 사격 위치 고정 (Contact.cs, docs/ISSUES_2026-10-20.md 1~3단계). 창 없이. KD_SELFTEST_ONLY=f1 로 단독 실행.
/// </summary>
public static class F1SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("f1", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[F1 접적 · 사격 위치]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestContactStates();
            TestNoRefillInContact();
            TestArriveInZone();
            TestNoChase();
            TestSpotCover();
            TestCommitment();
            TestZoneReturn();
            TestSnapshot();
            TestDeterminism();
        }
        catch (Exception e) { check(false, $"f1 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Ceiling(s * 60);

    /// <summary>소총 n (+ 장교) 중대를 (60,40) 에서 (60,60) 으로 옮겨 세운 상태.</summary>
    static (World W, Company C, List<Unit> Men) Stationed(int n = 20, int cols = 10, Action<GameMap>? terrain = null)
    {
        var w = Bare();
        terrain?.Invoke(w.Map);
        var us = new List<Unit>();
        for (int i = 0; i < n; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % cols - (cols - 1) * 0.5f), 40 + i / cols)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 38)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        w.Move(1, us, new Vector2(60, 60));
        Steps(w, 2);
        for (int t = 0; t < 60 * 40 && !(c.Anchor == null && us.All(u => u.Order == UnitOrder.Idle)); t++) w.Step();
        return (w, c, us.Where(u => !u.IsOfficer).OrderBy(u => u.Id).ToList());
    }

    /// <summary>n 틱 동안 firer 가 총격을 받는 것으로 (중대 접적) 돌린다.</summary>
    static void UnderFire(World w, int n, params Unit[] us)
    {
        for (int i = 0; i < n; i++) { foreach (var u in us) u.LastFiredAtTick = w.Tick; w.Step(); }
    }

    // ---------------------------------------------------------------- 시험

    static void TestValues()
    {
        var cr = defs.CombatRules;
        check(cr.ContactLoose && cr.ContactTriggerSec > 0 && cr.ContactDisengageSec > cr.ContactTriggerSec && cr.ContactReformSec > 0 && cr.ContactZoneRadius is >= 4 and <= 6
              && cr.SpotSearchRadius > 0 && cr.SpotSearchRadius < cr.ContactZoneRadius && cr.SpotDwellSec > 0 && cr.SpotMoveMin > 0 && cr.SpotReleaseSupp is > 0 and <= 1,
            $"F1 수치 (balance.json): 트리거 {cr.ContactTriggerSec}초 · 이탈 {cr.ContactDisengageSec}초 · 재정렬 +{cr.ContactReformSec}초 · 구역 {cr.ContactZoneRadius}칸 · 찾기 {cr.SpotSearchRadius}칸 · 고정 {cr.SpotDwellSec}초 · 문턱 {cr.SpotMoveMin}칸 · 해제 제압 {cr.SpotReleaseSupp}");
    }

    static void TestContactStates()
    {
        var (w, c, men) = Stationed();
        bool none0 = w.ContactOf(c) == ContactState.None;
        UnderFire(w, 30, men[0]);
        bool contact = w.ContactOf(c) == ContactState.Contact && w.ContactLoose(c) && c.ContactSince > 0;
        Steps(w, Sec(w.Defs.CombatRules.ContactTriggerSec + w.Defs.CombatRules.ContactDisengageSec) + 20);
        bool dis = w.ContactOf(c) == ContactState.Disengage && w.ContactLoose(c);
        Steps(w, Sec(w.Defs.CombatRules.ContactReformSec) + 20);
        bool none1 = w.ContactOf(c) == ContactState.None && !w.ContactLoose(c) && men.All(u => !u.HasZone && u.SpotTick < 0);
        check(none0 && contact && dis && none1 && w.Stats.ContactEnters >= 1,
            $"접적 상태: 처음 비접적 {none0} → 피격 = 접적 {contact} → 조용 = 이탈 {dis} → 더 조용 = 비접적 · 구역 지움 {none1} (진입 {w.Stats.ContactEnters})");
    }

    static void TestNoRefillInContact()
    {
        var (w, c, men) = Stationed(n: 30);
        // 앞줄 셋 사망 (빈자리) · 남은 병사 넷을 슬롯에서 4칸 밀어 둔다
        var front = men.OrderByDescending(u => Vector2.Dot(u.Pos, c.Facing)).ThenBy(u => MathF.Abs(u.Pos.X - 60)).Take(3).ToList();
        foreach (var u in front) u.Hp = 0;
        w.Step();
        int holes = c.Holes;
        var pushed = men.Where(u => !front.Contains(u)).Take(4).ToList();
        foreach (var u in pushed) u.Pos += new Vector2(4, 0);
        c.ContactTick = w.Tick;   // 접적 (트리거는 없음 — 곧 이탈 · 비접적으로 넘어감)
        long r0 = w.Stats.GapRefills;
        int span = Sec(w.Defs.CombatRules.ContactDisengageSec + w.Defs.CombatRules.ContactReformSec) - 60;
        Steps(w, span);
        long during = w.Stats.GapRefills - r0;
        bool looseStill = w.ContactLoose(c) && c.Holes == holes;
        Steps(w, Sec(w.Defs.CombatRules.GapCheckSec * 2.5f));
        check(holes == 3 && looseStill && during == 0 && c.Holes == 0, $"접적 · 이탈 중 빈자리 점검 없음 (빈자리 {holes} 유지 {looseStill}, 재배치 {during}) — 비접적이 된 뒤 다시 짬 (남은 빈자리 {c.Holes}, 재배치 {w.Stats.GapRefills - r0}명)");
    }

    static void TestArriveInZone()
    {
        // 도착 깃발 판정 (FollowAnchor → ArriveInZone): 접적 중 교전하는 (최근 사격) 병사는 슬롯 둘레 구역 안이면 그 자리에서 도착 (구역 중심 = 슬롯)
        var cr = defs.CombatRules;
        bool saved = cr.ContactArriveZone;
        cr.ContactArriveZone = true;   // 기본 끔 (측정상 나쁨) — 켰을 때의 규칙만 검사
        try { ArriveInZoneOn(); } finally { cr.ContactArriveZone = saved; }
        check(!saved, "도착 깃발 구역 도착 (contactArriveZone) 기본 끔");
    }

    static void ArriveInZoneOn()
    {
        var (w, c, men) = Stationed();
        var u = men[3];
        float R = w.Defs.CombatRules.ContactZoneRadius;
        var slot = u.Pos + new Vector2(0, 3);
        bool quiet = !w.ArriveInZone(u, slot, 3);                 // 비접적
        c.ContactTick = w.Tick;
        bool idle = !w.ArriveInZone(u, slot, 3);                  // 접적이지만 교전 안 함 (표적 · 최근 사격 없음, 풀밭)
        u.LastShotTick = w.Tick;
        bool far = !w.ArriveInZone(u, slot, R + 1);               // 구역 밖
        bool behind = !w.ArriveInZone(u, slot, 3, new Vector2(0, 1));   // 슬롯이 정면 3칸 앞 = 뒤처짐 → 계속 걸음
        bool ok = w.ArriveInZone(u, slot, 3, new Vector2(0, -1)) && u.HasZone && u.ZoneCenter == slot;
        check(quiet && idle && far && behind && ok, $"접적 중 교전하는 병사는 구역 ({R}칸) 안에서 도착 — 비접적 아님 {quiet} · 교전 안 함 아님 {idle} · 구역 밖 아님 {far} · 정면 쪽 뒤처짐 아님 {behind} · 도착 + 구역 중심 = 슬롯 {ok}");
    }

    static void TestNoChase()
    {
        var w = Bare();
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 40));
        var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(42, 40));
        w.FormCompany(1, new List<Unit> { a, b });
        var e = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(40, 40 + a.Weapon!.Range + 6));
        e.Order = UnitOrder.Hold;
        w.Step();
        var c = a.Company!;
        var p0 = a.Pos;
        for (int t = 0; t < 240; t++) { c.ContactTick = w.Tick; a.Target = e; w.Step(); }
        float moved = Vector2.Distance(p0, a.Pos);
        check(moved < 1.0f, $"접적 중 대기 병사는 사거리 밖 표적을 쫓지 않음 (이동 {moved:0.00}칸)");
    }

    static void TestSpotCover()
    {
        // 잔해 칸 (엄폐) 이 2칸 옆에 있으면 접적 중 그 칸으로 옮겨 사격 위치를 잡는다
        var w = Bare();
        w.Map.Set(42, 40, TerrainType.Rubble);
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40.5f, 40.5f));
        var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30.5f, 40.5f));
        w.FormCompany(1, new List<Unit> { a, b });
        w.Step();
        var c = a.Company!;
        bool coverBetter = w.Map.TerrainCover(new Vector2(42.5f, 40.5f)).Hit < w.Map.TerrainCover(a.Pos).Hit - w.Defs.CombatRules.CoverSeekGain;
        for (int t = 0; t < 400; t++) { c.ContactTick = w.Tick; w.Step(); }
        var (tx, ty) = GameMap.TileOf(a.Pos);
        check(coverBetter && tx == 42 && ty == 40 && a.SpotTick >= 0 && w.Stats.SpotMoves >= 1,
            $"접적 중 구역 안 엄폐 칸으로 사격 위치 이동 · 고정 (칸 {tx},{ty}, 고정 {a.SpotTick >= 0}, 이동 {w.Stats.SpotMoves})");
    }

    static void TestCommitment()
    {
        // 고정한 병사는 고정 시간 안에 더 좋은 엄폐가 생겨도 움직이지 않고, 고정 시간 뒤 (표적 없음) 에는 옮긴다. 새 명령이면 해제.
        var w = Bare();
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40.5f, 40.5f));
        var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30.5f, 40.5f));
        w.FormCompany(1, new List<Unit> { a, b });
        w.Step();
        var c = a.Company!;
        for (int t = 0; t < 60; t++) { c.ContactTick = w.Tick; w.Step(); }
        bool committed = a.SpotTick >= 0;
        w.Map.Set(42, 40, TerrainType.Rubble);
        var p0 = a.Pos;
        int dwell = Sec(w.Defs.CombatRules.SpotDwellSec);
        for (int t = 0; t < dwell - 120; t++) { c.ContactTick = w.Tick; w.Step(); }
        bool stayed = Vector2.Distance(p0, a.Pos) < 0.3f;
        for (int t = 0; t < 400; t++) { c.ContactTick = w.Tick; w.Step(); }
        var (tx, _) = GameMap.TileOf(a.Pos);
        bool movedAfter = tx == 42;
        w.Move(1, new List<Unit> { a }, new Vector2(40, 50));
        w.Step();
        bool released = a.SpotTick < 0;
        check(committed && stayed && movedAfter && released, $"사격 위치 고정: 고정 {committed} · 고정 시간 안 그대로 {stayed} · 그 뒤 엄폐로 {movedAfter} · 새 명령 = 해제 {released}");
    }

    static void TestZoneReturn()
    {
        var (w, c, men) = Stationed();
        for (int t = 0; t < 60; t++) { c.ContactTick = w.Tick; w.Step(); }
        var u = men[5];
        bool zone = u.HasZone;
        var zc = u.ZoneCenter;
        float R = w.Defs.CombatRules.ContactZoneRadius;
        u.Pos = zc + new Vector2(R + 3, 0);
        for (int t = 0; t < 600; t++) { c.ContactTick = w.Tick; w.Step(); }
        float d = Vector2.Distance(u.Pos, zc);
        check(zone && d <= R + 0.6f && w.Stats.ZoneReturns >= 1 && u.ZoneCenter == zc, $"구역 밖으로 밀린 병사는 구역 안으로 돌아감 (중심 거리 {d:0.0} ≤ {R}, 복귀 {w.Stats.ZoneReturns}, 중심 유지 {u.ZoneCenter == zc})");
    }

    static void TestSnapshot()
    {
        var (x, c, men) = Stationed();
        UnderFire(x, 120, men[0]);
        bool state = x.ContactLoose(c) && men.Any(u => u.HasZone) && men.Any(u => u.SpotTick >= 0);
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var yc = y.Companies.First(k => k.Id == c.Id);
        bool fields = yc.ContactTick == c.ContactTick && yc.ContactSince == c.ContactSince && men.All(u => y.Units.First(v => v.Id == u.Id) is { } v && v.HasZone == u.HasZone && v.ZoneCenter == u.ZoneCenter && v.SpotTick == u.SpotTick);
        var h0 = x.ComputeChecksum();
        men[1].SpotTick += 1; bool h1 = x.ComputeChecksum() != h0; men[1].SpotTick -= 1;
        c.ContactTick += 1; bool h2 = x.ComputeChecksum() != h0; c.ContactTick -= 1;
        men[1].ZoneCenter += Vector2.One; bool h3 = x.ComputeChecksum() != h0; men[1].ZoneCenter -= Vector2.One;
        Steps(x, 600); Steps(y, 600);
        check(state && same && fields && h1 && h2 && h3 && x.ComputeChecksum() == y.ComputeChecksum(),
            $"F1 스냅샷: 상태 {state} · 저장 바이트 동일 {same} · 필드 {fields} · 체크섬 반영 {h1}/{h2}/{h3} · 600틱 이어 돌려도 같은 체크섬 (형식 {Snapshot.FormatVersion})");
    }

    static void TestDeterminism()
    {
        ulong Run()
        {
            var w = Bare();
            w.Map.Set(62, 58, TerrainType.Rubble); w.Map.Set(57, 59, TerrainType.Crater);
            var us = new List<Unit>();
            for (int i = 0; i < 16; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(56 + i % 8, 50 + i / 8)));
            w.FormCompany(1, us);
            var es = new List<Unit>();
            for (int i = 0; i < 12; i++) es.Add(w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(56 + i % 6 * 1.5f, 76 + i / 6)));
            w.FormCompany(2, es);
            w.Step();
            w.AttackMove(1, us, new Vector2(60, 60));
            w.Hold(2, es);
            Steps(w, 60 * 40);
            return w.ComputeChecksum();
        }
        var a = Run(); var b = Run();
        check(a == b, $"F1 결정성: 같은 교전 두 번 = 같은 체크섬 ({a:X16})");
    }
}
