using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// F1 (접적 · 사격 위치) 관찰 — 읽기 전용, TacticsObs 가 1초마다 부른다. 접적 정의는 시뮬과 같은 World.ContactTrigger
/// (표적 · contactTriggerSec 안 사격 · 피격) 를 쓰고, 마지막 트리거 뒤 contactDisengageSec 안이면 '접적' 으로 센다 (기능 켜짐 여부와 무관 — 전 / 후 비교용).
/// 지표: 접적 중 보병 (장교 제외) 초 · 그중 사격 (3초 안 사격) 비율 · 분당 이동 거리 (1초 표본 사이 직선 거리 합).
/// </summary>
internal static class F1Obs
{
    sealed class S { public double Sec, Fire, Move; }
    static readonly Dictionary<int, long> last = new();
    static readonly Dictionary<int, Vector2> prev = new();
    static readonly Dictionary<int, S> side = new();
    static double compSec, contactCompSec;   // F1 2차: 살아 있는 중대-초 · 접적 중대-초 (분당 명령 수의 분모)

    internal static void Reset() { last.Clear(); prev.Clear(); side.Clear(); compSec = contactCompSec = 0; }

    internal static void Sample(World w)
    {
        var cr = w.Defs.CombatRules;
        long hold = (long)MathF.Round(cr.ContactDisengageSec / Config.SimDt);
        long fireWin = (long)MathF.Round(3f / Config.SimDt);
        foreach (var c in w.Companies)
        {
            if (w.ContactTrigger(c)) last[c.Id] = w.Tick;
            bool on = last.TryGetValue(c.Id, out long lt) && w.Tick - lt <= hold;
            if (c.Members.Exists(u => u.Alive)) { compSec += 1; if (w.ContactOf(c) != ContactState.None) contactCompSec += 1; }
            foreach (var u in c.Members)
            {
                if (!u.Alive || u.IsOfficer || !u.Def.IsInfantry) continue;
                bool had = prev.TryGetValue(u.Id, out var pp);
                prev[u.Id] = u.Pos;
                if (!on) continue;
                if (!side.TryGetValue(c.Owner, out var s)) side[c.Owner] = s = new S();
                s.Sec += 1;
                if (u.LastShotTick > 0 && w.Tick - u.LastShotTick <= fireWin) s.Fire += 1;
                if (had) s.Move += Vector2.Distance(pp, u.Pos);
            }
        }
    }

    internal static string Report(World w)
    {
        var st = w.Stats;
        var parts = side.OrderBy(k => k.Key).Select(k => $"세력{k.Key} 접적 보병-초 {k.Value.Sec:0}, 사격 비율 {(k.Value.Sec > 0 ? k.Value.Fire / k.Value.Sec : 0):0.000}, 분당 이동 {(k.Value.Sec > 0 ? k.Value.Move / (k.Value.Sec / 60) : 0):0.00}칸");
        return $"[F1] {string.Join(" | ", parts)} || 슬롯 복귀 {st.SlotReturns}, 빈자리 재배치 {st.GapRefills}, 대열정비 병사 {st.CloseMovers}, 장교 정비 명령 {st.CloseOrders}, 접적 진입 {st.ContactEnters}, 사격 위치 {st.SpotPicks}, 위치 이동 {st.SpotMoves}, 구역 복귀 {st.ZoneReturns}, 고정 해제 {st.SpotReleases}, 중대 명령 {st.CompanyMoves}, 접적 중대 명령 {st.ContactCompanyMoves}, 중대분 {compSec / 60:0}, 접적 중대분 {contactCompSec / 60:0}, 방향 전환 {st.Rotations}, 제자리 병사 {st.RotateStays}, 선까지 전진 {st.LooseAdvances}, 재명령 보류 {st.AiContactHolds}";
    }
}
