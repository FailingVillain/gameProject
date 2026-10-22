using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>N63 · N68 · N72 · N78 새 유닛: 정의 로드 · 세력 제한 · 몽골 승마 사격 · 카자크 돌격 · 간도 의용병 · 기병 정찰병 (시야 · 하마 관측 · 자위 사격) · 봉천 위대 · 기수 (붕괴 · 쓰러짐 · 저장) · 타찬카.
/// 창 없이. KD_SELFTEST_ONLY=units 로 단독 실행.</summary>
public static class UnitsSelfTest
{
    static World NewWorld(int seed) { var w = TestDefs.Strip(World.CreateStandard(MapFile.Load("flat4", seed), Defs.Current)); w.Fog.Enabled = false; return w; }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();
        try
        {
            var defs = Defs.Current; var cr = defs.CombatRules;
            string[] keys = { "cavalry_mongol", "cavalry_cossack", "common_gando_volunteer", "cavalry_scout", "infantry_guard_fengtian", "standard_bearer", "heavy_tachanka" };
            bool loaded = keys.All(k => defs.Units.TryGetValue(k, out var d) && d.Name.Length > 0 && d.Description.Length > 0
                && (d.Weapon == null || defs.Weapon(d.Weapon) != null) && (d.ArtAs == null || defs.Units.ContainsKey(d.ArtAs)));
            check(loaded, "units: 새 유닛 7종 정의 로드 (이름 · 설명 · 무기 · 그림 대용)");
            var U = defs.Units;
            check(new[] { "korean", "japanese", "fengtian", "russian", "bandit" }.All(e => U["cavalry_mongol"].AllowedFor(e) && U["common_gando_volunteer"].AllowedFor(e) && U["standard_bearer"].AllowedFor(e)),
                "units: 몽골 · 간도 의용병 · 기수 = 모든 세력 편성 가능");
            check(U["cavalry_cossack"].AllowedFor("korean") && U["cavalry_cossack"].AllowedFor("russian") && !U["cavalry_cossack"].AllowedFor("japanese") && !U["cavalry_cossack"].AllowedFor("fengtian") && !U["cavalry_cossack"].AllowedFor("bandit"), "units: 카자크 기병 = 러시아 계열 + 한국군 전용");
            check(U["infantry_guard_fengtian"].AllowedFor("fengtian") && !U["infantry_guard_fengtian"].AllowedFor("korean") && !U["infantry_guard_fengtian"].AllowedFor("japanese"), "units: 봉천 위대 = 봉천 전용");
            check(U["heavy_tachanka"].AllowedFor("korean") && U["heavy_tachanka"].AllowedFor("russian") && !U["heavy_tachanka"].AllowedFor("japanese"), "units: 타찬카 = 한국군 + 러시아 계열");

            var w = NewWorld(opt.Seed + 63);
            var c = new Vector2(64.5f, 56.5f);
            // 몽골 / 카자크
            var mon = w.SpawnUnit(U["cavalry_mongol"], 1, c); var cos = w.SpawnUnit(U["cavalry_cossack"], 1, c + new Vector2(0, 4)); var cav = w.SpawnUnit(U["cavalry_default"], 1, c + new Vector2(0, -4));
            var foe = w.SpawnUnit(U["infantry"], 2, c + new Vector2(14, 0)); foe.Weapon = null; foe.Hp = 5000; w.Hold(2, new[] { foe });
            float mM = w.HitChance(mon, foe), mC = w.HitChance(cav, foe); mon.Dismounted = true; float dM = w.HitChance(mon, foe); mon.Dismounted = false;
            check(dM > 0 && MathF.Abs(mM / dM - 0.7f) < 0.01f && mM > mC * 1.5f, $"units: 몽골 승마 사격 x{mM / dM:0.00} (기병 x{mC / dM:0.00})");
            check(U["cavalry_cossack"].ChargeSpeedMul > U["cavalry_default"].ChargeSpeedMul && cos.Def.Mounted, $"units: 카자크 돌격 배수 {U["cavalry_cossack"].ChargeSpeedMul} > 기병 {U["cavalry_default"].ChargeSpeedMul}");
            // 간도 한인 의용병
            var g = U["common_gando_volunteer"];
            check(g.SuppressResist > U["recruit"].SuppressResist && g.Weapon == "rifle_berdan2" && g.Accuracy <= 1f && g.Hp == 100, "units: 간도 의용병 = 지방대 무기 · 제압 취약 (징집병보다도)");
            // 기병 정찰병: 시야 · 하마 관측 · 자위 사격 · 속도
            var sc = w.SpawnUnit(U["cavalry_scout"], 1, c + new Vector2(-10, 10));
            int s0 = w.SightOf(sc); sc.Dismounted = true; int s1 = w.SightOf(sc); sc.Dismounted = false;
            check(s0 > w.SightOf(cav) && s1 > s0, $"units: 기병 정찰병 시야 {s0} (기병 {w.SightOf(cav)}), 하마 관측 {s1}");
            check(w.UnitSpeedForTest(sc) > w.UnitSpeedForTest(cav) && sc.Def.Scout && w.PassiveFire(sc), "units: 기병 정찰병 더 빠름 · 자위 사격 규칙 (N35)");
            sc.ScoutState = ScoutState.Observe; sc.ScoutAuto = true;
            int tDis = -1;
            for (int i = 0; i < 12 * 60 && tDis < 0; i++) { w.Step(); if (sc.Dismounted) tDis = i; sc.ScoutState = ScoutState.Observe; }
            check(tDis >= 0, $"units: 서서 관측 중인 기병 정찰병은 하마 ({tDis} 틱)");
            w.Move(1, new[] { sc }, sc.Pos + new Vector2(-15, 0));
            int tMt = -1;
            for (int i = 0; i < 14 * 60 && tMt < 0; i++) { w.Step(); if (!sc.Dismounted && sc.MountLeft == 0) tMt = i; }
            check(tMt >= 0, $"units: 이동 명령이면 다시 탐 ({tMt} 틱)");
            // 봉천 위대
            var gf = U["infantry_guard_fengtian"]; var gd = U["infantry_guard"];
            check(gf.Hp == gd.Hp && gf.Weapon == "rifle_mauser98" && gf.Melee == "bayonet" && gf.SuppressResist < U["infantry_fengtian"].SuppressResist, "units: 봉천 위대 = 시위대 체력 · 마우저 98 · 착검 · 제압 저항 우수");
            // 기수 (붕괴 스위치가 꺼져 있어도 시험 동안 0.15 로 켠다)
            float brkSave = cr.BreakChance; if (brkSave <= 0) cr.BreakChance = 0.15f;
            var w2 = NewWorld(opt.Seed + 68);
            var o = w2.SpawnUnit(U["infantry"], 1, c); var o2 = w2.SpawnUnit(U["infantry"], 1, c + new Vector2(40, 0));
            w2.Step();
            float P(Unit x) { x.Suppression = (short)cr.SuppressMax; return w2.BreakChanceOf(x); }
            float base0 = P(o);
            var br = w2.SpawnUnit(U["standard_bearer"], 1, c + new Vector2(4, 0));
            var enemyBr = w2.SpawnUnit(U["standard_bearer"], 2, c + new Vector2(3, 3));
            w2.Step();
            float withB = P(o), far = P(o2);
            check(base0 > 0 && withB < base0 && MathF.Abs(withB / base0 - cr.BearerBreakMul) < 0.01f && MathF.Abs(far - base0) < 1e-6f, $"units: 기수 근처 붕괴 확률 {base0:0.000} -> {withB:0.000} (먼 병사 {far:0.000}, 적 기수는 무관)");
            br.Hp = 1; w2.Damage(br, 100, null);
            w2.Step();
            float fell = P(o);
            check(!br.Alive && o.BearerFellUntil > w2.Tick && fell > base0 && MathF.Abs(fell / base0 - cr.BearerFellMul) < 0.01f && o2.BearerFellUntil == 0, $"units: 기수 전사 -> 근처 병사 붕괴 확률 증가 ({fell:0.000}, 먼 병사 영향 없음)");
            var meta = new SnapshotMeta("units", 1, false, "standard");
            var bytes = Snapshot.Save(w2, meta);
            var y = Snapshot.Load(bytes, defs).World;
            check(y.Units.First(u => u.Id == o.Id).BearerFellUntil == o.BearerFellUntil && y.ComputeChecksum() == w2.ComputeChecksum() && Snapshot.RawBody(Snapshot.Save(y, meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes)), "units: 기수 쓰러짐 표시 저장 · 불러오기 · 체크섬 동일");
            long end = o.BearerFellUntil;
            while (w2.Tick <= end) w2.Step();
            check(MathF.Abs(P(o) - base0) < 1e-6f, "units: 쓰러짐 효과는 기간이 지나면 사라짐");
            cr.BreakChance = brkSave;
            // 타찬카
            var t = U["heavy_tachanka"]; var mx = U["heavy_maxim"];
            check(t.Weapon == mx.Weapon && t.Speed > mx.Speed && World.SetupTicks(t) < World.SetupTicks(mx) && World.PackTicks(t) < World.PackTicks(mx) && t.Heavy, $"units: 타찬카 = 맥심 화기, 더 빠름 ({t.Speed} vs {mx.Speed}), 방열 {World.SetupTicks(t)} < {World.SetupTicks(mx)} 틱");
            var w3 = NewWorld(opt.Seed + 78);
            var tk = w3.SpawnUnit(t, 1, c);
            var f3 = w3.SpawnUnit(U["infantry"], 2, c + new Vector2(14, 0)); f3.Weapon = null; f3.Hp = 50000; w3.Hold(2, new[] { f3 });
            int tFire = -1;
            for (int i = 0; i < 15 * 60 && tFire < 0; i++) { w3.Step(); if (tk.Cooldown > 0) tFire = i; }
            check(tFire >= 0 && tFire < 8 * 60 && tk.Deployed, $"units: 타찬카 방열 뒤 사격 (첫 사격 {tFire} 틱)");
            // newunits (N68 · N71): 사냥꾼 · 마적 엽총수 · 초안 마적 · 민단 자위대
            string[] nk = { "common_hunter", "bandit_shotgunner", "common_draft_bandit", "common_village_militia" };
            check(nk.All(k => U.TryGetValue(k, out var d) && d.Name.Length > 0 && d.Description.Length > 0 && d.Weapon != null && defs.Weapon(d.Weapon) != null && (d.ArtAs == null || U.ContainsKey(d.ArtAs))), "units: 새 병종 4종 정의 로드 (이름 · 설명 · 무기 · 그림 대용)");
            check(new[] { "korean", "japanese", "fengtian", "russian", "bandit" }.All(e => U["common_hunter"].AllowedFor(e) && U["common_draft_bandit"].AllowedFor(e) && U["common_village_militia"].AllowedFor(e)), "units: 사냥꾼 · 초안 마적 · 민단 = 모든 세력 편성 가능 (공용)");
            check(U["bandit_shotgunner"].AllowedFor("bandit") && !U["bandit_shotgunner"].AllowedFor("korean") && !U["bandit_shotgunner"].AllowedFor("fengtian"), "units: 마적 엽총수 = 마적 전용");
            check(U["common_hunter"].Accuracy > U["infantry"].Accuracy && U["common_village_militia"].SuppressResist > U["infantry"].SuppressResist && U["common_village_militia"].SuppressResist == U["common_gando_volunteer"].SuppressResist
                && U["common_draft_bandit"].SuppressResist > U["bandit_rifleman"].SuppressResist && U["common_draft_bandit"].Accuracy < 1f, "units: 사냥꾼 정확 · 민단 제압에 약함 (의용군과 같음) · 초안 마적 규율 약함");
            var sgW = defs.Weapon("shotgun")!; var rfW = defs.Weapon("rifle_mosin")!;
            check(sgW.Range < rfW.Range / 2 && sgW.Damage > rfW.Damage && sgW.Cooldown > rfW.Cooldown, "units: 엽총 = 사거리 짧고 한 방 강하고 간격 김");
            var wn = NewWorld(opt.Seed + 71);
            var sg = wn.SpawnUnit(U["bandit_shotgunner"], 1, c); var tg = wn.SpawnUnit(U["infantry"], 2, c + new Vector2(6, 0)); tg.Weapon = null; tg.Hp = 50000; wn.Hold(2, new[] { tg });
            float hp0 = tg.Hp;
            for (int i = 0; i < 40 * 60; i++) wn.Step();
            check(tg.Hp < hp0, "units: 엽총수 6칸 표적 사격 피해");
            var tg2 = wn.SpawnUnit(U["infantry"], 2, c + new Vector2(20, 0)); tg2.Weapon = null; tg2.Hp = 50000; wn.Hold(2, new[] { tg2 });
            float hp2 = tg2.Hp; for (int i = 0; i < 40 * 60; i++) wn.Step();
            check(tg2.Hp == hp2, "units: 엽총수 20칸 밖 표적은 사거리 밖");
            // 기마 포병
            var ha = U["heavy_horse_artillery"]; var ig = U["heavy_infantry_gun"];
            check(ha.Weapon == ig.Weapon && ha.Speed > ig.Speed && ha.Speed < t.Speed + 0.01f && World.SetupTicks(ha) < World.SetupTicks(ig) && World.PackTicks(ha) < World.PackTicks(ig) && ha.Heavy, $"units: 기마 포병 = 보병포 화기, 보병포보다 빠름 ({ha.Speed} vs {ig.Speed}), 방열 {World.SetupTicks(ha)} < {World.SetupTicks(ig)} 틱");
            var w4 = NewWorld(opt.Seed + 79);
            var hg = w4.SpawnUnit(ha, 1, c);
            var f4 = w4.SpawnUnit(U["infantry"], 2, c + new Vector2(14, 0)); f4.Weapon = null; f4.Hp = 50000; w4.Hold(2, new[] { f4 });
            int hFire = -1;
            for (int i = 0; i < 15 * 60 && hFire < 0; i++) { w4.Step(); if (hg.Cooldown > 0) hFire = i; }
            check(hFire >= 0 && hFire < 10 * 60 && hg.Deployed, $"units: 기마 포병 방열 뒤 사격 (첫 사격 {hFire} 틱)");
            // C1E1-u2: 봉천 기마 · 봉천 야포 · 일본 철도수비대 · 피난민 · 의용병 정의
            string[] ens6 = { "korean", "japanese", "fengtian", "bandit", "russian", "soviet" };
            string[] ck = { "cavalry_fengtian", "heavy_field_gun_fengtian", "infantry_railway_guard_japanese", "civilian_refugee", "common_volunteer" };
            check(ck.All(k => U.TryGetValue(k, out var d) && d.Name.Length > 0 && d.Description.Length > 0 && (d.Weapon == null || defs.Weapon(d.Weapon) != null) && d.ArtAs != null && U.ContainsKey(d.ArtAs)), "units: C1E1 유닛 5종 정의 로드 (이름 · 설명 · 무기 · artAs 가 실재 유닛)");
            check(ens6.Where(e => U["cavalry_fengtian"].AllowedFor(e)).SequenceEqual(new[] { "fengtian" }) && ens6.Where(e => U["heavy_field_gun_fengtian"].AllowedFor(e)).SequenceEqual(new[] { "fengtian" }), "units: 봉천 기마 · 봉천 야포 = 봉천 전용");
            check(ens6.Where(e => U["infantry_railway_guard_japanese"].AllowedFor(e)).SequenceEqual(new[] { "japanese" }), "units: 일본 철도수비대 = 일본 전용");
            check(ens6.All(e => U["civilian_refugee"].AllowedFor(e) && U["common_volunteer"].AllowedFor(e)), "units: 피난민 · 의용병 = 공용 (모든 세력 편성 가능)");
            var rf = U["civilian_refugee"];
            check(rf.Weapon == null && rf.Melee == null && !rf.CanCapture && !rf.AutoAttack && rf.Hp < U["infantry"].Hp && rf.Speed < U["infantry"].Speed, "units: 피난민 = 비무장 · 점령 불가 · 자동 사격 없음 · 보병보다 약하고 느림");
            var cf = U["cavalry_fengtian"]; var cd = U["cavalry_default"]; var fgF = U["heavy_field_gun_fengtian"]; var fgD = U["heavy_field_gun"]; var rg = U["infantry_railway_guard_japanese"]; var ij = U["infantry_japanese"];
            check(cf.Hp == cd.Hp && cf.Speed == cd.Speed && cf.Weapon == cd.Weapon && cf.Mounted && fgF.Hp == fgD.Hp && fgF.Weapon == fgD.Weapon && fgF.SetupTime == fgD.SetupTime && fgF.Heavy && rg.Hp == ij.Hp && rg.Weapon == ij.Weapon && rg.Melee == ij.Melee,
                "units: 봉천 기마 = 기병 · 봉천 야포 = 야전포 · 철도수비대 = 일본 보병 수치");
            var vm = U["common_volunteer"]; var vt = U["common_village_militia"];
            check(vm.Weapon == vt.Weapon && vm.Accuracy == vt.Accuracy && vm.SuppressResist == vt.SuppressResist && U.ContainsKey("common_volunteer_tmp"), "units: 의용병(common_volunteer) = 민단 · 의용군 수치, 자리표시자 _tmp 는 그대로");
            // 미션 편성 검증 (MissionDef.Validate → AllowedFor)
            string mtxt = File.ReadAllText(MissionDef.PathOf("bandit_raid"));
            bool Bad(string t) { try { MissionDef.Parse(t, "bad"); return false; } catch (InvalidDataException) { return true; } }
            check(!Bad(mtxt.Replace("\"infantry\": 28", "\"civilian_refugee\": 28").Replace("\"bandit_thug\": 14", "\"common_volunteer\": 14")), "units: 피난민 · 의용병 편성 = 미션 검증 통과 (공용)");
            check(Bad(mtxt.Replace("\"infantry\": 28", "\"cavalry_fengtian\": 28")) && Bad(mtxt.Replace("\"infantry\": 28", "\"infantry_railway_guard_japanese\": 28")) && Bad(mtxt.Replace("\"bandit_thug\": 14", "\"heavy_field_gun_fengtian\": 14")), "units: 봉천 기마 · 야포 · 철도수비대를 한국 · 마적 편성에 넣으면 로드 오류");
        }
        catch (Exception e) { check(false, $"units 테스트 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }
}
