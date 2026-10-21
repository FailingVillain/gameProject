using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 탄약 사용량 측정기 (읽기 전용, 시뮬 상태 불변). TacticsObs 가 켜면 ConsumeAmmo 마다 유닛별 발수를 센다.
/// '교전' = 한 병사의 연속 사격 묶음 (마지막 사격 뒤 GapSec 초 넘게 쉬면 새 교전). 무기별로 교전당 발수 · 병사당 총 발수 · 탄 소진 비율 · 첫 소진 시각.
/// </summary>
public static class AmmoObs
{
    public static bool On;
    public const double GapSec = 30;
    sealed class U { public string W = ""; public int Max; public long Last = -1; public int Cur; public int Total; public List<int> Eng = new(); public long EmptyTick = -1; public bool Dry; }
    static readonly Dictionary<int, U> units = new();

    public static void Shot(Unit u, long tick)
    {
        if (!units.TryGetValue(u.Id, out var s)) units[u.Id] = s = new U { W = u.Weapon?.Key ?? "?", Max = u.AmmoMax };
        if (s.Last >= 0 && (tick - s.Last) * Config.SimDt > GapSec) { s.Eng.Add(s.Cur); s.Cur = 0; }
        s.Cur++; s.Total++; s.Last = tick;
        if (u.Ammo <= 0 && s.EmptyTick < 0) s.EmptyTick = tick;
    }

    static double Pct(List<int> l, double q) { if (l.Count == 0) return 0; var o = l.OrderBy(x => x).ToList(); return o[Math.Min(o.Count - 1, (int)(o.Count * q))]; }

    public static string Report(World w)
    {
        foreach (var s in units.Values) if (s.Cur > 0) { s.Eng.Add(s.Cur); s.Cur = 0; }
        var sb = new StringBuilder();
        sb.AppendLine($"[탄약사용] 교전 = 사격 간격 {GapSec:0}s 이내 묶음. 무기: 병사 / 교전수 / 교전당 발수 평균 p50 p90 / 병사당 총 / 한도 / 소진 병사 (첫 소진 중앙값 s)");
        foreach (var g in units.Values.GroupBy(x => x.W).OrderBy(g => g.Key))
        {
            var eng = g.SelectMany(x => x.Eng).ToList();
            var tot = g.Select(x => x.Total).ToList();
            var dry = g.Where(x => x.EmptyTick >= 0).Select(x => x.EmptyTick * Config.SimDt).OrderBy(x => x).ToList();
            sb.AppendLine($"  {g.Key}: {g.Count()} / {eng.Count} / {(eng.Count > 0 ? eng.Average() : 0):0.0} {Pct(eng, .5):0} {Pct(eng, .9):0} / {tot.Average():0.0} (최대 {tot.Max()}) / {g.First().Max} / {dry.Count}" + (dry.Count > 0 ? $" ({dry[dry.Count / 2]:0}s)" : ""));
        }
        return sb.ToString();
    }
}
