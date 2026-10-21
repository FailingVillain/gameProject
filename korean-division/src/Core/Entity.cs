using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>유닛과 건물의 공통 부분: 소유 세력 · 위치 · 체력 · 장갑 · 시야. 공격 대상이 될 수 있다.</summary>
public abstract class Entity
{
    public readonly int Id;
    public int Owner;
    public Vector2 Pos;              // 중심 (월드 타일 좌표)
    public float Hp;
    public bool Selected;            // UI 선택 상태 (Game 이 관리)
    public long LastDamagedTick = -1000;

    protected Entity(int id, int owner, Vector2 pos)
    {
        Id = id;
        Owner = owner;
        Pos = pos;
    }

    public abstract string Name { get; }
    public abstract float MaxHp { get; }
    public abstract float ArmorValue { get; }
    public abstract int Sight { get; }
    public abstract bool Indestructible { get; }

    public bool Alive => Hp > 0;

    /// <summary>점 p 에서 이 엔티티 표면까지의 거리 (사거리 판정용).</summary>
    public abstract float EdgeDistance(Vector2 p);
}

/// <summary>건물 = 도시 · 점령지 (방향 변경 §0-1 이후 건설 · 생산 없음). 타일 정렬된 Size×Size 영역을 예약한다 (도시는 통행 가능).</summary>
public sealed partial class Building : Entity
{
    public readonly BuildingDef Def;
    public readonly int TileX, TileY;     // 좌상단 타일

    // 점령
    public int CaptureFaction;             // 게이지를 채우는 중인 세력 (소유 세력과 같을 수 있음)
    public float CaptureProgress;          // 0..1
    public bool Contested;                 // 서로 적대인 세력이 함께 있음

    public Building(int id, BuildingDef def, int owner, int tileX, int tileY)
        : base(id, owner, new Vector2(tileX + def.Size / 2f, tileY + def.Size / 2f))
    {
        Def = def;
        TileX = tileX;
        TileY = tileY;
        Hp = def.Hp;
    }

    public override string Name => Def.Name;
    public override float MaxHp => Def.Hp;
    public override float ArmorValue => Defs.Current.Armor(Def.Armor)?.Armor ?? 0;
    public override int Sight => Def.Sight;
    public override bool Indestructible => Def.Indestructible;
    public int Size => Def.Size;

    public override float EdgeDistance(Vector2 p)
    {
        float dx = MathF.Max(MathF.Max(TileX - p.X, 0), p.X - (TileX + Size));
        float dy = MathF.Max(MathF.Max(TileY - p.Y, 0), p.Y - (TileY + Size));
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public bool ContainsTile(int x, int y) => x >= TileX && x < TileX + Size && y >= TileY && y < TileY + Size;
}

/// <summary>세력. 0 = 중립. (방향 변경 §0-1: 자원 · 인구 없음)</summary>
public sealed partial class Faction
{
    public readonly int Id;
    public string Name;              // 표시 이름 (기본 "세력 N")
    /// <summary>언어 코드 (en / ru / de ...): 보이스 재생용 표시 정보 — 시뮬레이션 · 체크섬과 무관. 지도 파일의 factions[].lang, 기본 en.</summary>
    public string Lang = "en";
    public bool IsAi;                // AI 가 조작하는 세력 (표시용 — 조작 경로는 사람과 같음)
    public bool RevealAll;           // [치트] 지도 전체를 본다 (show all maps)
    public bool Defeated;
    public bool Revealed;            // 대도시를 잃고 RevealDelay 가 지나 위치가 드러남
    public float NoHqTime;           // 대도시 없이 지난 시간
    /// <summary>정적 팀 (지도 파일 factions[].team, 0 = 팀 없음 = 모두와 적). 실행 중 불변 — WorldTeams.cs.</summary>
    public int Team;

    public Faction(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
