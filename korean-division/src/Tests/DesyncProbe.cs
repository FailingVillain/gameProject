using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>[진단 도구: --probe] 기록 실행과 재생 실행을 틱마다 비교해 처음 어긋나는 틱 · 엔티티를 출력.</summary>
public static class DesyncProbe
{
    /// <summary>[--probe snap] 저장 → 불러오기 뒤 원본과 사본을 틱마다 비교 (2인 생성맵 · 표준 vs 수비 AI).</summary>
    public static void RunSnap(int seed, int pre)
    {
        var defs = Defs.Current;
        int n = int.TryParse(Environment.GetEnvironmentVariable("KD_PROBE_N"), out var pn) ? pn : 10;
        var a = SnapshotSelfTest.ProbeGen(n);
        for (int i = 0; i < pre; i++) a.Step();
        var bytes = Snapshot.Save(a, new SnapshotMeta("probe", 1, false, "probe"));
        var b = Snapshot.Load(bytes, defs).World;
        // 불러온 직후 모든 필드 비교 (리플렉션)
        static string V(object? o) => o switch { null => "null", Entity e => "E" + e.Id, float f => f.ToString("R"), double d => d.ToString("R"), System.Collections.ICollection c => "n" + c.Count, _ => o.ToString() ?? "" };
        int diffs = 0;
        void Cmp(string tag, object x, object y)
        {
            for (var t = x.GetType(); t != null && t != typeof(object); t = t.BaseType)
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    string vx = V(f.GetValue(x)), vy = V(f.GetValue(y));
                    if (vx != vy && diffs < 40) { Console.WriteLine($" {tag}.{f.Name}: {vx} vs {vy}"); diffs++; }
                }
        }
        for (int i = 0; i < a.Units.Count; i++) Cmp($"U{a.Units[i].Id}", a.Units[i], b.Units[i]);
        for (int i = 0; i < a.Buildings.Count; i++) Cmp($"B{a.Buildings[i].Id}", a.Buildings[i], b.Buildings[i]);
        for (int i = 0; i < a.Factions.Count; i++) Cmp($"F{i}", a.Factions[i], b.Factions[i]);
        for (int i = 0; i < a.Companies.Count; i++)
        {
            var ca = a.Companies[i]; var cb = b.Companies[i];
            Cmp($"C{ca.Id}", ca, cb);
            if (ca.Anchor is { } xa && cb.Anchor is { } xb)
            {
                Cmp($"C{ca.Id}.anchor", xa, xb);
                Cmp($"C{ca.Id}.flag", xa.Flag, xb.Flag);
                string D(Dictionary<int, Vector2> d) => string.Join(";", d.Select(kv => $"{kv.Key}:{kv.Value.X:R},{kv.Value.Y:R}"));
                if (D(xa.Slots) != D(xb.Slots)) Console.WriteLine($" C{ca.Id} slots order/content differ");
                if (D(xa.FinalOff) != D(xb.FinalOff)) Console.WriteLine($" C{ca.Id} finaloff differ");
                if (D(xa.ColumnSlots) != D(xb.ColumnSlots)) Console.WriteLine($" C{ca.Id} colslots differ");
                if (string.Join(";", xa.Trail) != string.Join(";", xb.Trail)) Console.WriteLine($" C{ca.Id} trail differ");
                Cmp($"C{ca.Id}.group", xa.Group, xb.Group);
            }
        }
        Console.WriteLine($"field diffs after load: {diffs}");
        for (int i = 0; i < 60 * 120; i++)
        {
            a.Step(); b.Step();
            string sa = Snap(a), sb = Snap(b);
            if (sa != sb)
            {
                Console.WriteLine($"SNAP DESYNC at tick {a.Tick} (저장 틱 {pre})");
                var x = sa.Split('\n'); var y = sb.Split('\n');
                int shown = 0;
                for (int k = 0; k < Math.Max(x.Length, y.Length) && shown < 12; k++)
                {
                    string p = k < x.Length ? x[k] : "-", q = k < y.Length ? y[k] : "-";
                    if (p != q) { Console.WriteLine($" orig: {p}\n load: {q}"); shown++; }
                }
                return;
            }
        }
        Console.WriteLine("no snap desync in 120s");
    }

    public static void Run(int seed)
    {
        var defs = Defs.Current;
        var rec = World.CreateStandard(MapFile.Load("flat4", seed), defs);
        rec.Fog.Enabled = false;
        rec.Controllers.Add((1, AiRegistry.Create("standard")!));
        rec.Controllers.Add((2, AiRegistry.Create("rush")!));
        var snaps = new List<string>();
        for (int i = 0; i < 60 * 150; i++) { rec.Step(); snaps.Add(Snap(rec)); }
        var rep = ReplayFile.FromWorld(rec);
        var w = rep.CreateWorld(defs);
        for (int i = 0; i < snaps.Count; i++)
        {
            w.Step();
            string s = Snap(w);
            if (s != snaps[i])
            {
                Console.WriteLine($"DESYNC at tick {w.Tick}");
                var a = snaps[i].Split('\n'); var b = s.Split('\n');
                for (int k = 0; k < Math.Max(a.Length, b.Length); k++)
                {
                    string x = k < a.Length ? a[k] : "-", y = k < b.Length ? b[k] : "-";
                    if (x != y) { Console.WriteLine($" rec: {x}\n rep: {y}"); }
                }
                var cmds = rep.Commands.Where(c => Math.Abs(c.Tick - w.Tick) < 3).ToList();
                foreach (var c in cmds) Console.WriteLine($"  cmd@{c.Tick}: {c.Command}");
                return;
            }
        }
        Console.WriteLine("no desync in 150s");
    }

    static string Snap(World w)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var f in w.Factions) sb.Append($"F{f.Id} d{(f.Defeated ? 1 : 0)}\n");
        foreach (var u in w.Units) sb.Append($"U{u.Id} o{u.Owner} {u.Def.Key} {u.Pos.X:R},{u.Pos.Y:R} hp{u.Hp:R} {u.Order} t{u.Target?.Id} g{(u.Group != null ? u.Group.Target.ToString() : "-")} cd{u.Cooldown:R}\n");
        foreach (var b in w.Buildings) sb.Append($"B{b.Id} o{b.Owner} {b.Def.Key} hp{b.Hp:R} cp{b.CaptureProgress:R}\n");
        return sb.ToString();
    }
}
