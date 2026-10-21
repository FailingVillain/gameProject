using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// 포병 사격 연출 (표현 전용 — 시뮬레이션 상태를 바꾸지 않는다). world.Shots 의 새 포 발사 (포구 화염 · 발사음),
/// world.Shells 의 날아가는 포탄 (휘파람 · 직사 포탄 궤적), world.Impacts 의 새 착탄 (폭발 · 착탄음). 폭발은 테마 'fx.explosion' 시트(없으면 원으로 대체).
/// 화면 밖 · 안개 속은 그리지 않고, 목록은 상한이 있다.
/// </summary>
internal static class Impacts
{
    const int Life = 40, MaxList = 256;
    struct Boom { public Vector2 Pos; public long Born; public float Radius; public bool Muzzle; }

    /// <summary>폭발 · 포구 화염 변형 선택: 위치와 시각의 해시 (화면 전용이지만 리플레이에서도 같은 모양).</summary>
    static int Variant(Boom b, int count)
    {
        if (count <= 1) return 0;
        uint h = (uint)b.Born * 2654435761u ^ (uint)(int)b.Pos.X * 40503u ^ (uint)(int)b.Pos.Y * 2246822519u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return (int)(h % (uint)count);
    }

    static readonly List<Boom> booms = new();
    static World? lastWorld;
    static readonly List<(Vector2 Pos, bool Vehicle)> deaths = new();

    /// <summary>유닛 전사 (Corpses 가 알려줌). 차량이면 폭발 스프라이트 + explosion 소리, 보병이면 death 소리만. 다음 Scan 에서 처리.</summary>
    public static void UnitDied(Vector2 pos, bool vehicle) { if (deaths.Count < 512) deaths.Add((pos, vehicle)); }
    static long lastTick = -1;

    /// <summary>화면 흔들림 (옵션, 기본 꺼짐). Game 이 카메라 오프셋에 더해 쓸 수 있다 (월드 좌표 픽셀).</summary>
    public static bool Shake;
    public static Vector2 ShakeOffset(World world, Vector2 viewCenter)
    {
        if (!Shake) return Vector2.Zero;
        float best = 0;
        foreach (var b in booms)
        {
            long age = world.Tick - b.Born;
            if (age < 0 || age > 12) continue;
            float k = (1 - age / 12f) * Math.Clamp(1 - Vector2.Distance(b.Pos / Config.TileSize, viewCenter) / 30f, 0, 1);
            best = MathF.Max(best, k);
        }
        if (best <= 0) return Vector2.Zero;
        return new Vector2(MathF.Sin(world.Tick * 2.7f), MathF.Cos(world.Tick * 3.3f)) * 3f * best;
    }

    /// <summary>매 프레임 한 번. 새 포 발사 (world.Shots 의 범위 · 곡사 무기: 포구 화염 · 발사음), 날아가는 포탄의 휘파람, 새 착탄 (world.Impacts: 폭발 · 착탄음).</summary>
    public static void Scan(World world, RtsCamera cam, int me)
    {
        if (!ReferenceEquals(world, lastWorld) || world.Tick < lastTick) { lastWorld = world; booms.Clear(); lastTick = world.Tick; SfxDirector.ResetShells(); return; }
        var fog = world.Fog;
        foreach (var (pos, vehicle) in deaths)
        {
            Sfx.PlayAt(vehicle ? "explosion" : "death", pos, cam);
            if (vehicle) { if (booms.Count >= MaxList) booms.RemoveAt(0); booms.Add(new Boom { Pos = pos * Config.TileSize, Born = world.Tick, Radius = 1.1f * Config.TileSize }); }
        }
        deaths.Clear();
        // 발사 (발사 틱): 포구 화염 + 발사음. 사격 틱 T 는 T→T+1 스텝 안에서 기록되므로, 지난 프레임의 Tick(=T) 과 같은 것도 새 사격이다
        foreach (var (from, _, _, tick, _, shooterId) in world.Shots)
        {
            if (tick < lastTick || world.Find(shooterId) is not Unit su || su.Weapon is not { } w || (w.Splash <= 0 && !w.Indirect)) continue;
            if (!fog.IsVisible(me, from)) continue;
            SfxDirector.GunFire(cam, from, w.Key);
            if (booms.Count >= MaxList) booms.RemoveAt(0);
            booms.Add(new Boom { Pos = (from + new Vector2(MathF.Cos(su.Facing), MathF.Sin(su.Facing)) * 0.8f) * Config.TileSize, Born = tick, Muzzle = true });   // 포신 쪽 앞
        }
        SfxDirector.ShellWhistles(world, cam, me);   // 날아가는 곡사 포탄: 착탄 순간에 끝나게 휘파람
        // 착탄 (착탄 틱): 폭발 + 착탄음
        foreach (var (_, impact, _, tick, radius, _, weapon) in world.Impacts)
        {
            if (tick < lastTick || !fog.IsVisible(me, impact)) continue;
            SfxDirector.ShellImpact(cam, impact, weapon);
            if (booms.Count >= MaxList) booms.RemoveAt(0);
            booms.Add(new Boom { Pos = impact * Config.TileSize, Born = tick, Radius = (radius > 0 ? radius : 2f) * Config.TileSize });
        }
        lastTick = world.Tick;
        int k = 0;
        while (k < booms.Count && world.Tick - booms[k].Born > Life) k++;
        if (k > 0) booms.RemoveRange(0, k);
    }

    /// <summary>BeginMode2D 안에서 (유닛 위에) 폭발을 그린다.</summary>
    public static void Draw(World world, RtsCamera cam, int me, Vector2 min, Vector2 max)
    {
        if (booms.Count == 0) return;
        var spr = Theme.Current?.Get("fx.explosion");
        var fog = world.Fog;
        foreach (var b in booms)
        {
            if (b.Muzzle) { DrawMuzzle(world, cam, me, b, min, max); continue; }
            Vector2 t = b.Pos / Config.TileSize;
            if (t.X < min.X - 3 || t.X > max.X + 3 || t.Y < min.Y - 3 || t.Y > max.Y + 3) continue;
            if (!fog.IsVisible(me, t)) continue;
            float age = world.Tick - b.Born;
            if (age < 0 || age > Life) continue;
            float u = age / Life;
            if (spr is { Directional: true })
            {
                var frames = spr.Anim("play");
                float dur = spr.Fps > 0 ? frames.Length / spr.Fps / Config.SimDt : Life;   // 틱
                if (age >= dur) continue;
                int col = frames[Math.Min(frames.Length - 1, (int)(age / dur * frames.Length))];
                float scale = b.Radius * 2.6f / spr.FrameW;
                Theme.DrawCell(spr, Variant(b, spr.Variants), false, col, b.Pos, scale, cam.Zoom < 0.75f, Color.White);
                continue;
            }
            float r = b.Radius * (0.4f + u * 0.9f);
            Raylib.DrawCircleV(b.Pos, r, Palette.Alpha(new Color(255, 170, 70, 255), (int)(190 * (1 - u))));
            Raylib.DrawCircleV(b.Pos, r * 0.55f, Palette.Alpha(new Color(255, 235, 170, 255), (int)(220 * (1 - u))));
            Raylib.DrawRing(b.Pos, r, r + 2, 0, 360, 28, Palette.Alpha(new Color(60, 50, 40, 255), (int)(200 * (1 - u))));
        }
    }

    static void DrawMuzzle(World world, RtsCamera cam, int me, Boom b, Vector2 min, Vector2 max)
    {
        var spr = Theme.Current?.Get("fx.muzzle_big");
        if (spr is not { Directional: true }) return;
        float age = world.Tick - b.Born;
        var frames = spr.Anim("play");
        float dur = spr.Fps > 0 ? frames.Length / spr.Fps / Config.SimDt : 5;
        if (age < 0 || age >= dur) return;
        int col = frames[Math.Min(frames.Length - 1, (int)(age / dur * frames.Length))];
        Theme.DrawCell(spr, Variant(b, spr.Variants), false, col, b.Pos, 0.7f, cam.Zoom < 0.75f, Color.White);
    }
}
