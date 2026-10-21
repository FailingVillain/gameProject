using System.Globalization;
using System.Numerics;
using System.Text;
using KoreanDivision.Core;
namespace KoreanDivision.Tests;
/// <summary>
/// 중대 뭉침 측정 (읽기 전용, docs/BUNCHING_DIAG_2026-10-19.md): <c>--bunch &lt;미션&gt; [--seed N] [--seconds S] [--out f.csv] [--dump pos.csv --dump-at 0.5,2,…]</c>.
/// 전 세력 미션 AI 로 돌리며 1초마다 중대별 (보병 10명 이상, 장교 · 기병 · 대형 제외) RMS 반경 · 최근접 이웃 (nn) · 0.7칸 안 겹침 비율 (ovl) · 방향 반전 수 ·
/// 깃발 상태 · 종대 (col) · 자취 길이 / 종대 길이 (cov) · 막힌 · 지도 밖 슬롯 수를 CSV 로. --dump 는 지정 초의 모든 유닛 위치 (접촉표용). 시뮬 상태는 바꾸지 않는다.
/// </summary>
public static class BunchProbe
{
    public static int Run(string[] args)
    {
        var defs = Defs.Load();
        string id = args[1]; float seconds = 300; int seed = 1; string outp = "bunch.csv"; string? dump = null; var dumpAt = new List<long>();
        for (int i = 2; i < args.Length; i++) switch (args[i]) { case "--seconds": seconds = float.Parse(args[++i], CultureInfo.InvariantCulture); break; case "--seed": seed = int.Parse(args[++i]); break; case "--out": outp = args[++i]; break; case "--dump": dump = args[++i]; break; case "--dump-at": foreach (var x in args[++i].Split(',')) dumpAt.Add((long)MathF.Round(float.Parse(x, CultureInfo.InvariantCulture) / Config.SimDt)); break; }
        var def = MissionDef.Load(id);
        var w = World.CreateMissionWorld(def, defs, seed, new List<DeployEntry>());
        World.AttachMissionAi(w, def.Player, true);
        long max = (long)(seconds / Config.SimDt); int every = (int)MathF.Round(1f / Config.SimDt);
        var sb = new StringBuilder("t,f,cid,n,cx,cy,rms,nn,ovl,inGrp,goals,topGoal,anchor,drill,idle,move,amove,att,walk,rev,newgrp,fac_dx,fac_dy,sepPush,slotAsg,gapRef,closeMov,blk,oob,nearFlag,col,arr,cov,why\n");
        var dsb = new StringBuilder("t,f,cid,x,y,col\n");
        var prevV = new Dictionary<int, Vector2>(); var prevG = new Dictionary<int, MoveGroup?>();
        var revC = new Dictionary<int, int>(); var newG = new Dictionary<int, int>();
        long ps = 0, pa = 0, pg = 0, pc = 0;
        while (w.Winner == 0 && w.Tick < max)
        {
            w.Step();
            if (dump != null && dumpAt.Contains(w.Tick))
                foreach (var u in w.Units)
                    if (u.Alive) dsb.Append(FormattableString.Invariant($"{w.Tick * Config.SimDt:0.00},{u.Owner},{u.Company?.Id ?? 0},{u.Pos.X:0.00},{u.Pos.Y:0.00},{(u.Company?.Anchor is { Column: true } ? 1 : 0)}\n"));
            foreach (var u in w.Units)
            {
                if (!u.Alive || u.Company == null) continue;
                if (prevV.TryGetValue(u.Id, out var pv) && pv.LengthSquared() > 0.01f && u.Velocity.LengthSquared() > 0.01f && Vector2.Dot(Vector2.Normalize(pv), Vector2.Normalize(u.Velocity)) < -0.3f) { int cid = u.Company.Id; revC[cid] = revC.GetValueOrDefault(cid) + 1; }
                if (prevG.TryGetValue(u.Id, out var pg0) && !ReferenceEquals(pg0, u.Group) && u.Group != null) { int cid = u.Company.Id; newG[cid] = newG.GetValueOrDefault(cid) + 1; }
                prevV[u.Id] = u.Velocity; prevG[u.Id] = u.Group;
            }
            if (w.Tick % every != 0) continue;
            var st = w.Stats;
            long dS = st.SepPushUnits - ps, dA = st.SlotAssign - pa, dG = st.GapRefills - pg, dC = st.CloseMovers - pc; ps = st.SepPushUnits; pa = st.SlotAssign; pg = st.GapRefills; pc = st.CloseMovers;
            foreach (var c in w.Companies)
            {
                var ms = c.Members.Where(u => u.Alive && !u.IsOfficer && !u.Def.Mounted && !u.Large).ToList();
                if (ms.Count < 10) continue;
                Vector2 cen = Vector2.Zero; foreach (var u in ms) cen += u.Pos; cen /= ms.Count;
                double rms = Math.Sqrt(ms.Average(u => Vector2.DistanceSquared(u.Pos, cen)));
                double nnS = 0; int ov = 0;
                foreach (var u in ms)
                {
                    float best = 1e9f;
                    foreach (var o in ms) { if (o == u) continue; float d = Vector2.Distance(u.Pos, o.Pos); if (d < best) best = d; }
                    nnS += best; if (best < 0.7f) ov++;
                }
                int blk = 0, oob = 0, nearF = 0; bool col = false, arr = false;
                if (c.Anchor is { } an)
                {
                    col = an.Column; arr = an.Arrived; Vector2 rr = new(-an.Heading.Y, an.Heading.X);
                    foreach (var u in ms)
                    {
                        if (Vector2.Distance(u.Pos, an.Pos) < 1.5f) nearF++;
                        if (!an.Slots.TryGetValue(u.Id, out var l)) continue;
                        var q = an.Pos + rr * l.X + an.Heading * l.Y;
                        if (q.X < 0.5f || q.Y < 0.5f || q.X > w.Map.Width - 0.5f || q.Y > w.Map.Height - 0.5f) { oob++; continue; }
                        var (tx, ty) = GameMap.TileOf(q);
                        if (!w.Map.IsWalkable(tx, ty)) blk++;
                    }
                }
                double cov = -1; int why = 0;
                if (c.Anchor is { } a2)
                {
                    double tl = 0; for (int q = 1; q < a2.Trail.Count; q++) tl += Vector2.Distance(a2.Trail[q], a2.Trail[q - 1]); tl += a2.Trail.Count > 0 ? Vector2.Distance(a2.Trail[^1], a2.Pos) : 0;
                    double need = ((a2.Slots.Count + 1) / 2) * a2.Spacing; cov = need > 0 ? tl / need : 9;
                    Vector2 rr2 = new(-a2.Heading.Y, a2.Heading.X);
                    Vector2 l0 = a2.Pos - rr2 * a2.HalfWidth, r0 = a2.Pos + rr2 * a2.HalfWidth;
                    bool oobl = l0.X < 0.5f || l0.Y < 0.5f || r0.X < 0.5f || r0.Y < 0.5f || l0.X > w.Map.Width - 0.5f || r0.X > w.Map.Width - 0.5f || l0.Y > w.Map.Height - 0.5f || r0.Y > w.Map.Height - 0.5f;
                    why = oobl ? 1 : (!w.Map.ClearLine(l0, r0, 0.3f) ? 2 : 0);
                }
                int inG = ms.Count(u => u.Group != null);
                var goals = ms.Where(u => u.Group != null).GroupBy(u => ((int)MathF.Round(u.Group!.Goal.X / 2), (int)MathF.Round(u.Group!.Goal.Y / 2))).Select(g => g.Count()).OrderByDescending(x => x).ToList();
                sb.Append($"{w.Tick / every},{c.Owner},{c.Id},{ms.Count},{cen.X:0.0},{cen.Y:0.0},{rms:0.00},{nnS / ms.Count:0.00},{ov / (double)ms.Count:0.00},{inG},{goals.Count},{(goals.Count > 0 ? goals[0] : 0)},{(c.Anchor != null ? 1 : 0)},{(int)c.Drill.Phase},{ms.Count(u => u.Order == UnitOrder.Idle)},{ms.Count(u => u.Order == UnitOrder.Move)},{ms.Count(u => u.Order == UnitOrder.AttackMove)},{ms.Count(u => u.Order == UnitOrder.Attack)},{ms.Count(u => u.Walking)},{revC.GetValueOrDefault(c.Id)},{newG.GetValueOrDefault(c.Id)},{c.Facing.X:0.00},{c.Facing.Y:0.00},{dS},{dA},{dG},{dC},{blk},{oob},{nearF},{(col?1:0)},{(arr?1:0)},{cov:0.00},{why}\n");
            }
            revC.Clear(); newG.Clear();
        }
        File.WriteAllText(outp, sb.ToString());
        if (dump != null) File.WriteAllText(dump, dsb.ToString());
        Console.WriteLine($"done tick {w.Tick}");
        return 0;
    }
}
