using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 포병 AI 수치 (assets/data/tactics.json "artilleryAi", N20 2026-10-19). 밸런스 수치 아님 — AI 판단 문턱만. 없으면 내장 기본값.
/// </summary>
public sealed class ArtilleryAiDef
{
    /// <summary>관측자 (관측반 · AI 가 부리는 정찰병): 아군 보병 앞선보다 이 칸 앞 (보이는 적이 없을 때).</summary>
    public float ObserverAhead { get; set; } = 8f;
    /// <summary>접촉 뒤 관측자 지점 = 아군 전선 (중대 무게중심 앞선) 에서 이 칸 뒤.</summary>
    public float ObserverContactBack { get; set; } = 4f;
    /// <summary>관측자와 보이는 적의 최소 거리 = 소총 사거리 × 이 값 (적 소총 · 저격총 사거리 밖).</summary>
    public float ObserverStandoff { get; set; } = 1.5f;
    /// <summary>관측 지점 둘레 엄폐 · 고지 탐색 반경 (칸).</summary>
    public int ObserverCoverRadius { get; set; } = 3;
    /// <summary>관측 지점이 이 칸 넘게 바뀌어야 다시 보낸다.</summary>
    public float ObserverRepost { get; set; } = 6f;
    /// <summary>관측자 판단 간격 (초).</summary>
    public float ObserverSec { get; set; } = 2f;
    /// <summary>관측자가 모두 죽으면 소총 중대 정찰조에서 이 수만큼 새 관측반으로 (0 = 안 함). 관측 전용 포 (곡사포) 가 있을 때만.</summary>
    public int ObserverRetask { get; set; } = 2;
    /// <summary>tweak1 노출 회피: 노려져 엄폐한 뒤 observerAvoidSec 동안은 그 노출 지점 observerAvoidRadius 칸 안으로 다시 보내지 않고, 그때 사격자 위치에서
    /// (노출 지점 거리 + observerAvoidStandoff) 칸 밖 · 엄폐나 은폐 (숲 · 잔해 · 가옥 옆) 또는 그 사격자 사선 밖인 칸을 둘레 observerAvoidSearch 칸에서 고른다.</summary>
    public float ObserverAvoidSec { get; set; } = 120f;
    public float ObserverAvoidRadius { get; set; } = 8f;
    public float ObserverAvoidStandoff { get; set; } = 4f;
    public int ObserverAvoidSearch { get; set; } = 10;
    /// <summary>돌격 · 약진 표적 둘레 이 칸 안의 적을 곡사포 표적으로 우선한다.</summary>
    public float SupportRadius { get; set; } = 12f;
    /// <summary>근접 위험: 강제 표적은 아군에서 dangerClose + dangerScatterMul × (그 거리의 퍼짐) 칸 밖이어야 한다.</summary>
    public float DangerClose { get; set; } = 5f;
    public float DangerScatterMul { get; set; } = 0.75f;
    /// <summary>포대 재배치 쿨다운 (초, 방열을 마친 뒤) — 포대 수 / redeployBatteryRef 배 (1 이상) 로 늘린다 (포대가 많아도 세력 전체 이동 빈도는 같게).</summary>
    public float RedeployCooldownSec { get; set; } = 60f;
    public float RedeployBatteryRef { get; set; } = 4f;
    /// <summary>재배치 최소 이동 (칸) — 이보다 작은 수정은 제자리 선회 (N10) 로.</summary>
    public float RedeployMinMove { get; set; } = 10f;
    /// <summary>곡사포는 표적 (연대 화력 표적 · 관측 표적) 이 사거리 × 이 값을 넘으면 전진 (쿨다운 · 사격 중 · 최소 이동 조건 뒤).</summary>
    public float RedeployRangeFrac { get; set; } = 0.75f;
    /// <summary>곡사포가 옮길 때 표적과의 거리 = 사거리 × 이 값 (옛 0.75).</summary>
    public float RedeployWantFrac { get; set; } = 0.65f;
    /// <summary>supp2 (2026-10-20): 방열한 포가 없는 곡사포대는 보이는 적이 이 칸 안이면 (소총 사거리 24 + 여유) 그 자리에서 방열하지 않고 그 적에서 exposedRetreat 칸으로 물러나 방열한다 (방열 중 · 방열한 포가 있으면 그대로) (0 = 끔).
    /// 엎드림 · 붕괴로 보병 교전이 길어지면 소총 사거리 안에서 20초 방열하던 포가 쏘기 전에 죽던 원인.</summary>
    public float ExposedStandoff { get; set; } = 30f;
    public float ExposedRetreat { get; set; } = 50f;
    /// <summary>이 초 안에 쏜 포가 있는 포대는 옮기지 않는다.</summary>
    public float GunIdleSec { get; set; } = 20f;
    /// <summary>직사포 유효 사거리 비율: 이보다 먼 표적은 가까운 표적이 있으면 버린다 · 가장 가까운 적이 이보다 멀면 사격 진지로 다가간다.</summary>
    public float GunEffectiveFrac { get; set; } = 0.8f;
    /// <summary>직사포 사격 진지 거리 = max(사거리 × gunStandoffFrac, 소총 사거리 × gunStandoffRifleMul) (유효 사거리 이하로 자름).</summary>
    public float GunStandoffFrac { get; set; } = 0.65f;
    public float GunStandoffRifleMul { get; set; } = 1.3f;
    /// <summary>직사포 전개 시작: 보이는 적이 사거리 + 소총 사거리 × 이 값 안이면 사격 진지로 (옛 16 × K = 소총 사거리 × 1.0).</summary>
    public float GunDeployLead { get; set; } = 1.5f;

    // ---- N22 보병포 (동행 근접 지원포): 사거리 closeGunMaxRange 이하 직사포
    /// <summary>이 사거리 이하 직사포 = 보병포 (대대와 함께 움직이고 사격선 바로 뒤에서 직접 사격).</summary>
    public float CloseGunMaxRange { get; set; } = 50f;
    /// <summary>접촉 뒤 진지 = 지원하는 소총 중대 (사격선) 무게중심에서 적 반대쪽으로 이 칸 뒤.</summary>
    public float CloseGunBehind { get; set; } = 3f;
    /// <summary>접촉 전 (행군) = 지원 중대 무게중심에서 진행 반대쪽으로 이 칸 뒤를 따라감.</summary>
    public float CloseGunFollow { get; set; } = 8f;
    /// <summary>접촉: 보이는 적이 지원 중대에서 사거리 + 소총 사거리 × 이 값 안.</summary>
    public float CloseGunContactLead { get; set; } = 1.0f;
    /// <summary>방열한 보병포는 진지가 이 칸 넘게 바뀔 때만 옮김 (쿨다운 closeGunCooldownSec 뒤 · 유효 사거리 안 사격 중이 아니면).</summary>
    public float CloseGunMove { get; set; } = 10f;
    public float CloseGunCooldownSec { get; set; } = 20f;
    /// <summary>유효 사거리 안 표적을 이 초 안에 쏜 포가 있으면 '쏘는 중' (옮기지 않음).</summary>
    public float CloseGunIdleSec { get; set; } = 8f;
    /// <summary>표적 점수 (유효 사거리 안 · 사선): 기관총 (흩뿌림 무기) + mg · 포 + gun · 엄폐 (참호 · 숲 · 잔해 · 가옥 옆) + cover · 둘레 2칸 적 1기마다 + cluster · 거리 1칸마다 − dist. 표적은 closeGunRetargetSec 마다 다시 고름 (더 나은 표적이 keep 점 넘게 좋을 때만 바꿈).</summary>
    public float CloseGunMgW { get; set; } = 30f;
    public float CloseGunGunW { get; set; } = 20f;
    public float CloseGunCoverW { get; set; } = 12f;
    public float CloseGunClusterW { get; set; } = 4f;
    public float CloseGunDistW { get; set; } = 0.5f;
    public float CloseGunKeep { get; set; } = 10f;
    public float CloseGunRetargetSec { get; set; } = 4f;
    /// <summary>
    /// 사선 막힘 재배치 (N22 — 봉천 보병포 첫 사격 120초의 원인): 방열한 직사포 포대가 이 초 넘게 쏘지 못했고 유효 사거리 안 보이는 적은 있는데
    /// 사계 · 직사 사선이 트인 적이 하나도 없으면 (숲 · 고지 · 건물 뒤에 방열) 쿨다운 없이 둘레 gunLosSearch 칸에서 그 적이 사선에 드는 칸으로 옮긴다.
    /// </summary>
    public float GunBlindSec { get; set; } = 6f;
    public int GunLosSearch { get; set; } = 6;
}

/// <summary>
/// 포병 AI (N20 2026-10-19, docs/ARTILLERY_EFFECT_2026-10-19.md 'N20'):
///  · 관측자 전진 (관측반 = 정찰병만의 중대 · 정찰 분견대): 보병 앞선 앞 엄폐 · 고지, 적 소총 사거리 밖. 전멸하면 소총 중대 정찰조에서 새 관측반.
///  · 곡사포: 돌격 · 약진 표적 둘레 적 우선 → 연신 (연대 화력 표적) → 엔진 자동 표적 (관측된 가장 가까운 적). 강제 표적은 근접 위험 거리 밖만.
///    재배치 = 쿨다운 (포대 수로 정규화) · 최소 이동 · 쏘고 있으면 안 옮김 (작은 수정은 제자리 선회).
///  · 직사포 (보병포 · 야전포): 종류별 분견대, 유효 사거리 (사거리 × 0.8) 안 사격 진지, 먼 표적을 쏜 뒤 가까운 표적이 있으면 표적을 놓아 엔진이 가까운 적을 다시 고르게.
///  · 준비사격: 연대 준비사격 국면은 지원 포병이 실제로 쏜 뒤에만 끝난다 (관측 없으면 기존 시간 상한).
/// 새 저장 상태 없음 (기존 TacGroup 필드 · 유닛 LastShotTick · 월드 읽기만).
/// </summary>
public sealed partial class TacticalAi
{
    static ArtilleryAiDef Art(World w) => w.Defs.Tactics.ArtilleryAi;

    static bool IsGun(Unit u) => u.Def.Category == "gun" && u.Weapon != null && !World.IsIndirect(u.Weapon);

    /// <summary>직사포만의 무리인가 (N20 이후 포는 종류별 분견대 — 섞인 무리 (지도 중대) 는 다수가 포일 때).</summary>
    static bool IsGunGroup(List<Unit> m)
    {
        int guns = 0, other = 0;
        foreach (var u in m) { if (u.IsOfficer) continue; if (IsGun(u)) guns++; else other++; }
        return guns > 0 && guns >= other;
    }

    /// <summary>화력 분견대: 포는 유닛 종류별로 따로 (저격수 · 기관총과 섞이면 무리 사거리 = 최솟값, 속도 = 가장 느린 것 — 야전포가 저격수 사거리로 움직였다). 옆 순서는 그대로.</summary>
    void AddFireDetachments(TacFaction s, List<Unit> sorted, int directive)
    {
        var keys = new List<string>();
        foreach (var u in sorted) { string k = IsGun(u) ? u.Def.Key : ""; if (!keys.Contains(k)) keys.Add(k); }
        foreach (var k in keys)
        {
            var part = sorted.Where(u => (IsGun(u) ? u.Def.Key : "") == k).ToList();
            AddDetachments(s, TacKind.Fire, part, directive);
        }
    }

    /// <summary>포대 수 (곡사포 무리 + 직사포 분견대).</summary>
    int BatteryCount(World w, TacFaction s)
    {
        int n = 0;
        foreach (var g in s.Groups)
        {
            if (g.Pending || g.Alive <= 0) continue;
            if (g.Kind == TacKind.Artillery) n++;
            else if (g.Kind == TacKind.Fire && g.CompanyId == 0 && g.Units.Count > 0 && w.Find(g.Units[0]) is Unit u && IsGun(u)) n++;
        }
        return n;
    }

    /// <summary>재배치 쿨다운 (틱): 기본 × max(1, 포대 수 / 기준).</summary>
    internal long RedeployCooldown(World w, TacFaction s)
    {
        var A = Art(w);
        float mul = MathF.Max(1f, BatteryCount(w, s) / MathF.Max(1f, A.RedeployBatteryRef));
        return Sec(A.RedeployCooldownSec * mul);
    }

    static bool FiredWithin(World w, List<Unit> m, float sec, Func<Unit, bool> which)
    {
        long t = Sec(sec);
        foreach (var u in m) if (which(u) && w.Tick - u.LastShotTick < t) return true;
        return false;
    }

    // ---------------------------------------------------------------- 직사포 (보병포 · 야전포)

    void GunThink(World w, TacFaction s, TacGroup g)
    {
        var A = Art(w);
        float r = float.MaxValue; bool anti = false;
        foreach (var u in mem) if (IsGun(u)) { r = MathF.Min(r, u.Weapon!.Range); anti |= u.Weapon.IsAntiArmor; }
        if (r == float.MaxValue) r = 12f;
        if (r <= A.CloseGunMaxRange && CloseGunThink(w, s, g, r)) return;   // N22 보병포: 대대 동행 · 사격선 바로 뒤 직접 사격
        Unit? e = null;
        if (anti) e = Grid.Nearest(g.Center, TacGrid.Armored);
        if (e == null || Vector2.Distance(e.Pos, g.Center) > r + 15f * K) e = Grid.Nearest(g.Center, TacGrid.Any);
        float d = e != null ? Vector2.Distance(e.Pos, g.Center) : float.MaxValue;
        float eff = r * A.GunEffectiveFrac;
        float standoff = MathF.Min(eff - 1f, MathF.Max(r * A.GunStandoffFrac, s.RifleRange * A.GunStandoffRifleMul));
        float lead = (s.RifleRange > 0 ? s.RifleRange : 16f) * A.GunDeployLead;
        if (e != null && d < r + lead)
        {
            // 쏘는 중 = 유효 사거리 안 표적을 최근 쏜 포가 있음 (먼 표적만 쏘고 있으면 사격 진지로 다가간다 — 먼 사격은 산포 낭비)
            bool firing = FiredWithin(w, mem, A.GunIdleSec, u => IsGun(u) && u.Target is Unit ut && Vector2.Distance(ut.Pos, u.Pos) <= u.Weapon!.Range * A.GunEffectiveFrac);
            bool recent = g.State == TacState.Engage && w.Tick - g.StateTick < RedeployCooldown(w, s);
            if (d > eff && !firing && !recent)
            {
                // 유효 사거리 밖: 사격 진지 (TacMap — 고지 · 사선 가중) 로. 이동 중에는 쏘지 않고, 도착하면 사수 → 자동 방열
                Vector2 toE = SafeNorm(e.Pos - g.Center, g.TaskDir);
                Vector2 p;
                if (g.PickKind == 3 && Vector2.Distance(g.PickBase, e.Pos) <= 6f) p = g.PickResult;
                else
                {
                    p = TacMap.For(w.Map).PickPosition(g.Center, e.Pos, standoff, 16f, s.Profile.Tactics, Grid, r, 1.5f, 1.5f);
                    p = BestCover(w.Map, p, 2, e.Pos);
                    p = BehindInfantry(w, s, p, toE, 0f);   // 중화기는 아군 보병 앞선보다 앞에 두지 않는다 (사용자 규칙 2026-10-19)
                    g.PickKind = 3; g.PickBase = e.Pos; g.PickResult = p;
                }
                bool far = Vector2.Distance(p, g.Center) > (g.State == TacState.Engage ? A.RedeployMinMove : 3f);   // 방열한 포대는 작은 수정으로 옮기지 않는다
                if (far && (g.State != TacState.Approach || Vector2.Distance(p, g.OrderPoint) > 4f))
                {
                    if (!Due(w, g, 5f)) return;   // 명령 간격 — 사수로 세우지 않는다 (옛 동작: 공격 이동 직후 사수 → 방열 → 곧 철수)
                    Order(w, s, g, mem, Ord.Move, p, toE, g.TaskWidth);
                    g.State = TacState.Approach; g.StateTick = w.Tick;
                    return;
                }
                if (g.State == TacState.Approach && w.Tick - g.StateTick < Sec(20) && Vector2.Distance(g.Center, g.OrderPoint) > 2f) return;
            }
            if (g.State != TacState.Engage)
            {
                // tweak1 (사용자 결정 3c): 도착하면 사수 대신 표적에 공격 명령 — 이동 자세의 포가 표적 쪽으로 방열 (보병포 N22 와 같음; 사수 방열은 마지막 이동 방향으로 굳는다)
                g.State = TacState.Engage; g.StateTick = w.Tick; g.FireTargetId = 0; s.FireHolds++;
                if (ArrivalGunTarget(w, g) is { } t0) { ForceGunFire(w, s, g, t0); s.GunFaceDeploys++; }
                else HoldAll(w, s, g, mem);
                return;
            }
            if (BlindRelocate(w, s, g, e, r, standoff)) return;
            GunRangeDiscipline(w, s, A.GunEffectiveFrac);
            return;
        }
        GoAndHold(w, s, g, Ord.AttackMove, TacState.Engage, 4f);
    }

    /// <summary>tweak1 직사포 도착 표적: 포대 중심에서 사거리 × 0.95 안 · 최소 사거리 밖 · 직사 사선이 트인 보이는 적 중 유효 사거리 안을 먼저, 그다음 가까운 순. 없으면 null.</summary>
    Unit? ArrivalGunTarget(World w, TacGroup g)
    {
        float r = float.MaxValue, minR = 0;
        foreach (var u in mem) if (IsGun(u)) { r = MathF.Min(r, u.Weapon!.Range); minR = MathF.Max(minR, u.Weapon.MinRange); }
        if (r == float.MaxValue) return null;
        float eff = r * Art(w).GunEffectiveFrac, reach = r * 0.95f;
        return Grid.BestWhere(g.Center, reach, x =>
        {
            float d = Vector2.Distance(x.Pos, g.Center);
            if (d > reach || d < minR + 0.5f || !w.Map.HasFiringLine(g.Center, x.Pos, 0, x.Id)) return float.NegativeInfinity;
            return (d <= eff ? 100f : 0f) - d;
        });
    }

    /// <summary>보병포가 따라갈 소총 중대: 같은 대대 (없으면 같은 지시 그룹) 의 공격 · 방어 과제 소총 무리 중 포대에서 가장 가까운 것.</summary>
    static TacGroup? SupportedLine(TacFaction s, TacGroup g)
    {
        TacGroup? best = null; float bd = float.MaxValue; bool bestSameBn = false;
        foreach (var lg in s.Groups)
        {
            if (lg.Kind != TacKind.Line || lg.Pending || lg.Alive <= 0 || lg.Task is not (TacTask.Attack or TacTask.Defend) || lg.State == TacState.Fallback) continue;
            bool sameBn = g.Battalion != 0 && lg.Battalion == g.Battalion;
            if (!sameBn && lg.Directive != g.Directive) continue;
            float d = Vector2.DistanceSquared(lg.Center, g.Center);
            if (sameBn && !bestSameBn || sameBn == bestSameBn && d < bd) { best = lg; bd = d; bestSameBn = sameBn; }
        }
        return best;
    }

    /// <summary>
    /// 보병포 (N22, 사용자 결정 2026-10-19 — 대대와 함께 움직이는 근접 지원포): 지원 중대 (SupportedLine) 를 따라가다 (무게중심 closeGunFollow 칸 뒤),
    /// 접촉하면 (보이는 적이 중대에서 사거리 + 소총 사거리 × closeGunContactLead 안) 사격선 바로 뒤 (중대 무게중심에서 적 반대쪽 closeGunBehind 칸, 가장 가까운 적이 유효 사거리 − 2 안이 되게 당김,
    /// 아군 보병 앞선 1칸 뒤 상한) 에 방열하고 기관총 · 포 · 엄폐한 적 · 뭉친 적을 직접 쏜다 (PickCloseGunTarget → 강제 표적). 지원 중대가 없으면 false (옛 직사포 규칙).
    /// </summary>
    bool CloseGunThink(World w, TacFaction s, TacGroup g, float r)
    {
        var A = Art(w);
        var line = SupportedLine(s, g);
        if (line == null) return false;
        float R = s.RifleRange > 0 ? s.RifleRange : 16f;
        float eff = r * A.GunEffectiveFrac;
        Vector2 dir = SafeNorm(g.TaskDir, s.Dir);
        Vector2 lc = line.Center;
        var e = Grid.Nearest(lc, TacGrid.Any);
        bool contact = e != null && Vector2.Distance(e.Pos, lc) < r + R * A.CloseGunContactLead;
        Vector2 p;
        Vector2 toE = dir;
        if (contact)
        {
            toE = SafeNorm(e!.Pos - lc, dir);
            p = lc - toE * A.CloseGunBehind;
            float de = Vector2.Distance(p, e.Pos);
            if (de > eff - 2f) p += toE * (de - (eff - 2f));
            p = BehindInfantry(w, s, p, toE, 1f);
            p = BestCover(w.Map, p, 1, e.Pos);
            if (!w.Map.HasFiringLine(p, e.Pos, 0, e.Id) && FiringSpot(w, s, p, e, 1f, eff - 1f, toE) is Vector2 fp) p = fp;   // 사선이 막힌 칸 (숲 · 고지 · 건물 뒤) 에 서지 않는다
        }
        else p = lc - dir * A.CloseGunFollow;
        p = ClampMap(w.Map, p);
        bool firing = FiredWithin(w, mem, A.CloseGunIdleSec, u => IsGun(u) && u.Target is Unit ut && Vector2.Distance(ut.Pos, u.Pos) <= u.Weapon!.Range * A.GunEffectiveFrac);
        // 쿨다운은 이번 방열 뒤 실제로 쏜 포대만 (행군 중 잠깐 선 포대가 20초 묶여 중대를 놓쳤다)
        bool settled = g.State == TacState.Engage && w.Tick - g.StateTick < Sec(A.CloseGunCooldownSec) && mem.Exists(u => IsGun(u) && u.LastShotTick > g.StateTick);
        bool ahead = contact && Vector2.Dot(g.Center - lc, toE) > 0.5f;   // 사격선보다 앞 (중대가 물러남) - 쏘는 중이어도 뒤로
        bool nearIn = e != null && Vector2.Distance(e.Pos, g.Center) <= eff;
        float dist = Vector2.Distance(p, g.Center);
        if (g.State == TacState.Approach)
        {
            // 가는 중: 목표가 많이 바뀌면 다시 명령 (따라감), 도착 · 막힘 (20초) 이면 방열
            if (dist > 3f && Vector2.Distance(p, g.OrderPoint) > A.CloseGunMove && Due(w, g, 6f))
            { Order(w, s, g, mem, Ord.Move, p, toE, MathF.Min(12f, g.Alive * 2f)); g.StateTick = w.Tick; return true; }
            if (Vector2.Distance(g.Center, g.OrderPoint) > 2f && w.Tick - g.StateTick < Sec(20)) return true;
        }
        else
        {
            bool move = ahead || dist > (g.State == TacState.Engage ? A.CloseGunMove : 3f) && !settled && !(firing && nearIn);
            if (move && Due(w, g, 3f))
            {
                g.FireTargetId = 0;
                Order(w, s, g, mem, Ord.Move, p, toE, MathF.Min(12f, g.Alive * 2f));
                g.State = TacState.Approach; g.StateTick = w.Tick;
                return true;
            }
            if (move) return true;
        }
        if (g.State != TacState.Engage)
        {
            // 도착: 표적이 있으면 사수 대신 바로 공격 명령 — 이동 자세의 포는 표적 쪽으로 방열한다 (사수 방열은 마지막 이동 방향으로 굳어 사계 밖 적을 못 쏘았다)
            g.State = TacState.Engage; g.StateTick = w.Tick; g.FireTargetId = 0; s.FireHolds++;
            if (contact && PickCloseGunTarget(w, s, g, eff) is { } t0) ForceGunFire(w, s, g, t0);
            else HoldAll(w, s, g, mem);
            return true;
        }
        if (!contact) return true;
        if (BlindRelocate(w, s, g, e, r, eff - 2f)) return true;
        var tgt = PickCloseGunTarget(w, s, g, eff);
        if (tgt != null) ForceGunFire(w, s, g, tgt);
        else if (g.FireTargetId != 0) { HoldAll(w, s, g, mem); g.FireTargetId = 0; }
        else GunRangeDiscipline(w, s, A.GunEffectiveFrac);
        return true;
    }

    /// <summary>방열한 직사포 중 유효 사거리 안에 사계 · 직사 사선이 트인 보이는 적이 있는 포가 하나도 없는가.</summary>
    bool GunsBlind(World w, float effFrac)
    {
        bool any = false;
        foreach (var u in mem)
        {
            if (!IsGun(u) || u.CanDeploy && !u.Deployed) continue;
            any = true;
            float ue = u.Weapon!.Range * effFrac;
            if (Grid.NearestWhere(u.Pos, ue, c => Vector2.Distance(c.Pos, u.Pos) >= u.Weapon.MinRange + 0.5f && InGunArc(w, u, c.Pos) && w.Map.HasFiringLine(u.Pos, c.Pos, u.Id, c.Id)) != null) return false;
        }
        return any;
    }

    /// <summary>
    /// 사선 막힘 재배치: 방열 gunBlindSec 넘게 · 그동안 쏜 포 없음 · 유효 사거리 안 보이는 적 e 가 있는데 사선이 트인 적 없음 → 둘레에서 e 가 사선에 드는 칸 (FiringSpot) 으로 이동 (쿨다운 무시). 옮겼으면 true.
    /// </summary>
    bool BlindRelocate(World w, TacFaction s, TacGroup g, Unit? e, float r, float want)
    {
        var A = Art(w);
        if (e == null || g.State != TacState.Engage || w.Tick - g.StateTick < Sec(A.GunBlindSec) || !Due(w, g, A.GunBlindSec)) return false;
        if (FiredWithin(w, mem, A.GunBlindSec, IsGun)) return false;
        if (Vector2.Distance(e.Pos, g.Center) > r * A.GunEffectiveFrac + 4f || !GunsBlind(w, A.GunEffectiveFrac)) return false;
        Vector2 toE = SafeNorm(e.Pos - g.Center, g.TaskDir);
        if (FiringSpot(w, s, g.Center, e, 1f, MathF.Max(4f, want), toE) is not Vector2 p || Vector2.Distance(p, g.Center) < 1.5f) return false;
        g.FireTargetId = 0;
        Order(w, s, g, mem, Ord.Move, p, toE, MathF.Min(12f, g.Alive * 2f));
        g.State = TacState.Approach; g.StateTick = w.Tick;
        s.GunLosMoves++;
        return true;
    }

    /// <summary>
    /// c 둘레 gunLosSearch 칸에서 적 e 까지 직사 사선이 트이고 e 와 거리 [minD, maxD] 인 칸 중 c 에 가장 가까운 칸 (아군 보병 앞선 back 칸 뒤 — 넘는 칸 제외). 동점이면 먼저 훑은 칸. 없으면 null.
    /// </summary>
    Vector2? FiringSpot(World w, TacFaction s, Vector2 c, Unit e, float back, float maxD, Vector2 dir)
    {
        var A = Art(w);
        int rad = Math.Max(1, A.GunLosSearch);
        var (cx, cy) = GameMap.TileOf(c);
        float front = float.MinValue;
        if (dir.LengthSquared() > 1e-4f)
            foreach (var u in w.Units)
                if (u.Owner == s.Faction && u.Alive && u.Def.IsInfantry && !u.Def.Heavy && !u.IsOfficer && !u.Def.Scout) front = MathF.Max(front, Vector2.Dot(u.Pos, dir));
        float best = float.MaxValue; Vector2? bp = null;
        for (int dy = -rad; dy <= rad; dy++)
        for (int dx = -rad; dx <= rad; dx++)
        {
            float dd = dx * dx + dy * dy;
            if (dd > rad * rad || dd >= best) continue;
            int x = cx + dx, y = cy + dy;
            if (!w.Map.IsWalkable(x, y)) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float de = Vector2.Distance(q, e.Pos);
            if (de > maxD || de < 2f) continue;
            if (front != float.MinValue && Vector2.Dot(q, dir) > front - back) continue;
            if (!w.Map.HasFiringLine(q, e.Pos, 0, e.Id)) continue;
            best = dd; bp = q;
        }
        return bp;
    }

    /// <summary>보병포 표적 점수 (유효 사거리 밖 · 최소 사거리 안 · 포대 중심에서 직사 사선 없음 = 후보 아님).</summary>
    float CloseGunScore(World w, TacGroup g, Unit e, float eff, float minR)
    {
        var A = Art(w);
        float d = Vector2.Distance(e.Pos, g.Center);
        if (d > eff || d < minR + 0.5f || !w.Map.HasFiringLine(g.Center, e.Pos, 0, e.Id)) return float.NegativeInfinity;
        float sc = -A.CloseGunDistW * d;
        if (e.Weapon is { Spray: > 0 }) sc += A.CloseGunMgW;
        if (e.Def.Category == "gun") sc += A.CloseGunGunW;
        if (e.InTrench || w.Map.TerrainCover(e.Pos).Hit < 1f) sc += A.CloseGunCoverW;
        sc += A.CloseGunClusterW * (Grid.Stats(e.Pos, 2f).Count - 1);
        return sc;
    }

    /// <summary>보병포 강제 표적: 지금 표적이 유효하면 closeGunRetargetSec 동안 유지, 그 뒤 더 나은 표적이 closeGunKeep 점 넘게 좋을 때만 바꿈. 후보 없으면 null.</summary>
    Unit? PickCloseGunTarget(World w, TacFaction s, TacGroup g, float eff)
    {
        var A = Art(w);
        float minR = 0;
        foreach (var u in mem) if (IsGun(u)) minR = MathF.Max(minR, u.Weapon!.MinRange);
        Unit? cur = g.FireTargetId != 0 && w.Find(g.FireTargetId) is Unit c && c.Alive && w.Fog.CanSee(s.Faction, c) ? c : null;
        float cs = cur != null ? CloseGunScore(w, g, cur, eff, minR) : float.NegativeInfinity;
        if (float.IsNegativeInfinity(cs)) cur = null;
        if (cur != null && !Due(w, g, A.CloseGunRetargetSec)) return cur;
        var best = Grid.BestWhere(g.Center, eff, x => CloseGunScore(w, g, x, eff, minR));
        if (best == null) return cur;
        if (cur != null && best != cur && CloseGunScore(w, g, best, eff, minR) <= cs + A.CloseGunKeep) return cur;
        return best;
    }

    /// <summary>보병포 강제 사격: 방열 · 사거리 · 사계 · 사선 안 포에만 공격 명령 (공격 명령은 추격하므로). 같은 표적이면 아직 겨누지 않은 포에만.</summary>
    void ForceGunFire(World w, TacFaction s, TacGroup g, Unit tgt)
    {
        sub.Clear(); sub2.Clear();
        foreach (var u in mem)
        {
            if (!IsGun(u) || u.Deploy is DeployPhase.Deploying or DeployPhase.Packing) continue;
            float dd = Vector2.Distance(u.Pos, tgt.Pos);
            if (u.Weapon != null && w.AlliesNearImpact(u, tgt, u.Weapon, dd, aiForced: true)) { if (u.Order == UnitOrder.Attack && u.Target == tgt) sub2.Add(u); continue; }   // 포수 규율 (pursuit2): 아군이 탄착 둘레에 있으면 강제 사격 안 함 · 이미 쏘는 포는 멈춤
            if (u.Target == tgt) continue;
            if (dd > u.Weapon!.Range * 0.95f || dd < u.Weapon.MinRange + 0.5f || !w.Map.HasFiringLine(u.Pos, tgt.Pos, u.Id, tgt.Id)) continue;
            // 이동 자세 = 표적 쪽으로 방열, 방열했는데 사계 밖 (포가 선회 문턱 넘음) = 한동안 못 쏜 포만 재방열 (공격 명령이 철수 · 회전 · 방열)
            if (u.Deployed && !InGunArc(w, u, tgt.Pos) && w.Tick - u.LastShotTick < Sec(Art(w).CloseGunIdleSec)) continue;
            sub.Add(u);
        }
        if (sub2.Count > 0) { w.Hold(s.Faction, sub2); s.Orders++; }   // 포수 규율: 아군이 다가간 표적에 쏘던 포 멈춤
        bool changed = g.FireTargetId != tgt.Id;
        g.FireTargetId = tgt.Id;
        if (changed) g.OrderTick = w.Tick;
        if (sub.Count == 0) return;
        w.Attack(s.Faction, sub, tgt);
        s.Orders++;
    }

    /// <summary>
    /// 사거리 규율: 방열한 직사포가 유효 사거리 (사거리 × effFrac) 밖 표적을 막 쏘았고 (재장전 중) 유효 사거리 안에 쏠 수 있는 적 (사계 · 사선) 이 있으면 표적을 놓는다 (사수) —
    /// 엔진은 표적이 유효한 동안 바꾸지 않으므로 (먼저 잡은 먼 표적을 계속 쏨), 놓으면 가까운 적을 다시 고른다. 가까운 적이 없으면 먼 표적을 그대로 쏜다.
    /// </summary>
    void GunRangeDiscipline(World w, TacFaction s, float effFrac)
    {
        sub.Clear();
        long just = Sec(1.5f);
        foreach (var u in mem)
        {
            if (!IsGun(u) || u.CanDeploy && !u.Deployed || u.Target is not Unit t || w.Tick - u.LastShotTick >= just) continue;
            float ue = u.Weapon!.Range * effFrac;
            if (Vector2.Distance(u.Pos, t.Pos) <= ue) continue;
            var near = Grid.NearestWhere(u.Pos, ue, c => c != t && Vector2.Distance(c.Pos, u.Pos) >= u.Weapon.MinRange + 0.5f && InGunArc(w, u, c.Pos)
                                                        && w.Map.HasFiringLine(u.Pos, c.Pos, u.Id, c.Id));
            if (near != null) sub.Add(u);
        }
        if (sub.Count == 0) return;
        w.Hold(s.Faction, sub);
        s.Orders++;
    }

    // ---------------------------------------------------------------- 곡사포 (주공 연대 지원: 준비사격 · 돌격 지원 · 연신)

    void ArtilleryThink(World w, TacFaction s, TacGroup g)
    {
        var S = s.Profile.Strategy;
        var A = Art(w);
        WeaponDef? wpn = null;
        foreach (var u in mem) if (World.IsIndirect(u.Weapon)) { wpn = u.Weapon; break; }
        if (wpn == null) { GoAndHold(w, s, g, Ord.Move, TacState.Engage); return; }
        if (wpn.SpotterOnly) RetaskObservers(w, s, g);
        if (S.Artillery < 0.2f || g.Task is TacTask.Reserve or TacTask.Fallback)
        {
            if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
            return;
        }
        TacRegiment? reg = null;
        if (g.Supports != 0) foreach (var r in s.Regiments) if (r.Id == g.Supports) { reg = r; break; }
        bool spotted = s.HasSpotted && w.Tick - s.SpottedTick < Sec(10);
        // 진지 기준점: 지원 연대의 화력 표적 (준비사격 · 연신) > 관측 전용이면 관측 표적, 아니면 보이는 가장 가까운 적
        Vector2? tp = null;
        if (reg is { HasFireTarget: true }) tp = reg.FireTarget;
        else if (wpn.SpotterOnly) { if (spotted) tp = s.SpottedPos; }
        else if (Grid.Nearest(g.Center, TacGrid.Any) is { } e) tp = e.Pos;
        float minR = wpn.MinRange, R = wpn.Range;
        Vector2 dir = SafeNorm(g.TaskDir, s.Dir);
        long cool = RedeployCooldown(w, s);
        // 재배치 쿨다운: 방열 (Engage) 뒤 cool 동안 · 이 포대가 최근 쏘는 중이면 옮기지 않는다 (제자리 선회로 해결)
        bool settled = g.State == TacState.Engage && w.Tick - g.StateTick < cool;
        bool firing = FiredWithin(w, mem, A.GunIdleSec, u => World.IsIndirect(u.Weapon));
        if (tp is Vector2 t)
        {
            float d = Vector2.Distance(g.Center, t);
            // 소총 사거리 노출 (supp2): 방열한 포가 하나도 없고 보이는 적이 exposedStandoff 안이면 물러나서 방열 (그 적에서 exposedRetreat 칸)
            bool anyDeployed = false;
            foreach (var u in mem) if (World.IsIndirect(u.Weapon) && u.CanDeploy && u.Deployed) { anyDeployed = true; break; }   // 방열을 마친 포가 있으면 그대로 쏜다 (방열 중이면 접고 물러남)
            Unit? near = A.ExposedStandoff > 0 && !anyDeployed ? Grid.Nearest(g.Center, TacGrid.Any) : null;
            bool exposed = near != null && Vector2.Distance(near.Pos, g.Center) < A.ExposedStandoff;
            bool tooClose = d < minR + 2f || exposed;
            if (d > R * A.RedeployRangeFrac || tooClose)
            {
                float want = MathF.Max(R * A.RedeployWantFrac, minR + 4f);   // 한 번에 깊이 들어가 다음 이동까지 오래 쏜다 (옛 0.75)
                Vector2 p = t - SafeNorm(t - g.Center, dir) * want;
                if (exposed && d >= minR + 2f) p = near!.Pos - SafeNorm(near.Pos - g.Center, dir) * MathF.Max(A.ExposedRetreat, minR + 4f);
                float ahead = Vector2.Dot(p - g.TaskPoint, dir);   // 전선 (과제 위치) 보다 앞으로 나가지 않는다
                if (ahead > -4f) p -= dir * (ahead + 4f);
                p = BehindInfantry(w, s, p, dir, 8f);   // 곡사포는 아군 보병 앞선 8칸 뒤 (중화기 규칙)
                bool hold = !tooClose && (settled || firing && d <= R * 0.92f);   // 쏘는 중이라도 표적 지역이 사거리 끝 (0.92) 밖이면 (쿨다운 뒤) 옮긴다
                float need = g.State == TacState.Engage ? A.RedeployMinMove : 3f;
                if (!hold && Vector2.Distance(p, g.Center) > need && Due(w, g, 8f))
                {
                    Order(w, s, g, mem, Ord.Move, p, SafeNorm(t - p, dir), MathF.Min(12f, g.Alive * 2f));   // 도착 방향 = 표적 쪽 (방열 사계 안에 들도록)
                    g.State = TacState.Approach; g.StateTick = w.Tick; s.ArtilleryMoves++; g.FireTargetId = 0;
                    return;
                }
                if (g.State == TacState.Approach && w.Tick - g.StateTick < Sec(20) && Vector2.Distance(g.Center, g.OrderPoint) > 2f) return;
            }
            // 사계 밖 표적: 방열한 포 다수가 포가를 돌려도 (문턱 이상) 표적 쪽을 겨눌 수 없고 쿨다운 (≥ 15초) 넘게 그랬으면, 제자리에서 표적 쪽으로 다시 정렬 (철수 → 회전 → 재방열)
            if (g.State == TacState.Engage && w.Tick - g.StateTick > Math.Max(Sec(15), cool) && !firing && Due(w, g, 15f))
            {
                int outside = 0, n = 0;
                foreach (var u in mem) if (u.CanDeploy && World.IsIndirect(u.Weapon)) { n++; if (!InGunArc(w, u, t)) outside++; }
                if (n > 0 && outside * 2 > n)
                {
                    Order(w, s, g, mem, Ord.Move, g.Center, SafeNorm(t - g.Center, dir), MathF.Min(12f, g.Alive * 2f));
                    g.State = TacState.Approach; g.StateTick = w.Tick; s.ArtilleryMoves++; g.FireTargetId = 0;
                    return;
                }
            }
            if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; g.FireTargetId = 0; return; }
            // 표적: 돌격 · 약진 표적 둘레 > 연신 표적 > (자동 표적이 근접 위험이면) 안전한 표적 > 엔진 자동 표적 (관측된 가장 가까운 적 = 적 전선 — 준비사격)
            // (실험: 준비사격 중 매번 강제 공격 명령은 명령마다 반응 지연 · 관측이 끊긴 표적에 묶여 포격 수가 절반 이하 → 이유가 있을 때만 강제)
            var pick = PickArtilleryTarget(w, s, g, wpn, reg);
            if (pick != null) { ForceFireAt(w, s, g, wpn, pick); return; }
            if (g.FireTargetId != 0) { HoldAll(w, s, g, mem); g.FireTargetId = 0; }   // 강제할 이유가 없다 → 엔진 자동 표적
            return;
        }
        if (g.FireTargetId != 0) { HoldAll(w, s, g, mem); g.FireTargetId = 0; }
        // 관측 표적 없음: 전선 뒤 (사거리 절반) 에서 방열해 둔다
        Vector2 home = ClampMap(w.Map, g.TaskPoint - dir * (R * 0.5f));
        if (Vector2.Distance(g.Center, home) > MathF.Max(8f, A.RedeployMinMove) && !settled && Due(w, g, 8f) && (g.State != TacState.Approach || Vector2.Distance(home, g.OrderPoint) > 8f))
        {
            Order(w, s, g, mem, Ord.Move, home, dir, MathF.Min(12f, g.Alive * 2f));
            g.State = TacState.Approach; g.StateTick = w.Tick;
            return;
        }
        if (g.State == TacState.Approach && w.Tick - g.StateTick < Sec(20) && Vector2.Distance(g.Center, g.OrderPoint) > 2f) return;
        if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; }
    }

    /// <summary>
    /// 곡사포 강제 표적 (없으면 null = 엔진 자동 표적): ① 아군 돌격 · 약진 표적 (SupportPoints) 둘레 supportRadius 안 (돌격 > 기동조 > 약진, 포대에서 가까운 순)
    /// ② 방열한 포의 자동 표적이 과반 근접 위험이면 연신 — 연대 연신 표적 (Lifted · FireTarget) 둘레, 없으면 포대에서 가장 가까운 안전한 적. 후보 = 보이는 적 중 사거리 안 · 관측 (관측 전용이면) · 방열한 포 하나 이상이 제자리에서 겨눌 수 있음 · 근접 위험 밖.
    /// </summary>
    Unit? PickArtilleryTarget(World w, TacFaction s, TacGroup g, WeaponDef wpn, TacRegiment? reg)
    {
        var A = Art(w);
        Vector2 c = g.Center;
        bool Valid(Unit e)
        {
            float d = Vector2.Distance(e.Pos, c);
            if (d > wpn.Range * 0.95f || d < wpn.MinRange + 1f) return false;
            if (wpn.SpotterOnly && !w.SeenBySpotter(s.Faction, e)) return false;
            foreach (var u in mem) if (World.IsIndirect(u.Weapon) && (!u.CanDeploy || u.Deployed) && InGunArc(w, u, e.Pos)) return true;
            return false;
        }
        bool Safe(Unit e) => !DangerClose(w, s, e.Pos, A.DangerClose + A.DangerScatterMul * World.ScatterAt(wpn, Vector2.Distance(e.Pos, c)));
        Unit? Near(Vector2 p, float rad)
        {
            // 가까운 후보부터: 근접 위험이면 다음 후보 (최대 4번)
            Unit? skip1 = null, skip2 = null, skip3 = null;
            for (int k = 0; k < 4; k++)
            {
                var e = Grid.NearestWhere(p, rad, x => x != skip1 && x != skip2 && x != skip3 && Valid(x));
                if (e == null) return null;
                if (Safe(e)) return e;
                if (k == 0) skip1 = e; else if (k == 1) skip2 = e; else skip3 = e;
            }
            return null;
        }
        supportBuf.Clear();
        SupportPoints(w, s, supportBuf);
        // 이미 강제한 표적이 아직 유효하고 안전 (근접 위험 반경 × 0.8 — 경계에서 0.5초마다 풀고 다시 잡지 않게) 하면 그대로:
        // 표적을 바꾸거나 풀 때마다 명령 반응 지연 · 조준이 다시 시작된다 (실험: 번갈아 풀면 곡사포 살상이 1/3 줄었다)
        if (g.FireTargetId != 0 && w.Find(g.FireTargetId) is Unit cur && cur.Alive && Valid(cur)
            && !DangerClose(w, s, cur.Pos, 0.8f * (A.DangerClose + A.DangerScatterMul * World.ScatterAt(wpn, Vector2.Distance(cur.Pos, c)))))
            return cur;
        // ① 돌격 · 약진 지원 (포대에서 가까운 표적부터)
        supportOrder.Clear();
        for (int i = 0; i < supportBuf.Count; i++) supportOrder.Add(i);
        supportOrder.Sort((a, b) => { int r = supportPri[a].CompareTo(supportPri[b]); return r != 0 ? r : Vector2.DistanceSquared(supportBuf[a], c).CompareTo(Vector2.DistanceSquared(supportBuf[b], c)); });
        foreach (int i in supportOrder) if (Near(supportBuf[i], A.SupportRadius) is { } e1) return e1;
        // ①-2 대포병 (T10, FieldOps.cs — 기본 끔): 탐지된 적 포 중 쏠 수 있는 · 안전한 가장 가까운 것
        if (w.CbMarks.Count > 0 && FieldOpsDefs.Current.CounterBattery.AiPriority)
        {
            Unit? cb = null; float cbd = float.MaxValue;
            foreach (var m in w.CbMarks)
            {
                if (m.Until < w.Tick || (m.Faction != s.Faction && !w.Allied(s.Faction, m.Faction))) continue;
                if (w.Find(m.GunId) is not Unit e || !Valid(e) || !Safe(e)) continue;
                float d2 = Vector2.DistanceSquared(e.Pos, c);
                if (d2 < cbd) { cbd = d2; cb = e; }
            }
            if (cb != null) { w.Stats.CbAiTargets++; return cb; }
        }
        // ② 자동 표적이 과반 근접 위험 (아군이 다가감) → 연신: 연대가 연신했으면 연대 화력 표적 (깊은 표적) 둘레, 아니면 포대에서 가장 가까운 안전한 적.
        //    (엔진에 아군 오사가 없어 연신은 화력 손해 — 실제로 아군이 위험 거리 안에 들 때만 옮긴다)
        int bad = 0, n = 0;
        foreach (var u in mem)
        {
            if (!World.IsIndirect(u.Weapon) || u.Target is not Unit t) continue;
            n++;
            if (t.Id == g.FireTargetId) continue;
            if (!Safe(t)) bad++;
        }
        if (n > 0 && bad * 2 > n)
        {
            if (reg is { HasFireTarget: true, Lifted: true } && Near(reg.FireTarget, 14f * K) is { } e2) return e2;
            if (Near(c, wpn.Range * 0.95f) is { } e3) return e3;
        }
        return null;
    }

    [NonSerialized] readonly List<Vector2> supportBuf = new();
    [NonSerialized] readonly List<int> supportPri = new(), supportOrder = new();

    /// <summary>
    /// 아군 돌격 · 약진 표적: 공격 과제 소총 중대 중 틀 행동 (Drill) 이 약진 (BoundSquad) · 기동조 (Man 1/2/4) 중이면 그 적 위치 (Drill.Enemy),
    /// 틀 없는 중대가 약진 · 돌격 상태면 중대에서 가장 가까운 보이는 적.
    /// </summary>
    void SupportPoints(World w, TacFaction s, List<Vector2> into)
    {
        float R = s.RifleRange > 0 ? s.RifleRange : 16f;
        supportPri.Clear();
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Line || g.Pending || g.Alive <= 0 || g.Task != TacTask.Attack) continue;
            var co = g.CompanyId > 0 ? w.FindCompany(g.CompanyId) : null;
            if (co != null && co.Drill.Phase == DrillPhase.Engage && co.Drill.Enemy != Vector2.Zero && (co.Drill.BoundSquad > 0 || co.Drill.Man is 1 or 2 or 4))
            { into.Add(co.Drill.Enemy); supportPri.Add(co.Drill.Man == 4 ? 0 : co.Drill.Man is 1 or 2 ? 1 : 2); continue; }   // 우선: 돌격 > 기동조 > 약진
            if (g.State is TacState.Bound or TacState.Assault or TacState.Charge || g.SquadBound != 0)
                if (Grid.Nearest(g.Center, TacGrid.Any) is { } e && Vector2.Distance(e.Pos, g.Center) < R * 2f) { into.Add(e.Pos); supportPri.Add(g.State is TacState.Assault or TacState.Charge ? 0 : 2); }
        }
    }

    /// <summary>아군 (자기 세력) 유닛이 p 에서 rad 칸 안에 있는가.</summary>
    static bool DangerClose(World w, TacFaction s, Vector2 p, float rad)
    {
        float r2 = rad * rad;
        foreach (var u in w.UnitsOf(s.Faction)) if (u.Alive && Vector2.DistanceSquared(u.Pos, p) < r2) return true;
        return false;
    }

    /// <summary>연대 준비사격 국면 끝 조건: 지원 포대가 이 국면에 들어서 실제로 쏘았다 (관측이 없으면 연대가 시간 상한으로 끝낸다).</summary>
    bool PrepFired(World w, TacFaction s, TacGroup g, TacRegiment r)
    {
        Members(w, s, g, buf);
        foreach (var u in buf) if (World.IsIndirect(u.Weapon) && u.LastShotTick >= r.PhaseTick) return true;
        return false;
    }

    /// <summary>
    /// 강제 표적 tgt 를 방열을 마치고 사계 · 사거리 안에 둔 포에만 공격 명령 (공격 명령은 추격하므로 사거리 밖 포에는 내리지 않는다).
    /// 묶인 표적이 무효 (죽음 · 관측 끊김 · 사거리 밖) 면 먼저 푼다. 같은 표적이면 4초마다, 아직 그 표적을 겨누지 않은 포에만 (명령마다 반응 지연).
    /// </summary>
    void ForceFireAt(World w, TacFaction s, TacGroup g, WeaponDef wpn, Unit tgt)
    {
        if (g.FireTargetId != 0 && g.FireTargetId != tgt.Id && (w.Find(g.FireTargetId) is not Unit old || !old.Alive || wpn.SpotterOnly && !w.SeenBySpotter(s.Faction, old)
                                    || Vector2.Distance(old.Pos, g.Center) > wpn.Range * 0.97f))
        { HoldAll(w, s, g, mem); g.FireTargetId = 0; return; }
        if (g.FireTargetId == tgt.Id && !Due(w, g, 4f)) return;
        sub.Clear(); sub2.Clear();
        foreach (var u in mem)
        {
            if (!World.IsIndirect(u.Weapon) || u.CanDeploy && !u.Deployed) continue;
            float dd = Vector2.Distance(u.Pos, tgt.Pos);
            if (w.AlliesNearImpact(u, tgt, u.Weapon!, dd, aiForced: true)) { if (u.Order == UnitOrder.Attack && u.Target == tgt) sub2.Add(u); continue; }   // 포수 규율 (pursuit2): 아군이 탄착 둘레에 있으면 강제 사격 안 함 · 이미 쏘는 포는 멈춤
            if (u.Target == tgt) continue;
            if (dd > u.Weapon!.Range * 0.97f || dd < u.Weapon.MinRange + 0.5f || !InGunArc(w, u, tgt.Pos)) continue;
            sub.Add(u);
        }
        if (sub2.Count > 0) { w.Hold(s.Faction, sub2); s.Orders++; }   // 포수 규율: 아군이 다가간 표적에 쏘던 포 멈춤
        if (sub.Count == 0) { g.FireTargetId = tgt.Id; g.OrderTick = w.Tick; return; }
        w.Attack(s.Faction, sub, tgt);
        g.FireTargetId = tgt.Id; g.OrderTick = w.Tick;
        s.Orders++;
    }

    /// <summary>방열한 포가 철수 없이 지점 p 를 쏠 수 있는가 (사계 안, 또는 방열한 채 포가를 문턱 미만으로 돌려서 — N10) — 사계 규칙의 단일 출처 `World.CanTraverseInPlace` (묶여 있지 않으면 참). AI 의 유일한 호출 지점.</summary>
    static bool InGunArc(World w, Unit u, Vector2 p) => World.CanTraverseInPlace(u, p);

    // ---------------------------------------------------------------- 관측자 (관측반 · 정찰 분견대)

    /// <summary>
    /// 관측자 배치: 소속 중대 정찰조 (Unit.ScoutCompany — 엔진 자율 정찰) 가 아닌 정찰 무리 구성원을 한 명씩 관측 지점으로.
    /// 지점 = 아군 소총 보병 앞선 + observerAhead (가로는 아군 전선 폭에 고르게), 보이는 가장 가까운 적과 소총 사거리 × observerStandoff 이상 (아니면 뒤로),
    /// 둘레 엄폐 · 고지. 지점에서 관측 (사수) — 돌격을 이끌지 않는다 (적 소총 사거리 밖에 머묾). 관측자가 죽으면 남은 관측자로 가로 칸을 다시 나눈다.
    /// </summary>
    void ObserverThink(World w, TacFaction s, TacGroup g)
    {
        mem.RemoveAll(u => u.ScoutCompany != null);
        if (mem.Count == 0) return;
        var A = Art(w);
        if (!Due(w, g, A.ObserverSec)) return;
        Vector2 dir = SafeNorm(g.TaskDir, s.Dir), side = Perp(dir);
        // 전선 = 같은 지시 그룹 소총 중대 무게중심의 앞선 (낱 병사 최대값은 돌격한 한 중대에 끌려 관측자를 적 속으로 보냈다), 가로 = 중대 폭 포함 범위
        float front = float.MinValue, lo = float.MaxValue, hi = float.MinValue;
        foreach (var lg in s.Groups)
        {
            if (lg.Kind != TacKind.Line || lg.Pending || lg.Alive <= 0 || lg.Directive != g.Directive || lg.Task is TacTask.Reserve or TacTask.Fallback) continue;
            float a = Vector2.Dot(lg.Center, dir), l = Vector2.Dot(lg.Center, side), hw = Frontage(lg) / 2f;
            front = MathF.Max(front, a); lo = MathF.Min(lo, l - hw); hi = MathF.Max(hi, l + hw);
        }
        if (front == float.MinValue) { front = Vector2.Dot(g.TaskPoint, dir); lo = hi = Vector2.Dot(g.TaskPoint, side); }
        float R = s.RifleRange > 0 ? s.RifleRange : 16f;
        float standoff = R * A.ObserverStandoff;
        int n = mem.Count;
        bool any = false;
        for (int i = 0; i < n; i++)
        {
            var u = mem[i];
            if (u.ScoutState == ScoutState.Cover) continue;   // N22: 노려져서 엄폐 중 (엔진 - Scouts.cs), 조용해지면 다시 보낸다
            float lat = n == 1 ? (lo + hi) / 2f : lo + (hi - lo) * (i + 0.5f) / n;
            Vector2 p = dir * (front + A.ObserverAhead) + side * lat;
            // 접촉 (지점 둘레 standoff + 앞 거리 안에 보이는 적) 이면 전선 뒤로 — 관측자는 돌격을 이끌지 않고 아군 전선 뒤에서 본다 (시야 ×1.5)
            if (Grid.Stats(p, standoff + A.ObserverAhead).Count > 0) p = dir * (front - A.ObserverContactBack) + side * lat;
            // 보이는 적 standoff 안이면 그 적들 무게중심에서 멀어지는 쪽으로 (가장 가까운 적만 보면 다른 적 쪽으로 밀려 들어갔다)
            for (int k = 0; k < 6; k++)
            {
                var st = Grid.Stats(p, standoff);
                if (st.Count == 0) break;
                Vector2 away = SafeNorm(p - st.Sum / st.Count, -dir);
                var nn = Grid.Nearest(p, TacGrid.Any);
                float de = nn != null ? Vector2.Distance(nn.Pos, p) : standoff;
                p += away * MathF.Max(2f, standoff - de + 1f);
            }
            var e = Grid.Nearest(p, TacGrid.Any);
            p = ClampMap(w.Map, p);
            p = BestCover(w.Map, p, A.ObserverCoverRadius, e?.Pos ?? p + dir * 20f);
            // tweak1: 노려져 엄폐했던 관측자는 같은 노출 지점으로 돌려보내지 않는다 (D 관측반 0/24 의 원인 — 엄폐 뒤 같은 지점으로 다시 보냄)
            if (u.ScoutExposedTick >= 0 && w.Tick - u.ScoutExposedTick < Sec(A.ObserverAvoidSec)) { p = AvoidExposure(w, u, p, A, standoff, out bool moved); if (moved) s.ObserverAvoids++; }
            // 적이 이미 소총 사거리 안이면 바로 물러남 (지점 재계산이 이미 뒤로 뺐다)
            bool threatened = Grid.Nearest(u.Pos, TacGrid.Any) is { } ne && Vector2.Distance(ne.Pos, u.Pos) < R * 1.05f;
            Vector2 cur = u.Group != null && u.Order is UnitOrder.Move ? u.Group.Goal : u.Pos;
            bool need = Vector2.Distance(cur, p) > A.ObserverRepost || threatened && Vector2.Distance(cur, p) > 2f;
            if (!need) continue;
            w.Move(s.Faction, new[] { u }, p);
            any = true;
        }
        g.OrderTick = w.Tick;
        if (any) { s.Orders++; g.State = TacState.Advance; g.StateTick = w.Tick; }
        else if (g.State != TacState.Engage) { g.State = TacState.Engage; g.StateTick = w.Tick; }
    }

    /// <summary>
    /// tweak1 관측자 노출 회피: 지점 p 가 마지막 노출 지점 (Unit.ScoutExposed) 의 observerAvoidRadius 안이거나, 그때 사격자 위치 (ScoutThreat) 에
    /// (노출 거리 + observerAvoidStandoff) 보다 가까우면, 둘레 observerAvoidSearch 칸에서 두 조건을 지키는 칸 중 점수 최대로 바꾼다:
    /// 엄폐 · 은폐 (명중 배수 &lt; 1) +15, 사격자 사선 밖 +25, 보이는 적이 standoff 안 −30, p 에서 거리 × 0.5 감점. 후보가 없으면 사격자에게서 곧장 물러난 점.
    /// p 가 이미 조건을 지키고 엄폐 · 사선 밖이면 그대로.
    /// </summary>
    internal static Vector2 AvoidExposure(World w, Unit u, Vector2 p, ArtilleryAiDef A, float standoff, out bool moved)
    {
        moved = false;
        Vector2 X = u.ScoutExposed, T = u.ScoutThreat;
        float need = Vector2.Distance(X, T) + A.ObserverAvoidStandoff, avoid2 = A.ObserverAvoidRadius * A.ObserverAvoidRadius;
        bool Ok(Vector2 q) => Vector2.DistanceSquared(q, X) >= avoid2 && Vector2.Distance(q, T) >= need;
        bool Hidden(Vector2 q) => w.Map.TerrainCover(q).Hit < 1f || !w.Map.HasFiringLine(T, q, 0, u.Id);
        if (Ok(p) && Hidden(p)) return p;
        int rad = Math.Max(1, A.ObserverAvoidSearch);
        var (cx, cy) = GameMap.TileOf(p);
        float best = float.MinValue; Vector2? bp = null;
        for (int dy = -rad; dy <= rad; dy++)
        for (int dx = -rad; dx <= rad; dx++)
        {
            if (dx * dx + dy * dy > rad * rad) continue;
            int x = cx + dx, y = cy + dy;
            if (!w.Map.IsWalkable(x, y) || w.Map.Get(x, y) is TerrainType.Water or TerrainType.Wire) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            if (!Ok(q)) continue;
            float sc = -0.5f * MathF.Sqrt(dx * dx + dy * dy);
            if (w.Map.TerrainCover(q).Hit < 1f) sc += 15f;
            if (!w.Map.HasFiringLine(T, q, 0, u.Id)) sc += 25f;
            if (Grid.Stats(q, standoff).Count > 0) sc -= 30f;
            if (sc > best) { best = sc; bp = q; }
        }
        moved = true;
        if (bp is Vector2 b) return b;
        return ClampMap(w.Map, T + SafeNorm(p - T, SafeNorm(X - T, Vector2.UnitY)) * MathF.Max(need, Vector2.Distance(p, T)));
    }

    /// <summary>[테스트] 보이는 적 격자를 비우고 노출 회피 지점을 구한다.</summary>
    internal static Vector2 TestAvoidExposure(World w, Unit u, Vector2 p, float standoff)
    {
        Grid.Build(w.Map, new List<Unit>());
        return AvoidExposure(w, u, p, w.Defs.Tactics.ArtilleryAi, standoff, out _);
    }

    /// <summary>
    /// 관측자 재배치: 관측 전용 포 (곡사포) 가 있는데 AI 가 부릴 관측자 (정찰 무리 — 소속 중대 정찰조가 아닌) 가 하나도 없으면,
    /// 소총 중대 정찰조 (예비 과제 → 가장 뒤 중대 순) 에서 observerRetask 명을 골라 새 관측반 중대로 편성 (FormCompany — 정찰병만이면 평범한 중대).
    /// 편성 대기 무리는 다음 호출에 중대 Id 로 바꾼다.
    /// </summary>
    void RetaskObservers(World w, TacFaction s, TacGroup arty)
    {
        var A = Art(w);
        if (A.ObserverRetask <= 0) return;
        bool have = false;
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Scout) continue;
            if (g.Pending)
            {
                foreach (int id in g.Units)
                    if (w.Find(id) is Unit u && u.Company is { } co && co.Owner == s.Faction && co.Members.Contains(u)) { g.CompanyId = co.Id; break; }
                if (g.CompanyId != 0) { g.Pending = false; g.Units.Clear(); }
                else if (w.Tick - g.StateTick > Sec(5)) { g.Pending = false; }   // 편성 실패 → 분견대로
                have = true;
                continue;
            }
            Members(w, s, g, buf);
            foreach (var u in buf) if (u.ScoutCompany == null) { have = true; break; }
            if (have) break;
        }
        if (have) return;
        if (!s.HasEnemy || s.HasSpotted && w.Tick - s.SpottedTick < Sec(10)) return;   // 아직 적을 모름 · 관측이 살아 있음 (소총 중대 정찰조 · 장교도 관측자) → 그대로
        Vector2 dir = SafeNorm(arty.TaskDir, s.Dir);
        TacGroup? donor = null; float best = float.MaxValue;
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Line || g.Pending || g.CompanyId == 0 || g.Directive != arty.Directive) continue;
            var co = w.FindCompany(g.CompanyId);
            if (co == null || !co.Scouts.Exists(u => u.Alive)) continue;
            float sc = Vector2.Dot(g.Center, dir) - (g.Task == TacTask.Reserve ? 10000f : 0f);
            if (sc < best) { best = sc; donor = g; }
        }
        if (donor == null || w.FindCompany(donor.CompanyId) is not { } dc) return;
        var pick = dc.Scouts.Where(u => u.Alive && u.Owner == s.Faction).OrderBy(u => u.Id).Take(A.ObserverRetask).ToList();
        if (pick.Count == 0) return;
        w.FormCompany(s.Faction, pick);
        var ng = new TacGroup { Id = s.NextGroupId++, Kind = TacKind.Scout, Pending = true, Directive = arty.Directive, Initial = pick.Count, Alive = pick.Count, Center = Centroid(pick), StateTick = w.Tick };
        foreach (var u in pick) ng.Units.Add(u.Id);
        s.Groups.Add(ng);
        s.Orders++;
    }
}
