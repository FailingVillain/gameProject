using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 가옥 칸 (TerrainType.House) 을 건물 단위 사각형으로 묶는다 — 렌더 전용 (시뮬은 칸 단위 그대로: 통행 · 사선 · 시야 · 가옥 옆 엄폐).
/// 지도에서 가옥은 3×3 · 4×3 (민가) · 6×4 (큰 건물) 사각형으로 칠한다 (ROADMAP_LONG '건물 크기 확대'). 붙어 있는 가옥은 한 건물로 보이므로 1칸 이상 띄운다.
/// 탐욕 분해: 위 줄 → 아래, 왼쪽 → 오른쪽으로 아직 안 묶인 가옥 칸에서 오른쪽으로 늘리고 (≤ MaxSide), 그 폭 그대로 아래로 늘린다 (≤ MaxSide).
/// 세로로 긴 묶음 (높이 &gt; 폭) 은 그림이 가로로 넓은 3/4 시점이라 세로로 나눠 여러 채로 그린다 (<see cref="Pieces"/>).
/// </summary>
public static class HouseBlocks
{
    public const int MaxSide = 6;

    public readonly record struct Block(int X, int Y, int W, int H);

    static GameMap? cachedMap;
    static long cachedVer = -1;
    static List<Block> cached = new();

    /// <summary>지도의 가옥 묶음 (지형 버전이 같으면 이전 결과 재사용).</summary>
    public static IReadOnlyList<Block> Of(GameMap map)
    {
        if (!ReferenceEquals(map, cachedMap) || map.TerrainVersion != cachedVer)
        {
            cached = Build(map);
            cachedMap = map;
            cachedVer = map.TerrainVersion;
        }
        return cached;
    }

    public static List<Block> Build(GameMap map)
    {
        int W = map.Width, H = map.Height;
        var used = new bool[W * H];
        var res = new List<Block>();
        bool Free(int x, int y) => map.Get(x, y) == TerrainType.House && !used[y * W + x];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (!Free(x, y)) continue;
            int w = 1;
            while (w < MaxSide && x + w < W && Free(x + w, y)) w++;
            int h = 1;
            while (h < MaxSide && y + h < H)
            {
                bool row = true;
                for (int k = 0; k < w && row; k++) row = Free(x + k, y + h);
                if (!row) break;
                h++;
            }
            for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++) used[yy * W + xx] = true;
            res.Add(new Block(x, y, w, h));
        }
        return res;
    }

    /// <summary>그림 한 장씩 그릴 조각: 높이가 폭보다 크면 세로로 n 등분 (조각 높이 ≈ 폭 × 0.75), 아니면 그대로.</summary>
    public static IEnumerable<Block> Pieces(Block b)
    {
        if (b.H <= b.W) { yield return b; yield break; }
        int n = Math.Max(1, (int)MathF.Round(b.H / MathF.Max(1f, b.W * 0.75f)));
        int y = b.Y;
        for (int i = 0; i < n; i++)
        {
            int h = (b.Y + b.H - y) / (n - i);
            yield return new Block(b.X, y, b.W, h);
            y += h;
        }
    }
}
