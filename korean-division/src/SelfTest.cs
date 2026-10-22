using System.Diagnostics;
using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision;

/// <summary>창 없이 돌리는 시뮬레이션 검증 + 성능 측정.  dotnet run -- --selftest</summary>
public static partial class SelfTest
{
    static int failures;
    static Defs defs = null!;

    public static int Run(GameOptions opt)
    {
        failures = 0;
        defs = Defs.Current;
        // 기존(2인) 검증은 100×100 · 2인 고정 — 게임 기본값(10인 · 자동 크기)과 무관하게 같은 조건으로 돈다.
        // N인 맵은 TestMultiplayerMaps 와 AI 의 다인전 테스트에서 따로 검증한다.
        opt = new GameOptions { MapWidth = 100, MapHeight = 100, Players = 2, Seed = opt.Seed, Ai = opt.Ai };
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { }

        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "s4") { Tests.S4SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "assaultinf") { Tests.AssaultInfSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "units") { Tests.UnitsSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "n61") { Tests.N61SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "f4") { Tests.F4SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "armour") { Tests.ArmourSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "factions") { Tests.FactionsSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "lodaudit") { Tests.LodAuditSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "lmgtable") return Tests.LmgTable.Run();
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "lmgaim") { Tests.LmgAimSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "lmgorg") { Tests.LmgOrgSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "type3") { Tests.Type3SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "aim") { Tests.AimSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "cmdext") { Tests.CommandExtSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "supply") { TestCapture(opt); TestCoreV2(opt); Tests.SupplySelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "ai") { Tests.AiSelfTest.Run(opt, Check); Tests.BattleSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "combat4") { Tests.Combat4SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "combat3") { Tests.Combat3SelfTest.Run(opt, Check); Tests.Combat2SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "terrain") { Tests.TerrainSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "weather") { Tests.WeatherSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "mapsizes") { TestMapTiers(opt); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "shells") { Tests.ShellFlightSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "groundfire") { Tests.GroundFireSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "cleanup") { Tests.CleanupSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "engage") { Tests.EngageSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "trench") { Tests.TrenchSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "teams") { Tests.TeamSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "playercmd") { Tests.PlayerCmdSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "squads") { Tests.SquadSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "scouts") { Tests.ScoutSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "hash") { Tests.SpatialHashSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "collision") { Tests.CollisionSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "org") { Tests.OrgSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "missions") { Tests.MissionSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "tactical") { Tests.TacticalSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "savecompat") { Tests.SaveCompatSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "snapshot") { Tests.SnapshotSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "command") { Tests.CommandSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "n73") { Tests.N73SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "pronemove") { Tests.ProneMoveSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "n79a") { Tests.N79aSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "f1") { Tests.F1SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "props") { Tests.PropsSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "f2") { Tests.F2SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "n32") { Tests.N32SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "gapfill") { Tests.GapFillSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "n23") { Tests.N23SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "surr") { Tests.SurrenderSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "supp") { Tests.SuppressionSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "pursuit") { Tests.PursuitSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "officer") { Tests.OfficerSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "s3a") { Tests.S3aSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "cavcmd") { Tests.CavCmdSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "stale") { Tests.StaleSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "ai3b") { Tests.Ai3bSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "progbar") { Tests.ProgBarSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "charge") { Tests.ChargeSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "s2") { Tests.S2SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "pursuit2") { Tests.Pursuit2SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "ai412") { Tests.Ai412SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "train") { Tests.TrainSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }   // T30a 열차
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "replay") { Tests.ReplaySelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "tweak1") { Tests.Tweak1SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "crossing") { Tests.CrossingSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "comms") { Tests.CommsSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "arty3") { Tests.Arty3SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "arty2") { Tests.Arty2SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "forest") { Tests.ForestSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "formrelax") { Tests.FormRelaxSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "choke") { Tests.ChokeSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "bunchfix") { Tests.BunchFixSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "uiicons") { Tests.UiIconsSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "artyai") { Tests.ArtyAiSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "n12") { Tests.N12SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "n77") { Tests.N77SelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "drill") { Tests.TacticDrillSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "formations") { Tests.FormationTemplateSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "human") { Tests.HumanSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        if (Environment.GetEnvironmentVariable("KD_SELFTEST_ONLY") == "form") { Tests.FormationSelfTest.Run(opt, Check); Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})"); return failures == 0 ? 0 : 1; }
        TestMapGeneration(opt);
        TestMultiplayerMaps(opt);
        TestMapTiers(opt);
        TestMapFiles(opt);
        TestMovement(opt);
        TestCombat(opt);
        TestVehicles(opt);
        TestCapture(opt);
        TestVictory(opt);
        TestCoreV2(opt);
        Console.WriteLine("[탄약 회복 · 보급 마차 · 참호 (§0-1)]");
        Tests.SupplySelfTest.Run(opt, Check);
        TestAiMatch(opt);
        TestDeterminism(opt);
        TestCheats(opt);
        Console.WriteLine("[안개]");
        Tests.FogSelfTest.Run(opt, Check);
        Console.WriteLine("[지형 · 높이 · 지형 시야]");
        Tests.TerrainSelfTest.Run(opt, Check);
        Tests.WeatherSelfTest.Run(opt, Check);
        Console.WriteLine("[참호 자동 점유 · 포탄 구덩이]");
        Tests.TrenchSelfTest.Run(opt, Check);
        Tests.EngageSelfTest.Run(opt, Check);   // 교전 거리 · 반격 · 진지 점유 · 엄폐 찾기 (engage)
        Console.WriteLine("[정리 묶음 2: 장교 · 견인포 회전 · 머리띠 · 소리]");
        Tests.CleanupSelfTest.Run(opt, Check);
        Console.WriteLine("[정적 팀]");
        Tests.TeamSelfTest.Run(opt, Check);
        Console.WriteLine("[AI]");
        Tests.AiSelfTest.Run(opt, Check);
        Tests.TacticalSelfTest.Run(opt, Check);   // 전술 AI 1단계 (⑤)
        Console.WriteLine("[명령 · 리플레이]");
        Tests.ReplaySelfTest.Run(opt, Check);
        Tests.TrainSelfTest.Run(opt, Check);   // T30a 열차
        Console.WriteLine("[기본 4인 맵 · 세력 선택 · 부대 지정]");
        Tests.FactionsSelfTest.Run(opt, Check);
        Tests.ArmourSelfTest.Run(opt, Check);
        Tests.S4SelfTest.Run(opt, Check);
        Tests.F4SelfTest.Run(opt, Check);
        Tests.N61SelfTest.Run(opt, Check);
        Tests.UnitsSelfTest.Run(opt, Check);
        Tests.AssaultInfSelfTest.Run(opt, Check);
        Tests.NavSelfTest.Run(opt, Check);
        Tests.FormationSelfTest.Run(opt, Check);
        Tests.OrgSelfTest.Run(opt, Check);
        Tests.SpatialHashSelfTest.Run(opt, Check);   // N16 공간 해시 대 무차별 기준
        Tests.CollisionSelfTest.Run(opt, Check);   // 충돌 · 대형 재설계 · 중대 행진 사격 (docs/COLLISION_ANALYSIS.md)
        Tests.PlayerCmdSelfTest.Run(opt, Check);   // N36 섞인 돌격 · N37 선택 인원 지연 · N38 철조망 사선
        Tests.SquadSelfTest.Run(opt, Check);   // 분대 · 부사관 · 계급 · 빈자리 점검 · 지휘 계통 지연 (④)
        Tests.FormationTemplateSelfTest.Run(opt, Check);   // 전투 대형 · 역할군 · 중대 전술 틀 1단계
        Tests.TacticDrillSelfTest.Run(opt, Check);         // 중대 전술 틀 2단계 (행동 · 전환 · 지휘관)
        Tests.HumanSelfTest.Run(opt, Check);               // N34 대형 반응 · 포 발사 배수 · 첫 사격 반응
        Tests.N12SelfTest.Run(opt, Check);                 // N12 대형 강박 · 장교 위치 · 정찰병 묶음
        Tests.N77SelfTest.Run(opt, Check);                 // N77 미션 전체 로드
        Tests.GapFillSelfTest.Run(opt, Check);             // 대열 정비 (명령 · 장교 판단 · 숙련)
        Tests.F1SelfTest.Run(opt, Check);                  // F1 접적 · 구역 · 사격 위치 고정
        Tests.PropsSelfTest.Run(opt, Check);               // F6 엄폐물 소품 층
        Tests.F2SelfTest.Run(opt, Check);                  // F1 2차 제자리 방향 전환 · 선까지 전진 · 대형 유지 · AI 재명령 제한
        Tests.N32SelfTest.Run(opt, Check);
        Tests.N73SelfTest.Run(opt, Check);                 // N73 사격 통제
        Tests.N79aSelfTest.Run(opt, Check);                // N79a 집결 지점 · 포격 종류
        Tests.ProneMoveSelfTest.Run(opt, Check);         // pronemove 엎드림 이동
        Tests.SurrenderSelfTest.Run(opt, Check);          // N79b 항복
        Tests.SuppressionSelfTest.Run(opt, Check);        // 제압 재설계 N25 (근접 탄착 · 명중률 · 고정 · 장교 제압 후퇴)
        Tests.N23SelfTest.Run(opt, Check);                // N23 돌격보병 소탕 · N55 장교 경험 성향
        Tests.PursuitSelfTest.Run(opt, Check);            // 추격 (후퇴하는 적 돌격 금지 · 사격 추격 한계 · 후퇴 속보) · 기병 측면 · 소규모 기병
        Tests.StaleSelfTest.Run(opt, Check);              // ④ 교착 해소 (stale)
        Tests.Ai3bSelfTest.Run(opt, Check);               // 전술 AI 3B 방어 · 예비 · 역습 (ai3b)
        Tests.ProgBarSelfTest.Run(opt, Check);            // 일본군 처음부터 착검 (progbar)
        Tests.ChargeSelfTest.Run(opt, Check);             // N46 분대 따라 쏘기 · N47 행진 돌격 허가 · N57 착검 (charge)
        Tests.Pursuit2SelfTest.Run(opt, Check);           // 후퇴 사격 · 후퇴 뒤 재정비 · 포병만 아군 오사 · 미션 배치 겹침 (pursuit2)
        Tests.S3aSelfTest.Run(opt, Check);                // 전술 AI 3단계 A: 기동 계획 (측면 · 협공 · 중점) · 고정 → 측면 동기 · 미션 목표 → 과제 · 미션 철수 문턱
        Tests.ForestSelfTest.Run(opt, Check);             // 숲 전투 (시야 깊이 · 방어 이점 · 정찰조 숲 탐색)
        Tests.FormRelaxSelfTest.Run(opt, Check);           // 대형 완화 (짧은 이동 평행이동 · 산개 파상 횡대 · 휴식 정렬)
        Tests.ChokeSelfTest.Run(opt, Check);               // choke 좁은 길목 통과 (큐 · 종대 유지 · 저장)
        Tests.BunchFixSelfTest.Run(opt, Check);            // bunchfix 행군 종대 뭉침 (자취 외삽 · 좁은 길 문턱 · 막힌 슬롯 흩뿌림 · 배치 지도 안)
        Tests.Ai412SelfTest.Run(opt, Check);              // 결정 4 돌격 허가 · 결정 12 대대 역할 · 부사관 기본 편성
        Tests.CavCmdSelfTest.Run(opt, Check);             // cavcmd 기병 장교 · 기병 분대 부사관 대리
        Tests.S2SelfTest.Run(opt, Check);                 // S2 소형 중대 부사관 분대장
        Tests.OfficerSelfTest.Run(opt, Check);            // N35 장교 교전 회피 · 정찰병 자위 사격
        Tests.CrossingSelfTest.Run(opt, Check);            // 도하 · 병목 돌파 · 역습 교리 (2026-10-19)
        Tests.ArtyAiSelfTest.Run(opt, Check);              // N20 포병 AI (관측자 · 준비사격 · 돌격 지원 · 재배치 쿨다운 · 직사포 사거리)
        Tests.CommsSelfTest.Run(opt, Check);               // comms 통신 · 난이도 조각 ① (전달 지연 · 분실 · 저장 · 리플레이)
        Tests.Arty3SelfTest.Run(opt, Check);               // arty3 포병 · 중화기 묶음 (방렬 난수 · 자리 유지 · heavy 간격 · 야전포 간접 · 조정 사격 · 최후 저지선)
        Tests.Arty2SelfTest.Run(opt, Check);               // N22 보병포 동행 · 사선 막힘 재배치 · 정찰병 시야 48 · 피격 엄폐
        Tests.Tweak1SelfTest.Run(opt, Check);              // tweak1 대열 정비 화력조 제외 · 돌격 중 보류 · 방어 유닛만 엄폐 · 참호 0.5 · 보병포 · 관측자 노출 회피 · 야전포 방열
        Tests.UiIconsSelfTest.Run(opt, Check);              // 수류탄 X · 아군 오사 계수 · 분대 아이콘 우선순위
        Tests.CommandSelfTest.Run(opt, Check);   // 지휘 계급 · 승계 · 장교 경험치 (N3)
        Tests.ScoutSelfTest.Run(opt, Check);   // 정찰병 (정찰조 · 자율 행동 · 곡사포 관측)
        Tests.CommandExtSelfTest.Run(opt, Check);
        Console.WriteLine("[결정성 · 사격 지연]");
        Tests.DetCheck.SelfTest(Check);
        Tests.AimSelfTest.Run(opt, Check);
        Console.WriteLine("[전투 v0.4: 명중 곡선 · 경계 · 은폐 · 포병]");
        Tests.Combat2SelfTest.Run(opt, Check);
        Console.WriteLine("[포탄 비행 시간: 지점 착탄 · 저장 · 리플레이]");
        Tests.ShellFlightSelfTest.Run(opt, Check);
        Console.WriteLine("[플레이어 지점 포격: 관측자 없이 지정 지점 · 사계 · 취소 · AI 미사용 · 저장]");
        Tests.GroundFireSelfTest.Run(opt, Check);
        Console.WriteLine("[교전 2차: 표적 분산 · 제압 · 계급 · 돌격 · 기관총 · 멍칫]");
        Tests.Combat3SelfTest.Run(opt, Check);
        Console.WriteLine("[경기관총 사격 방향 · 스프라이트 정합 (N2)]");
        Tests.LmgAimSelfTest.Run(opt, Check);
        Tests.LmgOrgSelfTest.Run(opt, Check);
        Tests.Type3SelfTest.Run(opt, Check);
        Console.WriteLine("[교전 4차: 병종 · 포병 · 수류탄 · 행진 사격 · 기병]");
        Tests.Combat4SelfTest.Run(opt, Check);
        Console.WriteLine("[교전 테스트 · 세력 언어 · 밸런스 도구]");
        Tests.BattleSelfTest.Run(opt, Check);
        Console.WriteLine("[미션 틀: 목표 · 사건 · 배치 · 저장 · 리플레이]");
        Tests.MissionSelfTest.Run(opt, Check);
        Console.WriteLine("[저장 · 불러오기 (스냅샷)]");
        Tests.SnapshotSelfTest.Run(opt, Check);
        Console.WriteLine("[저장 형식 호환 (옛 버전 · 손상 파일 거부)]");
        Tests.SaveCompatSelfTest.Run(opt, Check);
        TestPerformance(opt);

        Console.WriteLine(failures == 0 ? "SELFTEST PASS" : $"SELFTEST FAIL ({failures})");
        return failures == 0 ? 0 : 1;
    }

    static World NewWorld(GameOptions opt, int? w = null, int? h = null)
    {
        // 기존 검증은 시작부터 전원 전쟁(자유전) — 외교 규칙은 [외교] 묶음에서 따로 검증
        var world = World.CreateStandard(GameMap.Generate(w ?? opt.MapWidth, h ?? opt.MapHeight, opt.Seed), defs);
        world.Fog.Enabled = false;   // 기존 검증은 안개 없이 (안개는 [안개] 묶음에서 따로 검증)
        return world;
    }

    static void Run(World w, float seconds)
    {
        for (int i = 0; i < (int)MathF.Round(seconds / Config.SimDt); i++) w.Step();
    }

    static Building Hq(World w, int f) => w.Buildings.First(b => b.Owner == f && b.Def.Headquarters);

    // ----------------------------------------------------------------

    static void TestMapGeneration(GameOptions opt)
    {
        Console.WriteLine("[맵]");
        foreach (var (mw, mh) in new[] { (48, 48), (100, 100), (160, 120), (256, 256) })
        {
            var map = GameMap.Generate(mw, mh, opt.Seed);
            bool sym = true;
            for (int y = 0; y < mh && sym; y++)
            for (int x = 0; x < mw; x++)
                if (map.Get(x, y) != map.Get(mw - 1 - x, mh - 1 - y)) { sym = false; break; }
            var w = World.CreateStandard(map, defs);
            var (sx, sy) = map.StartTiles[0];
            var field = new FlowField(map, new Vector2(sx + 0.5f, sy + 0.5f) + new Vector2(0, 3));
            bool connected = w.Buildings.All(b => Neighbors(b).Any(t => field.IsReachable(t.X, t.Y)));
            int hqCount = w.Buildings.Count(b => b.Def.Headquarters), siteCount = w.Buildings.Count(b => b.Def.Capturable && !b.Def.Headquarters);
            bool siteSym = map.ResourceSites.Count % 2 == 0 || map.ResourceSites.Count(r => r.Kind == "city") % 2 == 1;   // 쌍 + (중앙 하나)
            foreach (var r in map.ResourceSites)
            {
                int sz = GameMap.SiteSize(r.Kind);
                bool hasMirror = map.ResourceSites.Any(m => m.Kind == r.Kind && m.X == mw - r.X - sz && m.Y == mh - r.Y - sz);
                if (!hasMirror) siteSym = false;
            }
            Check(sym && siteSym && hqCount == 2 && siteCount >= (mw < 80 ? 6 : 14) && connected && MinSiteGap(w) >= 9,
                $"맵 {mw}x{mh}: 지형 대칭 {sym}, 시설 대칭 {siteSym}, 대도시 {hqCount} + 시설 {siteCount}, 모두 연결 {connected}, 시설 최소 간격 {MinSiteGap(w):0.0}");
        }
    }

    /// <summary>지도 크기 5단계 (Config.MapTiers, 2026-10-19): 값 · 인원 → 단계 · 이름, 각 단계 생성 지도 + 표준 월드 + 60틱 진행 (크기 · 연결 · 시간). `KD_SELFTEST_ONLY=mapsizes`.</summary>
    static void TestMapTiers(GameOptions opt)
    {
        Console.WriteLine("[지도 크기 5단계]");
        Check(Config.MapTiers.SequenceEqual(new[] { 200, 400, 800, 1200, 1500 }) && Config.MaxMapSize >= 1500,
            $"지도 크기 단계: {string.Join(" · ", Config.MapTiers)} (상한 {Config.MaxMapSize})");
        Check(Config.MapTierFor(2) == 200 && Config.MapTierFor(3) == 400 && Config.MapTierFor(8) == 400 && Config.MapTierFor(9) == 800 && Config.MapTierFor(16) == 800,
            $"인원 → 자동 단계: 2인 {Config.MapTierFor(2)} · 3인 {Config.MapTierFor(3)} · 8인 {Config.MapTierFor(8)} · 9인 {Config.MapTierFor(9)} · 16인 {Config.MapTierFor(16)}");
        Check(Config.MapTierByName("tiny") == 200 && Config.MapTierByName("HUGE") == 1500 && Config.MapTierByName("중형") == 800 && Config.MapTierByName("대형") == 1200 && Config.MapTierByName("x") == null,
            "단계 이름 (명령줄 --map tiny|small|medium|large|huge, 한글 이름)");
        Check(Config.MapSizeFor(2) == 150 && Config.MapSizeFor(16) == 424, $"옛 인원 공식 (테스트 · 명령줄 기본) 그대로: 2인 {Config.MapSizeFor(2)} · 16인 {Config.MapSizeFor(16)}");
        foreach (var (size, players) in new[] { (200, 2), (400, 4), (800, 8), (1200, 2), (1500, 16) })
        {
            var sw = Stopwatch.StartNew();
            var map = GameMap.Generate(size, size, opt.Seed, players);
            var w = World.CreateStandard(map, defs);
            double genMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            for (int i = 0; i < 60; i++) w.Step();
            double stepMs = sw.Elapsed.TotalMilliseconds / 60;
            int hq = w.Buildings.Count(b => b.Def.Headquarters);
            var starts = map.StartTiles;
            bool inside = starts.All(s => s.X > 0 && s.Y > 0 && s.X < size - 1 && s.Y < size - 1);
            Check(map.Width == size && map.Height == size && hq == players && inside && w.Units.Count > 0,
                $"{size}² ({players}인): 대도시 {hq}, 시작 위치 지도 안 {inside}, 유닛 {w.Units.Count}, 생성 {genMs:0} ms · 틱 평균 {stepMs:0.00} ms");
        }
    }

    static void TestMultiplayerMaps(GameOptions opt)
    {
        Console.WriteLine("[N인 맵]");
        Check(Config.MapSizeFor(2) == 150 && Config.MapSizeFor(10) == 336 && Config.MapSizeFor(16) <= Config.MaxMapSize,
            $"인원별 맵 크기: 2인 {Config.MapSizeFor(2)}, 10인 {Config.MapSizeFor(10)}, 16인 {Config.MapSizeFor(16)}");
        foreach (int n in new[] { 3, 10, 16 })
        {
            int size = Config.MapSizeFor(n);
            var sw = Stopwatch.StartNew();
            var map = GameMap.Generate(size, size, opt.Seed, n);
            var w = World.CreateStandard(map, defs);
            double genMs = sw.Elapsed.TotalMilliseconds;
            int hqs = w.Buildings.Count(b => b.Def.Headquarters);
            var hqOwners = w.Buildings.Where(b => b.Def.Headquarters).Select(b => b.Owner).Distinct().Count();
            int sites = w.Buildings.Count(b => b.Def.Capturable);
            int owned = w.Buildings.Count(b => b.Def.Capturable && b.Owner != 0);
            var (sx, sy) = map.StartTiles[0];
            var field = new FlowField(map, new Vector2(sx + 0.5f, sy + 3.5f));
            bool connected = w.Buildings.All(b => Neighbors(b).Any(t => field.IsReachable(t.X, t.Y)));
            float minGap = float.MaxValue;
            var hqList = w.Buildings.Where(b => b.Def.Headquarters).ToList();
            for (int i = 0; i < hqList.Count; i++)
            for (int j = i + 1; j < hqList.Count; j++)
                minGap = MathF.Min(minGap, Vector2.Distance(hqList[i].Pos, hqList[j].Pos));
            int forceN = defs.Start.Force.Sum(g => g.Units.Values.Sum());
            bool workers = Enumerable.Range(1, n).All(f => w.Units.Count(u => u.Owner == f) == forceN);   // 세력마다 기본 병력 (start.force)
            bool econ = true;
            var kinds = map.ResourceSites.GroupBy(r => r.Kind).ToDictionary(g => g.Key, g => g.Count());
            int K(string k) => kinds.GetValueOrDefault(k);
            bool allKinds = K("city") >= n / 2 && K("village") >= n;
            Check(w.Factions.Count == n + 1 && hqs == n && hqOwners == n && sites - n >= 4 * n && owned == n && connected && workers && econ && allKinds,
                $"{n}인 {size}×{size}: 대도시 {hqs}, 점령 시설 {sites - n} (소도시 {K("city")} · 마을 {K("village")}, 시작 소유 {owned}), 연결 {connected}, 기본 병력 {workers}, 대도시 최소 간격 {minGap:0}타일, 생성 {genMs:0}ms");
            Check(minGap >= 20 && MinSiteGap(w) >= 9, $"{n}인: 대도시끼리 20타일 이상, 모든 시설 중심 간격 9 이상 ({minGap:0} / {MinSiteGap(w):0.0})");
        }
        var r1 = GameMap.Generate(224, 224, opt.Seed, 10);
        var r2 = GameMap.Generate(224, 224, opt.Seed, 10);
        bool same = r1.StartTiles.SequenceEqual(r2.StartTiles) && r1.ResourceSites.SequenceEqual(r2.ResourceSites);
        for (int y = 0; y < 224 && same; y++)
        for (int x = 0; x < 224; x++)
            if (r1.Get(x, y) != r2.Get(x, y)) { same = false; break; }
        Check(same, "같은 시드 · 인원이면 같은 맵 (리플레이 재현 조건)");
    }

    static void TestMapFiles(GameOptions opt)
    {
        Console.WriteLine("[고정 지도]");
        var ids = MapFile.Available().ToList();
        Check(ids.Contains("flat4"), $"지도 파일 목록: {string.Join(", ", ids)}");
        var map = MapFile.Load("flat4", opt.Seed);
        var w = World.CreateStandard(map, defs);
        w.Fog.Enabled = false;
        int blocked = 0;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            if (!map.IsTerrainWalkable(x, y)) blocked++;
        var hqs = w.Buildings.Where(b => b.Def.Headquarters).OrderBy(b => b.Owner).ToList();
        bool sym = hqs.Count == 4 && Enumerable.Range(0, 4).All(i => Vector2.Distance(hqs[i].Pos, new Vector2(96, 96)) is var d && MathF.Abs(d - Vector2.Distance(hqs[0].Pos, new Vector2(96, 96))) < 0.01f);
        Check(map.Players == 4 && map.Width == 192 && blocked == 0 && sym, $"flat4: 4인 · 192² · 막힌 칸 {blocked} · 사령부 중앙에서 같은 거리 {sym}");
        int owned = w.Buildings.Count(b => b.Def.Capturable && b.Owner != 0);
        int Of(string k) => w.Buildings.Count(b => b.Def.Key == k);
        Check(w.Buildings.Count(b => b.Def.Capturable) == 32 && owned == 4 && Of("metropolis") == 4 && Of("city") == 4 && Of("village") == 24,
            $"flat4: 대도시 {Of("metropolis")} (시작 소유 {owned}) + 소도시 {Of("city")} · 마을 {Of("village")}");
        Check(MinSiteGap(w) >= 10, $"flat4: 시설 중심 최소 간격 {MinSiteGap(w):0.0}");
        int force = defs.Start.Force.Sum(g => g.Units.Values.Sum());
        Check(Enumerable.Range(1, 4).All(f => w.Units.Count(u => u.Owner == f) == force), $"flat4: 세력마다 기본 병력 {force}기 (start.force)");
        bool threwType = false;
        try
        {
            var badFile = new MapFile { Width = 64, Height = 64, Starts = { new[] { 10, 10 }, new[] { 50, 50 } }, Sites = { new Site { At = new[] { 30, 30 }, Type = "barracks" } } };
            typeof(MapFile).GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(badFile, new object[] { 1 });
        }
        catch (System.Reflection.TargetInvocationException e) when (e.InnerException is InvalidDataException) { threwType = true; }
        Check(threwType, "지도 파일: capturable 이 아닌 site.type 은 오류");
        // 리플레이: 지도 id 로 재현
        w.Controllers.Add((1, AiRegistry.Create("standard")!));
        w.Controllers.Add((2, AiRegistry.Create("rush")!));
        for (int i = 0; i < 60 * 60 * 3; i++) w.Step();
        var rep = ReplayFile.FromWorld(w);
        long bad = rep.Verify(defs, out var report);
        Check(rep.MapId == "flat4" && bad < 0, $"고정 지도 리플레이 재현 ({report})");
        bool threw = false;
        try { MapFile.Load("no_such_map", 1); } catch (FileNotFoundException) { threw = true; }
        Check(threw, "없는 지도는 명확한 오류");
    }

    /// <summary>점령 시설(대도시 포함) 중심끼리 최소 거리.</summary>
    static float MinSiteGap(World w)
    {
        var caps = w.Buildings.Where(b => b.Def.Capturable).ToList();
        float m = float.MaxValue;
        for (int i = 0; i < caps.Count; i++)
        for (int j = i + 1; j < caps.Count; j++)
            m = MathF.Min(m, Vector2.Distance(caps[i].Pos, caps[j].Pos));
        return m;
    }

    static IEnumerable<(int X, int Y)> Neighbors(Building b)
    {
        for (int i = -1; i <= b.Size; i++)
        {
            yield return (b.TileX + i, b.TileY - 1);
            yield return (b.TileX + i, b.TileY + b.Size);
            yield return (b.TileX - 1, b.TileY + i);
            yield return (b.TileX + b.Size, b.TileY + i);
        }
    }

    static void TestMovement(GameOptions opt)
    {
        Console.WriteLine("[이동]");
        var w = NewWorld(opt);
        var u = w.SpawnUnit(defs.Units["infantry"], 1, Hq(w, 1).Pos + new Vector2(4, -4));
        Vector2 target = u.Pos + new Vector2(20, -20);
        w.Move(1, new[] { u }, target);
        w.FlushCommands();   // 명령은 다음 틱 시작에 실행되므로, 즉시 효과를 확인할 때는 직접 실행
        Vector2 goal = u.Destination!.Value;
        bool walkable = true;
        int ticks = 0;
        while (u.Order == UnitOrder.Move && ticks++ < 60 * 60)
        {
            w.Step();
            walkable &= w.Map.IsWalkable(GameMap.TileOf(u.Pos).X, GameMap.TileOf(u.Pos).Y);
        }
        Check(u.Order == UnitOrder.Idle && Vector2.Distance(u.Pos, goal) < 0.2f, $"단일 유닛 목표 도착 ({ticks / 60f:0.0}초)");
        Check(walkable, "이동 중 막힌 타일 · 건물에 들어가지 않음");
        w.Hold(1, new[] { u });
        w.FlushCommands();
        Check(u.Order == UnitOrder.Hold, "사수 명령");
        w.QuickStep(1, new[] { u }, u.Pos + new Vector2(4, 0));
        Run(w, 0.5f);
        Check(u.Order == UnitOrder.Move && u.QuickStep, "속보는 이동 명령으로 유지됨");
        w.Move(2, new[] { u }, u.Pos + Vector2.One);
        w.FlushCommands();
        Check(u.Order == UnitOrder.Move && u.QuickStep, "남의 세력 유닛에는 명령 불가");
    }

    static void TestDeterminism(GameOptions opt)
    {
        Console.WriteLine("[결정성]");
        ulong RunMatch(out List<ulong> trace)
        {
            var w = NewWorld(opt);
            w.Fog.Enabled = true;
            w.Controllers.Add((1, AiRegistry.Create("standard")!));
            w.Controllers.Add((2, AiRegistry.Create("rush")!));
            trace = new List<ulong>();
            for (int i = 0; i < 60 * 60 * 6; i++)
            {
                w.Step();
                if (i % 600 == 0) trace.Add(w.ComputeChecksum());
            }
            return w.ComputeChecksum();
        }
        ulong a = RunMatch(out var ta), b = RunMatch(out var tb);
        int firstDiff = ta.Zip(tb).TakeWhile(p => p.First == p.Second).Count();
        Check(a == b && ta.SequenceEqual(tb), $"같은 시드 · 같은 AI 6분 대전 2회: 체크섬 일치 {a:X16}" + (a == b ? "" : $" (처음 어긋난 지점 {firstDiff * 10}초)"));
        var w3 = NewWorld(new GameOptions { MapWidth = opt.MapWidth, MapHeight = opt.MapHeight, Seed = opt.Seed + 1 });
        Check(w3.ComputeChecksum() != NewWorld(opt).ComputeChecksum(), "다른 시드면 체크섬도 다름");
    }

    static void TestCheats(GameOptions opt)
    {
        Console.WriteLine("[치트 명령]");
        var map = GameMap.Generate(100, 100, opt.Seed);
        var w = World.CreateStandard(map, defs);
        var hq2 = w.Buildings.First(b => b.Owner == 2 && b.Def.Headquarters);
        Check(!w.Fog.CanSee(1, hq2) && !w.CheatsUsed, "치트 전: 적 본진 안 보임");
        w.Issue(new RevealMapCommand(1, true));
        w.Step();
        Check(w.Fog.CanSee(1, hq2) && w.Fog.IsExplored(1, 0, 0) && w.CheatsUsed, "show all maps: 내 세력은 지도 전체를 봄 · 치트 사용 표시");
        Check(!w.Fog.CanSee(2, w.Buildings.First(b => b.Owner == 1 && b.Def.Headquarters)), "AI(다른 세력) 시야는 그대로");
        int before = w.Units.Count(u => u.Owner == 1 && u.Def.Key == "infantry");
        w.Issue(new DebugSpawnCommand(1, "infantry", hq2.Pos, 3));
        w.Step();
        Check(w.Units.Count(u => u.Owner == 1 && u.Def.Key == "infantry") >= before + 3, "spawn 치트 동작");
        w.Issue(new RevealMapCommand(1, false));
        w.Step();
        Check(!w.Fog.CanSee(1, hq2) || w.Units.Any(u => u.Owner == 1 && Vector2.Distance(u.Pos, hq2.Pos) < 12), "show all maps 해제 → 다시 안개");
        var rep = ReplayFile.FromWorld(w);
        long bad = rep.Verify(defs, out var report);
        Check(rep.CheatsUsed && bad < 0, $"치트가 리플레이에 기록되고 재현됨 ({report})");
    }

    static void TestCombat(GameOptions opt)
    {
        Console.WriteLine("[전투]");
        var w = NewWorld(opt);
        var inf = defs.Units["infantry"];
        Vector2 c = new(w.Map.Width / 2f, w.Map.Height / 2f);
        var (cx, cy) = w.Map.NearestWalkable((int)c.X, (int)c.Y);
        c = new Vector2(cx + 0.5f, cy + 0.5f);
        // v0.4: 평소 시야(sightNear 7)가 소총 사거리(8)보다 짧으므로 5칸 거리에서 시작 (경계 전에는 유효 시야 안의 적만 식별). 체력 100 → 30초.
        var a = Enumerable.Range(0, 6).Select(i => w.SpawnUnit(inf, 1, c + new Vector2(-2.5f, i * 0.8f - 2))).ToList();
        var b = Enumerable.Range(0, 3).Select(i => w.SpawnUnit(inf, 2, c + new Vector2(2.5f, i * 0.8f - 1))).ToList();
        Run(w, 30);
        int aAlive = a.Count(u => u.Alive), bAlive = b.Count(u => u.Alive);
        Check(bAlive == 0 && aAlive > 0, $"보병 6 vs 3 자동 교전: 아군 {aAlive}기 생존, 적 {bAlive}기 생존");

        // 명중률: 사격 횟수 대비 명중(피해 발생) 비율 — v0.4 거리 곡선 × 유닛 명중률 (World.HitChance 가 기대값)
        var w2 = NewWorld(opt);
        var shooter = w2.SpawnUnit(inf, 1, c);
        var dummy = w2.SpawnUnit(inf, 2, c + new Vector2(3, 0));
        w2.Hold(2, new[] { dummy });
        dummy.Weapon = null;
        int shots = 0, hits = 0;
        for (int i = 0; i < 60 * 900; i++)   // 발사 간격 1.8~2.4초 → 15분에 ≈430발 (표본 오차 ≈ ±2.2%). 탄약(100발)은 매 틱 채운다
        {
            dummy.Hp = 1000;
            shooter.Ammo = shooter.Weapon!.Ammo;
            w2.Step();
            int fired = w2.Shots.Count(s => s.Tick == w2.Tick - 1 && s.Faction == 1);
            shots += fired;
            if (fired > 0 && dummy.Hp < 1000) hits++;
        }
        double rate = shots > 0 ? (double)hits / shots : 0;
        float acc = w2.HitChance(shooter, dummy);
        Check(Math.Abs(rate - acc) < 0.05, $"기본 소총 명중률 {rate * 100:0.0}% (기대 {acc * 100:0.0}% = 근거리 곡선 × 정규군 1.0, 사격 {shots}회)");

    }

    static void TestVehicles(GameOptions opt)
    {
        Console.WriteLine("[전차 · 대전차]");
        var tank = defs.Units["tank_ft_male"];
        // 병종 상성: 정규군 소총(6) 은 중장갑(3) 전차에 거의 못 통하고, 대전차보병(바주카 ×3.5)은 전차를 잡는다.
        // v0.4 기준 재산정: 체력 100/400 · 거리 곡선. 4칸 거리에서 시작 (전차 평소 시야 6 · 보병 7 — 8칸이면 서로 식별 못 함).
        //   보병 3 → 전차: 3 × (6−3) × 명중 ≈0.63 / 0.6초 ≈ 9.5/초 → 400 체력에 42초, 전차 → 보병: 29 × 0.85 / 2.4초 ≈ 10/초 → 보병 1기당 10초.
        //   보병이 줄며 피해량이 떨어져 60초 안에 전차가 이긴다. 대전차 3 → 전차: 3 × (35−3) × 0.8 / 3초 ≈ 25/초 → 16초지만
        //   전차가 10초마다 대전차 1기를 잡고 바주카 탄약이 8발(3기 24발 · 명중 ~0.75 → 18발 × 32 = 576)이라 실측 3/6 (v0.4 초기값). 밸런스 도구에서 볼 항목 — 여기서는 "대전차가 전차와 대등 이상(≥3/6)" 만 확인.
        int infWins = 0, tankWins = 0, atWins = 0;
        for (int trial = 0; trial < 6; trial++)
        {
            var b = NewWorld(new GameOptions { MapWidth = 100, MapHeight = 100, Players = 2, Seed = opt.Seed + trial });
            Vector2 c = new(50, 50);
            var (cx, cy) = b.Map.NearestWalkable(50, 50);
            c = new Vector2(cx + 0.5f, cy + 0.5f);
            var infs = Enumerable.Range(0, 3).Select(i => b.SpawnUnit(defs.Units["infantry"], 1, c + new Vector2(-2, i - 1))).ToList();
            var t = b.SpawnUnit(tank, 2, c + new Vector2(2, 0));
            Run(b, 60);
            if (!t.Alive && infs.Any(u => u.Alive)) infWins++;
            else if (t.Alive && infs.All(u => !u.Alive)) tankWins++;
            var b2 = NewWorld(new GameOptions { MapWidth = 100, MapHeight = 100, Players = 2, Seed = opt.Seed + trial });
            var ats = Enumerable.Range(0, 3).Select(i => b2.SpawnUnit(defs.Units["heavy_at_rifleman"], 1, c + new Vector2(-2, i - 1))).ToList();
            var t2 = b2.SpawnUnit(tank, 2, c + new Vector2(2, 0));
            Run(b2, 60);
            if (!t2.Alive && ats.Any(u => u.Alive)) atWins++;
        }
        // 2026-10-18 소총 피해 40 (전차 장갑 3 → 1발 37) 이후 보병 3 이 전차를 이기는 경우가 잦다 — 다른 무기 피해 재조정은 ROADMAP §2-4 미결이라 참고 정보로만 둔다.
        Console.WriteLine($"  [info] (참고 · 판정 제외) 보병 3 vs 전차 1: 전차 승 {tankWins}/6 (보병 승 {infWins}) · 대전차보병 3 vs 전차 1: 대전차 승 {atWins}/6 — 밸런스 보고서 항목");
        Check(atWins >= 3, $"대전차보병 3 vs 전차 1: 대전차 승 {atWins}/6 (3 이상)");

    }

    static void TestCapture(GameOptions opt)
    {
        Console.WriteLine("[점령]");
        var w = NewWorld(opt);
        var neutral = w.Buildings.Where(b => b.Def.Capturable && b.Owner == 0).OrderBy(b => Vector2.Distance(b.Pos, Hq(w, 1).Pos)).First();
        var inf = defs.Units["infantry"];
        var u = w.SpawnUnit(inf, 1, neutral.Pos + new Vector2(0, neutral.Size / 2f + 1.5f));
        w.Hold(1, new[] { u });
        float capTime = neutral.Def.CaptureTime > 0 ? neutral.Def.CaptureTime : defs.Capture.Time;
        Run(w, capTime + 0.5f);
        Check(neutral.Owner == 1, $"보병 1기로 {capTime}초 만에 중립 {neutral.Def.Name} 점령");

        var keeper = w.Units.Where(o => o.Owner == 1 && o != u && o.Def.IsInfantry).OrderByDescending(o => Vector2.DistanceSquared(o.Pos, neutral.Pos)).ThenBy(o => o.Id).First();
        keeper.Weapon = null; int keep1 = keeper.Id;
        foreach (var other in w.Units.Where(o => o.Owner == 1 && o != u && o.Id != keep1)) other.Hp = 0;   // 시작 유닛이 끼어들어 적을 쓰러뜨리지 않게 (전멸 패배 방지용 1기는 남김)
        var e = w.SpawnUnit(inf, 2, neutral.Pos + new Vector2(0, -(neutral.Size / 2f + 1.5f)));
        w.Hold(2, new[] { e });
        e.Weapon = null; u.Weapon = null;   // 싸우지 않고 경합만
        Run(w, 3);
        Check(neutral.Contested && neutral.Owner == 1, "양측이 있으면 경합 — 소유 유지");
        u.Hp = 0;
        Run(w, capTime * 2 + 1);
        Check(neutral.Owner == 2, $"적이 게이지를 깎고 재점령 (소유 {neutral.Owner}, 게이지 {neutral.CaptureProgress:0.00}, 점령측 {neutral.CaptureFaction}, 적 생존 {e.Alive})");
        Check(!neutral.Alive == false && neutral.Indestructible, "자원 시설은 파괴 불가");
    }

    static void TestVictory(GameOptions opt)
    {
        Console.WriteLine("[승패]");
        var w = NewWorld(opt);
        Neutralize(Hq(w, 2));
        Run(w, 1);
        Check(w.Winner == 1 && w.Faction(2).Defeated, "적 도시를 모두 잃게 하면 승리 (도시 0 → 탈락)");

        var w2 = NewWorld(opt);
        var city = w2.Buildings.Where(b => b.Def.City && !b.Def.Headquarters).OrderBy(b => b.Id).First();
        city.Owner = 2; city.CaptureFaction = 2; city.CaptureProgress = 1;
        Thin(w2, 2);
        Neutralize(Hq(w2, 2));
        Run(w2, 1);
        Check(w2.Winner == 0 && !w2.Faction(2).Revealed, "대도시를 잃어도 소도시가 있으면 계속 진행");
        Run(w2, defs.Start.RevealDelay);
        Check(w2.Faction(2).Revealed, $"대도시 없이 {defs.Start.RevealDelay}초 지나면 위치 공개");

        var w3 = NewWorld(opt);
        foreach (var u in w3.Units.Where(u => u.Owner == 2)) u.Hp = 0;
        Run(w3, 1);
        Check(w3.Winner == 1 && w3.Faction(2).Defeated, "유닛이 전멸하면 탈락 (도시가 남아 있어도)");
    }

    static void TestAiMatch(GameOptions opt)
    {
        Console.WriteLine("[AI 대전]");
        foreach (var name in AiRegistry.All.Keys)
        {
            var w = NewWorld(opt);
            w.Controllers.Add((1, AiRegistry.Create(name)!));
            w.Controllers.Add((2, AiRegistry.Create(name)!));
            var sw = Stopwatch.StartNew();
            float minutes = 15;
            int steps = 0;
            for (; steps < (int)(minutes * 60 / Config.SimDt) && w.Winner == 0; steps++) w.Step();
            Console.WriteLine($"  [info] {name} vs {name}: {steps * Config.SimDt / 60:0.0}분, 유닛 {w.Units.Count(u => u.Owner == 1)}/{w.Units.Count(u => u.Owner == 2)}, 승자 {w.Winner}, 실행 {sw.Elapsed.TotalSeconds:0.0}초");
            Check(true, $"AI '{name}' 대전이 예외 없이 진행");
        }
    }

    static void TestPerformance(GameOptions opt)
    {
        Console.WriteLine("[성능]");
        foreach (int count in new[] { 200, 1000 })
        {
            var w = NewWorld(opt);
            var inf = defs.Units["infantry"];
            var (sx, sy) = w.Map.StartTiles[0];
            var (ex, ey) = w.Map.StartTiles[1];
            var mine = new List<Unit>();
            var theirs = new List<Unit>();
            int side = (int)MathF.Ceiling(MathF.Sqrt(count / 2f));
            for (int i = 0; i < count / 2; i++)
            {
                var off = new Vector2(i % side - side / 2f, i / side - side / 2f) * 0.8f;
                mine.Add(w.SpawnUnit(inf, 1, new Vector2(sx + 8, sy - 8) + off));
                theirs.Add(w.SpawnUnit(inf, 2, new Vector2(ex - 8, ey + 8) + off));
            }
            w.AttackMove(1, mine, new Vector2(ex, ey));
            w.AttackMove(2, theirs, new Vector2(sx, sy));
            var sw = Stopwatch.StartNew();
            const int steps = 60 * 30;
            double worst = 0;
            for (int i = 0; i < steps; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                w.Step();
                worst = Math.Max(worst, Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
            double avg = sw.Elapsed.TotalMilliseconds / steps;
            Console.WriteLine($"  [perf] 유닛 {count} 어택땅 교전 30초: 틱당 평균 {avg:0.000}ms, 최대 {worst:0.00}ms, 생존 {w.Units.Count}");
            Check(avg < 6, $"유닛 {count} 교전 틱 평균 < 6ms");
        }
    }

    static void Check(bool ok, string name)
    {
        Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name}");
        if (!ok) failures++;
    }
}
