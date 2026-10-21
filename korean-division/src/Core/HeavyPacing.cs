using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 중화기는 보병보다 앞으로 나가지 않는다 (사용자 규칙 2026-10-19, N4): 경기관총 · 맥심 · 대전차소총 · 보병포 · 야전포 · 곡사포 (UnitDef.Heavy).
/// 매 틱 (유닛 갱신 전) 진행 방향과 보병 앞선을 정한다 — 중대: 깃발 행군 중이면 대형 정면 (도착 방향), 틀 행동 (교전) 중이면 적 쪽; 보병이 없는 중화기 중대는 대대의 보병 앞선;
/// 중대 밖 섞인 이동 묶음 (보병 + 중화기): 보병 무게중심 → 목표. 중화기가 이번 틱 진행 방향으로 나아가 앞선 + heavyLeadTol 을 넘으면 이번 틱 이동을 되돌린다 (기다림).
/// 앞으로 나가지 않는 이동 (옆 · 뒤 — 보병이 참호 · 사수 중일 때 옆으로 다시 거치하는 것 포함) 은 막지 않는다. 기준값은 틱마다 다시 계산 (저장 안 함), 결정적 (중대 목록 · 유닛 목록 순).
/// </summary>
public sealed partial class World
{
    /// <summary>앞선 허용 여유 (칸).</summary>
    public const float HeavyLeadTol = 1f;
    readonly Dictionary<int, (Vector2 Dir, float Front)> heavyCompany = new();
    readonly Dictionary<MoveGroup, (Vector2 Dir, float Front)> heavyGroup = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<MoveGroup, (Vector2 Sum, int N, bool Heavy)> heavyScan = new(ReferenceEqualityComparer.Instance);

    static bool PaceInfantry(Unit u) => u.Def.IsInfantry && !u.Def.Heavy && !u.IsOfficer && !u.Def.Scout;

    void StepHeavyPacing()
    {
        heavyCompany.Clear(); heavyGroup.Clear();
        bool anyHeavy = false, anyOfficer = false;
        foreach (var u in Units) { if (!u.Alive) continue; if (u.Def.Heavy) anyHeavy = true; else if (u.IsOfficer && u.Company != null) anyOfficer = true; if (anyHeavy && anyOfficer) break; }
        if (!anyHeavy && !anyOfficer) return;
        foreach (var c in Companies)
        {
            bool heavy = false;
            foreach (var u in c.Members) if (u.Def.Heavy || u.IsOfficer) { heavy = true; break; }   // N12: 장교도 같은 앞선 기준 (OfficerPace)
            if (!heavy) continue;
            Vector2 dir = Vector2.Zero;
            if (c.Anchor is { } a) dir = a.FinalDir.LengthSquared() > 0.5f ? a.FinalDir : a.Heading;   // 대형 정면 (대대 이동은 중대 모두 같은 방향) — 도착 뒤 슬롯으로 걷는 동안도
            else if (c.Drill.Phase == DrillPhase.Engage && c.Drill.Enemy != Vector2.Zero)
            {
                var d = c.Drill.Enemy - Centroid(c.Members);
                if (d.LengthSquared() > 1e-3f) dir = Vector2.Normalize(d);
            }
            if (dir == Vector2.Zero) continue;
            float front = float.MinValue;
            foreach (var u in c.Members) if (PaceInfantry(u)) front = MathF.Max(front, Vector2.Dot(u.Pos, dir));
            if (front == float.MinValue && c.Battalion != null)
                foreach (var o in c.Battalion.Companies)
                    foreach (var u in o.Members) if (PaceInfantry(u)) front = MathF.Max(front, Vector2.Dot(u.Pos, dir));
            if (front != float.MinValue) heavyCompany[c.Id] = (dir, front);
        }
        if (!anyHeavy) return;
        // 중대 밖 (또는 중대 일부만 고른) 섞인 이동 묶음
        heavyScan.Clear();
        foreach (var u in Units)
        {
            if (!u.Alive || u.Group is not { } g || (u.Company?.Anchor is { } fa && ReferenceEquals(fa.Group, g))) continue;
            heavyScan.TryGetValue(g, out var v);
            if (PaceInfantry(u)) { v.Sum += u.Pos; v.N++; }
            if (u.Def.Heavy) v.Heavy = true;
            heavyScan[g] = v;
        }
        foreach (var u in Units)
        {
            if (!u.Alive || !u.Def.Heavy || u.Group is not { } g || heavyGroup.ContainsKey(g) || !heavyScan.TryGetValue(g, out var v) || v.N == 0 || !v.Heavy) continue;
            var dv = g.Goal - v.Sum / v.N;
            if (dv.LengthSquared() < 1f) continue;
            var dir = Vector2.Normalize(dv);
            float front = float.MinValue;
            foreach (var o in Units) if (o.Alive && ReferenceEquals(o.Group, g) && PaceInfantry(o)) front = MathF.Max(front, Vector2.Dot(o.Pos, dir));
            heavyGroup[g] = (dir, front);
        }
    }

    /// <summary>이번 틱 진행 방향으로 나아가 보병 앞선 + 여유를 넘었으면 이동을 되돌린다.</summary>
    void PaceHeavy(Unit u, Vector2 p0)
    {
        (Vector2 Dir, float Front) k;
        if (u.Company != null && heavyCompany.TryGetValue(u.Company.Id, out var kc) && (u.Company.Anchor == null || ReferenceEquals(u.Group, u.Company.Anchor.Group) || u.Group == null || !heavyGroup.ContainsKey(u.Group))) k = kc;
        else if (u.Group != null && heavyGroup.TryGetValue(u.Group, out var kg)) k = kg;
        else return;
        float gained = Vector2.Dot(u.Pos - p0, k.Dir);
        if (gained <= 0 || Vector2.Dot(u.Pos, k.Dir) <= k.Front + HeavyLeadTol) return;
        u.Pos = p0;
        u.Velocity = Vector2.Zero;
        Stats.HeavyHeld++;
    }

    /// <summary>
    /// N12 장교 위치: 돌격 중이 아닌 중대 장교는 이번 틱 진행 방향으로 나아가 중대 보병 앞선 + officerLeadTol 을 넘으면 이동을 되돌린다 (표적을 쫓아 혼자 앞으로 나서지 않음).
    /// 돌격 (자동 · 명령 · 독전 — 앞장서도 됨) 과 명시적 공격 명령 (Order Attack — 플레이어가 고른 표적) 은 예외. 기준 = 중화기 보폭과 같은 중대 앞선 (StepHeavyPacing).
    /// </summary>
    void PaceOfficer(Unit u, Vector2 p0)
    {
        if (u.Charging) { PaceOfficerCharge(u, p0); return; }   // N35: 돌격 중에도 돌격하는 병사 앞줄을 넘지 않음 (OfficerConduct.cs)
        if (u.Order == UnitOrder.Attack || u.Company == null) return;
        if (!heavyCompany.TryGetValue(u.Company.Id, out var k)) return;
        float gained = Vector2.Dot(u.Pos - p0, k.Dir);
        if (gained <= 0 || Vector2.Dot(u.Pos, k.Dir) <= k.Front + Defs.CombatRules.OfficerLeadTol) return;
        u.Pos = p0;
        u.Velocity = Vector2.Zero;
        Stats.OfficerHeld++;
    }
}
