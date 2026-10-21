using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 정찰병 (ROADMAP '정찰병 유닛', Core/Scouts.cs) 검증: 정의 · 정찰조 편성 · 앞서 가기 · 관측 지점 고르기 · 복귀 조건 4가지 · 자율 토글 · 곡사포 관측 · 자동 편입 · 저장 · 결정성. 창 없이.
/// KD_SELFTEST_ONLY=scouts 로 단독 실행.
/// </summary>
public static class ScoutSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("scouts", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[정찰병]");
        try
        {
            TestDef();
            TestFormation();
            TestSpotChoice();
            TestAhead();
            TestReturnConditions();
            TestAutonomyToggle();
            TestArtillerySpotting();
            TestAutoAttachAndLoss();
            TestSnapshot();
            TestDeterminism();
        }
        catch (Exception e) { check(false, $"정찰병 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World Bare(int size = 160)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        for (int f = 1; f <= 2; f++)
        {
            var b = w.Buildings.First(x => x.Owner == f);
            var k = w.SpawnUnit(defs.Units["supply_wagon"], f, b.Pos + new Vector2(f == 1 ? 3 : -3, 0));
            w.Hold(f, new[] { k });
        }
        w.Fog.Enabled = false;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static int Sec(float s) => (int)MathF.Round(s / Config.SimDt);

    /// <summary>중대 (보병 24 + 장교 1 + 정찰병 2) 를 (40,80) 에 세운다. 편성 후 1틱.</summary>
    static (World W, Company C, List<Unit> Body, List<Unit> Scouts, Unit Officer) Setup(int scouts = 2)
    {
        var w = Bare();
        var body = new List<Unit>();
        for (int i = 0; i < 24; i++) body.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40 + (i % 12) * 1f, 80 + (i / 12) * 1f)));
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(45, 78));
        var sc = new List<Unit>();
        for (int i = 0; i < scouts; i++) sc.Add(w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(44 + i * 2, 76)));
        var all = new List<Unit>(body) { off };
        all.AddRange(sc);
        w.FormCompany(1, all);
        w.Step();
        return (w, off.Company!, body, sc, off);
    }

    static void Advance(World w, Company c, List<Unit> body, float x = 140)
    {
        w.Move(1, body.Concat(c.Members.Where(m => m.IsOfficer)).Distinct().ToList(), new Vector2(x, 80), new FormSpec((int)FormType.Line, 0, new Vector2(1, 0)));
    }

    static Vector2 Center(Company c) => c.Members.Aggregate(Vector2.Zero, (a, u) => a + u.Pos) / c.Members.Count;

    /// <summary>두 정찰병이 모두 앞에 나가 (Advance 또는 Observe) 있을 때까지 최대 max 틱.</summary>
    /// <summary>중대가 도착해 멈추고 (자율 행동의 '움직임' 조건이 꺼지고) 정찰병이 모두 관측 중이 될 때까지.</summary>
    static bool Settle(World w, Company c, List<Unit> scouts, int max = 60 * 60)
    {
        for (int t = 0; t < max; t++)
        {
            w.Step();
            if (c.Anchor is { Arrived: false }) continue;
            if (scouts.All(s => !s.Alive || s.ScoutState == ScoutState.Observe)) { Steps(w, 30); return true; }
        }
        return false;
    }

    static bool RunUntilAway(World w, List<Unit> scouts, int max = 60 * 40)
    {
        for (int t = 0; t < max; t++)
        {
            w.Step();
            if (scouts.All(s => s.ScoutState is ScoutState.Observe)) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- 정의 · 편성

    static void TestDef()
    {
        var d = defs.Units["infantry_scout"]; var inf = defs.Units["infantry"];
        var w = Bare();
        var s = w.SpawnUnit(d, 1, new Vector2(30, 30)); var i = w.SpawnUnit(inf, 1, new Vector2(34, 30));
        check(d.Scout && d.Spotter && d.Name == "정찰병" && d.Hp == 100 && MathF.Abs(d.Speed - inf.Speed * 1.2f) < 0.01f && d.Weapon == "carbine" && defs.Weapons["carbine"].Damage == 35,
            $"infantry_scout: 정찰병 · HP 100 · 속도 ×1.2 ({d.Speed:0.00}) · 카빈 피해 35 · 관측(spotter) · scout 플래그");
        float ratio = w.SightOf(s) / (float)w.SightOf(i);
        check(MathF.Abs(ratio - defs.Vision.ScoutSightMul) < 0.08f, $"정찰병 시야 = 보병 ×{defs.Vision.ScoutSightMul} ({w.SightOf(s)} / {w.SightOf(i)})");
        check(defs.Start.Force.Count(g => g.Company && g.Units.GetValueOrDefault("infantry_scout") == 2) >= 2, "시작 병력: 중대마다 정찰병 2명 (start.force)");
    }

    static void TestFormation()
    {
        var (w, c, body, sc, off) = Setup();
        bool inScouts = c.Scouts.Count == 2 && sc.All(s => s.ScoutCompany == c) && sc.All(s => s.Company == null && s.Squad == null);
        bool notMembers = c.Members.Count == 25 && c.Members.All(m => !m.Def.Scout) && c.Squads.SelectMany(q => q.Members).All(m => !m.Def.Scout) && c.InitialStrength == 25;
        check(inScouts && notMembers, $"정찰조: 중대 정찰병 {c.Scouts.Count}명, 구성원 {c.Members.Count} (정찰병 제외), 분대 밖, 시작 병력 {c.InitialStrength}");
        // 정찰병 3명이면 perCompany(2) 만 붙고 나머지는 소속 없음
        var (w3, c3, _, sc3, _) = Setup(3);
        check(c3.Scouts.Count == defs.ScoutAi.PerCompany && sc3.Count(s => s.ScoutCompany == null) == 1, $"정찰병 3명 → 정찰조 상한 {defs.ScoutAi.PerCompany}명, 나머지 1명 소속 없음");
        // 정찰병만으로 편성하면 평범한 중대 (예전 동작)
        var wx = Bare();
        var only = Enumerable.Range(0, 3).Select(k => wx.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(30 + k, 30))).ToList();
        wx.FormCompany(1, only); wx.Step();
        check(only[0].Company is { } oc && oc.Members.Count == 3 && oc.Scouts.Count == 0, "정찰병만 편성하면 평범한 중대 (정찰조 없음)");
        // 해체: 정찰조 해제 + 자율 끔
        w.DisbandCompany(1, c); w.Step();
        check(sc.All(s => s.ScoutCompany == null && !s.ScoutAuto), "중대 해체 → 정찰병은 소속 없음 · 자율 꺼짐");
    }

    // ---------------------------------------------------------------- 관측 지점

    static void TestSpotChoice()
    {
        var w = Bare();
        var r = defs.ScoutAi;
        var flat = w.ScoutPickSpot(new Vector2(50.5f, 50.5f), r);
        w.Map.SetHigh(52, 50, true);
        var high = w.ScoutPickSpot(new Vector2(50.5f, 50.5f), r);
        check(flat == new Vector2(50.5f, 50.5f) && high == new Vector2(52.5f, 50.5f), $"관측 지점: 평지는 기준 칸 그대로 ({flat}), 근처 고지 칸을 고름 ({high})");
        w.Map.SetHigh(52, 50, false);
        // 숲 가장자리: 숲 덩어리 (53..58, 48..53) 의 가장자리 칸 (53, 50) 이 깊은 숲 (56, 50) 보다 낫다
        for (int y = 46; y <= 55; y++) for (int x = 53; x <= 60; x++) w.Map.Set(x, y, TerrainType.Forest);
        var edge = w.ScoutPickSpot(new Vector2(52.5f, 50.5f), r);
        check(w.Map.ForestDepth((int)edge.X, (int)edge.Y) == 1 && !w.Map.IsDeepForest((int)edge.X, (int)edge.Y) || (w.Map.Get((int)edge.X, (int)edge.Y) != TerrainType.Forest && w.Map.ForestDepth((int)edge.X + 1, (int)edge.Y) > 0),
            $"관측 지점: 숲 가장자리를 고름 ({edge}), 깊은 숲 · 평지 한가운데 아님");
        var deep = w.ScoutPickSpot(new Vector2(57.5f, 50.5f), r);
        check(!w.Map.IsDeepForest((int)deep.X, (int)deep.Y), $"관측 지점: 깊은 숲 한가운데에서 시작해도 시야가 트인 칸으로 ({deep})");
    }

    // ---------------------------------------------------------------- 앞서 가기

    static void TestAhead()
    {
        using var nd = TestDefs.NoCommandDelay();
        var (w, c, body, sc, off) = Setup();
        // 중대가 가만히 있으면 나가지 않는다
        Steps(w, Sec(8));
        check(sc.All(s => s.ScoutState == ScoutState.With), "중대가 서 있으면 정찰병은 중대와 함께 (나가지 않음)");
        Advance(w, c, body);
        bool settled = RunUntilAway(w, sc);
        var cen = Center(c);
        Vector2 dir = new(1, 0);
        var ahead = sc.Select(s => Vector2.Dot(s.Pos - cen, dir)).ToList();
        var r = defs.ScoutAi;
        // 중대가 앞으로 계속 가면 기준점이 따라 움직이므로 지점 도착 시점의 앞선 거리는 aheadMin - 탐색 반경 ~ aheadMax + 탐색 반경 + 한 번 갱신 거리
        bool inRange = ahead.All(a => a >= r.AheadMin - r.SearchRadius - 1f && a <= r.AheadMax + r.SearchRadius + 6f);
        check(settled && inRange && sc.All(s => s.Order == UnitOrder.Hold && s.ScoutObserving), $"중대 행군 → 정찰병이 앞 {string.Join(", ", ahead.Select(a => a.ToString("0.0")))}칸 지점에서 관측 (Hold + 쌍안경) — 허용 {r.AheadMin - r.SearchRadius}~{r.AheadMax + r.SearchRadius}");
        check(Math.Abs(sc[0].Pos.Y - sc[1].Pos.Y) >= r.Spread - r.SearchRadius * 2 - 0.5f, $"정찰조는 가로로 벌어져 선다 (세로 간격 {MathF.Abs(sc[0].Pos.Y - sc[1].Pos.Y):0.0}칸)");
        // 중대가 계속 가면 정찰병도 계속 앞서 간다 (지점을 건너뛰며) — 1초마다 12초 표본
        var samples = new List<float>(); bool noReturn = true, inReach = true;
        for (int k = 0; k < 12; k++)
        {
            Steps(w, 60);
            cen = Center(c);
            foreach (var s in sc)
            {
                samples.Add(Vector2.Dot(s.Pos - cen, dir));
                noReturn &= s.ScoutState is ScoutState.Advance or ScoutState.Observe;
                inReach &= Vector2.Distance(s.Pos, cen) <= r.MaxDist;
            }
        }
        float mean = samples.Average();
        check(noReturn && inReach && mean >= 5.5f && samples.Count(a => a > 0) >= samples.Count * 0.9f, $"12초 더 행군: 정찰병은 계속 중대 앞 (평균 {mean:0.0}칸, 최소 {samples.Min():0.0}, 최대 {samples.Max():0.0}) · 복귀 없음 · {r.MaxDist}칸 안");
    }

    // ---------------------------------------------------------------- 복귀 조건

    static void TestReturnConditions()
    {
        using var nd = TestDefs.NoCommandDelay();
        var r = defs.ScoutAi;
        // 관측 중인 정찰조를 만든다 (조건 없이 두면 10초 동안 그대로)
        {
            var (w, c, body, sc, off) = Setup();
            Advance(w, c, body, 70);
            bool ok = Settle(w, c, sc);
            Steps(w, Sec(10));
            check(ok && sc.All(s => s.ScoutState == ScoutState.Observe), "대조: 조건이 없으면 정찰병은 계속 관측 중");
        }
        // 1) 제압
        {
            var (w, c, body, sc, off) = Setup();
            Advance(w, c, body, 70); Settle(w, c, sc);
            sc[0].Suppression = (short)(r.ReturnSuppression + 50);
            Steps(w, 20);
            bool turned = sc[0].ScoutState == ScoutState.Return && sc[1].ScoutState == ScoutState.Observe;
            Steps(w, Sec(15));
            check(turned && sc[0].ScoutState == ScoutState.With && Vector2.Distance(sc[0].Pos, off.Pos) < r.ArriveDist + 1.5f, $"제압 {r.ReturnSuppression}+ → 장교에게 복귀 (돌아온 거리 {Vector2.Distance(sc[0].Pos, off.Pos):0.0}칸), 동료는 그대로 관측");
        }
        // 2) HP 50% 이하
        {
            var (w, c, body, sc, off) = Setup();
            Advance(w, c, body, 70); Settle(w, c, sc);
            sc[1].Hp = sc[1].MaxHp * r.ReturnHpFrac;
            Steps(w, 20);
            bool turned = sc[1].ScoutState == ScoutState.Return;
            Steps(w, Sec(15));
            // 쉬어도 HP 가 낮으면 다시 나가지 않는다 (중대가 계속 움직여도)
            Advance(w, c, body, 140); Steps(w, Sec(45));
            check(turned && sc[1].ScoutState == ScoutState.With && Vector2.Distance(sc[1].Pos, off.Pos) < r.FollowDist + 3f, $"HP {r.ReturnHpFrac:P0} 이하 → 복귀하고, 회복 전에는 다시 나가지 않고 중대를 따라감 ({Vector2.Distance(sc[1].Pos, off.Pos):0.0}칸)");
        }
        // 3) 적을 처음 본 뒤 5초
        {
            var (w, c, body, sc, off) = Setup();
            Advance(w, c, body, 70); Settle(w, c, sc);
            var enemy = w.SpawnUnit(defs.Units["supply_wagon"], 2, sc[0].Pos + new Vector2(17, 0));   // 비무장 적: 정찰병 시야 (21칸) 안
            w.Hold(2, new[] { enemy });
            Steps(w, 20);
            bool seen = sc[0].ScoutReportTick >= 0 && sc[0].ScoutState == ScoutState.Observe;
            Steps(w, Sec(r.ReportSec) - 40);
            bool before = sc[0].ScoutState == ScoutState.Observe;
            Steps(w, 80);
            bool after = sc[0].ScoutState == ScoutState.Return;
            check(seen && before && after, $"적 발견 → {r.ReportSec}초 동안은 관측, 지나면 본 정찰병이 복귀 (발견 {seen}, 직전 관측 {before}, 직후 복귀 {after})");
            Steps(w, Sec(r.CooldownSec - 10));
            check(sc.All(s => s.ScoutState == ScoutState.With || s.ScoutState == ScoutState.Return), "복귀 뒤 재출발 대기 시간 동안은 다시 나가지 않음");
        }
        // 4) 중대에서 15칸 초과
        {
            var (w, c, body, sc, off) = Setup();
            Advance(w, c, body, 70); Settle(w, c, sc);
            var cen = Center(c);
            sc[0].Pos = cen + new Vector2(r.MaxDist + 6, 0);
            Steps(w, 20);
            check(sc[0].ScoutState == ScoutState.Return && sc[1].ScoutState == ScoutState.Observe, $"중대에서 {r.MaxDist}칸 초과 → 복귀");
        }
    }

    // ---------------------------------------------------------------- 자율 토글

    static void TestAutonomyToggle()
    {
        using var nd = TestDefs.NoCommandDelay();
        var (w, c, body, sc, off) = Setup();
        // 직접 명령 → 자율 꺼짐
        w.Move(1, new[] { sc[0] }, new Vector2(30, 80));
        Steps(w, 3);
        Advance(w, c, body, 70);   // 중대 명령: 정찰조 자율을 다시 켠다 (직접 명령으로 꺼진 정찰병도)
        Steps(w, 3);
        check(sc[0].ScoutAuto && sc[1].ScoutAuto, "중대가 새 명령을 받으면 꺼졌던 정찰병 자율이 다시 켜짐");
        w.Move(1, new[] { sc[0] }, new Vector2(30, 80));
        Steps(w, 3);
        bool off0 = !sc[0].ScoutAuto && sc[1].ScoutAuto && sc[0].ScoutState == ScoutState.With;
        Settle(w, c, new List<Unit> { sc[1] });
        Steps(w, Sec(5));
        check(off0 && sc[1].ScoutState == ScoutState.Observe && sc[0].ScoutState == ScoutState.With && sc[0].Pos.X < Center(c).X, "직접 명령한 정찰병은 자율 꺼짐 (중대가 가도 안 나감), 다른 정찰병은 계속 자율");
        // 토글 명령
        w.SetScoutAuto(1, new[] { sc[0] }, true); Steps(w, 3);
        check(sc[0].ScoutAuto, "카드 토글 (SetScoutAutoCommand 켜기) → 자율 다시 켜짐");
        w.SetScoutAuto(1, sc, false); Steps(w, 3);
        check(!sc[0].ScoutAuto && !sc[1].ScoutAuto && sc.All(s => s.ScoutState == ScoutState.With), "토글 끄기 → 자율 꺼짐 · 상태 초기화");
        // 중대와 같이 명령 받은 정찰병: 중대 명령으로 취급해 자율 유지
        w.SetScoutAuto(1, sc, true); Steps(w, 3);
        w.Move(1, body.Concat(new[] { off }).Concat(sc).ToList(), new Vector2(110, 80));
        Steps(w, 3);
        check(sc.All(s => s.ScoutAuto) && sc.All(s => s.Group == null || s.Group.Target != new Vector2(110, 80)), "중대와 함께 선택해 내린 명령은 정찰병에게 직접 명령이 아님 (자율 유지)");
        // 리플레이 직렬화
        var cmd = new SetScoutAutoCommand(1, new[] { 5, 6 }, true);
        var json = System.Text.Json.JsonSerializer.Serialize<GameCommand>(cmd);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(json) as SetScoutAutoCommand;
        check(back != null && back.On && back.Units.SequenceEqual(new[] { 5, 6 }), "SetScoutAutoCommand 리플레이 JSON 왕복");
    }

    // ---------------------------------------------------------------- 곡사포 관측

    static void TestArtillerySpotting()
    {
        var w = Bare();
        var scout = w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(60, 80));
        var enemy = w.SpawnUnit(defs.Units["supply_wagon"], 2, new Vector2(60 + 20, 80));
        w.Hold(2, new[] { enemy });
        w.Hold(1, new[] { scout });
        w.Step();
        int sight = w.SightOf(scout);
        bool seen = w.SeenBySpotter(1, enemy);
        var officer = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(50, 80));
        w.Hold(1, new[] { officer });
        w.Step();
        bool seenWithOfficer = w.SeenBySpotter(1, enemy);
        // 장교의 시야는 이 거리를 못 본다 (정찰병이 없으면 관측 안 됨)
        scout.Hp = 0; w.Step();
        bool lost = w.SeenBySpotter(1, enemy);
        check(sight >= 20 && seen && seenWithOfficer && !lost, $"곡사포 관측: 정찰병 시야 {sight}칸 안의 표적은 SeenBySpotter, 정찰병이 죽으면 (장교 시야 {w.SightOf(officer)}칸 밖) 관측 안 됨");
    }

    // ---------------------------------------------------------------- 자동 편입 · 손실

    static void TestAutoAttachAndLoss()
    {
        var (w, c, body, sc, off) = Setup(0);
        var loose = Enumerable.Range(0, 3).Select(k => w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(60, 76 + k))).ToList();
        Steps(w, 35);
        check(c.Scouts.Count == defs.ScoutAi.PerCompany && loose.Count(s => s.ScoutCompany == c) == defs.ScoutAi.PerCompany && loose.Count(s => s.ScoutCompany == null) == 1, "소속 없는 정찰병은 가까운 중대의 빈 정찰조 자리에 자동 편입 (상한까지)");
        var far = w.SpawnUnit(defs.Units["infantry_scout"], 2, new Vector2(150, 150));   // 상대 세력: 중대 없음 → 그대로
        var manual = w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(70, 80));
        w.Move(1, new[] { manual }, new Vector2(72, 80));
        Steps(w, 70);
        check(far.ScoutCompany == null && manual.ScoutCompany == null && !manual.ScoutAuto, "직접 명령한 (자율 꺼진) 정찰병 · 다른 세력 정찰병은 편입되지 않음");
        var first = c.Scouts[0];
        first.Hp = 0; Steps(w, 2);
        check(c.Scouts.Count == defs.ScoutAi.PerCompany - 1 && !c.Scouts.Contains(first) && first.ScoutCompany == null, "정찰병 전사 → 정찰조에서 빠짐");
        // 중대 전멸 → 남은 정찰병 해제 (자율 유지)
        foreach (var u in c.Members.ToList()) u.Hp = 0;
        Steps(w, 2);
        var rest = loose.Where(s => s.Alive).ToList();
        check(w.Companies.Count == 0 && rest.All(s => s.ScoutCompany == null && s.ScoutAuto), "중대 전멸 → 정찰병 해제 (자율 유지, 다른 중대에 다시 붙을 수 있음)");
    }

    // ---------------------------------------------------------------- 저장 · 결정성

    static void TestSnapshot()
    {
        using var nd = TestDefs.NoCommandDelay();
        var (w, c, body, sc, off) = Setup();
        Advance(w, c, body, 100);
        for (int t = 0; t < Sec(9); t++) w.Step();   // 한창 나가는 중
        sc[1].Suppression = (short)(defs.ScoutAi.ReturnSuppression + 10);
        Steps(w, 20);   // 하나는 복귀 중
        w.SetScoutAuto(1, new[] { sc[0] }, false); w.Step();
        var states = sc.Select(s => (s.ScoutState, s.ScoutAuto)).ToList();
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var yc = y.Companies.First(k => k.Id == c.Id);
        bool restored = yc.Scouts.Select(s => s.Id).SequenceEqual(c.Scouts.Select(s => s.Id)) && yc.Scouts.Select(s => (s.ScoutState, s.ScoutAuto)).SequenceEqual(states)
            && yc.Scouts.All(s => s.ScoutCompany == yc) && yc.Scouts.Zip(c.Scouts, (a, b) => a.ScoutSpot == b.ScoutSpot && a.ScoutReportTick == b.ScoutReportTick && a.ScoutResumeTick == b.ScoutResumeTick).All(x => x);
        check(same && restored && w.ComputeChecksum() == y.ComputeChecksum(), $"정찰병 스냅샷: 저장 → 불러오기 → 저장 바이트 동일 · 정찰조 · 상태 복원 (상태 {string.Join("/", states.Select(s => s.ScoutState))})");
        Steps(w, 1800); Steps(y, 1800);
        check(w.ComputeChecksum() == y.ComputeChecksum() && w.Units.Where(u => u.Def.Scout).Select(u => u.Pos).SequenceEqual(y.Units.Where(u => u.Def.Scout).Select(u => u.Pos)), "불러온 월드를 30초 이어 돌려도 같은 체크섬 · 정찰병 위치");
    }

    static void TestDeterminism()
    {
        using var nd = TestDefs.NoCommandDelay();
        ulong Run()
        {
            var (w, c, body, sc, off) = Setup();
            Advance(w, c, body, 120);
            Steps(w, Sec(8));
            var e = w.SpawnUnit(defs.Units["supply_wagon"], 2, sc[0].Pos + new Vector2(20, 3));
            w.Hold(2, new[] { e });
            Steps(w, Sec(25));
            w.SetScoutAuto(1, sc, false); Steps(w, Sec(3)); w.SetScoutAuto(1, sc, true);
            Steps(w, Sec(20));
            return w.ComputeChecksum();
        }
        ulong a = Run(), b = Run();
        check(a == b, "정찰병 포함 시뮬 두 번 → 같은 체크섬");
    }
}
