namespace KoreanDivision;

/// <summary>전역 설정값. 화면/맵/조작 관련 상수는 모두 여기서 관리한다.</summary>
public static class Config
{
    public const string Title = "KOREAN DIVISION : MARCH ON MANCHURIA";
    public const string Version = "0.2";

    // ---- 화면 ----
    public const int ScreenWidth = 1280;
    public const int ScreenHeight = 720;
    public const int MinScreenWidth = 1024;
    public const int MinScreenHeight = 640;
    public const int MaxFps = 240;

    public const int TopBarHeight = 30;
    public const int HudHeight = 210;

    // ---- 맵 ----
    // 2026-10-19 사거리 · 시야 개편: 지도 한 변 ×1.5 (2인 100 → 150, 인원당 면적 5000 → 11250)
    public const int DefaultMapWidth = 150;
    public const int DefaultMapHeight = 150;
    public const int DefaultPlayers = 10;
    public const int MaxPlayers = 16;
    public const int TilesPerPlayer = 11250;   // 맵 크기 자동 결정: 인원 × 이 면적 (2인 = 150×150, 10인 336², 16인 424²)

    /// <summary>인원수에 맞춘 정사각 맵 한 변 (8의 배수, 150 ~ MaxMapSize).</summary>
    public static int MapSizeFor(int players) =>
        players <= 2 ? DefaultMapWidth : Math.Clamp((int)(MathF.Round(MathF.Sqrt(players * (float)TilesPerPlayer) / 8) * 8), DefaultMapWidth, MaxMapSize);
    public const int MinMapSize = 48;
    public const int MaxMapSize = 1500;   // 지도 크기 5단계 상한 (초대형 1500, 2026-10-19)

    // ---- 지도 크기 5단계 (사용자 2026-10-19): 초소형 200 · 소형 400 · 중형 800 · 대형 1200 · 초대형 1500 (한 변 칸 수, 정사각).
    // 시작 화면 '지도 크기' 선택지 · `--map tiny|small|medium|large|huge`. '자동' = 인원 × TilesPerPlayerTier 면적이 들어가는 가장 작은 단계
    // (2인 200, 3~8인 400, 9~16인 800). 대형 · 초대형은 직접 고른다. MapSizeFor (옛 인원 공식, 테스트 · 명령줄 기본) 는 그대로.
    public static readonly int[] MapTiers = { 200, 400, 800, 1200, 1500 };
    public static readonly string[] MapTierKeys = { "tiny", "small", "medium", "large", "huge" };
    public const int TilesPerPlayerTier = 20000;

    /// <summary>인원에 맞는 지도 크기 단계 (한 변): 인원 × TilesPerPlayerTier 이상인 가장 작은 단계.</summary>
    public static int MapTierFor(int players)
    {
        foreach (int t in MapTiers) if ((long)t * t >= (long)Math.Max(2, players) * TilesPerPlayerTier) return t;
        return MapTiers[^1];
    }

    /// <summary>단계 이름 (tiny · small · medium · large · huge, 또는 한글 초소형 · 소형 · 중형 · 대형 · 초대형) → 한 변. 모르면 null.</summary>
    public static int? MapTierByName(string name)
    {
        string n = name.Trim().ToLowerInvariant();
        for (int i = 0; i < MapTiers.Length; i++) if (n == MapTierKeys[i]) return MapTiers[i];
        return n switch { "초소형" => 200, "소형" => 400, "중형" => 800, "대형" => 1200, "초대형" => 1500, _ => null };
    }
    public const int DefaultSeed = 20261009;
    public const int TileSize = 32;          // 월드 1타일 = 32px (줌 100% 기준)
    public const int ChunkTiles = 32;        // 맵 렌더링 청크 크기 (타일)
    public const int MaxLoadedChunks = 64;   // GPU 에 올려둘 최대 청크 수

    // ---- 카메라 / 조작 ----
    public const float MinZoom = 0.35f;
    public const float MaxZoom = 4.0f;
    public const float ZoomStep = 1.15f;
    public const float ScrollSpeed = 1400f;  // 화면 px/초
    public const int EdgeScrollMargin = 8;
    public const float DragThreshold = 6f;
    public const double DoubleClickTime = 0.3;
    public const double GroupDoubleTapTime = 0.35;   // 같은 부대 번호를 연타하면 카메라 이동

    // ---- 시뮬레이션 ----
    public const float SimDt = 1f / 60f;     // 고정 틱
    public const int MaxStepsPerFrame = 5;
    public const int MaxUnits = 8000;     // 16세력 × 인구 상한 500
    public const int HumanPlayer = 1;
}
