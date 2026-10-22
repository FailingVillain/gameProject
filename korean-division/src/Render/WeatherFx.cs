using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 날씨 · 시간대 화면 효과 (화면 전용 — 시뮬레이션 · 결정성과 무관). 뷰포트 위에 EndMode2D 뒤 · 화면 기호 전에 그린다.
///  · 시간대 색조 (새벽 붉은 기 · 해질녘 주황 · 밤 짙은 남색) + 가장자리 어둡게 (밤에 강함)
///  · 흐림 = 회색 막, 안개 = 흰 막 + 천천히 흐르는 큰 안개 덩어리
///  · 비 · 눈 = 화면 좌표 입자 (상한 MaxRain / MaxSnow, 카메라 이동만큼 함께 밀려 땅에 붙은 것처럼 보인다)
/// 밤에는 안개(전장)의 '탐색됨' 영역을 조금 덜 어둡게 해 (FogRenderer.ExploredAlpha) 색조와 겹쳐도 지형이 읽히게 한다.
/// </summary>
public sealed class WeatherFx
{
    const int MaxRain = 420, MaxSnow = 300, FogBlobs = 10;

    readonly Vector2[] pos = new Vector2[Math.Max(MaxRain, MaxSnow)];
    readonly float[] speed = new float[Math.Max(MaxRain, MaxSnow)];
    readonly Vector4[] blobs = new Vector4[FogBlobs];   // x, y (뷰포트 비율), 반경 비율, 흐름 속도
    readonly Random rng = new(12345);
    Vector2 lastTarget;
    float lastZoom = -1;
    Rectangle lastVp;
    WeatherKind seededFor = (WeatherKind)255;
    double clock;

    /// <summary>날씨/시간대 전환 페이드 길이(초, 화면 전용 벽시계). 시뮬은 즉시 전환 — 막 알파만 선형 보간 (TW3, D4=B).</summary>
    public const float FadeSeconds = 2f;
    World? lastWorld;
    int lastChanges;
    WeatherKind prevKind; TimeOfDay prevTime;   // 페이드 중 '이전' 상태
    WeatherKind curKind; TimeOfDay curTime;
    float fadeT = 1f;                            // 0 → 1 (1 = 전환 끝)

    /// <summary>시간대별 '탐색됨' 안개 투명도 (기본 150, 밤에는 낮춰 색조와 합쳐 너무 검지 않게).</summary>
    public static int ExploredAlphaFor(TimeOfDay t) => t switch { TimeOfDay.Night => 105, TimeOfDay.Dusk => 135, TimeOfDay.Dawn => 140, _ => 150 };

    public void Draw(World world, RtsCamera cam, Rectangle vp, float dt)
    {
        var w = world.Weather;
        clock += dt;
        if ((int)vp.Width <= 0 || (int)vp.Height <= 0) return;

        // 카메라 이동 → 입자를 같은 만큼 민다 (화면 좌표)
        Vector2 shift = Vector2.Zero;
        if (lastZoom > 0 && lastZoom == cam.Zoom && lastVp.Width == vp.Width && lastVp.Height == vp.Height) shift = (lastTarget - cam.Cam.Target) * cam.Zoom;
        lastTarget = cam.Cam.Target; lastZoom = cam.Zoom; lastVp = vp;

        // 전환 감지 (월드 교체 = 즉시, Changes 증가 = 이전 상태에서 2초 페이드)
        if (!ReferenceEquals(lastWorld, world)) { lastWorld = world; lastChanges = w.Changes; curKind = prevKind = w.Kind; curTime = prevTime = w.Time; fadeT = 1f; }
        else if (w.Changes != lastChanges)
        {
            lastChanges = w.Changes;
            // 페이드 도중 재전환이면 현재 보이는 쪽(도착 상태)을 이전으로 삼는다 (단순화, 약간의 튐 허용)
            prevKind = curKind; prevTime = curTime; curKind = w.Kind; curTime = w.Time; fadeT = 0f;
        }
        else { curKind = w.Kind; curTime = w.Time; }
        if (fadeT < 1f) fadeT = MathF.Min(1f, fadeT + dt / FadeSeconds);
        bool fading = fadeT < 1f && (prevKind != curKind || prevTime != curTime);

        if (fading)
        {
            DrawLayer(prevKind, prevTime, 1f - fadeT, false, vp, shift, dt);
            DrawLayer(curKind, curTime, fadeT, true, vp, shift, dt);
        }
        else DrawLayer(curKind, curTime, 1f, true, vp, shift, dt);
    }

    static Color Fade(Color c, float k) => new Color(c.R, c.G, c.B, (int)(c.A * k));

    /// <param name="isCur">도착(현재) 층인가 — 입자 · 안개 덩어리 상태는 도착 층만 갱신 (이전 층의 안개 덩어리는 도착이 안개가 아닐 때만 함께 사라진다).</param>
    void DrawLayer(WeatherKind kind, TimeOfDay time, float k, bool isCur, Rectangle vp, Vector2 shift, float dt)
    {
        int px = (int)vp.X, py = (int)vp.Y, pw = (int)vp.Width, ph = (int)vp.Height;
        // ---- 날씨 막
        switch (kind)
        {
            case WeatherKind.Overcast:
                Raylib.DrawRectangle(px, py, pw, ph, Fade(new Color(90, 96, 108, 34), k));
                break;
            case WeatherKind.Rain:
                Raylib.DrawRectangle(px, py, pw, ph, Fade(new Color(60, 70, 88, 52), k));
                break;
            case WeatherKind.Snow:
                Raylib.DrawRectangle(px, py, pw, ph, Fade(new Color(225, 232, 240, 34), k));
                break;
            case WeatherKind.Fog:
                Raylib.DrawRectangle(px, py, pw, ph, Fade(new Color(205, 210, 214, 62), k));
                if (isCur || curKind != WeatherKind.Fog) DrawFogBlobs(vp, shift, dt, k);
                break;
        }

        // ---- 시간대 색조
        Color tint = time switch
        {
            TimeOfDay.Dawn => new Color(255, 160, 130, 34),
            TimeOfDay.Dusk => new Color(255, 120, 50, 44),
            TimeOfDay.Night => new Color(8, 14, 44, 128),
            _ => new Color(0, 0, 0, 0),
        };
        if (tint.A > 0) Raylib.DrawRectangle(px, py, pw, ph, Fade(tint, k));

        // ---- 비 · 눈 입자
        if (isCur && kind is WeatherKind.Rain or WeatherKind.Snow) DrawParticles(kind, time, vp, shift, dt, k);

        // ---- 가장자리 어둡게 (비네트): 네 변 그라데이션 띠
        int va = time switch { TimeOfDay.Night => 150, TimeOfDay.Dusk => 70, TimeOfDay.Dawn => 60, _ => 0 };
        if (kind is WeatherKind.Fog or WeatherKind.Rain) va += 30;
        if (va > 0)
        {
            var edge = Fade(new Color(0, 0, 0, Math.Min(va, 200)), k);
            var clear = new Color(0, 0, 0, 0);
            int bw = pw / 6, bh = ph / 5;
            Raylib.DrawRectangleGradientH(px, py, bw, ph, edge, clear);
            Raylib.DrawRectangleGradientH(px + pw - bw, py, bw, ph, clear, edge);
            Raylib.DrawRectangleGradientV(px, py, pw, bh, edge, clear);
            Raylib.DrawRectangleGradientV(px, py + ph - bh, pw, bh, clear, edge);
        }
    }

    void DrawFogBlobs(Rectangle vp, Vector2 shift, float dt, float k)
    {
        if (seededFor != WeatherKind.Fog)
        {
            for (int i = 0; i < FogBlobs; i++)
                blobs[i] = new Vector4((float)rng.NextDouble(), (float)rng.NextDouble(), 0.18f + (float)rng.NextDouble() * 0.22f, 0.004f + (float)rng.NextDouble() * 0.01f);
            seededFor = WeatherKind.Fog;
        }
        float m = MathF.Max(vp.Width, vp.Height);
        for (int i = 0; i < FogBlobs; i++)
        {
            var b = blobs[i];
            b.X += b.W * dt + shift.X / vp.Width;
            b.Y += shift.Y / vp.Height;
            if (b.X > 1.4f) b.X -= 1.8f; else if (b.X < -0.4f) b.X += 1.8f;
            if (b.Y > 1.4f) b.Y -= 1.8f; else if (b.Y < -0.4f) b.Y += 1.8f;
            blobs[i] = b;
            float r = b.Z * m;
            var c = new Vector2(vp.X + b.X * vp.Width, vp.Y + b.Y * vp.Height);
            Raylib.DrawCircleGradient(c, r, Fade(new Color(220, 224, 228, 70), k), new Color(220, 224, 228, 0));
        }
    }

    void DrawParticles(WeatherKind kind, TimeOfDay time, Rectangle vp, Vector2 shift, float dt, float k)
    {
        bool rain = kind == WeatherKind.Rain;
        int n = rain ? MaxRain : MaxSnow;
        // 넓은 화면일수록 많이, 작은 창은 적게 (상한 고정)
        n = Math.Min(n, (int)(n * (vp.Width * vp.Height) / (1600f * 900f)) + 40);
        if (seededFor != kind)
        {
            for (int i = 0; i < pos.Length; i++)
            {
                pos[i] = new Vector2(vp.X + (float)rng.NextDouble() * vp.Width, vp.Y + (float)rng.NextDouble() * vp.Height);
                speed[i] = 0.6f + (float)rng.NextDouble() * 0.8f;
            }
            seededFor = kind;
        }
        bool night = time == TimeOfDay.Night;
        var col = Fade(rain ? new Color(175, 190, 215, night ? 90 : 120) : new Color(245, 248, 255, night ? 150 : 210), k);
        Vector2 vel = rain ? new Vector2(-120, 900) : new Vector2(-18, 55);
        float sway = (float)Math.Sin(clock * 0.9);
        for (int i = 0; i < n; i++)
        {
            var p = pos[i] + shift + vel * speed[i] * dt;
            if (!rain) p.X += sway * 12f * speed[i] * dt;
            // 화면 밖으로 나가면 반대편에서 다시
            if (p.Y > vp.Y + vp.Height) { p.Y -= vp.Height; p.X = vp.X + (float)rng.NextDouble() * vp.Width; }
            else if (p.Y < vp.Y) p.Y += vp.Height;
            if (p.X < vp.X) p.X += vp.Width; else if (p.X > vp.X + vp.Width) p.X -= vp.Width;
            pos[i] = p;
            if (rain)
            {
                var tail = vel * speed[i] * 0.018f;
                Raylib.DrawLineV(p, p - tail, col);
            }
            else
            {
                float s = 1.1f + speed[i] * 1.2f;
                Raylib.DrawRectangleV(p, new Vector2(s, s), col);
            }
        }
    }
}
