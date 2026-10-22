using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 항복 (N79b, 사용자 사양 2026-10-20): 중대가 거의 소탕되고, 퇴각할 수 없고, 강한 제압이 계속될 때 아주 드물게 항복한다.
/// 항복한 병사 = 만세 포즈, 움직이지 않고 쏘지 않고 명령을 무시, 적의 표적이 아님, 포로 (손실) 로 계산.
/// 조건 (전부, 결정적 — 해시 난수는 마지막 확률 굴림에만):
///  1) 도보 보병 · 제압 대상 (CanPin) · 중대 소속, 중대 병력 &lt;= initial × (1 − surrenderCasualty) 이고 &lt;= surrenderMaxLeft 명.
///  2) 제압비 &gt;= surrenderSuppAt 가 surrenderSuppSec 초 연속 (SuppHighTicks).
///  3) 퇴각 불가 (<see cref="CannotRetreat"/>): 위협 반경 안 적이 있고, 적이 사방을 둘러쌌거나 (적 방향 벡터 합이 약함)
///     적 반대쪽 + 좌우 surrenderProbeDeg 세 방향이 모두 막힘 (물 · 지도 끝 · 못 걷는 칸 · 사선 위 surrenderBlockRadius 칸 안 적).
///  4) surrenderInterval 초마다 (Id 분산) surrenderChance 확률 — 플레이어 세력은 × surrenderPlayerMul.
/// AI 는 퇴각이 가능하면 (조건 3 불충족) 항복하지 않고 기존 철수 · 후퇴 논리를 따른다. 플레이어 병사는 이 규칙 때문에 후퇴하지 않는다 (명령 수행).
/// 저장 = CMB2 유닛마다 Surrendered · SuppHighTicks (FormatVersion 43), 체크섬 HashCombat3.
/// </summary>
public sealed partial class Unit
{
    /// <summary>항복함 (만세 포즈 · 제자리 · 사격 · 명령 무시 · 표적 아님).</summary>
    [SnapExt] public bool Surrendered { get; internal set; }
    /// <summary>제압비 ≥ surrenderSuppAt 가 연속된 틱 수.</summary>
    [SnapExt] public int SuppHighTicks { get; internal set; }
}

public sealed partial class World
{
    /// <summary>항복 판정 · 상태 갱신 (매 틱, UpdateBreak 다음).</summary>
    void UpdateSurrender(Unit u)
    {
        var c = Defs.CombatRules;
        bool ai = Faction(u.Owner).IsAi;
        float baseChance = ai && c.SurrenderAiChance >= 0 ? c.SurrenderAiChance : c.SurrenderChance;
        if (baseChance <= 0 && c.SurrenderChance <= 0) { u.SuppHighTicks = 0; return; }
        float suppAt = ai && c.SurrenderAiSuppAt >= 0 ? c.SurrenderAiSuppAt : c.SurrenderSuppAt;
        float suppSec = ai && c.SurrenderAiSuppSec >= 0 ? c.SurrenderAiSuppSec : c.SurrenderSuppSec;
        if (u.Suppression >= suppAt * c.SuppressMax) { if (u.SuppHighTicks < 1_000_000) u.SuppHighTicks++; }
        else u.SuppHighTicks = 0;
        if (u.SuppHighTicks < (int)MathF.Round(suppSec / Config.SimDt)) return;
        int iv = Math.Max(1, (int)MathF.Round(c.SurrenderInterval / Config.SimDt));
        if ((Tick + u.Id) % iv != 0) return;
        if (!SurrenderEligible(u)) return;
        if (!CannotRetreat(u)) return;   // AI 는 퇴각 가능하면 항복하지 않음 (기존 후퇴 · 철수가 먼저)
        float p = ai ? baseChance : c.SurrenderChance * c.SurrenderPlayerMul;
        if (Roll(u.Id, 0x51D3) >= p) return;
        Surrender(u);
    }

    /// <summary>자격: 도보 보병 · 중대 소속 · 중대가 거의 소탕됨.</summary>
    public bool SurrenderEligible(Unit u)
    {
        var c = Defs.CombatRules;
        if (u.Surrendered || !u.Alive || !CanPin(u) || u.Company is not { InitialStrength: > 0 } co) return false;
        int left = co.Members.Count;
        bool ai = Faction(u.Owner).IsAi;
        float cas = ai && c.SurrenderAiCasualty >= 0 ? c.SurrenderAiCasualty : c.SurrenderCasualty;
        int maxLeft = ai && c.SurrenderAiMaxLeft >= 0 ? c.SurrenderAiMaxLeft : c.SurrenderMaxLeft;
        return left <= co.InitialStrength * (1f - cas) && left <= maxLeft;
    }

    /// <summary>
    /// 퇴각 불가: 위협 반경 안 적이 있고 (없으면 false), (a) 적이 사방에 있어 방향 벡터 합 / 합의 크기 &lt; 0.35 (포위),
    /// 또는 (b) 적 반대쪽 · 좌우 surrenderProbeDeg 세 방향 직선 (surrenderProbeLen 칸) 이 전부 막힘 — 물 근처 · 못 걷는 칸 · 지도 끝 · 사선 위 적.
    /// </summary>
    public bool CannotRetreat(Unit u)
    {
        var c = Defs.CombatRules;
        float bm = Faction(u.Owner).IsAi ? c.SurrenderRetreatBlockedMul : 1f;   // T31c: AI 만 완화 (1 = 현행)
        UnitsInRadius(u.Pos, c.SurrenderThreatRadius, nearMissBuf);
        Vector2 sum = Vector2.Zero; float wsum = 0; int n = 0;
        for (int i = 0; i < nearMissBuf.Count; i++)
        {
            var e = nearMissBuf[i];
            if (!e.Alive || e.Surrendered || !IsHostile(u.Owner, e.Owner)) continue;
            Vector2 d = u.Pos - e.Pos; float len = d.Length();
            if (len < 1e-3f) continue;
            float wgt = 1f / MathF.Max(1f, len);
            sum += d / len * wgt; wsum += wgt; n++;
        }
        if (n == 0 || wsum <= 0) { nearMissBuf.Clear(); return false; }
        bool result;
        if (sum.Length() / wsum < MathF.Min(0.9f, 0.35f * bm)) result = true;   // 포위
        else
        {
            Vector2 away = Vector2.Normalize(sum);
            float a = c.SurrenderProbeDeg * MathF.PI / 180f;
            result = ProbeBlocked(u, away, bm) && ProbeBlocked(u, Rot(away, a), bm) && ProbeBlocked(u, Rot(away, -a), bm);
        }
        nearMissBuf.Clear();
        return result;
    }

    static Vector2 Rot(Vector2 v, float a) { float cs = DetMath.Cos(a), sn = DetMath.Sin(a); return new Vector2(v.X * cs - v.Y * sn, v.X * sn + v.Y * cs); }

    /// <summary>한 방향 퇴각로가 막혔나 (nearMissBuf 의 적 목록 사용).</summary>
    bool ProbeBlocked(Unit u, Vector2 dir, float mul = 1f)
    {
        var c = Defs.CombatRules;
        float br = c.SurrenderBlockRadius * mul, br2 = br * br;
        int steps = Math.Max(1, (int)MathF.Ceiling(c.SurrenderProbeLen * mul / 0.5f));
        for (int s = 1; s <= steps; s++)
        {
            Vector2 q = u.Pos + dir * (s * 0.5f);
            if (q.X < 0.5f || q.Y < 0.5f || q.X > Map.Width - 0.5f || q.Y > Map.Height - 0.5f) return true;
            var (tx, ty) = GameMap.TileOf(q);
            if (!Map.IsTerrainWalkable(tx, ty) || NearWater(q)) return true;
            for (int i = 0; i < nearMissBuf.Count; i++)
            {
                var e = nearMissBuf[i];
                if (!e.Alive || e.Surrendered || !IsHostile(u.Owner, e.Owner)) continue;
                if (Vector2.DistanceSquared(e.Pos, q) <= br2) return true;
            }
        }
        return false;
    }

    /// <summary>항복 실행: 모든 행동 상태를 지우고 그 자리에 선다.</summary>
    internal void Surrender(Unit u)
    {
        u.Surrendered = true;
        HoldSurrendered(u);
        u.Pinned = false; u.Prone = false; u.StandUntil = 0; u.ProneHoldSince = 0; u.BrokenUntil = 0; u.BrokenAt = 0; u.BreakTo = default; u.Retreating = false; u.QuickStep = false;
        if ((uint)u.Owner < MoveStats.MaxF) Stats.Surrenders[u.Owner]++;
    }

    /// <summary>항복 상태 유지 (매 틱): 이동 · 사격 · 명령 없음.</summary>
    void HoldSurrendered(Unit u)
    {
        u.Target = null; u.Order = UnitOrder.Idle; u.Group = null; u.ChargeState = 0; u.MarchTarget = 0; u.Velocity = Vector2.Zero; u.BurstLeft = 0;
    }

    internal void UpdateSurrenderForTest(Unit u) => UpdateSurrender(u);
    internal void SurrenderForTest(Unit u) => Surrender(u);
}
