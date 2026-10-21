using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// 시작 화면의 미션 페이지: 미션 목록 (assets/missions) → 브리핑 (제목 · 날짜/장소 · 지도 미리보기 · 목표 · 패배 조건 · 병력 요약)
/// → [배치 시작] (게임이 배치 단계로 열린다) / [바로 시작]. MainMenu 가 Draw 를 부르고 Chosen 을 가져간다.
/// </summary>
public sealed class MissionMenu : IDisposable
{
    sealed record Entry(string Id, MissionDef? Def, string? Error);

    readonly List<Entry> entries = new();
    int selected = -1;
    bool briefing;
    float scroll;
    Texture2D preview;
    bool hasPreview;
    int previewW, previewH;
    string? previewId;

    public GameOptions? Chosen { get; set; }
    public bool BackRequested { get; set; }

    Vector2 mouse;
    bool click;

    public MissionMenu(string? openBriefing = null)
    {
        foreach (var id in MissionDef.Available())
        {
            try { entries.Add(new Entry(id, MissionDef.Load(id), null)); }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException) { entries.Add(new Entry(id, null, e.Message)); }
        }
        if (openBriefing != null)
        {
            selected = entries.FindIndex(e => e.Id == openBriefing && e.Def != null);
            if (selected < 0) selected = entries.FindIndex(e => e.Def != null);
            briefing = selected >= 0;
        }
    }

    /// <summary>Esc: 브리핑이면 목록으로 (true = 처리함). 목록이면 false (MainMenu 가 첫 화면으로).</summary>
    public bool Back()
    {
        if (!briefing) return false;
        briefing = false;
        return true;
    }

    bool Button(Rectangle r, string label, bool enabled = true, int size = 16)
    {
        bool hover = enabled && Ui.Contains(r, mouse);
        Raylib.DrawRectangleRounded(r, 0.2f, 4, !enabled ? Palette.PanelDark : hover ? Palette.BtnHover : Palette.Btn);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
        Ui.TextCentered(label, r, size, enabled ? Palette.Text : Palette.TextDim);
        return enabled && hover && click;
    }

    public void Draw(int screenW, int screenH)
    {
        mouse = Input.GetMousePosition();
        click = Input.IsMouseButtonPressed(MouseButton.Left);
        if (briefing && selected >= 0 && selected < entries.Count && entries[selected].Def is { } d) DrawBriefing(screenW, screenH, entries[selected].Id, d);
        else { briefing = false; DrawList(screenW, screenH); }
    }

    // ---------------------------------------------------------------- 목록

    void DrawList(int screenW, int screenH)
    {
        float pw = MathF.Min(820, screenW - 40), ph = MathF.Min(520, screenH - 70);
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(12, (screenH - ph) / 2 - 10), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 235), Palette.HudTrim);
        Ui.Text(S.MsSelectTitle, panel.X + 24, panel.Y + 16, 26, Palette.Accent);
        var list = new Rectangle(panel.X + 16, panel.Y + 62, panel.Width - 32, panel.Height - 62 - 66);
        if (entries.Count == 0) Ui.TextCentered(S.MsNone, list, 16, Palette.TextDim);
        const float rowH = 64;
        float content = entries.Count * rowH;
        if (Ui.Contains(list, mouse)) scroll -= Input.GetMouseWheelMove() * rowH;
        scroll = Math.Clamp(scroll, 0, MathF.Max(0, content - list.Height));
        Raylib.BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
        for (int i = 0; i < entries.Count; i++)
        {
            var row = new Rectangle(list.X, list.Y + i * rowH - scroll, list.Width - 8, rowH - 6);
            if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;
            var e = entries[i];
            bool hover = Ui.Contains(row, mouse) && Ui.Contains(list, mouse);
            Raylib.DrawRectangleRounded(row, 0.15f, 4, hover ? Palette.BtnHover : Palette.Btn);
            Raylib.DrawRectangleRoundedLinesEx(row, 0.15f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
            if (e.Def is { } d)
            {
                Ui.Text(d.Title, row.X + 14, row.Y + 7, 26, Palette.Text);
                Ui.Text($"{d.Date}  ·  {d.Location}", row.X + 14, row.Y + 36, 16, Palette.TextDim);
                string types = string.Join(" · ", d.Objectives.Where(o => o.Primary && !o.Hidden).Select(o => MissionText.Objective(o)));
                Ui.Text(types, row.X + row.Width - 14 - MathF.Min(360, Ui.Measure(types, 16).X), row.Y + 12, 16, Palette.Accent);
            }
            else
            {
                Ui.Text($"{e.Id}  ({S.MsBroken})", row.X + 14, row.Y + 10, 16, Palette.Warning);
                Ui.Text(e.Error ?? "", row.X + 14, row.Y + 34, 16, Palette.TextDim);
            }
            if (hover && click && e.Def != null) { selected = i; briefing = true; click = false; }
        }
        Raylib.EndScissorMode();
        if (Button(new Rectangle(panel.X + 24, panel.Y + panel.Height - 54, 140, 40), S.MsBack)) BackRequested = true;
    }

    // ---------------------------------------------------------------- 브리핑

    void EnsurePreview(string id, MissionDef d)
    {
        if (previewId == id && hasPreview) return;
        if (hasPreview) { Raylib.UnloadTexture(preview); hasPreview = false; }
        previewId = id;
        GameMap map;
        try
        {
            map = d.MapData is { ValueKind: JsonValueKind.Object } md ? MapFile.LoadText(md.GetRawText(), "preview", d.Seed) : MapFile.Load(d.Map!, d.Seed);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException) { return; }
        Image img = Raylib.GenImageColor(map.Width, map.Height, Color.Black);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var tc = Terrains.Get(map.Get(x, y)).Color;
            if (map.IsHigh(x, y)) tc = new Color((byte)Math.Min(255, tc.R + 34), (byte)Math.Min(255, tc.G + 30), (byte)Math.Min(255, tc.B + 10), (byte)255);
            Raylib.ImageDrawPixel(ref img, x, y, tc);
        }
        preview = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(preview, TextureFilter.Point);
        previewW = map.Width; previewH = map.Height;
        hasPreview = true;
    }

    static List<string> Wrap(string text, float width, float size)
    {
        var lines = new List<string>();
        foreach (var para in text.Split('\n'))
        {
            string cur = "";
            foreach (var word in para.Split(' '))
            {
                string next = cur.Length == 0 ? word : cur + " " + word;
                if (cur.Length > 0 && Ui.Measure(next, size).X > width) { lines.Add(cur); cur = word; }
                else cur = next;
            }
            lines.Add(cur);
        }
        return lines;
    }

    static Color FactionColor(MissionDef d, int f) =>
        f >= 1 && f <= d.Factions.Count && d.Factions[f - 1].Color is { Length: 3 } c ? new Color(c[0], c[1], c[2], 255) : Palette.Player(f);

    void DrawBriefing(int screenW, int screenH, string id, MissionDef d)
    {
        float pw = MathF.Min(1180, screenW - 24), ph = MathF.Min(660, screenH - 50);
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(8, (screenH - ph) / 2 - 12), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 240), Palette.HudTrim);
        float lx = panel.X + 22, y = panel.Y + 14;
        Ui.Text(d.Title, lx, y, 26, Palette.Accent);
        y += 32;
        Ui.Text($"{d.Date}  ·  {d.Location}", lx, y, 16, Palette.Text);
        y += 20;
        if (d.Weather != null || d.Time != null) { Ui.Text(string.Format(S.MsWeatherTime, d.Weather ?? "-", d.Time ?? "-"), lx, y, 16, Palette.TextDim); y += 20; }

        // 오른쪽: 지도 미리보기
        float mapW = MathF.Min(pw * 0.44f, 520), mapAreaH = ph - 130;
        var mapArea = new Rectangle(panel.X + pw - mapW - 22, panel.Y + 16, mapW, mapAreaH);
        EnsurePreview(id, d);
        if (hasPreview)
        {
            float s = MathF.Min(mapArea.Width / previewW, mapArea.Height / previewH);
            var dst = new Rectangle(mapArea.X + (mapArea.Width - previewW * s) / 2, mapArea.Y, previewW * s, previewH * s);
            Raylib.DrawTexturePro(preview, new Rectangle(0, 0, previewW, previewH), dst, Vector2.Zero, 0, Color.White);
            Raylib.DrawRectangleLinesEx(dst, 1, Palette.PanelBorder);
            Vector2 P(float x, float yy) => new(dst.X + x * s, dst.Y + yy * s);
            void Area(MissionArea a, Color c)
            {
                if (a.Rect is { Length: 4 } r) Raylib.DrawRectangleLinesEx(new Rectangle(P(r[0], r[1]).X, P(r[0], r[1]).Y, r[2] * s, r[3] * s), 2, c);
                else if (a.Circle is { Length: 3 } ci) Raylib.DrawCircleLinesV(P(ci[0], ci[1]), ci[2] * s, c);
            }
            foreach (var z in d.DeployZones) Area(z, new Color(90, 160, 255, 230));
            foreach (var o in d.Objectives) if (!o.Hidden && d.Area(o.Area) is { } a) Area(a, Palette.Accent);
            foreach (var f in d.Forces)
            {
                if (f.At is not { Length: 2 } at) continue;
                int n = f.AllCompanies().Sum(c => c.Units.Values.Sum());
                float r = MathF.Max(3, MathF.Min(9, MathF.Sqrt(n) * 0.8f));
                Raylib.DrawCircleV(P(at[0], at[1]), r, FactionColor(d, f.Faction));
                Raylib.DrawCircleLinesV(P(at[0], at[1]), r, Palette.PanelDark);
            }
            Ui.Text(S.MsLegend, dst.X, dst.Y + dst.Height + 6, 16, Palette.TextDim);
        }

        // 왼쪽: 브리핑 글 · 목표 · 패배 조건 · 병력
        float textW = mapArea.X - lx - 20;
        y += 6;
        foreach (var line in Wrap(d.Briefing, textW, 16)) { Ui.Text(line, lx, y, 16, Palette.Text); y += 20; }
        y += 10;
        Ui.Text(S.MsObjectives, lx, y, 16, Palette.Accent); y += 22;
        bool hidden = false;
        foreach (var o in d.Objectives)
        {
            if (o.Hidden) { hidden = true; continue; }
            Ui.Text($"{(o.Primary ? S.MsPrimary : S.MsSecondary)}  ·  {MissionText.Objective(o)}", lx + 10, y, 16, o.Primary ? Palette.Text : Palette.TextDim);
            y += 20;
        }
        if (hidden) { Ui.Text(S.MsHiddenObj, lx + 10, y, 16, Palette.TextDim); y += 20; }
        if (d.Failures.Count > 0)
        {
            y += 6;
            Ui.Text(S.MsFailures, lx, y, 16, Palette.Accent); y += 22;
            foreach (var f in d.Failures) { Ui.Text(MissionText.Failure(f), lx + 10, y, 16, Palette.Warning); y += 20; }
        }
        y += 6;
        Ui.Text(S.MsForces, lx, y, 16, Palette.Accent); y += 22;
        float colW = textW / 2;
        float y0 = y;
        Ui.Text(S.MsOwnForces, lx + 10, y, 16, FactionColor(d, d.Player)); y += 20;
        foreach (var (u, n) in MissionText.Composition(d.Forces.Where(f => f.Faction == d.Player)))
        {
            if (y > panel.Y + ph - 70) break;
            Ui.Text($"{MissionText.UnitName(u)}  {string.Format(S.MsUnitsCount, n)}", lx + 20, y, 16, Palette.Text); y += 18;
        }
        float ey = y0;
        int enemy = d.Factions.Count > 1 ? (d.Player == 1 ? 2 : 1) : d.Player;
        Ui.Text(S.MsEnemyForces, lx + colW, ey, 16, FactionColor(d, enemy)); ey += 20;
        int myTeam = d.Factions[d.Player - 1].Team;
        bool Hostile(MissionForce f) => f.Faction != d.Player && f.Faction >= 1 && f.Faction <= d.Factions.Count && (myTeam == 0 || d.Factions[f.Faction - 1].Team != myTeam);
        foreach (var (u, n) in MissionText.Composition(d.Forces.Where(Hostile)))
        {
            if (ey > panel.Y + ph - 70) break;
            Ui.Text($"{MissionText.UnitName(u)}  {string.Format(S.MsUnitsCount, n)}", lx + colW + 10, ey, 16, Palette.TextDim); ey += 18;
        }

        float by = panel.Y + ph - 54;
        if (Button(new Rectangle(panel.X + 22, by, 140, 40), S.MsBack)) briefing = false;
        if (Button(new Rectangle(panel.X + pw - 22 - 200, by, 200, 40), S.MsDeployStart, size: 26))
            Chosen = new GameOptions { MissionId = id, FromMenu = true };
        if (Button(new Rectangle(panel.X + pw - 22 - 200 - 12 - 160, by, 160, 40), S.MsQuickStart))
            Chosen = new GameOptions { MissionId = id, FromMenu = true, MissionSkipDeploy = true };
    }

    public void Dispose()
    {
        if (hasPreview) { Raylib.UnloadTexture(preview); hasPreview = false; }
    }
}
