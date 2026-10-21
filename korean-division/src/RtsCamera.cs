using System.Numerics;
using Raylib_cs;

namespace KoreanDivision;

/// <summary>
/// RTS 카메라. Camera2D 를 감싸며, 외부 API 는 월드 좌표(타일 단위)를 쓴다.
/// Offset = 뷰포트 좌상단, Target = 뷰포트 좌상단에 보이는 월드 픽셀 좌표.
/// </summary>
public sealed class RtsCamera
{
    public Camera2D Cam = new() { Zoom = 1f };
    public Rectangle Viewport { get; private set; }
    readonly float mapPxW, mapPxH;

    public RtsCamera(int mapWidth, int mapHeight)
    {
        mapPxW = mapWidth * Config.TileSize;
        mapPxH = mapHeight * Config.TileSize;
    }

    public float Zoom => Cam.Zoom;

    public void SetViewport(Rectangle vp)
    {
        Vector2 center = Viewport.Width > 0 ? CenterWorld : Vector2.Zero;
        Viewport = vp;
        Cam.Offset = new Vector2(vp.X, vp.Y);
        if (center != Vector2.Zero) CenterOn(center);
        else Clamp();
    }

    public Vector2 ScreenToWorld(Vector2 screen) => Raylib.GetScreenToWorld2D(screen, Cam) / Config.TileSize;
    public Vector2 WorldToScreen(Vector2 world) => Raylib.GetWorldToScreen2D(world * Config.TileSize, Cam);

    Vector2 ViewSizePx => new Vector2(Viewport.Width, Viewport.Height) / Cam.Zoom;
    public Vector2 CenterWorld => (Cam.Target + ViewSizePx / 2) / Config.TileSize;

    /// <summary>화면에 보이는 월드 영역 (타일 단위, min/max).</summary>
    public (Vector2 Min, Vector2 Max) VisibleWorld()
    {
        Vector2 min = Cam.Target / Config.TileSize;
        return (min, min + ViewSizePx / Config.TileSize);
    }

    public void CenterOn(Vector2 world)
    {
        Cam.Target = world * Config.TileSize - ViewSizePx / 2;
        Clamp();
    }

    public void Scroll(Vector2 screenDelta)
    {
        Cam.Target += screenDelta / Cam.Zoom;
        Clamp();
    }

    /// <summary>확대 배율을 직접 지정 (화면 중앙 기준).</summary>
    public void SetZoom(float zoom)
    {
        if (!float.IsFinite(zoom)) return;   // "zoom nan" 이 카메라를 영구히 망가뜨리지 않게
        Vector2 c = CenterWorld;
        Cam.Zoom = Math.Clamp(zoom, Config.MinZoom, Config.MaxZoom);
        CenterOn(c);
    }

    public void ZoomAt(float wheel, Vector2 screenPos)
    {
        Vector2 before = Raylib.GetScreenToWorld2D(screenPos, Cam);
        Cam.Zoom = Math.Clamp(Cam.Zoom * MathF.Pow(Config.ZoomStep, wheel), Config.MinZoom, Config.MaxZoom);
        Vector2 after = Raylib.GetScreenToWorld2D(screenPos, Cam);
        Cam.Target += before - after;
        Clamp();
    }

    void Clamp()
    {
        Vector2 view = ViewSizePx;
        Cam.Target.X = view.X >= mapPxW ? (mapPxW - view.X) / 2 : Math.Clamp(Cam.Target.X, 0, mapPxW - view.X);
        Cam.Target.Y = view.Y >= mapPxH ? (mapPxH - view.Y) / 2 : Math.Clamp(Cam.Target.Y, 0, mapPxH - view.Y);
    }
}
