using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// F1 접적 상태 · 사격 위치 (2026-10-20 사용자 승인 — "라인배틀이 아니라 1차대전 전투", docs/ISSUES_2026-10-20.md 1~3단계).
///  · 중대 접적 (StepContact, 15틱마다 중대 Id 분산): 구성원이 표적을 잡았거나 · contactTriggerSec 안에 쐈거나 · 총격을 받았으면 접적 (ContactTick 갱신).
///    마지막 트리거 뒤 contactDisengageSec 넘게 조용하면 이탈 (Disengage), contactReformSec 더 조용하면 비접적 — 그때 구성원의 구역 · 사격 위치를 지운다.
///  · contactLoose (기본 켬): 접적 · 이탈 중에는 장교 빈자리 점검 (Squads.cs) · 장교 대열 정비 판단 (CloseRanks.cs — 적 돌격 감지는 그대로) 을 하지 않고,
///    도착 깃발 구성원은 교전 중 (HoldsGround) 이면 슬롯 둘레 contactZoneRadius 칸 안에서 도착 처리 (OrgFormation.FollowAnchor), 대기 병사는 표적을 쫓지 않는다.
///  · 구역 = 느슨한 닻 (UpdateSpot, coverSeekTicks 틱마다 Id 분산): 대기 · 사수 보병은 구역 (Unit.ZoneCenter 둘레 contactZoneRadius) 안,
///    spotSearchRadius 칸 안에서 엄폐 (명중 배수) · 고지 (spotHeightGain) · 표적 사선이 좋은 칸을 고른다 — 지금 자리보다 coverSeekGain 이상 좋고
///    spotMoveMin 칸 넘게 떨어졌을 때만 걸어가고, 아니면 지금 자리를 사격 위치로 고정 (SpotTick).
///  · 사격 위치 고정: spotDwellSec 동안 · 표적이 있는 동안은 다시 고르지 않는다. 제압 spotReleaseSupp 이상 · 새 명령 (ResetOrder) · 구역 밖으로 밀려남이면 해제.
///    구역 밖이면 구역 안 (중심 쪽 60%) 으로 돌아간다 (ZoneReturns).
/// 상태 = Company.ContactTick · ContactSince, Unit.HasZone · ZoneCenter · ZoneTick · SpotTick (ORG_ 끝, FormatVersion 37). 모두 틱 · 입력의 함수 — 결정적.
/// </summary>
public enum ContactState : byte { None = 0, Contact = 1, Disengage = 2 }

public sealed partial class Unit
{
    /// <summary>F1: 접적 구역이 있는가 · 구역 중심 (슬롯 또는 도착 자리).</summary>
    [SnapExt] public bool HasZone { get; internal set; }
    [SnapExt] public Vector2 ZoneCenter { get; internal set; }
    /// <summary>F1: 구역을 정한 틱 — 그 뒤 명령 (LastOrderTick) 을 받았으면 구역은 낡음 (새 자리에서 다시 정함).</summary>
    [SnapExt] public long ZoneTick { get; internal set; }
    /// <summary>F1: 사격 위치를 고정한 틱 (-1 = 없음).</summary>
    [SnapExt] public long SpotTick { get; internal set; } = -1;
}

public sealed partial class World
{
    /// <summary>
    /// [읽기] 중대 접적 트리거 (F1): 살아 있는 구성원 중 하나라도 표적을 잡았거나 · contactTriggerSec 안에 쐈거나 · 그 안에 총격을 받았다 (빗나감 포함).
    /// 구성원 목록 순 — 결정적. 측정 (F1Obs) 도 같은 정의를 쓴다.
    /// </summary>
    public bool ContactTrigger(Company c)
    {
        long win = (long)MathF.Round(Defs.CombatRules.ContactTriggerSec / Config.SimDt);
        foreach (var u in c.Members)
        {
            if (!u.Alive) continue;
            if (u.Target != null) return true;
            if (u.LastShotTick > 0 && Tick - u.LastShotTick <= win) return true;
            if (u.LastFiredAtTick > 0 && Tick - u.LastFiredAtTick <= win) return true;
        }
        return false;
    }

    /// <summary>[읽기] 중대 접적 상태 (ContactTick 으로부터).</summary>
    public ContactState ContactOf(Company c)
    {
        var cr = Defs.CombatRules;
        long q = Tick - c.ContactTick;
        if (q <= (long)MathF.Round(cr.ContactDisengageSec / Config.SimDt)) return ContactState.Contact;
        if (q <= (long)MathF.Round((cr.ContactDisengageSec + cr.ContactReformSec) / Config.SimDt)) return ContactState.Disengage;
        return ContactState.None;
    }

    /// <summary>접적 · 이탈 중이고 contactLoose 가 켜져 있다 (대형 강박 끔).</summary>
    internal bool ContactLoose(Company? c) => c != null && Defs.CombatRules.ContactLoose && ContactOf(c) != ContactState.None;

    const int ContactPollTicks = 15;

    /// <summary>매 틱 (Step: StepCommand 뒤, StepGapChecks 앞). 중대마다 15틱 간격 (Id 분산).</summary>
    void StepContact()
    {
        for (int ci = 0; ci < Companies.Count; ci++)
        {
            var c = Companies[ci];
            if ((Tick + c.Id) % ContactPollTicks != 0) continue;
            var before = ContactOf(c);
            if (ContactTrigger(c))
            {
                if (before == ContactState.None) { c.ContactSince = Tick; Stats.ContactEnters++; }
                c.ContactTick = Tick;
            }
            else if (before == ContactState.None)
                foreach (var u in c.Members) if (u.HasZone || u.SpotTick >= 0) { u.HasZone = false; u.SpotTick = -1; }   // 비접적: 구역 · 사격 위치 지움 (다음 빈자리 점검이 대열을 맞춤)
        }
    }

    /// <summary>F1 (FollowAnchor 도착 깃발): 접적 중 교전하는 병사는 슬롯 둘레 구역 안이면 그 자리에서 도착 — 구역 중심 = 슬롯.</summary>
    /// <remarks>슬롯보다 정면 (dir) 쪽으로 spotMoveMin 칸 넘게 뒤에 있으면 도착하지 않고 슬롯 쪽으로 계속 걷는다 (전진 명령이 구역 반경만큼 모자라게 끝나지 않게).</remarks>
    internal bool ArriveInZone(Unit u, Vector2 slot, float sd, Vector2 dir = default)
    {
        if (!Defs.CombatRules.ContactArriveZone || !ContactLoose(u.Company) || u.HasTrenchSpot || u.IsOfficer || sd > Defs.CombatRules.ContactZoneRadius || !HoldsGround(u)) return false;
        if (dir != Vector2.Zero && Vector2.Dot(u.Pos - slot, dir) < -Defs.CombatRules.SpotMoveMin) return false;
        u.HasZone = true; u.ZoneCenter = slot; u.ZoneTick = Tick;
        return true;
    }

    /// <summary>
    /// F1: 대기 병사가 표적을 쫓아도 되는가. 접적 중에는 사거리 안 표적은 그 자리에서 쏘고 (다가가지 않음), 사거리 밖 (사거리 + 구역 반경 안) 표적만
    /// 구역 안에서 (중심에서 구역 반경 − 0.5 안일 때) 다가간다 — 구역을 벗어나 쫓지 않는다.
    /// </summary>
    internal bool MayChase(Unit u)
    {
        if (!ContactLoose(u.Company) || !Defs.CombatRules.ContactHoldChase) return true;
        if (!u.HasZone || u.Target is not { } t || u.Weapon is not { } w) return false;
        float R = Defs.CombatRules.ContactZoneRadius;
        float dist = t.EdgeDistance(u.Pos) - u.Radius;
        if (dist <= w.Range || dist > w.Range + R) return false;
        return Vector2.DistanceSquared(u.Pos, u.ZoneCenter) < (R - 0.5f) * (R - 0.5f);
    }

    /// <summary>F1: 사격 위치 고정 중 (spotDwellSec 안이거나 표적이 있음) — 위치 재선택으로 움직이지 않는다 (피격 엄폐 찾기 · 진지 점유는 그대로 — 끄면 도하 교두보 테스트가 깨짐).</summary>
    internal bool SpotCommitted(Unit u) =>
        u.SpotTick >= 0 && (u.Target != null || Tick - u.SpotTick < (long)MathF.Round(Defs.CombatRules.SpotDwellSec / Config.SimDt));

    /// <summary>F1 매 틱 유닛마다 (UpdateCoverSeek 뒤) — 실제 판단은 coverSeekTicks 틱마다 (Id 분산).</summary>
    void UpdateSpot(Unit u)
    {
        var cr = Defs.CombatRules;
        if (cr.CoverSeekTicks <= 0 || (Tick + u.Id) % cr.CoverSeekTicks != 0) return;
        if (!cr.SpotEnabled || !ContactLoose(u.Company) || u.IsOfficer || !CoverCapable(u)) return;
        if (u.Order is not (UnitOrder.Idle or UnitOrder.Hold) || u.Group != null || u.Charging || u.InTrench || u.TrenchSpot >= 0 || u.ReactDelay > 0 || u.Retreating) return;
        if (u.Velocity != Vector2.Zero) return;
        if (u.HasZone && u.LastOrderTick > u.ZoneTick) u.HasZone = false;   // 구역을 정한 뒤 받은 명령 (도하 · 이동 등) — 새 자리가 구역
        if (!u.HasZone) { u.HasZone = true; u.ZoneCenter = u.Pos; u.ZoneTick = Tick; }
        // 제압 → 고정 해제 (다시 고를 때 엄폐를 찾는다)
        if (u.SpotTick >= 0 && u.SuppressionRatio >= cr.SpotReleaseSupp) { u.SpotTick = -1; Stats.SpotReleases++; }
        float R = cr.ContactZoneRadius;
        Vector2 off = u.Pos - u.ZoneCenter;
        if (off.LengthSquared() > (R + 0.5f) * (R + 0.5f))
        {
            // 구역 밖으로 밀려남 → 구역 안 (중심 쪽) 으로
            var dest = u.ZoneCenter + Vector2.Normalize(off) * (R * 0.6f);
            if (u.SpotTick >= 0) Stats.SpotReleases++;
            SpotWalk(u, ClampToMap(dest));
            Stats.ZoneReturns++;
            return;
        }
        if (SpotCommitted(u)) return;
        float cur = SpotScore(u.Pos, u);
        if ((cr.SpotAttackCover || DefendsForCover(u)) && BestSpot(u, cur - cr.CoverSeekGain) is Vector2 d && Vector2.Distance(d, u.Pos) >= cr.SpotMoveMin)   // tweak1: 공격 자세는 엄폐로 옮기지 않고 그 자리 고정
        {
            SpotWalk(u, d);
            Stats.SpotMoves++;
            return;
        }
        u.SpotTick = Tick;   // 지금 자리를 사격 위치로
        Stats.SpotPicks++;
    }

    /// <summary>사격 위치 점수 (낮을수록 좋음): 명중 배수 − 고지 가산.</summary>
    float SpotScore(Vector2 q, Unit u) => CoverHitAt(q, u) - (Map.IsHigh(q) ? Defs.CombatRules.SpotHeightGain : 0f);

    /// <summary>구역 안 · spotSearchRadius 안에서 점수 ≤ maxScore 인 가장 좋은 칸 (점수 + 0.04 × 거리, 칸 순). 칸당 coverSeekPerTile 명, 표적이 있으면 사선이 열린 칸만.</summary>
    Vector2? BestSpot(Unit u, float maxScore)
    {
        var cr = Defs.CombatRules;
        float r = cr.SpotSearchRadius, R = cr.ContactZoneRadius;
        var (px, py) = GameMap.TileOf(u.Pos);
        int ri = (int)MathF.Ceiling(r);
        float best = float.MaxValue, r2 = r * r, R2 = R * R;
        Vector2? res = null;
        int cap = Math.Max(1, cr.CoverSeekPerTile);
        for (int dy = -ri; dy <= ri; dy++)
        for (int dx = -ri; dx <= ri; dx++)
        {
            int x = px + dx, y = py + dy;
            if (!Map.IsWalkable(x, y) || Map.IsDeepForest(x, y)) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            if (Map.IsWire(q)) continue;
            float dd = Vector2.DistanceSquared(q, u.Pos);
            if (dd > r2 || Vector2.DistanceSquared(q, u.ZoneCenter) > R2) continue;
            float s = SpotScore(q, u);
            if (s > maxScore) continue;
            float sc = s + 0.04f * MathF.Sqrt(dd);
            if (sc >= best) continue;
            if (TileCrowd(q, u) >= cap) continue;
            if (u.Target != null && !Map.HasFiringLine(q, u.Target.Pos, 0, 0)) continue;
            best = sc; res = q;
        }
        return res;
    }

    /// <summary>사격 위치 · 구역 복귀 걸음: 한 사람 이동, 사수였으면 도착 뒤 사수 (CoverHold). 구역은 유지.</summary>
    void SpotWalk(Unit u, Vector2 d)
    {
        bool hold = u.Order == UnitOrder.Hold;
        var zc = u.ZoneCenter; long zt = u.ZoneTick;
        ResetOrder(u);
        u.HasZone = true; u.ZoneCenter = zc; u.ZoneTick = zt;
        u.Group = new MoveGroup(this, d, 1);
        u.Order = UnitOrder.Move;
        u.CoverHold = hold;
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 (ORG_ 끝, WritePostures 뒤 — FormatVersion 37)

    void WriteContact(SnapWriter w)
    {
        foreach (var c in Companies) { w.L(c.ContactTick); w.L(c.ContactSince); w.Bool(c.KeepFormation); }   // KeepFormation: FormatVersion 38 (F1 2차)
        foreach (var u in Units) { w.Bool(u.HasZone); w.F(u.ZoneCenter.X); w.F(u.ZoneCenter.Y); w.L(u.ZoneTick); w.L(u.SpotTick); }
    }

    void ReadContact(SnapReader r)
    {
        foreach (var c in Companies) { c.ContactTick = r.L(); c.ContactSince = r.L(); c.KeepFormation = r.Bool(); }
        foreach (var u in Units)
        {
            u.HasZone = r.Bool();
            u.ZoneCenter = new Vector2(r.F(), r.F());
            if (!float.IsFinite(u.ZoneCenter.X) || !float.IsFinite(u.ZoneCenter.Y)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "zone centre"));
            u.ZoneTick = r.L();
            u.SpotTick = r.L();
            if (u.SpotTick < -1) throw new SnapshotException(string.Format(SnapMsg.Invalid, "spot tick"));
        }
    }

    void HashContact(ref Hasher h)
    {
        foreach (var c in Companies) { h.Add(c.ContactTick); h.Add(c.ContactSince); h.Add(c.KeepFormation ? 1 : 0); }
        foreach (var u in Units) { h.Add(u.HasZone ? 1 : 0); h.Add(u.ZoneCenter.X); h.Add(u.ZoneCenter.Y); h.Add(u.ZoneTick); h.Add(u.SpotTick); }
    }
}
