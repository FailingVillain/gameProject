using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 시뮬레이션 불변식 검사 (AI 경기 도중 주기적으로 호출): 음수 재고 · NaN 위치 · 인구 집계 · 대기열 소유자 · 점령 시설 일관성 ·
/// 막힌 타일 안의 유닛 · 죽은 엔티티 잔존 · 오래 못 움직이는 이동 유닛. 같은 위반은 한 번만 기록한다.
/// </summary>
public sealed class Invariants
{
    readonly Dictionary<string, int> violations = new();
    readonly List<string> order = new();
    // 막힌 타일 안에 있는 연속 샘플 수 / 이동 명령인데 안 움직인 연속 샘플 수 (유닛 번호별)
    readonly Dictionary<int, int> inBlocked = new(), stuck = new();
    readonly Dictionary<int, Vector2> lastPos = new();
    public readonly Dictionary<string, int> StuckKinds = new();   // 20초 넘게 못 움직인 이동 유닛 (종류별 최대 동시 수 합산, 정보용)
    public int Samples;

    static readonly int TraceId = int.TryParse(Environment.GetEnvironmentVariable("KD_NAVTRACE")?.Split(':')[0], out var ti) ? ti : -1;
    static readonly long TraceFrom = long.TryParse(Environment.GetEnvironmentVariable("KD_NAVTRACE")?.Split(':').ElementAtOrDefault(1), out var tf) ? tf : 0;
    static string TraceFile => TestTemp.Child("wgnavtrace.txt");
    /// <summary>도달 불가 목표: 가장 가까운 도달 가능 지점에 이미 와 있으면 (AI 가 같은 목표를 계속 다시 내려도) 정체가 아니다.</summary>
    static bool AtApproxGoal(Unit u) => u.NavRoute is { Valid: true, Pending: false, Approx: true } r && Vector2.DistanceSquared(u.Pos, new Vector2(r.GoalX + 0.5f, r.GoalY + 0.5f)) < 1.2f * 1.2f;

    static string NavDiag(World w, Unit u)
    {
        var g = u.Group; var nr = u.NavRoute;
        var near = string.Join(";", w.Units.Where(o => o != u && o.Alive && Vector2.Distance(o.Pos, u.Pos) < 2.5f).Select(o => $"{o.Def.Key}#{o.Id}/o{o.Owner}/{o.Order}/imm{o.Immovable}/st{o.StuckLevel}/pnb{(o.Prone ? 1 : 0)}{(o.Pinned ? 1 : 0)}{(o.Broken ? 1 : 0)}/s{o.Suppression}/{o.Pos.X:F2},{o.Pos.Y:F2}/r{o.Radius}"));
        var (mx, my) = GameMap.TileOf(u.Pos); var lg = w.Nav.Large; lg.Sync();
        var sb = new System.Text.StringBuilder();
        for (int y = my - 4; y <= my + 4; y++) { for (int x = mx - 8; x <= mx + 8; x++) sb.Append(!w.Map.InBounds(x, y) ? ' ' : x == mx && y == my ? 'T' : (int)g!.Goal.X == x && (int)g.Goal.Y == y ? 'G' : !w.Map.IsWalkable(x, y) ? (w.Map.OccupantAt(x, y) is int oc && oc != 0 ? 'B' : '#') : w.Map.OccupantAt(x, y) is int oc2 && oc2 != 0 ? (lg.Walk(x, y) ? 'c' : 'k') : lg.Walk(x, y) ? '.' : ','); sb.Append('/'); }
        return $" map {sb}" + $" [walkOn {w.Map.IsWalkOnOccupied(mx, my)} lgWalk {lg.Walk(mx, my)} vel {u.Velocity} nd {u.NudgeDir} order {u.Order} arrR {g?.ArriveRadius} stuckLvl {u.StuckLevel} nudge {u.NudgeUntil - w.Tick} navKind {u.NavKind} navG ({u.NavGX},{u.NavGY}) raw ({u.NavRawX},{u.NavRawY}) route {(nr == null ? "null" : $"v{nr.Valid} p{nr.Pending} f{nr.Failed} a{nr.Approx} g({nr.GoalX},{nr.GoalY})")} form {g?.Plan != null} los {u.LosOk} near {near}]";
    }

    void Fail(string key, string detail)
    {
        if (violations.TryGetValue(key, out int n)) { violations[key] = n + 1; return; }
        violations[key] = 1;
        order.Add($"{key}: {detail}");
    }

    public IReadOnlyList<string> Violations => order;

    /// <summary>한 번 검사 (샘플 간격 = 1초 권장).</summary>
    public void Sample(World w)
    {
        Samples++;
        var map = w.Map;
        int nf = w.Factions.Count;
        string at = $"틱 {w.Tick}";

        // ---- 세력
        for (int f = 1; f < nf; f++)
        {
            var fa = w.Faction(f);
            if (fa.Defeated)
            {
                
                if (w.Buildings.Any(b => b.Owner == f && (!b.Def.Capturable || b.Def.City))) Fail("패배 세력에 건물 잔존", $"{at} 세력 {f}");
            }
        }

        // ---- 유닛
        var seen = new HashSet<int>();
        var stuckNow = new Dictionary<string, int>();
        foreach (var u in w.Units)
        {
            seen.Add(u.Id);
            if (!u.Alive)
            {
                // 멸망 처리(UpdateVictory)는 RemoveDead 뒤에 Hp=0 을 주므로 그 틱에는 남아 있을 수 있다
                if (!(u.Owner > 0 && u.Owner < nf && w.Faction(u.Owner).Defeated)) Fail("죽은 유닛이 목록에 남음", $"{at} {u.Def.Key}#{u.Id}");
                continue;
            }
            if (u.Owner < 0 || u.Owner >= nf) Fail("유닛 소유자 범위", $"{at} {u.Def.Key}#{u.Id} owner {u.Owner}");
            if (!float.IsFinite(u.Pos.X) || !float.IsFinite(u.Pos.Y) || !float.IsFinite(u.Velocity.X) || !float.IsFinite(u.Velocity.Y) || !float.IsFinite(u.Hp) || !float.IsFinite(u.Facing))
                Fail("유닛 NaN/Inf", $"{at} {u.Def.Key}#{u.Id} pos {u.Pos} hp {u.Hp}");
            if (u.Pos.X < 0 || u.Pos.Y < 0 || u.Pos.X > map.Width || u.Pos.Y > map.Height) Fail("유닛이 맵 밖", $"{at} {u.Def.Key}#{u.Id} pos {u.Pos}");
            if (u.Hp > u.MaxHp + 0.01f) Fail("체력이 최대치 초과", $"{at} {u.Def.Key}#{u.Id} {u.Hp}/{u.MaxHp}");
            if (w.Find(u.Id) != u) Fail("Find(Id) 불일치", $"{at} {u.Def.Key}#{u.Id}");
            if (u.UsesAmmo && (u.Ammo < 0 || u.Ammo > u.AmmoMax)) Fail("탄약 범위", $"{at} {u.Def.Key}#{u.Id} {u.Ammo}/{u.AmmoMax}");
            if (u.Cargo < 0 || u.Cargo > u.Def.Cargo + 1e-3f) Fail("보급 마차 적재량 범위", $"{at} {u.Def.Key}#{u.Id} {u.Cargo}/{u.Def.Cargo}");
            if ((u.InTrench || u.HasTrenchSpot) && !u.Def.CanOccupyTrench) Fail("참호를 차지할 수 없는 유닛이 참호 자리를 맡음", $"{at} {u.Def.Key}#{u.Id}");
            if (u.InTrench && !u.HasTrenchSpot) Fail("참호 안인데 자리가 없음", $"{at} {u.Def.Key}#{u.Id}");
            if (u.Target != null && u.Target.Owner == u.Owner) Fail("자기 세력을 표적으로 삼음", $"{at} {u.Def.Key}#{u.Id} -> {u.Target.Id}");

            // 막힌 타일 안 (도시 발자국은 통행 가능)
            var (tx, ty) = GameMap.TileOf(u.Pos);
            bool blocked = map.InBounds(tx, ty) && !map.IsWalkable(tx, ty);
            if (blocked) { int c = inBlocked.GetValueOrDefault(u.Id) + 1; inBlocked[u.Id] = c; if (c == 10) Fail("유닛이 10초 넘게 막힌 타일 안", $"{at} {u.Def.Key}#{u.Id} 타일 ({tx},{ty}) 지형 {map.Get(tx, ty)} 점유 {map.OccupantAt(tx, ty)}"); }
            else inBlocked.Remove(u.Id);

            // 이동 명령인데 20초 동안 거의 안 움직임 (정보용 — 전차가 좁은 통로에서 막히는 것은 알려진 한계)
            if (TraceId == u.Id && w.Tick >= TraceFrom && w.Tick <= TraceFrom + 1800) { try { File.AppendAllText(TraceFile, $"t{w.Tick} {u.Pos} {u.Order} tgt{u.Target?.Id} grp{u.Group?.GetHashCode()} goal{u.Group?.Goal} {NavDiag(w, u)}" + "\n"); } catch { } }
            bool wantsMove = !AtApproxGoal(u) && u.Group != null && (u.Order is UnitOrder.Move or UnitOrder.AttackMove) && !u.Immovable && u.Target == null;
            if (wantsMove && lastPos.TryGetValue(u.Id, out var lp) && Vector2.DistanceSquared(lp, u.Pos) < 0.0025f)
            {
                int c = stuck.GetValueOrDefault(u.Id) + 1;
                stuck[u.Id] = c;
                if (c >= 20) stuckNow[u.Def.Key] = stuckNow.GetValueOrDefault(u.Def.Key) + 1;
                // 큰 유닛 정체: 사수(Hold)·공사 중인 아군 유닛이 길을 막고 있으면 (몸이 좁은 틈을 메움) 정상이므로 제외 · 중대 깃발이 서 있으면 (교전 중 대열 정지 · 낙오자 기다림, 2026-10-18) 슬롯에서 기다리는 것도 정상
                if (c >= 6 && u.Large && !(u.Company?.Anchor is { Arrived: false } fa && ReferenceEquals(fa.Group, u.Group) && fa.SpeedMul < 0.05f) && !w.Units.Any(o => o != u && o.Alive && o.Owner == u.Owner && o.Immovable && Vector2.DistanceSquared(o.Pos, u.Pos) < 2.2f * 2.2f)) Fail("전차(큰 유닛)가 5초 넘게 이동하지 못함", $"{at} {u.Def.Key}#{u.Id} 위치 {u.Pos} 목표 {u.Group?.Goal}" + NavDiag(w, u));
            }
            else stuck.Remove(u.Id);
            lastPos[u.Id] = u.Pos;
        }
        foreach (var (k, n) in stuckNow) StuckKinds[k] = Math.Max(StuckKinds.GetValueOrDefault(k), n);
        if (Samples % 30 == 0)
            foreach (var id in inBlocked.Keys.Where(i => !seen.Contains(i)).ToList()) { inBlocked.Remove(id); stuck.Remove(id); lastPos.Remove(id); }

        // ---- 건물
        foreach (var b in w.Buildings)
        {
            string name = $"{b.Def.Key}#{b.Id}";
            if (!b.Alive) { Fail("죽은 건물이 목록에 남음", $"{at} {name}"); continue; }
            if (b.Owner < 0 || b.Owner >= nf) Fail("건물 소유자 범위", $"{at} {name} owner {b.Owner}");
            if (!float.IsFinite(b.Hp) || b.Hp > b.MaxHp + 0.01f) Fail("건물 체력 이상", $"{at} {name} {b.Hp}/{b.MaxHp}");
            if (map.OccupantAt(b.TileX, b.TileY) != b.Id) Fail("건물 점유 타일 불일치", $"{at} {name}");
            if (w.Find(b.Id) != b) Fail("Find(Id) 불일치", $"{at} {name}");
            // 점령 시설
            if (b.Def.Capturable)
            {
                if (b.CaptureProgress < 0 || b.CaptureProgress > 1 || float.IsNaN(b.CaptureProgress)) Fail("점령 게이지 범위", $"{at} {name} {b.CaptureProgress}");
                if (b.Owner > 0 && w.Faction(b.Owner).Defeated) Fail("패배 세력이 점령 시설 소유", $"{at} {name} owner {b.Owner}");
                if (b.CaptureFaction < 0 || b.CaptureFaction >= nf) Fail("CaptureFaction 범위", $"{at} {name} {b.CaptureFaction}");
            }
        }
    }

    /// <summary>위반을 검사 항목으로 보고. 알려진 정보(못 움직이는 유닛)도 한 줄 출력.</summary>
    public void Finish(string label, Action<bool, string> check)
    {
        foreach (var line in order) Console.WriteLine($"  [위반] {label}: {line} (x{violations[line.Split(':')[0]]})");
        check(order.Count == 0, $"{label}: 불변식 {Samples}회 검사, 위반 {order.Count}종");
        if (StuckKinds.Count > 0)
            Console.WriteLine($"  [info] {label}: 20초 넘게 이동하지 못한 유닛(최대 동시): {string.Join(", ", StuckKinds.Select(kv => $"{kv.Key} {kv.Value}"))}");
    }
}
