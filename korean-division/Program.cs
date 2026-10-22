using Raylib_cs;
using KoreanDivision;
using KoreanDivision.Core;
using KoreanDivision.UI;
using KoreanDivision.Tests;

// 유닛 수 스트레스 테스트 (창 없음): KoreanDivision --stress [A|B|C|D|E|F] [k=1000,5000] [ticks=900] ... (src/Tests/StressTest.cs)
if (args.Length > 0 && args[0] == "--detcheck") return KoreanDivision.Tests.DetCheck.Run(args);   // a1: 결정성 체크섬 (tools/detcheck.ps1)
if (args.Length > 0 && args[0] == "--stress") return KoreanDivision.Tests.StressTest.Run(args[1..]);
if (args.Length > 0 && args[0] == "--mapload") return KoreanDivision.Tests.MapSizeLoad.Run(args[1..]);   // N13 지도 크기별 부하 측정
if (args.Length > 0 && args[0] == "--collide") return KoreanDivision.Tests.CollisionProbe.Run(args[1..]);   // 대규모 전투 이동 · 겹침 측정 (src/Tests/CollisionProbe.cs)
if (args.Length > 0 && args[0] == "--choke") return KoreanDivision.Tests.ChokeProbe.Run(args[1..]);   // 길목 통과 측정 (src/Tests/ChokeProbe.cs)
if (args.Length > 1 && args[0] == "--bunch") return KoreanDivision.Tests.BunchProbe.Run(args);   // 중대 뭉침 측정 (src/Tests/BunchProbe.cs, docs/BUNCHING_DIAG_2026-10-19.md)
if (args.Length > 0 && args[0] == "--snapbench") return KoreanDivision.Tests.SnapshotSelfTest.Bench(args);   // 16인 8000기 저장 / 불러오기 크기 · 시간 측정
if (args.Length > 2 && args[0] == "--snap-run") return KoreanDivision.Tests.SnapshotSelfTest.Cli(args);   // 저장 파일 이어 돌리기 (새 프로세스 검증용, SnapshotSelfTest)
if (args.Length > 0 && args[0] == "--snaptest") { try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { } KoreanDivision.Core.Defs.Load(); int bad = 0; KoreanDivision.Tests.SnapshotSelfTest.Run(new GameOptions(), (ok, n) => { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {n}"); if (!ok) bad++; }); return bad == 0 ? 0 : 1; }   // 저장 / 불러오기 검증만 (개발용)
// 밸런스 자동 대전 (창 없음): KoreanDivision --balance [suite] ... (src/Tests/BalanceTool.cs). --balance-worker 는 그 하위 프로세스.
if (args.Length > 0 && args[0] is "--balance" or "--balance-worker") return KoreanDivision.Tests.BalanceTool.Run(args);
// 미션 창 없이 끝까지 (모든 세력 AI): KoreanDivision --mission-run <id> [--seconds N] [--seed N] [--verify] (src/Tests/MissionSelfTest.cs)
if (args.Length > 1 && args[0] == "--mission-run") return KoreanDivision.Tests.MissionSelfTest.RunCli(args);
// 전술 관찰 (읽기 전용 텔레메트리): KoreanDivision --tactics-obs <id> [--seed N] [--seconds S] [--out f] (src/Tests/TacticsObs.cs)
if (args.Length > 1 && args[0] == "--tactics-obs") return KoreanDivision.Tests.TacticsObs.RunCli(args);

// 사용법:  KoreanDivision.exe [--map 100x100] [--seed N] [--ai tactical|tactical:<프로필>|standard|passive|rush|battle|none] [--nofog]
//                      [--shot out.png [--shot-frame N] [--sim 초]] [--replay 파일] [--verify-replay 파일] [--selftest]
const string Usage = "사용법: KoreanDivision [--players N] [--faction 번호|이름] [--map WxH|tiny|small|medium|large|huge|지도이름] [--seed N] [--ai 이름|none] [--nofog] [--weather clear|overcast|fog|rain|snow] [--time dawn|day|dusk|night] [--shot out.png] [--shot-frame N] [--sim 초] [--demo] [--exec 명령] [--theme kd1920|ww2|none] [--replay 파일] [--verify-replay 파일] [--no-replay] [--spectate [--view N]] [--mute] [--quick-battle [n]] [--battle \"종류:수,.. --vs \"종류:수,..\" [--battle-map flat|terrain|city] [--battle-ai-vs-ai] [--battle-distance N]] [--balance [suite]] [--selftest]  (인자 없이 실행하면 시작 화면)";
var opt = new GameOptions();
bool selfTest = false, mapGiven = false, noVsync = false, fuzzMenu = false;
double fuzzSeconds = 0;   // > 0 이면 UI 퍼즈 모드 (src/Tests/UiFuzz.cs)
string? factionArg = null, verifyReplay = null, menuShot = null, menuPage = null;
string? battleA = null, battleB = null, battleMap = "flat";   // 교전 테스트: --battle "종류:수,..." --vs "..." [--battle-map flat|terrain|city] [--battle-ai-vs-ai] [--battle-distance N]
bool battleAiVsAi = false;
int battleDistance = 40;
bool aiGiven = false;   // --ai 를 명시했는가 (교전 테스트도 그 AI 로 — 비교용)
bool gameFlag = false;   // 게임을 바로 시작하는 인자가 하나라도 있으면 시작 화면을 건너뛴다
try
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] is not ("--novsync" or "--menu-shot" or "--menu-page" or "--theme" or "--uifuzz" or "--fuzz-menu" or "--mute" or "--dev-menu")) gameFlag = true;   // 테마만 바꿔도 시작 화면은 그대로
        switch (args[i])
        {
            case "--map":
                // "160x120" 같은 크기면 생성 맵, 아니면 고정 지도 이름 (assets/maps/<이름>.json)
                string mapArg = args[++i].ToLowerInvariant();
                if (Config.MapTierByName(mapArg) is int tier) { opt.MapWidth = opt.MapHeight = tier; mapGiven = true; }   // 지도 크기 5단계 이름 (tiny · small · medium · large · huge)
                else if (System.Text.RegularExpressions.Regex.IsMatch(mapArg, @"^\d+(x\d+)?$"))
                {
                    var parts = mapArg.Split('x');
                    opt.MapWidth = int.Parse(parts[0]);
                    opt.MapHeight = int.Parse(parts.Length > 1 ? parts[1] : parts[0]);
                    mapGiven = true;
                }
                else
                {
                    if (!File.Exists(MapFile.PathOf(mapArg)))
                        throw new ArgumentException($"지도 '{mapArg}' 없음 (가능: {string.Join(", ", MapFile.Available())})");
                    opt.MapId = mapArg;
                }
                break;
            case "--faction": factionArg = args[++i]; break;   // 내가 조작할 세력: 번호(1~) 또는 지도의 영문 이름
            case "--players": opt.Players = int.Parse(args[++i]); break;
            case "--seed": opt.Seed = int.Parse(args[++i]); break;
            case "--ai": opt.Ai = args[++i].ToLowerInvariant(); aiGiven = true; break;
            case "--nofog": opt.NoFog = true; break;
            case "--difficulty": opt.Difficulty = args[++i].ToLowerInvariant(); if (!KoreanDivision.Core.DifficultyDefs.Current.IsKnown(opt.Difficulty)) throw new ArgumentException("--difficulty easy|normal|hard"); break;
            case "--weather": opt.Weather = args[++i]; KoreanDivision.Core.WeatherState.ParseKind(opt.Weather); break;   // clear|overcast|fog|rain|snow (테스트용)
            case "--time": opt.TimeOfDay = args[++i]; KoreanDivision.Core.WeatherState.ParseTime(opt.TimeOfDay); break;   // dawn|day|dusk|night
            case "--shot": opt.ShotPath = Path.GetFullPath(args[++i]); if (Environment.GetEnvironmentVariable("KD_SHOT_NOINPUT") == "1") KoreanDivision.UI.Input.Source = new KoreanDivision.UI.IdleInput(); break;
            case "--shot-frame": opt.ShotFrame = int.Parse(args[++i]); break;
            case "--sim": opt.ShotSimSeconds = float.Parse(args[++i]); break;
            case "--demo": opt.Demo = true; break;
            case "--exec": opt.Exec.Add(args[++i]); break;
            case "--theme": opt.Theme = args[++i].ToLowerInvariant(); break;
            case "--novsync": noVsync = true; break;   // 성능 측정용: 화면 동기화 · FPS 상한 해제
            case "--mute": KoreanDivision.UI.Sfx.Muted = true; break;   // 소리 끄기 (게임 중 N 키로 켜고 끔)
            case "--replay": opt.ReplayPath = Path.GetFullPath(args[++i]); break;
            case "--verify-replay": verifyReplay = Path.GetFullPath(args[++i]); break;
            case "--no-replay": opt.SaveReplay = false; break;
            case "--spectate": opt.Spectate = true; break;
            case "--view": opt.ViewFaction = int.Parse(args[++i]); break;   // 관전 시작 시점 (0 = 전체)
            case "--dev-menu": KoreanDivision.UI.MainMenu.DevMenu = true; break;   // 숨긴 메뉴 항목(플레이 / 관전 인원) 표시 (T45)
            case "--menu-shot": menuShot = Path.GetFullPath(args[++i]); break;   // 시작 화면 스크린샷 (확인용)
            case "--menu-page": menuPage = args[++i].ToLowerInvariant(); break;   // main | setup | spectate | replays
            case "--selftest": selfTest = true; break;
            case "--uifuzz":   // UI 크래시 퍼즈: --uifuzz [초] (기본 120). 지도를 안 주면 corners4 + AI, 리플레이는 저장 안 함
                fuzzSeconds = i + 1 < args.Length && double.TryParse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture, out double fs) ? (++i, fs).fs : 120;
                break;
            case "--fuzz-menu": fuzzMenu = true; break;   // 퍼즈를 시작 화면부터 (메뉴 → 게임 → 메뉴 ...)
            case "--quick-battle":   // 빠른 교전: 빈 평지 보병 n 대 AI 보병 n (기본 500, 예: --quick-battle 200)
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int qn)) { battleA = battleB = MainMenu.QuickBattleSideFor(qn); i++; }
                else battleA = battleB = MainMenu.QuickBattleSide;
                battleMap = "flat"; break;
            case "--battle": battleA = args[++i]; break;
            case "--vs": battleB = args[++i]; break;
            case "--battle-map": battleMap = args[++i].ToLowerInvariant(); break;
            case "--battle-ai-vs-ai": battleAiVsAi = true; break;
            case "--battle-distance": battleDistance = int.Parse(args[++i]); break;
            case "--mission": opt.MissionId = args[++i]; break;   // 미션 (assets/missions/<id>.json) — 배치 단계부터
            case "--mission-start": opt.MissionSkipDeploy = true; break;   // 미션 배치 단계 건너뛰기
            case "--probe": break;
            default: throw new ArgumentException($"알 수 없는 인자: {args[i]}");
        }
    }
    if (fuzzSeconds > 0)
    {
        if (!mapGiven && opt.MapId == null && opt.MissionId == null) opt.MapId = "corners4";
        opt.SaveReplay = false;
        opt.NoResultsFile = true;
        if (!fuzzMenu) gameFlag = true;
    }
    opt.Players = Math.Clamp(opt.Players, 2, Config.MaxPlayers);
    if (!mapGiven) opt.MapWidth = opt.MapHeight = Config.MapSizeFor(opt.Players);
    opt.MapWidth = Math.Clamp(opt.MapWidth, Config.MinMapSize, Config.MaxMapSize);
    opt.MapHeight = Math.Clamp(opt.MapHeight, Config.MinMapSize, Config.MaxMapSize);
    if (factionArg != null) FactionChoice.Resolve(opt, factionArg);
    if (battleA != null || battleB != null)
    {
        string battleAi = aiGiven && opt.Ai != "none" ? opt.Ai : AiRegistry.Default;   // 기본 = 전술 AI, --ai battle 이면 옛 교전 AI
        opt.Battle = new BattleSetup { SideA = battleA ?? battleB!, SideB = battleB ?? battleA!, Map = battleMap!, Distance = battleDistance, AiVsAi = battleAiVsAi, AiA = battleAi, AiB = battleAi };
        opt.Spectate = battleAiVsAi;
        opt.Ai = battleAi;
        opt.Faction = 1;
        opt.Players = 2;
        opt.MapId = null;
    }
    if (opt.MissionId != null && !File.Exists(MissionDef.PathOf(opt.MissionId)))
        throw new ArgumentException($"미션 '{opt.MissionId}' 없음 (가능: {string.Join(", ", MissionDef.Available())})");
        if (opt.Ai != "none" && !AiRegistry.IsKnown(opt.Ai))
        throw new ArgumentException($"알 수 없는 AI: {opt.Ai} (가능: {string.Join(", ", AiRegistry.All.Keys)}, none)");
}
catch (Exception e) when (e is FormatException or IndexOutOfRangeException or ArgumentException)
{
    Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine(Usage);
    return 1;
}

var defs = Defs.Load();
if (opt.Battle != null)
{
    try { opt.Battle.Validate(defs); }
    catch (ArgumentException e) { Console.Error.WriteLine(e.Message); Console.Error.WriteLine(Usage); return 1; }
}
if (args.Contains("--probe") && Environment.GetEnvironmentVariable("KD_PROBE_SNAP") == "1") { KoreanDivision.Tests.DesyncProbe.RunSnap(opt.Seed, int.TryParse(Environment.GetEnvironmentVariable("KD_PROBE_PRE"), out var ppre) ? ppre : 3600); return 0; }
if (args.Contains("--probe")) { KoreanDivision.Tests.DesyncProbe.Run(opt.Seed); return 0; }
if (selfTest) return SelfTest.Run(opt);
if (verifyReplay != null)
{
    // 창 없이 리플레이를 처음부터 다시 돌려 기록된 체크섬과 비교
    try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { }
    var rep = ReplayFile.Load(verifyReplay);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    long bad = rep.Verify(defs, out var report);
    Console.WriteLine($"{(bad < 0 ? "OK" : "MISMATCH")}: {report}  ({sw.Elapsed.TotalSeconds:0.0}초)");
    return bad < 0 ? 0 : 2;
}

Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | (noVsync ? 0 : ConfigFlags.VSyncHint));
Raylib.InitWindow(Config.ScreenWidth, Config.ScreenHeight, $"{Config.Title} {Config.Version}");
Raylib.SetWindowMinSize(Config.MinScreenWidth, Config.MinScreenHeight);
Raylib.SetExitKey(KeyboardKey.Null);   // Esc 는 게임 내 취소 키로 사용
Raylib.SetTargetFPS(noVsync ? 0 : Config.MaxFps);
Ui.LoadFonts(defs.AllText().Concat(MissionDef.AllText()));   // 미션 파일의 제목 · 브리핑 · 목표 글자도 아틀라스에
KoreanDivision.Render.Theme.Use(opt.Theme);   // 이미지 세트 (assets/themes/<id>, "none" = 도형 그림)
Sfx.Init(opt.Theme == "none" ? "ww2" : opt.Theme);   // 효과음 (assets/sounds/<id>)

UiFuzz? fuzz = fuzzSeconds > 0 ? new UiFuzz(fuzzSeconds, opt.Seed) : null;
bool running = true;
double updMs = 0, drawMs = 0;
int measured = 0;
MainMenu? menu = null;
GameOptions? next = gameFlag ? opt : null;   // null = 시작 화면에서 고른다
while (running)
{
    if (next == null)
    {
        menu ??= new MainMenu(menuPage);
        int mframe = 0;
        while (next == null)
        {
            if (Raylib.WindowShouldClose() || (menu.ExitRequested && fuzz == null) || (fuzz?.Finished ?? false)) { running = false; break; }
            bool mshoot = menuShot != null && mframe == 5;
            if (fuzz != null)
            {
                menu.ExitRequested = false;
                fuzz.MenuFrame(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
                fuzz.Guard("menu", () => menu.Frame(null));
            }
            else menu.Frame(mshoot ? menuShot : null);
            if (mshoot) { running = false; break; }
            next = menu.Chosen;
            menu.Chosen = null;
            mframe++;
        }
        if (!running) break;
    }

    var cur = next!;
    next = null;
    Game? created = null;
    string startError = "";
    try { created = new Game(cur); }
    catch (Exception e) when (cur.FromMenu && e is IOException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
    {
        // 깨졌거나 옛 형식인 리플레이 · 없는 지도 등: 게임 전체가 죽지 않고 메뉴로 돌아간다
        Console.Error.WriteLine($"게임을 시작할 수 없음: {e.Message}");
        startError = e.Message;
    }
    if (created == null) { menu?.ShowMain(cur.FromMenu ? string.Format(S.StartFailed, startError) : null); continue; }
    cur.Loaded = null;   // 월드는 게임이 가져갔다 (다시 시작 · 메뉴 복귀가 옛 저장 월드를 다시 쓰지 않게)
    using var game = created;
    int frame = 0;
    while (true)
    {
        if (Raylib.WindowShouldClose() || (fuzz?.Finished ?? false)) { game.SaveReplay(); running = false; break; }
        if (game.PendingLoad != null)
        {
            // 불러오기 (F7 · 명령어 load · 상단 버튼): 지금 게임을 닫고 (리플레이 저장) 읽어 둔 월드로 새 게임을 연다
            game.SaveReplay();
            next = SaveManager.OptionsFor(game.PendingLoad, cur.FromMenu);
            break;
        }
        if (game.QuitRequested && fuzz != null && !cur.FromMenu) { next = cur; break; }   // 퍼즈: 종료 버튼은 다시 시작으로
        if (game.QuitRequested)
        {
            game.SaveReplay();
            if (cur.FromMenu) menu?.ShowMain();   // 메뉴에서 시작했으면 메뉴로, 아니면 종료
            else running = false;
            break;
        }
        if (game.RestartRequested) { game.SaveReplay(); if (cur.Battle != null) cur.Seed++; next = cur; break; }   // 교전 테스트: 같은 구성, 다음 시드 (피해 · 지형 무늬가 달라짐)
        if (game.WatchAgainRequested)
        {
            // 방금 관전한 경기를 리플레이로 다시 보기 (저장 안 됐으면 새로 시작)
            string? saved = game.SaveReplay();
            next = saved != null ? new GameOptions { ReplayPath = saved, FromMenu = cur.FromMenu, SaveReplay = false } : cur;
            break;
        }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        bool shoot = cur.ShotPath != null && frame == cur.ShotFrame;
        if (cur.ShotPath != null && frame == 1 && float.TryParse(Environment.GetEnvironmentVariable("KD_SHOT_ZOOM"), System.Globalization.CultureInfo.InvariantCulture, out float shotZoom)) game.Camera.SetZoom(shotZoom);   // 확인용: 지정 배율로 촬영
        if (cur.ShotPath != null && Environment.GetEnvironmentVariable("KD_SHOT_FOLLOW") == "1")   // 확인용: 카메라를 내 유닛 무게중심 (KD_SHOT_FOLLOW_KEY=종류 키면 그 종류만) 에 고정
        {
            string? only = Environment.GetEnvironmentVariable("KD_SHOT_FOLLOW_KEY");
            System.Numerics.Vector2 sum = default; int cnt = 0;
            foreach (var fu in game.World.Units) if (fu.Alive && fu.Owner == game.Me && (only == null || fu.Def.Key == only)) { sum += fu.Pos; cnt++; }
            if (cnt > 0) game.Camera.CenterOn(sum / cnt);
        }
        if (fuzz != null)
        {
            fuzz.GameFrame(game);
            fuzz.Guard("update", () => game.Update(1f / 30));
        }
        else game.Update(noVsync && cur.ShotPath != null ? 1f / 60 : MathF.Min(Raylib.GetFrameTime(), 0.1f));   // --shot + --novsync: 프레임 시간이 ≈0 이라 시뮬이 안 가므로 고정 1/60
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (fuzz != null)
        {
            if (!fuzz.Guard("draw", () => game.Draw(null)))
                fuzz.Guard("draw-cleanup", () => { Raylib.EndScissorMode(); Raylib.EndMode2D(); Raylib.EndDrawing(); });   // 그리다 중단된 상태 정리
        }
        else game.Draw(shoot ? cur.ShotPath : null);
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        game.FrameUpdateMs = game.FrameUpdateMs * 0.9 + System.Diagnostics.Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds * 0.1;
        game.FrameDrawMs = game.FrameDrawMs * 0.9 + System.Diagnostics.Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds * 0.1;
        if (frame >= 60) { updMs += System.Diagnostics.Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds; drawMs += System.Diagnostics.Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds; measured++; }
        if (shoot && measured > 0) Console.Error.WriteLine($"[frame] {measured}프레임: Update {updMs / measured:0.00}ms, Draw(+vsync 대기) {drawMs / measured:0.00}ms, FPS {Raylib.GetFPS()}");
        if (shoot) { game.SaveReplay(); running = false; break; }
        frame++;
    }
}

Sfx.Shutdown();
Ui.Unload();
Raylib.CloseWindow();
return fuzz?.Report() ?? 0;
