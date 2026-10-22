using System.Numerics;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>
/// [T47] 작전 지도 탭 (캠페인 홈 10번) 의 확대 · 이동 상태와 좌표 변환. 순수 계산 (그리기 · 입력 없음) — 세션 한정, 캠페인 저장과 무관.
/// 월드 좌표는 가상 지도 (<see cref="WorldW"/> x <see cref="WorldH"/>), Zoom 1 = 뷰포트에 맞춤 (fit). 중심 = 뷰포트 한가운데에 오는 월드 점.
/// </summary>
public sealed class MapZoomView
{
    public const float WorldW = 960f, WorldH = 320f;
    public const float MinZoom = 1f, MaxZoom = 8f;
    /// <summary>+/- 버튼 · 키 한 번의 배율 · 휠 한 칸의 배율.</summary>
    public const float ButtonFactor = 1.5f, WheelFactor = 1.18f;

    /// <summary>작전 노선 거점 (월드 좌표): 압록강 안동 봉황성 안산 요양 봉천 장춘 하얼빈 (남서 → 북동).</summary>
    public static readonly Vector2[] NodePos =
    {
        new(80, 255), new(190, 215), new(320, 238), new(450, 170),
        new(560, 140), new(660, 92), new(775, 70), new(880, 40),
    };

    public float Zoom { get; private set; } = 1f;
    public Vector2 Center { get; private set; } = new(WorldW / 2, WorldH / 2);

    public static float ClampZoom(float z) => float.IsNaN(z) ? MinZoom : Math.Clamp(z, MinZoom, MaxZoom);

    public static float FitScale(Rectangle vp) => MathF.Max(0.0001f, MathF.Min(vp.Width / WorldW, vp.Height / WorldH));
    public float Scale(Rectangle vp) => FitScale(vp) * Zoom;
    public bool IsFit => Zoom <= MinZoom + 0.001f;

    public Vector2 ToScreen(Vector2 world, Rectangle vp)
    {
        float s = Scale(vp);
        return new Vector2(vp.X + vp.Width / 2 + (world.X - Center.X) * s, vp.Y + vp.Height / 2 + (world.Y - Center.Y) * s);
    }

    public Vector2 ToWorld(Vector2 screen, Rectangle vp)
    {
        float s = Scale(vp);
        return new Vector2(Center.X + (screen.X - vp.X - vp.Width / 2) / s, Center.Y + (screen.Y - vp.Y - vp.Height / 2) / s);
    }

    /// <summary>지도가 뷰포트 밖으로 밀려나지 않게: 지도가 뷰포트보다 작은 축은 가운데 고정, 큰 축은 가장자리까지만.</summary>
    public void Clamp(Rectangle vp)
    {
        Zoom = ClampZoom(Zoom);
        float s = Scale(vp);
        float hx = vp.Width / 2 / s, hy = vp.Height / 2 / s;
        float cx = float.IsNaN(Center.X) ? WorldW / 2 : Center.X, cy = float.IsNaN(Center.Y) ? WorldH / 2 : Center.Y;
        cx = hx * 2 >= WorldW ? WorldW / 2 : Math.Clamp(cx, hx, WorldW - hx);
        cy = hy * 2 >= WorldH ? WorldH / 2 : Math.Clamp(cy, hy, WorldH - hy);
        Center = new Vector2(cx, cy);
    }

    /// <summary>화면 점 아래의 월드 점을 고정한 채 배율을 factor 배 (범위 안으로 정리).</summary>
    public void ZoomAt(Vector2 screen, float factor, Rectangle vp)
    {
        if (float.IsNaN(factor) || factor <= 0) return;
        Clamp(vp);
        Vector2 anchor = ToWorld(screen, vp);
        Zoom = ClampZoom(Zoom * factor);
        float s = Scale(vp);
        Center = new Vector2(anchor.X - (screen.X - vp.X - vp.Width / 2) / s, anchor.Y - (screen.Y - vp.Y - vp.Height / 2) / s);
        Clamp(vp);
    }

    public void ZoomCenter(float factor, Rectangle vp) => ZoomAt(new Vector2(vp.X + vp.Width / 2, vp.Y + vp.Height / 2), factor, vp);

    /// <summary>끌기: 화면 픽셀 이동량만큼 지도를 같이 움직인다.</summary>
    public void PanPixels(Vector2 delta, Rectangle vp)
    {
        if (float.IsNaN(delta.X) || float.IsNaN(delta.Y)) return;
        float s = Scale(vp);
        Center = new Vector2(Center.X - delta.X / s, Center.Y - delta.Y / s);
        Clamp(vp);
    }

    public void Reset() { Zoom = 1f; Center = new Vector2(WorldW / 2, WorldH / 2); }

    /// <summary>확인 · 스크린샷용: 배율과 중심을 직접 지정 (다음 Clamp 에서 정리).</summary>
    public void SetView(float zoom, Vector2 center) { Zoom = ClampZoom(zoom); Center = center; }

    /// <summary>화면 점에서 반경 radius px 안에서 가장 가까운 거점 번호 (없으면 -1). 뷰포트 밖 점은 선택 못 한다.</summary>
    public int HitNode(Vector2 screen, Rectangle vp, float radius)
    {
        if (screen.X < vp.X || screen.Y < vp.Y || screen.X > vp.X + vp.Width || screen.Y > vp.Y + vp.Height) return -1;
        int best = -1; float bestD = radius * radius;
        for (int i = 0; i < NodePos.Length; i++)
        {
            Vector2 p = ToScreen(NodePos[i], vp);
            float d = Vector2.DistanceSquared(p, screen);
            if (d <= bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>거점 표식 반지름 (px): 확대하면 조금 커지되 상한을 둔다.</summary>
    public float MarkerRadius() => 6f + MathF.Min(Zoom, 4f) * 1.5f;
    public float LineWidth() => 2f + MathF.Min(Zoom, 4f);
}
