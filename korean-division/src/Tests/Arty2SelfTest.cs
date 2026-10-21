using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N22 포병 기여 · 보병포 · 정찰병 (2026-10-19 사용자 결정): 정찰병 시야 48 · 피격 엄폐 (중대 정찰조 · 관측반) · 보병포 사격선 바로 뒤 방열 (유효 사거리 안) · 곡사포 수치.
/// 창 없이. KD_SELFTEST_ONLY=arty2 로 단독 실행.
/// </summary>
public static class Arty2SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk) => LegacyDeploy.Wrap(() => RunImpl(opt, chk));
    static void RunImpl(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[N22 포병 기여 · 보병포 · 정찰병]");
        try
        {
            TestValues();
            TestObserverCover();
            TestCompanyScoutCover();
            TestInfantryGunDeploy();
        }
        catch (Exception e) { check(false, $"N22 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 140)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void TestValues()
    {
        var sc = defs.Units["scout"]; var isc = defs.Units["infantry_scout"];
        int iscSight = (int)MathF.Round(isc.SightFar * defs.Vision.ScoutSightMul);
        check(sc.Sight == 48 && sc.SightFar == 48 && sc.Hp == 100, $"정찰병 (scout) 시야 48 · HP 100 (시야 {sc.Sight}/{sc.SightFar}, HP {sc.Hp})");
        check(iscSight == 48 && isc.Hp == 100, $"정찰병 (infantry_scout) 실효 최대 시야 48 = {isc.SightFar} × {defs.Vision.ScoutSightMul} · HP 100 ({iscSight}, HP {isc.Hp})");
        var r = defs.ScoutAi;
        check(r.CoverOnFire && r.CoverRadius > 0 && r.CoverCalmSec > 0, $"scoutAi 피격 엄폐 읽음 (반경 {r.CoverRadius}, 조용 {r.CoverCalmSec}초, 최소 {r.CoverMinSec}초)");
        var A = defs.Tactics.ArtilleryAi;
        check(A.RedeployCooldownSec == 60f && A.DangerScatterMul == 0.75f && A.GunStandoffRifleMul == 1.3f && A.RedeployRangeFrac == 0.75f,
            $"곡사포 · 직사포 수치 (N22 측정 선택): 재배치 쿨다운 {A.RedeployCooldownSec}초 · 근접 위험 퍼짐 ×{A.DangerScatterMul} · 재배치 사거리 ×{A.RedeployRangeFrac} · 직사포 진지 소총 ×{A.GunStandoffRifleMul}");
        check(A.CloseGunMaxRange >= defs.Weapon("gun_infantry")!.Range && A.CloseGunMaxRange < defs.Weapon("gun_field")!.Range,
            $"보병포만 동행 근접 지원 규칙 (사거리 {A.CloseGunMaxRange} 이하)");
    }

    /// <summary>숲 띠 (y0..y1, x0..x1) — 엄폐 · 사선 차단.</summary>
    static void Forest(World w, int x0, int x1, int y0, int y1)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) w.Map.Set(x, y, TerrainType.Forest);
    }

    static void TestObserverCover()
    {
        // 관측반 (정찰병만의 중대): 남쪽 적 소총병이 쏘면 곧바로 엄폐 (북쪽 숲 · 사선 밖), 적이 사라지고 조용해지면 엄폐를 끝낸다
        var w = Bare();
        Forest(w, 50, 70, 50, 55);
        var obs = new List<Unit> { w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(60.5f, 61.5f)), w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(62.5f, 61.5f)) };
        w.FormCompany(1, obs);
        for (int i = 0; i < 3; i++) w.Step();
        check(obs[0].Company != null && obs[0].ScoutCompany == null, "관측반 = 정찰병만의 중대");
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(61.5f, 80.5f));
        var o = obs[0];
        Vector2 start = o.Pos;
        long fired = -1, cover = -1;
        for (int i = 0; i < 20 * 60; i++)
        {
            w.Step();
            if (fired < 0 && obs.Any(x => x.LastFiredAtTick > 0)) fired = w.Tick;
            if (cover < 0 && obs.Any(x => x.ScoutState == ScoutState.Cover)) cover = w.Tick;
            if (cover >= 0 && w.Tick - cover > 6 * 60) break;
        }
        var u = obs.FirstOrDefault(x => x.Alive && x.ScoutState == ScoutState.Cover) ?? o;
        check(fired > 0 && cover >= fired && cover - fired <= 16, $"노려지면 곧바로 엄폐 상태 (피격 틱 {fired}, 엄폐 틱 {cover})");
        bool safe = w.Map.TerrainCover(u.Pos).Hit < 1f || !w.Map.HasFiringLine(foe.Pos, u.Pos, foe.Id, u.Id);
        check(safe && Vector2.Distance(u.Pos, foe.Pos) > Vector2.Distance(start, foe.Pos), $"엄폐 칸 = 숲 · 사격자 사선 밖, 사격자에게서 멀어짐 ({u.Pos.X:0.0},{u.Pos.Y:0.0})");
        foe.Hp = 0;
        long calm = -1;
        for (int i = 0; i < 20 * 60; i++)
        {
            w.Step();
            if (obs.Where(x => x.Alive).All(x => x.ScoutState != ScoutState.Cover)) { calm = w.Tick; break; }
        }
        float need = defs.ScoutAi.CoverCalmSec;
        check(calm > 0 && (calm - cover) * Config.SimDt >= need - 0.5f, $"조용해지면 ({need}초) 엄폐를 끝내고 다시 관측 대기 ({(calm - cover) * Config.SimDt:0.0}초 뒤)");
    }

    static void TestCompanyScoutCover()
    {
        // 중대 정찰조 (자율): 앞에서 관측하다 노려지면 엄폐 → 조용해지면 다시 관측 (복귀하지 않음 — 제압 복귀와 별개)
        var w = Bare();
        Forest(w, 80, 100, 62, 66);
        var mem = new List<Unit>();
        for (int i = 0; i < 8; i++) mem.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(84.5f + i * 1.2f, 52.5f)));
        mem.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(90.5f, 50.5f)));
        var sc = w.SpawnUnit(defs.Units["infantry_scout"], 1, new Vector2(90.5f, 51.5f));
        mem.Add(sc);
        w.FormCompany(1, mem);
        for (int i = 0; i < 3; i++) w.Step();
        check(sc.ScoutCompany != null && sc.ScoutAuto, "중대 정찰조 · 자율");
        sc.Pos = new Vector2(90.5f, 70.5f);
        sc.ScoutState = ScoutState.Observe;
        var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(90.5f, 88.5f));
        long fired = -1, cover = -1; int supp = 0;
        for (int i = 0; i < 15 * 60; i++)
        {
            w.Step();
            if (fired < 0 && sc.LastFiredAtTick > 0) { fired = w.Tick; supp = sc.Suppression; }
            if (cover < 0 && sc.ScoutState == ScoutState.Cover) cover = w.Tick;
            if (cover >= 0 && w.Tick - cover > 5 * 60) break;
        }
        check(fired > 0 && cover >= fired && cover - fired <= 16 && supp < defs.ScoutAi.ReturnSuppression,
            $"제압 복귀 문턱 아래 (제압 {supp} < {defs.ScoutAi.ReturnSuppression}) 에서도 노려지면 곧바로 엄폐 (피격 틱 {fired}, 엄폐 틱 {cover})");
        bool safe = w.Map.TerrainCover(sc.Pos).Hit < 1f || !w.Map.HasFiringLine(foe.Pos, sc.Pos, foe.Id, sc.Id);
        check(sc.Alive && safe && sc.Pos.Y < 70.5f, $"엄폐로 물러남 (숲 · 사선 밖, ({sc.Pos.X:0.0},{sc.Pos.Y:0.0}))");
        foe.Hp = 0;
        bool resumed = false;
        for (int i = 0; i < 20 * 60 && !resumed; i++) { w.Step(); resumed = sc.ScoutState is ScoutState.With or ScoutState.Advance or ScoutState.Observe; }
        check(resumed, $"조용해지면 다시 관측 (상태 {sc.ScoutState})");
    }

    static void TestInfantryGunDeploy()
    {
        // 보병포: 대대와 함께 가다 접촉하면 사격선 바로 뒤 · 유효 사거리 안에 방열하고 쏜다 (예전: 유효 사거리 밖 먼 발 69%)
        var setup = new BattleSetup { SideA = "infantry:60,officer:2,heavy_infantry_gun:3", SideB = "infantry:60,officer:2", Map = "flat", Distance = 90, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 5);
        w.Fog.Enabled = true;
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        var guns = w.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_infantry_gun").ToList();
        float eff = guns[0].Weapon!.Range * defs.Tactics.ArtilleryAi.GunEffectiveFrac;
        Vector2 dir = Vector2.Normalize(w.Units.Where(u => u.Owner == 2).Aggregate(Vector2.Zero, (a, u) => a + u.Pos) / w.Units.Count(u => u.Owner == 2)
                                        - guns.Aggregate(Vector2.Zero, (a, u) => a + u.Pos) / guns.Count);
        bool checkedDeploy = false, inEff = false, behind = false; float dE = 0, ahead = 0;
        int shells = 0, near = 0; float dsum = 0;
        for (int i = 0; i < 150 * 60 && w.Winner == 0; i++)
        {
            w.Step();
            foreach (var im in w.Impacts)
            {
                if (im.Tick != w.Tick - 1 || w.Find(im.ShooterId) is not Unit sh || sh.Def.Key != "heavy_infantry_gun") continue;
                shells++; float di = Vector2.Distance(im.From, im.Impact); dsum += di; if (di <= eff + 1f) near++;
            }
            if (checkedDeploy || i % 30 != 0) continue;
            var dep = guns.Where(g => g.Alive && g.Deployed && g.LastShotTick > 0).ToList();
            if (dep.Count == 0) continue;
            checkedDeploy = true;
            float front = w.Units.Where(u => u.Owner == 1 && u.Alive && u.Def.IsInfantry && !u.Def.Heavy && !u.IsOfficer && !u.Def.Scout).Max(u => Vector2.Dot(u.Pos, dir));
            var vis = w.Units.Where(e => e.Owner == 2 && e.Alive && w.Fog.CanSee(1, e)).ToList();
            dE = dep.Average(g => vis.Count > 0 ? vis.Min(e => Vector2.Distance(e.Pos, g.Pos)) : 999f);
            ahead = dep.Max(g => Vector2.Dot(g.Pos, dir) - front);
            inEff = dE <= eff + 2f;
            behind = ahead <= 1f;
        }
        check(checkedDeploy && inEff, $"보병포 방열 · 첫 사격 때 가장 가까운 적이 유효 사거리 안 (평균 {dE:0.0}칸, 유효 {eff:0})");
        check(checkedDeploy && behind, $"보병포는 아군 사격선 뒤 (앞선 대비 {ahead:0.0}칸)");
        // 착탄 거리 (발사점 → 착탄점) 는 표적 거리 + 퍼짐 (2칸) 이라 유효 사거리 경계 근처 발이 많다 — 평균이 유효 사거리 + 3 안이면 통과 (예전 진지 31칸 · 먼 발 69%)
        check(shells >= 10 && dsum / shells <= eff + 3f, $"보병포 평균 착탄 거리가 유효 사거리 근처 ({dsum / Math.Max(1, shells):0.0}칸, 유효 {eff:0}, 유효 안 {near}/{shells})");
    }
}
