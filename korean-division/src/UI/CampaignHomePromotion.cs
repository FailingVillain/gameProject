using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T42b] 탭 9 진급 · 군령권: 진급 후보 목록 + [진급] 버튼 (작전 사이에만, 규칙은 CampaignPromotion — 기본 꺼짐) + 최근 진급 기록.
/// 군령권은 TBD(스토리) — 문구 자리만. 상태 변경은 CampaignPromotion.Promote 한 곳, 바뀌면 진행 저장.
/// </summary>
public sealed partial class CampaignMenu
{
    void DrawTabPromotion(Rectangle body)
    {
        float x = body.X + 20, y = body.Y + 16, w = body.Width - 40;
        Ui.Text(S.CmPromoTitle, x, y, 22, Palette.Accent); y += 34;
        var block = CampaignPromotion.BlockOf(state);
        string status = block switch
        {
            CampaignPromotion.Block.Off => S.CmPromoOff,
            CampaignPromotion.Block.Operation => S.CmPromoDuringOp,
            CampaignPromotion.Block.Quota => string.Format(S.CmPromoQuotaFull, state.PromotionsThisBreak, CampaignEconomyDefs.Current.PromoMaxPerBreak),
            _ => string.Format(S.CmPromoQuota, state.PromotionsThisBreak, CampaignEconomyDefs.Current.PromoMaxPerBreak),
        };
        Ui.Text(status, x, y, 15, block == CampaignPromotion.Block.None ? Palette.Text : Palette.Warning); y += 28;

        Ui.Text(S.CmPromoCandHead, x, y, 16, Palette.Accent); y += 24;
        var cands = CampaignPromotion.Candidates(state);
        float bottom = body.Y + body.Height - 150;
        if (cands.Count == 0) { Ui.Text(S.CmPromoNone, x + 10, y, 14, Palette.TextDim); y += 22; }
        int shown = 0;
        foreach (var c in cands)
        {
            if (y + 26 > bottom) break;
            string name = string.Format(S.CmPromoRow, c.Officer ? S.CmPromoOfficer : S.CmPromoSoldier, c.UnitId, CampaignPromotion.RankName(c.Officer, c.FromRank), CampaignPromotion.RankName(c.Officer, c.ToRank),
                c.Officer ? S.CmPromoXp : S.CmPromoKills, c.Score, c.Need);
            Ui.Text(name, x + 10, y + 5, 14, Palette.Text);
            if (Button(new Rectangle(x + w - 90, y, 80, 24), S.CmPromoBtn, block == CampaignPromotion.Block.None, 14) && CampaignPromotion.Promote(state, c.Officer, c.UnitId, c.Key)) SaveLive();
            y += 28; shown++;
        }
        if (shown < cands.Count) { Ui.Text(string.Format(S.CmPromoMore, cands.Count - shown), x + 10, y, 13, Palette.TextDim); y += 20; }

        y = Math.Max(y + 8, bottom + 4);
        Ui.Text(S.CmPromoLogHead, x, y, 16, Palette.Accent); y += 22;
        var log = state.PromotionLog;
        if (log.Count == 0) Ui.Text(S.CmPromoLogEmpty, x + 10, y, 14, Palette.TextDim);
        for (int i = Math.Max(0, log.Count - 3); i < log.Count; i++)
        {
            var p = log[i];
            Ui.Text(string.Format(S.CmPromoLogRow, p.Officer ? S.CmPromoOfficer : S.CmPromoSoldier, p.UnitId, CampaignPromotion.RankName(p.Officer, p.FromRank), CampaignPromotion.RankName(p.Officer, p.ToRank)), x + 10, y, 14, Palette.Text);
            y += 20;
        }
        Ui.Text(S.CmPromoCommandLater, body.X + 20, body.Y + body.Height - 28, 14, Palette.TextDim);
    }
}
