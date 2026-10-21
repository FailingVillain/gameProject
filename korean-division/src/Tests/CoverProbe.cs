using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T28 측정 도구 (--t28probe [organic] [clump] [ppm <폴더>]): 생성 맵 시드 1~8 의 소품 최근접 이웃 거리 · 숲 면적 · 가장 긴 직선 변, 미션 지도의 숲 직선 변.
/// 플래그는 props.json 의 값을 인자로 덮어쓴다 (파일은 안 바꿈). ppm 폴더를 주면 시드 3 개 + 미션 지도 PPM 을 쓴다.
/// </summary>
public static class CoverProbe
{
    public static int Run(string[] a)
    {
        Defs.Load();
        var rules = PropDefs.Current.Rules;
        if (a.Contains("organic")) rules.OrganicForest = true;
        if (a.Contains("clump")) rules.Scatter.Clump = true;
        int pi = Array.IndexOf(a, "ppm");
        string? ppm = pi >= 0 && pi + 1 < a.Length ? a[pi + 1] : null;
        Console.WriteLine($"[T28] organicForest={rules.OrganicForest} clump={rules.Scatter.Clump}");
        double sumNn = 0, sumIso = 0; int sumProps = 0, sumForest = 0, worstRun = 0;
        foreach (int seed in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var m = GameMap.Generate(160, 160, seed);
            var (nn, iso) = Nn(m);
            int fa = Count(m), run = LongestRun(m);
            Console.WriteLine($"  seed {seed}: props {m.PropSeeds.Count} nn {nn:F2} isolated(nn>3) {iso:P0} forestTiles {fa} longestStraight {run}");
            sumNn += nn; sumIso += iso; sumProps += m.PropSeeds.Count; sumForest += fa; worstRun = Math.Max(worstRun, run);
            if (ppm != null && seed <= 3) Dump(m, Path.Combine(ppm, $"gen{seed}.ppm"));
        }
        Console.WriteLine($"  SUM: props {sumProps} nn(mean) {sumNn / 8:F3} isolated {sumIso / 8:P1} forestTiles {sumForest} worstStraight {worstRun}");
        foreach (var id in new[] { "c1_01_hoeryong", "bandit_raid" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "missions", id + ".json")), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var m = MapFile.LoadText(doc.RootElement.GetProperty("mapData").GetRawText(), "mission_" + id, doc.RootElement.TryGetProperty("seed", out var sd) ? sd.GetInt32() : 1);
            Console.WriteLine($"  mission {id}: forestTiles {Count(m)} longestStraight {LongestRun(m)}");
            if (ppm != null) Dump(m, Path.Combine(ppm, id + ".ppm"));
        }
        return 0;
    }

    static int Count(GameMap m) { int n = 0; for (int y = 0; y < m.Height; y++) for (int x = 0; x < m.Width; x++) if (m.Get(x, y) == TerrainType.Forest) n++; return n; }

    static bool F(GameMap m, int x, int y) => m.InBounds(x, y) && m.Get(x, y) == TerrainType.Forest;

    /// <summary>숲 경계 중 한 방향(상 · 하 · 좌 · 우)으로 노출된 칸이 일직선으로 이어진 최대 길이.</summary>
    static int LongestRun(GameMap m)
    {
        int best = 0;
        for (int dir = 0; dir < 4; dir++)
        {
            int dx = dir == 2 ? -1 : dir == 3 ? 1 : 0, dy = dir == 0 ? -1 : dir == 1 ? 1 : 0;
            bool horiz = dy != 0;   // 위 / 아래로 노출된 변 = 가로로 이어짐
            int outer = horiz ? m.Height : m.Width, inner = horiz ? m.Width : m.Height;
            for (int o = 0; o < outer; o++)
            {
                int run = 0;
                for (int i = 0; i < inner; i++)
                {
                    int x = horiz ? i : o, y = horiz ? o : i;
                    if (F(m, x, y) && !F(m, x + dx, y + dy)) { run++; best = Math.Max(best, run); } else run = 0;
                }
            }
        }
        return best;
    }

    static (double nn, double isolated) Nn(GameMap m)
    {
        var p = m.PropSeeds; if (p.Count < 2) return (0, 1);
        double sum = 0; int iso = 0;
        for (int i = 0; i < p.Count; i++)
        {
            double best = double.MaxValue;
            for (int j = 0; j < p.Count; j++) if (i != j) best = Math.Min(best, Math.Sqrt((p[i].X - p[j].X) * (p[i].X - p[j].X) + (p[i].Y - p[j].Y) * (p[i].Y - p[j].Y)));
            sum += best; if (best > 3) iso++;
        }
        return (sum / p.Count, iso / (double)p.Count);
    }

    static void Dump(GameMap m, string path)
    {
        int S = 5;   // 칸당 5px
        var buf = new byte[m.Width * S * m.Height * S * 3];
        void Px(int px, int py, byte r, byte g, byte b) { if (px < 0 || py < 0 || px >= m.Width * S || py >= m.Height * S) return; int o = (py * m.Width * S + px) * 3; buf[o] = r; buf[o + 1] = g; buf[o + 2] = b; }
        for (int y = 0; y < m.Height; y++)
        for (int x = 0; x < m.Width; x++)
        {
            var t = m.Get(x, y);
            (byte r, byte g, byte b) c = t switch
            {
                TerrainType.Forest => (30, 90, 40), TerrainType.Water => (50, 90, 170), TerrainType.Sand => (210, 195, 140), TerrainType.Rock => (110, 110, 110),
                TerrainType.Dirt => (140, 110, 70), TerrainType.House => (170, 60, 50), _ => (110, 160, 80),
            };
            if (m.IsHigh(x, y)) c = ((byte)Math.Min(255, c.r + 25), (byte)Math.Min(255, c.g + 25), (byte)Math.Min(255, c.b + 25));
            for (int j = 0; j < S; j++) for (int i = 0; i < S; i++) Px(x * S + i, y * S + j, c.r, c.g, c.b);
        }
        foreach (var p in m.PropSeeds)
        {
            bool tree = p.Type.StartsWith("tree"); (byte r, byte g, byte b) c = tree ? ((byte)10, (byte)50, (byte)15) : ((byte)240, (byte)240, (byte)240);
            int cx = (int)(p.X * S), cy = (int)(p.Y * S);
            for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) Px(cx + i, cy + j, c.r, c.g, c.b);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        var head = System.Text.Encoding.ASCII.GetBytes($"P6\n{m.Width * S} {m.Height * S}\n255\n");
        fs.Write(head); fs.Write(buf);
    }
}
