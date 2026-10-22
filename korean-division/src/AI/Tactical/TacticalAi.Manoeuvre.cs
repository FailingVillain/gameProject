using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 3단계 A (stage 3A, docs/TACTICAL_AI_STAGE3_REVIEW_2026-10-19.md S1 · S14) — 프로필 strategy.manoeuvre · objectives 뒤.
///  · S1 기동 계획 (연대, 접촉 때 한 번 · 접촉을 잃으면 다시): 정면 · 측면 (고정 제대 + 측면 제대) · 협공 (두 방향 수렴) · 중점 (좁은 정면 집중) 을
///    지형 (적 엄폐 · 경로 통행 · 숲) · 보이는 적 위치 (정면 폭 · 끝) · 기관총 · 병력비 · 미션 목표로 점수 매겨 고른다. 동점 = 고정 순서 (무작위 없음).
///    측면 · 협공: 바깥 중대들 = 기동 제대 (경유점 → 돌격 진지, 이동 중 교전 안 함), 나머지 = 고정 제대 (사격만, 돌격 금지).
///    동기: 고정 제대 교전 (FixTick) + 기동 제대 도착 (ArriveTick) → 돌격 허가 (ReleaseTick). 고정 제대는 기동 제대 도착 예정 fixLead 초 전까지 적 사거리 밖에서 대기한 뒤 교전 (혼자 오래 맞지 않게).
///    우회 상한 (사용자 결정 4): 경로 ≤ 정면 거리 × maxDetour, 도착 예정 ≤ maxArrive 초 — 넘으면 그 후보는 없음. 기한 (maxArrive × 1.25 + 10초) 이 지나면 그대로 풀린다.
///  · S14 미션 목표 → 과제 (ObjectiveFor): capture · defend · escort · eliminate 를 세력 기준 점령 · 고수 · 호송 · 차단 · 격멸로. 미션 상태의 순수 함수 (저장 없음).
///  · 철수 (사용자 결정 5): 미션 aiWithdrawAt · aiCommitment 가 프로필 withdrawAt · commitment 를 덮어쓴다 (Strategy.cs).
/// </summary>
public sealed partial class TacticalAi
{
    /// <summary>PlanLog 한 줄의 int 수: 연대 Id · 종류 · 측면 쪽 · 계획 틱 · 고정 교전 틱 · 도착 틱 · 풀림 틱 · 우회 ×10 · 정면 거리 ×10 · 도착 예정 초 ×10 · 끝 사유 (0 풀림 · 1 취소 · 2 정면/중점 결정 · 3 과제 바뀜) · 병력비 ×100 · 보인 적 수.</summary>
    public const int PlanLogStride = 13;

    [NonSerialized] readonly List<TacGroup> planCos = new(), planMan = new(), planFix = new();
    [NonSerialized] readonly List<Unit> planEnemies = new();

    /// <summary>[측정 · 디버그 전용, 시뮬 무관] 계획 후보 점수 한 줄 (TacticsObs KD_TAC_DBG=1 이 켠다).</summary>
    public static Action<string>? PlanTrace;

    /// <summary>미션 목표에서 끌어낸 과제 (Point = 목표 지점, Aux = 호송 목적지, Radius = 구역 반경).</summary>
    public struct AiObjective
    {
        public AiObjectiveKind Kind;
        public Vector2 Point, Aux;
        public float Radius, Seconds;
        public int Index;
    }

    // ================================================================ S14 미션 목표 → 과제

    /// <summary>
    /// 세력 f 의 과제: 진행 중 · 공개된 목표를 주 목표 먼저, 목록 순서로 보고 공간 과제 (점령 · 고수 · 호송 · 차단) 가 처음 나오는 것.
    /// capture: 우리 쪽 = 점령, 적 쪽 = 고수 · defend (구역): 우리 = 고수, 적 = 점령 (구역 없으면 적 = 격멸) · escort: 우리 = 호송 (태그 유닛 중심 → 목적지), 적 = 차단 (보이는 태그 유닛, 없으면 목적지)
    /// · eliminate: 행위자 (플레이어 쪽) = 격멸 (공간 과제 없음). 없으면 None.
    /// </summary>
    public static AiObjective ObjectiveFor(World w, int f)
    {
        var none = new AiObjective { Index = -1 };
        if (w.Mission is not { } m) return none;
        var def = m.Def;
        AiObjective destroy = none;
        for (int pass = 0; pass < 2; pass++)
        for (int i = 0; i < def.Objectives.Count; i++)
        {
            var o = def.Objectives[i];
            if (o.Primary != (pass == 0)) continue;
            if (i < m.ObjState.Length && m.ObjState[i] != MissionState.Active) continue;
            if (i < m.ObjRevealed.Length && !m.ObjRevealed[i]) continue;
            var a = def.Area(o.Area);
            int side = o.Faction > 0 ? o.Faction : m.Player;
            bool mine = side == f || w.Allied(f, side), hostile = !mine && w.IsHostile(f, side);
            var r = new AiObjective { Index = i, Seconds = o.Seconds };
            if (o.Train != null && o.Type is "escort" or "capture")   // T30c 화물열차: 기존 호송 · 점령 과제 재사용, 기준점 = 화물 객차 중심 (적 쪽은 보이는 것만)
            {
                var fc = w.FreightCentroid(o.Train, mine ? 0 : f);
                if (o.Type == "escort" && a != null)
                {
                    r.Aux = a.Center; r.Radius = a.Radius;
                    if (mine) { r.Kind = fc != null ? AiObjectiveKind.Escort : AiObjectiveKind.None; r.Point = fc ?? a.Center; }
                    else if (hostile) { r.Kind = AiObjectiveKind.Intercept; r.Point = fc ?? a.Center; }
                }
                else if (o.Type == "capture" && fc is Vector2 cp)
                {
                    r.Point = cp; r.Radius = o.Radius;
                    r.Kind = mine ? AiObjectiveKind.Take : hostile ? AiObjectiveKind.Hold : AiObjectiveKind.None;
                }
                if (r.Kind == AiObjectiveKind.None) continue;
                return r;
            }
            switch (o.Type)
            {
                case "capture":
                    if (a == null) break;
                    r.Point = a.Center; r.Radius = a.Radius;
                    r.Kind = mine ? AiObjectiveKind.Take : hostile ? AiObjectiveKind.Hold : AiObjectiveKind.None;
                    break;
                case "defend":
                    if (a != null) { r.Point = a.Center; r.Radius = a.Radius; r.Kind = mine ? AiObjectiveKind.Hold : hostile ? AiObjectiveKind.Take : AiObjectiveKind.None; }
                    else if (hostile) r.Kind = AiObjectiveKind.Destroy;
                    break;
                case "escort":
                {
                    if (a == null) break;
                    Vector2 sum = Vector2.Zero; int n = 0;
                    foreach (int id in m.TagUnits(o.Tag))
                        if (w.Find(id) is Unit u && u.Alive && (mine || w.Fog.CanSee(f, u))) { sum += u.Pos; n++; }   // 적 호송대는 보이는 것만
                    r.Aux = a.Center; r.Radius = a.Radius;
                    if (mine) { r.Kind = n > 0 ? AiObjectiveKind.Escort : AiObjectiveKind.None; r.Point = n > 0 ? sum / n : a.Center; }
                    else if (hostile) { r.Kind = AiObjectiveKind.Intercept; r.Point = n > 0 ? sum / n : a.Center; }
                    break;
                }
                case "eliminate":
                {
                    int actor = m.Player;
                    if (actor == f || w.Allied(f, actor)) r.Kind = AiObjectiveKind.Destroy;
                    break;
                }
            }
            if (r.Kind == AiObjectiveKind.None) continue;
            if (r.Kind == AiObjectiveKind.Destroy) { if (destroy.Index < 0) destroy = r; continue; }
            return r;
        }
        return destroy;
    }

    // ================================================================ S1 기동 계획 (연대)

    /// <summary>RegimentThink (공격 과제) 에서: 접촉하면 계획을 고르고, 진행 중이면 고정 · 기동 제대를 맞춘다. 꺼져 있으면 아무것도 안 함.</summary>
    void ManoeuvreStep(World w, TacFaction s, TacRegiment r, List<TacBattalion> fronts, Unit? e, float de, Vector2 dir, Vector2 side, float R)
    {
        var S = s.Profile.Strategy;
        if (!S.Manoeuvre) { if (r.Plan != ManPlan.None) EndPlan(w, s, r, 3); return; }
        if (r.Plan == ManPlan.None)
        {
            // 접촉 전 계획 (prePlan): 미션 목표 · 공격 구역이 있으면 그곳을 적 진지로 보고 측면 · 협공만 미리 정한다 (접촉하면 실제 적 그림으로 고침)
            if ((e == null || de >= R * 2f) && S.PrePlan && !r.PlanPreTried && r.Phase == RegPhase.Approach && w.Tick - s.SetupTick > Sec(2) && PreTarget(w, s, r) is (Vector2 pc, float pr))
            {
                float dt = Vector2.Distance(r.Center, pc);
                if (dt <= R * 4f && dt >= R * 1.5f) { r.PlanPreTried = true; ChoosePlan(w, s, r, fronts, null, dir, side, R, pc, pr); if (r.Plan != ManPlan.None) return; }
            }
            if (e == null || de >= R * 2f) { r.PlanTick = 0; return; }
            // 첫 접촉 뒤: 적 주 진지가 보일 만큼 (사거리 ×1.3 안) 다가가 3초, 또는 20초 — 전초만 보고 정면 끝 · 병력비를 잘못 잡지 않게
            if (r.PlanTick == 0) { r.PlanTick = w.Tick; return; }
            long since = w.Tick - r.PlanTick;
            if (!(since >= Sec(3) && de <= R * 1.3f) && since < Sec(20)) return;
            ChoosePlan(w, s, r, fronts, e, dir, side, R, default, 0);
            return;
        }
        if (r.PlanStage == 1 && r.Plan is ManPlan.Flank or ManPlan.Pincer)
        {
            if (r.PlanPre && e != null && de <= R * 1.3f) RefinePlan(w, s, r, e, dir, side, R);   // 접촉: 미리 정한 계획을 실제 적 정면 끝에 맞춘다
            SyncPlan(w, s, r, R);
        }
    }

    /// <summary>접촉 전 계획의 기준: 미션 점령 · 차단 목표 (세력 기본 그룹) 또는 공격 구역 (중심, 반경). 없으면 null.</summary>
    (Vector2, float)? PreTarget(World w, TacFaction s, TacRegiment r)
    {
        var S = s.Profile.Strategy;
        var d = s.Directives.Find(x => x.Id == r.Directive);
        if (d == null || d.Effective != AiPosture.Attack || d.Withdrawn || d.Culminated) return null;
        if (S.Objectives && d.Id == 0 && ObjectiveFor(w, s.Faction) is { Kind: AiObjectiveKind.Take or AiObjectiveKind.Intercept } o) return (o.Point, MathF.Max(8f, o.Radius));
        if (d.HasArea && d.AreaRadius > 0) return (d.Area, d.AreaRadius);
        return null;
    }

    /// <summary>적 그림 (planEnemies 에 보이는 적): 중심 · 정면 양 끝 (side 축) · 평균 엄폐 · 고지 비율 · 기관총 수 · 병력비.</summary>
    struct Picture { public Vector2 Ec; public float Lo, Hi, Cov, CovN, HiE, Odds; public int Ne, Mg; }

    Picture SeePicture(World w, Unit e, Vector2 side, float own)
    {
        var m = w.Map;
        var ens = planEnemies; ens.Clear();
        float rad = 45f * K;
        foreach (var u in enemies)
            if (u.Weapon != null && !u.Def.IsArmored && Vector2.DistanceSquared(u.Pos, e.Pos) <= rad * rad) ens.Add(u);
        if (ens.Count == 0) ens.Add(e);
        var p = new Picture { Lo = float.MaxValue, Hi = float.MinValue };
        foreach (var u in ens)
        {
            p.Ec += u.Pos;
            float l = Vector2.Dot(u.Pos, side);
            p.Lo = MathF.Min(p.Lo, l); p.Hi = MathF.Max(p.Hi, l);
            p.Cov += MathF.Max(0, TacMap.TileCover(m, u.Pos));
            if (m.IsHigh(u.Pos)) p.HiE++;
            if (IsMg(u)) p.Mg++;
        }
        p.Ne = ens.Count;
        p.Ec /= p.Ne; p.Cov /= p.Ne; p.HiE /= p.Ne;
        p.CovN = Math.Clamp(p.Cov / 0.8f, 0f, 1f);
        p.Odds = own / MathF.Max(MathF.Max(1f, p.Ne), own / 3f);   // 일부만 보일 때 부풀지 않게 3배에서 자름
        return p;
    }

    /// <summary>접촉 전 그림: 구역 (중심 c, 반경 rad) 을 적 진지로 — 정면 = 반경 ×0.8, 엄폐 = 구역 칸 중 좋은 엄폐 칸 (참호 · 숲 · 가옥) 비율, 병력비 = 모름 (2).</summary>
    Picture AreaPicture(World w, Vector2 c, float rad, Vector2 side)
    {
        var tm = TacMap.For(w.Map);
        planEnemies.Clear();
        float cl = Vector2.Dot(c, side);
        var p = new Picture { Ec = c, Lo = cl - rad * 0.8f, Hi = cl + rad * 0.8f, Odds = 2f };
        int n = 0, good = 0; float hi = 0;
        for (float y = c.Y - rad; y <= c.Y + rad; y += TacMap.Cell)
        for (float x = c.X - rad; x <= c.X + rad; x += TacMap.Cell)
        {
            var q = new Vector2(x, y);
            if (Vector2.DistanceSquared(q, c) > rad * rad || x < 0 || y < 0 || x >= w.Map.Width || y >= w.Map.Height) continue;
            int cell = tm.CellOf(q);
            n++; hi += tm.High(cell);
            if (TacMap.TileCover(w.Map, tm.BestTile(cell)) >= 0.6f) good++;
        }
        p.CovN = n > 0 ? good / (float)n : 0; p.Cov = p.CovN * 0.8f; p.HiE = n > 0 ? hi / n : 0;
        return p;
    }

    /// <summary>미리 정한 측면 · 협공을 접촉 때 실제 적 정면 끝에 맞춘다 (제대는 그대로, 경유점 · 돌격 진지 · 고정 구역만 다시).</summary>
    void RefinePlan(World w, TacFaction s, TacRegiment r, Unit e, Vector2 dir, Vector2 side, float R)
    {
        r.PlanPre = false;
        var m = w.Map;
        var man = planMan; man.Clear(); var fix = planFix; fix.Clear();
        float own = 0;
        foreach (var g in s.Groups)
        {
            if (g.Element == ManElement.None || g.Pending || RegimentOfGroup(s, g) != r.Id) continue;
            own += g.Alive;
            if (g.Element == ManElement.Manoeuvre) man.Add(g); else fix.Add(g);
        }
        if (man.Count == 0) return;
        var p = SeePicture(w, e, side, own);
        r.PlanOdds = p.Odds; r.PlanSeen = p.Ne;
        float ecl = Vector2.Dot(p.Ec, side);
        Vector2 end = p.Ec + side * ((r.PlanSide > 0 ? p.Hi : p.Lo) - ecl);
        PlanTrace?.Invoke($"  plan R{r.Id} t={w.Tick / 60} 접촉 고침: seen {p.Ne} odds {p.Odds:0.00} ec ({p.Ec.X:0},{p.Ec.Y:0}) end ({end.X:0},{end.Y:0}) 이전 ({r.PlanEnemy.X:0},{r.PlanEnemy.Y:0})");
        if (!FlankRoute(w, end, man, dir, side, r.PlanSide, R, out var way, out var goal, out _, out _, out _)) return;
        r.PlanWay = way; r.PlanAxis = goal; r.PlanEnemy = end;
        Vector2 face = SafeNorm(end - goal, -side * r.PlanSide), along = Perp(face);
        SlotOrder(man, along);
        for (int i = 0; i < man.Count; i++)
        {
            var g = man[i];
            float o = SlotOffset(man, i);
            var ng = ClampMap(m, goal + along * o);
            if (g.ManStage == 1) g.ManWay = ClampMap(m, way + along * o);
            if (g.ManStage == 3 && Vector2.Distance(ng, g.ManGoal) > 6f) { g.ManStage = 2; g.ManArrive = 0; g.StateTick = w.Tick; r.ArriveTick = 0; }
            g.ManGoal = ng; g.ManFace = face; g.OrderTick = -100000;
        }
        AssignFix(fix, side, r.PlanSide, p.Ec, p.Lo, p.Hi, m);
    }

    /// <summary>고정 제대 구역: 적 정면 가운데 → 측면 끝 사이 (기동 제대가 칠 쪽) 를 중대마다 나눠 맡는다 — 먼 쪽 끝으로 몰려 기동 제대 혼자 싸우지 않게.</summary>
    static void AssignFix(List<TacGroup> fix, Vector2 side, int sg, Vector2 ec, float lo, float hi, GameMap m)
    {
        fix.Sort((a, b) => { int c = Vector2.Dot(a.Center, side).CompareTo(Vector2.Dot(b.Center, side)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
        float ecl = Vector2.Dot(ec, side);
        float fEnd = (sg > 0 ? hi : lo) - ecl, fStart = -fEnd * 0.2f;
        for (int i = 0; i < fix.Count; i++)
        {
            int k = sg > 0 ? i : fix.Count - 1 - i;   // 바깥 (측면 쪽) 중대가 끝 쪽
            float t = fix.Count == 1 ? 0.5f : k / (float)(fix.Count - 1);
            fix[i].ManGoal = ClampMap(m, ec + side * (fStart + (fEnd * 0.8f - fStart) * t));
        }
    }

    /// <summary>기동 계획을 고른다 (결정적: 후보 순서 고정 · 점수 엄격 비교).</summary>
    /// e = null 이면 접촉 전 계획 (구역 preC · preR 을 적 진지로 — 측면 · 협공이 이길 때만 정한다).
    void ChoosePlan(World w, TacFaction s, TacRegiment r, List<TacBattalion> fronts, Unit? e, Vector2 dir, Vector2 side, float R, Vector2 preC, float preR)
    {
        bool pre = e == null;
        var S = s.Profile.Strategy;
        var m = w.Map;
        var tm = TacMap.For(m);
        // 우리: 앞 대대의 소총 중대 (편성 대기 · 후퇴 중 · 8명 미만 제외) + 붙은 화력
        var cos = planCos; cos.Clear();
        float own = 0;
        Vector2 oc = Vector2.Zero;
        foreach (var b in fronts)
        {
            if (b.Spent || b.FallbackUntil > w.Tick) continue;
            foreach (var g in s.Groups)
            {
                if (g.Battalion != b.Id || g.Pending) continue;
                if (g.Kind == TacKind.Fire) { own += g.Alive; continue; }
                if (g.Kind != TacKind.Line || g.Alive < 8 || g.State == TacState.Fallback) continue;
                cos.Add(g); own += g.Alive; oc += g.Center * g.Alive;
            }
        }
        int nOwnLine = 0; foreach (var g in cos) nOwnLine += g.Alive;
        if (cos.Count == 0 || nOwnLine == 0)   // 나눌 중대가 없음 (전부 후퇴 · 소수) — 기록 안 함
        { if (!pre) { r.Plan = ManPlan.Frontal; r.PlanStage = 3; r.PlanTick = w.Tick; } return; }
        oc /= nOwnLine;
        // 적 그림: 접촉 = e 둘레 45 × K 칸의 보이는 적, 접촉 전 = 목표 구역
        var pic = pre ? AreaPicture(w, preC, preR, side) : SeePicture(w, e!, side, own);
        var ens = planEnemies;
        Vector2 ec = pic.Ec; float lo = pic.Lo, hi = pic.Hi, cov = pic.Cov, hiE = pic.HiE, covN = pic.CovN, odds = pic.Odds;
        int ne = pic.Ne, mgAll = pic.Mg;
        var obj = S.Objectives ? ObjectiveFor(w, s.Faction) : new AiObjective { Index = -1 };
        bool objOn = obj.Kind is AiObjectiveKind.Take or AiObjectiveKind.Intercept or AiObjectiveKind.Hold;
        float ObjTerm(Vector2 p) => objOn ? -S.ObjectiveWeight * Vector2.Distance(p, obj.Point) / MathF.Max(8f, R) : 0f;
        float ecl = Vector2.Dot(ec, side);
        // 이동 속도 추정 (칸/초): 기동할 중대 구성원의 가장 느린 정의 속도 × 0.45 (대형 · 지형 손실 — T_r1.5 측정 ≈ 1.4칸/초)
        float minSpeed = float.MaxValue;
        foreach (var g in cos)
            if (w.FindCompany(g.CompanyId) is { } co)
                foreach (var u in co.Members) if (u.Alive && !u.Def.Heavy) minSpeed = MathF.Min(minSpeed, u.Def.Speed);
        if (minSpeed == float.MaxValue) minSpeed = 3f;
        float v = MathF.Max(0.5f, minSpeed * 0.45f);

        // ---- 후보 점수
        ManPlan best = ManPlan.Frontal; int bestSide = 0;
        float bestSc = S.PlanFrontal - 0.6f * covN - S.MgWeight * MathF.Min(3, mgAll) - 0.3f * hiE + 0.25f * Math.Clamp(odds - 2.5f, 0f, 2f) + ObjTerm(ec);
        float bDetour = 0, bFront = Vector2.Distance(oc, ec), bEta = 0;
        PlanTrace?.Invoke($"  plan R{r.Id} t={w.Tick / 60}{(pre ? " 접촉 전" : "")} own {own:0} seen {ne} odds {odds:0.00} cov {cov:0.00} mg {mgAll} hi {hiE:0.00} ec ({ec.X:0},{ec.Y:0}) width {hi - lo:0} n {cos.Count} | Frontal sc {bestSc:0.00}");
        Vector2 bWay = default, bGoal = default, bEnd = default;
        int n = cos.Count;
        for (int kind = 0; kind < 2; kind++)
        {
            var pk = kind == 0 ? ManPlan.Flank : ManPlan.Pincer;
            float share = kind == 0 ? S.FlankShare : S.PincerShare;
            float minOdds = kind == 0 ? S.FlankMinOdds : S.PincerMinOdds;
            if (n < 2 || odds < minOdds) continue;   // 중대 하나면 나눌 수 없다 (n − 1 = 0 이면 Clamp 예외 — F_trench s7)
            int nMan = Math.Clamp((int)MathF.Floor(n * share + 0.5f), 1, n - 1);
            if (kind == 1 && (nMan < 2 || n - nMan < 2)) continue;   // 협공 = 두 방향 모두 2개 중대 이상 (3개 이하면 측면)
            if (pre && kind == 0 && n >= 4 && S.PincerMinOdds <= 2f) continue;   // 접촉 전 (병력비 모름): 4개 중대 이상이면 협공만 본다
            for (int si = 0; si < 2; si++)
            {
                int sg = si == 0 ? s.FlankSign : -s.FlankSign;
                PickOuter(cos, side, sg, nMan, planMan);
                Vector2 end = ec + side * ((sg > 0 ? hi : lo) - ecl);
                if (!FlankRoute(w, end, planMan, dir, side, sg, R, out var way, out var goal, out float len, out float forest, out Vector2 sm)) { PlanTrace?.Invoke($"    plan R{r.Id} {pk}{(sg > 0 ? "+" : "-")} 경로 없음"); continue; }
                float frontD = MathF.Max(R, Vector2.Distance(sm, ec));
                float eta = len / v * (1f + 0.5f * forest);
                if (len > S.MaxDetour * frontD || eta > S.MaxArrive) { PlanTrace?.Invoke($"    plan R{r.Id} {pk}{(sg > 0 ? "+" : "-")} 상한 넘음 len {len:0} front {frontD:0} eta {eta:0}"); continue; }
                int mgSide = 0;
                foreach (var u in ens) if (IsMg(u) && Vector2.Distance(u.Pos, end) <= R * 1.2f) mgSide++;
                float sc = (kind == 0 ? S.PlanFlank + 0.5f : S.PlanPincer + 0.6f) + 0.4f * covN - S.MgWeight * MathF.Min(3, mgSide)
                           - 0.5f * eta / MathF.Max(1f, S.MaxArrive) + 0.15f * tm.Cover(tm.CellOf(way)) - 0.2f * forest
                           - 0.3f * Math.Clamp(minOdds + 0.4f - odds, 0f, 1f) - 0.15f * hiE + ObjTerm(goal);
                PlanTrace?.Invoke($"    plan R{r.Id} {pk}{(sg > 0 ? "+" : "-")} sc {sc:0.00} len {len:0} front {frontD:0} eta {eta:0} mgSide {mgSide} forest {forest:0.00}");
                if (sc > bestSc) { bestSc = sc; best = pk; bestSide = sg; bWay = way; bGoal = goal; bEnd = end; bDetour = len; bFront = frontD; bEta = eta; }
            }
        }
        // 중점: 적 정면을 셋으로 나눠 가장 약한 구역 (적 수 · 엄폐 · 기관총) 에 좁게
        float ew = hi - lo;
        Vector2 spAxis = default; float spWidth = 0;
        if (!pre && n >= 2 && ne >= 10 && ew >= 30f)
        {
            float bestW = float.MinValue, sumW = 0, bestCov = 0; int bestK = 1;
            for (int k = 0; k < 3; k++)
            {
                float a0 = lo + ew * k / 3f, a1 = lo + ew * (k + 1) / 3f;
                int cnt = 0, mg = 0; float cv = 0;
                foreach (var u in ens)
                {
                    float l = Vector2.Dot(u.Pos, side);
                    if (l < a0 || l > a1 + (k == 2 ? 0.01f : 0)) continue;
                    cnt++; cv += MathF.Max(0, TacMap.TileCover(m, u.Pos)); if (IsMg(u)) mg++;
                }
                float cvN = cnt > 0 ? Math.Clamp(cv / cnt / 0.8f, 0f, 1f) : 0f;
                float wk = -cnt / (float)ne - 0.5f * cvN - S.MgWeight * MathF.Min(3, mg);
                sumW += wk;
                if (cnt >= 3 && wk > bestW) { bestW = wk; bestK = k; bestCov = cvN; }   // 적이 있는 구역만 (빈 구역 = 정면 끝 — 측면 후보가 다룬다)
            }
            if (bestW > float.MinValue)
            {
                float mid = lo + ew * (bestK + 0.5f) / 3f;
                spAxis = ec + side * (mid - ecl);
                float sc = S.PlanSchwerpunkt + 0.4f * (bestW - sumW / 3f) + 0.15f * Math.Clamp(odds - 1f, 0f, 1f) - 0.8f * bestCov - S.MgWeight * 0.5f * MathF.Min(3, mgAll) + ObjTerm(spAxis);
                spWidth = MathF.Max(24f, ew / 3f + 10f);
                PlanTrace?.Invoke($"    plan R{r.Id} Schwerpunkt sc {sc:0.00} sector {bestK} cov {bestCov:0.00}");
                if (sc > bestSc) { bestSc = sc; best = ManPlan.Schwerpunkt; bestSide = 0; bDetour = 0; bEta = 0; }
            }
        }

        if (pre && best is not (ManPlan.Flank or ManPlan.Pincer)) return;   // 접촉 전에는 측면 · 협공만 정한다 (정면 · 중점은 접촉 때 실제 그림으로)
        r.Plan = best; r.PlanSide = bestSide; r.PlanTick = w.Tick; r.PlanPre = pre;
        r.FixTick = r.ArriveTick = r.ReleaseTick = 0;
        r.PlanDetour = bDetour; r.PlanFront = bFront; r.PlanEta = bEta; r.PlanOdds = odds; r.PlanSeen = ne;
        switch (best)
        {
            case ManPlan.Frontal:
                r.PlanStage = 3; s.PlansFrontal++; LogPlan(s, r, 2);
                return;
            case ManPlan.Schwerpunkt:
                r.PlanStage = 1; r.PlanAxis = ClampMap(m, spAxis); r.PlanWidth = spWidth; s.PlansSchwerpunkt++; LogPlan(s, r, 2);
                return;
        }
        // 측면 · 협공: 제대 배정 (바깥 중대들 = 기동, 나머지 = 고정)
        int nm = Math.Clamp((int)MathF.Floor(n * (best == ManPlan.Flank ? S.FlankShare : S.PincerShare) + 0.5f), 1, n - 1);
        PickOuter(cos, side, bestSide, nm, planMan);
        r.PlanStage = 1; r.PlanWay = bWay; r.PlanAxis = bGoal; r.PlanEnemy = bEnd;
        r.PlanDeadline = w.Tick + Sec(S.MaxArrive * 1.25f + 10f);
        if (best == ManPlan.Flank) s.PlansFlank++; else s.PlansPincer++;
        Vector2 face = SafeNorm(bEnd - bGoal, -side * bestSide);
        Vector2 along = Perp(face);
        SlotOrder(planMan, along);
        for (int i = 0; i < planMan.Count; i++)
        {
            var g = planMan[i];
            float o = SlotOffset(planMan, i);
            g.Element = ManElement.Manoeuvre; g.ManStage = 1; g.ManArrive = 0; g.Task = TacTask.Attack;   // 대대 예비 중대였어도 공격 과제로
            g.ManWay = ClampMap(m, bWay + along * o);
            g.ManGoal = ClampMap(m, bGoal + along * o);
            g.ManFace = face;
            g.OrderTick = -100000; g.SquadBound = 0;
        }
        foreach (var b in fronts) if (b.ReserveGroup != 0 && !b.ReserveCommitted && cos.Exists(g => g.Id == b.ReserveGroup)) b.ReserveGroup = 0;   // 제대에 들어간 예비 중대 (대대가 남은 중대 수로 다시 정한다)
        planFix.Clear();
        foreach (var g in cos)
            if (g.Element != ManElement.Manoeuvre)
            {
                g.Element = ManElement.Fix; g.ManStage = (byte)(S.FixLead > 0 ? 4 : 0);
                g.ManWay = default; g.ManArrive = 0;
                planFix.Add(g);
            }
        AssignFix(planFix, side, bestSide, ec, lo, hi, m);
    }

    /// <summary>기동 제대 자리 순서: along 축 위치 순 (같으면 Id) — 가까운 자리를 가까운 중대에.</summary>
    static void SlotOrder(List<TacGroup> man, Vector2 along) =>
        man.Sort((a, b) => { int c = Vector2.Dot(a.Center, along).CompareTo(Vector2.Dot(b.Center, along)); return c != 0 ? c : a.Id.CompareTo(b.Id); });

    /// <summary>i 번째 자리의 옆 거리 (along 축, 가운데 0): 이동 정면 폭 + 3칸 간격으로 나란히.</summary>
    static float SlotOffset(List<TacGroup> man, int i)
    {
        float total = 0;
        foreach (var g in man) total += ManWidth(g) + 3f;
        float o = -total / 2f;
        for (int k = 0; k < i; k++) o += ManWidth(man[k]) + 3f;
        return o + (ManWidth(man[i]) + 3f) / 2f;
    }

    /// <summary>기동 제대 중대의 이동 · 대기 정면 폭 (보통 정면의 0.6 — 우회 길을 좁게).</summary>
    static float ManWidth(TacGroup g) => MathF.Max(6f, Frontage(g) * 0.6f);

    /// <summary>보이는 적이 기관총 · 흩뿌림 무기인가 (거치 화기 포함).</summary>
    static bool IsMg(Unit u) => u.Weapon != null && (u.Weapon.Spray > 0 || u.CanDeploy) && !World.IsIndirect(u.Weapon);

    /// <summary>cos 를 side × sg 방향으로 바깥 것부터 count 개 (같으면 Id 순).</summary>
    static void PickOuter(List<TacGroup> cos, Vector2 side, int sg, int count, List<TacGroup> into)
    {
        into.Clear();
        into.AddRange(cos);
        into.Sort((a, b) => { int c = (sg * Vector2.Dot(b.Center, side)).CompareTo(sg * Vector2.Dot(a.Center, side)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
        if (into.Count > count) into.RemoveRange(count, into.Count - count);
    }

    /// <summary>
    /// 측면 경로: 적 정면 끝 end 바깥 (사거리 ×1.3) 조금 앞 (×0.75 우리 쪽) 경유점 → 끝 옆 사거리 ×0.9 · 우리 쪽 ×0.35 돌격 진지.
    /// 두 구간을 2칸마다 짚어 통행 불가 (물 · 건물) 가 3번 넘으면 없음. 숲 비율 (느림 · 엄폐) 도 함께.
    /// </summary>
    static bool FlankRoute(World w, Vector2 end, List<TacGroup> man, Vector2 dir, Vector2 side, int sg, float R, out Vector2 way, out Vector2 goal, out float len, out float forest, out Vector2 from)
    {
        var m = w.Map;
        way = ClampMap(m, end + side * (sg * R * 1.5f) - dir * (R * 0.9f));
        goal = ClampMap(m, end + side * (sg * R * 1.0f) - dir * (R * 0.3f));
        from = Vector2.Zero; int sn = 0;
        foreach (var g in man) { from += g.Center * g.Alive; sn += g.Alive; }
        from /= MathF.Max(1, sn);
        // 경로 길이 = 중대마다 (자리 순서대로 옆으로 벌린 경유점 · 진지) 가장 긴 것
        Vector2 face = SafeNorm(end - goal, -side * sg), along = Perp(face);
        SlotOrder(man, along);
        len = 0;
        for (int i = 0; i < man.Count; i++)
        {
            float o = SlotOffset(man, i);
            Vector2 wi = ClampMap(m, way + along * o), gi = ClampMap(m, goal + along * o);
            len = MathF.Max(len, Vector2.Distance(man[i].Center, wi) + Vector2.Distance(wi, gi));
        }
        forest = 0;
        // 지도 가장자리에 눌려 바깥으로 못 나가면 (끝에서 사거리 ×0.6 안) 측면이 아니다
        if (sg * Vector2.Dot(goal - end, side) < R * 0.6f) return false;
        int blocked = 0, samples = 0, trees = 0;
        for (int seg = 0; seg < 2; seg++)
        {
            Vector2 a = seg == 0 ? from : way, b = seg == 0 ? way : goal;
            float l = Vector2.Distance(a, b);
            int steps = Math.Max(1, (int)(l / 2f));
            for (int i = 1; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                int x = (int)p.X, y = (int)p.Y;
                samples++;
                if (!m.IsWalkable(x, y)) blocked++;
                else if (m.Get(x, y) == TerrainType.Forest) trees++;
            }
        }
        forest = samples > 0 ? trees / (float)samples : 0;
        return blocked <= 3;
    }

    /// <summary>측면 · 협공 진행: 고정 교전 · 기동 도착 확인, 협공 대기 해제, 돌격 허가 (풀기) · 취소.</summary>
    void SyncPlan(World w, TacFaction s, TacRegiment r, float R)
    {
        var S = s.Profile.Strategy;
        var man = planMan; man.Clear(); var fix = planFix; fix.Clear();
        foreach (var g in s.Groups)
        {
            if (g.Element == ManElement.None || g.Pending || RegimentOfGroup(s, g) != r.Id) continue;
            if (g.Element == ManElement.Manoeuvre) man.Add(g); else fix.Add(g);
        }
        if (man.Count == 0) { s.PlanAborts++; EndPlan(w, s, r, 1); return; }   // 기동 제대가 없어짐 (전멸 · 후퇴 · 조우)
        // 고정 제대 교전: 절반 이상 중대가 사격 중
        if (r.FixTick == 0 && fix.Count > 0)
        {
            int eng = 0;
            foreach (var g in fix)
            {
                if (g.CompanyId == 0 || w.FindCompany(g.CompanyId) is not { } co) continue;
                int n = 0, firing = 0;
                foreach (var u in co.Members) if (u.Alive) { n++; if (u.Target != null || w.Tick - u.LastShotTick < Sec(5)) firing++; }
                if (n > 0 && firing * 5 >= n) eng++;   // 구성원 1/5 이상이 표적을 잡았거나 5초 안에 쐈다
            }
            if (eng * 2 >= fix.Count) r.FixTick = w.Tick;
        }
        // 기동 제대 도착 예정 (초) · 도착
        bool all = true; float rem = 0;
        foreach (var g in man)
        {
            if (g.ManStage != 3) all = false;
            float d = g.ManStage == 1 ? Vector2.Distance(g.Center, g.ManWay) + Vector2.Distance(g.ManWay, g.ManGoal) : g.ManStage == 2 ? Vector2.Distance(g.Center, g.ManGoal) : 0;
            rem = MathF.Max(rem, d);
        }
        float v = r.PlanEta > 0 ? MathF.Max(0.5f, r.PlanDetour / r.PlanEta) : 1.5f;
        if (all && r.ArriveTick == 0) r.ArriveTick = w.Tick;
        // 3B (3A 남은 일 1): 도착했는데 고정 제대가 fixPushSec 동안 교전을 못 함 → 고정 중대를 표적 사거리 ×0.85 까지 밀기 (ManStage 5, FixPushStep)
        if (S.FixPush && r.ArriveTick != 0 && r.FixTick == 0 && w.Tick - r.ArriveTick >= Sec(S.FixPushSec))
            foreach (var g in fix) if (g.ManStage == 0) g.ManStage = 5;
        // 도착 예정이 fixLead 초 안이면 고정 제대 교전 시작 (사거리 밖 대기 해제)
        if (rem / v <= S.FixLead)
            foreach (var g in fix) if (g.ManStage == 4) { g.ManStage = 0; g.OrderTick = -100000; }
        bool release = r.ArriveTick != 0 && (r.FixTick != 0 || w.Tick - r.ArriveTick > Sec(S.FixWait));   // 고정 교전 → 기동 도착 → 돌격 (고정이 끝내 교전 못 하면 도착 20초 뒤)
        if (release || w.Tick >= r.PlanDeadline)
        {
            if (!release) s.PlanAborts++;
            EndPlan(w, s, r, release ? 0 : 1);
        }
    }

    /// <summary>계획 끝 (풀기 · 취소): 제대 표시를 지우고 기동 중대는 적 측면 끝을 향해 보통 공격으로.</summary>
    void EndPlan(World w, TacFaction s, TacRegiment r, int reason)
    {
        foreach (var g in s.Groups)
        {
            if (g.Element == ManElement.None || RegimentOfGroup(s, g) != r.Id) continue;
            if (g.Element == ManElement.Manoeuvre && reason != 3)
            {
                g.TaskPoint = ClampMap(w.Map, r.PlanEnemy); g.TaskDir = g.ManFace;
                if (g.State is TacState.Advance or TacState.Engage) g.State = TacState.Idle;
            }
            g.Element = ManElement.None; g.ManStage = 0; g.OrderTick = -100000;
        }
        if (r.Plan is ManPlan.Flank or ManPlan.Pincer && r.PlanStage == 1)
        {
            if (reason == 0) { r.ReleaseTick = w.Tick; s.PlanReleases++; }
            LogPlan(s, r, reason);
        }
        r.PlanStage = reason == 3 ? (byte)0 : (byte)2;
        if (reason == 3) r.Plan = ManPlan.None;
    }

    void LogPlan(TacFaction s, TacRegiment r, int reason)
    {
        if (s.PlanLog.Count >= PlanLogStride * 64) return;   // 상한 (측정용)
        s.PlanLog.Add(r.Id); s.PlanLog.Add((int)r.Plan); s.PlanLog.Add(r.PlanSide);
        s.PlanLog.Add((int)r.PlanTick); s.PlanLog.Add((int)r.FixTick); s.PlanLog.Add((int)r.ArriveTick); s.PlanLog.Add((int)r.ReleaseTick);
        s.PlanLog.Add((int)MathF.Round(r.PlanDetour * 10)); s.PlanLog.Add((int)MathF.Round(r.PlanFront * 10)); s.PlanLog.Add((int)MathF.Round(r.PlanEta * 10));
        s.PlanLog.Add(reason); s.PlanLog.Add((int)MathF.Round(r.PlanOdds * 100)); s.PlanLog.Add(r.PlanSeen);
    }

    /// <summary>교착 단계 (0 = 없음): 이 세력이 보이는 적 없이 N · 2N · 3N 초 지남 → 1 · 2 · 3.</summary>
    static int StaleLevel(World w, TacFaction s, float n) => StaleLevel(w.Tick, s.LastContactTick, n);
    public static int StaleLevel(long tick, long lastContact, float n)
    {
        if (n <= 0) return 0;
        long t = tick - lastContact;
        return t < Sec(n) ? 0 : t < Sec(2 * n) ? 1 : t < Sec(3 * n) ? 2 : 3;
    }

    /// <summary>접촉을 잃고 접근으로 돌아가면 다음 접촉에 다시 고른다 (진행 중인 계획은 그대로).</summary>
    static void ResetPlanOnApproach(TacRegiment r)
    {
        if (r.PlanStage == 1) return;
        r.Plan = ManPlan.None; r.PlanStage = 0; r.PlanTick = 0; r.PlanPre = false; r.PlanPreTried = false;
    }

    /// <summary>
    /// 기동 제대 중대 (LineThink 공격): 경유점 → 돌격 진지 (이동 명령 — 도중 교전 안 함) → 진지에서 사수 대기. 연대가 풀면 보통 공격.
    /// 적이 사거리 절반 안에 붙으면 (조우) 그 자리에서 보통 교전으로 (제대에서 빠짐). 처리했으면 true.
    /// </summary>
    bool ManoeuvreMove(World w, TacFaction s, TacGroup g, float range, Unit? e, float d)
    {
        if (g.ManStage is not (1 or 2 or 3)) return false;
        var T = s.Profile.Tactics;
        if (e != null && d < range * 0.5f) { g.ManStage = 0; g.Element = ManElement.None; g.OrderTick = -100000; return false; }
        if (g.ManStage == 1 && Vector2.Distance(g.Center, g.ManWay) < 6f) { g.ManStage = 2; g.StateTick = w.Tick; g.OrderTick = -100000; }
        if (g.ManStage == 2 && (Vector2.Distance(g.Center, g.ManGoal) < 4f || (g.State == TacState.Advance && w.Tick - g.StateTick > Sec(45))))
        {
            g.ManStage = 3; g.ManArrive = w.Tick;
            HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick;
            return true;
        }
        if (g.ManStage == 3)
        {
            // 교착 해소: 도착 뒤 N초 동안 사거리 안에 보이는 적이 없으면 사수 대기를 풀고 일반 공격 (전진 · 탐색) 으로
            if (T.StalemateSeconds > 0 && w.Tick - g.ManArrive > Sec(T.StalemateSeconds) && (e == null || d > range * 1.1f))
            { g.ManStage = 0; g.Element = ManElement.None; g.OrderTick = -100000; g.State = TacState.Idle; return false; }
            if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
            return true;
        }
        Vector2 tgt = g.ManStage == 1 ? g.ManWay : g.ManGoal;
        Vector2 face = g.ManStage == 1 ? SafeNorm(g.ManWay - g.Center, g.ManFace) : g.ManFace;
        if (Retarget(w, g, TacState.Advance, tgt, 4f))
        {
            var st = g.State;
            Order(w, s, g, mem, Ord.Move, tgt, face, ManWidth(g));
            g.State = TacState.Advance;
            if (st != TacState.Advance) g.StateTick = w.Tick;
        }
        return true;
    }

    /// <summary>고정 제대 대기 (ManStage 4): 기동 제대가 가까워질 때까지 가장 가까운 적에서 사거리 ×1.15 밖에 멈춰 기다린다 (이미 안이면 그 자리 사수). 처리했으면 true.</summary>
    bool FixHold(World w, TacFaction s, TacGroup g, float range)
    {
        if (g.ManStage != 4) return false;
        var n = Grid.Nearest(g.Center, TacGrid.Any);
        if (n == null || Vector2.Distance(n.Pos, g.Center) > range * 1.15f) return false;
        if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
        return true;
    }

    /// <summary>고정 제대 중대의 표적 기준: 맡은 구역 (ManGoal) 에 가장 가까운 보이는 적 — 없으면 null (보통 규칙).</summary>
    Unit? FixTarget(TacGroup g, bool hurtsArmor) => g.Element == ManElement.Fix && g.ManGoal != default ? Grid.Nearest(g.ManGoal, hurtsArmor ? TacGrid.Any : TacGrid.Infantry) : null;

    /// <summary>계획 진행 중 (고정 제대: 돌격 금지 · 틀 행동 금지) 인가 — 그 중대의 연대 계획이 측면 · 협공 1단계.</summary>
    static bool PlanHolding(TacGroup g) => g.Element != ManElement.None;
}
