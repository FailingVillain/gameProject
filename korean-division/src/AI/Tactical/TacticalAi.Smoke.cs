using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// T32b 전술 AI 연막 수류탄 (사용자 2026-10-22: AI 연막 · 철조망 보류 해제, 기본 꺼짐 — 켜는 결정은 측정 뒤 사용자).
/// 공격 과제 소총 중대 (연막 보유 돌격병 ≥ breachAi.smokeMinMen) 가 (R1) 엄폐 없는 개활 구간을 보이는 적 사수 앞에서 건널 때, (R2) 돌격 직전 · 중에 사격을 받을 때
/// 적 방향 가로줄 (smokeSpacing 간격, 최대 smokeMaxPerCompany) 로 기존 연막 수류탄 명령 (ThrowSmokeCommand, 리플레이 "smokegrenade") 을 낸다.
/// (R3 참호 돌격 순서 안 연막 = World 쪽 TrenchAssault.cs TaSmokeHook — tactics.json trenchAssault.smokeHook + 명령 Ai 플래그.)
/// 결정성 (AF1): 시뮬은 Faction.IsAi 를 읽지 않는다 — AI 는 명령만 낸다. 새 저장 상태 없음: 쿨다운 · 동시 수 = 세계의 자기 연막 · 날아가는 연막 수류탄 (저장 · 체크섬 대상) 에서,
/// 최근 사상 = 생존 구성원의 LastDamagedTick (저장) 에서 끌어낸다. 켜고 끄기 = 프로필 tactics.smokeAi (측정용 겹침 "smoke_on").
/// </summary>
public sealed partial class TacticalAi
{
    readonly List<Unit> smHold = new();
    readonly List<(Unit U, Vector2 P)> smPlan = new();

    /// <summary>중대 판단 (CompanyThink, 참호 돌격 판단 뒤). 던져도 이 틱의 다른 판단은 그대로 이어진다 (연막 뒤를 따라 진격).</summary>
    void SmokeThink(World w, TacFaction s, TacGroup g, float range)
    {
        if (!s.Profile.Tactics.SmokeAi || g.Task != TacTask.Attack || g.CompanyId == 0) return;
        if (g.State is TacState.Idle or TacState.Fallback or TacState.Reserve or TacState.Defend || g.RotateOut || w.Tick < g.RegroupUntil) return;
        var B = w.Defs.Tactics.BreachAi;
        smHold.Clear();
        foreach (var u in mem) if (u.SmokeGrenades > 0 && !u.IsOfficer && !u.Surrendered) smHold.Add(u);
        if (smHold.Count == 0 || smHold.Count < B.SmokeMinMen) return;
        var e = Grid.Nearest(g.Center, TacGrid.Infantry);
        if (e == null) return;
        float fireR = range * B.SmokeFireRangeMul, gr = w.Defs.Grenade.Range;
        float dE = Vector2.Distance(e.Pos, g.Center);
        if (dE > fireR || dE > gr + B.SmokeStandoffMax + Frontage(g)) return;
        // 쿨다운 · 동시 수: 중대 둘레의 자기 연막 (피어 있는 것 + 날아가는 것)
        float nearR = Frontage(g) * 0.5f + B.SmokeStandoffMax + gr;
        int active = OwnSmokes(w, s.Faction, g.Center, nearR, w.Tick - Sec(B.SmokeCooldownSec), out bool recent);
        if (recent || active >= B.SmokeMaxPerCompany) return;
        // 사선이 열린 보이는 적 사수 (소총 사거리 × smokeFireRangeMul 안) ≥ smokeMinShooters
        int shooters = 0; float fr2 = fireR * fireR;
        foreach (var x in enemies)
        {
            if (!x.Alive || x.Surrendered || x.Def.IsArmored || x.Weapon == null || Vector2.DistanceSquared(x.Pos, g.Center) > fr2) continue;
            if (!w.Map.HasFiringLine(x.Pos, g.Center, x.Id, 0) || w.SmokeBlocks(x.Pos, g.Center)) continue;
            if (++shooters >= B.SmokeMinShooters) break;
        }
        if (shooters < B.SmokeMinShooters) return;

        var dir = SafeNorm(e.Pos - g.Center, s.Dir);
        bool assault = g.State is TacState.Assault or TacState.Charge;
        int rule = 0;
        if (g.State is TacState.Advance or TacState.Approach or TacState.Bound && OpenRun(w.Map, g.Center, e.Pos, B.SmokeCoverMin) >= B.SmokeOpenMinDist) rule = 1;
        else if ((assault || g.State is TacState.Engage or TacState.Bound or TacState.Approach)
                 && (ContactSuppression(w, mem) >= B.SmokeUnderFireSupp || RecentHitFrac(w, mem, Sec(B.SmokeUnderFireLossSec)) >= B.SmokeUnderFireLoss)) rule = 2;
        if (rule == 0) return;
        if (B.SmokeSelfBlockGuard && !assault && FiringFrac(w, mem, Sec(B.SmokeFiringSec)) >= B.SmokeSelfBlockFrac) return;   // 우리 사격선을 막는 투척 (Q3)

        // 투척 줄: 중대 축 (g.Center 기준 dir · perp) 에서 앞선 보유자 + (사거리 − 0.5), 적에서 [minL, standoffMax] 칸 안
        var perp = Perp(dir);
        float aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
        foreach (var u in smHold)
        {
            var r = u.Pos - g.Center;
            aMax = MathF.Max(aMax, Vector2.Dot(r, dir));
            float b = Vector2.Dot(r, perp); bMin = MathF.Min(bMin, b); bMax = MathF.Max(bMax, b);
        }
        float throwD = MathF.Max(1f, gr - 0.5f);
        float minL = rule == 1 ? B.SmokeStandoffMin : 2f;
        float along = aMax + throwD;
        if (dE - along > B.SmokeStandoffMax) return;   // 아직 멀다 (연막이 적 사선 앞 standoffMax 안에 서지 않음)
        along = MathF.Min(along, dE - minL);
        if (along < aMax - throwD) return;            // 적에 너무 붙음 (줄이 앞선 보유자 뒤 사거리 밖)
        int n = Math.Clamp((int)((bMax - bMin) / MathF.Max(1f, B.SmokeSpacing)) + 1, 1, B.SmokeMaxPerCompany - active);
        var c0 = g.Center + dir * along + perp * ((bMin + bMax) * 0.5f);
        SmokeLine.Plan(w.Map, smHold, c0, perp, n, B.SmokeSpacing, gr - 0.05f, smPlan);
        if (smPlan.Count == 0) return;
        foreach (var (u, p) in smPlan) w.ThrowSmokeAt(s.Faction, new[] { u }, p);
        if (rule == 1) w.Stats.AiSmokeOpen += smPlan.Count; else w.Stats.AiSmokeFire += smPlan.Count;
        s.Orders++;
    }

    /// <summary>center 둘레 radius 안의 자기 연막 수 (피어 있는 연막 + 날아가는 연막 수류탄). recent = since 틱 뒤에 피었거나 아직 날아가는 것이 있다.</summary>
    static int OwnSmokes(World w, int faction, Vector2 center, float radius, long since, out bool recent)
    {
        recent = false; int n = 0; float r2 = radius * radius;
        foreach (var c in w.Smokes)
            if (c.Owner == faction && Vector2.DistanceSquared(c.Pos, center) <= r2) { n++; if (c.StartTick >= since) recent = true; }
        foreach (var b in w.Blasts)
            if (b.Smoke && b.Owner == faction && Vector2.DistanceSquared(b.Pos, center) <= r2) { n++; recent = true; }
        return n;
    }

    /// <summary>a → b 직선 (정수 타일 브레즌햄) 위에서 엄폐 점수 (TacMap.TileCover) 가 coverMin 미만인 칸이 이어진 가장 긴 구간 (칸, 끝 칸 = 적 자리 제외).</summary>
    internal static int OpenRun(GameMap m, Vector2 a, Vector2 b, float coverMin)
    {
        int x0 = (int)a.X, y0 = (int)a.Y, x1 = (int)b.X, y1 = (int)b.Y;
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        int run = 0, best = 0;
        while (x0 != x1 || y0 != y1)
        {
            if (x0 >= 0 && y0 >= 0 && x0 < m.Width && y0 < m.Height && TacMap.TileCover(m, new Vector2(x0 + 0.5f, y0 + 0.5f)) < coverMin) { if (++run > best) best = run; }
            else run = 0;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
        return best;
    }

    /// <summary>장교 제외 보병 중 win 틱 안에 피해를 입은 생존자 비율 (최근 사상의 대용 — 죽은 병사는 목록에 없음, 새 저장 상태 없이).</summary>
    static float RecentHitFrac(World w, List<Unit> m, long win)
    {
        int n = 0, hit = 0;
        foreach (var u in m) { if (u.IsOfficer || !u.Def.IsInfantry) continue; n++; if (w.Tick - u.LastDamagedTick <= win) hit++; }
        return n > 0 ? hit / (float)n : 0f;
    }

    /// <summary>장교 제외 무장 병사 중 win 틱 안에 쏜 비율.</summary>
    static float FiringFrac(World w, List<Unit> m, long win)
    {
        int n = 0, f = 0;
        foreach (var u in m) { if (u.IsOfficer || u.Weapon == null) continue; n++; if (u.LastShotTick > 0 && w.Tick - u.LastShotTick <= win) f++; }
        return n > 0 ? f / (float)n : 0f;
    }
}

/// <summary>연막 줄 투척 계획 (AI R1 · R2 와 참호 돌격 smokeHook 공용, 순수 함수 — 목록 순서 · 타일 중심).</summary>
internal static class SmokeLine
{
    /// <summary>center 를 가운데로 perp 방향 spacing 간격 n 점 (타일 중심으로 맞춤). 점마다 아직 안 쓴 보유자 중 가장 가까운 (같으면 목록 앞) 사거리 안 병사 1명.</summary>
    public static void Plan(GameMap m, List<Unit> holders, Vector2 center, Vector2 perp, int n, float spacing, float range, List<(Unit U, Vector2 P)> into)
    {
        into.Clear();
        float r2 = range * range;
        for (int k = 0; k < n; k++)
        {
            var q = center + perp * ((k - (n - 1) * 0.5f) * spacing);
            var p = new Vector2(Math.Clamp((int)q.X, 1, m.Width - 2) + 0.5f, Math.Clamp((int)q.Y, 1, m.Height - 2) + 0.5f);
            Unit? best = null; float bd = float.MaxValue;
            foreach (var u in holders)
            {
                if (u.SmokeGrenades <= 0 || !u.Alive) continue;
                float d = Vector2.DistanceSquared(u.Pos, p);
                if (d > r2 || d >= bd) continue;
                bool used = false;
                foreach (var t in into) if (t.U == u) { used = true; break; }
                if (!used) { bd = d; best = u; }
            }
            if (best != null) into.Add((best, p));
        }
    }
}

/// <summary>tactics.json "breachAi" — 전술 AI 연막 · (T32c) 철조망 판단 수치 (초안, T32b 카드). 칸 = 지도 칸, 초 = 게임 초.</summary>
public sealed class BreachAiDef
{
    /// <summary>중대에 연막 보유 돌격병이 이 수 이상일 때만 (R1 · R2).</summary>
    public int SmokeMinMen { get; set; } = 8;
    /// <summary>R1: 중대 → 가장 가까운 적 직선의 엄폐 없는 칸이 이어진 길이 하한.</summary>
    public float SmokeOpenMinDist { get; set; } = 18f;
    /// <summary>연막 줄이 적에서 떨어진 거리 [min, max] (R1; R2 의 하한은 2칸). 사거리 grenade.range 는 그대로 (Q2) — 앞선 보유자에서 사거리 안.</summary>
    public float SmokeStandoffMin { get; set; } = 8f;
    public float SmokeStandoffMax { get; set; } = 20f;
    /// <summary>투척점 간격 (= 연막 반경 2.5 × 2).</summary>
    public float SmokeSpacing { get; set; } = 5f;
    /// <summary>중대 둘레 자기 연막 동시 수 상한.</summary>
    public int SmokeMaxPerCompany { get; set; } = 4;
    /// <summary>중대 둘레에 자기 연막이 핀 뒤 이 초 동안 다시 안 던짐.</summary>
    public float SmokeCooldownSec { get; set; } = 20f;
    /// <summary>R2: 중대 장교가 보는 제압 (ContactSuppression) ≥ 이 값, 또는 최근 smokeUnderFireLossSec 초 피격 비율 ≥ smokeUnderFireLoss.</summary>
    public float SmokeUnderFireSupp { get; set; } = 0.25f;
    public float SmokeUnderFireLossSec { get; set; } = 10f;
    public float SmokeUnderFireLoss { get; set; } = 0.05f;
    /// <summary>돌격 중이 아니면, 최근 smokeFiringSec 초 안에 쏜 병사 비율 ≥ smokeSelfBlockFrac 일 때 던지지 않음 (우리 사격선 막힘, Q3).</summary>
    public bool SmokeSelfBlockGuard { get; set; } = true;
    public float SmokeSelfBlockFrac { get; set; } = 0.4f;
    public float SmokeFiringSec { get; set; } = 3f;
    /// <summary>사선이 열린 보이는 적 사수 (소총 사거리 × smokeFireRangeMul 안) 하한.</summary>
    public float SmokeFireRangeMul { get; set; } = 1.3f;
    public int SmokeMinShooters { get; set; } = 3;
    /// <summary>엄폐 칸 = TacMap.TileCover ≥ 이 값 (숲 · 포탄 구덩이 0.4, 잔해 · 참호 1.0, 풀밭 0).</summary>
    public float SmokeCoverMin { get; set; } = 0.4f;
}
