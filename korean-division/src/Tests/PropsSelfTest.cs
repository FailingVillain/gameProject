using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>F6 엄폐물 소품 층 (Props.cs · GameMapProps.cs): 배치 결정성 · 엄폐 / 시선 수식 · 포탄 파손 · 저장 왕복. 창 없이. KD_SELFTEST_ONLY=props</summary>
public static class PropsSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("props", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[F6 엄폐물 소품]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestData();
            TestPlacement();
            TestCover();
            TestLos();
            TestDestroy();
            TestSpotScore();
            TestMission();
            TestMissionsAll();
            TestSuppress();
            TestSnapshot();
            TestCoverClusterForest();
            TestPropsV2();
            TestVariants();
        }
        catch (Exception e) { check(false, $"props 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 100)
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

    /// <summary>소품 외형 변형 번호: 순수 함수 (같은 입력 = 같은 출력), 범위, 폴백(변형 1 = 항상 0), 골고루 분포.</summary>
    static void TestVariants()
    {
        bool pure = true, range = true, one = true;
        var hist = new int[6];
        for (int i = 0; i < 3000; i++)
        {
            float x = 3f + (i % 60) * 1.37f, y = 5f + (i / 60) * 1.91f;
            int a = Render.PropVariants.Pick(x, y, 6), b = Render.PropVariants.Pick(x, y, 6);
            pure &= a == b; range &= a >= 0 && a < 6; one &= Render.PropVariants.Pick(x, y, 1) == 0 && Render.PropVariants.Pick(x, y, 0) == 0;
            if (a >= 0 && a < 6) hist[a]++;
        }
        bool spread = true;
        foreach (int h in hist) spread &= h > 3000 / 6 / 2;
        check(pure && range, "props: 변형 번호 = 위치 해시 순수 함수, 범위 내");
        check(one, "props: 변형 1개(없음) -> 항상 0번 = 기존 외형 폴백");
        check(spread, "props: 변형 번호 분포가 고르다 (" + string.Join("/", hist) + ")");
    }

    static void TestData()
    {
        var pd = PropDefs.Current;
        bool names = new[] { "rock_s", "rock_m", "rock_l", "tree_birch", "tree_larch", "tree_pine", "tree_oak" }.All(k => pd.Types.ContainsKey(k));
        bool variants = pd.Types.Values.All(t => t.Destroyed.Length == 0 || pd.Types.ContainsKey(t.Destroyed));
        bool order = pd.Types["rock_l"].Cover < pd.Types["rock_m"].Cover && pd.Types["rock_m"].Cover < pd.Types["rock_s"].Cover
                     && pd.Types["tree_oak_fallen"].Cover > pd.Types["tree_oak"].Cover && pd.Types["rock_m_cracked"].Cover > pd.Types["rock_m"].Cover;
        check(names && variants && order, $"props.json: 일곱 종류 있음 {names} · 파괴 변종 모두 정의됨 {variants} · 큰 바위일수록 좋은 엄폐 / 부서지면 엄폐 나빠짐 {order}");
    }

    static void TestPlacement()
    {
        var a = GameMap.Generate(160, 160, 7);
        var b = GameMap.Generate(160, 160, 7);
        var c = GameMap.Generate(160, 160, 8);
        bool same = a.PropSeeds.SequenceEqual(b.PropSeeds);
        bool diff = !a.PropSeeds.SequenceEqual(c.PropSeeds);
        int blockers = 0; bool nbrOk = true;
        for (int y = 0; y < a.Height; y++)
        for (int x = 0; x < a.Width; x++)
            if (a.IsPropBlocked(x, y))
            {
                blockers++;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && !a.IsWalkable(x + dx, y + dy)) nbrOk = false;
            }
        bool inStart = a.PropSeeds.Any(p => a.StartTiles.Any(s => (p.X - s.X) * (p.X - s.X) + (p.Y - s.Y) * (p.Y - s.Y) < 100));
        var f = new FlowField(a, new Vector2(a.StartTiles[0].X + 0.5f, a.StartTiles[0].Y + 0.5f));
        bool reach = a.StartTiles.All(s => f.IsReachable(s.X, s.Y));
        int pairs = a.PropSeeds.Count(p => a.PropSeeds.Any(q => q.Type == p.Type && MathF.Abs(q.X - (a.Width - p.X)) < 0.01f && MathF.Abs(q.Y - (a.Height - p.Y)) < 0.01f));
        check(a.PropSeeds.Count >= 30 && same && diff && !inStart && reach && nbrOk && pairs * 10 >= a.PropSeeds.Count * 9,
            $"배치: {a.PropSeeds.Count}개 (막는 바위 {blockers}) 같은 시드 동일 {same} · 다른 시드 다름 {diff} · 시작점 12칸 비움 {!inStart} · 길 연결 {reach} · 막는 바위 이웃 모두 걸음 {nbrOk} · 회전 대칭 짝 {pairs}/{a.PropSeeds.Count}");
        var kinds = a.PropSeeds.Select(p => p.Type).Distinct().Count();
        check(kinds >= 5, $"배치: 종류 {kinds}가지 (나무 + 바위 섞임)");
    }

    static (World W, Unit Shooter, Unit Target) Duel(float sx, float tx, float y = 50.5f)
    {
        var w = Bare();
        var s = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(sx, y));
        var t = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(tx, y));
        return (w, s, t);
    }

    static void TestCover()
    {
        var (w, s, t) = Duel(62f, 50f);
        float none = w.HitChance(s, t);
        w.AddProp(50.8f, 50.5f, "rock_m");   // 표적 동쪽 0.8칸 = 사격자 쪽
        float behind = w.HitChance(s, t);
        float cm = PropDefs.Current.Types["rock_m"].Cover;
        bool ratio = MathF.Abs(behind / none - cm) < 0.02f;
        var (w2, s2, t2) = Duel(38f, 50f);   // 사격자 서쪽
        w2.AddProp(50.8f, 50.5f, "rock_m");
        bool otherSide = w2.PropCover(t2.Pos, s2.Pos) == 1f;
        var (w3, s3, t3) = Duel(62f, 50f);
        w3.AddProp(51.4f, 50.5f, "rock_m");   // 도달 거리 0.9 밖
        bool far = w3.PropCover(t3.Pos, s3.Pos) == 1f;
        var (w4, s4, t4) = Duel(62f, 50f);
        w4.AddProp(50.0f, 50.8f, "rock_m");   // 옆 0.8칸, 사격 방향과 직각 = 원뿔 밖
        bool side = w4.PropCover(t4.Pos, s4.Pos) == 1f;
        var (w5, s5, t5) = Duel(62f, 50f);
        w5.AddProp(50.5f, 50.5f, "rock_s"); w5.AddProp(50.7f, 50.4f, "rock_l");   // 겹치면 가장 좋은 하나 (곱하지 않음)
        bool best = MathF.Abs(w5.PropCover(t5.Pos, s5.Pos) - PropDefs.Current.Types["rock_l"].Cover) < 1e-6f;
        check(ratio && otherSide && far && side && best,
            $"엄폐 수식: 사격자 쪽 바위 뒤 명중 x{behind / none:F2} (기대 {cm}) {ratio} · 반대쪽 무효 {otherSide} · 도달 밖 무효 {far} · 옆 무효 {side} · 겹치면 최고 하나 {best}");
        // 지형 엄폐와는 겹치지 않고 좋은 쪽
        var (w6, s6, t6) = Duel(62f, 50f);
        w6.Map.Set(50, 50, TerrainType.Forest);
        float forest = w6.HitChance(s6, t6);
        w6.AddProp(50.8f, 50.5f, "tree_birch");   // 숲보다 약한 나무 -> 숲 값 그대로
        bool keep = MathF.Abs(w6.HitChance(s6, t6) - forest) < 1e-6f;
        w6.AddProp(50.8f, 50.2f, "rock_l");        // 큰 바위가 숲보다 좋음
        bool better = w6.HitChance(s6, t6) < forest * 0.9f;
        check(keep && better, $"엄폐 수식: 지형 엄폐와 소품은 좋은 쪽 하나 - 약한 나무는 숲 값 유지 {keep} · 큰 바위는 숲보다 좋아짐 {better}");
    }

    static void TestLos()
    {
        var (w, s, t) = Duel(62f, 50f);
        float none = w.HitChance(s, t);
        w.AddProp(56f, 50.6f, "rock_m");   // 사격선 위 가운데
        float one = w.HitChance(s, t);
        float lb = PropDefs.Current.Types["rock_m"].LosBlock;
        bool part = MathF.Abs(one / none - (1f - lb)) < 0.02f;
        w.AddProp(58f, 50.5f, "rock_l");
        float two = w.HitChance(s, t);
        float floor = 1f - PropDefs.Current.Rules.LosMaxBlock;
        bool stack = two < one && two / none >= floor - 0.02f;
        for (int i = 0; i < 6; i++) w.AddProp(53f + i * 1.5f, 50.5f, "rock_l");
        bool cap = MathF.Abs(w.HitChance(s, t) / none - floor) < 0.02f;
        var (w2, s2, t2) = Duel(62f, 50f);
        w2.AddProp(56f, 53.5f, "rock_l");  // 선에서 3칸 비켜남
        bool off = MathF.Abs(w2.HitChance(s2, t2) - none) < 1e-6f;
        var (w3, s3, t3) = Duel(62f, 50f);
        w3.AddProp(61.3f, 50.5f, "rock_l");   // 사격자 발치 (0.8 안) - 자기 앞 소품은 시선 차단으로 안 센다
        bool endClear = MathF.Abs(w3.HitChance(s3, t3) - none) < 1e-6f;
        check(part && stack && cap && off && endClear,
            $"시선 부분 차단: 바위 하나 x{one / none:F2} (기대 {1 - lb:F2}) {part} · 둘이면 더 감소(하한 {floor:F2}) {stack} · 많아도 하한 {cap} · 비켜난 바위 무효 {off} · 끝점 가까운 소품 제외 {endClear}");
    }

    static void TestDestroy()
    {
        var w = Bare();
        var how = defs.Weapons["howitzer_heavy"];
        w.AddProp(40f, 40f, "tree_oak");
        w.AddProp(45f, 40f, "rock_s");
        w.AddProp(70f, 70f, "rock_l");
        var hp0 = w.Props.Items.Select(p => p.Hp).ToArray();
        w.Explode(0, 1, null, new Vector2(30, 40), new Vector2(40.2f, 40f), 0, how);
        var oak = w.Props.Items[0];
        bool oakDamaged = oak.Hp < hp0[0] || oak.Destroyed;
        w.Explode(0, 1, null, new Vector2(30, 40), new Vector2(40f, 40f), 0, how);
        bool fell = oak.Type == "tree_oak_fallen" && oak.Destroyed && w.Props.Destroyed >= 1;
        bool rockFar = w.Props.Items[1].Type == "rock_s" && w.Props.Items[1].Hp == hp0[1];
        bool bigFar = w.Props.Items[2].Hp == hp0[2];
        // 쓰러진 나무는 엄폐가 약해진다
        var (w2, s2, t2) = Duel(62f, 50f);
        w2.AddProp(50.6f, 50.5f, "tree_oak");
        float standing = w2.PropCover(t2.Pos, s2.Pos);
        w2.Props.Items[0].Type = "tree_oak_fallen"; w2.Props.Items[0].Def = PropDefs.Current.Types["tree_oak_fallen"];
        bool weaker = w2.PropCover(t2.Pos, s2.Pos) > standing;
        // 바위: 맞으면 금감, 금간 바위는 길을 계속 막는다
        var w3 = Bare();
        w3.AddProp(30.5f, 30.5f, "rock_m");
        bool blocked = !w3.Map.IsWalkable(30, 30);
        for (int i = 0; i < 4; i++) w3.Explode(0, 1, null, new Vector2(20, 30), new Vector2(30.5f, 30.5f), 0, how);
        bool cracked = w3.Props.Items[0].Type == "rock_m_cracked" && w3.Props.Items[0].Hp == 0;
        for (int i = 0; i < 5; i++) w3.Explode(0, 1, null, new Vector2(20, 30), new Vector2(30.5f, 30.5f), 0, how);
        bool stays = w3.Props.Items[0].Type == "rock_m_cracked" && !w3.Map.IsWalkable(30, 30);
        // 약한 폭발 (damage < minShellDamage) 은 안 부순다
        var w4 = Bare();
        w4.AddProp(30.5f, 30.5f, "tree_birch");
        var weak = new WeaponDef { Key = "weak", Damage = 10, Splash = 3, SplashEdge = 1 };
        w4.Explode(0, 1, null, new Vector2(20, 30), new Vector2(30.5f, 30.5f), 0, weak);
        bool weakOk = w4.Props.Items[0].Type == "tree_birch";
        check(oakDamaged && fell && rockFar && bigFar && weaker && blocked && cracked && stays && weakOk,
            $"파손: 참나무 포탄 두 방에 쓰러짐 {fell} (첫 방 피해 {oakDamaged}) · 폭발 밖 소품 무사 {rockFar && bigFar} · 쓰러진 나무 엄폐 약해짐 {weaker} · 바위 금감 {cracked} + 금간 뒤에도 통행 불가 {blocked && stays} · 약한 폭발 무시 {weakOk}");
    }

    static void TestSpotScore()
    {
        var w = Bare();
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(50.5f, 50.5f));
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(70.5f, 50.5f));
        u.Target = foe;
        float bare = w.CoverHitAt(new Vector2(50.5f, 50.5f), u);
        w.AddProp(51.2f, 50.5f, "rock_m");
        float behind = w.CoverHitAt(new Vector2(50.5f, 50.5f), u);
        float away = w.CoverHitAt(new Vector2(45.5f, 50.5f), u);
        u.Target = null; u.Facing = 0f;
        float facing = w.CoverHitAt(new Vector2(50.5f, 50.5f), u);
        u.Facing = MathF.PI;
        float turned = w.CoverHitAt(new Vector2(50.5f, 50.5f), u);
        check(bare == 1f && behind < 0.6f && away == 1f && facing < 0.6f && turned == 1f,
            $"F1 사격 위치 점수: 맨땅 {bare:F2} · 적 쪽 바위 뒤 {behind:F2} · 바위 없는 자리 {away:F2} · 표적 없이 바라보는 쪽 {facing:F2} / 반대 {turned:F2}");
    }

    static void TestMission()
    {
        var def = MissionDef.Load("ai_bn_meeting");
        def.PropScatter = true;
        def.Props.Add(new MissionProp { Type = "rock_l", X = 20.5f, Y = 20.5f });
        var w1 = World.CreateMissionWorld(def, defs);
        var w2 = World.CreateMissionWorld(def, defs);
        bool det = w1.Props.Count > 5 && w1.Map.PropSeeds.SequenceEqual(w2.Map.PropSeeds) && w1.ComputeChecksum() == w2.ComputeChecksum();
        bool hand = w1.Props.Items[0].Type == "rock_l" && !w1.Map.IsWalkable(20, 20);
        bool clear = !w1.Units.Any(u => w1.Props.Items.Any(p => p.Id > 0 && (p.Pos - u.Pos).LengthSquared() < 6.25f));
        def.PropScatter = false; def.Props.Clear();
        var plain = World.CreateMissionWorld(def, defs);
        check(det && hand && clear && plain.Props.Count == 0, $"미션 소품: propScatter {w1.Props.Count}개 결정적 {det} · 손으로 놓은 바위 {hand} · 병력 2.5칸 안 비움 {clear} · 옵션 없는 미션은 소품 0 ({plain.Props.Count})");
    }

    static void TestMissionsAll()
    {
        var bad = new List<string>(); int n = 0;
        foreach (var id in MissionDef.Available())
        {
            if (id.StartsWith("test_")) continue;   // 시험 전용 지형 미션 (test_river_crossing 등): 소품 없이 지형만 시험 — 흩뿌리기 대상 아님
            var def = MissionDef.Load(id);
            if (!def.PropScatter) { bad.Add(id + "(옵션 없음)"); continue; }
            var w = World.CreateMissionWorld(def, defs);
            n++;
            if (w.Props.Count == 0) bad.Add(id + "(소품 0)");
        }
        check(bad.Count == 0 && n >= 13, $"미션 소품 배치: {n}개 미션 모두 propScatter 로 소품 생성 (문제 {string.Join(",", bad)})");
    }

    static void TestSuppress()
    {
        var (w, s, t) = Duel(62f, 50f);
        float bare = w.SuppressCoverMul(t, s.Pos);
        w.AddProp(50.8f, 50.5f, "rock_l");
        float behind = w.SuppressCoverMul(t, s.Pos);
        float noDir = w.SuppressCoverMul(t, null);
        float other = w.SuppressCoverMul(t, new Vector2(38f, 50.5f));
        float cl = PropDefs.Current.Types["rock_l"].Cover;
        // 실제 사격 경로: 같은 조건에서 목표 한 명이 6초간 받은 총 제압 (맞사격 끔 = 표적 사격 불가로 격리)
        int bareG = Gain(0), behindG = Gain(50.8f);
        check(bare == 1f && MathF.Abs(behind - cl) < 1e-6f && noDir == 1f && other == 1f && behindG < bareG,
            $"소품 제압 엄폐: 맨땅 x{bare:F2} · 사격자 쪽 큰 바위 뒤 x{behind:F2} (기대 {cl}) · 방향 모르면 무효 {noDir == 1f} · 반대쪽 무효 {other == 1f} · 실제 사격 6초 제압 {bareG} -> 바위 뒤 {behindG}");
    }

    static int Gain(float propX)
    {
        var w = Bare();
        var s = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(62f, 50.5f));
        var t = w.SpawnUnit(defs.Units["recruit"], 2, new Vector2(50f, 50.5f));
        if (propX > 0) w.AddProp(propX, 50.5f, "rock_l");
        w.Hold(1, new List<Unit> { s }); w.Hold(2, new List<Unit> { t });
        int sum = 0;
        for (int i = 0; i < 60 * 6; i++)
        {
            t.Hp = 100000; s.Hp = 100000;
            w.Step();
            sum = Math.Max(sum, t.Suppression);
        }
        return sum;
    }

    static void TestSnapshot()
    {
        var m = GameMap.Generate(120, 120, 3);
        var x = World.CreateStandard(m, defs);
        var how = defs.Weapons["howitzer_heavy"];
        foreach (var p in x.Props.Items.Take(40).ToList()) { x.Explode(0, 1, null, new Vector2(0, 0), p.Pos, 0, how); x.Explode(0, 1, null, new Vector2(0, 0), p.Pos, 0, how); }
        int broken = x.Props.Destroyed;
        x.Step();
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool state = y.Props.Count == x.Props.Count && y.Props.Destroyed == broken && y.Props.Items.Zip(x.Props.Items).All(z => z.First.Type == z.Second.Type && z.First.Hp == z.Second.Hp);
        bool blocks = Enumerable.Range(0, m.Height).All(yy => Enumerable.Range(0, m.Width).All(xx => m.IsPropBlocked(xx, yy) == y.Map.IsPropBlocked(xx, yy)));
        var h0 = x.ComputeChecksum();
        x.Props.Items[0].Hp += 1; bool h1 = x.ComputeChecksum() != h0; x.Props.Items[0].Hp -= 1;
        for (int i = 0; i < 300; i++) { x.Step(); y.Step(); }
        check(broken > 0 && same && state && blocks && h1 && x.ComputeChecksum() == y.ComputeChecksum(),
            $"소품 스냅샷: 부서진 소품 {broken}/{x.Props.Count} 저장 바이트 동일 {same} · 상태 일치 {state} · 막는 칸 복원 {blocks} · 체크섬 반영 {h1} · 300틱 이어 돌려도 같은 체크섬 (형식 {Snapshot.FormatVersion})");
    }

    /// <summary>T28: 군집 소품 + 불규칙 숲 (props.json 플래그, 기본 꺼짐): 결정성 · 대칭 · 소품 총량 ±10% · 최근접 이웃 거리 감소 · 직사각형 숲 직선 변 제거 · 숲 면적 ±10%.</summary>
    static void TestCoverClusterForest()
    {
        var rules = PropDefs.Current.Rules;
        bool o0 = rules.OrganicForest, c0 = rules.Scatter.Clump;
        try
        {
            check(!o0 && !c0, "T28 플래그 기본 꺼짐 (organicForest / scatter.clump = false)");
            int n0 = 0, n1 = 0; double nn0 = 0, nn1 = 0; int f0 = 0, f1 = 0; bool det = true, sym = true;
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                rules.OrganicForest = false; rules.Scatter.Clump = false;
                var a = GameMap.Generate(160, 160, seed);
                rules.OrganicForest = true; rules.Scatter.Clump = true;
                var b = GameMap.Generate(160, 160, seed); var b2 = GameMap.Generate(160, 160, seed);
                det &= b.PropSeeds.SequenceEqual(b2.PropSeeds) && TerrainHash(b) == TerrainHash(b2);
                for (int y = 0; y < 160 && sym; y++) for (int x = 0; x < 160; x++) if ((b.Get(x, y) == TerrainType.Forest) != (b.Get(159 - x, 159 - y) == TerrainType.Forest)) { sym = false; break; }
                n0 += a.PropSeeds.Count; n1 += b.PropSeeds.Count; nn0 += MeanNn(a); nn1 += MeanNn(b);
                f0 += CountForest(a); f1 += CountForest(b);
            }
            check(det, "T28 켜면 같은 시드 -> 같은 지도 (소품 · 지형)");
            check(sym, "T28 켜도 숲이 180도 대칭");
            check(Math.Abs(n1 - n0) <= n0 * 0.10, $"T28 소품 총량 {n0} -> {n1} (±10%)");
            check(nn1 < nn0 * 0.8, $"T28 소품 최근접 이웃 거리 평균 {nn0 / 8:F2} -> {nn1 / 8:F2} (20% 이상 감소)");
            check(Math.Abs(f1 - f0) <= f0 * 0.10, $"T28 생성 맵 숲 면적 {f0} -> {f1} (±10%)");
            // 지도 파일의 직사각형 숲
            const string json = "{\"width\":80,\"height\":60,\"base\":\"grass\",\"starts\":[[5,5],[75,55]],\"paint\":[{\"terrain\":\"forest\",\"rect\":[20,15,30,24]}]}";
            rules.OrganicForest = false; var r0 = MapFile.LoadText(json, "t28r", 1);
            rules.OrganicForest = true; var r1 = MapFile.LoadText(json, "t28r", 1);
            int a0 = CountForest(r0), a1 = CountForest(r1);
            check(LongestStraight(r0) == 30, $"T28 옛 직사각형 숲 직선 변 {LongestStraight(r0)} (30)");
            check(LongestStraight(r1) <= 14, $"T28 불규칙 숲 최장 직선 변 {LongestStraight(r1)} (<= 14)");
            check(Math.Abs(a1 - a0) <= a0 * 0.10, $"T28 직사각형 숲 면적 {a0} -> {a1} (±10%)");
        }
        finally { rules.OrganicForest = o0; rules.Scatter.Clump = c0; }
    }

    /// <summary>PR1: 자연 엄폐 소품 13종 정의 (범위 · 파괴 변종 참조 무결성 · 지형표 참조) + propsV2 (기본 꺼짐): 꺼짐 = 기존 배치, 켬 = 같은 시드 두 번 동일 + 기존 소품은 그대로 + 새 종류가 놓임.</summary>
    static void TestPropsV2()
    {
        var pd = PropDefs.Current;
        var rules = pd.Rules;
        string[] v2 = { "bush", "sorghum", "stump", "log_pile", "log_pile_scattered", "rock_outcrop", "dead_tree", "dead_tree_fallen", "dirt_mound", "willow", "willow_fallen", "reed", "snow_mound" };
        bool has = v2.All(k => pd.Types.ContainsKey(k));
        bool range = pd.Types.Values.All(t => t.Cover > 0f && t.Cover <= 1f && t.LosBlock >= 0f && t.LosBlock <= 1f && t.Radius > 0f && t.Radius <= 2f && t.Hp >= 0f);
        bool refs = pd.Types.Values.All(t => t.Destroyed.Length == 0 || (pd.Types.TryGetValue(t.Destroyed, out var d) && d.Hp == 0f && t.Hp > 0f && t.Destroyed != t.Key));
        bool noHpNoRef = pd.Types.Values.All(t => t.Hp > 0f || t.Destroyed.Length == 0);
        bool tables = rules.Scatter.V2.All(kv => GameMap.V2Biomes.Contains(kv.Key) && kv.Value.All(e => e.W >= 0 && pd.Types.ContainsKey(e.Type)))
                      && GameMap.V2Biomes.All(b => rules.Scatter.V2.TryGetValue(b, out var l) && l.Any(e => e.W > 0));
        check(has && range && refs && noHpNoRef && tables, $"PR1 소품 정의: 13종 있음 {has} · 범위 {range} · 파괴 변종 참조 {refs}/{noHpNoRef} · 지형표 참조 {tables}");
        bool v0 = rules.Scatter.PropsV2;
        try
        {
            check(!v0, "PR1 propsV2 기본 꺼짐");
            bool det = true, prefix = true, added = false; int nOff = 0, nOn = 0;
            foreach (int seed in new[] { 1, 2, 3, 4 })
            {
                rules.Scatter.PropsV2 = false; var a = GameMap.Generate(160, 160, seed); var a2 = GameMap.Generate(160, 160, seed);
                det &= a.PropSeeds.SequenceEqual(a2.PropSeeds);
                rules.Scatter.PropsV2 = true; var b = GameMap.Generate(160, 160, seed); var b2 = GameMap.Generate(160, 160, seed);
                det &= b.PropSeeds.SequenceEqual(b2.PropSeeds);
                prefix &= b.PropSeeds.Take(a.PropSeeds.Count).SequenceEqual(a.PropSeeds);
                added |= b.PropSeeds.Skip(a.PropSeeds.Count).Any(p => v2.Contains(p.Type) && p.Type != "rock_outcrop") ;
                nOff += a.PropSeeds.Count; nOn += b.PropSeeds.Count;
            }
            check(det && prefix && added && nOn > nOff, $"PR1 propsV2: 같은 시드 -> 같은 소품 {det} · 켜도 기존 소품 그대로(앞부분 동일) {prefix} · 새 종류 놓임 {added} · 총 {nOff} -> {nOn}");
        }
        finally { rules.Scatter.PropsV2 = v0; }
    }

    static long TerrainHash(GameMap m) { long h = 17; for (int y = 0; y < m.Height; y++) for (int x = 0; x < m.Width; x++) h = h * 31 + (int)m.Get(x, y) + (m.IsHigh(x, y) ? 100 : 0); return h; }

    static int CountForest(GameMap m) { int n = 0; for (int y = 0; y < m.Height; y++) for (int x = 0; x < m.Width; x++) if (m.Get(x, y) == TerrainType.Forest) n++; return n; }

    static double MeanNn(GameMap m)
    {
        var p = m.PropSeeds; if (p.Count < 2) return 0; double sum = 0;
        for (int i = 0; i < p.Count; i++)
        {
            double best = double.MaxValue;
            for (int j = 0; j < p.Count; j++) if (i != j) best = Math.Min(best, Math.Sqrt((p[i].X - p[j].X) * (p[i].X - p[j].X) + (p[i].Y - p[j].Y) * (p[i].Y - p[j].Y)));
            sum += best;
        }
        return sum / p.Count;
    }

    static int LongestStraight(GameMap m)
    {
        bool F(int x, int y) => m.InBounds(x, y) && m.Get(x, y) == TerrainType.Forest;
        int best = 0;
        for (int dir = 0; dir < 4; dir++)
        {
            int dx = dir == 2 ? -1 : dir == 3 ? 1 : 0, dy = dir == 0 ? -1 : dir == 1 ? 1 : 0; bool horiz = dy != 0;
            for (int o = 0; o < (horiz ? m.Height : m.Width); o++)
            {
                int run = 0;
                for (int i = 0; i < (horiz ? m.Width : m.Height); i++)
                {
                    int x = horiz ? i : o, y = horiz ? o : i;
                    if (F(x, y) && !F(x + dx, y + dy)) { run++; best = Math.Max(best, run); } else run = 0;
                }
            }
        }
        return best;
    }
}
