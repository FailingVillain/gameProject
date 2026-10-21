using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// F6 엄폐 소품 그리기 (바위 · 나무). 그림 = assets/themes/kd1920/props/&lt;종류&gt;/ (v1, tools/import_props.py), LOD 3단 (@128 / @64 / @32, 줌으로 고름),
/// 소품별 부드러운 바닥 그림자 따로. 발밑 기준점 = 소품 위치. UnitRenderer 가 유닛과 y 순으로 섞어 그린다 (Collect → DrawShadows → DrawBody).
/// 렌더 전용 - 시뮬 상태를 읽기만 한다.
/// </summary>
public static class PropRenderer
{
    const float TS = Config.TileSize;
    static readonly int[] TilePx = { 128, 64, 32 };
    static readonly string[] Suffix = { "", "@64", "@32" };

    sealed class Art
    {
        public readonly Texture2D[] Body = new Texture2D[3], Shadow = new Texture2D[3];
        public readonly Vector2[] Anchor = new Vector2[3];
        public bool Ok;
    }

    static readonly Dictionary<string, Art> cache = new();
    static (float Y, Prop P)[] vis = new (float, Prop)[256];
    static int count;

    static Art Load(string type)
    {
        if (cache.TryGetValue(type, out var a)) return a;
        a = new Art();
        cache[type] = a;
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "assets", "themes", "kd1920", "props", type);
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, type + "_layout.json")));
            var lods = doc.RootElement.GetProperty("lods");
            for (int i = 0; i < 3; i++)
            {
                var l = lods.GetProperty(i == 0 ? "@128" : Suffix[i]);
                var an = l.GetProperty("anchor");
                a.Anchor[i] = new Vector2(an[0].GetSingle(), an[1].GetSingle());
                a.Body[i] = Raylib.LoadTexture(Path.Combine(dir, type + Suffix[i] + ".png"));
                a.Shadow[i] = Raylib.LoadTexture(Path.Combine(dir, type + "_shadow" + Suffix[i] + ".png"));
                Raylib.SetTextureFilter(a.Body[i], TextureFilter.Point);
                Raylib.SetTextureFilter(a.Shadow[i], TextureFilter.Bilinear);
            }
            a.Ok = a.Body[0].Id != 0;
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { a.Ok = false; }
        return a;
    }

    static int Lod(float zoom) => zoom >= MapRenderer.Lod128Zoom ? 0 : zoom >= MapRenderer.Lod64Zoom ? 1 : 2;

    /// <summary>보이는 소품 (타일 좌표 min~max 둘레 여유 포함) 을 y 순으로 모은다. 돌려주는 값 = 개수.</summary>
    public static int Collect(World world, Vector2 min, Vector2 max)
    {
        count = 0;
        var items = world.Props.Items;
        if (items.Count == 0) return 0;
        const float M = 5f;   // 쓰러진 큰 나무 (폭 4.5칸) 여유
        for (int i = 0; i < items.Count; i++)
        {
            var p = items[i];
            if (p.X < min.X - M || p.X > max.X + M || p.Y < min.Y - M || p.Y > max.Y + M) continue;
            if (count == vis.Length) Array.Resize(ref vis, count * 2);
            vis[count++] = (p.Y * TS, p);
        }
        Array.Sort(vis, 0, count, Comparer<(float Y, Prop P)>.Create((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.P.Id.CompareTo(b.P.Id)));
        return count;
    }

    public static float YAt(int i) => vis[i].Y;

    public static void DrawShadows(float zoom)
    {
        int lod = Lod(zoom);
        for (int i = 0; i < count; i++)
        {
            var p = vis[i].P;
            var a = Load(p.Type);
            if (!a.Ok) continue;
            Blit(a.Shadow[lod], a.Anchor[lod], p, lod);
        }
    }

    public static void DrawBody(int i, float zoom)
    {
        var p = vis[i].P;
        var a = Load(p.Type);
        int lod = Lod(zoom);
        if (!a.Ok)
        {
            Raylib.DrawEllipse((int)(p.X * TS), (int)(p.Y * TS), p.Def.Radius * TS, p.Def.Radius * TS * 0.6f, new Color(110, 110, 100, 255));
            return;
        }
        Blit(a.Body[lod], a.Anchor[lod], p, lod);
    }

    static void Blit(Texture2D tex, Vector2 anchor, Prop p, int lod)
    {
        float k = TS / TilePx[lod];
        var dest = new Rectangle(p.X * TS - anchor.X * k, p.Y * TS - anchor.Y * k, tex.Width * k, tex.Height * k);
        Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), dest, Vector2.Zero, 0, Color.White);
    }
}
