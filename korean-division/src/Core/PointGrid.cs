using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// PR5: 점 집합 공간 해시 (칸 크기 = 질의 반경 이상). 후보 집합을 좁히는 용도일 뿐 - 거리 비교식은 호출한 쪽의 원래 식을 그대로 쓴다.
/// 지도 밖 점 · 질의는 가장자리 칸으로 눌러 담는다 (후보가 늘 뿐 빠지지는 않음). 순회 순서는 판정(any)에 영향을 주지 않는다.
/// </summary>
internal sealed class PointGrid
{
    readonly float cs;
    readonly int gw, gh;
    readonly int[] head;
    readonly List<int> next = new();
    public readonly List<Vector2> Pos = new();

    public PointGrid(int mapW, int mapH, float radius)
    {
        cs = MathF.Max(radius, 1f) + 0.5f;   // 반올림 오차 여유
        gw = (int)(mapW / cs) + 1; gh = (int)(mapH / cs) + 1;
        head = new int[gw * gh];
        Array.Fill(head, -1);
    }

    int Cx(float x) { int c = (int)MathF.Floor(x / cs); return c < 0 ? 0 : c >= gw ? gw - 1 : c; }
    int Cy(float y) { int c = (int)MathF.Floor(y / cs); return c < 0 ? 0 : c >= gh ? gh - 1 : c; }

    public int Count => Pos.Count;

    public void Add(float x, float y)
    {
        int cell = Cy(y) * gw + Cx(x);
        next.Add(head[cell]);
        head[cell] = Pos.Count;
        Pos.Add(new Vector2(x, y));
    }

    /// <summary>(x, y) 둘레 3x3 칸의 점 번호 사슬 순회: 칸 범위를 구한다.</summary>
    public void Range(float x, float y, out int x0, out int y0, out int x1, out int y1)
    {
        int cx = Cx(x), cy = Cy(y);
        x0 = Math.Max(0, cx - 1); x1 = Math.Min(gw - 1, cx + 1);
        y0 = Math.Max(0, cy - 1); y1 = Math.Min(gh - 1, cy + 1);
    }

    public int Head(int cx, int cy) => head[cy * gw + cx];
    public int Next(int i) => next[i];
}
