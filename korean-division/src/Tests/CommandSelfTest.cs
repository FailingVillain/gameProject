using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 지휘 계급 (N3, CommandRanks.cs): 직책 계급 배정 · 승계 · 인계 시간 · 임시 진급 상한 3 · 부사관 대리 능력 · 경험치 획득과 효과 · 저장 · 미션 결과.
/// KD_SELFTEST_ONLY=command 로 단독 실행.
/// </summary>
public static class CommandSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("command", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[지휘 계급 · 승계 · 장교 경험치]");
        try
        {
            TestStandardPosts();
            TestCompanyPosts();
            TestSuccession();
            TestTempCap();
            TestPromotionCap();
            TestNcoFallback();
            TestXp();
            TestXpEffects();
            TestSnapshot();
            TestMissionResults();
        }
        catch (Exception e) { check(false, $"지휘 계급 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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

    static List<Unit> Group(World w, int officers, int men, Vector2 c, int faction = 1)
    {
        var list = new List<Unit>();
        for (int i = 0; i < officers; i++) list.Add(w.SpawnUnit(defs.Units["officer"], faction, c + new Vector2(i * 1.2f, -2f)));
        for (int i = 0; i < men; i++) list.Add(w.SpawnUnit(defs.Units["infantry"], faction, c + new Vector2(i % 8 - 3.5f, i / 8)));
        return list;
    }

    static Company Form(World w, List<Unit> us, int battalion = 0)
    {
        w.FormCompany(us[0].Owner, us, battalion);
        w.Step();
        return us[0].Company!;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    static OfficerRulesDef R => defs.Officers;

    // ---------------------------------------------------------------- 직책 계급

    static void TestStandardPosts()
    {
        var w = SnapshotSelfTest.ProbeGen(2);
        var bats = w.BattalionsOf(1).ToList();
        var b = bats.FirstOrDefault();
        var cos = w.CompaniesOf(1).ToList();
        var offs = cos.SelectMany(c => c.Members).Where(u => u.IsOfficer).ToList();
        bool ok = b != null && b.Companies.Count == 3 && b.Commander is { } bc && bc.OfficerBase == R.BattalionSeniorRank && bc.TempRank == 0
            && offs.Count == 3 && offs.Where(u => u != b.Commander && u != w.AssistantOf(b)).All(u => u.OfficerBase == R.CompanyRank && u.TempRank == 0)
            && w.AssistantOf(b) is { } asst && asst.OfficerBase == R.BattalionRank && asst.TempRank == 0
            && cos.All(c => c.Commander != null && c.HandoverUntil == 0) && b.HandoverUntil == 0
            && b.Companies.Count(c => c.Commander == b.Commander) == 1;
        check(ok, $"표준 시작 병력: 대대장 = 중령 (정식 {b?.Commander?.OfficerBase}), 부대대장 소령, 다른 중대장 대위, 중대마다 지휘관, 인계 없음 (장교 {offs.Count}, 계급 {string.Join(",", offs.Select(u => u.OfficerRank))})");
        check(RankNames.Of("korean", b!.Commander!) == "부령" && RankNames.Of("japanese", offs.First(u => u != b.Commander && u != w.AssistantOf(b))) == "대위", "계급 이름 (ranks.json): 부령 · 대위");
    }

    static void TestCompanyPosts()
    {
        var w = Bare();
        var us = Group(w, 3, 20, new Vector2(40, 40));
        var c = Form(w, us);
        var offs = us.Where(u => u.IsOfficer).OrderBy(u => u.Id).ToList();
        check(offs[0].OfficerRank == R.CompanyRank && offs[1].OfficerRank == R.SecondRank && offs[2].OfficerRank == R.JuniorRank && c.Commander == offs[0]
              && w.CommandLevelOf(c) == CommandLevel.Officer && w.PostOf(offs[0]) == OfficerPost.Company && w.PostOf(offs[1]) == OfficerPost.Staff,
            $"중대 편성: 장교 Id 순 대위 · 중위 · 소위, 지휘관 = 대위 (계급 {string.Join(",", offs.Select(u => u.OfficerRank))})");
        // 이미 계급이 있는 장교는 그대로 (재편성)
        w.DisbandCompany(1, c); w.Step();
        offs[2].OfficerBase = 4;
        var c2 = Form(w, us);
        check(offs[2].OfficerRank == 4 && c2.Commander == offs[2] && offs[0].OfficerRank == R.CompanyRank, "재편성: 정한 계급 유지, 지휘관 = 최고 서열 (소령)");
    }

    // ---------------------------------------------------------------- 승계 · 인계

    static void TestSuccession()
    {
        var w = Bare();
        var us = Group(w, 3, 20, new Vector2(40, 40));
        var c = Form(w, us);
        var offs = us.Where(u => u.IsOfficer).OrderBy(u => u.Id).ToList();
        offs[0].Hp = 0;
        w.Step();
        int ho = w.HandoverTicks(offs[1]);
        bool mid = c.Commander == offs[1] && offs[1].TempRank == 1 && offs[1].OfficerRank == R.CompanyRank && w.InHandover(c) && w.CommandLevelOf(c) == CommandLevel.None
                   && c.HandoverUntil == w.Tick - 1 + ho;
        check(mid, $"중대장 전사 → 다음 서열 (중위) 이 지휘, 대위로 임시 진급 (+{offs[1].TempRank}), 인계 {ho}틱 동안 지휘 없음");
        check(ho == (int)MathF.Round(R.HandoverSec / Config.SimDt), $"인계 시간 = handoverSec {R.HandoverSec}초 ({ho}틱, 경험치 0)");
        while (w.Tick < c.HandoverUntil - 1) w.Step();
        bool before = w.CommandLevelOf(c) == CommandLevel.None;
        w.Step();
        check(before && w.CommandLevelOf(c) == CommandLevel.Officer && !w.InHandover(c), "인계 끝 틱부터 장교 지휘 (그 전 틱까지 없음)");
        offs[2].OfficerXp = 500;   // 소위지만 경험치가 많다 (승계 인계 시간 단축 확인용)
        // 둘째도 전사 → 소위 (경험치 많음) 가 +2 임시 진급
        offs[1].Hp = 0;
        w.Step();
        int ho2 = w.HandoverTicks(offs[2]);
        check(c.Commander == offs[2] && offs[2].TempRank == 2 && offs[2].OfficerRank == R.CompanyRank && ho2 < ho,
            $"다음 승계: 소위 → 대위 임시 (+{offs[2].TempRank}), 경험치 단계 {w.XpLevel(offs[2])} 로 인계 짧음 ({ho2} < {ho}틱)");
        // 같은 틱에 지휘관 둘이 전사 → 살아 있는 쪽만 후보
        var w2 = Bare();
        var us2 = Group(w2, 3, 12, new Vector2(40, 40));
        var c3 = Form(w2, us2);
        var o2 = us2.Where(u => u.IsOfficer).OrderBy(u => u.Id).ToList();
        o2[0].Hp = 0; o2[1].Hp = 0;
        w2.Step();
        check(c3.Commander == o2[2] && o2[2].TempRank == 2, "같은 틱 장교 둘 전사 → 살아 있는 소위가 승계");
    }

    static void TestTempCap()
    {
        var w = Bare(160);
        var g1 = Group(w, 1, 12, new Vector2(40, 40));
        var g2 = Group(w, 1, 12, new Vector2(60, 40));
        var g3 = Group(w, 1, 12, new Vector2(80, 40));
        w.FormCompany(1, g1, 1); w.FormCompany(1, g2, 1); w.FormCompany(1, g3, 1);
        w.Step();
        var b = w.BattalionsOf(1).Single();
        var a = g1[0]; var bo = g2[0]; var co = g3[0];
        check(b.Commander == a && a.OfficerBase == R.CompanyRank && a.TempRank == 0 && a.OfficerRank == R.CompanyRank && w.PostOf(a) == OfficerPost.Battalion
            && w.AssistantOf(b) == bo && bo.TempRank == 0 && w.PostOf(bo) == OfficerPost.Assistant,
            $"전투 중 대대 편성: 대대장 = 경험치 동률 → 계급 → Id, 임시 진급 없음 ({a.TempRank}), 부대대장 지정");
        bo.OfficerXp = 500; co.OfficerXp = 900;
        check(w.AssistantOf(b) == co, "부대대장 = 대대장 다음 우선순위 (경험치)");
        co.OfficerXp = 0; bo.OfficerXp = 0;
        co.OfficerBase = 1;   // 소위
        a.Hp = 0; bo.Hp = 0;
        w.Step();
        check(b.Commander == co && co.TempRank == R.MaxTempPromotion && co.OfficerRank == 1 + R.MaxTempPromotion && w.InHandover(b),
            $"소위가 대대장 승계: 임시 진급 상한 {R.MaxTempPromotion} → {co.OfficerRank} (중령 아님), 대대 인계 중");
        check(b.Companies.All(c => c.Members.Count == 0 || c.Commander == co || c.Members.All(m => !m.IsOfficer)), "대대장은 장교가 없는 자기 중대도 지휘");
    }

    /// <summary>진급 상한 (사용자 결정 2026-10-19): 어떤 진급도 중령 (officers.maxPromotionRank 5) 위로 못 올린다. 미션이 배치한 대령은 그대로.</summary>
    static void TestPromotionCap()
    {
        check(R.MaxPromotionRank == 5 && Defs.Current.Officers.MaxPromotionRank == 5, "진급 상한 기본 = 중령 (5, balance.json officers.maxPromotionRank)");
        int saved = R.MaxPromotionRank;
        try
        {
            foreach (int cap in new[] { 5, 3 })
            {
                R.MaxPromotionRank = cap;
                var w = Bare(160);
                var g1 = Group(w, 1, 12, new Vector2(40, 40));
                var g2 = Group(w, 1, 12, new Vector2(60, 40));
                var g3 = Group(w, 1, 12, new Vector2(80, 40));
                w.FormCompany(1, g1, 1); w.FormCompany(1, g2, 1); w.FormCompany(1, g3, 1);
                w.Step();
                var b = w.BattalionsOf(1).Single();
                var a = g1[0]; var bo = g2[0]; var co = g3[0];
                co.OfficerBase = 1; a.Hp = 0; bo.Hp = 0;
                w.Step();
                int want = Math.Min(cap, 1 + R.MaxTempPromotion);
                check(b.Commander == co && co.OfficerRank == want && co.OfficerRank <= cap,
                    $"상한 {cap}: 소위 승계 임시 진급 → {co.OfficerRank} (기대 {want}, 상한 이하)");
            }
            // 상한은 진급에만: 이미 대령인 장교는 계급 유지, 임시 진급 없음
            R.MaxPromotionRank = 5;
            var w2 = Bare(160);
            var h1 = Group(w2, 1, 12, new Vector2(40, 40));
            var h2 = Group(w2, 1, 12, new Vector2(60, 40));
            w2.FormCompany(1, h1, 1); w2.FormCompany(1, h2, 1);
            w2.Step();
            var col = h2[0]; col.OfficerBase = (byte)R.MaxRank;
            h1[0].Hp = 0;
            w2.Step();
            check(col.OfficerRank == R.MaxRank && col.TempRank == 0, $"배치된 대령은 계급 유지 ({col.OfficerRank}), 임시 진급 없음 ({col.TempRank})");
        }
        finally { R.MaxPromotionRank = saved; }
    }

    // ---------------------------------------------------------------- 부사관 대리

    static void TestNcoFallback()
    {
        var w = Bare();
        var us = Group(w, 1, 12, new Vector2(40, 40));
        var c = Form(w, us);
        var nco = us.Where(u => !u.IsOfficer).OrderBy(u => u.Id).Skip(3).First();
        nco.GrantKills(defs.CombatRules.RankKills[defs.CombatRules.NcoRank - 1]);
        check(nco.IsNco && w.ActingNco(c) == nco && w.TacticAllowed(c, CompanyTactic.FlankLeft), "장교 지휘 중: 모든 틀 가능");
        us[0].Hp = 0;
        w.Step();
        int ho = (int)MathF.Round(R.NcoHandoverSec / Config.SimDt);
        check(c.Commander == null && w.InHandover(c) && w.CommandLevelOf(c) == CommandLevel.None && c.HandoverUntil == w.Tick - 1 + ho && w.CommanderOf(c) == nco,
            $"마지막 장교 전사 → 부사관 대리 인계 {R.NcoHandoverSec}초 동안 지휘 없음");
        while (w.InHandover(c)) w.Step();
        check(w.CommandLevelOf(c) == CommandLevel.Nco && w.TacticAllowed(c, CompanyTactic.FireManoeuvre) && w.TacticAllowed(c, CompanyTactic.None)
              && !w.TacticAllowed(c, CompanyTactic.FlankLeft) && !w.TacticAllowed(c, CompanyTactic.Assault) && !w.TacticAllowed(c, CompanyTactic.DefenceDepth),
            "부사관 대리: 틀 없음 · 화력-기동만 (측면 · 돌격 · 종심 방어 불가)");
        nco.Hp = 0;
        w.Step();
        int savedMin = R.ActingMinRank;
        try
        {
            R.ActingMinRank = -1;   // T24 ① 끔 = 옛 규칙 (분대장 부사관만)
            check(w.CommandLevelOf(c) == CommandLevel.None && w.CommanderOf(c) == null && w.TacticAllowed(c, CompanyTactic.None) && !w.TacticAllowed(c, CompanyTactic.FireManoeuvre),
                "지휘자 없음 (actingMinRank −1): 기본 대형만 (틀 전환 없음)");
            R.ActingMinRank = 0;    // T24 ①: 분대장 아닌 최고 계급 병사 (같으면 Id 작은 쪽) 가 대리
            var best = c.Members.Where(u => u.Alive && !u.IsOfficer).OrderByDescending(u => u.Rank).ThenBy(u => u.Id).First();
            check(w.ActingNco(c) == best && w.CommandLevelOf(c) == CommandLevel.Nco && !w.TacticAllowed(c, CompanyTactic.FlankLeft),
                $"T24 ① 부사관 없음 (actingMinRank 0) → 최고 계급 병사 대리 (Id {best.Id}, 계급 {best.Rank})");
        }
        finally { R.ActingMinRank = savedMin; }
        // 인계 중에는 장교 판단 (빈자리 점검 · 행진 사격) 없음: 다른 중대 장교가 가까이 있어도
        var w2 = Bare();
        var a = Group(w2, 1, 10, new Vector2(40, 40));
        var ca = Form(w2, a);
        var helper = w2.SpawnUnit(defs.Units["officer"], 1, new Vector2(40, 43));
        a[0].Hp = 0;
        w2.Step();
        check(w2.InHandover(ca) && ca.Commander == null, "장교 없는 중대 인계 중 (근처 다른 장교는 인계 뒤에만 도움)");
        _ = helper;
    }

    // ---------------------------------------------------------------- 경험치

    static void TestXp()
    {
        var w = Bare(160);
        var g1 = Group(w, 2, 12, new Vector2(40, 40));
        var g2 = Group(w, 1, 12, new Vector2(70, 40));
        w.FormCompany(1, g1, 1); w.FormCompany(1, g2, 1);
        w.Step();
        var c1 = g1[0].Company!; var c2 = g2[0].Company!;
        var b = c1.Battalion!;
        var bat = b.Commander!;
        // 교전 시간: 구성원이 쏘는 동안 1초마다 +xpCombatPerSec
        var shooter = g2.First(u => !u.IsOfficer);
        int x0 = g2[0].OfficerXp;
        for (int i = 0; i < 600; i++) { shooter.LastShotTick = w.Tick; w.Step(); }
        int gain = g2[0].OfficerXp - x0;
        check(gain >= 9 * R.XpCombatPerSec && gain <= 11 * R.XpCombatPerSec && g1.Where(u => u.IsOfficer).All(u => u.OfficerXp == 0),
            $"교전 10초 → 그 중대 장교 경험치 +{gain} (초당 {R.XpCombatPerSec}), 교전 없는 중대는 0");
        Steps(w, (int)MathF.Ceiling(R.CombatWindowSec / Config.SimDt) + 1);
        int y0 = g2[0].OfficerXp;
        Steps(w, 600);
        check(g2[0].OfficerXp == y0, "교전이 끝나면 (3초 뒤) 경험치 증가 없음");
        // 사살: 사격자 중대 장교 + 대대장 (다른 중대면)
        int k0 = g2[0].OfficerXp, kb = bat.OfficerXp;
        w.CreditKillXp(shooter);
        check(g2[0].OfficerXp == k0 + R.XpPerKill && (bat.Company == c2 || bat.OfficerXp == kb + R.XpPerKillBattalion), $"중대원 사살 → 중대 장교 +{R.XpPerKill}, 대대장 +{R.XpPerKillBattalion}");
        // 실제 사살 (피해 → CountKill)
        var enemy = w.SpawnUnit(defs.Units["infantry"], 2, shooter.Pos + new Vector2(3, 0));
        enemy.Hp = 1;
        int r0 = g2[0].OfficerXp;
        for (int i = 0; i < 600 && enemy.Alive; i++) w.Step();
        check(!enemy.Alive && g2[0].OfficerXp >= r0 + R.XpPerKill, $"실제 교전 사살로 경험치 (전 {r0} → 후 {g2[0].OfficerXp})");
        // 상한
        g2[0].OfficerXp = R.XpMax - 1; w.CreditKillXp(shooter);
        check(g2[0].OfficerXp == R.XpMax, "경험치 상한 xpMax");
        // 사병은 경험치 없음
        check(shooter.OfficerXp == 0 && w.XpLevel(shooter) == 0, "사병은 경험치 없음 (사살 수만)");
    }

    static void TestXpEffects()
    {
        var w = Bare();
        var us = Group(w, 1, 12, new Vector2(40, 40));
        var c = Form(w, us);
        var o = us[0];
        var lv = R.XpLevels;
        o.OfficerXp = lv[0] - 1;
        int l0 = w.XpLevel(o);
        o.OfficerXp = lv[0];
        int l1 = w.XpLevel(o);
        o.OfficerXp = lv[^1];
        int lmax = w.XpLevel(o);
        check(l0 == 0 && l1 == 1 && lmax == lv.Length, $"경험치 단계: 문턱 {string.Join("/", lv)} → 0 · 1 · {lmax}");
        o.OfficerXp = 0;
        int iv0 = w.DecideInterval(c, 30);
        float d0 = w.ChainDelayMul(us[3]);
        o.OfficerXp = lv[^1];
        int iv1 = w.DecideInterval(c, 30);
        float d1 = w.ChainDelayMul(us[3]);
        check(iv0 == 30 && iv1 == (int)MathF.Round(30 * (1 - R.XpDecidePerLevel * lv.Length)) && iv1 < iv0, $"경험치 → 판단 간격 {iv0} → {iv1}틱 (행진 사격 · 빈자리 점검)");
        check(d0 == 1f && MathF.Abs(d1 - (1 - R.XpDelayPerLevel * lv.Length)) < 1e-5f, $"경험치 → 지휘 계통 지연 ×{d1:0.00}");
        // 지휘 지연 실측: 경험치 많은 지휘관 중대가 더 빨리 출발 (지휘 범위 밖 병사)
        int Delay(int xp)
        {
            var ww = Bare();
            var g = Group(ww, 1, 12, new Vector2(40, 40));
            var cc = Form(ww, g);
            g[0].OfficerXp = xp;
            g[0].Pos = new Vector2(10, 90);   // 지휘 범위 밖
            ww.Step();
            var men = g.Where(u => !u.IsOfficer).ToList();
            ww.Move(1, men, new Vector2(60, 40));
            ww.Step();
            return men.Sum(u => u.ReactDelay);
        }
        int slow = Delay(0), fast = Delay(lv[^1]);
        check(fast < slow, $"실측 지휘 지연 합: 경험치 0 = {slow}틱, 최고 단계 = {fast}틱");
        // 인계 중 · 부사관은 경험치 효과 없음
        o.Hp = 0; w.Step();
        check(w.DecideInterval(c, 30) == 30 && w.ChainDelayMul(us[3]) == 1f, "장교 지휘가 아니면 경험치 효과 없음");
    }

    // ---------------------------------------------------------------- 저장 · 미션 결과

    static void TestSnapshot()
    {
        var w = Bare(160);
        var g1 = Group(w, 2, 12, new Vector2(40, 40));
        var g2 = Group(w, 1, 12, new Vector2(70, 40));
        w.FormCompany(1, g1, 1); w.FormCompany(1, g2, 1);
        w.Step();
        g1[1].OfficerXp = 77;
        g1[0].Hp = 0;   // 대대장 겸 → 승계 · 인계 중 저장
        w.Step();
        var c1 = g1[1].Company!;
        var b = c1.Battalion!;
        bool state = w.InHandover(b) && b.Commander != null && b.Commander.TempRank > 0;
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var yb = y.Battalions.Single(); var yc = y.Companies.First(k => k.Id == c1.Id);
        bool fields = yb.Commander?.Id == b.Commander!.Id && yb.HandoverUntil == b.HandoverUntil && yc.Commander?.Id == c1.Commander?.Id && yc.HandoverUntil == c1.HandoverUntil
                      && y.Units.Where(u => u.IsOfficer).All(u => w.Find(u.Id) is Unit o && o.OfficerBase == u.OfficerBase && o.TempRank == u.TempRank && o.OfficerXp == u.OfficerXp);
        check(state && same && fields && w.ComputeChecksum() == y.ComputeChecksum(), "지휘관 · 인계 · 계급 · 경험치 스냅샷: 저장 → 불러오기 → 저장 바이트 동일, 체크섬 동일");
        Steps(w, 600); Steps(y, 600);
        check(w.ComputeChecksum() == y.ComputeChecksum() && y.CommandLevelOf(yb) == CommandLevel.Officer, "불러온 월드를 600틱 (인계 끝 포함) 이어 돌려도 같은 체크섬");
        // 체크섬이 지휘 상태를 본다
        ulong h0 = w.ComputeChecksum();
        g1[1].OfficerXp++;
        ulong h1 = w.ComputeChecksum();
        check(h0 != h1, "체크섬에 장교 경험치 포함");
    }

    const string MissionJson = """
    { "id": "t_command", "title": "T", "player": 1, "seed": 5,
      "factions": [ { "name": "A", "team": 1, "posture": "attack" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "t", "width": 64, "height": 64, "base": "grass", "starts": [ [10, 32], [50, 32] ] },
      "forces": [
        { "id": "p1", "faction": 1, "at": [10, 28], "facing": 90, "units": { "officer": 2, "infantry": 10 }, "officerRanks": [2, 1], "officerXp": 150 },
        { "id": "p2", "faction": 1, "at": [10, 40], "facing": 90, "companies": [ { "units": { "officer": 1, "infantry": 8 } }, { "units": { "officer": 1, "infantry": 8 } }, { "units": { "officer": 1, "infantry": 8 } } ] },
        { "id": "boss", "faction": 2, "at": [58, 32], "units": { "officer": 1 }, "tag": "boss", "company": false }
      ],
      "objectives": [ { "id": "o", "type": "eliminate", "tag": "boss" }, { "id": "o2", "type": "defend", "seconds": 999 } ] }
    """;

    static void TestMissionResults()
    {
        var d = MissionDef.Parse(MissionJson, "t_command");
        var w = World.CreateMissionWorld(d, defs);
        w.Fog.Enabled = false;
        var p1 = w.Mission!.ForceUnits["p1"].Select(i => w.Find(i)).OfType<Unit>().Where(u => u.IsOfficer).OrderBy(u => u.Id).ToList();
        var p2 = w.Mission!.ForceUnits["p2"].Select(i => w.Find(i)).OfType<Unit>().Where(u => u.IsOfficer).ToList();
        var b2 = p2[0].Company?.Battalion;
        check(p1.Count == 2 && p1[0].OfficerRank == 2 && p1[1].OfficerRank == 1 && p1.All(u => u.OfficerXp == 150) && p1[0].Company?.Commander == p1[0],
            $"미션 지정: 장교 계급 (중위 · 소위) · 시작 경험치 150, 지휘관 = 중위 (장교 {p1.Count}, 계급 {string.Join(",", p1.Select(u => u.OfficerRank))}, 경험치 {string.Join(",", p1.Select(u => u.OfficerXp))}, 지휘관 {p1.FirstOrDefault()?.Company?.Commander?.Id})");
        check(b2 != null && b2.Commander is { } bc && bc.OfficerBase == R.BattalionSeniorRank && bc.TempRank == 0 && p2.Count(u => u.OfficerRank == R.CompanyRank) == 1 && p2.Count(u => u.OfficerRank == R.BattalionRank) == 1,
            "미션 자동: 중대 3개 대대 → 대대장 중령 (정식), 부대대장 소령, 나머지 대위");
        foreach (var u in w.Mission.ForceUnits["boss"].Select(i => w.Find(i)).OfType<Unit>()) u.Hp = 0;
        for (int i = 0; i < 30; i++) w.Step();
        check(w.Mission.ObjState[0] == MissionState.Done && p1[0].OfficerXp < 150 + 60, $"목표 달성 경험치 없음 ({p1[0].OfficerXp})");
        var res = MissionResults.From(w);
        var line = res.Survivors.FirstOrDefault(s => s.Id == p1[0].Id);
        var bl = res.Survivors.FirstOrDefault(s => s.Id == b2!.Commander!.Id);
        var enl = res.Survivors.FirstOrDefault(s => s.Unit == "infantry");
        check(line != null && line.OfficerRank == 2 && line.Xp == p1[0].OfficerXp && line.Post == "company" && bl?.Post == "battalion" && enl != null && enl.Xp == null && enl.OfficerRank == null,
            "미션 결과: 장교 정식 계급 · 임시 진급 · 경험치 · 직책 기록 (사병은 없음)");
        string json = System.Text.Json.JsonSerializer.Serialize(res, CommandJson.Options);
        check(json.Contains("\"xp\"", StringComparison.OrdinalIgnoreCase) && json.Contains("\"tempRank\"", StringComparison.OrdinalIgnoreCase), "결과 json 에 xp · tempRank");
        // 임시 진급한 장교는 진급 후보
        p1[0].Hp = 0;
        for (int i = 0; i < 2; i++) w.Step();
        var res2 = MissionResults.From(w);
        check(res2.Promotions.Any(s => s.Id == p1[1].Id && s.TempRank > 0), "전투 중 임시 진급 장교 → 결과 진급 후보 (Promotions)");
    }
}
