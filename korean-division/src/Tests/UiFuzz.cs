using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>UI 퍼즈용 가짜 입력: UiFuzz 가 프레임마다 채운다.</summary>
public sealed class FuzzInput : IInputSource
{
    public Vector2 Mouse, Delta;
    public float Wheel;
    public readonly HashSet<MouseButton> Pressed = new(), Down = new(), Released = new();
    public readonly HashSet<KeyboardKey> KeyPress = new(), KeyHeld = new();
    public readonly Queue<int> Chars = new();
    public Vector2 GetMousePosition() => Mouse;
    public Vector2 GetMouseDelta() => Delta;
    public float GetMouseWheelMove() => Wheel;
    public bool IsMouseButtonPressed(MouseButton b) => Pressed.Contains(b);
    public bool IsMouseButtonDown(MouseButton b) => Down.Contains(b);
    public bool IsMouseButtonReleased(MouseButton b) => Released.Contains(b);
    public bool IsKeyPressed(KeyboardKey k) => KeyPress.Contains(k);
    public bool IsKeyPressedRepeat(KeyboardKey k) => false;
    public bool IsKeyDown(KeyboardKey k) => KeyHeld.Contains(k);
    public int GetCharPressed() => Chars.Count > 0 ? Chars.Dequeue() : 0;
    public bool IsWindowFocused() => false;   // 가장자리 스크롤은 끈다 (마우스가 화면 끝을 자주 지나므로 카메라가 구석에 붙어버림)
    public bool IsCursorOnScreen() => false;
}

/// <summary>
/// UI 크래시 퍼즈 (--uifuzz [초]): 실제 창 · 실제 Game/Hud 코드를 돌리면서 마우스를 화면 전체에 무작위로 훑고,
/// 클릭 · 키 · 콘솔 명령을 주입하고, 선택 대상을 모든 종류의 엔티티로 바꿔가며 그리기 · 입력 경로의 예외를 잡아낸다.
/// 예외는 (종류 + 우리 코드의 최상단 프레임) 으로 묶어 한 번만 자세히 보고하고 계속 진행한다. 결정적 (--seed 의 의사난수).
/// </summary>
public sealed class UiFuzz
{
    readonly Random rng;
    readonly FuzzInput inp = new();
    public readonly double Seconds;
    readonly DateTime start = DateTime.UtcNow;
    int rightFrames;
    long frame;
    int nextScenario = 30, stay, leftFrames, gameFrame, overFrames, shiftBurst;
    Building? aimBuilding;
    bool endedOnce;
    int lastSaves;
    Game? current;
    string context = "";
    readonly Dictionary<string, (int Count, string First)> crashes = new();
    public int Exceptions;
    // 커버리지: 선택 종류 · 사건 횟수
    readonly HashSet<string> seenSelections = new();
    readonly Dictionary<string, int> events = new();
    void Ev(string k) => events[k] = events.GetValueOrDefault(k) + 1;

    public UiFuzz(double seconds, int seed)
    {
        Seconds = seconds;
        rng = new Random(seed);
        Input.Source = inp;
        Ui.HitRecording = true;   // T38g: 그려진 버튼 사각형을 모아 겨냥 클릭에 쓴다
        PrepareSaves();
        // 멈춤 감시: 10초 넘게 프레임이 안 넘어가면 마지막 단계 · 상황을 출력 (무한 루프 · 교착 진단용)
        var wd = new Thread(() =>
        {
            long last = -1; var since = DateTime.UtcNow; bool told = false;
            while (true)
            {
                Thread.Sleep(1000);
                long f = Interlocked.Read(ref frame);
                if (f != last) { last = f; since = DateTime.UtcNow; told = false; continue; }
                if (!told && (DateTime.UtcNow - since).TotalSeconds > 10) { told = true; Console.Error.WriteLine($"[uifuzz] 멈춤 10초+: frame {f}, 단계 {phaseNow}, context {context}"); }
            }
        }) { IsBackground = true };
        wd.Start();
    }
    volatile string phaseNow = "";


    /// <summary>
    /// 저장 / 불러오기 퍼즈 준비: 퍼즈 전용 임시 saves 폴더 (진짜 saves/ 를 건드리지 않음)에 정상 저장 · 교전 저장 · 잘린 · 조각난 · 다른 형식 · 쓰레기 파일을 만들어
    /// 불러오기 페이지 · 불러오기 경로가 깨진 파일 앞에서도 예외 없이 동작하는지 두드린다. 자동 저장 간격도 짧게.
    /// </summary>
    static void PrepareSaves()
    {
        string dir = TestTemp.Child("fuzz_saves");
        try
        {
            Directory.CreateDirectory(dir);
            SaveManager.FolderOverride = dir;
            SaveManager.AutosaveTicks = 600;
            var defs = Defs.Current;
            var w = World.CreateStandard(MapFile.Load("corners4", 5), defs);
            for (int f = 1; f <= w.Map.Players; f++) w.Controllers.Add((f, AiRegistry.Create("standard")!));
            for (int i = 0; i < 400; i++) w.Step();
            byte[] good = Snapshot.Save(w, new SnapshotMeta("fz_good", 1, false, "standard"));
            File.WriteAllBytes(Path.Combine(dir, "fz_good.kds"), good);
            File.WriteAllBytes(Path.Combine(dir, "fz_trunc.kds"), good[..(good.Length / 2)]);
            var flip = (byte[])good.Clone(); flip[^30] ^= 0xFF;
            File.WriteAllBytes(Path.Combine(dir, "fz_flip.kds"), flip);
            var old = (byte[])good.Clone(); old[4] = 77;
            File.WriteAllBytes(Path.Combine(dir, "fz_oldformat.kds"), old);
            var junk = new byte[3000]; new Random(3).NextBytes(junk);
            File.WriteAllBytes(Path.Combine(dir, "fz_garbage.kds"), junk);
            File.WriteAllBytes(Path.Combine(dir, "fz_empty.kds"), Array.Empty<byte>());
            var battle = new BattleSetup { SideA = "infantry:40,tank_ft_male:4", SideB = "infantry:40", Map = "flat", Distance = 30, AiVsAi = true }.CreateWorld(defs, 2);
            battle.Controllers.Add((1, new BattleAi())); battle.Controllers.Add((2, new BattleAi()));
            for (int i = 0; i < 200; i++) battle.Step();
            File.WriteAllBytes(Path.Combine(dir, "fz_battle.kds"), Snapshot.Save(battle, new SnapshotMeta("fz_battle", 1, true, "battle")));
        }
        catch (Exception e) { Console.Error.WriteLine($"[uifuzz] 저장 파일 준비 실패: {e.Message}"); }
    }
    public bool Finished => (DateTime.UtcNow - start).TotalSeconds >= Seconds;
    public int UniqueCrashes => crashes.Count;

    // ---------------------------------------------------------------- 예외 수집

    /// <summary>예외를 잡아 기록하고 계속 진행. phase = update/draw/menu.</summary>
    public bool Guard(string phase, Action a)
    {
        phaseNow = phase;
        try { a(); return true; }
        catch (Exception e)
        {
            Exceptions++;
            string site = "?";
            foreach (var line in (e.StackTrace ?? "").Split('\n'))
                if (line.Contains("KoreanDivision.") && !line.Contains("KoreanDivision.Tests.UiFuzz")) { site = line.Trim(); break; }
            string key = $"{phase}: {e.GetType().Name} @ {site}";
            if (crashes.TryGetValue(key, out var c)) crashes[key] = (c.Count + 1, c.First);
            else
            {
                string info = $"[FUZZ-CRASH #{crashes.Count + 1}] {key}\n  frame {frame} ({gameFrame} in game), context: {context}\n  {e.Message}\n{e.StackTrace}";
                crashes[key] = (1, info);
                Console.Error.WriteLine(info);
            }
            return false;
        }
    }

    public int Report()
    {
        Console.Error.WriteLine($"[uifuzz] 선택 종류 {seenSelections.Count}가지: {string.Join(" ", seenSelections.OrderBy(x => x))}");
        Console.Error.WriteLine($"[uifuzz] 겨냥 클릭: 서로 다른 버튼 {aimedLabels.Count}개 (작은 것 {aimedSmall.Count}개: {string.Join(" | ", aimedSmall.OrderBy(x => x).Take(12))})");
        Console.Error.WriteLine($"[uifuzz] 사건: {string.Join(", ", events.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Console.Error.WriteLine(KoreanDivision.Render.LodAtlas.Summary());
        Console.Error.WriteLine($"[uifuzz] {frame} 프레임, 예외 {Exceptions}건, 고유 크래시 지점 {crashes.Count}곳");
        try { if (SaveManager.FolderOverride != null && SaveManager.FolderOverride.Contains("kd_fuzz_saves_")) Directory.Delete(SaveManager.FolderOverride, true); } catch (IOException) { }
        foreach (var (k, v) in crashes) Console.Error.WriteLine($"  x{v.Count}  {k}");
        return crashes.Count == 0 ? 0 : 3;
    }

    // ---------------------------------------------------------------- 메뉴

    /// <summary>시작 화면 퍼즈: 마우스를 훑고 가끔 클릭 · Esc.</summary>
    public void MenuFrame(int w, int h)
    {
        BeginFrame();
        context = "menu";
        if (stay-- <= 0)
        {
            // 절반은 가운데 열 (버튼 · 불러오기 목록 행이 있는 곳) 을 집중해서 두드린다
            inp.Mouse = rng.Next(2) == 0 ? new Vector2(rng.Next(0, w), rng.Next(0, h)) : new Vector2(w / 2f + rng.Next(-420, 420), h * 0.3f + rng.Next(0, (int)(h * 0.6f)));
            stay = rng.Next(1, 4);
        }
        if (rng.Next(14) == 0) { if (!AimedClick()) inp.Pressed.Add(MouseButton.Left); }
        if (rng.Next(400) == 0) inp.KeyPress.Add(KeyboardKey.Escape);
        if (rng.Next(60) == 0) inp.Wheel = rng.Next(-3, 4);
        if (rng.Next(30) == 0) inp.KeyHeld.Add(KeyboardKey.LeftShift);
    }

    // ---------------------------------------------------------------- 겨냥 클릭 (T38g)

    (Rectangle R, string Label)[] targets = Array.Empty<(Rectangle, string)>();   // 직전 프레임에 그려진 클릭 가능 사각형
    readonly HashSet<string> aimedLabels = new(), aimedSmall = new();
    const float SmallPx = 40f;   // 이 이하(가로·세로 모두)면 작은 버튼 ([-][+] · 쪽 넘김 등)
    static bool IsSmall(Rectangle r) => MathF.Max(r.Width, r.Height) <= SmallPx;

    /// <summary>클릭 30% 는 작은 버튼, 20% 는 아무 등록 버튼을 정확히 겨냥한다 (나머지는 호출자의 무작위). 겨냥했으면 true.</summary>
    bool AimedClick()
    {
        if (targets.Length == 0) return false;
        int roll = rng.Next(10);
        if (roll >= 5) return false;
        bool wantSmall = roll < 3;
        var pool = wantSmall ? targets.Where(t => IsSmall(t.R)).ToArray() : targets;
        if (pool.Length == 0) { if (wantSmall) pool = targets; else return false; }
        var (r, label) = pool[rng.Next(pool.Length)];
        inp.Mouse = new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2);
        inp.Pressed.Add(MouseButton.Left);
        bool small = IsSmall(r);
        Ev(small ? "aim-small" : "aim-any");
        aimedLabels.Add(label); if (small) aimedSmall.Add(label);
        return true;
    }

    void BeginFrame()
    {
        targets = Ui.Hits.ToArray(); Ui.Hits.Clear();
        frame++;
        inp.Pressed.Clear(); inp.Released.Clear(); inp.KeyPress.Clear(); inp.KeyHeld.Clear();
        inp.Wheel = 0; inp.Delta = Vector2.Zero;
    }

    // ---------------------------------------------------------------- 게임 프레임

    static readonly KeyboardKey[] Keys = BuildKeys();
    static KeyboardKey[] BuildKeys()
    {
        var l = new List<KeyboardKey>();
        for (var k = KeyboardKey.A; k <= KeyboardKey.Z; k++) l.Add(k);
        for (var k = KeyboardKey.Zero; k <= KeyboardKey.Nine; k++) l.Add(k);
        for (var k = KeyboardKey.Kp0; k <= KeyboardKey.Kp9; k++) l.Add(k);
        l.AddRange(new[] { KeyboardKey.Escape, KeyboardKey.Space, KeyboardKey.Tab, KeyboardKey.F10, KeyboardKey.F1, KeyboardKey.F2, KeyboardKey.F3, KeyboardKey.F6, KeyboardKey.F7, KeyboardKey.Equal, KeyboardKey.Minus,
            KeyboardKey.KpAdd, KeyboardKey.KpSubtract, KeyboardKey.LeftBracket, KeyboardKey.RightBracket, KeyboardKey.Backspace, KeyboardKey.Up, KeyboardKey.Down });
        return l.ToArray();
    }

    static readonly string[] ConsoleLines =
    {
        "help", "?", "fps", "mute", "volume 30", "volume x", "zoom 0.5", "zoom 2", "zoom nan", "zoom 0", "zoom -5", "zoom 1e30", "reveal", "show all maps",
        "spawn tank 5", "spawn infantry 200", "spawn worker 1", "spawn nothing", "spawn", "select infantry",
        "select tank_ft_male", "select worker", "select zzz", "unknownthing 1 2 3", "  ", "SPAWN SCOUT 3",
        "save", "save fz_a", "save quicksave", "save ../evil", "save a:b*c", "save " + new string('x', 200), "saves", "load", "load fz_good", "load fz_trunc", "load fz_garbage", "load fz_flip", "load fz_oldformat", "load fz_battle", "load nothere", "load quicksave", "load fz_a", "SAVE X", "mission win", "mission lose", "mission", "mission xyz",
    };

    /// <summary>게임 한 프레임 전: 이번 프레임의 입력과 시나리오 변경을 만든다.</summary>
    public void GameFrame(Game g)
    {
        if (!ReferenceEquals(current, g)) { Ev("games-started"); if (g.World.OriginSnapshot != null) Ev("games-loaded"); current = g; gameFrame = 0; overFrames = 0; nextScenario = 20; endedOnce = false; lastSaves = 0; Setup(g); }
        if (g.SavesDone != lastSaves) { events["saves"] = events.GetValueOrDefault("saves") + g.SavesDone - lastSaves; lastSaves = g.SavesDone; }
        if (g.PendingLoad != null) Ev("load-requested");
        BeginFrame();
        gameFrame++;
        var w = g.World;
        if (g.Selection.Count > 0) seenSelections.Add(Kind(g, g.Selection));
        if (g.CommandBox.IsOpen) Ev("console-open");
        if (g.Paused) Ev("paused");
        if (g.Tab == CardTab.Combat) Ev("tab-combat");
        if (g.Hud.MultiPage > 0) Ev("multi-page>0");
        if (g.SelectedCompanies().Count > 0) Ev("company-cards");
        if (g.ShowBatList) Ev("reg-list-open");
        if (w.Winner != 0) Ev("gameover-frames");
        if (g.DeployPhase) Ev("mission-deploy-frames");
        if (w.Mission != null && w.Winner != 0) Ev("mission-results-frames");
        context = $"tick {w.Tick} sel={Describe(g)} paused={g.Paused} console={g.CommandBox.IsOpen} winner={w.Winner}";
        if (gameFrame % 700 == 350) Raylib.SetWindowSize(rng.Next(Config.MinScreenWidth, 1921), rng.Next(Config.MinScreenHeight, 1081));   // 창 크기 변경 (Layout 재계산 경로)
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();

        // ---- 마우스 위치
        if (stay-- <= 0)
        {
            stay = rng.Next(1, 6);
            inp.Mouse = PickMouse(g, sw, sh);
        }
        // ---- 새 UI: 탭 · 쪽 넘김 버튼 · Shift 공병 배분을 직접 두드린다
        if (rng.Next(12) == 0 && !g.Observer)
        {
            int ti = rng.Next(3);
            inp.Mouse = new Vector2(g.Hud.TabRect(ti).X + 8, g.Hud.TabRect(ti).Y + 8);
            inp.Pressed.Add(MouseButton.Left); Ev("tab-click");
        }
        else if (rng.Next(14) == 0 && g.Selection.Count > 25)
        {
            var pr = g.Hud.PagerRect(rng.Next(2) == 0 ? -1 : 1, g.Selection.Count);
            inp.Mouse = new Vector2(pr.X + 5, pr.Y + 5);
            inp.Pressed.Add(MouseButton.Left); Ev("pager-click");
        }
        else if (g.Tab == CardTab.Combat && rng.Next(4) == 0)
        {
            var cr = g.Hud.Card;
            inp.Mouse = new Vector2(cr.X + 10 + rng.Next(0, (int)cr.Width - 20), cr.Y + 30 + rng.Next(0, (int)cr.Height - 40));
            inp.Pressed.Add(MouseButton.Left); Ev("combat-click");
        }
        if (shiftBurst > 0)
        {
            shiftBurst--;
            inp.KeyHeld.Add(KeyboardKey.LeftShift);
            if (aimBuilding is { Alive: true } && shiftBurst % 3 == 0)
            {
                inp.Mouse = g.Camera.WorldToScreen(aimBuilding.Pos);
                inp.Pressed.Add(MouseButton.Right); Ev("shift-assign-click");
            }
        }
        // ---- 미션 배치 단계: 배치 가능한 부대를 집어 끌기 (구역 안 · 밖), 가끔 회전
        if (g.DeployPhase && leftFrames == 0 && rng.Next(4) == 0 && w.Mission is { } dm)
        {
            var forces = dm.Def.Forces.Where(f => w.IsDeployable(f)).ToList();
            if (forces.Count > 0 && w.ForceCentroid(forces[rng.Next(forces.Count)].Id) is Vector2 fc)
            {
                inp.Mouse = g.Camera.WorldToScreen(fc);
                inp.Pressed.Add(MouseButton.Left); inp.Down.Add(MouseButton.Left);
                leftFrames = rng.Next(3, 20);
                Ev("mission-deploy-drag");
                if (rng.Next(4) == 0) { inp.KeyPress.Add(KeyboardKey.R); Ev("mission-deploy-rotate"); }
            }
        }
        if (g.DeployPhase && leftFrames > 0) inp.Mouse += new Vector2(rng.Next(-25, 26), rng.Next(-25, 26));
        // ---- 겨냥 클릭: 등록된 버튼 중앙을 정확히 누른다 (작은 버튼 30% 예약)
        if (leftFrames == 0 && !g.DeployPhase && rng.Next(8) == 0) AimedClick();
        // ---- 클릭 (드래그 포함)
        if (leftFrames > 0)
        {
            leftFrames--;
            inp.Down.Add(MouseButton.Left);
            if (leftFrames == 0) { inp.Down.Remove(MouseButton.Left); inp.Released.Add(MouseButton.Left); }
        }
        else if (rng.Next(5) == 0)
        {
            inp.Pressed.Add(MouseButton.Left);
            leftFrames = rng.Next(0, 3) == 0 ? rng.Next(2, 25) : 1;
            inp.Down.Add(MouseButton.Left);
            if (leftFrames == 1) { leftFrames = 0; inp.Released.Add(MouseButton.Left); }   // 같은 프레임에 눌렀다 뗌 (빠른 클릭)
        }
        // 우클릭: 빠른 클릭 또는 끌기 (전선) — 뗄 때 이동 · 공격 · 전선 명령
        if (rightFrames > 0)
        {
            rightFrames--;
            inp.Down.Add(MouseButton.Right);
            if (rightFrames == 0) { inp.Down.Remove(MouseButton.Right); inp.Released.Add(MouseButton.Right); Ev("right-drag-end"); }
        }
        else if (rng.Next(9) == 0)
        {
            inp.Pressed.Add(MouseButton.Right);
            if (rng.Next(3) == 0) { rightFrames = rng.Next(2, 20); inp.Down.Add(MouseButton.Right); }
            else inp.Released.Add(MouseButton.Right);
        }
        if (rng.Next(25) == 0) inp.KeyHeld.Add(KeyboardKey.LeftAlt);   // Alt+숫자 = 중대 편성
        if (rng.Next(40) == 0) { inp.Down.Add(MouseButton.Middle); inp.Delta = new Vector2(rng.Next(-30, 30), rng.Next(-30, 30)); }
        if (rng.Next(12) == 0) inp.Wheel = rng.Next(-3, 4);
        // ---- 키
        if (g.CommandBox.IsOpen)
        {
            if (inp.Chars.Count == 0 && rng.Next(3) == 0)
            {
                foreach (char c in ConsoleLines[rng.Next(ConsoleLines.Length)]) inp.Chars.Enqueue(c);
                if (rng.Next(6) == 0) inp.Chars.Enqueue(rng.Next(0, 300));   // 이상한 글자
                inp.KeyPress.Add(rng.Next(8) == 0 ? KeyboardKey.Escape : KeyboardKey.Enter);
            }
            else if (rng.Next(10) == 0) inp.KeyPress.Add(rng.Next(3) == 0 ? KeyboardKey.Backspace : rng.Next(2) == 0 ? KeyboardKey.Up : KeyboardKey.Down);
        }
        else
        {
            if (rng.Next(3) == 0) inp.KeyPress.Add(Keys[rng.Next(Keys.Length)]);
            if (rng.Next(90) == 0) inp.KeyPress.Add(KeyboardKey.Enter);   // 콘솔 열기
            if (rng.Next(30) == 0) inp.KeyHeld.Add(KeyboardKey.LeftShift);
            if (rng.Next(30) == 0) inp.KeyHeld.Add(KeyboardKey.LeftControl);
        }
        if (g.Paused && rng.Next(40) == 0) inp.KeyPress.Add(KeyboardKey.F3);   // 일시정지에 갇히지 않게

        // ---- 게임 종료 화면: 잠깐 두드린 뒤 다시 시작
        if (w.Winner != 0)
        {
            if (++overFrames == 240) { inp.KeyPress.Add(KeyboardKey.Enter); Ev("restart-pressed"); }
        }
        // ---- 시나리오
        if (--nextScenario <= 0)
        {
            nextScenario = rng.Next(15, 70);
            Guard("scenario", () => Scenario(g));
        }
    }

    string Kind(Game g, List<Entity> s)
    {
        string own(Entity e) => e.Owner == g.Me ? "own" : e.Owner == 0 ? "neutral" : "foreign";
        if (s.Count > 1) return $"multi({(s.All(e => e is Unit) ? "units" : "mixed")})";
        if (s[0] is Building b) return $"B:{b.Def.Key}/{own(b)}";
        var u = (Unit)s[0];
        return $"U:{u.Def.Key}/{own(u)}";
    }

    string Describe(Game g)
    {
        var s = g.Selection;
        if (s.Count == 0) return "none";
        if (s.Count == 1) return s[0] is Building b ? $"B:{b.Def.Key}/o{b.Owner}" : $"U:{((Unit)s[0]).Def.Key}/o{s[0].Owner}";
        return $"multi{s.Count}";
    }

    Vector2 PickMouse(Game g, int w, int h)
    {
        var hud = g.Hud;
        Vector2 In(Rectangle r) => new(r.X + (float)rng.NextDouble() * r.Width, r.Y + (float)rng.NextDouble() * r.Height);
        switch (rng.Next(14))
        {
            case 0: return new Vector2(rng.Next(-5, w + 5), rng.Next(-5, h + 5));
            case 1: return In(hud.Info);
            case 2: return In(hud.Card);
            case 3: return In(hud.MinimapPanel);
            case 4: return In(hud.TopBar);
            case 5: return new Vector2(hud.Viewport.X + rng.Next(0, 420), hud.Bottom.Y - rng.Next(0, 40));   // 부대 막대
            case 6: return In(new Rectangle(hud.Info.X + 130, hud.Info.Y + hud.Info.Height - 80, 300, 80));   // 생산 대기열
            case 7: return In(new Rectangle(hud.Card.X, hud.Card.Y, hud.Card.Width, hud.Card.Height));
            case 8: return In(new Rectangle(w - 440, hud.Viewport.Y, 440, 400));   // 관전 현황판 · 외교 창 오른쪽
            case 9: return In(new Rectangle(w / 2f - 410, hud.Viewport.Y, 820, 600));   // 외교 창
            case 10 or 11:
            {
                // 화면에 보이는 엔티티 위 (선택 · 호버 경로)
                var w2 = g.World;
                int n = w2.Units.Count + w2.Buildings.Count;
                if (n == 0) break;
                int i = rng.Next(n);
                Entity e = i < w2.Units.Count ? w2.Units[i] : w2.Buildings[i - w2.Units.Count];
                if (rng.Next(3) == 0 && g.Selection.Count > 0) e = g.Selection[rng.Next(g.Selection.Count)];
                var p = g.Camera.WorldToScreen(e.Pos);
                if (p.X > 0 && p.Y > hud.Viewport.Y && p.X < w && p.Y < hud.Bottom.Y) return p;
                g.Camera.CenterOn(e.Pos);
                return g.Camera.WorldToScreen(e.Pos);
            }
        }
        return In(hud.Viewport);
    }

    // ---------------------------------------------------------------- 상황 만들기

    BuildingDef[] BDefs => Defs.Current.Buildings.Values.ToArray();

    void Setup(Game g)
    {
        var w = g.World;
        w.RecordCommands = false;
        // 모든 종류의 유닛 · 건물을 맵 곳곳 · 시점 근처에 깔아둔다 (소유: 나 / 적 / 중립 / 건설 중)
        var c = g.Camera.CenterWorld;
        int me = g.Me;
        var owners = new List<int> { 0 };
        for (int f = 1; f < Math.Min(w.Factions.Count, 5); f++) owners.Add(f);
        foreach (var ud in Defs.Current.Units.Values)
            foreach (int o in owners)
            {
                if (o == 0) continue;
                for (int i = 0; i < 3; i++) w.SpawnUnit(ud, o, c + new Vector2(rng.Next(-8, 9), rng.Next(-6, 7)));
            }
        foreach (var bd in BDefs)
            foreach (int o in owners)
            {
                if (o == 0 && !bd.Capturable) continue;
                PlaceNear(w, bd, o, c);
                PlaceNear(w, bd, o, c);
            }
    }

    Building? PlaceNear(World w, BuildingDef d, int owner, Vector2 near)
    {
        for (int i = 0; i < 400; i++)
        {
            int range = i < 200 ? 14 : Math.Min(w.Map.Width, w.Map.Height) / 2 - 2;
            int tx = (int)near.X + rng.Next(-range, range + 1), ty = (int)near.Y + rng.Next(-range, range + 1);
            if (tx < 1 || ty < 1 || tx + d.Size >= w.Map.Width || ty + d.Size >= w.Map.Height) continue;
            if (w.Map.CanPlace(tx, ty, d.Size)) return w.PlaceBuilding(d, owner, tx, ty);
        }
        return null;
    }

    T? Pick<T>(IList<T> l) where T : class => l.Count == 0 ? null : l[rng.Next(l.Count)];

    void Scenario(Game g)
    {
        var w = g.World;
        int me = g.Me;
        var own = w.Units.Where(u => u.Owner == me && me > 0).ToList();
        var ownB = w.Buildings.Where(b => b.Owner == me && me > 0).ToList();
        int k = rng.Next(23);
        switch (k)
        {
            case 0 or 1: if (Pick(w.Units) is Unit ru) g.SetSelection(new[] { ru }); break;                  // 아무 유닛 (적 · 중립 포함)
            case 2 or 3: if (Pick(w.Buildings) is Building rb0) g.SetSelection(new[] { rb0 }); break;
            case 4: if (Pick(ownB.Where(b => b.Def.City).ToList()) is Building pb) g.SetSelection(new[] { pb }); break;   // 내 도시
            case 5: if (Pick(own.Where(x => x.Def.CanOccupyTrench).ToList()) is Unit du) { w.Hold(me, new[] { du }); g.SetSelection(new[] { du }); } break;   // 보병 사수
            case 6: if (Pick(w.Buildings.Where(b => b.Def.Capturable).ToList()) is Building sb) g.SetSelection(new[] { sb }); break;
            case 7: g.SetSelection(own.OrderBy(_ => rng.Next()).Take(rng.Next(2, 70))); break;              // 다중 선택
            case 8:
                // 선택한 것 일부를 죽인다 (선택 중 사망)
                foreach (var e in g.Selection.ToList()) if (rng.Next(2) == 0) e.Hp = 0;
                break;
            case 9: // 점령 · 중립화 · 소유 변경: 선택 중인 점령 시설 (없으면 아무 시설) 에 적을 보내 게이지를 거의 비운다
            {
                var b = g.Selection.FirstOrDefault() as Building;
                if (b == null || !b.Def.Capturable) b = Pick(w.Buildings.Where(x => x.Def.Capturable).ToList());
                if (b == null) break;
                int enemy = rng.Next(1, w.Factions.Count);
                if (enemy == b.Owner) enemy = enemy % (w.Factions.Count - 1) + 1;
                for (int i = 0; i < 6; i++) w.SpawnUnit(Defs.Current.Units["infantry"], enemy, b.Pos + new Vector2(i % 3 - 1, i / 3 - 1) * 0.5f);
                b.CaptureProgress = Math.Min(b.CaptureProgress, 0.03f);
                break;
            }
            case 10: // (유닛 양도 — 외교 삭제로 없음) 선택한 유닛 정지
                if (me > 0) w.Stop(me, g.Selection.OfType<Unit>().Where(x => x.Owner == me));
                break;
            case 11: // 내 건물 / 적 건물 파괴 (사령부 포함)
                if (Pick(w.Buildings) is Building db) db.Hp = 0;
                break;
            case 12: // 선택한 유닛 사수
                if (me > 0) w.Hold(me, g.Selection.OfType<Unit>().Where(x => x.Owner == me));
                break;
            case 13: // 보급 마차 · 탄약 바닥
                foreach (var u in own.Where(_ => rng.Next(4) == 0)) { u.Ammo = 0; if (u.Def.IsSupplyWagon) u.Cargo = 0; }
                break;
            case 14: // 유닛 추가 (큰 무리)
                foreach (var ud in Defs.Current.Units.Values.OrderBy(_ => rng.Next()).Take(3))
                    for (int i = 0; i < 15; i++) w.SpawnUnit(ud, me > 0 ? me : rng.Next(1, w.Factions.Count), g.Camera.CenterWorld + new Vector2(rng.Next(-6, 7), rng.Next(-6, 7)));
                break;
            case 15: g.RunConsoleCommand(ConsoleLines[rng.Next(ConsoleLines.Length)]); break;   // 콘솔 명령 직접 (치트)
            case 16: // 이동 · 공격 명령으로 전투 유발
                if (me > 0 && own.Count > 0 && w.Units.Count > 0) w.AttackMove(me, own.Take(30), w.Units[rng.Next(w.Units.Count)].Pos);
                break;
            case 17: // 시점 이동 / 부대 지정
                if (Pick(w.Units) is Unit cu) g.Camera.CenterOn(cu.Pos);
                break;
            case 18: // 게임 끝내기: 한 번만. 승리(나 말고 전부 파괴) 또는 패배(내 것 전부 파괴)
                if (endedOnce || gameFrame < 900 || rng.Next(3) != 0) break;
                endedOnce = true;
                bool win = rng.Next(2) == 0;
                foreach (var e in w.Units.Cast<Entity>().Concat(w.Buildings)) if ((e.Owner == me) != win && e.Owner != 0 && !(me == 0)) e.Hp = 0;
                if (me == 0) { int keep = rng.Next(1, w.Factions.Count); foreach (var e in w.Units.Cast<Entity>().Concat(w.Buildings)) if (e.Owner != keep && e.Owner != 0) e.Hp = 0; }
                break;
            case 20: // 25기 초과 다중 선택 (쪽 넘김)
                g.SetSelection(own.OrderBy(_ => rng.Next()).Take(rng.Next(26, 130)));
                break;
            case 21: // 보급 마차 + 탄약 바닥 보병 무리 선택
            {
                if (me <= 0) break;
                var center = g.Camera.CenterWorld;
                var grp = new List<Unit>();
                for (int i = 0; i < 6; i++) { var u = w.SpawnUnit(Defs.Current.Units["infantry"], me, center + new Vector2(i % 3 - 1, i / 3)); u.Ammo = 0; grp.Add(u); }
                if (Defs.Current.Units.TryGetValue("supply_wagon", out var wd)) grp.Add(w.SpawnUnit(wd, me, center + new Vector2(0, 3)));
                g.SetSelection(grp);
                break;
            }
            case 22: // 사수 후 바로 이동 (해제)
                if (me > 0 && own.Count > 0) { var pick = own.Where(x => x.Def.CanOccupyTrench).Take(8).ToList(); w.Hold(me, pick); w.Move(me, pick, g.Camera.CenterWorld); }
                break;
            case 19: // 새 시설 / 건물 추가 배치
                if (Pick(BDefs) is BuildingDef nd) PlaceNear(w, nd, nd.Capturable ? rng.Next(0, Math.Min(w.Factions.Count, 5)) : rng.Next(1, Math.Min(w.Factions.Count, 5)), g.Camera.CenterWorld);
                break;
        }
    }
}
