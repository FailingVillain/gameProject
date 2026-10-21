using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 역습 교리 수치 (assets/data/tactics.json "counter", 사용자 결정 2026-10-19). 밸런스 수치 아님 — AI 판단 문턱만. 없으면 내장 기본값.
/// </summary>
public sealed class CounterAiDef
{
    /// <summary>false = 역습도 보통 공격과 같게 (옛 동작).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>접촉하면 예비 (대대 예비 중대 · 연대 예비 대대) 를 곧바로 투입 (역습은 한 번에 친다).</summary>
    public bool CommitReserves { get; set; } = true;
    /// <summary>고정 사격 (전 중대가 사격 진지에서 사격) 최소 · 최대 (초). 최소가 지나고 표적 둘레 적 평균 제압 ≥ assaultSupp 이면, 최대면 무조건 돌격 (병력비 조건은 따로).</summary>
    public float FixMinSec { get; set; } = 15f;
    public float FixMaxSec { get; set; } = 35f;
    public float AssaultSupp { get; set; } = 0.15f;
    /// <summary>돌격 국면 병력비: 연대 앞 소총 중대 병력 ≥ 표적 둘레 (소총 사거리) 의 보이는 적 × 이 값일 때만 (아니면 계속 사격 — 약한 역습은 진지를 못 뺏는다).</summary>
    public float MinOdds { get; set; } = 1.2f;
    /// <summary>돌격 중대: 돌격 거리 × closeFrac 까지 속보로 다가간 뒤 착검 돌격.</summary>
    public float CloseFrac { get; set; } = 0.85f;
    /// <summary>역습 돌격 조건 (보통 tactics.chargeSuppression · chargeOdds 대신): 표적 둘레 적 평균 제압 ≥ 이 값, 또는 돌격 인원 ≥ 적 × chargeOdds.</summary>
    public float ChargeSuppression { get; set; } = 0.2f;
    public float ChargeOdds { get; set; } = 1.0f;
    /// <summary>돌격 허가 (결정 4) 예비 타이머: 돌격 국면 시작에서 이 초 뒤 (보통 behaviour.assaultFallbackSec 150 대신).</summary>
    public float AssaultFallbackSec { get; set; } = 15f;
    /// <summary>돌격 국면 시작부터 이 초 동안 중대 스스로 후퇴 (병력 · 제압 임계) 를 미룬다.</summary>
    public float NoRetreatSec { get; set; } = 25f;
    /// <summary>돌격 국면 최대 (초) — 지나면 고정 사격으로 돌아가 다시 (fixMinSec 뒤) 돌격.</summary>
    public float AssaultMaxSec { get; set; } = 45f;
    /// <summary>화력 기반 중대 비율 (반올림, 중대 2개 이상이면 최소 1): 돌격하지 않고 엄호 사격.</summary>
    public float FireBaseFrac { get; set; } = 0.34f;
}

/// <summary>
/// 역습 교리 (사용자 결정 2026-10-19 — docs/BATTALION_CAMPAIGN_2026-10-19.md '도하 · 역습 (crossing)').
/// 대상: 미션 posture "counter" (AiPosture.Counter) 지시 그룹 · 방어 태세의 세력 반격 (TacDirective.CounterUntil 동안).
/// 교착 원인 (CTR_*): 반격 중대가 사거리 끝 사격 진지에서 참호 속 적과 소모전만 하고 (사격선 전진은 1.3배 우세 · 적 제압 0.3 이 있어야, 돌격 허가 예비 타이머 150초),
/// 중대 하나씩 후퇴 → 다시 접근 (축차 투입) 하다 연대가 무너진다. 역습 교리 (연대 층, 결정적):
///  1 고정 사격: 접촉하면 예비를 투입하고 전 중대가 사격 진지에서 사격 (fixMinSec ~ fixMaxSec, 적이 제압되면 일찍).
///  2 일제 돌격: 병력비 (minOdds) 가 되면 화력 기반 (fireBaseFrac) 을 뺀 모든 중대가 같은 순간 속보로 돌격 거리까지 다가가 착검 돌격
///    (역습 돌격 조건 · 돌격 허가 예비 타이머 짧게, noRetreatSec 동안 스스로 후퇴 미룸). 화력 기반 · 포병은 계속 엄호 (연신은 기존 규칙).
///  3 assaultMaxSec 뒤 (또는 돌격 중대가 모두 후퇴 · 전멸) 다시 1.
/// 상태: TacRegiment.CounterStage · CounterStageTick, TacGroup.CounterAssaultTick (스냅샷 — ObjCodec).
/// </summary>
public sealed partial class TacticalAi
{
    static CounterAiDef Ctr(World w) => w.Defs.Tactics.Counter;

    /// <summary>연대가 역습 교리를 쓰나: 지시 그룹 태세가 역습이거나 방어 그룹의 세력 반격 중.</summary>
    static bool CounterDoctrine(World w, TacFaction s, TacRegiment r)
    {
        foreach (var d in s.Directives)
            if (d.Id == r.Directive) return d.Posture == AiPosture.Counter || w.Tick < d.CounterUntil;
        return false;
    }

    void ResetCounter(TacFaction s, TacRegiment r)
    {
        if (r.CounterStage == 0) return;
        r.CounterStage = 0; r.CounterStageTick = 0;
        foreach (var g in s.Groups) if (g.CounterAssaultTick != 0 && RegimentOfGroup(s, g) == r.Id) { g.CounterAssaultTick = 0; g.OrderTick = -100000; }
    }

    void CounterThink(World w, TacFaction s, TacRegiment r, List<TacBattalion> fronts, Vector2 fc, Unit? e, float R, bool arty)
    {
        var C = Ctr(w);
        if (!C.Enabled || !CounterDoctrine(w, s, r)) { ResetCounter(s, r); return; }
        var lines = crossBuf;
        CrossCandidates(s, r, lines);
        if (r.CounterStage == 0)
        {
            if (e == null || Vector2.Distance(e.Pos, fc) > R * 2f) return;
            r.CounterStage = 1; r.CounterStageTick = w.Tick;
            if (C.CommitReserves)
            {
                if (r.ReserveBattalion != 0) s.ReserveCommits++;
                r.ReserveCommitted = true; r.ReserveBattalion = 0;
                foreach (var b in s.Battalions) if (b.Regiment == r.Id && !b.ReserveCommitted) { b.ReserveCommitted = true; b.ReserveGroup = 0; }
            }
            return;
        }
        long el = w.Tick - r.CounterStageTick;
        if (r.CounterStage == 1)
        {
            if (e == null || el < Sec(C.FixMinSec)) return;
            int ours = 0;
            foreach (var g in lines) if (g.Task == TacTask.Attack && g.State != TacState.Fallback) ours += g.Alive;
            var st = Grid.Stats(e.Pos, R);
            if (st.Count == 0 || ours < st.Count * C.MinOdds) return;
            if (st.Supp / st.Count < C.AssaultSupp && el < Sec(C.FixMaxSec)) return;
            // 일제 돌격: 화력 기반 (사격 진지 점수 = 지금 역할이 화력 기반인 중대 우선, 그다음 적에서 먼 중대) 을 뺀 전 중대
            var cand = lines.Where(g => g.Task == TacTask.Attack && g.State != TacState.Fallback).ToList();
            if (cand.Count == 0) return;
            int k = cand.Count >= 2 ? Math.Clamp((int)MathF.Round(cand.Count * C.FireBaseFrac), 1, cand.Count - 1) : 0;
            var basis = cand.OrderByDescending(g => g.Role == TacRole.BaseOfFire ? 1 : 0).ThenByDescending(g => Vector2.Distance(g.Center, e.Pos)).ThenBy(g => g.Id).Take(k).ToList();
            foreach (var g in cand)
            {
                if (basis.Contains(g)) continue;
                g.CounterAssaultTick = w.Tick;
                g.NoRetreatUntil = Math.Max(g.NoRetreatUntil, w.Tick + Sec(C.NoRetreatSec));
                g.OrderTick = -100000; g.SquadBound = 0;
                if (g.State != TacState.Assault) g.State = TacState.Idle;
            }
            r.CounterStage = 2; r.CounterStageTick = w.Tick; s.CounterAssaults++;
            return;
        }
        // 돌격 국면: 돌격 중대가 모두 후퇴 · 전멸했거나 최대 시간이면 다시 고정 사격
        bool any = false;
        foreach (var g in lines) if (g.CounterAssaultTick != 0 && g.State != TacState.Fallback && g.Task == TacTask.Attack) any = true;
        if (!any || el >= Sec(C.AssaultMaxSec))
        {
            foreach (var g in s.Groups) if (g.CounterAssaultTick != 0 && RegimentOfGroup(s, g) == r.Id) { g.CounterAssaultTick = 0; g.OrderTick = -100000; }
            r.CounterStage = 1; r.CounterStageTick = w.Tick;
        }
    }

    /// <summary>역습 돌격 중대 (LineThink 공격, 표적 e · 거리 d): 돌격 거리 밖이면 속보로 다가가고, 안이면 착검 돌격 (안 되면 서서 사격).</summary>
    void CounterAssault(World w, TacFaction s, TacGroup g, Unit e, float d, float supp)
    {
        var C = Ctr(w);
        float cr = s.Profile.Tactics.ChargeRange * K;
        if (d <= cr)
        {
            if (TryAssault(w, s, g, e, d, supp, attack: true, counter: true)) return;
            if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
            return;
        }
        Vector2 toE = SafeNorm(e.Pos - g.Center, g.TaskDir);
        Vector2 p = BestCover(w.Map, e.Pos - toE * (cr * C.CloseFrac), 2, e.Pos);
        if (g.State != TacState.Bound || (Due(w, g, 6f) && Vector2.Distance(p, g.OrderPoint) > 4f) || Due(w, g, 20f))
        {
            Order(w, s, g, mem, Ord.Quick, p, toE, g.TaskWidth);
            g.State = TacState.Bound; g.StateTick = w.Tick; g.BoundDist = cr * C.CloseFrac;
        }
    }
}
