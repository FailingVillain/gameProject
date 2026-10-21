using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 중대 깃발 대형 이동 (ROADMAP §2-4b 10차 확정, 설계 docs/ORG_FORMATION_PLAN.md §3).
///  · 이동 명령 → 중대마다 깃발(가상 유닛) 하나가 경로를 따라가고, 구성원은 깃발 기준 회전 슬롯을 따라간다 (경로 계산 = 중대당 1개).
///  · 깃발은 낙오자를 기다린다 (슬롯에서 2.5칸 넘게 떨어진 구성원이 1/3 넘으면 정지, 1.2칸 넘게가 10% 넘으면 절반 속도; 종대는 선두 3줄만 기다림). 깃발 속도 = 가장 느린 구성원 × 0.9 (따라잡을 여유).
///  · 좁은 길: 깃발 좌우 폭 선의 25% 이상이 막히면 2열 종대로, 120틱 연속 트이면 원래 대형으로 (balance.json narrowBlockFrac · narrowClearTicks).
///  · 도착: 깃발이 목표에 닿으면 구성원은 최종 슬롯(목표 + 도착 방향 회전 오프셋)으로 간다.
///  · 대대 대형 = 중대 블록 배치 (횡대 · 종대 · 사다리 · 2선+예비), 중대 대형 = 병사 배치 (Company.Form).
///  · 병사가 죽으면 그 슬롯은 비어 있다 — 다시 이동 명령을 내리거나, 장교가 있으면 빈자리 점검 (Squads.cs StepGapChecks, 10초 간격) 이 메운다.
/// 모두 틱 · 입력의 함수 (Companies 목록 순서, 유닛 Id 순) — 결정적.
/// </summary>
public sealed partial class World
{
    /// <summary>깃발 가상 유닛 Id = 이 값 + 중대 Id (월드 엔티티 Id 와 겹치지 않는 범위, byId 에 없음).</summary>
    internal const int FlagIdBase = 1 << 29;
    const float LagWait = 2.5f, LagSlow = 1.2f;
    const float FlagSpeedFrac = 0.9f;
    const int MaxWaitTicks = 600;           // 낙오자를 10초 넘게 기다리면 절반 속도로라도 출발 (끼어 버린 병사 때문에 중대가 영원히 서지 않게)
    const int NarrowCheckInterval = 10;   // 종대 진입 문턱 · 해제 틱 = balance.json narrowBlockFrac · narrowClearTicks (CheckNarrow)
    /// <summary>깃발 속도 배수 (0~1) 의 초당 변화 한도: 서는 쪽은 빠르게 (0.5초에 1 → 0), 출발은 부드럽게 (1초).</summary>
    const float FlagDecelPerSec = 2f, FlagAccelPerSec = 1f;
    /// <summary>현재 방향 유지 범위: 모든 중대의 마지막 방향이 같고 목표가 이 거리 안이면 그 방향으로 도착 (옆걸음 · 짧은 전진).</summary>
    public const float KeepFacingRange = 20f;

    /// <summary>
    /// 이동 · 공격 이동 명령에서 중대 소속 유닛을 떼어 깃발 이동으로 시작하고, 나머지(중대 없는 유닛 · 중대 일부만 고른 유닛 = 임시 묶음)를 돌려준다.
    /// form: 우클릭 드래그의 방향 · 폭 (Type 은 중대 없는 유닛용 — 중대는 Company.Form).
    /// </summary>
    internal List<Unit> StartCompanyOrders(List<Unit> list, Vector2 target, UnitOrder order, FormSpec? form)
    {
        if (!list.Exists(u => u.Company != null)) return list;
        var loose = new List<Unit>();
        var seen = new HashSet<int>();
        var byCompany = new Dictionary<int, List<Unit>>();
        foreach (var u in list)
        {
            if (!seen.Add(u.Id)) continue;
            if (u.Company is not { } c) { loose.Add(u); continue; }
            if (!byCompany.TryGetValue(c.Id, out var l)) byCompany[c.Id] = l = new List<Unit>();
            l.Add(u);
        }
        var blocks = new List<OrgLayout.Block>();
        foreach (var c in Companies)   // 중대 목록 순서 (결정적)
            if (byCompany.TryGetValue(c.Id, out var l))
            {
                // 부분 선택 (중대 일부 — 분대 하나 등) = 임시 묶음: 옛 방식 그룹 이동, 소속은 그대로 (ROADMAP §2-7 선택 방법)
                if (l.Count < c.Members.Count) { loose.AddRange(l); continue; }
                l.Sort((a, b) => a.Id.CompareTo(b.Id));
                blocks.Add(new OrgLayout.Block(c, l, c.Form));
            }
        var plan = OrgLayout.Plan(blocks, target, form, KeepFacing(blocks), BatFormOf(blocks), FormRules, TacRules, Defs.CombatRules);
        foreach (var b in plan.Blocks) StartCompanyMove(b, plan.Dir, order);
        return loose;
    }

    /// <summary>모든 중대의 마지막 도착 방향이 같으면 그 방향 (아니면 0).</summary>
    static Vector2 KeepFacing(List<OrgLayout.Block> blocks)
    {
        Vector2 f = Vector2.Zero;
        foreach (var b in blocks)
        {
            var cf = b.Company?.Facing ?? Vector2.Zero;
            if (cf == Vector2.Zero) return Vector2.Zero;
            if (f == Vector2.Zero) f = cf;
            else if (f != cf) return Vector2.Zero;
        }
        return f;
    }

    /// <summary>블록들이 모두 한 대대 소속이면 그 대대 대형, 아니면 횡대.</summary>
    static BatForm BatFormOf(List<OrgLayout.Block> blocks)
    {
        var r = blocks.Count > 0 ? blocks[0].Company?.Battalion : null;
        if (r == null) return BatForm.Line;
        foreach (var b in blocks) if (b.Company?.Battalion != r) return BatForm.Line;
        return r.Form;
    }

    void StartCompanyMove(OrgLayout.Block b, Vector2 dir, UnitOrder order)
    {
        var c = b.Company!;
        var units = b.Units;
        var g = new MoveGroup(this, b.Center, units.Count) { ArriveRadius = 0.6f, Inited = true };
        g.Big = false; g.Members = null;
        Vector2 cen = Vector2.Zero;
        foreach (var u in units) cen += u.Pos;
        cen /= units.Count;
        // 깃발 = 가장 느린 구성원의 몸 (큰 유닛이 있으면 가장 느린 큰 유닛 — 큰 그래프로 길을 찾아야 전차도 따라온다)
        UnitDef? def = null;
        bool anyLarge = units.Exists(u => u.Large);
        foreach (var u in units)
            if ((!anyLarge || u.Large) && (def == null || u.Def.Speed < def.Speed)) def = u.Def;
        // 이어 걷기 (최소 재배정, docs/COLLISION_ANALYSIS.md (a)-6 · (b)-3): 행군 중인 중대가 비슷한 방향으로 새 명령을 받으면
        // 깃발 위치 · 진행 방향 · 속도를 이어받고, 슬롯은 지금 슬롯 위치에서 가까운 순으로 다시 배정, 같은 쪽으로 걷던 병사는 반응 지연 없이 이어 걷는다.
        var prev = c.Anchor;
        Vector2 head0 = g.Goal - (prev?.Pos ?? cen);
        head0 = head0.LengthSquared() > 0.25f ? Vector2.Normalize(head0) : dir;
        bool cont = prev != null && !prev.Arrived && !prev.Column && Vector2.Dot(head0, prev.Heading) > ContinueDot && units.TrueForAll(u => ReferenceEquals(u.Group, prev.Group));
        // F1 2차 (ContactF2.cs): 제자리 방향 전환 (깃발 행군 없이 새 방향 슬롯에 가까운 병사 배정) · 접적 중 선까지 전진 (지금 배치 그대로, 대형 모양 없음)
        var mode = cont ? F2Mode.Formation : F2ModeOf(c, cen, g.Goal, dir);
        var flag = new Unit(FlagIdBase + c.Id, def!, c.Owner, cen) { Group = g };
        flag.Pos = cont ? prev!.Pos : mode == F2Mode.Rotate ? ShiftSlot(flag, g.Goal) : ShiftSlot(flag, cen);
        Vector2 head = g.Goal - flag.Pos;
        head = head.LengthSquared() > 0.25f ? Vector2.Normalize(head) : dir;
        if (mode == F2Mode.Rotate) head = dir;
        if (cont) head = prev!.Heading;   // 진행 방향은 MoveFlag 가 각속도 제한으로 새 목표 쪽으로 돌린다 (슬롯이 한 번에 돌지 않게)
        flag.Facing = DetMath.Atan2(head.Y, head.X);
        var a = new CompanyAnchor { Pos = flag.Pos, Heading = head, Goal = g.Goal, FinalDir = dir, Spacing = b.Spacing, Group = g, Flag = flag, SpeedMul = cont ? prev!.SpeedMul : 0f };
        a.Trail.Add(a.Pos);
        float hw = 0;
        foreach (var l in b.Local) hw = MathF.Max(hw, MathF.Abs(l.X));
        a.HalfWidth = hw + b.Spacing * 0.5f;

        // 슬롯 배정: 출발점(깃발) 기준 행군 슬롯에 (거리², Id, 슬롯) 탐욕 + 2-opt — 같은 로컬 슬롯을 도착에도 쓴다
        int n = units.Count;
        Vector2 right = new(-head.Y, head.X);
        var world = new Vector2[n];
        for (int i = 0; i < n; i++) world[i] = a.Pos + right * b.Local[i].X + head * b.Local[i].Y;
        var assign = new int[n];
        Vector2[]? from = null;
        if (cont) { from = new Vector2[n]; for (int i = 0; i < n; i++) from[i] = MarchSlot(prev!, units[i]); }   // 지금 슬롯 기준 (밀려난 위치가 아니라)
        Vector2 rightF = new(-dir.Y, dir.X);
        var finals = new Vector2[n];
        if (mode == F2Mode.Loose && !LooseSlots(a, b, units, cen, head, dir, finals)) mode = F2Mode.Formation;   // 선까지 전진: 슬롯 = 지금 배치 (ContactF2.cs), 길이 막히면 보통 대형
        if (mode == F2Mode.Formation && !cont && ShortShiftOk(c, units, cen, g.Goal, dir) && ShiftSlots(a, b, units, cen, head, dir, finals)) mode = F2Mode.Loose;   // 대형 완화: 짧은 이동 = 평행이동 · 즉시 출발 (FormRelax.cs)
        if (mode != F2Mode.Loose)
        {
            CompanyLayout.Match(b.Layout!, units, world, from, assign);   // 틀이 있으면 역할 · 분대 묶음마다 (CombatFormations.cs). 제자리 방향 전환 = 새 방향 슬롯 (world = 도착 자리) 에 가까운 병사부터
            for (int i = 0; i < n; i++)
            {
                var u = units[i];
                Vector2 l = b.Local[assign[i]];
                a.Slots[u.Id] = l;
                finals[i] = ShiftSlot(u, g.Goal + rightF * l.X + dir * l.Y);
            }
        }
        // 참호 근처 최종 슬롯 → 빈 참호 자리 (Trenches.cs: 참호선에 중대를 세우는 이동이 곧장 되게)
        var spots = SnapSlotsToTrenches(units, finals, order == UnitOrder.AttackMove);
        for (int i = 0; i < n; i++)
        {
            if (spots[i] >= 0) finals[i] = TrenchSpotPos(spots[i]);
            a.FinalOff[units[i].Id] = finals[i] - g.Goal;
        }
        c.Anchor = a;
        DrillOnCompanyOrder(c, order);   // 새 중대 명령 = 틀 행동 초기화, 일반 이동이면 도착까지 행동 안 함 (TacticDrill.cs)
        Stats.CompanyMoves++; Stats.SlotAssign += n;
        if (ContactOf(c) != ContactState.None) Stats.ContactCompanyMoves++;   // [측정 F1 2차] 접적 · 이탈 중대에 낸 이동 명령
        c.Facing = dir;
        c.Holes = 0;   // 새 대형 — 빈자리 없음
        foreach (var u in units)
        {
            bool keep = cont && u.ReactDelay == 0 && !u.Charging && u.Order == order;   // 같은 명령으로 이미 걷던 병사
            var tgt = keep && order == UnitOrder.AttackMove ? u.Target : null;          // 공격 이동 → 공격 이동: 쏘던 표적 유지
            ResetOrder(u);
            u.Order = order;
            u.Group = g;
            Acknowledge(u);
            { float mul = PlayerSelDelayMul(execSel); u.ReactDelay = (int)(FormReactTicks(u) * mul) + ChainDelayTicks(u, mul); }   // N34: 중대 대형 명령 = 병사마다 대형 반응 0~3틱 (기본 출발 지연 0~2틱 대신, 겹치지 않음) + 지휘 계통 지연; N37 배수는 둘 다에 한 번씩
            if (keep) { u.ReactDelay = 0; if (tgt != null) u.Target = tgt; }
        }
        ClaimSnapped(units, spots);
        if (mode == F2Mode.Rotate) RotateStay(c, a, units, finals, spots);   // 새 슬롯 가까운 병사는 제자리 (방향만), 접적 중이면 새 슬롯이 구역 중심
    }

    /// <summary>이어 걷기 판정: 새 진행 방향과 지금 진행 방향의 cos 이 이보다 커야 (≈ 45° 안).</summary>
    const float ContinueDot = 0.7f;

    // ---------------------------------------------------------------- 매 틱 (Step, UpdateUnit 전)

    void StepAnchors(float dt)
    {
        if (Companies.Count == 0) return;
        foreach (var c in Companies)
        {
            var a = c.Anchor;
            if (a == null) continue;
            a.Goal = a.Group.Goal;   // 지도 변경으로 목표가 다시 해석됐을 수 있다 (RefreshPaths)
            if (a.Column && !a.Arrived) UpdateColumnRows(a);
            int n = 0, far = 0, mid = 0, headFar = 0, engaged = 0;
            float slowest = float.MaxValue;   // 가장 느린 구성원의 실제 속도 (돌격 · 속보 · 스태미나 · 제압 반영)
            float headDepth = -ColumnHeadRows * a.Spacing - 0.01f;
            float lagSlow = MathF.Max(LagSlow, FormTolerance + 0.3f), lagWait = MathF.Max(LagWait, lagSlow + 0.7f);   // 대형 완화: 허용 오차 안 병사를 처진 병사로 세지 않음
            foreach (var u in c.Members)
            {
                if (!ReferenceEquals(u.Group, a.Group)) continue;
                n++;
                bool proneOut = u.Prone && Defs.CombatRules.ProneHoldInFormation && !CrossExempt(u);   // pronemove: 엎드려 버티거나 기어 따라오는 병사는 깃발 속도 · 낙오 판정에서 뺀다 (예전엔 끌려와 늘 제자리)
                if (!proneOut) slowest = MathF.Min(slowest, UnitSpeed(u));
                if (a.Arrived) continue;
                if (u.Target != null && (u.AimTarget != 0 || u.Charging) && !(u.IsOfficer && Defs.CombatRules.OfficerAvoid)) engaged++;   // 서서 쏘는 중 (N35: 장교 자위 사격은 빼고) · 돌격 중 (공격 이동 대형 — 대열이 함께 멈춘다)
                if (proneOut) continue;
                float lag = Vector2.Distance(u.Pos, MarchSlot(a, u));
                if (lag > lagWait) { far++; if (a.Column && a.ColumnSlots.TryGetValue(u.Id, out var cl) && cl.Y > headDepth) headFar++; }
                else if (lag > lagSlow) mid++;
            }
            if (n == 0) { c.Anchor = null; continue; }
            if (a.Arrived) continue;
            if ((Tick + c.Id) % NarrowCheckInterval == 0) CheckNarrow(c, a);
            float mul;
            if (a.Column) mul = headFar > 0 ? 0f : far > n / 4 ? 0.5f : 1f;   // 종대: 선두 몇 줄만 기다리고, 꼬리가 많이 처지면 절반 속도
            else
            {
                // 횡대 등: 3분의 1 넘게 처지면 정지 (방향 전환 · 정렬을 기다림), 10% 넘게 처지면 절반 속도
                int allow = n / 10;
                mul = far > n / 3 ? 0f : far + mid > allow ? 0.5f : 1f;
            }
            bool hold = Defs.CombatRules.ChokeEnabled && StepChokeQueue(c, a);   // 길목 앞에서 앞 중대가 빠지기를 기다림 (Choke.cs)
            bool fight = engaged > n / 4;   // 공격 이동: 구성원 4분의 1 넘게 교전 중 → 대열 전체가 서서 싸운다 (기다림 상한 없음)
            if (fight) { mul = 0f; a.WaitTicks = 0; }
            else if (hold) { mul = MathF.Min(mul, KdHoldMul); a.WaitTicks = 0; }
            else if (mul == 0) { if (++a.WaitTicks > MaxWaitTicks) mul = 0.5f; }
            else a.WaitTicks = 0;
            // 깃발 속도 배수는 목표값 (0 · 0.5 · 1) 으로 바로 뛰지 않고 가속 제한으로 따라간다 (서고 가기 깜빡임 방지, (a)-4 · (b)-3)
            float sm = a.SpeedMul, step = (mul < sm ? FlagDecelPerSec : FlagAccelPerSec) * dt;
            a.SpeedMul = mul < sm ? MathF.Max(mul, sm - step) : MathF.Min(mul, sm + step);
            if (!Stats.lastFlagMul.TryGetValue(c.Id, out float lm) || lm != mul) { if (Stats.lastFlagMul.ContainsKey(c.Id)) Stats.FlagMulChange++; Stats.lastFlagMul[c.Id] = mul; }
            if (a.SpeedMul > 0) MoveFlag(c, a, dt, a.SpeedMul * (slowest < float.MaxValue ? slowest / MathF.Max(0.01f, UnitSpeed(a.Flag)) : 1f));
        }
    }

    static readonly float KdHoldMul = Environment.GetEnvironmentVariable("KD_CQ_HOLDMUL") is { } hm ? float.Parse(hm, System.Globalization.CultureInfo.InvariantCulture) : 0f;
    void MoveFlag(Company c, CompanyAnchor a, float dt, float mul)
    {
        var f = a.Flag;
        Vector2 before = f.Pos;
        float spd = UnitSpeed(f) * FlagSpeedFrac * mul;
        float dist = Vector2.Distance(f.Pos, a.Goal);
        bool ok = dist <= MathF.Max(0.3f, spd * dt) || MoveToward(f, a.Goal, a.Goal, false, dt, FlagSpeedFrac * mul);
        ResolveTerrain(f);
        a.Pos = f.Pos;
        AppendTrail(a);
        if (!ok || f.StuckLevel >= 4 || Vector2.Distance(a.Pos, a.Goal) <= MathF.Max(0.3f, spd * dt))
        {
            a.Arrived = true;   // 목표 도착 (또는 길 없음 · 깃발 정체) → 구성원은 각자 최종 슬롯으로
            f.Velocity = Vector2.Zero;
            return;
        }
        // 진행 방향: 실제 이동 방향 쪽으로, 대형 끝이 따라올 수 있는 각속도 이내 (끝 병사 속도 ≈ 각속도 × 반폭)
        Vector2 v = a.Pos - before;
        if (v.LengthSquared() < 1e-8f) return;
        Vector2 want = Vector2.Normalize(v);
        float maxTurn = UnitSpeed(f) * 0.8f / MathF.Max(1f, a.HalfWidth) * dt;
        a.Heading = RotateToward(a.Heading, want, maxTurn);
    }

    /// <summary>단위 벡터 from 을 to 쪽으로 최대 maxAngle 라디안 회전 (DetMath — 결정적).</summary>
    internal static Vector2 RotateToward(Vector2 from, Vector2 to, float maxAngle)
    {
        float cross = from.X * to.Y - from.Y * to.X, dot = Vector2.Dot(from, to);
        float ang = DetMath.Atan2(cross, dot);
        if (MathF.Abs(ang) <= maxAngle) return to;
        float t = MathF.Sign(ang) * maxAngle;
        float cs = DetMath.Cos(t), sn = DetMath.Sin(t);
        var r = new Vector2(from.X * cs - from.Y * sn, from.X * sn + from.Y * cs);
        return r.LengthSquared() > 1e-8f ? Vector2.Normalize(r) : to;
    }

    /// <summary>
    /// 좁은 길 판정 (bunchfix 2026-10-19): 깃발 좌우 폭 선 (지금 · 3칸 앞 · 8칸 앞) 중 하나라도 막힌 비율이 narrowBlockFrac (balance.json, 25%) 이상이면 2열 종대,
    /// 셋 다 그 아래로 narrowClearTicks (120틱) 연속이면 원래 대형. 작은 장애물 (집 한 채 · 나무) 은 종대로 바꾸지 않는다 — 그 슬롯 병사만 비켜 선다 (MarchSlot).
    /// 막힌 비율 = 폭 선 위 1칸 간격 표본 중 깃발이 설 수 없는 칸 · 지도 밖 표본의 비율.
    /// </summary>
    internal void CheckNarrow(Company c, CompanyAnchor a)
    {
        if (c.Members.Count < 3) return;
        float t = Defs.CombatRules.NarrowBlockFrac;
        float worst = MathF.Max(NarrowBlockedFrac(a, a.Pos), MathF.Max(NarrowBlockedFrac(a, a.Pos + a.Heading * 3f), NarrowBlockedFrac(a, a.Pos + a.Heading * 8f)));
        float exit = Relax ? MathF.Min(t, Defs.CombatRules.NarrowExitFrac) : t;   // 대형 완화: 복귀 문턱을 진입보다 낮게 (히스테리시스)
        if (worst >= t)
        {
            a.ClearTicks = 0;
            if (!a.Column) EnterColumn(c, a);
        }
        else if (a.Column && worst >= exit) a.ClearTicks = 0;   // 아직 덜 트임 — 종대 유지
        else if (a.Column && Defs.CombatRules.ChokeEnabled && Environment.GetEnvironmentVariable("KD_CQ_NOCOL") == null && CompanyInChoke(c)) a.ClearTicks = 0;   // choke: 꼬리가 길목을 다 빠질 때까지 종대 유지 (대형 재배정 없음)
        else if (a.Column && (a.ClearTicks += NarrowCheckInterval) >= Defs.CombatRules.NarrowClearTicks)
        {
            a.Column = false;
            a.ClearTicks = 0;
            Stats.ColumnExits++;
        }
    }

    /// <summary>p 를 지나는 깃발 폭 선 (진행 방향에 수직, ±HalfWidth) 의 막힌 비율 0~1 (1칸 간격 표본, 지도 밖 = 막힘).</summary>
    internal float NarrowBlockedFrac(CompanyAnchor a, Vector2 p)
    {
        Vector2 right = new(-a.Heading.Y, a.Heading.X);
        int n = Math.Max(2, (int)MathF.Ceiling(a.HalfWidth * 2f) + 1);
        int bad = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 q = p + right * (-a.HalfWidth + 2f * a.HalfWidth * i / (n - 1));
            if (q.X < 0.5f || q.Y < 0.5f || q.X > Map.Width - 0.5f || q.Y > Map.Height - 0.5f) { bad++; continue; }
            var (x, y) = GameMap.TileOf(q);
            if (!SlotOk(a.Flag, x, y)) bad++;
        }
        return bad / (float)n;
    }

    Vector2 ClampToMap(Vector2 p) => new(Math.Clamp(p.X, 0.5f, Map.Width - 0.5f), Math.Clamp(p.Y, 0.5f, Map.Height - 0.5f));

    /// <summary>종대에서 깃발이 기다려 주는 선두 줄 수 (그 뒤는 처져도 따라온다).</summary>
    const int ColumnHeadRows = 3;
    const float TrailStep = 0.5f;

    /// <summary>
    /// 2열 종대 슬롯: 앞에 있는 병사부터 (깃발 = 선두), 같은 줄은 왼쪽부터. 로컬 값 X = 좌우 (±간격/2), Y = −(줄 × 간격) = 깃발 자취를 따라 뒤로 잰 거리.
    /// 종대 병사는 깃발이 지나간 자취(Trail) 위에 줄지어 선다 — 굽은 길에서도 벽을 가로지르지 않는다.
    /// </summary>
    internal void EnterColumn(Company c, CompanyAnchor a)
    {
        a.Column = true;
        Stats.ColumnEnter++;
        a.ColumnSlots.Clear();
        Vector2 right = new(-a.Heading.Y, a.Heading.X);
        // 깃발에 가까운 병사부터 앞줄 (가운데가 먼저 들어가고 양 끝은 뒤로 붙는다), 같은 거리면 왼쪽부터
        var followers = c.Members.Where(u => ReferenceEquals(u.Group, a.Group))
            .OrderBy(u => MathF.Round(Vector2.Distance(u.Pos, a.Pos) * 2f))
            .ThenBy(u => Vector2.Dot(u.Pos - a.Pos, right)).ThenBy(u => u.Id).ToList();
        float s = a.Spacing;
        for (int i = 0; i < followers.Count; i++)
            a.ColumnSlots[followers[i].Id] = new Vector2(((i & 1) - 0.5f) * s, -(i >> 1) * s);
        UpdateColumnRows(a);
    }

    /// <summary>깃발 자취 기록 (TrailStep 간격, 종대 길이만큼만 보관 — 오래된 쪽부터 버림).</summary>
    static void AppendTrail(CompanyAnchor a)
    {
        var t = a.Trail;
        if (t.Count == 0 || Vector2.DistanceSquared(t[^1], a.Pos) >= TrailStep * TrailStep) t.Add(a.Pos);
        float need = ((a.Slots.Count + 1) / 2 + 2) * a.Spacing + 2f;
        int keep = (int)MathF.Ceiling(need / TrailStep) + 4;
        if (t.Count > keep * 2) t.RemoveRange(0, t.Count - keep);   // 가끔 한꺼번에 (매 틱 앞에서 지우지 않게)
    }

    /// <summary>줄마다 자취 위 위치 · 접선 (이번 틱 작업값 — 깃발 위치 + 자취에서 다시 계산, 저장 안 함).</summary>
    internal void UpdateColumnRows(CompanyAnchor a)
    {
        int rows = (a.Slots.Count + 1) / 2 + 1;
        if (a.RowPos.Length < rows) { a.RowPos = new Vector2[rows]; a.RowTan = new Vector2[rows]; }
        var t = a.Trail;
        float s = a.Spacing;
        Vector2 cur = a.Pos, tan = a.Heading;
        int k = t.Count - 1;      // 다음으로 볼 자취 점 (최신 → 오래된)
        float walked = 0;
        int r0 = 0;               // 자취로 채운 줄 수
        for (; r0 < rows; r0++)
        {
            float want = r0 * s;
            while (walked < want && k >= 0)
            {
                Vector2 seg = cur - t[k];
                float len = seg.Length();
                if (len < 1e-5f) { cur = t[k]; k--; continue; }
                if (walked + len >= want)
                {
                    float f = (want - walked) / len;
                    tan = seg / len;
                    cur -= seg * f;
                    walked = want;
                    break;
                }
                walked += len;
                tan = seg / len;
                cur = t[k];
                k--;
            }
            if (walked < want - 1e-4f) break;   // 자취가 모자람 → 아래에서 외삽
            a.RowPos[r0] = cur;
            a.RowTan[r0] = tan;
        }
        a.RowReach = rows;
        if (r0 >= rows) return;
        // bunchfix (2026-10-19, docs/BUNCHING_DIAG_2026-10-19.md 1순위): 자취가 모자란 줄은 가장 오래된 점 한 곳에 쌓지 않고
        // 마지막 자취 점에서 마지막 접선 반대쪽 (-tan) 으로 (r × 간격 − 걸은 길이) 만큼 연장한 점에 세운다.
        // 연장선이 막힌 칸 · 지도 밖에 닿으면 그 앞까지만 — 나머지 줄은 RowReach 로 표시하고 MarchSlot 이 마지막 닿는 점 둘레로 흩어 세운다.
        float reach = 0, stepLen = 0.5f, maxNeed = (rows - 1) * s - walked;
        while (reach + stepLen <= maxNeed + 1e-4f)
        {
            Vector2 q = cur - tan * (reach + stepLen);
            if (q.X < 0.5f || q.Y < 0.5f || q.X > Map.Width - 0.5f || q.Y > Map.Height - 0.5f) break;
            var (qx, qy) = GameMap.TileOf(q);
            if (!SlotOk(a.Flag, qx, qy)) break;
            reach += stepLen;
        }
        for (int r = r0; r < rows; r++)
        {
            float ext = r * s - walked;
            if (ext > reach + 1e-4f) { a.RowReach = r; ext = reach; }
            a.RowPos[r] = cur - tan * ext;
            a.RowTan[r] = tan;
            if (a.RowReach < rows) { for (int q = r + 1; q < rows; q++) { a.RowPos[q] = a.RowPos[r]; a.RowTan[q] = tan; } break; }
        }
    }

    /// <summary>bunchfix: 종대 꼬리 넘침 칸 idx 의 오프셋 — 좌우 번갈아 바깥 열 (±1.5 · 2.5 · 3.5 간격), 앞으로 (fwd) 간격씩, 바깥 열마다 반 간격 어긋남. flip = 좌우 바꿈.</summary>
    static Vector2 ColumnOverflowOffset(int idx, Vector2 fwd, Vector2 right, float s, bool flip)
    {
        int side = (idx & 1) == 0 ^ flip ? -1 : 1, k = idx >> 1, lane = k % 3, depth = k / 3;
        return right * (side * (1.5f + lane) * s) + fwd * (depth * s + (lane & 1) * s * 0.5f);
    }

    /// <summary>bunchfix: 결정적 흩뿌림 오프셋 — 칸 idx (0, 1, 2, …) 를 5열 격자 (옆 · 뒤 간격 × 0.5, 홀수 줄은 반 칸 어긋남) 에 놓는다.</summary>
    static Vector2 SpreadOffset(int idx, Vector2 back, Vector2 right, float step)
    {
        int lane = idx % 5, depth = idx / 5;
        float x = (lane - 2) * step * 2f + ((depth & 1) != 0 ? step : 0f);
        return right * x + back * (depth * step);
    }

    /// <summary>행군 중 이 구성원의 슬롯 (세계 좌표). 설 수 없는 칸이면 가까운 설 수 있는 칸에 흩어 (BlockedSlotFallback, 종대는 자취 위 점) — 개별 길찾기 → 지나간 뒤 재정렬.</summary>
    internal Vector2 MarchSlot(CompanyAnchor a, Unit u)
    {
        if (a.Column)
        {
            if (!a.ColumnSlots.TryGetValue(u.Id, out var cl)) return a.Pos;
            // 줄 수는 지금 구성원 기준 (RowPos 배열은 커지기만 해서 옛 줄 값이 남아 있을 수 있다 — 배열 길이로 자르면 불러온 월드와 어긋남)
            int rows = Math.Min((a.Slots.Count + 1) / 2 + 1, a.RowPos.Length);
            if (rows == 0) return a.Pos;
            int row = Math.Clamp((int)MathF.Round(-cl.Y / a.Spacing), 0, rows - 1);
            Vector2 rp = a.RowPos[row], rt = a.RowTan[row];
            Vector2 rr = new(-rt.Y, rt.X);
            if (row >= a.RowReach)
            {
                // bunchfix: 자취 · 외삽이 닿지 않는 줄 = 마지막 닿는 점에서 종대 꼬리 양옆 바깥 칸 (좌우 번갈아, 옆 3칸 × 앞으로 간격, 칸마다 반 간격 어긋남)
                // — 종대 슬롯 순번 (줄 · 좌우, 병사 Id 로 정렬된 배정) 마다 다른 자리 (결정적). 막혔으면 반대편 · 가까운 설 수 있는 칸.
                int idx = (row - a.RowReach) * 2 + (cl.X > 0 ? 1 : 0);
                Vector2 c1 = rp + ColumnOverflowOffset(idx, rt, rr, a.Spacing, false);
                if (InMapOk(u, c1)) return c1;
                Vector2 c2 = rp + ColumnOverflowOffset(idx, rt, rr, a.Spacing, true);
                if (InMapOk(u, c2)) return c2;
                return ShiftSlot(u, c1);
            }
            Vector2 q = rp + rr * cl.X;
            return InMapOk(u, q) ? q : rp;
        }
        if (!a.Slots.TryGetValue(u.Id, out var l)) return a.Pos;
        Vector2 p = a.Pos + new Vector2(-a.Heading.Y, a.Heading.X) * l.X + a.Heading * l.Y;
        p = ClampToMap(p);   // 지도 밖 슬롯 = 가장자리로 (깃발 한 점으로 모이지 않게, (c)-3)
        var (tx, ty) = GameMap.TileOf(p);
        if (SlotOk(u, tx, ty)) return p;
        return BlockedSlotFallback(a, u, p);
    }

    bool InMapOk(Unit u, Vector2 q)
    {
        if (q.X < 0.5f || q.Y < 0.5f || q.X > Map.Width - 0.5f || q.Y > Map.Height - 0.5f) return false;
        var (x, y) = GameMap.TileOf(q);
        return SlotOk(u, x, y);
    }

    /// <summary>
    /// bunchfix (진단 4순위): 행군 슬롯이 설 수 없는 칸이면 깃발 한 점 대신 가장 가까운 설 수 있는 칸 (ShiftSlot) + 병사 Id 해시로 고른
    /// 흩뿌림 칸 (옆 · 뒤 간격 × 0.5 격자 15칸) — 같은 막힌 곳에 걸린 병사들이 한 타일로 모이지 않게. 흩뿌린 칸이 막혔으면 ShiftSlot 칸, 그것도 없으면 깃발.
    /// </summary>
    Vector2 BlockedSlotFallback(CompanyAnchor a, Unit u, Vector2 p)
    {
        Vector2 b = ShiftSlot(u, p);
        if (!InMapOk(u, b)) return a.Pos;
        uint h = (uint)u.Id * 2654435761u;
        int idx = (int)((h >> 16) % 15u);
        Vector2 right = new(-a.Heading.Y, a.Heading.X);
        Vector2 q = b + SpreadOffset(idx, -a.Heading, right, a.Spacing * 0.5f) + a.Heading * a.Spacing * 0.5f;   // 격자 가운데가 b 근처로
        return InMapOk(u, q) ? q : b;
    }

    /// <summary>N12: 행군 중 작은 슬롯 오차를 줄이는 비율 (초당, 3 = 오차가 약 0.33초에 63% 줄어듦 — 밀림을 곧 흡수).</summary>
    const float SmallErrorGain = 3f;

    /// <summary>깃발 그룹 구성원의 이동 (FollowGroup 에서). 도착하면 true.</summary>
    bool FollowAnchor(Unit u, CompanyAnchor a, float dt)
    {
        if (a.Arrived)
        {
            Vector2 slot = a.Goal + (a.FinalOff.TryGetValue(u.Id, out var fo) ? fo : Vector2.Zero);
            float sd = Vector2.Distance(slot, u.Pos);
            float spd = UnitSpeed(u);
            float slack = u.HasTrenchSpot ? 0.3f : MathF.Max(0.3f, Defs.CombatRules.FormArriveSlack);   // N12: 도착 여유 반경 (참호 자리로 가는 병사는 옛 0.3 — 들어가야 하므로)
            if (sd <= slack || sd <= spd * dt || (sd < 3f && u.StuckLevel >= 2))
            {
                if (sd <= spd * dt) u.Pos = slot;
                if (u.Def.TurnRate <= 0) u.Facing = DetMath.Atan2(a.FinalDir.Y, a.FinalDir.X);   // 느리게 도는 견인포는 지금 방향 유지 (순간 회전 없음)
                u.StuckLevel = 0;
                return true;
            }
            if (ArriveInZone(u, slot, sd, a.FinalDir)) return true;   // F1: 접적 중 교전하는 병사는 구역 안에서 도착 (Contact.cs)
            if (ArriveRelaxed(u, slot, sd, slack, a.FinalDir))
            {
                if (u.Def.TurnRate <= 0) u.Facing = DetMath.Atan2(a.FinalDir.Y, a.FinalDir.X);
                u.StuckLevel = 0;
                return true;   // 대형 완화: 구역 도착 (FormRelax.cs)
            }
            if (u.LastMoveTick < Tick - 1) Stats.SlotReturns++;   // [측정 F1] 서 있다가 도착 깃발 슬롯으로 다시 걷기 시작
            return !MoveToward(u, slot, slot, false, dt, 1f, sd < FaceSlotDist ? a.FinalDir : default);
        }
        Vector2 ms = MarchSlot(a, u);
        float lag0 = Vector2.Distance(ms, u.Pos);
        if (a.SpeedMul > 0 && lag0 > 1e-5f && lag0 <= UnitSpeed(u) * dt && u.NudgeUntil <= Tick && !u.Large)
        {
            // 슬롯에 붙어 걷는 중: 이번 틱 슬롯 위치로 그대로 (깃발이 천천히 움직여도 걷기 ↔ 서기가 깜빡이지 않게, (b)-3)
            u.Velocity = (ms - u.Pos) / dt;
            u.Pos = ms;
            u.Facing = TurnToward(u.Facing, DetMath.Atan2(a.Heading.Y, a.Heading.X), u.Def.TurnRadPerSec * dt);
            u.ProgPos = u.Pos; u.ProgTick = Tick; u.ProgLast = Tick;   // 정체 판정 기준도 함께 (슬롯을 따라가는 중 = 정체 아님)
            return false;
        }
        float tol = FormTolerance;
        if (a.SpeedMul >= 0.99f && lag0 <= tol && u.NudgeUntil <= Tick && !u.Large && u.Def.TurnRate <= 0 && !(a.Column && a.ColumnSlots.ContainsKey(u.Id)))
        {
            // N12: 행군 중 작은 슬롯 오차 (formMoveTolerance 안) 는 고치려 걷지 않는다 — 깃발과 나란히 같은 걸음으로 가고 오차는 서서히만 줄인다 (초당 SmallErrorGain 비율)
            Vector2 step = a.Flag.Velocity * dt + MarchCorrection(ms - u.Pos, dt, SmallErrorGain);   // 대형 완화: 데드밴드 안 오차는 고치지 않음
            float along = Vector2.Dot(step, a.Heading);
            if (along < 0) step -= a.Heading * along;   // 행군 중에는 뒤로 걷지 않는다 (앞선 병사는 늦춰서 맞춤 — 앞뒤 떨림 방지)
            float maxStep = UnitSpeed(u) * dt;
            if (step.LengthSquared() > maxStep * maxStep) step = Vector2.Normalize(step) * maxStep;
            var np = u.Pos + step;
            var (nx, ny) = GameMap.TileOf(np);
            if (step.LengthSquared() > 1e-10f && SlotOk(u, nx, ny))
            {
                u.Velocity = step / dt;
                u.Pos = np;
                u.Facing = TurnToward(u.Facing, DetMath.Atan2(a.Heading.Y, a.Heading.X), u.Def.TurnRadPerSec * dt);
                u.ProgPos = u.Pos; u.ProgTick = Tick; u.ProgLast = Tick;
                return false;
            }
        }
        if (lag0 <= 0.01f || StandInZone(u, a, lag0, tol))
        {
            // 슬롯에 서 있음 (깃발이 기다리는 중): 진행 방향을 본다. 이동 판정(CheckProgress)은 건너뛴다 — 정체 오판 방지
            u.Facing = TurnToward(u.Facing, DetMath.Atan2(a.Heading.Y, a.Heading.X), u.Def.TurnRadPerSec * dt);
            return false;
        }
        // 조향 = 자기 슬롯 (직선이 트이면 곧장). 막혔을 때의 경로 목표 = 중대 목표 (깃발과 같은 길 · 같은 경로 캐시 — 깃발이 움직여도 경로를 다시 구하지 않는다)
        float lag = Vector2.Distance(ms, u.Pos);
        Vector2 hd = a.Column && a.ColumnSlots.ContainsKey(u.Id) ? default : a.Heading;   // 종대는 자취 접선이 줄마다 달라 그대로 (조향 방향)
        return !MoveToward(u, a.Goal, ms, false, dt, 1f, lag < FaceSlotDist ? hd : default);
    }

    // ---------------------------------------------------------------- 미리보기 (UI — 순수 함수, 월드 상태를 바꾸지 않음)

    /// <summary>
    /// 이동 명령을 이 지점 · 이 대형 지정으로 내렸을 때의 도착 슬롯 (세계 좌표). 중대 소속이면 깃발 배치, 아니면 옛 진형.
    /// 설 수 없는 칸 보정(ShiftSlot)은 하지 않는다 (길찾기 캐시를 건드리지 않게) — 표시용 근사.
    /// </summary>
    public List<Vector2> PreviewSlots(IReadOnlyList<Unit> units, Vector2 target, FormSpec? form)
    {
        var res = new List<Vector2>();
        var comp = new Dictionary<int, List<Unit>>();
        var loose = new List<Unit>();
        foreach (var u in units)
        {
            if (u.Company is not { } c) { loose.Add(u); continue; }
            if (!comp.TryGetValue(c.Id, out var l)) comp[c.Id] = l = new List<Unit>();
            l.Add(u);
        }
        foreach (var c in Companies)
            if (comp.TryGetValue(c.Id, out var part) && part.Count < c.Members.Count) { loose.AddRange(part); comp.Remove(c.Id); }   // 부분 선택 = 임시 묶음 (옛 진형)
        if (comp.Count > 0)
        {
            var blocks = new List<OrgLayout.Block>();
            foreach (var c in Companies) if (comp.TryGetValue(c.Id, out var l)) blocks.Add(new OrgLayout.Block(c, l, c.Form));
            var plan = OrgLayout.Plan(blocks, target, form, KeepFacing(blocks), BatFormOf(blocks), FormRules, TacRules, Defs.CombatRules);
            Vector2 right = new(-plan.Dir.Y, plan.Dir.X);
            foreach (var b in plan.Blocks)
                foreach (var l in b.Local) res.Add(b.Center + right * l.X + plan.Dir * l.Y);
        }
        var lform = LooseForm(loose, form, out var lloc);   // 분대만 따로 = 분대 대형, 사선은 기병만
        if (loose.Count >= 2 && lform != null && (!lform.IsFree || lform.Width > 0))
        {
            // 옛 진형 (Formations.Build 와 같은 방향 · 슬롯, 배정은 생략)
            var kind = lform.IsFree ? FormType.Line : lform.Kind;
            Vector2 cen = Vector2.Zero;
            foreach (var u in loose) cen += u.Pos;
            cen /= loose.Count;
            Vector2 dir = lform.Dir.LengthSquared() > 1e-6f ? Vector2.Normalize(lform.Dir) : OrgLayout.Snap8(target - cen);
            Vector2 right = new(-dir.Y, dir.X);
            foreach (var l in lloc ?? Formations.Slots(kind, loose.Count, OrgLayout.SpacingFor(loose), lform.Width)) res.Add(target + right * l.X + dir * l.Y);
        }
        return res;
    }
}

/// <summary>중대 블록 · 대대 배치 계산 (순수 로직, 시뮬레이션과 미리보기가 같이 쓴다).</summary>
internal static class OrgLayout
{
    public const float BlockGap = 2f;

    public sealed class Block
    {
        public readonly Company? Company;
        public readonly List<Unit> Units;
        public readonly FormType Form;
        public float Spacing;
        public Vector2[] Local = Array.Empty<Vector2>();   // 로컬 슬롯 (X 오른쪽, Y 앞, 평균 0)
        public CompanyLayout.Result? Layout;               // 슬롯 묶음 (전술 틀 — 역할 · 분대별 배정, CombatFormations.cs)
        public Vector2 Center;                             // 도착 블록 중심 (세계)
        public float MinX, MaxX, MinY, MaxY;
        public Block(Company? c, List<Unit> units, FormType form) { Company = c; Units = units; Form = form; }
        public Vector2 Centroid { get { Vector2 s = Vector2.Zero; foreach (var u in Units) s += u.Pos; return s / Math.Max(1, Units.Count); } }
    }

    public sealed record Result(Vector2 Dir, List<Block> Blocks);

    /// <summary>슬롯 간격: 가장 큰 구성원 지름 + 여유 (옛 진형과 같음).</summary>
    public static float SpacingFor(IReadOnlyList<Unit> units)
    {
        float maxR = 0.3f;
        foreach (var u in units) maxR = MathF.Max(maxR, u.Radius);
        return 2 * maxR + 0.35f;
    }

    /// <summary>8방향 중 d 와 가장 가까운 단위 벡터 (사선 대형 방지 — 대각선은 45° 정확히). d = 0 이면 북쪽.</summary>
    public static Vector2 Snap8(Vector2 d)
    {
        if (d.LengthSquared() < 1e-8f) return new Vector2(0, -1);
        const float h = 0.70710677f;
        Span<Vector2> dirs = stackalloc Vector2[8] { new(0, -1), new(h, -h), new(1, 0), new(h, h), new(0, 1), new(-h, h), new(-1, 0), new(-h, -h) };
        int best = 0;
        float bd = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            float v = Vector2.Dot(dirs[i], d);
            if (v > bd + 1e-6f) { bd = v; best = i; }
        }
        return dirs[best];
    }

    /// <summary>
    /// 중대 블록 슬롯 (로컬, 평균 0). width &gt; 0 = 우클릭 드래그 폭 (열 수 = 폭 / 간격 + 1).
    /// 폭 없음: 횡대 = 2열 (넓게), 종대 = 4열, 방진 · 자유 = 정사각, 산개 = 정사각 × 넓은 간격, 쐐기 = 옛 쐐기.
    /// </summary>
    public static Vector2[] CompanySlots(FormType form, int n, float s, float width, FormationRules? rules = null)
    {
        // 모양은 formations.json "company" (기본값 = 옛 식: 횡대 2열 · 종대 4열 · 방진/자유 정사각 · 산개 ×2.2 · 쐐기)
        var k = form == FormType.Free ? FormType.Box : form;
        var fr = rules ?? FormationRules.Default;
        return FormationRules.SlotsFor(fr.CompanyShape(k), k, n, s, width);
    }

    /// <summary>
    /// 방향 · 블록 슬롯 · 블록 중심 계산.
    /// 방향 = form.Dir (우클릭 드래그) → 없으면 keepFacing (모든 중대 같은 방향 + 목표가 KeepFacingRange 안) → 아니면 무게중심→목표 8방향 스냅.
    /// 블록 순서 = 지금 좌우(종대는 앞뒤) 위치 순 → 서로 가로지르지 않는다.
    /// </summary>
    public static Result Plan(List<Block> blocks, Vector2 target, FormSpec? form, Vector2 keepFacing, BatForm reg, FormationRules? rules = null, TacticRules? tactics = null, CombatRulesDef? combat = null)
    {
        var fr = rules ?? FormationRules.Default;
        var tr = tactics ?? TacticRules.Default;
        Vector2 cen = Vector2.Zero;
        int total = 0;
        foreach (var b in blocks) foreach (var u in b.Units) { cen += u.Pos; total++; }
        cen /= Math.Max(1, total);
        Vector2 dir;
        if (form != null && form.Dir.LengthSquared() > 1e-6f) dir = Vector2.Normalize(form.Dir);
        else if (keepFacing != Vector2.Zero && Vector2.Distance(cen, target) <= World.KeepFacingRange) dir = keepFacing;
        else dir = Snap8(target - cen);
        Vector2 right = new(-dir.Y, dir.X);
        float width = form?.Width ?? 0;
        int k = blocks.Count;
        if (k == 0) return new Result(dir, blocks);

        // 줄 배치 (2선 + 예비: 앞줄 = 앞쪽에 있는 중대 2/3)
        var order = blocks.OrderBy(b => MathF.Round(Vector2.Dot(b.Centroid - cen, right) * 4f)).ThenBy(b => b.Company?.Id ?? 0).ToList();
        List<List<Block>> rows;
        if (reg == BatForm.Column)
            rows = blocks.OrderByDescending(b => MathF.Round(Vector2.Dot(b.Centroid - cen, dir) * 4f)).ThenBy(b => b.Company?.Id ?? 0).Select(b => new List<Block> { b }).ToList();
        else if (reg == BatForm.TwoFrontReserve && k >= 2)
        {
            int front = Math.Max(1, (k * 2 + 2) / 3);
            var byDepth = blocks.OrderByDescending(b => MathF.Round(Vector2.Dot(b.Centroid - cen, dir) * 4f)).ThenBy(b => b.Company?.Id ?? 0).ToList();
            var f = byDepth.Take(front).ToHashSet();
            rows = new List<List<Block>> { order.Where(f.Contains).ToList(), order.Where(b => !f.Contains(b)).ToList() };
        }
        else if (reg == BatForm.Waves && k >= 2)
        {
            // 파상 대형: 앞에 있는 중대부터 줄 (물결) 에 나눠 담고, 줄 안은 좌우 순서
            var wv = fr.Waves;
            int nr = Math.Clamp(k >= wv.ThreeRowsAt ? 3 : wv.Rows, 1, k);
            int per = (k + nr - 1) / nr;
            var byDepth = blocks.OrderByDescending(b => MathF.Round(Vector2.Dot(b.Centroid - cen, dir) * 4f)).ThenBy(b => b.Company?.Id ?? 0).ToList();
            rows = new List<List<Block>>();
            for (int i = 0; i < k; i += per)
            {
                var set = byDepth.Skip(i).Take(per).ToHashSet();
                rows.Add(order.Where(set.Contains).ToList());
            }
        }
        else rows = new List<List<Block>> { order };

        int perRow = rows.Max(r => r.Count);
        float wc = width > 0 ? MathF.Max(0.1f, (width - BlockGap * (perRow - 1)) / perRow) : 0;
        foreach (var b in blocks)
        {
            b.Spacing = SpacingFor(b.Units);
            b.Layout = CompanyLayout.Build(b.Company, b.Units, b.Form, b.Spacing, wc, fr, tr, combat: combat);   // 틀 없음 = 옛 중대 대형 슬롯 (장교는 대열 뒤, N12)
            b.Local = b.Layout.Local;
            b.MinX = b.MinY = float.MaxValue; b.MaxX = b.MaxY = float.MinValue;
            foreach (var l in b.Local)
            {
                b.MinX = MathF.Min(b.MinX, l.X); b.MaxX = MathF.Max(b.MaxX, l.X);
                b.MinY = MathF.Min(b.MinY, l.Y); b.MaxY = MathF.Max(b.MaxY, l.Y);
            }
        }

        // 로컬 블록 중심: 줄마다 왼쪽부터 가장자리 간격 BlockGap, 앞 가장자리를 줄 앞선에 맞춤
        var local = new Dictionary<Block, Vector2>();
        float rowFront = 0;
        for (int ri = 0; ri < rows.Count; ri++)
        {
            var row = rows[ri];
            float rowW = row.Sum(b => b.MaxX - b.MinX) + BlockGap * (row.Count - 1);
            float x = -rowW / 2, depth = 0;
            for (int i = 0; i < row.Count; i++)
            {
                var b = row[i];
                float y = rowFront - b.MaxY;
                if (reg == BatForm.Echelon) y -= i * ((b.MaxY - b.MinY) * 0.5f + BlockGap);   // 사다리: 오른쪽으로 갈수록 뒤로
                local[b] = new Vector2(x - b.MinX, y);
                x += b.MaxX - b.MinX + BlockGap;
                depth = MathF.Max(depth, b.MaxY - b.MinY);
            }
            rowFront -= depth + (reg == BatForm.Waves ? fr.Waves.DepthGap : BlockGap * (reg == BatForm.TwoFrontReserve ? 2 : 1));
        }
        // 전체 경계 상자 중심을 목표에
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        foreach (var b in blocks)
        {
            var c = local[b];
            x0 = MathF.Min(x0, c.X + b.MinX); x1 = MathF.Max(x1, c.X + b.MaxX);
            y0 = MathF.Min(y0, c.Y + b.MinY); y1 = MathF.Max(y1, c.Y + b.MaxY);
        }
        Vector2 mid = new((x0 + x1) / 2, (y0 + y1) / 2);
        foreach (var b in blocks)
        {
            var c = local[b] - mid;
            b.Center = target + right * c.X + dir * c.Y;
        }
        return new Result(dir, blocks);
    }
}
