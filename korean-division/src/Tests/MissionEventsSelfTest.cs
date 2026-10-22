using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>C1E1-g3 미션 이벤트 행동 확장 (missions 묶음): 목표 AND · 지연 · 사건 종료(win) · 미션 변수 쓰기/조건 · 미션 중 결정(대기 → 응답 명령 → 조건) ·
/// 저장/불러오기(대기 중) · 리플레이 · 캠페인 정산 도우미 · 형식 검증.</summary>
public static class MissionEventsSelfTest
{
    const string Template = """
    { "id": "t_events", "title": "T", "player": 1, "seed": 5,
      "factions": [ { "name": "A", "team": 1, "posture": "hold" }, { "name": "B", "team": 2, "posture": "hold" } ],
      "mapData": { "name": "t", "width": 64, "height": 64, "base": "grass", "starts": [ [10, 32], [50, 32] ] },
      "areas": { "goal": { "rect": [28, 26, 10, 12] } },
      "forces": [
        { "id": "p1", "faction": 1, "at": [10, 28], "units": { "officer": 1, "infantry": 4 } },
        { "id": "e1", "faction": 2, "at": [58, 56], "units": { "infantry": 3 } } ],
      "objectives": [ { "id": "main", "type": "defend", "seconds": 9999 },
                      { "id": "o2", "type": "defend", "seconds": 3, "primary": false },
                      { "id": "o3", "type": "defend", "seconds": 5, "primary": false } ],
      "events": [ EVT ] }
    """;

    static MissionDef Make(string evt) => MissionDef.Parse(Template.Replace("EVT", evt), "t_events");
    static World W(MissionDef d, IReadOnlyDictionary<string, int>? sv = null) { var w = World.CreateMissionWorld(d, Defs.Current, null, null, sv); w.Fog.Enabled = false; return w; }
    static void Steps(World w, float sec) { for (int i = 0; i < (int)MathF.Round(sec / Config.SimDt) && w.Winner == 0; i++) w.Step(); }
    static long MsgTick(World w, string text) { foreach (var (t, s) in w.Mission!.Messages) if (s == text) return t; return -1; }
    static bool Throws(string evt) { try { Make(evt); return false; } catch (InvalidDataException) { return true; } }

    public static void Run(Action<bool, string> check)
    {
        // 1) 목표 둘 AND (o2 3초 · o3 5초) + 지연
        var w = W(Make("""
            { "id": "and", "when": { "objectives": ["o2", "o3"] }, "do": [ { "message": "and" } ] },
            { "id": "single", "when": { "objective": "o2" }, "do": [ { "message": "single" } ] },
            { "id": "dly", "when": { "objectives": ["o2", "o3"], "delay": 2 }, "do": [ { "message": "dly" } ] }
            """));
        Steps(w, 4.5f);
        bool early = MsgTick(w, "single") >= 0 && MsgTick(w, "and") < 0;
        Steps(w, 1.5f);
        long tAnd = MsgTick(w, "and");
        Steps(w, 2.5f);
        long tDly = MsgTick(w, "dly");
        check(early && tAnd >= 5 * 60 - 2 && tAnd <= 5 * 60 + 3 && tDly - tAnd >= 118 && tDly - tAnd <= 122,
            $"g3 사건 조건: 목표 둘 AND (3초 목표만으론 안 됨, 둘째 완료 틱 {tAnd}) · delay 2초 (+{tDly - tAnd}틱)");

        // 2) 사건으로 승리 / 패배 (주 목표가 남아 있어도)
        var ww = W(Make("""{ "id": "win", "when": { "time": 2 }, "do": [ { "message": "boss", "end": "win" }, { "message": "after" } ] }"""));
        Steps(ww, 4);
        check(ww.Winner == 1 && ww.Mission!.Result == MissionState.Won && MsgTick(ww, "after") >= 0, $"g3 end:win — 주 목표 미완료에도 조기 승리 (승패 판정이 덮어쓰지 않음, 틱 {ww.Mission.EndTick})");
        var wl = W(Make("""{ "id": "lose", "when": { "time": 2 }, "do": [ { "end": "lose" } ] }"""));
        Steps(wl, 4);
        check(wl.Mission!.Result == MissionState.Lost && wl.Winner == 2, "g3 end:lose — 사건 패배");

        // 3) 미션 변수 쓰기 + 조건 (시작 변수 포함)
        var wv = W(Make("""
            { "id": "s1", "when": { "time": 1 }, "do": [ { "setVar": "x.a", "value": 3 }, { "setVar": "x.a", "delta": 2 }, { "setVar": "x.b", "delta": -1 } ] },
            { "id": "c1", "when": { "var": "x.a", "op": "==", "value": 5 }, "do": [ { "message": "a5" } ] },
            { "id": "c2", "when": { "var": "x.b", "op": "<", "value": 0 }, "do": [ { "message": "bneg" } ] },
            { "id": "c3", "when": { "var": "seed.v", "value": 7, "time": 0 }, "do": [ { "message": "seed7" } ] },
            { "id": "c4", "when": { "var": "x.a", "op": ">", "value": 5 }, "do": [ { "message": "never" } ] }
            """), new Dictionary<string, int> { ["seed.v"] = 7 });
        Steps(wv, 3);
        check(wv.Mission!.Var("x.a") == 5 && wv.Mission.Var("x.b") == -1 && MsgTick(wv, "a5") >= 0 && MsgTick(wv, "bneg") >= 0 && MsgTick(wv, "seed7") >= 0 && MsgTick(wv, "never") < 0,
            "g3 미션 변수: setVar 대입/증감 · when.var 비교 · 시작 변수 주입");

        // 4) 미션 중 결정: 제기 → 대기 → 응답 명령 → decision 조건 (B 는 안 걸림)
        var evt4 = """
            { "id": "ask", "when": { "time": 1 }, "do": [ { "decision": "D08" }, { "decision": "D08" } ] },
            { "id": "onA", "when": { "decision": "D08", "choice": "A" }, "do": [ { "message": "chose A" } ] },
            { "id": "onB", "when": { "decision": "D08", "choice": "B" }, "do": [ { "message": "chose B" } ] },
            { "id": "any", "when": { "decision": "D08" }, "do": [ { "setVar": "rep.local", "delta": 1 } ] }
            """;
        var wd = W(Make(evt4)); wd.RecordCommands = true;
        Steps(wd, 3);
        bool pending = wd.Mission!.PendingDecisions.SequenceEqual(new[] { "D08" }) && wd.Mission.Decisions.Count == 0;
        wd.Issue(new MissionDecisionCommand(1, "D99", "A"));   // 대기 중 아닌 결정 → 무시
        wd.Issue(new MissionDecisionCommand(2, "D08", "B"));   // 다른 세력 → 무시
        Steps(wd, 0.5f);
        bool ignored = wd.Mission.PendingDecisions.Count == 1 && wd.Mission.Decisions.Count == 0;
        var meta = new SnapshotMeta("t", 1, false, "mission");
        var bytes = Snapshot.Save(wd, meta);   // 대기 중 저장
        var ld = Snapshot.Load(bytes, Defs.Current).World;
        bool restored = ld.Mission!.PendingDecisions.SequenceEqual(new[] { "D08" }) && Snapshot.RawBody(Snapshot.Save(ld, meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        wd.Issue(new MissionDecisionCommand(1, "D08", "A")); ld.Issue(new MissionDecisionCommand(1, "D08", "A"));
        Steps(wd, 2); Steps(ld, 2);
        check(pending && ignored && restored, "g3 결정: 제기(중복 무시) → 대기 · 엉뚱한 id/세력 응답 무시 · 대기 중 저장/불러오기 바이트 동일");
        check(wd.Mission.Decisions["D08"] == "A" && wd.Mission.PendingDecisions.Count == 0 && MsgTick(wd, "chose A") >= 0 && MsgTick(wd, "chose B") < 0 && wd.Mission.Var("rep.local") == 1
            && wd.ComputeChecksum() == ld.ComputeChecksum(), "g3 결정: 응답 → decision/choice 조건 사건 · 불러온 월드와 체크섬 동일");
        var rep = ReplayFile.FromWorld(wd, 1);
        check(rep.Verify(Defs.Current, out string rpt) < 0, $"g3 결정 응답이 리플레이에 기록되어 재생 일치: {rpt}");

        // 5) 시작 변수가 리플레이 머리말로 재현
        var wr = W(Make("""{ "id": "c", "when": { "var": "seed.v", "value": 7 }, "do": [ { "message": "ok" }, { "setVar": "seed.v", "delta": 1 } ] }"""), new Dictionary<string, int> { ["seed.v"] = 7 });
        wr.RecordCommands = true; Steps(wr, 2);
        check(ReplayFile.FromWorld(wr, 1).Verify(Defs.Current, out string r5) < 0 && wr.Mission!.StartVars["seed.v"] == 7 && wr.Mission.Var("seed.v") == 8, $"g3 시작 변수 리플레이 머리말 재현: {r5}");

        // 6) 기본 형식 미션은 새 상태를 쓰지 않음 (해시에 아무것도 안 더함)
        var wp = W(Make("""{ "id": "plain", "when": { "time": 1 }, "do": [ { "message": "m" } ] }"""));
        Steps(wp, 2);
        var m0 = wp.Mission!;
        check(m0.Vars.Count == 0 && m0.PendingDecisions.Count == 0 && m0.Decisions.Count == 0 && m0.EventArmed.All(t => t == 0), "g3 기존 형식 미션: 새 상태 전부 비어 있음 (체크섬 기여 없음)");

        // 7) 형식 검증
        check(Throws("""{ "id": "x", "when": { "time": 1 }, "do": [ { "end": "draw" } ] }""")
              && Throws("""{ "id": "x", "when": { "time": 1 }, "do": [ { "setVar": "a", "value": 1, "delta": 1 } ] }""")
              && Throws("""{ "id": "x", "when": { "time": 1 }, "do": [ { "setVar": "a" } ] }""")
              && Throws("""{ "id": "x", "when": { "objectives": ["nope"] }, "do": [] }""")
              && Throws("""{ "id": "x", "when": { "delay": -1 }, "do": [] }""")
              && Throws("""{ "id": "x", "when": { "var": "a", "op": "=~" }, "do": [] }""")
              && Throws("""{ "id": "x", "when": { "choice": "A" }, "do": [] }""")
              && Throws("""{ "id": "x", "when": { "time": 1 }, "do": [ { "decision": "bad id!" } ] }"""), "g3 형식 검증: 잘못된 end · setVar · objectives · delay · op · choice · 결정 id 거부");

        // 8) 캠페인 정산 도우미: 변수 대입 + 결정 응답 반영
        CampaignVars.Override(null); CampaignDecisions.Override(null);
        var cs = new CampaignState();
        var def = Make("""{ "id": "v", "when": { "var": "flag.dockIntel", "value": 1 }, "do": [ { "setVar": "intel.andong", "value": 1 }, { "setVar": "zz.unknown", "value": 1 } ] }""");
        var sv = CampaignVars.StartVarsFor(cs, def);
        var errs = CampaignDecisions.Current.ApplyMissionOutcome(cs, new Dictionary<string, string> { ["D08"] = "A", ["D99"] = "A" }, new Dictionary<string, int> { ["intel.andong"] = 1, ["zz.unknown"] = 1 });
        check(sv.Count == 2 && sv.ContainsKey("flag.dockIntel") && sv.ContainsKey("intel.andong") && CampaignVars.Get(cs, "intel.andong") == 1 && CampaignDecisions.Current.IsAnswered(cs, "D08")
              && errs.Count == 1 && errs[0].StartsWith("D99") && !cs.Flags.ContainsKey("zz.unknown"), $"g3 캠페인 정산 도우미: 시작 변수 수집(레지스트리만) · 변수 대입 · 결정 응답 반영 · 모르는 결정 오류만 ({string.Join("; ", errs)})");
    }
}
