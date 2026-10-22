using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>[C1E1-g1] 에피소드 그래프 런타임: 로더 검증 · R1 표 (6행 x 4열 x 대기 유/무) · SELECT 규칙 · 재도전 복원 · 저장 왕복 · 스키마 v12 이행.
/// 그룹 KD_SELFTEST_ONLY=episode (savecompat 묶음에서도 돈다).</summary>
public static class CampaignEpisodeSelfTest
{
    static string Step(string? err, string what) => err == null ? "" : $"[{what}: {err}]";

    /// <summary>E1 전체를 한 번 돈다. 반환 = 첫 실패 사유 ("" = 성공) · 예고 일수 (IL2 시점).</summary>
    static string RunE1(EpisodeDef def, CampaignState s, string approach, int skCount, string d10, bool wait, out int projected)
    {
        var vars = new FlagsCampaignVars(s);
        projected = -1;
        EpisodeRuntime.Start(s, def);
        string f = Step(EpisodeRuntime.Resolve(s, def), "EV1") + Step(EpisodeRuntime.Resolve(s, def), "CH1") + Step(EpisodeRuntime.ConfirmFormation(s, def, false), "IL1")
                 + Step(EpisodeRuntime.BeginMission(s, def, vars, out _), "BT1 시작") + Step(EpisodeRuntime.MissionWon(s, def), "BT1 승리");
        string[] sks = { "SK1", "SK4" };
        for (int i = 0; i < 2; i++)
        {
            if (i < skCount) f += Step(EpisodeRuntime.Select(s, def, sks[i]), "SELECT") + Step(EpisodeRuntime.BeginMission(s, def, vars, out _), "SK 시작") + Step(EpisodeRuntime.MissionWon(s, def), "SK 승리");
            else f += Step(EpisodeRuntime.Select(s, def, null), "SELECT 건너뛰기");
        }
        f += Step(EpisodeRuntime.Resolve(s, def), "CH2") + Step(EpisodeRuntime.Resolve(s, def, d10), "EV2");
        projected = EpisodeRuntime.ProjectDays(s, def, approach, wait);
        f += Step(EpisodeRuntime.ConfirmFormation(s, def, wait), "IL2") + Step(EpisodeRuntime.ChooseVariant(s, def, approach), "D11")
           + Step(EpisodeRuntime.BeginMission(s, def, vars, out _), "OP1 시작") + Step(EpisodeRuntime.MissionWon(s, def), "OP1 승리")
           + Step(EpisodeRuntime.Resolve(s, def), "D14") + Step(EpisodeRuntime.Resolve(s, def), "EV3");
        if (f == "" && !s.Episode!.Finished) f = "끝나지 않음";
        return f;
    }

    static EpisodeDef? P(string json, out List<string> errors) { errors = new(); return EpisodeDefs.Parse(json, errors); }

    const string Mini = "{\"id\":\"m\",\"dayCap\":7,\"start\":\"A\",\"nodes\":[{\"id\":\"A\",\"kind\":\"EV\",\"next\":\"S\"},{\"id\":\"S\",\"kind\":\"SELECT\",\"options\":[\"K\"],\"next\":\"B\"},{\"id\":\"K\",\"kind\":\"SK\",\"mission\":\"k\",\"days\":1},{\"id\":\"B\",\"kind\":\"BT\",\"mission\":\"b\",\"days\":1,\"next\":\"\"}]}";

    public static void Run(Action<bool, string> check)
    {
        var errs = new List<string>();
        var def = EpisodeDefs.Load(EpisodeDefs.PathFor("c1e1"), errs);
        check(def != null && errs.Count == 0 && def.Nodes.Count == 16 && def.DayCap == 7 && def.Start == "EV1" && def.Find("OP1")!.Variants.Count == 2 && def.Find("EV2")!.Deadline.Count == 4,
            $"C1E1-g1 c1e1.json 로드 (오류 {string.Join(" / ", errs)})");
        if (def == null) return;

        // 1) 로더 검증: 정상 최소 그래프 통과, 깨진 그래프는 각각 거부
        check(P(Mini, out var e0) != null && e0.Count == 0, $"C1E1-g1 최소 그래프 통과 ({string.Join("/", e0)})");
        var bad = new (string Name, string Json)[]
        {
            ("끊긴 전이", Mini.Replace("\"next\":\"B\"", "\"next\":\"Z\"")),
            ("모르는 kind", Mini.Replace("\"kind\":\"EV\"", "\"kind\":\"XX\"")),
            ("숫자 kind", Mini.Replace("\"kind\":\"EV\"", "\"kind\":\"0\"")),
            ("id 중복", Mini.Replace("{\"id\":\"B\",", "{\"id\":\"A\",")),
            ("SELECT 선택지가 SK 아님", Mini.Replace("\"options\":[\"K\"]", "\"options\":[\"B\"]")),
            ("SELECT 선택지 없음", Mini.Replace("\"options\":[\"K\"]", "\"options\":[]")),
            ("SK 에 next", Mini.Replace("\"mission\":\"k\",\"days\":1}", "\"mission\":\"k\",\"days\":1,\"next\":\"B\"}")),
            ("next 없음", Mini.Replace("\"days\":1,\"next\":\"\"}", "\"days\":1}")),
            ("닿지 않는 노드", Mini.Replace("]}", ",{\"id\":\"Q\",\"kind\":\"EV\",\"next\":\"\"}]}")),
            ("순환", Mini.Replace("\"days\":1,\"next\":\"\"}", "\"days\":1,\"next\":\"A\"}")),
            ("start 없음", Mini.Replace("\"start\":\"A\"", "\"start\":\"Z\"")),
            ("mission 없음", Mini.Replace("\"mission\":\"b\",", "")),
            ("일수 음수", Mini.Replace("\"mission\":\"b\",\"days\":1", "\"mission\":\"b\",\"days\":-1")),
            ("기한 범위 밖", Mini.Replace("\"kind\":\"EV\",", "\"kind\":\"EV\",\"deadline\":{\"x\":8},")),
            ("dayCap 0", Mini.Replace("\"dayCap\":7", "\"dayCap\":0")),
            ("BT 실패 확정", Mini.Replace("\"mission\":\"b\",", "\"mission\":\"b\",\"onLose\":\"retryOrFail\",")),
            ("next 가 SK 직접", Mini.Replace("{\"id\":\"A\",\"kind\":\"EV\",\"next\":\"S\"}", "{\"id\":\"A\",\"kind\":\"EV\",\"next\":\"K\"}")),
            ("쓰레기", "{\"id\":"),
            ("빈 문자열", ""),
            ("배열", "[]"),
        };
        string leaked = "";
        foreach (var (name, json) in bad)
        {
            try { if (P(json, out var be) != null || be.Count == 0) leaked += name + " "; }
            catch (Exception ex) { leaked += $"{name}({ex.GetType().Name}) "; }
        }
        check(leaked == "", $"C1E1-g1 깨진 그래프 {bad.Length}종 거부 (통과해 버린 것: {leaked})");

        // 2) R1 표: 6행 (강공/포위 x SK 0~2) x 4열 (따름5/지연6/설득성립7/설득실패5) x 대기 무/유. O = 기한 이내, X = 초과. 원문 spec_c1e1 R1 그대로.
        string[] cols = { "follow", "delay", "persuadeOk", "persuadeFail" };
        var table = new (string Approach, int Sk, int Total, string[] Marks)[]
        {
            ("assault", 0, 3, new[] { "OO", "OO", "OO", "OO" }),
            ("assault", 1, 4, new[] { "OO", "OO", "OO", "OO" }),
            ("assault", 2, 5, new[] { "OX", "OO", "OO", "OX" }),
            ("siege",   0, 5, new[] { "OX", "OO", "OO", "OX" }),
            ("siege",   1, 6, new[] { "XX", "OX", "OO", "XX" }),
            ("siege",   2, 7, new[] { "XX", "XX", "OX", "XX" }),
        };
        int cells = 0; string r1Bad = "";
        foreach (var row in table)
            for (int c = 0; c < 4; c++)
                for (int w = 0; w < 2; w++)
                {
                    var s = new CampaignState();
                    string f = RunE1(def, s, row.Approach, row.Sk, cols[c], w == 1, out int proj);
                    int want = row.Total + w;
                    bool wantLate = row.Marks[c][w] == 'X';
                    var e = s.Episode!;
                    if (f != "" || e.DaysUsed != want || e.Late != wantLate || proj != want || EpisodeRuntime.WouldBeLate(s, def, proj) != wantLate || e.Waited != (w == 1))
                        r1Bad += $"[{row.Approach} SK{row.Sk} {cols[c]} 대기{w}: {f} 일수 {e.DaysUsed}/{want} 예고 {proj} 초과 {e.Late}/{wantLate}] ";
                    cells++;
                }
        check(cells == 48 && r1Bad == "", $"C1E1-g1 R1 기한 표 6x4x2 = {cells}칸 일치 {r1Bad}");

        // 3) SELECT 규칙: 중복 불가 · 목록 밖 불가 · 건너뛰기 후 뒤 칸에서 고르기 · 실패 확정 SK 재선택 불가 · 실패 확정은 일수 0
        {
            var s = new CampaignState(); var v = new FlagsCampaignVars(s);
            EpisodeRuntime.Start(s, def);
            EpisodeRuntime.Resolve(s, def); EpisodeRuntime.Resolve(s, def); EpisodeRuntime.ConfirmFormation(s, def, false);
            EpisodeRuntime.BeginMission(s, def, v, out string bt); EpisodeRuntime.MissionWon(s, def);
            string? notIn = EpisodeRuntime.Select(s, def, "BT1");
            string? skip = EpisodeRuntime.Select(s, def, null);
            bool atSel2 = s.Episode!.NodeId == "SEL2";
            string? pick = EpisodeRuntime.Select(s, def, "SK3");
            EpisodeRuntime.BeginMission(s, def, v, out string sk3);
            string? fail = EpisodeRuntime.MissionLost(s, def, v, confirmFail: true);
            var e = s.Episode;
            check(bt == "c1e1_bt1" && notIn != null && skip == null && atSel2 && pick == null && sk3 == "c1e1_sk3" && fail == null && e.NodeId == "CH2" && e.DaysUsed == 1
                  && e.Selected.SequenceEqual(new[] { "SK3" }) && e.FailedSk.SequenceEqual(new[] { "SK3" }),
                $"C1E1-g1 SELECT: 목록 밖 거부 · 건너뛰기 · 뒤 칸 선택 · SK 실패 확정 → 일수 0 · SELECT 다음 (노드 {e.NodeId} 일수 {e.DaysUsed})");

            var s2 = new CampaignState(); var v2 = new FlagsCampaignVars(s2);
            EpisodeRuntime.Start(s2, def);
            EpisodeRuntime.Resolve(s2, def); EpisodeRuntime.Resolve(s2, def); EpisodeRuntime.ConfirmFormation(s2, def, false);
            EpisodeRuntime.BeginMission(s2, def, v2, out _);
            string? btConfirm = EpisodeRuntime.MissionLost(s2, def, v2, confirmFail: true);   // BT 는 재도전만
            EpisodeRuntime.MissionLost(s2, def, v2, confirmFail: false);
            EpisodeRuntime.BeginMission(s2, def, v2, out _); EpisodeRuntime.MissionWon(s2, def);
            EpisodeRuntime.Select(s2, def, "SK2"); EpisodeRuntime.BeginMission(s2, def, v2, out _); EpisodeRuntime.MissionWon(s2, def);
            string? dup = EpisodeRuntime.Select(s2, def, "SK2");
            bool still = s2.Episode!.NodeId == "SEL2";
            string? resolveWrong = EpisodeRuntime.Resolve(s2, def);   // SELECT 에서 Resolve 불가
            string? ok2 = EpisodeRuntime.Select(s2, def, "SK1");
            check(btConfirm != null && dup != null && still && resolveWrong != null && ok2 == null && s2.Episode.NodeId == "SK1" && s2.Episode.SelectNode == "SEL2" && s2.Episode.DaysUsed == 2,
                $"C1E1-g1 SELECT: 같은 SK 중복 거부 · BT 실패 확정 거부 · 잘못된 순서 거부 (일수 {s2.Episode.DaysUsed})");
        }

        // 4) 재도전 복원: 패배 = 일수 · 보급 · 변수 불변. OP1 재도전 = IL2 선택(대기 포함) 이후 상태, 변형 다시 선택.
        {
            var s = new CampaignState(); var v = new FlagsCampaignVars(s);
            EpisodeRuntime.Start(s, def);
            EpisodeRuntime.Resolve(s, def); EpisodeRuntime.Resolve(s, def); EpisodeRuntime.ConfirmFormation(s, def, false);
            v.Set("rep.local", 3); s.Supply = 4000f;
            EpisodeRuntime.BeginMission(s, def, v, out _);
            v.Set("rep.local", 9); v.Set("pow.bandit", 40); s.Supply = 3100f;   // 미션 중 변화 (예약 · 보상 등)
            string? lost = EpisodeRuntime.MissionLost(s, def, v, false);
            bool btRestored = lost == null && s.Episode!.NodeId == "BT1" && s.Episode.DaysUsed == 0 && v.Get("rep.local") == 3 && !v.Has("pow.bandit") && s.Supply == 4000f && s.Episode.Checkpoint == null;
            string? wonWithout = EpisodeRuntime.MissionWon(s, def);   // 진입 없이 승리 불가
            EpisodeRuntime.BeginMission(s, def, v, out _); EpisodeRuntime.MissionWon(s, def);
            EpisodeRuntime.Select(s, def, null); EpisodeRuntime.Select(s, def, null);
            EpisodeRuntime.Resolve(s, def); EpisodeRuntime.Resolve(s, def, "delay");
            EpisodeRuntime.ConfirmFormation(s, def, true);   // D16 대기: 확정 시 1일
            int afterIl2 = s.Episode.DaysUsed;
            v.Set("morale.troops", 5); s.Supply = 3800f;
            string? noVariant = EpisodeRuntime.BeginMission(s, def, v, out _);
            EpisodeRuntime.ChooseVariant(s, def, "siege");
            EpisodeRuntime.BeginMission(s, def, v, out string op);
            string? switchMid = EpisodeRuntime.ChooseVariant(s, def, "assault");
            v.Set("morale.troops", 1); v.Set("flag.andongSurrender", 1); s.Supply = 900f;
            EpisodeRuntime.MissionLost(s, def, v, false);
            var e = s.Episode;
            bool opRestored = e.NodeId == "OP1" && e.Variant == "" && e.DaysUsed == afterIl2 && afterIl2 == 2 && e.Waited && v.Get("morale.troops") == 5 && !v.Has("flag.andongSurrender") && s.Supply == 3800f;
            string? opConfirm = null;
            EpisodeRuntime.ChooseVariant(s, def, "assault"); EpisodeRuntime.BeginMission(s, def, v, out string op2);
            opConfirm = EpisodeRuntime.MissionLost(s, def, v, true);   // OP 실패 확정 불가 → 상태 그대로 (미션 진행 중)
            EpisodeRuntime.MissionWon(s, def);
            check(btRestored && wonWithout != null && noVariant != null && op == "c1e1_op1_siege" && switchMid != null && opRestored && op2 == "c1e1_op1_assault" && opConfirm != null
                  && e.DaysUsed == 4 && e.DeadlineDay == 6 && !e.Late && e.NodeId == "D14",
                $"C1E1-g1 재도전 복원: BT1 {btRestored} · OP1 = IL2 이후 상태 {opRestored} (일수 {e.DaysUsed} 기한 {e.DeadlineDay})");
        }

        // 5) 저장 왕복: 미션 중 (체크포인트 포함) 저장 → 불러오기 → 이어서 같은 결과. 결정적 직렬화.
        {
            var s = new CampaignState(); var v = new FlagsCampaignVars(s);
            EpisodeRuntime.Start(s, def);
            EpisodeRuntime.Resolve(s, def, "a"); EpisodeRuntime.Resolve(s, def); EpisodeRuntime.ConfirmFormation(s, def, false);
            EpisodeRuntime.BeginMission(s, def, v, out _); EpisodeRuntime.MissionWon(s, def);
            EpisodeRuntime.Select(s, def, "SK2"); EpisodeRuntime.BeginMission(s, def, v, out _); EpisodeRuntime.MissionWon(s, def);
            EpisodeRuntime.Select(s, def, "SK4");
            v.Set("intel.andong", 1); s.Supply = 3500f;
            EpisodeRuntime.BeginMission(s, def, v, out _);
            v.Set("intel.andong", 7);
            string json = s.ToJson();
            var back = CampaignState.Parse(json, out var perr);
            var be = back?.Episode;
            bool same = back != null && perr == null && back.Version == CampaignState.SchemaVersion && be != null && be.EpisodeId == "c1e1" && be.NodeId == "SK4" && be.SelectNode == "SEL2"
                        && be.DaysUsed == 2 && be.Selected.SequenceEqual(new[] { "SK2", "SK4" }) && be.Outcomes.Count == 1 && be.Outcomes[0].Node == "EV1" && be.Outcomes[0].Outcome == "a"
                        && be.Checkpoint != null && be.Checkpoint.Supply == 3500f && be.Checkpoint.Vars.Any(x => x.Key == "intel.andong" && x.Value == 1) && CampaignState.Parse(back.ToJson(), out _)?.ToJson() == back.ToJson();
            string? lost = back == null ? "x" : EpisodeRuntime.MissionLost(back, def, new FlagsCampaignVars(back), false);
            bool cont = back != null && lost == null && back.Flags["intel.andong"] == 1 && back.Supply == 3500f && back.Episode!.NodeId == "SK4";
            if (back != null) { var bv = new FlagsCampaignVars(back); EpisodeRuntime.BeginMission(back, def, bv, out _); EpisodeRuntime.MissionWon(back, def); }
            bool next = back?.Episode?.NodeId == "CH2" && back.Episode.DaysUsed == 3;
            var keys = be?.Checkpoint?.Vars.Select(x => x.Key).ToList() ?? new();
            bool sorted = keys.SequenceEqual(keys.OrderBy(k => k, StringComparer.Ordinal));
            check(same && cont && next && sorted, $"C1E1-g1 저장 왕복: 미션 중 체크포인트 보존 {same} · 불러온 뒤 재도전 복원 {cont} · 이어서 진행 {next} · 변수 정렬 {sorted} ({perr})");
        }

        // 6) 스키마 이행: v9/v11 (Episode 없음) → 현재 번호 · null. 손상 Episode (null 목록/원소 · 음수) 는 정리해 연다.
        {
            var v9 = CampaignState.Parse("{\"Version\":9,\"Completed\":[\"c0_01_bandits\"],\"Day\":3}", out var e9);
            var v11 = CampaignState.Parse("{\"Version\":11,\"Day\":2}", out var e11);
            var dirty = CampaignState.Parse("{\"Version\":12,\"Episode\":{\"EpisodeId\":\"c1e1\",\"NodeId\":\"SEL1\",\"DaysUsed\":-4,\"Selected\":null,\"FailedSk\":[null,\"SK1\"],\"Outcomes\":[null],\"Checkpoint\":{\"Vars\":[null,{\"Key\":\"\",\"Value\":1},{\"Key\":\"a\",\"Value\":2}],\"Supply\":-5}}}", out var ed);
            var de = dirty?.Episode;
            check(v9 != null && e9 == null && v9.Version == CampaignState.SchemaVersion && v9.Episode == null && v9.Day == 3 && v11 != null && e11 == null && v11.Episode == null
                  && dirty != null && ed == null && de != null && de.DaysUsed == 0 && de.Selected.Count == 0 && de.FailedSk.SequenceEqual(new[] { "SK1" }) && de.Outcomes.Count == 0
                  && de.Checkpoint!.Vars.Count == 1 && de.Checkpoint.Supply == 0f && EpisodeRuntime.Current(dirty, def)?.Id == "SEL1",
                $"C1E1-g1 저장 이행: v9/v11 → v{CampaignState.SchemaVersion} Episode 없음 · 손상 Episode 정리 ({e9}/{e11}/{ed})");
            var ghost = new CampaignState { Episode = new EpisodeState { EpisodeId = "c1e1", NodeId = "ZZ" } };
            check(EpisodeRuntime.Current(ghost, def) == null && EpisodeRuntime.Resolve(ghost, def) != null && ghost.Episode.NodeId == "ZZ"
                  && EpisodeRuntime.Resolve(new CampaignState(), def) != null,
                "C1E1-g1 정의에 없는 노드 · 에피소드 없음은 크래시 없이 거부");
        }
    }
}
