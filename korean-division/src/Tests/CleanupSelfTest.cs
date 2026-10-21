using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 정리 묶음 2 (2026-10-19 사용자 승인): 장교 선제 교전 없음 · 견인포 회전 속도 · 머리띠 표시 규칙 · 교전 테스트 N 키 소리.
/// SelfTest 가 호출, 단독 KD_SELFTEST_ONLY=cleanup.
/// </summary>
public static class CleanupSelfTest
{
    const string Flat = """
    { "name": "cleanup test", "width": 80, "height": 80, "base": "grass", "starts": [ [6, 6], [74, 74] ] }
    """;

    static World Fresh(int seed, string map = Flat)
    {
        var w = World.CreateStandard(MapFile.LoadText(map, "cleanuptest", seed), Defs.Current);
        foreach (var u in w.Units) u.Hp = 0;
        w.Step();
        w.Fog.Enabled = false;
        return w;
    }

    static Vector2 C(int x, int y) => new(x + 0.5f, y + 0.5f);
    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }
    static float Deg(float a) => MathF.Abs(DetMath.WrapPi(a)) * 180f / MathF.PI;

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();
        var defs = Defs.Current;

        // ---- 장교: 먼저 교전하지 않는다 (사거리 밖 적에게 다가가지 않음, 사거리 안에 들어온 적만 사격)
        {
            var w = Fresh(opt.Seed);
            var off = w.SpawnUnit(defs.Units["officer"], 1, C(30, 40));
            var rifle = w.SpawnUnit(defs.Units["infantry"], 1, C(30, 30));
            var foe = w.SpawnUnit(defs.Units["infantry"], 2, C(42, 40)); foe.Weapon = null; foe.Hp = 100000;
            var foe2 = w.SpawnUnit(defs.Units["infantry"], 2, C(60, 30)); foe2.Weapon = null; foe2.Hp = 100000;
            off.Hp = 100000;
            Vector2 o0 = off.Pos, r0 = rifle.Pos;
            Steps(w, 60 * 5);
            float range = off.Weapon!.Range;
            check(Vector2.Distance(off.Pos, o0) < 0.3f && off.LastShotTick < 0, $"장교: 사거리 {range} 밖 (12칸) 적에게 다가가지도 쏘지도 않음 (이동 {Vector2.Distance(off.Pos, o0):0.00})");
            check(Vector2.Distance(rifle.Pos, r0) > 2f || rifle.LastShotTick > 0, "(대조) 소총수는 시야 안 적과 교전 (다가가거나 사격)");
            foe.Pos = off.Pos + new Vector2(range - 1.5f, 0);
            Steps(w, 60 * 4);
            check(off.LastShotTick > 0 && Vector2.Distance(off.Pos, o0) < 0.5f, $"장교: 사거리 안에 들어온 적은 제자리에서 사격 (마지막 사격 틱 {off.LastShotTick})");
            // 공격 이동 중에도 사거리 밖 적 때문에 멈추거나 끼어들지 않는다
            var w2 = Fresh(opt.Seed);
            var off2 = w2.SpawnUnit(defs.Units["officer"], 1, C(20, 40)); off2.Hp = 100000;
            var side = w2.SpawnUnit(defs.Units["infantry"], 2, C(30, 52)); side.Weapon = null; side.Hp = 100000;   // 경로 옆 12칸
            w2.AttackMove(1, new[] { off2 }, C(40, 40));
            Steps(w2, 60 * 9);
            check(off2.Pos.X > 38f && MathF.Abs(off2.Pos.Y - 40.5f) < 1.5f && off2.LastShotTick < 0, $"장교 공격 이동: 사거리 밖 적에게 끼어들지 않고 목표까지 ({off2.Pos.X:0.0},{off2.Pos.Y:0.0})");
        }

        // ---- 견인포: 느린 차체 회전 (balance.json turnRate)
        {
            var w = Fresh(opt.Seed);
            var how = w.SpawnUnit(defs.Units["heavy_howitzer"], 1, C(40, 40));
            var inf = w.SpawnUnit(defs.Units["infantry"], 1, C(40, 20));
            how.Facing = 0; inf.Facing = 0;   // 동쪽
            float rate = defs.Units["heavy_howitzer"].TurnRate;
            w.Move(1, new[] { how }, C(25, 40));
            w.Move(1, new[] { inf }, C(25, 20));
            Steps(w, 30);
            check(Deg(inf.Facing - MathF.PI) < 5f, $"보병: 회전 즉시 (0.5초 뒤 서쪽과 {Deg(inf.Facing - MathF.PI):0}°)");
            Steps(w, 30);
            float turned = Deg(how.Facing);
            float moved = Vector2.Distance(how.Pos, C(40, 40));
            check(rate > 0 && turned <= rate * 1.05f + 1f && turned >= rate * 0.5f, $"곡사포: 1초에 {turned:0}° 회전 (turnRate {rate}°/s)");
            check(moved < defs.Units["heavy_howitzer"].Speed * 0.6f, $"곡사포: 차체가 크게 어긋난 동안 감속 (1초 이동 {moved:0.00}칸, 속도 {defs.Units["heavy_howitzer"].Speed})");
            Steps(w, 60 * 16);
            check(Deg(how.Facing - MathF.PI) < 10f && Vector2.Distance(how.Pos, C(25, 40)) < 1.5f, $"곡사포: 결국 진행 방향으로 돌아 도착 (방향 오차 {Deg(how.Facing - MathF.PI):0}°)");
            check(defs.Units["heavy_infantry_gun"].TurnRate > defs.Units["heavy_field_gun"].TurnRate && defs.Units["heavy_field_gun"].TurnRate > rate,
                  $"회전 속도: 보병포 {defs.Units["heavy_infantry_gun"].TurnRate} > 야전포 {defs.Units["heavy_field_gun"].TurnRate} > 곡사포 {rate} (°/s)");
            // 제자리 조준 회전도 같은 속도 (뒤쪽 적 → 포를 돌린 뒤 방열)
            var w2 = Fresh(opt.Seed);
            var fg = w2.SpawnUnit(defs.Units["heavy_field_gun"], 1, C(40, 40)); fg.Facing = 0; fg.Hp = 100000;
            var tgt = w2.SpawnUnit(defs.Units["infantry"], 2, C(20, 40)); tgt.Weapon = null; tgt.Hp = 100000;
            Steps(w2, 60);
            float fr = defs.Units["heavy_field_gun"].TurnRate;
            check(Deg(fg.Facing) <= fr * 1.05f + 1f && Deg(fg.Facing) > 1f, $"야전포: 뒤쪽 적을 향해 1초에 {Deg(fg.Facing):0}° (turnRate {fr}°/s)");
        }

        // ---- 머리띠 (팀 층) 표시 규칙: 군복이 헷갈리는 교전만 (같은 그림 변형의 적대 세력)
        {
            static string Map3(string a, string b, string c, int ta = 0, int tb = 0, int tc = 0) => $$"""
            { "name": "band", "width": 48, "height": 48, "base": "grass", "starts": [ [5, 5], [42, 42], [5, 42] ],
              "factions": [ { "name": "A", "en": "{{a}}", "team": {{ta}} }, { "name": "B", "en": "{{b}}", "team": {{tb}} }, { "name": "C", "en": "{{c}}", "team": {{tc}} } ] }
            """;
            bool[] Band(string map)
            {
                var w = World.CreateStandard(MapFile.LoadText(map, "bandtest", opt.Seed), defs);
                return Enumerable.Range(1, w.Factions.Count - 1).Select(f => Render.UnitRenderer.HeadbandOn(w, f)).ToArray();
            }
            var b1 = Band(Map3("korean", "japanese", "fengtian"));
            check(b1.All(x => !x), "서로 다른 군복끼리 = 머리띠 없음");
            var b2 = Band(Map3("korean", "japanese", "korean"));
            check(b2[0] && !b2[1] && b2[2], "같은 군복 (한국 대 한국) 이 적대 = 그 둘만 머리띠");
            var b3 = Band(Map3("russian", "japanese", "soviet"));
            check(b3.All(x => !x), "백군 · 적군은 그림 변형이 달라 머리띠 없음");
            var b4 = Band(Map3("korean", "japanese", "korean", 1, 2, 1));
            check(b4.All(x => !x), "같은 군복이라도 동맹 (같은 팀) 이면 머리띠 없음");
        }

        // ---- 소리: N 키 (ToggleSound) 가 교전 테스트 무음도 푼다
        {
            bool m0 = UI.Sfx.Muted, s0 = UI.Sfx.Silent;
            try
            {
                UI.Sfx.Muted = false; UI.Sfx.Silent = true;
                Game.ToggleSound();
                bool on = !UI.Sfx.Muted && !UI.Sfx.Silent;
                Game.ToggleSound();
                check(on && UI.Sfx.Muted, "N 키: 교전 테스트 무음 (Silent) 이면 소리 켬, 다시 누르면 음소거");
            }
            finally { UI.Sfx.Muted = m0; UI.Sfx.Silent = s0; }
        }
    }
}
