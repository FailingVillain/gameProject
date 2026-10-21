using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>N73 사격 통제 (FireControl.cs): 자유 · 사격 금지 · 응사만, 피격 · 근접 트리거, 접적 상태, 저장 · 리플레이 왕복. KD_SELFTEST_ONLY=n73 로 단독 실행.</summary>
public static class N73SelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("n73", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[N73 사격 통제]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestModes();
            TestReturnTriggers();
            TestAttackOrderAndContact();
            TestRoundTrip();
        }
        catch (Exception e) { check(false, $"n73 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(bool record = false)
    {
        const int size = 100;
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        var w = new World(m, defs, 2) { RecordCommands = record };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    static void Steps(World w, int n) { for (int i = 0; i < n; i++) w.Step(); }

    /// <summary>아군 (소총 6) 과 적 (불사 · 사격 금지 · 대기) 3 을 dist 칸 떨어뜨려 세운다.</summary>
    static (World W, List<Unit> Us, List<Unit> Foes) Scene(float dist, bool record = false)
    {
        var w = Bare(record);
        var us = new List<Unit>();
        for (int i = 0; i < 6; i++) us.Add(w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(48 + i, 40)));
        var foes = new List<Unit>();
        for (int i = 0; i < 3; i++) { var f = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(49 + i, 40 + dist)); f.Hp = 1e9f; f.FireMode = FireMode.Hold; foes.Add(f); }
        w.Step();
        return (w, us, foes);
    }

    static long Shots(IEnumerable<Unit> us) => us.Count(u => u.LastShotTick > 0);

    static void TestModes()
    {
        var (w, us, foes) = Scene(10);
        Steps(w, 60 * 8);
        check(Shots(us) > 0 && us.All(u => u.FireMode == FireMode.Free), $"자유 사격 (기본): 8초 안에 사격 ({Shots(us)}명)");

        var (w2, us2, foes2) = Scene(10);
        w2.SetFireMode(1, us2, FireMode.Hold); w2.Step();
        bool set = us2.All(u => u.FireMode == FireMode.Hold);
        Steps(w2, 60 * 20);
        check(set && Shots(us2) == 0 && us2.All(u => u.Target == null) && foes2.All(f => f.Hp >= 1e9f), $"사격 금지: 20초 동안 한 발도 안 쏨 · 표적 없음 (사격 {Shots(us2)}명)");

        var (w3, us3, _) = Scene(10);
        w3.SetFireMode(1, us3, FireMode.ReturnFire); w3.Step();
        Steps(w3, 60 * 20);
        check(Shots(us3) == 0, $"응사만: 맞지도 않고 멀면 (10칸) 안 쏨 (사격 {Shots(us3)}명)");
    }

    static void TestReturnTriggers()
    {
        // 피격 → 응사 (분대원이 맞으면 분대 전체)
        var (w, us, _) = Scene(10);
        w.FormCompany(1, us); w.Step();
        w.SetFireMode(1, us, FireMode.ReturnFire); w.Step();
        Steps(w, 60 * 3);
        us[0].LastFiredAtTick = w.Tick;   // 한 명이 총격을 받음
        Steps(w, 60 * 6);
        check(Shots(us) > 0, $"응사만: 총격을 받으면 쏨 (사격 {Shots(us)}명)");
        // 가까이 온 적
        var (w2, us2, _) = Scene(3);
        w2.SetFireMode(1, us2, FireMode.ReturnFire); w2.Step();
        Steps(w2, 60 * 6);
        check(Shots(us2) > 0, $"응사만: 적이 가까이 (3칸) 오면 쏨 (사격 {Shots(us2)}명)");
        // 사격 금지는 가까워도 안 쏨
        var (w3, us3, _) = Scene(3);
        w3.SetFireMode(1, us3, FireMode.Hold); w3.Step();
        Steps(w3, 60 * 10);
        check(Shots(us3) == 0, $"사격 금지: 3칸 앞의 적에게도 안 쏨 (사격 {Shots(us3)}명)");
        w3.SetFireMode(1, us3, FireMode.Free); w3.Step();
        Steps(w3, 60 * 6);
        check(Shots(us3) > 0, "자유 사격으로 되돌리면 다시 쏨");
    }

    static void TestAttackOrderAndContact()
    {
        var (w, us, foes) = Scene(10);
        w.FormCompany(1, us); w.Step();
        var c = us[0].Company!;
        w.SetFireMode(1, us, FireMode.Hold); w.Step();
        w.Attack(1, us.Where(u => u.Weapon != null).ToList(), foes[0]);
        Steps(w, 60 * 15);
        check(Shots(us) == 0 && w.ContactOf(c) == ContactState.None, $"사격 금지: 공격 명령도 쏘지 않고 접적 상태를 만들지 않음 (사격 {Shots(us)}명, 접적 {w.ContactOf(c)})");

        var (w2, us2, _) = Scene(10);
        w2.FormCompany(1, us2); w2.Step();
        w2.SetFireMode(1, us2, FireMode.ReturnFire); w2.Step();
        Steps(w2, 60 * 10);
        check(w2.ContactOf(us2[0].Company!) == ContactState.None, "응사만: 맞기 전에는 접적 상태 아님");
        foreach (var u in us2) u.LastFiredAtTick = w2.Tick;
        Steps(w2, 60 * 4);
        check(w2.ContactOf(us2[0].Company!) != ContactState.None, "응사만: 총격을 받으면 접적 상태 (F1) 로");
    }

    static void TestRoundTrip()
    {
        var (w, us, _) = Scene(10, record: true);
        w.SetFireMode(1, us.Take(3), FireMode.Hold); w.Step();
        w.SetFireMode(1, us.Skip(3).Take(2), FireMode.ReturnFire); w.Step();
        Steps(w, 120);
        var bytes = Snapshot.Save(w, Meta);
        var y = Snapshot.Load(bytes, defs).World;
        bool same = Snapshot.RawBody(Snapshot.Save(y, Meta)).AsSpan().SequenceEqual(Snapshot.RawBody(bytes));
        bool fields = us.All(u => y.Units.First(v => v.Id == u.Id).FireMode == u.FireMode);
        var h0 = w.ComputeChecksum();
        us[0].FireMode = FireMode.Free; bool hash = w.ComputeChecksum() != h0; us[0].FireMode = FireMode.Hold;
        Steps(w, 300); Steps(y, 300);
        check(same && fields && hash && w.ComputeChecksum() == y.ComputeChecksum(), $"저장: 바이트 동일 {same} · 필드 {fields} · 체크섬 반영 {hash} · 이어 돌려도 같음 (형식 {Snapshot.FormatVersion})");

        var cmds = w.CommandLog.Select(c => c.Command).OfType<SetFireModeCommand>().ToList();
        bool json = cmds.Count == 2;
        if (json)
        {
            var s = JsonSerializer.Serialize<GameCommand>(cmds[0]);
            var back = JsonSerializer.Deserialize<GameCommand>(s) as SetFireModeCommand;
            json = back != null && back.Mode == cmds[0].Mode && back.Units.SequenceEqual(cmds[0].Units);
        }
        ulong Run()
        {
            var (a, ua, _) = Scene(10);
            a.SetFireMode(1, ua.Take(3), FireMode.Hold); a.Step();
            a.SetFireMode(1, ua.Skip(3).Take(2), FireMode.ReturnFire); a.Step();
            Steps(a, 300);
            return a.ComputeChecksum();
        }
        ulong r1 = Run(), r2 = Run();
        check(json && r1 == r2, $"리플레이: 명령 기록 {cmds.Count}건 · JSON 왕복 {json} · 같은 명령 = 같은 체크섬 ({r1:X16})");
    }
}
