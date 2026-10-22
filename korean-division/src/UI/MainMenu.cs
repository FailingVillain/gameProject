using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// 시작 화면: [플레이] [관전 (AI끼리)] [리플레이 보기] [종료].
/// 플레이 · 관전은 설정 패널(맵 · 인원 · AI · 시드 · 안개)을 거쳐 시작하고, 리플레이는 목록에서 고른다.
/// Program 이 Frame() 을 매 프레임 부르고, Chosen 이 채워지면 그 옵션으로 게임을 만든다.
/// </summary>
public sealed class MainMenu
{
    enum Page { Main, Setup, Replays, Battle, Load, Missions, Campaign, Settings, Codex }
    CodexMenu? codex;
    CampaignMenu? campaign;
    MissionMenu? missions;   // 미션 선택 · 브리핑 (MissionMenu.cs)

    sealed record FixedMap(string Id, int Players, IReadOnlyList<FactionDef> Factions);

    const float RowH = 46;

    Page page = Page.Main;
    bool spectate;

    // 설정
    readonly List<FixedMap> fixedMaps = new();
    int mapIndex = -1;            // -1 = 생성 맵, 그 외 fixedMaps 인덱스 (기본: corners4)
    int faction = 1;              // 내가 조작할 세력 (1부터)
    int players = Config.DefaultPlayers;
    int mapTier = -1;             // 생성 맵 크기 단계 (Config.MapTiers 인덱스), -1 = 자동 (인원 기준 Config.MapTierFor)
    readonly string[] aiKeys = AiRegistry.All.Keys.Where(k => k != "battle" && k != "mission").ToArray();   // battle AI 는 교전 테스트 전용
    int aiIndex;
    int? seed;                    // null = 시작할 때 무작위
    bool fog = true;

    // 리플레이 목록
    List<string>? replayFiles;
    readonly Dictionary<string, string> replayInfo = new();
    float replayScroll;

    /// <summary>시작할 게임 (설정 완료 시). Program 이 읽고 비운다.</summary>
    public GameOptions? Chosen { get; set; }
    public bool ExitRequested { get; internal set; }

    Vector2 mouse;
    bool click;
    int screenW, screenH;

    public MainMenu(string? initialPage = null)
    {
        foreach (var id in MapFile.Available().Where(id => !id.StartsWith("battle_")))   // 교전 테스트 지도는 맵 목록에서 뺀다
            if (MapFile.Peek(id) is { Starts.Count: > 0 } mf) fixedMaps.Add(new FixedMap(id, mf.Starts.Count, mf.Factions));
        mapIndex = fixedMaps.FindIndex(m => m.Id == DefaultMapId);   // 기본 4인 맵 (없으면 생성 맵 = -1)
        aiIndex = Math.Max(0, Array.IndexOf(aiKeys, AiRegistry.Default));
        switch (initialPage)
        {
            case "setup": page = Page.Setup; break;
            case "setup-gen": page = Page.Setup; mapIndex = -1; break;   // 스크린샷용: 생성 맵 (지도 크기 줄)
            case "spectate": page = Page.Setup; spectate = true; break;
            case "replays": OpenReplays(); break;
            case "battle": page = Page.Battle; break;
            case "settings": page = Page.Settings; break;
            case "codex": page = Page.Codex; codex = new CodexMenu(); break;
            case "load": OpenLoad(); break;
            case "missions": page = Page.Missions; missions = new MissionMenu(); break;
            case "campaign": page = Page.Campaign; campaign = new CampaignMenu(); break;
            case "briefing": page = Page.Missions; missions = new MissionMenu(Environment.GetEnvironmentVariable("KD_MENU_MISSION") ?? ""); break;   // 스크린샷용 (첫 미션 또는 KD_MENU_MISSION)
        }
    }

    public const string DefaultMapId = "corners4";

    int FactionCount => mapIndex >= 0 ? fixedMaps[mapIndex].Players : players;

    string FactionName(int f) =>
        mapIndex >= 0 && f - 1 < fixedMaps[mapIndex].Factions.Count && !string.IsNullOrWhiteSpace(fixedMaps[mapIndex].Factions[f - 1].Name)
            ? fixedMaps[mapIndex].Factions[f - 1].Name
            : string.Format(S.FactionLabel, f);

    Color FactionColor(int f) =>
        mapIndex >= 0 && f - 1 < fixedMaps[mapIndex].Factions.Count && fixedMaps[mapIndex].Factions[f - 1].Color is { Length: 3 } c
            ? new Color(c[0], c[1], c[2], 255)
            : Palette.Player(f);

    /// <summary>게임을 마치고 돌아왔을 때: 첫 화면으로.</summary>
    public void ShowMain(string? notice = null) { page = Page.Main; startNotice = notice; }
    /// <summary>캠페인 미션을 끝내고 돌아올 때: 캠페인 홈을 새로 읽어 (전과 보고 → 보충) 연다.</summary>
    public CampaignMenu? FuzzCampaign => page == Page.Campaign ? campaign : null;   // T38h 퍼즈 관찰용
    public void ShowCampaign() { page = Page.Campaign; campaign = new CampaignMenu(); }
    string? startNotice;   // 시작 실패 안내 (옛 형식 리플레이 등) — 첫 화면에 한 번 표시, 클릭하면 사라짐

    // ---------------------------------------------------------------- 프레임

    public void Frame(string? screenshotPath = null)
    {
        screenW = Raylib.GetScreenWidth();
        screenH = Raylib.GetScreenHeight();
        mouse = Input.GetMousePosition();
        click = Input.IsMouseButtonPressed(MouseButton.Left);

        if (Input.IsKeyPressed(KeyboardKey.Escape))
        {
            if (page == Page.Main) ExitRequested = true;
            else if (page == Page.Missions && missions?.Back() == true) { }   // 브리핑 → 미션 목록
            else page = Page.Main;
        }

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Palette.Void);
        DrawBackdrop();
        switch (page)
        {
            case Page.Main: DrawMain(); break;
            case Page.Setup: DrawSetup(); break;
            case Page.Replays: DrawReplays(); break;
            case Page.Battle: DrawBattle(); break;
            case Page.Settings: DrawSettings(); break;
            case Page.Load: DrawLoad(); break;
            case Page.Codex:
                codex ??= new CodexMenu();
                codex.Draw(screenW, screenH);
                if (codex.BackRequested) { codex.BackRequested = false; page = Page.Main; }
                break;
            case Page.Campaign:
                campaign ??= new CampaignMenu();
                campaign.Draw(screenW, screenH);
                if (campaign.OpenMission != null) { missions?.Dispose(); missions = new MissionMenu(campaign.OpenMission); campaign.OpenMission = null; page = Page.Missions; }
                else if (campaign.BackRequested) { campaign.BackRequested = false; page = Page.Main; }
                break;
            case Page.Missions:
                missions ??= new MissionMenu();
                missions.Draw(screenW, screenH);
                if (missions.Chosen != null) { Chosen = missions.Chosen; missions.Chosen = null; }
                if (missions.BackRequested) { missions.BackRequested = false; page = Page.Main; }
                break;
        }
        Ui.TextCentered(S.MenuHint, new Rectangle(0, screenH - 30, screenW, 20), 16, Palette.TextDim);
        if (page == Page.Main && startNotice != null)
        {
            string nm = startNotice;
            while (nm.Length > 4 && Ui.Measure(nm, 16).X > screenW - 40) nm = nm[..^4] + "...";
            Ui.TextCentered(nm, new Rectangle(0, screenH - 64, screenW, 20), 16, Palette.Warning);
            if (click) startNotice = null;
        }

        if (screenshotPath != null)
        {
            Rlgl.DrawRenderBatchActive();
            Image img = Raylib.LoadImageFromScreen();
            Raylib.ExportImage(img, screenshotPath);
            Raylib.UnloadImage(img);
        }
        Raylib.EndDrawing();
    }

    void DrawBackdrop()
    {
        Raylib.DrawRectangleGradientV(0, 0, screenW, screenH, new Color(22, 26, 34, 255), Palette.Void);
        if (page == Page.Main) Raylib.DrawRectangleRec(new Rectangle(0, screenH * 0.30f, screenW, 2), Palette.Alpha(Palette.HudTrim, 120));
    }

    void DrawTitle(float y)
    {
        Vector2 m = Ui.Measure(S.GameTitle, 72);
        Ui.Text(S.GameTitle, (screenW - m.X) / 2, y, 72, Palette.Accent);
    }

    bool Button(Rectangle r, string label, bool enabled = true, bool active = false, int size = 16)
    {
        Ui.RegHit(r, label, enabled);
        bool hover = enabled && Ui.Contains(r, mouse);
        Color fill = !enabled ? Palette.PanelDark : active ? Palette.BtnActive : hover ? Palette.BtnHover : Palette.Btn;
        Raylib.DrawRectangleRounded(r, 0.2f, 4, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
        Ui.TextCentered(label, r, size, enabled ? Palette.Text : Palette.TextDim);
        return enabled && hover && click;
    }

    // ---------------------------------------------------------------- 메인

    void DrawMain()
    {
        DrawTitle(screenH * 0.30f - 120);
        const float w = 320, h = 46, gap = 10;
        float x = (screenW - w) / 2, y = screenH * 0.30f + 30;
        if (Button(new Rectangle(x, y, w, h), S.MenuCampaign, size: 26)) { page = Page.Campaign; campaign = new CampaignMenu(); }
        y += h + gap;
        if (Button(new Rectangle(x, y, w, h), S.MenuMissions, size: 26)) { page = Page.Missions; missions ??= new MissionMenu(); }
        y += h + gap;
        if (Button(new Rectangle(x, y, w, h), S.MenuPlay, size: 26)) { page = Page.Setup; spectate = false; }
        y += h + gap;
        if (Button(new Rectangle(x, y, w, h), S.MenuSpectate, size: 26)) { page = Page.Setup; spectate = true; }
        y += h + gap;
        float lw = (w - gap) / 2;   // 불러오기 · 리플레이 한 줄 (미션 버튼 자리)
        if (Button(new Rectangle(x, y, lw, h), S.MenuLoad, size: 24)) OpenLoad();
        if (Button(new Rectangle(x + lw + gap, y, lw, h), S.MenuReplays, size: 24)) OpenReplays();
        y += h + gap;
        if (Button(new Rectangle(x, y, lw, h), S.MenuBattle, size: 24)) page = Page.Battle;
        if (Button(new Rectangle(x + lw + gap, y, lw, h), S.MenuCodex, size: 24)) { page = Page.Codex; codex ??= new CodexMenu(); }
        y += h + gap;
        // 빠른 교전 두 가지: 200 대 200 (가볍게) · 500 대 500 — 한 줄에 반씩
        float hw = (w - gap) / 2;
        if (Button(new Rectangle(x, y, hw, h), S.MenuQuickBattle200, size: 24)) Chosen = QuickBattleOptions(200);
        if (Ui.Contains(new Rectangle(x, y, hw, h), mouse)) Ui.Text(S.MenuQuickBattle200Tip, x + (w - Ui.Measure(S.MenuQuickBattle200Tip, 16).X) / 2, screenH - 40, 16, Palette.TextDim);
        if (Button(new Rectangle(x + hw + gap, y, hw, h), S.MenuQuickBattle, size: 24)) Chosen = QuickBattleOptions();
        if (Ui.Contains(new Rectangle(x + hw + gap, y, hw, h), mouse)) Ui.Text(S.MenuQuickBattleTip, x + (w - Ui.Measure(S.MenuQuickBattleTip, 16).X) / 2, screenH - 40, 16, Palette.TextDim);
        y += h + gap;
        if (Button(new Rectangle(x, y, lw, h), S.MenuSettings, size: 24)) page = Page.Settings;
        if (Button(new Rectangle(x + lw + gap, y, lw, h), S.Quit, size: 24)) ExitRequested = true;
    }

    // ---------------------------------------------------------------- 환경 설정 (UserSettings)

    void DrawSettings()
    {
        float pw = MathF.Min(760, screenW - 40), ph = 424;
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(16, (screenH - ph) / 2 - 10), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 235), Palette.HudTrim);
        Ui.Text(S.MenuSettings, panel.X + 24, panel.Y + 16, 26, Palette.Accent);
        float lx = panel.X + 24, cx = panel.X + 170, y = panel.Y + 70;
        bool windowed = UserSettings.Mode == UserSettings.WindowMode.Windowed;

        Ui.Text(S.SetResolution, lx, y + 6, 16, Palette.TextDim);
        float x = cx;
        foreach (var (rw, rh) in UserSettings.Resolutions)
        {
            string lab = $"{rw}x{rh}";
            float bw = Ui.Measure(lab, 16).X + 28;
            if (Button(new Rectangle(x, y, bw, 32), lab, windowed, windowed && UserSettings.Width == rw && UserSettings.Height == rh))
            { UserSettings.Width = rw; UserSettings.Height = rh; UserSettings.Apply(); UserSettings.Save(); }
            x += bw + 8;
        }
        y += 38;
        if (!windowed) Ui.Text(S.SetResolutionNote, cx, y, 16, Palette.TextDim);
        y += RowH - 38 + 16;

        Ui.Text(S.SetWindowMode, lx, y + 6, 16, Palette.TextDim);
        var modes = new[] { (UserSettings.WindowMode.Windowed, S.SetModeWindowed), (UserSettings.WindowMode.Borderless, S.SetModeBorderless), (UserSettings.WindowMode.Fullscreen, S.SetModeFullscreen) };
        x = cx;
        foreach (var (m, lab) in modes)
        {
            if (Button(new Rectangle(x, y, 120, 32), lab, true, UserSettings.Mode == m))
            { UserSettings.Mode = m; UserSettings.Apply(); UserSettings.Save(); }
            x += 128;
        }
        y += RowH + 16;

        Ui.Text(S.SetVolume, lx, y + 6, 16, Palette.TextDim);
        if (Button(new Rectangle(cx, y, 36, 32), "-")) { UserSettings.SetVolume(UserSettings.Volume - 0.1f); UserSettings.Save(); }
        Ui.TextCentered($"{(int)MathF.Round(UserSettings.Volume * 100)}%", new Rectangle(cx + 40, y, 80, 32), 16, Palette.Text);
        if (Button(new Rectangle(cx + 124, y, 36, 32), "+")) { UserSettings.SetVolume(UserSettings.Volume + 0.1f); UserSettings.Save(); }
        if (Button(new Rectangle(cx + 180, y, 100, 32), S.SetMute, true, Sfx.Muted)) Sfx.Muted = !Sfx.Muted;
        y += RowH + 16;

        Ui.Text(S.SetColorBlind, lx, y + 6, 16, Palette.TextDim);
        if (Button(new Rectangle(cx, y, 120, 32), UserSettings.ColorBlind ? S.SetColorBlindOn : S.SetColorBlindOff, true, UserSettings.ColorBlind))
        { UserSettings.ColorBlind = !UserSettings.ColorBlind; UserSettings.Save(); }
        Ui.Text(S.SetColorBlindNote, cx + 132, y + 8, 14, Palette.TextDim);

        Ui.Text(S.SetFileNote, lx, panel.Y + panel.Height - 96, 16, Palette.TextDim);
        if (Button(new Rectangle(panel.X + 24, panel.Y + panel.Height - 62, 140, 40), S.MenuBack)) page = Page.Main;
    }

    // ---------------------------------------------------------------- 설정

    void DrawSetup()
    {
        string title = spectate ? S.MenuSetupSpectate : S.MenuSetupPlay;
        float pw = MathF.Min(760, screenW - 40), ph = spectate ? 430 + RowH : 478 + RowH;
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(16, (screenH - ph) / 2 - 10), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 235), Palette.HudTrim);
        Ui.Text(title, panel.X + 24, panel.Y + 16, 26, Palette.Accent);

        float lx = panel.X + 24, cx = panel.X + 170, y = panel.Y + 70;
        bool fixedMap = mapIndex >= 0;

        // 맵 선택 칩
        Ui.Text(S.MenuMap, lx, y + 6, 16, Palette.TextDim);
        float x = cx;
        var labels = new List<string> { S.MenuGenerated };
        labels.AddRange(fixedMaps.Select(m => $"{m.Id}  {string.Format(S.MenuMapPlayers, m.Players)}"));
        float rowY = y;
        for (int i = 0; i < labels.Count; i++)
        {
            float bw = Ui.Measure(labels[i], 16).X + 28;
            if (x + bw > panel.X + panel.Width - 24) { x = cx; rowY += 38; }
            if (Button(new Rectangle(x, rowY, bw, 32), labels[i], true, mapIndex == i - 1)) mapIndex = i - 1;
            x += bw + 8;
        }
        y = rowY + 52;
        fixedMap = mapIndex >= 0;

        // 인원
        Ui.Text(S.MenuPlayers, lx, y + 6, 16, Palette.TextDim);
        if (fixedMap) Ui.Text($"{fixedMaps[mapIndex].Players}   ·   {S.MenuFixedNote}", cx, y + 6, 16, Palette.TextDim);
        else
        {
            if (Button(new Rectangle(cx, y, 36, 32), "-")) players = Math.Max(2, players - (Input.IsKeyDown(KeyboardKey.LeftShift) ? 4 : 1));
            Ui.TextCentered(players.ToString(), new Rectangle(cx + 40, y, 60, 32), 16, Palette.Text);
            if (Button(new Rectangle(cx + 104, y, 36, 32), "+")) players = Math.Min(Config.MaxPlayers, players + (Input.IsKeyDown(KeyboardKey.LeftShift) ? 4 : 1));
        }
        y += RowH;

        // 지도 크기 (생성 맵만): 자동 (인원 기준) · 초소형 200 · 소형 400 · 중형 800 · 대형 1200 · 초대형 1500
        Ui.Text(S.MenuMapSize, lx, y + 6, 16, Palette.TextDim);
        if (fixedMap) Ui.Text(S.MenuMapSizeFixed, cx, y + 6, 16, Palette.TextDim);
        else
        {
            float sx = cx;
            for (int i = -1; i < Config.MapTiers.Length; i++)
            {
                string lab = i < 0 ? string.Format(S.MenuMapSizeAuto, Config.MapTierFor(players)) : $"{S.MapTierNames[i]} {Config.MapTiers[i]}";
                float bw = Ui.Measure(lab, 14).X + 16;
                if (sx + bw > panel.X + panel.Width - 16) break;
                if (Button(new Rectangle(sx, y, bw, 32), lab, true, mapTier == i, 14)) mapTier = i;
                sx += bw + 4;
            }
        }
        y += RowH;

        // 세력 선택 (플레이할 때만): 지도가 세력 이름을 정의하면 그 이름, 아니면 "세력 N"
        if (!spectate)
        {
            faction = Math.Clamp(faction, 1, FactionCount);
            Ui.Text(S.MenuFaction, lx, y + 6, 16, Palette.TextDim);
            if (Button(new Rectangle(cx, y, 36, 32), "<")) faction = faction <= 1 ? FactionCount : faction - 1;
            var box = new Rectangle(cx + 40, y, 200, 32);
            Raylib.DrawRectangleRec(new Rectangle(box.X + 6, box.Y + 8, 16, 16), FactionColor(faction));
            Ui.TextCentered(FactionName(faction), box, 16, Palette.Text);
            if (Button(new Rectangle(cx + 244, y, 36, 32), ">")) faction = faction >= FactionCount ? 1 : faction + 1;
            y += RowH;
        }

        // AI 종류 (클릭하면 다음)
        Ui.Text(S.MenuAi, lx, y + 6, 16, Palette.TextDim);
        if (Button(new Rectangle(cx, y, 160, 32), aiKeys[aiIndex])) aiIndex = (aiIndex + 1) % aiKeys.Length;
        y += RowH;

        // 시드
        Ui.Text(S.MenuSeed, lx, y + 6, 16, Palette.TextDim);
        if (Button(new Rectangle(cx, y, 36, 32), "-")) seed = (seed ?? Config.DefaultSeed) - 1;
        Ui.TextCentered(seed?.ToString() ?? S.MenuRandom, new Rectangle(cx + 40, y, 100, 32), 16, seed == null ? Palette.TextDim : Palette.Text);
        if (Button(new Rectangle(cx + 144, y, 36, 32), "+")) seed = (seed ?? Config.DefaultSeed) + 1;
        if (Button(new Rectangle(cx + 192, y, 90, 32), S.MenuRandom, true, seed == null)) seed = seed == null ? Config.DefaultSeed : null;
        y += RowH;

        // 안개
        Ui.Text(S.MenuFog, lx, y + 6, 16, Palette.TextDim);
        if (Button(new Rectangle(cx, y, 100, 32), fog ? S.On : S.Off, true, fog)) fog = !fog;

        // 하단 버튼
        float by = panel.Y + panel.Height - 62;
        if (Button(new Rectangle(panel.X + 24, by, 140, 40), S.MenuBack)) page = Page.Main;
        if (Button(new Rectangle(panel.X + panel.Width - 24 - 200, by, 200, 40), S.MenuStart, true, true, 26)) StartGame();
    }

    void StartGame()
    {
        var opt = new GameOptions { Spectate = spectate, FromMenu = true, Faction = Math.Clamp(faction, 1, FactionCount), Ai = aiKeys[aiIndex], NoFog = !fog };
        opt.Seed = seed ?? Random.Shared.Next(1, 1_000_000);
        if (mapIndex >= 0)
        {
            opt.MapId = fixedMaps[mapIndex].Id;
            opt.Players = fixedMaps[mapIndex].Players;
        }
        else
        {
            opt.Players = players;
            opt.MapWidth = opt.MapHeight = mapTier >= 0 ? Config.MapTiers[mapTier] : Config.MapTierFor(players);
        }
        Chosen = opt;
    }

    // ---------------------------------------------------------------- 교전 테스트

    readonly string[] unitKeys = Defs.Current.Units.Keys.ToArray();
    int[][]? battleCount;
    int battleMapIndex, battleDistance = 60;
    bool battleFog, battleAiVsAi;

    int[][] BattleCounts()
    {
        if (battleCount != null) return battleCount;
        battleCount = new[] { new int[unitKeys.Length], new int[unitKeys.Length] };
        ApplyPreset(1);
        return battleCount;
    }

    int BattleTotal(int side) => BattleCounts()[side].Sum();

    void AdjustBattle(int side, int unit, int delta)
    {
        var c = BattleCounts()[side];
        int room = BattleSetup.MaxPerSide - BattleTotal(side);
        c[unit] = Math.Clamp(c[unit] + delta, 0, c[unit] + Math.Max(0, room));
    }

    /// <summary>프리셋 (없는 종류는 무시): 0 보병만 · 1 혼합 · 2 보병 대 전차 · 3 최대 2000 대 2000.</summary>
    void ApplyPreset(int p)
    {
        var c = BattleCounts();
        void Set(int side, params (string Key, int N)[] list)
        {
            Array.Clear(c[side]);
            foreach (var (k, n) in list) { int i = Array.IndexOf(unitKeys, k); if (i >= 0) c[side][i] = n; }
        }
        switch (p)
        {
            case 0: Set(0, ("infantry", 500)); Set(1, ("infantry", 500)); break;
            case 1: Set(0, ("recruit", 200), ("infantry", 200), ("veteran", 80), ("heavy_at_rifleman", 40), ("tank_ft_male", 20));
                    Set(1, ("recruit", 200), ("infantry", 200), ("veteran", 80), ("heavy_at_rifleman", 40), ("tank_ft_male", 20)); break;
            case 2: Set(0, ("infantry", 700), ("heavy_at_rifleman", 150)); Set(1, ("tank_ft_male", 150), ("infantry", 150)); break;
            default: Set(0, ("recruit", 800), ("infantry", 700), ("veteran", 300), ("heavy_at_rifleman", 200));
                     Set(1, ("recruit", 800), ("infantry", 700), ("veteran", 300), ("tank_ft_male", 200)); break;
        }
    }

    void DrawBattle()
    {
        var counts = BattleCounts();
        float pw = MathF.Min(920, screenW - 24), ph = MathF.Min(screenH - 24, 60 + 46 + 28 + unitKeys.Length * 34 + 2 * 44 + 70);
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(8, (screenH - ph) / 2 - 8), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 235), Palette.HudTrim);
        Ui.Text(S.BattleSetupTitle, panel.X + 24, panel.Y + 14, 26, Palette.Accent);
        Ui.Text(S.BattleCap, panel.X + panel.Width - 24 - Ui.Measure(S.BattleCap, 16).X, panel.Y + 22, 16, Palette.TextDim);

        float y = panel.Y + 56;
        // 프리셋
        Ui.Text(S.BattlePresets, panel.X + 24, y + 6, 16, Palette.TextDim);
        float px = panel.X + 110;
        foreach (var (label, idx) in new[] { (S.BattlePresetInf, 0), (S.BattlePresetMixed, 1), (S.BattlePresetInfTank, 2), (S.BattlePresetMax, 3) })
        {
            float bw = Ui.Measure(label, 16).X + 28;
            if (Button(new Rectangle(px, y, bw, 32), label)) ApplyPreset(idx);
            px += bw + 8;
        }
        {
            float bw = Ui.Measure(S.BattleClear, 16).X + 28;
            if (Button(new Rectangle(px, y, bw, 32), S.BattleClear)) { Array.Clear(counts[0]); Array.Clear(counts[1]); }
        }
        y += 46;

        // 머리말: 세력 이름 · 합계
        float sideW = 262, leftX = panel.X + 20, rightX = panel.X + pw - 20 - sideW, midX = leftX + sideW;
        string leftName = battleAiVsAi ? S.BattleSideAi : S.BattleSideYou, rightName = battleAiVsAi ? S.BattleSideAi : S.BattleSideFoe;
        Raylib.DrawRectangle((int)leftX, (int)y + 4, 12, 12, Palette.Player(1));
        Ui.Text($"{leftName}   {string.Format(S.BattleTotal, BattleTotal(0), BattleSetup.MaxPerSide)}", leftX + 18, y, 16, Palette.Text);
        Raylib.DrawRectangle((int)rightX, (int)y + 4, 12, 12, Palette.Player(2));
        Ui.Text($"{rightName}   {string.Format(S.BattleTotal, BattleTotal(1), BattleSetup.MaxPerSide)}", rightX + 18, y, 16, Palette.Text);
        y += 28;

        for (int i = 0; i < unitKeys.Length; i++, y += 34)
        {
            Ui.TextCentered(Defs.Current.Units[unitKeys[i]].Name, new Rectangle(midX, y, rightX - midX, 28), 16, Palette.Text);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? leftX : rightX;
                if (Button(new Rectangle(x, y, 44, 28), "-100", size: 14)) AdjustBattle(side, i, -100);
                if (Button(new Rectangle(x + 48, y, 40, 28), "-10", size: 14)) AdjustBattle(side, i, -10);
                Ui.TextCentered(counts[side][i].ToString(), new Rectangle(x + 90, y, 82, 28), 16, counts[side][i] > 0 ? Palette.Text : Palette.TextDim);
                if (Button(new Rectangle(x + 174, y, 40, 28), "+10", size: 14)) AdjustBattle(side, i, 10);
                if (Button(new Rectangle(x + 218, y, 44, 28), "+100", size: 14)) AdjustBattle(side, i, 100);
            }
        }
        y += 8;

        // 맵 · 거리
        Ui.Text(S.BattleTerrain, panel.X + 24, y + 6, 16, Palette.TextDim);
        float mx = panel.X + 110;
        string[] mapLabels = { S.BattleTerrainFlat, S.BattleTerrainRough, S.BattleTerrainCity };
        for (int m = 0; m < BattleSetup.Maps.Length; m++)
        {
            float bw = Ui.Measure(mapLabels[m], 16).X + 28;
            if (Button(new Rectangle(mx, y, bw, 32), mapLabels[m], true, battleMapIndex == m)) battleMapIndex = m;
            mx += bw + 8;
        }
        mx += 24;
        Ui.Text(S.BattleDistance, mx, y + 6, 16, Palette.TextDim);
        mx += Ui.Measure(S.BattleDistance, 16).X + 12;
        if (Button(new Rectangle(mx, y, 36, 32), "-")) battleDistance = Math.Max(BattleSetup.MinDistance, battleDistance - 8);
        Ui.TextCentered(battleDistance.ToString(), new Rectangle(mx + 40, y, 50, 32), 16, Palette.Text);
        if (Button(new Rectangle(mx + 94, y, 36, 32), "+")) battleDistance = Math.Min(BattleSetup.MaxDistance, battleDistance + 8);
        y += 44;

        // 안개 · AI 대 AI
        Ui.Text(S.MenuFog, panel.X + 24, y + 6, 16, Palette.TextDim);
        if (Button(new Rectangle(panel.X + 110, y, 80, 32), battleFog ? S.On : S.Off, true, battleFog)) battleFog = !battleFog;
        float ax = panel.X + 214;
        float aw = Ui.Measure(S.BattleAiVsAi, 16).X + 28;
        if (Button(new Rectangle(ax, y, aw, 32), S.BattleAiVsAi, true, battleAiVsAi)) battleAiVsAi = !battleAiVsAi;

        bool ok = BattleTotal(0) > 0 && BattleTotal(1) > 0;
        float by = panel.Y + panel.Height - 56;
        if (Button(new Rectangle(panel.X + 24, by, 140, 40), S.MenuBack)) page = Page.Main;
        if (!ok) Ui.TextCentered(S.BattleNeedUnits, new Rectangle(panel.X + 180, by, panel.Width - 180 - 240, 40), 16, Palette.Warning);
        if (Button(new Rectangle(panel.X + panel.Width - 24 - 200, by, 200, 40), S.MenuStart, ok, true, 26)) StartBattle();
    }

    /// <summary>빠른 교전 (ROADMAP 8.6): 아무것도 없는 평지에서 보병 n 대 AI 보병 n (기본 500, 메뉴에 200 도). 섬멸하면 승리. --quick-battle [n] 도 같은 구성.</summary>
    public static GameOptions QuickBattleOptions(int count = 500) => new()
    {
        Battle = new BattleSetup { SideA = QuickBattleSideFor(count), SideB = QuickBattleSideFor(count), Map = "flat", Distance = 60 },
        FromMenu = true, Faction = 1, Players = 2, Ai = AiRegistry.Default, Seed = Random.Shared.Next(1, 1_000_000),
    };
    public const string QuickBattleSide = "infantry:500";
    public static string QuickBattleSideFor(int count)
    {
        count = Math.Clamp(count, 1, 2000);
        int scouts = count / 25;   // 중대(≈50명) 마다 정찰병 2명 — 전술 AI 가 중대를 짜면 정찰조로 붙는다 (World.AttachLooseScouts)
        return scouts > 0 ? $"infantry:{count},infantry_scout:{scouts}" : $"infantry:{count}";
    }

    void StartBattle()
    {
        var c = BattleCounts();
        var setup = new BattleSetup
        {
            SideA = BattleSetup.Format(unitKeys.Select((k, i) => (k, c[0][i]))),
            SideB = BattleSetup.Format(unitKeys.Select((k, i) => (k, c[1][i]))),
            Map = BattleSetup.Maps[battleMapIndex],
            Distance = battleDistance,
            AiVsAi = battleAiVsAi,
        };
        Chosen = new GameOptions
        {
            Battle = setup, Spectate = battleAiVsAi, FromMenu = true, Faction = 1, Players = 2, Ai = AiRegistry.Default, NoFog = !battleFog,
            Seed = Random.Shared.Next(1, 1_000_000),
        };
    }

    // ---------------------------------------------------------------- 리플레이

    void OpenReplays()
    {
        page = Page.Replays;
        replayScroll = 0;
        string dir = Path.Combine(AppContext.BaseDirectory, "replays");
        replayFiles = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.kdr.json").OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).ToList()
            : new List<string>();
    }

    static string DateOf(string file)
    {
        string name = Path.GetFileName(file);
        return DateTime.TryParseExact(name[..Math.Min(15, name.Length)], "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out var d)
            ? d.ToString("yyyy-MM-dd HH:mm") : name;
    }

    /// <summary>머리말 정보 (맵 · 인원 · 길이). 파일을 통째로 읽으므로 보이는 행만, 프레임당 한 개씩.</summary>
    string InfoOf(string file, ref bool budget)
    {
        if (replayInfo.TryGetValue(file, out var cached)) return cached;
        if (!budget) return "...";
        budget = false;
        string info;
        try
        {
            var r = ReplayFile.Load(file);
            string map = r.MapId ?? string.Format(S.MenuGeneratedShort, r.MapWidth, r.MapHeight);
            info = string.Format(S.MenuReplayInfo, map, r.FactionCount, Game.Clock(r.EndTick))
                   + (r.StartSnapshot != null ? "  ·  " + string.Format(S.ReplayFromSave, Game.Clock(r.StartTick)) : "");
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or FormatException)
        {
            info = ReplayVersionException.Is(e) ? S.MenuReplayOld : S.MenuBroken;
        }
        replayInfo[file] = info;
        return info;
    }

    void DrawReplays()
    {
        float pw = MathF.Min(820, screenW - 40), ph = MathF.Min(520, screenH - 70);
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(12, (screenH - ph) / 2 - 10), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 235), Palette.HudTrim);
        Ui.Text(S.MenuReplayTitle, panel.X + 24, panel.Y + 16, 26, Palette.Accent);

        var list = new Rectangle(panel.X + 16, panel.Y + 62, panel.Width - 32, panel.Height - 62 - 66);
        var files = replayFiles ?? new List<string>();
        if (files.Count == 0) Ui.TextCentered(S.MenuNoReplays, list, 16, Palette.TextDim);

        float content = files.Count * RowH;
        if (Ui.Contains(list, mouse)) replayScroll -= Input.GetMouseWheelMove() * RowH;
        replayScroll = Math.Clamp(replayScroll, 0, MathF.Max(0, content - list.Height));

        bool budget = true;
        Raylib.BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
        for (int i = 0; i < files.Count; i++)
        {
            var row = new Rectangle(list.X, list.Y + i * RowH - replayScroll, list.Width - 8, RowH - 4);
            if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;
            bool hover = Ui.Contains(row, mouse) && Ui.Contains(list, mouse);
            Raylib.DrawRectangleRounded(row, 0.15f, 4, hover ? Palette.BtnHover : Palette.Btn);
            Raylib.DrawRectangleRoundedLinesEx(row, 0.15f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
            Ui.Text(DateOf(files[i]), row.X + 14, row.Y + 11, 16, Palette.Text);
            Ui.Text(InfoOf(files[i], ref budget), row.X + 190, row.Y + 11, 16, Palette.TextDim);
            if (hover && click)
            {
                Chosen = new GameOptions { ReplayPath = files[i], FromMenu = true, SaveReplay = false };
                break;
            }
        }
        Raylib.EndScissorMode();

        if (content > list.Height)
        {
            float th = MathF.Max(24, list.Height * list.Height / content);
            float ty = list.Y + (list.Height - th) * (replayScroll / (content - list.Height));
            Raylib.DrawRectangleRec(new Rectangle(list.X + list.Width - 4, ty, 3, th), Palette.Alpha(Palette.TextDim, 160));
        }

        if (Button(new Rectangle(panel.X + 24, panel.Y + panel.Height - 54, 140, 40), S.MenuBack)) page = Page.Main;
    }

    // ---------------------------------------------------------------- 불러오기

    List<SaveEntry>? saves;
    float saveScroll;
    string? loadError, deleteArm;
    double deleteArmUntil;
    const float SaveRowH = 66;

    void OpenLoad()
    {
        page = Page.Load;
        saveScroll = 0;
        loadError = null;
        deleteArm = null;
        saves = SaveManager.List();
    }

    static string TagsOf(SnapshotHeader h)
    {
        var tags = new List<string>();
        if (h.Auto) tags.Add(S.SaveTagAuto);
        if (h.Battle != null) tags.Add(S.SaveTagBattle);
        else if (h.Spectate) tags.Add(S.SaveTagSpectate);
        if (h.CheatsUsed) tags.Add(S.SaveTagCheats);
        if (h.Winner != 0) tags.Add(S.SaveTagOver);
        return string.Join("  ·  ", tags);
    }

    static string MapLabel(SnapshotHeader h) => h.MapId ?? string.Format(S.MenuGeneratedShort, h.MapWidth, h.MapHeight);

    void DrawLoad()
    {
        float pw = MathF.Min(900, screenW - 40), ph = MathF.Min(560, screenH - 60);
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(12, (screenH - ph) / 2 - 10), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 235), Palette.HudTrim);
        Ui.Text(S.LoadTitle, panel.X + 24, panel.Y + 16, 26, Palette.Accent);

        var list = new Rectangle(panel.X + 16, panel.Y + 62, panel.Width - 32, panel.Height - 62 - 66);
        var items = saves ??= new List<SaveEntry>();
        if (items.Count == 0)
        {
            Ui.TextCentered(S.LoadNone, new Rectangle(list.X, list.Y + list.Height / 2 - 30, list.Width, 28), 20, Palette.TextDim);
            Ui.TextCentered(S.LoadEmptyHint, new Rectangle(list.X, list.Y + list.Height / 2, list.Width, 24), 16, Palette.TextDim);
        }

        float content = items.Count * SaveRowH;
        if (Ui.Contains(list, mouse)) saveScroll -= Input.GetMouseWheelMove() * SaveRowH;
        saveScroll = Math.Clamp(saveScroll, 0, MathF.Max(0, content - list.Height));

        double now = Raylib.GetTime();
        if (deleteArm != null && now > deleteArmUntil) deleteArm = null;
        int loadIndex = -1, deleteIndex = -1;
        Raylib.BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
        for (int i = 0; i < items.Count; i++)
        {
            var row = new Rectangle(list.X, list.Y + i * SaveRowH - saveScroll, list.Width - 8, SaveRowH - 6);
            if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;
            var e = items[i];
            var h = e.Header;
            bool inList = Ui.Contains(list, mouse);
            var del = new Rectangle(row.X + row.Width - 84, row.Y + (row.Height - 30) / 2, 74, 30);
            bool overDel = inList && Ui.Contains(del, mouse);
            bool hover = inList && Ui.Contains(row, mouse) && !overDel;
            Color fill = h == null ? Palette.PanelDark : hover ? Palette.BtnHover : Palette.Btn;
            Raylib.DrawRectangleRounded(row, 0.15f, 4, fill);
            Raylib.DrawRectangleRoundedLinesEx(row, 0.15f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);

            float x = row.X + 14;
            Ui.Text(e.Name, x, row.Y + 7, 16, h == null ? Palette.TextDim : Palette.Text);
            if (h != null)
            {
                float nx = x + Ui.Measure(e.Name, 16).X + 14;
                string tags = TagsOf(h);
                if (tags.Length > 0) { Ui.Text(tags, nx, row.Y + 7, 16, Palette.Accent); nx += Ui.Measure(tags, 16).X + 14; }
                var warn = new List<string>();
                if (h.GameVersion != Config.Version) warn.Add(S.SaveVersionDiffers);
                if (h.BalanceHash != Defs.Current.Hash) warn.Add(S.SaveBalanceDiffers);
                if (warn.Count > 0) Ui.Text(string.Join(" · ", warn), nx, row.Y + 7, 16, Palette.Warning);
                string date = h.SavedAt;
                Ui.Text(date, del.X - 12 - Ui.Measure(date, 16).X, row.Y + 7, 16, Palette.TextDim);
                string info = string.Format(S.SaveInfo, MapLabel(h), SaveManager.Clock(h.Tick), h.Factions.Count, h.Units);
                Ui.Text(info, x, row.Y + 29, 16, Palette.TextDim);
                // 세력 칩 (색 + 이름), 공간이 허락하는 만큼
                float fx = x + Ui.Measure(info, 16).X + 18;
                foreach (var f in h.Factions)
                {
                    string label = f.Name + (f.Id == h.HumanFaction && !h.Spectate ? "*" : "");
                    float w = Ui.Measure(label, 14).X + 22;
                    if (fx + w > del.X - 16) break;
                    var col = f.Color is { Length: 3 } c ? new Color(c[0], c[1], c[2], 255) : Palette.Player(f.Id);
                    Raylib.DrawRectangle((int)fx, (int)(row.Y + 33), 10, 10, f.Defeated ? Palette.Alpha(col, 90) : col);
                    Ui.Text(label, fx + 14, row.Y + 30, 14, f.Defeated ? Palette.TextDim : Palette.Text);
                    fx += w + 6;
                }
            }
            else
            {
                string kind = e.Error != null && e.Error.StartsWith("저장 형식") ? S.SaveIncompatible : S.SaveUnreadable;
                Ui.Text(kind, x + Ui.Measure(e.Name, 16).X + 14, row.Y + 7, 16, Palette.HpLow);
                string msg = e.Error ?? "";
                float maxW = del.X - x - 24;
                while (msg.Length > 4 && Ui.Measure(msg, 16).X > maxW) msg = msg[..^4] + "...";
                Ui.Text(msg, x, row.Y + 29, 16, Palette.TextDim);
            }
            bool armed = deleteArm == e.Path;
            Ui.Button(del, armed ? S.SaveDeleteSure : S.SaveDelete, overDel, armed);

            if (click && inList)
            {
                if (overDel) deleteIndex = i;
                else if (hover && h != null) loadIndex = i;
            }
        }
        Raylib.EndScissorMode();

        if (content > list.Height)
        {
            float th = MathF.Max(24, list.Height * list.Height / content);
            float ty = list.Y + (list.Height - th) * (saveScroll / (content - list.Height));
            Raylib.DrawRectangleRec(new Rectangle(list.X + list.Width - 4, ty, 3, th), Palette.Alpha(Palette.TextDim, 160));
        }

        if (deleteIndex >= 0 && deleteIndex < items.Count)
        {
            var e = items[deleteIndex];
            if (deleteArm == e.Path)
            {
                SaveManager.Delete(e.Path);
                deleteArm = null;
                saves = SaveManager.List();
            }
            else { deleteArm = e.Path; deleteArmUntil = now + 3; }
        }
        else if (loadIndex >= 0 && loadIndex < items.Count) TryLoad(loadIndex);

        if (loadError != null)
        {
            string msg = string.Format(S.LoadFailed, loadError);
            float maxW = panel.Width - 240;
            while (msg.Length > 4 && Ui.Measure(msg, 16).X > maxW) msg = msg[..^4] + "...";
            Ui.Text(msg, panel.X + 190, panel.Y + panel.Height - 44, 16, Palette.Warning);
        }
        if (Button(new Rectangle(panel.X + 24, panel.Y + panel.Height - 54, 140, 40), S.MenuBack)) page = Page.Main;
    }

    /// <summary>저장을 끝까지 읽어 본다 (여기서 실패하면 목록에 '읽을 수 없음' 으로 표시하고 계속 — 게임은 죽지 않는다). 성공하면 그 월드로 시작.</summary>
    void TryLoad(int index)
    {
        var e = saves![index];
        try
        {
            var l = Snapshot.LoadFile(e.Path, Defs.Current);
            Chosen = SaveManager.OptionsFor(l, fromMenu: true);
            loadError = null;
        }
        catch (SnapshotException ex)
        {
            loadError = ex.Message;
            saves[index] = e with { Header = null, Error = ex.Message };
        }
    }
}
