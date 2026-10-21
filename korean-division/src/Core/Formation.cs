using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>진형 종류. Free = 대형 해제 (밀집: 목표 둘레에 촘촘히). Box = 밀집 (정사각). Spread = 산개 (간격 SpreadFactor 배). Echelon* = 사선 (기병만, formations.json).</summary>
public enum FormType { Free = 0, Line = 1, Column = 2, Wedge = 3, Box = 4, Spread = 5, EchelonLeft = 6, EchelonRight = 7 }

/// <summary>병과 배치: Mixed = 병과 무시, ArmorFront = 기갑 앞 · 보병 뒤, InfantryFront = 보병 앞 · 기갑 뒤.</summary>
public enum FormOrder { Mixed = 0, ArmorFront = 1, InfantryFront = 2 }

/// <summary>
/// 명령에 실려 가는 진형 지정 (리플레이에 그대로 기록). Dir = 진행 방향 (단위 벡터 — 삼각함수 없이 결정적; 0 이면 시뮬이 무게중심→목표로 정한다).
/// Type/Order 는 FormType/FormOrder 의 정수값.
/// </summary>
public sealed record FormSpec(int Type, int Order, Vector2 Dir, float Width = 0)
{
    /// <summary>Width = 우클릭 드래그로 정한 전선 폭(칸). 0 = 대형 기본 폭. (옛 리플레이에는 없음 → 0)</summary>
    public FormType Kind => Enum.IsDefined(typeof(FormType), Type) ? (FormType)Type : FormType.Free;
    public FormOrder Ord => Enum.IsDefined(typeof(FormOrder), Order) ? (FormOrder)Order : FormOrder.Mixed;
    public bool IsFree => Kind == FormType.Free;
}

/// <summary>한 이동 그룹의 진형 배정 결과: 유닛 Id → 목표점 기준 슬롯 오프셋.</summary>
internal sealed class FormPlan
{
    public readonly Dictionary<int, Vector2> Off = new();
    public float Speed = float.MaxValue;   // 진형 이동 속도 = 가장 느린 구성원 (함께 도착)
    public float Facing;                   // 도착 후 바라볼 방향
    public Vector2 Dir;
    public float Extent;                   // 중심에서 가장 먼 슬롯까지 거리
}

/// <summary>슬롯 생성 + 결정적 배정. 순수 로직 (월드 상태는 읽기만).</summary>
internal static class Formations
{
    public const float SpreadFactor = 2.2f;

    /// <summary>진형 슬롯 (로컬 좌표: X = 오른쪽, Y = 앞). 평균이 원점이 되게 맞춘다.</summary>
    public static Vector2[] Slots(FormType type, int n, float s, float width = 0)
    {
        if (FormationRules.IsEchelon(type)) return FormationRules.SlotsFor(FormationRules.Default.CompanyShape(type), type, n, s, width);   // 사선 (기병, formations.json)
        var r = new Vector2[n];
        if (width > 0 && type is FormType.Line or FormType.Column or FormType.Box or FormType.Spread)
        {
            // 우클릭 드래그 폭: 열 수 = 폭 / 간격 + 1
            float sp = type == FormType.Spread ? s * SpreadFactor : s;
            Grid(r, Math.Clamp((int)MathF.Floor(width / sp + 1e-3f) + 1, 1, Math.Max(1, n)), sp);
            type = FormType.Free;   // 아래 switch 건너뜀
        }
        switch (type)
        {
            case FormType.Line: Grid(r, Math.Max(1, (int)MathF.Ceiling(n / (float)Math.Max(1, (int)MathF.Sqrt(n / 4f)))), s); break;   // 횡대: 넓고 얕게
            case FormType.Column: Grid(r, Math.Max(1, (int)MathF.Sqrt(n / 4f)), s); break;                                              // 종대: 좁고 깊게
            case FormType.Box: Grid(r, (int)MathF.Ceiling(MathF.Sqrt(n)), s); break;                                                  // 방진: 정사각
            case FormType.Spread: Grid(r, (int)MathF.Ceiling(MathF.Sqrt(n)), s * SpreadFactor); break;                                 // 산개: 정사각 격자, 간격 넓게
            case FormType.Wedge:
                for (int i = 0, k = 0; i < n; k++)
                    for (int j = 0; j <= k && i < n; j++, i++) r[i] = new Vector2((j - k / 2f) * s, -k * s * 0.87f);
                break;
            default: break;
        }
        Vector2 c = Vector2.Zero;
        foreach (var p in r) c += p;
        if (n > 0) c /= n;
        for (int i = 0; i < n; i++) r[i] -= c;
        return r;
    }

    static void Grid(Vector2[] r, int cols, float s)
    {
        int n = r.Length;
        for (int i = 0; i < n; i++)
        {
            int row = i / cols, col = i % cols, cnt = Math.Min(cols, n - row * cols);
            r[i] = new Vector2((col - (cnt - 1) / 2f) * s, -row * s);
        }
    }

    /// <summary>
    /// 진형 배정. center = 도착 중심 (Goal), 슬롯은 막힌 칸이면 shift 가 가장 가까운 통행 가능 위치로 옮긴다.
    /// from = 각 유닛이 슬롯으로 출발하는 위치 (null 이면 현재 위치).
    /// 방법: 병과 순서가 있으면 앞쪽 슬롯부터 해당 병과에 나눠 주고, 각 부분집합에서 (거리², 유닛 Id, 슬롯 번호) 순 탐욕 매칭 → 2-opt 교환 (거리² 합이 줄면 교환 — 경로 교차 제거).
    /// </summary>
    public static FormPlan Build(List<Unit> units, Vector2 center, FormSpec spec, Func<Unit, Vector2, Vector2> shift, Vector2[]? from = null, Vector2[]? localOverride = null)
    {
        int n = units.Count;
        var plan = new FormPlan();
        Vector2 cen = Vector2.Zero;
        for (int i = 0; i < n; i++) cen += from != null ? from[i] : units[i].Pos;
        cen /= Math.Max(1, n);
        Vector2 dir = spec.Dir;
        // 방향 지정이 없으면 무게중심 → 목표를 8방향으로 스냅 (아무 각도 = 사선 대형 문제, ROADMAP §2-4b 8차 ①)
        dir = dir.LengthSquared() < 1e-6f ? OrgLayout.Snap8(center - cen) : Vector2.Normalize(dir);
        Vector2 right = new(-dir.Y, dir.X);
        plan.Dir = dir;
        plan.Facing = DetMath.Atan2(dir.Y, dir.X);

        float maxR = 0.3f;
        foreach (var u in units) { maxR = MathF.Max(maxR, u.Radius); plan.Speed = MathF.Min(plan.Speed, u.Def.Speed); }
        float s = 2 * maxR + 0.35f;
        var local = localOverride != null && localOverride.Length == n ? localOverride : Slots(spec.Kind, n, s, spec.Width);   // 분대 대형 · 사선 = 호출 쪽이 만든 슬롯
        var world = new Vector2[n];
        for (int i = 0; i < n; i++) world[i] = center + right * local[i].X + dir * local[i].Y;

        // 앞쪽 슬롯 순서 (앞 = 로컬 Y 큰 쪽, 같으면 중앙에 가까운 쪽, 번호)
        var slotOrder = Enumerable.Range(0, n).OrderByDescending(i => MathF.Round(local[i].Y * 100f)).ThenBy(i => MathF.Abs(local[i].X)).ThenBy(i => i).ToList();
        var idx = Enumerable.Range(0, n).ToList();
        var assign = new int[n];   // 유닛 번호 → 슬롯 번호
        FormOrder ord = spec.Ord;
        if (ord == FormOrder.Mixed)
            Match(units, idx, slotOrder, world, from, assign);
        else
        {
            bool frontArmor = ord == FormOrder.ArmorFront;
            var a = idx.Where(i => units[i].Def.IsArmored == frontArmor).ToList();
            var b = idx.Where(i => units[i].Def.IsArmored != frontArmor).ToList();
            Match(units, a, slotOrder.Take(a.Count).ToList(), world, from, assign);
            Match(units, b, slotOrder.Skip(a.Count).ToList(), world, from, assign);
        }
        for (int i = 0; i < n; i++)
        {
            Vector2 p = shift(units[i], world[assign[i]]);
            Vector2 off = p - center;
            plan.Off[units[i].Id] = off;
            plan.Extent = MathF.Max(plan.Extent, off.Length());
        }
        return plan;
    }

    internal static void Match(List<Unit> units, List<int> us, List<int> slots, Vector2[] world, Vector2[]? from, int[] assign)
    {
        int m = us.Count;
        if (m == 0) return;
        Vector2 P(int ui) => from != null ? from[ui] : units[ui].Pos;
        var pairs = new List<(float D, int Id, int U, int S)>(m * m);
        foreach (int ui in us)
            foreach (int si in slots)
                pairs.Add((Vector2.DistanceSquared(P(ui), world[si]), units[ui].Id, ui, si));
        pairs.Sort((x, y) => { int c = x.D.CompareTo(y.D); if (c != 0) return c; c = x.Id.CompareTo(y.Id); return c != 0 ? c : x.S.CompareTo(y.S); });
        var usedU = new HashSet<int>(); var usedS = new HashSet<int>();
        foreach (var p in pairs)
        {
            if (usedU.Contains(p.U) || usedS.Contains(p.S)) continue;
            usedU.Add(p.U); usedS.Add(p.S); assign[p.U] = p.S;
            if (usedU.Count == m) break;
        }
        // 2-opt: 두 유닛의 슬롯을 맞바꿔 거리² 합이 줄면 교환
        for (int pass = 0; pass < 4; pass++)
        {
            bool any = false;
            for (int i = 0; i < m; i++)
            for (int j = i + 1; j < m; j++)
            {
                int a = us[i], b = us[j];
                float cur = Vector2.DistanceSquared(P(a), world[assign[a]]) + Vector2.DistanceSquared(P(b), world[assign[b]]);
                float swp = Vector2.DistanceSquared(P(a), world[assign[b]]) + Vector2.DistanceSquared(P(b), world[assign[a]]);
                if (swp < cur - 1e-4f) { (assign[a], assign[b]) = (assign[b], assign[a]); any = true; }
            }
            if (!any) break;
        }
    }
}
