using Raylib_cs;
using KoreanDivision.UI;

namespace KoreanDivision.Core;

/// <summary>지형 종류. 값은 저장 파일 · 지도 바이트로 쓰이므로 끝에만 추가한다.</summary>
public enum TerrainType : byte { Grass, Dirt, Sand, Water, Rock, Forest, Rubble, House, Wire, Trench, Crater, Rail, Ford, Bridge }

public readonly record struct TerrainInfo(string Name, Color Color, bool Walkable);

/// <summary>지형 정의. 새 지형은 enum 과 Info 배열에 같은 순서로 추가. 이동 · 엄폐 수치는 balance.json "terrain" (TerrainRulesDef).</summary>
public static class Terrains
{
    static readonly TerrainInfo[] Info =
    {
        new(S.TerrainGrass, new Color(70, 116, 60, 255), true),
        new(S.TerrainDirt, new Color(126, 102, 70, 255), true),
        new(S.TerrainSand, new Color(194, 178, 126, 255), true),
        new(S.TerrainWater, new Color(42, 84, 138, 255), false),
        new(S.TerrainRock, new Color(92, 90, 98, 255), false),
        new(S.TerrainForest, new Color(38, 78, 40, 255), true),
        new(S.TerrainRubble, new Color(118, 110, 100, 255), true),
        new(S.TerrainHouse, new Color(150, 92, 70, 255), false),
        new(S.TerrainWire, new Color(96, 104, 76, 255), false),   // 임시 (2026-10-19): 통과 불가 장애물, 시야 · 사선은 막지 않음
        new(S.TerrainTrench, new Color(98, 78, 52, 255), true),
        new(S.TerrainCrater, new Color(84, 70, 56, 255), true),   // 포격 자국 (포탄 착탄으로 생김, World.TryCrater)
        new(S.TerrainRail, new Color(104, 96, 88, 255), true),    // 철도 선로 (2026-10-22): 자갈 바닥 + 침목 · 레일 (RailRenderer). 통행 · 시야 · 사선 = 맨땅과 같음, 엄폐 없음. 장갑열차 · 화물열차의 길
        new(S.TerrainFord, new Color(96, 156, 172, 255), true),   // 여울 (색 = 작은 지도 · 개관 색: 깊은 물(진한 파랑)과 모래(황색) 사이의 청록) (2026-10-22, 하천 T39a): 얕은 물 — 통행 가능, 이동 느림 (balance terrain.fordMove), 엄폐 없음, 사선 · 시야 안 막음. 깊은 물은 그대로 Water
        new(S.TerrainBridge, new Color(138, 106, 66, 255), true),  // 다리 (2026-10-22, 하천 T39d): 널빤지 바닥 — 통행 가능 x1 (MoveMul 기본), 엄폐 없음, 사선 · 시야 안 막음. 지도 paint "bridge" 로만 생김 (기존 지도 불변). 판자 방향 · 난간은 BridgeRenderer
    };

    public static int Count => Info.Length;
    public static TerrainInfo Get(TerrainType t) => Info[(int)t];
}
