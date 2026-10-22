using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T48d] 탭 8 참모진 소탭 [연대장] [대대장·참모] [후보 풀] [학파] + 탭 9 종착 배정 구역. 버튼은 전부 T48b 함수(CommanderPoolOps / OfficerAssign) 호출만 —
/// 규칙은 UI 에 없고, 버튼 활성 여부와 사유 문구는 CanHire / CanDismiss / CanAssign* 결과 그대로 (CommanderHomeModel). 색은 Palette 의미 토큰만. 상태가 바뀌면 진행 저장.
/// </summary>
public sealed partial class CampaignMenu
{
    enum CmdSub { Regiment, Staff, Pool, School }
    CmdSub cmdSub = CmdSub.Regiment;
    string poolFilter = "all";
    string? poolSel;
    float poolScroll;
    int poolHireRegiment;
    string? cmdNotice;
    string hoverReason = "";
    (string Kind, string Id, int Regiment)? cmdConfirm;

    static readonly Dictionary<string, Texture2D?> portraitCache = new();

    /// <summary>스크린샷용: KD_CAMPAIGN_SUB = regiment | staff | pool | school [:후보 id] [:confirm-dismiss] (예 "pool:cmd_sample_02").</summary>
    void ApplyCommanderSubOverride()
    {
        string? v = Environment.GetEnvironmentVariable("KD_CAMPAIGN_SUB");
        if (string.IsNullOrWhiteSpace(v)) return;
        var parts = v.Trim().Split(':');
        cmdSub = parts[0].ToLowerInvariant() switch { "staff" => CmdSub.Staff, "pool" => CmdSub.Pool, "school" => CmdSub.School, _ => CmdSub.Regiment };
        if (parts.Length > 1 && parts[1].Length > 0) poolSel = parts[1];
        if (parts.Length > 2 && parts[2] == "confirm-dismiss" && poolSel != null) cmdConfirm = ("dismiss", poolSel, 1);
    }

    // ---------------------------------------------------------------- 공통 부품

    /// <summary>버튼 한 개: 안 보이면 안 그린다. 비활성이면 마우스를 올렸을 때 사유를 아래 줄에 띄운다. 눌렀으면 true.</summary>
    bool CmdButton(Rectangle r, string label, CmdBtn st, int size = 13, Color? col = null)
    {
        if (!st.Visible) return false;
        if (!st.Enabled && st.Reason.Length > 0 && Ui.Contains(r, mouse)) hoverReason = st.Reason;
        return Button(r, label, st.Enabled, size, col);
    }

    static string Fit(string text, float size, float w)
    {
        if (Ui.Measure(text, size).X <= w) return text;
        while (text.Length > 1 && Ui.Measure(text + "..", size).X > w) text = text[..^1];
        return text + "..";
    }

    static Color TierColor(string tierId) => tierId switch { "cold" => Palette.HpLow, "normal" => Palette.HpMid, "good" => Palette.HpGood, "trusted" => Palette.Accent, _ => Palette.TextDim };

    /// <summary>호감 막대 + "62 (호의)" 문구. 막대 폭 w.</summary>
    static void DrawFavorBar(float x, float y, float w, int favor, string tierId, string tierName)
    {
        var eco = CampaignEconomyDefs.Current;
        float t = Math.Clamp((favor - eco.CommanderFavorMin) / (float)Math.Max(1, eco.CommanderFavorMax - eco.CommanderFavorMin), 0f, 1f);
        var bar = new Rectangle(x, y + 3, w, 8);
        Raylib.DrawRectangleRec(bar, Palette.Alpha(Palette.PanelDark, 220));
        Raylib.DrawRectangleRec(new Rectangle(bar.X, bar.Y, bar.Width * t, bar.Height), TierColor(tierId));
        Raylib.DrawRectangleLinesEx(bar, 1, Palette.PanelBorder);
    }

    static Texture2D? Portrait(string file)
    {
        if (file.Length == 0) return null;
        if (portraitCache.TryGetValue(file, out var t)) return t;
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "ui", "commanders", file);
        Texture2D? tex = null;
        try { if (File.Exists(path)) { var loaded = Raylib.LoadTexture(path); if (loaded.Id != 0) tex = loaded; } } catch (Exception) { tex = null; }
        portraitCache[file] = tex;
        return tex;
    }

    /// <summary>초상 칸 (파일이 없으면 자리표시자 실루엣).</summary>
    static void DrawPortrait(Rectangle r, string file, bool dim)
    {
        Raylib.DrawRectangleRec(r, Palette.PanelDark);
        if (Portrait(file) is { } tex)
            Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), r, Vector2.Zero, 0, Color.White);
        else
        {
            var c = dim ? Palette.PanelBorder : Palette.TextDim;
            Raylib.DrawCircle((int)(r.X + r.Width / 2), (int)(r.Y + r.Height * 0.36f), r.Width * 0.18f, c);
            Raylib.DrawRectangleRounded(new Rectangle(r.X + r.Width * 0.18f, r.Y + r.Height * 0.58f, r.Width * 0.64f, r.Height * 0.34f), 0.5f, 4, c);
        }
        Raylib.DrawRectangleLinesEx(r, 1, Palette.PanelBorder);
    }

    void CmdTryHire(string id, int regiment)
    {
        var res = CommanderPoolOps.Hire(state, id, regiment);
        cmdNotice = res.Ok ? null : res.Reason;
        if (res.Ok) SaveLive();
    }

    void CmdTryDismiss(string id)
    {
        var res = CommanderPoolOps.Dismiss(state, id);
        cmdNotice = res.Ok ? null : res.Reason;
        if (res.Ok) SaveLive();
    }

    void CmdAssignResult(AssignResult res)
    {
        cmdNotice = res.Ok ? null : res.Reason;
        if (res.Ok) SaveLive();
    }

    // ---------------------------------------------------------------- 탭 8

    void DrawTabStaff(Rectangle body)
    {
        if (!CommanderHomeModel.RulesOn) { DrawTabSchools(body); Ui.Text(S.CmdRulesOff, body.X + 20, body.Y + body.Height - 52, 14, Palette.Warning); return; }
        hoverReason = "";
        bool modal = cmdConfirm != null;
        bool savedClick = click;
        if (modal) click = false;   // 확인 창이 떠 있으면 뒤 화면은 클릭을 받지 않는다

        float x = body.X + 14, y = body.Y + 10;
        string[] labels = { S.CmdSubRegiment, S.CmdSubStaff, S.CmdSubPool, S.CmdSubSchool };
        float gap = 6, bw = 130;
        for (int i = 0; i < 4; i++)
        {
            var r = new Rectangle(x + i * (bw + gap), y, bw, 28);
            bool cur = (int)cmdSub == i;
            if (Button(r, labels[i], true, 15, cur ? Palette.Accent : Palette.Text)) cmdSub = (CmdSub)i;
            if (cur) Raylib.DrawLineEx(new Vector2(r.X + 6, r.Y + r.Height - 2), new Vector2(r.X + r.Width - 6, r.Y + r.Height - 2), 2, Palette.Accent);
        }
        string between = state.Pending != null ? S.CmdBetweenOnly : "";
        if (between.Length > 0) Ui.Text(between, x + 4 * (bw + gap) + 4, y + 7, 13, Palette.Warning);

        var content = new Rectangle(x, y + 36, body.Width - 28, body.Height - 36 - 10 - 22);
        switch (cmdSub)
        {
            case CmdSub.Regiment: DrawCmdRegiments(content); break;
            case CmdSub.Staff: DrawCmdStaff(content); break;
            case CmdSub.Pool: DrawCmdPool(content); break;
            default: DrawTabSchools(new Rectangle(body.X, body.Y + 40, body.Width, body.Height - 40)); break;
        }

        string foot = cmdNotice != null ? string.Format(S.CmdNotice, cmdNotice) : hoverReason;
        if (foot.Length > 0) Ui.Text(Fit(foot, 13, body.Width - 28), x, body.Y + body.Height - 24, 13, Palette.Warning);
        if (modal) { click = savedClick; DrawCmdConfirm(body); }
    }

    // ---- 소탭 1: 연대장 3x3 인사 카드 판

    void DrawCmdRegiments(Rectangle area)
    {
        var board = CommanderHomeModel.RegimentBoard(state);
        float gap = 8, cw = (area.Width - gap * 2) / 3, ch = (area.Height - gap * 2) / 3;
        for (int i = 0; i < board.Count; i++)
        {
            var (reg, card) = board[i];
            var r = new Rectangle(area.X + (i % 3) * (cw + gap), area.Y + (i / 3) * (ch + gap), cw, ch);
            DrawRegimentCard(r, reg, card);
        }
    }

    void DrawRegimentCard(Rectangle r, int reg, CommanderHomeModel.Card? card)
    {
        Ui.Panel(r, Palette.Alpha(Palette.Panel, 230), card == null ? Palette.PanelBorder : Palette.HudTrim);
        Ui.Text(string.Format(S.CmdRegimentN, reg), r.X + 8, r.Y + 5, 13, Palette.Accent);
        if (card == null)
        {
            Ui.Text(S.CmdVacant, r.X + 10, r.Y + 34, 15, Palette.TextDim);
            DrawPortrait(new Rectangle(r.X + 10, r.Y + 58, 40, 40), "", true);
            var pick = new Rectangle(r.X + 60, r.Y + r.Height - 34, r.Width - 70, 26);
            if (Button(pick, S.CmdBtnHire + "...", true, 13)) { cmdSub = CmdSub.Pool; poolHireRegiment = reg; cmdNotice = null; }
            return;
        }
        float px = r.X + 8, py = r.Y + 24;
        DrawPortrait(new Rectangle(px, py, 46, 46), card.Portrait, false);
        float tx = px + 54, tw = r.Width - 70 - 8;
        Ui.Text(Fit(card.Name, 16, tw), tx, py, 16, Palette.Text);
        Ui.Text(Fit(card.Rank + " · " + card.School, 12, tw), tx, py + 19, 12, Palette.TextDim);
        Ui.Text(Fit(card.Origin, 12, tw), tx, py + 33, 12, Palette.TextDim);
        float fy = py + 52;
        Ui.Text(S.CmdFavor, px, fy - 2, 12, Palette.TextDim);
        DrawFavorBar(px + 30, fy, r.Width - 150, card.Favor, card.TierId, card.TierName);
        Ui.Text(Fit(string.Format(S.CmdFavorLine, card.Favor, card.TierName), 12, 90), px + 30 + (r.Width - 150) + 6, fy - 2, 12, TierColor(card.TierId));
        float by = r.Y + r.Height - 32;
        var dis = CommanderHomeModel.DismissBtn(state, card.Id);
        if (CmdButton(new Rectangle(r.X + 8, by, 62, 26), S.CmdBtnDismiss, dis, 13)) cmdConfirm = ("dismiss", card.Id, reg);
        if (card.Unfireable) DrawStamp(new Rectangle(r.X + 8, by, 84, 26), S.CmdStamp);
        if (Button(new Rectangle(r.X + (card.Unfireable ? 100 : 78), by, 52, 26), S.CmdBtnDetail, true, 13)) { cmdSub = CmdSub.Pool; poolSel = card.Id; cmdNotice = null; }
        if (card.Unfireable && card.UnfireableNote.Length > 0 && Ui.Contains(r, mouse)) hoverReason = card.UnfireableNote;
    }

    /// <summary>빨간 도장 ('해임 불가'): 직인 느낌의 테두리 글씨 (의미 색 HpLow).</summary>
    static void DrawStamp(Rectangle r, string text)
    {
        Raylib.DrawRectangleLinesEx(r, 2, Palette.HpLow);
        Ui.TextCentered(text, r, 14, Palette.HpLow);
    }

    // ---- 소탭 2: 대대장 · 참모

    void DrawCmdStaff(Rectangle area)
    {
        float x = area.X, y = area.Y, w = area.Width;
        Ui.Text(S.CmdStaffHead, x, y, 15, Palette.Accent); y += 22;
        foreach (var row in CommanderHomeModel.StaffBoard(state))
        {
            var rr = new Rectangle(x, y, w, 30);
            Ui.Panel(rr, Palette.Alpha(Palette.Panel, 230), Palette.PanelBorder);
            Ui.Text(row.SlotLabel, x + 8, y + 7, 14, Palette.Accent);
            Ui.Text(row.OfficerKey > 0 ? Fit(row.OfficerName, 14, 150) : S.CmdSlotEmpty, x + 60, y + 7, 14, row.OfficerKey > 0 ? Palette.Text : Palette.TextDim);
            Ui.Text(Fit(row.Effect, 13, w - 300), x + 230, y + 8, 13, row.OfficerKey > 0 ? Palette.Text : Palette.TextDim);
            if (row.OfficerKey > 0 && Button(new Rectangle(x + w - 62, y + 3, 56, 24), S.CmdBtnUnassign, state.Pending == null, 12))
                CmdAssignResult(OfficerAssign.Unassign(state, row.OfficerKey));
            y += 34;
        }
        y += 6;
        Ui.Text(S.CmdBatHead, x, y, 15, Palette.Accent); y += 22;
        var bats = CommanderHomeModel.BattalionBoard(state);
        if (bats.Count == 0) { Ui.Text(S.CmdNoBattalions, x + 8, y, 13, Palette.TextDim); y += 20; }
        int shown = 0;
        foreach (var b in bats)
        {
            if (y + 24 > area.Y + area.Height - 150) break;
            Ui.Text(string.Format(S.CmdBatRow, b.Battalion), x + 8, y + 4, 13, Palette.Accent);
            Ui.Text(b.OfficerKey > 0 ? Fit(b.OfficerName + "  " + b.Detail, 13, w - 190) : S.CmdBatEmpty, x + 80, y + 4, 13, b.OfficerKey > 0 ? Palette.Text : Palette.TextDim);
            if (b.OfficerKey > 0 && Button(new Rectangle(x + w - 62, y, 56, 22), S.CmdBtnUnassign, state.Pending == null, 12)) CmdAssignResult(OfficerAssign.Unassign(state, b.OfficerKey));
            y += 25; shown++;
        }
        if (shown < bats.Count) { Ui.Text(string.Format(S.CmdMore, bats.Count - shown), x + 8, y, 12, Palette.TextDim); y += 18; }
        y += 6;
        DrawTerminalSection(x, Math.Max(y, area.Y + area.Height - 140), w, 3);
    }

    /// <summary>종착 대기 장교 구역 (탭 8 · 탭 9 공용): 장교 한 줄 + [대대장으로] [보급] [정보] [연구] [학파]. 그린 높이를 돌려준다.</summary>
    float DrawTerminalSection(float x, float y, float w, int maxRows, string? head = null)
    {
        float y0 = y;
        Ui.Text(head ?? S.CmdTermHead, x, y, 15, Palette.Accent); y += 22;
        var list = CommanderHomeModel.Terminal(state);
        if (list.Count == 0) { Ui.Text(S.CmdTermNone, x + 8, y, 13, Palette.TextDim); return y + 20 - y0; }
        int shown = 0;
        foreach (var o in list)
        {
            if (shown >= maxRows) break;
            Ui.Text(Fit(o.Name + "  " + string.Format(S.CmdTermWhere, o.Where), 13, w - 380), x + 8, y + 5, 13, Palette.Text);
            float bx = x + w - 372;
            int bat = CommanderHomeModel.TargetBattalion(state, o.Key);
            var batSt = bat > 0 ? CommanderHomeModel.BattalionBtn(state, o.Key, bat) : new CmdBtn(true, false, S.CmdNoBattalions);
            if (CmdButton(new Rectangle(bx, y, 88, 24), S.CmdBtnBattalion, batSt, 12)) CmdAssignResult(OfficerAssign.AssignBattalion(state, o.Key, bat));
            bx += 94;
            foreach (var slot in StaffEffects.Slots)
            {
                if (CmdButton(new Rectangle(bx, y, 64, 24), CommanderHomeModel.SlotLabel(slot), CommanderHomeModel.StaffBtn(state, o.Key, slot), 12)) CmdAssignResult(OfficerAssign.AssignStaff(state, o.Key, slot));
                bx += 70;
            }
            y += 28; shown++;
        }
        if (shown < list.Count) { Ui.Text(string.Format(S.CmdMore, list.Count - shown), x + 8, y, 12, Palette.TextDim); y += 18; }
        return y - y0;
    }

    // ---- 소탭 3: 후보 풀

    void DrawCmdPool(Rectangle area)
    {
        float x = area.X, y = area.Y;
        Ui.Text(S.CmdFilterLabel, x, y + 6, 13, Palette.TextDim);
        string[] fl = CommanderHomeModel.Filters;
        float fx = x + 36;
        foreach (var f in fl)
        {
            string lbl = f == "all" ? S.CmdFilterAll : f switch { CommanderStatus.Available => S.CmdStAvailable, CommanderStatus.Hired => S.CmdStHired.Split(' ')[0], CommanderStatus.Dismissed => S.CmdStDismissed, _ => S.CmdStLocked.Split(' ')[0] };
            if (Button(new Rectangle(fx, y, 74, 26), lbl, true, 13, poolFilter == f ? Palette.Accent : Palette.Text)) { poolFilter = f; poolScroll = 0; }
            fx += 80;
        }
        y += 34;
        float detailW = Math.Min(290, area.Width * 0.4f);
        var grid = new Rectangle(x, y, area.Width - detailW - 12, area.Height - 34 - 16);
        var detail = new Rectangle(grid.X + grid.Width + 12, y, detailW, area.Height - 34);
        var cards = CommanderHomeModel.Pool(state, poolFilter);
        const float gap = 8, rowH = 74;
        float cw = (grid.Width - gap * 2) / 3;
        int rows = (cards.Count + 2) / 3;
        float totalH = rows * (rowH + gap);
        if (Ui.Contains(grid, mouse)) poolScroll -= Input.GetMouseWheelMove() * (rowH + gap);
        poolScroll = Math.Clamp(poolScroll, 0, MathF.Max(0, totalH - grid.Height));
        if (cards.Count == 0) Ui.Text(S.CmdPoolEmpty, grid.X + 8, grid.Y + 8, 14, Palette.TextDim);
        Raylib.BeginScissorMode((int)grid.X, (int)grid.Y, (int)grid.Width, (int)grid.Height);
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            var r = new Rectangle(grid.X + (i % 3) * (cw + gap), grid.Y + (i / 3) * (rowH + gap) - poolScroll, cw, rowH);
            if (r.Y + r.Height < grid.Y || r.Y > grid.Y + grid.Height) continue;
            bool sel = poolSel == c.Id;
            Ui.Panel(r, Palette.Alpha(Palette.Panel, 230), sel ? Palette.Accent : Palette.PanelBorder);
            DrawPortrait(new Rectangle(r.X + 6, r.Y + 6, 32, 32), c.Portrait, c.Locked);
            Ui.Text(Fit(c.Name, 14, cw - 48), r.X + 44, r.Y + 5, 14, c.Locked ? Palette.TextDim : Palette.Text);
            Ui.Text(Fit(c.Rank + (c.School.Length > 0 ? " · " + c.School : ""), 11, cw - 48), r.X + 44, r.Y + 23, 11, Palette.TextDim);
            Ui.Text(Fit(c.StatusLabel, 12, cw - 12), r.X + 8, r.Y + 42, 12, c.Status == CommanderStatus.Available ? Palette.HpGood : c.Status == CommanderStatus.Hired ? Palette.Accent : Palette.TextDim);
            if (!c.Locked) DrawFavorBar(r.X + 8, r.Y + 58, cw - 16, c.Favor, c.TierId, c.TierName);
            if (Ui.Contains(r, mouse) && Ui.Contains(grid, mouse) && click) { poolSel = c.Id; cmdNotice = null; }
        }
        Raylib.EndScissorMode();
        if (totalH > grid.Height) Ui.Text(string.Format(S.CmdMore, Math.Max(0, cards.Count - (int)((grid.Height + poolScroll) / (rowH + gap)) * 3)), grid.X + 4, grid.Y + grid.Height + 2, 11, Palette.TextDim);
        DrawCmdDetail(detail);
    }

    void DrawCmdDetail(Rectangle r)
    {
        Ui.Panel(r, Palette.Alpha(Palette.PanelDark, 200), Palette.PanelBorder);
        var card = poolSel != null ? CommanderHomeModel.CardOf(state, poolSel) : null;
        var def = poolSel != null ? CommanderCatalog.Find(poolSel) : null;
        if (card == null || def == null) { Ui.Text(S.CmdDetailPick, r.X + 10, r.Y + 10, 13, Palette.TextDim); return; }
        float x = r.X + 10, y = r.Y + 8, w = r.Width - 20;
        DrawPortrait(new Rectangle(x, y, 56, 56), card.Portrait, card.Locked);
        Ui.Text(Fit(card.Name, 18, w - 66), x + 64, y, 18, card.Locked ? Palette.TextDim : Palette.Accent);
        Ui.Text(Fit(card.Rank + (card.School.Length > 0 ? " · " + card.School : ""), 13, w - 66), x + 64, y + 24, 13, Palette.TextDim);
        Ui.Text(Fit(card.StatusLabel, 13, w - 66), x + 64, y + 40, 13, Palette.Text);
        y += 64;
        if (card.Locked) return;
        Ui.Text(S.CmdFavor, x, y - 2, 12, Palette.TextDim);
        DrawFavorBar(x + 30, y, w - 120, card.Favor, card.TierId, card.TierName);
        Ui.Text(Fit(string.Format(S.CmdFavorLine, card.Favor, card.TierName), 12, 86), x + w - 84, y - 2, 12, TierColor(card.TierId));
        y += 18;
        if (card.Origin.Length > 0) { Ui.Text(Fit(card.Origin, 12, w), x, y, 12, Palette.TextDim); y += 16; }
        if (card.TraitNames.Count > 0) { Ui.Text(Fit(S.CmdDetailTraits + ": " + string.Join(" · ", card.TraitNames), 12, w), x, y, 12, Palette.Text); y += 16; }
        if (card.Summary.Length > 0) foreach (var l in WrapText(string.Format(S.CmdBattleLine, card.Summary), w, 12).Take(2)) { Ui.Text(l, x, y, 12, Palette.Text); y += 15; }
        string lk = S.CmdDetailLikes + ": " + (def.Favor.Likes.Count > 0 ? string.Join(" ", def.Favor.Likes) : S.CmdNone);
        string dk = S.CmdDetailDislikes + ": " + (def.Favor.Dislikes.Count > 0 ? string.Join(" ", def.Favor.Dislikes) : S.CmdNone);
        Ui.Text(Fit(lk, 12, w), x, y, 12, Palette.HpGood); y += 15;
        Ui.Text(Fit(dk, 12, w), x, y, 12, Palette.HpLow); y += 15;
        string cond = string.Format(S.CmdDetailCost, def.Hire.Cost) + (def.Hire.Condition.Length > 0 ? " · " + S.CmdDetailCond + ": " + def.Hire.Condition : "");
        foreach (var l in WrapText(cond, w, 12).Take(2)) { Ui.Text(l, x, y, 12, Palette.TextDim); y += 15; }
        float bioBottom = r.Y + r.Height - 74;
        if (def.Bio.Length > 0)
            foreach (var l in WrapText(def.Bio.Replace("\\n", "\n"), w, 12)) { if (y + 15 > bioBottom) break; Ui.Text(l, x, y, 12, Palette.Text); y += 15; }

        // 기용 / 해임 (T48b 함수 결과 그대로)
        float by = r.Y + r.Height - 68;
        if (card.Status == CommanderStatus.Hired)
        {
            var dis = CommanderHomeModel.DismissBtn(state, card.Id);
            if (CmdButton(new Rectangle(x, by + 34, 80, 28), S.CmdBtnDismiss, dis, 14)) cmdConfirm = ("dismiss", card.Id, card.Regiment);
            if (card.Unfireable) { DrawStamp(new Rectangle(x, by + 34, 90, 28), S.CmdStamp); if (card.UnfireableNote.Length > 0) Ui.Text(Fit(card.UnfireableNote, 12, w - 100), x + 96, by + 40, 12, Palette.TextDim); }
            return;
        }
        if (poolHireRegiment < 1 || poolHireRegiment > CampaignRegiments.Max) poolHireRegiment = Math.Max(1, CommanderHomeModel.FirstVacantRegiment(state));
        if (CommanderHomeModel.FirstVacantRegiment(state) == 0) { Ui.Text(S.CmdNoVacant, x, by + 6, 13, Palette.TextDim); return; }
        Ui.Text(S.CmdHireInto, x, by + 6, 13, Palette.TextDim);
        if (Button(new Rectangle(x + 70, by, 26, 26), "<", true, 14) && CommanderHomeModel.NextVacantRegiment(state, poolHireRegiment, -1) is int p and > 0) poolHireRegiment = p;
        Ui.TextCentered(string.Format(S.CmdRegimentN, poolHireRegiment), new Rectangle(x + 98, by, 60, 26), 14, Palette.Text);
        if (Button(new Rectangle(x + 160, by, 26, 26), ">", true, 14) && CommanderHomeModel.NextVacantRegiment(state, poolHireRegiment, 1) is int n and > 0) poolHireRegiment = n;
        var hire = CommanderHomeModel.HireBtn(state, card.Id, poolHireRegiment);
        if (CmdButton(new Rectangle(x, by + 34, 80, 28), S.CmdBtnHire, hire, 14)) cmdConfirm = ("hire", card.Id, poolHireRegiment);
        if (!hire.Enabled && hire.Reason.Length > 0) foreach (var l in WrapText(hire.Reason, w - 90, 12).Take(2).Select((s, i) => (s, i))) Ui.Text(l.s, x + 88, by + 36 + l.i * 14, 12, Palette.Warning);
    }

    // ---- 확인 대화상자

    void DrawCmdConfirm(Rectangle body)
    {
        if (cmdConfirm is not { } cf) return;
        Raylib.DrawRectangleRec(body, Palette.Alpha(Color.Black, 150));
        float w = Math.Min(440, body.Width - 40), h = 190;
        var box = new Rectangle(body.X + (body.Width - w) / 2, body.Y + (body.Height - h) / 2, w, h);
        Ui.Panel(box, Palette.Panel, Palette.Accent);
        string text = cf.Kind == "dismiss" ? CommanderHomeModel.DismissNotice(state, cf.Id) : CommanderHomeModel.HireNotice(state, cf.Id, cf.Regiment);
        float ty = box.Y + 18;
        var parts = text.Split('\n');
        for (int pi = 0; pi < parts.Length; pi++)
            foreach (var l in WrapText(parts[pi], w - 36, 15)) { Ui.Text(l, box.X + 18, ty, 15, pi == 0 ? Palette.Text : Palette.Warning); ty += 22; }
        var ok = cf.Kind == "dismiss" ? CmdBtn.From(CommanderPoolOps.CanDismiss(state, cf.Id)) : CmdBtn.From(CommanderPoolOps.CanHire(state, cf.Id, cf.Regiment));
        if (Button(new Rectangle(box.X + w / 2 - 130, box.Y + h - 46, 120, 32), S.CmdBtnYes, ok.Enabled, 16, ok.Enabled ? Palette.Accent : Palette.TextDim))
        {
            if (cf.Kind == "dismiss") CmdTryDismiss(cf.Id); else CmdTryHire(cf.Id, cf.Regiment);
            cmdConfirm = null;
        }
        if (Button(new Rectangle(box.X + w / 2 + 10, box.Y + h - 46, 120, 32), S.CmdBtnNo, true, 16)) cmdConfirm = null;
    }

    // ---------------------------------------------------------------- 탭 9 종착 배정 / 탭 12 인사 일지

    /// <summary>탭 12 부대 기록 아래: 인사 일지 (연표의 commander 줄 최근 n건, 문구는 id 로 찾음).</summary>
    float DrawCommanderLog(float x, float y, int n)
    {
        float y0 = y;
        Ui.Text(string.Format(S.CmdRecHead, n), x, y, 16, Palette.Accent); y += 22;
        var rows = state.Timeline.Where(e => e != null && e.Kind == "commander").Reverse().Take(n).ToList();
        if (rows.Count == 0) { Ui.Text(S.CmdRecNone, x + 10, y, 14, Palette.TextDim); return y + 20 - y0; }
        foreach (var e in rows)
        {
            Ui.Text(string.Format(S.CmSchRowDay, e.Day), x + 10, y, 13, Palette.TextDim);
            Ui.Text(CommanderHomeModel.TimelineText(state, e), x + 80, y, 13, Palette.Text);
            y += 19;
        }
        return y - y0;
    }
}
