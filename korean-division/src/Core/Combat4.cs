using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 교전 4차 (ROADMAP 포병 · 수류탄 · 추가 병종 · 행진 사격 · 기병): 수류탄, 행진 사격, 곡사포 관측 (장교 · 정찰병), 직사포 폭발, 기병 돌격 전용 · 충격.
/// 모든 값은 틱 · 입력의 함수 (해시 Roll). 저장 = CMB2 뒤쪽 (Combat3.cs WriteCombat3 가 이 파일의 상태도 쓴다).
/// </summary>
public sealed partial class Unit
{
    /// <summary>남은 수류탄 (시작 = Def.Grenades).</summary>
    [SnapExt] public int Grenades { get; internal set; }
    /// <summary>비전투 중 수류탄 회복 누적 시간 (초).</summary>
    [SnapExt] internal float GrenadeRegen;
    /// <summary>기병 '돌격만' 토글: 사격하지 않고 적을 보면 돌격 (전차에는 아무것도 안 함).</summary>
    [SnapExt] public bool ChargeOnly { get; internal set; }
    /// <summary>이번 돌격에서 달린 틱 수 (돌격 충격 판정).</summary>
    [SnapExt] internal int ChargeRun;
    /// <summary>이번 돌격의 충격을 이미 썼는가.</summary>
    [SnapExt] internal bool ImpactSpent;
    /// <summary>돌격 충격 쿨다운 남은 틱 (기병, 충격 뒤 10~15초). 0 = 충격 가능.</summary>
    [SnapExt] internal int ChargeCdLeft;
    /// <summary>이번 돌격에서 근접으로 친 서로 다른 보병 수 · 마지막 표적 Id (동시 근접 제한, N61).</summary>
    [SnapExt] internal int MeleeVictims;
    [SnapExt] internal int MeleeLastId;
    /// <summary>행진 사격 표적 Id (0 = 없음) — 포탑이 이쪽을 겨눈다.</summary>
    [SnapExt] internal int MarchTarget;
    /// <summary>행진 사격 중인 한 발 (HitChance 가 읽는 순간 값, 저장 안 함).</summary>
    internal bool MarchShot;

    public bool CanQuickStep => HasStamina && !Def.Mounted;
}

/// <summary>던진 수류탄: 신관이 끝나는 틱에 폭발.</summary>
public sealed class GrenadeBlast
{
    public int ThrowerId, Owner;
    public Vector2 Pos;
    public long ExplodeTick;
    /// <summary>연막 수류탄 (N65): 터지면 피해 대신 연막 (Breach.cs).</summary>
    public bool Smoke;
}

public sealed partial class World
{
    /// <summary>날아가는(신관이 타는) 수류탄. 던진 순서대로 처리. 저장 · 체크섬 포함.</summary>
    public readonly List<GrenadeBlast> Blasts = new();
    readonly List<Unit> spotters = new();
    readonly List<Unit> grenBuffer = new();

    // ---------------------------------------------------------------- 곡사포 관측

    /// <summary>faction(또는 동맹)의 관측 유닛(장교 · 정찰병)이 지금 시야 안에서 보고 있는 표적인가 (SpotterOnly 무기).</summary>
    public bool SeenBySpotter(int faction, Entity t)
    {
        if (CbMarks.Count > 0 && t is Unit cu && CbMarked(faction, cu)) return true;   // T10 ② 대포병 탐지 (FieldOps.cs)
        for (int i = 0; i < spotters.Count; i++)
        {
            var s = spotters[i];
            if (!s.Alive || (s.Owner != faction && !Allied(faction, s.Owner))) continue;
            if (t.EdgeDistance(s.Pos) <= SightOf(s) && !SmokeBlocks(s.Pos, t.Pos)) return true;   // 연막 너머는 관측 못 함 (N59)
        }
        return false;
    }

    /// <summary>[훅] 지형 은폐 보정 (숲 · 고지에 있는 은폐 유닛은 더 안 보임). 지형이 추가되면 여기서 계산한다 — 지금은 0.</summary>
    public float TerrainConcealment(Unit u) => 0f;

    /// <summary>날씨 은폐 보정 %p (안개 · 눈 등). weather.extendedEffects 가 꺼져 있으면 0. 상태 없음 — 종류 + balance 값으로 계산.</summary>
    public float WeatherConcealment(Unit u) => Defs.Weather.ConcealAdd(Weather.Kind);

    // ---------------------------------------------------------------- 근접 규칙 (전차 · 장갑차에는 근접 불가)

    /// <summary>u 가 t 에게 근접 공격을 할 수 있는가: 장갑 병과 불가 (착검 · 검 · 대도 · 기병 모두), 피해를 줄 수 없는 병과 불가.</summary>
    public static bool CanMelee(Unit u, Entity t)
    {
        var m = u.Melee;
        if (m == null) return false;
        return t is not Unit tu ? t is not TrainCar : (!tu.Def.IsArmored && m.CanDamage(tu.Def.Category));   // T30b: 열차 객차 = 장갑 → 근접 불가
    }

    // ---------------------------------------------------------------- 직사포 폭발 (보병포 · 야전포)

    /// <summary>직사 범위 무기: 명중이면 표적 위치, 빗나가면 splash 반경 근처 + 거리별 퍼짐 (N11 ScatterAt — 가까이 1칸 안팎, 최대 사거리 scatter) 에 폭발 (사선 검사는 호출 전에).
    /// scatter 가 없는 무기는 옛 규칙 (splash × 0.8~2.0).</summary>
    void FireGun(Entity shooter, Vector2 from, Entity target, WeaponDef w, float dist)
    {
        bool hit = Roll(shooter.Id, target.Id) < ShotChance(shooter, target, w, dist);
        Vector2 impact = target.Pos;
        if (!hit)
        {
            float ang = Roll(target.Id, shooter.Id) * DetMath.Tau;
            float u = Roll(target.Id, -3 - shooter.Id);
            float r = w.Scatter > 0 ? w.Splash * 0.8f + ScatterAt(w, dist) * MathF.Sqrt(u) : w.Splash * (0.8f + 1.2f * u);
            impact += new Vector2(DetMath.Cos(ang) * r, DetMath.Sin(ang) * r);
        }
        LaunchShell(shooter, from, impact, target.Id, w);   // 짧은 비행 뒤 그 지점에서 폭발 (ShellFlight.cs)
    }

    // ---------------------------------------------------------------- 수류탄

    /// <summary>수류탄 투척 (명령 · 자동): 보유 -1, 목표 둘레에 결정적 흩어짐, 신관 뒤 폭발.</summary>
    void ThrowGrenade(Unit u, Vector2 at)
    {
        var g = Defs.Grenade;
        u.Grenades--;
        float d = Vector2.Distance(u.Pos, at);
        float maxErr = g.Range > 0 ? g.Scatter * MathF.Min(1f, d / g.Range) : 0f;
        float err = maxErr * MathF.Sqrt(Roll(u.Id, 0x6A01));
        float ang = Roll(u.Id, 0x6A02) * DetMath.Tau;
        Vector2 land = at + new Vector2(DetMath.Cos(ang) * err, DetMath.Sin(ang) * err);
        Blasts.Add(new GrenadeBlast { ThrowerId = u.Id, Owner = u.Owner, Pos = land, ExplodeTick = Tick + Math.Max(1, (int)MathF.Round(g.Fuse / Config.SimDt)) });
        u.LastShotTick = Tick; u.RevealUntil = Tick + RevealTicks;
        MarkAlert(u.Owner, u.Pos);
        FaceToward(u, at, 1f);
    }

    void ExecThrowGrenade(int faction, List<Unit> list, Vector2 target)
    {
        float range = Defs.Grenade.Range;
        foreach (var u in list)
        {
            if (u.Grenades <= 0 || !u.Alive) continue;
            if (Vector2.Distance(u.Pos, target) > range + 0.01f) continue;   // 사거리 밖은 던지지 않는다
            ThrowGrenade(u, target);
        }
    }

    /// <summary>자동 투척 (15틱마다, Id 분산): 엄폐 중인 적 보병 autoCoverRange 칸 이내, 전차(장갑) autoTankRange 칸 이내 중 가장 가까운 표적.</summary>
    void AutoGrenade(Unit u)
    {
        var g = Defs.Grenade;
        if ((Tick + u.Id) % 15 != 0 || u.FireMode == FireMode.Hold) return;   // N73: 사격 금지 = 수류탄도 안 던짐
        float reach = MathF.Max(g.AutoCoverRange, g.AutoTankRange);
        UnitsInRadius(u.Pos, reach + Defs.MaxUnitRadius, grenBuffer);
        Unit? best = null; float bd = float.MaxValue;
        for (int i = 0; i < grenBuffer.Count; i++)
        {
            var e = grenBuffer[i];
            if (!e.Alive || !IsHostile(u.Owner, e.Owner)) continue;
            float d = e.EdgeDistance(u.Pos) - u.Radius;
            bool ok = e.Def.IsArmored ? d <= g.AutoTankRange : (CoverLevelOf(e) > 0 && d <= g.AutoCoverRange);
            if (!ok || d >= bd || !Fog.CanSee(u.Owner, e)) continue;
            (best, bd) = (e, d);
        }
        grenBuffer.Clear();
        if (best != null && Vector2.Distance(u.Pos, best.Pos) <= g.Range) ThrowGrenade(u, best.Pos);
    }

    /// <summary>비전투(마지막 사격 · 피격 뒤 supply.regenDelay 초) 중 regenSeconds 에 1개 회복.</summary>
    void RegenGrenade(Unit u, float dt)
    {
        var g = Defs.Grenade;
        bool needSmoke = u.SmokeGrenades < u.Def.SmokeGrenades;   // N65: 수류탄이 다 찬 뒤 연막 수류탄 (같은 시계)
        if ((u.Grenades >= u.Def.Grenades && !needSmoke) || g.RegenSeconds <= 0) return;
        if (Tick - LastCombatTick(u) < (long)(Defs.Supply.RegenDelay / Config.SimDt)) return;
        float mul = AmmoRegenMulAt(u.Owner, u.Pos);   // 수류탄 보충은 아군 도시 · 보급 마차 근처에서만 (탄약과 같은 배수)
        if (mul <= 1f) return;
        u.GrenadeRegen += dt * mul;
        if (u.GrenadeRegen >= g.RegenSeconds) { if (u.Grenades < u.Def.Grenades) u.Grenades++; else u.SmokeGrenades++; u.GrenadeRegen = 0; }
    }

    /// <summary>신관이 다 탄 수류탄 폭발: 반경 안 적대 유닛에 피해 (중심 damage → 가장자리 edgeDamage, 엄폐 무시), 전차는 묶음 고정 피해, 제압 +suppress (엄폐 무시).</summary>
    void UpdateBlasts()
    {
        UpdateSmokes();   // 다 탄 연막 지우기 (Breach.cs)
        UpdateFieldOps();   // 대포병 표시 만료 · 다 탄 조명탄 (FieldOps.cs, T10)
        if (Blasts.Count == 0) return;
        var g = Defs.Grenade;
        for (int bi = 0; bi < Blasts.Count;)
        {
            var b = Blasts[bi];
            if (b.ExplodeTick > Tick) { bi++; continue; }
            Blasts.RemoveAt(bi);
            if (BlastSmoke(b)) continue;   // 연막 수류탄 (N65, Breach.cs)
            var thrower = Find(b.ThrowerId);
            Impacts.Add((b.Pos, b.Pos, b.Owner, Tick, g.Radius, b.ThrowerId, "grenade"));
            UnitsInRadius(b.Pos, g.Radius + Defs.MaxUnitRadius, grenBuffer);
            for (int i = 0; i < grenBuffer.Count; i++)
            {
                var e = grenBuffer[i];
                if (!e.Alive || !IsHostile(b.Owner, e.Owner)) continue;
                float d = MathF.Max(0, Vector2.Distance(b.Pos, e.Pos) - e.Radius);
                if (d > g.Radius) continue;
                float gain = g.Suppress * e.Def.SuppressResist;   // 엄폐 무시
                if (gain > 0) { int old = e.Suppression; e.Suppression = (short)Math.Min(Defs.CombatRules.SuppressMax, e.Suppression + (int)MathF.Round(gain)); Stats.AddSupp(e.Owner, SupSrc.Grenade, e.Suppression - old); }
                e.LastFiredAtTick = Tick;
                float dmg = e.Def.IsArmored ? g.TankDamage : g.Damage + (g.EdgeDamage - g.Damage) * (g.Radius > 0 ? d / g.Radius : 0f);
                Damage(e, dmg * e.Def.ShellDamageMul, thrower, "grenade");
            }
            grenBuffer.Clear();
            NearMissRing(b.Owner, b.Pos, g.Radius, g.Suppress, ignoreCover: true);   // 폭발 반경 바깥 띠 근접 제압 (엄폐 무시, Suppression.cs)
        }
    }

    // ---------------------------------------------------------------- 근접 보조 무기 (N21)

    /// <summary>표적(명시 · 자동 · 행진)이 sidearmRange 안이면 보조 무기, sidearmRange + 0.5 밖이거나 표적이 없으면 주무기. 거리만으로 정해지는 결정적 선택 (저장할 새 상태 없음).</summary>
    void UpdateSidearm(Unit u)
    {
        var d = u.Def;
        Entity? t = u.Target ?? (u.MarchTarget != 0 ? Find(u.MarchTarget) : null);
        bool close;
        if (t == null || !t.Alive) close = false;
        else
        {
            float dist = t.EdgeDistance(u.Pos) - u.Radius;
            close = u.Weapon!.Key == d.Sidearm ? dist <= d.SidearmRange + 0.5f : dist <= d.SidearmRange;
        }
        string want = close ? d.Sidearm! : d.Weapon!;
        if (u.Weapon!.Key != want) u.Weapon = Defs.Current.Weapon(want);
    }

    // ---------------------------------------------------------------- 행진 사격

    /// <summary>행진 사격: 움직이는 보병 계열 · 기관총 전차가 이동 방향 앞 ±marchArcDeg 안의 가까운 적을 쏜다. 이번 틱 움직였을 때만 부른다 (참호 칸 · 참호 자리로 가는 중 제외).</summary>
    void MarchFire(Unit u)
    {
        var c = Defs.CombatRules;
        var w = u.Weapon;
        if (!u.Def.MarchFire || w == null || !FireMayHappen(u) || u.Charging || u.QuickStep || u.Target != null || u.CanDeploy
            || u.Suppression >= c.MarchMaxSuppress || (u.Order != UnitOrder.Move && u.Order != UnitOrder.AttackMove) || u.Group == null
            || u.HasTrenchSpot || Map.IsTrench((int)MathF.Floor(u.Pos.X), (int)MathF.Floor(u.Pos.Y))   // 참호 안 · 참호로 들어가는 중 · 참호 칸 위 보병은 행진 사격하지 않는다
            || !MarchCommanded(u))   // 보병은 장교 (중대) · 부사관 (분대) 이 명령한 동안만 — 지휘자 없으면 개별 행진 사격 없음 (MarchCommand.cs)
        { u.MarchTarget = 0; return; }
        if (u.Def.Category == "infantry" && MarchCharge(u)) return;   // 행진 사격 중 적이 marchChargeRange 안 → 자동 돌격
        Entity? t = u.MarchTarget != 0 ? Find(u.MarchTarget) : null;
        if (t != null && !MarchTargetOk(u, t, w, c, out _)) t = null;
        if (t == null)
        {
            u.MarchTarget = 0;
            if ((Tick + u.Id) % 10 != 0) return;
            float rad = MarchRadius(u, w, c);
            t = FindTarget(u.Owner, u.Pos, rad, w.MinRange, w.Los ? u : null, u.Id, TargetJitter(u), w);
            if (t == null || !MarchTargetOk(u, t, w, c, out _)) return;
            u.MarchTarget = t.Id;
        }
        if (!FireAllowed(u, t)) { u.MarchTarget = 0; return; }   // N73
        if (u.Cooldown > 0 || !u.HasAmmo || !AimedAt(u, t, w)) return;
        MarchTargetOk(u, t, w, c, out float dist);
        if (!ConsumeAmmo(u)) return;
        bool smg = w.Submachine;
        u.Cooldown = NextCooldown(u, w) * FireIntervalMul(u) * (u.Def.MarchIntervalMul > 0 ? u.Def.MarchIntervalMul : smg ? c.MarchSmgIntervalMul : c.MarchIntervalMul);
        BurstAfterShot(u, w);
        u.MarchShot = true;
        Fire(u, u.Pos, t, w, dist);
        u.MarchShot = false;
    }

    float MarchRadius(Unit u, WeaponDef w, CombatRulesDef c)
    {
        int near = WeatherSight(u.Def.SightNear > 0 ? u.Def.SightNear : u.Def.Sight, WeatherSightMul);   // 날씨 · 시간대
        float r = MathF.Min(w.Range, near * c.MarchSightFrac);
        return PassiveFire(u) ? MathF.Min(r, PassiveFireRange(u)) : r;   // N35: 장교 · 정찰병 행진 사격 = 자위 반경만
    }

    bool MarchTargetOk(Unit u, Entity t, WeaponDef w, CombatRulesDef c, out float dist)
    {
        dist = 0;
        if (!t.Alive || t.Indestructible || !IsHostile(u.Owner, t.Owner) || !Fog.CanSee(u.Owner, t)) return false;
        if (!w.CanDamage(CategoryOf(t))) return false;   // 유닛 · 열차 객차 (건물 = 항상 가능)
        dist = MathF.Max(0, t.EdgeDistance(u.Pos) - u.Radius);
        if (dist > MarchRadius(u, w, c) || dist < w.MinRange) return false;
        Vector2 dir = u.Velocity, to = t.Pos - u.Pos;
        float dl = dir.Length(), tl = to.Length();
        if (dl < 1e-4f) return false;
        if (tl > 1e-4f && Vector2.Dot(dir, to) / (dl * tl) < DetMath.Cos(c.MarchArcDeg * (DetMath.Pi / 180f))) return false;
        return HasFiringLine(u, t, w);
    }

    // ---------------------------------------------------------------- 명령 (돌격만 토글 · 수류탄)

    public void ThrowGrenadeAt(int faction, IEnumerable<Unit> units, Vector2 target) =>
        IssueUnits(faction, units.Where(u => u.Grenades > 0), ids => new ThrowGrenadeCommand(faction, ids, target));

    public void SetMount(int faction, IEnumerable<Unit> units, bool mount) =>
        IssueUnits(faction, units.Where(u => u.Def.Mounted && !u.IsOfficer), ids => new MountCommand(faction, ids, mount));   // N61

    public void SetChargeOnly(int faction, IEnumerable<Unit> units, bool on) =>
        IssueUnits(faction, units.Where(u => u.CanCharge && u.Def.Mounted), ids => new ChargeOnlyCommand(faction, ids, on));
}
