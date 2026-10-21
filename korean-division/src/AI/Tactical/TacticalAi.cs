using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 전술 AI (ROADMAP ⑤, 1단계 2026-10-19 · 2단계 docs/TACTICAL_AI_DESIGN.md). --ai tactical (교전 테스트 · 빠른 교전 · 표준 게임 · 미션 기본).
/// 두 주층 (사용자 지시 2026-10-19):
///  · 세력 전략 (TacticalAi.Strategy.cs, ≈2초): 지시 그룹 (미션 병력별 태세 · 구역) 마다 태세 · 목표 · 연대 정면 배분 · 주공/조공 · 예비 연대 ·
///    증원 편입 · 포병/기병/정찰 배정 · 태세 변경 (방어 대기 → 공격, 공격 한계 → 방어, 우세 → 반격).
///  · 연대 전술 (TacticalAi.Regiment.cs, ≈1초): 대대 2~4 의 AI 묶음. 공격 = 접근 → 준비사격 → 돌격 (화력 기반 · 기동 대대, 포병 연신),
///    방어 = 종심 (주저항선 · 역습 예비). 그 아래 실행 층 = 대대 (≈1초, 중대 정면 · 예비 중대 · 붙은 화력) · 중대 (TacticalAi.Company.cs, ≈0.5초:
///    사격 진지 (TacMap) · 분대 교대 약진 · 전초 · 돌격 · 참호 · 후퇴 · 병과 운용).
/// 규칙: 조작은 World 공개 명령 (Move · AttackMove · QuickStep · Hold · Attack · Charge · FormCompany · FormBattalion) 만, 적 정보는 Fog.CanSee 로 보이는 것만.
/// 결정적: 목록 순서 · Id 순, 해시 기반 선택, 틱 기반 시차. 상태는 TacFaction (스냅샷 저장). 지형 캐시 (TacMap) 는 지형의 순수 함수.
/// </summary>
public sealed partial class TacticalAi : IAiController, IAiDirectable
{
    public const string BaseName = "tactical";
    readonly string? profileOverride;   // "tactical:aggressive" → aggressive (비교용)
    readonly Dictionary<int, TacFaction> states = new();

    // 작업 버퍼 (저장 안 함 — List<Unit> 은 ObjCodec 이 건너뛴다)
    readonly List<Unit> enemies = new(), mem = new(), own = new(), buf = new(), buf2 = new(), sub = new(), sub2 = new();
    [ThreadStatic] static TacGrid? gridT;
    static TacGrid Grid => gridT ??= new TacGrid();

    public TacticalAi() { }
    public TacticalAi(string? profile) { profileOverride = string.IsNullOrWhiteSpace(profile) ? null : profile.Trim().ToLowerInvariant(); }

    public string Name => profileOverride == null ? BaseName : BaseName + ":" + profileOverride;

    /// <summary>테스트 · 관찰용: 세력 상태.</summary>
    public TacFaction? StateOf(int faction) => states.GetValueOrDefault(faction);

    /// <summary>디버그 한 줄 (KD_TAC_DBG — 교전 sim · 미션 실행): 지시 그룹 태세 · 연대 국면 · 대대 역할 · 중대 상태.</summary>
    public static string Describe(TacFaction s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var d in s.Directives)
            if (d.Alive > 0) sb.Append($"[G{d.Id} {d.Posture}/{d.Effective}{(d.Culminated ? " 한계" : "")} {d.Alive}/{d.Initial} T=({d.Target.X:0},{d.Target.Y:0})] ");
        foreach (var r in s.Regiments)
        {
            sb.Append($"\n     R{r.Id} G{r.Directive} {r.Task}/{r.Phase}{(r.Plan != ManPlan.None ? $" 계획 {r.Plan}/{r.PlanStage}{(r.PlanSide != 0 ? (r.PlanSide > 0 ? "+" : "-") : "")}{(r.PlanStage == 1 && r.Plan is ManPlan.Flank or ManPlan.Pincer ? $" W({r.PlanWay.X:0},{r.PlanWay.Y:0}) A({r.PlanAxis.X:0},{r.PlanAxis.Y:0}) E({r.PlanEnemy.X:0},{r.PlanEnemy.Y:0})" : "")}" : "")}{(r.CrossPhase != 0 ? $" 도하{r.CrossPhase} ({r.CrossEntry.X:0},{r.CrossEntry.Y:0})>({r.CrossExit.X:0},{r.CrossExit.Y:0}) {r.CrossNext}/{r.CrossQueue.Count}" : "")}{(r.CounterStage != 0 ? $" 역습{r.CounterStage}" : "")}{(r.Main ? " 주공" : "")}{(r.HasFireTarget ? (r.Lifted ? " 연신" : " 화력표적") : "")} {r.Alive}/{r.Initial}:");
            foreach (var b in s.Battalions)
            {
                if (b.Regiment != r.Id) continue;
                sb.Append($" B{b.Id}{(b.Number > 0 ? "#" + b.Number : "")} {b.Task}/{b.Role}{(b.Split ? "/분할" : "")}{(b.HoldFire ? "/대기" : "")} {b.Alive}/{b.Initial} {{");
                foreach (var g in s.Groups) if (g.Battalion == b.Id) sb.Append($" {g.Kind.ToString()[0]}{g.Id} {g.Task}/{g.State}/{g.Role}{(g.Element != ManElement.None ? "/" + g.Element + g.ManStage : "")}{(g.SquadBound != 0 ? "/약진" : "")}{(g.OutpostState == 1 ? "/전초" : "")}{(g.CrossStage != 0 ? "/도" + g.CrossStage : "")} {g.Alive}@({g.Center.X:0},{g.Center.Y:0})");
                sb.Append(" }");
            }
        }
        var direct = s.Groups.Where(g => g.Battalion == 0).ToList();
        if (direct.Count > 0)
        {
            sb.Append("\n     직속:");
            foreach (var g in direct) sb.Append($" {g.Kind.ToString()[0]}{g.Id} G{g.Directive} {g.Task}/{g.State}{(g.Supports != 0 ? " →R" + g.Supports : "")} {g.Alive}@({g.Center.X:0},{g.Center.Y:0})");
        }
        return sb.ToString();
    }

    TacFaction State(int faction)
    {
        if (!states.TryGetValue(faction, out var s))
        {
            states[faction] = s = new TacFaction { Faction = faction };
            s.Directives.Add(new TacDirective { Id = 0 });
        }
        return s;
    }

    public void SetDirective(int faction, AiPosture posture, string? profile = null, Vector2? area = null, float radius = 0)
    {
        var s = State(faction);
        s.DirectiveSet = true;
        s.Posture = posture;
        if (profile != null)
        {
            s.DirectiveProfile = profile;
            if (s.Phase > 0 && AiProfiles.Find(profile) is { } p) s.Profile = p;
        }
        s.HasArea = area != null;
        s.Area = area ?? default;
        s.AreaRadius = MathF.Max(0, radius);
    }

    public void SetDirective(int faction, int group, AiPosture posture, Vector2? area, float radius, IReadOnlyList<string>? forces, IReadOnlyList<int>? units = null)
    {
        if (group <= 0) { SetDirective(faction, posture, null, area, radius); return; }
        var s = State(faction);
        var d = s.Directives.Find(x => x.Id == group);
        if (d == null)
        {
            d = new TacDirective { Id = group };
            int at = s.Directives.FindIndex(x => x.Id > group);
            if (at < 0) s.Directives.Add(d); else s.Directives.Insert(at, d);
        }
        d.Posture = posture;
        d.HasArea = area != null;
        d.Area = area ?? default;
        d.AreaRadius = MathF.Max(0, radius);
        d.Forces.Clear(); if (forces != null) d.Forces.AddRange(forces);
        d.Units.Clear(); if (units != null) d.Units.AddRange(units);
    }

    /// <summary>
    /// 미션 (stage 3A, 사용자 결정 5): 세력의 철수 문턱 (지시 그룹 병력 비율, 0 = 끝까지) · 목표 고수 정도 (0~1, 0.5 = 프로필 그대로 — 공격 한계 · 철수 문턱 배수 2 × (1 − 값)).
    /// null = 프로필 값 (strategy.withdrawAt · commitment). 첫 Tick 전에 부른다 — AI 상태로 저장된다.
    /// </summary>
    public void SetMissionParams(int faction, float? withdrawAt, float? commitment)
    {
        var s = State(faction);
        s.MissionWithdrawAt = withdrawAt is float a ? Math.Clamp(a, 0f, 0.95f) : -1f;
        s.MissionCommitment = commitment is float c ? Math.Clamp(c, 0f, 1f) : -1f;
    }

    /// <summary>미션: 비무장 유닛을 이 AI 의 편성에서 뺀다 (첫 Tick 전에 부른다).</summary>
    public void SetSkipUnarmed(int faction, bool on) => State(faction).SkipUnarmed = on;

    /// <summary>거리 배율 = 소총 사거리 / 16 (1단계 상수는 소총 16 에서 맞춘 칸 수 — 사거리 · 지도 ×1.5 같은 개편에 따라 늘어난다). 호출마다 세력 값으로.</summary>
    float K = 1f;

    static long Sec(float s) => (long)MathF.Round(s / Config.SimDt);

    public void Tick(World w, int faction)
    {
        var s = State(faction);
        K = (s.RifleRange > 0 ? s.RifleRange : 16f) / 16f;
        Perceive(w, s);
        if (s.Phase == 0) { Setup(w, s); return; }
        bool all = false;
        if (s.Phase == 1) { Resolve(w, s); all = true; }
        else if (s.PendingIntake) { ResolveIntake(w, s); all = true; }
        long call = (w.Tick + faction * 7) / 15;   // World.Step 이 15틱마다 부른다 → 호출 번호 (틱의 함수 — 저장 불필요)
        if (all || call % 8 == 0) Strategy(w, s);
        for (int i = 0; i < s.Regiments.Count; i++)
            if (all || (call + i) % 4 == 0) RegimentThink(w, s, s.Regiments[i]);
        for (int i = 0; i < s.Battalions.Count; i++)
            if (all || (call + i + 2) % 4 == 0) BattalionThink(w, s, s.Battalions[i]);
        for (int i = 0; i < s.Groups.Count; i++)
            if (all || (call + i) % 2 == 0) CompanyThink(w, s, s.Groups[i]);
    }

    // ================================================================ 인지 (보이는 적만)

    /// <summary>faction 이 지금 볼 수 있는 적대 유닛 (World.Units 순서). 테스트가 안개 규칙을 확인하는 데도 쓴다.</summary>
    public static void VisibleEnemies(World w, int faction, List<Unit> into)
    {
        into.Clear();
        foreach (var u in w.Units)
            if (u.Alive && u.Owner > 0 && u.Owner != faction && w.IsHostile(faction, u.Owner) && w.Fog.CanSee(faction, u)) into.Add(u);
    }

    void Perceive(World w, TacFaction s)
    {
        VisibleEnemies(w, s.Faction, enemies);
        Grid.Build(w.Map, enemies);
        if (enemies.Count > 0) s.LastContactTick = w.Tick;
        canHurtArmor = false;
        foreach (var u in w.UnitsOf(s.Faction))
            if (u.Alive && u.Owner == s.Faction && u.Weapon != null && u.Weapon.CanDamage("armored")) { canHurtArmor = true; break; }
    }

    bool canHurtArmor;   // 이번 호출: 우리 편에 장갑을 깰 무기가 있나 (없으면 전차는 표적 · 목표에서 뺀다 — 소총으로 전차를 둘러싸고 서 있지 않게)

    /// <summary>자율 정찰 유닛 (Def.Scout — World.StepScouts 가 부린다): 중대 밖이면 편성하지 않고 관측자로만 쓴다 (보는 것은 Fog · SeenBySpotter 로 들어온다).</summary>
    internal static bool IsAutonomousScout(Unit u) => u.Def.Scout;

    // ================================================================ 편성 (처음 한 번 + 증원 편입)

    static TacKind KindOf(Unit u)
    {
        var d = u.Def;
        var wpn = u.Weapon;
        if (d.IsSupplyWagon || wpn == null) return TacKind.Wagon;
        if (World.IsIndirect(wpn)) return TacKind.Artillery;
        if (d.IsArmored) return TacKind.Armor;
        if (d.Category == "gun") return TacKind.Fire;
        if (d.Mounted) return TacKind.Cavalry;
        if (u.CanDeploy || wpn.Range >= LongRange) return TacKind.Fire;
        if (d.Spotter && !d.IsOfficer) return TacKind.Scout;
        return TacKind.Line;
    }

    /// <summary>장거리 화력 기준 = 소총 사거리 × 1.25 (옛 20칸 @ 소총 16; 2026-10-19 소총 24 → 30 — 저격총 · 대포만, 장총은 보병).</summary>
    static float LongRange => (Defs.Current.Weapon("rifle")?.Range ?? 16f) * 1.25f;

    static int MaxOf(TacKind k) => k switch
    {
        TacKind.Fire => 8, TacKind.Artillery => 6, TacKind.Cavalry => 30, TacKind.Armor => 6, TacKind.Wagon => 2, TacKind.Scout => 6, _ => 50,
    };

    /// <summary>유닛 Id → 지시 그룹 Id (작업값: 편성 · 편입 때만 만든다). 그룹 0 은 빠진다.</summary>
    Dictionary<int, int> DirectiveMap(World w, TacFaction s)
    {
        var map = new Dictionary<int, int>();
        foreach (var d in s.Directives)
        {
            if (d.Id == 0) continue;
            foreach (int id in d.Units) map[id] = d.Id;
            if (w.Mission is { } m)
                foreach (var f in d.Forces)
                    if (m.ForceUnits.TryGetValue(f, out var ids)) foreach (int id in ids) map[id] = d.Id;
        }
        return map;
    }

    void Setup(World w, TacFaction s)
    {
        int f = s.Faction;
        own.Clear();
        foreach (var u in w.UnitsOf(f)) if (u.Alive && u.Owner == f && !(s.SkipUnarmed && u.Weapon == null)) own.Add(u);
        foreach (var u in w.UnitsOf(f)) s.LastUnitId = Math.Max(s.LastUnitId, u.Id);
        s.Phase = 1;
        s.SetupTick = s.LastContactTick = w.Tick;
        // 프로필 · 태세
        var mapDefs = MapFile.FactionsOf(w.Map);
        var md = f - 1 < mapDefs.Count ? mapDefs[f - 1] : null;
        s.Profile = AiProfiles.Resolve(s.DirectiveProfile ?? profileOverride, md?.AiProfile, md?.En);
        if (!s.DirectiveSet) s.Posture = AiProfile.ParsePosture(md?.AiPosture) ?? s.Profile.DefaultPosture;
        if (own.Count == 0) { s.Home = AiHelpers.StartOf(w, f); s.Front = s.Dir = new Vector2(1, 0); return; }

        Vector2 c = Centroid(own);
        s.Home = c;
        Vector2 enemyRef = NearestRivalStart(w, f, c);
        Vector2 fr = enemyRef - c;
        s.Front = s.Dir = fr.LengthSquared() > 1f ? Vector2.Normalize(fr) : new Vector2(1, 0);
        // 측면 (기병 · 측면 공격 쪽): 지도 가장자리까지 여유가 큰 쪽
        Vector2 side = Perp(s.Front);
        s.FlankSign = RoomAlong(w.Map, c, side) >= RoomAlong(w.Map, c, -side) ? 1 : -1;
        s.RifleRange = RifleRangeOf(own);
        Organise(w, s, own);
    }

    /// <summary>소총 사거리 (거리 상수의 기준 — 정의에서 읽는다): 소총 보병 무기 사거리의 최댓값, 없으면 무장 유닛 최솟값, 그래도 없으면 16.</summary>
    static float RifleRangeOf(List<Unit> l)
    {
        float r = 0, any = float.MaxValue;
        foreach (var u in l)
        {
            if (u.Weapon == null || u.IsOfficer) continue;
            if (KindOf(u) == TacKind.Line) r = MathF.Max(r, u.Weapon.Range);
            any = MathF.Min(any, u.Weapon.Range);
        }
        return r > 0 ? r : any < float.MaxValue ? any : 16f;
    }

    /// <summary>units 를 지시 그룹 · 종류별로 묶는다: 이미 있는 중대는 그대로, 중대 없는 소총 보병은 50명 안팎 FormCompany (대기), 나머지는 분견대.</summary>
    void Organise(World w, TacFaction s, List<Unit> units)
    {
        int f = s.Faction;
        var dmap = DirectiveMap(w, s);
        int DirOf(Unit u) => dmap.TryGetValue(u.Id, out int d) ? d : 0;
        Vector2 side = Perp(s.Front);
        var inSet = new HashSet<int>();
        foreach (var u in units) inSet.Add(u.Id);
        // 이미 있는 중대 (지도 배치 · 시작 병력 · 미션 증원) — 구성원 다수의 병과, 첫 구성원의 지시 그룹
        Span<int> counts = stackalloc int[7];   // 루프 밖 한 번 (CA2014), 중대마다 지움
        foreach (var co in w.Companies)
        {
            if (co.Owner != f || co.Members.Count == 0 || !co.Members.Exists(u => inSet.Contains(u.Id))) continue;
            if (s.Groups.Exists(g => g.CompanyId == co.Id)) continue;
            counts.Clear();
            foreach (var u in co.Members) if (!u.IsOfficer) counts[(int)KindOf(u)]++;
            int best = 0;
            for (int k = 1; k < 7; k++) if (counts[k] > counts[best]) best = k;
            s.Groups.Add(new TacGroup { Id = s.NextGroupId++, Kind = (TacKind)best, CompanyId = co.Id, Directive = DirOf(co.Members[0]), Initial = co.Members.Count, Alive = co.Members.Count, Center = Centroid(co.Members) });
        }
        Comparison<Unit> lateral = (a, b) =>
        {
            int r = Vector2.Dot(a.Pos, side).CompareTo(Vector2.Dot(b.Pos, side));
            if (r != 0) return r;
            r = Vector2.Dot(a.Pos, s.Front).CompareTo(Vector2.Dot(b.Pos, s.Front));
            return r != 0 ? r : a.Id.CompareTo(b.Id);
        };
        foreach (var d in s.Directives)
        {
            // 중대 없는 유닛: 종류별로 옆 방향 순서로 묶는다
            var loose = new List<Unit>[7];
            for (int k = 0; k < 7; k++) loose[k] = new List<Unit>();
            foreach (var u in units) if (u.Company == null && !IsAutonomousScout(u) && DirOf(u) == d.Id) loose[(int)KindOf(u)].Add(u);
            // 소총 중대: 50명 안팎으로 나눠 FormCompany (장교는 옆 위치 순서로 고르게)
            var line = loose[(int)TacKind.Line];
            line.Sort(lateral);
            var men = line.Where(u => !u.IsOfficer).ToList();
            var officers = line.Where(u => u.IsOfficer).ToList();
            if (men.Count >= 2)
            {
                int k = (men.Count + 49) / 50;
                int size = (men.Count + k - 1) / k;
                var chunks = new List<List<Unit>>();
                for (int i = 0; i < men.Count; i += size) chunks.Add(men.GetRange(i, Math.Min(size, men.Count - i)));
                for (int j = 0; j < officers.Count; j++)
                {
                    int ci = Math.Min(chunks.Count - 1, j * chunks.Count / Math.Max(1, officers.Count));
                    if (chunks[ci].Count < Company.MaxSize) chunks[ci].Add(officers[j]);
                }
                foreach (var ch in chunks)
                {
                    w.FormCompany(f, ch);
                    var g = new TacGroup { Id = s.NextGroupId++, Kind = TacKind.Line, Pending = true, Directive = d.Id, Initial = ch.Count, Alive = ch.Count, Center = Centroid(ch) };
                    foreach (var u in ch.OrderBy(u => u.Id)) g.Units.Add(u.Id);
                    s.Groups.Add(g);
                }
            }
            else if (line.Count > 0) AddDetachments(s, TacKind.Line, line, d.Id);
            for (int k = 1; k < 7; k++)
            {
                var l = loose[k];
                if (l.Count == 0) continue;
                l.Sort(lateral);
                if ((TacKind)k == TacKind.Fire) { AddFireDetachments(s, l, d.Id); continue; }   // N20: 포는 종류별 분견대 (TacticalAi.Artillery.cs)
                AddDetachments(s, (TacKind)k, l, d.Id);
            }
        }
    }

    void AddDetachments(TacFaction s, TacKind kind, List<Unit> sorted, int directive)
    {
        int max = MaxOf(kind);
        int n = (sorted.Count + max - 1) / max;
        int per = (sorted.Count + n - 1) / n;
        for (int i = 0; i < sorted.Count; i += per)
        {
            var part = sorted.GetRange(i, Math.Min(per, sorted.Count - i));
            var g = new TacGroup { Id = s.NextGroupId++, Kind = kind, Directive = directive, Initial = part.Count, Alive = part.Count, Center = Centroid(part) };
            foreach (var u in part.OrderBy(u => u.Id)) g.Units.Add(u.Id);
            s.Groups.Add(g);
        }
    }

    /// <summary>편성 명령이 실행된 뒤: 대기 중인 묶음을 중대 Id 로 바꾸고 대대 · 연대를 짠다.</summary>
    void Resolve(World w, TacFaction s)
    {
        ResolvePending(w, s);
        RefreshGroups(w, s);
        BuildBattalions(w, s);
        SetHomes(s);
        s.Phase = 2;
    }

    void SetHomes(TacFaction s)
    {
        foreach (var d in s.Directives)
        {
            if (d.HomeSet) continue;
            Vector2 sum = Vector2.Zero; int n = 0;
            foreach (var g in s.Groups) if (g.Directive == d.Id) { sum += g.Center * g.Alive; n += g.Alive; }
            if (n > 0) { d.HomeSet = true; d.Home = sum / n; d.Dir = s.Front; }
        }
    }

    void ResolvePending(World w, TacFaction s)
    {
        foreach (var g in s.Groups)
        {
            if (!g.Pending) continue;
            g.Pending = false;
            foreach (int id in g.Units)
                if (w.Find(id) is Unit u && u.Company is { } co && co.Owner == s.Faction) { g.CompanyId = co.Id; break; }
            if (g.CompanyId != 0) g.Units.Clear();   // 편성 실패면 분견대로 남는다
        }
    }

    /// <summary>
    /// 대대 · 연대 짜기 (처음 · 증원): 대대가 없는 소총 중대를 지시 그룹별로 — 같은 월드 대대 소속이면 그 대대 (번호 연결), 나머지는 옆 순서 4개씩
    /// (월드 대대가 9개 미만이면 FormBattalion 명령 → 다음 전략 호출에 LinkWorld 가 연결). 화력 · 기갑 · 마차는 같은 그룹의 옆 위치가 가까운 대대에.
    /// 새 대대는 그 그룹의 연대에 (운용 중이면 가장 약한 연대, 연대 크기 +1 을 넘으면 새 연대).
    /// </summary>
    void BuildBattalions(World w, TacFaction s)
    {
        int f = s.Faction;
        Vector2 side = Perp(s.Front);
        int regSize = Math.Clamp(s.Profile.Strategy.RegimentSize, 2, 4);
        int worldBats = 0;
        foreach (var b in w.Battalions) if (b.Owner == f) worldBats++;
        foreach (var d in s.Directives)
        {
            var lines = s.Groups.Where(g => g.Kind == TacKind.Line && g.Battalion == 0 && g.Directive == d.Id && !g.Pending)
                                .OrderBy(g => Vector2.Dot(g.Center, side)).ThenBy(g => g.Id).ToList();
            if (lines.Count == 0) continue;
            var fresh = new List<TacBattalion>();
            // 월드 대대 소속 중대 (미션 battalion · 교전 테스트 대대)
            var byWorld = new SortedDictionary<int, List<TacGroup>>();
            var rest = new List<TacGroup>();
            foreach (var g in lines)
            {
                var co = g.CompanyId != 0 ? w.FindCompany(g.CompanyId) : null;
                if (co?.Battalion is { } wb) { if (!byWorld.TryGetValue(wb.Id, out var l)) byWorld[wb.Id] = l = new(); l.Add(g); }
                else rest.Add(g);
            }
            foreach (var (wid, l) in byWorld)
            {
                var existing = s.Battalions.Find(b => b.WorldId == wid);
                var b = existing ?? new TacBattalion { Id = s.NextBattalionId++, WorldId = wid, Number = w.Battalions.First(x => x.Id == wid).Number };
                foreach (var g in l) { g.Battalion = b.Id; b.Initial += g.Initial; }
                if (existing == null) { s.Battalions.Add(b); fresh.Add(b); }
            }
            if (rest.Count > 0)
            {
                int nb = (rest.Count + 3) / 4;
                int per = (rest.Count + nb - 1) / nb;
                for (int i = 0; i < rest.Count; i += per)
                {
                    var b = new TacBattalion { Id = s.NextBattalionId++ };
                    var cos = new List<Company>();
                    for (int k = i; k < Math.Min(rest.Count, i + per); k++)
                    {
                        rest[k].Battalion = b.Id; b.Initial += rest[k].Initial;
                        if (rest[k].CompanyId != 0 && w.FindCompany(rest[k].CompanyId) is { } co) cos.Add(co);
                    }
                    if (cos.Count > 0 && worldBats < 9) { w.FormBattalion(f, cos); worldBats++; }   // 월드 대대 · 번호 (1~9) — 다음 전략 호출에 연결
                    s.Battalions.Add(b); fresh.Add(b);
                }
            }
            RefreshBattalions(s);
            // 연대: 운용 중이면 그룹의 기존 연대 (가장 약한 것, 크기 +1 미만) → 남으면 새 연대 (옆 순서 regSize 개씩)
            fresh.Sort((a, b) => { int r = Vector2.Dot(a.Center, side).CompareTo(Vector2.Dot(b.Center, side)); return r != 0 ? r : a.Id.CompareTo(b.Id); });
            var queue = new List<TacBattalion>();
            foreach (var b in fresh)
            {
                TacRegiment? into = null;
                if (s.Phase == 2)
                    foreach (var r in s.Regiments)
                    {
                        if (r.Directive != d.Id || r.Spent) continue;
                        int cnt = 0;
                        foreach (var x in s.Battalions) if (x.Regiment == r.Id) cnt++;
                        if (cnt >= regSize + 1) continue;
                        if (into == null || r.Alive * (float)Math.Max(1, into.Initial) < into.Alive * (float)Math.Max(1, r.Initial)) into = r;
                    }
                if (into != null) { b.Regiment = into.Id; into.Initial += b.Initial; s.Reinforced++; }
                else queue.Add(b);
            }
            if (queue.Count > 0)
            {
                int nr = (queue.Count + regSize - 1) / regSize;
                int per = (queue.Count + nr - 1) / nr;
                for (int i = 0; i < queue.Count; i += per)
                {
                    var r = new TacRegiment { Id = s.NextRegimentId++, Directive = d.Id };
                    for (int k = i; k < Math.Min(queue.Count, i + per); k++) { queue[k].Regiment = r.Id; r.Initial += queue[k].Initial; }
                    s.Regiments.Add(r);
                    if (s.Phase == 2) s.Reinforced++;
                }
            }
        }
        RefreshBattalions(s);
        // 화력 · 기갑 · 마차 → 같은 지시 그룹에서 옆 위치가 가장 가까운 대대
        foreach (var g in s.Groups)
        {
            if (g.Battalion != 0 || g.Kind is not (TacKind.Fire or TacKind.Armor or TacKind.Wagon)) continue;
            float gl = Vector2.Dot(g.Center, side);
            TacBattalion? best = null; float bd = float.MaxValue;
            foreach (var b in s.Battalions)
            {
                if (DirectiveOfBattalion(s, b) != g.Directive) continue;
                float dd = MathF.Abs(Vector2.Dot(b.Center, side) - gl);
                if (dd < bd) { bd = dd; best = b; }
            }
            if (best != null) g.Battalion = best.Id;
        }
    }

    static int DirectiveOfBattalion(TacFaction s, TacBattalion b)
    {
        foreach (var r in s.Regiments) if (r.Id == b.Regiment) return r.Directive;
        return 0;
    }

    /// <summary>증원 편입 (전략 호출마다): LastUnitId 보다 큰 Id 의 새 자기 유닛 → 편성 (지시 그룹 = 미션 병력), 다음 호출에 대대 · 연대 배정.</summary>
    void Intake(World w, TacFaction s)
    {
        int f = s.Faction, maxId = s.LastUnitId;
        own.Clear();
        foreach (var u in w.UnitsOf(f))
        {
            if (u.Id <= s.LastUnitId) continue;
            maxId = Math.Max(maxId, u.Id);
            if (u.Alive && u.Owner == f && !(s.SkipUnarmed && u.Weapon == null)) own.Add(u);
        }
        s.LastUnitId = maxId;
        if (own.Count == 0) return;
        if (s.RifleRange <= 0) s.RifleRange = RifleRangeOf(own);
        Organise(w, s, own);
        s.PendingIntake = true;
    }

    void ResolveIntake(World w, TacFaction s)
    {
        s.PendingIntake = false;
        ResolvePending(w, s);
        RefreshGroups(w, s);
        BuildBattalions(w, s);
        SetHomes(s);
    }

    /// <summary>AI 대대 ↔ 월드 대대 연결: 구성 소총 중대가 모두 한 월드 대대 소속이면 그 Id · 번호 (FormBattalion 결과 · 편제 변경 반영).</summary>
    void LinkWorld(World w, TacFaction s)
    {
        foreach (var b in s.Battalions)
        {
            int wid = -1;
            foreach (var g in s.Groups)
            {
                if (g.Battalion != b.Id || g.Kind != TacKind.Line || g.CompanyId == 0) continue;
                int id = w.FindCompany(g.CompanyId)?.Battalion?.Id ?? 0;
                if (wid == -1) wid = id; else if (wid != id) { wid = 0; break; }
            }
            if (wid <= 0) { b.WorldId = 0; b.Number = 0; continue; }
            b.WorldId = wid;
            b.Number = w.Battalions.Find(x => x.Id == wid)?.Number ?? 0;
        }
    }

    // ================================================================ 공용 도우미

    static Vector2 Perp(Vector2 d) => new(-d.Y, d.X);

    static Vector2 Centroid(List<Unit> l)
    {
        Vector2 c = Vector2.Zero;
        foreach (var u in l) c += u.Pos;
        return l.Count > 0 ? c / l.Count : c;
    }

    static Vector2 SafeNorm(Vector2 v, Vector2 fallback) => v.LengthSquared() > 1e-4f ? Vector2.Normalize(v) : fallback;

    static float RoomAlong(GameMap m, Vector2 p, Vector2 d)
    {
        float t = float.MaxValue;
        if (d.X > 1e-4f) t = MathF.Min(t, (m.Width - 1 - p.X) / d.X); else if (d.X < -1e-4f) t = MathF.Min(t, (p.X - 1) / -d.X);
        if (d.Y > 1e-4f) t = MathF.Min(t, (m.Height - 1 - p.Y) / d.Y); else if (d.Y < -1e-4f) t = MathF.Min(t, (p.Y - 1) / -d.Y);
        return t == float.MaxValue ? 0 : MathF.Max(0, t);
    }

    static Vector2 ClampMap(GameMap m, Vector2 p) => new(Math.Clamp(p.X, 1.5f, m.Width - 1.5f), Math.Clamp(p.Y, 1.5f, m.Height - 1.5f));

    static Vector2 NearestRivalStart(World w, int f, Vector2 from)
    {
        Vector2 best = new(w.Map.Width / 2f, w.Map.Height / 2f);
        float bd = float.MaxValue;
        for (int g = 1; g < w.Factions.Count; g++)
        {
            if (!AiHelpers.IsRival(w, f, g) || !AiHelpers.IsAlive(w, g)) continue;
            var p = AiHelpers.StartOf(w, g);
            float d = Vector2.DistanceSquared(p, from);
            if (d < bd) { bd = d; best = p; }
        }
        return best;
    }

    static float Hash01(int a, int b)
    {
        uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    /// <summary>구성원 (살아 있는 자기 유닛) 을 into 에 모으고 병력 · 무게중심을 갱신한다.</summary>
    void Members(World w, TacFaction s, TacGroup g, List<Unit> into)
    {
        into.Clear();
        if (g.CompanyId != 0)
        {
            var co = w.FindCompany(g.CompanyId);
            if (co != null) foreach (var u in co.Members) if (u.Alive && u.Owner == s.Faction) into.Add(u);
        }
        else
        {
            int keep = 0;
            for (int i = 0; i < g.Units.Count; i++)
                if (w.Find(g.Units[i]) is Unit u && u.Alive && u.Owner == s.Faction) { g.Units[keep++] = g.Units[i]; into.Add(u); }
            g.Units.RemoveRange(keep, g.Units.Count - keep);
        }
        g.Alive = into.Count;
        if (into.Count > 0) g.Center = Centroid(into);
    }

    void RefreshGroups(World w, TacFaction s)
    {
        for (int i = s.Groups.Count - 1; i >= 0; i--)
        {
            var g = s.Groups[i];
            if (g.Pending) continue;
            Members(w, s, g, buf);
            if (g.Alive == 0) s.Groups.RemoveAt(i);
        }
        RefreshBattalions(s);
    }

    static void RefreshBattalions(TacFaction s)
    {
        for (int i = s.Battalions.Count - 1; i >= 0; i--)
        {
            var b = s.Battalions[i];
            Vector2 sum = Vector2.Zero; int n = 0; bool pending = false;
            foreach (var g in s.Groups)
            {
                if (g.Battalion != b.Id) continue;
                if (g.Kind == TacKind.Line) { sum += g.Center * g.Alive; n += g.Alive; }
                pending |= g.Pending;
            }
            b.Alive = n;
            if (n == 0 && !pending)
            {
                foreach (var g in s.Groups) if (g.Battalion == b.Id) g.Battalion = 0;   // 붙은 화력 · 마차는 전략 직속으로
                foreach (var r in s.Regiments) if (r.ReserveBattalion == b.Id) r.ReserveBattalion = 0;
                s.Battalions.RemoveAt(i);
                continue;
            }
            if (n > 0) b.Center = sum / n;
        }
        for (int i = s.Regiments.Count - 1; i >= 0; i--)
        {
            var r = s.Regiments[i];
            Vector2 sum = Vector2.Zero; int n = 0; bool any = false;
            foreach (var b in s.Battalions) if (b.Regiment == r.Id) { sum += b.Center * b.Alive; n += b.Alive; any = true; }
            r.Alive = n;
            if (!any)
            {
                foreach (var g in s.Groups) if (g.Supports == r.Id) g.Supports = 0;
                foreach (var d in s.Directives) if (d.ReserveRegiment == r.Id) d.ReserveRegiment = 0;
                s.Regiments.RemoveAt(i);
                continue;
            }
            if (n > 0) r.Center = sum / n;
        }
    }

    /// <summary>중대 정면 폭 (칸): 횡대 2열 기준.</summary>
    static float Frontage(TacGroup g) => Math.Clamp(g.Alive * 0.5f + 2f, 6f, 48f);

    /// <summary>엄폐 점수: 지형 엄폐 (숲 · 잔해 · 참호 · 포탄 구덩이 · 가옥 옆) + 고지, 철조망 감점.</summary>
    static float CoverScore(GameMap m, Vector2 q) => TacMap.TileCover(m, q) + (m.IsHigh(q) ? 0.6f : 0);

    /// <summary>p 둘레 r 칸 안에서 엄폐가 좋은 칸 (너무 멀거나 적 쪽으로 나가면 감점). 나아지는 게 없으면 p.</summary>
    static Vector2 BestCover(GameMap m, Vector2 p, int r, Vector2 enemy)
    {
        float baseScore = CoverScore(m, p);
        float best = baseScore + 0.2f;
        Vector2 bestP = p;
        var (px, py) = GameMap.TileOf(p);
        float dpe = Vector2.Distance(p, enemy);
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            int x = px + dx, y = py + dy;
            if (!m.IsWalkable(x, y)) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float sc = CoverScore(m, q) - 0.15f * Vector2.Distance(q, p);
            if (Vector2.Distance(q, enemy) < dpe - 1.5f) sc -= 0.3f;
            if (sc > best) { best = sc; bestP = q; }
        }
        return bestP;
    }

    // ================================================================ 명령 (World 공개 API 만)

    enum Ord { AttackMove, Move, Quick, Retreat, Cross }   // Cross = 도하 강행 속보 (pronemove 엎드림 규칙 면제)

    /// <summary>N79a: 이번 후퇴 명령은 중대 집결 지점을 따른다 (자기 후퇴 판단에서만 켠다 — 그 밖의 AI 후퇴는 기존 목표 그대로).</summary>
    bool retreatUseRally;

    void Order(World w, TacFaction s, TacGroup g, List<Unit> m, Ord k, Vector2 p, Vector2 dir, float width, bool marchFire = false)
    {
        if (m.Count == 0) return;
        Issue(w, s, m, k, p, dir, width, marchFire);
        g.OrderPoint = ClampMap(w.Map, p);
        g.OrderTick = w.Tick;
    }

    /// <summary>무리 이동 명령 (깃발 횡대: 방향 · 폭). 속보가 안 되는 유닛은 보통 이동.</summary>
    void Issue(World w, TacFaction s, List<Unit> m, Ord k, Vector2 p, Vector2 dir, float width, bool marchFire = false)
    {
        if (m.Count == 0) return;
        p = ClampMap(w.Map, p);
        var form = new FormSpec((int)FormType.Line, 0, SafeNorm(dir, Vector2.Zero), MathF.Max(0, width));
        switch (k)
        {
            case Ord.AttackMove: w.AttackMove(s.Faction, m, p, form); break;
            case Ord.Move: w.Move(s.Faction, m, p, form); break;
            case Ord.Retreat: w.Retreat(s.Faction, m, p, form, retreatUseRally); break;   // 후퇴 = 속보 (가능한 병사) + 후퇴 표시 (Pursuit.cs)
            case Ord.Quick:
            case Ord.Cross:
                buf2.Clear();
                foreach (var u in m) if (!u.CanQuickStep) buf2.Add(u);
                if (buf2.Count == m.Count) w.Move(s.Faction, m, p, form);
                else
                {
                    w.QuickStep(s.Faction, m, p, form, k == Ord.Cross);
                    if (buf2.Count > 0) w.Move(s.Faction, buf2, p, form);
                }
                break;
        }
        if (m.Exists(u => u.Def.MarchFireDefault))   // 돌격보병 중대: 돌격 사격이 정체성 — 항상 켠다 (실제 사격은 공격 이동 접근 중에만 판단, MarchCommand.cs)
        {
            w.SetMarchFire(s.Faction, m.FindAll(u => u.Def.MarchFireDefault), true);
            w.SetMarchFire(s.Faction, m.FindAll(u => !u.Def.MarchFireDefault && !(u.Company?.Members.Exists(x => x.Def.MarchFireDefault) ?? false)), k == Ord.AttackMove && marchFire);   // 돌격보병이 섞인 중대는 장교 때문에 꺼지지 않게
        }
        else w.SetMarchFire(s.Faction, m, k == Ord.AttackMove && marchFire);   // 돌격 사격은 공격 이동의 접근 (사격 진지로 다가가기) 에서만 켠다 — 이동 · 속보 · 후퇴에서는 끈다
        s.Orders++;
    }

    void HoldAll(World w, TacFaction s, TacGroup g, List<Unit> m)
    {
        if (m.Count == 0) return;
        w.Hold(s.Faction, m);
        g.OrderPoint = g.Center;
        g.OrderTick = w.Tick;
        s.Orders++;
    }

    static bool Due(World w, TacGroup g, float seconds) => w.Tick - g.OrderTick >= Sec(seconds);

    /// <summary>
    /// 새 이동 명령을 낼지: 동작이 바뀌었으면 (2초 간격) 바로, 같은 동작이면 목표가 크게 (minMove 와 남은 거리 30% 중 큰 값 넘게) 바뀌고 6초 지났을 때만.
    /// 중대 깃발은 명령마다 대형을 새로 짜므로 자잘한 재명령은 부대를 멈춰 세운다.
    /// </summary>
    static bool Retarget(World w, TacGroup g, TacState st, Vector2 p, float minMove, bool contactLimit = true)
    {
        if (g.State != st) return Due(w, g, 2f);
        float need = MathF.Max(minMove, 0.3f * Vector2.Distance(g.Center, p));
        float moved = Vector2.Distance(p, g.OrderPoint);
        bool ok = moved > need && Due(w, g, 6f);
        // F1 2차: 접적 · 이탈 중인 중대에는 같은 동작의 재명령을 aiContactReorderDist 칸 넘게 바뀌고 aiContactReorderSec 지났을 때만 (왔다갔다 명령 줄이기)
        if (ok && contactLimit && g.CompanyId > 0 && w.FindCompany(g.CompanyId) is { } co && w.ContactOf(co) != ContactState.None)
        {
            var cr = w.Defs.CombatRules;
            if (moved <= MathF.Max(need, cr.AiContactReorderDist) || !Due(w, g, MathF.Max(6f, cr.AiContactReorderSec))) { w.Stats.AiContactHolds++; return false; }
        }
        return ok;
    }
}
