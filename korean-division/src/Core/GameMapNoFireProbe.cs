using System.Numerics;
namespace KoreanDivision.Core;

public sealed partial class GameMap
{
    /// <summary>[진단 프로브, 읽기 전용] HasFiringLine 이 막힌 이유 (HasFiringLine 과 같은 순회): 통행 불가 지형 / 건물 점유 / 평지→고지 너머 / 깊은 숲 / 열림.</summary>
    internal string FiringLineBlockerForProbe(Entity s, Entity t) => FiringLineBlocker(s.Pos, t.Pos, s.Id, t.Id);
    internal string FiringLineBlocker(Vector2 a, Vector2 b, int ignoreA, int ignoreB)
    {
        EnsureTerrain();
        int seeDepth = Defs.Current.Terrain.ForestSeeDepth;
        var (x, y) = TileOf(a);
        var (ex, ey) = TileOf(b);
        string Why(int cx, int cy)
        {
            if (!InBounds(cx, cy)) return "맵 밖";
            int i = cy * Width + cx;
            if (!walkable[i]) return "통행 불가 지형 " + tiles[i];
            return "건물 점유 칸";
        }
        if (!ClearFor(x, y, ignoreA, ignoreB)) return Why(x, y) + " (출발 칸)";
        bool shooterLow = !high[y * Width + x];
        bool passedHigh = false;
        Vector2 d = b - a;
        int stepX = Math.Sign(d.X), stepY = Math.Sign(d.Y);
        float tDeltaX = d.X != 0 ? MathF.Abs(1f / d.X) : float.MaxValue;
        float tDeltaY = d.Y != 0 ? MathF.Abs(1f / d.Y) : float.MaxValue;
        float tMaxX = d.X > 0 ? (x + 1 - a.X) * tDeltaX : d.X < 0 ? (a.X - x) * tDeltaX : float.MaxValue;
        float tMaxY = d.Y > 0 ? (y + 1 - a.Y) * tDeltaY : d.Y < 0 ? (a.Y - y) * tDeltaY : float.MaxValue;
        int guard = Math.Abs(ex - x) + Math.Abs(ey - y) + 2;
        while ((x != ex || y != ey) && guard-- > 0)
        {
            if (tMaxX < tMaxY) { x += stepX; tMaxX += tDeltaX; }
            else if (tMaxY < tMaxX) { y += stepY; tMaxY += tDeltaY; }
            else
            {
                if (!ClearFor(x + stepX, y, ignoreA, ignoreB)) return Why(x + stepX, y) + " (대각 모서리)";
                if (!ClearFor(x, y + stepY, ignoreA, ignoreB)) return Why(x, y + stepY) + " (대각 모서리)";
                x += stepX; y += stepY; tMaxX += tDeltaX; tMaxY += tDeltaY;
            }
            if (!ClearFor(x, y, ignoreA, ignoreB)) return Why(x, y);
            if (passedHigh) return "평지 사수: 고지 너머";
            int i = y * Width + x;
            if ((x != ex || y != ey) && forestDepth[i] > seeDepth) return "깊은 숲 (사이 칸)";
            if (shooterLow && high[i]) passedHigh = true;
        }
        return "열림";
    }
}
