using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// N61 기병 하마 사격 (사용자 승인): 기병 사격은 기본 하마 (말에서 내려 쏨), 승마 사격은 명중 크게 감소 (몽골 기병은 덜함 — UnitDef.mountedFireAccMul).
/// 사격하려고 멈춘 기병은 자동으로 내리고 (dismountSec), 돌격 · 이동 · 멀리 추격이면 다시 탄다 (mountSec). 내린 동안 = 보병 같은 표적
/// (고정 · 엎드림 가능, 이동 속도 ×dismountedSpeedMul). 전환 중에는 제자리 (사격 · 이동 없음). 말 지기 · 전용 그림은 아직 없음 (기병 그림 그대로).
/// 플레이어 명령 MountCommand: 하마 = 내리고 자동 복귀, 승마 = 타고 MountLock (자동 하마 안 함, 승마 사격).
/// 저장 CMB2 (WriteCombat3, FormatVersion 37) · 체크섬 HashCombat3.
/// </summary>
public sealed partial class Unit
{
    /// <summary>내린 상태 (전환이 끝난 뒤의 상태 — 전환 중이면 MountLeft > 0, 끝나면 뒤집힘).</summary>
    [SnapExt] internal bool Dismounted;
    /// <summary>하마 · 승마 전환 남은 틱 (0 = 전환 안 함).</summary>
    [SnapExt] internal int MountLeft;
    /// <summary>플레이어가 승마를 지정함 — 자동 하마 안 함.</summary>
    [SnapExt] internal bool MountLock;
    /// <summary>말 탄 채 사격하는 기병 (명중 감소 대상).</summary>
    internal bool MountedFirer => Def.Mounted && !Dismounted && !IsOfficer;
}

public sealed partial class World
{
    bool CanDismount(Unit u) => u.Def.Mounted && !u.IsOfficer && Defs.CombatRules.DismountSec > 0;

    /// <summary>하마/승마 전환 시작 (이미 그 상태이거나 전환 중이면 무시).</summary>
    internal void BeginMount(Unit u, bool toDismounted)
    {
        if (!CanDismount(u) || u.MountLeft > 0 || u.Dismounted == toDismounted) return;
        var c = Defs.CombatRules;
        if (toDismounted) Stats.Dismounts++; else Stats.Mounts++;
        u.MountLeft = Math.Max(1, (int)MathF.Round((toDismounted ? c.DismountSec : c.MountSec) / Config.SimDt));
    }

    /// <summary>전환 한 틱: 전환 중이면 true (제자리 — 표적 쪽으로 몸만). 돌격이 시작되면 하마 전환은 취소, 내린 채 돌격이면 승마 시작.</summary>
    bool StepMount(Unit u, float dt)
    {
        if (!u.Def.Mounted || u.IsOfficer) return false;
        if (u.Charging)
        {
            if (u.MountLeft > 0 && !u.Dismounted) u.MountLeft = 0;   // 하마 중 돌격 = 취소 (그대로 탄 채)
            else if (u.Dismounted && u.MountLeft == 0) BeginMount(u, false);
        }
        else if (u.Dismounted && u.MountLeft == 0 && (u.Order == UnitOrder.Move
                 || (u.Order is UnitOrder.AttackMove or UnitOrder.Attack && u.Target == null && u.Destination is Vector2 d && Vector2.DistanceSquared(u.Pos, d) > 4f)))
            BeginMount(u, false);   // 이동 명령 · 표적 없이 전진 = 타고 간다
        if (u.Def.Scout && !u.Dismounted && u.MountLeft == 0 && !u.MountLock && !u.Charging && u.Target == null && u.ScoutObserving) BeginMount(u, true);   // N72 하마 관측: 서서 관측 중인 기병 정찰병은 내려서 본다 (이동 명령이면 위 규칙대로 다시 탐)
        if (u.MountLeft <= 0) return false;
        if (--u.MountLeft == 0) u.Dismounted = !u.Dismounted;
        if ((u.Target?.Pos ?? u.Destination) is Vector2 look) FaceToward(u, look, dt);
        return true;
    }

    /// <summary>Engage 의 사거리 안 사격 직전: 말 탄 기병이 사격하려면 먼저 내린다. true = 아직 사격 못 함 (전환 시작).</summary>
    bool MustDismountToFire(Unit u)
    {
        if (u.Dismounted || !CanDismount(u) || u.MountLock || !Defs.CombatRules.AutoDismount || u.Charging) return false;
        BeginMount(u, true);
        return u.MountLeft > 0;
    }

    /// <summary>내린 기병이 표적이 한참 멀어 추격해야 하면 다시 탄다 (사거리 × mountRangeFrac 밖). true = 승마 시작.</summary>
    bool MountToChase(Unit u, float dist, float range)
    {
        if (!u.Dismounted || !CanDismount(u) || u.MountLock) return false;
        if (dist <= range * Defs.CombatRules.MountRangeFrac) return false;
        BeginMount(u, false);
        return u.MountLeft > 0;
    }

    /// <summary>하마 속도 배수 (UnitSpeed).</summary>
    float DismountedSpeed(Unit u) => u.Dismounted && u.Def.Mounted && !u.IsOfficer ? Defs.CombatRules.DismountedSpeedMul : 1f;

    /// <summary>승마 사격 명중 배수 (HitChance).</summary>
    float MountedFireMul(Unit u) => !u.MountedFirer ? 1f : u.Def.MountedFireAccMul > 0 ? u.Def.MountedFireAccMul : Defs.CombatRules.MountedFireAccMul;

    /// <summary>플레이어 명령 (MountCommand): mount = 타고 잠금, 아니면 내리고 자동 복귀.</summary>
    void ExecMount(Unit u, bool mount)
    {
        if (!CanDismount(u)) return;
        u.MountLock = mount;
        u.MountLeft = 0;
        BeginMount(u, !mount);
    }
}
