using System.Text.RegularExpressions;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T45 시작 화면 정리: 플레이 버튼 숨김(개발 플래그) · 관전 설정의 다자간 선택 숨김 · AI 대 AI 미션이 미션 목록에서 관전 탭으로 이동. 창 없이. KD_SELFTEST_ONLY=menuhide.</summary>
public static class MenuHideSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        Console.WriteLine("[T45 메뉴 숨김 · 관전 미션 목록]");
        try
        {
            bool oldDev = MainMenu.DevMenu;
            MainMenu.DevMenu = false;
            check(!MainMenu.PlayButtonVisible, "개발 메뉴 꺼짐 -> 플레이 버튼 숨김");
            MainMenu.DevMenu = true;
            check(MainMenu.PlayButtonVisible, "개발 메뉴 켜짐 -> 플레이 버튼 보임 (--dev-menu / KD_DEV_MENU=1)");
            MainMenu.DevMenu = oldDev;

            // 근거: 미션 파일 원문에서 "spectate": true 를 직접 찾는다 (MissionDef 와 독립)
            var raw = new List<string>();
            foreach (var id in MissionDef.Available())
                if (Regex.IsMatch(File.ReadAllText(MissionDef.PathOf(id)), @"""spectate""\s*:\s*true")) raw.Add(id);
            var menu = new MainMenu();
            var spec = menu.SpectateMissions.Select(m => m.Id).ToList();
            check(spec.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(raw.OrderBy(x => x, StringComparer.Ordinal)) && spec.Count >= 4,
                $"관전 탭 목록 = spectate:true 미션 ({string.Join(",", spec)})");
            foreach (var need in new[] { "ai_bn_meeting", "ai_bn_assault", "ai_kr_vs_ft_1000", "ai_kr_vs_jp_trench" })
                check(spec.Contains(need), $"관전 탭에 '{need}'");
            check(menu.SpectateMissions.All(m => m.Title.Length > 0), "관전 미션 제목 있음");

            var mm = new MissionMenu();
            var listed = mm.ListedIds;
            check(!listed.Any(id => spec.Contains(id)), "미션 메뉴 목록에 관전 미션 없음");
            check(listed.Concat(spec).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(MissionDef.Available().OrderBy(x => x, StringComparer.Ordinal)),
                $"미션 메뉴 {listed.Count} + 관전 탭 {spec.Count} = 전체 미션 {MissionDef.Available().Count()} (빠진 것 없음)");
            check(listed.Contains("yalu_crossing") && listed.Contains("c0_01_bandits"), "일반 미션은 미션 메뉴에 그대로");

            // CLI/id 경로는 영향 없음: 관전 미션도 id 로 읽히고 Spectate 플래그 유지
            foreach (var id in spec)
                check(MissionDef.Load(id).Spectate && File.Exists(MissionDef.PathOf(id)), $"id 로 읽기 '{id}' (--mission-run / tactics-obs 경로)");

            // 관전 설정: 다자간(3인 이상) 지도는 숨기고, 개발 메뉴에선 그대로
            check(MainMenu.SpectateDefaultPlayers == 2, "관전 기본 인원 2 (AI 대 AI)");
            var hidden = menu.VisibleFixedMapIds(true);
            check(!hidden.Contains("corners4") && !hidden.Contains("flat4") && menu.VisibleFixedMapIds(false).Contains(MainMenu.DefaultMapId),
                $"관전 설정 지도 칩: 숨김 상태={string.Join(",", hidden)} (4인 지도 뺌), 개발 메뉴={string.Join(",", menu.VisibleFixedMapIds(false))}");
        }
        catch (Exception e) { check(false, $"menuhide 예외: {e.GetType().Name}: {e.Message}"); }
    }
}
