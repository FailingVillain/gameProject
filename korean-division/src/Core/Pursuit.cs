using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 추격 · 후퇴 규칙 (사용자 결정 2026-10-19, 브랜치 pursuit — docs/ENGINE_NOTES.md '추격 · 기병 측면').
///   ① 후퇴 = <see cref="RetreatCommand"/> (속보 가능한 병사는 속보, 그 밖은 보통 이동) → <see cref="Unit.Retreating"/> (도착 · 새 명령까지).
///   ② 보병은 후퇴 중인 적에게 착검 돌격 · 자동 돌격 · 행진 사격 3칸 돌격 · 장교 돌격을 걸지 않는다 (사격만, <see cref="PursuitDef.NoChargeFleeing"/>).
///      기병은 <see cref="PursuitDef.CavalryChargeFleeing"/> (기본 true = 전통적 기병 추격 허용).
/// 자료 tactics.json "pursuit" · "cavalry" (전술 AI 추격 한계 · 기병 측면 공격 · 소규모 기병 규칙도 여기).
/// </summary>
public sealed class PursuitDef
{
    /// <summary>보병 (기병 아닌 모든 근접 유닛) 은 후퇴 중인 적에게 돌격하지 않는다 (명령 돌격 = 플레이어 선택은 예외).</summary>
    public bool NoChargeFleeing { get; set; } = true;
    /// <summary>기병은 후퇴 중인 적에게 돌격해도 된다 (전통적 추격, 사용자 2026-10-19). false = 보병과 같은 규칙.</summary>
    public bool CavalryChargeFleeing { get; set; } = true;
    /// <summary>전술 AI 사격 추격: 켜면 후퇴하는 적을 유효 사거리에서 따라가며 쏘고 한계 (거리 · 시간) 에서 멈춰 재정비.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>적 무리 (표적 둘레 fleeingRadius) 의 이 비율 이상이 후퇴 중이면 '후퇴하는 적' 으로 본다.</summary>
    public float FleeingShare { get; set; } = 0.5f;
    public float FleeingRadius { get; set; } = 12f;
    /// <summary>따라가는 거리 = 무기 사거리 × 이 값 (유효 사거리).</summary>
    public float RangeFrac { get; set; } = 0.75f;
    /// <summary>추격 시작 위치 (자기 선) 에서 적 쪽으로 이 칸 넘게 나가지 않는다 (소총 사거리 배수).</summary>
    public float MaxDist { get; set; } = 1.0f;
    /// <summary>추격 한 번의 최대 시간 (초).</summary>
    public float MaxSec { get; set; } = 40f;
    /// <summary>한계에서 멈춘 뒤 재정비 (제자리 사수 · 대열 정비) 시간 (초).</summary>
    public float RegroupSec { get; set; } = 20f;
    /// <summary>후퇴 사격 (사용자 결정 2026-10-19, pursuit2): 후퇴 중 (<see cref="World.IsFleeing"/>) 병사가 걸으면서 사거리 안 적을 쏜다 — 멈추지 · 돌아서지 · 돌격하지 않는다.
    /// false = 옛 동작 (후퇴 중 사격 없음).</summary>
    public bool RetreatFire { get; set; } = true;
    /// <summary>후퇴 사격 명중 배수 (행진 사격 combat.marchAccMul 0.4 보다 나쁨 — 뒤돌아 쏘며 걷는다).</summary>
    public float RetreatFireAccMul { get; set; } = 0.3f;
    /// <summary>후퇴 사격 간격 배수 (재장전 · 걸음).</summary>
    public float RetreatFireIntervalMul { get; set; } = 2.0f;
    /// <summary>후퇴 사격 최대 거리 = min(무기 사거리 × 이 값, 근거리 시야 × combat.marchSightFrac).</summary>
    public float RetreatFireRangeFrac { get; set; } = 0.75f;
}

/// <summary>
/// 후퇴 뒤 재정비 · 재전진 판단 (사용자 결정 2026-10-19, pursuit2 — 전술 AI 소총 중대): 스스로 후퇴 (Fallback 15초) 가 끝나면 과제가 공격이어도 바로 같은 적에게 다시 걸어가지 않는다.
/// minSec 동안 제자리 사수 (재정비 · 대열 정비) → 그 뒤 checkSec 마다: 병력 ≥ rejoinStrength · 평균 제압 ≤ rejoinSupp 이고 (국지 병력비 ≥ rejoinOdds 또는 지원) 이면 재전진,
/// 증원 (병력 +reinforceFrac × 처음 병력) 이면 제압만 보고 재전진, 적이 접촉 거리에 없으면 재전진 (과제 위치로). 아니면 그 자리 사수 (사거리 안 적에게 지원 사격).
/// 지원 = 같은 세력 다른 공격 소총 중대 (supportMin 개 이상) 가 그 적 둘레 supportRadius × 소총 사거리 안에서 교전 중.
/// </summary>
public sealed class RegroupDef
{
    /// <summary>false = 옛 동작 (후퇴 15초 뒤 곧바로 공격 과제 재개).</summary>
    public bool Enabled { get; set; } = true;
    public float MinSec { get; set; } = 20f;
    public float CheckSec { get; set; } = 5f;
    public float RejoinStrength { get; set; } = 0.5f;
    public float RejoinSupp { get; set; } = 0.2f;
    public float RejoinOdds { get; set; } = 1.0f;
    public float ReinforceFrac { get; set; } = 0.15f;
    public float SupportRadius { get; set; } = 1.2f;
    public int SupportMin { get; set; } = 1;
}

/// <summary>기병 운용 (전술 AI CavalryThink) — 측면 · 후방 공격 우선, 소규모 기병은 정찰 · 차장, 기관총 앞 교착 방지.</summary>
public sealed class CavalryRulesDef
{
    /// <summary>돌격하려면 기수가 이 수 이상 (아래면 정찰 · 차장: 카빈 사격 · 대기 · 물러남).</summary>
    public int MinRiders { get; set; } = 8;
    /// <summary>국지 병력비: 기수 ≥ 표적 둘레 oddsRadius 의 보이는 적 × minOdds (측면 · 후방이면 × flankOddsMul).</summary>
    public float MinOdds { get; set; } = 1.0f;
    public float FlankOddsMul { get; set; } = 0.7f;
    /// <summary>후퇴하지 않는 보병 묶음을 돌격 표적으로 삼으려면 평균 제압 ≥ 이 값 (형성된 측면 · 후방, 걷는 대형, 흐트러진 대형 모두 — pursuit2, 0 = N22 동작).</summary>
    public float FlankMinSupp { get; set; } = 0.15f;
    /// <summary>흐트러짐 (방향 정렬도 &lt; formedAlign) 판정에 필요한 묶음 안 보이는 적 수 — 미만이면 형성된 대형으로 본다 (pursuit2, 0 = N22 동작).</summary>
    public int FormedMinN { get; set; } = 4;
    public float OddsRadius { get; set; } = 6f;
    /// <summary>접근 각 (표적 대형 정면 기준) 이 이 각 (도) 안이면 정면, 180 − 이 각 밖이면 후방, 그 사이 = 측면.</summary>
    public float FrontDeg { get; set; } = 60f;
    /// <summary>점수: 측면 · 후방 가산, 정면 감점.</summary>
    public float FlankScore { get; set; } = 0.6f;
    public float RearScore { get; set; } = 0.8f;
    public float FrontPenalty { get; set; } = 1.0f;
    /// <summary>정면 돌격도 허용: 표적 무리 평균 제압 ≥ frontSuppOk, 또는 기수 ≥ 적 × frontWeakOdds (아주 약함), 또는 대형이 흐트러짐 (방향 정렬도 &lt; formedAlign).</summary>
    public float FrontSuppOk { get; set; } = 0.5f;
    public float FrontWeakOdds { get; set; } = 3f;
    public float WeakRadius { get; set; } = 15f;
    public float FormedAlign { get; set; } = 0.6f;
    /// <summary>측면 경유점: 표적 무리 중심에서 옆 · 뒤로 이 칸.</summary>
    public float FlankOffset { get; set; } = 14f;
    /// <summary>경유점을 보이는 적의 가장 긴 직사 사거리 + 2 칸 밖으로 (flankOffset 보다 멀면).</summary>
    public bool WayOutsideRange { get; set; } = true;
    /// <summary>경유점 · 접근 경로를 기관총 사계 (정면 ± frontDeg) 와 이 반경 밖으로.</summary>
    public float MgAvoidRadius { get; set; } = 26f;
    /// <summary>표적 없이 적 가까이 (적 사거리 + 6 안) 머문 시간이 이 초를 넘으면 물러나 차장 (교착 방지).</summary>
    public float StallSec { get; set; } = 20f;
    /// <summary>물러날 때 적에게서 이 칸 (적 최대 사거리 + 이 값) 밖으로.</summary>
    public float ScreenGap { get; set; } = 10f;
    /// <summary>경유점에 이만큼 다가가면 (또는 이 초가 지나면) 다음 단계.</summary>
    public float WayReach { get; set; } = 4f;
    public float WaySec { get; set; } = 25f;
    /// <summary>cavcmd: AI 기병 돌격 때 장교는 돌격하지 않고 표적 앞 이 칸 (돌격 방향 반대쪽) 으로 따라간다 (지휘 유지). 0 = 장교도 함께 돌격.</summary>
    public float OfficerBack { get; set; } = 10f;
    /// <summary>F4: 기병 돌격 대폭 제한 (AI 자동 · 전술 AI). 표적이 측면 · 후방 · 후퇴 · 제압 · 흐트러짐 · 중화기 승무원 (무방비) · 고립 소수일 때만. 플레이어 명령 돌격은 그대로. false = S4 동작.</summary>
    public bool StrictCharge { get; set; } = true;
    /// <summary>F4: 표적 제압 비율 (0~1) 이 이 이상이면 정면이어도 돌격 허용.</summary>
    public float AutoMinTargetSupp { get; set; } = 0.5f;
    /// <summary>F4: 표적 둘레 isolatedRadius 의 같은 편 (자신 제외) 이 isolatedMax 이하면 고립 (또는 AI 묶음 수 ≤ isolatedMax).</summary>
    public int IsolatedMax { get; set; } = 3;
    public float IsolatedRadius { get; set; } = 6f;
    /// <summary>F4: 중화기 승무원 표적 둘레 이 칸에 비중화기 보병이 없으면 무방비.</summary>
    public float CrewGuardRadius { get; set; } = 6f;
    /// <summary>F4: 자동 돌격 접근 각 — 표적 정면 기준 이 도 이내면 정면 (측면 · 후방만 허용).</summary>
    public float AutoFrontDeg { get; set; } = 60f;
    /// <summary>F4: 기병 자신의 제압이 이 이상이면 자동 돌격 안 함 (돌격이 꺾인 뒤 재돌격 방지 — 제압이 빠질 때까지 냉각).</summary>
    public int AutoMaxOwnSupp { get; set; } = 250;
    /// <summary>F4: AI 기병 돌격 한 번이 끝난 뒤 다음 돌격까지 (초).</summary>
    public float RechargeCooldownSec { get; set; } = 30f;
    /// <summary>true = pursuit 전 옛 기병 AI (비교 측정용 · 되돌리기용).</summary>
    public bool Legacy { get; set; }
}

public sealed partial class Unit
{
    /// <summary>후퇴 중 (RetreatCommand 로 이동, 도착 · 새 명령까지): 보병 돌격 표적이 되지 않는다 (추격 = 사격만). 저장 · 체크섬 (FormatVersion 29).</summary>
    [SnapExt] internal bool Retreating;
    /// <summary>후퇴 사격 중인 한 발 (HitChance 가 읽는 순간 값, 저장 안 함 — pursuit2).</summary>
    internal bool RetreatShot;
}

public sealed partial class World
{
    public PursuitDef PursuitRules => Defs.Tactics.Pursuit;
    public CavalryRulesDef CavalryRules => Defs.Tactics.Cavalry;
    public RegroupDef RegroupRules => Defs.Tactics.Regroup;

    /// <summary>후퇴 중 (상태로 정함: 후퇴 명령으로 이동 중 — 속도가 아니라 명령 · 그룹).</summary>
    public static bool IsFleeing(Entity? e) => e is Unit u && u.Alive && u.Retreating && u.Order == UnitOrder.Move;

    /// <summary>charger 가 t 에게 (자동 · AI) 돌격하면 안 되는가: 후퇴 중인 적 + 보병 (또는 기병 추격 금지 설정).</summary>
    internal bool ChargeBlockedFleeing(Unit charger, Entity? t)
    {
        bool blocked = FleeingBlocks(charger, t);
        if (blocked) Stats.FleeingChargeBlocked++;
        return blocked;
    }

    /// <summary>같은 판정 (계수 없음).</summary>
    internal bool FleeingBlocks(Unit charger, Entity? t)
    {
        if (!IsFleeing(t)) return false;
        var p = PursuitRules;
        return charger.Def.Mounted ? !p.CavalryChargeFleeing : p.NoChargeFleeing;
    }

    readonly List<Unit> cavGateBuf = new();

    /// <summary>F4: 기병이 이 표적에 돌격할 만한가 — 후퇴 · 제압 · 고정 · 무방비 중화기 승무원 · 고립 소수 · 측면 / 후방 (표적 개별 방향 기준).</summary>
    internal bool CavChargeJustified(Unit u, Entity? t) => CavChargeWhy(u, t) != 0;

    /// <summary>0 = 무의미 · 1 후퇴 · 2 제압 · 3 무방비 승무원 · 4 고립 · 5 측면/후방 · 6 비유닛.</summary>
    internal int CavChargeWhy(Unit u, Entity? t)
    {
        if (t is not Unit tu) return 6;
        var C = CavalryRules;
        if (IsFleeing(tu)) return 1;
        if (tu.Pinned || tu.SuppressionRatio >= C.AutoMinTargetSupp) return 2;
        UnitsInRadius(tu.Pos, MathF.Max(C.IsolatedRadius, C.CrewGuardRadius), cavGateBuf);
        int friends = 0, guards = 0;
        foreach (var o in cavGateBuf)
        {
            if (ReferenceEquals(o, tu) || !o.Alive || o.Owner != tu.Owner) continue;
            float d2 = Vector2.DistanceSquared(o.Pos, tu.Pos);
            if (d2 <= C.IsolatedRadius * C.IsolatedRadius) friends++;
            if (!o.Def.Heavy && !o.Def.Mounted && d2 <= C.CrewGuardRadius * C.CrewGuardRadius) guards++;
        }
        if (tu.Def.Heavy && guards == 0) return 3;
        if (friends <= C.IsolatedMax) return 4;
        Vector2 toU = u.Pos - tu.Pos;
        if (toU.LengthSquared() < 1e-6f) return 0;
        Vector2 face = new(MathF.Cos(tu.Facing), MathF.Sin(tu.Facing));
        return Vector2.Dot(face, Vector2.Normalize(toU)) < DetMath.Cos(C.AutoFrontDeg * MathF.PI / 180f) ? 5 : 0;   // 정면 원뿔 밖 = 측면 · 후방
    }

    /// <summary>F4: 자동 돌격 (엔진 근접 · 발밑 · 행진) 막기 — 기병만, 명령 돌격은 부르지 않는다.</summary>
    internal bool CavAutoChargeBlocked(Unit u, Entity? t)
    {
        var C = CavalryRules;
        if (!C.StrictCharge || !u.Def.Mounted || t is not Unit) return false;
        bool blocked = u.Suppression >= C.AutoMaxOwnSupp || !CavChargeJustified(u, t);
        if (blocked) Stats.CavAutoBlocked++;
        return blocked;
    }

    /// <summary>[측정] 돌격 개시 표적이 후퇴 중이면 센다 (보병 = 0 이어야 함).</summary>
    internal void CountChargeAt(Unit charger, Entity? t)
    {
        if (charger.Def.Mounted && t is Unit)
        {
            Stats.CavChargeStarts++;
            int why = CavChargeWhy(charger, t); if (why == 0) Stats.CavChargePointless++; else if (why < 6) Stats.CavWhy[why - 1]++;   // F4: 형성되고 제압 안 된 정면 (측정 — 플레이어 명령 포함)
        }
        if (!IsFleeing(t)) return;
        if (charger.Def.Mounted) Stats.CavChargeFleeing++; else Stats.InfChargeFleeing++;
    }

    /// <summary>
    /// 후퇴 사격 (pursuit2, 사용자 결정 2026-10-19): 후퇴 중 걷는 병사 (행진 사격 가능 유닛 — 대포 · 곡사 · 기병 제외) 가 사거리 안의 보이는 적을 쏜다.
    /// 방향 제한 없음 (돌아보며 쏨), 이동 · 명령 · 대형은 그대로 (멈춤 · 돌아섬 · 돌격 없음 — 표적은 MarchTarget 에만, u.Target 은 건드리지 않음).
    /// 명중 × retreatFireAccMul, 간격 × retreatFireIntervalMul. 이번 틱 움직였을 때만 (WorldCombat 이동 끝). 새 상태 없음 (MarchTarget 재사용).
    /// </summary>
    void RetreatFire(Unit u)
    {
        var p = PursuitRules;
        var c = Defs.CombatRules;
        var w = u.Weapon;
        if (!p.RetreatFire || !u.Def.MarchFire || u.Def.Mounted || w == null || u.Charging || u.CanDeploy || IsIndirect(w)
            || u.Suppression >= c.MarchMaxSuppress || u.Def.IsOfficer || !FireMayHappen(u))
        { u.MarchTarget = 0; return; }
        float rad = RetreatFireRadius(u, w, c, p);
        Entity? t = u.MarchTarget != 0 ? Find(u.MarchTarget) : null;
        if (t != null && !RetreatTargetOk(u, t, w, rad, out _)) t = null;
        if (t == null)
        {
            u.MarchTarget = 0;
            if ((Tick + u.Id) % 10 != 0) return;
            t = FindTarget(u.Owner, u.Pos, rad, w.MinRange, w.Los ? u : null, u.Id, TargetJitter(u), w);
            if (t == null || !RetreatTargetOk(u, t, w, rad, out _)) return;
            u.MarchTarget = t.Id;
        }
        if (u.Cooldown > 0 || !u.HasAmmo || !FireAllowed(u, t)) return;
        RetreatTargetOk(u, t, w, rad, out float dist);
        if (!ConsumeAmmo(u)) return;
        u.Cooldown = NextCooldown(u, w) * FireIntervalMul(u) * MathF.Max(1f, p.RetreatFireIntervalMul);
        BurstAfterShot(u, w);
        u.RetreatShot = true;
        Fire(u, u.Pos, t, w, dist);
        u.RetreatShot = false;
        Stats.RetreatShots++;
    }

    float RetreatFireRadius(Unit u, WeaponDef w, CombatRulesDef c, PursuitDef p)
    {
        int near = WeatherSight(u.Def.SightNear > 0 ? u.Def.SightNear : u.Def.Sight, WeatherSightMul);
        return MathF.Min(w.Range * p.RetreatFireRangeFrac, near * c.MarchSightFrac);
    }

    bool RetreatTargetOk(Unit u, Entity t, WeaponDef w, float rad, out float dist)
    {
        dist = 0;
        if (!t.Alive || t.Indestructible || !IsHostile(u.Owner, t.Owner) || !Fog.CanSee(u.Owner, t)) return false;
        if (!w.CanDamage(CategoryOf(t))) return false;   // 유닛 · 열차 객차 (건물 = 항상 가능)
        dist = MathF.Max(0, t.EdgeDistance(u.Pos) - u.Radius);
        if (dist > rad || dist < w.MinRange) return false;
        return HasFiringLine(u, t, w);
    }

    readonly List<Unit> dangerBuffer = new();

    /// <summary>
    /// 포병 아군 오사 (pursuit2) 의 포수 규율: 포탄 무기 (곡사 · 직사 범위포) 가 명시 공격 명령 (Order Attack) 없이 쏘려는 표적 둘레
    /// splash × 1.8 + 그 거리의 퍼짐 × combat.crewDangerScatterMul + combat.crewDangerMargin 칸 안에 아군 · 동맹 유닛 (쏜 포 제외) 이 있으면 true (쏘지 않고 표적을 놓는다).
    /// combat.crewDangerMargin &lt; 0 = 끔. 플레이어 공격 명령과 지점 포격은 그대로 쏜다 (아군도 맞음). 전술 AI 는 공격 명령 전에 AlliesNearImpact 로 거르고, 위험해지면 사격 중지.
    /// </summary>
    internal bool CrewDangerClose(Unit u, Entity t, WeaponDef w, float dist)
    {
        if (u.Order == UnitOrder.Attack || !AlliesNearImpact(u, t, w, dist)) return false;
        Stats.CrewHoldFire++;
        return true;
    }

    /// <summary>포수 규율 판정만 (명령 · 통계 무관): 포탄 무기 표적 t 둘레 splash × 1.8 + 퍼짐 + crewDangerMargin 칸 안에 쏜 포 말고 아군 · 동맹이 있나.
    /// 전술 AI 의 강제 사격 (공격 명령) 도 이것으로 거른다 — 엔진은 공격 명령이면 그대로 쏘므로 (플레이어 명령 존중), AI 가 명령 전에 판단 (pursuit2 · crossing 병합).</summary>
    public bool AlliesNearImpact(Unit u, Entity t, WeaponDef w, float dist, bool aiForced = false)
    {
        var c = Defs.CombatRules;
        if (w.Splash <= 0 || !c.ArtilleryFriendlyFire || c.CrewDangerMargin < 0) return false;
        // aiForced = 전술 AI 강제 사격 (준비사격 · 돌격 지원 · 도하 화력): 퍼짐 없이 splash × 1.8 + margin 만 — 아군 바로 앞 지원 사격 (danger close) 은 감수
        float r = w.Splash * 1.8f + (aiForced ? 0f : ScatterAt(w, dist) * c.CrewDangerScatterMul) + c.CrewDangerMargin;
        UnitsInRadius(t.Pos, r + Defs.MaxUnitRadius, dangerBuffer);
        bool near = false;
        foreach (var o in dangerBuffer)
        {
            if (!o.Alive || o.Id == u.Id || !(o.Owner == u.Owner || Allied(o.Owner, u.Owner))) continue;
            if (Vector2.Distance(o.Pos, t.Pos) - o.Radius <= r) { near = true; break; }
        }
        dangerBuffer.Clear();
        return near;
    }

    /// <summary>후퇴 이동: 속보 가능한 병사 = 속보, 그 밖 (기병 · 차량 · 중화기 일부) = 보통 이동. 전원 Retreating.</summary>
    public void Retreat(int faction, IEnumerable<Unit> units, Vector2 target, FormSpec? form = null, bool useRally = false) =>
        IssueUnits(faction, units, ids => new RetreatCommand(faction, ids, target, NormForm(form), useRally));   // useRally: 집결 지점이 있는 중대는 그 지점으로 (Rally.cs, N79a)
}
