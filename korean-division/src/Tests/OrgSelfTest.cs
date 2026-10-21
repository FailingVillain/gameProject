using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 부대 편제 (중대 · 대대) + 깃발 대형 이동 검증 (ROADMAP §2-4b, docs/ORG_FORMATION_PLAN.md §6). 창 없이. SelfTest 가 호출한다.
/// 소속 규칙 · 번호 · 분할/해산 · 행군 중 대형 유지 · 우클릭 드래그 방향/폭 · 좁은 길 종대 · 대대 대형 · 스냅샷 왕복 · 리플레이 체크섬 · 경로 계산 수.
/// </summary>
public static class OrgSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("org", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[부대 편제 · 깃발 대형]");
        using var noDelay = TestDefs.NoCommandDelay();   // 행군 대형 · 시간 측정 — 지휘 계통 지연은 SquadSelfTest 에서
        TestMembership();
        TestBattalionRules();
        TestSplitDisband();
        TestDeathTransfer();
        TestMarchKeepsFormation();
        TestGaitSpeedAnchor();
        TestDragDirWidth();
        TestKeepFacing();
        TestNarrowColumn();
        TestBattalionLayout();
        TestSnapshotRoundTrip();
        TestReplayDeterminism();
        TestRouteCount();
    }

    // ---------------------------------------------------------------- 도우미

    static GameMap Open(int w, int h)
    {
        var m = new GameMap(w, h, 1);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) m.Set(x, y, TerrainType.Grass);
        return m;
    }

    static void Rock(GameMap m, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++) m.Set(x, y, TerrainType.Rock);
    }

    /// <summary>사령부 없는 2세력 월드 (승패 판정은 사령부가 없으면 세력 패배 — 테스트는 그 전에 끝나거나 영향 없음).</summary>
    static World Bare(GameMap map, bool record = false)
    {
        var w = new World(map, defs, 2) { RecordCommands = record };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, map.Width - 1 - hq.Size, map.Height - 1 - hq.Size);
        // 전멸 패배 방지 (방향 변경 §0-1: 유닛이 하나도 없으면 탈락) — 세력마다 비무장 1기를 대도시 옆에
        for (int f = 1; f <= 2; f++)
        {
            var b = w.Buildings.First(x => x.Owner == f);
            var k = w.SpawnUnit(defs.Units["supply_wagon"], f, b.Pos + new Vector2(f == 1 ? 3 : -3, 0));
            w.Hold(f, new[] { k });
        }
        return w;
    }

    /// <summary>n 기를 cols 열 격자로 (간격 1칸) — 이미 대형을 이룬 중대.</summary>
    static List<Unit> Grid(World w, int n, Vector2 c, int cols, int faction = 1, string def = "infantry")
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++)
            list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * 1f, (i / cols) * 1f)));
        return list;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    static bool RunUntilIdle(World w, List<Unit> us, float seconds)
    {
        int max = (int)(seconds * 60);
        for (int t = 0; t < max; t++)
        {
            w.Step();
            if (t > 30 && us.All(u => !u.Alive || u.Order == UnitOrder.Idle)) { Steps(w, 20); return true; }
        }
        return false;
    }

    static Company Form(World w, List<Unit> us, int reg = 0)
    {
        w.FormCompany(us[0].Owner, us, reg);
        w.Step();
        return us[0].Company!;
    }

    static (float X0, float X1, float Y0, float Y1) Bounds(IEnumerable<Unit> us)
    {
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        foreach (var u in us) { x0 = MathF.Min(x0, u.Pos.X); x1 = MathF.Max(x1, u.Pos.X); y0 = MathF.Min(y0, u.Pos.Y); y1 = MathF.Max(y1, u.Pos.Y); }
        return (x0, x1, y0, y1);
    }

    static Vector2 Centroid(IEnumerable<Unit> us)
    {
        Vector2 s = Vector2.Zero; int n = 0;
        foreach (var u in us) { s += u.Pos; n++; }
        return s / Math.Max(1, n);
    }

    // ---------------------------------------------------------------- 소속 규칙

    static void TestMembership()
    {
        var w = Bare(Open(80, 80));
        var a = Grid(w, 120, new Vector2(40, 20), 20);
        var c1 = Form(w, a);
        check(c1.Members.Count == Company.MaxSize && a.Take(100).All(u => u.Company == c1) && a.Skip(100).All(u => u.Company == null) && c1.Number == 1 && c1.InitialStrength == 100,
            $"중대 편성: 120기 중 Id 순 100기만 (구성원 {c1.Members.Count}, 번호 {c1.Number}, 초기 병력 {c1.InitialStrength})");
        check(c1.Members.Zip(c1.Members.Skip(1)).All(p => p.First.Id < p.Second.Id), "중대 구성원은 Id 오름차순");
        var c2 = Form(w, a.Skip(100).ToList());
        check(c2.Number == 2 && w.CompaniesOf(1).Count() == 2, $"두 번째 중대 = 제2중대 (번호 {c2.Number})");
        // 이미 중대에 있는 유닛으로 새 중대: 옛 중대에서 빠진다 (소속은 하나만), 옛 중대가 비면 사라진다
        var c3 = Form(w, a.Skip(100).Take(20).ToList());
        check(!w.Companies.Contains(c2) && c3.Members.Count == 20 && c3.Number == 2, $"소속은 하나만: 제2중대 전원을 새 중대로 옮기면 옛 중대 소멸 · 새 중대가 빈 번호 2 사용 (번호 {c3.Number})");
        var c4 = Form(w, a.Take(10).ToList());
        check(c1.Members.Count == 90 && c4.Number == 3 && a.Take(10).All(u => u.Company == c4), $"일부만 새 중대로: 제1중대 90기 남음 · 새 중대 제3중대 ({c1.Members.Count}, 번호 {c4.Number})");
        // 남의 유닛은 편성 불가
        var enemy = Grid(w, 5, new Vector2(40, 60), 5, faction: 2);
        w.Issue(new FormCompanyCommand(1, enemy.Select(u => u.Id).ToArray()));
        w.Step();
        check(enemy.All(u => u.Company == null), "다른 세력 유닛은 편성할 수 없음");
    }

    static void TestBattalionRules()
    {
        var w = Bare(Open(120, 120));
        var cos = new List<Company>();
        for (int i = 0; i < 12; i++) cos.Add(Form(w, Grid(w, 5, new Vector2(10 + i * 8, 20), 5)));
        w.FormBattalion(1, cos);
        w.Step();
        var r1 = cos[0].Battalion;
        check(r1 != null && r1.Companies.Count == Battalion.MaxCompanies && r1.Number == 1 && cos[10].Battalion == null && cos[11].Battalion == null,
            $"대대 병합: 12개 중 Id 순 10개만 (중대 {r1?.Companies.Count}, 번호 {r1?.Number})");
        w.FormBattalion(1, new[] { cos[10], cos[11] });
        w.Step();
        var r2 = cos[10].Battalion!;
        check(r2.Number == 2 && r2.Companies.Count == 2, $"두 번째 대대 = 자동 번호 2 ({r2.Number})");
        // 가득 찬 대대로 옮기기는 거부
        w.MoveCompany(1, cos[10], r1);
        w.Step();
        check(cos[10].Battalion == r2 && r1.Companies.Count == 10, "중대 10개인 대대로 옮기기 거부");
        // 번호 교환
        w.SetBattalionNumber(1, r2, 1);
        w.Step();
        check(r2.Number == 1 && r1.Number == 2 && w.BattalionByNumber(1, 1) == r2, $"대대 번호 변경: 이미 쓰는 번호면 교환 (r1 {r1.Number}, r2 {r2.Number})");
        // 대대에서 빼기 · 빈 대대 소멸
        w.MoveCompany(1, cos[10], null);
        w.MoveCompany(1, cos[11], null);
        w.Step();
        check(!w.Battalions.Contains(r2) && cos[10].Battalion == null, "마지막 중대를 빼면 빈 대대 즉시 제거");
        // 옮기기: 다른 대대로
        w.FormBattalion(1, new[] { cos[10] });
        w.Step();
        var r3 = cos[10].Battalion!;
        w.MoveCompany(1, cos[0], r3);
        w.Step();
        check(cos[0].Battalion == r3 && r1.Companies.Count == 9 && r3.Companies.Count == 2, "중대를 다른 대대로 옮기기 (카드 끌어 놓기)");
        // Alt+숫자: 그 번호 대대에 넣기 (없으면 그 번호로 생성)
        var c = Form(w, Grid(w, 6, new Vector2(60, 80), 6), reg: 7);
        check(c.Battalion is { Number: 7 } && w.BattalionByNumber(1, 7)?.Companies.Count == 1, "Alt+7 편성: 제7대대가 없으면 그 번호로 만들어 편입");
        // 번호가 1~9 모두 차면 새 대대는 번호 없음 (0)
        for (int n = 1; n <= 9; n++)
            if (w.BattalionByNumber(1, n) == null) { Form(w, Grid(w, 2, new Vector2(10 + n * 9, 100), 2), reg: n); }
        var extra = Form(w, Grid(w, 2, new Vector2(100, 110), 2));
        w.FormBattalion(1, new[] { extra });
        w.Step();
        check(extra.Battalion is { Number: 0 }, $"1~9 가 모두 쓰이면 새 대대는 번호 없음 (0 목록) — 번호 {extra.Battalion?.Number}");
    }

    static void TestSplitDisband()
    {
        var w = Bare(Open(80, 80));
        var us = Grid(w, 31, new Vector2(40, 30), 10);
        var c = Form(w, us);
        w.FormBattalion(1, new[] { c });
        w.Step();
        check(c.Squads.Count == 3 && c.Squads.Sum(q => q.Members.Count) == 31 && c.Squads.All(q => q.Members.Count is 10 or 11), $"31명 중대 = 분대 3개 (11 · 10 · 10)");
        var lastSquad = c.Squads[^1];
        var lastIds = lastSquad.Members.Select(u => u.Id).ToList();
        w.SplitCompany(1, c);
        w.Step();
        var nc = lastSquad.Company;
        check(c.Members.Count == 21 && nc != c && nc.Members.Count == 10 && nc.Battalion == c.Battalion && c.Squads.Count == 2 && nc.Squads.Count == 1
              && nc.Squads[0] == lastSquad && lastSquad.Members.Select(u => u.Id).SequenceEqual(lastIds) && c.InitialStrength == 21 && nc.InitialStrength == 10,
            $"분할: 분대째로 앞 2개 유지 · 뒤 1개 새 중대 (같은 대대 편입, 분대 구성 그대로), 초기 병력 {c.InitialStrength}/{nc.InitialStrength}");
        var ncMembers = nc.Members.ToList();
        w.DisbandCompany(1, nc);
        w.Step();
        check(!w.Companies.Contains(nc) && ncMembers.All(u => u.Company == null && u.Squad == null) && c.Battalion!.Companies.Count == 1, "해산: 중대 소멸 · 병사는 무소속 (분대도 없음)");
    }

    static void TestDeathTransfer()
    {
        var w = Bare(Open(80, 80));
        var us = Grid(w, 4, new Vector2(40, 30), 4);
        var c = Form(w, us);
        w.FormBattalion(1, new[] { c });
        w.Step();
        var r = c.Battalion!;
        us[0].Hp = 0;
        w.Step();
        check(c.Members.Count == 3 && us[0].Company == null, "사망한 병사는 중대에서 빠짐 (빈자리 자동 보충 없음)");
        us[1].Hp = 0; us[2].Hp = 0; us[3].Hp = 0;
        w.Step();
        check(!w.Companies.Contains(c) && !w.Battalions.Contains(r), "마지막 병사가 죽으면 빈 중대 · 빈 대대 즉시 제거");
    }

    // ---------------------------------------------------------------- 깃발 이동

    /// <summary>직선 행군 중 슬롯 편차 (구성원 위치 ↔ 깃발 기준 슬롯) — 출발 정렬 2초 뒤부터 도착 전까지.</summary>
    /// <summary>깃발 속도 = 가장 느린 구성원의 실제 속도: 스태미나 바닥 (×0.85) 은 느리게, 속보 (×1.15) 는 빠르게 간다.</summary>
    static void TestGaitSpeedAnchor()
    {
        var w = Bare(Open(220, 100));
        var normal = Grid(w, 30, new Vector2(20, 20), 15);
        var tired = Grid(w, 30, new Vector2(20, 50), 15);
        var quick = Grid(w, 30, new Vector2(20, 80), 15);
        foreach (var u in tired) u.Fatigue = 99;   // 스태미나 1 → 25 미만 (이동 ×0.85)
        var cn = Form(w, normal); var ct = Form(w, tired); var cq = Form(w, quick);
        w.Move(1, normal, new Vector2(200, 20));
        w.Move(1, tired, new Vector2(200, 50));
        w.QuickStep(1, quick, new Vector2(200, 80));
        Steps(w, 60 * 7);   // 출발 정렬(선회 · 슬롯 수렴) 흡수 — 중대마다 길이가 다르다 (속보 중대는 약 0.2초 더 늦게 출발) → 정속 구간만 잰다
        Vector2 n0 = cn.Anchor?.Pos ?? Vector2.Zero, t0 = ct.Anchor?.Pos ?? Vector2.Zero, q0 = cq.Anchor?.Pos ?? Vector2.Zero;
        Steps(w, 60 * 6);
        if (cn.Anchor == null || ct.Anchor == null || cq.Anchor == null) { check(false, "깃발 속도: 세 중대 모두 행군 중이어야 함"); return; }
        float dn = Vector2.Distance(n0, cn.Anchor.Pos), dt = Vector2.Distance(t0, ct.Anchor.Pos), dq = Vector2.Distance(q0, cq.Anchor.Pos);
        float rt = dt / MathF.Max(0.01f, dn), rq = dq / MathF.Max(0.01f, dn);
        check(rt > 0.78f && rt < 0.92f && rq > 1.08f && rq < 1.22f, $"깃발 속도: 지친 중대 ×{rt:0.00} (기대 ≈0.85) · 속보 중대 ×{rq:0.00} (기대 ≈1.15)");
    }

    static void TestMarchKeepsFormation()
    {
        var w = Bare(Open(160, 60));
        var us = Grid(w, 60, new Vector2(20, 30), 30);   // 2열 횡대 (북쪽을 봄)
        var c = Form(w, us);
        w.Move(1, us, new Vector2(130, 30));
        w.Step();
        var a = c.Anchor;
        float maxDev = 0, sumDev = 0; int samples = 0, marchTicks = 0, formTick = -1;
        var devs = new List<float>();
        for (int t = 0; t < 60 * 90 && c.Anchor != null; t++)
        {
            w.Step();
            if (c.Anchor is { Arrived: false } an)
            {
                float m = 0, sum = 0;
                foreach (var u in c.Members)
                {
                    var l = an.Slots[u.Id];
                    Vector2 slot = an.Pos + new Vector2(-an.Heading.Y, an.Heading.X) * l.X + an.Heading * l.Y;
                    float d = Vector2.Distance(u.Pos, slot);
                    m = MathF.Max(m, d); sum += d;
                }
                if (formTick < 0 && m < 2.5f) formTick = t;   // 출발 정렬(90° 선회)이 끝난 순간부터 잰다
                if (formTick < 0) continue;
                marchTicks++;
                sumDev += sum; samples += c.Members.Count;
                devs.Add(m);
                maxDev = MathF.Max(maxDev, m);
            }
        }
        Steps(w, 120);
        devs.Sort();
        float p95 = devs.Count > 0 ? devs[(int)(devs.Count * 0.95f)] : 99;
        float avg = samples > 0 ? sumDev / samples : 99;
        check(a != null && marchTicks > 600 && p95 < 3.0f && avg < 0.3f,
            $"직선 행군 중 대형 유지: 정렬 {formTick / 60f:0.0}초 뒤 평균 슬롯 편차 {avg:0.00}칸, 틱별 최대 편차 p95 {p95:0.00} · 최대 {maxDev:0.00}칸 ({marchTicks}틱)");
        var (x0, x1, y0, y1) = Bounds(us);
        bool idle = us.All(u => u.Order == UnitOrder.Idle);
        // 동쪽 행군 · 같은 방향 유지 아님(처음 이동) → 8방향 스냅 = 동쪽. 2열 횡대 → 남북으로 넓고 동서로 얕다
        check(idle && x0 > 125 && x1 - x0 < 2.5f && y1 - y0 > 25, $"도착: 동쪽을 향한 2열 횡대 (깊이 {x1 - x0:0.0}, 폭 {y1 - y0:0.0}, 대기 {idle})");
        check(MathF.Abs(c.Facing.X - 1) < 1e-4f && c.Anchor == null, $"도착 후 중대 방향 = 동쪽, 깃발 제거 ({c.Facing})");
    }

    static void TestDragDirWidth()
    {
        var w = Bare(Open(120, 120));
        var us = Grid(w, 44, new Vector2(60, 90), 11);
        var c = Form(w, us);
        // 우클릭 드래그: 북쪽을 향해 폭 20칸
        w.Move(1, us, new Vector2(60, 40), new FormSpec((int)FormType.Free, 0, new Vector2(0, -1), 20f));
        bool idle = RunUntilIdle(w, us, 60);
        var (x0, x1, y0, y1) = Bounds(us);
        float s = 2 * MathF.Max(0.3f, us[0].Radius) + 0.35f;
        int cols = (int)MathF.Floor(20f / s + 1e-3f) + 1, rows = (44 + cols - 1) / cols;
        check(idle && MathF.Abs((x1 - x0) - (cols - 1) * s) < 1.5f && (y1 - y0) < rows * s + 0.6f,
            $"우클릭 드래그 폭 20칸: 열 {cols} × 줄 {rows} (폭 {x1 - x0:0.0}, 깊이 {y1 - y0:0.0})");
        check(MathF.Abs(c.Facing.Y + 1) < 1e-4f && us.All(u => MathF.Abs(DetMath.WrapPi(u.Facing + MathF.PI / 2)) < 0.3f), "우클릭 드래그 방향 = 북쪽, 병사도 북쪽을 봄");
        // 사선 대형 방지: 방향 지정 없는 대각 이동 = 정확히 45°
        w.Move(1, us, new Vector2(85, 20));
        RunUntilIdle(w, us, 60);
        var f = c.Facing;
        bool ok45 = MathF.Abs(MathF.Abs(f.X) - 0.70710677f) < 1e-4f || MathF.Abs(f.X) < 1e-4f || MathF.Abs(MathF.Abs(f.X) - 1) < 1e-4f;
        check(ok45, $"방향 지정 없는 이동: 8방향 스냅 ({f.X:0.000}, {f.Y:0.000})");
    }

    static void TestKeepFacing()
    {
        var w = Bare(Open(120, 80));
        var us = Grid(w, 20, new Vector2(40, 40), 10);
        var c = Form(w, us);
        w.Move(1, us, new Vector2(40, 30), new FormSpec(0, 0, new Vector2(0, -1)));
        RunUntilIdle(w, us, 30);
        // 옆으로 12칸 (KeepFacingRange 안) → 북쪽 유지
        w.Move(1, us, new Vector2(52, 30));
        RunUntilIdle(w, us, 30);
        bool kept = MathF.Abs(c.Facing.Y + 1) < 1e-4f;
        // 멀리 (40칸 동쪽) → 8방향 스냅 = 동쪽
        w.Move(1, us, new Vector2(95, 30));
        RunUntilIdle(w, us, 60);
        check(kept && MathF.Abs(c.Facing.X - 1) < 1e-4f, $"현재 방향 유지: 가까운 옆걸음은 북쪽 유지 ({kept}), 먼 이동은 진행 방향 ({c.Facing})");
    }

    static void TestNarrowColumn()
    {
        var m = Open(140, 60);
        Rock(m, 60, 0, 60, 59);
        for (int y = 28; y <= 31; y++) m.Set(60, y, TerrainType.Grass);   // 4칸 틈
        var w = Bare(m);
        var us = Grid(w, 40, new Vector2(30, 30), 20);
        var c = Form(w, us);
        w.Move(1, us, new Vector2(110, 30));
        bool sawColumn = false;
        int t;
        for (t = 0; t < 60 * 120; t++)
        {
            w.Step();
            if (c.Anchor is { Column: true }) sawColumn = true;
            if (t > 60 && us.All(u => u.Order == UnitOrder.Idle)) break;
        }
        Steps(w, 30);
        var (x0, x1, y0, y1) = Bounds(us);
        check(sawColumn && x0 > 61, $"좁은 길: 4칸 틈에서 2열 종대로 바꿔 전원 통과 (종대 {sawColumn}, 최소 x {x0:0.0}, {t / 60f:0.0}초)");
        check(y1 - y0 > 12, $"틈을 지난 뒤 원래 횡대로 다시 펼쳐짐 (폭 {y1 - y0:0.0})");
    }

    static void TestBattalionLayout()
    {
        var w = Bare(Open(200, 120));
        var cos = new List<Company>();
        for (int i = 0; i < 3; i++) cos.Add(Form(w, Grid(w, 20, new Vector2(60 + i * 14, 90), 10)));
        w.FormBattalion(1, cos);
        w.Step();
        var r = cos[0].Battalion!;
        var all = cos.SelectMany(c => c.Members).ToList();
        w.Move(1, all, new Vector2(80, 40), new FormSpec(0, 0, new Vector2(0, -1)));
        RunUntilIdle(w, all, 60);
        var bs = cos.Select(c => Bounds(c.Members)).ToList();
        bool sideBySide = bs[0].X1 < bs[1].X0 && bs[1].X1 < bs[2].X0 && bs.All(b => b.Y1 - b.Y0 < 2.5f);
        check(sideBySide, $"대대 횡대: 중대 블록이 왼쪽→오른쪽 순서로 겹치지 않게 나란히 (x {bs[0].X0:0}-{bs[0].X1:0} | {bs[1].X0:0}-{bs[1].X1:0} | {bs[2].X0:0}-{bs[2].X1:0})");
        w.SetBattalionForm(1, r, BatForm.Column);
        w.Move(1, all, new Vector2(80, 70), new FormSpec(0, 0, new Vector2(0, -1)));
        RunUntilIdle(w, all, 60);
        bs = cos.Select(c => Bounds(c.Members)).ToList();
        var ord = bs.OrderBy(b => b.Y0).ToList();
        float slackY = 2 * defs.CombatRules.FormArriveSlack;   // N12: 도착 여유 반경 (슬롯 앞뒤 ±0.6칸에서 멈춤) 만큼은 블록 가장자리가 닿아도 됨
        bool stacked = ord[0].Y1 < ord[1].Y0 + slackY && ord[1].Y1 < ord[2].Y0 + slackY && ord[0].Y0 < ord[1].Y0 && ord[1].Y0 < ord[2].Y0;
        check(stacked, $"대대 종대: 중대 블록이 앞뒤로 겹치지 않게 (y {ord[0].Y0:0}-{ord[0].Y1:0} | {ord[1].Y0:0}-{ord[1].Y1:0} | {ord[2].Y0:0}-{ord[2].Y1:0})");
        // 중대 대형 (병사 배치) 은 대대 대형과 따로: 방진
        w.SetCompanyForm(1, cos, FormType.Box);
        w.SetBattalionForm(1, r, BatForm.Line);
        w.Move(1, all, new Vector2(80, 40), new FormSpec(0, 0, new Vector2(0, -1)));
        RunUntilIdle(w, all, 60);
        var b0 = Bounds(cos[0].Members);
        check(MathF.Abs((b0.X1 - b0.X0) - (b0.Y1 - b0.Y0)) < 1.6f && b0.Y1 - b0.Y0 > 2.5f, $"중대 대형 = 방진 (블록 {b0.X1 - b0.X0:0.0} × {b0.Y1 - b0.Y0:0.0})");
    }

    // ---------------------------------------------------------------- 저장 · 리플레이

    static World MarchWorld(bool record)
    {
        var m = Open(140, 80);
        Rock(m, 70, 0, 70, 79);
        for (int y = 38; y <= 41; y++) m.Set(70, y, TerrainType.Grass);
        var w0 = Bare(m, record);
        Grid(w0, 30, new Vector2(30, 30), 15);
        Grid(w0, 30, new Vector2(30, 50), 15);
        // 스냅샷에서 시작 (리플레이가 시작 지점으로 첨부 → 테스트용 월드도 리플레이 검증 가능)
        var w = Snapshot.Load(Snapshot.Save(w0, Meta), defs).World;
        w.RecordCommands = record;
        var a = w.Units.Where(u => u.Owner == 1 && u.Pos.Y < 40).OrderBy(u => u.Id).ToList();
        var b = w.Units.Where(u => u.Owner == 1 && u.Pos.Y >= 40).OrderBy(u => u.Id).ToList();
        w.FormCompany(1, a, 1);
        w.FormCompany(1, b, 1);
        w.Step();
        w.Move(1, a.Concat(b), new Vector2(110, 40));
        return w;
    }

    static void TestSnapshotRoundTrip()
    {
        var x = MarchWorld(false);
        Steps(x, 400);   // 행군 중 (깃발 · 종대 상태 포함)
        bool marching = x.Companies.Any(c => c.Anchor != null);
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        var again = Snapshot.RawBody(Snapshot.Save(y, Meta));
        bool same = again.AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        check(marching && same && x.ComputeChecksum() == y.ComputeChecksum(), $"편제 + 깃발 스냅샷: 저장 → 불러오기 → 저장 바이트 동일, 체크섬 동일 (행군 중 {marching})");
        Steps(x, 900); Steps(y, 900);
        check(x.ComputeChecksum() == y.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(x, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(Snapshot.Save(y, Meta))),
            "불러온 월드를 900틱 이어 돌려도 원본과 같은 체크섬 · 같은 바이트");
    }

    static void TestReplayDeterminism()
    {
        var w1 = MarchWorld(true);
        w1.SplitCompany(1, w1.Companies[0]);
        Steps(w1, 1500);
        var w2 = MarchWorld(true);
        w2.SplitCompany(1, w2.Companies[0]);
        Steps(w2, 1500);
        check(w1.ComputeChecksum() == w2.ComputeChecksum(), $"같은 입력 → 같은 체크섬 ({w1.ComputeChecksum():x})");
        // 편제 명령 JSON 왕복
        var cmds = new GameCommand[]
        {
            new FormCompanyCommand(1, new[] { 3, 4 }, 2), new DisbandCompanyCommand(1, 5), new SplitCompanyCommand(1, 6), new FormBattalionCommand(1, new[] { 1, 2 }),
            new SetBattalionNumberCommand(1, 3, 4), new MoveCompanyCommand(1, 2, 0), new SetCompanyFormCommand(1, new[] { 1 }, 4), new SetBattalionFormCommand(1, 1, 3),
            new MoveCommand(1, new[] { 1 }, new Vector2(3, 4), new FormSpec(0, 0, new Vector2(0, -1), 12.5f)),
        };
        bool ok = true;
        foreach (var c in cmds)
        {
            string json = JsonSerializer.Serialize(c, CommandJson.Options);
            var back = JsonSerializer.Deserialize<GameCommand>(json, CommandJson.Options)!;
            ok &= JsonSerializer.Serialize(back, CommandJson.Options) == json && back.GetType() == c.GetType();
        }
        check(ok, "편제 명령 8종 + 폭 지정 이동 명령 JSON 왕복");
        // 리플레이 검증 (기록된 명령만으로 재생 → 체크섬 일치)
        var rep = ReplayFile.FromWorld(w1, 1);
        long bad = rep.Verify(defs, out string report);
        check(bad < 0, $"편제 · 깃발 이동 리플레이 검증 일치: {report}");
    }

    /// <summary>경로 계산 수: 중대 깃발 (중대당 1개) vs 옛 진형 이동 (유닛별 개인 경로 다수). 드문 바위 지형 + 참고로 지그재그 벽.</summary>
    static void TestRouteCount()
    {
        (int Routes, float Secs) Run(bool company, bool zigzag)
        {
            var m = Open(200, 100);
            if (zigzag) for (int i = 0; i < 6; i++) Rock(m, 30 + i * 25, 10 + (i % 2) * 30, 32 + i * 25, 60 + (i % 2) * 30);   // 지그재그 벽
            else { var rng = new Random(3); for (int i = 0; i < 40; i++) { int x = 25 + rng.Next(150), y = 5 + rng.Next(85); Rock(m, x, y, x + rng.Next(4), y + rng.Next(4)); } }
            var w = Bare(m);
            var us = Grid(w, 100, new Vector2(12, 50), 10);
            if (company) Form(w, us);
            int r0 = w.NavStats.RoutesComputed;
            w.Move(1, us, new Vector2(190, 50), new FormSpec((int)FormType.Line, 0, Vector2.Zero));
            int t = 0;
            for (; t < 60 * 240; t++) { w.Step(); if (t > 60 && us.All(u => u.Order == UnitOrder.Idle)) break; }
            return (w.NavStats.RoutesComputed - r0, t / 60f);
        }
        var oldWay = Run(false, false);
        var flag = Run(true, false);
        // 큰 그룹(옛 방식)도 리더 경로를 공유하므로 이 장면의 경로 수는 비슷하다 — 차이는 --stress J (유닛별 슬롯 경로) 에서 크다. 여기서는 도착 · 소요 시간만 판정.
        check(flag.Secs < 240 && flag.Routes <= oldWay.Routes * 2 + 10, $"드문 바위 100기 행군: 중대 깃발 경로 {flag.Routes}회 {flag.Secs:0}초 · 옛 진형 이동 {oldWay.Routes}회 {oldWay.Secs:0}초 (깃발은 막히면 종대 → 다시 횡대 정렬)");
        var oldZ = Run(false, true);
        var flagZ = Run(true, true);
        check(flagZ.Secs < 240, $"지그재그 벽 (참고): 중대 깃발 경로 {flagZ.Routes}회 {flagZ.Secs:0}초 · 옛 진형 이동 {oldZ.Routes}회 {oldZ.Secs:0}초 (깃발은 2열 종대로 통과)");
    }
}

