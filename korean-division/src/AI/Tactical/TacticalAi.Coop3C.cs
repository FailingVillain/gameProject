using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 전술 AI 3C-1 (T34a, docs/T34_3CD_SPEC.md · TACTICAL_AI_STAGE3_REVIEW S5 · S9 일부). 모든 행동은 프로필 키 뒤, 기본 끔 — 끄면 3B2 와 같다 (겹침 "no3c").
///  · S5 제압 문턱: tactics.superioritySupp (AI 사격선 전진의 표적 둘레 우세, 0 = 옛 0.3) · boundSuppression (자기 중대 제압 상한, 0 = 옛 0.4) ·
///    chargeSuppression · retreatSuppression (기존 키 — 겹침 3c_suppthr 가 새 분포 값으로). 엔진 틀 (TacticDrill) 문턱은 tactics.json 전역 값 그대로
///    (시뮬은 AI 프로필을 읽지 않는다 — 리플레이 재생에는 AI 가 없다). 측정 도구 TacticsObs 만 겹침 값을 틀에 거울로 비춘다.
///  · pinnedTargetWeight: 엎드린 적을 가중 × 사거리 칸만큼 가깝게 쳐서 표적으로 (침투 원칙). ownPronePivot: 아군 엎드림 비율 ≥ 값이면 약진 대신 사격.
///  · S9 엄호 철수 (coverWithdraw): 후퇴 결정 때 사격 진지 점수 (엄폐 + 고지 + 병력) 높은 분대 (몫 withdrawStep) 와 기관총은 남아 coverSec 초 사격,
///    본대가 먼저 집결 지점으로. coverSec 뒤 엄호조도 이탈 (같은 집결 지점). 새 저장 상태 없음 — 표지 = TacGroup.FallbackUntil − StateTick 길이 · OrderTick.
/// </summary>
public sealed partial class TacticalAi
{
    readonly List<Unit> coverMem = new();
    readonly List<(Squad q, float score, int n)> coverSq = new();

    /// <summary>[테스트] AI 사격선 전진의 자기 중대 제압 상한 (boundSuppression, 0 = 0.4).</summary>
    public static float BoundSuppLimit(TacticsProfile T) => T.BoundSuppression > 0 ? T.BoundSuppression : 0.4f;

    /// <summary>[테스트] 아군 엎드림 전환: ownPronePivot &gt; 0 이고 엎드린 병사 ≥ 값 × 보병 수.</summary>
    public static bool PronePivot(TacticsProfile T, int prone, int n) => T.OwnPronePivot > 0 && n > 0 && prone >= T.OwnPronePivot * n;

    /// <summary>사격선 전진 (비틀 중대) 조건의 제압 부분: 자기 제압 상한 · 아군 엎드림 전환 · 표적 둘레 우세 (superioritySupp 가 있으면 그 문턱, 없으면 옛 Favourable).</summary>
    bool BoundOk3C(World w0, TacticsProfile T, TacGroup g, Unit e, float supp)
    {
        if (supp >= BoundSuppLimit(T)) return false;
        if (T.OwnPronePivot > 0)
        {
            int n = 0, p = 0;
            foreach (var u in mem) if (!u.IsOfficer && u.Def.IsInfantry) { n++; if (u.Prone) p++; }
            if (PronePivot(T, p, n)) { w0.Stats.Prone3cPivots++; return false; }
        }
        if (T.SuperioritySupp <= 0) return Favourable(g, e);
        var st = Grid.Stats(e.Pos, 12f * K);
        if (st.Count == 0) return true;
        return st.Supp / st.Count >= T.SuperioritySupp || g.Alive >= st.Count * 1.3f;
    }

    /// <summary>엎드린 적 가중 표적: pinnedTargetWeight = 0 이면 e 그대로. 장갑 표적 · 고정 제대 표적은 그대로.</summary>
    Unit? PinnedPick(World w, TacGroup g, Unit? e, TacticsProfile T, float range)
    {
        if (T.PinnedTargetWeight <= 0 || e == null || e.Def.IsArmored) return e;
        float bonus = T.PinnedTargetWeight * range;
        Vector2 c = g.Center;
        float r = Vector2.Distance(e.Pos, c) + bonus;
        var pick = Grid.BestWhere(c, r, u => u.Def.IsArmored || u.Weapon == null ? float.NaN : (u.Prone ? bonus : 0f) - Vector2.Distance(u.Pos, c)) ?? e;
        if (pick != e) w.Stats.Pinned3cRetargets++;
        return pick;
    }

    /// <summary>
    /// [테스트] 엄호 분대 고르기: 점수 내림차순 (같으면 분대 Id 오름차순) 앞 k 개, k = round(몫 × 분대 수) (1 이상, 분대 수 − 1 이하). 분대 2개 미만이면 없음.
    /// </summary>
    public static int CoverCount(float share, int squads) => squads < 2 || share <= 0 ? 0 : Math.Clamp((int)MathF.Round(share * squads), 1, squads - 1);

    /// <summary>후퇴 명령 (엄호 철수 켬이면 엄호조를 남기고 본대만). 반환 = 엄호로 남긴 병사 수 (0 = 옛 동작 — 전원 후퇴).</summary>
    int CoverRetreat(World w, TacFaction s, TacGroup g, TacticsProfile T, List<Unit> m, Vector2 rp)
    {
        int kept = CoverSplit(w, s, g, T, m);
        Order(w, s, g, m, Ord.Retreat, rp, g.TaskDir, Frontage(g));
        coverExtra = CoverExtra(T, kept);
        if (kept > 0) { m.AddRange(coverMem); coverMem.Clear(); }
        return kept;
    }

    /// <summary>엄호조를 m 에서 coverMem 으로 옮기고 사수 명령. 소총 중대 (Line) · 중대 소속만.</summary>
    int CoverSplit(World w, TacFaction s, TacGroup g, TacticsProfile T, List<Unit> m)
    {
        coverMem.Clear();
        if (!T.CoverWithdraw || T.CoverSec <= 0 || g.Kind != TacKind.Line || g.CompanyId <= 0 || w.FindCompany(g.CompanyId) is not { } co || co.Owner != s.Faction) return 0;
        coverSq.Clear();
        int maxN = 1;
        foreach (var q in co.Squads)
        {
            int n = 0; Vector2 sum = Vector2.Zero;
            foreach (var u in q.Members) if (u.Alive && !u.IsOfficer && m.Contains(u)) { n++; sum += u.Pos; }
            if (n == 0) continue;
            maxN = Math.Max(maxN, n);
            coverSq.Add((q, CoverScore(w.Map, sum / n), n));
        }
        // 사격 진지 점수 = 엄폐 + 고지 (CoverScore) + 0.5 × 병력 (가장 큰 분대 대비) — 가장 성하고 엄폐 좋은 분대가 남는다
        for (int i = 0; i < coverSq.Count; i++) { var (q, sc, n) = coverSq[i]; coverSq[i] = (q, sc + 0.5f * n / maxN, n); }
        coverSq.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : a.q.Id.CompareTo(b.q.Id));
        int k = CoverCount(T.WithdrawStep, coverSq.Count);
        for (int i = 0; i < k; i++)
            foreach (var u in coverSq[i].q.Members) if (u.Alive && !u.IsOfficer && m.Contains(u)) coverMem.Add(u);
        foreach (var u in m) if (!u.IsOfficer && u.Weapon is { Spray: > 0 } && !coverMem.Contains(u)) coverMem.Add(u);   // 기관총은 늘 엄호
        if (coverMem.Count == 0 || coverMem.Count >= m.Count) { coverMem.Clear(); return 0; }
        coverMem.Sort((a, b) => a.Id.CompareTo(b.Id));
        m.RemoveAll(u => coverMem.Contains(u));
        w.Hold(s.Faction, coverMem);
        s.Orders++; w.Stats.Cover3cWithdraws++; w.Stats.Cover3cMen += coverMem.Count;
        return coverMem.Count;
    }

    /// <summary>엄호 시간 (틱) — 후퇴 대기 FallbackUntil 에 더한다 (엄호조가 없으면 0).</summary>
    static long CoverExtra(TacticsProfile T, int kept) => kept > 0 ? Sec(T.CoverSec) : 0;
    long coverExtra;   // 같은 판단 안에서만 (CoverRetreat → FallbackUntil), 저장 아님
    long TakeCoverExtra() { long x = coverExtra; coverExtra = 0; return x; }

    /// <summary>
    /// 후퇴 대기 중 (Fallback): 엄호 철수였고 (FallbackUntil − StateTick = 15초 + coverSec) 후퇴 뒤 다른 명령이 없었으면 (OrderTick = StateTick) coverSec 지나 엄호조 이탈 —
    /// 후퇴 중이 아닌 구성원을 같은 집결 지점으로. 한 번만 (OrderTick 갱신).
    /// </summary>
    void CoverRelease(World w, TacFaction s, TacGroup g, TacticsProfile T)
    {
        if (!T.CoverWithdraw || T.CoverSec <= 0 || g.OrderTick != g.StateTick || g.FallbackUntil - g.StateTick != Sec(15) + Sec(T.CoverSec)) return;
        if (w.Tick - g.StateTick < Sec(T.CoverSec)) return;
        coverMem.Clear();
        foreach (var u in mem) if (!World.IsFleeing(u)) coverMem.Add(u);
        if (coverMem.Count == 0) { g.OrderTick = w.Tick; return; }
        retreatUseRally = true; w.Stats.Cover3cReleases++;
        Order(w, s, g, coverMem, Ord.Retreat, g.OrderPoint, g.TaskDir, Frontage(g));
        retreatUseRally = false;
        coverMem.Clear();
    }
}
