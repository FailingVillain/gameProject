using System.Numerics;
using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.Tests;

/// <summary>
/// 제압 재설계 (N25, 2026-10-19 사용자 결정): 근접 탄착 제압 (사선 · 폭발 띠, 감쇠 · 1.5칸 밖 없음) · 명중률 배수 · 고정 (엎드림) 이력 0.6 / 0.4 ·
/// 고정 중 이동 금지 (후퇴 명령 예외) · 엎드린 표적 명중 감소 · 비례 감소 · 장교 제압 후퇴 확률 (제압 · 사상 · 경험 · 부사관, 결정적) · 저장 · 체크섬 · 분대 아이콘.
/// 창 없이. KD_SELFTEST_ONLY=supp 로 단독 실행.
/// </summary>
public static class SuppressionSelfTest
{
    static Action<bool, string> check = null!;
    static Defs defs = null!;
    static readonly SnapshotMeta Meta = new("supp", 1, false, "standard");

    public static void Run(GameOptions opt, Action<bool, string> chk) => LegacyDeploy.Wrap(() => RunImpl(opt, chk));
    static void RunImpl(GameOptions opt, Action<bool, string> chk)
    {
        check = chk;
        defs = Defs.Current;
        Console.WriteLine("[supp 제압 재설계 — 근접 탄착 · 명중률 · 고정 · 장교 후퇴 판단]");
        using var nd = TestDefs.NoCommandDelay();
        try
        {
            TestValues();
            TestNearMissPath();
            TestNearMissRing();
            TestNearMissLive();
            TestAccuracy();
            TestPinHysteresis();
            TestPinnedNoMove();
            TestProneHarderToHit();
            TestDecay();
            TestMgKeepsPinned();
            TestRetreatHazard();
            TestAiSuppressionRetreat();
            TestSnapshot();
            TestSquadIcon();
            TestProneStanding();
            TestCoverDecay();
            TestBreakChance();
            TestBreakLive();
            TestBreakRetreatOrder();
            TestBreakCornered();
            TestBreakSnapshot();
            TestSourceCounters();
        }
        catch (Exception e) { check(false, $"supp 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---------------------------------------------------------------- 도우미

    static World Bare(int size = 120, Action<GameMap>? paint = null)
    {
        var m = new GameMap(size, size, 1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) m.Set(x, y, TerrainType.Grass);
        paint?.Invoke(m);
        var w = new World(m, defs, 2) { RecordCommands = false };
        var hq = defs.Headquarters;
        w.PlaceBuilding(hq, 1, 1, 1);
        w.PlaceBuilding(hq, 2, size - 1 - hq.Size, size - 1 - hq.Size);
        w.Fog.Enabled = false;
        return w;
    }

    /// <summary>시험 동안 붕괴를 끈다 (고정 자체를 보는 옛 시험 — 붕괴는 TestBreak* 에서 따로).</summary>
    sealed class NoBreakScope : IDisposable { readonly float v = C.BreakChance; public NoBreakScope() => C.BreakChance = 0; public void Dispose() => C.BreakChance = v; }
    /// <summary>엎드림 스위치 (combat.proneAt, 기본 0 = 끔) 를 시험 동안 제안값 0.35 로 켠다.</summary>
    sealed class ProneOnScope : IDisposable { readonly float v = C.ProneAt; public ProneOnScope() { if (C.ProneAt <= 0) C.ProneAt = 0.35f; } public void Dispose() => C.ProneAt = v; }
    /// <summary>붕괴 스위치 (combat.breakChance, 기본 0 = 끔) 를 시험 동안 제안값 0.15 로 켠다.</summary>
    sealed class BreakOnScope : IDisposable { readonly float v = C.BreakChance; public BreakOnScope() { if (C.BreakChance <= 0) C.BreakChance = 0.15f; } public void Dispose() => C.BreakChance = v; }
    static int Ticks(float sec) => Math.Max(1, (int)MathF.Round(sec / Config.SimDt));
    static CombatRulesDef C => defs.CombatRules;
    static float NearExpect(WeaponDef w, Unit e, float perp) =>
        MathF.Round(w.Suppress * C.NearMissFrac * MathF.Max(0f, 1f - MathF.Max(0f, perp - e.Radius) / C.NearMissRadius) * e.Def.SuppressResist);

    // ---------------------------------------------------------------- 수치

    static void TestValues()
    {
        check(C.NearMissFrac is > 0 and <= 1 && MathF.Abs(C.NearMissRadius - 1.5f) < 1e-4f && C.SuppressAccMul is > 0 and < 1 && C.PinAt > C.PinRelease && C.PinRelease > 0
              && C.PinnedAccMul is > 0 and <= 1 && C.ProneHitMul is > 0 and < 1 && C.SuppressDecayProp >= 0,
            $"balance.json combat: nearMissFrac {C.NearMissFrac} · radius {C.NearMissRadius} · suppressAccMul {C.SuppressAccMul} · pinAt/Release {C.PinAt}/{C.PinRelease} · pinnedAccMul {C.PinnedAccMul} · proneHitMul {C.ProneHitMul} · decay {C.SuppressDecay}+{C.SuppressDecayProp}×s");
    }

    // ---------------------------------------------------------------- 근접 탄착: 사선 1.5칸 안 감쇠, 밖은 0, 표적 · 아군 제외

    static void TestNearMissPath()
    {
        var w = Bare();
        var rifle = defs.Weapon("rifle")!;
        Vector2 from = new(30, 60), impact = new(60, 60);
        var target = w.SpawnUnit(defs.Units["infantry"], 2, impact);
        float[] perp = { 0.2f, 0.6f, 1.0f, 1.4f, 1.9f, 2.6f };
        var by = perp.Select((p, i) => w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(54.5f + i * 1.0f, 60 + p))).ToList();   // 착탄점 0.5~5.5칸 앞 (1칸 간격 — 겹침 해소에 밀리지 않게), 사선에서 p 칸
        var behind = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(62, 61.0f));   // 착탄점 너머 2칸 (탄이 계속 날아감)
        var far = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(45, 60.2f));      // 착탄점 15칸 앞 (사선 구간 밖)
        var friend = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(56, 59.0f));
        w.Step();
        foreach (var u in w.Units) u.Suppression = 0;
        w.NearMissPathForTest(1, from, impact, rifle.Suppress, target.Id);
        var got = by.Select(u => (int)u.Suppression).ToArray();
        var want = by.Select(u => (int)NearExpect(rifle, u, MathF.Abs(u.Pos.Y - 60f))).ToArray();   // 실제 위치 (겹침 해소로 조금 밀릴 수 있음)
        bool match = got.Zip(want).All(p => Math.Abs(p.First - p.Second) <= 1);
        bool mono = got.Zip(got.Skip(1)).All(p => p.First >= p.Second);
        bool zeroBeyond = by.Where((u, i) => perp[i] - u.Radius >= C.NearMissRadius).All(u => u.Suppression == 0);
        bool peak = got[0] > 0 && got[0] <= MathF.Round(rifle.Suppress * C.NearMissFrac * by[0].Def.SuppressResist);
        check(match && mono && zeroBeyond && peak, $"근접 탄착 (사선): 거리 {string.Join("/", perp)}칸 → 제압 {string.Join("/", got)} (기대 {string.Join("/", want)}, 최대 = 직접 {rifle.Suppress} × {C.NearMissFrac} × 저항, 1.5칸 밖 0)");
        check(target.Suppression == 0 && friend.Suppression == 0, $"근접 탄착: 표적 (직접 제압은 Fire 가 따로) · 아군 제외 (표적 {target.Suppression}, 아군 {friend.Suppression})");
        check(behind.Suppression > 0 && far.Suppression == 0, $"근접 탄착: 착탄점 너머 {C.NearMissOvershoot}칸 안 포함 ({behind.Suppression}), 착탄점 앞 {C.NearMissPathLen}칸 밖 사선은 제외 ({far.Suppression})");
        // 흩뿌림 원 제외 (이미 반경 제압을 받은 유닛)
        foreach (var u in w.Units) u.Suppression = 0;
        w.NearMissPathForTest(1, from, impact, rifle.Suppress, target.Id, impact, 3.2f);
        bool inside0 = by.Concat(new[] { behind }).Where(u => Vector2.Distance(u.Pos, impact) - u.Radius <= 3.2f).All(u => u.Suppression == 0);
        bool outsideGot = by.Where(u => Vector2.Distance(u.Pos, impact) - u.Radius > 3.2f).Any(u => u.Suppression > 0);
        check(inside0 && outsideGot, "근접 탄착: 흩뿌림 반경 안 유닛은 띠 제압에서 뺀다 (중복 없음), 그 밖 사선은 그대로");
    }

    static void TestNearMissRing()
    {
        var w = Bare();
        var how = defs.Weapon("howitzer_heavy")!;
        Vector2 at = new(60, 60);
        float rad = defs.Units["infantry"].Radius;
        float[] d = { 1.0f, how.Splash + rad + 0.3f, how.Splash + rad + 0.9f, how.Splash + rad + 1.6f, how.Splash + rad + 2.4f };
        var us = d.Select((r, i) => w.SpawnUnit(defs.Units["infantry"], 2, at + new Vector2(DetMath.Cos(i * 1.3f) * r, DetMath.Sin(i * 1.3f) * r))).ToList();   // 방향을 나눠 겹치지 않게
        w.Step();
        foreach (var u in w.Units) u.Suppression = 0;
        w.NearMissRingForTest(1, at, how.Splash, how.Suppress);
        var got = us.Select(u => (int)u.Suppression).ToArray();
        bool inner0 = got[0] == 0;   // 반경 안 = 호출자 (Explode) 가 전량
        bool band = got[1] > got[2] && got[2] > 0;
        bool outside = us.Where((u, i) => d[i] - u.Radius - how.Splash >= C.NearMissRadius).All(u => u.Suppression == 0);
        check(inner0 && band && outside, $"폭발 바깥 띠: 중심 거리 {string.Join("/", d.Select(x => x.ToString("0.0")))} → {string.Join("/", got)} (반경 {how.Splash} 안은 Explode 가 전량, 띠 {C.NearMissRadius}칸 감쇠, 그 밖 0)");
    }

    /// <summary>실전 경로: 소총 · 기관총 사격이 표적 옆 병사를 제압 (빗나간 탄 근접 제압이 Fire · FireSpray 에서 불림).</summary>
    static void TestNearMissLive()
    {
        foreach (var (def, wk) in new[] { ("infantry", "rifle"), ("heavy_machinegunner", "lmg") })
        {
            var w = Bare();
            var shooter = w.SpawnUnit(defs.Units[def], 1, new Vector2(40, 60));
            var target = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 60));
            var side = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 61.2f));   // 표적 옆 1.2칸 (흩뿌림 1.5 안 → 기관총은 원래 반경 제압)
            var side2 = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(57.5f, 61.6f));   // 사선 옆 1.6칸, 착탄점 2.5칸 앞
            w.Step();
            w.Hold(2, new[] { target, side, side2 });
            w.Attack(1, new[] { shooter }, target);
            long gain2 = 0; int prev = 0;
            for (int i = 0; i < Ticks(20) && target.Alive; i++)
            {
                w.Step();
                if (side2.Suppression > prev) gain2 += side2.Suppression - prev;
                prev = side2.Suppression;
                target.Hp = target.Def.Hp; side.Hp = side.Def.Hp; side2.Hp = side2.Def.Hp; shooter.Hp = shooter.Def.Hp; shooter.Suppression = 0;   // 오래 쏘게
            }
            check(gain2 > 0, $"근접 탄착 실전 ({wk}): 표적이 아닌 사선 옆 병사도 제압을 받음 (누적 +{gain2})");
        }
    }

    // ---------------------------------------------------------------- 명중률 × (1 − 0.5 × 제압비), 고정 사격자 추가 감소

    static void TestAccuracy()
    {
        var w = Bare();
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 60));
        var b = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(52, 60));
        w.Step();
        a.Suppression = 0; a.Pinned = false;
        float p0 = w.HitChance(a, b);
        a.Suppression = (short)(C.SuppressMax / 2);
        float p5 = w.HitChance(a, b);
        a.Suppression = (short)C.SuppressMax;
        float p10 = w.HitChance(a, b);
        a.Pinned = true;
        float pp = w.HitChance(a, b);
        a.Pinned = false; a.Suppression = 0;
        bool lin = MathF.Abs(p5 / p0 - (1f - C.SuppressAccMul * 0.5f)) < 1e-3f && MathF.Abs(p10 / p0 - (1f - C.SuppressAccMul)) < 1e-3f;
        check(p0 > 0 && lin, $"명중률 × (1 − {C.SuppressAccMul} × 제압비): 제압 0 {p0:0.0000} · 0.5 {p5:0.0000} (×{p5 / p0:0.000}) · 1.0 {p10:0.0000} (×{p10 / p0:0.000})");
        check(MathF.Abs(pp / p10 - C.PinnedAccMul) < 1e-3f, $"고정 (엎드림) 사격자는 명중률 추가 × {C.PinnedAccMul} ({pp / p10:0.000})");
    }

    // ---------------------------------------------------------------- 고정 이력 0.6 / 0.4

    static void TestPinHysteresis()
    {
        var w = Bare();
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 60));
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(30, 60));
        var cav = w.SpawnUnit(defs.Units["cavalry_default"], 1, new Vector2(30, 70));
        w.Step();
        int on = (int)MathF.Ceiling(C.PinAt * C.SuppressMax), rel = (int)MathF.Ceiling(C.PinRelease * C.SuppressMax);
        var seq = new List<bool>();
        foreach (int s in new[] { on - 10, on, (on + rel) / 2, rel, rel - 1, (on + rel) / 2, on })
        {
            u.Suppression = (short)s; w.UpdatePinnedForTest(u); seq.Add(u.Pinned);
        }
        bool ok = seq.SequenceEqual(new[] { false, true, true, true, false, false, true });
        check(ok, $"고정 이력: 제압 {on - 10}/{on}/{(on + rel) / 2}/{rel}/{rel - 1}/{(on + rel) / 2}/{on} → {string.Join("/", seq.Select(x => x ? "고정" : "-"))} (≥ {C.PinAt} 고정, < {C.PinRelease} 풀림)");
        u.Suppression = 0; w.UpdatePinnedForTest(u);
        // 시뮬 경로 (매 틱 UpdateUnitCore)
        u.Suppression = (short)(on + 50);
        w.Step();
        bool live = u.Pinned;
        off.Suppression = (short)C.SuppressMax; cav.Suppression = (short)C.SuppressMax;
        w.Step();
        check(live && !off.Pinned && !cav.Pinned, $"고정: 시뮬 틱에서 켜짐 {live}, 장교 (제압 면역) · 기병 (기마) 은 고정 대상 아님 ({off.Pinned}/{cav.Pinned})");
        u.ChargeState = 2; w.UpdatePinnedForTest(u);
        check(u.Pinned && u.ChargeState == 0, "고정되면 돌격을 그친다 (엎드린 병사는 돌격하지 않음)");
    }

    // ---------------------------------------------------------------- 고정 중 이동 금지 (후퇴 명령만 예외)

    static void TestPinnedNoMove()
    {
        using var nb = new NoBreakScope();
        var w = Bare();
        var us = Enumerable.Range(0, 4).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40 + i * 1.2f, 60))).ToList();
        var free = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 70));
        w.Step();
        w.Move(1, us, new Vector2(42, 30));
        w.Move(1, new[] { free }, new Vector2(40, 40));
        var p0 = us.Select(u => u.Pos).ToList(); var f0 = free.Pos;
        for (int i = 0; i < Ticks(3); i++) { foreach (var u in us) u.Suppression = (short)C.SuppressMax; w.Step(); }
        float moved = us.Select((u, i) => Vector2.Distance(u.Pos, p0[i])).Max();
        bool stillOrder = us.All(u => u.Order == UnitOrder.Move && u.Pinned);
        check(moved > 0.05f && moved < Vector2.Distance(free.Pos, f0) * 0.5f && stillOrder && Vector2.Distance(free.Pos, f0) > 1f, $"고정: 개별 이동 명령 = 기어감 (pronemove, 걷기의 절반 미만) (최대 {moved:0.000}칸, 명령 유지 {stillOrder}; 대조 고정 아님 {Vector2.Distance(free.Pos, f0):0.0}칸)");
        w.Retreat(1, us, new Vector2(42, 100));
        var p1 = us.Select(u => u.Pos).ToList();
        for (int i = 0; i < Ticks(3); i++) { foreach (var u in us) u.Suppression = (short)C.SuppressMax; w.Step(); }
        float back = us.Select((u, i) => Vector2.Distance(u.Pos, p1[i])).Min();
        check(back > 0.5f && us.All(u => u.Pinned), $"고정: 후퇴 명령이면 움직인다 (최소 {back:0.0}칸, 고정 유지)");
        // 고정 사격: 사거리 안 적은 쏜다
        var w2 = Bare();
        var a = w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 60));
        var e = w2.SpawnUnit(defs.Units["infantry"], 2, new Vector2(40, 45));
        w2.Step();
        w2.Hold(1, new[] { a });   // pronemove: 이동 명령이면 기어가므로 사수로 (제자리 사격)
        w2.Hold(2, new[] { e });
        int shots = 0; var pa = a.Pos;
        for (int i = 0; i < Ticks(8); i++)
        {
            a.Suppression = (short)C.SuppressMax; a.Hp = a.Def.Hp; e.Hp = e.Def.Hp; e.Suppression = 0;
            w2.Step();
            foreach (var s in w2.Shots) if (s.Tick == w2.Tick - 1 && s.ShooterId == a.Id) shots++;
        }
        check(shots > 0 && Vector2.Distance(a.Pos, pa) < 0.05f, $"고정: 제자리에서 사거리 안 적에게 사격 ({shots}발, 이동 {Vector2.Distance(a.Pos, pa):0.00}칸)");
    }

    // ---------------------------------------------------------------- 엎드린 표적은 맞히기 어렵다 (지형 엄폐와 겹치지 않음)

    static void TestProneHarderToHit()
    {
        var w = Bare(paint: m => m.Set(60, 52, TerrainType.Trench));
        var a = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40.5f, 52.5f));
        var b = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(52.5f, 52.5f));
        var t = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60.5f, 52.5f));
        w.Step();
        b.Pinned = false; float h0 = w.HitChance(a, b);
        b.Pinned = true; float h1 = w.HitChance(a, b);
        b.Pinned = false;
        check(MathF.Abs(h1 / h0 - C.ProneHitMul) < 1e-3f, $"엎드린 (고정) 표적 명중률 × {C.ProneHitMul} (개활지 {h0:0.0000} → {h1:0.0000})");
        float th = defs.Terrain.TrenchHit;
        float pt = w.HitChance(a, t);
        t.Pinned = true; float pt2 = w.HitChance(a, t); t.Pinned = false;
        float wantRatio = MathF.Min(th, C.ProneHitMul) / th;
        check(MathF.Abs(pt2 / pt - wantRatio) < 1e-3f, $"엎드림 · 참호는 겹치지 않음: 좋은 쪽 하나 (참호 {th}, 엎드림 {C.ProneHitMul} → 비 {pt2 / pt:0.000}, 기대 {wantRatio:0.000})");
    }

    // ---------------------------------------------------------------- 비례 감소

    static void TestDecay()
    {
        // N39 (2026-10-19 사용자 결정): 제압이 오래 남는다 — 사격이 그친 뒤 0.6 → 0.4 (고정 풀림) 약 10~15초 (장교 · 부사관 없음, 사병 계급 0)
        var w = Bare();
        var calm = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 60));
        var shot = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(50, 60));
        w.Step();
        int pin = (int)MathF.Round(C.PinAt * C.SuppressMax), rel = (int)MathF.Round(C.PinRelease * C.SuppressMax);
        calm.Suppression = (short)pin; shot.Suppression = (short)pin;
        shot.LastFiredAtTick = w.Tick;   // 방금 노려짐 (사격이 막 그침)
        long t0 = w.Tick, tCalm = -1, tShot = -1;
        for (int i = 0; i < 60 * 40 && (tCalm < 0 || tShot < 0); i++)
        {
            w.Step();
            if (tCalm < 0 && calm.Suppression < rel) tCalm = w.Tick - t0;
            if (tShot < 0 && shot.Suppression < rel) tShot = w.Tick - t0;
        }
        float sc = tCalm * Config.SimDt, ss = tShot * Config.SimDt;
        check(C.SuppressDecay is >= 20 and <= 40 && C.SuppressRankDecayBonus <= 0.05f, $"제압 감소 {C.SuppressDecay}/초 (옛 80), 계급 단계당 +{C.SuppressRankDecayBonus * 100:0}%");
        check(ss is >= 9.5f and <= 15f, $"사격이 그친 뒤 {C.PinAt} → {C.PinRelease} (고정 풀림): {ss:0.0}초 (기대 10~15, 최근 {C.SuppressCalmSec}초 노려진 동안 감소 ×{C.SuppressFireDecayMul})");
        check(tCalm > 0 && ss > sc, $"노려진 직후가 더 천천히 회복 ({ss:0.0}초 > 조용한 상태 {sc:0.0}초)");
    }

    static void TestMgKeepsPinned()
    {
        // N39: 지속 기관총 사격 아래 분대는 고정 상태로 남는다 (상한에 붙지는 않음)
        var w = Bare();
        var sq = Enumerable.Range(0, 8).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(36 + i * 1.1f, 60))).ToList();
        var mg = w.SpawnUnit(defs.Units["heavy_maxim"], 2, new Vector2(40, 44));
        w.Step();
        w.Hold(1, sq); w.Hold(2, new[] { mg });
        int samples = 0, pinned = 0, maxS = 0;
        for (int i = 0; i < Ticks(40); i++)
        {
            foreach (var u in sq) u.Hp = u.Def.Hp;
            mg.Hp = mg.Def.Hp; mg.Suppression = 0; mg.Ammo = Math.Max(mg.Ammo, 100);
            w.Step();
            if (i >= Ticks(15) && i % 30 == 0) foreach (var u in sq) { samples++; if (u.Pinned || u.Broken) pinned++; maxS = Math.Max(maxS, u.Suppression); }   // supp: 붕괴 (달아남) 도 눌림
        }
        float frac = pinned / (float)Math.Max(1, samples);
        check(frac >= 0.5f && maxS < C.SuppressMax, $"지속 맥심 사격 (16칸) 아래 소총 분대 8명: 15~40초 고정 · 붕괴 비율 {frac * 100:0}% (≥ 50%), 최대 제압 {maxS} (< 상한 {C.SuppressMax})");
    }

    // ---------------------------------------------------------------- 장교 후퇴 확률

    static void TestRetreatHazard()
    {
        var T = AiProfiles.Find("default")!.Tactics;
        float H(float s, float cas = 0f, CommandLevel lv = CommandLevel.Officer, int xp = 0, int rank = 3) => TacticalAi.RetreatHazard(T, s, cas, lv, xp, rank);
        float[] ss = { 0.2f, T.RetreatSuppStart, 0.4f, 0.5f, T.RetreatSuppression, 0.8f };
        var hs = ss.Select(s => H(s)).ToArray();
        bool rise = hs[0] == 0 && hs[1] == 0 && hs.Skip(1).Zip(hs.Skip(2)).All(p => p.Second > p.First);
        check(rise, $"제압 후퇴 위험률: 평균 제압 {string.Join("/", ss)} → {string.Join("/", hs.Select(h => h.ToString("0.000")))} /초 ({T.RetreatSuppStart} 에서 시작, {T.RetreatSuppression} 에서 높음)");
        float c0 = H(0.5f, 0f), c1 = H(0.5f, 0.15f), c2 = H(0.5f, 0.3f);
        check(c2 > c1 && c1 > c0 && H(0.2f, 0.3f) == 0, $"사상률과 결합: 제압 0.5 + 사상 0 / 15 / 30% → {c0:0.000} / {c1:0.000} / {c2:0.000} (제압이 시작점 아래면 0)");
        float x0 = H(0.6f, 0f, CommandLevel.Officer, 0), x4 = H(0.6f, 0f, CommandLevel.Officer, 4), r1 = H(0.6f, 0f, CommandLevel.Officer, 0, 1), r5 = H(0.6f, 0f, CommandLevel.Officer, 0, 5);
        float nco = H(0.6f, 0f, CommandLevel.Nco), none = H(0.6f, 0f, CommandLevel.None);
        check(x4 < x0 && r5 < r1 && nco > x0 && none > x0, $"지휘: 장교 경험 0 → 4 단계 {x0:0.000} → {x4:0.000}, 계급 소위 → 중령 {r1:0.000} → {r5:0.000}, 부사관 대리 {nco:0.000}, 지휘 없음 {none:0.000}");
        // 결정적 굴림
        float a = TacticalAi.RetreatRoll(1234, 1, 7, 42), b = TacticalAi.RetreatRoll(1234, 1, 7, 42), c = TacticalAi.RetreatRoll(1235, 1, 7, 42);
        double mean = Enumerable.Range(0, 4000).Average(i => TacticalAi.RetreatRoll(i * 30, 2, 3, 9));
        check(a == b && a != c && a is >= 0 and < 1 && mean is > 0.47 and < 0.53, $"후퇴 굴림: 같은 입력 = 같은 값 ({a:0.0000}), 틱이 바뀌면 다름, 평균 {mean:0.000}");
    }

    static World AiWorld(int seed)
    {
        var setup = new BattleSetup { SideA = "infantry:40,officer:2", SideB = "infantry:40,officer:2", Map = "flat", Distance = 70, AiVsAi = true, AiA = "tactical", AiB = "tactical" };
        var w = setup.CreateWorld(defs, seed);
        w.Controllers.Add((1, AiRegistry.Create("tactical")!));
        w.Controllers.Add((2, AiRegistry.Create("tactical")!));
        w.RecordCommands = true;
        return w;
    }

    static TacFaction? StOrNull(World w, int f) => ((TacticalAi)w.Controllers.First(c => c.Faction == f).Ai).StateOf(f);
    static TacFaction St(World w, int f) => ((TacticalAi)w.Controllers.First(c => c.Faction == f).Ai).StateOf(f)!;

    /// <summary>전술 AI: 사상 없이 중대 평균 제압만 높여도 장교가 (확률로) 후퇴를 명령 — 원인 = 제압, 같은 시드는 같은 틱. 제압이 시작점 아래면 후퇴 없음.</summary>
    static (long Tick, int Supp, int All, int Cause) RunAi(int seed, float supp)
    {
        var w = AiWorld(seed);
        long first = -1;
        for (int i = 0; i < Ticks(90) && first < 0; i++)
        {
            if (w.Tick > Ticks(3)) foreach (var u in w.Units) if (u.Alive && u.Owner == 1 && u.Def.IsInfantry) u.Suppression = (short)(supp * C.SuppressMax);
            w.Step();
            if (StOrNull(w, 1) is { Fallbacks: > 0 }) first = w.Tick;
        }
        var s = St(w, 1);
        int cause = s.Groups.Select(g => g.RetreatCause).DefaultIfEmpty(0).Max();
        return (first, s.FallbacksSupp, s.Fallbacks, cause);
    }

    static void TestAiSuppressionRetreat()
    {
        var a = RunAi(5, 0.75f);
        var b = RunAi(5, 0.75f);
        var lo = RunAi(5, 0.2f);
        check(a.Tick > 0 && a.Supp > 0 && a.Cause == 2, $"전술 AI 제압 후퇴: 사상 0, 평균 제압 0.75 → {a.Tick * Config.SimDt:0.0}초에 후퇴 (제압 원인 {a.Supp}/{a.All}, 원인 코드 {a.Cause})");
        check(a.Tick == b.Tick && a.Supp == b.Supp, $"제압 후퇴 결정성: 같은 시드 = 같은 틱 ({a.Tick} / {b.Tick})");
        check(lo.Supp == 0, $"평균 제압 0.2 (시작점 아래) 는 제압 후퇴 없음 (제압 원인 {lo.Supp}회, 전체 {lo.All})");
    }

    // ---------------------------------------------------------------- 저장 · 체크섬

    static void TestSnapshot()
    {
        var w = Bare();
        var us = Enumerable.Range(0, 3).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40 + i, 60))).ToList();
        w.Step();
        us[1].Suppression = (short)C.SuppressMax; w.Step();
        bool pinned = us[1].Pinned;
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        bool same = y.Units.All(u => w.Find(u.Id) is Unit o && o.Pinned == u.Pinned) && y.ComputeChecksum() == w.ComputeChecksum();
        check(pinned && same, $"고정: 저장 · 불러오기 · 체크섬 일치 (고정 {pinned})");
        var h0 = w.ComputeChecksum(); us[0].Pinned = !us[0].Pinned; var h1 = w.ComputeChecksum(); us[0].Pinned = !us[0].Pinned;
        check(h0 != h1, "고정: 체크섬에 들어감");
        for (int i = 0; i < 30; i++) { w.Step(); y.Step(); }
        check(w.ComputeChecksum() == y.ComputeChecksum(), "고정: 불러온 월드가 30틱 뒤에도 같은 체크섬");
        check(Snapshot.FormatVersion >= 30, $"Snapshot.FormatVersion {Snapshot.FormatVersion} (≥ 30)");
    }

    // ---------------------------------------------------------------- 분대 아이콘 '고정' (Facts.Pinned)

    static void TestSquadIcon()
    {
        var w = Bare();
        var co = Enumerable.Range(0, 8).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40 + i, 60))).ToList();
        co.Add(w.SpawnUnit(defs.Units["officer"], 1, new Vector2(40, 62)));
        w.FormCompany(1, co);
        w.Step();
        var q = w.Companies.First(c => c.Owner == 1).Squads[0];
        var f0 = SquadIcons.Read(w, q, false);
        foreach (var u in q.Members) u.Pinned = true;
        var f1 = SquadIcons.Read(w, q, false);
        foreach (var u in q.Members) u.Pinned = false;
        check(!f0.Pinned && f1.Pinned && SquadIcons.Pick(f1) == SquadIcons.Status.Pinned, $"분대 아이콘: 분대원 절반 이상 고정이면 '고정' ({f0.Pinned} → {f1.Pinned}, {SquadIcons.Pick(f1)})");
    }

    // ---------------------------------------------------------------- supp 2026-10-20: 서서 사격받는 병사 엎드림

    static void TestProneStanding()
    {
        using var po = new ProneOnScope();
        check(C.ProneAt > C.ProneRelease && C.ProneRelease > 0 && C.ProneAt < C.PinAt, $"엎드림 기준 {C.ProneRelease} < {C.ProneAt} < 고정 {C.PinAt}");
        var w = Bare();
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 60));
        var mover = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40, 70));
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(30, 60));
        w.Step();
        w.Move(1, new[] { mover }, new Vector2(40, 100));
        for (int i = 0; i < Ticks(1.5f); i++) w.Step();
        int at = (int)MathF.Ceiling(C.ProneAt * C.SuppressMax), rel = (int)MathF.Ceiling(C.ProneRelease * C.SuppressMax);
        var seq = new List<bool>();
        foreach (int sv in new[] { at - 5, at, (at + rel) / 2, rel - 1 }) { u.Suppression = (short)sv; w.UpdateProneForTest(u); seq.Add(u.Prone); }
        bool hyst = seq.SequenceEqual(new[] { false, true, true, false });
        check(hyst && !u.Pinned, $"엎드림 이력 (서 있는 병사, 고정 아님): {string.Join("/", seq.Select(x => x ? "엎드림" : "-"))} (≥ {C.ProneAt} 엎드림, < {C.ProneRelease} 일어섬)");
        mover.Suppression = (short)(at + 20); off.Suppression = (short)C.SuppressMax; w.Step();
        check(mover.Walking && !mover.Prone && !off.Prone, $"걷는 병사 · 장교는 엎드리지 않음 (걷는 중 {mover.Walking} 엎드림 {mover.Prone}, 장교 {off.Prone})");
        var a = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(40.5f, 48.5f));
        u.Suppression = (short)(at + 20); w.UpdateProneForTest(u);
        u.Prone = false; float h0 = w.HitChance(a, u); u.Prone = true; float h1 = w.HitChance(a, u);
        check(MathF.Abs(h1 / h0 - C.ProneHitMul) < 1e-3f, $"엎드린 (고정 아님) 표적 명중률 × {C.ProneHitMul} ({h0:0.0000} → {h1:0.0000})");
    }

    // ---------------------------------------------------------------- 엄폐 안에서 빨리 회복

    static void TestCoverDecay()
    {
        float cdm = C.CoverDecayMul; if (cdm <= 1f) C.CoverDecayMul = 1.25f;   // 스위치 (기본 1 = 끔) 를 제안값으로
        try {
        var w = Bare(paint: m => m.Set(60, 60, TerrainType.Trench));
        var open = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40.5f, 60.5f));
        var tr = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60.5f, 60.5f));
        w.Step();
        open.Suppression = tr.Suppression = 800;
        for (int i = 0; i < Ticks(10); i++) w.Step();
        int dOpen = 800 - open.Suppression, dTr = 800 - tr.Suppression;
        float r = dTr / (float)Math.Max(1, dOpen);
        check(C.CoverDecayMul > 1f && r > 1.05f && r < C.CoverDecayMul + 0.1f, $"엄폐 (참호) 안 제압 감소 ×{C.CoverDecayMul} (15틱 정수 반올림): 10초 동안 개활지 −{dOpen} · 참호 −{dTr} (비 {r:0.00})");
        } finally { C.CoverDecayMul = cdm; }
    }

    // ---------------------------------------------------------------- 붕괴 확률 (사기 배수)

    static void TestBreakChance()
    {
        using var bo = new BreakOnScope();
        var w = Bare(paint: m => m.Set(70, 60, TerrainType.Trench));
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40.5f, 60.5f));
        var tr = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(70.5f, 60.5f));
        w.Step();
        float P(Unit x, float sv) { x.Suppression = (short)(sv * C.SuppressMax); return w.BreakChanceOf(x); }
        float below = P(u, C.BreakAt - 0.02f), mid = P(u, (C.BreakAt + 1f) / 2), top = P(u, 1f);
        check(C.BreakChance > 0 && below == 0 && mid > 0 && top > mid, $"붕괴 확률: 제압 {C.BreakAt - 0.02f:0.00} → 0, 중간 {mid:0.000}, 1.0 → {top:0.000}/{C.BreakInterval}초");
        int r0 = u.Rank; short k0 = u.StartKills; float pr0 = P(u, 1f); u.StartKills = 48; int r6 = u.Rank; float pr6 = P(u, 1f); u.StartKills = k0;
        float cov = P(tr, 1f);
        var off = w.SpawnUnit(defs.Units["officer"], 1, new Vector2(42.5f, 60.5f));
        w.Step();
        float near = P(u, 1f);
        off.Hp = 0; w.Step();
        check(pr6 < pr0 && MathF.Abs(cov / pr0 - C.BreakCoverMul) < 1e-3f && MathF.Abs(near / pr0 - C.BreakOfficerMul) < 1e-3f,
            $"붕괴 사기 배수: 계급 {r0} {pr0:0.000} → {r6} {pr6:0.000}, 참호 ×{cov / pr0:0.00} (기대 {C.BreakCoverMul}), 장교 근처 ×{near / pr0:0.00} (기대 {C.BreakOfficerMul})");
        // 중대 사상률
        var w2 = Bare();
        var co = Enumerable.Range(0, 10).Select(i => w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40 + i, 60))).ToList();
        w2.FormCompany(1, co); w2.Step();
        float c0 = P2(w2, co[0]);
        for (int i = 5; i < 10; i++) co[i].Hp = 0;
        for (int i = 0; i < 3; i++) w2.Step();
        float c5 = P2(w2, co[0]);
        static float P2(World ww, Unit x) { x.Suppression = (short)ww.Defs.CombatRules.SuppressMax; return ww.BreakChanceOf(x); }
        check(c5 > c0 * 1.5f, $"붕괴: 중대 사상 50% 면 확률 ↑ ({c0:0.000} → {c5:0.000}, 가중 {C.BreakCasualtyWeight})");
    }

    // ---------------------------------------------------------------- 붕괴 실전: 달아나고 · 쏘지 않고 · 사격이 그치면 회복

    static void TestBreakLive()
    {
        using var bo = new BreakOnScope();
        var w = Bare();
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60, 60));
        var e = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 46));
        w.Step();
        w.Hold(2, new[] { e });
        long broke = -1; Vector2 p0 = u.Pos; int shotsBroken = 0;
        for (int i = 0; i < Ticks(30) && broke < 0; i++)
        {
            u.Suppression = (short)C.SuppressMax; u.Hp = u.Def.Hp; e.Hp = e.Def.Hp;
            w.Step();
            if (u.Broken) { broke = w.Tick; p0 = u.Pos; }
        }
        check(broke > 0 && Vector2.Distance(u.BreakTo, u.Pos) > C.BreakDistance * 0.8f && u.BreakTo.Y > u.Pos.Y + C.BreakDistance * 0.8f,
            $"붕괴: 제압 상한 · 고정 병사가 {broke * Config.SimDt:0.0}초 안에 무너짐, 도주 목표 = 적 반대쪽 ({u.BreakTo.X:0.0},{u.BreakTo.Y:0.0})");
        for (int i = 0; i < Ticks(4); i++)
        {
            u.Suppression = (short)C.SuppressMax; u.Hp = u.Def.Hp; e.Hp = e.Def.Hp;
            w.Step();
            foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == u.Id) shotsBroken++;
        }
        float ran = u.Pos.Y - p0.Y;
        check(u.Broken && ran > 2f && shotsBroken == 0, $"붕괴 중: 적 반대쪽으로 달아남 ({ran:0.0}칸 / 4초), 사격 {shotsBroken}발");
        e.Hp = 0;
        long rally = -1;
        for (int i = 0; i < Ticks(60) && rally < 0; i++) { w.Step(); if (!u.Broken) rally = w.Tick; }
        float rs = (rally - broke) * Config.SimDt;
        check(rally > 0 && rs >= C.BreakMinSec && u.Suppression < C.BreakRallyAt * C.SuppressMax + 1, $"붕괴 회복: 사격이 그치면 {rs:0.0}초 뒤 (≥ {C.BreakMinSec}초, 제압 < {C.BreakRallyAt})");
        // 결정성: 같은 시나리오 두 번 = 같은 붕괴 틱
        long Again()
        {
            var w2 = Bare();
            var u2 = w2.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60, 60));
            var e2 = w2.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 46));
            w2.Step(); w2.Hold(2, new[] { e2 });
            for (int i = 0; i < Ticks(30); i++) { u2.Suppression = (short)C.SuppressMax; u2.Hp = u2.Def.Hp; e2.Hp = e2.Def.Hp; w2.Step(); if (u2.Broken) return w2.Tick; }
            return -1;
        }
        check(Again() == broke, "붕괴 결정성: 같은 입력 = 같은 틱");
        // 거치 화기 사수는 무너지지 않음
        var w3 = Bare();
        var mg = w3.SpawnUnit(defs.Units["heavy_machinegunner"], 1, new Vector2(60, 60));
        w3.Step();
        bool mgBroke = false;
        for (int i = 0; i < Ticks(20); i++) { mg.Suppression = (short)C.SuppressMax; w3.Step(); mgBroke |= mg.Broken; }
        check(!mgBroke && mg.Pinned, "붕괴: 거치 화기 (기관총) 사수는 고정만, 무너지지 않음");
    }

    // ---------------------------------------------------------------- supp2: 달아날 길이 물 (강 · 다리) 에 막힘 = 물가 앞에서 끊고 덜 무너짐

    static void TestBreakCornered()
    {
        using var bo = new BreakOnScope();
        var w = Bare(paint: m => { for (int x = 0; x < 120; x++) for (int y = 63; y <= 66; y++) m.Set(x, y, TerrainType.Water); });   // 병사 (y 60) 뒤 3칸에 강
        Vector2 to = w.BreakEscapeForTest(new Vector2(60, 60), new Vector2(60, 70), out bool cornered);
        Vector2 free = w.BreakEscapeForTest(new Vector2(60, 60), new Vector2(60, 50), out bool c2);
        check(cornered && to.Y < 62f && !c2 && free == new Vector2(60, 50), $"붕괴 도주 길: 뒤가 강이면 물가 앞 ({to.Y:0.0}) 에서 끊김 (배수진), 반대쪽은 그대로");
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60, 60));
        var e = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(60, 46));
        w.Step(); w.Hold(2, new[] { e });
        float maxY = u.Pos.Y; bool broke = false;
        for (int i = 0; i < Ticks(60); i++)
        {
            u.Suppression = (short)C.SuppressMax; u.Hp = u.Def.Hp; e.Hp = e.Def.Hp;
            w.Step();
            broke |= u.Broken; maxY = MathF.Max(maxY, u.Pos.Y);
        }
        check(broke && maxY < 62.5f, $"교두보 · 배수진: 무너져도 강을 건너 달아나지 않음 (최대 y {maxY:0.0}, 강 63~66)");
    }

    static void TestBreakRetreatOrder()
    {
        using var bo = new BreakOnScope();
        var w = Bare();
        var u = w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(60, 60));
        w.Step();
        u.Suppression = (short)C.SuppressMax; w.Step();
        u.BrokenUntil = w.Tick + Ticks(30); u.BrokenAt = w.Tick; u.BreakTo = new Vector2(60, 80);
        w.Retreat(1, new[] { u }, new Vector2(20, 60));
        var p = u.Pos;
        for (int i = 0; i < Ticks(3); i++) { u.Suppression = (short)C.SuppressMax; w.Step(); }
        check(u.Pos.X < p.X - 1f, $"붕괴 중 후퇴 명령은 따른다 (x {p.X:0.0} → {u.Pos.X:0.0})");
        u.BrokenAt = w.Tick - Ticks(C.BreakMaxSec) - 1; w.Step();
        check(!u.Broken, $"붕괴 상한 {C.BreakMaxSec}초 지나면 무조건 회복");
    }

    static void TestBreakSnapshot()
    {
        using var po = new ProneOnScope();
        var w = Bare();
        var us = Enumerable.Range(0, 3).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(40 + i, 60))).ToList();
        var e = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(41, 45));
        w.Step();
        us[0].Suppression = 450; us[1].Suppression = (short)C.SuppressMax;
        us[1].BrokenUntil = w.Tick + 100; us[1].BrokenAt = w.Tick; us[1].BreakTo = new Vector2(41, 75);
        for (int i = 0; i < Ticks(1); i++) w.Step();
        var y = Snapshot.Load(Snapshot.Save(w, Meta), defs).World;
        bool same = y.Units.All(u => w.Find(u.Id) is Unit o && o.Prone == u.Prone && o.BrokenUntil == u.BrokenUntil && o.BreakTo == u.BreakTo && o.BrokenAt == u.BrokenAt) && y.ComputeChecksum() == w.ComputeChecksum();
        check(same && us[0].Prone && us[1].Broken, $"엎드림 · 붕괴: 저장 · 불러오기 · 체크섬 일치 (엎드림 {us[0].Prone}, 붕괴 {us[1].Broken})");
        var h0 = w.ComputeChecksum(); us[2].Prone = !us[2].Prone; var h1 = w.ComputeChecksum(); us[2].Prone = !us[2].Prone;
        us[2].BrokenUntil = 5; var h2 = w.ComputeChecksum(); us[2].BrokenUntil = 0;
        check(h0 != h1 && h0 != h2, "엎드림 · 붕괴: 체크섬에 들어감");
        for (int i = 0; i < 60; i++) { w.Step(); y.Step(); }
        check(w.ComputeChecksum() == y.ComputeChecksum(), "엎드림 · 붕괴: 불러온 월드가 60틱 뒤에도 같은 체크섬");
        check(Snapshot.FormatVersion >= 41, $"Snapshot.FormatVersion {Snapshot.FormatVersion} (≥ 41)");
    }

    // ---------------------------------------------------------------- [측정] 출처 계수 (읽기 전용 — 시뮬 불변)

    static void TestSourceCounters()
    {
        var w = Bare();
        var sq = Enumerable.Range(0, 6).Select(i => w.SpawnUnit(defs.Units["infantry"], 1, new Vector2(36 + i * 1.1f, 60))).ToList();
        var mg = w.SpawnUnit(defs.Units["heavy_maxim"], 2, new Vector2(40, 44));
        var rf = w.SpawnUnit(defs.Units["infantry"], 2, new Vector2(30, 46));
        w.Step();
        w.Hold(1, sq); w.Hold(2, new[] { mg, rf });
        for (int i = 0; i < Ticks(15); i++) { foreach (var u in sq) u.Hp = u.Def.Hp; mg.Hp = mg.Def.Hp; rf.Hp = rf.Def.Hp; mg.Suppression = rf.Suppression = 0; w.Step(); }
        var st = w.Stats; int K = SupSrc.Count;
        long spray = st.SuppBy[1 * K + SupSrc.Spray], near = st.SuppBy[1 * K + SupSrc.NearMiss], direct = st.SuppBy[1 * K + SupSrc.Direct];
        check(spray > 0 && near > 0 && st.ProneTicks[1] > 0 && st.PinTicksF[1] > 0 && st.SuppBy[2 * K + SupSrc.Spray] == 0,
            $"[제압2] 계수: 세력 1 받은 제압 흩뿌림 {spray} · 근접 {near} · 직접 {direct}, 엎드림 {st.ProneTicks[1] * Config.SimDt:0}병-초 · 고정 {st.PinTicksF[1] * Config.SimDt:0}병-초, 붕괴 {st.Breaks[1]}");
    }
}
