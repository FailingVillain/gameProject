namespace KoreanDivision.Render;

/// <summary>
/// 소품 외형 변형 번호 (렌더 전용). 변형 = 게임플레이 속성이 같은 그림만 다른 외형 - sim · 저장 · 체크섬 · 리플레이와 무관.
/// 번호 = 소품 위치(타일 1/64 단위)의 정수 해시 % 변형 수. 순수 함수라 같은 맵이면 어느 PC · 프레임에서나 같은 그림이다.
/// 그림 규칙: props/&lt;종류&gt;/ 의 기존 파일 = 0번, props/&lt;종류&gt;/var_01 .. var_NN/ = 1.. (파일 이름은 0번과 같은 &lt;종류&gt;.png 등).
/// 변형 수는 폴더 존재로 정한다 (없으면 1 = 기존 외형 그대로). 상한 MaxVariants.
/// </summary>
public static class PropVariants
{
    /// <summary>종류당 변형 상한 (0번 포함). 종류 20개 x 8 x 약 130KB (LOD 3단 + 그림자) = 약 21MB 상한.</summary>
    public const int MaxVariants = 8;

    public static string FolderName(int v) => "var_" + v.ToString("00");

    public static int Pick(float x, float y, int variants)
    {
        if (variants <= 1) return 0;
        uint h = (uint)(int)MathF.Round(x * 64f) * 0x9E3779B1u ^ (uint)(int)MathF.Round(y * 64f) * 0x85EBCA77u;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return (int)(h % (uint)variants);
    }
}
