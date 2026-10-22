using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>미니맵: 타일 1개 = 1px 텍스처를 영역에 맞춰 확대해 그린다.</summary>
public sealed class Minimap : IDisposable
{
    readonly GameMap map;
    readonly Texture2D tex;
    public Rectangle Rect { get; private set; }

    public Minimap(GameMap map)
    {
        this.map = map;
        Image img = Raylib.GenImageColor(map.Width, map.Height, Color.Black);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            { var tc = Terrains.Get(map.Get(x, y)).Color; if (map.IsHigh(x, y)) tc = new Color((byte)Math.Min(255, tc.R + 34), (byte)Math.Min(255, tc.G + 30), (byte)Math.Min(255, tc.B + 10), (byte)255); Raylib.ImageDrawPixel(ref img, x, y, tc); }
        tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
    }

    /// <summary>area 안에 맵 비율을 유지하며 가운데 정렬.</summary>
    public void Layout(Rectangle area)
    {
        float s = MathF.Min(area.Width / map.Width, area.Height / map.Height);
        float w = map.Width * s, h = map.Height * s;
        Rect = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
    }

    public bool Contains(Vector2 p) => Raylib.CheckCollisionPointRec(p, Rect);

    public Vector2 ScreenToWorld(Vector2 p) => new(
        Math.Clamp((p.X - Rect.X) / Rect.Width * map.Width, 0, map.Width),
        Math.Clamp((p.Y - Rect.Y) / Rect.Height * map.Height, 0, map.Height));

    public Vector2 WorldToScreen(Vector2 w) => new(Rect.X + w.X / map.Width * Rect.Width, Rect.Y + w.Y / map.Height * Rect.Height);

    Rectangle TileRect(int tx, int ty, int size)
    {
        Vector2 a = WorldToScreen(new Vector2(tx, ty)), b = WorldToScreen(new Vector2(tx + size, ty + size));
        return new Rectangle(a.X, a.Y, MathF.Max(2, b.X - a.X), MathF.Max(2, b.Y - a.Y));
    }

    /// <summary>점령 시설 표식: 종류별 색 (도시 = 금색 · 큰 칸, 마을 = 연두) + 소유 세력 색 중심. 분쟁 중이면 깜박임.</summary>
    static Color SiteColor(string key) => key switch
    {
        "metropolis" or "city" => Palette.Gold,
        "village" => new Color(150, 220, 120, 255),
        _ => Palette.Gold,
    };

    void DrawSite(Building b, Rectangle r)
    {
        bool city = b.Def.City;
        float min = city ? 6 : 4;
        if (r.Width < min) { r.X -= (min - r.Width) / 2; r.Width = min; }
        if (r.Height < min) { r.Y -= (min - r.Height) / 2; r.Height = min; }
        Color kind = SiteColor(b.Def.Key);
        Raylib.DrawRectangleRec(r, Palette.Alpha(Palette.Player(b.Owner), 235));
        float line = city ? 2 : 1;
        Raylib.DrawRectangleLinesEx(r, line, b.Selected ? Color.White : kind);
        float c = city ? 2.5f : 1.5f;
        var mid = new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2);
        if (b.Def.Key == "metropolis") Raylib.DrawRectangleV(mid - new Vector2(c), new Vector2(c * 2), kind);   // 대도시: 중앙 표식
        if (b.Contested && (int)(Raylib.GetTime() * 3) % 2 == 0)
            Raylib.DrawRectangleLinesEx(new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), 1.5f, Palette.Warning);
    }

    public void Draw(World world, RtsCamera cam, int me, FogRenderer fogRenderer, (Vector2 Pos, double Age)? ping)
    {
        Raylib.DrawTexturePro(tex, new Rectangle(0, 0, map.Width, map.Height), Rect, Vector2.Zero, 0, Color.White);
        fogRenderer.DrawMinimap(Rect, me);

        foreach (var g in world.Fog.Ghosts(me))
            Raylib.DrawRectangleRec(TileRect(g.TileX, g.TileY, g.Def.Size), Palette.Alpha(Palette.Player(g.Owner), 140));
        foreach (var b in world.Buildings)
        {
            if (!world.Fog.CanSee(me, b)) continue;
            var r = TileRect(b.TileX, b.TileY, b.Size);
            if (b.Def.Capturable) { DrawSite(b, r); continue; }
            Raylib.DrawRectangleRec(r, b.Selected ? Color.White : Palette.Player(b.Owner));
        }

        DrawUnits(world, me);

        var (min, max) = cam.VisibleWorld();
        Vector2 a = WorldToScreen(min), bb = WorldToScreen(max);
        Raylib.BeginScissorMode((int)Rect.X, (int)Rect.Y, (int)MathF.Ceiling(Rect.Width), (int)MathF.Ceiling(Rect.Height));
        Raylib.DrawRectangleLinesEx(new Rectangle(a.X, a.Y, bb.X - a.X, bb.Y - a.Y), 1.5f, Color.White);
        foreach (var (car, _) in KoreanDivision.UI.MissionText.FreightMarkers(world))   // T30c 화물열차 목표 화물 객차 (적 열차는 보일 때만)
            if (world.Allied(me, car.Owner) || world.Fog.CanSee(me, car))
                Raylib.DrawRing(WorldToScreen(car.Pos), 3, 4.5f, 0, 360, 16, Palette.Accent);
        if (ping is var (pos, age) && age < 3)
        {
            float t = (float)(age % 1.0);
            Raylib.DrawRing(WorldToScreen(pos), 4 + t * 14, 6 + t * 14, 0, 360, 24, Palette.Alpha(Palette.HpLow, (int)(255 * (1 - t))));
        }
        Raylib.EndScissorMode();
    }

    // 유닛 점: 미니맵 크기의 작은 RGBA 버퍼에 찍어 한 장으로 올린다 (수천 기도 그리기 호출 1번 · 20Hz 로만 갱신)
    Texture2D unitTex;
    Color[] unitPx = Array.Empty<Color>();
    int unitW, unitH;
    double unitStamp = -1;
    int unitSeat = -2;

    void DrawUnits(World world, int me)
    {
        int w = (int)MathF.Ceiling(Rect.Width), h = (int)MathF.Ceiling(Rect.Height);
        if (w <= 0 || h <= 0) return;
        double now = Raylib.GetTime();
        if (unitTex.Id == 0 || w != unitW || h != unitH)
        {
            if (unitTex.Id != 0) Raylib.UnloadTexture(unitTex);
            var img = Raylib.GenImageColor(w, h, new Color((byte)0, (byte)0, (byte)0, (byte)0));
            unitTex = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            Raylib.SetTextureFilter(unitTex, TextureFilter.Point);
            unitPx = new Color[w * h];
            unitW = w; unitH = h; unitStamp = -1;
        }
        if (now - unitStamp >= 0.05 || unitSeat != me)
        {
            unitStamp = now;
            unitSeat = me;
            Array.Clear(unitPx);
            int dot = Math.Max(2, (int)MathF.Round(Rect.Width / map.Width * 0.9f));
            float sx = Rect.Width / map.Width, sy = Rect.Height / map.Height;
            var fog = world.Fog;
            foreach (var u in world.Units)
            {
                if (!fog.CanSee(me, u)) continue;
                int x0 = (int)(u.Pos.X * sx) - dot / 2, y0 = (int)(u.Pos.Y * sy) - dot / 2;
                var c = u.Selected ? Color.White : Palette.Player(u.Owner);
                for (int y = Math.Max(0, y0); y < Math.Min(h, y0 + dot); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(w, x0 + dot); x++)
                    unitPx[y * w + x] = c;
            }
            Raylib.UpdateTexture(unitTex, unitPx);
        }
        Raylib.DrawTexturePro(unitTex, new Rectangle(0, 0, w, h), new Rectangle(Rect.X, Rect.Y, w, h), Vector2.Zero, 0, Color.White);
    }

    public void Dispose()
    {
        Raylib.UnloadTexture(tex);
        if (unitTex.Id != 0) Raylib.UnloadTexture(unitTex);
        unitTex = default;
    }
}
