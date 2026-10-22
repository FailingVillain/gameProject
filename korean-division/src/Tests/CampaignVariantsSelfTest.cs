using System.Text.Json.Nodes;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>TC-F 미션 데이터 변형 (MissionVariants) · 결과 표 (CampaignVariants: SK 플래그 · pow.bandit) · D15 협상 -1. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignVariantsSelfTest
{
    // 작은 시험 미션: 적 소총 110 (70+40) · 잡병 60 (30+30) · 기마 50 · 기관총 2 · 부두 경비 20 · 증원 기마 30 (사건 spawn). variants = 실제 BT1/OP1 데이터가 쓸 형태.
    const string Mission = @"{
  ""id"": ""tcf_test"", ""title"": ""t"", ""seed"": 7, ""player"": 1,
  ""factions"": [
    { ""name"": ""한국군"", ""en"": ""korean"", ""lang"": ""ko"", ""color"": [60,130,235], ""team"": 1 },
    { ""name"": ""마적"", ""en"": ""bandit"", ""lang"": ""zh"", ""color"": [200,120,40], ""team"": 2, ""posture"": ""hold"" } ],
  ""mapData"": { ""name"": ""t"", ""width"": 120, ""height"": 96, ""base"": ""grass"", ""starts"": [[20,80],[60,20]] },
  ""areas"": { ""a_player"": { ""rect"": [10,70,30,20] }, ""a_flank"": { ""rect"": [60,30,10,10] }, ""a_dock"": { ""circle"": [100,20,6] } },
  ""deployZones"": [ { ""faction"": 1, ""rect"": [10,70,30,20] } ],
  ""props"": [ { ""type"": ""rock_s"", ""x"": 62, ""y"": 32 }, { ""type"": ""rock_s"", ""x"": 20, ""y"": 20 } ],
  ""forces"": [
    { ""id"": ""b1"", ""faction"": 1, ""at"": [20,80], ""companies"": [ { ""units"": { ""officer"": 1, ""infantry"": 20 } } ] },
    { ""id"": ""e_a"", ""faction"": 2, ""at"": [60,20], ""companies"": [ { ""units"": { ""bandit_rifleman"": 70, ""bandit_thug"": 30 } } ] },
    { ""id"": ""e_b"", ""faction"": 2, ""at"": [80,20], ""companies"": [ { ""units"": { ""bandit_rifleman"": 40, ""bandit_thug"": 30 } } ] },
    { ""id"": ""e_mg"", ""faction"": 2, ""at"": [70,25], ""units"": { ""heavy_maxim"": 2 } },
    { ""id"": ""e_cav"", ""faction"": 2, ""at"": [90,30], ""companies"": [ { ""units"": { ""bandit_horseman"": 50 } } ] },
    { ""id"": ""e_dock"", ""faction"": 2, ""at"": [100,20], ""units"": { ""bandit_rifleman"": 20 } } ],
  ""reinforcements"": [ { ""id"": ""e_counter"", ""faction"": 2, ""area"": ""a_dock"", ""units"": { ""bandit_horseman"": 30 } } ],
  ""objectives"": [ { ""id"": ""o1"", ""type"": ""eliminate"", ""primary"": true, ""text"": ""x"" } ],
  ""events"": [ { ""id"": ""ev1"", ""when"": { ""time"": 60 }, ""do"": [ { ""spawn"": ""e_counter"" }, { ""message"": ""hi"" } ] } ],
  ""variants"": [
    { ""id"": ""defect"", ""when"": [ { ""decision"": ""D02"", ""has"": ""B"" } ],
      ""ops"": [ { ""op"": ""removeUnits"", ""faction"": 2, ""units"": [""bandit_rifleman"", ""bandit_thug""], ""forces"": [""e_a"", ""e_b""], ""count"": { ""decision"": ""D02"", ""choice"": ""B"", ""per"": 100, ""each"": 25, ""max"": 75 } } ] },
    { ""id"": ""flank"", ""when"": [ { ""var"": ""flag.flankPath"", ""op"": "">="", ""value"": 1 } ],
      ""ops"": [ { ""op"": ""addDeployZone"", ""faction"": 1, ""rect"": [60,60,20,10] }, { ""op"": ""removeProps"", ""area"": ""a_flank"" } ] },
    { ""id"": ""dock"", ""when"": [ { ""var"": ""flag.dockIntel"", ""value"": 1 } ], ""ops"": [ { ""op"": ""removeForces"", ""ids"": [""e_dock"", ""e_counter""] } ] },
    { ""id"": ""cavcut"", ""when"": [ { ""var"": ""flag.andongCavalryCut"", ""value"": 1 } ], ""ops"": [ { ""op"": ""scaleUnits"", ""faction"": 2, ""units"": [""bandit_horseman""], ""percent"": 60 } ] },
    { ""id"": ""cavboost"", ""when"": [ { ""var"": ""flag.andongCavalryBoost"", ""value"": 1 } ], ""ops"": [ { ""op"": ""scaleUnits"", ""faction"": 2, ""units"": [""bandit_horseman""], ""percent"": 120, ""scope"": ""forces"" } ] }
  ]
}";

    static void D02B(CampaignState s, int men) { s.Decisions.Answers.Add(new DecisionAnswer { Id = "D02", Choices = { "B" }, Quantity = men, Day = 1 }); }

    static int Count(JsonNode root, string list, string unitKey, string? forceId = null)
    {
        int n = 0;
        foreach (var f in (root[list] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            if (forceId != null && f["id"]?.GetValue<string>() != forceId) continue;
            if (f["units"] is JsonObject u && u[unitKey] != null) n += u[unitKey]!.GetValue<int>();
            if (f["companies"] is JsonArray cs) foreach (var c in cs.OfType<JsonObject>()) if (c["units"] is JsonObject cu && cu[unitKey] != null) n += cu[unitKey]!.GetValue<int>();
        }
        return n;
    }

    static JsonNode P(string text) => JsonNode.Parse(text)!;

    public static void Run(Action<bool, string> check)
    {
        CampaignVars.Override(null); CampaignDecisions.Override(null); CampaignVariants.Override(null);
        try
        {
            // 1) 캠페인 없음 / 조건 불성립 = 변형 없음 (variants 절만 지워짐), variants 없는 텍스트는 그대로 (기본 상태 불변)
            string plain = "{\"id\":\"x\"}";
            string none = MissionVariants.Patch(Mission, null), blank = MissionVariants.Patch(Mission, new CampaignState());
            check(MissionVariants.Patch(plain, new CampaignState()) == plain && none == blank && !none.Contains("variants") && Count(P(none), "forces", "bandit_rifleman") == 130 && Count(P(none), "forces", "bandit_horseman") == 50,
                "TC-F 변형: variants 없는 미션 텍스트는 그대로 · 캠페인 없음/조건 불성립 = 적용 0 (기본 상태 불변)");

            // 2) D02 B: 마적 100명당 25명 이탈 (최대 75), 소총 → 잡병 순, 기마 · 중기관총 유지
            var sd = new CampaignState(); D02B(sd, 300);
            var d3 = P(MissionVariants.Patch(Mission, sd, out var ap3));
            check(ap3.SequenceEqual(new[] { "defect" }) && Count(d3, "forces", "bandit_rifleman", "e_a") == 0 && Count(d3, "forces", "bandit_rifleman", "e_b") == 35 && Count(d3, "forces", "bandit_thug") == 60
                && Count(d3, "forces", "bandit_horseman") == 50 && Count(d3, "forces", "heavy_maxim") == 2 && Count(d3, "forces", "bandit_rifleman", "e_dock") == 20,
                $"TC-F D02 B 300명: 소총 75 이탈 (e_a 70 + e_b 5), 잡병 · 기마 · 중기관총 · 다른 병력 유지 (소총 e_b {Count(d3, "forces", "bandit_rifleman", "e_b")})");
            int Rifle(int men) { var s = new CampaignState(); D02B(s, men); var p = P(MissionVariants.Patch(Mission, s)); return Count(p, "forces", "bandit_rifleman", "e_a") + Count(p, "forces", "bandit_rifleman", "e_b"); }
            int r100 = Rifle(100), r99 = Rifle(99), r1000 = Rifle(1000), r0 = Rifle(0);
            check(r100 == 85 && r99 == 110 && r1000 == 35 && r0 == 110, $"TC-F D02 B 수량: 100명=25 · 99명=0 (블록 내림) · 1000명=상한 75 · 0명=0 ({r100}/{r99}/{r1000}/{r0})");
            var sa = new CampaignState(); sa.Decisions.Answers.Add(new DecisionAnswer { Id = "D02", Choices = { "A", "C" }, Quantity = 500, Day = 1 });
            MissionVariants.Patch(Mission, sa, out var apA);
            check(apA.Count == 0, "TC-F D02 A+C 만 고르면 이탈 없음 (선택지에 B 없음)");
            string lit = Mission.Replace("\"count\": { \"decision\": \"D02\", \"choice\": \"B\", \"per\": 100, \"each\": 25, \"max\": 75 }", "\"count\": 150");
            var lt = P(MissionVariants.Patch(lit, sd));
            check(Count(lt, "forces", "bandit_rifleman", "e_a") + Count(lt, "forces", "bandit_rifleman", "e_b") == 0 && Count(lt, "forces", "bandit_thug") == 20, "TC-F removeUnits 정수 count 150: 소총 110 먼저, 남은 40 은 잡병에서 (우선순위)");

            // 3) SK 플래그 3종 + 기마 실패 확정
            var sf = new CampaignState(); CampaignVars.Set(sf, "flag.flankPath", 1);
            var f1 = P(MissionVariants.Patch(Mission, sf));
            check(((JsonArray)f1["deployZones"]!).Count == 2 && ((JsonArray)f1["props"]!).Count == 1 && Count(f1, "forces", "bandit_rifleman") == 130, "TC-F flag.flankPath: 배치 구역 +1 · 우회로 구역의 소품 제거 (적 배치 불변)");
            var sk = new CampaignState(); CampaignVars.Set(sk, "flag.dockIntel", 1);
            var k1 = P(MissionVariants.Patch(Mission, sk));
            var kdo = (JsonArray)k1["events"]![0]!["do"]!;
            check(!((JsonArray)k1["forces"]!).OfType<JsonObject>().Any(o => o["id"]!.GetValue<string>() == "e_dock") && ((JsonArray)k1["reinforcements"]!).Count == 0 && kdo.Count == 1 && kdo[0]!["message"] != null,
                "TC-F flag.dockIntel: 부두 경비 병력 제거 · 증원 제거 + 그 증원을 부르는 사건 줄만 삭제 (메시지 줄 유지)");
            var sc = new CampaignState(); CampaignVars.Set(sc, "flag.andongCavalryCut", 1);
            var c1 = P(MissionVariants.Patch(Mission, sc));
            var sb = new CampaignState(); CampaignVars.Set(sb, "flag.andongCavalryBoost", 1);
            var b1 = P(MissionVariants.Patch(Mission, sb));
            check(Count(c1, "forces", "bandit_horseman") == 30 && Count(c1, "reinforcements", "bandit_horseman") == 18 && Count(b1, "forces", "bandit_horseman") == 60 && Count(b1, "reinforcements", "bandit_horseman") == 30,
                "TC-F flag.andongCavalryCut 기마 60% (50->30, 증원 30->18) · 실패 확정 120% (50->60, scope=forces 라 증원 그대로 30)");

            // 4) 결정적 + 로드 가능 + 월드 생성
            var all = new CampaignState(); D02B(all, 300); CampaignVars.Set(all, "flag.flankPath", 1); CampaignVars.Set(all, "flag.andongCavalryCut", 1);
            string t1 = MissionVariants.Patch(Mission, all, out var apAll), t2 = MissionVariants.Patch(Mission, all);
            var def = MissionDef.Parse(t1, "tcf_test");
            var w = World.CreateMissionWorld(def, Defs.Current);
            int enemies = w.Units.Count(u => u.Alive && u.Owner == 2);
            check(t1 == t2 && apAll.SequenceEqual(new[] { "defect", "flank", "cavcut" }) && def.DeployZones.Count == 2 && enemies > 0,
                $"TC-F 같은 입력 = 같은 텍스트 · 적용 순서 보존 · 적용 결과 파싱/월드 생성 가능 (적 유닛 {enemies}, 해시 {def.Hash[..8]})");
            var wBase = World.CreateMissionWorld(MissionDef.Parse(MissionVariants.Patch(Mission, null), "tcf_test"), Defs.Current);
            int enemiesBase = wBase.Units.Count(u => u.Alive && u.Owner == 2);
            check(MissionDef.Parse(MissionVariants.Patch(Mission, sd), "t").Hash != MissionDef.Parse(MissionVariants.Patch(Mission, null), "t").Hash && enemiesBase - enemies == 75 + (50 - 30),
                $"TC-F 변형이 월드 유닛 수에 반영 (기본 {enemiesBase} -> {enemies}: 이탈 -75, 기마 -20) · 미션 해시가 달라져 리플레이 구분");

            // 5) 잘못된 데이터는 즉시 오류 (조건 불성립인 변형도 형식 검사)
            bool Bad(string a, string b) { try { MissionVariants.Patch(Mission.Replace(a, b), new CampaignState()); return false; } catch (InvalidDataException) { return true; } }
            check(Bad("\"removeProps\"", "\"explode\"") && Bad("\"flag.dockIntel\"", "\"flag.nope\"") && Bad("\"area\": \"a_flank\" }", "\"area\": \"a_none\" }") && Bad("\"percent\": 60", "\"percent\": 5000")
                && Bad("\"id\": \"dock\"", "\"id\": \"flank\"") && Bad("\"faction\": 2, \"units\": [\"bandit_horseman\"], \"percent\": 120", "\"faction\": 9, \"units\": [\"bandit_horseman\"], \"percent\": 120"),
                "TC-F 잘못된 변형 거부: 모르는 조작 · 모르는 변수 · 없는 구역 · 범위 밖 percent · id 중복 · 세력 범위");

            // 6) 결과 표: SK 플래그 · pow.bandit (BT1 + SK1 합산) · 실패 확정 · 해당 없는 미션 불변
            var sr = new CampaignState();
            var bt1 = new MissionResults { MissionId = "c1e1_bt1", Result = "won", EnemyCaptured = 50, EnemyCapturedByFaction = { ["bandit"] = 40, ["fengtian"] = 10 } };
            var sk1 = new MissionResults { MissionId = "c1e1_sk1", Result = "won", EnemyCaptured = 25, EnemyCapturedByFaction = { ["bandit"] = 25 } };
            CampaignVariants.OnResult(sr, "c1e1_bt1", "won", bt1);
            bool afterBt1 = CampaignVars.Get(sr, "pow.bandit") == 40 && CampaignVars.Get(sr, "flag.flankPath") == 0;
            CampaignVariants.OnResult(sr, "c1e1_sk1", "won", sk1);
            check(afterBt1 && CampaignVars.Get(sr, "pow.bandit") == 65 && CampaignVars.Get(sr, "flag.flankPath") == 1 && CampaignVars.Get(sr, "flag.dockIntel") == 0,
                "TC-F pow.bandit = BT1 40 + SK1 25 = 65 (마적 세력만, 봉천 10 제외) · SK1 승리 = flag.flankPath, 다른 플래그 불변");
            var sl = new CampaignState();
            CampaignVariants.OnResult(sl, "c1e1_bt1", "lost", new MissionResults { MissionId = "c1e1_bt1", Result = "lost", EnemyCapturedByFaction = { ["bandit"] = 9 } });
            CampaignVariants.OnResult(sl, "c1e1_sk4", "lost", null);
            CampaignVariants.OnResult(sl, "c1e1_sk2", "won", new MissionResults { MissionId = "c1e1_sk2", Result = "won", EnemyCapturedByFaction = { ["bandit"] = 9 } });
            CampaignVariants.OnResult(sl, "c0_01_bandits", "won", new MissionResults { MissionId = "c0_01_bandits", Result = "won", EnemyCapturedByFaction = { ["bandit"] = 9 } });
            check(CampaignVars.Get(sl, "pow.bandit") == 0 && !sl.Flags.ContainsKey("flag.andongCavalryBoost") && CampaignVars.Get(sl, "flag.andongCavalryCut") == 0, "TC-F 패배 · 재도전 · 표에 없는 미션 = 변수 불변 (pow.bandit 은 승리한 BT1/SK1 만)");
            CampaignVariants.OnResult(sl, "c1e1_sk4", "won", new MissionResults { MissionId = "c1e1_sk4", Result = "won" });
            CampaignVariants.OnResult(sl, "c1e1_sk3", "won", new MissionResults { MissionId = "c1e1_sk3", Result = "won" });
            CampaignVariants.OnResult(sl, "c1e1_sk4", "failConfirmed", null);
            check(CampaignVars.Get(sl, "flag.andongCavalryCut") == 1 && CampaignVars.Get(sl, "flag.dockIntel") == 1 && CampaignVars.Get(sl, "flag.andongCavalryBoost") == 1, "TC-F SK4 승리 = andongCavalryCut · SK3 승리 = dockIntel · SK4 실패 확정 = andongCavalryBoost");

            // 정산 경로: SettleBattle 이 같은 표를 부른다 (포로 장부 T42c 는 별개로 유지)
            var ss = new CampaignState();
            CampaignEconomy.SettleBattle(ss, new MissionResults { MissionId = "c1e1_sk1", Result = "won", EnemyKills = 10, EnemyCaptured = 30, EnemyCapturedByFaction = { ["bandit"] = 30 } });
            check(CampaignVars.Get(ss, "flag.flankPath") == 1 && CampaignVars.Get(ss, "pow.bandit") == 30, "TC-F SettleBattle 경로: SK1 승리 정산 -> flankPath + pow.bandit 30");

            // 저장 / 불러오기 후 유지 + 처형 후 0 (D09 B)
            var rt = CampaignState.FromJson(ss.ToJson())!;
            check(CampaignVars.Get(rt, "flag.flankPath") == 1 && CampaignVars.Get(rt, "pow.bandit") == 30 && CampaignVars.Get(rt, "flag.andongCavalryBoost") == 0 && rt.Version == CampaignState.SchemaVersion, "TC-F 저장/불러오기 후 SK 플래그 · pow.bandit 유지 (새 저장 필드 없음, 스키마 불변)");
            string eB = CampaignDecisions.Current.Answer(rt, "D09", new[] { "B" }, 0, out _);
            check(eB == "" && CampaignVars.Get(rt, "pow.bandit") == 0, "TC-F pow.bandit 30 -> D09 B 처형 활성 · 처형 후 0 (소비처 연결)");

            // 7) 결과 수집: 세력 en 별 항복 인원
            var rw = World.CreateMissionWorld(MissionDef.Parse(t1, "tcf_test"), Defs.Current);
            foreach (var u in rw.Units.Where(u => u.Alive && u.Owner == 2 && u.Def.IsInfantry).Take(3).ToList()) rw.SurrenderForTest(u);
            var res = MissionResults.From(rw);
            check(res.EnemyCaptured == 3 && res.EnemyCapturedByFaction.GetValueOrDefault("bandit") == 3 && res.EnemyCapturedByFaction.Count == 1, $"TC-F 결과 수집: 항복한 적 인원이 세력 en 별로 (bandit {res.EnemyCapturedByFaction.GetValueOrDefault("bandit")})");

            // 8) D15 협상 -1: 포위(D11 B) 중 추인(D15 A) 만. 강공 중 추인 · 철회는 영향 없음, 0 아래로 안 내려감
            var dd = CampaignDecisions.Current;
            CampaignState Neg(string d11, string d15)
            {
                var s = new CampaignState();
                dd.Answer(s, "D02", new[] { "A", "C" }, 0, out _);   // +1
                CampaignVars.Set(s, "flag.andongCavalryCut", 1);     // +1
                if (d11 != "") s.Decisions.Answers.Add(new DecisionAnswer { Id = "D11", Choices = { d11 }, Day = 3 });
                if (d15 != "") s.Decisions.Answers.Add(new DecisionAnswer { Id = "D15", Choices = { d15 }, Day = 4 });
                return s;
            }
            int base2 = dd.Negotiation(Neg("B", "")), sA = dd.Negotiation(Neg("B", "A")), sAssault = dd.Negotiation(Neg("A", "A")), sRetract = dd.Negotiation(Neg("B", "B")), sNoD11 = dd.Negotiation(Neg("", "A"));
            var zero = new CampaignState(); zero.Decisions.Answers.Add(new DecisionAnswer { Id = "D11", Choices = { "B" } }); zero.Decisions.Answers.Add(new DecisionAnswer { Id = "D15", Choices = { "A" } });
            check(base2 == 2 && sA == 1 && sAssault == 2 && sRetract == 2 && sNoD11 == 2 && dd.Negotiation(zero) == 0,
                $"TC-F D15 협상 -1: 포위 중 추인만 -1 (기준 {base2} -> {sA}), 강공 중 추인 {sAssault} · 철회 {sRetract} · D11 없음 {sNoD11} 은 불변, 0 미만으로 안 내려감");
        }
        finally { CampaignVars.Override(null); CampaignDecisions.Override(null); CampaignVariants.Override(null); }
    }
}
