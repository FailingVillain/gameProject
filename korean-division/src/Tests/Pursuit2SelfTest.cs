using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// pursuit2 (2026-10-19 사용자 결정): ① 후퇴 사격 (걸으며 쏨 — 멈춤 · 돌아섬 · 돌격 없음) ② 후퇴 뒤 재정비 (조건 없이 곧바로 재전진하지 않음)
/// ③ 포병 (포탄 착탄 폭발) 만 아군 오사 — 소총 · 기관총 · 수류탄 · 근접은 같은 팀 제외 ④ ai_kr_vs_ft_1000 (+ tools 복제본) 시작 배치 겹침 없음. KD_SELFTEST_ONLY=pursuit2.
/// </summary>
public static class Pursuit2SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[pursuit2 후퇴 사격 · 재정비 · 포병 오사 · 배치 겹침]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestRetreatFire();
            TestRegroup();
            TestArtilleryFriendlyFire();
            TestOtherWeaponsNoFriendlyFire();
            TestPlayerGroundFireHurtsOwn();
            TestCrewDangerClose();
            TestDeploymentNoOverlap();
        }
        catch (Exception e) { check(false, $"pursuit2 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World Bare(int size = 120)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static List<Unit> Spawn(World w, int n, Vector2 c, int faction, string def = "infantry", int cols = 6, float gap = 1.1f)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++) list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * gap, (i / cols) * gap)));
        return list;
    }

    static int Ticks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));

    static TacFaction? St(World w, int f) => w.Controllers.Select(c => c.Ai).OfType<TacticalAi>().Select(a => a.StateOf(f)).FirstOrDefault(x => x != null);

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        var p = defs.Tactics.Pursuit; var r = defs.Tactics.Regroup; var c = defs.Tactics.Cavalry;
        check(p.RetreatFire && p.RetreatFireAccMul > 0 && p.RetreatFireAccMul < defs.CombatRules.MarchAccMul && p.RetreatFireIntervalMul > 1 && p.RetreatFireRangeFrac is > 0 and <= 1,
            $"tactics.json 후퇴 사격: retreatFire {p.RetreatFire} · 명중 ×{p.RetreatFireAccMul} (행진 ×{defs.CombatRules.MarchAccMul} 보다 나쁨) · 간격 ×{p.RetreatFireIntervalMul} · 거리 ×{p.RetreatFireRangeFrac}");
        check(r.Enabled && r.MinSec > 0 && r.CheckSec > 0 && r.RejoinStrength is > 0 and < 1 && r.RejoinSupp is > 0 and < 1,
            $"tactics.json 재정비: enabled {r.Enabled} · minSec {r.MinSec} · checkSec {r.CheckSec} · rejoinStrength {r.RejoinStrength} · rejoinSupp {r.RejoinSupp} · rejoinOdds {r.RejoinOdds}");
        check(c.FlankOddsMul > 0.5f && c.FlankMinSupp > 0, $"tactics.json 기병 측면 조금 엄격히: flankOddsMul {c.FlankOddsMul} (N22 0.5) · flankMinSupp {c.FlankMinSupp}");
        check(defs.CombatRules.ArtilleryFriendlyFire && Snapshot.FormatVersion >= 31, $"balance.json artilleryFriendlyFire {defs.CombatRules.ArtilleryFriendlyFire}, Snapshot v{Snapshot.FormatVersion} (재정비 상태)");
    }

    // ---------------------------------------------------------------- ① 후퇴 사격

    static (int Shots, int Charges, int Stops, int Targets, float Moved, long Kills) RetreatRun(bool on)
    {
        var P = defs.Tactics.Pursuit;
        bool old = P.RetreatFire;
        P.RetreatFire = on;
        try
        {
            var w = Bare();
            var a = Spawn(w, 12, new Vector2(60, 60), 1, cols: 12);
            var b = Spawn(w, 12, new Vector2(60, 50), 2, cols: 12);
            w.Hold(2, b);
            w.Step();
            w.Retreat(1, a, new Vector2(60, 110));
            w.Step();
            var ids = a.Select(u => u.Id).ToHashSet();
            float y0 = a.Average(u => u.Pos.Y);
            int shots = 0, charges = 0, stops = 0, targets = 0;
            for (int i = 0; i < Ticks(10); i++)
            {
                w.Step();
                foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && ids.Contains(s.ShooterId)) shots++;
                foreach (var u in a)
                {
                    if (!u.Alive || !u.Retreating) continue;
                    if (u.Charging) charges++;
                    if (u.Order != UnitOrder.Move) stops++;
                    if (u.Target != null) targets++;
                }
            }
            var alive = a.Where(u => u.Alive).ToList();
            float moved = alive.Count > 0 ? alive.Average(u => u.Pos.Y) - y0 : 0;
            return (shots, charges, stops, targets, moved, w.Stats.KillsRetreatFire);
        }
        finally { P.RetreatFire = old; }
    }

    static void TestRetreatFire()
    {
        var on = RetreatRun(true);
        var off = RetreatRun(false);
        check(on.Shots > 0 && off.Shots == 0, $"후퇴 사격: 후퇴 중 사격 {on.Shots}발 (사살 {on.Kills}), 끄면 {off.Shots}발");
        check(on.Charges == 0 && on.Stops == 0 && on.Targets == 0, $"후퇴 사격 중 돌격 {on.Charges} · 이동 명령을 잃음 {on.Stops} · 교전 표적 (멈춰 쏨) {on.Targets} (기-틱, 모두 0)");
        check(on.Moved >= off.Moved - 1.5f && on.Moved > 6f, $"후퇴 사격이 후퇴를 늦추지 않음: 10초 이동 {on.Moved:0.0}칸 (사격 없음 {off.Moved:0.0}칸)");
        // 명중 배수
        var w = Bare();
        var s1 = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 40));
        var t1 = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(50, 40));
        var wp = s1.Weapon!;
        float p0 = w.HitChance(s1, t1, wp, 8f);
        s1.RetreatShot = true;
        float p1 = w.HitChance(s1, t1, wp, 8f);
        s1.RetreatShot = false;
        check(p0 > 0 && MathF.Abs(p1 / p0 - defs.Tactics.Pursuit.RetreatFireAccMul) < 0.01f, $"후퇴 사격 명중 = 보통 × {defs.Tactics.Pursuit.RetreatFireAccMul} ({p0:0.000} → {p1:0.000})");
    }

    // ---------------------------------------------------------------- ② 후퇴 뒤 재정비

    static (int Readv, int Holds, float AdvMin, float AdvLate, bool Started, string Why, float AdvAfterFree) RegroupRun(bool enabled)
    {
        var R = defs.Tactics.Regroup;
        bool old = R.Enabled;
        R.Enabled = enabled;
        try
        {
            var w = Bare(160);
            var a = Spawn(w, 40, new Vector2(80, 130), 1, cols: 20);
            a.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(80, 134)));
            var b = Spawn(w, 6, new Vector2(80, 92), 2);
            w.Controllers.Add((1, new TacticalAi()));
            w.Hold(2, b);
            TacGroup? g = null;
            for (int i = 0; i < Ticks(8) && g == null; i++)
            {
                w.Step();
                if (St(w, 1) is { Phase: 2 } s) g = s.Groups.FirstOrDefault(x => x.Kind == TacKind.Line && x.CompanyId > 0 && x.Task == TacTask.Attack);
            }
            if (g == null) return (0, 0, 0, 0, false, "공격 과제 소총 중대 없음", 0);
            // 스스로 후퇴가 막 끝난 것처럼 (후퇴 15초 → 끝) — 같은 적 (북쪽 38칸) 이 그대로 있다
            g.State = TacState.Fallback; g.FallbackUntil = w.Tick + 1;
            var st = St(w, 1)!;
            int r0 = st.Readvances;
            float Y() { var m = a.Where(u => u.Alive && !u.IsOfficer).ToList(); return m.Average(u => u.Pos.Y); }
            void Pin(float r) { foreach (var u in a) if (u.Alive) u.Suppression = (short)(defs.CombatRules.SuppressMax * r); }
            float y0 = Y(), advMin = 0, advLate = 0;
            int readvMin = 0;
            for (int i = 0; i < Ticks(R.MinSec + 15); i++)   // 제압 0.25 (> rejoinSupp) 유지: 최소 시간 뒤에도 재전진 안 함
            {
                Pin(0.25f);
                w.Step();
                float adv = y0 - Y();
                if (i < Ticks(R.MinSec)) { advMin = MathF.Max(advMin, adv); readvMin = st.Readvances - r0; } else advLate = MathF.Max(advLate, adv);
            }
            int readv = st.Readvances - r0, holds = st.RegroupHolds;
            if (!enabled) return (readvMin, holds, advMin, advLate, true, "", 0);
            // 제압이 풀리면 (병력 · 국지 병력비 충분) 재전진
            float y1 = Y();
            for (int i = 0; i < Ticks(30); i++) w.Step();
            return (readv, holds, advMin, advLate, true, "", y1 - Y());
        }
        finally { R.Enabled = old; }
    }

    static void TestRegroup()
    {
        var n = RegroupRun(true);
        check(n.Started, $"재정비 시험 준비: {(n.Started ? "공격 과제 소총 중대" : n.Why)}");
        if (!n.Started) return;
        check(n.Readv == 0 && n.AdvMin < 3f && n.AdvLate < 3f, $"후퇴 뒤 재정비: 최소 {defs.Tactics.Regroup.MinSec}초 + 조건 미달 (제압 0.25) 15초 동안 재전진 {n.Readv}회, 적 쪽 전진 최대 {n.AdvMin:0.0} / {n.AdvLate:0.0}칸 (3칸 미만)");
        check(n.Holds > 0, $"조건 미달 → 사수 (지원 사격) 판단 {n.Holds}회");
        check(n.AdvAfterFree > 4f, $"제압이 풀리고 병력 · 병력비가 충분하면 재전진: 30초 동안 적 쪽 {n.AdvAfterFree:0.0}칸");
        var o = RegroupRun(false);
        check(o.Readv >= 1 && o.AdvMin > 4f, $"대조 (regroup 끔 = 옛 동작): 곧바로 재전진 {o.Readv}회, 최소 시간 안 적 쪽 {o.AdvMin:0.0}칸");
    }

    // ---------------------------------------------------------------- ③ 포병만 아군 오사

    static void TestArtilleryFriendlyFire()
    {
        foreach (string gunKey in new[] { "heavy_howitzer", "heavy_field_gun", "heavy_infantry_gun" })
        {
            var w = Bare();
            var gun = w.SpawnUnit(defs.Units[gunKey], 1, new Vector2(30, 60));
            var wp = gun.Weapon!;
            var friends = Spawn(w, 6, new Vector2(70, 60), 1, cols: 3, gap: 0.8f);
            var foes = Spawn(w, 3, new Vector2(70, 62), 2, cols: 3, gap: 0.8f);
            var neutral = w.SpawnUnit(defs.Units["infantry"], 0, new Vector2(71, 60.5f));
            float hp0 = friends.Sum(u => u.Hp), nhp = neutral.Hp;
            w.Explode(gun.Id, 1, gun, gun.Pos, new Vector2(70, 60.5f), 0, wp);
            float dmg = hp0 - friends.Sum(u => u.Hp);
            bool supp = friends.Any(u => u.Suppression > 0);
            bool counted = w.Stats.FriendlyFire.TryGetValue(wp.Key, out var ff) && ff.Hits > 0;
            check(dmg > 0 && supp && counted && neutral.Hp == nhp, $"{gunKey} 착탄: 아군 피해 {dmg:0} · 제압 {supp} · 아군 오사 계수 {wp.Key} 명중 {ff?.Hits ?? 0} 사살 {ff?.Kills ?? 0}, 중립 피해 없음 {neutral.Hp == nhp}");
            // 쏜 포 자신은 맞지 않음, 끄면 옛 동작
            var w2 = Bare();
            var g2 = w2.SpawnUnit(defs.Units[gunKey], 1, new Vector2(60, 60));
            var f2 = Spawn(w2, 4, new Vector2(60, 60.7f), 1, cols: 4, gap: 0.8f);
            float ghp = g2.Hp, fhp = f2.Sum(u => u.Hp);
            bool old = defs.CombatRules.ArtilleryFriendlyFire;
            defs.CombatRules.ArtilleryFriendlyFire = false;
            try { w2.Explode(g2.Id, 1, g2, g2.Pos, g2.Pos, 0, wp); } finally { defs.CombatRules.ArtilleryFriendlyFire = old; }
            bool offOk = f2.Sum(u => u.Hp) == fhp && w2.Stats.FriendlyFire.Count == 0;
            w2.Explode(g2.Id, 1, g2, g2.Pos, g2.Pos, 0, wp);
            check(g2.Hp == ghp && offOk && f2.Sum(u => u.Hp) < fhp, $"{gunKey}: 쏜 포 자신은 맞지 않음 ({g2.Hp == ghp}), artilleryFriendlyFire false = 아군 피해 없음 ({offOk}), 켜면 옆 아군 피해");
        }
        // 아군 사살은 계급 · 사살 수에 넣지 않음
        var w3 = Bare();
        var g3 = w3.SpawnUnit(defs.Units["heavy_howitzer"], 1, new Vector2(30, 60));
        var victim = w3.SpawnUnit(defs.Units["infantry"], 1, new Vector2(70, 60));
        victim.Hp = 1;
        long kf = w3.Stats.KillsFire;
        w3.Explode(g3.Id, 1, g3, g3.Pos, victim.Pos, 0, g3.Weapon!);
        check(!victim.Alive && g3.Kills == 0 && w3.Stats.KillsFire == kf && w3.Stats.FriendlyFire[g3.Weapon!.Key].Kills == 1, $"아군 포탄 사살: 포 사살 수 {g3.Kills} (0), 적 사살 계수 그대로, 아군 오사 사살 1");
    }

    static void TestOtherWeaponsNoFriendlyFire()
    {
        // 수류탄 폭발 지점에 아군 · 적 → 적만
        var w = Bare();
        var thrower = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(50, 60));
        var fr = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60, 60));
        var fo = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60.6f, 60));
        w.Hold(1, new[] { thrower, fr }); w.Hold(2, new[] { fo });
        float fhp = fr.Hp, ohp = fo.Hp;
        w.Blasts.Add(new GrenadeBlast { ThrowerId = thrower.Id, Owner = 1, Pos = new Vector2(60.3f, 60), ExplodeTick = w.Tick + 1 });
        w.Step(); w.Step();
        check(fr.Hp == fhp && fo.Hp < ohp, $"수류탄: 아군 피해 없음 ({fr.Hp == fhp}), 적 피해 {ohp - fo.Hp:0}");
        // 소총 · 경기관총 · 근접 (돌격) 혼전: 아군 사이로 쏘고 찔러도 아군 오사 0
        var w2 = Bare();
        var A = Spawn(w2, 10, new Vector2(60, 64), 1, cols: 10);
        A.AddRange(Spawn(w2, 3, new Vector2(60, 66), 1, "heavy_machinegunner", cols: 3));
        A.Add(w2.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 67)));
        var front = Spawn(w2, 10, new Vector2(60, 62.8f), 1, cols: 10);   // 사선 위 아군
        var B = Spawn(w2, 12, new Vector2(60, 58), 2, cols: 12);
        w2.Step();
        w2.Charge(1, front, new Vector2(60, 57));
        int shots = 0;
        for (int i = 0; i < Ticks(15); i++) { w2.Step(); shots += w2.Shots.Count(s => s.Tick == w2.Tick - 1 && s.Faction == 1); }
        bool anyKill = B.Any(u => !u.Alive) || B.Sum(u => u.Hp) < B.Count * defs.Units["infantry"].Hp;
        check(w2.Stats.FriendlyFire.Count == 0 && shots > 0 && anyKill, $"소총 · 경기관총 · 근접 혼전 15초: 아군 오사 {w2.Stats.FriendlyFire.Count}종 (0), 사격 {shots}발, 적 피해 있음 {anyKill}");
    }

    static void TestPlayerGroundFireHurtsOwn()
    {
        var w = Bare();
        var gun = w.SpawnUnit(defs.Units["heavy_infantry_gun"], 1, new Vector2(40, 60));
        var own = Spawn(w, 12, new Vector2(65, 60), 1, cols: 4, gap: 0.8f);
        w.Hold(1, own);
        w.Step();
        w.GroundFire(1, new[] { gun }, new Vector2(65, 60.8f));
        float hp0 = own.Sum(u => u.Hp);
        for (int i = 0; i < Ticks(40) && own.Sum(u => u.Hp) >= hp0; i++) w.Step();
        bool hurt = own.Sum(u => u.Hp) < hp0;
        check(hurt && w.Stats.FriendlyFire.ContainsKey(gun.Weapon!.Key), $"플레이어 지점 포격을 아군 위에: 아군 피해 {hp0 - own.Sum(u => u.Hp):0} · 계수 {string.Join(",", w.Stats.FriendlyFire.Keys)}");
    }

    static void TestCrewDangerClose()
    {
        (int Shots, long Holds) Run(bool friendNear, bool attack)
        {
            var w = Bare();
            var gun = w.SpawnUnit(defs.Units["heavy_field_gun"], 1, new Vector2(48, 60));
            var foe = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 60));
            foe.Hp = 99999;
            w.Hold(2, new[] { foe });
            if (friendNear) { var fr = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(61.5f, 60)); fr.Hp = 99999; w.Hold(1, new[] { fr }); }
            w.Step();
            if (attack) w.Attack(1, new[] { gun }, foe);
            int shots = 0;
            for (int i = 0; i < Ticks(40); i++) { w.Step(); shots += w.Shots.Count(sh => sh.Tick == w.Tick - 1 && sh.ShooterId == gun.Id); }
            return (shots, w.Stats.CrewHoldFire);
        }
        var clear = Run(false, false); var near = Run(true, false); var cmd = Run(true, true);
        check(clear.Shots > 0 && near.Shots == 0 && near.Holds > 0 && cmd.Shots > 0,
            $"포수 규율: 아군 없는 자동 표적 사격 {clear.Shots}발, 표적 옆 아군 = 자동 사격 {near.Shots}발 (보류 판단 {near.Holds}), 명시 공격 명령 = {cmd.Shots}발");
    }

    // ---------------------------------------------------------------- ④ 미션 시작 배치 겹침 없음

    static void TestDeploymentNoOverlap()
    {
        var files = new List<string> { MissionDef.PathOf("ai_kr_vs_ft_1000") };
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var tools = Path.Combine(dir.FullName, "tools");
            if (Directory.Exists(tools) && File.Exists(Path.Combine(dir.FullName, "KoreanDivision.csproj")))
            { files.AddRange(Directory.GetFiles(tools, "ai_kr_vs_ft_*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)); break; }
        }
        int bad = 0; var report = new List<string>();
        foreach (var f in files)
        {
            var def = MissionDef.Parse(File.ReadAllText(f));
            var w = World.CreateMissionWorld(def, defs, 1, new List<DeployEntry>());
            var boxes = new List<(string Id, int Fac, float X0, float X1, float Y0, float Y1)>();
            foreach (var fo in def.Forces)
            {
                var at = fo.At is { Length: 2 } p ? new Vector2(p[0], p[1]) : Vector2.Zero;
                var pos = w.LayoutForce(fo, at, MissionDef.DirOf(fo.Facing)).Select(x => x.Pos).ToList();
                if (pos.Count == 0) continue;
                boxes.Add((fo.Id, fo.Faction, pos.Min(q => q.X), pos.Max(q => q.X), pos.Min(q => q.Y), pos.Max(q => q.Y)));
            }
            int over = 0; string first = "";
            for (int i = 0; i < boxes.Count; i++)
            for (int k = i + 1; k < boxes.Count; k++)
            {
                var (a, b) = (boxes[i], boxes[k]);
                if (a.X0 < b.X1 - 0.3f && b.X0 < a.X1 - 0.3f && a.Y0 < b.Y1 - 0.3f && b.Y0 < a.Y1 - 0.3f) { over++; if (first == "") first = $"{a.Id}/{b.Id}"; }
            }
            var bn = boxes.Where(x => x.Id.Contains("_bn")).ToList();
            float wmax = bn.Count > 0 ? bn.Max(x => x.X1 - x.X0) : 0;
            if (over > 0) bad++;
            report.Add($"{Path.GetFileNameWithoutExtension(f)} 겹침 {over}{(first != "" ? " (" + first + ")" : "")} 대대 폭 {wmax:0}");
        }
        check(bad == 0 && files.Count >= 2, $"ai_kr_vs_ft 시작 배치 병력 상자 겹침 없음 ({files.Count}개 파일): {string.Join("; ", report)}");
    }
}
