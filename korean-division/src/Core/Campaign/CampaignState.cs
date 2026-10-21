using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 캠페인 상태 (최소판): 끝낸 미션 · 다음 미션 · 이월 병력 (생존 사병 계급 · 사살 수) · 장교 (정식 계급 · 경험치 · 직책).
/// 미션 결과 (MissionResults) 에서 만들고 %LOCALAPPDATA%/KoreanDivision/campaign/campaign.json 에 저장한다
/// (환경변수 KD_CAMPAIGN_DIR 로 폴더 변경). 표시 · 이월 전용 — 시뮬레이션 상태와 무관 (스냅샷 · 체크섬 영향 없음).
/// 다음 미션이 병력을 읽는 것은 아직 없음 (Carry 가 로더 자리).
/// </summary>
public sealed class CampaignState
{
    public sealed class MissionRecord { public string MissionId { get; set; } = ""; public string Title { get; set; } = ""; public float DurationSeconds { get; set; } public int EnemyKills { get; set; } public int Lost { get; set; } public int Survivors { get; set; } public string SavedAt { get; set; } = ""; }

    /// <summary>캠페인 저장 스키마 버전. 규칙 (ENGINE_NOTES 저장 호환): 필드는 추가만 한다 — 새 필드는 C# 기본값이 곧 "옛 파일에 없을 때의 값"
    /// 이라 옛 파일 (Version 이 작거나 없음) 은 변환 없이 열고 모자란 필드는 기본값으로 채운다. 필드를 지우거나 뜻을 바꾸면 SchemaVersion 을 올리고
    /// 그 옛 파일은 거부한다. 현재보다 큰 Version (더 새 게임이 만든 파일) 과 깨진 파일도 거부 (사유는 LastLoadError, 한국어).</summary>
    public const int SchemaVersion = 1;
    public int Version { get; set; } = SchemaVersion;
    public string CampaignId { get; set; } = "c1";
    /// <summary>다음에 할 미션 id (파일이 없으면 '준비 중' 화면).</summary>
    public string NextMission { get; set; } = "";
    public List<string> Completed { get; set; } = new();
    public List<MissionRecord> History { get; set; } = new();
    /// <summary>이월 사병 (계급 · 사살 수 · 병력 id).</summary>
    public List<MissionResults.UnitLine> Units { get; set; } = new();
    /// <summary>이월 장교 (OfficerRank · Xp · Post).</summary>
    public List<MissionResults.UnitLine> Officers { get; set; } = new();

    /// <summary>곡사포 중대가 정식 편입됐는지 (1-2 승리 보상). 다음 미션부터 곡사포 중대를 데려간다.</summary>
    public bool HowitzerCompany { get; set; }
    /// <summary>캠페인 난이도 키 (difficulty.json, Comms.cs). 빈 값 = 기본 난이도 (옛 파일 호환).</summary>
    public string Difficulty { get; set; } = "";
    /// <summary>마지막으로 끝낸 미션이 시작할 때 들고 간 병력 (그 미션을 다시 할 때 같은 이월 병력으로 시작하기 위해).</summary>
    public List<MissionResults.UnitLine> EntryUnits { get; set; } = new();
    public List<MissionResults.UnitLine> EntryOfficers { get; set; } = new();
    public string EntryMission { get; set; } = "";

    public const string FirstMission = "c1_01_hoeryong";
    /// <summary>캠페인 미션 순서 (id → 다음 id · 표시 이름). 다음 파일이 없으면 준비 중.</summary>
    static readonly (string Id, string Next, string NextName)[] Chain =
    {
        (FirstMission, "c1_02_tumen", "1-2 두만강 도하"),
        ("c1_02_tumen", "c1_03", "1-3"),
    };

    public static bool IsCampaignMission(string id) => Chain.Any(c => c.Id == id);
    /// <summary>다시 하기 버튼용 짧은 이름 ("1-1" · "1-2").</summary>
    public static string ShortNameOf(string id) => id switch { FirstMission => "1-1", "c1_02_tumen" => "1-2", _ => id };
    public static string NextNameOf(string id) => Chain.FirstOrDefault(c => c.Id == id).NextName ?? "";
    public bool NextAvailable => NextMission.Length > 0 && File.Exists(MissionDef.PathOf(NextMission));
    public string NextName => Chain.Where(c => c.Next == NextMission).Select(c => c.NextName).FirstOrDefault() ?? NextMission;
    public bool HasProgress => Completed.Count > 0;

    public static string Folder => Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR") is { Length: > 0 } d ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoreanDivision", "campaign");
    public static string FilePath => Path.Combine(Folder, "campaign.json");

    static readonly JsonSerializerOptions Opts = new(CommandJson.Options) { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>승리한 캠페인 미션의 결과를 반영한 새 상태 (패배 · 캠페인 밖 미션이면 null). 병력은 마지막 미션의 생존자로 교체 (이월).</summary>
    public static CampaignState? Apply(CampaignState? prev, MissionResults r)
    {
        if (r.Result != "won" || !IsCampaignMission(r.MissionId)) return null;
        var s = prev ?? new CampaignState();
        bool wasDone = s.Completed.Contains(r.MissionId);
        if (!wasDone) s.Completed.Add(r.MissionId);
        s.History.RemoveAll(h => h.MissionId == r.MissionId);
        var me = r.Factions.FirstOrDefault(f => f.Player);
        s.History.Add(new MissionRecord { MissionId = r.MissionId, Title = r.Title, DurationSeconds = r.DurationSeconds, EnemyKills = r.EnemyKills, Lost = me?.Lost ?? 0, Survivors = r.Survivors.Count, SavedAt = r.SavedAt });
        // 이 미션이 들고 들어간 병력: 이미 끝낸 미션을 다시 한 것이면 그때 기록한 것, 처음이면 직전 상태의 병력
        if (s.EntryMission != r.MissionId || !wasDone) { s.EntryUnits = s.Units.ToList(); s.EntryOfficers = s.Officers.ToList(); s.EntryMission = r.MissionId; }
        if (r.MissionId == "c1_02_tumen") s.HowitzerCompany = true;
        s.NextMission = Chain.First(c => c.Id == r.MissionId).Next;
        s.Officers = r.Survivors.Where(u => u.OfficerRank != null).Select(Copy).ToList();
        s.Units = r.Survivors.Where(u => u.OfficerRank == null).Select(Copy).ToList();
        return s;
    }

    static MissionResults.UnitLine Copy(MissionResults.UnitLine u) => new()
    {
        Id = u.Id, Unit = u.Unit, Name = u.Name, Kills = u.Kills, Rank = u.Rank, StartRank = u.Rank, Force = u.Force,
        OfficerRank = u.OfficerRank, TempRank = null, Xp = u.Xp, Post = u.Post,
    };

    /// <summary>다음 미션이 읽을 이월 병력 (장교 먼저).</summary>
    public IEnumerable<MissionResults.UnitLine> Carry() => Officers.Concat(Units);

    /// <summary>미션 <paramref name="missionId"/> 가 들고 시작할 병력: 이미 끝낸 미션을 다시 하면 그 미션이 처음 받았던 병력, 아니면 현재 이월 병력.</summary>
    public IEnumerable<MissionResults.UnitLine> CarryFor(string missionId) =>
        EntryMission == missionId && Completed.Contains(missionId) ? EntryOfficers.Concat(EntryUnits) : Carry();

    public string ToJson() => JsonSerializer.Serialize(this, Opts);
    public static CampaignState? FromJson(string text) => JsonSerializer.Deserialize<CampaignState>(text, Opts);

    /// <summary>저장 (실패해도 게임은 계속). 임시 파일에 쓰고 바꿔치기. 성공 여부.</summary>
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, ToJson());
            File.Move(tmp, FilePath, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"캠페인 저장 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>마지막 Load 가 파일을 거부한 사유 (한국어, 거부가 아니면 null). 파일이 없는 것은 거부가 아니다.</summary>
    public static string? LastLoadError { get; private set; }

    /// <summary>읽기 (없거나 깨졌거나 새 버전이면 null — 사유는 LastLoadError). 크래시하지 않는다.</summary>
    public static CampaignState? Load()
    {
        if (!File.Exists(FilePath)) { LastLoadError = null; return null; }
        string text;
        try { text = File.ReadAllText(FilePath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LastLoadError = string.Format(SnapMsg.CantRead, e.Message); return null; }
        return Parse(text, out _);
    }

    /// <summary>[테스트 · Load] 텍스트 → 상태 (거부면 null + 한국어 사유).</summary>
    public static CampaignState? Parse(string text, out string? error)
    {
        error = null;
        CampaignState? s = null;
        try { s = FromJson(text); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException) { }
        if (s == null || s.Version < 1) error = SnapMsg.CampaignBroken;
        else if (s.Version > SchemaVersion) { error = string.Format(SnapMsg.CampaignNewer, s.Version, SchemaVersion); s = null; }
        else if (s.Units == null! || s.Officers == null! || s.History == null! || s.Completed == null! || s.EntryUnits == null! || s.EntryOfficers == null!) error = SnapMsg.CampaignBroken;
        if (error != null) s = null;
        else s!.Version = SchemaVersion;   // 옛 파일은 기본값으로 채운 채 현재 스키마로 취급 (다음 저장부터 현재 번호)
        LastLoadError = error;
        return s;
    }

    public static void Reset() { try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
