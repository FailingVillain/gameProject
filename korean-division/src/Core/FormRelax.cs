using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 대형 완화 (formation, 2026-10-21 사용자 결정 "대형 관련으로는 네 제안대로 한다", docs/ROADMAP_LONG.md 진행 순서 2). balance.json combat.formRelax = 전체 켜고 끄기.
///  · 짧은 이동 (무게중심 → 목표 shortMoveDist 칸 안, 방향이 지금 중대 방향 shortMoveFaceDeg 안) = 슬롯 재배정 없는 평행이동: 행군 · 도착 슬롯 = 지금 자기 자리 (블록 경계 + 구역 반경으로 자름), 깃발은 즉시 제 속도.
///  · 행군 오차 허용 relaxMoveTolerance (옛 formMoveTolerance 0.5) + 작은 오차 데드밴드 formDeadband (이 아래는 고치지 않음) — FollowAnchor.
///  · 구역 도착 formArriveZone: 도착 깃발 슬롯 이 반경 안이면 (진행 반대쪽으로는 formArriveSlack 까지만) 그 자리에서 도착 — FollowAnchor.
///  · 정확한 재정렬은 L 명령 (대열 정비) · 비접적 휴식 (restAlignSec 동안 선 중대, 접적 · 교전 · 틀 행동 아님 — 대열 정비 한 번) 때만.
///  · 종대 ↔ 횡대 히스테리시스: 복귀 문턱 narrowExitFrac (진입 narrowBlockFrac 보다 낮음) — CheckNarrow.
/// 상태 추가 없음 (휴식 판정 = 구성원 LastMoveTick, 이미 저장 · 체크섬).
/// </summary>
public sealed partial class World
{
    bool Relax => Defs.CombatRules.FormRelax;

    /// <summary>행군 중 무시할 슬롯 오차 (대형 완화 켬 = relaxMoveTolerance, 끔 = 옛 formMoveTolerance).</summary>
    float FormTolerance => Relax ? MathF.Max(Defs.CombatRules.FormMoveTolerance, Defs.CombatRules.RelaxMoveTolerance) : Defs.CombatRules.FormMoveTolerance;

    /// <summary>짧은 이동 평행이동 조건 (이어 걷기 · 제자리 방향 전환 · 선까지 전진이 아닐 때 StartCompanyMove 가 묻는다).</summary>
    internal bool ShortShiftOk(Company c, List<Unit> units, Vector2 cen, Vector2 goal, Vector2 dir)
    {
        var cr = Defs.CombatRules;
        if (!cr.FormRelax || cr.ShortMoveDist <= 0 || c.KeepFormation || c.Facing == Vector2.Zero || dir == Vector2.Zero) return false;
        if (Vector2.Distance(cen, goal) > cr.ShortMoveDist) return false;
        if (Vector2.Dot(Vector2.Normalize(c.Facing), Vector2.Normalize(dir)) < DetMath.Cos(cr.ShortMoveFaceDeg * MathF.PI / 180f)) return false;
        foreach (var u in units) if (u.Large || u.Def.TurnRate > 0) return false;   // 큰 유닛 · 느리게 도는 포는 보통 깃발 대형
        return true;
    }

    /// <summary>
    /// 평행이동 슬롯: 도착 방향 틀에서 지금 무게중심 기준 자기 자리 (블록 경계 ± 여유로 자름 — 멀리 흩어진 병사만 안으로), 행군 슬롯 = 같은 자리를 진행 방향 틀로.
    /// 자기 도착 자리까지 직선이 막히면 (강 · 다리 · 건물) false → 보통 깃발 대형.
    /// </summary>
    internal bool ShiftSlots(CompanyAnchor a, OrgLayout.Block b, List<Unit> units, Vector2 cen, Vector2 head, Vector2 dir, Vector2[] finals)
    {
        float pad = MathF.Max(b.Spacing, Defs.CombatRules.FormArriveZone);
        float x0 = b.MinX - pad, x1 = b.MaxX + pad, y0 = b.MinY - pad, y1 = b.MaxY + pad;
        Vector2 right = new(-head.Y, head.X), rightF = new(-dir.Y, dir.X);
        var loc = new Vector2[units.Count];
        float hw = 0;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            Vector2 off = u.Pos - cen;
            float fx = Math.Clamp(Vector2.Dot(off, rightF), x0, x1), fy = Math.Clamp(Vector2.Dot(off, dir), y0, y1);
            Vector2 wo = rightF * fx + dir * fy;
            loc[i] = new Vector2(Vector2.Dot(wo, right), Vector2.Dot(wo, head));
            hw = MathF.Max(hw, MathF.Abs(loc[i].X));
            finals[i] = ShiftSlot(u, a.Group.Goal + wo);
            if (!OpenLine(u.Pos, finals[i])) return false;
        }
        for (int i = 0; i < units.Count; i++) a.Slots[units[i].Id] = loc[i];
        a.HalfWidth = hw + b.Spacing * 0.5f;
        a.SpeedMul = 1f;   // 즉시 출발 (병사가 이미 자기 슬롯에 있다)
        Stats.ShortShifts++;
        return true;
    }

    /// <summary>
    /// 행군 중 작은 오차 보정 걸음 (FollowAnchor, 허용 오차 안): 깃발 속도 + (오차 − 데드밴드) × 비율. 데드밴드 안 오차는 고치지 않는다.
    /// </summary>
    Vector2 MarchCorrection(Vector2 err, float dt, float gain)
    {
        float db = Relax ? Defs.CombatRules.FormDeadband : 0f;
        if (db <= 0) return err * MathF.Min(1f, gain * dt);
        float e = err.Length();
        if (e <= db) return Vector2.Zero;
        return err * ((e - db) / e) * MathF.Min(1f, gain * dt);
    }

    /// <summary>
    /// 깃발이 서 있는 동안 (교전 정지 · 낙오 기다림, SpeedMul ≈ 0) 슬롯 오차가 작으면 그 자리에 선다 — 밀려난 만큼 슬롯으로 되걷다 다시 밀리는 비빔 (N33/N51) 방지.
    /// 작음 = 접적 중이면 행군 허용 오차 (구역), 아니면 데드밴드 × 2.
    /// </summary>
    bool StandInZone(Unit u, CompanyAnchor a, float lag, float tol)
    {
        if (!Relax || !Defs.CombatRules.FormStandHold || a.SpeedMul > 0.05f || u.Large || u.Def.TurnRate > 0 || (a.Column && a.ColumnSlots.ContainsKey(u.Id))) return false;
        if (u.LastMoveTick >= Tick - 1) return false;   // 히스테리시스: 걷던 병사는 슬롯까지 다 걷는다 (경계에서 서기 ↔ 걷기 깜빡임 방지) — 선 병사만 제자리
        float lim = ContactLoose(u.Company) ? tol : Defs.CombatRules.FormDeadband * 2f;
        return lag <= lim;
    }

    /// <summary>구역 도착 (FollowAnchor 도착 깃발): 슬롯 formArriveZone 안이고 진행 반대쪽 (슬롯 뒤) 으로 slack 넘게 처지지 않았으면 그 자리에서 도착.</summary>
    bool ArriveRelaxed(Unit u, Vector2 slot, float sd, float slack, Vector2 dir)
    {
        var cr = Defs.CombatRules;
        if (!cr.FormRelax || cr.FormArriveZone <= slack || sd > cr.FormArriveZone || u.HasTrenchSpot || u.Large || u.Def.TurnRate > 0) return false;
        if (dir != Vector2.Zero && Vector2.Dot(u.Pos - slot, dir) < -slack) return false;   // 뒤에서 오는 병사는 슬롯 앞뒤 여유까지 걷는다 (전진이 모자라게 끝나지 않게)
        Stats.ZoneArrivals++;
        return true;
    }

    /// <summary>지금 대열 폭: 병사 (장교 제외) 의 옆 축 위치 5~95 백분위 폭 (멀리 떨어진 몇 명은 무시). 2명 미만이면 0 (기본 모양).</summary>
    internal float HeldWidth(Company c)
    {
        Vector2 right = new(-c.Facing.Y, c.Facing.X);
        var xs = new List<float>();
        foreach (var u in c.Members) if (!u.IsOfficer) xs.Add(Vector2.Dot(u.Pos, right));
        if (xs.Count < 2) return 0;
        xs.Sort();
        int k = xs.Count / 20;
        return MathF.Max(0.1f, xs[xs.Count - 1 - k] - xs[k]);
    }

    /// <summary>
    /// 비접적 휴식 정렬 (StepCloseRanks 뒤): 서 있는 중대 (모든 구성원이 restAlignSec 동안 걷지 않음 — 그 시점에 한 번) 가 접적 · 교전 · 틀 행동 · 정비 중이 아니면
    /// 대열 정비 한 번 — 지금 대열 폭으로 짠 대형에서 max(closeSlack, gapMoveMin) 넘게 벗어난 병사만 (N12 "2.75칸 안 병사는 다시 걷지 않음" 유지),
    /// 엄폐 · 참호 · 최근 사격 병사 · 정찰병은 그대로.
    /// </summary>
    void StepRestAlign()
    {
        var cr = Defs.CombatRules;
        if (!cr.FormRelax || cr.RestAlignSec <= 0 || Companies.Count == 0) return;
        const int iv = 30;
        long rest = Math.Max(iv, (long)MathF.Round(cr.RestAlignSec / Config.SimDt));
        int combat = (int)MathF.Round(cr.GapCombatSec / Config.SimDt);
        for (int ci = 0; ci < Companies.Count; ci++)
        {
            var c = Companies[ci];
            if ((Tick + c.Id) % iv != 0) continue;
            if (c.Members.Count < 2 || c.Facing == Vector2.Zero || c.Marching || c.Drill.Phase != DrillPhase.Idle || ClosingRanks(c)) continue;
            if (ContactOf(c) != ContactState.None) continue;
            long last = 0;
            bool idle = true;
            foreach (var u in c.Members)
            {
                last = Math.Max(last, u.LastMoveTick);
                if (u.Group != null || u.Order is not (UnitOrder.Idle or UnitOrder.Hold) || u.Retreating || u.Charging) { idle = false; break; }
            }
            if (!idle) continue;
            long since = Tick - last;
            if (since < rest || since >= rest + iv) continue;   // 휴식이 문턱을 넘는 그 판정에서 한 번
            if (CompanyInCombat(c, combat) || !CanJudgeRanks(c)) continue;   // 정렬을 시킬 지휘관 · 장교 · 대리 부사관이 있어야 (장교 없는 중대는 빈자리 그대로 — N12)
            Stats.RestAligns++;
            CloseRanksPass(c, rest: true);
        }
    }
}
