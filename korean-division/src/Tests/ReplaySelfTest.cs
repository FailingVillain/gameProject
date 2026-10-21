using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 명령 객체 · 리플레이 검증 (창 없이). SelfTest 가 호출한다.
///  · 명령 JSON 왕복, 세력 권한, 한 틱 지연 실행
///  · AI 대전을 기록 → 저장 → 불러오기 → 재생 검증(체크섬 일치) · 변조 감지 · 재생 속도
/// </summary>
public static class ReplaySelfTest
{
    /// <summary>임시 리플레이 파일을 두는 곳 (세션 전용 스크래치 디렉터리).</summary>
    static string ScratchDir => TestTemp.Dir;

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            CommandJsonRoundTrip(check);
            CommandSemantics(opt, check);
            RecordAndReplay(opt, check);
        }
        catch (Exception e)
        {
            check(false, $"리플레이 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    // ---------------------------------------------------------------- JSON 왕복

    static void CommandJsonRoundTrip(Action<bool, string> check)
    {
        var samples = new GameCommand[]
        {
            new MoveCommand(1, new[] { 1, 2, 3 }, new Vector2(1.5f, 2.25f)),
            new AttackMoveCommand(1, new[] { 4 }, new Vector2(80.125f, 3)),
            new QuickStepCommand(2, new[] { 5, 6 }, new Vector2(0, 99.5f)),
            new RetreatCommand(1, new[] { 5, 9 }, new Vector2(12.5f, 7.25f)),
            new AttackCommand(1, new[] { 7, 8 }, 42),
            new StopCommand(2, new[] { 9 }),
            new HoldCommand(1, new[] { 10, 11, 12 }),
            new CloseRanksCommand(1, new[] { 3, 5 }),
            new OccupyPositionsCommand(1, new[] { 3, 5 }, new Vector2(10, 12), new Vector2(0, -1), 8f, 0.85f),
            new GroundFireCommand(1, new[] { 13, 14 }, new Vector2(33.5f, 44.25f)),
            new DebugSpawnCommand(1, "infantry", new Vector2(50.5f, 49.5f), 7),
            new RevealMapCommand(1, true),
            new SetCombatSettingCommand(1, 5, 2),
            new MissionEndCommand(1, true),
            new ThrowGrenadeCommand(1, new[] { 5 }, new Vector2(7.5f, 8.25f)),
            new ChargeOnlyCommand(1, new[] { 5, 6 }, true),
            new MountCommand(1, new[] { 5, 6 }, true),
            new ChargeCommand(1, new[] { 5, 6 }, new Vector2(3, 4), 7),
            new FormCompanyCommand(1, new[] { 5, 6 }, 2),
            new DisbandCompanyCommand(1, 3),
            new SplitCompanyCommand(1, 3),
            new FormBattalionCommand(1, new[] { 1, 2 }),
            new SetBattalionNumberCommand(1, 1, 4),
            new MoveCompanyCommand(1, 2, 0),
            new SetCompanyFormCommand(1, new[] { 1 }, 4), new SetMarchFireCommand(1, new[] { 1 }, true),
            new SetCompanyTacticCommand(1, new[] { 1 }, 5), new SetSquadFormCommand(1, new[] { 1 }, 3), new SetUnitRoleCommand(1, new[] { 1 }, 3),
            new SetBattalionFormCommand(1, 1, 3),
            new SetScoutAutoCommand(1, new[] { 5, 6 }, false),
            new SetPostureCommand(1, new[] { 1, 2 }, CompanyPosture.Defend),
            new SetKeepFormationCommand(1, new[] { 1, 2 }, true),
            new SetFireModeCommand(1, new[] { 1, 2 }, FireMode.ReturnFire),
            new TrenchAssaultCommand(1, new[] { 1, 2 }, new System.Numerics.Vector2(40.5f, 22.5f), true),
            new SetRallyCommand(1, new[] { 1, 2 }, new Vector2(5, 6), false), new GroundFireExCommand(1, new[] { 3 }, new Vector2(7, 8), GroundFireKind.Barrage, new Vector2(1, 0)),
            new SetFinalLineCommand(1, new[] { 3 }, new Vector2(9, 10)),   // arty3
            new DispatchOrderCommand(1, 2, CommsVia.Courier, new MoveCommand(1, new[] { 1 }, new Vector2(11, 12))),   // comms
            new ThrowSmokeCommand(1, new[] { 5 }, new Vector2(13.5f, 14.25f)), new CutWireCommand(1, new[] { 6 }, new Vector2(15, 16)),   // breach (N65 · N59)
            new FireFlareCommand(1, new[] { 7 }, new Vector2(17.5f, 18)),   // T10 조명탄
        };

        // GameCommand.cs 에 등록된 모든 파생 형식이 표본에 들어 있는지 (새 명령을 추가하고 표본을 빠뜨리면 여기서 걸림)
        var registered = typeof(GameCommand).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToHashSet();
        var sampled = samples.Select(c => c.GetType()).ToHashSet();
        var missing = registered.Except(sampled).Select(t => t.Name).ToList();
        check(registered.Count > 0 && missing.Count == 0,
              $"등록된 명령 형식 {registered.Count}종 모두 표본에 포함" + (missing.Count > 0 ? $" (빠짐: {string.Join(", ", missing)})" : ""));

        var failed = new List<string>();
        foreach (var c in samples)
        {
            string json = JsonSerializer.Serialize(c, CommandJson.Options);
            var back = JsonSerializer.Deserialize<GameCommand>(json, CommandJson.Options);
            string again = back == null ? "<null>" : JsonSerializer.Serialize(back, CommandJson.Options);
            if (back == null || back.GetType() != c.GetType() || again != json || !json.Contains("\"$t\""))
                failed.Add($"{c.GetType().Name}: {json} → {again}");
        }
        check(failed.Count == 0, $"명령 {samples.Length}개 JSON 왕복 (형식 식별자 · 값 동일)" + (failed.Count > 0 ? "\n    " + string.Join("\n    ", failed) : ""));

        // 기록 한 줄(CommandRecord) 도 왕복
        var rec = new CommandRecord(1234, new MoveCommand(2, new[] { 9 }, new Vector2(3, 4)));
        string recJson = JsonSerializer.Serialize(rec, CommandJson.Options);
        var recBack = JsonSerializer.Deserialize<CommandRecord>(recJson, CommandJson.Options);
        check(recBack.Tick == 1234 && recBack.Command is MoveCommand { Faction: 2 } mc && mc.Target == new Vector2(3, 4) && mc.Units.SequenceEqual(new[] { 9 }), $"명령 기록(틱 + 명령) JSON 왕복: {recJson}");
    }

    // ---------------------------------------------------------------- 명령 실행 의미

    static void CommandSemantics(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = World.CreateStandard(GameMap.Generate(opt.MapWidth, opt.MapHeight, opt.Seed), defs);
        w.Fog.Enabled = false;
        var mine = w.Units.First(u => u.Owner == 1);
        var theirs = w.Units.First(u => u.Owner == 2);

        // 한 틱 지연: 공개 API 호출 직후에는 상태가 바뀌지 않고, 실행(FlushCommands / Step) 후에 바뀐다
        w.Move(1, new[] { mine }, mine.Pos + new Vector2(5, 0));
        bool delayed = mine.Order == UnitOrder.Idle && mine.Destination == null && w.CommandLog.Count == 0;
        w.FlushCommands();
        check(delayed && mine.Order == UnitOrder.Move && mine.Destination != null, "명령은 호출 즉시가 아니라 다음 실행 시점에 적용됨");
        check(w.CommandLog.Count == 1 && w.CommandLog[0].Tick == w.Tick && w.CommandLog[0].Command is MoveCommand { Faction: 1 },
              $"실행된 명령이 CommandLog 에 (틱 {w.CommandLog[0].Tick}, {w.CommandLog[0].Command.GetType().Name}) 으로 기록됨");

        // 남의 세력 유닛 Id 를 넣어도 실행 시 무시된다
        var before = theirs.Order;
        w.Issue(new MoveCommand(1, new[] { theirs.Id, mine.Id }, theirs.Pos + new Vector2(3, 3)));
        w.FlushCommands();
        check(theirs.Order == before && theirs.Destination == null && mine.Order == UnitOrder.Move,
              "다른 세력 유닛 Id 가 섞인 명령: 남의 유닛은 무시, 내 유닛만 실행");

        // 범위 밖 세력 번호(0 · 음수 · 없는 세력) 명령은 기록조차 되지 않음
        int logged = w.CommandLog.Count;
        w.Issue(new MoveCommand(0, new[] { mine.Id }, mine.Pos));
        w.Issue(new MoveCommand(-1, new[] { mine.Id }, mine.Pos));
        w.Issue(new MoveCommand(w.Factions.Count, new[] { mine.Id }, mine.Pos));
        w.FlushCommands();
        check(w.CommandLog.Count == logged, "유효하지 않은 세력 번호의 명령은 무시되고 기록되지 않음");

        // 실행 시점 재검증: '돌격만' 은 실행할 때 기병인지 다시 본다 (보병 Id 는 무시)
        var hq = w.Buildings.First(b => b.Owner == 1 && b.Def.Headquarters);
        var foot = w.Units.FirstOrDefault(u => u.Owner == 1 && !u.Def.Mounted);
        if (foot != null)
        {
            w.Issue(new ChargeOnlyCommand(1, new[] { foot.Id }, true));
            w.FlushCommands();
            check(!foot.ChargeOnly, "돌격만 명령: 기병이 아니면 실행 시 무시");
        }

        // 기록 끄기
        w.RecordCommands = false;
        logged = w.CommandLog.Count;
        w.Stop(1, new[] { mine });
        w.FlushCommands();
        check(mine.Order == UnitOrder.Idle && w.CommandLog.Count == logged, "RecordCommands=false 면 실행은 하되 기록하지 않음");
        w.RecordCommands = true;

        // 디버그 생성 명령도 일반 명령처럼 실행 · 기록된다
        int units = w.Units.Count(u => u.Owner == 2);
        w.Issue(new DebugSpawnCommand(2, "infantry", hq.Pos + new Vector2(0, 8), 6));
        w.FlushCommands();
        check(w.Units.Count(u => u.Owner == 2) == units + 6 && w.CommandLog[^1].Command is DebugSpawnCommand,
              "디버그 생성 명령: 유닛 6기 생성 · 기록됨");

        // Step 안에서도 매 틱 시작에 실행된다 (유닛 갱신 전에)
        w.Hold(1, new[] { mine });
        w.Step();
        check(mine.Order == UnitOrder.Hold && mine.Velocity == Vector2.Zero, "Step 은 틱 시작에 대기 명령을 먼저 실행");
    }

    // ---------------------------------------------------------------- 기록 → 저장 → 재생 검증

    static void RecordAndReplay(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        const int minutes = 8;
        int endTick = minutes * 60 * 60;   // 60틱/초

        var w = World.CreateStandard(GameMap.Generate(100, 100, opt.Seed), defs);
        w.Controllers.Add((1, AiRegistry.Create("standard")!));
        w.Controllers.Add((2, AiRegistry.Create("rush")!));
        var hq1 = w.Buildings.First(b => b.Owner == 1 && b.Def.Headquarters);
        var hq2 = w.Buildings.First(b => b.Owner == 2 && b.Def.Headquarters);
        Vector2 center = new(w.Map.Width / 2f, w.Map.Height / 2f);
        var spawned = new List<Unit>();
        var manual = new List<string>();
        long spawnTick = 30 * 60;
        List<Unit> Mine() => spawned.Any(u => u.Alive) ? spawned : w.Units.Where(u => u.Owner == 1 && u.Alive && u.Def.Key == "infantry").Take(5).ToList();   // 생성한 5기가 죽었으면 아무 살아 있는 보병 (명령 종류 확인용)

        var sw = Stopwatch.StartNew();
        while (w.Tick < endTick)
        {
            // 중간중간 사람이 조작하듯 공개 API 로 명령을 섞는다 (AI 가 쓰지 않는 종류 포함).
            // 주의: 명령이 아닌 직접 상태 변경(자금 조작 등)은 기록되지 않아 재생이 어긋난다 — 여기서는 명령만 쓴다.
            if (w.Tick == spawnTick)
            {
                w.Issue(new DebugSpawnCommand(1, "infantry", hq1.Pos + new Vector2(0, -(hq1.Size / 2f + 3)), 5));
                manual.Add("debugspawn");
            }
            else if (w.Tick == spawnTick + 1)
            {
                spawned.AddRange(w.Units.Where(u => u.Owner == 1 && u.Def.Key == "infantry").TakeLast(5));
            }
            else if (w.Tick == 45 * 60) { w.Move(1, spawned, center); manual.Add("move"); }
            else if (w.Tick == 60 * 60) { w.Hold(1, spawned); manual.Add("hold"); }
            else if (w.Tick == 75 * 60) { w.QuickStep(1, spawned, hq1.Pos + new Vector2(10, 0)); manual.Add("quickstep"); }
            else if (w.Tick == 90 * 60) { w.Stop(1, spawned); manual.Add("stop0"); }
            else if (w.Tick == 150 * 60) { w.AttackMove(1, spawned, center); manual.Add("amove"); }
            else if (w.Tick == 170 * 60) { w.Attack(1, Mine(), hq2); manual.Add("attack"); }
            else if (w.Tick == 200 * 60) { w.Stop(1, Mine()); manual.Add("stop"); }
            w.Step();
        }
        double recordSecs = sw.Elapsed.TotalSeconds;

        var replay = ReplayFile.FromWorld(w);
        var kinds = replay.Commands.Select(c => c.Command.GetType().Name).Distinct().OrderBy(n => n).ToList();
        Console.WriteLine($"  [info] 기록: {minutes}분 (표준 vs 돌격), 승자 {w.Winner}, 명령 {replay.Commands.Count}개 ({kinds.Count}종: {string.Join(", ", kinds)}), " +
                          $"체크섬 {replay.Checksums.Count}곳, 수동 명령 {string.Join("/", manual)}, 실행 {recordSecs:0.0}초");
        check(replay.EndTick == endTick && replay.Seed == opt.Seed && replay.MapWidth == 100 && replay.FactionCount == 2 && replay.BalanceHash == defs.Hash,
              $"리플레이 머리말: 틱 {replay.EndTick}, 시드 {replay.Seed}, 맵 {replay.MapWidth}x{replay.MapHeight}, 세력 {replay.FactionCount}, 밸런스 {replay.BalanceHash[..Math.Min(8, replay.BalanceHash.Length)]}");
        check(replay.Commands.Count > 50 && replay.Checksums.Count == endTick / ReplayFile.ChecksumInterval,
              $"명령 {replay.Commands.Count}개 · 체크섬 {replay.Checksums.Count}곳 ({ReplayFile.ChecksumInterval}틱마다) 기록");
        check(replay.Commands.Any(c => c.Command is DebugSpawnCommand && c.Tick == spawnTick) && replay.Commands.Any(c => c.Command is StopCommand) &&
              replay.Commands.Any(c => c.Command is QuickStepCommand) && replay.Commands.Any(c => c.Command is HoldCommand),
              "수동 명령(디버그 생성 · 정지 · 속보 · 사수)이 AI 명령과 함께 틱 순으로 기록됨");
        bool ordered = true;
        for (int i = 1; i < replay.Commands.Count; i++) ordered &= replay.Commands[i - 1].Tick <= replay.Commands[i].Tick;
        check(ordered, "명령 기록은 틱 오름차순");

        // 저장 → 불러오기
        string path = Path.Combine(ScratchDir, $"selftest_{opt.Seed}.replay.json");
        replay.Save(path);
        long bytes = new FileInfo(path).Length;
        var loaded = ReplayFile.Load(path);
        string a = JsonSerializer.Serialize(replay, CommandJson.Options), b = JsonSerializer.Serialize(loaded, CommandJson.Options);
        check(a == b && loaded.Commands.Count == replay.Commands.Count && loaded.EndChecksum == replay.EndChecksum,
              $"저장 → 불러오기 동일 ({bytes / 1024.0:0.0}KB, 명령당 {(double)bytes / Math.Max(1, loaded.Commands.Count):0}바이트)");

        // 재생 검증
        sw.Restart();
        long bad = loaded.Verify(defs, out string report);
        double verifySecs = sw.Elapsed.TotalSeconds;
        check(bad == -1, $"불러온 리플레이 재생 검증: {report} (명령 {loaded.Commands.Count}개, {bytes / 1024.0:0.0}KB)");
        check(true, $"재생 검증 속도: 시뮬 {minutes}분을 {verifySecs:0.00}초에 — {minutes / Math.Max(1e-6, verifySecs):0.0}분/초 (기록 {minutes / Math.Max(1e-6, recordSecs):0.0}분/초, AI 포함)");

        // 재생 월드: AI 없음 · 기록 안 함 · 끝까지 돌리면 마지막 체크섬 재현
        var rw = loaded.CreateWorld(defs);
        bool clean = rw.Controllers.Count == 0 && !rw.RecordCommands && rw.CommandFeed != null;
        while (rw.Tick < loaded.EndTick) rw.Step();
        check(clean && rw.ComputeChecksum() == loaded.EndChecksum && rw.CommandLog.Count == 0 && rw.ChecksumLog.Count == 0,
              $"재생 월드(AI 없음 · 기록 끔)를 {loaded.EndTick}틱 돌리면 마지막 체크섬 {loaded.EndChecksum:X16} 재현, 유닛 {rw.Units.Count} = {w.Units.Count}");
        check(rw.Units.Count == w.Units.Count && rw.Buildings.Count == w.Buildings.Count && rw.Winner == w.Winner,
              "재생 월드의 유닛 · 건물 수 · 승자가 원본과 같음");

        // 변조 감지 1: 중간 명령 하나 삭제 (디버그 생성 — 확실히 상태를 바꾸는 명령)
        var t1 = ReplayFile.Load(path);
        int si = t1.Commands.FindIndex(c => c.Command is DebugSpawnCommand && c.Tick == spawnTick);
        t1.Commands.RemoveAt(si);
        long bad1 = t1.Verify(defs, out string report1);
        check(bad1 != -1 && bad1 > spawnTick && bad1 <= spawnTick + ReplayFile.ChecksumInterval,
              $"변조 감지(명령 삭제): {report1}");

        // 변조 감지 2: 중반 이동 명령의 목표 변경
        var t2 = ReplayFile.Load(path);
        int mid = t2.Commands.Count / 2;
        // 후보 (가운데에 가까운 이동 명령) 를 차례로 바꿔 본다 — 목표를 조금 옮겨도 결과가 같은 명령이 있을 수 있어 (N54 이후 이 시나리오에서 실제로 있음) 첫 감지까지.
        long bad2 = -1; string report2 = ""; int tickAlt = 0; string kind = "";
        foreach (int mi in Enumerable.Range(0, t2.Commands.Count).OrderBy(i => Math.Abs(i - mid)).Where(i => t2.Commands[i].Command is MoveCommand or AttackMoveCommand).Take(12))
        {
            var t2b = ReplayFile.Load(path);
            var orig = t2b.Commands[mi];
            GameCommand altered = orig.Command switch
            {
                MoveCommand m => m with { Target = Nudge(m.Target, t2b) },
                AttackMoveCommand m => m with { Target = Nudge(m.Target, t2b) },
                _ => orig.Command,
            };
            t2b.Commands[mi] = new CommandRecord(orig.Tick, altered);
            bad2 = t2b.Verify(defs, out report2); tickAlt = (int)orig.Tick; kind = orig.Command.GetType().Name;
            if (bad2 != -1) break;
        }
        check(bad2 != -1 && bad2 > tickAlt, $"변조 감지(틱 {tickAlt} {kind} 목표 변경): {report2}");

        // 변조 감지 3: 마지막 체크섬만 바꾸면 끝에서 걸림
        var t3 = ReplayFile.Load(path);
        t3.EndChecksum ^= 1;
        long bad3 = t3.Verify(defs, out string report3);
        check(bad3 == t3.EndTick, $"변조 감지(마지막 체크섬): {report3}");

        try { File.Delete(path); } catch (IOException) { }
    }

    /// <summary>이동 목표를 맵 안에서 크게 옮긴다 (변조용).</summary>
    static Vector2 Nudge(Vector2 p, ReplayFile r)
    {
        float x = p.X + 25 <= r.MapWidth - 2 ? p.X + 25 : p.X - 25;
        float y = p.Y + 25 <= r.MapHeight - 2 ? p.Y + 25 : p.Y - 25;
        return new Vector2(Math.Clamp(x, 1, r.MapWidth - 2), Math.Clamp(y, 1, r.MapHeight - 2));
    }
}
