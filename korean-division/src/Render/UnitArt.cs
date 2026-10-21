using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 유닛 종류별 그림 정보 캐시: 테마 스프라이트 · 동작 열 번호 배열을 한 번만 찾아 둔다
/// (유닛마다 문자열 연결 · 딕셔너리 조회를 하지 않도록). 테마가 바뀌면 다시 만든다.
/// </summary>
internal sealed class UnitArt
{
    static readonly int[] Zero = { 0 };
    static readonly Dictionary<(UnitDef, string?), UnitArt> cache = new();
    static Theme? cachedFor;

    public ThemeSprite? Body, Turret;
    public bool Dir;            // 방향 시트 유닛
    public bool Tank;           // 차체 + 포탑 (장착점이 있는 시트)
    public bool HasDeath;       // 보병식 사망 모션 (death = 맞음 · 쓰러짐 · 쓰러진 채)
    public bool HasWreck;       // 잔해 칸 (dead)
    public int[] Idle = Zero, Walk = Zero, Shoot = Zero, Death = Zero;
    public int[]? IdleAlt;
    /// <summary>사격 변형 [[조준, 발사], ...] (새 16칸 시트, 없으면 null). 사격마다 해시로 고른다.</summary>
    public int[][]? ShootVariants;
    public int[]? Deploy;       // 견인포: 전개 · 철수 중 칸 (없으면 null → 자주포처럼 그림)
    public bool Vehicle;        // 포탑 없는 차량 (포병 자주포): 사망 = 잔해 칸
    public int HullWreck, TurretWreck, TurretIdle, TurretFire;

    /// <summary>사격마다 결정적으로 고르는 변형 번호 (유닛 id · 마지막 사격 틱의 해시). 화면 전용.</summary>
    public int VariantFor(Unit u)
    {
        if (ShootVariants == null || ShootVariants.Length <= 1) return 0;
        uint h = (uint)u.Id * 2654435761u ^ (uint)(u.LastShotTick * 40503 + 977);
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return (int)(h % (uint)ShootVariants.Length);
    }

    /// <summary>
    /// variant = 진영 그림 변형 (japanese · fengtian · russian-white · russian-red, <see cref="FactionArt"/>). 테마에 "unit.&lt;키&gt;@&lt;변형&gt;" 이 있으면 그 그림,
    /// 없으면 기본(한국) 그림.
    /// </summary>
    public static UnitArt For(UnitDef d, string? variant = null)
    {
        if (!ReferenceEquals(cachedFor, Theme.Current)) { cache.Clear(); cachedFor = Theme.Current; }
        if (cache.TryGetValue((d, variant), out var a)) return a;
        a = new UnitArt();
        string vk = variant != null ? "@" + variant : "";
        string key = d.Key;
        var body = variant != null ? Theme.Current?.Get("unit." + key + vk) : null;
        if (body == null) { vk = ""; body = Theme.Current?.Get("unit." + key); }
        if (body == null && d.ArtAs != null)   // 전용 그림 없음 → 대용 유닛 그림 (마적 = 봉천 그림, 공용 = 보병 그림)
        {
            string ak = "unit." + d.ArtAs;
            string? av = d.ArtVariant ?? variant;
            body = av != null ? Theme.Current?.Get(ak + "@" + av) : null;
            if (body != null) { vk = "@" + av; key = d.ArtAs; }
            else { body = Theme.Current?.Get(ak); if (body != null) key = d.ArtAs; }
        }
        if (body?.Lod != null) a.InitLod(body, Theme.Current?.Get("unit." + key + vk + ".turret"));
        else if (body is { Directional: true })
        {
            a.Dir = true;
            a.Body = body;
            a.Idle = body.Anim("idle");
            a.Walk = body.Anims.ContainsKey("walk") ? body.Anim("walk") : body.Anims.ContainsKey("travel") ? body.Anim("travel") : body.Anim("walk");   // 견인포: travel
            a.Deploy = body.Anims.TryGetValue("deploy", out var dep) && dep.Length > 0 ? dep : null;
            a.ShootVariants = body.ShootVariants;
            a.Shoot = body.Anims.ContainsKey("shoot") ? body.Anim("shoot") : a.ShootVariants != null ? a.ShootVariants[0] : body.Anim("shoot");
            a.IdleAlt = body.Anims.TryGetValue("idle_alt", out var alt) && alt.Length > 0 ? alt : null;
            a.HasDeath = body.Anims.TryGetValue("death", out var dth) && dth.Length >= 3;
            if (a.HasDeath) a.Death = dth!;
            a.HasWreck = body.Anims.TryGetValue("dead", out var dd) && dd.Length > 0;
            a.HullWreck = a.HasWreck ? dd![0] : 0;
            a.Vehicle = a.HasWreck && !a.HasDeath && body.Mounts == null;
            var tur = Theme.Current?.Get("unit." + key + ".turret");
            if (tur is { Directional: true } && body.Mounts != null)
            {
                a.Tank = true;
                a.Turret = tur;
                a.TurretIdle = tur.Anim("idle")[0];
                a.TurretFire = tur.Anims.TryGetValue("shoot", out var sh) && sh.Length > 0 ? sh[0] : a.TurretIdle;
                a.TurretWreck = tur.Anims.TryGetValue("dead", out var td) && td.Length > 0 ? td[0] : a.TurretIdle;
            }
        }
        else a.Body = body;
        cache[(d, variant)] = a;
        return a;
    }

    // ------------------------------------------------------------ 새 형식 (LOD 시트 · 이름 붙은 애니메이션)

    public LodSprite? Lod, LodTurret;
    LodAnim? aCrawl, aTrenchFire, aGrenade, aIdle, aWalkFire, aSalute, aPresent, aStart, aHitB, aSupply, aGallop, aDeploy, aPack, aCrewFire, aBinocKneel, aSurrender;
    LodAnim[] idleExtras = Array.Empty<LodAnim>();
    int fPackedPose = -1, fDeployedPose = -1;
    LodAnim? aIdleAlt, aIdleLean, aWalk, aWalkShoulder, aWalkQuick, aCharge, aMelee, aMeleeB, aSwordWave, aWaveFlag, aHullMove, aTurretFire, aBinoc;
    LodAnim[] fire = Array.Empty<LodAnim>();
    LodAnim? aFixBayonet;   // N57 착검 · 칼 뽑기 (보병 시트에만 — 없으면 조준 자세)
    LodAnim? aPistolFire;   // 권총 사격 (보조 무기, 카빈은 멜빵) — 현재 무기가 sidearm 일 때 fire 대신

    /// <summary>사망 애니메이션들 (death · death_b · death_c 중 있는 것). 유닛 id 로 하나를 고른다.</summary>
    public LodAnim[] Deaths = Array.Empty<LodAnim>();
    int fIdle, fHit = -1, fAimProne = -1, fFireProne = -1, fHullIdle, fHullDead = -1, fTurretIdle, fTurretDead = -1;
    /// <summary>제압 비율이 이 값 이상이면 (서 있을 때) 엎드려 쏘기 자세 (fire_prone 이 있을 때만).</summary>
    public const float ProneAt = 0.5f;   // (옛 기준 — 지금 엎드림은 Unit.Pinned, 제압 ≥ combat.pinAt 이력)
    /// <summary>마지막 사격 뒤 조준 자세를 유지하는 시간 (틱, 60 = 1초).</summary>
    public const int AimHoldTicks = 150;
    /// <summary>이동 시작마다 어깨총 걷기를 고를 확률 (%, ROADMAP §3 20차).</summary>
    public const int ShoulderPct = 33;

    static long Ms(long ticks) => ticks * 1000 / 60;
    static uint Hash(int a, long b)
    {
        uint h = (uint)a * 2654435761u ^ (uint)(b * 40503 + 977);
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return h;
    }

    // 새 애니메이션 이름을 여기서 쓰면 LodSprite.UsedAnimNames 에도 추가 (아니면 그 칸이 아틀라스에서 빠짐)
    void InitLod(ThemeSprite body, ThemeSprite? turret)
    {
        var s = body.Lod!;
        Lod = s; Body = body; Dir = true;
        fIdle = s.Anim("idle")?.Frames[0] ?? 0;
        Idle = new[] { fIdle };
        aIdle = s.Anim("idle");
        aIdleAlt = s.Anim("idle_alt"); aIdleLean = s.Anim("idle_lean");
        // 대기 변형: 가끔 하나를 골라 재생 (보병 idle_alt/lean + 새 모션 rest/look/adjust/stretch, 마차 idle_3)
        idleExtras = new[] { "idle_alt", "idle_lean", "idle_rest", "idle_look", "idle_adjust", "idle_stretch", "idle_3" }.Select(s.Anim).Where(x => x != null).Select(x => x!).ToArray();
        aWalkFire = s.Anim("walk_fire") is { Frames.Length: >= 2 } wf ? wf : null;
        aSalute = s.Anim("salute"); aPresent = s.Anim("present_arms"); aSurrender = s.Anim("surrender");   // N79b 만세 (없으면 idle)
        aCrawl = s.Anim("crawl") is { Frames.Length: >= 2 } cr ? cr : null; aTrenchFire = s.Anim("trench_fire") is { Frames.Length: >= 2 } tf2 ? tf2 : null; aGrenade = s.Anim("grenade_throw");   // drag_wounded: 시뮬에 아직 없음 (UsedAnimNames 에만)
        aStart = s.Anim("start"); aHitB = s.Anim("hit_b"); aSupply = s.Anim("supply"); aGallop = s.Anim("gallop");
        // 승무원 · 포: 방열(deploy | unpack) · 철수(pack) · 사격(fire | fire_prone | fire_kneel)
        aDeploy = s.Anim("deploy") ?? s.Anim("unpack"); aPack = s.Anim("pack");
        aCrewFire = aDeploy != null && aPack != null ? (s.Anim("fire") ?? s.Anim("fire_prone") ?? s.Anim("fire_kneel")) : null;
        if (aCrewFire != null && aCrewFire.Frames.Length < 2) aCrewFire = null;
        if (aCrewFire == null) { aDeploy = null; aPack = null; }
        aWalk = s.Anim("walk") ?? s.Anim("move"); aWalkShoulder = s.Anim("walk_shoulder"); aWalkQuick = s.Anim("walk_quick");
        aCharge = s.Anim("charge"); aMelee = s.Anim("melee"); aMeleeB = s.Anim("melee_b"); aSwordWave = s.Anim("sword_wave"); aWaveFlag = s.Anim("wave_flag"); aFixBayonet = s.Anim("fix_bayonet"); aBinoc = s.Anim("binoculars"); aBinocKneel = s.Anim("binoculars_kneel");
        if (aCrewFire != null)
        {
            fDeployedPose = aDeploy!.Frames[^1];
            // 포: 방열 끝 칸 = 서 있기(idle) 이면 이동 자세는 방열 첫 칸 (포를 매단 모습), 승무원: 이동 자세 = 서 있기
            fPackedPose = fDeployedPose == fIdle ? aDeploy.Frames[0] : fIdle;
        }
        fire = new[] { "fire_a", "fire_b", "fire_c", aCrewFire == null ? "fire" : "" }.Select(n => n == "" ? null : s.Anim(n)).Where(x => x != null && x.Frames.Length >= 2).Select(x => x!).ToArray();
        aPistolFire = s.Anim("pistol_fire") is { Frames.Length: >= 2 } pf ? pf : null;
        Deaths = new[] { "death", "death_b", "death_c", "destroyed" }.Select(s.Anim).Where(x => x != null).Select(x => x!).ToArray();
        HasDeath = Deaths.Length > 0;
        fHit = s.Frame("hit");
        if (aCrewFire == null && s.Anim("fire_prone") != null) { fAimProne = s.Frame("aim_prone"); fFireProne = s.Frame("fire_prone"); }
        // 차량: 차체 idle / move / dead, 포탑 idle / fire / dead
        aHullMove = s.Anim("move");
        fHullIdle = fIdle;
        fHullDead = s.Frame("dead");
        HasWreck = fHullDead >= 0 && !HasDeath;
        HullWreck = Math.Max(0, fHullDead);
        if (turret?.Lod is { } t && s.Levels[0].Mount != null)
        {
            Tank = true; Turret = turret; LodTurret = t;
            fTurretIdle = t.Anim("idle")?.Frames[0] ?? 0;
            aTurretFire = t.Anim("fire");
            fTurretDead = t.Frame("dead");
            TurretWreck = fTurretDead >= 0 ? fTurretDead : fTurretIdle;
        }
        Vehicle = HasWreck && !Tank;
    }

    /// <summary>[감사 · 읽기 전용] 이 그림의 <see cref="LodFrame"/> · 사망 · 잔해가 낼 수 있는 몸체 시트(<see cref="Lod"/>) 프레임 번호 전부, turret = 포탑 시트(<see cref="LodTurret"/>) 쪽.</summary>
    /// <summary>[감사] 사격 모션 칸 수 (fire_a/b/c · fire · 승무원 사격 · 엎드려 쏘기).</summary>
    public int CanFireFrames => fire.Length + (aCrewFire != null ? 1 : 0) + (fFireProne >= 0 ? 1 : 0);

    public (HashSet<int> Body, HashSet<int> Turret) ReachableFrames()
    {
        var b = new HashSet<int> { fIdle };
        void Add(LodAnim? a) { if (a != null) foreach (int f in a.Frames) b.Add(f); }
        foreach (var a in new[] { aIdle, aIdleAlt, aIdleLean, aWalk, aWalkShoulder, aWalkQuick, aWalkFire, aCharge, aMelee, aMeleeB, aSwordWave, aWaveFlag, aFixBayonet, aCrawl, aTrenchFire, aGrenade, aSalute, aPresent, aSurrender, aStart, aHitB, aSupply, aGallop, aDeploy, aPack, aCrewFire, aBinoc, aBinocKneel, aHullMove }) Add(a);
        foreach (var a in idleExtras) Add(a);
        foreach (var a in fire) Add(a);
        Add(aPistolFire);
        foreach (var a in Deaths) Add(a);
        foreach (int f in new[] { fHit, fAimProne, fFireProne, fHullDead, fPackedPose, fDeployedPose }) if (f >= 0) b.Add(f);
        var t = new HashSet<int>();
        if (LodTurret != null)
        {
            t.Add(fTurretIdle);
            if (aTurretFire != null) foreach (int f in aTurretFire.Frames) t.Add(f);
            if (fTurretDead >= 0) t.Add(fTurretDead);
        }
        return (b, t);
    }

    /// <summary>
    /// 보병 동작 → 프레임 (LOD 시트). 시뮬레이션 값만 읽는다 (표시 전용, 결정적 해시).
    ///   장교 빈자리 점검 칼 흔들기 (SwordWaveTick) = sword_wave 한 번.
    ///   돌격: 근접 공격 직후 melee · melee_b (공격마다 해시) → 달리는 중 charge, 장교는 돌격 명령 직후 sword_wave.
    ///   사격 직후 = 사격 칸 (fire_a/b/c 를 사격마다 해시로), 고정 (Unit.Pinned) 이고 서 있으면 엎드려 쏘기.
    ///   맞은 직후 0.15초 = hit, 속보 = walk_quick (없으면 walk), 걷기 = walk (이동 명령마다 33% 는 walk_shoulder, 없으면 walk), 표적 = 조준,
    ///   대기 = idle (가끔 idle_alt · idle_lean).
    /// </summary>
    public int LodFrame(Unit u, long tick, bool victory = false, bool inTrench = false, bool marching = false)
    {
        long sinceShot = tick - u.LastShotTick;
        if (u.Surrendered) return aSurrender != null ? aSurrender.FrameAt(Ms(tick + u.Id * 7), true) : fIdle;   // N79b 항복: 만세 포즈 (프레임 없으면 idle)
        if (victory && !u.Walking && (aPresent != null || aSalute != null))   // 승리 의식: 장교 = 경례, 병사 = 받들어총
        {
            var c = u.Def.IsOfficer ? aSalute ?? aPresent : aPresent ?? aSalute;
            if (c != null) return c.Frames[0];
        }
        if (aCrewFire != null && u.CanDeploy) return CrewFrame(u, tick, sinceShot);
        if (u.Def.Mounted && u.ImpactSpent && u.ReactDelay > 12 && fHit >= 0) return fHit;   // 돌격 충격 직후 마비
        if (aHitB != null && tick - u.LastDamagedTick < 30) return aHitB.FrameAt(Ms(tick - u.LastDamagedTick), false);   // 마차 피격
        if (aStart != null && u.Walking && Ms(tick - u.LastOrderTick) < aStart.TotalMs) return aStart.FrameAt(Ms(tick - u.LastOrderTick), false);   // 마차 출발
        // 장교 칼 흔들기: 빈자리 점검 중 교전이면 (World.StepGapChecks, 33%) — 한 번 재생
        if (aSwordWave != null && u.SwordWaveTick > 0 && tick >= u.SwordWaveTick && Ms(tick - u.SwordWaveTick) < aSwordWave.TotalMs && !u.Charging)
            return aSwordWave.FrameAt(Ms(tick - u.SwordWaveTick), false);
        if (u.Charging && u.FixLeft > 0 && (u.Def.Mounted || u.Def.Melee != "bayonet") && aCharge != null)   // 칼 뽑기 (대도 · 군도 · 단검 · 기병) 중: 돌격 자세 (progbar)
            return u.Walking ? aCharge.FrameAt(Ms(tick + u.Id * 13), true) : aCharge.Frames[0];
        if (u.Charging && u.FixLeft > 0)   // N57 착검 중: fix_bayonet 이 착검이 끝나는 순간 끝나게 재생, 없으면 조준 자세
        {
            if (aFixBayonet != null) return aFixBayonet.FrameAt(Math.Max(0, aFixBayonet.TotalMs - Ms(u.FixLeft)), false);
            if (fire.Length > 0) return fire[0].Frames[0];
        }
        if (u.Charging)
        {
            var m = aMeleeB != null && aMelee != null ? (Hash(u.Id, u.LastShotTick) % 3 == 0 ? aMeleeB : aMelee) : aMelee ?? aMeleeB;
            if (m != null && Ms(sinceShot) < m.TotalMs) return m.FrameAt(Ms(sinceShot), false);
            if (aSwordWave != null && Ms(tick - u.LastOrderTick) < aSwordWave.TotalMs * 2) return aSwordWave.FrameAt(Ms(tick - u.LastOrderTick), true);
            if (aCharge != null) return u.Walking ? aCharge.FrameAt(Ms(tick + u.Id * 13), true) : aCharge.Frames[0];
        }
        if (aGrenade != null && GrenadeThrow(u, tick) is long gt && Ms(tick - gt) < aGrenade.TotalMs && !u.Walking) return aGrenade.FrameAt(Ms(tick - gt), false);   // 수류탄 투척
        if (aTrenchFire != null && inTrench && !u.Walking && (u.Target != null || u.Aiming || Ms(sinceShot) < 500))   // 참호 사격: 쏜 직후 = 둘째 칸
            return Ms(sinceShot) < 160 ? aTrenchFire.Frames[^1] : aTrenchFire.Frames[0];
        if (aCrawl != null && u.Walking && !u.Charging && u.Prone && !u.Broken) return aCrawl.FrameAt(Ms(tick + u.Id * 7), true);   // 엎드린 채 이동 (고정 개별 이동 · 후퇴, pronemove 상한 뒤 대열 따라감) = 기어감
        bool prone = fFireProne >= 0 && !u.Walking && (u.Pinned || u.Prone);   // 고정 (엎드림, 제압 재설계 N25 — Suppression.cs) = 엎드려 쏘기
        bool usePistol = aPistolFire != null && u.Def.Sidearm != null && u.Weapon?.Key == u.Def.Sidearm;   // 렌더 전용: 근접 보조 무기 사용 중
        int aim = fIdle, shot = fIdle, shotMs = 160;
        if (prone) { aim = fAimProne; shot = fFireProne; }
        else if (fire.Length > 0 || usePistol)
        {
            var f = usePistol ? aPistolFire! : fire[fire.Length == 1 ? 0 : (int)(Hash(u.Id, u.LastShotTick) % (uint)fire.Length)];
            aim = f.Frames[0]; shot = f.Frames[^1];
            shotMs = f.EndMs[^1] - f.EndMs[^2];
        }
        if (marching && u.Walking && aWalkFire != null && u.Def.MarchFire && !u.Charging && (u.Target != null || u.MarchTarget != 0 || Ms(sinceShot) < 500))   // 행진 사격 (실제로 지휘받는 행진 사격 중인 병사만 — 2026-10-19)
        {
            if (aWalkFire.Frames.Length >= 4) return aWalkFire.FrameAt(Ms(tick + u.Id * 7), true);   // 4프레임 (다리 위상 = walk_1..4, 섬광 2/4) 순환
            return Ms(sinceShot) < 140 ? aWalkFire.Frames[^1] : aWalkFire.Frames[0];   // 옛 2프레임 시트
        }
        if (Ms(sinceShot) < shotMs) return shot;
        if (fHit >= 0 && tick - u.LastDamagedTick < 9 && !u.Walking) return fHit;
        if (aWaveFlag != null && u.Def.Bearer && !u.Walking) return aWaveFlag.FrameAt(Ms(tick + u.Id * 7), true);   // 기수 (사용자 2026-10-21): 사격 대신 깃발 흔들기 (서 있을 때)
        if (aSupply != null && Supplying(u, tick)) return aSupply.FrameAt(Ms(tick + u.Id * 11), true);
        if ((aBinoc != null || aBinocKneel != null) && u.ScoutObserving && !u.Aiming && u.Target == null)
        {
            var bn = aBinocKneel != null && (aBinoc == null || u.SuppressionRatio >= 0.15f) ? aBinocKneel : aBinoc!;   // 제압 기미가 있으면 무릎쏴 쌍안경
            return bn.FrameAt(Ms(tick + u.Id * 7), true);
        }   // 정찰병 관측 중 (쌍안경, Scouts.cs)
        if (u.Walking)
        {
            if (aGallop != null && u.Velocity.Length() > u.Def.Speed * 1.25f) return aGallop.FrameAt(Ms(tick + u.Id * 7), true);
            if (aWalkQuick != null && u.QuickStep && !u.Def.Mounted) return aWalkQuick.FrameAt(Ms(tick + u.Id * 7), true);   // 속보 (표시 전용): 빠른 박자 · 큰 보폭 · 앞으로 숙임 · 총 높이 듦. 시트에 없으면 보통 걷기
            var w = aWalkShoulder != null && Hash(u.Id * 31 + 7, u.LastOrderTick) % 100 < ShoulderPct ? aWalkShoulder : aWalk;
            if (w != null) return w.FrameAt(Ms(tick + u.Id * 7), true);
        }
        if (prone) return aim;
        if (u.Aiming || u.Target != null) return aim;
        // 사격 사이 조준 유지: 표적이 사거리 안팎을 오가거나 (장교 권총 6칸) 쿨다운이 길어도 (권총 1.2~1.6초) 쏜 뒤 2.5초는 총을 든 채 — 넣었다 뺐다 깜빡임 방지
        if (u.LastShotTick > 0 && sinceShot is >= 0 and < AimHoldTicks && (fire.Length > 0 || usePistol)) return aim;
        if (idleExtras.Length > 0)
        {
            long window = (tick + u.Id * 37) / 240, off = (tick + u.Id * 37) % 240;
            uint h = (uint)(window * 2654435761u) ^ (uint)(u.Id * 40503);
            if (h % 100 < 14)
            {
                var ex = idleExtras[(int)((h >> 8) % (uint)idleExtras.Length)];
                if (off < Math.Max(80, ex.TotalMs * 60 / 1000)) return ex.FrameAt(Ms(off), false);
            }
        }
        if (aIdle != null && aIdle.Frames.Length > 1) return aIdle.FrameAt(Ms(tick + u.Id * 53), true);   // 기병 · 마차: 숨쉬기 2칸
        return fIdle;
    }

    /// <summary>행진 사격 중 방금 쐈으면 true — walk_fire 시트에는 총구 화염이 없으므로 (2026-10-19 재렌더) 렌더러가 총구에 화염을 덧그린다. 사격 칸에 화염이 구워진 다른 동작에는 쓰지 않는다 (중복 방지).</summary>
    public bool MarchFlash(Unit u, long tick, bool marching) =>
        marching && aWalkFire != null && u.Walking && u.Def.MarchFire && !u.Charging && u.LastShotTick > 0 && tick - u.LastShotTick is >= 0 and < 4
        && (u.Target != null || Ms(tick - u.LastShotTick) < 500);

    /// <summary>승무원 · 포: 방열(진행도로 deploy/unpack 칸) → 사격(fire 칸, 쏜 시각부터) → 조준/방열 자세, 철수(pack), 이동/이동 자세.</summary>
    int CrewFrame(Unit u, long tick, long sinceShot)
    {
        switch (u.Deploy)
        {
            case DeployPhase.Deploying: return aDeploy!.FrameAt((long)(u.DeployProgress * aDeploy.TotalMs), false);
            case DeployPhase.Packing: return aPack!.FrameAt((long)((1f - u.DeployProgress) * aPack.TotalMs), false);
            case DeployPhase.Deployed:
            {
                long t = aCrewFire!.EndMs[0] + Ms(sinceShot);
                if (t < aCrewFire.TotalMs) return aCrewFire.FrameAt(t, false);
                return u.Aiming || u.Target != null ? aCrewFire.Frames[0] : fDeployedPose;
            }
        }
        if (u.Walking && aWalk != null) return aWalk.FrameAt(Ms(tick + u.Id * 7), true);
        if (fHit >= 0 && tick - u.LastDamagedTick < 9) return fHit;
        return fPackedPose;
    }

    static readonly Dictionary<int, (int Count, long Throw)> grenadeSeen = new();
    /// <summary>수류탄을 마지막으로 던진 틱 (남은 수류탄이 줄어든 순간, 표시 전용). 없으면 null.</summary>
    static long? GrenadeThrow(Unit u, long tick)
    {
        if (!grenadeSeen.TryGetValue(u.Id, out var seen)) seen = (u.Grenades, -100000);
        if (u.Grenades < seen.Count) seen.Throw = tick;
        seen.Count = u.Grenades;
        grenadeSeen[u.Id] = seen;
        return seen.Throw < 0 ? null : seen.Throw;
    }

    static readonly Dictionary<int, (float Cargo, long Drop)> cargoSeen = new();
    /// <summary>보급 마차가 탄약을 나눠 주는 중인가: 적재량이 최근 1.5초 안에 줄었으면 (표시 전용).</summary>
    static bool Supplying(Unit u, long tick)
    {
        if (!cargoSeen.TryGetValue(u.Id, out var seen)) seen = (u.Cargo, -1000);
        if (u.Cargo < seen.Cargo - 0.0001f) seen.Drop = tick;
        seen.Cargo = u.Cargo;
        cargoSeen[u.Id] = seen;
        return tick - seen.Drop < 90 && !u.Walking;
    }

    /// <summary>시체: 유닛 id 로 고른 사망 애니메이션의 age(틱) 시점 프레임 (마지막 프레임에서 멈춤).</summary>
    public int LodDeathFrame(int unitId, long age)
    {
        if (Deaths.Length == 0) return fIdle;
        var d = Deaths[(int)(Hash(unitId * 17 + 3, 0) % (uint)Deaths.Length)];
        return d.FrameAt(Ms(age), false);
    }

    /// <summary>LOD 전차 · 차량 그리기 (차체 이동 · 포탑 사격 애니메이션 포함). wreck = 잔해 칸.</summary>
    public void DrawLodVehicle(Unit? u, long tick, Vector2 feet, float zoom, float hullFacing, float turretFacing, Color team, bool wreck, int alpha = 255)
    {
        var hull = Lod!;
        int hf = wreck ? HullWreck : u != null && u.Walking && aHullMove != null ? aHullMove.FrameAt(Ms(tick + u.Id * 5), true) : fHullIdle;
        int tf = fTurretIdle;
        if (wreck) tf = TurretWreck;
        int fm = -1;
        if (wreck) { }
        else if (u != null && aTurretFire != null && Ms(tick - u.LastShotTick) < aTurretFire.TotalMs)
        {
            tf = aTurretFire.FrameAt(Ms(tick - u.LastShotTick), false);
            fm = (int)((u.LastShotTick / 8) & 1);   // 포탑 여럿 (표시 전용): 사격마다 번갈아 한 포탑만 불꽃 — 시뮬 영향 없음
        }
        LodDraw.Tank(hull, LodTurret, feet, hull.Scale, hull.Scale * zoom, hullFacing, turretFacing, hf, tf, team, alpha, wreck ? -1 : fm, wreck ? tf : fTurretIdle);
    }

    /// <summary>
    /// 전차 그리기: 차체(행 = 차체 방향) + 포탑(행 = 포탑 방향), 포탑 pivot 을 차체 장착점에 맞춘다.
    /// feet = 차체 접지점 (월드/화면 좌표).
    /// </summary>
    public void DrawTank(Vector2 feet, float screenScale, float hullFacing, float turretFacing, int hullCol, int turretCol, Color team, int alpha = 255)
    {
        var hull = Body!;
        float scale = hull.Scale;
        bool smooth = screenScale < 0.75f;
        int hrow = Theme.RowOf(hull, hullFacing, out bool hflip);
        Theme.DrawCell(hull, hrow, hflip, hullCol, feet, scale, smooth, team, alpha);
        var tur = Turret;
        if (tur == null || hull.Mounts == null) return;
        var g = hull.AnchorAt(hrow, hullCol);
        var m = hull.Mounts[hrow];
        float gx = hflip ? hull.FrameW - g.X : g.X, mx = hflip ? hull.FrameW - m.X : m.X;
        var mount = feet + new Vector2(mx - gx, m.Y - g.Y) * scale;
        int trow = Theme.RowOf(tur, turretFacing, out bool tflip);
        Theme.DrawCell(tur, trow, tflip, turretCol, mount, tur.Scale, smooth, team, alpha);
    }
}

/// <summary>그림자 · 발밑 세력 표시용 작은 텍스처 한 장 (타원 두 개). 같은 텍스처라 수천 개를 한 배치로 그린다.</summary>
internal static class Blob
{
    static Texture2D tex;
    static bool loaded;
    public static Rectangle Shadow = new(1, 1, 30, 14), Ring = new(33, 1, 30, 14), Thin = new(65, 1, 30, 14);

    public static Texture2D Tex
    {
        get
        {
            if (loaded && tex.Id != 0) return tex;
            var img = Raylib.GenImageColor(96, 16, new Color((byte)255, (byte)255, (byte)255, (byte)0));
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 96; x++)
            {
                float nx = ((x & 31) + 0.5f - 16f) / 16f, ny = (y + 0.5f - 8f) / 8f;
                float d = MathF.Sqrt(nx * nx + ny * ny);
                float soft = x < 32 ? 0.45f : 0.12f;
                int a = (int)(Math.Clamp((1f - d) / soft, 0f, 1f) * 255);
                if (x >= 64) a = (int)(Math.Clamp((1f - d) / 0.1f, 0f, 1f) * Math.Clamp((d - 0.6f) / 0.1f, 0f, 1f) * 255);   // 얇은 고리 (축소 팀색 고리)
                Raylib.ImageDrawPixel(ref img, x, y, new Color((byte)255, (byte)255, (byte)255, (byte)a));
            }
            tex = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
            loaded = true;
            return tex;
        }
    }

    /// <summary>중심 c, 반지름 rx × ry 의 타원 하나.</summary>
    public static void Draw(Rectangle region, Vector2 c, float rx, float ry, Color tint) =>
        Raylib.DrawTexturePro(Tex, region, new Rectangle(c.X - rx, c.Y - ry, rx * 2, ry * 2), Vector2.Zero, 0, tint);
}

/// <summary>
/// 세력 → 그림 변형. 지도 파일 factions[].en 으로 고른다: japanese · fengtian → 같은 이름, russian(=백군) → russian-white, soviet → russian-red.
/// 그 밖의 값(korean · Blue · Red ...)은 변형 없음 = 기본(한국) 그림. 확인용: 환경변수 KD_ART_FACTIONS="japanese,fengtian,..." (세력 1, 2, ... 순서).
/// </summary>
internal static class FactionArt
{
    static World? forWorld;
    static string?[] byOwner = Array.Empty<string?>();
    static readonly string[] Override = (Environment.GetEnvironmentVariable("KD_ART_FACTIONS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static string? Normalize(string en) => en.Trim().ToLowerInvariant() switch
    {
        "japanese" => "japanese",
        "fengtian" => "fengtian",   // bandit 은 변형 없음 (null) — 유닛 정의 artAs 로 봉천 그림 대용 (N26)
        "russian" or "russian-white" or "white" => "russian-white",
        "soviet" or "russian-red" => "russian-red",
        _ => null,
    };

    public static string? Variant(World world, int owner)
    {
        if (!ReferenceEquals(forWorld, world))
        {
            forWorld = world;
            var defs = MapFile.FactionsOf(world.Map);
            byOwner = new string?[world.Factions.Count];
            for (int i = 1; i < byOwner.Length; i++)
            {
                string en = i - 1 < Override.Length ? Override[i - 1] : i - 1 < defs.Count ? defs[i - 1].En : "";
                byOwner[i] = Normalize(en);
            }
        }
        return (uint)owner < (uint)byOwner.Length ? byOwner[owner] : null;
    }
}
