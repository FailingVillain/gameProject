using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

public static class UnitRenderer
{
    const float TS = Config.TileSize;
    static readonly List<Unit> drawOrder = new();

    /// <summary>
    /// 유닛 몸체 (월드/화면 공용 — HUD 아이콘에도 사용). c = 중심, r = 반경(픽셀).
    /// 방향 · 동작 시트가 있으면 '서 있기' 프레임을 그 방향으로, 회전 스프라이트가 있으면 회전, 없으면 벡터 그림.
    /// </summary>
    public static void DrawBody(Vector2 c, float r, Color team, float facing, string key = "infantry")
    {
        var sprite = Theme.Current?.Get("unit." + key);
        if (sprite?.Lod is { } lod)
        {
            // 새 형식 아이콘: 정면 서 있기 (전차는 차체 + 포탑). 화면 배율로 LOD 선택
            float sc = r * 2.6f / (sprite.FrameH * 0.75f);
            var ft = c + new Vector2(0, r * 1.25f);
            int idle = lod.Anim("idle")?.Frames[0] ?? 0;
            if (lod.Levels[0].Mount != null && Theme.Current?.Get("unit." + key + ".turret")?.Lod is { } tl)
                LodDraw.Tank(lod, tl, ft, sc, sc, MathF.PI / 2, MathF.PI / 2, idle, tl.Anim("idle")?.Frames[0] ?? 0, team);
            else LodDraw.Directional(lod, ft, sc, sc, MathF.PI / 2, idle, team);
            TeamShader.Leave();
            return;
        }
        if (sprite is { Directional: true })
        {
            // 아이콘: 그림 높이(프레임의 약 3/4)가 반경의 2.6배쯤 되게
            float scale = r * 2.6f / (sprite.FrameH * 0.75f);
            var feet = c + new Vector2(0, r * 1.25f);
            if (sprite.Mounts != null && Theme.Current?.Get("unit." + key + ".turret") is { Directional: true } tur)
            {
                // 전차 아이콘: 차체 + 포탑 (정면)
                int row = Theme.RowOf(sprite, MathF.PI / 2, out _);
                Theme.DrawCell(sprite, row, false, sprite.Anim("idle")[0], feet, scale, true, team);
                var g = sprite.AnchorAt(row, 0); var m = sprite.Mounts[row];
                Theme.DrawCell(tur, Theme.RowOf(tur, MathF.PI / 2, out _), false, tur.Anim("idle")[0], feet + (m - g) * scale, scale, true, team);
                return;
            }
            Theme.DrawDirectional(sprite, feet, scale, scale, MathF.PI / 2, sprite.Anim("idle")[0], team);
            return;
        }
        if (DrawSprite(c, r, team, facing, key)) return;
        var dir = new Vector2(MathF.Cos(facing), MathF.Sin(facing));
        var side = new Vector2(-dir.Y, dir.X);
        if (key.StartsWith("tank")) { DrawTank(c, r, team, dir, side); return; }
        if (key == "worker")
            Raylib.DrawLineEx(c + side * r * 0.5f, c + side * r * 0.5f + dir * r * 1.1f, MathF.Max(2f, r * 0.35f), new Color(200, 170, 60, 255)); // 공구
        else
            Raylib.DrawLineEx(c + dir * r * 0.2f, c + dir * r * (key == "scout" ? 1.25f : 1.5f), MathF.Max(2f, r * 0.3f), new Color(36, 38, 42, 255)); // 소총
        Raylib.DrawCircleV(c, r, team);
        Raylib.DrawRing(c, r * 0.84f, r, 0, 360, 24, Palette.Shade(team, -75));
        Vector2 helmet = c + dir * r * 0.12f;
        Raylib.DrawCircleV(helmet, r * 0.5f, Palette.Shade(team, -38));
        Raylib.DrawCircleV(helmet - new Vector2(r * 0.14f), r * 0.17f, Palette.Alpha(Color.White, 90));
        if (key == "worker")      // 안전모 띠
            Raylib.DrawRing(helmet, r * 0.38f, r * 0.5f, 0, 360, 16, new Color(230, 190, 60, 255));
        else if (key == "scout")  // 안테나
            Raylib.DrawLineEx(c - dir * r * 0.3f + side * r * 0.4f, c - dir * r * 1.1f + side * r * 0.6f, 1.5f, new Color(30, 30, 30, 255));
    }

    /// <summary>초상화 칸에 그리기: 테마 초상화가 있으면 그것, 없으면 몸체 그림. 초상화를 썼으면 true.</summary>
    public static bool DrawPortrait(Rectangle r, Color team, float facing, string key)
    {
        var sprite = Theme.Current?.Get("unit." + key);
        if (sprite != null && Theme.DrawPortrait(sprite, r, team)) return true;
        DrawBody(new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2 + 4), MathF.Min(r.Width, r.Height) * 0.27f, team, facing, key);
        return false;
    }

    /// <summary>이미지 테마로 그린다 (스프라이트는 위쪽이 앞). 테마/항목이 없으면 false -> 벡터 그림.</summary>
    static bool DrawSprite(Vector2 c, float r, Color team, float facing, string key)
    {
        var sprite = Theme.Current?.Get("unit." + key);
        if (sprite == null) return false;
        float refR = Defs.Current.Units.TryGetValue(key, out var def) ? def.Radius * TS : r;
        float scale = r / MathF.Max(1f, refR);
        float rot = facing * (180f / MathF.PI) + 90f;
        Theme.Draw(sprite, c, scale, rot, team);
        var turret = Theme.Current?.Get("unit." + key + ".turret");   // 포탑은 따로 회전 (지금은 차체 방향과 같음)
        if (turret != null) Theme.Draw(turret, c, scale, rot, team);
        return true;
    }

    /// <summary>전차: 차체(사각) + 양쪽 궤도 + 포탑(원) + 포신. 이미지 테마가 없을 때의 기본 그림.</summary>
    static void DrawTank(Vector2 c, float r, Color team, Vector2 dir, Vector2 side)
    {
        Vector2 P(float f, float s) => c + dir * (r * f) + side * (r * s);
        void Quad(Vector2 a, Vector2 b, Vector2 cc, Vector2 d, Color col)
        {
            Icons.Tri(a, b, cc, col);
            Icons.Tri(a, cc, d, col);
        }
        var track = new Color(40, 40, 36, 255);
        Quad(P(1.05f, -1.0f), P(1.05f, -0.62f), P(-1.05f, -0.62f), P(-1.05f, -1.0f), track);
        Quad(P(1.05f, 0.62f), P(1.05f, 1.0f), P(-1.05f, 1.0f), P(-1.05f, 0.62f), track);
        Quad(P(0.95f, -0.66f), P(0.95f, 0.66f), P(-0.95f, 0.66f), P(-0.95f, -0.66f), Palette.Shade(team, -45));
        Quad(P(0.8f, -0.52f), P(0.8f, 0.52f), P(-0.8f, 0.52f), P(-0.8f, -0.52f), team);
        Raylib.DrawLineEx(c, P(1.65f, 0), MathF.Max(2f, r * 0.22f), new Color(36, 38, 42, 255));
        Raylib.DrawCircleV(c, r * 0.48f, Palette.Shade(team, -25));
        Raylib.DrawRing(c, r * 0.42f, r * 0.5f, 0, 360, 20, Palette.Shade(team, -80));
    }

    public static void DrawHpBar(Rectangle r, float ratio)
    {
        Raylib.DrawRectangleRec(r, Palette.HpBg);
        Raylib.DrawRectangleRec(new Rectangle(r.X + 1, r.Y + 1, (r.Width - 2) * Math.Clamp(ratio, 0, 1), r.Height - 2), Palette.HpColor(ratio));
    }

    // ---------------------------------------------------------------- 월드 그리기

    static readonly bool SwitchLog = Environment.GetEnvironmentVariable("KD_TEXSWITCH") != null;
    /// <summary>[측정] y 순 그리기에서 텍스처가 바뀌는 횟수: 시트별 텍스처(아틀라스 전) 대 페이지(아틀라스 후). KD_TEXSWITCH=1.</summary>
    static void LogSwitches(int n)
    {
        int before = 0, after = 0; ThemeSprite? prevBody = null; uint prevPage = 0;
        for (int i = 0; i < n; i++)
        {
            var a = sorted[i].Art;
            if (!a.Dir) continue;
            // 전차는 차체 시트 + 포탑 시트를 번갈아 쓴다
            var seq = a.Tank ? new[] { a.Body!, a.Turret! } : new[] { a.Body! };
            foreach (var b in seq)
            {
                if (!ReferenceEquals(b, prevBody)) before++;
                if (b.Sheet.Id != prevPage) after++;
                prevBody = b; prevPage = b.Sheet.Id;
            }
        }
        Console.Error.WriteLine($"[texswitch] 유닛 {n}기: 시트별 텍스처 {before}회 -> 아틀라스 {after}회");
    }

    /// <summary>화면에 그릴 유닛 한 기 (컬링 · 정렬용 작은 구조체).</summary>
    internal struct Item { public Unit U; public UnitArt Art; public float Y; }

    static Item[] cull = new Item[1024], prevCull = new Item[1024], sorted = new Item[1024];
    static int cullCount, prevCount;
    static int[] buckets = new int[2048];
    static readonly Dictionary<int, float> turretAngle = new();   // 전차 포탑의 현재 방향 (표적을 향해 부드럽게 돈다)
    static readonly Func<Unit, float> turretOf = u => turretAngle.TryGetValue(u.Id, out var a) ? a : u.Facing;
    /// <summary>[화면 보간] 0 = 지난 틱 위치, 1 = 지금 틱 위치 (Game 이 프레임마다 씀 — 누적 시간 / SimDt).</summary>
    public static float Alpha = 1f;
    static Vector2 RP(Unit u) => Alpha >= 0.999f ? u.Pos : Vector2.Lerp(u.RenderPrev, u.Pos, Alpha);
    /// <summary>
    /// 그림에 쓰는 방향 = 시트 행 중심 (행 히스테리시스, N2 2026-10-19): 시뮬레이션 방향이 지금 행의 칸 (반폭 + RowHysteresis) 을 벗어날 때만
    /// 가장 가까운 행 중심으로 옮긴다 — 경계 (정북 · 정남 반전 포함) 에서 매 틱 뒤집히지 않고, 그려진 방향과 실제 방향의 차이는 최대 반폭 + 4° (5행 = 26.5°, 9행 = 15.25°).
    /// 옛 규칙 (방향 12° 데드밴드 뒤 행 반올림) 은 차이가 반폭 + 12° 까지 났다.
    /// </summary>
    static readonly Dictionary<int, float> shownFacing = new();
    public const float RowHysteresis = 4f * MathF.PI / 180f;
    static readonly bool AimDebug = Environment.GetEnvironmentVariable("KD_AIMDEBUG") == "1";
    static float ShownFacing(Unit u, int dirCount)
    {
        bool has = shownFacing.TryGetValue(u.Id, out float f);
        float s = SnapFacing(has, f, u.Facing, dirCount);
        if (!has || s != f) shownFacing[u.Id] = s;
        return s;
    }

    /// <summary>[테스트] 행 히스테리시스 한 단계: 이전 표시 방향 prev (has = 있음) → 새 표시 방향 (행 중심). dirCount = 시트 행 수 (반원 s..n, 서쪽은 반전).</summary>
    public static float SnapFacing(bool has, float prev, float facing, int dirCount)
    {
        if (!float.IsFinite(facing)) return has && float.IsFinite(prev) ? prev : 0f;   // 깨진 방향 값이 표시 방향으로 굳지 않게
        if (has && !float.IsFinite(prev)) has = false;
        if (dirCount <= 1) return facing;
        float step = MathF.PI / (dirCount - 1);
        if (has)
        {
            float d = facing - prev;
            d -= MathF.Tau * MathF.Floor((d + MathF.PI) / MathF.Tau);
            if (MathF.Abs(d) <= step * 0.5f + RowHysteresis) return prev;
        }
        return MathF.Round(facing / step) * step;   // 행 중심: yaw = 90° − facing 이 step 의 배수 (90° 는 22.5° · 45° 의 배수)
    }

    /// <summary>이 배율보다 축소하면 유닛을 세력 색 점으로 그린다 (수천 기가 한 화면에 보여도 가볍게 · 알아보기 쉽게).</summary>
    public const float DotZoom = 0.5f;

    /// <summary>축소 팀색 발밑 고리 (사용자 2026-10-19 "발밑 고리만"): 줌 ≤ RingFull 에서 최대, RingOff 에서 0 (그 사이 선형 페이드), 그 위는 안 그림.
    /// DotZoom 아래는 점 · 중대 휘장이 대신하므로 해당 없음. 군복 색조는 건드리지 않는다.</summary>
    public const float RingFull = 0.8f, RingOff = 1.0f;
    const int RingAlpha = 210;
    static readonly bool NoRing = Environment.GetEnvironmentVariable("KD_NORING") == "1";   // 전후 비교용

    /// <summary>축소 표시: 유닛 = 세력 색 사각 점 (화면 기준 크기 고정, 큰 유닛은 조금 크게). 선택 = 흰 테두리.</summary>
    static void DrawDots(int me, Entity? hover, float zoom)
    {
        float px = 1f / zoom;   // 화면 1픽셀 = 월드 좌표 px
        var outline = Palette.Alpha(Color.Black, 150);
        for (int i = 0; i < cullCount; i++)
        {
            var u = cull[i].U;
            if (UnitSymbols.Replaces(u)) continue;   // 중대 소속은 부대 기호 하나로 (UnitSymbols.Draw)
            Vector2 p = RP(u) * TS;
            float half = (u.Radius > 0.5f ? 3f : 2f) * px;
            var c = Palette.Player(u.Owner);
            if (u.Selected || u == hover)
                Raylib.DrawRectangleRec(new Rectangle(p.X - half - px, p.Y - half - px, (half + px) * 2, (half + px) * 2),
                    u.Selected ? (u.Owner == me ? Palette.Select : Palette.HpLow) : Color.White);
            else
                Raylib.DrawRectangleRec(new Rectangle(p.X - half - px * 0.5f, p.Y - half - px * 0.5f, half * 2 + px, half * 2 + px), outline);
            Raylib.DrawRectangleRec(new Rectangle(p.X - half, p.Y - half, half * 2, half * 2), c);
        }
    }

    /// <summary>전차 포탑 방향 갱신: 표적이 있으면 그쪽으로 (초당 약 5 rad 로 부드럽게), 없으면 차체 방향으로 돌아온다.</summary>
    static float UpdateTurret(Unit u, float dt)
    {
        float want = u.Target != null ? MathF.Atan2(u.Target.Pos.Y - u.Pos.Y, u.Target.Pos.X - u.Pos.X) : u.Facing;
        float cur = turretAngle.TryGetValue(u.Id, out var a) ? a : u.Facing;
        float d = want - cur;
        d -= MathF.Tau * MathF.Floor((d + MathF.PI) / MathF.Tau);   // -π..π
        float step = 5f * dt;
        cur = MathF.Abs(d) <= step ? want : cur + MathF.Sign(d) * step;
        turretAngle[u.Id] = cur;
        return cur;
    }

    /// <summary>
    /// 지금 보여줄 동작 프레임 (시트 열 번호). 시뮬레이션 값만 읽는다.
    ///   사격 직후 0.1초 = 발사, 일꾼 작업 = 휘두르기, 걷는 중 = 걷기 2장 번갈아, 조준 중 · 표적을 두고 서 있음 = 조준,
    ///   가만히 있으면 가끔(4초 구간마다 12%) 대기 동작 1.3초 (보병: 총 치켜들기).
    /// </summary>
    static int Column(UnitArt a, Unit u, long tick)
    {
        long sinceShot = tick - u.LastShotTick;
        var shoot = a.ShootVariants != null ? a.ShootVariants[a.VariantFor(u)] : a.Shoot;
        if (sinceShot < 6) return shoot[^1];                                             // 발사 (섬광)
        if (a.Deploy != null && SimView.Deploying(u)) return a.Deploy[0];                  // 견인포: 전개 · 철수 중
        if (a.Deploy != null && SimView.Limbered(u) && !u.Walking) return a.Walk[0];       // 견인포: 이동 자세로 서 있음 (포를 매단 상태)
        if (u.Walking) return a.Walk[(int)((tick + u.Id * 3) / (a.Walk.Length > 2 ? 7 : 10) % a.Walk.Length)];   // 실제로 걸을 때만
        // 조준 유지 (칸이 하나뿐이면 차량: 서 있기). 공병은 제외 — 공병의 '조준' 칸은 공구를 머리 위로 든 자세라
        // 표적만 잡고 서 있어도 계속 그 자세(정찰병 쌍안경처럼 보임)로 굳어 있었다. 공병은 실제로 휘두를 때(발사 프레임)만 움직인다.
        if (u.Aiming || u.Target != null) return shoot.Length > 1 ? shoot[0] : a.Idle[0];
        if (a.IdleAlt != null)
        {
            long window = (tick + u.Id * 37) / 240;
            uint h = (uint)(window * 2654435761u) ^ (uint)(u.Id * 40503);
            if (h % 100 < 12 && (tick + u.Id * 37) % 240 < 80) return a.IdleAlt[0];
        }
        return a.Idle[0];
    }

    // ---------------------------------------------------------------- 머리띠 (팀 층) 표시 규칙 (사용자 2026-10-19)
    static bool[] bandOn = Array.Empty<bool>();
    static readonly string? BandOverride = Environment.GetEnvironmentVariable("KD_HEADBAND");   // always · never (그림 확인용)

    /// <summary>행진 사격 총구 화염 (표시 전용): 보병 시트 기준 총구 위치 (발밑 기준점 기준 시트 px, 방향별) 에 짧은 섬광. 쏜 뒤 ~3틱.</summary>
    static readonly Vector2[] MarchMuzzle = { new(-4f, -1f), new(52.5f, -29f), new(53.5f, -58.5f), new(54.5f, -109.5f), new(2.5f, -124f) };
    static void DrawMarchFlash(Vector2 p, float face, float scale, long age)
    {
        TeamShader.Leave();
        float c = MathF.Cos(face), sn = MathF.Sin(face);
        // 실측 (infantry walk_fire 시트의 소총 끝, 발밑 기준 시트 px; 방향 s · se · e · ne · n = 화면각 90 · 45 · 0 · -45 · -90 도, 왼쪽은 좌우 반전). 2026-10-21
        float ang = MathF.Atan2(sn, MathF.Abs(c)) * (180f / MathF.PI);   // -90..90
        float t = Math.Clamp((90f - ang) / 45f, 0f, 3.9999f);
        int i0 = (int)t; float f = t - i0;
        float mx = MarchMuzzle[i0].X + (MarchMuzzle[i0 + 1].X - MarchMuzzle[i0].X) * f;
        float my = MarchMuzzle[i0].Y + (MarchMuzzle[i0 + 1].Y - MarchMuzzle[i0].Y) * f;
        if (c < 0) mx = -mx;
        var m = p + new Vector2(mx, my) * scale;
        float k = 1f - age / 5f;   // 틱이 지날수록 작아짐
        float r = (2.8f + 1.6f * k) * (scale / 0.25f);
        var dir = new Vector2(c, sn);
        Raylib.DrawLineEx(m, m + dir * r * 2.6f, r * 0.7f, Palette.Alpha(new Color(255, 190, 70, 255), (int)(210 * k)));
        Raylib.DrawCircleV(m, r, Palette.Alpha(new Color(255, 170, 60, 255), (int)(230 * k)));
        Raylib.DrawCircleV(m, r * 0.5f, Palette.Alpha(new Color(255, 245, 200, 255), (int)(255 * k)));
    }

    /// <summary>
    /// 세력 f 의 병사 머리띠를 세력 색으로 보여야 하는가: 군복이 헷갈리는 교전이 있을 때만 —
    /// 살아 있는 적대 세력 중 같은 그림 변형 (같은 세력끼리 · 같은 군복을 쓰는 세력끼리, FactionArt.Variant) 이 하나라도 있으면 true.
    /// 표시 전용 (시뮬 무관). 매 프레임 세력 수² 만 본다.
    /// </summary>
    public static bool HeadbandOn(World world, int f)
    {
        if (BandOverride == "always") return true;
        if (BandOverride == "never") return false;
        if (f <= 0 || f >= world.Factions.Count || world.Factions[f].Defeated) return false;
        string? vf = FactionArt.Variant(world, f);
        for (int g = 1; g < world.Factions.Count; g++)
            if (g != f && !world.Factions[g].Defeated && world.IsHostile(f, g) && FactionArt.Variant(world, g) == vf) return true;
        return false;
    }

    static void UpdateHeadbands(World world)
    {
        int n = world.Factions.Count;
        if (bandOn.Length != n) bandOn = new bool[n];
        for (int f = 0; f < n; f++) bandOn[f] = HeadbandOn(world, f);
    }

    /// <summary>팀색 셰이더 입력 (정점 색): 머리띠를 보일 때는 세력 색, 아니면 천 색 (Palette.BandHidden) — 셰이더 · 배치는 그대로.
    /// 그림 재임포트 뒤 팀 층이 머리띠만 남으면 이 색이 곧 '머리띠 없음'.</summary>
    static Color BandTint(int owner, Color team) => (uint)owner < (uint)bandOn.Length && bandOn[owner] ? team : Palette.BandHidden;

    /// <summary>BeginMode2D 안에서 호출. 화면 밖 · 시야 밖 유닛은 건너뛴다. 아래쪽 유닛이 위쪽 유닛을 가리도록 y 순으로 그린다.</summary>
    public static void DrawUnits(World world, RtsCamera cam, int me, Entity? hover, bool showPaths)
    {
        LodAtlas.PumpAll();   // LOD 페이지 업로드 · 내리기 (프레임당 한도, 점 화면에서도 내리기는 돈다)
        var (min, max) = cam.VisibleWorld();
        min -= Vector2.One;
        max += Vector2.One;
        float ring = 1.1f / cam.Zoom;   // 선택 선: 화면 기준 약 1px (사용자 2026-10-19 — 최대한 얇게)
        var fog = world.Fog;
        long tick = world.Tick;
        float zoom = cam.Zoom;
        float dt = MathF.Min(Raylib.GetFrameTime(), 0.1f);

        UpdateHeadbands(world);

        // 1) 컬링: 화면 안 + 보이는 유닛만 (그림 정보는 종류별로 캐시)
        bool seesAll = fog.SeesAll(me);
        var grid = seesAll ? ReadOnlySpan<byte>.Empty : fog.Grid(me);
        bool allVisible = seesAll || grid.Length == 0;
        int mapW = world.Map.Width;
        var units = world.Units;
        if (cull.Length < units.Count) cull = new Item[units.Count + 256];
        int n = 0;
        UnitDef? lastDef = null; UnitArt? lastArt = null; int lastOwner = -1;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            Vector2 pos = u.Pos;
            if (pos.X < min.X || pos.X > max.X || pos.Y < min.Y || pos.Y > max.Y) continue;
            if (!allVisible && u.Owner != me && grid[(int)MathF.Floor(pos.Y) * mapW + (int)MathF.Floor(pos.X)] != 2) continue;
            if (!ReferenceEquals(u.Def, lastDef) || u.Owner != lastOwner) { lastDef = u.Def; lastOwner = u.Owner; lastArt = UnitArt.For(u.Def, FactionArt.Variant(world, u.Owner)); }
            cull[n].U = u; cull[n].Art = lastArt!; cull[n].Y = pos.Y;
            n++;
        }
        cullCount = n;

        // 2) 지난 프레임에 보이던 유닛 중 죽은 것 → 시체 (포탑 방향은 죽기 직전 값). 이번 목록을 다음 프레임용으로 복사해 둔다.
        Corpses.Begin(world);
        Corpses.Detect(world, prevCull, prevCount, turretOf);
        if (turretAngle.Count > 0)
            for (int i = 0; i < prevCount; i++)
                if (!prevCull[i].U.Alive) turretAngle.Remove(prevCull[i].U.Id);
        for (int i = 0; i < prevCount; i++)
            if (!prevCull[i].U.Alive) shownFacing.Remove(prevCull[i].U.Id);
        if (shownFacing.Count > units.Count * 2 + 1024) shownFacing.Clear();   // 화면 밖에서 죽은 유닛 기록 정리 (가끔)
        if (prevCull.Length < n) prevCull = new Item[cull.Length];
        Array.Copy(cull, prevCull, n);
        prevCount = n;

        Impacts.Scan(world, cam, me);
        if (zoom < DotZoom) { DrawDots(me, hover, zoom); SquadIcons.Draw(world, cam, me, cull.AsSpan(0, cullCount), true); return; }

        Corpses.Draw(world, cam, me, min, max);

        // 3) y 순 정렬 (계수 정렬: 화면 y 픽셀 단위 칸에 나눠 담기, O(n) · 같은 칸이면 목록 순서 유지)
        float baseY = min.Y * TS;
        int nb = (int)((max.Y - min.Y) * TS) + 2;
        if (buckets.Length < nb + 1) buckets = new int[nb + 1];
        Array.Clear(buckets, 0, nb + 1);
        for (int i = 0; i < n; i++)
        {
            int b = Math.Clamp((int)(cull[i].Y * TS - baseY), 0, nb - 1);
            buckets[b + 1]++;
        }
        for (int b = 1; b <= nb; b++) buckets[b] += buckets[b - 1];
        if (sorted.Length < n) sorted = new Item[cull.Length];
        for (int i = 0; i < n; i++)
        {
            int b = Math.Clamp((int)(cull[i].Y * TS - baseY), 0, nb - 1);
            sorted[buckets[b]++] = cull[i];
        }

        int propN = PropRenderer.Collect(world, min, max);   // F6 소품 (y 순으로 몸체 단계에서 유닛과 섞어 그린다)
        int propI = 0;
        if (propN > 0) PropRenderer.DrawShadows(zoom);

        // 4) 그림자 · 선택 선 (발밑 세력 색 원은 2026-10-19 제거 — 세력 구분은 그림 · 머리띠 · 미니맵)
        var shadowTint = Palette.Alpha(Color.Black, 80);
        float ringFade = zoom >= RingOff ? 0f : zoom <= RingFull ? 1f : (RingOff - zoom) / (RingOff - RingFull);
        int ringA = NoRing ? 0 : (int)(RingAlpha * ringFade);
        for (int i = 0; i < n; i++)
        {
            var u = sorted[i].U;
            Vector2 p = RP(u) * TS;
            float r = u.Radius * TS;
            if (sorted[i].Art.Dir)
            {
                Blob.Draw(Blob.Shadow, new Vector2(p.X, p.Y + 1), r * 0.95f, r * 0.42f, shadowTint);
            }
            else Raylib.DrawCircleV(p + new Vector2(2, 3), r, Palette.Alpha(Color.Black, 70));
            if (ringA > 0 && !u.Selected && u != hover && sorted[i].Art.Dir)
            {
                var tc = Palette.Player(u.Owner);
                Blob.Draw(Blob.Thin, new Vector2(p.X, p.Y + 1), r * 0.95f, r * 0.42f, new Color(tc.R, tc.G, tc.B, (byte)ringA));
            }
            if (u.Selected || u == hover)
            {
                float rr = r * 1.15f;
                // 교전 중 (경계 상태) 인 내 유닛은 같은 얇은 선을 주황으로 — 따로 겹쳐 그리지 않는다 (사용자 2026-10-19)
                Color c = !u.Selected ? Palette.Alpha(Color.White, 120) : u.Owner != me ? Palette.HpLow : SimView.Alerted(world, u) ? Palette.SelectCombat : Palette.Select;
                Raylib.DrawRing(p + new Vector2(0, r * 0.15f), rr, rr + ring, 0, 360, 28, c);
                if (u.Selected && World.TraverseLocked(u))   // 방열 사계: 옅은 부채꼴 (사거리 끝까지)
                {
                    float half = World.TraverseHalf(u) * (180f / MathF.PI), mid = u.DeployCenter * (180f / MathF.PI), reach = (u.Weapon?.Range ?? 0) * TS;
                    Raylib.DrawCircleSector(p, reach, mid - half, mid + half, 24, Palette.Alpha(c, 18));
                    Raylib.DrawRing(p, reach - 1.5f, reach, mid - half, mid + half, 24, Palette.Alpha(c, 90));
                }
            }
        }

        // 5) 몸체 (명령 직후 0.13초는 살짝 움찔)
        for (int i = 0; i < n; i++)
        {
            var u = sorted[i].U;
            var art = sorted[i].Art;
            Vector2 p = RP(u) * TS;
            while (propI < propN && PropRenderer.YAt(propI) <= p.Y) { TeamShader.Leave(); PropRenderer.DrawBody(propI++, zoom); }
            var team = Palette.Player(u.Owner);
            var band = BandTint(u.Owner, team);   // 사람 그림의 팀 층 = 머리띠 (헷갈리는 교전에서만 세력 색)
            float face = ShownFacing(u, art.Lod?.DirCount ?? art.Body?.DirCount ?? 5);
            if (art.Lod != null)
            {
                // 새 형식: 줌에 맞는 LOD 시트 (점 필터) + 팀색 셰이더
                if (art.Tank || art.Vehicle)
                {
                    float tf = u.Facing;
                    if (art.Tank) { if (SimView.TurretFacing(u, out tf)) turretAngle[u.Id] = tf; else tf = UpdateTurret(u, dt); }
                    art.DrawLodVehicle(u, tick, p, zoom, u.Facing, tf, team, false);
                }
                else
                {
                    LodDraw.Directional(art.Lod, p, art.Lod.Scale, art.Lod.Scale * zoom, face, art.LodFrame(u, tick, world.Winner > 0 && world.Allied(u.Owner, world.Winner), u.InTrench || world.Map.Get(GameMap.TileOf(u.Pos).X, GameMap.TileOf(u.Pos).Y) == TerrainType.Trench, world.MarchCommanded(u)), band);
                    if (zoom >= DotZoom && art.MarchFlash(u, tick, world.MarchCommanded(u))) DrawMarchFlash(p, face, art.Lod.Scale, tick - u.LastShotTick);   // 행진 사격 총구 화염 (시트에 없음, 사격 사건마다)
                }
                continue;
            }
            TeamShader.Leave();
            if (!art.Dir) { DrawBody(p, u.Radius * TS, team, u.Facing, u.Def.Key); continue; }
            if (art.Tank)
            {
                float tf;
                if (SimView.TurretFacing(u, out tf)) turretAngle[u.Id] = tf; else tf = UpdateTurret(u, dt);
                int hullCol = u.Walking ? art.Walk[(int)((tick + u.Id) / 6 % art.Walk.Length)] : art.Idle[0];
                int turCol = tick - u.LastShotTick < 6 ? art.TurretFire : art.TurretIdle;
                art.DrawTank(p, art.Body!.Scale * zoom, u.Facing, tf, hullCol, turCol, team);
            }
            else Theme.DrawDirectional(art.Body!, p, art.Body!.Scale, art.Body!.Scale * zoom, face, Column(art, u, tick), band);
        }
        TeamShader.Leave();
        while (propI < propN) PropRenderer.DrawBody(propI++, zoom);

        if (AimDebug)   // 확인용 (KD_AIMDEBUG=1): 빨강 = 표적 방위, 노랑 = 그려진 행 방향 (UnitRenderer.SnapFacing)
            for (int i = 0; i < n; i++)
            {
                var u = sorted[i].U;
                if (u.Target is not { } t || !shownFacing.TryGetValue(u.Id, out float sf)) continue;
                Vector2 p = RP(u) * TS;
                float lw = MathF.Max(1f, 1.5f / zoom);
                Raylib.DrawLineEx(p, t.Pos * TS, lw, new Color(230, 40, 40, 200));
                Raylib.DrawLineEx(p, p + new Vector2(MathF.Cos(sf), MathF.Sin(sf)) * TS * 3.5f, lw, new Color(255, 230, 40, 230));
            }

        if (SwitchLog && tick % 60 == 0) LogSwitches(n);

        // 6) 체력바 (선택 · 마우스오버 · 최근 피격) + 탄약 · 보급 마차 적재 막대 (선택 또는 부족) — 방향 시트 유닛은 머리 위
        for (int i = 0; i < n; i++)
        {
            var u = sorted[i].U;
            bool recent = tick - u.LastDamagedTick < 180;
            bool own = u.Owner == me;
            bool ammoLow = false, cargoLow = false;
            float ammo = 1, cargo = 1;
            bool hasAmmo = own && SimView.AmmoRatio(u, out ammo), hasCargo = own && SimView.CargoRatio(u, out cargo);
            if (hasAmmo) ammoLow = ammo <= 0.2f;
            if (hasCargo) cargoLow = cargo <= 0.2f;
            bool showBars = u.Selected && own || ammoLow || cargoLow;
            bool showHp = u.Selected || u == hover || recent;
            if (!showHp && !showBars && !(own && SimView.Concealing(u))) continue;
            Vector2 p = RP(u) * TS;
            float r = u.Radius * TS;
            var body = sorted[i].Art.Body;
            float top = sorted[i].Art.Dir ? p.Y - body!.Anchor.Y * body.Scale + 4 : p.Y - r;
            float w = r * 2.4f, h = MathF.Max(4f, 5f / zoom);
            if (own && SimView.Concealing(u))   // 은폐 중 표시 (장식): 발밑 점선 호
                for (int k = 0; k < 8; k++)
                {
                    float a0 = k * MathF.Tau / 8f + 0.1f;
                    Raylib.DrawLineEx(p + new Vector2(MathF.Cos(a0), MathF.Sin(a0) * 0.45f) * r * 0.95f + new Vector2(0, r * 0.2f),
                        p + new Vector2(MathF.Cos(a0 + 0.3f), MathF.Sin(a0 + 0.3f) * 0.45f) * r * 0.95f + new Vector2(0, r * 0.2f), MathF.Max(1f, 1.5f / zoom), Palette.Alpha(new Color(150, 210, 120, 255), 110));
                }
            float y = top - h - 4;
            if (showHp) DrawHpBar(new Rectangle(p.X - w / 2, y, w, h), u.Hp / u.MaxHp);
            if (showBars && (hasAmmo || hasCargo))
            {
                float bh = MathF.Max(2f, 3f / zoom);
                if (hasAmmo) { y -= bh + 1; MiniBar(new Rectangle(p.X - w / 2, y, w, bh), ammo, ammoLow ? new Color(230, 90, 60, 255) : new Color(225, 200, 90, 255)); }
                if (hasCargo) { y -= bh + 1; MiniBar(new Rectangle(p.X - w / 2, y, w, bh), cargo, cargoLow ? new Color(230, 90, 60, 255) : new Color(90, 160, 230, 255)); }
            }
        }

        // 7) 진행 가로 막대 (거치 · 방열 · 철수) — 유닛 상단, 보이는 유닛 중 해당 상태만, 표시 전용 (착검 · 칼 뽑기는 막대 없음)
        for (int i = 0; i < n; i++)
        {
            var u = sorted[i].U;
            if (!ProgressRing.TryGet(world, u, false, out float pr, out int pk)) continue;
            Vector2 p = RP(u) * TS;
            float r = u.Radius * TS;
            var body = sorted[i].Art.Body;
            float top = sorted[i].Art.Dir ? p.Y - body!.Anchor.Y * body.Scale + 4 : p.Y - r;
            float bw = r * 1.8f, bh = MathF.Max(3f, 4f / zoom);
            ProgressRing.Draw(new Rectangle(p.X - bw / 2, top - bh - 12f / zoom, bw, bh), zoom, pr, pk);
        }

        SquadIcons.Draw(world, cam, me, sorted.AsSpan(0, n), false);
        Impacts.Draw(world, cam, me, min, max);
        SmokeFx.Draw(world, me, min, max);   // 연막 (N59) — 유닛 · 폭발 위
    }

    static void MiniBar(Rectangle r, float ratio, Color c)
    {
        Raylib.DrawRectangleRec(r, Palette.Alpha(Color.Black, 170));
        Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, r.Width * Math.Clamp(ratio, 0, 1), r.Height), c);
    }

    /// <summary>
    /// 사격 · 사망 이펙트. BeginMode2D 안에서 호출. 사격선은 그리지 않는다:
    /// 쏜 쪽은 총구 섬광(방향 시트 유닛은 그림 속 섬광 프레임이 대신함), 맞은 곳은 피격 먼지, 빗나가면 흙먼지.
    /// </summary>
    public static void DrawEffects(World world, int me)
    {
        foreach (var (from, to, _, tick, hit, shooterId) in world.Shots)
        {
            float t = (world.Tick - tick) / 10f;
            if (t > 1) continue;
            int a = (int)(255 * (1 - t));
            if (world.Fog.IsVisible(me, from) && !(world.Find(shooterId) is Unit su && UnitArt.For(su.Def).Dir))
            {
                // 무기 끝 쪽 섬광 (전차 · 포탑 · 벡터 유닛)
                var dir = to - from;
                var tip = from + (dir.LengthSquared() > 1e-4f ? Vector2.Normalize(dir) * 0.6f : Vector2.Zero);
                if (t < 0.4f) Raylib.DrawCircleV(tip * TS, 4.5f * (1 - t * 2), Palette.Alpha(Palette.Tracer, a));
            }
            if (world.Fog.IsVisible(me, to) && t > 0.15f && !(world.Find(shooterId) is Unit ind && ind.Weapon is { } iw && (iw.Indirect || iw.Splash > 0)))   // 포 탄착은 Impacts 폭발이 (착탄 틱에) 대신
            {
                // 탄착: 맞으면 붉은빛 먼지, 빗나가면 흙먼지 (조금 늦게 · 퍼지며 사라짐)
                Color puff = hit ? new Color(150, 70, 50, 255) : new Color(140, 115, 80, 255);
                float pr = 2 + t * (hit ? 4 : 6);
                Raylib.DrawCircleV(to * TS, pr, Palette.Alpha(puff, a * 3 / 4));
            }
        }
        foreach (var (pos, tick, building) in world.Deaths)
        {
            if (!building || !world.Fog.IsVisible(me, pos)) continue;   // 유닛 전사는 시체(보병) · 폭발 스프라이트(차량)가 대신한다
            float t = (world.Tick - tick) / 40f;
            float r = (building ? 50 : 14) * (0.4f + t);
            Raylib.DrawCircleV(pos * TS, r, Palette.Alpha(new Color(255, 150, 60, 255), (int)(160 * (1 - t))));
            Raylib.DrawRing(pos * TS, r, r + 2, 0, 360, 24, Palette.Alpha(new Color(60, 50, 40, 255), (int)(200 * (1 - t))));
        }
    }

    static bool Visible(Unit u, Vector2 min, Vector2 max) =>
        u.Pos.X >= min.X && u.Pos.X <= max.X && u.Pos.Y >= min.Y && u.Pos.Y <= max.Y;
}
