using System;
using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 시간이 걸리는 동작 (거치 · 방열 · 철수) 진행 가로 막대 (착검 · 칼 뽑기는 표시 없음). 표시 전용 — 시뮬 상태를 읽기만 한다.
/// 새 타이머 (기병 승하마 등) 는 <see cref="TryGet"/> 에 한 줄 추가하면 된다.
/// </summary>
internal static class ProgressRing
{
    /// <summary>시간 걸리는 상태인가. ratio 0→1 = 시작→완료 (철수도 완료로 올라간다), kind 0 = 방열/거치, 1 = 철수.</summary>
    public static bool TryGet(World world, Unit u, bool detail, out float ratio, out int kind)
    {
        ratio = 0; kind = 0;
        if (!u.Alive) return false;
        switch (u.Deploy)
        {
            case DeployPhase.Deploying: ratio = u.DeployProgress; kind = 0; return true;
            case DeployPhase.Packing: ratio = 1f - u.DeployProgress; kind = 1; return true;
        }
        return false;
    }

    /// <summary>유닛 상단 작은 가로 막대. BeginMode2D 안, 월드 px 좌표.</summary>
    public static void Draw(Rectangle r, float zoom, float ratio, int kind)
    {
        ratio = Math.Clamp(ratio, 0f, 1f);
        float e = 1f / zoom;
        Raylib.DrawRectangleRec(new Rectangle(r.X - e, r.Y - e, r.Width + 2 * e, r.Height + 2 * e), new Color(0, 0, 0, 170));
        Color fill = kind == 1 ? new Color(235, 170, 70, 255) : new Color(120, 200, 240, 255);
        Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, r.Width * ratio, r.Height), fill);
    }
}
