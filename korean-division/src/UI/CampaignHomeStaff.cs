using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T19d] 8 참모진 탭 (우 화면) — 읽기 전용. 장교 학파 5종(assets/data/doctrines.json)과 학파별 인물(figures)만 표시한다.
/// 상태를 바꾸지 않고 시뮬 · 전투 AI 에 쓰이지 않는다. AI 경향 수치(axes)는 미정이라 표시하지 않는다 (설계 §13-2).
/// </summary>
public sealed partial class CampaignMenu
{
    Doctrines? doctrines;
    string doctrinesError = "";
    int staffSel;

    void EnsureDoctrines()
    {
        if (doctrines != null || doctrinesError.Length > 0) return;
        try { doctrines = Doctrines.Load(); } catch (Exception ex) { doctrinesError = ex.Message; }
    }

    void DrawTabStaff(Rectangle body)
    {
        EnsureDoctrines();
        float x = body.X + 20, y = body.Y + 16;
        Ui.Text(S.CmStaffTitle, x, y, 22, Palette.Accent); y += 32;
        if (doctrines == null) { Ui.Text(S.CmStaffLoadFail, x, y, 16, Palette.Warning); return; }
        var schools = doctrines.Schools;
        staffSel = Math.Clamp(staffSel, 0, schools.Count - 1);

        const float listW = 150;
        for (int i = 0; i < schools.Count; i++)
            if (Button(new Rectangle(x, y + i * 38, listW, 32), schools[i].Name, true, 16, i == staffSel ? Palette.Accent : Palette.Text)) staffSel = i;

        var s = schools[staffSel];
        float dx = x + listW + 24, dw = (body.X + body.Width) - dx - 20, dy = y;
        Ui.Text(string.Format(S.CmStaffSchoolHead, s.Name), dx, dy, 20, Palette.Text); dy += 30;
        dy = StaffLines(S.CmStaffOfficer, s.Officer, dx, dy, dw);
        dy = StaffLines(S.CmStaffTactics, s.Tactics, dx, dy, dw);
        dy = StaffLines(S.CmStaffAi, string.Join(" · ", s.Ai), dx, dy, dw);
        dy = StaffLines(S.CmStaffResearch, string.Join(" · ", s.Research), dx, dy, dw);
        dy = StaffLines(S.CmStaffFigures, s.Figures.Count > 0 ? string.Join(" · ", s.Figures) : S.CmStaffNoFigures, dx, dy, dw);
        Ui.Text(S.CmStaffNote, x, (body.Y + body.Height) - 30, 14, Palette.TextDim);
    }

    float StaffLines(string label, string text, float x, float y, float w)
    {
        Ui.Text(label, x, y, 14, Palette.TextDim); y += 18;
        foreach (var l in WrapText(text, w, 16)) { Ui.Text(l, x + 8, y, 16, Palette.Text); y += 21; }
        return y + 8;
    }
}
