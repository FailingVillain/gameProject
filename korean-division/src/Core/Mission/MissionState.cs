using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>배치 단계의 최종 위치 (병력 id → 중심 · 방향). 시작 상태의 일부 — 저장 · 리플레이 머리말에 기록되어 그대로 재현된다.</summary>
public sealed record DeployEntry(string Force, Vector2 At, float Facing);

/// <summary>
/// 미션 실행 상태 (시뮬레이션 상태 — 스냅샷 MISN 구역 · 체크섬 포함). 정의(MissionDef)는 JSON 텍스트로 함께 저장된다.
/// 목표 진행 · 발동한 사건 · 증원 · 태그 유닛 · 손실 집계가 모두 여기 있다.
/// </summary>
public sealed class MissionState
{
    public const int Running = 0, Won = 1, Lost = 2;
    public const int Active = 0, Done = 1, Failed = 2;

    public MissionDef Def { get; internal set; }
    /// <summary>첫 틱이 돌았는가 (false = 배치 단계: 플레이어가 부대를 옮길 수 있다).</summary>
    public bool Started { get; internal set; }
    /// <summary>배치 단계에서 옮긴 병력 (병력 id 당 하나, 마지막 값).</summary>
    public readonly List<DeployEntry> Deployment = new();
    /// <summary>병력 id → 그 병력으로 생긴 유닛 Id (생성 순).</summary>
    public readonly SortedDictionary<string, List<int>> ForceUnits = new(StringComparer.Ordinal);
    /// <summary>태그 → 유닛 Id.</summary>
    public readonly SortedDictionary<string, List<int>> Tags = new(StringComparer.Ordinal);
    /// <summary>유닛 Id → 시작 계급 (승진 집계용).</summary>
    public readonly SortedDictionary<int, int> StartRank = new();
    public int[] ObjState = Array.Empty<int>();
    public float[] ObjProgress = Array.Empty<float>();
    public bool[] ObjRevealed = Array.Empty<bool>();
    public bool[] EventFired = Array.Empty<bool>();
    public bool[] ReinforcementSpawned = Array.Empty<bool>();
    /// <summary>TC-B: 증원별 예약 도착 틱 (0 = 예약 없음). spawn 행동의 delay 초 뒤.</summary>
    public long[] ReinforcementDue = Array.Empty<long>();
    /// <summary>TC-B: 병력(Def.Forces 순서)별 순찰 다음 경유점 번호.</summary>
    public int[] PatrolLeg = Array.Empty<int>();
    /// <summary>세력별 지금까지 생긴 유닛 수 (손실 = 생긴 수 − 살아 있는 수).</summary>
    public int[] Spawned = Array.Empty<int>();
    /// <summary>사건 메시지 기록 (틱, 글). HUD 가 최근 것을 띄운다.</summary>
    public readonly List<(long Tick, string Text)> Messages = new();
    public string? Weather { get; internal set; }
    public string? TimeOfDay { get; internal set; }
    public int Result { get; internal set; }
    public long EndTick { get; internal set; }
    /// <summary>실패 사유 (패배 조건 인덱스, -1 = 목표 실패 / 없음).</summary>
    public int FailReason { get; internal set; } = -1;
    /// <summary>[작업 버퍼 — 저장 안 함] 세력별 살아 있는 유닛 수 (UpdateMission 이 매 틱 다시 센다).</summary>
    internal int[] Alive = Array.Empty<int>();
    /// <summary>[작업 버퍼 — 저장 안 함] T40: 세력별 살아 있는 열차 객차 수 / 전체 객차 수(잔해 포함). 전멸 · 섬멸 판정에만 쓴다 (손실 % 는 유닛만).</summary>
    internal int[] AliveCars = Array.Empty<int>(), TotalCars = Array.Empty<int>();

    public MissionState(MissionDef def)
    {
        Def = def;
        ObjState = new int[def.Objectives.Count];
        ObjProgress = new float[def.Objectives.Count];
        ObjRevealed = def.Objectives.Select(o => !o.Hidden).ToArray();
        EventFired = new bool[def.Events.Count];
        ReinforcementSpawned = new bool[def.Reinforcements.Count];
        ReinforcementDue = new long[def.Reinforcements.Count];
        PatrolLeg = new int[def.Forces.Count];
        Spawned = new int[def.FactionCount + 1];
        Weather = def.Weather;
        TimeOfDay = def.Time;
    }

    public int Player => Def.Player;

    public IReadOnlyList<int> TagUnits(string? tag) => tag != null && Tags.TryGetValue(tag, out var l) ? l : Array.Empty<int>();

    // ---------------------------------------------------------------- 저장 (MISN 구역)

    internal void Write(SnapWriter w)
    {
        w.Str(Def.Json); w.Str(Def.Id);
        w.Bool(Started);
        w.I(Deployment.Count);
        foreach (var d in Deployment) { w.Str(d.Force); w.Vec(d.At); w.F(d.Facing); }
        WriteMap(w, ForceUnits); WriteMap(w, Tags);
        w.I(StartRank.Count);
        foreach (var (k, v) in StartRank) { w.I(k); w.I(v); }
        w.I(ObjState.Length);
        for (int i = 0; i < ObjState.Length; i++) { w.I(ObjState[i]); w.F(ObjProgress[i]); w.Bool(ObjRevealed[i]); }
        w.I(EventFired.Length); foreach (var b in EventFired) w.Bool(b);
        w.I(ReinforcementSpawned.Length); foreach (var b in ReinforcementSpawned) w.Bool(b);
        w.I(Spawned.Length); foreach (var n in Spawned) w.I(n);
        w.I(Messages.Count);
        foreach (var (t, s) in Messages) { w.L(t); w.Str(s); }
        w.Str(Weather); w.Str(TimeOfDay);
        w.I(Result); w.L(EndTick); w.I(FailReason);
        w.I(ReinforcementDue.Length); foreach (long d in ReinforcementDue) w.L(d);   // TC-B (FV 67)
        w.I(PatrolLeg.Length); foreach (int p in PatrolLeg) w.I(p);
    }

    static void WriteMap(SnapWriter w, SortedDictionary<string, List<int>> m)
    {
        w.I(m.Count);
        foreach (var (k, l) in m) { w.Str(k); w.I(l.Count); foreach (int x in l) w.I(x); }
    }

    static void ReadMap(SnapReader r, SortedDictionary<string, List<int>> m)
    {
        m.Clear();
        int n = r.Count(2);
        for (int i = 0; i < n; i++)
        {
            string k = r.Str() ?? "";
            int c = r.Count();
            var l = new List<int>(c);
            for (int j = 0; j < c; j++) l.Add(r.I());
            m[k] = l;
        }
    }

    internal static MissionState Read(SnapReader r)
    {
        string json = r.Str() ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission"));
        string id = r.Str() ?? "";
        MissionDef def;
        try { def = MissionDef.Parse(json, id); }
        catch (InvalidDataException e) { throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission: " + e.Message), e); }
        var s = new MissionState(def);
        s.Started = r.Bool();
        int nd = r.Count(3);
        for (int i = 0; i < nd; i++) s.Deployment.Add(new DeployEntry(r.Str() ?? "", r.Vec(), r.F()));
        ReadMap(r, s.ForceUnits); ReadMap(r, s.Tags);
        int nr = r.Count(2);
        for (int i = 0; i < nr; i++) { int k = r.I(); s.StartRank[k] = r.I(); }
        int no = r.Count(3);
        if (no != s.ObjState.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission objectives"));
        for (int i = 0; i < no; i++) { s.ObjState[i] = r.I(); s.ObjProgress[i] = r.F(); s.ObjRevealed[i] = r.Bool(); }
        int ne = r.Count();
        if (ne != s.EventFired.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission events"));
        for (int i = 0; i < ne; i++) s.EventFired[i] = r.Bool();
        int nre = r.Count();
        if (nre != s.ReinforcementSpawned.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission reinforcements"));
        for (int i = 0; i < nre; i++) s.ReinforcementSpawned[i] = r.Bool();
        int ns = r.Count();
        if (ns != s.Spawned.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission factions"));
        for (int i = 0; i < ns; i++) s.Spawned[i] = r.I();
        int nm = r.Count(2);
        for (int i = 0; i < nm; i++) s.Messages.Add((r.L(), r.Str() ?? ""));
        s.Weather = r.Str(); s.TimeOfDay = r.Str();
        s.Result = r.I(); s.EndTick = r.L(); s.FailReason = r.I();
        int nd2 = r.Count();
        if (nd2 != s.ReinforcementDue.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission reinforcement due"));
        for (int i = 0; i < nd2; i++) s.ReinforcementDue[i] = r.L();
        int np = r.Count();
        if (np != s.PatrolLeg.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "mission patrol"));
        for (int i = 0; i < np; i++) s.PatrolLeg[i] = Math.Max(0, r.I());
        return s;
    }
}
