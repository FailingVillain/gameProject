using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T38f] 읽기 전용 탭 3종: 12 부대 기록 · 5 장비 재고 · 3 편제 트리. 표시 자료는 CampaignHomeViews (상태를 바꾸지 않음, 버튼 없음).
/// </summary>
public sealed partial class CampaignMenu
{
    static bool IsReadonlyView(Tab t) => t is Tab.Records or Tab.Equipment or Tab.Organization or Tab.Schedule;

    void DrawTabRecords(Rectangle body)
    {
        var v = CampaignHomeViews.Records(state);
        float x = body.X + 20, y = body.Y + 16, w = body.Width - 40;
        Ui.Text(S.CmRecTitle, x, y, 22, Palette.Accent); y += 32;
        Ui.Text(string.Format(S.CmRecSummary, v.Missions, v.TotalKills, v.TotalLost), x, y, 16, Palette.Text); y += 30;

        Ui.Text(string.Format(S.CmRecRecentHead, CampaignHomeViews.RecentMax), x, y, 16, Palette.Accent); y += 22;
        if (v.Recent.Count == 0) { Ui.Text(S.CmRecEmpty, x + 10, y, 14, Palette.TextDim); y += 20; }
        foreach (var r in v.Recent)
        {
            string line = (r.Day > 0 ? string.Format(S.CmRecDay, r.Day) : "") + string.Format(S.CmRecRow, r.Title, r.Kills, r.Lost, r.Survivors);
            Ui.Text(line, x + 10, y, 14, Palette.Text);
            if (r.SavedAt.Length > 0) { var sz = Ui.Measure(r.SavedAt, 12); Ui.Text(r.SavedAt, x + w - sz.X, y + 1, 12, Palette.TextDim); }
            y += 20;
        }
        y += 10;
        Ui.Text(S.CmRecTopHead, x, y, 16, Palette.Accent); y += 22;
        if (v.Top.Count == 0) { Ui.Text(S.CmRecTopEmpty, x + 10, y, 14, Palette.TextDim); y += 20; }
        foreach (var t in v.Top) { Ui.Text(string.Format(S.CmRecTopRow, t.Name, t.Kills), x + 10, y, 14, Palette.Text); y += 20; }
        y += 10;
        Ui.Text(S.CmRecDebriefHead, x, y, 16, Palette.Accent); y += 22;
        var d = v.Debrief;
        if (d == null) Ui.Text(S.CmRecDebriefEmpty, x + 10, y, 14, Palette.TextDim);
        else Ui.Text(string.Format(S.CmRecDebrief, string.IsNullOrEmpty(d.Title) ? d.MissionId : d.Title, d.Dead, d.WoundedReturned, d.DischargedWounded, d.EnemyKills), x + 10, y, 14, Palette.Text);
        if (CommanderHomeModel.RulesOn && y + 130 < body.Y + body.Height - 52) DrawCommanderLog(x, y + 30, 3);   // T48d 인사 일지
        ReadonlyFooter(body, S.CmRecLater);
    }

    /// <summary>[T42d] 탭 14 일정: 현재 일수 + 연표 (이벤트 발동 · 작전 결과 시간순, 최신 위). 읽기 전용.</summary>
    void DrawTabSchedule(Rectangle body)
    {
        var v = CampaignHomeViews.Schedule(state);
        float x = body.X + 20, y = body.Y + 16, w = body.Width - 40;
        Ui.Text(S.CmSchTitle, x, y, 22, Palette.Accent); y += 34;
        Ui.Text(string.Format(S.CmSchDay, v.Day), x, y, 18, Palette.Text); y += 26;
        Ui.Text(string.Format(S.CmSchCounts, v.Missions, v.Events), x, y, 15, Palette.TextDim); y += 30;
        Ui.Text(string.Format(S.CmSchHead, CampaignHomeViews.ScheduleMax), x, y, 16, Palette.Accent); y += 24;
        if (v.Rows.Count == 0) Ui.Text(S.CmSchEmpty, x + 10, y, 14, Palette.TextDim);
        foreach (var r in v.Rows)
        {
            var dayLabel = string.Format(S.CmSchRowDay, r.Day);
            Ui.Text(dayLabel, x + 10, y, 14, Palette.TextDim);
            Ui.Text(r.Text, x + 80, y, 14, r.IsEvent ? Palette.Accent : Palette.Text);
            y += 21;
        }
        ReadonlyFooter(body, S.CmSchLater);
    }

    void DrawTabEquipment(Rectangle body)
    {
        var v = CampaignHomeViews.Equipment(state);
        float x = body.X + 20, y = body.Y + 16;
        Ui.Text(S.CmEqTitle, x, y, 22, Palette.Accent); y += 34;
        float c0 = x + 10, c1 = x + 150, c2 = x + 290, c3 = x + 450;
        Ui.Text(S.CmEqColCaliber, c0, y, 14, Palette.TextDim); Ui.Text(S.CmEqColRifle, c1, y, 14, Palette.TextDim);
        Ui.Text(S.CmEqColAmmo, c2, y, 14, Palette.TextDim); Ui.Text(S.CmEqColCo, c3, y, 14, Palette.TextDim); y += 22;
        foreach (var r in v.Calibers)
        {
            Ui.Text(S.CmCaliberName(r.Key), c0, y, 15, Palette.Text);
            Ui.Text($"{r.Rifles:N0}", c1, y, 15, Palette.Text);
            Ui.Text($"{r.Ammo:N0}", c2, y, 15, Palette.Text);
            Ui.Text($"{r.Companies}", c3, y, 15, r.Companies > 0 ? Palette.Text : Palette.TextDim);
            y += 22;
        }
        y += 12;
        Ui.Text(S.CmEqShellsHead, x, y, 16, Palette.Accent); y += 22;
        foreach (var (k, n) in v.Shells) { Ui.Text($"{S.CmStockShort(k)}  {n:N0}", x + 10, y, 15, Palette.Text); y += 20; }
        y += 10;
        Ui.Text(S.CmEqHeavyHead, x, y, 16, Palette.Accent); y += 22;
        if (v.Heavy.Count == 0) { Ui.Text(S.CmEqHeavyNone, x + 10, y, 14, Palette.TextDim); y += 20; }
        foreach (var (k, n) in v.Heavy) { Ui.Text($"{k}  {n:N0}", x + 10, y, 15, Palette.Text); y += 20; }
        y += 10;
        Ui.Text(string.Format(S.CmEqHorses, v.Horses, v.TotalRifles), x, y, 15, Palette.Text);
        ReadonlyFooter(body, S.CmEqLater);
    }

    void DrawTabOrganization(Rectangle body)
    {
        var tree = CampaignHomeViews.Organization(state);
        float x = body.X + 20, y = body.Y + 16;
        Ui.Text(S.CmOrgTitle, x, y, 22, Palette.Accent); y += 34;
        if (tree.Count == 0) { Ui.Text(S.CmOrgEmpty, x, y, 16, Palette.TextDim); ReadonlyFooter(body, S.CmOrgLater); return; }
        float limit = body.Y + body.Height - 56;
        int total = tree.Sum(r => 1 + r.Battalions.Count), shown = 0;
        foreach (var r in tree)
        {
            if (y + 22 > limit) break;
            string head = r.Regiment > 0 ? string.Format(S.CmOrgRegHead, r.Regiment, r.Companies, r.Strength) : string.Format(S.CmOrgDirectHead, r.Companies, r.Strength);
            Ui.Text(head, x, y, 16, Palette.Accent); y += 24; shown++;
            foreach (var b in r.Battalions)
            {
                if (y + 20 > limit) break;
                string line = b.Number > 0
                    ? string.Format(S.CmOrgBat, string.IsNullOrEmpty(b.Name) ? $"{b.Number}" : b.Name, b.Companies, b.Strength, b.MaxStrength, b.Wounded, b.Available)
                    : string.Format(S.CmOrgDirectCo, b.Name, b.Strength, b.MaxStrength, b.Wounded, b.Available > 0 ? "" : S.CmOrgDirectUnavail);
                Ui.Text(line, x + 24, y, 14, Palette.Text);
                DrawMeter(body.X + body.Width - 330, y + 3, S.CmOrgBarFatigue, b.Fatigue, b.Fatigue >= CampaignEconomyDefs.Current.FatigueWarnThreshold ? Palette.Warning : Palette.GoodInk);   // T42a 읽기 막대
                DrawMeter(body.X + body.Width - 170, y + 3, S.CmOrgBarMorale, b.Morale, Palette.Accent);
                y += 20; shown++;
            }
            y += 6;
        }
        if (shown < total) Ui.Text(string.Format(S.CmOrgMore, total - shown), x, limit + 4, 13, Palette.TextDim);
        ReadonlyFooter(body, S.CmOrgLater);
    }

    /// <summary>[T42a] 라벨 + 0..100 막대 (읽기 전용, 폭 150).</summary>
    static void DrawMeter(float x, float y, string label, int value, Color fill)
    {
        value = Math.Clamp(value, 0, 100);
        Ui.Text(label, x, y - 2, 12, Palette.TextDim);
        var bar = new Rectangle(x + 32, y, 90, 10);
        Raylib.DrawRectangleRec(bar, Palette.Alpha(Palette.PanelDark, 200));
        Raylib.DrawRectangleRec(new Rectangle(bar.X, bar.Y, bar.Width * value / 100f, bar.Height), fill);
        Ui.Text($"{value}", bar.X + bar.Width + 4, y - 2, 12, Palette.Text);
    }

    void ReadonlyFooter(Rectangle body, string later)
    {
        Ui.Text(later, body.X + 20, body.Y + body.Height - 48, 14, Palette.TextDim);
        Ui.Text(S.CmRoNote, body.X + 20, body.Y + body.Height - 28, 13, Palette.TextDim);
    }
}
