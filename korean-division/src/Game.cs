using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.Render;
using KoreanDivision.UI;

namespace KoreanDivision;

public sealed class GameOptions
{
    public int Players = Config.DefaultPlayers;
    public string Theme = "kd1920";  // 이미지 세트 (kd1920 = 새 LOD 시트, 없는 항목은 ww2 상속) (시대 배경별 교체: assets/themes/<id>), "none" = 도형 그림
    public string? MapId;            // 고정 지도 (assets/maps/<id>.json). 지정하면 인원 · 크기는 지도가 정한다
    public int MapWidth = Config.MapSizeFor(Config.DefaultPlayers);   // --map 을 안 주면 인원수에 맞춰 자동
    public int MapHeight = Config.MapSizeFor(Config.DefaultPlayers);
    public int Seed = Config.DefaultSeed;
    public string Ai = AiRegistry.Default;   // 다른 세력(2..N)의 AI 이름 (AiRegistry, 기본 tactical), "none" = AI 없음
    public bool NoFog;               // 전장의 안개 끄기 (테스트용)
    public string? Difficulty;       // --difficulty easy|normal|hard (difficulty.json, Comms.cs; null = 캠페인 미션이면 캠페인 값, 아니면 기본)
    public string? Weather;          // --weather clear|overcast|fog|rain|snow (지도 설정을 덮어씀, 테스트용)
    public string? TimeOfDay;        // --time dawn|day|dusk|night
    public string? ShotPath;         // 지정 시 ShotFrame 번째 프레임을 PNG 로 저장하고 종료
    public int ShotFrame = 120;
    public float ShotSimSeconds;     // 시작 전에 미리 돌릴 시뮬레이션 시간 (스크린샷 테스트용)
    public bool Demo;                // 시연용: 본진 앞에서 보병 교전 + 병영 건설
    public string? ReplayPath;       // 지정 시 리플레이 재생 (조작 불가, 안개 없음)
    public bool SaveReplay = true;   // 게임을 마치면 replays/ 에 리플레이 자동 저장
    public int Faction = Config.HumanPlayer;   // 사람이 조작하는 세력 (1..N, 나머지는 AI). 관전 · 리플레이에서는 무시
    public bool Spectate;            // 관전 모드: 모든 세력(1..N)을 AI 가 조작, 사람은 관전자(세력 0)
    public int ViewFaction;          // 관전 시작 시점 (0 = 전체, N = 세력 N 의 시야)
    public bool FromMenu;            // 시작 화면에서 시작함 (끝나면 메뉴로 복귀)
    public BattleSetup? Battle;      // 교전 테스트 (유닛만 · 건물 없음). 지정하면 지도 · 인원 설정은 무시
    public LoadedSnapshot? Loaded;   // 저장 파일에서 불러온 월드 (지정하면 맵 · 인원 · AI 설정은 그 월드가 정한다)
    public readonly List<string> Exec = new();   // 시작 시 실행할 명령어 (--exec, 자동 확인용)
    public string? MissionId;        // 미션 (assets/missions/<id>.json). 사람 세력 = 미션의 player, 배치 단계부터 시작
    public bool MissionSkipDeploy;   // 배치 단계 건너뛰기 (--mission-start · 테스트)
    public int? MissionSeed;         // 미션 시드 (없으면 미션 파일의 seed)
    public bool NoResultsFile;       // 결과 json 을 쓰지 않음 (퍼즈 · 테스트)
}

/// <summary>입력 처리 · 선택 · 명령 하달 · 화면 그리기를 묶는 최상위 객체.</summary>
public sealed partial class Game : IDisposable
{
    /// <summary>--weather / --time: 틱 0 전에 날씨 · 시간대를 덮어쓴다 (지도 일정은 유지). 리플레이 머리말에 시작 날씨로 기록된다.</summary>
    static void ApplyWeatherOption(World w, GameOptions opt)
    {
        ApplyDifficultyOption(w, opt);   // 난이도 (GameComms.cs)
        if (opt.Weather == null && opt.TimeOfDay == null) return;
        w.SetupWeather(opt.Weather != null ? WeatherState.ParseKind(opt.Weather) : w.Weather.Kind,
                       opt.TimeOfDay != null ? WeatherState.ParseTime(opt.TimeOfDay) : w.Weather.Time);
    }


    public readonly World World;
    readonly ReplayFile? replay;
    readonly bool saveReplay;
    readonly bool spectate;
    readonly string aiName;
    public bool IsReplay => replay != null;
    public bool IsSpectate => spectate;
    /// <summary>관전자 시점 (리플레이 · AI 관전): 명령 불가.</summary>
    public bool Observer => replay != null || spectate;
    /// <summary>시작 화면에서 시작한 게임인가 (종료하면 메뉴로).</summary>
    public readonly bool FromMenu;
    /// <summary>다시 보기 요청 (관전 후 방금 경기의 리플레이 / 리플레이 처음부터).</summary>
    public bool WatchAgainRequested { get; private set; }
    /// <summary>화면에 보여줄 세력 시점 (0 = 전체). 관전자만 바꿀 수 있다.</summary>
    public int View { get; private set; }
    public readonly SpectatorPanel Spectator;
    public readonly RtsCamera Camera;
    public readonly Hud Hud;
    public readonly FogRenderer FogRenderer;
    readonly WeatherFx weatherFx = new();
    int weatherSeen = -1;   // 마지막으로 알린 World.Weather.Changes (환경음 · 알림)
    public readonly CommandConsole CommandBox = new();
    public readonly HelpOverlay HelpBox = new();   // T38b: F1 도움말 (Bindings 단일 출처, 화면 전용)
    public bool ShowPerf { get; private set; }
    /// <summary>프레임 시간 (Program 이 채움, fps 명령으로 표시).</summary>
    public double FrameUpdateMs, FrameDrawMs;
    bool[]? seenDefeated;   // 표시용: 패배 알림을 이미 띄운 세력
    readonly MapRenderer mapRenderer;
    /// <summary>화면의 주인 세력. 리플레이 · 관전은 0 (관전자: 전체가 보이고 조작 불가).</summary>
    public int Me => Observer ? 0 : humanFaction;
    readonly int humanFaction;
    /// <summary>"(플레이어)" 표시 대상 세력: 직접 플레이면 Me, 리플레이면 기록된 세력, 관전이면 0.</summary>
    public int HumanSeat { get; }

    public readonly List<Entity> Selection = new();
    /// <summary>N79a: 지점 포격 (T) 의 종류 선택 (UI 상태 — 명령에 실려 시뮬로 간다).</summary>
    public GroundFireKind FireKindSel { get; private set; } = GroundFireKind.Sustained;
    public string? PendingCommand { get; private set; }     // move / quickstep / attack / repair / rally
    /// <summary>커맨드 카드 탭 (명령 · 전투).</summary>
    public CardTab Tab { get; private set; }
    FormType lastSimDefaultForm = FormType.Free;
    public bool ShowGrid { get; private set; }
    bool losShow, losCursor; int frameCounter;   // N75 사선 표시
    public bool Paused { get; private set; }
    public float Speed => Speeds[speedIndex];
    static readonly float[] PlaySpeeds = { 0.5f, 1f, 1.5f, 2f, 3f };
    static readonly float[] SpectateSpeeds = { 0.5f, 1f, 1.5f, 2f, 3f, 4f, 6f, 8f };
    float[] Speeds => spectate ? SpectateSpeeds : PlaySpeeds;
    /// <summary>선택 가능한 배속 목록 (관전 = 최대 8배) · 현재 칸 · 선택 (관전 패널의 배속 버튼).</summary>
    public IReadOnlyList<float> SpeedOptions => Speeds;
    public int SpeedIndex => speedIndex;
    public void SetSpeedIndex(int i) => speedIndex = Math.Clamp(i, 0, Speeds.Length - 1);
    int speedIndex = 1;
    public bool QuitRequested { get; private set; }
    public bool RestartRequested { get; private set; }
    public float LastStepMs { get; private set; }

    /// <summary>기본 대형 (중대 밖 유닛 · 우클릭 드래그 전선에 쓴다). 화면 쪽 상태 — 시뮬 쪽은 CombatSetting.DefaultFormation 과 맞춘다.</summary>
    public FormType DefaultType = FormType.Free;
    public FormOrder DefaultOrder = FormOrder.Mixed;
    /// <summary>대대 목록 패널 (0 키).</summary>
    public bool ShowBatList;
    int lastBattalionId;                    // 마지막으로 부른 대대
    readonly Dictionary<int, int> battalionRegiment = new();

    /// <summary>대대의 소속 연대 번호 (1~9, 기본 = (대대번호-1)/3 + 1).</summary>
    public int RegimentOf(Battalion r) =>
        battalionRegiment.TryGetValue(r.Id, out int reg) && reg is >= 1 and <= 9 ? reg : (r.Number > 0 ? (r.Number - 1) / 3 + 1 : 1);

    public void SetBattalionRegiment(Battalion r, int reg)
    {
        if (reg is >= 1 and <= 9)
        {
            battalionRegiment[r.Id] = reg;
            ShowNotice(string.Format(S.RegimentAssigned, r.Number > 0 ? string.Format(S.BattalionName, r.Number) : S.BattalionNoNumber, reg));
        }
    }

    /// <summary>연대 n 선택 (소속 대대들 전체 선택). 같은 번호를 0.35초 안에 다시 누르면 카메라를 연대로 옮긴다.</summary>
    public void RecallRegiment(int n)
    {
        if (Observer) return;
        var bats = World.BattalionsOf(Me).Where(r => RegimentOf(r) == n && r.Companies.Count > 0).ToList();
        var units = bats.SelectMany(r => r.Companies).SelectMany(c => c.Members).Where(u => u.Alive).ToList();
        if (units.Count == 0 && World.BattalionByNumber(Me, n) is { } fallback)
        {
            RecallBattalion(n);
            return;
        }
        if (units.Count == 0) return;
        bool again = lastGroupKey == n && Now - lastGroupTime < Config.GroupDoubleTapTime;
        lastGroupKey = n;
        lastGroupTime = Now;
        SetSelection(units);
        if (again && Selection.Count > 0) CenterOnSelection();
    }
    Company? cardPress;                    // 중대 카드를 누른 채 (클릭인지 끌기인지는 뗄 때 정한다)
    Vector2 cardPressPos;
    /// <summary>끌고 있는 중대 카드 (HUD 가 잔상을 그린다). 문턱을 넘기 전에는 null.</summary>
    public Company? DragCard => cardPress != null && Input.IsMouseButtonDown(MouseButton.Left) && Vector2.Distance(mouse, cardPressPos) >= Config.DragThreshold ? cardPress : null;
    Vector2? rightStartScreen, rightStartWorld;    // 우클릭 누른 자리 (전선 끌기)
    readonly List<(Vector2 Pos, double Time, Color Color)> markers = new();
    /// <summary>명령 순간의 이동선: 유닛들 위치 → 목표 (1초 동안 보이다 사라짐).</summary>
    readonly List<(Vector2[] From, Vector2 To, double Time, Color Color)> orderLines = new();
    Vector2? dragStartWorld;
    bool minimapDrag;
    double lastClickTime, lastGroupTime, lastAlertTime = -100;
    int lastClickId = -1, lastGroupKey = -1;
    long lastAttackTick = -1;
    (Vector2 Pos, double Time)? alert;
    string? notice;
    string? loadWarning;
    long autosaveSlot;
    /// <summary>불러오기를 요청해 이미 읽어 둔 저장 (Program 이 받아 이 게임을 닫고 그 월드로 새 게임을 연다).</summary>
    public LoadedSnapshot? PendingLoad { get; private set; }
    double noticeUntil;
    float accumulator;
    int screenW, screenH;
    MouseCursor cursor = MouseCursor.Default;

    Vector2 mouse, mouseWorld;
    bool mouseInViewport;
    Entity? hover;

    public Game(GameOptions opt)
    {
        FromMenu = opt.FromMenu;
        var missionDef = opt.MissionId != null && opt.Loaded == null && opt.ReplayPath == null ? MissionDef.Load(opt.MissionId) : null;   // 미션: 사람 세력 = 미션의 player
        if (missionDef != null) { opt.Faction = missionDef.Player; if (opt.Ai == "standard") opt.Ai = "mission"; }
        humanFaction = Math.Max(1, opt.Faction);
        spectate = (opt.Spectate || missionDef?.Spectate == true) && opt.ReplayPath == null;
        aiName = opt.Ai == "none" ? AiRegistry.Default : opt.Ai;
        if (opt.Loaded != null)
        {
            // 저장 파일에서: 월드는 이미 복원돼 있다 (AI 컨트롤러 · 안개 설정 · 세력 이름 포함). 사람 세력 · 관전 · AI 이름은 옵션이 머리말에서 받아 왔다.
            World = opt.Loaded.World;
            if (humanFaction >= World.Factions.Count) throw new ArgumentException($"세력 {humanFaction} 없음 (1~{World.Factions.Count - 1})");
            loadWarning = opt.Loaded.Warnings.Count > 0 ? string.Join(" / ", opt.Loaded.Warnings) : null;
        }
        else if (opt.ReplayPath != null)
        {
            replay = ReplayFile.Load(opt.ReplayPath);
            World = replay.CreateWorld(Defs.Current);   // 세력 이름 · 색은 지도 파일에서 (CreateWorld 가 적용)
            // 안개 설정은 기록대로 (시뮬레이션 재현) — 화면은 관전자(세력 0) 시점이라 전체가 보인다
        }
        else if (missionDef != null)
        {
            World = CreateMissionGameWorld(opt, missionDef, humanFaction, spectate);   // GameMission.cs
            deployDone = opt.MissionSkipDeploy;
            noResultsFile = opt.NoResultsFile;
        }
        else if (opt.Battle != null)
        {
            World = opt.Battle.CreateWorld(Defs.Current, opt.Seed);
            World.Fog.Enabled = !opt.NoFog;
            ApplyWeatherOption(World, opt);
            for (int f = 1; f <= 2; f++)
                if ((spectate || f != humanFaction) && AiRegistry.Create(f == 1 ? opt.Battle.AiA : opt.Battle.AiB) is IAiController bai)
                {
                    World.Controllers.Add((f, bai));
                    World.Faction(f).IsAi = true;
                }
                else if (!spectate) AutoFormBattleCompanies(World, f);   // 교전 테스트: 플레이어 보병도 중대로 (AI 와 같은 규칙)
        }
        else
        {
            var map = opt.MapId != null ? MapFile.Load(opt.MapId, opt.Seed) : GameMap.Generate(opt.MapWidth, opt.MapHeight, opt.Seed, opt.Players);
            World = World.CreateStandard(map, Defs.Current);
            MapFile.ApplyFactions(World);
            if (humanFaction >= World.Factions.Count) throw new ArgumentException($"세력 {humanFaction} 없음 (1~{World.Factions.Count - 1})");
            World.Fog.Enabled = !opt.NoFog;
            ApplyWeatherOption(World, opt);
            if (opt.Ai != "none" || spectate) AttachControllers(World, humanFaction, spectate, aiName);
        }
        saveReplay = opt.SaveReplay && replay == null && !opt.Demo;
        HumanSeat = replay != null ? replay.HumanFaction : spectate ? 0 : Me;
        Palette.UseMapColors(MapFile.FactionsOf(World.Map));
        Camera = new RtsCamera(World.Map.Width, World.Map.Height);
        Hud = new Hud(this, World.Map);
        FogRenderer = new FogRenderer(World);
        Spectator = new SpectatorPanel(this);
        View = Observer && opt.ViewFaction > 0 && opt.ViewFaction < World.Factions.Count ? opt.ViewFaction : 0;
        mapRenderer = new MapRenderer(World.Map);
        Layout();

        var hq = World.Mission != null ? null : World.Buildings.FirstOrDefault(b => b.Owner == Math.Max(Me, 1) && b.Def.Headquarters)
                 ?? World.Buildings.FirstOrDefault(b => b.Owner == Math.Max(Me, 1) && b.Def.City);   // 대도시 (없으면 첫 소도시). 미션은 내 병력 중심
        Camera.CenterOn(hq?.Pos ?? CenterOfOwnArmy());
        if (Environment.GetEnvironmentVariable("KD_CAMPOS") is { } cp && cp.Split(',') is var cc && cc.Length >= 2
            && float.TryParse(cc[0], System.Globalization.CultureInfo.InvariantCulture, out float cpx) && float.TryParse(cc[1], System.Globalization.CultureInfo.InvariantCulture, out float cpy))
            Camera.CenterOn(new Vector2(cpx, cpy));   // 스크린샷용: KD_CAMPOS=<타일x>,<타일y>
        if (float.TryParse(Environment.GetEnvironmentVariable("KD_CAMZOOM"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float czm)) Camera.SetZoom(czm);   // 스크린샷용 줌
        if (World.BattleMode && Spectator.Visible) Spectator.Toggle();   // 교전 테스트: 세력 현황판 대신 교전 현황 패널
        if (hq != null && Me > 0) SetSelection(new[] { hq });
        if (opt.Demo && hq != null)
        {
            World.RecordCommands = false;   // 시연 배치는 명령 밖에서 만들어 리플레이로 재현 불가
            var scene = Environment.GetEnvironmentVariable("KD_SCENE");
            if (scene == "mgaim") SetupMgAimScene(hq); else if (scene == "allunits") SetupAllUnitsScene(hq); else if (scene == "ringshot") SetupRingShotScene(hq); else if (scene == "pronemove") SetupProneMoveScene(hq); else SetupDemo(hq);
        }

        Sfx.Silent = World.BattleMode && Environment.GetEnvironmentVariable("KD_SFX_BATTLE") != "1";   // 교전 테스트는 무음 (ROADMAP §2-4g E; KD_SFX_BATTLE=1 = 소리 성능 측정용 해제)
        autosaveSlot = World.Tick / SaveManager.AutosaveTicks;   // 불러온 시점 이전의 자동 저장 시각은 건너뛴다
        if (loadWarning != null) ShowNotice(string.Format(S.LoadedWarn, loadWarning));
        foreach (var cmd in opt.Exec) RunConsoleCommand(cmd);
        for (int i = 0; i < (int)MathF.Round(opt.ShotSimSeconds / Config.SimDt); i++) { RunDeferred(); World.Step(); }
        if (opt.Demo && Environment.GetEnvironmentVariable("KD_SCENE") == "pronemove" && World.Units.Where(u => u.Owner == Me && u.Alive && u.Def.Key == "infantry").ToList() is { Count: > 0 } pm) Camera.CenterOn(pm.Aggregate(Vector2.Zero, (a, u) => a + u.Pos) / pm.Count);   // 확인용: 중대 중심으로
    }

    /// <summary>사람이 고른 세력(관전이면 없음)을 뺀 모든 세력에 AI 를 붙인다.</summary>
    public static void AttachControllers(World world, int humanFaction, bool spectate, string aiName)
    {
        for (int f = 1; f < world.Factions.Count; f++)
            if ((spectate || f != humanFaction) && AiRegistry.Create(aiName) is IAiController ai)
            {
                world.Controllers.Add((f, ai));
                world.Faction(f).IsAi = true;
            }
    }

    /// <summary>관전 중 모든 AI 세력이 쓰는 AI 이름 (리플레이면 알 수 없음).</summary>
    public string? AiTypeName => spectate ? aiName : null;
    /// <summary>시야 · 렌더링 기준 세력: 관전 시점을 고르면 그 세력, 아니면 Me.</summary>
    public int ViewSeat => Observer ? View : Me;

    public void SetView(int faction)
    {
        if (!Observer || faction < 0 || faction >= World.Factions.Count || faction == View) return;
        View = faction;
        var hq = faction > 0 ? (World.Buildings.FirstOrDefault(b => b.Owner == faction && b.Def.Headquarters) ?? World.Buildings.FirstOrDefault(b => b.Owner == faction && b.Def.City)) : null;
        if (hq != null) Camera.CenterOn(hq.Pos);
    }

    double Now => Raylib.GetTime();

    /// <summary>카메라 시작 위치: 내 유닛들의 중심 (교전 테스트처럼 건물이 없을 때), 없으면 맵 중앙.</summary>
    Vector2 CenterOfOwnArmy()
    {
        Vector2 sum = Vector2.Zero;
        int n = 0;
        foreach (var u in World.Units)
            if (u.Owner == Math.Max(Me, 1)) { sum += u.Pos; n++; }
        return n > 0 && !Observer ? sum / n : new Vector2(World.Map.Width / 2f, World.Map.Height / 2f);
    }

    // ---------------------------------------------------------------- HUD 용 조회

    public string? StatusText
    {
        get
        {
            if (replay != null)
                return string.Format(S.ReplayStatus, Clock(World.Tick), Clock(replay.EndTick)) + (World.Tick >= replay.EndTick ? S.ReplayEnded : "");
            if (PendingCommand != null) return string.Format(S.Pending, PendingLabel(PendingCommand));
            return Now < noticeUntil ? notice : null;
        }
    }

    public static string Clock(long tick)
    {
        long s = tick / 60;
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }

    /// <summary>소유 세력 표시 이름 (0 = 중립, AI 면 "AI" 표시).</summary>
    public string OwnerName(int owner)
    {
        if (owner <= 0 || owner >= World.Factions.Count) return S.Neutral;
        var f = World.Faction(owner);
        if (owner == HumanSeat) return $"{f.Name} {S.PlayerTag}";
        return f.IsAi ? $"{f.Name} ({S.AiTag})" : f.Name;
    }

    static string PendingLabel(string key) => key == "rally" ? S.CmdRally : key == "trenchassault" ? S.CmdTrenchAssault : Commands.Get(key)?.Label ?? key;

    public (Vector2 Pos, double Age)? AlertPing => alert is var (p, t) ? (p, Now - t) : null;

    List<Unit> OwnUnits() => Selection.OfType<Unit>().Where(u => u.Owner == Me && u.Alive).ToList();
    public List<Unit> SelectedOwnUnits() => !Observer ? OwnUnits() : new List<Unit>();
    public Building? SelectedBuilding => Selection.Count == 1 ? Selection[0] as Building : null;

    /// <summary>현재 선택에 맞는 커맨드 카드 버튼 목록.</summary>
    public List<CardButton> CommandCard()
    {
        if (Observer) return new List<CardButton>();   // 리플레이 · 관전: 조작 불가
        if (Tab == CardTab.Combat) return CombatCard(World.Faction(Me));
        return PaginateCard(BuildUnitCard());
    }

    /// <summary>카드 쪽 (0 = 첫 쪽). 버튼이 9칸을 넘치면 둘째 쪽으로 넘어간다 (칸이 모자라 버튼이 사라지지 않게).</summary>
    public int CardPage { get; private set; }

    /// <summary>모든 쪽의 명령 버튼 (단축키 처리용 — 안 보이는 쪽의 단축키도 동작해야 한다).</summary>
    List<CardButton> BuildUnitCard()
    {
        var list = new List<CardButton>();
        var units = OwnUnits();
        var f = World.Faction(Me);
        if (units.Count > 0)
        {
            foreach (var c in Commands.UnitCommands)
            {
                if (!CardAvailable(c, units)) continue;
                bool active = PendingCommand == c.Key || (c.Key == "hold" && units.All(u => u.Order == UnitOrder.Hold)) || (c.Key == "scoutauto" && units.All(u => u.ScoutAuto));
                list.Add(new CardButton(c.Key, c.Label, c.Tooltip, c.Hotkey.ToString(), Commands.Key(c.Hotkey), c.Col, c.Row, c.Key, true, active));
            }
            // 중대 명령: 돌격 사격 켜기 / 끄기 (기본 꺼짐 — 켜면 공격 이동 중 장교가 판단해 중대 단위로 걸으며 쏜다)
            var inf = units.Where(u => u.Company != null && u.Def.MarchFire && u.Def.Category == "infantry").ToList();
            if (inf.Count > 0)
                list.Add(new CardButton("marchfire", S.CmdMarchFire, S.TipMarchFire, "F", KeyboardKey.F, 0, 2, "marchfire", true, inf.All(u => u.Company!.MarchFireOn)));
            // 중대 명령: 대열 정비 (L) — 빈 칸 (1,2) → (2,1) 순으로 (기병 V · 수류탄 G · 정찰 E 와 겹치지 않게)
            if (units.Any(u => u.Company != null))
            {
                var (cc, cr) = !list.Exists(b => b.Col == 1 && b.Row == 2) ? (1, 2) : !list.Exists(b => b.Col == 2 && b.Row == 1) ? (2, 1) : (-1, -1);
                if (cc >= 0) list.Add(CloseRanksButton(cc, cr));
            }
            // N32 참호 돌격 (W): 빈 칸이 있을 때만 (보병 중대는 칸이 가득 차면 전투 탭 · 단축키 W 로)
            if (SelectedCompanies().Count > 0 && Defs.Current.Tactics.TrenchAssault.Enabled)
            {
                var used = new bool[9];
                foreach (var b in list) if (b.Col is >= 0 and <= 2 && b.Row is >= 0 and <= 2) used[b.Row * 3 + b.Col] = true;
                int fi = Array.IndexOf(used, false);
                if (fi >= 0) list.Add(TrenchAssaultButton(fi % 3, fi / 3));
            }
            // N73 사격 통제 (K): 자유 → 금지 → 응사만 순환 (무장 유닛이 있을 때). 빈 칸 (1,2) 우선
            var armedSel = units.Where(u => u.Weapon != null).ToList();
            if (armedSel.Count > 0)
            {
                var fm = armedSel[0].FireMode;
                list.Add(new CardButton("firecontrol", S.CmdFireControl, S.TipFireControl, "K", KeyboardKey.K, 1, 2, "firecontrol", true, fm != FireMode.Free, null, FireModeName(fm)));
            }
            // N79a: 포격 종류 (O, 곡사포 · 방열 포가 있을 때) · 집결 지점 (J, 중대 소속이 있을 때 — 단축키는 전역 처리). 빈 칸이 없으면 카드에서 빠진다 (전투 탭에는 집결 지점 버튼이 항상 있음)
            if (units.Exists(World.CanGroundFire))
                list.Add(new CardButton("firekind", S.CmdFireKind, S.TipFireKind, "O", KeyboardKey.O, 2, 1, "groundfire", true, FireKindSel != GroundFireKind.Sustained, null, FireKindName(FireKindSel)));
            if (units.Any(u => u.Company != null))
                list.Add(new CardButton("rally", S.CmdRallyPoint, S.TipRallyPoint, "J", KeyboardKey.Null, 2, 0, "rally", true, PendingCommand == "rally"));
            return list;
        }

        return list;
    }

    /// <summary>9칸 넘치는 버튼을 둘째 쪽으로: 첫 쪽 마지막 칸 = "더 보기", 둘째 쪽 마지막 칸 = "처음으로". 단축키는 그대로.</summary>
    List<CardButton> PaginateCard(List<CardButton> all)
    {
        var page1 = new List<CardButton>();
        var over = new List<CardButton>();
        var used = new bool[9];
        foreach (var b in all)
        {
            var nb = b;
            if (b.Col is >= 0 and <= 2 && b.Row is >= 0 and <= 2)
            {
                int slot = used[b.Row * 3 + b.Col] ? Array.IndexOf(used, false) : b.Row * 3 + b.Col;
                if (slot < 0) { over.Add(b); continue; }
                used[slot] = true;
                nb = b with { Col = slot % 3, Row = slot / 3 };
            }
            page1.Add(nb);
        }
        if (over.Count == 0) { CardPage = 0; return page1; }
        var last = page1[^1];   // 가장 나중 버튼을 둘째 쪽으로 보내고 그 칸에 "더 보기"
        page1.RemoveAt(page1.Count - 1);
        over.Insert(0, last);
        if (CardPage == 0)
        {
            page1.Add(new CardButton("cardpage", S.CmdMoreCard, S.TipMoreCard, "", KeyboardKey.Null, last.Col, last.Row, "quickstep"));
            return page1;
        }
        var page2 = new List<CardButton>();
        for (int i = 0; i < over.Count && i < 8; i++) page2.Add(over[i] with { Col = i % 3, Row = i / 3 });
        page2.Add(new CardButton("cardpage", S.CmdBackCard, S.TipBackCard, "", KeyboardKey.Null, 2, 2, "stop"));
        return page2;
    }

    /// <summary>명령 카드 버튼을 보일지: 공격 · 돌격은 선택 중 하나라도 할 수 있으면 (N36 — 비전투 유닛은 대열을 따라 이동, 돌격 못 하는 유닛은 사수), 나머지는 전원이 할 수 있을 때.</summary>
    public static bool CardAvailable(CommandDef c, IReadOnlyList<Unit> units) =>
        c.Key is "attack" or "charge" ? units.Any(u => c.Available(u.Def)) : units.All(u => c.Available(u.Def));

    /// <summary>플레이어 직접 명령: 그동안 내는 명령에 선택 인원을 싣는다 (N37 지연 곡선, World.IssueSel).</summary>
    void PlayerOrder(int selected, Action issue)
    {
        World.IssueSel = Math.Max(0, selected);
        try { issue(); } finally { World.IssueSel = 0; }
    }

    /// <summary>카드 칸 충돌 해소 (N15-2): 같은 칸을 쓰는 버튼은 먼저 온 쪽이 칸을 지키고 나중 버튼이 첫 빈 칸 (행 우선) 으로 옮겨 간다. 단축키는 그대로.</summary>
    public static void ResolveSlots(List<CardButton> list)
    {
        var used = new bool[9];
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b.Col is < 0 or > 2 || b.Row is < 0 or > 2) continue;
            if (!used[b.Row * 3 + b.Col]) { used[b.Row * 3 + b.Col] = true; continue; }
            int free = Array.IndexOf(used, false);
            if (free < 0) continue;
            used[free] = true;
            list[i] = b with { Col = free % 3, Row = free / 3 };
        }
    }

    public static string FireKindName(GroundFireKind k) => k switch { GroundFireKind.Prep => S.FireKindPrep, GroundFireKind.Barrage => S.FireKindBarrage, GroundFireKind.Smoke => S.FireKindSmoke, _ => S.FireKindSustained };

    public static string FireModeName(FireMode m) => m switch { FireMode.Hold => S.FireHold, FireMode.ReturnFire => S.FireReturn, FireMode.FinalLine => S.FireFinalLine, _ => S.FireFree };

    CardButton TrenchAssaultButton(int col, int row) =>
        new("trenchassault", S.CmdTrenchAssault, S.TipTrenchAssault, "W", KeyboardKey.W, col, row, "charge", true, PendingCommand == "trenchassault" || SelectedCompanies().Any(c => c.TrenchAssaultStage != TaStage.None));

    static CardButton CloseRanksButton(int col, int row) => new("closeranks", S.CmdCloseRanks, S.TipCloseRanks, "L", KeyboardKey.L, col, row, "closeranks");

    // ---- 전투 탭 (세력 설정 · 대형). 단축키 없음 (명령 탭의 글자 단축키와 겹치지 않게), 클릭 전용 — 대열 정비 (L) 만 예외 (두 탭에서 같은 키).
    List<CardButton> CombatCard(Faction f)
    {
        var c = f.Combat;
        var selComps = SelectedCompanies();
        var ft = selComps.Count > 0 ? selComps[0].Form : DefaultType;
        bool extra = ft is FormType.Line or FormType.Column or FormType.Wedge or FormType.Box;
        CardButton B(string key, string label, string tip, int col, int row, string icon, bool active, string? value = null) =>
            new(key, label, tip, "", KeyboardKey.Null, col, row, icon, true, active, null, value);
        var list = new List<CardButton>
        {
            B("cb:form:0", S.CbFormFree, S.CbTipFormFree, 0, 2, "form:0", ft == FormType.Free),
            B("cb:form:5", S.CbFormSpread, S.CbTipFormSpread, 1, 2, "form:5", ft == FormType.Spread),
            B("cb:formmore", extra ? Hud.FormName(ft) : S.CbFormMore, S.CbTipFormMore, 2, 2, "form:" + (extra ? (int)ft : (int)FormType.Line), extra),
        };
        if (OwnUnits().Any(u => u.Company != null)) list.Add(CloseRanksButton(0, 0));   // 대열 정비 (L)
        if (selComps.Count > 0 && Defs.Current.Tactics.TrenchAssault.Enabled) list.Add(TrenchAssaultButton(2, 0));   // N32 참호 돌격 (W)
        if (selComps.Count > 0)
        {
            list.Add(B("cb:rally", S.CmdRallyPoint, S.TipRallyPoint + " [J]", 2, 0, "rally", PendingCommand == "rally"));   // N79a
            list.Add(B("cb:retreat", S.CmdRetreatRally, S.TipRetreatRally, 0, 1, "back", false));
        }
        if (selComps.Count > 0) list.Add(B("cb:keepform", S.CbKeepForm, S.CbTipKeepForm, 1, 0, "form:" + (int)FormType.Line, selComps.All(k => k.KeepFormation)));   // F1 2차: 대형 유지 (켜고 끄기)
        return list;
    }

    void CombatPress(string key)
    {
        var f = World.Faction(Me);
        var c = f.Combat;
        switch (key)
        {
            case "cb:form:0": ApplyForm(FormType.Free); break;
            case "cb:form:5": ApplyForm(FormType.Spread); break;
            case "cb:rally": if (SelectedCompanies().Count > 0) PendingCommand = "rally"; break;
            case "cb:retreat":
            {
                var rl = OwnUnits();
                int n = rl.Where(u => u.Company is { HasRally: true }).Select(u => u.Company!.Id).Distinct().Count();
                if (n == 0) { ShowNotice(S.RetreatNoRally); break; }
                PlayerOrder(rl.Count, () => World.RetreatToRally(Me, rl));
                ShowNotice(string.Format(S.RetreatToRally, n));
                Voice("move");
                break;
            }
            case "cb:keepform":
            {
                var sc = SelectedCompanies();
                if (sc.Count == 0) break;
                bool on = !sc.All(k => k.KeepFormation);
                World.SetKeepFormation(Me, sc.Select(k => k.Id), on);
                ShowNotice(on ? S.KeepFormOn : S.KeepFormOff);
                break;
            }
            case "cb:formmore":
            {
                var sc = SelectedCompanies();
                var cur = sc.Count > 0 ? sc[0].Form : DefaultType;
                FormType[] extra = { FormType.Line, FormType.Column, FormType.Wedge, FormType.Box };
                int i = Array.IndexOf(extra, cur);
                ApplyForm(extra[(i + 1) % extra.Length]);
                break;
            }
        }
    }

    /// <summary>전투 탭의 대형 종류: 고른 중대가 있으면 그 중대들의 대형, 없으면 기본 대형 (병과 배치는 유지). 기본 대형은 세력 설정(명령)으로도 기록한다.</summary>
    void ApplyForm(FormType t)
    {
        var comps = SelectedCompanies();
        if (comps.Count > 0 && t != FormType.Free)
        {
            World.SetCompanyForm(Me, comps, t);
            ShowNotice(string.Format(S.CompanyFormSet, Hud.FormName(t)));
            return;
        }
        DefaultType = t;
        World.SetCombatSetting(Me, CombatSetting.DefaultFormation, (int)t);
        ShowNotice(string.Format(S.FormSetDefault, Hud.FormName(t), Hud.OrderName(DefaultOrder)));
    }

    // ---------------------------------------------------------------- 날씨 · 시간대 (화면 · 소리 전용)

    static readonly string[] weatherNames = S.WeatherNames.Split('|'), timeNames = S.TimeNames.Split('|');
    /// <summary>HUD 표시: "안개 · 밤".</summary>
    public string WeatherLabel => $"{weatherNames[(int)World.Weather.Kind]} · {timeNames[(int)World.Weather.Time]}";

    /// <summary>날씨가 바뀌면 환경음 고리를 바꾸고 알림 (시작 때는 알림 없이 환경음만).</summary>
    void UpdateWeatherCues()
    {
        var w = World.Weather;
        if (w.Changes != weatherSeen)
        {
            if (weatherSeen >= 0) ShowNotice(string.Format(S.WeatherChanged, weatherNames[(int)w.Kind], timeNames[(int)w.Time]));
            weatherSeen = w.Changes;
            // 비 = 빗소리 (밤비는 센 비), 눈 · 안개 = 바람, 맑은 밤 = ambient_night (파일이 없으면 약한 바람)
            var (ambKey, ambVol) = w.Kind switch
            {
                WeatherKind.Rain => (w.Time == TimeOfDay.Night && Sfx.Has("ambient_rain_heavy") ? "ambient_rain_heavy" : "ambient_rain", 1f),
                WeatherKind.Snow or WeatherKind.Fog => ("ambient_wind", 1f),
                _ when w.Time == TimeOfDay.Night => Sfx.Has("ambient_night") ? ("ambient_night", 1f) : ("ambient_wind", 0.45f),
                _ => ((string?)null, 1f),
            };
            Sfx.SetAmbient(ambKey, ambVol);
        }
        Sfx.UpdateAmbient();
    }

    // ---------------------------------------------------------------- 업데이트

    public void Update(float dt)
    {
        if (deferredConsole.Count > 0) RunDeferred();
        Sfx.BeginFrame();
        if (Raylib.GetScreenWidth() != screenW || Raylib.GetScreenHeight() != screenH) Layout();

        mouse = Input.GetMousePosition();
        mouseInViewport = Ui.Contains(Hud.Viewport, mouse) && !Hud.Contains(mouse) && !Spectator.Contains(mouse);
        mouseWorld = Camera.ScreenToWorld(mouse);
        hover = mouseInViewport ? EntityAt(mouseWorld) : null;
        UpdateWeatherCues();

        if (CommandBox.IsOpen)
        {
            if (CommandBox.Update() is string line) RunConsoleCommand(line);
        }
        else if (HelpBox.IsOpen) HelpBox.Update(mouse);   // 도움말이 열려 있는 동안 게임 입력은 막힘 (F1 은 HandleGlobalKeys)
        else if (World.Winner != 0) { if (World.Mission != null) HandleMissionOver(); else HandleGameOver(); }
        else if (DeployPhase) UpdateDeploy();   // 미션 배치 단계 (GameMission.cs)
        else if (Input.IsKeyPressed(KeyboardKey.Enter) || Input.IsKeyPressed(KeyboardKey.KpEnter)) CommandBox.Open();
        else
        {
            HandleMouse();
            HandleKeyboard();
        }
        if (!CommandBox.IsOpen)
        {
            HandleGlobalKeys();
            if (!HelpBox.IsOpen) ScrollCamera(dt);
        }

        if (replay != null && World.Tick >= replay.EndTick) Paused = true;

        // 고정 틱 시뮬레이션 (게임 속도 배율 적용)
        if (!Paused && World.Winner == 0 && !DeployPhase)
        {
            accumulator += dt * Speed;
            int steps = 0, maxSteps = Config.MaxStepsPerFrame * 3;
            long t0 = Stopwatch.GetTimestamp();
            while (accumulator >= Config.SimDt && steps < maxSteps)
            {
                var us = World.Units;
                for (int i = 0; i < us.Count; i++) us[i].RenderPrev = us[i].Pos;   // 화면 보간 기준 (틱 사이 위치, 렌더 전용 — docs/COLLISION_ANALYSIS.md (b)-5)
                World.Step();
                accumulator -= Config.SimDt;
                steps++;
            }
            if (steps == maxSteps) accumulator = 0;
            UnitRenderer.Alpha = Math.Clamp(accumulator / Config.SimDt, 0f, 1f);
            if (steps > 0) LastStepMs = (float)Stopwatch.GetElapsedTime(t0).TotalMilliseconds / steps;
        }

        AutosaveIfDue();
        ConsumeWorldEvents();
        Sfx.EndFrame();
        double now = Now;
        markers.RemoveAll(m => now - m.Time > 0.6);
        orderLines.RemoveAll(l => now - l.Time > 1.0);
        foreach (var e in Selection.Where(e => !e.Alive || !World.Fog.CanSee(ViewSeat, e)).ToList()) Deselect(e);
        if (!Observer && Me > 0 && Me < World.Factions.Count)
        {
            var simDef = World.Faction(Me).Combat.DefaultFormation;   // 불러오기 · 리플레이로 바뀐 기본 대형을 화면 쪽 설정에 반영
            if (simDef != lastSimDefaultForm) { lastSimDefaultForm = simDef; DefaultType = simDef; }
        }

        bool targeting = PendingCommand != null && (mouseInViewport || Hud.Minimap.Contains(mouse));
        var want = targeting ? MouseCursor.Crosshair : MouseCursor.Default;
        if (want != cursor) Raylib.SetMouseCursor(cursor = want);
    }

    // ---------------------------------------------------------------- 효과음 (표현 전용 — 시뮬레이션과 무관)

    long soundDeathTick;
    int soundMaxUnitId = int.MaxValue;
    bool soundGameOver;

    /// <summary>이번 프레임에 새로 생긴 사건을 보고 소리를 낸다 (보이는 것만 — 안개 속 전투는 들리지 않음).</summary>
    void PlayWorldSounds()
    {
        int seat = ViewSeat;
        var fog = World.Fog;
        SfxDirector.SimSpeed = Paused ? 0f : Speed;   // 포탄 휘파람 시작 시각 (실시간 ↔ 틱)
        SfxDirector.Events(World, Camera, seat);   // 무기별 사격 · 근접 타격 · 총알 소리 (포 · 범위 무기는 Render/Impacts 가 낸다)
        SfxDirector.Frame(World, Camera, seat);    // 교전 환경음 · 전차 / 기병 / 마차 / 삽질 고리
        foreach (var (pos, tick, building) in World.Deaths)
        {
            if (tick < soundDeathTick || !fog.IsVisible(seat, pos)) continue;
            if (building) Sfx.PlayAt("explosion", pos, Camera);   // 유닛 전사 소리(보병 death · 차량 explosion)는 Render/Impacts 가 낸다
        }
        soundDeathTick = World.Tick;

        // 생산 완료 (내 새 유닛)
        if (soundMaxUnitId == int.MaxValue) soundMaxUnitId = World.Units.Count > 0 ? World.Units.Max(u => u.Id) : 0;
        foreach (var u in World.Units)
            if (u.Id > soundMaxUnitId)
            {
                soundMaxUnitId = u.Id;
                if (u.Owner == Me && Me > 0) { Sfx.Play("unit_ready", 0.8f); Voice("unit_ready"); }
            }
        PlayVoiceEvents();

        if (World.Winner != 0 && !soundGameOver)
        {
            soundGameOver = true;
            Sfx.Play(Me > 0 && !World.Winners.Contains(Me) ? "defeat" : "victory");
        }
    }

    // ---------------------------------------------------------------- 보이스 (세력 언어: Faction.Lang — assets/sounds/<테마>/voice/<언어>/<사건>_<n>.wav, 없으면 조용)

    /// <summary>내 세력(= 내 유닛의 주인)의 언어로 사건 대사를 재생. 관전 · 리플레이에서는 없음.</summary>
    void Voice(string ev)
    {
        if (Observer || Me <= 0 || Me >= World.Factions.Count) return;
        Sfx.PlayVoice(Me, World.Faction(Me).Lang, ev);
    }

    HashSet<int> voiceOwn = new(), voiceOwnPrev = new();
    readonly Dictionary<int, int> voiceSiteOwner = new();

    /// <summary>내 유닛이 사라짐(전사) · 내가 중립 / 적 시설을 점령함을 감지해 대사를 낸다. 프레임당 유닛 수만큼의 가벼운 순회.</summary>
    void PlayVoiceEvents()
    {
        if (Observer || Me <= 0) return;
        voiceOwn.Clear();
        foreach (var u in World.Units)
            if (u.Owner == Me) voiceOwn.Add(u.Id);
        bool lost = false;
        foreach (int id in voiceOwnPrev)
            if (!voiceOwn.Contains(id)) { lost = true; break; }
        if (lost) Voice("death");
        (voiceOwn, voiceOwnPrev) = (voiceOwnPrev, voiceOwn);

        foreach (var b in World.Buildings)
        {
            if (!b.Def.Capturable) continue;
            if (voiceSiteOwner.TryGetValue(b.Id, out int old) && old != Me && b.Owner == Me) Voice("capture");
            voiceSiteOwner[b.Id] = b.Owner;
        }
    }

    void ConsumeWorldEvents()
    {
        PlayWorldSounds();
        // 세력 패배 알림 (외교 소식 피드 대체 — 표시 전용, 시뮬레이션 상태를 읽기만 함)
        if (seenDefeated == null || seenDefeated.Length != World.Factions.Count)
            seenDefeated = World.Factions.Select(f => f.Defeated).ToArray();
        for (int i = 1; i < World.Factions.Count; i++)
            if (World.Factions[i].Defeated && !seenDefeated[i])
            {
                seenDefeated[i] = true;
                ShowNotice(string.Format(S.EvDefeated, OwnerName(i)));
            }

        World.Notices.Clear();   // 생산 · 건설 실패 알림은 방향 변경 §0-1 에서 사라짐

        if (World.LastAttacked.TryGetValue(Me, out var atk) && atk.Tick != lastAttackTick)
        {
            lastAttackTick = atk.Tick;
            if (Now - lastAlertTime > 8)
            {
                lastAlertTime = Now;
                alert = (atk.Pos, Now);
                ShowNotice(S.UnderAttack);
                Sfx.Play("alert");
                Voice("under_attack");
            }
        }
    }

    void Layout()
    {
        screenW = Raylib.GetScreenWidth();
        screenH = Raylib.GetScreenHeight();
        Hud.Layout(screenW, screenH);
        Camera.SetViewport(Hud.Viewport);
        Spectator.Layout(Hud.Viewport);
    }

    void HandleGameOver()
    {
        if (Input.IsKeyPressed(KeyboardKey.Enter)) { if (spectate) WatchAgainRequested = true; else RestartRequested = true; }
        if (Input.IsMouseButtonPressed(MouseButton.Left))
            switch (Hud.GameOverButtonAt(mouse))
            {
                case "restart": if (spectate) WatchAgainRequested = true; else RestartRequested = true; break;
                case "quit": QuitRequested = true; break;
            }
    }

    void HandleMouse()
    {
        bool shift = Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift);
        bool ctrl = Input.IsKeyDown(KeyboardKey.LeftControl) || Input.IsKeyDown(KeyboardKey.RightControl);

        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Hud.TopButtonAt(mouse) is string top) OnTopButton(top);
            else if (Spectator.Click(mouse)) { }
            else if (!Observer && Hud.TabAt(mouse) is int tabIdx and >= 0) SetTab((CardTab)tabIdx);
            else if (Hud.PagerAt(mouse) is int pageDir and not 0) { Hud.MovePage(pageDir); Sfx.Play("ui_click"); }
            else if (!Observer && Hud.DetailButtonAt(mouse) is string detailBtn) DetailPress(detailBtn);
            else if (!Observer && Hud.SquadChipAt(mouse) is Squad chip) SelectSquad(chip, shift);
            else if (!Observer && Hud.CompanyCardAt(mouse) is Company card) { cardPress = card; cardPressPos = mouse; }
            else if (!Observer && Hud.MergeButtonAt(mouse)) MergeSelected();
            else if (!Observer && Hud.BatListAt(mouse) is object listItem) SelectListItem(listItem, shift);
            else if (!Observer && Hud.OverBatList(mouse)) { }
            else if (!Observer && Hud.BatFormAt(mouse) is int formSlot) { if (formSlot == 0) CycleDefaultForm(false); else CycleBattalionForm(World.BattalionByNumber(Me, formSlot)); }
            else if (!Observer && Hud.BatAt(mouse) is int grp) RecallBattalion(grp);
            else if (Hud.CardAt(mouse) is CardButton btn) Press(btn);
            else if (Hud.Minimap.Contains(mouse))
            {
                Vector2 w = Hud.Minimap.ScreenToWorld(mouse);
                if (PendingCommand != null) ResolvePending(w, null);
                else { minimapDrag = true; Camera.CenterOn(w); }
            }
            else if (mouseInViewport)
            {
                if (PendingCommand != null) ResolvePending(mouseWorld, hover);
                else dragStartWorld = mouseWorld;
            }
        }
        if (minimapDrag && Input.IsMouseButtonDown(MouseButton.Left)) Camera.CenterOn(Hud.Minimap.ScreenToWorld(mouse));
        if (Input.IsMouseButtonReleased(MouseButton.Left))
        {
            minimapDrag = false;
            if (cardPress is Company cp)
            {
                if (Vector2.Distance(mouse, cardPressPos) >= Config.DragThreshold) DropCard(cp);
                else SelectCompany(cp, shift);
                cardPress = null;
            }
            if (dragStartWorld is Vector2 s) FinishSelection(s, shift, ctrl, Input.IsKeyDown(KeyboardKey.LeftAlt) || Input.IsKeyDown(KeyboardKey.RightAlt));
            dragStartWorld = null;
        }

        if (Input.IsMouseButtonPressed(MouseButton.Right))
        {
            rightStartScreen = rightStartWorld = null;
            if (PendingCommand != null) CancelPending();
            else if (!Observer && Hud.BatFormAt(mouse) is int formSlotR) { if (formSlotR == 0) CycleDefaultForm(true); }   // 기본 대형 단추 우클릭 = 병과 배치
            else if (!Observer && (Hud.OverBatBar(mouse) || Hud.OverBatList(mouse))) { }
            else if (Hud.Minimap.Contains(mouse)) SmartCommand(Hud.Minimap.ScreenToWorld(mouse), null);
            else if (mouseInViewport && Observer) SmartCommand(mouseWorld, hover);
            else if (mouseInViewport) { rightStartScreen = mouse; rightStartWorld = mouseWorld; }   // 뗄 때 정한다: 짧으면 이동 · 공격, 끌었으면 전선
        }
        if (Input.IsMouseButtonReleased(MouseButton.Right) && rightStartScreen is Vector2 rs && rightStartWorld is Vector2 rw)
        {
            rightStartScreen = rightStartWorld = null;
            var units = OwnUnits();
            if (units.Count > 0 && Vector2.Distance(mouse, rs) >= Config.DragThreshold) IssueFrontLine(units, rw, mouseWorld);
            else SmartCommand(rw, hover);
        }

        if (Input.IsMouseButtonDown(MouseButton.Middle))
        {
            Vector2 d = Input.GetMouseDelta();
            if (d != Vector2.Zero) Camera.Scroll(-d);
        }

        float wheel = Input.GetMouseWheelMove();
        if (wheel != 0 && Spectator.Contains(mouse)) Spectator.Wheel(wheel);
        else if (wheel != 0 && mouseInViewport) Camera.ZoomAt(wheel, mouse);
    }

    void HandleKeyboard()
    {
        bool ctrl = Input.IsKeyDown(KeyboardKey.LeftControl) || Input.IsKeyDown(KeyboardKey.RightControl);

        if (Observer && Input.IsKeyPressed(KeyboardKey.Tab)) Spectator.Toggle();
        if (Input.IsKeyPressed(KeyboardKey.Escape))
        {
            if (PendingCommand != null) CancelPending();
            else ClearSelection();
        }
        bool altKey = Input.IsKeyDown(KeyboardKey.LeftAlt) || Input.IsKeyDown(KeyboardKey.RightAlt);
        losShow = !Observer && !ctrl && !altKey && (Input.IsKeyDown(KeyboardKey.B) || Environment.GetEnvironmentVariable("KD_LOSDEMO") is "1" or "cursor");   // N75: B 누르는 동안 사선 표시 (Shift = 커서 기준)
        losCursor = losShow && (Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift) || Environment.GetEnvironmentVariable("KD_LOSDEMO") == "cursor");
        if (ctrl && Input.IsKeyPressed(KeyboardKey.G)) ShowGrid = !ShowGrid;
        else if (!ctrl && !altKey && !Observer && Input.IsKeyPressed(KeyboardKey.G) && SelectedCompanies().Count > 0) MergeSelected();
        if (!ctrl && !altKey && Input.IsKeyPressed(KeyboardKey.I)) { SquadIcons.Enabled = !SquadIcons.Enabled; ShowNotice(SquadIcons.Enabled ? S.SquadIconsOn : S.SquadIconsOff); }   // 분대 상태 아이콘 켜고 끄기
        if (Input.IsKeyPressed(KeyboardKey.N)) { ToggleSound(); ShowNotice(Sfx.Muted ? S.MuteOn : S.MuteOff); }   // 소리 켜고 끄기 (교전 테스트 무음도 해제)
        if (!Observer && Input.IsKeyPressed(KeyboardKey.Tab))   // Tab: 카드 탭 순환 (명령 <-> 전투)
        {
            SetTab((CardTab)(((int)Tab + 1) % 2));
        }
        if (ctrl && !Observer && Input.IsKeyPressed(KeyboardKey.F))   // Ctrl+F: 선택한 부대의 진형 종류 순환 (Ctrl+Shift+F: 병과 배치). F 는 건설 메뉴 단축키(공장)와 겹치지 않게 Ctrl 필수
        {
            if (Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift)) CycleBattalionForm(CurrentBattalion());
            else CycleCompanyForm();
        }
        if (Input.IsKeyPressed(KeyboardKey.Space) && Selection.Count > 0) CenterOnSelection();

        bool shift = Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift);
        for (int n = 0; n <= 9; n++)
        {
            if (!Input.IsKeyPressed(KeyboardKey.Zero + n) && !Input.IsKeyPressed(KeyboardKey.Kp0 + n)) continue;
            if (Observer) { if (!ctrl && n < World.Factions.Count) SetView(n); }   // 관전: 숫자 = 시점 (0 = 전체)
            else if (n == 0) { ShowBatList = !ShowBatList; Sfx.Play("ui_click"); } // 0: 대대 목록
            else if (ctrl && altKey) { FormCompanyFromSelection(n); }              // Ctrl+Alt+1..9: 중대 편성 및 편입
            else if (ctrl && shift)                                                // Ctrl+Shift+1..9: 현재 대대 연대 소속 변경
            {
                if (CurrentBattalion() is { } curBat) SetBattalionRegiment(curBat, n);
            }
            else if (ctrl) { RenumberBattalion(n); }                               // Ctrl+1..9: 대대 번호 변경
            else if (altKey) { RecallBattalion(n); }                               // Alt+1..9: 대대 선택
            else if (!shift) { RecallRegiment(n); }                                // 1..9: 연대 선택
        }

        bool pressedCard = false;
        if (!ctrl && !Observer && Input.IsKeyPressed(KeyboardKey.J) && SelectedCompanies().Count > 0) { PendingCommand = "rally"; Sfx.Play("ui_click"); }   // N79a: 집결 지점 (J — 어느 탭에서든)
        if (!ctrl)
            foreach (var c in Tab == CardTab.Combat ? CommandCard() : BuildUnitCard())
                if (c.Hotkey != KeyboardKey.Null && Input.IsKeyPressed(c.Hotkey)) { Press(c); pressedCard = true; break; }
        // N32: 참호 돌격 W 는 카드에 칸이 없어도 (보병 중대는 명령 탭이 가득 참) 어느 탭에서나
        if (!ctrl && !pressedCard && !Observer && Defs.Current.Tactics.TrenchAssault.Enabled && Input.IsKeyPressed(KeyboardKey.W) && SelectedCompanies().Count > 0) { Sfx.Play("ui_click"); PressTrenchAssault(); }
    }

    /// <summary>게임 종료 화면에서도 동작하는 키.</summary>
    void HandleGlobalKeys()
    {
        if (Input.IsKeyPressed(KeyboardKey.F1)) HelpBox.Toggle();   // T38b: 도움말 (일시정지 · 종료 화면에서도)
        if (Input.IsKeyPressed(KeyboardKey.F3)) Paused = !Paused;
        if (Input.IsKeyPressed(KeyboardKey.F6)) QuickSave();   // 빠른 저장 (saves/quicksave.kds)
        if (Input.IsKeyPressed(KeyboardKey.F7)) QuickLoad();   // 빠른 불러오기
        if (World.BattleMode && Input.IsKeyPressed(KeyboardKey.F5)) { if (spectate) WatchAgainRequested = false; RestartRequested = true; }   // 교전 테스트: 같은 구성으로 바로 다시
        if (Input.IsKeyPressed(KeyboardKey.LeftBracket)) speedIndex = Math.Max(0, speedIndex - 1);
        if (Input.IsKeyPressed(KeyboardKey.RightBracket)) speedIndex = Math.Min(Speeds.Length - 1, speedIndex + 1);
        if (Input.IsKeyPressed(KeyboardKey.Equal) || Input.IsKeyPressed(KeyboardKey.KpAdd)) Camera.ZoomAt(1, ViewportCenter);
        if (Input.IsKeyPressed(KeyboardKey.Minus) || Input.IsKeyPressed(KeyboardKey.KpSubtract)) Camera.ZoomAt(-1, ViewportCenter);
    }

    Vector2 ViewportCenter => new(Hud.Viewport.X + Hud.Viewport.Width / 2, Hud.Viewport.Y + Hud.Viewport.Height / 2);

    void ScrollCamera(float dt)
    {
        Vector2 dir = Vector2.Zero;
        if (Input.IsKeyDown(KeyboardKey.Left)) dir.X -= 1;
        if (Input.IsKeyDown(KeyboardKey.Right)) dir.X += 1;
        if (Input.IsKeyDown(KeyboardKey.Up)) dir.Y -= 1;
        if (Input.IsKeyDown(KeyboardKey.Down)) dir.Y += 1;

        // 화면 가장자리 스크롤 (창이 포커스를 갖고 커서가 창 안에 있을 때만)
        if (Input.IsWindowFocused() && Input.IsCursorOnScreen() && !minimapDrag)
        {
            int m = Config.EdgeScrollMargin;
            if (mouse.X <= m) dir.X -= 1;
            if (mouse.X >= screenW - 1 - m) dir.X += 1;
            if (mouse.Y <= m) dir.Y -= 1;
            if (mouse.Y >= screenH - 1 - m) dir.Y += 1;
        }
        if (dir != Vector2.Zero) Camera.Scroll(Vector2.Normalize(dir) * Config.ScrollSpeed * dt);
    }

    // ---------------------------------------------------------------- 선택

    void ClearSelection()
    {
        foreach (var e in Selection) e.Selected = false;
        Selection.Clear();

        CancelPending();
    }

    internal void SetSelection(IEnumerable<Entity> entities)
    {
        ClearSelection();
        foreach (var e in entities) AddToSelection(e);
    }

    void AddToSelection(Entity e)
    {
        if (e.Selected) return;
        if (Selection.Count == 0) { if (Tab != CardTab.Command) Tab = CardTab.Command; Hud.ResetPage(); CardPage = 0; }   // 새 선택: 명령 탭으로 · 쪽 초기화
        e.Selected = true;
        Selection.Add(e);
    }

    void Deselect(Entity e)
    {
        e.Selected = false;
        Selection.Remove(e);
    }

    /// <summary>마우스 아래의 보이는 엔티티. 유닛 우선, 그다음 건물.</summary>
    Entity? EntityAt(Vector2 world)
    {
        Entity? best = null;
        float bestD = float.MaxValue;
        foreach (var u in World.Units)
        {
            float d = Vector2.DistanceSquared(u.Pos, world);
            float r = u.Radius + 0.15f;
            if (d <= r * r && d < bestD && World.Fog.CanSee(ViewSeat, u)) (best, bestD) = (u, d);
        }
        if (best != null) return best;
        var (tx, ty) = GameMap.TileOf(world);
        foreach (var b in World.Buildings)
            if (b.ContainsTile(tx, ty) && World.Fog.CanSee(ViewSeat, b)) return b;
        return null;
    }

    bool OnScreen(Entity e)
    {
        var (min, max) = Camera.VisibleWorld();
        return e.Pos.X >= min.X && e.Pos.X <= max.X && e.Pos.Y >= min.Y && e.Pos.Y <= max.Y;
    }

    void FinishSelection(Vector2 startWorld, bool shift, bool ctrl, bool alt = false)
    {
        FinishSelectionCore(startWorld, shift, ctrl, alt);
        if (!Observer && Selection.Any(e => e is Unit && e.Owner == Me)) Voice("select");
    }

    /// <summary>
    /// 선택 방법 (ROADMAP §2-7, 2026-10-19 승인): 드래그 = 자유 유닛 · Alt+드래그 = 분대 단위 · Shift+드래그 = 중대 단위 (추가) · 더블클릭 = 분대 → 중대 → 대대 순환 · Ctrl+클릭 = 화면 안 같은 종류 · 숫자 = 대대.
    /// 부분 선택으로 내린 이동은 임시 묶음 (옛 진형) — 소속은 그대로 (World.StartCompanyOrders).
    /// </summary>
    void FinishSelectionCore(Vector2 startWorld, bool shift, bool ctrl, bool alt = false)
    {
        Vector2 a = Camera.WorldToScreen(startWorld);
        var rect = new Rectangle(MathF.Min(a.X, mouse.X), MathF.Min(a.Y, mouse.Y), MathF.Abs(mouse.X - a.X), MathF.Abs(mouse.Y - a.Y));

        if (rect.Width < Config.DragThreshold && rect.Height < Config.DragThreshold)
        {
            var e = EntityAt(mouseWorld);
            if (e == null)
            {
                if (!shift) ClearSelection();
                return;
            }
            bool doubleClick = Now - lastClickTime < Config.DoubleClickTime && lastClickId == e.Id;
            lastClickTime = Now;
            lastClickId = e.Id;
            if (doubleClick && !ctrl && e is Unit eu && eu.Owner == Me && eu.Company != null)
            {
                CycleEchelon(eu, shift);
            }
            else if ((doubleClick || ctrl) && e is Unit u && u.Owner == Me)
            {
                if (!shift) ClearSelection();
                foreach (var o in World.Units)
                    if (o.Owner == Me && o.Def == u.Def && OnScreen(o)) AddToSelection(o);
            }
            else if (shift && e is Unit && e.Owner == Me && Selection.All(s => s is Unit && s.Owner == Me))
            {
                if (e.Selected) Deselect(e);
                else AddToSelection(e);
            }
            else SetSelection(new[] { e });
            return;
        }

        // 범위 선택: 아군 유닛 우선, 없으면 아군 건물 1개
        var picked = new List<Entity>();
        foreach (var u in World.Units)
        {
            if (u.Owner != Me || Me <= 0) continue;   // 관전자(Me=0)는 범위 선택 불가 (중립 시설이 잡히지 않게)
            Vector2 p = Camera.WorldToScreen(u.Pos);
            float r = u.Radius * Config.TileSize * Camera.Zoom;
            if (p.X + r >= rect.X && p.X - r <= rect.X + rect.Width && p.Y + r >= rect.Y && p.Y - r <= rect.Y + rect.Height)
                picked.Add(u);
        }
        if (picked.Count == 0 && Me > 0)
            foreach (var b in World.Buildings)
            {
                if (b.Owner != Me) continue;
                if (Raylib.CheckCollisionPointRec(Camera.WorldToScreen(b.Pos), rect)) { picked.Add(b); break; }
            }
        if (shift)   // Shift+드래그 = 중대 단위 선택: 상자에 한 기라도 들어온 중대는 통째로 (중대 없는 유닛은 그대로)
            foreach (var cu in picked.OfType<Unit>().Select(u => u.Company).Where(c => c != null).Distinct().ToList())
            {
                foreach (var m in cu!.Members) if (m.Alive && !picked.Contains(m)) picked.Add(m);
            }
        else if (alt)   // Alt+드래그 = 분대 단위 선택: 상자에 한 기라도 들어온 분대는 통째로 (분대 없는 유닛 · 장교는 그대로)
            foreach (var sq in picked.OfType<Unit>().Select(u => u.Squad).Where(q => q != null).Distinct().ToList())
            {
                foreach (var m in sq!.Members) if (m.Alive && !picked.Contains(m)) picked.Add(m);
            }
        if (!shift || Selection.Any(s => s is not Unit)) ClearSelection();
        foreach (var e in picked) AddToSelection(e);
    }

    // ---- 편제 (분대 · 중대 · 대대) · 대형

    /// <summary>선택이 정확히 이 유닛 묶음인가.</summary>
    bool SelectionIs(IReadOnlyCollection<Unit> units) => units.Count > 0 && Selection.Count == units.Count && units.All(x => x.Selected);

    /// <summary>
    /// 더블클릭: 분대 → 중대 → 대대 → 분대 … 순환. 지금 선택이 그 유닛의 분대 전원이면 중대, 중대 전원이면 대대 (없으면 분대로), 그 밖이면 분대 (분대가 없으면 중대).
    /// Shift = 지금 선택에 더한다 (순환 판정은 하지 않고 분대 → 중대 순으로 아직 다 고르지 않은 쪽).
    /// </summary>
    void CycleEchelon(Unit u, bool add)
    {
        var squad = u.Squad?.Members.Where(m => m.Alive).ToList() ?? new List<Unit>();
        var comp = u.Company!.Members.Where(m => m.Alive).ToList();
        var bat = u.Company.Battalion?.Companies.SelectMany(c => c.Members).Where(m => m.Alive).ToList() ?? new List<Unit>();
        List<Unit> next;
        if (add) next = squad.Count > 0 && !squad.All(m => m.Selected) ? squad : !comp.All(m => m.Selected) || bat.Count == 0 ? comp : bat;
        else if (squad.Count > 0 && SelectionIs(squad)) next = comp.Count > squad.Count ? comp : bat.Count > comp.Count ? bat : squad;
        else if (SelectionIs(comp)) next = bat.Count > comp.Count ? bat : squad.Count > 0 ? squad : comp;
        else if (bat.Count > 0 && SelectionIs(bat)) next = squad.Count > 0 ? squad : comp;
        else next = squad.Count > 0 ? squad : comp;
        if (add) foreach (var m in next) AddToSelection(m);
        else SetSelection(next);
        if (next == comp || next == bat) Hud.DetailCompanyId = u.Company.Id;
        if (next == bat && u.Company.Battalion is { } b) lastBattalionId = b.Id;
    }

    /// <summary>분대 칩 클릭: 그 분대를 선택 (Shift = 추가 / 이미 다 골랐으면 빼기).</summary>
    void SelectSquad(Squad q, bool add)
    {
        Hud.DetailCompanyId = q.Company.Id;
        var ms = q.Members.Where(m => m.Alive).ToList();
        if (ms.Count == 0) return;
        if (add)
        {
            bool all = ms.All(m => m.Selected);
            foreach (var m in ms) { if (all) Deselect(m); else AddToSelection(m); }
        }
        else SetSelection(ms);
        Sfx.Play("ui_click");
    }

    /// <summary>선택한 내 유닛이 속한 중대들 (번호 순).</summary>
    public List<Company> SelectedCompanies()
    {
        var res = new List<Company>();
        if (Observer || Me <= 0) return res;
        foreach (var e in Selection)
            if (e is Unit { Alive: true } u && u.Owner == Me && u.Company is { } c && !res.Contains(c)) res.Add(c);
        res.Sort((a, b) => a.Number != b.Number ? a.Number.CompareTo(b.Number) : a.Id.CompareTo(b.Id));
        return res;
    }

    static string BattalionLabel(Battalion? r) => r == null ? S.NoBattalion : r.Number > 0 ? string.Format(S.BattalionName, r.Number) : S.BattalionNoNumber;

    /// <summary>"현재 대대": 선택이 정확히 한 대대의 전 중대이거나, 마지막으로 부른 대대 안에 들어 있으면 그 대대. 아니면 null.</summary>
    Battalion? CurrentBattalion()
    {
        if (Observer || Selection.Count == 0) return null;
        var units = Selection.OfType<Unit>().Where(u => u.Alive && u.Owner == Me).ToList();
        if (units.Count != Selection.Count || units.Any(u => u.Company?.Battalion == null)) return null;
        var regs = units.Select(u => u.Company!.Battalion!).Distinct().ToList();
        if (regs.Count != 1) return null;
        var r = regs[0];
        if (r.Companies.SelectMany(c => c.Members).All(u => u.Selected)) return r;
        return r.Id == lastBattalionId ? r : null;
    }

    /// <summary>이 이동 명령에 쓸 진형: 기본 대형 (중대 소속은 자기 대형을 쓰고, 여기서는 방향만 정해 준다). 방향 = 무게중심 → 목표.</summary>
    FormSpec? FormFor(List<Unit> units, Vector2 target)
    {
        if (units.Count < 2) return null;
        Vector2 c = Vector2.Zero;
        foreach (var u in units) c += u.Pos;
        c /= units.Count;
        Vector2 d = target - c;
        d = d.LengthSquared() > 0.25f ? Vector2.Normalize(d) : Vector2.Zero;
        if (DefaultType == FormType.Free && !units.Any(u => u.Company != null)) return null;
        return new FormSpec((int)DefaultType, (int)DefaultOrder, d);
    }

    /// <summary>우클릭 드래그 전선: 드래그선의 중점으로 이동, 폭 = 드래그 길이, 방향 = 드래그선에 수직이고 선택 무게중심에서 먼 쪽.</summary>
    FormSpec FrontLineSpec(List<Unit> units, Vector2 a, Vector2 b, out Vector2 mid)
    {
        mid = (a + b) / 2;
        Vector2 line = b - a;
        float len = line.Length();
        Vector2 t = len > 1e-4f ? line / len : new Vector2(1, 0);
        Vector2 n = new(-t.Y, t.X);
        Vector2 c = Vector2.Zero;
        foreach (var u in units) c += u.Pos;
        c /= Math.Max(1, units.Count);
        if (Vector2.Dot(n, mid - c) < 0) n = -n;
        return new FormSpec((int)DefaultType, (int)DefaultOrder, n, len);
    }

    void IssueFrontLine(List<Unit> units, Vector2 a, Vector2 b)
    {
        var spec = FrontLineSpec(units, a, b, out var mid);
        PlayerOrder(units.Count, () => World.Move(Me, units, mid, spec));
        Mark(mid, Palette.MarkerMove);
        Voice("move");
    }

    void CycleDefaultForm(bool order)
    {
        var t = DefaultType; var o = DefaultOrder;
        if (order) o = FormCycle.NextOrder(o); else t = FormCycle.NextType(t);
        DefaultType = t; DefaultOrder = o;
        if (!Observer) World.SetCombatSetting(Me, CombatSetting.DefaultFormation, (int)t);
        Sfx.Play("ui_click");
        ShowNotice(string.Format(S.FormSetDefault, Hud.FormName(t), Hud.OrderName(o)));
    }

    /// <summary>Ctrl+F: 고른 중대들의 대형 순환 (고른 중대가 없으면 기본 대형 종류).</summary>
    void CycleCompanyForm()
    {
        var comps = SelectedCompanies();
        if (comps.Count == 0) { CycleDefaultForm(false); return; }
        CycleCompanyFormOf(comps, comps[0]);
    }

    void CycleCompanyFormOf(List<Company> comps, Company basis)
    {
        var t = FormCycle.NextCompanyType(basis.Form, comps.TrueForAll(c => FormationRules.CavalryOnly(c.Members)));   // 사선은 기병 중대만
        World.SetCompanyForm(Me, comps, t);
        Sfx.Play("ui_click");
        ShowNotice(string.Format(S.CompanyFormSet, Hud.FormName(t)));
    }

    /// <summary>Ctrl+Shift+F · 대대 막대 단추: 대대 대형 순환 (대대가 없으면 기본 대형의 병과 배치).</summary>
    void CycleBattalionForm(Battalion? r)
    {
        if (r == null) { CycleDefaultForm(true); return; }
        var f = FormCycle.NextBatForm(r.Form);
        World.SetBattalionForm(Me, r, f);
        Sfx.Play("ui_click");
        ShowNotice(string.Format(S.BattalionFormSet, BattalionLabel(r), Hud.BatFormName(f)));
    }

    /// <summary>Alt+1..9: 선택한 내 유닛으로 중대를 편성해 대대 n 에 넣는다.</summary>
    /// <summary>교전 테스트 시작: 중대 없는 소총 보병을 50명 안팎 중대로 편성 (장교 · 정찰병은 고르게 분배). 일반 명령 경로 → 리플레이에 기록.</summary>
    static void AutoFormBattleCompanies(World w, int f)
    {
        var inf = w.Units.Where(u => u.Owner == f && u.Alive && u.Company == null && u.Def.IsInfantry && u.Weapon != null
                                     && !u.Def.IsSupplyWagon && !u.Def.Mounted && !u.CanDeploy && !World.IsIndirect(u.Weapon)
                                     && u.Weapon.Range < (Defs.Current.Weapon("rifle")?.Range ?? 16f) * 1.25f).ToList();
        inf.Sort((a, b) => { int r = a.Pos.Y.CompareTo(b.Pos.Y); if (r != 0) return r; r = a.Pos.X.CompareTo(b.Pos.X); return r != 0 ? r : a.Id.CompareTo(b.Id); });
        var men = inf.Where(u => !u.Def.IsOfficer && !u.Def.Scout).ToList();
        var officers = inf.Where(u => u.Def.IsOfficer).ToList();
        var scouts = inf.Where(u => u.Def.Scout && !u.Def.IsOfficer).ToList();
        if (men.Count < 2) return;
        int k = (men.Count + 49) / 50, size = (men.Count + k - 1) / k;
        var chunks = new List<List<Unit>>();
        for (int i = 0; i < men.Count; i += size) chunks.Add(men.GetRange(i, Math.Min(size, men.Count - i)));
        for (int j = 0; j < officers.Count; j++)
        {
            int ci = Math.Min(chunks.Count - 1, j * chunks.Count / Math.Max(1, officers.Count));
            if (chunks[ci].Count < Company.MaxSize) chunks[ci].Add(officers[j]);
        }
        for (int j = 0; j < scouts.Count; j++) chunks[j % chunks.Count].Add(scouts[j]);
        foreach (var ch in chunks) w.FormCompany(f, ch);
    }

    void FormCompanyFromSelection(int n)
    {
        var units = OwnUnits();
        if (units.Count == 0) return;
        if (World.BattalionByNumber(Me, n) is { } r && r.Companies.Count >= Battalion.MaxCompanies && !units.All(u => u.Company?.Battalion == r))
        {
            ShowNotice(S.BattalionFull);
            return;
        }
        World.FormCompany(Me, units, n);
        ShowNotice(string.Format(S.CompanyFormedIn, string.Format(S.BattalionName, n)));
    }

    /// <summary>G · [병합]: 고른 중대들을 한 대대로.</summary>
    void MergeSelected()
    {
        var comps = SelectedCompanies();
        if (comps.Count == 0) { ShowNotice(S.MergeNeedCompany); return; }
        World.FormBattalion(Me, comps);
        Sfx.Play("ui_click");
        ShowNotice(string.Format(S.BattalionFormed, comps.Count));
    }

    /// <summary>Ctrl+1..9: 현재 대대의 번호를 n 으로 (이미 쓰고 있으면 서로 바뀜).</summary>
    void RenumberBattalion(int n)
    {
        var r = CurrentBattalion();
        if (r == null || r.Number == n) return;
        World.SetBattalionNumber(Me, r, n);
        ShowNotice(string.Format(S.BattalionRenumbered, string.Format(S.BattalionName, n)));
    }

    void DetailPress(string key)
    {
        if (Hud.DetailCompany is not { } c) return;
        switch (key)
        {
            case "split": World.SplitCompany(Me, c); ShowNotice(string.Format(S.CompanySplit, string.Format(S.CompanyName, c.Number))); break;
            case "disband": World.DisbandCompany(Me, c); ShowNotice(string.Format(S.CompanyDisbanded, string.Format(S.CompanyName, c.Number))); break;
            case "form": CycleCompanyFormOf(new List<Company> { c }, c); break;
            case "tactic":
            {
                var t = FormCycle.NextTactic(c.Tactic);
                World.SetCompanyTactic(Me, new[] { c }, t);
                ShowNotice(string.Format(S.CompanyTacticSet, Hud.TacticName(t)));
                break;
            }
            case "sqform" when Hud.FocusSquad is { } q:
            {
                var f = FormCycle.NextSquadForm(q.Form, FormationRules.CavalryOnly(q.Members));
                World.SetSquadForm(Me, new[] { q }, f);
                ShowNotice(string.Format(S.SquadFormSet, string.Format(S.SquadName, q.Number), Hud.SquadFormName(f)));
                break;
            }
            case "sqrole" when Hud.FocusSquad is { } q:
            {
                var r = FormCycle.NextRole(Hud.SquadRole(q));
                World.SetUnitRole(Me, q.Members, r);
                ShowNotice(string.Format(S.SquadRoleSet, string.Format(S.SquadName, q.Number), Hud.RoleName(r)));
                break;
            }
        }
        Sfx.Play("ui_click");
    }

    /// <summary>중대 카드 클릭: 그 중대를 선택 (Shift = 추가 / 이미 있으면 빼기).</summary>
    void SelectCompany(Company c, bool add)
    {
        Hud.DetailCompanyId = c.Id;
        if (add)
        {
            bool all = c.Members.All(u => u.Selected);
            foreach (var u in c.Members) { if (all) Deselect(u); else AddToSelection(u); }
        }
        else SetSelection(c.Members.ToList());
    }

    void SelectListItem(object item, bool add)
    {
        if (item is int regNum) RecallRegiment(regNum);
        else if (item is Battalion r) SelectBattalion(r);
        else if (item is Company c) SelectCompany(c, add);
    }

    void SelectBattalion(Battalion r)
    {
        var units = r.Companies.SelectMany(c => c.Members).Where(u => u.Alive).ToList();
        if (units.Count == 0) return;
        lastBattalionId = r.Id;
        SetSelection(units);
    }

    /// <summary>카드를 놓았다: 대대 칸 = 그 대대로 편입, 대대 막대의 빈 곳 · 목록 패널 = 대대에서 분리.</summary>
    void DropCard(Company c)
    {
        if (Hud.BatSlotAt(mouse) is int n)
        {
            var r = World.BattalionByNumber(Me, n);
            if (r == null || c.Battalion == r) return;
            if (r.Companies.Count >= Battalion.MaxCompanies) { ShowNotice(S.BattalionFull); return; }
            World.MoveCompany(Me, c, r);
            ShowNotice(string.Format(S.CardMoved, string.Format(S.CompanyName, c.Number), string.Format(S.BattalionName, n)));
        }
        else if ((Hud.OverBatBar(mouse) || Hud.OverBatList(mouse)) && c.Battalion != null)
        {
            World.MoveCompany(Me, c, null);
            ShowNotice(string.Format(S.CardLeft, string.Format(S.CompanyName, c.Number)));
        }
    }

    /// <summary>대대 n 선택. 같은 번호를 0.35초 안에 다시 누르면 카메라를 대대로 옮긴다.</summary>
    public void RecallBattalion(int n)
    {
        if (Observer || World.BattalionByNumber(Me, n) is not { } r) return;
        bool again = lastGroupKey == n && Now - lastGroupTime < Config.GroupDoubleTapTime;
        lastGroupKey = n;
        lastGroupTime = Now;
        SelectBattalion(r);
        if (again && Selection.Count > 0) CenterOnSelection();
    }

    void CenterOnSelection()
    {
        Vector2 c = Vector2.Zero;
        foreach (var e in Selection) c += e.Pos;
        Camera.CenterOn(c / Selection.Count);
    }

    // ---------------------------------------------------------------- 명령

    void SetTab(CardTab t)
    {
        Tab = t;
        CardPage = 0;
        CancelPending();
        Sfx.Play("ui_click");
    }

    void CancelPending()
    {
        PendingCommand = null;
    }

    /// <summary>커맨드 카드 버튼 (클릭 또는 단축키).</summary>
    void Press(CardButton btn)
    {
        Sfx.Play("ui_click");
        var units = OwnUnits();
        var key = btn.Key;
        if (key.StartsWith("cb:")) { CombatPress(key); return; }
        switch (key)
        {
            case "cardpage": CardPage = CardPage == 0 ? 1 : 0; break;   // 카드 쪽 넘기기 (UI 전용)
            case "stop": World.Stop(Me, units); break;
            case "hold": World.Hold(Me, units); break;
            case "marchfire": World.SetMarchFire(Me, units, !units.Where(u => u.Company != null && u.Def.MarchFire && u.Def.Category == "infantry").All(u => u.Company!.MarchFireOn)); break;   // 돌격 사격 토글 (중대 단위)
            case "chargeonly": World.SetChargeOnly(Me, units, !units.Where(u => u.Def.Mounted).All(u => u.ChargeOnly)); break;   // 기병 돌격만 토글
            case "mount": World.SetMount(Me, units, units.Where(u => u.Def.Mounted && !u.IsOfficer).All(u => u.Dismounted || u.MountLeft > 0)); break;   // 기병 하마/승마 토글 (N61): 모두 내렸으면 타기
            case "scoutauto": World.SetScoutAuto(Me, units, !units.Where(u => u.Def.Scout).All(u => u.ScoutAuto)); break;   // 정찰병 자율 행동 토글 (Scouts.cs)
            case "firecontrol":   // N73: 자유 → 금지 → 응사만 (→ arty3 최후 저지선: 중기관총이 있으면 지점 고르기) → 자유
            {
                var armed = units.Where(u => u.Weapon != null).ToList();
                if (armed.Count == 0) break;
                if (PendingCommand == "finalline") { PendingCommand = null; World.SetFireMode(Me, armed, FireMode.Free); break; }
                var cur = armed[0].FireMode;
                if (cur == FireMode.ReturnFire && armed.Any(World.CanFinalLine)) { PendingCommand = "finalline"; ShowNotice(S.FinalLinePick); break; }
                World.SetFireMode(Me, armed, cur == FireMode.FinalLine ? FireMode.Free : (FireMode)(((int)cur + 1) % 3));
                break;
            }
            case "trenchassault": PressTrenchAssault(); break;   // N32 참호 돌격 (TrenchAssault.cs)
            case "closeranks": World.CloseRanks(Me, units); ShowNotice(S.CloseRanksIssued); break;   // 대열 정비 (CloseRanks.cs)
            case "firekind":   // N79a: 지속 → 준비 → 이동탄막 → 연막 순환
                FireKindSel = (GroundFireKind)(((int)FireKindSel + 1) % 4);
                ShowNotice(FireKindSel == GroundFireKind.Smoke ? S.FireKindSmokeNone : FireKindName(FireKindSel));
                break;
            case "move" or "quickstep" or "attack" or "charge" or "grenade" or "groundfire" or "rally":
                PendingCommand = key;
                break;
        }
    }

    /// <summary>N32 참호 돌격 버튼 · 단축키 W: 돌격 중인 중대가 있으면 취소, 없으면 참호 지점을 고르게 한다.</summary>
    void PressTrenchAssault()
    {
        var sc = SelectedCompanies();
        if (sc.Count == 0) return;
        if (sc.Any(c => c.TrenchAssaultStage != TaStage.None))
        {
            World.CancelTrenchAssault(Me, sc.Where(c => c.TrenchAssaultStage != TaStage.None).Select(c => c.Id));
            ShowNotice(S.TrenchAssaultCancelled);
            return;
        }
        PendingCommand = PendingCommand == "trenchassault" ? null : "trenchassault";
    }

    /// <summary>대상 지정이 필요한 명령을 좌클릭 지점(또는 엔티티)에 실행.</summary>
    void ResolvePending(Vector2 point, Entity? target)
    {
        var key = PendingCommand!;
        PendingCommand = null;
        var units = OwnUnits();
        PlayerOrder(units.Count, () => ResolvePendingFor(key, units, point, target));
    }

    void ResolvePendingFor(string key, List<Unit> units, Vector2 point, Entity? target)
    {
        switch (key)
        {
            case "move": World.Move(Me, units, point, FormFor(units, point)); Mark(point, Palette.MarkerMove); Voice("move"); break;
            case "quickstep": World.QuickStep(Me, units, point, FormFor(units, point)); Mark(point, Palette.MarkerMove); Voice("move"); break;
            case "attack":
                // 강제 지정 공격: 자기 · 동맹만 아니면 중립 상대도 가능 (피해가 오가면 서로 '교전 중')
                if (target != null && World.IsHostile(Me, target.Owner) && !target.Indestructible)
                {
                    var armed = units.Where(u => u.Weapon != null).ToList();
                    World.Attack(Me, armed, target);
                    World.Move(Me, units.Except(armed), target.Pos);   // 비전투 유닛은 일반 이동으로 따라감
                }
                else World.AttackMove(Me, units, point, FormFor(units, point));
                Mark(target?.Pos ?? point, Palette.HpLow);
                Voice("attack");
                break;
            case "trenchassault":
            {
                var ids = SelectedCompanies().Select(c => c.Id).ToList();
                if (ids.Count > 0 && World.TrenchAssault(Me, ids, point)) { Mark(point, Palette.HpLow); Voice("attack"); ShowNotice(string.Format(S.TrenchAssaultIssued, ids.Count)); }
                else ShowNotice(S.TrenchAssaultNoTrench);
                break;
            }
            case "grenade": World.ThrowGrenadeAt(Me, units, point); Mark(point, Palette.HpLow); Voice("attack"); break;   // 수류탄 (교전 4차)
            case "finalline":   // arty3 최후 저지선: 중기관총 = 사격선 지정, 나머지 무장 유닛 = 순환 다음 (자유 사격)
            {
                var fpl = units.Where(World.CanFinalLine).ToList();
                var rest = units.Where(u => u.Weapon != null && !World.CanFinalLine(u)).ToList();
                if (fpl.Count > 0) World.SetFinalLine(Me, fpl, point);
                if (rest.Count > 0) World.SetFireMode(Me, rest, FireMode.Free);
                Mark(point, Palette.HpLow);
                ShowNotice(string.Format(S.FinalLineSet, fpl.Count));
                break;
            }
            case "groundfire":   // 플레이어 지점 포격 (GroundFire.cs) + 포격 종류 (N79a)
            {
                if (FireKindSel == GroundFireKind.Smoke) { ShowNotice(S.FireKindSmokeNone); break; }   // 연막탄 없음
                var guns = units.Where(World.CanGroundFire).ToList();
                if (guns.Count == 0) break;
                Vector2 gc = Vector2.Zero;
                foreach (var gu in guns) gc += gu.Pos;
                gc /= guns.Count;
                World.GroundFire(Me, units, point, FireKindSel, point - gc);   // 이동탄막 방향 = 포대 중심 → 목표
                Mark(point, Palette.HpLow); Voice("attack");
                break;
            }
            case "rally":   // N79a: 선택한 중대들의 집결 지점
            {
                var comps = units.Where(u => u.Company != null).Select(u => u.Company!.Id).Distinct().ToList();
                if (comps.Count == 0) break;
                World.SetRally(Me, comps, point);
                Mark(point, Palette.Accent);
                ShowNotice(string.Format(S.RallySet, comps.Count));
                break;
            }
            case "charge":
                // 착검 돌격 (ROADMAP §2-4b): 적을 찍으면 그 적에게, 땅을 찍으면 그 지점으로 돌격 이동
                // N36: 섞인 선택은 돌격 가능한 병사만 돌격, 나머지 (기관총 · 포 · 수레) 는 그 자리 사수로 엄호 (World.ChargeMixed)
                World.ChargeMixed(Me, units, point, target != null && World.IsHostile(Me, target.Owner) && !target.Indestructible ? target : null);
                Mark(target?.Pos ?? point, Palette.HpLow);
                Voice("attack");
                break;
        }
    }

    /// <summary>우클릭 기본 동작: 적 → 공격, 그 외 → 이동.</summary>
    void SmartCommand(Vector2 point, Entity? target)
    {
        if (Observer) return;
        var units = OwnUnits();
        if (units.Count == 0) return;
        PlayerOrder(units.Count, () => SmartCommandFor(units, point, target));
    }

    void SmartCommandFor(List<Unit> units, Vector2 point, Entity? target)
    {
        if (target != null && World.IsHostile(Me, target.Owner) && !target.Indestructible)   // 우클릭은 적대 상대만 공격 (중립은 A 로 강제 지정)
        {
            var armed = units.Where(u => u.Weapon != null).ToList();
            World.Attack(Me, armed, target);
            World.Move(Me, units.Except(armed), point);
            Mark(target.Pos, Palette.HpLow);
            Voice("attack");
            return;
        }
        World.Move(Me, units, point, FormFor(units, point));
        Mark(point, Palette.MarkerMove);
        Voice("move");
    }

    void Mark(Vector2 p, Color c)
    {
        markers.Add((p, Now, c));
        var from = OwnUnits().Take(40).Select(u => u.Pos).ToArray();
        if (from.Length > 0) orderLines.Add((from, p, Now, c));
    }

    void OnTopButton(string key)
    {
        Sfx.Play("ui_click");
        switch (key)
        {
            case "grid": ShowGrid = !ShowGrid; break;
            case "icons": SquadIcons.Enabled = !SquadIcons.Enabled; break;
            case "speed": speedIndex = (speedIndex + 1) % Speeds.Length; break;
            case "save": QuickSave(); break;
            case "load": QuickLoad(); break;
            case "board": Spectator.Toggle(); break;
            case "pause": Paused = !Paused; break;
            case "quit": QuitRequested = true; break;
        }
    }

    /// <summary>
    /// 명령어 입력창 처리. 상태를 바꾸는 명령(치트)은 전부 GameCommand 로 보내 리플레이에 기록된다.
    /// </summary>
    /// <summary>N 키 · 콘솔 mute: 소리가 꺼져 있으면 (사용자 음소거 또는 교전 테스트 무음 Sfx.Silent) 둘 다 풀어 켜고, 켜져 있으면 음소거.</summary>
    internal static void ToggleSound()
    {
        if (Sfx.Muted || Sfx.Silent) { Sfx.Muted = false; Sfx.Silent = false; }
        else Sfx.Muted = true;
    }

    /// <summary>[시연 · 스크린샷] "after N 명령" 으로 미룬 콘솔 명령 (틱, 명령).</summary>
    readonly List<(long Tick, string Cmd)> deferredConsole = new();

    void RunDeferred()
    {
        for (int i = 0; i < deferredConsole.Count; i++)
            if (World.Tick >= deferredConsole[i].Tick) { var c = deferredConsole[i].Cmd; deferredConsole.RemoveAt(i--); RunConsoleCommand(c); }
    }

    /// <summary>[시연] 전투 대형 · 전술 틀 콘솔 명령 (고른 중대 대상): ftac 틀 · fco 대형 · fsq 분대대형 · fbat 대대대형 · fmove dx dy [폭] · famove dx dy [폭] (공격 이동) · fcam [dx dy] (카메라 = 중대 무게중심).</summary>
    bool FormationConsole(string[] words)
    {
        var comps = SelectedCompanies();
        switch (words[0])
        {
            case "ftac" when words.Length == 2:
                var t = Enum.GetValues<CompanyTactic>().FirstOrDefault(x => TacticRules.KeyOf(x) == words[1]);
                World.SetCompanyTactic(Me, comps, t);
                return true;
            case "fco" when words.Length == 2 && FormationRules.TypeOf(words[1]) is { } cf:
                World.SetCompanyForm(Me, comps, cf);
                return true;
            case "fsq" when words.Length == 2:
                World.SetSquadForm(Me, comps.SelectMany(c => c.Squads), FormationRules.TypeOf(words[1]) ?? FormType.Free);
                return true;
            case "fbat" when words.Length == 2:
                var bf = words[1] switch { "column" => BatForm.Column, "echelon" => BatForm.Echelon, "reserve" => BatForm.TwoFrontReserve, "waves" => BatForm.Waves, _ => BatForm.Line };
                if (comps.Count > 0 && comps[0].Battalion is { } r) World.SetBattalionForm(Me, r, bf);
                return true;
            case "fcam" when words.Length is 1 or 3:
            {
                // 고른 중대 무게중심 (+ dx dy) 로 카메라
                var ms = comps.SelectMany(c => c.Members).ToList();
                if (ms.Count == 0) return true;
                Vector2 cc = Vector2.Zero;
                foreach (var u in ms) cc += u.Pos;
                cc /= ms.Count;
                if (words.Length == 3 && float.TryParse(words[1], System.Globalization.CultureInfo.InvariantCulture, out float ox) && float.TryParse(words[2], System.Globalization.CultureInfo.InvariantCulture, out float oy)) cc += new Vector2(ox, oy);
                Camera.CenterOn(cc);
                return true;
            }
            case "fface" when words.Length == 3 && float.TryParse(words[1], System.Globalization.CultureInfo.InvariantCulture, out float fx) && float.TryParse(words[2], System.Globalization.CultureInfo.InvariantCulture, out float fy):
            {
                // F1 2차 시연: 제자리에서 (fx, fy) 방향으로 방향 전환 (목표 = 지금 무게중심)
                var fu = comps.SelectMany(c => c.Members).ToList();
                if (fu.Count == 0 || fx == 0 && fy == 0) return true;
                Vector2 fc = Vector2.Zero;
                foreach (var u in fu) fc += u.Pos;
                fc /= fu.Count;
                World.Move(Me, fu, fc, new FormSpec(0, 0, Vector2.Normalize(new Vector2(fx, fy)), 0));
                return true;
            }
            case "fmove" or "famove" when words.Length is 3 or 4 && float.TryParse(words[1], System.Globalization.CultureInfo.InvariantCulture, out float dx) && float.TryParse(words[2], System.Globalization.CultureInfo.InvariantCulture, out float dy):
                float wd = words.Length == 4 && float.TryParse(words[3], System.Globalization.CultureInfo.InvariantCulture, out float ww) ? ww : 0;
                var us = comps.SelectMany(c => c.Members).ToList();
                if (us.Count == 0) return true;
                Vector2 cen = Vector2.Zero;
                foreach (var u in us) cen += u.Pos;
                cen /= us.Count;
                var d = new Vector2(dx, dy);
                var spec = new FormSpec(0, 0, d.LengthSquared() > 0 ? Vector2.Normalize(d) : new Vector2(0, -1), wd);
                if (words[0] == "famove") { World.AttackMove(Me, us, cen + d, spec); Camera.CenterOn(cen + d * 0.5f); }   // 공격 이동 (틀 행동 — 접촉하면 지휘관이 실행), 카메라 = 가운데
                else { World.Move(Me, us, cen + d, spec); Camera.CenterOn(cen + d); }
                return true;
        }
        return false;
    }

    internal void RunConsoleCommand(string line)
    {
        var words = line.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string joined = string.Join(' ', words);
        if (words.Length >= 3 && words[0] == "after" && long.TryParse(words[1], out long afterTicks)) { deferredConsole.Add((World.Tick + afterTicks, string.Join(' ', words.Skip(2)))); return; }
        if (words.Length >= 2 && !Observer && FormationConsole(words)) return;
        if (joined == "keys")
        {
            var parts = KoreanDivision.UI.Bindings.All().Select(b => $"{b.Key} {b.Text}").ToList();
            for (int i = 0; i < parts.Count; i += 4) CommandBox.Print(string.Join(" · ", parts.Skip(i).Take(4)));
            return;
        }
        if (joined == "helpui") { HelpBox.Toggle(); return; }
        if (joined == "help" || joined == "?")
        {
            foreach (var h in new[] { S.ConsoleHelp1, S.ConsoleHelp2, S.ConsoleHelp4, S.ConsoleHelp6, S.ConsoleHelp7, S.ConsoleHelpKeys, S.ConsoleHelp5 }) CommandBox.Print(h);
            return;
        }
        if (words.Length >= 1 && words[0] == "volume")
        {
            if (words.Length == 2 && int.TryParse(words[1], out int vol)) Sfx.Volume = Math.Clamp(vol, 0, 100) / 100f;
            CommandBox.Print(string.Format(S.ConsoleVolume, (int)MathF.Round(Sfx.Volume * 100), Sfx.Muted ? S.ConsoleMuted : ""));
            return;
        }
        if (joined == "mute")
        {
            ToggleSound();
            CommandBox.Print(string.Format(S.ConsoleVolume, (int)MathF.Round(Sfx.Volume * 100), Sfx.Muted ? S.ConsoleMuted : ""));
            return;
        }
        if (words.Length >= 1 && words[0] == "props")   // F6 확인용: "props" = 개수 · "props goto" = 가장 빽빽한 소품 무리로 카메라
        {
            var items = World.Props.Items;
            CommandBox.Print($"props {items.Count} broken {World.Props.Destroyed}");
            if (words.Length == 2 && words[1] == "goto" && items.Count > 0)
            {
                Prop best = items[0]; int bn = -1;
                foreach (var p in items) { if (p.X < 20 || p.Y < 20 || p.X > World.Map.Width - 20 || p.Y > World.Map.Height - 20) continue; var near = items.Where(q => Vector2.DistanceSquared(p.Pos, q.Pos) < 36f).ToList(); int n = near.Count + 4 * near.Select(q => q.Type).Distinct().Count() + 6 * near.Count(q => q.Type.StartsWith("tree")); if (n > bn) { bn = n; best = p; } }
                Camera.CenterOn(best.Pos);
                Console.WriteLine($"props best {best.X:0.0},{best.Y:0.0}");
            }
            return;
        }
        if (words.Length == 2 && words[0] == "zoom" && float.TryParse(words[1], System.Globalization.CultureInfo.InvariantCulture, out float z))
        {
            Camera.SetZoom(z);
            return;
        }
        if (words.Length is 3 or 4 && words[0] == "org" && int.TryParse(words[1], out int perCo) && int.TryParse(words[2], out int nCo) && !Observer)
        {
            // 편제 시연용: 화면 중앙에서 가까운 내 보병 perCo*nCo 기를 nCo 개 중대로 편성 (대대 번호 words[3], 기본 1) 하고 선택 · 앞으로 행군
            int regNo = words.Length == 4 && int.TryParse(words[3], out int rn) ? rn : 1;
            var pool = World.Units.Where(u => u.Owner == Me && u.Alive && u.Company == null && u.Weapon != null && !u.Def.IsArmored)
                                  .OrderBy(u => Vector2.DistanceSquared(u.Pos, Camera.CenterWorld)).ThenBy(u => u.Id).Take(perCo * nCo).ToList();
            for (int i = 0; i + perCo <= pool.Count; i += perCo) World.FormCompany(Me, pool.Skip(i).Take(perCo).ToList(), regNo);
            if (pool.Count > 0)
            {
                SetSelection(pool);
                Camera.CenterOn(pool[0].Pos);
                World.Move(Me, pool, pool[0].Pos + new Vector2(14, 0), new FormSpec((int)DefaultType, (int)DefaultOrder, new Vector2(1, 0)));
            }
            return;
        }
        if (words.Length == 2 && words[0] == "firemode" && !Observer)   // N73 확인용: 선택한 무장 유닛 사격 통제 (free | hold | return)
        {
            var fm = words[1] switch { "hold" => FireMode.Hold, "return" => FireMode.ReturnFire, _ => FireMode.Free };
            World.SetFireMode(Me, OwnUnits().Where(u => u.Weapon != null).ToList(), fm);
            return;
        }
        if (words.Length == 3 && words[0] == "rally" && !Observer && float.TryParse(words[1], System.Globalization.CultureInfo.InvariantCulture, out float rdx) && float.TryParse(words[2], System.Globalization.CultureInfo.InvariantCulture, out float rdy))
        {
            // N79a 확인용: 선택한 중대의 집결 지점을 그 중대 중심에서 (dx, dy) 칸 떨어진 곳에 지정
            foreach (var rc in SelectedCompanies()) World.SetRally(Me, new[] { rc.Id }, rc.Members.Aggregate(Vector2.Zero, (sum, m) => sum + m.Pos) / Math.Max(1, rc.Members.Count) + new Vector2(rdx, rdy));
            return;
        }
        if (words.Length == 2 && words[0] == "select")
        {
            // 화면 중앙에서 가까운 내 유닛 중 그 종류를 골라 선택 (확인 · 시연용)
            var pick = World.Units.Where(u => u.Owner == Me && u.Def.Key == words[1])
                                  .OrderBy(u => Vector2.DistanceSquared(u.Pos, Camera.CenterWorld)).Take(1).ToList();
            if (pick.Count > 0) { SetSelection(pick); Camera.CenterOn(pick[0].Pos); }
            return;
        }
                if (joined == "fps")
        {
            ShowPerf = !ShowPerf;
            CommandBox.Print(string.Format(S.ConsoleFps, ShowPerf ? S.On : S.Off));
            return;
        }
        // 저장 / 불러오기: 관전 · 교전 테스트에서도 쓴다 (리플레이 재생 중에만 저장 불가)
        if (words.Length >= 1 && words[0] == "save") { ConsoleSave(words.Length > 1 ? words[1] : null); return; }
        if (words.Length >= 1 && words[0] == "load") { ConsoleLoad(words.Length > 1 ? words[1] : null); return; }
        if (joined == "saves") { ConsoleSaves(); return; }
        if (Observer) { CommandBox.Print(S.ConsoleReplay, Palette.Warning); return; }
        if (TryMissionConsole(words)) return;   // mission win|lose (GameMission.cs)
        if (joined is "show all maps" or "showallmaps" or "show all map" or "reveal")
        {
            bool on = !World.Faction(Me).RevealAll;
            World.Issue(new RevealMapCommand(Me, on));
            CommandBox.Print(on ? S.ConsoleRevealOn : S.ConsoleRevealOff, Palette.Warning);
            return;
        }
        if (words.Length >= 2 && words[0] == "spawn")
        {
            int n = words.Length >= 3 && int.TryParse(words[2], out int k) ? Math.Clamp(k, 1, 200) : 1;
            if (!Defs.Current.Units.ContainsKey(words[1]))
            {
                CommandBox.Print(string.Format(S.ConsoleBadUnit, words[1], string.Join(", ", Defs.Current.Units.Keys)), Palette.Warning);
                return;
            }
            World.Issue(new DebugSpawnCommand(Me, words[1], Camera.CenterWorld, n));
            CommandBox.Print(string.Format(S.ConsoleSpawned, words[1], n), Palette.Warning);
            return;
        }
        CommandBox.Print(string.Format(S.ConsoleUnknown, line), Palette.Warning);
    }

    /// <summary>[시연] 본진 앞 교전.</summary>
    void SetupDemo(Building hq)
    {
        var inf = Defs.Current.Units["infantry"];
        // 사령부에서 맵 중앙 쪽으로, 반경 6타일이 전부 트인 땅을 찾아 교전 장면을 배치 (물가 · 바위 회피, 인원수 무관)
        Vector2 toCenter = Vector2.Normalize(new Vector2(World.Map.Width / 2f, World.Map.Height / 2f) - hq.Pos);
        Vector2 front = OpenSpot(hq.Pos, toCenter, 6) ?? hq.Pos + toCenter * 10;
        Vector2 side = new(-toCenter.Y, toCenter.X);
        var mine = Enumerable.Range(0, 12).Select(i => World.SpawnUnit(inf, Me, front - toCenter * 3 + side * (i % 4 * 0.8f - 1.2f) - toCenter * (i / 4 * 0.8f))).ToList();
        var theirs = Enumerable.Range(0, 10).Select(i => World.SpawnUnit(i % 3 == 0 ? Defs.Current.Units["scout"] : inf, Me == 2 ? 3 : 2,
            front + toCenter * 4 + side * (i % 4 * 0.8f - 1.2f) + toCenter * (i / 4 * 0.8f))).ToList();
        World.AttackMove(Me, mine, front + toCenter * 6);
        World.AttackMove(Me == 2 ? 3 : 2, theirs, hq.Pos);
        Camera.CenterOn(front);
    }

    /// <summary>from 에서 dir 쪽(±70°)으로 6~24타일 범위에서 반경 r 이 모두 이동 가능한 지점.</summary>
    /// <summary>[확인용 장면] KD_SCENE=mgaim + --demo: 표적 하나를 16방위에서 둘러싼 경기관총 (N2 사격 방향 · 그림 정합 스크린샷, KD_AIMDEBUG=1 과 같이).</summary>
    void SetupMgAimScene(Building hq)
    {
        Vector2 toCenter = Vector2.Normalize(new Vector2(World.Map.Width / 2f, World.Map.Height / 2f) - hq.Pos);
        Vector2 c = OpenSpot(hq.Pos, toCenter, 10) ?? hq.Pos + toCenter * 12;
        int foeOwner = Me == 2 ? 3 : 2;
        var foe = World.SpawnUnit(Defs.Current.Units["infantry"], foeOwner, c);
        foe.Weapon = null; foe.Hp = 1e9f;
        World.Hold(foeOwner, new[] { foe });
        var mgDef = Defs.Current.Units["heavy_machinegunner"];
        var mgs = new List<Unit>();
        float ringR = float.TryParse(Environment.GetEnvironmentVariable("KD_SCENE_R"), System.Globalization.CultureInfo.InvariantCulture, out float rr) ? rr : 7f;   // 확인용: 최대 확대에서 한 화면에 담기 (예 2.2)
        for (int k = 0; k < 16; k++)
        {
            float a = k * MathF.Tau / 16f;
            var u = World.SpawnUnit(mgDef, Me, c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * ringR);
            u.Facing = a;   // 바깥을 보고 시작 → 거치 · 회전 뒤 표적을 겨눈다
            mgs.Add(u);
        }
        World.Hold(Me, mgs);
        Camera.CenterOn(c);
    }

    /// <summary>[확인용 장면] KD_SCENE=pronemove + --demo --sim 8: 맥심 사격을 받으며 옆으로 이동하는 중대 (엎드린 병사가 대열에 끌려가는지, docs/art_review/prone_move.png). KD_PRONE_OLD=1 = 옛 동작 (proneHoldInFormation 끔).</summary>
    void SetupProneMoveScene(Building hq)
    {
        if (Environment.GetEnvironmentVariable("KD_PRONE_OLD") == "1") Defs.Current.CombatRules.ProneHoldInFormation = false;
        Vector2 toCenter = Vector2.Normalize(new Vector2(World.Map.Width / 2f, World.Map.Height / 2f) - hq.Pos);
        Vector2 c = OpenSpot(hq.Pos, toCenter, 10) ?? hq.Pos + toCenter * 12;
        Vector2 side = new(-toCenter.Y, toCenter.X);
        int foeOwner = Me == 2 ? 3 : 2;
        var us = new List<Unit>();
        for (int i = 0; i < 20; i++) us.Add(World.SpawnUnit(Defs.Current.Units["infantry"], Me, c + side * (i % 10 - 4.5f) * 0.9f - toCenter * (i / 10) * 0.9f));
        us.Add(World.SpawnUnit(Defs.Current.Units["officer"], Me, c - toCenter * 3f));
        foreach (var u in us) u.Hp = 1e9f;
        World.FormCompany(Me, us);
        for (int k = 0; k < 2; k++) { var f = World.SpawnUnit(Defs.Current.Units["heavy_maxim"], foeOwner, c + toCenter * 20 + side * (k * 3f - 1.5f)); f.Hp = 1e9f; World.Hold(foeOwner, new[] { f }); }
        for (int i = 0; i < 12; i++) us[i * 5 % 20].Suppression = (short)(i % 2 == 0 ? 450 : 800);
        World.Move(Me, us, c + side * 16f, new FormSpec((int)FormType.Line, 0, toCenter));
        Camera.CenterOn(c + side * 8f);
    }

    /// <summary>[확인용 장면] KD_SCENE=ringshot + --demo: 거치 중 맥심 · 방열 중 야포 · 보병 (진행 고리 스크린샷, --sim 1 정도).</summary>
    void SetupRingShotScene(Building hq)
    {
        Vector2 toCenter = Vector2.Normalize(new Vector2(World.Map.Width / 2f, World.Map.Height / 2f) - hq.Pos);
        Vector2 c = OpenSpot(hq.Pos, toCenter, 10) ?? hq.Pos + toCenter * 12;
        Vector2 side = new(-toCenter.Y, toCenter.X);
        int foeOwner = Me == 2 ? 3 : 2;
        var mine = new List<Unit>();
        string[] ids = { "heavy_maxim", "heavy_maxim", "heavy_field_gun", "heavy_infantry_gun" };
        for (int i = 0; i < ids.Length; i++) { var u = World.SpawnUnit(Defs.Current.Units[ids[i]], Me, c + side * ((i - 1.5f) * 2.2f)); u.Facing = MathF.Atan2(toCenter.Y, toCenter.X); mine.Add(u); }
        for (int i = 0; i < 4; i++) { var f = World.SpawnUnit(Defs.Current.Units["infantry"], foeOwner, c + toCenter * 14 + side * ((i - 1.5f) * 1.5f)); f.Hp = 1e9f; World.Hold(foeOwner, new[] { f }); }
        World.Hold(Me, mine);
        Camera.CenterOn(c);
    }

    /// <summary>[확인용 장면] KD_SCENE=allunits + --demo: 모든 유닛 종류를 격자로 세움 (N8 최대 확대 LOD0 확인). KD_SCENE_COMPANY=1 이면 짝수 번째를 중대로 편성 (N9 축소 휘장 · 점 확인).</summary>
    void SetupAllUnitsScene(Building hq)
    {
        Vector2 toCenter = Vector2.Normalize(new Vector2(World.Map.Width / 2f, World.Map.Height / 2f) - hq.Pos);
        Vector2 c = OpenSpot(hq.Pos, toCenter, 10) ?? hq.Pos + toCenter * 12;
        var defs = Defs.Current.Units.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value).ToList(); var names = Defs.Current.Units.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).ToList();
        int cols = 6; float gap = float.TryParse(Environment.GetEnvironmentVariable("KD_SCENE_GAP"), System.Globalization.CultureInfo.InvariantCulture, out float g) ? g : 3.5f;
        bool comp = Environment.GetEnvironmentVariable("KD_SCENE_COMPANY") == "1";
        var all = new List<Unit>(); var inCo = new List<Unit>();
        int page = int.TryParse(Environment.GetEnvironmentVariable("KD_SCENE_PAGE"), out int pg) ? pg : -1;   // 한 쪽 6종 (3x2) 만 — 최대 확대에서 한 화면에 담기
        if (page >= 0) { defs = defs.Skip(page * 6).Take(6).ToList(); names = names.Skip(page * 6).Take(6).ToList(); cols = 3; }
        for (int i = 0; i < defs.Count; i++)
        {
            var u = World.SpawnUnit(defs[i], Me, c + (page >= 0 ? new Vector2((i % 3 - 1) * gap * 1.4f, (i / 3 - 0.5f) * gap) : new Vector2((i % cols - (cols - 1) / 2f) * gap, (i / cols - defs.Count / cols / 2f) * gap)));
            u.Facing = MathF.PI / 2; all.Add(u);
            Console.Error.WriteLine($"[scene] {i} {names[i]}");
            if (comp && i % 2 == 0) inCo.Add(u);
        }
        if (Environment.GetEnvironmentVariable("KD_SCENE_FIGHT") == "1")   // 표적 하나를 두고 교전 (걷기 · 사격 · 피격 모션 확인)
        {
            int foeOwner = Me == 2 ? 3 : 2;
            var foe = World.SpawnUnit(Defs.Current.Units["infantry"], foeOwner, c + new Vector2(0, -7f));
            foe.Weapon = null; foe.Hp = 1e9f;
            World.Hold(foeOwner, new[] { foe });
            World.AttackMove(Me, all, c + new Vector2(0, -7f));
        }
        else if (Environment.GetEnvironmentVariable("KD_SCENE_FIGHT") == "2")   // 제자리 사격 (표적 5칸)
        {
            int foeOwner = Me == 2 ? 3 : 2;
            var foes = new List<Unit>();
            for (int k = 0; k < 6; k++) { var fo = World.SpawnUnit(Defs.Current.Units["infantry"], foeOwner, c + new Vector2(k - 2.5f, -(float.TryParse(Environment.GetEnvironmentVariable("KD_SCENE_FOEDIST"), System.Globalization.CultureInfo.InvariantCulture, out float fd) ? fd : 5f))); fo.Weapon = null; fo.Hp = 1e9f; foes.Add(fo); }
            World.Hold(foeOwner, foes);
            World.Hold(Me, all);
        }
        else World.Hold(Me, all);
        if (comp) foreach (var u in inCo) World.FormCompany(Me, new[] { u }, 1);
        Camera.CenterOn(c);
    }

    Vector2? OpenSpot(Vector2 from, Vector2 dir, int r)
    {
        foreach (float dist in new[] { 12f, 9f, 15f, 18f, 7f, 21f, 24f })
        foreach (float ang in new[] { 0f, 0.3f, -0.3f, 0.6f, -0.6f, 0.9f, -0.9f, 1.2f, -1.2f })
        {
            var d = new Vector2(dir.X * MathF.Cos(ang) - dir.Y * MathF.Sin(ang), dir.X * MathF.Sin(ang) + dir.Y * MathF.Cos(ang));
            var p = from + d * dist;
            bool open = true;
            for (int y = -r; y <= r && open; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r * r && !World.Map.IsWalkable((int)p.X + x, (int)p.Y + y)) { open = false; break; }
            if (open) return p;
        }
        return null;
    }

    // ---------------------------------------------------------------- 저장 / 불러오기

    /// <summary>
    /// 지금 상태를 saves/&lt;name&gt;.kds 로 저장 (시뮬레이션 스레드 = 이 스레드, 틱 사이). 실패해도 게임은 계속된다.
    /// 리플레이 재생 중에는 저장할 수 없다 (월드가 기록에서 만들어진 것이라 의미가 없음). 관전 · 교전 테스트는 저장된다.
    /// </summary>
    public bool SaveGame(string name, bool auto = false)
    {
        if (IsReplay) { ShowNotice(S.SaveNotInReplay); return false; }
        string clean = SaveManager.Sanitize(name);
        try
        {
            var sw = Stopwatch.StartNew();
            Snapshot.SaveToFile(World, new SnapshotMeta(clean, spectate ? 0 : humanFaction, spectate, aiName, auto), SaveManager.PathOf(clean));
            LastSaveMs = (int)sw.ElapsedMilliseconds;
            SavesDone++;
            if (!auto) ShowNotice(string.Format(S.SaveDone, clean, LastSaveMs));
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"저장 실패: {e}");
            ShowNotice(string.Format(S.SaveFailed, e.Message));
            return false;
        }
    }

    public int LastSaveMs { get; private set; }
    /// <summary>이 게임에서 성공한 저장 횟수 (자동 저장 포함 — 퍼즈 · 테스트 확인용).</summary>
    public int SavesDone { get; private set; }
    public bool QuickSave() => SaveGame(SaveManager.QuickName);

    /// <summary>저장 파일을 읽어 두고 (실패하면 알림만, 지금 게임은 그대로) Program 이 새 게임으로 전환하도록 PendingLoad 에 둔다.</summary>
    public bool RequestLoad(string name)
    {
        string path = SaveManager.PathOf(name);
        if (!File.Exists(path)) { ShowNotice(string.Format(S.LoadMissing, SaveManager.Sanitize(name))); return false; }
        return RequestLoadFile(path);
    }

    public bool RequestLoadFile(string path)
    {
        try
        {
            PendingLoad = Snapshot.LoadFile(path, Defs.Current);
            return true;
        }
        catch (SnapshotException e)
        {
            ShowNotice(string.Format(S.LoadFailed, e.Message));
            return false;
        }
    }

    public bool QuickLoad() => RequestLoad(SaveManager.QuickName);

    /// <summary>게임 시간 10분마다 자동 저장 (autosave_1..3 을 돌려 쓴다).</summary>
    void AutosaveIfDue()
    {
        if (IsReplay || World.Winner != 0 || SaveManager.AutosaveTicks <= 0) return;
        long slot = World.Tick / SaveManager.AutosaveTicks;
        if (slot <= autosaveSlot) return;
        autosaveSlot = slot;
        SaveGame(SaveManager.AutosaveName(World.Tick), auto: true);
    }

    void ConsoleSave(string? name)
    {
        if (IsReplay) { CommandBox.Print(S.SaveNotInReplay, Palette.Warning); return; }
        name = string.IsNullOrEmpty(name) ? SaveManager.TimestampName() : name;
        if (SaveGame(name)) CommandBox.Print(string.Format(S.SaveDone, SaveManager.Sanitize(name), LastSaveMs));
    }

    void ConsoleLoad(string? name)
    {
        if (name == null)
        {
            var newest = File.Exists(SaveManager.PathOf(SaveManager.QuickName)) ? null : SaveManager.Newest();
            if (newest != null) { if (!RequestLoadFile(newest.Path)) CommandBox.Print(string.Format(S.LoadFailed, newest.Name), Palette.Warning); return; }
            name = SaveManager.QuickName;
        }
        if (!RequestLoad(name)) CommandBox.Print(string.Format(S.LoadMissing, SaveManager.Sanitize(name)), Palette.Warning);
    }

    void ConsoleSaves()
    {
        var all = SaveManager.List();
        if (all.Count == 0) { CommandBox.Print(S.ConsoleNoSaves); return; }
        CommandBox.Print(string.Format(S.ConsoleSaves, all.Count, string.Join(", ", all.Take(5).Select(e => e.Header == null ? $"{e.Name} ({S.SaveUnreadable})" : e.Name))));
    }

    public void ShowNotice(string text)
    {
        notice = text;
        noticeUntil = Now + 2.5;
    }

    // ---------------------------------------------------------------- 그리기

    public void Draw(string? screenshotPath = null)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Palette.Void);

        Rectangle vp = Hud.Viewport;
        Raylib.BeginScissorMode((int)vp.X, (int)vp.Y, (int)vp.Width, (int)vp.Height);
        Raylib.BeginMode2D(Camera.Cam);
        mapRenderer.Draw(Camera, ShowGrid);
        BuildingRenderer.DrawAll(World, Camera, ViewSeat, hover);
        DrawMarkers();
        DrawMissionWorld();   // 미션 구역 · 배치 (GameMission.cs)
        UnitRenderer.DrawUnits(World, Camera, ViewSeat, hover, false);
        UnitRenderer.DrawEffects(World, ViewSeat);
        FogRenderer.ExploredAlpha = WeatherFx.ExploredAlphaFor(World.Weather.Time);   // 밤: 시야 밖 지형을 조금 밝게 (색조와 겹침)
        FogRenderer.DrawWorld(Camera, ViewSeat);
        LastSeen.Update(World, ViewSeat);   // 마지막 목격 위치 (화면 전용)
        LastSeen.Draw();
        DrawFrontLinePreview();
        DrawFinalLines();   // arty3
        if (losShow) { LosOverlay.Update(World, OwnUnits(), mouseWorld, losCursor, Camera.VisibleWorld(), frameCounter++); LosOverlay.Draw(); }   // N75 사선 표시 (표시 전용)
        Raylib.EndMode2D();
        weatherFx.Draw(World, Camera, vp, Raylib.GetFrameTime());   // 날씨 · 시간대 색조 · 안개 · 비 · 눈 (화면 전용)
        if (Camera.Zoom < UnitRenderer.DotZoom) UnitSymbols.Draw(World, Camera, ViewSeat, mouse, mouseInViewport, vp);
        DrawCompanyFlags();
        DrawCompanyZones();
        if (losShow) Ui.Text(losCursor ? S.LosLegendCursor : S.LosLegendSel, Hud.Viewport.X + 12, Hud.Viewport.Y + 40, 16, Palette.Accent);
        DrawDragBox();
        DrawHoverInfo();
        Raylib.EndScissorMode();

        Spectator.Draw(mouse);
        Hud.Draw(mouse);
        CommandBox.Draw(Hud.Viewport);
        if (World.BattleMode) BattlePanel.Draw(this, Hud.Viewport);
        DrawMissionOverlay();   // 미션 HUD · 배치 패널 · 결과 (GameMission.cs)
        if (ShowPerf)
        {
            string perf = $"Update {FrameUpdateMs:0.00}ms · Draw {FrameDrawMs:0.00}ms · sim {LastStepMs:0.00}ms · {Raylib.GetMonitorRefreshRate(Raylib.GetCurrentMonitor())}Hz · units {World.Units.Count} · {Render.LodAtlas.ShortInfo}";
            Vector2 pm = Ui.Measure(perf, 16);
            Raylib.DrawRectangleRec(new Rectangle(Hud.Viewport.X + Hud.Viewport.Width - pm.X - 24, Hud.Viewport.Y + 8, pm.X + 16, 24), Palette.Alpha(Palette.PanelDark, 210));
            Ui.Text(perf, Hud.Viewport.X + Hud.Viewport.Width - pm.X - 16, Hud.Viewport.Y + 12, 16, Palette.Accent);
        }

        HelpBox.Draw(mouse);
        if (screenshotPath != null)
        {
            Rlgl.DrawRenderBatchActive();
            Image img = Raylib.LoadImageFromScreen();
            Raylib.ExportImage(img, screenshotPath);
            Raylib.UnloadImage(img);
        }
        Raylib.EndDrawing();
    }

    void DrawMarkers()
    {
        double now = Now;
        foreach (var (from, to, time, color) in orderLines)
        {
            float t = (float)((now - time) / 1.0);
            int a = (int)(150 * (1 - t) * (1 - t));
            foreach (var f in from)
                Raylib.DrawLineEx(f * Config.TileSize, to * Config.TileSize, MathF.Max(1f, 1.5f / Camera.Zoom), Palette.Alpha(color, a));
        }
        DrawGroundFireTargets();
        DrawRallyFlags();
        foreach (var (pos, time, color) in markers)
        {
            float t = (float)((now - time) / 0.6);
            float r = (1 - t) * 14 + 4;
            Vector2 p = pos * Config.TileSize;
            Raylib.DrawRing(p, r, r + 2f / Camera.Zoom, 0, 360, 24, Palette.Alpha(color, (int)(255 * (1 - t))));
        }
    }

    /// <summary>지점 포격 중인 내 포의 목표 표시 (GroundFire.cs): 빨간 조준 원 + 십자, 선택한 포는 포에서 목표까지 가는 선도. 선택 여부와 무관하게 명령이 살아 있는 동안 계속 표시.</summary>
    void DrawGroundFireTargets()
    {
        if (Observer || Me <= 0) return;
        float th = MathF.Max(1.2f, 2f / Camera.Zoom);
        var drawn = new HashSet<Vector2>();
        foreach (var u in World.Units)
        {
            if (!u.Alive || u.Owner != Me || u.Order != UnitOrder.GroundFire) continue;
            Vector2 aim = World.GroundAimPoint(u);   // 이동탄막은 전진하는 착탄선 (N79a)
            Vector2 p = aim * Config.TileSize;
            if (u.Selected) Raylib.DrawLineEx(u.Pos * Config.TileSize, p, th * 0.6f, Palette.Alpha(Palette.HpLow, 70));
            if (!drawn.Add(aim)) continue;
            float splash = MathF.Max(1f, u.Weapon?.Splash ?? 1f) * Config.TileSize;
            float pulse = 1f + 0.08f * MathF.Sin((float)Now * 5f);
            var c = Palette.Alpha(Palette.HpLow, 230);
            Raylib.DrawRing(p, splash * pulse, splash * pulse + th, 0, 360, 32, c);
            float arm = splash * 1.5f;
            Raylib.DrawLineEx(p - new Vector2(arm, 0), p + new Vector2(arm, 0), th, c);
            Raylib.DrawLineEx(p - new Vector2(0, arm), p + new Vector2(0, arm), th, c);
        }
    }

    /// <summary>선택한 내 중대의 집결 지점 깃발 (N79a): 작은 깃대 + 삼각 깃발, 지정 대기 (J) 중이면 모든 중대 것을 보여 준다.</summary>
    void DrawRallyFlags()
    {
        if (Observer || Me <= 0) return;
        var comps = SelectedCompanies();
        if (comps.Count == 0) return;
        float s = Config.TileSize;
        float th = MathF.Max(1.2f, 2f / Camera.Zoom);
        foreach (var c in comps)
        {
            if (!c.HasRally) continue;
            Vector2 p = c.RallyPoint * s;
            float h = 2.2f * s;
            var col = Palette.Alpha(Palette.Accent, 235);
            Raylib.DrawLineEx(p, p + new Vector2(0, -h), th, col);
            Raylib.DrawTriangle(p + new Vector2(0, -h), p + new Vector2(0, -h * 0.55f), p + new Vector2(h * 0.55f, -h * 0.78f), col);
            Raylib.DrawTriangle(p + new Vector2(0, -h), p + new Vector2(h * 0.55f, -h * 0.78f), p + new Vector2(0, -h * 0.55f), col);
            Raylib.DrawRing(p, 0.5f * s, 0.5f * s + th * 0.8f, 0, 360, 20, Palette.Alpha(Palette.Accent, 140));
        }
    }

    /// <summary>arty3: 선택한 내 중기관총의 최후 저지선 사격선 (유닛 → 지정 방향, 사거리까지; 월드 좌표계 안에서 호출, 표시 전용).</summary>
    void DrawFinalLines()
    {
        float th = MathF.Max(1f, 1.5f / Camera.Zoom);
        foreach (var u in OwnUnits())
        {
            if (u.FireMode != FireMode.FinalLine || u.Weapon == null) continue;
            Vector2 d = u.FinalLinePoint - u.Pos;
            if (d.LengthSquared() < 1e-6f) continue;
            Vector2 end = u.Pos + Vector2.Normalize(d) * u.Weapon.Range;
            Raylib.DrawLineEx(u.Pos * Config.TileSize, end * Config.TileSize, th, Palette.Alpha(Palette.HpLow, 160));
        }
    }

    /// <summary>우클릭 드래그 중: 전선 선분 + 도착 슬롯 점 (월드 좌표계 안에서 호출).</summary>
    void DrawFrontLinePreview()
    {
        if (rightStartScreen is not Vector2 rs || rightStartWorld is not Vector2 rw || !Input.IsMouseButtonDown(MouseButton.Right)) return;
        if (Vector2.Distance(mouse, rs) < Config.DragThreshold) return;
        var units = OwnUnits();
        if (units.Count == 0) return;
        var spec = FrontLineSpec(units, rw, mouseWorld, out var mid);
        float th = MathF.Max(1.5f, 2.5f / Camera.Zoom);
        Raylib.DrawLineEx(rw * Config.TileSize, mouseWorld * Config.TileSize, th, Palette.Alpha(Palette.Select, 220));
        Raylib.DrawLineEx(mid * Config.TileSize, (mid + spec.Dir * 1.5f) * Config.TileSize, th, Palette.Alpha(Palette.Accent, 220));   // 진행 방향 표시
        float r = MathF.Max(2f, 3f / Camera.Zoom);
        foreach (var p in World.PreviewSlots(units, mid, spec))
            Raylib.DrawCircleV(p * Config.TileSize, r, Palette.Alpha(Palette.Select, 200));
    }

    /// <summary>행군 중인 내 중대의 깃발 (화면 좌표: 깃대 + 세력색 직사각형 + 중대 번호).</summary>
    void DrawCompanyFlags()
    {
        if (Observer || Me <= 0) return;
        Color team = Palette.Player(Me);
        foreach (var c in World.Companies)
        {
            if (c.Owner != Me || !c.Marching || c.FlagPos is not Vector2 fp) continue;
            Vector2 s = Camera.WorldToScreen(fp);
            if (!Ui.Contains(Hud.Viewport, s)) continue;
            Raylib.DrawLineEx(s, new Vector2(s.X, s.Y - 28), 2, Palette.Alpha(Palette.Text, 230));
            var flag = new Rectangle(s.X + 1, s.Y - 28, 22, 14);
            Raylib.DrawRectangleRec(flag, team);
            Raylib.DrawRectangleLinesEx(flag, 1, Palette.Alpha(Palette.PanelDark, 230));
            Ui.TextCentered(c.Number.ToString(), flag, 14, Palette.Text);
        }
    }

    /// <summary>
    /// F1 2차 표시 (⑤, 화면 전용): 고른 내 중대의 접적 구역 테두리 (구성원 구역 중심을 중대 방향 틀로 감싼 사각형 + 구역 반경) 와 접적 상태 표지 (접적 = 빨강 · 이탈 = 주황).
    /// </summary>
    void DrawCompanyZones()
    {
        if (Observer || Me <= 0) return;
        float R = World.Defs.CombatRules.ContactZoneRadius;
        foreach (var c in SelectedCompanies())
        {
            var st = World.ContactOf(c);
            Vector2 fwd = c.Facing.LengthSquared() > 1e-4f ? Vector2.Normalize(c.Facing) : new Vector2(0, -1), right = new(-fwd.Y, fwd.X);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            Vector2 cen = Vector2.Zero; int n = 0;
            foreach (var u in c.Members)
            {
                if (!u.Alive) continue;
                cen += u.Pos; n++;
                if (!u.HasZone) continue;
                float x = Vector2.Dot(u.ZoneCenter, right), y = Vector2.Dot(u.ZoneCenter, fwd);
                x0 = MathF.Min(x0, x); x1 = MathF.Max(x1, x); y0 = MathF.Min(y0, y); y1 = MathF.Max(y1, y);
            }
            if (n == 0) continue;
            cen /= n;
            Color col = st == ContactState.Contact ? Palette.HpLow : Palette.Warning;
            if (x0 <= x1)
            {
                // 구역 테두리: 사각형 (방향 틀) 을 구역 반경만큼 넓힘
                x0 -= R; x1 += R; y0 -= R; y1 += R;
                Vector2 P(float x, float y) => Camera.WorldToScreen(right * x + fwd * y);
                Vector2 a = P(x0, y0), b = P(x1, y0), cc = P(x1, y1), d = P(x0, y1);
                Color lc = Palette.Alpha(st == ContactState.None ? Palette.TextDim : col, 140);
                Raylib.DrawLineEx(a, b, 2, lc); Raylib.DrawLineEx(b, cc, 2, lc); Raylib.DrawLineEx(cc, d, 2, lc); Raylib.DrawLineEx(d, a, 2, lc);
            }
            if (st == ContactState.None) continue;
            // 접적 표지: 중대 무게중심 위
            Vector2 s = Camera.WorldToScreen(cen);
            if (!Ui.Contains(Hud.Viewport, s)) continue;
            string label = st == ContactState.Contact ? S.ContactBadge : S.DisengageBadge;
            Vector2 m = Ui.Measure(label, 14);
            var r = new Rectangle(s.X - m.X / 2 - 6, s.Y - 46, m.X + 12, m.Y + 4);
            Raylib.DrawRectangleRec(r, Palette.Alpha(col, 220));
            Raylib.DrawRectangleLinesEx(r, 1, Palette.Alpha(Palette.PanelDark, 230));
            Ui.TextCentered(label, r, 14, Palette.Text);
        }
    }

    void DrawDragBox()
    {
        if (dragStartWorld is not Vector2 s || !Input.IsMouseButtonDown(MouseButton.Left)) return;
        Vector2 a = Camera.WorldToScreen(s);
        var r = new Rectangle(MathF.Min(a.X, mouse.X), MathF.Min(a.Y, mouse.Y), MathF.Abs(mouse.X - a.X), MathF.Abs(mouse.Y - a.Y));
        if (r.Width < Config.DragThreshold && r.Height < Config.DragThreshold) return;
        Raylib.DrawRectangleRec(r, Palette.SelectFill);
        Raylib.DrawRectangleLinesEx(r, 1, Palette.Select);
    }

    void DrawHoverInfo()
    {
        if (!mouseInViewport) return;
        var (tx, ty) = GameMap.TileOf(mouseWorld);
        if (!World.Map.InBounds(tx, ty)) return;
        string text;
        if (hover != null)
            text = $"{hover.Name}  ·  {OwnerName(hover.Owner)}";
        else if (World.Fog.IsExplored(ViewSeat, tx, ty))
        {
            var info = Terrains.Get(World.Map.Get(tx, ty));
            text = $"{S.Tile} ({tx}, {ty})  ·  {info.Name}{(World.Map.IsHigh(tx, ty) ? "  ·  " + S.HighGround : "")}  ·  {(World.Map.IsWalkable(tx, ty) ? S.Walkable : S.Blocked)}";
        }
        else return;
        Vector2 m = Ui.Measure(text, 16);
        var r = new Rectangle(Hud.Viewport.X + 8, Hud.Viewport.Y + Hud.Viewport.Height - m.Y - 14, m.X + 16, m.Y + 8);
        Raylib.DrawRectangleRec(r, Palette.Alpha(Palette.PanelDark, 200));
        Ui.Text(text, r.X + 8, r.Y + 4, 16, Palette.Text);
    }

    /// <summary>게임 종료 시 리플레이 저장: 실행 폴더/replays/yyyyMMdd_HHmmss.kdr.json (최근 20개 유지).</summary>
    public string? SaveReplay()
    {
        if (!saveReplay || World.Tick == 0) return null;
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "replays");
            string path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}.kdr.json");
            ReplayFile.FromWorld(World, spectate ? 0 : Me).Save(path);
            foreach (var old in new DirectoryInfo(dir).GetFiles("*.kdr.json").OrderByDescending(f => f.Name).Skip(20)) old.Delete();
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"리플레이 저장 실패: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        Sfx.SetAmbient(null);   // 날씨 환경음 끔
        Sfx.Silent = false;   // 교전 테스트 무음 해제 (시작 화면 · 다음 게임)
        mapRenderer.Dispose();
        FogRenderer.Dispose();
        Hud.Dispose();
        if (cursor != MouseCursor.Default) Raylib.SetMouseCursor(MouseCursor.Default);
    }
}
