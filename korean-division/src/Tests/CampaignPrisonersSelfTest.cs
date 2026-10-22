using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T42c 포로 장부 · 송환/수용: v3~v5 -> v6 이행 · 왕복 · 정산 가산 (승리만) · 처리 한도 · 작전 사이에만 · 0 이어도 안전 · 결과 수집. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignPrisonersSelfTest
{
    static MissionResults Won(int captured, string result = "won")
    {
        var r = new MissionResults { MissionId = "m", Result = result, EnemyKills = 10, EnemyCaptured = captured };
        r.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 60, Alive = 60 });
        return r;
    }

    public static void Run(Action<bool, string> check)
    {
        var saved = CampaignEconomyDefs.Current;
        try
        {
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { PrisonerAcceptSupply = 2f }.Sanitized());

            // 1) 옛 저장 (v3 · v5) -> v6: 포로 0, 새 필드 왕복
            var v3 = CampaignState.FromJson("{\"Version\":3,\"Roster\":[{\"Id\":\"a\",\"Kind\":\"infantry\",\"Strength\":60,\"MaxStrength\":60}]}");
            var v5 = CampaignState.FromJson("{\"Version\":5,\"Stock\":{\"Horses\":7},\"PromotionsThisBreak\":2}");
            check(v3 != null && v3.Version == 9 && v3.Stock.Prisoners == 0 && v3.PrisonersRepatriated == 0 && v5 != null && v5.Version == 9 && v5.Stock.Prisoners == 0 && v5.Stock.Horses == 7 && v5.PromotionsThisBreak == 2,
                "T42c 저장 이행: v3 · v5 파일 -> v6, 포로 0 (다른 필드 보존)");
            var s = new CampaignState();
            s.Stock.Prisoners = 5; s.PrisonersRepatriated = 3; s.PrisonersInterned = 4;
            var back = CampaignState.FromJson(s.ToJson());
            check(back != null && back.Stock.Prisoners == 5 && back.PrisonersRepatriated == 3 && back.PrisonersInterned == 4, "T42c 저장 왕복: 포로 장부 · 송환 · 수용 누계");
            var neg = CampaignState.FromJson("{\"Version\":6,\"Stock\":{\"Prisoners\":-9},\"PrisonersInterned\":-2}");
            check(neg != null && neg.Stock.Prisoners == 0 && neg.PrisonersInterned == 0, "T42c 손상 값(음수)은 0 으로 보정");

            // 2) 정산: 승리만 장부에 더한다, 0 이어도 안전 (항복 꺼짐 = 기본)
            var win = new CampaignState();
            CampaignEconomy.SettleBattle(win, Won(0));
            check(win.Stock.Prisoners == 0 && win.LastDebrief!.PrisonersTaken == 0, "T42c 항복 없음(0): 장부 0 · 크래시 없음");
            CampaignEconomy.SettleBattle(win, Won(7));
            CampaignEconomy.SettleBattle(win, Won(3));
            check(win.Stock.Prisoners == 10 && win.LastDebrief!.PrisonersTaken == 3 && win.Stock.RecentLog[^1].Contains("포로 +3"), "T42c 정산: 승리 포로 수 -> 장부 가산 · 보고서 · 로그");
            var lost = new CampaignState();
            CampaignEconomy.SettleBattle(lost, Won(8, "lost"));
            check(lost.Stock.Prisoners == 0 && lost.LastDebrief!.PrisonersTaken == 0, "T42c 정산: 패배는 포로 없음");

            // 3) 처리: 송환 무료 · 수용 보급 소모 · 한도
            var h = new CampaignState { Supply = 7f };
            h.Stock.Prisoners = 10;
            int a = CampaignPrisoners.Accept(h, 100);   // 보급 7 / 2 = 3명
            check(a == 3 && h.Stock.Prisoners == 7 && MathF.Abs(h.Supply - 1f) < 1e-4f && h.PrisonersInterned == 3, $"T42c 수용: 보급 한도만큼 (3명), 1인당 2 소모 (got {a}, 보급 {h.Supply})");
            check(CampaignPrisoners.Accept(h, 1) == 0 && h.Stock.Prisoners == 7, "T42c 수용: 보급 부족이면 0명");
            int r1 = CampaignPrisoners.Repatriate(h, 2), r2 = CampaignPrisoners.Repatriate(h, 99);
            check(r1 == 2 && r2 == 5 && h.Stock.Prisoners == 0 && h.PrisonersRepatriated == 7 && MathF.Abs(h.Supply - 1f) < 1e-4f, "T42c 송환: 무료 · 장부 한도 · 누계");
            check(CampaignPrisoners.Repatriate(h, 1) == 0 && CampaignPrisoners.Accept(h, 1) == 0 && CampaignPrisoners.BlockOf(h) == CampaignPrisoners.Block.Empty, "T42c 빈 장부: 처리 0 · 예외 없음");

            // 4) 작전 사이에만
            var op = new CampaignState { Supply = 100f };
            op.Stock.Prisoners = 4;
            op.Pending = new CampaignState.PendingOperation { MissionId = "m2", DeployedCompanyIds = new() { "a" } };
            check(CampaignPrisoners.BlockOf(op) == CampaignPrisoners.Block.Operation && CampaignPrisoners.Repatriate(op, 2) == 0 && CampaignPrisoners.Accept(op, 2) == 0 && op.Stock.Prisoners == 4 && op.Supply == 100f,
                "T42c 작전 진행 중(Pending)에는 처리 거부 (상태 불변)");
            check(CampaignPrisoners.BlockOf(null) == CampaignPrisoners.Block.Empty && CampaignPrisoners.Repatriate(new CampaignState(), -3) == 0, "T42c null · 음수 입력 안전");

            // 5) 수용 단가 0 = 보급 무관, 정의 보정
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { PrisonerAcceptSupply = 0f }.Sanitized());
            var z = new CampaignState { Supply = 0f };
            z.Stock.Prisoners = 4;
            check(CampaignPrisoners.Accept(z, 9) == 4 && z.Stock.Prisoners == 0, "T42c 수용 단가 0: 보급 0 이어도 전원 가능");
            var weird = new CampaignEconomyDefs { PrisonerAcceptSupply = float.NaN }.Sanitized();
            check(weird.PrisonerAcceptSupply == 2f && new CampaignEconomyDefs { PrisonerAcceptSupply = -5f }.Sanitized().PrisonerAcceptSupply == 0f, "T42c 정의 보정: NaN -> 기본, 음수 -> 0");

            // 6) 결과 수집: 끝났을 때 항복 상태로 살아 있는 적대 유닛 = EnemyCaptured (아군 항복은 세지 않음), 시뮬 읽기만
            var def = MissionDef.Parse(File.ReadAllText(MissionDef.PathOf(CampaignState.FirstMission)), CampaignState.FirstMission);
            var w = World.CreateMissionWorld(def, Defs.Current);
            var enemies = w.Units.Where(u => u.Alive && w.IsHostile(def.Player, u.Owner) && u.Def.IsInfantry).Take(2).ToList();
            var mine = w.Units.FirstOrDefault(u => u.Alive && u.Owner == def.Player && u.Def.IsInfantry);
            var resBefore = MissionResults.From(w);
            foreach (var u in enemies) w.SurrenderForTest(u);
            if (mine != null) w.SurrenderForTest(mine);
            var res = MissionResults.From(w);
            check(enemies.Count == 2 && res.EnemyCaptured == 2, $"T42c 결과 수집: 항복한 적대 유닛만 포로 (got {res.EnemyCaptured})");
            check(res.EnemyKills == resBefore.EnemyKills && res.EnemyKills + res.EnemyCaptured == resBefore.EnemyKills + 2, $"T42c 포로는 사살 수에서 제외 (이중 계상 방지; 항복 꺼짐=기존과 동일): kills {resBefore.EnemyKills} -> {res.EnemyKills}");
        }
        finally { CampaignEconomyDefs.Override(saved); }
    }
}
