using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T42c] 탭 7 외교 · 포로: 포로 장부 + 송환 / 수용 (작전 사이에만, 규칙은 CampaignPrisoners). 편입은 없음.
/// 상태 변경은 CampaignPrisoners 한 곳, 바뀌면 진행 저장.
/// </summary>
public sealed partial class CampaignMenu
{
    void DrawTabPrisoners(Rectangle body)
    {
        float x = body.X + 20, y = body.Y + 16;
        Ui.Text(S.CmPrisTitle, x, y, 22, Palette.Accent); y += 34;
        Ui.Text(string.Format(S.CmPrisLedger, state.Stock.Prisoners), x, y, 18, Palette.Text); y += 28;
        Ui.Text(string.Format(S.CmPrisTotals, state.PrisonersRepatriated, state.PrisonersInterned), x, y, 15, Palette.TextDim); y += 22;
        if (state.LastDebrief != null) { Ui.Text(string.Format(S.CmPrisLast, state.LastDebrief.PrisonersTaken), x, y, 15, Palette.TextDim); y += 22; }
        y += 8;
        var block = CampaignPrisoners.BlockOf(state);
        if (block == CampaignPrisoners.Block.Empty) { Ui.Text(S.CmPrisEmpty, x, y, 15, Palette.TextDim); y += 28; }
        else if (block == CampaignPrisoners.Block.Operation) { Ui.Text(S.CmPrisDuringOp, x, y, 15, Palette.Warning); y += 28; }
        bool can = block == CampaignPrisoners.Block.None;
        float bw = 90;

        Ui.Text(S.CmPrisRepHead, x, y + 5, 16, Palette.Accent);
        if (Button(new Rectangle(x + 260, y, bw, 26), S.CmPrisOne, can, 14) && CampaignPrisoners.Repatriate(state, 1) > 0) SaveLive();
        if (Button(new Rectangle(x + 260 + bw + 8, y, bw, 26), S.CmPrisAll, can, 14) && CampaignPrisoners.Repatriate(state, state.Stock.Prisoners) > 0) SaveLive();
        y += 38;

        int acc = CampaignPrisoners.MaxAcceptable(state);
        Ui.Text(string.Format(S.CmPrisAccHead, CampaignEconomyDefs.Current.PrisonerAcceptSupply), x, y + 5, 16, Palette.Accent);
        if (Button(new Rectangle(x + 260, y, bw, 26), S.CmPrisOne, can && acc >= 1, 14) && CampaignPrisoners.Accept(state, 1) > 0) SaveLive();
        if (Button(new Rectangle(x + 260 + bw + 8, y, bw, 26), S.CmPrisAll, can && acc >= 1, 14) && CampaignPrisoners.Accept(state, acc) > 0) SaveLive();
        y += 30;
        Ui.Text(string.Format(S.CmPrisAccAvail, state.Supply, acc), x + 10, y, 14, Palette.TextDim);
        Ui.Text(S.CmPrisLater, body.X + 20, body.Y + body.Height - 28, 14, Palette.TextDim);
    }
}
