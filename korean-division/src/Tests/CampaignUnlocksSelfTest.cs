using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>C1E1-g6 unlock.* 편성 해금: 기본 상태 불변 · 미해금 병종 편성 불가 (BeginOperation 상태 불변, 편성 화면 목록 제외) · 첫 승리 정산 해금 · 저장 왕복 · 옛 저장 이행 (스키마 번호 그대로). SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignUnlocksSelfTest
{
    static CampaignState.RosterCompany Co(string id, string kind, int n = 40) => new() { Id = id, Name = id, Strength = n, MaxStrength = n, Kind = kind };

    public static void Run(Action<bool, string> check)
    {
        var saved = CampaignEconomyDefs.Current;
        try
        {
            CampaignEconomyDefs.Override(null);
            var d = CampaignEconomyDefs.Current;
            check(d.UnlockKinds.GetValueOrDefault("fieldGun") == "unlock.fieldGun" && d.UnlockOnWin.GetValueOrDefault("c1e1_bt1")?.Contains("unlock.fieldGun") == true,
                "g6 JSON: fieldGun 병종 = unlock.fieldGun 필요 · c1e1_bt1 첫 승리 = 야전포 해금");

            // 1) 기본 상태: 기존 병종 (infantry · howitzer 등) 은 전부 편성 가능, 야전포만 잠김
            var s = new CampaignState(); s.Roster.Clear();
            s.Roster.Add(Co("inf", "infantry")); s.Roster.Add(Co("how", "howitzer")); s.Roster.Add(Co("fg", "fieldGun")); s.Roster.Add(Co("ig", "infGun"));
            check(!CampaignUnlocks.IsUnlocked(s, "unlock.fieldGun") && CampaignUnlocks.Deployable(s).Select(c => c.Id).SequenceEqual(new[] { "inf", "how", "ig" }),
                "g6 기본: 야전포만 편성 목록에서 빠짐 (보병 · 곡사포 · 보병포는 그대로)");

            // 2) 미해금 편성은 시작하지 않는다 (상태 불변), 해금 후엔 통과
            var none = new Dictionary<string, int>();
            bool locked = CampaignEconomy.BeginOperation(s, "x", new[] { "inf", "fg" }, 0, none);
            bool okInf = CampaignEconomy.BeginOperation(s, "x", new[] { "inf", "ig" }, 0, none);
            s.Pending = null;
            check(!locked && okInf, "g6 미해금 야전포 편성 거부 (Pending 없음) · 나머지는 정상");

            // 3) 첫 승리 정산: 표의 미션이면 켜짐 (한 번), 표에 없는 미션은 무영향, 잘못된 키는 무시
            int n0 = CampaignUnlocks.OnWon(s, "c0_01_bandits");
            int n1 = CampaignUnlocks.OnWon(s, "c1e1_bt1"), n2 = CampaignUnlocks.OnWon(s, "c1e1_bt1");
            bool badKey = CampaignUnlocks.Grant(s, "flag.x") || CampaignUnlocks.Grant(s, "");
            check(n0 == 0 && n1 == 1 && n2 == 0 && !badKey && CampaignUnlocks.IsUnlocked(s, "unlock.fieldGun") && CampaignUnlocks.Deployable(s).Count() == 4,
                "g6 BT1 첫 승리 -> unlock.fieldGun 참 (재정산 중복 없음 · 다른 미션/잘못된 키 무영향)");
            bool okFg = CampaignEconomy.BeginOperation(s, "x", new[] { "inf", "fg" }, 0, none);
            s.Pending = null;
            check(okFg, "g6 해금 후 야전포 편성 가능");

            // 4) 정산 경로 (Apply): 표를 갈아 끼워 체인 미션 승리로 시험 - 해금 유지 + 재승리 중복 없음
            var ec = new CampaignEconomyDefs { UnlockOnWin = new() { [CampaignState.FirstMission] = new() { "unlock.fieldGun" } } }.Sanitized();
            CampaignEconomyDefs.Override(ec);
            var s2 = new CampaignState();
            var r = new MissionResults { MissionId = CampaignState.FirstMission, Result = "won" };
            var after = CampaignState.Apply(s2, r);
            check(after != null && CampaignUnlocks.IsUnlocked(after, "unlock.fieldGun"), "g6 정산(Apply) 승리 -> 해금 켜짐");

            // 5) 저장 왕복 · 옛 저장 (키 없음 = 잠김) · 스키마 번호는 그대로 (Flags 재사용)
            var back = CampaignState.FromJson(after!.ToJson());
            var old = CampaignState.FromJson("{\"Version\":8,\"Roster\":[{\"Id\":\"fg\",\"Kind\":\"fieldGun\",\"Strength\":40}]}");
            check(back != null && CampaignUnlocks.IsUnlocked(back, "unlock.fieldGun") && old != null && !CampaignUnlocks.IsUnlocked(old, "unlock.fieldGun") && !CampaignUnlocks.Deployable(old).Any(),
                "g6 저장 왕복 유지 · 옛 저장은 잠김 (스키마 번호 변화 없음)");

            // 6) 깨진 표: unlock. 아닌 값은 버림
            var bad = new CampaignEconomyDefs { UnlockKinds = new() { ["fieldGun"] = "oops", ["x"] = "unlock.x" }, UnlockOnWin = new() { ["m"] = new() { "a", "unlock.b" } } }.Sanitized();
            check(!bad.UnlockKinds.ContainsKey("fieldGun") && bad.UnlockKinds["x"] == "unlock.x" && bad.UnlockOnWin["m"].SequenceEqual(new[] { "unlock.b" }), "g6 표 보정: unlock. 아닌 키 버림");
        }
        finally { CampaignEconomyDefs.Override(saved); }
    }
}
