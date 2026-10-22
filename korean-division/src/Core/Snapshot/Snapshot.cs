using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KoreanDivision.Core;

/// <summary>머리말의 세력 요약 (불러오기 목록 · 호환 확인용).</summary>
public sealed class SnapFactionInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Lang { get; set; } = "en";
    public int[]? Color { get; set; }
    public bool IsAi { get; set; }
    public bool Defeated { get; set; }
}

/// <summary>
/// 저장 파일 머리말 (압축하지 않은 JSON — 불러오기 목록이 본문을 읽지 않고도 보여줄 수 있다).
/// 본문에는 월드 전체 상태가 들어 있고 (World.WriteBody), 머리말에는 '이 저장이 무엇인가'만 있다.
/// </summary>
public sealed class SnapshotHeader
{
    public int Format { get; set; }
    public string GameVersion { get; set; } = "";
    public string BalanceHash { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Auto { get; set; }
    public string SavedAt { get; set; } = "";
    public long Tick { get; set; }
    public string Checksum { get; set; } = "";
    // 지도
    public string? MapId { get; set; }
    public string? MapHash { get; set; }
    public string? MapName { get; set; }
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }
    public int Seed { get; set; }
    // 설정
    public List<SnapFactionInfo> Factions { get; set; } = new();
    public int HumanFaction { get; set; }
    public bool Spectate { get; set; }
    public string Ai { get; set; } = "standard";
    public bool Fog { get; set; } = true;
    public BattleSetup? Battle { get; set; }
    /// <summary>미션 id (미션 게임이면). 불러오기 목록 표시용 — 미션 상태는 본문 MISN 구역.</summary>
    public string? Mission { get; set; }
    // 요약
    public int Units { get; set; }
    public int Buildings { get; set; }
    public bool CheatsUsed { get; set; }
    public int Winner { get; set; }
    public long BodyLength { get; set; }
}

/// <summary>저장 시 호출자(Game)가 정하는 값 — 월드가 모르는 것들.</summary>
public sealed record SnapshotMeta(string Name, int HumanFaction, bool Spectate, string Ai, bool Auto = false);

/// <summary>불러오기 결과.</summary>
public sealed class LoadedSnapshot
{
    public World World = null!;
    public SnapshotHeader Header = null!;
    /// <summary>경고 (밸런스 · 게임 버전이 저장 당시와 다름 등). 불러오기는 되지만 이후 진행이 원래와 달라질 수 있다.</summary>
    public readonly List<string> Warnings = new();
    /// <summary>파일 내용 (리플레이가 시작 지점으로 첨부할 수 있게 월드에도 붙어 있다).</summary>
    public byte[] FileBytes = Array.Empty<byte>();
}

/// <summary>
/// 저장 파일 (.kds) 형식:
/// <code>
/// "KDSV" | u16 형식버전 | u32 머리말 길이 | 머리말 JSON (UTF-8) | u32 본문 원본 길이 | u64 본문 FNV-1a | u32 압축 길이 | Brotli(본문)
/// </code>
/// 형식 버전이 다르면 거부한다. 게임 · 밸런스 버전이 다르면 경고만 하고 불러온다 (밸런스가 바뀌어 유닛 종류가 없어졌다면 거부).
/// 멀티플레이 대비: 스냅샷 = 접속한 클라이언트가 받을 상태 그대로 (명령 기록 없이도 이어 돌릴 수 있다).
/// </summary>
public static class Snapshot
{
    /// <summary>파일 구조가 바뀌면 올린다 (옛 저장은 불러올 수 없다 — 명확한 메시지와 함께 거부).</summary>
    public const int FormatVersion = 52;   // 52: comms 통신 · 난이도 (ORG_ 끝 WriteComms — 난이도 키 · 전달 중 명령, Comms.cs) · 51: 길목 통과 큐 (ORG_ 끝 중대마다 ChokeHold, Choke.cs, formation) · 50: arty3 곡사포 조정 사격 (유닛마다 AdjustShots · AdjustAim) + 중기관총 최후 저지선 (FireMode.FinalLine · FinalLinePoint) — ORG_ 사격 통제 뒤 유닛 목록 (FireControl.cs) · 49: assaultinf 돌격 접근 약진 (유닛마다 HopUntil/HopTo, ChargeHop.cs) · 48: pronemove 엎드림 이동 (유닛마다 ProneHoldSince/StandUntil/ForceCross) · 47: N68 기수 쓰러짐 Unit.BearerFellUntil (CMB2 Prone/Broken 뒤, units) · 46: 전술 AI 3B (AI 블랙보드 TacGroup RotateUntil · RotateOut, TacBattalion RotateTick · CalmSince · Plug*, TacFaction 3B 누계, 프로필 3B 키, ai3b) · 45: N32 참호 돌격 (ORG_ 끝 중대마다 TA 단계 · 목표 · 시각, TrenchAssault.cs) · 44: N79a 집결 지점 (ORG_ 끝 중대마다 HasRally · RallyPoint) + 포격 종류 (유닛마다 GroundKind · GroundRounds · GroundDir · GroundStart, Rally.cs · GroundFire.cs) · 43: N79b 항복 (CMB2 유닛마다 Surrendered · SuppHighTicks, Surrender.cs) · 42: F6 엄폐물 소품 (지도 PropSeeds + ORG_ 끝 소품 hp · 변종, Props.cs) · 41: 제압 재설계 2 엎드림 · 붕괴 (CMB2 유닛마다 Prone · BrokenUntil · BrokenAt · BreakTo, supp) · 40: F1 2차 중대 대형 유지 (ORG_ 접적 구역 중대마다 KeepFormation, ContactF2.cs) · 39: N73 사격 통제 (ORG_ 끝 유닛마다 FireMode, FireControl.cs) · 38: 기병 동시 근접 제한 (CMB2 유닛마다 MeleeVictims · MeleeLastId, N61) + 기병 하마 사격 (Dismounted · MountLeft · MountLock, N61) + F1 접적 · 구역 · 사격 위치 (ORG_ 끝, Contact.cs) · 36: 착검 (CMB2 유닛마다 BayonetFixed · FixLeft, N57 charge) · 35: 후퇴 뒤 재정비 (전술 AI 블랙보드 TacGroup RegroupUntil · RegroupCheck · RegroupAlive, pursuit2) · 34: 중대 자세 (ORG_ 끝 중대마다 Posture) + 정찰병 노출 기억 (ORG_ 정찰병마다 ScoutExposed · ScoutThreat · ScoutExposedTick, tweak1) · 33: 도하 · 역습 AI 상태 (전술 AI 스냅샷 — TacGroup CrossStage · CrossRegiment · CrossLane · CrossPoint · CounterAssaultTick, TacRegiment Cross* · CounterStage, TacFaction 누계; crossing) · 32: 고정 (엎드림 — CMB2 유닛마다 Pinned, 제압 재설계 N25) + 전술 AI 후퇴 원인 · 제압 후퇴 수 (AI 블랙보드) (supp N39) · 31: 전술 AI 3단계 A — 기동 계획 · 제대 · 철수 · 미션 철수 문턱 (AI 블랙보드 TacRegiment · TacGroup · TacDirective · TacFaction, 프로필 strategy 키, s3a) · 30: 엄폐 찾기 · 진지 점유 (SUPL 참호 구역 유닛마다 CoverHold, engage) · 29: 후퇴 중 (CMB2 유닛마다 Retreating, pursuit) + 전술 AI 추격 · 기병 상태 (AI 블랙보드) · 28: 대열 정비 2 (ORG_ 끝 중대마다 깃발 Close 표시, gapfill2) · 27: 대열 정비 (ORG_ 끝 — 중대마다 CloseAlarm · CloseAt · CloseUntil, gapfill) · 26: 전술 틀 2단계 (ORG_ 전술 틀 구역 중대마다 ActiveTactic + 행동 상태 DrillState, N4) · 25: 기병 돌격 쿨다운 (CMB2 유닛마다 ChargeCdLeft, 돌격 충격 제압 burst) · 24: 경기관총 점사 (CMB2 유닛마다 BurstLeft) · 23: 장교 직책 계급 · 임시 진급 · 경험치 (CMB2 유닛마다 OfficerBase · TempRank · OfficerXp) + 지휘관 · 인계 (ORG_ 끝 — 대대 · 중대 Commander · HandoverUntil) · 22: 전투 대형 · 전술 틀 (ORG_ 끝 — 중대 Tactic · 구성원 Role · 분대 Form) · 21: 플레이어 지점 포격 (UnitOrder.GroundFire, CMB2 유닛마다 GroundPoint) · 20: 중대 돌격 사격 허용 MarchFireOn (ORG_ 중대 MarchUntil 뒤) · 19: 충돌 · 대형 재설계 (ORG_ 깃발 속도 배수 SpeedMul, 중대 · 분대 행진 사격 지휘 MarchUntil) · 18: 건설 제거 (SUPL 의 개인 참호 · 참호선 작업 상태 삭제) + 참호 자동 점유 (SUPL 끝: 유닛별 참호 자리 · 사수 · 무시 묶음) · 17: 정찰병 (ORG_ 끝 — 정찰조 · 자율 상태, Unit.ScoutAuto 등) · 16: 날아가는 포탄 (CMB2 끝, ShellFlight) · 15: 자주포 · 견인포 · 중형 전차 · 광산 · 유전 정의 삭제 (옛 저장 호환 안 됨) · 14: 미션 (MISN 구역 — 미션 정의 JSON · 목표 · 사건 · 증원 · 배치, WTHR 뒤) · 13: 참호선 (작업 · 칸 진행량 · 유닛 작업, SUPL 끝) + 포탄 구덩이 지형 · 12: 날씨 · 시간대 (WTHR, ORG_ 뒤) · 11: 분대 (ORG_ 중대마다 Holes + 분대 목록, nextSquadId) · 장교 칼 흔들기 틱 (CMB2 뒤쪽) · 10: 지형 · 높이 (지형 종류 5개 추가, 지도 고지 배열, 안개 = 세력별 자기 시야 격자 · 시차 갱신) · 9: 외교 삭제 → 정적 팀 (DiplomacySnapshot · StartedAtWar 제거, Faction.Team 추가) · 8: 교전 4차 (수류탄 · 돌격 충격 · 돌격만 · 행진 사격 표적, CMB2 뒤쪽 + 날아가는 수류탄) · 유닛 id 이름 규칙 (machinegunner -> heavy_machinegunner 등) · 2: v0.4 확장 구역 틀 · 3: SUPL · CMB2 · CMDX 내용 + 낡은 NavToken 엔티티 · 4: 교전 2차 (제압 · 사살 · 돌격 · 멍칫, CMB2 뒤쪽) · 5: 스태미나 · 속보 (피로 · QuickStep) + 정찰 제거 (UnitOrder · PatrolBack 삭제) · 6: 편제 ORG_ (중대 · 대대 · 깃발) · 7: 방향 변경 (자원 · 생산 · 건설 · 공병 · 연료 · 포탑 삭제, SUPL = 탄약 회복 · 보급 마차 · 엄폐 · 참호, 지도 유닛 배치)
    public const string Extension = ".kds";
    static readonly byte[] Magic = { (byte)'K', (byte)'D', (byte)'S', (byte)'V' };
    const int BrotliQuality = 4, BrotliWindow = 22;

    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    // ---------------------------------------------------------------- 저장

    public static byte[] Save(World world, SnapshotMeta meta)
    {
        var body = new SnapWriter(1 << 20);
        world.WriteBody(body);
        var raw = body.Buffer;

        var h = new SnapshotHeader
        {
            Format = FormatVersion,
            GameVersion = Config.Version,
            BalanceHash = world.Defs.Hash,
            Name = meta.Name,
            Auto = meta.Auto,
            SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Tick = world.Tick,
            Checksum = world.ComputeChecksum().ToString("X16"),
            MapId = world.Map.Id,
            MapHash = world.Map.FileHash,
            MapName = world.Map.DisplayName,
            MapWidth = world.Map.Width,
            MapHeight = world.Map.Height,
            Seed = world.Map.Seed,
            HumanFaction = meta.HumanFaction,
            Spectate = meta.Spectate,
            Ai = meta.Ai,
            Fog = world.Fog.Enabled,
            Battle = world.Battle,
            Mission = world.Mission?.Def.Id,
            Units = world.Units.Count,
            Buildings = world.Buildings.Count,
            CheatsUsed = world.CheatsUsed,
            Winner = world.Winner,
            BodyLength = raw.Length,
        };
        var colors = MapFile.FactionsOf(world.Map);
        for (int i = 1; i < world.Factions.Count; i++)
        {
            var f = world.Factions[i];
            h.Factions.Add(new SnapFactionInfo
            {
                Id = i, Name = f.Name, Lang = f.Lang, IsAi = f.IsAi, Defeated = f.Defeated,
                Color = i - 1 < colors.Count ? colors[i - 1].Color : null,
            });
        }
        byte[] head = JsonSerializer.SerializeToUtf8Bytes(h, JsonOpts);

        var packed = Compress(raw.Span);
        ulong sum = Fnv(raw.Span);

        using var ms = new MemoryStream(head.Length + packed.Length + 64);
        ms.Write(Magic);
        Span<byte> tmp = stackalloc byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(tmp, (ushort)FormatVersion); ms.Write(tmp[..2]);
        BinaryPrimitives.WriteUInt32LittleEndian(tmp, (uint)head.Length); ms.Write(tmp[..4]);
        ms.Write(head);
        BinaryPrimitives.WriteUInt32LittleEndian(tmp, (uint)raw.Length); ms.Write(tmp[..4]);
        BinaryPrimitives.WriteUInt64LittleEndian(tmp, sum); ms.Write(tmp);
        BinaryPrimitives.WriteUInt32LittleEndian(tmp, (uint)packed.Length); ms.Write(tmp[..4]);
        ms.Write(packed);
        return ms.ToArray();
    }

    /// <summary>임시 파일에 쓴 뒤 바꿔치기 (쓰다 중단돼도 기존 저장이 깨지지 않게).</summary>
    public static void SaveToFile(World world, SnapshotMeta meta, string path)
    {
        var bytes = Save(world, meta);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, overwrite: true);
    }

    static byte[] Compress(ReadOnlySpan<byte> raw)
    {
        var buf = new byte[System.IO.Compression.BrotliEncoder.GetMaxCompressedLength(raw.Length)];
        if (!BrotliEncoder.TryCompress(raw, buf, out int written, BrotliQuality, BrotliWindow))
            throw new IOException("압축 실패");
        return buf.AsSpan(0, written).ToArray();
    }

    static ulong Fnv(ReadOnlySpan<byte> data)
    {
        ulong h = 14695981039346656037UL;
        foreach (byte b in data) { h ^= b; h *= 1099511628211UL; }
        return h;
    }

    // ---------------------------------------------------------------- 읽기

    /// <summary>머리말만 읽는다 (불러오기 목록용 — 본문은 건드리지 않는다). 깨졌거나 형식이 다르면 SnapshotException.</summary>
    public static SnapshotHeader ReadHeader(byte[] file) => ReadHeader(file, out _);

    static SnapshotHeader ReadHeader(byte[] file, out int bodyStart)
    {
        bodyStart = 0;
        if (file.Length < 10 || !file.AsSpan(0, 4).SequenceEqual(Magic)) throw new SnapshotException(SnapMsg.NotSave);
        int fmt = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(4, 2));
        if (fmt != FormatVersion)
            throw new SnapshotException(string.Format(fmt < FormatVersion ? SnapMsg.OldFormat : SnapMsg.NewFormat, fmt, FormatVersion))
            { Note = fmt < FormatVersion ? SnapMsg.OldFormatNote : SnapMsg.NewFormatNote };
        uint hl = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(6, 4));
        if (hl > file.Length - 10 || hl > 1 << 22) throw new SnapshotException(SnapMsg.BadHeader);
        SnapshotHeader? h;
        try { h = JsonSerializer.Deserialize<SnapshotHeader>(file.AsSpan(10, (int)hl), JsonOpts); }
        catch (JsonException e) { throw new SnapshotException(SnapMsg.BadHeader, e); }
        if (h == null || h.Format != fmt) throw new SnapshotException(SnapMsg.BadHeader);
        bodyStart = 10 + (int)hl;
        return h;
    }

    public static SnapshotHeader ReadHeaderFile(string path)
    {
        // 머리말만 필요하니 파일 앞부분만 읽는다 (큰 저장도 목록은 빠르게)
        using var fs = File.OpenRead(path);
        var pre = new byte[10];
        int n = fs.Read(pre, 0, 10);
        if (n < 10 || !pre.AsSpan(0, 4).SequenceEqual(Magic)) throw new SnapshotException(SnapMsg.NotSave);
        uint hl = BinaryPrimitives.ReadUInt32LittleEndian(pre.AsSpan(6, 4));
        if (hl > 1 << 22) throw new SnapshotException(SnapMsg.BadHeader);
        var file = new byte[10 + hl];
        Array.Copy(pre, file, 10);
        int got = fs.Read(file, 10, (int)hl);
        if (got < hl) throw new SnapshotException(SnapMsg.Truncated);
        var h = ReadHeader(file);
        // 본문까지 읽지 않고도 잘린 파일을 알아본다: 머리말 뒤의 길이 칸이 가리키는 압축 본문 길이와 파일 길이가 맞아야 한다
        var tail = new byte[16];
        if (fs.Read(tail, 0, 16) < 16) throw new SnapshotException(SnapMsg.Truncated);
        long packed = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(12, 4));
        long expect = 10 + hl + 16 + packed;
        if (fs.Length < expect) throw new SnapshotException(SnapMsg.Truncated);
        if (fs.Length > expect) throw new SnapshotException(string.Format(SnapMsg.Invalid, "file length"));
        return h;
    }



    /// <summary>[테스트] 본문만 바꿔 다시 포장 (압축 · 길이 · 검사값을 새로 맞춤) — 압축 해제 뒤의 파서가 깨진 본문을 견디는지 확인하는 퍼즈용.</summary>
    internal static byte[] Rebuild(byte[] file, byte[] rawBody)
    {
        ReadHeader(file, out int p);
        var packed = Compress(rawBody);
        using var ms = new MemoryStream();
        ms.Write(file.AsSpan(0, p));
        Span<byte> tmp = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(tmp, (uint)rawBody.Length); ms.Write(tmp[..4]);
        BinaryPrimitives.WriteUInt64LittleEndian(tmp, Fnv(rawBody)); ms.Write(tmp);
        BinaryPrimitives.WriteUInt32LittleEndian(tmp, (uint)packed.Length); ms.Write(tmp[..4]);
        ms.Write(packed);
        return ms.ToArray();
    }
    /// <summary>[테스트 · 도구] 압축을 푼 본문 — 머리말(저장 시각 등)을 빼고 상태만 바이트 단위로 비교할 때 쓴다.</summary>
    public static byte[] RawBody(byte[] file)
    {
        ReadHeader(file, out int p);
        uint rawLen = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(p, 4));
        uint packedLen = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(p + 12, 4));
        var raw = new byte[rawLen];
        if (!BrotliDecoder.TryDecompress(file.AsSpan(p + 16, (int)packedLen), raw, out int got) || got != rawLen) throw new SnapshotException(SnapMsg.BadPack);
        return raw;
    }
    public static LoadedSnapshot LoadFile(string path, Defs defs)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new SnapshotException(string.Format(SnapMsg.CantRead, e.Message), e); }
        return Load(bytes, defs);
    }

    /// <summary>파일 내용 → 월드. 어떤 오류(깨짐 · 잘림 · 형식 · 밸런스 불일치)든 SnapshotException 하나로 돌려준다 — 게임이 죽지 않는다.</summary>
    public static LoadedSnapshot Load(byte[] file, Defs defs)
    {
        try
        {
            var h = ReadHeader(file, out int p);
            if (file.Length < p + 16) throw new SnapshotException(SnapMsg.Truncated);
            uint rawLen = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(p, 4));
            ulong sum = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(p + 4, 8));
            uint packedLen = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(p + 12, 4));
            p += 16;
            if (packedLen > file.Length - p) throw new SnapshotException(SnapMsg.Truncated);
            if (rawLen > 1 << 30) throw new SnapshotException(string.Format(SnapMsg.Invalid, "body length"));
            var raw = new byte[rawLen];
            if (!BrotliDecoder.TryDecompress(file.AsSpan(p, (int)packedLen), raw, out int got) || got != rawLen)
                throw new SnapshotException(SnapMsg.BadPack);
            if (Fnv(raw) != sum) throw new SnapshotException(SnapMsg.BadSum);

            var res = new LoadedSnapshot { Header = h, FileBytes = file };
            if (h.GameVersion != Config.Version)
                res.Warnings.Add(string.Format(SnapMsg.WarnVersion, h.GameVersion, Config.Version));
            if (h.BalanceHash != defs.Hash)
                res.Warnings.Add(SnapMsg.WarnBalance);

            var r = new SnapReader(raw);
            var world = World.ReadBody(r, defs);
            if (!r.AtEnd) throw new SnapshotException(SnapMsg.Trailing);
            world.OriginSnapshot = file;
            world.OriginTick = world.Tick;
            // 무결성: 불러온 상태의 체크섬이 저장 시점과 같아야 한다 (체크섬은 밸런스 값에 의존하지 않는다)
            string now = world.ComputeChecksum().ToString("X16");
            if (now != h.Checksum) throw new SnapshotException(string.Format(SnapMsg.BadChecksum, now, h.Checksum));
            res.World = world;
            return res;
        }
        catch (SnapshotException) { throw; }
        catch (Exception e) when (e is InvalidDataException or IOException or InvalidOperationException or ArgumentException or KeyNotFoundException
                                      or FormatException or NullReferenceException or IndexOutOfRangeException or OverflowException or OutOfMemoryException
                                      or System.Reflection.TargetInvocationException or InvalidCastException or EndOfStreamException or JsonException)
        {
            throw new SnapshotException(string.Format(SnapMsg.ParseError, e.GetType().Name, e.Message), e);
        }
    }
}
