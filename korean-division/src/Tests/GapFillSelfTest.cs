using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 대열 정비 (gapfill, 2026-10-19 사용자 결정): 플레이어 명령 · 장교 판단 (문턱 · 히스테리시스) · 병사 숙련 (신병 곧장 / 노병 교전 우선) · 저장 · 리플레이.
/// gapfill2: 앞줄 구멍 판단 (흐트러짐만으로는 명령 없음) · 대상 넓히기 (사수 · 공격 이동, 돌격 · 참호 · 승무원 제외) · 적 돌격 감지 즉시 판단 (재명령 간격 지킴).
/// 창 없이. KD_SELFTEST_ONLY=gapfill 로 단독 실행.
/// </summary>
public static class GapFillSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("gapfill", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[대열 정비 (gapfill)]");
        using var nd = TestDefs.NoCommandDelay();
        // F1 (2026-10-20): 접적 중에는 장교 대열 정비 판단을 하지 않는다 (Contact.cs) — 이 묶음은 대열 정비 규칙 자체를 보므로 contactLoose 를 끄고 돈다
        bool loose = defs.CombatRules.ContactLoose;
        defs.CombatRules.ContactLoose = false;
        try
        {
            TestValues();
            TestManualCommand();
            TestOfficerThreshold();
            TestHysteresis();
            TestScope();
            TestChargeReaction();
            TestSkill();
            TestSnapshot();
            TestReplay();
        }
        catch (Exception e) { check(false, $"gapfill 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
        finally { defs.CombatRules.ContactLoose = loose; }
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

    /// <summary>소총 n (cols 열) (+ 장교 1) 중대를 남쪽 (y+) 으로 20칸 옮겨 세운 상태. 앞 = +y.</summary>
    static (World W, Company C, List<Unit> Men) Stationed(bool officer = true, bool record = false, int n = 20, int cols = 10, bool mg = false)
    {
        var w = Bare();
        w.RecordCommands = record;
        var us = new List<Unit>();
        if (mg) us.Add(w.SpawnUnit(defs.Units["heavy_maxim"], 1, new Vector2(60, 37)));
        for (int i = 0; i < n; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % cols - (cols - 1) * 0.5f), 40 + i / cols)));
        if (officer) us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 38)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        w.Move(1, us, new Vector2(60, 60));
        Steps(w, 2);
        for (int t = 0; t < 60 * 40 && !(c.Anchor == null && us.All(u => u.Order == UnitOrder.Idle)); t++) w.Step();
        return (w, c, us.Where(u => !u.IsOfficer && !u.Def.Heavy).OrderBy(u => u.Id).ToList());
    }

    /// <summary>뒷줄 (정면 반대쪽) 병사부터 정렬.</summary>
    static List<Unit> RearFirst(Company c, List<Unit> men) => men.OrderBy(u => Vector2.Dot(u.Pos, c.Facing)).ThenBy(u => u.Pos.X).ThenBy(u => u.Id).ToList();

    /// <summary>n 틱 동안 firer 가 계속 쏘는 것으로 (중대 교전 중) 돌린다.</summary>
    static void Fight(World w, Unit firer, int n, params Unit[] more)
    {
        for (int i = 0; i < n; i++) { firer.LastShotTick = w.Tick; foreach (var u in more) u.LastShotTick = w.Tick; w.Step(); }
    }

    static int Sec(float s) => (int)MathF.Ceiling(s * 60);

    static Vector2 Centroid(List<Unit> us) { var s = Vector2.Zero; foreach (var u in us) s += u.Pos; return s / Math.Max(1, us.Count); }

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var cr = defs.CombatRules;
        bool tables = cr.CloseObey.Length == 3 && cr.CloseDelaySec.Length == 3 && cr.CloseObey[0] >= cr.CloseObey[1] && cr.CloseObey[1] >= cr.CloseObey[2]
            && cr.CloseDelaySec[0] <= cr.CloseDelaySec[1] && cr.CloseDelaySec[1] <= cr.CloseDelaySec[2] && cr.CloseObey[0] >= 0.999f;
        check(tables && cr.CloseHoleFrac is > 0 and < 1 && cr.CloseHoleGap >= 2 && cr.CloseHoleLat is > 0 and <= 1 && cr.CloseChargeThrMul is > 0 and <= 1 && cr.CloseChargePollSec < cr.CloseCheckSec && cr.CloseOnFrac > 0.66f && cr.CloseHysteresis is > 0 and < 1 && cr.CloseSlack > cr.FormArriveSlack && cr.CloseCheckSec > 0 && cr.CloseWindowSec >= cr.CloseDelaySec[2] && cr.CloseVeteranRank > cr.CloseRegularRank,
            $"대열 정비 수치 (balance.json): 점검 {cr.CloseCheckSec}초 · 앞줄 구멍 max({cr.CloseHoles}, {cr.CloseHoleFrac:P0}) · 연속 {cr.CloseHoleGap} · 보조 {cr.CloseOnFrac:P0} · 돌격 {cr.CloseChargeRange}칸 ×{cr.CloseChargeThrMul} · 히스테리시스 ×{cr.CloseHysteresis} · 유효 {cr.CloseWindowSec}초 · 숙련 곧장 {string.Join("/", cr.CloseObey)} · 지연 {string.Join("/", cr.CloseDelaySec)}초 (신병 곧장 = 1)");
        var w = Bare();
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30, 30));
        var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(32, 30));
        var v = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(34, 30));
        b.GrantKills(cr.RankKills[cr.CloseRegularRank - 1]);
        v.GrantKills(cr.RankKills[cr.CloseVeteranRank - 1]);
        check(w.SkillOf(a) == RankSkill.Recruit && w.SkillOf(b) == RankSkill.Regular && w.SkillOf(v) == RankSkill.Veteran,
            $"숙련 = 사병 계급: 계급 {a.Rank} 신병 · {b.Rank} 정규 · {v.Rank} 노병 ({w.SkillOf(a)}/{w.SkillOf(b)}/{w.SkillOf(v)})");
    }

    // ---------------------------------------------------------------- ① 플레이어 명령

    static void TestManualCommand()
    {
        // 장교 없는 중대 (장교 판단 · 빈자리 점검 없음): 6명을 뒤로 4칸 흩뜨려 두고 교전 중. 명령한 쪽만 다시 자리를 잡는다.
        (World W, Company C, List<Unit> Men, List<Unit> Moved, Vector2[] At) Make(bool order)
        {
            var (w, c, men) = Stationed(officer: false, record: true);
            var rear = RearFirst(c, men);
            var moved = new List<Unit>();
            for (int i = 0; i < 6; i++) { var u = rear[i * 2 % 10 + i / 5]; u.Pos -= c.Facing * 4f; moved.Add(u); }
            var at = moved.Select(u => u.Pos).ToArray();
            var firer = rear[^1];
            if (order) w.CloseRanks(1, men.Take(3));   // 분대 · 일부만 골라도 중대 전체
            Fight(w, firer, Sec(8));
            return (w, c, men, moved, at);
        }
        var (w1, c1, men1, mv1, at1) = Make(true);
        var (w2, _, _, mv2, at2) = Make(false);
        float back1 = mv1.Select((u, i) => Vector2.Dot(u.Pos - at1[i], c1.Facing)).Min();
        float back2 = mv2.Select((u, i) => Vector2.Distance(u.Pos, at2[i])).Max();
        var d1 = w1.MeasureRanks(c1);
        var log = w1.CommandLog.Where(r => r.Command is CloseRanksCommand).ToList();
        string json = log.Count > 0 ? JsonSerializer.Serialize<GameCommand>(log[0].Command, CommandJson.Options) : "";
        var back = json.Length > 0 ? JsonSerializer.Deserialize<GameCommand>(json, CommandJson.Options) : null;
        check(back1 > 2.5f && back2 < 0.3f && d1.DispFrac < 0.01f && w1.Stats.CloseManual == 1 && w1.Stats.CloseMovers >= 6,
            $"대열 정비 명령 (교전 중, 장교 없음): 흩어진 6명이 앞으로 {back1:0.0}칸 이상 돌아가 다시 자리 (흐트러짐 {d1.DispFrac:P0}), 명령 없는 대조군은 그대로 (이동 {back2:0.00}칸)");
        check(log.Count == 1 && back is CloseRanksCommand cr && cr.Companies.SequenceEqual(new[] { c1.Id }) && json.Contains("\"closeranks\""),
            $"대열 정비 = 명령 경로 (WorldCommands → 리플레이 기록 {log.Count}개, JSON {json})");
    }

    // ---------------------------------------------------------------- ② 장교 판단 (gapfill2: 앞줄 구멍)

    /// <summary>중대 정면 축 옆 좌표 (오른쪽 +).</summary>
    static float Lat(Company c, Vector2 p) => Vector2.Dot(p, new Vector2(-c.Facing.Y, c.Facing.X));

    /// <summary>옆 좌표 중앙값에서 halfWidth 안의 병사 (앞뒤 줄 모두) 를 정면 반대로 back 칸 옮겨 앞줄에 구멍을 낸다. 옮긴 병사.</summary>
    static List<Unit> Hole(Company c, List<Unit> men, float halfWidth, float back)
    {
        var lat = men.Select(u => Lat(c, u.Pos)).OrderBy(v => v).ToList();
        float mid = lat[lat.Count / 2];
        var moved = men.Where(u => MathF.Abs(Lat(c, u.Pos) - mid) <= halfWidth).ToList();
        foreach (var u in moved) u.Pos -= c.Facing * back;
        return moved;
    }

    /// <summary>n 틱 동안 firer 가 계속 쏘고 (중대 교전 중), pins 는 반응 지연으로 제자리에 묶어 둔다 (대열 정비 대상 아님).</summary>
    static void FightPinned(World w, Unit firer, int n, List<Unit> pins)
    {
        for (int i = 0; i < n; i++) { firer.LastShotTick = w.Tick; foreach (var u in pins) u.ReactDelay = 3; w.Step(); }
        foreach (var u in pins) u.ReactDelay = 0;
    }

    static void TestOfficerThreshold()
    {
        var cr = defs.CombatRules;
        int checks = Sec(cr.CloseCheckSec * 2.2f);
        // 문턱 아래: 뒷줄 2명만 뒤로 4칸 — 장교는 명령하지 않고, 교전 중이라 빈자리 점검도 없다
        {
            var (w, c, men) = Stationed();
            var rear = RearFirst(c, men);
            rear[0].Pos -= c.Facing * 4f; rear[3].Pos -= c.Facing * 4f;
            var at = new[] { rear[0].Pos, rear[3].Pos };
            var d = w.MeasureRanks(c);
            Fight(w, rear[^1], checks);
            float moved = MathF.Max(Vector2.Distance(rear[0].Pos, at[0]), Vector2.Distance(rear[3].Pos, at[1]));
            check(!w.RanksBroken(d, 1f) && d.Holes == 0 && w.Stats.CloseOrders == 0 && !c.CloseAlarm && moved < 0.3f,
                $"장교 판단 문턱 아래 (앞줄 구멍 {d.Holes}/{d.FrontSlots} · 연속 {d.HoleGap}, 벗어난 병사 {d.DispFrac:P0}): 명령 없음 ({w.Stats.CloseOrders}), 병사 그대로 (이동 {moved:0.00}칸)");
        }
        // 흐트러짐만 (gapfill2): 뒷줄 10명 (50%) 을 앞뒤로 4칸씩 — 옛 문턱 35% 는 넘지만 앞줄에 구멍이 없으니 장교는 명령하지 않는다
        {
            var (w, c, men) = Stationed();
            var rear = RearFirst(c, men).Take(10).ToList();
            for (int i = 0; i < rear.Count; i++) rear[i].Pos += c.Facing * (i % 2 == 0 ? 4.5f : -4f);
            var d = w.MeasureRanks(c);
            Fight(w, RearFirst(c, men)[^1], checks + Sec(6));
            check(d.DispFrac >= 0.35f && d.DispFrac < cr.CloseOnFrac && d.Holes == 0 && !w.RanksBroken(d, 1f) && w.Stats.CloseOrders == 0 && !c.CloseAlarm,
                $"흐트러짐만으로는 명령 없음: 벗어난 병사 {d.DispFrac:P0} (옛 문턱 35% 넘음, 보조 문턱 {cr.CloseOnFrac:P0} 아래) · 앞줄 구멍 {d.Holes} → 명령 {w.Stats.CloseOrders}");
        }
        // 앞줄 구멍: 40명 (20열 2줄) 중대 가운데 열들을 앞뒤 줄 모두 6칸 뒤로 — 구멍 지표가 잡고 장교가 정비를 명령, 구멍이 메워진다
        {
            var (w, c, men) = Stationed(n: 40, cols: 20);
            var moved = Hole(c, men, 3.6f, 6f);
            var d = w.MeasureRanks(c);
            Fight(w, RearFirst(c, men)[^1], checks + Sec(8));
            var d2 = w.MeasureRanks(c);
            check(w.RanksBroken(d, 1f) && d.HoleGap >= cr.CloseHoleGap && w.Stats.CloseOrders >= 1 && w.Stats.CloseMovers >= moved.Count / 2 && !w.RanksBroken(d2, cr.CloseHysteresis),
                $"앞줄 구멍 판단: 가운데 {moved.Count}명 이탈 → 구멍 {d.Holes}/{d.FrontSlots} · 연속 {d.HoleGap} (문턱 max({cr.CloseHoles}, {cr.CloseHoleFrac:P0}) / {cr.CloseHoleGap}) → 명령 {w.Stats.CloseOrders} · 이동 {w.Stats.CloseMovers}명 → 구멍 {d2.Holes} · 연속 {d2.HoleGap}");
        }
    }

    static void TestHysteresis()
    {
        var cr = defs.CombatRules;
        // 판단 함수: 켜기 문턱 · 해제 문턱 (× closeHysteresis) 사이에서는 '켜기 아님 · 해제 아님'
        int slots = 20;
        float on = MathF.Max(cr.CloseHoles, cr.CloseHoleFrac * slots);
        var w0 = Bare();
        var hi = new RankDistortion(0.3f, 0, 0, 40, (int)MathF.Ceiling(on), 1, 0, slots);
        var mid = new RankDistortion(0.3f, 0, 0, 40, (int)MathF.Ceiling(on * (1 + cr.CloseHysteresis) / 2f), 1, 0, slots);
        var lo = new RankDistortion(0.3f, 0, 0, 40, 0, 0, 0, slots);
        var gap = new RankDistortion(0.3f, 0, 0, 40, 1, cr.CloseHoleGap, 0, slots);
        check(w0.RanksBroken(hi, 1f) && !w0.RanksBroken(mid, 1f) && w0.RanksBroken(mid, cr.CloseHysteresis) && !w0.RanksBroken(lo, cr.CloseHysteresis) && w0.RanksBroken(gap, 1f),
            $"앞줄 구멍 문턱 (앞줄 {slots}): 구멍 {hi.Holes} 켜짐 · {mid.Holes} 켜기 아님 / 해제 아님 (×{cr.CloseHysteresis}) · 0 해제, 연속 구멍 {cr.CloseHoleGap} 만으로도 켜짐");
        // 실제 판단: 병사를 제자리에 묶어 (반응 지연) 구멍 크기를 조절 — 큼 → 켜짐, 중간 → 그대로 켜짐, 없음 → 꺼짐, 다시 중간 → 꺼진 채
        var (w, c, men) = Stationed(n: 40, cols: 20);
        var firer = RearFirst(c, men)[^1];
        var home = men.ToDictionary(u => u.Id, u => u.Pos);
        List<Unit> Make(float half)
        {
            foreach (var u in men) u.Pos = home[u.Id];
            return Hole(c, men, half, 6f);
        }
        int iv = Sec(cr.CloseCheckSec * 1.6f);
        Make(3.6f); var f1 = w.MeasureRanks(c); FightPinned(w, firer, iv, men);   // 전원 묶음 — 이웃 병사가 옆으로 메우지 않게 (지표만 조절)
        bool on1 = c.CloseAlarm; long o1 = w.Stats.CloseOrders;
        Make(2.6f); var f2 = w.MeasureRanks(c); FightPinned(w, firer, iv, men);
        bool on2 = c.CloseAlarm;
        Make(-1f); var f3 = w.MeasureRanks(c); FightPinned(w, firer, iv, men);
        bool on3 = c.CloseAlarm; long o3 = w.Stats.CloseOrders;
        Make(2.6f); FightPinned(w, firer, Sec(cr.CloseCooldownSec + cr.CloseCheckSec * 2), men);
        bool on4 = c.CloseAlarm; long o4 = w.Stats.CloseOrders;
        check(on1 && o1 >= 1 && w.RanksBroken(f1, 1f) && !w.RanksBroken(f2, 1f) && w.RanksBroken(f2, cr.CloseHysteresis) && on2 && !on3 && !on4 && o4 == o3,
            $"히스테리시스: 구멍 {f1.Holes}·연속 {f1.HoleGap} 켜짐 {on1} (명령 {o1}) → {f2.Holes}·{f2.HoleGap} 켜짐 유지 {on2} → {f3.Holes}·{f3.HoleGap} 꺼짐 {!on3} → 다시 {f2.Holes}·{f2.HoleGap} 꺼진 채 {!on4} (그동안 새 명령 {o4 - o3})");
        // 경보 중 재명령은 closeCooldownSec 간격 (묶인 병사는 움직이지 않아 경보가 계속 켜져 있음)
        var (w5, c5, men5) = Stationed(n: 40, cols: 20);
        var pins5 = Hole(c5, men5, 3.6f, 6f);
        int span = Sec(cr.CloseCooldownSec * 2.5f);
        FightPinned(w5, RearFirst(c5, men5)[^1], span, pins5);
        long expect = (long)MathF.Floor(cr.CloseCooldownSec * 2.5f / cr.CloseCooldownSec) + 1;
        check(c5.CloseAlarm && w5.Stats.CloseOrders >= 2 && w5.Stats.CloseOrders <= expect,
            $"경보 중 재명령 간격 {cr.CloseCooldownSec}초: {cr.CloseCooldownSec * 2.5f:0}초 동안 명령 {w5.Stats.CloseOrders}번 (최대 {expect}) — 매 점검마다 명령하지 않음");
    }

    // ---------------------------------------------------------------- gapfill2: 대상 넓히기 · 공격 이동 · 제외 · 돌격 감지

    static void TestScope()
    {
        var cr = defs.CombatRules;
        // 대상 판정: 대기 · 사수 · (그룹 없는) 공격 이동 = 대상, 돌격 · 참호 · 중화기 승무원 · 다른 그룹 이동 = 제외
        {
            var (w, c, men) = Stationed(officer: false);
            var idle = men[0]; var hold = men[1]; var chg = men[2]; var tr = men[3]; var spot = men[4]; var mv = men[5];
            hold.Order = UnitOrder.Hold;
            chg.ChargeState = 2;
            tr.InTrench = true;
            spot.TrenchSpot = 0;
            mv.Order = UnitOrder.Move; mv.Group = new MoveGroup(w, mv.Pos + c.Facing * 5f, 1);
            var mg = w.SpawnUnit(defs.Units["heavy_maxim"], 1, men[6].Pos + c.Facing * -3f);
            bool ok = w.CloseCandidate(c, idle, false) && w.CloseCandidate(c, hold, false)
                && !w.CloseCandidate(c, chg, false) && !w.CloseCandidate(c, tr, false) && !w.CloseCandidate(c, spot, false) && !w.CloseCandidate(c, mv, false) && !w.CloseCandidate(c, mg, false);
            chg.ChargeState = 0; tr.InTrench = false; spot.TrenchSpot = -1; mv.Group = null; mv.Order = UnitOrder.Idle;
            check(ok, "대열 정비 대상: 대기 · 사수 = 대상, 돌격 · 참호 안 · 참호 자리 · 다른 이동 · 중화기 승무원 = 제외");
        }
        // 사수 병사 (교전 중 정지 — gapfill 1차는 대기 병사만) 도 플레이어 대열 정비로 슬롯에 돌아온다. 같은 자리의 돌격 · 참호 · 승무원은 그대로
        {
            var (w, c, men) = Stationed(officer: false, mg: true);
            var rear = RearFirst(c, men).Where(u => u.Role is not (UnitRole.Fire or UnitRole.Support)).ToList();   // tweak1: 화력조 · 지원 역할은 정비 대상 아님
            var holds = new[] { rear[0], rear[2], rear[4] };
            foreach (var u in holds) { u.Order = UnitOrder.Hold; u.Pos -= c.Facing * 4f; }
            var tr = rear[6]; tr.Pos -= c.Facing * 4f; tr.InTrench = true;
            var mg = c.Members.First(u => u.Def.Heavy);
            mg.Pos -= c.Facing * 5f;
            var c2 = c;
            Vector2 trAt = tr.Pos, mgAt = mg.Pos;
            var hAt = holds.Select(u => u.Pos).ToArray();
            w.CloseRanks(1, men.Take(2));
            Fight(w, rear[^1], Sec(6));
            float back = holds.Select((u, i) => Vector2.Dot(u.Pos - hAt[i], c2.Facing)).Min();
            check(back > 2.5f && Vector2.Distance(tr.Pos, trAt) < 0.3f && Vector2.Distance(mg.Pos, mgAt) < 0.5f,
                $"사수 병사도 대열 정비 (앞으로 {back:0.0}칸 이상), 참호 안 병사 ({Vector2.Distance(tr.Pos, trAt):0.00}칸) · 중기관총 승무원 ({Vector2.Distance(mg.Pos, mgAt):0.00}칸) 은 그대로");
        }
        // 공격 이동 병사: 중대 공격 이동 대형 (도착한 깃발) 에서 표적을 잡고 서서 쏘던 병사 6명을 흩뜨린 뒤 대열 정비 → 공격 이동 그대로 슬롯으로 걸어가고, 닿으면 다시 싸운다
        {
            var (w, c, men) = Stationed(officer: false);
            var foe = w.SpawnUnit(defs.Units["officer"], 2, Centroid(men) + c.Facing * 15f);
            foe.Hp = 1e6f;
            float shortMove = cr.ShortMoveDist;
            cr.ShortMoveDist = 0;   // formation: 이 장면은 '도착한 공격 이동 깃발에 남아 싸우는 병사' 를 만든다 — 1.5칸 전진이 평행이동 (FormRelax) 이면 바로 도착해 깃발을 떠남
            w.AttackMove(1, men, Centroid(men) + c.Facing * 1.5f);
            for (int t = 0; t < Sec(20) && !(c.Anchor is { Arrived: true } && men.Count(u => u.Target != null) >= 10); t++) w.Step();   // 명령은 다음 Step 에서 실행되므로 (지연 큐) 끄기는 이 루프 뒤에 되돌린다
            cr.ShortMoveDist = shortMove;
            var a0 = c.Anchor;
            var rear = RearFirst(c, men);
            var hit = new List<Unit>();
            foreach (var u in rear) if (hit.Count < 6 && u.Order == UnitOrder.AttackMove && a0 != null && ReferenceEquals(u.Group, a0.Group)) hit.Add(u);
            foreach (var u in hit) u.Pos -= c.Facing * 4f;
            for (int t = 0; t < 30; t++) w.Step();   // 흩어진 자리에서도 표적을 잡고 서서 쏜다 (대형 구성원 공격 이동)
            var at = hit.Select(u => u.Pos).ToArray();
            bool fought = hit.Count(u => u.Target != null) >= 3;
            w.CloseRanks(1, men.Take(1));
            Steps(w, Sec(1));
            bool amove = hit.All(u => u.Order == UnitOrder.AttackMove);
            float midway = hit.Select((u, i) => Vector2.Distance(u.Pos, at[i])).Average();
            Steps(w, Sec(6));
            float went = hit.Select((u, i) => Vector2.Dot(u.Pos - at[i], c.Facing)).Min();
            Steps(w, Sec(3));
            int again = hit.Count(u => u.Target != null || w.Tick - u.LastShotTick < Sec(4));
            check(a0 is { Arrived: true } && hit.Count == 6 && fought && amove && midway > 0.5f && went > 2.5f && again >= 4 && w.Stats.CloseMoveAttack >= 6,
                $"공격 이동 병사 대열 정비: 흩어져 싸우던 {hit.Count}명 (표적 {fought}) → 1초 뒤 공격 이동 유지 {amove} · {midway:0.0}칸 걸음 → 슬롯으로 {went:0.0}칸 이상 → 다시 교전 {again}명 (공격 이동 그대로 보냄 {w.Stats.CloseMoveAttack})");
        }
    }

    static void TestChargeReaction()
    {
        var cr = defs.CombatRules;
        // 작은 구멍 (평소 문턱 아래, 돌격 문턱 × closeChargeThrMul 위) — 장교는 평소엔 명령하지 않고, 대도 돌격이 다가오면 점검 간격을 기다리지 않고 바로 명령
        (World W, Company C, List<Unit> Men, List<Unit> Pins, RankDistortion D) Setup()
        {
            var (w, c, men) = Stationed(n: 40, cols: 20);
            var pins = Hole(c, men, 1.6f, 6f);
            return (w, c, men, pins, w.MeasureRanks(c));
        }
        Unit Charger(World w, Company c, List<Unit> men)
        {
            var e = w.SpawnUnit(defs.Units["infantry_fengtian"], 2, Centroid(men) + c.Facing * 10f);
            e.Hp = 1e6f;
            w.Charge(2, new[] { e }, men[0].Pos, men[0]);
            return e;
        }
        // 대조: 돌격 없이 교전만 — 판단 간격 두 번이 지나도 명령 없음
        {
            var (w, c, men, pins, d) = Setup();
            FightPinned(w, RearFirst(c, men)[^1], Sec(cr.CloseCheckSec * 2.2f), pins);
            check(!w.RanksBroken(d, 1f) && w.RanksBroken(d, cr.CloseChargeThrMul) && w.Stats.CloseOrders == 0,
                $"작은 구멍 (구멍 {d.Holes}/{d.FrontSlots} · 연속 {d.HoleGap}): 평소 판단으로는 명령 없음 ({w.Stats.CloseOrders}), 돌격 문턱 (×{cr.CloseChargeThrMul}) 은 넘음");
        }
        // 돌격 감지: 대도 보병이 12칸 안에서 돌격해 오면 바로 판단 · 명령 (점검 간격 {closeCheckSec}초보다 훨씬 빨리)
        {
            var (w, c, men, pins, d) = Setup();
            long t0 = w.Tick;
            var e = Charger(w, c, men);
            long hitAt = -1;
            for (int t = 0; t < Sec(3) && hitAt < 0; t++) { foreach (var u in pins) u.ReactDelay = 3; w.Step(); if (w.Stats.CloseChargeOrders > 0) hitAt = w.Tick; }
            float sec = hitAt < 0 ? 99 : (hitAt - t0) / 60f;
            check(e.Charging || hitAt >= 0, $"(돌격 시작: {e.ChargeState})");
            check(hitAt >= 0 && sec <= cr.CloseChargePollSec + 1f && sec < cr.CloseCheckSec && c.CloseAt >= t0 && w.Stats.CloseChargeSeen >= 1,
                $"돌격 감지 즉시 판단: 대도 돌격 시작 {sec:0.00}초 뒤 대열 정비 명령 (감지 판단 {w.Stats.CloseChargeSeen}, 명령 {w.Stats.CloseChargeOrders}) — 점검 간격 {cr.CloseCheckSec}초를 기다리지 않음");
        }
        // 재명령 간격: 방금 (5초 전) 정비를 명령했으면 돌격이 와도 간격이 끝날 때까지 다시 명령하지 않는다
        {
            var (w, c, men, pins, d) = Setup();
            c.CloseAt = Math.Max(1, w.Tick - Sec(5));
            var e = Charger(w, c, men);
            long before = w.Stats.CloseOrders;
            int left = Sec(cr.CloseCooldownSec - 5) - 30;
            for (int t = 0; t < left; t++) { foreach (var u in pins) u.ReactDelay = 3; e.Hp = 1e6f; w.Step(); }
            long during = w.Stats.CloseOrders - before;
            check(during == 0 && w.Stats.CloseChargeSeen == 0,
                $"돌격 감지도 재명령 간격 {cr.CloseCooldownSec}초를 지킴: 간격 안 명령 {during} · 감지 판단 {w.Stats.CloseChargeSeen}");
        }
        // 감지 조건: 우군 돌격 · 멀어지는 적 · 12칸 밖 적은 감지하지 않음
        {
            var (w, c, men, _, _) = Setup();
            var far = w.SpawnUnit(defs.Units["infantry_fengtian"], 2, Centroid(men) + c.Facing * (cr.CloseChargeRange + 8f));
            far.ChargeState = 2; far.Velocity = -c.Facing;
            var own = w.SpawnUnit(defs.Units["infantry"], 1, Centroid(men) + c.Facing * 4f);
            own.ChargeState = 2;
            var away = w.SpawnUnit(defs.Units["infantry_fengtian"], 2, Centroid(men) + c.Facing * 6f);
            away.ChargeState = 2; away.Velocity = c.Facing * 3f;
            w.RefreshChargersForTest();
            bool none = !w.ChargeIncoming(c, cr.CloseChargeRange);
            away.Velocity = -c.Facing * 3f;
            w.RefreshChargersForTest();
            bool yes = w.ChargeIncoming(c, cr.CloseChargeRange);
            check(none && yes, $"돌격 감지 조건: 우군 · {cr.CloseChargeRange + 8:0}칸 밖 · 멀어지는 적 돌격 = 감지 안 함 ({none}), 다가오는 적 = 감지 ({yes})");
        }
    }

    // ---------------------------------------------------------------- ③ 병사 숙련

    static void TestSkill()
    {
        var cr = defs.CombatRules;
        var savedObey = cr.CloseObey;
        cr.CloseObey = new[] { 1f, 0.5f, 0f };   // 노병이 '곧장' 굴림에 걸리지 않게 (해시 굴림은 따로 결정성만 확인)
        try
        {
            // 신병 · 노병 둘 다 교전 중 (계속 사격) 이고 슬롯에서 4칸 뒤. 플레이어 대열 정비 → 신병은 곧장, 노병은 closeDelaySec 동안 싸우다가 따른다.
            var (w, c, men) = Stationed(officer: false);
            var rear = RearFirst(c, men);
            var rec = rear[0]; var vet = rear[3];
            vet.GrantKills(cr.RankKills[cr.CloseVeteranRank - 1]);
            rec.Pos -= c.Facing * 4f; vet.Pos -= c.Facing * 4f;
            Vector2 r0 = rec.Pos, v0 = vet.Pos;
            w.CloseRanks(1, men);
            float delay = cr.CloseDelaySec[(int)RankSkill.Veteran];
            Fight(w, rear[^1], Sec(MathF.Max(2f, delay * 0.5f)), rec, vet);
            float recEarly = Vector2.Distance(rec.Pos, r0), vetEarly = Vector2.Distance(vet.Pos, v0);
            long late = w.Stats.CloseLate;
            Fight(w, rear[^1], Sec(delay * 0.5f + 4f), rec, vet);
            float vetLater = Vector2.Distance(vet.Pos, v0);
            check(recEarly > 2f && vetEarly < 0.3f && late > 0 && vetLater > 2f,
                $"숙련: 교전 중 신병은 곧장 슬롯으로 ({recEarly:0.0}칸), 교전 중 노병은 {delay * 0.5f:0}초까지 제자리에서 싸움 ({vetEarly:0.00}칸, 미룬 횟수 {late}) → {delay}초 뒤 따름 ({vetLater:0.0}칸)");
            // 노병이라도 교전이 끝나면 (표적 없음 · regroupFireSec 넘게 안 쏨) 지연을 기다리지 않고 따른다
            var (w2, c2, men2) = Stationed(officer: false);
            var rear2 = RearFirst(c2, men2);
            var vet2 = rear2[3];
            vet2.GrantKills(cr.RankKills[cr.CloseVeteranRank - 1]);
            vet2.Pos -= c2.Facing * 4f;
            Vector2 q0 = vet2.Pos;
            w2.CloseRanks(1, men2);
            Fight(w2, rear2[^1], Sec(1), vet2);   // 1초 교전
            float still = Vector2.Distance(vet2.Pos, q0);
            Fight(w2, rear2[^1], Sec(cr.RegroupFireSec + 2.5f));   // 노병은 사격을 멈춤 (중대는 계속 교전 중)
            float went = Vector2.Distance(vet2.Pos, q0);
            check(still < 0.3f && went > 2f && cr.RegroupFireSec + 3.5f < delay + 1e-3f,
                $"노병: 교전 중엔 제자리 ({still:0.00}칸) → 사격을 멈추고 {cr.RegroupFireSec}초가 지나면 지연 ({delay}초) 전이라도 따름 ({went:0.0}칸)");
        }
        finally { cr.CloseObey = savedObey; }
        // 해시 굴림 결정성: 같은 조건 두 번 = 같은 따름 여부, 정규 50% 는 병사마다 갈린다
        bool[] Obey()
        {
            var (w, c, men) = Stationed(officer: false);
            foreach (var u in men) { u.GrantKills(cr.RankKills[cr.CloseRegularRank - 1]); u.LastShotTick = w.Tick; }
            w.CloseRanks(1, men);
            w.Step();
            foreach (var u in men) u.LastShotTick = w.Tick;
            return men.Select(u => w.CompliesWithClose(u, c)).ToArray();
        }
        var x = Obey(); var y = Obey();
        int yes = x.Count(b => b);
        check(x.SequenceEqual(y) && yes > 0 && yes < x.Length, $"숙련 굴림 결정적 (해시 — 병사 Id · 명령 틱 · 시드): 교전 중 정규 {x.Length}명 중 곧장 {yes}명, 두 번 같음");
    }

    // ---------------------------------------------------------------- 저장 · 리플레이

    static void TestSnapshot()
    {
        var (x, c, men) = Stationed(n: 40, cols: 20);
        Hole(c, men, 3.6f, 6f);
        var firer = RearFirst(c, men)[^1];
        for (int t = 0; t < Sec(defs.CombatRules.CloseCheckSec * 1.6f) && x.Stats.CloseOrders == 0; t++) { firer.LastShotTick = x.Tick; x.Step(); }
        for (int t = 0; t < 20; t++) { firer.LastShotTick = x.Tick; x.Step(); }   // 정비 깃발로 걸어가는 중에 저장
        bool state = c.CloseAlarm && c.CloseAt > 0 && c.CloseUntil > x.Tick && c.Anchor is { Close: true };
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var yc = y.Companies.First(k => k.Id == c.Id);
        check(state && same && x.ComputeChecksum() == y.ComputeChecksum() && yc.CloseAlarm == c.CloseAlarm && yc.CloseAt == c.CloseAt && yc.CloseUntil == c.CloseUntil && yc.Anchor is { Close: true },
            $"대열 정비 스냅샷: 경보 {c.CloseAlarm} · 명령 틱 {c.CloseAt} · 유효 {c.CloseUntil} · 정비 깃발 {c.Anchor?.Close} — 저장 → 불러오기 → 저장 바이트 동일, 체크섬 동일 (형식 {Snapshot.FormatVersion})");
        if (c.Anchor != null)
        {
            var h0 = x.ComputeChecksum();
            c.Anchor.Close = false;
            bool hashedA = x.ComputeChecksum() != h0;
            c.Anchor.Close = true;
            check(hashedA, "정비 깃발 표시 (Close) 가 체크섬에 들어감");
        }
        var c0 = x.ComputeChecksum();
        c.CloseAlarm = !c.CloseAlarm;
        bool hashed = x.ComputeChecksum() != c0;
        c.CloseAlarm = !c.CloseAlarm;
        Steps(x, 900); Steps(y, 900);
        check(hashed && x.ComputeChecksum() == y.ComputeChecksum(), $"대열 정비 상태가 체크섬에 들어감 ({hashed}), 불러온 월드를 900틱 이어 돌려도 같은 체크섬");
    }

    static void TestReplay()
    {
        // 기록한 명령 (JSON 직렬화 → 역직렬화) 을 같은 시작 상태의 새 월드에 같은 틱으로 넣으면 같은 체크섬
        (World W, Company C) Setup(bool record)
        {
            var (w, c, men) = Stationed(officer: true, record: record);
            var rear = RearFirst(c, men);
            for (int i = 0; i < 6; i++) rear[i].Pos -= c.Facing * 4f;
            return (w, c);
        }
        var (a, ca) = Setup(true);
        long start = a.Tick;
        int logStart = a.CommandLog.Count;
        Steps(a, 30);
        a.CloseRanks(1, ca.Members);
        Steps(a, 600);
        var recs = a.CommandLog.Skip(logStart).ToList();
        string json = JsonSerializer.Serialize(recs, CommandJson.Options);
        var back = JsonSerializer.Deserialize<List<CommandRecord>>(json, CommandJson.Options)!;
        var (b, _) = Setup(false);
        b.CommandFeed = t => back.Where(r => r.Tick == t).Select(r => r.Command);
        Steps(b, (int)(a.Tick - start));
        check(back.Exists(r => r.Command is CloseRanksCommand) && a.Tick == b.Tick && a.ComputeChecksum() == b.ComputeChecksum() && a.Stats.CloseManual == 1 && b.Stats.CloseManual == 1,
            $"리플레이 왕복: 대열 정비 명령 기록 → JSON → 재생 월드 체크섬 동일 ({a.ComputeChecksum():x16} / {b.ComputeChecksum():x16}), 리플레이 형식 {ReplayFile.FormatVersion}");
    }
}
