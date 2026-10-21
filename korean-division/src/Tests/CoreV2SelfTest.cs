using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision;

/// <summary>핵심 규칙 검증 (방향 변경 §0-1): 주어진 병력 · 지도 유닛 배치 · 점령 도시 · 패배 규칙 · 대전차 보너스.</summary>
public static partial class SelfTest
{
    /// <summary>점령 시설을 즉시 세력 소유로 (테스트 설정).</summary>
    static void GiveSite(Building b, int owner)
    {
        b.Owner = owner; b.CaptureFaction = owner; b.CaptureProgress = 1;
    }

    /// <summary>점령 시설을 즉시 중립으로 (테스트 설정 — 게이지까지 비워야 다음 틱에 되돌아오지 않는다).</summary>
    static void Neutralize(Building b)
    {
        b.Owner = 0; b.CaptureFaction = 0; b.CaptureProgress = 0;
    }

    /// <summary>세력 f 의 시작 병력을 비우고 점령 못 하는 보급 마차 1기만 남긴다 (전멸 패배 방지 · 대도시 재점령 방지).</summary>
    static void Thin(World w, int f)
    {
        var keep = w.Units.Where(u => u.Owner == f && !u.Def.CanCapture).OrderBy(u => u.Id).First();
        foreach (var u in w.Units.Where(u => u.Owner == f && u != keep)) u.Hp = 0;
        w.Step();
    }

    static void TestCoreV2(GameOptions opt)
    {
        Console.WriteLine("[규칙 v3 (방향 변경 §0-1): 주어진 병력 · 도시 · 패배 규칙 · 대전차 보너스]");

        // ---- 시작: 대도시 소유 + 기본 병력 (start.force, 중대 편성)
        var w0 = NewWorld(opt);
        int force = defs.Start.Force.Sum(g => g.Units.Values.Sum());
        int companies = defs.Start.Force.Count(g => g.Company);
        bool startOk = true;
        for (int f = 1; f <= 2; f++)
        {
            startOk &= Hq(w0, f).Def.Key == "metropolis" && Hq(w0, f).Owner == f;
            startOk &= w0.Units.Count(u => u.Owner == f) == force;
            startOk &= w0.Companies.Count(c => c.Owner == f) == companies;
        }
        Check(startOk, $"시작: 세력마다 대도시 소유 · 기본 병력 {force}기 · 중대 {companies}개");
        Check(w0.Units.Where(u => u.Owner == 1).All(u => Vector2.Distance(u.Pos, Hq(w0, 1).Pos) < 30), "기본 병력은 대도시 앞에 선다");

        // ---- 지도 유닛 배치가 있으면 기본 병력 대신 그대로
        var map = GameMap.Generate(opt.MapWidth, opt.MapHeight, opt.Seed);
        var (sx, sy) = map.StartTiles[0];
        var (ex, ey) = map.StartTiles[1];
        var p1 = map.NearestWalkable(sx + 4, sy);
        var p2 = map.NearestWalkable(ex - 4, ey);
        map.UnitSpawns.Add(new UnitSpawn("infantry", 1, new Vector2(p1.X + 0.5f, p1.Y + 0.5f)));
        map.UnitSpawns.Add(new UnitSpawn("tank_ft_male", 1, new Vector2(p1.X + 0.5f, p1.Y + 2.5f)));
        map.UnitSpawns.Add(new UnitSpawn("infantry", 2, new Vector2(p2.X + 0.5f, p2.Y + 0.5f)));
        var wm = World.CreateStandard(map, defs);
        Check(wm.Units.Count(u => u.Owner == 1) == 2 && wm.Units.Count(u => u.Owner == 2) == 1 && wm.Units.Any(u => u.Owner == 1 && u.Def.Key == "tank_ft_male"),
            "지도 유닛 배치: 지정한 병력만 스폰 (기본 병력 없음)");

        // ---- 대전차소총: 기갑 3.5배, 보병은 그대로
        Check(CombatDrops("heavy_at_rifleman", "tank_ft_male").SetEquals(new[] { 34f }), $"대전차보병 vs FT: 피해 (10×3.5 - 장갑 1) = 34 (측정 {string.Join(",", CombatDrops("heavy_at_rifleman", "tank_ft_male"))})");
        Check(CombatDrops("heavy_at_rifleman", "infantry").SetEquals(new[] { 10f }), "대전차소총병 vs 보병: 피해 (10 - 장갑 0) = 10 (보너스 없음, 보병 장갑 0)");
        Check(CombatDrops("infantry", "tank_ft_male").Count == 0, "정규군 소총 vs 전차: 면역 — 피해 0 (자동 표적에서도 제외; 상세는 Combat3 장갑 규칙 테스트)");

        // ---- 대도시 점령은 captureTime
        var w4 = NewWorld(opt);
        var metro = Hq(w4, 1);
        foreach (var u in w4.Units.Where(u => u.Owner == 1 && Vector2.Distance(u.Pos, metro.Pos) < 40)) u.Hp = 0;
        var keeper = w4.SpawnUnit(defs.Units["infantry"], 1, w4.Units.First(u => u.Owner == 2).Pos + new Vector2(0, 30));   // 전멸 패배 방지
        keeper.Weapon = null;
        GiveSite(w4.Buildings.Where(b => b.Def.City && !b.Def.Headquarters).OrderBy(b => b.Id).First(), 1);   // 대도시를 잃어도 패배하지 않도록
        var cu = w4.SpawnUnit(defs.Units["infantry"], 2, metro.Pos + new Vector2(0, metro.Size / 2f + 1.5f));
        cu.Weapon = null;
        w4.Hold(2, new[] { cu });
        w4.Hold(1, new[] { keeper });
        float ct = metro.Def.CaptureTime;
        Run(w4, ct - 1.5f);
        Check(metro.Owner == 1 && metro.CaptureProgress > 0, $"대도시: {ct}초가 지나기 전에는 소유 유지 (게이지 {metro.CaptureProgress:0.00})");
        Run(w4, 3);
        Check(metro.Owner == 0 && !w4.Faction(1).Defeated, $"대도시 점령 시간 {ct}초 (유닛 1기)");

        // ---- 패배 규칙
        var w6 = NewWorld(opt);
        var village = w6.Buildings.Where(b => b.Def.Key == "village").OrderBy(b => b.Id).First();
        Neutralize(Hq(w6, 2));
        GiveSite(village, 2);
        Run(w6, 1);
        Check(w6.Faction(2).Defeated && w6.Winner == 1, "대도시를 잃고 마을(도시 아님)만 남으면 패배");
        var w7 = NewWorld(opt);
        var city7 = w7.Buildings.Where(b => b.Def.Key == "city").OrderBy(b => b.Id).First();
        Thin(w7, 2);
        Neutralize(Hq(w7, 2));
        GiveSite(city7, 2);
        Run(w7, 1);
        Check(!w7.Faction(2).Defeated && w7.Winner == 0, "대도시를 잃어도 점령한 소도시가 있으면 생존");
        Run(w7, defs.Start.RevealDelay + 1);
        Check(w7.Faction(2).Revealed, "대도시 없이 시간이 지나면 위치 공개 (소도시만으로는 해제되지 않음)");
        Neutralize(city7);
        Run(w7, 1);
        Check(w7.Faction(2).Defeated && w7.Winner == 1, "소도시도 잃으면 패배");
        Check(defs.Buildings.Values.All(b => b.Capturable), "건물 정의는 점령 시설뿐 (건설 · 생산 건물 없음)");
    }

    /// <summary>shooter 가 target 병과를 쏠 때 맞을 때마다 깎이는 체력 값들 (대상은 체력을 계속 되돌려 죽지 않게).</summary>
    static HashSet<float> CombatDrops(string shooterKey, string targetKey)
    {
        var w = NewWorld(new GameOptions { MapWidth = 100, MapHeight = 100, Players = 2, Seed = 20261009 });
        var c = new Vector2(50, 50);
        var (cx, cy) = w.Map.NearestWalkable(50, 50);
        c = new Vector2(cx + 0.5f, cy + 0.5f);
        var shooter = w.SpawnUnit(defs.Units[shooterKey], 1, c);
        var target = w.SpawnUnit(defs.Units[targetKey], 2, c + new Vector2(3, 0));
        target.Weapon = null;
        w.Hold(2, new[] { target });
        var drops = new HashSet<float>();
        for (int i = 0; i < 60 * 40; i++)
        {
            target.Hp = 1000;
            w.Step();
            if (target.Hp < 1000) drops.Add(1000 - target.Hp);
        }
        return drops;
    }
}
