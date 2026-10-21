using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// 시체 (표현 전용 — 시뮬레이션 상태를 바꾸지 않는다). 화면에 보이던 유닛이 죽어 월드에서 사라진 것을 감지해 기록하고,
/// 보병: 맞음(0.15초, 보던 방향) → 쓰러짐(0.2초) → 엎어진 채 5초 → 0.3초 페이드.
/// 쓰러지는 방향은 유닛 id 로 정한 네 대각선 중 하나 (같은 경기는 같은 모습).
/// 전차: 차체 + 포탑 잔해 5초 → 페이드. 죽은 유닛 객체는 Alive = false 로 남아 있으므로 지난 프레임의 목록을 훑어 찾는다.
/// </summary>
internal static class Corpses
{
    const int HitTicks = 9, FallTicks = 12, KeepTicks = 300, FadeTicks = 18, MaxCorpses = 3000;

    struct Corpse
    {
        public Vector2 Pos;
        public float Facing, TurretFacing;
        public long Born;
        public int Owner, Fall, Id;
        public UnitArt Art;
    }

    static readonly List<Corpse> list = new();
    static World? lastWorld;
    /// <summary>쓰러지는 네 방향: 남동 · 남서 · 북동 · 북서.</summary>
    static readonly float[] FallFacing = { MathF.PI / 4, 3 * MathF.PI / 4, -MathF.PI / 4, -3 * MathF.PI / 4 };

    public static int Count => list.Count;
    static readonly bool Log = Environment.GetEnvironmentVariable("KD_CORPSE_LOG") != null;

    /// <summary>월드가 바뀌면(재시작 등) 비운다. 매 프레임 한 번 호출.</summary>
    public static void Begin(World world)
    {
        if (ReferenceEquals(lastWorld, world))
        {
            // 틱이 거꾸로 가면(되감기 등) 아직 태어나지 않은 시체는 지운다
            if (list.Count > 0 && list[^1].Born > world.Tick) list.RemoveAll(c => c.Born > world.Tick);
            return;
        }
        lastWorld = world;
        list.Clear();
    }

    /// <summary>지난 프레임에 보이던 유닛 중 죽은 것을 시체로. cur 의 앞 count 개가 대상.</summary>
    public static void Detect(World world, UnitRenderer.Item[] prev, int count, Func<Unit, float> turretOf)
    {
        for (int i = 0; i < count; i++)
        {
            var u = prev[i].U;
            if (u.Alive) continue;
            var art = prev[i].Art;
            Impacts.UnitDied(u.Pos, u.Def.IsArmored || art.Tank || art.Vehicle);   // 차량: 폭발 연출 + 소리, 보병: 소리만
            if (!art.Dir || !(art.HasDeath || ((art.Tank || art.Vehicle) && art.HasWreck))) continue;
            if (Log) Console.Error.WriteLine($"[corpse] tick {world.Tick} unit {u.Id} {u.Def.Key} owner {u.Owner}");
            list.Add(new Corpse
            {
                Pos = u.Pos, Facing = u.Facing, TurretFacing = art.Tank ? turretOf(u) : u.Facing, Born = world.Tick,
                Owner = u.Owner, Art = art, Id = u.Id, Fall = (int)(((uint)u.Id * 2654435761u) >> 30),
            });
        }
        if (list.Count > MaxCorpses) list.RemoveRange(0, list.Count - MaxCorpses);
        // 오래된 것(앞쪽)부터 만료 제거
        int k = 0;
        while (k < list.Count && world.Tick - list[k].Born >= KeepTicks + FadeTicks) k++;
        if (k > 0) list.RemoveRange(0, k);
    }

    /// <summary>살아 있는 유닛 밑에 그린다. 화면 밖 · 시야 밖은 건너뜀.</summary>
    public static void Draw(World world, RtsCamera cam, int me, Vector2 min, Vector2 max)
    {
        if (FakeAge >= 0) DrawFakes(world, cam, me);
        if (list.Count == 0) return;
        DrawList(list, world, cam, me, min, max);
    }

    /// <summary>[확인용] 환경변수 KD_FAKE_AGE=틱 이면 화면 중앙에 그 나이의 가짜 시체들(보병 8방향 · 전차)을 매 프레임 그린다.</summary>
    static readonly int FakeAge = int.TryParse(Environment.GetEnvironmentVariable("KD_FAKE_AGE"), out var fa) ? fa : -1;
    static readonly List<Corpse> fakes = new();

    static void DrawFakes(World world, RtsCamera cam, int me)
    {
        fakes.Clear();
        var c0 = cam.CenterWorld;
        if (!Defs.Current.Units.TryGetValue("infantry", out var inf)) return;
        var art = UnitArt.For(inf);
        for (int i = 0; i < 8; i++)
            fakes.Add(new Corpse { Pos = c0 + new Vector2(-4 + i * 1.1f, -1.5f), Facing = i * MathF.PI / 4 - MathF.PI, Born = world.Tick - FakeAge, Owner = 1 + i % 2, Art = art, Fall = i % 4 });
        if (Defs.Current.Units.TryGetValue("tank_ft_male", out var tk))
        {
            var ta = UnitArt.For(tk);
            for (int i = 0; i < 6; i++)
                fakes.Add(new Corpse { Pos = c0 + new Vector2(-3.5f + i * 1.5f, 1.5f), Facing = i * MathF.PI / 3, TurretFacing = i * MathF.PI / 3 + 0.7f, Born = world.Tick - FakeAge, Owner = 1, Art = ta });
        }
        var (min, max) = cam.VisibleWorld();
        DrawList(fakes, world, cam, 0, min, max);
    }

    static void DrawList(List<Corpse> list, World world, RtsCamera cam, int me, Vector2 min, Vector2 max)
    {
        var fog = world.Fog;
        long tick = world.Tick;
        float zoom = cam.Zoom;
        foreach (var c in list)
        {
            if (c.Pos.X < min.X || c.Pos.X > max.X || c.Pos.Y < min.Y || c.Pos.Y > max.Y) continue;
            if (!fog.IsVisible(me, c.Pos)) continue;
            long age = tick - c.Born;
            if (age < 0) continue;
            int alpha = age < KeepTicks ? 255 : (int)(255 * (1f - (age - KeepTicks) / (float)FadeTicks));
            if (alpha <= 0) continue;
            var team = Palette.Player(c.Owner);
            var body = c.Art.Body!;
            Vector2 p = c.Pos * Config.TileSize;
            float screen = body.Scale * zoom;
            if (c.Art.Lod != null)
            {
                // 새 형식: 잔해 칸 / 유닛 id 로 고른 사망 애니메이션 (death · death_b · death_c), 보던 방향 그대로
                if (c.Art.Tank || c.Art.Vehicle)
                {
                    TeamShader.Leave();
                    float vr = body.Scale * body.FrameW * 0.375f;   // 차체 칸 폭 기준 (192 → 72, 288 → 108)
                    Blob.Draw(Blob.Shadow, p + new Vector2(0, 2), vr, vr * 0.45f, Palette.Alpha(Color.Black, alpha * 90 / 255));
                    c.Art.DrawLodVehicle(null, tick, p, zoom, c.Facing, c.TurretFacing, team, true, alpha);
                }
                else LodDraw.Directional(c.Art.Lod, p, c.Art.Lod.Scale, c.Art.Lod.Scale * zoom, c.Facing, c.Art.LodDeathFrame(c.Id, age), team, alpha);
                continue;
            }
            TeamShader.Leave();
            if (c.Art.Vehicle)
            {
                float vr = body.Scale * 36f;
                Blob.Draw(Blob.Shadow, p + new Vector2(0, 2), vr, vr * 0.45f, Palette.Alpha(Color.Black, alpha * 90 / 255));
                Theme.DrawDirectional(body, p, body.Scale, screen, c.Facing, c.Art.HullWreck, team, alpha);
                continue;
            }
            if (c.Art.Tank)
            {
                float r = c.Art.Body!.Scale * 36f;
                Blob.Draw(Blob.Shadow, p + new Vector2(0, 2), r, r * 0.45f, Palette.Alpha(Color.Black, alpha * 90 / 255));
                c.Art.DrawTank(p, screen, c.Facing, c.TurretFacing, c.Art.HullWreck, c.Art.TurretWreck, team, alpha);
                continue;
            }
            // 보병: 맞음(보던 방향) → 쓰러짐 → 엎어짐 (대각선 방향)
            int col; float facing;
            if (age < HitTicks) { col = c.Art.Death[0]; facing = c.Facing; }
            else { col = age < HitTicks + FallTicks ? c.Art.Death[1] : c.Art.Death[2]; facing = FallFacing[c.Fall]; }
            Theme.DrawDirectional(body, p, body.Scale, screen, facing, col, team, alpha);
        }
        TeamShader.Leave();
    }
}
