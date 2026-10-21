using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N12 (2026-10-19 사용자 승인): 대형 강박 줄이기 · 장교 위치 · 정찰병 묶음에 장교 없음. 창 없이. KD_SELFTEST_ONLY=n12 로 단독 실행.
/// </summary>
public static class N12SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[N12 대형 강박 · 장교 위치 · 정찰병 묶음]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestNoReassignUnderThreshold();
            TestNoRegroupWhileFiringOrInCover();
            TestNoGapCheckInCombat();
            TestOfficerSlotBehind();
            TestOfficerDoesNotPassFront();
            TestScoutGroupNoOfficer();
            TestMissionObserversNoOfficer();
        }
        catch (Exception e) { check(false, $"N12 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static List<Unit> Grid(World w, int n, Vector2 c, int cols, string def = "infantry", int faction = 1)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++)
            list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * 1f, (i / cols) * 1f)));
        return list;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    /// <summary>소총 20 (10열) + 장교 1 중대를 남쪽 (y+) 으로 20칸 옮겨 세운 상태.</summary>
    static (World W, Company C, List<Unit> Us) Stationed(GameMap? map = null)
    {
        var w = Bare();
        var us = Grid(w, 20, new Vector2(60, 40), 10);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 38)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        w.Move(1, us, new Vector2(60, 60));
        Steps(w, 2);
        for (int t = 0; t < 60 * 40 && !(c.Anchor == null && us.All(u => u.Order == UnitOrder.Idle)); t++) w.Step();
        return (w, c, us);
    }

    /// <summary>점검 두 번이 지나가도록 (간격 gapCheckSec + 중대 Id 분산).</summary>
    static int TwoChecks() => (int)MathF.Ceiling(defs.CombatRules.GapCheckSec * 2.1f * 60);

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var cr = defs.CombatRules;
        var b = defs.Tactics.Behaviour;
        check(cr.GapCheckSec is >= 20 and <= 30 && cr.GapCheckMinSec >= 20 && cr.GapMoveMin is >= 2.5f and <= 3f && cr.FormArriveSlack > 0.3f && cr.FormMoveTolerance > 0 && cr.OfficerBack is >= 1 and <= 4,
            $"N12 수치 (balance.json): 점검 {cr.GapCheckSec}초 (하한 {cr.GapCheckMinSec}) · 재배정 {cr.GapMoveMin}칸 · 도착 여유 {cr.FormArriveSlack} · 행군 오차 {cr.FormMoveTolerance} · 장교 뒤 {cr.OfficerBack}칸");
        check(MathF.Abs(b.SuperioritySupp - 0.30f) < 1e-4f && MathF.Abs(b.PatienceSec - 90f) < 1e-4f,
            $"N17 수치 (tactics.json): superioritySupp {b.SuperioritySupp} (0.30, N25) · patienceSec {b.PatienceSec} (90)");
    }

    // ---------------------------------------------------------------- 대형 강박

    static void TestNoReassignUnderThreshold()
    {
        var (w, c, us) = Stationed();
        bool settled = c.Anchor == null && c.Facing != Vector2.Zero;
        var men = us.Where(u => !u.IsOfficer).OrderBy(u => u.Id).ToList();
        // 앞줄 가운데 2명 사망 → 빈자리 2. 한 명은 대열 뒤 1.8칸 (문턱 아래), 한 명은 뒤 4.5칸 (문턱 위) 으로 옮겨 둔다.
        var front = men.OrderByDescending(u => Vector2.Dot(u.Pos, c.Facing)).ThenBy(u => MathF.Abs(u.Pos.X - 60)).Take(2).ToList();
        foreach (var u in front) u.Hp = 0;
        var rest = men.Except(front).ToList();
        var near = rest.OrderBy(u => Vector2.Dot(u.Pos, c.Facing)).ThenBy(u => u.Pos.X).First();       // 뒷줄 왼쪽 끝
        var far = rest.OrderBy(u => Vector2.Dot(u.Pos, c.Facing)).ThenByDescending(u => u.Pos.X).First(); // 뒷줄 오른쪽 끝
        near.Pos -= c.Facing * 1.8f;
        far.Pos -= c.Facing * 4.5f;
        w.Step();
        int holes = c.Holes;
        Vector2 nearAt = near.Pos, farAt = far.Pos;
        long refills0 = w.Stats.GapRefills;
        Steps(w, TwoChecks());
        float nearMoved = Vector2.Distance(near.Pos, nearAt), farMoved = Vector2.Distance(far.Pos, farAt);
        check(settled && holes == 2 && c.Holes == 0 && nearMoved < 0.3f && farMoved > 2f && w.Stats.GapRefills - refills0 >= 1,
            $"빈자리 점검: 슬롯에서 {defs.CombatRules.GapMoveMin}칸 안 (1.8칸) 병사는 그대로 (이동 {nearMoved:0.00}칸), 4.5칸 벗어난 병사만 복귀 ({farMoved:0.0}칸), 빈자리 {holes} → {c.Holes}");
    }

    static void TestNoRegroupWhileFiringOrInCover()
    {
        // ① 병사 판정 (HoldsGround): 방금 쏜 병사 · 표적을 잡은 병사 · 숲 안 병사 = 제자리, 평지에서 쏘지 않은 병사 = 복귀 대상.
        var w = Bare();
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(30.5f, 30.5f));
        var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(34.5f, 30.5f));
        var k = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(38.5f, 30.5f));
        w.Map.Set(34, 30, TerrainType.Forest);
        Steps(w, 400);
        a.LastShotTick = w.Tick - 60;
        bool fire = w.HoldsGround(a), cov = w.HoldsGround(b), open = w.HoldsGround(k);
        a.LastShotTick = w.Tick - (int)(defs.CombatRules.RegroupFireSec * 60) - 1;
        bool fireOld = w.HoldsGround(a);
        check(fire && cov && !open && !fireOld, $"대형 복귀 금지 판정: 1초 전 사격 {fire} · 숲 {cov} · 평지 대기 {open} (거짓이어야) · {defs.CombatRules.RegroupFireSec}초 넘은 사격 {fireOld} (거짓이어야)");
        // ② 빈자리 점검: 숲 안 병사는 슬롯에서 멀어도 돌아가지 않고, 평지 대조군은 돌아간다.
        var (w2, c2, us2) = Stationed();
        var men2 = us2.Where(u => !u.IsOfficer).OrderBy(u => u.Id).ToList();
        men2[0].Hp = 0;
        var cov2 = men2[2]; var ctl2 = men2[3];
        cov2.Pos -= c2.Facing * 5f;
        ctl2.Pos -= c2.Facing * 5f + new Vector2(6, 0);
        var (ux, uy) = GameMap.TileOf(cov2.Pos);
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) w2.Map.Set(ux + dx, uy + dy, TerrainType.Forest);
        w2.Step();
        Vector2 cv0 = cov2.Pos, ct0 = ctl2.Pos;
        Steps(w2, TwoChecks());
        float covMoved = Vector2.Distance(cov2.Pos, cv0), ctlMoved = Vector2.Distance(ctl2.Pos, ct0);
        check(covMoved < 0.3f && ctlMoved > 2f && c2.Holes == 0, $"엄폐 (숲) 중인 병사는 대형으로 복귀하지 않음 (이동 {covMoved:0.00}칸), 평지 대조군은 복귀 ({ctlMoved:0.0}칸)");
    }

    static void TestNoGapCheckInCombat()
    {
        // 구성원이 계속 쏘는 (교전 중) 중대는 빈자리를 메우지 않고, 조용해지면 다음 점검에서 메운다.
        var (w, c, us) = Stationed();
        var men = us.Where(u => !u.IsOfficer).OrderBy(u => u.Id).ToList();
        men[0].Hp = 0;
        var stray = men[3];
        stray.Pos -= c.Facing * 5f;
        w.Step();
        Vector2 s0 = stray.Pos;
        int ticks = TwoChecks();
        for (int t = 0; t < ticks; t++) { men[5].LastShotTick = w.Tick; w.Step(); }
        int holesInCombat = c.Holes;
        float movedInCombat = Vector2.Distance(stray.Pos, s0);
        Steps(w, ticks + (int)(defs.CombatRules.GapCombatSec * 60));
        float movedAfter = Vector2.Distance(stray.Pos, s0);
        check(holesInCombat == 1 && movedInCombat < 0.3f && c.Holes == 0 && movedAfter > 2f,
            $"사격 중 (구성원 사격 {defs.CombatRules.GapCombatSec}초 창) 대형 복귀 · 빈자리 점검 안 함 (빈자리 {holesInCombat}, 이동 {movedInCombat:0.00}칸) → 조용해진 뒤 메움 (빈자리 {c.Holes}, 이동 {movedAfter:0.0}칸)");
    }

    // ---------------------------------------------------------------- 장교 위치

    static void TestOfficerSlotBehind()
    {
        var w = Bare();
        var us = Grid(w, 40, new Vector2(60, 40), 10);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 47)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(62, 47)));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        var units = c.Members.ToList();
        float s = OrgLayout.SpacingFor(units);
        var lay = CompanyLayout.Build(c, units, FormType.Line, s, 0, defs.Formations, defs.Tactics, combat: defs.CombatRules);
        // 배정: 깃발이 지금 무게중심에서 북쪽 (y-) 으로 향할 때
        Vector2 dir = new(0, -1), right = new(-dir.Y, dir.X), cen = Vector2.Zero;
        foreach (var u in units) cen += u.Pos;
        cen /= units.Count;
        var world = new Vector2[units.Count];
        for (int i = 0; i < units.Count; i++) world[i] = cen + right * lay.Local[i].X + dir * lay.Local[i].Y;
        var assign = new int[units.Count];
        CompanyLayout.Match(lay, units, world, null, assign);
        float menY = 0; int nm = 0; float maxManY = float.MinValue;
        for (int i = 0; i < units.Count; i++) if (!units[i].IsOfficer) { menY += lay.Local[assign[i]].Y; nm++; }
        menY /= nm;
        var offY = Enumerable.Range(0, units.Count).Where(i => units[i].IsOfficer).Select(i => lay.Local[assign[i]].Y).ToList();
        for (int i = 0; i < units.Count; i++) if (!units[i].IsOfficer) maxManY = MathF.Max(maxManY, lay.Local[assign[i]].Y);
        float rear = Enumerable.Range(0, units.Count).Where(i => !units[i].IsOfficer).Min(i => lay.Local[assign[i]].Y);
        bool behind = offY.All(y => menY - y is >= 1f and <= 4.01f && y < rear);
        check(offY.Count == 2 && behind, $"장교 슬롯 = 대열 중앙 뒤 1~4칸 (장교 {offY.Count}, 중앙 대비 {string.Join(", ", offY.Select(y => (menY - y).ToString("0.0")))}칸, 맨 뒷줄보다 뒤)");
    }

    static void TestOfficerDoesNotPassFront()
    {
        // 장교를 대열 앞에 세워 둔 중대가 적 (권총 사거리 밖, 소총 사거리 안) 쪽으로 공격 이동: 장교는 중대 보병 앞선을 넘지 않고, 표적을 쫓아 혼자 나가지 않는다.
        var w = Bare(140);
        var us = Grid(w, 30, new Vector2(70, 100), 10);
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(70, 97.5f));   // 앞줄보다 2.5칸 앞 (북쪽이 적)
        us.Add(off);
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        var foes = Grid(w, 10, new Vector2(70, 76), 10, "infantry", 2);   // engage: 반응 거리 (16.8) 밖에서 시작 — 장교가 뒤 슬롯으로 돌아갈 시간
        w.Hold(2, foes);
        w.AttackMove(1, us, new Vector2(70, 70));
        Vector2 dir = new(0, -1);
        float maxLead = float.MinValue, sumLead = 0; int n = 0, chargingTicks = 0;
        bool chasedOutOfRange = false;
        for (int t = 0; t < 60 * 30; t++)
        {
            w.Step();
            if (!off.Alive) break;
            if (off.Charging) { chargingTicks++; continue; }
            float front = c.Members.Where(u => !u.IsOfficer && u.Alive).Select(u => Vector2.Dot(u.Pos, dir)).DefaultIfEmpty(float.MinValue).Max();
            if (front == float.MinValue) break;
            float lead = Vector2.Dot(off.Pos, dir) - front;
            if (t > 60 * 6) { maxLead = MathF.Max(maxLead, lead); sumLead += lead; n++; }   // 처음 6초 = 앞에 서 있던 장교가 뒤 슬롯으로 돌아가는 중
            if (off.Target is Unit tg && Vector2.Distance(off.Pos, tg.Pos) > off.Weapon!.Range + off.Radius + tg.Radius + 0.5f && off.Velocity != Vector2.Zero) chasedOutOfRange = true;
        }
        float tol = defs.CombatRules.OfficerLeadTol;
        check(n > 0 && maxLead <= tol + 0.3f && !chasedOutOfRange && sumLead / Math.Max(1, n) < -1f,
            $"장교는 중대 앞선을 넘지 않고 표적을 쫓지 않음 (앞선 대비 최대 {maxLead:+0.00;-0.00}칸 · 평균 {sumLead / Math.Max(1, n):+0.0;-0.0}칸, 사거리 밖 추격 {chasedOutOfRange}, 돌격 틱 {chargingTicks})");
    }

    // ---------------------------------------------------------------- 정찰병 묶음

    static void TestScoutGroupNoOfficer()
    {
        var w = Bare();
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(50, 50));
        var sc = Grid(w, 3, new Vector2(52, 50), 3, "infantry_scout");
        w.FormCompany(1, sc.Append(off).ToList());
        w.Step();
        var c = sc[0].Company ?? sc[0].ScoutCompany;
        bool noOfficer = off.Company == null && c != null && !c.Members.Exists(u => u.IsOfficer);
        // 대조: 소총병이 섞이면 장교가 지휘 (정찰병은 정찰조)
        var w2 = Bare();
        var off2 = w2.SpawnUnit(defs.Units["officer"], 1, new Vector2(50, 50));
        var inf2 = Grid(w2, 6, new Vector2(52, 50), 3);
        var sc2 = Grid(w2, 2, new Vector2(52, 54), 2, "infantry_scout");
        w2.FormCompany(1, inf2.Concat(sc2).Append(off2).ToList());
        w2.Step();
        bool mixedOk = off2.Company != null && off2.Company.Scouts.Count == 2;
        check(noOfficer && mixedOk, $"정찰병 묶음 편성에는 장교 없음 (장교 소속 {(off.Company == null ? "없음" : "중대")}, 정찰 중대 장교 {c?.Members.Count(u => u.IsOfficer) ?? -1}명), 소총병이 섞인 중대는 장교 지휘 + 정찰조 {off2.Company?.Scouts.Count ?? -1}");
    }

    static void TestMissionObserversNoOfficer()
    {
        // 미션 자료: 정찰병이 든 묶음은 장교 없이 (관측 장교는 포대 쪽). 소총병 등 다른 병과가 섞인 묶음 (중대 정찰조) 은 그대로.
        var dir = Path.Combine(AppContext.BaseDirectory, "assets", "missions");
        var bad = new List<string>();
        int observers = 0;
        foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            void Walk(JsonElement e)
            {
                if (e.ValueKind == JsonValueKind.Object)
                {
                    if (e.TryGetProperty("units", out var u) && u.ValueKind == JsonValueKind.Object)
                    {
                        var keys = u.EnumerateObject().Select(p => p.Name).ToList();
                        bool scouts = keys.Any(k => defs.Units.TryGetValue(k, out var d) && d.Scout);
                        bool officers = keys.Any(k => defs.Units.TryGetValue(k, out var d) && d.IsOfficer);
                        bool others = keys.Any(k => defs.Units.TryGetValue(k, out var d) && !d.Scout && !d.IsOfficer);
                        if (scouts && !others) { observers++; if (officers) bad.Add($"{Path.GetFileName(f)}:{(e.TryGetProperty("id", out var id) ? id.GetString() : "?")}"); }
                    }
                    foreach (var p in e.EnumerateObject()) Walk(p.Value);
                }
                else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) Walk(x);
            }
            Walk(doc.RootElement);
        }
        check(bad.Count == 0 && observers >= 1, $"미션 정찰 · 관측반 묶음 {observers}개에 장교 없음 (위반 {string.Join(", ", bad)})");
    }
}
