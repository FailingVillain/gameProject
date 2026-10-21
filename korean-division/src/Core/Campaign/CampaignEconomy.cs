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

    public static int TotalRifles(CampaignState s) => s.Stock.Rifles.Values.Sum();

    /// <summary>
    /// 부상자 치료 (1명당 보급품 0.5f).
    /// RosterCompany 의 부상자를 먼저 치료해 Strength 로 복귀시키고 (MaxStrength 초과분은 Recruits 로),
    /// 남은 여력으로 Manpower.Wounded 를 치료해 Recruits 로 복귀시킨다. 치료 인원 반환.
    /// </summary>
    public static int HealWounded(CampaignState s, int maxCount = int.MaxValue)
    {
        int totalWounded = s.Roster.Sum(c => c.Wounded) + s.Manpower.Wounded;
        int maxAffordable = (int)(s.Supply / 0.5f);
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

        s.Supply = Math.Max(0f, s.Supply - healedTotal * 0.5f);
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
        int supplyLimit = (int)(s.Supply / 0.5f);
        int riflesAvail = TotalRifles(s);
        int n = Math.Min(requestedCount, Math.Min(space, Math.Min(s.Manpower.Recruits, Math.Min(riflesAvail, supplyLimit))));
        if (n <= 0) return 0;

        c.Strength += n;
        s.Manpower.Recruits -= n;
        s.Supply = Math.Max(0f, s.Supply - n * 0.5f);

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

    /// <summary>
    /// 작전 개시: 투입 비용 차감, 탄약 적재, 의무 중대 거부 패널티, PendingOperation 기록.
    /// </summary>
    public static void BeginOperation(CampaignState s, string missionId, IReadOnlyList<string> deployedIds, int maxDeploy = 0)
    {
        var selected = s.Roster.Where(c => deployedIds.Contains(c.Id)).ToList();

        // 의무 포함 중대(Mandatory) 미포함 여부 확인
        // 출격 직전에는 상태를 바꾸지 않는다: 비용 · 탄약 · 거부 패널티는 Pending 에 기록만 하고
        // 전투 결과가 확정될 때 SettleBattle 이 한 번에 반영한다 (출격 후 강제 종료 · 포기해도 자원이 사라지지 않음).
        bool refusedMandatory = s.Roster.Any(c => c.Mandatory && !deployedIds.Contains(c.Id));

        float scaleMul = (maxDeploy > 0 && selected.Sum(c => c.Strength) > 180) ? 1.5f : 1.0f;
        float cost = Math.Min(s.Supply, TotalDeployCost(selected, scaleMul));

        // 소총탄 기본 휴대량 (보병 1명당 60발, 기관총 200발)
        int neededAmmo = selected.Where(c => c.Kind is "infantry" or "recruit" or "guard" or "assault" or "sniper").Sum(c => c.Strength) * 60;
        neededAmmo += selected.Where(c => c.Kind is "mg").Sum(c => c.Strength) * 200;

        int availAmmo = s.Stock.Ammo.GetValueOrDefault(StockKeys.Ru, 0);
        int ammoLoaded = 0;
        float ammoScale = 1.0f;
        if (neededAmmo > 0)
        {
            if (availAmmo >= neededAmmo)
            {
                ammoLoaded = neededAmmo;
                ammoScale = 1.0f;
            }
            else
            {
                ammoLoaded = availAmmo;
                ammoScale = (float)availAmmo / neededAmmo;
            }
        }

        s.Pending = new CampaignState.PendingOperation
        {
            MissionId = missionId,
            DeployedCompanyIds = deployedIds.ToList(),
            SupplySpent = cost,
            AmmoLoaded = ammoLoaded,
            AmmoScale = ammoScale,
            RefusedMandatory = refusedMandatory
        };
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
        recruitPenalty = Math.Clamp(recruitPenalty, 0f, 0.30f);

        float supplyPenalty = 0f;
        int cmdFavor = s.Flags.GetValueOrDefault("cmd_favor", 50);
        if (cmdFavor < 20) supplyPenalty = 0.20f;
        else if (cmdFavor < 40) supplyPenalty = 0.10f;

        float extraSupply = s.Flags.GetValueOrDefault("next_supply_penalty_pct", 0) / 100f;
        supplyPenalty += extraSupply;
        supplyPenalty = Math.Clamp(supplyPenalty, 0f, 0.30f);

        return (recruitPenalty, supplyPenalty);
    }

    /// <summary>
    /// 전투 후 정산: 미치료 부상자 경과/전역, 아군 손실 25% 부상 복귀, 탄약 환원, 적 노획, 정기 보급, LastDebrief.
    /// </summary>
    /// <summary>노획 소총 · 탄약이 들어갈 구경 키 (적 세력 이름에 일본이 있으면 jp, 아니면 ru — 이름 판별은 여기 한 곳).</summary>
    public static string EnemyCaliber(MissionResults r) =>
        r.Factions.Any(f => !f.Player && f.Name.Contains("일본")) ? StockKeys.Jp : StockKeys.Ru;

    /// <param name="firstWin">false = 이미 끝낸 미션을 다시 이김 → 정기 보급 · 신병 없음 (D10 A, 2026-10-22).</param>
    public static void SettleBattle(CampaignState s, MissionResults r, bool firstWin = true)
    {
        // 0) 출격 때 기록해 둔 비용 · 탄약 · 거부 패널티를 결과 확정 시점에 반영 (원자적 커밋)
        if (s.Pending != null && s.Pending.MissionId == r.MissionId)
        {
            s.Supply = Math.Max(0f, s.Supply - s.Pending.SupplySpent);
            s.Stock.Ammo[StockKeys.Ru] = Math.Max(0, s.Stock.Ammo.GetValueOrDefault(StockKeys.Ru, 0) - s.Pending.AmmoLoaded);
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
                int woundC = (int)MathF.Round(lostC * 0.25f);
                c.Strength -= lostC;
                c.Wounded += woundC;
                totalLost += lostC; woundedReturned += woundC;
            }
            dead = totalLost - woundedReturned;
        }
        else
        {
            woundedReturned = (int)MathF.Round(totalLost * 0.25f);
            dead = totalLost - woundedReturned;
            s.Manpower.Wounded += woundedReturned;
        }

        // 3) 탄약 환원: Pending 에 적재된 탄약 중 생존 비율만큼 복귀
        int ammoRefunded = 0;
        if (s.Pending != null && s.Pending.AmmoLoaded > 0)
        {
            float survRatio = (me != null && me.Spawned > 0) ? Math.Clamp((float)me.Alive / me.Spawned, 0f, 1f) : 1f;
            ammoRefunded = (int)MathF.Round(s.Pending.AmmoLoaded * survRatio);
            s.Stock.Ammo[StockKeys.Ru] = s.Stock.Ammo.GetValueOrDefault(StockKeys.Ru, 0) + ammoRefunded;
        }

        // 4) 노획 (승리 시)
        float lootedSupply = 0f;
        int lootedRifles = 0;
        int lootedAmmo = 0;
        int lootedHorses = 0;
        if (r.Result == "won")
        {
            lootedSupply = r.EnemyKills * 0.3f;
            lootedRifles = (int)(r.EnemyKills * 0.5f);
            lootedAmmo = r.EnemyKills * 20;
            string enemyCaliber = EnemyCaliber(r);
            s.Supply += lootedSupply;
            s.Stock.Rifles[enemyCaliber] = s.Stock.Rifles.GetValueOrDefault(enemyCaliber, 0) + lootedRifles;
            s.Stock.Ammo[enemyCaliber] = s.Stock.Ammo.GetValueOrDefault(enemyCaliber, 0) + lootedAmmo;
        }

        // 5) 정기 보급 (기본 보급 +1500, 신병 +1000, 패널티 상한 30%)
        float supplyBefore = s.Supply;
        var (recruitPenalty, supplyPenalty) = ComputePenalty(s);
        if (firstWin) s.Flags.Remove("next_supply_penalty_pct");   // 보급이 안 오는 재승리는 다음 보급 감액을 소모하지 않는다

        float regularSupply = firstWin ? 1500f * (1.0f - supplyPenalty) : 0f;
        int regularRecruits = firstWin ? (int)MathF.Round(1000 * (1.0f - recruitPenalty)) : 0;
        s.Supply += regularSupply;
        s.Manpower.Recruits = Math.Min(CampaignState.ManpowerState.MaxRecruits, s.Manpower.Recruits + regularRecruits);

        // 6) 입고 로그 갱신 (최대 5건)
        string logEntry = $"[{r.MissionId}] 정산: 보급 +{regularSupply:0}(노획 +{lootedSupply:0}), 신병 +{regularRecruits}, 탄약환원 +{ammoRefunded}";
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
            AmmoRefunded = ammoRefunded
        };
        s.Pending = null;
    }
}
