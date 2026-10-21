using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N32 참호 돌격 (TrenchAssault.cs): 자료 · 단계 전환 · 플레이어 명령 (기록 · 취소 · 다른 세력 무시 · 참호 아닌 지점 거절) · 리플레이 · 스냅샷. 창 없이. 단독 KD_SELFTEST_ONLY=n32.
/// </summary>
public static class N32SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("n32", 1, false, "standard");

    // 풀밭 200x100, 세로 참호선 x=100 (y 30..70)
    const string Map = """
    { "name": "n32 test", "width": 200, "height": 100, "base": "grass",
      "starts": [ [10, 10], [190, 90] ],
      "paint": [ { "terrain": "trench", "rect": [100, 30, 1, 41] } ] }
    """;

    static Vector2 C(int x, int y) => new(x + 0.5f, y + 0.5f);
    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    /// <summary>공격 1세력 중대 (소총 n + 경기관총 guns + 장교) 를 (30, 50) 에, 방어 2세력 소총 m 명을 참호 자리에.</summary>
    internal static (World W, Company C, List<Unit> Att, List<Unit> Def) Setup(int seed, int n = 36, int m = 20, int guns = 4)
    {
        var w = World.CreateStandard(MapFile.LoadText(Map, "n32", seed), defs);
        foreach (var u in w.Units) u.Hp = 0;
        w.Step();
        w.Fog.Enabled = false;
        w.RecordCommands = true;
        var att = new List<Unit>();
        for (int i = 0; i < n; i++) att.Add(w.SpawnUnit(defs.Units[i % 3 == 0 ? "infantry_assault" : "infantry"], 1, new Vector2(30 + i % 12 * 0.9f, 44 + i / 12 * 1.2f)));
        for (int i = 0; i < guns; i++) att.Add(w.SpawnUnit(defs.Units["heavy_machinegunner"], 1, new Vector2(29, 44 + i * 1.2f)));
        att.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(28, 50)));
        w.FormCompany(1, att);
        var dfn = new List<Unit>();
        for (int i = 0; i < m; i++) dfn.Add(w.SpawnUnit(defs.Units["infantry"], 2, C(100, 40 + i)));
        w.OccupyPositions(2, dfn, C(100, 50), new Vector2(-1, 0), 14, 0.9f);
        Steps(w, 3);
        var c = att[0].Company!;
        return (w, c, att, dfn);
    }

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[N32 참호 돌격]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestCommand(opt.Seed);
            TestSequence(opt.Seed);
            TestReplayAndSnapshot(opt.Seed);
        }
        catch (Exception e) { check(false, $"n32 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static void TestValues()
    {
        var R = defs.Tactics.TrenchAssault;
        check(R.Enabled && R.BoundEndDist == 12f && R.Volleys == 2 && R.AiEnabled && !R.SmokeHook && !R.WireCutHook && R.BoundLength == 6f,
            $"tactics.json trenchAssault 자료: 약진 끝 {R.BoundEndDist}칸 · 수류탄 {R.Volleys}번 · AI {R.AiEnabled} · 연막 · 철조망 자리만");
    }

    static void TestCommand(int seed)
    {
        var (w, c, att, _) = Setup(seed);
        bool far = w.TrenchAssault(1, new[] { c.Id }, C(60, 10));       // 참호에서 멀다 -> 거절
        bool other = w.TrenchAssault(2, new[] { c.Id }, C(100, 50));     // 남의 중대 -> 거절 (명령 안 만듦)
        bool ok = w.TrenchAssault(1, new[] { c.Id }, C(98, 50));         // 참호 2칸 옆 -> 맞춰서 수락
        w.Step();
        bool logged = w.CommandLog.Any(k => k.Command is TrenchAssaultCommand { On: true, Faction: 1 } tc && tc.Companies.Contains(c.Id));
        bool started = c.TrenchAssaultStage == TaStage.Suppress && Vector2.Distance(c.TrenchAssaultTarget, C(100, 50)) < 0.9f;
        check(!far && !other && ok && logged && started, $"참호 돌격 명령: 먼 지점 거절 {!far} · 남의 중대 거절 {!other} · 참호에 맞춤 {started} · 리플레이 기록 {logged}");
        // 취소: 같은 명령 (On=false) 과, 그 중대 유닛에 내린 다른 명령 둘 다
        w.CancelTrenchAssault(1, new[] { c.Id });
        w.Step();
        bool c1 = c.TrenchAssaultStage == TaStage.None && c.TA.Result == 3;
        w.TrenchAssault(1, new[] { c.Id }, C(100, 50));
        Steps(w, 2);
        bool again = c.TrenchAssaultStage == TaStage.Suppress;
        w.Hold(1, new[] { att[0] });
        Steps(w, 2);
        check(c1 && again && c.TrenchAssaultStage == TaStage.None && c.TA.Result == 3, $"취소: 취소 명령 {c1} · 다시 시작 {again} · 중대 유닛에 다른 명령 -> 취소 {c.TrenchAssaultStage == TaStage.None}");
        var h0 = w.ComputeChecksum();
        c.TA.CooldownUntil += 7;
        bool hs = w.ComputeChecksum() != h0;
        c.TA.CooldownUntil -= 7;
        check(hs, "참호 돌격 상태가 체크섬에 반영");
    }

    static void TestSequence(int seed)
    {
        var (w, c, att, dfn) = Setup(seed);
        w.TrenchAssault(1, new[] { c.Id }, C(100, 50));
        var seen = new List<TaStage>();
        var at = new Dictionary<TaStage, float>();
        long limit = 60L * 480;
        for (long t = 0; t < limit; t++)
        {
            w.Step();
            var s = c.TrenchAssaultStage;
            if (s != TaStage.None && (seen.Count == 0 || seen[^1] != s)) { seen.Add(s); at[s] = w.Tick / 60f; }
            if (Environment.GetEnvironmentVariable("KD_N32_DEBUG") != null && t % 600 == 0 && s != TaStage.None)
            {
                var gn = att.Where(u => u.Alive && u.Def.Weapon == "lmg").ToList(); var rf = att.Where(u => u.Alive && !u.IsOfficer && u.Def.Weapon != "lmg").ToList();
                Console.WriteLine($"  dbg t={t / 60} stage={s} sub={c.TA.Sub} ready={c.TA.ReadyTick / 60} guns x={string.Join(",", gn.Select(u => u.Pos.X.ToString("0") + (u.Order == UnitOrder.Hold ? "H" : u.Order.ToString()[0].ToString())))} rifle x avg={rf.Average(u => u.Pos.X):0.0} orders={string.Join("", rf.Take(8).Select(u => u.Order.ToString()[0]))} defSupp={dfn.Where(u => u.Alive).Select(u => u.SuppressionRatio).DefaultIfEmpty(0).Average():0.00} prone={dfn.Count(u => u.Alive && u.Prone)}");
            }
            if (s == TaStage.None && seen.Count > 0) break;
        }
        int inT = att.Count(u => u.Alive && u.InTrench), attAlive = att.Count(u => u.Alive && !u.IsOfficer), defAlive = dfn.Count(u => u.Alive);
        string order = string.Join(">", seen.Select(s => $"{s}@{at[s]:0}"));
        var expect = new[] { TaStage.Suppress, TaStage.Approach, TaStage.BreakIn, TaStage.Clear, TaStage.Consolidate };
        check(seen.SequenceEqual(expect), $"단계 순서 제압 > 접근 > 돌파 > 소탕 > 확보: {order}");
        check(c.TA.Result == 1 && defAlive <= dfn.Count / 2, $"결과: 성공 {c.TA.Result == 1} (결과 {c.TA.Result}) · 방어병 {dfn.Count} -> {defAlive} · 공격 생존 {attAlive}/{att.Count - 1} · 참호 안 {inT} · 수류탄 {w.Stats.TaGrenades} · 약진 {w.Stats.TaBounds}");
        check(w.Stats.TaBounds >= 2 && w.Stats.TaGrenades > 0 && w.Stats.TaCharges == 1 && w.Stats.TaConsolidations == 1,
            $"약진 (교대) {w.Stats.TaBounds} · 수류탄 {w.Stats.TaGrenades} · 돌격 {w.Stats.TaCharges} · 확보 {w.Stats.TaConsolidations}");
        check(inT >= 4 && att.Where(u => u.InTrench).All(u => u.Order == UnitOrder.Hold), $"확보: 점령한 참호에 병사 {inT} 명이 들어가 사수");
    }

    static void TestReplayAndSnapshot(int seed)
    {
        var (w0, c0, _, _) = Setup(seed, 24, 12);
        w0.TrenchAssault(1, new[] { c0.Id }, C(100, 50));
        Steps(w0, 60 * 40);   // 접근 중
        var bytes = Snapshot.Save(w0, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        var cy = y.Companies.First(k => k.Id == c0.Id);
        bool field = cy.TrenchAssaultStage == c0.TrenchAssaultStage && cy.TA.Target == c0.TA.Target && cy.TA.BoundTick == c0.TA.BoundTick;
        Steps(w0, 60 * 30); Steps(y, 60 * 30);
        check(c0.TrenchAssaultStage != TaStage.None && same && field && w0.ComputeChecksum() == y.ComputeChecksum(),
            $"스냅샷: 접근 중 저장 -> 불러오기 바이트 동일 {same} · 필드 {field} · 30초 더 돌려도 같은 체크섬 (단계 {c0.TrenchAssaultStage}, 형식 {Snapshot.FormatVersion})");
        var log = w0.CommandLog.ToList();
        var json = JsonSerializer.Serialize<GameCommand>(log.Select(r => r.Command).OfType<TrenchAssaultCommand>().First(), CommandJson.Options);
        var back = JsonSerializer.Deserialize<GameCommand>(json, CommandJson.Options);
        var (w1, _, _, _) = Setup(seed, 24, 12);
        w1.RecordCommands = false;
        long t0 = w1.Tick;
        var byTick = log.Where(r => r.Tick >= t0).GroupBy(r => r.Tick).ToDictionary(g => g.Key, g => g.Select(r => JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(r.Command, CommandJson.Options), CommandJson.Options)!).ToArray());
        w1.CommandFeed = t => byTick.TryGetValue(t, out var l) ? l : Array.Empty<GameCommand>();
        while (w1.Tick < w0.Tick) w1.Step();
        check(back is TrenchAssaultCommand { On: true } && w1.ComputeChecksum() == w0.ComputeChecksum(), $"리플레이: JSON 왕복 {json} · 명령 재생 -> 같은 체크섬");
    }
}
