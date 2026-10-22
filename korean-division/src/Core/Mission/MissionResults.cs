using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 미션 결과 (결과 화면 · 결과 json). 캠페인 이월 (나중) 을 위해 사용자 데이터 폴더에 남긴다:
/// %LOCALAPPDATA%/KoreanDivision/results/&lt;미션&gt;_&lt;시각&gt;.json + &lt;미션&gt;_last.json (환경변수 KD_RESULTS_DIR 로 바꿈).
/// 월드를 읽기만 한다 (표시 전용 — 시뮬레이션과 무관).
/// </summary>
public sealed class MissionResults
{
    public sealed class FactionLine { public int Id { get; set; } public string Name { get; set; } = ""; public bool Player { get; set; } public int Spawned { get; set; } public int Lost { get; set; } public int Alive { get; set; } public int Captured { get; set; } }
    public sealed class ObjectiveLine { public string Id { get; set; } = ""; public string Type { get; set; } = ""; public bool Primary { get; set; } public bool Revealed { get; set; } public string State { get; set; } = ""; public float Progress { get; set; } public string? Text { get; set; } }
    public sealed class UnitLine { public int Id { get; set; } public string Unit { get; set; } = ""; public string Name { get; set; } = ""; public int Kills { get; set; } public int Rank { get; set; } public int StartRank { get; set; } public string? Force { get; set; }
        /// <summary>[장교] 정식 계급 · 전투 중 임시 진급 단계 · 경험치 · 직책 (company · battalion · staff) — 캠페인 정식 진급 · 계승 근거. 사병은 null.</summary>
        public int? OfficerRank { get; set; } public int? TempRank { get; set; } public int? Xp { get; set; } public string? Post { get; set; }
        /// <summary>[T48b1] 캠페인 장교 신원 키 (미션 텍스트 officerKeys 로 주입된 장교만, 아니면 null). 정산이 첫 등장 장교에게 발급.</summary>
        public int? Key { get; set; } }
    /// <summary>[T48b1] 키를 받고 출전한 장교가 살아 돌아오지 못함 (전사, Captured = 항복해 포로). 정산이 명부를 dead (AlwaysWounded 는 부상) 로.</summary>
    public sealed class OfficerDeath { public int Key { get; set; } public int UnitId { get; set; } public bool Captured { get; set; } }

    public int Version { get; set; } = 1;
    public string MissionId { get; set; } = "";
    public string Title { get; set; } = "";
    public string MissionHash { get; set; } = "";
    public string Result { get; set; } = "";
    public string? FailReason { get; set; }
    public float DurationSeconds { get; set; }
    public long EndTick { get; set; }
    public int Seed { get; set; }
    public bool CheatsUsed { get; set; }
    public int PlayerFaction { get; set; }
    public int EnemyKills { get; set; }
    /// <summary>[T42c] 끝났을 때 항복한 채 살아 있는 적대 세력 유닛 수 (= 포로). 항복 규칙이 꺼져 있으면 0. 시뮬 상태를 읽기만 함 — 저장 · 체크섬 무관.</summary>
    public int EnemyCaptured { get; set; }
    /// <summary>[측정] 아군 오사 — 무기 종류별 (명중 · 사살 · 피해량). 저장 · 체크섬과 무관한 부가 계수기.</summary>
    public Dictionary<string, FriendlyFireEntry> FriendlyFire { get; set; } = new();
    /// <summary>[T19b] 플레이어 세력 포 (병과 gun) 의 무기 키별 발사 수 — 캠페인 포탄 정산 (실제 발사 수 차감). 미션 중 불러오기를 하면 그 뒤 발사만 센다.</summary>
    public Dictionary<string, int> PlayerGunShots { get; set; } = new();
    public sealed class FriendlyFireEntry { public int Hits { get; set; } public int Kills { get; set; } public double Damage { get; set; } }
    public string SavedAt { get; set; } = "";
    public List<FactionLine> Factions { get; set; } = new();
    public List<ObjectiveLine> Objectives { get; set; } = new();
    /// <summary>살아남은 플레이어 유닛 중 사살 수 상위 (캠페인 이월 후보).</summary>
    public List<UnitLine> TopUnits { get; set; } = new();
    public List<UnitLine> Promotions { get; set; } = new();
    /// <summary>살아남은 플레이어 유닛 전부 (병력 id · 종류 · 계급 — 캠페인 이월용).</summary>
    public List<UnitLine> Survivors { get; set; } = new();
    public List<DeployEntry> Deployment { get; set; } = new();
    /// <summary>[T48b1] 키 장교 전사 · 포로 (Key 순).</summary>
    public List<OfficerDeath> Deaths { get; set; } = new();
    /// <summary>[g3] 미션 끝의 미션 변수 · 미션 중 결정 응답 (결정 id → 선택지) — 캠페인 정산 입력 (CampaignDecisions.ApplyMissionOutcome). 표시 전용, 저장 · 체크섬 무관.</summary>
    public Dictionary<string, int> Vars { get; set; } = new();
    public Dictionary<string, string> Decisions { get; set; } = new();

    public static MissionResults From(World w)
    {
        var m = w.Mission ?? throw new InvalidOperationException("미션 월드가 아님");
        var r = new MissionResults
        {
            MissionId = m.Def.Id, Title = m.Def.Title, MissionHash = m.Def.Hash,
            Result = m.Result == MissionState.Won ? "won" : m.Result == MissionState.Lost ? "lost" : "running",
            DurationSeconds = (m.Result != MissionState.Running ? m.EndTick : w.Tick) * Config.SimDt,
            EndTick = m.Result != MissionState.Running ? m.EndTick : w.Tick,
            Seed = w.Map.Seed, CheatsUsed = w.CheatsUsed, PlayerFaction = m.Player,
            SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Deployment = new List<DeployEntry>(m.Deployment),
            Vars = new Dictionary<string, int>(m.Vars), Decisions = new Dictionary<string, string>(m.Decisions),
        };
        if ((uint)m.Player < MoveStats.MaxF) foreach (var (k, v) in w.Stats.GunShots[m.Player]) r.PlayerGunShots[k] = v;
        foreach (var (k, v) in w.Stats.FriendlyFire) r.FriendlyFire[k] = new FriendlyFireEntry { Hits = v.Hits, Kills = v.Kills, Damage = Math.Round(v.Damage, 1) };
        if (m.FailReason >= 0 && m.FailReason < m.Def.Failures.Count) r.FailReason = m.Def.Failures[m.FailReason].Type;
        else if (m.Result == MissionState.Lost) r.FailReason = "objective";
        var alive = new int[w.Factions.Count];
        int[] captured = new int[w.Factions.Count];
        foreach (var u in w.Units) if (u.Alive && u.Owner < alive.Length) { if (u.Surrendered) captured[u.Owner]++; else alive[u.Owner]++; }
        for (int f = 1; f < w.Factions.Count; f++)
        {
            int sp = f < m.Spawned.Length ? m.Spawned[f] : 0;
            r.Factions.Add(new FactionLine { Id = f, Name = w.Faction(f).Name, Player = f == m.Player, Spawned = sp, Lost = Math.Max(0, sp - alive[f]), Alive = alive[f], Captured = captured[f] });
            if (w.IsHostile(m.Player, f)) { r.EnemyKills += Math.Max(0, sp - alive[f] - captured[f]); r.EnemyCaptured += captured[f]; }
        }
        for (int i = 0; i < m.Def.Objectives.Count; i++)
        {
            var o = m.Def.Objectives[i];
            r.Objectives.Add(new ObjectiveLine
            {
                Id = o.Id, Type = o.Type, Primary = o.Primary, Revealed = m.ObjRevealed[i], Progress = m.ObjProgress[i], Text = o.Text,
                State = m.ObjState[i] == MissionState.Done ? "done" : m.ObjState[i] == MissionState.Failed ? "failed" : "active",
            });
        }
        var forceOf = new Dictionary<int, string>();
        foreach (var (fid, ids) in m.ForceUnits) foreach (int id in ids) forceOf[id] = fid;
        foreach (var u in w.Units)
        {
            if (!u.Alive || u.Surrendered || u.Owner != m.Player) continue;
            int start = m.StartRank.TryGetValue(u.Id, out int sr) ? sr : 0;
            var fds = MapFile.FactionsOf(w.Map);
            string? en = u.Owner >= 1 && u.Owner <= fds.Count ? fds[u.Owner - 1].En : null;   // 미사용 계급은 건너뛴다 (RankNames.Snap)
            var line = new UnitLine { Id = u.Id, Unit = u.Def.Key, Name = u.Def.Name, Kills = u.Kills, Rank = u.IsOfficer ? 0 : RankNames.Snap(en, Math.Max(u.Rank, Defs.Current.CombatRules.RankOf(u.Kills))),   // 사살로 얻은 진급 후보 계급 (전투 중 계급은 안 오른다 — 캠페인 진급 근거)
                StartRank = start, Force = forceOf.GetValueOrDefault(u.Id) };
            if (u.IsOfficer)
            {
                line.OfficerRank = u.OfficerBase; line.TempRank = u.TempRank; line.Xp = u.OfficerXp;
                if (m.OfficerKey.TryGetValue(u.Id, out int ok)) line.Key = ok;
                line.Post = w.PostOf(u) switch { OfficerPost.Battalion => "battalion", OfficerPost.Assistant => "assistant", OfficerPost.Company => "company", OfficerPost.Staff => "staff", _ => null };
            }
            r.Survivors.Add(line);
            if (line.Rank > start || line.TempRank > 0) r.Promotions.Add(line);   // 장교 임시 진급 = 캠페인 정식 진급 후보
        }
        foreach (var (uid, key) in m.OfficerKey)   // T48b1: 주입한 키 장교 중 생존 줄이 없는 것 (전사 · 포로)
            if (w.Find(uid) is not Unit ou || ou.Surrendered || ou.Owner != m.Player)
                r.Deaths.Add(new OfficerDeath { Key = key, UnitId = uid, Captured = w.Find(uid) is Unit cu && cu.Surrendered });
        r.Deaths = r.Deaths.OrderBy(d => d.Key).ToList();
        r.TopUnits = r.Survivors.Where(s => s.Kills > 0).OrderByDescending(s => s.Kills).ThenBy(s => s.Id).Take(5).ToList();
        return r;
    }

    public static string Folder => Environment.GetEnvironmentVariable("KD_RESULTS_DIR") is { Length: > 0 } d ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoreanDivision", "results");

    /// <summary>결과 json 쓰기 (실패해도 게임은 계속). 쓴 경로 또는 null.</summary>
    public string? Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string safe = string.Concat(MissionId.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_'));
            string path = Path.Combine(Folder, $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            string text = JsonSerializer.Serialize(this, new JsonSerializerOptions(CommandJson.Options) { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            File.WriteAllText(path, text);
            File.WriteAllText(Path.Combine(Folder, $"{safe}_last.json"), text);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"미션 결과 저장 실패: {e.Message}");
            return null;
        }
    }
}
