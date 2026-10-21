using System.Numerics;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>[측정 전용, 읽기 전용] 중화기 (setupTime 있는 유닛) 방열 상태별 기-초 · 이동 · 사살. 1초마다 Sample, 끝에 Report.</summary>
public static class HeavyObs
{
    sealed class A { public long Dep, Setup, Pack, Packed, PackedMoving, Alive; }
    static readonly Dictionary<string, A> by = new();
    static readonly Dictionary<int, Vector2> prev = new();

    public static void Reset() { by.Clear(); prev.Clear(); }

    public static void Sample(World w)
    {
        foreach (var u in w.Units)
        {
            if (!u.Alive || !u.CanDeploy) continue;
            string k = u.Def.Key + "/f" + u.Owner;
            if (!by.TryGetValue(k, out var a)) by[k] = a = new A();
            a.Alive++;
            switch (u.Deploy)
            {
                case DeployPhase.Deployed: a.Dep++; break;
                case DeployPhase.Deploying: a.Setup++; break;
                case DeployPhase.Packing: a.Pack++; break;
                default:
                    a.Packed++;
                    if (prev.TryGetValue(u.Id, out var p) && Vector2.Distance(p, u.Pos) > 0.5f) a.PackedMoving++;
                    break;
            }
            prev[u.Id] = u.Pos;
        }
    }

    public static string Report(World w)
    {
        var sb = new StringBuilder();
        var kills = new Dictionary<string, (int n, int k)>();
        foreach (var u in w.Units) if (u.Def.SetupTime > 0)
        {
            string k = u.Def.Key + "/f" + u.Owner; kills.TryGetValue(k, out var v); kills[k] = (v.n + 1, v.k + u.Kills);
        }
        sb.AppendLine("[중화기방열] 기-초: 방열완료 / 방열중 / 철수중 / 이동자세(그중 이동) | 시작 기 · 사살");
        foreach (var kv in by.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var a = kv.Value; kills.TryGetValue(kv.Key, out var kk);
            sb.AppendLine($"  {kv.Key}: {a.Dep} / {a.Setup} / {a.Pack} / {a.Packed} ({a.PackedMoving}) | {kk.n}기 사살 {kk.k}");
        }
        return sb.ToString();
    }
}
