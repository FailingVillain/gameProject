namespace KoreanDivision.Core;

/// <summary>
/// 캠페인 경제 · 정산 순수 규칙 (docs/CAMPAIGN_MVP_SPEC.md §3, §7):
/// 투입 비용 · 치료 · 신병 충원 · 작전 개시 (탄약 적재) · 전투 정산 (사상/부상 25% 복귀/노획/정기 보급/패널티).
/// </summary>
public static class CampaignEconomy
{
    public static float UnitDeployCost(string kind) => kind switch
    {
        "infantry" or "recruit" or "guard" => 1.0f,
        "assault" or "sniper" or "infantry_assault" or "infantry_sniper" => 1.5f,
        "mg" or "heavy_maxim" or "heavy_machinegunner" => 2.0f,
        "cavalry" => 3.0f,
        "infGun" or "heavy_infantry_gun" => 5.0f,
        "fieldGun" or "heavy_field_gun" => 7.0f,
        "howitzer" or "heavy_howitzer" => 9.0f,
        "armoredCar" or "tank" => 12.0f,
        "armoredTrain" => 40.0f,
        _ => 1.0f
    };

    public static float CompanyDeployCost(CampaignState.RosterCompany c, float scaleMul = 1.0f) =>
        c.Strength * UnitDeployCost(c.Kind) * scaleMul;

    public static float TotalDeployCost(IEnumerable<CampaignState.RosterCompany> selected, float scaleMul = 1.0f) =>
        selected.Sum(c => CompanyDeployCost(c, scaleMul));

    /// <summary>작전 투입 비용 (상한 전): 대규모 투입 (LargeDeployThreshold 초과) 이면 규모 배수 (§1-1). BeginOperation · 병참 탭 미리보기 공용.</summary>
    public static float DeployCost(IReadOnlyCollection<CampaignState.RosterCompany> selected, int maxDeploy)
    {
        float scaleMul = (maxDeploy > 0 && selected.Sum(c => c.Strength) > CampaignEconomyDefs.Current.LargeDeployThreshold) ? CampaignEconomyDefs.Current.LargeDeployScaleMul : 1.0f;
        return TotalDeployCost(selected, scaleMul);
    }

    public static int TotalRifles(CampaignState s) => s.Stock.Rifles.Values.Sum();

    /// <summary>
    /// 부상자 치료 (1명당 보급품 0.5f).
    /// RosterCompany 의 부상자를 먼저 치료해 Strength 로 복귀시키고 (MaxStrength 초과분은 Recruits 로),
    /// 남은 여력으로 Manpower.Wounded 를 치료해 Recruits 로 복귀시킨다. 치료 인원 반환.
    /// </summary>
    public static int HealWounded(CampaignState s, int maxCount = int.MaxValue)
    {
        int totalWounded = s.Roster.Sum(c => c.Wounded) + s.Manpower.Wounded;
        int maxAffordable = (int)(s.Supply / CampaignEconomyDefs.Current.HealCostPerMan);
        int budget = Math.Min(totalWounded, Math.Min(maxCount, maxAffordable));
        if (budget <= 0) return 0;

        int healedTotal = 0;
        // 1) Roster 중대 부상자 치료
        foreach (var c in s.Roster)
        {
            if (healedTotal >= budget) break;
            if (c.Wounded <= 0) continue;

            int heal = Math.Min(c.Wounded, budget - healedTotal);
            c.Wounded -= heal;
            c.UnhealedOps = 0;
            int space = Math.Max(0, c.MaxStrength - c.Strength);
            int toStrength = Math.Min(heal, space);
            c.Strength += toStrength;
            int overflow = heal - toStrength;
            if (overflow > 0)
                s.Manpower.Recruits = Math.Min(CampaignState.ManpowerState.MaxRecruits, s.Manpower.Recruits + overflow);

            healedTotal += heal;
        }

        // 2) 사단 공용 부상자 풀(Manpower.Wounded) 치료
        if (healedTotal < budget && s.Manpower.Wounded > 0)
        {
            int heal = Math.Min(s.Manpower.Wounded, budget - healedTotal);
            s.Manpower.Wounded -= heal;
            s.Manpower.Recruits = Math.Min(CampaignState.ManpowerState.MaxRecruits, s.Manpower.Recruits + heal);
            healedTotal += heal;
        }

        s.Supply = Math.Max(0f, s.Supply - healedTotal * CampaignEconomyDefs.Current.HealCostPerMan);
        return healedTotal;
    }

    /// <summary>
    /// 신병을 중대에 배치: 1명당 보급품 0.5f + 소총 1정 ("ru" 우선 차감).
    /// 실제 배치된 인원 수 반환 (자원/소총/정원 부족 시 가능한 만큼만).
    /// </summary>
    public static int AssignRecruits(CampaignState s, string companyId, int requestedCount = 100)
    {
        var c = s.Roster.FirstOrDefault(x => x.Id == companyId);
        if (c == null) return 0;

        int space = Math.Max(0, c.MaxStrength - c.Strength);
        int supplyLimit = (int)(s.Supply / CampaignEconomyDefs.Current.RecruitAssignCostPerMan);
        int riflesAvail = TotalRifles(s);
        int n = Math.Min(requestedCount, Math.Min(space, Math.Min(s.Manpower.Recruits, Math.Min(riflesAvail, supplyLimit))));
        if (n <= 0) return 0;

        c.Strength += n;
        s.Manpower.Recruits -= n;
        s.Supply = Math.Max(0f, s.Supply - n * CampaignEconomyDefs.Current.RecruitAssignCostPerMan);

        // 소총 차감 ("ru" 우선)
        int needed = n;
        if (s.Stock.Rifles.TryGetValue(StockKeys.Ru, out int ruCount) && ruCount > 0)
        {
            int take = Math.Min(ruCount, needed);
            s.Stock.Rifles[StockKeys.Ru] -= take;
            needed -= take;
        }
        if (needed > 0)
        {
            foreach (var k in s.Stock.Rifles.Keys.ToList())
            {
                if (needed <= 0) break;
                int cnt = s.Stock.Rifles[k];
                if (cnt <= 0) continue;
                int take = Math.Min(cnt, needed);
                s.Stock.Rifles[k] -= take;
                needed -= take;
            }
        }

        return n;
    }

    /// <summary>재고에서 휴대량 채우기 (Q5 · Q6, 탄약 · 포탄 같은 규칙): 재고가 기본 휴대량 이상이면 전부, 모자라면 기본 휴대량의 ShortLoadRatio (50%) — 재고가 그보다도 적으면 남은 재고만큼.</summary>
    public static int FillLoad(int need, int stock)
    {
        if (need <= 0) return 0;
        stock = Math.Max(0, stock);
        if (stock >= need) return need;
        int part = (int)Math.Ceiling(need * (double)Math.Clamp(CampaignEconomyDefs.Current.ShortLoadRatio, 0f, 1f));
        return Math.Min(stock, part);
    }

    /// <summary>중대의 탄약 기본 휴대량 (보병 1명당 60발, 기관총 200발; 그 외 병과 0).</summary>
    public static int CompanyAmmoNeed(CampaignState.RosterCompany c) => c.Kind switch
    {
        "infantry" or "recruit" or "guard" or "assault" or "sniper" => c.Strength * CampaignEconomyDefs.Current.AmmoPerInfantry,
        "mg" => c.Strength * CampaignEconomyDefs.Current.AmmoPerMg,
        _ => 0
    };

    /// <summary>[Q6] 미션의 플레이어 포 (시작 병력 + 증원) 기본 포탄 휴대량 — 포탄 종류별 (포 수 × 그 무기 balance.json ammo). 포 없는 미션 = 빈 표.</summary>
    public static Dictionary<string, int> ShellBaseLoad(MissionDef def)
    {
        var res = new Dictionary<string, int>();
        var map = CampaignEconomyDefs.Current.ShellWeapons;
        foreach (var f in def.Forces.Concat(def.Reinforcements))
        {
            if (f.Faction != def.Player) continue;
            foreach (var c in f.AllCompanies())
                foreach (var (unitKey, n) in c.Units)
                {
                    if (n <= 0 || !Defs.Current.Units.TryGetValue(unitKey, out var ud) || ud.Weapon is not { } wk) continue;
                    if (!map.TryGetValue(wk, out var shell) || !Defs.Current.Weapons.TryGetValue(wk, out var wd) || wd.Ammo <= 0) continue;
                    res[shell] = res.GetValueOrDefault(shell) + n * wd.Ammo;
                }
        }
        return res;
    }

    /// <summary>미션 파일의 기본 포탄 휴대량 (파일이 없거나 깨졌으면 빈 표 = 포탄 무영향).</summary>
    public static Dictionary<string, int> ShellBaseLoad(string missionId)
    {
        try { return ShellBaseLoad(MissionDef.Parse(File.ReadAllText(MissionDef.PathOf(missionId)), missionId)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException) { return new(); }
    }

    /// <summary>
    /// 작전 개시: 투입 비용 · 구경별 탄약 적재 (Q5) · 포탄 종류별 적재 (Q6) · 의무 중대 거부 패널티를 PendingOperation 에 기록.
    /// shellBase = 포탄 기본 휴대량 (null 이면 미션 파일에서 ShellBaseLoad).
    /// </summary>
    public static bool BeginOperation(CampaignState s, string missionId, IReadOnlyList<string> deployedIds, int maxDeploy = 0, IReadOnlyDictionary<string, int>? shellBase = null)
    {
        var selected = s.Roster.Where(c => deployedIds.Contains(c.Id)).ToList();
        if (CampaignFunds.CheckControl(selected) != null) return false;   // [TC-D] 통제 한도 · 외인 슬롯 초과 편성은 시작하지 않는다 (상태 불변)

        // 출격 직전에는 상태를 바꾸지 않는다: 비용 · 탄약 · 포탄 · 거부 패널티는 Pending 에 기록만 하고
        // 전투 결과가 확정될 때 SettleBattle 이 한 번에 반영한다 (출격 후 강제 종료 · 포기해도 자원이 사라지지 않음).
        bool refusedMandatory = s.Roster.Any(c => c.Mandatory && !deployedIds.Contains(c.Id));

        float cost = Math.Min(s.Supply, DeployCost(selected, maxDeploy));

        // 구경별 탄약 (전투 안에서는 구경 구분 없음 → 미션에는 합친 비율 하나)
        var ammoBy = new Dictionary<string, int>();
        int neededAmmo = 0, ammoLoaded = 0;
        foreach (var cal in StockKeys.AllCalibers)
        {
            int need = selected.Where(c => c.Caliber == cal).Sum(CompanyAmmoNeed);
            if (need <= 0) continue;
            int load = FillLoad(need, s.Stock.Ammo.GetValueOrDefault(cal, 0));
            neededAmmo += need; ammoLoaded += load;
            ammoBy[cal] = load;
        }
        float ammoScale = neededAmmo > 0 ? (float)ammoLoaded / neededAmmo : 1.0f;

        // 포탄 종류별
        var shellsLoaded = new Dictionary<string, int>();
        var shellScale = new Dictionary<string, float>();
        foreach (var (shell, need) in shellBase ?? ShellBaseLoad(missionId))
        {
            if (need <= 0) continue;
            int load = FillLoad(need, s.Stock.Shells.GetValueOrDefault(shell, 0));
            shellsLoaded[shell] = load;
            shellScale[shell] = (float)load / need;
        }

        s.Pending = new CampaignState.PendingOperation
        {
            MissionId = missionId,
            DeployedCompanyIds = deployedIds.ToList(),
            SupplySpent = cost,
            AmmoLoaded = ammoLoaded,
            AmmoScale = ammoScale,
            AmmoByCaliber = ammoBy,
            ShellsLoaded = shellsLoaded,
            ShellScale = shellScale,
            RefusedMandatory = refusedMandatory,
            EpisodeSupplyReserved = DeployCost(selected, maxDeploy)   // [TC-D] 출발 시 예약
        };
        return true;
    }

    /// <summary>
    /// 황손/총사령관 심기에 따른 신병 및 보급 패널티 (합산 절대 상한 30%).
    /// </summary>
    public static (float RecruitPenalty, float SupplyPenalty) ComputePenalty(CampaignState s)
    {
        float recruitPenalty = 0f;
        int heirMood = s.Flags.GetValueOrDefault("heir_mood", 60);
        if (heirMood < 20) recruitPenalty = 0.20f;
        else if (heirMood < 40) recruitPenalty = 0.10f;
        recruitPenalty = Math.Clamp(recruitPenalty, 0f, CampaignEconomyDefs.Current.PenaltyCap);

        float supplyPenalty = 0f;
        int cmdFavor = s.Flags.GetValueOrDefault("cmd_favor", 50);
        if (cmdFavor < 20) supplyPenalty = 0.20f;
        else if (cmdFavor < 40) supplyPenalty = 0.10f;

        float extraSupply = s.Flags.GetValueOrDefault("next_supply_penalty_pct", 0) / 100f;
        supplyPenalty += extraSupply;
        supplyPenalty = Math.Clamp(supplyPenalty, 0f, CampaignEconomyDefs.Current.PenaltyCap);

        return (recruitPenalty, supplyPenalty);
    }

    /// <summary>
    /// 전투 후 정산: 미치료 부상자 경과/전역, 아군 손실 25% 부상 복귀, 탄약 환원, 적 노획, 정기 보급, LastDebrief.
    /// </summary>
    /// <summary>노획 소총 · 탄약이 들어갈 구경 키 (적 세력 이름에 일본이 있으면 jp, 아니면 ru — 이름 판별은 여기 한 곳).</summary>
    public static string EnemyCaliber(MissionResults r) =>
        r.Factions.Any(f => !f.Player && f.Name.Contains("일본")) ? StockKeys.Jp : StockKeys.Ru;

    /// <summary>이미 이긴 미션의 반복 승리 (D23): 자원 · 병력 변화 없이 결과 요약만 남기고 출격 기록(Pending)을 버린다.</summary>
    public static void RecordRepeatVictory(CampaignState s, MissionResults r)
    {
        var me = r.Factions.FirstOrDefault(f => f.Player);
        s.LastDebrief = new CampaignState.DebriefReport
        {
            MissionId = r.MissionId, Title = r.Title, TotalLost = me?.Lost ?? 0, EnemyKills = r.EnemyKills,
            SupplyBefore = s.Supply, SupplyAfter = s.Supply
        };
        s.Pending = null;
    }

    /// <summary>[Q6] 전투 결과의 플레이어 포 발사 수 → 포탄 종류별 (무기 → 종류 표 = campaign_economy.json shellWeapons).</summary>
    public static Dictionary<string, int> ShellsFired(MissionResults r)
    {
        var res = new Dictionary<string, int>();
        foreach (var (wk, n) in r.PlayerGunShots)
            if (n > 0 && CampaignEconomyDefs.Current.ShellWeapons.TryGetValue(wk, out var shell)) res[shell] = res.GetValueOrDefault(shell) + n;
        return res;
    }

    /// <summary>[T42a] 중대 피로 · 사기 정산 (정수, 로스터 순서 — 결정적). lossRatio = 잃은 수 / 투입 수.</summary>
    public static void SettleFatigueMorale(CampaignState s, IReadOnlyCollection<CampaignState.RosterCompany> deployed, bool won, float lossRatio)
    {
        var d = CampaignEconomyDefs.Current;
        int moraleDeltaDeployed = (won ? d.MoraleWin : -d.MoraleLoss) - (int)MathF.Round(Math.Clamp(lossRatio, 0f, 1f) * d.MoraleCasualtyScale);
        foreach (var c in s.Roster)
        {
            if (deployed.Contains(c))
            {
                c.Fatigue = Math.Clamp(c.Fatigue + d.FatiguePerDeploy, 0, 100);
                c.Morale = Math.Clamp(c.Morale + moraleDeltaDeployed, 0, 100);
            }
            else
            {
                c.Fatigue = Math.Clamp(c.Fatigue - d.FatigueRestRecovery, 0, 100);
                c.Morale = Math.Clamp(c.Morale + d.MoraleRestRecovery, 0, 100);
            }
        }
    }

    /// <summary>[T42a] 지침 (피로 문턱 이상) 인가 — 홈 표시 · 투입 경고용. 잠금 아님.</summary>
    public static bool IsTired(CampaignState.RosterCompany c) => c.Fatigue >= CampaignEconomyDefs.Current.FatigueWarnThreshold;

    public static void SettleBattle(CampaignState s, MissionResults r)
    {
        // 0) 출격 때 기록해 둔 비용 · 탄약 · 거부 패널티를 결과 확정 시점에 반영 (원자적 커밋)
        if (s.Pending != null && s.Pending.MissionId == r.MissionId)
        {
            s.Supply = Math.Max(0f, s.Supply - s.Pending.SupplySpent);
            foreach (var (cal, n) in s.Pending.AmmoByCaliber)
                s.Stock.Ammo[cal] = Math.Max(0, s.Stock.Ammo.GetValueOrDefault(cal, 0) - n);
            if (s.Pending.RefusedMandatory)
            {
                s.Flags["heir_mood"] = Math.Clamp(s.Flags.GetValueOrDefault("heir_mood", 60) - 10, 0, 100);
                s.Flags["cmd_favor"] = Math.Clamp(s.Flags.GetValueOrDefault("cmd_favor", 50) - 5, 0, 100);
            }
        }
        else s.Pending = null;

        // 1) 직전 미션 미치료 부상자 UnhealedOps++, 3회 이상이면 전역 처리
        int discharged = 0;
        foreach (var c in s.Roster)
        {
            if (c.Wounded > 0)
            {
                c.UnhealedOps++;
                if (c.UnhealedOps >= 3)
                {
                    discharged += c.Wounded;
                    c.Wounded = 0;
                    c.UnhealedOps = 0;
                }
            }
        }

        // 2) 아군 손실: 25% 부상 복귀, 나머지 전사
        var me = r.Factions.FirstOrDefault(f => f.Player);
        int totalLost = me?.Lost ?? 0;
        int woundedReturned, dead;
        var deployed = s.Pending == null ? new List<CampaignState.RosterCompany>()
            : s.Roster.Where(c => s.Pending.DeployedCompanyIds.Contains(c.Id)).ToList();
        if (deployed.Count > 0 && me != null && me.Spawned > 0)
        {
            // 출격 편성이 있으면 전투 손실 비율 (잃은 수 / 투입 수) 을 출격 중대 병력에 그대로 적용한다 (정수 반올림, 로스터 순서 — 결정적).
            // 부상 복귀 25% 는 그 중대의 부상자 (치료 → 같은 중대 복귀), 나머지는 전사. 보고 숫자도 사단 편성 기준.
            int lostFrac = Math.Clamp(totalLost, 0, me.Spawned);
            totalLost = 0; woundedReturned = 0;
            foreach (var c in deployed)
            {
                int lostC = Math.Min(c.Strength, (int)(((long)c.Strength * lostFrac + me.Spawned / 2) / me.Spawned));
                int woundC = (int)MathF.Round(lostC * CampaignEconomyDefs.Current.WoundReturnRatio);
                c.Strength -= lostC;
                c.Wounded += woundC;
                totalLost += lostC; woundedReturned += woundC;
            }
            dead = totalLost - woundedReturned;
        }
        else
        {
            woundedReturned = (int)MathF.Round(totalLost * CampaignEconomyDefs.Current.WoundReturnRatio);
            dead = totalLost - woundedReturned;
            s.Manpower.Wounded += woundedReturned;
        }

        // 2-1) [T42a] 피로 · 사기 계정: 투입 중대 피로 +, 미투입 −. 사기 = 승패 ± 손실률. 출격 편성이 있을 때만 (홈 계정, 시뮬 무관)
        if (deployed.Count > 0) SettleFatigueMorale(s, deployed, r.Result == "won", me != null && me.Spawned > 0 ? Math.Clamp((float)me.Lost / me.Spawned, 0f, 1f) : 0f);

        // 2-2) [TC-D] 에피소드 보급: 예약 환원, 영구 소모 (전사자 보충 + 투입 비용 10%) 누적
        CampaignFunds.SettleReservation(s, dead);

        // 3) 탄약 환원: Pending 에 적재된 탄약 중 생존 비율만큼 같은 구경으로 복귀
        int ammoRefunded = 0;
        if (s.Pending != null && s.Pending.AmmoLoaded > 0)
        {
            float survRatio = (me != null && me.Spawned > 0) ? Math.Clamp((float)me.Alive / me.Spawned, 0f, 1f) : 1f;
            foreach (var (cal, n) in s.Pending.AmmoByCaliber)
            {
                int back = (int)MathF.Round(n * survRatio);
                s.Stock.Ammo[cal] = s.Stock.Ammo.GetValueOrDefault(cal, 0) + back;
                ammoRefunded += back;
            }
        }

        // 3-1) 포탄 (Q6): 실제 발사 수를 재고에서 뺀다 (실은 수가 한도 — 전투 안 회복분은 재고 밖). 포 없는 미션 = 발사 0 → 무영향
        var shellsFired = new Dictionary<string, int>();
        if (s.Pending != null)
        {
            var fired = ShellsFired(r);
            foreach (var shell in StockKeys.AllShells)
            {
                int n = fired.GetValueOrDefault(shell);
                if (s.Pending.ShellsLoaded.TryGetValue(shell, out int loaded)) n = Math.Min(n, loaded);
                if (n <= 0) continue;
                int stock = s.Stock.Shells.GetValueOrDefault(shell, 0);
                n = Math.Min(n, stock);
                s.Stock.Shells[shell] = stock - n;
                shellsFired[shell] = n;
            }
        }

        // 4) 노획 (승리 시)
        float lootedSupply = 0f;
        int lootedRifles = 0;
        int lootedAmmo = 0;
        int lootedHorses = 0;
        var lootedShells = new Dictionary<string, int>();
        int prisoners = 0;
        if (r.Result == "won")
        {
            lootedSupply = r.EnemyKills * CampaignEconomyDefs.Current.LootSupplyPerKill;
            lootedRifles = (int)(r.EnemyKills * CampaignEconomyDefs.Current.LootRiflesPerKill);
            lootedAmmo = r.EnemyKills * CampaignEconomyDefs.Current.LootAmmoPerKill;
            string enemyCaliber = EnemyCaliber(r);
            s.Supply += lootedSupply;
            s.Stock.Rifles[enemyCaliber] = s.Stock.Rifles.GetValueOrDefault(enemyCaliber, 0) + lootedRifles;
            s.Stock.Ammo[enemyCaliber] = s.Stock.Ammo.GetValueOrDefault(enemyCaliber, 0) + lootedAmmo;
            prisoners = Math.Max(0, r.EnemyCaptured);   // T42c: 포로 = 항복한 적 (꺼져 있으면 0)
            s.Stock.Prisoners += prisoners;
            foreach (var shell in StockKeys.AllShells)   // 노획 포탄 = 적 손실 비례 (Q6)
            {
                int n = (int)(r.EnemyKills * CampaignEconomyDefs.Current.LootShellsPerKill.GetValueOrDefault(shell, 0f));
                if (n <= 0) continue;
                s.Stock.Shells[shell] = s.Stock.Shells.GetValueOrDefault(shell, 0) + n;
                lootedShells[shell] = n;
            }
        }

        // 5) 정기 보급 (기본 보급 +1500, 신병 +1000, 패널티 상한 30%)
        float supplyBefore = s.Supply;
        var (recruitPenalty, supplyPenalty) = ComputePenalty(s);
        s.Flags.Remove("next_supply_penalty_pct");

        float regularSupply = CampaignEconomyDefs.Current.RegularSupply * (1.0f - supplyPenalty);
        int regularRecruits = (int)MathF.Round(CampaignEconomyDefs.Current.RegularRecruits * (1.0f - recruitPenalty));
        s.Supply += regularSupply;
        s.Manpower.Recruits = Math.Min(CampaignState.ManpowerState.MaxRecruits, s.Manpower.Recruits + regularRecruits);

        // 6) 입고 로그 갱신 (최대 5건)
        string logEntry = $"[{r.MissionId}] 정산: 보급 +{regularSupply:0}(노획 +{lootedSupply:0}), 신병 +{regularRecruits}, 탄약환원 +{ammoRefunded}, 포탄 발사 -{shellsFired.Values.Sum()} 노획 +{lootedShells.Values.Sum()}" + (prisoners > 0 ? $", 포로 +{prisoners}" : "");
        s.Stock.RecentLog.Add(logEntry);
        while (s.Stock.RecentLog.Count > 5) s.Stock.RecentLog.RemoveAt(0);

        // 7) DebriefReport 기록 및 Pending 정리
        s.LastDebrief = new CampaignState.DebriefReport
        {
            MissionId = r.MissionId,
            Title = r.Title,
            TotalLost = totalLost,
            WoundedReturned = woundedReturned,
            Dead = dead,
            DischargedWounded = discharged,
            EnemyKills = r.EnemyKills,
            SupplyBefore = supplyBefore,
            SupplyAfter = s.Supply,
            RegularSupplyAdded = regularSupply,
            RecruitsAdded = regularRecruits,
            LootedSupply = lootedSupply,
            LootedRifles = lootedRifles,
            LootedAmmo = lootedAmmo,
            LootedHorses = lootedHorses,
            AmmoRefunded = ammoRefunded,
            ShellsFired = shellsFired,
            LootedShells = lootedShells,
            PrisonersTaken = prisoners
        };
        s.Pending = null;

        // 8) T42d: 작전 일수 + 연표 (첫 승리 정산만 — SettleBattle 은 첫 승리에서만 불린다)
        if (s.Day <= 0) s.Day = 1;   // 새 캠페인 = 1일째 시작
        s.Day = (int)Math.Min(CampaignState.DayMax, (long)s.Day + CampaignEconomyDefs.Current.MissionDays);
        s.AddTimeline("mission", r.MissionId, r.Title ?? "");
    }
}
