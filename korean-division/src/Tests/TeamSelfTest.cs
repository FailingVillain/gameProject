using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>정적 팀 (외교 대체, WorldTeams.cs): 기본 = 모두 적, 지도 factions[].team 이 같으면 동맹 (교전 없음 · 시야 공유 · 탄약 회복 · 공동 승리). SelfTest 가 호출.</summary>
public static class TeamSelfTest
{
    const string TeamMap = """
    { "name": "team test", "width": 96, "height": 96, "base": "grass",
      "starts": [ [16, 16], [80, 16], [16, 80], [80, 80] ],
      "factions": [ { "name": "A", "team": 1 }, { "name": "B", "team": 2 }, { "name": "C", "team": 1 }, { "name": "D", "team": 2 } ] }
    """;

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var inf = defs.Units["infantry"];

        // ---- 팀 정보 없음: 모두가 서로 적
        var w0 = World.CreateStandard(GameMap.Generate(120, 120, opt.Seed, 4), defs);
        bool allHostile = true;
        for (int a = 1; a <= 4; a++)
        for (int b = 1; b <= 4; b++)
            if (a != b && (!w0.IsHostile(a, b) || w0.Allied(a, b))) allHostile = false;
        check(allHostile && w0.Allied(1, 1) && !w0.IsHostile(1, 0) && !w0.Allied(0, 0), "팀 정보가 없으면 모든 세력이 서로 적 (중립 0 은 적도 동맹도 아님)");

        // ---- 지도 파일 팀: 1·3 / 2·4
        World TeamWorld() => World.CreateStandard(MapFile.LoadText(TeamMap, "teamtest", opt.Seed), defs);
        var w = TeamWorld();
        check(w.Faction(1).Team == 1 && w.Faction(4).Team == 2 && w.Allied(1, 3) && w.Allied(2, 4) && w.IsHostile(1, 2) && w.IsHostile(3, 4) && !w.IsHostile(1, 3),
              "지도 factions[].team: 같은 번호끼리 동맹, 다른 번호는 적");

        // ---- 교전: 같은 팀은 서로 쏘지 않고, 강제 공격도 안 됨. 적은 쏜다
        foreach (var u in w.Units) u.Hp = 0;
        w.Step();
        var c = new Vector2(48, 48);
        var a1 = Enumerable.Range(0, 6).Select(i => w.SpawnUnit(inf, 1, c + new Vector2(-2, i * 0.9f))).ToList();
        var a3 = Enumerable.Range(0, 6).Select(i => w.SpawnUnit(inf, 3, c + new Vector2(2, i * 0.9f))).ToList();
        w.Attack(1, a1, a3[0]);
        for (int i = 0; i < 300; i++) w.Step();
        check(a1.Concat(a3).All(u => u.Alive && u.Hp == u.Def.Hp), "같은 팀 유닛은 붙어 있어도 교전하지 않음 (강제 공격 명령도 거부)");
        var e2 = Enumerable.Range(0, 6).Select(i => w.SpawnUnit(inf, 2, c + new Vector2(10, i * 0.9f))).ToList();
        for (int i = 0; i < 600; i++) w.Step();
        check(e2.Any(u => !u.Alive || u.Hp < u.Def.Hp), "다른 팀 유닛과는 교전");

        // ---- 시야 공유 · 동맹 도시 근처 = 탄약 회복 지역
        var wv = TeamWorld();
        var probe = wv.SpawnUnit(inf, 3, new Vector2(48, 30));
        var enemy = wv.SpawnUnit(inf, 2, new Vector2(49, 30));
        probe.Weapon = null; enemy.Weapon = null;
        for (int i = 0; i < 24; i++) wv.Step();
        check(wv.Fog.CanSee(3, enemy) && wv.Fog.CanSee(1, enemy), "같은 팀은 시야 공유 (3 이 본 적을 1 도 봄)");
        var hq3 = wv.Buildings.First(b => b.Owner == 3 && b.Def.Headquarters);
        check(wv.NearOwnedCity(1, hq3.Pos, 1) && !wv.NearOwnedCity(2, hq3.Pos, 1), "같은 팀 도시 근처 = 아군 도시 (탄약 회복), 다른 팀은 아님");

        // ---- 공동 승리: 2 · 4 전멸 → 1 · 3 공동 승리
        var wg = TeamWorld();
        foreach (var u in wg.Units.Where(u => u.Owner is 2 or 4)) u.Hp = 0;
        for (int i = 0; i < 60 && wg.Winner == 0; i++) wg.Step();
        check(wg.Winner != 0 && wg.Winners.Contains(1) && wg.Winners.Contains(3) && !wg.Winners.Contains(2), $"남은 세력이 모두 같은 팀이면 공동 승리 (승자 {string.Join(",", wg.Winners)})");

        // ---- 저장 · 불러오기에서 팀 유지 (실행 중 불변이지만 스냅샷 · 체크섬에 포함)
        var ws = TeamWorld();
        var loaded = Snapshot.Load(Snapshot.Save(ws, new SnapshotMeta("team", 1, false, "none")), defs).World;
        check(Enumerable.Range(1, 4).All(f => loaded.Faction(f).Team == ws.Faction(f).Team) && loaded.ComputeChecksum() == ws.ComputeChecksum(), "스냅샷이 팀을 보존 (체크섬 일치)");
        var wn = World.CreateStandard(MapFile.LoadText(TeamMap.Replace("\"team\": 1", "\"team\": 0").Replace("\"team\": 2", "\"team\": 0"), "teamtest", opt.Seed), defs);
        check(wn.ComputeChecksum() != ws.ComputeChecksum(), "팀 배정은 체크섬에 반영");
    }
}
