using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// v0.4 전투 수치 (ROADMAP §2 · §5): 거리별 명중 곡선 × 유닛 명중률 × 지형(훅), 포탑 회전 시뮬, 포병(즉발 범위 포격 · 퍼짐 · 최소 사거리 · 관측 사격 지연).
/// 시야 2단계 · 경계 · 은폐 탐지는 Detection.cs. 저장(CMB2 구역) · 체크섬은 이 파일 아래쪽.
/// </summary>
public sealed partial class Unit
{
    /// <summary>[렌더러] 포탑 방향(라디안, 월드 기준). 포탑이 없는 유닛(turretTurnRate 0)은 매 틱 Facing 과 같게 맞춘다.</summary>
    [SnapExt] public float TurretFacing = -MathF.PI / 2;
    /// <summary>사격 후 은폐가 0 으로 취급되는 마지막 틱 (총구 섬광, World.RevealTicks).</summary>
    [SnapExt] internal long RevealUntil = -1;
    /// <summary>[관측 세력 Id] 이 틱까지 탐지된 상태 (은폐 유닛에만 할당, 16세력 × 4바이트). null = 아직 판정 없음.</summary>
    [SnapExt] internal int[]? DetectedUntil;

    public bool HasTurret => Def.TurretTurnRate > 0;

    // ---- 견인포 방열 / 철수 (ROADMAP §5.1)
    /// <summary>방열 단계 (setupTime 이 있는 유닛만 바뀐다 — 그 외는 항상 Packed).</summary>
    [SnapExt] internal byte DeployState;
    /// <summary>방열 · 철수 완료까지 남은 틱.</summary>
    [SnapExt] internal int DeployLeft;
    /// <summary>방열 사계 중심 (라디안, 방열을 시작할 때 정함 — 표적 방향 또는 그때 차체 방향). 방열 중 · 방열 완료면 차체는 이 ± 사계 절반 안에서만 돈다.</summary>
    [SnapExt] internal float DeployFacing;
    /// <summary>[렌더러 · AI] 방열 사계 중심 (라디안).</summary>
    public float DeployCenter => DeployFacing;

    /// <summary>방열이 필요한 무기 (견인포): 방열 상태에서만 사격, 방열 · 방열 중에는 이동 불가, 이동 전 철수.</summary>
    public bool CanDeploy => Def.SetupTime > 0;
    public DeployPhase Deploy => (DeployPhase)DeployState;
    /// <summary>[렌더러] 방열 완료 (사격 가능 자세).</summary>
    public bool Deployed => DeployState == (byte)DeployPhase.Deployed;
    /// <summary>[렌더러] 방열 진행도 0(이동 자세) ~ 1(방열 완료). 방열 중에는 올라가고 철수 중에는 내려간다.</summary>
    public float DeployProgress
    {
        get
        {
            switch (Deploy)
            {
                case DeployPhase.Deployed: return 1f;
                case DeployPhase.Deploying: { int t = World.SetupTicksOf(this); return t <= 0 ? 1f : Math.Clamp(1f - DeployLeft / (float)t, 0f, 1f); }   // arty3: 시작 지연 동안은 0
                case DeployPhase.Packing: { int t = World.PackTicks(Def); return t <= 0 ? 0f : DeployLeft / (float)t; }
                default: return 0f;
            }
        }
    }
}

/// <summary>견인포 자세: Packed(이동 가능) → Deploying(방열 중) → Deployed(사격 가능) → Packing(철수 중) → Packed.</summary>
public enum DeployPhase : byte { Packed = 0, Deploying = 1, Deployed = 2, Packing = 3 }

public sealed partial class World
{
    /// <summary>명중률 상한.</summary>
    public const float HitCap = 0.95f;
    /// <summary>포탑 · 차체가 표적을 이만큼(라디안, 5°) 안으로 겨눠야 발사.</summary>
    public const float FireAngleTolerance = 5f * (DetMath.Pi / 180f);
    /// <summary>사격 후 은폐 0 유지 틱 (3초).</summary>
    public const int RevealTicks = 180;

    /// <summary>[렌더러 · 효과음] 포탄 착탄 · 수류탄 폭발 (포구 위치, 착탄점, 사격 세력, 착탄 틱, 범위 반경, 사격자 Id, 무기 키 — 수류탄 "grenade"). 40틱 뒤 지운다. 포구 섬광 · 발사음은 발사 틱의 Shots 한 줄 (ShellFlight.cs).</summary>
    public readonly List<(Vector2 From, Vector2 Impact, int Faction, long Tick, float Radius, int ShooterId, string Weapon)> Impacts = new();

    readonly List<Unit> splashBuffer = new();

    /// <summary>[측정 훅] 은폐 탐지 판정 · 포병 범위 피해에 쓴 Stopwatch 틱 누적 (Profile 일 때만).</summary>
    public static long ProfDetect, ProfSplash;

    // ---------------------------------------------------------------- 명중률 (§2)

    /// <summary>곡사 무기 (포병, weapon.fire = "indirect"): 범위 포격, 장애물 완전 무시. 소속 세력이 보는 모든 표적을 [minRange, range] 에서 노린다.</summary>
    public static bool IsIndirect(WeaponDef? w) => w != null && w.Indirect;

    /// <summary>
    /// 무기 거리 곡선 (ROADMAP §2-3): p(d) = accNear − (accNear − accFar)·(d/range)² — 밀착 accNear 에서 최대 사거리 accFar 까지 거리 제곱에 비례해 연속 감소,
    /// 사거리 밖은 accFar. 곡선이 없으면(accNear 0) Accuracy 고정.
    /// </summary>
    public static float AccuracyCurve(WeaponDef w, float dist)
    {
        if (w.AccNear <= 0 || w.Range <= 0) return w.Accuracy;
        float r = dist <= 0 ? 0f : dist / w.Range;
        if (r >= 1f) return w.AccFar;
        return w.AccNear - (w.AccNear - w.AccFar) * r * r;
    }

    /// <summary>사격 후 재장전 시간(초): cooldownMin~Max 사이의 결정적 난수 (사격자 Id · 현재 틱 해시, 사격마다 다름) 또는 고정 cooldown.</summary>
    float NextCooldown(Entity shooter, WeaponDef w) =>
        (w.RandomCooldown ? w.CooldownMin + (w.CooldownMax - w.CooldownMin) * Roll(shooter.Id, -1 - shooter.Id) : w.Cooldown)
        * (shooter is Unit su && IsArtyGun(su, w) ? ArtyRateMul(su) : 1f);

    /// <summary>N34: 포 = 곡사 무기 또는 방열하는 폭발 무기 (보병포 · 야전포 · 곡사포).</summary>
    internal static bool IsArtyGun(Unit u, WeaponDef w) => IsIndirect(w) || (u.CanDeploy && w.Splash > 0);

    /// <summary>N34 포 발사 속도 배수 (간격에 곱함): artyRateMin~Max, 유닛 Id · 지도 시드 해시 — 틱과 무관한 고정값 (저장 불필요).</summary>
    internal float ArtyRateMul(Unit u)
    {
        var c = Defs.CombatRules;
        return c.ArtyRateMin + (c.ArtyRateMax - c.ArtyRateMin) * StableRoll(u.Id, 0x41525459);
    }

    /// <summary>[0,1) 틱과 무관한 결정적 난수 (유닛 고유 성질용).</summary>
    float StableRoll(int a, int b)
    {
        ulong h = (ulong)(uint)a * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)b * 0x165667B19E3779F9UL ^ (ulong)(uint)Map.Seed * 0x9E3779B97F4A7C15UL;
        h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33;
        return (h >> 40) / (float)(1UL << 24);
    }

    /// <summary>직사 무기의 사선 (weapon.los): 사격자 → 표적 직선이 막힌 칸(건물 · 지형 · 깊은 숲 · 평지에서 본 고지 너머)을 지나면 false (GameMap.HasFiringLine). 사격자 · 표적 자신의 건물 발자국은 막힌 것으로 치지 않는다. los 가 없는 무기는 항상 true.</summary>
    public bool HasFiringLine(Entity shooter, Entity target, WeaponDef w) =>
        !w.Los || (Map.HasFiringLine(shooter.Pos, target.Pos, shooter.Id, target.Id) && !SmokeBlocks(shooter.Pos, target.Pos));   // 연막 (N59, Breach.cs)

    /// <summary>엄폐 · 지형 보정: 표적의 엄폐 (숲 · 잔해 · 참호 지형 · 포탄 구덩이 · 가옥 옆 중 가장 좋은 값, Supply.cs) × 높이 (고지 → 평지 terrain.highToLowHit, 반대 lowToHighHit).</summary>
    float TerrainAccuracy(Entity shooter, Entity target, bool props = true)
    {
        float m = CoverHitMul(target);
        if (props && Props.Count > 0)   // F6 소품 엄폐 (사격자 쪽 소품 뒤, 지형 엄폐와 겹치지 않고 좋은 쪽) + 시선 부분 차단
        {
            if (target is Unit pu) m = MathF.Min(m, PropCover(pu.Pos, shooter.Pos));
            m *= Props.LosMul(shooter.Pos, target.Pos);
        }
        if (target is Unit tu && (tu.Pinned || tu.Prone)) m = MathF.Min(m, Defs.CombatRules.ProneHitMul);   // 엎드린 (고정) 표적 = 작은 표적 — 지형 엄폐와 겹치지 않고 더 좋은 쪽 (Suppression.cs)
        if (target is Unit mu && mu.Def.MovingHitMul != 1f && Tick - mu.LastMoveTick <= 1) m *= mu.Def.MovingHitMul;   // assaultinf: 이동 중 피격 감소
        bool sh = Map.IsHigh(shooter.Pos), th = Map.IsHigh(target.Pos);
        if (sh != th) m *= sh ? Defs.Terrain.HighToLowHit : Defs.Terrain.LowToHighHit;
        return m;
    }

    /// <summary>최종 명중률 = 곡선(거리) × 유닛 명중률(건물 1.0) × 지형, 상한 HitCap. dist = 사격자 가장자리에서 표적 가장자리까지.</summary>
    public float HitChance(Entity shooter, Entity target, WeaponDef w, float dist)
    {
        float p = AccuracyCurve(w, dist);
        if (shooter is Unit su)
        {
            p *= su.Def.Accuracy;
            p *= SuppressAccuracyMul(su);   // 제압 · 고정 (Suppression.cs)
            if (su.MarchShot) p *= su.Def.MarchAccMul > 0 ? su.Def.MarchAccMul : w.Submachine ? Defs.CombatRules.MarchSmgAccMul : Defs.CombatRules.MarchAccMul;   // 행진 사격 (Combat4.cs)
            p *= MountedFireMul(su);   // 승마 사격 (Dismount.cs, N61)
            if (su.RetreatShot) p *= Defs.Tactics.Pursuit.RetreatFireAccMul;   // 후퇴 사격 (Pursuit.cs, pursuit2)
        }
        p *= TerrainAccuracy(shooter, target, !IsIndirect(w));   // 곡사는 소품 엄폐 · 시선 차단 없음
        p *= WeatherHitMul;   // 날씨 · 시간대 (Weather.cs)
        return p > HitCap ? HitCap : p;
    }

    /// <summary>유닛이 지금 위치에서 표적을 쏠 때의 명중률 (UI · 테스트용). 무기가 없으면 0.</summary>
    public float HitChance(Unit shooter, Entity target) =>
        shooter.Weapon is { } w ? HitChance(shooter, target, w, MathF.Max(0, target.EdgeDistance(shooter.Pos) - shooter.Radius)) : 0;


    // ---------------------------------------------------------------- 포탑 · 조준 각 (§2)

    /// <summary>포탑 회전: 표적이 있으면 표적 쪽으로(차체가 움직여도 계속 추적), 없으면 차체 정면으로. 포탑 없는 유닛은 차체와 같다.</summary>
    void UpdateTurretFacing(Unit u, float dt)
    {
        if (!u.HasTurret) { u.TurretFacing = u.Facing; return; }
        float want = u.Facing;
        if ((u.Target ?? (u.MarchTarget != 0 ? Find(u.MarchTarget) : null)) is { } t)
        {
            Vector2 d = t.Pos - u.Pos;
            if (d.LengthSquared() > 1e-6f) want = DetMath.Atan2(d.Y, d.X);
        }
        u.TurretFacing = TurnToward(u.TurretFacing, want, u.Def.TurretTurnRate * (DetMath.Pi / 180f) * dt);
    }

    /// <summary>
    /// 발사 각 조건: 포탑 유닛은 포탑이, 포탑 없는 차량(포병 · 방열하는 대포)은 차체가 표적을 FireAngleTolerance 안으로 겨눠야 한다.
    /// 보병 · 기타 유닛은 제한 없음 (FaceToward 가 조준 시간 안에 돌아선다).
    /// </summary>
    bool AimedAt(Unit u, Entity t, WeaponDef w)
    {
        bool gate = u.HasTurret || IsIndirect(w) || u.CanDeploy;
        if (!gate) return true;
        Vector2 d = t.Pos - u.Pos;
        if (d.LengthSquared() <= 1e-6f) return true;
        float ang = DetMath.Atan2(d.Y, d.X);
        float cur = u.HasTurret ? u.TurretFacing : u.Facing;
        float diff = DetMath.WrapPi(ang - cur);
        return (diff < 0 ? -diff : diff) <= FireAngleTolerance;
    }

    /// <summary>[렌더러 · 테스트] 포탑(또는 차체)과 표적 방향의 차이 (라디안, 절댓값). 표적이 없으면 0.</summary>
    public float AimError(Unit u)
    {
        if (u.Target is not { } t) return 0;
        Vector2 d = t.Pos - u.Pos;
        if (d.LengthSquared() <= 1e-6f) return 0;
        float diff = DetMath.WrapPi(DetMath.Atan2(d.Y, d.X) - (u.HasTurret ? u.TurretFacing : u.Facing));
        return diff < 0 ? -diff : diff;
    }

    /// <summary>표적을 바꿀 때의 조준 틱: 무기 aimTime + (자기 시야 밖 표적이면) spotterDelay — 아군 관측 사격은 뜸을 들인다.</summary>
    int AimTicksFor(Unit u, Entity t, WeaponDef w)
    {
        int ticks = w.AimTicks;
        if (Defs.CombatRules.CavReactOn && u.Def.IsInfantry && IsCavThreat(t) && !IsIndirect(w) && !u.CanDeploy) return Math.Max(ticks, (int)MathF.Round(Defs.CombatRules.CavReactAimSec / Config.SimDt));   // n61: 돌격 기병은 짧은 조준만
        if (w.SpotterDelay > 0 && !InOwnSight(u, t)) ticks += Math.Max(0, (int)MathF.Round(w.SpotterDelay / Config.SimDt));
        return Math.Max(ticks, FirstShotTicks(u, w));
    }

    /// <summary>
    /// N34 첫 사격 반응 (틱, 0 = 해당 없음): firstShotIdleSec 넘게 쏘지 않은 보병 (곡사 · 방열 무기 제외) 이 새 표적을 잡으면
    /// firstShotMin~Max 초 × 숙련 배수 (신병 · 정규 · 노병, CloseRanks.SkillOf) — 유닛 Id · 틱 해시. 이어지는 사격 간격은 무기 간격 난수 그대로.
    /// </summary>
    internal int FirstShotTicks(Unit u, WeaponDef w)
    {
        var c = Defs.CombatRules;
        if (c.FirstShotMax <= 0 || !u.Def.IsInfantry || IsIndirect(w) || u.CanDeploy || w.Range < 1f) return 0;
        if (u.LastShotTick > 0 && (Tick - u.LastShotTick) * Config.SimDt < c.FirstShotIdleSec) return 0;
        float sec = c.FirstShotMin + (c.FirstShotMax - c.FirstShotMin) * Roll(u.Id, 0x46535354);
        var mul = c.FirstShotSkillMul;
        int sk = (int)SkillOf(u);
        if (mul != null && sk < mul.Length) sec *= mul[sk];
        return Math.Max(0, (int)MathF.Round(sec / Config.SimDt));
    }

    /// <summary>표적이 유닛 자신의 현재 시야(경계 시 최대 시야) 안인지. 아군이 보여 주는 표적은 false.</summary>
    public bool InOwnSight(Unit u, Entity t) => t.EdgeDistance(u.Pos) <= SightOf(u);

    /// <summary>자동 표적 탐색 반경: 포병은 무기 사거리(세력이 보는 모든 표적), 그 외는 현재 시야.</summary>
    float AcquireRadius(Unit u) => IsIndirect(u.Weapon) ? u.Weapon!.Range + u.Radius : SightOf(u);

    // ---------------------------------------------------------------- 대포 방열 / 철수 (§5.1 → ROADMAP §2-3: 모든 대포, 명령에 따라 자동)
    // setupTime 이 있는 유닛(견인포 · 자주포 · 앞으로의 직사포)은 방열 상태에서만 쏜다. 전환은 행동 갱신이 자동으로 한다:
    // 사거리 안 표적(공격 · 공격 이동 · 대기 · 사수) → AdvanceDeploy, 이동 · 추격 → AdvancePack. 따로 명령은 없다.

    public static int SetupTicks(UnitDef d) => Math.Max(0, (int)MathF.Round(d.SetupTime / Config.SimDt));
    public static int PackTicks(UnitDef d) => Math.Max(0, (int)MathF.Round(d.PackTime / Config.SimDt));

    /// <summary>arty3: 방렬 난수를 쓰는 유닛 = 대포 (category gun). 중기관총 거치는 그대로.</summary>
    static bool GunDeployNoise(Unit u) => u.Def.Category == "gun";

    /// <summary>arty3: 이 유닛의 방렬 시간 (틱) = setupTime × (1 ± gunDeployTimeVar) — 포마다 고정 (PairNoise(Id), 상태 없음). 대포가 아니면 SetupTicks.</summary>
    public static int SetupTicksOf(Unit u)
    {
        int b = SetupTicks(u.Def);
        float v = KoreanDivision.Core.Defs.Current.CombatRules.GunDeployTimeVar;
        if (b <= 0 || v <= 0 || !GunDeployNoise(u)) return b;
        return Math.Max(1, (int)MathF.Round(b * (1f + v * (2f * PairNoise(u.Id, 0x44455031, 0) - 1f))));
    }

    /// <summary>arty3: 방렬 시작 지연 (틱) 0 ~ gunDeployDelayMax 초 — 포마다 고정 (PairNoise(Id), 상태 없음). DeployLeft 에 더해 방렬 중 단계 앞쪽에서 소모된다.</summary>
    public static int DeployDelayTicksOf(Unit u)
    {
        float m = KoreanDivision.Core.Defs.Current.CombatRules.GunDeployDelayMax;
        if (m <= 0 || !GunDeployNoise(u) || SetupTicks(u.Def) <= 0) return 0;
        return (int)MathF.Round(m * PairNoise(u.Id, 0x44455032, 0) / Config.SimDt);
    }

    /// <summary>
    /// 사격하려는 틱마다 호출: 방열 완료면 true. 아니면 방열을 진행하고(철수 중이면 철수를 마저 끝내고) false — 그동안 조준은 시작하지 않는다.
    /// 방열이 필요 없는 유닛은 항상 true.
    /// </summary>
    bool AdvanceDeploy(Unit u, float? centre = null)
    {
        if (!u.CanDeploy) return true;
        switch (u.Deploy)
        {
            case DeployPhase.Deployed: return true;
            case DeployPhase.Deploying:
                if (--u.DeployLeft <= 0) { u.DeployLeft = (int)Tick; u.DeployState = (byte)DeployPhase.Deployed; return true; }   // Deployed 동안 DeployLeft = 방열 완료 틱 (bal2 최소 대기, 저장 형식 변경 없음)
                return false;
            case DeployPhase.Packing:
                if (--u.DeployLeft <= 0) { u.DeployLeft = 0; u.DeployState = (byte)DeployPhase.Packed; }
                return false;
            default:
                if (HeavyGapStep(u)) return false;   // arty3 heavy 간격: 방렬 · 거치한 아군 중화기 옆이면 먼저 비켜선다 (Arty3.cs)
                u.DeployState = (byte)DeployPhase.Deploying;
                u.DeployFacing = centre ?? u.Facing;   // 사계 중심 고정
                u.DeployLeft = SetupTicksOf(u) + DeployDelayTicksOf(u);   // arty3: 시작 지연 + 방렬 시간 ±15%
                if (u.DeployLeft <= 0) { u.DeployLeft = (int)Tick; u.DeployState = (byte)DeployPhase.Deployed; return true; }
                return false;
        }
    }

    /// <summary>
    /// 이동하려는 틱마다 호출: 이동 자세(Packed)면 true. 방열 완료 · 방열 중이면 철수를 시작하고(방열 중이던 만큼만 비례해 짧게), 철수 중이면 진행하며 false.
    /// </summary>
    /// <summary>bal2: 방열 유지 (minDeployStay 있는 유닛 = 맥심). 중대 깃발을 따라가는 방열 완료 유닛이 깃발 목표가 stayLeaveDist 안이면 계속 서서 쏘고,
    /// stayLeaveDist 밖이어도 방열 뒤 minDeployStay 초 전이면 서 있는다. stayFarDist(= 2배) 밖이거나 개별 명령 (중대 깃발 아님) 이면 곧바로 철수.</summary>
    bool HoldDeployedStay(Unit u)
    {
        if (u.Deploy != DeployPhase.Deployed || u.Def.MinDeployStay <= 0f) return false;
        if (u.Company?.Anchor is not { } a || u.Group == null || !ReferenceEquals(a.Group, u.Group)) return false;
        float d = Vector2.Distance(a.Goal, u.Pos), leave = u.Def.StayLeaveDist;
        if (d <= leave) return true;
        if (d > leave * 2f) return false;
        return Tick - u.DeployLeft < (long)MathF.Round(u.Def.MinDeployStay / Config.SimDt);
    }

    bool AdvancePack(Unit u)
    {
        if (!u.CanDeploy) return true;
        switch (u.Deploy)
        {
            case DeployPhase.Packed: return true;
            case DeployPhase.Packing:
                if (--u.DeployLeft <= 0) { u.DeployLeft = 0; u.DeployState = (byte)DeployPhase.Packed; return true; }
                return false;
            case DeployPhase.Deploying:
            {
                int setup = SetupTicksOf(u), done = Math.Max(0, setup - u.DeployLeft);   // arty3: 시작 지연 중이면 0
                u.DeployLeft = setup > 0 ? (int)((long)PackTicks(u.Def) * done / setup) : 0;
                u.DeployState = (byte)DeployPhase.Packing;
                if (u.DeployLeft <= 0) { u.DeployState = (byte)DeployPhase.Packed; return true; }
                return false;
            }
            default:
                u.DeployState = (byte)DeployPhase.Packing;
                u.DeployLeft = PackTicks(u.Def);
                if (u.DeployLeft <= 0) { u.DeployState = (byte)DeployPhase.Packed; return true; }
                return false;
        }
    }

    // ---------------------------------------------------------------- 방열 사계 (traverseArc)

    /// <summary>사계 절반 (라디안). 방열하는 대포 + 무기 traverseArc > 0 일 때만, 아니면 0 (제한 없음).</summary>
    public static float TraverseHalf(Unit u) => u.CanDeploy && u.Weapon is { TraverseArc: > 0 } w ? w.TraverseArc * 0.5f * (DetMath.Pi / 180f) : 0f;

    /// <summary>지금 사계에 묶여 있는가 (방열 중 · 방열 완료). 이동 자세 · 철수 중이면 false.</summary>
    public static bool TraverseLocked(Unit u) => u.Deploy is DeployPhase.Deploying or DeployPhase.Deployed && TraverseHalf(u) > 0;

    /// <summary>
    /// [AI · 명령] u 가 지금 자세 그대로 p 쪽으로 쏠 수 있는가 (사계 안). 묶여 있지 않으면 (이동 자세 · 사계 없는 무기) 항상 true —
    /// false 면 쏘려면 철수 → 회전 → 재방열 (setupTime + packTime) 이 필요하다. 공격 명령은 자동으로 그렇게 하고, 자동 표적은 사계 안만 고른다.
    /// </summary>
    public static bool CanTraverseTo(Unit u, Vector2 p)
    {
        if (!TraverseLocked(u)) return true;
        Vector2 d = p - u.Pos;
        if (d.LengthSquared() <= 1e-6f) return true;
        float diff = DetMath.WrapPi(DetMath.Atan2(d.Y, d.X) - u.DeployFacing);
        return (diff < 0 ? -diff : diff) <= TraverseHalf(u) + 1e-4f;
    }

    /// <summary>원하는 방향 want 를 사계 안으로 자른다 (묶여 있을 때만).</summary>
    internal static float ClampTraverse(Unit u, float want)
    {
        if (!TraverseLocked(u)) return want;
        float half = TraverseHalf(u);
        return u.DeployFacing + Math.Clamp(DetMath.WrapPi(want - u.DeployFacing), -half, half);
    }

    // ---- N10 재조준 (2026-10-19): 사계 밖 표적이라도 사계 중심을 조금만 옮기면 되면 방열한 채 포가를 돌린다 (철수 없음).
    //      옮길 각 ≥ combat.traverseRedeployDeg (기본 90°) 일 때만 철수 → 회전 → 재방열 (공격 명령 · 지점 포격). 자동 표적은 그 각 밖을 고르지 않는다.

    /// <summary>[AI · 렌더] 지점 p 를 사계 안에 넣으려면 사계 중심을 옮겨야 하는 각 (라디안). 묶여 있지 않거나 이미 사계 안이면 0.</summary>
    public static float TraverseShiftTo(Unit u, Vector2 p)
    {
        if (!TraverseLocked(u)) return 0f;
        Vector2 d = p - u.Pos;
        if (d.LengthSquared() <= 1e-6f) return 0f;
        float diff = DetMath.WrapPi(DetMath.Atan2(d.Y, d.X) - u.DeployFacing);
        float s = (diff < 0 ? -diff : diff) - TraverseHalf(u) - 1e-4f;
        return s > 0 ? s : 0f;
    }

    /// <summary>재방열 문턱 (라디안, combat.traverseRedeployDeg).</summary>
    static float RedeployRad => KoreanDivision.Core.Defs.Current.CombatRules.TraverseRedeployDeg * (DetMath.Pi / 180f);

    /// <summary>[AI · 명령] u 가 철수 없이 (사계 안이거나, 방열한 채 사계 중심을 문턱 미만으로 돌려) p 를 쏠 수 있는가.</summary>
    public static bool CanTraverseInPlace(Unit u, Vector2 p) => TraverseShiftTo(u, p) < RedeployRad;

    /// <summary>
    /// 사계 밖 · 문턱 미만인 지점 p 쪽으로 방열한 채 포가를 돌리는 한 틱: 사계 중심과 차체를 같은 각만큼 (turnRate × traverseShiftMul) 돌린다.
    /// 그동안 사격하지 않는다. 옮길 각이 사계 절반보다 크면 조준을 처음부터 (작은 수정은 조준 유지).
    /// </summary>
    void ShiftTraverse(Unit u, Vector2 p, float dt)
    {
        Vector2 d = p - u.Pos;
        float want = DetMath.Atan2(d.Y, d.X);
        float diff = DetMath.WrapPi(want - u.DeployFacing);
        float need = (diff < 0 ? -diff : diff) - TraverseHalf(u);   // 표적이 사계 가장자리에 들 때까지
        if (need > TraverseHalf(u)) { u.AimLeft = 0; u.AimTarget = 0; }
        float step = MathF.Min(need + 1e-3f, u.Def.TurnRadPerSec * Defs.CombatRules.TraverseShiftMul * dt);
        if (step <= 0) return;
        float delta = diff < 0 ? -step : step;
        u.DeployFacing = DetMath.WrapPi(u.DeployFacing + delta);
        u.Facing = DetMath.WrapPi(u.Facing + delta);
        u.Facing = ClampTraverse(u, u.Facing);
        Stats.TraverseShiftTicks++;
    }

    // ---------------------------------------------------------------- 포병 (§5)

    /// <summary>범위 피해 비율: 중심 1.0 → 가장자리 SplashEdge 직선 감소.</summary>
    public static float SplashFactor(WeaponDef w, float d)
    {
        if (w.Splash <= 0 || d <= 0) return 1f;
        float t = d / w.Splash;
        if (t > 1f) t = 1f;
        return 1f - (1f - w.SplashEdge) * t;
    }

    /// <summary>
    /// N11 거리별 착탄 오차 반경 (칸): scatter × (near + (1 − near) × ((1 − q)·f + q·f²)), f = min(1, 거리/사거리). 가까우면 정확, 최대 사거리에서 scatter.
    /// 곱셈 · 덧셈만 (결정적). 곡사포 · 직사포 빗나감 · 지점 포격 공용.
    /// </summary>
    public static float ScatterAt(WeaponDef w, float dist)
    {
        if (w.Scatter <= 0 || w.Range <= 0) return 0f;
        var c = KoreanDivision.Core.Defs.Current.CombatRules;
        float f = MathF.Min(1f, MathF.Max(0f, dist / w.Range));
        float near = Math.Clamp(c.ScatterNearFrac, 0f, 1f), q = Math.Clamp(c.ScatterQuad, 0f, 1f);
        return w.Scatter * (near + (1f - near) * ((1f - q) * f + q * f * f));
    }

    /// <summary>
    /// 포격: 착탄점 = 표적 위치 + 결정적 퍼짐(반경 ScatterAt(거리), 면적 균등), 반경 Splash 안의 적대 유닛 · 건물에 감소 피해.
    /// 아군 · 중립(강제 지정 표적 제외)은 피해 없음. 발사 때 Shots(섬광 · 소리), 착탄 때 Impacts(폭발).
    /// </summary>
    void FireArtillery(Entity shooter, Vector2 from, Entity target, WeaponDef w)
    {
        long pt0 = Profile ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        Vector2 aim = target.Pos;
        float centerDist = Vector2.Distance(from, aim);
        float maxErr = ScatterAt(w, centerDist) * AdjustFireMul(shooter, aim, w);   // arty3 곡사포 조정 사격 (Arty3.cs)
        float u1 = Roll(shooter.Id, target.Id), u2 = Roll(target.Id, shooter.Id);
        float err = maxErr * MathF.Sqrt(u1);
        float ang = u2 * DetMath.Tau;
        Vector2 impact = aim + new Vector2(DetMath.Cos(ang) * err, DetMath.Sin(ang) * err);
        LaunchShell(shooter, from, impact, target.Id, w);   // 착탄점은 지금 정하고, 폭발은 비행 시간 뒤 그 지점에서 (ShellFlight.cs)
    }

    /// <summary>착탄점 폭발: 폭발 기록, 반경 안 적대 유닛 · 건물에 감소 피해 + 제압 (곡사포 · 직사포 공용, 착탄 틱에 호출). 아군 · 동맹 유닛도 맞음 (combat.artilleryFriendlyFire, pursuit2).
    /// shooter = 피해 출처 (죽었으면 null), targetId = 지정 표적 (중립이어도 맞음).</summary>
    internal void Explode(int shooterId, int owner, Entity? shooter, Vector2 from, Vector2 impact, int targetId, WeaponDef w)
    {
        long pt0 = Profile ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        Impacts.Add((from, impact, owner, Tick, w.Splash, shooterId, w.Key));
        TryCrater(shooterId, impact, w);   // 포탄 구덩이 지형 (Trenches.cs)
        Props.Blast(impact, w);            // F6 소품 파손: 나무 쓰러짐 · 바위 금감 (Props.cs)

        UnitsInRadius(impact, w.Splash + Defs.MaxUnitRadius, splashBuffer);
        bool ff = Defs.CombatRules.ArtilleryFriendlyFire;   // 포병만 아군 오사 (pursuit2): 같은 세력 · 동맹도 피해 · 제압 — 쏜 포 자신과 중립 (지정 표적 제외) 은 그대로 제외
        for (int i = 0; i < splashBuffer.Count; i++)
        {
            var e = splashBuffer[i];
            if (!e.Alive || e.Id == shooterId) continue;
            bool friend = owner > 0 && (e.Owner == owner || Allied(owner, e.Owner));
            if (friend ? !ff : e.Id != targetId && !IsHostile(owner, e.Owner)) continue;
            float d = Vector2.Distance(impact, e.Pos) - e.Radius;
            if (d > w.Splash) continue;
            Suppress(e, w);   // 포격: 폭발 반경 안 전원 제압 (Combat3.cs)
            if (e.Def.ShellCenterKillRadius > 0 && Vector2.Distance(impact, e.Pos) <= e.Def.ShellCenterKillRadius) { Damage(e, e.Hp + 1000f, shooter, w.Key); continue; }   // assaultinf: 착탄 중심 직격 = 사망 (체력 무관)
            Damage(e, w.Damage * SplashFactor(w, d) * w.BonusAgainst(e.Def.Category) * e.Def.ShellDamageMul, shooter, w.Key);
        }
        splashBuffer.Clear();
        NearMissRing(owner, impact, w.Splash, w.Suppress);   // 폭발 반경 바깥 띠 근접 제압 (Suppression.cs)
        foreach (var b in Buildings)
        {
            if (!b.Alive || b.Indestructible || b.Owner == owner) continue;
            if (b.Id != targetId && !IsHostile(owner, b.Owner)) continue;
            float d = b.EdgeDistance(impact);
            if (d > w.Splash) continue;
            Damage(b, w.Damage * SplashFactor(w, d), shooter, w.Key);
        }
        if (Profile) ProfSplash += System.Diagnostics.Stopwatch.GetTimestamp() - pt0;
    }

    // ---------------------------------------------------------------- 저장 (CMB2 구역) · 체크섬

    partial void WriteCombatExtImpl(SnapWriter w, SnapCtx ctx)
    {
        w.I(Units.Count);
        foreach (var u in Units)
        {
            w.F(u.TurretFacing);
            w.L(u.RevealUntil);
            w.Byte(u.DeployState); w.I(u.DeployLeft); w.F(u.DeployFacing);
            var det = u.DetectedUntil;
            w.I(det?.Length ?? 0);
            if (det != null) foreach (int v in det) w.I(v);
        }
        Alert.WriteState(w);
        WriteCombat3(w);   // 제압 · 사살 · 돌격 · 멍칫 상한 (Combat3.cs)
    }

    partial void ReadCombatExtImpl(SnapReader r, SnapCtx ctx)
    {
        int n = r.I();
        if (n != Units.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "combat ext unit count"));
        foreach (var u in Units)
        {
            u.TurretFacing = r.F();
            u.RevealUntil = r.L();
            u.DeployState = r.Byte();
            if (u.DeployState > (byte)DeployPhase.Packing) throw new SnapshotException(string.Format(SnapMsg.Invalid, "deploy state"));
            u.DeployLeft = r.I();
            u.DeployFacing = r.F();
            int len = r.Count(1);
            if (len == 0) u.DetectedUntil = null;
            else
            {
                if (len != Factions.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "detected table size"));
                var det = new int[len];
                for (int i = 0; i < len; i++) det[i] = r.I();
                u.DetectedUntil = det;
            }
        }
        Alert.ReadState(r);
        ReadCombat3(r);
    }

    /// <summary>체크섬 추가 블록 (WorldChecksum.ComputeChecksum 이 부른다): 포탑 · 은폐 노출 · 탐지 표 · 경계 지도(표시된 칸만).</summary>
    partial void HashCombat2(ref Hasher h)
    {
        foreach (var u in Units)
        {
            h.Add(u.TurretFacing);
            h.Add(u.RevealUntil);
            h.Add(u.DeployState * 1_000_000 + u.DeployLeft);
            h.Add(u.DeployFacing);
            if (u.DetectedUntil is { } det) foreach (int v in det) h.Add(v);
        }
        HashAlert(ref h);
        HashCombat3(ref h);
    }
}
