using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// 사단 캠페인 홈 화면 (3화면 15탭 — 배치 · 틀은 CampaignHome.cs, MVP 5탭 본문은 여기 (6번째 8 참모진은 읽기 전용, CampaignHomeStaff.cs) · 작전 전 출격 준비 · 전과 보고 모달).
/// S2(home-ui), S3(작전 전 투입), S4(전과 보고), S5(본국 전문) 통합 UI.
/// </summary>
public sealed partial class CampaignMenu
{
    public string? OpenMission { get; set; }
    /// <summary>튜토리얼 연습 (0장): 캠페인 상태 · 결과 파일을 건드리지 않는 출격 (MainMenu 가 MissionMenu practice 로 연다).</summary>
    public string? OpenPractice { get; set; }
    public bool BackRequested { get; set; }

    enum ViewMode { Home, PreOp, Debrief }

    CampaignState state;        // 슬롯 불러오기 시 교체 (T19a)
    /// <summary>[TC-A] 캠페인 변수 읽기 (브리핑 정보 단계). 슬롯 교체 후에도 현재 state 를 본다.</summary>
    public int Flag(string key) => state.Flags.GetValueOrDefault(key, 0);
    string? loadError;
    ViewMode currentView = ViewMode.Home;
    Tab currentTab = Tab.Summary;

    // 작전 전(PreOp) 상태
    string targetMissionId = CampaignState.FirstMission;
    readonly HashSet<string> preOpSelectedCompanyIds = new();

    // 본국(Events) 탭 상태
    int selectedEventIndex;
    string? eventNotice;

    // 병참 탭 알림
    string? logisticsNotice;

    // 스크롤 및 UI 상태
    float rosterScroll;
    float recentLogScroll;
    float historyScroll;
    bool resetArmed;
    Vector2 mouse;
    bool click;

    // 저장 슬롯 창 · 저장 실패 재시도 (T19a)
    bool slotsOpen;
    string? slotArm, slotNotice;
    string? migratedNote; double migratedUntil;   // T49a: 캠페인 파일을 새 스키마로 처음 옮겼을 때 한 번 보이는 알림
    CampaignSaves.Info[] slotInfo = Array.Empty<CampaignSaves.Info>();   // 0..8 = 슬롯 1~9, 9 = 작전 직전, 10 = 정산 직후
    Func<bool>? saveRetry;      // null 이 아니면 저장 실패 알림 (다시 시도할 저장)
    Action? saveThen;           // 저장 성공 뒤 이어서 할 일 (출격 · 불러오기 완료)

    public CampaignMenu()
    {
        state = CampaignState.Load() ?? new CampaignState();
        loadError = CampaignState.LastLoadError;
        Adopt(state);
        ApplyViewOverride();
        ApplyMapZoomOverride();   // T47 확인용
    }

    /// <summary>상태를 화면에 붙인다 (생성 · 슬롯 불러오기).</summary>
    void Adopt(CampaignState s)
    {
        state = s;
        state.EnsureInitialized();
        currentView = state.LastDebrief != null ? ViewMode.Debrief : ViewMode.Home;   // 직전 전과 보고가 있으면 바로 Debrief 모달 표시
        targetMissionId = !string.IsNullOrEmpty(state.NextMission) ? state.NextMission : CampaignState.FirstMission;   // 기본 타깃 미션
        eventNotice = logisticsNotice = null;
        resetArmed = false;
    }

    /// <summary>저장 → 성공하면 then, 실패하면 알림 + [다시 시도] (설계 §3: 실패 알림 + 다시 시도).</summary>
    bool saveFresh;   // 저장 실패 알림이 이번 프레임에 만들어짐 → 같은 클릭 무시

    void Persist(Func<bool> save, Action? then = null)
    {
        if (save()) { saveRetry = null; saveThen = null; then?.Invoke(); }
        else { saveRetry = save; saveThen = then; saveFresh = true; }   // 이 프레임의 클릭이 [다시 시도] 를 누르지 않게 한다
    }

    /// <summary>진행 저장 (읽기 실패한 원본은 덮어쓰지 않는다 — 원본 보호).</summary>
    void SaveLive(Action? then = null)
    {
        if (loadError != null) { then?.Invoke(); return; }
        Persist(state.Save, then);
    }

    // ---------------------------------------------------------------- UI 유틸

    bool Button(Rectangle r, string label, bool enabled = true, int size = 16, Color? textColor = null)
    {
        Ui.RegHit(r, label, enabled);
        bool hover = enabled && Ui.Contains(r, mouse);
        Color fill = !enabled ? Palette.PanelDark : hover ? Palette.BtnHover : Palette.Btn;
        Raylib.DrawRectangleRounded(r, 0.2f, 4, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
        Ui.TextCentered(label, r, size, textColor ?? (enabled ? Palette.Text : Palette.TextDim));
        return enabled && hover && click;
    }

    bool Checkbox(Rectangle r, bool isChecked, bool enabled = true)
    {
        Ui.RegHit(r, "checkbox", enabled);
        bool hover = enabled && Ui.Contains(r, mouse);
        Raylib.DrawRectangleRounded(r, 0.2f, 3, hover ? Palette.BtnHover : Palette.Btn);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 3, 1, isChecked ? Palette.Accent : Palette.PanelBorder);
        if (isChecked)
        {
            var inner = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
            Raylib.DrawRectangleRounded(inner, 0.2f, 2, Palette.Accent);
        }
        return enabled && hover && click;
    }

    static int CountFrom(object? obj) => obj switch
    {
        int n => n,
        IDictionary<string, int> d => d.Values.Sum(),
        IEnumerable<int> e => e.Sum(),
        _ => 0
    };

    static MissionDef? LoadMissionDefSafe(string id)
    {
        try
        {
            var peekMethod = typeof(MissionDef).GetMethod("Peek", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (peekMethod != null && peekMethod.Invoke(null, new object[] { id }) is MissionDef peeked)
                return peeked;
            return MissionDef.Load(id);
        }
        catch
        {
            return null;
        }
    }

    static int GetMaxDeploySafe(MissionDef? def)
    {
        if (def == null) return 300;
        var prop = def.GetType().GetProperty("MaxDeploy");
        if (prop?.GetValue(def) is int md && md > 0) return md;
        return 300;
    }

    static List<string> WrapText(string text, float width, float size)
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

    // ---------------------------------------------------------------- 메인 드로우

    public void Draw(int screenW, int screenH)
    {
        mouse = Input.GetMousePosition();
        bool rawClick = Input.IsMouseButtonPressed(MouseButton.Left);
        click = rawClick && !slotsOpen && saveRetry == null;   // 창이 떠 있으면 뒤 화면은 클릭을 받지 않는다
        DrawViews(screenW, screenH);
        if (slotsOpen) { click = rawClick && saveRetry == null; DrawSlots(screenW, screenH); }
        if (saveRetry != null) { click = rawClick && !saveFresh; DrawSaveFailed(screenW, screenH); }
        saveFresh = false;
    }

    void DrawViews(int screenW, int screenH)
    {
        float pw = MathF.Min(1020, screenW - 32);
        float ph = MathF.Min(640, screenH - 48);
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(8, (screenH - ph) / 2 - 8), pw, ph);

        if (currentView == ViewMode.Debrief && state.LastDebrief != null)
        {
            // 배경 홈 패널 렌더링 후 위에 전과 보고 오버레이 표시
            DrawHomeFrame(panel);
            DrawDebriefModal(screenW, screenH, state.LastDebrief);
            return;
        }

        if (currentView == ViewMode.PreOp)
        {
            DrawPreOpView(panel);
            return;
        }

        DrawHomeFrame(panel);
    }

    // ---------------------------------------------------------------- 홈 프레임 & 상단바 & 탭

    void DrawHomeFrame(Rectangle panel)
    {
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 245), Palette.HudTrim);

        // 상단 바 (사단명, 보급품, 편성 병력, 신병 풀, 부상자, 차기 작전, 본국 전문 배지)
        DrawTopBar(panel);

        // 좌측 탭 바 (약 130px)
        float tabW = 126;
        float tabX = panel.X + 16;
        float tabY = panel.Y + 70;

        // 좌측 하단 액션 버튼 ([작전 개시], [캠페인 초기화], [뒤로])
        float by = panel.Y + panel.Height - 48;
        DrawTabColumn(tabX, tabY, tabW, by - 88 - 8);   // 현재 화면의 탭 (중앙 9 · 좌 3 · 우 3, CampaignHome.cs)
        var opStartRect = new Rectangle(tabX, by - 88, tabW, 40);
        bool opReady = File.Exists(MissionDef.PathOf(targetMissionId));   // 다음 작전 파일이 없으면 (준비 중) 출격 잠금
        if (Button(opStartRect, S.CmBtnOpStart, opReady, 18, opReady ? Palette.Accent : null))
        {
            OpenPreOp(targetMissionId);
        }

        if (CampaignState.TakeMigrationNotice() is { } mn) { migratedNote = mn; migratedUntil = Raylib.GetTime() + 12; }
        if (migratedNote != null && Raylib.GetTime() > migratedUntil) migratedNote = null;
        if (migratedNote != null && loadError == null) Ui.Text(migratedNote, panel.X + 160, panel.Y + panel.Height - 30, 16, Palette.HpGood);
        if (loadError != null)   // 저장을 못 읽었다: 조용히 새 캠페인으로 덮어쓰지 않도록 사유를 보인다 (저장 호환)
        {
            Ui.Text(loadError, panel.X + 160, panel.Y + panel.Height - 52, 16, Palette.Warning);
            Ui.Text(S.CmOverwriteWarn, panel.X + 160, panel.Y + panel.Height - 30, 16, Palette.TextDim);
        }

        var resetRect = new Rectangle(tabX, by - 44, tabW, 36);
        string resetLabel = resetArmed ? S.CmResetConfirm : S.CmReset;
        if (Button(resetRect, resetLabel, true, 14, resetArmed ? Palette.HpLow : Palette.TextDim))
        {
            if (resetArmed)
            {
                if (loadError != null) CampaignState.BackupBroken();   // 읽지 못한 원본은 .bak 로 보존한 뒤 초기화
                CampaignState.Reset();
                BackRequested = true;
            }
            else resetArmed = true;
        }

        var backRect = new Rectangle(tabX, by, tabW, 36);
        if (Button(backRect, S.CmBack, true, 16))
        {
            BackRequested = true;
        }

        // 우측 본문 영역
        var body = DrawScreenStrip(new Rectangle(panel.X + tabW + 28, panel.Y + 64, panel.Width - tabW - 44, panel.Height - 80));   // 3화면 띠
        Ui.Panel(body, Palette.Alpha(Palette.Panel, 200), Palette.PanelBorder);

        switch (currentTab)
        {
            case Tab.Summary: DrawTabSummary(body); break;
            case Tab.Roster: DrawTabRoster(body); break;
            case Tab.Logistics: DrawTabLogistics(body); break;
            case Tab.Map: DrawTabMap(body); break;
            case Tab.Events: DrawTabEvents(body); break;
            case Tab.Staff: DrawTabStaff(body); break;
            case Tab.Schedule: DrawTabSchedule(body); break;      // T42d
            case Tab.Records: DrawTabRecords(body); break;        // T38f 읽기 전용
            case Tab.Equipment: DrawTabEquipment(body); break;
            case Tab.Organization: DrawTabOrganization(body); break;
            case Tab.Promotion: DrawTabPromotion(body); break;       // T42b
            case Tab.Diplomacy: DrawTabPrisoners(body); break;       // T42c
            default: DrawTabPlaceholder(body, currentTab); break;   // 후속 탭 (틀만)
        }
    }

    void DrawTopBar(Rectangle panel)
    {
        float x = panel.X + 20, y = panel.Y + 14;
        Ui.Text(S.CmDivisionName, x, y, 22, Palette.Accent);

        // 자원 요약 한 줄
        int totalStrength = state.Roster.Sum(c => c.Strength);
        int recruits = state.Manpower?.Recruits ?? 0;
        int maxRecruits = CampaignState.ManpowerState.MaxRecruits;
        int wounded = state.Manpower?.Wounded ?? 0;
        float supply = state.Supply;

        float rx = panel.X + 260;
        string resText = $"{string.Format(S.CmTopSupply, supply)}   |   {string.Format(S.CmTopManpower, totalStrength)}   |   {string.Format(S.CmTopRecruits, recruits, maxRecruits)}   |   {string.Format(S.CmTopWounded, wounded)}";
        Ui.Text(resText, rx, y + 4, 15, Palette.Text);

        // 다음 작전 & 미확인 전문
        string nextName = !string.IsNullOrEmpty(state.NextName) ? state.NextName : (string.IsNullOrEmpty(state.NextMission) ? S.CmMissionC001Name : state.NextMission);
        var pendingEvents = CampaignEvents.AvailableEvents(state);

        float rEnd = panel.X + panel.Width - 20;
        float rightY = y + 4;
        if (state.LastDebrief != null)
        {
            var debriefBtnRect = new Rectangle(rEnd - 130, y, 130, 28);
            if (Button(debriefBtnRect, S.CmTopDebriefBtn, true, 14, Palette.Accent))
            {
                currentView = ViewMode.Debrief;
            }
            rEnd -= 140;
        }

        var slotsBtnRect = new Rectangle(rEnd - 130, y, 130, 28);   // 슬롯 UI = 홈 상단 메뉴 (설계 §15 표 8)
        if (Button(slotsBtnRect, S.CmTopSaves, true, 14)) OpenSlots();
        rEnd -= 140;

        if (pendingEvents.Count > 0)
        {
            string evBadge = string.Format(S.CmTopEvents, pendingEvents.Count);
            Vector2 evSz = Ui.Measure(evBadge, 14);
            Ui.Text(evBadge, rEnd - evSz.X, rightY, 14, Palette.Warning);
            rEnd -= evSz.X + 20;
        }

        string opText = string.Format(S.CmTopNextOp, nextName);
        Vector2 opSz = Ui.Measure(opText, 14);
        Ui.Text(opText, rEnd - opSz.X, rightY, 14, Palette.TextDim);

        // 구분선
        Raylib.DrawLineEx(new Vector2(panel.X + 16, panel.Y + 56), new Vector2(panel.X + panel.Width - 16, panel.Y + 56), 1, Palette.PanelBorder);
    }

    // ---------------------------------------------------------------- 1. 요약 탭

    void DrawTabSummary(Rectangle body)
    {
        float x = body.X + 20, y = body.Y + 16;
        float w = body.Width - 40;
        Ui.Text(S.CmSumTitle, x, y, 22, Palette.Accent); y += 32;

        // 좌우 2컬럼 레이아웃
        float colW = (w - 20) / 2;
        float leftX = x, rightX = x + colW + 20;
        float ly = y, ry = y;

        // [좌측] 1. 사단 상태 요약
        Ui.Text(S.CmSumSectionState, leftX, ly, 17, Palette.Accent); ly += 24;
        int totalStrength = state.Roster.Sum(c => c.Strength);
        int totalInfantry = state.Roster.Where(c => c.Kind.Contains("infantry") || c.Kind.Contains("보병") || c.Kind.Contains("assault")).Sum(c => c.Strength);
        int totalRifles = state.Stock?.Rifles?.Values.Sum() ?? 0;
        int rifleRate = totalInfantry > 0 ? (int)MathF.Round((float)totalRifles / totalInfantry * 100f) : 100;

        Ui.Text($"총 편성 병력: {totalStrength:N0}명 / 10,000명 (총 {state.Roster.Count}개 중대)", leftX + 10, ly, 15, Palette.Text); ly += 20;
        Ui.Text($"신병 대기소: {state.Manpower?.Recruits ?? 0:N0}명 / {CampaignState.ManpowerState.MaxRecruits:N0}명", leftX + 10, ly, 15, Palette.Text); ly += 20;
        Ui.Text($"야전 부상자: {state.Manpower?.Wounded ?? 0:N0}명 (치료 대기)", leftX + 10, ly, 15, Palette.Text); ly += 20;
        Ui.Text($"보급품 잔고: {state.Supply:N0} (작전 승리 시 +1,500 보충)", leftX + 10, ly, 15, Palette.Text); ly += 20;

        Color rifleCol = rifleRate >= 100 ? Palette.HpGood : rifleRate >= 70 ? Palette.Warning : Palette.HpLow;
        Ui.Text(string.Format(S.CmSumRifleRate, rifleRate, totalRifles, totalInfantry), leftX + 10, ly, 15, rifleCol); ly += 20;
        var fm = CampaignHomeViews.FatigueMorale(state);   // [T42a] 읽기 전용 한 줄
        Ui.Text(string.Format(S.CmSumFatigueMorale, fm.AvgFatigue, fm.AvgMorale, fm.Tired), leftX + 10, ly, 15, fm.Tired > 0 ? Palette.Warning : Palette.Text); ly += 30;

        // [좌측] 2. 지휘부 및 황실 동향
        Ui.Text(S.CmSumSectionPolitics, leftX, ly, 17, Palette.Accent); ly += 24;
        int heirMood = state.Flags.TryGetValue("heir_mood", out int hm) ? hm : 60;
        int cmdFavor = state.Flags.TryGetValue("cmd_favor", out int cf) ? cf : 50;

        string heirText = heirMood >= 80 ? S.CmHeirMood80 : heirMood >= 50 ? S.CmHeirMood50 : heirMood >= 30 ? S.CmHeirMood30 : S.CmHeirMood0;
        string cmdText = cmdFavor >= 80 ? S.CmCmdFavor80 : cmdFavor >= 50 ? S.CmCmdFavor50 : cmdFavor >= 30 ? S.CmCmdFavor30 : S.CmCmdFavor0;

        Ui.Text($"황손 심기 동향:", leftX + 10, ly, 15, Palette.TextDim); ly += 18;
        foreach (var l in WrapText(heirText, colW - 20, 14)) { Ui.Text(l, leftX + 18, ly, 14, Palette.Text); ly += 18; }
        var (recPen, supPen) = CampaignEconomy.ComputePenalty(state);
        if (recPen > 0) { Ui.Text(string.Format(S.CmSumRecruitPenalty, (int)(recPen * 100)), leftX + 18, ly, 14, Palette.Warning); ly += 18; }
        ly += 6;

        Ui.Text($"총사령관 신임 동향:", leftX + 10, ly, 15, Palette.TextDim); ly += 18;
        foreach (var l in WrapText(cmdText, colW - 20, 14)) { Ui.Text(l, leftX + 18, ly, 14, Palette.Text); ly += 18; }
        if (supPen > 0) { Ui.Text(string.Format(S.CmSumSupplyPenalty, (int)(supPen * 100)), leftX + 18, ly, 14, Palette.Warning); ly += 18; }

        // [우측] 3. 최신 본국 전문
        Ui.Text(S.CmSumSectionEvent, rightX, ry, 17, Palette.Accent); ry += 24;
        var avEvents = CampaignEvents.AvailableEvents(state);
        if (avEvents.Count > 0)
        {
            var ev = avEvents[0];
            Ui.Text($"[{ev.Title}]", rightX + 10, ry, 15, Palette.Accent); ry += 20;
            Ui.Text(string.Format(S.CmEventSender, ev.Sender), rightX + 10, ry, 14, Palette.TextDim); ry += 18;
            foreach (var l in WrapText(ev.Text, colW - 20, 14).Take(3)) { Ui.Text(l, rightX + 10, ry, 14, Palette.Text); ry += 18; }
            var evBtn = new Rectangle(rightX + 10, ry + 4, 140, 26);
            if (Button(evBtn, "[본국 탭에서 확인]", true, 13, Palette.Accent)) SelectTab(Tab.Events);
            ry += 38;
        }
        else
        {
            Ui.Text(S.CmSumNoEvents, rightX + 10, ry, 14, Palette.TextDim); ry += 36;
        }

        // [우측] 4. 역대 작전 기록
        Ui.Text(S.CmSumSectionHistory, rightX, ry, 17, Palette.Accent); ry += 24;
        if (state.History.Count == 0)
        {
            Ui.Text(S.CmSumNoHistory, rightX + 10, ry, 14, Palette.TextDim);
        }
        else
        {
            var histBox = new Rectangle(rightX, ry, colW, body.Y + body.Height - ry - 16);
            Raylib.BeginScissorMode((int)histBox.X, (int)histBox.Y, (int)histBox.Width, (int)histBox.Height);
            float hy = histBox.Y;
            foreach (var h in state.History.AsEnumerable().Reverse())
            {
                Ui.Text($"• {h.Title} ({MissionText.Clock(h.DurationSeconds)})", rightX + 10, hy, 14, Palette.Text); hy += 18;
                Ui.Text($"   사살 {h.EnemyKills}명 · 아군 손실 {h.Lost}명 · 생존 {h.Survivors}명", rightX + 10, hy, 13, Palette.TextDim); hy += 22;
                if (hy > histBox.Y + histBox.Height) break;
            }
            Raylib.EndScissorMode();
        }
    }

    // ---------------------------------------------------------------- 2. 부대 탭

    void DrawTabRoster(Rectangle body)
    {
        float x = body.X + 20, y = body.Y + 16;
        float w = body.Width - 40;
        Ui.Text(S.CmRosterTitle, x, y, 22, Palette.Accent);
        DrawRegimentSummary(x + w, y - 4); y += 30;   // 연대 배속 요약 (Q9)

        // 표 머리말
        float colBattalion = 100, colCompany = 110, colKind = 70, colStrength = 100, colWounded = 50, colStatus = 100, colRegiment = 90, colBtn = 130;
        var headerRect = new Rectangle(x, y, w, 26);
        Ui.Panel(headerRect, Palette.Alpha(Palette.PanelDark, 200), Palette.PanelBorder);

        float hx = x + 8;
        Ui.Text(S.CmColBattalion, hx, y + 5, 14, Palette.TextDim); hx += colBattalion;
        Ui.Text(S.CmColCompany, hx, y + 5, 14, Palette.TextDim); hx += colCompany;
        Ui.Text(S.CmColKind, hx, y + 5, 14, Palette.TextDim); hx += colKind;
        Ui.Text(S.CmColStrength, hx, y + 5, 14, Palette.TextDim); hx += colStrength;
        Ui.Text(S.CmColWounded, hx, y + 5, 14, Palette.TextDim); hx += colWounded;
        Ui.Text(S.CmColStatus, hx, y + 5, 14, Palette.TextDim); hx += colStatus;
        Ui.Text(S.CmColRegiment, hx, y + 5, 14, Palette.TextDim); hx += colRegiment;
        Ui.Text(S.CmColAction, hx, y + 5, 14, Palette.TextDim);
        y += 30;

        // Roster 중대 행 스크롤 목록
        var listArea = new Rectangle(x, y, w, body.Y + body.Height - y - 16);
        const float rowH = 32;
        float totalH = state.Roster.Count * rowH;

        if (Ui.Contains(listArea, mouse)) rosterScroll -= Input.GetMouseWheelMove() * rowH * 2;
        rosterScroll = Math.Clamp(rosterScroll, 0, MathF.Max(0, totalH - listArea.Height));

        Raylib.BeginScissorMode((int)listArea.X, (int)listArea.Y, (int)listArea.Width, (int)listArea.Height);
        int curBn = -1;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var c = state.Roster[i];
            float ry = listArea.Y + i * rowH - rosterScroll;
            if (ry + rowH < listArea.Y || ry > listArea.Y + listArea.Height) continue;

            var rowRect = new Rectangle(listArea.X, ry, listArea.Width, rowH - 2);
            bool hover = Ui.Contains(rowRect, mouse);
            Raylib.DrawRectangleRec(rowRect, hover ? Palette.BtnHover : (i % 2 == 0 ? Palette.Alpha(Palette.PanelDark, 120) : Palette.Alpha(Palette.Panel, 120)));

            float rx = listArea.X + 8;
            // 대대명
            string bnName = !string.IsNullOrEmpty(c.BattalionName) ? c.BattalionName : $"{c.BattalionNumber}대대";
            Ui.Text(bnName, rx, ry + 7, 14, Palette.Text); rx += colBattalion;
            // 중대명
            Ui.Text(c.Name, rx, ry + 7, 14, Palette.Text); rx += colCompany;
            // 병종
            Ui.Text(c.Kind, rx, ry + 7, 14, Palette.TextDim); rx += colKind;
            // 편성 / 정원
            Color stCol = c.Strength < c.MaxStrength ? Palette.Warning : Palette.Text;
            Ui.Text($"{c.Strength} / {c.MaxStrength}", rx, ry + 7, 14, stCol); rx += colStrength;
            // 부상
            Color wCol = c.Wounded > 0 ? Palette.HpLow : Palette.TextDim;
            Ui.Text($"{c.Wounded}", rx, ry + 7, 14, wCol); rx += colWounded;
            // 상태 / 의무
            if (c.Mandatory) Ui.Text(S.CmStatusMandatory, rx, ry + 7, 14, Palette.Accent);
            else if (c.Available && CampaignEconomy.IsTired(c)) Ui.Text(S.CmStatusTired, rx, ry + 7, 14, Palette.Warning);   // T42a: 경고만, 잠금 없음
            else if (c.Available) Ui.Text(S.CmStatusAvailable, rx, ry + 7, 14, Palette.HpGood);
            else Ui.Text(S.CmStatusUnavailable, rx, ry + 7, 14, Palette.HpLow);
            rx += colStatus;
            if (ry >= listArea.Y && ry + rowH <= listArea.Y + listArea.Height) DrawRegimentCell(c, rx, ry, rowH);   // 연대 배속 (가려진 행은 클릭 안 받음)
            rx += colRegiment;

            // 신병 배치 버튼
            int need = c.MaxStrength - c.Strength;
            int recruits = state.Manpower?.Recruits ?? 0;
            int toAssign = Math.Min(100, Math.Min(need, recruits));
            float reqSupply = toAssign * 0.5f;
            int totalRifles = state.Stock?.Rifles?.Values.Sum() ?? 0;
            bool canAssign = toAssign > 0 && state.Supply >= reqSupply && totalRifles >= toAssign;

            var btnRect = new Rectangle(rx, ry + 3, colBtn - 8, rowH - 8);
            if (need <= 0)
            {
                Ui.Text(S.CmAssignFull, rx + 4, ry + 7, 13, Palette.TextDim);
            }
            else
            {
                string btnText = toAssign >= 100 ? S.CmBtnAssign100 : S.CmBtnAssignRem;
                if (Button(btnRect, btnText, canAssign, 13))
                {
                    CampaignEconomy.AssignRecruits(state, c.Id, toAssign);
                    SaveLive();
                }
                if (hover && !canAssign)
                {
                    // 마우스 호버 시 부족 이유 툴팁
                    Ui.Text(S.CmAssignLackRes, listArea.X + listArea.Width - 160, ry + 7, 12, Palette.HpLow);
                }
            }
        }
        Raylib.EndScissorMode();
    }

    // ---------------------------------------------------------------- 3. 병참 탭

    void DrawTabLogistics(Rectangle body)
    {
        float x = body.X + 20, y = body.Y + 16;
        float w = body.Width - 40;
        Ui.Text(S.CmLogTitle, x, y, 22, Palette.Accent); y += 32;

        float colW = (w - 20) / 2;
        float leftX = x, rightX = x + colW + 20;
        float ly = y, ry = y;

        // [좌측 열] 보급품 총량 & 야전 병원
        Ui.Text(string.Format(S.CmSupplyTotal, state.Supply), leftX, ly, 18, Palette.Accent); ly += 28;

        Ui.Text(S.CmHospitalSecTitle, leftX, ly, 16, Palette.Accent); ly += 22;
        int wounded = state.Manpower?.Wounded ?? 0;
        float healCost = wounded * 0.5f;
        Ui.Text(string.Format(S.CmHospitalWait, wounded), leftX + 10, ly, 14, wounded > 0 ? Palette.Warning : Palette.TextDim); ly += 20;

        bool canHeal = wounded > 0 && state.Supply >= healCost;
        var healBtn = new Rectangle(leftX + 10, ly, 180, 32);
        if (Button(healBtn, S.CmBtnHeal, canHeal, 14, canHeal ? Palette.Accent : null))
        {
            int healed = CampaignEconomy.HealWounded(state);
            SaveLive();
            logisticsNotice = string.Format(S.CmHospitalDone, healed);
        }
        ly += 38;

        if (logisticsNotice != null)
        {
            Ui.Text(logisticsNotice, leftX + 10, ly, 13, Palette.HpGood); ly += 20;
        }

        // 소총 총 보유량 및 구경별 소총
        int totalRifles = state.Stock?.Rifles?.Values.Sum() ?? 0;
        Ui.Text(string.Format(S.CmRiflesTotal, totalRifles), leftX, ly, 16, Palette.Accent); ly += 22;
        var rf = state.Stock?.Rifles ?? new Dictionary<string, int>();
        Ui.Text(string.Format(S.CmRifleRu, rf.GetValueOrDefault(StockKeys.Ru)), leftX + 10, ly, 14, Palette.Text); ly += 18;
        Ui.Text(string.Format(S.CmRifleJp, rf.GetValueOrDefault(StockKeys.Jp)), leftX + 10, ly, 14, Palette.Text); ly += 18;
        Ui.Text(string.Format(S.CmRifleDe, rf.GetValueOrDefault(StockKeys.De)), leftX + 10, ly, 14, Palette.Text); ly += 18;
        Ui.Text(string.Format(S.CmRifleFr, rf.GetValueOrDefault(StockKeys.Fr)), leftX + 10, ly, 14, Palette.Text); ly += 18;
        Ui.Text(string.Format(S.CmRifleJpOld, rf.GetValueOrDefault(StockKeys.JpOld)), leftX + 10, ly, 14, Palette.Text); ly += 24;

        // 군마
        Ui.Text(string.Format(S.CmHorses, state.Stock?.Horses ?? 0), leftX, ly, 16, Palette.Text); ly += 24;

        // 다음 작전 보급 미리보기 (가용 중대 전원 · 구경별 탄약 · 포탄, CampaignHome.cs)
        ly += DrawSupplyPreview(leftX, ly, colW);

        // [우측 열] 탄약 5종 & 포탄 3종 & 최근 입고 5건
        Ui.Text(S.CmAmmoSecTitle, rightX, ry, 16, Palette.Accent); ry += 22;
        var am = state.Stock?.Ammo ?? new Dictionary<string, int>();
        Ui.Text(string.Format(S.CmAmmoRu, am.GetValueOrDefault(StockKeys.Ru)), rightX + 10, ry, 14, Palette.Text); ry += 18;
        Ui.Text(string.Format(S.CmAmmoJp, am.GetValueOrDefault(StockKeys.Jp)), rightX + 10, ry, 14, Palette.Text); ry += 18;
        Ui.Text(string.Format(S.CmAmmoDe, am.GetValueOrDefault(StockKeys.De)), rightX + 10, ry, 14, Palette.Text); ry += 18;
        Ui.Text(string.Format(S.CmAmmoFr, am.GetValueOrDefault(StockKeys.Fr)), rightX + 10, ry, 14, Palette.Text); ry += 18;
        Ui.Text(string.Format(S.CmAmmoJpOld, am.GetValueOrDefault(StockKeys.JpOld)), rightX + 10, ry, 14, Palette.Text); ry += 24;

        Ui.Text(S.CmShellsSecTitle, rightX, ry, 16, Palette.Accent); ry += 22;
        var sh = state.Stock?.Shells ?? new Dictionary<string, int>();
        Ui.Text(string.Format(S.CmShellInfGun, sh.GetValueOrDefault(StockKeys.InfGun)), rightX + 10, ry, 14, Palette.Text); ry += 18;
        Ui.Text(string.Format(S.CmShellField, sh.GetValueOrDefault(StockKeys.Field)), rightX + 10, ry, 14, Palette.Text); ry += 18;
        Ui.Text(string.Format(S.CmShellHowitzer, sh.GetValueOrDefault(StockKeys.Howitzer)), rightX + 10, ry, 14, Palette.Text); ry += 26;

        // 최근 입고·정산 내역 5건
        Ui.Text(S.CmRecentLogTitle, rightX, ry, 16, Palette.Accent); ry += 22;
        var logs = state.Stock?.RecentLog ?? new List<string>();
        if (logs.Count == 0)
        {
            Ui.Text(S.CmRecentLogEmpty, rightX + 10, ry, 14, Palette.TextDim);
        }
        else
        {
            foreach (var log in logs.AsEnumerable().Reverse().Take(5))
            {
                Ui.Text($"• {log}", rightX + 10, ry, 13, Palette.Text); ry += 18;
            }
        }
    }

    // ---------------------------------------------------------------- 4. 지도 탭

    /// <summary>작전 노선도 거점 i 를 점령하는 미션 id (거점 순서대로; 미션이 아직 없는 거점은 배열 밖 = 항상 미완료).</summary>
    static readonly string[] RouteMissionIds = { CampaignState.Tutorial2, CampaignState.YaluCrossing };
    bool RouteDone(int node) => node >= 0 && node < RouteMissionIds.Length && state.Completed.Contains(RouteMissionIds[node]);

    void DrawTabMap(Rectangle body)
    {
        float x = body.X + 20, y = body.Y + 16;
        float w = body.Width - 40;
        Ui.Text(S.CmMapTitle, x, y, 22, Palette.Accent);
        // 튜토리얼 / 0장 연습: 항상 가능 · 캠페인 상태 · 경제 · 기록을 바꾸지 않는다 (결과 파일도 안 씀)
        float tbw = 150, tby = y - 2;
        if (Button(new Rectangle(x + w - tbw, tby, tbw, 28), S.CmBtnTutorial2, true, 13)) OpenPractice = CampaignState.Tutorial2;
        if (Button(new Rectangle(x + w - tbw * 2 - 8, tby, tbw, 28), S.CmBtnTutorial1, true, 13)) OpenPractice = CampaignState.FirstMission;
        Ui.Text(S.CmTutorialNote, x + w - tbw * 2 - 8 - Ui.Measure(S.CmTutorialNote, 12).X - 10, tby + 8, 12, Palette.TextDim);
        y += 30;

        // 상단: 만주 북진 작전 경로 노선도 — 확대 · 이동 가능 (T47, CampaignMapView.cs)
        float mapH = Math.Clamp(body.Y + body.Height - y - 16 - 240, 150, 280);   // 아래 작전 목록(240) 을 남기고 세로 여유만큼
        DrawMapView(new Rectangle(x, y, w, mapH));
        y += mapH + 16;

        // 하단: 작전 미션 목록
        Ui.Text("선택 가능한 작전 목록:", x, y, 16, Palette.Accent); y += 24;

        (string Id, string Name, string Desc)[] missions = {
            (CampaignState.FirstMission, S.CmMissionC001Name, S.CmMissionC001Desc),
            (CampaignState.Tutorial2, S.CmMissionC002Name, S.CmMissionC002Desc),
            (CampaignState.YaluCrossing, S.CmMissionC101Name, S.CmMissionC101Desc),
            ("c1_02", S.CmMissionNextName, S.CmMissionNextDesc)
        };

        for (int i = 0; i < missions.Length; i++)
        {
            var m = missions[i];
            bool done = state.Completed.Contains(m.Id);
            bool isNext = state.NextMission == m.Id || (string.IsNullOrEmpty(state.NextMission) && m.Id == CampaignState.FirstMission);
            bool available = MissionAvailable(m.Id);   // 파일 없는 다음 작전 = 준비 중

            var rowRect = new Rectangle(x, y, w, 44);
            bool hover = Ui.Contains(rowRect, mouse);
            Raylib.DrawRectangleRec(rowRect, hover ? Palette.BtnHover : Palette.Alpha(Palette.PanelDark, 140));
            Raylib.DrawRectangleLinesEx(rowRect, 1, m.Id == targetMissionId ? Palette.Accent : Palette.PanelBorder);

            Ui.Text(m.Name, rowRect.X + 14, rowRect.Y + 6, 16, available ? Palette.Text : Palette.TextDim);
            Ui.Text(m.Desc, rowRect.X + 14, rowRect.Y + 24, 13, Palette.TextDim);

            string statusTag = done ? S.CmMapStatusDone : isNext ? S.CmMapStatusCurrent : S.CmMapStatusLocked;
            Color tagCol = done ? Palette.HpGood : isNext ? Palette.Accent : Palette.TextDim;
            Ui.Text(statusTag, rowRect.X + rowRect.Width - 220, rowRect.Y + 12, 14, tagCol);

            var prepBtn = new Rectangle(rowRect.X + rowRect.Width - 110, rowRect.Y + 7, 96, 30);
            if (Button(prepBtn, S.CmBtnOpPrepare, available, 14, available ? Palette.Accent : null))
            {
                OpenPreOp(m.Id);
            }

            y += 50;
        }
    }

    // ---------------------------------------------------------------- 5. 본국 탭

    void DrawTabEvents(Rectangle body)
    {
        float x = body.X + 20, y = body.Y + 16;
        float w = body.Width - 40;
        Ui.Text(S.CmEventTitle, x, y, 22, Palette.Accent); y += 30;

        var events = CampaignEvents.AvailableEvents(state);
        if (events.Count == 0)
        {
            Ui.Text(S.CmEventNone, x, y, 16, Palette.TextDim);
            if (eventNotice != null) Ui.Text(eventNotice, x, y + 30, 15, Palette.HpGood);
            return;
        }

        // 좌측 이벤트 목록 (200px) & 우측 상세 카드
        float listW = 210;
        var listRect = new Rectangle(x, y, listW, body.Y + body.Height - y - 16);
        Ui.Panel(listRect, Palette.Alpha(Palette.PanelDark, 180), Palette.PanelBorder);

        float ey = listRect.Y + 6;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            var btnR = new Rectangle(listRect.X + 4, ey, listRect.Width - 8, 36);
            bool isSel = selectedEventIndex == i;
            if (Button(btnR, ev.Title, true, 14, isSel ? Palette.Accent : null))
            {
                selectedEventIndex = i;
            }
            ey += 40;
        }

        // 우측 이벤트 상세
        if (selectedEventIndex >= events.Count) selectedEventIndex = 0;
        var currentEv = events[selectedEventIndex];

        float cardX = x + listW + 16;
        float cardW = w - listW - 16;
        var cardRect = new Rectangle(cardX, y, cardW, listRect.Height);
        Ui.Panel(cardRect, Palette.Alpha(Palette.PanelDark, 220), Palette.PanelBorder);

        float cy = cardRect.Y + 14;
        float cx = cardRect.X + 16;
        Ui.Text(currentEv.Title, cx, cy, 20, Palette.Accent); cy += 28;
        Ui.Text(string.Format(S.CmEventSender, currentEv.Sender), cx, cy, 14, Palette.Text); cy += 20;

        Raylib.DrawLineEx(new Vector2(cx, cy + 4), new Vector2(cardRect.X + cardW - 16, cy + 4), 1, Palette.PanelBorder);
        cy += 14;

        Ui.Text(S.CmEventBody, cx, cy, 14, Palette.TextDim); cy += 20;
        foreach (var line in WrapText(currentEv.Text, cardW - 32, 14))
        {
            Ui.Text(line, cx, cy, 14, Palette.Text); cy += 20;
        }
        cy += 12;

        Raylib.DrawLineEx(new Vector2(cx, cy), new Vector2(cardRect.X + cardW - 16, cy), 1, Palette.PanelBorder);
        cy += 12;

        Ui.Text(S.CmEventChoices, cx, cy, 15, Palette.Accent); cy += 24;
        for (int ci = 0; ci < currentEv.Choices.Count; ci++)
        {
            var choice = currentEv.Choices[ci];
            var chBtn = new Rectangle(cx, cy, cardW - 32, 42);
            if (Button(chBtn, choice.Text, true, 14))
            {
                string res = CampaignEvents.ApplyChoice(state, currentEv.Id, ci);
                eventNotice = string.Format(S.CmEventApplied, res);
                SaveLive();
                selectedEventIndex = 0;
                break;
            }
            if (!string.IsNullOrEmpty(choice.Effect))
            {
                Ui.Text(string.Format(S.CmEventPreview, choice.Effect), cx + 10, cy + 24, 12, Palette.TextDim);
            }
            cy += 48;
        }

        if (eventNotice != null)
        {
            Ui.Text(eventNotice, cx, cy + 6, 14, Palette.HpGood);
        }
    }

    // ---------------------------------------------------------------- 작전 전 화면 (S3 Pre-Operation)

    void OpenPreOp(string missionId)
    {
        targetMissionId = missionId;
        currentView = ViewMode.PreOp;

        // 기본 투입 선택: 가용한 모든 중대 선택 (의무 중대 포함)
        preOpSelectedCompanyIds.Clear();
        foreach (var c in state.Roster)
        {
            if (c.Available) preOpSelectedCompanyIds.Add(c.Id);
        }
    }

    void DrawPreOpView(Rectangle panel)
    {
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 250), Palette.Accent);

        float x = panel.X + 24, y = panel.Y + 16;
        float w = panel.Width - 48;

        Ui.Text(S.CmPreTitle, x, y, 22, Palette.Accent); y += 32;

        var def = LoadMissionDefSafe(targetMissionId);
        int maxDeploy = GetMaxDeploySafe(def);

        // 상단 작전 브리핑 요약
        string missionTitle = def?.Title ?? targetMissionId;
        string dateLoc = $"{def?.Date ?? "1920"} · {def?.Location ?? "간도"}";
        Ui.Text($"{missionTitle} ({dateLoc})", x, y, 16, Palette.Text); y += 22;

        if (def?.Briefing != null)
        {
            foreach (var l in WrapText(def.BriefingFor(Flag), w, 14).Take(2))
            {
                Ui.Text(l, x, y, 14, Palette.TextDim); y += 18;
            }
        }
        y += 10;

        Raylib.DrawLineEx(new Vector2(x, y), new Vector2(x + w, y), 1, Palette.PanelBorder);
        y += 10;

        // 투입 부대 선택 영역 (대대 단위 + 개별 중대 체크박스)
        Ui.Text(S.CmPreDeploySelection, x, y, 16, Palette.Accent);

        // [전체 선택] / [전체 해제] 버튼
        var selAllBtn = new Rectangle(x + 160, y - 2, 90, 26);
        if (Button(selAllBtn, S.CmPreBtnSelectAll, true, 13))
        {
            foreach (var c in state.Roster) if (c.Available) preOpSelectedCompanyIds.Add(c.Id);
        }
        var deselAllBtn = new Rectangle(x + 258, y - 2, 90, 26);
        if (Button(deselAllBtn, S.CmPreBtnDeselectAll, true, 13))
        {
            preOpSelectedCompanyIds.Clear();
        }

        y += 30;

        // 중대 목록 체크박스 스크롤 영역
        var listArea = new Rectangle(x, y, w, 220);
        Ui.Panel(listArea, Palette.Alpha(Palette.Panel, 140), Palette.PanelBorder);

        const float rowH = 28;
        float totalH = state.Roster.Count * rowH;
        if (Ui.Contains(listArea, mouse)) rosterScroll -= Input.GetMouseWheelMove() * rowH * 2;
        rosterScroll = Math.Clamp(rosterScroll, 0, MathF.Max(0, totalH - listArea.Height));

        Raylib.BeginScissorMode((int)listArea.X, (int)listArea.Y, (int)listArea.Width, (int)listArea.Height);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var c = state.Roster[i];
            float ry = listArea.Y + i * rowH - rosterScroll;
            if (ry + rowH < listArea.Y || ry > listArea.Y + listArea.Height) continue;

            bool isSel = preOpSelectedCompanyIds.Contains(c.Id);
            var cbRect = new Rectangle(listArea.X + 8, ry + 4, 18, 18);
            if (Checkbox(cbRect, isSel, c.Available))
            {
                if (isSel) preOpSelectedCompanyIds.Remove(c.Id);
                else preOpSelectedCompanyIds.Add(c.Id);
            }

            float cx = listArea.X + 34;
            string bnName = !string.IsNullOrEmpty(c.BattalionName) ? c.BattalionName : $"{c.BattalionNumber}대대";
            Ui.Text(bnName, cx, ry + 5, 14, Palette.TextDim); cx += 100;
            Ui.Text(c.Name, cx, ry + 5, 14, c.Available ? Palette.Text : Palette.TextDim); cx += 130;
            Ui.Text(c.Kind, cx, ry + 5, 14, Palette.TextDim); cx += 90;
            Ui.Text($"{c.Strength}명", cx, ry + 5, 14, Palette.Text); cx += 80;

            if (c.Mandatory) Ui.Text(S.CmStatusMandatory, cx, ry + 5, 13, Palette.Accent);
            else if (!c.Available) Ui.Text(S.CmStatusUnavailable, cx, ry + 5, 13, Palette.HpLow);
        }
        Raylib.EndScissorMode();

        y += 230;

        // 선택 병력 집계 및 상한
        var selectedComps = state.Roster.Where(c => preOpSelectedCompanyIds.Contains(c.Id)).ToList();
        int totalStrength = selectedComps.Sum(c => c.Strength);
        float deployCost = CampaignEconomy.TotalDeployCost(selectedComps);

        Ui.Text(string.Format(S.CmPreDeployCap, totalStrength, maxDeploy), x, y, 16, totalStrength > maxDeploy ? Palette.HpLow : Palette.Text);
        y += 24;

        // 보급 비용 게이지 (바 그래프)
        float maxSupplyBarW = w - 240;
        float supplyRatio = Math.Clamp(deployCost / MathF.Max(1f, state.Supply), 0f, 1f);
        var gaugeBg = new Rectangle(x, y, maxSupplyBarW, 20);
        var gaugeFill = new Rectangle(x, y, maxSupplyBarW * supplyRatio, 20);

        Color gaugeCol = deployCost > state.Supply ? Palette.HpLow : Palette.HpGood;
        Raylib.DrawRectangleRec(gaugeBg, Palette.Alpha(Palette.PanelDark, 200));
        Raylib.DrawRectangleRec(gaugeFill, gaugeCol);
        Raylib.DrawRectangleLinesEx(gaugeBg, 1, Palette.PanelBorder);

        Ui.Text(string.Format(S.CmPreSupplyGauge, deployCost, state.Supply), x + maxSupplyBarW + 14, y + 2, 14, deployCost > state.Supply ? Palette.HpLow : Palette.Text);
        y += 28;

        // [TC-D] 에피소드 가용 보급 게이지 · 자금 · 통제 한도 사용량 (한 줄)
        {
            var ed = CampaignEconomyDefs.Current;
            float availEp = CampaignFunds.AvailableSupply(state);
            var (ctrlReg, ctrlFrn) = CampaignFunds.ControlUsage(selectedComps);
            bool epOver = deployCost > availEp;
            float epRatio = Math.Clamp(deployCost / MathF.Max(1f, ed.EpisodeSupplyStart), 0f, 1f);
            float availRatio = Math.Clamp(availEp / MathF.Max(1f, ed.EpisodeSupplyStart), 0f, 1f);
            var epBg = new Rectangle(x, y, maxSupplyBarW, 12);
            Raylib.DrawRectangleRec(epBg, Palette.Alpha(Palette.PanelDark, 200));
            Raylib.DrawRectangleRec(new Rectangle(x, y, maxSupplyBarW * availRatio, 12), Palette.Alpha(Palette.HpGood, 110));
            Raylib.DrawRectangleRec(new Rectangle(x, y, maxSupplyBarW * MathF.Min(epRatio, availRatio), 12), epOver ? Palette.HpLow : Palette.HpGood);
            Raylib.DrawRectangleLinesEx(epBg, 1, Palette.PanelBorder);
            y += 16;
            Ui.Text(string.Format(S.CmPreEpisodeSupply, availEp, ed.EpisodeSupplyStart, deployCost, state.Funds, CampaignFunds.ToSilver(state.Funds), ctrlReg, ed.RegularControlCap, ctrlFrn, ed.ForeignSlotCap),
                x, y, 13, epOver || CampaignFunds.CheckControl(selectedComps) != null ? Palette.HpLow : Palette.TextDim);
            y += 20;
        }

        // 경고 메시지 표시
        bool overSupply = deployCost > state.Supply;
        bool overCap = totalStrength > maxDeploy;
        bool noDeploy = totalStrength <= 0;
        bool mandatoryRefused = state.Roster.Any(c => c.Mandatory && !preOpSelectedCompanyIds.Contains(c.Id));

        if (overSupply) { Ui.Text(S.CmWarnOverSupply, x, y, 14, Palette.HpLow); y += 18; }
        if (overCap) { Ui.Text(S.CmWarnOverCap, x, y, 14, Palette.HpLow); y += 18; }
        if (noDeploy) { Ui.Text(S.CmWarnNoSelection, x, y, 14, Palette.Warning); y += 18; }
        if (mandatoryRefused) { Ui.Text(S.CmWarnMandatoryRefused, x, y, 14, Palette.Warning); y += 18; }

        // 하단 버튼 ([출격], [취소 / 뒤로])
        float by = panel.Y + panel.Height - 52;
        bool canLaunch = !overSupply && !overCap && !noDeploy && loadError == null && CampaignFunds.CheckControl(selectedComps) == null;   // TC-D 통제 한도   // 저장을 못 읽었으면 출격 기록을 저장할 수 없다

        var deployBtnRect = new Rectangle(panel.X + panel.Width - 24 - 180, by, 180, 40);
        if (Button(deployBtnRect, S.CmPreBtnDeploy, canLaunch, 18, canLaunch ? Palette.Accent : null))
        {
            if (!CampaignEconomy.BeginOperation(state, targetMissionId, preOpSelectedCompanyIds.ToList(), maxDeploy)) return;   // TC-D 한도 초과 (버튼은 이미 잠겨 있음)
            string mission = targetMissionId;
            // 작전 시작 직전 자동 저장 (= 재도전 지점). 실패하면 출격하지 않고 알림 + 다시 시도 (성공하면 그때 출격)
            Persist(() => CampaignSaves.SaveWithAuto(state, CampaignSaves.Auto.PreOp), () => OpenMission = mission);
        }

        var cancelBtnRect = new Rectangle(panel.X + 24, by, 140, 40);
        if (Button(cancelBtnRect, S.CmPreBtnCancel, true, 16))
        {
            currentView = ViewMode.Home;
        }
    }

    // ---------------------------------------------------------------- 작전 후 전과 보고 모달 (S4 Debrief)

    void DrawDebriefModal(int screenW, int screenH, CampaignState.DebriefReport d)
    {
        // 배경 어두운 반투명 딤
        Raylib.DrawRectangle(0, 0, screenW, screenH, Palette.Alpha(Color.Black, 180));

        float dw = MathF.Min(800, screenW - 32);
        float dh = MathF.Min(500, screenH - 48);
        var modal = new Rectangle((screenW - dw) / 2, (screenH - dh) / 2, dw, dh);

        Ui.Panel(modal, Palette.Alpha(Palette.PanelDark, 250), Palette.Accent);

        float x = modal.X + 28, y = modal.Y + 20;
        Ui.Text(S.CmDebriefTitle, x, y, 22, Palette.Accent); y += 30;

        string missionTitle = !string.IsNullOrEmpty(d.Title) ? d.Title : d.MissionId;
        Ui.Text($"작전: {missionTitle}", x, y, 16, Palette.Text); y += 26;

        Raylib.DrawLineEx(new Vector2(x, y), new Vector2(modal.X + modal.Width - 28, y), 1, Palette.PanelBorder);
        y += 14;

        // 인명 손실
        Ui.Text(string.Format(S.CmDebriefLosses, d.TotalLost, d.Dead, d.WoundedReturned, d.DischargedWounded), x, y, 15, Palette.Text); y += 22;
        // 적군 사살
        Ui.Text(string.Format(S.CmDebriefKills, d.EnemyKills), x, y, 15, Palette.HpGood); y += 26;

        // 전리품 노획
        int lootedRifles = CountFrom(d.LootedRifles);
        int lootedAmmo = CountFrom(d.LootedAmmo);
        Ui.Text(string.Format(S.CmDebriefLoot, d.LootedSupply, lootedRifles, lootedAmmo, d.LootedHorses), x, y, 15, Palette.Accent); y += 22;

        // 탄약 환원
        int ammoRefunded = CountFrom(d.AmmoRefunded);
        Ui.Text(string.Format(S.CmDebriefAmmoRefund, ammoRefunded), x, y, 15, Palette.Text); y += 22;

        // 포탄 정산 (T19b Q6): 포를 쏘았거나 노획했을 때만
        var fired = d.ShellsFired ?? new Dictionary<string, int>();
        var lootSh = d.LootedShells ?? new Dictionary<string, int>();
        if (fired.Count > 0 || lootSh.Count > 0)
        {
            Ui.Text(string.Format(S.CmDebriefShells, fired.Values.Sum(), fired.GetValueOrDefault(StockKeys.InfGun), fired.GetValueOrDefault(StockKeys.Field), fired.GetValueOrDefault(StockKeys.Howitzer), lootSh.Values.Sum()), x, y, 15, Palette.Text); y += 22;
        }

        // 포로 (T42c): 있을 때만
        if (d.PrisonersTaken > 0) { Ui.Text(string.Format(S.CmDebriefPrisoners, d.PrisonersTaken), x, y, 15, Palette.Accent); y += 22; }

        // 정기 보급 도착
        Ui.Text(string.Format(S.CmDebriefRegular, d.RegularSupplyAdded, d.RecruitsAdded), x, y, 15, Palette.HpGood); y += 22;

        // 보급고 변화
        Ui.Text(string.Format(S.CmDebriefSupplyChange, d.SupplyBefore, d.SupplyAfter), x, y, 15, Palette.TextDim); y += 26;

        // 이벤트 발생 안내
        if (!string.IsNullOrEmpty(d.TriggeredEventId))
        {
            Ui.Text(S.CmDebriefNewEvent, x, y, 14, Palette.Warning); y += 22;
        }

        // 닫기 버튼
        float by = modal.Y + modal.Height - 52;
        var closeBtn = new Rectangle(modal.X + modal.Width - 180 - 28, by, 180, 38);
        if (Button(closeBtn, S.CmDebriefClose, true, 16, Palette.Accent))
        {
            state.LastDebrief = null;
            SaveLive();
            currentView = ViewMode.Home;
        }
    }

    // ---------------------------------------------------------------- 저장 슬롯 창 · 저장 실패 알림 (T19a)

    void OpenSlots()
    {
        slotsOpen = true;
        slotArm = slotNotice = null;
        RefreshSlots();
    }

    void RefreshSlots()
    {
        slotInfo = new CampaignSaves.Info[CampaignSaves.SlotCount + 2];
        for (int i = 0; i < CampaignSaves.SlotCount; i++) slotInfo[i] = CampaignSaves.Describe(CampaignSaves.SlotPath(i + 1));
        slotInfo[CampaignSaves.SlotCount] = CampaignSaves.Describe(CampaignSaves.AutoPath(CampaignSaves.Auto.PreOp));
        slotInfo[CampaignSaves.SlotCount + 1] = CampaignSaves.Describe(CampaignSaves.AutoPath(CampaignSaves.Auto.Settle));
    }

    static string SlotPathOf(int row) => row < CampaignSaves.SlotCount ? CampaignSaves.SlotPath(row + 1)
        : CampaignSaves.AutoPath(row == CampaignSaves.SlotCount ? CampaignSaves.Auto.PreOp : CampaignSaves.Auto.Settle);

    static string SlotLabelOf(int row) => row < CampaignSaves.SlotCount ? string.Format(S.CmSlotName, row + 1)
        : row == CampaignSaves.SlotCount ? S.CmAutoPreOp : S.CmAutoSettle;

    void DrawSlots(int screenW, int screenH)
    {
        Raylib.DrawRectangle(0, 0, screenW, screenH, Palette.Alpha(Color.Black, 180));
        float dw = MathF.Min(820, screenW - 32), dh = MathF.Min(560, screenH - 32);
        var modal = new Rectangle((screenW - dw) / 2, (screenH - dh) / 2, dw, dh);
        Ui.Panel(modal, Palette.Alpha(Palette.PanelDark, 250), Palette.Accent);
        float x = modal.X + 24, y = modal.Y + 16;
        Ui.Text(S.CmSlotsTitle, x, y, 20, Palette.Accent); y += 34;

        string? hoverReason = null;
        int rows = CampaignSaves.SlotCount + 2;
        float rowH = MathF.Min(34, (modal.Height - 130) / rows);
        for (int row = 0; row < rows && row < slotInfo.Length; row++)
        {
            var info = slotInfo[row];
            bool auto = row >= CampaignSaves.SlotCount;
            float ry = y + row * rowH + (auto ? 10 : 0);
            bool rejected = info.Error != null;   // T49a: 못 여는 칸 = 회색 행 + 흐린 이름 + 사유 (버튼 앞에서 줄이고, 전체 사유는 마우스를 올리면 아래에)
            float descX = x + 200, descMaxW = modal.X + modal.Width - 24 - 110 - 118 - 12 - descX;
            if (rejected) Raylib.DrawRectangleRounded(new Rectangle(x - 8, ry, modal.Width - 32, rowH - 4), 0.2f, 4, Palette.Alpha(Palette.TextDim, 30));
            Ui.Text(SlotLabelOf(row), x, ry + 6, 15, rejected ? Palette.TextDim : auto ? Palette.Accent : Palette.Text);
            string desc = !info.Exists ? S.CmSlotEmpty
                : rejected ? string.Format(S.CmSlotBroken, info.Error)
                : string.Format(S.CmSlotSummary, info.SavedAt, info.NextName, info.Completed, info.Supply);
            if (rejected)
            {
                if (Ui.Contains(new Rectangle(x - 8, ry, descX + descMaxW - x + 8, rowH - 4), mouse)) hoverReason = desc;
                while (desc.Length > 4 && Ui.Measure(desc, 13).X > descMaxW) desc = desc[..^4] + "...";
            }
            Ui.Text(desc, descX, ry + 7, 13, rejected ? Palette.Warning : Palette.TextDim);

            float bx = modal.X + modal.Width - 24 - 110;
            string loadKey = "load" + row, saveKey = "save" + row;
            bool canLoad = info.Exists && info.Error == null;
            if (Button(new Rectangle(bx, ry + 2, 110, rowH - 6), slotArm == loadKey ? S.CmSlotLoadConfirm : S.CmSlotLoad, canLoad, 13))
            {
                if (slotArm != loadKey) slotArm = loadKey;
                else LoadSlot(row);
            }
            if (!auto)
            {
                bool canSave = loadError == null && info.Error == null;   // 깨진 · 새 버전 칸은 덮어쓰지 않는다
                if (Button(new Rectangle(bx - 118, ry + 2, 110, rowH - 6), slotArm == saveKey ? S.CmSlotOverwrite : S.CmSlotSave, canSave, 13, slotArm == saveKey ? Palette.Warning : null))
                {
                    if (info.Exists && slotArm != saveKey) slotArm = saveKey;   // 덮어쓰기는 한 번 더 눌러 확인
                    else
                    {
                        int slot = row + 1;
                        slotArm = null;
                        Persist(() => CampaignSaves.SaveSlot(state, slot), () => { slotNotice = string.Format(S.CmSlotSaved, slot); RefreshSlots(); });
                    }
                }
            }
        }

        float by = modal.Y + modal.Height - 50;
        if (hoverReason != null) { string hr = hoverReason; while (hr.Length > 4 && Ui.Measure(hr, 13).X > modal.Width - 48) hr = hr[..^4] + "..."; Ui.Text(hr, x, by - 46, 13, Palette.Warning); }
        if (loadError != null) Ui.Text(S.CmSlotNoSaveBroken, x, by - 26, 13, Palette.Warning);
        else if (slotInfo.Any(i => i.Error != null)) Ui.Text(S.CmSlotBrokenKept, x, by - 26, 13, Palette.Warning);
        if (slotNotice != null) Ui.Text(slotNotice, x, by + 10, 14, Palette.HpGood);
        if (Button(new Rectangle(modal.X + modal.Width - 24 - 140, by, 140, 36), S.CmSlotClose, true, 15))
        {
            slotsOpen = false;
            slotArm = null;
        }
    }

    /// <summary>슬롯 · 자동 저장을 진행 저장(campaign.json)으로 되돌린다. 작전 직전 자동 저장 = 재도전 복귀.</summary>
    void LoadSlot(int row)
    {
        slotArm = null;
        var loaded = CampaignSaves.Read(SlotPathOf(row), out string? err);
        if (loaded == null) { slotNotice = err ?? S.CmSlotEmpty; RefreshSlots(); return; }
        string label = SlotLabelOf(row);
        Persist(() =>
        {
            if (loadError != null) CampaignState.BackupBroken();   // 읽지 못한 원본은 .bak 로 보존한 뒤 덮어쓴다
            return loaded.Save();
        }, () =>
        {
            loadError = null;
            Adopt(loaded);
            slotNotice = string.Format(S.CmSlotLoaded, label);
            RefreshSlots();
        });
    }

    void DrawSaveFailed(int screenW, int screenH)
    {
        Raylib.DrawRectangle(0, 0, screenW, screenH, Palette.Alpha(Color.Black, 160));
        float dw = MathF.Min(560, screenW - 32), dh = 170;
        var modal = new Rectangle((screenW - dw) / 2, (screenH - dh) / 2, dw, dh);
        Ui.Panel(modal, Palette.Alpha(Palette.PanelDark, 252), Palette.HpLow);
        Ui.Text(S.CmSaveFailedTitle, modal.X + 24, modal.Y + 16, 20, Palette.HpLow);
        foreach (var (l, i) in WrapText(S.CmSaveFailedText, dw - 48, 14).Select((l, i) => (l, i)))
            Ui.Text(l, modal.X + 24, modal.Y + 52 + i * 18, 14, Palette.Text);
        float by = modal.Y + modal.Height - 52;
        if (Button(new Rectangle(modal.X + modal.Width - 24 - 150, by, 150, 38), S.CmSaveRetry, true, 16, Palette.Accent))
            Persist(saveRetry!, saveThen);
        else if (Button(new Rectangle(modal.X + 24, by, 180, 38), S.CmSaveGiveUp, true, 14))
        {
            saveRetry = null;
            saveThen = null;
        }
    }
}
