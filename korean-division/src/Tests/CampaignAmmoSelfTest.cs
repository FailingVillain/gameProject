using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T19b 구경별 탄약 5종 (Q5) · 포탄 규칙 (Q6): 저장 v3 왕복 · v2 이행, 구경별 휴대 (부족 시 50%), 포탄 종류별 적재 · 실제 발사 수 차감 · 노획 가산,
/// 포 없는 미션 무영향, 미션 텍스트 (ammoScaleByWeapon) → 스폰 탄약. SaveCompatSelfTest 에서 부른다 (KD_SELFTEST_ONLY=savecompat).
/// </summary>
public static class CampaignAmmoSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        var E = CampaignEconomyDefs.Current;

        // 1) v2 저장 → v3 자동 이행 (구경 = ru, 옛 출격 기록의 탄약 = ru 한 종)
        var v2 = CampaignState.FromJson("{\"Version\":2,\"Roster\":[{\"Id\":\"b1_c1\",\"Kind\":\"infantry\",\"Strength\":60,\"MaxStrength\":60}],"
            + "\"Pending\":{\"MissionId\":\"c1_01_yalu\",\"DeployedCompanyIds\":[\"b1_c1\"],\"AmmoLoaded\":3600,\"AmmoScale\":1}}");
        check(v2 != null && v2.Version == CampaignState.SchemaVersion && CampaignState.SchemaVersion == 8 && v2.Roster[0].Caliber == StockKeys.Ru
            && v2.Pending!.AmmoByCaliber.GetValueOrDefault(StockKeys.Ru) == 3600 && v2.Pending.ShellsLoaded.Count == 0
            && StockKeys.AllCalibers.All(k => v2.Stock.Ammo.ContainsKey(k)) && StockKeys.AllShells.All(k => v2.Stock.Shells.ContainsKey(k)),
            "T19b 저장: v2 → v3 이행 (중대 구경 ru · 옛 출격 탄약 = ru · 구경 5종 · 포탄 3종 키)");

        // 2) v3 왕복 (구경별 재고 · 중대 구경 · 출격 기록 · 전과 보고)
        var s3 = new CampaignState();
        s3.Stock.Ammo[StockKeys.Fr] = 1234; s3.Stock.Shells[StockKeys.Field] = 77;
        s3.Roster.First(c => c.Id == "b1_c2").Caliber = StockKeys.De;
        s3.Pending = new CampaignState.PendingOperation { MissionId = "m", DeployedCompanyIds = new() { "b1_c2" }, AmmoByCaliber = new() { [StockKeys.De] = 10 }, ShellsLoaded = new() { [StockKeys.Howitzer] = 64 }, ShellScale = new() { [StockKeys.Howitzer] = 0.5f } };
        s3.LastDebrief = new CampaignState.DebriefReport { ShellsFired = new() { [StockKeys.InfGun] = 5 }, LootedShells = new() { [StockKeys.Field] = 2 } };
        var b3 = CampaignState.FromJson(s3.ToJson());
        check(b3 != null && b3.Stock.Ammo[StockKeys.Fr] == 1234 && b3.Stock.Shells[StockKeys.Field] == 77 && b3.Roster.First(c => c.Id == "b1_c2").Caliber == StockKeys.De
            && b3.Pending!.AmmoByCaliber[StockKeys.De] == 10 && b3.Pending.ShellsLoaded[StockKeys.Howitzer] == 64 && MathF.Abs(b3.Pending.ShellScale[StockKeys.Howitzer] - 0.5f) < 1e-6f
            && b3.LastDebrief!.ShellsFired[StockKeys.InfGun] == 5 && b3.LastDebrief.LootedShells[StockKeys.Field] == 2,
            "T19b 저장: v3 왕복 (구경별 재고 · 중대 구경 · 포탄 적재 · 발사/노획 보고)");
        var bad = CampaignState.FromJson("{\"Version\":3,\"Roster\":[{\"Id\":\"x\",\"Caliber\":\"zz\"}],\"Pending\":{\"MissionId\":\"m\",\"DeployedCompanyIds\":[],\"ShellScale\":{\"field\":7},\"ShellsLoaded\":{\"field\":-3}}}");
        check(bad != null && bad.Roster[0].Caliber == StockKeys.Ru && bad.Pending!.ShellScale["field"] == 1f && bad.Pending.ShellsLoaded["field"] == 0,
            "T19b 저장 보정: 모르는 구경 → ru, 포탄 비율 0..1 · 음수 적재 0");

        // 3) 구경별 휴대: 재고 충분 = 전부, 부족 = 기본 휴대량의 50% (그보다도 적으면 남은 만큼), 구경끼리 섞지 않음
        var sc = new CampaignState();
        sc.Roster.First(c => c.Id == "b1_c2").Caliber = StockKeys.Jp;
        sc.Roster.First(c => c.Id == "b1_c3").Caliber = StockKeys.De;
        sc.Stock.Ammo[StockKeys.Jp] = 2000;   // 3600 필요 → 50% = 1800
        sc.Stock.Ammo[StockKeys.De] = 1000;   // 3600 필요 → 50% 도 안 됨 → 1000
        int ru0 = sc.Stock.Ammo[StockKeys.Ru];
        CampaignEconomy.BeginOperation(sc, "m", new[] { "b1_c1", "b1_c2", "b1_c3", "mg" }, shellBase: new Dictionary<string, int>());
        var ab = sc.Pending!.AmmoByCaliber;
        int ruNeed = 60 * E.AmmoPerInfantry + 30 * E.AmmoPerMg;
        check(ab[StockKeys.Ru] == ruNeed && ab[StockKeys.Jp] == 1800 && ab[StockKeys.De] == 1000 && !ab.ContainsKey(StockKeys.Fr) && sc.Pending.AmmoLoaded == ruNeed + 2800
            && MathF.Abs(sc.Pending.AmmoScale - (float)(ruNeed + 2800) / (ruNeed + 7200)) < 1e-5f && sc.Stock.Ammo[StockKeys.Jp] == 2000,
            $"T19b 출격 탄약: 구경별 재고에서 채움, 부족 시 50% (ru {ab[StockKeys.Ru]} · jp {ab[StockKeys.Jp]} · de {ab[StockKeys.De]}), 출격 때 재고 불변");
        var rc = new MissionResults { MissionId = "m", Result = "lost" };
        rc.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 100, Alive = 50, Lost = 50 });
        CampaignEconomy.SettleBattle(sc, rc);
        check(sc.Stock.Ammo[StockKeys.Ru] == ru0 - ruNeed + (int)MathF.Round(ruNeed * 0.5f) && sc.Stock.Ammo[StockKeys.Jp] == 2000 - 1800 + 900 && sc.Stock.Ammo[StockKeys.De] == 1000 - 1000 + 500,
            "T19b 정산 탄약: 구경별 차감 · 생존 비율 환원 (같은 구경으로)");

        // 4) 포탄: 종류별 적재 (부족 50%) · 실제 발사 수 차감 (실은 수 한도) · 노획 = 적 손실 비례
        var ss = new CampaignState();
        ss.Stock.Shells[StockKeys.Howitzer] = 100;   // 128 필요 → 64
        ss.Stock.Shells[StockKeys.InfGun] = 500;     // 120 필요 → 전부
        var baseLoad = new Dictionary<string, int> { [StockKeys.Howitzer] = 128, [StockKeys.InfGun] = 120 };
        CampaignEconomy.BeginOperation(ss, "m", new[] { "b1_c1" }, shellBase: baseLoad);
        check(ss.Pending!.ShellsLoaded[StockKeys.Howitzer] == 64 && MathF.Abs(ss.Pending.ShellScale[StockKeys.Howitzer] - 0.5f) < 1e-6f
            && ss.Pending.ShellsLoaded[StockKeys.InfGun] == 120 && ss.Pending.ShellScale[StockKeys.InfGun] == 1f && !ss.Pending.ShellsLoaded.ContainsKey(StockKeys.Field)
            && ss.Stock.Shells[StockKeys.Howitzer] == 100, "T19b 출격 포탄: 종류별 재고에서 채움, 부족 시 50%, 출격 때 재고 불변");
        int field0 = ss.Stock.Shells[StockKeys.Field];
        var rs = new MissionResults { MissionId = "m", Result = "won", EnemyKills = 100 };
        rs.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 60, Alive = 60 });
        rs.PlayerGunShots["howitzer_heavy"] = 70;   // 실은 64 보다 많음 (전투 안 회복분) → 64 만
        rs.PlayerGunShots["gun_infantry"] = 33;
        rs.PlayerGunShots["rifle"] = 999;           // 포가 아닌 무기는 무시
        CampaignEconomy.SettleBattle(ss, rs);
        int lootH = (int)(100 * E.LootShellsPerKill[StockKeys.Howitzer]), lootI = (int)(100 * E.LootShellsPerKill[StockKeys.InfGun]), lootF = (int)(100 * E.LootShellsPerKill[StockKeys.Field]);
        var dbf = ss.LastDebrief!;
        check(ss.Stock.Shells[StockKeys.Howitzer] == 100 - 64 + lootH && ss.Stock.Shells[StockKeys.InfGun] == 500 - 33 + lootI && ss.Stock.Shells[StockKeys.Field] == field0 + lootF
            && dbf.ShellsFired[StockKeys.Howitzer] == 64 && dbf.ShellsFired[StockKeys.InfGun] == 33 && dbf.LootedShells.GetValueOrDefault(StockKeys.Howitzer) == lootH,
            $"T19b 정산 포탄: 발사 수 차감 (실은 수 한도) + 노획 적 손실 비례 (곡사포 {ss.Stock.Shells[StockKeys.Howitzer]})");

        // 5) 포 없는 미션: 포탄 적재 · 차감 · 미션 텍스트 모두 무영향
        var sn = new CampaignState();
        var shells0 = new Dictionary<string, int>(sn.Stock.Shells);
        CampaignEconomy.BeginOperation(sn, CampaignState.FirstMission, new[] { "b1_c1" });
        bool noGunBase = CampaignEconomy.ShellBaseLoad(CampaignState.FirstMission).Count == 0;
        string raw0 = File.ReadAllText(MissionDef.PathOf(CampaignState.FirstMission));
        sn.Pending!.AmmoScale = 0.5f;
        string patched0 = CampaignCarry.Patch(raw0, CampaignState.FirstMission, sn);
        var rn = new MissionResults { MissionId = CampaignState.FirstMission, Result = "lost" };
        rn.Factions.Add(new MissionResults.FactionLine { Id = 1, Player = true, Spawned = 60, Alive = 60 });
        CampaignEconomy.SettleBattle(sn, rn);
        check(noGunBase && !patched0.Contains("ammoScaleByWeapon") && StockKeys.AllShells.All(k => sn.Stock.Shells[k] == shells0[k]) && sn.LastDebrief!.ShellsFired.Count == 0,
            "T19b 포 없는 미션 (0-1): 포탄 적재 없음 · 미션 텍스트 그대로 · 포탄 재고 불변");

        // 6) 포 있는 미션 (1-1): 기본 휴대량 = 포 × 무기 ammo, 부족 비율이 미션 텍스트 → 스폰한 포의 탄약에 반영 (소총 비율과 따로)
        var yaluBase = CampaignEconomy.ShellBaseLoad(CampaignState.YaluCrossing);
        int howAmmo = Defs.Current.Weapons["howitzer_heavy"].Ammo;
        check(yaluBase.GetValueOrDefault(StockKeys.Howitzer) == 4 * howAmmo, $"T19b 기본 포탄 휴대량: 1-1 곡사포 4문 × {howAmmo} = {yaluBase.GetValueOrDefault(StockKeys.Howitzer)}");
        var sy = new CampaignState();
        sy.Stock.Shells[StockKeys.Howitzer] = 4 * howAmmo - 1;   // 부족 → 50%
        CampaignEconomy.BeginOperation(sy, CampaignState.YaluCrossing, new[] { "b1_c1", "b1_c2", "b1_c3" });
        string text = CampaignCarry.Patch(File.ReadAllText(MissionDef.PathOf(CampaignState.YaluCrossing)), CampaignState.YaluCrossing, sy);
        var def = MissionDef.Parse(text, CampaignState.YaluCrossing);
        var w = World.CreateMissionWorld(def, Defs.Current);
        var hows = w.Units.Where(u => u.Alive && u.Owner == def.Player && u.Weapon?.Key == "howitzer_heavy").ToList();
        var rifles = w.Units.Where(u => u.Alive && u.Owner == def.Player && u.Def.IsInfantry && u.Weapon is { Ammo: > 0 } wp && u.Ammo == wp.Ammo).Count();
        check(def.AmmoScaleByWeapon?.GetValueOrDefault("howitzer_heavy") == 0.5f && hows.Count == 4 && hows.All(u => u.Ammo == World.ScaleAmmo(howAmmo, 0.5f)) && rifles > 0,
            $"T19b 미션 반영: 곡사포 시작 탄약 = 50% ({(hows.Count > 0 ? hows[0].Ammo : -1)}/{howAmmo}), 소총은 그대로");

        // 7) 실제 발사 수 집계: 플레이어 포의 사격 1회 = 1, MissionResults 로 나간다 (시뮬 상태 아님)
        foreach (var u in hows.Take(2)) { w.ConsumeAmmo(u); w.ConsumeAmmo(u); }
        var mr = MissionResults.From(w);
        check(mr.PlayerGunShots.GetValueOrDefault("howitzer_heavy") == 4 && CampaignEconomy.ShellsFired(mr).GetValueOrDefault(StockKeys.Howitzer) == 4,
            $"T19b 발사 수 집계: 곡사포 4발 → 결과 {mr.PlayerGunShots.GetValueOrDefault("howitzer_heavy")}");
    }
}
