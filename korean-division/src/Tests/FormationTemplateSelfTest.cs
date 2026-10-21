using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>
/// 전투 대형 (분대 · 기병 사선 · 포병 대형 · 대대 파상) · 역할군 자동 배정 · 중대 전술 틀 정지 배치 · 저장 · 결정성 검증
/// (docs/TACTICAL_AI_STAGE3_PLAN.md '전투 대형 결정' · '전술 대형 (역할군) 결정' 1단계). 창 없이. KD_SELFTEST_ONLY=formations 로 단독 실행.
/// </summary>
public static class FormationTemplateSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("formations", 1, false, "standard");
    static readonly Vector2 North = new(0, -1);

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[전투 대형 · 역할군 · 중대 전술 틀]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestDataLoaded();
            TestSquadShapes();
            TestCompanyShapesUnchanged();
            TestEchelon();
            TestCycles();
            TestRoleAutoAssign();
            TestRoleReassign();
            TestTemplatePlacement();
            TestArtilleryFormation();
            TestArtilleryDeploy();
            TestWaves();
            TestSquadAloneMove();
            TestSaveAndDeterminism();
            TestAiTemplates();
        }
        catch (Exception e) { check(false, $"전투 대형 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World Bare(int size = 140)
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

    static List<Unit> Spawn(World w, string def, int n, Vector2 c, int cols = 6, int faction = 1)
    {
        var list = new List<Unit>();
        for (int i = 0; i < n; i++) list.Add(w.SpawnUnit(defs.Units[def], faction, c + new Vector2((i % cols - (cols - 1) / 2f) * 1.1f, (i / cols) * 1.1f)));
        return list;
    }

    /// <summary>소총 36 (분대 3) + 경기관총 4 + 장교 1 = 중대 하나 (분대 4개: 소총수 · 기관총 섞임).</summary>
    static (World W, Company C) Rifles(int rifles = 36, int lmgs = 4, Vector2? at = null)
    {
        var w = Bare();
        var c0 = at ?? new Vector2(70, 100);
        var us = Spawn(w, "infantry", rifles, c0, 12);
        us.AddRange(Spawn(w, "heavy_machinegunner", lmgs, c0 + new Vector2(0, 4), 4));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, c0 + new Vector2(0, 6)));
        w.FormCompany(1, us);
        w.Step();
        return (w, us[0].Company!);
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    static float MinDist(Vector2[] p)
    {
        float m = float.MaxValue;
        for (int i = 0; i < p.Length; i++) for (int j = i + 1; j < p.Length; j++) m = MathF.Min(m, Vector2.Distance(p[i], p[j]));
        return m;
    }

    static Vector2 Mean(IEnumerable<Vector2> ps) { Vector2 s = Vector2.Zero; int n = 0; foreach (var p in ps) { s += p; n++; } return n > 0 ? s / n : s; }

    /// <summary>이동 명령 후 중대 최종 오프셋 (로컬: X = 오른쪽, Y = 앞) — 북쪽 진행이면 (off.X, −off.Y).</summary>
    static Dictionary<int, Vector2> FinalLocal(Company c) => c.Anchor!.FinalOff.ToDictionary(kv => kv.Key, kv => new Vector2(kv.Value.X, -kv.Value.Y));

    // ---------------------------------------------------------------- 자료 · 모양

    static void TestDataLoaded()
    {
        var fr = defs.Formations; var tr = defs.Tactics;
        bool ok = fr.Squad.Count >= 7 && fr.Company.Count >= 7 && fr.Squad["wedge"].Shape == "wedge" && fr.Squad["echelon_left"].CavalryOnly
                  && tr.Templates.Count >= 6 && tr.Template(CompanyTactic.FlankRight, out bool mir) != null && mir && tr.Roles.ByWeapon["lmg"] == "fire";
        check(ok, $"formations.json · tactics.json 읽음 (분대 모양 {fr.Squad.Count}, 중대 모양 {fr.Company.Count}, 틀 {tr.Templates.Count}, 우측 우회 = 좌측 대칭)");
    }

    static void TestSquadShapes()
    {
        var fr = defs.Formations;
        const float s = 1f;
        const int n = 10;
        Vector2[] S(FormType t) => FormationRules.SlotsFor(fr.SquadShape(t), t, n, s, 0);
        var close = S(FormType.Box); var open = S(FormType.Spread); var col = S(FormType.Column); var line = S(FormType.Line); var wedge = S(FormType.Wedge);
        bool means = new[] { close, open, col, line, wedge }.All(p => p.Length == n && Mean(p).Length() < 1e-3f);
        bool closeOk = MathF.Abs(MinDist(close) - s * fr.Squad["close"].Spacing) < 1e-3f;
        bool openOk = MathF.Abs(MinDist(open) - s * fr.Squad["open"].Spacing) < 1e-3f && open.All(p => MathF.Abs(p.Y - open[0].Y) < 1e-4f);   // 산개 = 넓은 한 줄
        bool colOk = col.Select(p => MathF.Round(p.X, 3)).Distinct().Count() == 2 && col.Select(p => MathF.Round(p.Y, 3)).Distinct().Count() == 5;   // 2열 종대
        bool lineOk = line.All(p => MathF.Abs(p.Y - line[0].Y) < 1e-4f) && line.Max(p => p.X) - line.Min(p => p.X) > (n - 1) * s;   // 한 줄 횡대
        float front = wedge.Max(p => p.Y);
        bool wedgeOk = wedge.Count(p => MathF.Abs(p.Y - front) < 1e-4f) == 1;   // 쐐기 = 앞 꼭짓점 한 명
        check(means && closeOk && openOk && colOk && lineOk && wedgeOk, $"분대 대형 모양 (10명): 평균 원점 {means}, 밀집 간격 {closeOk}, 산개 넓은 한 줄 {openOk}, 2열 종대 {colOk}, 한 줄 횡대 {lineOk}, 쐐기 꼭짓점 {wedgeOk}");
    }

    static void TestCompanyShapesUnchanged()
    {
        // 중대 모양은 옛 CompanySlots 식과 같아야 (결정성 골든 유지): 횡대 2열 · 종대 4열 · 정사각 · 산개 ×2.2
        const float s = 0.95f;
        bool ok = true;
        foreach (int n in new[] { 7, 24, 41 })
        {
            var line = OrgLayout.CompanySlots(FormType.Line, n, s, 0);
            var col = OrgLayout.CompanySlots(FormType.Column, n, s, 0);
            var spr = OrgLayout.CompanySlots(FormType.Spread, n, s, 0);
            ok &= line.Select(p => MathF.Round(p.Y, 3)).Distinct().Count() == 2;
            ok &= col.Select(p => MathF.Round(p.Y, 3)).Distinct().Count() == (n + 3) / 4;
            ok &= MathF.Abs(MinDist(spr) - s * Formations.SpreadFactor) < 1e-3f;
        }
        check(ok, "중대 대형 (틀 없음) 은 옛 모양 그대로: 횡대 2열 · 종대 4열 · 산개 ×2.2");
    }

    static void TestEchelon()
    {
        var fr = defs.Formations;
        var r = FormationRules.SlotsFor(fr.SquadShape(FormType.EchelonRight), FormType.EchelonRight, 6, 1f, 0);
        var l = FormationRules.SlotsFor(fr.SquadShape(FormType.EchelonLeft), FormType.EchelonLeft, 6, 1f, 0);
        var rs = r.OrderBy(p => p.X).ToArray(); var ls = l.OrderBy(p => p.X).ToArray();
        bool shape = Enumerable.Range(1, 5).All(i => rs[i].Y < rs[i - 1].Y) && Enumerable.Range(1, 5).All(i => ls[i].Y > ls[i - 1].Y);
        check(shape, "사선: 우사선 = 오른쪽으로 갈수록 뒤, 좌사선 = 왼쪽으로 갈수록 뒤");

        // 기병만: 보병 중대 · 분대에 사선 명령 → 거부, 기병 중대 → 적용
        var w = Bare();
        var inf = Spawn(w, "infantry", 12, new Vector2(40, 60));
        var cav = Spawn(w, "cavalry_default", 12, new Vector2(90, 60));
        w.FormCompany(1, inf); w.FormCompany(1, cav); w.Step();
        Company ci = inf[0].Company!, cc = cav[0].Company!;
        w.Notices.Clear();
        w.SetCompanyForm(1, new[] { ci }, FormType.EchelonLeft);
        w.SetCompanyForm(1, new[] { cc }, FormType.EchelonRight);
        w.SetSquadForm(1, ci.Squads, FormType.EchelonLeft);
        w.SetSquadForm(1, cc.Squads, FormType.EchelonLeft);
        w.Step();
        bool rule = ci.Form == FormType.Line && cc.Form == FormType.EchelonRight && ci.Squads.All(q => q.Form == FormType.Free) && cc.Squads.All(q => q.Form == FormType.EchelonLeft)
                    && w.Notices.Count(n => n.Result == CommandResult.NotAllowed) >= 2;
        check(rule, $"사선은 기병만: 보병 중대 {Hud.FormName(ci.Form)} (거부) · 기병 중대 {Hud.FormName(cc.Form)}, 보병 분대 거부 · 기병 분대 적용, 거부 알림 {w.Notices.Count}");

        // 중대 밖 보병 묶음에 사선 진형 = 횡대로 대신
        var loose = Spawn(w, "infantry", 6, new Vector2(40, 100));
        var spec = w.LooseForm(loose, new FormSpec((int)FormType.EchelonLeft, 0, North), out var loc);
        var spec2 = w.LooseForm(cav.Take(0).Concat(Spawn(w, "cavalry_default", 4, new Vector2(100, 100))).ToList(), new FormSpec((int)FormType.EchelonLeft, 0, North), out var loc2);
        check(spec!.Kind == FormType.Line && loc == null && spec2!.Kind == FormType.EchelonLeft && loc2 != null, "중대 밖 묶음: 보병 사선 → 횡대, 기병 사선 → 사선 슬롯");

        // 기병 중대 사선 이동 → 도착 슬롯이 사선
        w.Move(1, cc.Members, new Vector2(90, 30), new FormSpec(0, 0, North));
        w.Step();
        var fl = FinalLocal(cc).Values.OrderBy(p => p.X).ToList();
        float corr = 0; var mx = Mean(fl);
        foreach (var p in fl) corr += (p.X - mx.X) * (p.Y - mx.Y);
        check(corr < 0, $"기병 중대 우사선 이동: 도착 슬롯이 오른쪽으로 갈수록 뒤 (공분산 {corr:0.0})");
    }

    static void TestCycles()
    {
        bool a = FormCycle.NextCompanyType(FormType.Spread) == FormType.Line && FormCycle.NextCompanyType(FormType.Spread, true) == FormType.EchelonLeft
                 && FormCycle.NextCompanyType(FormType.EchelonLeft, true) == FormType.EchelonRight && FormCycle.NextCompanyType(FormType.EchelonRight, true) == FormType.Line;
        bool b = FormCycle.NextSquadForm(FormType.Free) == FormType.Box && FormCycle.NextSquadForm(FormType.Wedge) == FormType.Free && FormCycle.NextSquadForm(FormType.Wedge, true) == FormType.EchelonLeft;
        bool c = FormCycle.NextTactic(CompanyTactic.Assault) == CompanyTactic.None && FormCycle.NextBatForm(BatForm.TwoFrontReserve) == BatForm.Waves && FormCycle.NextBatForm(BatForm.Waves) == BatForm.Line
                 && FormCycle.NextRole(UnitRole.Command) == UnitRole.None && FormCycle.NextType(FormType.Spread) == FormType.Free;
        check(a && b && c, $"순환: 중대 (기병 사선) {a}, 분대 {b}, 전술 · 대대 파상 · 역할 {c}");
    }

    // ---------------------------------------------------------------- 역할군

    static void TestRoleAutoAssign()
    {
        var (w, c) = Rifles();
        var roles = c.Members.GroupBy(u => u.Role).ToDictionary(g => g.Key, g => g.Count());
        int lmg = c.Members.Count(u => u.Def.Weapon == "lmg");
        bool lmgFire = c.Members.Where(u => u.Def.Weapon == "lmg").All(u => u.Role == UnitRole.Fire);
        bool officer = c.Members.Where(u => u.IsOfficer).All(u => u.Role == UnitRole.Command);
        int loaders = c.Members.Count(u => u.Def.Weapon == "rifle_mosin" && u.Role == UnitRole.Fire);
        // 탄약수는 기관총과 같은 분대
        bool loaderSquad = c.Members.Where(u => u.Def.Weapon == "rifle_mosin" && u.Role == UnitRole.Fire).All(u => u.Squad!.Members.Any(m => m.Def.Weapon == "lmg"));
        int man = roles.GetValueOrDefault(UnitRole.Manoeuvre), rif = roles.GetValueOrDefault(UnitRole.Rifle);
        bool split = man > 0 && rif > 0 && c.Members.Where(u => u.Role == UnitRole.Manoeuvre).Select(u => u.Squad!.Number).Min() > c.Members.Where(u => u.Role == UnitRole.Rifle).Select(u => u.Squad!.Number).Min();
        check(lmgFire && officer && loaders == lmg * defs.Tactics.Roles.LoadersPerGun && loaderSquad && split && !c.Members.Any(u => u.Role == UnitRole.None),
            $"역할 자동 배정: 기관총 {lmg} → 화력조, 탄약수 {loaders} (같은 분대 {loaderSquad}), 장교 → 지휘, 사격조 {rif} · 기동조 {man} (뒤쪽 분대), 분대 {c.Squads.Count}");
        // 돌격보병 · 대전차 · 저격 = 무기 규칙
        var w2 = Bare();
        var us = Spawn(w2, "infantry_assault", 4, new Vector2(50, 50)).Concat(Spawn(w2, "heavy_at_rifleman", 2, new Vector2(50, 56))).Concat(Spawn(w2, "infantry_sniper", 2, new Vector2(56, 56))).ToList();
        w2.FormCompany(1, us); w2.Step();
        bool w2ok = us.Where(u => u.Def.Weapon == "carbine_assault").All(u => u.Role == UnitRole.Manoeuvre) && us.Where(u => u.Def.Weapon is "at_rifle" or "sniper_rifle").All(u => u.Role == UnitRole.Support);
        check(w2ok, "역할 무기 규칙: 돌격보병 → 기동조, 대전차 · 저격 → 지원");
    }

    static void TestRoleReassign()
    {
        var (w, c) = Rifles();
        var q = c.Squads[0];
        var before = q.Members.ToDictionary(u => u.Id, u => u.Role);
        w.SetUnitRole(1, q.Members, UnitRole.Support);
        w.Step();
        bool set = q.Members.All(u => u.Role == UnitRole.Support) && Hud.SquadRole(q) == UnitRole.Support;
        w.SetUnitRole(1, q.Members, UnitRole.None);   // 자동으로 되돌림
        w.Step();
        bool back = q.Members.All(u => u.Role == before[u.Id]);
        // 중대를 떠나면 역할 없음
        var u0 = q.Members[0];
        w.DisbandCompany(1, c); w.Step();
        check(set && back && u0.Role == UnitRole.None, $"역할 재배정: 분대 → 지원 {set}, 자동 되돌림 {back}, 해산 후 None {u0.Role == UnitRole.None}");
    }

    // ---------------------------------------------------------------- 전술 틀 배치

    static Dictionary<UnitRole, Vector2> RoleCentroids(Company c)
    {
        var fl = FinalLocal(c);
        return c.Members.GroupBy(u => u.Role).ToDictionary(g => g.Key, g => Mean(g.Select(u => fl[u.Id])));
    }

    static (World W, Company C) Placed(CompanyTactic t, float width = 0)
    {
        var (w, c) = Rifles();
        w.SetCompanyTactic(1, new[] { c }, t);
        w.Step();
        w.Move(1, c.Members, new Vector2(70, 50), new FormSpec(0, 0, North, width));
        w.Step();
        return (w, c);
    }

    static void TestTemplatePlacement()
    {
        float D = defs.Tactics.DepthUnit;
        {
            var (w, c) = Placed(CompanyTactic.FireManoeuvre);
            var m = RoleCentroids(c);
            bool ok = c.Tactic == CompanyTactic.FireManoeuvre && m[UnitRole.Manoeuvre].X > m[UnitRole.Fire].X + 4 && m[UnitRole.Manoeuvre].Y > m[UnitRole.Rifle].Y + 1
                      && m[UnitRole.Command].Y < m[UnitRole.Rifle].Y - 2 && m[UnitRole.Fire].X < m[UnitRole.Rifle].X;
            check(ok, $"화력-기동: 기동조 오른쪽 앞 ({m[UnitRole.Manoeuvre].X:0.0},{m[UnitRole.Manoeuvre].Y:0.0}), 화력조 왼쪽 ({m[UnitRole.Fire].X:0.0}), 지휘 뒤 ({m[UnitRole.Command].Y:0.0})");
            // 묶음 배정: 같은 분대 · 같은 역할 병사는 서로 붙어 있다 (다른 묶음 슬롯으로 섞이지 않음)
            var fl = FinalLocal(c);
            bool tight = c.Squads.All(q => q.Members.GroupBy(u => u.Role).All(g => g.Max(a => g.Max(b => Vector2.Distance(fl[a.Id], fl[b.Id]))) < 14f));
            check(tight, "틀 배치: 분대 · 역할 묶음마다 슬롯이 모여 있음 (묶음 밖 배정 없음)");
        }
        {
            var (_, cl) = Placed(CompanyTactic.FlankLeft);
            var (_, cr) = Placed(CompanyTactic.FlankRight);
            var ml = RoleCentroids(cl); var mr = RoleCentroids(cr);
            bool ok = ml[UnitRole.Manoeuvre].X < ml[UnitRole.Rifle].X - 5 && mr[UnitRole.Manoeuvre].X > mr[UnitRole.Rifle].X + 5 && ml[UnitRole.Manoeuvre].Y > ml[UnitRole.Rifle].Y;
            check(ok, $"측면 우회: 좌 = 기동조 왼쪽 앞 ({ml[UnitRole.Manoeuvre].X:0.0}), 우 = 오른쪽 앞 ({mr[UnitRole.Manoeuvre].X:0.0})");
        }
        {
            var (_, c) = Placed(CompanyTactic.DefenceDepth);
            var m = RoleCentroids(c);
            bool ok = m[UnitRole.Rifle].Y > m[UnitRole.Fire].Y && m[UnitRole.Manoeuvre].Y < m[UnitRole.Rifle].Y - D * 1.5f;
            check(ok, $"종심 방어: 사격조 앞 ({m[UnitRole.Rifle].Y:0.0}), 화력조 뒤 ({m[UnitRole.Fire].Y:0.0}), 기동조 예비 깊이 ({m[UnitRole.Manoeuvre].Y:0.0})");
        }
        {
            var (_, c) = Placed(CompanyTactic.Assault);
            var m = RoleCentroids(c);
            bool ok = m[UnitRole.Manoeuvre].Y > m[UnitRole.Rifle].Y + 1 && m[UnitRole.Command].Y < m[UnitRole.Rifle].Y;
            check(ok, $"돌격: 기동조 맨 앞 ({m[UnitRole.Manoeuvre].Y:0.0}), 지휘 뒤 ({m[UnitRole.Command].Y:0.0})");
        }
        {
            // 우클릭 드래그 폭 = 전선 폭: 넓히면 역할군 간격도 넓어짐
            var (_, a) = Placed(CompanyTactic.FireManoeuvre, 0);
            var (_, b) = Placed(CompanyTactic.FireManoeuvre, 60);
            var ma = RoleCentroids(a); var mb = RoleCentroids(b);
            float da = ma[UnitRole.Manoeuvre].X - ma[UnitRole.Fire].X, db = mb[UnitRole.Manoeuvre].X - mb[UnitRole.Fire].X;
            check(db > da + 5, $"틀 + 우클릭 드래그 폭: 폭 60 에서 화력조 ↔ 기동조 간격 {db:0.0} > 기본 {da:0.0}");
        }
        {
            // 도착하면 역할군이 그 자리에 선다 (정지 배치)
            var (w, c) = Placed(CompanyTactic.DefenceDepth);
            Steps(w, 1800);
            var fl = c.Members.Where(u => u.Alive).ToList();
            Vector2 goal = c.Anchor?.Goal ?? new Vector2(70, 50);
            var rifY = Mean(fl.Where(u => u.Role == UnitRole.Rifle).Select(u => u.Pos)).Y;
            var manY = Mean(fl.Where(u => u.Role == UnitRole.Manoeuvre).Select(u => u.Pos)).Y;
            check(manY > rifY + D * 1.2f, $"종심 방어 도착: 기동조가 사격조보다 {manY - rifY:0.0}칸 뒤 (북쪽 진행)");
        }
    }

    static void TestArtilleryFormation()
    {
        var (w, c) = Placed(CompanyTactic.Artillery);
        var fl = FinalLocal(c);
        var art = defs.Formations.Artillery;
        // 분대마다 종대 (깊이 > 폭), 분대 사이 가로 간격 ≥ squadGap
        var boxes = c.Squads.Select(q =>
        {
            var ps = q.Members.Select(u => fl[u.Id]).ToList();
            return (MinX: ps.Min(p => p.X), MaxX: ps.Max(p => p.X), MinY: ps.Min(p => p.Y), MaxY: ps.Max(p => p.Y));
        }).OrderBy(b => b.MinX).ToList();
        bool cols = boxes.All(b => b.MaxY - b.MinY > b.MaxX - b.MinX);
        bool gaps = Enumerable.Range(1, boxes.Count - 1).All(i => boxes[i].MinX - boxes[i - 1].MaxX >= art.SquadGap - 0.05f);
        var off = c.Members.Where(u => u.IsOfficer).Select(u => fl[u.Id]).ToList();
        bool hq = off.Count > 0 && off.All(p => p.Y < boxes.Min(b => b.MinY));
        check(cols && gaps && hq, $"포병 대형: 분대 {boxes.Count}개가 작은 종대 {cols}, 종대 사이 ≥ {art.SquadGap}칸 {gaps}, 본부 뒤 {hq}");
    }

    static void TestArtilleryDeploy()
    {
        var (w, c) = Rifles(at: new Vector2(70, 110));
        w.SetCompanyTactic(1, new[] { c }, CompanyTactic.Artillery);
        w.Step();
        w.AttackMove(1, c.Members, new Vector2(70, 40), new FormSpec(0, 0, North));
        Steps(w, 120);
        bool marching = c.Tactic == CompanyTactic.Artillery && c.Marching;
        var foe = Spawn(w, "infantry", 6, (c.FlagPos ?? new Vector2(70, 90)) + new Vector2(0, -18), 6, 2);
        foreach (var f in foe) w.Hold(2, new[] { f });
        long t0 = w.Tick;
        for (int i = 0; i < 600 && c.Tactic == CompanyTactic.Artillery; i++) w.Step();
        // 2단계 (TacticDrill.cs): 장교가 지휘하면 tactics.json artillery.deployTactic (화력-기동) 으로 전개 (지휘 없으면 횡대 — TacticDrillSelfTest)
        bool deployed = c.Tactic == CompanyTactic.FireManoeuvre && c.ActiveTactic == CompanyTactic.FireManoeuvre && c.Form == FormType.Line && w.Stats.ArtilleryDeploys >= 1;
        // 전개 후 행군 슬롯 = 틀 배치 (기동조가 사격조보다 앞)
        bool laid = c.Anchor != null && c.Members.Where(u => u.Role == UnitRole.Manoeuvre && c.Anchor.Slots.ContainsKey(u.Id)).Select(u => c.Anchor.Slots[u.Id].Y).DefaultIfEmpty(-99).Average()
                    > c.Members.Where(u => u.Role == UnitRole.Rifle && c.Anchor.Slots.ContainsKey(u.Id)).Select(u => c.Anchor.Slots[u.Id].Y).DefaultIfEmpty(99).Average();
        check(marching && deployed && laid, $"포병 대형 접촉 → 화력-기동 틀 자동 전개: 행군 중 {marching}, 전개 {deployed} ({w.Tick - t0}틱), 틀 슬롯 {laid}");
    }

    static void TestWaves()
    {
        foreach (int k in new[] { 4, 6 })
        {
            var w = Bare(200);
            var cos = new List<Company>();
            for (int i = 0; i < k; i++)
            {
                var us = Spawn(w, "infantry", 12, new Vector2(30 + i * 22, 160));
                w.FormCompany(1, us, 3);
                w.Step();
                cos.Add(us[0].Company!);
            }
            var r = cos[0].Battalion!;
            w.SetBattalionForm(1, r, BatForm.Waves);
            w.Step();
            w.Move(1, cos.SelectMany(c => c.Members), new Vector2(100, 80), new FormSpec(0, 0, North));
            w.Step();
            // 깃발 목표의 앞뒤 (북쪽 = −Y) 줄
            var ys = cos.Select(c => c.Anchor!.Goal.Y).ToList();
            var rows = ys.Select(y => MathF.Round(y, 1)).Distinct().OrderBy(y => y).ToList();
            int want = k >= defs.Formations.Waves.ThreeRowsAt ? 3 : defs.Formations.Waves.Rows;
            // 줄 사이 실제 간격 (앞줄 블록 뒤끝 → 뒷줄 블록 앞끝) ≥ depthGap
            bool gap = true;
            for (int i = 1; i < rows.Count; i++)
            {
                var front = cos.Where(c => MathF.Round(c.Anchor!.Goal.Y, 1) == rows[i - 1]).ToList();
                var back = cos.Where(c => MathF.Round(c.Anchor!.Goal.Y, 1) == rows[i]).ToList();
                float frontTail = front.Max(c => c.Anchor!.FinalOff.Values.Max(o => o.Y) + c.Anchor.Goal.Y);
                float backHead = back.Min(c => c.Anchor!.FinalOff.Values.Min(o => o.Y) + c.Anchor.Goal.Y);
                gap &= backHead - frontTail >= defs.Formations.Waves.DepthGap - 0.1f;
            }
            check(r.Form == BatForm.Waves && rows.Count == want && gap, $"대대 파상 ({k}중대): 물결 {rows.Count}줄 (기대 {want}), 줄 사이 ≥ {defs.Formations.Waves.DepthGap}칸 {gap}");
        }
    }

    static void TestSquadAloneMove()
    {
        var (w, c) = Rifles();
        var q = c.Squads[1];
        w.SetSquadForm(1, new[] { q }, FormType.Line);
        w.Step();
        w.Move(1, q.Members, new Vector2(40, 60), new FormSpec(0, 0, North));
        w.Step();
        var g = q.Members[0].Group;
        bool line = g?.Plan != null && q.Members.All(u => MathF.Abs(g.Plan.Off[u.Id].Y - g.Plan.Off[q.Members[0].Id].Y) < 0.05f);
        check(q.Form == FormType.Line && line, $"분대만 따로 이동 = 분대 대형 (횡대 한 줄) {line}");
    }

    // ---------------------------------------------------------------- 전술 AI 공용 (프로필 tactics.templates)

    static void TestAiTemplates()
    {
        // 기본 켜짐 (사용자 결정 2026-10-19 — 2단계 행동 뒤). 공격 = 접촉 (구성원이 표적을 잡음) 뒤 화력-기동, 방어 = 종심 방어.
        bool on = KoreanDivision.Core.AiProfiles.Find("default")!.Tactics.Templates;
        var setup = new BattleSetup { SideA = "infantry:100,officer:3", SideB = "infantry:100,officer:3", Map = "flat", Distance = 40, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, 3);
        w.Controllers.Add((1, AiRegistry.Create("tactical:notemplates")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        bool picked = true, any = false;
        for (int i = 0; i < 1800; i++)
        {
            w.Step();
            if (i % 30 != 0) continue;
            if (w.Controllers.First(c => c.Faction == 2).Ai is not TacticalAi ta || ta.StateOf(2) is not { } st) continue;
            foreach (var g in st.Groups.Where(g => g.Kind == TacKind.Line && g.CompanyId > 0))
                if (w.FindCompany(g.CompanyId) is { } co)
                {
                    if (g.Task == TacTask.Defend && co.Tactic == CompanyTactic.DefenceDepth) any = true;
                    if (g.Task == TacTask.Attack && co.Tactic == CompanyTactic.FireManoeuvre) any = true;
                    if (g.Task == TacTask.Attack && co.Tactic == CompanyTactic.DefenceDepth && co.Members.Exists(u => u.Target != null)) picked = false;
                }
        }
        bool other = w.Companies.Where(c => c.Owner == 1).All(c => c.Tactic == CompanyTactic.None);
        check(on && any && picked && other, $"전술 AI 틀: 기본 켜짐 {on}, 공격 (접촉 뒤) = 화력-기동 · 방어 = 종심 방어 사용 {any && picked}, 틀 끈 프로필 세력 영향 없음 {other}");
    }

    // ---------------------------------------------------------------- 저장 · 결정성 · 리플레이

    static World Scenario()
    {
        var (w, c) = Rifles(at: new Vector2(70, 110));
        w.SetCompanyTactic(1, new[] { c }, CompanyTactic.FireManoeuvre);
        w.SetSquadForm(1, new[] { c.Squads[0] }, FormType.Wedge);
        w.SetUnitRole(1, c.Squads[1].Members, UnitRole.Support);
        w.Step();
        w.AttackMove(1, c.Members, new Vector2(70, 40), new FormSpec(0, 0, North));
        Steps(w, 200);
        return w;
    }

    static void TestSaveAndDeterminism()
    {
        var x = Scenario();
        var y0 = Scenario();
        check(x.ComputeChecksum() == y0.ComputeChecksum(), "같은 명령 → 같은 체크섬 (틀 · 분대 대형 · 역할 재배정 포함)");
        var c = x.Companies[0];
        var bytes = Snapshot.Save(x, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        var yc = y.Companies.First(k => k.Id == c.Id);
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool state = yc.Tactic == c.Tactic && yc.Squads[0].Form == FormType.Wedge && yc.Members.Select(u => u.Role).SequenceEqual(c.Members.Select(u => u.Role));
        check(same && state && x.ComputeChecksum() == y.ComputeChecksum(), $"저장 → 불러오기: 바이트 동일 {same}, 틀 · 분대 대형 · 역할 복원 {state}, 체크섬 동일");
        Steps(x, 600); Steps(y, 600);
        check(x.ComputeChecksum() == y.ComputeChecksum(), "불러온 월드를 600틱 이어 돌려도 같은 체크섬");
        // 체크섬이 틀 상태를 본다
        var z = Scenario();
        z.SetCompanyTactic(1, z.Companies, CompanyTactic.Assault);
        z.Step();
        var x2 = Scenario(); x2.Step();
        check(z.ComputeChecksum() != x2.ComputeChecksum(), "체크섬에 전술 틀 포함");
        // 명령 JSON (리플레이) 왕복
        var cmds = new GameCommand[] { new SetCompanyTacticCommand(1, new[] { 3 }, 5), new SetSquadFormCommand(1, new[] { 2, 4 }, 7), new SetUnitRoleCommand(1, new[] { 9 }, 3) };
        var json = JsonSerializer.Serialize(cmds);
        var back = JsonSerializer.Deserialize<GameCommand[]>(json)!;
        bool rt = back.Length == 3 && back[0] is SetCompanyTacticCommand t && t.Tactic == 5 && back[1] is SetSquadFormCommand s && s.Squads.SequenceEqual(new[] { 2, 4 }) && back[2] is SetUnitRoleCommand r && r.Role == 3;
        check(rt && json.Contains("\"cotactic\"") && json.Contains("\"sqform\"") && json.Contains("\"role\""), "새 명령 (cotactic · sqform · role) 리플레이 JSON 왕복");
    }
}
