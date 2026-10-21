using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>N79b 항복 (사용자 사양 2026-10-20): 엄격한 조건 · AI 는 퇴각 가능하면 항복 안 함 · 플레이어 낮은 빈도 · 항복자는 표적 아님 · 저장. KD_SELFTEST_ONLY=surr.</summary>
public static class SurrenderSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("surr", 1, false, "standard");
    static CombatRulesDef C => defs.CombatRules;
    static int Ticks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));

    sealed class ChanceScope : IDisposable { readonly float v = C.SurrenderChance, m = C.SurrenderPlayerMul; public ChanceScope(float c, bool keepMul = false) { C.SurrenderChance = c; if (!keepMul) C.SurrenderPlayerMul = 1f; } public void Dispose() { C.SurrenderChance = v; C.SurrenderPlayerMul = m; } }

    public static void Run(GameOptions opt, Action<bool, string> chk) => LegacyDeploy.Wrap(() => RunImpl(chk));
    static void RunImpl(Action<bool, string> chk)
    {
        check = chk; defs = Defs.Current;
        Console.WriteLine("[surr 항복 — 엄격한 조건 · AI 퇴각 먼저 · 플레이어 낮은 빈도 · 표적 제외 · 저장]");
        using var nd = TestDefs.NoCommandDelay();
        try { TestValues(); TestGates(); TestWaterBlocked(); TestAiRetreatFirst(); TestRates(); TestSurrenderedState(); TestSnapshot(); }
        catch (Exception e) { check(false, $"surr 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    static World Bare(int size = 120, Action<GameMap>? paint = null)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        paint?.Invoke(m);
        var w = new World(m, defs, 2) { RecordCommands = false };
        w.PlaceBuilding(defs.Headquarters, 1, 1, 1);
        w.PlaceBuilding(defs.Headquarters, 2, size - 1 - defs.Headquarters.Size, size - 1 - defs.Headquarters.Size);
        w.Fog.Enabled = false;
        return w;
    }

    /// <summary>중대 하나 (시작 initial 명, 지금 left 명). 반환 = 맨 앞 병사.</summary>
    static Unit Squad(World w, int left, int initial, Vector2 at, int owner = 1)
    {
        var list = Enumerable.Range(0, left).Select(i => w.SpawnUnit(defs.Units["infantry"], owner, at + new Vector2(i * 1.3f, 0))).ToList();
        w.Step();
        w.FormCompany(owner, list, 1); w.Step();
        list[0].Company!.InitialStrength = initial;
        foreach (var u in list) u.Hp = 1e6f;
        return list[0];
    }

    static void Enemy(World w, Vector2 p) { var e = w.SpawnUnit(defs.Units["infantry"], 2, p); e.Hp = 1e6f; }
    static void Ring(World w, Vector2 c, float r) { for (int k = 0; k < 8; k++) { float a = k * MathF.PI / 4; Enemy(w, c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r); } }

    /// <summary>sec 초 동안 제압을 supp 비로 유지하며 돌린다. 항복하면 true.</summary>
    static bool Hold(World w, Unit u, float supp, float sec)
    {
        for (int i = 0; i < Ticks(sec); i++) { u.Suppression = (short)(C.SuppressMax * supp); w.Step(); if (u.Surrendered) return true; }
        return false;
    }

    static void TestValues()
    {
        check(C.SurrenderChance is > 0 and <= 0.2f && C.SurrenderPlayerMul is > 0 and < 1 && C.SurrenderCasualty >= 0.8f && C.SurrenderSuppAt >= 0.85f && C.SurrenderSuppSec >= 5,
            $"balance.json: surrenderChance {C.SurrenderChance} (낮음) · playerMul {C.SurrenderPlayerMul} (<1) · casualty {C.SurrenderCasualty} · suppAt {C.SurrenderSuppAt} · {C.SurrenderSuppSec}s");
    }

    static void TestGates()
    {
        using var cs = new ChanceScope(1f);
        var c = new Vector2(60, 60);
        var w = Bare(); var u = Squad(w, 3, 30, c); Ring(w, c, 8f);
        check(Hold(w, u, 0.95f, C.SurrenderSuppSec + 6), "전 조건 충족 (사상 90% · 포위 · 제압 0.95 지속) → 항복 (확률 1)");
        w = Bare(); u = Squad(w, 20, 30, c); Ring(w, c, 8f);
        check(!Hold(w, u, 0.95f, C.SurrenderSuppSec + 6), "중대가 건재 (사상 33%) → 항복 안 함");
        w = Bare(); u = Squad(w, 3, 30, c); Ring(w, c, 8f);
        check(!Hold(w, u, 0.6f, C.SurrenderSuppSec + 6), "제압 0.6 (강한 제압 아님) → 항복 안 함");
        w = Bare(); u = Squad(w, 3, 30, c); Ring(w, c, 8f);
        check(!Hold(w, u, 0.95f, C.SurrenderSuppSec * 0.6f), $"제압이 {C.SurrenderSuppSec * 0.6f:0}초뿐 (< {C.SurrenderSuppSec}초 지속) → 항복 안 함");
        w = Bare(); u = Squad(w, 3, 30, c);
        check(!Hold(w, u, 0.95f, C.SurrenderSuppSec + 6), "주변에 적 없음 → 항복 안 함");
        w = Bare(); u = Squad(w, 3, 30, c); Enemy(w, c + new Vector2(0, -8));
        check(!Hold(w, u, 0.95f, C.SurrenderSuppSec + 6), "적이 한쪽뿐 · 뒤가 트임 → 퇴각 가능 → 항복 안 함");
    }

    static void TestWaterBlocked()
    {
        using var cs = new ChanceScope(1f);
        var c = new Vector2(60, 60);
        void Sea(GameMap m) { for (int y = 64; y < 120; y++) for (int x = 0; x < 120; x++) m.Set(x, y, TerrainType.Water); }
        var w = Bare(120, Sea);
        var u = Squad(w, 3, 30, c); Enemy(w, c + new Vector2(0, -8));
        check(w.CannotRetreat(u), "적 북쪽 · 남쪽 물 → 퇴각 불가 판정 (세 방향 모두 막힘)");
        check(Hold(w, u, 0.95f, C.SurrenderSuppSec + 6), "물에 막힌 소수 병력 · 강한 제압 → 항복");
        w = Bare(120, Sea);
        u = Squad(w, 3, 30, c); Enemy(w, c + new Vector2(0, 8));
        check(!w.CannotRetreat(u), "적 남쪽 · 북쪽 트임 → 퇴각 가능");
        w = Bare(); u = Squad(w, 3, 30, c); Enemy(w, c + new Vector2(0, -8)); Enemy(w, c + new Vector2(0, 8));
        check(w.CannotRetreat(u), "적이 앞뒤 (퇴각쪽에 적) → 퇴각 불가 (포위)");
    }

    static void TestAiRetreatFirst()
    {
        using var cs = new ChanceScope(1f);
        var c = new Vector2(60, 60);
        var w = Bare(); w.Faction(1).IsAi = true;
        var u = Squad(w, 3, 30, c); Enemy(w, c + new Vector2(0, -8));
        check(!Hold(w, u, 0.95f, C.SurrenderSuppSec + 8), "AI: 퇴각로가 열려 있으면 (확률 1 이라도) 항복하지 않고 퇴각 쪽");
        w = Bare(); w.Faction(1).IsAi = true; u = Squad(w, 3, 30, c); Ring(w, c, 8f);
        check(Hold(w, u, 0.95f, C.SurrenderSuppSec + 6), "AI: 퇴각 불가 (포위) 일 때만 항복");
        w = Bare(); u = Squad(w, 3, 30, c); Enemy(w, c + new Vector2(0, -8));
        w.Move(1, new[] { u }, new Vector2(80, 60));
        for (int i = 0; i < Ticks(2); i++) { u.Suppression = (short)(C.SuppressMax * 0.95f); w.Step(); }
        check(!u.Retreating && !u.Surrendered, "플레이어: 제압 · 소탕 상황에서도 자동 후퇴하지 않음 (명령 수행)");
    }

    static void TestRates()
    {
        using var cs = new ChanceScope(0.3f, keepMul: true);
        int Count(bool ai)
        {
            var w = Bare(120, m => { for (int y = 62; y < 120; y++) for (int x = 0; x < 120; x++) m.Set(x, y, TerrainType.Water); });
            w.Faction(1).IsAi = ai;
            var us = new List<Unit>();
            for (int i = 0; i < 90; i++)
            {
                us.Add(Squad(w, 1, 30, new Vector2(8 + i * 1.2f, 58), 1));
                Enemy(w, new Vector2(8 + i * 1.2f, 50));
            }
            for (int t = 0; t < Ticks(C.SurrenderSuppSec) + 2 * Ticks(C.SurrenderInterval) + 5; t++)
            {
                foreach (var u in us) u.Suppression = (short)(C.SuppressMax * 0.95f);
                w.Step();
            }
            return us.Count(u => u.Surrendered);
        }
        int ai = Count(true), pl = Count(false);
        Console.WriteLine($"[surr] 90명 · 같은 조건: AI {ai} 항복, 플레이어 {pl} 항복");
        check(ai > 0 && pl < ai && pl <= ai * 0.7f, $"플레이어는 AI 보다 항복 빈도가 낮다 (AI {ai} / 플레이어 {pl}, ×{C.SurrenderPlayerMul})");
    }

    static void TestSurrenderedState()
    {
        using var cs = new ChanceScope(1f);
        var c = new Vector2(60, 60);
        var w = Bare(); var u = Squad(w, 3, 30, c); Ring(w, c, 8f);
        Hold(w, u, 0.95f, C.SurrenderSuppSec + 6);
        check(u.Surrendered && w.Stats.Surrenders[1] >= 1, "항복 계수 (Stats.Surrenders) 증가");
        var p0 = u.Pos; long shot0 = u.LastShotTick;
        w.Move(1, new[] { u }, new Vector2(100, 60));   // 명령 무시
        for (int i = 0; i < Ticks(8); i++) w.Step();
        check(Vector2.Distance(p0, u.Pos) < 0.3f && u.Order == UnitOrder.Idle && u.Target == null, $"항복자는 움직이지 않고 명령을 무시 (이동 {Vector2.Distance(p0, u.Pos):0.00})");
        check(u.LastShotTick == shot0, "항복자는 쏘지 않음");
        bool targeted = false; float hp0 = u.Hp;
        for (int i = 0; i < Ticks(8); i++) { w.Step(); foreach (var e in w.Units.Where(x => x.Owner == 2)) if (ReferenceEquals(e.Target, u)) targeted = true; }
        check(!targeted && w.FindTarget(2, u.Pos, 10f) is not Unit { Surrendered: true }, "적이 항복자를 표적으로 잡지 않음 (FindTarget · 기존 표적 해제)");
        check(hp0 - u.Hp < 1f, "항복자는 직사 사격을 받지 않음 (체력 변화 없음)");
    }

    static void TestSnapshot()
    {
        using var cs = new ChanceScope(1f);
        var c = new Vector2(60, 60);
        var w = Bare(); var u = Squad(w, 3, 30, c); Ring(w, c, 8f);
        Hold(w, u, 0.95f, C.SurrenderSuppSec + 6);
        var other = Squad(w, 2, 30, new Vector2(30, 30)); other.SuppHighTicks = 77;
        for (int i = 0; i < 30; i++) w.Step();
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        bool same = y.Units.All(a => w.Find(a.Id) is Unit o && o.Surrendered == a.Surrendered && o.SuppHighTicks == a.SuppHighTicks) && y.ComputeChecksum() == w.ComputeChecksum();
        check(same && u.Surrendered, "항복 상태 저장 · 불러오기 · 체크섬 일치");
        var h0 = w.ComputeChecksum(); u.Surrendered = !u.Surrendered; var h1 = w.ComputeChecksum(); u.Surrendered = !u.Surrendered;
        other.SuppHighTicks++; var h2 = w.ComputeChecksum(); other.SuppHighTicks--;
        check(h0 != h1 && h0 != h2, "Surrendered · SuppHighTicks 가 체크섬에 들어감");
        for (int i = 0; i < 120; i++) { w.Step(); y.Step(); }
        check(w.ComputeChecksum() == y.ComputeChecksum(), "불러온 월드가 120틱 뒤에도 같은 체크섬");
        check(Snapshot.FormatVersion >= 43, $"Snapshot.FormatVersion {Snapshot.FormatVersion} (≥ 43)");
    }
}
