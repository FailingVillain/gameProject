using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 대규모 전투 충돌 · 대형 재설계 검증 (docs/COLLISION_ANALYSIS.md). KD_SELFTEST_ONLY=collision.
/// 겹침 해소 대칭 · 결정성, 행군 방향 안정 · 깃발 직선, 공격 이동 펼침 (중대 · 옛 큰 그룹), 공격 이동 대형 유지 (추격 안 함), 이어 걷기 (최소 재배정).
/// </summary>
public static class CollisionSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[충돌 · 대형 재설계]");
        using var noDelay = TestDefs.NoCommandDelay();
        TestSeparationSymmetry();
        TestSeparationDeterminism();
        TestMarchSteady();
        TestAttackMoveSpread();
        TestLooseSpread();
        TestAttackMoveHoldsFormation();
        TestContinueOrder();
        CollisionSelfTest2.Run(check);
    }

    // ---------------------------------------------------------------- 도우미

    internal static GameMap Open(int w, int h)
    {
        var m = new GameMap(w, h, 1);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) m.Set(x, y, TerrainType.Grass);
        return m;
    }

    internal static World Bare(GameMap map)
    {
        var w = new World(map, defs, 2);
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, map.Width - 1 - hq.Size, map.Height - 1 - hq.Size);
        for (int f = 1; f <= 2; f++)   // 전멸 패배 방지: 세력마다 비무장 1기 (대도시 옆, 사수)
        {
            var b = w.Buildings.First(x => x.Owner == f);
            var k = w.SpawnUnit(defs.Units["supply_wagon"], f, b.Pos + new Vector2(f == 1 ? 3 : -3, 0));
            w.Hold(f, new[] { k });
        }
        return w;
    }

    internal static List<Unit> Grid(World w, int n, Vector2 c, int cols, int faction = 1, string def = "infantry", float s = 1f)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++)
            list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * s, (i / cols) * s)));
        return list;
    }

    internal static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    internal static int MaxPerCell(IEnumerable<Unit> us)
    {
        var d = new Dictionary<(int, int), int>();
        foreach (var u in us) { var k = ((int)MathF.Floor(u.Pos.X), (int)MathF.Floor(u.Pos.Y)); d[k] = d.GetValueOrDefault(k) + 1; }
        return d.Count == 0 ? 0 : d.Values.Max();
    }

    internal static int Overlaps(List<Unit> us, float frac)
    {
        int c = 0;
        for (int i = 0; i < us.Count; i++)
        for (int j = i + 1; j < us.Count; j++)
            if (Vector2.Distance(us[i].Pos, us[j].Pos) < (us[i].Radius + us[j].Radius) * frac) c++;
        return c;
    }

    // ---------------------------------------------------------------- 겹침 해소

    static void TestSeparationSymmetry()
    {
        // 셋이 한 줄로 겹쳐 서 있음 (가운데가 양쪽과 0.3 겹침): 처리 순서와 무관하게 가운데는 제자리, 양쪽은 같은 거리만큼 바깥으로
        var w = Bare(Open(40, 40));
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.0f, 20.5f));
        var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.4f, 20.5f));
        var c = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.8f, 20.5f));
        w.Step();
        float da = 20.0f - a.Pos.X, dc = c.Pos.X - 20.8f, db = MathF.Abs(b.Pos.X - 20.4f);
        check(da > 0.01f && MathF.Abs(da - dc) < 1e-4f && db < 1e-4f, $"겹침 해소 대칭: 양쪽 바깥으로 같은 거리 ({da:0.0000} / {dc:0.0000}), 가운데 제자리 ({db:0.00000})");
        // 같은 배치를 거꾸로 만들어도 (유닛 Id · 목록 순서 반대) 같은 결과
        var w2 = Bare(Open(40, 40));
        var c2 = w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.8f, 20.5f));
        var b2 = w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.4f, 20.5f));
        var a2 = w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.0f, 20.5f));
        w2.Step();
        check(MathF.Abs(a2.Pos.X - a.Pos.X) < 1e-5f && MathF.Abs(b2.Pos.X - b.Pos.X) < 1e-5f && MathF.Abs(c2.Pos.X - c.Pos.X) < 1e-5f,
            $"겹침 해소는 처리 순서와 무관 (생성 순서 반대: {a2.Pos.X:0.0000}/{b2.Pos.X:0.0000}/{c2.Pos.X:0.0000})");
        // 참호 안 (사수) 병사는 아군에게 밀리지 않는다 — 고정 0 (참호 묶음 테스트는 TrenchSelfTest)
        var w3 = Bare(Open(40, 40));
        var h1 = w3.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.0f, 20.5f));
        var h2 = w3.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20.2f, 20.5f));
        w3.Hold(1, new[] { h1, h2 });
        Steps(w3, 120);
        float sep = Vector2.Distance(h1.Pos, h2.Pos);
        check(sep > 0.4f, $"둘 다 사수 중 겹쳐 있으면 조금씩 비켜 섬 (굳지 않음): 2초 뒤 거리 {sep:0.00}");
    }

    static void TestSeparationDeterminism()
    {
        ulong Run()
        {
            var w = Bare(Open(60, 60));
            var us = Grid(w, 200, new Vector2(30, 20), 20, s: 0.45f);   // 빽빽하게 겹쳐 생성
            Steps(w, 5);
            w.Move(1, us, new Vector2(30, 45));
            Steps(w, 240);
            return w.ComputeChecksum();
        }
        ulong x = Run(), y = Run();
        check(x == y, $"겹침 해소 결정성: 같은 입력 두 번 = 같은 체크섬 ({x:X16})");
    }

    // ---------------------------------------------------------------- 행군

    static void TestMarchSteady()
    {
        // 50명 중대가 평지 90칸을 동쪽으로: 깃발은 직선 (구역 경로 출입구로 꺾이지 않음), 걷는 동안 그림 방향 변화 · 걷기↔서기 깜빡임이 거의 없다
        var w = Bare(Open(160, 60));
        var us = Grid(w, 50, new Vector2(20, 26), 10);
        w.FormCompany(1, us);
        w.Step();
        var co = us[0].Company!;
        w.Move(1, us, new Vector2(110, 30), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        Steps(w, 240);   // 출발 · 정렬
        float yMin = float.MaxValue, yMax = float.MinValue;
        long walkTicks = 0, faceCh = 0, toggles = 0;
        var lastFace = us.ToDictionary(u => u.Id, u => u.Facing);
        var lastWalk = us.ToDictionary(u => u.Id, u => u.Walking);
        for (int t = 0; t < 900; t++)
        {
            w.Step();
            if (co.Anchor is { Arrived: false } a) { yMin = MathF.Min(yMin, a.Pos.Y); yMax = MathF.Max(yMax, a.Pos.Y); }
            foreach (var u in us)
            {
                if (u.Walking && lastWalk[u.Id])
                {
                    walkTicks++;
                    float d = u.Facing - lastFace[u.Id];
                    d -= MathF.Tau * MathF.Floor((d + MathF.PI) / MathF.Tau);
                    if (MathF.Abs(d) > 0.05f) faceCh++;
                }
                if (u.Order == UnitOrder.Move && u.Walking != lastWalk[u.Id]) toggles++;
                lastFace[u.Id] = u.Facing; lastWalk[u.Id] = u.Walking;
            }
        }
        float sec = walkTicks / 60f;
        check(yMax - yMin < 1.5f, $"깃발 직선 행군 (평지 90칸): 깃발 y 흔들림 {yMax - yMin:0.00} 칸");
        check(sec > 100 && faceCh / sec < 0.3f, $"행군 중 몸 방향 흔들림 (3° 넘는 변화) {faceCh / Math.Max(1f, sec):0.000} 회/유닛-초 (걷는 유닛-초 {sec:0})");
        check(toggles / (50 * 15f) < 0.05f, $"행군 중 걷기 ↔ 서기 깜빡임 {toggles / (50 * 15f):0.000} 회/유닛-초");
    }

    // ---------------------------------------------------------------- 공격 이동

    static void TestAttackMoveSpread()
    {
        // 중대 4개 (각 40) 가 적 없는 평지로 공격 이동: 일반 이동과 같은 대형으로 도착 — 칸당 2명 이하, 정면 폭 유지, 한 점에 몰리지 않음
        var w = Bare(Open(150, 120));
        var all = new List<Unit>();
        for (int k = 0; k < 4; k++)
        {
            var us = Grid(w, 40, new Vector2(30 + k * 12, 30), 8);
            w.FormCompany(1, us);
            all.AddRange(us);
        }
        w.Step();
        w.AttackMove(1, all, new Vector2(75, 85), new FormSpec((int)FormType.Line, 0, new Vector2(0, 1)));
        Steps(w, 60 * 40);
        int idle = all.Count(u => u.Order == UnitOrder.Idle);
        float x0 = all.Min(u => u.Pos.X), x1 = all.Max(u => u.Pos.X);
        int mpc = MaxPerCell(all), deep = Overlaps(all, 0.6f);
        check(idle == all.Count && mpc <= 2 && deep == 0 && x1 - x0 > 60f,
            $"공격 이동 = 대형 슬롯으로 펼쳐 도착: 도착 {idle}/{all.Count}, 칸 최대 {mpc}명, 깊은 겹침 {deep}, 정면 폭 {x1 - x0:0.0} 칸");
    }

    static void TestLooseSpread()
    {
        // 중대 없는 300기 · 진형 지정 없는 공격 이동 (옛 큰 그룹): 압축 오프셋이 몸 넓이보다 좁아지지 않는다
        var w = Bare(Open(120, 120));
        var us = Grid(w, 300, new Vector2(60, 15), 20, s: 0.95f);
        w.AttackMove(1, us, new Vector2(60, 90));
        Steps(w, 60 * 45);
        int mpc = MaxPerCell(us), deep = Overlaps(us, 0.6f);
        check(mpc <= 4 && deep <= 3, $"진형 없는 큰 그룹 공격 이동: 칸 최대 {mpc}명, 깊은 겹침 {deep}쌍 (한 점에 몰리지 않음)");
    }

    static void TestAttackMoveHoldsFormation()
    {
        // 50명 중대가 서 있는 적 30명 너머로 공격 이동: 대열은 사거리에서 멈춰 쏘고, 병사가 대형을 떠나 적에게 몰려가지 않는다
        var w = Bare(Open(120, 80));
        var us = Grid(w, 50, new Vector2(30, 40), 10);
        w.FormCompany(1, us);
        var foe = Grid(w, 30, new Vector2(80, 40), 10, faction: 2);
        w.Hold(2, foe);
        w.Step();
        w.AttackMove(1, us, new Vector2(100, 42), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        float worstSpread = 0; int maxChase = 0;
        for (int t = 0; t < 60 * 25; t++)
        {
            w.Step();
            var alive = us.Where(u => u.Alive).ToList();
            if (alive.Count < 10) break;
            if (t % 30 != 0) continue;
            float ex = foe.Where(f => f.Alive).Select(f => f.Pos.X).DefaultIfEmpty(200).Min();
            int chasers = alive.Count(u => u.Target != null && u.AimTarget == 0 && !u.Charging && u.Walking);   // 표적을 쫓아 걷는 병사
            maxChase = Math.Max(maxChase, chasers);
            float cx = alive.Average(u => u.Pos.X);
            worstSpread = MathF.Max(worstSpread, alive.Max(u => MathF.Abs(u.Pos.X - cx)));
        }
        check(maxChase == 0, $"공격 이동 대형 구성원은 표적을 쫓아 대형을 떠나지 않음 (쫓는 병사 최대 {maxChase})");
        check(worstSpread < 12f, $"교전 중 대열 깊이 유지: 무게중심에서 앞뒤 최대 {worstSpread:0.0} 칸");
    }

    static void TestContinueOrder()
    {
        // 행군 중 비슷한 방향으로 다시 명령 (전술 AI 가 몇 초마다 하듯): 슬롯 거의 그대로 · 걷던 병사는 멈추지 않는다
        var w = Bare(Open(160, 60));
        var us = Grid(w, 40, new Vector2(20, 26), 8);
        w.FormCompany(1, us);
        w.Step();
        var co = us[0].Company!;
        w.Move(1, us, new Vector2(100, 30), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        Steps(w, 420);
        var before = co.Anchor!.Slots.ToDictionary(kv => kv.Key, kv => kv.Value);
        int walking = us.Count(u => u.Walking);
        w.Move(1, us, new Vector2(110, 33), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
        w.Step();
        w.Step();
        var after = co.Anchor!.Slots;
        int same = before.Count(kv => after.TryGetValue(kv.Key, out var v) && Vector2.Distance(v, kv.Value) < 0.01f);
        int still = us.Count(u => u.Walking);
        check(same >= before.Count * 9 / 10, $"이어 걷기: 슬롯 유지 {same}/{before.Count}");
        check(still >= walking - 2, $"이어 걷기: 걷던 병사가 멈추지 않음 (명령 전 {walking} → 직후 {still})");
    }
}
