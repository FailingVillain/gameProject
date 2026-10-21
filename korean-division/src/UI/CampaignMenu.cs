using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>시작 화면의 캠페인 페이지 (최소판): 새 캠페인 → 1-1 브리핑 / 진행 요약 · 이월 병력 · 다음 미션 (준비 중 화면).</summary>
public sealed class CampaignMenu
{
    public string? OpenMission { get; set; }
    public bool BackRequested { get; set; }
    readonly CampaignState? state = CampaignState.Load();
    bool resetArmed;
    Vector2 mouse;
    bool click;

    bool Button(Rectangle r, string label, int size = 16)
    {
        bool hover = Ui.Contains(r, mouse);
        Raylib.DrawRectangleRounded(r, 0.2f, 4, hover ? Palette.BtnHover : Palette.Btn);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
        Ui.TextCentered(label, r, size, Palette.Text);
        return hover && click;
    }

    public void Draw(int screenW, int screenH)
    {
        mouse = Input.GetMousePosition();
        click = Input.IsMouseButtonPressed(MouseButton.Left);
        float pw = MathF.Min(820, screenW - 40), ph = MathF.Min(520, screenH - 70);
        var panel = new Rectangle((screenW - pw) / 2, MathF.Max(12, (screenH - ph) / 2 - 10), pw, ph);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 235), Palette.HudTrim);
        Ui.Text(S.CmTitle, panel.X + 24, panel.Y + 16, 26, Palette.Accent);
        float x = panel.X + 28, y = panel.Y + 70;
        float by = panel.Y + panel.Height - 54;
        if (state == null || !state.HasProgress)
        {
            Ui.Text(S.CmNewTitle, x, y, 26, Palette.Text); y += 36;
            Ui.Text(S.CmNewText, x, y, 16, Palette.TextDim);
            if (CampaignState.LastLoadError is { } cerr) { Ui.Text(cerr, x, y + 30, 16, Palette.Warning); Ui.Text(S.CmOverwriteWarn, x, y + 54, 16, Palette.TextDim); }
            if (Button(new Rectangle(panel.X + pw - 24 - 260, by, 260, 40), S.CmStart, 26)) OpenMission = CampaignState.FirstMission;
        }
        else
        {
            Ui.Text(S.CmCompleted, x, y, 16, Palette.Accent); y += 24;
            foreach (var h in state.History)
            {
                Ui.Text(string.Format(S.CmHistory, h.Title, MissionText.Clock(h.DurationSeconds), h.EnemyKills, h.Lost, h.Survivors), x + 10, y, 16, Palette.Text);
                y += 22;
            }
            y += 10;
            Ui.Text(string.Format(S.CmCarry, state.Units.Count, state.Officers.Count), x, y, 16, Palette.Text); y += 36;
            Ui.Text(string.Format(state.NextAvailable ? S.CmNextReady : S.CmNext, state.NextName), x, y, 26, state.NextAvailable ? Palette.Accent : Palette.TextDim);
            if (state.NextAvailable && Button(new Rectangle(panel.X + pw - 24 - 220, by, 220, 40), S.CmNextStart, 26)) OpenMission = state.NextMission;
            var last = state.History.LastOrDefault();
            if (state.HowitzerCompany) { Ui.Text(S.CmHowitzer, x, y + 34, 16, Palette.Text); }
            if (last != null && Button(new Rectangle(panel.X + pw - 24 - 220 - 12 - 160, by, 160, 40), string.Format(S.CmRetry, CampaignState.ShortNameOf(last.MissionId)))) OpenMission = last.MissionId;
            if (Button(new Rectangle(panel.X + 24 + 152, by, 220, 40), resetArmed ? S.CmResetConfirm : S.CmReset))
            {
                if (resetArmed) { CampaignState.Reset(); BackRequested = true; }
                else resetArmed = true;
            }
        }
        if (Button(new Rectangle(panel.X + 24, by, 140, 40), S.CmBack)) BackRequested = true;
    }
}
