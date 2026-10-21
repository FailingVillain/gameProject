using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>밸런스 데이터에서 AI 가 쓰는 정의를 역할별로 찾아 둔 것 (키 이름에 의존하지 않음).</summary>
public sealed class AiDefs
{
    public readonly BuildingDef Hq;
    public readonly UnitDef Infantry, Scout;
    public readonly UnitDef Recruit, Veteran;   // 병사 등급: 가장 약한 것 · 가장 강한 것 (등급이 둘뿐이면 겹칠 수 있다)
    public readonly UnitDef? AntiArmor;         // 기갑에 보너스 피해를 주는 보병
    public readonly UnitDef? Tank;

    static AiDefs? cached;
    static Defs? cachedFor;

    AiDefs(Defs d)
    {
        Hq = d.Headquarters;
        // 방향 변경 §0-1: 비용이 없으므로 체력 → 명중률 → 키 순으로 등급을 매긴다
        var fighters = d.Units.Values.Where(u => u.CanCapture && u.Weapon != null && u.IsInfantry && !u.IsOfficer && u.Factions == null && !u.Common)   // 세력 전용 · 공용 유닛 (마적 · 의용병)은 기본 AI 편성 후보에서 제외
            .OrderBy(u => u.Hp).ThenBy(u => u.Accuracy).ThenBy(u => u.Key, StringComparer.Ordinal).ToList();
        if (fighters.Count == 0) fighters = d.Units.Values.Where(u => u.Weapon != null).OrderBy(u => u.Key, StringComparer.Ordinal).ToList();
        AntiArmor = fighters.FirstOrDefault(u => d.Weapon(u.Weapon)?.IsAntiArmor == true);
        var rest = fighters.Where(u => u != AntiArmor).ToList();
        if (rest.Count == 0) rest = fighters;
        Scout = rest.Skip(1).OrderByDescending(u => u.Sight).ThenByDescending(u => u.Speed).ThenBy(u => u.Key, StringComparer.Ordinal).FirstOrDefault() ?? rest[0];
        var line = rest.Where(u => u != Scout).ToList();
        if (line.Count == 0) line = rest;
        Recruit = line[0];
        Veteran = line[^1];
        Infantry = line[line.Count / 2];
        Tank = d.Units.Values.Where(u => u.IsArmored && u.Weapon != null).OrderBy(u => u.Key, StringComparer.Ordinal).FirstOrDefault();
    }

    public static AiDefs For(Defs d)
    {
        if (cached == null || cachedFor != d) { cached = new AiDefs(d); cachedFor = d; }
        return cached;
    }

    /// <summary>점령 시설 우선순위 점수 (작을수록 먼저): 대도시 &gt; 도시 &gt; 그 밖의 점령지.</summary>
    public float SitePenalty(BuildingDef site) => site.City ? (site.Headquarters ? -10 : 0) : 16;
}

/// <summary>
/// AI 공용 판단 보조. 관계 판단은 여기만 거친다 (정적 팀 — WorldTeams.cs, 실행 중 불변):
///  · IsRival  = 같은 팀이 아닌 다른 세력 → 공격 · 점령 · 정찰 대상 후보
///  · IsHostile = 적대 → 보이는 적 병력 · 위협 판단 (정적 팀에서는 IsRival 과 같다)
/// </summary>
public static class AiHelpers
{
    /// <summary>적대 세력인지 — 보이는 적 병력 · 위협 판단용.</summary>
    public static bool IsHostile(World w, int me, int other) => w.IsHostile(me, other);

    /// <summary>공격 · 점령 · 정찰 대상이 될 수 있는 경쟁 세력인지 (같은 팀이 아닌 다른 세력).</summary>
    public static bool IsRival(World w, int me, int other) => me != other && other > 0 && !w.Allied(me, other);

    /// <summary>살아 있는(패배하지 않은) 세력인지.</summary>
    /// <summary>공개 지표: 살아 있는 유닛 수 (방향 변경 §0-1 — 인구 대신).</summary>
    public static int ArmySize(World w, int faction)
    {
        int n = 0;
        foreach (var u in w.UnitsOf(faction)) if (u.Alive && u.Owner == faction) n++;
        return n;
    }

    public static bool IsAlive(World w, int faction) =>
        faction > 0 && faction < w.Factions.Count && !w.Factions[faction].Defeated;

    /// <summary>세력의 시작 위치 중심 (공개 정보). 시작 위치가 없으면 맵 중앙.</summary>
    public static Vector2 StartOf(World w, int faction)
    {
        int i = faction - 1;
        if (i < 0 || i >= w.Map.StartTiles.Count) return new Vector2(w.Map.Width / 2f, w.Map.Height / 2f);
        var (sx, sy) = w.Map.StartTiles[i];
        return new Vector2(sx + 0.5f, sy + 0.5f);
    }
}

/// <summary>마지막으로 목격한 적 건물 (Owner = 목격 당시 소유 세력).</summary>
public readonly record struct EnemyMemory(Vector2 Pos, BuildingDef Def, int Owner, long SeenTick);

/// <summary>AI 가 세력별로 유지하는 공통 상태: 자기 엔티티 캐시 · 기지 기하 · 적 건물 기억 · 표적 세력.</summary>
public class AiBaseState
{
    public int Faction;
    public bool Initialized;
    public Building? Hq;
    public Vector2 Home;                 // 사령부 중심 (잃었으면 마지막 위치)
    public Vector2 Center;               // 맵 중앙
    public Vector2 Front;                // 맵 중앙 쪽 단위 벡터 (2인 맵에서는 곧 적 방향)
    public Vector2 Side;                 // Front 에 수직인 단위 벡터
    public Vector2 Gather;               // 병력 집결 지점 (기지 앞)
    public int TargetFaction;            // 현재 주적 (0 = 없음). 가장 가까운 적을 고르되 한동안 유지한다
    public Vector2 TargetStart;          // 주적의 시작 위치 (공개 정보)
    public long TargetSinceTick, LastTargetEvalTick = -100000;
    public int ThreatFaction;            // 최근 우리 영토를 공격한 세력 (목격 기준)
    public long ThreatTick = -100000;
    public float NearestHostileDist;     // 사령부에서 가장 가까운 살아 있는 적 시작 위치까지 거리
    public readonly List<Unit> Units = new(), Army = new();
    public readonly List<Building> Sites = new();                       // 맵의 모든 점령 시설
    public readonly List<Building> Cities = new();                      // 우리 도시
    public int EnemyArmored;                                            // 보이는 적 기갑 수 (대전차 생산 판단)
    public readonly Dictionary<int, EnemyMemory> KnownEnemy = new();    // 적 건물 기억 (파괴 불가 시설 제외)
    public long LastDefenseTick = -100000, LastReturnTick = -100000;
}

/// <summary>
/// AI 공통 기반: 세력별 상태 캐시 · 적 건물 기억 · 주적 판단 · 기지 방어 (방향 변경 §0-1: 건설 · 생산 없음 — 주어진 병력만).
/// 모든 조작은 World 의 공개 명령 메서드로만 하고, 적 정보는 Fog.CanSee 로 보이는 것만 쓴다.
/// </summary>
public abstract class AiBase<TState> : IAiController where TState : AiBaseState, new()
{
    readonly Dictionary<int, TState> states = new();
    readonly List<Unit> unitBuffer = new();
    readonly List<Unit> orderBuffer = new();
    readonly List<int> staleIds = new();
    protected AiDefs D = null!;

    public abstract string Name { get; }
    protected abstract void Think(World w, TState s);

    /// <summary>AI 틱 번호 (초당 4회). 무거운 계획은 이 값의 배수에서만 수행.</summary>
    protected static long AiTick(World w) => w.Tick / 15;

    public void Tick(World w, int faction)
    {
        D = AiDefs.For(w.Defs);
        if (!states.TryGetValue(faction, out var s))
        {
            s = new TState { Faction = faction };
            states[faction] = s;
        }
        Refresh(w, s);
        Think(w, s);
    }

    /// <summary>테스트용: 세력 상태 조회.</summary>
    public TState? StateOf(int faction) => states.GetValueOrDefault(faction);

    // ---------------------------------------------------------------- 상태 갱신

    void Refresh(World w, TState s)
    {
        int f = s.Faction;
        s.Units.Clear(); s.Army.Clear();
        s.Sites.Clear(); s.Cities.Clear();
        s.Hq = null;
        s.EnemyArmored = 0;

        foreach (var u in w.UnitsOf(f))   // 세력별 목록 (World.Units 와 같은 순서)
        {
            if (u.Owner != f || !u.Alive) continue;
            s.Units.Add(u);
            if (u.Weapon != null && u.Def.CanCapture) s.Army.Add(u);
        }
        for (int g = 0; g < w.Factions.Count; g++)   // 적대 세력의 기갑 목록만 (개수만 세므로 순서 무관)
        {
            if (g == f || !AiHelpers.IsHostile(w, f, g)) continue;
            foreach (var u in w.ArmoredOf(g))
                if (u.Alive && u.Owner == g && w.Fog.CanSee(f, u)) s.EnemyArmored++;
        }
        foreach (var b in w.Buildings)
        {
            if (!b.Alive) continue;
            if (b.Def.Capturable)
            {
                s.Sites.Add(b);
                if (b.Owner == f)
                {
                    if (b.Def.City) s.Cities.Add(b);
                    if (b.Def.Headquarters && s.Hq == null) s.Hq = b;
                }
                continue;
            }
            if (b.Owner == f) continue;
            if (AiHelpers.IsRival(w, f, b.Owner) && w.Fog.CanSee(f, b))
                s.KnownEnemy[b.Id] = new EnemyMemory(b.Pos, b.Def, b.Owner, w.Tick);
        }

        if (!s.Initialized)
        {
            s.Initialized = true;
            s.Home = s.Hq?.Pos ?? AiHelpers.StartOf(w, f);
            s.Center = new Vector2(w.Map.Width / 2f, w.Map.Height / 2f);
            // 기지 방향은 맵 중앙 쪽: 2인 대칭 맵에서는 적 방향, 원형 N인 맵에서는 모든 이웃과 맞닿는 안쪽
            var dir = s.Center - s.Home;
            s.Front = dir.LengthSquared() > 1e-4f ? Vector2.Normalize(dir) : new Vector2(1, 0);
            s.Side = new Vector2(-s.Front.Y, s.Front.X);
            s.Gather = WalkablePoint(w, s.Home + s.Front * (D.Hq.Size / 2f + 6));
            UpdateTargetFaction(w, s);
        }
        else if (s.Hq != null) s.Home = s.Hq.Pos;

        // 주적 재평가: 10초마다, 또는 주적이 패배했을 때 즉시
        if (w.Tick - s.LastTargetEvalTick >= 600 || (s.TargetFaction != 0 && !AiHelpers.IsAlive(w, s.TargetFaction)))
            UpdateTargetFaction(w, s);

        if (AiTick(w) % 4 == 1) ForgetStale(w, s);
    }

    /// <summary>기억한 적 건물 위치가 지금 보이는데 건물이 없거나 더 이상 적이 아니면 잊는다.</summary>
    void ForgetStale(World w, TState s)
    {
        staleIds.Clear();
        foreach (var (id, m) in s.KnownEnemy)
        {
            if (!w.Fog.IsVisible(s.Faction, m.Pos)) continue;
            var e = w.Find(id);
            if (e == null || !AiHelpers.IsRival(w, s.Faction, e.Owner)) staleIds.Add(id);
        }
        foreach (var id in staleIds) s.KnownEnemy.Remove(id);
    }

    // ---------------------------------------------------------------- 주적 · 위협

    float[] factionScratch = Array.Empty<float>();
    int[] factionCount = Array.Empty<int>();

    /// <summary>
    /// 주적 선택: 살아 있는 적 세력 중 사령부에서 가장 가까운 쪽 (시작 위치 거리, 더 가까이 지은 적 건물을 알면 그 거리).
    /// 최근 우리를 공격한 세력은 거리를 절반으로 쳐서 우선한다. 거리가 거의 같은(10% 안) 이웃끼리는 번호 순서상 다음 세력을
    /// 고른다 — 원형 맵에서 양 이웃이 똑같이 가까울 때 모두가 같은 희생양(낮은 번호)에게 몰리지 않게.
    /// 이미 주적이 있으면 새 후보가 뚜렷이(60% 미만) 가까울 때만 바꿔 부대가 이리저리 흔들리지 않게 하고, 주적이 패배하면 다음으로 넘어간다.
    /// </summary>
    protected void UpdateTargetFaction(World w, TState s)
    {
        s.LastTargetEvalTick = w.Tick;
        int n = w.Factions.Count;
        if (factionScratch.Length < n) factionScratch = new float[n];
        Array.Fill(factionScratch, float.MaxValue, 0, n);
        foreach (var m in s.KnownEnemy.Values)
        {
            if (m.Owner <= 0 || m.Owner >= n) continue;
            float d = Vector2.Distance(m.Pos, s.Home) + 15;
            if (d < factionScratch[m.Owner]) factionScratch[m.Owner] = d;
        }

        int best = 0, bestOrder = int.MaxValue;
        float bestScore = float.MaxValue, currentScore = float.MaxValue, nearest = float.MaxValue;
        for (int g = 1; g < n; g++)
        {
            if (g == s.Faction || !AiHelpers.IsAlive(w, g) || !AiHelpers.IsRival(w, s.Faction, g)) continue;
            float startDist = Vector2.Distance(s.Home, AiHelpers.StartOf(w, g));
            nearest = MathF.Min(nearest, startDist);
            float score = MathF.Min(startDist, factionScratch[g]);
            if (g == s.ThreatFaction && w.Tick - s.ThreatTick < 7200) score *= 0.5f;
            if (g == s.TargetFaction) currentScore = score;
            int order = ((g - s.Faction) % (n - 1) + (n - 1)) % (n - 1);     // 번호 순서상 거리 (1 = 바로 다음 세력)
            bool tie = best != 0 && MathF.Abs(score - bestScore) <= MathF.Min(score, bestScore) * 0.1f;
            if (tie ? order < bestOrder : score < bestScore) (best, bestScore, bestOrder) = (g, MathF.Min(score, bestScore), order);
        }
        s.NearestHostileDist = nearest == float.MaxValue ? MathF.Max(w.Map.Width, w.Map.Height) : nearest;

        bool keep = s.TargetFaction != 0 && currentScore != float.MaxValue && bestScore >= currentScore * 0.6f;
        int chosen = keep ? s.TargetFaction : best;
        if (chosen != s.TargetFaction)
        {
            s.TargetFaction = chosen;
            s.TargetSinceTick = w.Tick;
        }
        s.TargetStart = chosen != 0 ? AiHelpers.StartOf(w, chosen) : s.Center;
    }

    /// <summary>pos 근처에 보이는 적 전투 유닛 중 가장 많은 세력을 '최근 위협'으로 기록 (방어 반응 시 호출).</summary>
    protected void RecordThreat(World w, TState s, Vector2 pos, float radius)
    {
        int n = w.Factions.Count;
        if (factionCount.Length < n) factionCount = new int[n];
        Array.Clear(factionCount, 0, n);
        int best = 0;
        foreach (var u in w.UnitsInRadius(pos, radius, unitBuffer))
        {
            if (u.Owner <= 0 || u.Owner >= n || u.Weapon == null || !AiHelpers.IsHostile(w, s.Faction, u.Owner) || !w.Fog.CanSee(s.Faction, u)) continue;
            if (++factionCount[u.Owner] > (best == 0 ? 0 : factionCount[best])) best = u.Owner;
        }
        if (best == 0) return;
        s.ThreatFaction = best;
        s.ThreatTick = w.Tick;
    }

    /// <summary>pos 에서 가장 가까운 살아 있는 적 시작 위치 쪽 단위 벡터 (없으면 맵 중앙 쪽). 포탑 · 경계 방향용.</summary>
    protected Vector2 ThreatDirection(World w, TState s, Vector2 pos)
    {
        Vector2 toward = s.Center;
        float bestD = float.MaxValue;
        for (int g = 1; g < w.Factions.Count; g++)
        {
            if (g == s.Faction || !AiHelpers.IsAlive(w, g) || !AiHelpers.IsRival(w, s.Faction, g)) continue;
            var start = AiHelpers.StartOf(w, g);
            float d = Vector2.DistanceSquared(start, pos);
            if (d < bestD) (toward, bestD) = (start, d);
        }
        var dir = toward - pos;
        return dir.LengthSquared() > 1e-4f ? Vector2.Normalize(dir) : s.Front;
    }

    /// <summary>
    /// p 가 살아 있는 적 세력의 영토인지: 그 세력 시작 위치에서 16타일 안이거나, 우리 사령부보다 그쪽 시작 위치에 두 배 이상 가까움.
    /// 이웃과의 경계 거점(양쪽에서 같은 거리)은 영토가 아니다 → 분대의 점령 대상이 된다.
    /// </summary>
    protected bool InHostileTerritory(World w, TState s, Vector2 p)
    {
        float homeD2 = Vector2.DistanceSquared(p, s.Home);
        for (int g = 1; g < w.Factions.Count; g++)
        {
            if (g == s.Faction || !AiHelpers.IsAlive(w, g) || !AiHelpers.IsRival(w, s.Faction, g)) continue;
            float d2 = Vector2.DistanceSquared(p, AiHelpers.StartOf(w, g));
            if (d2 < 256 || d2 * 4 < homeD2) return true;
        }
        return false;
    }

    protected static Vector2 WalkablePoint(World w, Vector2 p)
    {
        var (tx, ty) = GameMap.TileOf(p);
        var (nx, ny) = w.Map.NearestWalkable(tx, ty);
        return new Vector2(nx + 0.5f, ny + 0.5f);
    }

    // ---------------------------------------------------------------- 집계

    protected int CountUnits(TState s, UnitDef def)
    {
        int n = 0;
        foreach (var u in s.Units) if (u.Def == def) n++;
        return n;
    }

    /// <summary>pos 반경 radius 안에 보이는 적 전투 유닛 수.</summary>
    protected int VisibleEnemyFighters(World w, int faction, Vector2 pos, float radius)
    {
        int n = 0;
        foreach (var u in w.UnitsInRadius(pos, radius, unitBuffer))
            if (AiHelpers.IsHostile(w, faction, u.Owner) && u.Weapon != null && w.Fog.CanSee(faction, u)) n++;
        return n;
    }

    // ---------------------------------------------------------------- 방어

    /// <summary>
    /// 최근(반응 지연 후 3초 안) 기지 근처가 공격받았으면 가까운 한가한 병력을 그 지점으로 어택땅.
    /// 2초에 한 번만 재발령. 반응했으면 true.
    /// </summary>
    protected bool Defend(World w, TState s, List<Unit> candidates, float baseRadius, float pullRadius, float reactionDelay)
    {
        if (!w.LastAttacked.TryGetValue(s.Faction, out var la)) return false;
        long delay = (long)(reactionDelay * 60);
        long age = w.Tick - la.Tick;
        if (age < delay || age > delay + 180 || w.Tick - s.LastDefenseTick < 120) return false;
        if (!NearOwnTerritory(s, la.Pos, baseRadius)) return false;
        RecordThreat(w, s, la.Pos, 12);
        orderBuffer.Clear();
        foreach (var u in candidates)
            if ((u.Order == UnitOrder.Idle || u.Order == UnitOrder.Move) && Vector2.DistanceSquared(u.Pos, la.Pos) <= pullRadius * pullRadius)
                orderBuffer.Add(u);
        s.LastDefenseTick = w.Tick;
        if (orderBuffer.Count == 0) return false;
        w.AttackMove(s.Faction, orderBuffer, la.Pos);
        return true;
    }

    /// <summary>기지(사령부 주변) 또는 자기 자원 시설 근처인지.</summary>
    protected static bool NearOwnTerritory(TState s, Vector2 p, float baseRadius)
    {
        if (Vector2.DistanceSquared(p, s.Home) <= baseRadius * baseRadius) return true;
        foreach (var site in s.Sites)
            if (site.Owner == s.Faction && Vector2.DistanceSquared(p, site.Pos) <= 100) return true;
        return false;
    }

    /// <summary>집결지에서 멀어진 한가한 병력을 5초마다 한 번 집결지로 되돌린다.</summary>
    protected void ReturnToGather(World w, TState s, List<Unit> units, float radius)
    {
        if (w.Tick - s.LastReturnTick < 300 || w.Tick - s.LastDefenseTick < 600) return;
        s.LastReturnTick = w.Tick;
        orderBuffer.Clear();
        foreach (var u in units)
            if (u.Order == UnitOrder.Idle && Vector2.DistanceSquared(u.Pos, s.Gather) > radius * radius) orderBuffer.Add(u);
        if (orderBuffer.Count > 0) w.Move(s.Faction, orderBuffer, s.Gather);
    }

    /// <summary>
    /// 추격 등으로 center 에서 radius 밖으로 나간 유닛(이미 복귀 중인 것 제외)을 to 로 불러들인다.
    /// 유닛의 자동 추격(반격 포함)은 표적까지의 거리만 보므로, 후퇴하는 적을 끝없이 쫓아가는 것을 막는다.
    /// </summary>
    protected void Leash(World w, TState s, List<Unit> units, Vector2 center, float radius, Vector2 to)
    {
        orderBuffer.Clear();
        foreach (var u in units)
            if (u.Order != UnitOrder.Move
                && Vector2.DistanceSquared(u.Pos, center) > radius * radius) orderBuffer.Add(u);
        if (orderBuffer.Count > 0) w.Move(s.Faction, orderBuffer, to);
    }

    protected List<Unit> OrderBuffer()
    {
        orderBuffer.Clear();
        return orderBuffer;
    }
}
