using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>교전 테스트 화면의 현황 패널: 경과 시간 · 세력별 남은 수 / 손실 · 종류별 남은 수 · 시뮬 / 그리기 시간. 읽기 전용.</summary>
public static class BattlePanel
{
    static World? cachedFor;
    static List<(string Key, int A, int B)> initial = new();
    static readonly int[] remain = new int[2 * 64];
    static long countedTick = -1;

    public static void Draw(Game g, Rectangle vp)
    {
        var w = g.World;
        if (w.Battle == null) return;
        var defs = Defs.Current;
        if (cachedFor != w)
        {
            cachedFor = w;
            var a = BattleSetup.Parse(w.Battle.SideA, defs).ToDictionary(e => e.Unit, e => e.Count);
            var b = BattleSetup.Parse(w.Battle.SideB, defs).ToDictionary(e => e.Unit, e => e.Count);
            initial = defs.Units.Keys.Where(k => a.ContainsKey(k) || b.ContainsKey(k)).Select(k => (k, a.GetValueOrDefault(k), b.GetValueOrDefault(k))).ToList();
            countedTick = -1;
        }
        var keys = defs.Units.Keys.ToList();
        if (countedTick != w.Tick)
        {
            countedTick = w.Tick;
            Array.Clear(remain);
            foreach (var u in w.Units)
                if (u.Owner is 1 or 2 && u.Hp > 0) remain[(u.Owner - 1) * 64 + keys.IndexOf(u.Def.Key)]++;
        }

        int lines = 5 + initial.Count(e => e.A > 0) + initial.Count(e => e.B > 0);
        const float lh = 19;
        var panel = new Rectangle(vp.X + 8, vp.Y + 8, 330, lines * lh + 22);
        Raylib.DrawRectangleRec(panel, Palette.Alpha(Palette.PanelDark, 215));
        Raylib.DrawRectangleLinesEx(panel, 1, Palette.PanelBorder);
        float x = panel.X + 10, y = panel.Y + 7;
        Ui.Text($"{S.BattlePanelTitle}   {S.BattleElapsed} {Game.Clock(w.Tick)}", x, y, 16, Palette.Accent); y += lh;
        Ui.Text($"{S.BattleSim} {g.LastStepMs:0.00}ms  ·  {S.BattleDraw} {g.FrameDrawMs:0.00}ms  ·  {w.Units.Count}", x, y, 16, Palette.TextDim); y += lh + 4;

        for (int side = 0; side < 2; side++)
        {
            int total = 0, left = 0;
            foreach (var e in initial) { total += side == 0 ? e.A : e.B; }
            for (int k = 0; k < keys.Count; k++) left += remain[side * 64 + k];
            string name = w.Faction(side + 1).Name;
            Raylib.DrawRectangle((int)x, (int)y + 4, 10, 10, Palette.Player(side + 1));
            Ui.Text($"{name}   {S.BattleRemain} {left} / {total}   {S.BattleLost} {total - left}", x + 16, y, 16, Palette.Text); y += lh;
            foreach (var e in initial)
            {
                int init = side == 0 ? e.A : e.B;
                if (init == 0) continue;
                int now = remain[side * 64 + keys.IndexOf(e.Key)];
                Ui.Text($"{defs.Units[e.Key].Name}", x + 16, y, 16, Palette.TextDim);
                Ui.Text($"{now} / {init}", x + 210, y, 16, now == 0 ? Palette.HpLow : Palette.Text);
                y += lh;
            }
            y += 4;
        }
        Ui.Text(S.BattleRestartHint, x, y, 16, Palette.TextDim);
    }
}
