using System.Numerics;
using System.Text.Json.Serialization;

namespace KoreanDivision.Core;

// ============================================================================ 열차 (T30a, 2026-10-22, FormatVersion 58 · Replay 23)
// 열차 = 객차 N량의 순서열. 객차 = TrainCar (Entity 하위 — 소유 세력 · HP · 장갑, T30b 에서 공격 대상).
// 객차는 World.Units / byId 에 넣지 않는다 → 군집 · 충돌 해소 · 전술 AI · 길찾기 · 표적 탐색에 끼지 않음 (열차는 World.Trains 만).
// 위치 = 선로 경로 위 호장(arc) 정수 오프셋 (1칸 = TrainTrack.Unit). 선두 객차 호장 S, 객차 i = S - i·간격.
// 이동: 조종(TrainMoveCommand 전진 · 정지 · 후진) 또는 스크립트(경유점 + 정차 시간, 선로 끝에서 되돌아 반복 · 순환선은 계속 돈다).
// 앞쪽 선로 위에 유닛이 있으면 멈춘다 (치지 않음). 선로 = 지형 (파괴 없음), 분기 없음 (경유점 순서로 경로 고정).

/// <summary>balance.json "trains" (T30a). 속도 · 가속은 칸/초 단위, 틱 단위 정수로 바꿔 쓴다.</summary>
public sealed class TrainRulesDef
{
    /// <summary>최고 속도 (칸/초). 보병 걸음(≈1.2)보다 빠르고 기병 속보보다 느리게.</summary>
    public float MaxSpeed { get; set; } = 3f;
    /// <summary>가속 (칸/초²).</summary>
    public float Accel { get; set; } = 0.4f;
    /// <summary>제동 (칸/초²). 최고 속도에서 정지 거리 = MaxSpeed² / (2·Brake) = 3.75칸.</summary>
    public float Brake { get; set; } = 1.2f;
    /// <summary>객차 중심 간격 (칸).</summary>
    public float CarSpacing { get; set; } = 1.5f;
    /// <summary>열차 한 대의 최대 객차 수.</summary>
    public int MaxCars { get; set; } = 8;
    /// <summary>앞쪽 선로를 살피는 거리 (칸, 선두 객차 끝부터). 정지 거리보다 길게.</summary>
    public float Lookahead { get; set; } = 5f;
    /// <summary>선로 위 유닛 판정 반경 (칸, 선로 중심선에서).</summary>
    public float BlockRadius { get; set; } = 0.7f;
    /// <summary>객차 종류. 1차(T30a)는 이름 · HP · 장갑만 (무기 = T30b).</summary>
    public Dictionary<string, TrainCarDef> Cars { get; set; } = new()
    {
        ["locomotive"] = new() { Name = "기관차", Hp = 400, Armor = "heavy" },
        ["wagon"] = new() { Name = "화차", Hp = 200, Armor = "basic" },
    };

    public const int TicksPerSecond = 60;
    internal int VMax => Math.Max(1, (int)MathF.Round(MaxSpeed * TrainTrack.Unit / TicksPerSecond));
    internal int Acc => Math.Max(1, (int)MathF.Round(Accel * TrainTrack.Unit / (TicksPerSecond * TicksPerSecond)));
    internal int Brk => Math.Max(1, (int)MathF.Round(Brake * TrainTrack.Unit / (TicksPerSecond * TicksPerSecond)));
    internal long Pitch => Math.Max(1, (long)MathF.Round(CarSpacing * TrainTrack.Unit));
    internal long Look => (long)MathF.Round(Lookahead * TrainTrack.Unit);
}

public sealed class TrainCarDef
{
    public string Name { get; set; } = "";
    public float Hp { get; set; } = 100;
    /// <summary>balance.json armors 키.</summary>
    public string Armor { get; set; } = "basic";
    public int Sight { get; set; } = 6;
}

/// <summary>미션 json "trains" 한 줄. path = 경유점 칸 [[x, y], ...] (모두 rail 칸, 사이는 선로 칸을 따라 BFS), waits = 경유점마다 정차 초.</summary>
public sealed class MissionTrain
{
    public string? Tag { get; set; }
    public int Faction { get; set; } = 1;
    /// <summary>객차 종류 (앞 = 선두). balance.json trains.cars 키.</summary>
    public List<string> Cars { get; set; } = new();
    public int[][] Path { get; set; } = Array.Empty<int[]>();
    public float[]? Waits { get; set; }
    /// <summary>true = 순환선 (마지막 경유점에서 첫 경유점으로 이어짐).</summary>
    public bool Loop { get; set; }
    /// <summary>true (기본) = 스크립트 이동, false = 조종 대기 (명령이 올 때까지 정지).</summary>
    public bool Script { get; set; } = true;
    /// <summary>시작 경유점 번호 (선두 객차 위치).</summary>
    public int Start { get; set; }
}

/// <summary>선로 경로: 칸 열 + 누적 호장 (정수). 순환선이면 마지막 칸 → 첫 칸 구간이 하나 더 있다.</summary>
public sealed class TrainTrack
{
    public const int Unit = 1 << 16;        // 1칸
    const int DiagUnit = 92682;             // √2 · Unit (반올림)
    public readonly int[] TX, TY;
    public readonly long[] Cum;             // Cum[i] = 칸 i 까지 호장
    public readonly int[] Waypoints;        // 경유점마다 칸 번호
    public readonly bool Loop;
    public readonly long Length;            // 순환선 = 닫는 구간 포함

    public TrainTrack(int[] tx, int[] ty, int[] waypoints, bool loop)
    {
        TX = tx; TY = ty; Waypoints = waypoints; Loop = loop;
        Cum = new long[tx.Length];
        for (int i = 1; i < tx.Length; i++) Cum[i] = Cum[i - 1] + Step(i - 1, i);
        Length = Cum[^1] + (loop ? Step(tx.Length - 1, 0) : 0);
    }

    long Step(int a, int b) => TX[a] != TX[b] && TY[a] != TY[b] ? DiagUnit : Unit;
    public long WaypointArc(int k) => Cum[Waypoints[k]];

    /// <summary>경유점 칸 사이를 rail 칸으로 잇는다 (8방향 BFS, 고정 이웃 순서 — 결정적). 이미 쓴 칸은 다시 지나지 않는다.</summary>
    public static TrainTrack Build(GameMap map, IReadOnlyList<(int X, int Y)> wps, bool loop)
    {
        if (wps.Count < 2) throw new InvalidDataException("열차 경로: 경유점이 2개 이상이어야 함");
        foreach (var (x, y) in wps)
            if (!map.InBounds(x, y) || map.Get(x, y) != TerrainType.Rail) throw new InvalidDataException($"열차 경로: 경유점 ({x}, {y}) 가 선로(rail) 칸이 아님");
        var xs = new List<int> { wps[0].X }; var ys = new List<int> { wps[0].Y };
        var used = new HashSet<int> { wps[0].Y * map.Width + wps[0].X };
        var idx = new List<int> { 0 };
        int legs = loop ? wps.Count : wps.Count - 1;
        for (int k = 0; k < legs; k++)
        {
            var a = wps[k]; var b = wps[(k + 1) % wps.Count];
            var leg = Bfs(map, a, b, used) ?? throw new InvalidDataException($"열차 경로: ({a.X}, {a.Y}) → ({b.X}, {b.Y}) 를 잇는 선로가 없음 (분기 · 이미 지난 칸 제외)");
            for (int i = 1; i < leg.Count; i++)
            {
                if (loop && k == legs - 1 && i == leg.Count - 1) break;   // 닫는 구간의 끝 = 첫 칸
                xs.Add(leg[i].X); ys.Add(leg[i].Y); used.Add(leg[i].Y * map.Width + leg[i].X);
            }
            if (!(loop && k == legs - 1)) idx.Add(xs.Count - 1);
        }
        return new TrainTrack(xs.ToArray(), ys.ToArray(), idx.ToArray(), loop);
    }

    static readonly (int dx, int dy)[] Nbr = { (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, 1), (-1, -1), (1, -1) };

    static List<(int X, int Y)>? Bfs(GameMap map, (int X, int Y) a, (int X, int Y) b, HashSet<int> used)
    {
        int W = map.Width, start = a.Y * W + a.X, goal = b.Y * W + b.X;
        var prev = new Dictionary<int, int> { [start] = -1 };
        var q = new Queue<int>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            if (c == goal) break;
            int cx = c % W, cy = c / W;
            foreach (var (dx, dy) in Nbr)
            {
                int nx = cx + dx, ny = cy + dy;
                if (!map.InBounds(nx, ny) || map.Get(nx, ny) != TerrainType.Rail) continue;
                int n = ny * W + nx;
                if (prev.ContainsKey(n) || (used.Contains(n) && n != goal)) continue;
                prev[n] = c;
                q.Enqueue(n);
            }
        }
        if (!prev.ContainsKey(goal) || goal == start) return null;
        var path = new List<(int, int)>();
        for (int c = goal; c != -1; c = prev[c]) path.Add((c % W, c / W));
        path.Reverse();
        return path;
    }

    public long Wrap(long s) => Loop ? ((s % Length) + Length) % Length : s;

    /// <summary>호장 s 의 위치 (칸 중심을 잇는 꺾은선), 고정소수 (1칸 = Unit). 정수 연산뿐 — 플랫폼 무관 (선로 위 유닛 판정 · 체크섬 경로용).</summary>
    public void PosFix(long s, out long px, out long py, out int dxs, out int dys)
    {
        s = Wrap(s);
        int n = TX.Length;
        int i;
        if (Loop && s >= Cum[n - 1]) i = n - 1;
        else
        {
            s = Math.Clamp(s, 0, Cum[n - 1]);
            int lo = 0, hi = n - 1;   // Cum[lo] <= s 인 최대 lo
            while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (Cum[mid] <= s) lo = mid; else hi = mid - 1; }
            i = Math.Min(lo, n - 2);
        }
        int j = i + 1 < n ? i + 1 : 0;
        long seg = (j == 0 ? Length : Cum[j]) - Cum[i];
        long ax = (long)TX[i] * Unit + Unit / 2, ay = (long)TY[i] * Unit + Unit / 2;
        long ddx = ((long)TX[j] - TX[i]) * Unit, ddy = ((long)TY[j] - TY[i]) * Unit;
        long off = s - Cum[i];
        px = seg > 0 ? ax + ddx * off / seg : ax;   // long 나눗셈 = 0 쪽 버림 (결정적)
        py = seg > 0 ? ay + ddy * off / seg : ay;
        dxs = Math.Sign(ddx); dys = Math.Sign(ddy);
    }

    /// <summary>호장 s 의 위치 (칸 단위 float, PosFix 에서 변환 — 렌더 · 객차 Pos 용) · 진행 방향 (단위 벡터, 렌더용).</summary>
    public Vector2 PosAt(long s, out Vector2 dir)
    {
        PosFix(s, out long px, out long py, out int dx, out int dy);
        var d = new Vector2(dx, dy);
        dir = d.LengthSquared() > 0 ? d / MathF.Sqrt(d.LengthSquared()) : Vector2.UnitX;
        return new Vector2(px / (float)Unit, py / (float)Unit);
    }
}

/// <summary>객차 하나. 위치 · 방향은 열차 호장에서 매 틱 다시 계산 (저장 안 함).</summary>
public sealed class TrainCar : Entity
{
    public readonly string Kind;
    public readonly TrainCarDef Def;
    public readonly int Index;               // 0 = 선두
    public Vector2 Dir;                      // 진행 방향 (선로 접선, 렌더용)

    public TrainCar(int id, int owner, string kind, TrainCarDef def, int index) : base(id, owner, Vector2.Zero)
    {
        Kind = kind; Def = def; Index = index; Hp = def.Hp;
    }

    public override string Name => Def.Name;
    public override float MaxHp => Def.Hp;
    public override float ArmorValue => Defs.Current.Armor(Def.Armor)?.Armor ?? 0;
    public override int Sight => Def.Sight;
    public override bool Indestructible => false;
    public override float EdgeDistance(Vector2 p) => MathF.Max(0f, Vector2.Distance(p, Pos) - 0.6f);
}

/// <summary>열차 한 대 (저장 · 체크섬 · Audit).</summary>
public sealed class Train
{
    public readonly int Id;
    public int Owner;
    public readonly string? Tag;
    public readonly TrainTrack Track;
    public readonly List<TrainCar> Cars = new();
    public readonly int[] WaitTicks;         // 경유점마다 정차 틱
    public long S;                           // 선두 객차 호장
    public int V;                            // 부호 있는 속도 (호장/틱, + = 경로 정방향)
    public bool Manual;                      // true = 조종 (스크립트 끔)
    public int ManualDir;                    // 조종 명령: -1 후진 · 0 정지 · 1 전진
    public int Next;                         // 스크립트: 다음 경유점
    public int ScriptDir = 1;                // 스크립트: 경유점 진행 방향
    public int WaitLeft;                     // 스크립트: 남은 정차 틱
    public bool Blocked;                     // 이번 틱 앞쪽 선로 위 유닛 때문에 멈춤 (제동)
    internal readonly List<Unit> buf = new();   // 작업 버퍼

    public Train(int id, int owner, string? tag, TrainTrack track, int[] waitTicks)
    {
        Id = id; Owner = owner; Tag = tag; Track = track; WaitTicks = waitTicks;
    }

    public long TailOffset(TrainRulesDef r) => (Cars.Count - 1) * r.Pitch;
    long SMin(TrainRulesDef r) => Track.Loop ? long.MinValue : TailOffset(r);
    long SMax => Track.Loop ? long.MaxValue : Track.Length;
    /// <summary>경유점 k 에 선두를 둘 호장 (선로 끝에서는 열차 전체가 선로 안에 있게 당김).</summary>
    public long StopArc(int k, TrainRulesDef r) => Track.Loop ? Track.WaypointArc(k) : Math.Clamp(Track.WaypointArc(k), TailOffset(r), Track.Length);

    internal void PlaceCars(TrainRulesDef r)
    {
        foreach (var c in Cars)
        {
            c.Pos = Track.PosAt(S - c.Index * r.Pitch, out var d);
            c.Dir = d;
        }
    }

    /// <summary>한 틱 진행. blockedAhead(진행 부호) = 앞쪽 선로 위 유닛 여부.</summary>
    /// <summary>앞쪽 선로 감지 주기 (틱). 최고 속도에서 6틱 = 0.3칸 — 감지 거리(5칸) · 정지 거리(3.75칸) 차이 안.</summary>
    public const int BlockCheckTicks = 6;

    internal void Step(TrainRulesDef r, long tick, Func<Train, int, bool> blockedAhead)
    {
        int want = 0;
        long dist = long.MaxValue;   // 원하는 방향으로 멈춰야 할 지점까지
        bool targetStop = false;
        if (Manual)
        {
            want = Math.Sign(ManualDir);
            if (!Track.Loop) dist = want > 0 ? SMax - S : want < 0 ? S - SMin(r) : 0;
        }
        else if (WaitLeft > 0)
        {
            if (V == 0) WaitLeft--;
        }
        else
        {
            long target = StopArc(Next, r);
            long delta = Track.Loop ? Track.Wrap(target - S) : target - S;
            if (delta == 0 && V == 0)
            {
                WaitLeft = WaitTicks[Next];
                AdvanceScript();
            }
            else if (delta != 0)
            {
                want = Track.Loop ? 1 : Math.Sign(delta);
                dist = Math.Abs(delta);
                targetStop = true;
            }
        }

        int sgn = Math.Sign(V), sp = Math.Abs(V);
        int look = sgn != 0 ? sgn : want;
        if (look == 0) Blocked = false;
        else if ((tick + Id) % BlockCheckTicks == 0 || sp == 0) Blocked = blockedAhead(this, look);   // 감지는 6틱마다 (열차마다 엇갈림) — 멈춰 있으면(출발 직전 · 방향 바꿈) 매 틱 바로 본다 — 지난 방향의 막힘 값이 남지 않음
        if (sgn != 0 && sgn != want)
        {
            sp = Math.Max(0, sp - r.Brk);   // 반대 방향 · 정지 명령: 먼저 제동
        }
        else if (want != 0)
        {
            sgn = want;
            long d = Math.Min(dist, 1L << 40);
            long allowed = Blocked ? 0 : Math.Min(r.VMax, ISqrt(2L * r.Brk * d));
            if (sp < allowed) sp = (int)Math.Min(allowed, sp + r.Acc);
            else sp = (int)Math.Max(allowed, sp - r.Brk);
        }
        if (sgn == 0 || sp == 0) { V = 0; return; }
        long room = Track.Loop ? long.MaxValue : sgn > 0 ? SMax - S : S - SMin(r);   // 선로 끝 (관통 없음)
        if (sgn == want && (targetStop || Manual)) room = Math.Min(room, dist);   // 스크립트 정차 지점 · 조종 선로 끝
        long move = Math.Min(sp, Math.Max(0, room));
        S = Track.Wrap(S + sgn * move);
        if (move < sp || move == room) sp = 0;   // 끝 · 정차 지점 도착
        V = sgn * sp;
    }

    void AdvanceScript()
    {
        int n = Track.Waypoints.Length;
        if (Track.Loop) { Next = (Next + 1) % n; return; }
        int nx = Next + ScriptDir;
        if (nx < 0 || nx >= n) { ScriptDir = -ScriptDir; nx = Next + ScriptDir; }
        Next = Math.Clamp(nx, 0, n - 1);
    }

    static long ISqrt(long x)
    {
        if (x <= 0) return 0;
        long s = (long)Math.Sqrt(x);
        while (s * s > x) s--;
        while ((s + 1) * (s + 1) <= x) s++;
        return s;
    }
}

/// <summary>열차 조종: Dir = 1 전진 · 0 정지 · -1 후진 (경로 정방향 기준). 스크립트 이동을 끈다.</summary>
public sealed record TrainMoveCommand(int Faction, int Train, int Dir) : GameCommand(Faction);

public sealed partial class World
{
    /// <summary>열차 목록 (T30a). 비어 있으면 체크섬 · 틱 비용 영향 없음.</summary>
    [SnapExt] public readonly List<Train> Trains = new();

    TrainRulesDef TrainRules => Defs.Trains;

    /// <summary>미션 정의로 열차를 놓는다 (틱 0, 미션 생성 끝). 경로가 선로가 아니면 InvalidDataException.</summary>
    public Train SpawnTrain(MissionTrain spec)
    {
        var r = TrainRules;
        if (spec == null) throw new InvalidDataException("열차 정의가 비어 있음 (null)");
        if (spec.Faction < 1 || spec.Faction >= Factions.Count) throw new InvalidDataException($"열차 세력 {spec.Faction} 없음");
        if (spec.Cars == null) throw new InvalidDataException("열차 cars 가 null (객차 종류 목록 필요)");
        if (spec.Cars.Count < 1 || spec.Cars.Count > r.MaxCars) throw new InvalidDataException($"열차 객차 수 1~{r.MaxCars} (받은 {spec.Cars.Count})");
        foreach (var k in spec.Cars) if (k == null || !r.Cars.ContainsKey(k)) throw new InvalidDataException($"열차 객차 종류 '{k}' 없음 (balance.json trains.cars)");
        if (spec.Path == null || spec.Path.Any(p => p is not { Length: 2 })) throw new InvalidDataException("열차 path 는 [[x, y], ...]");
        if (spec.Waits != null)
            foreach (var wv in spec.Waits)
                if (float.IsNaN(wv) || float.IsInfinity(wv) || wv < 0f || wv > 3600f) throw new InvalidDataException($"열차 waits 값 {wv} (0~3600 초)");
        var track = TrainTrack.Build(Map, spec.Path.Select(p => (p[0], p[1])).ToList(), spec.Loop);
        int n = track.Waypoints.Length;
        if (spec.Start < 0 || spec.Start >= n) throw new InvalidDataException($"열차 start 0~{n - 1} (받은 {spec.Start})");
        if (spec.Waits != null && spec.Waits.Length > n) throw new InvalidDataException("열차 waits 가 경유점보다 많음");
        var waits = new int[n];
        for (int i = 0; i < n; i++) waits[i] = spec.Waits != null && i < spec.Waits.Length ? (int)MathF.Round(spec.Waits[i] * TrainRulesDef.TicksPerSecond) : 0;
        var t = new Train(nextId++, spec.Faction, spec.Tag, track, waits);
        for (int i = 0; i < spec.Cars.Count; i++) t.Cars.Add(new TrainCar(nextId++, spec.Faction, spec.Cars[i], r.Cars[spec.Cars[i]], i));
        if (!track.Loop && track.Length < t.TailOffset(r)) throw new InvalidDataException($"열차 경로가 열차보다 짧음 ({track.Length / (float)TrainTrack.Unit:0.0}칸)");
        if (track.Loop && track.Length < t.Cars.Count * r.Pitch) throw new InvalidDataException($"열차 순환선이 열차보다 짧음 ({track.Length / (float)TrainTrack.Unit:0.0}칸)");
        t.S = t.StopArc(spec.Start, r);
        t.Next = spec.Start;
        if (!track.Loop && spec.Start == n - 1) t.ScriptDir = -1;
        t.Manual = !spec.Script;
        t.PlaceCars(r);
        Trains.Add(t);
        return t;
    }

    public Train? FindTrain(int id)
    {
        foreach (var t in Trains) if (t.Id == id) return t;
        return null;
    }

    /// <summary>열차 조종 명령 (다음 틱 실행).</summary>
    public CommandResult TrainMove(int faction, int trainId, int dir)
    {
        if (FindTrain(trainId) is not { } t || t.Owner != faction) return CommandResult.NotAllowed;
        Issue(new TrainMoveCommand(faction, trainId, Math.Sign(dir)));
        return CommandResult.Ok;
    }

    void ExecTrainMove(int f, TrainMoveCommand c)
    {
        if (FindTrain(c.Train) is not { } t || t.Owner != f) return;
        t.Manual = true;
        t.ManualDir = Math.Sign(c.Dir);
    }

    /// <summary>틱마다: 열차 진행 (유닛 이동 전 — 공간 해시는 이번 틱 위치).</summary>
    internal void StepTrains()
    {
        if (Trains.Count == 0) return;
        var r = TrainRules;
        foreach (var t in Trains)
        {
            t.Step(r, Tick, TrackBlocked);
            t.PlaceCars(r);
        }
    }

    /// <summary>열차 앞쪽 (진행 부호 sgn) 선로 위에 살아 있는 유닛이 있는가. 선두(후진이면 꼬리) 객차 끝에서 Lookahead 까지 0.5칸 간격으로 본다.</summary>
    bool TrackBlocked(Train t, int sgn)
    {
        var r = TrainRules;
        long half = r.Pitch / 2;
        long front = sgn > 0 ? t.S + half : t.S - t.TailOffset(r) - half;
        long lo = t.Track.Loop ? long.MinValue : 0, hi = t.Track.Loop ? long.MaxValue : t.Track.Length;
        long step = TrainTrack.Unit / 2;
        long radFix = (long)MathF.Round(r.BlockRadius * TrainTrack.Unit);
        float query = r.BlockRadius + Defs.MaxUnitRadius + 0.25f;   // 후보만 넓게 (공간 해시) — 판정은 아래 정수 거리
        for (long d = 0; d <= r.Look; d += step)
        {
            long s = front + sgn * d;
            if (s < lo || s > hi) break;
            t.Track.PosFix(s, out long px, out long py, out _, out _);
            var p = new Vector2(px / (float)TrainTrack.Unit, py / (float)TrainTrack.Unit);
            foreach (var u in UnitsInRadius(p, query, t.buf))
            {
                if (!u.Alive) continue;
                long ux = (long)MathF.Floor(u.Pos.X * TrainTrack.Unit), uy = (long)MathF.Floor(u.Pos.Y * TrainTrack.Unit);   // ×2^16 은 float 에서 정확
                long reach = radFix + (long)MathF.Round(u.Def.Radius * TrainTrack.Unit);
                long ex = ux - px, ey = uy - py;
                if (ex * ex + ey * ey <= reach * reach) return true;
            }
        }
        return false;
    }

    void WriteTrains(SnapWriter w)
    {
        w.I(Trains.Count);
        foreach (var t in Trains)
        {
            w.I(t.Id); w.I(t.Owner); w.Str(t.Tag);
            var k = t.Track;
            w.Bool(k.Loop);
            w.I(k.TX.Length);
            for (int i = 0; i < k.TX.Length; i++) { w.I(k.TX[i]); w.I(k.TY[i]); }
            w.I(k.Waypoints.Length);
            for (int i = 0; i < k.Waypoints.Length; i++) { w.I(k.Waypoints[i]); w.I(t.WaitTicks[i]); }
            w.I(t.Cars.Count);
            foreach (var c in t.Cars) { w.I(c.Id); w.Str(c.Kind); w.I(c.Owner); w.F(c.Hp); w.L(c.LastDamagedTick); }
            w.L(t.S); w.I(t.V); w.Bool(t.Manual); w.I(t.ManualDir); w.I(t.Next); w.I(t.ScriptDir); w.I(t.WaitLeft); w.Bool(t.Blocked);
        }
    }

    void ReadTrains(SnapReader r)
    {
        var rules = TrainRules;
        Trains.Clear();
        int n = r.Count(20);
        for (int ti = 0; ti < n; ti++)
        {
            int id = r.I(), owner = r.I();
            string? tag = r.Str();
            bool loop = r.Bool();
            int nt = r.Count(2);
            var tx = new int[nt]; var ty = new int[nt];
            for (int i = 0; i < nt; i++) { tx[i] = r.I(); ty[i] = r.I(); }
            if (nt < 2) throw new SnapshotException(string.Format(SnapMsg.Invalid, "train track"));
            int nw = r.Count(2);
            var wp = new int[nw]; var waits = new int[nw];
            for (int i = 0; i < nw; i++) { wp[i] = r.I(); waits[i] = r.I(); if (wp[i] < 0 || wp[i] >= nt) throw new SnapshotException(string.Format(SnapMsg.Invalid, "train waypoint")); }
            var t = new Train(id, owner, tag, new TrainTrack(tx, ty, wp, loop), waits);
            int nc = r.Count(8);
            for (int i = 0; i < nc; i++)
            {
                int cid = r.I();
                string kind = r.Str() ?? "";
                if (!rules.Cars.TryGetValue(kind, out var def)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "train car " + kind));
                var c = new TrainCar(cid, r.I(), kind, def, i) { Hp = r.F(), LastDamagedTick = r.L() };
                t.Cars.Add(c);
            }
            t.S = r.L(); t.V = r.I(); t.Manual = r.Bool(); t.ManualDir = r.I(); t.Next = r.I(); t.ScriptDir = r.I(); t.WaitLeft = r.I(); t.Blocked = r.Bool();
            t.PlaceCars(rules);
            Trains.Add(t);
        }
    }

    partial void HashTrains(ref Hasher h)
    {
        if (Trains.Count == 0) return;   // 열차 없는 지도: 체크섬 불변 (골든 그대로)
        foreach (var t in Trains)
        {
            h.Add(t.Id); h.Add(t.Owner); h.Add(t.S); h.Add(t.V); h.Add(t.Manual ? 1 : 0); h.Add(t.ManualDir);
            h.Add(t.Next); h.Add(t.ScriptDir); h.Add(t.WaitLeft); h.Add(t.Blocked ? 1 : 0);
            h.Add(t.Tag ?? ""); foreach (var wt in t.WaitTicks) h.Add(wt);
            foreach (var c in t.Cars) { h.Add(c.Id); h.Add(c.Hp); h.Add(c.Kind); }
        }
    }
}
