using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 중대 실행 층 (≈0.5초): 소총 중대 = 사격 진지 (TacMap) · 분대 교대 약진 (사격과 기동) · 사수 · 사격선 전진 · 착검 돌격 · 방어 참호 · 전초 분대 · 후퇴,
/// 병과 = 기관총 화력 기반 거치 · 곡사포 (주공 연대 지원 — 준비사격 · 연신 강제 표적) · 기병 · 기갑 · 마차 · 정찰.
/// </summary>
public sealed partial class TacticalAi
{
    readonly List<Unit> outpostMem = new();

    /// <summary>
    /// tweak1 (사용자 결정 2a): 중대 자세를 과제에 맞춘다 — 공격 = Attack (맞아도 엄폐로 빠지지 않음), 방어 · 예비 · 후퇴 = Defend (피격 엄폐 찾기).
    /// 소총 · 화력 (기관총) 중대만, 바뀔 때만 명령.
    /// </summary>
    void SyncPosture(World w, TacFaction s, TacGroup g)
    {
        if (g.CompanyId == 0 || g.Kind is not (TacKind.Line or TacKind.Fire)) return;
        var want = g.Task switch
        {
            TacTask.Attack => CompanyPosture.Attack,
            TacTask.Defend or TacTask.Reserve or TacTask.Fallback => CompanyPosture.Defend,
            _ => CompanyPosture.None,
        };
        if (s.Profile.Strategy.AmbushHoldFire && w.FindCompany(g.CompanyId) is { } fc && fc.Owner == s.Faction)   // N73 훅: 방어 · 예비 = 응사만 (기본 끔)
        {
            var wantFm = g.Task is TacTask.Defend or TacTask.Reserve ? FireMode.ReturnFire : FireMode.Free;
            var chg = fc.Members.Where(m => m.Alive && m.FireMode != wantFm).ToList();
            if (chg.Count > 0) { w.SetFireMode(s.Faction, chg, wantFm); s.Orders++; }
        }
        if (w.FindCompany(g.CompanyId) is not { } co || co.Owner != s.Faction || co.Posture == want) return;
        w.SetPosture(s.Faction, new[] { co.Id }, want);
        s.Orders++;
    }

    void CompanyThink(World w, TacFaction s, TacGroup g)
    {
        if (g.Pending || g.Task == TacTask.None) return;
        Members(w, s, g, mem);
        if (mem.Count == 0) return;
        SyncPosture(w, s, g);
        var T = s.Profile.Tactics;
        // 전초 분대는 중대 명령에서 뺀다 (나가 있는 동안)
        outpostMem.Clear();
        if (g.OutpostState == 1 && OutpostSquad(w, g) is { } oq)
        {
            foreach (var u in oq.Members) if (u.Alive && u.Owner == s.Faction) outpostMem.Add(u);
            mem.RemoveAll(u => u.Squad == oq);
            if (mem.Count == 0) { mem.AddRange(outpostMem); outpostMem.Clear(); g.OutpostState = 2; }
        }
        else if (g.OutpostState == 1) g.OutpostState = 2;
        float supp = 0, range = 0;
        foreach (var u in mem)
        {
            supp += u.SuppressionRatio;
            if (u.Weapon != null && !u.IsOfficer) range = MathF.Max(range, u.Weapon.Range);
        }
        supp /= mem.Count;
        if (range <= 0) range = 6;
        float strength = g.Initial > 0 ? g.Alive / (float)g.Initial : 1f;

        // 스스로 후퇴 (병력 · 제압 임계)
        if (g.Kind is TacKind.Line or TacKind.Fire or TacKind.Cavalry or TacKind.Armor && g.Task is TacTask.Attack or TacTask.Defend)
        {
            if (g.State == TacState.Fallback)
            {
                if (w.Tick < g.FallbackUntil) { CoverRelease(w, s, g, T); return; }   // T34a 엄호조 이탈 (TacticalAi.Coop3C.cs)
                g.State = TacState.Idle;
                g.NoRetreatUntil = w.Tick + Sec(20);
                g.OrderTick = -100000;
                BeginRegroup(w, s, g);   // 공격 과제 소총 중대: 곧바로 같은 적에게 다시 걸어가지 않고 재정비 (pursuit2)
            }
            else if (w.Tick >= g.NoRetreatUntil)
            {
                float thr = g.Spent ? T.RetreatStrength * 0.5f : T.RetreatStrength;
                bool byCas = strength < thr;
                bool bySupp = !byCas && SuppressionRetreatRoll(w, s, g, T, ContactSuppression(w, mem), 1f - strength);   // 장교의 제압 후퇴 판단 (N25)
                if (byCas || bySupp)
                {
                    if (byCas) g.Spent = true;
                    g.RetreatCause = byCas ? 1 : 2;
                    if (bySupp) s.FallbacksSupp++;
                    if (g.Element == ManElement.Manoeuvre) { g.Element = ManElement.None; g.ManStage = 0; }   // 기동 제대에서 빠진다 (stage 3A)
                    mem.AddRange(outpostMem); outpostMem.Clear();
                    if (g.OutpostState == 1) g.OutpostState = 2;
                    g.SquadBound = 0;
                    // 방어 과제는 다음 진지 (주저항선 깊이의 절반) 까지만 — 18 × K 칸 (27칸) 후퇴는 마을 · 진지를 통째로 비웠다
                    float back = g.Task == TacTask.Defend ? MathF.Min(18f * K, MathF.Max(8f, T.Depth * (s.RifleRange > 0 ? s.RifleRange : 16f) * 0.5f)) : 18f * K;
                    // 방어 과제 (engage): 다음 참호 · 엄폐선이 있으면 그 진지로 (개활지 대신) — 병사마다 자리 배정 후 사수
                    if (g.Task == TacTask.Defend && FallbackPost(w, s, g, SafeNorm(g.TaskDir, s.Dir), back) is Vector2 fp)
                    { w.OccupyPositions(s.Faction, mem, fp, SafeNorm(g.TaskDir, s.Dir), MathF.Max(8f, Frontage(g) * 0.5f + 3f), T.CoverMaxHit); g.Dug = true; g.OccPost = fp; s.Orders++; }
                    else
                    {
                        // N79a: 전술 AI 도 자기 중대의 집결 지점을 정한다 — 전선 뒤 (기본 후퇴 위치와 같은 곳) 에 두고 거기로 후퇴 (집결 지점이 이 후퇴의 목적지)
                        var rp = ClampMap(w.Map, g.Center - SafeNorm(g.TaskDir, s.Dir) * back);
                        if (g.CompanyId != 0 && w.FindCompany(g.CompanyId) is { } rco && rco.Owner == s.Faction) { w.SetRally(s.Faction, new[] { rco.Id }, rp); retreatUseRally = true; }
                        CoverRetreat(w, s, g, T, mem, rp);   // T34a: 엄호 철수 켬이면 엄호조를 남기고 본대만 (끔 = 전원 Order Retreat)
                        retreatUseRally = false;
                    }
                    g.State = TacState.Fallback; g.StateTick = w.Tick; g.FallbackUntil = w.Tick + Sec(15) + TakeCoverExtra();
                    s.Fallbacks++;
                    return;
                }
            }
        }

        if (g.Kind == TacKind.Line && TrenchAssaultThink(w, s, g)) return;   // N32 참호 돌격 (TacticalAi.TrenchAssault.cs)
        if (g.Kind == TacKind.Line) PickTemplate(w, s, g, range);
        switch (g.Kind)
        {
            case TacKind.Line: LineThink(w, s, g, range, supp); break;
            case TacKind.Fire: FireThink(w, s, g); break;
            case TacKind.Artillery: ArtilleryThink(w, s, g); break;
            case TacKind.Cavalry: CavalryThink(w, s, g); break;
            case TacKind.Armor: ArmorThink(w, s, g); break;
            case TacKind.Wagon: WagonThink(w, s, g); break;
            case TacKind.Scout: ObserverThink(w, s, g); break;
        }
    }

    /// <summary>후퇴 판단의 '접촉 중' 창 (초): 이 안에 공격받은 (Combat3.Suppress — 명중 무관) 병사.</summary>
    public const float ContactWindowSec = 5f;

    /// <summary>
    /// 장교가 보는 중대 제압 (N25): 접촉 중인 병사 (장교 제외 보병, ContactWindowSec 안에 공격받음) 의 평균 제압비. 접촉 병사가 max(3, 보병 25%) 미만이면
    /// 장교 제외 보병 전체 평균 (적에게 닿지 않은 후방 분대가 평균을 희석하지 않게 — 측정: 중대 전체 평균은 교전 중에도 p90 0.18 이라 0.3 시작점에 거의 닿지 않음).
    /// </summary>
    public static float ContactSuppression(World w, List<Unit> mem)
    {
        int n = 0, nc = 0; float all = 0, con = 0;
        long win = Sec(ContactWindowSec);
        foreach (var u in mem)
        {
            if (u.IsOfficer || !u.Def.IsInfantry) continue;
            n++; all += u.SuppressionRatio;
            if (w.Tick - u.LastFiredAtTick < win) { nc++; con += u.SuppressionRatio; }
        }
        if (n == 0) return 0f;
        return nc >= Math.Max(3, (n + 3) / 4) ? con / nc : all / n;
    }

    /// <summary>중대 장교 판단 간격 (CompanyThink 호출 = 30틱).</summary>
    public const float RetreatDecideSec = 0.5f;

    /// <summary>
    /// 제압 후퇴 위험률 (초당, N25 — AiProfile.RetreatSuppression 주석): 평균 제압 supp 가 시작점을 넘으면 sf (0..2) 에 비례, 사상률 (casualty / 사상 임계) 로 커진다.
    /// 지휘: 장교 = 경험 단계 · 계급으로 줄임 (하한 0.3배), 부사관 대리 = ×ncoMul, 지휘 없음 (인계 중 포함) = ×leaderlessMul.
    /// </summary>
    public static float RetreatHazard(TacticsProfile T, float supp, float casualty, CommandLevel level, int xpLevel, int officerRank)
    {
        float span = MathF.Max(0.01f, T.RetreatSuppression - T.RetreatSuppStart);
        float sf = Math.Clamp((supp - T.RetreatSuppStart) / span, 0f, 2f);
        if (sf <= 0 || T.RetreatSuppRate <= 0) return 0f;
        float casThr = MathF.Max(0.05f, 1f - T.RetreatStrength);
        float h = T.RetreatSuppRate * sf * (1f + T.RetreatCasualtyWeight * Math.Clamp(casualty / casThr, 0f, 1f));
        h *= level switch
        {
            CommandLevel.Officer => MathF.Max(0.3f, 1f - T.RetreatXpPerLevel * xpLevel - T.RetreatRankPer * Math.Max(0, officerRank - 1)),
            CommandLevel.Nco => T.RetreatNcoMul,
            _ => T.RetreatLeaderlessMul,
        };
        return h;
    }

    /// <summary>장교 판단 1회의 제압 후퇴 굴림 (결정적 해시: 틱 · 세력 · 그룹 · 지도 시드). 확률 = min(1, 위험률 × 판단 간격). 중대가 없는 분견대는 지휘 없음으로 본다.</summary>
    bool SuppressionRetreatRoll(World w, TacFaction s, TacGroup g, TacticsProfile T, float supp, float casualty)
    {
        if (supp <= T.RetreatSuppStart) return false;
        var co = g.CompanyId > 0 ? w.FindCompany(g.CompanyId) : null;
        var level = co != null ? w.CommandLevelOf(co) : CommandLevel.None;
        var cmd = co?.Commander;
        float p = MathF.Min(1f, RetreatHazard(T, supp, casualty, level, w.XpLevel(cmd), cmd?.OfficerRank ?? 0) * RetreatDecideSec * w.BearerRetreatMulOf(co));   // N68 기수
        return p > 0 && RetreatRoll(w.Tick, s.Faction, g.Id, w.Map.Seed) < p;
    }

    /// <summary>[테스트] 제압 후퇴 굴림의 난수 [0,1) — 틱 · 세력 · 그룹 · 시드의 해시.</summary>
    public static float RetreatRoll(long tick, int faction, int groupId, int seed) => Hash01((int)(tick * 31 + faction * 7919), groupId * 0x2545 ^ seed * 0x3B9 ^ 0x5A7E);

    /// <summary>
    /// 소총 중대 전술 틀 (tactics.json): 방어 과제 = 종심 방어, 공격 과제 = 화력-기동, 그 밖 = 틀 없음.
    /// 배치는 다음 중대 이동 (깃발 배치) 부터, 접촉하면 중대 지휘관이 틀 행동 (Core/TacticDrill.cs) 을 실행하고 AI 는 그동안 접근 · 약진 · 돌격 명령을 내지 않는다
    /// (후퇴 · 과제 변경만 — 일반 이동 명령은 엔진 행동을 멈춘다). 프로필 tactics.templates = false 면 손대지 않음.
    /// </summary>
    void PickTemplate(World w, TacFaction s, TacGroup g, float range)
    {
        if (g.CompanyId <= 0 || w.FindCompany(g.CompanyId) is not { } co || co.Owner != s.Faction) return;
        // F1 2차: AI 중대 대형 유지 (명령 — 리플레이 기록): looseAdvanceAi 끔 (기본) 이면 늘, 켜도 도하 중에는 (다리 · 여울을 깃발 종대로)
        bool keep = !w.Defs.CombatRules.LooseAdvanceAi || g.CrossStage is CsAssemble or CsTransit or CsBridgehead or CsFireBase;
        if (co.KeepFormation != keep) w.SetKeepFormation(s.Faction, new[] { co.Id }, keep);
        if (!s.Profile.Tactics.Templates) return;
        // 공격: 접촉 (구성원이 표적을 잡음) 뒤에만 화력-기동 (한 번 켜면 과제가 바뀔 때까지 유지) — 접근 중에 틀 배치로 바꾸면 역할군 재정렬로 깃발이 멈춰
        // 중대가 늦고 기관총 화력 기반만 앞서 나갔다 (2026-10-19 측정). 방어: 처음부터 종심 방어 (진지로 가는 이동이 곧 배치).
        bool contact = co.Tactic == CompanyTactic.FireManoeuvre || co.Members.Exists(u => u.Target != null);   // 재정비 중 (pursuit2) 에는 틀을 끔 — 화력-기동의 약진 · 우회가 재전진이 되지 않게   // 실제 교전 (구성원이 표적을 잡음) — 그 뒤 이동은 틀 배치 없이 (CompanyLayout.InContact)
        if (g.CrossStage is CsAssemble or CsTransit or CsBridgehead or CsFireBase || g.CounterAssaultTick != 0) { if (co.Tactic != CompanyTactic.None && co.Tactic != CompanyTactic.Artillery) w.SetCompanyTactic(s.Faction, new[] { co }, CompanyTactic.None); return; }   // 도하 단계: 틀 없이 AI 가 직접
        var want = g.Task switch { TacTask.Defend => CompanyTactic.DefenceDepth, TacTask.Attack when contact && g.RegroupUntil == 0 && !PlanHolding(g) && w.Defs.CombatRules.AiContactTemplates => CompanyTactic.FireManoeuvre, _ => CompanyTactic.None };   // 기동 계획 제대 (고정 · 기동) 는 풀릴 때까지 틀 행동 없음 (stage 3A)
        if (co.Tactic != want && co.Tactic != CompanyTactic.Artillery) w.SetCompanyTactic(s.Faction, new[] { co }, want);
    }

    /// <summary>[중화기 규칙] p 를 진행 방향 dir 기준 아군 보병 (중화기 · 장교 · 정찰 제외) 앞선에서 back 칸 뒤까지로 당긴다. 보병이 없으면 그대로.</summary>
    static Vector2 BehindInfantry(World w, TacFaction s, Vector2 p, Vector2 dir, float back)
    {
        if (dir.LengthSquared() < 1e-4f) return p;
        float front = float.MinValue;
        foreach (var u in w.Units)
            if (u.Owner == s.Faction && u.Alive && u.Def.IsInfantry && !u.Def.Heavy && !u.IsOfficer && !u.Def.Scout) front = MathF.Max(front, Vector2.Dot(u.Pos, dir));
        if (front == float.MinValue) return p;
        float over = Vector2.Dot(p, dir) - (front - back);
        return over > 0 ? ClampMap(w.Map, p - dir * over) : p;
    }

    /// <summary>이 중대가 전술 틀을 실행 중 (지휘 능력이 허용한 틀) 인가.</summary>
    static bool Templated(World w, TacGroup g) => g.CompanyId > 0 && w.FindCompany(g.CompanyId) is { ActiveTactic: not CompanyTactic.None and not CompanyTactic.Artillery };

    /// <summary>중대 지휘관이 틀 행동 중 (접촉 — Engage · Defend) 인가: 그동안 AI 는 중대에 세부 명령을 내지 않는다.</summary>
    /// 공격 틀은 약진 · 기동조 우회 · 돌격 중일 때만 (화력 우세 전에는 AI 가 접근 · 사수를 그대로 지휘), 종심 방어는 국면 내내.
    static bool DrillRunning(World w, TacGroup g) => g.CompanyId > 0 && w.FindCompany(g.CompanyId) is { ActiveTactic: not CompanyTactic.None and not CompanyTactic.Artillery, DrillPhase: not DrillPhase.Idle } co
        && (co.DrillPhase == DrillPhase.Defend || co.Drill.BoundSquad > 0 || co.Drill.Man != 0);

    /// <summary>과제 위치로 (대형 유지) 가서 사수. 이미 그 근처면 사수만.</summary>
    void GoAndHold(World w, TacFaction s, TacGroup g, Ord k, TacState arrived, float tol = 4f)
    {
        float d = Vector2.Distance(g.Center, g.TaskPoint);
        if (d > tol)
        {
            if (Retarget(w, g, TacState.Advance, g.TaskPoint, 8f))
            {
                Order(w, s, g, mem, k, g.TaskPoint, g.TaskDir, g.TaskWidth);
                g.State = TacState.Advance; g.StateTick = w.Tick;
            }
            else if (g.State == TacState.Advance && Due(w, g, 30f)) g.OrderTick = -100000;   // 오래 걸리면 다시 명령
            return;
        }
        if (g.State != arrived) { HoldAll(w, s, g, mem); g.State = arrived; g.StateTick = w.Tick; }
    }

    // ---------------------------------------------------------------- 소총 중대

    void LineThink(World w, TacFaction s, TacGroup g, float range, float supp)
    {
        var T = s.Profile.Tactics;
        if (g.CrossStage != CsNone && LineCrossing(w, s, g, range, supp)) return;   // 도하 단계 (TacticalAi.Crossing.cs)
        if (g.Task != TacTask.Attack) g.SquadBound = 0;
        switch (g.Task)
        {
            case TacTask.Reserve:
            {
                if (RotateOutStep(w, s, g)) return;   // 3B: 교대로 나가는 중대는 교전 없이 예비 위치로
                var e0 = Grid.Nearest(g.Center, TacGrid.Infantry);
                float d0 = e0 != null ? Vector2.Distance(e0.Pos, g.Center) : float.MaxValue;
                // 역습 예비 (방어): 가까이 들어온 적이 약하거나 제압됐으면 착검 역습
                if (e0 != null && g.Role == TacRole.Counter && d0 <= range * 0.6f && TryAssault(w, s, g, e0, d0, supp)) return;
                if (g.State == TacState.Assault)
                {
                    bool charging = false;
                    foreach (var u in mem) charging |= u.Charging;
                    if (w.Tick - g.StateTick < Sec(10) && charging) return;
                    g.State = TacState.Idle; g.OrderTick = -100000;
                }
                if (e0 != null && d0 <= range && g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; return; }
                if (g.State == TacState.Engage && (e0 == null || d0 > range + 4f)) g.State = TacState.Idle;
                // 엄폐 뒤 예비 (engage, 2026-10-19): 방어 역습 예비는 예비 위치 둘레 엄폐 · 참호 거점에 자리 잡는다
                if (g.State != TacState.Engage && g.Role == TacRole.Counter && s.Profile.Tactics.DefendCover && Vector2.Distance(g.Center, g.TaskPoint) < 14f)
                {
                    var T0 = s.Profile.Tactics;
                    Vector2 rdir = SafeNorm(g.TaskDir, s.Dir);
                    Vector2? rp = (T0.Trench > 0 ? w.NearestTrench(g.TaskPoint, 8f, rdir) : null) ?? w.CoverCluster(g.TaskPoint, 8f);
                    if (rp is Vector2 rpp && (!g.Dug || Vector2.Distance(rpp, g.OccPost) > 8f))
                    {
                        w.OccupyPositions(s.Faction, mem, rpp, rdir, MathF.Max(8f, Frontage(g) * 0.5f), T0.CoverMaxHit);
                        g.Dug = true; g.OccPost = rpp; g.OrderPoint = rpp; g.OrderTick = w.Tick; g.State = TacState.Reserve; g.StateTick = w.Tick; s.Orders++;
                        return;
                    }
                    if (g.Dug) return;
                }
                if (g.State != TacState.Engage) GoAndHold(w, s, g, Ord.AttackMove, TacState.Reserve, 6f);
                return;
            }
            case TacTask.Fallback:
                if (g.State != TacState.Fallback && g.State != TacState.Reserve) { Order(w, s, g, mem, Ord.Retreat, g.TaskPoint, g.TaskDir, g.TaskWidth); g.State = TacState.Fallback; g.StateTick = w.Tick; }
                else if (g.State == TacState.Fallback && Vector2.Distance(g.Center, g.TaskPoint) < 5f) { HoldAll(w, s, g, mem); g.State = TacState.Reserve; }
                else if (g.State == TacState.Fallback && w.Tick - g.StateTick > Sec(25)) g.State = TacState.Idle;
                return;
        }
        if (g.State == TacState.Fallback || g.State == TacState.Reserve) { g.State = TacState.Idle; g.OrderTick = -100000; }

        // 돌격 중: 10초 (또는 둘레에 적이 없어질 때) 까지 맡겨 둔다. 소총 중대는 깰 수 없는 전차를 표적 삼지 않는다
        bool hurtsArmor = false;
        foreach (var u in mem) if (u.Weapon != null && u.Weapon.CanDamage("armored")) { hurtsArmor = true; break; }
        var e = FixTarget(g, hurtsArmor) ?? PinnedPick(w, g, Grid.Nearest(g.Center, hurtsArmor ? TacGrid.Any : TacGrid.Infantry), T, range);   // 고정 제대는 맡은 구역의 적 (stage 3A) · T34a 엎드린 적 가중
        float d = e != null ? Vector2.Distance(e.Pos, g.Center) : float.MaxValue;
        if (g.State == TacState.Assault)
        {
            bool charging = false;
            foreach (var u in mem) charging |= u.Charging;
            if (w.Tick - g.StateTick < Sec(10) && charging && d < 14f * K) return;
            g.State = TacState.Idle; g.OrderTick = -100000;
        }

        if (g.Task == TacTask.Defend) { LineDefend(w, s, g, range, supp, e, d); return; }
        if (g.OutpostState == 1) RecallOutpost(w, s, g);
        if (g.CounterAssaultTick != 0 && e != null && d <= range + 14f * K) { CounterAssault(w, s, g, e, d, supp); return; }   // 역습 일제 돌격 (TacticalAi.Counter.cs)
        if (Regrouping(w, s, g, range, supp, e, d)) return;
        if (ManoeuvreMove(w, s, g, range, e, d) || FixHold(w, s, g, range) || FixPushStep(w, s, g, range)) return;   // FixPushStep = 3B 고정 제대 밀기   // stage 3A 기동 계획 (TacticalAi.Manoeuvre.cs)
        if (PursueByFire(w, s, g, range, e, d)) return;
        // 틀 행동 중: 화력조 사수 · 사격조 교대 약진 · 기동조 우회 · 돌격은 중대 지휘관 (엔진) 이 한다
        if (DrillRunning(w, g)) { if (g.State != TacState.Engage) { g.State = TacState.Engage; g.StateTick = w.Tick; } g.SquadBound = 0; return; }

        // ---- 교착 해소 (stale): 공격 과제인데 N초 넘게 교전 자세로 서서 아무도 쏘지 않고 과제 위치와 멀면 다시 전진 (방어 · 고수 과제는 여기 오지 않는다)
        if (T.StalemateSeconds > 0 && g.State == TacState.Engage && g.Task == TacTask.Attack && w.Tick - g.StateTick > Sec(T.StalemateSeconds))
        {
            int shooting = 0;
            foreach (var u in mem) if (u.Target != null) shooting++;
            Vector2 stp = FarSide(s, g, g.TaskPoint, w);
            if (shooting == 0 && Vector2.Distance(g.Center, stp) > 12f)
            {
                Order(w, s, g, mem, Ord.AttackMove, stp, g.TaskDir, g.TaskWidth);
                g.State = TacState.Advance; g.StateTick = w.Tick;
                return;
            }
        }
        // ---- 공격
        float engageD = range * T.EngageFrac;
        float contactD = range + 14f * K;   // 접촉 거리 (시야 안이면 거의 늘 — 1단계 규칙, 2단계 실험에서 더 짧게 하면 깃발 횡대로 사격 거리까지 걸어 들어가 손해)
        if (e == null || d > contactD)
        {
            g.SquadBound = 0;
            // 접촉 전: 깃발 횡대로 과제 위치 쪽 (공격 이동 — 도중에 만난 적과는 교전)
            Vector2 tp = FarSide(s, g, g.TaskPoint, w);   // 건넌 중대는 강 건너 쪽만
            if (Vector2.Distance(g.Center, tp) > 4f)
            {
                if (Retarget(w, g, TacState.Advance, tp, 8f))
                {
                    Order(w, s, g, mem, Ord.AttackMove, tp, g.TaskDir, g.TaskWidth);
                    g.State = TacState.Advance; g.StateTick = w.Tick;
                }
            }
            else if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
            return;
        }
        Vector2 toE = SafeNorm(e.Pos - g.Center, g.TaskDir);
        if (g.SquadBound != 0) { if (ContinueSquadBound(w, s, g, e, d, engageD, supp, toE)) return; }
        if (g.State == TacState.Bound)
        {
            if (d > g.BoundDist + 1f && w.Tick - g.StateTick < Sec(6)) return;
            HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick;
            return;
        }
        bool fireOnly = g.Role == TacRole.BaseOfFire || g.HoldFire;
        // 공격 이동은 표적을 잡으면 그 자리에서 멈춰 쏜다 (사거리 끝) — 그 상태로 9초 넘게 절반 넘게 사격 중이면 사격선 (교전) 으로 본다.
        // 그래야 사격선 전진 (분대 교대 약진) · 돌격 판단이 돈다. 그 전에는 교전 거리 (사거리 × engageFrac) 의 사격 진지까지 계속 다가간다.
        // 실험 (전술 대 옛 battle AI 보병 200 대 200 평지 · 지형 각 12판 + yalu_crossing 10판 승수): 3초 (이전) = 평지 6 · 지형 10 · yalu 3, 6초 = 12 · 10 · 5,
        // 9초 = 12 · 9 · 8, 15초 = 11 · 8 · 9, 22초 = 12 · 8 · 7, 판정 없음 = 11 · 9 · 7 (단 교대 약진 · 돌격 판단이 안 돈다). 사거리 24 끝 사격전은 명중률이 낮아 손해였다.
        bool enemyCovered = CoverScore(w.Map, e.Pos) >= CoverScore(w.Map, g.Center) + 0.3f;
        int firing = 0;
        foreach (var u in mem) if (u.Target != null) firing++;
        bool onLine = d <= range + 2f && (g.State == TacState.Engage || g.State == TacState.Approach && firing * 2 >= mem.Count && w.Tick - g.StateTick > Sec(9));
        // 돌격보병 중대 (N23): 사격 진지에서 멈추지 않고 적 너머 4칸으로 공격 이동 (돌격 사격) — 걸으며 쏘고 6칸 권총 · 3칸 수류탄 → 단검 돌격 (엔진 MarchCommand.cs).
        // 살아 쏘는 기관총 둘레 · 화력 기반 · 사격 금지면 보통 소총 중대 규칙.
        if (!fireOnly && SweepGroup(mem) && !Grid.ActiveMgNear(e.Pos, 8f * K))
        {
            Vector2 p = e.Pos + toE * 4f;
            if (Retarget(w, g, TacState.Approach, p, 4f, contactLimit: false))   // 돌격 사격 쓸기는 접적 재명령 제한 없음 (적 너머로 계속)
            {
                Order(w, s, g, mem, Ord.AttackMove, p, toE, g.TaskWidth, marchFire: true);
                g.State = TacState.Approach; g.StateTick = w.Tick; w.Stats.DrillSweeps++;
            }
            return;
        }
        if (d > engageD + 3f && !onLine)
        {
            // 접근: 교전 거리의 사격 진지 (TacMap: 엄폐 · 고지 · 사선 · 위협) 까지 공격 이동 (도중에 적을 잡으면 멈춰 쏜다).
            // 분대 교대 약진은 여기서 쓰지 않는다 — 실험 (2단계, 지형 보병 200 대 200 12판): 화력 우세 전 사거리 안 교대 약진은 1승 11패 (이동 분대는 쏘지 않고 조준이 풀림).
            // 교리대로 화력 우세를 얻은 뒤의 사격선 전진 (아래 bound) 에만 쓴다.
            Vector2 p = FiringPosition(w, s, g, e, engageD, range);
            if (CoverHop(w, g, p, e.Pos) is Vector2 hop) p = hop;   // 엄폐에서 엄폐로 (engage, 가벼운 판): 가는 길에 엄폐가 있으면 먼저 그리로
            if (Retarget(w, g, TacState.Approach, p, 6f))
            {
                Order(w, s, g, mem, Ord.AttackMove, p, toE, g.TaskWidth, marchFire: true);   // 접근 = 돌격 사격 허용
                g.State = TacState.Approach; g.StateTick = w.Tick;
            }
            return;
        }
        // 교전 거리: 멈춰 사격
        if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
        if (fireOnly) return;   // 화력 기반 · 준비사격 중: 사격만
        if (TryAssault(w, s, g, e, d, supp, attack: true)) return;
        // 사격선 전진 (bound): 한동안 사격한 뒤 제압이 낮고 유리하면 몇 칸 앞으로 — 분대 교대 (행진 사격 유닛은 걸으며 쏜다)
        long boundAfter = Sec(14f - 10f * T.Aggressiveness);
        if (d > T.ChargeRange * K + 1f && w.Tick - g.StateTick > boundAfter && T.Aggressiveness > 0.1f && BoundOk3C(w, T, g, e, supp))   // T34a: 제압 상한 · 아군 엎드림 · 우세 문턱 (끔 = supp < 0.4 · Favourable)
        {
            float bd = MathF.Max(T.ChargeRange * K - 1f, d - 6f * K);
            Vector2 p = BestCover(w.Map, g.Center + toE * (d - bd), 2, e.Pos);
            // 엄폐 없는 곳으로 걸어 나가는 것은 적이 뚜렷이 제압됐거나 (≥ 돌격 제압 기준) 크게 우세할 때만 (평지 실험: 대등 사격전 전진 = 손해)
            var est = Grid.Stats(e.Pos, 12f * K);
            bool better = CoverScore(w.Map, p) > CoverScore(w.Map, g.Center) + 0.3f;
            bool pinned = est.Count > 0 && est.Supp / est.Count >= T.ChargeSuppression;
            bool strong = g.Alive >= est.Count * 1.6f;
            // 참호 · 엄폐 속 적과의 사거리 끝 사격전은 이길 수 없다 (yalu_crossing: 사거리 24 에서 버티다 93 → 26 으로 녹음) — 오래 쐈으면 다가간다
            bool dugIn = enemyCovered && w.Tick - g.StateTick > boundAfter * 2;
            if (!better && !pinned && !strong && !dugIn) return;
            // N55: 경험 적은 지휘관은 중대 전체 돌격 사격 전진 (공격 이동 + 행진 사격), 경험 많은 지휘관은 분대 교대 약진 (OfficerStyle.cs). 지휘 없는 중대는 행진 사격을 못 하므로 약진
            var bco = g.CompanyId != 0 ? w.FindCompany(g.CompanyId) : null;
            bool marchStyle = bco != null && w.CommandLevelOf(bco) != CommandLevel.None && w.PrefersMarchFire(bco);
            if (T.FireAndMovement && !marchStyle && StartSquadBound(w, s, g, p, toE, range, e)) { g.BoundDist = bd; return; }
            if (marchStyle) { Order(w, s, g, mem, Ord.AttackMove, p, toE, g.TaskWidth, marchFire: true); w.Stats.DrillMarchAdvances++; }
            else Order(w, s, g, mem, Ord.Move, p, toE, g.TaskWidth);
            g.State = TacState.Bound; g.StateTick = w.Tick; g.BoundDist = bd;
        }
    }

    /// <summary>
    /// 엄폐 경유점 (engage, 2026-10-19 — 공격 이동의 엄폐에서 엄폐로, 가벼운 판): 중대 중심 → 사격 진지 직선 위 4칸 앞부터 3칸 간격으로 둘레 2칸의 엄폐를 훑어
    /// 지금보다 엄폐 점수가 0.3 이상 좋고 0.5 이상인 첫 칸. 사격 진지가 10칸 안이거나 없으면 null. (본격 P1/P2 엄폐 도약은 3단계)
    /// </summary>
    static Vector2? CoverHop(World w, TacGroup g, Vector2 dest, Vector2 enemy)
    {
        float dist = Vector2.Distance(g.Center, dest);
        if (dist <= 10f) return null;
        Vector2 dir = (dest - g.Center) / dist;
        float here = CoverScore(w.Map, g.Center);
        for (float t = 4f; t <= dist - 5f; t += 3f)
        {
            Vector2 q = BestCover(w.Map, g.Center + dir * t, 2, enemy);
            float c = CoverScore(w.Map, q);
            if (c >= 0.5f && c >= here + 0.3f && Vector2.Distance(q, g.Center) >= 4f) return q;
        }
        return null;
    }

    /// <summary>표적 둘레 적 무리가 후퇴 중인가 (표적 자신이 후퇴 중이거나 둘레 보이는 적의 fleeingShare 이상) — 보병 돌격 금지 · 사격 추격 판단.</summary>
    bool FleeingTarget(World w, Unit e, TacGrid.Stat st)
    {
        var P = w.PursuitRules;
        if (!P.NoChargeFleeing) return false;
        return World.IsFleeing(e) || (st.Count > 0 && st.Fleeing >= st.Count * P.FleeingShare);
    }

    /// <summary>
    /// 사격 추격 (사용자 결정 2026-10-19): 가장 가까운 적 무리가 후퇴 중이면 돌격하지 않고 유효 사거리 (사거리 × rangeFrac) 로 따라가며 쏜다 (공격 이동 — 표적을 잡으면 서서 쏨).
    /// 추격 시작 위치 (자기 선) 에서 적 쪽으로 maxDist × 소총 사거리 넘게, 또는 maxSec 넘게 쫓으면 멈춰 regroupSec 동안 제자리 사수 · 재정비 (대열 정비는 장교 판단).
    /// 후퇴하던 적이 보이지 않게 되어도 추격을 끝내고 재정비 (보이지 않는 적을 쫓아 들어가지 않는다). 처리했으면 true.
    /// </summary>
    bool PursueByFire(World w, TacFaction s, TacGroup g, float range, Unit? e, float d)
    {
        var P = w.PursuitRules;
        if (!P.Enabled) return false;
        if (g.PursuitHaltUntil > w.Tick)
        {
            if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
            return true;
        }
        float contactD = range + 14f * K;
        bool fleeing = false;
        if (e != null && d <= contactD)
        {
            var st = Grid.Stats(e.Pos, P.FleeingRadius * K);
            fleeing = World.IsFleeing(e) || (st.Count > 0 && st.Fleeing >= st.Count * P.FleeingShare);
        }
        if (!fleeing)
        {
            if (g.PursuitTick == 0) return false;
            g.PursuitTick = 0;
            if (e != null && d <= contactD) return false;   // 적이 멈춰 섰다 (재집결) → 보통 교전
            return Halt(w, s, g, P);                        // 놓쳤다 → 재정비
        }
        Vector2 toE = SafeNorm(e!.Pos - g.Center, g.TaskDir);
        if (g.PursuitTick == 0) { g.PursuitTick = w.Tick; g.PursuitOrigin = g.Center; s.Pursuits++; }
        float maxD = P.MaxDist * (s.RifleRange > 0 ? s.RifleRange : 16f);
        float ahead = Vector2.Dot(g.Center - g.PursuitOrigin, toE);
        if (w.Tick - g.PursuitTick > Sec(P.MaxSec) || ahead >= maxD) { g.PursuitTick = 0; return Halt(w, s, g, P); }
        float want = range * P.RangeFrac;
        if (d > want + 2f)
        {
            Vector2 p = e.Pos - toE * want;
            float over = Vector2.Dot(p - g.PursuitOrigin, toE) - maxD;   // 한계 너머로 명령하지 않음
            if (over > 0) p -= toE * over;
            if (Vector2.Distance(p, g.Center) > 3f && Retarget(w, g, TacState.Approach, p, 4f))
            {
                Order(w, s, g, mem, Ord.AttackMove, p, toE, g.TaskWidth);
                g.State = TacState.Approach; g.StateTick = w.Tick;
            }
        }
        else if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
        return true;
    }

    /// <summary>스스로 후퇴가 끝난 공격 소총 중대 → 재정비 시작 (regroup.enabled false = 옛 동작: 곧바로 재전진, 계수만).</summary>
    void BeginRegroup(World w, TacFaction s, TacGroup g)
    {
        if (g.Kind != TacKind.Line || g.Task != TacTask.Attack) return;
        var R = w.RegroupRules;
        if (!R.Enabled) { s.Readvances++; w.Stats.Readvances++; return; }
        g.RegroupUntil = w.Tick + Sec(R.MinSec);
        g.RegroupCheck = g.RegroupUntil;
        g.RegroupAlive = g.Alive;
        g.SquadBound = 0; g.PursuitTick = 0;
        s.Regroups++;
    }

    /// <summary>
    /// 후퇴 뒤 재정비 (pursuit2, Pursuit.cs RegroupDef): minSec 동안 제자리 사수 (사거리 안 적은 엔진이 쏜다 = 지원 사격), 그 뒤 checkSec 마다 재전진 조건 —
    /// ① 접촉 거리 안 적 없음 ② 병력 ≥ rejoinStrength · 제압 ≤ rejoinSupp · (국지 병력비 ≥ rejoinOdds 또는 지원) ③ 증원 · 제압 ≤ rejoinSupp. 못 맞추면 계속 사수 (지원 사격).
    /// 과제가 바뀌면 (예비 · 후퇴 · 방어) 끝. 스스로 후퇴 규칙 (병력 30~40% 사상 · 장교 제압 판단) 은 그대로 위에서 먼저 돈다. 처리했으면 true.
    /// </summary>
    bool Regrouping(World w, TacFaction s, TacGroup g, float range, float supp, Unit? e, float d)
    {
        if (g.RegroupUntil == 0) return false;
        if (g.Task != TacTask.Attack) { g.RegroupUntil = 0; return false; }
        var R = w.RegroupRules;
        if (w.Tick >= g.RegroupCheck)
        {
            g.RegroupCheck = w.Tick + Sec(MathF.Max(0.5f, R.CheckSec));
            if (RejoinOk(w, s, g, range, supp, e, d, R))
            {
                g.RegroupUntil = 0;
                g.State = TacState.Idle; g.OrderTick = -100000;
                s.Readvances++; w.Stats.Readvances++; w.Stats.RegroupRejoins++;
                return false;
            }
            if (w.Tick >= g.RegroupUntil) { s.RegroupHolds++; w.Stats.RegroupHolds++; }
        }
        if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
        return true;
    }

    bool RejoinOk(World w, TacFaction s, TacGroup g, float range, float supp, Unit? e, float d, RegroupDef R)
    {
        if (w.Tick < g.RegroupUntil) return false;
        float contactD = range + 14f * K;
        if (e == null || d > contactD) return true;   // 적이 접촉 거리에 없음 → 과제 위치로 (같은 적에게 다시 걸어 들어가는 것이 아님)
        if (supp > R.RejoinSupp) return false;
        int init = Math.Max(1, g.Initial);
        if (g.Alive >= g.RegroupAlive + MathF.Max(1f, R.ReinforceFrac * init)) return true;   // 증원
        if (g.Alive < init * R.RejoinStrength) return false;
        var st = Grid.Stats(e.Pos, 12f * K);
        if (st.Count == 0 || g.Alive >= st.Count * R.RejoinOdds) return true;
        return Supported(s, g, e, R);
    }

    /// <summary>지원: 같은 세력 다른 공격 소총 중대 (재정비 · 후퇴 아님) supportMin 개 이상이 적 e 둘레 supportRadius × 소총 사거리 안에서 교전 · 접근 · 돌격 중.</summary>
    bool Supported(TacFaction s, TacGroup g, Unit e, RegroupDef R)
    {
        float r = R.SupportRadius * (s.RifleRange > 0 ? s.RifleRange : 16f);
        int n = 0;
        foreach (var o in s.Groups)
        {
            if (o == g || o.Kind != TacKind.Line || o.Pending || o.Alive <= 0 || o.Task != TacTask.Attack || o.RegroupUntil != 0) continue;
            if (o.State is not (TacState.Engage or TacState.Approach or TacState.Assault or TacState.Bound)) continue;
            if (Vector2.Distance(o.Center, e.Pos) <= r && ++n >= R.SupportMin) return true;
        }
        return false;
    }

    bool Halt(World w, TacFaction s, TacGroup g, PursuitDef P)
    {
        HoldAll(w, s, g, mem);
        g.State = TacState.Engage; g.StateTick = w.Tick; g.SquadBound = 0;
        g.PursuitHaltUntil = w.Tick + Sec(P.RegroupSec);
        s.PursuitHalts++;
        return true;
    }

    /// <summary>공격 사격 진지: TacMap 점수로 고른 칸 (적이 6칸 넘게 움직였을 때만 다시 고른다 — 같은 상태에서 목표가 흔들리지 않게).</summary>
    Vector2 FiringPosition(World w, TacFaction s, TacGroup g, Unit e, float engageD, float range)
    {
        if (g.PickKind == 1 && Vector2.Distance(g.PickBase, e.Pos) <= 6f) return g.PickResult;
        var p = TacMap.For(w.Map).PickPosition(g.Center, e.Pos, engageD, MathF.Max(8f, g.TaskWidth), s.Profile.Tactics, Grid, range);
        p = BestCover(w.Map, p, 4, e.Pos);   // 타일 단위 마무리 (1단계와 같은 반경)
        p = FarSide(s, g, p, w);             // 건넌 중대: 강 건너 쪽만 (TacticalAi.Crossing.cs)
        g.PickKind = 1; g.PickBase = e.Pos; g.PickResult = p;
        return p;
    }

    // ---- 분대 교대 약진 (사격과 기동): 짝수 · 홀수 분대가 번갈아 — 한쪽은 사수 (화력 기반), 한쪽은 엄폐 칸으로 boundLength 이동 (기동)

    static Squad? SquadById(Company? co, int id)
    {
        if (co == null) return null;
        foreach (var q in co.Squads) if (q.Id == id) return q;
        return null;
    }

    bool StartSquadBound(World w, TacFaction s, TacGroup g, Vector2 dest, Vector2 toE, float range, Unit e)
    {
        var co = g.CompanyId != 0 ? w.FindCompany(g.CompanyId) : null;
        if (co == null || co.Squads.Count < 2 || g.HoldFire && Vector2.Distance(g.Center, e.Pos) <= range) return false;
        g.SquadBound = 1; g.SquadBoundPoint = dest;
        BoundStep(w, s, g, co, toE, range, e);
        g.State = TacState.Approach; g.StateTick = w.Tick;
        return true;
    }

    void BoundStep(World w, TacFaction s, TacGroup g, Company co, Vector2 toE, float range, Unit e)
    {
        int parity = g.SquadBound == 1 ? 0 : 1;
        sub.Clear(); sub2.Clear();
        for (int i = 0; i < co.Squads.Count; i++)
            foreach (var u in co.Squads[i].Members)
                if (u.Alive && u.Owner == s.Faction) (i % 2 == parity ? sub : sub2).Add(u);
        foreach (var u in mem) if (u.Squad == null) sub2.Add(u);   // 장교 (중대 본부) 는 화력 분대와 함께
        if (sub.Count == 0) { g.SquadBound = 0; return; }
        Vector2 mc = Centroid(sub), hc = sub2.Count > 0 ? Centroid(sub2) : mc;
        float bound = MathF.Max(3f, s.Profile.Tactics.BoundLength * range);
        float lead = Vector2.Dot(hc - mc, toE);
        float step = MathF.Max(2f, lead + bound * 0.5f);
        float remain = Vector2.Dot(g.SquadBoundPoint - mc, toE);
        step = MathF.Min(step, MathF.Max(1f, remain + 1f));
        Vector2 tgt = BestCover(w.Map, mc + toE * step, 2, e.Pos);
        Issue(w, s, sub, Ord.Move, tgt, toE, MathF.Max(4f, sub.Count * 0.9f));
        if (sub2.Count > 0) { w.Hold(s.Faction, sub2); s.Orders++; }
        g.SquadBoundTick = w.Tick; g.OrderTick = w.Tick; g.OrderPoint = ClampMap(w.Map, tgt);
        s.SquadBounds++;
    }

    /// <summary>약진 진행: 이동 분대가 도착 (또는 8초) 하면 교대. 중대가 사격 진지에 닿거나 제압되면 끝 (사수). 처리했으면 true.</summary>
    bool ContinueSquadBound(World w, TacFaction s, TacGroup g, Unit e, float d, float engageD, float supp, Vector2 toE)
    {
        var co = g.CompanyId != 0 ? w.FindCompany(g.CompanyId) : null;
        if (co == null || co.Squads.Count < 2) { g.SquadBound = 0; return false; }
        int parity = g.SquadBound == 1 ? 0 : 1;
        sub.Clear();
        for (int i = 0; i < co.Squads.Count; i++)
            if (i % 2 == parity) foreach (var u in co.Squads[i].Members) if (u.Alive && u.Owner == s.Faction) sub.Add(u);
        bool arrived = sub.Count == 0 || Vector2.Distance(Centroid(sub), g.OrderPoint) < 2.5f || w.Tick - g.SquadBoundTick > Sec(8);
        if (!arrived) return true;
        float range = 0;
        foreach (var u in mem) if (u.Weapon != null && !u.IsOfficer) range = MathF.Max(range, u.Weapon.Range);
        bool reached = Vector2.Dot(g.SquadBoundPoint - g.Center, toE) <= 1.5f || d <= engageD + 1f || (g.BoundDist > 0 && d <= g.BoundDist + 1f);
        if (reached || supp > 0.6f || g.HoldFire && d <= range)
        {
            HoldAll(w, s, g, mem);
            g.SquadBound = 0; g.BoundDist = 0;
            g.State = TacState.Engage; g.StateTick = w.Tick;
            return true;
        }
        g.SquadBound = 3 - g.SquadBound;
        BoundStep(w, s, g, co, toE, range > 0 ? range : 16f, e);
        return true;
    }

    // ---- 방어 (주저항선 · 전초)

    Squad? OutpostSquad(World w, TacGroup g) => g.OutpostSquad != 0 && g.CompanyId != 0 ? SquadById(w.FindCompany(g.CompanyId), g.OutpostSquad) : null;

    /// <summary>방어 위치에서 (지도) 참호를 찾는 반경 (칸).</summary>
    const float TrenchSearch = 6f;

    void LineDefend(World w, TacFaction s, TacGroup g, float range, float supp, Unit? e, float d)
    {
        var T = s.Profile.Tactics;
        float R = s.RifleRange > 0 ? s.RifleRange : range;
        Vector2 dir = SafeNorm(g.TaskDir, s.Dir);
        bool contact = e != null && d <= range;
        // 방어 지형 활용 (engage, 2026-10-19 사용자 결정 — 방어 과제 기본): 방어 위치 둘레 참호선 (적이 다가올 쪽 우선) → 없으면 엄폐 칸 거점 (가옥 옆 · 숲 가장자리 · 잔해) 을
        // 진지 점유 명령 (Core/CoverSeek.cs) 으로 병사마다 참호 자리 · 엄폐 칸에 배정 — 깃발 대형을 참호에 대는 옛 방식은 앞줄만 참호에 들고 나머지는 10칸 뒤에 섰다.
        if (DefendFortify(w, s, g, dir, R, contact)) return;
        // 방어 위치 근처 (지도 · 미션의) 참호: 참호 성향 확률로 그 참호에 진지 — 대형 슬롯이 참호 자리에 붙고 도착하면 자동 점유 (Core/Trenches.cs)
        bool useTrench = T.Trench > 0 && Hash01(g.Id, s.Faction * 31 + 7) < T.Trench;
        Vector2 post = useTrench && w.NearestTrench(g.TaskPoint, TrenchSearch) is Vector2 tp ? tp : g.TaskPoint;
        float toPos = Vector2.Distance(g.Center, post);
        bool trenchPending = post != g.TaskPoint && !g.Dug;   // 참호를 찾았는데 아직 그리로 가라는 명령을 안 냈다 (가까워도 한 번은 이동 — 슬롯이 참호 자리에 붙는다)
        bool rotIn = g.RotateUntil > w.Tick && !g.RotateOut;   // 3B 교대로 들어오는 중대: 접촉 중에도 받은 칸으로 간다
        if ((toPos > 5f || trenchPending) && (!contact || rotIn))
        {
            if (Retarget(w, g, TacState.Advance, post, 6f))
            {
                if (g.OutpostState == 1) RecallOutpost(w, s, g);
                Order(w, s, g, mem, post != g.TaskPoint ? Ord.Move : Ord.AttackMove, post, g.TaskDir, g.TaskWidth);   // 참호로 가는 이동은 일반 이동 (공격 이동은 참호에 안 잡힌다)
                g.State = TacState.Advance; g.StateTick = w.Tick; g.Dug = post != g.TaskPoint;
                if (g.Dug) s.TrenchMoves++;
            }
            if (g.State == TacState.Advance && w.Tick - g.StateTick < Sec(30)) return;
        }
        if (g.State == TacState.Advance && toPos > 2.5f && !contact && w.Tick - g.StateTick < Sec(30)) return;   // 깃발 대형으로 아직 걷는 중
        if (g.State != TacState.Defend)
        {
            // 방어 위치: 사수 (참호 안 · 참호 자리로 가는 중인 병사는 사수 명령이 자리를 유지 — WorldCommands HoldCommand)
            HoldAll(w, s, g, mem);
            g.State = TacState.Defend; g.StateTick = w.Tick; g.Dug = post != g.TaskPoint;
            // 전초: 첫 분대를 앞 엄폐 칸으로 (분대 3개 이상 · 20명 이상 · 아직 적이 멀 때 · 한 번만)
            if (T.Outposts && !Templated(w, g) && g.OutpostState == 0 && g.Role is TacRole.MainLine or TacRole.Line && (e == null || d > range * 1.3f)
                && g.CompanyId != 0 && w.FindCompany(g.CompanyId) is { Squads.Count: >= 3 } co && g.Alive >= 20)
            {
                var q = co.Squads[0];
                sub.Clear();
                foreach (var u in q.Members) if (u.Alive && u.Owner == s.Faction) sub.Add(u);
                if (sub.Count > 0)
                {
                    // dir = 방어 정면 (위)
                    Vector2 op = g.TaskPoint + dir * MathF.Max(6f, T.OutpostDistance * range);
                    op = BestCover(w.Map, op, 3, op + dir * 30f);
                    Issue(w, s, sub, Ord.Move, op, dir, MathF.Max(4f, sub.Count * 1.2f));
                    g.OutpostSquad = q.Id; g.OutpostState = 1; g.OutpostTick = w.Tick;
                    s.Outposts++;
                }
            }
            return;
        }
        if (DrillRunning(w, g)) return;   // 종심 방어 틀 행동 중: 참호 점유 · 예비 역습은 중대 지휘관 (엔진) 이
        // 전초: 적이 사거리 80% 안 · 전초 제압 · 절반 손실이면 주저항선으로 철수
        if (g.OutpostState == 1 && outpostMem.Count > 0)
        {
            Vector2 oc = Centroid(outpostMem);
            float osupp = 0; foreach (var u in outpostMem) osupp += u.SuppressionRatio; osupp /= outpostMem.Count;
            var oe = Grid.Nearest(oc, TacGrid.Any);
            bool threatened = oe != null && Vector2.Distance(oe.Pos, oc) <= range * 0.8f;
            if (threatened || osupp > 0.4f || outpostMem.Count * 2 < OutpostInitial(g)) RecallOutpost(w, s, g);
        }
        // 역습: 가까이 온 적이 약하거나 제압되면
        if (e != null && T.Aggressiveness >= 0.3f && TryAssault(w, s, g, e, d, supp)) { g.Dug = false; return; }
        // 진지가 크게 옮겨졌으면 다시
        if (toPos > 8f && !contact && Due(w, g, 6f)) { g.State = TacState.Idle; g.OrderTick = -100000; }
    }

    /// <summary>
    /// 방어 진지 (engage, 2026-10-19): 과제 위치 둘레 참호 (trenchSearch × 사거리, 적 쪽 우선) → 엄폐 거점 (CoverCluster) 이 있으면 진지 점유 명령 한 번 —
    /// 병사는 참호 자리 · 엄폐 칸으로 걸어가 사수, 장교 · 남는 병사는 그 뒤. 진지가 8칸 넘게 바뀌면 다시. 처리했으면 (이 중대는 진지 방어) true.
    /// </summary>
    bool DefendFortify(World w, TacFaction s, TacGroup g, Vector2 dir, float R, bool contact)
    {
        var T = s.Profile.Tactics;
        if ((g.Kind != TacKind.Line && g.Kind != TacKind.Fire) || T.TrenchSearch < 0) return false;
        Vector2? post = null;
        if (T.Trench > 0) post = w.NearestTrench(g.TaskPoint, MathF.Max(TrenchSearch, T.TrenchSearch * R), dir);
        if (post == null && T.DefendCover) post = w.CoverCluster(g.TaskPoint, MathF.Max(6f, T.TrenchSearch * R * 0.75f));
        if (post is not Vector2 p) return false;
        float toPos = Vector2.Distance(g.Center, p);
        bool moved = g.Dug && Vector2.Distance(p, g.OccPost) > (T.DefendRepick > 0 ? MathF.Max(8f, T.DefendRepick * R) : 8f);   // 3B S4: 진지 히스테리시스 (defendRepick)
        if (moved && g.Spent && Vector2.Dot(g.OccPost - p, dir) < -4f) moved = false;   // 밀려 물러난 다음 진지에 남는다 (옛 진지로 되돌아가 적 앞 개활지를 걷지 않음)
        if (moved && (contact || w.Tick - g.OrderTick < Sec(20))) moved = false;   // 접촉 중 · 방금 낸 진지는 옮기지 않는다 (과제 위치가 조금씩 바뀔 때마다 참호를 비우고 다시 걷던 것)
        if (!g.Dug || moved)
        {
            if (contact && toPos > (g.RotateUntil > w.Tick && !g.RotateOut ? 40f : 14f)) return false;   // 접촉 중 먼 진지로 걸어가지 않는다 (가까운 진지만 — 3B 교대로 들어오는 중대는 받은 칸까지)
            if (g.OutpostState == 1) { RecallOutpost(w, s, g); g.OutpostState = 2; }
            float radius = MathF.Max(8f, MathF.Max(g.TaskWidth, Frontage(g)) * 0.5f + 3f);
            w.OccupyPositions(s.Faction, mem, p, dir, radius, T.CoverMaxHit);
            g.Dug = true; g.OccPost = p;
            g.OrderPoint = p; g.OrderTick = w.Tick;
            g.State = TacState.Advance; g.StateTick = w.Tick;
            s.Orders++; s.TrenchMoves++;
            return true;
        }
        if (g.State == TacState.Advance)
        {
            int moving = 0;
            foreach (var u in mem) if (u.Group != null) moving++;
            if (moving * 10 > mem.Count && w.Tick - g.StateTick < Sec(30) && !contact) return true;
            g.State = TacState.Defend; g.StateTick = w.Tick;
        }
        if (g.State != TacState.Defend) { g.State = TacState.Defend; g.StateTick = w.Tick; }
        if (DrillRunning(w, g)) return true;   // 종심 방어 틀: 예비 역습 · 참호 점유는 중대 지휘관 (엔진)
        var e = g.Kind == TacKind.Line ? Grid.Nearest(g.Center, TacGrid.Infantry) : null;
        if (e != null && T.Aggressiveness >= 0.3f)
        {
            float dd = Vector2.Distance(e.Pos, g.Center);
            float sp = 0; foreach (var u in mem) sp += u.SuppressionRatio; sp /= Math.Max(1, mem.Count);
            if (TryAssault(w, s, g, e, dd, sp)) { g.Dug = false; return true; }
        }
        return true;
    }

    /// <summary>후퇴 (방어 과제, engage): 뒤로 back 칸 둘레의 다음 참호 · 엄폐선 (없으면 null).</summary>
    Vector2? FallbackPost(World w, TacFaction s, TacGroup g, Vector2 dir, float back)
    {
        var T = s.Profile.Tactics;
        Vector2 bp = ClampMap(w.Map, g.Center - dir * back);
        float r = MathF.Max(6f, back * 0.6f);
        Vector2? p = T.Trench > 0 ? w.NearestTrench(bp, r, dir) : null;
        if (p is Vector2 tp && Vector2.Dot(tp - g.Center, dir) > -4f) p = null;   // 지금 진지와 같은 선이면 아님
        if (p == null && T.DefendCover) p = w.CoverCluster(bp, r * 0.75f);
        if (p is Vector2 cp && Vector2.Dot(cp - g.Center, dir) > -4f) p = null;
        return p;
    }

    static int OutpostInitial(TacGroup g) => Math.Max(2, g.Initial / 5);

    /// <summary>전초 분대 철수: 속보로 중대 진지 (과제 위치) 로 돌아와 사수 — 다음 명령부터 중대에 다시 포함.</summary>
    void RecallOutpost(World w, TacFaction s, TacGroup g)
    {
        if (outpostMem.Count > 0)
        {
            Vector2 dir = SafeNorm(g.TaskDir, s.Dir);
            Issue(w, s, outpostMem, Ord.Retreat, g.TaskPoint - dir * 1.5f, dir, MathF.Max(4f, outpostMem.Count * 1.2f));
            s.OutpostWithdrawals++;
        }
        g.OutpostState = 2; g.OutpostTick = w.Tick;
    }

    /// <summary>사격선 전진 조건: 표적 둘레 (12칸) 적이 제압됐거나 우리가 1.3배 이상 많을 때 — 대등한 사격전에서 걸어 나가면 화력만 잃는다.</summary>
    // 수 비교는 이 중대 병력 기준 (실험: 같은 표적을 치는 공격 중대 전체로 세면 평지 · 지형 보병전 6판 3승으로 떨어짐 — 대등 사격전에서 일찍 걸어 나감)
    bool Favourable(TacGroup g, Unit e)
    {
        var st = Grid.Stats(e.Pos, 12f * K);
        if (st.Count == 0) return true;
        return st.Supp / st.Count >= 0.3f || g.Alive >= st.Count * 1.3f;
    }

    /// <summary>착검 돌격 판단: 가깝고, 우리 제압이 낮고, 표적 둘레 적이 제압됐거나 수가 적고, 둘레에 살아 있는 기관총이 없을 때.</summary>
    /// 공격 돌격 (attack) 은 결정 4 (2026-10-19) 의 허가도 받아야 한다: 화력 우세 신호 · 엄호 확인 + 병력비, 아니면 교전 assaultFallbackSec 뒤 (World.AssaultClearance — 보이는 적만).
    /// 방어 역습 (주저항선 · 역습 예비) 은 그대로.
    /// <summary>돌격보병 중대 (N23): 장교 · 부사관 아닌 구성원 과반이 MarchFireDefault.</summary>
    static bool SweepGroup(List<Unit> m)
    {
        int a = 0, n = 0;
        foreach (var u in m) { if (u.IsOfficer) continue; n++; if (u.Def.MarchFireDefault) a++; }
        return a * 2 > n;
    }

    bool TryAssault(World w, TacFaction s, TacGroup g, Unit e, float d, float supp, bool attack = false, bool counter = false)
    {
        var T = s.Profile.Tactics;
        if (T.Aggressiveness <= 0.05f || d > T.ChargeRange * K || supp > 0.5f || e.Def.IsArmored) return false;
        var st = Grid.Stats(e.Pos, 10f * K);
        if (st.Count == 0) return false;
        if (FleeingTarget(w, e, st)) { w.Stats.FleeingChargeBlocked++; return false; }   // 후퇴하는 적에게는 착검 돌격 없음 — 사격 추격 (pursuit)
        if (Grid.ActiveMgNear(e.Pos, 8f * K)) return false;
        int chargers = 0;
        foreach (var u in mem) if (u.CanCharge) chargers++;
        if (chargers < mem.Count / 2) return false;
        float es = st.Supp / st.Count;
        var CC = counter ? Ctr(w) : null;   // 역습 돌격: 조건 · 허가 예비 타이머는 역습 교리 수치 (TacticalAi.Counter.cs)
        if (es < (CC?.ChargeSuppression ?? T.ChargeSuppression) && chargers < (CC?.ChargeOdds ?? T.ChargeOdds) * st.Count) return false;
        if (attack)
        {
            var co = g.CompanyId != 0 ? w.FindCompany(g.CompanyId) : null;
            long since = co is { Drill.Phase: not DrillPhase.Idle } ? co.Drill.PhaseTick : g.StateTick;   // 교전 시작 (틀 국면, 없으면 교전 상태 진입)
            if (CC != null && g.CounterAssaultTick != 0) since = Math.Min(since, g.CounterAssaultTick - Sec(MathF.Max(0, w.TacRules.Behaviour.AssaultFallbackSec - CC.AssaultFallbackSec)));
            if (!w.CountAssault(w.AssaultClearance(s.Faction, g.Center, s.RifleRange > 0 ? s.RifleRange : 16f, e.Pos, mem, chargers, since))) return false;
        }
        Vector2 toE = SafeNorm(e.Pos - g.Center, g.TaskDir);
        foreach (var u in mem) if (u.CanCharge) { w.CountChargeAt(u, e); break; }
        w.Charge(s.Faction, mem, ClampMap(w.Map, e.Pos + toE * 3f));
        g.OrderPoint = e.Pos; g.OrderTick = w.Tick;
        g.State = TacState.Assault; g.StateTick = w.Tick; g.SquadBound = 0;
        s.Orders++; s.Charges++;
        return true;
    }

    // ---------------------------------------------------------------- 화력 지원 (기관총 · 대전차 · 직사포 · 저격수) = 화력 기반

    void FireThink(World w, TacFaction s, TacGroup g)
    {
        if (g.Task is TacTask.Reserve or TacTask.Fallback) { GoAndHold(w, s, g, g.Task == TacTask.Fallback ? Ord.Retreat : Ord.Move, TacState.Reserve, 5f); return; }
        if (FireCrossing(w, s, g)) return;   // 도하 화력 기반 (TacticalAi.Crossing.cs)
        if (IsGunGroup(mem)) { GunThink(w, s, g); return; }   // 직사포 (보병포 · 야전포) = TacticalAi.Artillery.cs (N20)
        // 방어 기관총 (engage, 2026-10-19): 진지 (적 쪽 참호 · 엄폐 칸) 에 들어가 거치 — 사계는 참호선 앞. 진지 근처 (14칸) 에 오면 한 번, 그 뒤 사수
        if (g.Task == TacTask.Defend && (g.Dug || Vector2.Distance(g.Center, g.TaskPoint) < 14f)
            && DefendFortify(w, s, g, SafeNorm(g.TaskDir, s.Dir), s.RifleRange > 0 ? s.RifleRange : 16f, false)) return;
        float r = float.MaxValue; bool anti = false;
        foreach (var u in mem)
            if (u.Weapon != null && !u.IsOfficer && (u.CanDeploy || u.Def.Category == "gun" || u.Weapon.Range >= LongRange)) { r = MathF.Min(r, u.Weapon.Range); anti |= u.Weapon.IsAntiArmor; }
        if (r == float.MaxValue) r = 12f;
        Unit? e = null;
        if (anti) e = Grid.Nearest(g.Center, TacGrid.Armored);
        if (e == null || Vector2.Distance(e.Pos, g.Center) > r + 15f * K) e = Grid.Nearest(g.Center, TacGrid.Any);
        float d = e != null ? Vector2.Distance(e.Pos, g.Center) : float.MaxValue;
        float standoff = r * 0.9f;
        if (e != null && d < r + 16f * K)
        {
            if (d > standoff + 2f)
            {
                // 거치 전 이동: 사격 진지 (TacMap — 고지 · 사선 가중) 로 (이동 중에는 쏘지 않고, 도착하면 사수 → 자동 거치)
                Vector2 toE = SafeNorm(e.Pos - g.Center, g.TaskDir);
                Vector2 p;
                if (g.PickKind == 3 && Vector2.Distance(g.PickBase, e.Pos) <= 6f) p = g.PickResult;
                else
                {
                    p = TacMap.For(w.Map).PickPosition(g.Center, e.Pos, standoff, 16f, s.Profile.Tactics, Grid, r, 1.5f, 1.5f);
                    p = BestCover(w.Map, p, 2, e.Pos);
                    p = BehindInfantry(w, s, p, toE, 0f);   // 중화기 화력 기반은 아군 보병 앞선보다 앞에 두지 않는다 (사용자 규칙 2026-10-19)
                    g.PickKind = 3; g.PickBase = e.Pos; g.PickResult = p;
                }
                bool settled = g.State == TacState.Engage && w.Tick - g.StateTick < Sec(8);
                if (!settled && (g.State != TacState.Approach || Vector2.Distance(p, g.OrderPoint) > 4f) && Due(w, g, 5f))
                {
                    Order(w, s, g, mem, Ord.Move, p, toE, g.TaskWidth);
                    g.State = TacState.Approach; g.StateTick = w.Tick;
                    return;
                }
                if (g.State == TacState.Approach && w.Tick - g.StateTick < Sec(12)) return;
            }
            if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; s.FireHolds++; }
            return;
        }
        GoAndHold(w, s, g, Ord.AttackMove, TacState.Engage, 4f);
    }

    // 곡사포 (ArtilleryThink · ForceFire · InGunArc) · 직사포 (GunThink) · 관측자 (ObserverThink) = TacticalAi.Artillery.cs (N20)

    // ---------------------------------------------------------------- 기병 (차장 · 측면 경계 → 돌격 · 추격)

    void CavalryThink(World w, TacFaction s, TacGroup g)
    {
        var S = s.Profile.Strategy;
        var C = w.CavalryRules;
        if (C.Legacy) { CavalryThinkLegacy(w, s, g); return; }
        if (S.Cavalry < 0.15f || g.Task is TacTask.Reserve or TacTask.Fallback) { g.CavStage = 0; GoAndHold(w, s, g, g.Task == TacTask.Fallback ? Ord.Retreat : Ord.Move, TacState.Reserve, 6f); return; }
        if (g.State == TacState.Charge)
        {
            bool any = false;
            foreach (var u in mem) any |= u.Charging;
            if (any && w.Tick - g.StateTick < Sec(12)) return;
            g.State = TacState.Idle; g.NoChargeUntil = w.Tick + Sec(MathF.Max(6f, C.StrictCharge ? C.RechargeCooldownSec : 0f)); g.OrderTick = -100000;
        }
        // 측면 경유 중: 경유점에 닿거나 waySec 이 지날 때까지 (표적이 사라지면 그만)
        if (g.CavStage == 1)
        {
            var ct = w.Find(g.CavTarget) as Unit;
            bool alive = ct != null && ct.Alive && enemies.Contains(ct);
            bool reached = Vector2.Distance(g.Center, g.CavWay) <= C.WayReach;
            bool danger = TooClose(g.Center, Grid.MaxRange() * 0.75f);   // 도는 길에 적이 다가와 사거리 안 = 그만두고 다시 판단 (지금 각에서 돌격 또는 차장)
            if (alive && !reached && !danger && w.Tick - g.StateTick < Sec(C.WaySec)) return;
            g.CavStage = 0;
            // 경유점 도착 = 측면에서 바로 돌격 (표적 대형이 몸을 돌려도 — 대형 전환에는 시간이 걸린다)
            if (alive && reached && (!World.IsFleeing(ct) || w.PursuitRules.CavalryChargeFleeing))
            {
                Vector2 dirT = SafeNorm(ct!.Pos - g.Center, g.TaskDir);
                foreach (var u in mem) if (u.CanCharge) { w.CountChargeAt(u, ct); break; }
                CavalryCharge(w, s, g, ClampMap(w.Map, ct.Pos + dirT * 4f), ct.Pos);
                g.OrderPoint = ct.Pos; g.OrderTick = w.Tick;
                g.State = TacState.Charge; g.StateTick = w.Tick;
                s.Orders++; s.CavalryCharges++; w.Stats.CavChargeFlank++;
                return;
            }
        }
        // 물러나 차장 중 (교착 방지): stallSec 동안 그 자리
        if (g.State == TacState.Fallback && w.Tick - g.StateTick < Sec(C.StallSec)) return;
        int riders = 0;
        foreach (var u in mem) if (u.Def.Mounted && !u.IsOfficer) riders++;   // 기병 장교 (cavcmd) 는 기수 수에서 뺌 (옛 보병 장교와 같은 셈)
        var plan = w.Tick >= g.NoChargeUntil ? PickCavalryPlan(w, s, g, riders) : null;
        if (plan is { } pl)
        {
            g.CavStallSince = 0;
            // 소규모 기병 (minRiders 미만): 돌격하지 않고 정찰 · 차장 (카빈 사격은 엔진 자동 — 사수 · 대기)
            if (riders < C.MinRiders) { w.Stats.CavSmallHeld++; CavalryScreen(w, s, g); return; }
            if (pl.Route)
            {
                Order(w, s, g, mem, Ord.Move, pl.Way, pl.Cen - pl.Way, MathF.Min(16f, g.Alive * 1.2f));
                g.State = TacState.Approach; g.StateTick = w.Tick;
                g.CavStage = 1; g.CavWay = pl.Way; g.CavTarget = pl.Target.Id;
                w.Stats.CavFlankRoutes++;
                return;
            }
            Vector2 dirT = SafeNorm(pl.Target.Pos - g.Center, g.TaskDir);
            foreach (var u in mem) if (u.CanCharge) { w.CountChargeAt(u, pl.Target); break; }
            CavalryCharge(w, s, g, ClampMap(w.Map, pl.Target.Pos + dirT * 4f), pl.Target.Pos);
            g.OrderPoint = pl.Target.Pos; g.OrderTick = w.Tick;
            g.State = TacState.Charge; g.StateTick = w.Tick;
            s.Orders++; s.CavalryCharges++;
            if (pl.Angle == 0) w.Stats.CavChargeFront++; else if (pl.Angle == 1) w.Stats.CavChargeFlank++; else w.Stats.CavChargeRear++;
            if (pl.Rash) w.Stats.CavRashFront++;
            return;
        }
        // 돌격할 곳이 없다: 적 사거리 + 6 안에서 stallSec 넘게 머물면 물러나 차장 (기관총 앞 교착 방지 — N18)
        var near = Grid.Nearest(g.Center, TacGrid.Any);
        float er = Grid.MaxRange();
        if (near != null && Vector2.Distance(near.Pos, g.Center) <= er + 6f)
        {
            if (g.CavStallSince == 0) g.CavStallSince = w.Tick;
            else if (w.Tick - g.CavStallSince > Sec(C.StallSec))
            {
                Vector2 away = SafeNorm(g.Center - near.Pos, -SafeNorm(g.TaskDir, s.Dir));
                Order(w, s, g, mem, Ord.Move, near.Pos + away * (er + C.ScreenGap), -away, MathF.Min(16f, g.Alive * 1.2f));
                g.State = TacState.Fallback; g.StateTick = w.Tick; g.CavStallSince = 0;
                w.Stats.CavScreens++;
                return;
            }
        }
        else g.CavStallSince = 0;
        if (riders < C.MinRiders) { CavalryScreen(w, s, g); return; }
        if (g.State == TacState.Approach && w.Tick - g.StateTick < Sec(6)) return;
        GoAndHold(w, s, g, Ord.Move, TacState.Engage, 6f);
    }

    /// <summary>소규모 기병 = 정찰 · 차장: 과제 위치 (측면) 에서 사수 — 카빈이 있으면 엔진이 사거리 안 적을 쏜다. 적 보병이 12칸 안이면 10칸 물러남.</summary>
    void CavalryScreen(World w, TacFaction s, TacGroup g)
    {
        var e = Grid.Nearest(g.Center, TacGrid.Infantry);
        if (e != null && Vector2.Distance(e.Pos, g.Center) < 12f * K && Due(w, g, 4f))
        {
            Vector2 away = SafeNorm(g.Center - e.Pos, -SafeNorm(g.TaskDir, s.Dir));
            Order(w, s, g, mem, Ord.Move, g.Center + away * (10f * K), -away, MathF.Min(16f, g.Alive * 1.2f));
            g.State = TacState.Approach; g.StateTick = w.Tick;
            return;
        }
        if (g.State == TacState.Approach && w.Tick - g.StateTick < Sec(4)) return;
        GoAndHold(w, s, g, Ord.Move, TacState.Engage, 6f);
    }

    readonly List<TacGrid.CavCell> cavCells = new();

    /// <summary>p 에서 가장 가까운 보이는 적이 r 칸 안인가.</summary>
    bool TooClose(Vector2 p, float r) => Grid.Nearest(p, TacGrid.Any) is { } n && Vector2.Distance(n.Pos, p) < r;

    /// <summary>[시험] 지금 보이는 적으로 기병 계획을 세운다 (상태 변경 없음 — 작업 격자만).</summary>
    internal CavPlan? TestCavalryPlan(World w, TacFaction s, TacGroup g, int riders)
    {
        K = (s.RifleRange > 0 ? s.RifleRange : 16f) / 16f;
        Perceive(w, s);
        return PickCavalryPlan(w, s, g, riders);
    }

    /// <summary>기병 계획: 표적 · 접근 각 (0 정면 · 1 측면 · 2 후방) · 측면 경유 필요 여부 · 경유점.</summary>
    internal readonly record struct CavPlan(Unit Target, Vector2 Cen, int Angle, bool Route, Vector2 Way, bool Rash = false);

    /// <summary>
    /// 기병 표적 (pursuit 2026-10-19): 둘레 50칸 8칸 묶음마다 — 보병 평균 제압 · 흐트러짐 (이동) · 후퇴 (cavalryChargeFleeing) 점수 + 접근 각 점수 (표적 대형 정면 = 평균 방향 기준:
    /// 측면 flankScore · 후방 rearScore, 정면은 제압 ≥ frontSuppOk · 아주 약함 · 대형 흐트러짐일 때만, 아니면 측면 경유점이 기관총 사계 밖이면 경유 (점수 일부), 둘 다 아니면 후보 아님).
    /// 국지 병력비 (기수 ≥ oddsRadius 안 적 × minOdds, 측면 · 후방 × flankOddsMul) 를 못 맞추면 후보 아님. 기관총은 접근점을 정면 사계에 둔 것만 막는다 (측면 · 후방 기관총은 표적 가능).
    /// </summary>
    internal CavPlan? PickCavalryPlan(World w, TacFaction s, TacGroup g, int riders)
    {
        var C = w.CavalryRules;
        var P = w.PursuitRules;
        float needSupp = 0.6f - 0.35f * s.Profile.Strategy.Cavalry;
        float cosF = DetMath.Cos(C.FrontDeg * MathF.PI / 180f);
        Grid.CavalryCells(g.Center, 50f * K, cavCells);
        float enemyRange = Grid.MaxRange();
        CavPlan? best = null; float bestScore = float.MinValue; bool bestFront = false;
        // N55: 경험 적은 (무능한) 지휘관은 형성된 보병 정면에도 우회 없이 돌격 (cavFront 성향, OfficerStyle.cs) — 기관총 금지 · 병력비는 그대로
        var cavCo = g.CompanyId != 0 ? w.FindCompany(g.CompanyId) : null;
        bool rash = !C.StrictCharge && cavCo != null && w.RashCavalryFront(w.CommanderOf(cavCo), g.Id);   // 편제 밖 분견대 (중대 없음) 는 성향 없음
        foreach (var cc in cavCells)
        {
            bool fleeing = cc.Fleeing >= cc.N * P.FleeingShare;
            if (fleeing && !P.CavalryChargeFleeing) continue;
            float mf = cc.Moving / (float)cc.N;
            float align = cc.Face.Length();
            bool formed = (align >= C.FormedAlign || cc.N < C.FormedMinN) && !fleeing;   // pursuit2: 보이는 몇 명 (안개) 의 방향만으로 흐트러졌다고 보지 않음 — formedMinN 미만이면 형성된 대형으로 침
            bool ok = cc.Supp >= needSupp || (mf >= 0.6f && cc.N <= riders) || fleeing;
            Vector2 face = align > 1e-3f ? cc.Face / align : Vector2.Zero;
            Vector2 toUs = SafeNorm(g.Center - cc.Cen, -face);
            float cos = formed ? Vector2.Dot(face, toUs) : 0f;
            int ang = cos >= cosF ? 0 : cos <= -cosF ? 2 : 1;
            int local = Grid.Stats(cc.Cen, C.OddsRadius * K).Count;
            bool weak = cc.Supp >= C.FrontSuppOk || riders >= C.FrontWeakOdds * Grid.Stats(cc.Cen, C.WeakRadius * K).Count && (!formed || (!C.StrictCharge && cc.Supp >= C.FlankMinSupp));   // 아주 약함 = 둘레 weakRadius 의 적 전체 대비 — 형성된 제압 안 된 대형은 수로만 약하다고 보지 않음 (pursuit2)
            bool route = false, rashFront = false; Vector2 way = Vector2.Zero; float angScore;
            if (ang == 1) angScore = C.FlankScore;
            else if (ang == 2) angScore = C.RearScore;
            else if (rash && !weak) { angScore = 0f; rashFront = true; }   // 무모한 정면 돌격 (N55)
            else if (weak && !(Grid.ActiveMgNear(cc.Cen, 10f * K) && Grid.MgCovers(g.Center, 10f * K + Vector2.Distance(g.Center, cc.Cen), cosF))) angScore = 0f;   // 약한 정면 = 바로 (기관총 정면 사계면 경유)
            else
            {
                // 측면 경유점: 우리 쪽 옆 → 반대 옆 → 후방 순, 기관총 사계 밖인 첫 점
                Vector2 side = Perp(face);
                float sg = Vector2.Dot(g.Center - cc.Cen, side) >= 0 ? 1f : -1f;
                float off = MathF.Max(C.FlankOffset * K, C.WayOutsideRange ? enemyRange + 2f : 0f);   // 경유점은 적 사거리 밖 (돌아가는 동안 맞지 않게)
                for (int k = 0; k < 3 && !route; k++)
                {
                    Vector2 q = k switch { 0 => cc.Cen + side * (sg * off) - face * (off * 0.3f), 1 => cc.Cen - side * (sg * off) - face * (off * 0.3f), _ => cc.Cen - face * off };
                    q = ClampMap(w.Map, q);
                    var (qx, qy) = GameMap.TileOf(q);
                    if (!w.Map.IsWalkable(qx, qy) || Grid.MgCovers(q, C.MgAvoidRadius, cosF)) continue;
                    if (C.WayOutsideRange && (TooClose(q, enemyRange) || TooClose((q + g.Center) * 0.5f, enemyRange * 0.6f))) continue;   // 경유점 · 가는 길 가운데가 보이는 다른 적 사거리 안이면 그 길로 돌지 않는다 (큰 무리 앞을 가로지르다 녹음)
                    way = q; route = true;
                }
                angScore = route ? C.FlankScore * 0.7f - Vector2.Distance(g.Center, way) / 200f : -C.FrontPenalty;   // 돌아갈 곳이 없으면 정면 = 감점 (점수 < 0 이면 돌격 안 함)
            }
            ok |= formed && (ang != 0 || rashFront);   // 형성된 대형의 측면 · 후방 = 그 자체로 기회 (제압 · 흐트러짐 없어도). 기관총 정면 무리는 경유해도 사격에 녹으므로 (N18 측정) 물러나 차장
            bool isolated = C.StrictCharge && cc.N <= C.IsolatedMax;
            ok |= isolated;
            ok &= fleeing || rashFront || isolated || cc.Supp >= C.FlankMinSupp;   // pursuit2: 후퇴하지 않는 보병은 제압이 조금은 있어야 (flankMinSupp — 걷는 · 방향이 흐트러진 · 측면을 보인 대형 모두) — 형성된 대형의 측면 · 후방 = 그 자체로 기회 (제압 · 흐트러짐 없어도). 기관총 정면 무리는 경유해도 사격에 녹으므로 (N18 측정) 물러나 차장
            if (!ok) continue;
            // 기관총: 표적 둘레 (10칸) 에 제압되지 않은 기관총이 있으면 후보 아님 — 경기관총은 제자리에서 바로 돌려 쏘므로 측면 돌격도 녹는다 (N18 측정: 측면 경유 돌격 30기 전멸 · 기관총 손실 0).
            // 기관총만 남은 적 앞에서는 교착 규칙 (stallSec) 으로 물러나 차장.
            if (Grid.ActiveMgNear(cc.Cen, 10f * K)) continue;
            float need = C.MinOdds * local * (ang != 0 || route ? C.FlankOddsMul : 1f);
            if (riders < need) continue;
            float score = cc.Supp + 0.25f * mf + (fleeing ? 0.3f : 0f) + (cc.N * 2 <= riders ? 0.2f : 0) + angScore - cc.NearD / 120f;
            if (score > bestScore) { bestScore = score; best = new CavPlan(cc.Nearest, cc.Cen, route ? 1 : ang, route, way, rashFront); bestFront = ang == 0 && !route && !weak && !rashFront; }
        }
        return bestFront && bestScore < 0 ? null : best;   // 정면 감점으로 음수 = 돌아갈 곳 없는 형성된 정면 보병뿐 → 돌격하지 않음
    }

    /// <summary>[옛 기병 AI — tactics.json cavalry.legacy = true 일 때만, 비교 측정용] pursuit 전 동작 그대로.</summary>
    /// <summary>[측정] 접근 각 분류 (0 정면 · 1 측면 · 2 후방): 표적 둘레 8칸 적 평균 방향 기준 (흐트러진 무리 = 측면).</summary>
    int ChargeAngleClass(World w, Unit t, Vector2 from)
    {
        var C = w.CavalryRules;
        Grid.CavalryCells(t.Pos, 8f, cavCells);
        Vector2 f = Vector2.Zero; int n = 0;
        foreach (var cc in cavCells) { f += cc.Face * cc.N; n += cc.N; }
        if (n == 0) return 1;
        f /= n;
        float al = f.Length();
        if (al < C.FormedAlign) return 1;
        float cos = Vector2.Dot(f / al, SafeNorm(from - t.Pos, f / al)), cf = DetMath.Cos(C.FrontDeg * MathF.PI / 180f);
        return cos >= cf ? 0 : cos <= -cf ? 2 : 1;
    }

    /// <summary>
    /// 기병 돌격 명령 (cavcmd, 2026-10-19): 기수는 지점 돌격, 장교 (기병 장교) 는 돌격에 끼지 않고 표적 앞 tactics.json cavalry.officerBack 칸
    /// (돌격 방향 반대쪽) 으로 따라가 지휘를 유지한다 (소총 중대 틀의 '지휘조' 와 같은 원칙 — 첫 돌격에서 장교가 전사해 지휘가 끊기지 않게).
    /// 적이 가까우면 장교 자동 돌격 · 근접 규칙은 그대로. officerBack 0 = 옛 동작 (장교도 함께 돌격).
    /// </summary>
    void CavalryCharge(World w, TacFaction s, TacGroup g, Vector2 point, Vector2 target)
    {
        float back = w.CavalryRules.OfficerBack;
        bool split = false;
        if (back > 0) foreach (var u in mem) if (u.IsOfficer) { split = true; break; }
        if (!split) { w.Charge(s.Faction, mem, point); return; }
        buf.Clear(); buf2.Clear();
        foreach (var u in mem) (u.IsOfficer ? buf2 : buf).Add(u);
        if (buf.Count == 0) { w.Charge(s.Faction, mem, point); return; }
        w.Charge(s.Faction, buf, point);
        Vector2 dir = SafeNorm(target - g.Center, g.TaskDir);
        w.Move(s.Faction, buf2, ClampMap(w.Map, target - dir * back));
        w.Stats.CavOfficerHeldBack++;
    }

    void CavalryThinkLegacy(World w, TacFaction s, TacGroup g)
    {
        var S = s.Profile.Strategy;
        if (S.Cavalry < 0.15f || g.Task is TacTask.Reserve or TacTask.Fallback) { GoAndHold(w, s, g, Ord.Move, TacState.Reserve, 6f); return; }
        if (g.State == TacState.Charge)
        {
            bool any = false;
            foreach (var u in mem) any |= u.Charging;
            if (any && w.Tick - g.StateTick < Sec(12)) return;
            g.State = TacState.Idle; g.NoChargeUntil = w.Tick + Sec(6); g.OrderTick = -100000;
        }
        if (w.Tick >= g.NoChargeUntil && PickCavalryTarget(w, s, g) is { } t)
        {
            Vector2 side = Perp(SafeNorm(g.TaskDir, s.Dir));
            Vector2 wp = ClampMap(w.Map, t.Pos + side * (s.FlankSign * 10f));   // 측면 대기 쪽에서 비스듬히
            float dT = Vector2.Distance(g.Center, t.Pos), dW = Vector2.Distance(g.Center, wp);
            if (dT > 12f && dW > 6f && Vector2.Distance(wp, t.Pos) < dT)
            {
                if ((g.State != TacState.Approach || Vector2.Distance(wp, g.OrderPoint) > 6f) && Due(w, g, 2f))
                {
                    Order(w, s, g, mem, Ord.Move, wp, t.Pos - wp, MathF.Min(16f, g.Alive * 1.2f));
                    g.State = TacState.Approach; g.StateTick = w.Tick;
                }
                return;
            }
            Vector2 dirT = SafeNorm(t.Pos - g.Center, g.TaskDir);
            w.Charge(s.Faction, mem, ClampMap(w.Map, t.Pos + dirT * 4f));
            g.OrderPoint = t.Pos; g.OrderTick = w.Tick;
            g.State = TacState.Charge; g.StateTick = w.Tick;
            s.Orders++; s.CavalryCharges++;
            int ca = ChargeAngleClass(w, t, g.Center); if (ca == 0) w.Stats.CavChargeFront++; else if (ca == 1) w.Stats.CavChargeFlank++; else w.Stats.CavChargeRear++;   // [측정]
            return;
        }
        if (g.State == TacState.Approach && w.Tick - g.StateTick < Sec(6)) return;
        GoAndHold(w, s, g, Ord.Move, TacState.Engage, 6f);
    }

    /// <summary>기병 표적: 둘레 50칸의 8칸 묶음 중 보병이 제압됐거나 (이동 · 후퇴 중) 흐트러진 곳 = 돌격 · 추격, 살아 있는 기관총 10칸 안은 뺀다.</summary>
    Unit? PickCavalryTarget(World w, TacFaction s, TacGroup g)
    {
        float needSupp = 0.6f - 0.35f * s.Profile.Strategy.Cavalry;
        return Grid.BestCavalryCell(g.Center, 50f * K, needSupp, g.Alive, 10f * K);
    }

    // ---------------------------------------------------------------- 기갑 · 마차 · 정찰

    void ArmorThink(World w, TacFaction s, TacGroup g)
    {
        if (g.Task is TacTask.Reserve or TacTask.Fallback) { GoAndHold(w, s, g, g.Task == TacTask.Fallback ? Ord.Retreat : Ord.Move, TacState.Reserve, 5f); return; }
        if (g.Task == TacTask.Defend) { GoAndHold(w, s, g, Ord.AttackMove, TacState.Defend, 4f); return; }
        // 공격: 대대 과제 위치로 공격 이동 (교전은 유닛이 스스로)
        if (Vector2.Distance(g.Center, g.TaskPoint) > 4f)
        {
            if (g.State == TacState.Advance && Due(w, g, 30f)) g.State = TacState.Idle;   // 오래 못 가면 다시 명령
            if ((g.State != TacState.Advance || Vector2.Distance(g.TaskPoint, g.OrderPoint) > 8f) && Due(w, g, 4f))
            {
                Order(w, s, g, mem, Ord.AttackMove, g.TaskPoint, g.TaskDir, g.TaskWidth);
                g.State = TacState.Advance; g.StateTick = w.Tick;
            }
        }
        else if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
    }

    void WagonThink(World w, TacFaction s, TacGroup g)
    {
        var e = Grid.Nearest(g.Center, TacGrid.Any);
        if (e != null && Vector2.Distance(e.Pos, g.Center) < 16f * K)
        {
            if (Due(w, g, 3f))
            {
                Vector2 away = SafeNorm(g.Center - e.Pos, -g.TaskDir);
                Order(w, s, g, mem, Ord.Retreat, g.Center + away * (12f * K), -away, 0);   // 마차 물러남 = 후퇴
                g.State = TacState.Fallback; g.StateTick = w.Tick;
            }
            return;
        }
        if (g.State == TacState.Fallback && w.Tick - g.StateTick < Sec(4)) return;
        if (Vector2.Distance(g.Center, g.TaskPoint) > 6f && (g.State != TacState.Advance || Vector2.Distance(g.TaskPoint, g.OrderPoint) > 6f) && Due(w, g, 3f))
        {
            Order(w, s, g, mem, Ord.Move, g.TaskPoint, g.TaskDir, 0);
            g.State = TacState.Advance; g.StateTick = w.Tick;
        }
    }
}
