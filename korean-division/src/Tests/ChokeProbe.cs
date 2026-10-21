using System.Numerics;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 길목 (choke) 측정 장면: 강 (물 12칸) 에 폭 W 칸 다리 하나, 중대 N개 (대대) 가 다리 건너로 이동.
///   KoreanDivision --choke [w=3] [co=3] [men=40] [ticks=5400] [seeds=1] [off=1] [terrain=bridge|ford]
/// 측정: 전원 통과 틱 (y > 다리 끝 + 4) · 길목 구역 (다리 ± 12칸) 에서 걷는 유닛의 진행 방향 역전 / 유닛-분 · 정체 (StuckLevel ≥ 2) 표본 · 밀림 수 · 겹침 해소 밀림 합.
/// off=1 이면 길목 기능을 끈다 (before).
/// </summary>
public static class ChokeProbe
{
    public sealed record Result(int CrossTick, int AllArrivedTick, double ReversalsPerUnitMin, long StuckSamples, long Pushes, double PushSum, int Crossed, int Total);

    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        int width = 3, co = 3, men = 40, ticks = 5400, seeds = 1; bool off = false; string terrain = "bridge";
        foreach (var a in args)
        {
            int eq = a.IndexOf('=');
            if (eq < 0) continue;
            string k = a[..eq].ToLowerInvariant(), v = a[(eq + 1)..];
            switch (k)
            {
                case "w": width = int.Parse(v); break;
                case "co": co = int.Parse(v); break;
                case "men": men = int.Parse(v); break;
                case "ticks": ticks = int.Parse(v); break;
                case "seeds": seeds = int.Parse(v); break;
                case "off": off = v != "0"; break;
                case "terrain": terrain = v; break;
            }
        }
        var defs = Defs.Load();
        defs.CombatRules.ChokeEnabled = !off;
        if (Environment.GetEnvironmentVariable("KD_CQ_PUSH") is { } pv) defs.CombatRules.ChokePushScale = float.Parse(pv, System.Globalization.CultureInfo.InvariantCulture);
        Console.WriteLine($"[choke] 다리 폭 {width} · 중대 {co} x {men} · {terrain} · 길목 기능 {(off ? "끔" : "켬")}");
        for (int s = 0; s < seeds; s++)
        {
            var r = Measure(defs, width, co, men, ticks, 100 + s, terrain);
            Console.WriteLine($" seed {100 + s}: 통과 {r.Crossed}/{r.Total} · 전원 통과 틱 {r.CrossTick} · 도착 틱 {r.AllArrivedTick} · 역전/유닛-분 {r.ReversalsPerUnitMin:0.00} · 정체표본 {r.StuckSamples} · 밀림 {r.Pushes} (합 {r.PushSum:0.0})");
        }
        return 0;
    }

    static readonly bool Dbg = Environment.GetEnvironmentVariable("KD_CHOKE_DEBUG") == "1";
    static readonly Dictionary<string, int> RevMap = new();
    public const int BridgeY0 = 60, BridgeY1 = 72, BridgeX = 60;

    public static GameMap MakeMap(int width, string terrain)
    {
        const int sx = 120, sy = 150;
        var m = new GameMap(sx, sy, 1);
        for (int y = 0; y < sy; y++)
        for (int x = 0; x < sx; x++)
        {
            bool river = y >= BridgeY0 && y < BridgeY1;
            bool bridge = river && x >= BridgeX - width / 2 && x < BridgeX - width / 2 + width;
            m.Set(x, y, river ? (bridge ? (terrain == "ford" ? TerrainType.Sand : TerrainType.Dirt) : TerrainType.Water) : TerrainType.Grass);
        }
        return m;
    }

    public static (World W, List<Company> Cs, List<Unit> Us) Setup(Defs defs, int width, int co, int men, int seed, string terrain)
    {
        var m = MakeMap(width, terrain);
        var w = new World(m, defs, seed) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, m.Width - 1 - hq.Size, m.Height - 1 - hq.Size);
        w.Fog.Enabled = false;
        var all = new List<Unit>();
        for (int c = 0; c < co; c++)
        {
            var us = new List<Unit>();
            for (int i = 0; i < men; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(BridgeX - co * 11 + c * 22 + 0.5f + i % 20, 20.5f + i / 20)));
            w.FormCompany(1, us);
            all.AddRange(us);
        }
        w.Step();
        w.FormBattalion(1, w.Companies.Where(c => c.Owner == 1).ToList());
        w.Step();
        return (w, w.Companies.Where(c => c.Owner == 1).ToList(), all);
    }

    public static Result Measure(Defs defs, int width, int co, int men, int ticks, int seed, string terrain)
    {
        using var nd = TestDefs.NoCommandDelay();
        var (w, cs, us) = Setup(defs, width, co, men, seed, terrain);
        w.Move(1, us, new Vector2(BridgeX, 125));
        int crossTick = -1, arrTick = -1;
        long rev = 0, walkTicks = 0, stuck = 0;
        var prev = new Dictionary<int, Vector2>(); var prev2 = new Dictionary<int, Vector2>();
        var st0 = w.Stats.SepPushUnits; double sum0 = w.Stats.SepPushSum;
        for (int t = 1; t <= ticks; t++)
        {
            w.Step();
            int past = 0;
            foreach (var u in us)
            {
                if (!u.Alive) continue;
                bool zone = u.Pos.Y > BridgeY0 - 12 && u.Pos.Y < BridgeY1 + 12 && MathF.Abs(u.Pos.X - BridgeX) < 14;
                if (u.Pos.Y > BridgeY1 + 4) past++;
                if (zone)
                {
                    if (u.Walking)
                    {
                        walkTicks++;
                        if (prev.TryGetValue(u.Id, out var p1) && prev2.TryGetValue(u.Id, out var p2))
                        {
                            var d0 = p1 - p2; var d1 = u.Pos - p1;
                            if (d0.LengthSquared() > 1e-5f && d1.LengthSquared() > 1e-5f && Vector2.Dot(d0, d1) < 0) { rev++; if (Dbg) { var key = $"y{(int)(u.Pos.Y / 4) * 4:000} x{(int)((u.Pos.X - BridgeX + 100) / 4) * 4 - 100:+00;-00} co{u.Company?.Id} {(u.Company?.Anchor?.Column == true ? "col" : "nocol")}"; RevMap[key] = RevMap.GetValueOrDefault(key) + 1; } }
                        }
                    }
                    if (u.StuckLevel >= 2) stuck++;
                }
                if (prev.TryGetValue(u.Id, out var q)) prev2[u.Id] = q;
                prev[u.Id] = u.Pos;
            }
            if (Dbg && t % 60 == 0)
            {
                foreach (var c in cs)
                {
                    var a = c.Anchor; if (a == null) continue;
                    int inz = c.Members.Count(m => m.Pos.Y > BridgeY0 - 12 && m.Pos.Y < BridgeY1 + 12 && MathF.Abs(m.Pos.X - BridgeX) < 14);
                    int far = c.Members.Count(m => Vector2.Distance(m.Pos, w.MarchSlot(a, m)) > 2.5f);
                    Console.WriteLine($"  t={t} co{c.Id} flag=({a.Pos.X:0.0},{a.Pos.Y:0.0}) col={a.Column} mul={a.SpeedMul:0.00} hold={c.ChokeHold} inZone={inz} lag>2.5={far} stuck={c.Members.Count(m => m.StuckLevel >= 2)} walking={c.Members.Count(m => m.Walking)} arr={a.Arrived}");
                }
            }
            if (crossTick < 0 && past >= us.Count(x => x.Alive)) crossTick = t;
            if (crossTick >= 0 && arrTick < 0 && cs.All(c => c.Anchor == null || c.Anchor.Arrived)) { arrTick = t; break; }
        }
        if (Dbg) foreach (var kv in RevMap.OrderByDescending(k => k.Value).Take(14)) Console.WriteLine($"  [rev] {kv.Key}: {kv.Value}");
        double unitMin = Math.Max(1, walkTicks) / 3600.0;
        return new Result(crossTick, arrTick, rev / unitMin, stuck, w.Stats.SepPushUnits - st0, w.Stats.SepPushSum - sum0, us.Count(u => u.Pos.Y > BridgeY1 + 4), us.Count);
    }
}
