using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// M1 세력 사기 ↔ 제압 감소 배수 (Morale.cs, ops/DECISIONS BH2 · BH3): 배수 공식 (50 정확히 1 · 0 / 100 끝값 · 자름) ·
/// 실제 감소 속도 (사기 0 &lt; 50 &lt; 100) · 사기 50 = 설정 안 한 월드와 체크섬 동일 · 저장 왕복 · 리플레이 머리말 재현. KD_SELFTEST_ONLY=morale.
/// </summary>
public static class MoraleSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("morale", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[M1 세력 사기 · 제압 감소]");
        try
        {
            TestFormula();
            TestDecay();
            TestNeutral();
            TestRoundTrip();
            TestReplay(opt);
            TestCampaignLink(opt);
        }
        catch (Exception e) { check(false, $"morale 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 60)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2);
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void TestFormula()
    {
        var c = defs.CombatRules;
        float m0 = World.MoraleSuppressDecayMul(0, c), m50 = World.MoraleSuppressDecayMul(50, c), m100 = World.MoraleSuppressDecayMul(100, c);
        float m25 = World.MoraleSuppressDecayMul(25, c), m75 = World.MoraleSuppressDecayMul(75, c);
        bool clamp = World.MoraleSuppressDecayMul(-20, c) == m0 && World.MoraleSuppressDecayMul(300, c) == m100;
        bool keys = c.MoraleSuppressDecayAt0 == 0.75f && c.MoraleSuppressDecayAt100 == 1.25f;   // 자리표시 (balance.json) — 0.3 밸런스 때 바뀌면 이 줄도 같이
        check(m50 == 1f && m0 == c.MoraleSuppressDecayAt0 && m100 == c.MoraleSuppressDecayAt100 && Math.Abs(m25 - 0.875f) < 1e-6f && Math.Abs(m75 - 1.125f) < 1e-6f && clamp && keys,
              $"배수: 사기 0 ×{m0} · 25 ×{m25} · 50 ×{m50} · 75 ×{m75} · 100 ×{m100}, 범위 밖 자름 {clamp}, balance 키 {keys}");
        var w = Bare();
        w.SetFactionMorale(1, 140); int hi = w.Faction(1).Morale;
        w.SetFactionMorale(1, -5); int lo = w.Faction(1).Morale;
        bool threw = false; try { w.SetFactionMorale(0, 60); } catch (ArgumentOutOfRangeException) { threw = true; }
        check(hi == 100 && lo == 0 && threw && w.Faction(2).Morale == Faction.MoraleDefault, $"진입점 SetFactionMorale: 140→{hi} · -5→{lo} · 중립 세력 0 거부 {threw} · 기본 {w.Faction(2).Morale}");
    }

    /// <summary>사기 m 인 세력 1 이병 하나가 제압 800 → 0 까지 걸린 틱.</summary>
    static int DecayTicks(int? morale)
    {
        var w = Bare();
        if (morale is int m) w.SetFactionMorale(1, m);
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20, 20));
        w.Step();
        u.Suppression = 800;
        long t0 = w.Tick;
        for (int t = 0; t < 60 * 120; t++) { w.Step(); if (u.Suppression == 0) return (int)(w.Tick - t0); }
        return -1;
    }

    static void TestDecay()
    {
        int d0 = DecayTicks(0), d50 = DecayTicks(50), d100 = DecayTicks(100), dn = DecayTicks(null);
        float r0 = d50 > 0 ? (float)d0 / d50 : 0, r100 = d50 > 0 ? (float)d100 / d50 : 0;
        // 감소량 ∝ 배수 → 걸린 시간 ∝ 1/배수. 0.25초마다 정수 반올림 (6.25×배수 → 5/7/8) 이라 계단 오차 ±0.12 허용
        check(d0 > d50 && d50 > d100 && d100 > 0 && dn == d50 && Math.Abs(r0 - 1f / 0.75f) < 0.12f && Math.Abs(r100 - 1f / 1.25f) < 0.12f,
              $"제압 800 → 0: 사기 0 {d0}틱 (×{r0:0.000}, 기대 {1f / 0.75f:0.000}) · 50 {d50}틱 · 100 {d100}틱 (×{r100:0.000}, 기대 0.800) · 설정 안 함 {dn}틱");
    }

    /// <summary>사기 50 (명시) = 설정 안 한 월드: 매 틱 체크섬 동일 (골든 불변 근거). 다른 사기는 체크섬 반영.</summary>
    static void TestNeutral()
    {
        var a = Bare(); var b = Bare(); var c = Bare();
        b.SetFactionMorale(1, 50); c.SetFactionMorale(1, 51);
        foreach (var w in new[] { a, b, c }) { var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20, 20)); w.Step(); u.Suppression = 600; }
        bool diff = b.ComputeChecksum() != c.ComputeChecksum(), same = a.ComputeChecksum() == b.ComputeChecksum();
        for (int t = 0; t < 600 && same; t++) { a.Step(); b.Step(); same = a.ComputeChecksum() == b.ComputeChecksum(); }
        check(same && diff,$"사기 50 = 기본: 600틱 체크섬 동일 {same} · 사기 51 은 체크섬 반영");
    }

    static void TestRoundTrip()
    {
        var w = Bare();
        w.SetFactionMorale(1, 30); w.SetFactionMorale(2, 90);
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20, 20));
        w.Step(); u.Suppression = 700; w.Step();
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool fields = y.Faction(1).Morale == 30 && y.Faction(2).Morale == 90;
        for (int t = 0; t < 300; t++) { w.Step(); y.Step(); }
        check(same && fields && w.ComputeChecksum() == y.ComputeChecksum(), $"저장: 바이트 동일 {same} · 사기 30/90 복원 {fields} · 이어 돌려도 같음 (형식 {Snapshot.FormatVersion})");
    }

    static void TestReplay(GameOptions opt)
    {
        var w = World.CreateStandard(GameMap.Generate(64, 64, opt.Seed, 2), defs);
        w.RecordCommands = true;
        w.SetFactionMorale(1, 80);
        w.Issue(new DebugSpawnCommand(1, "infantry", new Vector2(20, 20), 4));
        for (int i = 0; i < 120; i++) w.Step();
        var rep = ReplayFile.FromWorld(w);
        string path = TestTemp.Child("morale.kdr.json");
        rep.Save(path);
        var loaded = ReplayFile.Load(path);
        long bad = loaded.Verify(defs, out var report);
        loaded.Morale = null;
        long bad2 = loaded.Verify(defs, out _);
        check(rep.Morale is [80, 50] && bad < 0 && bad2 >= 0, $"리플레이: 머리말 사기 [{string.Join(",", rep.Morale ?? Array.Empty<int>())}] 재현 ({report}) · 머리말 빼면 어긋남 {bad2 >= 0}");
    }

    static CampaignState CampWith(params int[] morales)
    {
        var s = new CampaignState();
        s.Roster.Clear();
        for (int i = 0; i < morales.Length; i++) s.Roster.Add(new CampaignState.RosterCompany { Id = "c" + i, Strength = 1, Morale = morales[i] });
        return s;
    }

    /// <summary>M3 캠페인 사기 평균 → 미션 시작 사기 → 제압 감소 배수 경로, 단독 미션 = 설정 안 함, 저장 · 리플레이 왕복.</summary>
    static void TestCampaignLink(GameOptions opt)
    {
        // 1) 평균 (morale.troops 내림, 병력 0 제외): 40/50/61 = 151/3 = 50.33 -> 50 · 50/51 = 50.5 -> 50 · 빈 로스터 50 · 범위 밖은 자름
        int a1 = CampaignEconomy.AverageCompanyMorale(CampWith(40, 50, 61)), a2 = CampaignEconomy.AverageCompanyMorale(CampWith(50, 51));
        int a3 = CampaignEconomy.AverageCompanyMorale(CampWith()), a4 = CampaignEconomy.AverageCompanyMorale(CampWith(0, 0, 0)), a5 = CampaignEconomy.AverageCompanyMorale(CampWith(100, 200));
        check(a1 == 50 && a2 == 50 && a3 == 50 && a4 == 0 && a5 == 100, $"캠페인 사기 평균: 40/50/61 -> {a1} · 50/51 -> {a2} · 빈 {a3} · 전부 0 -> {a4} · 100/200 -> {a5}");

        // 2) 미션 시작 사기: 출격 기록(Pending) 이 그 미션일 때만. 단독 · 다른 미션 · 상태 없음 = null (SetFactionMorale 호출 안 함)
        var camp = CampWith(0, 0); camp.Pending = new CampaignState.PendingOperation { MissionId = "m_x" };
        int? start = CampaignEconomy.MissionStartMorale(camp, "m_x");
        bool none = CampaignEconomy.MissionStartMorale(camp, "m_y") == null && CampaignEconomy.MissionStartMorale(CampWith(0), "m_x") == null && CampaignEconomy.MissionStartMorale(null, "m_x") == null;
        check(start == 0 && none, $"미션 시작 사기: 출격 미션 {start} · 단독/다른 미션/상태 없음 null {none}");

        // 3) 통합: 평균 0 -> SetFactionMorale -> 제압 회복이 기본 50 보다 느림, 평균 100 은 빠름
        int dCamp0 = DecayTicks(start), dDefault = DecayTicks(null), dCamp100 = DecayTicks(CampaignEconomy.AverageCompanyMorale(CampWith(100, 100)));
        check(dCamp0 > dDefault && dDefault > dCamp100, $"통합: 캠페인 사기 0 제압 회복 {dCamp0}틱 > 기본 50 {dDefault}틱 > 캠페인 100 {dCamp100}틱");

        // 4) 저장 왕복 + 적 세력은 기본 50 그대로
        var w = Bare(); w.SetFactionMorale(1, start!.Value);
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20, 20)); w.Step(); u.Suppression = 700; w.Step();
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        for (int t = 0; t < 200; t++) { w.Step(); y.Step(); }
        check(y.Faction(1).Morale == 0 && y.Faction(2).Morale == Faction.MoraleDefault && w.ComputeChecksum() == y.ComputeChecksum(), $"저장 왕복: 아군 {y.Faction(1).Morale} · 적 {y.Faction(2).Morale} (미설정) · 이어 돌려도 체크섬 같음");

        // 5) 리플레이: 캠페인 사기 0 머리말 재현
        var rw = World.CreateStandard(GameMap.Generate(64, 64, opt.Seed, 2), defs);
        rw.RecordCommands = true; rw.SetFactionMorale(1, start.Value);
        rw.Issue(new DebugSpawnCommand(1, "infantry", new Vector2(20, 20), 4));
        for (int i = 0; i < 120; i++) rw.Step();
        var rep = ReplayFile.FromWorld(rw);
        string path = TestTemp.Child("morale_m3.kdr.json");
        rep.Save(path);
        long bad = ReplayFile.Load(path).Verify(defs, out var report);
        check(rep.Morale is [0, 50] && bad < 0, $"리플레이: 머리말 사기 [{string.Join(",", rep.Morale ?? Array.Empty<int>())}] 일치 ({report})");
    }
}
