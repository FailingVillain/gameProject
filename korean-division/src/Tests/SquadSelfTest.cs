using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 분대 · 부사관 · 계급 · 장교 빈자리 점검 · 지휘 계통 지연 · 부분 선택 검증 (ROADMAP §2-4c · §2-7, 2026-10-19 확정 ④). 창 없이.
/// KD_SELFTEST_ONLY=squads 로 단독 실행.
/// </summary>
public static class SquadSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("squads", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[분대 · 부사관 · 계급 · 빈자리 점검 · 지휘 지연]");
        try
        {
            TestBuild();
            TestMergeAndReform();
            TestNcoLeader();
            TestNcoDecay();
            TestRankNames();
            TestKillsCap();
            TestPartialSelection();
            TestGapCheck();
            TestSwordWave();
            TestCommandDelayTiers();
            TestCommandDelayDeterminism();
            TestSnapshot();
        }
        catch (Exception e) { check(false, $"분대 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
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
        for (int f = 1; f <= 2; f++)
        {
            var b = w.Buildings.First(x => x.Owner == f);
            var k = w.SpawnUnit(defs.Units["supply_wagon"], f, b.Pos + new Vector2(f == 1 ? 3 : -3, 0));
            w.Hold(f, new[] { k });
        }
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

    static Company Form(World w, List<Unit> us)
    {
        w.FormCompany(us[0].Owner, us);
        w.Step();
        return us[0].Company!;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    static bool EveryoneInOneSquad(Company c) =>
        c.Members.Where(u => !u.IsOfficer).All(u => u.Squad != null && u.Squad.Company == c && c.Squads.Count(q => q.Members.Contains(u)) == 1)
        && c.Members.Where(u => u.IsOfficer).All(u => u.Squad == null)
        && c.Squads.All(q => q.Members.Count is >= 1 and <= 12 && q.Members.SequenceEqual(q.Members.OrderBy(u => u.Id)));

    // ---------------------------------------------------------------- 편성

    static void TestBuild()
    {
        var w = Bare();
        var us = Grid(w, 30, new Vector2(60, 40), 15);
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 37)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(62, 37)));
        var c = Form(w, us);
        check(c.Squads.Count == 3 && c.Squads.All(q => q.Members.Count == 10) && EveryoneInOneSquad(c) && c.Squads.Select(q => q.Number).SequenceEqual(new[] { 1, 2, 3 }),
            $"중대 30명 + 장교 2 → 분대 3개 × 10명 (장교는 분대 밖, 번호 1~3): {string.Join("/", c.Squads.Select(q => q.Members.Count))}");
        // 공간 순: 넓게 퍼진 축 (가로 15열) 으로 잘라 이웃끼리 — 분대마다 x 구간이 겹치지 않는다
        var spans = c.Squads.Select(q => (q.Members.Min(u => u.Pos.X), q.Members.Max(u => u.Pos.X))).ToList();
        check(spans[0].Item2 <= spans[1].Item1 && spans[1].Item2 <= spans[2].Item1, "분대 = 이웃끼리 (가로로 잘림, 겹치지 않음)");
        var big = Grid(w, 100, new Vector2(60, 80), 25);
        var c2 = Form(w, big);
        check(c2.Squads.Count == 9 && c2.Squads.All(q => q.Members.Count is 11 or 12) && EveryoneInOneSquad(c2), $"100명 → 분대 9개 (11~12명): {string.Join("/", c2.Squads.Select(q => q.Members.Count))}");
        check(c2.Squads[0].Platoon == 1 && c2.Squads[8].Platoon == 3, "소대 = 표시용 (분대 9개 → 3개 소대)");
    }

    static void TestMergeAndReform()
    {
        var w = Bare();
        // 13명 → 분대 7 · 6. 6명 분대에서 2명 사망 → 4명 (< 6) → 7명 분대와 합쳐 11명
        var c = Form(w, Grid(w, 13, new Vector2(60, 40), 13));
        check(c.Squads.Count == 2, $"13명 → 분대 2개 ({string.Join("/", c.Squads.Select(q => q.Members.Count))})");
        var small = c.Squads.OrderBy(q => q.Members.Count).First();
        var keepId = c.Squads.First(q => q != small).Id;
        small.Members[0].Hp = 0; small.Members[1].Hp = 0;
        w.Step();
        check(c.Squads.Count == 1 && c.Squads[0].Id == keepId && c.Squads[0].Members.Count == 11 && EveryoneInOneSquad(c), $"줄어든 분대 (4명) → 다른 분대와 합치기 (11명, 분대 {c.Squads.Count}개)");

        // 25명 → 분대 9 · 8 · 8. 8명 분대에서 3명 사망 → 5명: 합치면 12 초과 → 다른 분대 빈자리 (3 + 4) 에 나눠 넣기
        var w2 = Bare();
        var c2 = Form(w2, Grid(w2, 25, new Vector2(60, 40), 25));
        var sq = c2.Squads.First(q => q.Members.Count == 8);
        for (int i = 0; i < 3; i++) sq.Members[i].Hp = 0;
        w2.Step();
        check(c2.Squads.Count == 2 && c2.Squads.Sum(q => q.Members.Count) == 22 && c2.Squads.All(q => q.Members.Count <= 12) && EveryoneInOneSquad(c2),
            $"합칠 곳이 없으면 빈자리에 나눠 넣기 (재편성): {string.Join("/", c2.Squads.Select(q => q.Members.Count))}");

        // 24명 → 12 · 12. 한 분대에서 7명 사망 → 5명: 합치기 · 나눠 넣기 모두 자리 없음 → 그대로
        var w3 = Bare();
        var c3 = Form(w3, Grid(w3, 24, new Vector2(60, 40), 24));
        var s3 = c3.Squads[0];
        for (int i = 0; i < 7; i++) s3.Members[i].Hp = 0;
        w3.Step();
        check(c3.Squads.Count == 2 && s3.Members.Count == 5 && EveryoneInOneSquad(c3), "자리가 없으면 줄어든 분대 그대로 (고정 편성)");
    }

    // ---------------------------------------------------------------- 부사관

    static void TestNcoLeader()
    {
        var w = Bare();
        var c = Form(w, Grid(w, 10, new Vector2(60, 40), 10));
        var q = c.Squads[0];
        check(q.Leader == q.Members[0] && !q.NcoLed, "부사관 없음: 가장 높은 계급 (같으면 Id 순) 이 임시 분대장, 효과 없음");
        q.Members[5].GrantKills(18);   // 병장
        check(q.Leader == q.Members[5] && !q.NcoLed && !w.NcoLeads(q.Members[0]), "병장 (계급 4) 분대장 = 효과 없음 (부사관 아님)");
        q.Members[7].GrantKills(30);   // 하사
        check(q.Leader == q.Members[7] && q.NcoLed && w.NcoLeads(q.Members[0]) && w.NcoLeads(q.Members[7]), "하사 (계급 5) = 공식 부사관 분대장 → 분대원 효과");
        q.Members[0].Pos = q.Members[7].Pos + new Vector2(defs.CombatRules.NcoRadius + 1, 0);
        check(!w.NcoLeads(q.Members[0]), $"분대장에서 {defs.CombatRules.NcoRadius}칸 밖이면 효과 없음");
    }

    static void TestNcoDecay()
    {
        // 제압 600 에서 1초 동안 감소량: 보통 80 · 부사관 분대 (×ncoDecayMul) · 장교 근처 240 (×3) · 장교 + 부사관 = 장교만 (중첩 없음)
        float Measure(bool nco, bool officer)
        {
            var w = Bare();
            var us = Grid(w, 6, new Vector2(60, 40), 6);
            var c = Form(w, us);
            if (nco) c.Squads[0].Members[5].GrantKills(30);
            if (officer) { var o = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 43)); w.Hold(1, new[] { o }); }
            w.Hold(1, us);
            w.Step();
            var u = c.Squads[0].Members[0];
            u.Suppression = 600;
            Steps(w, 60);
            return 600 - u.Suppression;
        }
        float plain = Measure(false, false), nco = Measure(true, false), off = Measure(false, true), both = Measure(true, true);
        var cr = defs.CombatRules;
        check(MathF.Abs(nco / plain - cr.NcoDecayMul) < 0.12f && MathF.Abs(off / plain - cr.OfficerDecayMul) < 0.2f && both == off,
            $"제압 감소 1초: 보통 {plain} · 부사관 분대 {nco} (×{cr.NcoDecayMul}) · 장교 {off} (×{cr.OfficerDecayMul}) · 둘 다 {both} (중첩 없음)");
    }

    // ---------------------------------------------------------------- 계급

    static void TestRankNames()
    {
        check(RankNames.Enlisted(null, 0) == "훈련병" && RankNames.Enlisted(null, 3) == "상등병" && RankNames.Enlisted(null, 5) == "참교" && RankNames.Enlisted(null, 8) == "특무정교"
              && RankNames.Officer(null, 0) == "장교" && RankNames.Officer(null, 1) == "참위" && RankNames.Officer(null, 6) == "정령",
            "계급 이름 (ranks.json default = 대한제국): 훈련병 … 특무정교 / 장교 · 참위 … 정령");
        check(RankNames.Enlisted("japanese", 5) == "오장" && RankNames.Enlisted("nowhere", 5) == "참교" && RankNames.Enlisted(null, 99) == "특무정교",
            "세력별 이름 (japanese 5 = 오장), 없는 세력 = default (한국군), 범위 밖 = 마지막");
        check(RankNames.AllText().Contains("특무정교") && RankNames.AllText().Contains("참장") && RankNames.AllText().Contains("부소대 지휘관 (초임)"), "계급 이름이 폰트 아틀라스 문자열에 포함");

        // 세력 6개 × 통합 색인 0..16 (문서 기준): 사용 여부 표
        var unusedExpected = new Dictionary<string, int[]>
        {
            ["korean"] = new[] { 4, 15 }, ["japanese"] = new[] { 4, 15, 16 }, ["fengtian"] = new[] { 4, 15, 16 },
            ["bandit"] = new[] { 0, 4, 8, 15, 16 }, ["russian-white"] = new[] { 4, 15, 16 }, ["russian-red"] = new[] { 0, 4, 8, 15, 16 },
        };
        bool all17 = true; string why = "";
        foreach (var (fac, unused) in unusedExpected)
        {
            for (int lv = 0; lv < RankNames.UnifiedLevels; lv++)
            {
                bool isNull = RankNames.Unified(fac, lv) == null;
                bool expectNull = unused.Contains(lv);
                if (isNull != expectNull) { all17 = false; why += $" {fac}#{lv}"; }
            }
        }
        check(all17, "6개 세력 표 모두 통합 색인 0..16 매핑 (미사용 = 한국 4 · 15, 일본/봉천/백군 4 · 15 · 16, 마적/적군 0 · 4 · 8 · 15 · 16)" + why);
        check(RankNames.Unified("fengtian", 11) == "상위" && RankNames.Unified("bandit", 14) == "대당가 (숙련)" && RankNames.Unified("russian-red", 14) == "연대 지휘관" && RankNames.Unified("russian-white", 11) == "주임대위",
            "문서 이름 샘플: 봉천 11 상위 · 마적 14 대당가 (숙련) · 적군 14 연대 지휘관 · 백군 11 주임대위");
        check(RankNames.Unified("russian", 5) == RankNames.Unified("russian-white", 5) && RankNames.Unified("soviet", 1) == "전사" && RankNames.Unified("zzz", 5) == "참교",
            "지도 키 별칭 (russian = russian-white, soviet = russian-red) · 모르는 세력 = 한국군 표 (fallback)");
        check(RankNames.General("korean", 0) == "참장" && RankNames.General("korean", 1) == "부장" && RankNames.General("korean", 2) == "대장" && RankNames.GeneralCount("korean") == 3
              && RankNames.General("japanese", 0) == null && RankNames.General(null, 2) == "대장", "장성 (통합 16, 표시 전용): 한국 참장 · 부장 · 대장, 다른 세력 없음, 세력 없음 = 한국");

        // 미사용 계급: 승진은 건너뛴다
        check(RankNames.Next("korean", 3) == 5 && RankNames.Next("japanese", 3) == 5 && RankNames.Next("korean", 8) == 8 && RankNames.Next("korean", 0) == 1,
            "승진 건너뜀: 한국 · 일본 3 → 5 (4 미사용), 최고 계급은 그대로");
        check(RankNames.Next("russian-red", 0) == 1 && RankNames.Next("bandit", 3) == 5 && RankNames.Next("bandit", 7) == 7 && RankNames.Next("russian-red", 7) == 7,
            "승진 건너뜀: 마적 · 적군 8 미사용 (7 이 상한), 3 → 5");
        check(RankNames.Snap("korean", 4) == 3 && RankNames.Snap("bandit", 8) == 7 && RankNames.Snap("bandit", 4) == 3 && RankNames.Snap("korean", 5) == 5 && RankNames.Snap("korean", 0) == 0
              && RankNames.Snap("bandit", 0) == 0 && RankNames.Snap("russian-red", 1) == 1,
            "미사용 계급 맞춤 (아래로): 한국 4 → 3 · 마적 8 → 7 · 4 → 3, 쓰이는 계급 · 0 (계급 없음) 은 그대로");
        check(RankNames.Enlisted("korean", 4) == "상등병" && RankNames.Enlisted("bandit", 0) == "척자" && RankNames.Enlisted("russian-red", 8) == "부소대 지휘관 (숙련)",
            "표시: 미사용 계급은 가까운 쓰이는 계급 이름으로 (한국 4 = 상등병 · 마적 0 = 척자 · 적군 8 = 숙련)");
        bool leaderOk = true;
        foreach (var fac in unusedExpected.Keys) leaderOk &= RankNames.EnlistedUsed(fac, Defs.Current.CombatRules.SquadLeaderRank) && RankNames.Snap(fac, Defs.Current.CombatRules.SquadLeaderRank) == Defs.Current.CombatRules.SquadLeaderRank;
        check(leaderOk && Defs.Current.CombatRules.NcoRank >= 5 && RankNames.EnlistedUsed("bandit", Defs.Current.CombatRules.NcoRank), "기본 분대장 계급 (squadLeaderRank 5) 은 모든 세력에서 쓰이는 계급");
        check(Defs.Current.Officers.MaxPromotionRank == 5 && RankNames.Officer("korean", Defs.Current.Officers.MaxPromotionRank) == "부령" && RankNames.Officer("japanese", 5) == "중좌",
            "진급 상한 중령 (maxPromotionRank 5 = 통합 13) 유지: 한국 부령 · 일본 중좌");
    }

    static void TestKillsCap()
    {
        var w = Bare();
        var sh = w.SpawnUnit(defs.Units["veteran"], 1, new Vector2(60, 40));
        w.Hold(1, new[] { sh });
        sh.GrantKills(defs.CombatRules.KillsCap);
        var d = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(62, 40));
        d.Weapon = null; d.Hp = 1; w.Hold(2, new[] { d });
        for (int t = 0; t < 3600 && d.Alive; t++) w.Step();
        check(!d.Alive && sh.Kills == defs.CombatRules.KillsCap && sh.Rank == 8, $"사살 수 상한 {defs.CombatRules.KillsCap} (사살 후 {sh.Kills}, 계급 {sh.Rank})");
    }

    // ---------------------------------------------------------------- 부분 선택 = 임시 묶음

    static void TestPartialSelection()
    {
        using var nd = TestDefs.NoCommandDelay();
        var w = Bare();
        var us = Grid(w, 20, new Vector2(60, 40), 10);
        var c = Form(w, us);
        var part = c.Squads[0].Members.ToList();
        w.Move(1, part, new Vector2(60, 70));
        w.Step();
        check(c.Anchor == null && part.All(u => u.Group != null && u.Company == c && u.Squad == c.Squads[0]) && us.Except(part).All(u => u.Group == null),
            "분대만 고른 이동 = 임시 묶음 (중대 깃발 없음), 소속 그대로");
        w.Move(1, us, new Vector2(60, 40));
        w.Step();
        check(c.Anchor != null && us.All(u => ReferenceEquals(u.Group, c.Anchor!.Group)), "중대 전원 이동 = 중대 깃발 대형");
    }

    // ---------------------------------------------------------------- 장교 빈자리 점검 · 칼 흔들기

    static (World W, Company C, List<Unit> Us) Stationed(bool officer, int seedShift = 0, int pad = 0)
    {
        var w = Bare();
        for (int i = 0; i < pad; i++) w.Hold(1, new[] { w.SpawnUnit(defs.Units["supply_wagon"], 1, new Vector2(10 + i, 110)) });   // 유닛 Id 를 밀어 해시 결과를 바꾼다
        var us = Grid(w, 20, new Vector2(60 + seedShift, 40), 10);
        if (officer) us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60 + seedShift, 38)));
        var c = Form(w, us);
        w.Move(1, us, new Vector2(60 + seedShift, 60));
        Steps(w, 2);
        for (int t = 0; t < 60 * 40 && !(c.Anchor == null && us.All(u => u.Order == UnitOrder.Idle)); t++) w.Step();
        return (w, c, us);
    }

    static void TestGapCheck()
    {
        using var nd = TestDefs.NoCommandDelay();
        foreach (bool officer in new[] { true, false })
        {
            var (w, c, us) = Stationed(officer);
            bool settled = c.Anchor == null && c.Facing != Vector2.Zero;
            // 앞줄 가운데 4명 사망 → 빈자리 4
            var front = us.Where(u => !u.IsOfficer).OrderByDescending(u => Vector2.Dot(u.Pos, c.Facing)).ThenBy(u => MathF.Abs(u.Pos.X - 60)).Take(4).ToList();
            foreach (var u in front) u.Hp = 0;
            w.Step();
            int holes = c.Holes;
            var before = c.Members.ToDictionary(u => u.Id, u => u.Pos);
            Steps(w, (int)(60 * defs.CombatRules.GapCheckSec * 2.1f));
            float moved = c.Members.Max(u => Vector2.Distance(u.Pos, before[u.Id]));
            if (officer)
                check(settled && holes == 4 && c.Holes == 0 && moved < defs.CombatRules.GapMoveMin + 0.6f && c.Members.All(u => u.Order == UnitOrder.Idle),
                    $"장교 있음: 빈자리 {holes} → {defs.CombatRules.GapCheckSec}초 점검으로 다시 짬 (N12: {defs.CombatRules.GapMoveMin}칸 안 병사는 그대로 — 최대 이동 {moved:0.0}칸, 남은 빈자리 {c.Holes})");
            else
                check(settled && holes == 4 && c.Holes == 4 && moved < 0.05f, $"장교 없음: 빈자리 그대로 ({c.Holes}, 이동 {moved:0.00}칸)");
        }
    }

    static void TestSwordWave()
    {
        using var nd = TestDefs.NoCommandDelay();
        int[] Run()
        {
            var res = new List<int>();
            for (int k = 0; k < 12; k++)
            {
                var (w, c, us) = Stationed(true, (k % 4) * 3, k);
                var off = us.First(u => u.IsOfficer);
                us.First(u => !u.IsOfficer).Hp = 0;
                w.Step();
                // 교전 중으로 표시 (최근 사격) 하며 점검 한 번 지나가기
                int waves = 0;
                for (int t = 0; t < (int)(60 * defs.CombatRules.GapCheckSec) + 120; t++)
                {
                    foreach (var u in c.Members) if (!u.IsOfficer) { u.LastShotTick = w.Tick; break; }
                    w.Step();
                    if (off.SwordWaveTick > 0) { waves = 1; break; }
                }
                res.Add(waves);
            }
            return res.ToArray();
        }
        var a = Run();
        var b = Run();
        int n = a.Sum();
        check(a.SequenceEqual(b) && n >= 1 && n <= 10, $"교전 중 빈자리 점검 → 장교 칼 흔들기 확률 33% (12회 중 {n}), 같은 입력 = 같은 결과");
    }

    // ---------------------------------------------------------------- 지휘 계통 지연

    static void TestCommandDelayTiers()
    {
        var w = Bare(160);
        // ① 장교 지휘 범위 안 (분대 소속)
        var a = Grid(w, 6, new Vector2(30, 30), 6);
        a.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(30, 28)));
        var ca = Form(w, a);
        // ② 부사관 분대 (장교 없음)
        var b = Grid(w, 6, new Vector2(80, 30), 6);
        var cb = Form(w, b);
        cb.Squads[0].Members[3].GrantKills(30);
        // ③ 임시 분대장만
        var cc = Form(w, Grid(w, 6, new Vector2(130, 30), 6));
        // ④ 분대 없음 (자유 유닛) · 차량
        var free = Grid(w, 6, new Vector2(30, 100), 6);
        var tank = w.SpawnUnit(defs.Units["tank_ft_female"], 1, new Vector2(80, 100));
        var soldiersA = a.Where(u => !u.IsOfficer).ToList();
        // T37 (U4 승인): 대리 지휘관 (T24 ① actingMinRank ≥ 0) 이 있는 중대는 분대에 부사관이 없어도 Nco 단계. 임시 분대장 (Acting) 단계는
        // 대리 지휘관이 없을 때 (actingMinRank −1 · 인계 중) 만 — 그 경우를 따로 확인한다.
        var offRules = defs.Officers;
        int savedActingMin = offRules.ActingMinRank;
        bool ccActingNco = w.ActingNco(cc) != null;
        bool ccTierWithActing = ccActingNco && cc.Members.All(u => w.ChainTierOf(u) == ChainTier.Nco);   // T37: 대리 지휘관 있음 → 반드시 Nco
        bool ccTierNoActing;
        try
        {
            offRules.ActingMinRank = -1;
            w.Step();   // 대리 캐시는 틱 단위
            ccTierNoActing = cc.Members.All(u => w.ChainTierOf(u) == ChainTier.Acting);
        }
        finally { offRules.ActingMinRank = savedActingMin; }
        w.Step();
        check(soldiersA.All(u => w.ChainTierOf(u) == ChainTier.Officer) && cb.Members.All(u => w.ChainTierOf(u) == ChainTier.Nco)
              && ccTierWithActing && ccTierNoActing && free.All(u => w.ChainTierOf(u) == ChainTier.Isolated) && w.ChainTierOf(tank) == ChainTier.None,
            $"지휘 계통 단계: 장교 범위 안 · 부사관 분대 · 임시 분대장 (대리 지휘관 {(ccActingNco ? "있음 → Nco" : "없음 — T37 위반")}, 대리 끔 → Acting) · 분대 없음 · 차량 (지연 없음)");
        var lone = cc.Members[0];
        foreach (var u in cc.Members.Skip(1)) u.Hp = 0;
        w.Step();
        check(w.ChainTierOf(lone) == ChainTier.Isolated, "혼자 남은 분대원 = 고립");

        // 공격 이동 명령 → 실행 틱의 ReactDelay (실행 뒤 UpdateUnit 이 1 줄임) = 출발 0~3틱 + 단계 지연
        var cd = defs.Command;
        float T(float sec) => sec / Config.SimDt;
        bool InRange(IEnumerable<Unit> us, float lo, float hi) => us.All(u => u.ReactDelay + 1 >= MathF.Floor(T(lo)) && u.ReactDelay + 1 <= MathF.Ceiling(T(hi)) + 3);
        var all = soldiersA.Concat(cb.Members).Concat(free).Append(tank).ToList();
        w.AttackMove(1, all, new Vector2(80, 70));
        w.Step();
        check(InRange(soldiersA, 0, 0) && InRange(cb.Members, cd.NcoMin, cd.NcoMax) && InRange(free, cd.IsolatedMin, cd.IsolatedMax) && tank.ReactDelay <= 3,
            $"공격 이동 지연 (틱): 장교 {string.Join(",", soldiersA.Select(u => u.ReactDelay))} · 부사관 {string.Join(",", cb.Members.Select(u => u.ReactDelay))} · 고립 {string.Join(",", free.Select(u => u.ReactDelay))} · 차량 {tank.ReactDelay}");
        check(free.Select(u => u.ReactDelay).Distinct().Count() > 1, "유닛마다 지연이 다름 (해시)");
        // 임시 분대장 단계: 새 분대 — 대리 지휘관 없음 (actingMinRank −1) 이면 Acting 지연, 있으면 (T37) Nco 지연
        try
        {
            offRules.ActingMinRank = -1;
            var ce = Form(w, Grid(w, 6, new Vector2(130, 120), 6));
            w.AttackMove(1, ce.Members, new Vector2(130, 140));
            w.Step();
            check(InRange(ce.Members, cd.ActingMin, cd.ActingMax), $"임시 분대장 분대 지연 (대리 지휘관 없음) {string.Join(",", ce.Members.Select(u => u.ReactDelay))} 틱 ({cd.ActingMin}~{cd.ActingMax}초)");
            offRules.ActingMinRank = 0;
            var cf = Form(w, Grid(w, 6, new Vector2(130, 150), 6));
            w.AttackMove(1, cf.Members, new Vector2(130, 158));
            w.Step();
            check(w.ActingNco(cf) != null && InRange(cf.Members, cd.NcoMin, cd.NcoMax),
                $"T37: 대리 지휘관 있는 분대 (부사관 없음) 지연 = 부사관 단계 {string.Join(",", cf.Members.Select(u => u.ReactDelay))} 틱 ({cd.NcoMin}~{cd.NcoMax}초)");
        }
        finally { offRules.ActingMinRank = savedActingMin; }
        // 후퇴 (이동 명령) = 절반
        Steps(w, 120);
        w.Move(1, free, new Vector2(30, 140));
        w.Step();
        check(InRange(free, cd.IsolatedMin * cd.RetreatMul, cd.IsolatedMax * cd.RetreatMul), $"이동 (후퇴) 명령 = 지연 ×{cd.RetreatMul}: {string.Join(",", free.Select(u => u.ReactDelay))}");
        // 돌격 명령도 지연 (전액)
        Steps(w, 120);
        w.Charge(1, cb.Members, new Vector2(80, 10));
        w.Step();
        check(InRange(cb.Members, cd.NcoMin, cd.NcoMax), $"돌격 명령 지연 (부사관 분대): {string.Join(",", cb.Members.Select(u => u.ReactDelay))}");
    }

    static void TestCommandDelayDeterminism()
    {
        int[] Run()
        {
            var w = Bare();
            var c = Form(w, Grid(w, 24, new Vector2(60, 40), 12));
            var free = Grid(w, 10, new Vector2(60, 80), 10);
            w.AttackMove(1, c.Members.Concat(free), new Vector2(60, 100));
            w.Step();
            return c.Members.Concat(free).Select(u => u.ReactDelay).ToArray();
        }
        var a = Run(); var b = Run();
        check(a.SequenceEqual(b), $"지휘 계통 지연 결정적 (같은 입력 = 같은 지연, {a.Length}기)");
    }

    // ---------------------------------------------------------------- 저장

    static void TestSnapshot()
    {
        var (x, c, us) = Stationed(true);
        foreach (var u in us.Where(u => !u.IsOfficer).Take(3)) u.Hp = 0;
        x.Step();
        c.Squads[0].Members[0].GrantKills(30);
        bool squads = c.Squads.Count > 0 && c.Holes > 0;
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var yc = y.Companies.First(k => k.Id == c.Id);
        check(squads && same && x.ComputeChecksum() == y.ComputeChecksum() && yc.Holes == c.Holes && yc.Squads.Select(q => q.Members.Count).SequenceEqual(c.Squads.Select(q => q.Members.Count)),
            $"분대 · 빈자리 스냅샷: 저장 → 불러오기 → 저장 바이트 동일, 체크섬 동일 (분대 {c.Squads.Count}, 빈자리 {c.Holes})");
        Steps(x, 900); Steps(y, 900);
        check(x.ComputeChecksum() == y.ComputeChecksum(), "불러온 월드를 900틱 (빈자리 점검 포함) 이어 돌려도 같은 체크섬");
    }
}
