using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 교전 개편 2차 (ROADMAP §2-4b · §2-4c · §2-4d · §2-4e): 제압 · 계급 · 장교 지휘 · 착검 돌격 · 근접전 · 반응 지연 · 멍칫 · 기관총 흩뿌림 · 표적 분산.
/// 모든 값은 틱 · 입력의 함수 (해시 Roll). 저장은 CMB2 구역 뒤쪽 (WriteCombat3), 체크섬은 HashCombat3.
/// </summary>
public sealed partial class Unit
{
    /// <summary>제압 지수 0..combat.suppressMax. 자신을 노린 공격(명중 무관)마다 무기 suppress × Def.SuppressResist 증가, 시간이 지나면 감소. 효과 = 사격 간격 · 이동 속도 (명중률 영향 없음).</summary>
    [SnapExt] public short Suppression { get; internal set; }
    /// <summary>누적 사살 수 (계급 기준). 장교는 세기만 하고 계급 없음.</summary>
    [SnapExt] public short Kills { get; internal set; }
    /// <summary>배치 · 편성 때 정해진 사병 계급의 기준 사살 수 — 전투 중 사살로는 계급이 오르지 않는다 (진급은 캠페인 단계, 사용자 2026-10-19). 사살 수 Kills 는 결과 파일 · 캠페인 진급 근거로만 쌓인다.</summary>
    [SnapExt] public short StartKills { get; internal set; }
    /// <summary>배치 때 계급을 정한다 (Kills · StartKills 동시).</summary>
    internal void GrantKills(int k) { Kills = StartKills = (short)Math.Clamp(k, 0, short.MaxValue); }
    /// <summary>돌격 상태: 0 없음 · 1 자동 (적이 chargeAutoRange 이내) · 2 명령 · 3 역돌격 (돌격해 오는 적에게 자동 돌격 — 보너스 없음). 돌격 중에는 사격하지 않고 근접 무기로 싸우며 속도 +30% (역돌격 제외).</summary>
    [SnapExt] internal byte ChargeState;
    /// <summary>피로 0..staminaMax (0 = 충분히 쉼, 새 유닛 기본값). 스태미나 = staminaMax − 피로. 돌격 · 속보 중 쌓이고 쉬면 줄어든다 (보병 · 장교만, 차량은 쓰지 않음).</summary>
    [SnapExt] internal float Fatigue;
    /// <summary>속보 (Order = Move 인 동안): 걷기보다 빠르게 (+quickStepSpeedMul), 스태미나 소모. ROADMAP §4.</summary>
    [SnapExt] internal bool QuickStep;
    /// <summary>이 틱 전에는 다시 멍칫하지 않음 (평균 속도 손실 상한: 멈춘 틱 × 1/maxLoss).</summary>
    [SnapExt] internal long HesitateBlockUntil;

    /// <summary>근접 무기 (착검 · 곡도). 없으면 돌격 불가.</summary>
    public WeaponDef? Melee => Defs.Current.Weapon(Def.Melee);
    public bool CanCharge => Def.Melee != null;
    public bool Charging => ChargeState != 0;
    /// <summary>역돌격 (돌격해 오는 적에게 맞받은 자동 돌격): 속도 보너스 · 기병 충격 없음 (2026-10-19).</summary>
    public bool CounterCharging => ChargeState == 3;
    public bool IsOfficer => Def.IsOfficer;
    /// <summary>스태미나를 쓰는 유닛 (보병 · 장교).</summary>
    public bool HasStamina => Def.IsInfantry;
    /// <summary>현재 스태미나 0..staminaMax (차량은 항상 최대).</summary>
    public float Stamina => HasStamina ? MathF.Max(0, StaminaCap - Fatigue) : Defs.Current.CombatRules.StaminaMax;
    /// <summary>스태미나 한도 (기병 ×3).</summary>
    public float StaminaCap => Defs.Current.CombatRules.StaminaMax * Def.StaminaMul;
    /// <summary>[표시] 스태미나 비율 0..1.</summary>
    public float StaminaRatio => Math.Clamp(Stamina / StaminaCap, 0, 1);
    /// <summary>사병 계급 0..8 (삼등병 … 원사, 이름은 RankNames). 장교는 -1 (사병 계급 없음 — 장교 계급은 OfficerRank).</summary>
    public int Rank => IsOfficer ? -1 : Defs.Current.CombatRules.RankOf(StartKills);
    /// <summary>장교 계급 = 정식 계급 OfficerBase + 임시 진급 TempRank (0 = 미정 '장교', 1 소위 … 6 대령, CommandRanks.cs). 장교가 아니면 -1.</summary>
    public int OfficerRank => IsOfficer ? Math.Min(OfficerBase + TempRank, Math.Max(1, Defs.Current.Officers.MaxRank)) : -1;
    /// <summary>공식 부사관 (사병 계급 ≥ combat.ncoRank = 하사 이상).</summary>
    public bool IsNco => !IsOfficer && Rank >= Defs.Current.CombatRules.NcoRank;
    /// <summary>[표시] 제압 비율 0..1.</summary>
    public float SuppressionRatio => Math.Clamp(Suppression / (float)Defs.Current.CombatRules.SuppressMax, 0, 1);
}

public sealed partial class World
{
    // ---------------------------------------------------------------- 표적 분산 (§2-4b)

    /// <summary>(a, b, 시드) 쌍마다 고정인 [0,1) 잡음 — 틱과 무관해 같은 사격자는 같은 표적을 계속 선호한다 (표적이 바뀌어 흔들리지 않음).</summary>
    static float PairNoise(int a, int b, int seed)
    {
        ulong h = (ulong)(uint)a * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)b * 0x165667B19E3779F9UL ^ (ulong)(uint)seed * 0x9E3779B97F4A7C15UL;
        h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33;
        return (h >> 40) / (float)(1UL << 24);
    }

    /// <summary>자동 표적 점수에 더할 흔들림 폭(칸): 직사 보병만 (곡사 · 차량 · 건물 0).</summary>
    float TargetJitter(Unit u) => u.Def.IsInfantry && u.Weapon != null && !IsIndirect(u.Weapon) ? Defs.CombatRules.TargetSpreadJitter : 0f;

    // ---------------------------------------------------------------- 제압 (§2-4b 6차)

    /// <summary>표적을 노린 공격 1회: 제압 += 무기 suppress × 유닛 저항 (장교 0 = 면역). 상한 suppressMax.</summary>
    void Suppress(Entity target, WeaponDef w, Vector2? from = null)
    {
        if (target is Unit ft) ft.LastFiredAtTick = Tick;   // 탄약 회복 대기 (빗나감 포함, Supply.cs)
        if (w.Suppress <= 0 || target is not Unit t || !t.Alive) return;
        AddSuppression(t, w.Suppress, false, w.Splash > 0 || IsIndirect(w) ? SupSrc.Blast : w.Spray > 0 ? SupSrc.Spray : SupSrc.Direct, from);   // × 저항 × 엄폐 (참호), 상한 (Suppression.cs)
    }

    /// <summary>기병 돌격 충격 지점 (at) 반경 combat.chargeSuppressRange 칸 안 적 보병에게 제압 combat.chargeSuppress (저항 · 엄폐 적용).</summary>
    void ChargeImpactSuppress(Unit rider, Vector2 at)
    {
        var rc = Defs.CombatRules;
        foreach (var o in Units)
        {
            if (!o.Alive || !o.Def.IsInfantry || !AreEnemies(rider.Owner, o.Owner) || Vector2.Distance(o.Pos, at) > rc.ChargeSuppressRange) continue;
            float gain = rc.ChargeSuppress * o.Def.SuppressResist * CoverSuppressMul(o);
            if (gain <= 0) continue;
            int v = o.Suppression + (int)MathF.Round(gain), old = o.Suppression;
            o.Suppression = (short)(v > rc.SuppressMax ? rc.SuppressMax : v);
            Stats.AddSupp(o.Owner, SupSrc.Charge, o.Suppression - old);   // [측정]
        }
    }

    /// <summary>제압 감소 (suppressDecayInterval 틱마다, Id 분산): 초당 (suppressDecay + suppressDecayProp × 제압) × (1 + 계급 × rankDecayBonus) × (장교 근처 ×officerDecayMul, 아니면 부사관 분대장 ×ncoDecayMul) × (최근 suppressCalmSec 초 안 노려졌으면 ×suppressFireDecayMul, N39).</summary>
    void DecaySuppression(Unit u)
    {
        if (u.Suppression <= 0) return;
        var c = Defs.CombatRules;
        int iv = Math.Max(1, c.SuppressDecayInterval);
        if ((Tick + u.Id) % iv != 0) return;
        float mul = 1f + Math.Max(0, u.Rank) * c.SuppressRankDecayBonus;
        if (NearOfficer(u)) mul *= c.OfficerDecayMul;
        else if (NcoLeads(u)) mul *= c.NcoDecayMul;   // 부사관 분대장 (장교 효과와 중첩 안 함, Squads.cs)
        if (c.CoverDecayMul != 1f && CoverSuppressMul(u) < 1f) mul *= c.CoverDecayMul;   // 엄폐 안에서 빨리 추스른다 (supp 2026-10-20)
        if (c.SuppressCalmSec > 0 && Tick - u.LastFiredAtTick <= (long)MathF.Round(c.SuppressCalmSec / Config.SimDt)) mul *= c.SuppressFireDecayMul;   // 사격받는 중 · 직후 천천히 (N39)
        mul *= u.Def.SuppressRecoverMul;   // assaultinf: 돌격보병 회복 ×1.5
        int dec = (int)MathF.Round((c.SuppressDecay + c.SuppressDecayProp * u.Suppression) * iv * Config.SimDt * mul);   // 비례 항 (N25, Suppression.cs)
        if (dec < 1) dec = 1;
        u.Suppression = (short)Math.Max(0, u.Suppression - dec);
    }

    /// <summary>제압에 따른 사격 간격 배수 1 ~ (1 + suppressFireSlow), 장교 근처 ×officerFireMul.</summary>
    public float FireIntervalMul(Unit u)
    {
        var c = Defs.CombatRules;
        float m = 1f + c.SuppressFireSlow * (u.Suppression / (float)c.SuppressMax);
        if (NearOfficer(u)) m *= c.OfficerFireMul;
        return m;
    }

    /// <summary>걸음걸이 배수: 돌격 (1 + 0.3 × f) · 속보 (1 + 0.15 × f) · 걷기 1. f = 스태미나 / staminaFullSpeed (0..1). 차량은 스태미나 없이 돌격 배수 그대로.</summary>
    internal float GaitMul(Unit u)
    {
        var c = Defs.CombatRules;
        float csm = u.Def.ChargeSpeedMul > 0 ? u.Def.ChargeSpeedMul : c.ChargeSpeedMul;
        if (u.CounterCharging) return 1f;   // 역돌격: 돌격 보너스 없음
        if (!u.HasStamina) return u.Charging ? csm : 1f;
        if (!u.Charging && !u.QuickStep) return 1f;
        float f = Math.Clamp(u.Stamina / MathF.Max(1f, c.StaminaFullSpeed), 0f, 1f);
        return 1f + ((u.Charging ? csm : c.QuickStepSpeedMul) - 1f) * f;
    }

    internal float UnitSpeedForTest(Unit u) => UnitSpeed(u);

    /// <summary>진형 이동 속도 상한: 진형은 가장 느린 구성원 걷기 속도에 맞추되, 돌격 · 속보 중이면 그 걸음걸이 배수만큼 올린다.</summary>
    internal float PlanCap(Unit u, FormPlan plan) => plan.Speed * GaitMul(u);

    /// <summary>스태미나 갱신 (매 틱, 이동 전). 돌격 중 이동하면 chargeDrain/s, 속보 중 이동하면 quickDrain/s 소모, 돌격 중 제자리(근접전)는 변화 없음, 그 밖에는 이동 중 regenMove/s · 정지 시 regenIdle/s 회복.</summary>
    void UpdateStamina(Unit u, bool moved, float dt)
    {
        if (!u.HasStamina) return;
        var c = Defs.CombatRules;
        float f = u.Fatigue;
        if (u.Charging) { if (moved) f += c.StaminaChargeDrain * dt; }
        else if (u.QuickStep && moved) f += c.StaminaQuickDrain * dt;
        else f -= (moved ? c.StaminaRegenMove : c.StaminaRegenIdle) * dt;
        u.Fatigue = Math.Clamp(f, 0f, u.StaminaCap);
    }

    /// <summary>이동 속도 (칸/초): 기본 × 걸음걸이(돌격 · 속보, 스태미나 비례) × 지침(스태미나 &lt; staminaLow 면 ×staminaLowMoveMul) × (1 − suppressMoveSlow × 제압 비율) × 칸 지형.</summary>
    internal float UnitSpeed(Unit u)
    {
        var c = Defs.CombatRules;
        float s = u.Def.Speed * GaitMul(u);
        if (u.HasStamina && u.Stamina < c.StaminaLow) s *= c.StaminaLowMoveMul;
        if (u.Suppression > 0) s *= 1f - c.SuppressMoveSlow * (u.Suppression / (float)c.SuppressMax);
        if (c.CrawlSpeedMul > 0 && !CrossExempt(u) && ((u.Prone && u.Pinned && !u.Broken && u.Order is UnitOrder.Move) || ProneCrawlFollow(u))) s *= c.CrawlSpeedMul;   // pronemove: 고정 병사의 개별 이동 (후퇴 포함) · 상한 뒤 대열을 따라 기어감 (돌격 · 강행 상한으로 일어선 고정 병사는 걷는다)
        s *= DismountedSpeed(u);   // 내린 기병 (Dismount.cs)
        if (MarchSlowed(u)) s *= u.Def.MarchMoveMul > 0 ? u.Def.MarchMoveMul : c.MarchMoveMul;   // 행진 사격 중 (보병 = 중대 · 분대 지휘로 전원 같은 순간, MarchCommand.cs) — 진형 깃발도 가장 느린 구성원에 맞춰 같이 느려진다
        return s * Map.MoveMul(u.Pos) * WeatherMoveMul;   // 지형 · 날씨 (숲 · 잔해 · 철조망 · 경사, GameMapTerrain.cs)
    }

    // ---------------------------------------------------------------- 장교 지휘 (§2-4c 12차)

    /// <summary>살아 있는 장교 (commandRadius > 0) — RebuildHash 가 Units 순서로 다시 모은다 (작업 버퍼, 저장 안 함).</summary>
    readonly List<Unit> officers = new();
    readonly List<Unit> bearers = new();   // [N68] 살아 있는 기수 (RebuildHash, Bearer.cs)

    /// <summary>같은 세력 장교의 지휘 범위 안인가 (장교 자신은 제외, 중첩 없음 — 참/거짓만).</summary>
    public bool NearOfficer(Unit u)
    {
        if (officers.Count == 0) return false;
        for (int i = 0; i < officers.Count; i++)
        {
            var o = officers[i];
            if (ReferenceEquals(o, u) || !o.Alive || o.Owner != u.Owner) continue;
            float r = o.Def.CommandRadius;
            if (Vector2.DistanceSquared(o.Pos, u.Pos) <= r * r) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- 계급 (§2-4c 14차)

    /// <summary>피해로 표적이 죽었으면 사격자의 사살 수 +1 (Damage 가 부른다).</summary>
    void CountKill(Entity target, Entity? source, string? weapon = null)
    {
        if (target.Hp <= 0 && target is Unit kv && source is Unit ks && !(ks.Owner == kv.Owner || Allied(ks.Owner, kv.Owner)))   // [측정 전용] 사살 원인: 10초 구간 / 공격자 종류 / 무기 / 피해자 종류
        { string kk = $"{(int)(Time / 10) * 10:000}s {ks.Def.Key}/{weapon ?? ks.Def.Weapon}->{kv.Def.Key}"; Stats.KillSources[kk] = Stats.KillSources.GetValueOrDefault(kk) + 1; }
        bool friendly = source != null && (source.Owner == target.Owner || Allied(source.Owner, target.Owner));   // 아군 오사 (포탄 파편, pursuit2) 는 사살 · 계급에 넣지 않음
        if (target.Hp <= 0 && target is Unit tv && source is Unit sv && !friendly)
        {
            bool melee = sv.Charging;
            if (melee) { Stats.KillsMelee++; if (sv.Def.Mounted) Stats.CavMeleeKills++; } else Stats.KillsFire++;
            if (tv.Retreating) { if (melee) Stats.KillsFleeingMelee++; else Stats.KillsFleeingFire++; }
            if (sv.RetreatShot) Stats.KillsRetreatFire++;   // 후퇴 사격으로 잡은 적 (Pursuit.cs)
        }
        if (friendly) return;
        if (source is Unit su && su.Alive && target.Hp <= 0 && su.Kills < Math.Min((int)short.MaxValue, Defs.CombatRules.KillsCap)) su.Kills++;
        if (source is Unit ku && ku.Alive && target.Hp <= 0 && target is Unit && IsHostile(ku.Owner, target.Owner)) CreditKillXp(ku);   // 장교 경험치 (CommandRanks.cs)
    }

    // ---------------------------------------------------------------- 착검 돌격 · 근접전 (§2-4b)

    /// <summary>적이 chargeAutoRange 안이면 자동 돌격 (근접 무기가 있는 유닛만). 이미 돌격 중이거나 제압이 chargeAutoMaxSuppress 이상이면 그대로.</summary>
    void AutoCharge(Unit u, Entity t, float dist)
    {
        if (u.ChargeState != 0 || !u.CanCharge || t is not Unit || !CanMelee(u, t) || !AutoChargeAllowed(u)) return;   // 건물에는 돌격하지 않음 (명령 돌격은 가능)
        if (!OfficerChargeOk(u)) return;   // N35: 장교는 중대 돌격 중일 때만 (OfficerConduct.cs)
        if (dist <= Defs.CombatRules.ChargeAutoRange && !ChargeBlockedFleeing(u, t) && (u.Order == UnitOrder.Attack || !CavAutoChargeBlocked(u, t))) { u.ChargeState = AutoChargeState(t); CountChargeAt(u, t); BeginFix(u, false); ProbeOfficerCharge(u, "auto", t); }   // 후퇴하는 적에게는 사격만 (Pursuit.cs)
    }

    /// <summary>자동 돌격 가능 여부: 제압 &lt; combat.chargeAutoMaxSuppress (2026-10-19).</summary>
    bool AutoChargeAllowed(Unit u) => u.Suppression < Defs.CombatRules.ChargeAutoMaxSuppress && !u.Pinned && !u.Broken;   // 고정 (엎드림) 병사는 돌격 안 함 (Suppression.cs)

    /// <summary>자동 돌격 상태값: 표적이 지금 돌격 중이면 역돌격 (3, 보너스 없음), 아니면 자동 돌격 (1).</summary>
    static byte AutoChargeState(Entity t) => t is Unit tu && tu.Charging ? (byte)3 : (byte)1;

    /// <summary>
    /// 돌격 교전: 근접 거리(meleeRange)면 멈춰 근접 공격 (간격 난수, 명중 = 무기 accuracy, 피해 − 장갑), 아니면 돌격 속도로 표적에게 달려간다. 사격 · 조준 · 멍칫 없음.
    /// 계속할 수 없으면 false (호출자가 표적을 놓는다).
    /// </summary>
    bool EngageMelee(Unit u, Entity t, float dt, bool chase, float leash)
    {
        var m = u.Melee;
        if (m == null || !CanMelee(u, t)) return false;   // 전차 · 장갑차에는 근접 공격 불가
        float dist = t.EdgeDistance(u.Pos) - u.Radius;
        MarkAlert(u.Owner, u.Pos);
        if (dist <= Defs.CombatRules.MeleeRange + m.Range)
        {
            if (u.Def.Mounted && t is Unit mt && mt.Def.IsInfantry && Defs.CombatRules.CavMeleeMaxTargets > 0 && mt.Id != u.MeleeLastId)
            {
                if (u.MeleeVictims >= Defs.CombatRules.CavMeleeMaxTargets) return false;   // N61: 한 돌격에서 서로 다른 보병 N명까지만 근접 (넘으면 이탈)
                u.MeleeVictims++; u.MeleeLastId = mt.Id;
            }
            FaceToward(u, t.Pos, dt);
            u.HoldLeft = 0; u.EngagedTick = Tick;
            u.AimTarget = t.Id; u.AimLeft = 0;   // 사격 자세와 같이 밀리지 않음
            if (u.Cooldown <= 0)
            {
                u.Cooldown = NextCooldown(u, m);
                u.LastShotTick = Tick;
                bool hit = Roll(u.Id, t.Id) < m.Accuracy;
                MeleeStrikes.Add((u.Pos, t.Pos, u.Owner, Tick, hit, u.Id));
                if (hit)
                {
                    float dmg = m.Damage * (t is Unit tu ? m.BonusAgainst(tu.Def.Category) : 1f);
                    var rc = Defs.CombatRules;
                    if (u.Def.Mounted && !u.ImpactSpent && u.ChargeCdLeft <= 0 && !u.CounterCharging && t is Unit ti && ti.Def.IsInfantry && u.ChargeRun * Config.SimDt >= rc.ChargeImpactSeconds)
                    {
                        // 기병 돌격 충격 이벤트 (2026-10-19 개정): 피해 보너스 없음 (일반 근접 피해). 2초 이상 달린 뒤 첫 적중 = 돌격 한 번에 한 번,
                        // 기병마다 쿨다운 10~15초 (결정적), 직후 기병 마비 (고정 0.6초), 충격 지점 반경 안 적 보병 제압.
                        u.ImpactSpent = true;
                        float cd = rc.ChargeCooldownMin + (rc.ChargeCooldownMax - rc.ChargeCooldownMin) * Roll(u.Id, 0xCD10);
                        u.ChargeCdLeft = Math.Max(1, (int)MathF.Round(cd / Config.SimDt));
                        float sec = rc.ChargeStunMin + (rc.ChargeStunMax - rc.ChargeStunMin) * Roll(u.Id, 0x4C48);
                        u.ReactDelay = Math.Max(1, (int)MathF.Round(sec / Config.SimDt));
                        ChargeImpactSuppress(u, ti.Pos);
                    }
                    Damage(t, dmg, u, "melee");
                }
                else Retaliate(t, u);
            }
            return true;
        }
        u.AimLeft = 0; u.AimTarget = 0;
        if (!chase || Vector2.Distance(u.Pos, u.LeashOrigin) > leash) return false;
        if (!AdvancePack(u)) return true;
        if (u.Def.ChargeHopRange > 0 && ChargeHopGoal(u, t) is { } hop) return StepToward(u, hop, null, dt, chase: false);   // assaultinf: 엄폐 경유 약진 (ChargeHop.cs)
        return StepToward(u, t.Pos, null, dt, chase: true);
    }

    /// <summary>[렌더러 · 효과음] 근접 공격 (공격자 위치, 표적 위치, 세력, 틱, 명중, 공격자 Id). 10틱 뒤 지운다.</summary>
    public readonly List<(Vector2 From, Vector2 To, int Faction, long Tick, bool Hit, int AttackerId)> MeleeStrikes = new();

    // ---------------------------------------------------------------- 반응 지연 · 멍칫 (§2-4d 14차)

    /// <summary>명령 → 출발 지연 틱 0..reactDelayMaxTicks (유닛 · 명령 틱 해시). 표시 · 음성은 즉시 (LastOrderTick).</summary>
    int ReactDelayFor(Unit u) => (int)(Roll(u.Id, 0x7E57) * (Defs.CombatRules.ReactDelayMaxTicks + 1));

    /// <summary>
    /// 이동 중 멍칫 판정 (hesitateInterval 마다, Id 분산): 제압 ≥ 기준, 돌격 아님, 이동 명령 뒤 유예 지남, 상한 기간 아님 →
    /// 확률 = hesitateChance × (제압 − 기준)/(최대 − 기준) × (1 − 계급 × rankFactor) × (장교 근처 officerMul). 멈추면 ReactDelay 로 서고, 멈춘 틱 / maxLoss 동안 다시 멈추지 않는다.
    /// </summary>
    bool MaybeHesitate(Unit u)
    {
        if (MaybeWireHesitate(u)) return true;
        var c = Defs.CombatRules;
        if (u.Suppression < c.HesitateMinSuppress || u.Charging) return false;
        int iv = Math.Max(1, (int)MathF.Round(c.HesitateInterval / Config.SimDt));
        if ((Tick + u.Id) % iv != 0) return false;
        if (Tick - u.LastMoveTick > 1) return false;                                   // 지난 틱에 걷고 있던 유닛만
        if (Tick < u.HesitateBlockUntil) return false;
        if (u.Order == UnitOrder.Move && Tick - u.LastOrderTick < c.HesitateGraceSec / Config.SimDt) return false;   // 후퇴 · 이동 명령 직후
        float span = Math.Max(1, c.SuppressMax - c.HesitateMinSuppress);
        float p = c.HesitateChance * Math.Clamp((u.Suppression - c.HesitateMinSuppress) / span, 0f, 1f);
        p *= Math.Max(0f, 1f - Math.Max(0, u.Rank) * c.HesitateRankFactor);
        if (NearOfficer(u)) p *= c.HesitateOfficerMul;
        else if (NcoLeads(u)) p *= c.NcoHesitateMul;   // 부사관 분대장 (장교 효과와 중첩 안 함)
        if (p <= 0 || Roll(u.Id, 0x4E51) >= p) return false;
        float sec = c.HesitateMin + (c.HesitateMax - c.HesitateMin) * Roll(u.Id, 0x4E52);
        int ticks = Math.Max(1, (int)MathF.Round(sec / Config.SimDt));
        u.ReactDelay = ticks;
        u.HesitateBlockUntil = Tick + (c.HesitateMaxLoss > 0 ? (long)MathF.Ceiling(ticks / c.HesitateMaxLoss) : ticks);
        return true;
    }

    /// <summary>철조망 멈칫: 철조망 칸에서 지난 틱에 걷던 비장갑 유닛이 wireHesitateInterval 마다 (Id 분산) 확률 wireHesitateChance 로 wireHesitateMin~Max 초 선다.</summary>
    bool MaybeWireHesitate(Unit u)
    {
        var r = Defs.Terrain;
        if (r.WireHesitateChance <= 0 || u.Def.IsArmored) return false;
        int iv = Math.Max(1, (int)MathF.Round(r.WireHesitateInterval / Config.SimDt));
        if ((Tick + u.Id) % iv != 0 || Tick - u.LastMoveTick > 1 || !Map.IsWire(u.Pos)) return false;
        if (Roll(u.Id, 0x5752) >= r.WireHesitateChance) return false;
        float sec = r.WireHesitateMin + (r.WireHesitateMax - r.WireHesitateMin) * Roll(u.Id, 0x5753);
        u.ReactDelay = Math.Max(1, (int)MathF.Round(sec / Config.SimDt));
        return true;
    }

    // ---------------------------------------------------------------- 기관총 흩뿌림 (§2-4e 16차)

    readonly List<Unit> sprayBuffer = new();

    /// <summary>
    /// 흩뿌림 사격 (weapon.spray > 0): 명중 판정은 표적 기준. 빗나간 탄은 표적 둘레 spray 반경 안 결정적 지점에 떨어지고,
    /// 그 지점에서 spray 안의 다른 적 중 하나(가까울수록 가중)가 자기 명중률로 대신 맞을 수 있다. 제압은 착탄점 spray 안 적 전원 (아군 오사 없음).
    /// </summary>
    void FireSpray(Unit shooter, Vector2 from, Entity target, WeaponDef w, float dist)
    {
        bool hit = Roll(shooter.Id, target.Id) < ShotChance(shooter, target, w, dist);
        bool aimedHit = hit;
        Vector2 impact = target.Pos;
        if (!hit)
        {
            float ang = Roll(target.Id, shooter.Id) * DetMath.Tau;
            float r = w.Spray * MathF.Sqrt(Roll(target.Id, -3 - shooter.Id));
            impact += new Vector2(DetMath.Cos(ang) * r, DetMath.Sin(ang) * r);
        }
        Entity victim = target;
        if (!hit)
        {
            UnitsInRadius(impact, w.Spray + Defs.MaxUnitRadius, sprayBuffer);
            // 가중 선택: 착탄점에 가까울수록 (1/(d+0.25)), 표적 자신은 제외 (이미 빗나감)
            float total = 0;
            for (int i = 0; i < sprayBuffer.Count; i++)
            {
                var e = sprayBuffer[i];
                if (e.Id == target.Id || !e.Alive || !IsHostile(shooter.Owner, e.Owner)) continue;
                float d = Vector2.Distance(impact, e.Pos) - e.Radius;
                if (d > w.Spray) continue;
                total += 1f / (MathF.Max(0, d) + 0.25f);
            }
            if (total > 0)
            {
                float pick = Roll(shooter.Id, -5 - target.Id) * total, acc = 0;
                Unit? chosen = null;
                for (int i = 0; i < sprayBuffer.Count; i++)
                {
                    var e = sprayBuffer[i];
                    if (e.Id == target.Id || !e.Alive || !IsHostile(shooter.Owner, e.Owner)) continue;
                    float d = Vector2.Distance(impact, e.Pos) - e.Radius;
                    if (d > w.Spray) continue;
                    acc += 1f / (MathF.Max(0, d) + 0.25f);
                    chosen = e;
                    if (acc >= pick) break;
                }
                if (chosen != null && Roll(chosen.Id, -9 - shooter.Id) < ShotChance(shooter, chosen, w, MathF.Max(0, chosen.EdgeDistance(shooter.Pos) - shooter.Radius)))
                {
                    hit = true; victim = chosen; impact = chosen.Pos;
                }
            }
        }
        Shots.Add((from, impact, shooter.Owner, Tick, hit, shooter.Id));
        // 제압: 착탄점 spray 안 적 전원 (표적 포함)
        UnitsInRadius(impact, w.Spray + Defs.MaxUnitRadius, sprayBuffer);
        for (int i = 0; i < sprayBuffer.Count; i++)
        {
            var e = sprayBuffer[i];
            if (!e.Alive || e.Owner == shooter.Owner) continue;
            if (e.Id != target.Id && !IsHostile(shooter.Owner, e.Owner)) continue;
            if (Vector2.Distance(impact, e.Pos) - e.Radius > w.Spray) continue;
            Suppress(e, w, from);
        }
        sprayBuffer.Clear();
        if (!aimedHit) NearMissPath(shooter.Owner, from, impact, w.Suppress, target.Id, impact, w.Spray);   // 빗나간 탄: 흩뿌림 반경 밖 사선 근접 제압 (Suppression.cs)
        if (hit) Damage(victim, w.Damage * (victim is Unit vu ? w.BonusAgainst(vu.Def.Category) : 1f), shooter, w.Key);
        else
        {
            MarkAlert(target.Owner, target.Pos);
            Retaliate(target, shooter);
        }
    }

    // ---------------------------------------------------------------- 저장 (CMB2 구역 뒤쪽) · 체크섬

    void WriteCombat3(SnapWriter w)
    {
        foreach (var u in Units)
        {
            w.I(u.Suppression); w.I(u.Kills); w.Byte(u.ChargeState); w.L(u.HesitateBlockUntil); w.F(u.Fatigue); w.Bool(u.QuickStep);
            w.I(u.Grenades); w.F(u.GrenadeRegen); w.Bool(u.ChargeOnly); w.I(u.ChargeRun); w.Bool(u.ImpactSpent); w.I(u.MarchTarget);
            w.L(u.SwordWaveTick); w.I(u.StartKills);
            w.F(u.GroundPoint.X); w.F(u.GroundPoint.Y);   // 지점 포격 목표 (GroundFire.cs, FormatVersion 21)
            w.Byte(u.OfficerBase); w.Byte(u.TempRank); w.I(u.OfficerXp); w.I(u.BurstLeft);   // 장교 계급 · 임시 진급 · 경험치 (CommandRanks.cs, FormatVersion 23) · 경기관총 점사 (FormatVersion 24)
            w.I(u.ChargeCdLeft);   // 기병 돌격 충격 쿨다운 (FormatVersion 25)
            w.Bool(u.Retreating);   // 후퇴 중 (Pursuit.cs, FormatVersion 29)
            w.Bool(u.Pinned);   // 고정 (엎드림, Suppression.cs, FormatVersion 30)
            w.Bool(u.Prone); w.L(u.BrokenUntil); w.L(u.BrokenAt); w.F(u.BreakTo.X); w.F(u.BreakTo.Y); w.L(u.BearerFellUntil);   // N68 기수 쓰러짐 (FormatVersion 45)
              // supp 엎드림 · 붕괴 (Suppression.cs, FormatVersion 41)
            w.Bool(u.Dismounted); w.I(u.MountLeft); w.Bool(u.MountLock);   // N61 하마 (Dismount.cs, FormatVersion 37)
            w.Bool(u.BayonetFixed); w.I(u.FixLeft);   // N57 착검 (Bayonet.cs, FormatVersion 36)
            w.I(u.MeleeVictims); w.I(u.MeleeLastId);   // N61 기병 동시 근접 제한 (FormatVersion 38)
            w.Bool(u.Surrendered); w.I(u.SuppHighTicks); w.L(u.StandUntil); w.L(u.ProneHoldSince); w.Bool(u.ForceCross);   // N79b 항복 (Surrender.cs, FormatVersion 43)
            w.L(u.HopUntil); w.F(u.HopTo.X); w.F(u.HopTo.Y);   // assaultinf 약진 (ChargeHop.cs)
        }
        w.I(Blasts.Count);
        foreach (var b in Blasts) { w.I(b.ThrowerId); w.I(b.Owner); w.F(b.Pos.X); w.F(b.Pos.Y); w.L(b.ExplodeTick); }
        WriteShells(w);   // 날아가는 포탄 (ShellFlight.cs)
    }

    void ReadCombat3(SnapReader r)
    {
        int max = Defs.CombatRules.SuppressMax;
        foreach (var u in Units)
        {
            int s = r.I();
            if (s < 0 || s > Math.Max(max, short.MaxValue)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "suppression"));
            u.Suppression = (short)Math.Min(s, short.MaxValue);
            int k = r.I();
            if (k < 0 || k > short.MaxValue) throw new SnapshotException(string.Format(SnapMsg.Invalid, "kills"));
            u.Kills = (short)k;
            u.ChargeState = r.Byte();
            if (u.ChargeState > 3) throw new SnapshotException(string.Format(SnapMsg.Invalid, "charge state"));
            u.HesitateBlockUntil = r.L();
            u.Fatigue = r.F();
            if (!(u.Fatigue >= 0 && u.Fatigue <= u.StaminaCap)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "fatigue"));
            u.QuickStep = r.Bool();
            u.Grenades = r.I();
            if (u.Grenades < 0 || u.Grenades > Math.Max(1, u.Def.Grenades)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "grenades"));
            u.GrenadeRegen = r.F();
            u.ChargeOnly = r.Bool();
            u.ChargeRun = r.I();
            u.ImpactSpent = r.Bool();
            u.MarchTarget = r.I();
            u.SwordWaveTick = r.L();
            int sk = r.I();
            if (sk < 0 || sk > short.MaxValue) throw new SnapshotException(string.Format(SnapMsg.Invalid, "start kills"));
            u.StartKills = (short)sk;
            u.GroundPoint = new Vector2(r.F(), r.F());
            if (!float.IsFinite(u.GroundPoint.X) || !float.IsFinite(u.GroundPoint.Y)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "ground point"));
            u.OfficerBase = r.Byte(); u.TempRank = r.Byte(); u.OfficerXp = r.I();
            u.BurstLeft = r.I();
            if (u.BurstLeft < 0 || u.BurstLeft > 10000) throw new SnapshotException(string.Format(SnapMsg.Invalid, "burst left"));
            u.ChargeCdLeft = r.I();
            if (u.ChargeCdLeft < 0 || u.ChargeCdLeft > 60 * 60) throw new SnapshotException(string.Format(SnapMsg.Invalid, "charge cooldown"));
            u.Retreating = r.Bool();
            u.Pinned = r.Bool();
            u.Prone = r.Bool(); u.BrokenUntil = r.L(); u.BrokenAt = r.L(); u.BreakTo = new Vector2(r.F(), r.F()); u.BearerFellUntil = r.L();
            if (u.BearerFellUntil < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "bearer fell"));
            if (u.BrokenUntil < 0 || u.BrokenAt < 0 || !float.IsFinite(u.BreakTo.X) || !float.IsFinite(u.BreakTo.Y)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "broken"));
            u.Dismounted = r.Bool(); u.MountLeft = r.I(); u.MountLock = r.Bool();
            if (u.MountLeft < 0 || u.MountLeft > 60 * 60) throw new SnapshotException(string.Format(SnapMsg.Invalid, "mount left"));
            u.BayonetFixed = r.Bool(); u.FixLeft = r.I();
            u.MeleeVictims = r.I(); u.MeleeLastId = r.I();
            u.Surrendered = r.Bool(); u.SuppHighTicks = r.I(); u.StandUntil = r.L(); u.ProneHoldSince = r.L(); u.ForceCross = r.Bool();
            u.HopUntil = r.L(); u.HopTo = new Vector2(r.F(), r.F());
            if (u.HopUntil < 0 || !float.IsFinite(u.HopTo.X) || !float.IsFinite(u.HopTo.Y)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "charge hop"));
            if (u.StandUntil < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "stand until"));
            if (u.ProneHoldSince < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "prone hold since"));
            if (u.SuppHighTicks < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "surrender"));
            if (u.MeleeVictims < 0 || u.MeleeVictims > 100000) throw new SnapshotException(string.Format(SnapMsg.Invalid, "melee victims"));
            if (u.FixLeft < 0 || u.FixLeft > 60 * 60) throw new SnapshotException(string.Format(SnapMsg.Invalid, "bayonet fix"));
            if (u.OfficerBase > 6 || u.TempRank > 6 || u.OfficerXp < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "officer rank"));
        }
        Blasts.Clear();
        int nb = r.Count(1);
        for (int i = 0; i < nb; i++)
            Blasts.Add(new GrenadeBlast { ThrowerId = r.I(), Owner = r.I(), Pos = new Vector2(r.F(), r.F()), ExplodeTick = r.L() });
        ReadShells(r);
    }

    void HashCombat3(ref Hasher h)
    {
        foreach (var u in Units)
        {
            h.Add(u.Suppression);
            h.Add(u.Kills);
            h.Add(u.ChargeState);
            h.Add(u.HesitateBlockUntil);
            h.Add(u.Fatigue);
            h.Add(u.QuickStep ? 1 : 0);
            h.Add(u.Grenades);
            h.Add(u.GrenadeRegen);
            h.Add((u.ChargeOnly ? 1 : 0) + (u.ImpactSpent ? 2 : 0));
            h.Add(u.ChargeRun);
            h.Add(u.MarchTarget);
            h.Add(u.SwordWaveTick);
            h.Add(u.StartKills);
            h.Add(u.GroundPoint.X); h.Add(u.GroundPoint.Y);
            h.Add(u.OfficerBase | u.TempRank << 8); h.Add(u.OfficerXp); h.Add(u.BurstLeft);
            h.Add(u.ChargeCdLeft);
            h.Add((u.Retreating ? 1 : 0) | (u.Pinned ? 2 : 0) | (u.BayonetFixed ? 4 : 0) | (u.Prone ? 8 : 0));
            h.Add(u.BrokenUntil); h.Add(u.BrokenAt); h.Add(u.BreakTo.X); h.Add(u.BreakTo.Y);   // supp 붕괴
            h.Add(u.BearerFellUntil);   // N68
            h.Add(u.FixLeft);   // N57
            h.Add(u.MeleeVictims); h.Add(u.MeleeLastId);   // N61 동시 근접 제한
            h.Add(u.Surrendered ? 1 : 0); h.Add(u.SuppHighTicks); h.Add(u.StandUntil); h.Add(u.ProneHoldSince); h.Add(u.ForceCross ? 1 : 0);   // N79b 항복
            h.Add(u.HopUntil); h.Add(u.HopTo.X); h.Add(u.HopTo.Y);   // assaultinf 약진
            h.Add((u.Dismounted ? 1 : 0) | (u.MountLock ? 2 : 0)); h.Add(u.MountLeft);   // N61
        }
        foreach (var b in Blasts) { h.Add(b.ThrowerId); h.Add(b.Pos.X); h.Add(b.Pos.Y); h.Add(b.ExplodeTick); }
        HashShells(ref h);
    }
}
