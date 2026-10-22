using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Render;

/// <summary>
/// 건물 그리기. 이미지 테마(Theme.Current)에 항목이 있으면 스프라이트, 없으면 벡터 도형.
/// 종류별 장식은 def.Key 로 분기하고, 모르는 키는 기본 모양 + 이름 첫 글자로 그린다.
/// </summary>
public static class BuildingRenderer
{
    const float TS = Config.TileSize;
    static float curZoom = 1;     // DrawAll 의 카메라 줌 (LOD 선택용)
    static int curOwner;          // 지금 그리는 건물의 주인 (0 = 중립 → 깃발 없는 그림)

    /// <summary>BeginMode2D 안에서 호출.</summary>
    public static void DrawAll(World world, RtsCamera cam, int me, Entity? hover)
    {
        var (min, max) = cam.VisibleWorld();
        curZoom = cam.Zoom;
        BridgeRenderer.Draw(world.Map, cam, min, max);   // 다리 널빤지 · 난간 (T39d)
        RailRenderer.Draw(world.Map, cam, min, max);   // 선로 침목 · 레일 (지형 다음, 가옥 · 건물 앞)
        DrawHouses(world.Map, cam, min, max);
        foreach (var g in world.Fog.Ghosts(me))
        {
            curOwner = g.Owner;
            if (!InView(g.TileX, g.TileY, g.Def.Size, min, max)) continue;
            var r = new Rectangle(g.TileX * TS, g.TileY * TS, g.Def.Size * TS, g.Def.Size * TS);
            DrawBody(g.Def, Palette.Player(g.Owner), r, true, 1, -MathF.PI / 2, 110);
        }
        foreach (var b in world.Buildings)
        {
            if (!InView(b.TileX, b.TileY, b.Size, min, max) || !world.Fog.CanSee(me, b)) continue;
            Draw(b, cam, b == hover);
        }
    }

    static readonly string[] HouseKeys = { "building_art.house.thatch", "building_art.house.tile", "building_art.house.brick", "building_art.house.barn" };
    // 가옥 그림 고르기 (가로/세로 비율별): 정사각에 가까운 묶음 = 초가 · 벽돌 (그림 폭/높이 ≈ 1.1~1.25), 가로로 긴 묶음 = 기와 · 헛간 (≈ 1.5~1.6)
    static readonly int[] SquareKeys = { 0, 2, 0, 2, 1 }, WideKeys = { 1, 3 };

    /// <summary>테마에 가옥 그림이 있는지 (있으면 지형 타일은 가옥 칸을 마당 흙으로 굽고 가옥은 여기서 건물 단위로 그린다 — MapRenderer.SlotInfo).</summary>
    public static bool HasHouseArt(Theme? th)
    {
        if (th?.Get(HouseKeys[0])?.Lod != null) return true;
        if (!ReferenceEquals(th, artTheme)) { artTheme = th; artAny = th != null && HouseStyles.AnyArt(HouseStyles.Current, HasKey); }   // 새 양식 그림만 있는 테마도 (테마마다 한 번)
        return artAny;
    }
    static Theme? artTheme; static bool artAny;
    static bool HasKey(string key) => Theme.Current?.Get(key)?.Lod != null;

    static uint Hash(int x, int y)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return h;
    }

    /// <summary>
    /// 가옥: 'house' 칸을 건물 단위 사각형으로 묶어 (HouseBlocks) 한 채씩 그 발자국에 맞춘 집 그림 (초가 · 기와 · 벽돌 · 헛간, 비율 + 좌표 해시로 고름). 모든 줌에서 그림 (LOD 시트).
    /// 잔해 칸은 예전처럼 3칸에 1칸 폐가 (확대에서만). 테마에 그림이 없으면 아무것도 안 그림 (지형 타일 그대로).
    /// 렌더 전용 — 시뮬 상태를 읽기만 한다. 위 줄부터 아래 줄 순서로 그려 아래 집이 위 집을 덮는다.
    /// </summary>
    static void DrawHouses(GameMap map, RtsCamera cam, Vector2 min, Vector2 max)
    {
        if (Theme.Current == null) return;
        var table = HouseStyles.Current;
        string? style = HouseStyles.Resolve(table, MapFile.HouseStyleOf(map));   // null = 예전 4종 (houseStyle 생략)
        var picks = style != null ? HouseStyles.AssignCached(map, style, Theme.Current, HasKey) : null;
        foreach (var b in HouseBlocks.Of(map))
        {
            if (b.X + b.W < min.X - 1 || b.X > max.X + 1 || b.Y + b.H < min.Y - 1 || b.Y > max.Y + 2) continue;
            foreach (var p in HouseBlocks.Pieces(b))
            {
                LodSprite lod; bool mirror = false; Color tint = Color.White; bool plain = false;
                if (picks == null)
                {
                    uint h = Hash(p.X, p.Y);
                    bool wide = p.W >= p.H * 1.4f;
                    int k = wide ? WideKeys[h % (uint)WideKeys.Length] : SquareKeys[h % (uint)SquareKeys.Length];
                    if (Theme.Current.Get(HouseKeys[k])?.Lod is not { } l0) continue;
                    lod = l0;
                }
                else
                {
                    if (!picks.TryGetValue(((long)p.Y << 32) | (uint)p.X, out var pk)) continue;
                    if ((Theme.Current.Get(pk.DrawKey)?.Lod ?? Theme.Current.Get(pk.Key)?.Lod) is not { } l1) continue;
                    lod = l1; mirror = pk.Mirror; tint = ToneColor(table, pk.Tone); plain = true;
                }
                const float m = 0.06f;   // 발자국 가장자리 여백 (칸) — 이웃 칸 병사와 겹치지 않게
                var r = new Rectangle((p.X + m) * TS, (p.Y + m) * TS, (p.W - 2 * m) * TS, (p.H - 2 * m) * TS);
                LodDraw.CellFit(lod, 0, r, cam.Zoom, tint, mirror, plain);
                TeamShader.Leave();
            }
        }
        if (cam.Zoom < MapRenderer.Lod64Zoom) return;
        int x0 = Math.Max(0, (int)MathF.Floor(min.X) - 2), y0 = Math.Max(0, (int)MathF.Floor(min.Y) - 1);
        int x1 = Math.Min(map.Width - 1, (int)MathF.Ceiling(max.X) + 2), y1 = Math.Min(map.Height - 1, (int)MathF.Ceiling(max.Y) + 3);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            if (map.Get(x, y) != TerrainType.Rubble) continue;
            uint h = Hash(x, y);
            if ((h >> 8) % 3 != 0) continue;   // 잔해 칸은 3칸에 1칸만 폐가 그림 (겹쳐서 너저분해지는 것 방지)
            LodSprite? lod; bool flip = false; Color tint = Color.White; bool alt = (h >> 20 & 1) == 1;
            if (style == null) lod = Theme.Current.Get(HouseKeys[h % 4])?.Lod;
            else
            {
                if (HouseStyles.PickRuin(table, style, x, y, HasKey) is not { } rp) continue;
                lod = Theme.Current.Get(rp.Key)?.Lod; flip = rp.Mirror; tint = ToneColor(table, rp.Tone); alt = rp.AltRuin;
            }
            if (lod == null) continue;
            LodDraw.Cell(lod, 0, flip, RuinFrame(lod, alt), new Vector2((x + 0.5f) * TS, (y + 0.5f) * TS), lod.Scale, lod.Scale * cam.Zoom, tint, 255, style != null);
            TeamShader.Leave();
        }
    }

    /// <summary>폐허 칸: T39i 의 'ruined' (a) / 'ruined_b' 프레임 (없으면 예전처럼 1번 칸). 변형 비트는 좌표 해시에서.</summary>
    static int RuinFrame(LodSprite lod, bool alt)
    {
        if (alt && lod.Frame("ruined_b") is int b && b >= 0) return b;
        return lod.Frame("ruined") is int a && a >= 0 ? a : 1;
    }

    static Color ToneColor(HouseStyleTable t, int tone) =>
        (uint)tone < (uint)t.Tones.Count && t.Tones[tone].Length >= 3 ? new Color((byte)t.Tones[tone][0], (byte)t.Tones[tone][1], (byte)t.Tones[tone][2], (byte)255) : Color.White;

    static bool InView(int tx, int ty, int size, Vector2 min, Vector2 max) =>
        tx + size >= min.X && tx <= max.X && ty + size >= min.Y && ty <= max.Y;

    static void Draw(Building b, RtsCamera cam, bool hover)
    {
        var r = new Rectangle(b.TileX * TS, b.TileY * TS, b.Size * TS, b.Size * TS);
        float line = MathF.Max(1.5f, 2f / cam.Zoom);
        if (b.Selected || hover)
        {
            var sel = new Rectangle(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8);
            Raylib.DrawRectangleRoundedLinesEx(sel, 0.08f, 4, line, b.Selected ? Palette.Select : Palette.Alpha(Color.White, 120));
        }

        Color team = Palette.Player(b.Owner);
        curOwner = b.Owner;
        if (b.Def.Capturable) DrawResource(b, r, cam);
        else DrawBody(b.Def, team, r, true, 1, -MathF.PI / 2, 255);

        // 체력바
        bool showHp = !b.Indestructible && (b.Selected || hover || b.Hp < b.MaxHp);
        float bw = r.Width * 0.8f, bh = MathF.Max(5, 6 / cam.Zoom);
        float bx = r.X + (r.Width - bw) / 2, by = r.Y - bh - 6;
        if (showHp) UnitRenderer.DrawHpBar(new Rectangle(bx, by, bw, bh), b.Hp / b.MaxHp);
    }

    public static void DrawProgress(Rectangle r, float ratio, Color c)
    {
        Raylib.DrawRectangleRec(r, Palette.HpBg);
        Raylib.DrawRectangleRec(new Rectangle(r.X + 1, r.Y + 1, (r.Width - 2) * Math.Clamp(ratio, 0, 1), r.Height - 2), c);
    }

    /// <summary>건물 몸체. r 은 픽셀 사각형 (월드 또는 화면). alpha &lt; 255 면 기억된 모습(유령).</summary>
    /// <summary>[HUD] 화면 사각형 안에 맞춘 건물 그림 (LOD 시트는 칸 폭을 r 에 맞춤 — 도시 그림이 8칸이라 월드 배율로는 넘친다).</summary>
    public static void DrawPortrait(BuildingDef def, Color team, Rectangle r, float facing = -MathF.PI / 2)
    {
        uiFit = true;
        try { DrawBody(def, team, r, true, 1, facing, 255); }
        finally { uiFit = false; }
    }

    static bool uiFit;

    public static void DrawBody(BuildingDef def, Color team, Rectangle r, bool complete, float progress, float facing, int alpha)
    {
        float s = r.Width;
        float a = complete ? alpha / 255f : 0.55f;
        Color A(Color c) => Palette.Alpha(c, (int)(c.A * a));

        if (DrawSprite(def, team, r, (int)(255 * a), facing))
            return;

        Raylib.DrawRectangleRounded(new Rectangle(r.X + 3, r.Y + 4, r.Width, r.Height), 0.12f, 4, Palette.Alpha(Color.Black, (int)(80 * a)));
        var body = new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
        Raylib.DrawRectangleRounded(body, 0.12f, 4, A(Palette.Shade(team, -95)));
        var roof = new Rectangle(body.X + s * 0.08f, body.Y + s * 0.08f, body.Width - s * 0.16f, body.Height - s * 0.16f);
        Raylib.DrawRectangleRounded(roof, 0.1f, 4, A(Palette.Shade(team, -30)));

        Vector2 c = new(r.X + r.Width / 2, r.Y + r.Height / 2);
        switch (def.Key)
        {
            case "metropolis":
            case "city":
                DrawCityShape(def.Key == "metropolis", roof, team, A);
                break;
            case "village":
                DrawVillageShape(roof, team, A);
                break;
            case "supply_plant":
                // 공장: 톱니 지붕 + 굴뚝 + 상자
                for (int i = 0; i < 3; i++)
                {
                    float x0 = roof.X + i * roof.Width * 0.62f / 3;
                    Icons.Tri(new(x0, roof.Y + roof.Height * 0.7f), new(x0 + roof.Width * 0.2f, roof.Y + roof.Height * 0.7f), new(x0 + roof.Width * 0.2f, roof.Y + roof.Height * 0.2f), A(Palette.Shade(team, -60)));
                }
                Raylib.DrawCircleV(new(roof.X + roof.Width * 0.82f, roof.Y + roof.Height * 0.18f), s * 0.07f, A(new Color(70, 64, 60, 255)));
                Raylib.DrawRectangleRec(new Rectangle(roof.X + roof.Width * 0.7f, roof.Y + roof.Height * 0.5f, s * 0.12f, s * 0.12f), A(new Color(150, 118, 70, 255)));
                Raylib.DrawRectangleRec(new Rectangle(roof.X + roof.Width * 0.7f, roof.Y + roof.Height * 0.7f, s * 0.12f, s * 0.12f), A(new Color(150, 118, 70, 255)));
                break;
            case "barracks":
                for (int i = 0; i < 3; i++)
                    Raylib.DrawRectangleRec(new Rectangle(roof.X + 4, roof.Y + 6 + i * roof.Height * 0.22f, roof.Width - 8, roof.Height * 0.09f), A(Palette.Shade(team, -60)));
                Raylib.DrawRectangleRec(new Rectangle(c.X - s * 0.1f, body.Y + body.Height - s * 0.22f, s * 0.2f, s * 0.22f), A(new Color(30, 30, 34, 255)));
                break;
            case "depot":
                float q = roof.Width / 2 - 3;
                for (int i = 0; i < 4; i++)
                {
                    var crate = new Rectangle(roof.X + 2 + i % 2 * (q + 2), roof.Y + 2 + i / 2 * (q + 2), q, q);
                    Raylib.DrawRectangleRec(crate, A(new Color(150, 118, 70, 255)));
                    Raylib.DrawRectangleLinesEx(crate, 1.5f, A(new Color(90, 66, 36, 255)));
                }
                break;
            case "factory":
                // 톱니 지붕 + 굴뚝 + 차량 출입문
                for (int i = 0; i < 4; i++)
                {
                    float x0 = roof.X + i * roof.Width / 4;
                    Icons.Tri(new(x0, roof.Y + roof.Height * 0.45f), new(x0 + roof.Width / 4, roof.Y + roof.Height * 0.45f), new(x0 + roof.Width / 4, roof.Y + 4), A(Palette.Shade(team, -60)));
                }
                Raylib.DrawRectangleRec(new Rectangle(roof.X + roof.Width * 0.75f, roof.Y + 2, s * 0.09f, s * 0.2f), A(new Color(70, 64, 60, 255)));
                Raylib.DrawRectangleRec(new Rectangle(c.X - s * 0.18f, body.Y + body.Height - s * 0.2f, s * 0.36f, s * 0.2f), A(new Color(30, 30, 34, 255)));
                break;
            case "turret":
                Raylib.DrawCircleV(c, s * 0.3f, A(Palette.Shade(team, -55)));
                var dir = new Vector2(MathF.Cos(facing), MathF.Sin(facing));
                Raylib.DrawLineEx(c, c + dir * s * 0.48f, s * 0.11f, A(new Color(40, 42, 46, 255)));
                Raylib.DrawCircleV(c, s * 0.18f, A(Palette.Shade(team, 15)));
                break;
            default:
                Ui.TextCentered(def.Name.Length > 0 ? def.Name[..1] : "?", roof, 26, A(Palette.Text));
                break;
        }
        Raylib.DrawRectangleRoundedLinesEx(body, 0.12f, 4, 2, A(Palette.Shade(team, -120)));
    }

    /// <summary>이미지 테마로 건물 몸체 + 별도 회전 포신. 항목이 없으면 false -> 벡터 그림.</summary>
    static bool DrawSprite(BuildingDef def, Color team, Rectangle r, int alpha, float facing)
    {
        var sprite = Theme.Current?.Get("building." + def.Key);
        if (sprite == null) return false;
        if (sprite.Lod is { } lod)
        {
            // 새 LOD 건물 시트 (열 0 = 중립 · 열 1 = 점령: 팀색 깃발). 기준점 = 발자국 중심. scale 은 theme.json (칸 수 / 그림 발자국).
            // 칸 수가 건물 크기와 다르면 (예: 마을 3칸) 그 항목의 scale 로 이미 맞춰져 있다.
            int frame = curOwner != 0 && lod.FrameNames.Length > 1 ? 1 : 0;
            float ws = lod.Scale, ss = lod.Scale * curZoom;
            if (uiFit && lod.Levels.Length > 0 && lod.Levels[0].FrameW > 0) ws = ss = r.Width / lod.Levels[0].FrameW;   // HUD: 칸 폭 = r 폭
            LodDraw.Cell(lod, 0, false, frame, new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), ws, ss, team, alpha);
            TeamShader.Leave();
            return true;
        }
        Raylib.DrawRectangleRec(new Rectangle(r.X + 3, r.Y + 4, r.Width, r.Height), Palette.Alpha(Color.Black, alpha * 70 / 255));
        Theme.DrawRect(sprite, r, team, alpha);
        var gun = Theme.Current?.Get("building." + def.Key + ".gun");
        if (gun != null)
        {
            float scale = r.Width / sprite.Size.X;
            Theme.Draw(gun, new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), scale, facing * (180f / MathF.PI) + 90f, team, alpha);
        }
        return true;
    }

    // ---- 점령 시설: 이미지 테마가 없을 때의 종류별 벡터 모양 (DrawBody 의 switch 에서 호출) ----

    static void Flag(Vector2 pole, float h, Color team, Func<Color, Color> A)
    {
        Raylib.DrawLineEx(pole, pole - new Vector2(0, h), 2, A(Palette.Text));
        Icons.Tri(pole - new Vector2(0, h), pole - new Vector2(0, h * 0.5f), pole + new Vector2(h * 0.55f, -h * 0.75f), A(team));
    }

    /// <summary>대도시 / 소도시: 건물 블록 격자 (대도시 3x3, 소도시 2x2) + 가운데 시청(팀색) + 깃발.</summary>
    static void DrawCityShape(bool metro, Rectangle roof, Color team, Func<Color, Color> A)
    {
        int n = metro ? 3 : 2;
        float cell = roof.Width / n, pad = cell * 0.14f;
        var tones = new[] { new Color(112, 118, 128, 255), new Color(146, 112, 92, 255), new Color(170, 160, 132, 255) };
        for (int i = 0; i < n * n; i++)
        {
            var c = new Rectangle(roof.X + i % n * cell + pad, roof.Y + i / n * cell + pad, cell - 2 * pad, cell - 2 * pad);
            Raylib.DrawRectangleRec(new Rectangle(c.X + 2, c.Y + 2, c.Width, c.Height), A(Palette.Alpha(Color.Black, 110)));
            Raylib.DrawRectangleRec(c, A(tones[(i * 7 + (metro ? 1 : 0)) % 3]));
            Raylib.DrawRectangleLinesEx(c, 1, A(new Color(30, 30, 34, 255)));
        }
        int mid = n == 3 ? 4 : 0;
        var m = new Rectangle(roof.X + mid % n * cell + pad, roof.Y + mid / n * cell + pad, cell - 2 * pad, cell - 2 * pad);
        Raylib.DrawRectangleRec(m, A(team));
        Raylib.DrawRectangleLinesEx(m, 1.5f, A(Palette.Shade(team, -90)));
        Flag(new Vector2(m.X + m.Width * 0.5f, m.Y + m.Height * 0.2f), roof.Width * (metro ? 0.3f : 0.26f), team, A);
    }

    static void DrawVillageShape(Rectangle roof, Color team, Func<Color, Color> A)
    {
        float s = roof.Width;
        Raylib.DrawRectangleRec(new Rectangle(roof.X + s * 0.5f, roof.Y + s * 0.52f, s * 0.46f, s * 0.42f), A(new Color(150, 128, 70, 255)));
        for (int i = 0; i < 4; i++)
            Raylib.DrawLineEx(new(roof.X + s * 0.52f, roof.Y + s * (0.57f + i * 0.1f)), new(roof.X + s * 0.94f, roof.Y + s * (0.57f + i * 0.1f)), 2, A(new Color(112, 150, 62, 255)));
        foreach (var (hx, hy) in new[] { (0.06f, 0.08f), (0.5f, 0.12f), (0.1f, 0.54f) })
        {
            var h = new Rectangle(roof.X + s * hx, roof.Y + s * hy, s * 0.3f, s * 0.26f);
            Raylib.DrawRectangleRec(h, A(new Color(146, 70, 52, 255)));
            Raylib.DrawLineEx(new(h.X, h.Y + h.Height / 2), new(h.X + h.Width, h.Y + h.Height / 2), 1.5f, A(new Color(102, 48, 36, 255)));
        }
        Flag(new Vector2(roof.X + s * 0.86f, roof.Y + s * 0.5f), s * 0.28f, team, A);
    }

    /// <summary>점령 시설 (대도시 · 소도시 · 마을): 몸체 + 점령 반경 + 점령 게이지 / 분쟁 표시.</summary>
    static void DrawResource(Building b, Rectangle r, RtsCamera cam)
    {
        Color team = Palette.Player(b.Owner);
        Vector2 c = new(r.X + r.Width / 2, r.Y + r.Height / 2);
        DrawBody(b.Def, team, r, true, 1, -MathF.PI / 2, 255);

        float radius = (Defs.Current.Capture.Radius + b.Size / 2f) * TS;
        Raylib.DrawRing(c, radius - 1, radius, 0, 360, 64, Palette.Alpha(team, 50));
        if (b.CaptureProgress > 0 && b.CaptureProgress < 1 || b.Contested)
        {
            Color gc = b.Contested ? Palette.Warning : Palette.Player(b.CaptureFaction);
            float ring = MathF.Max(3, 4 / cam.Zoom);
            float rr = r.Width * 0.62f;
            Raylib.DrawRing(c, rr - 1, rr + ring + 1, 0, 360, 48, Palette.Alpha(Color.Black, 110));
            Raylib.DrawRing(c, rr, rr + ring, -90, -90 + 360 * b.CaptureProgress, 48, gc);
        }
        if (b.Contested && (int)(Raylib.GetTime() * 3) % 2 == 0)
        {
            // 분쟁 표시: 깜박이는 경고 삼각형
            var p = new Vector2(c.X, r.Y - 14);
            Icons.Tri(p + new Vector2(0, -12), p + new Vector2(-11, 8), p + new Vector2(11, 8), Palette.Warning);
            Raylib.DrawRectangleRec(new Rectangle(p.X - 1.5f, p.Y - 5, 3, 7), Palette.PanelDark);
            Raylib.DrawCircleV(p + new Vector2(0, 5), 1.6f, Palette.PanelDark);
        }
    }
}
