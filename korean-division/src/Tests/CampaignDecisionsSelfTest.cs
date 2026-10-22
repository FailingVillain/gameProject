using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>TC-E 변수 레지스트리 (favor.* 등) · 결정 16개 데이터 · 결정 런타임 (응답/조건/성립/협상 점수/자금) · 재도전 복원 · 저장 v13. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignDecisionsSelfTest
{
    static string[] One(string c) => new[] { c };

    public static void Run(Action<bool, string> check)
    {
        var savedEco = CampaignEconomyDefs.Current;
        try
        {
            CampaignEconomyDefs.Override(null);
            CampaignVars.Override(null); CampaignDecisions.Override(null);

            // 1) 변수 레지스트리 JSON + 별칭 (옛 Flags 키 공유) + 범위
            var s0 = new CampaignState();
            bool fresh = CampaignVars.Defs.Count == 18 && CampaignVars.Get(s0, "favor.hwangson") == 60 && CampaignVars.Get(s0, "favor.minDaejang") == 50 && s0.Flags["heir_mood"] == 60 && s0.Flags["cmd_favor"] == 50
                && CampaignVars.Get(s0, "favor.namChamjang") == 0 && CampaignVars.Get(s0, "maxCasualtyFrac") == 50 && !CampaignVars.Has("favor.kimJuhyun");
            check(fresh, $"TC-E 변수: 18개 로드 · favor.hwangson 60 / minDaejang 50 = 옛 heir_mood / cmd_favor 공유 (등록 {CampaignVars.Defs.Count})");
            bool a1 = CampaignVars.Set(s0, "favor.hwangson", 250), a2 = CampaignVars.Add(s0, "rep.local", -1000), a3 = CampaignVars.Set(s0, "nope.var", 5), a4 = CampaignVars.Add(s0, "pow.bandit", int.MaxValue);
            check(a1 && a2 && !a3 && a4 && s0.Flags["heir_mood"] == 100 && CampaignVars.Get(s0, "rep.local") == -100 && CampaignVars.Get(s0, "nope.var") == 0 && !s0.Flags.ContainsKey("nope.var") && CampaignVars.Get(s0, "pow.bandit") == 100000,
                "TC-E 변수: 범위로 자름 (호감 100 · 평판 -100 · 포로 상한) · 모르는 키는 쓰기 거부 / 읽기 0");
            var snap = CampaignVars.SnapshotAll(s0); CampaignVars.Set(s0, "favor.hwangson", 1); CampaignVars.Set(s0, "flag.namLetter", 1); CampaignVars.RestoreAll(s0, snap);
            check(CampaignVars.Get(s0, "favor.hwangson") == 100 && CampaignVars.Get(s0, "flag.namLetter") == 0 && snap.Count == 18, "TC-E 변수: 스냅샷 / 복원");
            var junk = CampaignVars.Sanitized(new CampaignVarDef?[] { null, new() { Key = "" }, new() { Key = "x.a", Min = 5, Max = 1 }, new() { Key = "x.b", Min = 0, Max = 10, Default = 99 }, new() { Key = "x.b" }, new() { Key = "x.c", Alias = "x.b" } });
            check(junk.Count == 1 && junk[0].Key == "x.b" && junk[0].Default == 10, "TC-E 변수 JSON 보정: 빈 키 · min>max · 키 중복 · 저장 키 충돌은 버리고 초기값은 범위로");

            // 2) 결정 16개 데이터
            var dd = CampaignDecisions.Current;
            int major = dd.Decisions.Count(d => d.Tier == "major"), std = dd.Decisions.Count(d => d.Tier == "std"), minor = dd.Decisions.Count(d => d.Tier == "minor");
            check(dd.LoadErrors.Count == 0 && dd.Decisions.Count == 16 && major == 4 && std == 8 && minor == 4 && dd.Decisions.All(d => d.Consumers.Count > 0 && Array.IndexOf(CampaignDecisions.Kinds, d.Kind) >= 0),
                $"TC-E 결정 데이터: 16개 (major {major} · std {std} · minor {minor}) · 소비처 전부 · kind 7종 안 · 오류 {string.Join(";", dd.LoadErrors)}");
            check(new[] { "D02", "D10", "D11", "D13" }.All(i => dd.Find(i)?.Tier == "major") && new[] { "D04", "D08", "D12", "D14" }.All(i => dd.Find(i)?.Tier == "minor")
                && dd.Decisions.Where(d => d.Kind == "INMISSION").All(d => d.PausesMission) && dd.Find("D02")!.Multi && dd.Decisions.Where(d => d.Kind == "SELECT").Count() == 2,
                "TC-E 결정 데이터: 가중치 배치 (major D02/D10/D11/D13 · minor D04/D08/D12/D14) · 미션 중 결정은 일시정지 · D02 중복 선택 · SELECT 2개");
            var seen = new HashSet<string> { "D01" };
            bool v1 = CampaignDecisions.Validate(new DecisionDef { Id = "D01", Consumers = { "x" }, Choices = { new() { Id = "A" } } }, seen) != null
                && CampaignDecisions.Validate(new DecisionDef { Id = "Z", Kind = "BOGUS", Consumers = { "x" }, Choices = { new() { Id = "A" } } }, seen) != null
                && CampaignDecisions.Validate(new DecisionDef { Id = "Z", Choices = { new() { Id = "A" } } }, seen) != null
                && CampaignDecisions.Validate(new DecisionDef { Id = "Z", Consumers = { "x" }, Choices = { new() { Id = "A", EnableIf = { new() { Var = "nope" } } } } }, seen) != null
                && CampaignDecisions.Validate(new DecisionDef { Id = "Z", Consumers = { "x" }, Choices = { new() { Id = "A", Effects = { new() { Op = "set", Var = "nope" } } } } }, seen) != null
                && CampaignDecisions.Validate(new DecisionDef { Id = "Z", Consumers = { "x" }, Choices = { new() { Id = "A" }, new() { Id = "A" } } }, seen) != null
                && CampaignDecisions.Validate(new DecisionDef { Id = "Z", Tier = "std", Consumers = { "x" }, Choices = { new() { Id = "A" } } }, seen) == null;
            check(v1, "TC-E 결정 검증: id 중복 · 모르는 kind · 소비처 없음 · 모르는 변수(조건/효과) · 선택지 중복은 거부, 정상은 통과");

            // 3) 런타임: 조건 / 성립 / 원자성
            var s = new CampaignState { Day = 2 };
            string eB = dd.Answer(s, "D09", One("B"), 0, out _);
            check(eB.Contains("고를 수 없는") && s.Decisions.Answers.Count == 0 && !dd.IsEnabled(s, "D09", "B") && dd.IsEnabled(s, "D09", "D"), "TC-E D09 B 포로 처형: pow.bandit 0 이면 비활성 (불변)");
            CampaignVars.Set(s, "pow.bandit", 40);
            string okB = dd.Answer(s, "D09", One("B"), 0, out var ansB);
            check(okB == "" && ansB != null && CampaignVars.Get(s, "pow.bandit") == 0 && ansB.Day == 2 && dd.IsAnswered(s, "D09"), "TC-E D09 B: pow.bandit 40 이면 선택 가능, 처형 후 0");
            check(dd.Answer(s, "D09", One("D"), 0, out _).Contains("이미 응답") && dd.Answer(s, "D01", new[] { "A", "B" }, 0, out _).Contains("하나만") && dd.Answer(s, "D01", One("Z"), 0, out _).Contains("모르는 선택지")
                && dd.Answer(s, "D99", One("A"), 0, out _).Contains("모르는 결정") && dd.Answer(s, "D01", Array.Empty<string>(), 0, out _).Contains("선택이 없"), "TC-E 응답 거부: 중복 · 단일 결정 복수 선택 · 모르는 선택지/결정 · 빈 선택");
            // D10 설득: 조건 없어도 선택 가능 (R7), 결과만 갈림
            var sp = new CampaignState();
            string pv0 = dd.PreviewOutcome(sp, "D10", "C");
            string e10a = dd.Answer(sp, "D10", One("C"), 0, out var a10a);
            var sq = new CampaignState(); CampaignVars.Set(sq, "intel.andong", 1);
            string pv1 = dd.PreviewOutcome(sq, "D10", "C");
            string e10b = dd.Answer(sq, "D10", One("C"), 0, out var a10b);
            string e10c = dd.Answer(new CampaignState(), "D10", One("A"), 0, out var a10c);
            check(pv0 == "fail" && e10a == "" && a10a!.Outcome == "fail" && pv1 == "ok" && e10b == "" && a10b!.Outcome == "ok" && e10c == "" && a10c!.Outcome == "" && dd.IsEnabled(sp, "D10", "C"),
                "TC-E D10 설득: 조건 없어도 선택 가능 · 예고 fail/ok · 기록 fail/ok (따름은 빈 결과)");

            // 자금: D02 A(150명 = 6) + C(대민 구제: 영구 -400); 부족하면 불변
            var sf = new CampaignState();
            string eAC = dd.Answer(sf, "D02", new[] { "A", "C" }, 0, out var aAC);
            check(eAC == "" && sf.Funds == 4 && Math.Abs(sf.EpisodePermanentSupplyLoss - CampaignEconomyDefs.Current.CivilianReliefSupplyLoss) < 0.01f && aAC!.Choices.Count == 2,
                $"TC-E D02 A+C: 자금 10 → 4 (의용군 150 = 6) · 영구 보급 소모 400 (자금 {sf.Funds}, 소모 {sf.EpisodePermanentSupplyLoss:0})");
            var sg = new CampaignState { Funds = 5 };
            string eAB = dd.Answer(sg, "D02", new[] { "A", "B" }, 100, out _);
            check(eAB.Contains("모자랍니다") && sg.Funds == 5 && sg.Decisions.Answers.Count == 0 && sg.EpisodePermanentSupplyLoss == 0f, "TC-E D02 A+B(100): 필요 9 > 보유 5 → 거부, 아무것도 안 바뀜 (원자적)");
            var sh = new CampaignState();
            string eB2 = dd.Answer(sh, "D02", new[] { "B", "C" }, 300, out _);
            check(eB2 == "" && sh.Funds == 1 && CampaignFunds.BanditPrice(300) == 9, "TC-E D02 B(300)+C: 마적 100명당 3 → 9, 자금 10 → 1");
            var sw = new CampaignState();
            dd.Answer(sw, "D09", One("A"), 0, out _);
            var sw2 = new CampaignState(); dd.Answer(sw2, "D09", One("C"), 0, out _);
            check(sw.Funds == 8 && sw2.Funds == 8, "TC-E D09 요정 / 보검: 각 2");

            // 협상 점수
            var sn = new CampaignState();
            dd.Answer(sn, "D02", new[] { "A", "C" }, 0, out _);
            int n1 = dd.Negotiation(sn);
            bool d13off = !dd.IsEnabled(sn, "D13", "C") && dd.IsEnabled(sn, "D13", "B") && dd.IsEnabled(sn, "D13", "A");
            dd.Answer(sn, "D07", One("B"), 0, out _);
            int n2 = dd.Negotiation(sn); bool intel = CampaignVars.Get(sn, "intel.andong") == 1;
            CampaignVars.Set(sn, "flag.andongCavalryCut", 1);
            int n3 = dd.Negotiation(sn);
            var sn2 = new CampaignState(); dd.Answer(sn2, "D02", One("A"), 0, out _);   // A 만 = 점수 없음
            check(n1 == 1 && n2 == 2 && n3 == 3 && intel && d13off && dd.IsEnabled(sn, "D13", "C") && dd.Negotiation(sn2) == 0 && !dd.IsEnabled(sn2, "D13", "B") && dd.Negotiation(new CampaignState()) == 0,
                $"TC-E 협상 점수 0~3: D02 A+C +1 · D07 B +1 · SK4 성공 +1 ({n1}/{n2}/{n3}) · D13 C 는 3점부터, B 는 1점부터, 0점은 무장해제만");

            // 대기(Pending): 제기 → 응답 전까지 일시정지 사유
            var sr = new CampaignState();
            bool r0 = dd.Raise(sr, "D04") == "" && dd.HasPending(sr) && dd.Raise(sr, "D04") == "" && sr.Decisions.Pending.Count == 1 && dd.Raise(sr, "D99") != "";
            dd.Answer(sr, "D04", One("A"), 0, out _);
            check(r0 && !dd.HasPending(sr) && dd.Raise(sr, "D04").Contains("이미"), "TC-E minor 결정: 제기하면 응답할 때까지 대기 (일시정지 사유) · 응답하면 해소");

            // 4) 재도전 복원: 일수 불변, 응답/변수/자금/보급 되돌림. 승리하면 일수 확정.
            var sm = new CampaignState { Day = 3, Supply = 1234f };
            dd.Answer(sm, "D01", One("A"), 0, out _);
            CampaignVars.Set(sm, "rep.local", 7);
            CampaignDecisions.BeginMission(sm);
            dd.Raise(sm, "D04");
            dd.Answer(sm, "D04", One("A"), 0, out _); dd.Answer(sm, "D09", One("A"), 0, out _);
            CampaignVars.Add(sm, "morale.troops", -30); CampaignVars.Set(sm, "rep.local", -9); sm.Supply = 900f; CampaignFunds.AddPermanentLoss(sm, 50f);
            bool during = sm.Decisions.Answers.Count == 3 && sm.Funds == 8;
            bool retried = CampaignDecisions.RetryMission(sm);
            check(during && retried && sm.Day == 3 && sm.Decisions.Answers.Count == 1 && sm.Decisions.Answers[0].Id == "D01" && sm.Funds == 10 && CampaignVars.Get(sm, "rep.local") == 7 && CampaignVars.Get(sm, "morale.troops") == 70
                && sm.Supply == 1234f && sm.EpisodePermanentSupplyLoss == 0f && !dd.HasPending(sm) && sm.Decisions.Checkpoint == null && !CampaignDecisions.RetryMission(sm) && !dd.IsAnswered(sm, "D04"),
                $"TC-E 재도전 복원: 미션 중 응답 · 변수 · 자금 · 보급 되돌림, 일수 {sm.Day} 그대로 (재도전은 일수 안 씀), 복원 뒤 같은 결정 다시 응답 가능");
            check(dd.Answer(sm, "D04", One("B"), 0, out _) == "", "TC-E 재도전 뒤 같은 결정 재응답 가능");
            // 승리: 일수가 미션 승리 시점에 확정된다
            var sv = new CampaignState { Day = 3 };
            CampaignDecisions.BeginMission(sv);
            dd.Answer(sv, "D12", One("A"), 0, out var a12);
            int dayDuring = a12!.Day;
            sv.Day += CampaignEconomyDefs.Current.MissionDays;   // 승리 정산이 일수를 더한 뒤
            CampaignDecisions.CommitMission(sv);
            check(dayDuring == 3 && sv.Decisions.Answers[0].Day == 3 + CampaignEconomyDefs.Current.MissionDays && sv.Decisions.Checkpoint == null && !CampaignDecisions.RetryMission(sv),
                "TC-E 일수: 미션 중 응답은 승리 시점에 확정된 날짜로 다시 찍힘 · 복원점 폐기");

            // 5) 저장 v13: 왕복 (복원점 포함) · 옛 파일 이행 · 손상 값
            var sp2 = new CampaignState { Day = 4 };
            dd.Answer(sp2, "D01", One("B"), 0, out _);
            CampaignDecisions.BeginMission(sp2);
            dd.Raise(sp2, "D08"); CampaignVars.Set(sp2, "favor.siwidae", 33);
            var back = CampaignState.FromJson(sp2.ToJson());
            check(back != null && back.Version == CampaignState.SchemaVersion && CampaignState.SchemaVersion >= 13 && back.Decisions.Answers.Count == 1 && back.Decisions.Answers[0].Choices[0] == "B" && back.Decisions.Pending.SequenceEqual(new[] { "D08" })
                && back.Decisions.Checkpoint != null && back.Decisions.Checkpoint.Answers == 1 && back.Decisions.Checkpoint.Vars.Count == 18 && CampaignVars.Get(back, "favor.siwidae") == 33 && back.ToJson() == sp2.ToJson(),
                "TC-E 저장 v13: 응답 · 대기 · 복원점 · 변수(Flags) 왕복 동일");
            var old = CampaignState.FromJson("{\"Version\":11,\"Funds\":7,\"Flags\":{\"heir_mood\":80,\"cmd_favor\":41}}");
            check(old != null && old.Version == CampaignState.SchemaVersion && old.Decisions.Answers.Count == 0 && old.Decisions.Checkpoint == null && old.Funds == 7 && CampaignVars.Get(old, "favor.hwangson") == 80 && CampaignVars.Get(old, "favor.minDaejang") == 41,
                "TC-E 저장 이행: v11 → v13 (빈 결정 기록 · 호감 80/41 그대로)");
            var bad = CampaignState.FromJson("{\"Decisions\":{\"Answers\":[{\"Id\":\"D01\",\"Choices\":[\"\",\"A\"],\"Quantity\":-5,\"Day\":-3},{\"Id\":\"\"}],\"Pending\":[\"\",\"D04\"],\"Checkpoint\":{\"Answers\":99,\"Funds\":-4,\"Supply\":-1}},\"Flags\":{\"heir_mood\":900,\"rep.local\":-777,\"pow.bandit\":-5}}");
            check(bad != null && bad.Decisions.Answers.Count == 1 && bad.Decisions.Answers[0].Choices.Count == 1 && bad.Decisions.Answers[0].Quantity == 0 && bad.Decisions.Answers[0].Day == 0 && bad.Decisions.Pending.SequenceEqual(new[] { "D04" })
                && bad.Decisions.Checkpoint!.Answers == 1 && bad.Decisions.Checkpoint.Funds == 0 && bad.Decisions.Checkpoint.Supply == 0f && CampaignVars.Get(bad, "favor.hwangson") == 100 && bad.Flags["heir_mood"] == 100 && bad.Flags["rep.local"] == -100 && bad.Flags["pow.bandit"] == 0,
                "TC-E 저장 보정: 빈 선택/수량 음수/일수 음수/복원점 범위/Flags 변수 범위");
            var nul = CampaignState.FromJson("{\"Decisions\":null,\"Flags\":null}");
            check(nul != null && nul.Decisions.Answers.Count == 0 && CampaignVars.Get(nul, "favor.hwangson") == 60, "TC-E 저장: Decisions/Flags null 이어도 기본값");

            // 6) 기본 설정에서 기존 동작 불변: 새 상태의 옛 Flags 키 · 이벤트 처리가 같은 값을 공유
            var se = new CampaignState();
            CampaignVars.Add(se, "favor.hwangson", -10);
            check(se.Flags["heir_mood"] == 50 && se.Flags.Count == 2, "TC-E 하위 호환: 새 상태 Flags 는 옛 두 키뿐, favor.hwangson 변경이 heir_mood 에 반영");
        }
        finally { CampaignEconomyDefs.Override(savedEco); CampaignVars.Override(null); CampaignDecisions.Override(null); }
    }
}
