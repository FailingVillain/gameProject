namespace KoreanDivision.Core;

/// <summary>
/// 결정적 수학: 시뮬레이션(Core · AI)은 MathF.Sin/Cos/Atan2/Exp/Log/Pow 를 쓰면 안 된다 — 플랫폼 · 런타임(libm / CRT / 하드웨어)마다
/// 마지막 비트가 달라질 수 있다. 여기 함수들은 + - * / 비교 · Floor(정확) 만 쓰므로 IEEE-754 에서 모든 x64 / ARM / x86(SSE2) 에서 비트 단위로 같다.
/// 정밀도: Sin/Cos 절대오차 ≲ 4e-6 (|x|≤50 범위 축소 포함), Atan2 ≲ 1e-7 rad. 거대한 입력(|x| &gt; ~1e5)은 정밀도가 떨어지지만 결정적이다.
/// 곱셈 · 덧셈은 항상 별개의 문장으로 쓴다 (JIT 는 FMA 로 합치지 않지만, 합치는 환경이 생겨도 식 구조가 바뀌지 않도록 Horner 형태 고정).
/// </summary>
public static class DetMath
{
    public const float Pi = 3.14159265358979f;
    public const float HalfPi = 1.57079632679490f;
    public const float Tau = 6.28318530717959f;
    const float InvTau = 0.159154943091895f;

    /// <summary>각도를 [-π, π) 로 감는다.</summary>
    public static float WrapPi(float a) => a - Tau * MathF.Floor(a * InvTau + 0.5f);

    public static float Sin(float x)
    {
        x = WrapPi(x);
        if (x > HalfPi) x = Pi - x;
        else if (x < -HalfPi) x = -Pi - x;
        float x2 = x * x;
        float p = -1.0f / 6227020800.0f;
        p = p * x2 + 1.0f / 39916800.0f;
        p = p * x2 - 1.0f / 362880.0f;
        p = p * x2 + 1.0f / 5040.0f;
        p = p * x2 - 1.0f / 120.0f;
        p = p * x2 + 1.0f / 6.0f;
        p = p * x2 - 1.0f;
        return -(x * p);
    }

    public static float Cos(float x) => Sin(x + HalfPi);

    public static (float Sin, float Cos) SinCos(float x) => (Sin(x), Cos(x));

    /// <summary>atan2(y, x), 결과 (-π, π]. (0,0) → 0.</summary>
    public static float Atan2(float y, float x)
    {
        float ax = x < 0 ? -x : x, ay = y < 0 ? -y : y;
        if (ax == 0 && ay == 0) return 0;
        bool swap = ay > ax;
        float t = swap ? ax / ay : ay / ax;
        float a = AtanUnit(t);
        if (swap) a = HalfPi - a;
        if (x < 0) a = Pi - a;
        return y < 0 ? -a : a;
    }

    /// <summary>atan(t), t ∈ [0,1] (Abramowitz–Stegun 4.4.49, 오차 ≤ 2e-8).</summary>
    static float AtanUnit(float t)
    {
        float t2 = t * t;
        float p = 0.0028662257f;
        p = p * t2 - 0.0161657367f;
        p = p * t2 + 0.0429096138f;
        p = p * t2 - 0.0752896400f;
        p = p * t2 + 0.1065626393f;
        p = p * t2 - 0.1420889944f;
        p = p * t2 + 0.1999355085f;
        p = p * t2 - 0.3333314528f;
        p = p * t2 + 1.0f;
        return t * p;
    }
}
