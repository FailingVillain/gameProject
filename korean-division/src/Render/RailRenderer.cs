using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 철도 선로 (TerrainType.Rail) 덧그리기 — 렌더 전용. 지형 타일은 자갈 바닥만 굽고 (MapRenderer 절차 타일), 여기서 침목 · 레일을
/// 이웃 선로 칸 중심끼리 잇는 선분으로 그린다: 동 · 남 (4방향) + 남동 · 남서 (대각선, 두 직각 이웃이 모두 선로가 아닐 때만 — 계단 모양 삼각형 방지).
/// 선로는 1칸 폭 선으로 칠한다 (MapFile paint "line" · 1칸 폭 rect). 이웃 없는 외톨이 칸은 가로 토막.
/// </summary>
public static class RailRenderer
{
    const float TS = Config.TileSize;
    static readonly Color Sleeper = new(78, 58, 40, 255), SleeperDark = new(52, 40, 30, 255), RailTop = new(168, 168, 172, 255), RailSide = new(58, 58, 62, 255);

    /// <summary>선분 하나 (칸 중심 → 이웃 칸 중심, 타일 좌표).</summary>
    public readonly record struct Seg(Vector2 A, Vector2 B);

    static GameMap? cachedMap;
    static long cachedVer = -1;
    static List<(int X, int Y, Seg S)> cached = new();

    static bool Rail(GameMap m, int x, int y) => m.InBounds(x, y) && m.Get(x, y) == TerrainType.Rail;

    /// <summary>지도의 선로 선분 (위 줄 → 아래, 왼쪽 → 오른쪽 칸 순서; 지형 버전이 같으면 재사용).</summary>
    public static List<(int X, int Y, Seg S)> Segments(GameMap map)
    {
        if (ReferenceEquals(map, cachedMap) && map.TerrainVersion == cachedVer) return cached;
        var res = new List<(int, int, Seg)>();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            if (map.Get(x, y) != TerrainType.Rail) continue;
            var c = new Vector2(x + 0.5f, y + 0.5f);
            bool e = Rail(map, x + 1, y), s = Rail(map, x, y + 1), w = Rail(map, x - 1, y), n = Rail(map, x, y - 1);
            bool any = e || s || w || n;
            if (e) res.Add((x, y, new Seg(c, c + new Vector2(1, 0))));
            if (s) res.Add((x, y, new Seg(c, c + new Vector2(0, 1))));
            if (Rail(map, x + 1, y + 1) && !e && !s) { res.Add((x, y, new Seg(c, c + new Vector2(1, 1)))); any = true; }
            if (Rail(map, x - 1, y + 1) && !w && !s) { res.Add((x, y, new Seg(c, c + new Vector2(-1, 1)))); any = true; }
            any |= Rail(map, x - 1, y - 1) && !w && !n || Rail(map, x + 1, y - 1) && !e && !n;
            if (!any) res.Add((x, y, new Seg(c - new Vector2(0.5f, 0), c + new Vector2(0.5f, 0))));
        }
        cached = res; cachedMap = map; cachedVer = map.TerrainVersion;
        return res;
    }

    /// <summary>BeginMode2D 안, 지형 다음 · 건물 앞에 호출.</summary>
    public static void Draw(GameMap map, RtsCamera cam, Vector2 min, Vector2 max)
    {
        var segs = Segments(map);
        if (segs.Count == 0) return;
        bool detail = cam.Zoom >= 0.6f;
        float px = 1f / cam.Zoom;   // 화면 1px (월드 px)
        foreach (var (x, y, s) in segs)
        {
            if (x < min.X - 2 || x > max.X + 2 || y < min.Y - 2 || y > max.Y + 2) continue;
            Vector2 a = s.A * TS, b = s.B * TS, d = b - a;
            float len = d.Length();
            Vector2 u = d / len, nrm = new(-u.Y, u.X);
            if (!detail)
            {
                Raylib.DrawLineEx(a, b, MathF.Max(2.5f * px, TS * 0.22f), SleeperDark);
                continue;
            }
            // 침목: 선분 위 일정 간격 (칸의 1/4 폭), 레일 간격보다 조금 길게
            int nSl = Math.Max(1, (int)MathF.Round(len / (TS * 0.25f)));
            float half = TS * 0.36f, thick = MathF.Max(1.5f * px, TS * 0.09f);
            float ang = MathF.Atan2(nrm.Y, nrm.X) * 180f / MathF.PI;
            for (int i = 0; i < nSl; i++)
            {
                var p = a + d * ((i + 0.5f) / nSl);
                Raylib.DrawRectanglePro(new Rectangle(p.X, p.Y, half * 2f, thick), new Vector2(half, thick / 2f), ang, Sleeper);
            }
            // 레일 두 줄 (어두운 옆면 + 밝은 윗면). 이음매가 비지 않게 양 끝을 조금 늘린다.
            float g = TS * 0.2f, ext = TS * 0.04f;
            for (int k = -1; k <= 1; k += 2)
            {
                var o = nrm * (g * k);
                Vector2 ra = a + o - u * ext, rb = b + o + u * ext;
                Raylib.DrawLineEx(ra, rb, MathF.Max(1.5f * px, TS * 0.07f), RailSide);
                Raylib.DrawLineEx(ra - nrm * (TS * 0.012f), rb - nrm * (TS * 0.012f), MathF.Max(1f * px, TS * 0.035f), RailTop);
            }
        }
    }
}
