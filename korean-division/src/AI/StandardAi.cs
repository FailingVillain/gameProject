using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>표준 AI 난이도 설정. 값을 바꿔 변형 AI 를 등록할 수 있다.</summary>
public sealed record StandardAiSettings(
    string Name = "standard",
    int WaveSize = 12,               // 첫 공격 부대 규모
    int WaveGrowth = 6,              // 공격마다 커지는 규모
    int MaxWave = 30,
    float RetreatRatio = 0.4f,       // 부대가 출발 인원의 이 비율 아래로 줄면 후퇴
    int SquadSize = 3,               // 점령 분대 인원
    int GuardPerPoint = 2,           // 점령 후 남겨 두는 경비 인원
    int ScoutPerInfantry = 4,        // 보병 n 당 정찰병 1
    float ReactionDelay = 0.5f,      // 피격 반응 지연 (초)
    bool UseScout = true);

public enum AiRole { Pool, Squad, Scout }
public enum ArmyPhase { Gather, Attack }

public sealed class AiUnitInfo
{
    public AiRole Role;
    public long LastOrderTick;
}

/// <summary>자원 시설 하나를 점령·경비하는 소규모 분대.</summary>
public sealed class AiSquad
{
    public int SiteId;
    public readonly List<int> UnitIds = new();
    public long LastOrderTick;
}

public sealed class StandardAiState : AiBaseState
{
    public readonly Dictionary<int, AiUnitInfo> Info = new();
    public readonly List<AiSquad> Squads = new();
    public readonly Dictionary<int, long> SiteRetryTick = new();   // 분대가 전멸한 시설의 재시도 시각
    public readonly Dictionary<int, int> SiteWipes = new();
    public readonly List<Unit> Pool = new();                       // 이번 틱의 주력 병력 (분대·정찰 제외)
    public readonly List<Vector2> ScoutRoute = new(), SearchRoute = new();
    public int SearchRouteFaction;                                 // SearchRoute 를 만든 기준 주적 (바뀌면 다시 만든다)
    public ArmyPhase Phase;
    public int WaveSize, LaunchSize, SearchIndex, ScoutWaypoint, ScoutsSent, ScoutUnitId;
    public long LastTargetTick, LastSquadTick, LastScoutTick = -100000;
    public Vector2 AttackTarget;
    public int Launches;                                            // 공격 출발 횟수 (테스트/통계)
}

/// <summary>
/// 표준 대전 AI (방향 변경 §0-1: 생산 · 건설 없음 — 주어진 병력으로만 싸운다). 병력을
/// 기지 앞에 집결시키고, 소규모 분대로 가까운 중립 자원 시설부터 점령·경비한다 (적 영토 안의 시설은 제외). 집결 병력이
/// 부대 규모에 이르면 주적(가장 가까운 적 세력, AiBase.UpdateTargetFaction)의 알려진 건물 — 없으면 그 시작 위치 — 로 어택땅,
/// 많이 줄면 후퇴해 재집결. 기지가 공격받으면 근처 병력이 대응한다. N인전에서도 같은 논리로 이웃부터 차례로 상대한다.
/// </summary>
public sealed partial class StandardAi : AiBase<StandardAiState>
{
    public readonly StandardAiSettings Settings;
    readonly List<int> staleIds = new();
    readonly List<Unit> pick = new();

    public StandardAi() : this(new StandardAiSettings()) { }
    public StandardAi(StandardAiSettings settings) => Settings = settings;

    public override string Name => Settings.Name;

    protected override void Think(World w, StandardAiState s)
    {
        RefreshRoles(w, s);
        Defend(w, s, s.Army, baseRadius: 24, pullRadius: 35, reactionDelay: Settings.ReactionDelay);
        ManageSquads(w, s);
        Scouting(w, s);
        MainArmy(w, s);
    }

    // ---------------------------------------------------------------- 역할

    void RefreshRoles(World w, StandardAiState s)
    {
        s.Pool.Clear();
        foreach (var u in s.Army)
        {
            if (!s.Info.TryGetValue(u.Id, out var info))
            {
                info = new AiUnitInfo();
                if (Settings.UseScout && u.Def == D.Scout && s.ScoutUnitId == 0 && s.ScoutsSent < 3 && w.Tick - s.LastScoutTick > 10800)
                {
                    info.Role = AiRole.Scout;
                    s.ScoutUnitId = u.Id;
                    s.ScoutsSent++;
                    s.ScoutWaypoint = 0;
                    s.LastScoutTick = w.Tick;
                }
                s.Info[u.Id] = info;
            }
            if (info.Role == AiRole.Pool) s.Pool.Add(u);
        }
        if (AiTick(w) % 16 == 3)
        {
            staleIds.Clear();
            foreach (var id in s.Info.Keys)
                if (w.Find(id) == null) staleIds.Add(id);
            foreach (var id in staleIds) s.Info.Remove(id);
        }
    }

    // ---------------------------------------------------------------- 점령 분대

    void ManageSquads(World w, StandardAiState s)
    {
        for (int i = s.Squads.Count - 1; i >= 0; i--)
        {
            var sq = s.Squads[i];
            for (int k = sq.UnitIds.Count - 1; k >= 0; k--)
                if (w.Find(sq.UnitIds[k]) is not Unit u || !u.Alive) sq.UnitIds.RemoveAt(k);
            var site = w.Find(sq.SiteId) as Building;
            if (site == null || sq.UnitIds.Count == 0)
            {
                if (site != null && site.Owner != s.Faction)
                {
                    // 전멸할수록 더 오래(1분 × 횟수, 최대 4분) 미뤄 다른 시설로 눈을 돌린다
                    int wipes = s.SiteWipes.GetValueOrDefault(site.Id) + 1;
                    s.SiteWipes[site.Id] = wipes;
                    s.SiteRetryTick[site.Id] = w.Tick + 3600 * Math.Min(wipes, 4);
                }
                s.Squads.RemoveAt(i);
                continue;
            }
            bool owned = site.Owner == s.Faction;
            if (owned && sq.UnitIds.Count > Settings.GuardPerPoint)
            {
                var release = OrderBuffer();
                while (sq.UnitIds.Count > Settings.GuardPerPoint)
                {
                    int id = sq.UnitIds[^1];
                    sq.UnitIds.RemoveAt(sq.UnitIds.Count - 1);
                    if (w.Find(id) is Unit u) { release.Add(u); s.Info[id].Role = AiRole.Pool; }
                }
                if (release.Count > 0) w.Move(s.Faction, release, s.Gather);
            }
            if (w.Tick - sq.LastOrderTick >= 300)
            {
                var back = OrderBuffer();
                foreach (int id in sq.UnitIds)
                    if (w.Find(id) is Unit u && u.Order == UnitOrder.Idle && Vector2.DistanceSquared(u.Pos, site.Pos) > 36) back.Add(u);
                if (back.Count > 0) { w.AttackMove(s.Faction, back, site.Pos); sq.LastOrderTick = w.Tick; }
            }
        }

        // 새 분대 편성: 3초마다, 점령 진행 중인 분대가 없을 때
        if (w.Tick - s.LastSquadTick < 180) return;
        s.LastSquadTick = w.Tick;
        foreach (var sq in s.Squads)
            if (w.Find(sq.SiteId) is Building b && b.Owner != s.Faction) return;

        pick.Clear();
        foreach (var u in s.Pool)
            if (u.Order == UnitOrder.Idle && Vector2.DistanceSquared(u.Pos, s.Gather) < 144) pick.Add(u);
        if (pick.Count < Settings.SquadSize) return;

        // 후보 순위: 사령부에서 가까운 순. 살아 있는 적이 보유한 시설은 20타일 멀리 있는 셈 쳐서 중립 시설을 먼저 잡고,
        // 보이는 적 병력이 분대 규모 이상 지키는 곳 · 적 포탑 근처 · 적 영토 안은 건너뛴다 (경계 거점처럼 서로 같은 거리인 곳은 괜찮다).
        Building? target = null;
        int size = 0;
        float bestD = float.MaxValue;
        foreach (var site in s.Sites)
        {
            if (site.Owner == s.Faction) continue;
            if (s.SiteRetryTick.TryGetValue(site.Id, out long retry) && w.Tick < retry) continue;
            float d = Vector2.Distance(site.Pos, s.Home) + D.SitePenalty(site.Def);   // 대도시 > 도시 > 그 밖
            if (site.Owner != 0 && AiHelpers.IsAlive(w, site.Owner) && AiHelpers.IsRival(w, s.Faction, site.Owner)) d += 20;
            if (site.Owner != 0 && !AiHelpers.IsRival(w, s.Faction, site.Owner)) continue;   // 동맹의 시설은 노리지 않음
            if (d >= bestD) continue;
            if (InHostileTerritory(w, s, site.Pos)) continue;     // 적 영토(본진 · 그쪽에 훨씬 가까운 곳)의 시설은 주력이 맡는다
            bool targeted = false;
            foreach (var sq in s.Squads) if (sq.SiteId == site.Id) { targeted = true; break; }
            if (targeted) continue;
            int need = Settings.SquadSize + Math.Min(s.SiteWipes.GetValueOrDefault(site.Id), 2);
            if (VisibleEnemyFighters(w, s.Faction, site.Pos, 9) >= need) continue;
            (target, bestD, size) = (site, d, need);
        }
        if (target == null || pick.Count < size) return;

        pick.Sort((a, b) => Vector2.DistanceSquared(a.Pos, target.Pos).CompareTo(Vector2.DistanceSquared(b.Pos, target.Pos)));
        var squad = new AiSquad { SiteId = target.Id, LastOrderTick = w.Tick };
        var members = OrderBuffer();
        for (int i = 0; i < size; i++)
        {
            squad.UnitIds.Add(pick[i].Id);
            s.Info[pick[i].Id].Role = AiRole.Squad;
            members.Add(pick[i]);
        }
        s.Squads.Add(squad);
        w.AttackMove(s.Faction, members, target.Pos);
    }

    // ---------------------------------------------------------------- 정찰

    void Scouting(World w, StandardAiState s)
    {
        if (s.ScoutUnitId == 0) return;
        if (w.Find(s.ScoutUnitId) is not Unit u || !u.Alive) { s.ScoutUnitId = 0; return; }
        var one = OrderBuffer();
        one.Add(u);
        if (u.Hp < u.MaxHp * 0.5f)
        {
            s.Info[u.Id].Role = AiRole.Pool;
            s.ScoutUnitId = 0;
            w.Move(s.Faction, one, s.Gather);
            return;
        }
        if (u.Order != UnitOrder.Idle) return;
        if (s.ScoutWaypoint == 0 || s.ScoutRoute.Count == 0) BuildScoutRoute(w, s);
        if (s.ScoutRoute.Count == 0) return;
        w.Move(s.Faction, one, s.ScoutRoute[s.ScoutWaypoint % s.ScoutRoute.Count]);
        s.ScoutWaypoint++;
    }

    /// <summary>
    /// 정찰 경로 (새 정찰병마다 다시 만든다): 살아 있는 적 이웃의 시작 위치 가까운 순 (최대 3, 본진 앞 9타일 지점) →
    /// 그 범위(가장 먼 이웃까지 거리 × 1.1) 안의 자원 시설을 주적 쪽부터. 맵 반대편까지 헤매지 않는다.
    /// </summary>
    void BuildScoutRoute(World w, StandardAiState s)
    {
        s.ScoutRoute.Clear();
        var starts = new List<(float D, Vector2 P)>();
        for (int g = 1; g < w.Factions.Count; g++)
            if (g != s.Faction && AiHelpers.IsAlive(w, g) && AiHelpers.IsRival(w, s.Faction, g))
            {
                var p = AiHelpers.StartOf(w, g);
                starts.Add((Vector2.Distance(p, s.Home), p));
            }
        if (starts.Count == 0) return;
        starts.Sort((a, b) => a.D.CompareTo(b.D));
        int take = Math.Min(3, starts.Count);
        float range = starts[take - 1].D * 1.1f;
        for (int i = 0; i < take; i++)
        {
            var dir = s.Center - starts[i].P;
            s.ScoutRoute.Add(starts[i].P + (dir.LengthSquared() > 1e-4f ? Vector2.Normalize(dir) : -s.Front) * 9);
        }
        Vector2 primary = s.TargetFaction != 0 ? s.TargetStart : starts[0].P;
        var near = new List<(float D, int I, Vector2 P)>();
        foreach (var b in s.Sites)
            if (Vector2.Distance(b.Pos, s.Home) <= range) near.Add((Vector2.DistanceSquared(b.Pos, primary), near.Count, b.Pos));
        near.Sort(static (a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.I.CompareTo(b.I));   // OrderBy 와 같은 안정 정렬
        foreach (var e in near) s.ScoutRoute.Add(e.P);
    }

    // ---------------------------------------------------------------- 주력 부대

    void MainArmy(World w, StandardAiState s)
    {
        if (s.WaveSize == 0) s.WaveSize = Settings.WaveSize;
        var pool = s.Pool;
        if (s.Phase == ArmyPhase.Gather)
        {
            int assembled = 0;
            foreach (var u in pool)
                if (Vector2.DistanceSquared(u.Pos, s.Gather) <= 144) assembled++;
            // 증원이 없다 (방향 변경 §0-1) — 부대 규모는 지금 가진 병력의 80% 를 넘지 않는다 (후퇴 뒤 남은 병력으로 다시 출발)
            int need = Math.Min(s.WaveSize, Math.Max(2, (int)(pool.Count * 0.8f)));
            if (pool.Count > 0 && assembled >= need)
            {
                s.AttackTarget = PickTarget(w, s, Centroid(pool));
                w.AttackMove(s.Faction, pool, s.AttackTarget);
                s.Phase = ArmyPhase.Attack;
                s.LaunchSize = pool.Count;
                s.Launches++;
                s.LastTargetTick = w.Tick;
                s.WaveSize = Math.Min(s.WaveSize + Settings.WaveGrowth, Settings.MaxWave);
            }
            else
            {
                ReturnToGather(w, s, pool, 7);
                Leash(w, s, pool, s.Gather, 28, s.Gather);
            }
            return;
        }

        int threshold = Math.Max(2, (int)(s.LaunchSize * Settings.RetreatRatio));
        if (pool.Count < threshold)
        {
            w.Retreat(s.Faction, pool, s.Gather);   // 후퇴 = 속보 (pursuit)
            s.Phase = ArmyPhase.Gather;
            return;
        }
        if (w.Tick - s.LastTargetTick < 300) return;
        s.LastTargetTick = w.Tick;

        // 전선 중심 기준으로 표적 재선정. 집결지의 신병은 다음 부대를 위해 남겨 두고(각개 투입 방지),
        // 전선에서 한가해진 병력만 재발령한다.
        Vector2 front = Vector2.Zero;
        int n = 0;
        foreach (var u in pool)
            if (Vector2.DistanceSquared(u.Pos, s.Gather) > 144) { front += u.Pos; n++; }
        if (n < threshold)
        {
            w.Move(s.Faction, pool, s.Gather);
            s.Phase = ArmyPhase.Gather;
            return;
        }
        s.AttackTarget = PickTarget(w, s, front / n);
        var idle = OrderBuffer();
        foreach (var u in pool)
            if (u.Order == UnitOrder.Idle && Vector2.DistanceSquared(u.Pos, s.Gather) > 144) idle.Add(u);
        if (idle.Count > 0) w.AttackMove(s.Faction, idle, s.AttackTarget);
    }

    /// <summary>
    /// 공격 지점: from 에서 가장 가까운 기억 속 적 건물 (주적 것, 또는 어느 적이든 우리 기지 45타일 안에 지은 것) →
    /// 없으면 주적 시작 위치 → 거기도 비었으면 주적 쪽 자원 시설 순회 (주적까지 거리의 75% 안, 맵 반대편은 가지 않음).
    /// 주적이 없으면(모두 패배) 집결지.
    /// </summary>
    Vector2 PickTarget(World w, StandardAiState s, Vector2 from)
    {
        if (s.TargetFaction == 0) return s.Gather;
        Vector2? best = null;
        float bestScore = float.MaxValue;
        foreach (var m in s.KnownEnemy.Values)
        {
            if (m.Owner != s.TargetFaction && Vector2.DistanceSquared(m.Pos, s.Home) > 45 * 45) continue;
            float score = Vector2.Distance(m.Pos, from) + (m.Def.Headquarters ? -3 : 0);
            // 점수가 같으면 위치(x, y)가 작은 쪽 — 딕셔너리 열거 순서(제거 · 추가 이력에 따라 달라짐)에 의존하지 않는다
            if (score < bestScore || (score == bestScore && best is Vector2 bp && (m.Pos.X < bp.X || (m.Pos.X == bp.X && m.Pos.Y < bp.Y)))) (best, bestScore) = (m.Pos, score);
        }
        if (best is Vector2 p) return p;
        if (Vector2.DistanceSquared(from, s.TargetStart) > 144) return s.TargetStart;
        if (s.SearchRoute.Count == 0 || s.SearchRouteFaction != s.TargetFaction)
        {
            s.SearchRoute.Clear();
            s.SearchRouteFaction = s.TargetFaction;
            s.SearchIndex = 0;
            float range = Vector2.Distance(s.Home, s.TargetStart) * 0.75f;
            var near = new List<(float D, int I, Vector2 P)>();
            foreach (var b in s.Sites)
                if (b.Owner != s.Faction && Vector2.Distance(b.Pos, s.TargetStart) <= range) near.Add((Vector2.DistanceSquared(b.Pos, s.TargetStart), near.Count, b.Pos));
            near.Sort(static (a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.I.CompareTo(b.I));
            foreach (var e in near) s.SearchRoute.Add(e.P);
            if (s.SearchRoute.Count == 0) s.SearchRoute.Add(s.TargetStart);
        }
        return s.SearchRoute[s.SearchIndex++ % s.SearchRoute.Count];
    }

    static Vector2 Centroid(List<Unit> units)
    {
        if (units.Count == 0) return Vector2.Zero;
        Vector2 c = Vector2.Zero;
        foreach (var u in units) c += u.Pos;
        return c / units.Count;
    }
}
