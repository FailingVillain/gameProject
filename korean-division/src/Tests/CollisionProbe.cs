using System.Diagnostics;
using System.Numerics;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 대규모 전투 이동 · 겹침 측정 장면 (docs/COLLISION_ANALYSIS.md). 창 없음.
///   KoreanDivision --collide [scene=battle|amove|loose] [n=1500] [cav=100] [ticks=3600] [win=600] [seed=N]
/// battle = 양쪽 전술 AI · amove = 세력 1 중대 편성 후 적 중심으로 공격 이동 (세력 2 대기) · loose = amove 와 같되 중대 없이 진형 없는 공격 이동.
/// 측정 (구간마다): 걷는 유닛의 그림 방향(5방향 + 좌우 반전) 변화 · 좌우 반전 변화 · 걷기↔서기 깜빡임 · 진행 방향 역전 · 가속 흔들림,
/// 겹침 쌍 (깊음 = 중심 거리 &lt; 반경 합 × 0.6), 겹침 해소 밀림, 슬롯 배정 · 깃발 속도 바뀜 · 빈자리 재배치 · 정체, 길찾기 계산 수, 행진 사격 시작 · 종료, ms/틱.
/// </summary>
public static class CollisionProbe
{
    sealed class Track { public Vector2 P1, P2; public bool W1, Has1, Has2; public int Row = -1; public bool Flip; public bool March; }

    sealed class Win
    {
        public long MoveTicks, RowCh, FlipCh, WalkToggle, Reversal, MarchCh, Samples, Overlap, Deep, OverSamples;
        public double Accel, Speed, Ms;
        public int Ticks;
        // N12: 장교 위치 (중대 보병 앞선 대비, 적 무게중심 방향) · 정지 중 걷기 (대형 강박 — 도착 뒤 슬롯 맞추기 · 빈자리 재배치)
        public long OffN, OffAhead0, OffAhead2, IdleWalk, IdleSamples;
        public double OffLead, OffMax = double.MinValue;
    }

    static readonly Dictionary<string, long> OffWhy = new();

    static readonly Dictionary<string, long> StopWhy = new();
    static readonly Dictionary<string, long> RevWhy = new();

    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        string scene = "battle";
        int n = 1500, cav = 100, ticks = 3600, win = 600, seed = Config.DefaultSeed, dumpAt = -1;
        foreach (var a in args)
        {
            int eq = a.IndexOf('=');
            if (eq < 0) continue;
            string k = a[..eq].ToLowerInvariant(), v = a[(eq + 1)..];
            switch (k)
            {
                case "scene": scene = v.ToLowerInvariant(); break;
                case "n": n = int.Parse(v); break;
                case "cav": cav = int.Parse(v); break;
                case "ticks": ticks = int.Parse(v); break;
                case "win": win = int.Parse(v); break;
                case "seed": seed = int.Parse(v); break;
                case "dump": dumpAt = int.Parse(v); break;
            }
        }
        var defs = Defs.Load();
        foreach (var part in (Environment.GetEnvironmentVariable("KD_OBS_VARIANT") ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries)) TacticsObs.ApplyVariant(defs, part);   // formation: 같은 변형 (FMOFF · LWOFF · OLDFORM · CHOKE)
        int officers = Math.Max(1, (n - cav) / 50);
        int inf = n - cav - officers;
        string side = $"officer:{officers},infantry:{inf},cavalry_default:{cav}";
        string sideB = scene == "march" ? "infantry:1" : side;
        var setup = new BattleSetup { SideA = side, SideB = sideB, Map = "flat", Distance = scene == "march" ? 110 : 60, AiVsAi = true };
        var w = setup.CreateWorld(defs, seed);
        int warm = 0;
        if (scene == "battle")
        {
            for (int f = 1; f <= 2; f++) if (AiRegistry.Create(AiRegistry.Default) is IAiController ai) w.Controllers.Add((f, ai));
        }
        else
        {
            var mine = w.Units.Where(u => u.Owner == 1).ToList();
            var theirs = w.Units.Where(u => u.Owner == 2).ToList();
            Vector2 ec = Vector2.Zero; foreach (var u in theirs) ec += u.Pos; ec /= theirs.Count;
            Vector2 mc = Vector2.Zero; foreach (var u in mine) mc += u.Pos; mc /= mine.Count;
            if (scene is "amove" or "march" or "mfire")
            {
                var offs = mine.Where(u => u.Def.IsOfficer).OrderBy(u => u.Pos.Y).ThenBy(u => u.Id).ToList();
                var men = mine.Where(u => !u.Def.IsOfficer && !u.Def.Mounted).OrderBy(u => MathF.Round(u.Pos.Y)).ThenBy(u => u.Pos.X).ThenBy(u => u.Id).ToList();
                int per = (men.Count + offs.Count - 1) / offs.Count;
                var groups = new List<List<Unit>>();
                for (int i = 0; i < offs.Count; i++)
                {
                    var l = men.Skip(i * per).Take(per).ToList();
                    l.Add(offs[i]);
                    groups.Add(l);
                    w.FormCompany(1, l);
                }
                w.Step();
                var cavs = mine.Where(u => u.Def.Mounted).ToList();
                // 대대 (중대 7개) 4개: 앞줄 2 · 뒷줄 2 (뒷줄은 20칸 뒤), 대대 정면 65칸 (중대 ≈ 8열 × 7줄)
                int bats = (groups.Count + 6) / 7;
                void Order(Vector2 center, bool attack)
                {
                    for (int b = 0; b < bats; b++)
                    {
                        var l = groups.Skip(b * 7).Take(7).SelectMany(x => x).Where(u => u.Alive).ToList();
                        float y = center.Y + ((b % 2) - 0.5f) * 68f;
                        var p = new Vector2(center.X - (b / 2) * 20f, y);
                        var fs = new FormSpec((int)FormType.Line, 0, new Vector2(1, 0), 65f);
                        if (attack) w.AttackMove(1, l, p, fs); else w.Move(1, l, p, fs);
                    }
                    if (cavs.Count > 0)
                    {
                        var fc = new FormSpec((int)FormType.Line, 0, new Vector2(1, 0), 30);
                        var p = new Vector2(center.X - 40, center.Y);
                        if (attack) w.AttackMove(1, cavs, p, fc); else w.Move(1, cavs, p, fc);
                    }
                }
                Order(new Vector2(mc.X + 10, w.Map.Height / 2f), false);   // 정렬 (대형 짓기)
                for (int i = 0; i < 1500; i++) w.Step();
                warm = 1500;
                if (scene == "march") Order(new Vector2(mc.X + 80, w.Map.Height / 2f), false);
                else if (scene == "mfire") { w.Hold(2, theirs); Order(new Vector2(ec.X - 10, w.Map.Height / 2f), false); }   // 행진 사격: 서 있는 적에게 이동 명령으로 다가감
                else Order(new Vector2(ec.X + 10, w.Map.Height / 2f), true);
            }
            else w.AttackMove(1, mine, ec);   // loose: 진형 없는 공격 이동 (옛 방식 큰 그룹)
        }
        Console.WriteLine($"[collide] scene={scene} 세력당 {n} (장교 {officers} · 보병 {inf} · 기병 {cav}) ticks={ticks} seed={seed} (정렬 {warm}틱 뒤 측정)");
        Console.WriteLine(" 구간      | 걷는유닛 | 방향변화/s | 반전/s | 걷기깜빡/s | 역전/s | 가속/속도 | 겹침/유닛 | 깊은겹침 | 밀림유닛/틱 | 밀림평균 | 상한걸림 | 슬롯배정 | 깃발속도변화 | 빈자리 | 정체 | 경로 | 행진토글/s | ms/틱 | 생존 A/B");
        var tr = new Dictionary<int, Track>();
        var cur = new Win();
        var st0 = Snap(w);
        var nav0 = w.NavStats;
        var grid = new Dictionary<long, List<Unit>>();
        var total = new Win();
        for (int t = 1; t <= ticks; t++)
        {
            long t0 = Stopwatch.GetTimestamp();
            w.Step();
            cur.Ms += Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            cur.Ticks++;
            Observe(w, tr, cur);
            if (t % 10 == 0) { Overlaps(w, grid, cur); Officers(w, cur); }
            if (t == dumpAt) Dump(w);
            if (t % win == 0 || t == ticks)
            {
                var st1 = Snap(w);
                var nav1 = w.NavStats;
                Print($"{t - cur.Ticks,5}-{t,5}", cur, st0, st1, nav1.RoutesComputed - nav0.RoutesComputed + nav1.FieldsComputed - nav0.FieldsComputed, w);
                Add(total, cur);
                st0 = st1; nav0 = nav1; cur = new Win();
            }
            if (w.Winner != 0) break;
        }
        if (scene != "battle") Spread(w);
        Console.WriteLine("[officer-ahead] 앞선 1칸 넘게 앞선 장교 표본 원인: " + string.Join(" · ", OffWhy.OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine("[stop] 이동 명령 중 멈춤 원인: " + string.Join(" · ", StopWhy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine("[rev] 진행 방향 역전 유닛 상태: " + string.Join(" · ", RevWhy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
        return 0;
    }

    static float DumpFrac = float.TryParse(Environment.GetEnvironmentVariable("KD_DUMPFRAC"), System.Globalization.CultureInfo.InvariantCulture, out var df) ? df : 0.97f;
    static void Dump(World w)
    {
        var lag = new Dictionary<string, (double S, int N, int Far)>();
        foreach (var x in w.Units)
        {
            var a = x.Company?.Anchor; if (a == null || a.Arrived || !a.Slots.TryGetValue(x.Id, out var l)) continue;
            var q = a.Pos + new Vector2(-a.Heading.Y, a.Heading.X) * l.X + a.Heading * l.Y;
            float d = Vector2.Distance(q, x.Pos);
            var e = lag.GetValueOrDefault(x.Def.Key); lag[x.Def.Key] = (e.S + d, e.N + 1, e.Far + (d > 1.2f ? 1 : 0));
        }
        foreach (var (k, e) in lag) Console.WriteLine($"  [lag] {k}: 평균 {e.S / e.N:0.00} 칸 · 1.2칸 넘게 {e.Far}/{e.N}");
        var cat = new Dictionary<string, int>();
        foreach (var x in w.Units)
        {
            if (!x.Walking) continue;
            var a = x.Company?.Anchor;
            string key = $"{x.Def.Key} ord={x.Order} form={(a != null && ReferenceEquals(a.Group, x.Group) ? (a.Arrived ? "arr" : "march") : x.Group != null ? "grp" : "-")} tgt={(x.Target != null ? (x.AimTarget != 0 ? "aim" : "chase") : "-")} chg={x.ChargeState} stuck={(x.StuckLevel > 0)}";
            cat[key] = cat.GetValueOrDefault(key) + 1;
        }
        foreach (var (k, v) in cat.OrderByDescending(kv => kv.Value).Take(12)) Console.WriteLine($"  [walk] {v,5} {k}");
        int shown = 0;
        var us = w.Units.OrderBy(u => u.Id).ToList();
        foreach (var u in us)
        {
            foreach (var o in w.UnitsInRadius(u.Pos, 0.7f))
            {
                if (o.Id <= u.Id || shown >= 25) continue;
                float d = Vector2.Distance(u.Pos, o.Pos);
                if (d >= (u.Radius + o.Radius) * DumpFrac) continue;
                shown++;
                string S(Unit x) => $"#{x.Id} f{x.Owner} imm={x.Immovable} {x.Def.Key} co={x.Company?.Id} ord={x.Order} grp={(x.Group != null)} vel={x.Velocity.Length():0.00} stuck={x.StuckLevel} nudge={(x.NudgeUntil > w.Tick)} anc={(x.Company?.Anchor != null ? (x.Company.Anchor.Arrived ? "arr" : x.Company.Anchor.Column ? "col" : "line") : "-")} tgt={(x.Target != null)}";
                Vector2? Slot(Unit x) { var a = x.Company?.Anchor; if (a == null || !a.Slots.TryGetValue(x.Id, out var l)) return null; return a.Pos + new Vector2(-a.Heading.Y, a.Heading.X) * l.X + a.Heading * l.Y; }
                var su = Slot(u); var so = Slot(o);
                string sl = su is { } q1 && so is { } q2 ? $"slotD={Vector2.Distance(q1, q2):0.00} lagA={Vector2.Distance(q1, u.Pos):0.00} lagB={Vector2.Distance(q2, o.Pos):0.00} hdA={u.Company!.Anchor!.Heading} hdB={o.Company!.Anchor!.Heading} flagA={u.Company.Anchor.Pos} flagB={o.Company.Anchor.Pos} goalA={u.Company.Anchor.Goal} goalB={o.Company.Anchor.Goal} fdA={u.Company.Anchor.FinalDir} hw={u.Company.Anchor.HalfWidth:0.0}" : "";
                Console.WriteLine($"  [dump] d={d:0.000} {S(u)} | {S(o)} {sl}");
            }
            if (shown >= 25) break;
        }
    }

    static long[] Snap(World w)
    {
        var s = w.Stats;
        return new[] { s.SepPushUnits, (long)(s.SepPushSum * 1000), s.SepClamped, s.SlotAssign, s.FlagMulChange, s.GapRefills, s.StuckUps, s.CompanyMoves };
    }

    static void Add(Win a, Win b) { a.MoveTicks += b.MoveTicks; a.RowCh += b.RowCh; a.Ticks += b.Ticks; }

    // 렌더러 LodSprite.RowOf 와 같은 규칙 (5방향 + 서쪽 반전)
    static int RowOf(float facing, out bool flip)
    {
        float yaw = (90f - facing * (180f / MathF.PI)) % 360f;
        if (yaw < 0) yaw += 360f;
        flip = false;
        if (yaw > 180f) { yaw = 360f - yaw; flip = true; }
        return Math.Min((int)MathF.Round(yaw / 45f), 4);
    }

    static void Observe(World w, Dictionary<int, Track> tr, Win c)
    {
        foreach (var u in w.Units)
        {
            if (u.Def.IsArmored || u.Def.IsSupplyWagon) continue;
            if (!tr.TryGetValue(u.Id, out var k)) { tr[u.Id] = k = new Track(); }
            bool walk = u.Walking;
            int row = RowOf(u.Facing, out bool flip);
            bool march = u.MarchTarget != 0;
            if (k.Has1)
            {
                Vector2 d1 = u.Pos - k.P1;
                bool ordered = u.Order is UnitOrder.Move or UnitOrder.AttackMove && u.Group != null;
                if (walk && k.W1)
                {
                    c.MoveTicks++;
                    if (row != k.Row) c.RowCh++;
                    if (flip != k.Flip && row != 0 && row != 4) { }
                    if (flip != k.Flip) c.FlipCh++;
                    if (k.Has2)
                    {
                        Vector2 d0 = k.P1 - k.P2;
                        if (d0.LengthSquared() > 1e-5f && d1.LengthSquared() > 1e-5f && Vector2.Dot(d0, d1) < 0)
                        {
                            c.Reversal++;
                            // N33/N51 진단: 역전한 유닛의 상태 (접적 · 표적 · 정체 옆걸음 · 깃발 정지 · 명령)
                            var an = u.Company?.Anchor;
                            bool inFlag = an != null && ReferenceEquals(an.Group, u.Group);
                            string why = (u.Company != null && w.ContactOf(u.Company) != ContactState.None ? "contact/" : "free/")
                                + (u.NudgeUntil > w.Tick ? "nudge" : u.Target != null ? "target" : inFlag ? (an!.Arrived ? "flag-arrived" : an.SpeedMul < 0.05f ? "flag-stop" : "flag-walk") : u.Order is UnitOrder.Move or UnitOrder.AttackMove ? "order" : "idle");
                            RevWhy[why] = RevWhy.GetValueOrDefault(why) + 1;
                        }
                        c.Accel += (d1 - d0).Length();
                        c.Speed += d1.Length();
                    }
                }
                if (ordered && walk != k.W1)
                {
                    c.WalkToggle++;
                    if (!walk)
                    {
                        var an = u.Company?.Anchor;
                        string why = u.ReactDelay > 0 ? "react" : u.Target != null ? "engage" : an != null && ReferenceEquals(an.Group, u.Group) ? (an.Arrived ? "arrived-slot" : an.SpeedMul == 0 ? "flag-stop" : "at-slot") : u.HoldLeft > 0 ? "holdwait" : "other";
                        StopWhy[why] = StopWhy.GetValueOrDefault(why) + 1;
                    }
                }
                if (march != k.March) c.MarchCh++;
            }
            k.P2 = k.P1; k.Has2 = k.Has1;
            k.P1 = u.Pos; k.Has1 = true; k.W1 = walk; k.Row = row; k.Flip = flip; k.March = march;
        }
    }

    static void Overlaps(World w, Dictionary<long, List<Unit>> grid, Win c)
    {
        foreach (var l in grid.Values) l.Clear();
        foreach (var u in w.Units)
        {
            long key = ((long)(int)MathF.Floor(u.Pos.X) << 32) | (uint)(int)MathF.Floor(u.Pos.Y);
            if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<Unit>();
            l.Add(u);
        }
        foreach (var u in w.Units)
        {
            int cx = (int)MathF.Floor(u.Pos.X), cy = (int)MathF.Floor(u.Pos.Y);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                long key = ((long)(cx + dx) << 32) | (uint)(cy + dy);
                if (!grid.TryGetValue(key, out var l)) continue;
                foreach (var o in l)
                {
                    if (o.Id <= u.Id) continue;
                    float rr = u.Radius + o.Radius, d = Vector2.Distance(u.Pos, o.Pos);
                    if (d < rr - 0.02f) c.Overlap++;
                    if (d < rr * 0.6f) c.Deep++;
                }
            }
        }
        c.Samples += w.Units.Count;
        c.OverSamples++;
    }

    /// <summary>N12 측정: 중대 장교가 중대 보병 앞선 (적 무게중심 방향 투영 최대) 보다 얼마나 앞에 있나, 그리고 정지 상태 (대기 · 도착한 대형) 에서 걷는 병사 수.</summary>
    static void Officers(World w, Win c)
    {
        var sum = new Vector2[3]; var cnt = new int[3];
        foreach (var u in w.Units) if (u.Owner is 1 or 2) { sum[u.Owner] += u.Pos; cnt[u.Owner]++; }
        foreach (var co in w.Companies)
        {
            int e = co.Owner == 1 ? 2 : 1;
            if (co.Owner is not (1 or 2) || cnt[e] == 0 || co.Members.Count == 0) continue;
            Vector2 cen = Vector2.Zero; foreach (var u in co.Members) cen += u.Pos; cen /= co.Members.Count;
            var d = sum[e] / cnt[e] - cen;
            if (d.LengthSquared() < 1f) continue;
            d = Vector2.Normalize(d);
            float front = float.MinValue;
            foreach (var u in co.Members) if (u.Def.IsInfantry && !u.Def.Heavy && !u.IsOfficer && !u.Def.Scout) front = MathF.Max(front, Vector2.Dot(u.Pos, d));
            if (front == float.MinValue) continue;
            foreach (var o in co.Members)
            {
                if (!o.IsOfficer || !o.Alive) continue;
                float lead = Vector2.Dot(o.Pos, d) - front;
                c.OffN++; c.OffLead += lead; c.OffMax = Math.Max(c.OffMax, lead);
                if (lead > 0) c.OffAhead0++;
                if (lead > 2) c.OffAhead2++;
                if (lead > 1)
                {
                    var an = co.Anchor;
                    string key = $"ord={o.Order} chg={o.Charging} tgt={(o.Target != null)} form={(an != null && ReferenceEquals(an.Group, o.Group) ? (an.Arrived ? "arr" : "march") : o.Group != null ? "grp" : "-")} drill={co.Drill.Phase}";
                    OffWhy[key] = OffWhy.GetValueOrDefault(key) + 1;
                }
            }
        }
        foreach (var u in w.Units)
        {
            if (u.Def.IsArmored || u.Def.IsSupplyWagon || u.Target != null || u.Charging) continue;
            // 정지 상태 = 대기 (명령 · 그룹 없음) 또는 도착한 대형에서 최종 슬롯 1.5칸 안 (슬롯 맞추기 잔걸음)
            bool still = u.Order == UnitOrder.Idle && u.Group == null;
            if (!still && u.Company?.Anchor is { Arrived: true } an && ReferenceEquals(an.Group, u.Group) && an.FinalOff.TryGetValue(u.Id, out var fo))
                still = Vector2.Distance(an.Goal + fo, u.Pos) < 1.5f;
            if (!still) continue;
            c.IdleSamples++;
            if (u.Walking) c.IdleWalk++;
        }
    }

    static void Print(string label, Win c, long[] a, long[] b, long nav, World w)
    {
        double ms = Math.Max(1, c.MoveTicks) / 60.0;   // 걷는 유닛-초
        double ticks = Math.Max(1, c.Ticks);
        long push = b[0] - a[0];
        int alive1 = w.Units.Count(u => u.Owner == 1), alive2 = w.Units.Count(u => u.Owner == 2);
        double units = Math.Max(1.0, w.Units.Count);
        Console.WriteLine($" {label} | {c.MoveTicks / ticks,8:0} | {c.RowCh / ms,10:0.000} | {c.FlipCh / ms,6:0.000} | {c.WalkToggle / (units * ticks / 60),10:0.000} | {c.Reversal / ms,6:0.000} | {(c.Speed > 0 ? c.Accel / c.Speed : 0),9:0.000} | {(double)c.Overlap / Math.Max(1, c.Samples),9:0.000} | {(double)c.Deep / Math.Max(1, c.Samples),8:0.0000} | {push / ticks,11:0.0} | {(push > 0 ? (b[1] - a[1]) / 1000.0 / push : 0),8:0.0000} | {b[2] - a[2],8} | {b[3] - a[3],8} | {b[4] - a[4],12} | {b[5] - a[5],6} | {b[6] - a[6],4} | {nav,4} | {c.MarchCh / (units * ticks / 60),10:0.000} | {c.Ms / ticks,5:0.00} | {alive1}/{alive2}");
        if (c.OffN > 0 || c.IdleSamples > 0)
            Console.WriteLine($"   [n12] 장교 표본 {c.OffN} · 앞선 대비 평균 {(c.OffN > 0 ? c.OffLead / c.OffN : 0):+0.00;-0.00} 칸 · 최대 {(c.OffN > 0 ? c.OffMax : 0):+0.0;-0.0} · 앞선 넘음 {(c.OffN > 0 ? 100.0 * c.OffAhead0 / c.OffN : 0):0.0}% · 2칸 넘게 앞 {(c.OffN > 0 ? 100.0 * c.OffAhead2 / c.OffN : 0):0.0}% | 정지 상태 걷기 {(c.IdleSamples > 0 ? 100.0 * c.IdleWalk / c.IdleSamples : 0):0.00}% (표본 {c.IdleSamples})");
    }

    /// <summary>공격 이동 끝: 세력 1 의 퍼짐 — 1칸에 3명 이상 선 칸 수 · 최대 밀도 · 가로 폭 · 목표 3칸 안 인원.</summary>
    static void Spread(World w)
    {
        var mine = w.Units.Where(u => u.Owner == 1 && !u.Def.Mounted).ToList();
        if (mine.Count == 0) return;
        var cells = new Dictionary<(int, int), int>();
        foreach (var u in mine) { var key = ((int)MathF.Floor(u.Pos.X), (int)MathF.Floor(u.Pos.Y)); cells[key] = cells.GetValueOrDefault(key) + 1; }
        float y0 = mine.Min(u => u.Pos.Y), y1 = mine.Max(u => u.Pos.Y), x0 = mine.Min(u => u.Pos.X), x1 = mine.Max(u => u.Pos.X);
        int moving = mine.Count(u => u.Group != null), charging = mine.Count(u => u.Charging);
        Console.WriteLine($"[spread] 세력1 보병 {mine.Count}: 칸 최대 {cells.Values.Max()}명 · 3명 이상 칸 {cells.Values.Count(v => v >= 3)} · 폭(y) {y1 - y0:0.0} · 깊이(x) {x1 - x0:0.0} · 이동 중 {moving} · 돌격 중 {charging}");
    }
}
