using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>정찰병의 자율 행동 단계. With = 중대와 함께 (따라다님) · Advance = 관측 지점으로 가는 중 · Observe = 지점에서 관측 중 (쌍안경) · Return = 장교에게 돌아오는 중
/// · Cover = 노려져서 엄폐 · 사격자 사선 밖으로 물러남 (N22, 조용해지면 다시 관측).</summary>
public enum ScoutState : byte { With = 0, Advance = 1, Observe = 2, Return = 3, Cover = 4 }

/// <summary>
/// 정찰병 (ROADMAP '정찰병 유닛', 2026-10-19). 중대 소속 정찰조 (<see cref="Company.Scouts"/>, 중대당 scoutAi.perCompany 명, 분대 밖 — Company.Members 에는 없다).
/// 시뮬 안의 유닛 단위 자율 행동이라 사람 · AI 가 똑같이 받는다 (명령이 아니므로 AI 는 World 를 건드리지 않는다).
///  · 중대가 움직이거나 (깃발 행군 · 구성원 이동) 싸우면 진행 방향 aheadMin~aheadMax 칸 앞 (정찰조 안 가로로 spread 간격) 의 관측 지점 (고지 · 숲 가장자리 우선) 으로 나가 선다 → 서면 Hold + 쌍안경 동작.
///  · 장교에게 돌아온다: 제압 &gt;= returnSuppression · HP &lt;= returnHpFrac · 앞에 나와 적을 처음 본 뒤 reportSec 초 · 중대 중심에서 maxDist 칸 초과 중 하나라도. 돌아온 뒤 cooldownSec 초 · 제압 resumeSuppression 미만 · HP 회복 불가 → HP 낮으면 다시 안 나감.
///  · 피격 엄폐 (N22): 노려지면 (Unit.LastFiredAtTick — 명중 · 빗나감 · 폭발 · 흩뿌림) 곧바로 coverRadius 칸 안의 엄폐 · 사격자 사선 밖 칸 (Cover), coverCalmSec 초 조용하면 다시 관측.
///    중대 정찰조 (자율) 와 관측반 (정찰병만의 중대 — AI 가 부리는 관측자) 모두. 제압 복귀와 별개 (제압은 거의 쌓이지 않아 복귀가 드물었다). 상태는 기존 필드 (ScoutState · ScoutSpot · ScoutReportTick = 엄폐 칸을 고른 틱).
///  · 플레이어가 정찰병에게 직접 명령 (이동 · 공격 · 정지 · 사수 ...) 하면 자율이 꺼진다 (Unit.ScoutAuto). 카드 토글 (SetScoutAutoCommand) 이나 소속 중대가 새 명령을 받으면 다시 켜진다.
///  · 곡사포 관측: 정찰병은 spotter 유닛이라 그가 보는 표적이 SeenBySpotter 에 그대로 들어간다 (Combat4.cs).
///  · 소속 없는 정찰병 (ScoutAuto) 은 가까운 (attachRadius) 중대 중 정찰조가 비는 곳에 자동으로 붙는다 (전투 설정 · AI 가 나중에 중대를 짜도 된다).
/// 모든 값은 틱 · 입력의 함수. 저장은 ORG_ 구역 끝 (Organisation.cs), 체크섬은 HashOrganisation.
/// </summary>
public sealed partial class Unit
{
    /// <summary>소속 중대 (정찰조 구성원일 때). Company.Scouts 의 역참조 — Company (분대 · 대형 구성원) 와는 별개.</summary>
    public Company? ScoutCompany { get; internal set; }
    /// <summary>자율 행동 켜짐 (정찰병 유닛만 의미). 직접 명령을 받으면 꺼진다.</summary>
    [SnapExt] public bool ScoutAuto { get; internal set; } = true;
    [SnapExt] public ScoutState ScoutState { get; internal set; }
    /// <summary>마지막으로 자율 이동을 낸 목표 (관측 지점 · 복귀 지점).</summary>
    [SnapExt] internal Vector2 ScoutSpot;
    /// <summary>앞에 나와 적을 처음 본 틱 (-1 = 아직 못 봄). Cover 상태에서는 엄폐 칸을 고른 틱.</summary>
    [SnapExt] internal long ScoutReportTick = -1;
    /// <summary>이 틱 전에는 다시 앞으로 나가지 않는다 (복귀 뒤 재출발 대기).</summary>
    [SnapExt] internal long ScoutResumeTick;
    /// <summary>tweak1 노출 기억: 마지막으로 노려져 엄폐로 들어간 자리 · 그때 사격자 위치 · 틱 (-1 = 없음). 관측자를 같은 노출 지점으로 다시 보내지 않는 데 쓴다 (AI ObserverThink).</summary>
    [SnapExt] internal Vector2 ScoutExposed, ScoutThreat;
    [SnapExt] internal long ScoutExposedTick = -1;

    /// <summary>정찰병 유닛인가 (unit "scout": true).</summary>
    public bool IsScout => Def.Scout;
    /// <summary>[표시] 지금 관측 중 (서서 쌍안경 동작).</summary>
    public bool ScoutObserving => Def.Scout && ScoutAuto && ScoutState == ScoutState.Observe && Velocity == Vector2.Zero;
}

/// <summary>정찰병 자율 행동 켜기 · 끄기 (커맨드 카드 토글). 정찰병이 아닌 유닛은 무시.</summary>
public sealed record SetScoutAutoCommand(int Faction, int[] Units, bool On) : GameCommand(Faction);

public sealed partial class World
{
    const int ScoutThinkInterval = 15;   // 유닛마다 15틱 (Id 로 분산)

    struct ScoutCtx
    {
        public Vector2 Center, Anchor, Dir;
        public bool Active, Marching;
        /// <summary>숲 탐색 (forest): 앞에 숲이 있고 적이 안 보이는 동안 — 깊은 숲도 들어가 가로지르며 찾는다.</summary>
        public bool Probe;
    }

    [ThreadStatic] static List<Unit>? scoutTmp;

    static int ScTicks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));

    /// <summary>[테스트] 마지막에 생성한 유닛 n 기를 지운 뒤 Id 발급기를 되감는다 (TestDefs.Strip — 시작 정찰병이 없던 때의 Id 를 보존).</summary>
    internal void TestRewindIds(int n) { nextId -= n; }

    public void SetScoutAuto(int faction, IEnumerable<Unit> units, bool on) =>
        IssueUnits(faction, units.Where(u => u.Def.Scout), ids => new SetScoutAutoCommand(faction, ids, on));

    void ExecSetScoutAuto(int f, List<Unit> list, bool on)
    {
        foreach (var u in list)
        {
            if (!u.Def.Scout) continue;
            u.ScoutAuto = on;
            ScoutResetState(u);
            if (on) { u.ScoutResumeTick = 0; }
        }
    }

    static void ScoutResetState(Unit s)
    {
        s.ScoutState = ScoutState.With;
        s.ScoutReportTick = -1;
    }

    /// <summary>
    /// 순서 명령 (이동 · 공격 · 정지 · 사수 · 돌격) 의 대상 해석: 중대와 같이 명령받은 소속 정찰병은 명령에서 빼고 (자율 유지),
    /// 중대가 새 명령을 받으면 그 정찰조의 자율을 다시 켜고, 정찰병이 단독으로 (또는 소속 아닌 중대 없이) 받으면 자율을 끈다.
    /// </summary>
    List<Unit> ResolveOrder(int faction, int[] ids)
    {
        var list = Resolve(faction, ids);
        bool direct = false, viaCompany = false;
        foreach (var u in list)
        {
            if (u.Def.Scout) direct = true;
            else if (u.Company is { Scouts.Count: > 0 }) viaCompany = true;
        }
        if (!direct && !viaCompany) return list;
        var comps = new List<Company>();
        foreach (var u in list) if (u.Company is { } c && !comps.Contains(c)) comps.Add(c);
        foreach (var c in comps)
            foreach (var s in c.Scouts)
                if (!s.ScoutAuto) { s.ScoutAuto = true; ScoutResetState(s); s.ScoutResumeTick = 0; }
        if (!direct) return list;
        var res = new List<Unit>(list.Count);
        foreach (var u in list)
        {
            if (u.Def.Scout)
            {
                if (u.ScoutCompany is { } sc && comps.Contains(sc)) continue;   // 중대 명령: 정찰병은 따로 움직인다
                u.ScoutAuto = false; ScoutResetState(u);   // 직접 명령 (소속 없는 정찰병 포함 — 자동 편입도 막는다)
            }
            res.Add(u);
        }
        return res;
    }

    // ---------------------------------------------------------------- 편입 · 해제

    /// <summary>정찰병을 중대 정찰조에 넣는다 (이미 다른 정찰조면 먼저 뺀다).</summary>
    void AttachScout(Company c, Unit s)
    {
        DetachScout(s);
        int idx = c.Scouts.BinarySearch(s, UnitIdOrder.Instance);
        c.Scouts.Insert(idx < 0 ? ~idx : idx, s);
        s.ScoutCompany = c;
        ScoutResetState(s);
        s.ScoutResumeTick = 0;
    }

    void DetachScout(Unit s)
    {
        if (s.ScoutCompany is not { } c) return;
        c.Scouts.Remove(s);
        s.ScoutCompany = null;
        ScoutResetState(s);
    }

    /// <summary>중대가 사라질 때 (해체 · 전멸): 정찰조는 소속 없는 정찰병이 된다 (disband = 자율 끔, 전멸 = 켜 둠 → 가까운 중대에 다시 붙는다).</summary>
    void ReleaseScouts(Company c, bool disband)
    {
        foreach (var s in c.Scouts)
        {
            s.ScoutCompany = null;
            ScoutResetState(s);
            if (disband) s.ScoutAuto = false;
        }
        c.Scouts.Clear();
    }

    // ---------------------------------------------------------------- 매 틱

    /// <summary>StepGapChecks 뒤 · 유닛 갱신 앞에서 부른다. 정찰병의 이동 목표만 정하고 (일반 이동 명령과 같은 MoveGroup), 실제 이동은 UpdateUnit 이 한다.</summary>
    void StepScouts()
    {
        var r = Defs.ScoutAi;
        if (Tick % 30 == 0) AttachLooseScouts(r);
        for (int ci = 0; ci < Companies.Count; ci++)
        {
            var c = Companies[ci];
            if (c.Scouts.Count == 0) continue;
            ScoutCtx? ctx = null;
            for (int i = 0; i < c.Scouts.Count; i++)
            {
                var s = c.Scouts[i];
                if (!s.Alive || !s.ScoutAuto || (Tick + s.Id) % ScoutThinkInterval != 0) continue;
                ctx ??= BuildScoutCtx(c);
                ScoutThink(s, c, i, ctx.Value, r);
            }
        }
        if (!r.CoverOnFire) return;
        // 관측반 (정찰병만의 중대 구성원 — 자율 정찰이 아니라 AI · 플레이어가 부림): 피격 엄폐만 엔진이 한다
        for (int ci = 0; ci < Companies.Count; ci++)
        {
            var c = Companies[ci];
            for (int i = 0; i < c.Members.Count; i++)
            {
                var s = c.Members[i];
                if (!s.Alive || !s.Def.Scout || s.ScoutCompany != null || (Tick + s.Id) % ScoutThinkInterval != 0) continue;
                ObserverCoverThink(s, r);
            }
        }
    }

    /// <summary>이번 판단 간격 (15틱) 안에 노려졌는가.</summary>
    bool ScoutFiredUpon(Unit s, ScoutAiDef r) => r.CoverOnFire && Tick - s.LastFiredAtTick < ScoutThinkInterval;

    /// <summary>엄폐로 들어가거나 (다시 노려졌고 엄폐 칸에 이미 섰으면) 다른 칸으로. away = 사격자를 못 찾을 때 물러날 방향.</summary>
    void ScoutEnterCover(Unit s, ScoutAiDef r, Vector2 away)
    {
        if (s.ScoutState != ScoutState.Cover)
        {
            // tweak1: 노출 지점 · 사격자 위치를 기억 (엄폐 중 다시 노려져 칸을 바꾸는 것은 새 노출로 치지 않음)
            var th = ScoutThreatOf(s, r);
            s.ScoutExposed = s.Pos;
            s.ScoutThreat = th?.Pos ?? s.Pos;
            s.ScoutExposedTick = Tick;
        }
        s.ScoutState = ScoutState.Cover;
        s.ScoutReportTick = Tick;
        ScoutMove(s, ScoutCoverSpot(s, r, away), 0);
    }

    /// <summary>Cover 상태 진행: 도착하면 사수, 다시 노려지면 (엄폐 칸에 선 뒤 coverRepickSec 마다) 다른 칸, 조용하면 (coverCalmSec · coverMinSec) 끝 → true.</summary>
    bool ScoutCoverStep(Unit s, ScoutAiDef r, Vector2 away)
    {
        bool arrived = Vector2.Distance(s.Pos, s.ScoutSpot) <= 1.2f || s.Order != UnitOrder.Move && s.Group == null;
        if (arrived && s.Order == UnitOrder.Move) ResetOrder(s);
        if (arrived && s.Order != UnitOrder.Hold) { ResetOrder(s); s.Order = UnitOrder.Hold; }
        if (ScoutFiredUpon(s, r) && arrived && Tick - s.ScoutReportTick >= ScTicks(r.CoverRepickSec)) { ScoutEnterCover(s, r, away); return false; }
        return Tick - s.LastFiredAtTick >= ScTicks(r.CoverCalmSec) && Tick - s.ScoutReportTick >= ScTicks(r.CoverMinSec);
    }

    void ObserverCoverThink(Unit s, ScoutAiDef r)
    {
        if (s.ScoutState != ScoutState.Cover)
        {
            if (ScoutFiredUpon(s, r)) ScoutEnterCover(s, r, Vector2.Zero);
            return;
        }
        if (!ScoutCoverStep(s, r, Vector2.Zero)) return;
        s.ScoutState = ScoutState.With;   // 조용해짐 — 부리는 쪽 (AI ObserverThink · 플레이어) 이 다시 관측 지점으로 보낸다
        s.ScoutReportTick = -1;
        if (s.Order == UnitOrder.Move) ResetOrder(s);
        s.Order = UnitOrder.Hold;
    }

    /// <summary>
    /// 피격 엄폐 칸: 사격자 = 반경 coverThreatRadius 안에서 이 정찰병을 표적으로 잡은 가장 가까운 적 (없으면 가장 가까운 적; 그것도 없으면 away 쪽 20칸 앞을 위협으로 본다).
    /// 둘레 coverRadius 칸 중 점수 최대 칸: 사격자 사선 (직사 사선 HasFiringLine) 밖 +40, 지형 엄폐 +15, 사격자에서 멀어진 거리 × 2 (가까워지면 × 4 감점), 이동 거리 × 0.5 감점. 동점이면 먼저 훑은 칸 (결정적).
    /// </summary>
    /// <summary>사격자 = 반경 coverThreatRadius 안에서 이 정찰병을 표적으로 잡은 가장 가까운 적 (없으면 가장 가까운 적, 그것도 없으면 null).</summary>
    Unit? ScoutThreatOf(Unit s, ScoutAiDef r)
    {
        Unit? shooter = null, nearest = null;
        float bs = float.MaxValue, bn = float.MaxValue;
        var list = UnitsInRadius(s.Pos, r.CoverThreatRadius, scoutTmp ??= new List<Unit>());
        foreach (var e in list)
        {
            if (!e.Alive || !IsHostile(s.Owner, e.Owner)) continue;
            float d = Vector2.DistanceSquared(e.Pos, s.Pos);
            if (e.Target == s && (d < bs || d == bs && shooter != null && e.Id < shooter.Id)) { bs = d; shooter = e; }
            if (d < bn || d == bn && nearest != null && e.Id < nearest.Id) { bn = d; nearest = e; }
        }
        return shooter ?? nearest;
    }

    internal Vector2 ScoutCoverSpot(Unit s, ScoutAiDef r, Vector2 away)
    {
        var threat = ScoutThreatOf(s, r);
        Vector2 tp = threat?.Pos ?? (away != Vector2.Zero ? s.Pos - Vector2.Normalize(away) * 20f : s.Pos);
        if (threat == null && away == Vector2.Zero) return s.Pos;
        int tid = threat?.Id ?? 0;
        float d0 = Vector2.Distance(s.Pos, tp);
        var (bx, by) = GameMap.TileOf(s.Pos);
        int rad = Math.Max(1, r.CoverRadius);
        float best = float.MinValue; Vector2 bp = s.Pos;
        for (int dy = -rad; dy <= rad; dy++)
        for (int dx = -rad; dx <= rad; dx++)
        {
            if (dx * dx + dy * dy > rad * rad) continue;
            int x = bx + dx, y = by + dy;
            if (!Map.IsWalkable(x, y) || Map.Get(x, y) is TerrainType.Water or TerrainType.Wire) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float sc = -0.5f * MathF.Sqrt(dx * dx + dy * dy);
            float gain = Vector2.Distance(q, tp) - d0;
            sc += gain >= 0 ? gain * 2f : gain * 4f;
            if (Map.TerrainCover(q).Hit < 1f) sc += 15f;
            if (threat != null && !Map.HasFiringLine(tp, q, tid, s.Id)) sc += 40f;
            if (sc > best) { best = sc; bp = q; }
        }
        return bp;
    }

    void AttachLooseScouts(ScoutAiDef r)
    {
        List<Unit>? loose = null;
        foreach (var u in Units)
            if (u.Def.Scout && u.ScoutAuto && u.ScoutCompany == null && u.Company == null && u.Alive) (loose ??= new()).Add(u);
        if (loose == null || Companies.Count == 0) return;
        float maxD2 = r.AttachRadius * r.AttachRadius;
        foreach (var u in loose)
        {
            Company? best = null;
            float bd = maxD2;
            foreach (var c in Companies)
            {
                if (c.Owner != u.Owner || c.Scouts.Count >= r.PerCompany) continue;
                float d = Vector2.DistanceSquared(CompanyCenter(c), u.Pos);
                if (d < bd) { bd = d; best = c; }
            }
            if (best != null) AttachScout(best, u);
        }
    }

    static Vector2 CompanyCenter(Company c)
    {
        Vector2 sum = Vector2.Zero;
        int n = 0;
        foreach (var m in c.Members) if (m.Alive) { sum += m.Pos; n++; }
        return n > 0 ? sum / n : Vector2.Zero;
    }

    ScoutCtx BuildScoutCtx(Company c)
    {
        var ctx = new ScoutCtx();
        Vector2 sum = Vector2.Zero;
        int n = 0, moving = 0, engaged = 0;
        Vector2 goals = Vector2.Zero, tgt = Vector2.Zero;
        Unit? officer = null;
        int engTicks = ScTicks(3f);
        foreach (var m in c.Members)
        {
            if (!m.Alive) continue;
            sum += m.Pos; n++;
            if (officer == null && m.IsOfficer) officer = m;
            if (m.IsMoving && m.Group != null) { moving++; goals += m.Group.Goal; }
            if (m.Engaging || Tick - m.EngagedTick < engTicks) { engaged++; if (m.Target != null && tgt == Vector2.Zero) tgt = m.Target.Pos; }
        }
        ctx.Center = n > 0 ? sum / n : Vector2.Zero;
        ctx.Anchor = officer?.Pos ?? ctx.Center;
        Vector2 dir = Vector2.Zero;
        if (c.Anchor != null && !c.Anchor.Arrived)
        {
            ctx.Active = ctx.Marching = true;
            dir = c.Anchor.Goal - c.Anchor.Pos;
            if (dir.LengthSquared() < 4f) dir = c.Anchor.Heading;
        }
        else if (moving > 0 && moving * 3 >= n)
        {
            ctx.Active = ctx.Marching = true;
            dir = goals / moving - ctx.Center;
        }
        else if (engaged > 0)
        {
            ctx.Active = true;
            dir = c.Facing != Vector2.Zero ? c.Facing : tgt != Vector2.Zero ? tgt - ctx.Center : Vector2.Zero;
        }
        if (dir.LengthSquared() < 1e-6f) dir = c.Facing;
        ctx.Dir = dir.LengthSquared() > 1e-6f ? Vector2.Normalize(dir) : Vector2.Zero;
        if (ctx.Dir == Vector2.Zero) ctx.Active = false;
        BuildProbe(c, ref ctx, n);
        return ctx;
    }

    /// <summary>
    /// 숲 탐색 (forest, 2026-10-19 사용자 결정): 앞 (진행 · 정면 방향) forestProbeLook 칸 안에 숲이 있고, 적이 안 보이는 동안 (아래 ScoutThink 에서 정찰병별로 확인)
    /// 정찰조가 깊은 숲도 들어가 가로지른다. 중대가 서 있어도 (교착) forestProbeQuietSec 이상 조용하면 (전 구성원 교전 · 피격 없음) 정찰조를 앞으로 내보낸다.
    /// 상태 없음 — 틱 · 구성원 상태 · 지형의 함수.
    /// </summary>
    void BuildProbe(Company c, ref ScoutCtx ctx, int alive)
    {
        var r = Defs.ScoutAi;
        if (!r.ForestProbe || alive == 0) return;
        Vector2 dir = ctx.Dir != Vector2.Zero ? ctx.Dir : c.Facing != Vector2.Zero ? Vector2.Normalize(c.Facing) : Vector2.Zero;
        if (dir == Vector2.Zero) return;
        int quiet = ScTicks(r.ForestProbeQuietSec);
        foreach (var m in c.Members)
            if (m.Alive && (Tick - m.EngagedTick < quiet || Tick - m.LastFiredAtTick < quiet || m.Engaging)) return;
        if (!ctx.Active && Tick < quiet) return;
        bool forest = false;
        for (float d = 3f; d <= r.ForestProbeLook && !forest; d += 3f)
        {
            var (fx, fy) = GameMap.TileOf(ctx.Center + dir * d);
            if (Map.ForestDepth(fx, fy) > 0) forest = true;
        }
        if (!forest) return;
        ctx.Probe = true;
        if (!ctx.Active) { ctx.Active = true; ctx.Dir = dir; }
    }

    void ScoutThink(Unit s, Company c, int index, ScoutCtx ctx, ScoutAiDef r)
    {
        if (ctx.Probe && ScoutSeesEnemy(s)) ctx.Probe = false;   // 적을 찾았다 — 보통 정찰
        bool hpLow = s.Hp <= s.MaxHp * r.ReturnHpFrac;
        bool supp = s.Suppression >= r.ReturnSuppression;
        bool away = s.ScoutState is ScoutState.Advance or ScoutState.Observe;
        if (away && s.ScoutReportTick < 0 && ScoutSeesEnemy(s)) s.ScoutReportTick = Tick;   // 앞에서 적을 처음 봄 → 보고 타이머
        bool report = s.ScoutReportTick >= 0 && Tick - s.ScoutReportTick >= ScTicks(r.ReportSec);
        bool far = Vector2.Distance(s.Pos, ctx.Center) > (ctx.Probe ? r.MaxDist + r.ForestProbeExtra : r.MaxDist);
        // N22 피격 엄폐: 복귀 중이 아니면 곧바로 (제압 · 보고 복귀보다 먼저 — 노려진 자리에서 장교에게 걸어가지 않는다)
        if (s.ScoutState is not (ScoutState.Return or ScoutState.Cover) && ScoutFiredUpon(s, r))
        {
            ScoutEnterCover(s, r, ctx.Dir);
            return;
        }
        if (away && (supp || hpLow || report || far))
        {
            s.ScoutState = ScoutState.Return;
            s.ScoutReportTick = -1;
            s.ScoutResumeTick = Tick + ScTicks(r.CooldownSec);
            ScoutMove(s, ctx.Anchor, 0);
            return;
        }
        switch (s.ScoutState)
        {
            case ScoutState.Return:
                if (Vector2.Distance(s.Pos, ctx.Anchor) <= r.ArriveDist)
                {
                    s.ScoutState = ScoutState.With;
                    if (s.Order == UnitOrder.Move) ResetOrder(s);
                }
                else ScoutMove(s, ctx.Anchor, 3f);
                break;

            case ScoutState.Cover:
                if (!ScoutCoverStep(s, r, ctx.Dir)) break;
                s.ScoutReportTick = -1;
                if (hpLow || supp || far)   // 조용해졌지만 다쳤거나 · 눌렸거나 · 너무 멀다 → 장교에게
                {
                    s.ScoutState = ScoutState.Return;
                    s.ScoutResumeTick = Tick + ScTicks(r.CooldownSec);
                    ScoutMove(s, ctx.Anchor, 0);
                }
                else { s.ScoutState = ScoutState.With; s.ScoutResumeTick = Tick; }   // 다시 관측 (다음 판단에 새 관측 지점으로)
                break;

            case ScoutState.With:
                if (ctx.Active && !hpLow && s.Suppression < r.ResumeSuppression && Tick >= s.ScoutResumeTick)
                {
                    Vector2 spot = ScoutPickSpot(ScoutBase(s, c, index, ctx, r), r, ctx.Center, ctx.Dir, ctx.Probe);
                    s.ScoutState = ScoutState.Advance;
                    s.ScoutReportTick = -1;
                    ScoutMove(s, spot, 0);
                }
                else if (Vector2.Distance(s.Pos, ctx.Anchor) > r.FollowDist)
                    ScoutMove(s, ScoutFollowPoint(ctx, index), 3f);
                break;

            default:   // Advance · Observe
                if (ctx.Active)
                {
                    Vector2 b = ScoutBase(s, c, index, ctx, r);
                    // 서서 관측하던 정찰병이 aheadMin 밑으로 처졌거나 (중대가 따라왔다), 가던 지점이 따라잡을 수 없게 처졌거나, 방향이 크게 바뀌었다 → 새 지점으로 건너뛴다
                    float spotAhead = Vector2.Dot(s.ScoutSpot - ctx.Center, ctx.Dir);
                    bool stale = s.ScoutState == ScoutState.Observe ? Vector2.Dot(s.Pos - ctx.Center, ctx.Dir) < r.AheadMin : spotAhead < r.AheadMin - 2f;
                    if (stale || Vector2.Distance(b, s.ScoutSpot) > r.AheadMax + r.MarchLead + r.SearchRadius + 4f)
                    {
                        s.ScoutState = ScoutState.Advance;
                        ScoutMove(s, ScoutPickSpot(b, r, ctx.Center, ctx.Dir, ctx.Probe), 0);
                        break;
                    }
                }
                if (s.ScoutState == ScoutState.Advance)
                {
                    bool arrived = Vector2.Distance(s.Pos, s.ScoutSpot) <= 1.2f;
                    if (arrived || (s.Order != UnitOrder.Move && s.Group == null))   // 도착했거나 막혀서 멈췄다
                    {
                        s.ScoutState = ScoutState.Observe;
                        ResetOrder(s);
                        s.Order = UnitOrder.Hold;
                    }
                }
                break;
        }
    }

    /// <summary>진행 방향 앞 aheadMin~aheadMax (유닛 Id 로 결정) 칸, 정찰조 안 가로로 spread 간격으로 벌린 기준점.</summary>
    Vector2 ScoutBase(Unit s, Company c, int index, ScoutCtx ctx, ScoutAiDef r)
    {
        float ahead = r.AheadMin + (r.AheadMax - r.AheadMin) * Roll(s.Id, 0x5343) + (ctx.Marching ? r.MarchLead : 0f) + (ctx.Probe ? r.ForestProbeExtra : 0f);   // 행군 중에는 중대가 따라올 몫만큼 더 앞
        Vector2 side = new(-ctx.Dir.Y, ctx.Dir.X);
        float lat = (index - (c.Scouts.Count - 1) / 2f) * r.Spread;
        Vector2 p = ctx.Center + ctx.Dir * ahead + side * lat;
        return new Vector2(Math.Clamp(p.X, 1f, Map.Width - 1f), Math.Clamp(p.Y, 1f, Map.Height - 1f));
    }

    Vector2 ScoutFollowPoint(ScoutCtx ctx, int index)
    {
        Vector2 side = ctx.Dir != Vector2.Zero ? new Vector2(-ctx.Dir.Y, ctx.Dir.X) : new Vector2(0, 1);
        return ctx.Anchor + side * ((index % 2 == 0 ? 1 : -1) * (1.5f + index / 2));
    }

    /// <summary>기준점 둘레 searchRadius 칸에서 (중대보다 minAhead 이상 앞인 칸만) 관측하기 좋은 칸: 고지 +30, 숲 가장자리 (숲 가장자리 칸 · 숲과 맞닿은 열린 칸) +20, 엄폐 +5, 깊은 숲 −40 (시야 막힘), 기준점에서 멀수록 −(거리²). 같은 점수면 먼저 훑은 칸 (결정적).</summary>
    static float MinAhead(ScoutAiDef r) => MathF.Max(1f, r.AheadMin + 2f);   // 새 지점은 중대보다 이만큼 이상 앞

    internal Vector2 ScoutPickSpot(Vector2 b, ScoutAiDef r, Vector2 center = default, Vector2 dir = default, bool probe = false)
    {
        bool filter = dir != Vector2.Zero;
        float minAhead = MinAhead(r);
        float mx = r.MaxDist + (probe ? r.ForestProbeExtra : 0f);
        int bx = (int)MathF.Floor(b.X), by = (int)MathF.Floor(b.Y), rad = Math.Max(0, r.SearchRadius);
        int best = int.MinValue, ox = bx, oy = by;
        for (int dy = -rad; dy <= rad; dy++)
        for (int dx = -rad; dx <= rad; dx++)
        {
            int x = bx + dx, y = by + dy;
            if (!Map.IsWalkable(x, y) || Map.Get(x, y) is TerrainType.Water or TerrainType.Wire) continue;
            if (filter)   // 중대보다 minAhead 이상 앞 (되돌아 서지 않게), 복귀 거리 (maxDist) 안
            {
                Vector2 off = new Vector2(x + 0.5f, y + 0.5f) - center;
                if (Vector2.Dot(off, dir) < minAhead || off.LengthSquared() > (mx - 1f) * (mx - 1f)) continue;
            }
            int score = -(dx * dx + dy * dy);
            if (Map.IsHigh(x, y)) score += 30;
            if (probe && Map.ForestDepth(x, y) > 0) score += Map.IsDeepForest(x, y) ? r.ForestProbeDeepBonus : 20;   // 숲 탐색: 깊은 숲도 간다
            else if (Map.IsDeepForest(x, y)) score -= 40;
            else if (ForestEdge(x, y)) score += 20;
            if (Map.TerrainCover(new Vector2(x + 0.5f, y + 0.5f)).Hit < 1f) score += 5;
            if (score > best) { best = score; ox = x; oy = y; }
        }
        if (best == int.MinValue) return filter ? ScoutPickSpot(b, r, default, default, probe) : b;
        return new Vector2(ox + 0.5f, oy + 0.5f);
    }

    bool ForestEdge(int x, int y)
    {
        if (Map.ForestDepth(x, y) == 1) return true;
        if (Map.ForestDepth(x, y) > 1) return false;
        return Map.ForestDepth(x + 1, y) > 0 || Map.ForestDepth(x - 1, y) > 0 || Map.ForestDepth(x, y + 1) > 0 || Map.ForestDepth(x, y - 1) > 0;
    }

    /// <summary>자율 이동 (명령 지연 · 반응 지연 없음): retarget 칸 안이면 이미 가던 이동을 그대로 둔다.</summary>
    void ScoutMove(Unit s, Vector2 p, float retarget)
    {
        if (Vector2.Distance(s.Pos, p) < 1f)
        {
            if (s.Order == UnitOrder.Move) ResetOrder(s);
            s.ScoutSpot = p;
            return;
        }
        if (retarget > 0 && s.Order == UnitOrder.Move && s.Group != null && Vector2.Distance(s.ScoutSpot, p) < retarget) return;
        ResetOrder(s);
        s.Order = UnitOrder.Move;
        s.Group = new MoveGroup(this, p, 1);
        s.ScoutSpot = p;
    }

    /// <summary>정찰병이 지금 직접 보는 적이 있는가: 자기 시야 (SightOf) 안 · 아군 안개로 보이는 적대 유닛.</summary>
    bool ScoutSeesEnemy(Unit s)
    {
        int sight = SightOf(s);
        var list = UnitsInRadius(s.Pos, sight + 1, scoutTmp ??= new List<Unit>());
        foreach (var e in list)
        {
            if (!IsHostile(s.Owner, e.Owner)) continue;
            if (e.EdgeDistance(s.Pos) > sight) continue;
            if (Fog.CanSee(s.Owner, e)) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- 저장 (ORG_ 구역 끝) · 체크섬

    void WriteScouts(SnapWriter w)
    {
        int n = 0;
        foreach (var u in Units) if (u.Def.Scout) n++;
        w.I(n);
        foreach (var u in Units)
        {
            if (!u.Def.Scout) continue;
            w.I(u.Id); w.I(u.ScoutCompany?.Id ?? 0); w.Bool(u.ScoutAuto); w.Byte((byte)u.ScoutState);
            w.Vec(u.ScoutSpot); w.L(u.ScoutReportTick); w.L(u.ScoutResumeTick);
            w.Vec(u.ScoutExposed); w.Vec(u.ScoutThreat); w.L(u.ScoutExposedTick);   // tweak1 (FormatVersion 34)
        }
    }

    void ReadScouts(SnapReader r)
    {
        foreach (var u in Units) { u.ScoutCompany = null; u.ScoutAuto = true; u.ScoutState = ScoutState.With; u.ScoutReportTick = -1; u.ScoutResumeTick = 0; u.ScoutExposed = u.ScoutThreat = Vector2.Zero; u.ScoutExposedTick = -1; }
        foreach (var c in Companies) c.Scouts.Clear();
        int n = r.Count(8);
        for (int i = 0; i < n; i++)
        {
            int id = r.I(), cid = r.I();
            if (FindAny(id) is not Unit u || !u.Def.Scout) throw new SnapshotException(string.Format(SnapMsg.Invalid, "scout " + id));
            u.ScoutAuto = r.Bool();
            byte st = r.Byte();
            if (st > (byte)ScoutState.Cover) throw new SnapshotException(string.Format(SnapMsg.Invalid, "scout state"));
            u.ScoutState = (ScoutState)st;
            u.ScoutSpot = r.Vec(); u.ScoutReportTick = r.L(); u.ScoutResumeTick = r.L();
            u.ScoutExposed = r.Vec(); u.ScoutThreat = r.Vec(); u.ScoutExposedTick = r.L();
            if (cid != 0)
            {
                var c = Companies.FirstOrDefault(x => x.Id == cid) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "scout company " + cid));
                if (c.Owner != u.Owner || u.Company != null) throw new SnapshotException(string.Format(SnapMsg.Invalid, "scout company " + cid));
                u.ScoutCompany = c;
                c.Scouts.Add(u);
            }
        }
        foreach (var c in Companies) c.Scouts.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    void HashScouts(ref Hasher h)
    {
        foreach (var u in Units)
        {
            if (!u.Def.Scout) continue;
            h.Add(u.Id); h.Add(u.ScoutCompany?.Id ?? 0); h.Add(u.ScoutAuto ? 1 : 0); h.Add((int)u.ScoutState);
            h.Add(u.ScoutSpot.X); h.Add(u.ScoutSpot.Y); h.Add(u.ScoutReportTick); h.Add(u.ScoutResumeTick);
            h.Add(u.ScoutExposed.X); h.Add(u.ScoutExposed.Y); h.Add(u.ScoutThreat.X); h.Add(u.ScoutThreat.Y); h.Add(u.ScoutExposedTick);
        }
    }
}
