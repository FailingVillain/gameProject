using KoreanDivision.Render;

namespace KoreanDivision.Tests;

/// <summary>T38n: KD_MONITOR 모니터 선택 · 크기 clamp 순수 함수. KD_SELFTEST_ONLY=monitorpick. 창 없이.</summary>
public static class MonitorPickSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var two = new List<MonitorPick.Rect> { new(0, 0, 1920, 1080), new(1920, 0, 2560, 1440) };
        var one = new List<MonitorPick.Rect> { new(0, 0, 1920, 1080) };
        var s = MonitorPick.Choose(two, 0, "secondary", 1280, 720);
        check(s is { Index: 1, WinW: 1280, WinH: 720, PosX: 1920 + (2560 - 1280) / 2, PosY: (1440 - 720) / 2 }, "monitorpick: secondary = 2번째, 가운데");
        check(MonitorPick.Choose(two, 0, "primary", 1280, 720) is { Index: 0, PosX: 320, PosY: 180 }, "monitorpick: primary 가운데");
        check(MonitorPick.Choose(two, 1, "secondary", 1280, 720) is { Index: 0 }, "monitorpick: 주 모니터가 1번이면 secondary = 0번");
        check(MonitorPick.Choose(one, 0, "secondary", 1280, 720) is { Index: 0 }, "monitorpick: 모니터 하나면 secondary = primary");
        check(MonitorPick.Choose(two, 0, "1", 1280, 720) is { Index: 1 }, "monitorpick: 정수 인덱스");
        check(MonitorPick.Choose(two, 0, "9", 1280, 720) is { Index: 1 }, "monitorpick: 범위 밖 인덱스는 clamp");
        var small = MonitorPick.Choose(new List<MonitorPick.Rect> { new(0, 0, 1024, 768) }, 0, "primary", 1280, 720);
        check(small is { WinW: 1024 - MonitorPick.Margin, WinH: 688 }, "monitorpick: 모니터보다 큰 창은 크기 줄임");
        check(small is { } sm && sm.PosX >= 0 && sm.PosX + sm.WinW <= 1024 && sm.PosY + sm.WinH <= 768, "monitorpick: 창이 모니터 안");
        check(MonitorPick.Choose(two, 0, null, 1280, 720) == null && MonitorPick.Choose(two, 0, "", 1280, 720) == null
            && MonitorPick.Choose(two, 0, "bogus", 1280, 720) == null && MonitorPick.Choose(new List<MonitorPick.Rect>(), 0, "primary", 1280, 720) == null,
            "monitorpick: 미설정 · 이상값 · 모니터 없음 = 옮기지 않음");
    }
}
