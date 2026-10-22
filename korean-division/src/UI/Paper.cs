using System.Numerics;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>
/// UI 종이 질감 (T08). 렌더 전용 — 시뮬 상태 · 난수와 무관 (고정 해시로 한 번 만든 타일 텍스처를 패널 위에 약하게 겹친다).
/// 그림 자원 없이 코드로 생성하므로 창이 없는 환경(selftest)에서는 아무것도 하지 않는다.
/// </summary>
public static class Paper
{
    const int Tile = 128;
    static Texture2D tex;
    static bool made, failed;

    static uint Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }

    static bool Ensure()
    {
        if (made) return true;
        if (failed || !Raylib.IsWindowReady()) return false;
        try
        {
            Image img = Raylib.GenImageColor(Tile, Tile, new Color(0, 0, 0, 0));
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    uint h = Hash(x, y);
                    int v = (int)(h & 0xFF);
                    // 거친 알갱이: 대부분 투명, 일부는 어두운 · 밝은 점
                    if (v < 40) Raylib.ImageDrawPixel(ref img, x, y, new Color(40, 28, 16, 26 + (v & 7)));
                    else if (v > 224) Raylib.ImageDrawPixel(ref img, x, y, new Color(255, 244, 220, 16 + (v & 7)));
                }
            // 섬유: 짧은 가는 선 (타일 가장자리에서 감기지 않게 안쪽에만)
            for (int i = 0; i < 46; i++)
            {
                uint h = Hash(i, 977);
                int x0 = 8 + (int)(h % (Tile - 16)), y0 = 8 + (int)((h >> 8) % (Tile - 16));
                int dx = (int)((h >> 16) % 13) - 6, dy = (int)((h >> 20) % 7) - 3;
                Raylib.ImageDrawLine(ref img, x0, y0, x0 + dx, y0 + dy, new Color(70, 52, 30, 20));
            }
            tex = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            Raylib.SetTextureWrap(tex, TextureWrap.Repeat);
            made = true;
        }
        catch { failed = true; }
        return made;
    }

    public static void Unload()
    {
        if (made) Raylib.UnloadTexture(tex);
        made = false;
    }

    /// <summary>r 위에 종이 알갱이를 겹치고 안쪽에 가는 가장자리 그림자를 넣는다 (fill 은 호출쪽이 이미 칠함).</summary>
    public static void Grain(Rectangle r)
    {
        if (r.Width < 4 || r.Height < 4 || !Ensure()) return;
        // 화면 좌표에 고정된 타일 (패널이 움직여도 알갱이가 헤엄치지 않음)
        Raylib.DrawTextureRec(tex, new Rectangle(r.X, r.Y, r.Width, r.Height), new Vector2(r.X, r.Y), Color.White);
        Raylib.DrawRectangleLinesEx(new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), 1, new Color(0, 0, 0, 38));
    }
}
