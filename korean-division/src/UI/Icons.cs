using System.Numerics;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>커맨드 버튼 아이콘 (벡터 도형). 이미지 아이콘으로 교체 시 이 파일만 바꾸면 된다.</summary>
public static class Icons
{
    public static void Draw(string key, Rectangle r, Color c)
    {
        Vector2 ctr = new(r.X + r.Width / 2, r.Y + r.Height / 2);
        float s = MathF.Min(r.Width, r.Height) / 2;
        switch (key)
        {
            case "move":
                Arrow(ctr, s, +1, c);
                break;
            case "firecontrol":   // 조준선 + 사선 (사격 통제)
                Raylib.DrawRing(ctr, s * 0.5f, s * 0.65f, 0, 360, 24, c);
                Raylib.DrawLineEx(ctr + new Vector2(-s * 0.9f, s * 0.9f), ctr + new Vector2(s * 0.9f, -s * 0.9f), s * 0.18f, c);
                break;
            case "stop":
                Raylib.DrawRectangleRec(new Rectangle(ctr.X - s * 0.6f, ctr.Y - s * 0.6f, s * 1.2f, s * 1.2f), c);
                break;
            case "hold":
                Raylib.DrawRectangleRec(new Rectangle(ctr.X - s * 0.7f, ctr.Y - s * 0.8f, s * 1.4f, s * 0.8f), c);
                Tri(new(ctr.X - s * 0.7f, ctr.Y), new(ctr.X + s * 0.7f, ctr.Y), new(ctr.X, ctr.Y + s * 0.9f), c);
                break;
            case "quickstep":   // 겹쳐진 두 화살표 (빠른 이동)
                Arrow(ctr - new Vector2(s * 0.45f, 0), s * 0.55f, +1, c);
                Arrow(ctr + new Vector2(s * 0.3f, 0), s * 0.55f, +1, c);
                break;
            case "marchfire":   // 앞으로 가는 화살표 + 탄흔 점
                Arrow(ctr - new Vector2(s * 0.2f, 0), s * 0.6f, +1, c);
                Raylib.DrawCircleV(ctr + new Vector2(s * 0.7f, -s * 0.35f), s * 0.12f, c);
                Raylib.DrawCircleV(ctr + new Vector2(s * 0.7f, s * 0.35f), s * 0.12f, c);
                break;
            case "closeranks":   // 두 줄 점 (대열) + 가운데로 모이는 화살 머리
                for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                    Raylib.DrawRectangleRec(new Rectangle(ctr.X + (col - 1.5f) * s * 0.45f - s * 0.1f, ctr.Y + (row - 0.5f) * s * 0.7f - s * 0.1f + s * 0.25f, s * 0.2f, s * 0.2f), c);
                Tri(new(ctr.X - s * 0.35f, ctr.Y - s * 0.85f), new(ctr.X + s * 0.35f, ctr.Y - s * 0.85f), new(ctr.X, ctr.Y - s * 0.45f), c);
                break;
            case "attack":   // 조준선
                Raylib.DrawRing(ctr, s * 0.55f, s * 0.7f, 0, 360, 24, c);
                Raylib.DrawRectangleRec(new Rectangle(ctr.X - s * 0.08f, ctr.Y - s * 0.95f, s * 0.16f, s * 0.5f), c);
                Raylib.DrawRectangleRec(new Rectangle(ctr.X - s * 0.08f, ctr.Y + s * 0.45f, s * 0.16f, s * 0.5f), c);
                Raylib.DrawRectangleRec(new Rectangle(ctr.X - s * 0.95f, ctr.Y - s * 0.08f, s * 0.5f, s * 0.16f), c);
                Raylib.DrawRectangleRec(new Rectangle(ctr.X + s * 0.45f, ctr.Y - s * 0.08f, s * 0.5f, s * 0.16f), c);
                break;
            case "build":    // 망치
                Raylib.DrawLineEx(ctr + new Vector2(-s * 0.6f, s * 0.7f), ctr + new Vector2(s * 0.25f, -s * 0.15f), s * 0.2f, c);
                Raylib.DrawRectanglePro(new Rectangle(ctr.X + s * 0.2f, ctr.Y - s * 0.2f, s * 0.9f, s * 0.38f), new Vector2(s * 0.45f, s * 0.19f), 45, c);
                break;
            case "repair":   // 렌치
                Raylib.DrawLineEx(ctr + new Vector2(-s * 0.6f, s * 0.6f), ctr + new Vector2(s * 0.3f, -s * 0.3f), s * 0.22f, c);
                Raylib.DrawRing(ctr + new Vector2(s * 0.42f, -s * 0.42f), s * 0.18f, s * 0.38f, 0, 360, 16, c);
                break;
            case "back":
                Arrow(ctr, s, -1, c);
                break;
            case "rally":    // 깃발
                Raylib.DrawLineEx(ctr + new Vector2(-s * 0.4f, -s * 0.8f), ctr + new Vector2(-s * 0.4f, s * 0.9f), s * 0.14f, c);
                Tri(ctr + new Vector2(-s * 0.4f, -s * 0.8f), ctr + new Vector2(-s * 0.4f, 0), ctr + new Vector2(s * 0.7f, -s * 0.4f), c);
                break;
            case "cancel":   // X
                Raylib.DrawLineEx(ctr - new Vector2(s * 0.6f), ctr + new Vector2(s * 0.6f), s * 0.22f, c);
                Raylib.DrawLineEx(ctr + new Vector2(-s * 0.6f, s * 0.6f), ctr + new Vector2(s * 0.6f, -s * 0.6f), s * 0.22f, c);
                break;
        }
    }

    /// <summary>dir = +1 오른쪽, -1 왼쪽.</summary>
    static void Arrow(Vector2 ctr, float s, int dir, Color c)
    {
        float t = s * 0.17f;
        float shaftStart = ctr.X - dir * s * 0.8f, headStart = ctr.X + dir * s * 0.1f, tip = ctr.X + dir * s * 0.85f;
        float x0 = MathF.Min(shaftStart, headStart);
        Raylib.DrawRectangleRec(new Rectangle(x0, ctr.Y - t, MathF.Abs(headStart - shaftStart), t * 2), c);
        Tri(new(headStart, ctr.Y - s * 0.45f), new(headStart, ctr.Y + s * 0.45f), new(tip, ctr.Y), c);
    }

    /// <summary>raylib 는 정점 순서(감김 방향)에 따라 삼각형이 컬링되므로 순서를 맞춰서 그린다.</summary>
    public static void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (cross > 0) (b, c) = (c, b);
        Raylib.DrawTriangle(a, b, c, col);
    }
}
