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
                    combos++;
                    if (btns.Select(b => b.Hotkey).Distinct().Count() != btns.Count) bad++;
                    bad += CardPagesBad(btns);
                }
            check(bad == 0, $"카드 칸 · 단축키 충돌 없음 ({combos} 조합)");
            // T38c: 동적 버튼(포격 종류 · 집결 · 참호 돌격 · 사격 통제 …)까지 붙은 11개 · 25개 카드 — 쪽마다 칸 충돌 없음, 버튼은 정확히 한 쪽, 쪽 버튼은 (2,2)
            var big = new List<KoreanDivision.UI.CardButton>();
            string ks = "MSAQHXTVDEC";
            for (int n = 0; n < 11; n++) big.Add(new(ks[n].ToString(), "", "", ks[n].ToString(), Raylib_cs.KeyboardKey.A + n, n % 3, n / 3 % 3, ks[n].ToString()));   // 일부러 칸 겹침
            check(CardPagesBad(big) == 0 && Game.PaginateCard(big, 0, out int pg11).Count == 9 && pg11 == 2, "11개 카드 → 2쪽 (쪽당 8 + 쪽 버튼), 모든 버튼이 한 쪽에");
            var big25 = Enumerable.Range(0, 25).Select(n => new KoreanDivision.UI.CardButton("k" + n, "", "", "", Raylib_cs.KeyboardKey.Null, n % 3, n / 3 % 3, "")).ToList();
            check(CardPagesBad(big25) == 0 && Game.PaginateCard(big25, 99, out int pg25).Count == 2 && pg25 == 4, "25개 카드 → 4쪽, 범위 밖 쪽 번호는 마지막 쪽 (버튼 1 + 쪽 버튼)");
            var nine = big.Take(9).ToList();
            check(Game.PaginateCard(nine, 0, out int pg9).All(b => b.Key != "cardpage") && pg9 == 1 && CardPagesBad(nine) == 0, "9개 이하는 쪽 버튼 없이 옛 배치");
            var pgBtn = Game.PaginateCard(big, 1, out _).First(b => b.Key == "cardpage");
            check(pgBtn.Hotkey == Raylib_cs.KeyboardKey.Period && pgBtn.HotkeyLabel == "." && pgBtn.Value == "2/2", "쪽 버튼: 키 '.', 값 2/2");
            check(KoreanDivision.UI.Bindings.Extra.Any(x => x.Key == ".") && KoreanDivision.UI.Bindings.Conflict("move", '.') != null && KoreanDivision.UI.Bindings.TrySet("move", '.') != null, "'.' 는 고정 키 — 도움말 표에 있고 명령에 재지정 불가");
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

            // 3b) 색각 대응 도형 (T27 ⑧): 기본 꺼짐, 세력 1~5 가 서로 다른 도형, 같은 세력은 항상 같은 도형
            check(!KoreanDivision.UI.UserSettings.ColorBlind, "색각 대응 기본값 = 꺼짐");
            check(Enumerable.Range(1, 5).Select(KoreanDivision.Render.ColorBlind.Shape).Distinct().Count() == 5, "색각 대응: 세력 5종 도형이 모두 다름");
            check(KoreanDivision.Render.ColorBlind.Shape(1) == KoreanDivision.Render.ColorBlind.Shape(6) && Enumerable.Range(-2, 20).All(o => KoreanDivision.Render.ColorBlind.Shape(o) is >= 0 and < 5), "색각 대응: 세력 6 이상 순환 · 범위 밖 안전");

            // 3c) 도감 (T27 ⑥): 모든 유닛이 '전체' 에 있고 세력 · 분류 필터에 최소 하나씩 걸림; 설명 누락은 목록으로 보고
            var cx = defs.Units.Values.ToList();
            var facs = Enum.GetValues<KoreanDivision.UI.CodexMenu.FactionFilter>().Where(x => x != KoreanDivision.UI.CodexMenu.FactionFilter.All).ToList();
            var cats = Enum.GetValues<KoreanDivision.UI.CodexMenu.CategoryFilter>().Where(x => x != KoreanDivision.UI.CodexMenu.CategoryFilter.All).ToList();
            var noFac = cx.Where(u => !facs.Any(f => KoreanDivision.UI.CodexMenu.Filter(new[] { u }, f, KoreanDivision.UI.CodexMenu.CategoryFilter.All).Count > 0)).Select(u => u.Key).ToList();
            var noCat = cx.Where(u => !cats.Any(c => KoreanDivision.UI.CodexMenu.Filter(new[] { u }, KoreanDivision.UI.CodexMenu.FactionFilter.All, c).Count > 0)).Select(u => u.Key).ToList();
            check(noFac.Count == 0, $"도감: 세력 필터에 안 걸리는 유닛 0 ({string.Join(",", noFac)})");
            check(noCat.Count == 0, $"도감: 분류 필터에 안 걸리는 유닛 0 ({string.Join(",", noCat)})");
            var noDesc = cx.Where(u => string.IsNullOrWhiteSpace(u.Description)).Select(u => u.Key).ToList();
            Console.WriteLine($"  [도감 보고] 설명 문구 누락 {noDesc.Count}/{cx.Count}: {string.Join(", ", noDesc)}");

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
        Console.WriteLine("[단축키 재지정 (T38a)]");
        try { CheckRemap(check); } catch (Exception ex) { check(false, $"단축키 재지정 테스트 예외: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        Console.WriteLine("[머리 위 표시 단계 (T38m)]");
        try { CheckOverlay(check); } catch (Exception ex) { check(false, $"머리 위 표시 테스트 예외: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        Console.WriteLine("[도움말 단축키 = CONTROLS.md]");
        try { CheckListScroll(check); } catch (Exception ex) { check(false, $"T38e 목록 스크롤 테스트 예외: {ex.GetType().Name}: {ex.Message}"); }
        try { CheckControlsDoc(check); } catch (Exception ex) { check(false, $"도움말 문서 동기 테스트 예외: {ex.GetType().Name}: {ex.Message}"); }
    }

    /// <summary>T38a: 단축키 재지정 — 충돌 거부 · settings.json 왕복 · 기본값 복원 · 손상 파일 방어 · 도움말/카드 반영. 임시 settings 파일 사용, 끝에 원복.</summary>
    static void CheckRemap(Action<bool, string> check)
    {
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kd_keys_" + Guid.NewGuid().ToString("N") + ".json");
        var oldPath = KoreanDivision.UI.UserSettings.PathOverride;
        var oldMap = new Dictionary<string, char>(KoreanDivision.UI.UserSettings.KeyMap);
        try
        {
            KoreanDivision.UI.UserSettings.PathOverride = tmp;
            KoreanDivision.UI.Bindings.ResetAll();
            var move = Commands.Get("move")!; var stop = Commands.Get("stop")!;
            check(KoreanDivision.UI.Bindings.KeyOf(move) == 'M', "기본: 이동 = M");
            check(KoreanDivision.UI.Bindings.TrySet("move", 'S') != null && KoreanDivision.UI.Bindings.KeyOf(move) == 'M', "다른 명령(정지 S)이 쓰는 키는 거부, 키 그대로");
            check(KoreanDivision.UI.Bindings.TrySet("move", 'F') != null, "고정 키(F 돌격 사격)는 거부");
            check(KoreanDivision.UI.Bindings.TrySet("move", '1') != null, "글자 아닌 키는 거부");
            check(KoreanDivision.UI.Bindings.TrySet("move", 'z') == null && KoreanDivision.UI.Bindings.KeyOf(move) == 'Z', "빈 키 Z 로 재지정 (소문자도 대문자로)");
            check(KoreanDivision.UI.Bindings.TrySet("stop", 'M') == null, "비어진 기본 키 M 을 다른 명령이 쓸 수 있음");
            check(KoreanDivision.UI.Bindings.All().Any(x => x.Key == "Z" && x.Text == move.Label) && KoreanDivision.UI.Bindings.All().Any(x => x.Key == "M" && x.Text == stop.Label), "도움말 일람(All)이 새 키를 반영");
            check(KoreanDivision.UI.Bindings.RaylibKeyOf(move) == Raylib_cs.KeyboardKey.Z, "카드 단축키(RaylibKeyOf)가 새 키를 반영");
            check(KoreanDivision.UI.Bindings.AllDefaults().Any(x => x.Key == "M" && x.Text == move.Label), "AllDefaults 는 기본 키 그대로");
            KoreanDivision.UI.UserSettings.Save();
            KoreanDivision.UI.UserSettings.KeyMap = new();
            KoreanDivision.UI.UserSettings.Load();
            check(KoreanDivision.UI.Bindings.KeyOf(move) == 'Z' && KoreanDivision.UI.Bindings.KeyOf(stop) == 'M', "settings.json 왕복 후 재지정 유지");
            var all = Commands.UnitCommands.Select(KoreanDivision.UI.Bindings.KeyOf).ToList();
            check(all.Distinct().Count() == all.Count, "재지정 뒤에도 카드 키 중복 없음");
            KoreanDivision.UI.Bindings.TrySet("move", 'M');   // 기본값과 같으면 재지정 항목이 사라짐 (M 은 정지가 쓰는 중이라 거부)
            check(KoreanDivision.UI.UserSettings.KeyMap.ContainsKey("move"), "기본 키가 남이 쓰는 중이면 복원 시도도 거부");
            KoreanDivision.UI.Bindings.ResetAll();
            check(KoreanDivision.UI.Bindings.KeyOf(move) == 'M' && KoreanDivision.UI.Bindings.KeyOf(stop) == 'S', "모두 기본값 복원");
            // 손으로 망가뜨린 파일: 중복 · 고정 키 · 모르는 명령 · 숫자
            File.WriteAllText(tmp, "{\"Keys\":{\"move\":\"K\",\"stop\":\"A\",\"nope\":\"Q\",\"hold\":\"7\",\"attack\":\"A\"}}");
            KoreanDivision.UI.UserSettings.Load();
            var all2 = Commands.UnitCommands.Select(KoreanDivision.UI.Bindings.KeyOf).ToList();
            check(all2.Distinct().Count() == all2.Count && KoreanDivision.UI.Bindings.KeyOf(move) == 'M' && KoreanDivision.UI.Bindings.KeyOf(Commands.Get("hold")!) == 'H', "손상된 키 항목(고정 키 · 숫자 · 중복)은 버리고 키 중복 없음");
        }
        finally
        {
            KoreanDivision.UI.UserSettings.PathOverride = oldPath;
            KoreanDivision.UI.UserSettings.KeyMap = oldMap;
            try { File.Delete(tmp); } catch { }
        }
    }

    /// <summary>T38c: 쪽 나눔 불량 수 — 쪽마다 칸 중복 · 칸 범위 밖 · 쪽 버튼 위치 · 모든 버튼이 정확히 한 쪽에 보이는지.</summary>
    static int CardPagesBad(List<KoreanDivision.UI.CardButton> btns)
    {
        int bad = 0;
        var seen = new HashSet<string>();
        Game.PaginateCard(btns, 0, out int pgs);
        for (int pg = 0; pg < pgs; pg++)
        {
            var vis = Game.PaginateCard(btns, pg, out _);
            if (vis.Select(b => b.Row * 3 + b.Col).Distinct().Count() != vis.Count || vis.Any(b => b.Col is < 0 or > 2 || b.Row is < 0 or > 2)) bad++;
            if (pgs > 1 && !vis.Exists(b => b.Key == "cardpage" && b.Col == 2 && b.Row == 2)) bad++;
            foreach (var vb in vis) if (vb.Key != "cardpage" && !seen.Add(vb.Key)) bad++;
        }
        if (seen.Count != btns.Count) bad++;
        return bad;
    }

    /// <summary>T38e: 부대 목록 스크롤 순수 함수 (40행 · 18행 창) + F2 도감 키가 Bindings 에 있음.</summary>
    static void CheckListScroll(Action<bool, string> check)
    {
        const int V = 18, N = 40;
        check(KoreanDivision.UI.Hud.ListClamp(-5, N, V) == 0 && KoreanDivision.UI.Hud.ListClamp(99, N, V) == N - V, "목록 스크롤 범위 0..N-18");
        check(KoreanDivision.UI.Hud.ListClamp(5, 10, V) == 0, "18행 이하는 스크롤 0");
        check(KoreanDivision.UI.Hud.ListEnsureVisible(0, 30, N, V) == 30 - V + 1, "아래 행 선택 -> 그 행이 맨 아래에 보임");
        check(KoreanDivision.UI.Hud.ListEnsureVisible(20, 3, N, V) == 3, "위 행 선택 -> 그 행이 맨 위에 보임");
        check(KoreanDivision.UI.Hud.ListEnsureVisible(5, 10, N, V) == 5, "이미 보이면 그대로");
        check(KoreanDivision.UI.Hud.ListEnsureVisible(0, N - 1, N, V) == N - V, "마지막 행 -> 끝까지");
        check(KoreanDivision.UI.Hud.ListScrollFromFraction(0f, N, V) == 0 && KoreanDivision.UI.Hud.ListScrollFromFraction(1f, N, V) == N - V && KoreanDivision.UI.Hud.ListScrollFromFraction(0.5f, N, V) == 11, "막대 끌기 비율 -> 시작 행");
        check(KoreanDivision.UI.Bindings.AllDefaults().Any(x => x.Key == "F2"), "도감 키 F2 가 Bindings 에 있음");
    }

    /// <summary>T38m: 단계별 가시성 규칙 (순수 함수) · 기본 간결 · settings.json 저장 / 관대한 읽기. 임시 파일, 끝에 원복.</summary>
    static void CheckOverlay(Action<bool, string> check)
    {
        var C = KoreanDivision.UI.OverlayLevel.Concise; var N = KoreanDivision.UI.OverlayLevel.Normal; var D = KoreanDivision.UI.OverlayLevel.Detailed;
        bool hp(KoreanDivision.UI.OverlayLevel l, bool sel, bool hov, bool rec, bool own) => KoreanDivision.Render.OverlayRules.ShowHp(l, sel, hov, rec, own);
        bool bars(KoreanDivision.UI.OverlayLevel l, bool sel, bool own, bool low) => KoreanDivision.Render.OverlayRules.ShowAmmoCargo(l, sel, own, low);
        check(!hp(C, false, false, false, true) && hp(C, true, false, false, true) && hp(C, false, true, false, false) && hp(C, false, false, true, false), "간결: 체력 막대는 선택 · 호버 · 피격만");
        check(!hp(N, false, false, false, true) && hp(D, false, false, false, true) && !hp(D, false, false, false, false), "체력: 보통은 상시 없음, 상세는 아군 상시 (적 제외)");
        check(!bars(C, false, true, true) && bars(C, true, true, false) && !bars(C, true, false, false), "간결: 탄약 · 적재량 막대는 선택한 아군만 (부족해도 상시 없음)");
        check(bars(N, false, true, true) && !bars(N, false, true, false) && bars(D, false, true, false) && !bars(D, true, false, true), "보통 = 선택 또는 부족, 상세 = 아군 상시, 적군은 어느 단계에서도 없음");
        bool sq(KoreanDivision.UI.OverlayLevel l, KoreanDivision.Render.SquadIcons.Status st, bool sel) => KoreanDivision.Render.OverlayRules.ShowSquadIcon(l, st, sel);
        check(!sq(C, KoreanDivision.Render.SquadIcons.Status.Holding, false) && sq(C, KoreanDivision.Render.SquadIcons.Status.Holding, true), "간결: 참호 같은 평범한 상태는 선택 분대만");
        check(new[] { KoreanDivision.Render.SquadIcons.Status.Pinned, KoreanDivision.Render.SquadIcons.Status.Suppressed, KoreanDivision.Render.SquadIcons.Status.Retreating, KoreanDivision.Render.SquadIcons.Status.OutOfAmmo, KoreanDivision.Render.SquadIcons.Status.LowAmmo }.All(x => sq(C, x, false)), "간결: 고정 · 제압 · 후퇴 · 탄약 없음/부족 아이콘은 선택 없이도 경고");
        check(!sq(C, KoreanDivision.Render.SquadIcons.Status.None, true) && sq(N, KoreanDivision.Render.SquadIcons.Status.Holding, false) && sq(D, KoreanDivision.Render.SquadIcons.Status.Charging, false), "None 은 안 그림, 보통 · 상세는 모든 상태");
        var KR = KoreanDivision.Render.OverlayRules.ShowTeamMarker;
        check(!KR(C, false, false) && KR(C, true, false) && KR(C, false, true) && KR(N, false, false) && KR(D, false, false), "색각 도형: 간결은 선택 · 호버만, 보통 · 상세는 상시");
        check(KoreanDivision.Render.OverlayRules.ShowProgress(C, true) && !KoreanDivision.Render.OverlayRules.ShowProgress(D, false), "진행 막대: 동작 중일 때만");

        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kd_overlay_" + Guid.NewGuid().ToString("N") + ".json");
        var oldPath = KoreanDivision.UI.UserSettings.PathOverride; var oldLv = KoreanDivision.UI.UserSettings.Overlay;
        try
        {
            KoreanDivision.UI.UserSettings.PathOverride = tmp;
            File.WriteAllText(tmp, "{\"Width\":1280,\"Height\":720}");   // Overlay 항목 없는 옛 파일 = 기본 간결
            KoreanDivision.UI.UserSettings.Overlay = D; KoreanDivision.UI.UserSettings.Load();
            check(KoreanDivision.UI.UserSettings.Overlay == C, "기본 단계 = 간결 (옛 settings.json 에 항목 없음)");
            foreach (var lv in new[] { N, D, C })
            {
                KoreanDivision.UI.UserSettings.Overlay = lv; KoreanDivision.UI.UserSettings.Save();
                KoreanDivision.UI.UserSettings.Overlay = lv == C ? N : C; KoreanDivision.UI.UserSettings.Load();
                check(KoreanDivision.UI.UserSettings.Overlay == lv, $"단계 {lv} 저장 · 불러오기 왕복");
            }
            File.WriteAllText(tmp, "{\"Overlay\":\"엉뚱한값\"}"); KoreanDivision.UI.UserSettings.Overlay = D; KoreanDivision.UI.UserSettings.Load();
            check(KoreanDivision.UI.UserSettings.Overlay == C, "모르는 값 = 간결");
            File.WriteAllText(tmp, "{\"Overlay\":\" DETAILED \"}"); KoreanDivision.UI.UserSettings.Load();
            check(KoreanDivision.UI.UserSettings.Overlay == D, "대소문자 · 공백 관대");
            File.WriteAllText(tmp, "{\"Overlay\":5}"); KoreanDivision.UI.UserSettings.Overlay = D; KoreanDivision.UI.UserSettings.Load();
            check(KoreanDivision.UI.UserSettings.Overlay == C, "형식이 틀린 값은 기본값 간결 (T49 관대한 읽기: 필드별 폴백, 예외 없음)");
            check(KoreanDivision.UI.UserSettings.NextOverlay(C) == N && KoreanDivision.UI.UserSettings.NextOverlay(N) == D && KoreanDivision.UI.UserSettings.NextOverlay(D) == C, "U 키 순환 간결 → 보통 → 상세 → 간결");
            check(KoreanDivision.UI.Bindings.Extra.Any(e => e.Key == "U") && KoreanDivision.UI.Bindings.Conflict("move", 'U') != null, "U 는 도움말 표에 있고 카드 키 재지정 대상에서 제외");
        }
        finally
        {
            KoreanDivision.UI.UserSettings.PathOverride = oldPath; KoreanDivision.UI.UserSettings.Overlay = oldLv;
            try { File.Delete(tmp); } catch { }
        }
    }

    /// <summary>T38b: docs/CONTROLS.md 「단축키 일람」 표 (BINDINGS 표지 사이) 가 Bindings.All() 과 한 줄도 어긋나지 않는다 (누락 0 · 잉여 0).</summary>
    static void CheckControlsDoc(Action<bool, string> check)
    {
        string? path = null;
        foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(root); d != null && path == null; d = d.Parent)
                if (File.Exists(System.IO.Path.Combine(d.FullName, "docs", "CONTROLS.md"))) path = System.IO.Path.Combine(d.FullName, "docs", "CONTROLS.md");
        check(path != null, "docs/CONTROLS.md 를 찾음");
        if (path == null) return;
        var lines = File.ReadAllLines(path);
        int b = Array.FindIndex(lines, l => l.Contains("<!-- BINDINGS:BEGIN")), e = Array.FindIndex(lines, l => l.Contains("<!-- BINDINGS:END"));
        check(b >= 0 && e > b, "CONTROLS.md 에 BINDINGS:BEGIN / END 표지가 있음");
        if (b < 0 || e <= b) return;
        var doc = lines.Skip(b + 1).Take(e - b - 1).Where(l => l.StartsWith("| `")).Select(l => l.TrimEnd()).ToList();
        var code = KoreanDivision.UI.Bindings.AllDefaults().Select(KoreanDivision.UI.Bindings.MarkdownRow).ToList();
        var missing = code.Except(doc).ToList();   // 코드에 있고 문서에 없음
        var extra = doc.Except(code).ToList();   // 문서에 있고 코드에 없음
        string detail = missing.Count + extra.Count > 0 ? Environment.NewLine + "  누락: " + string.Join(" / ", missing) + Environment.NewLine + "  잉여: " + string.Join(" / ", extra) : "";
        check(missing.Count == 0 && extra.Count == 0 && doc.Count == code.Count, $"CONTROLS.md 단축키 일람 = Bindings.All() (누락 {missing.Count}, 잉여 {extra.Count}){detail}");
        check(code.Distinct().Count() == code.Count, "Bindings.All() 에 같은 줄이 두 번 나오지 않음");
        check(KoreanDivision.UI.Bindings.AllDefaults().Any(x => x.Key == "F1"), "도움말 키 F1 이 Bindings 에 있음");
    }
}
