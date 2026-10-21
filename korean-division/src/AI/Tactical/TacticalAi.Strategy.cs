using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 세력 전략 층 (≈2초, docs/TACTICAL_AI_DESIGN.md §4 · §6): 증원 편입 · 월드 대대 연결 · 관측 표적, 지시 그룹마다 태세 · 목표 · 연대 정면 배분 ·
/// 주공 (포병 지원 · 정면 집중) · 예비 연대 · 직속 병과 (포병 → 주공 연대 지원, 기병 → 차장 / 주공 열린 측면, 정찰 · 마차) 배정.
/// </summary>
public sealed partial class TacticalAi
{
    void Strategy(World w, TacFaction s)
    {
        RefreshGroups(w, s);
        if (!s.PendingIntake) Intake(w, s);
        LinkWorld(w, s);
        int f = s.Faction;
        // 관측 표적 (장교 · 정찰병이 보는 적, 전선에서 가장 가까운 것) — 관측 전용 곡사포 진지 선정용. 포병이 있을 때만
        if (s.Groups.Exists(g => g.Kind == TacKind.Artillery))
        {
            Vector2 lc0 = s.LineCenter;
            float bd = float.MaxValue; Unit? sp = null;
            foreach (var e in enemies)
            {
                float d = Vector2.DistanceSquared(e.Pos, lc0);
                if (d < bd && w.SeenBySpotter(f, e)) { bd = d; sp = e; }
            }
            if (sp != null) { s.SpottedPos = sp.Pos; s.SpottedTick = w.Tick; s.HasSpotted = true; }
        }
        // 세력 기본 그룹 (0) = 세력 지시 (SetDirective · 지도 · 프로필)
        var d0 = s.Directives[0];
        d0.Posture = s.Posture; d0.HasArea = s.HasArea; d0.Area = s.Area; d0.AreaRadius = s.AreaRadius;
        TacDirective? major = null;
        foreach (var d in s.Directives)
        {
            StrategyGroup(w, s, d);
            if (d.Alive > 0 && (major == null || d.Alive > major.Alive)) major = d;
        }
        if (major != null)
        {
            s.Effective = major.Effective; s.Target = major.Target; s.Dir = major.Dir;
            s.HasEnemy = major.HasEnemy; s.EnemyPos = major.EnemyPos; s.LineCenter = major.LineCenter;
        }
    }

    void StrategyGroup(World w, TacFaction s, TacDirective d)
    {
        var S = s.Profile.Strategy;
        var T = s.Profile.Tactics;
        float R = s.RifleRange > 0 ? s.RifleRange : 16f;
        int f = s.Faction;
        int resReg = d.ReserveRegiment;
        // 병력 · 전선 중심 (소총 · 화력 · 기갑, 예비 연대 제외)
        Vector2 sum = Vector2.Zero, asum = Vector2.Zero; int n = 0, all = 0;
        foreach (var g in s.Groups)
        {
            if (g.Directive != d.Id || g.Pending) continue;
            all += g.Alive; asum += g.Center * g.Alive;
            if (g.Kind is TacKind.Line or TacKind.Fire or TacKind.Armor && (resReg == 0 || RegimentOfGroup(s, g) != resReg)) { sum += g.Center * g.Alive; n += g.Alive; }
        }
        d.Alive = all;
        if (all > d.Initial) d.Initial = all;   // 증원이 들어오면 기준도 올라간다
        if (all == 0) return;
        Vector2 lc = n > 0 ? sum / n : asum / all;
        d.LineCenter = lc;
        if (!d.HomeSet) { d.HomeSet = true; d.Home = lc; d.Dir = s.Front; }
        if (d.Dir.LengthSquared() < 0.5f) d.Dir = s.Front;

        // 보이는 적: 이 그룹 전선에서 가장 가까운 적과 그 둘레 무리 중심 · 정면 폭
        if (enemies.Count > 0 && Grid.Nearest(lc, canHurtArmor ? TacGrid.Any : TacGrid.Infantry) is { } near)
        {
            var st = Grid.Stats(near.Pos, 40f * K);
            d.EnemyNear = near.Pos;
            d.EnemyPos = st.Count > 0 ? st.Sum / st.Count : near.Pos;
            d.EnemySeenTick = w.Tick;
            d.HasEnemy = true;
            Vector2 sd = Perp(d.Dir);
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var e in enemies)
            {
                if (Vector2.DistanceSquared(e.Pos, near.Pos) > 45f * 45f * K * K) continue;
                float l = Vector2.Dot(e.Pos, sd);
                lo = MathF.Min(lo, l); hi = MathF.Max(hi, l);
            }
            d.EnemyWidth = hi >= lo ? hi - lo : 0;
        }

        // 태세
        var eff = d.Posture == AiPosture.Counter ? AiPosture.Attack : d.Posture;   // 역습 = 공격 (교리만 다름 — TacticalAi.Counter.cs)
        bool hasArea = d.HasArea;
        Vector2 anchor = hasArea ? d.Area : d.Home;
        float radius = d.AreaRadius;
        // stage 3A: 미션 목표 → 과제 (세력 기본 그룹만, S14) · 목표 고수 정도 (공격 한계 · 철수 문턱 배수, 미션 aiCommitment > 프로필) · 철수 문턱 (미션 aiWithdrawAt > 프로필)
        var aobj = S.Objectives && d.Id == 0 ? ObjectiveFor(w, f) : new AiObjective { Index = -1 };
        float commit = s.MissionCommitment >= 0 ? s.MissionCommitment : S.Commitment;
        float cScale = Math.Clamp(2f * (1f - commit), 0f, 2f);
        float culm = Math.Clamp(S.Culminate * cScale, 0f, 0.95f);
        float withdrawAt = s.MissionWithdrawAt >= 0 ? s.MissionWithdrawAt : Math.Clamp(S.WithdrawAt * cScale, 0f, 0.95f);
        if (aobj.Kind == AiObjectiveKind.Hold && eff is AiPosture.Defend or AiPosture.HoldArea) { anchor = aobj.Point; radius = MathF.Max(radius, aobj.Radius); hasArea = true; }   // 고수 목표: 그 구역을 지킨다
        // 지킬 곳 없이 오래 대치 (교전 테스트 등 — 미션 지시가 아닐 때만) → 접촉할 때까지 전진
        if (eff == AiPosture.Defend && !hasArea && !s.DirectiveSet && d.Id == 0 && S.DefendPatience > 0 && w.Tick - s.LastContactTick > Sec(S.DefendPatience)) eff = AiPosture.Attack;
        bool recent = d.HasEnemy && w.Tick - d.EnemySeenTick < Sec(60);
        int nearCount = recent ? Grid.Stats(d.EnemyPos, 45f * K).Count : 0;
        // 철수 (목표 포기, 사용자 결정 5): 병력 비율이 철수 문턱 아래 (접촉 중) → 물러나 방어, 다시 공격하지 않는다
        if (!d.Withdrawn && withdrawAt > 0 && recent && d.Initial > 0 && all < d.Initial * withdrawAt)
        {
            d.Withdrawn = true; d.WithdrawTick = w.Tick; s.Withdrawals++;
            d.WithdrawPoint = ClampMap(w.Map, d.Home - SafeNorm(d.Dir, s.Front) * (R * 1.5f));
            foreach (var r in s.Regiments)
                if (r.Directive == d.Id && !(r.Spent && r.FallbackUntil > w.Tick)) { r.Task = TacTask.Fallback; r.Point = d.WithdrawPoint; r.FallbackUntil = w.Tick + Sec(30); r.Width = 0; }
        }
        if (d.Withdrawn) { eff = AiPosture.Defend; anchor = d.WithdrawPoint; hasArea = true; radius = MathF.Max(radius, R); }
        // 공격 한계: 병력이 줄었고 보이는 적이 우리보다 많으면 그 자리 방어 (우세를 되찾으면 다시 공격)
        if (eff == AiPosture.Attack && !d.Culminated && recent && d.Initial > 0 && all < d.Initial * culm && nearCount > all * 1.2f)
        { d.Culminated = true; d.Home = lc; s.Culminations++; }
        if (d.Culminated)
        {
            if (recent && nearCount > 0 && all >= nearCount * 1.5f) d.Culminated = false;
            else if ((d.Posture is AiPosture.Attack or AiPosture.Counter) && !d.Withdrawn) { eff = AiPosture.Defend; anchor = d.Home; hasArea = false; }
        }
        Vector2 T0;
        TacTask task;
        bool inZone = eff == AiPosture.HoldArea && recent && Vector2.Distance(d.EnemyNear, anchor) < radius + 15f * K;
        // 미션이 방어 · 고수를 지시했으면 (s.DirectiveSet) 세력 단위 반격 (진지를 비우는 공격 전환) 은 하지 않는다 — 국지 역습은 연대 예비 대대가 맡는다
        bool counterNow = !s.DirectiveSet && !d.Withdrawn && eff is AiPosture.Defend or AiPosture.HoldArea && recent && nearCount > 0 && all >= S.CounterattackOdds * nearCount
                          && Vector2.Distance(d.EnemyNear, anchor) < radius + R * 1.25f;   // 진지 앞 국지 반격만 (2.5 사거리 출격은 밤 · 안개에서 적 수를 과소평가해 진지를 비웠다 — village_defense 패배)
        // 반격은 두 번 연속 (≈4초) 우세일 때 시작해 20초 유지 — 한 번 반짝 공격 과제로 바뀌면 연대가 예비 대대를 써 버리고 다시 방어로 돌아갔다.
        // 유지 중 적이 우리 이상이면 그만둔다.
        d.CounterVotes = counterNow ? d.CounterVotes + 1 : 0;
        if (d.CounterVotes >= 2 && w.Tick >= d.CounterUntil) d.CounterUntil = w.Tick + Sec(20);
        if (w.Tick < d.CounterUntil && (!(eff is AiPosture.Defend or AiPosture.HoldArea) || !recent || nearCount >= all)) d.CounterUntil = 0;
        bool counter = w.Tick < d.CounterUntil;
        if (inZone || counter) { eff = AiPosture.Attack; T0 = d.EnemyNear; task = TacTask.Attack; }   // 지역에 들어온 적 · 약한 적에게 반격
        else if (eff == AiPosture.Attack)
        {
            task = TacTask.Attack;
            if (recent) T0 = d.EnemyPos;
            else if (aobj.Kind is AiObjectiveKind.Take or AiObjectiveKind.Intercept) T0 = aobj.Point;   // 미션 목표 (점령 · 차단) — stage 3A
            else if (aobj.Kind == AiObjectiveKind.Escort) T0 = aobj.Point + SafeNorm(aobj.Aux - aobj.Point, d.Dir) * MathF.Min(Vector2.Distance(aobj.Point, aobj.Aux), R * 0.6f);   // 호송: 호송대 앞
            else if (hasArea) T0 = d.Area;   // 미션이 목표 구역을 줬다
            else if (ChooseObjective(w, s, lc) is Vector2 obj) T0 = obj;
            else if (d.HasEnemy) T0 = d.EnemyPos;
            else T0 = NearestRivalStart(w, f, lc);
            if (!recent && StaleLevel(w, s, T.StalemateSeconds) >= 3 && aobj.Kind is not (AiObjectiveKind.Take or AiObjectiveKind.Intercept or AiObjectiveKind.Escort) && !hasArea) T0 = NearestRivalStart(w, f, lc);   // 교착 3단계: 전면 전진
        }
        else { task = TacTask.Defend; T0 = anchor; }
        d.Effective = eff;
        d.Target = T0;
        Vector2 dir = task == TacTask.Attack
            ? (Vector2.DistanceSquared(T0, lc) > 25f ? Vector2.Normalize(T0 - lc) : d.Dir)
            : SafeNorm((d.HasEnemy ? d.EnemyPos : anchor + s.Front * 30f) - anchor, d.Dir);
        d.Dir = dir;
        Vector2 side = Perp(dir);

        // 연대: 무너진 연대 후퇴 · 예비 연대
        var regs = new List<TacRegiment>();
        foreach (var r in s.Regiments) if (r.Directive == d.Id) regs.Add(r);
        foreach (var r in regs)
            if (!r.Spent && r.Initial > 0 && r.Alive < r.Initial * T.RetreatStrength * 0.75f && !CrossCommitted(w, r))
            { r.Spent = true; r.FallbackUntil = w.Tick + Sec(20); s.Fallbacks++; }
        var fronts = new List<TacRegiment>();
        foreach (var r in regs) if (!r.Spent) fronts.Add(r);
        if (S.Reserve && fronts.Count >= 3 && !d.ReserveRegimentCommitted)
        {
            if (d.ReserveRegiment == 0 || !fronts.Exists(r => r.Id == d.ReserveRegiment))
                d.ReserveRegiment = fronts.OrderBy(r => Vector2.Dot(r.Center, dir)).ThenBy(r => r.Id).First().Id;
            int fa = 0, fi = 0;
            foreach (var r in fronts) if (r.Id != d.ReserveRegiment) { fa += r.Alive; fi += r.Initial; }
            bool collapse = regs.Exists(r => r.Spent);
            if (d.ContactSince == 0 && recent && task == TacTask.Attack) d.ContactSince = w.Tick;
            bool longFight = d.ContactSince != 0 && w.Tick - d.ContactSince > Sec(60f - 30f * S.Aggressiveness);
            var est = recent ? Grid.Stats(d.EnemyPos, 30f * K) : default;
            bool opportunity = task == TacTask.Attack && est.Count > 0 && est.Supp / est.Count >= T.ChargeSuppression;
            bool stale2 = task == TacTask.Attack && StaleLevel(w, s, T.StalemateSeconds) >= 2;   // 교착 2단계: 예비 투입
            if (collapse || stale2 || (fi > 0 && fa < fi * 0.7f) || longFight || opportunity || (task == TacTask.Attack && recent && nearCount > 0 && fa > nearCount * 2))
            { d.ReserveRegimentCommitted = true; d.ReserveRegiment = 0; s.ReserveCommits++; }
        }
        else if (d.ReserveRegiment != 0 && !fronts.Exists(r => r.Id == d.ReserveRegiment)) d.ReserveRegiment = 0;
        fronts.RemoveAll(r => r.Id == d.ReserveRegiment);
        if (fronts.Count == 0)
        {
            if (d.ReserveRegiment != 0) { d.ReserveRegimentCommitted = true; d.ReserveRegiment = 0; s.ReserveCommits++; }
            foreach (var r in regs) if (r.FallbackUntil <= w.Tick) fronts.Add(r);
        }
        Vector2 fsum = Vector2.Zero; int fcnt = 0;
        foreach (var r in fronts) { fsum += r.Center * r.Alive; fcnt += r.Alive; }
        Vector2 lcFront = fcnt > 0 ? fsum / fcnt : lc;
        fronts.Sort((a, b) => { int c = Vector2.Dot(a.Center, side).CompareTo(Vector2.Dot(b.Center, side)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
        // 주공: 가장 강한 연대 (같으면 목표 옆 위치에 가까운 쪽)
        TacRegiment? main = null;
        foreach (var r in fronts)
            if (main == null || r.Alive > main.Alive || (r.Alive == main.Alive && MathF.Abs(Vector2.Dot(r.Center - T0, side)) < MathF.Abs(Vector2.Dot(main.Center - T0, side)))) main = r;
        foreach (var r in regs) r.Main = r == main;

        // 정면 폭: 연대 폭 = 앞 대대 폭 합 (주공은 좁혀 집중), 지도 폭 · 적 정면에 맞춰 줄임
        var widths = new float[fronts.Count];
        float total = 0;
        for (int i = 0; i < fronts.Count; i++)
        {
            widths[i] = RegimentWidth(s, fronts[i]);
            if (fronts[i] == main && fronts.Count >= 2) widths[i] *= 1f - 0.4f * Math.Clamp(S.MainEffort, 0, 1);
            total += widths[i];
        }
        total += 8f * Math.Max(0, fronts.Count - 1);
        float roomR = RoomAlong(w.Map, T0, side), roomL = RoomAlong(w.Map, T0, -side);
        float avail = MathF.Max(20f, (roomR + roomL) * 0.9f);
        if (task == TacTask.Attack && recent) avail = MathF.Min(avail, MathF.Max(30f, d.EnemyWidth * 1.15f + 12f));   // 적 정면보다 넓게 퍼지지 않는다 (집중)
        float k = total > avail ? avail / total : 1f;
        float half = total * k / 2f, shift = 0;
        if (half > roomR) shift = -(half - roomR);
        else if (half > roomL) shift = half - roomL;
        float off = -half + shift;
        for (int i = 0; i < fronts.Count; i++)
        {
            var r = fronts[i];
            float rw = widths[i] * k;
            float center = off + rw / 2f;
            off += rw + 8f * k;
            Vector2 pt = T0 + side * center;
            if (S.Scheme == "flank" && !S.Manoeuvre && task == TacTask.Attack && fronts.Count >= 2 && i == (s.FlankSign > 0 ? fronts.Count - 1 : 0))
                pt += side * (s.FlankSign * rw * 0.6f) + dir * 6f;   // 측면 연대: 비스듬히 바깥으로
            if (r.FallbackUntil > w.Tick) continue;
            r.Task = task; r.Point = ClampMap(w.Map, pt); r.Dir = dir; r.Width = rw;
        }
        foreach (var r in regs)
        {
            if (fronts.Contains(r)) continue;
            if (r.Spent && r.FallbackUntil > w.Tick)
            {
                if (r.Task != TacTask.Fallback) { r.Task = TacTask.Fallback; r.Point = ClampMap(w.Map, r.Center - dir * (R * 1.6f)); r.Dir = dir; r.Width = 0; }
            }
            else { r.Task = TacTask.Reserve; r.Point = ClampMap(w.Map, lcFront - dir * (R * 1.4f) + side * (r.Spent ? 8f * s.FlankSign : 0)); r.Dir = dir; r.Width = 0; }
        }

        // 직속 병과 (대대에 붙지 않은 무리): 포병 → 주공 연대 지원, 기병 → 접촉 전 차장 / 접촉 후 주공 열린 측면, 정찰 · 마차 · 그 밖
        float frontHalf = total * k / 2f;
        Vector2 frontPt = task == TacTask.Defend ? anchor : lc;
        foreach (var g in s.Groups)
        {
            if (g.Directive != d.Id || g.Pending) continue;
            if (g.Battalion != 0 && s.Battalions.Exists(b => b.Id == g.Battalion)) continue;
            g.TaskDir = dir;
            switch (g.Kind)
            {
                case TacKind.Artillery:
                    g.Task = task; g.TaskPoint = frontPt;
                    g.Supports = S.Artillery >= 0.2f && main != null ? main.Id : 0;
                    break;
                case TacKind.Cavalry:
                    g.Task = S.Cavalry < 0.15f ? TacTask.Reserve : task;
                    g.Supports = main?.Id ?? 0;
                    if (!d.HasEnemy && task == TacTask.Attack)
                        g.TaskPoint = ClampMap(w.Map, lc + dir * (R * 0.4f) + side * (s.FlankSign * (frontHalf + 14f)));   // 차장 (적을 아직 못 봄): 측면 · 조금 앞
                    else
                    {
                        Vector2 basis = main != null && task == TacTask.Attack ? main.Center : frontPt;   // 주공의 지금 위치 (과제 지점 = 적 쪽이 아니라)
                        float hw = main != null ? main.Width / 2f : frontHalf;
                        g.TaskPoint = ClampMap(w.Map, basis + side * (s.FlankSign * (hw + 14f)) - dir * 4f);   // 주공의 열린 측면 경계
                    }
                    break;
                case TacKind.Scout: g.Task = task; g.TaskPoint = ClampMap(w.Map, lc + dir * (R * 0.6f)); break;
                case TacKind.Wagon: g.Task = task; g.TaskPoint = ClampMap(w.Map, lc - dir * (R * 0.6f)); break;
                case TacKind.Armor: g.Task = task; g.TaskPoint = ClampMap(w.Map, task == TacTask.Attack ? T0 : anchor); g.TaskWidth = 8; break;
                default: g.Task = task; g.TaskPoint = ClampMap(w.Map, task == TacTask.Attack ? T0 : anchor); g.TaskWidth = Frontage(g); break;
            }
        }
    }

    static int RegimentOfGroup(TacFaction s, TacGroup g)
    {
        if (g.Battalion == 0) return 0;
        foreach (var b in s.Battalions) if (b.Id == g.Battalion) return b.Regiment;
        return 0;
    }

    /// <summary>대대 폭 = 앞줄 중대 폭 합 (예비 중대 하나 빼고) + 간격.</summary>
    float BattalionWidth(TacFaction s, TacBattalion b)
    {
        float wsum = 0, minW = float.MaxValue; int cnt = 0;
        foreach (var g in s.Groups)
            if (g.Battalion == b.Id && g.Kind == TacKind.Line) { float fw = Frontage(g); wsum += fw; cnt++; minW = MathF.Min(minW, fw); }
        if (s.Profile.Strategy.Reserve && cnt >= 3 && !b.ReserveCommitted) wsum -= minW;
        return MathF.Max(10f, wsum + 2f * Math.Max(0, cnt - 2));
    }

    /// <summary>연대 폭 = 앞 대대 폭 합 (예비 대대 빼고) + 간격.</summary>
    float RegimentWidth(TacFaction s, TacRegiment r)
    {
        float wsum = 0; int cnt = 0;
        foreach (var b in s.Battalions)
            if (b.Regiment == r.Id && !b.Spent && b.Id != r.ReserveBattalion) { wsum += BattalionWidth(s, b); cnt++; }
        return MathF.Max(10f, wsum + 6f * Math.Max(0, cnt - 1));
    }

    /// <summary>점령 목표: 우리 · 동맹 것이 아닌 점령 시설 중 전선에서 가까운 곳 (보이는 적 도시 우선). 보이지 않는 시설의 주인은 모른다 (우리 것만 안다).</summary>
    static Vector2? ChooseObjective(World w, TacFaction s, Vector2 from)
    {
        float best = float.MaxValue; Vector2? pos = null;
        foreach (var b in w.Buildings)
        {
            if (!b.Alive || !b.Def.Capturable) continue;
            if (b.Owner == s.Faction || (b.Owner > 0 && w.Allied(s.Faction, b.Owner))) continue;
            float score = Vector2.Distance(b.Pos, from);
            if (w.Fog.CanSee(s.Faction, b) && b.Owner > 0 && b.Def.City) score -= 30f;
            if (score < best) { best = score; pos = b.Pos; }
        }
        return pos;
    }
}
