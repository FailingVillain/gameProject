using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// 관전자용 오버레이 (뷰포트 우측):
///   위: 시점 선택 줄 — [전체] + 세력 번호 칩 (클릭 또는 숫자키 0..9)
///   아래: 세력 현황판 (Tab 으로 켜고 끔) — 세력당 두 줄, 점수순 정렬, 휠로 스크롤, 행 클릭 = 그 세력 시점
/// 읽기 전용: World 를 바꾸지 않는다.
/// </summary>
public sealed class SpectatorPanel
{
    const float BoardW = 420, RowH = 40, Chip = 28, ChipH = 24, Pad = 6;

    readonly Game game;
    public bool Visible { get; private set; } = true;
    Rectangle strip, board, allButton, speedRow;
    readonly List<(Rectangle R, int Index)> speedHits = new();
    float scroll;
    readonly List<(Rectangle R, int Faction)> hits = new();

    // 프레임마다 다시 계산하는 집계 (세력 번호로 인덱스)
    int[] army = Array.Empty<int>(), points = Array.Empty<int>(), buildings = Array.Empty<int>();

    public SpectatorPanel(Game game) => this.game = game;

    World W => game.World;

    public void Toggle() => Visible = !Visible;

    public void Layout(Rectangle vp)
    {
        if (!game.Observer) { strip = board = default; return; }
        float w = MathF.Min(BoardW, vp.Width - 40);
        int n = W.Factions.Count - 1;
        float chipsX = vp.X + vp.Width - Pad - w + Pad + 54;
        int perRow = Math.Max(1, (int)((vp.X + vp.Width - Pad - Pad - chipsX) / Chip));
        int rows = (n + perRow - 1) / perRow;
        float h = Pad * 2 + MathF.Max(ChipH, rows * (ChipH + 2) - 2);
        h += ChipH + 4;   // 배속 줄 (관전 · 리플레이: 0.5 ~ 8배)
        strip = new Rectangle(vp.X + vp.Width - Pad - w, vp.Y + Pad, w, h);
        allButton = new Rectangle(strip.X + Pad, strip.Y + Pad, 46, ChipH);
        speedRow = new Rectangle(strip.X + Pad, strip.Y + h - Pad - ChipH, w - Pad * 2, ChipH);
        float top = strip.Y + strip.Height + 4;
        board = new Rectangle(strip.X, top, w, MathF.Max(0, vp.Y + vp.Height - Pad - top));
    }

    public bool Contains(Vector2 p) =>
        game.Observer && (Raylib.CheckCollisionPointRec(p, strip) || (Visible && Raylib.CheckCollisionPointRec(p, board)));

    public void Wheel(float wheel)
    {
        if (Visible) scroll = MathF.Max(0, scroll - wheel * RowH);
    }

    /// <summary>클릭 처리: 시점 선택. 소비했으면 true.</summary>
    public bool Click(Vector2 p)
    {
        if (!Contains(p)) return false;
        foreach (var (r, i) in speedHits)
            if (Raylib.CheckCollisionPointRec(p, r)) { game.SetSpeedIndex(i); return true; }
        foreach (var (r, f) in hits)
            if (Raylib.CheckCollisionPointRec(p, r)) { game.SetView(f); break; }
        return true;
    }

    // ---------------------------------------------------------------- 집계

    void Tally()
    {
        int n = W.Factions.Count;
        if (army.Length != n) { army = new int[n]; points = new int[n]; buildings = new int[n]; }
        Array.Clear(army); Array.Clear(points); Array.Clear(buildings);
        foreach (var u in W.Units)
            if ((uint)u.Owner < (uint)n && u.Def.Weapon != null) army[u.Owner]++;
        foreach (var b in W.Buildings)
        {
            if ((uint)b.Owner >= (uint)n) continue;
            if (b.Def.Capturable) points[b.Owner]++;
            else buildings[b.Owner]++;
        }
    }

    // ---------------------------------------------------------------- 그리기

    public void Draw(Vector2 mouse)
    {
        hits.Clear();
        if (!game.Observer || strip.Width <= 0) return;
        Tally();
        DrawStrip(mouse);
        if (Visible) DrawBoard(mouse);
    }

    void DrawStrip(Vector2 mouse)
    {
        Ui.Panel(strip, Palette.Alpha(Palette.PanelDark, 225), Palette.PanelBorder);
        DrawSpeeds(mouse);
        bool all = game.View == 0;
        DrawChip(allButton, S.ViewAll, Palette.TextDim, all, false, mouse, 0);

        int n = W.Factions.Count - 1;
        float x0 = allButton.X + allButton.Width + 8;
        int perRow = Math.Max(1, (int)((strip.X + strip.Width - Pad - x0) / Chip));
        for (int f = 1; f <= n; f++)
        {
            int i = f - 1;
            var r = new Rectangle(x0 + i % perRow * Chip, strip.Y + Pad + i / perRow * (ChipH + 2), Chip - 2, ChipH);
            DrawChip(r, f.ToString(), Palette.Player(f), game.View == f, W.Faction(f).Defeated, mouse, f);
        }
    }

    /// <summary>배속 선택 버튼 줄: "배속" + 0.5 · 1 · 1.5 · 2 · 3 · 4 · 6 · 8 (클릭 = 그 배속, [ ] 키와 같음).</summary>
    void DrawSpeeds(Vector2 mouse)
    {
        speedHits.Clear();
        var opts = game.SpeedOptions;
        float labelW = 46;
        Ui.TextCentered(S.SpeedShort, new Rectangle(speedRow.X, speedRow.Y, labelW, speedRow.Height), 15, Palette.TextDim);
        float bw = (speedRow.Width - labelW - 4) / opts.Count;
        for (int i = 0; i < opts.Count; i++)
        {
            var r = new Rectangle(speedRow.X + labelW + 4 + i * bw, speedRow.Y, bw - 2, speedRow.Height);
            bool sel = game.SpeedIndex == i, hover = Raylib.CheckCollisionPointRec(mouse, r);
            Raylib.DrawRectangleRounded(r, 0.2f, 4, sel ? Palette.Alpha(Palette.Select, 200) : hover ? Palette.BtnHover : Palette.Btn);
            Ui.TextCentered($"{opts[i]:0.#}×", r, 15, sel ? Color.Black : Palette.Text);
            speedHits.Add((r, i));
        }
    }

    void DrawChip(Rectangle r, string label, Color color, bool selected, bool dim, Vector2 mouse, int faction)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, r);
        Color fill = selected ? Palette.Alpha(color, 200) : hover ? Palette.BtnHover : Palette.Btn;
        Raylib.DrawRectangleRounded(r, 0.2f, 4, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, selected ? 2 : 1, selected ? Color.White : Palette.Alpha(color, dim ? 70 : 200));
        Color ink = selected ? Color.Black : dim ? Palette.TextDim : Palette.Text;
        Ui.TextCentered(label, r, 16, ink);
        hits.Add((r, faction));
    }

    void DrawBoard(Vector2 mouse)
    {
        Ui.Panel(board, Palette.Alpha(Palette.PanelDark, 225), Palette.PanelBorder);
        int n = W.Factions.Count - 1;
        var order = Enumerable.Range(1, n)
            .OrderBy(f => W.Faction(f).Defeated)
            .ThenByDescending(f => points[f])
            .ThenByDescending(f => army[f])
            .ThenBy(f => f)
            .ToList();

        var inner = new Rectangle(board.X + 1, board.Y + 1, board.Width - 2, board.Height - 2);
        float content = order.Count * RowH;
        scroll = MathF.Min(scroll, MathF.Max(0, content - inner.Height));

        Raylib.BeginScissorMode((int)inner.X, (int)inner.Y, (int)inner.Width, (int)inner.Height);
        for (int i = 0; i < order.Count; i++)
        {
            var row = new Rectangle(inner.X, inner.Y + i * RowH - scroll, inner.Width, RowH);
            if (row.Y + row.Height < inner.Y || row.Y > inner.Y + inner.Height) continue;
            DrawRow(row, order[i], mouse);
        }
        Raylib.EndScissorMode();

        if (content > inner.Height)   // 스크롤 바
        {
            float th = MathF.Max(24, inner.Height * inner.Height / content);
            float ty = inner.Y + (inner.Height - th) * (scroll / (content - inner.Height));
            Raylib.DrawRectangleRec(new Rectangle(inner.X + inner.Width - 4, ty, 3, th), Palette.Alpha(Palette.TextDim, 160));
        }
    }

    void DrawRow(Rectangle row, int f, Vector2 mouse)
    {
        var fac = W.Faction(f);
        bool dead = fac.Defeated;
        bool hover = Raylib.CheckCollisionPointRec(mouse, row) && Raylib.CheckCollisionPointRec(mouse, board);
        Color baseCol = Palette.Player(f);
        if (game.View == f) Raylib.DrawRectangleRec(row, Palette.Alpha(baseCol, 45));
        else if (hover) Raylib.DrawRectangleRec(row, Palette.Alpha(Palette.BtnHover, 120));
        Raylib.DrawLineEx(new(row.X, row.Y + row.Height - 1), new(row.X + row.Width, row.Y + row.Height - 1), 1, Palette.Alpha(Palette.PanelBorder, 90));
        hits.Add((Raylib.GetCollisionRec(row, board), f));

        Color dim(Color c) => dead ? Palette.Alpha(c, 100) : c;
        float x = row.X + 8, y = row.Y + 3;
        Raylib.DrawRectangleRec(new Rectangle(x, y + 3, 10, 12), dim(baseCol));
        x += 18;
        Ui.Text(fac.Name, x, y, 16, dim(Palette.Text));
        x += Ui.Measure(fac.Name, 16).X + 10;
        string ai = game.AiTypeName ?? (fac.IsAi ? S.AiTag : "");
        if (ai != "") Ui.Text(ai, x, y, 16, dim(Palette.TextDim));

        // 오른쪽: 관계 요약 (멸망이면 멸망 표시)
        string rel;
        if (dead) rel = S.BoardDefeated;
        else
        {
            int war = 0, ally = 0;
            for (int g = 1; g < W.Factions.Count; g++)
            {
                if (g == f || W.Faction(g).Defeated) continue;
                if (W.Allied(f, g)) ally++;   // 정적 팀
                else war++;
            }
            rel = war + ally == 0 ? S.BoardNoRel : war > 0 && ally > 0 ? $"{string.Format(S.BoardWar, war)} · {string.Format(S.BoardAlly, ally)}"
                : war > 0 ? string.Format(S.BoardWar, war) : string.Format(S.BoardAlly, ally);
        }
        Ui.Text(rel, row.X + row.Width - 10 - Ui.Measure(rel, 16).X, y, 16, dead ? Palette.Alpha(Palette.HpLow, 160) : RelColor(rel));

        // 둘째 줄: 병력 · 거점 · 건물
        float y2 = row.Y + 21;
        x = row.X + 26;
        string[] rest = { $"{S.BoardArmy} {army[f]}", $"{S.BoardPoints} {points[f]}", $"{S.BoardBuildings} {buildings[f]}" };
        foreach (var s in rest)
        {
            Ui.Text(s, x, y2, 16, dim(Palette.Text));
            x += Ui.Measure(s, 16).X + 12;
        }
    }

    static Color RelColor(string rel) => rel.StartsWith(S.RelWar) ? Palette.Warning : Palette.TextDim;
}
