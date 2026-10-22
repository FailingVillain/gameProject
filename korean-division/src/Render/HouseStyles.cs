using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>민가 클래스 한 종류 (초가 · 기와 ...). 키 = KeyPrefix + Prefix + 두 자리 번호.</summary>
public sealed class HouseClassDef
{
    public string Prefix { get; set; } = "";
    public int Count { get; set; }
    /// <summary>한 키가 구워 넣은 색 변형 수 (A, B). 메모리 예산 계산용.</summary>
    public int Baked { get; set; } = 1;
    /// <summary>클래스에 그림이 하나도 없을 때 대신 쓰는 키 (예전 4종).</summary>
    public string? Fallback { get; set; }
}

public sealed class HouseStyleDef
{
    /// <summary>클래스 → [정사각, 가로형, 큰 건물] 정수 가중치.</summary>
    public Dictionary<string, int[]>? Weights { get; set; }
    /// <summary>양식 → 정수 가중치 (섞기). 있으면 Weights 대신 쓴다.</summary>
    public Dictionary<string, int>? Mix { get; set; }
}

/// <summary>assets/data/house_styles.json.</summary>
public sealed class HouseStyleTable
{
    public string KeyPrefix { get; set; } = "building_art.house.";
    public double FrameMb { get; set; } = 2.0;
    public double Lod0Share { get; set; } = 0.75;
    public float WideRatio { get; set; } = 1.4f;
    public int LargeArea { get; set; } = 16;
    public int Retry { get; set; } = 3;
    public List<int[]> Tones { get; set; } = new();
    public Dictionary<string, HouseClassDef> Classes { get; set; } = new();
    public Dictionary<string, HouseStyleDef> Styles { get; set; } = new();
}

/// <summary>한 채의 선택 결과: 클래스 · 키 · 구운 변형(0=A 1=B) · 곱하기 단계 · 좌우반전 · 폐허 b 변형 비트.</summary>
public readonly record struct HousePick(string ClassId, string Key, int Variant, int Tone, bool Mirror, bool AltRuin)
{
    /// <summary>테마에서 찾을 키 (B 변형 = 키 + ".b").</summary>
    public string DrawKey => Variant == 1 ? Key + ".b" : Key;
}

/// <summary>
/// 민가 양식 선택 (T55s) — 렌더 전용, 시뮬 상태를 읽지 않고 쓰지도 않는다. 결정적: 좌표 해시 + 정수 가중치만 (벽시계 · 딕셔너리 열거 순서 · 부동소수 없음).
/// 그림이 테마에 없는 키는 <c>has</c> 로 걸러 건너뛴다 (42종이 다 안 와도 되는 점진 도입).
/// </summary>
public static class HouseStyles
{
    static HouseStyleTable? current;
    public static HouseStyleTable Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "house_styles.json"));

    public static HouseStyleTable Load(string path)
    {
        try { return Parse(File.ReadAllText(path)); }
        catch (Exception e) when (e is IOException or JsonException) { Console.Error.WriteLine($"[house_styles] {path}: {e.Message} - 예전 4종으로"); return new HouseStyleTable(); }
    }

    public static HouseStyleTable Parse(string text) =>
        JsonSerializer.Deserialize<HouseStyleTable>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) ?? new HouseStyleTable();

    /// <summary>양식 이름 → 정의가 있는 이름 (소문자). null = 예전 4종 (생략 · "legacy" · 모르는 이름).</summary>
    public static string? Resolve(HouseStyleTable t, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string n = name.Trim().ToLowerInvariant();
        return t.Styles.ContainsKey(n) ? n : null;
    }

    static IEnumerable<string> Sorted(IEnumerable<string> keys) => keys.OrderBy(k => k, StringComparer.Ordinal);

    // ---------------------------------------------------------------- 키

    /// <summary>클래스의 기본 키 전부 (번호 순).</summary>
    public static IEnumerable<string> ClassKeys(HouseStyleTable t, string cls)
    {
        if (!t.Classes.TryGetValue(cls, out var c)) yield break;
        for (int i = 1; i <= c.Count; i++) yield return t.KeyPrefix + c.Prefix + i.ToString("00");
    }

    /// <summary>클래스에서 지금 그릴 수 있는 키 (없으면 fallback 한 개, 그것도 없으면 빈 목록).</summary>
    static List<string> Avail(HouseStyleTable t, string cls, Func<string, bool> has)
    {
        var l = ClassKeys(t, cls).Where(has).ToList();
        if (l.Count == 0 && t.Classes.TryGetValue(cls, out var c) && c.Fallback != null && has(c.Fallback)) l.Add(c.Fallback);
        return l;
    }

    /// <summary>양식이 쓰는 클래스 (섞기면 하위 양식의 클래스 합집합), 이름순.</summary>
    public static List<string> ClassesOf(HouseStyleTable t, string style)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        void Add(string s, int depth)
        {
            if (depth > 2 || !t.Styles.TryGetValue(s, out var d)) return;
            if (d.Weights != null) foreach (var (k, w) in d.Weights) if (w.Any(x => x > 0)) set.Add(k);
            if (d.Mix != null) foreach (var k in Sorted(d.Mix.Keys)) Add(k, depth + 1);
        }
        Add(style, 0);
        return set.ToList();
    }

    /// <summary>테마에 그림이 하나라도 있는 양식인가 (지형이 가옥 칸을 마당 흙으로 굽는 판단용).</summary>
    public static bool AnyArt(HouseStyleTable t, Func<string, bool> has) =>
        t.Classes.Keys.Any(c => Avail(t, c, has).Count > 0);

    // ---------------------------------------------------------------- 해시

    static uint Mix(uint h) { h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16; return h; }
    static uint H(int x, int y, int salt) => Mix((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791) ^ 0x5bd1e995u);

    /// <summary>0 = 정사각에 가까움, 1 = 가로로 긴, 2 = 큰 건물 (면적 &gt;= LargeArea).</summary>
    public static int Shape(HouseStyleTable t, int w, int h) => w >= h * t.WideRatio ? 1 : w * h >= t.LargeArea ? 2 : 0;

    static int Pick(uint h, List<int> weights)
    {
        long total = 0; foreach (int w in weights) total += w;
        long r = h % (uint)Math.Max(1, total);
        for (int i = 0; i < weights.Count; i++) { if (r < weights[i]) return i; r -= weights[i]; }
        return weights.Count - 1;
    }

    // ---------------------------------------------------------------- 한 채 고르기

    /// <summary>
    /// 한 채 (좌표 x,y · 모양 shape) 의 선택. salt 를 올리면 다른 결과 (이웃 반복 회피 재시도용). 고를 수 있는 그림이 없으면 null.
    /// </summary>
    public static HousePick? PickOne(HouseStyleTable t, string style, int x, int y, int shape, int salt, Func<string, bool> has)
    {
        uint ha = H(x, y, salt), hb = Mix(ha + 0x9e3779b9u), hc = Mix(hb + 0x9e3779b9u);
        if (!t.Styles.TryGetValue(style, out var def)) return null;
        if (def.Mix != null)
        {
            // 하위 양식 가중치 선택 (그림이 하나도 없는 하위 양식은 뺀다)
            var names = new List<string>(); var ws = new List<int>();
            foreach (var n in Sorted(def.Mix.Keys))
                if (n != style && t.Styles.TryGetValue(n, out var sd) && sd.Weights != null && def.Mix[n] > 0 && ClassesOf(t, n).Any(c => Avail(t, c, has).Count > 0)) { names.Add(n); ws.Add(def.Mix[n]); }
            if (names.Count == 0) return null;
            def = t.Styles[names[Pick(ha, ws)]];
        }
        if (def.Weights == null) return null;
        var cls = new List<string>(); var cw = new List<int>(); var ck = new List<List<string>>();
        for (int pass = 0; pass < 2 && cls.Count == 0; pass++)
            foreach (var c in Sorted(def.Weights.Keys))
            {
                var w = def.Weights[c];
                int wt = pass == 0 ? (shape < w.Length ? w[shape] : 0) : (w.Length > 0 ? w.Max() : 0);   // 2차: 이 모양에 맞는 클래스가 없으면 아무 모양이나
                if (wt <= 0) continue;
                var keys = Avail(t, c, has);
                if (keys.Count == 0) continue;
                cls.Add(c); cw.Add(wt); ck.Add(keys);
            }
        if (cls.Count == 0) return null;
        int ci = Pick(hb, cw);
        var kl = ck[ci];
        string key = kl[(int)((hc >> 4) % (uint)kl.Count)];
        int variant = (hc >> 12 & 1) == 1 && has(key + ".b") ? 1 : 0;
        int tone = t.Tones.Count > 0 ? (int)((hc >> 14) % (uint)t.Tones.Count) : 0;
        return new HousePick(cls[ci], key, variant, tone, (hc >> 20 & 1) == 1, (hc >> 21 & 1) == 1);
    }

    /// <summary>폐허 칸 한 개의 선택 (이웃 회피 없음, 정사각 모양 가중치).</summary>
    public static HousePick? PickRuin(HouseStyleTable t, string style, int x, int y, Func<string, bool> has) => PickOne(t, style, x, y, 0, 101, has);

    // ---------------------------------------------------------------- 배정 (이웃 반복 회피)

    /// <summary>
    /// 조각 순서대로 (HouseBlocks 의 위 → 아래, 왼 → 오른 순서) 한 채씩 배정한다. 왼쪽 · 위쪽 (대각선 포함, 2칸 안) 에 이미 배정된 조각과 같은 (키, 구운 변형) 이면
    /// salt 를 올려 최대 Retry 회 다시 고른다. 화면 컬링과 무관하게 지도 전체를 한 번에 배정하므로 같은 지도는 항상 같은 결과.
    /// 결과 키 = (y &lt;&lt; 32) | x of 조각 왼쪽 위.
    /// </summary>
    public static Dictionary<long, HousePick> Assign(HouseStyleTable t, string style, int mapW, int mapH, IEnumerable<HouseBlocks.Block> blocks, Func<string, bool> has)
    {
        var res = new Dictionary<long, HousePick>();
        var owner = new int[Math.Max(1, mapW * mapH)];
        var list = new List<HousePick>();
        foreach (var blk in blocks)
        foreach (var p in HouseBlocks.Pieces(blk))
        {
            int shape = Shape(t, p.W, p.H);
            HousePick? best = null;
            for (int salt = 0; salt <= t.Retry; salt++)
            {
                var c = PickOne(t, style, p.X, p.Y, shape, salt, has);
                if (c == null) break;
                best = c;
                if (!Repeats(c.Value, p, owner, list, mapW, mapH)) break;
            }
            if (best == null) continue;
            list.Add(best.Value);
            for (int yy = p.Y; yy < p.Y + p.H && yy < mapH; yy++)
            for (int xx = p.X; xx < p.X + p.W && xx < mapW; xx++) owner[yy * mapW + xx] = list.Count;
            res[((long)p.Y << 32) | (uint)p.X] = best.Value;
        }
        return res;
    }

    static bool Repeats(HousePick c, HouseBlocks.Block p, int[] owner, List<HousePick> list, int mapW, int mapH)
    {
        bool Same(int x, int y)
        {
            if ((uint)x >= (uint)mapW || (uint)y >= (uint)mapH) return false;
            int o = owner[y * mapW + x];
            return o > 0 && list[o - 1].Key == c.Key && list[o - 1].Variant == c.Variant;
        }
        for (int d = 1; d <= 2; d++)
        {
            for (int yy = p.Y - d; yy < p.Y + p.H; yy++) if (Same(p.X - d, yy)) return true;           // 왼쪽 (왼쪽 위 대각 포함)
            for (int xx = p.X - d; xx <= p.X + p.W; xx++) if (Same(xx, p.Y - d)) return true;         // 위쪽 (양 대각 포함)
        }
        return false;
    }

    // ---------------------------------------------------------------- 캐시 (그리기용)

    static GameMap? aMap; static long aVer = -1; static string? aStyle; static object? aTheme; static Dictionary<long, HousePick> aRes = new();

    /// <summary>지도 · 지형 버전 · 양식 · 테마가 같으면 이전 배정 재사용.</summary>
    public static Dictionary<long, HousePick> AssignCached(GameMap map, string style, object themeId, Func<string, bool> has)
    {
        if (!ReferenceEquals(map, aMap) || map.TerrainVersion != aVer || style != aStyle || !ReferenceEquals(themeId, aTheme))
        {
            aRes = Assign(Current, style, map.Width, map.Height, HouseBlocks.Of(map), has);
            aMap = map; aVer = map.TerrainVersion; aStyle = style; aTheme = themeId;
        }
        return aRes;
    }

    // ---------------------------------------------------------------- 아틀라스 예산

    /// <summary>
    /// 한 지도에서 이 양식을 다 올렸을 때의 아틀라스 추정 (MB, 전 LOD): 구운 프레임 수 (키 × 클래스 구운 변형 수) × frameMb,
    /// withRuins 면 기본 키마다 폐허 프레임 하나 더. has != null 이면 테마에 있는 키만 센다. 안 그려지는 종은 지연 생성이라 실제는 이 값 이하.
    /// </summary>
    public static double AtlasBudgetMb(HouseStyleTable t, string style, bool withRuins, Func<string, bool>? has = null)
    {
        int frames = 0, baseKeys = 0;
        foreach (var c in ClassesOf(t, style))
        {
            int n = ClassKeys(t, c).Count(k => has == null || has(k));
            baseKeys += n; frames += n * Math.Max(1, t.Classes[c].Baked);
        }
        return (frames + (withRuins ? baseKeys : 0)) * t.FrameMb;
    }

    /// <summary>예산 대비 LOD0 상주 비율 (0..1+): mb × lod0Share / LOD0 한도 MB.</summary>
    public static double Lod0Use(HouseStyleTable t, double mb, double lod0BudgetMb) => lod0BudgetMb <= 0 ? 0 : mb * t.Lod0Share / lod0BudgetMb;
}
