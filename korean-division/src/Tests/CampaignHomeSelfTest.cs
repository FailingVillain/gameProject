using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T19c 캠페인 홈: 연대 배속 (Q9, 연대 1~9 · 0 = 직할) 규칙 · 저장 왕복 · 옛 파일 기본값 · 범위 밖 값 정리, 투입 비용 공용 함수.
/// SaveCompatSelfTest 에서 부른다 (KD_SELFTEST_ONLY=savecompat). 디스크를 쓰지 않는다 (ToJson / FromJson).
/// </summary>
public static class CampaignHomeSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        // 옛 파일 (Regiment 없음) → 모두 직할 (0)
        var old = CampaignState.FromJson("{\"Version\":3,\"Roster\":[{\"Id\":\"b1_c1\",\"BattalionNumber\":1},{\"Id\":\"mg\",\"BattalionNumber\":0}]}");
        check(old != null && old.Roster.Count == 2 && old.Roster.All(c => c.Regiment == 0) && old.Version == CampaignState.SchemaVersion,
            "캠페인 홈: 옛 저장 (연대 필드 없음) 은 모두 직할 (0) 로 열린다, 스키마 번호 그대로");

        // 대대 단위 배속 · 직할 중대 하나씩 · 범위 밖 거부
        var s = new CampaignState();
        s.Roster.Add(new CampaignState.RosterCompany { Id = "b2_c1", BattalionNumber = 2, Name = "제1중대" });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "b2_c2", BattalionNumber = 2, Name = "제2중대" });
        s.Roster.Add(new CampaignState.RosterCompany { Id = "cav", BattalionNumber = 0, Kind = "cavalry", Name = "기병중대" });
        int bn = CampaignRegiments.Attach(s, "b1_c2", 3);             // 1대대 3개 중대 → 3연대
        int bn2 = CampaignRegiments.Attach(s, "b2_c1", 9);            // 2대대 2개 중대 → 9연대
        int direct = CampaignRegiments.Attach(s, "mg", 1);            // 직할 기관총중대 하나만
        bool rejected = CampaignRegiments.Attach(s, "mg", 10) == 0 && CampaignRegiments.Attach(s, "mg", -1) == 0 && CampaignRegiments.Attach(s, "nope", 2) == 0;
        bool battalionWide = s.Roster.Where(c => c.BattalionNumber == 1).All(c => c.Regiment == 3) && s.Roster.Where(c => c.BattalionNumber == 2).All(c => c.Regiment == 9);
        bool directOnly = s.Roster.First(c => c.Id == "mg").Regiment == 1 && s.Roster.First(c => c.Id == "cav").Regiment == 0;
        bool step = CampaignRegiments.Step(0, -1) == 9 && CampaignRegiments.Step(9, 1) == 0 && CampaignRegiments.Step(4, 1) == 5;
        check(bn == 3 && bn2 == 2 && direct == 1 && rejected && battalionWide && directOnly && step,
            $"캠페인 홈: 연대 배속 대대 단위 {bn}/{bn2}, 직할 하나씩 {direct}, 범위 밖 · 모르는 id 거부 {rejected}, 0↔9 순환 {step}");

        // 연대 1~9 저장 왕복 (각 연대에 하나씩) + 범위 밖 값은 직할로 정리
        var r = new CampaignState();
        r.Roster.Clear();
        for (int i = 1; i <= CampaignRegiments.Max; i++)
            r.Roster.Add(new CampaignState.RosterCompany { Id = $"x{i}", BattalionNumber = 10 + i, Regiment = i });
        var back = CampaignState.FromJson(r.ToJson());
        int rt = back == null ? 0 : Enumerable.Range(1, CampaignRegiments.Max).Count(i => back.Roster.FirstOrDefault(c => c.Id == $"x{i}")?.Regiment == i);
        r.Roster[0].Regiment = -3; r.Roster[8].Regiment = 12;   // 손상 · 수작업 편집된 값
        var bad = CampaignState.FromJson(r.ToJson());
        bool sanitized = bad != null && bad.Roster.First(c => c.Id == "x9").Regiment == 0 && bad.Roster.First(c => c.Id == "x1").Regiment == 0 && bad.Roster.First(c => c.Id == "x5").Regiment == 5;
        var counts = CampaignRegiments.CompanyCounts(back!);
        check(rt == CampaignRegiments.Max && sanitized && counts.Skip(1).All(n => n == 1) && counts[0] == 0,
            $"캠페인 홈: 연대 1~9 저장 왕복 {rt}/9, 범위 밖 (12 · -3) → 직할 {sanitized}");

        // 투입 비용 공용 함수 (BeginOperation · 병참 미리보기) = 대규모 투입이면 규모 배수
        var e = CampaignEconomyDefs.Current;
        var big = new List<CampaignState.RosterCompany> { new() { Kind = "infantry", Strength = e.LargeDeployThreshold + 1 } };
        var small = new List<CampaignState.RosterCompany> { new() { Kind = "infantry", Strength = 10 } };
        bool cost = Math.Abs(CampaignEconomy.DeployCost(big, 500) - (e.LargeDeployThreshold + 1) * e.LargeDeployScaleMul) < 0.01f
            && Math.Abs(CampaignEconomy.DeployCost(small, 500) - 10f) < 0.01f && Math.Abs(CampaignEconomy.DeployCost(big, 0) - (e.LargeDeployThreshold + 1)) < 0.01f;
        check(cost, "캠페인 홈: 투입 비용 = 병력 × 병종 단가 × (대규모면 규모 배수), 미리보기 · 작전 개시 같은 함수");

        // 병참 미리보기 (CampaignMenu.UpdatePreview: 사본 · ShellBaseLoad · BeginOperation) = 실제 출격 BeginOperation 과 구경별 탄약 · 포탄 수치 전부 같다
        {
            const string mission = CampaignState.YaluCrossing;
            var pre = new CampaignState();
            pre.Roster.First(c => c.Id == "b1_c2").Caliber = StockKeys.Jp;
            pre.Roster.First(c => c.Id == "b1_c3").Caliber = StockKeys.De;
            pre.Stock.Ammo[StockKeys.Jp] = 2000;          // 부족 → 구경별 부분 적재
            pre.Stock.Ammo[StockKeys.De] = 1000;
            pre.Stock.Shells[StockKeys.Howitzer] = 1;     // 포탄 부족 → 종류별 부분 적재
            pre.Stock.Shells[StockKeys.InfGun] = 500;
            var ids = pre.Roster.Where(c => c.Available).Select(c => c.Id).ToList();
            const int maxD = 3;
            var prev = CampaignState.FromJson(pre.ToJson())!;
            CampaignEconomy.BeginOperation(prev, mission, ids, maxD, CampaignEconomy.ShellBaseLoad(mission));   // 미리보기 경로
            var real = CampaignState.FromJson(pre.ToJson())!;
            CampaignEconomy.BeginOperation(real, mission, ids, maxD);                                             // 출격 경로 (기본 shellBase)
            var pp = prev.Pending!; var rp = real.Pending!;
            bool sameAmmo = pp.AmmoLoaded == rp.AmmoLoaded && MathF.Abs(pp.AmmoScale - rp.AmmoScale) < 1e-6f && pp.AmmoByCaliber.Count == rp.AmmoByCaliber.Count
                && pp.AmmoByCaliber.All(kv => rp.AmmoByCaliber.GetValueOrDefault(kv.Key, -1) == kv.Value);
            bool sameShell = pp.ShellsLoaded.Count == rp.ShellsLoaded.Count && pp.ShellsLoaded.All(kv => rp.ShellsLoaded.GetValueOrDefault(kv.Key, -1) == kv.Value)
                && pp.ShellScale.Count == rp.ShellScale.Count && pp.ShellScale.All(kv => MathF.Abs(rp.ShellScale.GetValueOrDefault(kv.Key, -9f) - kv.Value) < 1e-6f);
            bool nonTrivial = pp.AmmoByCaliber.Count >= 2 && pp.ShellsLoaded.ContainsKey(StockKeys.Howitzer) && pp.ShellScale[StockKeys.Howitzer] < 1f;
            check(sameAmmo && sameShell && nonTrivial,
                $"캠페인 홈: 병참 미리보기 = 실제 출격 적재 (구경별 탄약 {pp.AmmoByCaliber.Count}종 · 포탄 {pp.ShellsLoaded.Count}종, 곡사포 {pp.ShellsLoaded.GetValueOrDefault(StockKeys.Howitzer)}/{rp.ShellsLoaded.GetValueOrDefault(StockKeys.Howitzer)}) 탄약 {sameAmmo} · 포탄 {sameShell}");
        }

        // KD_CAMPAIGN_VIEW 파싱 (CampaignMenu.ApplyViewOverride): 생성자 · 디스크 없이 빈 인스턴스에 필드만 세팅, 환경 변수는 finally 로 복원
        {
            string? prevEnv = Environment.GetEnvironmentVariable("KD_CAMPAIGN_VIEW");
            try
            {
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var menu = (UI.CampaignMenu)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UI.CampaignMenu));
                var tabT = typeof(UI.CampaignMenu).GetNestedType("Tab", System.Reflection.BindingFlags.NonPublic)!;
                var scrT = typeof(UI.CampaignMenu).GetNestedType("Screen", System.Reflection.BindingFlags.NonPublic)!;
                var fScr = typeof(UI.CampaignMenu).GetField("currentScreen", F)!;
                var fTab = typeof(UI.CampaignMenu).GetField("currentTab", F)!;
                var fLast = typeof(UI.CampaignMenu).GetField("lastTabOf", F)!;
                var mApply = typeof(UI.CampaignMenu).GetMethod("ApplyViewOverride", F)!;
                // 화면마다 마지막 탭: 좌 = 11 (정보), 중앙 = 1 (요약), 우 = 7 (외교)
                var last = Array.CreateInstance(tabT, 3);
                last.SetValue(Enum.ToObject(tabT, 11), 0); last.SetValue(Enum.ToObject(tabT, 1), 1); last.SetValue(Enum.ToObject(tabT, 7), 2);
                fLast.SetValue(menu, last);
                string Apply(string? env)
                {
                    // 매번 우 · 7 에서 출발해 "바뀌지 않음"과 "기본값" 을 구분한다 (화면별 마지막 탭도 매번 초기화: SelectTab 이 갱신하므로)
                    fScr.SetValue(menu, Enum.Parse(scrT, "Right")); fTab.SetValue(menu, Enum.ToObject(tabT, 7));
                    var fresh = Array.CreateInstance(tabT, 3);
                    fresh.SetValue(Enum.ToObject(tabT, 11), 0); fresh.SetValue(Enum.ToObject(tabT, 1), 1); fresh.SetValue(Enum.ToObject(tabT, 7), 2);
                    fLast.SetValue(menu, fresh);
                    Environment.SetEnvironmentVariable("KD_CAMPAIGN_VIEW", env);
                    mApply.Invoke(menu, null);
                    return $"{fScr.GetValue(menu)}:{Convert.ToInt32(fTab.GetValue(menu))}";
                }
                var cases = new (string? Env, string Want)[]
                {
                    (null, "Right:7"), ("", "Right:7"), ("   ", "Right:7"), ("bogus", "Right:7"),
                    ("center:4", "Center:4"), (" CENTER:4 ", "Center:4"), ("right", "Right:7"), ("left", "Left:11"),
                    ("right:9", "Right:9"),
                    ("center:99", "Center:1"), ("center:abc", "Center:1"),       // 탭 번호 범위 밖 · 숫자 아님 → 화면만 적용, 탭은 그 화면 마지막 탭
                    ("left:1", "Center:1"),                                       // 탭 1 (요약) 은 중앙에 있다 → 탭이 화면을 이긴다 (as-is)
                };
                var got = cases.Select(cs => (cs.Env, cs.Want, Got: Apply(cs.Env))).ToList();
                bool all = got.All(g => g.Got == g.Want);
                string fails = string.Join(" ", got.Where(g => g.Got != g.Want).Select(g => $"[{g.Env ?? "null"}→{g.Got}≠{g.Want}]"));
                check(all, $"캠페인 홈: KD_CAMPAIGN_VIEW 파싱 (center:4 · right · left:1 · 범위 밖 · 빈 값 · 잘못된 값 → 기본 유지) {(all ? "전부 일치" : "불일치 " + fails)}");
            }
            finally { Environment.SetEnvironmentVariable("KD_CAMPAIGN_VIEW", prevEnv); }
        }

        // 미투입 포대: ShellBaseLoad 는 미션 파일의 플레이어 포 전부를 센다 (투입 중대 id 를 받지 않는다) → 곡사포 중대를 빼도 포탄 휴대량 · 적재 불변 (as-is)
        {
            const string mission = CampaignState.YaluCrossing;
            int howAmmo = Defs.Current.Weapons["howitzer_heavy"].Ammo;
            int baseHow = CampaignEconomy.ShellBaseLoad(mission).GetValueOrDefault(StockKeys.Howitzer);
            var a = new CampaignState();
            a.Stock.Shells[StockKeys.Howitzer] = 1_000_000;   // 재고 충분 → 적재 = 기본 휴대량 그대로
            CampaignEconomy.BeginOperation(a, mission, new[] { "b1_c1" });                // 보병 중대만 투입 (포대 미투입)
            var b = new CampaignState();
            b.Stock.Shells[StockKeys.Howitzer] = 1_000_000;
            CampaignEconomy.BeginOperation(b, mission, b.Roster.Select(c => c.Id).ToList());   // 전원 투입
            int loadA = a.Pending!.ShellsLoaded.GetValueOrDefault(StockKeys.Howitzer), loadB = b.Pending!.ShellsLoaded.GetValueOrDefault(StockKeys.Howitzer);
            check(baseHow == 4 * howAmmo && loadA == baseHow && loadB == baseHow && a.Stock.Shells[StockKeys.Howitzer] == 1_000_000,
                $"캠페인 홈: 미투입 포대 포탄은 ShellBaseLoad 에 그대로 계산 (1-1 곡사포 {baseHow} = 4 × {howAmmo}, 보병만 투입 {loadA} · 전원 {loadB})");
        }
    }
}
