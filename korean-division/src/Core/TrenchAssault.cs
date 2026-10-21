using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>참호 돌격 단계 (N32): None = 안 함.</summary>
public enum TaStage : byte { None = 0, Suppress = 1, Approach = 2, BreakIn = 3, Clear = 4, Consolidate = 5 }

/// <summary>
/// 참호 돌격 교리 (N32, 사용자 승인 2026-10-20): 중대가 적 참호선을 치는 자동 순서. 플레이어 명령 (TrenchAssaultCommand) · 전술 AI 가 같은 명령을 쓴다.
///  1 제압 (Suppress): 집결 → 경기관총이 사거리 안 사격 진지에서 참호 방어병을 누르고, 포병은 지점 포격 (있으면) — 방어병이 엎드림 · 제압되면 (또는 시간 초과) 다음.
///  2 접근 (Approach): 소총 분대가 번갈아 약진 (한 쪽이 움직이고 한 쪽이 엄호, 엄폐 칸 우선) — 참호까지 boundEndDist 안이면 속보로 수류탄 거리까지 달린다.
///  3 돌파 (BreakIn): 수류탄 일제 투척 → 착검 돌격 (ExecCharge, N57 착검 포함).
///  4 소탕 (Clear): 참호 안 분대가 참호를 따라 양쪽 끝으로 옆으로 쓸어내고, 경기관총은 참호 양 끝을 잡는다. 보이는 적이 없으면 다음.
///  5 확보 (Consolidate): 점령한 참호를 반대쪽 (공격 방향) 으로 향해 곧바로 점유 (OccupyPositions) — 역습 대비 사수.
/// 연막 · 철조망 절단은 이 묶음에 없다 (tactics.json trenchAssault.smokeHook · wireCutHook 자리만). 붕괴 · 패주에는 의존하지 않는다.
/// 취소: 같은 명령 (On = false) · 그 중대 유닛에 내린 다른 명령. 상태는 Company.TA (ORG_ 끝, FormatVersion 44).
/// </summary>
public sealed record TrenchAssaultCommand(int Faction, int[] Companies, Vector2 Target, bool On, bool Ai = false) : GameCommand(Faction);   // Ai = 전술 AI 가 낸 명령 (리플레이 재생에는 AI 가 없으므로 시뮬이 세력의 IsAi 를 보면 안 됨 — 명령에 싣는다)

/// <summary>tactics.json "trenchAssault" — 참호 돌격 수치 (밸런스 아님, 판단 · 순서). 칸 = 지도 칸.</summary>
public sealed class TrenchAssaultDef
{
    public bool Enabled { get; set; } = true;
    public float DecideSec { get; set; } = 0.5f;
    public float SnapRadius { get; set; } = 6f;
    public int MinMen { get; set; } = 6;
    // 1 제압
    public float FarDist { get; set; } = 40f;
    public float GunStandFrac { get; set; } = 0.85f;
    public float PrepMinSec { get; set; } = 8f;
    public float PrepMaxSec { get; set; } = 45f;
    public float SuppGoal { get; set; } = 0.35f;
    public float SuppFrac { get; set; } = 0.5f;
    public float SuppRadius { get; set; } = 12f;
    public float StageMaxSec { get; set; } = 150f;
    public bool UseGroundFire { get; set; } = true;
    public bool AiGroundFire { get; set; } = false;
    public float CreepLead { get; set; } = 10f;
    public float LiftDist { get; set; } = 14f;
    // 2 접근
    public float BoundLength { get; set; } = 6f;
    public float BoundMaxSec { get; set; } = 10f;
    public float BoundEndDist { get; set; } = 12f;
    public int CoverSearch { get; set; } = 3;
    public float RushStandFrac { get; set; } = 0.8f;
    public float ApproachMaxSec { get; set; } = 150f;
    public float BreakFrac { get; set; } = 0.5f;
    // 3 돌파
    public int Volleys { get; set; } = 2;
    public float VolleyGapSec { get; set; } = 1.2f;
    public float FuseWaitSec { get; set; } = 2f;
    public float ChargeThrough { get; set; } = 3f;
    public float BreakMaxSec { get; set; } = 25f;
    public float EnterFrac { get; set; } = 0.4f;
    // 4 소탕
    public float ClearMaxSec { get; set; } = 60f;
    public float ClearConfirmSec { get; set; } = 3f;
    public float ClearRadius { get; set; } = 2.5f;
    public float EndHoldBack { get; set; } = 1.5f;
    // 5 확보
    public float HoldPad { get; set; } = 3f;
    public float ConsolidateSec { get; set; } = 15f;
    public float CoverMaxHit { get; set; } = 0.9f;
    // 전체
    public float MaxTotalSec { get; set; } = 420f;
    public float CooldownSec { get; set; } = 60f;
    public float SpanRadius { get; set; } = 45f;
    // 전술 AI (TacticalAi.TrenchAssault.cs): 공격 과제 소총 중대가 보이는 적 참호선 (참호 안 방어병 aiMinDefenders 이상) 을 치려 할 때
    public bool AiEnabled { get; set; } = true;
    public float AiTriggerDist { get; set; } = 70f;
    public float AiMinDist { get; set; } = 10f;
    public int AiMinDefenders { get; set; } = 3;
    public int AiMinMen { get; set; } = 20;
    public float AiMinOdds { get; set; } = 0.8f;
    // 자리만 (이 묶음에서는 구현 없음 — 참호 돌파 묶음: N59 연막 · 철조망 절단)
    public bool SmokeHook { get; set; } = false;
    public bool WireCutHook { get; set; } = false;
}

/// <summary>중대의 참호 돌격 상태 (저장 · 체크섬 · Audit).</summary>
public sealed class TrenchAssaultState
{
    public TaStage Stage;
    /// <summary>단계 안 하위 상태 (0 = 아직 시작 안 함).</summary>
    public byte Sub;
    /// <summary>마지막 결과: 0 없음 · 1 성공 · 2 실패 · 3 취소.</summary>
    public byte Result;
    /// <summary>1 = 전술 AI 가 시작.</summary>
    public byte Flags;
    public Vector2 Target, Dir, FirePoint;
    public long StartTick, StageTick, ReadyTick, SubTick, BoundTick, CooldownUntil;
    public int BoundGroup;

    public bool Active => Stage != TaStage.None;

    internal void Write(SnapWriter w)
    {
        w.Byte((byte)Stage); w.Byte(Sub); w.Byte(Result); w.Byte(Flags);
        w.F(Target.X); w.F(Target.Y); w.F(Dir.X); w.F(Dir.Y); w.F(FirePoint.X); w.F(FirePoint.Y);
        w.L(StartTick); w.L(StageTick); w.L(ReadyTick); w.L(SubTick); w.L(BoundTick); w.L(CooldownUntil); w.I(BoundGroup);
    }

    internal void Read(SnapReader r)
    {
        byte st = r.Byte();
        if (!Enum.IsDefined(typeof(TaStage), st)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "trench assault stage"));
        Stage = (TaStage)st; Sub = r.Byte(); Result = r.Byte(); Flags = r.Byte();
        Target = new Vector2(r.F(), r.F()); Dir = new Vector2(r.F(), r.F()); FirePoint = new Vector2(r.F(), r.F());
        if (!float.IsFinite(Target.X) || !float.IsFinite(Target.Y) || !float.IsFinite(Dir.X) || !float.IsFinite(Dir.Y) || !float.IsFinite(FirePoint.X) || !float.IsFinite(FirePoint.Y))
            throw new SnapshotException(string.Format(SnapMsg.Invalid, "trench assault point"));
        StartTick = r.L(); StageTick = r.L(); ReadyTick = r.L(); SubTick = r.L(); BoundTick = r.L(); CooldownUntil = r.L(); BoundGroup = r.I();
    }
}

public sealed partial class Company
{
    /// <summary>N32 참호 돌격 상태 (TrenchAssault.cs). 저장 · 체크섬.</summary>
    internal readonly TrenchAssaultState TA = new();
    /// <summary>[조회] 참호 돌격 단계 (None = 안 함).</summary>
    public TaStage TrenchAssaultStage => TA.Stage;
    /// <summary>[조회] 참호 돌격 목표 참호 지점.</summary>
    public Vector2 TrenchAssaultTarget => TA.Target;
}

public sealed partial class World
{
    TrenchAssaultDef TaR => TacRules.TrenchAssault;
    internal TrenchAssaultDef TrenchAssaultRules => TaR;

    // ---------------------------------------------------------------- 공개 API

    /// <summary>[UI · AI] p 에서 radius 안의 가장 가까운 참호 칸 중심 (없으면 null).</summary>
    public Vector2? TrenchPointNear(Vector2 p, float radius)
    {
        var comps = TrenchComps();
        int w = Map.Width;
        int x0 = Math.Max(0, (int)MathF.Floor(p.X - radius)), x1 = Math.Min(w - 1, (int)MathF.Floor(p.X + radius));
        int y0 = Math.Max(0, (int)MathF.Floor(p.Y - radius)), y1 = Math.Min(Map.Height - 1, (int)MathF.Floor(p.Y + radius));
        float best = radius * radius + 1e-3f;
        Vector2? res = null;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (comps[y * w + x] < 0) continue;
                var q = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.DistanceSquared(q, p);
                if (d < best) { best = d; res = q; }
            }
        return res;
    }

    /// <summary>[UI · AI] 이 중대가 참호 돌격 중인가.</summary>
    public bool TrenchAssaultActive(Company c) => c.TA.Stage != TaStage.None;

    /// <summary>참호 돌격 명령 (플레이어 · 전술 AI 공통). 자기 중대만. target = 참호 지점 (참호 칸 SnapRadius 안으로 맞춘다).</summary>
    public bool TrenchAssault(int faction, IEnumerable<int> companies, Vector2 target, bool ai = false)
    {
        if (!TaR.Enabled) return false;
        var ids = companies.Distinct().OrderBy(i => i).Where(i => OwnCompany(faction, i) != null).ToArray();
        if (ids.Length == 0 || TrenchPointNear(target, TaR.SnapRadius) == null) return false;
        Issue(new TrenchAssaultCommand(faction, ids, target, true, ai));
        return true;
    }

    /// <summary>참호 돌격 취소 (플레이어 · AI).</summary>
    public void CancelTrenchAssault(int faction, IEnumerable<int> companies)
    {
        var ids = companies.Distinct().OrderBy(i => i).Where(i => OwnCompany(faction, i) != null).ToArray();
        if (ids.Length > 0) Issue(new TrenchAssaultCommand(faction, ids, Vector2.Zero, false));
    }

    void ExecTrenchAssault(int f, int[] companies, Vector2 target, bool on, bool ai)
    {
        foreach (int id in companies)
        {
            if (OwnCompany(f, id) is not { } c) continue;
            if (!on) { if (c.TA.Active) TaEnd(c, 3); continue; }
            if (!TaR.Enabled || c.Members.Count < Math.Min(TaR.MinMen, 1) || TrenchPointNear(target, TaR.SnapRadius) is not Vector2 t) continue;
            if (c.TA.Active) TaEnd(c, 3);
            TaStart(c, t, ai);
        }
    }

    /// <summary>다른 명령이 이 중대 유닛을 부르면 참호 돌격 취소 (ExecuteInner 맨 앞). 우리 안의 내부 호출은 명령이 아니라 통과.</summary>
    void TaCancelByOrder(GameCommand command)
    {
        if (Companies.Count == 0) return;
        int[]? ids = command switch
        {
            MoveCommand c => c.Units, AttackMoveCommand c => c.Units, QuickStepCommand c => c.Units, RetreatCommand c => c.Units,
            AttackCommand c => c.Units, StopCommand c => c.Units, HoldCommand c => c.Units, ChargeCommand c => c.Units,
            OccupyPositionsCommand c => c.Units, _ => null,
        };
        if (ids == null) return;
        foreach (int id in ids)
            if (Find(id) is Unit { Company: { } co } u && u.Owner == command.Faction && co.TA.Active) { string k = command.GetType().Name + "@" + co.TA.Stage; Stats.TaCancelWhy[k] = Stats.TaCancelWhy.GetValueOrDefault(k) + 1; TaEnd(co, 3); }
    }

    // ---------------------------------------------------------------- 상태 전환

    void TaStart(Company c, Vector2 t, bool ai)
    {
        var ta = c.TA;
        var cen = Centroid(c.Members);
        var dir = t - cen;
        dir = dir.LengthSquared() > 1e-4f ? Vector2.Normalize(dir) : new Vector2(0, -1);
        ta.Stage = TaStage.Suppress; ta.Sub = 0; ta.Result = 0; ta.Flags = (byte)(ai ? 1 : 0);
        ta.Target = t; ta.Dir = dir; ta.FirePoint = Vector2.Zero;
        ta.StartTick = ta.StageTick = Tick; ta.ReadyTick = ta.SubTick = ta.BoundTick = 0; ta.BoundGroup = 0;
        if (c.Drill.Phase != DrillPhase.Idle) EndDrill(c, reform: false);
        c.Drill.Clear();
        Stats.TaStarts++; Stats.TaStageEntered[1]++;
    }

    void TaGo(Company c, TaStage s)
    {
        c.TA.Stage = s; c.TA.Sub = 0; c.TA.StageTick = Tick; c.TA.SubTick = 0;
        Stats.TaStageEntered[(int)s]++;
    }

    /// <summary>끝: result 1 성공 · 2 실패 · 3 취소. 지점 포격 풀고 쿨다운.</summary>
    void TaEnd(Company c, byte result)
    {
        var ta = c.TA;
        if (ta.Stage == TaStage.None) return;
        TaStopFire(c);
        if (result == 1) Stats.TaDone++; else if (result == 2) Stats.TaFail++; else Stats.TaCancel++;
        ta.Stage = TaStage.None; ta.Sub = 0; ta.Result = result; ta.BoundGroup = 0;
        ta.CooldownUntil = Tick + SecTicks(TaR.CooldownSec);
        if (result == 2)
            foreach (var u in c.Members) if (u.Alive && !u.Charging && !Moving(u)) DrillHoldUnit(u);   // 실패: 서 있는 자리에서 사수
    }

    // ---------------------------------------------------------------- 매 틱

    readonly List<Unit> taGun = new(), taAs = new(), taOff = new(), taTmp = new(), taMv = new();
    readonly List<Vector2> taTiles = new();

    void StepTrenchAssaults()
    {
        if (Companies.Count == 0) return;
        int iv = SecTicks(TaR.DecideSec);
        foreach (var c in Companies)
        {
            if (c.TA.Stage == TaStage.None) continue;
            if ((Tick + c.Id) % iv != 0) continue;
            TaStep(c);
        }
    }

    bool TaIsGunner(Unit u) => !u.IsOfficer && u.Def.Weapon != null && Defs.Tactics.Roles.ByWeapon.TryGetValue(u.Def.Weapon, out var r) && r == "fire";

    int TaGroupOf(Unit u, bool byId) => byId || u.Squad == null ? (u.Id & 1) : (u.Squad.Number & 1);

    void TaSplit(Company c)
    {
        taGun.Clear(); taAs.Clear(); taOff.Clear();
        foreach (var u in c.Members)
        {
            if (!u.Alive || u.Surrendered) continue;
            if (u.IsOfficer) taOff.Add(u);
            else if (TaIsGunner(u)) taGun.Add(u);
            else taAs.Add(u);
        }
    }

    void TaStep(Company c)
    {
        var ta = c.TA; var R = TaR;
        TaSplit(c);
        if (taAs.Count + taGun.Count == 0 || (taAs.Count == 0 && ta.Stage < TaStage.Clear)) { TaEnd(c, 2); return; }
        if (Tick - ta.StartTick > SecTicks(R.MaxTotalSec)) { TaEnd(c, 2); return; }
        if (InHandover(c)) return;
        TaGeometry(ta.Target, out var tiles, out var endA, out var endB);
        switch (ta.Stage)
        {
            case TaStage.Suppress: TaSuppress(c, tiles); break;
            case TaStage.Approach: TaApproach(c, tiles); break;
            case TaStage.BreakIn: TaBreakIn(c, tiles); break;
            case TaStage.Clear: TaClear(c, tiles, endA, endB); break;
            case TaStage.Consolidate: TaConsolidate(c, tiles); break;
        }
    }

    // ---------------------------------------------------------------- 지오메트리 (참호 묶음 · 양 끝 — 매번 지도에서 다시 계산, 저장 안 함)

    /// <summary>t 가 속한 참호 묶음의 칸 (t 둘레 SpanRadius 안) 과 참호 축 양 끝 칸.</summary>
    void TaGeometry(Vector2 t, out List<Vector2> tiles, out Vector2 endA, out Vector2 endB)
    {
        taTiles.Clear(); tiles = taTiles;
        var (tx, ty) = GameMap.TileOf(t);
        int comp = TrenchCompAt(tx, ty);
        endA = endB = t;
        if (comp < 0) return;
        var comps = TrenchComps();
        int w = Map.Width, r = (int)MathF.Ceiling(TaR.SpanRadius);
        int x0 = Math.Max(0, tx - r), x1 = Math.Min(w - 1, tx + r), y0 = Math.Max(0, ty - r), y1 = Math.Min(Map.Height - 1, ty + r);
        Vector2 sum = Vector2.Zero;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (comps[y * w + x] != comp) continue;
                var q = new Vector2(x + 0.5f, y + 0.5f);
                taTiles.Add(q); sum += q;
            }
        if (taTiles.Count < 2) return;
        var m = sum / taTiles.Count;
        float sxx = 0, syy = 0, sxy = 0;
        foreach (var q in taTiles) { var d = q - m; sxx += d.X * d.X; syy += d.Y * d.Y; sxy += d.X * d.Y; }
        float ang = 0.5f * DetMath.Atan2(2f * sxy, sxx - syy);
        var ax = new Vector2(DetMath.Cos(ang), DetMath.Sin(ang));
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var q in taTiles)
        {
            float p = Vector2.Dot(q - m, ax);
            if (p < lo) { lo = p; endA = q; }
            if (p > hi) { hi = p; endB = q; }
        }
    }

    float TaTrenchDist(Vector2 p, List<Vector2> tiles)
    {
        if (tiles.Count == 0) return float.MaxValue;
        float best = float.MaxValue;
        foreach (var q in tiles) { float d = Vector2.DistanceSquared(p, q); if (d < best) best = d; }
        return MathF.Sqrt(best);
    }

    float TaMeanDist(List<Unit> list, List<Vector2> tiles)
    {
        if (list.Count == 0) return 0;
        float s = 0;
        foreach (var u in list) s += TaTrenchDist(u.Pos, tiles);
        return s / list.Count;
    }

    // ---------------------------------------------------------------- 지점 포격 (포병 있으면; 전술 AI 는 자기 포병 AI 가 따로 쏘므로 기본 꺼짐)

    bool TaFireOk(Company c) => TaR.UseGroundFire && (c.TA.Flags == 0 || TaR.AiGroundFire);   // Flags 1 = 전술 AI 가 시작 (포병 AI 와 충돌하므로 기본 꺼짐)

    void TaFire(Company c, Vector2 point)
    {
        var ta = c.TA;
        if (!TaFireOk(c)) return;
        taTmp.Clear();
        foreach (var u in Units)
        {
            if (!u.Alive || u.Owner != c.Owner || !CanGroundFire(u)) continue;
            bool mine = u.Order == UnitOrder.GroundFire && ta.FirePoint != Vector2.Zero && Vector2.DistanceSquared(u.GroundPoint, ta.FirePoint) < 0.36f;
            bool free = u.Order is UnitOrder.Idle or UnitOrder.Hold;
            if ((mine || (free && ta.FirePoint == Vector2.Zero)) && CanGroundFireAt(u, point)) taTmp.Add(u);
        }
        if (taTmp.Count > 0) { ExecGroundFire(c.Owner, new List<Unit>(taTmp), point, GroundFireKind.Sustained, Vector2.Zero); Stats.TaFireOrders++; }
        ta.FirePoint = point;
    }

    void TaStopFire(Company c)
    {
        var ta = c.TA;
        if (ta.FirePoint == Vector2.Zero) return;
        foreach (var u in Units)
            if (u.Alive && u.Owner == c.Owner && u.Order == UnitOrder.GroundFire && Vector2.DistanceSquared(u.GroundPoint, ta.FirePoint) < 0.36f) ResetOrder(u);
        ta.FirePoint = Vector2.Zero;
    }

    // ---------------------------------------------------------------- 1 제압

    void TaSuppress(Company c, List<Vector2> tiles)
    {
        var ta = c.TA; var R = TaR;
        var T = ta.Target;
        float farD = R.FarDist;
        var stagePos = ClampToMap(T - ta.Dir * farD);
        Vector2 ac = taAs.Count > 0 ? Centroid(taAs) : Centroid(c.Members);
        float dAs = TaTrenchDist(ac, tiles);
        if (ta.Sub == 0)
        {
            ta.Sub = 1; ta.SubTick = Tick;
            TaFire(c, T);
        }
        // 소총 · 장교: 멀면 집결 지점까지 걷고 (한 번), 닿으면 서서 엄호
        bool staged = dAs <= farD + 3f;
        if (!staged)
        {
            taMv.Clear(); foreach (var u in taAs) taMv.Add(u); foreach (var u in taOff) taMv.Add(u);
            bool any = false; foreach (var u in taMv) if (Moving(u) || u.Charging) { any = true; break; }
            if (!any) TaMove(new List<Unit>(taMv), stagePos, UnitOrder.Move, ta.Dir, false, FormType.Line);
        }
        else { foreach (var u in taAs) if (!Moving(u)) DrillHoldUnit(u); foreach (var u in taOff) if (!Moving(u)) DrillHoldUnit(u); }
        // 경기관총: 사거리 안 사격 진지로 (적 참호에서 사거리 × gunStandFrac 떨어진 곳, 이미 안이면 그 자리)
        bool gunsReady = true;
        if (taGun.Count > 0)
        {
            int gReady = 0;
            float rng = 0; foreach (var u in taGun) if (u.Weapon != null) rng = MathF.Max(rng, u.Weapon.Range);
            float standD = MathF.Max(6f, rng * R.GunStandFrac);
            var standPos = ClampToMap(T - ta.Dir * standD);
            taMv.Clear();
            foreach (var u in taGun)
            {
                float d = TaTrenchDist(u.Pos, tiles);
                if (d > standD + 3f) { if (!Moving(u)) taMv.Add(u); }
                else { gReady++; if (!Moving(u)) DrillHoldUnit(u); }
            }
            gunsReady = gReady * 2 >= taGun.Count;   // 절반 이상이 진지에 닿으면 (끼어 못 가는 한 정 때문에 전체가 서지 않게)
            if (taMv.Count > 0) TaMove(new List<Unit>(taMv), standPos, UnitOrder.Move, ta.Dir, false, FormType.Line);
        }
        bool timeout = Tick - ta.StageTick > SecTicks(R.StageMaxSec);
        if (!(staged && gunsReady) && !timeout) return;
        if (ta.ReadyTick == 0) ta.ReadyTick = Tick;
        float el = (Tick - ta.ReadyTick) * Config.SimDt;
        if (el < R.PrepMinSec && !timeout) return;
        if (el < R.PrepMaxSec && !timeout && !TaDefendersPinned(c, tiles)) return;
        TaGo(c, TaStage.Approach);
    }

    /// <summary>참호 둘레 (SuppRadius) 의 보이는 방어병이 엎드림 · 고정 · 제압 (≥ SuppGoal) 인 비율 ≥ SuppFrac, 또는 보이는 방어병이 없을 때 true.</summary>
    bool TaDefendersPinned(Company c, List<Vector2> tiles)
    {
        var T = c.TA.Target;
        int n = 0, ok = 0;
        UnitsInRadius(T, TaR.SuppRadius + 20f, taTmp);
        foreach (var e in taTmp)
        {
            if (!e.Alive || e.Surrendered || !IsHostile(c.Owner, e.Owner) || !e.Def.IsInfantry || !Fog.CanSee(c.Owner, e)) continue;
            if (!(e.InTrench || TaTrenchDist(e.Pos, tiles) <= 2f)) continue;
            n++;
            if (e.Prone || e.Pinned || e.SuppressionRatio >= TaR.SuppGoal) ok++;
        }
        return n == 0 || ok >= n * TaR.SuppFrac;
    }

    // ---------------------------------------------------------------- 2 접근

    void TaMove(List<Unit> list, Vector2 dest, UnitOrder order, Vector2 dir, bool quick, FormType form)
    {
        if (list.Count == 0) return;
        DrillMove(list, dest, order, dir, MathF.Max(3f, list.Count * 1.2f), form, quick);
    }

    void TaApproach(Company c, List<Vector2> tiles)
    {
        var ta = c.TA; var R = TaR;
        var T = ta.Target;
        if (Tick - ta.StageTick > SecTicks(R.ApproachMaxSec)) { TaGo(c, TaStage.BreakIn); return; }
        float dist = TaMeanDist(taAs, tiles);
        float stand = Defs.Grenade.Range * R.RushStandFrac;
        // 포격: 소총이 liftDist 안이면 포격 지점을 참호 너머 (creepLead) 로 옮긴다 (이동 탄막)
        if (dist <= R.LiftDist && ta.FirePoint != Vector2.Zero && Vector2.DistanceSquared(ta.FirePoint, T) < 0.36f) TaFire(c, ClampToMap(T + ta.Dir * R.CreepLead));
        // 총 사격 진지 지킴 (경기관총): 서서 사격
        foreach (var u in taGun) if (!Moving(u)) DrillHoldUnit(u);
        if (dist <= Defs.Grenade.Range - 0.2f + 0.5f || TaFracWithin(taAs, tiles, Defs.Grenade.Range - 0.2f) >= R.BreakFrac) { TaGo(c, TaStage.BreakIn); return; }
        if (dist <= R.BoundEndDist + 1f)
        {
            // 돌진: 속보로 수류탄 거리까지 (경기관총은 엄호)
            if (ta.Sub == 0)
            {
                ta.Sub = 1; ta.SubTick = Tick;
                taMv.Clear(); foreach (var u in taAs) taMv.Add(u); foreach (var u in taOff) taMv.Add(u);
                TaMove(new List<Unit>(taMv), ClampToMap(T - ta.Dir * stand), UnitOrder.Move, ta.Dir, true, FormType.Line);
            }
            else if ((Tick - ta.SubTick) % SecTicks(5f) == 0)
            {
                // 멈춘 병사 (막힘 · 도착) 만 다시 보냄
                taMv.Clear();
                foreach (var u in taAs) if (!Moving(u) && TaTrenchDist(u.Pos, tiles) > Defs.Grenade.Range - 0.2f) taMv.Add(u);
                if (taMv.Count > 0) TaMove(new List<Unit>(taMv), ClampToMap(T - ta.Dir * stand), UnitOrder.Move, ta.Dir, true, FormType.Line);
            }
            return;
        }
        // 분대 교대 약진
        bool byId = true;
        { int a = 0, b = 0; foreach (var u in taAs) { if (u.Squad != null) { if ((u.Squad.Number & 1) == 0) a++; else b++; } } if (a > 0 && b > 0) byId = false; }
        taMv.Clear();
        foreach (var u in taAs) if (TaGroupOf(u, byId) == ta.BoundGroup) taMv.Add(u);
        bool moving = false;
        foreach (var u in taMv) if (Moving(u)) { moving = true; break; }
        if (moving && Tick - ta.BoundTick < SecTicks(R.BoundMaxSec) && ta.BoundTick != 0)
        {
            foreach (var u in taAs) if (TaGroupOf(u, byId) != ta.BoundGroup && !Moving(u)) DrillHoldUnit(u);
            return;
        }
        ta.BoundGroup ^= 1;
        taMv.Clear();
        foreach (var u in taAs) if (TaGroupOf(u, byId) == ta.BoundGroup) taMv.Add(u);
        if (taMv.Count == 0) { ta.BoundGroup ^= 1; foreach (var u in taAs) if (TaGroupOf(u, byId) == ta.BoundGroup) taMv.Add(u); }
        if (taMv.Count > 0)
        {
            var mv = new List<Unit>(taMv);
            foreach (var u in taOff) mv.Add(u);
            Vector2 gc = Centroid(mv);
            float step = MathF.Max(1f, MathF.Min(R.BoundLength, TaTrenchDist(gc, tiles) - R.BoundEndDist + 1f));
            Vector2 p = DrillCover(gc + ta.Dir * step, R.CoverSearch, T);
            TaMove(mv, p, UnitOrder.Move, ta.Dir, false, FormType.Line);
            ta.BoundTick = Tick;
            Stats.DrillBounds++;
            Stats.TaBounds++;
            foreach (var u in taAs) if (!mv.Contains(u) && !Moving(u)) DrillHoldUnit(u);
            foreach (var u in taAs) if (!mv.Contains(u) && Moving(u)) DrillHoldUnit(u);
        }
    }

    float TaFracWithin(List<Unit> list, List<Vector2> tiles, float d)
    {
        if (list.Count == 0) return 0;
        int n = 0;
        foreach (var u in list) if (TaTrenchDist(u.Pos, tiles) <= d) n++;
        return n / (float)list.Count;
    }

    // ---------------------------------------------------------------- 3 돌파

    void TaBreakIn(Company c, List<Vector2> tiles)
    {
        var ta = c.TA; var R = TaR;
        var T = ta.Target;
        var g = Defs.Grenade;
        foreach (var u in taGun) if (!Moving(u)) DrillHoldUnit(u);
        if (ta.Sub == 0)
        {
            // 돌진이 멈춘 병사 (사거리 밖) 는 마저 다가가게 — 이미 안이면 그대로
            ta.Sub = 1; ta.SubTick = Tick;
        }
        long since = Tick - ta.SubTick;
        int volleyTicks = SecTicks(R.VolleyGapSec);
        int haveG = 0;
        foreach (var u in taAs) if (u.Grenades > 0) haveG++;
        if (ta.Sub != 0xFE)
        {
            // Sub 1.. = 다음 투척 차례 (Volleys 까지). 던진 뒤에는 BoundTick = 마지막 투척 틱 (신관 시간 기다림)
            if (ta.Sub <= R.Volleys && haveG > 0)
            {
                if (since >= (long)(ta.Sub - 1) * volleyTicks)
                {
                    int thrown = 0;
                    foreach (var u in taAs)
                    {
                        if (u.Grenades <= 0 || u.Charging) continue;
                        float best = float.MaxValue; Vector2 at = default; bool found = false;
                        foreach (var q in tiles)
                        {
                            float d = Vector2.Distance(u.Pos, q);
                            if (d <= g.Range - 0.3f && d < best) { best = d; at = q; found = true; }
                        }
                        if (!found) continue;
                        ThrowGrenade(u, at);
                        thrown++;
                    }
                    Stats.TaGrenades += thrown;
                    if (thrown > 0) ta.BoundTick = Tick;
                    ta.Sub++;
                }
                return;
            }
            if (ta.BoundTick != 0 && Tick - ta.BoundTick < SecTicks(R.FuseWaitSec)) return;
            TaStopFire(c);   // 돌격이 들어가면 포격은 멈춘다 (아군 오사 방지)
            taMv.Clear();
            foreach (var u in taAs) taMv.Add(u);
            foreach (var u in taOff) taMv.Add(u);
            var through = ClampToMap(T + ta.Dir * R.ChargeThrough);
            ExecCharge(c.Owner, new List<Unit>(taMv), through, null);
            taTmp.Clear(); foreach (var u in taAs) if (!u.CanCharge) taTmp.Add(u);
            if (taTmp.Count > 0) TaMove(new List<Unit>(taTmp), through, UnitOrder.AttackMove, ta.Dir, false, FormType.Line);
            ta.Sub = 0xFE; ta.SubTick = Tick; Stats.TaCharges++;
            return;
        }
        // 돌격 중: 참호에 들어갔는가
        int inside = 0;
        foreach (var u in taAs) if (u.InTrench || TaTrenchDist(u.Pos, tiles) <= 1.2f) inside++;
        long el = Tick - ta.SubTick;
        if (inside >= Math.Max(1, (int)MathF.Ceiling(taAs.Count * R.EnterFrac)) || TaEnemiesAround(c, tiles) == 0 && el > SecTicks(3f)) { TaGo(c, TaStage.Clear); return; }
        if (el > SecTicks(R.BreakMaxSec)) { if (inside >= 3 || taAs.Count >= 3 && TaEnemiesAround(c, tiles) == 0) TaGo(c, TaStage.Clear); else TaEnd(c, 2); }
    }

    /// <summary>참호 칸 · 둘레 (ClearRadius) 의 보이는 적 보병 수.</summary>
    int TaEnemiesAround(Company c, List<Vector2> tiles)
    {
        int n = 0;
        UnitsInRadius(c.TA.Target, TaR.SpanRadius + 5f, taTmp);
        foreach (var e in taTmp)
        {
            if (!e.Alive || e.Surrendered || !IsHostile(c.Owner, e.Owner) || e.Def.IsArmored || !Fog.CanSee(c.Owner, e)) continue;
            if (TaTrenchDist(e.Pos, tiles) <= TaR.ClearRadius) n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- 4 소탕

    void TaClear(Company c, List<Vector2> tiles, Vector2 endA, Vector2 endB)
    {
        var ta = c.TA; var R = TaR;
        if (ta.Sub == 0)
        {
            TaStopFire(c);
            ta.Sub = 1; ta.SubTick = Tick;
            TaSweepOrder(c, endA, endB, tiles);
            ta.ReadyTick = 0;
            return;
        }
        int enemies = TaEnemiesAround(c, tiles);
        if (enemies == 0)
        {
            if (ta.ReadyTick == 0) ta.ReadyTick = Tick;
            if (Tick - ta.ReadyTick >= SecTicks(R.ClearConfirmSec)) { TaGo(c, TaStage.Consolidate); return; }
        }
        else ta.ReadyTick = 0;
        long el = Tick - ta.StageTick;
        if (el > SecTicks(R.ClearMaxSec))
        {
            int near = 0; foreach (var u in taAs) if (TaTrenchDist(u.Pos, tiles) <= 3f) near++;
            if (near >= 3) TaGo(c, TaStage.Consolidate); else TaEnd(c, 2);
            return;
        }
        // 멈춘 소탕조는 5초마다 (적이 남아 있으면) 다시 끝으로
        if ((Tick - ta.SubTick) % SecTicks(5f) == 0 && enemies > 0)
        {
            bool idle = false; foreach (var u in taAs) if (!Moving(u) && !u.Charging && u.Order != UnitOrder.Hold) { idle = true; break; }
            if (idle) TaSweepOrder(c, endA, endB, tiles);
        }
        foreach (var u in taGun) if (!Moving(u) && u.Order != UnitOrder.Hold && !u.InTrench) DrillHoldUnit(u);
    }

    void TaSweepOrder(Company c, Vector2 endA, Vector2 endB, List<Vector2> tiles)
    {
        var ta = c.TA; var R = TaR;
        bool byId = true;
        { int a = 0, b = 0; foreach (var u in taAs) if (u.Squad != null) { if ((u.Squad.Number & 1) == 0) a++; else b++; } if (a > 0 && b > 0) byId = false; }
        Vector2 axis = endB - endA;
        Vector2 ad = axis.LengthSquared() > 1e-3f ? Vector2.Normalize(axis) : ta.Dir;
        // 가까운 끝으로 가는 쪽 (병사 평균 위치 기준) 을 0 으로 — 두 덩어리가 서로 다른 끝으로
        Vector2 ac = Centroid(taAs);
        bool aNearA = Vector2.DistanceSquared(ac, endA) <= Vector2.DistanceSquared(ac, endB);
        var near = aNearA ? endA : endB; var far = aNearA ? endB : endA;
        Vector2 nd = Vector2.Normalize(near - far == Vector2.Zero ? ad : near - far);
        for (int g = 0; g < 2; g++)
        {
            taMv.Clear();
            foreach (var u in taAs) if (TaGroupOf(u, byId) == g) taMv.Add(u);
            if (g == 0) foreach (var u in taOff) taMv.Add(u);
            if (taMv.Count == 0) continue;
            var dest = g == 0 ? near : far;
            var dd = g == 0 ? nd : -nd;
            // 한 덩어리 뿐이면 (분대가 하나) 짝수는 가까운 끝, 홀수는 먼 끝 (위 byId)
            TaMove(new List<Unit>(taMv), dest, UnitOrder.AttackMove, dd, false, FormType.Column);
        }
        // 경기관총: 참호 양 끝 바깥쪽을 잡는다 (끝 칸에서 EndHoldBack 칸 안쪽 — 참호 칸이면 자리를 맡아 들어감)
        if (taGun.Count > 0)
        {
            taTmp.Clear();
            int i = 0;
            foreach (var u in taGun)
            {
                var e = (i++ & 1) == 0 ? near : far;
                var inward = (i - 1 & 1) == 0 ? -nd : nd;
                var p = ClampToMap(e + inward * R.EndHoldBack);
                TaMove(new List<Unit> { u }, p, UnitOrder.Move, inward, true, FormType.Line);
            }
        }
    }

    // ---------------------------------------------------------------- 5 확보

    void TaConsolidate(Company c, List<Vector2> tiles)
    {
        var ta = c.TA; var R = TaR;
        if (ta.Sub == 0)
        {
            ta.Sub = 1; ta.SubTick = Tick;
            Vector2 ac = taAs.Count > 0 ? Centroid(taAs) : Centroid(c.Members);
            // 점유 중심 = 소총 무게중심에 가장 가까운 참호 칸, 반경 = 참호 폭의 절반 + 여유 (참호 자리 · 엄폐 칸)
            Vector2 centre = ac; float best = float.MaxValue; float span = 0;
            foreach (var q in tiles) { float d = Vector2.DistanceSquared(q, ac); if (d < best) { best = d; centre = q; } }
            foreach (var q in tiles) span = MathF.Max(span, Vector2.Distance(q, centre));
            float radius = Math.Clamp(MathF.Max(8f, MathF.Min(span, 30f) * 0.5f + R.HoldPad), 8f, 30f);
            taMv.Clear();
            foreach (var u in c.Members) if (u.Alive && !u.Charging) taMv.Add(u);
            ExecOccupyPositions(c.Owner, new List<Unit>(taMv), centre, ta.Dir, radius, R.CoverMaxHit);   // 공격 방향 = 점령한 참호가 이제 막을 쪽 (처음 적 쪽의 반대)
            Stats.TaConsolidations++;
            return;
        }
        if (Tick - ta.SubTick >= SecTicks(R.ConsolidateSec)) TaEnd(c, 1);
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 (ORG_ 끝, WriteProps 뒤 — FormatVersion 44)

    void WriteTrenchAssault(SnapWriter w) { foreach (var c in Companies) c.TA.Write(w); }

    void ReadTrenchAssault(SnapReader r) { foreach (var c in Companies) c.TA.Read(r); }

    void HashTrenchAssault(ref Hasher h)
    {
        foreach (var c in Companies)
        {
            var t = c.TA;
            h.Add((int)t.Stage); h.Add(t.Sub); h.Add(t.Result); h.Add(t.Flags);
            h.Add(t.Target.X); h.Add(t.Target.Y); h.Add(t.Dir.X); h.Add(t.Dir.Y); h.Add(t.FirePoint.X); h.Add(t.FirePoint.Y);
            h.Add(t.StartTick); h.Add(t.StageTick); h.Add(t.ReadyTick); h.Add(t.SubTick); h.Add(t.BoundTick); h.Add(t.CooldownUntil); h.Add(t.BoundGroup);
        }
    }
}
