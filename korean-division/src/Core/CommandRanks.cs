using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 장교 직책 계급 · 임시 진급 · 승계 · 경험치 (balance.json "officers", docs/TACTICAL_AI_STAGE3_PLAN.md '지휘 계급 · 경험치 결정', N3).
/// 계급 번호 = ranks.json officer 색인 (1 소위 · 2 중위 · 3 대위 · 4 소령 · 5 중령 · 6 대령).
/// </summary>
public sealed class OfficerRulesDef
{
    /// <summary>중대장 직책 계급 (대위). 편성 때 중대의 첫 장교 (계급 미정) 가 받는다.</summary>
    public int CompanyRank { get; set; } = 3;
    /// <summary>중대의 둘째 장교 (중위) · 그 밖 (소위) — 중대 전투 보조.</summary>
    public int SecondRank { get; set; } = 2;
    public int JuniorRank { get; set; } = 1;
    /// <summary>대대장 직책 계급: 중대 battalionSeniorCompanies 개 이상이면 battalionSeniorRank (중령), 아니면 battalionRank (소령).</summary>
    public int BattalionRank { get; set; } = 4;
    public int BattalionSeniorRank { get; set; } = 5;
    public int BattalionSeniorCompanies { get; set; } = 3;
    /// <summary>계급 상한 (대령).</summary>
    public int MaxRank { get; set; } = 6;
    /// <summary>진급으로 오를 수 있는 최고 계급 (중령 = 5). 임시 진급 · 대대장 정식 진급 모두 이 위로는 못 올린다. 미션이 직접 배치한 더 높은 계급 (연대장 대령) 은 그대로 둔다 — 상한은 진급에만 적용.</summary>
    public int MaxPromotionRank { get; set; } = 5;
    /// <summary>전투 중 임시 진급 상한 (정식 계급 + 최대 3).</summary>
    public int MaxTempPromotion { get; set; } = 3;
    /// <summary>지휘관을 잃은 뒤 다음 서열 장교가 지휘를 넘겨받기까지 (초, 경험치 단계마다 × (1 − xpHandoverPerLevel)).</summary>
    public float HandoverSec { get; set; } = 5f;
    /// <summary>장교가 없어 부사관이 대리 지휘를 맡기까지 (초).</summary>
    public float NcoHandoverSec { get; set; } = 8f;
    /// <summary>T24 ① (D08): 부사관 분대장이 하나도 없으면 사병 계급이 이 값 이상인 중대원 중 최고 계급 (분대장 아니어도) 이 대리 지휘. −1 = 끔 (분대장 부사관만).</summary>
    public int ActingMinRank { get; set; } = -1;
    /// <summary>경험치 단계 문턱 (오름차순). 단계 = 넘은 문턱 수.</summary>
    public int[] XpLevels { get; set; } = { 60, 180, 420, 900 };
    /// <summary>중대가 교전 중인 1초마다 그 중대 장교 경험치 +.</summary>
    public int XpCombatPerSec { get; set; } = 1;
    /// <summary>중대원이 적 유닛을 죽일 때마다 그 중대 장교 경험치 + · 대대장 (다른 중대 소속이면) +.</summary>
    public int XpPerKill { get; set; } = 2;
    public int XpPerKillBattalion { get; set; } = 1;
    /// <summary>교전 판정: 구성원이 표적을 잡았거나 이 초 안에 사격.</summary>
    public float CombatWindowSec { get; set; } = 3f;
    /// <summary>경험치 단계마다: 지휘 판단 간격 (행진 사격 · 빈자리 점검) × (1 − 값), 지휘 계통 지연 × (1 − 값), 인계 시간 × (1 − 값). 배수 하한 0.5.</summary>
    public float XpDecidePerLevel { get; set; } = 0.05f;
    public float XpDelayPerLevel { get; set; } = 0.06f;
    public float XpHandoverPerLevel { get; set; } = 0.1f;
    public int XpMax { get; set; } = 1_000_000;
    /// <summary>장교 경험 성향 (N55, OfficerStyle.cs): 돌격 사격 대 교대 약진 · 기병 정면 돌격.</summary>
    public OfficerStyleDef Style { get; set; } = new();
}

/// <summary>중대 지휘 능력 단계 (N4 전술 틀 전환이 쓴다): None = 지휘 없음 · 인계 중 (기본 대형만) · Nco = 부사관 대리 (단순 틀만) · Officer = 장교 (전부).</summary>
public enum CommandLevel : byte { None = 0, Nco = 1, Officer = 2 }

/// <summary>배치 단계 편성 방식: None = 전투 중 (임시 진급 · 인계) · Auto = 직책 계급 자동 (대대장 정식 진급) · Explicit = 미션이 계급 지정 (진급 없음).</summary>
public enum SpawnRanks : byte { None = 0, Auto = 1, Explicit = 2 }

/// <summary>[표시] 장교 직책.</summary>
public enum OfficerPost : byte { None = 0, Staff = 1, Company = 2, Battalion = 3, Assistant = 4 }

public sealed partial class Unit
{
    /// <summary>[장교] 정식 계급 (0 = 미정 '장교', 1 소위 … 6 대령). 배치 · 편성 때 직책으로 정한다 (미션이 지정 가능). 정식 진급은 캠페인.</summary>
    [SnapExt] public byte OfficerBase { get; internal set; }
    /// <summary>[장교] 전투 중 임시 진급 단계 (0..officers.maxTempPromotion) — 지휘관 승계 · 상위 직책을 맡을 때.</summary>
    [SnapExt] public byte TempRank { get; internal set; }
    /// <summary>[장교] 경험치 (교전 시간 · 소속 중대 사살 · 목표). 저장 · 결과 파일 · 캠페인 계승.</summary>
    [SnapExt] public int OfficerXp { get; internal set; }
}

public sealed partial class World
{
    OfficerRulesDef OffRules => Defs.Officers;

    // ---------------------------------------------------------------- 조회 (UI · AI · N4)

    /// <summary>장교 경험치 단계 (0 = 문턱 아래). 장교가 아니거나 null 이면 0.</summary>
    public int XpLevel(Unit? u)
    {
        if (u == null || !u.IsOfficer) return 0;
        int l = 0;
        foreach (int t in OffRules.XpLevels) if (u.OfficerXp >= t) l++;
        return l;
    }

    /// <summary>중대 지휘자: 장교 지휘관, 없으면 대리 부사관 (분대장 중 최고 계급). 둘 다 없으면 null. 인계 중에도 다음 지휘자를 돌려준다 (능력은 CommandLevelOf).</summary>
    public Unit? CommanderOf(Company c) => c.Commander ?? ActingNco(c);

    /// <summary>대대장 (없으면 null).</summary>
    public Unit? CommanderOf(Battalion b) => b.Commander;

    /// <summary>장교가 없는 중대의 대리 지휘 부사관: 공식 부사관인 분대장 중 최고 계급 (같으면 Id 작은 쪽). 없으면 officers.actingMinRank ≥ 0 일 때 그 계급 이상 중대원 중 최고 계급 (T24 ①).</summary>
    public Unit? ActingNco(Company c)
    {
        Unit? best = null;
        foreach (var q in c.Squads)
            if (q.Leader is { IsNco: true } l && (best == null || l.Rank > best.Rank || (l.Rank == best.Rank && l.Id < best.Id))) best = l;
        int min = OffRules.ActingMinRank;
        if (best != null || min < 0) return best;
        // T24 ①: 부사관 분대장이 없으면 분대장 아닌 최고 계급 보병 (중화기 · 정찰 · 기병 제외, 같으면 Id 작은 쪽)
        foreach (var m in c.Members)
            if (m.Alive && !m.IsOfficer && m.Def.IsInfantry && !m.Def.Heavy && !m.Def.Scout && m.Company == c && m.Rank >= min && (best == null || m.Rank > best.Rank || (m.Rank == best.Rank && m.Id < best.Id))) best = m;
        return best;
    }

    /// <summary>지휘관 인계 중인가 (지휘관을 잃은 뒤 handoverSec).</summary>
    public bool InHandover(Company c) => Tick < c.HandoverUntil;
    public bool InHandover(Battalion b) => Tick < b.HandoverUntil;

    /// <summary>중대 지휘 능력: 인계 중 None, 장교 지휘관 Officer, 대리 부사관 Nco, 아무도 없으면 None.</summary>
    public CommandLevel CommandLevelOf(Company c)
    {
        if (Tick < c.HandoverUntil) return CommandLevel.None;
        if (c.Commander != null) return CommandLevel.Officer;
        return ActingNco(c) != null ? CommandLevel.Nco : CommandLevel.None;
    }

    public CommandLevel CommandLevelOf(Battalion b) => Tick < b.HandoverUntil || b.Commander == null ? CommandLevel.None : CommandLevel.Officer;

    /// <summary>이 지휘 능력으로 쓸 수 있는 전술 틀: 장교 = 전부 · 부사관 = 틀 없음 (대형 유지) · 화력-기동만 · 지휘 없음 = 틀 없음만.</summary>
    public static bool TacticAllowed(CommandLevel level, CompanyTactic t) => level switch
    {
        CommandLevel.Officer => true,
        CommandLevel.Nco => t is CompanyTactic.None or CompanyTactic.FireManoeuvre,
        _ => t == CompanyTactic.None,
    };

    public bool TacticAllowed(Company c, CompanyTactic t) => TacticAllowed(CommandLevelOf(c), t);

    /// <summary>[표시] 장교의 직책 (대대장 · 중대장 · 중대 보조 · 없음).</summary>
    public OfficerPost PostOf(Unit u)
    {
        if (!u.IsOfficer || u.Company is not { } c) return OfficerPost.None;
        if (c.Battalion?.Commander == u) return OfficerPost.Battalion;
        if (c.Battalion is { } pb && AssistantOf(pb) == u) return OfficerPost.Assistant;
        return c.Commander == u ? OfficerPost.Company : OfficerPost.Staff;
    }

    /// <summary>경험치 단계 배수 1 − perLevel × 단계 (하한 0.5).</summary>
    float XpMul(Unit? u, float perLevel) => MathF.Max(0.5f, 1f - perLevel * XpLevel(u));

    /// <summary>지휘 판단 간격 (틱): 장교가 지휘하는 중대는 지휘관 경험치로 짧아진다.</summary>
    internal int DecideInterval(Company c, int iv) =>
        CommandLevelOf(c) == CommandLevel.Officer ? Math.Max(1, (int)MathF.Round(iv * XpMul(c.Commander, OffRules.XpDecidePerLevel))) : iv;

    /// <summary>지휘 계통 지연 배수: 장교가 지휘하는 중대원은 지휘관 경험치로 줄어든다.</summary>
    internal float ChainDelayMul(Unit u) =>
        u.Company is { } c && CommandLevelOf(c) == CommandLevel.Officer ? XpMul(c.Commander, OffRules.XpDelayPerLevel) : 1f;

    // ---------------------------------------------------------------- 직책 계급 (편성 · 배치)

    /// <summary>지휘 우선순위 (전투 중 편성 · 승계): 경험치 높은 쪽 → 계급 높은 쪽 → Id 작은 쪽. 계급은 올리지 않고 지휘권만 준다.</summary>
    static bool Senior(Unit a, Unit b) =>
        a.OfficerXp != b.OfficerXp ? a.OfficerXp > b.OfficerXp : a.OfficerRank != b.OfficerRank ? a.OfficerRank > b.OfficerRank : a.Id < b.Id;

    /// <summary>대대 부대대장 (파생, 저장 없음): 중대 3개 이상 (battalionSeniorCompanies) 인 대대에서 대대장 다음 우선순위 장교. 아니면 null.</summary>
    public Unit? AssistantOf(Battalion b)
    {
        if (b.Commander == null || b.Companies.Count < OffRules.BattalionSeniorCompanies) return null;
        Unit? best = null;
        foreach (var c in b.Companies)
            foreach (var m in c.Members)
                if (m.Alive && m.IsOfficer && m != b.Commander && (best == null || Senior(m, best))) best = m;
        return best;
    }

    int BattalionPostRank(Battalion b) => b.Companies.Count >= OffRules.BattalionSeniorCompanies ? OffRules.BattalionSeniorRank : OffRules.BattalionRank;

    /// <summary>계급 미정 장교 (OfficerBase 0) 에게 중대 직책 계급: 첫 장교 대위 (이미 대위 이상이 있으면 건너뜀) · 둘째 중위 · 나머지 소위 (Id 순).</summary>
    void AssignCompanyRanks(Company c)
    {
        var r = OffRules;
        bool captain = false, second = false;
        foreach (var u in c.Members)
            if (u.IsOfficer && u.OfficerBase > 0) { captain |= u.OfficerRank >= r.CompanyRank; second |= u.OfficerRank == r.SecondRank; }
        foreach (var u in c.Members)
        {
            if (!u.IsOfficer || u.OfficerBase > 0) continue;
            int rank = !captain ? r.CompanyRank : !second ? r.SecondRank : r.JuniorRank;
            if (rank == r.CompanyRank) captain = true; else if (rank == r.SecondRank) second = true;
            u.OfficerBase = (byte)Math.Clamp(rank, 1, r.MaxRank);
        }
    }

    /// <summary>직책 계급까지 임시 진급 (정식 계급 + 최대 maxTempPromotion, 상한 maxRank). 이미 높으면 그대로.</summary>
    void TempPromote(Unit u, int post)
    {
        var r = OffRules;
        int need = Math.Min(Math.Min(post - u.OfficerBase, r.MaxTempPromotion), Math.Min(r.MaxRank, r.MaxPromotionRank) - u.OfficerBase);
        if (need > u.TempRank) u.TempRank = (byte)need;
    }

    /// <summary>배치 단계 (시작 병력 · 미션 병력): 인계 지연 없음. auto 면 대대장을 직책 계급으로 정식 진급 (임시 진급 없이), 아니면 (미션 지정 계급) 이번에 편성한 장교의 임시 진급을 지운다.</summary>
    void ConfirmSpawnPosts(Company c, bool auto)
    {
        c.HandoverUntil = 0;
        if (!auto) foreach (var u in c.Members) u.TempRank = 0;
        if (c.Battalion is not { } b) return;   // (이미 있던 대대 · 중대의 인계는 건드리지 않는다 — 증원이 기존 대대에 들어갈 때)
        if (auto && AssistantOf(b) is { } asst && asst.OfficerRank < Math.Min(OffRules.BattalionRank, OffRules.MaxPromotionRank))
        {
            asst.OfficerBase = (byte)Math.Clamp(Math.Min(OffRules.BattalionRank, OffRules.MaxPromotionRank), 1, OffRules.MaxRank);   // 부대대장 소령 (정식)
            asst.TempRank = 0;
        }
        if (auto && b.Commander is { } bc)
        {
            int post = Math.Min(BattalionPostRank(b), OffRules.MaxPromotionRank);
            int eff = bc.OfficerRank;
            if (eff < post) eff = post;
            bc.OfficerBase = (byte)Math.Clamp(eff, 1, OffRules.MaxRank);
            bc.TempRank = 0;
        }
    }

    // ---------------------------------------------------------------- 지휘관 · 승계

    bool ValidCompanyCommander(Company c, Unit u)
    {
        if (!u.Alive || u.Company != c || !u.IsOfficer) return false;
        if (c.Battalion?.Commander == u)   // 대대장은 중대에 다른 장교가 있으면 중대 지휘를 넘긴다
            foreach (var m in c.Members) if (m != u && m.Alive && m.IsOfficer) return false;
        return true;
    }

    Unit? BestCompanyOfficer(Company c)
    {
        Unit? best = null, bat = c.Battalion?.Commander, batHere = null;
        foreach (var m in c.Members)
        {
            if (!m.Alive || !m.IsOfficer) continue;
            if (m == bat) { batHere = m; continue; }
            if (best == null || Senior(m, best)) best = m;
        }
        return best ?? batHere;
    }

    internal int HandoverTicks(Unit? next)
    {
        var r = OffRules;
        float sec = next != null ? r.HandoverSec * XpMul(next, r.XpHandoverPerLevel) : r.NcoHandoverSec;
        return Math.Max(0, (int)MathF.Round(sec / Config.SimDt));
    }

    /// <summary>
    /// 중대 지휘관 확인: 지금 지휘관이 유효하면 그대로. 아니면 다음 서열 장교 (대대장은 다른 장교가 없을 때만) 가 맡고 중대장 직책 계급까지 임시 진급.
    /// 지휘관을 잃은 경우 (전사 · 이탈) 인계 지연 — 장교 handoverSec (경험치로 단축) · 장교가 없으면 부사관 대리 ncoHandoverSec. 처음 정할 때는 즉시.
    /// </summary>
    void ResolveCompanyCommand(Company c)
    {
        var cur = c.Commander;
        if (cur != null && ValidCompanyCommander(c, cur)) return;
        var next = BestCompanyOfficer(c);
        c.Commander = next;
        if (cur != null && next != null) TempPromote(next, OffRules.CompanyRank);   // 임시 진급은 승계 때만
        if (cur != null) c.HandoverUntil = Tick + HandoverTicks(next);
    }

    /// <summary>대대장 확인 (유효하면 그대로, 아니면 대대 안 최고 서열 장교 + 대대장 직책 계급까지 임시 진급, 잃은 경우 인계 지연) 뒤 소속 중대 지휘관도 확인.</summary>
    void ResolveBattalionCommand(Battalion b)
    {
        var cur = b.Commander;
        if (cur == null || !cur.Alive || !cur.IsOfficer || cur.Company?.Battalion != b)
        {
            Unit? best = null;
            foreach (var c in b.Companies)
                foreach (var m in c.Members)
                    if (m.Alive && m.IsOfficer && (best == null || Senior(m, best))) best = m;
            b.Commander = best;
            if (cur != null)
            {
                if (best != null) TempPromote(best, BattalionPostRank(b));   // 임시 진급은 승계 때만
                b.HandoverUntil = Tick + HandoverTicks(best);
            }
        }
        foreach (var c in b.Companies) ResolveCompanyCommand(c);
    }

    /// <summary>LeaveCompany 가 구성원을 뺀 뒤: 그 유닛이 지휘관 · 대대장이었으면 승계.</summary>
    void OnCommandMemberLeft(Unit u, Company c, Battalion? b)
    {
        if (b != null && b.Commander == u && Battalions.Contains(b)) { ResolveBattalionCommand(b); return; }
        if (c.Commander == u && Companies.Contains(c)) ResolveCompanyCommand(c);
    }

    // ---------------------------------------------------------------- 매 틱 (Step: StepAnchors 뒤, StepGapChecks 앞)

    void StepCommand()
    {
        if (Companies.Count == 0) return;
        foreach (var b in Battalions)
            if (b.Commander is { } bc && (!bc.Alive || bc.Company?.Battalion != b)) ResolveBattalionCommand(b);
        int per = Math.Max(1, (int)MathF.Round(1f / Config.SimDt));
        long window = Math.Max(1, (long)MathF.Round(OffRules.CombatWindowSec / Config.SimDt));
        for (int i = 0; i < Companies.Count; i++)
        {
            var c = Companies[i];
            if (c.Commander is { } cm && !ValidCompanyCommander(c, cm)) ResolveCompanyCommand(c);   // 안전망 (보통은 LeaveCompany 가 처리)
            if ((Tick + c.Id * 13L) % per != 0 || OffRules.XpCombatPerSec <= 0) continue;
            bool officer = false, combat = false;
            foreach (var u in c.Members)
            {
                if (!u.Alive) continue;
                officer |= u.IsOfficer;
                combat |= u.Target != null || (u.LastShotTick > 0 && Tick - u.LastShotTick < window);
            }
            if (!officer || !combat) continue;
            foreach (var u in c.Members) if (u.Alive && u.IsOfficer) AddXp(u, OffRules.XpCombatPerSec);
        }
    }

    void AddXp(Unit u, int xp)
    {
        if (xp <= 0 || !u.IsOfficer) return;
        u.OfficerXp = (int)Math.Min(OffRules.XpMax, (long)u.OfficerXp + xp);
    }

    /// <summary>적 유닛을 죽였을 때 (CountKill): 사격자 중대의 장교 전원 + 대대장 (다른 중대면).</summary>
    internal void CreditKillXp(Unit killer)
    {
        if (killer.Company is not { } c) return;
        var r = OffRules;
        foreach (var u in c.Members) if (u.Alive && u.IsOfficer) AddXp(u, r.XpPerKill);
        if (c.Battalion?.Commander is { } bc && bc.Alive && bc.Company != c) AddXp(bc, r.XpPerKillBattalion);
    }

    // ---------------------------------------------------------------- 저장 (ORG_ 끝, FormatVersion 23) · 체크섬

    void WriteCommandState(SnapWriter w)
    {
        foreach (var b in Battalions) { w.I(b.Commander?.Id ?? 0); w.L(b.HandoverUntil); }
        foreach (var c in Companies) { w.I(c.Commander?.Id ?? 0); w.L(c.HandoverUntil); }
    }

    void ReadCommandState(SnapReader r)
    {
        foreach (var b in Battalions)
        {
            int id = r.I();
            b.HandoverUntil = r.L();
            if (id == 0) { b.Commander = null; continue; }
            if (FindAny(id) is not Unit u || !u.IsOfficer || u.Company?.Battalion != b) throw new SnapshotException(string.Format(SnapMsg.Invalid, "battalion commander " + id));
            b.Commander = u;
        }
        foreach (var c in Companies)
        {
            int id = r.I();
            c.HandoverUntil = r.L();
            if (id == 0) { c.Commander = null; continue; }
            if (FindAny(id) is not Unit u || !u.IsOfficer || u.Company != c) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company commander " + id));
            c.Commander = u;
        }
    }

    void HashCommandState(ref Hasher h)
    {
        foreach (var b in Battalions) { h.Add(b.Commander?.Id ?? 0); h.Add(b.HandoverUntil); }
        foreach (var c in Companies) { h.Add(c.Commander?.Id ?? 0); h.Add(c.HandoverUntil); }
    }
}
