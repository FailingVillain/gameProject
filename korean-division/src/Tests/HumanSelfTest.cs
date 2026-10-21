using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N34 덜 기계적인 전투 (2026-10-19 승인): ① 대형 · 방향 변경 병사별 반응 0~50ms (0~3틱, 결정적) ② 포마다 고정 발사 간격 배수 0.93~1.07
/// ③ 접촉 첫 사격 반응 (0.2~1.5초 × 숙련) — 중대 첫 일제 사격이 0.8초 넘게 흩어진다.
/// </summary>
public static class HumanSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        using var noDelay = TestDefs.NoCommandDelay();
        try
        {
            FormReact(opt, check);
            ArtyRate(opt, check);
            FirstVolley(opt, check);
        }
        catch (Exception e) { check(false, $"N34 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World NewWorld(int seed)
    {
        var w = TestDefs.Strip(World.CreateStandard(MapFile.Load("flat4", seed), Defs.Current));
        w.Fog.Enabled = false;
        return w;
    }

    static Vector2 Center(World w) => new(64.5f, 56.5f);

    static Unit Dummy(World w, int faction, Vector2 pos)
    {
        var u = w.SpawnUnit(Defs.Current.Units["infantry"], faction, pos);
        u.Weapon = null;
        u.Hp = 1000;
        w.Hold(faction, new[] { u });
        return u;
    }

    // ---------------------------------------------------------------- ① 대형 반응

    static void FormReact(GameOptions opt, Action<bool, string> check)
    {
        int max = (int)MathF.Round(Defs.Current.Command.FormReactMaxSec / Config.SimDt);
        check(max == 3 && max * Config.SimDt <= 0.05f + 1e-4f, $"대형 반응 최대 {max}틱 = {max * Config.SimDt * 1000:0}ms (≤ 50ms)");
        List<int> Delays(int seed)
        {
            var w = NewWorld(seed);
            var c = Center(w);
            var men = new List<Unit>();
            for (int i = 0; i < 24; i++) men.Add(w.SpawnUnit(Defs.Current.Units["infantry"], 1, c + new Vector2(i % 8, i / 8) * 0.9f));
            w.FormCompany(1, men);
            w.FlushCommands();
            w.Step();
            w.Move(1, men, c + new Vector2(0, 8));   // 중대 이동 명령 (방향 바꿔 대형) — 기본 출발 지연 대신 대형 반응
            w.FlushCommands();
            var d = men.Select(u => u.ReactDelay).ToList();
            // 직접 호출 — 여러 틱에서 범위 · 분산
            for (int t = 0; t < 30; t++) { w.Step(); foreach (var u in men) d.Add(w.FormReactTicks(u)); }
            return d;
        }
        var a = Delays(opt.Seed + 340);
        var b = Delays(opt.Seed + 340);
        var head = a.Take(24).ToList();
        check(a.All(x => x >= 0 && x <= max) && head.Distinct().Count() >= 3 && a.Distinct().Count() == max + 1,
              $"대형 반응 0~{max}틱 (중대 명령 직후 {string.Join(",", head.GroupBy(x => x).OrderBy(g => g.Key).Select(g => $"{g.Key}틱x{g.Count()}"))})");
        check(a.SequenceEqual(b), "대형 반응 결정적 (같은 시드 두 번 동일)");
    }

    // ---------------------------------------------------------------- ② 포 발사 속도

    static void ArtyRate(GameOptions opt, Action<bool, string> check)
    {
        var w = NewWorld(opt.Seed + 341);
        var c = Center(w);
        var guns = new List<Unit>();
        foreach (var k in new[] { "heavy_field_gun", "heavy_infantry_gun", "heavy_howitzer" })
            for (int i = 0; i < 4; i++) guns.Add(w.SpawnUnit(Defs.Current.Units[k], 1, c + new Vector2(-30 + i * 3, guns.Count % 4 * 2)));
        var cr = Defs.Current.CombatRules;
        var muls = guns.Select(g => w.ArtyRateMul(g)).ToList();
        check(guns.All(g => World.IsArtyGun(g, g.Weapon!)) && muls.All(m => m >= cr.ArtyRateMin && m <= cr.ArtyRateMax) && muls.Distinct().Count() == muls.Count,
              $"포 발사 간격 배수 {cr.ArtyRateMin}~{cr.ArtyRateMax}, 포마다 다름: {string.Join(" ", muls.Select(m => m.ToString("0.000")))}");
        var inf = w.SpawnUnit(Defs.Current.Units["infantry"], 1, c);
        var mg = w.SpawnUnit(Defs.Current.Units["heavy_machinegunner"], 1, c + new Vector2(0, 2));
        check(!World.IsArtyGun(inf, inf.Weapon!) && !World.IsArtyGun(mg, mg.Weapon!), "보병 · 기관총은 포 배수 없음");
        check(guns.Select(g => w.ArtyRateMul(g)).SequenceEqual(muls), "포 배수는 틱과 무관 (고정)");
        // 실제 사격: 야전포 4문이 각자 표적을 쏜다 — 평균 간격이 포마다 다르다 (일제 사격 아님)
        var w2 = NewWorld(opt.Seed + 342);
        var c2 = Center(w2);
        var fg = new List<Unit>(); var tg = new List<Unit>();
        for (int i = 0; i < 4; i++)
        {
            var g = w2.SpawnUnit(Defs.Current.Units["heavy_field_gun"], 1, c2 + new Vector2(-25, -6 + i * 4));
            var d = Dummy(w2, 2, c2 + new Vector2(10, -6 + i * 4));
            fg.Add(g); tg.Add(d);
            w2.Attack(1, new[] { g }, d);
        }
        var shots = fg.ToDictionary(g => g.Id, _ => new List<long>());
        for (int t = 0; t < 60 * 70; t++)
        {
            w2.Step();
            foreach (var s in w2.Shots) if (s.Tick == w2.Tick - 1 && shots.TryGetValue(s.ShooterId, out var l)) l.Add(s.Tick);
            foreach (var d in tg) d.Hp = 1000;
        }
        var means = fg.Select(g => { var l = shots[g.Id]; return l.Count >= 3 ? (l[^1] - l[0]) * Config.SimDt / (l.Count - 1) : 0f; }).ToList();
        bool lockstep = fg.Skip(1).All(g => shots[g.Id].SequenceEqual(shots[fg[0].Id]));
        check(means.All(m => m > 0) && !lockstep && means.Max() - means.Min() > 0.05f,
              $"야전포 4문 평균 간격 {string.Join(" / ", means.Select(m => m.ToString("0.00")))}초 (발 수 {string.Join(",", fg.Select(g => shots[g.Id].Count))}) — 일제 사격 아님");
    }

    // ---------------------------------------------------------------- ③ 첫 일제 사격

    /// <summary>보병 36명 횡대 (사수) 앞에 표적 줄을 갑자기 세우고 병사별 첫 사격 틱의 폭 (초).</summary>
    static float VolleySpread(int seed, out int fired)
    {
        var w = NewWorld(seed);
        var c = Center(w);
        var men = new List<Unit>();
        for (int i = 0; i < 36; i++) men.Add(w.SpawnUnit(Defs.Current.Units["infantry"], 1, c + new Vector2(-16 + i * 0.9f, 8)));
        w.FormCompany(1, men);
        w.FlushCommands();
        for (int i = 0; i < 120; i++) w.Step();
        var dummies = new List<Unit>();
        for (int i = 0; i < 12; i++) dummies.Add(Dummy(w, 2, c + new Vector2(-14 + i * 2.6f, -6)));   // 14칸 앞 (반응 거리 안)
        var first = new Dictionary<int, long>();
        for (int t = 0; t < 60 * 6; t++)
        {
            w.Step();
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.Faction == 1 && !first.ContainsKey(s.ShooterId)) first[s.ShooterId] = s.Tick;
            foreach (var d in dummies) d.Hp = 1000;
        }
        fired = first.Count;
        return first.Count < 2 ? 0f : (first.Values.Max() - first.Values.Min()) * Config.SimDt;
    }

    static void FirstVolley(GameOptions opt, Action<bool, string> check)
    {
        var cr = Defs.Current.CombatRules;
        float savedMax = cr.FirstShotMax;
        cr.FirstShotMax = 0;
        float before;
        int firedBefore;
        try { before = VolleySpread(opt.Seed + 343, out firedBefore); }
        finally { cr.FirstShotMax = savedMax; }
        float after = VolleySpread(opt.Seed + 343, out int firedAfter);
        float again = VolleySpread(opt.Seed + 343, out _);
        check(firedAfter >= 30 && after >= 0.8f && after == again,
              $"첫 일제 사격 폭 {after:0.00}초 ({firedAfter}명, 반응 없을 때 {before:0.00}초 · {firedBefore}명) ≥ 0.8초, 결정적");
        var w = NewWorld(opt.Seed + 344);
        var u = w.SpawnUnit(Defs.Current.Units["infantry"], 1, Center(w));
        int recruit = 0, vet = 0;
        for (int t = 0; t < 120; t++) { w.Step(); u.GrantKills(0); recruit += w.FirstShotTicks(u, u.Weapon!); u.GrantKills(cr.RankKills[cr.CloseVeteranRank - 1]); vet += w.FirstShotTicks(u, u.Weapon!); }
        var all = new List<int>();
        u.GrantKills(0);
        for (int t = 0; t < 200; t++) { w.Step(); all.Add(w.FirstShotTicks(u, u.Weapon!)); }
        int lo = (int)MathF.Round(cr.FirstShotMin / Config.SimDt), hi = (int)MathF.Round(cr.FirstShotMax / Config.SimDt);
        check(all.All(x => x >= lo && x <= hi) && vet < recruit, $"첫 사격 반응 신병 {lo}~{hi}틱 (평균 신병 {recruit / 120f:0} · 노병 {vet / 120f:0}틱 — 노병이 빠름)");
        u.LastShotTick = w.Tick - 10;
        check(w.FirstShotTicks(u, u.Weapon!) == 0, "계속 쏘는 중 (5초 안 사격) 이면 첫 사격 반응 없음 — 이후 간격은 무기 난수");
    }
}
