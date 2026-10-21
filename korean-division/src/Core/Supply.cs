using System.Diagnostics;
using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 탄약 회복 · 보급 마차 · 엄폐 배수 (방향 변경 ROADMAP §0-1). 세력 재고 · 연료 · 보급 시설 없음.
///  · 사격 1회 = 탄약 1 (ConsumeAmmo), 탄약 0 = 교전 불가 (이동은 가능).
///  · 회복: 마지막 사격 · 피격 (빗나감 포함) 뒤 supply.regenDelay 초가 지나면 supply.interval 마다 (유닛 Id 로 틱 분산) 한도 × regenPct × interval.
///    자기 · 동맹 도시 (BuildingDef.City) 가장자리 cityRadius 칸 안 ×cityMul, 적재량이 남은 자기 보급 마차 wagonRadius 칸 안 ×wagonMul — 중첩 없이 큰 쪽
///    (마차가 이기면 가속분만큼 마차 적재량 감소: 짐 1 = 병사 1명의 탄약 한도 전체). 보급 마차는 아군 도시 근처에서 wagonRefill 짐/초 다시 싣는다.
///  · 엄폐: 칸 지형 (GameMap.TerrainCover) 만 — 개인 참호 · 참호선 파기 (건설) 는 2026-10-19 제거, 참호 지형 자동 점유는 Trenches.cs.
/// 틱 안의 실행 위치: World.Step → UpdateSupply. 모든 순회는 Units/Buildings 목록 순 — 결정적.
/// </summary>
public sealed partial class World
{
    /// <summary>[측정 훅] UpdateSupply 누적 Stopwatch 틱 (Profile 일 때, StressTest 가 읽고 비운다).</summary>
    public static long SupplyProf;

    /// <summary>[테스트 전용] true 면 탄약을 쓰지 않는다 (환경 변수 KD_INF_AMMO=1).</summary>
    internal static bool DebugInfiniteAmmo = Environment.GetEnvironmentVariable("KD_INF_AMMO") == "1";

    // 틱 안에서만 쓰는 작업 버퍼 (저장하지 않음)
    readonly List<Building> supplyCities = new();
    readonly List<Unit> supplyWagons = new();

    long SupplyTicks(float seconds) => (long)MathF.Round(seconds / Config.SimDt);
    int SupplyIntervalTicks => Math.Max(1, (int)SupplyTicks(Defs.Supply.Interval));

    /// <summary>사격 1회분 탄약 소모. 탄약이 없으면 false (사격 불가). 무한 탄약 · 교전 테스트(기본)는 항상 true.</summary>
    internal bool ConsumeAmmo(Unit u)
    {
        if (!AmmoEnabled || !u.UsesAmmo || DebugInfiniteAmmo) return true;
        if (u.Ammo <= 0) return false;
        u.Ammo--;
        if (KoreanDivision.Tests.AmmoObs.On) KoreanDivision.Tests.AmmoObs.Shot(u, Tick);   // 읽기 전용 측정
        return true;
    }

    static long LastCombatTick(Unit u) => Math.Max(u.LastShotTick, Math.Max(u.LastDamagedTick, u.LastFiredAtTick));

    /// <summary>마지막 사격 · 피격 (빗나감 포함) 뒤 회복 대기 시간이 지났는가.</summary>
    public bool AmmoQuiet(Unit u) => Tick - LastCombatTick(u) >= SupplyTicks(Defs.Supply.RegenDelay);

    bool IsFriendlyCity(int faction, Building b) => b.Alive && b.Def.City && b.Owner > 0 && (b.Owner == faction || Allied(faction, b.Owner));

    /// <summary>자기 · 동맹 도시의 가장자리가 radius 칸 안에 있는가.</summary>
    public bool NearOwnedCity(int faction, Vector2 pos, float radius)
    {
        foreach (var b in Buildings)
            if (IsFriendlyCity(faction, b) && b.EdgeDistance(pos) <= radius) return true;
        return false;
    }

    /// <summary>[표시 · 테스트] 이 위치에서 이 세력 유닛이 받는 회복 배수 (1 = 기본, 도시 · 보급 마차 근처 ×mul).</summary>
    public float AmmoRegenMulAt(int faction, Vector2 pos)
    {
        var s = Defs.Supply;
        float mul = 1f;
        if (NearOwnedCity(faction, pos, s.CityRadius)) mul = MathF.Max(mul, s.CityMul);
        float r2 = s.WagonRadius * s.WagonRadius;
        foreach (var w in Units)
            if (w.Alive && w.Owner == faction && w.Def.IsSupplyWagon && w.Cargo > 0 && Vector2.DistanceSquared(w.Pos, pos) <= r2) { mul = MathF.Max(mul, s.WagonMul); break; }
        return mul;
    }

    void UpdateSupply(float dt)
    {
        long t0 = Profile ? Stopwatch.GetTimestamp() : 0;
        if (AmmoEnabled) UpdateAmmo();
        if (Profile) SupplyProf += Stopwatch.GetTimestamp() - t0;
    }

    void UpdateAmmo()
    {
        var s = Defs.Supply;
        int interval = SupplyIntervalTicks;
        float step = interval * Config.SimDt;
        supplyCities.Clear(); supplyWagons.Clear();
        foreach (var b in Buildings) if (b.Alive && b.Def.City && b.Owner > 0) supplyCities.Add(b);   // 도시는 몇 개뿐 — 유닛마다 훑어도 싸다
        foreach (var u in Units) if (u.Alive && u.Owner > 0 && u.Def.IsSupplyWagon) supplyWagons.Add(u);
        float wagonR2 = s.WagonRadius * s.WagonRadius;
        long delay = SupplyTicks(s.RegenDelay);
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            if (!u.Alive || u.Owner <= 0 || (Tick + u.Id) % interval != 0) continue;
            bool nearCity = false;
            foreach (var c in supplyCities)
                if ((c.Owner == u.Owner || Allied(u.Owner, c.Owner)) && c.EdgeDistance(u.Pos) <= s.CityRadius) { nearCity = true; break; }
            if (u.Def.IsSupplyWagon)
            {
                if (nearCity && u.Cargo < u.Def.Cargo) u.Cargo = MathF.Min(u.Def.Cargo, u.Cargo + s.WagonRefill * step);
                continue;
            }
            if (!u.UsesAmmo || u.Ammo >= u.AmmoMax) { u.AmmoFrac = 0; continue; }
            if (Tick - LastCombatTick(u) < delay) continue;
            float mul = nearCity ? MathF.Max(1f, s.CityMul) : 1f;
            Unit? wagon = null;
            if (s.WagonMul > mul)   // 마차가 도시보다 나을 때만 마차 적재량을 쓴다
                foreach (var w in supplyWagons)
                    if (w.Owner == u.Owner && w.Cargo > 0 && Vector2.DistanceSquared(w.Pos, u.Pos) <= wagonR2) { wagon = w; mul = s.WagonMul; break; }
            float frac = s.RegenPct * step * mul;   // 한도 대비 비율
            float add = u.AmmoMax * frac + u.AmmoFrac;
            int whole = (int)add;
            u.AmmoFrac = add - whole;
            u.Ammo = Math.Min(u.AmmoMax, u.Ammo + whole);
            if (u.Ammo >= u.AmmoMax) u.AmmoFrac = 0;
            if (wagon != null) wagon.Cargo = MathF.Max(0, wagon.Cargo - s.RegenPct * step * (mul - 1f));
        }
    }

    // ---------------------------------------------------------------- 엄폐 배수 (칸 지형)

    /// <summary>엄폐 배수 — 표적이 맞을 확률 (Combat2.HitChance 의 TerrainAccuracy 자리).
    /// 칸 지형 (숲 · 잔해 · 참호 · 포탄 구덩이 · 가옥 옆, GameMap.TerrainCover) 중 가장 좋은 (작은) 값 하나 — 겹치지 않는다. 건물 표적은 1.</summary>
    float CoverHitMul(Entity target) => target is Unit u ? Map.TerrainCover(u.Pos).Hit : 1f;
    /// <summary>엄폐 배수 — 표적이 받는 제압 (Combat3.Suppress). CoverHitMul 과 같은 규칙.</summary>
    internal float CoverSuppressMul(Unit target) => Map.TerrainCover(target.Pos).Suppress;
    /// <summary>엄폐 단계: 0 = 없음, 1 = 엄폐 지형 위 (자동 수류탄 판단 · UI).</summary>
    public int CoverLevelOf(Unit u) => Map.TerrainCover(u.Pos).Hit < 1f ? 1 : 0;

    // ---------------------------------------------------------------- 체크섬 · 저장

    void HashSupply(ref Hasher h)
    {
        foreach (var u in Units)
        {
            h.Add(u.Ammo); h.Add(u.AmmoFrac); h.Add(u.Cargo); h.Add(u.LastFiredAtTick);
        }
        h.Add(BattleAmmo ? 1 : 0);
        HashTrenches(ref h);   // 참호 자동 점유 (Trenches.cs)
    }

    partial void WriteSupplyExtImpl(SnapWriter w, SnapCtx ctx)
    {
        w.I(Units.Count);
        foreach (var u in Units)
        {
            w.I(u.Ammo); w.F(u.AmmoFrac); w.F(u.Cargo); w.L(u.LastFiredAtTick);
        }
        w.Bool(BattleAmmo);
        WriteTrenches(w);   // 참호 자동 점유 (Trenches.cs, FormatVersion 18)
    }

    partial void ReadSupplyExtImpl(SnapReader r, SnapCtx ctx)
    {
        if (r.Count() != Units.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "supply units"));
        foreach (var u in Units)
        {
            u.Ammo = r.I(); u.AmmoFrac = r.F(); u.Cargo = r.F(); u.LastFiredAtTick = r.L();
        }
        BattleAmmo = r.Bool();
        ReadTrenches(r);
    }
}
