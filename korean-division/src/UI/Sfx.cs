using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace KoreanDivision.UI;

/// <summary>
/// 효과음 (assets/sounds/&lt;테마&gt;/sounds.json — 키 → 파일 변형 · 음량 · 동시 재생 수 · 최소 간격).
/// 화면 표현일 뿐 시뮬레이션과 무관 (리플레이 · 결정성에 영향 없음). 오디오 장치가 없거나 초기화 전이면 아무것도 하지 않는다.
///  · 같은 키는 maxVoices 개까지만 동시에, minInterval 초 간격 이상으로만 재생 (수백 기 교전에서도 뭉개지지 않게)
///  · PlayAt: 화면 안이면 원래 크기, 화면 밖이면 거리에 따라 작아지다 끊김. 좌우 위치로 좌우 소리 나눔
/// </summary>
public static class Sfx
{
    sealed class SoundDef
    {
        [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
        [JsonPropertyName("volume")] public float Volume { get; set; } = 1;
        [JsonPropertyName("maxVoices")] public int MaxVoices { get; set; } = 2;
        [JsonPropertyName("minInterval")] public float MinInterval { get; set; } = 0.05f;
        /// <summary>true = 반복 재생 고리 (Music 스트림, 첫 파일만). Loop/LoopAt 로 매 프레임 세기를 알려 주면 켜지고, 끊기면 서서히 꺼진다.</summary>
        [JsonPropertyName("loop")] public bool Loop { get; set; }
        /// <summary>&gt;0 이고 "&lt;키&gt;_far" 가 있으면: 카메라 중심에서 이 거리(칸) 보다 멀거나 화면 밖인 소리는 _far 변형을 쓴다.</summary>
        [JsonPropertyName("farDist")] public float FarDist { get; set; }
        /// <summary>true = 프레임당 시작 예산이 다 차도 조금 더 허용 (포 · 폭발).</summary>
        [JsonPropertyName("important")] public bool Important { get; set; }
        [JsonIgnore] public string Dir { get; set; } = "";
    }

    sealed class Entry
    {
        public SoundDef Def = null!;
        public Sound[] Sources = Array.Empty<Sound>();   // 변형별 원본
        public Sound[][] Voices = Array.Empty<Sound[]>();   // [변형][동시 재생용 별칭]
        public double LastPlay = -100;
        public int LastVariant = -1;
        // 고리
        public Music Stream;
        public bool HasStream, StreamOn;
        public float WantAcc, PanAcc, Want, WantPan = 0.5f, Cur;
        public double HoldUntil;
    }

    static readonly Dictionary<string, Entry> entries = new();
    static readonly List<Entry> loopEntries = new();
    static readonly Random jitter = new(1);
    static bool ready;
    public static float Volume { get; set; } = 0.8f;
    /// <summary>사용자 음소거 (--mute · 게임 중 N 키). Silent 와 별개.</summary>
    public static bool Muted { get; set; }
    /// <summary>교전 테스트 월드(World.BattleMode) 동안 Game 이 켠다 — 효과음 · 음성 전부 생략 (사용자 설정 Muted 는 건드리지 않음).</summary>
    public static bool Silent { get; set; }

    /// <summary>프레임당 새로 시작할 수 있는 소리 수 (수천 기 교전에서 한 프레임에 수백 개가 몰리지 않게). Important 키는 +4.</summary>
    public const int FrameBudget = 10;
    static int startedThisFrame;
    /// <summary>[통계] 지금까지 시작한 소리 · 예산 / 동시 재생 수 때문에 버린 소리 수.</summary>
    public static long StatStarted, StatDropped, StatLoopStarts;
    /// <summary>소리가 준비됐나 (오디오 장치 있음 + sounds.json 읽음).</summary>
    public static bool Ready => ready;
    public static bool Has(string key) => ready && entries.ContainsKey(key);

    /// <summary>창 생성 이후 호출. 기본 세트 ww2 를 깔고 테마 세트(assets/sounds/&lt;테마&gt;/sounds.json)로 키별로 덮어쓴다 (테마에 없는 키는 ww2 것).</summary>
    public static void Init(string theme)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "assets", "sounds");
        var layers = new List<string>();
        string baseDir = Path.Combine(root, "ww2");
        if (File.Exists(Path.Combine(baseDir, "sounds.json"))) layers.Add(baseDir);
        string themeDir = Path.Combine(root, theme);
        if (theme != "ww2" && File.Exists(Path.Combine(themeDir, "sounds.json"))) layers.Add(themeDir);
        if (layers.Count == 0) return;
        voiceDir = Directory.Exists(Path.Combine(themeDir, "voice")) ? Path.Combine(themeDir, "voice") : Path.Combine(baseDir, "voice");
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady()) return;
        try
        {
            var merged = new Dictionary<string, SoundDef>();
            foreach (var dir in layers)
            {
                var defs = JsonSerializer.Deserialize<Dictionary<string, SoundDef>>(File.ReadAllText(Path.Combine(dir, "sounds.json"))) ?? new();
                foreach (var (key, def) in defs) { def.Dir = dir; merged[key] = def; }
            }
            foreach (var (key, def) in merged)
            {
                if (def.Loop)
                {
                    if (def.Files.Count == 0) continue;
                    var m = Raylib.LoadMusicStream(Path.Combine(def.Dir, def.Files[0]));
                    if (m.FrameCount == 0) continue;
                    m.Looping = true;
                    var le = new Entry { Def = def, Stream = m, HasStream = true };
                    entries[key] = le;
                    loopEntries.Add(le);
                    continue;
                }
                var sources = def.Files.Select(f => Raylib.LoadSound(Path.Combine(def.Dir, f))).Where(s => s.FrameCount > 0).ToArray();
                if (sources.Length == 0) continue;
                int voices = Math.Max(1, def.MaxVoices);
                var aliases = new Sound[sources.Length][];
                for (int v = 0; v < sources.Length; v++)
                {
                    aliases[v] = new Sound[voices];
                    for (int i = 0; i < voices; i++) aliases[v][i] = Raylib.LoadSoundAlias(sources[v]);
                }
                entries[key] = new Entry { Def = def, Sources = sources, Voices = aliases };
            }
            ready = true;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Console.Error.WriteLine($"효과음 읽기 실패: {e.Message}");
        }
    }

    static bool Quiet => Muted || Silent || Volume <= 0;

    /// <summary>[프레임 시작] 한 번. 시작 예산을 비운다.</summary>
    public static void BeginFrame() { startedThisFrame = 0; }

    /// <summary>키의 변형 개수 (없으면 0).</summary>
    public static int VariantCount(string key) => ready && entries.TryGetValue(key, out var e) ? e.Sources.Length : 0;

    /// <summary>변형의 길이 (초). 포탄 휘파람을 착탄에 맞출 때 쓴다.</summary>
    public static float Duration(string key, int variant)
    {
        if (!ready || !entries.TryGetValue(key, out var e) || e.Sources.Length == 0) return 0;
        return e.Sources[Math.Clamp(variant, 0, e.Sources.Length - 1)].FrameCount / 44100f;
    }

    /// <summary>위치 없는 소리 (UI · 알림). volume 0..1, pan 0 = 왼쪽 · 0.5 = 가운데 · 1 = 오른쪽. variant &lt; 0 = 무작위 (직전과 다른 것).</summary>
    public static void Play(string key, float volume = 1, float pan = 0.5f, int variant = -1)
    {
        if (!ready || Quiet || !entries.TryGetValue(key, out var e) || e.Sources.Length == 0) return;
        double now = Raylib.GetTime();
        if (now - e.LastPlay < e.Def.MinInterval) return;
        if (startedThisFrame >= FrameBudget + (e.Def.Important ? 4 : 0)) { StatDropped++; return; }
        int n = e.Sources.Length;
        int vi;
        if (variant >= 0) vi = variant % n;
        else
        {
            vi = n == 1 ? 0 : jitter.Next(n);
            if (n > 1 && vi == e.LastVariant) vi = (vi + 1) % n;
        }
        // 이 키가 이미 maxVoices 개 재생 중이면 생략 (동시 재생 수 제한), 아니면 쉬는 별칭을 쓴다
        int playing = 0;
        int free = -1;
        for (int a = 0; a < n; a++)
        {
            var list = e.Voices[a];
            for (int i = 0; i < list.Length; i++)
            {
                if (Raylib.IsSoundPlaying(list[i])) playing++;
                else if (a == vi && free < 0) free = i;
            }
        }
        if (playing >= e.Def.MaxVoices || free < 0) { StatDropped++; return; }
        var v = e.Voices[vi][free];
        Raylib.SetSoundVolume(v, Math.Clamp(e.Def.Volume * volume * Volume, 0, 1));
        Raylib.SetSoundPan(v, Math.Clamp(pan, 0, 1));
        Raylib.SetSoundPitch(v, 0.94f + (float)jitter.NextDouble() * 0.12f);   // 같은 소리 반복이 덜 기계적이게
        Raylib.PlaySound(v);
        e.LastPlay = now;
        e.LastVariant = vi;
        startedThisFrame++;
        StatStarted++;
    }

    /// <summary>화면 안이면 1, 화면 밖은 거리(화면 폭 절반)만큼 작아지다 0. outside = 화면 밖으로 벗어난 픽셀.</summary>
    static float Falloff(Vector2 s, Rectangle vp, out float outside)
    {
        float dx = MathF.Max(MathF.Max(vp.X - s.X, 0), s.X - (vp.X + vp.Width));
        float dy = MathF.Max(MathF.Max(vp.Y - s.Y, 0), s.Y - (vp.Y + vp.Height));
        outside = MathF.Sqrt(dx * dx + dy * dy);
        return MathF.Max(0, 1 - outside / (vp.Width * 0.5f));
    }

    /// <summary>near / far 선택: farDist 가 있고 "_far" 키가 있으면, 카메라 중심에서 farDist 칸보다 멀거나 화면 밖일 때 _far.</summary>
    static string PickNearFar(string key, Vector2 world, RtsCamera cam, float outside)
    {
        if (!entries.TryGetValue(key, out var e) || e.Def.FarDist <= 0) return key;
        string far = key + "_far";
        if (!entries.ContainsKey(far)) return key;
        return outside > 0 || Vector2.Distance(world, cam.CenterWorld) > e.Def.FarDist ? far : key;
    }

    /// <summary>월드 위치의 소리: 화면 안이면 크게, 화면 밖은 거리(화면 폭 절반)만큼 작아지다 끊김. 많이 축소하면 조금 작게. 카메라 중심에서 멀면 "_far" 변형.</summary>
    public static void PlayAt(string key, Vector2 world, RtsCamera cam, float volume = 1, int variant = -1)
    {
        if (!ready || Quiet) return;
        Vector2 s = cam.WorldToScreen(world);
        var vp = cam.Viewport;
        float falloff = Falloff(s, vp, out float outside);
        if (falloff <= 0.02f) return;
        float zoom = Math.Clamp(0.55f + cam.Zoom * 0.45f, 0.5f, 1f);
        float pan = 0.5f + Math.Clamp((s.X - (vp.X + vp.Width / 2)) / vp.Width, -0.5f, 0.5f) * 0.7f;
        Play(PickNearFar(key, world, cam, outside), volume * falloff * zoom, pan, variant);
    }

    // ---------------------------------------------------------------- 반복 고리 (Music 스트림)
    // 사용: 소리 내는 원천마다 Loop/LoopAt 로 세기를 더한다. EndFrame 이 합계로 음량을 정하고,
    // 세기가 끊긴 지 0.3초가 지나면 서서히 끈다 (기관총 사격 중지 · 기병 멈춤 · 날씨 바뀜).

    /// <summary>[고리] 세기 intensity(0..1, 합산됨) 를 이번 프레임 key 고리에 더한다. 위치 없음.</summary>
    public static void Loop(string key, float intensity, float pan = 0.5f)
    {
        if (!ready || intensity <= 0.001f || !entries.TryGetValue(key, out var e) || !e.HasStream) return;
        e.WantAcc += intensity;
        e.PanAcc += pan * intensity;
    }

    /// <summary>[고리] 월드 위치의 고리: 화면 밖 감쇠 · 줌 · 좌우 위치 · near/far 키 선택은 PlayAt 과 같다.</summary>
    public static void LoopAt(string key, Vector2 world, RtsCamera cam, float intensity = 1)
    {
        if (!ready || Quiet) return;
        Vector2 s = cam.WorldToScreen(world);
        var vp = cam.Viewport;
        float falloff = Falloff(s, vp, out float outside);
        if (falloff <= 0.02f) return;
        float zoom = Math.Clamp(0.55f + cam.Zoom * 0.45f, 0.5f, 1f);
        float pan = 0.5f + Math.Clamp((s.X - (vp.X + vp.Width / 2)) / vp.Width, -0.5f, 0.5f) * 0.7f;
        Loop(PickNearFar(key, world, cam, outside), intensity * falloff * zoom, pan);
    }

    /// <summary>[프레임 끝] 한 번. 고리 음량을 정한다 (켜기 · 서서히 키우기 · 서서히 끄기).</summary>
    public static void EndFrame()
    {
        if (!ready) return;
        double now = Raylib.GetTime();
        float dt = Math.Clamp(Raylib.GetFrameTime(), 0.001f, 0.1f);
        bool quiet = Quiet;
        foreach (var e in loopEntries)
        {
            if (e.WantAcc > 0 && !quiet)
            {
                e.Want = MathF.Min(1f, e.WantAcc);
                e.WantPan = e.PanAcc / e.WantAcc;
                e.HoldUntil = now + 0.3;
            }
            e.WantAcc = 0; e.PanAcc = 0;
            float target = quiet || now > e.HoldUntil ? 0 : e.Want;
            e.Cur += Math.Clamp(target - e.Cur, -dt * 2.5f, dt * 5f);
            if (e.Cur > 0.01f)
            {
                if (!e.StreamOn) { Raylib.PlayMusicStream(e.Stream); e.StreamOn = true; StatLoopStarts++; }
                Raylib.SetMusicVolume(e.Stream, Math.Clamp(e.Def.Volume * e.Cur * Volume, 0, 1));
                Raylib.SetMusicPan(e.Stream, Math.Clamp(e.WantPan, 0, 1));
                Raylib.UpdateMusicStream(e.Stream);
            }
            else if (e.StreamOn) { Raylib.StopMusicStream(e.Stream); e.StreamOn = false; e.Cur = 0; }
        }
    }

    // ---------------------------------------------------------------- 보이스 (세력 언어별 대사)
    // assets/sounds/<테마>/voice/<언어코드>/<사건>_<번호>.wav — 번호는 1부터 변형 (무작위 · 같은 것 연속 반복 회피).
    // 파일이 없으면 조용히 넘어간다. 한 세력이 대사를 너무 자주 하지 않도록 (세력, 사건) 별 최소 간격 + 세력별 공통 간격,
    // 같은 세력의 앞 대사가 아직 재생 중이면 새 대사는 생략. 표현 전용 (시뮬레이션과 무관).

    /// <summary>지원하는 사건 이름 (voice/README.txt 참고) → 같은 세력의 같은 사건 최소 간격 (초).</summary>
    public static readonly IReadOnlyDictionary<string, float> VoiceEvents = new Dictionary<string, float>
    {
        ["select"] = 0.9f,            // 내 유닛 선택
        ["move"] = 0.9f,              // 이동 명령
        ["attack"] = 0.9f,            // 공격 · 어택땅 명령
        ["under_attack"] = 10f,       // 내 병력 · 기지가 공격받음
        ["unit_ready"] = 2.5f,        // 생산 완료
        ["death"] = 4f,               // 내 유닛 전사
        ["capture"] = 4f,             // 시설 점령
    };

    static string voiceDir = "";
    static readonly Dictionary<string, Sound[]> voiceCache = new();
    static readonly Dictionary<(int, string), double> voiceLast = new();
    static readonly Dictionary<int, (double Time, Sound Sound)> voiceFaction = new();
    static readonly Dictionary<string, int> voiceLastVariant = new();
    const float VoiceFactionGap = 0.7f;   // 같은 세력의 대사 사이 최소 간격

    static Sound[] LoadVoice(string lang, string ev)
    {
        string key = lang + "/" + ev;
        if (voiceCache.TryGetValue(key, out var cached)) return cached;
        var list = new List<Sound>();
        string dir = Path.Combine(voiceDir, lang);
        if (Directory.Exists(dir))
            foreach (var file in Directory.GetFiles(dir, ev + "_*").Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal))
            {
                var s = Raylib.LoadSound(file);
                if (s.FrameCount > 0) list.Add(s);
            }
        return voiceCache[key] = list.ToArray();
    }

    /// <summary>세력 faction(언어 lang)의 사건 대사 재생. 재생했으면 true (파일 없음 · 간격 미달 · 이전 대사 재생 중이면 false).</summary>
    public static bool PlayVoice(int faction, string lang, string ev, float volume = 1)
    {
        if (!ready || Muted || Silent || Volume <= 0 || !VoiceEvents.TryGetValue(ev, out float interval)) return false;
        double now = Raylib.GetTime();
        if (voiceLast.TryGetValue((faction, ev), out double last) && now - last < interval) return false;
        if (voiceFaction.TryGetValue(faction, out var prev) && (now - prev.Time < VoiceFactionGap || Raylib.IsSoundPlaying(prev.Sound))) return false;
        var variants = LoadVoice(string.IsNullOrWhiteSpace(lang) ? "en" : lang.ToLowerInvariant(), ev);
        if (variants.Length == 0) return false;
        string vk = faction + lang + ev;
        int pick = jitter.Next(variants.Length);
        if (variants.Length > 1 && voiceLastVariant.TryGetValue(vk, out int lastPick) && pick == lastPick) pick = (pick + 1 + jitter.Next(variants.Length - 1)) % variants.Length;
        voiceLastVariant[vk] = pick;
        var snd = variants[pick];
        Raylib.SetSoundVolume(snd, Math.Clamp(0.9f * volume * Volume, 0, 1));
        Raylib.SetSoundPan(snd, 0.5f);
        Raylib.PlaySound(snd);
        voiceLast[(faction, ev)] = now;
        voiceFaction[faction] = (now, snd);
        return true;
    }

    // ---------------------------------------------------------------- 환경음 고리 (날씨 · 시간대, 화면 전용)
    static string? ambientKey;
    static float ambientVol = 1;

    /// <summary>[고리] 반복 환경음을 바꾼다 (null = 끔). Game 이 날씨 · 시간대가 바뀔 때 부른다. 키가 없으면 무동작.</summary>
    public static void SetAmbient(string? key, float volume = 1)
    {
        ambientKey = key;
        ambientVol = volume;
    }

    /// <summary>[고리] 매 프레임: 날씨 환경음 세기를 더한다 (EndFrame 에서 반영).</summary>
    public static void UpdateAmbient()
    {
        if (ambientKey != null) Loop(ambientKey, ambientVol);
    }

    public static void Shutdown()
    {
        ambientKey = null;
        if (!ready) return;
        if (Environment.GetEnvironmentVariable("KD_SFX_STATS") == "1") Console.Error.WriteLine($"[sfx] started {StatStarted}, dropped {StatDropped}, loop starts {StatLoopStarts}");
        foreach (var e in entries.Values)
        {
            foreach (var list in e.Voices) foreach (var v in list) Raylib.UnloadSoundAlias(v);
            foreach (var s in e.Sources) Raylib.UnloadSound(s);
            if (e.HasStream) { Raylib.StopMusicStream(e.Stream); Raylib.UnloadMusicStream(e.Stream); }
        }
        entries.Clear();
        loopEntries.Clear();
        foreach (var list in voiceCache.Values) foreach (var s in list) Raylib.UnloadSound(s);
        voiceCache.Clear();
        voiceFaction.Clear();
        Raylib.CloseAudioDevice();
        ready = false;
    }
}
