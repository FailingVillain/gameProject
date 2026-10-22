using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.UI;

/// <summary>커맨드 카드 버튼 한 칸. Game.CommandCard() 가 선택 상태에 맞춰 만든다.</summary>
public sealed record CardButton(
    string Key, string Label, string Tooltip, string HotkeyLabel, KeyboardKey Hotkey, int Col, int Row,
    string Icon, bool Enabled = true, bool Active = false, string? CostLine = null, string? Value = null);

/// <summary>커맨드 카드 탭 (v0.4 D): 명령 · 전투.</summary>
public enum CardTab { Command = 0, Combat = 1 }

/// <summary>
/// 화면 레이아웃과 HUD.
///   상단 바:  자금 · 인구 · 상태 메시지 · 속도 · 버튼
///   하단 HUD: [미니맵] [선택 정보] [커맨드 카드 3×3]
///   그 사이가 게임 뷰포트.
/// </summary>
public sealed class Hud : IDisposable
{
    const float Pad = 8, Gap = 6, CardInner = 8;

    readonly Game game;
    public readonly Minimap Minimap;

    public Rectangle TopBar { get; private set; }
    public Rectangle Bottom { get; private set; }
    public Rectangle Viewport { get; private set; }
    public Rectangle MinimapPanel { get; private set; }
    public Rectangle Info { get; private set; }
    public Rectangle Card { get; private set; }
    float btnSize;
    const float TabH = 20, TabTop = 4;
    float GridTop => Card.Y + TabTop + TabH + 4;
    int multiPage;                 // 다중 선택 정보 패널의 쪽 (0부터)
    const int MultiPageSize = 25;

    static readonly (string Key, string Label)[] TopButtonDefs = { ("board", S.BoardButton), ("save", S.SaveButton), ("load", S.LoadButton), ("pause", S.Pause), ("speed", S.SpeedShort), ("grid", S.Grid), ("icons", S.SquadIcons), ("quit", S.Quit) };
    /// <summary>이 게임에서 쓰는 상단 버튼 (현황은 관전자만, 메뉴에서 왔으면 종료 = 메뉴로).</summary>
    IEnumerable<(string Key, string Label)> ActiveButtons => TopButtonDefs
        .Where(d => game.Observer || d.Key != "board")
        .Where(d => !(game.IsReplay && d.Key is "save" or "load"))   // 리플레이 재생은 저장 · 불러오기 없음
        .Select(d => d.Key == "quit" && game.FromMenu ? (d.Key, S.ToMenu) : (d.Key == "speed" ? (d.Key, $"{game.Speed:0.0#}×") : d));
    readonly Dictionary<string, Rectangle> topButtons = new();
    Rectangle restartButton, quitButton;

    public Hud(Game game, GameMap map)
    {
        this.game = game;
        Minimap = new Minimap(map);
    }

    public void Layout(int w, int h)
    {
        TopBar = new Rectangle(0, 0, w, Config.TopBarHeight);
        Bottom = new Rectangle(0, h - Config.HudHeight, w, Config.HudHeight);
        Viewport = new Rectangle(0, Config.TopBarHeight, w, MathF.Max(1, h - Config.TopBarHeight - Config.HudHeight));

        float inner = Config.HudHeight - 2 * Pad;
        MinimapPanel = new Rectangle(Pad, Bottom.Y + Pad, inner, inner);
        Minimap.Layout(new Rectangle(MinimapPanel.X + 5, MinimapPanel.Y + 5, MinimapPanel.Width - 10, MinimapPanel.Height - 10));

        btnSize = MathF.Floor((inner - TabTop - TabH - 4 - CardInner - 2 * Gap) / 3);
        float cardW = 3 * btnSize + 2 * Gap + 2 * CardInner;
        Card = new Rectangle(w - Pad - cardW, Bottom.Y + Pad, cardW, inner);
        float ix = MinimapPanel.X + MinimapPanel.Width + Pad;
        Info = new Rectangle(ix, Bottom.Y + Pad, MathF.Max(0, Card.X - Pad - ix), inner);

        topButtons.Clear();
        float x = w - 6;
        var active = ActiveButtons.ToArray();
        for (int i = active.Length - 1; i >= 0; i--)
        {
            var (key, label) = active[i];
            float bw = Ui.Measure(label, 16).X + 20;
            x -= bw;
            topButtons[key] = new Rectangle(x, 4, bw, Config.TopBarHeight - 8);
            x -= 6;
        }

        var c = new Vector2(Viewport.X + Viewport.Width / 2, Viewport.Y + Viewport.Height / 2);
        restartButton = new Rectangle(c.X - 170, c.Y + 30, 160, 36);
        quitButton = new Rectangle(c.X + 10, c.Y + 30, 160, 36);
    }

    // ---------------------------------------------------------------- 입력 판정

    public bool Contains(Vector2 p) => Ui.Contains(TopBar, p) || Ui.Contains(Bottom, p);

    public string? TopButtonAt(Vector2 p)
    {
        foreach (var (key, r) in topButtons)
            if (Ui.Contains(r, p)) return key;
        return null;
    }

    Rectangle SlotRect(int col, int row) => new(
        Card.X + CardInner + col * (btnSize + Gap), GridTop + row * (btnSize + Gap), btnSize, btnSize);

    public Rectangle TabRect(int i)
    {
        float w = (Card.Width - 2 * 4 - 1 * 3) / 2;
        return new Rectangle(Card.X + 4 + i * (w + 3), Card.Y + TabTop, w, TabH);
    }

    /// <summary>카드 탭 버튼 위치 (-1 = 아님). 관전은 탭 없음.</summary>
    public int TabAt(Vector2 p)
    {
        if (game.Observer) return -1;
        for (int i = 0; i < 2; i++) if (Ui.Contains(TabRect(i), p)) return i;
        return -1;
    }

    // ---- 다중 선택 쪽 넘김 (25기씩)
    public Rectangle PagerRect(int dir, int selCount)
    {
        var r = new Rectangle(Info.X + Info.Width - 14 - 28, Info.Y + 10, 28, 24);
        return dir < 0 ? new Rectangle(r.X - 110 - 28 - 4, r.Y, 28, 24) : r;
    }
    int PageCount(int selCount) => Math.Max(1, (selCount + MultiPageSize - 1) / MultiPageSize);

    /// <summary>다중 선택 쪽 버튼 위치: -1 = 이전, +1 = 다음, 0 = 아님.</summary>
    public int PagerAt(Vector2 p)
    {
        int n = game.Selection.Count;
        if (n <= MultiPageSize) return 0;
        if (Ui.Contains(PagerRect(-1, n), p)) return -1;
        if (Ui.Contains(PagerRect(+1, n), p)) return 1;
        return 0;
    }

    public int MultiPage => multiPage;
    public void ResetPage() => multiPage = 0;
    public void MovePage(int d)
    {
        int n = game.Selection.Count;
        multiPage = Math.Clamp(multiPage + d, 0, PageCount(n) - 1);
    }

    public CardButton? CardAt(Vector2 p) => game.CommandCard().FirstOrDefault(b => Ui.Contains(SlotRect(b.Col, b.Row), p));


    public string? GameOverButtonAt(Vector2 p) =>
        Ui.Contains(restartButton, p) ? "restart" : Ui.Contains(quitButton, p) ? "quit" : null;

    // ---------------------------------------------------------------- 그리기

    public void Draw(Vector2 mouse)
    {
        DrawTopBar(mouse);
        DrawBottom(mouse);
        DrawGroupBar(mouse);
        if (game.World.Winner != 0) DrawGameOver(mouse);
    }

    // ---- 대대 막대: 뷰포트 왼쪽 아래(하단 HUD 바로 위). 맨 앞 [기본] 대형 단추 + 편성된 대대 1~9. 위쪽 = 선택, 아래쪽 = 대형 단추
    const float GroupW = 80, GroupH = 40, GroupGap = 4, GroupFormH = 17;
    float BarY => Bottom.Y - GroupH - 6;

    IEnumerable<(int N, Rectangle R)> BatSlots()
    {
        if (game.Observer) yield break;
        float x = Viewport.X + 8, y = BarY;
        yield return (0, new Rectangle(x, y, GroupW, GroupH));   // 0 = 기본 대형
        x += GroupW + GroupGap;
        for (int n = 1; n <= 9; n++)
        {
            var r = game.World.BattalionByNumber(game.Me, n);
            if (r == null || r.Companies.Count == 0) continue;
            yield return (n, new Rectangle(x, y, GroupW, GroupH));
            x += GroupW + GroupGap;
        }
    }

    Rectangle BarRect => new(Viewport.X + 8, BarY, 10 * (GroupW + GroupGap), GroupH);
    Rectangle MergeRect { get { float x = Viewport.X + 8; foreach (var (_, r) in BatSlots()) x = r.X + r.Width; return new Rectangle(x + 8, BarY, 76, GroupH); } }

    static Rectangle FormRect(Rectangle r) => new(r.X + 1, r.Y + r.Height - GroupFormH - 1, r.Width - 2, GroupFormH);

    /// <summary>대대 번호 칸 위(선택 영역)의 대대. 대형 단추 위는 제외.</summary>
    public int? BatAt(Vector2 p)
    {
        foreach (var (n, r) in BatSlots()) if (n > 0 && Ui.Contains(r, p) && !Ui.Contains(FormRect(r), p)) return n;
        return null;
    }

    /// <summary>대대 칸 전체 (카드를 끌어다 놓는 대상).</summary>
    public int? BatSlotAt(Vector2 p)
    {
        foreach (var (n, r) in BatSlots()) if (n > 0 && Ui.Contains(r, p)) return n;
        return null;
    }

    /// <summary>대형 단추 위의 칸 (0 = 기본 대형).</summary>
    public int? BatFormAt(Vector2 p)
    {
        foreach (var (n, r) in BatSlots()) if (Ui.Contains(FormRect(r), p)) return n;
        return null;
    }

    /// <summary>대대 막대 영역 안인가 (우클릭 이동 명령이 새지 않게 · 카드를 빈 곳에 놓으면 대대에서 분리).</summary>
    public bool OverBatBar(Vector2 p) => !game.Observer && (Ui.Contains(BarRect, p) || (MergeVisible && Ui.Contains(MergeRect, p)));

    bool MergeVisible => !game.Observer && game.SelectedCompanies().Count > 0;
    public bool MergeButtonAt(Vector2 p) => MergeVisible && Ui.Contains(MergeRect, p);

    // ---- 대대 목록 패널 (0 키): 연대 › 대대 › 중대 트리 구조
    const float ListRowH = 22, ListW = 380;
    const int ListMaxRows = 18;
    Rectangle listPanel;

    List<(Rectangle R, object Item)> BatListRows()
    {
        var res = new List<(Rectangle, object)>();
        if (game.Observer || !game.ShowBatList) return res;
        var w = game.World;
        var items = new List<object>();

        // 내 대대들을 연대 번호별로 그룹화
        var bats = w.BattalionsOf(game.Me).Where(r => r.Companies.Count > 0).ToList();
        var regGroups = bats.GroupBy(game.RegimentOf).OrderBy(g => g.Key).ToList();
        foreach (var rg in regGroups)
        {
            items.Add(rg.Key); // int: 연대 헤더
            foreach (var b in rg.OrderBy(b => b.Number == 0 ? 100 : b.Number).ThenBy(b => b.Id))
            {
                items.Add(b); // Battalion: 대대
                foreach (var c in b.Companies.OrderBy(c => c.Number).ThenBy(c => c.Id))
                {
                    items.Add(c); // Company: 소속 중대
                }
            }
        }

        // 독립 중대
        var indeps = w.CompaniesOf(game.Me).Where(c => c.Battalion == null && c.Members.Count > 0).OrderBy(c => c.Number).ThenBy(c => c.Id).ToList();
        foreach (var c in indeps) items.Add(c);

        // T38e: 18행 초과는 스크롤 (휠 · 막대 끌기 · 선택 행이 보이게 자동)
        listTotal = items.Count;
        int rows = Math.Min(items.Count, ListMaxRows);
        listFirstSel = FirstSelectedIndex(items);
        if (listFirstSel != listLastSel) { listLastSel = listFirstSel; if (listFirstSel >= 0) listScroll = ListEnsureVisible(listScroll, listFirstSel, items.Count, ListMaxRows); }
        listScroll = ListClamp(listScroll, items.Count, ListMaxRows);
        bool bar = items.Count > ListMaxRows;
        float h = 26 + Math.Max(1, rows) * ListRowH + 4;
        float x = Viewport.X + 8, top = MathF.Max(TopBar.Height + 4, BarY - 6 - h);
        float rw = ListW - 8 - (bar ? ListBarW + 2 : 0);
        for (int i = 0; i < rows; i++) res.Add((new Rectangle(x + 4, top + 26 + i * ListRowH, rw, ListRowH - 2), items[listScroll + i]));
        listPanel = new Rectangle(x, top, ListW, h);
        listTrack = bar ? new Rectangle(x + ListW - ListBarW - 4, top + 26, ListBarW, rows * ListRowH) : default;
        return res;
    }

    // ---- T38e: 목록 스크롤 (화면 전용). 순수 함수는 테스트가 직접 부른다.
    const float ListBarW = 8;
    int listScroll, listTotal, listFirstSel = -1, listLastSel = -1;
    bool listBarDrag;
    Rectangle listTrack;

    /// <summary>스크롤 시작 행을 [0, total-visible] 로.</summary>
    public static int ListClamp(int scroll, int total, int visible) => Math.Clamp(scroll, 0, Math.Max(0, total - visible));

    /// <summary>idx 행이 보이도록 최소로 움직인 시작 행.</summary>
    public static int ListEnsureVisible(int scroll, int idx, int total, int visible)
    {
        if (idx < scroll) scroll = idx;
        else if (idx >= scroll + visible) scroll = idx - visible + 1;
        return ListClamp(scroll, total, visible);
    }

    /// <summary>막대 위치 y (트랙 안 0..1 비율) → 시작 행.</summary>
    public static int ListScrollFromFraction(float frac, int total, int visible) =>
        ListClamp((int)MathF.Round(Math.Clamp(frac, 0f, 1f) * Math.Max(0, total - visible)), total, visible);

    int FirstSelectedIndex(List<object> items)
    {
        if (game.Selection.Count == 0) return -1;
        for (int i = 0; i < items.Count; i++)
            if (items[i] is Company c && c.Members.Count > 0 && c.Members.All(u => u.Selected)) return i;
        return -1;
    }

    /// <summary>휠: 목록 위에서 스크롤 (먹었으면 true — 카메라 확대로 새지 않게).</summary>
    public bool ListWheel(Vector2 p, float wheel)
    {
        if (!OverBatList(p)) return false;
        if (listTotal > ListMaxRows) listScroll = ListClamp(listScroll - (int)MathF.Sign(wheel) * 3, listTotal, ListMaxRows);
        return true;
    }

    /// <summary>막대 위 누름 (끌기 시작 · 트랙 클릭은 그 위치로 점프). 먹었으면 true.</summary>
    public bool ListBarPress(Vector2 p)
    {
        if (game.Observer || !game.ShowBatList) return false;
        BatListRows();
        if (listTrack.Width <= 0 || !Ui.Contains(listTrack, p)) return false;
        listBarDrag = true; ListBarDrag(p);
        return true;
    }

    public void SetListScroll(int row) => listScroll = ListClamp(row, listTotal, ListMaxRows);   // 콘솔 listscroll (시트 · 점검용)
    public bool ListBarDragging => listBarDrag;
    public void ListBarDrag(Vector2 p)
    {
        if (!listBarDrag) return;
        float thumbH = MathF.Max(16, listTrack.Height * ListMaxRows / listTotal);
        float frac = (p.Y - listTrack.Y - thumbH / 2) / MathF.Max(1, listTrack.Height - thumbH);
        listScroll = ListScrollFromFraction(frac, listTotal, ListMaxRows);
    }
    public void ListBarRelease() => listBarDrag = false;

    /// <summary>대대 목록 패널의 행 (Battalion 또는 Company).</summary>
    public object? BatListAt(Vector2 p)
    {
        foreach (var (r, item) in BatListRows()) if (Ui.Contains(r, p)) return item;
        return null;
    }

    public bool OverBatList(Vector2 p)
    {
        if (game.Observer || !game.ShowBatList) return false;
        BatListRows();
        return Ui.Contains(listPanel, p);
    }

    public static string BatFormName(BatForm f) => f switch
    {
        BatForm.Column => S.BatFormColumn, BatForm.Echelon => S.BatFormEchelon, BatForm.TwoFrontReserve => S.BatFormTwoFront, BatForm.Waves => S.BatFormWaves, _ => S.BatFormLine,
    };

    public static string TacticName(CompanyTactic t) => t switch
    {
        CompanyTactic.FireManoeuvre => S.TacFireManoeuvre, CompanyTactic.FlankLeft => S.TacFlankLeft, CompanyTactic.FlankRight => S.TacFlankRight,
        CompanyTactic.DefenceDepth => S.TacDefenceDepth, CompanyTactic.Artillery => S.TacArtillery, CompanyTactic.Assault => S.TacAssault, _ => S.TacNone,
    };

    public static string RoleName(UnitRole r) => r switch
    {
        UnitRole.Fire => S.RoleFire, UnitRole.Rifle => S.RoleRifle, UnitRole.Manoeuvre => S.RoleManoeuvre, UnitRole.Support => S.RoleSupport, UnitRole.Command => S.RoleCommand, _ => S.FormAuto,
    };

    public static string StanceName(string st) => st switch { "fire" => S.StanceFire, "advance" => S.StanceAdvance, _ => S.StanceHold };

    public static Color RoleColor(UnitRole r) => r switch
    {
        UnitRole.Fire => new Color(235, 145, 40, 255), UnitRole.Manoeuvre => new Color(225, 80, 80, 255), UnitRole.Support => new Color(175, 125, 225, 255),
        UnitRole.Command => new Color(235, 215, 95, 255), _ => new Color(120, 175, 235, 255),
    };

    /// <summary>분대의 대표 역할 (구성원 다수, 같으면 앞 번호).</summary>
    public static UnitRole SquadRole(Squad q)
    {
        Span<int> cnt = stackalloc int[6];
        foreach (var u in q.Members) cnt[(int)u.Role]++;
        int best = 1;
        for (int i = 1; i < 6; i++) if (cnt[i] > cnt[best]) best = i;
        return (UnitRole)best;
    }

    static int Strength(IEnumerable<Company> cs) => cs.Sum(c => c.Members.Count);

    public static string FormName(FormType t) => t switch
    {
        FormType.Line => S.FormLine, FormType.Column => S.FormColumn, FormType.Wedge => S.FormWedge, FormType.Box => S.FormBox, FormType.Spread => S.FormSpread,
        FormType.EchelonLeft => S.FormEchelonLeft, FormType.EchelonRight => S.FormEchelonRight, _ => S.CbFormFree,
    };

    /// <summary>분대 대형 이름 (Free = 자동).</summary>
    public static string SquadFormName(FormType t) => t == FormType.Free ? S.FormAuto : FormName(t);

    public static string OrderName(FormOrder o) => o switch
    {
        FormOrder.ArmorFront => S.OrdArmorFront, FormOrder.InfantryFront => S.OrdInfFront, _ => S.OrdMixed,
    };

    /// <summary>진형 모양 아이콘: 점으로 슬롯 배치를 그린다 (위쪽 = 진행 방향).</summary>
    internal static void DrawFormIcon(FormType t, float x, float y, Color c)
    {
        void D(float dx, float dy) => Raylib.DrawRectangle((int)(x + dx), (int)(y + dy), 3, 3, c);
        switch (t)
        {
            case FormType.Line: for (int i = 0; i < 5; i++) D(i * 4, 5); break;
            case FormType.Column: for (int i = 0; i < 4; i++) { D(4, i * 4 - 2); D(8, i * 4 - 2); } break;
            case FormType.Wedge: D(8, -2); D(6, 2); D(10, 2); D(4, 6); D(8, 6); D(12, 6); break;
            case FormType.Box: for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) D(2 + i * 4, j * 4 - 2); break;
            case FormType.Spread: D(0, 4); D(6, 0); D(11, 6); D(3, 8); D(9, 10); D(15, 2); break;
            case FormType.EchelonLeft: for (int i = 0; i < 5; i++) D(i * 4, 9 - (4 - i) * 2); break;    // 왼쪽 끝이 뒤로
            case FormType.EchelonRight: for (int i = 0; i < 5; i++) D(i * 4, 9 - i * 2); break;         // 오른쪽 끝이 뒤로
            default: D(4, 2); D(7, 2); D(10, 2); D(4, 5); D(7, 5); D(10, 5); D(4, 8); D(7, 8); D(10, 8); break;   // 해제 = 밀집
        }
    }

    void DrawGroupBar(Vector2 mouse)
    {
        if (game.Observer) return;
        string? tip = null;
        var drag = game.DragCard;
        if (drag != null) Raylib.DrawRectangleRec(BarRect, Palette.Alpha(Palette.Select, 22));
        foreach (var (n, r) in BatSlots())
        {
            var fr = FormRect(r);
            bool hover = Ui.Contains(r, mouse), formHover = Ui.Contains(fr, mouse);
            var reg = n == 0 ? null : game.World.BattalionByNumber(game.Me, n);
            var members = reg == null ? new List<Unit>() : reg.Companies.SelectMany(c => c.Members).ToList();
            bool selected = members.Count > 0 && members.All(e => e.Selected);
            bool dropHover = drag != null && n > 0 && hover;
            Raylib.DrawRectangleRec(r, Palette.Alpha(selected ? Palette.BtnActive : (hover && !formHover) || dropHover ? Palette.BtnHover : Palette.PanelDark, 225));
            Raylib.DrawRectangleLinesEx(r, dropHover ? 2 : 1, dropHover ? Palette.Select : hover || selected ? Palette.Accent : Palette.PanelBorder);
            if (n == 0) Ui.Text(S.FormDefaultShort, r.X + 6, r.Y + 4, 16, Palette.TextDim);
            else
            {
                Ui.Text(n.ToString(), r.X + 6, r.Y + 4, 16, Palette.Accent);
                string cnt = string.Format(S.BatCount, reg!.Companies.Count);
                Ui.Text(cnt, r.X + r.Width - 6 - Ui.Measure(cnt, 14).X, r.Y + 5, 14, Palette.Text);
            }
            // 아래쪽: 대형 단추
            Raylib.DrawRectangleRec(fr, Palette.Alpha(formHover ? Palette.BtnHover : Palette.PanelBorder, formHover ? 235 : 110));
            if (reg == null)
            {
                var type = game.DefaultType; var order = game.DefaultOrder;
                Color ic = type == FormType.Free ? Palette.TextDim : Palette.Accent;
                DrawFormIcon(type, fr.X + 5, fr.Y + 5, ic);
                Ui.Text(FormName(type), fr.X + 25, fr.Y + 1, 14, type == FormType.Free ? Palette.TextDim : Palette.Text);
                if (type != FormType.Free && order != FormOrder.Mixed)
                    Ui.Text(order == FormOrder.ArmorFront ? S.OrdTagArmor : S.OrdTagInf, fr.X + fr.Width - 16, fr.Y + 1, 14, Palette.Warning);
            }
            else Ui.Text(BatFormName(reg.Form), fr.X + 6, fr.Y + 1, 14, Palette.Text);
            if (formHover) tip = n == 0 ? S.FormTipDefault : S.FormTip;
            else if (hover && drag == null && n > 0) tip = S.BatBarTip;
        }
        if (MergeVisible)
        {
            var mr = MergeRect;
            bool mh = Ui.Contains(mr, mouse);
            Ui.Button(mr, S.Merge + " (G)", mh, false);
            if (mh) tip = S.MergeTip;
        }
        if (game.ShowBatList) DrawBatList(mouse);
        if (tip != null)
        {
            var sz = Ui.Measure(tip, 14);
            var gx = Viewport.X + 8;
            float ty = BarY - 22;
            Raylib.DrawRectangle((int)gx, (int)ty, (int)sz.X + 12, 20, Palette.Alpha(Palette.PanelDark, 235));
            Ui.Text(tip, gx + 6, ty + 2, 14, Palette.Text);
        }
        if (drag != null)
        {
            string label = string.Format(S.CompanyName, drag.Number);
            var gr = new Rectangle(mouse.X + 10, mouse.Y + 6, Ui.Measure(label, 16).X + 16, 24);
            Raylib.DrawRectangleRec(gr, Palette.Alpha(Palette.BtnActive, 230));
            Raylib.DrawRectangleLinesEx(gr, 1, Palette.Accent);
            Ui.Text(label, gr.X + 8, gr.Y + 3, 16, Palette.Text);
        }
    }

    void DrawBatList(Vector2 mouse)
    {
        var rows = BatListRows();
        Ui.Panel(listPanel, Palette.Alpha(Palette.PanelDark, 235), Palette.PanelBorder);
        Ui.Text(S.BatListTitle + " (0)", listPanel.X + 8, listPanel.Y + 4, 16, Palette.Accent);
        if (listTotal > ListMaxRows)
        {
            string range = string.Format(S.BatListRange, listScroll + 1, listScroll + ListMaxRows, listTotal);
            Ui.Text(range, listPanel.X + listPanel.Width - Ui.Measure(range, 14).X - 10, listPanel.Y + 6, 14, Palette.TextDim);
            Ui.RegHit(listTrack, "listbar");
            Raylib.DrawRectangleRec(listTrack, Palette.Alpha(Palette.Panel, 200));
            float thumbH = MathF.Max(16, listTrack.Height * ListMaxRows / listTotal);
            float ty = listTrack.Y + (listTrack.Height - thumbH) * listScroll / (listTotal - ListMaxRows);
            Raylib.DrawRectangleRec(new Rectangle(listTrack.X, ty, listTrack.Width, thumbH), listBarDrag ? Palette.Accent : Palette.PanelBorder);
        }
        if (rows.Count == 0) Ui.Text(S.BatListEmpty, listPanel.X + 8, listPanel.Y + 30, 14, Palette.TextDim);
        bool dropHover = game.DragCard != null && Ui.Contains(listPanel, mouse);
        if (dropHover) Raylib.DrawRectangleLinesEx(listPanel, 2, Palette.Select);
        foreach (var (r, item) in rows)
        {
            bool hv = Ui.Contains(r, mouse);
            Ui.RegHit(r, "listrow");
            string text; bool sel; float ind = 6;
            if (item is int regNum)
            {
                var rBats = game.World.BattalionsOf(game.Me).Where(b => game.RegimentOf(b) == regNum && b.Companies.Count > 0).ToList();
                var rUnits = rBats.SelectMany(b => b.Companies).SelectMany(c => c.Members).Where(u => u.Alive).ToList();
                text = $"[{string.Format(S.RegimentName, regNum)}] · {rBats.Count}대대 · {string.Format(S.UnitsShort, rUnits.Count)}";
                sel = rUnits.Count > 0 && rUnits.All(u => u.Selected);
                ind = 6;
            }
            else if (item is Battalion reg)
            {
                string nm = reg.Number > 0 ? string.Format(S.BattalionName, reg.Number) : S.BattalionNoNumber;
                var bUnits = reg.Companies.SelectMany(c => c.Members).Where(u => u.Alive).ToList();
                int ammoSum = 0, ammoMax = 0;
                foreach (var u in bUnits) { ammoSum += u.Ammo; ammoMax += u.Weapon?.Ammo ?? 0; }
                int ammoPct = ammoMax > 0 ? (int)MathF.Round(ammoSum * 100f / ammoMax) : 100;
                text = $"└ {nm} · {string.Format(S.BatCount, reg.Companies.Count)} · {string.Format(S.UnitsShort, bUnits.Count)} · {ammoPct}%";
                sel = bUnits.Count > 0 && bUnits.All(u => u.Selected);
                ind = 16;
            }
            else
            {
                var c = (Company)item;
                int ammoSum = 0, ammoMax = 0;
                foreach (var u in c.Members.Where(m => m.Alive)) { ammoSum += u.Ammo; ammoMax += u.Weapon?.Ammo ?? 0; }
                int ammoPct = ammoMax > 0 ? (int)MathF.Round(ammoSum * 100f / ammoMax) : 100;
                bool wiped = !c.Members.Any(m => m.Alive);   // T27 ③: 전멸 중대는 상태 문구로 표시 (행은 유지)
                string st = wiped ? S.UnitListWiped
                    : game.World.ContactTrigger(c) ? S.UnitListEngage
                    : c.Marching ? S.UnitListMove
                    : c.Members.Any(u => u.Order == UnitOrder.Hold) ? S.UnitListHold
                    : S.UnitListIdle;
                string prefix = c.Battalion != null ? "   · " : "· ";
                text = $"{prefix}{string.Format(S.CompanyName, c.Number)} · {c.Members.Count}/{c.InitialStrength} · {ammoPct}% · {st}";
                sel = c.Members.Count > 0 && c.Members.All(u => u.Selected);
                ind = c.Battalion != null ? 24 : 10;
            }
            Raylib.DrawRectangleRec(r, Palette.Alpha(sel ? Palette.BtnActive : hv ? Palette.BtnHover : Palette.Btn, 220));
            Ui.Text(text, r.X + ind, r.Y + 2, 14, item is Company wc && !wc.Members.Any(m => m.Alive) ? Palette.TextDim : Palette.Text);
        }
    }

    void DrawTopBar(Vector2 mouse)
    {
        Raylib.DrawRectangleRec(TopBar, Palette.TopBar);
        Paper.Grain(TopBar);
        Raylib.DrawLineEx(new(0, TopBar.Height), new(TopBar.Width, TopBar.Height), 1, Palette.HudTrim);

        float y = (TopBar.Height - 16) / 2;
        Ui.Text(S.GameTitle, 10, y, 16, Palette.Accent);
        float x = 10 + Ui.Measure(S.GameTitle, 16).X + 20;
        if (game.Observer) x = DrawObserverInfo(x, y);
        else x = DrawPlayerInfo(x, y);

        float buttonsLeft = topButtons.Values.Min(r => r.X);
        string spd = game.Paused ? S.Paused : $"{S.Speed} {game.Speed:0.0#}×";
        string wx = game.WeatherLabel;   // 날씨 · 시간대
        string perf = $"{wx}  ·  {spd}  ·  FPS {Raylib.GetFPS()}" + (game.Observer ? "" : $"  ·  sim {game.LastStepMs:0.00}ms");
        if (buttonsLeft - Ui.Measure(perf, 16).X - 14 < x) perf = $"{wx}  ·  {spd}  ·  {Raylib.GetFPS()}";   // 자원 줄이 길면 간략하게
        if (buttonsLeft - Ui.Measure(perf, 16).X - 14 < x) perf = $"{wx}  ·  " + (game.Paused ? S.Paused : $"{game.Speed:0.0#}×");
        float perfX = buttonsLeft - Ui.Measure(perf, 16).X - 14;

        string? status = game.StatusText;
        bool drawPerf = true;
        if (status != null)
        {
            float sw = Ui.Measure(status, 16).X;
            float sx = MathF.Max(x, MathF.Min((TopBar.Width - sw) / 2, perfX - sw - 16));   // 속도 · FPS 표시와 겹치지 않게 왼쪽으로 비킨다
            Ui.Text(status, sx, y, 16, Palette.Warning);
            drawPerf = sx + sw + 12 <= perfX;   // 그래도 안 들어가면 알림이 우선 (속도 표시는 잠깐 숨김)
        }
        if (drawPerf) Ui.Text(perf, perfX, y, 16, game.Paused ? Palette.Warning : Palette.TextDim);
        var labels = ActiveButtons.ToDictionary(d => d.Key, d => d.Label);
        foreach (var (key, r) in topButtons)
        {
            string label = labels[key];
            bool active = (key == "grid" && game.ShowGrid) || (key == "icons" && SquadIcons.Enabled) || (key == "pause" && game.Paused) || (key == "board" && game.Spectator.Visible);
            Ui.Button(r, label, Ui.Contains(r, mouse), active);
        }
    }

    /// <summary>관전자 상단 정보: 관전 중 · 경과 시간 · 생존 세력 수 · 현재 시점.</summary>
    float DrawObserverInfo(float x, float y)
    {
        string mode = game.IsSpectate ? S.Spectating : S.ReplayLabel;
        Ui.Text(mode, x, y, 16, Palette.Warning);
        x += Ui.Measure(mode, 16).X + 22;
        string elapsed = $"{S.Elapsed} {Game.Clock(game.World.Tick)}";
        Ui.Text(elapsed, x, y, 16, Palette.Text);
        x += Ui.Measure(elapsed, 16).X + 22;
        int total = game.World.Factions.Count - 1, alive = game.World.Factions.Skip(1).Count(f => !f.Defeated);
        string aliveText = string.Format(S.AliveCount, alive, total);
        Ui.Text(aliveText, x, y, 16, Palette.Text);
        x += Ui.Measure(aliveText, 16).X + 22;
        string view = $"{S.ViewLabel}: {(game.View == 0 ? S.ViewAll : string.Format(S.ViewFaction, game.View))}";
        Ui.Text(view, x, y, 16, game.View == 0 ? Palette.TextDim : Palette.Player(game.View));
        return x + Ui.Measure(view, 16).X + 22;
    }

    /// <summary>상단 바 세력 정보 (방향 변경 §0-1: 자원 · 인구 없음 — 병력 · 도시 수). 다음 x 를 돌려준다.</summary>
    float DrawPlayerInfo(float x, float y)
    {
        var w = game.World;
        int army = 0, cities = 0;
        foreach (var u in w.UnitsOf(game.Me)) if (u.Alive && u.Owner == game.Me) army++;
        bool anyCity = false;
        foreach (var b in w.Buildings) if (b.Alive && b.Def.City) { anyCity = true; if (b.Owner == game.Me) cities++; }
        string text = w.Mission != null || !anyCity ? $"{S.ArmySize} {army}" : $"{S.ArmySize} {army}   {S.CityCount} {cities}";   // 미션 · 점령 도시가 없는 지도는 도시 수 숨김
        Ui.Text(text, x, y, 16, Palette.Text);
        return x + Ui.Measure(text, 16).X + 22;
    }

    void DrawBottom(Vector2 mouse)
    {
        Raylib.DrawRectangleRec(Bottom, Palette.HudBg);
        Paper.Grain(Bottom);
        Raylib.DrawRectangleRec(new Rectangle(0, Bottom.Y, Bottom.Width, 3), Palette.HudTrim);

        Ui.Panel(MinimapPanel, Palette.PanelDark, Palette.PanelBorder);
        Minimap.Draw(game.World, game.Camera, game.ViewSeat, game.FogRenderer, game.AlertPing);

        DrawInfo(mouse);
        DrawCommandCard(mouse);
    }

    // ---------------------------------------------------------------- 정보 패널

    void DrawInfo(Vector2 mouse)
    {
        Ui.Panel(Info, Palette.Panel, Palette.PanelBorder);
        var sel = game.Selection;
        if (sel.Count == 0)
        {
            Ui.Text(S.NoSelection, Info.X + 16, Info.Y + 14, 26, Palette.TextDim);
            // 관전 · 리플레이는 조작 안내 대신 관전 안내
            var hints = game.Observer ? new[] { S.ObsHint1, S.ObsHint2, S.ObsHint3 } : new[] { S.Hint1, S.Hint2, S.Hint3, S.Hint4 };
            for (int i = 0; i < hints.Length; i++) Ui.Text(hints[i], Info.X + 16, Info.Y + 64 + i * 26, 16, Palette.TextDim);
            return;
        }
        if (sel.Count > 1 && CardList() != null) DrawCompanies(mouse);
        else if (sel.Count > 1) DrawMulti(sel);
        else if (sel[0] is Unit u) DrawUnit(u);
        else if (sel[0] is Building b) DrawBuilding(b, mouse);
    }

    Rectangle Portrait => new(Info.X + 12, Info.Y + 12, 112, 112);

    float DrawHeader(Entity e, float x, float y)
    {
        Ui.Text(e.Name, x, y, 26, Palette.Text);
        string owner = game.OwnerName(e.Owner);
        Ui.Text(owner, x + Ui.Measure(e.Name, 26).X + 12, y + 8, 16, Palette.Player(e.Owner));
        y += 36;
        if (!e.Indestructible)
        {
            Ui.Text(S.Hp, x, y, 16, Palette.TextDim);
            UnitRenderer.DrawHpBar(new Rectangle(x + 44, y + 3, 180, 12), e.Hp / e.MaxHp);
            Ui.Text($"{MathF.Ceiling(e.Hp):0} / {e.MaxHp:0}", x + 234, y, 16, Palette.Text);
            y += 22;
        }
        return y;
    }

    void DrawUnit(Unit u)
    {
        // 테마 초상화(320×320)가 있으면 160×160 칸에 정확히 절반 크기로 (도트가 뭉개지지 않게)
        bool hd = Theme.Current?.Get("unit." + u.Def.Key)?.Portrait != null;
        var portrait = hd ? new Rectangle(Info.X + 10, Info.Y + (Info.Height - 160) / 2, 160, 160) : Portrait;
        Ui.Panel(portrait, Palette.PanelDark, Palette.PanelBorder);
        UnitRenderer.DrawPortrait(portrait, Palette.Player(u.Owner), u.Facing, u.Def.Key);

        float x = portrait.X + portrait.Width + 18;
        float y = DrawHeader(u, x, Info.Y + 12);
        Ui.Text($"{S.State}: {StateLabel(u)}", x, y, 16, Palette.Text);
        var (ammo, fuel) = SupplyBars(u);
        float bx = x + 250;
        if (ammo is float af) { Ui.Text(S.AmmoLabel, bx, y, 16, Palette.TextDim); BuildingRenderer.DrawProgress(new Rectangle(bx + 44, y + 3, 90, 10), af, AmmoColor); }
        if (fuel is float ff) { Ui.Text(S.FuelLabel, bx, y + 21, 16, Palette.TextDim); BuildingRenderer.DrawProgress(new Rectangle(bx + 44, y + 24, 90, 10), ff, FuelColor); }
        if (u.HasStamina && fuel == null) { Ui.Text(S.StaminaLabel, bx, y + 21, 16, Palette.TextDim); BuildingRenderer.DrawProgress(new Rectangle(bx + 44, y + 24, 90, 10), u.StaminaRatio, StaminaColor); }   // 스태미나 (ROADMAP §4)
        // 제압 (ROADMAP §2-4b 6차: 화면 표시) · 계급 (§2-4c 14차)
        float sx = bx + 150;
        if (u.Def.SuppressResist > 0) { Ui.Text(S.SuppressLabel, sx, y, 16, Palette.TextDim); BuildingRenderer.DrawProgress(new Rectangle(sx + 44, y + 3, 90, 10), u.SuppressionRatio, SuppressColor); Ui.Text(u.Suppression.ToString(), sx + 140, y, 16, Palette.TextDim); }
        string rank = u.IsOfficer ? OfficerLine(u) : $"{RankName(u)} · {S.KillsLabel} {u.Kills}";
        if (u.Squad is { } sq && ReferenceEquals(sq.Leader, u)) rank += $" · {S.SquadLeaderTag}";
        string aura = game.World.NearOfficer(u) ? $" · {S.OfficerAura}" : game.World.NcoLeads(u) ? $" · {S.NcoAura}" : "";
        if (u.Weapon != null) Ui.Text($"{S.RankLabel}: {rank}" + aura, sx, y + 21, 16, Palette.TextDim);
        y += 21;
        string weapon = u.Weapon is WeaponDef w
            ? $"{w.Name}  ({w.Damage:0} · {S.RangeShort} {w.Range:0.#} · {(w.RandomCooldown ? $"{w.CooldownMin:0.0#}~{w.CooldownMax:0.0#}" : $"{w.Cooldown:0.0#}")}{S.Seconds} · {S.Accuracy} {(w.AccNear > 0 ? $"{w.AccNear * 100:0}~{w.AccFar * 100:0}" : $"{w.Accuracy * 100:0}")}%)"
            : S.NoWeapon;
        Ui.Text($"{S.Weapon}: {weapon}", x, y, 16, Palette.TextDim);
        y += 21;
        string armorLine = $"{S.Armor}: {(u.Armor is ArmorDef a ? $"{a.Name}  ({a.Armor:0.#})" : S.NoWeapon)}";
        if (u.Melee is WeaponDef mw) armorLine += $" · {S.MeleeLabel}: {mw.Name} ({mw.Damage:0} · {mw.CooldownMin:0.0#}~{mw.CooldownMax:0.0#}{S.Seconds} · {S.Accuracy} {mw.Accuracy * 100:0}%)";
        Ui.Text(armorLine, x, y, 16, Palette.TextDim);
        y += 21;
        string cat = u.Def.IsArmored ? S.CatArmored : S.CatInfantry;
        string line = $"{S.Speed}: {u.Def.Speed:0.0} · {S.Sight}: {u.Def.Sight} · {S.CategoryLabel}: {cat}";
        Ui.Text(line, x, y, 16, Palette.TextDim);
        Ui.Text(u.Def.Description, hd ? x : Info.X + 12, Info.Y + Info.Height - 30, 16, Palette.TextDim);
    }

    void DrawBuilding(Building b, Vector2 mouse)
    {
        var portrait = Portrait;
        Ui.Panel(portrait, Palette.PanelDark, Palette.PanelBorder);
        BuildingRenderer.DrawPortrait(b.Def, Palette.Player(b.Owner), new Rectangle(portrait.X + 6, portrait.Y + 6, 100, 100));

        float x = portrait.X + portrait.Width + 18;
        float y = DrawHeader(b, x, Info.Y + 12);

        if (b.Def.Capturable)
        {
            string owner = game.OwnerName(b.Owner);
            Ui.Text($"{S.Owner}: {owner}", x, y, 16, Palette.Player(b.Owner));
            y += 21;
            // 내 시설이 완전히 점령된 상태면 게이지 줄은 생략 (생산 카드 자리 확보)
            if (b.Owner != game.Me || b.CaptureProgress < 1 || b.Contested)
            {
                Ui.Text(S.Capturing, x, y, 16, Palette.TextDim);
                BuildingRenderer.DrawProgress(new Rectangle(x + 76, y + 3, 150, 12), b.CaptureProgress, Palette.Player(b.CaptureFaction));
                Ui.Text($"{b.CaptureProgress * 100:0}%", x + 234, y, 16, Palette.Text);
                y += 21;
            }
            if (b.Contested) { Ui.Text(S.Contested, x, y, 16, Palette.Warning); y += 21; }
        }
        if (b.Def.City) { Ui.Text(string.Format(S.CityResupply, Defs.Current.Supply.CityRadius, Defs.Current.Supply.CityMul), x, y, 16, Palette.TextDim); y += 21; }
        Ui.Text(b.Def.Description, Info.X + 12, Info.Y + Info.Height - 30, 16, Palette.TextDim);
    }

    /// <summary>탄약 · 보급 마차 적재 비율 (0~1, 해당 없음 = null).</summary>
    static (float? Ammo, float? Fuel) SupplyBars(Unit u) => (u.UsesAmmo ? u.AmmoRatio : null, u.Def.IsSupplyWagon ? u.CargoRatio : null);

    static readonly Color AmmoColor = new(236, 204, 96, 255), FuelColor = new(224, 156, 86, 255), SuppressColor = new(214, 92, 92, 255), StaminaColor = new(110, 190, 120, 255);

    /// <summary>유닛의 계급 이름 (세력별 이름 — assets/data/ranks.json, 지도 factions[].en 키).</summary>
    string RankName(Unit u) => RankNames.Of(FactionKey(u.Owner), u);

    /// <summary>장교 정보: 계급 (임시 진급이면 '임시 · 정식 계급') · 직책 · 경험치 (단계).</summary>
    string OfficerLine(Unit u)
    {
        var w = game.World;
        string s = RankName(u);
        if (u.TempRank > 0) s += $" ({S.TempRankTag} · {RankNames.Officer(FactionKey(u.Owner), u.OfficerBase)})";
        string post = w.PostOf(u) switch { OfficerPost.Battalion => S.PostBattalion, OfficerPost.Assistant => S.PostAssistant, OfficerPost.Company => S.PostCompany, OfficerPost.Staff => S.PostStaff, _ => "" };
        if (post.Length > 0) s += $" · {post}";
        return s + $" · {S.XpLabel} {u.OfficerXp} (Lv{w.XpLevel(u)})";
    }

    /// <summary>중대 지휘자 요약 (상세 줄): 장교 계급 · 부사관 대리 · 인계 중 · 없음.</summary>
    string CommanderLine(Company c)
    {
        var w = game.World;
        var cmd = w.CommanderOf(c);
        string who = cmd == null ? S.NoCommander : cmd.IsOfficer ? RankName(cmd) : $"{RankName(cmd)} ({S.ActingNcoTag})";
        return $"{S.CommanderTag}: {who}" + (w.InHandover(c) ? $" ({S.HandoverTag})" : "");
    }

    string? FactionKey(int owner)
    {
        var fd = MapFile.FactionsOf(game.World.Map);
        return owner >= 1 && owner <= fd.Count ? fd[owner - 1].En : null;
    }

    // ---------------------------------------------------------------- 중대 카드 (편제)

    const float CardH = 92, CardGapX = 4, ChipY = 21, ChipH = 8;
    public int DetailCompanyId;

    /// <summary>중대 카드를 보여줄 선택인가: 내 유닛만 2기 이상이고 중대 소속이 하나라도 있을 때. 아니면 null.</summary>
    List<Company>? CardList()
    {
        if (game.Observer) return null;
        var sel = game.Selection;
        if (sel.Count < 2 || sel.Any(e => e is not Unit)) return null;
        var comps = game.SelectedCompanies();
        return comps.Count > 0 ? comps : null;
    }

    List<(Rectangle R, Company C)> CardRects(List<Company> comps)
    {
        var res = new List<(Rectangle, Company)>();
        int n = comps.Count;
        float avail = Info.Width - 28;
        float cw = Math.Clamp(MathF.Floor((avail - (n - 1) * CardGapX) / n), 54, 104);
        float x = Info.X + 14, y = Info.Y + 34;
        foreach (var c in comps)
        {
            res.Add((new Rectangle(x, y, cw, CardH), c));
            x += cw + CardGapX;
        }
        return res;
    }

    Company DetailOf(List<Company> comps) => comps.FirstOrDefault(c => c.Id == DetailCompanyId) ?? comps[0];

    Rectangle[] DetailButtons() =>
        new[] { new Rectangle(Info.X + 14, Info.Y + Info.Height - 36, 70, 28), new Rectangle(Info.X + 90, Info.Y + Info.Height - 36, 70, 28), new Rectangle(Info.X + 166, Info.Y + Info.Height - 36, 150, 28) };

    public Company? CompanyCardAt(Vector2 p)
    {
        if (CardList() is not { } comps) return null;
        foreach (var (r, c) in CardRects(comps)) if (Ui.Contains(r, p)) return c;
        return null;
    }

    /// <summary>카드 위 분대 칩 사각형 (분대 번호 순, 카드 폭에 맞춰).</summary>
    static List<(Rectangle R, Squad S)> ChipRects(Rectangle card, Company c)
    {
        var res = new List<(Rectangle, Squad)>();
        int k = c.Squads.Count;
        if (k == 0) return res;
        float avail = card.Width - 10;
        float cw = MathF.Min(12, MathF.Floor((avail - (k - 1) * 2) / k));
        if (cw < 3) cw = 3;
        float total = k * cw + (k - 1) * 2;
        float x = card.X + (card.Width - total) / 2;
        foreach (var q in c.Squads) { res.Add((new Rectangle(x, card.Y + ChipY, cw, ChipH), q)); x += cw + 2; }
        return res;
    }

    /// <summary>마우스 아래 분대 칩 (카드 표시 중에만).</summary>
    public Squad? SquadChipAt(Vector2 p)
    {
        if (CardList() is not { } comps) return null;
        foreach (var (r, c) in CardRects(comps))
            if (Ui.Contains(r, p))
                foreach (var (cr, q) in ChipRects(r, c))
                    if (p.X >= cr.X - 1 && p.X <= cr.X + cr.Width + 1 && p.Y >= cr.Y - 2 && p.Y <= cr.Y + cr.Height + 2) return q;
        return null;
    }

    /// <summary>상세 줄 단추: "split" · "disband" · "form" · "tactic" · "sqform" · "sqrole" (카드 표시 중에만). 해당 중대는 DetailCompany.</summary>
    public string? DetailButtonAt(Vector2 p)
    {
        if (CardList() is null) return null;
        var b = DetailButtons();
        string[] keys = { "split", "disband", "form" };
        for (int i = 0; i < 3; i++) if (Ui.Contains(b[i], p)) return keys[i];
        var fb = FormationButtons();
        string[] fkeys = { "tactic", "sqform", "sqrole" };
        for (int i = 0; i < 3; i++) if (Ui.Contains(fb[i], p) && (i == 0 || FocusSquad != null)) return fkeys[i];
        return null;
    }

    /// <summary>[전술] [분대 대형] [역할] 단추 — 상세 줄 오른쪽 끝 (자리가 좁으면 폭을 줄임).</summary>
    Rectangle[] FormationButtons()
    {
        float y = Info.Y + Info.Height - 36, right = Info.X + Info.Width - 14, left = Info.X + 322;
        float[] want = { 150, 130, 130 };
        float k = MathF.Min(1f, MathF.Max(0.4f, (right - left - 12) / want.Sum()));
        var res = new Rectangle[3];
        float x = right;
        for (int i = 2; i >= 0; i--) { float wdt = MathF.Floor(want[i] * k); x -= wdt; res[i] = new Rectangle(x, y, wdt, 28); x -= 6; }
        return res;
    }

    /// <summary>선택이 정확히 상세 중대의 분대 하나 (살아 있는 구성원 전원) 이면 그 분대 — 분대 대형 · 역할 단추 대상.</summary>
    public Squad? FocusSquad
    {
        get
        {
            if (CardList() is not { } comps) return null;
            var c = DetailOf(comps);
            var sel = game.Selection;
            foreach (var q in c.Squads)
            {
                int alive = q.Members.Count(m => m.Alive);
                if (alive > 0 && alive == sel.Count && q.Members.All(m => !m.Alive || m.Selected)) return q;
            }
            return null;
        }
    }

    public Company? DetailCompany => CardList() is { } comps ? DetailOf(comps) : null;

    static Color StrengthColor(float t) => t > 0.6f ? Palette.HpGood : t > 0.3f ? Palette.HpMid : Palette.HpLow;

    void DrawCompanies(Vector2 mouse)
    {
        var comps = CardList()!;
        var detail = DetailOf(comps);
        // 머리글: 선택이 대대 하나면 대대 이름, 아니면 중대 수
        var regs = comps.Select(c => c.Battalion).Distinct().ToList();
        string head;
        if (regs.Count == 1 && regs[0] is { } rg && rg.Companies.All(c => comps.Contains(c)))
            head = $"{(rg.Number > 0 ? string.Format(S.BattalionName, rg.Number) : S.BattalionNoNumber)} · {BatFormName(rg.Form)} · {string.Format(S.BatCount, comps.Count)} · {string.Format(S.UnitsShort, Strength(comps))}";
        else head = $"{string.Format(S.BatCount, comps.Count)} · {string.Format(S.UnitsShort, Strength(comps))}";
        Ui.Text(head, Info.X + 14, Info.Y + 8, 22, Palette.Text);
        if (game.DragCard == null) Ui.Text(S.DragCardTip, Info.X + Info.Width - 14 - Ui.Measure(S.DragCardTip, 14).X, Info.Y + 14, 14, Palette.TextDim);

        foreach (var (r, c) in CardRects(comps))
        {
            bool hv = Ui.Contains(r, mouse), sel = c.Members.All(u => u.Selected), isDetail = c == detail;
            Raylib.DrawRectangleRec(r, Palette.Alpha(hv ? Palette.BtnHover : sel ? Palette.BtnActive : Palette.PanelDark, 235));
            Raylib.DrawRectangleLinesEx(r, isDetail ? 2 : 1, isDetail || hv ? Palette.Accent : Palette.PanelBorder);
            Ui.Text(string.Format(S.CompanyName, c.Number), r.X + 5, r.Y + 3, 16, Palette.Accent);
            if (c.Members.Count > 0)
            {
                var def = c.Members.GroupBy(u => u.Def).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Key, StringComparer.Ordinal).First().Key;
                UnitRenderer.DrawBody(new Vector2(r.X + r.Width / 2, r.Y + 39), 9, Palette.Player(c.Owner), -MathF.PI / 2, def.Key);
                if (UnitSymbols.BranchIcon(UnitSymbols.Of(def), 12) is { } bic) Raylib.DrawTexture(bic.Base[0], (int)(r.X + 3), (int)(r.Y + 33), Color.White);   // 병종 이미지 (중대 카드)
            }
            // 분대 칩: 칸 = 분대 (번호 순), 채움 = 인원 / 분대 최대, 위쪽 밝은 줄 = 부사관 분대장, 테두리 = 분대 전원 선택됨
            int sqMax = Math.Max(1, Defs.Current.CombatRules.SquadMax);
            foreach (var (cr, q) in ChipRects(r, c))
            {
                float t = Math.Clamp(q.Members.Count / (float)sqMax, 0, 1);
                bool chipHv = Ui.Contains(cr, mouse), chipSel = q.Members.Count > 0 && q.Members.All(u => u.Selected);
                Raylib.DrawRectangleRec(cr, Palette.Alpha(Palette.PanelBorder, 200));
                float fh = MathF.Max(1, MathF.Round(cr.Height * t));
                Raylib.DrawRectangleRec(new Rectangle(cr.X, cr.Y + cr.Height - fh, cr.Width, fh), StrengthColor(t));
                if (q.NcoLed) Raylib.DrawRectangleRec(new Rectangle(cr.X, cr.Y - 2, cr.Width, 2), Palette.Accent);
                Raylib.DrawRectangleRec(new Rectangle(cr.X, cr.Y + cr.Height, cr.Width, 2), RoleColor(SquadRole(q)));   // 아래 줄 = 대표 역할 색
                if (chipSel || chipHv) Raylib.DrawRectangleLinesEx(new Rectangle(cr.X - 1, cr.Y - 1, cr.Width + 2, cr.Height + 2), 1, chipHv ? Palette.Accent : Palette.Text);
            }
            int initial = Math.Max(1, c.InitialStrength);
            float st = Math.Clamp(c.Members.Count / (float)initial, 0, 1);
            string cnt = $"{c.Members.Count}/{c.InitialStrength}";
            Ui.Text(cnt, r.X + (r.Width - Ui.Measure(cnt, 14).X) / 2, r.Y + 48, 14, Palette.Text);
            float bx = r.X + 5, bw = r.Width - 10;
            BuildingRenderer.DrawProgress(new Rectangle(bx, r.Y + 66, bw, 6), st, StrengthColor(st));
            var ammoUnits = c.Members.Where(u => u.UsesAmmo).ToList();
            float ammo = ammoUnits.Count > 0 ? ammoUnits.Average(u => u.AmmoRatio) : 1f;
            BuildingRenderer.DrawProgress(new Rectangle(bx, r.Y + 75, bw, 6), ammo, AmmoColor);
            float sup = c.Members.Count > 0 ? c.Members.Average(u => u.SuppressionRatio) : 0f;
            BuildingRenderer.DrawProgress(new Rectangle(bx, r.Y + 84, bw, 5), sup, SuppressColor);
        }

        // 상세 줄: 고른 중대 + [분할] [해산] [대형]
        string reg = detail.Battalion is { } dr ? (dr.Number > 0 ? string.Format(S.BattalionName, dr.Number) : S.BattalionNoNumber) : S.NoBattalion;
        int ncoLed = detail.Squads.Count(q => q.NcoLed);
        string info = $"{string.Format(S.CompanyName, detail.Number)} ({reg})  ·  {string.Format(S.CompanyInfo, detail.Members.Count, detail.InitialStrength)}  ·  {string.Format(S.SquadCount, detail.Squads.Count)}" + (ncoLed > 0 ? $" ({string.Format(S.SquadNcoCount, ncoLed)})" : "") + $"  ·  {FormName(detail.Form)}" + (detail.Tactic != CompanyTactic.None ? $"  ·  {TacticName(detail.Tactic)}" + (detail.ActiveTactic != detail.Tactic ? $" ({S.TacGated}: {TacticName(detail.ActiveTactic)})" : "") : "") + $"  ·  {CommanderLine(detail)}";
        var fbs = FormationButtons();
        var focus = FocusSquad;
        if (SquadChipAt(mouse) is { } hq)
        {
            var ld = hq.Leader;
            info = $"{string.Format(S.CompanyName, detail.Number)} {string.Format(S.PlatoonName, hq.Platoon)} {string.Format(S.SquadName, hq.Number)}  ·  {string.Format(S.UnitsShort, hq.Members.Count)}" + (ld != null ? $"  ·  {S.SquadLeaderTag}: {RankName(ld)}" + (ld.IsNco ? $" ({S.NcoTag})" : "") : "")
                + $"  ·  {RoleName(SquadRole(hq))}  ·  {SquadFormName(hq.Form)}";
        }
        else if (Ui.Contains(fbs[0], mouse))
        {
            // 틀의 역할군 기본 태세 (1단계 — 표시만)
            var tr = Defs.Current.Tactics;
            info = detail.Tactic == CompanyTactic.Artillery ? S.TacticTipArtillery
                : detail.Tactic == CompanyTactic.None ? $"{S.TacticBtn}: {S.TacNone}"
                : string.Join("  ·  ", TacticRules.RoleOrder.Select(r => $"{RoleName(r)} {StanceName(tr.StanceOf(detail.Tactic, r))}"));
        }
        else if (focus == null && (Ui.Contains(fbs[1], mouse) || Ui.Contains(fbs[2], mouse))) info = S.SquadPickTip;
        Ui.Text(info, Info.X + 14, Info.Y + Info.Height - 62, 16, Palette.Text);
        var bs = DetailButtons();
        Ui.Button(bs[0], S.Split, Ui.Contains(bs[0], mouse), false);
        Ui.Button(bs[1], S.Disband, Ui.Contains(bs[1], mouse), false);
        Ui.Button(bs[2], $"{S.CompanyFormBtn}: {FormName(detail.Form)}", Ui.Contains(bs[2], mouse), false);
        Ui.Button(fbs[0], $"{S.TacticBtn}: {TacticName(detail.Tactic)}", Ui.Contains(fbs[0], mouse), false);
        Ui.Button(fbs[1], $"{S.SquadFormBtn}: {(focus != null ? SquadFormName(focus.Form) : "-")}", focus != null && Ui.Contains(fbs[1], mouse), false);
        Ui.Button(fbs[2], $"{S.RoleBtn}: {(focus != null ? RoleName(SquadRole(focus)) : "-")}", focus != null && Ui.Contains(fbs[2], mouse), false);
    }

    void DrawMulti(IReadOnlyList<Entity> sel)
    {
        int pages = PageCount(sel.Count);
        multiPage = Math.Clamp(multiPage, 0, pages - 1);
        Ui.Text(string.Format(S.SelectedCount, sel.Count), Info.X + 14, Info.Y + 10, 26, Palette.Text);
        if (pages > 1)
        {
            var mouse = Input.GetMousePosition();
            string label = string.Format(S.PageLabel, multiPage + 1, pages);
            var pr = PagerRect(-1, sel.Count); var nr = PagerRect(+1, sel.Count);
            foreach (var (r, dir) in new[] { (pr, -1), (nr, 1) })
            {
                bool ok = dir < 0 ? multiPage > 0 : multiPage < pages - 1;
                Ui.RegHit(r, dir < 0 ? "pager-" : "pager+", ok);
                bool hv = ok && Ui.Contains(r, mouse);
                Raylib.DrawRectangleRounded(r, 0.2f, 4, !ok ? Palette.PanelDark : hv ? Palette.BtnHover : Palette.Btn);
                Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, hv ? Palette.Accent : Palette.PanelBorder);
                var c = new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2);
                Icons.Tri(c + new Vector2(dir * 6, 0), c + new Vector2(-dir * 5, -7), c + new Vector2(-dir * 5, 7), ok ? Palette.Text : Palette.TextDim);
            }
            Ui.TextCentered(label, new Rectangle(pr.X + pr.Width, pr.Y, nr.X - pr.X - pr.Width, pr.Height), 16, Palette.Text);
        }
        float availH = Info.Y + Info.Height - (Info.Y + 48) - 6;
        float cell = MathF.Min(46, MathF.Floor(availH / 3));
        int rowsFit = Math.Max(1, (int)(availH / cell));
        int cols = Math.Max(1, (int)MathF.Ceiling(MultiPageSize / (float)rowsFit));
        cell = MathF.Min(cell, MathF.Floor((Info.Width - 28) / cols));
        float x0 = Info.X + 14, y0 = Info.Y + 48;
        int first = multiPage * MultiPageSize, last = Math.Min(sel.Count, first + MultiPageSize);
        for (int i = first; i < last; i++)
        {
            var e = sel[i];
            int k = i - first;
            var r = new Rectangle(x0 + k % cols * cell, y0 + k / cols * cell, cell - 4, cell - 4);
            Ui.Panel(r, Palette.PanelDark, Palette.PanelBorder);
            var (am, fu) = e is Unit uu ? SupplyBars(uu) : (null, null);
            if (e is Unit u) UnitRenderer.DrawBody(new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2 - 5), 11, Palette.Player(u.Owner), -MathF.PI / 2, u.Def.Key);
            float by = r.Y + r.Height - 6;
            UnitRenderer.DrawHpBar(new Rectangle(r.X + 4, by, r.Width - 8, 4), e.Hp / e.MaxHp);
            if (am is float a) { Raylib.DrawRectangle((int)r.X + 4, (int)by - 5, (int)(r.Width - 8), 3, Palette.PanelBorder); Raylib.DrawRectangle((int)r.X + 4, (int)by - 5, (int)((r.Width - 8) * Math.Clamp(a, 0, 1)), 3, AmmoColor); }
            if (fu is float f) { Raylib.DrawRectangle((int)r.X + 4, (int)by - 9, (int)(r.Width - 8), 3, Palette.PanelBorder); Raylib.DrawRectangle((int)r.X + 4, (int)by - 9, (int)((r.Width - 8) * Math.Clamp(f, 0, 1)), 3, FuelColor); }
            if (e is Unit su && su.Suppression > 0) Raylib.DrawRectangle((int)r.X + 4, (int)r.Y + 3, (int)((r.Width - 8) * su.SuppressionRatio), 3, SuppressColor);   // 제압 (위쪽 빨간 줄)
        }
    }

    static string StateLabel(Unit u) => u.InTrench && u.Target == null ? S.StateEntrenched : u.Charging ? (u.Target != null && u.AimTarget == u.Target.Id ? S.StateMelee : S.StateCharge) : u.Order switch
    {
        UnitOrder.Move => u.QuickStep ? S.StateQuickStep : S.StateMove,
        UnitOrder.AttackMove => u.Target != null ? S.StateEngaging : S.StateAttackMove,
        UnitOrder.Attack => S.StateAttack,
        UnitOrder.Hold => S.StateHold,
        UnitOrder.GroundFire => S.StateGroundFire,
        _ => u.Target != null ? S.StateEngaging : S.StateIdle,
    };

    // ---------------------------------------------------------------- 커맨드 카드

    void DrawCommandCard(Vector2 mouse)
    {
        Ui.Panel(Card, Palette.Panel, Palette.PanelBorder);
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
            Raylib.DrawRectangleRounded(SlotRect(col, row), 0.15f, 4, Palette.PanelDark);

        string? tabTip = null; string tabTitle = "";
        if (!game.Observer)
        {
            string[] names = { S.TabCommand, S.TabCombat };
            for (int i = 0; i < 2; i++)
            {
                var tr = TabRect(i);
                bool th = Ui.Contains(tr, mouse), on = (int)game.Tab == i;
                Ui.RegHit(tr, names[i]);
                Raylib.DrawRectangleRounded(tr, 0.25f, 4, on ? Palette.BtnActive : th ? Palette.BtnHover : Palette.Btn);
                Raylib.DrawRectangleRoundedLinesEx(tr, 0.25f, 4, 1, on || th ? Palette.Accent : Palette.PanelBorder);
                Ui.TextCentered(names[i], tr, 16, Palette.Text);
                if (th) tabTitle = names[i];
                if (th) tabTip = i == 0 ? S.TabTipCommand : S.TabTipCombat;
            }
        }

        CardButton? hovered = null;
        foreach (var c in game.CommandCard())
        {
            var r = SlotRect(c.Col, c.Row);
            Ui.RegHit(r, "card:" + c.Label);
            bool hover = Ui.Contains(r, mouse);
            if (hover) hovered = c;
            Color fill = c.Active ? Palette.BtnActive : hover ? Palette.BtnHover : Palette.Btn;
            Raylib.DrawRectangleRounded(r, 0.15f, 4, fill);
            Raylib.DrawRectangleRoundedLinesEx(r, 0.15f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
            const bool afford = true;
            Color ink = c.Enabled && afford ? Palette.Text : Palette.TextDim;
            var iconRect = new Rectangle(r.X + r.Width * 0.27f, r.Y + r.Height * 0.14f, r.Width * 0.46f, r.Height * 0.46f);
            if (c.Value != null) Ui.TextCentered(c.Value, new Rectangle(r.X, iconRect.Y, r.Width, iconRect.Height), 16, c.Active ? Palette.Select : Palette.Gold);
            else DrawIcon(c.Icon, iconRect, ink);
            if (c.HotkeyLabel.Length > 0) Ui.Text(c.HotkeyLabel, r.X + 4, r.Y + 2, 16, c.Enabled && afford ? Palette.Accent : Palette.TextDim);
            Vector2 m = Ui.Measure(c.Label, 16);
            Ui.Text(c.Label, r.X + (r.Width - m.X) / 2, r.Y + r.Height - m.Y - 3, 16, ink);
        }

        if (hovered == null && tabTip != null) DrawTooltipBox(tabTitle, tabTip, (string?)null, Card.X + Card.Width, Bottom.Y - 6, alignRight: true);
        if (hovered != null)
        {
            DrawTooltipBox(HoverTitle(hovered), hovered.Tooltip, hovered.CostLine, Card.X + Card.Width, Bottom.Y - 6, alignRight: true);
        }
    }

    static string HoverTitle(CardButton c) => c.HotkeyLabel.Length > 0 ? $"{c.Label}  [{c.HotkeyLabel}]" : c.Label;

    void DrawTooltipBox(string title, string body, List<(string Text, Color Color)> cost, float x, float bottomY)
    {
        float cw = cost.Sum(s => Ui.Measure(s.Text, 16).X);
        float w = MathF.Max(MathF.Max(Ui.Measure(title, 16).X, Ui.Measure(body, 16).X), cw) + 20;
        var r = new Rectangle(x - w, bottomY - 74, w, 74);
        Ui.Panel(r, Palette.Alpha(Palette.PanelDark, 240), Palette.HudTrim);
        Ui.Text(title, r.X + 10, r.Y + 7, 16, Palette.Accent);
        Ui.Text(body, r.X + 10, r.Y + 28, 16, Palette.Text);
        float cx = r.X + 10;
        foreach (var (t, col) in cost) { Ui.Text(t, cx, r.Y + 50, 16, col); cx += Ui.Measure(t, 16).X; }
    }

    void DrawIcon(string icon, Rectangle r, Color c)
    {
        var center = new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2);
        if (icon.StartsWith("unit:") && Defs.Current.Units.ContainsKey(icon[5..]))
            UnitRenderer.DrawBody(center, r.Width * 0.38f, Palette.Player(game.Me), -MathF.PI / 2, icon[5..]);
        else if (icon.StartsWith("bld:") && Defs.Current.Buildings.TryGetValue(icon[4..], out var bd))
            BuildingRenderer.DrawPortrait(bd, Palette.Player(game.Me), r, -MathF.PI / 4);
        else if (icon.StartsWith("form:") && int.TryParse(icon[5..], out int ft))
        {
            DrawFormIcon((FormType)ft, r.X + r.Width / 2 - 8, r.Y + r.Height / 2 - 4, c);
        }
        else Icons.Draw(icon, r, c);
    }

    void DrawTooltipBox(string title, string body, string? cost, float x, float bottomY, bool alignRight = false)
    {
        float w = MathF.Max(MathF.Max(Ui.Measure(title, 16).X, Ui.Measure(body, 16).X), cost != null ? Ui.Measure(cost, 16).X : 0) + 20;
        float h = cost != null ? 74 : 52;
        var r = new Rectangle(alignRight ? x - w : x, bottomY - h, w, h);
        Ui.Panel(r, Palette.Alpha(Palette.PanelDark, 240), Palette.HudTrim);
        Ui.Text(title, r.X + 10, r.Y + 7, 16, Palette.Accent);
        Ui.Text(body, r.X + 10, r.Y + 28, 16, Palette.Text);
        if (cost != null) Ui.Text(cost, r.X + 10, r.Y + 50, 16, Palette.Gold);
    }

    // ---------------------------------------------------------------- 게임 종료

    void DrawGameOver(Vector2 mouse)
    {
        Raylib.DrawRectangleRec(Viewport, Palette.Alpha(Color.Black, 150));
        int w = game.World.Winner;
        bool won = game.World.Winners.Contains(game.Me);
        string title = won ? S.Victory : w < 0 ? S.Draw : S.Defeat;
        string sub = won ? (game.World.Winners.Count > 1 ? S.SharedVictorySub : S.VictorySub) : S.DefeatSub;
        var c = new Vector2(Viewport.X + Viewport.Width / 2, Viewport.Y + Viewport.Height / 2);
        if (game.Observer)
        {
            title = S.GameOver;
            sub = game.World.Winners.Count > 0
                ? string.Format(S.WinnersLabel, string.Join(", ", game.World.Winners.Select(game.OwnerName)))
                : S.Draw;
            won = true;
        }
        var panel = new Rectangle(c.X - 200, c.Y - 90, 400, 170);
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 240), Palette.HudTrim);
        Ui.TextCentered(title, new Rectangle(panel.X, panel.Y + 14, panel.Width, 40), 26, won ? Palette.Gold : Palette.HpLow);
        Ui.TextCentered(sub, new Rectangle(panel.X + 8, panel.Y + 60, panel.Width - 16, 30), 16, Palette.Text);
        Ui.Button(restartButton, game.Observer ? S.WatchAgain : S.Restart, Ui.Contains(restartButton, mouse), false);
        Ui.Button(quitButton, game.FromMenu ? S.ToMenu : S.Quit, Ui.Contains(quitButton, mouse), false);
    }

    public void Dispose() => Minimap.Dispose();
}
