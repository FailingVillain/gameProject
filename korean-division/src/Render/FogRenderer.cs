using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 전장의 안개 그리기 (메인 뷰 + 미니맵). 타일 1개 = 1px RGBA 텍스처에 안개 상태를 올리고
/// 선형 필터로 확대해 덮는다 (경계가 한 타일 폭으로 부드럽게 번짐).
/// 텍스처는 FogOfWar.Version 이 바뀌었을 때만 다시 올리며, 창이 있어야 하므로 첫 그리기에 만든다.
/// </summary>
public sealed class FogRenderer : IDisposable
{
    static readonly Color Unexplored = new(8, 9, 12, 255);
    Color Explored = new(0, 0, 0, 150);
    static readonly Color Visible = new(0, 0, 0, 0);

    readonly World world;
    readonly int width, height;
    Texture2D tex;
    bool loaded;
    Color[] pixels = Array.Empty<Color>();
    int version = -1, faction = -1;
    byte[]? last;   // 마지막으로 올린 격자 (바뀐 줄 찾기용)

    public FogRenderer(World world)
    {
        this.world = world;
        width = world.Map.Width;
        height = world.Map.Height;
    }

    /// <summary>'탐색됨' 영역 투명도 (기본 150). 밤에는 Game 이 낮춰 시야 가장자리 밖 지형이 조금 더 보이게 한다 (화면 전용). 바뀌면 다시 올린다.</summary>
    public int ExploredAlpha
    {
        get => Explored.A;
        set { byte a = (byte)Math.Clamp(value, 0, 255); if (a == Explored.A) return; Explored = new Color(0, 0, 0, (int)a); version = -1; last = null; }
    }

    /// <summary>텍스처를 (필요하면 만들고) 현재 안개 상태로 맞춘다.</summary>
    void Sync(int me)
    {
        if (!loaded)
        {
            Image img = Raylib.GenImageColor(width, height, Visible);
            tex = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            Raylib.SetTextureWrap(tex, TextureWrap.Clamp);      // 가장자리에서 반대편이 번지지 않게
            pixels = new Color[width * height];
            loaded = true;
        }
        var fog = world.Fog;
        int v = fog.VersionOf(me);
        if (version == v && faction == me) return;
        version = v;
        if (faction != me) last = null;
        faction = me;

        var grid = fog.Grid(me);
        if (grid.Length != pixels.Length)
        {
            // 중립 등 격자 없는 세력: 전부 시야 안
            Array.Fill(pixels, Visible);
            Raylib.UpdateTexture(tex, pixels);
            last = null;
            return;
        }
        // 바뀐 줄만 다시 올린다 (변한 첫 줄 ~ 마지막 줄 구간만 텍스처 갱신)
        if (last == null || last.Length != grid.Length) { last = new byte[grid.Length]; Array.Fill(last, (byte)255); }
        int y0 = -1, y1 = -1;
        for (int y = 0; y < height; y++)
        {
            var row = grid.Slice(y * width, width);
            if (row.SequenceEqual(last.AsSpan(y * width, width))) continue;
            if (y0 < 0) y0 = y;
            y1 = y;
            for (int i = y * width, e = i + width; i < e; i++)
                pixels[i] = grid[i] switch { 2 => Visible, 1 => Explored, _ => Unexplored };
            row.CopyTo(last.AsSpan(y * width, width));
        }
        if (y0 >= 0)
            Raylib.UpdateTextureRec(tex, new Rectangle(0, y0, width, y1 - y0 + 1), new ReadOnlySpan<Color>(pixels, y0 * width, (y1 - y0 + 1) * width));
    }

    /// <summary>BeginMode2D 안에서, 맵 · 유닛 · 건물을 그린 뒤 호출.</summary>
    public void DrawWorld(RtsCamera cam, int me)
    {
        if (world.Fog.SeesAll(me)) return;
        Sync(me);
        const float ts = Config.TileSize;
        Raylib.DrawTexturePro(tex, new Rectangle(0, 0, width, height), new Rectangle(0, 0, width * ts, height * ts), Vector2.Zero, 0, Color.White);
    }

    /// <summary>미니맵 지형 위에 덮기 (화면 좌표 rect).</summary>
    public void DrawMinimap(Rectangle rect, int me)
    {
        if (world.Fog.SeesAll(me)) return;
        Sync(me);
        Raylib.DrawTexturePro(tex, new Rectangle(0, 0, width, height), rect, Vector2.Zero, 0, Color.White);
    }

    public void Dispose()
    {
        if (loaded) Raylib.UnloadTexture(tex);
        loaded = false;
    }
}
