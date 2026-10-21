using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// F1 2차 (2026-10-20 사용자 승인, docs/ISSUES_2026-10-20.md 4~6단계 — "라인배틀이 아니라 1차대전 전투").
///  · 4 회전 (F2): 중대 목표가 무게중심 rotateInPlaceDist 칸 안이고 방향이 rotateMinDeg 넘게 바뀌면 깃발이 행군하지 않는다 (대형을 통째로 돌리지 않음) —
///    새 방향의 슬롯 (구역 닻) 을 목표에 놓고 가까운 병사부터 다시 배정 (CompanyLayout.Match), 새 슬롯 rotateStayRadius 칸 안 (접적 중 = contactZoneRadius) 병사는
///    제자리에서 방향만 바꾼다 (도착 자리 = 지금 자리, 접적 중이면 구역 중심 = 새 슬롯). 그 밖의 병사만 새 슬롯으로 걷는다.
///  · 6 선까지 전진: 접적 · 이탈 중 (contactLoose) 이고 대형 유지 (Company.KeepFormation) 가 아닌 중대의 이동 · 공격 이동 = 지금 배치 (좌우 · 앞뒤) 그대로
///    목표 선으로 — 틀 · 대형 모양 강제 없음 (앞뒤는 looseDepth, 좌우는 대형 반폭 × 1.5 로 자름). 행진 사격 (MarchCommand) · 분대 약진 (전술 AI) 은 그대로 쓴다.
///  · 대형 유지 = SetKeepFormationCommand (플레이어 전투 탭, 리플레이 기록). 상태 Company.KeepFormation (ORG_ 접적 구역, FormatVersion 38).
/// </summary>
public sealed record SetKeepFormationCommand(int Faction, int[] Companies, bool On) : GameCommand(Faction);

internal enum F2Mode : byte { Formation, Rotate, Loose }

public sealed partial class World
{
    /// <summary>중대 대형 유지 켜고 끄기 (플레이어 명령). 자기 중대만.</summary>
    public void SetKeepFormation(int faction, IEnumerable<int> companies, bool on)
    {
        var ids = companies.Distinct().OrderBy(i => i).ToArray();
        if (ids.Length > 0) Issue(new SetKeepFormationCommand(faction, ids, on));
    }

    void ExecKeepFormation(int f, int[] companies, bool on)
    {
        foreach (int id in companies)
            if (OwnCompany(f, id) is { } c) c.KeepFormation = on;
    }

    /// <summary>새 중대 이동 명령의 방식 (이어 걷기가 아닐 때): 제자리 방향 전환 · 선까지 전진 · 보통 깃발 대형.</summary>
    internal F2Mode F2ModeOf(Company c, Vector2 cen, Vector2 goal, Vector2 dir)
    {
        var cr = Defs.CombatRules;
        if (cr.RotateInPlace && c.Facing != Vector2.Zero && dir != Vector2.Zero && Vector2.Distance(cen, goal) <= cr.RotateInPlaceDist
            && Vector2.Dot(Vector2.Normalize(c.Facing), Vector2.Normalize(dir)) < DetMath.Cos(cr.RotateMinDeg * MathF.PI / 180f))
            return F2Mode.Rotate;
        if (cr.LooseAdvance && !c.KeepFormation && ContactLoose(c) && Vector2.Distance(cen, goal) > cr.RotateStayRadius && OpenLine(cen, goal)) return F2Mode.Loose;
        return F2Mode.Formation;
    }

    /// <summary>무게중심 → 목표 직선 (1칸 간격 표본) 이 모두 걸을 수 있는 칸인가 — 아니면 (강 · 다리 · 건물 사이) 선까지 전진 대신 깃발 대형 (좁은 길 종대 처리).</summary>
    bool OpenLine(Vector2 a, Vector2 b)
    {
        float d = Vector2.Distance(a, b);
        int n = Math.Max(1, (int)MathF.Ceiling(d));
        for (int i = 0; i <= n; i++)
        {
            var (x, y) = GameMap.TileOf(Vector2.Lerp(a, b, i / (float)n));
            if (!Map.IsWalkable(x, y)) return false;
        }
        return true;
    }

    /// <summary>
    /// 선까지 전진 슬롯: 행군 슬롯 = 지금 무게중심 기준 자기 자리 (진행 방향 틀, 그대로 — 재배정 · 모양 바꿈 없음), 도착 자리 = 목표 + 지금 자리 (도착 방향 틀에서 자름).
    /// </summary>
    bool LooseSlots(CompanyAnchor a, OrgLayout.Block b, List<Unit> units, Vector2 cen, Vector2 head, Vector2 dir, Vector2[] finals)
    {
        var cr = Defs.CombatRules;
        float hw0 = 0;
        foreach (var l in b.Local) hw0 = MathF.Max(hw0, MathF.Abs(l.X));
        float maxX = MathF.Max(2f, hw0 * 1.5f), maxY = MathF.Max(0.5f, cr.LooseDepth);
        Vector2 right = new(-head.Y, head.X), rightF = new(-dir.Y, dir.X);
        float hw = 0;
        var loc = new Vector2[units.Count];
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            Vector2 off = u.Pos - cen;
            loc[i] = new Vector2(Math.Clamp(Vector2.Dot(off, right), -maxX, maxX), Math.Clamp(Vector2.Dot(off, head), -maxY, maxY));
            hw = MathF.Max(hw, MathF.Abs(loc[i].X));
            float fx = Math.Clamp(Vector2.Dot(off, rightF), -maxX, maxX), fy = Math.Clamp(Vector2.Dot(off, dir), -maxY, maxY);
            finals[i] = ShiftSlot(u, a.Group.Goal + rightF * fx + dir * fy);
            if (!OpenLine(u.Pos, finals[i])) return false;   // 자기 길이 막힘 (강 · 다리 · 건물) → 깃발 대형 (좁은 길 종대 처리)
        }
        for (int i = 0; i < units.Count; i++) a.Slots[units[i].Id] = loc[i];
        a.HalfWidth = hw + b.Spacing * 0.5f;
        Stats.LooseAdvances++;
        return true;
    }

    /// <summary>제자리 방향 전환 마무리: 깃발은 이미 도착, 새 슬롯 가까운 병사는 지금 자리를 도착 자리로 (방향만 바꿈). 접적 중이면 구역 중심 = 새 슬롯.</summary>
    void RotateStay(Company c, CompanyAnchor a, List<Unit> units, Vector2[] finals, int[] spots)
    {
        var cr = Defs.CombatRules;
        a.Arrived = true;
        a.Flag.Velocity = Vector2.Zero;
        bool contact = ContactLoose(c);
        float stay = contact ? MathF.Max(cr.RotateStayRadius, cr.ContactZoneRadius) : cr.RotateStayRadius;
        Stats.Rotations++;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (spots[i] >= 0 || u.Large || u.Def.TurnRate > 0) continue;   // 참호 자리 · 큰 유닛 · 느리게 도는 포는 그대로 슬롯으로
            if (Vector2.Distance(u.Pos, finals[i]) > stay) continue;
            a.FinalOff[u.Id] = u.Pos - a.Goal;
            u.ReactDelay = 0;
            if (contact) { u.HasZone = true; u.ZoneCenter = finals[i]; u.ZoneTick = Tick; }
            Stats.RotateStays++;
        }
    }
}
