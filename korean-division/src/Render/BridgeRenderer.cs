using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 다리 (TerrainType.Bridge) 덧그리기 - 렌더 전용 (T39d). 지형 타일은 나무색 바닥만 굽고, 여기서 널빤지 줄과 물 쪽 난간을 그린다.
/// 널빤지 방향 2종: 다리 덩어리가 가로로 더 길면 (동서로 건넘) 널빤지는 세로 줄, 세로로 더 길면 가로 줄.
/// 같으면 물 이웃이 남북에 더 많을 때 세로 줄, 아니면 가로 줄. 난간은 물(깊은 물 · 여울) 쪽 변에만. 시뮬 영향 없음.
/// </summary>
public static class BridgeRenderer
{
    const float TS = Config.TileSize;
    static readonly Color Gap = new(72, 52, 34, 255), Plank = new(160, 124, 80, 255), Rail = new(96, 70, 44, 255);

    /// <summary>칸 하나의 그림 정보: 널빤지가 세로 줄인가, 난간이 있는 변 (북 · 동 · 남 · 서 비트).</summary>
    public readonly record struct Tile(int X, int Y, bool Vertical, int RailSides);

    static GameMap? cachedMap;
    static long cachedVer = -1;
    static List<Tile> cached = new();

    static bool Wet(GameMap m, int x, int y) => m.InBounds(x, y) && m.Get(x, y) is TerrainType.Water or TerrainType.Ford;

    /// <summary>(x, y) 를 지나는 한 축 방향 다리 칸 연속 길이 (양쪽 합, 자기 포함).</summary>
    static int Run(GameMap m, int x, int y, int dx, int dy)
    {
        int n = 1;
        for (int i = 1; m.InBounds(x + dx * i, y + dy * i) && m.Get(x + dx * i, y + dy * i) == TerrainType.Bridge; i++) n++;
        for (int i = 1; m.InBounds(x - dx * i, y - dy * i) && m.Get(x - dx * i, y - dy * i) == TerrainType.Bridge; i++) n++;
        return n;
    }

    /// <summary>지도의 다리 칸 목록 (지형 버전이 같으면 재사용).</summary>
    public static List<Tile> Tiles(GameMap map)
    {
        if (ReferenceEquals(map, cachedMap) && map.TerrainVersion == cachedVer) return cached;
        var res = new List<Tile>();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            if (map.Get(x, y) != TerrainType.Bridge) continue;
            bool n = Wet(map, x, y - 1), e = Wet(map, x + 1, y), s = Wet(map, x, y + 1), w = Wet(map, x - 1, y);
            int ns = (n ? 1 : 0) + (s ? 1 : 0), ew = (e ? 1 : 0) + (w ? 1 : 0);
            int rx = Run(map, x, y, 1, 0), ry = Run(map, x, y, 0, 1);
            bool vertical = rx != ry ? rx > ry : ns > ew;   // 가로로 더 긴 다리 = 동서로 건넘 = 세로 널빤지; 같으면 물 이웃으로
            int sides = (n ? 1 : 0) | (e ? 2 : 0) | (s ? 4 : 0) | (w ? 8 : 0);
            res.Add(new Tile(x, y, vertical, sides));
        }
        cached = res; cachedMap = map; cachedVer = map.TerrainVersion;
        return res;
    }

    /// <summary>BeginMode2D 안, 지형 다음 · 건물 앞에 호출.</summary>
    public static void Draw(GameMap map, RtsCamera cam, Vector2 min, Vector2 max)
    {
        var tiles = Tiles(map);
        if (tiles.Count == 0) return;
        bool detail = cam.Zoom >= 0.6f;
        float px = 1f / cam.Zoom;
        float t = MathF.Max(1.5f * px, TS * 0.09f);
        foreach (var b in tiles)
        {
            if (b.X < min.X - 2 || b.X > max.X + 2 || b.Y < min.Y - 2 || b.Y > max.Y + 2) continue;
            float x0 = b.X * TS, y0 = b.Y * TS;
            if (!detail) { Raylib.DrawRectangleRec(new Rectangle(x0, y0, TS, TS), Plank); continue; }
            // 널빤지: 칸을 4등분한 줄, 줄 사이 어두운 틈
            for (int k = 0; k < 4; k++)
            {
                float o = k * TS * 0.25f;
                var r = b.Vertical ? new Rectangle(x0 + o, y0, TS * 0.25f, TS) : new Rectangle(x0, y0 + o, TS, TS * 0.25f);
                Raylib.DrawRectangleRec(r, Gap);
                float g = MathF.Max(px, TS * 0.02f);
                Raylib.DrawRectangleRec(new Rectangle(r.X + (b.Vertical ? g : 0), r.Y + (b.Vertical ? 0 : g), r.Width - (b.Vertical ? 2 * g : 0), r.Height - (b.Vertical ? 0 : 2 * g)), Plank);
            }
            if ((b.RailSides & 1) != 0) Raylib.DrawRectangleRec(new Rectangle(x0, y0, TS, t), Rail);
            if ((b.RailSides & 2) != 0) Raylib.DrawRectangleRec(new Rectangle(x0 + TS - t, y0, t, TS), Rail);
            if ((b.RailSides & 4) != 0) Raylib.DrawRectangleRec(new Rectangle(x0, y0 + TS - t, TS, t), Rail);
            if ((b.RailSides & 8) != 0) Raylib.DrawRectangleRec(new Rectangle(x0, y0, t, TS), Rail);
        }
    }
}
