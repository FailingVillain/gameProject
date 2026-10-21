using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 미션 틀 검증 (KD_SELFTEST_ONLY=missions): 샘플 미션 읽기 · 목표 종류별 판정 · 패배 조건 · 사건표 · 배치 단계 결정성 ·
/// 미션 중 저장 / 불러오기 · 리플레이. 그리고 창 없는 미션 실행 (--mission-run <id> [--seconds N] [--seed N] [--verify]).
/// </summary>
public static class MissionSelfTest
{
    // 시험용 미션 (64×64 들판). OBJ · FAIL · EVT 자리를 시험마다 바꿔 끼운다.
    const string Template = """
    { "id": "t_mission", "title": "T", "player": 1, "seed": 5,
      "factions": [ { "name": "A", "team": 1, "posture": "attack", "area": "goal" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "t", "width": 64, "height": 64, "base": "grass", "starts": [ [10, 32], [50, 32] ] },
      "areas": { "goal": { "rect": [28, 26, 10, 12] }, "far": { "rect": [56, 2, 6, 6] }, "trig": { "circle": [20, 50, 3] } },
      "deployZones": [ { "faction": 1, "rect": [2, 20, 20, 24] } ],
      "forces": [
        { "id": "p1", "faction": 1, "at": [10, 28], "facing": 90, "units": { "officer": 1, "infantry": 10 } },
        { "id": "p2", "faction": 1, "at": [10, 36], "facing": 90, "battalion": 2, "companies": [ { "units": { "infantry": 8 } }, { "units": { "infantry": 8 }, "rank": 2 } ] },
        { "id": "vip", "faction": 1, "at": [5, 32], "units": { "supply_wagon": 1 }, "tag": "vip", "company": false },
        { "id": "e1", "faction": 2, "at": [52, 32], "facing": 270, "units": { "infantry": 6 } },
        { "id": "boss", "faction": 2, "at": [58, 32], "units": { "officer": 1 }, "tag": "boss", "company": false }
      ],
      "reinforcements": [ { "id": "r1", "faction": 1, "area": "far", "units": { "infantry": 3 } }, { "id": "r2", "faction": 2, "at": [60, 60], "time": 5, "units": { "infantry": 2 } } ],
      "objectives": [ OBJ ], "failures": [ FAIL ], "events": [ EVT ] }
    """;

    static MissionDef Make(string obj, string fail = "", string evt = "") =>
        MissionDef.Parse(Template.Replace("OBJ", obj).Replace("FAIL", fail).Replace("EVT", evt), "t_mission");

    static World W(MissionDef d) { var w = World.CreateMissionWorld(d, Defs.Current); w.Fog.Enabled = false; return w; }
    static void Steps(World w, float sec) { for (int i = 0; i < (int)MathF.Round(sec / Config.SimDt) && w.Winner == 0; i++) w.Step(); }
    static List<Unit> Force(World w, string id) => w.Mission!.ForceUnits[id].Select(i => w.Find(i)).OfType<Unit>().ToList();
    static void Teleport(IEnumerable<Unit> us, Vector2 c) { int k = 0; foreach (var u in us) { u.Pos = c + new Vector2(k % 4 * 0.9f, k / 4 * 0.9f); k++; } }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        CampaignChecks(defs, check);
        Campaign2Checks(defs, check);
        CampaignHomeChecks(defs, check);
        TutorialChecks(defs, check);
        SliceChecks(defs, check);

        // ---- 샘플 미션: 읽기 · 생성 · 병력 수 · 중대/대대 · 계급 · 태그
        var ids = MissionDef.Available().ToList();
        check(ids.Contains("yalu_crossing") && ids.Contains("village_defense") && ids.Contains("supply_escort"), $"샘플 미션 3개 있음 ({string.Join(", ", ids)})");
        foreach (var id in ids)
        {
            try
            {
                var d = MissionDef.Load(id);
                var w = World.CreateMissionWorld(d, defs);
                int want = d.Forces.Sum(f => f.AllCompanies().Sum(c => c.Units.Values.Sum()));
                check(w.Units.Count == want && w.Mission!.Spawned.Sum() == want && w.Buildings.All(b => !b.Def.Headquarters),
                    $"미션 '{id}': 생성 {w.Units.Count}기 = 정의 {want}기, 대도시 없음, 세력 {w.Factions.Count - 1}");
            }
            catch (Exception e) when (e is InvalidDataException or IOException or KeyNotFoundException) { check(false, $"미션 '{id}' 읽기 실패: {e.Message}"); }
        }
        {
            var d = Make("""{ "id": "o", "type": "defend", "seconds": 99 }""");
            var w = W(d);
            var p2 = Force(w, "p2");
            check(p2.Count == 16 && p2.All(u => u.Company != null) && p2.Select(u => u.Company!.Id).Distinct().Count() == 2 && p2[0].Company!.Battalion?.Number == 2,
                "companies 2개 → 중대 2개 + 대대 번호 2");
            check(p2.Count(u => u.Rank == 2) == 8 && Force(w, "vip").All(u => u.Company == null) && w.Mission!.TagUnits("boss").Count == 1,
                "계급 2 중대 · 태그 · 낱 유닛 (중대 아님)");
            check(Force(w, "p1").All(u => MathF.Abs(u.Facing) < 1e-4f), "facing 90 (동쪽) → 유닛 방향 0 rad");
        }

        // ---- 목표: capture
        {
            var w = W(Make("""{ "id": "cap", "type": "capture", "area": "goal", "seconds": 2 }"""));
            Teleport(Force(w, "p1"), new Vector2(30, 28));
            Teleport(Force(w, "boss"), new Vector2(35, 35));
            foreach (var u in Force(w, "boss")) u.Hp = 100000;   // 사거리 24 면 0.3초 안에 일제 사격으로 죽을 수 있다 — 여기서는 '함께 있음' 만 본다
            Steps(w, 0.3f);
            check(w.Mission!.ObjProgress[0] == 0 && w.Winner == 0, $"capture: 적이 함께 있으면 확보 시간 정지 (진행 {w.Mission.ObjProgress[0]:0.00}, 승자 {w.Winner}, 보스 {string.Join(",", Force(w, "boss").Select(u => $"{u.Pos}/{u.Hp}"))})");
            foreach (var u in Force(w, "boss")) u.Hp = 0;
            Steps(w, 2.3f);
            check(w.Mission.ObjState[0] == MissionState.Done && w.Mission.Result == MissionState.Won && w.Winner == 1 && w.Winners.SequenceEqual(new[] { 1 }) && w.Faction(2).Defeated,
                $"capture: 단독 2초 유지 → 완료 · 승리 (진행 {w.Mission.ObjProgress[0]:0.00})");
        }
        // ---- eliminate: 태그 · 비율
        {
            var w = W(Make("""{ "id": "k", "type": "eliminate", "tag": "boss" }"""));
            Steps(w, 0.2f);
            check(w.Winner == 0, "eliminate(tag): 표적 생존 중엔 진행");
            foreach (var u in Force(w, "boss")) u.Hp = 0;
            Steps(w, 0.2f);
            check(w.Mission!.ObjState[0] == MissionState.Done && w.Winner == 1, "eliminate(tag): 태그 유닛 사망 → 완료");
            var w2 = W(Make("""{ "id": "k", "type": "eliminate", "percent": 50 }"""));
            var e1 = Force(w2, "e1");
            for (int i = 0; i < 3; i++) e1[i].Hp = 0;
            Steps(w2, 0.2f);
            check(w2.Winner == 0 && MathF.Abs(w2.Mission!.ObjProgress[0] - 300f / 7) < 0.01f, $"eliminate(50%): 3/7 처치 = {w2.Mission.ObjProgress[0]:0.0}% → 아직");
            e1[3].Hp = 0;
            Steps(w2, 0.2f);
            check(w2.Mission.ObjState[0] == MissionState.Done, "eliminate(50%): 4/7 처치 → 완료");
        }
        // ---- defend: 시간 · 구역 빼앗김
        {
            var w = W(Make("""{ "id": "d", "type": "defend", "seconds": 3 }"""));
            Steps(w, 2.9f);
            check(w.Winner == 0, "defend: 시간 전엔 진행");
            Steps(w, 0.3f);
            check(w.Mission!.ObjState[0] == MissionState.Done && w.Winner == 1, "defend: 3초 버팀 → 완료");
            var w2 = W(Make("""{ "id": "d", "type": "defend", "area": "goal", "seconds": 60, "loseAfter": 1 }"""));
            Teleport(Force(w2, "e1"), new Vector2(30, 28));
            Steps(w2, 1.3f);
            check(w2.Mission!.ObjState[0] == MissionState.Failed && w2.Mission.Result == MissionState.Lost && w2.Winner == 2 && w2.Faction(1).Defeated,
                "defend(area): 적이 1초 홀로 차지 → 실패 · 패배 (승자 = 적)");
        }
        // ---- escort: 도착 · 사망
        {
            var w = W(Make("""{ "id": "e", "type": "escort", "tag": "vip", "area": "goal" }"""));
            Steps(w, 0.2f);
            check(w.Winner == 0, "escort: 도착 전 진행");
            Teleport(Force(w, "vip"), new Vector2(32, 32));
            Steps(w, 0.2f);
            check(w.Mission!.ObjState[0] == MissionState.Done && w.Winner == 1, "escort: 태그 유닛 구역 도착 → 완료");
            var w2 = W(Make("""{ "id": "e", "type": "escort", "tag": "vip", "area": "goal" }"""));
            foreach (var u in Force(w2, "vip")) u.Hp = 0;
            Steps(w2, 0.2f);
            check(w2.Mission!.ObjState[0] == MissionState.Failed && w2.Winner == 2, "escort: 태그 유닛 사망 → 실패");
        }
        // ---- 패배 조건
        {
            const string keep = """{ "id": "d", "type": "defend", "seconds": 999 }""";
            var w = W(Make(keep, """{ "type": "losses", "percent": 50 }"""));
            var mine = w.Units.Where(u => u.Owner == 1).ToList();
            for (int i = 0; i < 13; i++) mine[i].Hp = 0;
            Steps(w, 0.2f);
            check(w.Winner == 0, "losses 50%: 13/28 손실 → 아직");
            mine[13].Hp = 0;
            Steps(w, 0.2f);
            check(w.Mission!.Result == MissionState.Lost && w.Mission.FailReason == 0, "losses 50%: 14/28 손실 → 패배 (사유 0)");
            var w2 = W(Make(keep, """{ "type": "keyUnit", "tag": "vip" }"""));
            foreach (var u in Force(w2, "vip")) u.Hp = 0;
            Steps(w2, 0.2f);
            check(w2.Mission!.Result == MissionState.Lost, "keyUnit: 핵심 유닛 사망 → 패배");
            var w3 = W(Make(keep, """{ "type": "timeLimit", "seconds": 2 }"""));
            Steps(w3, 1.9f);
            check(w3.Winner == 0, "timeLimit: 시간 전엔 진행");
            Steps(w3, 0.3f);
            check(w3.Mission!.Result == MissionState.Lost && w3.Winner == 2, "timeLimit: 2초 안에 주 목표 미달성 → 패배");
        }
        // ---- 사건표
        {
            var w = W(Make(
                """{ "id": "o1", "type": "defend", "seconds": 2, "primary": false }, { "id": "o2", "type": "eliminate", "tag": "boss", "hidden": true }""", "",
                """{ "id": "a", "when": { "time": 1 }, "do": [ { "message": "hello" }, { "weather": "rain", "time": "night" } ] },""" +
                """{ "id": "b", "when": { "area": "trig", "faction": 1 }, "do": [ { "spawn": "r1" } ] },""" +
                """{ "id": "c", "when": { "objective": "o1" }, "do": [ { "reveal": "o2" } ] },""" +
                """{ "id": "d", "when": { "killed": "boss", "time": 0.5 }, "do": [ { "message": "boss down" } ] }"""));
            var m = w.Mission!;
            check(!m.ObjRevealed[1] && m.Weather == null, "숨은 목표 · 날씨 초기값");
            Steps(w, 1.1f);
            check(m.EventFired[0] && m.Messages.Any(x => x.Text == "hello") && m.Weather == "rain" && m.TimeOfDay == "night" && !m.EventFired[1], "사건: 시간 → 메시지 · 날씨/시간대 변경");
            int before = w.Units.Count(u => u.Owner == 1);
            Teleport(Force(w, "p1").Take(1), new Vector2(20, 50));
            Steps(w, 0.1f);
            check(m.EventFired[1] && m.ReinforcementSpawned[0] && w.Units.Count(u => u.Owner == 1) == before + 3 && m.Spawned[1] == 31, "사건: 구역 진입 → 증원 r1 등장 (+3)");
            Steps(w, 1.0f);
            check(m.ObjState[0] == MissionState.Done && m.ObjRevealed[1] && w.Winner == 0, "사건: 목표 완료 → 숨은 목표 공개");
            foreach (var u in Force(w, "boss")) u.Hp = 0;
            Steps(w, 0.2f);
            check(m.EventFired[3] && m.Messages.Any(x => x.Text == "boss down") && w.Winner == 1, "사건: 태그 유닛 사망 → 메시지, 공개된 주 목표 완료 → 승리");
            var w2 = W(Make("""{ "id": "d", "type": "defend", "seconds": 99 }"""));
            Steps(w2, 5.1f);
            check(w2.Mission!.ReinforcementSpawned[1] && w2.Units.Count(u => u.Owner == 2) == 9, "증원 time 5 → 5초에 적 증원 등장");
        }

        Deployment(check);
        SaveLoadAndReplay(check);
        KoreaOnlyScan(check);
    }

    // ---------------------------------------------------------------- 배치 단계 결정성

    static void Deployment(Action<bool, string> check)
    {
        var defs = Defs.Current;
        var d = MissionDef.Load("yalu_crossing");
        var a = World.CreateMissionWorld(d, defs);
        check(!a.ApplyDeployment("j1", new Vector2(20, 30), 90), "배치: 적 병력은 옮길 수 없음");
        check(!a.ApplyDeployment("b1", new Vector2(60, 40), 90), "배치: 배치 구역 밖은 거부");
        check(a.ApplyDeployment("b1", new Vector2(20, 30), 90) && a.ApplyDeployment("mg1", new Vector2(10, 52), 45) && a.ApplyDeployment("b1", new Vector2(24, 46), 135),
            "배치: 구역 안 이동 · 회전 성공");
        var b1 = a.Mission!.ForceUnits["b1"].Select(i => a.Find(i)).OfType<Unit>().ToList();
        Vector2 cen = Vector2.Zero; foreach (var u in b1) cen += u.Pos; cen /= b1.Count;
        check(Vector2.Distance(cen, new Vector2(24, 46)) < 1.5f && a.Mission.Deployment.Count == 2 && a.Mission.Deployment[0].Force == "b1" && a.Mission.Deployment[0].Facing == 135,
            $"배치: 마지막 값만 기록 (b1 중심 {cen.X:0.0},{cen.Y:0.0}, 기록 {a.Mission.Deployment.Count}개)");
        var b = World.CreateMissionWorld(d, defs, null, a.Mission.Deployment);
        bool sameSum = a.ComputeChecksum() == b.ComputeChecksum();
        var meta = new SnapshotMeta("t", 1, false, "mission");
        bool sameBytes = Snapshot.RawBody(Snapshot.Save(a, meta)).AsSpan().SequenceEqual(Snapshot.RawBody(Snapshot.Save(b, meta)));
        check(sameSum && sameBytes, "배치: 여러 번 옮긴 월드 = 최종 목록으로 새로 만든 월드 (체크섬 · 저장 바이트 동일)");
        World.AttachMissionAi(a, 1, true); World.AttachMissionAi(b, 1, true);
        for (int i = 0; i < 900; i++) { a.Step(); b.Step(); }
        check(a.ComputeChecksum() == b.ComputeChecksum() && a.Mission.Started, $"배치: AI 와 900틱 진행 후에도 같음 (틱 {a.Tick})");
        check(!a.ApplyDeployment("b1", new Vector2(20, 30), 90), "배치: 시작한 뒤엔 거부");
    }

    // ---------------------------------------------------------------- 저장 · 리플레이

    static void SaveLoadAndReplay(Action<bool, string> check)
    {
        var defs = Defs.Current;
        var meta = new SnapshotMeta("t", 1, false, "mission");
        var w = World.CreateMissionWorld(MissionDef.Load("village_defense"), defs);
        w.ApplyDeployment("c1", new Vector2(60, 66), 0);
        World.AttachMissionAi(w, 1, true);
        w.RecordCommands = true;
        for (int i = 0; i < 3000; i++) w.Step();   // 50초 (교전 중)
        var bytes = Snapshot.Save(w, meta);
        var l = Snapshot.Load(bytes, defs).World;
        check(w.Winner == 0 && l.Mission != null && l.Mission.Started && l.Mission.Deployment.Count == 1 && l.Mission.Messages.Count == w.Mission!.Messages.Count && Snapshot.ReadHeader(bytes).Mission == "village_defense",
            $"미션 중 저장 → 불러오기: 미션 상태 복원 (메시지 {l.Mission?.Messages.Count}개, 머리말 미션 id)");
        check(Snapshot.RawBody(Snapshot.Save(l, meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes)), "미션 저장 → 불러오기 → 저장 바이트 동일");
        for (int i = 0; i < 6000; i++) { w.Step(); l.Step(); }
        check(w.ComputeChecksum() == l.ComputeChecksum() && w.Mission.ObjProgress.SequenceEqual(l.Mission!.ObjProgress) && w.Mission.EventFired.SequenceEqual(l.Mission.EventFired),
            $"불러온 미션 월드를 이어 돌린 결과 = 원래 (틱 {w.Tick}, 증원 relief {w.Mission.ReinforcementSpawned[2]})");
        var rep = ReplayFile.FromWorld(w, 1);
        string tmp = TestTemp.Child("kd_mission_rep.kdr.json");
        rep.Save(tmp);
        var back = ReplayFile.Load(tmp);
        long bad = back.Verify(defs, out string report);
        check(back.Mission?.Id == "village_defense" && back.Mission.Deployment.Count == 1 && bad < 0, $"미션 리플레이 (배치 포함) 검증: {report}");
        File.Delete(tmp);
        // 치트 종료 명령도 리플레이로 재현
        var c = World.CreateMissionWorld(MissionDef.Load("supply_escort"), defs);
        World.AttachMissionAi(c, 1, true);
        c.RecordCommands = true;
        for (int i = 0; i < 300; i++) c.Step();
        c.Issue(new MissionEndCommand(1, true));
        c.Step();
        var rep2 = ReplayFile.FromWorld(c, 1);
        check(c.Winner == 1 && c.CheatsUsed && rep2.Verify(defs, out string r2) < 0, $"MissionEndCommand → 즉시 승리 · 리플레이 일치");
    }

    // ---------------------------------------------------------------- 창 없는 미션 실행 (CLI)

    /// <summary>--mission-run <id> [--seconds N] [--seed N] [--verify] [--deploy force:x,y,facing]: 모든 세력 AI 로 끝까지 (또는 N초) 돌리고 결과 json 을 쓴다.</summary>
    /// <summary>재배치된 옛 1-1 회령 (Q10, 사슬에는 남음): 읽기 · 300틱 실행 · 400 마적 · 캠페인 상태 왕복 저장.</summary>
    static void CampaignChecks(Defs defs, Action<bool, string> check)
    {
        var d = MissionDef.Load(CampaignState.Hoeryong);
        var w = World.CreateMissionWorld(d, defs);
        w.Fog.Enabled = false;
        World.AttachMissionAi(w, d.Player, true);
        for (int i = 0; i < 300; i++) w.Step();
        int bandits = d.Forces.Concat(d.Reinforcements).Where(f => f.Faction == 2).Sum(f => f.AllCompanies().Sum(c => c.Units.Values.Sum()));
        check(w.Tick == 300 && w.Winner == 0 && w.Mission!.Result == MissionState.Running, "캠페인 1-1: 300틱 실행, 진행 중");
        check(bandits == 400 && d.Objectives.Count(o => o.Primary) == 1 && d.Objectives.Count == 2, $"캠페인 1-1: 마적 {bandits} (400), 주 목표 1 · 부 목표 1");
        var r = MissionResults.From(w);
        r.Result = "won";
        r.Survivors.Add(new MissionResults.UnitLine { Id = 7, Unit = "officer", Name = "장교", Kills = 3, OfficerRank = 2, Xp = 55, Post = "company" });
        var s = CampaignState.Apply(null, r);
        check(s != null && s.Completed.Contains(CampaignState.Hoeryong) && s.NextMission == "c1_02_tumen" && s.NextAvailable && s.Officers.Count >= 1 && s.Units.Count > 0, "캠페인 상태: 승리 → 이월 병력 · 다음 미션 1-2 (열림)");
        check(CampaignState.Apply(null, new MissionResults { MissionId = "village_defense", Result = "won" }) == null && CampaignState.Apply(null, new MissionResults { MissionId = CampaignState.Hoeryong, Result = "lost" }) == null, "캠페인 상태: 패배 · 캠페인 밖 미션은 저장 안 함");
        string dir = TestTemp.Child("campaign");
        string? old = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", dir);
        try
        {
            check(CampaignState.Load() == null, "캠페인 저장 없음 → Load null");
            bool saved = s!.Save();
            var l = CampaignState.Load();
            check(saved && l != null && l.ToJson() == s.ToJson() && l.Officers.Any(u => u.Xp == 55 && u.OfficerRank == 2 && u.Post == "company") && l.Carry().Count() == s.Units.Count + s.Officers.Count, "캠페인 상태 왕복 (저장 → 읽기 동일, 장교 계급 · 경험치 · 직책 보존)");
            CampaignState.Reset();
            check(CampaignState.Load() == null, "캠페인 초기화");
        }
        finally { Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", old); }
    }

    /// <summary>챕터 0 튜토리얼 (c0_01_bandits · c0_02_yalu): 규모 · 안개 · 퇴각/승리 · 이벤트 2개 · 산채 파괴 · 패배 · 단독 로드 스모크.</summary>
    static void TutorialChecks(Defs defs, Action<bool, string> check)
    {
        int Alive(World w, string id) => Force(w, id).Count(u => u.Hp > 0);
        // ---- 0-1: 아군 90 vs 마적 30 (소총 10 · 잡병 20)
        {
            var d = MissionDef.Load("c0_01_bandits");
            var w = World.CreateMissionWorld(d, defs);
            check(w.Units.Count(u => u.Owner == 1) == 90 && w.Units.Count(u => u.Owner == 2) == 30
                && w.Units.Count(u => u.Owner == 2 && u.Def.Key == "bandit_rifleman") == 10 && w.Units.Count(u => u.Owner == 2 && u.Def.Key == "bandit_thug") == 20,
                "0-1: 아군 90 vs 마적 30 (소총 10 · 잡병 20)");
            check(d.Factions[1].AiWithdrawAt is 0.5f && d.Failures.Any(f => f.Type == "losses" && f.Percent == 50), "0-1: 마적 철수 문턱 50% · 패배 = 아군 50%");
            // 마적 14명 사망 = 아직 (46.7%), 15명 = 승리
            var bd = Force(w, "bd");
            for (int i = 0; i < 14; i++) bd[i].Hp = 0;
            Steps(w, 0.3f);
            check(w.Winner == 0, "0-1: 마적 14/30 사망 → 아직 진행");
            bd[14].Hp = 0;
            Steps(w, 0.3f);
            check(w.Mission!.Result == MissionState.Won && w.Winner == 1, "0-1: 마적 15/30 (50%) 사망 → 자동 승리");
            var wl = World.CreateMissionWorld(d, defs);
            var kr = Force(wl, "kr");
            for (int i = 0; i < 44; i++) kr[i].Hp = 0;
            Steps(wl, 0.3f);
            check(wl.Winner == 0, "0-1: 아군 44/90 사망 → 아직");
            kr[44].Hp = 0;
            Steps(wl, 0.3f);
            check(wl.Mission!.Result == MissionState.Lost && wl.Winner == 2, "0-1: 아군 45/90 (50%) 쓰러짐 → 패배");
            // 단독 로드 스모크 (아군 무명령 + 마적 AI): 30초
            var ws = World.CreateMissionWorld(d, defs);
            World.AttachMissionAi(ws, d.Player, false);
            Steps(ws, 30);
            check(ws.Tick > 0 && ws.Winner == 0 && ws.Units.Count(u => u.Owner == 1) == 90, "0-1: 단독 로드 30초 스모크 (예외 없음, 아군 90)");
        }
        // ---- 0-2: 약 600명, 다리 여러 개, 초소 1곳만 보임, 이벤트 2개, 산채 파괴 = 승리, 사상 50% = 패배
        {
            var d = MissionDef.Load("c0_02_yalu");
            var w = World.CreateMissionWorld(d, defs);
            int mine = w.Units.Count(u => u.Owner == 1), foes = w.Units.Count(u => u.Owner == 2);
            check(mine >= 560 && mine <= 620 && foes == 221, $"0-2: 아군 {mine} (약 600), 마적 {foes} (소총 150 · 잡병 70 · 두목 1)");
            check(w.Units.Count(u => u.Owner == 1 && u.Def.Key == "infantry_default_old") == 168 && w.Units.Count(u => u.Owner == 1 && u.Def.Key == "heavy_maxim") == 4
                && w.Units.Count(u => u.Owner == 1 && u.Def.Key == "heavy_infantry_gun") == 2 && w.Units.Count(u => u.Owner == 1 && u.Def.Key == "cavalry_default") == 40,
                "0-2: 구식 지방대 168 · 맥심 4 · 보병포 2 · 기병 40");
            var rv = d.Reinforcements.ToDictionary(r => r.Id);
            check(rv["bandit_cav"].AllCompanies().Sum(c => c.Units.Values.Sum()) == 30 && rv["volunteers"].AllCompanies().Sum(c => c.Units.Values.Sum()) == 40 && rv["volunteers"].Faction == 1
                && rv["volunteers"].AllCompanies().All(c => c.Units.Keys.All(k => k == "common_gando_volunteer")), "0-2: 이벤트 증원 = 마적 기병 30 · 서간도 의용군 40 (아군)");
            // 다리: 강 (y 100~107) 을 건너는 흙 띠 4개
            int bridges = 0;
            foreach (int bx in new[] { 30, 90, 150, 210 })
                if (Enumerable.Range(100, 8).All(y => w.Map.Get(bx, y) != TerrainType.Water)) bridges++;
            int water = Enumerable.Range(0, 240).Count(x => w.Map.Get(x, 103) == TerrainType.Water);
            check(bridges == 4 && water > 150 && w.Map.Get(60, 103) == TerrainType.Water, $"0-2: 다리 {bridges}개 · 강 폭 확인 (물 {water}칸)");
            // 안개: 처음에 보이는 마적 = 초소 1 뿐
            w.Fog.Enabled = true;
            for (int i = 0; i < 20; i++) w.Step();
            var seen = new HashSet<string>();
            foreach (var f in d.Forces.Where(f => f.Faction == 2))
                if (Force(w, f.Id).Any(u => w.Fog.CanSee(1, u))) seen.Add(f.Id);
            check(seen.Count == 1 && seen.Contains("o1"), $"0-2: 시작 시 보이는 마적 = {string.Join(",", seen)} (초소 1 만)");
            // 이벤트 2: 둘째 초소 구역 진입 → 의용군 40 합류
            int before = w.Units.Count(u => u.Owner == 1);
            var scouts = Force(w, "b1_scout");
            Teleport(scouts, new Vector2(150, 90));
            Steps(w, 0.5f);
            check(w.Units.Count(u => u.Owner == 1) == before + 40 && w.Mission!.EventFired.Count(x => x) >= 1, "0-2: 둘째 초소 구역 진입 → 서간도 의용군 40 합류");
            // 이벤트 1: 초소 1 · 2 격파 → 기병 30 출몰 (초소 1 만으로는 안 나옴)
            Check1(w, check, "o1", "o2");
            // 산채 파괴 = 승리
            foreach (var u in Force(w, "sanchae")) u.Hp = 0;
            Steps(w, 0.3f);
            check(w.Mission!.Result == MissionState.Won && w.Winner == 1, "0-2: 산채 수비대 전멸 (파괴) → 승리");
            // 패배: 아군 사상 50%
            var wl = World.CreateMissionWorld(d, defs);
            var ours = wl.Units.Where(u => u.Owner == 1).ToList();
            for (int i = 0; i < ours.Count / 2 - 1; i++) ours[i].Hp = 0;
            Steps(wl, 0.3f);
            check(wl.Winner == 0, "0-2: 아군 50% 미만 사상 → 진행");
            for (int i = 0; i < 3; i++) ours[ours.Count / 2 + i].Hp = 0;
            Steps(wl, 0.3f);
            check(wl.Mission!.Result == MissionState.Lost && wl.Winner == 2, "0-2: 아군 사상 50% → 패배");
            // 단독 로드 스모크 (예외 없음)
            var ws = World.CreateMissionWorld(d, defs);
            World.AttachMissionAi(ws, d.Player, false);
            Steps(ws, 20);
            check(ws.Tick > 0 && ws.Winner == 0, "0-2: 단독 로드 20초 스모크 (예외 없음)");
        }
    }

    static void Check1(World w, Action<bool, string> check, string first, string second)
    {
        int cavBefore = w.Units.Count(u => u.Owner == 2 && u.Def.Key == "bandit_horseman");
        foreach (var id in w.Mission!.ForceUnits[first].Select(i => w.Find(i)).OfType<Unit>()) id.Hp = 0;
        Steps(w, 0.3f);
        check(w.Units.Count(u => u.Owner == 2 && u.Def.Key == "bandit_horseman") == cavBefore, "0-2: 초소 1 만 격파 → 기병 아직 안 나옴");
        foreach (var u in w.Mission.ForceUnits[second].Select(i => w.Find(i)).OfType<Unit>()) u.Hp = 0;
        Steps(w, 0.3f);
        check(w.Units.Count(u => u.Owner == 2 && u.Def.Key == "bandit_horseman") == cavBefore + 30 && w.Winner == 0, "0-2: 초소 1 · 2 격파 → 마적 기병 30 출몰");
    }

    /// <summary>T09 캠페인 수직 슬라이스 0-1 → 0-2 → 1-1 (압록강 도하): 미션 선택 → 편성 (Pending) → 전투 → 결과 (정산 · 손실 → 중대) → 보충 (치료 · 신병) 한 사이클,
    /// 저장 원자성 (출격 직전 저장엔 자원 불변 · 패배는 저장 안 함 · 임시 파일 안 남음), D10 (재승리 정기 보급 없음) · D11 (패배 비용 없음), 지난 미션 재승리 시 진행 유지, 0-2 → 1-1 이월.</summary>
    static void SliceChecks(Defs defs, Action<bool, string> check)
    {
        string dir = TestTemp.Child("campaign_slice");
        string? old = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", dir);
        try
        {
            CampaignState.Reset();
            var s0 = new CampaignState();
            check(CampaignState.FirstMission == "c0_01_bandits" && CampaignState.IsCampaignMission("c0_02_yalu") && CampaignState.IsCampaignMission(CampaignState.YaluCrossing)
                && CampaignState.IsCampaignMission(CampaignState.Hoeryong) && File.Exists(MissionDef.PathOf(CampaignState.YaluCrossing)), "슬라이스: 사슬 0-1 → 0-2 → 1-1 (회령 · 두만강은 재배치, 사슬에 남음)");

            // 1) 편성: 0-1 에 전 중대 출격 → 저장 (출격 직전 저장: 자원 불변, Pending 만)
            var ids = s0.Roster.Where(c => c.Available).Select(c => c.Id).ToList();
            CampaignEconomy.BeginOperation(s0, CampaignState.FirstMission, ids);
            float cost = s0.Pending!.SupplySpent;
            check(s0.Save() && !File.Exists(CampaignState.FilePath + ".tmp"), "슬라이스 저장: 임시 파일에 쓰고 바꿔치기 (임시 파일 안 남음)");
            var onDisk = CampaignState.Load()!;
            check(onDisk.Supply == 3000f && onDisk.Pending?.MissionId == CampaignState.FirstMission && cost == 240f, $"출격 직전 저장: 보급 그대로 3000, Pending 만 (비용 {cost})");

            // 2) 전투: 0-1 을 Pending 반영 텍스트로 읽어 300틱
            var d1 = MissionDef.Load(CampaignState.FirstMission);
            var w1 = World.CreateMissionWorld(d1, defs);
            World.AttachMissionAi(w1, d1.Player, true);
            for (int i = 0; i < 300; i++) w1.Step();
            check(w1.Mission!.Result == MissionState.Running && w1.Units.Count(u => u.Owner == 1) == 90, "0-1: 편성 반영 텍스트로 생성 · 300틱 진행 (한국 90)");

            // 3) 패배 = 저장 안 함 (D11 비용 없음)
            var rLost = MissionResults.From(w1); rLost.Result = "lost";
            check(CampaignState.Apply(CampaignState.Load(), rLost) == null && CampaignState.Load()!.Supply == 3000f, "패배: 정산 없음 · 저장 파일 그대로 (D11)");

            // 4) 결과: 승리 (아군 손실 22/90) → 정산 · 손실은 출격 중대에 비율로
            var r1 = MissionResults.From(w1); r1.Result = "won";
            var me = r1.Factions.First(f => f.Player); me.Spawned = 90; me.Lost = 22; me.Alive = 68;
            var s1 = CampaignState.Apply(CampaignState.Load(), r1)!;
            var c1 = s1.Roster.First(c => c.Id == "b1_c1"); var mg = s1.Roster.First(c => c.Id == "mg");
            var db = s1.LastDebrief!;
            check(c1.Strength == 45 && c1.Wounded == 4 && mg.Strength == 23 && mg.Wounded == 2 && db.TotalLost == 52 && db.WoundedReturned == 14 && db.Dead == 38,
                $"정산 손실: 22/90 비율 → 중대 60 → 45 (부상 4), 기관총 30 → 23 (부상 2), 보고 52 = 전사 38 + 부상 14 (실제 {c1.Strength}/{c1.Wounded} · {mg.Strength}/{mg.Wounded} · {db.TotalLost})");
            check(s1.Pending == null && MathF.Abs(s1.Supply - (3000f - cost + db.LootedSupply + 1500f)) < 0.01f && db.RegularSupplyAdded == 1500f && s1.NextMission == "c0_02_yalu" && s1.NextAvailable,
                $"정산 보급: 3000 - 출격 {cost} + 노획 + 정기 1500 (첫 승리) = {s1.Supply:0.0}, 다음 0-2 열림");
            check(s1.Save(), "결과 저장");

            // 5) 보충: 치료 → 신병 배치로 정원 회복
            int healed = CampaignEconomy.HealWounded(s1);
            int recruits0 = s1.Manpower.Recruits, assigned = 0;
            foreach (var c in s1.Roster) assigned += CampaignEconomy.AssignRecruits(s1, c.Id, 100);
            check(healed == 14 && assigned == 38 && s1.Roster.All(c => c.Strength == c.MaxStrength && c.Wounded == 0) && s1.Manpower.Recruits == recruits0 - 38,
                $"보충: 부상 14 치료 · 신병 38 배치 → 전 중대 정원 (치료 {healed}, 배치 {assigned})");
            check(s1.Save() && CampaignState.Load()!.ToJson() == s1.ToJson(), "보충 후 저장 왕복 동일");

            // 6) 같은 미션 재승리 (다시 하기): 정기 보급 · 신병 없음 (D10), 진행 그대로
            var sR = CampaignState.Load()!;
            CampaignEconomy.BeginOperation(sR, CampaignState.FirstMission, ids);
            var sR2 = CampaignState.Apply(sR, r1)!;
            check(sR2.LastDebrief!.RegularSupplyAdded == 0f && sR2.LastDebrief.RecruitsAdded == 0 && sR2.NextMission == "c0_02_yalu", "재승리: 정기 보급 · 신병 0 (D10), 다음 미션 그대로");

            // 7) 0-2 승리 → 1-1 열림, 생존 소총병 이월
            var r2 = new MissionResults { MissionId = "c0_02_yalu", Title = "0-2", Result = "won" };
            r2.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 600, Alive = 600 });
            int uid = 1;
            for (int i = 0; i < 90; i++) r2.Survivors.Add(new MissionResults.UnitLine { Id = uid++, Unit = "infantry", Rank = i < 30 ? 3 : 2, Force = "b1" });
            r2.Survivors.Add(new MissionResults.UnitLine { Id = uid++, Unit = "officer", OfficerRank = 3, Xp = 80, Post = "company", Force = "b1" });
            var s2 = CampaignState.Apply(s1, r2)!;
            check(s2.NextMission == CampaignState.YaluCrossing && s2.NextAvailable && s2.NextName == "1-1 압록강 도하" && s2.Units.Count == 90, "0-2 승리: 다음 1-1 압록강 도하 (열림), 이월 소총병 90");
            check(s2.Save(), "0-2 결과 저장");

            // 8) 지난 미션 (0-1) 재승리: 진행 · 이월 병력은 뒤로 감기지 않음
            var s3 = CampaignState.Apply(CampaignState.Load(), r1)!;
            check(s3.NextMission == CampaignState.YaluCrossing && s3.Units.Count == 90 && s3.EntryMission == "c0_02_yalu", "지난 미션 재승리: 다음 미션 · 이월 병력 유지");

            // 9) 1-1 편성: 제3중대 빼고 출격 → 미션 텍스트에 이월 + 중대 수 반영 (결정적)
            var s4 = CampaignState.Load()!;
            CampaignEconomy.BeginOperation(s4, CampaignState.YaluCrossing, new[] { "b1_c1", "b1_c2", "mg" });
            check(s4.Save(), "1-1 출격 저장");
            var d3 = MissionDef.Load(CampaignState.YaluCrossing);
            var w3 = World.CreateMissionWorld(d3, defs);
            int r3k = RankNames.Snap("korean", 3);
            var inf = w3.Units.Where(u => u.Owner == 1 && u.Def.Key == "infantry").ToList();
            check(d3.Carry && d3.Json.Contains("infantryRanks") && inf.Count == 120 && inf.Count(u => u.Rank >= r3k) >= 30 && w3.Units.Any(u => u.Owner == 1 && u.IsOfficer && u.OfficerXp == 80),
                $"1-1: 출격 2개 중대 (소총 {inf.Count} = 120), 0-2 고참 {inf.Count(u => u.Rank >= r3k)} (>=30) · 장교 경험치 80 이월");
            var w3b = World.CreateMissionWorld(MissionDef.Load(CampaignState.YaluCrossing), defs);
            check(w3b.Units.Select(u => (u.Def.Key, u.Rank, u.OfficerXp)).SequenceEqual(w3.Units.Select(u => (u.Def.Key, u.Rank, u.OfficerXp))), "1-1: 같은 캠페인 상태 → 같은 편성 (결정적)");
            World.AttachMissionAi(w3, d3.Player, true);
            for (int i = 0; i < 300; i++) w3.Step();
            int enemy = d3.Forces.Concat(d3.Reinforcements).Where(f => f.Faction != d3.Player).Sum(f => f.AllCompanies().Sum(c => c.Units.Values.Sum()));
            check(w3.Mission!.Result == MissionState.Running && enemy is >= 650 and <= 800 && d3.Factions.Count == 3, $"1-1: 300틱 진행, 적 {enemy} (마적 + 봉천 경비대)");

            // 10) 1-1 승리 → 다음 1-2 준비 중 (슬라이스 끝)
            var r4 = new MissionResults { MissionId = CampaignState.YaluCrossing, Title = "1-1", Result = "won" };
            r4.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 300, Lost = 30, Alive = 270 });
            var s5 = CampaignState.Apply(s4, r4)!;
            check(s5.NextMission == "c1_02" && !s5.NextAvailable && s5.NextName == "1-2" && new[] { "c0_01_bandits", "c0_02_yalu", CampaignState.YaluCrossing }.All(s5.Completed.Contains)
                && s5.Roster.First(c => c.Id == "b1_c3").Strength == 60 && s5.Roster.First(c => c.Id == "b1_c1").Strength == 54, "1-1 승리: 0-1 · 0-2 · 1-1 완료, 다음 1-2 준비 중, 손실은 출격 중대만 (제3중대 그대로)");
        }
        finally { CampaignState.Reset(); Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", old); }
    }

    /// <summary>캠페인 1-2 두만강 도하: 읽기 · 300틱 · 적 규모 · 이월 병력 왕복 (1-1 결과 → 1-2 편성) · 곡사포 편입 · 1-3 준비 중.</summary>
    static void Campaign2Checks(Defs defs, Action<bool, string> check)
    {
        const string Id = "c1_02_tumen";
        string dir = TestTemp.Child("campaign2");
        string? old = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", dir);
        try
        {
            CampaignState.Reset();
            var d = MissionDef.Load(Id);
            var w = World.CreateMissionWorld(d, defs);
            w.Fog.Enabled = false;
            World.AttachMissionAi(w, d.Player, true);
            for (int i = 0; i < 300; i++) w.Step();
            int enemy = d.Forces.Concat(d.Reinforcements).Where(f => f.Faction != d.Player).Sum(f => f.AllCompanies().Sum(c => c.Units.Values.Sum()));
            check(w.Tick == 300 && w.Winner == 0 && w.Mission!.Result == MissionState.Running, "캠페인 1-2: 300틱 실행, 진행 중");
            check(enemy is >= 650 and <= 800 && d.Objectives.Count(o => o.Primary) == 1 && d.Objectives.Count == 3 && d.Factions.Count == 3 && d.Carry, $"캠페인 1-2: 적 {enemy} (650~800), 주 목표 1 · 부 목표 2 (여울), 세력 3, carry");
            check(d.Forces.Any(f => f.Id == "how" && f.Units!.GetValueOrDefault("heavy_howitzer") == 4) && d.Forces.Any(f => f.Id == "obs"), "캠페인 1-2: 곡사포 중대 · 관측반 있음");
            var plain = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "infantry").ToList();
            check(plain.Count == 180, "이월 없음: 보충병만 180명");

            // 1-1 결과 → 캠페인 상태 → 1-2 편성
            var r = new MissionResults { MissionId = CampaignState.Hoeryong, Title = "1-1", Result = "won" };
            r.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true });
            int id = 1;
            for (int i = 0; i < 100; i++) r.Survivors.Add(new MissionResults.UnitLine { Id = id++, Unit = "infantry", Rank = i < 20 ? 3 : 2, Kills = i < 20 ? 6 : 2, Force = i % 2 == 0 ? "c1" : "c2" });
            r.Survivors.Add(new MissionResults.UnitLine { Id = id++, Unit = "heavy_maxim", Rank = 1, Force = "mg" });
            r.Survivors.Add(new MissionResults.UnitLine { Id = id++, Unit = "officer", OfficerRank = 3, Xp = 70, Post = "company", Force = "c1" });
            r.Survivors.Add(new MissionResults.UnitLine { Id = id++, Unit = "officer", OfficerRank = 2, Xp = 40, Post = "company", Force = "c2" });
            var s = CampaignState.Apply(null, r)!;
            check(s.Save(), "1-1 결과 저장");
            var d2 = MissionDef.Load(Id);
            var w2 = World.CreateMissionWorld(d2, defs);
            var inf = w2.Units.Where(u => u.Owner == 1 && u.Def.Key == "infantry").ToList();
            int r3 = RankNames.Snap("korean", 3), r2 = RankNames.Snap("korean", 2), r1 = RankNames.Snap("korean", 1);
            check(inf.Count == 180 && inf.Count(u => u.Rank >= r3) >= 20 && inf.Count(u => u.Rank == r1) <= 80,
                $"이월 → 1-2: 1-1 생존 소총병 100명 (계급 {r3} x20 · {r2} x80) 이 대대에 들어오고 나머지 80명은 보충병 (고참 {inf.Count(u => u.Rank >= r3)} · 이등병 {inf.Count(u => u.Rank == r1)})");
            var offs = w2.Units.Where(u => u.Owner == 1 && u.IsOfficer && u.Def.Key == "officer").OrderByDescending(u => u.OfficerXp).ToList();
            check(offs.Count >= 2 && offs[0].OfficerXp == 70 && offs[0].OfficerBase == 3 && offs[1].OfficerXp == 40 && offs[1].OfficerBase == 2, "이월 장교: 계급 · 경험치 유지 (3급 70xp · 2급 40xp)");
            check(d2.Json.Contains("infantryRanks") && d2.Hash != d.Hash && MissionDef.Parse(d2.Json).Hash == d2.Hash, "이월 병력은 미션 텍스트에 써 넣어짐 (스냅샷 · 리플레이가 같은 텍스트)");
            check(w2.Units.Count(u => u.Owner == 1 && u.Def.Key == "heavy_howitzer") == 4, "1-2: 곡사포 4문");
            var w3 = World.CreateMissionWorld(MissionDef.Load(Id), defs);
            check(w3.Units.Select(u => (u.Def.Key, u.Rank, u.OfficerXp)).SequenceEqual(w2.Units.Select(u => (u.Def.Key, u.Rank, u.OfficerXp))), "같은 이월 상태 → 같은 편성 (결정적)");

            // 1-2 승리 → 곡사포 편입 · 1-3 준비 중 · 다시 하기는 같은 이월 병력
            var r12 = new MissionResults { MissionId = Id, Title = "1-2", Result = "won" };
            r12.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true });
            r12.Survivors.Add(new MissionResults.UnitLine { Id = 1, Unit = "infantry", Rank = 4 });
            var s2 = CampaignState.Apply(s, r12)!;
            check(s2.HowitzerCompany && s2.Completed.Contains(Id) && s2.NextMission == "c1_03" && !s2.NextAvailable && s2.NextName == "1-3", "1-2 승리: 곡사포 중대 편입 · 다음 1-3 (준비 중)");
            check(s2.Save(), "1-2 결과 저장");
            var w4 = World.CreateMissionWorld(MissionDef.Load(Id), defs);
            check(w4.Units.Count(u => u.Owner == 1 && u.Def.Key == "infantry" && u.Rank >= r3) >= 20 && w4.Units.Any(u => u.Owner == 1 && u.IsOfficer && u.OfficerXp == 70), "1-2 다시 하기: 처음 받은 이월 병력으로 시작 (1-2 생존자로 덮이지 않음)");
        }
        finally { Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", old); }
    }

    /// <summary>
    /// 캠페인 홈 MVP (S1 home-state, S5 home-events):
    /// v1 JSON 호환성 및 v2 기본값 초기화 · CampaignEconomy (비용/치료/전역/신병배치/전투정산/패널티상한) · CampaignEvents (5종 로드/선택지/결정적 분기).
    /// </summary>
    static void CampaignHomeChecks(Defs defs, Action<bool, string> check)
    {
        // 1) v1 JSON 읽기 호환성 및 v2 기본값 초기화
        string v1Json = """
        {
          "Version": 1,
          "CampaignId": "c1",
          "NextMission": "c1_02_tumen",
          "Completed": [ "c1_01_hoeryong" ],
          "History": [],
          "Units": [],
          "Officers": [],
          "HowitzerCompany": false
        }
        """;
        var s1 = CampaignState.FromJson(v1Json);
        check(s1 != null && s1.Version == CampaignState.SchemaVersion, $"v1 JSON 읽기: Version={CampaignState.SchemaVersion} 마이그레이션");
        check(s1!.Supply == 3000f, $"v1 읽기: Supply = 3000 (실제 {s1.Supply})");
        check(s1.Manpower.Recruits == 2000 && s1.Manpower.Wounded == 0, $"v1 읽기: Recruits = 2000, Wounded = 0");
        check(s1.Stock.Rifles.GetValueOrDefault("ru") == 3000, "v1 읽기: Rifles['ru'] = 3000");
        check(s1.Stock.Ammo["ru"] == 100000 && s1.Stock.Ammo["jp"] == 100000 && s1.Stock.Ammo["de"] == 100000 && s1.Stock.Ammo["fr"] == 100000 && s1.Stock.Ammo["jpOld"] == 100000, "v1 읽기: Ammo 5종 각 100,000");
        check(s1.Stock.Shells["infGun"] == 500 && s1.Stock.Shells["field"] == 300 && s1.Stock.Shells["howitzer"] == 150, "v1 읽기: Shells 500/300/150");
        check(s1.Roster.Count >= 4 && s1.Roster.Any(c => c.Id == "b1_c1") && s1.Roster.Any(c => c.Id == "mg"), "v1 읽기: Roster 4개 중대 초기화");
        check(s1.Flags["heir_mood"] == 60 && s1.Flags["cmd_favor"] == 50, "v1 읽기: Flags heir_mood=60, cmd_favor=50");

        // 2) CampaignEconomy: 병종별 투입 비용 계산
        check(CampaignEconomy.UnitDeployCost("infantry") == 1.0f &&
              CampaignEconomy.UnitDeployCost("recruit") == 1.0f &&
              CampaignEconomy.UnitDeployCost("guard") == 1.0f &&
              CampaignEconomy.UnitDeployCost("assault") == 1.5f &&
              CampaignEconomy.UnitDeployCost("sniper") == 1.5f &&
              CampaignEconomy.UnitDeployCost("mg") == 2.0f &&
              CampaignEconomy.UnitDeployCost("cavalry") == 3.0f &&
              CampaignEconomy.UnitDeployCost("infGun") == 5.0f &&
              CampaignEconomy.UnitDeployCost("fieldGun") == 7.0f &&
              CampaignEconomy.UnitDeployCost("howitzer") == 9.0f &&
              CampaignEconomy.UnitDeployCost("armoredCar") == 12.0f &&
              CampaignEconomy.UnitDeployCost("armoredTrain") == 40.0f,
              "CampaignEconomy: 병종별 투입 비용 매핑");
        var cInf = new CampaignState.RosterCompany { Id = "test_inf", Kind = "infantry", Strength = 60 };
        var cMg = new CampaignState.RosterCompany { Id = "test_mg", Kind = "mg", Strength = 30 };
        check(CampaignEconomy.CompanyDeployCost(cInf) == 60f && CampaignEconomy.CompanyDeployCost(cMg) == 60f, "CampaignEconomy: 중대 투입 비용");
        check(CampaignEconomy.TotalDeployCost(new[] { cInf, cMg }, 1.5f) == 180f, "CampaignEconomy: 총 투입 비용 (scaleMul 1.5)");

        var E = CampaignEconomyDefs.Current;   // T29: 경제 숫자는 assets/data/campaign_economy.json 에서 읽는다
        check(E.RegularSupply == 1500f && E.RegularRecruits == 1000 && E.HealCostPerMan == 0.5f && E.HowitzerCompanyStrength == 40, "CampaignEconomyDefs: JSON 값 = 이전 코드 상수 (T29 값 불변)");
        {   // T29b: 경제 JSON 검증 · 폴백 (없음 / 깨짐 / 범위 밖 / 폴더)
            string td = Path.Combine(Path.GetTempPath(), "kd_econ_" + Environment.ProcessId); Directory.CreateDirectory(td);
            try
            {
                var dflt = new CampaignEconomyDefs();
                check(CampaignEconomyDefs.Load(Path.Combine(td, "none.json")).HealCostPerMan == dflt.HealCostPerMan, "경제 JSON 없음 → 기본값");
                string bad = Path.Combine(td, "bad.json"); File.WriteAllText(bad, "{ not json");
                check(CampaignEconomyDefs.Load(bad).RegularSupply == dflt.RegularSupply, "경제 JSON 깨짐 → 기본값");
                check(CampaignEconomyDefs.Load(td).RegularSupply == dflt.RegularSupply, "경제 경로가 폴더(접근 오류) → 기본값");
                string oor = Path.Combine(td, "oor.json");
                File.WriteAllText(oor, "{\"WoundReturnRatio\":5,\"PenaltyCap\":-1,\"HealCostPerMan\":0,\"RecruitAssignCostPerMan\":-3,\"AmmoPerInfantry\":-9,\"AmmoPerMg\":-1,\"RegularRecruits\":-5,\"RegularSupply\":-100,\"LootAmmoPerKill\":-2,\"HowitzerCompanyStrength\":0,\"LargeDeployScaleMul\":0}");
                var o = CampaignEconomyDefs.Load(oor);
                check(o.WoundReturnRatio == 1f && o.PenaltyCap == 0f && o.HealCostPerMan >= 0.01f && o.RecruitAssignCostPerMan >= 0.01f
                    && o.AmmoPerInfantry == 0 && o.AmmoPerMg == 0 && o.RegularRecruits == 0 && o.RegularSupply == 0f && o.LootAmmoPerKill == 0
                    && o.HowitzerCompanyStrength >= 1 && o.LargeDeployScaleMul >= 1f, "경제 JSON 범위 밖 값 → 정상 범위로 자름");
                string oor2 = Path.Combine(td, "oor2.json");   // T19b 새 키: 범위 밖 · null · 모르는 포탄 키
                File.WriteAllText(oor2, "{\"shortLoadRatio\":7,\"shellWeapons\":{\"gun_field\":\"field\",\"bad\":\"nope\",\"nul\":null},\"lootShellsPerKill\":{\"infGun\":-3,\"howitzer\":99999,\"zzz\":1}}");
                var o2 = CampaignEconomyDefs.Load(oor2);
                check(o2.ShortLoadRatio == 1f && o2.ShellWeapons.Count == 1 && o2.ShellWeapons["gun_field"] == "field" && o2.LootShellsPerKill[StockKeys.InfGun] == 0f
                    && o2.LootShellsPerKill[StockKeys.Howitzer] == 1000f && !o2.LootShellsPerKill.ContainsKey("zzz"), "경제 JSON T19b 새 키(포탄 휴대 · 노획) 범위 보정");
                string nul = Path.Combine(td, "nul.json"); File.WriteAllText(nul, "{\"shellWeapons\":null,\"lootShellsPerKill\":null}");
                check(CampaignEconomyDefs.Load(nul).ShellWeapons.Count == dflt.ShellWeapons.Count, "경제 JSON 포탄 사전 null → 기본값");
                check(World.ToPermille(0.5f) == 500 && World.ToPermille(0.333f) == 333 && World.ToPermille(2f) == 1000 && World.ToPermille(-1f) == 0 && World.ToPermille(float.NaN) == 1000
                    && World.ScaleAmmoPermille(7, 500) == 4, "탄약 배율 천분율 정수 변환");
            }
            finally { try { Directory.Delete(td, true); } catch { } }
        }
        // 3) CampaignEconomy: 부상자 치료 (보급 HealCostPerMan 차감)
        var sEco = new CampaignState { Supply = 100f };
        var c1 = sEco.Roster.First(c => c.Id == "b1_c1");
        c1.Strength = 40;
        c1.Wounded = 20;
        c1.UnhealedOps = 2;
        int healed = CampaignEconomy.HealWounded(sEco, 15);
        check(healed == 15 && c1.Strength == 55 && c1.Wounded == 5 && c1.UnhealedOps == 0 && MathF.Abs(sEco.Supply - (100f - 15 * E.HealCostPerMan)) < 1e-3f, "CampaignEconomy: 부상자 치료 (보급 0.5 차감, Strength 복귀, UnhealedOps 리셋)");

        // 4) CampaignEconomy: 미치료 3회 전역 처리
        var sUnheal = new CampaignState();
        var cu = sUnheal.Roster.First(c => c.Id == "b1_c1");
        cu.Wounded = 10;
        cu.UnhealedOps = 2;
        var rTest = new MissionResults { MissionId = "c1_01_hoeryong", Result = "won" };
        rTest.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Lost = 0 });
        CampaignEconomy.SettleBattle(sUnheal, rTest);
        check(cu.Wounded == 0 && cu.UnhealedOps == 0 && sUnheal.LastDebrief?.DischargedWounded == 10, "CampaignEconomy: 미치료 3회 부상자 전역 처리");

        // 5) CampaignEconomy: 신병 배치 (보급 0.5 + 소총 1 차감, 소총 0일 때 배치 불가)
        var sRec = new CampaignState { Supply = 100f };
        sRec.Manpower.Recruits = 50;
        sRec.Stock.Rifles["ru"] = 10;
        var cr = sRec.Roster.First(c => c.Id == "b1_c1");
        cr.Strength = 30; // MaxStrength=60, 여유 30
        int assigned = CampaignEconomy.AssignRecruits(sRec, "b1_c1", 20); // 소총 10정이므로 10명만 배치
        check(assigned == 10 && cr.Strength == 40 && sRec.Stock.Rifles["ru"] == 0 && sRec.Manpower.Recruits == 40 && MathF.Abs(sRec.Supply - (100f - 10 * E.RecruitAssignCostPerMan)) < 1e-3f, "CampaignEconomy: 신병 배치 (소총 1정 + 보급 0.5 차감, 소총 한도 제한)");
        int assignedFail = CampaignEconomy.AssignRecruits(sRec, "b1_c1", 10);
        check(assignedFail == 0 && cr.Strength == 40, "CampaignEconomy: 소총 0일 때 신병 배치 불가 (0 반환)");

        // 6) CampaignEconomy: 전투 정산 (사상자 25% 부상자 복귀, 적 사살 노획, 정기 보급 도착)
        var sBat = new CampaignState
        {
            Supply = 1000f,
            Manpower = new CampaignState.ManpowerState { Recruits = 1000, Wounded = 0 }
        };
        sBat.Stock.Rifles["ru"] = 500;
        sBat.Stock.Ammo["ru"] = 10000;
        var rBat = new MissionResults { MissionId = "c1_01_hoeryong", Result = "won", EnemyKills = 100 };
        rBat.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Lost = 40, Spawned = 100, Alive = 60 });
        rBat.Factions.Add(new MissionResults.FactionLine { Id = 2, Player = false, Name = "마적" });
        CampaignEconomy.SettleBattle(sBat, rBat);
        int wRet = (int)MathF.Round(40 * E.WoundReturnRatio);
        check(sBat.Manpower.Wounded == wRet && sBat.LastDebrief?.WoundedReturned == wRet && sBat.LastDebrief?.Dead == 40 - wRet, "CampaignEconomy: 사상자 25% 부상자 복귀");
        check(MathF.Abs((sBat.LastDebrief?.LootedSupply ?? 0) - 100 * E.LootSupplyPerKill) < 0.01f && sBat.LastDebrief?.LootedRifles == (int)(100 * E.LootRiflesPerKill) && sBat.LastDebrief?.LootedAmmo == 100 * E.LootAmmoPerKill, $"CampaignEconomy: 적 사살 노획 정산 (보급/소총/탄약: sup={sBat.LastDebrief?.LootedSupply}, rif={sBat.LastDebrief?.LootedRifles}, ammo={sBat.LastDebrief?.LootedAmmo})");
        check(sBat.LastDebrief?.RegularSupplyAdded == E.RegularSupply && sBat.LastDebrief?.RecruitsAdded == E.RegularRecruits, "CampaignEconomy: 정기 보급 도착");
        check(sBat.Stock.RecentLog.Count > 0, "CampaignEconomy: RecentLog 정산 기록 추가");

        // 7) CampaignEconomy: 패널티 합산 상한 30% 제한
        var sPen = new CampaignState();
        sPen.Flags["heir_mood"] = 5;
        sPen.Flags["cmd_favor"] = 5;
        sPen.Flags["next_supply_penalty_pct"] = 25;
        var (recPen, supPen) = CampaignEconomy.ComputePenalty(sPen);
        check(recPen <= 0.30f && supPen <= 0.30f && supPen == 0.30f, $"CampaignEconomy: 패널티 상한 30% 제한 (신병: {recPen:0.00}, 보급: {supPen:0.00})");

        // 8) CampaignEvents: 5종 이벤트 로드
        var events = CampaignEvents.Current;
        check(events.Count == 5 &&
              events.Any(e => e.Id == "e1_guard_front") &&
              events.Any(e => e.Id == "e2_credit_steal") &&
              events.Any(e => e.Id == "e3_bleed_order") &&
              events.Any(e => e.Id == "e4_heir_lecture") &&
              events.Any(e => e.Id == "e5_corps_restraint"),
              "CampaignEvents: 5종 이벤트 로드 완료");

        // 9) CampaignEvents: E1(a) 시위대 의무 투입 & E2(c) 남 참장 동맹 플래그
        var sEv1 = new CampaignState();
        CampaignEvents.ApplyChoice(sEv1, "e1_guard_front", 0);
        var guardCo = sEv1.Roster.FirstOrDefault(c => c.Id == "guard");
        check(sEv1.Flags["heir_mood"] == 70 && sEv1.Supply == 3200f && guardCo != null && guardCo.Mandatory && sEv1.Flags["ev_e1_guard_front"] == 1,
              "CampaignEvents: E1(a) 수락 -> 심기+10, 보급+200, 시위대 의무 투입");

        var sEv2 = new CampaignState();
        CampaignEvents.ApplyChoice(sEv2, "e2_credit_steal", 2);
        check(sEv2.Flags.GetValueOrDefault("nam_ally") == 1 && sEv2.Flags["ev_e2_credit_steal"] == 3,
              "CampaignEvents: E2(c) 통보 -> nam_ally=1 플래그");

        // 10) CampaignEvents: 시드 기반 50% 분기 결정성 확인
        var sDet1 = new CampaignState { Seed = 12345 };
        var sDet2 = new CampaignState { Seed = 12345 };
        int roll1a = CampaignEvents.RollDeterministic(sDet1, "e1_guard_front_b");
        int roll2a = CampaignEvents.RollDeterministic(sDet2, "e1_guard_front_b");
        check(roll1a == roll2a, $"CampaignEvents: 동일 시드 동일 분기 결정성 (roll: {roll1a})");
        var sDet3 = new CampaignState { Seed = 98765 };
        int roll3 = CampaignEvents.RollDeterministic(sDet3, "e1_guard_front_b");
        check(roll1a is >= 0 and < 100 && roll3 is >= 0 and < 100, "CampaignEvents: 결정적 난수 0..99 범위");

        // 11) CampaignEvents: E5(a) 군단장 만류 수용
        var sEv5 = new CampaignState();
        CampaignEvents.ApplyChoice(sEv5, "e5_corps_restraint", 0);
        check(sEv5.Supply == 3200f && sEv5.Flags["heir_mood"] == 55, "CampaignEvents: E5(a) 수용 -> 보급+200, 심기-5");

        // 12) Phase 2 S3: PendingOperation (AmmoScale & 출격 중대) 미션 반영 검증
        string s3Dir = TestTemp.Child("campaign_s3");
        string? oldS3Dir = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", s3Dir);
        try
        {
            var sS3 = new CampaignState();
            sS3.Pending = new CampaignState.PendingOperation
            {
                MissionId = CampaignState.Hoeryong,
                DeployedCompanyIds = new() { "b1_c1", "b1_c2" },
                AmmoScale = 0.5f,
            };
            sS3.Save();
            var dS3 = MissionDef.Load(CampaignState.Hoeryong);
            check(dS3.Json.Contains("\"ammoScale\": 0.5"), "S3: PendingOperation ammoScale 미션 JSON 반영");
            var wS3 = World.CreateMissionWorld(dS3, defs);
            var uS3 = wS3.Units.First(u => u.Owner == 1 && u.Def.Category == "infantry");
            check(uS3.Ammo == (int)MathF.Round((uS3.Weapon?.Ammo ?? 0) * 0.5f), $"S3: World 생성 시 유닛 Ammo 절반 감량 ({uS3.Ammo}/{uS3.Weapon?.Ammo})");
        }
        finally
        {
            CampaignState.Reset();
            Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", oldS3Dir);
        }

        // 13) 출격 직전 저장 위험: BeginOperation 은 Pending 에만 기록하고 자원은 결과 확정 (SettleBattle) 때 반영
        var sOp = new CampaignState();
        float supply0 = sOp.Supply; int ammo0 = sOp.Stock.Ammo[StockKeys.Ru];
        CampaignEconomy.BeginOperation(sOp, CampaignState.Hoeryong, new[] { "b1_c1", "b1_c2" });
        check(sOp.Supply == supply0 && sOp.Stock.Ammo[StockKeys.Ru] == ammo0 && sOp.Pending is { SupplySpent: > 0f, AmmoLoaded: > 0 },
            "출격 개시: 자원은 그대로, Pending 에만 비용 · 탄약 기록");
        float cost = sOp.Pending!.SupplySpent; int loaded = sOp.Pending.AmmoLoaded;
        var rOp = new MissionResults { MissionId = CampaignState.Hoeryong, Result = "won" };
        rOp.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 120, Alive = 120 });
        CampaignEconomy.SettleBattle(sOp, rOp);
        check(sOp.Pending == null && MathF.Abs(sOp.Supply - (supply0 - cost + E.RegularSupply)) < 0.01f && sOp.Stock.Ammo[StockKeys.Ru] == ammo0 && sOp.LastDebrief?.AmmoRefunded == loaded,
            $"전투 정산: 비용 차감 후 정기 보급, 생존 100% 라 탄약 전량 환원 (보급 {sOp.Supply})");
        var sStale = new CampaignState();
        sStale.Pending = new CampaignState.PendingOperation { MissionId = "other", SupplySpent = 999f, DeployedCompanyIds = new() { "b1_c1" } };
        CampaignEconomy.SettleBattle(sStale, rOp);
        check(sStale.Pending == null && sStale.Supply == 3000f + E.RegularSupply, "전투 정산: 다른 미션의 묵은 Pending 은 비용 없이 버림");

        // 14) 출격 검증 · 군수 계산 (투입 비용 · 상한 · 탄약 부족)
        var sCap = new CampaignState();
        CampaignEconomy.BeginOperation(sCap, "m", new[] { "b1_c1", "b1_c2", "b1_c3", "mg" }, maxDeploy: 2);
        check(sCap.Pending!.SupplySpent == (180f + 60f) * E.LargeDeployScaleMul, $"출격 비용: 상한 지정 미션에서 180명 초과 = x1.5 ({sCap.Pending.SupplySpent})");
        var sLow = new CampaignState();
        sLow.Stock.Ammo[StockKeys.Ru] = 3600;   // 60명 x 60발 = 3600 필요 → 절반만 있으면 scale 0.5
        sLow.Stock.Ammo[StockKeys.Ru] = 1800;
        CampaignEconomy.BeginOperation(sLow, "m", new[] { "b1_c1" });
        check(MathF.Abs(sLow.Pending!.AmmoScale - 0.5f) < 0.001f && sLow.Pending.AmmoLoaded == 1800, "출격 탄약 부족: AmmoScale = 보유 / 필요");
        var sMand = new CampaignState();
        sMand.Roster.First(c => c.Id == "b1_c1").Mandatory = true;
        CampaignEconomy.BeginOperation(sMand, "m", new[] { "b1_c2" });
        check(sMand.Pending!.RefusedMandatory && sMand.Flags["heir_mood"] == 60, "의무 중대 제외: 출격 때는 심기 불변, 기록만");
        sMand.Pending.MissionId = CampaignState.Hoeryong;
        CampaignEconomy.SettleBattle(sMand, rOp);
        check(sMand.Flags["heir_mood"] == 50 && sMand.Flags["cmd_favor"] == 45, "의무 중대 제외: 결과 확정 때 심기 -10 · 총사령관 -5");

        // 14b) D23: 이미 이긴 미션의 반복 승리는 결과만 기록, 자원 · 병력 불변
        var sRep = CampaignState.Apply(null, new MissionResults { MissionId = CampaignState.FirstMission, Result = "won" })!;
        float repSupply = sRep.Supply; int repRecruits = sRep.Manpower.Recruits; int repWounded = sRep.Manpower.Wounded;
        var rRep = new MissionResults { MissionId = CampaignState.FirstMission, Result = "won", EnemyKills = 100 };
        rRep.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Lost = 40, Spawned = 100, Alive = 60 });
        var sRep2 = CampaignState.Apply(sRep, rRep)!;
        check(sRep2.Supply == repSupply && sRep2.Manpower.Recruits == repRecruits && sRep2.Manpower.Wounded == repWounded && sRep2.LastDebrief?.RegularSupplyAdded == 0f && sRep2.LastDebrief?.EnemyKills == 100 && sRep2.Pending == null,
            "D23: 반복 승리는 정산(보급 · 신병 · 노획 · 부상) 없이 결과만 기록");

        // 15) 저장 보정 (Sanitize) · 상수 키
        var sBad = CampaignState.FromJson("{\"Version\":2,\"Supply\":-50,\"Manpower\":{\"Recruits\":99999},\"Roster\":[{\"Id\":\"x\",\"Strength\":500,\"MaxStrength\":60}],\"Stock\":{\"Ammo\":{\"ru\":-5}}}")!;
        check(sBad.Supply == 0f && sBad.Manpower.Recruits == CampaignState.ManpowerState.MaxRecruits && sBad.Roster[0].Strength == 60 && sBad.Stock.Ammo[StockKeys.Ru] == 0 && sBad.Stock.Ammo.ContainsKey(StockKeys.Jp),
            "캠페인 저장 보정: 음수 · 상한 초과 값 정리, 빠진 키는 기본값");

        // 16) 도감 필터 (정적 Filter)
        var allUnits = defs.Units.Values.ToList();
        check(UI.CodexMenu.Filter(allUnits, UI.CodexMenu.FactionFilter.All, UI.CodexMenu.CategoryFilter.All).Count == allUnits.Count, "도감 필터: 전체 = 전부");
        var cavs = UI.CodexMenu.Filter(allUnits, UI.CodexMenu.FactionFilter.All, UI.CodexMenu.CategoryFilter.Cavalry);
        check(cavs.All(u => u.Mounted) && cavs.Count == allUnits.Count(u => u.Mounted), "도감 필터: 기병 = Mounted 만");
        var infs = UI.CodexMenu.Filter(allUnits, UI.CodexMenu.FactionFilter.All, UI.CodexMenu.CategoryFilter.Infantry);
        check(infs.All(u => !u.Mounted && !u.IsArmored) && !infs.Any(cavs.Contains), "도감 필터: 보병 · 기병 겹치지 않음");
    }

    public static int RunCli(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { }
        var defs = Defs.Load();
        string id = args[1];
        float seconds = 0;
        int? seed = null;
        bool verify = false;
        var deploy = new List<DeployEntry>();
        for (int i = 2; i < args.Length; i++)
            switch (args[i])
            {
                case "--seconds": seconds = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--seed": seed = int.Parse(args[++i]); break;
                case "--verify": verify = true; break;
                case "--deploy":
                    var p = args[++i].Split(':', ',');
                    deploy.Add(new DeployEntry(p[0], new Vector2(float.Parse(p[1]), float.Parse(p[2])), p.Length > 3 ? float.Parse(p[3]) : 0));
                    break;
            }
        var def = MissionDef.Load(id);
        if (seconds <= 0) seconds = (KoreanDivision.UI.MissionText.TimeLimit(def) is float tl and > 0 ? tl : 1800) + 30;
        var w = World.CreateMissionWorld(def, defs, seed, deploy);
        World.AttachMissionAi(w, def.Player, true);
        w.RecordCommands = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool meas = Environment.GetEnvironmentVariable("KD_MEAS") == "1";   // N21 측정: MeasX 한 줄 보고 (돌격 사격 사용량 포함)
        if (meas) MeasX.Init(w);
        long max = (long)(seconds / Config.SimDt);
        bool dbg = Environment.GetEnvironmentVariable("KD_TAC_DBG") == "1";
        int dbgEvery = int.TryParse(Environment.GetEnvironmentVariable("KD_TAC_DBG_SEC"), out int dsec) && dsec > 0 ? (int)(dsec / Config.SimDt) : 1800;   // 디버그 간격 (초)
        var trenchPeak = new Dictionary<int, int>();   // 세력별 동시 참호 점유 최대 (참호 자동 점유 관찰)
        while (w.Winner == 0 && w.Tick < max)
        {
            w.Step();
            if (meas) MeasX.Tick(w);
            if (w.Tick % 60 == 0)
                foreach (var g in w.Units.Where(u => u.InTrench).GroupBy(u => u.Owner))
                    trenchPeak[g.Key] = Math.Max(trenchPeak.GetValueOrDefault(g.Key), g.Count());
            if (dbg && w.Tick % dbgEvery == 0)
                foreach (var (f, ai) in w.Controllers)
                    if (ai is TacticalAi ta && ta.StateOf(f) is { } ts)
                        Console.WriteLine($"   tac{f} t={w.Time:0} " + TacticalAi.Describe(ts) + "\n     비무장:" + string.Concat(w.Units.Where(u => u.Owner == f && u.Weapon == null).Select(u => $" {u.Id}@({u.Pos.X:0},{u.Pos.Y:0}){u.Order}")));
        }
        if (meas) MeasX.Report(w);
        var m = w.Mission!;
        var res = MissionResults.From(w);
        string? path = res.Save();
        Console.WriteLine($"[mission-run] {def.Id} '{def.Title}': {res.Result} ({(m.FailReason >= 0 ? def.Failures[m.FailReason].Type : "-")}) at {res.DurationSeconds:0.0}s, ticks {w.Tick}, {sw.Elapsed.TotalSeconds:0.0}s wall");
        for (int i = 0; i < def.Objectives.Count; i++)
            Console.WriteLine($"  objective {def.Objectives[i].Id} ({def.Objectives[i].Type}{(def.Objectives[i].Primary ? ", primary" : "")}): {res.Objectives[i].State} progress {m.ObjProgress[i]:0.0}");
        foreach (var f in res.Factions) Console.WriteLine($"  faction {f.Id} {f.Name}: spawned {f.Spawned}, lost {f.Lost}, alive {f.Alive}, trench peak {trenchPeak.GetValueOrDefault(f.Id)} (now {w.Units.Count(u => u.Owner == f.Id && u.InTrench)})");
        foreach (var (fid, ai) in w.Controllers)   // 전술 AI 예비 · 역습 누계 (3B 측정, 읽기 전용)
            if (ai is TacticalAi ta && ta.StateOf(fid) is { } ts)
                Console.WriteLine($"  tac {fid}: reserve {ts.ReserveCommits} counter {ts.Counterattacks} fallbacks {ts.Fallbacks} ammoRot {ts.AmmoRotations} plug {ts.PlugGaps}/{ts.PlugBreaches}/{ts.PlugFlanks} redesig {ts.ReserveRedesignations} fixPush {ts.FixPushes}");
        Console.WriteLine($"  events fired {m.EventFired.Count(x => x)}/{m.EventFired.Length}, reinforcements {m.ReinforcementSpawned.Count(x => x)}/{m.ReinforcementSpawned.Length}, messages {m.Messages.Count}, top kills {(res.TopUnits.Count > 0 ? res.TopUnits[0].Kills : 0)}, promotions {res.Promotions.Count}");
        Console.WriteLine($"  results: {path}");
        Console.WriteLine($"  checksum {w.ComputeChecksum():X16} tick {w.Tick}");
        if (verify)
        {
            var rep = ReplayFile.FromWorld(w, def.Player);
            long bad = rep.Verify(defs, out string report);
            Console.WriteLine($"  replay verify: {(bad < 0 ? "OK" : "MISMATCH")} {report}");
            if (bad >= 0) return 2;
        }
        return res.Result == "running" ? 3 : 0;
    }

    // ---------------------------------------------------------------- 한국군 전용 유닛 (N21)

    /// <summary>koreaOnly 유닛(돌격보병)이 한국군 외 세력 편성에 있으면 로드 오류 + 모든 미션 · 시나리오 JSON 에 위반이 없다.</summary>
    static void KoreaOnlyScan(Action<bool, string> check)
    {
        var defs = Defs.Current;
        check(defs.Units["infantry_assault"].KoreaOnly && defs.Units.Values.Count(u => u.KoreaOnly) == 1, "돌격보병 = 한국군 전용 (koreaOnly 정의 1개)");
        string txt = File.ReadAllText(MissionDef.PathOf("ai_kr_vs_ft_1000"));
        int at = txt.IndexOf("\"infantry_fengtian\": 19", StringComparison.Ordinal);
        bool threw = false;
        if (at >= 0) { try { MissionDef.Parse(txt.Replace("\"infantry_fengtian\": 19", "\"infantry_assault\": 19"), "bad"); } catch (InvalidDataException) { threw = true; } }
        check(at >= 0 && threw, "봉천 세력에 돌격보병 편성 → 로드 오류 (한국군 전용 검사)");
        var files = new List<string>(Directory.GetFiles(MissionDef.Folder, "*.json"));
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var tools = Path.Combine(dir.FullName, "tools");
            if (Directory.Exists(tools)) { files.AddRange(Directory.GetFiles(tools, "*.json", SearchOption.AllDirectories)); break; }
        }
        var bad = new List<string>(); int scanned = 0;
        foreach (var f in files)
        {
            string t = File.ReadAllText(f);
            if (!t.Contains("\"factions\"", StringComparison.Ordinal) || !t.Contains("\"forces\"", StringComparison.Ordinal)) continue;
            scanned++;
            try { MissionDef.Parse(t, Path.GetFileNameWithoutExtension(f)); }
            catch (InvalidDataException e) when (e.Message.Contains("한국군 전용", StringComparison.Ordinal)) { bad.Add($"{Path.GetFileName(f)}: {e.Message}"); }
            catch (Exception) { }   // 다른 사유의 오류는 이 검사의 대상이 아님
        }
        check(bad.Count == 0 && scanned >= 3, $"모든 미션 · 시나리오 JSON {scanned}개에서 한국군 전용 유닛 위반 없음" + (bad.Count > 0 ? " — " + string.Join("; ", bad.Take(3)) : ""));
    }
}
