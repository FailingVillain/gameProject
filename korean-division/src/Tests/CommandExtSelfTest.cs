using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>v0.4 D: 전투 탭 설정 명령 (실행 · JSON · 리플레이 · 저장 왕복 · 체크섬), 산개 진형 슬롯, 공병 배정 명령. 창 없이.</summary>
public static class CommandExtSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[전투 탭 설정 · 산개]");
        var defs = Defs.Current;
        var setup = new BattleSetup { SideA = "infantry:20", SideB = "infantry:20", Map = "flat", Distance = 30 };

        // 기본값 (B 가 컴파일에 쓰는 이름 그대로)
        var w0 = setup.CreateWorld(defs, 3);
        var d = w0.Faction(1).Combat;
        check(d.DefaultFormation == FormType.Free, "전투 설정 기본값: 대형 해제");

        // 명령 실행 + 다른 세력 불변 + 체크섬 변화
        ulong c0 = w0.ComputeChecksum();
        w0.RecordCommands = true;
        w0.SetCombatSetting(1, CombatSetting.DefaultFormation, (int)FormType.Spread);
        w0.SetCombatSetting(1, CombatSetting.DefaultFormation, 77);          // 없는 대형은 무시
        w0.Step();
        var c = w0.Faction(1).Combat;
        check(c.DefaultFormation == FormType.Spread, "SetCombatSettingCommand 실행 (잘못된 대형 무시)");
        var o = w0.Faction(2).Combat;
        check(o.DefaultFormation == FormType.Free, "다른 세력 설정은 그대로");
        check(w0.ComputeChecksum() != c0, "설정이 체크섬에 들어감");

        // JSON 왕복
        var cmd = w0.CommandLog.Select(r => r.Command).OfType<SetCombatSettingCommand>().First();
        var back = JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize<GameCommand>(cmd, CommandJson.Options), CommandJson.Options);
        check(back is SetCombatSettingCommand b && b.Setting == cmd.Setting && b.Value == cmd.Value && b.Faction == 1, "SetCombatSettingCommand JSON 왕복");
        var ab = JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize<GameCommand>(new ChargeOnlyCommand(1, new[] { 5, 6 }, true), CommandJson.Options), CommandJson.Options);
        check(ab is ChargeOnlyCommand { Faction: 1, On: true } dg && dg.Units.SequenceEqual(new[] { 5, 6 }), "ChargeOnlyCommand JSON 왕복");

        // 리플레이: 기록한 명령을 새 월드에 먹이면 같은 체크섬
        var log = w0.CommandLog.ToList();
        ulong after = RunTo(w0, 120);
        var w1 = setup.CreateWorld(defs, 3);
        var byTick = log.GroupBy(r => r.Tick).ToDictionary(g => g.Key, g => g.Select(r => JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(r.Command, CommandJson.Options), CommandJson.Options)!).ToList());
        w1.CommandFeed = t => byTick.TryGetValue(t, out var l) ? l : Array.Empty<GameCommand>();
        w1.RecordCommands = false;
        ulong replay = RunTo(w1, 120);
        check(replay == after, $"설정 명령 리플레이 재생 → 같은 체크섬 ({after:x})");

        // 저장 / 불러오기 왕복
        var bytes = Snapshot.Save(w0, new SnapshotMeta("cmdext", 1, false, "battle"));
        var loaded = Snapshot.Load(bytes, defs).World;
        var lc = loaded.Faction(1).Combat;
        check(lc.DefaultFormation == FormType.Spread && loaded.Faction(2).Combat.DefaultFormation == FormType.Free, "저장 → 불러오기: 전투 설정 보존");
        check(loaded.ComputeChecksum() == w0.ComputeChecksum(), "불러온 월드의 체크섬이 저장 시점과 같음");

        // 산개 슬롯: 같은 인원의 밀집(방진) 격자보다 2.2 배 넓고, 겹치지 않으며, 평균이 원점
        const float s = 0.7f;
        foreach (int n in new[] { 1, 2, 9, 30 })
        {
            var box = Formations.Slots(FormType.Box, n, s);
            var spr = Formations.Slots(FormType.Spread, n, s);
            float minD = float.MaxValue;
            for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) minD = MathF.Min(minD, Vector2.Distance(spr[i], spr[j]));
            Vector2 mean = Vector2.Zero; foreach (var p in spr) mean += p;
            bool ok = spr.Length == n && (n < 2 || MathF.Abs(minD - s * Formations.SpreadFactor) < 0.01f) && (mean / n).Length() < 0.01f;
            check(ok, $"산개 슬롯 n={n}: 최소 간격 {(n < 2 ? 0 : minD):0.00} = {s * Formations.SpreadFactor:0.00} (밀집 {s}), 평균 원점");
        }
        // 산개 이동: 도착 후 간격이 밀집(Free)보다 넓다
        float Spread(FormSpec? f)
        {
            var w = setup.CreateWorld(defs, 4);
            var us = w.Units.Where(u => u.Owner == 1).Take(16).ToList();
            w.Move(1, us, new Vector2(w.Map.Width / 2f, w.Map.Height / 2f), f);
            for (int t = 0; t < 60 * 30; t++) w.Step();
            Vector2 cen = Vector2.Zero; foreach (var u in us) cen += u.Pos; cen /= us.Count;
            return us.Average(u => Vector2.Distance(u.Pos, cen));
        }
        float dense = Spread(null), wide = Spread(new FormSpec((int)FormType.Spread, 0, Vector2.Zero));
        check(wide > dense * 1.4f, $"산개 이동: 도착 후 평균 반경 {wide:0.0} > 해제(밀집) {dense:0.0} x1.4");
    }

    static ulong RunTo(World w, long tick)
    {
        while (w.Tick < tick) w.Step();
        return w.ComputeChecksum();
    }
}
