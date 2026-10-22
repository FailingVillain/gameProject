using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>TC-D 자금 · 외인 슬롯 · 통제 한도 · 가용 보급: v10 이하 -> v11 이행 · 왕복 · 가격 · 한도 초과 편성 불가 · 예약/환원/영구 소모 · R5 시나리오 4개 OP1 가용 보급. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignFundsSelfTest
{
    static CampaignState.RosterCompany Co(string id, int n, bool foreign = false) => new() { Id = id, Name = id, Strength = n, MaxStrength = n, Kind = "infantry", Foreign = foreign };

    public static void Run(Action<bool, string> check)
    {
        var saved = CampaignEconomyDefs.Current;
        try
        {
            CampaignEconomyDefs.Override(null);   // 실제 JSON (수치는 파일에서)
            var d = CampaignEconomyDefs.Current;

            // 1) JSON 값 (사용자 결정)
            check(d.FundsStart == 10 && d.SilverPerFund == 500 && d.RegularControlCap == 3000 && d.ForeignSlotCap == 800 && d.EpisodeSupplyStart == 4000 && d.CivilianReliefSupplyLoss == 400
                && CampaignFunds.ToSilver(10) == 5000, "TC-D JSON: 자금 10 = 5,000냥 · 정규 3,000 · 외인 800 · 보급 4,000 · 대민 구제 400");

            // 2) 가격: 의용군 150 = 6 (50명당 2) · 마적 100명당 3 · 올림
            check(CampaignFunds.VolunteerPrice(150) == 6 && CampaignFunds.VolunteerPrice(100) == 4 && CampaignFunds.VolunteerPrice(0) == 0 && CampaignFunds.VolunteerPrice(51) == 4
                && CampaignFunds.BanditPrice(100) == 3 && CampaignFunds.BanditPrice(150) == 6 && CampaignFunds.BanditPrice(-5) == 0, "TC-D 가격: 의용군 150=6 · 마적 100=3 (올림)");

            // 3) 자금 사용: 모자라면 불변
            var s = new CampaignState();
            bool a = CampaignFunds.TrySpend(s, 6, out var e1), b = CampaignFunds.TrySpend(s, 6, out var e2), c = CampaignFunds.TrySpend(s, -1, out _);
            CampaignFunds.Gain(s, 3);
            check(s.Funds == 7 && a && !b && e2.Contains("모자랍니다") && e1 == "" && !c, $"TC-D 자금: 10 -6 = 4, 부족 거부(불변), +3 = 7 (자금 {s.Funds})");

            // 4) 저장: v10 이하 -> v11 (자금 = fundsStart, 소모 0, 외인 아님) · 왕복 · 깨진 값
            var old = CampaignState.FromJson("{\"Version\":8,\"Roster\":[{\"Id\":\"a\",\"Strength\":60}]}");
            check(old != null && old.Version == CampaignState.SchemaVersion && CampaignState.SchemaVersion == 11 && old.Funds == 10 && old.EpisodePermanentSupplyLoss == 0f && old.Roster.All(r => !r.Foreign),
                "TC-D 저장 이행: v8 -> v11 (자금 10 · 소모 0 · 외인 아님)");
            var rt = new CampaignState { Funds = 4, EpisodePermanentSupplyLoss = 623f };
            rt.Roster.Add(Co("v", 150, true));
            var back = CampaignState.FromJson(rt.ToJson());
            var bad = CampaignState.FromJson("{\"Funds\":-5,\"EpisodePermanentSupplyLoss\":-9}");
            check(back != null && back.Funds == 4 && back.EpisodePermanentSupplyLoss == 623f && back.Roster.Any(r => r.Id == "v" && r.Foreign) && bad != null && bad.Funds == 0 && bad.EpisodePermanentSupplyLoss == 0f,
                "TC-D 저장 왕복 + 음수 값 보정");

            // 5) 통제 한도: 정규 3,000 · 외인 800 초과 편성 불가 (BeginOperation 은 상태를 바꾸지 않고 false)
            var st = new CampaignState();
            st.Roster.Add(Co("r1", 2000)); st.Roster.Add(Co("r2", 1000)); st.Roster.Add(Co("r3", 1)); st.Roster.Add(Co("f1", 800, true)); st.Roster.Add(Co("f2", 1, true));
            var none = new Dictionary<string, int>();
            bool okEdge = CampaignEconomy.BeginOperation(st, "x", new[] { "r1", "r2", "f1" }, 0, none);   // 3000 + 800 = 한도 정확히
            float res1 = st.Pending?.EpisodeSupplyReserved ?? -1f;
            st.Pending = null;
            bool overReg = CampaignEconomy.BeginOperation(st, "x", new[] { "r1", "r2", "r3" }, 0, none);
            bool pendingAfterReg = st.Pending != null;
            bool overFrn = CampaignEconomy.BeginOperation(st, "x", new[] { "r1", "f1", "f2" }, 0, none);
            check(okEdge && res1 == 3800f && !overReg && !overFrn && !pendingAfterReg && st.Pending == null
                && CampaignFunds.CheckControl(st.Roster.Where(x => x.Id is "r1" or "r2" or "r3")) != null && CampaignFunds.CheckControl(st.Roster.Where(x => x.Id is "r1" or "f1")) == null
                && CampaignFunds.CheckControl(st.Roster.Where(x => x.Id is "r1" or "r2" or "r3"), 5000) == null,
                $"TC-D 통제 한도: 3000+800 통과 · 정규 3001 / 외인 801 편성 불가 (E2 한도 인자 5000 은 통과) (예약 {res1})");

            // 6) 예약 -> 정산: 가용 보급 = 4000 - 영구 소모 - 예약, 정산 때 예약 해제 + 10% + 전사 보충
            var ps = new CampaignState();
            ps.Roster.Add(Co("a", 100));
            CampaignEconomy.BeginOperation(ps, "m1", new[] { "a" }, 0, none);
            float availRes = CampaignFunds.AvailableSupply(ps);
            var mr = new MissionResults { MissionId = "m1", Title = "t", Result = "won" };
            mr.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 100, Alive = 80, Lost = 20 });
            CampaignEconomy.SettleBattle(ps, mr);
            // 손실 20 -> 부상 복귀 25% = 5, 전사 15; 영구 소모 = 100x10% + 15x1 = 25
            check(availRes == 3900f && Math.Abs(ps.EpisodePermanentSupplyLoss - 25f) < 0.01f && Math.Abs(CampaignFunds.AvailableSupply(ps) - 3975f) < 0.01f && ps.Pending == null,
                $"TC-D 보급: 예약 중 3900 -> 정산 후 3975 (영구 소모 {ps.EpisodePermanentSupplyLoss:0.#})");

            // 7) R5 시나리오 4개: OP1 가용 보급 (앞 미션 영구 소모 = BT1 155 · SK1 43 · SK2 25 · SK3 100 · SK4 43, D02 C = JSON 400)
            float Op1(params float[] losses) { var x = new CampaignState(); foreach (var l in losses) CampaignFunds.AddPermanentLoss(x, l); return CampaignFunds.AvailableSupply(x); }
            float c400 = d.CivilianReliefSupplyLoss;
            float s1 = Op1(155, 43, 43), s2 = Op1(c400, 155, 25, 43), s3 = Op1(c400, 155, 43, 25), s4 = Op1(155);
            check(Math.Abs(s1 - 3760) <= 1 && Math.Abs(s2 - 3380) <= 3 && Math.Abs(s3 - 3380) <= 3 && s4 == 3845,
                $"TC-D R5 시나리오 OP1 가용 보급: S1 {s1:0} (~3760) · S2 {s2:0} (~3380) · S3 {s3:0} (~3380) · S4 {s4:0} (3845)");
            check(Math.Abs(CampaignFunds.PermanentLoss(1000f, 20) - 120f) < 0.01f && CampaignFunds.PermanentLoss(-5f, -3) == 0f, "TC-D 영구 소모 식: 1000x10% + 20x1 = 120, 음수 0");

            // 7b) [C1E1-d2] campaign_e1_start.json op1SupplyCheck 를 읽어 가용 보급 재현 (JSON 키 기반, 하드코딩 아님)
            check(d.SupplyCostInfantry == 1 && d.SupplyCostCavalry == 3 && d.SupplyCostForeign == 1, "C1E1-d2 supplyCost* JSON 매핑: 보병 1 · 기병 3 · 외인 1");
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "assets", "data", "campaign_e1_start.json");
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                var chk = doc.RootElement.GetProperty("op1SupplyCheck");
                var per = chk.GetProperty("_estimateMissionPermanentUse");
                float M(string k) => per.GetProperty(k).GetSingle();
                // 허용오차 5: S2·S3 는 4000-623=3377 이 정확하나 명세 기록은 반올림 3380 (S1 도 명세 240 vs 합 241).
                const float tol = 5f;
                // 경로별 구성: D02 C(대민 구제) 여부 + 거친 미션
                var routes = new (string Id, bool Relief, string[] Ms)[] { ("S1", false, new[] { "BT1", "SK1", "SK4" }), ("S2", true, new[] { "BT1", "SK2", "SK4" }), ("S3", true, new[] { "BT1", "SK1", "SK2" }), ("S4", false, new[] { "BT1" }) };
                var got = new List<string>(); bool allOk = true;
                foreach (var (id, relief, ms) in routes)
                {
                    var r = chk.GetProperty(id);
                    var cs = new CampaignState();
                    CampaignFunds.AddPermanentLoss(cs, r.GetProperty("permanentUse").GetSingle());
                    float avail = CampaignFunds.AvailableSupply(cs);
                    float sum = (relief ? d.CivilianReliefSupplyLoss : 0f) + ms.Sum(M);
                    float expect = r.GetProperty("op1AvailableSupply").GetSingle();
                    bool ok = Math.Abs(avail - expect) <= tol && Math.Abs(d.EpisodeSupplyStart - sum - expect) <= tol;
                    allOk &= ok; got.Add($"{id} {avail:0}/{expect:0} (합산 {d.EpisodeSupplyStart - sum:0})");
                }
                check(allOk, "C1E1-d2 op1SupplyCheck(JSON) OP1 가용 보급 ±5: " + string.Join(" · ", got));
            }
            catch (Exception ex) { check(false, "C1E1-d2 op1SupplyCheck 읽기 실패: " + ex.Message); }

            // 8) JSON 깨진 값 보정
            var wild = new CampaignEconomyDefs { RegularControlCap = -4, ForeignSlotCap = int.MaxValue, PermanentDeployFrac = float.NaN, EpisodeSupplyStart = -1, VolunteerBlockMen = 0 }.Sanitized();
            check(wild.RegularControlCap == 0 && wild.ForeignSlotCap == 1_000_000 && wild.PermanentDeployFrac == 0.1f && wild.EpisodeSupplyStart == 0 && wild.VolunteerBlockMen == 1, "TC-D 경제 JSON 보정");
        }
        finally { CampaignEconomyDefs.Override(saved); }
    }
}
