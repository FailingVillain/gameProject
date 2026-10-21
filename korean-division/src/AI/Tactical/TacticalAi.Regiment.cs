using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 연대 전술 층 (≈1초, docs/TACTICAL_AI_DESIGN.md §6) 과 그 아래 대대 실행 층 (≈1초, 연대와 2호출 엇갈림).
///  · 공격: 접근 → (지원 포병이 있으면) 준비사격 → 돌격. 접촉하면 역할: 앞 대대 2개 이상 = 사격 진지 점수가 좋은 대대가 화력 기반, 나머지 기동
///    (scheme flank 면 바깥 기동 대대를 측면 쪽으로), 1개면 대대 안에서 중대를 나눔 (Split). 아군 기동 중대가 포병 표적에 가까우면 연신.
///  · 방어: 앞 대대 = 주저항선 (중대 진지 깊이는 TacMap), 예비 대대 = 역습 (주저항선 돌파 · 근접한 적이 약하거나 제압되면 그 지점으로 25초 공격).
/// </summary>
public sealed partial class TacticalAi
{
    [NonSerialized] readonly List<TacBattalion> batBuf = new(), frontBuf = new();

    void RegimentThink(World w, TacFaction s, TacRegiment r)
    {
        if (r.Task == TacTask.None) return;
        var T = s.Profile.Tactics;
        var S = s.Profile.Strategy;
        float R = s.RifleRange > 0 ? s.RifleRange : 16f;
        Vector2 dir = SafeNorm(r.Dir, s.Dir), side = Perp(dir);
        if (r.Task != TacTask.Attack && r.Plan != ManPlan.None) EndPlan(w, s, r, 3);   // stage 3A: 과제가 공격이 아니면 기동 계획 취소
        var bats = batBuf; bats.Clear();
        foreach (var b in s.Battalions) if (b.Regiment == r.Id) bats.Add(b);
        if (bats.Count == 0) return;
        // 무너진 대대 → 20초 후퇴 뒤 예비
        foreach (var b in bats)
            if (!b.Spent && b.Initial > 0 && b.Alive < b.Initial * T.RetreatStrength * 0.75f && !CrossCommitted(w, r))   // 도하 중 (병목 · 교두보) 에는 대대 후퇴를 미룬다
            { b.Spent = true; b.FallbackUntil = w.Tick + Sec(20); s.Fallbacks++; }

        if (r.Task is TacTask.Reserve or TacTask.Fallback)
        {
            ResetCrossing(s, r); ResetCounter(s, r);
            r.Phase = RegPhase.None; r.HasFireTarget = false;
            for (int i = 0; i < bats.Count; i++)
            {
                var b = bats[i];
                b.Task = r.Task; b.Dir = dir; b.Width = 0; b.Role = TacRole.Line; b.HoldFire = false; b.Split = false;
                b.Point = ClampMap(w.Map, r.Point + side * ((i - (bats.Count - 1) / 2f) * 14f));
            }
            return;
        }

        var fronts = frontBuf; fronts.Clear();
        foreach (var b in bats) if (!b.Spent) fronts.Add(b);
        bool wantRes = S.Reserve && fronts.Count >= 3 && !r.ReserveCommitted;
        if (wantRes)
        {
            if (r.ReserveBattalion == 0 || !fronts.Exists(b => b.Id == r.ReserveBattalion))
                r.ReserveBattalion = fronts.OrderBy(b => Vector2.Dot(b.Center, dir)).ThenBy(b => b.Id).First().Id;
        }
        else if (!r.ReserveCommitted) r.ReserveBattalion = 0;
        TacBattalion? resB = r.ReserveBattalion != 0 ? fronts.Find(b => b.Id == r.ReserveBattalion) : null;
        if (resB != null) fronts.Remove(resB);
        if (fronts.Count == 0)
        {
            if (resB != null) { r.ReserveCommitted = true; r.ReserveBattalion = 0; s.ReserveCommits++; fronts.Add(resB); resB = null; }
            foreach (var b in bats) if (b.Spent && b.FallbackUntil <= w.Tick && !fronts.Contains(b)) fronts.Add(b);
        }
        Vector2 fc = Vector2.Zero; int fn = 0;
        foreach (var b in fronts) { fc += b.Center * b.Alive; fn += b.Alive; }
        fc = fn > 0 ? fc / fn : r.Center;
        var e = Grid.Nearest(fc, canHurtArmor ? TacGrid.Any : TacGrid.Infantry);
        float de = e != null ? Vector2.Distance(e.Pos, fc) : float.MaxValue;
        bool contact = de < R * 2f;
        bool arty = S.Artillery >= 0.2f && s.Groups.Exists(g => g.Kind == TacKind.Artillery && g.Supports == r.Id);

        if (r.Task == TacTask.Attack)
        {
            int fa = 0, fi = 0;
            foreach (var b in fronts) { fa += b.Alive; fi += b.Initial; }
            if (r.Phase is RegPhase.None or RegPhase.Defend or RegPhase.Counter) { r.Phase = RegPhase.Approach; r.PhaseTick = w.Tick; r.Lifted = false; }
            if (r.Phase == RegPhase.Approach && contact && e != null)
            {
                r.Phase = arty && T.PrepSeconds > 0 ? RegPhase.Prep : RegPhase.Assault;
                if (r.Phase == RegPhase.Prep) s.Preps++;
                r.PhaseTick = w.Tick;
                AssignAttackRoles(w, s, fronts, e.Pos);
            }
            else if (r.Phase == RegPhase.Prep)
            {
                bool ready = ArtilleryReady(w, s, r);
                long el = w.Tick - r.PhaseTick;
                if ((ready && el >= Sec(T.PrepSeconds)) || el >= Sec(T.PrepSeconds + 20f)) { r.Phase = RegPhase.Assault; r.PhaseTick = w.Tick; }
            }
            else if (r.Phase == RegPhase.Assault && (e == null || de > R * 3.5f) && w.Tick - r.PhaseTick > Sec(15)) { r.Phase = RegPhase.Approach; r.PhaseTick = w.Tick; ResetPlanOnApproach(r); }
            if (r.Phase is RegPhase.Prep or RegPhase.Assault && fronts.Exists(b => b.Role == TacRole.Line && !b.Split && CombatBattalion(s, b, w.TacRules.BattalionRoles)) && e != null) AssignAttackRoles(w, s, fronts, e.Pos);   // 새로 들어온 대대
            // 예비 대대 투입: 붕괴 · 70% 미만 · 오랜 접촉 · 돌파 기회 (표적 둘레 적이 제압됨) · 2배 우세
            if (resB != null)
            {
                bool collapse = bats.Exists(b => b.Spent);
                bool longFight = r.Phase == RegPhase.Assault && w.Tick - r.PhaseTick > Sec(40f - 20f * S.Aggressiveness);
                var est = e != null ? Grid.Stats(e.Pos, 14f * K) : default;
                bool exploit = r.Phase == RegPhase.Assault && est.Count > 0 && est.Supp / est.Count >= T.ChargeSuppression;
                if (collapse || (fi > 0 && fa < fi * 0.7f) || longFight || exploit || (contact && est.Count > 0 && fa > est.Count * 2))
                { r.ReserveCommitted = true; r.ReserveBattalion = 0; s.ReserveCommits++; fronts.Add(resB); resB.Role = TacRole.Manoeuvre; resB = null; }
            }
            bool prep = r.Phase == RegPhase.Prep;
            foreach (var b in fronts)
            {
                if (r.Phase == RegPhase.Approach) { b.Role = TacRole.Line; b.Split = false; }
                b.HoldFire = prep && b.Role != TacRole.BaseOfFire;
            }
            if (r.CrossPhase is CpNone or CpDone) ManoeuvreStep(w, s, r, fronts, e, de, dir, side, R);   // stage 3A S1 기동 계획 (TacticalAi.Manoeuvre.cs)
            else if (r.Plan != ManPlan.None) EndPlan(w, s, r, 3);   // 도하 계획 중에는 기동 계획을 쓰지 않는다 (도하가 중대를 직접 부림)
            if (S.Manoeuvre && r.Plan == ManPlan.Schwerpunkt && r.PlanStage == 1) { r.Point = r.PlanAxis; r.Width = r.Width > 0 ? MathF.Min(r.Width, r.PlanWidth) : r.PlanWidth; }   // 중점: 좁은 구역에 집중
            LayOut(w, s, r, fronts, TacTask.Attack, dir, side, R);
            UpdateFireTarget(w, s, r, fronts, dir, R, arty, e);
            CrossingThink(w, s, r, fronts, fc, e, R, arty);   // 도하 · 병목 돌파 (TacticalAi.Crossing.cs)
            if (r.CrossPhase is CpNone or CpDone) CounterThink(w, s, r, fronts, fc, e, R, arty); else ResetCounter(s, r);   // 역습 교리 (TacticalAi.Counter.cs)
        }
        else
        {
            // 방어 (종심): 주저항선 + 역습 예비
            ResetCrossing(s, r); ResetCounter(s, r);
            if (r.Phase is not (RegPhase.Defend or RegPhase.Counter)) { r.Phase = RegPhase.Defend; r.PhaseTick = w.Tick; r.Lifted = false; }
            foreach (var b in fronts) { b.Role = TacRole.MainLine; b.Split = false; b.HoldFire = false; }
            if (resB != null && bats.Exists(b => b.Spent && b.FallbackUntil > w.Tick))
            { r.ReserveCommitted = true; r.ReserveBattalion = 0; s.ReserveCommits++; resB.Role = TacRole.MainLine; fronts.Add(resB); resB = null; }   // 주저항선이 무너지면 예비가 메운다
            LayOut(w, s, r, fronts, TacTask.Defend, dir, side, R);
            if (resB != null)
            {
                if (w.Tick >= r.CounterUntil)
                {
                    var e2 = Grid.Nearest(r.Point, TacGrid.Infantry);
                    if (e2 != null)
                    {
                        Vector2 rel = e2.Pos - r.Point;
                        bool penetration = Vector2.Dot(rel, dir) < R * 0.25f && MathF.Abs(Vector2.Dot(rel, side)) < r.Width / 2f + R;
                        var st = Grid.Stats(e2.Pos, 12f * K);
                        // 돌파 (주저항선에 닿거나 넘은 적) 또는 주저항선 앞 사거리 안의 적이 제압됐거나 예비보다 뚜렷이 약함 (공격이 꺾인 순간) → 역습
                        // 수 비교는 연대 정면 전체의 보이는 적 (표적 둘레 12칸만 세면 공격 덩어리를 과소평가 — 실험: 예비 대대가 204 → 56 으로 녹음)
                        bool near = st.Count > 0 && Vector2.Dot(rel, dir) < R && MathF.Abs(Vector2.Dot(rel, side)) < r.Width / 2f + R;
                        int wide = near ? Grid.Stats(r.Point + dir * (R * 0.5f), r.Width / 2f + R * 1.5f).Count : 0;
                        if (CounterGo(penetration, near, st.Count, st.Supp, wide, resB.Alive, T.CounterOdds, T.ChargeSuppression))
                        { r.CounterUntil = w.Tick + Sec(25); r.CounterPoint = e2.Pos; s.Counterattacks++; }
                    }
                }
                resB.Dir = dir; resB.HoldFire = false; resB.Split = false;
                if (w.Tick < r.CounterUntil)
                {
                    r.Phase = RegPhase.Counter;
                    resB.Task = TacTask.Attack; resB.Role = TacRole.Counter; resB.Point = ClampMap(w.Map, r.CounterPoint); resB.Width = BattalionWidth(s, resB);
                }
                else
                {
                    r.Phase = RegPhase.Defend;
                    resB.Task = TacTask.Reserve; resB.Role = TacRole.Counter; resB.Width = 0;
                    resB.Point = ClampMap(w.Map, r.Point - dir * MathF.Max(10f, T.Depth * R));
                }
            }
            UpdateFireTarget(w, s, r, fronts, dir, R, arty, e);
        }
        if (resB != null && r.Task == TacTask.Attack)
        {
            resB.Task = TacTask.Reserve; resB.Role = TacRole.Line; resB.HoldFire = false; resB.Split = false; resB.Dir = dir; resB.Width = 0;
            resB.Point = ClampMap(w.Map, r.Point - dir * MathF.Max(12f, T.Depth * R * 1.2f));
        }
        // 후퇴 중인 · 후퇴를 마친 대대
        foreach (var b in bats)
        {
            if (fronts.Contains(b) || b == resB) continue;
            b.Role = TacRole.Line; b.HoldFire = false; b.Split = false; b.Dir = dir; b.Width = 0;
            if (b.Spent && b.FallbackUntil > w.Tick) { if (b.Task != TacTask.Fallback) { b.Task = TacTask.Fallback; b.Point = ClampMap(w.Map, b.Center - dir * (R * 1.6f)); } }
            else { b.Task = TacTask.Reserve; b.Point = ClampMap(w.Map, r.Point - dir * (R * 1.4f) + side * 8f * s.FlankSign); }
        }
    }

    /// <summary>
    /// 연대 예비 대대 역습 결심 (방어): 돌파 (주저항선에 닿거나 넘은 적, 표적 둘레 n 명 · 제압 합 supp) 또는 공격이 꺾인 순간 (near — 주저항선 앞 사거리 안, 정면 전체 보이는 적 wide).
    /// counterOdds &gt; 0 (3B S8): 예비 ≥ 적 × counterOdds, 제압된 적 (돌파 0.35 · 꺾임 chargeSuppression) 이면 × 0.6. 0 = 2단계 문턱 (돌파 1/1.2 · 제압 0.35, 꺾임 1.25 · 제압 0.67).
    /// </summary>
    internal static bool CounterGo(bool penetration, bool near, int n, float supp, int wide, int res, float counterOdds, float chargeSupp)
    {
        if (n <= 0) return false;
        float sr = supp / n, co = counterOdds;
        bool pen = co > 0 ? n * co <= res || sr >= 0.35f && n * co * 0.6f <= res : n <= res * 1.2f || sr >= 0.35f;
        bool broken = near && (co > 0 ? wide * co <= res || sr >= chargeSupp && wide * co * 0.6f <= res : wide <= res * 0.8f || sr >= chargeSupp && wide <= res * 1.5f);
        return penetration && pen || broken;
    }

    /// <summary>대대 정면 배분 (연대 폭 안에서 좌우 순서 유지). 공격의 측면 기동 대대는 비스듬히 바깥으로.</summary>
    void LayOut(World w, TacFaction s, TacRegiment r, List<TacBattalion> fronts, TacTask task, Vector2 dir, Vector2 side, float R)
    {
        fronts.Sort((a, b) => { int c = Vector2.Dot(a.Center, side).CompareTo(Vector2.Dot(b.Center, side)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
        var widths = new float[fronts.Count];
        float total = 0;
        for (int i = 0; i < fronts.Count; i++) { widths[i] = BattalionWidth(s, fronts[i]); total += widths[i]; }
        total += 6f * Math.Max(0, fronts.Count - 1);
        float k = r.Width > 0 && total > r.Width ? r.Width / total : 1f;
        float off = -total * k / 2f;
        int outer = s.FlankSign > 0 ? fronts.Count - 1 : 0;
        for (int i = 0; i < fronts.Count; i++)
        {
            var b = fronts[i];
            float bw = widths[i] * k;
            Vector2 pt = r.Point + side * (off + bw / 2f);
            off += bw + 6f * k;
            if (task == TacTask.Attack && b.Role == TacRole.Manoeuvre && s.Profile.Strategy.Scheme == "flank" && !s.Profile.Strategy.Manoeuvre && i == outer && fronts.Count >= 2)
                pt += side * (s.FlankSign * bw * 0.5f) + dir * (R * 0.3f);   // 측면 기동
            if (b.FallbackUntil > w.Tick) continue;
            b.Task = task; b.Point = ClampMap(w.Map, pt); b.Dir = dir; b.Width = bw;
        }
    }

    /// <summary>접촉 시 공격 역할: 앞 대대 2개 이상이면 사격 진지 점수 (TacMap: 엄폐 · 고지 · 적 사선) 가 가장 좋은 대대 = 화력 기반, 나머지 = 기동. 1개면 대대 안에서 나눈다.</summary>
    /// 결정 12 (2026-10-19): 전투 대대 (소총 중대 병력 ≥ battalionRoles.minStrength) 만 화력 기반 · 기동 역할을 받는다 — 1명 분견대 · 전문 병과만 있는 대대는 구분 없음 (Line).
    /// 전투 대대가 사실상 하나면 그 대대 안에서 중대를 화력 기반 · 기동으로 나눈다 (Split).
    internal void AssignAttackRoles(World w, TacFaction s, List<TacBattalion> fronts, Vector2 enemy)
    {
        var rules = w.TacRules.BattalionRoles;
        var combat = new List<TacBattalion>();
        foreach (var b in fronts)
        {
            if (CombatBattalion(s, b, rules)) combat.Add(b);
            else { b.Role = TacRole.Line; b.Split = false; }
        }
        if (combat.Count == 0) return;
        if (combat.Count == 1) { combat[0].Role = TacRole.Line; combat[0].Split = true; return; }
        if (combat.Exists(b => b.Role == TacRole.BaseOfFire))
        {
            foreach (var b in combat) if (b.Role == TacRole.Line) { b.Role = TacRole.Manoeuvre; b.Split = false; }
            return;
        }
        var tm = TacMap.For(w.Map);
        TacBattalion? best = null; float bs = float.MinValue;
        foreach (var b in combat)
        {
            float sc = tm.ScoreAt(b.Point, enemy, s.Profile.Tactics) + b.Alive * 0.002f;
            if (sc > bs || (sc == bs && best != null && b.Id < best.Id)) { bs = sc; best = b; }
        }
        foreach (var b in combat) { b.Role = b == best ? TacRole.BaseOfFire : TacRole.Manoeuvre; b.Split = false; }
    }

    /// <summary>전투 대대: 소속 소총 중대 (TacKind.Line) 의 살아 있는 병력 합이 minStrength 이상 (전문 병과 · 화력 · 마차 묶음은 세지 않음).</summary>
    internal static bool CombatBattalion(TacFaction s, TacBattalion b, BattalionRolesDef rules)
    {
        int n = 0;
        foreach (var g in s.Groups) if (g.Battalion == b.Id && g.Kind == TacKind.Line) n += g.Alive;
        return n >= rules.MinStrength;
    }

    bool ArtilleryReady(World w, TacFaction s, TacRegiment r)
    {
        foreach (var g in s.Groups)
            if (g.Kind == TacKind.Artillery && g.Supports == r.Id && g.State == TacState.Engage && PrepFired(w, s, g, r)) return true;   // N20: 준비사격이 실제로 나갔을 때만 (TacticalAi.Artillery.cs)
        return false;
    }

    /// <summary>
    /// 지원 포병의 표적 기준점: 연대 정면의 적 무리 중심. 아군 기동 (돌격 · 역습) 중대가 그 점에서 liftDistance 안이면 연신 —
    /// 적 쪽으로 더 깊은 적 (0.6 사거리 이상 뒤) 을 표적, 없으면 기준점 없음 (엔진 자동 표적). 엔진에는 아군 오사가 없어서 연신은 화력을 2선 · 예비로 옮기는 효과.
    /// </summary>
    void UpdateFireTarget(World w, TacFaction s, TacRegiment r, List<TacBattalion> fronts, Vector2 dir, float R, bool arty, Unit? e)
    {
        if (!arty || e == null || r.Phase == RegPhase.Approach) { r.HasFireTarget = false; r.Lifted = false; return; }
        var basis = Grid.Nearest(r.Point, TacGrid.Infantry) ?? e;
        var st = Grid.Stats(basis.Pos, 16f * K);
        Vector2 cluster = st.Count > 0 ? st.Sum / st.Count : basis.Pos;
        float lift = s.Profile.Tactics.LiftDistance * K;   // 칸 수는 소총 사거리 배수로 (사거리 24 → 15칸)
        bool close = false;
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Line || g.Pending || g.Role is TacRole.BaseOfFire or TacRole.MainLine) continue;
            int reg = RegimentOfGroup(s, g);
            if (reg != r.Id) continue;
            // 가까워졌거나, 마지막 돌격 (착검 돌격 · 돌격 약진) 에 들어갔으면 연신 — 교전 거리 (사거리 ×0.72) 에서 멈춰 쏘는 동안은 연신하지 않는다
            if (Vector2.Distance(g.Center, cluster) < lift || g.State is TacState.Charge or TacState.Assault && Vector2.Distance(g.Center, cluster) < lift * 2f) { close = true; break; }
        }
        if (!close) { r.FireTarget = cluster; r.HasFireTarget = true; r.Lifted = false; return; }
        if (!r.Lifted) { r.Lifted = true; s.Lifts++; }
        var deep = Grid.Deepest(cluster, 40f * K, dir);
        if (deep != null && Vector2.Dot(deep.Pos - cluster, dir) >= R * 0.6f) { r.FireTarget = deep.Pos; r.HasFireTarget = true; }
        else r.HasFireTarget = false;
    }

    // ================================================================ 대대 실행 층 (≈1초)

    [NonSerialized] readonly List<TacGroup> lineBuf = new(), frontG = new(), fireBuf = new(), armorBuf = new();

    void BattalionThink(World w, TacFaction s, TacBattalion b)
    {
        var T = s.Profile.Tactics;
        var S = s.Profile.Strategy;
        if (b.Task == TacTask.None) return;
        float R = s.RifleRange > 0 ? s.RifleRange : 16f;
        Vector2 dir = SafeNorm(b.Dir, s.Dir), side = Perp(dir);
        var lines = lineBuf; lines.Clear();
        foreach (var g in s.Groups) if (g.Battalion == b.Id && g.Kind == TacKind.Line && !g.Pending && g.Element != ManElement.Manoeuvre) lines.Add(g);   // 기동 제대 중대는 연대 계획이 직접 (stage 3A)
        RotateUpkeep(w, lines);
        ReserveRedesignateStep(w, s, b, lines, R);   // 3B: 조용해지면 예비 재지정 (TacticalAi.Reserve3B.cs)
        // 예비 중대: 우세하면 결정적 순간 (적 제압 = 돌파 기회) 까지 보유, 대등 · 열세 접촉이면 곧바로 측면 연장으로 투입 (작은 대등전에서 예비는 손해)
        bool holdReserve = S.Reserve && lines.Count >= 3 && b.Task is TacTask.Attack or TacTask.Defend && !b.ReserveCommitted && b.Role != TacRole.BaseOfFire;
        if (holdReserve && (b.ReserveGroup == 0 || !lines.Exists(g => g.Id == b.ReserveGroup)))
            b.ReserveGroup = lines.OrderBy(g => Vector2.Dot(g.Center, dir)).ThenBy(g => g.Id).First().Id;
        if (holdReserve && ReserveEventStep(w, s, b, lines, dir, side, R)) { }   // 3B S3: 틈 · 돌파 · 측면 사건 투입 (방어)
        else if (holdReserve)
        {
            int fa = 0, fi = 0; bool broke = false; long engagedSince = long.MaxValue;
            Vector2 fcen = Vector2.Zero; int fcn = 0;
            foreach (var g in lines)
                if (g.Id != b.ReserveGroup)
                {
                    fa += g.Alive; fi += g.Initial; broke |= g.State == TacState.Fallback;
                    fcen += g.Center; fcn++;
                    if (g.State is TacState.Engage or TacState.Bound or TacState.Assault or TacState.Approach && g.Task == TacTask.Attack && Grid.Nearest(g.Center, TacGrid.Any) is { } ge && Vector2.Distance(ge.Pos, g.Center) < R * 1.3f)
                        engagedSince = Math.Min(engagedSince, g.StateTick);
                }
            bool engaged = engagedSince != long.MaxValue;
            var st = fcn > 0 ? Grid.Stats(fcen / fcn, R * 1.6f) : default;
            bool even = engaged && w.Tick - engagedSince > Sec(3) && st.Count >= fa * 0.8f;
            bool exploit = engaged && st.Count > 0 && st.Supp / st.Count >= T.ChargeSuppression;
            bool longFight = engaged && w.Tick - engagedSince > Sec(25f - 12f * T.Aggressiveness);
            bool commit = broke || (fi > 0 && fa < fi * 0.75f);
            if (b.Task == TacTask.Attack) commit |= longFight || exploit || even;
            if (commit)
            {
                if (even && !exploit && !broke && b.Task == TacTask.Attack) b.FlankGroup = b.ReserveGroup;
                b.ReserveCommitted = true; b.ReserveGroup = 0; s.ReserveCommits++;
            }
            else AmmoRotateStep(w, s, b, lines);   // 3B: 저탄약 교대
        }
        else if (!b.ReserveCommitted) b.ReserveGroup = 0;
        if (b.FlankGroup != 0 && (!lines.Exists(g => g.Id == b.FlankGroup) || b.Task != TacTask.Attack)) b.FlankGroup = 0;   // 측면 연장은 공격만 (방어로 바뀌면 앞줄로 — engage: 방어 측면 연장 중대가 주저항선 진지를 못 받던 것)
        var front = frontG; front.Clear();
        foreach (var g in lines) if (g.Id != b.ReserveGroup && g.Id != b.FlankGroup && g.Id != b.PlugGroup) front.Add(g);
        front.Sort((x, y) => { int r = SlotKey(w, x, side).CompareTo(SlotKey(w, y, side)); return r != 0 ? r : x.Id.CompareTo(y.Id); });   // 교대로 들어오는 중대는 받은 칸 기준 (3B)
        float total = 0;
        foreach (var g in front) total += Frontage(g);
        total += 2f * Math.Max(0, front.Count - 1);
        float k = b.Width > 0 && total > b.Width ? b.Width / total : 1f;
        float off = -total * k / 2f;
        Vector2 frontSum = Vector2.Zero; int fn = 0;
        // Split (연대에 앞 대대 하나): 사격 진지 점수가 좋은 중대 = 화력 기반, 나머지 = 기동 (한 번 고르면 살아 있는 동안 유지)
        if (b.Split && front.Count >= 2 && (b.BaseGroup == 0 || !front.Exists(g => g.Id == b.BaseGroup)))
        {
            var e0 = Grid.Nearest(b.Point, TacGrid.Infantry);
            var tm = TacMap.For(w.Map);
            float bs = float.MinValue; int bid = 0;
            float o2 = -total * k / 2f;
            foreach (var g in front)
            {
                float gw = Frontage(g) * k;
                Vector2 pt = b.Point + side * (o2 + gw / 2f);
                o2 += gw + 2f * k;
                float sc = e0 != null ? tm.ScoreAt(pt, e0.Pos, T) : 0;
                if (sc > bs) { bs = sc; bid = g.Id; }
            }
            b.BaseGroup = bid;
        }
        if (!b.Split) b.BaseGroup = 0;
        // 방어 정면 (engage, 2026-10-19): 방어 중대 정면 폭 상한 = defendSpacing × 사거리 — 이웃 중대 중심 거리가 유효 사거리 안 (상호 지원). 대대 정면을 그만큼 좁힌다
        // 참호선이 있는 정면은 참호 배치가 간격을 정한다 (상한을 걸면 중대가 참호 사이 빈터로 몰렸다 — engage 측정 T 2:1: 상한 켬 공격 6/6 · K/E 2.1, 끔 3/6 · 1.4)
        float dcap = b.Task == TacTask.Defend && T.DefendSpacing > 0 && w.NearestTrench(b.Point, MathF.Max(TrenchSearch, T.TrenchSearch * R) + b.Width * 0.5f) == null ? T.DefendSpacing * R : float.MaxValue;
        if (dcap < float.MaxValue)
        {
            float t2 = 0;
            foreach (var g in front) t2 += MathF.Min(Frontage(g) * k, dcap);
            t2 += 2f * k * Math.Max(0, front.Count - 1);
            off = -t2 / 2f;
        }
        foreach (var g in front)
        {
            float gw = MathF.Min(Frontage(g) * k, dcap);
            Vector2 pt = b.Point + side * (off + gw / 2f);
            off += gw + 2f * k;
            g.Task = b.Task;
            g.Role = b.Split && front.Count >= 2 ? (g.Id == b.BaseGroup ? TacRole.BaseOfFire : TacRole.Manoeuvre) : b.Role;
            if (g.Element == ManElement.Fix) g.Role = TacRole.BaseOfFire;   // 고정 제대: 기동 제대가 도착해 풀릴 때까지 사격만 (stage 3A)
            g.HoldFire = b.HoldFire && g.Role != TacRole.BaseOfFire;
            g.TaskDir = dir;
            g.TaskWidth = gw;
            pt = ClampMap(w.Map, pt);
            if (b.Task == TacTask.Defend) pt = MainLinePoint(w, s, g, pt, dir, gw, R);
            g.TaskPoint = pt;
            frontSum += g.Center; fn++;
        }
        if (b.ReserveGroup != 0 && lines.Find(g => g.Id == b.ReserveGroup) is { } rg)
        {
            rg.Task = TacTask.Reserve; rg.Role = b.Task == TacTask.Defend ? TacRole.Counter : TacRole.Line; rg.HoldFire = false;
            rg.TaskPoint = ClampMap(w.Map, b.Point - dir * MathF.Max(10f, T.Depth * R * 0.75f)); rg.TaskDir = dir; rg.TaskWidth = Frontage(rg);
        }
        if (b.FlankGroup != 0 && lines.Find(g => g.Id == b.FlankGroup) is { } fg)
        {
            // 측면 연장: 앞줄 바깥 (측면 쪽) 에 붙여 적 정면보다 넓게 — 감싸기
            fg.Task = b.Task; fg.Role = TacRole.Manoeuvre; fg.HoldFire = b.HoldFire; fg.TaskDir = dir; fg.TaskWidth = Frontage(fg);
            fg.TaskPoint = ClampMap(w.Map, b.Point + side * (s.FlankSign * (total * k / 2f + Frontage(fg) / 2f + 3f)) + dir * 2f);
        }
        PlugLayout(w, s, b, lines, dir);   // 3B S3: 사건 투입 중대
        Vector2 frontC = fn > 0 ? frontSum / fn : b.Center;
        // 붙은 화력 · 기갑 · 마차
        var fire = fireBuf; fire.Clear(); var armor = armorBuf; armor.Clear();
        foreach (var g in s.Groups)
        {
            if (g.Battalion != b.Id) continue;
            if (g.Kind == TacKind.Fire) fire.Add(g);
            else if (g.Kind == TacKind.Armor) armor.Add(g);
            else if (g.Kind == TacKind.Wagon)
            {
                g.Task = b.Task; g.TaskDir = dir; g.TaskWidth = 0;
                g.TaskPoint = ClampMap(w.Map, frontC - dir * 9f);
            }
        }
        float bw = MathF.Max(b.Width, 12f);
        for (int i = 0; i < fire.Count; i++)
        {
            var g = fire[i];
            float o = fire.Count == 1 ? 0 : (i - (fire.Count - 1) / 2f) * (bw / fire.Count);
            g.Task = b.Task == TacTask.Reserve && b.Role == TacRole.Counter ? TacTask.Defend : b.Task;   // 역습 예비 대대의 기관총은 진지에 남는다
            g.Role = TacRole.BaseOfFire; g.TaskDir = dir; g.TaskWidth = MathF.Min(10f, g.Alive * 1.2f);
            g.TaskPoint = ClampMap(w.Map, b.Point - dir * 4f + side * o);
        }
        for (int i = 0; i < armor.Count; i++)
        {
            var g = armor[i];
            float o = armor.Count == 1 ? 0 : (i - (armor.Count - 1) / 2f) * (bw / armor.Count);
            g.Task = b.Task; g.TaskDir = dir; g.TaskWidth = MathF.Min(10f, g.Alive * 1.6f);
            g.TaskPoint = ClampMap(w.Map, b.Point + side * o);
        }
    }

    /// <summary>주저항선 중대 진지: 정면 위치 둘레 (깊이 ±0.5 사거리) 에서 TacMap 점수 (엄폐 · 고지 · 앞쪽 사선 · 위협) 가 좋은 칸. 기준점이 4칸 넘게 바뀔 때만 다시 고른다.</summary>
    Vector2 MainLinePoint(World w, TacFaction s, TacGroup g, Vector2 pt, Vector2 dir, float gw, float R)
    {
        if (g.PickKind == 2 && Vector2.Distance(g.PickBase, pt) <= 4f) return g.PickResult;
        var tm = TacMap.For(w.Map);
        Vector2 notional = pt + dir * (R * 1.2f);
        Vector2 p = tm.PickPosition(pt, notional, R * 1.2f, MathF.Max(8f, gw * 0.5f), s.Profile.Tactics, Grid, R);
        float depth = Vector2.Dot(p - pt, dir);
        float lim = R * 0.5f;
        if (MathF.Abs(depth) > lim) p -= dir * (depth - MathF.Sign(depth) * lim);
        float lat = Vector2.Dot(p - pt, Perp(dir));
        if (MathF.Abs(lat) > gw * 0.5f) p -= Perp(dir) * (lat - MathF.Sign(lat) * gw * 0.5f);
        p = ClampMap(w.Map, p);
        g.PickKind = 2; g.PickBase = pt; g.PickResult = p;
        return p;
    }
}
