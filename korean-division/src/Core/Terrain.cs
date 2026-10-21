using Raylib_cs;
using KoreanDivision.UI;

namespace KoreanDivision.Core;

/// <summary>지형 종류. 값은 저장 파일 · 지도 바이트로 쓰이므로 끝에만 추가한다.</summary>
public enum TerrainType : byte { Grass, Dirt, Sand, Water, Rock, Forest, Rubble, House, Wire, Trench, Crater }

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
    };

    public static int Count => Info.Length;
    public static TerrainInfo Get(TerrainType t) => Info[(int)t];
}
