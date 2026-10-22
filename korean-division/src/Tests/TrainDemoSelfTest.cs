using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T30e 시연 미션 train_escort_demo: 로드 · 검증, 미션 AI 로 완주 (승리), 같은 시드 2회 체크섬 일치, 기준(golden) 체크섬 일치,
/// 시간 제한 변형은 패배. 기준값 갱신 = KD_TRAIN_DEMO_UPDATE=1. KD_SELFTEST_ONLY=traindemo</summary>
public static class TrainDemoSelfTest
{
    const int Seed = 1;
    const string GoldenFile = "train_demo.golden";

    static (World W, ulong Sum) Play(MissionDef def, Defs defs, int seed)
    {
        var w = World.CreateMissionWorld(def, defs, seed, new List<DeployEntry>());
        World.AttachMissionAi(w, def.Player, true);
        long max = 60L * 60 * 16;
        while (w.Winner == 0 && w.Tick < max) w.Step();
        return (w, w.ComputeChecksum());
    }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var def = MissionDef.Load("train_escort_demo");
        check(def.Trains.Count == 2 && def.Objectives.Count == 1 && def.Objectives[0].Train == "freight" && def.Failures.Count == 1, "시연 미션 로드: 열차 2 (장갑 · 화물), 호송 목표, 시간 제한");

        var (w1, s1) = Play(def, defs, Seed);
        var (w2, s2) = Play(def, defs, Seed);
        check(w1.Mission!.Result == MissionState.Won && w1.Winner == 1 && w1.Mission.ObjProgress[0] >= 2, $"시연 미션 완주: 승리 (화차 {w1.Mission.ObjProgress[0]}량, {w1.Time:0}초)");
        check(s1 == s2 && w1.Tick == w2.Tick, $"같은 시드 2회 체크섬 일치 ({s1:X16})");

        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "data");
        string line = $"{s1:x16} {w1.Tick}";
        if (Environment.GetEnvironmentVariable("KD_TRAIN_DEMO_UPDATE") == "1")
        {
            File.WriteAllText(Path.Combine(dir, GoldenFile), line + "\n");
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "KoreanDivision.csproj"))) { File.WriteAllText(Path.Combine(d.FullName, "assets", "data", GoldenFile), line + "\n"); break; }
            Console.WriteLine("train_demo golden updated: " + line);
        }
        string p = Path.Combine(dir, GoldenFile);
        check(File.Exists(p) && File.ReadAllText(p).Trim() == line, $"시연 미션 golden 일치 ({line})");

        // 패배 경로: 같은 미션을 시간 제한 40초로 → 화차 도착 전 시간 초과 = 패배
        var lose = MissionDef.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "missions", "train_escort_demo.json")).Replace("\"seconds\": 900", "\"seconds\": 40"), "train_escort_demo");
        var (w3, _) = Play(lose, defs, Seed);
        check(w3.Mission!.Result == MissionState.Lost && w3.Winner == 2, "시간 제한 40초 변형: 패배 (결과 화면은 패배로 기록)");
        check(MissionResults.From(w3).Result == "lost" && MissionResults.From(w1).Result == "won", "결과 기록: 승 · 패 모두 생성");
    }
}
