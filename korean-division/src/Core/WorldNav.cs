using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 유닛 이동의 길찾기 연결부.
///  ① 직선 확인: 몸 크기를 고려한 직선이 비어 있으면 곧장 간다 (확인은 몇 틱마다 한 번).
///  ② 목표 합치기: 같은 (출발 구역, 목표 4×4 칸) 이면 구역 경로(회랑)를 공유, 추격 목표는 2×2 로 양자화.
///  ③ 시간 분할: 경로 계산은 틱당 작업량 한도 · 지역 흐름장은 틱당 개수 한도 안에서 — 준비 전에는 직진 (결정적: 노드 수 기준).
///  ④ 계층형: 구역 경로 → 구역 안 지역 흐름장 (Nav.cs).
///  ⑤ 큰 그룹: 리더 위치로 그룹 경로 한 번, 구성원은 압축된 상대 위치로 목표 둘레에 조준 (MoveGroup).
///  ⑥ 크기별 그래프: 큰 유닛(전차)은 여유 공간(칸 중심 ~ 막힌 칸 가장자리 ≥ LargeClearance)이 있는 칸만 지나는 별도 그래프(Nav.Large)로 길을 찾는다.
///  ⑦ 정체 해소: 이동 중인데 못 가면 경로를 새로 구하고, 그래도 안 되면 여유 칸 쪽으로 · 옆으로 비켜선다 (CheckProgress).
///  ⑧ 진형: 그룹에 FormPlan 이 있으면 각 유닛은 자기 슬롯(목표 + 오프셋)으로 간다 (Formation.cs).
/// </summary>
public sealed partial class World
{
    Nav? nav;
    int navEpoch;
    internal Nav Nav => nav ??= new Nav(Map);

    /// <summary>틱당 경로 계산 작업량 (확정 타일 + A* 노드 수). 넘으면 남은 요청은 다음 틱으로.</summary>
    public const int RouteWorkPerTick = 12000;
    const float DirectRange = 30f;
    /// <summary>그룹 이동 (추격 아님) 의 직선 확인 거리 — 넓은 평지에서 큰 무리가 구역 경로 출입구로 모여 짓눌리지 않게 ((a)-3 · (c)-4).</summary>
    const float GroupDirectRange = 60f;
    /// <summary>이보다 반지름이 큰 유닛(전차 0.6)은 큰 길찾기 그래프를 쓴다.</summary>
    public const float LargeRadius = 0.45f;
    /// <summary>큰 그래프의 통행 조건: 칸 중심에서 막힌 칸 가장자리까지 이 거리 이상 (전차 반지름 0.6 + 여유).</summary>
    public const float LargeClearance = 0.62f;
    /// <summary>큰 유닛이 이 정체 단계(45틱 단위)까지 못 나아가면 이동 명령을 포기한다.</summary>
    const int GiveUpStuckLevel = 5;

    /// <summary>[통계] 길찾기 캐시 현황 (테스트 · 스트레스용).</summary>
    public (int Routes, int Fields, int Pending, int Pooled, int RoutesComputed, int FieldsComputed) NavStats =>
        nav == null ? default : (nav.RouteCount, nav.FieldCount, nav.PendingCount, nav.PooledArrays, nav.RoutesComputed, nav.FieldsComputed);

    /// <summary>그룹 흐름을 따라 이동. 도착하면 true. 진형 그룹이면 자기 슬롯(목표 + 오프셋)으로 간다.</summary>
    bool FollowGroup(Unit u, float dt)
    {
        var g = u.Group!;
        if (!g.Inited) g.Init();
        if (u.Company?.Anchor is { } anchor && ReferenceEquals(anchor.Group, g)) return FollowAnchor(u, anchor, dt);   // 중대 깃발 대형 (OrgFormation.cs)
        Vector2 goal = g.Goal;
        var nr = u.NavRoute;
        if (nr != null && nr.Valid && !nr.Pending && nr.Approx && ReferenceEquals(u.NavToken, g))
            goal = new Vector2(nr.GoalX + 0.5f, nr.GoalY + 0.5f);   // 도달 불가 목표 → 가장 가까운 도달 가능 지점
        // 큰 유닛이 비켜서기 · 경로 갱신 · 대기 아군 밀기까지 다 해 보고도 ~4초(정체 5단계) 못 나아가면 포기하고 명령을 끝낸다
        // (좁은 틈 · 건물 모서리에 낀 전차가 영원히 이동 명령 상태로 남지 않게 — AI 는 필요하면 새로 명령한다)
        // nobreak: 붕괴가 꺼져 엎드린 적 보병이 달아나지 않으므로, 탄이 떨어져 (쏴서 치울 수 없는) 큰 유닛이 적 보병에게 막히면 정체 2단계 (~1.5초) 에서 바로 포기한다
        if (u.Large && (u.StuckLevel >= GiveUpStuckLevel || (u.StuckLevel >= 2 && !u.HasAmmo && HostileBlocking(u) != null))) { u.StuckLevel = 0; u.NudgeUntil = 0; return true; }
        var plan = g.Plan;
        if (plan != null && plan.Off.TryGetValue(u.Id, out Vector2 so))
        {
            // 진형: 슬롯이 이 유닛이 설 수 있는 칸이어야 한다 (아니면 목표점으로)
            Vector2 slot = goal + so;
            var (sx, sy) = GameMap.TileOf(slot);
            if (!SlotOk(u, sx, sy)) slot = goal;
            float sd = Vector2.Distance(slot, u.Pos);
            float spd = MathF.Min(UnitSpeed(u), PlanCap(u, plan));
            if (sd <= 0.3f || sd <= spd * dt || (sd < 3f && u.StuckLevel >= 2))
            {
                if (sd <= spd * dt) u.Pos = slot;
                if (u.Def.TurnRate <= 0) u.Facing = plan.Facing;      // 도착한 유닛은 진행 방향을 바라본다 (느리게 도는 견인포는 지금 방향 유지)
                u.StuckLevel = 0;
                return true;
            }
            return !MoveToward(u, slot, slot, false, dt, 1f, sd < FaceSlotDist ? plan.Dir : default);
        }
        float dist = Vector2.Distance(goal, u.Pos);
        float uspd = UnitSpeed(u);
        if (dist <= g.ArriveRadius || dist <= uspd * dt)
        {
            if (dist <= uspd * dt) u.Pos = goal;
            return true;
        }
        // 더 가까이 갈 수 없는 경우도 도착: ① 목표가 이 유닛이 설 수 없는 칸이라 가장 가까운 설 수 있는 칸(NavGX/Y)까지 왔다
        // ② 목표 근처에서 1.5초 넘게 못 나아간다 (몸집이 큰 유닛 · 먼저 도착한 아군이 목표를 둘러싼 경우)
        if (ReferenceEquals(u.NavToken, g) && u.NavRawX == Math.Clamp((int)MathF.Floor(g.Goal.X), 0, Map.Width - 1) && u.NavRawY == Math.Clamp((int)MathF.Floor(g.Goal.Y), 0, Map.Height - 1)
            && (u.NavGX != u.NavRawX || u.NavGY != u.NavRawY) && dist < 2f)
        {
            var (ux, uy) = GameMap.TileOf(u.Pos);
            if (ux == u.NavGX && uy == u.NavGY) { u.StuckLevel = 0; return true; }
        }
        if (u.StuckLevel >= 2 && dist < MathF.Max(2f, g.ArriveRadius * 2f)) { u.StuckLevel = 0; return true; }
        Vector2 steer = goal;
        if (ReferenceEquals(u.FormGroup, g) && u.FormOff != Vector2.Zero)
        {
            Vector2 p = goal + u.FormOff;
            var (px, py) = GameMap.TileOf(p);
            if (Map.IsWalkable(px, py)) steer = p;
        }
        return !MoveToward(u, goal, steer, false, dt);
    }

    /// <summary>슬롯 타일이 이 유닛(크기별 그래프)이 서 있을 수 있는 곳인가.</summary>
    bool SlotOk(Unit u, int x, int y)
    {
        if (!Map.IsWalkable(x, y)) return false;
        if (!u.Large) return true;
        var lg = Nav.Large;
        lg.Sync();
        return lg.Walk(x, y);
    }

    /// <summary>진형 슬롯을 이 유닛이 설 수 있는 가장 가까운 위치로 (막힌 칸 · 좁은 칸이면 이동).</summary>
    internal Vector2 ShiftSlot(Unit u, Vector2 p)
    {
        p = new Vector2(Math.Clamp(p.X, 0.5f, Map.Width - 0.5f), Math.Clamp(p.Y, 0.5f, Map.Height - 0.5f));
        var (x, y) = GameMap.TileOf(p);
        if (SlotOk(u, x, y)) return p;
        (int X, int Y)? q;
        if (u.Large) { var lg = Nav.Large; lg.Sync(); q = lg.NearestOk(x, y, 8); }
        else
        {
            var n = Map.NearestWalkable(x, y, 8);
            q = Map.IsWalkable(n.X, n.Y) ? n : null;
        }
        if (q is not { } t) return p;
        return new Vector2(t.X + 0.5f, t.Y + 0.5f);
    }

    /// <summary>goal 쪽으로 한 틱 이동 (직선이 보이면 직진, 아니면 경로). 갈 수 없으면 false. chase = 움직이는 표적 추격.</summary>
    bool StepToward(Unit u, Vector2 goal, FlowField? field, float dt, bool chase = false) => MoveToward(u, goal, goal, chase, dt);

    /// <summary>슬롯까지 이 거리 안이면 몸은 대형 방향을 본다 (작은 보정 벡터로 몸을 돌리지 않음 — 움찔거림 방지, docs/COLLISION_ANALYSIS.md (b)-1).</summary>
    internal const float FaceSlotDist = 0.8f;
    /// <summary>진형 없는 이동: 목표까지 이 거리 안의 마지막 몇 걸음은 몸을 돌리지 않는다.</summary>
    const float FaceKeepDist = 0.25f;

    bool MoveToward(Unit u, Vector2 navGoal, Vector2 steer, bool chase, float dt, float speedMul = 1f, Vector2 face = default)
    {
        Vector2 to = steer - u.Pos;
        float dist = to.Length();
        if (dist < 1e-4f) return true;
        Vector2 dir;
        CheckProgress(u, steer, dt);
        if (u.NudgeUntil > Tick) dir = u.NudgeDir;   // 정체 해소: 잠깐 비켜선다
        else if (!NavDirection(u, navGoal, steer, chase, out dir)) return false;
        float spd = UnitSpeed(u) * speedMul;   // 돌격 +30% · 제압 감속 · 스태미나 (Combat3.cs) · 깃발 대기 배수 (OrgFormation.cs)
        if (!chase && u.Group?.Plan is { } plan) spd = MathF.Min(spd, PlanCap(u, plan));   // 진형은 가장 느린 유닛에 맞춘다
        float want = DetMath.Atan2(dir.Y, dir.X);
        if (u.Def.TurnRate <= 0 && u.NudgeUntil <= Tick)
        {
            if (face != default) u.Facing = TurnToward(u.Facing, DetMath.Atan2(face.Y, face.X), u.Def.TurnRadPerSec * dt);   // 슬롯 근처: 대형 방향
            else if (chase || dist > FaceKeepDist) u.Facing = TurnToward(u.Facing, want, u.Def.TurnRadPerSec * dt);
        }
        else u.Facing = TurnToward(u.Facing, want, u.Def.TurnRadPerSec * dt);
        if (u.Def.TurnRate > 0)
        {
            // 느리게 도는 유닛 (견인포): 차체가 진행 방향과 크게 어긋나 있으면 돌면서 천천히 (옆으로 미끄러지지 않게) — cos(오차), 하한 0.2
            float err = MathF.Abs(DetMath.WrapPi(want - u.Facing));
            float k = DetMath.Cos(err);
            spd *= MathF.Max(0.2f, k);
            if (k < 0.5f) { u.ProgPos = u.Pos; u.ProgTick = Tick; }   // 제자리에 가깝게 도는 중 = 정체가 아니다 (정체 판정 · 포기를 미룸)
        }
        float step = MathF.Min(spd * dt, dist);
        u.Velocity = dir * spd;
        u.Pos += dir * step;
        return true;
    }

    /// <summary>
    /// 정체 감지: 이동 중인데 0.75초(45틱) 동안 기대 거리의 20% 도 못 갔으면 StuckLevel 을 올린다.
    /// 1단계: 경로 · 직선 확인을 새로 구한다. 2단계 이상: 큰 유닛이 좁은 칸에 끼었으면 가장 가까운 여유 칸으로, 아니면 옆으로 비켜선다.
    /// </summary>
    readonly List<Unit> jamBuf = new();
    /// <summary>조향점까지 이보다 가까우면 정체여도 옆걸음 (Nudge) 하지 않는다 (작은 유닛).</summary>
    const float NudgeMinDist = 1.5f;
    /// <summary>중대 깃발 (가상 유닛) 의 직선 확인 거리 — 깃발은 수십 개뿐이라 평지에서는 거리 제한 없이 직선 (구역 경로 출입구로 여러 중대가 모이지 않게, (a)-3).</summary>
    const float FlagDirectRange = 1000f;

    void CheckProgress(Unit u, Vector2 steer, float dt)
    {
        if (Tick - u.ProgLast > 3) { u.ProgPos = u.Pos; u.ProgTick = Tick; u.StuckLevel = 0; }
        u.ProgLast = Tick;
        if (u.StuckLevel >= 3 && u.Large) YieldNeighbors(u);
        if (u.StuckLevel >= 1 && u.Large && u.Order == UnitOrder.Move && u.Target == null && HostileBlocking(u) is { } blocker) u.Target = blocker;   // 서서 길을 막은 적 보병은 밀 수 없다 → 그 적을 쏜다 (Move 분기가 Large 표적을 교전 처리) — 제자리 맴돌며 5초+ 정체 방지
        if (Tick - u.ProgTick < 45) return;
        float moved = Vector2.Distance(u.Pos, u.ProgPos);
        float expect = u.Def.Speed * 45 * dt;
        // supp2: 큰 유닛이 이 구간에 옆걸음 (Nudge) 했으면 변위가 아니라 조향점 쪽 순 전진으로 판정 — 대기 아군 무리에 낀 전차가 옆걸음 ↔ 밀려 돌아옴을
        // 되풀이하면 변위는 커도 전진은 0 이라 정체 단계가 2 → 0 으로 되돌아가 (아군 비켜서기 · 포기 단계에 못 감) 5초 넘게 제자리였다. 옆걸음 안 한 구간은 예전 그대로.
        // 이 판정은 정체 3단계 (대기 아군 비켜서기) 까지만 올린다 — 포기 (5단계, 명령 끝) 는 예전 판정으로만 (다리 위 큰 유닛이 포기해 길을 막지 않게).
        bool nudgeJam = false;
        if (u.Large && u.NudgeUntil > u.ProgTick && moved >= expect * 0.2f)   // 대기 아군 (비켜서기 대상) 이 바로 앞에 있을 때만
        {
            float prog = Vector2.Distance(u.ProgPos, steer) - Vector2.Distance(u.Pos, steer);
            if (prog < expect * 0.2f && IdleFriendNear(u)) { nudgeJam = true; moved = MathF.Max(0f, prog); }
        }
        u.ProgPos = u.Pos; u.ProgTick = Tick;
        if (moved >= expect * 0.2f) { u.StuckLevel = 0; return; }
        u.StuckLevel = nudgeJam ? Math.Max(u.StuckLevel, Math.Min(u.StuckLevel + 1, 3)) : Math.Min(u.StuckLevel + 1, 8);
        Stats.StuckUps++;
        u.NavRoute = null; u.NavSeg = 0; u.NavRawX = -1; u.LosOk = false; u.NextLosTick = Tick + 20;
        if (u.StuckLevel < 2) return;
        Vector2 d = steer - u.Pos;
        if (!u.Large && d.LengthSquared() < NudgeMinDist * NudgeMinDist) return;   // 슬롯 · 목표 바로 앞 (대열 속 · 접촉선): 옆걸음하지 않는다 — 밀림 때문에 늘 '정체' 로 보여 옆걸음 ↔ 복귀가 떨림을 만든다
        d = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : new Vector2(1, 0);
        if (u.StuckLevel >= 3)
        {
            // 서로 막은 무리(교착): 정체 중인 이웃 중 Id 가 가장 큰 유닛만 계속 밀고 나가고, 나머지는 그 유닛에게서 멀어지며 길을 터 준다
            Unit? lead = null;
            foreach (var o in UnitsInRadius(u.Pos, 2.7f, jamBuf))
                if (o.StuckLevel >= 2 && o.ProgLast >= Tick - 3 && (lead == null || o.Id > lead.Id)) lead = o;
            if (lead == u && u.StuckLevel < 5) { u.NudgeUntil = 0; return; }   // 5단계부터는 선두도 옆걸음 · 후진으로 빠져나온다
            if (lead != null && lead != u)
            {
                Vector2 away = u.Pos - lead.Pos;
                u.NudgeDir = away.LengthSquared() > 1e-6f ? Vector2.Normalize(away) : -d;
                u.NudgeUntil = Tick + 30;
                return;
            }
        }
        Vector2 nd = default;
        if (u.Large)
        {
            var lg = Nav.Large;
            lg.Sync();
            var (ux, uy) = GameMap.TileOf(u.Pos);
            if (!lg.Walk(ux, uy) && FindNearbyOk(lg, u, ux, uy) is { } q) nd = new Vector2(q.X + 0.5f, q.Y + 0.5f) - u.Pos;
        }
        if (nd == default)
        {
            float sign = ((u.Id + u.StuckLevel) & 1) == 0 ? 1f : -1f;
            nd = new Vector2(-d.Y, d.X) * sign + d * 0.3f;
        }
        u.NudgeDir = nd.LengthSquared() > 1e-6f ? Vector2.Normalize(nd) : d;
        u.NudgeUntil = Tick + 20 + 5 * Math.Min(u.StuckLevel, 4);
    }

    /// <summary>
    /// 정체한 큰 유닛 앞의 가만히 서 있는 아군(대기 중, 도시 위면 반경 2배)은 큰 유닛에게 밀려도 서로 · 벽에 눌려 버티는 균형이 생긴다
    /// (이동 중 유닛의 밀침 가중치가 0.25 로 작음). 큰 유닛이 정체 3단계부터는 이런 대기 아군이 매 틱 조금씩 길을 터 준다.
    /// </summary>
    /// <summary>supp2: 큰 유닛 바로 앞 (반지름 + 1.3칸) 에 비켜설 수 있는 대기 아군 (YieldNeighbors 대상) 이 있는가.</summary>
    bool IdleFriendNear(Unit u)
    {
        foreach (var o in UnitsInRadius(u.Pos, u.Radius + 1.3f, jamBuf))
            if (o != u && o.Alive && o.Owner == u.Owner && o.Order == UnitOrder.Idle && !o.Immovable && !o.Large) return true;
        return false;
    }

    void YieldNeighbors(Unit u)
    {
        foreach (var o in UnitsInRadius(u.Pos, u.Radius + 1.3f, jamBuf))
        {
            if (o == u || o.Owner != u.Owner || o.Order != UnitOrder.Idle || o.Immovable || o.Large) continue;
            Vector2 away = o.Pos - u.Pos;
            float l = away.Length();
            if (l < 1e-4f) { away = new Vector2(((o.Id & 1) == 0) ? 1 : -1, 0); l = 1; }
            Vector2 np = o.Pos + away / l * 0.06f;
            var (tx, ty) = GameMap.TileOf(np);
            if (Map.IsWalkable(tx, ty)) o.Pos = np;
        }
    }

    /// <summary>큰 유닛 바로 앞 (반지름 + 1.3칸) 의 큰 유닛이 아닌 가장 가까운 적 유닛 (밀어낼 수 없는 장애물). 없으면 null.</summary>
    Unit? HostileBlocking(Unit u)
    {
        Unit? best = null; float bd = float.MaxValue;
        foreach (var o in UnitsInRadius(u.Pos, u.Radius + 1.3f, jamBuf))
        {
            if (o == u || !o.Alive || o.Large || !IsHostile(u.Owner, o.Owner)) continue;
            float d = Vector2.DistanceSquared(o.Pos, u.Pos);
            if (d < bd || d == bd && o.Id < best!.Id) { bd = d; best = o; }
        }
        return best;
    }

    /// <summary>가까운 (4칸 이내) 통행 가능 칸 중 막힘 없이 바로 갈 수 있는 곳. 없으면 null.</summary>
    (int X, int Y)? FindNearbyOk(Nav nv, Unit u, int ux, int uy)
    {
        for (int r = 1; r <= 4; r++)
        {
            int best = int.MaxValue, bx = 0, by = 0;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !nv.Walk(ux + dx, uy + dy)) continue;
                int d = dx * dx + dy * dy;
                if (d >= best || !Map.HasLineOfSight(u.Pos, new Vector2(ux + dx + 0.5f, uy + dy + 0.5f))) continue;
                (best, bx, by) = (d, ux + dx, uy + dy);
            }
            if (best != int.MaxValue) return (bx, by);
        }
        return null;
    }

    /// <summary>이번 틱 이동 방향. 길이 없으면 false.</summary>
    bool NavDirection(Unit u, Vector2 navGoal, Vector2 steer, bool chase, out Vector2 dir)
    {
        Vector2 to = steer - u.Pos;
        float dist = to.Length();
        dir = dist > 1e-4f ? to / dist : Vector2.Zero;
        object? token = chase ? u.Target : u.Group;
        if (!ReferenceEquals(token, u.NavToken) || u.NavEpoch != navEpoch)
        {
            u.NavToken = token; u.NavEpoch = navEpoch;
            u.NavRoute = null; u.NavSeg = 0; u.NavRawX = -1; u.NextLosTick = 0; u.LosOk = false;
        }
        // ① 직선 확인 (몸 크기 그대로 — 큰 유닛은 좁은 틈을 직선으로 지나려 하지 않는다)
        if (dist <= (chase ? DirectRange : GroupDirectRange) || (u.Id >= FlagIdBase && dist <= FlagDirectRange))
        {
            if (Tick >= u.NextLosTick || Vector2.DistanceSquared(steer, u.LosGoal) > 4f)
            {
                u.LosOk = Map.ClearLine(u.Pos, steer, u.Radius);
                u.LosGoal = steer;
                u.NextLosTick = Tick + 10 + u.Id % 5;
            }
            if (u.LosOk) return true;
        }
        return RouteDirection(u, navGoal, chase, ref dir);
    }

    static int SecOf(NavRoute r, int i) => i < r.Segs.Length ? r.Segs[i].Region : r.GoalRegion;

    static int FindSeg(NavRoute r, int sector, int hint)
    {
        int n = r.Segs.Length;
        for (int d = 0; d <= n; d++)
        {
            if (hint + d <= n && SecOf(r, hint + d) == sector) return hint + d;
            if (hint - d >= 0 && SecOf(r, hint - d) == sector) return hint - d;
        }
        return -1;
    }

    bool RouteDirection(Unit u, Vector2 navGoal, bool chase, ref Vector2 dir)
    {
        var nv0 = Nav;
        nv0.Sync();
        int W = Map.Width, H = Map.Height;
        int ux = Math.Clamp((int)MathF.Floor(u.Pos.X), 0, W - 1), uy = Math.Clamp((int)MathF.Floor(u.Pos.Y), 0, H - 1);
        if (!Map.IsWalkable(ux, uy)) return true;   // 막힌 칸 위 (밀려나는 중): 직진, 지형 해소가 밀어낸다

        // 크기별 그래프: 큰 유닛은 여유 있는 칸만. 지금 서 있는 칸이 좁으면 가까운 여유 칸으로 빠져나온다 (없으면 일반 그래프로 임시 대체)
        var nv = nv0;
        if (u.Large)
        {
            nv = nv0.Large;
            nv.Sync();
            if (!nv.Walk(ux, uy))
            {
                if (FindNearbyOk(nv, u, ux, uy) is { } q)
                {
                    Vector2 d0 = new Vector2(q.X + 0.5f, q.Y + 0.5f) - u.Pos;
                    if (d0.LengthSquared() > 1e-6f) dir = Vector2.Normalize(d0);
                    return true;
                }
                nv = nv0;
            }
        }
        byte kind = (byte)(ReferenceEquals(nv, nv0) ? 0 : 1);
        if (u.NavKind != kind) { u.NavRoute = null; u.NavSeg = 0; u.NavRawX = -1; u.NavKind = kind; }

        int rx = Math.Clamp((int)MathF.Floor(navGoal.X), 0, W - 1), ry = Math.Clamp((int)MathF.Floor(navGoal.Y), 0, H - 1);
        if (rx != u.NavRawX || ry != u.NavRawY)
        {
            u.NavRawX = rx; u.NavRawY = ry;
            int gx = rx, gy = ry;
            if (!nv.Walk(gx, gy))
            {
                if (kind == 1 && nv.NearestOk(gx, gy) is { } q2) (gx, gy) = q2;
                else (gx, gy) = Map.NearestWalkable(gx, gy);
            }
            else if (chase)
            {
                int qx = gx & ~1, qy = gy & ~1;   // ② 움직이는 표적: 2×2 칸으로 양자화해 경로 · 흐름장 공유
                if (nv.Walk(qx, qy)) { gx = qx; gy = qy; }
            }
            u.NavGX = gx; u.NavGY = gy;
            if (u.NavRoute != null && ((u.NavRoute.GX >> 2) != (gx >> 2) || (u.NavRoute.GY >> 2) != (gy >> 2))) { u.NavRoute = null; u.NavSeg = 0; }
        }

        int su = nv.SectorId(ux, uy), gs = nv.SectorId(u.NavGX, u.NavGY);
        int ureg = nv.RegionOf(ux, uy);
        NavField? f;
        int wx, wy;
        if (su == gs && u.NavRoute == null)
        {
            // 같은 구역: 목표로 가는 지역 흐름장 하나면 된다
            f = nv.GetField(gs, u.NavGX, u.NavGY, Tick);
            if (f == null) return true;
            if (nv.Waypoint(f, ux, uy, out wx, out wy)) { SetDir(u, wx, wy, ref dir); return true; }
            if (nv.CostAt(f, ux, uy) < float.PositiveInfinity) return true;   // 목표 칸
            // 같은 구역이지만 구역 안에서는 못 간다 (다른 구역으로 돌아가야 함) → 경로로
        }

        var r = u.NavRoute;
        if (r == null || !r.Valid)
        {
            r = AcquireRoute(u, nv, kind, ux, uy, ureg);
            u.NavRoute = r; u.NavSeg = 0;
        }
        if (r.Pending) return true;       // ③ 계산 순서를 기다리는 동안 직진
        if (r.Failed) return false;

        int n = r.Segs.Length;
        int i = Math.Clamp(u.NavSeg, 0, n);
        if (SecOf(r, i) != ureg)
        {
            int j;
            if (i < n && SecOf(r, i + 1) == ureg) j = i + 1;
            else if (i > 0 && SecOf(r, i - 1) == ureg) j = i - 1;
            else j = FindSeg(r, ureg, i);
            if (j < 0)
            {
                // 경로에서 벗어남 (밀려남 등): 이 위치 기준 경로를 다시 구한다
                var r2 = nv.GetRoute(ux, uy, u.NavGX, u.NavGY, Tick);
                if (ReferenceEquals(r2, r) || !r2.Covers(ureg)) r2 = nv.GetRoute(ux, uy, u.NavGX, u.NavGY, Tick, personal: true);
                u.NavRoute = r2; u.NavSeg = 0;
                return true;
            }
            i = j;
        }
        u.NavSeg = i;

        if (i == n)
        {
            int tx = r.Approx ? r.GoalX : u.NavGX, ty = r.Approx ? r.GoalY : u.NavGY;
            if (nv.SectorId(tx, ty) != su) return true;
            f = nv.GetField(su, tx, ty, Tick);
            if (f == null) return true;
            if (nv.Waypoint(f, ux, uy, out wx, out wy)) { SetDir(u, wx, wy, ref dir); return true; }
            if (nv.CostAt(f, ux, uy) < float.PositiveInfinity)
                return !(chase && r.Approx);   // 도달 불가 표적을 쫓다 가장 가까운 곳에 닿음 → 추격 포기
            Repath(u, nv, ux, uy);
            return true;
        }

        var sg = r.Segs[i];
        if (ux == sg.ExitX && uy == sg.ExitY)
        {
            Vector2 d = new Vector2(sg.PeerX + 0.5f, sg.PeerY + 0.5f) - u.Pos;
            float l = d.Length();
            if (l > 1e-4f) dir = d / l;
            return true;
        }
        f = nv.GetField(su, sg.ExitX, sg.ExitY, Tick);
        if (f == null) { SetDir(u, sg.ExitX, sg.ExitY, ref dir); return true; }
        if (nv.Waypoint(f, ux, uy, out wx, out wy)) SetDir(u, wx, wy, ref dir);
        else { Repath(u, nv, ux, uy); SetDir(u, sg.ExitX, sg.ExitY, ref dir); }
        return true;
    }

    static void SetDir(Unit u, int tx, int ty, ref Vector2 dir)
    {
        Vector2 w = new Vector2(tx + 0.5f, ty + 0.5f) - u.Pos;
        if (w.LengthSquared() > 1e-6f) dir = Vector2.Normalize(w);
    }

    /// <summary>구역 안에서 출구에 닿을 수 없는 칸(웅덩이 등)에 있을 때: 이 유닛 기준 개인 경로를 (가끔) 새로 구한다.</summary>
    void Repath(Unit u, Nav nv, int ux, int uy)
    {
        if (Tick < u.NextPersonalTick) return;
        u.NextPersonalTick = Tick + 60;
        u.NavRoute = nv.GetRoute(ux, uy, u.NavGX, u.NavGY, Tick, personal: true);
        u.NavSeg = 0;
    }

    NavRoute AcquireRoute(Unit u, Nav nv, byte kind, int ux, int uy, int ureg)
    {
        var g = u.Group;
        if (g != null && g.Big && ReferenceEquals(u.NavToken, g))
        {
            var gr = kind == 1 ? g.RouteL : g.Route;
            if (gr == null || !gr.Valid)
            {
                var l = g.Leader;
                int lx = ux, ly = uy;
                if (l != null && l.Alive)
                {
                    int tx = Math.Clamp((int)MathF.Floor(l.Pos.X), 0, Map.Width - 1), ty = Math.Clamp((int)MathF.Floor(l.Pos.Y), 0, Map.Height - 1);
                    if (nv.Walk(tx, ty)) (lx, ly) = (tx, ty);
                }
                gr = nv.GetRoute(lx, ly, u.NavGX, u.NavGY, Tick);   // 그룹 경로: 리더 위치 기준 (공유)
                if (kind == 1) g.RouteL = gr; else g.Route = gr;
            }
            if (gr.Covers(ureg) && (gr.GX >> 2) == (u.NavGX >> 2) && (gr.GY >> 2) == (u.NavGY >> 2)) return gr;
        }
        return nv.GetRoute(ux, uy, u.NavGX, u.NavGY, Tick);
    }
}
