using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using KoreanDivision;
using KoreanDivision.Core;
using KoreanDivision.Render;
using MapEditor.Model;
using Raylib_cs;
using ModelUnit = MapEditor.Model.UnitDef;
using ModelSite = MapEditor.Model.SiteDef;

namespace MapEditor.App;

public enum Tool { Brush, Line, Rect, Circle, Fill, Eraser, Prop, Site, Start, Area, Zone, Unit, Rail }

/// <summary>한 프레임의 입력. 실제 입력(Raylib)과 스크립트 재생이 같은 모양을 만든다.</summary>
public sealed class InputFrame
{
    public Vector2 Mouse;
    public bool Left, Right, Middle;
    public float Wheel;
    public List<string> Keys = new();   // "b", "ctrl+z", "[" ... (소문자)
    public string Chars = "";           // 이 프레임에 친 글자 (이름 칸이 포커스일 때만 쓰임)
    public InputFrame Clone() => new() { Mouse = Mouse, Left = Left, Right = Right, Middle = Middle, Wheel = Wheel, Keys = new List<string>(Keys), Chars = Chars };
}

public sealed class Button
{
    public string Id = "", Label = "";
    public Rectangle Rect;
    public Action Click = () => { };
    public Func<bool> Active = () => false;
    public Color? Swatch;
    /// <summary>팝업 (열기 목록) 에 속한 버튼 -- 팝업이 열려 있으면 이 버튼만 눌린다.</summary>
    public bool Modal;
    /// <summary>true = 목록 줄 (왼쪽 정렬, 글자가 넘치면 잘림).</summary>
    public bool Row;
    public Color? RowColor;
}

/// <summary>패널에 그릴 글자 한 줄 (Layout 이 만들고 View 는 그리기만 한다). Kind: 0 보통 1 흐림 2 경고 3 강조 4 오류.</summary>
public readonly record struct Lbl(string Text, float X, float Y, int Kind, bool Centered = false, float W = 0);

/// <summary>
/// 지도 편집기 상태 + 입력 처리 (창 없이도 돈다: 렌더러만 창이 있을 때 만든다). 문서 연산은 전부 MapDoc / MissionDoc (모델) 를 거친다 -- 여기서 지도 의미를 새로 만들지 않는다.
/// 한 번의 되돌리기 스택에 지형 · 시작 · 시설 · 유닛 · 소품 · 구역이 모두 들어간다.
/// </summary>
public sealed class Editor
{
    public const int TopH = 40, StatusH = 26, LeftW = 132, RightW = 200, BottomH = 116;
    public const int ListRows = 5, IssueRows = 4, PopupRows = 8;
    public const string TestId = "_editor_test";
    public static readonly string[] PaletteNames = MapDoc.TerrainNames.Concat(new[] { "high", "low" }).ToArray();
    public static readonly string[] PaletteLabels = { "풀", "흙", "모래", "물", "바위", "숲", "잔해", "집", "철조망", "참호", "구덩이", "선로", "여울", "다리", "고지 올림", "고지 내림" };
    public static readonly string[] ToolLabels = { "브러시 B", "선 L", "사각 R", "원 C", "채움 F", "지우개 E", "소품 P", "시설 S", "시작 T", "구역 Z", "배치구역 X", "유닛 U", "선로 N" };
    public static readonly string[] ToolIds = { "tool_brush", "tool_line", "tool_rect", "tool_circle", "tool_fill", "tool_eraser", "tool_prop", "tool_site", "tool_start", "tool_area", "tool_zone", "tool_unit", "tool_rail" };

    // 정적 초기화 순서: 게임 정의(balance.json) 를 먼저 읽어야 아래 팔레트가 채워진다 (아래 필드들은 글자 순서대로 초기화됨)
    static readonly bool defsLoaded = LoadDefs();
    static bool LoadDefs() { Defs.Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "balance.json")); return true; }

    static readonly MethodInfo loadText = typeof(MapFile).GetMethod("LoadText", BindingFlags.NonPublic | BindingFlags.Static)!;
    static readonly MethodInfo scatterProps = typeof(GameMap).GetMethod("ScatterProps", BindingFlags.NonPublic | BindingFlags.Instance)!;

    // ---- 게임 쪽 팔레트 (props.json / balance.json 그대로)
    public static readonly string[] PropTypes = BuildPropTypes();
    public static readonly string[] SiteTypes = Defs.Current.SiteKinds.Select(k => k.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
    public static readonly string[] UnitTypes = Defs.Current.Units.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

    static string[] BuildPropTypes()
    {
        var pd = PropDefs.Current;
        var variants = new HashSet<string>(pd.Types.Values.Select(t => t.Destroyed).Where(s => s.Length > 0));   // 부서진 변종은 놓지 않는다 (게임이 만든다)
        return pd.Types.Keys.Where(k => !variants.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }

    /// <summary>테스트 실행 방식. 기본 = 게임 창을 띄움 (run_game.ps1, 보조 모니터); 스크립트 · selftest 는 기록만 하는 쪽으로 바꾼다.</summary>
    public static Func<string[], string> Launcher = RealLaunch;

    public MapDoc Doc { get; private set; }
    public MissionDoc? Mission { get; private set; }
    public string SourceLabel = "";
    public string SaveDir;
    public string? SavePath;
    public string SavedHash = "";
    public bool Dirty { get; private set; }
    public Tool Tool = Tool.Brush;
    public int Palette;          // PaletteNames 색인
    public int BrushSize = 1;
    public bool ShowGrid, ShowHigh;
    public string Status = "";
    public RtsCamera Cam;
    public int WinW = 1280, WinH = 720;
    public Rectangle MapView;
    public readonly List<Button> Buttons = new();
    public readonly List<Lbl> Labels = new();
    public bool PopupOpen;
    public int PopupTab, PopupPage;   // 0 게임 지도, 1 게임 미션, 2 편집본
    public List<(string Label, string Path)> PopupItems = new();
    public bool RenderDirty = true;       // 창 쪽이 렌더러를 다시 만들 때 확인
    public GameMap? PreviewMap;           // 마지막으로 성공한 게임 지도 (렌더러 입력)
    public int Rebuilds;
    public string LastHash = "";
    public string LoadError = "";

    // ---- 배치 도구 상태
    public int PropSel, SiteSel, UnitSel;
    public int OwnerSel = 1, UnitCount = 1, ZoneFaction = 1;
    public bool AreaCircle;
    public string AreaName = "area_1";
    public string? AreaSel;
    public int ZoneSel = -1, ListPage;
    public bool TextFocus;
    public bool ScatterPreview;
    public readonly List<PropSeed> ScatterList = new();
    public List<Issue> Issues = new();
    public int IssuePage, IssueSel = -1;
    public string LastLaunch = "";
    public int ErrorCount => Issues.Count(i => i.Error);
    public int WarnCount => Issues.Count(i => !i.Error);

    // ---- 드래그 상태
    public bool Dragging;
    public int DragButton;       // 0 = 왼쪽, 1 = 오른쪽(지우개 / 삭제)
    public (int X, int Y) DragStart, DragCur;
    public Vector2 DragWorld;
    public int DragStartIdx = -1;   // 시작 도구: 끌고 있는 시작 번호
    public readonly List<(int X, int Y)> DragCells = new();
    public (int X, int Y) Hover;
    public Vector2 HoverWorld;
    public bool HoverIn;
    bool prevL, prevR, prevM, ready;
    Vector2 prevMouse;
    public int ScriptFailures;

    public Editor(MapDoc doc, string label, string saveDir)
    {
        Doc = doc; SourceLabel = label; SaveDir = saveDir;
        Cam = new RtsCamera(doc.Width, doc.Height);
        Layout(WinW, WinH);
        Cam.SetViewport(MapView);
        FitCamera();
        Refresh(true);
    }

    // ---------------------------------------------------------------- 문서

    public void LoadDoc(MapDoc doc, string label, string? savePath, MissionDoc? mission = null)
    {
        Mission = mission;
        Doc = mission?.Map ?? doc; SourceLabel = label; SavePath = savePath; PopupOpen = false;
        CancelDrag();
        AreaSel = null; ZoneSel = -1; ListPage = 0; IssuePage = 0; IssueSel = -1; TextFocus = false;
        AreaName = NextFreeAreaName();
        Cam = new RtsCamera(Doc.Width, Doc.Height);
        Cam.SetViewport(MapView);
        FitCamera();
        Refresh(true);
        Status = LoadError.Length > 0 ? LoadError : $"열림: {label} ({Doc.Width}x{Doc.Height}){(mission != null ? " 미션" : "")}";
        Layout(WinW, WinH);
    }

    public static MapDoc NewDoc(int w, int h)
    {
        int cy = h / 2;
        return MapDoc.Parse($"{{\"name\":\"new\",\"width\":{w},\"height\":{h},\"base\":\"grass\",\"starts\":[[{Math.Min(8, w - 1)},{cy}],[{Math.Max(0, w - 9)},{cy}]]}}");
    }

    string CurrentKey() => Doc.ToJson() + (Mission != null ? "|" + Mission.StateKey() : "");

    /// <summary>문서가 바뀐 뒤: 게임 지도로 다시 굽고 (게임과 같은 로더) 변경 표시 · 검사 목록 갱신. 커밋 · 되돌리기 때만 부른다 (프레임마다 아님).</summary>
    public void Refresh(bool markSaved = false)
    {
        string json = Doc.ToJson();
        LoadError = "";
        LastHash = MapDoc.Hash(json);
        string key = MapDoc.Hash(CurrentKey());
        if (markSaved) SavedHash = key;
        Dirty = key != SavedHash;
        try
        {
            PreviewMap = (GameMap)loadText.Invoke(null, new object?[] { json + "\n", "edit", Mission?.Seed ?? Config.DefaultSeed, null })!;
            RenderDirty = true; Rebuilds++;
        }
        catch (Exception e)
        {
            LoadError = "게임 로드 실패: " + (e.InnerException?.Message ?? e.Message);
            Status = LoadError;
        }
        if (ZoneSel >= (Mission?.Zones.Count ?? 0)) ZoneSel = -1;
        if (AreaSel != null && Mission?.Areas.Any(a => a.Name == AreaSel) != true) AreaSel = null;
        RunInspect();
        RefreshScatter();
    }

    void RunInspect()
    {
        var env = new InspectEnv
        {
            Walkable = LoadError.Length == 0 && PreviewMap != null ? PreviewMap.IsTerrainWalkable : null,
            PropRadius = t => PropDefs.Current.TryGet(t, out var d) ? d.Radius : null,
            SiteSize = k => Defs.Current.SiteKind(k)?.Size ?? 0,
            UnitExists = k => Defs.Current.Units.ContainsKey(k),
        };
        var list = Inspector.Run(Doc, Mission, env);
        string[] covered = { "시작 위치가", "시설 ", "유닛 " };
        foreach (var e in Doc.Validate())
            if (!covered.Any(c => e.StartsWith(c)) && !list.Any(i => i.Message == e)) list.Add(new Issue(true, "map", e, Doc.Width / 2.0, Doc.Height / 2.0));
        if (Mission != null)
            foreach (var e in Mission.Validate(t => PropDefs.Current.TryGet(t, out _)))
                if (e.StartsWith("propDensity") || e.StartsWith("propCell")) list.Add(new Issue(true, "scatter", e, Doc.Width / 2.0, Doc.Height / 2.0));
        if (LoadError.Length > 0) list.Add(new Issue(true, "map", LoadError, Doc.Width / 2.0, Doc.Height / 2.0));
        Issues = list.OrderByDescending(i => i.Error).ToList();
        int pages = Math.Max(1, (Issues.Count + IssueRows - 1) / IssueRows);
        IssuePage = Math.Clamp(IssuePage, 0, pages - 1);
        if (IssueSel >= Issues.Count) IssueSel = -1;
    }

    /// <summary>propScatter 미리보기: 게임의 ScatterProps 를 새 지도에 그대로 호출 (같은 시드 = 같은 소품). 병력 둘레 비우기는 병력 위치(at) 만 근사.</summary>
    public void RefreshScatter()
    {
        ScatterList.Clear();
        if (!ScatterPreview || Mission == null || !Mission.PropScatter || LoadError.Length > 0) return;
        try
        {
            var gm = (GameMap)loadText.Invoke(null, new object?[] { Doc.ToFileText(), "scatter", Mission.Seed, null })!;
            gm.StartTiles.Clear();
            for (int f = 1; f <= Math.Max(2, Mission.FactionCount); f++)
            {
                var fp = Mission.ForcePoints.FirstOrDefault(p => p.Faction == f);
                gm.StartTiles.Add(fp != null ? (Math.Clamp((int)fp.X, 0, gm.Width - 1), Math.Clamp((int)fp.Y, 0, gm.Height - 1)) : (gm.Width / 2, gm.Height / 2));
            }
            var zones = Mission.Zones;
            var pts = Mission.ForcePoints;
            bool Avoid(float x, float y)
            {
                foreach (var z in zones) if (InShape(z.Rect, z.Circle, x, y)) return true;
                foreach (var p in pts) if ((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < 6.25) return true;
                return false;
            }
            scatterProps.Invoke(gm, new object?[] { false, (Func<float, float, bool>)Avoid, (float)Mission.PropDensity, Mission.PropCell });
            ScatterList.AddRange(gm.PropSeeds);
        }
        catch (Exception e) { Status = "흩뿌리기 미리보기 실패: " + (e.InnerException?.Message ?? e.Message); }
    }

    public static bool InShape(double[]? rect, double[]? circle, double x, double y)
    {
        if (rect is { Length: 4 } r) return x >= r[0] && y >= r[1] && x < r[0] + r[2] && y < r[1] + r[3];
        if (circle is { Length: 3 } c) return (x - c[0]) * (x - c[0]) + (y - c[1]) * (y - c[1]) <= c[2] * c[2];
        return false;
    }

    public void FitCamera()
    {
        float z = Math.Min(MapView.Width / (Doc.Width * Config.TileSize), MapView.Height / (Doc.Height * Config.TileSize));
        SetZoom(z);
        Cam.CenterOn(new Vector2(Doc.Width / 2f, Doc.Height / 2f));
    }

    /// <summary>편집기 전용 배율 범위: 게임 카메라의 최소 배율 (0.35) 은 큰 지도 전체를 못 보여줘서 낮춘다. 게임 코드는 건드리지 않고 Camera2D 필드를 직접 쓴다.</summary>
    public const float EditMinZoom = 0.08f;

    public void SetZoom(float z)
    {
        if (!float.IsFinite(z)) return;
        var c = Cam.CenterWorld;
        Cam.Cam.Zoom = Math.Clamp(z, EditMinZoom, Config.MaxZoom);
        Cam.CenterOn(c);
    }

    void ZoomAtMouse(float wheel, Vector2 screen)
    {
        Vector2 before = Raylib.GetScreenToWorld2D(screen, Cam.Cam);
        Cam.Cam.Zoom = Math.Clamp(Cam.Cam.Zoom * MathF.Pow(Config.ZoomStep, wheel), EditMinZoom, Config.MaxZoom);
        Vector2 after = Raylib.GetScreenToWorld2D(screen, Cam.Cam);
        Cam.Scroll((before - after) * Cam.Cam.Zoom);   // Scroll 은 화면 픽셀 단위 이동이고 끝에 지도 안으로 자름
    }

    /// <summary>팔레트 견본색. 앞 11개는 게임 정의 그대로, 그 뒤 (rail · ford · bridge) 는 아직 안 합쳐진 브랜치의 지형이라 게임 enum 에 없을 수 있어 편집기가 색을 갖는다 (값은 각 브랜치 Terrain.cs 와 같음; 병합 뒤 Terrains.Get 으로 바꿔도 됨).</summary>
    public static Color SwatchColor(string name)
    {
        switch (name)
        {
            case "rail": return new Color(104, 96, 88, 255);
            case "ford": return new Color(96, 156, 172, 255);
            case "bridge": return new Color(138, 106, 66, 255);
            case "high": return new Color(230, 220, 120, 255);
            case "low": return new Color(110, 140, 170, 255);
        }
        int i = Array.IndexOf(MapDoc.TerrainNames, name);
        return i >= 0 && i < 11 ? Terrains.Get((TerrainType)i).Color : Color.White;
    }

    /// <summary>선로 도구: 지금까지 찍은 꺾임점 (Enter / 우클릭 = 확정, Esc = 취소, Backspace = 마지막 점 취소).</summary>
    public readonly List<(int X, int Y)> RailPts = new();

    public string CurrentTerrain => PaletteNames[Palette];
    public bool IsTerrainTool => (int)Tool < 6;

    string NextFreeAreaName()
    {
        for (int i = 1; i < 1000; i++)
        {
            string n = "area_" + i;
            if (Mission?.Areas.Any(a => a.Name == n) != true) return n;
        }
        return "area_x";
    }

    // ---------------------------------------------------------------- 배치 / 버튼

    public void Layout(int w, int h)
    {
        WinW = w; WinH = h;
        MapView = new Rectangle(LeftW, TopH, Math.Max(64, w - LeftW - RightW), Math.Max(64, h - TopH - StatusH - BottomH));
        Cam.SetViewport(MapView);
        Buttons.Clear(); Labels.Clear();
        float x = 8;
        void Top(string id, string label, float bw, Action click, Func<bool>? active = null)
        {
            Buttons.Add(new Button { Id = id, Label = label, Rect = new Rectangle(x, 6, bw, 28), Click = click, Active = active ?? (() => false) });
            x += bw + 6;
        }
        Top("new", "새로", 50, () => LoadDoc(NewDoc(64, 64), "새 지도", null));
        Top("open", "열기", 50, OpenPopup);
        Top("save", "저장 ^S", 72, () => Save());
        x += 6;
        Top("undo", "되돌리기", 76, () => Do(Doc.Undo(), "되돌림"));
        Top("redo", "다시 ^Y", 66, () => Do(Doc.Redo(), "다시 적용"));
        x += 6;
        Top("grid", "격자 G", 62, () => ShowGrid = !ShowGrid, () => ShowGrid);
        Top("high", "고지 H", 62, () => ShowHigh = !ShowHigh, () => ShowHigh);
        x += 6;
        Top("zoomout", "-", 30, () => SetZoom(Cam.Zoom / Config.ZoomStep));
        Top("zoomin", "+", 30, () => SetZoom(Cam.Zoom * Config.ZoomStep));
        Top("fit", "맞춤", 50, FitCamera);
        x += 6;
        Top("testplay", "테스트 F5", 86, TestPlay);
        TitleX = x + 6;

        float y = TopH + 8;
        for (int i = 0; i < ToolIds.Length; i++)
        {
            var t = (Tool)i;
            Buttons.Add(new Button { Id = ToolIds[i], Label = ToolLabels[i], Rect = new Rectangle(8, y, LeftW - 16, 28), Click = () => SetTool(t), Active = () => Tool == t });
            y += 32;
        }
        y += 6;
        SizeLabelY = y;
        Buttons.Add(new Button { Id = "size_dec", Label = "-", Rect = new Rectangle(8, y + 22, 34, 28), Click = () => BrushSize = Math.Max(1, BrushSize - 1) });
        Buttons.Add(new Button { Id = "size_inc", Label = "+", Rect = new Rectangle(LeftW - 42, y + 22, 34, 28), Click = () => BrushSize = Math.Min(15, BrushSize + 1) });

        LayoutRight(w);
        LayoutIssues();

        if (PopupOpen)
        {
            float pw = 360, pxx = (w - pw) / 2, pyy = 70;
            string[] tabIds = { "tab_maps", "tab_missions", "tab_edits" }, tabLabels = { "게임 지도", "게임 미션", "편집본" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                Buttons.Add(new Button { Id = tabIds[i], Label = tabLabels[i], Rect = new Rectangle(pxx + 10 + i * 114, pyy + 34, 108, 28), Click = () => SetPopupTab(k), Active = () => PopupTab == k, Modal = true });
            }
            int first = PopupPage * PopupRows;
            for (int i = 0; i < PopupRows; i++)
            {
                int k = i;
                string lab = first + i < PopupItems.Count ? PopupItems[first + i].Label : "";
                Buttons.Add(new Button { Id = "item_" + i, Label = lab, Rect = new Rectangle(pxx + 10, pyy + 68 + i * 30, pw - 20, 26), Click = () => OpenItem(first + k), Modal = true, Row = true });
            }
            float by = pyy + 68 + PopupRows * 30 + 4;
            Buttons.Add(new Button { Id = "pg_prev", Label = "이전", Rect = new Rectangle(pxx + 10, by, 80, 28), Click = () => PopupPage = Math.Max(0, PopupPage - 1), Modal = true });
            Buttons.Add(new Button { Id = "pg_next", Label = "다음", Rect = new Rectangle(pxx + pw - 90, by, 80, 28), Click = () => { if ((PopupPage + 1) * PopupRows < PopupItems.Count) PopupPage++; }, Modal = true });
            Buttons.Add(new Button { Id = "popup_close", Label = "취소 (Esc)", Rect = new Rectangle(pxx + 10, by + 34, pw - 20, 28), Click = () => PopupOpen = false, Modal = true });
        }
    }

    public float TitleX, SizeLabelY;
    public Rectangle PopupRect => new((WinW - 360) / 2f, 70, 360, 68 + PopupRows * 30 + 4 + 34 + 28 + 14);
    public Rectangle IssueRect => new(MapView.X, MapView.Y + MapView.Height, MapView.Width, BottomH);

    void LayoutRight(int w)
    {
        float px = w - RightW + 8, bw = RightW - 16, y = TopH + 10;
        void Head(string t) { Labels.Add(new Lbl(t, px, y, 3)); y += 22; }
        void Line(string t, int kind = 1) { Labels.Add(new Lbl(t, px, y, kind)); y += 18; }
        void Btn(string id, string label, Action click, Func<bool>? active = null, float width = 0, float dx = 0)
        {
            Buttons.Add(new Button { Id = id, Label = label, Rect = new Rectangle(px + dx, y, width > 0 ? width : bw, 28), Click = click, Active = active ?? (() => false) });
        }
        void Chooser(string idPrev, string idNext, string text, Action prev, Action next)
        {
            Buttons.Add(new Button { Id = idPrev, Label = "<", Rect = new Rectangle(px, y, 34, 28), Click = prev });
            Buttons.Add(new Button { Id = idNext, Label = ">", Rect = new Rectangle(px + bw - 34, y, 34, 28), Click = next });
            Labels.Add(new Lbl(Trunc(text, 14), px + 34, y + 6, 0, true, bw - 68));
            y += 34;
        }
        void Pair(string idDec, string idInc, string text, Action dec, Action inc)
        {
            Chooser(idDec, idInc, text, dec, inc);
        }
        void NeedMission()
        {
            if (Mission == null) { Line("미션 JSON 을 열어야", 2); Line("열기 > 게임 미션", 2); }
        }
        switch (Tool)
        {
            case Tool.Prop:
                Head("소품 종류");
                Chooser("type_prev", "type_next", PropTypes[Math.Clamp(PropSel, 0, PropTypes.Length - 1)], () => PropSel = Wrap(PropSel - 1, PropTypes.Length), () => PropSel = Wrap(PropSel + 1, PropTypes.Length));
                if (PropDefs.Current.TryGet(PropTypes[Math.Clamp(PropSel, 0, PropTypes.Length - 1)], out var pdef))
                {
                    Line($"엄폐 {pdef.Cover:0.00} 시선 {pdef.LosBlock:0.00}");
                    Line($"반지름 {pdef.Radius:0.00}  {(pdef.BlocksMove ? "길 막음" : "통과 가능")}");
                    Line(pdef.Hp > 0 ? $"체력 {pdef.Hp:0}" : "안 부서짐");
                }
                y += 4;
                Head("흩뿌리기");
                bool on = Mission?.PropScatter == true;
                Btn("scatter_toggle", on ? "흩뿌리기 켬" : "흩뿌리기 끔", ScatterToggle, () => on);
                y += 34;
                Pair("dens_dec", "dens_inc", $"밀도 {(Mission?.PropDensity ?? 1):0.0}", () => ScatterNum(-0.5, 0), () => ScatterNum(0.5, 0));
                Pair("cell_dec", "cell_inc", Mission == null || Mission.PropCell == 0 ? "칸 기본" : $"칸 {Mission.PropCell}", () => ScatterNum(0, -1), () => ScatterNum(0, 1));
                Btn("scatter_view", ScatterPreview ? "미리보기 켬" : "미리보기 끔", ToggleScatterView, () => ScatterPreview);
                y += 34;
                Line(ScatterPreview ? $"미리보기 {ScatterList.Count}개 (근사)" : $"손으로 놓은 소품 {Mission?.Props.Count ?? 0}개");
                NeedMission();
                break;
            case Tool.Site:
                Head("시설 종류");
                Chooser("type_prev", "type_next", SiteTypes.Length == 0 ? "-" : SiteTypes[Math.Clamp(SiteSel, 0, SiteTypes.Length - 1)], () => SiteSel = Wrap(SiteSel - 1, SiteTypes.Length), () => SiteSel = Wrap(SiteSel + 1, SiteTypes.Length));
                if (SiteTypes.Length > 0) Line($"한 변 {Defs.Current.SiteKind(SiteTypes[Math.Clamp(SiteSel, 0, SiteTypes.Length - 1)])?.Size}칸 (점령 시설)");
                y += 4;
                Head("소유 세력");
                Pair("own_dec", "own_inc", OwnerSel == 0 ? "0 (중립)" : $"{OwnerSel}", () => OwnerSel = Math.Max(0, OwnerSel - 1), () => OwnerSel = Math.Min(Doc.Starts.Count, OwnerSel + 1));
                y += 4;
                Line("클릭 = 놓기 (칸 가운데)"); Line("우클릭 = 지우기");
                Line("장식 건물 형식은 없음:"); Line("집 지형 칸으로 칠함");
                break;
            case Tool.Start:
                Head("시작 위치");
                Line($"{Doc.Starts.Count}개 (2~{MapDoc.MaxStarts})", 0);
                y += 4;
                Line("클릭 = 추가"); Line("끌기 = 옮기기"); Line("우클릭 = 지우기");
                break;
            case Tool.Area:
                Head("구역 (areas)");
                Btn("shape_toggle", AreaCircle ? "모양: 원" : "모양: 사각", () => AreaCircle = !AreaCircle);
                y += 34;
                Line("이름 (a-z 0-9 _):", 1);
                Btn("name_field", AreaName + (TextFocus ? "_" : ""), () => TextFocus = true, () => TextFocus);
                y += 34;
                ListRowsFor(px, bw, ref y, Mission?.Areas.Select(a => (a.Name, a.Name == AreaSel)).ToList(), "area_item_", k => SelectArea(ListPage * ListRows + k));
                ListPager(px, bw, ref y, Mission?.Areas.Count ?? 0);
                Btn("sel_del", "선택 구역 삭제", DeleteSelected);
                y += 34;
                Line("끌기 = 새 구역 / 같은 이름은 교체");
                NeedMission();
                break;
            case Tool.Zone:
                Head("배치 구역");
                Btn("shape_toggle", AreaCircle ? "모양: 원" : "모양: 사각", () => AreaCircle = !AreaCircle);
                y += 34;
                Pair("own_dec", "own_inc", $"세력 {ZoneFaction}", () => ZoneFaction = Math.Max(1, ZoneFaction - 1), () => ZoneFaction = Math.Min(Math.Max(2, Mission?.FactionCount ?? 2), ZoneFaction + 1));
                ListRowsFor(px, bw, ref y, Mission?.Zones.Select((z, i) => ($"{i + 1}  세력 {z.Faction}  {ShapeText(z.Rect, z.Circle)}", i == ZoneSel)).ToList(), "zone_item_", k => SelectZone(ListPage * ListRows + k));
                ListPager(px, bw, ref y, Mission?.Zones.Count ?? 0);
                Btn("sel_del", "선택 구역 삭제", DeleteSelected);
                y += 34;
                Line("끌기 = 새 구역"); Line("선택하고 끌면 교체");
                NeedMission();
                break;
            case Tool.Rail:
                Head("선로 (꺾은선)");
                Line("클릭 = 꺾임점 추가"); Line("Enter / 우클릭 = 확정"); Line("Backspace = 점 취소"); Line("Esc = 전부 취소");
                y += 4;
                Line($"찍은 점 {RailPts.Count}개"); Line("저장 = paint line"); Line("지형 = rail (고정)");
                break;
            case Tool.Unit:
                Head("유닛 종류 (지도 units)");
                Chooser("type_prev", "type_next", UnitTypes[Math.Clamp(UnitSel, 0, UnitTypes.Length - 1)], () => UnitSel = Wrap(UnitSel - 1, UnitTypes.Length), () => UnitSel = Wrap(UnitSel + 1, UnitTypes.Length));
                Head("수");
                Pair("count_dec", "count_inc", $"{UnitCount}", () => UnitCount = Math.Max(1, UnitCount - 1), () => UnitCount = Math.Min(200, UnitCount + 1));
                Head("세력");
                Pair("own_dec", "own_inc", $"{Math.Clamp(OwnerSel, 1, Math.Max(1, Doc.Starts.Count))}", () => OwnerSel = Math.Max(1, OwnerSel - 1), () => OwnerSel = Math.Min(Math.Max(1, Doc.Starts.Count), OwnerSel + 1));
                y += 4;
                Line("클릭 = 놓기"); Line("우클릭 = 지우기");
                Line("편성은 범위 밖");
                break;
            default:
                float py = y;
                for (int i = 0; i < PaletteNames.Length; i++)
                {
                    int k = i;
                    Color sw = SwatchColor(PaletteNames[i]);
                    Buttons.Add(new Button { Id = "pal_" + i, Label = PaletteLabels[i], Rect = new Rectangle(px, py, bw, 26), Click = () => Palette = k, Active = () => Palette == k, Swatch = sw });
                    py += 28;
                }
                y = py + 6;
                Line($"{Doc.Width}x{Doc.Height}  기본 {Doc.Base}"); Line($"연산 {Doc.Paint.Count}  되돌리기 {Doc.UndoDepth}"); Line($"해시 {LastHash}");
                Line($"시작 {Doc.Starts.Count}  {(Dirty ? "변경됨" : "저장됨")}", Dirty ? 2 : 1);
                break;
        }
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 2)] + "..";
    static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;

    static string ShapeText(double[]? rect, double[]? circle) =>
        rect is { Length: 4 } r ? $"[{r[0]:0},{r[1]:0},{r[2]:0},{r[3]:0}]" : circle is { Length: 3 } c ? $"({c[0]:0},{c[1]:0}) r{c[2]:0}" : "?";

    void ListRowsFor(float px, float bw, ref float y, List<(string Text, bool Sel)>? items, string idPrefix, Action<int> click)
    {
        for (int i = 0; i < ListRows; i++)
        {
            int k = i;
            int idx = ListPage * ListRows + i;
            bool has = items != null && idx < items.Count;
            Buttons.Add(new Button { Id = idPrefix + i, Label = has ? items![idx].Text : "", Rect = new Rectangle(px, y, bw, 26), Click = () => click(k), Active = () => has && items![idx].Sel, Row = true });
            y += 28;
        }
    }

    void ListPager(float px, float bw, ref float y, int count)
    {
        Buttons.Add(new Button { Id = "list_prev", Label = "이전", Rect = new Rectangle(px, y, 80, 28), Click = () => ListPage = Math.Max(0, ListPage - 1) });
        Buttons.Add(new Button { Id = "list_next", Label = "다음", Rect = new Rectangle(px + bw - 80, y, 80, 28), Click = () => { if ((ListPage + 1) * ListRows < count) ListPage++; } });
        int pages = Math.Max(1, (count + ListRows - 1) / ListRows);
        Labels.Add(new Lbl($"{Math.Min(ListPage, pages - 1) + 1}/{pages}", px + 80, y + 6, 1, true, bw - 160));
        y += 34;
    }

    void LayoutIssues()
    {
        var r = IssueRect;
        float rows = r.Y + 20;
        Labels.Add(new Lbl($"검사  오류 {ErrorCount}  경고 {WarnCount}", r.X + 8, r.Y + 1, ErrorCount > 0 ? 4 : WarnCount > 0 ? 2 : 3));
        for (int i = 0; i < IssueRows; i++)
        {
            int k = i, idx = IssuePage * IssueRows + i;
            bool has = idx < Issues.Count;
            Buttons.Add(new Button
            {
                Id = "issue_" + i, Label = has ? (Issues[idx].Error ? "E " : "W ") + Issues[idx].Message : "", Rect = new Rectangle(r.X + 4, rows + i * 24, r.Width - 56, 24),
                Click = () => PickIssue(IssuePage * IssueRows + k), Active = () => has && idx == IssueSel, Row = true, RowColor = has ? (Issues[idx].Error ? new Color(255, 120, 110, 255) : Palette_Warn) : null,
            });
        }
        Buttons.Add(new Button { Id = "insp_up", Label = "^", Rect = new Rectangle(r.X + r.Width - 46, rows, 40, 24), Click = () => IssuePage = Math.Max(0, IssuePage - 1) });
        Buttons.Add(new Button { Id = "insp_down", Label = "v", Rect = new Rectangle(r.X + r.Width - 46, rows + 2 * 24, 40, 24), Click = () => { if ((IssuePage + 1) * IssueRows < Issues.Count) IssuePage++; } });
    }

    static readonly Color Palette_Warn = new(255, 190, 90, 255);

    public Button? ButtonAt(Vector2 p)
    {
        foreach (var b in Buttons) if (b.Modal == PopupOpen && Contains(b.Rect, p)) return b;
        return null;
    }

    public Button? ButtonById(string id) => Buttons.FirstOrDefault(b => b.Id == id);

    static bool Contains(Rectangle r, Vector2 p) => p.X >= r.X && p.X < r.X + r.Width && p.Y >= r.Y && p.Y < r.Y + r.Height;

    void SetTool(Tool t)
    {
        if (Tool != t) { ListPage = 0; TextFocus = false; CancelDrag(); }
        Tool = t;
    }

    void Do(bool ok, string msg)
    {
        if (ok) { Refresh(); Status = msg + $" (남은 되돌리기 {Doc.UndoDepth})"; }
        else Status = "더 할 것이 없음";
    }

    void PickIssue(int idx)
    {
        if (idx < 0 || idx >= Issues.Count) return;
        IssueSel = idx;
        var i = Issues[idx];
        Cam.CenterOn(new Vector2((float)i.X + 0.5f, (float)i.Y + 0.5f));
        Status = (i.Error ? "오류: " : "경고: ") + i.Message;
    }

    // ---------------------------------------------------------------- 열기 / 저장

    void OpenPopup()
    {
        PopupOpen = true; PopupPage = 0;
        SetPopupTab(PopupTab);
    }

    void SetPopupTab(int tab)
    {
        PopupTab = tab; PopupPage = 0;
        PopupItems.Clear();
        if (tab == 0)
        {
            foreach (var id in MapFile.Available()) if (!id.StartsWith("_editor_")) PopupItems.Add((id, MapFile.PathOf(id)));
        }
        else if (tab == 1)
        {
            foreach (var id in MissionDef.Available()) if (!id.StartsWith("_editor_")) PopupItems.Add((id, MissionDef.PathOf(id)));
        }
        else
        {
            if (Directory.Exists(SaveDir))
                foreach (var f in Directory.GetFiles(SaveDir, "*.json").OrderBy(f => f, StringComparer.Ordinal)) PopupItems.Add((Path.GetFileNameWithoutExtension(f) + "  (지도)", f));
            string md = Path.Combine(SaveDir, "missions");
            if (Directory.Exists(md))
                foreach (var f in Directory.GetFiles(md, "*.json").OrderBy(f => f, StringComparer.Ordinal)) PopupItems.Add((Path.GetFileNameWithoutExtension(f) + "  (미션)", f));
        }
    }

    void OpenItem(int i)
    {
        if (i < 0 || i >= PopupItems.Count) return;
        OpenPath(PopupItems[i].Path);
    }

    static string? ReadMap(string id)
    {
        string p = MapFile.PathOf(id);
        return File.Exists(p) ? File.ReadAllText(p) : null;
    }

    static bool InFolder(string path, string folder) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(folder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>지도 id · 미션 id · 파일 경로를 연다 (파일 내용을 보고 지도 / 미션을 가른다). 게임 에셋은 덮어쓰지 않도록 저장 위치를 SaveDir 로 돌린다.</summary>
    public bool OpenPath(string idOrPath)
    {
        string path;
        if (File.Exists(idOrPath)) path = Path.GetFullPath(idOrPath);
        else if (File.Exists(MapFile.PathOf(idOrPath))) path = MapFile.PathOf(idOrPath);
        else if (File.Exists(MissionDef.PathOf(idOrPath))) path = MissionDef.PathOf(idOrPath);
        else { Status = "파일 없음: " + idOrPath; return false; }
        try
        {
            string text = File.ReadAllText(path);
            string name = Path.GetFileNameWithoutExtension(path);
            if (MissionDoc.LooksLikeMission(text))
            {
                var m = MissionDoc.Parse(text, ReadMap, name);
                bool isAsset = InFolder(path, MissionDef.Folder);
                LoadDoc(m.Map, name, isAsset ? Path.Combine(SaveDir, "missions", name + ".json") : path, m);
                if (m.NeedsEmbed) Status += $"  (지도 '{m.MapRef}' 참조 -> 저장하면 내장으로 복사)";
                if (m.Map.DroppedComments > 0) Status += $"  (지도 주석 {m.Map.DroppedComments}줄은 저장 때 버려짐)";
            }
            else
            {
                var doc = MapDoc.Parse(text);
                bool isAsset = InFolder(path, MapFile.Folder);
                LoadDoc(doc, name, isAsset ? Path.Combine(SaveDir, name + ".json") : path);
                if (doc.DroppedComments > 0) Status += $"  (주석 {doc.DroppedComments}줄은 저장 때 버려짐)";
            }
            Layout(WinW, WinH);
            return true;
        }
        catch (Exception e) { Status = "열기 실패: " + e.Message; return false; }
    }

    /// <summary>파일 이름으로 써도 안전한 이름 (경로 구분자 · 금지 문자 제거; 미션 id 에 ".." 가 있어도 저장 폴더 밖으로 못 나감).</summary>
    static string SafeName(string n, string fallback)
    {
        var bad = Path.GetInvalidFileNameChars();
        string t = new string(n.Where(c => !bad.Contains(c)).ToArray()).Trim('.', ' ');
        return t.Length > 0 ? t : fallback;
    }

    string DefaultSavePath() =>
        Mission != null ? Path.Combine(SaveDir, "missions", SafeName(Mission.Id, "mission") + ".json")
                        : Path.Combine(SaveDir, SafeName(Doc.Name, "map") + ".json");

    public bool Save() => SaveTo(SavePath ?? DefaultSavePath());

    /// <summary>저장 텍스트를 먼저 게임 로더로 검사하고 (실패하면 파일을 건드리지 않음), 임시 파일에 쓴 뒤 교체한다.</summary>
    public bool SaveTo(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            string text = Mission != null ? Mission.ToText() : Doc.ToFileText();
            if (Mission != null) MissionDef.Parse(text, Mission.Id);                        // 게임 미션 검증
            else loadText.Invoke(null, new object?[] { text, "check", Config.DefaultSeed, null });   // 게임 지도 검증
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            string tmp = full + ".tmp";
            File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(false));
            File.Move(tmp, full, true);
            int lost = Mission?.CommentsAtRisk() ?? 0;   // 바뀐 속성 (소품 · 구역 · 배치 구역) 값 안의 // 주석은 다시 쓰며 사라짐
            Mission?.MarkSaved(text);
            SavePath = full;
            SavedHash = MapDoc.Hash(CurrentKey()); Dirty = false;
            Status = "저장됨: " + full + (lost > 0 ? $"  (바뀐 속성 안 주석 {lost}줄은 사라짐)" : "");
            return true;
        }
        catch (Exception e) { Status = "저장 실패: " + (e.InnerException?.Message ?? e.Message); return false; }
    }

    // ---------------------------------------------------------------- 테스트 실행

    /// <summary>
    /// 저장 -> 게임이 읽는 폴더에 임시 사본(_editor_test) -> 게임 실행 (--map ID / --mission ID --mission-start). 게임 플래그 추가 없음:
    /// 편집기 폴더의 assets 가 게임과 같은 에셋 폴더이므로 임시 id 로 놓으면 기존 --map / --mission 이 그대로 읽는다.
    /// </summary>
    public void TestPlay()
    {
        TextFocus = false;
        if (ErrorCount > 0) { Status = $"오류 {ErrorCount}개가 있어 테스트 실행 안 함 (아래 검사 목록 확인)"; return; }
        if (!Save()) return;
        try
        {
            string[] args;
            if (Mission != null)
            {
                Directory.CreateDirectory(MissionDef.Folder);
                File.Copy(SavePath!, MissionDef.PathOf(TestId), true);
                MissionDef.Load(TestId);   // 게임이 같은 위치에서 읽히는지 확인
                args = new[] { "--mission", TestId, "--mission-start" };
            }
            else
            {
                Directory.CreateDirectory(MapFile.Folder);
                File.Copy(SavePath!, MapFile.PathOf(TestId), true);
                MapFile.Load(TestId, Config.DefaultSeed);
                args = new[] { "--map", TestId };
            }
            LastLaunch = string.Join(" ", args);
            Status = Launcher(args);
        }
        catch (Exception e) { Status = "테스트 실행 실패: " + (e.InnerException?.Message ?? e.Message); }
    }

    /// <summary>스크립트 · selftest 용: 게임을 띄우지 않고 인자만 기록.</summary>
    public static string RecordLaunch(string[] args) => "실행 기록만 (창 안 띄움): " + string.Join(" ", args);

    static string RealLaunch(string[] args)
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "KoreanDivision.exe");
        if (!File.Exists(exe)) return "게임 실행 파일 없음: " + exe;
        string? ps1 = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null && ps1 == null; d = d.Parent)
        {
            foreach (var rel in new[] { Path.Combine("korean-division", "tools", "agent", "run_game.ps1"), Path.Combine("tools", "agent", "run_game.ps1") })
            {
                string c = Path.Combine(d.FullName, rel);
                if (File.Exists(c)) { ps1 = c; break; }
            }
        }
        // 셸 실행 (UseShellExecute) 으로 띄운다: 파이프 · 콘솔 핸들을 물려받지 않아 편집기를 닫거나 파이프로 읽어도 게임과 얽히지 않는다.
        static string Q(string v) => v.Contains(' ') || v.Contains('"') ? "\"" + v.Replace("\"", "\\\"") + "\"" : v;
        var psi = new ProcessStartInfo { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = AppContext.BaseDirectory };
        if (ps1 != null)
        {
            psi.FileName = "powershell.exe";
            psi.Arguments = string.Join(" ", new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Q(ps1), Q(exe) }.Concat(args.Select(Q)));
        }
        else { psi.FileName = exe; psi.WindowStyle = ProcessWindowStyle.Normal; psi.Arguments = string.Join(" ", args.Select(Q)); }   // run_game.ps1 이 없으면 직접 실행
        Process.Start(psi);
        return "게임 실행: " + string.Join(" ", args) + (ps1 != null ? " (보조 모니터)" : "");
    }

    // ---------------------------------------------------------------- 입력

    public (int X, int Y) TileAt(Vector2 screen)
    {
        var w = Cam.ScreenToWorld(screen);
        return ((int)MathF.Floor(w.X), (int)MathF.Floor(w.Y));
    }

    (int X, int Y) Clamp((int X, int Y) t) => (Math.Clamp(t.X, 0, Doc.Width - 1), Math.Clamp(t.Y, 0, Doc.Height - 1));

    void CancelDrag() { RailPts.Clear(); Dragging = false; DragCells.Clear(); DragStartIdx = -1; }

    public void Update(InputFrame f)
    {
        Layout(WinW, WinH);   // 크기는 호스트가 SetSize 로 바꾼다; 팝업 열림 상태 반영
        bool lPress = f.Left && !prevL, lRel = !f.Left && prevL;
        bool rPress = f.Right && !prevR, rRel = !f.Right && prevR;
        Vector2 delta = ready ? f.Mouse - prevMouse : Vector2.Zero;
        ready = true; prevMouse = f.Mouse;
        bool inMap = Contains(MapView, f.Mouse);
        var tile = TileAt(f.Mouse);
        var world = Cam.ScreenToWorld(f.Mouse);
        Hover = tile; HoverWorld = world; HoverIn = inMap && tile.X >= 0 && tile.Y >= 0 && tile.X < Doc.Width && tile.Y < Doc.Height;

        if (TextFocus) { foreach (var ch in f.Chars) TypeChar(ch); foreach (var k in f.Keys) TextKey(k); }
        else foreach (var k in f.Keys) Key(k);

        if (lPress || rPress)
        {
            var b = ButtonAt(f.Mouse);
            if (b != null && lPress) { if (b.Id != "name_field") TextFocus = false; Status = b.Label.Trim() + " 눌림"; b.Click(); }   // 눌림 문구는 기본값, 동작이 자기 상태 문구를 정하면 그것이 남는다
            else if (!PopupOpen && inMap && !Dragging) { TextFocus = false; BeginDrag(rPress ? 1 : 0, Clamp(tile), world); }
        }
        if (Dragging)
        {
            var cur = Clamp(tile);
            DragWorld = world;
            if (cur != DragCur)
            {
                if (IsTerrainTool && (Tool == Tool.Brush || Tool == Tool.Eraser || DragButton == 1)) AddBrushCells(DragCur, cur);
                DragCur = cur;
            }
            if ((DragButton == 0 && lRel) || (DragButton == 1 && rRel)) EndDrag();
        }
        if (f.Middle && prevM && inMap) Cam.Scroll(-delta);
        if (f.Wheel != 0 && inMap && !PopupOpen) ZoomAtMouse(f.Wheel, f.Mouse);
        prevL = f.Left; prevR = f.Right; prevM = f.Middle;
        Layout(WinW, WinH);   // 이번 프레임 변경이 글자 · 목록에 바로 반영되게
    }

    void TypeChar(char ch)
    {
        char c = char.ToLowerInvariant(ch);
        if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
        {
            if (AreaName.Length < 24) AreaName += c;
        }
    }

    void TextKey(string k)
    {
        switch (k)
        {
            case "backspace": if (AreaName.Length > 0) AreaName = AreaName[..^1]; break;
            case "enter": case "esc": TextFocus = false; break;
        }
    }

    void Key(string k)
    {
        switch (k)
        {
            case "b": SetTool(Tool.Brush); break;
            case "l": SetTool(Tool.Line); break;
            case "r": SetTool(Tool.Rect); break;
            case "c": SetTool(Tool.Circle); break;
            case "f": SetTool(Tool.Fill); break;
            case "e": SetTool(Tool.Eraser); break;
            case "p": SetTool(Tool.Prop); break;
            case "s": SetTool(Tool.Site); break;
            case "t": SetTool(Tool.Start); break;
            case "z": SetTool(Tool.Area); break;
            case "x": SetTool(Tool.Zone); break;
            case "u": SetTool(Tool.Unit); break;
            case "n": SetTool(Tool.Rail); break;
            case "enter": if (Tool == Tool.Rail) CommitRail(); break;
            case "backspace": if (Tool == Tool.Rail && RailPts.Count > 0) { RailPts.RemoveAt(RailPts.Count - 1); Status = $"선로 점 {RailPts.Count}개"; } break;
            case "g": ShowGrid = !ShowGrid; break;
            case "h": ShowHigh = !ShowHigh; break;
            case "f5": TestPlay(); break;
            case "[": BrushSize = Math.Max(1, BrushSize - 1); break;
            case "]": BrushSize = Math.Min(15, BrushSize + 1); break;
            case "ctrl+z": Do(Doc.Undo(), "되돌림"); break;
            case "ctrl+y": Do(Doc.Redo(), "다시 적용"); break;
            case "ctrl+s": Save(); break;
            case "esc": if (PopupOpen) { PopupOpen = false; } else CancelDrag(); break;
        }
    }

    void BeginDrag(int button, (int X, int Y) t, Vector2 world)
    {
        if (Tool == Tool.Rail)   // 꺾은선 도구는 끌지 않고 점을 하나씩 찍는다
        {
            Dragging = false;
            if (button == 1) CommitRail();
            else if (HoverIn)
            {
                if (RailPts.Count == 0 || RailPts[^1] != t) RailPts.Add(t);
                Status = $"선로 점 {RailPts.Count}개 -- 계속 클릭, Enter / 우클릭 = 확정, Esc = 취소";
            }
            return;
        }
        Dragging = true; DragButton = button; DragStart = DragCur = t; DragWorld = world; DragCells.Clear(); DragStartIdx = -1;
        if (!IsTerrainTool)
        {
            if (!HoverIn && Tool != Tool.Area && Tool != Tool.Zone) { Dragging = false; return; }   // 지도 밖 빈 곳 누름은 무시
            if (Tool == Tool.Start && button == 0) DragStartIdx = NearestStart(world, 1.2f);
            return;
        }
        if (Tool == Tool.Fill && button == 0) { Dragging = false; Commit(t, t, 0); return; }
        if (Tool == Tool.Brush || Tool == Tool.Eraser || button == 1) AddBrushCells(t, t);
    }

    void AddBrushCells((int X, int Y) a, (int X, int Y) b)
    {
        foreach (var c in MapDoc.LineCells(a.X, a.Y, b.X, b.Y, BrushSize)) DragCells.Add(c);
    }

    void EndDrag()
    {
        Dragging = false;
        try { Commit(DragStart, DragCur, DragButton); }
        finally { DragCells.Clear(); DragStartIdx = -1; }
    }

    /// <summary>획을 문서 연산으로 확정 (undo 한 번 = 획 하나). 드래그 중에는 문서 · 래스터를 건드리지 않고 마우스를 뗄 때만 부른다. 모델 연산 하나가 곧 undo 한 단계다.</summary>
    void Commit((int X, int Y) a, (int X, int Y) b, int button)
    {
        int before = Doc.UndoDepth;
        try
        {
            string msg = IsTerrainTool ? CommitTerrain(a, b, button) : CommitPlace(a, b, button);
            if (Doc.UndoDepth != before) { Refresh(); Status = msg + $" (되돌리기 {Doc.UndoDepth})"; }
            else Status = msg.Length > 0 ? msg : "변화 없음";
        }
        catch (Exception e)
        {
            Status = "배치 실패: " + e.Message;
            Refresh();
        }
        finally
        {
            if (Doc.InStroke) { try { Doc.EndStroke(); } catch (InvalidOperationException) { } }   // 어떤 경로로도 획이 열린 채 남지 않게
        }
    }

    string CommitTerrain((int X, int Y) a, (int X, int Y) b, int button)
    {
        string t = CurrentTerrain;
        bool erase = Tool == Tool.Eraser || button == 1;
        if (erase)
            Doc.PaintTiles(Doc.Base, DragCells.Count > 0 ? DragCells : new List<(int, int)> { a }, false);
        else switch (Tool)
        {
            case Tool.Brush: Doc.PaintTiles(t, DragCells); break;
            case Tool.Line: Doc.PaintLine(t, a.X, a.Y, b.X, b.Y, BrushSize); break;
            case Tool.Rect: Doc.PaintRect(t, Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1); break;
            case Tool.Circle:
                int r = (int)MathF.Round(MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)));
                Doc.PaintCircle(t, a.X, a.Y, r); break;
            case Tool.Fill: Doc.Fill(t, a.X, a.Y); break;
        }
        return $"{ToolLabels[(int)Tool].Split(' ')[0]} {(erase ? Doc.Base : t)} 칠함";
    }

    /// <summary>찍어 둔 꺾임점을 한 번의 되돌리기로 확정 (게임 paint "line" 한 줄, 지형 = rail).</summary>
    void CommitRail()
    {
        if (RailPts.Count < 2) { Status = RailPts.Count == 0 ? "선로: 점을 2개 이상 찍으세요" : "선로: 점이 하나뿐 -- 하나 더 찍으세요"; return; }
        var pts = RailPts.ToList();
        RailPts.Clear();
        int before = Doc.UndoDepth;
        try { Doc.PaintPolyline("rail", pts); }
        catch (Exception e) { Status = "선로 실패: " + e.Message; return; }
        Refresh();
        Status = Doc.UndoDepth != before ? $"선로 {pts.Count}점 칠함 (되돌리기 {Doc.UndoDepth})" : "변화 없음";
    }

    // ---------------------------------------------------------------- 배치 확정

    bool NeedMissionFor(string what)
    {
        if (Mission != null) return false;
        Status = $"{what} 은 미션 JSON 에만 저장됨 -- 열기 > 게임 미션 에서 미션을 여세요";
        return true;
    }

    int NearestStart(Vector2 w, float maxDist)
    {
        int best = -1; float bd = maxDist * maxDist;
        for (int i = 0; i < Doc.Starts.Count; i++)
        {
            float dx = w.X - (Doc.Starts[i][0] + 0.5f), dy = w.Y - (Doc.Starts[i][1] + 0.5f), d = dx * dx + dy * dy;
            if (d <= bd) { bd = d; best = i; }
        }
        return best;
    }

    string CommitPlace((int X, int Y) a, (int X, int Y) b, int button)
    {
        bool del = button == 1;
        var w = DragWorld;
        switch (Tool)
        {
            case Tool.Prop:
                if (NeedMissionFor("소품")) return "";
                if (del)
                {
                    int bi = -1; double bd = 0.8 * 0.8;
                    for (int i = 0; i < Mission!.Props.Count; i++)
                    {
                        var p = Mission.Props[i];
                        double d = (p.X - w.X) * (p.X - w.X) + (p.Y - w.Y) * (p.Y - w.Y);
                        if (d <= bd) { bd = d; bi = i; }
                    }
                    if (bi < 0) return "지울 소품 없음";
                    string tn = Mission.Props[bi].Type;
                    Mission.RemoveProp(bi); return $"소품 {tn} 지움";
                }
                else
                {
                    if (!HoverIn && !InMapTile(b)) return "지도 밖";
                    string type = PropTypes[Math.Clamp(PropSel, 0, PropTypes.Length - 1)];
                    double px = b.X + 0.5, py = b.Y + 0.5;
                    if (Mission!.Props.Any(p => p.Type == type && Math.Abs(p.X - px) < 0.01 && Math.Abs(p.Y - py) < 0.01)) return "같은 소품이 이미 있음";
                    Mission.AddProp(new PropPlace(type, px, py)); return $"소품 {type} 놓음";
                }
            case Tool.Site:
                if (del)
                {
                    int bi = -1; double bd = double.MaxValue;
                    for (int i = 0; i < Doc.Sites.Count; i++)
                    {
                        var s = Doc.Sites[i]; int size = Defs.Current.SiteKind(s.Type)?.Size ?? 3;
                        double cx = s.X + 0.5, cy = s.Y + 0.5, d = (cx - w.X) * (cx - w.X) + (cy - w.Y) * (cy - w.Y);
                        bool inside = Math.Abs(w.X - cx) <= size / 2.0 + 0.5 && Math.Abs(w.Y - cy) <= size / 2.0 + 0.5;
                        if (inside && d < bd) { bd = d; bi = i; }
                    }
                    if (bi < 0) return "지울 시설 없음";
                    string tn = Doc.Sites[bi].Type;
                    Doc.RemoveSite(bi); return $"시설 {tn} 지움";
                }
                else
                {
                    if (SiteTypes.Length == 0) return "점령 가능한 시설 종류가 없음";
                    string type = SiteTypes[Math.Clamp(SiteSel, 0, SiteTypes.Length - 1)];
                    int owner = Math.Clamp(OwnerSel, 0, Doc.Starts.Count);
                    if (!InMapTile(b)) return "지도 밖";
                    if (Doc.Sites.Any(s => s.X == b.X && s.Y == b.Y && s.Type == type)) return "같은 시설이 이미 있음";
                    Doc.AddSite(new ModelSite(b.X, b.Y, type, owner)); return $"시설 {type} (소유 {owner}) 놓음";
                }
            case Tool.Start:
                if (del)
                {
                    int i = NearestStart(w, 1.5f);
                    if (i < 0) return "지울 시작 없음";
                    if (Doc.Starts.Count <= 2) return "시작은 2개 이상 있어야 함";
                    Doc.RemoveStart(i); return $"시작 {i + 1} 지움";
                }
                if (DragStartIdx >= 0)
                {
                    if (b == DragStart) return "";
                    var cur = Doc.Starts[DragStartIdx];
                    if (cur[0] == b.X && cur[1] == b.Y) return "";
                    Doc.MoveStart(DragStartIdx, b.X, b.Y); return $"시작 {DragStartIdx + 1} 옮김 ({b.X},{b.Y})";
                }
                if (Doc.Starts.Count >= MapDoc.MaxStarts) return $"시작은 최대 {MapDoc.MaxStarts}개";
                if (!InMapTile(b)) return "지도 밖";
                Doc.AddStart(b.X, b.Y); return $"시작 {Doc.Starts.Count} 추가 ({b.X},{b.Y})";
            case Tool.Unit:
                if (del)
                {
                    int bi = -1; double bd = 1.2 * 1.2;
                    for (int i = 0; i < Doc.Units.Count; i++)
                    {
                        var u = Doc.Units[i]; double d = (u.X - w.X) * (u.X - w.X) + (u.Y - w.Y) * (u.Y - w.Y);
                        if (d <= bd) { bd = d; bi = i; }
                    }
                    if (bi < 0) return "지울 유닛 없음";
                    string tn = Doc.Units[bi].Unit;
                    Doc.RemoveUnit(bi); return $"유닛 {tn} 지움";
                }
                else
                {
                    if (!InMapTile(b)) return "지도 밖";
                    string type = UnitTypes[Math.Clamp(UnitSel, 0, UnitTypes.Length - 1)];
                    int fac = Math.Clamp(OwnerSel, 1, Math.Max(1, Doc.Starts.Count));
                    Doc.AddUnit(new ModelUnit(type, b.X + 0.5, b.Y + 0.5, fac, Math.Max(1, UnitCount))); return $"유닛 {type} x{UnitCount} (세력 {fac}) 놓음";
                }
            case Tool.Area:
            case Tool.Zone:
                if (NeedMissionFor(Tool == Tool.Area ? "구역" : "배치 구역")) return "";
                if (del)
                {
                    if (Tool == Tool.Area)
                    {
                        var hit = Mission!.Areas.LastOrDefault(ar => InShape(ar.Rect, ar.Circle, w.X, w.Y));
                        if (hit == null) return "지울 구역 없음";
                        int refs = Mission.Refs.Count(r => r.Name == hit.Name);
                        Mission.RemoveArea(hit.Name); return $"구역 {hit.Name} 지움" + (refs > 0 ? $" (참조 {refs}곳이 끊김)" : "");
                    }
                    int zi = -1;
                    for (int i = 0; i < Mission!.Zones.Count; i++) if (InShape(Mission.Zones[i].Rect, Mission.Zones[i].Circle, w.X, w.Y)) zi = i;
                    if (zi < 0) return "지울 배치 구역 없음";
                    Mission.RemoveZone(zi); return $"배치 구역 {zi + 1} 지움";
                }
                else
                {
                    double[]? rect = null, circle = null;
                    if (AreaCircle)
                    {
                        int r = (int)MathF.Round(MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)));
                        if (r < 1) return "원은 반지름 1 이상으로 끌어야 함";
                        circle = new double[] { a.X, a.Y, r };
                    }
                    else rect = new double[] { Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1 };
                    if (Tool == Tool.Area)
                    {
                        if (!MissionDoc.NameRule.IsMatch(AreaName)) return $"구역 이름 '{AreaName}' 은 영문 소문자 · 숫자 · _ 1~24자";
                        bool existed = Mission!.Areas.Any(ar => ar.Name == AreaName);
                        Mission.SetArea(AreaName, rect, circle);
                        string nm = AreaName;
                        if (!existed) { AreaSel = null; AreaName = NextFreeAreaNameAfter(nm); }
                        return existed ? $"구역 {nm} 모양 교체" : $"구역 {nm} 추가";
                    }
                    if (ZoneSel >= 0 && ZoneSel < Mission!.Zones.Count) { Mission.ReplaceZone(ZoneSel, rect, circle); return $"배치 구역 {ZoneSel + 1} 모양 교체"; }
                    int fac = Math.Clamp(ZoneFaction, 1, Math.Max(2, Mission!.FactionCount));
                    Mission.AddZone(new ZoneDef(fac, rect, circle)); return $"배치 구역 추가 (세력 {fac})";
                }
        }
        return "";
    }

    bool InMapTile((int X, int Y) t) => t.X >= 0 && t.Y >= 0 && t.X < Doc.Width && t.Y < Doc.Height;

    string NextFreeAreaNameAfter(string used)
    {
        // 방금 만든 이름 뒤의 다음 빈 이름 (add 직후라 Areas 에 used 가 이미 들어 있음)
        return NextFreeAreaName();
    }

    // ---------------------------------------------------------------- 목록 · 흩뿌리기 동작

    void SelectArea(int idx)
    {
        if (Mission == null) { NeedMissionFor("구역"); return; }
        if (idx < 0 || idx >= Mission.Areas.Count) return;
        string n = Mission.Areas[idx].Name;
        if (AreaSel == n) { AreaSel = null; AreaName = NextFreeAreaName(); Status = "선택 해제"; return; }
        AreaSel = n; AreaName = n;
        var (cx, cy) = Mission.Areas[idx].Center;
        Cam.CenterOn(new Vector2((float)cx, (float)cy));
        Status = $"구역 {n} 선택: 지도에 끌면 모양 교체";
    }

    void SelectZone(int idx)
    {
        if (Mission == null) { NeedMissionFor("배치 구역"); return; }
        if (idx < 0 || idx >= Mission.Zones.Count) return;
        if (ZoneSel == idx) { ZoneSel = -1; Status = "선택 해제"; return; }
        ZoneSel = idx;
        var (cx, cy) = Mission.Zones[idx].Center;
        Cam.CenterOn(new Vector2((float)cx, (float)cy));
        Status = $"배치 구역 {idx + 1} 선택: 지도에 끌면 모양 교체";
    }

    void DeleteSelected()
    {
        if (Mission == null) { NeedMissionFor("구역"); return; }
        try
        {
            if (Tool == Tool.Area)
            {
                if (AreaSel == null) { Status = "먼저 목록에서 구역을 고르세요"; return; }
                int refs = Mission.Refs.Count(r => r.Name == AreaSel);
                string n = AreaSel; Mission.RemoveArea(n); AreaSel = null;
                Refresh(); Status = $"구역 {n} 삭제" + (refs > 0 ? $" (참조 {refs}곳이 끊김 -- 검사 목록 확인)" : "");
            }
            else
            {
                if (ZoneSel < 0) { Status = "먼저 목록에서 배치 구역을 고르세요"; return; }
                int z = ZoneSel; Mission.RemoveZone(z); ZoneSel = -1;
                Refresh(); Status = $"배치 구역 {z + 1} 삭제";
            }
        }
        catch (Exception e) { Status = "삭제 실패: " + e.Message; Refresh(); }
    }

    void ScatterToggle()
    {
        if (NeedMissionFor("흩뿌리기")) return;
        Mission!.SetScatter(!Mission.PropScatter, Mission.PropDensity, Mission.PropCell);
        Refresh(); Status = Mission.PropScatter ? "흩뿌리기 켬" : "흩뿌리기 끔";
    }

    void ScatterNum(double dDensity, int dCell)
    {
        if (NeedMissionFor("흩뿌리기 숫자")) return;
        double dens = Math.Round(Math.Clamp(Mission!.PropDensity + dDensity, 0, 100) * 2) / 2;
        int cell = Mission.PropCell;
        if (dCell > 0) cell = cell == 0 ? 4 : Math.Min(64, cell + 1);
        else if (dCell < 0) cell = cell <= 4 ? 0 : cell - 1;
        if (dens == Mission.PropDensity && cell == Mission.PropCell) return;
        Mission.SetScatter(Mission.PropScatter, dens, cell);
        Refresh(); Status = $"밀도 {dens:0.0}  칸 {(cell == 0 ? "기본" : cell.ToString())}";
    }

    void ToggleScatterView()
    {
        ScatterPreview = !ScatterPreview;
        RefreshScatter();
        if (ScatterPreview && Mission?.PropScatter != true) Status = "흩뿌리기가 꺼져 있어 미리보기 없음 (먼저 켜기)";
        else Status = ScatterPreview ? $"미리보기 {ScatterList.Count}개" : "미리보기 끔";
    }

    // ---------------------------------------------------------------- 표시용 정보

    public string TerrainUnder(int x, int y)
    {
        var g = Doc.Raster();
        if (!g.InBounds(x, y)) return "-";
        int i = g.At(x, y);
        return PaletteLabels[Math.Min(i, 10)] + (g.High[y * g.Width + x] ? " (고지)" : "");
    }

    /// <summary>폰트 글자 모음: 고정 문구 + 이 도구 소스 (한글 상태 · 오류 문구가 모두 들어가게 빌드가 소스를 같이 복사함).</summary>
    public static IEnumerable<string> AllText()
    {
        var list = new List<string>(PaletteLabels.Concat(ToolLabels).Concat(new[] { "새로", "열기", "저장 ^S", "되돌리기", "다시 ^Y", "격자 G", "고지 H", "맞춤", "취소 (Esc)", "크기", "게임 지도", "게임 미션", "편집본", "이전", "다음" }));
        string src = Path.Combine(AppContext.BaseDirectory, "srcmirror");
        if (Directory.Exists(src))
            foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)) list.Add(File.ReadAllText(f));
        return list;
    }
}
