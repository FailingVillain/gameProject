using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 시뮬 중 지형 편집 (포탄 구덩이, GameMap.TerrainEditAt) 반영 + 참호 조각 (이웃 마스크).
///  · 편집된 칸 ±2 (슬롯이 바뀌는 8이웃 + 그 경계 섞기가 닿는 범위) 을 덮는 청크의 세대를 올린다 → 그 청크는 옛 그림을 쓰면서 다시 굽고, 다 구워지면 바꾼다.
///  · 참호 그림은 한 길: 슬롯 (SlotAt: 북 · 동 · 남 · 서 이웃 마스크 → 테마 trench_h/v/ne/nw/se/sw/end_*) 만 다시 계산한다.
/// </summary>
public sealed partial class MapRenderer
{
    int editCursor;
    Color[]? avgColors;

    void ApplyEdits()
    {
        int n = map.TerrainEditCount;
        if (editCursor >= n) return;
        for (; editCursor < n; editCursor++)
        {
            int i = map.TerrainEditAt(editCursor), x = i % W, y = i / W;
            ter[i] = (byte)map.Get(x, y);
            if (slot != null)
            {
                for (int k = 0; k < 9; k++)
                {
                    int nx = x + k % 3 - 1, ny = y + k / 3 - 1;
                    if ((uint)nx < (uint)W && (uint)ny < (uint)H) slot[ny * W + nx] = SlotAt(nx, ny);   // 숲 깊이 · 참호 이웃 마스크
                }
                if (avgColors != null && overview.Id != 0)
                {
                    int sl = slot[i];
                    Raylib.UpdateTextureRec(overview, new Rectangle(x, y, 1, 1), new[] { hi[i] && sl != SlotHigh ? HighTint(avgColors[sl]) : avgColors[sl] });
                }
            }
            for (int l = 0; l < 3; l++)
            {
                var L = lods[l];
                int cx0 = Math.Max(0, (x - 2) / L.CT), cy0 = Math.Max(0, (y - 2) / L.CT);
                int cx1 = Math.Min(L.CX - 1, (x + 2) / L.CT), cy1 = Math.Min(L.CY - 1, (y + 2) / L.CT);
                for (int cy = cy0; cy <= cy1; cy++)
                for (int cx = cx0; cx <= cx1; cx++) L.Gen[cy * L.CX + cx]++;
            }
        }
    }
}
