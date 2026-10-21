using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// 화면 전용 (시뮬 상태 · 저장 · 체크섬 무관):
///  · N74 명령 대기열 경로 — 선택한 유닛이 든 대기열의 무게중심 → 지금 다리 → 남은 다리 지점을 선 · 표식으로.
///  · N75 사선 막힘 사수 — 우리 직사 사수 중 표적이 없고, 세력이 보는 사거리 안 적이 있는데 전부 숲 · 고지 · 장애물에 사선이 막힌 사수 머리 위 표식
///    (docs/legacy/NOFIRE_DIAG_2026-10-19.md 원인 2 · 5 의 (a) 표시 개선). 판정 = GameMap.FiringLineBlock (HasFiringLine 과 같은 걸음, 읽기 전용).
/// </summary>
public static class OrderOverlay
{
    // ---------------------------------------------------------------- N74 대기열 경로

    public static void DrawQueues(World w, IReadOnlyList<Unit> sel, float zoom)
    {
        if (w.OrderQueues.Count == 0 || sel.Count == 0) return;
        var shown = new HashSet<int>();
        float th = MathF.Max(1f, 2f / zoom);
        float ts = Config.TileSize;
        foreach (var u in sel)
        {
            if (w.QueueOf(u) is not { } q || !shown.Add(q.Id)) continue;
            Vector2 c = Vector2.Zero; int n = 0;
            foreach (int id in q.Units) if (w.Find(id) is Unit { Alive: true } m) { c += m.Pos; n++; }
            if (n == 0) continue;
            Vector2 prev = c / n;
            var pts = new List<(Vector2 P, QueuedKind K)>();
            if (q.HasCurrent) pts.Add((LegPoint(w, q.Current), q.Current.Kind));
            foreach (var l in q.Legs) pts.Add((LegPoint(w, l), l.Kind));
            for (int i = 0; i < pts.Count; i++)
            {
                var (p, k) = pts[i];
                var col = KindColor(k);
                bool cur = q.HasCurrent && i == 0;
                Raylib.DrawLineEx(prev * ts, p * ts, th, Palette.Alpha(col, cur ? 110 : 190));
                if (k == QueuedKind.Hold) Raylib.DrawRectangleLinesEx(new Rectangle(p.X * ts - 6 / zoom, p.Y * ts - 6 / zoom, 12 / zoom, 12 / zoom), th, col);
                else Raylib.DrawCircleLines((int)(p.X * ts), (int)(p.Y * ts), 6f / zoom, col);
                prev = p;
            }
        }
    }

    static Vector2 LegPoint(World w, in QueuedLeg l) =>
        l.Kind is QueuedKind.Attack or QueuedKind.Charge && l.TargetId != 0 && w.Find(l.TargetId) is Entity { Alive: true } t ? t.Pos : l.Target;

    static Color KindColor(QueuedKind k) => k switch
    {
        QueuedKind.Attack or QueuedKind.AttackMove or QueuedKind.Charge => Palette.HpLow,
        QueuedKind.Hold => Palette.Accent,
        _ => Palette.MarkerMove,
    };

    // ---------------------------------------------------------------- N75 사선 막힘 사수

    public const int Interval = 10, MaxShooters = 1500, Bucket = 8;
    static int frame = -1000;
    static readonly List<(int Id, LosBlock Why)> blocked = new();
    static readonly Dictionary<long, List<Unit>> grid = new();
    public static int BlockedCount => blocked.Count;
    public static LosBlock BlockedWhy(int unitId) { foreach (var b in blocked) if (b.Id == unitId) return b.Why; return LosBlock.None; }

    /// <summary>[읽기 전용] 이 사수가 지금 '보이는 적이 사거리 안에 있으나 전부 사선이 막힘' 이면 가장 가까운 적을 막은 것 (아니면 None). 테스트 · 표시 공용.</summary>
    public static LosBlock Classify(World w, Unit u, int me, Func<Vector2, float, IEnumerable<Unit>> near)
    {
        if (!u.Alive || u.Owner != me || u.Weapon is not { } wd || !wd.Los || wd.Indirect || wd.Range <= 0 || u.Target != null) return LosBlock.None;
        float best = float.MaxValue; LosBlock why = LosBlock.None;
        foreach (var e in near(u.Pos, wd.Range))
        {
            if (!e.Alive || !w.IsHostile(me, e.Owner) || !w.Fog.CanSee(me, e)) continue;
            float d = Vector2.Distance(u.Pos, e.Pos);
            if (d > wd.Range) continue;
            var b = w.Map.FiringLineBlock(u.Pos, e.Pos, u.Id, e.Id);
            if (b == LosBlock.None) return LosBlock.None;   // 한 명이라도 트였으면 막힘 아님 (사격 대기 · 반응 거리 등 다른 이유)
            if (d < best) { best = d; why = b; }
        }
        return why;
    }

    public static void UpdateBlocked(World w, int me, (Vector2 Min, Vector2 Max) view, int frameNo)
    {
        if (frameNo - frame < Interval) return;
        frame = frameNo;
        blocked.Clear();
        grid.Clear();
        foreach (var e in w.Units)
        {
            if (!e.Alive || e.Owner == me) continue;
            long k = Key((int)MathF.Floor(e.Pos.X / Bucket), (int)MathF.Floor(e.Pos.Y / Bucket));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<Unit>();
            l.Add(e);
        }
        if (grid.Count == 0) return;
        int n = 0;
        foreach (var u in w.Units)
        {
            if (u.Owner != me || !u.Alive) continue;
            if (u.Pos.X < view.Min.X - 2 || u.Pos.Y < view.Min.Y - 2 || u.Pos.X > view.Max.X + 2 || u.Pos.Y > view.Max.Y + 2) continue;
            if (++n > MaxShooters) break;
            var why = Classify(w, u, me, Near);
            if (why != LosBlock.None) blocked.Add((u.Id, why));
        }
    }

    static long Key(int bx, int by) => ((long)bx << 32) ^ (uint)by;

    static IEnumerable<Unit> Near(Vector2 p, float r)
    {
        int x0 = (int)MathF.Floor((p.X - r) / Bucket), x1 = (int)MathF.Floor((p.X + r) / Bucket);
        int y0 = (int)MathF.Floor((p.Y - r) / Bucket), y1 = (int)MathF.Floor((p.Y + r) / Bucket);
        for (int by = y0; by <= y1; by++)
        for (int bx = x0; bx <= x1; bx++)
            if (grid.TryGetValue(Key(bx, by), out var l)) foreach (var e in l) yield return e;
    }

    /// <summary>월드 좌표 (2D 카메라 모드 안) 에서 호출: 사수 머리 위 작은 표식 (숲 = 짙은 초록, 고지 = 갈색, 장애물 = 회색 — 붉은 사선 줄).</summary>
    public static void DrawBlocked(World w, float zoom)
    {
        if (blocked.Count == 0) return;
        float ts = Config.TileSize, r = MathF.Max(3f, 4.5f / MathF.Max(zoom, 0.3f));
        var slash = new Color((byte)230, (byte)50, (byte)40, (byte)230);
        foreach (var (id, why) in blocked)
        {
            if (w.Find(id) is not Unit { Alive: true } u) continue;
            var p = new Vector2(u.Pos.X * ts, u.Pos.Y * ts - ts * 0.9f);
            var fill = why switch { LosBlock.Forest => new Color((byte)40, (byte)110, (byte)50, (byte)230), LosBlock.High => new Color((byte)150, (byte)105, (byte)55, (byte)230), _ => new Color((byte)130, (byte)130, (byte)130, (byte)230) };
            Raylib.DrawCircleV(p, r, fill);
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, r, slash);
            Raylib.DrawLineEx(p + new Vector2(-r, -r) * 0.7f, p + new Vector2(r, r) * 0.7f, MathF.Max(1f, r * 0.3f), slash);
        }
    }
}
