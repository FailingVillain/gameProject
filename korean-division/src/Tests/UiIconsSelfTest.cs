using System.Numerics;
using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.Tests;

/// <summary>수류탄 X 키 · 아군 오사 계수 (측정 전용) · 분대 상태 아이콘 우선순위. 창 없이. KD_SELFTEST_ONLY=uiicons 로 단독 실행.</summary>
public static class UiIconsSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[수류탄 X · 아군 오사 · 분대 아이콘]");
        try
        {
            var defs = Defs.Current;
            // 1) 수류탄 = X, 다른 명령과 겹치지 않음, G 는 어떤 명령도 안 씀 (중대 병합 전용)
            var g = Commands.Get("grenade");
            check(g != null && g.Hotkey == 'X', "수류탄 단축키 = X");
            check(Commands.UnitCommands.Count(c => c.Hotkey == 'X') == 1, "X 를 쓰는 명령은 수류탄뿐");
            check(Commands.UnitCommands.All(c => c.Hotkey != 'G'), "G 는 어떤 명령도 안 씀 (중대 병합 전용)");

            // 1a) 병종 이미지 (축소 중대 기호 · 카드): 병종마다 이미지 이름, 테마 키가 가리키는 파일이 모두 있다. 지휘만 NATO 깃발.
            var names = Enum.GetValues<UnitSymbols.Branch>().Select(UnitSymbols.IconName).ToList();
            check(names.All(n => n != null) && names.Distinct().Count() == names.Count,
                $"병종 이미지 이름: 병종마다 하나 ({string.Join(",", names)}), 지휘 = officer");
            string themeDir = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "themes", "kd1920");
            if (!Directory.Exists(themeDir)) themeDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "assets", "themes", "kd1920");
            int missing = 0;
            foreach (var n in names) foreach (var px in new[] { 12, 16, 20, 24, 32 }) if (!File.Exists(System.IO.Path.Combine(themeDir, "icons", $"branch-{n}_{px}.png"))) missing++;
            check(missing == 0, $"병종 이미지 파일 모두 있음 (누락 {missing}, {themeDir})");
            bool defsOk = defs.Units.Values.All(d => UnitSymbols.Of(d) is var b && UnitSymbols.IconName(b) != null);
            check(defsOk, "모든 유닛 정의가 병종 이미지에 대응");
            // T43: 곡사포 · 저격은 자료(무기 속성)로 판정, 이름 하드코딩 없음. 유닛 -> 병종 전체 목록 (누락 0).
            var unitBranch = string.Join(", ", defs.Units.Values.Select(d => $"{d.Key}={UnitSymbols.Of(d)}"));
            Console.WriteLine("  [병종 매핑] " + unitBranch);
            check(defs.Units.Values.Any(d => UnitSymbols.Of(d) == UnitSymbols.Branch.Howitzer && d.Category == "gun" && d.Weapon != null && defs.Weapons[d.Weapon].Indirect),
                "곡사 무기 포 = Howitzer (곡사포 이미지)");
            check(defs.Units.Values.Any(d => UnitSymbols.Of(d) == UnitSymbols.Branch.Sniper && d.Weapon != null && defs.Weapons[d.Weapon].PreferOfficer),
                "장교 우선 표적 직사 무기 = Sniper (저격 이미지)");
            check(defs.Units.Values.Where(d => d.Category == "gun" && d.Weapon != null && !defs.Weapons[d.Weapon].Indirect).All(d => UnitSymbols.Of(d) == UnitSymbols.Branch.Artillery),
                "직사 포 = Artillery 유지");
            // T43b: 승마 유닛 = 기병 (category 가 infantry 인 기병 정의도 포함, 예: cavalry_default · cavalry_cossack). 지휘관 제외.
            check(defs.Units.Values.Any(d => d.Mounted && d.Category == "infantry"),
                "승마 보병 정의가 있음 (기병 판정 시험 대상)");
            check(defs.Units.Values.Where(d => d.Mounted && !d.IsOfficer && !d.Scout).All(d => UnitSymbols.Of(d) == UnitSymbols.Branch.Cavalry),
                "승마 유닛 = Cavalry (category 무관, 장교 · 승마 정찰 제외)");

            // 1b) 카드 칸: 어떤 선택 조합 (유닛 종류 1~2종) 에서도 두 버튼이 같은 칸을 쓰지 않는다 (N15-2)
            var all = defs.Units.Values.ToList();
            int bad = 0, combos = 0;
            for (int i = 0; i < all.Count; i++)
                for (int j = i; j < all.Count; j++)
                {
                    var sel = new[] { all[i], all[j] };
                    var btns = new List<KoreanDivision.UI.CardButton>();
                    foreach (var c in Commands.UnitCommands)
                        if (c.Key == "attack" ? sel.Any(d => c.Available(d)) : sel.All(d => c.Available(d)))
                            btns.Add(new KoreanDivision.UI.CardButton(c.Key, c.Label, c.Tooltip, c.Hotkey.ToString(), Commands.Key(c.Hotkey), c.Col, c.Row, c.Key));
                    if (sel.All(d => d.MarchFire && d.Category == "infantry")) btns.Add(new KoreanDivision.UI.CardButton("marchfire", "", "", "F", Raylib_cs.KeyboardKey.F, 0, 2, "marchfire"));
                    btns.Add(new KoreanDivision.UI.CardButton("closeranks", "", "", "L", Raylib_cs.KeyboardKey.L, 1, 2, "closeranks"));
                    Game.ResolveSlots(btns);
                    combos++;
                    if (btns.Select(b => b.Row * 3 + b.Col).Distinct().Count() != btns.Count || btns.Any(b => b.Col is < 0 or > 2 || b.Row is < 0 or > 2)) bad++;
                    if (btns.Select(b => b.Hotkey).Distinct().Count() != btns.Count) bad++;
                }
            check(bad == 0, $"카드 칸 · 단축키 충돌 없음 ({combos} 조합)");
            var tf = new List<KoreanDivision.UI.CardButton> { new("groundfire", "", "", "T", Raylib_cs.KeyboardKey.T, 0, 2, "g"), new("marchfire", "", "", "F", Raylib_cs.KeyboardKey.F, 0, 2, "m") };
            Game.ResolveSlots(tf);
            check(tf[0].Col == 0 && tf[0].Row == 2 && (tf[1].Col, tf[1].Row) != (0, 2) && tf[1].Hotkey == Raylib_cs.KeyboardKey.F, "T · F 가 같은 칸이면 F 가 빈 칸으로 이동, 단축키 유지");

            // 2) 아군 오사 계수
            var m = new GameMap(60, 60, 1);
            for (int y = 0; y < 60; y++) for (int x = 0; x < 60; x++) m.Set(x, y, TerrainType.Grass);
            var w = new World(m, defs, 2) { RecordCommands = false };
            w.PlaceBuilding(defs.Headquarters, 1, 1, 1);
            w.PlaceBuilding(defs.Headquarters, 2, 50, 50);
            w.Fog.Enabled = false;
            var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(20, 20));
            var b = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(21, 20));
            var e = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(30, 20));
            w.Damage(e, 1, a, "rifle");
            check(w.Stats.FriendlyFire.Count == 0, "적에게 준 피해는 아군 오사로 세지 않음");
            w.Damage(b, 1, a, "rifle");
            check(w.Stats.FriendlyFire.TryGetValue("rifle", out var ff) && ff.Hits == 1 && ff.Kills == 0, "아군 소총 피해 1회 계수");
            w.Damage(b, 9999, a, "grenade");
            check(w.Stats.FriendlyFire.TryGetValue("grenade", out var fg) && fg.Hits == 1 && fg.Kills == 1, "아군 수류탄 사살 계수 (무기별 분리)");
            var res = new MissionResults();
            check(res.FriendlyFire.Count == 0, "결과 구조에 아군 오사 칸이 있음");

            // 3) 아이콘 우선순위
            var f = new SquadIcons.Facts { Holding = true };
            check(SquadIcons.Pick(f) == SquadIcons.Status.Holding, "참호만 = Holding");
            f.Leaderless = true; check(SquadIcons.Pick(f) == SquadIcons.Status.Leaderless, "지휘관 없음 > 참호");
            f.Charging = true; check(SquadIcons.Pick(f) == SquadIcons.Status.Charging, "돌격 > 지휘관 없음");
            f.LowAmmo = true; check(SquadIcons.Pick(f) == SquadIcons.Status.LowAmmo, "탄약 부족 > 돌격");
            f.OutOfAmmo = true; check(SquadIcons.Pick(f) == SquadIcons.Status.OutOfAmmo, "탄약 없음 > 돌격");
            f.Retreating = true; check(SquadIcons.Pick(f) == SquadIcons.Status.Retreating, "후퇴 > 탄약 없음");
            f.Suppressed = true; check(SquadIcons.Pick(f) == SquadIcons.Status.Suppressed, "제압 > 후퇴");
            f.Pinned = true; check(SquadIcons.Pick(f) == SquadIcons.Status.Pinned, "고정 > 제압");
            check(SquadIcons.Pick(default) == SquadIcons.Status.None, "아무 상태도 없으면 None");
            check(Enum.GetValues<SquadIcons.Status>().Where(s => s != SquadIcons.Status.None).All(s => SquadIcons.Shape(s).Length == 7 && SquadIcons.Shape(s).All(r => r.Length == 7)), "아이콘 모양 7x7");

            // 4) 실제 분대에서 읽기: 장교 없는 보병 중대 = 지휘관 없음, 제압 올리면 Suppressed
            var us = new List<Unit>();
            for (int i = 0; i < 12; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(10 + i % 4, 40 + i / 4)));
            w.FormCompany(1, us); w.Step();
            var q = us[0].Squad;
            check(q != null, "분대 편성됨");
            if (q != null)
            {
                var r0 = SquadIcons.Read(w, q, false);
                check(r0.Leaderless && !r0.Suppressed, "장교 · 부사관 없는 분대 = Leaderless, 제압 아님");
                foreach (var u in q.Members) u.Suppression = (short)Defs.Current.CombatRules.SuppressMax;
                check(SquadIcons.Read(w, q, false).Suppressed, "제압 평균이 임계 이상이면 Suppressed");
                check(SquadIcons.Read(w, q, true).Retreating, "후퇴 훅 전달");
            }
        }
        catch (Exception ex) { check(false, $"아이콘 테스트 중 예외: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
    }
}
