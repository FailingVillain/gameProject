using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// AF1 isai-restore: Faction.IsAi 는 시뮬이 읽는다 (Surrender.cs — AI 확률 ×1, 플레이어 × surrenderPlayerMul).
/// 자유전 · 교전 · 미션 생성 경로는 AI 를 붙이지 않으므로 리플레이 머리말 AiFactions 가 정본, 스냅샷은 본문(FactionSnapshot)에 이미 있다.
///  · 교전(자유전) 리플레이: 머리말 기록 → 저장 → 불러오기 → 재생 월드 IsAi 복원 · 체크섬 일치 · 항복 판정 동일
///  · 머리말을 비우면 (옛 결함 재현) 재생이 어긋난다
///  · 스냅샷 왕복 · 체크섬 반영 · 스냅샷 시작 리플레이는 스냅샷 값
/// ReplaySelfTest.Run 이 부른다 (KD_SELFTEST_ONLY=replay).
/// </summary>
public static class IsAiReplaySelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var c = defs.CombatRules;
        var keep = (c.SurrenderChance, c.SurrenderPlayerMul, c.SurrenderCasualty, c.SurrenderMaxLeft, c.SurrenderSuppAt, c.SurrenderSuppSec, c.SurrenderProbeLen);
        try
        {
            // 시험 무대: 항복 문턱만 낮춰 짧은 교전에서도 판정이 자주 돌게 한다 (퇴각로 탐침이 지도 끝에 닿아 '퇴각 불가').
            // 확률은 AI ×1 · 플레이어 × surrenderPlayerMul(0.3 그대로) → IsAi 가 판정을 가른다.
            c.SurrenderChance = 1f; c.SurrenderCasualty = 0f; c.SurrenderMaxLeft = 1000; c.SurrenderSuppAt = 0.3f; c.SurrenderSuppSec = 2f; c.SurrenderProbeLen = 400f;
            FreeBattleReplay(defs, check);
        }
        finally { (c.SurrenderChance, c.SurrenderPlayerMul, c.SurrenderCasualty, c.SurrenderMaxLeft, c.SurrenderSuppAt, c.SurrenderSuppSec, c.SurrenderProbeLen) = keep; }
        SnapshotRoundTrip(defs, check);
    }

    static World Battle(Defs defs, out BattleSetup setup)
    {
        setup = new BattleSetup { SideA = "infantry:50", SideB = "infantry:100", Map = "flat", Distance = 30, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 7);
        for (int f = 1; f <= 2; f++)   // Game.cs 교전 경로와 같다: AI 붙이기 + IsAi
        {
            w.Controllers.Add((f, AiRegistry.Create(f == 1 ? setup.AiA : setup.AiB)!));
            w.Faction(f).IsAi = true;
        }
        return w;
    }

    static List<int> Surrendered(World w) => w.Units.Where(u => u.Surrendered).Select(u => u.Id).OrderBy(i => i).ToList();

    static void FreeBattleReplay(Defs defs, Action<bool, string> check)
    {
        var w = Battle(defs, out _);
        int endTick = 4 * 60 * 60;
        while (w.Tick < endTick && w.Winner == 0) w.Step();
        var orig = Surrendered(w);

        var replay = ReplayFile.FromWorld(w);
        string path = Path.Combine(TestTemp.Dir, "isai.replay.json");
        replay.Save(path);
        var loaded = ReplayFile.Load(path);
        check(loaded.AiFactions.SequenceEqual(new[] { 1, 2 }) && loaded.Format == ReplayFile.FormatVersion,
              $"IsAi 머리말: AiFactions [{string.Join(",", loaded.AiFactions)}] 저장 → 불러오기 (리플레이 형식 {ReplayFile.FormatVersion})");

        var rw = loaded.CreateWorld(defs);
        check(rw.Tick == 0 && rw.Controllers.Count == 0 && rw.Faction(1).IsAi && rw.Faction(2).IsAi,
              "IsAi 재생 월드: AI 없이 틱 0 전에 세력 1 · 2 IsAi 복원");
        while (rw.Tick < loaded.EndTick) rw.Step();
        var again = Surrendered(rw);
        check(rw.ComputeChecksum() == loaded.EndChecksum && orig.Count > 0 && again.SequenceEqual(orig),
              $"IsAi 교전 리플레이: {loaded.EndTick}틱 끝 체크섬 {loaded.EndChecksum:X16} 재현, 항복 {orig.Count}명 판정 동일 (재생 {again.Count}명)");
        long bad = loaded.Verify(defs, out string report);
        check(bad == -1, $"IsAi 교전 리플레이 검증: {report}");

        // 옛 결함 재현: 머리말을 비우면 두 세력이 사람으로 재생 → 항복 확률 ×0.3 → 판정이 갈린다
        var stripped = ReplayFile.Load(path);
        stripped.AiFactions = Array.Empty<int>();
        var sw = stripped.CreateWorld(defs);
        while (sw.Tick < stripped.EndTick) sw.Step();
        var lost = Surrendered(sw);
        long bad2 = stripped.Verify(defs, out string report2);
        check(bad2 != -1 && !lost.SequenceEqual(orig),
              $"IsAi 없는 재생은 어긋남 (옛 결함): 항복 {orig.Count} → {lost.Count}명, {report2}");
        try { File.Delete(path); } catch (IOException) { }
    }

    static void SnapshotRoundTrip(Defs defs, Action<bool, string> check)
    {
        var w = Battle(defs, out _);
        w.Faction(1).IsAi = false;   // 사람 1 · AI 2
        for (int i = 0; i < 120; i++) w.Step();
        var meta = new SnapshotMeta("isai", 1, false, "battle");
        var bytes = Snapshot.Save(w, meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.Save(y, meta).SequenceEqual(bytes);
        check(same && !y.Faction(1).IsAi && y.Faction(2).IsAi && y.ComputeChecksum() == w.ComputeChecksum(),
              $"IsAi 스냅샷 왕복: 세력 1 사람 · 2 AI 복원, 바이트 동일 {same}, 체크섬 동일 (형식 {Snapshot.FormatVersion})");

        ulong h0 = y.ComputeChecksum();
        y.Faction(1).IsAi = true; ulong h1 = y.ComputeChecksum();
        y.Faction(1).IsAi = false;
        check(h0 != h1 && y.ComputeChecksum() == h0, "IsAi 체크섬 반영: 세력 IsAi 를 바꾸면 체크섬이 달라짐");

        // 스냅샷 시작 리플레이: 머리말이 아니라 스냅샷 값이 정본
        var r = ReplayFile.FromWorld(y);
        r.StartSnapshot = Convert.ToBase64String(bytes);
        r.StartTick = w.Tick;
        r.AiFactions = Array.Empty<int>();
        var sw = r.CreateWorld(defs);
        check(!sw.Faction(1).IsAi && sw.Faction(2).IsAi && sw.Controllers.Count == 0, "IsAi 스냅샷 시작 리플레이: 스냅샷 값 유지 (머리말 무시)");
    }
}
