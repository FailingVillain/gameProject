using System.Text.Json;

namespace KoreanDivision.Core;


/// <summary>
/// 캠페인 상태 (v2): 끝낸 미션 · 다음 미션 · 이월 병력 (생존 사병 계급 · 사살 수) · 장교 (정식 계급 · 경험치 · 직책)
/// + 자원 (인력풀 · 보급품 · 탄약/소총 재고) · 중대 편성 (Roster) · 숨은 플래그 · 진행 중 작전 · 전투 정산 보고.
/// %LOCALAPPDATA%/KoreanDivision/campaign/campaign.json 에 저장한다 (환경변수 KD_CAMPAIGN_DIR 로 폴더 변경).
/// 표시 · 이월 전용 — 시뮬레이션 상태와 무관 (스냅샷 · 체크섬 영향 없음).
/// </summary>
/// <summary>군수 재고 키 (저장 파일의 문자열과 같다 — 오타로 새 키가 생기지 않게 상수로만 쓴다).</summary>
public static class StockKeys
{
    public const string Ru = "ru", Jp = "jp", De = "de", Fr = "fr", JpOld = "jpOld";
    public const string InfGun = "infGun", Field = "field", Howitzer = "howitzer";
    public static readonly string[] AllCalibers = { Ru, Jp, De, Fr, JpOld };
    public static readonly string[] AllShells = { InfGun, Field, Howitzer };
}

public sealed class CampaignState
{
    public sealed class MissionRecord
    {
        public string MissionId { get; set; } = "";
        public string Title { get; set; } = "";
        public float DurationSeconds { get; set; }
        public int EnemyKills { get; set; }
        public int Lost { get; set; }
        public int Survivors { get; set; }
        public string SavedAt { get; set; } = "";
    }

    public sealed class ManpowerState
    {
        public const int MaxRecruits = 7000;
        public int Recruits { get; set; } = 2000;
        public int Wounded { get; set; } = 0;
        public int Reserve { get; set; } = 0;
    }

    public sealed class StockState
    {
        public Dictionary<string, int> Rifles { get; set; } = new() { [StockKeys.Ru] = 3000 };
        public Dictionary<string, int> Ammo { get; set; } = new()
        {
            [StockKeys.Ru] = 100000,
            [StockKeys.Jp] = 100000,
            [StockKeys.De] = 100000,
            [StockKeys.Fr] = 100000,
            [StockKeys.JpOld] = 100000
        };
        public Dictionary<string, int> Shells { get; set; } = new()
        {
            ["infGun"] = 500,
            ["field"] = 300,
            ["howitzer"] = 150
        };
        public Dictionary<string, int> Heavy { get; set; } = new();
        public int Horses { get; set; } = 100;
        public List<string> RecentLog { get; set; } = new();
    }

    public sealed class RosterCompany
    {
        public string Id { get; set; } = "";
        public int BattalionNumber { get; set; } = 1;
        public string BattalionName { get; set; } = "제1대대";
        public string Kind { get; set; } = "infantry";
        public string Name { get; set; } = "";
        public int Strength { get; set; } = 60;
        public int MaxStrength { get; set; } = 60;
        public int Wounded { get; set; } = 0;
        public int UnhealedOps { get; set; } = 0;
        public bool Available { get; set; } = true;
        public bool Mandatory { get; set; } = false;
    }

    public sealed class PendingOperation
    {
        public string MissionId { get; set; } = "";
        public List<string> DeployedCompanyIds { get; set; } = new();
        public float SupplySpent { get; set; }
        public int AmmoLoaded { get; set; }
        public float AmmoScale { get; set; } = 1f;
        public bool RefusedMandatory { get; set; }
    }

    public sealed class DebriefReport
    {
        public string MissionId { get; set; } = "";
        public string Title { get; set; } = "";
        public int TotalLost { get; set; }
        public int WoundedReturned { get; set; }
        public int Dead { get; set; }
        public int DischargedWounded { get; set; }
        public int EnemyKills { get; set; }
        public float SupplyBefore { get; set; }
        public float SupplyAfter { get; set; }
        public float RegularSupplyAdded { get; set; }
        public int RecruitsAdded { get; set; }
        public float LootedSupply { get; set; }
        public int LootedRifles { get; set; }
        public int LootedAmmo { get; set; }
        public int LootedHorses { get; set; }
        public int AmmoRefunded { get; set; }
        public string? TriggeredEventId { get; set; }
    }

    /// <summary>캠페인 저장 스키마 버전. 규칙 (ENGINE_NOTES 저장 호환): 필드는 추가만 한다 — 새 필드는 C# 기본값이 곧 "옛 파일에 없을 때의 값"
    /// 이라 옛 파일 (Version 이 작거나 없음) 은 변환 없이 열고 모자란 필드는 기본값으로 채운다. 필드를 지우거나 뜻을 바꾸면 SchemaVersion 을 올리고
    /// 그 옛 파일은 거부한다. 현재보다 큰 Version (더 새 게임이 만든 파일) 과 깨진 파일도 거부 (사유는 LastLoadError, 한국어).</summary>
    public const int SchemaVersion = 2;
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

    // ---- v2 신규 필드
    public ManpowerState Manpower { get; set; } = new();
    public float Supply { get; set; } = 3000f;
    public StockState Stock { get; set; } = new();
    public List<RosterCompany> Roster { get; set; } = new();
    public Dictionary<string, int> Flags { get; set; } = new() { ["heir_mood"] = 60, ["cmd_favor"] = 50 };
    public uint Seed { get; set; } = 19200401u;
    public PendingOperation? Pending { get; set; }
    public DebriefReport? LastDebrief { get; set; }

    /// <summary>캠페인 첫 미션 = 챕터 0 튜토리얼 0-1 (T09 수직 슬라이스, Q10 압록강 시작 승인 2026-10-22).</summary>
    public const string FirstMission = "c0_01_bandits";
    public const string Tutorial2 = "c0_02_yalu", YaluCrossing = "c1_01_yalu";
    /// <summary>재배치된 옛 1-1 · 1-2 (Q10: 러시아 편 앞부분으로 옮김 — 번호 · 위치는 추후). 옛 저장이 이어서 진행할 수 있게 사슬에는 남긴다.</summary>
    public const string Hoeryong = "c1_01_hoeryong", Tumen = "c1_02_tumen";
    /// <summary>캠페인 미션 순서 (id → 다음 id · 표시 이름). 다음 파일이 없으면 준비 중.</summary>
    static readonly (string Id, string Next, string NextName)[] Chain =
    {
        (FirstMission, Tutorial2, "0-2 압록강을 건너서"),
        (Tutorial2, YaluCrossing, "1-1 압록강 도하"),
        (YaluCrossing, "c1_02", "1-2"),
        (Hoeryong, Tumen, "두만강 도하"),
        (Tumen, "c1_03", "1-3"),
    };


    public CampaignState()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        Manpower ??= new();
        Stock ??= new();
        Stock.Rifles ??= new();
        if (!Stock.Rifles.ContainsKey(StockKeys.Ru)) Stock.Rifles[StockKeys.Ru] = 3000;
        Stock.Ammo ??= new();
        foreach (var k in StockKeys.AllCalibers)
            if (!Stock.Ammo.ContainsKey(k)) Stock.Ammo[k] = 100000;
        Stock.Shells ??= new();
        if (!Stock.Shells.ContainsKey(StockKeys.InfGun)) Stock.Shells[StockKeys.InfGun] = 500;
        if (!Stock.Shells.ContainsKey(StockKeys.Field)) Stock.Shells[StockKeys.Field] = 300;
        if (!Stock.Shells.ContainsKey(StockKeys.Howitzer)) Stock.Shells[StockKeys.Howitzer] = 150;
        Stock.Heavy ??= new();
        Stock.RecentLog ??= new();
        Flags ??= new();
        if (!Flags.ContainsKey("heir_mood")) Flags["heir_mood"] = 60;
        if (!Flags.ContainsKey("cmd_favor")) Flags["cmd_favor"] = 50;
        Roster ??= new();
        if (Roster.Count == 0)
        {
            Roster.Add(new RosterCompany { Id = "b1_c1", BattalionNumber = 1, BattalionName = "제1대대", Kind = "infantry", Name = "제1중대", Strength = 60, MaxStrength = 60, Available = true });
            Roster.Add(new RosterCompany { Id = "b1_c2", BattalionNumber = 1, BattalionName = "제1대대", Kind = "infantry", Name = "제2중대", Strength = 60, MaxStrength = 60, Available = true });
            Roster.Add(new RosterCompany { Id = "b1_c3", BattalionNumber = 1, BattalionName = "제1대대", Kind = "infantry", Name = "제3중대", Strength = 60, MaxStrength = 60, Available = true });
            Roster.Add(new RosterCompany { Id = "mg", BattalionNumber = 0, BattalionName = "직할", Kind = "mg", Name = "기관총중대", Strength = 30, MaxStrength = 30, Available = true });
        }
        if (HowitzerCompany)
        {
            var how = Roster.FirstOrDefault(c => c.Id == "how");
            if (how == null)
                Roster.Add(new RosterCompany { Id = "how", BattalionNumber = 0, BattalionName = "직할", Kind = "howitzer", Name = "곡사포중대", Strength = 40, MaxStrength = 40, Available = true });
            else
                how.Available = true;
        }
    }

    /// <summary>손상 · 수작업 편집된 저장 값을 허용 범위로 되돌린다 (음수 재고 · 정원 초과 · 모르는 미션을 가리키는 출격 기록).</summary>
    public void Sanitize()
    {
        Supply = float.IsFinite(Supply) ? Math.Max(0f, Supply) : 0f;
        Manpower.Recruits = Math.Clamp(Manpower.Recruits, 0, ManpowerState.MaxRecruits);
        Manpower.Wounded = Math.Max(0, Manpower.Wounded);
        Manpower.Reserve = Math.Max(0, Manpower.Reserve);
        foreach (var d in new[] { Stock.Rifles, Stock.Ammo, Stock.Shells, Stock.Heavy })
            foreach (var k in d.Keys.ToList()) if (d[k] < 0) d[k] = 0;
        Stock.Horses = Math.Max(0, Stock.Horses);
        foreach (var c in Roster)
        {
            c.MaxStrength = Math.Max(1, c.MaxStrength);
            c.Strength = Math.Clamp(c.Strength, 0, c.MaxStrength);
            c.Wounded = Math.Max(0, c.Wounded);
            c.UnhealedOps = Math.Max(0, c.UnhealedOps);
        }
        if (Pending != null && (string.IsNullOrEmpty(Pending.MissionId) || Pending.DeployedCompanyIds == null)) Pending = null;
        if (Pending != null) Pending.AmmoScale = float.IsFinite(Pending.AmmoScale) ? Math.Clamp(Pending.AmmoScale, 0f, 1f) : 1f;
    }

    public static bool IsCampaignMission(string id) => Chain.Any(c => c.Id == id);
    /// <summary>다시 하기 버튼용 짧은 이름 ("1-1" · "1-2").</summary>
    public static string ShortNameOf(string id) => id switch { FirstMission => "0-1", Tutorial2 => "0-2", YaluCrossing => "1-1", Hoeryong => "회령", Tumen => "두만강", _ => id };
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
        s.EnsureInitialized();
        bool wasDone = s.Completed.Contains(r.MissionId);
        if (!wasDone) s.Completed.Add(r.MissionId);
        s.History.RemoveAll(h => h.MissionId == r.MissionId);
        var me = r.Factions.FirstOrDefault(f => f.Player);
        s.History.Add(new MissionRecord { MissionId = r.MissionId, Title = r.Title, DurationSeconds = r.DurationSeconds, EnemyKills = r.EnemyKills, Lost = me?.Lost ?? 0, Survivors = r.Survivors.Count, SavedAt = r.SavedAt });
        // 지난 미션 (마지막 미션이 아닌, 이미 끝낸 미션) 을 다시 이긴 것: 진행 (다음 미션) · 이월 병력은 그대로 둔다 (진행이 뒤로 감기지 않게)
        bool olderReplay = wasDone && s.EntryMission != r.MissionId;
        if (!olderReplay)
        {
            // 이 미션이 들고 들어간 병력: 마지막 미션을 다시 한 것이면 그때 기록한 것, 처음이면 직전 상태의 병력
            if (!wasDone) { s.EntryUnits = s.Units.ToList(); s.EntryOfficers = s.Officers.ToList(); s.EntryMission = r.MissionId; }
            if (r.MissionId == Tumen) s.HowitzerCompany = true;
            s.NextMission = Chain.First(c => c.Id == r.MissionId).Next;
            s.Officers = r.Survivors.Where(u => u.OfficerRank != null).Select(Copy).ToList();
            s.Units = r.Survivors.Where(u => u.OfficerRank == null).Select(Copy).ToList();
        }

        // 곡사포 중대가 편입될 때 HowitzerCompany == true 이면 Roster 에 "how" 곡사포 중대도 Available = true 로 추가/활성화
        if (s.HowitzerCompany)
        {
            var how = s.Roster.FirstOrDefault(c => c.Id == "how");
            if (how == null)
                s.Roster.Add(new RosterCompany { Id = "how", BattalionNumber = 0, BattalionName = "직할", Kind = "howitzer", Name = "곡사포중대", Strength = 40, MaxStrength = 40, Available = true });
            else
                how.Available = true;
        }

        // 전투 정산 (자원 / 부상 / 노획 / 정기보급 / LastDebrief)
        CampaignEconomy.SettleBattle(s, r, firstWin: !wasDone);   // D10 A: 정기 보급은 그 미션의 첫 승리만

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
    public static CampaignState? FromJson(string text)
    {
        var s = JsonSerializer.Deserialize<CampaignState>(text, Opts);
        s?.EnsureInitialized();
        if (s != null && s.Version >= 1 && s.Version < SchemaVersion) s.Version = SchemaVersion;   // 옛 파일 (1) 은 기본값으로 채운 채 현재 번호로. 0 · 음수는 그대로 두어 Parse 가 거부
        s?.Sanitize();
        return s;
    }

    /// <summary>읽지 못한 원본을 campaign.json.bak 로 옮긴다 (초기화 전 보존). 옮겼으면 true.</summary>
    public static bool BackupBroken()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            string bak = FilePath + ".bak";
            for (int i = 1; File.Exists(bak); i++) bak = FilePath + ".bak." + i;   // 기존 .bak 는 덮어쓰지 않는다
            File.Move(FilePath, bak);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>저장 (실패해도 게임은 계속). 임시 파일에 쓰고 바꿔치기 — 실패 시 임시 파일도 지운다 (CampaignSaves.WriteAtomic). 성공 여부.</summary>
    public bool Save() => CampaignSaves.WriteAtomic(FilePath, ToJson());

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
