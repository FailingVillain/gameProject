using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>pronemove: 엎드린 (제압) 병사는 대열 이동에 끌려가지 않는다. KD_SELFTEST_ONLY=pronemove.</summary>
public static class ProneMoveSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk; defs = Defs.Current;
        Console.WriteLine("[엎드림 이동]");
        using var nd = TestDefs.NoCommandDelay();
        try { TestFormation(); TestAssault(); TestCross(); TestIndividual(); TestRetreat(); TestSnapshot(); }
        catch (Exception e) { check(false, $"pronemove 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 120)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = true };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1); w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static readonly SnapshotMeta Meta = new("pronemove", 1, false, "standard");
    static CombatRulesDef C => defs.CombatRules;

    static float Crawl => defs.Units["infantry"].Speed * C.CrawlSpeedMul * 1.05f + 1e-3f;

    /// <summary>③ 자세 · 애니 · 속도 일관성: 엎드린 채 (Prone) 움직이는 병사는 기어가는 속도 이하 (엎드려 걷기 금지), 고정 병사가 걸으면 기어가는 속도 이하 (후퇴 · 붕괴 = 일어나 달아남은 제외).</summary>
    static int Consistency(World w, int ticks, Action<int>? each = null)
    {
        int bad = 0;
        for (int t = 0; t < ticks; t++)
        {
            each?.Invoke(t); w.Step();
            foreach (var u in w.Units)
            {
                if (!u.Alive || u.Broken) continue;
                if (u.Walking && u.Prone && u.Velocity.Length() > u.Def.Speed * C.CrawlSpeedMul * 1.05f + 1e-3f) bad++;
                if (u.Walking && u.Pinned && !World.IsFleeing(u) && u.Velocity.Length() > u.Def.Speed * C.CrawlSpeedMul * 1.05f + 1e-3f) bad++;
            }
        }
        return bad;
    }

    static void TestFormation()
    {
        var w = Bare();
        var us = new List<Unit>();
        for (int i = 0; i < 20; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % 10 - 4.5f), 70 + i / 10)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 72)));
        w.FormCompany(1, us); w.Step();
        w.Move(1, us, new Vector2(60, 40), new FormSpec((int)FormType.Line, 0, new Vector2(0, -1)));
        var men = us.Where(u => !u.IsOfficer).ToList();
        var pin = men.Take(3).ToList(); var prn = men.Skip(3).Take(3).ToList(); var free = men.Skip(6).ToList();
        var p0 = men.ToDictionary(u => u.Id, u => u.Pos);
        bool supp = true;
        Action<int> hold = _ =>
        {
            if (!supp) return;
            foreach (var u in pin) u.Suppression = 800;
            foreach (var u in prn) u.Suppression = 450;
        };
        int capT = (int)MathF.Round(C.ProneHoldMaxSec / Config.SimDt);
        int bad = Consistency(w, capT - 60, hold);   // 상한 (proneHoldMaxSec) 1초 전까지
        float dPin = pin.Max(u => Vector2.Distance(u.Pos, p0[u.Id])), dPrn = prn.Max(u => Vector2.Distance(u.Pos, p0[u.Id])), dFree = free.Min(u => Vector2.Distance(u.Pos, p0[u.Id]));
        check(prn.All(u => u.Prone) && pin.All(u => u.Pinned), $"제압 중 엎드림 {prn.Count(u => u.Prone)}/{prn.Count} · 고정 {pin.Count(u => u.Pinned)}/{pin.Count}");
        check(dPin < 1.5f && dPrn < 1.5f && dFree > 8f, $"대열 이동: 상한 전 엎드린 · 고정 병사는 끌려가지 않음 (고정 {dPin:0.0} · 엎드림 {dPrn:0.0} < 1.5, 나머지 최소 {dFree:0.0} > 8)");
        // 상한 뒤: 아직 제압 중이어도 엎드린 채 대열을 따라 기어감 (기는 속도 이하)
        var q0 = pin.Concat(prn).ToDictionary(u => u.Id, u => u.Pos);
        float maxV = 0;
        bad += Consistency(w, 60 * 6, t => { hold(t); foreach (var u in pin.Concat(prn)) if (u.Walking) maxV = MathF.Max(maxV, u.Velocity.Length()); });
        float crawled = pin.Concat(prn).Min(u => Vector2.Distance(u.Pos, q0[u.Id]));
        check(pin.Concat(prn).All(u => u.Prone) && crawled > 1f && maxV > 0 && maxV <= Crawl, $"상한 {C.ProneHoldMaxSec}초 뒤 엎드린 · 고정 병사는 엎드린 채 대열을 따라 기어감 (최소 {crawled:0.0}칸 > 1, 최고 속도 {maxV:0.00} ≤ {Crawl:0.00})");
        supp = false;
        foreach (var u in pin.Concat(prn)) u.Suppression = 0;
        bad += Consistency(w, 60 * 40);
        var a = us[0].Company!.Anchor;
        float far = pin.Concat(prn).Max(u => Vector2.Distance(u.Pos, new Vector2(60, 40)));
        check(pin.Concat(prn).All(u => !u.Prone && !u.Pinned) && far < 25f, $"제압이 풀리면 일어나 대열에 합류 (목표까지 최대 {far:0.0})");
        check(bad == 0, $"자세 · 이동 일관성: 걸으며 엎드림 / 고정이 기는 속도 초과 위반 {bad}건 (0)");
    }

    /// <summary>돌격 · 강행 (속보) 대열: proneHoldMaxAssaultSec 뒤 가볍게 엎드린 병사는 일어나 걷고, 고정 병사는 기어서 따라간다 (사용자 2026-10-21 "3초 상한은 추천안 대로").</summary>
    static void TestAssault()
    {
        var w = Bare();
        var us = new List<Unit>();
        for (int i = 0; i < 20; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % 10 - 4.5f), 90 + i / 10)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 92)));
        w.FormCompany(1, us); w.Step();
        w.QuickStep(1, us, new Vector2(60, 40), new FormSpec((int)FormType.Line, 0, new Vector2(0, -1)));
        var men = us.Where(u => !u.IsOfficer).ToList();
        var pin = men.Take(3).ToList(); var prn = men.Skip(3).Take(3).ToList();
        var p0 = men.ToDictionary(u => u.Id, u => u.Pos);
        bool prnSupp = true;
        Action<int> hold = _ => { foreach (var u in pin) u.Suppression = 800; if (prnSupp) foreach (var u in prn) u.Suppression = 450; };
        int capT = (int)MathF.Round(C.ProneHoldMaxAssaultSec / Config.SimDt);
        int bad = Consistency(w, capT - 45, hold);
        float dPin = pin.Max(u => Vector2.Distance(u.Pos, p0[u.Id])), dPrn = prn.Max(u => Vector2.Distance(u.Pos, p0[u.Id]));
        check(us.All(u => u.QuickStep || u.IsOfficer) && prn.All(u => u.Prone) && dPin < 1.5f && dPrn < 1.5f, $"속보 대열: 상한 {C.ProneHoldMaxAssaultSec}초 전엔 엎드린 채 (고정 {dPin:0.0} · 엎드림 {dPrn:0.0} < 1.5)");
        var q0 = men.ToDictionary(u => u.Id, u => u.Pos);
        float maxPin = 0; int prnUp = 0;
        bad += Consistency(w, 60 * 5, t =>
        {
            hold(t);
            foreach (var u in pin) if (u.Walking) maxPin = MathF.Max(maxPin, u.Velocity.Length());
            if (t == 60) prnUp = prn.Count(u => !u.Prone);
        });
        float walkPrn = prn.Min(u => Vector2.Distance(u.Pos, q0[u.Id])), crawlPin = pin.Min(u => Vector2.Distance(u.Pos, q0[u.Id]));
        check(prnUp == prn.Count && walkPrn > crawlPin * 2f && walkPrn > 5f, $"속보 상한 뒤 가볍게 엎드린 병사는 일어나 걸어서 따라감 (일어남 {prnUp}/{prn.Count}, 걸음 {walkPrn:0.0} > 5 · 기기의 2배)");
        check(pin.All(u => u.Pinned && u.Prone) && crawlPin > 1f && maxPin <= Crawl, $"속보 상한 뒤 고정 병사는 기어서 따라감 (이동 {crawlPin:0.0} > 1, 최고 속도 {maxPin:0.00} ≤ {Crawl:0.00})");
        check(bad == 0, $"속보 대열 자세 · 이동 일관성 위반 {bad}건 (0)");
    }

    /// <summary>도하 강행 면제 (사용자 2026-10-21 "도하에는 엎드림 정지 면제 승인"): 전술 AI 도하 속보 (QuickStep cross) 대열의 엎드린 병사는 상한 전에도 멈추지 않고
    /// 일어나 걸어서 따라간다 (엎드림 정지 · 일어서기 지연 · 기어가기 없음 — pronemove 이전 동작). 같은 속보라도 cross 가 아니면 TestAssault 대로 멈춘다.</summary>
    static void TestCross()
    {
        var w = Bare();
        var us = new List<Unit>();
        for (int i = 0; i < 20; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60 + (i % 10 - 4.5f), 90 + i / 10)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 92)));
        w.FormCompany(1, us); w.Step();
        var men = us.Where(u => !u.IsOfficer).ToList();
        var prn = men.Skip(3).Take(3).ToList();
        for (int t = 0; t < 45; t++) { foreach (var u in prn) u.Suppression = 450; w.Step(); }   // 먼저 엎드린 상태
        bool prone0 = prn.All(u => u.Prone);
        w.QuickStep(1, us, new Vector2(60, 40), new FormSpec((int)FormType.Line, 0, new Vector2(0, -1)), cross: true);
        var p0 = prn.ToDictionary(u => u.Id, u => u.Pos);
        int held = 0, stand = 0; float maxV = 0;
        int capT = (int)MathF.Round(C.ProneHoldMaxAssaultSec / Config.SimDt);
        for (int t = 0; t < capT - 45; t++)   // 속보 상한 (3초) 전에
        {
            foreach (var u in prn) u.Suppression = 450;
            w.Step();
            foreach (var u in prn) { if (u.ProneHoldSince != 0) held++; if (u.StandUntil != 0) stand++; if (u.Walking) maxV = MathF.Max(maxV, u.Velocity.Length()); }
        }
        float moved = prn.Min(u => Vector2.Distance(u.Pos, p0[u.Id]));
        check(prone0 && us.All(u => u.IsOfficer || u.QuickStep && u.ForceCross) && held == 0 && stand == 0, $"도하 강행 (속보 cross): 엎드림 정지 · 일어서기 지연 면제 (버티기 {held} · 일어서기 {stand} 틱 = 0, 시작 엎드림 {prone0})");
        check(moved > 3f && maxV > Crawl, $"도하 강행: 엎드렸던 병사가 상한 {C.ProneHoldMaxAssaultSec}초 전에도 걸어서 대열을 따라감 (최소 {moved:0.0}칸 > 3, 최고 속도 {maxV:0.00} > 기기 {Crawl:0.00})");
        w.Move(1, us, new Vector2(60, 90));
        w.Step(); w.Step();
        check(us.All(u => !u.ForceCross), "새 (보통) 이동 명령이면 도하 면제 해제");
    }

    static void TestIndividual()
    {
        var w = Bare();
        var inf = defs.Units["infantry"];
        var pinned = w.SpawnUnit(inf, 1, new Vector2(40, 60)); var prone = w.SpawnUnit(inf, 1, new Vector2(44, 60)); var free = w.SpawnUnit(inf, 1, new Vector2(48, 60));
        w.Step();
        foreach (var u in new[] { pinned, prone, free }) w.Move(1, new List<Unit> { u }, new Vector2(u.Pos.X, 30));
        long t0 = w.Tick; long moveAt = -1; float maxSpd = 0; int bad = 0; bool standHeld = true;
        var pos0 = prone.Pos;
        for (int t = 0; t < 600; t++)
        {
            pinned.Suppression = 700;
            if (t < 2) prone.Suppression = 450;
            else if (prone.Prone || prone.StandUntil != 0 || t < 5) prone.Suppression = 450;
            w.Step();
            if (prone.Walking && moveAt < 0) moveAt = w.Tick - t0;
            if (moveAt < 0 && Vector2.Distance(prone.Pos, pos0) > 0.05f) standHeld = false;
            if (pinned.Walking) maxSpd = MathF.Max(maxSpd, pinned.Velocity.Length());
            if (prone.Walking && prone.Prone) bad++;
            if (pinned.Walking && pinned.Velocity.Length() > inf.Speed * C.CrawlSpeedMul * 1.05f + 1e-3f) bad++;
        }
        float dPinned = Vector2.Distance(pinned.Pos, new Vector2(40, 60)), dFree = Vector2.Distance(free.Pos, new Vector2(48, 60));
        check(dPinned > 1f && maxSpd > 0 && maxSpd <= inf.Speed * C.CrawlSpeedMul * 1.05f + 1e-3f, $"고정 병사의 개별 이동 명령 = 기어감 (이동 {dPinned:0.0}, 최고 속도 {maxSpd:0.00} ≤ {inf.Speed * C.CrawlSpeedMul:0.00})");
        check(dFree > dPinned * 2f, $"서 있는 병사보다 기는 병사가 훨씬 느림 (걷기 {dFree:0.0} vs 기기 {dPinned:0.0})");
        float sec = moveAt * Config.SimDt;
        check(moveAt > 0 && standHeld && sec >= C.ProneStandSec - 0.2f && sec <= C.ProneStandSec + 1.0f, $"엎드린 병사의 개별 이동 명령 = 제자리에서 일어선 뒤 걷기 (걷기 시작 {sec:0.00}s, 기준 {C.ProneStandSec}s, 일어서는 동안 제자리 {standHeld})");
        check(bad == 0, $"개별 이동 일관성 위반 {bad}건 (0)");
    }

    /// <summary>후퇴 명령: 고정 병사도 일어나 (엎드리지 않고) 기는 속도보다 빠르게 달아난다 — 기어서 후퇴하면 추격에 잡힘 (tactical 곡사포 회귀 원인).</summary>
    static void TestRetreat()
    {
        var w = Bare();
        var inf = defs.Units["infantry"];
        var us = new List<Unit>();
        for (int i = 0; i < 10; i++) us.Add(w.SpawnUnit(inf, 1, new Vector2(55 + i, 60)));
        us.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(60, 62)));
        w.FormCompany(1, us); w.Step();
        var pin = us.Take(3).ToList();
        for (int t = 0; t < 30; t++) { foreach (var u in pin) u.Suppression = 800; w.Step(); }
        bool pinned0 = pin.All(u => u.Pinned && u.Prone);
        var p0 = pin.ToDictionary(u => u.Id, u => u.Pos);
        w.Retreat(1, us, new Vector2(60, 100));
        int pronePinWalk = 0; float maxV = 0;
        for (int t = 0; t < 60 * 5; t++)
        {
            foreach (var u in pin) u.Suppression = 800;
            w.Step();
            foreach (var u in pin) { if (u.Walking && u.Prone) pronePinWalk++; if (u.Walking) maxV = MathF.Max(maxV, u.Velocity.Length()); }
        }
        float moved = pin.Min(u => Vector2.Distance(u.Pos, p0[u.Id]));
        check(pinned0 && pin.All(u => u.Pinned && World.IsFleeing(u)) && pronePinWalk == 0 && maxV > Crawl && moved > inf.Speed * C.CrawlSpeedMul * 5f, $"후퇴 명령: 고정 병사도 일어나 달아남 (엎드려 이동 {pronePinWalk}건, 최고 속도 {maxV:0.00} > 기기 {Crawl:0.00}, 5초 이동 {moved:0.0}칸)");
    }

    static void TestSnapshot()
    {
        var w = Bare(); var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 40)); w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(80, 80));
        u.StandUntil = 1234; w.Step(); u.StandUntil = 1234;
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        check(y.Find(u.Id) is Unit o && o.StandUntil == 1234 && y.ComputeChecksum() == w.ComputeChecksum(), "StandUntil 저장 · 불러오기 · 체크섬 일치");
        var h0 = w.ComputeChecksum(); u.StandUntil = 99; var h1 = w.ComputeChecksum();
        check(h0 != h1, "StandUntil 이 체크섬에 들어감");
        u.ProneHoldSince = 4321;
        var z = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        check(z.Find(u.Id) is Unit o2 && o2.ProneHoldSince == 4321 && z.ComputeChecksum() == w.ComputeChecksum(), "ProneHoldSince 저장 · 불러오기 · 체크섬 일치");
        var h2 = w.ComputeChecksum(); u.ProneHoldSince = 77;
        check(h2 != w.ComputeChecksum(), "ProneHoldSince 가 체크섬에 들어감");
        u.ForceCross = true;
        var x = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        check(x.Find(u.Id) is Unit o3 && o3.ForceCross && x.ComputeChecksum() == w.ComputeChecksum(), "ForceCross 저장 · 불러오기 · 체크섬 일치");
        var h3 = w.ComputeChecksum(); u.ForceCross = false;
        check(h3 != w.ComputeChecksum(), "ForceCross 가 체크섬에 들어감");
    }
}
