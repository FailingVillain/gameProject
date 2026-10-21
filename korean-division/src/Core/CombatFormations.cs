using System.Numerics;
using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>역할군 (전술 대형 1단계). None = 중대 밖 · 아직 배정 안 됨.</summary>
public enum UnitRole : byte { None = 0, Fire = 1, Rifle = 2, Manoeuvre = 3, Support = 4, Command = 5 }

/// <summary>중대 전술 틀 (assets/data/tactics.json templates). None = 틀 없음 (중대 대형 Company.Form 으로 전체 배치).</summary>
public enum CompanyTactic { None = 0, FireManoeuvre = 1, FlankLeft = 2, FlankRight = 3, DefenceDepth = 4, Artillery = 5, Assault = 6 }

/// <summary>대형 하나의 모양 (formations.json 항목).</summary>
public sealed class FormShapeDef
{
    public string Shape { get; set; } = "grid";
    public int Ranks { get; set; }
    public int Cols { get; set; }
    public float Spacing { get; set; } = 1f;
    public float Facing { get; set; }
    public bool CavalryOnly { get; set; }
    /// <summary>줄 사이 간격 (기본 간격 배수, 0 = spacing 과 같음) — 산개 파상 (formation 2026-10-21).</summary>
    public float RankGap { get; set; }
    /// <summary>열 수 상한 (0 = 없음) — 넘으면 줄 (물결) 을 늘린다 (ranks 는 최소 줄 수가 됨).</summary>
    public int MaxCols { get; set; }
}

public sealed class ArtilleryFormDef
{
    public string SquadForm { get; set; } = "column";
    public float SquadGap { get; set; } = 4f;
    public float RowGap { get; set; } = 2f;
    public string DeployForm { get; set; } = "line";
    public int CheckTicks { get; set; } = 10;
}

public sealed class WavesDef
{
    public int Rows { get; set; } = 2;
    public int ThreeRowsAt { get; set; } = 6;
    public float DepthGap { get; set; } = 10f;
}

/// <summary>전투 대형 자료 (assets/data/formations.json). 없거나 깨진 파일 = 내장 기본값 (파일과 같은 값).</summary>
public sealed class FormationRules
{
    public Dictionary<string, FormShapeDef> Company { get; set; } = new()
    {
        ["close"] = new() { Shape = "grid" },
        ["open"] = new() { Shape = "grid", Spacing = 2.2f },
        ["column"] = new() { Shape = "grid", Cols = 4 },
        ["line"] = new() { Shape = "grid", Ranks = 2 },
        ["line_waves"] = new() { Shape = "grid", Ranks = 2, Spacing = 1.3f, RankGap = 3f, MaxCols = 30 },
        ["wedge"] = new() { Shape = "wedge" },
        ["echelon_left"] = new() { Shape = "echelon", Ranks = 2, Spacing = 1.2f, Facing = 30, CavalryOnly = true },
        ["echelon_right"] = new() { Shape = "echelon", Ranks = 2, Spacing = 1.2f, Facing = 30, CavalryOnly = true },
    };
    public Dictionary<string, FormShapeDef> Squad { get; set; } = new()
    {
        ["close"] = new() { Shape = "grid" },
        ["open"] = new() { Shape = "grid", Ranks = 1, Spacing = 2.2f },
        ["column"] = new() { Shape = "grid", Cols = 2 },
        ["line"] = new() { Shape = "grid", Ranks = 1, Spacing = 1.2f },
        ["wedge"] = new() { Shape = "wedge", Spacing = 1.3f },
        ["echelon_left"] = new() { Shape = "echelon", Ranks = 1, Spacing = 1.3f, Facing = 30, CavalryOnly = true },
        ["echelon_right"] = new() { Shape = "echelon", Ranks = 1, Spacing = 1.3f, Facing = 30, CavalryOnly = true },
    };
    public ArtilleryFormDef Artillery { get; set; } = new();
    public WavesDef Waves { get; set; } = new();

    /// <summary>FormType ↔ 자료 키.</summary>
    public static string KeyOf(FormType t) => t switch
    {
        FormType.Line => "line", FormType.Column => "column", FormType.Wedge => "wedge", FormType.Spread => "open",
        FormType.EchelonLeft => "echelon_left", FormType.EchelonRight => "echelon_right", _ => "close",
    };
    public static FormType? TypeOf(string? key) => key?.ToLowerInvariant() switch
    {
        "line" => FormType.Line, "column" => FormType.Column, "wedge" => FormType.Wedge, "open" => FormType.Spread, "close" => FormType.Box,
        "echelon_left" => FormType.EchelonLeft, "echelon_right" => FormType.EchelonRight, _ => null,
    };
    public static bool IsEchelon(FormType t) => t is FormType.EchelonLeft or FormType.EchelonRight;

    /// <summary>중대 횡대가 쓸 모양 키 (formation 2026-10-21 "횡대 2열 기본 수정": "line_waves" = 산개 파상, 빈 값 = 옛 "line" 2열). formations.json "lineShape".</summary>
    public string LineShape { get; set; } = "";
    public FormShapeDef CompanyShape(FormType t) =>
        t == FormType.Line && !string.IsNullOrEmpty(LineShape) && Company.TryGetValue(LineShape, out var lw) ? lw
        : Company.TryGetValue(KeyOf(t), out var d) ? d : Company.TryGetValue("close", out var c) ? c : new FormShapeDef();
    public FormShapeDef SquadShape(FormType t) => Squad.TryGetValue(KeyOf(t), out var d) ? d : Squad.TryGetValue("close", out var c) ? c : new FormShapeDef();

    static FormationRules? builtin;
    /// <summary>실행 폴더의 formations.json (없으면 내장 기본값) — 처음 한 번 읽고 재사용.</summary>
    public static FormationRules Default => builtin ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "formations.json"));

    public static FormationRules Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<FormationRules>(File.ReadAllText(path), DataJson.Opts) is { } r) return r;
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
        return new FormationRules();
    }

    /// <summary>
    /// 모양 → 로컬 슬롯 (X 오른쪽, Y 앞, 평균 0). s = 기본 간격, width = 우클릭 드래그 폭 (0 = 기본).
    /// 격자 열 수 · 간격 계산은 옛 OrgLayout.CompanySlots 와 같은 식 (같은 값이면 같은 슬롯 — 결정성 골든 유지).
    /// </summary>
    public static Vector2[] Slots(FormShapeDef d, int n, float s, float width, float echelonSign = 1f)
    {
        float sp = s * d.Spacing;
        if (d.Shape == "wedge") return Formations.Slots(FormType.Wedge, n, sp);
        int cols;
        if (width > 0) cols = (int)MathF.Floor(width / sp + 1e-3f) + 1;
        else if (d.Ranks > 0) cols = (n + d.Ranks - 1) / d.Ranks;
        else if (d.Cols > 0) cols = d.Cols;
        else cols = (int)MathF.Ceiling(MathF.Sqrt(n));
        if (width <= 0 && d.MaxCols > 0) cols = Math.Min(cols, d.MaxCols);
        cols = Math.Clamp(cols, 1, Math.Max(1, n));
        float rg = d.RankGap > 0 ? s * d.RankGap : sp;
        var r = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            int row = i / cols, col = i % cols, cnt = Math.Min(cols, n - row * cols);
            r[i] = new Vector2((col - (cnt - 1) / 2f) * sp, -row * rg);
        }
        if (d.Shape == "echelon" && n > 0)
        {
            // 사선: 격자를 기울인다 — 왼쪽 사선 = 왼쪽 끝이 뒤로, 오른쪽 사선 = 오른쪽 끝이 뒤로 (Facing 도)
            float a = d.Facing * (MathF.PI / 180f);
            float t = DetMath.Sin(a) / MathF.Max(0.05f, DetMath.Cos(a));
            float half = (cols - 1) * sp / 2f;
            for (int i = 0; i < n; i++) r[i].Y -= (half + r[i].X * echelonSign) * t;
        }
        Vector2 c = Vector2.Zero;
        foreach (var p in r) c += p;
        if (n > 0) c /= n;
        for (int i = 0; i < n; i++) r[i] -= c;
        return r;
    }

    /// <summary>사선 방향을 반영한 슬롯 (왼쪽 사선 = 오른쪽 사선의 좌우 대칭).</summary>
    public static Vector2[] SlotsFor(FormShapeDef d, FormType t, int n, float s, float width) => Slots(d, n, s, width, t == FormType.EchelonLeft ? -1f : 1f);

    /// <summary>기병 대형 조건: 장교를 뺀 구성원이 있고 전원 기마.</summary>
    public static bool CavalryOnly(IEnumerable<Unit> units)
    {
        bool any = false;
        foreach (var u in units)
        {
            if (u.IsOfficer) continue;
            if (!u.Def.Mounted) return false;
            any = true;
        }
        return any;
    }

    /// <summary>사선은 기병만 — 아니면 횡대로 대신.</summary>
    public static FormType Allowed(FormType t, IEnumerable<Unit> units) => IsEchelon(t) && !CavalryOnly(units) ? FormType.Line : t;
}

internal static class DataJson
{
    public static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
}

public sealed class RoleSlotDef
{
    public float X { get; set; }
    public float Y { get; set; }
    public string Stance { get; set; } = "hold";
    public string? SquadForm { get; set; }
    /// <summary>2단계 행동 (TacticDrill.cs): hold · bound · manoeuvre · follow · support · command · trench · reserve. 없으면 역할 기본 (DrillDef.DefaultAct).</summary>
    public string? Act { get; set; }
    /// <summary>[manoeuvre] 접근 각도 (도, 적 → 우리 방향 기준 + = 오른쪽) · 적과의 거리 (사거리 배수) · 돌격 개시 거리 (0 = behaviour.chargeRange).</summary>
    public float Angle { get; set; }
    public float Standoff { get; set; } = 0.5f;
    public float ChargeRange { get; set; }
    /// <summary>0 보다 크면 분대를 좌우 두 덩어리로 나눠 ±flanks × 전선 폭에 (종심 방어의 기관총 양 측면).</summary>
    public float Flanks { get; set; }
    /// <summary>[bound · manoeuvre] 화력 우세 (적 제압 · 수 우세 · 오래 버팀) 를 얻을 때까지 그 자리에서 사격 (기본 true, 돌격 틀 기동조 = false).</summary>
    public bool Wait { get; set; } = true;
}

public sealed class TemplateDef
{
    /// <summary>"groups" (역할군 배치) | "squadColumns" (포병 대형: 분대 종대 횡렬).</summary>
    public string Layout { get; set; } = "groups";
    /// <summary>다른 틀의 좌우 대칭 (x 부호만 바꿈).</summary>
    public string? Mirror { get; set; }
    public string Stance { get; set; } = "advance";
    public Dictionary<string, RoleSlotDef> Groups { get; set; } = new();
    /// <summary>[포병 대형] 접촉하면 이 틀로 전개 (지휘 능력이 허용할 때, 아니면 formations.json artillery.deployForm 대형).</summary>
    public string? DeployTactic { get; set; }
}

public sealed class RifleSplitDef
{
    public float ManoeuvreFrac { get; set; } = 0.34f;
    public int MinRifleSquads { get; set; } = 3;
}

public sealed class RoleRulesDef
{
    public string Officer { get; set; } = "command";
    public string Scout { get; set; } = "command";
    public Dictionary<string, string> ByWeapon { get; set; } = new() { ["lmg"] = "fire", ["lmg_chauchat"] = "fire", ["hmg"] = "fire", ["hmg_type3"] = "fire", ["carbine_assault"] = "manoeuvre", ["at_rifle"] = "support", ["sniper_rifle"] = "support" };
    public Dictionary<string, string> ByCategory { get; set; } = new() { ["gun"] = "support", ["armored"] = "fire" };
    public string Default { get; set; } = "rifle";
    public int LoadersPerGun { get; set; } = 1;
    public RifleSplitDef RifleSplit { get; set; } = new();
}

/// <summary>전술 AI 대대 역할 배정 규칙 (결정 12, 2026-10-19).</summary>
public sealed class BattalionRolesDef
{
    /// <summary>화력 기반 · 기동 대대가 될 수 있는 최소 소총 중대 병력 (이보다 작은 대대 · 전문 병과만 있는 대대는 구분 없음).</summary>
    public int MinStrength { get; set; } = 40;
}

/// <summary>역할군 · 중대 전술 틀 자료 (assets/data/tactics.json). 없거나 깨진 파일 = 내장 기본값.</summary>
public sealed class TacticRules
{
    public float DepthUnit { get; set; } = 6f;
    public float GroupGap { get; set; } = 2f;
    public float SquadGap { get; set; } = 1.5f;
    public RoleRulesDef Roles { get; set; } = new();
    /// <summary>2단계 행동 · 전환 수치 (TacticDrill.cs).</summary>
    public DrillDef Behaviour { get; set; } = new();
    /// <summary>전술 AI 대대 역할 (결정 12 — TacticalAi.Regiment.cs AssignAttackRoles).</summary>
    public BattalionRolesDef BattalionRoles { get; set; } = new();
    /// <summary>추격 · 후퇴 규칙 (Pursuit.cs — 후퇴하는 적 돌격 금지 · 전술 AI 사격 추격 한계).</summary>
    public PursuitDef Pursuit { get; set; } = new();
    /// <summary>기병 운용 (Pursuit.cs CavalryRulesDef — 측면 공격 · 소규모 기병 · 교착 방지).</summary>
    public CavalryRulesDef Cavalry { get; set; } = new();
    /// <summary>후퇴 뒤 재정비 · 재전진 판단 (Pursuit.cs RegroupDef, pursuit2).</summary>
    public RegroupDef Regroup { get; set; } = new();
    /// <summary>포병 AI 문턱 (N20, TacticalAi.Artillery.cs).</summary>
    public ArtilleryAiDef ArtilleryAi { get; set; } = new();
    /// <summary>도하 · 병목 돌파 (TacticalAi.Crossing.cs, 2026-10-19).</summary>
    public CrossingAiDef Crossing { get; set; } = new();
    /// <summary>역습 교리 (TacticalAi.Counter.cs, 2026-10-19).</summary>
    public CounterAiDef Counter { get; set; } = new();
    /// <summary>참호 돌격 교리 (TrenchAssault.cs, N32).</summary>
    public TrenchAssaultDef TrenchAssault { get; set; } = new();
    public Dictionary<string, TemplateDef> Templates { get; set; } = BuiltinTemplates();

    static Dictionary<string, TemplateDef> BuiltinTemplates()
    {
        static RoleSlotDef G(float x, float y, string st, string f, string act, float angle = 0, float standoff = 0.5f, float charge = 0, float flanks = 0, bool wait = true) =>
            new() { X = x, Y = y, Stance = st, SquadForm = f, Act = act, Angle = angle, Standoff = standoff, ChargeRange = charge, Flanks = flanks, Wait = wait };
        return new()
        {
            ["fire_manoeuvre"] = new() { Groups = new() { ["fire"] = G(-0.3f, 0, "fire", "line", "hold"), ["rifle"] = G(0, 0, "fire", "line", "bound"), ["manoeuvre"] = G(0.45f, 0.6f, "advance", "wedge", "manoeuvre", 35, 0.45f), ["support"] = G(-0.3f, -0.8f, "fire", "line", "support"), ["command"] = G(0, -1f, "hold", "close", "command") } },
            ["flank_left"] = new() { Groups = new() { ["fire"] = G(0.1f, 0, "fire", "line", "hold"), ["rifle"] = G(0.35f, 0, "fire", "line", "hold"), ["manoeuvre"] = G(-0.8f, 0.9f, "advance", "column", "manoeuvre", -80, 0.45f), ["support"] = G(0.2f, -0.8f, "fire", "line", "support"), ["command"] = G(-0.25f, -0.6f, "hold", "close", "command") } },
            ["flank_right"] = new() { Mirror = "flank_left" },
            ["defence_depth"] = new() { Groups = new() { ["rifle"] = G(0, 0, "hold", "open", "trench"), ["fire"] = G(0, -0.3f, "fire", "line", "hold", flanks: 0.45f), ["support"] = G(-0.35f, -1f, "fire", "line", "support"), ["command"] = G(0.3f, -1.2f, "hold", "close", "command"), ["manoeuvre"] = G(0, -2f, "hold", "close", "reserve") } },
            ["artillery"] = new() { Layout = "squadColumns", DeployTactic = "fire_manoeuvre" },
            ["assault"] = new() { Groups = new() { ["manoeuvre"] = G(0, 0.6f, "advance", "line", "manoeuvre", 0, 0.15f, 9f, wait: false), ["rifle"] = G(0, 0, "advance", "line", "follow"), ["fire"] = G(-0.45f, -0.2f, "fire", "line", "hold"), ["support"] = G(0.45f, -0.4f, "fire", "line", "support"), ["command"] = G(0, -0.7f, "advance", "close", "command") } },
        };
    }

    public static string KeyOf(CompanyTactic t) => t switch
    {
        CompanyTactic.FireManoeuvre => "fire_manoeuvre", CompanyTactic.FlankLeft => "flank_left", CompanyTactic.FlankRight => "flank_right",
        CompanyTactic.DefenceDepth => "defence_depth", CompanyTactic.Artillery => "artillery", CompanyTactic.Assault => "assault", _ => "",
    };
    public static CompanyTactic TacticOf(string? key) => key?.ToLowerInvariant() switch
    {
        "fire_manoeuvre" => CompanyTactic.FireManoeuvre, "flank_left" => CompanyTactic.FlankLeft, "flank_right" => CompanyTactic.FlankRight,
        "defence_depth" => CompanyTactic.DefenceDepth, "artillery" => CompanyTactic.Artillery, "assault" => CompanyTactic.Assault, _ => CompanyTactic.None,
    };
    public static string KeyOf(UnitRole r) => r switch
    {
        UnitRole.Fire => "fire", UnitRole.Rifle => "rifle", UnitRole.Manoeuvre => "manoeuvre", UnitRole.Support => "support", UnitRole.Command => "command", _ => "",
    };
    public static UnitRole RoleOf(string? key) => key?.ToLowerInvariant() switch
    {
        "fire" => UnitRole.Fire, "rifle" => UnitRole.Rifle, "manoeuvre" => UnitRole.Manoeuvre, "support" => UnitRole.Support, "command" => UnitRole.Command, _ => UnitRole.Rifle,
    };
    /// <summary>배치 순서 (역할군 겹침 해소도 이 순서).</summary>
    public static readonly UnitRole[] RoleOrder = { UnitRole.Fire, UnitRole.Rifle, UnitRole.Manoeuvre, UnitRole.Support, UnitRole.Command };

    /// <summary>틀 정의 (대칭 틀은 원본을 x 대칭한 사본). 없으면 null.</summary>
    public TemplateDef? Template(CompanyTactic t, out bool mirrored)
    {
        mirrored = false;
        if (!Templates.TryGetValue(KeyOf(t), out var d)) return null;
        if (d.Mirror != null && Templates.TryGetValue(d.Mirror, out var src) && src.Mirror == null) { mirrored = true; return src; }
        return d;
    }

    /// <summary>역할의 기본 태세 (표시용, 1단계).</summary>
    public string StanceOf(CompanyTactic t, UnitRole r)
    {
        var d = Template(t, out _);
        if (d == null) return "";
        return d.Groups.TryGetValue(KeyOf(r), out var g) ? g.Stance : d.Stance;
    }

    static TacticRules? builtin;
    public static TacticRules Default => builtin ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "tactics.json"));

    public static TacticRules Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<TacticRules>(File.ReadAllText(path), DataJson.Opts) is { } r) return r;
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
        return new TacticRules();
    }
}

/// <summary>
/// 중대 슬롯 배치 (순수 로직, 시뮬레이션 · 미리보기 · 빈자리 점검 공용).
/// 틀 없음 = 중대 대형 (formations.json company) 한 덩어리 — 옛 CompanySlots 와 같은 슬롯.
/// 틀 있음 = 역할군 (분대째 작은 블록) 을 틀 위치 (전선 폭 · 종심 단위) 에, 포병 대형 = 분대 종대를 나란히.
/// 슬롯은 묶음 (Pool) 으로 나뉘고 유닛은 자기 묶음의 슬롯에만 배정된다 (역할 · 분대 유지).
/// </summary>
internal static class CompanyLayout
{
    public sealed class Result
    {
        public Vector2[] Local = Array.Empty<Vector2>();
        /// <summary>슬롯 번호 → 묶음, 유닛 번호 (units 목록 순) → 묶음.</summary>
        public int[] SlotPool = Array.Empty<int>(), UnitPool = Array.Empty<int>();
        public int Pools = 1;
    }

    static Result Single(Vector2[] local, int n) => new() { Local = local, SlotPool = new int[local.Length], UnitPool = new int[n], Pools = 1 };

    /// <summary>틀 없는 중대 전체 배치 (사선은 기병만, 아니면 횡대).</summary>
    public static Vector2[] Plain(FormationRules fr, FormType form, List<Unit> units, float s, float width)
    {
        var k = form == FormType.Free ? FormType.Box : FormationRules.Allowed(form, units);
        return FormationRules.SlotsFor(fr.CompanyShape(k), k, units.Count, s, width);
    }

    /// <summary>
    /// N12 장교 위치: 틀 없는 중대 대형 — 장교를 뺀 병사로 대형을 짓고, 장교 슬롯은 대열 중앙 뒤 officerBack 칸 (깊은 대형이면 맨 뒷줄 뒤 한 간격),
    /// 여럿이면 옆으로 officerSpread 칸 간격. 병사 · 장교 슬롯은 따로 배정 (묶음 2개) — 장교가 앞줄 슬롯을 받지 않는다. 장교가 없거나 장교뿐이면 옛 배치 그대로.
    /// </summary>
    static Result PlainWithOfficers(FormationRules fr, FormType form, List<Unit> units, float s, float width, CombatRulesDef cr, int n)
    {
        int k = 0;
        foreach (var u in units) if (u.IsOfficer) k++;
        if (k == 0 || k == n) return Single(Plain(fr, form, units, s, width), n);
        var men = new List<Unit>(n - k);
        foreach (var u in units) if (!u.IsOfficer) men.Add(u);
        var ml = Plain(fr, form, men, s, width);
        float meanY = 0, rear = float.MaxValue;
        foreach (var l in ml) { meanY += l.Y; rear = MathF.Min(rear, l.Y); }
        meanY /= ml.Length;
        float oy = MathF.Min(meanY - cr.OfficerBack, rear - s);
        var local = new Vector2[n];
        var slotPool = new int[n];
        var unitPool = new int[n];
        for (int i = 0; i < ml.Length; i++) local[i] = ml[i];
        for (int j = 0; j < k; j++) { local[ml.Length + j] = new Vector2((j - (k - 1) / 2f) * cr.OfficerSpread, oy); slotPool[ml.Length + j] = 1; }
        for (int i = 0; i < n; i++) unitPool[i] = units[i].IsOfficer ? 1 : 0;
        return new Result { Local = local, SlotPool = slotPool, UnitPool = unitPool, Pools = 2 };
    }

    sealed class Sub
    {
        public readonly List<int> Units = new();
        public Vector2[] Local = Array.Empty<Vector2>();
        public float MinX, MaxX, MinY, MaxY;
        public Vector2 Off;
        public void Bounds()
        {
            MinX = MinY = float.MaxValue; MaxX = MaxY = float.MinValue;
            foreach (var l in Local) { MinX = MathF.Min(MinX, l.X); MaxX = MathF.Max(MaxX, l.X); MinY = MathF.Min(MinY, l.Y); MaxY = MathF.Max(MaxY, l.Y); }
            if (Local.Length == 0) MinX = MaxX = MinY = MaxY = 0;
        }
    }

    sealed class Grp
    {
        public readonly List<Sub> Subs = new();
        public float MinX, MaxX, MinY, MaxY;   // 그룹 로컬 경계 (Off 반영 전)
        public Vector2 Pos;                    // 그룹 경계 중심의 위치
        public float L => Pos.X - (MaxX - MinX) / 2;
        public float R => Pos.X + (MaxX - MinX) / 2;
        public float B => Pos.Y - (MaxY - MinY) / 2;
        public float T => Pos.Y + (MaxY - MinY) / 2;
    }

    /// <summary>분대 블록들을 왼쪽부터 gap 간격으로 앞선을 맞춰 나란히 (Off = 그룹 로컬 위치).</summary>
    static Grp Row(List<Sub> subs, float gap)
    {
        var g = new Grp();
        float x = 0;
        foreach (var sb in subs)
        {
            sb.Off = new Vector2(x - sb.MinX, -sb.MaxY);
            x += sb.MaxX - sb.MinX + gap;
            g.Subs.Add(sb);
        }
        GroupBounds(g);
        return g;
    }

    /// <summary>분대 블록을 왼쪽부터 나란히, 줄 폭이 budget 을 넘으면 뒤로 새 줄 (앞선 맞춤 · 줄마다 가운데 정렬).</summary>
    static Grp Wrap(List<Sub> subs, float gap, float budget)
    {
        var rows = new List<List<Sub>>();
        var cur = new List<Sub>();
        float w = 0;
        foreach (var sb in subs)
        {
            float bw = sb.MaxX - sb.MinX;
            if (cur.Count > 0 && w + gap + bw > budget) { rows.Add(cur); cur = new List<Sub>(); w = 0; }
            w += (cur.Count > 0 ? gap : 0) + bw;
            cur.Add(sb);
        }
        if (cur.Count > 0) rows.Add(cur);
        var g = new Grp();
        float y = 0;
        foreach (var row in rows)
        {
            float rw = -gap, depth = 0;
            foreach (var sb in row) { rw += sb.MaxX - sb.MinX + gap; depth = MathF.Max(depth, sb.MaxY - sb.MinY); }
            float x = -rw / 2;
            foreach (var sb in row)
            {
                sb.Off = new Vector2(x - sb.MinX, y - sb.MaxY);
                x += sb.MaxX - sb.MinX + gap;
                g.Subs.Add(sb);
            }
            y -= depth + gap;
        }
        GroupBounds(g);
        return g;
    }

    /// <summary>
    /// 이미 놓은 역할군과 겹치면 덜 움직이는 쪽으로 민다: 옆 (중심 기준 자기 쪽, 0 이면 틀 x 부호) 또는 뒤.
    /// 옆으로 전선 폭의 ¼ 넘게 밀어야 하는데 뒤로 미는 쪽이 짧으면 뒤로.
    /// </summary>
    static void Separate(Grp g, List<Grp> placed, float gap, float F, float gx)
    {
        for (int it = 0; it < 24; it++)
        {
            Grp? hit = null;
            foreach (var p in placed)
                if (g.L < p.R + gap && g.R > p.L - gap && g.B < p.T + gap && g.T > p.B - gap) { hit = p; break; }
            if (hit == null) return;
            bool right = g.Pos.X > hit.Pos.X || (g.Pos.X == hit.Pos.X && gx >= 0);
            float dx = right ? hit.R + gap - g.L : -(g.R - (hit.L - gap));
            float dyBack = hit.B - gap - g.T;            // 뒤로 (음수)
            float dyFront = hit.T + gap - g.B;           // 앞으로 (양수) — 틀 위치가 상대보다 앞이면
            float dy = g.Pos.Y > hit.Pos.Y ? dyFront : dyBack;
            if (MathF.Abs(dx) > F * 0.25f && MathF.Abs(dy) < MathF.Abs(dx)) g.Pos.Y += dy;
            else g.Pos.X += dx;
        }
    }

    static void GroupBounds(Grp g)
    {
        g.MinX = g.MinY = float.MaxValue; g.MaxX = g.MaxY = float.MinValue;
        foreach (var sb in g.Subs)
        {
            g.MinX = MathF.Min(g.MinX, sb.Off.X + sb.MinX); g.MaxX = MathF.Max(g.MaxX, sb.Off.X + sb.MaxX);
            g.MinY = MathF.Min(g.MinY, sb.Off.Y + sb.MinY); g.MaxY = MathF.Max(g.MaxY, sb.Off.Y + sb.MaxY);
        }
    }

    /// <summary>
    /// 접촉 중 (틀 행동 중이거나 구성원이 표적을 잡음) 인 중대는 틀 배치를 하지 않는다 — 사격 중에 역할군 자리로 재정렬하면 쏘는 대신 걷는다
    /// (2026-10-19 측정: 접촉 뒤 화력-기동 배치로 바꾼 AI 중대가 평지 맞대결 6판 1승). 행동은 역할로 하므로 배치 없이도 돈다. forcePlacement = 포병 대형 전개.
    /// </summary>
    public static bool InContact(Company c) => c.Drill.Phase != DrillPhase.Idle || c.Members.Exists(u => u.Target != null);

    public static Result Build(Company? c, List<Unit> units, FormType form, float s, float width, FormationRules fr, TacticRules tr, bool forcePlacement = false, CombatRulesDef? combat = null)
    {
        int n = units.Count;
        TemplateDef? T = null;
        bool mirror = false;
        if (c != null && c.ActiveTactic != CompanyTactic.None && n >= 2 && (forcePlacement || !InContact(c))) T = tr.Template(c.ActiveTactic, out mirror);
        if (T == null) return combat is { OfficerBack: > 0 } cr ? PlainWithOfficers(fr, form, units, s, width, cr, n) : Single(Plain(fr, form, units, s, width), n);

        var groups = new List<Grp>();
        if (T.Layout == "squadColumns")
        {
            // 포병 대형: 분대 (번호 순) 마다 작은 종대, 나란히. 장교 · 분대 없는 병사 = 뒤 가운데 본부.
            var art = fr.Artillery;
            var sqForm = FormationRules.TypeOf(art.SquadForm) ?? FormType.Column;
            var subs = new List<Sub>();
            foreach (var key in SquadKeys(units))
            {
                var sb = new Sub();
                for (int i = 0; i < n; i++) if (SquadKey(units[i]) == key) sb.Units.Add(i);
                if (key == int.MaxValue) continue;
                var f = FormationRules.Allowed(sqForm, sb.Units.Select(i => units[i]));
                sb.Local = FormationRules.SlotsFor(fr.SquadShape(f), f, sb.Units.Count, s, 0);
                sb.Bounds();
                subs.Add(sb);
            }
            var front = Row(subs, art.SquadGap);
            front.Pos = Vector2.Zero;
            if (subs.Count > 0) groups.Add(front);
            var hq = new Sub();
            for (int i = 0; i < n; i++) if (SquadKey(units[i]) == int.MaxValue) hq.Units.Add(i);
            if (hq.Units.Count > 0)
            {
                hq.Local = FormationRules.SlotsFor(fr.SquadShape(FormType.Box), FormType.Box, hq.Units.Count, s, 0);
                hq.Bounds();
                var hg = Row(new List<Sub> { hq }, 0);
                float backY = subs.Count > 0 ? front.MinY - (front.MinY + front.MaxY) / 2 : 0;   // 앞 그룹 뒤끝 (중심 기준)
                hg.Pos = new Vector2(0, backY - art.RowGap - (hg.MaxY - hg.MinY) / 2);
                groups.Add(hg);
            }
        }
        else
        {
            float F = width > 0 ? width : MathF.Max(8f, (n + 1) / 2 * s);
            float D = tr.DepthUnit;
            // 역할군마다 분대 블록 모으기
            var roleSubs = new List<(UnitRole Role, RoleSlotDef? Def, List<Sub> Subs, int Count)>();
            foreach (var role in TacticRules.RoleOrder)
            {
                var subs = new List<Sub>();
                T.Groups.TryGetValue(TacticRules.KeyOf(role), out var gd);
                var defForm = FormationRules.TypeOf(gd?.SquadForm) ?? FormType.Line;
                int cnt = 0;
                foreach (var key in SquadKeys(units))
                {
                    var sb = new Sub();
                    Squad? sq = null;
                    for (int i = 0; i < n; i++)
                        if (SquadKey(units[i]) == key && RoleOf(units[i]) == role) { sb.Units.Add(i); sq ??= units[i].Squad; }
                    if (sb.Units.Count == 0) continue;
                    var f = sq != null && sq.Form != FormType.Free ? sq.Form : defForm;
                    f = FormationRules.Allowed(f, sb.Units.Select(i => units[i]));
                    sb.Local = FormationRules.SlotsFor(fr.SquadShape(f), f, sb.Units.Count, s, 0);
                    sb.Bounds();
                    subs.Add(sb);
                    cnt += sb.Units.Count;
                }
                if (subs.Count > 0) roleSubs.Add((role, gd, subs, cnt));
            }
            // 전선 폭 몫 (2단계 겹침 수정): 같은 줄 (종심 차 ≤ 0.5 단위) 역할군끼리 병력 비율로 전선 폭을 나눠 갖고, 몫보다 넓으면 분대 블록을 여러 줄로 접는다.
            // (1단계는 블록을 한 줄로 늘어놓고 겹치면 옆으로 밀어 — 100명 중대에서 화력조가 틀 위치에서 19칸 벗어났다.)
            float Gy(RoleSlotDef? d) => d?.Y ?? -1.5f;
            foreach (var (role, gd, subs, cnt) in roleSubs)
            {
                int band = 0;
                foreach (var o in roleSubs) if (MathF.Abs(Gy(o.Def) - Gy(gd)) <= 0.5f) band += o.Count;
                float gx = gd?.X ?? 0, gy = Gy(gd);
                if (mirror) gx = -gx;
                float flanks = gd?.Flanks ?? 0;
                var parts = new List<(List<Sub> Subs, float X)>();
                if (flanks > 0 && subs.Count >= 2)
                {
                    // 좌우 두 덩어리 (번호 순으로 번갈아): 기관총 양 측면
                    var l = new List<Sub>(); var r = new List<Sub>();
                    for (int i = 0; i < subs.Count; i++) (i % 2 == 0 ? l : r).Add(subs[i]);
                    parts.Add((l, gx - flanks)); parts.Add((r, gx + flanks));
                }
                else parts.Add((subs, gx));
                foreach (var (ps, px) in parts)
                {
                    float budget = F * cnt / MathF.Max(1, band) / parts.Count;
                    var g = Wrap(ps, tr.SquadGap, budget);
                    g.Pos = new Vector2(px * F, gy * D);
                    Separate(g, groups, tr.GroupGap, F, px);
                    groups.Add(g);
                }
            }
        }

        // 묶음 = 분대 블록 하나. 슬롯 = 블록 슬롯 + 블록 위치 + 그룹 위치 (그룹 경계 중심 기준)
        var local = new List<Vector2>(n);
        var slotPool = new List<int>(n);
        var unitPool = new int[n];
        int pool = 0;
        foreach (var g in groups)
        {
            Vector2 gc = new((g.MinX + g.MaxX) / 2, (g.MinY + g.MaxY) / 2);
            foreach (var sb in g.Subs)
            {
                foreach (var l in sb.Local) { local.Add(g.Pos + sb.Off + l - gc); slotPool.Add(pool); }
                foreach (int ui in sb.Units) unitPool[ui] = pool;
                pool++;
            }
        }
        var arr = local.ToArray();
        Vector2 mean = Vector2.Zero;
        foreach (var p in arr) mean += p;
        if (arr.Length > 0) mean /= arr.Length;
        for (int i = 0; i < arr.Length; i++) arr[i] -= mean;
        return new Result { Local = arr, SlotPool = slotPool.ToArray(), UnitPool = unitPool, Pools = Math.Max(1, pool) };
    }

    static int SquadKey(Unit u) => u.Squad?.Number ?? int.MaxValue;
    static UnitRole RoleOf(Unit u) => u.Role == UnitRole.None ? UnitRole.Rifle : u.Role;

    /// <summary>분대 번호 (오름차순, 분대 없음 = 맨 끝 int.MaxValue).</summary>
    static List<int> SquadKeys(List<Unit> units)
    {
        var keys = new SortedSet<int>();
        foreach (var u in units) keys.Add(SquadKey(u));
        return keys.ToList();
    }

    /// <summary>묶음별 배정: 묶음이 하나면 옛 배정과 똑같이 (전체 탐욕 + 2-opt), 여럿이면 묶음마다.</summary>
    public static void Match(Result lay, List<Unit> units, Vector2[] world, Vector2[]? from, int[] assign)
    {
        int n = units.Count;
        if (lay.Pools <= 1) { var idx = Enumerable.Range(0, n).ToList(); Formations.Match(units, idx, idx.ToList(), world, from, assign); HeavyBack(lay.Local, units, idx, assign); return; }
        for (int p = 0; p < lay.Pools; p++)
        {
            var us = new List<int>(); var ss = new List<int>();
            for (int i = 0; i < n; i++) if (lay.UnitPool[i] == p) us.Add(i);
            for (int i = 0; i < lay.SlotPool.Length; i++) if (lay.SlotPool[i] == p) ss.Add(i);
            Formations.Match(units, us, ss, world, from, assign);
            HeavyBack(lay.Local, units, us, assign);
        }
    }

    /// <summary>
    /// [중화기 규칙] 같은 묶음 안에서 중화기가 보병보다 앞 슬롯 (로컬 Y 큼) 을 받았으면 가장 뒤 슬롯의 보병과 바꾼다 (중화기 = 보병 횡대와 같은 줄이거나 뒤).
    /// 결정적: 앞 슬롯 중화기부터 (Y, 유닛 순), 바꿀 보병은 가장 뒤 슬롯 (Y, 유닛 순).
    /// </summary>
    static void HeavyBack(Vector2[] local, List<Unit> units, List<int> us, int[] assign)
    {
        for (int guard = 0; guard < us.Count; guard++)
        {
            int hv = -1, inf = -1;
            foreach (int i in us)
            {
                var d = units[i].Def;
                if (d.Heavy) { if (hv < 0 || local[assign[i]].Y > local[assign[hv]].Y) hv = i; }
                else if (d.IsInfantry && !units[i].IsOfficer && !d.Scout) { if (inf < 0 || local[assign[i]].Y < local[assign[inf]].Y) inf = i; }
            }
            if (hv < 0 || inf < 0 || local[assign[hv]].Y <= local[assign[inf]].Y + 0.01f) return;
            (assign[hv], assign[inf]) = (assign[inf], assign[hv]);
        }
    }
}

// ================================================================ 상태 · 명령

public sealed partial class Unit
{
    /// <summary>역할군 (중대 구성원만 의미, 중대를 떠나면 None). ORG_ 구역 저장 (CombatFormations.cs).</summary>
    public UnitRole Role { get; internal set; }
}

/// <summary>중대 전술 틀 지정 (Tactic = CompanyTactic 정수값).</summary>
public sealed record SetCompanyTacticCommand(int Faction, int[] Companies, int Tactic) : GameCommand(Faction);
/// <summary>분대 대형 지정 (Form = FormType 정수값, Free = 자동 — 틀의 역할 기본 대형). 사선은 기병 분대만.</summary>
public sealed record SetSquadFormCommand(int Faction, int[] Squads, int Form) : GameCommand(Faction);
/// <summary>역할 재배정 (Role = UnitRole 정수값, None = 자동 배정으로 되돌림). 중대 구성원만.</summary>
public sealed record SetUnitRoleCommand(int Faction, int[] Units, int Role) : GameCommand(Faction);

public sealed partial class World
{
    public FormationRules FormRules => Defs.Formations;
    public TacticRules TacRules => Defs.Tactics;

    public void SetCompanyTactic(int faction, IEnumerable<Company> companies, CompanyTactic t)
    {
        var ids = companies.Where(c => c.Owner == faction).Select(c => c.Id).Distinct().ToArray();
        if (ids.Length > 0) Issue(new SetCompanyTacticCommand(faction, ids, (int)t));
    }
    public void SetSquadForm(int faction, IEnumerable<Squad> squads, FormType form)
    {
        var ids = squads.Where(q => q.Owner == faction).Select(q => q.Id).Distinct().ToArray();
        if (ids.Length > 0) Issue(new SetSquadFormCommand(faction, ids, (int)form));
    }
    public void SetUnitRole(int faction, IEnumerable<Unit> units, UnitRole role) =>
        IssueUnits(faction, units.Where(u => u.Company != null), ids => new SetUnitRoleCommand(faction, ids, (int)role));

    public Squad? FindSquad(int id)
    {
        foreach (var c in Companies) foreach (var q in c.Squads) if (q.Id == id) return q;
        return null;
    }

    /// <summary>전투 대형 · 역할 명령이면 실행하고 true (ExecuteOrganisation 에서).</summary>
    bool ExecuteFormationCommand(GameCommand command)
    {
        int f = command.Faction;
        switch (command)
        {
            case SetCompanyTacticCommand c:
                if (!Enum.IsDefined(typeof(CompanyTactic), c.Tactic)) return true;
                foreach (int id in c.Companies)
                    if (OwnCompany(f, id) is { } tc)
                    {
                        // 지휘 능력이 허용하지 않는 틀은 기억만 하고 (지휘관이 돌아오면 실행) 지금은 허용되는 틀로 (TacticDrill.cs) — 거절 알림
                        tc.Tactic = (CompanyTactic)c.Tactic;
                        if (InHandover(tc)) continue;
                        var gated = GateTactic(tc, tc.Tactic);
                        SetActiveTactic(tc, gated);
                        if (gated != tc.Tactic) Notify(f, CommandResult.NotAllowed);
                    }
                return true;
            case SetSquadFormCommand c:
                if (!Enum.IsDefined(typeof(FormType), c.Form)) return true;
                foreach (int id in c.Squads)
                    if (FindSquad(id) is { } q && q.Owner == f)
                    {
                        var ft = (FormType)c.Form;
                        if (FormationRules.IsEchelon(ft) && !FormationRules.CavalryOnly(q.Members)) { Notify(f, CommandResult.NotAllowed); continue; }
                        q.Form = ft;
                    }
                return true;
            case SetUnitRoleCommand c:
                if (c.Role < (int)UnitRole.None || c.Role > (int)UnitRole.Command) return true;
                var list = Resolve(f, c.Units).Where(u => u.Company != null).Distinct().ToList();
                if (c.Role == (int)UnitRole.None)
                {
                    // 자동으로 되돌림: 중대 단위로 자동 배정을 계산해 고른 병사에게만 적용
                    foreach (var co in list.Select(u => u.Company!).Distinct().OrderBy(x => x.Id).ToList())
                    {
                        var auto = AutoRoles(co);
                        foreach (var u in list) if (u.Company == co && auto.TryGetValue(u.Id, out var r)) u.Role = r;
                    }
                }
                else foreach (var u in list) u.Role = (UnitRole)c.Role;
                return true;
        }
        return false;
    }

    /// <summary>중대 전원 자동 역할 배정 (편성 · 분할 때).</summary>
    internal void AssignRoles(Company c)
    {
        var auto = AutoRoles(c);
        foreach (var u in c.Members) u.Role = auto.TryGetValue(u.Id, out var r) ? r : UnitRole.Rifle;
    }

    /// <summary>
    /// 자동 역할 (유닛 Id → 역할): 장교 · 정찰병 · 무기 · 병과 규칙 → 기관총마다 같은 분대 소총수 탄약수 → 소총 분대가 많으면 뒤쪽 분대 = 기동조.
    /// 순수 함수 (구성원 · 분대 번호 · Id 순서만 읽음).
    /// </summary>
    internal Dictionary<int, UnitRole> AutoRoles(Company c)
    {
        var rr = TacRules.Roles;
        var res = new Dictionary<int, UnitRole>();
        UnitRole Base(Unit u)
        {
            if (u.IsOfficer) return TacticRules.RoleOf(rr.Officer);
            if (u.Def.Scout) return TacticRules.RoleOf(rr.Scout);
            if (u.Def.Weapon != null && rr.ByWeapon.TryGetValue(u.Def.Weapon, out var w)) return TacticRules.RoleOf(w);
            if (rr.ByCategory.TryGetValue(u.Def.Category, out var k)) return TacticRules.RoleOf(k);
            return TacticRules.RoleOf(rr.Default);
        }
        foreach (var u in c.Members) res[u.Id] = Base(u);
        // 탄약수: 기관총 (화력조 기본 역할인 보병) 마다 같은 분대 소총수 (Id 순)
        int per = Math.Max(0, rr.LoadersPerGun);
        if (per > 0)
            foreach (var q in c.Squads)
            {
                var guns = q.Members.Count(u => res[u.Id] == UnitRole.Fire && u.Def.IsInfantry);
                int need = guns * per;
                foreach (var u in q.Members)
                {
                    if (need <= 0) break;
                    if (res[u.Id] == UnitRole.Rifle) { res[u.Id] = UnitRole.Fire; need--; }
                }
            }
        // 소총 분대 나누기: 남은 소총수가 있는 분대 (번호 순) 가 minRifleSquads 이상이면 뒤쪽 ceil(k × frac) 분대 = 기동조
        var rifleSquads = c.Squads.Where(q => q.Members.Any(u => res[u.Id] == UnitRole.Rifle)).ToList();
        var split = rr.RifleSplit;
        if (rifleSquads.Count >= Math.Max(1, split.MinRifleSquads) && split.ManoeuvreFrac > 0)
        {
            int m = Math.Min(rifleSquads.Count - 1, (int)MathF.Ceiling(rifleSquads.Count * split.ManoeuvreFrac));
            for (int i = rifleSquads.Count - m; i < rifleSquads.Count; i++)
                foreach (var u in rifleSquads[i].Members) if (res[u.Id] == UnitRole.Rifle) res[u.Id] = UnitRole.Manoeuvre;
        }
        return res;
    }

    /// <summary>
    /// 중대 밖 · 부분 선택 묶음의 진형: 고른 병사가 정확히 분대 하나 (대형 지정됨) 면 그 분대 대형 (우클릭 드래그 방향 · 폭은 그대로),
    /// 사선은 기병만 (아니면 횡대). local = 쓸 슬롯 (null = Formations.Slots 기본).
    /// </summary>
    internal FormSpec? LooseForm(List<Unit> list, FormSpec? form, out Vector2[]? local)
    {
        local = null;
        if (list.Count < 2) return form;
        var q = list[0].Squad;
        if (q != null && q.Form != FormType.Free && list.Count == q.Members.Count && list.TrueForAll(u => u.Squad == q))
        {
            var k = FormationRules.Allowed(q.Form, list);
            var spec = (form ?? new FormSpec(0, 0, Vector2.Zero)) with { Type = (int)k };
            local = FormationRules.SlotsFor(FormRules.SquadShape(k), k, list.Count, OrgLayout.SpacingFor(list), spec.Width);
            return spec;
        }
        if (form != null && FormationRules.IsEchelon(form.Kind))
        {
            if (!FormationRules.CavalryOnly(list)) return form with { Type = (int)FormType.Line };
            local = FormationRules.SlotsFor(FormRules.CompanyShape(form.Kind), form.Kind, list.Count, OrgLayout.SpacingFor(list), form.Width);
        }
        return form;
    }

    // ---------------------------------------------------------------- 포병 대형 자동 전개

    /// <summary>포병 대형 중대가 접촉하면 (구성원이 표적을 잡았거나 최근 1초 안 사격) deployForm 중대 대형으로 전개 (StepAnchors 앞에서).</summary>
    void StepFormationTriggers()
    {
        var art = FormRules.Artillery;
        int every = Math.Max(1, art.CheckTicks);
        foreach (var c in Companies)
        {
            if (c.Tactic != CompanyTactic.Artillery || (Tick + c.Id) % every != 0) continue;
            bool contact = false;
            foreach (var u in c.Members) if (u.Target != null || (u.LastShotTick > 0 && Tick - u.LastShotTick < 60)) { contact = true; break; }
            if (contact) DeployFromArtillery(c);
        }
    }

    void DeployFromArtillery(Company c)
    {
        // 접촉: 포병 대형 틀의 deployTactic (화력-기동) 으로 — 지휘 능력이 허용하지 않으면 (지휘 없음 · 인계 중) 틀 없이 deployForm 횡대 (TacticDrill.cs)
        var want = TacticRules.TacticOf(TacRules.Template(CompanyTactic.Artillery, out _)?.DeployTactic);
        var t = want is CompanyTactic.None or CompanyTactic.Artillery || InHandover(c) ? CompanyTactic.None : GateTactic(c, want);
        c.Tactic = t;
        SetActiveTactic(c, t);
        c.Form = FormationRules.TypeOf(FormRules.Artillery.DeployForm) ?? FormType.Line;
        Stats.ArtilleryDeploys++;
        var a = c.Anchor;
        var units = c.Members.Where(u => a != null && ReferenceEquals(u.Group, a.Group)).ToList();
        if (a == null || units.Count < 2) { if (c.Facing != Vector2.Zero) RefillCompany(c, Centroid(c.Members)); return; }
        // 행군 중 (또는 도착해 슬롯으로 가는 중): 같은 깃발 · 진행 방향 그대로 슬롯만 새 배치로 (지금 위치에서 가까운 순, 틀이면 역할 · 분대 묶음마다)
        float sp = a.Spacing;
        var lay = CompanyLayout.Build(c, units, c.Form, sp, 0, FormRules, TacRules, forcePlacement: true, combat: Defs.CombatRules);
        var local = lay.Local;
        int n = units.Count;
        Vector2 head = a.Heading, right = new(-head.Y, head.X);
        var world = new Vector2[n];
        for (int i = 0; i < n; i++) world[i] = a.Pos + right * local[i].X + head * local[i].Y;
        var assign = new int[n];
        CompanyLayout.Match(lay, units, world, null, assign);
        Vector2 dir = a.FinalDir, rightF = new(-dir.Y, dir.X);
        float hw = 0;
        foreach (var l in local) hw = MathF.Max(hw, MathF.Abs(l.X));
        a.HalfWidth = hw + sp * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var u = units[i];
            Vector2 l = local[assign[i]];
            a.Slots[u.Id] = l;
            a.FinalOff[u.Id] = ShiftSlot(u, a.Goal + rightF * l.X + dir * l.Y) - a.Goal;
            FormReact(u);   // N34: 전개 — 병사마다 0~3틱 뒤 새 슬롯으로
        }
        if (a.Column) { a.Column = false; a.ClearTicks = 0; }
    }

    // ---------------------------------------------------------------- 저장 (ORG_ 끝) · 체크섬

    void WriteFormationState(SnapWriter w)
    {
        foreach (var c in Companies)
        {
            w.I((int)c.Tactic);
            foreach (var u in c.Members) w.Byte((byte)u.Role);
            foreach (var q in c.Squads) w.I((int)q.Form);
            WriteDrill(w, c);   // 실행 틀 · 2단계 행동 상태 (TacticDrill.cs, FormatVersion 24)
        }
    }

    void ReadFormationState(SnapReader r)
    {
        foreach (var c in Companies)
        {
            int t = r.I();
            if (!Enum.IsDefined(typeof(CompanyTactic), t)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company tactic"));
            c.Tactic = (CompanyTactic)t;
            foreach (var u in c.Members)
            {
                byte b = r.Byte();
                if (!Enum.IsDefined(typeof(UnitRole), b)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "unit role"));
                u.Role = (UnitRole)b;
            }
            foreach (var q in c.Squads)
            {
                int f = r.I();
                if (!Enum.IsDefined(typeof(FormType), f)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "squad form"));
                q.Form = (FormType)f;
            }
            ReadDrill(r, c);
        }
    }

    /// <summary>틀 · 분대 대형을 쓰는 중대만 (기본 상태의 체크섬은 옛 것과 같게 — 역할은 틀이 있을 때만 시뮬레이션에 영향).</summary>
    void HashFormationState(ref Hasher h)
    {
        foreach (var c in Companies)
        {
            bool used = c.Tactic != CompanyTactic.None || c.ActiveTactic != CompanyTactic.None || c.Drill.Used;
            foreach (var q in c.Squads) if (q.Form != FormType.Free) used = true;
            if (!used) continue;
            h.Add(c.Id); h.Add((int)c.Tactic); h.Add((int)c.ActiveTactic);
            foreach (var u in c.Members) h.Add((int)u.Role);
            foreach (var q in c.Squads) h.Add((int)q.Form);
            HashDrill(ref h, c);
        }
    }
}
