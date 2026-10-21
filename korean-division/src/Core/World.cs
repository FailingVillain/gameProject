using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 게임 시뮬레이션 (렌더링 비의존). 고정 틱(Config.SimDt)으로 Step() 이 호출된다.
/// 파일 분할: World.cs(엔티티·이동·공간 해시) · WorldCommands.cs(명령 API) ·
/// WorldCombat.cs(전투) · WorldEconomy.cs(점령 · 승패) · Supply.cs(탄약 회복 · 보급 마차) · Cover.cs(엄폐 · 참호).
/// 플레이어와 AI 는 모두 WorldCommands 의 공개 메서드로만 게임을 조작한다.
/// </summary>
public sealed partial class World
{
    public readonly GameMap Map;
    public readonly Defs Defs;
    public readonly List<Faction> Factions = new();      // index = 세력 Id (0 = 중립)
    public readonly List<Unit> Units = new();
    public readonly List<Building> Buildings = new();
    public readonly FogOfWar Fog;
    public readonly List<(int Faction, IAiController Ai)> Controllers = new();
    public long Tick { get; private set; }
    public float Time => Tick * Config.SimDt;
    int nextId = 1;
    int lastMapVersion;

    readonly Dictionary<int, Entity> byId = new();
    // 공간 해시: 칸별 유닛을 계수 정렬로 모은다 (칸 안은 유닛 번호 내림차순). cellStart[c]..cellStart[c]+cellCount[c] 이 칸 c 의 sortedIdx 구간.
    // N16: 비용이 지도 면적이 아니라 유닛 수 · 점유 칸 수에 비례 — 지난 재구성에서 점유한 칸 (occCells) 만 되돌려 지우고, 점유 칸만 정렬해 시작 위치를 매긴다.
    // 빈 칸의 cellStart 는 낡은 값일 수 있다 (cellCount = 0 이라 구간은 항상 비어 있음). sortedIdx 순서 = 칸 오름차순 · 칸 안 번호 내림차순 (예전과 같음).
    readonly int[] cellStart, cellCount, cellCursor;
    int[] occCells = new int[64];       // 지난 재구성에서 점유된 칸 (재구성 끝에는 오름차순 정렬)
    int occCount;
    readonly uint[] coarseMask;         // 8×8 칸 묶음의 소유 세력 비트 (적 없는 넓은 지역을 한 번에 건너뛴다)
    readonly int coarseW;
    readonly uint[] cellMask;           // 칸에 있는 유닛 소유 세력 비트 (표적 탐색에서 적 없는 칸을 건너뛴다)
    uint unitMask, bldgMask;            // 전체 유닛 · 건물의 소유 세력 비트
    int[] sortedIdx = new int[64], unitCell = new int[64];
    // 겹침 해소용 칸 순서 배열 (SoA — 캐시 친화)
    float[] sepX = new float[64], sepY = new float[64], sepR = new float[64], sepR0 = new float[64], sepDX = new float[64], sepDY = new float[64];
    byte[] sepN = new byte[64];         // 이번 틱 밀림을 준 접촉 수 (Jacobi 평균)
    byte[] sepFlag = new byte[64];      // 1 = 못 밀림, 2 = 그룹 이동 중, 4 = 위치 변경됨, 8 = 이번 틱 실제로 움직임 (추격 포함)
    byte[] sepOwner = new byte[64];     // 소유 세력 (적대 쌍 판정)
    uint[] sepHostile = new uint[32];   // 세력별 적대 세력 비트 (Separate 시작 때 갱신)
    MoveGroup?[] sepGrp = new MoveGroup?[64], sepLast = new MoveGroup?[64];
    /// <summary>겹침 해소로 한 틱에 유닛이 밀리는 최대 거리(칸). 걷는 속도(≈0.05칸/틱)의 몇 배 — 겹쳐 생성된 무리는 몇 틱에 걸쳐 풀린다.</summary>
    public const float MaxSeparationStep = 0.25f;
    bool hashDirty;                     // 유닛 추가/제거 후 다음 조회 전에 재구성

    public World(GameMap map, Defs defs, int factionCount = 2)
    {
        Map = map;
        Defs = defs;
        cellStart = new int[map.Width * map.Height];
        cellCount = new int[map.Width * map.Height];
        cellCursor = new int[map.Width * map.Height];
        cellMask = new uint[map.Width * map.Height];
        coarseW = (map.Width + 7) >> 3;
        coarseMask = new uint[coarseW * ((map.Height + 7) >> 3)];
        Factions.Add(new Faction(0, "Neutral"));
        for (int i = 1; i <= factionCount; i++)
            Factions.Add(new Faction(i, string.Format(KoreanDivision.UI.S.FactionLabel, i)));
        Fog = new FogOfWar(this);
        lastMapVersion = map.Version;
        InitProps();   // F6 소품 층 (Props.cs)
    }

    /// <summary>
    /// 표준 시작 배치 (방향 변경 §0-1): 세력(= 맵의 시작 위치 수)마다 대도시(소유 · 점령 완료), 점령 시설은 맵 정의대로.
    /// 병력: 맵 파일에 유닛 배치(map.UnitSpawns)가 있으면 그대로, 없으면 balance.json start.force 기본 병력을 대도시 앞에 (중대 · 대대 편성 포함).
    /// </summary>
    public static World CreateStandard(GameMap map, Defs defs)
    {
        int players = map.Players;
        var world = new World(map, defs, players);
        var fdefs = MapFile.FactionsOf(map);   // 정적 팀 (지도 파일 factions[].team) — 시작 전에 한 번만
        for (int i = 0; i < fdefs.Count && i + 1 <= players; i++) world.AssignTeam(i + 1, fdefs[i].Team);
        var hq = defs.Headquarters;
        var hqs = new Building?[players + 1];
        for (int f = 1; f <= players; f++)
        {
            var (sx, sy) = map.StartTiles[f - 1];
            int tx = sx - hq.Size / 2, ty = sy - hq.Size / 2;
            if (map.Mirrored && players == 2 && f == 2)   // 2인 생성 맵: 세력 1 배치의 정확한 회전 대칭
            {
                var (s1x, s1y) = map.StartTiles[0];
                (tx, ty) = (map.Width - (s1x - hq.Size / 2) - hq.Size, map.Height - (s1y - hq.Size / 2) - hq.Size);
            }
            var b = world.PlaceBuilding(hq, f, tx, ty);
            if (b != null) { b.CaptureFaction = f; b.CaptureProgress = 1; }
            hqs[f] = b;
        }
        foreach (var (x, y, owner, kind) in map.ResourceSites)
        {
            var r = world.PlaceBuilding(defs.Buildings[kind], owner, x, y);
            if (r != null && owner != 0) { r.CaptureFaction = owner; r.CaptureProgress = 1; }
        }
        if (map.UnitSpawns.Count > 0)
        {
            foreach (var sp in map.UnitSpawns)
                if (sp.Faction >= 1 && sp.Faction <= players && defs.Units.TryGetValue(sp.Unit, out var ud)) world.SpawnUnit(ud, sp.Faction, sp.Pos);
        }
        else
        {
            var startJobs = new List<StartJob>();
            for (int f = 1; f <= players; f++)
                if (hqs[f] is { } b) world.SpawnStartForce(f, b.Pos, b.Size, new Vector2(map.Width / 2f, map.Height / 2f), startJobs);
            world.FinishStartForce(startJobs);
        }
        if (MapFile.WeatherOf(map) is { } ws)   // 지도 파일의 날씨 · 시간대 · 일정 (Weather.cs) — 생성 맵은 맑은 낮
        {
            world.Weather.Kind = world.Weather.StartKind = ws.Kind;
            world.Weather.Time = world.Weather.StartTime = ws.Time;
            world.Weather.TimeStepTicks = ws.TimeStepTicks;
            world.Weather.Schedule.AddRange(ws.Schedule.OrderBy(e => e.Tick));
        }
        world.Fog.Update();
        world.Nav.Sync();   // 계층 길찾기 그래프를 미리 만든다 (첫 이동 명령에서 끊기지 않게)
        return world;
    }

    /// <summary>
    /// 기본 병력 (balance.json start.force) 을 base 앞(toward 쪽)에 줄지어 생성. 묶음마다 한 줄 이상 (한 줄 최대 12기), company 면 중대로 편성.
    /// 게임 시작 전 설정 단계 전용 (명령이 아니다 — CreateStandard 와 같은 시작 상태).
    /// </summary>
    internal void SpawnStartForce(int f, Vector2 basePos, int baseSize, Vector2 toward, List<StartJob> jobs)
    {
        Vector2 dir = toward - basePos;
        dir = dir.LengthSquared() > 1e-4f ? Vector2.Normalize(dir) : new Vector2(0, 1);
        Vector2 side = new(-dir.Y, dir.X);
        float row = baseSize * 0.5f + 1.5f;
        const int PerRow = 12;
        // 정찰병은 모든 묶음의 일반 유닛 뒤에 생성한다 (유닛 Id 가 정찰병 도입 전과 같게 — Id 로 갈리는 해시 · 테스트 보존, TestDefs.Strip 이 되감는다)
        foreach (var g in Defs.Start.Force)
        {
            var spawned = new List<Unit>();
            var list = new List<UnitDef>();
            foreach (var (key, count) in g.Units.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (Defs.Units.TryGetValue(key, out var ud) && !ud.Scout) for (int i = 0; i < count; i++) list.Add(ud);
            for (int i = 0; i < list.Count; i += PerRow)
            {
                int n = Math.Min(PerRow, list.Count - i);
                float spacing = list[i].Radius > 0.45f ? 1.6f : 0.9f;
                for (int k = 0; k < n; k++)
                    spawned.Add(SpawnUnit(list[i + k], f, basePos + dir * row + side * ((k - (n - 1) / 2f) * spacing)));
                row += list[i].Radius > 0.45f ? 1.8f : 1.0f;
            }
            row += 0.6f;
            jobs.Add(new StartJob(f, spawned, g, basePos, dir, side, row - 1.1f));
        }
    }

    /// <summary>정찰병은 모든 세력의 일반 유닛 뒤에 생성한다 (유닛 Id 가 정찰병 도입 전과 같게 — Id 로 갈리는 해시 · 테스트 보존, TestDefs.Strip 이 되감는다). 중대 편성 순서는 그대로.</summary>
    internal void FinishStartForce(List<StartJob> jobs)
    {
        foreach (var j in jobs)
        {
            int ns = 0;
            foreach (var (key, count) in j.G.Units.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (Defs.Units.TryGetValue(key, out var sd) && sd.Scout)
                    for (int i = 0; i < count; i++) j.Spawned.Add(SpawnUnit(sd, j.F, j.Base + j.Dir * j.Row + j.Side * ((ns++ - 0.5f) * 1.2f + 7f)));
            if (j.G.Company && j.Spawned.Count > 0) ExecFormCompany(j.F, j.Spawned, j.G.Battalion, spawn: SpawnRanks.Auto);
        }
    }

    internal sealed record StartJob(int F, List<Unit> Spawned, StartGroupDef G, Vector2 Base, Vector2 Dir, Vector2 Side, float Row);
    public Faction Faction(int id) => Factions[id];
    public Entity? Find(int id) => byId.TryGetValue(id, out var e) && e.Alive ? e : null;
    /// <summary>[호환] 적대 여부 — IsHostile 과 같음 (정적 팀).</summary>
    public bool AreEnemies(int a, int b) => IsHostile(a, b);

    // ---------------------------------------------------------------- 생성

    /// <summary>사병 기본 시작 계급 (1 = 이등병). 명시 0 (훈련병) 은 미션 rank:0 / GrantKills(0).</summary>
    public const int DefaultEnlistedRank = 1;
    public Unit SpawnUnit(UnitDef def, int owner, Vector2 pos)
    {
        var (tx, ty) = GameMap.TileOf(pos);
        if (!Map.IsWalkable(tx, ty))
        {
            (tx, ty) = Map.NearestWalkable(tx, ty);
            pos = new Vector2(tx + 0.5f, ty + 0.5f);
        }
        var u = new Unit(nextId++, def, owner, pos);
        if (def.Weapon == Defs.RifleSlot && Defs.Current.FactionRifles.Count > 0 && Defs.Current.WeaponKeyFor(def, FactionEn(owner), u.Id, Map.Seed) is { } wk && wk != def.Weapon)
            u.Weapon = Defs.Current.Weapon(wk);   // [N54] 세력별 소총 분배 (해시 결정적). 저장은 무기 키 그대로 (Unit.Weapon)
        if (def.IsInfantry && !def.IsOfficer)   // [2026-10-20] 사병 기본 계급 1 (이등병) — 세력이 1 을 안 쓰면 첫 사용 계급
        {
            int dr = RankNames.Snap(FactionEn(owner), DefaultEnlistedRank);
            if (dr > 0) u.GrantKills(Defs.Current.CombatRules.RankKills[Math.Min(dr, Defs.Current.CombatRules.RankKills.Length) - 1]);
        }
        Units.Add(u); IndexSpawn(u);
        byId[u.Id] = u;
        hashDirty = true;
        return u;
    }

    /// <summary>도시 · 점령지를 즉시 배치 (지도 준비 단계 · 테스트 전용, 검증 포함). 실패 시 null.</summary>
    public Building? PlaceBuilding(BuildingDef def, int owner, int tx, int ty)
    {
        if (!Map.CanPlace(tx, ty, def.Size)) return null;
        var b = new Building(nextId++, def, owner, tx, ty);
        Buildings.Add(b);
        byId[b.Id] = b;
        Map.SetOccupant(tx, ty, def.Size, b.Id, def.City);   // 도시 · 소도시는 점유(예약)만 하고 통행은 막지 않는다
        bldgMask |= 1u << (owner & 31);
        return b;
    }

    // ---------------------------------------------------------------- 시뮬레이션

    /// <summary>[측정 훅] Step 구간별 누적 Stopwatch 틱 (StressTest 가 읽는다). Profile=false 면 비용 없음.
    /// 0 AI+명령+경로 · 1 hash1 · 2 UpdateUnit · 3 hash2 · 4 Separate · 5 Terrain · 6 점령/보급/외교 · 7 RemoveDead · 8 안개 · 9 승리/정리 · 10 체크섬 ·
    /// 11 FindTarget · 12 경로 계산 · 13 지역 흐름장 계산 · 14 경로 수 · 15 흐름장 수.</summary>
    public static bool Profile;
    public static readonly long[] Prof = new long[16];
    static long P(ref long t0, int slot) { long t = System.Diagnostics.Stopwatch.GetTimestamp(); Prof[slot] += t - t0; t0 = t; return t; }

    public void Step()
    {
        long pt = Profile ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        float dt = Config.SimDt;
        FlushCommands();
        StepOrderQueues();   // N74 명령 대기열: 다리가 끝난 묶음은 다음 다리 (OrderQueue.cs)
        StepComms();     // 전달 중 명령 도착 · 분실 (Comms.cs)
        StepWeather();   // 날씨 · 시간대 일정 (Weather.cs)
        if (Map.Version != lastMapVersion) RefreshPaths();
        if (nav != null) { nav.BeginTick(Tick); nav.Process(RouteWorkPerTick); }

        foreach (var (faction, ai) in Controllers)
            if ((Tick + faction * 7) % 15 == 0 && !Factions[faction].Defeated) ai.Tick(this, faction);
        if (Profile) P(ref pt, 0);

        if (hashDirty) RebuildHash();   // 틱 경계에서 항상 dirty — 앞부분(AI · 명령)의 질의가 이미 지금 위치로 다시 만들었다면 그대로 쓴다 (그 뒤 해시에 영향 주는 변경은 hashDirty 를 올린다)
        if (Profile) P(ref pt, 1);
        StepFormationTriggers();   // 포병 대형 접촉 → 틀 (화력-기동) · 횡대 전개 (CombatFormations.cs)
        StepTrenchAssaults();   // N32 참호 돌격 순서 (TrenchAssault.cs) — 전술 틀 행동보다 먼저 (그 중대는 틀 행동을 건너뜀)
        StepTactics();     // 전술 틀 2단계: 지휘관의 역할별 행동 · 전환 (TacticDrill.cs)
        StepAnchors(dt);   // 중대 깃발 전진 (OrgFormation.cs) — 구성원 이동 전에
        StepCommand();     // 지휘관 승계 안전망 · 장교 교전 경험치 (CommandRanks.cs)
        StepContact();     // F1 중대 접적 상태 (Contact.cs)
        StepGapChecks();   // 장교 빈자리 점검 · 칼 흔들기 (Squads.cs, 중대마다 10초 간격)
        StepCloseRanks();  // 대열 정비: 장교 판단 (교전 중 크게 흐트러지면) · 명령 유효 중 늦게 따르는 병사 (CloseRanks.cs)
        StepRestAlign();   // 대형 완화: 비접적 휴식 정렬 (FormRelax.cs)
        StepMarchFire();   // 장교 (중대) · 부사관 (분대) 행진 사격 시작 · 종료 (MarchCommand.cs)
        StepScouts();      // 정찰병 자율 행동 (Scouts.cs) — 이동 목표만 정한다
        StepHeavyPacing(); // 중화기 보폭 기준: 중대 · 대대 · 섞인 이동 묶음의 보병 앞선 (HeavyPacing.cs)
        for (int i = 0; i < Units.Count; i++) UpdateUnit(Units[i], dt);
        if (Profile) P(ref pt, 2);
        RebuildHash();
        if (Profile) P(ref pt, 3);
        Separate();
        if (Profile) P(ref pt, 4);
        foreach (var u in Units) ResolveTerrain(u);
        if (Profile) P(ref pt, 5);

        UpdateCapture(dt);
        UpdateBlasts();     // 수류탄 폭발 (Combat4.cs)
        UpdateShells();     // 포탄 착탄 (ShellFlight.cs)
        UpdateSupply(dt);   // 탄약 회복 · 보급 마차 (Supply.cs)
        if (Profile) P(ref pt, 6);
        RemoveDead();
        if (Profile) P(ref pt, 7);
        Fog.UpdateStaggered();   // 세력마다 12틱 간격, 세력별 시차 (FogOfWar.cs)
        if (Profile) P(ref pt, 8);
        UpdateVictory(dt);
        PruneEffects();
        Tick++;
        if (Profile) P(ref pt, 9);
        if (RecordCommands && Tick % ReplayFile.ChecksumInterval == 0) ChecksumLog.Add(new ChecksumRecord(Tick, ComputeChecksum()));
        // 틱 경계 불변식 (저장 · 불러오기가 의존): 길찾기는 지도와 동기화돼 있고, 공간 해시는 '낡은 채로' 다음 틱으로 넘어가지 않는다
        // (다음 틱 앞부분의 AI · 명령 질의는 처음 묻는 순간 현재 위치로 해시를 다시 만든다 — 저장한 월드와 이어 돌린 월드가 같은 해시를 보게 된다).
        if (Map.Version != lastMapVersion) RefreshPaths();
        hashDirty = true;
        if (Profile) P(ref pt, 10);
    }

    /// <summary>주기적 체크섬 기록 (리플레이 검증용).</summary>
    public readonly List<ChecksumRecord> ChecksumLog = new();

    /// <summary>지도 통행 정보가 바뀌면 (건물 제거 등): 목표 칸을 다시 해석하고, 계층 그래프를 영향받은 구역만 갱신 (그 구역을 지나는 경로는 무효 → 유닛이 다시 요청).</summary>
    void RefreshPaths()
    {
        lastMapVersion = Map.Version;
        var groups = new HashSet<MoveGroup>();
        foreach (var u in Units)
        {
            if (u.Group != null) groups.Add(u.Group);
        }
        foreach (var g in groups) g.Retarget(this);
        navEpoch++;
        nav?.Sync();
    }

    void RemoveDead()
    {
        bool removedBuilding = false;
        int firstDead = -1;
        for (int i = 0; i < Units.Count; i++) if (!Units[i].Alive) { firstDead = i; break; }
        if (firstDead >= 0)
        {
            for (int i = Units.Count - 1; i >= firstDead; i--)   // 기록 순서는 예전과 같게 (뒤에서부터)
            {
                var u = Units[i];
                if (u.Alive) continue;
                Deaths.Add((u.Pos, Tick, false)); IndexDead(u.Owner);
                if (u.Def.Bearer) BearerFell(u);   // N68
                LeaveCompany(u);   // 빈 중대 · 빈 대대는 즉시 제거, 분대 자동 합치기 (Squads.cs)
                byId.Remove(u.Id);
            }
            int w = firstDead;   // 한 번에 앞으로 당겨 채움 (RemoveAt 반복은 O(n·죽은 수))
            for (int i = firstDead; i < Units.Count; i++) if (Units[i].Alive) Units[w++] = Units[i];
            Units.RemoveRange(w, Units.Count - w);
            hashDirty = true;
        }
        for (int i = Buildings.Count - 1; i >= 0; i--)
        {
            var b = Buildings[i];
            if (b.Alive) continue;
            Deaths.Add((b.Pos, Tick, true));
            Map.SetOccupant(b.TileX, b.TileY, b.Size, 0);
            byId.Remove(b.Id);
            Buildings.RemoveAt(i);
            removedBuilding = true;
        }
        if (removedBuilding) RefreshPaths();
    }

    // ---------------------------------------------------------------- 이동 (길찾기 본체는 WorldNav.cs)

    /// <summary>[호환] target 의 목표 표시 껍데기. 게임 안의 이동은 계층형 길찾기(WorldNav)가 한다.</summary>
    public FlowField FieldTo(Vector2 target) => new FlowField(FlowField.ResolveGoal(Map, target).Goal);

    static float TurnToward(float current, float target, float maxDelta)
    {
        float diff = target - current;
        diff -= MathF.Tau * MathF.Floor((diff + MathF.PI) / MathF.Tau);   // [-π, π) 로 감기 (IEEERemainder 보다 싸다)
        return current + Math.Clamp(diff, -maxDelta, maxDelta);
    }

    void FaceToward(Unit u, Vector2 p, float dt)
    {
        Vector2 d = p - u.Pos;
        float rate = u.Deploy is DeployPhase.Deploying or DeployPhase.Deployed ? u.Def.TraverseRadPerSec : u.Def.TurnRadPerSec;   // 거치한 경기관총: 느린 조준 회전 (traverseRate)
        if (d.LengthSquared() > 1e-6f) u.Facing = TurnToward(u.Facing, ClampTraverse(u, DetMath.Atan2(d.Y, d.X)), rate * dt);   // 방열한 대포는 사계 안에서만 (Combat2.cs)
    }

    // ---------------------------------------------------------------- 공간 해시 · 충돌

    void RebuildHash()
    {
        hashDirty = false;
        int n = Units.Count, W = Map.Width;
        if (sortedIdx.Length < n) { sortedIdx = new int[n * 2]; unitCell = new int[n * 2]; }
        if (occCells.Length < n) Array.Resize(ref occCells, n * 2);   // 지난 점유 목록 (occCount 개) 은 보존해야 지울 수 있다
        // 1) 지난번에 점유했던 칸만 되돌려 지운다 (칸 수 · 지도 면적과 무관)
        for (int k = 0; k < occCount; k++)
        {
            int c = occCells[k];
            cellCount[c] = 0;
            cellMask[c] = 0;
            coarseMask[(c / W >> 3) * coarseW + ((c % W) >> 3)] = 0;
        }
        occCount = 0;
        uint um = 0;
        officers.Clear();
        bearers.Clear();
        spotters.Clear();
        for (int i = 0; i < n; i++)
        {
            var u = Units[i];
            if (u.Def.Spotter) spotters.Add(u);   // 곡사포 관측 유닛 (Combat4.cs)
            if (u.Def.CommandRadius > 0) officers.Add(u);
            if (u.Def.Bearer) bearers.Add(u);   // N68 기수   // 장교 목록 (지휘 범위 판정, Combat3.cs)
            int c = CellOf(u.Pos);
            unitCell[i] = c;
            if (cellCount[c]++ == 0) occCells[occCount++] = c;
            uint bit = 1u << (u.Owner & 31);
            cellMask[c] |= bit;
            coarseMask[(c / W >> 3) * coarseW + ((c % W) >> 3)] |= bit;
            um |= bit;
        }
        // 2) 점유 칸만 오름차순 정렬 → 시작 위치 (예전 전체 접두합과 같은 값)
        SortCells(occCells, occCount);
        int run = 0;
        for (int k = 0; k < occCount; k++)
        {
            int c = occCells[k];
            cellStart[c] = run;
            run += cellCount[c];
            cellCursor[c] = run;
        }
        for (int i = 0; i < n; i++) sortedIdx[--cellCursor[unitCell[i]]] = i;   // 번호 오름차순으로 뒤에서부터 채움 → 칸 안은 내림차순
        unitMask = um;
        uint bm = 0;
        foreach (var b in Buildings) bm |= 1u << (b.Owner & 31);
        bldgMask = bm;
    }

    int[] sortTmp = new int[64];
    /// <summary>서로 다른 칸 번호 n 개를 오름차순으로 (11비트씩 LSD 기수 정렬 — 값이 유일하므로 결과는 유일). 작으면 삽입 정렬.</summary>
    void SortCells(int[] a, int n)
    {
        if (n < 64)
        {
            for (int i = 1; i < n; i++)
            {
                int v = a[i], j = i - 1;
                while (j >= 0 && a[j] > v) { a[j + 1] = a[j]; j--; }
                a[j + 1] = v;
            }
            return;
        }
        if (sortTmp.Length < n) sortTmp = new int[a.Length];
        int maxV = cellMask.Length - 1;
        int[] src = a, dst = sortTmp;
        Span<int> cnt = stackalloc int[2049];
        for (int shift = 0; shift == 0 || (maxV >> shift) != 0; shift += 11)
        {
            cnt.Clear();
            for (int i = 0; i < n; i++) cnt[((src[i] >> shift) & 2047) + 1]++;
            for (int b = 0; b < 2048; b++) cnt[b + 1] += cnt[b];
            for (int i = 0; i < n; i++) dst[cnt[(src[i] >> shift) & 2047]++] = src[i];
            (src, dst) = (dst, src);
        }
        if (!ReferenceEquals(src, a)) Array.Copy(src, a, n);
    }

    /// <summary>[테스트 전용] 칸 c 의 유닛 (Units 번호) — 해시가 돌려주는 순서 그대로.</summary>
    internal void HashCellForTest(int c, List<int> into)
    {
        into.Clear();
        if (hashDirty) RebuildHash();
        for (int k = cellStart[c], e = k + cellCount[c]; k < e; k++) into.Add(sortedIdx[k]);
    }

    /// <summary>[테스트 전용] 해시의 전체 순서 (Separate 가 도는 순서 = sortedIdx 0..n-1).</summary>
    internal int[] HashOrderForTest()
    {
        if (hashDirty) RebuildHash();
        return sortedIdx.AsSpan(0, Units.Count).ToArray();
    }

    /// <summary>[테스트 전용] 칸 · 8×8 묶음 소유 세력 비트의 사본.</summary>
    internal (uint[] Cell, uint[] Coarse) HashMasksForTest()
    {
        if (hashDirty) RebuildHash();
        return ((uint[])cellMask.Clone(), (uint[])coarseMask.Clone());
    }

    /// <summary>[테스트 전용] 다음 질의에서 해시를 다시 만들게 한다 (틱 경계와 같은 상태).</summary>
    internal void MarkHashDirtyForTest() => hashDirty = true;

    int CellOf(Vector2 p)
    {
        int x = Math.Clamp((int)p.X, 0, Map.Width - 1);
        int y = Math.Clamp((int)p.Y, 0, Map.Height - 1);
        return y * Map.Width + x;
    }

    /// <summary>center 반경 radius 안의 유닛 (공간 해시 사용). 결과는 재사용 버퍼.</summary>
    public List<Unit> UnitsInRadius(Vector2 center, float radius, List<Unit>? into = null)
    {
        into ??= new List<Unit>();
        into.Clear();
        if (hashDirty) RebuildHash();
        int x0 = Math.Max(0, (int)(center.X - radius - 1)), x1 = Math.Min(Map.Width - 1, (int)(center.X + radius + 1));
        int y0 = Math.Max(0, (int)(center.Y - radius - 1)), y1 = Math.Min(Map.Height - 1, (int)(center.Y + radius + 1));
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int c = y * Map.Width + x;
            for (int k = cellStart[c], e = k + cellCount[c]; k < e; k++)
            {
                int j = sortedIdx[k];
                if (j >= Units.Count) continue;
                var u = Units[j];
                if (u.Alive && Vector2.DistanceSquared(u.Pos, center) <= r2) into.Add(u);
            }
        }
        return into;
    }

    /// <summary>
    /// 겹침 해소 (2026-10-18 재설계, docs/COLLISION_ANALYSIS.md). 쌍은 반경이 더 큰 쪽(같으면 칸 순서가 앞선 쪽)이 맡는다 — 유닛은 자기 반경 기준(2r)으로만 이웃 칸을 보므로
    /// 작은 유닛은 3×3 칸만, 큰 유닛(전차 · 도시 위 유닛)만 넓게 본다. 도시 위의 유닛은 반경 2배.
    /// 모든 쌍의 밀림을 원래 위치 기준으로 누적한 뒤 한 번에 적용한다 (Jacobi — 처리 순서와 무관하게 대칭, 같은 입력 = 같은 결과).
    /// 한 틱에 겹침의 일부만 민다 (감쇠: 보통 SepStiff, 깊으면 SepStiffDeep) + 겹침 여유 (SepSlop · 같은 대형끼리 SepSlopForm) — 슬롯으로 다시 걸어 들어가는 미세한 밀고 당기기를 없앤다.
    /// 유닛당 틱당 밀림은 MaxSeparationStep 으로 자른다 (ROADMAP §2-1 ③).
    /// </summary>
    void Separate()
    {
        int n = Units.Count;
        if (sepX.Length < n)
        {
            int m = n * 2;
            sepX = new float[m]; sepY = new float[m]; sepR = new float[m]; sepFlag = new byte[m]; sepOwner = new byte[m];
            sepDX = new float[m]; sepDY = new float[m]; sepN = new byte[m]; sepR0 = new float[m];
            sepGrp = new MoveGroup?[m]; sepLast = new MoveGroup?[m];
        }
        int W = Map.Width, H = Map.Height;
        int nf = Math.Min(Factions.Count, 32);
        for (int f = 0; f < nf; f++)
        {
            uint hm = 0;
            for (int g = 0; g < nf; g++) if (IsHostile(f, g)) hm |= 1u << g;
            sepHostile[f] = hm;
        }
        for (int k = 0; k < n; k++)
        {
            var u = Units[sortedIdx[k]];
            Vector2 p = u.Pos;
            sepX[k] = p.X; sepY[k] = p.Y;
            sepDX[k] = 0; sepDY[k] = 0; sepN[k] = 0;
            int tx = Math.Clamp((int)p.X, 0, W - 1), ty = Math.Clamp((int)p.Y, 0, H - 1);
            sepR[k] = Map.IsWalkOnOccupied(tx, ty) ? u.Radius * 2f : u.Radius;
            sepR0[k] = u.Radius;
            sepFlag[k] = (byte)(SepFlags(u) | (u.Group != null && (u.Group.Plan != null || ReferenceEquals(u.Company?.Anchor?.Group, u.Group)) ? 128 : 0));   // 128 = 슬롯이 있는 대형 이동 (진형 · 중대 깃발)
            sepOwner[k] = (byte)(u.Owner & 31);
            sepGrp[k] = u.Group; sepLast[k] = u.LastGroup;
        }
        for (int k = 0; k < n; k++)
        {
            float ra = sepR[k];
            int reach = (int)MathF.Ceiling(ra * 2f);
            int c = unitCell[sortedIdx[k]];
            int cx = c % W, cy = c / W;
            int y1 = Math.Min(cy + reach, H - 1), x1 = Math.Min(cx + reach, W - 1), x0 = Math.Max(cx - reach, 0);
            float ax = sepX[k], ay = sepY[k];
            for (int ny = Math.Max(cy - reach, 0); ny <= y1; ny++)
            {
                // 한 행의 연속된 칸들을 칸 순서대로 (= 정렬 배열의 연속 구간과 같은 순서). 빈 칸은 건너뜀
                int row = ny * W;
                for (int cc = row + x0, ce = row + x1; cc <= ce; cc++)
                for (int j = cellStart[cc], e = j + cellCount[cc]; j < e; j++)
                {
                    float rb = sepR[j];
                    if (rb > ra || (rb == ra && j <= k)) continue;
                    float ddx = sepX[j] - ax, ddy = sepY[j] - ay, rs = ra + rb;
                    if (ddx * ddx + ddy * ddy >= rs * rs) continue;   // 대부분은 겹치지 않는다
                    SeparatePair(k, j, ra, rb);
                }
            }
        }
        for (int k = 0; k < n; k++)
        {
            if ((sepFlag[k] & 4) == 0) continue;
            float mx = sepDX[k], my = sepDY[k];
            int cn = sepN[k];
            if (cn > 2) { float f = SepRelax / MathF.Sqrt(cn); mx *= f; my *= f; }   // 접촉이 여럿이면 합 × 이완 계수 / √접촉 수 (합을 그대로 쓰면 빽빽한 무리에서 넘쳐 떨리고, 평균은 너무 물러 무리가 짓눌린다)
            float m2 = mx * mx + my * my;
            if (m2 < 1e-12f) continue;
            var u = Units[sortedIdx[k]];
            if (m2 > MaxSeparationStep * MaxSeparationStep)   // 틱당 밀림 상한
            {
                float s = MaxSeparationStep / MathF.Sqrt(m2);
                mx *= s; my *= s;
                Stats.SepClamped++;
            }
            if ((sepFlag[k] & 128) != 0 && Defs.CombatRules.ChokeEnabled && ChokeAt(u.Pos)) { mx *= Defs.CombatRules.ChokePushScale; my *= Defs.CombatRules.ChokePushScale; }   // choke ④
            Stats.SepPushUnits++; Stats.SepPushSum += MathF.Sqrt(mx * mx + my * my);
            u.Pos = new Vector2(u.Pos.X + mx, u.Pos.Y + my);
        }
        Array.Clear(sepGrp, 0, n);
        Array.Clear(sepLast, 0, n);
    }

    /// <summary>한 틱에 미는 겹침 비율 (보통 · 깊은 겹침 = 반경 합의 절반보다 가까움 · 같은 대형 슬롯끼리 = 부드럽게).</summary>
    const float SepStiff = 0.8f, SepStiffDeep = 0.8f, SepStiffForm = 0.5f;
    /// <summary>접촉이 3개 이상인 유닛의 밀림 = 합 × SepRelax / √접촉 수.</summary>
    const float SepRelax = 1.5f;
    /// <summary>둘 다 고정 (사수 · 사격 자세, 방열 · 참호 제외) 인 아군 쌍의 가중치 — 겹친 채 굳지 않을 만큼만.</summary>
    const float SepHoldShuffle = 0.15f;
    /// <summary>겹침 여유 (칸): 이만큼은 겹쳐도 밀지 않는다. 같은 대형 (같은 이동 그룹 · 둘 다 걷는 중) 끼리는 슬롯 간격이 보장되므로 더 너그럽게.</summary>
    const float SepSlop = 0.02f, SepSlopForm = 0.06f;

    static byte SepFlags(Unit u) => (byte)((u.Immovable ? 1 : 0) | (u.IsMoving ? 2 : 0) | (u.Velocity != Vector2.Zero ? 8 : 0) | (u.Def.Scout && u.ScoutAuto && u.Order == UnitOrder.Move ? 16 : 0) | (u.TrenchSpot >= 0 ? 32 : 0)
        | (u.DeployState != 0 || u.InTrench ? 64 : 0));   // 16 = 자율 이동 중인 정찰병 (Scouts.cs), 32 = 참호 자리를 맡음 (Trenches.cs), 64 = 완전 고정 (방열 · 참호 안)

    /// <summary>
    /// 한 쌍의 겹침 해소 (ROADMAP §2-1, 2026-10-18 재설계) — 밀림은 sepDX/DY 에 누적만 한다 (적용은 Separate 끝에서 한 번).
    /// 아군(비적대) 쌍: 밀림을 가중치로 나눈다 — 고정(사수 · 참호 · 방열 · 사격 자세) 0, 움직이는 쪽 1, 그 상대로 서 있는 쪽 0.25 (움직이는 쪽이 비켜 간다 ②),
    /// 같은 대형으로 함께 걷는 쌍은 0.5 : 0.5 + 넓은 여유. 참호 자리를 맡은 아군끼리 · 자율 정찰병과 아군은 밀지 않는다.
    /// 적대 쌍: 밀지 않고 막기만 한다 ④ — 이번 틱에 실제로 움직인 쪽만 자기 겹침을 되돌린다 (상대는 그대로). 둘 다 움직였거나 둘 다 서 있으면 반씩,
    /// 고정 유닛은 역시 0. 운동량이 적에게 전달되지 않으므로 밀어붙여도 적 대열이 밀리지 않는다.
    /// </summary>
    void SeparatePair(int a, int b, float ra, float rb)
    {
        float dx = sepX[b] - sepX[a], dy = sepY[b] - sepY[a];
        float rr = ra + rb;
        float d2 = dx * dx + dy * dy;
        if (d2 >= rr * rr) return;
        Stats.SepPairs++;
        float dist = MathF.Sqrt(d2);
        float nx, ny;
        if (dist > 1e-4f) { nx = dx / dist; ny = dy / dist; }
        else
        {
            int ia = Units[sortedIdx[a]].Id, ib = Units[sortedIdx[b]].Id;
            float ang = (ia * 7919 + ib * 104729) % 628 / 100f;  // 완전히 겹친 경우 결정적 방향
            nx = DetMath.Cos(ang); ny = DetMath.Sin(ang);
        }

        byte fa = sepFlag[a], fb = sepFlag[b];
        float wa, wb, slop = SepSlop, stiff = dist < rr * 0.5f ? SepStiffDeep : SepStiff;
        if ((sepHostile[sepOwner[a]] >> sepOwner[b] & 1) != 0)
        {
            stiff = SepStiffDeep;   // 적대: 막기 (단단하게)
            bool walkA = (fa & 8) != 0, walkB = (fb & 8) != 0;
            wa = (fa & 1) != 0 ? 0 : walkA || !walkB ? 1f : 0f;
            wb = (fb & 1) != 0 ? 0 : walkB || !walkA ? 1f : 0f;
        }
        else
        {
            // 앞서 나가는 정찰병은 아군 대열을 그대로 통과한다 (촘촘한 대열은 몸으로 뚫을 수 없어 정찰병이 중대 속에 갇힌다)
            if (((fa ^ fb) & 16) != 0) return;
            // 참호 자리를 맡은 아군끼리는 밀지 않는다 (칸당 trenchPerTile 명이 몸 반경보다 촘촘히 선다 — 자리가 겹치지 않게 배정돼 있음)
            if ((fa & fb & 32) != 0) return;
            bool movA = (fa & 2) != 0, movB = (fb & 2) != 0;
            if (movA && movB && (fa & fb & 128) != 0 && ReferenceEquals(sepGrp[a], sepGrp[b])) { wa = wb = 0.5f; slop = SepSlopForm; stiff = SepStiffForm; }   // 같은 대형 (슬롯) 으로 함께 걷는 중 — 슬롯이 간격을 보장
            else
            {
                wa = !movA && movB ? 0.25f : 1f;
                wb = !movB && movA ? 0.25f : 1f;
            }
            if ((fa & fb & 1) != 0 && ((fa | fb) & 64) == 0)
            {
                // 둘 다 서서 버티는 중 (사수 · 사격 자세): 겹친 채 굳지 않게 조금씩 비켜 선다 — 몸 반경 기준 (도시 위 2배 반경은 쓰지 않음)
                wa = wb = SepHoldShuffle;
                rr = sepR0[a] + sepR0[b];
            }
            else
            {
                if ((fa & 1) != 0) wa = 0;
                if ((fb & 1) != 0) wb = 0;
            }
        }
        float sum = wa + wb;
        float over = rr - dist - slop;
        if (sum > 0 && over > 0)
        {
            float push = over * stiff / sum;
            float pa = push * wa, pb = push * wb;
            sepDX[a] -= nx * pa; sepDY[a] -= ny * pa;
            sepDX[b] += nx * pb; sepDY[b] += ny * pb;
            if (pa > 0) { sepFlag[a] |= 4; if (sepN[a] < 255) sepN[a]++; }
            if (pb > 0) { sepFlag[b] |= 4; if (sepN[b] < 255) sepN[b]++; }
        }

        // 먼저 도착한 같은 그룹 유닛과 닿으면 함께 도착 처리 (뭉쳐서 멈추기) — 드문 경로라 유닛 객체로 처리
        if ((sepGrp[a] != null && sepGrp[b] == null && sepLast[b] == sepGrp[a]) || (sepGrp[b] != null && sepGrp[a] == null && sepLast[a] == sepGrp[b]))
        {
            var ua = Units[sortedIdx[a]]; var ub = Units[sortedIdx[b]];
            TouchArrive(ua, ub);
            TouchArrive(ub, ua);
            sepGrp[a] = ua.Group; sepGrp[b] = ub.Group; sepLast[a] = ua.LastGroup; sepLast[b] = ub.LastGroup;
            sepFlag[a] = (byte)((sepFlag[a] & (4 | 128)) | SepFlags(ua));
            sepFlag[b] = (byte)((sepFlag[b] & (4 | 128)) | SepFlags(ub));
        }
    }

    void TouchArrive(Unit mover, Unit other)
    {
        if (mover.Group == null || other.Group != null || other.LastGroup != mover.Group) return;
        if (mover.Order != UnitOrder.Move && mover.Order != UnitOrder.AttackMove) return;
        var g = mover.Group;
        if (ReferenceEquals(mover.Company?.Anchor?.Group, g)) return;   // 중대 깃발 대형: 각자 자기 슬롯까지 간다
        if (Vector2.Distance(mover.Pos, g.Goal) < g.ArriveRadius * 2.2f)
        {
            if (g.Plan != null && mover.Def.TurnRate <= 0) mover.Facing = g.Plan.Facing;   // 진형 도착: 비켜 가느라 틀어진 방향 대신 진행 방향을 본다 (WorldNav 의 슬롯 도착과 같게)
            ArriveGroup(mover);
        }
    }

    void ResolveTerrain(Unit u)
    {
        float r = u.Radius;
        u.Pos.X = Math.Clamp(u.Pos.X, r, Map.Width - r);
        u.Pos.Y = Math.Clamp(u.Pos.Y, r, Map.Height - r);
        int x0 = (int)MathF.Floor(u.Pos.X - r), x1 = (int)MathF.Floor(u.Pos.X + r);
        int y0 = (int)MathF.Floor(u.Pos.Y - r), y1 = (int)MathF.Floor(u.Pos.Y + r);
        for (int ty = y0; ty <= y1; ty++)
        for (int tx = x0; tx <= x1; tx++)
        {
            if (Map.IsWalkable(tx, ty)) continue;
            var closest = new Vector2(Math.Clamp(u.Pos.X, tx, tx + 1), Math.Clamp(u.Pos.Y, ty, ty + 1));
            Vector2 d = u.Pos - closest;
            float l2 = d.LengthSquared();
            if (l2 >= r * r) continue;
            if (l2 > 1e-8f) u.Pos = closest + d / MathF.Sqrt(l2) * r;
            else
            {
                var (nx, ny) = Map.NearestWalkable(tx, ty);
                u.Pos = new Vector2(nx + 0.5f, ny + 0.5f);
                return;
            }
        }
    }
}
