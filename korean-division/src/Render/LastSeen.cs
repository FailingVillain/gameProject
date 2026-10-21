using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// "마지막 목격 위치" 표시 (ROADMAP '지형 · 높이'): 보이던 적 유닛이 시야에서 사라지면 그 자리에 흐린 표식을 남긴다.
/// 화면 전용 — 시뮬 · AI 는 쓰지 않는다 (AI 는 FogOfWar.CanSee 만 본다). 내 안개 격자가 바뀔 때 (Fog.VersionOf) 만 다시 계산.
/// 같은 유닛이 다시 보이거나 죽거나 MaxAge 초가 지나면 지운다.
/// </summary>
public static class LastSeen
{
    const double MaxAge = 45;
    readonly record struct Mark(Vector2 Pos, int Owner, double Time);

    static readonly Dictionary<int, Mark> marks = new();
    static Dictionary<int, (Vector2 Pos, int Owner)> seen = new(), next = new();
    static readonly List<int> drop = new();
    static World? lastWorld;
    static int lastMe = -1, lastVersion = -1;

    public static int Count => marks.Count;

    public static void Update(World w, int me)
    {
        var fog = w.Fog;
        if (!ReferenceEquals(w, lastWorld) || me != lastMe) { marks.Clear(); seen.Clear(); lastWorld = w; lastMe = me; lastVersion = -1; }
        if (fog.VersionOf(me) == lastVersion) return;
        lastVersion = fog.VersionOf(me);
        if (fog.SeesAll(me)) { marks.Clear(); seen.Clear(); return; }
        double now = Raylib.GetTime();
        next.Clear();
        foreach (var u in w.Units)
        {
            if (!u.Alive || u.Owner <= 0 || u.Owner == me || w.Allied(me, u.Owner)) continue;
            if (!fog.CanSee(me, u)) continue;
            next[u.Id] = (u.Pos, u.Owner);
            marks.Remove(u.Id);
        }
        foreach (var (id, s) in seen)
            if (!next.ContainsKey(id) && w.Find(id) is Unit { Alive: true }) marks[id] = new Mark(s.Pos, s.Owner, now);
        drop.Clear();
        foreach (var (id, m) in marks)
            if (now - m.Time > MaxAge || w.Find(id) is not Unit { Alive: true }) drop.Add(id);
        foreach (int id in drop) marks.Remove(id);
        (seen, next) = (next, seen);
    }

    /// <summary>BeginMode2D 안에서 (월드 좌표, 1 = 타일 TileSize px).</summary>
    public static void Draw()
    {
        if (marks.Count == 0) return;
        double now = Raylib.GetTime();
        float ts = Config.TileSize, r = ts * 0.28f;
        foreach (var m in marks.Values)
        {
            float age = (float)((now - m.Time) / MaxAge);
            int a = (int)(170 * (1f - age)) + 30;
            var c = Palette.Alpha(Palette.Player(m.Owner), a);
            var p = m.Pos * ts;
            Raylib.DrawLineEx(p - new Vector2(r, r), p + new Vector2(r, r), 2f, c);
            Raylib.DrawLineEx(p - new Vector2(r, -r), p + new Vector2(r, -r), 2f, c);
            Raylib.DrawCircleLinesV(p, r * 1.5f, c);
        }
    }
}
