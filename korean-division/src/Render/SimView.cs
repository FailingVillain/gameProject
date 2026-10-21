using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 렌더러가 읽는 시뮬레이션 값의 한 창구 (읽기 전용). 시뮬레이션 쪽 필드가 아직 없을 때는 '없음'을 돌려주고,
/// 필드가 생기면 이 파일의 본문만 바꾼다 — 나머지 렌더 코드는 그대로.
/// </summary>
internal static class SimView
{
    /// <summary>시뮬레이션이 정하는 포탑 방향 (라디안). 값이 없으면 false → 화면 쪽 부드러운 추종을 쓴다.</summary>
    public static bool TurretFacing(Unit u, out float facing) { facing = u.TurretFacing; return u.HasTurret; }

    /// <summary>탄약 비율 0..1 (무한 · 해당 없음 이면 false).</summary>
    public static bool AmmoRatio(Unit u, out float ratio) { ratio = u.AmmoRatio; return u.UsesAmmo; }

    /// <summary>보급 마차 적재 비율 0..1 (보급 마차가 아니면 false).</summary>
    public static bool CargoRatio(Unit u, out float ratio) { ratio = u.CargoRatio; return u.Def.IsSupplyWagon; }

    /// <summary>이 유닛 주변이 경계 상태인가 (선택 표시용).</summary>
    public static bool Alerted(World world, Unit u) => world.IsAlerted(u);

    /// <summary>견인포가 전개 · 철수 중인가 (Unit.Deploy 단계).</summary>
    public static bool Deploying(Unit u) => u.CanDeploy && (u.Deploy == DeployPhase.Deploying || u.Deploy == DeployPhase.Packing);

    /// <summary>견인포가 이동 자세(포를 매단 채)인가.</summary>
    public static bool Limbered(Unit u) => u.CanDeploy && u.Deploy == DeployPhase.Packed;

    /// <summary>은폐 표시를 낼 유닛인가 (은폐 계수 > 0).</summary>
    public static bool Concealing(Unit u) => u.Def.Concealment > 0;
}
