using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>대열 정비 (플레이어 명령, 단축키 L): 고른 유닛의 중대들이 곧장 빈자리를 메우고 자리를 다시 잡는다 (교전 중에도). 리플레이 기록.</summary>
public sealed record CloseRanksCommand(int Faction, int[] Companies) : GameCommand(Faction);

/// <summary>대열 정비 명령에 따르는 병사 숙련 (사병 계급 기준, balance.json combat.closeRegularRank · closeVeteranRank).</summary>
public enum RankSkill { Recruit = 0, Regular = 1, Veteran = 2 }

/// <summary>
/// [측정 · 테스트] 중대 대열의 흐트러짐. 장교 판단 (gapfill2) = 앞줄 구멍 Holes · 가장 넓은 연속 구멍 HoleGap · 뒤처진 앞줄 병사 Behind (+ 보조 DispFrac).
/// FrontEmpty · FrontGap = 옛 (gapfill 1차) 엄격한 앞줄 빈 슬롯 — 측정 비교용. FrontSlots = 구멍을 센 앞줄 슬롯 수.
/// </summary>
public readonly record struct RankDistortion(float DispFrac, int FrontEmpty, int FrontGap, int Men, int Holes = 0, int HoleGap = 0, int Behind = 0, int FrontSlots = 0);

/// <summary>
/// 대열 정비 (gapfill, 2026-10-19 사용자 결정). N12 이후 교전 중에는 빈자리 점검이 대열을 메우지 않는다 — 대신 명령으로:
///  ① 플레이어 '대열 정비' (CloseRanksCommand) — 고른 중대가 곧장 다시 자리를 잡는다.
///  ② 장교 판단 — 중대 지휘관 (승계한 장교 포함, 장교가 없으면 대리 부사관) 이 closeCheckSec 마다 (DecideInterval — 경험치) 교전 중인 중대의 대열을 재고
///     앞줄에 구멍이 났으면 (gapfill2: MeasureRanks 의 앞줄 구멍 · 연속 구멍 · 뒤처진 앞줄 병사, 보조로 벗어난 병사 비율) 대열 정비를 명령한다. 히스테리시스: 경보 (CloseAlarm) 는
///     모든 지표가 문턱 × closeHysteresis 아래로 내려가야 꺼지고, 경보 중 재명령은 closeCooldownSec 간격. 적 돌격 (대도 · 착검 · 기병) 이 closeChargeRange 안에서 다가오면
///     점검 간격을 건너뛰고 바로 판단 (문턱 × closeChargeThrMul, 재명령 간격은 지킴).
///     gapfill2: 틀 행동 (교전 국면) 중 · 도착한 깃발이 있어도 판단한다 (교전 중 중대-초의 약 80% 가 틀 행동 중이었다).
///  ③ 병사 숙련 — 명령이 유효한 동안 (closeWindowSec, closePassSec 마다 다시 살핌) 슬롯에서 closeSlack 넘게 벗어난 병사를 보낸다 (gapfill2: 대기 · 사수 · 공격 이동 병사 —
///     공격 이동 병사는 공격 이동 그대로 슬롯까지 걸어가며 행진 사격, 돌격 · 참호 · 중화기 승무원 · 틀의 기동조 · 예비 · 지휘조 · 약진 중인 분대 · 일반 이동 중은 제외). 교전 중 (표적 · regroupFireSec 안 사격) 인 병사는
///     숙련별 closeObey 확률로 곧장 따르고, 아니면 closeDelaySec 뒤 (또는 교전이 끝나면) 따른다 — 신병은 사격 중에도 곧장, 노병은 싸우기를 먼저.
///     확률은 (병사 Id, 명령 틱, 지도 시드) 해시 — 상태 없음. 참호 안 · 참호 자리 병사는 움직이지 않는다.
/// 상태 = 중대마다 CloseAlarm · CloseAt · CloseUntil + 깃발 Close 표시 (ORG_ 끝, FormatVersion 28). 그 밖은 위치 · 틱의 함수.
/// </summary>
public sealed partial class World
{
    // ---------------------------------------------------------------- 공개 API

    /// <summary>units 가 속한 자기 중대들에 대열 정비 명령 (중대가 없으면 명령 없음). 분대만 골라도 그 중대 전체.</summary>
    public void CloseRanks(int faction, IEnumerable<Unit> units)
    {
        var ids = units.Where(u => u.Alive && u.Owner == faction && u.Company != null && u.Company.Owner == faction)
            .Select(u => u.Company!.Id).Distinct().OrderBy(i => i).ToArray();
        if (ids.Length > 0) Issue(new CloseRanksCommand(faction, ids));
    }

    /// <summary>병사 숙련: 사병 계급 &lt; closeRegularRank = 신병, &lt; closeVeteranRank = 정규, 이상 = 노병. 장교는 신병 취급 (곧장 — 명령하는 쪽).</summary>
    public RankSkill SkillOf(Unit u)
    {
        if (u.IsOfficer) return RankSkill.Recruit;
        var cr = Defs.CombatRules;
        int r = u.Rank;
        return r >= cr.CloseVeteranRank ? RankSkill.Veteran : r >= cr.CloseRegularRank ? RankSkill.Regular : RankSkill.Recruit;
    }

    /// <summary>[조회] 대열 정비 명령이 유효한가 (늦게 따르는 병사를 아직 보내는 중).</summary>
    public bool ClosingRanks(Company c) => c.CloseUntil != 0 && Tick <= c.CloseUntil;

    // ---------------------------------------------------------------- 실행

    void ExecCloseRanks(int f, int[] companies)
    {
        foreach (int id in companies)
            if (OwnCompany(f, id) is { } c)
            {
                Stats.CloseManual++;
                OrderCloseRanks(c);
            }
    }

    /// <summary>대열 정비 명령 (플레이어 · 장교): 지금부터 closeWindowSec 동안 유효, 첫 정비는 바로.</summary>
    void OrderCloseRanks(Company c)
    {
        c.CloseAt = Math.Max(1, Tick);
        c.CloseUntil = c.CloseAt + Math.Max(1, (int)MathF.Round(Defs.CombatRules.CloseWindowSec / Config.SimDt));
        CloseRanksPass(c);
    }

    // ---------------------------------------------------------------- 매 틱 (Step, StepGapChecks 뒤)

    readonly List<Unit> closeChargers = new();

    void StepCloseRanks()
    {
        if (Companies.Count == 0) return;
        var cr = Defs.CombatRules;
        int checkIv = Math.Max(1, (int)MathF.Round(cr.CloseCheckSec / Config.SimDt));
        int passIv = Math.Max(1, (int)MathF.Round(cr.ClosePassSec / Config.SimDt));
        int combatTicks = (int)MathF.Round(cr.GapCombatSec / Config.SimDt);
        int cooldown = Math.Max(1, (int)MathF.Round(cr.CloseCooldownSec / Config.SimDt));
        int pollIv = Math.Max(1, (int)MathF.Round(cr.CloseChargePollSec / Config.SimDt));
        // 돌격 감지 (gapfill2): pollIv 마다 돌격 중인 유닛 (자동 · 명령 돌격 — 역돌격 제외) 목록 (유닛 목록 순)
        closeChargers.Clear();
        if (Tick % pollIv == 0 && cr.CloseChargeRange > 0)
            foreach (var u in Units) if (u.Alive && u.Charging && !u.CounterCharging) closeChargers.Add(u);
        for (int ci = 0; ci < Companies.Count; ci++)
        {
            var c = Companies[ci];
            if (c.TA.Stage != TaStage.None) continue;   // N32 참호 돌격 중
            // ③ 유효한 명령: 늦게 따르는 병사 (교전 중 노병 등) 를 closePassSec 마다 다시 살핀다
            if (c.CloseUntil != 0)
            {
                if (Tick > c.CloseUntil) c.CloseUntil = 0;
                else if (Tick != c.CloseAt && (Tick - c.CloseAt) % passIv == 0) CloseRanksPass(c);
            }
            bool ready = c.CloseAt == 0 || Tick - c.CloseAt >= cooldown;
            // ② 적 돌격이 다가오면 점검 간격을 건너뛰고 바로 판단 (재명령 간격 안이면 판단하지 않음)
            // tweak1 (사용자 결정 1b): 자기 중대가 돌격 중이면 대열 정비 명령을 미룬다 (경보 · 재명령 간격은 그대로 — 돌격이 끝나면 다음 판단에서)
            bool assaulting = CompanyAssaulting(c);
            if (assaulting && (Tick + c.Id * 53L) % DecideInterval(c, checkIv) == 0) Stats.CloseAssaultHeld++;
            if (closeChargers.Count > 0 && ready && !assaulting && ChargeIncoming(c, cr.CloseChargeRange) && CanJudgeRanks(c))
            {
                Stats.CloseChargeSeen++;
                var dc = MeasureRanks(c);
                if (dc.Men >= 2 && RanksBroken(dc, cr.CloseChargeThrMul))
                {
                    c.CloseAlarm = true;
                    Stats.CloseOrders++; Stats.CloseChargeOrders++;
                    OrderCloseRanks(c);
                    continue;
                }
            }
            // ② 장교 판단: closeCheckSec 간격 (지휘관 경험치로 짧아짐, 중대 Id 분산)
            if ((Tick + c.Id * 53L) % DecideInterval(c, checkIv) != 0) continue;
            if (assaulting) continue;
            if (ContactLoose(c)) { c.CloseAlarm = false; continue; }   // F1: 접적 중 장교 대열 정비 판단 없음 (적 돌격 감지 · 플레이어 명령은 그대로, Contact.cs)
            if (!CanJudgeRanks(c)) { c.CloseAlarm = false; continue; }
            if (!CompanyInCombat(c, combatTicks)) { c.CloseAlarm = false; continue; }   // 조용하면 N12 빈자리 점검 (Squads.cs) 이 맡는다
            var d = MeasureRanks(c);
            if (d.Men < 2) { c.CloseAlarm = false; continue; }
            if (!c.CloseAlarm) { if (RanksBroken(d, 1f)) c.CloseAlarm = true; }
            else if (!RanksBroken(d, cr.CloseHysteresis)) c.CloseAlarm = false;
            if (c.CloseAlarm && ready)
            {
                Stats.CloseOrders++;
                OrderCloseRanks(c);
            }
        }
    }

    /// <summary>
    /// 앞줄 구멍 판단 (문턱 × mul — 경보 켜기 1, 해제 closeHysteresis, 돌격 closeChargeThrMul): 구멍 ≥ max(closeHoles, closeHoleFrac × 앞줄 슬롯) · 연속 구멍 ≥ closeHoleGap ·
    /// 뒤처진 앞줄 병사 ≥ closeBehindMen (0 = 끔) · 벗어난 병사 비율 ≥ closeOnFrac (보조, 1 이상이면 끔) 중 하나라도.
    /// </summary>
    internal bool RanksBroken(RankDistortion d, float mul)
    {
        var cr = Defs.CombatRules;
        float holes = MathF.Max(cr.CloseHoles, cr.CloseHoleFrac * d.FrontSlots);
        if (d.FrontSlots > 0 && d.Holes >= holes * mul) return true;
        if (cr.CloseHoleGap > 0 && d.HoleGap >= cr.CloseHoleGap * mul) return true;
        if (cr.CloseBehindMen > 0 && d.Behind >= cr.CloseBehindMen * mul) return true;
        return cr.CloseOnFrac < 1f && d.DispFrac >= cr.CloseOnFrac * mul;
    }

    /// <summary>적 돌격 감지: closeChargers 중 적대 세력이 중대 병사 (장교 제외) range 칸 안에 있고 다가오는 중 (표적이 이 중대 병사이거나 속도가 중대 무게중심 쪽).</summary>
    internal bool ChargeIncoming(Company c, float range)
    {
        if (c.Members.Count == 0) return false;
        float r2 = range * range;
        Vector2 cen = Centroid(c.Members);
        foreach (var e in closeChargers)
        {
            if (!e.Alive || !IsHostile(c.Owner, e.Owner)) continue;
            bool closing = (e.Target is Unit t && t.Company == c) || Vector2.Dot(e.Velocity, cen - e.Pos) > 0;
            if (!closing) continue;
            foreach (var u in c.Members)
                if (!u.IsOfficer && Vector2.DistanceSquared(u.Pos, e.Pos) <= r2) return true;
        }
        return false;
    }

    /// <summary>[테스트] 이번 틱 돌격 목록을 다시 모은다 (StepCloseRanks 와 같은 규칙).</summary>
    internal void RefreshChargersForTest()
    {
        closeChargers.Clear();
        foreach (var u in Units) if (u.Alive && u.Charging && !u.CounterCharging) closeChargers.Add(u);
    }

    /// <summary>장교 판단을 할 수 있는가: 지휘관 (인계 중이 아님) · 같은 세력 장교 · 대리 부사관 중 하나, 대형 방향이 있고 행군 중이 아님 (gapfill2: 틀 행동 · 도착한 깃발은 허용).</summary>
    internal bool CanJudgeRanks(Company c)
    {
        if (c.Facing == Vector2.Zero || c.Members.Count < 2 || c.Marching) return false;
        if (InHandover(c)) return false;
        return CompanyOfficer(c, Centroid(c.Members)) != null || ActingNco(c) != null;
    }

    /// <summary>
    /// tweak1 (사용자 결정 1b): 자기 중대의 돌격이 진행 중인가 — 틀 기동조 · 예비 돌격 (Drill.Man 4), 명령 돌격 중인 병사 (돌격 상태 2 — 전술 AI 돌격 · 플레이어 돌격),
    /// 또는 자동 돌격 (상태 1) 병사가 중대 병사의 assaultHoldAutoFrac 이상. 역돌격 (3) 은 자기 돌격이 아님. 위치 · 상태의 함수 (새 상태 없음).
    /// </summary>
    internal bool CompanyAssaulting(Company c)
    {
        if (c.Drill.Man == 4) return true;
        int men = 0, auto = 0;
        foreach (var u in c.Members)
        {
            if (!u.Alive || u.IsOfficer) continue;
            men++;
            if (u.ChargeState == 2) return true;
            if (u.ChargeState == 1) auto++;
        }
        float f = Defs.CombatRules.CloseAssaultAutoFrac;
        return men > 0 && f > 0 && auto >= f * men;
    }

    /// <summary>tweak1 (사용자 결정 1a): 화력조 · 지원 역할 (화력 기반 · 엄호 사격) 병사는 대열 정비 대상이 아니다 — 정비로 걸어가는 동안 엄호 화력이 끊겼다 (한국 돌격 후퇴 43 → 60%).</summary>
    static bool FireTeamRole(Unit u) => u.Role is UnitRole.Fire or UnitRole.Support;

    /// <summary>틀 행동 중 이 병사의 역할 행동 (국면 Idle 이면 Hold).</summary>
    DrillAct CloseActOf(Company c, Unit u)
    {
        if (c.Drill.Phase == DrillPhase.Idle) return DrillAct.Hold;
        var T = TacRules.Template(c.ActiveTactic, out _);
        var role = u.Role == UnitRole.None ? UnitRole.Rifle : u.Role;
        RoleSlotDef? gd = null;
        T?.Groups.TryGetValue(TacticRules.KeyOf(role), out gd);
        return ActOf(gd?.Act, role);
    }

    /// <summary>대열 정비로 메울 수 있는 병사인가 (앞줄 구멍을 셀 슬롯의 주인): 장교 · 중화기 승무원 아님, 틀 행동 중이면 사격 · 지원 · 뒤따름 · 약진 역할만.</summary>
    bool LineMember(Company c, Unit u)
    {
        if (u.IsOfficer || u.Def.Heavy || FireTeamRole(u)) return false;
        return CloseActOf(c, u) is DrillAct.Hold or DrillAct.Follow or DrillAct.Bound;   // tweak1: 지원 (엄호 사격) 행동 제외
    }

    /// <summary>지금 구성원으로 짠 중대 대형 (무게중심 · Facing 기준, 빈자리 점검과 같은 배치) 과 그 배정.</summary>
    (CompanyLayout.Result Lay, Vector2[] World, int[] Assign, float Spacing, Vector2 Cen) IdealRanks(Company c, bool keepWidth = false)
    {
        var units = c.Members;
        int n = units.Count;
        float spacing = OrgLayout.SpacingFor(units);
        float width = keepWidth ? HeldWidth(c) : 0;   // 대형 완화 휴식 정렬: 지금 대열 폭 (드래그 폭 · 대대 배치 폭을 그대로) — 기본 모양 폭으로 바꿔 옆 중대와 겹치지 않게
        var lay = CompanyLayout.Build(c, units, c.Form, spacing, width, FormRules, TacRules, combat: Defs.CombatRules);
        Vector2 dir = c.Facing, right = new(-dir.Y, dir.X);
        // 대열 중심 = 병사 위치의 (옆 · 정면 축) 중앙값을 대형 슬롯의 중앙값에 맞춘 점 — 무게중심이 아니라 (몇 명이 멀리 떨어져도 대열 전체가 그쪽으로 끌려가지 않게)
        var px = new List<(float V, int Id)>(); var py = new List<(float V, int Id)>();
        foreach (var u in units) if (!u.IsOfficer) { px.Add((Vector2.Dot(u.Pos, right), u.Id)); py.Add((Vector2.Dot(u.Pos, dir), u.Id)); }
        var sx = new List<(float V, int Id)>(); var sy = new List<(float V, int Id)>();
        for (int s = 0; s < lay.Local.Length; s++) if (!IsOfficerSlot(lay, s)) { sx.Add((lay.Local[s].X, s)); sy.Add((lay.Local[s].Y, s)); }
        Vector2 cen = px.Count == 0 || sx.Count == 0 ? Centroid(units)
            : right * (Median(px) - Median(sx)) + dir * (Median(py) - Median(sy));
        var world = new Vector2[n];
        var assign = new int[n];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < n; i++) world[i] = cen + right * lay.Local[i].X + dir * lay.Local[i].Y;
            CompanyLayout.Match(lay, units, world, null, assign);
            if (pass == 1 || px.Count == 0) break;
            // 두 번째: 배정 슬롯에서의 어긋남 (병사 − 슬롯) 의 중앙값만큼 중심을 옮긴다 — 제자리 병사들의 공통 어긋남 (중앙값의 이산 오차) 을 없애고, 멀리 떨어진 병사는 무시
            var rx = new List<(float V, int Id)>(); var ry = new List<(float V, int Id)>();
            for (int i = 0; i < n; i++)
                if (!units[i].IsOfficer) { var d = units[i].Pos - world[assign[i]]; rx.Add((Vector2.Dot(d, right), units[i].Id)); ry.Add((Vector2.Dot(d, dir), units[i].Id)); }
            cen += right * Median(rx) + dir * Median(ry);
        }
        return (lay, world, assign, spacing, cen);
    }

    /// <summary>중앙값 (같은 값은 Id 순 — 결정적, 짝수면 가운데 둘의 평균).</summary>
    static float Median(List<(float V, int Id)> l)
    {
        l.Sort((a, b) => a.V != b.V ? a.V.CompareTo(b.V) : a.Id.CompareTo(b.Id));
        int m = l.Count / 2;
        return (l.Count & 1) == 1 ? l[m].V : (l[m - 1].V + l[m].V) * 0.5f;
    }

    /// <summary>
    /// 대열 흐트러짐 (장교 판단 지표, 장교 제외 병사만): DispFrac = 배정 슬롯에서 closeDispTiles 넘게 벗어난 병사 비율 ·
    /// FrontEmpty · FrontGap = 옛 엄격한 지표 (앞줄 슬롯 중 closeFrontTol × 간격 안에 병사가 없는 수 · 이어진 가장 긴 수 — 측정 비교용) ·
    /// gapfill2 앞줄 구멍: 메울 수 있는 병사 (LineMember) 에 배정된 슬롯 중 가장 앞 줄 (FrontSlots) 마다, 옆으로 closeHoleLat × 간격 안이고 슬롯보다 closeHoleDepth 칸 넘게
    /// 뒤가 아닌 곳 (앞은 얼마든지) 에 병사가 하나도 없으면 구멍 (Holes), 옆으로 이어진 가장 긴 구멍 (HoleGap), 앞줄 슬롯에 배정된 병사 중 closeBehindTiles 칸 넘게 뒤처진 수 (Behind).
    /// </summary>
    internal RankDistortion MeasureRanks(Company c)
    {
        var units = c.Members;
        if (units.Count < 2 || c.Facing == Vector2.Zero) return new RankDistortion(0, 0, 0, 0);
        var cr = Defs.CombatRules;
        var (lay, world, assign, spacing, _) = IdealRanks(c);
        Vector2 dir = c.Facing, right = new(-dir.Y, dir.X);
        float thr = cr.CloseDispTiles, thr2 = thr * thr;
        float fr = cr.CloseFrontTol * spacing, fr2 = fr * fr;   // 앞줄 슬롯을 채운 것으로 보는 반경 (간격 비율 — 뒷줄 병사는 채운 것이 아님)
        int men = 0, off = 0;
        float frontY = float.MinValue;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].IsOfficer) continue;
            men++;
            if (Vector2.DistanceSquared(units[i].Pos, world[assign[i]]) > thr2) off++;
        }
        for (int s = 0; s < lay.Local.Length; s++) if (!IsOfficerSlot(lay, s)) frontY = MathF.Max(frontY, lay.Local[s].Y);
        if (men == 0) return new RankDistortion(0, 0, 0, 0);
        var front = new List<int>();
        for (int s = 0; s < lay.Local.Length; s++) if (!IsOfficerSlot(lay, s) && lay.Local[s].Y >= frontY - spacing * 0.5f) front.Add(s);
        front.Sort((a, b) => lay.Local[a].X != lay.Local[b].X ? lay.Local[a].X.CompareTo(lay.Local[b].X) : a.CompareTo(b));
        int empty = 0, run = 0, gap = 0;
        foreach (int s in front)
        {
            bool filled = false;
            foreach (var u in units)
                if (!u.IsOfficer && Vector2.DistanceSquared(u.Pos, world[s]) <= fr2) { filled = true; break; }
            if (filled) { run = 0; continue; }
            empty++;
            gap = Math.Max(gap, ++run);
        }
        // gapfill2 앞줄 구멍: 메울 수 있는 병사에게 배정된 슬롯만 (중화기 · 장교 · 기동조 자리는 비어 있어도 구멍이 아님)
        float lineY = float.MinValue;
        var slotOwner = new int[lay.Local.Length];
        Array.Fill(slotOwner, -1);
        for (int i = 0; i < units.Count; i++)
            if (LineMember(c, units[i])) { slotOwner[assign[i]] = i; lineY = MathF.Max(lineY, lay.Local[assign[i]].Y); }
        var line = new List<int>();
        for (int s = 0; s < lay.Local.Length; s++) if (slotOwner[s] >= 0 && lay.Local[s].Y >= lineY - spacing * 0.5f) line.Add(s);
        line.Sort((a, b) => lay.Local[a].X != lay.Local[b].X ? lay.Local[a].X.CompareTo(lay.Local[b].X) : a.CompareTo(b));
        float lat = cr.CloseHoleLat * spacing, depth = cr.CloseHoleDepth, behindT = cr.CloseBehindTiles;
        int holes = 0, hrun = 0, hgap = 0, behind = 0;
        foreach (int s in line)
        {
            bool covered = false;
            foreach (var u in units)
            {
                if (u.IsOfficer) continue;
                var dv = u.Pos - world[s];
                if (MathF.Abs(Vector2.Dot(dv, right)) <= lat && Vector2.Dot(dv, dir) >= -depth) { covered = true; break; }
            }
            if (Vector2.Dot(units[slotOwner[s]].Pos - world[s], dir) < -behindT) behind++;
            if (covered) { hrun = 0; continue; }
            holes++;
            hgap = Math.Max(hgap, ++hrun);
        }
        return new RankDistortion(off / (float)men, empty, gap, men, holes, hgap, behind, line.Count);
    }

    /// <summary>장교 슬롯인가 (N12 PlainWithOfficers 의 묶음 1). 틀 배치는 역할 묶음이라 장교 슬롯을 따로 가리지 않는다.</summary>
    static bool IsOfficerSlot(CompanyLayout.Result lay, int s) => lay.Pools == 2 && lay.SlotPool[s] == 1;

    /// <summary>교전 중인 병사: 표적을 잡았거나 regroupFireSec 안에 쐈다 (대열 정비에서 숙련이 갈리는 경우).</summary>
    internal bool EngagedForRanks(Unit u) =>
        u.Target != null || Tick - u.LastShotTick < (int)MathF.Round(Defs.CombatRules.RegroupFireSec / Config.SimDt);

    /// <summary>대열 정비 명령 (c.CloseAt) 에 이 병사가 지금 따르는가: 교전 중이 아니면 곧장, 교전 중이면 숙련별 확률 (해시 — 명령마다 고정) 로 곧장, 아니면 지연 뒤.</summary>
    internal bool CompliesWithClose(Unit u, Company c)
    {
        if (!EngagedForRanks(u)) return true;
        var cr = Defs.CombatRules;
        int k = (int)SkillOf(u);
        float obey = k < cr.CloseObey.Length ? cr.CloseObey[k] : 1f;
        float delay = k < cr.CloseDelaySec.Length ? cr.CloseDelaySec[k] : 0f;
        if (PairNoise(u.Id, (int)(c.CloseAt & 0x7FFFFFFF), Map.Seed ^ 0x434C5352) < obey) return true;
        return Tick - c.CloseAt >= (long)MathF.Round(delay / Config.SimDt);
    }

    /// <summary>
    /// 정비 한 번: 행군 중 (도착하지 않은 깃발) 이면 다음 기회로. 아니면 지금 구성원으로 대형을 다시 짜서 슬롯에서 closeSlack 넘게 벗어난 대상 병사 (CloseCandidate) 중
    /// 따르는 병사를 슬롯으로 (대기 · 사수 = 이동 — 곧장 걸어감 (행진 사격), 공격 이동 = 공격 이동 그대로 슬롯까지 걸어감 (CloseWalking), 도착하면 다시 싸운다).
    /// 도착한 깃발 (옛 대형 · 빈자리 점검 · 앞 정비) 이 있으면 새 깃발로 바꾸고, 옮기지 않는 그 구성원은 명령 · 최종 위치 그대로 넘겨받는다 — 단 따르지 않은
    /// 교전 중 공격 이동 병사 (노병) 는 그 자리에서 싸우도록 깃발에서 풀어 준다 (다음 정비에서 다시 살핌).
    /// </summary>
    void CloseRanksPass(Company c, bool rest = false)
    {
        if (c.Members.Count < 2 || c.Facing == Vector2.Zero || c.Marching) return;
        if (CompanyAssaulting(c)) return;   // tweak1: 자기 돌격 중에는 보내지 않음 (명령 유효 시간 안에 돌격이 끝나면 다음 정비에서)
        var units = c.Members;
        var old = c.Anchor;
        bool follow = Tick != c.CloseAt;   // 명령 뒤 closePassSec 마다 다시 살피는 정비
        var (lay, world, assign, spacing, cen) = IdealRanks(c, rest);
        float slack = MathF.Max(Defs.CombatRules.CloseSlack, Defs.CombatRules.FormArriveSlack);
        // 대형 완화 휴식 정렬: 구역 도착 반경 (formArriveZone) 안 병사는 다시 걷지 않음 — N12 문턱 2.75칸을 쓰면 줄 바꿈 배정 (뒤로 밀린 앞줄 병사 → 뒷줄 슬롯) 이 모두 문턱 안이라 정렬이 아무도 옮기지 않았다
        if (rest) slack = MathF.Max(slack, Defs.CombatRules.FormArriveZone);
        var movers = new List<(Unit U, int Slot)>();
        var moving = new HashSet<int>();
        var attack = new HashSet<int>();
        var stay = new HashSet<int>();
        int late = 0;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            bool inOld = old != null && u.Group != null && ReferenceEquals(u.Group, old.Group);
            if (follow && inOld && old!.Close && u.Order is UnitOrder.Move or UnitOrder.AttackMove) continue;   // 이 명령으로 이미 슬롯에 가는 중 (늦은 병사 살피는 정비는 새로 따르는 병사만)
            if (!CloseCandidate(c, u, inOld)) continue;
            if (rest && (HoldsGround(u) || u.Def.Scout)) continue;   // 대형 완화 휴식 정렬 (FormRelax.cs): 엄폐 · 참호 · 최근 사격 병사는 그대로
            if (Vector2.Distance(u.Pos, world[assign[i]]) <= slack) continue;
            // 늦은 병사 살피는 정비: 이 명령으로 이미 슬롯에 닿은 병사 (깃발에 최종 위치가 남아 있음) 는 대열 중심이 조금 옮겨졌다고 다시 보내지 않는다
            if (follow && old is { Close: true } && old.FinalOff.TryGetValue(u.Id, out var done) && Vector2.Distance(u.Pos, old.Goal + done) <= slack) continue;
            if (!CompliesWithClose(u, c))                                         // 노병: 싸우기를 먼저 (지연 뒤 · 교전이 끝나면)
            {
                late++;
                if (inOld && u.Order == UnitOrder.AttackMove) stay.Add(u.Id);
                continue;
            }
            movers.Add((u, assign[i]));
            moving.Add(u.Id);
            if (u.Order == UnitOrder.AttackMove) attack.Add(u.Id);
        }
        Stats.CloseLate += late;
        if (movers.Count == 0) return;
        Stats.CloseMovers += movers.Count;
        Stats.CloseMoveAttack += attack.Count;
        List<(Unit U, Vector2 Local, Vector2 Final)>? keep = null;
        if (old != null)
        {
            keep = new();
            foreach (var u in units)
            {
                if (u.Group == null || !ReferenceEquals(u.Group, old.Group) || moving.Contains(u.Id)) continue;
                if (stay.Contains(u.Id)) { u.Group = null; u.Order = UnitOrder.Idle; continue; }   // 교전 중 노병: 그 자리에서 싸움 (표적 유지)
                keep.Add((u, old.Slots.TryGetValue(u.Id, out var l) ? l : Vector2.Zero, old.Goal + (old.FinalOff.TryGetValue(u.Id, out var fo) ? fo : Vector2.Zero)));
            }
        }
        SendToSlots(c, cen, lay.Local, world, spacing, movers, UnitOrder.Move, attack, keep, close: true);
        // 같은 명령 안에서 깃발을 새로 세웠으면, 앞 깃발로 이미 슬롯에 닿은 병사의 최종 위치를 넘겨 둔다 (다음 정비에서 다시 보내지 않게 — 구성원 순)
        if (follow && old is { Close: true } && c.Anchor is { } na && !ReferenceEquals(na, old))
            foreach (var u in units)
                if (u.Group == null && !na.FinalOff.ContainsKey(u.Id) && old.FinalOff.TryGetValue(u.Id, out var fo)) na.FinalOff[u.Id] = old.Goal + fo - na.Goal;
    }

    /// <summary>
    /// 대열 정비 대상 (gapfill2): 돌격 · 반응 지연 · 참호 (안 · 자리) · 중화기 승무원 제외. 대기 · 사수 (그룹 없음) · 공격 이동 (그룹 없음 또는 도착한 깃발) ·
    /// 깃발 안 이동 (옛 슬롯으로 가는 중) 만 — 다른 이동 (약진 · 후퇴 · 틀 이동) 은 그대로. 틀 행동 중이면 사격 · 지원 · 뒤따름 · 약진 역할만, 지금 약진 중인 분대는 제외.
    /// </summary>
    internal bool CloseCandidate(Company c, Unit u, bool inOld)
    {
        if (u.Charging || u.ReactDelay > 0 || u.InTrench || u.HasTrenchSpot || u.Def.Heavy || FireTeamRole(u)) return false;   // tweak1: 화력조 · 지원 역할 제외
        bool ok = u.Order switch
        {
            UnitOrder.Idle => u.Group == null || inOld,
            UnitOrder.Hold => u.Group == null,
            UnitOrder.AttackMove => u.Group == null || inOld,
            UnitOrder.Move => inOld,
            _ => false,
        };
        if (!ok) return false;
        if (u.IsOfficer || c.Drill.Phase == DrillPhase.Idle) return true;
        if (c.Drill.BoundSquad > 0 && u.Squad != null && u.Squad.Id == c.Drill.BoundSquad) return false;
        return LineMember(c, u);
    }

    /// <summary>대열 정비 깃발로 슬롯까지 걸어가는 공격 이동 병사 (gapfill2): 표적에 멈추지 않고 걷는다 (행진 사격). 슬롯 closeSlack 안이면 보통 공격 이동 (서서 싸움).</summary>
    internal bool CloseWalking(Unit u)
    {
        if (u.Order != UnitOrder.AttackMove || u.Charging || u.Group == null || u.InTrench || u.HasTrenchSpot) return false;
        if (u.Company?.Anchor is not { Close: true, Arrived: true } a || !ReferenceEquals(a.Group, u.Group)) return false;
        if (!a.FinalOff.TryGetValue(u.Id, out var fo)) return false;
        float s = Defs.CombatRules.CloseSlack;
        return Vector2.DistanceSquared(u.Pos, a.Goal + fo) > s * s;
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 (ORG_ 끝, FormatVersion 28)

    void WriteCloseRanks(SnapWriter w)
    {
        foreach (var c in Companies) { w.Bool(c.CloseAlarm); w.L(c.CloseAt); w.L(c.CloseUntil); w.Bool(c.Anchor?.Close ?? false); }
    }

    void ReadCloseRanks(SnapReader r)
    {
        foreach (var c in Companies)
        {
            c.CloseAlarm = r.Bool(); c.CloseAt = r.L(); c.CloseUntil = r.L();
            bool close = r.Bool();
            if (close && c.Anchor == null) throw new SnapshotException(string.Format(SnapMsg.Invalid, "close ranks anchor"));
            if (c.Anchor != null) c.Anchor.Close = close;
            if (c.CloseAt < 0 || c.CloseUntil < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company close ranks"));
        }
    }

    void HashCloseRanks(ref Hasher h)
    {
        foreach (var c in Companies) { h.Add(c.CloseAlarm ? 1 : 0); h.Add(c.CloseAt); h.Add(c.CloseUntil); h.Add(c.Anchor?.Close == true ? 1 : 0); }
    }
}
