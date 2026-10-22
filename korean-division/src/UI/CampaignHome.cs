using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T19c] 캠페인 홈 3화면 · 15탭 틀 (설계 CAMPAIGN_HOME_DESIGN §2 · §11-1, 사용자 Q1 2026-10-22: 중앙 9 + 좌 3 + 우 3).
/// 탭 추가 = <see cref="Tab"/> 열거형 · <see cref="ScreenTabs"/> 배치 · 문자열 (StringsCampaignHome) 만 건드린다 (§14).
/// MVP 5탭 (1 · 2 · 4 · 10 · 13) 본문은 CampaignMenu.cs, 6번째 본문 8 참모진은 읽기 전용 (CampaignHomeStaff.cs, T19d), 나머지 9탭은 틀 (<see cref="DrawTabPlaceholder"/>).
/// </summary>
public sealed partial class CampaignMenu
{
    /// <summary>탭 번호 = 설계 §2 번호.</summary>
    enum Tab
    {
        Summary = 1, Roster = 2, Organization = 3, Logistics = 4, Equipment = 5, Training = 6, Diplomacy = 7, Staff = 8,
        Promotion = 9, Map = 10, Intel = 11, Records = 12, Events = 13, Schedule = 14, Research = 15
    }

    enum Screen { Left, Center, Right }

    /// <summary>화면별 탭 (§11-1 제안 F1: 중앙 1·2·3·4·5·6·10·13·15 / 좌 11·12·14 / 우 7·8·9).</summary>
    static readonly Tab[][] ScreenTabs =
    {
        new[] { Tab.Intel, Tab.Records, Tab.Schedule },
        new[] { Tab.Summary, Tab.Roster, Tab.Organization, Tab.Logistics, Tab.Equipment, Tab.Training, Tab.Map, Tab.Events, Tab.Research },
        new[] { Tab.Diplomacy, Tab.Staff, Tab.Promotion },
    };

    static bool IsMvp(Tab t) => t is Tab.Summary or Tab.Roster or Tab.Logistics or Tab.Map or Tab.Events or Tab.Staff || IsReadonlyView(t) || t == Tab.Promotion || t == Tab.Diplomacy;

    static string TabLabel(Tab t) => t switch
    {
        Tab.Summary => S.CmTabSummary, Tab.Roster => S.CmTabRoster, Tab.Logistics => S.CmTabLogistics, Tab.Map => S.CmTabMap, Tab.Events => S.CmTabEvents,
        Tab.Organization => S.CmTabOrg, Tab.Equipment => S.CmTabEquip, Tab.Training => S.CmTabTraining, Tab.Diplomacy => S.CmTabDiplomacy,
        Tab.Staff => S.CmTabStaff, Tab.Promotion => S.CmTabPromotion, Tab.Intel => S.CmTabIntel, Tab.Records => S.CmTabRecords,
        Tab.Schedule => S.CmTabSchedule, Tab.Research => S.CmTabResearch, _ => t.ToString()
    };

    static (string Title, string Desc) PlaceholderText(Tab t) => t switch
    {
        Tab.Organization => (S.CmPhOrgTitle, S.CmPhOrgDesc), Tab.Equipment => (S.CmPhEquipTitle, S.CmPhEquipDesc),
        Tab.Training => (S.CmPhTrainingTitle, S.CmPhTrainingDesc), Tab.Diplomacy => (S.CmPhDiplomacyTitle, S.CmPhDiplomacyDesc),
        Tab.Staff => (S.CmPhStaffTitle, S.CmPhStaffDesc), Tab.Promotion => (S.CmPhPromotionTitle, S.CmPhPromotionDesc),
        Tab.Intel => (S.CmPhIntelTitle, S.CmPhIntelDesc), Tab.Records => (S.CmPhRecordsTitle, S.CmPhRecordsDesc),
        Tab.Schedule => (S.CmPhScheduleTitle, S.CmPhScheduleDesc), Tab.Research => (S.CmPhResearchTitle, S.CmPhResearchDesc),
        _ => (TabLabel(t), "")
    };

    Screen currentScreen = Screen.Center;
    /// <summary>화면마다 마지막으로 연 탭 (화면을 바꿨다 돌아오면 그 탭).</summary>
    readonly Tab[] lastTabOf = { Tab.Intel, Tab.Summary, Tab.Diplomacy };

    void SelectTab(Tab t)
    {
        currentTab = t;
        for (int s = 0; s < ScreenTabs.Length; s++)
            if (Array.IndexOf(ScreenTabs[s], t) >= 0) { currentScreen = (Screen)s; lastTabOf[s] = t; }
    }

    void SelectScreen(Screen s)
    {
        currentScreen = s;
        currentTab = lastTabOf[(int)s];
    }

    /// <summary>확인 · 스크린샷용: KD_CAMPAIGN_VIEW = left | center | right [:탭 번호] (예 "right", "center:4").</summary>
    void ApplyViewOverride()
    {
        string? v = Environment.GetEnvironmentVariable("KD_CAMPAIGN_VIEW");
        if (string.IsNullOrWhiteSpace(v)) return;
        var parts = v.Trim().ToLowerInvariant().Split(':');
        Screen? scr = parts[0] switch { "left" => Screen.Left, "center" => Screen.Center, "right" => Screen.Right, _ => null };
        if (scr is { } sc) SelectScreen(sc);
        if (parts.Length > 1 && int.TryParse(parts[1], out int n) && Enum.IsDefined(typeof(Tab), n)) SelectTab((Tab)n);
        ApplyCommanderSubOverride();
    }

    // ---------------------------------------------------------------- 화면 띠 · 탭 열

    const float ScreenStripH = 28;

    /// <summary>본문 위 3화면 띠 (좌 · 중앙 · 우). 띠 아래 남는 본문 영역을 돌려준다.</summary>
    Rectangle DrawScreenStrip(Rectangle area)
    {
        string[] labels = { S.CmScreenLeft, S.CmScreenCenter, S.CmScreenRight };
        float gap = 6, bw = (area.Width - gap * 2) / 3;
        for (int i = 0; i < 3; i++)
        {
            var r = new Rectangle(area.X + i * (bw + gap), area.Y, bw, ScreenStripH);
            bool cur = (int)currentScreen == i;
            string lbl = $"{labels[i]} ({ScreenTabs[i].Length})";
            if (Button(r, lbl, true, 14, cur ? Palette.Accent : Palette.TextDim)) SelectScreen((Screen)i);
            if (cur) Raylib.DrawLineEx(new Vector2(r.X + 6, r.Y + r.Height - 2), new Vector2(r.X + r.Width - 6, r.Y + r.Height - 2), 2, Palette.Accent);
        }
        float top = ScreenStripH + 6;
        return new Rectangle(area.X, area.Y + top, area.Width, area.Height - top);
    }

    /// <summary>왼쪽 탭 열: 현재 화면의 탭들 (틀만 있는 탭은 흐린 글씨). bottom = 탭 열이 내려갈 수 있는 끝 y.</summary>
    void DrawTabColumn(float x, float y, float w, float bottom)
    {
        var tabs = ScreenTabs[(int)currentScreen];
        const float gap = 6;
        float h = Math.Clamp((bottom - y) / tabs.Length - gap, 22, 36);
        int pending = CampaignEvents.AvailableEvents(state).Count;
        for (int i = 0; i < tabs.Length; i++)
        {
            var t = tabs[i];
            var r = new Rectangle(x, y + i * (h + gap), w, h);
            string lbl = TabLabel(t);
            if (t == Tab.Events && pending > 0) lbl += $" ({pending})";
            Color col = currentTab == t ? Palette.Accent : IsMvp(t) ? Palette.Text : Palette.TextDim;
            if (Button(r, lbl, true, h >= 30 ? 16 : 14, col)) SelectTab(t);
        }
    }

    /// <summary>후속 탭 자리: 이름 · 설계 §2 한 줄 · "후속" 안내 (상태를 읽지도 바꾸지도 않는다).</summary>
    void DrawTabPlaceholder(Rectangle body, Tab t)
    {
        float x = body.X + 20, y = body.Y + 16;
        var (title, desc) = PlaceholderText(t);
        Ui.Text(title, x, y, 22, Palette.Accent); y += 36;
        foreach (var l in WrapText(desc, body.Width - 40, 16)) { Ui.Text(l, x, y, 16, Palette.Text); y += 22; }
        y += 12;
        Ui.Text(S.CmPlaceholderNote, x, y, 15, Palette.TextDim);
    }

    // ---------------------------------------------------------------- 2 신병 · 부대: 연대 배속 (Q9)

    /// <summary>연대 칸: [-] N연대 [+] (대대 중대는 대대 단위로 함께). 바뀌면 진행 저장.</summary>
    void DrawRegimentCell(CampaignState.RosterCompany c, float x, float y, float h)
    {
        const float bw = 20;
        var minus = new Rectangle(x, y + 3, bw, h - 8);
        var plus = new Rectangle(x + 58, y + 3, bw, h - 8);
        string name = c.Regiment > 0 ? string.Format(S.CmRegimentName, c.Regiment) : S.CmRegimentNone;
        var mid = new Rectangle(x + bw, y, 58 - bw, h - 2);
        Ui.TextCentered(name, mid, 13, c.Regiment > 0 ? Palette.Text : Palette.TextDim);
        int delta = Button(minus, "-", true, 14) ? -1 : Button(plus, "+", true, 14) ? 1 : 0;
        if (delta != 0 && CampaignRegiments.Attach(state, c.Id, CampaignRegiments.Step(c.Regiment, delta)) > 0) SaveLive();
    }

    /// <summary>부대 탭 제목 줄 오른쪽: 연대별 배속 요약.</summary>
    void DrawRegimentSummary(float right, float y)
    {
        var n = CampaignRegiments.CompanyCounts(state);
        var parts = new List<string>();
        for (int r = 1; r <= CampaignRegiments.Max; r++) if (n[r] > 0) parts.Add(string.Format(S.CmRegimentSummaryPart, r, n[r]));
        if (n[0] > 0) parts.Add(string.Format(S.CmRegimentSummaryDirect, n[0]));
        string text = string.Format(S.CmRegimentSummary, string.Join(" · ", parts));
        Vector2 sz = Ui.Measure(text, 13);
        Ui.Text(text, right - sz.X, y, 13, Palette.Text);
        Vector2 hz = Ui.Measure(S.CmRegimentHint, 12);
        Ui.Text(S.CmRegimentHint, right - hz.X, y + 16, 12, Palette.TextDim);
    }

    // ---------------------------------------------------------------- 4 병참: 보급 미리보기

    string? previewKey;
    CampaignState.PendingOperation? previewOp;
    int previewCompanies, previewMen;
    float previewCost;
    Dictionary<string, int> previewShellBase = new();

    /// <summary>다음 작전 (targetMissionId) 에 가용 중대 전원을 보낼 때의 소모 · 적재 — 상태 사본에 BeginOperation 을 돌려 같은 규칙으로 계산 (진짜 상태는 그대로).</summary>
    void UpdatePreview()
    {
        string key = $"{targetMissionId}|{state.Supply}|{string.Join(",", state.Roster.Select(c => $"{c.Id}:{c.Strength}:{c.Available}:{c.Caliber}:{c.Kind}"))}|{string.Join(",", state.Stock.Ammo.Select(kv => $"{kv.Key}={kv.Value}"))}|{string.Join(",", state.Stock.Shells.Select(kv => $"{kv.Key}={kv.Value}"))}";
        if (key == previewKey) return;
        previewKey = key;
        previewOp = null;
        var copy = CampaignState.FromJson(state.ToJson());
        if (copy == null) return;
        var ids = copy.Roster.Where(c => c.Available).Select(c => c.Id).ToList();
        var selected = copy.Roster.Where(c => c.Available).ToList();
        int maxDeploy = GetMaxDeploySafe(LoadMissionDefSafe(targetMissionId));
        previewCompanies = selected.Count;
        previewMen = selected.Sum(c => c.Strength);
        previewCost = CampaignEconomy.DeployCost(selected, maxDeploy);
        previewShellBase = CampaignEconomy.ShellBaseLoad(targetMissionId);
        CampaignEconomy.BeginOperation(copy, targetMissionId, ids, maxDeploy, previewShellBase);
        previewOp = copy.Pending;
    }

    /// <summary>병참 탭 보급 미리보기 칸 (x, y 에서 아래로). 그린 높이를 돌려준다.</summary>
    float DrawSupplyPreview(float x, float y, float w)
    {
        UpdatePreview();
        float y0 = y;
        Ui.Text(S.CmPreviewTitle, x, y, 16, Palette.Accent); y += 22;
        if (previewOp == null) return y - y0;
        Ui.Text(string.Format(S.CmPreviewBasis, CampaignState.ShortNameOf(targetMissionId), previewCompanies, previewMen), x + 10, y, 13, Palette.TextDim); y += 18;
        bool lack = previewCost > state.Supply;
        Ui.Text(lack ? string.Format(S.CmPreviewSupplyLack, previewCost, state.Supply) : string.Format(S.CmPreviewSupply, previewCost, state.Supply - previewCost),
            x + 10, y, 14, lack ? Palette.HpLow : Palette.Text); y += 18;

        var selected = state.Roster.Where(c => c.Available).ToList();
        bool anyAmmo = false;
        foreach (var cal in StockKeys.AllCalibers)
        {
            int need = selected.Where(c => c.Caliber == cal).Sum(CampaignEconomy.CompanyAmmoNeed);
            if (need <= 0) continue;
            anyAmmo = true;
            int load = previewOp.AmmoByCaliber.GetValueOrDefault(cal);
            Ui.Text(string.Format(S.CmPreviewLoad, S.CmStockShort(cal), need, load, state.Stock.Ammo.GetValueOrDefault(cal)), x + 10, y, 13, load < need ? Palette.Warning : Palette.Text); y += 17;
        }
        if (!anyAmmo) { Ui.Text(S.CmPreviewNoAmmo, x + 10, y, 13, Palette.TextDim); y += 17; }
        if (previewOp.ShellsLoaded.Count == 0) { Ui.Text(S.CmPreviewNoGuns, x + 10, y, 13, Palette.TextDim); y += 17; }
        else
            foreach (var (shell, load) in previewOp.ShellsLoaded)
            {
                int need = previewShellBase.GetValueOrDefault(shell);
                Ui.Text(string.Format(S.CmPreviewLoad, S.CmStockShort(shell), need, load, state.Stock.Shells.GetValueOrDefault(shell)), x + 10, y, 13, load < need ? Palette.Warning : Palette.Text); y += 17;
            }
        return y - y0;
    }
}
