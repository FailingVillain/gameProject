using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// N75 사선 표시 (표시 전용 — 시뮬 상태 · 저장 · 체크섬 무관): B 를 누르는 동안 선택한 분대 (분대당 대표 1명, 최대 MaxOrigins) 가
/// 서 있는 곳에서 쏠 수 있는 칸 (초록) 과 숲 · 고지 · 건물에 막힌 칸 (붉은색) 을 보이는 화면 안에 그린다.
/// 판정은 시뮬과 같은 GameMap.HasFiringLine (깊은 숲 · 고지 · 건물 · 통행 불가 지형). 칸은 2x2 타일 표본, Interval 프레임마다만 다시 계산.
/// </summary>
public static class LosOverlay
{
    public const int Cell = 2, MaxOrigins = 24, Interval = 8;
    static int gx0, gy0, gw, gh, frame = -1000;
    static byte[] seen = Array.Empty<byte>();     // 0 = 사거리 밖, 1 = 사거리 안 막힘, 2 = 사거리 안 쏠 수 있음
    static readonly List<Vector2> origins = new();
    public static int OriginCount => origins.Count;
    public static bool CursorMode;

    public static void Reset() { frame = -1000; }

    /// <summary>units = 선택한 우리 유닛, cursor = 커서 기준일 때 커서 월드 좌표.</summary>
    public static void Update(World w, IReadOnlyList<Unit> units, Vector2 cursor, bool cursorMode, (Vector2 Min, Vector2 Max) view, int frameNo)
    {
        CursorMode = cursorMode;
        if (frameNo - frame < Interval && !cursorMode) return;
        if (cursorMode && frameNo - frame < 2) return;
        frame = frameNo;
        origins.Clear();
        float range = 0;
        var seenSq = new HashSet<Squad>();
        var cand = new List<Unit>();
        foreach (var u in units)
        {
            if (!u.Alive || u.Weapon == null || u.Weapon.Indirect || u.Weapon.Range <= 0) continue;
            range = MathF.Max(range, u.Weapon.Range);
            if (u.Squad is { } q && !seenSq.Add(q)) continue;
            cand.Add(u);
        }
        if (range <= 0) { seen = Array.Empty<byte>(); gw = gh = 0; return; }
        if (cursorMode) origins.Add(cursor);
        else
        {
            int step = Math.Max(1, (cand.Count + MaxOrigins - 1) / MaxOrigins);
            for (int i = 0; i < cand.Count; i += step) origins.Add(cand[i].Pos);
        }
        gx0 = (int)MathF.Floor(view.Min.X / Cell); gy0 = (int)MathF.Floor(view.Min.Y / Cell);
        gw = Math.Clamp((int)MathF.Ceiling(view.Max.X / Cell) - gx0 + 1, 0, 160);
        gh = Math.Clamp((int)MathF.Ceiling(view.Max.Y / Cell) - gy0 + 1, 0, 120);
        if (seen.Length < gw * gh) seen = new byte[gw * gh]; else Array.Clear(seen, 0, seen.Length);
        var map = w.Map;
        int rc = (int)MathF.Ceiling(range / Cell) + 1;
        foreach (var o in origins)
        {
            int ox = (int)MathF.Floor(o.X / Cell), oy = (int)MathF.Floor(o.Y / Cell);
            for (int cy = Math.Max(gy0, oy - rc); cy <= Math.Min(gy0 + gh - 1, oy + rc); cy++)
            for (int cx = Math.Max(gx0, ox - rc); cx <= Math.Min(gx0 + gw - 1, ox + rc); cx++)
            {
                var p = new Vector2(cx * Cell + Cell * 0.5f, cy * Cell + Cell * 0.5f);
                if (Vector2.DistanceSquared(p, o) > range * range) continue;
                int i = (cy - gy0) * gw + (cx - gx0);
                if (seen[i] == 2) continue;
                seen[i] = map.InBounds((int)p.X, (int)p.Y) && map.HasFiringLine(o, p, 0, 0) ? (byte)2 : (byte)Math.Max(1, (int)seen[i]);
            }
        }
    }

    /// <summary>월드 좌표 (2D 카메라 모드 안) 에서 호출.</summary>
    public static void Draw()
    {
        if (gw == 0 || gh == 0) return;
        float s = Cell * Config.TileSize;
        var good = new Color((byte)70, (byte)220, (byte)90, (byte)70);
        var bad = new Color((byte)230, (byte)60, (byte)50, (byte)70);
        for (int cy = 0; cy < gh; cy++)
        for (int cx = 0; cx < gw; cx++)
        {
            byte v = seen[cy * gw + cx];
            if (v == 0) continue;
            Raylib.DrawRectangleV(new Vector2((gx0 + cx) * s, (gy0 + cy) * s), new Vector2(s, s), v == 2 ? good : bad);
        }
        foreach (var o in origins) Raylib.DrawCircleLines((int)(o.X * Config.TileSize), (int)(o.Y * Config.TileSize), Config.TileSize * 0.8f, Color.White);
    }
}
