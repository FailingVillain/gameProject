using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>장갑 차량 3종 (armour 2026-10-21): Mark V 수컷 · 랜체스터 · 봉천 급조 장갑차 — 정의 · 세력 제한 · 시험 미션 (KD_SELFTEST_ONLY=armour).</summary>
public static class ArmourSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            var d = Defs.Current;
            var U = d.Units;
            var mv = U["tank_mark_v"]; var lc = U["tank_lanchester"]; var ft = U["tank_fengtian_truck"];
            check(mv.IsArmored && lc.IsArmored && ft.IsArmored && mv.Category == "armored", "장갑 차량 3종 정의 · 병과 armored");
            string[] all = { "korean", "russian", "soviet", "japanese", "fengtian", "bandit" };
            check(all.Where(e => mv.AllowedFor(e)).SequenceEqual(new[] { "korean", "russian", "soviet" }), "Mark V: 한국 · 러시아 백군 · 적군만");
            check(all.Where(e => lc.AllowedFor(e)).SequenceEqual(new[] { "korean", "russian", "soviet" }), "랜체스터: 한국 · 백군 · 적군 (일본 · 봉천 · 마적 불가)");
            check(all.Where(e => ft.AllowedFor(e)).SequenceEqual(new[] { "fengtian" }), "봉천 급조 장갑차: 봉천만 (마적 · 일본 불가)");
            check(!U.Values.Where(u => u.IsArmored && u.Key != "tank_ft_male").Any(u => u.AllowedFor("bandit") && u.Factions != null), "마적은 장갑 차량 없음");
            check(mv.Weapon == "gun_6pdr" && d.Weapons["gun_6pdr"].Damage > d.Weapons["ft_gun"].Damage && mv.Sidearm == "mg" && !mv.Weapon!.Equals("mg"), "Mark V: 6파운더 (FT 37mm 보다 강함) + 근거리 기관총");
            check(mv.TurretTurnRate == 0 && lc.TurretTurnRate > 0 && ft.TurretTurnRate > 0, "Mark V 는 포탑 없음, 나머지는 회전 포탑");
            check(mv.Speed < U["infantry"].Speed && mv.Hp > U["tank_ft_male"].Hp * 1.5f && d.Armor(mv.Armor)!.Armor > d.Armor("basic")!.Armor, "Mark V: 보병보다 느리고 FT 보다 튼튼 · 중장갑");
            check(ft.Hp < U["tank_armored_car"].Hp && ft.Speed < U["tank_armored_car"].Speed && lc.Speed > U["tank_armored_car"].Speed, "급조 장갑차는 Austin 보다 약하고 느림");
            // 시험 미션: 로드 · 배치 · 결정성 (60초)
            var md = MissionDef.Load("armour_test");
            ulong Run1()
            {
                var w = World.CreateMissionWorld(md, d, 1, new List<DeployEntry>());
                World.AttachMissionAi(w, md.Player, true);
                for (long t = 0; t < 60 / Config.SimDt; t++) w.Step();
                return (ulong)w.ComputeChecksum();
            }
            var w0 = World.CreateMissionWorld(md, d, 1, new List<DeployEntry>());
            check(w0.Units.Count(u => u.Def.Key == "tank_mark_v" && u.Owner == 1) == 2 && w0.Units.Count(u => u.Def.Key == "tank_lanchester" && u.Owner == 1) == 1 && w0.Units.Count(u => u.Def.Key == "tank_fengtian_truck" && u.Owner == 2) == 2, "armour_test 배치: Mark V 2 · 랜체스터 1 · 급조 2");
            check(Run1() == Run1(), "armour_test 60초 두 번 = 같은 체크섬");
            var wm = World.CreateMissionWorld(md, d, 1, new List<DeployEntry>());
            World.AttachMissionAi(wm, md.Player, true);
            for (long t = 0; t < 90 / Config.SimDt; t++) wm.Step();
            var marks = wm.Units.Where(u => u.Def.Key == "tank_mark_v" && u.Hp > 0).ToList();
            check(marks.Count == 0 || marks.Any(u => u.Pos.Y < 90), "Mark V 가 90초 안에 전진함 (제자리 정지 아님)");
        }
        catch (Exception e)
        {
            check(false, $"장갑 차량 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }
}
