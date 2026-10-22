using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>T48b1 장교 신원 (OfficerKey): 발급 · 미션 주입 (officerKeys → MissionState.OfficerKey) · 미션 중 스냅샷 왕복 · 결과 환수 (Key · Deaths) ·
/// 전사 = 영구 상실 / AlwaysWounded = 부상 + 1작전 뒤 복귀 · 대대장 주입 · 3작전 + 저장 왕복 뒤 신원 유지 · 진급 Key 전환 · 종착 · 옛 저장 v8 → v9. 그룹 KD_SELFTEST_ONLY=officers (SaveCompat 에서도 부름).</summary>
public static class OfficerIdentitySelfTest
{
    static MissionResults.UnitLine Off(int id, int rank, int xp, string post = "company", int? key = null) =>
        new() { Id = id, Unit = "officer", Name = "장교", OfficerRank = rank, Xp = xp, Post = post, Force = "b1", Key = key };

    static MissionResults Won(string mission, IEnumerable<MissionResults.UnitLine> survivors)
    {
        var r = new MissionResults { MissionId = mission, Result = "won", Title = mission };
        r.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 60, Alive = 60 });
        r.Survivors.AddRange(survivors);
        return r;
    }

    static World Spawn(CampaignState s, string mission, out string text)
    {
        text = CampaignCarry.Patch(File.ReadAllText(MissionDef.PathOf(mission)), mission, s);
        return World.CreateMissionWorld(MissionDef.Parse(text, mission), Defs.Current);
    }

    public static void Run(Action<bool, string> check)
    {
        var savedEco = CampaignEconomyDefs.Current;
        try
        {
            // 1) 옛 저장 v8 -> v9: 키 없음, NextOfficerKey = 1 (명부에 키가 있으면 최대 + 1)
            var v8 = CampaignState.FromJson("{\"Version\":8,\"CampaignOfficers\":[{\"Key\":7,\"Name\":\"갑\"}]}");
            var v7 = CampaignState.FromJson("{\"Version\":7}");
            check(v8 != null && v8.Version == CampaignState.SchemaVersion && v8.NextOfficerKey == 8 && v8.CampaignOfficers[0].Status == "alive" && v8.CampaignOfficers[0].Traits.Count == 0
                && v7 != null && v7.NextOfficerKey == 1 && v7.CampaignOfficers.Count == 0, "T48b1 저장 이행: v7 · v8 -> v9 (NextOfficerKey = 최대 키 + 1 · 기본값)");

            // 2) 첫 승리 정산: 키 없는 생존 장교에게 Id 순으로 키 발급 + 명부 · 종착 (상한 계급 = TerminalReady)
            int cap = Math.Min(Defs.Current.Officers.MaxPromotionRank, Defs.Current.Officers.MaxRank);
            var s = CampaignState.Apply(new CampaignState(), Won(CampaignState.FirstMission, new[] { Off(30, 2, 50), Off(12, 4, 900, "battalion"), Off(20, 3, 200), Off(25, cap, 100) }))!;
            var keyOf = s.Officers.ToDictionary(l => l.Id, l => l.Key ?? 0);
            check(keyOf[12] == 1 && keyOf[20] == 2 && keyOf[25] == 3 && keyOf[30] == 4 && s.NextOfficerKey == 5 && s.CampaignOfficers.Count == 4
                && s.CampaignOfficers.All(o => o.Status == "alive") && OfficerIdentity.Find(s, 3)!.TerminalReady && !OfficerIdentity.Find(s, 2)!.TerminalReady,
                $"T48b1 키 발급: Id 순 1..4, NextOfficerKey 5, 상한 계급 = 종착 (keys {string.Join(",", keyOf.OrderBy(k => k.Key).Select(k => k.Value))})");
            OfficerIdentity.Find(s, 2)!.AlwaysWounded = true;

            // 3) 주입 (1-1 = 중대 3개 장교 1명씩): 대대장 먼저 → 계급순, 4번째는 자리 없음 — officerKeys 가 미션 텍스트 → MissionState.OfficerKey
            var w = Spawn(s, CampaignState.YaluCrossing, out string text);
            var m = w.Mission!;
            var injected = m.OfficerKey.Values.OrderBy(v => v).ToList();
            check(text.Contains("\"officerKeys\"") && string.Join(",", injected) == "1,2,3", $"T48b1 주입: 대대장 (키 1) + 계급순 2명, 자리 없는 1명 제외 (주입 {string.Join(",", injected)})");
            var bat = w.Units.First(u => m.OfficerKey.TryGetValue(u.Id, out int k) && k == 1);
            check(w.PostOf(bat) == OfficerPost.Battalion && bat.OfficerXp == 900, $"T48b1 대대장 주입 경로: Post battalion 장교가 다시 대대장 ({w.PostOf(bat)})");

            // 4) 미션 중 스냅샷 왕복: 키 매핑 유지
            var meta = new SnapshotMeta("t48b1", 1, false, "standard");
            var y = Snapshot.Load(Snapshot.Save(w, meta), Defs.Current).World;
            check(y.Mission != null && y.Mission.OfficerKey.SequenceEqual(m.OfficerKey) && y.ComputeChecksum() == w.ComputeChecksum(), "T48b1 미션 중 저장/불러오기: OfficerKey 유지 · 체크섬 같음");

            // 5) 대대장 (키 1) · AlwaysWounded (키 2) 전사 → 결과 Deaths, 정산: 1 = 영구 상실 · 2 = 부상 (다음 작전 쉼), 3 생존, 4 미출전 = 이월 보존
            foreach (int k in new[] { 1, 2 }) { var u = y.Units.First(u => y.Mission!.OfficerKey.TryGetValue(u.Id, out int kk) && kk == k); y.Damage(u, 1e7f, null); }
            var r = MissionResults.From(y); r.Result = "won";
            check(string.Join(",", r.Deaths.Select(d => d.Key)) == "1,2" && r.Survivors.Count(l => l.Key == 3) == 1, $"T48b1 결과 환수: Deaths {string.Join(",", r.Deaths.Select(d => d.Key))} · 생존 줄 Key");
            s = CampaignState.Apply(s, r)!;
            var o1 = OfficerIdentity.Find(s, 1)!; var o2 = OfficerIdentity.Find(s, 2)!;
            int n = s.Completed.Count;
            check(o1.Status == "dead" && o1.DeathMission == CampaignState.YaluCrossing && o2.Status == "wounded" && o2.WoundedUntilMission == n + 1
                && s.Officers.Any(l => l.Key == 2) && s.Officers.Any(l => l.Key == 4) && s.Officers.All(l => l.Key != 1)
                && s.Timeline.Any(e => e.Kind == "commander" && e.Id == "dead:1") && s.Timeline.Any(e => e.Kind == "commander" && e.Id == "wounded:2"),
                $"T48b1 전사 정산: 대대장 dead · AlwaysWounded wounded (복귀 {o2.WoundedUntilMission}, 작전 {n}) · 연표 commander · 미출전/부상 이월 보존");

            // 6) 다음 작전: 전사 · 부상 장교 주입 안 됨 → 그 작전 승리 뒤 부상 복귀 → 그다음 작전 주입
            var w2 = Spawn(s, CampaignState.Tumen, out _);
            var inj2 = w2.Mission!.OfficerKey.Values.ToHashSet();
            check(!inj2.Contains(1) && !inj2.Contains(2) && inj2.Contains(3), $"T48b1 부상 중 출전 불가 · 전사 영구 상실 (주입 {string.Join(",", inj2.OrderBy(v => v))})");
            var r2 = MissionResults.From(w2); r2.Result = "won";
            s = CampaignState.Apply(s, r2)!;
            check(OfficerIdentity.Find(s, 2)!.Status == "alive" && s.Officers.Any(l => l.Key == 2), "T48b1 AlwaysWounded 복귀: 한 작전 쉰 뒤 alive");
            check(CampaignCarry.Officers(s, CampaignState.YaluCrossing).Any(l => l.Key == 2) && CampaignCarry.Officers(s, CampaignState.YaluCrossing).All(l => l.Key != 1), "T48b1 복귀 뒤 다시 주입 후보 (전사 장교는 영구 제외)");

            // 7) 3작전 + 저장 왕복: 키 · 이름 · 계급 · 경험치 유지, 키 재사용 없음
            var back = CampaignState.FromJson(s.ToJson())!;
            var a3 = OfficerIdentity.Find(s, 3)!; var b3 = OfficerIdentity.Find(back, 3)!;
            check(back.NextOfficerKey == s.NextOfficerKey && back.CampaignOfficers.Count == s.CampaignOfficers.Count && b3.Name == a3.Name && b3.Rank == a3.Rank && b3.Xp == a3.Xp
                && back.Officers.Select(l => l.Key).SequenceEqual(s.Officers.Select(l => l.Key)) && OfficerIdentity.Find(back, 1)!.Status == "dead"
                && s.CampaignOfficers.Select(o => o.Key).Distinct().Count() == s.CampaignOfficers.Count,
                $"T48b1 신원 영속: 3작전 + 저장 왕복 (명부 {s.CampaignOfficers.Count}, 다음 키 {s.NextOfficerKey})");

            // 8) 전사 100건: 일반 50 전원 dead, AlwaysWounded 50 사망 0 · 복귀 시점 = 작전 수 + 1
            var t = new CampaignState();
            for (int i = 1; i <= 100; i++) t.CampaignOfficers.Add(new CampaignState.CampaignOfficer { Key = i, Name = "o" + i, AlwaysWounded = i % 2 == 0 });
            t.EntryOfficers = Enumerable.Range(1, 100).Select(i => Off(i, 3, 0, i <= 10 ? "battalion" : "company", i)).ToList();
            t.Completed.Add("x");
            var rd = new MissionResults { MissionId = "x" };
            for (int i = 1; i <= 100; i++) rd.Deaths.Add(new MissionResults.OfficerDeath { Key = i, UnitId = i });
            t.Officers = new();
            OfficerIdentity.Settle(t, rd);
            int deadN = t.CampaignOfficers.Count(o => !o.AlwaysWounded && o.Status == "dead"), awDead = t.CampaignOfficers.Count(o => o.AlwaysWounded && o.Status == "dead");
            check(deadN == 50 && awDead == 0 && t.CampaignOfficers.Where(o => o.AlwaysWounded).All(o => o.Status == "wounded" && o.WoundedUntilMission == 2) && t.Officers.Count == 50,
                $"T48b1 전사 100건: 일반 dead {deadN}/50 · AlwaysWounded 사망 {awDead} · 이월 보존 {t.Officers.Count}");

            // 9) 진급은 Key 로 (UnitId 는 미션마다 바뀜) + 상한 도달 = 종착
            CampaignEconomyDefs.Override(new CampaignEconomyDefs { PromotionManual = true }.Sanitized());
            var p = new CampaignState { EntryMission = "m1" };
            p.Officers = new() { Off(5, cap - 1, 99999, key: 9), Off(9, 2, 0, key: 5) };
            p.CampaignOfficers.Add(new CampaignState.CampaignOfficer { Key = 9, Rank = cap - 1 });
            bool ok = CampaignPromotion.Promote(p, true, 0, 9);
            check(ok && p.Officers[0].OfficerRank == cap && p.Officers[1].OfficerRank == 2 && OfficerIdentity.Find(p, 9)!.TerminalReady && p.PromotionLog[^1].Key == 9 && p.PromotionLog[^1].UnitId == 5
                && CampaignPromotion.Candidates(p).All(c => c.Key != 9), "T48b1 진급 Key 전환 · 종착 (상한 도달 → TerminalReady, 더 이상 후보 아님)");
        }
        finally { CampaignEconomyDefs.Override(savedEco); }
    }
}
