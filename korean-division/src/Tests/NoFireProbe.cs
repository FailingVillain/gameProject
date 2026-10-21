using System.Numerics;
using System.Text;
using KoreanDivision.Core;
namespace KoreanDivision.Tests;

/// <summary>
/// 미사격 진단 프로브 (읽기 전용, KD_NOFIRE=1 일 때만 TacticsObs 가 부른다; 시뮬 상태 불변).
/// 매 초: 사거리 안에 아군 시야로 보이는 적이 있는데 최근 4초 안에 쏘지 않은 사수를 찾아 이유를 분류한다. 출력: "NOFIRE ..." 줄.
/// </summary>
public static class NoFireProbe
{
    public static bool On => Environment.GetEnvironmentVariable("KD_NOFIRE") == "1";
    static readonly Dictionary<string, int> Reasons = new();
    static readonly Dictionary<string, Dictionary<string, int>> ByType = new();
    static readonly Dictionary<string, int> Armed = new();   // 종류별 '사거리 안 적 있는' 사수 · 초
    static readonly List<(int shooter, string reason, double t, string key, float dist)> Samples = new();
    static int totalArmedSec, totalNoFire;
    const int QuietTicks = 240;

    public static void Tick(World w)
    {
        foreach (var u in w.Units)
        {
            if (!u.Alive || u.Weapon is not { } wp || u.Def.IsOfficer && false) continue;
            if (w.IsIndirectForProbe(wp)) continue;
            Unit? best = null; float bd = float.MaxValue; Unit? blocked = null; float bkd = float.MaxValue;
            foreach (var e in w.UnitsInRadius(u.Pos, wp.Range + 2f))
            {
                if (!e.Alive || e.Owner == u.Owner || !w.AreEnemies(u.Owner, e.Owner)) continue;
                float d = e.EdgeDistance(u.Pos) - u.Radius;
                if (d > wp.Range || d < wp.MinRange || d >= bd) continue;
                if (!w.Fog.CanSee(u.Owner, e)) continue;
                if (!w.HasFiringLine(u, e, wp)) { if (d < bkd) { blocked = e; bkd = d; } continue; }
                best = e; bd = d;
            }
            bool allBlocked = false;
            if (best == null) { if (blocked == null) continue; best = blocked; bd = bkd; allBlocked = true; }
            string key = u.Def.Key;
            Armed[key] = Armed.GetValueOrDefault(key) + 1; totalArmedSec++;
            if (w.Tick - u.LastShotTick < QuietTicks) continue;
            string r = allBlocked ? "05 사선 막힘(전부): " + w.Map.FiringLineBlockerForProbe(u, best) : w.ProbeReason(u, best, bd);
            totalNoFire++;
            Reasons[r] = Reasons.GetValueOrDefault(r) + 1;
            if (!ByType.TryGetValue(key, out var m)) ByType[key] = m = new();
            m[r] = m.GetValueOrDefault(r) + 1;
            if (Samples.Count < 4000 && (u.Id % 7 == 0)) Samples.Add((u.Id, r, w.Time, key, bd));
        }
    }

    public static void Report(string label)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"NOFIRE {label}: 사거리 안 적 있는 사수-초 {totalArmedSec}, 그중 4초 이상 미사격 {totalNoFire} ({(totalArmedSec == 0 ? 0 : 100.0 * totalNoFire / totalArmedSec):0.0}%)");
        foreach (var kv in Reasons.OrderByDescending(k => k.Value)) sb.AppendLine($"NOFIRE reason {kv.Key} {kv.Value} ({100.0 * kv.Value / Math.Max(1, totalNoFire):0.0}% of nofire, {100.0 * kv.Value / Math.Max(1, totalArmedSec):0.00}% of armed)");
        foreach (var t in ByType.OrderByDescending(k => k.Value.Values.Sum()))
        {
            int n = t.Value.Values.Sum(); int a = Armed.GetValueOrDefault(t.Key);
            sb.AppendLine($"NOFIRE type {t.Key}: armed {a} nofire {n} ({100.0 * n / Math.Max(1, a):0.0}%) :: " + string.Join(", ", t.Value.OrderByDescending(k => k.Value).Take(4).Select(k => $"{k.Key}={k.Value}")));
        }
        Console.Write(sb.ToString());
    }
}
