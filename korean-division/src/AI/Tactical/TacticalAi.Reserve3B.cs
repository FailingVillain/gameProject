using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 전술 AI 3단계 B (방어 · 예비 · 역습, docs/TACTICAL_AI_STAGE3_REVIEW_2026-10-19.md S3 · S4 · S8 + 3A 남은 일 1). 모든 행동은 프로필 키 뒤 (tactics.ammoRotate ·
/// reserveEvents · reserveRedesignate · counterOdds · defendRepick, strategy.fixPush) — 끄면 2단계 · 3A 와 같다 (겹침 "no3b").
///  · 저탄약 교대: 대대 예비 중대 (탄약 충분) ↔ 탄약 평균이 문턱 아래인 전선 중대. 나가는 중대는 교전하지 않고 예비 위치로 걸어가 회복, 들어오는 중대는 나간 중대의 정면 칸을 받는다.
///  · 사건 투입 (방어): 전선 중대 붕괴 → 그 칸 (틈), 주저항선을 넘은 적 → 그 지점 국지 역습 (돌파), 측면 끝의 적 → 그 끝 (측면). plugSeconds 뒤 전선 합류.
///  · 예비 재지정: 예비를 쓴 대대가 조용해지면 가장 성한 (병력 · 탄약) 중대를 다시 예비로.
///  · 고정 제대 밀기: 기동 제대 도착 뒤에도 고정 제대가 교전을 못 하면 표적 사거리 ×0.85 까지 공격 이동 (3A 순서 80% 미달 대책).
/// 상태: TacGroup.RotateUntil · RotateOut, TacBattalion.RotateTick · CalmSince · Plug*, TacFaction 누계 (스냅샷 — ObjCodec).
/// </summary>
public sealed partial class TacticalAi
{
    /// <summary>중대 탄약 평균 (탄약을 쓰는 살아 있는 구성원, 장교 제외). 탄약 규칙이 꺼졌거나 분견대면 1.</summary>
    internal static float GroupAmmo(World w, TacFaction s, TacGroup g)
    {
        if (!w.AmmoEnabled || g.CompanyId <= 0 || w.FindCompany(g.CompanyId) is not { } co || co.Owner != s.Faction) return 1f;
        int n = 0; float a = 0;
        foreach (var u in co.Members) if (u.Alive && u.UsesAmmo && !u.IsOfficer) { n++; a += u.AmmoRatio; }
        return n > 0 ? a / n : 1f;
    }

    /// <summary>교대 중인 중대 표시 정리: 시간이 다 됐거나 자리에 닿으면 끝.</summary>
    static void RotateUpkeep(World w, List<TacGroup> lines)
    {
        foreach (var g in lines)
            if (g.RotateUntil != 0 && (w.Tick >= g.RotateUntil || Vector2.Distance(g.Center, g.TaskPoint) < 6f && g.OrderTick > 0 && w.Tick - g.OrderTick > Sec(4)))
            { g.RotateUntil = 0; g.RotateOut = false; }
    }

    /// <summary>정면 정렬 열쇠: 들어오는 교대 중대는 받은 칸 (TaskPoint), 그 밖은 현재 위치 — 교대가 다른 중대의 칸을 뒤섞지 않게.</summary>
    static float SlotKey(World w, TacGroup g, Vector2 side) => g.RotateUntil > w.Tick && !g.RotateOut ? Vector2.Dot(g.TaskPoint, side) : Vector2.Dot(g.Center, side);

    static void StartRotate(World w, TacGroup g, bool outgoing)
    {
        g.Dug = false; g.PickKind = 0; g.OrderTick = -100000; g.SquadBound = 0;
        if (g.State != TacState.Assault) g.State = TacState.Idle;
        g.RotateUntil = w.Tick + Sec(60); g.RotateOut = outgoing;
    }

    /// <summary>저탄약 교대 (대대, 예비 보유 중): 맞바꾸면 true.</summary>
    bool AmmoRotateStep(World w, TacFaction s, TacBattalion b, List<TacGroup> lines)
    {
        var T = s.Profile.Tactics;
        if (!T.AmmoRotate || b.ReserveCommitted || b.ReserveGroup == 0 || w.Tick < b.RotateTick) return false;
        var rg = lines.Find(g => g.Id == b.ReserveGroup);
        if (rg == null || rg.State == TacState.Fallback || rg.RotateUntil > w.Tick || rg.Alive < rg.Initial * 0.5f) return false;
        if (GroupAmmo(w, s, rg) < T.AmmoRotateReady) return false;
        TacGroup? low = null; float la = T.AmmoRotateAt;
        foreach (var g in lines)
        {
            if (g == rg || g.Id == b.FlankGroup || g.Id == b.PlugGroup || g.State == TacState.Fallback || g.Element != ManElement.None || g.CrossStage != CsNone || g.CounterAssaultTick != 0 || g.Alive < 4) continue;
            float a = GroupAmmo(w, s, g);
            if (a < la) { la = a; low = g; }
        }
        if (low == null) return false;
        b.ReserveGroup = low.Id; b.RotateTick = w.Tick + Sec(T.AmmoRotateCooldown); s.AmmoRotations++;
        rg.TaskPoint = low.TaskPoint;   // 들어오는 중대가 나가는 중대의 정면 칸을 받는다 (SlotKey)
        StartRotate(w, low, true); StartRotate(w, rg, false);
        return true;
    }

    /// <summary>
    /// S3 사건 투입 (방어 과제, 예비 보유 중): 틈 (전선 중대 붕괴) · 돌파 (주저항선에 닿거나 넘은 적) · 측면 (정면 끝 바깥 사거리 ×0.6 안의 적) → 예비 중대를 그 지점으로. 투입했으면 true.
    /// </summary>
    bool ReserveEventStep(World w, TacFaction s, TacBattalion b, List<TacGroup> lines, Vector2 dir, Vector2 side, float R)
    {
        var T = s.Profile.Tactics;
        if (!T.ReserveEvents || b.Task != TacTask.Defend || b.ReserveCommitted || b.ReserveGroup == 0) return false;
        int kind = 0; Vector2 pp = default;
        foreach (var g in lines)
            if (g.Id != b.ReserveGroup && (g.State == TacState.Fallback || g.Spent) && g.RotateUntil <= w.Tick) { kind = 1; pp = g.TaskPoint; break; }
        float half = MathF.Max(b.Width, 12f) * 0.5f;
        if (kind == 0 && Grid.Nearest(b.Point, TacGrid.Infantry) is { } e)
        {
            Vector2 rel = e.Pos - b.Point;
            if (Vector2.Dot(rel, dir) < R * 0.25f && MathF.Abs(Vector2.Dot(rel, side)) < half + R * 0.5f) { kind = 2; pp = e.Pos; }
        }
        if (kind == 0)
            for (int sg = -1; sg <= 1 && kind == 0; sg += 2)
            {
                Vector2 end = b.Point + side * (sg * (half + R * 0.3f));
                if (Grid.Nearest(end, TacGrid.Infantry) is { } fe && Vector2.Distance(fe.Pos, end) < R * 0.6f && sg * Vector2.Dot(fe.Pos - b.Point, side) > half && Vector2.Dot(fe.Pos - b.Point, dir) < R * 0.8f)
                { kind = 3; pp = b.Point + side * (sg * half); }
            }
        if (kind == 0) return false;
        b.PlugGroup = b.ReserveGroup; b.PlugPoint = ClampMap(w.Map, pp); b.PlugUntil = w.Tick + Sec(T.PlugSeconds); b.PlugAttack = kind == 2;
        b.ReserveCommitted = true; b.ReserveGroup = 0; s.ReserveCommits++;
        if (kind == 1) s.PlugGaps++; else if (kind == 2) s.PlugBreaches++; else s.PlugFlanks++;
        if (lines.Find(g => g.Id == b.PlugGroup) is { } pg) { pg.OrderTick = -100000; pg.Dug = false; pg.PickKind = 0; if (pg.State != TacState.Assault) pg.State = TacState.Idle; }
        return true;
    }

    /// <summary>S3 예비 재지정: 예비를 쓴 대대가 reserveCalm 초 동안 조용하면 가장 성한 중대를 다시 예비로 (교대 표시 — 교전 없이 예비 위치로).</summary>
    void ReserveRedesignateStep(World w, TacFaction s, TacBattalion b, List<TacGroup> lines, float R)
    {
        var T = s.Profile.Tactics;
        if (!T.ReserveRedesignate || !s.Profile.Strategy.Reserve || !b.ReserveCommitted || b.PlugGroup != 0 || lines.Count < 3 || b.Task is not (TacTask.Attack or TacTask.Defend) || b.Role == TacRole.BaseOfFire)
        { b.CalmSince = 0; return; }
        var en = Grid.Nearest(b.Point, TacGrid.Any);
        bool calm = (en == null || Vector2.Distance(en.Pos, b.Point) > R * 1.5f + MathF.Max(b.Width, 12f) * 0.5f) && !lines.Exists(g => g.State is TacState.Fallback or TacState.Assault or TacState.Charge);
        if (!calm) { b.CalmSince = 0; return; }
        if (b.CalmSince == 0) { b.CalmSince = w.Tick; return; }
        if (w.Tick - b.CalmSince < Sec(T.ReserveCalm)) return;
        TacGroup? best = null; float bs = float.MinValue;
        foreach (var g in lines)
        {
            if (g.Id == b.FlankGroup || g.Element != ManElement.None || g.CrossStage != CsNone || g.CounterAssaultTick != 0 || g.Spent || g.Initial <= 0) continue;
            float sc = g.Alive / (float)g.Initial + GroupAmmo(w, s, g);
            if (sc > bs || sc == bs && best != null && g.Id < best.Id) { bs = sc; best = g; }
        }
        b.CalmSince = 0;
        if (best == null) return;
        b.ReserveCommitted = false; b.ReserveGroup = best.Id; b.FlankGroup = 0; s.ReserveRedesignations++;
        StartRotate(w, best, true);
    }

    /// <summary>사건 투입 중대 배치 (대대 층, 정면 배분 뒤): 지점으로 (돌파 = 공격, 틈 · 측면 = 방어). 끝나면 전선 합류.</summary>
    void PlugLayout(World w, TacFaction s, TacBattalion b, List<TacGroup> lines, Vector2 dir)
    {
        if (b.PlugGroup == 0) return;
        var pg = lines.Find(g => g.Id == b.PlugGroup);
        if (pg == null || w.Tick >= b.PlugUntil || b.Task != TacTask.Defend) { b.PlugGroup = 0; if (pg != null) { pg.OrderTick = -100000; pg.Dug = false; pg.PickKind = 0; } return; }
        pg.Task = b.PlugAttack ? TacTask.Attack : TacTask.Defend; pg.Role = b.PlugAttack ? TacRole.Manoeuvre : TacRole.MainLine;
        pg.HoldFire = false; pg.TaskDir = dir; pg.TaskWidth = Frontage(pg); pg.TaskPoint = b.PlugPoint;
    }

    /// <summary>교대로 나가는 중대 (예비 과제): 교전하지 않고 예비 위치로 이동. 처리했으면 true.</summary>
    bool RotateOutStep(World w, TacFaction s, TacGroup g)
    {
        if (!g.RotateOut || g.RotateUntil <= w.Tick) return false;
        if (Vector2.Distance(g.Center, g.TaskPoint) < 6f) { g.RotateOut = false; g.RotateUntil = 0; return false; }
        if (Retarget(w, g, TacState.Advance, g.TaskPoint, 6f, contactLimit: false))
        {
            Order(w, s, g, mem, Ord.Move, g.TaskPoint, g.TaskDir, Frontage(g));
            g.State = TacState.Advance; g.StateTick = w.Tick;
        }
        return true;
    }

    /// <summary>고정 제대 밀기 (ManStage 5 → 6 끝, 3A 남은 일 1): 표적이 사거리 ×0.9 밖이면 사거리 ×0.85 지점까지 공격 이동. 처리했으면 true.</summary>
    bool FixPushStep(World w, TacFaction s, TacGroup g, float range)
    {
        if (g.ManStage != 5) return false;
        var e = FixTarget(g, false) ?? Grid.Nearest(g.Center, TacGrid.Infantry);
        if (e == null || Vector2.Distance(e.Pos, g.Center) <= range * 0.9f) { g.ManStage = 6; return false; }   // 6 = 밀기 끝 (계획이 끝날 때까지 다시 밀지 않음)
        Vector2 toE = SafeNorm(e.Pos - g.Center, g.TaskDir);
        Vector2 p = ClampMap(w.Map, e.Pos - toE * (range * 0.85f));
        if (Retarget(w, g, TacState.Advance, p, 4f))
        {
            Order(w, s, g, mem, Ord.AttackMove, p, toE, g.TaskWidth);
            g.State = TacState.Advance; g.StateTick = w.Tick; s.FixPushes++;
        }
        return true;
    }
}
