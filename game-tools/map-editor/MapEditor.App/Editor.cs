using System.Numerics;
using System.Reflection;
using KoreanDivision;
using KoreanDivision.Core;
using KoreanDivision.Render;
using MapEditor.Model;
using Raylib_cs;

namespace MapEditor.App;

public enum Tool { Brush, Line, Rect, Circle, Fill, Eraser }

/// <summary>한 프레임의 입력. 실제 입력(Raylib)과 스크립트 재생이 같은 모양을 만든다.</summary>
public sealed class InputFrame
{
    public Vector2 Mouse;
    public bool Left, Right, Middle;
    public float Wheel;
    public List<string> Keys = new();   // "b", "ctrl+z", "[" ... (소문자)
    public InputFrame Clone() => new() { Mouse = Mouse, Left = Left, Right = Right, Middle = Middle, Wheel = Wheel, Keys = new List<string>(Keys) };
}

public sealed class Button
{
    public string Id = "", Label = "";
    public Rectangle Rect;
    public Action Click = () => { };
    public Func<bool> Active = () => false;
    public Color? Swatch;
}

/// <summary>
/// 편집기 상태 + 입력 처리 (창 없이도 돈다: 렌더러만 창이 있을 때 만든다). 문서 연산은 전부 MapDoc (T33a) 를 거친다 -- 여기서 지도 의미를 새로 만들지 않는다.
/// </summary>
public sealed class Editor
{
    public const int TopH = 40, StatusH = 26, LeftW = 132, RightW = 184;
    public static readonly string[] PaletteNames = MapDoc.TerrainNames.Concat(new[] { "high", "low" }).ToArray();
    public static readonly string[] PaletteLabels = { "풀", "흙", "모래", "물", "바위", "숲", "잔해", "집", "철조망", "참호", "구덩이", "고지 올림", "고지 내림" };
    public static readonly string[] ToolLabels = { "브러시 B", "선 L", "사각 R", "원 C", "채움 F", "지우개 E" };

    static readonly MethodInfo loadText = typeof(MapFile).GetMethod("LoadText", BindingFlags.NonPublic | BindingFlags.Static)!;

    static Editor() { Defs.Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "balance.json")); }   // 시설/유닛 정의: 게임 로더가 요구

    public MapDoc Doc { get; private set; }
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
    public bool PopupOpen;
    public List<(string Label, string Path)> PopupItems = new();
    public bool RenderDirty = true;       // 창 쪽이 렌더러를 다시 만들 때 확인
    public GameMap? PreviewMap;           // 마지막으로 성공한 게임 지도 (렌더러 입력)
    public int Rebuilds;
    public string LastHash = "";
    public string LoadError = "";

    // 드래그 상태
    public bool Dragging;
    public int DragButton;       // 0 = 왼쪽, 1 = 오른쪽(지우개)
    public (int X, int Y) DragStart, DragCur;
    public readonly List<(int X, int Y)> DragCells = new();
    public (int X, int Y) Hover;
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

    public void LoadDoc(MapDoc doc, string label, string? savePath)
    {
        Doc = doc; SourceLabel = label; SavePath = savePath; PopupOpen = false;
        CancelDrag();
        Cam = new RtsCamera(doc.Width, doc.Height);
        Cam.SetViewport(MapView);
        FitCamera();
        Refresh(true);
        Status = LoadError.Length > 0 ? LoadError : $"열림: {label} ({doc.Width}x{doc.Height})";
    }

    public static MapDoc NewDoc(int w, int h)
    {
        int cy = h / 2;
        return MapDoc.Parse($"{{\"name\":\"new\",\"width\":{w},\"height\":{h},\"base\":\"grass\",\"starts\":[[{Math.Min(8, w - 1)},{cy}],[{Math.Max(0, w - 9)},{cy}]]}}");
    }

    /// <summary>문서가 바뀐 뒤: 게임 지도로 다시 굽고 (게임과 같은 로더) 변경 표시 갱신.</summary>
    public void Refresh(bool markSaved = false)
    {
        string json = Doc.ToJson();
        LoadError = "";
        string hash = MapDoc.Hash(json);
        LastHash = hash;
        if (markSaved) SavedHash = hash;
        Dirty = hash != SavedHash;
        try
        {
            PreviewMap = (GameMap)loadText.Invoke(null, new object?[] { json + "\n", "edit", Config.DefaultSeed, null })!;
            RenderDirty = true; Rebuilds++;
        }
        catch (Exception e)
        {
            LoadError = "게임 로드 실패: " + (e.InnerException?.Message ?? e.Message);
            Status = LoadError;
        }
    }

    public void FitCamera()
    {
        float z = Math.Min(MapView.Width / (Doc.Width * Config.TileSize), MapView.Height / (Doc.Height * Config.TileSize));
        Cam.SetZoom(Math.Clamp(z, Config.MinZoom, Config.MaxZoom));
        Cam.CenterOn(new Vector2(Doc.Width / 2f, Doc.Height / 2f));
    }

    public string CurrentTerrain => PaletteNames[Palette];

    // ---------------------------------------------------------------- 배치 / 버튼

    public void Layout(int w, int h)
    {
        WinW = w; WinH = h;
        MapView = new Rectangle(LeftW, TopH, Math.Max(64, w - LeftW - RightW), Math.Max(64, h - TopH - StatusH));
        Cam.SetViewport(MapView);
        Buttons.Clear();
        float x = 8;
        void Top(string id, string label, float bw, Action click, Func<bool>? active = null)
        {
            Buttons.Add(new Button { Id = id, Label = label, Rect = new Rectangle(x, 6, bw, 28), Click = click, Active = active ?? (() => false) });
            x += bw + 6;
        }
        Top("new", "새로", 50, () => LoadDoc(NewDoc(64, 64), "새 지도", null));
        Top("open", "열기", 50, OpenPopup);
        Top("save", "저장 ^S", 72, () => Save());
        x += 10;
        Top("undo", "되돌리기", 76, () => Do(Doc.Undo(), "되돌림"));
        Top("redo", "다시 ^Y", 66, () => Do(Doc.Redo(), "다시 적용"));
        x += 10;
        Top("grid", "격자 G", 62, () => ShowGrid = !ShowGrid, () => ShowGrid);
        Top("high", "고지 H", 62, () => ShowHigh = !ShowHigh, () => ShowHigh);
        x += 10;
        Top("zoomout", "-", 30, () => Cam.SetZoom(Cam.Zoom / Config.ZoomStep));
        Top("zoomin", "+", 30, () => Cam.SetZoom(Cam.Zoom * Config.ZoomStep));
        Top("fit", "맞춤", 50, FitCamera);

        float y = TopH + 10;
        for (int i = 0; i < 6; i++)
        {
            var t = (Tool)i;
            string[] ids = { "tool_brush", "tool_line", "tool_rect", "tool_circle", "tool_fill", "tool_eraser" };
            Buttons.Add(new Button { Id = ids[i], Label = ToolLabels[i], Rect = new Rectangle(8, y, LeftW - 16, 32), Click = () => Tool = t, Active = () => Tool == t });
            y += 38;
        }
        y += 8;
        Buttons.Add(new Button { Id = "size_dec", Label = "-", Rect = new Rectangle(8, y + 22, 34, 28), Click = () => BrushSize = Math.Max(1, BrushSize - 1) });
        Buttons.Add(new Button { Id = "size_inc", Label = "+", Rect = new Rectangle(LeftW - 42, y + 22, 34, 28), Click = () => BrushSize = Math.Min(15, BrushSize + 1) });

        float px = w - RightW + 8, py = TopH + 10;
        for (int i = 0; i < PaletteNames.Length; i++)
        {
            int k = i;
            Color sw = i < MapDoc.TerrainNames.Length ? Terrains.Get((TerrainType)i).Color : (i == 11 ? new Color(230, 220, 120, 255) : new Color(110, 140, 170, 255));
            Buttons.Add(new Button { Id = "pal_" + i, Label = PaletteLabels[i], Rect = new Rectangle(px, py, RightW - 16, 26), Click = () => Palette = k, Active = () => Palette == k, Swatch = sw });
            py += 30;
        }

        if (PopupOpen)
        {
            float pw = 320, pxx = (w - pw) / 2, pyy = 80;
            for (int i = 0; i < PopupItems.Count; i++)
            {
                int k = i;
                Buttons.Add(new Button { Id = "item_" + i, Label = PopupItems[i].Label, Rect = new Rectangle(pxx + 10, pyy + 40 + i * 30, pw - 20, 26), Click = () => OpenItem(k) });
            }
            Buttons.Add(new Button { Id = "popup_close", Label = "취소 (Esc)", Rect = new Rectangle(pxx + 10, pyy + 48 + PopupItems.Count * 30, pw - 20, 28), Click = () => PopupOpen = false });
        }
    }

    public Rectangle PopupRect => new((WinW - 320) / 2f, 80, 320, 100 + PopupItems.Count * 30);

    public Button? ButtonAt(Vector2 p)
    {
        if (PopupOpen)
        {
            foreach (var b in Buttons) if ((b.Id.StartsWith("item_") || b.Id == "popup_close") && Contains(b.Rect, p)) return b;
            return null;
        }
        foreach (var b in Buttons) if (!b.Id.StartsWith("item_") && b.Id != "popup_close" && Contains(b.Rect, p)) return b;
        return null;
    }

    public Button? ButtonById(string id) => Buttons.FirstOrDefault(b => b.Id == id);

    static bool Contains(Rectangle r, Vector2 p) => p.X >= r.X && p.X < r.X + r.Width && p.Y >= r.Y && p.Y < r.Y + r.Height;

    void Do(bool ok, string msg)
    {
        if (ok) { Refresh(); Status = msg + $" (남은 되돌리기 {Doc.UndoDepth})"; }
        else Status = "더 할 것이 없음";
    }

    // ---------------------------------------------------------------- 열기 / 저장

    void OpenPopup()
    {
        PopupItems.Clear();
        foreach (var id in MapFile.Available()) PopupItems.Add((id + "  (게임 지도)", MapFile.PathOf(id)));
        if (Directory.Exists(SaveDir))
            foreach (var f in Directory.GetFiles(SaveDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                PopupItems.Add((Path.GetFileNameWithoutExtension(f) + "  (편집본)", f));
        if (PopupItems.Count > 12) PopupItems.RemoveRange(12, PopupItems.Count - 12);
        PopupOpen = true;
        Layout(WinW, WinH);
    }

    void OpenItem(int i)
    {
        if (i < 0 || i >= PopupItems.Count) return;
        OpenPath(PopupItems[i].Path);
    }

    /// <summary>지도 id 또는 파일 경로를 연다. 게임 지도 (assets/maps) 는 덮어쓰지 않도록 저장 위치를 SaveDir 로 돌린다.</summary>
    public bool OpenPath(string idOrPath)
    {
        string path = File.Exists(idOrPath) ? Path.GetFullPath(idOrPath) : MapFile.PathOf(idOrPath);
        if (!File.Exists(path)) { Status = "파일 없음: " + idOrPath; return false; }
        try
        {
            var doc = MapDoc.Parse(File.ReadAllText(path));
            bool isAsset = string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(MapFile.Folder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            string name = Path.GetFileNameWithoutExtension(path);
            LoadDoc(doc, name, isAsset ? Path.Combine(SaveDir, name + ".json") : path);
            if (doc.DroppedComments > 0) Status += $"  (주석 {doc.DroppedComments}줄은 저장 때 버려짐)";
            Layout(WinW, WinH);
            return true;
        }
        catch (Exception e) { Status = "열기 실패: " + e.Message; return false; }
    }

    public bool Save() => SaveTo(SavePath ?? Path.Combine(SaveDir, Doc.Name.Length > 0 ? Doc.Name + ".json" : "map.json"));

    public bool SaveTo(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            string text = Doc.ToFileText(), tmp = full + ".tmp";
            File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(false));
            File.Move(tmp, full, true);
            // 저장 직후 게임 로더로 재적재해 같은 검증을 통과하는지 확인
            loadText.Invoke(null, new object?[] { File.ReadAllText(full), "check", Config.DefaultSeed, null });
            SavePath = full; SavedHash = MapDoc.Hash(Doc.ToJson()); Dirty = false;
            Status = "저장됨: " + full;
            return true;
        }
        catch (Exception e) { Status = "저장 실패: " + (e.InnerException?.Message ?? e.Message); return false; }
    }

    // ---------------------------------------------------------------- 입력

    public (int X, int Y) TileAt(Vector2 screen)
    {
        var w = Cam.ScreenToWorld(screen);
        return ((int)MathF.Floor(w.X), (int)MathF.Floor(w.Y));
    }

    (int X, int Y) Clamp((int X, int Y) t) => (Math.Clamp(t.X, 0, Doc.Width - 1), Math.Clamp(t.Y, 0, Doc.Height - 1));

    void CancelDrag() { Dragging = false; DragCells.Clear(); }

    public void Update(InputFrame f)
    {
        Layout(WinW, WinH);   // 크기는 호스트가 SetSize 로 바꾼다; 팝업 열림 상태 반영
        bool lPress = f.Left && !prevL, lRel = !f.Left && prevL;
        bool rPress = f.Right && !prevR, rRel = !f.Right && prevR;
        Vector2 delta = ready ? f.Mouse - prevMouse : Vector2.Zero;
        ready = true; prevMouse = f.Mouse;
        bool inMap = Contains(MapView, f.Mouse);
        var tile = TileAt(f.Mouse);
        Hover = tile; HoverIn = inMap && tile.X >= 0 && tile.Y >= 0 && tile.X < Doc.Width && tile.Y < Doc.Height;

        foreach (var k in f.Keys) Key(k);

        if (lPress || rPress)
        {
            var b = ButtonAt(f.Mouse);
            if (b != null && lPress) { b.Click(); Status = b.Label.Trim() + " 눌림"; }
            else if (!PopupOpen && inMap && !Dragging) BeginDrag(rPress ? 1 : 0, Clamp(tile));
        }
        if (Dragging)
        {
            var cur = Clamp(tile);
            if (cur != DragCur) { if (Tool == Tool.Brush || Tool == Tool.Eraser || DragButton == 1) AddBrushCells(DragCur, cur); DragCur = cur; }
            if ((DragButton == 0 && lRel) || (DragButton == 1 && rRel)) EndDrag();
        }
        if (f.Middle && prevM && inMap) Cam.Scroll(-delta);
        if (f.Wheel != 0 && inMap && !PopupOpen) Cam.ZoomAt(f.Wheel, f.Mouse);
        prevL = f.Left; prevR = f.Right; prevM = f.Middle;
    }

    void Key(string k)
    {
        switch (k)
        {
            case "b": Tool = Tool.Brush; break;
            case "l": Tool = Tool.Line; break;
            case "r": Tool = Tool.Rect; break;
            case "c": Tool = Tool.Circle; break;
            case "f": Tool = Tool.Fill; break;
            case "e": Tool = Tool.Eraser; break;
            case "g": ShowGrid = !ShowGrid; break;
            case "h": ShowHigh = !ShowHigh; break;
            case "[": BrushSize = Math.Max(1, BrushSize - 1); break;
            case "]": BrushSize = Math.Min(15, BrushSize + 1); break;
            case "ctrl+z": Do(Doc.Undo(), "되돌림"); break;
            case "ctrl+y": Do(Doc.Redo(), "다시 적용"); break;
            case "ctrl+s": Save(); break;
            case "esc": if (PopupOpen) { PopupOpen = false; } else CancelDrag(); break;
        }
    }

    void BeginDrag(int button, (int X, int Y) t)
    {
        Dragging = true; DragButton = button; DragStart = DragCur = t; DragCells.Clear();
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
        Commit(DragStart, DragCur, DragButton);
        DragCells.Clear();
    }

    /// <summary>획을 문서 연산 하나로 확정 (undo 한 번 = 획 하나).</summary>
    void Commit((int X, int Y) a, (int X, int Y) b, int button)
    {
        try { CommitCore(a, b, button); }
        catch (Exception e) { Status = "칠하기 실패: " + e.Message; Refresh(); }
    }

    void CommitCore((int X, int Y) a, (int X, int Y) b, int button)
    {
        string t = CurrentTerrain;
        bool erase = Tool == Tool.Eraser || button == 1;
        int before = Doc.UndoDepth;
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
        if (Doc.UndoDepth != before) { Refresh(); Status = $"{ToolLabels[(int)Tool].Split(' ')[0]} {(erase ? Doc.Base : t)} 칠함 (되돌리기 {Doc.UndoDepth})"; }
        else Status = "변화 없음";
    }

    // ---------------------------------------------------------------- 표시용 정보

    public string TerrainUnder(int x, int y)
    {
        var g = Doc.Raster();
        if (!g.InBounds(x, y)) return "-";
        int i = g.At(x, y);
        return PaletteLabels[Math.Min(i, 10)] + (g.High[y * g.Width + x] ? " (고지)" : "");
    }

    public static IEnumerable<string> AllText() =>
        PaletteLabels.Concat(ToolLabels).Concat(new[] { "새로", "열기", "저장 ^S", "되돌리기", "다시 ^Y", "격자 G", "고지 H", "맞춤", "취소 (Esc)", "크기", "새 지도", "편집본", "게임 지도", "저장됨", "변경됨", "눌림", "열림", "칠함", "획", "변화 없음", "더 할 것이 없음", "남은", "되돌림", "다시 적용", "파일 없음", "열기 실패", "저장 실패", "게임 로드 실패", "주석", "줄은", "저장", "때", "버려짐", "지도", "칸", "고지", "사각", "원", "선", "브러시", "채움", "지우개", "되돌리기", "연산", "해시", "크기", "시작" });
}
