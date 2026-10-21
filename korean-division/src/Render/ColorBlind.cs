using System.Numerics;
using KoreanDivision.UI;
using Raylib_cs;

namespace KoreanDivision.Render;

/// <summary>색각 이상 대응 (T27 ⑧): 켜면 세력 색 말고도 세력별 도형 표식을 병기 (색 단독 구분 금지). 표시 전용 — 시뮬 · 저장과 무관. 기본 꺼짐 (UserSettings.ColorBlind).</summary>
public static class ColorBlind
{
    public const int ShapeCount = 5;
    public static bool Enabled => UserSettings.ColorBlind;

    /// <summary>세력(owner) 번호 → 도형 0..4 (원 · 사각 · 삼각 · 마름모 · 십자). 같은 세력은 항상 같은 도형.</summary>
    public static int Shape(int owner) => ((owner > 0 ? owner - 1 : 0) % ShapeCount + ShapeCount) % ShapeCount;

    /// <summary>중심 c, 반지름 rad 의 표식 (검은 테두리 + 세력 색 면). 켜져 있을 때만 호출.</summary>
    public static void Draw(Vector2 c, float rad, int owner, Color team)
    {
        int s = Shape(owner);
        float o = MathF.Max(rad * 0.28f, 0.5f);
        Mark(c, rad + o, s, Color.Black);
        Mark(c, rad, s, Color.White);
        Mark(c, rad * 0.62f, s, team);
    }

    static void Mark(Vector2 c, float r, int shape, Color col)
    {
        switch (shape)
        {
            case 0: Raylib.DrawCircleV(c, r, col); break;
            case 1: Raylib.DrawRectangleRec(new Rectangle(c.X - r * 0.85f, c.Y - r * 0.85f, r * 1.7f, r * 1.7f), col); break;
            case 2: Raylib.DrawPoly(new Vector2(c.X, c.Y + r * 0.15f), 3, r * 1.2f, -90f, col); break;
            case 3: Raylib.DrawPoly(c, 4, r * 1.25f, 0f, col); break;
            default:
                Raylib.DrawRectangleRec(new Rectangle(c.X - r, c.Y - r * 0.38f, r * 2, r * 0.76f), col);
                Raylib.DrawRectangleRec(new Rectangle(c.X - r * 0.38f, c.Y - r, r * 0.76f, r * 2), col);
                break;
        }
    }
}
