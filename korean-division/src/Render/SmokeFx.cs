using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// 연막 (N59, Breach.cs) 그리기 — 표현 전용. 연막 한 덩이 = 회색 반투명 원 여러 개 (위치 해시로 고정된 뭉게 모양), 처음 1.5초 피어오르고 마지막 5초 옅어진다.
/// 가장자리나 중심이 한 곳이라도 보이면 그린다 (연막 속은 시야가 막혀 안 보이므로). 전용 그림은 나중.
/// </summary>
internal static class SmokeFx
{
    static readonly Vector2[] Dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    public static void Draw(World world, int me, Vector2 min, Vector2 max)
    {
        if (world.Smokes.Count == 0) return;
        var fog = world.Fog;
        float ts = Config.TileSize;
        foreach (var s in world.Smokes)
        {
            if (s.Pos.X + s.Radius < min.X - 1 || s.Pos.X - s.Radius > max.X + 1 || s.Pos.Y + s.Radius < min.Y - 1 || s.Pos.Y - s.Radius > max.Y + 1) continue;
            bool seen = s.Owner == me || fog.IsVisible(me, s.Pos);
            for (int k = 0; k < Dirs.Length && !seen; k++) seen = fog.IsVisible(me, s.Pos + Dirs[k] * (s.Radius * 0.9f));
            if (!seen) continue;
            float age = (world.Tick - s.StartTick) * Config.SimDt, left = (s.EndTick - world.Tick) * Config.SimDt;
            float a = MathF.Min(1f, age / 1.5f) * MathF.Min(1f, left / 5f);
            if (a <= 0) continue;
            float grow = 0.6f + 0.4f * MathF.Min(1f, age / 1.5f);
            uint h = (uint)(s.StartTick * 2654435761L) ^ (uint)(int)(s.Pos.X * 16) * 40503u ^ (uint)(int)(s.Pos.Y * 16) * 2246822519u;
            var c = s.Pos * ts;
            Raylib.DrawCircleV(c, s.Radius * ts * grow, Palette.Alpha(new Color(200, 200, 196, 255), (int)(150 * a)));
            for (int i = 0; i < 7; i++)
            {
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                float ang = (h & 1023) / 1024f * MathF.Tau, dist = ((h >> 10) & 255) / 255f * 0.55f * s.Radius;
                float r = (0.35f + ((h >> 18) & 63) / 63f * 0.3f) * s.Radius;
                var p = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * dist * ts * grow;
                Raylib.DrawCircleV(p, r * ts * grow, Palette.Alpha(new Color(226, 226, 220, 255), (int)(110 * a)));
            }
        }
    }
}
