using System.Numerics;
namespace KoreanDivision.Core;

/// <summary>미사격 진단 프로브 보조 (읽기 전용 — Tests/NoFireProbe.cs). 상태를 바꾸지 않는다.</summary>
public sealed partial class World
{
    internal bool IsIndirectForProbe(WeaponDef w) => IsIndirect(w);

    internal string ProbeReason(Unit u, Unit foe, float dist)
    {
        var c = Defs.CombatRules;
        if (!u.HasAmmo) return "01 탄약 0";
        if (u.Charging) return "02 돌격 중 (사격 안 함)";
        if (u.Retreating) return "03 후퇴 중";
        if (u.ReactDelay > 0) return u.Suppression >= c.HesitateMinSuppress ? "04 멍칫 (제압 >= 기준)" : "04b 명령 직후 반응 지연";
        bool moving = u.Velocity != Vector2.Zero || Tick - u.LastMoveTick <= 1;
        if (u.Order == UnitOrder.Move) return moving ? "06 이동 명령 중 걷기" : "06b 이동 명령 대기";
        if (u.Order == UnitOrder.AttackMove && MarchAdvancing(u)) return "07 돌격 사격 전진 (MarchAdvancing)";
        if (CloseWalking(u)) return "08 대열 정비 걷기 (CloseWalking)";
        if (u.Order == UnitOrder.AttackMove && moving && u.Target == null) return "09 공격 이동 걷는 중 (표적 없음)";
        float react = ReactRange(u);
        bool beyond = dist > react;
        if (u.Target == null)
            return beyond ? (UnderFire(u) ? "10 반응거리 밖, 피격 중인데 표적 없음" : "10b 반응 거리 밖 (0.7 x 사거리) 표적 없음")
                          : (u.Order == UnitOrder.Idle && u.Def.IsOfficer ? "11 장교 수동 (표적 없음)" : "11 반응 거리 안인데 표적 없음 (획득 지연 / 필터)");
        float td = u.Target.EdgeDistance(u.Pos) - u.Radius;
        if (td > u.Weapon!.Range) return "12 표적이 사거리 밖 (다른 표적 추격 / 선택)";
        if (u.Target != foe && td < dist + 0.01f) { }
        if (u.CanDeploy && u.Deploy != DeployPhase.Deployed) return "18 방열/철수 중 (대포 · 기관총)";
        if (u.AimLeft > 0) return "13 조준 중 (AimLeft)";
        if (u.Cooldown > 0) return u.Suppression > 0 ? "14 재장전 (제압 배수 포함)" : "14 재장전/쿨다운";
        if (!CanTraverseTo(u, u.Target.Pos)) return "15 사계 밖 (포가)";
        if (u.HoldLeft > 0) return "16 HoldWait";
        return moving ? "17 기타 (이동 중)" : "17b 기타 (서 있음, 표적 있음)";
    }
}
