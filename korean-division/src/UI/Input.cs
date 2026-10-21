using System.Numerics;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>입력 원천: 실제 플레이는 Raylib, UI 퍼즈 테스트(--uifuzz)는 가짜 입력으로 바꿔 끼운다.</summary>
public interface IInputSource
{
    Vector2 GetMousePosition();
    Vector2 GetMouseDelta();
    float GetMouseWheelMove();
    bool IsMouseButtonPressed(MouseButton b);
    bool IsMouseButtonDown(MouseButton b);
    bool IsMouseButtonReleased(MouseButton b);
    bool IsKeyPressed(KeyboardKey k);
    bool IsKeyPressedRepeat(KeyboardKey k);
    bool IsKeyDown(KeyboardKey k);
    int GetCharPressed();
    bool IsWindowFocused();
    bool IsCursorOnScreen();
}

/// <summary>Raylib 입력 그대로 (기본값 — 실제 플레이 동작 불변).</summary>
public sealed class RaylibInput : IInputSource
{
    public Vector2 GetMousePosition() => Raylib.GetMousePosition();
    public Vector2 GetMouseDelta() => Raylib.GetMouseDelta();
    public float GetMouseWheelMove() => Raylib.GetMouseWheelMove();
    public bool IsMouseButtonPressed(MouseButton b) => Raylib.IsMouseButtonPressed(b);
    public bool IsMouseButtonDown(MouseButton b) => Raylib.IsMouseButtonDown(b);
    public bool IsMouseButtonReleased(MouseButton b) => Raylib.IsMouseButtonReleased(b);
    public bool IsKeyPressed(KeyboardKey k) => Raylib.IsKeyPressed(k);
    public bool IsKeyPressedRepeat(KeyboardKey k) => Raylib.IsKeyPressedRepeat(k);
    public bool IsKeyDown(KeyboardKey k) => Raylib.IsKeyDown(k);
    public int GetCharPressed() => Raylib.GetCharPressed();
    public bool IsWindowFocused() => Raylib.IsWindowFocused();
    public bool IsCursorOnScreen() => Raylib.IsCursorOnScreen();
}

/// <summary>아무 입력도 없음 (스크린샷 비교용 <c>KD_SHOT_NOINPUT=1</c> — 실제 마우스 위치 · 휠이 카메라를 움직이지 않게).</summary>
public sealed class IdleInput : IInputSource
{
    public Vector2 GetMousePosition() => new(-1, -1);
    public Vector2 GetMouseDelta() => Vector2.Zero;
    public float GetMouseWheelMove() => 0;
    public bool IsMouseButtonPressed(MouseButton b) => false;
    public bool IsMouseButtonDown(MouseButton b) => false;
    public bool IsMouseButtonReleased(MouseButton b) => false;
    public bool IsKeyPressed(KeyboardKey k) => false;
    public bool IsKeyPressedRepeat(KeyboardKey k) => false;
    public bool IsKeyDown(KeyboardKey k) => false;
    public int GetCharPressed() => 0;
    public bool IsWindowFocused() => false;
    public bool IsCursorOnScreen() => false;
}

/// <summary>입력 읽기 창구. Game · Hud · 콘솔 · 메뉴가 Raylib 대신 여기를 부른다.</summary>
public static class Input
{
    public static IInputSource Source = new RaylibInput();
    public static Vector2 GetMousePosition() => Source.GetMousePosition();
    public static Vector2 GetMouseDelta() => Source.GetMouseDelta();
    public static float GetMouseWheelMove() => Source.GetMouseWheelMove();
    public static bool IsMouseButtonPressed(MouseButton b) => Source.IsMouseButtonPressed(b);
    public static bool IsMouseButtonDown(MouseButton b) => Source.IsMouseButtonDown(b);
    public static bool IsMouseButtonReleased(MouseButton b) => Source.IsMouseButtonReleased(b);
    public static bool IsKeyPressed(KeyboardKey k) => Source.IsKeyPressed(k);
    public static bool IsKeyPressedRepeat(KeyboardKey k) => Source.IsKeyPressedRepeat(k);
    public static bool IsKeyDown(KeyboardKey k) => Source.IsKeyDown(k);
    public static int GetCharPressed() => Source.GetCharPressed();
    public static bool IsWindowFocused() => Source.IsWindowFocused();
    public static bool IsCursorOnScreen() => Source.IsCursorOnScreen();
}
