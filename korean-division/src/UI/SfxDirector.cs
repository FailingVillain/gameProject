using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// 전투 소리 연출 (표현 전용 — 시뮬레이션 상태를 읽기만 하고 바꾸지 않는다. 결정성 · 리플레이와 무관).
/// World.Shots / MeleeStrikes / Impacts 의 새 사건을 훑어 무기별 키를 고르고, 유닛 상태(전차 · 기병 · 마차 · 삽질)로 반복 고리를 켠다.
/// 키 · 음량 · 동시 재생 수 · near/far 거리는 assets/sounds/kd1920/sounds.json. 수천 기 교전 대비:
///  · 새 사건 하나당 비용은 키 조회 + 가시 검사 정도, 총알 가르는 소리 · 도탄은 확률로 솎고 sounds.json 의 minInterval/maxVoices 가 한 번 더 막는다
///  · 유닛 훑기(고리용)는 0.12초에 한 번, 화면 근처 유닛만 본다 · 프레임당 시작 예산은 Sfx.FrameBudget
/// </summary>
internal static class SfxDirector
{
    static World? lastWorld;
    static long shotTick, meleeTick;
    static readonly Random rng = new(7);      // 화면 전용 난수 (시뮬레이션 난수와 무관)
    static float combatRate;                  // 보이는 교전 사건 초당 수 (부드럽게)
    static int eventsThisFrame;
    static double nextScan, nextHorse;
    /// <summary>발굽 고리에 동시에 더해 줄 말 수 상한 (많은 기병 = 한 고리를 조금 키울 뿐, 여러 겹 쌓지 않음). 표현 전용.</summary>
    const int HoofLoopCap = 3;
    static double lastFrameTime;
    static readonly HashSet<int> mounting = new();   // 하마 · 승마 전환 중인 기병 (전환 시작에 한 번만 소리). 표현 전용.

    /// <summary>월드가 바뀌었거나 시간이 되감겼으면 기준을 다시 잡는다. 다시 잡았으면 true (이번 프레임 사건은 건너뜀).</summary>
    static bool Sync(World world)
    {
        if (ReferenceEquals(world, lastWorld) && world.Tick >= shotTick) return false;
        lastWorld = world;
        shotTick = meleeTick = world.Tick;
        combatRate = 0; eventsThisFrame = 0; mounting.Clear();
        return true;
    }

    // ---------------------------------------------------------------- 사격 · 근접

    /// <summary>무기 키 → (효과음 키, 음량). hmg 는 고리.</summary>
    static (string Key, float Vol) FireKey(Entity? shooter)
    {
        if (shooter is Unit u)
            return (u.Weapon?.Key ?? "") switch
            {
                "rifle" or "rifle_long" or "rifle_mosin" or "rifle_berdan2" or "rifle_arisaka38" or "rifle_murata" or "rifle_hanyang88" or "rifle_mauser98" or "rifle_lebel86" => ("rifle", 1f),
                "sniper_rifle" => ("rifle", 1.15f),
                "carbine" or "carbine_assault" => ("carbine", 1f),
                "pistol" or "pistol_assault" => ("pistol", 1f),
                "lmg" or "lmg_chauchat" or "mg" => ("lmg", 1f),
                "hmg" or "hmg_type3" => ("hmg_loop", 1f),
                "at_rifle" => ("at_rifle", 1f),
                "shotgun" => ("shotgun", 1f),   // 엽총 (마적 엽총수): 묵직한 단발 — 대전차소총 소리 재사용
                "ft_gun" => ("gun37_fire", 1f),
                "tank_gun" => ("artillery_fire", 0.9f),
                _ => ("rifle", 1f),
            };
        return shooter is Building ? ("lmg", 1f) : ("rifle", 1f);   // 포탑 건물
    }

    /// <summary>매 프레임 한 번: 새 사격 · 근접 타격에 소리를 낸다 (보이는 것만). 포 · 범위 무기의 발사 / 착탄은 Impacts.Scan 이 낸다.</summary>
    public static void Events(World world, RtsCamera cam, int seat)
    {
        if (Sync(world)) return;
        var fog = world.Fog;
        Vector2 center = cam.CenterWorld;
        foreach (var (from, to, _, tick, hit, shooterId) in world.Shots)
        {
            if (tick < shotTick) continue;
            var shooter = world.Find(shooterId);
            var wpn = (shooter as Unit)?.Weapon;
            if (wpn != null && (wpn.Splash > 0 || wpn.Indirect)) { if (fog.IsVisible(seat, from)) eventsThisFrame++; continue; }
            bool seenFrom = fog.IsVisible(seat, from), seenTo = fog.IsVisible(seat, to);
            if (!seenFrom && !seenTo) continue;
            eventsThisFrame++;
            if (seenFrom)
            {
                var (key, vol) = FireKey(shooter);
                if (key == "hmg_loop") Sfx.LoopAt(key, from, cam, 0.8f);   // 사격이 이어지는 동안 고리, 멈추면 서서히 꺼짐
                else Sfx.PlayAt(key, from, cam, vol);
            }
            if (!seenTo || Vector2.DistanceSquared(to, center) > 18f * 18f) continue;
            // 화면 가까이 떨어지는 탄: 스치는 소리 (빗나감) · 갈라지는 소리 (명중) · 가끔 도탄. 확률로 솎아 폭주를 막는다.
            double r = rng.NextDouble();
            if (hit) { if (r < 0.14) Sfx.PlayAt("bullet_crack", to, cam, 0.9f); else if (r < 0.2) Sfx.PlayAt("ricochet", to, cam, 0.8f); else Sfx.PlayAt("impact", to, cam, 0.7f); }
            else if (r < 0.35) Sfx.PlayAt("bullet_whiz", to, cam);
            else if (r < 0.43) Sfx.PlayAt("ricochet", to, cam, 0.8f);
        }
        shotTick = world.Tick;

        foreach (var (from, to, _, tick, hit, attackerId) in world.MeleeStrikes)
        {
            if (tick < meleeTick) continue;
            bool seenFrom = fog.IsVisible(seat, from), seenTo = fog.IsVisible(seat, to);
            if (!seenFrom && !seenTo) continue;
            eventsThisFrame++;
            int variant = (world.Find(attackerId) as Unit)?.Melee?.Key switch { "dadao" => 1, "bayonet" => 2, _ => 0 };   // 곡도 · 대도 · 총검 찌르기
            Sfx.PlayAt("melee_swing", from, cam, 1f, variant);
            if (hit)
            {
                Sfx.PlayAt("melee_hit", to, cam);
                Sfx.PlayAt("melee_flesh", to, cam);
            }
        }
        meleeTick = world.Tick;
    }

    // ---------------------------------------------------------------- 포 · 포탄 · 수류탄 (Impacts.Scan 에서): 발사 → (비행) → 휘파람 → 착탄

    /// <summary>게임 속도 배수 (Game 이 매 프레임 넣는다, 일시정지 = 0). 포탄 휘파람을 착탄 순간에 끝나게 시작 시각을 맞추는 데만 쓴다.</summary>
    public static float SimSpeed = 1f;

    /// <summary>무기 키 → (발사음, 착탄음, 착탄 음량).</summary>
    static (string Fire, string Boom, float BoomVol) GunKeys(string weapon) => weapon switch
    {
        "howitzer_heavy" => ("howitzer_fire", "howitzer_impact", 1f),
        "gun_infantry" => ("gun37_fire", "explosion", 0.6f),
        _ => ("artillery_fire", "artillery_impact", 1f),
    };

    /// <summary>포 · 범위포 발사 (발사 틱, Shots 에서): 발사음.</summary>
    public static void GunFire(RtsCamera cam, Vector2 from, string weapon) => Sfx.PlayAt(GunKeys(weapon).Fire, from, cam);

    /// <summary>착탄 · 수류탄 폭발 (착탄 틱, Impacts 에서): 착탄음. 휘파람은 ShellWhistle 이 미리 시작해 이 순간에 끝난다.</summary>
    public static void ShellImpact(RtsCamera cam, Vector2 impact, string weapon)
    {
        if (weapon == "grenade") { Sfx.PlayAt("explosion", impact, cam, 0.85f); return; }
        var k = GunKeys(weapon);
        Sfx.PlayAt(k.Boom, impact, cam, k.BoomVol);
    }

    /// <summary>곡사 포탄 휘파람 변형 (없으면 -1): 중곡사포 2, 곡사포 · 견인 곡사포 가까우면 0 / 멀면 1. 직사포는 없음.</summary>
    static int WhistleVariant(string weapon, float dist)
    {
        int v = weapon switch { "howitzer_heavy" => 2, "howitzer" or "howitzer_towed" => dist < 14 ? 0 : 1, _ => -1 };
        return v >= 0 && Sfx.VariantCount("shell_incoming") > v ? v : -1;
    }

    static readonly HashSet<(int Shooter, long Fire)> whistled = new();

    /// <summary>
    /// 매 프레임 (Impacts.Scan): 날아가는 곡사 포탄마다, 착탄까지 남은 실시간 ≤ 휘파람 길이가 되는 순간 휘파람을 시작한다 (= 착탄 순간에 끝남).
    /// 처음 볼 때 이미 짧으면 바로 시작 (잘린 채 착탄음과 겹침). 착탄점이 보일 때만.
    /// </summary>
    public static void ShellWhistles(World world, RtsCamera cam, int seat)
    {
        if (whistled.Count > 0) whistled.RemoveWhere(k => world.Tick - k.Fire > 1200);
        if (world.Shells.Count == 0 || SimSpeed <= 0) return;
        var fog = world.Fog;
        foreach (var s in world.Shells)
        {
            int v = WhistleVariant(s.Weapon.Key, Vector2.Distance(s.From, s.Pos));
            if (v < 0 || whistled.Contains((s.ShooterId, s.FireTick))) continue;
            float left = (s.ImpactTick - world.Tick) * Config.SimDt / SimSpeed;
            if (left > Sfx.Duration("shell_incoming", v)) continue;
            whistled.Add((s.ShooterId, s.FireTick));
            if (fog.IsVisible(seat, s.Pos)) Sfx.PlayAt("shell_incoming", s.Pos, cam, 1f, v);
        }
    }

    /// <summary>월드가 바뀌었거나 되감겼을 때 (Impacts.Scan 이 부른다).</summary>
    public static void ResetShells() => whistled.Clear();

    // ---------------------------------------------------------------- 매 프레임: 교전 환경음 · 유닛 상태 고리

    public static void Frame(World world, RtsCamera cam, int seat)
    {
        if (!Sfx.Ready) return;
        double now = Raylib.GetTime();
        float dt = (float)Math.Clamp(now - lastFrameTime, 0.001, 0.25);
        lastFrameTime = now;
        // 교전 환경음: 보이는 사건 비율을 부드럽게 따라가며 세기로
        combatRate += (eventsThisFrame / dt - combatRate) * MathF.Min(1f, dt * 0.7f);
        eventsThisFrame = 0;
        float loud = Math.Clamp(combatRate / 10f, 0f, 1f);
        if (loud > 0.05f) Sfx.Loop("ambient_battle", 0.2f + 0.8f * loud);

        if (now < nextScan) return;
        nextScan = now + 0.12;
        ScanUnits(world, cam, seat, now);
    }

    static void ScanUnits(World world, RtsCamera cam, int seat, double now)
    {
        var (min, max) = cam.VisibleWorld();
        min -= new Vector2(8, 8); max += new Vector2(8, 8);
        var fog = world.Fog;
        Vector2 center = cam.CenterWorld;
        Unit? horse = null;
        int horses = 0, gallopers = 0, chargers = 0;
        foreach (var u in world.Units)
        {
            if (!u.Alive) continue;
            var p = u.Pos;
            if (p.X < min.X || p.X > max.X || p.Y < min.Y || p.Y > max.Y) continue;
            var d = u.Def;
            if (d.IsInfantry && !d.Mounted && !(d.SetupTime > 0 && d.Key is "heavy_tachanka")) continue;   // 보병은 고리 없음 (대부분의 유닛이 여기서 끝; 타찬카는 마차라 예외)
            if (u.Owner != seat && !fog.IsVisible(seat, p)) continue;
            if (d.IsArmored)
            {
                if (u.Walking) { Sfx.LoopAt("tank_move", p, cam, 0.5f); Sfx.LoopAt("tank_tracks", p, cam, 0.5f); }
                else Sfx.LoopAt("tank_idle", p, cam, 0.5f);
            }
            else if (d.IsSupplyWagon || d.Key is "heavy_tachanka" or "heavy_horse_artillery") { if (u.Walking) Sfx.LoopAt("wagon_loop", p, cam, 0.6f); }   // 보급 · 타찬카 · 기마 포병 마차 바퀴
            else if (d.Mounted)
            {
                if (u.MountLeft > 0) { if (mounting.Add(u.Id)) Sfx.PlayAt("mount_switch", p, cam); }   // 하마 · 승마 전환 시작 한 번
                else if (mounting.Count > 0) mounting.Remove(u.Id);
                if (u.Charging) { if (chargers++ < HoofLoopCap) Sfx.LoopAt("cavalry_charge", p, cam, 0.4f); }
                else if (u.Walking && u.Velocity.LengthSquared() > 2.25f) { if (gallopers++ < HoofLoopCap) Sfx.LoopAt("cavalry_move", p, cam, 0.4f); }
                else if (!u.Walking && Vector2.DistanceSquared(p, center) < 18f * 18f && rng.Next(++horses) == 0) horse = u;   // 서 있는 말: 한 마리 뽑기
            }
        }
        if (horse != null && now >= nextHorse)
        {
            nextHorse = now + 3 + rng.NextDouble() * 6;
            Sfx.PlayAt(rng.Next(3) switch { 0 => "horse_snort", 1 => "horse_nicker", _ => "horse_stomp" }, horse.Pos, cam);
        }
    }
}
