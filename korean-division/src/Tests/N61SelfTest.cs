using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>N61: 기병 하마 사격 — 사격하려 서면 자동 하마 (사격은 하마 뒤), 승마 사격 명중 감소 (유닛별 배수 가능), 내린 이동 속도, 이동 · 돌격이면 승마, MountCommand (잠금 · JSON), 저장.</summary>
public static class N61SelfTest
{
    static World NewWorld(int seed) { var w = TestDefs.Strip(World.CreateStandard(MapFile.Load("flat4", seed), Defs.Current)); w.Fog.Enabled = false; return w; }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();
        try
        {
            var defs = Defs.Current; var cr = defs.CombatRules;
            check(cr.DismountSec >= 3 && cr.DismountSec <= 5 && cr.MountSec >= 3 && cr.MountedFireAccMul > 0 && cr.MountedFireAccMul < 1 && cr.DismountedSpeedMul < 1, $"N61 balance.json 하마 설정 (하마 {cr.DismountSec}s, 승마 {cr.MountSec}s, 승마 사격 ×{cr.MountedFireAccMul})");
            var w = NewWorld(opt.Seed + 61);
            var c = new Vector2(64.5f, 56.5f);
            var cav = w.SpawnUnit(defs.Units["cavalry_default"], 1, c);
            var foe = w.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(14, 0)); foe.Weapon = null; foe.Hp = 5000; w.Hold(2, new[] { foe });
            float mnt = w.HitChance(cav, foe), cavRange = cav.Weapon!.Range;
            cav.Dismounted = true; float dis = w.HitChance(cav, foe); cav.Dismounted = false;
            check(dis > 0 && MathF.Abs(mnt / dis - cr.MountedFireAccMul) < 0.01f, $"N61 승마 사격 명중 ×{mnt / dis:0.00} (하마 {dis:0.00} → 승마 {mnt:0.00})");
            defs.Units["cavalry_default"].MountedFireAccMul = 0.7f;
            check(MathF.Abs(w.HitChance(cav, foe) / dis - 0.7f) < 0.01f, "N61 유닛별 승마 사격 배수 (몽골 자리) 0.7 적용");
            defs.Units["cavalry_default"].MountedFireAccMul = 0;
            float v0 = w.UnitSpeedForTest(cav); cav.Dismounted = true; float v1 = w.UnitSpeedForTest(cav); cav.Dismounted = false;
            check(MathF.Abs(v1 / v0 - cr.DismountedSpeedMul) < 0.01f, $"N61 내린 기병 속도 ×{v1 / v0:0.00}");

            // 자동 하마: 사거리 안 표적 + 대기 → 하마 시간 동안 사격 없음 → 내린 뒤 사격
            long shots0 = 0; int tFire = -1, tDis = -1;
            for (int i = 0; i < 20 * 60 && tFire < 0; i++)
            {
                w.Step();
                if (tDis < 0 && cav.Dismounted) tDis = i;
                if (cav.Cooldown > 0 && tFire < 0) tFire = i;
            }
            check(tDis >= (int)(cr.DismountSec / Config.SimDt) - 3 && tFire >= tDis - 1 && cav.Dismounted, $"N61 사격 전에 자동 하마 (하마 완료 틱 {tDis}, 첫 사격 {tFire})");
            // 이동 명령 → 승마
            w.Move(1, new[] { cav }, c + new Vector2(-20, 0));
            int tMount = -1;
            for (int i = 0; i < 12 * 60 && tMount < 0; i++) { w.Step(); if (!cav.Dismounted && cav.MountLeft == 0) tMount = i; }
            check(tMount >= (int)(cr.MountSec / Config.SimDt) - 3, $"N61 이동 명령 = 다시 탐 ({tMount} 틱)");
            // 승마 잠금 명령: 자동 하마 안 함, 승마 사격 (명령 · JSON)
            var w2 = NewWorld(opt.Seed + 62);
            var cv2 = w2.SpawnUnit(defs.Units["cavalry_default"], 1, c);
            var f2 = w2.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(14, 0)); f2.Weapon = null; f2.Hp = 5000; w2.Hold(2, new[] { f2 });
            w2.SetMount(1, new[] { cv2 }, true);
            bool fired = false;
            for (int i = 0; i < 12 * 60; i++) { w2.Step(); if (cv2.Cooldown > 0) fired = true; }
            check(!cv2.Dismounted && cv2.MountLock && w2.Stats.Dismounts == 0 && fired, $"N61 승마 잠금: 하마 안 함, 말 위에서 사격 (사격 {fired})");
            w2.SetMount(1, new[] { cv2 }, false);
            for (int i = 0; i < 6 * 60; i++) w2.Step();
            check(cv2.Dismounted && !cv2.MountLock, "N61 하마 명령: 내리고 잠금 해제");
            var rt = JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize<GameCommand>(new MountCommand(1, new[] { 5, 6 }, true), CommandJson.Options), CommandJson.Options);
            check(rt is MountCommand { Faction: 1, Mount: true } m && m.Units.SequenceEqual(new[] { 5, 6 }), "N61 MountCommand JSON 왕복");
            // 내린 채 돌격 → 승마 먼저
            var w3 = NewWorld(opt.Seed + 63);
            var cv3 = w3.SpawnUnit(defs.Units["cavalry_default"], 1, c); cv3.Dismounted = true;
            var f3 = w3.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(12, 0)); f3.Weapon = null; f3.Hp = 5000; w3.Hold(2, new[] { f3 });
            w3.Charge(1, new[] { cv3 }, f3.Pos, f3);
            for (int i = 0; i < 2 * 60; i++) w3.Step();
            check(cv3.MountLeft > 0 && cv3.Dismounted && Vector2.Distance(cv3.Pos, c) < 0.5f, "N61 내린 채 돌격 명령 = 제자리에서 승마 먼저");
            // 저장
            cv2.Dismounted = true; cv2.MountLeft = 77; cv2.MountLock = true;
            var meta = new SnapshotMeta("n61", 1, false, "standard");
            var bytes = Snapshot.Save(w2, meta);
            var y = Snapshot.Load(bytes, defs).World;
            var yc = y.Units.First(u => u.Id == cv2.Id);
            check(yc.Dismounted && yc.MountLeft == 77 && yc.MountLock && y.ComputeChecksum() == w2.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(y, meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes)), "N61 하마 상태 저장 · 불러오기 · 체크섬 동일");
        }
        catch (Exception e) { check(false, $"N61 테스트 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
        // 보병 대기병 조기 사격 (cavReact): 돌격 기병에게 사거리 끝에서 짧은 조준으로 사격
        var cfg = Defs.Current.CombatRules; bool oldOn = cfg.CavReactOn;
        try
        {
            var defs = Defs.Current;
            (float dist, float aim, int sw) Run1(bool on)
            {
                cfg.CavReactOn = on;
                var w = NewWorld(opt.Seed + 64);
                var c = new Vector2(64.5f, 56.5f);
                var inf = w.SpawnUnit(defs.Units["infantry"], 2, c); w.Hold(2, new[] { inf });
                var cv = w.SpawnUnit(defs.Units["cavalry_default"], 1, c + new Vector2(45, 0)); cv.Hp = 100000;
                w.Charge(1, new[] { cv }, inf.Pos, inf);
                float d = -1; int t0 = -1;
                for (int i = 0; i < 30 * 60 && d < 0; i++)
                {
                    w.Step();
                    if (t0 < 0 && cv.Charging) t0 = i;
                    if (inf.LastShotTick > 0) d = Vector2.Distance(inf.Pos, cv.Pos);
                }
                return (d, d, (int)w.Stats.CavReactSwitches);
            }
            var off = Run1(false); var on = Run1(true);
            float range = defs.Units["infantry"].Weapon != null ? defs.Weapons[defs.Units["infantry"].Weapon!].Range : 24;
            check(on.dist > 0 && off.dist > 0 && on.dist > off.dist + 2f && on.dist <= range + 1f && on.sw > 0 && off.sw == 0,
                $"cavReact: 돌격 기병에게 첫 사격 거리 {on.dist:0.0} (끔 {off.dist:0.0}, 사거리 {range}), 표적 전환 {on.sw}");
            check(cfg.CavReactAimSec > 0 && cfg.CavReactAimSec < 1.2f && cfg.CavFastSpeed > defs.Units["cavalry_default"].Speed && cfg.CavReactInterval >= 1, "cavReact balance.json 설정");
            // 보병끼리는 영향 없음
            cfg.CavReactOn = true;
            var w2 = NewWorld(opt.Seed + 65);
            var q = new Vector2(64.5f, 56.5f);
            var a = w2.SpawnUnit(defs.Units["infantry"], 1, q); var b = w2.SpawnUnit(defs.Units["infantry"], 2, q + new Vector2(15, 0));
            w2.Hold(1, new[] { a }); w2.Hold(2, new[] { b });
            for (int i = 0; i < 10 * 60; i++) w2.Step();
            check(w2.Stats.CavReactSwitches == 0 && a.LastShotTick > 0, "cavReact: 보병 대 보병은 표적 전환 없음, 사격은 그대로");
        }
        catch (Exception e) { check(false, $"cavReact 테스트 예외: {e.GetType().Name}: {e.Message}"); }
        finally { cfg.CavReactOn = oldOn; }
    }
}
