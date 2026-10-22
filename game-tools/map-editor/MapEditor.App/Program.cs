using System.Numerics;
using KoreanDivision;
using KoreanDivision.Render;
using KoreanDivision.UI;
using MapEditor.Model;
using Raylib_cs;

namespace MapEditor.App;

/// <summary>
/// 지도 편집기 (T33b/c). 사용: MapEditor [--map ID | --mission ID | --file PATH] [--savedir DIR] [--real-launch] [--script FILE] [--shot OUT.png] [--shotdir DIR] [--keep] [--theme ID]
///   --script  입력 재생 (ScriptPlayer 문서 참고)   --shot  스크립트가 끝난 뒤 마지막 화면을 PNG 로 저장 (스크립트 안 shot 명령도 가능)
///   --selftest [스크립트 폴더]  창 없이 시험 (스크립트 재생 + 버튼 배치/덮개 + 저장 왕복)
///   --real-launch  스크립트 안의 테스트 실행(F5)도 실제로 게임 창을 띄움 (기본: 스크립트 · selftest 는 인자만 기록)
/// 게임 지도 · 미션(assets)은 절대 덮어쓰지 않는다: 저장 위치 기본 = ./maps_edit/<id>.json (미션은 ./maps_edit/missions/<id>.json) (--savedir 로 변경; run_game.ps1 은 --out 을 PowerShell 공통 인자로 오해함).
/// </summary>
public static class EditorMain
{
    static string shotDir = "";
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string? map = null, file = null, script = null, shot = null, theme = "kd1920", selfDir = null;
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "maps_edit");
        bool keep = false, selftest = false, realLaunch = false;
        shotDir = Directory.GetCurrentDirectory();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--map": case "--mission": map = args[++i]; break;
                case "--file": file = args[++i]; break;
                case "--savedir": case "--out": outDir = Path.GetFullPath(args[++i]); break;
                case "--script": script = args[++i]; break;
                case "--shot": shot = Path.GetFullPath(args[++i]); break;
                case "--keep": keep = true; break;
                case "--real-launch": realLaunch = true; break;
                case "--shotdir": shotDir = Path.GetFullPath(args[++i]); break;
                case "--theme": theme = args[++i]; break;
                case "--selftest": selftest = true; if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) selfDir = args[++i]; break;
                default: Console.Error.WriteLine("알 수 없는 인자 " + args[i]); return 2;
            }
        }
        if (script != null && !realLaunch || selftest) Editor.Launcher = Editor.RecordLaunch;
        if (selftest) return SelfTest.Run(selfDir ?? Path.Combine(AppContext.BaseDirectory, "scripts"));

        var ed = new Editor(Editor.NewDoc(64, 64), "새 지도", outDir);
        if (map != null || file != null) { if (!ed.OpenPath(file ?? map!)) { Console.Error.WriteLine(ed.Status); return 2; } }

        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        Raylib.InitWindow(1280, 720, "Korean Division Map Editor");
        Raylib.SetWindowMinSize(960, 600);
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(60);
        Ui.LoadFonts(Editor.AllText());
        Theme.Use(theme);
        var view = new View();

        ScriptPlayer? player = null;
        IEnumerator<Step>? steps = null;
        if (script != null)
        {
            player = new ScriptPlayer(ed, Path.GetFileName(script), File.ReadAllLines(script));
            steps = player.Steps().GetEnumerator();
        }
        int exit = 0, idle = 0;
        var last = new InputFrame { Mouse = new Vector2(700, 300) };
        while (!Raylib.WindowShouldClose())
        {
            ed.WinW = Raylib.GetScreenWidth(); ed.WinH = Raylib.GetScreenHeight();
            string? shotNow = null;
            InputFrame f;
            if (steps != null)
            {
                if (steps.MoveNext())
                {
                    f = steps.Current.Frame ?? last.Clone();
                    shotNow = steps.Current.Shot;
                }
                else
                {
                    steps = null; f = last.Clone();
                    if (shot != null) shotNow = shot;
                    exit = player!.Failures > 0 ? 1 : 0;
                    foreach (var l in player.Log) Console.WriteLine(l);
                    if (!keep) idle = -1;
                }
            }
            else f = RealInput.Poll();
            last = f;
            ed.Update(f);
            // 스크립트가 열기 목록 등을 만들었으면 같은 프레임에 배치가 반영돼 있다
            view.Draw(ed, f.Mouse, shotNow == null ? null : ResolveShot(shotNow, shot));
            if (idle == -1) break;
        }
        view.Dispose();
        Ui.Unload();
        Raylib.CloseWindow();
        return exit;
    }

    static string ResolveShot(string name, string? defaultPath) => Path.IsPathRooted(name) ? name : Path.Combine(shotDir, name);
}

static class RealInput
{
    static readonly (KeyboardKey K, string N)[] Keys =
    {
        (KeyboardKey.B, "b"), (KeyboardKey.L, "l"), (KeyboardKey.R, "r"), (KeyboardKey.C, "c"), (KeyboardKey.F, "f"), (KeyboardKey.E, "e"),
        (KeyboardKey.G, "g"), (KeyboardKey.H, "h"), (KeyboardKey.Z, "z"), (KeyboardKey.Y, "y"), (KeyboardKey.S, "s"),
        (KeyboardKey.P, "p"), (KeyboardKey.T, "t"), (KeyboardKey.X, "x"), (KeyboardKey.U, "u"),
        (KeyboardKey.F5, "f5"), (KeyboardKey.Enter, "enter"), (KeyboardKey.Backspace, "backspace"),
        (KeyboardKey.LeftBracket, "["), (KeyboardKey.RightBracket, "]"), (KeyboardKey.Escape, "esc"),
    };

    public static InputFrame Poll()
    {
        var f = new InputFrame
        {
            Mouse = Raylib.GetMousePosition(),
            Left = Raylib.IsMouseButtonDown(MouseButton.Left),
            Right = Raylib.IsMouseButtonDown(MouseButton.Right),
            Middle = Raylib.IsMouseButtonDown(MouseButton.Middle),
            Wheel = Raylib.GetMouseWheelMove(),
        };
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        foreach (var (k, n) in Keys)
        {
            if (!Raylib.IsKeyPressed(k)) continue;
            if (ctrl) { if (n is "z" or "y" or "s") f.Keys.Add("ctrl+" + n); }
            else f.Keys.Add(n);
        }
        var chars = new System.Text.StringBuilder();
        for (int c = Raylib.GetCharPressed(); c != 0; c = Raylib.GetCharPressed()) chars.Append(char.ConvertFromUtf32(c));
        f.Chars = chars.ToString();
        return f;
    }
}
