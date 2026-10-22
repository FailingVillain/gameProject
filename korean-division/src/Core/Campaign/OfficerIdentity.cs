namespace KoreanDivision.Core;

/// <summary>
/// [T48b1] 캠페인 장교 신원 (OfficerKey). 미션 안 Unit Id 는 미션마다 새로 매겨지므로 (WorldMission.SpawnForce) 캠페인이 키를 발급해
/// 이월 줄 (UnitLine.Key) · 명부 (CampaignState.CampaignOfficers) 에 싣는다. 흐름: CampaignCarry.Patch 가 중대 "officerKeys" 를 쓰고 →
/// WorldMission 이 스폰한 장교 Unit Id → 키 (MissionState.OfficerKey, 스냅샷 FormatVersion 53) → MissionResults 가 생존 줄 Key · 전사 Deaths 로 돌려주고 →
/// 승리 정산 (CampaignState.Apply → <see cref="Settle"/>) 이 명부를 갱신한다. 작전 사이 데이터 처리만 — 전투 시뮬 입력은 기존 계급 · 경험치 그대로.
/// 규칙 (사용자 2026-10-22): 전사 = 영구 상실 (키 재사용 없음), AlwaysWounded 인물은 전사 대신 부상 (복귀 = 다음 작전 하나 쉬고, Q-C8 기본 1).
/// 모든 판정은 정수 · 정렬된 목록 순서 (결정적).
/// </summary>
public static class OfficerIdentity
{
    public const string Alive = "alive", Wounded = "wounded", Dead = "dead";
    /// <summary>AlwaysWounded 부상 뒤 쉬는 작전 수 (Q-C8 기본 1, 임시).</summary>
    public const int WoundReturnMissions = 1;

    public static CampaignState.CampaignOfficer? Find(CampaignState? s, int key) =>
        key <= 0 || s?.CampaignOfficers == null ? null : s.CampaignOfficers.FirstOrDefault(o => o != null && o.Key == key);

    /// <summary>NextOfficerKey 를 (명부 · 이월 줄의 최대 키 + 1) 이상, 최소 1 로 (옛 파일 · 손상 파일).</summary>
    public static void EnsureNextKey(CampaignState s)
    {
        int max = 0;
        foreach (var o in s.CampaignOfficers ?? new()) if (o != null) max = Math.Max(max, o.Key);
        foreach (var l in (s.Officers ?? new()).Concat(s.EntryOfficers ?? new())) if (l?.Key is int k) max = Math.Max(max, k);
        if (s.NextOfficerKey <= max) s.NextOfficerKey = max + 1;
        if (s.NextOfficerKey < 1) s.NextOfficerKey = 1;
    }

    /// <summary>이 장교를 다음 미션에 넣을 수 있는가: 키 없음 (옛 줄) = 예, 전사 = 아니오, 부상 = 복귀 작전 수에 이르기 전엔 아니오, 참모 배정 = 아니오 (대대장만 참전).</summary>
    public static bool CanDeploy(CampaignState s, MissionResults.UnitLine line)
    {
        if (Find(s, line.Key ?? 0) is not { } o) return true;
        if (o.Status == Dead || o.Assignment == "staff") return false;
        return o.Status != Wounded || s.Completed.Count >= o.WoundedUntilMission;
    }

    /// <summary>대대장 줄인가 (명부 배정 battalion 또는 지난 미션 직책 battalion) — 주입 순서 · 전사 연표용.</summary>
    public static bool IsBattalion(CampaignState s, MissionResults.UnitLine? line, CampaignState.CampaignOfficer? o) =>
        o?.Assignment == "battalion" || line?.Post == "battalion";

    /// <summary>
    /// 승리 정산 (첫 승리, 이월 병력을 새로 정한 뒤): ① 전사 처리 (AlwaysWounded → 부상 · 이월 줄 보존) ② 복귀 시점이 된 부상자 → alive
    /// ③ 생존 장교 줄: 키 있으면 명부 계급 · 경험치 · 공적 갱신, 없으면 새 키 발급 + 명부 추가 ④ 이번 미션에 키를 주입했으면, 출전하지 않은 (생존도 전사도 아닌) 키 장교의 이월 줄을 보존.
    /// </summary>
    public static void Settle(CampaignState s, MissionResults r)
    {
        EnsureNextKey(s);
        int n = s.Completed.Count;
        var entry = new SortedDictionary<int, MissionResults.UnitLine>();
        foreach (var l in s.EntryOfficers) if (l?.Key is int k && k > 0 && !entry.ContainsKey(k)) entry[k] = l;
        var survivorKeys = new HashSet<int>();
        foreach (var l in s.Officers) if (l.Key is int k && k > 0) survivorKeys.Add(k);
        bool injected = survivorKeys.Count > 0 || r.Deaths.Count > 0;

        // ① 전사
        foreach (var d in r.Deaths.OrderBy(d => d.Key))
        {
            if (d.Key <= 0 || survivorKeys.Contains(d.Key)) continue;
            var o = Find(s, d.Key);
            if (o == null) { o = NewRecord(d.Key, entry.GetValueOrDefault(d.Key)); s.CampaignOfficers.Add(o); }
            if (o.Status == Dead) continue;
            bool bat = IsBattalion(s, entry.GetValueOrDefault(d.Key), o);
            if (o.AlwaysWounded)
            {
                o.Status = Wounded;
                o.WoundedUntilMission = n + WoundReturnMissions;
                s.AddTimeline("commander", "wounded:" + o.Key, o.Name, o.WoundedUntilMission);
            }
            else
            {
                o.Status = Dead; o.DeathMission = r.MissionId; o.DeathDay = Math.Max(1, s.Day);
                o.TerminalReady = false;
                if (bat) s.AddTimeline("commander", "dead:" + o.Key, o.Name);
            }
        }
        // ② 부상 복귀
        foreach (var o in s.CampaignOfficers)
            if (o.Status == Wounded && n >= o.WoundedUntilMission && !r.Deaths.Any(d => d.Key == o.Key)) o.Status = Alive;
        // ③ 생존 장교 (Id 순 = 결정적 발급 순서)
        foreach (var l in s.Officers.OrderBy(l => l.Id))
        {
            CampaignState.CampaignOfficer? o = l.Key is int k && k > 0 ? Find(s, k) : null;
            if (o == null)
            {
                int key = l.Key is int lk && lk > 0 ? lk : s.NextOfficerKey++;
                l.Key = key;
                o = NewRecord(key, l);
                s.CampaignOfficers.Add(o);
            }
            if (o.Status == Dead) continue;
            o.Rank = l.OfficerRank ?? o.Rank; o.Xp = l.Xp ?? o.Xp;
            o.Merits = (int)Math.Min(int.MaxValue, (long)o.Merits + Math.Max(0, l.Kills));
            o.TerminalReady = CampaignPromotion.IsTerminal(o.Rank);
        }
        // ④ 출전하지 않은 키 장교 보존 (부상 · 참모 · 자리 부족)
        if (injected)
            foreach (var (k, l) in entry)
                if (!survivorKeys.Contains(k) && Find(s, k) is { } o && o.Status != Dead) s.Officers.Add(CampaignState.CopyLine(l));
        EnsureNextKey(s);
    }

    static CampaignState.CampaignOfficer NewRecord(int key, MissionResults.UnitLine? l) => new()
    {
        Key = key, Name = l?.Name ?? "", Rank = l?.OfficerRank ?? 0, Xp = l?.Xp ?? 0, Status = Alive,   // 배정 (Assignment) 은 T48b2 가 정한다
    };
}
