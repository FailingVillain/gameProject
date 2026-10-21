using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>형식 번호가 현재와 다른 리플레이 (저장 호환 정책: 열지 않고 안내).</summary>
public static class ReplayVersionException
{
    public const string Key = "versionMismatch";
    public static InvalidDataException Make(string m) { var e = new InvalidDataException(m); e.Data[Key] = true; return e; }
    public static bool Is(Exception? e) => e?.Data[Key] is true;
}

/// <summary>체크섬 기록 한 줄.</summary>
public readonly record struct ChecksumRecord(long Tick, ulong Checksum);

/// <summary>
/// 리플레이 파일: 시작 조건(맵 · 시드 · 세력 수 · 밸런스 해시) + 실행된 명령 기록 + 주기적 체크섬.
/// 같은 조건으로 월드를 만들고 명령을 같은 틱에 다시 넣으면 게임이 똑같이 재현된다 (결정적 시뮬레이션).
/// </summary>
public sealed class ReplayFile
{
    public const int FormatVersion = 19;   // 19: comms 대대장 앞 명령 전달 (dispatch) + 머리말 Difficulty · 18: arty3 최후 저지선 명령 (finalline) · 17: 참호 돌격 명령 (trenchassault, N32) · 16: 집결 지점 · 포격 종류 명령 (rally · groundfireex · retreat UseRally, N79a) · 15: 중대 대형 유지 명령 (keepform, F1 2차) · 14: 기병 하마/승마 명령 (mount, N61) · 13: 중대 자세 명령 (posture, tweak1) · 12: 명령 선택 인원 Sel (N37, 플레이어 명령 지연 곡선) · 11: 진지 점유 명령 (occupy, engage) · 10: 대열 정비 명령 (closeranks, gapfill) · 9: 돌격 사격 켜기 명령 (marchfire) · 8: 참호 파기 · 참호선 명령 삭제 (dig · digline — 건설 제거 2026-10-19) · 7: 미션 (머리말 Mission = 미션 JSON + 배치 단계 결과, MissionEndCommand) · 6: 연대 → 대대 이름 변경 (battalion · batnumber · batform 명령) · 5: 외교 명령 삭제 (정적 팀) · 4: 교전 4차 (수류탄 · 돌격만 명령, 유닛 id 이름 규칙 변경) · 3: 방향 변경 (생산 · 건설 · 자원 명령 삭제, 참호 파기 추가)
    public const int ChecksumInterval = 600;   // 10초마다 체크섬 기록

    public int Format { get; set; } = FormatVersion;
    public string GameVersion { get; set; } = Config.Version;
    public string? MapId { get; set; }          // 고정 지도 파일 (없으면 생성 맵)
    public string? MapHash { get; set; }
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }
    public int Seed { get; set; }
    public int FactionCount { get; set; }
    /// <summary>사람이 조작한 세력 (0 = 없음 · 관전 · 오래된 파일). 재생 화면의 "(플레이어)" 표시용 — 시뮬레이션에는 영향 없음.</summary>
    public int HumanFaction { get; set; }
    public bool CheatsUsed { get; set; }
    /// <summary>교전 테스트 설정 (있으면 이 설정으로 월드를 만든다 — 건물 · 경제 없음).</summary>
    public BattleSetup? Battle { get; set; }
    /// <summary>미션 설정 (있으면 미션 정의 + 배치 결과로 월드를 만든다).</summary>
    public MissionReplayInfo? Mission { get; set; }
    public bool FogEnabled { get; set; } = true;
    /// <summary>틱 0 의 날씨 · 시간대 (--weather/--time 덮어쓰기 재현용; 일정은 지도 파일에서). 없으면 지도 설정 그대로.</summary>
    public string? Weather { get; set; }
    /// <summary>난이도 키 (difficulty.json, Comms.cs). null = 기본.</summary>
    public string? Difficulty { get; set; }
    public string? TimeOfDay { get; set; }   // 안개 설정도 시뮬레이션에 영향 (표적 획득 · 건설 가능 위치)
    public string BalanceHash { get; set; } = "";
    public long EndTick { get; set; }
    public ulong EndChecksum { get; set; }
    /// <summary>이 리플레이가 저장 파일(스냅샷) 시점에서 시작하면 그 파일 내용 (Base64). 없으면 틱 0 부터 (지도 · 시드 · 세력 수로 처음부터 재현).
    /// 긴 게임을 저장 지점부터 다시 볼 수 있다 — 명령 기록은 StartTick 이후 것만 담는다.</summary>
    public string? StartSnapshot { get; set; }
    public long StartTick { get; set; }
    public List<CommandRecord> Commands { get; set; } = new();
    public List<ChecksumRecord> Checksums { get; set; } = new();

    public static ReplayFile FromWorld(World w, int humanFaction = 0) => new()
    {
        HumanFaction = humanFaction,
        MapId = w.Map.Id,
        MapHash = w.Map.FileHash,
        MapWidth = w.Map.Width,
        MapHeight = w.Map.Height,
        Seed = w.Map.Seed,
        FactionCount = w.Factions.Count - 1,
        CheatsUsed = w.CheatsUsed,
        Battle = w.Battle,
        Mission = MissionReplayInfo.From(w),
        FogEnabled = w.Fog.Enabled,
        Weather = WeatherState.KeyOf(w.Weather.StartKind),
        TimeOfDay = WeatherState.KeyOf(w.Weather.StartTime),
        Difficulty = w.Difficulty,
        BalanceHash = w.Defs.Hash,
        EndTick = w.Tick,
        EndChecksum = w.ComputeChecksum(),
        Commands = new List<CommandRecord>(w.CommandLog),
        Checksums = w.OriginSnapshot == null ? new List<ChecksumRecord>(w.ChecksumLog) : w.ChecksumLog.Where(c => c.Tick > w.OriginTick).ToList(),
        StartSnapshot = w.OriginSnapshot == null ? null : Convert.ToBase64String(w.OriginSnapshot),
        StartTick = w.OriginSnapshot == null ? 0 : w.OriginTick,
    };

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        JsonSerializer.Serialize(fs, this, CommandJson.Options);
    }

    /// <summary>읽을 수 없는 리플레이 (옛/새 형식 · 손상). 메시지는 사용자에게 그대로 보인다.</summary>
    public static ReplayFile Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        try
        {
            // 형식 번호부터 본다: 옛 형식은 명령 종류가 달라 전체 파싱이 먼저 실패할 수 있다
            using (var doc = JsonDocument.Parse(bytes))
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException(SnapMsg.ReplayBroken);
                foreach (var p in doc.RootElement.EnumerateObject())
                    if (string.Equals(p.Name, nameof(Format), StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int fmt) && fmt != FormatVersion)
                        throw ReplayVersionException.Make(string.Format(fmt < FormatVersion ? SnapMsg.ReplayOldFormat : SnapMsg.ReplayNewFormat, fmt, FormatVersion));
            }
            var r = JsonSerializer.Deserialize<ReplayFile>(bytes, CommandJson.Options) ?? throw new InvalidDataException(SnapMsg.ReplayBroken);
            if (r.Format != FormatVersion) throw ReplayVersionException.Make(string.Format(r.Format < FormatVersion ? SnapMsg.ReplayOldFormat : SnapMsg.ReplayNewFormat, r.Format, FormatVersion));
            return r;
        }
        catch (JsonException e) { throw new InvalidDataException(SnapMsg.ReplayBroken, e); }
    }

    /// <summary>재생용 월드: 같은 시작 조건 + 기록된 명령을 틱마다 공급. AI 는 붙이지 않는다 (AI 의 명령도 기록에 있음).</summary>
    public World CreateWorld(Defs defs)
    {
        World world;
        if (StartSnapshot != null)
        {
            // 저장 지점에서 시작: 스냅샷을 불러오고, AI 는 떼고 (AI 가 낸 명령은 기록에 있다), 대기 중이던 명령도 버린다 (실행된 순간 기록에 남았다)
            try { world = Snapshot.Load(Convert.FromBase64String(StartSnapshot), defs).World; }
            catch (Exception e) when (e is SnapshotException or FormatException) { throw new InvalidDataException(string.Format(SnapMsg.ReplayStartBroken, e.Message), e); }
            world.Controllers.Clear();
            world.ClearPendingCommands();
        }
        else if (Battle != null) world = Battle.CreateWorld(defs, Seed);
        else if (Mission != null) world = Mission.CreateWorld(defs, Seed);   // 미션: 정의 JSON + 배치 결과 (Mission/MissionReplay.cs)   // 교전 테스트: 설정에서 같은 배치를 다시 만든다
        else
        {
            var map = MapId != null ? MapFile.Load(MapId, Seed) : GameMap.Generate(MapWidth, MapHeight, Seed, FactionCount);
            world = World.CreateStandard(map, defs);
            MapFile.ApplyFactions(world);   // 세력 이름은 지도 파일에서 (기록에 따로 저장하지 않음)
        }
        if (StartSnapshot == null && (Weather != null || TimeOfDay != null))
        {
            var k = Weather != null ? WeatherState.ParseKind(Weather) : world.Weather.Kind;
            var t = TimeOfDay != null ? WeatherState.ParseTime(TimeOfDay) : world.Weather.Time;
            if (k != world.Weather.Kind || t != world.Weather.Time) world.SetupWeather(k, t);
        }
        if (StartSnapshot == null && Difficulty != null) world.Difficulty = Difficulty;   // 난이도 (Comms.cs — 스냅샷 시작이면 스냅샷 값)
        world.Fog.Enabled = FogEnabled;
        world.RecordCommands = false;
        int next = 0;
        var batch = new List<GameCommand>();
        world.CommandFeed = tick =>
        {
            batch.Clear();
            while (next < Commands.Count && Commands[next].Tick < tick) next++;
            while (next < Commands.Count && Commands[next].Tick == tick) batch.Add(Commands[next++].Command);
            return batch;
        };
        return world;
    }

    /// <summary>
    /// 처음부터 다시 돌려서 기록된 체크섬과 비교. 어긋나면 처음 어긋난 틱을 돌려준다 (-1 = 모두 일치).
    /// </summary>
    public long Verify(Defs defs, out string report)
    {
        var world = CreateWorld(defs);
        int ci = 0;
        while (world.Tick < EndTick)
        {
            world.Step();
            while (ci < Checksums.Count && Checksums[ci].Tick < world.Tick) ci++;
            if (ci < Checksums.Count && Checksums[ci].Tick == world.Tick)
            {
                ulong got = world.ComputeChecksum();
                if (got != Checksums[ci].Checksum)
                {
                    report = $"틱 {world.Tick} ({world.Tick / 60f:0.0}초)에서 어긋남: 기록 {Checksums[ci].Checksum:X16} ≠ 재생 {got:X16}";
                    return world.Tick;
                }
                ci++;
            }
        }
        ulong end = world.ComputeChecksum();
        if (end != EndChecksum)
        {
            report = $"마지막 틱 {EndTick} 체크섬 불일치: 기록 {EndChecksum:X16} ≠ 재생 {end:X16}";
            return EndTick;
        }
        report = $"일치: {EndTick / 60f / 60f:0.0}분, 명령 {Commands.Count}개, 체크섬 {Checksums.Count + 1}곳" +
                 (BalanceHash != defs.Hash ? " (주의: 밸런스 파일이 기록 당시와 다름)" : "") +
                 (MapId != null && MapHash != world.Map.FileHash ? " (주의: 지도 파일이 기록 당시와 다름)" : "");
        return -1;
    }
}
