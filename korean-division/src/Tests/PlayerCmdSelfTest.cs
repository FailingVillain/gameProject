using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 플레이어 명령 (2026-10-19): N36 섞인 선택 돌격 (카드 · 단축키 · 돌격 가능한 병사만), N37 선택 인원별 명령 지연 (곡선 · 결정성 · 리플레이),
/// N38 철조망은 사선을 막지 않음 (벽 · 가옥은 막음). 창 없이. KD_SELFTEST_ONLY=playercmd 로 단독 실행.
/// </summary>
public static class PlayerCmdSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[플레이어 명령: 섞인 돌격 · 선택 인원 지연 · 철조망 사선]");
        try
        {
            MixedCharge();
            SelDelay();
            SelReplay();
            WireFire();
        }
        catch (Exception e) { check(false, $"플레이어 명령 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 120)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static List<Unit> Line(World w, int n, Vector2 c, string def = "infantry", int faction = 1)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++) list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2(i * 1.2f, 0)));
        return list;
    }

    // ---------------------------------------------------------------- N36

    static void MixedCharge()
    {
        var w = Bare();
        var inf = Line(w, 6, new Vector2(30, 60));
        var mg = Line(w, 2, new Vector2(30, 64), "heavy_machinegunner");
        var wagon = w.SpawnUnit(defs.Units["supply_wagon"], 1, new Vector2(30, 68));
        var mixed = inf.Concat(mg).Append(wagon).ToList();
        var charge = Commands.Get("charge")!;
        check(Game.CardAvailable(charge, mixed) && Game.CardAvailable(charge, inf) && !Game.CardAvailable(charge, mg.Append(wagon).ToList()),
            "N36 돌격 버튼 (카드 · 단축키 C 공통): 섞인 선택에 하나라도 돌격 가능하면 보임, 아무도 못 하면 숨김");
        check(!Game.CardAvailable(Commands.Get("quickstep")!, mixed) && Game.CardAvailable(Commands.Get("attack")!, mixed), "다른 버튼 규칙 그대로 (속보 = 전원, 공격 = 하나라도)");
        w.ChargeMixed(1, mixed, new Vector2(30, 100));
        w.Step();
        check(inf.All(u => u.Charging) && mg.All(u => !u.Charging && u.Order == UnitOrder.Hold) && !wagon.Charging && wagon.Order == UnitOrder.Hold,
            $"N36 섞인 선택 돌격: 보병만 돌격 ({inf.Count(u => u.Charging)}/{inf.Count}), 기관총 · 수레는 사수 ({string.Join(",", mg.Append(wagon).Select(u => u.Order))})");
        var w2 = Bare();
        var only = Line(w2, 2, new Vector2(30, 64), "heavy_machinegunner");
        w2.RecordCommands = true;
        w2.ChargeMixed(1, only, new Vector2(30, 100));
        w2.Step();
        check(w2.CommandLog.Count == 0 && only.All(u => !u.Charging), $"돌격 가능한 병사가 없으면 아무 명령도 안 냄 (기록 {w2.CommandLog.Count})");
    }

    // ---------------------------------------------------------------- N37

    static int[] DelaysWith(int sel, out List<Unit> us)
    {
        var w = Bare();
        us = Line(w, 10, new Vector2(30, 60));   // 분대 없음 = 고립 단계 (지휘 계통 지연 최대)
        w.IssueSel = sel;
        w.AttackMove(1, us, new Vector2(30, 100));
        w.IssueSel = 0;
        w.Step();
        return us.Select(u => u.ReactDelay).ToArray();
    }

    static void SelDelay()
    {
        var w = Bare();
        float m1 = w.PlayerSelDelayMul(1), m10 = w.PlayerSelDelayMul(10), m40 = w.PlayerSelDelayMul(40), m150 = w.PlayerSelDelayMul(150), m500 = w.PlayerSelDelayMul(500), m2000 = w.PlayerSelDelayMul(2000);
        check(m1 < 0.5f && m1 <= m10 && m10 < m40 && m40 < m150 && m150 < m500 && MathF.Abs(m500 - 1f) < 1e-5f && MathF.Abs(m2000 - 1f) < 1e-5f && w.PlayerSelDelayMul(0) == 1f,
            $"N37 지연 곡선 (balance.json command.playerSel*): 1명 ×{m1:0.00} · 분대 ×{m10:0.00} · 소대 ×{m40:0.00} · 중대 ×{m150:0.00} · 대대 ×{m500:0.00} · AI(0) ×1");
        var small = DelaysWith(10, out _);
        var big = DelaysWith(600, out _);
        var ai = DelaysWith(0, out _);
        check(big.SequenceEqual(ai), "대대 이상 선택 = 지금 수준 (AI 명령과 같은 지연)");
        check(small.Zip(big).All(p => p.First <= p.Second) && small.Sum() * 2 < big.Sum(),
            $"분대 선택 지연이 훨씬 짧음: 합 {small.Sum()} 틱 vs 대대 {big.Sum()} 틱 (평균 {small.Average() / 60:0.00}s vs {big.Average() / 60:0.00}s)");
        check(DelaysWith(10, out _).SequenceEqual(small), "같은 선택 인원 = 같은 지연 (결정적)");
    }

    static void SelReplay()
    {
        var setup = new BattleSetup { SideA = "infantry:20", SideB = "infantry:20", Map = "flat", Distance = 30 };
        var w0 = setup.CreateWorld(defs, 5);
        w0.RecordCommands = true;
        var mine = w0.Units.Where(u => u.Owner == 1 && u.Alive).ToList();
        w0.IssueSel = 4;
        w0.Move(1, mine.Take(4), mine[0].Pos + new Vector2(0, -6));
        w0.IssueSel = 0;
        w0.AttackMove(1, mine.Skip(4), mine[0].Pos + new Vector2(0, 10));
        w0.Step();
        var log = w0.CommandLog.ToList();
        var mv = log.Select(r => r.Command).OfType<MoveCommand>().FirstOrDefault();
        var json = mv == null ? "" : JsonSerializer.Serialize<GameCommand>(mv, CommandJson.Options);
        var back = mv == null ? null : JsonSerializer.Deserialize<GameCommand>(json, CommandJson.Options);
        var am = log.Select(r => r.Command).OfType<AttackMoveCommand>().FirstOrDefault();
        check(mv is { Sel: 4 } && back is MoveCommand { Sel: 4 } && am is { Sel: 0 } && !JsonSerializer.Serialize<GameCommand>(am, CommandJson.Options).Contains("Sel"),
            $"N37 선택 인원이 명령에 실림 · JSON 왕복 (AI 명령은 Sel 생략): {json}");
        for (int i = 0; i < 180; i++) w0.Step();
        ulong after = w0.ComputeChecksum();
        var w1 = setup.CreateWorld(defs, 5);
        var byTick = log.GroupBy(r => r.Tick).ToDictionary(g => g.Key, g => g.Select(r => JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(r.Command, CommandJson.Options), CommandJson.Options)!).ToList());
        w1.CommandFeed = t => byTick.TryGetValue(t, out var l) ? l : Array.Empty<GameCommand>();
        w1.RecordCommands = false;
        while (w1.Tick < w0.Tick) w1.Step();
        check(w1.ComputeChecksum() == after, $"N37 리플레이 재생 → 같은 체크섬 ({after:x})");
        check(ReplayFile.FormatVersion >= 12, $"리플레이 형식 버전 {ReplayFile.FormatVersion} (≥12, Sel)");
    }

    // ---------------------------------------------------------------- N38

    static void WireFire()
    {
        var w = Bare(60);
        var m = w.Map;
        for (int y = 0; y < 60; y++) { m.Set(20, y, TerrainType.Wire); m.Set(40, y, TerrainType.House); }
        w.PlaceBuilding(TestDefs.Wall(2), 1, 30, 40);
        static Vector2 C(int x, int y) => new(x + 0.5f, y + 0.5f);
        check(m.HasFiringLine(C(12, 10), C(28, 10), 0, 0) && !m.HasLineOfSight(C(12, 10), C(28, 10)) && m.HasFiringLine(C(12, 10), C(28, 14), 0, 0),
            "N38 철조망 칸은 사선을 막지 않음 (이동용 직선 검사는 그대로 막음, 사선 · 대각도)");
        check(!m.HasFiringLine(C(34, 10), C(46, 10), 0, 0), "가옥은 여전히 사선을 막음");
        check(!m.HasFiringLine(C(25, 41), C(36, 41), 0, 0), "벽 (건물 점유 칸) 은 여전히 사선을 막음");
        // 실제 사격: 철조망 너머 적을 쏜다
        var s = w.SpawnUnit(defs.Units["infantry"], 1, C(14, 20));
        var t = w.SpawnUnit(defs.Units["infantry"], 2, C(26, 20));
        w.Hold(1, new[] { s }); w.Hold(2, new[] { t });
        for (int i = 0; i < 60 * 8 && s.LastShotTick < 0; i++) w.Step();
        check(s.LastShotTick >= 0, $"철조망 너머 적에게 실제 사격 (마지막 사격 틱 {s.LastShotTick})");
    }
}
