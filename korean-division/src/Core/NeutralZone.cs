using System.Numerics;
using System.Text.Json.Serialization;
namespace KoreanDivision.Core;

// TC-G 1단계 (R9): 중립 구역 (일본 철도 부속지) = 플레이어 진입 금지 + 구역 안 표적 사격 금지.
// 구역 정의는 미션 JSON 의 "neutralZones": ["구역 이름", ...] (areas 의 이름) 뿐이다 — 미션 텍스트가 저장 · 리플레이에 그대로 실리므로
// 새 저장 상태가 없다 (FormatVersion · Replay 불변). 오폭 카운터 · D12 안전 반경 · 적 억류는 후속 (카드 TC-G).
public sealed partial class MissionDef
{
    /// <summary>중립 구역 이름 목록 (areas 키). 비어 있으면 기능 꺼짐 (기본).</summary>
    public List<string> NeutralZones { get; set; } = new();
    [JsonIgnore] MissionArea[]? neutralAreas;
    /// <summary>중립 구역 모양 (파싱 때 한 번 계산, 읽기 전용).</summary>
    [JsonIgnore] internal MissionArea[] NeutralAreas => neutralAreas ??= NeutralZones.Select(n => Areas[n]).ToArray();

    void ValidateNeutralZones()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in NeutralZones)
            Require(n != null && Areas.ContainsKey(n) && seen.Add(n), $"neutralZones '{n}': areas 에 없거나 중복");
    }
}

public sealed partial class World
{
    MissionArea[]? NeutralAreasOrNull => Mission is { } m && m.Def.NeutralZones.Count > 0 ? m.Def.NeutralAreas : null;

    /// <summary>[읽기] 점이 중립 구역 안인가 (미션 · 구역이 없으면 항상 false).</summary>
    internal bool InNeutralZone(Vector2 p)
    {
        if (NeutralAreasOrNull is not { } zs) return false;
        foreach (var z in zs) if (z.Contains(p)) return true;
        return false;
    }

    /// <summary>[읽기] 선분 a→b 가 중립 구역과 만나는가 (a 가 이미 구역 안이면 b 만 본다 — 나가는 이동은 허용).</summary>
    internal bool SegmentTouchesNeutral(Vector2 a, Vector2 b)
    {
        if (NeutralAreasOrNull is not { } zs) return false;
        foreach (var z in zs)
        {
            if (z.Contains(b)) return true;
            if (z.Contains(a)) continue;
            if (z.SegmentHits(a, b)) return true;
        }
        return false;
    }

    /// <summary>명령 실행 전 거절: 진입 금지 (미션 플레이어 세력의 이동류 · 돌격 · 진지 점유 · 대기열 이동) · 구역 안 표적 사격 · 지점 포격 · 수류탄.
    /// true = 명령을 버림 (알림 NotAllowed). 구역이 없으면 곧바로 false — 기본 설정에서 아무것도 바뀌지 않는다.</summary>
    bool NeutralZoneRejects(GameCommand command, int f)
    {
        if (NeutralAreasOrNull == null) return false;
        bool player = f == Mission!.Player;
        bool bad = command switch
        {
            MoveCommand c => player && EntersNeutral(f, c.Units, c.Target),
            AttackMoveCommand c => player && EntersNeutral(f, c.Units, c.Target),
            QuickStepCommand c => player && EntersNeutral(f, c.Units, c.Target),
            OccupyPositionsCommand c => player && EntersNeutral(f, c.Units, c.Target),
            ChargeCommand c => (player && EntersNeutral(f, c.Units, c.Target)) || (c.TargetId != 0 && Find(c.TargetId) is { } t && InNeutralZone(t.Pos)),
            QueueOrderCommand c => c.Kind switch
            {
                QueuedKind.Move or QueuedKind.AttackMove or QueuedKind.QuickStep or QueuedKind.Charge => player && EntersNeutral(f, c.Units, c.Target),
                QueuedKind.Attack => Find(c.TargetId) is { } t && InNeutralZone(t.Pos),
                _ => false,
            },
            RetreatCommand c => player && !(c.UseRally && c.Target == Vector2.Zero) && EntersNeutral(f, c.Units, c.Target),   // 집결지 후퇴 (목표 0) = 집결지는 지정 때 이미 검사
            SetRallyCommand c => player && !c.Clear && RallyEntersNeutral(f, c.Companies, c.Point),
            TrenchAssaultCommand c => player && c.On && !c.Ai && RallyEntersNeutral(f, c.Companies, c.Target),
            AttackCommand c => Find(c.TargetId) is { } t && InNeutralZone(t.Pos),
            GroundFireCommand c => InNeutralZone(c.Target),
            GroundFireExCommand c => InNeutralZone(c.Target),
            ThrowGrenadeCommand c => InNeutralZone(c.Target),
            _ => false,
        };
        if (bad) Notify(f, CommandResult.NotAllowed);
        return bad;
    }

    /// <summary>[읽기] 중대 단위 (집결지 지정 · 참호 돌격) 목표가 구역 안이거나, 중대원 무게중심 → 목표 직선이 구역에 닿는가.</summary>
    bool RallyEntersNeutral(int f, int[] companies, Vector2 target)
    {
        if (InNeutralZone(target)) return true;
        Vector2 sum = Vector2.Zero; int n = 0;
        foreach (int id in companies)
            if (OwnCompany(f, id) is { } co)
                foreach (var u in Units) if (u.Alive && u.Owner == f && u.Company == co) { sum += u.Pos; n++; }
        return n > 0 && SegmentTouchesNeutral(sum / n, target);
    }

    /// <summary>[읽기] 이 유닛들의 무게중심 → 목표 직선이 중립 구역에 닿는가 (닿으면 플레이어가 경유점으로 직접 돌아가야 한다).</summary>
    bool EntersNeutral(int f, int[] ids, Vector2 target)
    {
        if (InNeutralZone(target)) return true;
        Vector2 sum = Vector2.Zero; int n = 0;
        foreach (int id in ids)
            if (Find(id) is Unit u && u.Owner == f) { sum += u.Pos; n++; }
        return n > 0 && SegmentTouchesNeutral(sum / n, target);
    }
}

public partial class MissionArea
{
    /// <summary>선분 a→b 가 구역과 겹치는가 (사각형 = Liang-Barsky, 원 = 선분 위 최근접점; 곱셈 · 나눗셈 · 비교뿐 — 결정적).</summary>
    public bool SegmentHits(Vector2 a, Vector2 b)
    {
        if (Rect is { Length: 4 } r)
        {
            float t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
            return Clip(-dx, a.X - r[0], ref t0, ref t1) && Clip(dx, r[0] + r[2] - a.X, ref t0, ref t1)
                && Clip(-dy, a.Y - r[1], ref t0, ref t1) && Clip(dy, r[1] + r[3] - a.Y, ref t0, ref t1);
        }
        if (Circle is { Length: 3 } c)
        {
            var d = b - a; var ctr = new Vector2(c[0], c[1]);
            float l2 = d.LengthSquared();
            float t = l2 < 1e-9f ? 0 : Math.Clamp(Vector2.Dot(ctr - a, d) / l2, 0f, 1f);
            return Vector2.DistanceSquared(a + d * t, ctr) <= c[2] * c[2];
        }
        return false;
    }

    static bool Clip(float p, float q, ref float t0, ref float t1)
    {
        if (p == 0) return q >= 0;
        float t = q / p;
        if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
        else { if (t < t0) return false; if (t < t1) t1 = t; }
        return true;
    }
}
