using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// cavcmd (2026-10-19 사용자 결정): ① 기병 장교 `officer_cavalry` (장교 규칙 — 지휘 · 승계 · 임시 진급 · 경험치 — 그대로, 말 위에서 권총 · 군도)
/// ② 기병 분대 부사관 기본 편성 (장교 상실 뒤 N3 부사관 대리) ③ 기병 중대 미션은 보병 장교 대신 기병 장교. 창 없이. KD_SELFTEST_ONLY=cavcmd 로 단독 실행.
/// </summary>
public static class CavCmdSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static OfficerRulesDef R => defs.Officers;
    static readonly SnapshotMeta Meta = new("cavcmd", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[cavcmd 기병 장교 · 기병 부사관]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestDef();
            TestCommandSuccession();
            TestSabreCharge();
            TestMissions();
            TestCavalryNco();
            TestMissionOverride();
            TestSnapshot();
            TestAiOfficerBack();
        }
        catch (Exception e) { check(false, $"cavcmd 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static List<Unit> Troop(World w, int officers, int riders, Vector2 c)
    {
        var list = new List<Unit>();
        for (int i = 0; i < officers; i++) list.Add(w.SpawnUnit(defs.Units["officer_cavalry"], 1, c + new Vector2(i * 1.5f, -3f)));
        for (int i = 0; i < riders; i++) list.Add(w.SpawnUnit(defs.Units["cavalry_default"], 1, c + new Vector2((i % 8) * 1.4f, (i / 8) * 1.4f)));
        return list;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    /// <summary>미션 월드의 기병 중대 (장교를 뺀 구성원 전원 기마).</summary>
    static List<Company> CavCompanies(World w) => w.Companies.Where(c => c.Members.Count > 0 && FormationRules.CavalryOnly(c.Members)).OrderBy(c => c.Id).ToList();

    // ---------------------------------------------------------------- ① 정의

    static void TestDef()
    {
        var d = defs.Units["officer_cavalry"];
        var off = defs.Units["officer"];
        var cav = defs.Units["cavalry_default"];
        check(d.IsOfficer && d.Mounted && d.CommandRadius == off.CommandRadius && d.Hp == off.Hp && d.Weapon == off.Weapon && d.Melee == off.Melee && d.SuppressResist == 0
              && d.Speed == cav.Speed && d.ChargeSpeedMul == cav.ChargeSpeedMul && d.StaminaMul == cav.StaminaMul && d.Radius == cav.Radius && !d.CanOccupyTrench,
            $"기병 장교 정의: 장교 (지휘 범위 {d.CommandRadius}, hp {d.Hp}, {d.Weapon} · {d.Melee}, 제압 안 받음) + 기병 이동 (속도 {d.Speed} · 돌격 ×{d.ChargeSpeedMul} · 스태미나 ×{d.StaminaMul}), 참호 안 들어감");
        var w = Bare();
        var u = w.SpawnUnit(d, 1, new Vector2(50, 50));
        var r = w.SpawnUnit(cav, 1, new Vector2(54, 50));
        check(w.UnitSpeedForTest(u) == w.UnitSpeedForTest(r) && u.StaminaCap == r.StaminaCap, $"기병 장교 이동 속도 {w.UnitSpeedForTest(u):0.00} = 기병 {w.UnitSpeedForTest(r):0.00}, 스태미나 한도 같음");
    }

    // ---------------------------------------------------------------- ① 지휘 · 승계 · 임시 진급 · 경험치 · 지휘 범위

    static void TestCommandSuccession()
    {
        var w = Bare();
        var us = Troop(w, 2, 20, new Vector2(40, 40));
        w.FormCompany(1, us);
        w.Step();
        var c = us[0].Company!;
        var offs = us.Where(u => u.IsOfficer).OrderBy(u => u.Id).ToList();
        check(c.Commander == offs[0] && w.CommandLevelOf(c) == CommandLevel.Officer && w.PostOf(offs[0]) == OfficerPost.Company && FormationRules.CavalryOnly(c.Members),
            $"기병 장교가 기병 중대 지휘 (지휘관 {c.Commander?.Def.Key}, 직책 {w.PostOf(offs[0])}), 기병 대형 조건 그대로");
        var rider = us.First(u => !u.IsOfficer && Vector2.Distance(u.Pos, offs[0].Pos) <= offs[0].Def.CommandRadius);
        check(w.NearOfficer(rider), "기병 장교 지휘 범위 안 기병 = NearOfficer (제압 회복 · 사격 간격 효과)");
        // 승계: 첫 장교 전사 → 둘째 장교가 지휘 · 직책 계급으로 임시 진급 · 인계
        int rank0 = offs[1].OfficerRank;
        offs[0].Hp = 0;
        w.Step();
        int ho = w.HandoverTicks(offs[1]);
        check(c.Commander == offs[1] && offs[1].TempRank > 0 && offs[1].OfficerRank == R.CompanyRank && w.InHandover(c) && w.CommandLevelOf(c) == CommandLevel.None,
            $"기병 장교 전사 → 다음 기병 장교 승계 ({rank0} → 임시 {offs[1].OfficerRank}, +{offs[1].TempRank}), 인계 {ho}틱 동안 지휘 없음");
        while (w.InHandover(c)) w.Step();
        check(w.CommandLevelOf(c) == CommandLevel.Officer, "인계 끝 → 기병 장교 지휘");
        // 경험치: 사살 → 중대 장교 경험치
        int xp0 = offs[1].OfficerXp;
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, rider.Pos + new Vector2(0, 3));
        foe.Hp = 1;
        w.Attack(1, new[] { rider }, foe);
        for (int i = 0; i < 600 && foe.Alive; i++) w.Step();
        check(!foe.Alive && offs[1].OfficerXp > xp0, $"기병 사살 → 기병 장교 경험치 {xp0} → {offs[1].OfficerXp}");
    }

    // ---------------------------------------------------------------- ① 말 위 군도 돌격 (기병 충격 규칙 그대로)

    static void TestSabreCharge()
    {
        var w = Bare();
        defs.CombatRules.OfficerAvoid = false;   // N35: 장교 단독 돌격 차단을 끔 (군도 충격 규칙만 확인)
        var o = w.SpawnUnit(defs.Units["officer_cavalry"], 1, new Vector2(30, 60));
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 60));
        foe.Hp = 5000;
        w.Hold(2, new[] { foe });
        w.Charge(1, new[] { o }, foe.Pos, foe);
        float hp0 = foe.Hp; bool impact = false;
        for (int t = 0; t < 60 * 12 && !impact; t++) { w.Step(); impact |= o.ImpactSpent; }
        defs.CombatRules.OfficerAvoid = true;
        check(impact && foe.Hp < hp0, $"기병 장교 군도 돌격: 충격 {impact}, 피해 {hp0 - foe.Hp:0}");
    }

    // ---------------------------------------------------------------- ③ 미션: 기병 중대 = 기병 장교

    static void TestMissions()
    {
        int cavCos = 0, mountedCmd = 0, infOff = 0;
        var names = new List<string>();
        foreach (var id in new[] { "ai_bn_meeting", "ai_bn_assault", "ai_kr_vs_ft_1000" })
        {
            var w = World.CreateMissionWorld(MissionDef.Load(id), defs, 1);
            foreach (var c in CavCompanies(w))
            {
                cavCos++;
                if (c.Commander is { } k && k.Def.Mounted && k.Def.Key == "officer_cavalry") mountedCmd++;
                infOff += c.Members.Count(u => u.IsOfficer && !u.Def.Mounted);
                names.Add($"{id}:{c.Owner}:{c.Members.Count}");
            }
        }
        check(cavCos >= 6 && mountedCmd == cavCos && infOff == 0, $"미션 기병 중대 {cavCos}개 [{string.Join(" ", names)}]: 지휘관 기병 장교 {mountedCmd}, 보병 장교 {infOff}");
    }

    // ---------------------------------------------------------------- ② 기병 분대 부사관 · 장교 상실 뒤 부사관 대리

    static void TestCavalryNco()
    {
        var w = World.CreateMissionWorld(MissionDef.Load("ai_kr_vs_ft_1000"), defs, 1);
        var cos = CavCompanies(w);
        int sq = 0, nco = 0;
        foreach (var c in cos) foreach (var q in c.Squads) { sq++; if (q.Leader is { IsNco: true, Def.Mounted: true }) nco++; }
        check(cos.Count == 2 && sq >= 8 && nco == sq, $"기병 분대 부사관 기본 편성: 기병 중대 {cos.Count}개 분대 {sq}개 중 분대장 부사관 {nco} (계급 squadLeaderRank {defs.CombatRules.SquadLeaderRank})");
        var co = cos.First(c => c.Owner == 1);
        var off = co.Commander!;
        off.Hp = 0;
        w.Step();
        int ho = (int)MathF.Round(R.NcoHandoverSec / Config.SimDt);
        var acting = w.ActingNco(co);
        check(co.Commander == null && w.InHandover(co) && co.HandoverUntil == w.Tick - 1 + ho && acting is { Def.Mounted: true, IsNco: true },
            $"기병 장교 전사 → 부사관 대리 인계 {R.NcoHandoverSec}초 (다음 지휘자 {acting?.Def.Key} 계급 {acting?.Rank})");
        while (w.InHandover(co)) w.Step();
        check(w.CommandLevelOf(co) == CommandLevel.Nco && w.CommanderOf(co) == acting && w.TacticAllowed(co, CompanyTactic.FireManoeuvre) && !w.TacticAllowed(co, CompanyTactic.FlankLeft),
            "인계 끝 → 기병 부사관 대리 지휘 (N3 규칙: 화력-기동만)");
        // 분대장 부사관이 하나씩 쓰러지면 다음 분대의 부사관
        acting!.Hp = 0;
        w.Step();
        var next = w.ActingNco(co);
        check(next != null && next != acting && next.IsNco && w.CommandLevelOf(co) == CommandLevel.Nco, $"대리 부사관 전사 → 다른 기병 분대 부사관 ({next?.Rank})");
    }

    // ---------------------------------------------------------------- ② 미션 squadLeaderRank 덮어쓰기

    static void TestMissionOverride()
    {
        string Desc(Company c) => string.Join(",", c.Squads.Select(q => q.Leader?.Rank ?? -1));
        var res = new List<string>();
        bool ok = true;
        foreach (var (rank, expectNco) in new[] { (0, false), (6, true) })
        {
            var def = MissionDef.Load("ai_kr_vs_ft_1000");
            def.Forces.First(f => f.Id == "f1_cav").SquadLeaderRank = rank;
            var w = World.CreateMissionWorld(def, defs, 1);
            var co = CavCompanies(w).First(c => c.Owner == 1);
            var other = CavCompanies(w).First(c => c.Owner == 2);
            bool good = rank == 0 ? co.Squads.All(q => q.Leader is not { IsNco: true }) : co.Squads.All(q => q.Leader is { Rank: 6 });
            good &= other.Squads.All(q => q.Leader is { IsNco: true } l && l.Rank == defs.CombatRules.SquadLeaderRank);   // 다른 세력 기병은 기본 규칙
            if (!expectNco)
            {
                co.Commander!.Hp = 0;
                Steps(w, (int)MathF.Round(R.NcoHandoverSec / Config.SimDt) + 2);
                good &= w.CommandLevelOf(co) == CommandLevel.None;
            }
            ok &= good;
            res.Add($"{rank}: [{Desc(co)}]");
        }
        check(ok, $"기병 squadLeaderRank 덮어쓰기: {string.Join(" · ", res)} (0 = 부사관 없음 → 장교 상실 뒤 지휘 없음, 6 = 분대장 6), 다른 세력은 기본 하사");
    }

    // ---------------------------------------------------------------- 전술 AI 기병 돌격: 장교는 뒤에서 따라감 (cavalry.officerBack)

    static void TestAiOfficerBack()
    {
        var def = MissionDef.Load("ai_bn_meeting");
        var w = World.CreateMissionWorld(def, defs, 1, new List<DeployEntry>());
        World.AttachMissionAi(w, def.Player, true);
        var cos = CavCompanies(w);
        bool seen = false, officerCharging = false, ridersCharging = false; float gap = 0;
        for (int i = 0; i < (int)(200 / Config.SimDt) && !seen; i++)
        {
            long before = w.Stats.CavOfficerHeldBack;
            w.Step();
            if (w.Stats.CavOfficerHeldBack == before) continue;
            w.Step();   // 명령 실행 틱
            foreach (var c in cos)
            {
                if (c.Commander is not { } o || !c.Members.Exists(u => u.Charging)) continue;
                seen = true; officerCharging |= o.Charging; ridersCharging = true;
                gap = c.Members.Where(u => u.Charging).Min(u => Vector2.Distance(u.Pos, o.Pos));
            }
        }
        check(seen && ridersCharging && !officerCharging && w.CavalryRules.OfficerBack > 0,
            $"전술 AI 기병 돌격: 기수 돌격, 기병 장교는 돌격하지 않고 뒤 {w.CavalryRules.OfficerBack}칸으로 따라감 (관찰 {seen}, 장교 돌격 {officerCharging}, 장교 ↔ 가장 가까운 돌격 기수 {gap:0.0}칸)");
    }

    // ---------------------------------------------------------------- 저장 (상태 추가 없음 — 기존 지휘 · 계급 필드)

    static void TestSnapshot()
    {
        var w = World.CreateMissionWorld(MissionDef.Load("ai_bn_meeting"), defs, 1);
        var co = CavCompanies(w).First(c => c.Owner == 1);
        Steps(w, 30);
        co.Commander!.OfficerXp = 55;
        co.Commander.Hp = 0;   // 기병 장교 전사 → 부사관 대리 인계 중 저장
        w.Step();
        bool state = w.InHandover(co) && w.ActingNco(co) is { Def.Mounted: true };
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var yc = y.Companies.First(k => k.Id == co.Id);
        bool fields = yc.HandoverUntil == co.HandoverUntil && yc.Commander == null && y.ActingNco(yc)?.Id == w.ActingNco(co)?.Id
                      && y.Units.Where(u => u.Def.Key == "officer_cavalry").All(u => w.Find(u.Id) is Unit o && o.OfficerBase == u.OfficerBase && o.TempRank == u.TempRank && o.OfficerXp == u.OfficerXp);
        check(state && same && fields && w.ComputeChecksum() == y.ComputeChecksum(), "기병 장교 · 기병 부사관 대리 인계 스냅샷: 저장 → 불러오기 → 저장 바이트 동일, 체크섬 동일");
        Steps(w, 600); Steps(y, 600);
        check(w.ComputeChecksum() == y.ComputeChecksum() && y.CommandLevelOf(yc) == w.CommandLevelOf(co), $"불러온 월드를 600틱 이어 돌려도 같은 체크섬 (지휘 {y.CommandLevelOf(yc)})");
    }
}
