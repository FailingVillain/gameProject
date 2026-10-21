namespace KoreanDivision.Core;

/// <summary>탄약 · 보급 마차 상태 (방향 변경 ROADMAP §0-1). 모두 틱 · 입력의 함수 — 결정적. 저장은 World.WriteSupplyExtImpl (SUPL 구역).</summary>
public sealed partial class Unit
{
    /// <summary>남은 탄약 (무기 ammo 가 0 이면 무한 — 값은 쓰이지 않음).</summary>
    [SnapExt] public int Ammo { get; internal set; }
    /// <summary>회복 중인 탄약의 1발 미만 누적분 (0..1).</summary>
    [SnapExt] internal float AmmoFrac;
    /// <summary>보급 마차 적재량 ('짐' 단위, 0..Def.Cargo). 보급 마차가 아니면 0.</summary>
    [SnapExt] public float Cargo { get; internal set; }
    /// <summary>마지막으로 공격받은 틱 (명중 무관 — 탄약 회복 대기 판정).</summary>
    [SnapExt] internal long LastFiredAtTick = -100_000;

    partial void InitSupply()
    {
        Ammo = Weapon?.Ammo ?? 0;
        Cargo = Def.Cargo;
        Grenades = Def.Grenades;
    }

    /// <summary>무기 탄약 칸 (0 = 무한 · 무기 없음).</summary>
    public int AmmoMax => Weapon?.Ammo ?? 0;
    /// <summary>탄약이 유한한 무기를 가진 유닛인가.</summary>
    public bool UsesAmmo => Weapon is { Ammo: > 0 };
    /// <summary>사격 가능한 탄약이 있는가 (무한 탄약 · 무기 없음도 true).</summary>
    public bool HasAmmo => !UsesAmmo || Ammo > 0;
    /// <summary>[표시] 탄약 비율 0..1 (무한이면 1).</summary>
    public float AmmoRatio => UsesAmmo ? Math.Clamp(Ammo / (float)AmmoMax, 0, 1) : 1f;
    /// <summary>[표시] 보급 마차 적재 비율 0..1.</summary>
    public float CargoRatio => Def.Cargo > 0 ? Math.Clamp(Cargo / Def.Cargo, 0, 1) : 0f;
}

public sealed partial class World
{
    /// <summary>교전 테스트에서도 탄약을 쓰는가 (BattleSetup.Ammo). 기본 false = 교전 테스트는 무한 탄약. 일반 게임에서는 의미 없음.</summary>
    [SnapExt] public bool BattleAmmo { get; internal set; }
    /// <summary>탄약을 쓰는 월드인가 (일반 게임, 또는 탄약 켠 교전 테스트).</summary>
    public bool AmmoEnabled => !BattleMode || BattleAmmo;
}
