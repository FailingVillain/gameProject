using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 플레이어 지점 포격 (사용자 결정 2026-10-19, docs/TACTICAL_AI_STAGE3_PLAN.md): 곡사포는 평소 관측자 (장교 · 정찰병) 가 보는 적만 쏘지만,
/// 플레이어가 땅의 한 지점을 직접 지정하면 관측자 없이도 그 지점으로 계속 쏜다. AI 는 이 명령을 쓰지 않는다 (World.GroundFire 를 부르는 AI 코드 없음 — 테스트가 지킨다).
///  · 곡사포 (Indirect) 와 방열하는 직사 범위포 (보병포 · 야전포) 가 대상. 직사포는 포에서 지점까지 사선 (weapon.los) 이 있어야 하고, 곡사포는 사선이 필요 없다.
///  · 사거리 밖 · 최소 사거리 안인 포는 명령을 받지 않는다 (제자리 사격 — 포는 따라가지 않는다).
///  · 방열 사계 밖이면 작은 각은 방열한 채 포가 회전, 큰 각 (≥ traverseRedeployDeg) 은 철수 → 지점 쪽으로 회전 → 재방열 (공격 명령과 같음, N10), 그 뒤 쿨다운마다 사격. 탄약이 떨어지면 명령이 끝난다. 정지 · 다른 명령이 취소.
///  · 관측자 없이 쏘므로 퍼짐은 combat.groundScatterMul (기본 ×1.5) 배.
/// </summary>
/// <summary>
/// 포격 종류 (N79a, 사용자 승인 2026-10-20): Sustained = 지속 사격 (취소 · 탄약 소진까지, 기존 지점 포격), Prep = 준비 사격 (combat.prepRounds 발을 한 지점에 쏘고 끝),
/// Barrage = 이동탄막 (착탄선이 combat.barrageStepSec 초마다 barrageStepTiles 칸씩 Dir 방향으로 전진, 사거리 · 지도 끝 · barrageMaxTiles 를 넘으면 끝),
/// Smoke = 연막 (아직 없음 — 명령 종류만 있고 실행되지 않는다. 참호 돌파 묶음에서 구현). 탄약 한도는 모든 종류에 적용.
/// </summary>
public enum GroundFireKind : byte { Sustained = 0, Prep = 1, Barrage = 2, Smoke = 3 }

/// <summary>포격 종류를 지정한 지점 포격 (Kind != Sustained). Dir = 이동탄막 진행 방향 (0 이면 포 → 목표 방향). 리플레이 v16.</summary>
public sealed record GroundFireExCommand(int Faction, int[] Units, Vector2 Target, GroundFireKind Kind, Vector2 Dir) : GameCommand(Faction);

public sealed partial class Unit
{
    /// <summary>포격 종류 · 준비 사격 남은 발수 · 이동탄막 방향 · 시작 틱 (UnitOrder.GroundFire 일 때만 의미, N79a). 저장 · 체크섬.</summary>
    [SnapExt] internal GroundFireKind GroundKind;
    [SnapExt] internal int GroundRounds;
    [SnapExt] internal Vector2 GroundDir;
    [SnapExt] internal long GroundStart;

    /// <summary>지점 포격 목표 (UnitOrder.GroundFire 일 때만 의미 있음, 아니면 0).</summary>
    [SnapExt] internal Vector2 GroundPoint;
}

public sealed partial class World
{
    /// <summary>이 유닛이 지점 포격을 할 수 있는 종류인가: 곡사포, 또는 방열하는 직사 범위포.</summary>
    public static bool CanGroundFire(Unit u) => u.Weapon is { } w && (IsIndirect(w) || (u.CanDeploy && w.Splash > 0));

    /// <summary>[UI · 명령] u 가 point 에 지점 포격을 할 수 있는가 (종류 · 사거리 · 최소 사거리 · 직사포 사선).</summary>
    public bool CanGroundFireAt(Unit u, Vector2 point)
    {
        if (!u.Alive || !CanGroundFire(u) || !Map.InBounds((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y))) return false;
        var w = u.Weapon!;
        float d = Vector2.Distance(u.Pos, point);
        if (d > w.Range + 0.01f || d < w.MinRange) return false;
        if (!IsIndirect(w) && w.Los && !Map.HasFiringLine(u.Pos, point, u.Id, 0)) return false;
        return true;
    }

    /// <summary>지점 포격 명령 (플레이어 전용 — AI 는 부르지 않는다). 곡사포 · 방열 직사포만 골라 보낸다.</summary>
    public void GroundFire(int faction, IEnumerable<Unit> units, Vector2 point, GroundFireKind kind = GroundFireKind.Sustained, Vector2 dir = default)
    {
        if (kind == GroundFireKind.Smoke || !Enum.IsDefined(typeof(GroundFireKind), kind)) return;   // 연막탄 없음 (N79a — 참호 돌파 묶음에서)
        if (kind == GroundFireKind.Sustained) IssueUnits(faction, units.Where(CanGroundFire), ids => new GroundFireCommand(faction, ids, point));
        else IssueUnits(faction, units.Where(CanGroundFire), ids => new GroundFireExCommand(faction, ids, point, kind, dir));
    }

    /// <summary>이 유닛의 이번 틱 포격 조준점: 이동탄막이면 시작 지점에서 (경과 단계 × barrageStepTiles) 칸 진행 방향으로 전진한 지점, 아니면 GroundPoint.</summary>
    public Vector2 GroundAimPoint(Unit u)
    {
        if (u.GroundKind != GroundFireKind.Barrage) return u.GroundPoint;
        var c = Defs.CombatRules;
        long stepTicks = Math.Max(1, (long)MathF.Round(c.BarrageStepSec / Config.SimDt));
        long step = Math.Max(0, Tick - u.GroundStart) / stepTicks;
        return u.GroundPoint + u.GroundDir * (step * c.BarrageStepTiles);
    }

    void ExecGroundFire(int faction, List<Unit> list, Vector2 point, GroundFireKind kind, Vector2 dir)
    {
        if (kind == GroundFireKind.Smoke || !Enum.IsDefined(typeof(GroundFireKind), kind)) return;
        foreach (var u in list)
        {
            if (!CanGroundFireAt(u, point)) continue;
            ResetOrder(u);
            u.Order = UnitOrder.GroundFire;
            u.GroundPoint = point;
            u.GroundKind = kind;
            if (kind == GroundFireKind.Prep) u.GroundRounds = Math.Max(1, Defs.CombatRules.PrepRounds);
            if (kind == GroundFireKind.Barrage)
            {
                Vector2 d = float.IsFinite(dir.X) && float.IsFinite(dir.Y) && dir.LengthSquared() > 1e-6f ? dir : point - u.Pos;
                u.GroundDir = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : Vector2.UnitX;
                u.GroundStart = Tick;
            }
            u.LeashOrigin = u.Pos;
            Acknowledge(u);
        }
    }

    /// <summary>UnitOrder.GroundFire 한 틱: 사계 밖이면 철수 · 회전 · 재방열, 방열이 끝나면 조준 → 사격 (관측자 · 시야 불필요). 탄약이 없으면 명령 해제.</summary>
    void UpdateGroundFire(Unit u, float dt)
    {
        var w = u.Weapon;
        if (w == null || !CanGroundFire(u)) { ResetOrder(u); return; }
        Vector2 p = GroundAimPoint(u);
        Vector2 d = p - u.Pos;
        float dist = d.Length();
        if (u.GroundKind == GroundFireKind.Barrage && (!Map.InBounds((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y)) || Vector2.Distance(p, u.GroundPoint) > Defs.CombatRules.BarrageMaxTiles)) { ResetOrder(u); return; }   // 이동탄막 끝 (지도 끝 · 최대 전진)
        if (dist > w.Range + 0.01f || dist < w.MinRange) { ResetOrder(u); return; }   // (밀려서) 사거리를 벗어남 — 따라가지 않고 해제
        if (u.Target != null) u.Target = null;
        if (u.UsesAmmo && u.Ammo <= 0 && AmmoEnabled && !DebugInfiniteAmmo) { ResetOrder(u); return; }   // 탄약 소진
        float want = DetMath.Atan2(d.Y, d.X);

        // 방열 사계 밖 (N10): 옮길 각 < traverseRedeployDeg 면 방열한 채 포가를 돌리고, 그 이상이면 철수 → (이동 자세에서) 회전 → 재방열
        if (!CanTraverseTo(u, p))
        {
            if (CanTraverseInPlace(u, p)) { ShiftTraverse(u, p, dt); u.HoldLeft = 0; u.EngagedTick = Tick; return; }
            u.AimLeft = 0; u.AimTarget = 0;
            if (u.Deploy != DeployPhase.Packing) Stats.TraverseRepacks++;
            AdvancePack(u); return;
        }
        FaceToward(u, p, dt);
        u.HoldLeft = 0; u.EngagedTick = Tick;
        if (u.Deploy == DeployPhase.Packed && TraverseHalf(u) > 0 && MathF.Abs(DetMath.WrapPi(want - u.Facing)) > TraverseHalf(u)) { u.AimTarget = 0; u.AimLeft = 0; return; }
        if (!AdvanceDeploy(u, want)) { u.AimTarget = 0; u.AimLeft = 0; return; }
        // 조준 (AimTarget -1 = 지점 조준 중): 처음 한 번만 aimTime, 이후 쿨다운마다 연속 사격
        if (u.AimTarget != -1) { u.AimTarget = -1; u.AimLeft = w.AimTicks; }
        if (u.AimLeft > 0) u.AimLeft--;
        float diff = MathF.Abs(DetMath.WrapPi(want - (u.HasTurret ? u.TurretFacing : u.Facing)));
        if (u.AimLeft == 0 && u.Cooldown <= 0 && diff <= FireAngleTolerance)
        {
            if (!ConsumeAmmo(u)) { ResetOrder(u); return; }
            u.Cooldown = NextCooldown(u, w) * FireIntervalMul(u);
            FireAtGround(u, p, w, dist);
            if (u.GroundKind == GroundFireKind.Prep && --u.GroundRounds <= 0) ResetOrder(u);   // 준비 사격: 정해진 발수를 쏘면 끝
        }
    }

    /// <summary>지점 포격 1발: 관측자 없는 사격이라 퍼짐 × groundScatterMul. 곡사 · 직사포 모두 ScatterAt (N11 거리별 곡선; scatter 없는 직사포는 splash 기반) (사선은 명령 때 확인).</summary>
    void FireAtGround(Unit u, Vector2 p, WeaponDef w, float dist)
    {
        u.LastShotTick = Tick; u.RevealUntil = Tick + RevealTicks;
        MarkAlert(u.Owner, u.Pos);
        float frac = w.Range > 0 ? MathF.Min(1f, dist / w.Range) : 0f;
        float mul = Defs.CombatRules.GroundScatterMul;
        float maxErr = (w.Scatter > 0 ? ScatterAt(w, dist) : w.Splash * (0.3f + 0.7f * frac)) * mul;
        float err = maxErr * MathF.Sqrt(Roll(u.Id, 0x6701));
        float ang = Roll(u.Id, 0x6702) * DetMath.Tau;
        Vector2 impact = p + new Vector2(DetMath.Cos(ang) * err, DetMath.Sin(ang) * err);
        LaunchShell(u, u.Pos, impact, 0, w);
    }
}
