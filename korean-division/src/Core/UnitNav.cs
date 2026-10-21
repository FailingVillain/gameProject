using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>Unit 의 길찾기 · 정체 해소 · 진형 상태 (Nav/WorldNav/Formation 이 쓴다). 모두 틱 · 입력의 함수 — 결정적.</summary>
public sealed partial class Unit
{
    /// <summary>큰 유닛 (전차 등): 여유 공간이 있는 칸으로만 다니는 큰 길찾기 그래프를 쓴다.</summary>
    public bool Large => Def.Radius > World.LargeRadius;

    internal byte NavKind;               // NavRoute 를 만든 그래프 (0 일반, 1 큰 유닛용)
    // 정체 감지: ProgTick 부터 45틱 동안 거의 못 움직이면 StuckLevel 증가 → 경로 새로 구하기 → 비켜서기
    internal Vector2 ProgPos;
    internal long ProgTick, ProgLast = -100, NudgeUntil;
    internal int StuckLevel;
    internal Vector2 NudgeDir;
}

public sealed partial class MoveGroup
{
    internal NavRoute? RouteL;           // 큰 유닛용 그룹 경로
    internal FormPlan? Plan;             // 진형 (null = 자유)
}
