using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T19a 캠페인 저장 슬롯 9칸 · 자동 저장 (작전 직전 · 정산 직후) · 저장 실패 주입 → 재시도 · 반쪽 파일 0 · 옛 단일 저장 호환.
/// SaveCompatSelfTest 에서 부른다 (KD_SELFTEST_ONLY=savecompat).
/// </summary>
public static class CampaignSlotsSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        string dir = TestTemp.Child("kd_compat_slots");
        string? oldEnv = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
        int oldInject = CampaignSaves.InjectFailures;
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", dir);
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            CampaignSaves.InjectFailures = 0;

            // 옛 단일 저장 (v1 campaign.json, 슬롯 없음) 은 그대로 진행 저장으로 열리고, 슬롯 칸은 모두 비어 있다
            File.WriteAllText(CampaignState.FilePath, "{\"Version\":1,\"NextMission\":\"c1_02_tumen\",\"Completed\":[\"c1_01_hoeryong\"]}");
            var old = CampaignState.Load();
            bool emptySlots = Enumerable.Range(1, CampaignSaves.SlotCount).All(i => !CampaignSaves.Describe(CampaignSaves.SlotPath(i)).Exists)
                && !CampaignSaves.Describe(CampaignSaves.AutoPath(CampaignSaves.Auto.PreOp)).Exists;
            check(old != null && old.NextMission == "c1_02_tumen" && emptySlots, "캠페인 슬롯: 옛 단일 저장(v1)은 진행 저장으로 열리고 슬롯 9칸 · 자동 저장은 비어 있다");

            // 슬롯 9칸 저장 · 불러오기 · 덮어쓰기 (옛 파일에서 온 상태를 그대로 슬롯에)
            int rt = 0;
            for (int i = 1; i <= CampaignSaves.SlotCount; i++)
            {
                old!.Supply = 100 * i;
                if (CampaignSaves.SaveSlot(old, i) && CampaignSaves.Read(CampaignSaves.SlotPath(i), out var er) is { } back && er == null
                    && back.Supply == 100 * i && back.NextMission == "c1_02_tumen" && back.Version == CampaignState.SchemaVersion) rt++;
            }
            old!.Supply = 4321;
            bool over = CampaignSaves.SaveSlot(old, 3) && CampaignSaves.Read(CampaignSaves.SlotPath(3), out _)?.Supply == 4321
                && CampaignSaves.Read(CampaignSaves.SlotPath(4), out _)?.Supply == 400;
            bool outOfRange = !CampaignSaves.SaveSlot(old, 0) && !CampaignSaves.SaveSlot(old, CampaignSaves.SlotCount + 1);
            check(rt == CampaignSaves.SlotCount && over && outOfRange, $"캠페인 슬롯: 9칸 저장 · 불러오기 왕복 {rt}/9, 덮어쓰기는 그 칸만 바꿈 {over}, 0 · 10번 칸 거부 {outOfRange}");

            // 저장 실패 주입: 슬롯 · 진행 저장 · 자동 저장 쌍 모두 false, 이전 내용 그대로, .tmp (반쪽 파일) 0개 → 다시 시도하면 성공
            string before3 = File.ReadAllText(CampaignSaves.SlotPath(3)), beforeLive = File.ReadAllText(CampaignState.FilePath);
            old.Supply = 999;
            CampaignSaves.InjectFailures = 1; bool f1 = !CampaignSaves.SaveSlot(old, 3);
            CampaignSaves.InjectFailures = 1; bool f2 = !old.Save();
            CampaignSaves.InjectFailures = 1; bool f3 = !CampaignSaves.SaveWithAuto(old, CampaignSaves.Auto.PreOp);
            CampaignSaves.InjectFailures = 0;
            int tmps = Directory.GetFiles(dir, "*.tmp").Length;
            bool kept = File.ReadAllText(CampaignSaves.SlotPath(3)) == before3 && File.ReadAllText(CampaignState.FilePath) == beforeLive
                && !File.Exists(CampaignSaves.AutoPath(CampaignSaves.Auto.PreOp));
            CampaignSaves.InjectFailures = 1; bool f4 = !CampaignSaves.SaveWithAuto(old, CampaignSaves.Auto.Settle);   // 정산 직후 쌍도 같은 방식으로 실패 보고
            int tmps2 = Directory.GetFiles(dir, "*.tmp").Length;
            bool retry = CampaignSaves.SaveSlot(old, 3) && CampaignSaves.SaveWithAuto(old, CampaignSaves.Auto.Settle)
                && CampaignSaves.Read(CampaignSaves.SlotPath(3), out _)?.Supply == 999 && CampaignSaves.Read(CampaignSaves.AutoPath(CampaignSaves.Auto.Settle), out _)?.Supply == 999
                && CampaignState.Load()?.Supply == 999;
            check(f1 && f2 && f3 && f4 && kept && tmps == 0 && tmps2 == 0 && retry,
                $"캠페인 슬롯: 저장 실패 주입 → 실패 보고 {f1}/{f2}/{f3}/{f4}, 이전 파일 그대로 {kept}, 반쪽 .tmp {tmps}+{tmps2}개, 다시 시도 성공 {retry}");

            // 재도전: 작전 직전 자동 저장 → (전투 · 정산으로 진행 저장이 바뀜) → 작전 직전 자동 저장으로 진행 저장 복귀
            var pre = CampaignState.Load()!;
            pre.Supply = 1500;
            pre.Pending = new CampaignState.PendingOperation { MissionId = "c1_02_tumen", DeployedCompanyIds = { "b1_c1" } };
            bool preOk = CampaignSaves.SaveWithAuto(pre, CampaignSaves.Auto.PreOp);
            var after = CampaignState.Load()!;
            after.Supply = 200; after.Pending = null; after.Completed.Add("c1_02_tumen");
            bool settleOk = CampaignSaves.SaveWithAuto(after, CampaignSaves.Auto.Settle);
            var back2 = CampaignSaves.Read(CampaignSaves.AutoPath(CampaignSaves.Auto.PreOp), out var e2);
            bool restored = back2 != null && e2 == null && back2.Save() && CampaignState.Load() is { } live
                && live.Supply == 1500 && live.Pending?.MissionId == "c1_02_tumen" && !live.Completed.Contains("c1_02_tumen")
                && CampaignSaves.Read(CampaignSaves.AutoPath(CampaignSaves.Auto.Settle), out _)?.Supply == 200;
            check(preOk && settleOk && restored, "캠페인 슬롯: 작전 직전 자동 저장으로 재도전 복귀 (진행 저장 = 출격 직전 상태, 정산 직후 자동 저장은 따로 남음)");

            // 깨진 · 더 새 버전 슬롯: 열지 않고 사유를 보인다 (크래시 없음)
            File.WriteAllText(CampaignSaves.SlotPath(5), "{\"Version\":1");
            File.WriteAllText(CampaignSaves.SlotPath(6), $"{{\"Version\":{CampaignState.SchemaVersion + 1}}}");
            var d5 = CampaignSaves.Describe(CampaignSaves.SlotPath(5));
            var d6 = CampaignSaves.Describe(CampaignSaves.SlotPath(6));
            check(d5.Exists && d5.Error != null && d6.Exists && d6.Error != null && CampaignSaves.Read(CampaignSaves.SlotPath(6), out _) == null,
                $"캠페인 슬롯: 깨진 · 더 새 버전 슬롯은 사유와 함께 거부 — {d5.Error} / {d6.Error}");
        }
        finally
        {
            CampaignSaves.InjectFailures = oldInject;
            Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", oldEnv);
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
