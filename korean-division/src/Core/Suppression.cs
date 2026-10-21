using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 제압 재설계 (N25, 사용자 결정 2026-10-19 — docs/BATTALION_CAMPAIGN_2026-10-19.md '제압 재설계 결과'):
///  · 근접 탄착 제압: 빗나간 탄은 사선 (착탄점 앞 nearMissPathLen 칸 + 너머 nearMissOvershoot 칸) 에서 nearMissRadius 칸 안 적에게
///    무기 suppress × nearMissFrac × (1 − 거리/반경) (저항 · 엄폐 적용). 폭발 (포 · 수류탄) · 기관총 흩뿌림은 반경 안 전원 그대로 + 그 바깥 nearMissRadius 띠.
///  · 제압 감소 = (suppressDecay + suppressDecayProp × 제압) / 초 × 계급 · 장교 · 부사관 배수 (비례 항), 가산 포화 × (1 − suppressGainSat × 제압비) — 지속 사격에서 상한에 붙지 않고 평형.
///  · 명중률 × (1 − suppressAccMul × 제압비) (사격자), 고정 (엎드림) 사격자 × pinnedAccMul, 고정 표적은 엄폐 배수 = min(지형 엄폐, proneHitMul).
///  · 고정 (Pinned): 제압 ≥ pinAt 이면 엎드려 고정, &lt; pinRelease 면 풀림 (이력). 고정 중에는 후퇴 명령 (IsFleeing) 이 아니면 움직이지 않고 제자리 사수, 돌격 끝.
///    대상 = 도보 보병 (기병 · 차량 · 포 · 제압 면역 장교 제외).
/// 모든 판정은 틱 · 상태의 함수 (해시 난수 없음 — 결정적). 저장 = CMB2 유닛마다 Pinned (FormatVersion 30), 체크섬 HashCombat3.
/// </summary>
public sealed partial class Unit
{
    /// <summary>고정 (엎드림): 제압 ≥ combat.pinAt 이면 켜지고 &lt; combat.pinRelease 면 꺼진다. 후퇴 명령이 아니면 이동하지 않는다.</summary>
    [SnapExt] public bool Pinned { get; internal set; }
    /// <summary>엎드림 (supp 2026-10-20): 고정이거나, 서 있는 병사의 제압비 ≥ combat.proneAt (이력 proneRelease). 피격 = min(지형, proneHitMul). 매 틱 UpdateProne — 저장 · 체크섬 (같은 틱 앞서 쏜 유닛이 읽음).</summary>
    [SnapExt] public bool Prone { get; internal set; }
    /// <summary>pronemove: 개별 이동 명령을 받은 엎드린 병사가 일어서기를 마치는 틱 (0 = 아님, Suppression.cs UpdateProne). FormatVersion 45.</summary>
    [SnapExt] public long StandUntil { get; internal set; }
    /// <summary>pronemove 상한: 대열 이동 중 엎드려 버티기 시작한 틱 (0 = 아님). combat.proneHoldMaxSec (돌격 · 강행 = proneHoldMaxAssaultSec) 이 지나면 따라간다. FormatVersion 47.</summary>
    [SnapExt] public long ProneHoldSince { get; internal set; }
    /// <summary>pronemove 도하 면제: 전술 AI 도하 강행 속보 명령 (QuickStepCommand.Cross) 을 받은 병사 (QuickStep 인 동안만 뜻이 있음). FormatVersion 48.</summary>
    [SnapExt] public bool ForceCross { get; internal set; }
    /// <summary>붕괴 (supp): 0 = 아님, 아니면 회복 판정을 시작하는 틱 (붕괴 + breakMinSec). 붕괴 중에는 BreakTo 로 달아나고 쏘지 않는다.</summary>
    [SnapExt] public long BrokenUntil { get; internal set; }
    /// <summary>붕괴 도주 목표 (붕괴 순간 가장 가까운 적 반대쪽 breakDistance 칸).</summary>
    [SnapExt] public Vector2 BreakTo { get; internal set; }
    /// <summary>붕괴 시작 틱 (breakMaxSec 상한용).</summary>
    [SnapExt] public long BrokenAt { get; internal set; }
    public bool Broken => BrokenUntil > 0;
    /// <summary>[N68] 기수가 근처에서 쓰러진 뒤 이 틱까지 붕괴 · 후퇴 확률 증가 (0 = 없음). 저장 · 체크섬.</summary>
    [SnapExt] public long BearerFellUntil { get; internal set; }
}

public sealed partial class World
{
    readonly List<Unit> nearMissBuf = new();

    /// <summary>고정 대상: 도보 보병 (기병 · 차량 · 포 제외), 제압을 받는 유닛 (저항 0 = 장교 제외).</summary>
    public static bool CanPin(Unit u) => u.Def.IsInfantry && (!u.Def.Mounted || u.Dismounted) && u.Def.SuppressResist > 0;

    /// <summary>제압 엄폐 배수: 지형 엄폐와 (사격자 방향을 알면) 소품 엄폐 중 좋은 쪽.</summary>
    internal float SuppressCoverMul(Unit t, Vector2? from)
    {
        float m = CoverSuppressMul(t);
        if (from is { } f && Props.Count > 0 && PropDefs.Current.Rules.Enabled && PropDefs.Current.Rules.SuppressCover)
        {
            float pc = PropCover(t.Pos, f);
            if (pc < m) m = pc;
        }
        return m;
    }

    /// <summary>제압 가산 1회 (저항 · 엄폐 적용, 반올림 후 상한). baseGain = 무기 suppress × 배율.</summary>
    void AddSuppression(Unit t, float baseGain, bool ignoreCover = false, int src = SupSrc.NearMiss, Vector2? from = null)
    {
        if (!t.Alive || baseGain <= 0) return;
        float gain = baseGain * t.Def.SuppressResist * (ignoreCover ? 1f : SuppressCoverMul(t, from));
        int max = Defs.CombatRules.SuppressMax;
        float sat = Defs.CombatRules.SuppressGainSat;   // 포화: 이미 눌린 병사일수록 덜 쌓인다 × (1 − sat × 제압비) — 상한에 붙지 않음
        if (sat > 0) gain *= MathF.Max(0f, 1f - sat * t.Suppression / (float)max);
        if (gain <= 0) return;
        int v = t.Suppression + (int)MathF.Round(gain);
        int old = t.Suppression;
        t.Suppression = (short)(v > max ? max : v);
        Stats.AddSupp(t.Owner, src, t.Suppression - old);   // [측정] 출처별 제압 (MoveStats)
    }

    /// <summary>점 p 에서 선분 ab 까지 거리.</summary>
    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l2 = ab.LengthSquared();
        float t = l2 > 1e-8f ? Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>
    /// 빗나간 직사탄의 근접 제압: 사선 (from → impact) 의 착탄점 앞 nearMissPathLen 칸 + 너머 nearMissOvershoot 칸 구간에서
    /// nearMissRadius 칸 안 적 (가장자리 거리) 에게 suppress × nearMissFrac × (1 − d/R). skipId = 직접 제압을 받은 표적,
    /// skipR ≥ 0 이면 (skipC, skipR) 원 안 (이미 흩뿌림 반경 제압을 받은 유닛) 은 뺀다. 공간 해시 한 번 조회.
    /// </summary>
    void NearMissPath(int owner, Vector2 from, Vector2 impact, float suppress, int skipId, Vector2 skipC = default, float skipR = -1f)
    {
        var c = Defs.CombatRules;
        float R = c.NearMissRadius, frac = c.NearMissFrac;
        if (suppress <= 0 || R <= 0 || frac <= 0) return;
        Vector2 d = impact - from;
        float len = d.Length();
        if (len < 1e-4f) return;
        Vector2 dir = d / len;
        float back = MathF.Min(len, MathF.Max(0f, c.NearMissPathLen)), over = MathF.Max(0f, c.NearMissOvershoot);
        Vector2 a = impact - dir * back, b = impact + dir * over;
        float maxR = Defs.MaxUnitRadius;
        UnitsInRadius((a + b) * 0.5f, (back + over) * 0.5f + R + maxR, nearMissBuf);
        for (int i = 0; i < nearMissBuf.Count; i++)
        {
            var e = nearMissBuf[i];
            if (!e.Alive || e.Id == skipId || e.Owner == owner || !IsHostile(owner, e.Owner)) continue;
            if (skipR >= 0 && Vector2.Distance(skipC, e.Pos) - e.Radius <= skipR) continue;
            float dist = MathF.Max(0f, SegmentDistance(e.Pos, a, b) - e.Radius);
            if (dist >= R) continue;
            AddSuppression(e, suppress * frac * (1f - dist / R), false, SupSrc.NearMiss, from);
            Stats.NearMissHits++;
        }
        nearMissBuf.Clear();
    }

    /// <summary>
    /// 폭발 · 반경 제압의 바깥 띠: 착탄점에서 (가장자리 거리) innerR 초과 innerR + nearMissRadius 미만인 적에게 suppress × nearMissFrac × (1 − (d − innerR)/R).
    /// 반경 안 (innerR 이하) 은 호출자가 이미 전량 제압했다. skipId = 지정 표적 (중립일 수 있음 — 띠 제압은 적대만).
    /// </summary>
    void NearMissRing(int owner, Vector2 impact, float innerR, float suppress, bool ignoreCover = false)
    {
        var c = Defs.CombatRules;
        float R = c.NearMissRadius, frac = c.NearMissFrac;
        if (suppress <= 0 || R <= 0 || frac <= 0) return;
        UnitsInRadius(impact, innerR + R + Defs.MaxUnitRadius, nearMissBuf);
        for (int i = 0; i < nearMissBuf.Count; i++)
        {
            var e = nearMissBuf[i];
            if (!e.Alive || e.Owner == owner || !IsHostile(owner, e.Owner)) continue;
            float dist = MathF.Max(0f, Vector2.Distance(impact, e.Pos) - e.Radius) - innerR;
            if (dist <= 0 || dist >= R) continue;
            AddSuppression(e, suppress * frac * (1f - dist / R), ignoreCover);
            Stats.NearMissHits++;
        }
        nearMissBuf.Clear();
    }

    /// <summary>제압에 따른 사격자 명중률 배수: (1 − suppressAccMul × 제압비) × (고정이면 pinnedAccMul).</summary>
    public float SuppressAccuracyMul(Unit u)
    {
        var c = Defs.CombatRules;
        float m = 1f;
        if (u.Suppression > 0) m *= MathF.Max(0f, 1f - c.SuppressAccMul * (u.Suppression / (float)c.SuppressMax));
        if (u.Pinned) m *= c.PinnedAccMul;
        return m;
    }

    /// <summary>고정 상태 갱신 (매 틱, UpdateUnitCore 의 제압 감소 직후): 이력 pinAt / pinRelease. 고정되면 돌격을 그친다.</summary>
    void UpdatePinned(Unit u)
    {
        if (u.ChargeState != 0) ChargeBreakCheck(u);
        if (!u.Pinned && u.Suppression <= 0) return;
        if (!CanPin(u)) { u.Pinned = false; return; }
        var c = Defs.CombatRules;
        if (!u.Pinned)
        {
            if (u.Suppression >= c.PinAt * c.SuppressMax) { u.Pinned = true; Stats.PinEvents++; }
        }
        else if (u.Suppression < c.PinRelease * c.SuppressMax) u.Pinned = false;
        if (u.Pinned && u.ChargeState != 0) u.ChargeState = 0;   // 엎드린 병사는 돌격하지 않는다 (명령 돌격 포함 — 표적이 있으면 그 자리에서 쏜다)
    }

    /// <summary>S4: 돌격 중 제압이 chargeBreakSuppress(기병은 Cav) 이상이면 돌격 중단 (유예 = ChargeRun). 새 상태 없음.</summary>
    void ChargeBreakCheck(Unit u)
    {
        var c = Defs.CombatRules;
        int thr = u.Def.Mounted ? c.ChargeBreakSuppressCav : c.ChargeBreakSuppress;
        if (thr <= 0 || u.Suppression < thr || u.ChargeRun * Config.SimDt < c.ChargeBreakGraceSec) return;
        u.ChargeState = 0; Stats.ChargesBroken++;
    }

    /// <summary>엎드림 갱신 (매 틱, UpdatePinned 다음): 고정 = 엎드림. 아니면 proneSettleSec 초 서 있었고 돌격 · 붕괴가 아닌 도보 보병이 제압비 ≥ proneAt 이면 엎드리고, &lt; proneRelease 이거나 움직이면 일어난다.</summary>
    void UpdateProne(Unit u)
    {
        var c = Defs.CombatRules;
        bool p;
        // pronemove 상한: 대열 이동 중 엎드려 버틴 시간 (엎드림 · 고정이 풀리거나 대열 이동이 아니면 지움)
        bool ex = CrossExempt(u);   // 도하 강행 = pronemove 규칙 면제 (옛 동작)
        if (ex || !(c.ProneHoldInFormation && FormationFollower(u)) || u.Charging || IsFleeing(u) || (!u.Prone && !u.Pinned)) u.ProneHoldSince = 0;
        else if (u.ProneHoldSince == 0) u.ProneHoldSince = Tick;
        if (u.Pinned) { p = !(IsFleeing(u) && c.CrawlSpeedMul > 0); u.StandUntil = 0; }   // pronemove: 후퇴 명령 = 고정 병사도 일어나 달아난다 (기어서 후퇴하면 추격에 잡힘 — tactical 곡사포 회귀)
        else if (c.ProneAt <= 0 || u.Suppression <= 0 || u.ChargeState != 0 || u.Broken || !CanPin(u)) p = false;
        else
        {
            bool still = Tick - u.LastMoveTick > (long)MathF.Round(c.ProneSettleSec / Config.SimDt);
            if (!ex && u.Prone && c.ProneHoldInFormation && FormationFollower(u)) still = true;   // pronemove: 대열 이동 명령이 와도 엎드린 채 (끌려가지 않음)
            float s = u.Suppression / (float)c.SuppressMax;
            p = still && s >= (u.Prone ? c.ProneRelease : c.ProneAt);
            if (p && !ex && u.Order is UnitOrder.Move && !FormationFollower(u) && !IsFleeing(u))   // 개별 이동 명령: proneStandSec 동안 일어서고 (제자리) 걷는다
            {
                if (u.StandUntil == 0) u.StandUntil = Tick + Math.Max(1, (long)MathF.Round(c.ProneStandSec / Config.SimDt));
                if (Tick >= u.StandUntil) { p = false; u.StandUntil = 0; u.LastMoveTick = Tick; }   // 일어섬 (움직이는 동안 다시 엎드리지 않음)
            }
            else u.StandUntil = 0;
        }
        if (p && !u.Pinned && ProneHoldReleased(u) && ProneAssault(u)) { p = false; u.LastMoveTick = Tick; }   // 돌격 · 강행 상한: (고정 아닌) 엎드린 병사는 일어나 대열을 따라 걷는다 — 고정 병사는 기어 따라간다 (고정 = 돌격하지 않음 규칙과 같음)
        if (!p) u.StandUntil = 0;
        u.Prone = p;
        if ((uint)u.Owner < MoveStats.MaxF)
        {
            if (p) Stats.ProneTicks[u.Owner]++;
            if (u.Pinned) Stats.PinTicksF[u.Owner]++;
            if (u.Broken) Stats.BrokenTicks[u.Owner]++;
        }
    }

    /// <summary>pronemove 도하 면제 (사용자 2026-10-21 "도하에는 엎드림 정지 면제 승인"): 전술 AI 도하 강행 (속보 + QuickStepCommand.Cross) 중인 병사는
    /// 대열 엎드림 정지 · 상한 · 개별 일어서기 · 기어가기를 받지 않는다 (pronemove 이전 동작 — 고정 병사의 제자리 사수 N25 는 그대로). combat.proneCrossExempt = false 면 끔.</summary>
    internal bool CrossExempt(Unit u) => u.ForceCross && u.QuickStep && Defs.CombatRules.ProneCrossExempt;

    /// <summary>pronemove: 중대 깃발 대형을 따라 걷는 (이동 · 공격 이동) 병사 — 개별 명령이 아니다.</summary>
    static bool FormationFollower(Unit u) => u.Company?.Anchor != null && u.Order is UnitOrder.Move or UnitOrder.AttackMove;

    /// <summary>pronemove 상한: 돌격 · 강행 대열 — 속보 이동 (도하 강행 등) 이거나 중대 참호 돌격 접근 · 돌파 단계.</summary>
    static bool ProneAssault(Unit u) => u.QuickStep || u.Company is { } c && c.TA.Stage is TaStage.Approach or TaStage.BreakIn;

    /// <summary>pronemove 상한: 대열 이동 중 엎드려 버틴 시간이 proneHoldMaxSec (돌격 · 강행 = proneHoldMaxAssaultSec) 을 넘었나 (0 = 상한 없음).</summary>
    bool ProneHoldReleased(Unit u)
    {
        if (u.ProneHoldSince == 0) return false;
        var c = Defs.CombatRules;
        float cap = ProneAssault(u) ? c.ProneHoldMaxAssaultSec : c.ProneHoldMaxSec;
        return cap > 0 && Tick - u.ProneHoldSince >= (long)MathF.Round(cap / Config.SimDt);
    }

    /// <summary>pronemove: 대열을 따라 기어가는 중 — 상한이 지나 엎드린 채 대열을 따라가는 병사 (UnitSpeed × crawlSpeedMul).</summary>
    internal bool ProneCrawlFollow(Unit u) => u.Prone && !u.Broken && Defs.CombatRules.ProneHoldInFormation && FormationFollower(u) && ProneHoldReleased(u);

    /// <summary>pronemove: 엎드린 (제압) 병사는 대열 이동 명령이 와도 따라가지 않고 제자리에서 쏜다 (상한 proneHoldMaxSec 까지). 개별 이동 명령으로 일어서는 동안도 제자리. 후퇴 · 붕괴 · 돌격은 예외.</summary>
    bool ProneMoveHold(Unit u, float dt)
    {
        var c = Defs.CombatRules;
        if (!u.Prone || u.Pinned || u.Charging || IsFleeing(u) || u.Order is UnitOrder.Hold or UnitOrder.GroundFire || CrossExempt(u)) return false;
        bool follower = c.ProneHoldInFormation && FormationFollower(u) && !ProneHoldReleased(u);
        bool standing = u.StandUntil != 0 && u.Order is UnitOrder.Move;
        if (!follower && !standing) return false;
        if (u.Group is { Big: true, Inited: false } bg && !ReferenceEquals(u.FormGroup, bg)) { bg.Members!.Add(u); u.FormGroup = bg; }
        if (u.Target == null && u.Weapon != null && u.Def.AutoAttack) TryAcquire(u, u.Weapon.Range + u.Radius);
        if (u.Target != null && !Engage(u, u.Target, dt, chase: false, leash: 0) && u.Order != UnitOrder.Attack) u.Target = null;
        return true;
    }

    /// <summary>붕괴 대상: 고정 대상 중 거치 화기 (기관총 사수 등) 아님.</summary>
    static bool CanBreak(Unit u) => CanPin(u) && !u.CanDeploy;

    /// <summary>
    /// 붕괴 굴림 · 회복 (매 틱, UpdatePinned 다음). 붕괴 중: breakMinSec 지난 뒤 제압비 &lt; breakRallyAt 이거나 breakMaxSec 지나면 회복 (명령 · 그룹은 그대로라 이어 감).
    /// 아니면 고정 + 제압비 ≥ breakAt 인 병사가 breakInterval 초마다 (Id 분산) 확률 굴림 (사기 배수 — BreakChanceOf).
    /// </summary>
    void UpdateBreak(Unit u)
    {
        var c = Defs.CombatRules;
        if (u.Broken)
        {
            bool maxed = c.BreakMaxSec > 0 && Tick - u.BrokenAt >= (long)MathF.Round(c.BreakMaxSec / Config.SimDt);
            if (!CanBreak(u) || maxed || (Tick >= u.BrokenUntil && u.Suppression < c.BreakRallyAt * c.SuppressMax))
            {
                u.BrokenUntil = 0; u.BrokenAt = 0; u.BreakTo = default;
                if ((uint)u.Owner < MoveStats.MaxF) Stats.Rallies[u.Owner]++;
            }
            return;
        }
        if (c.BreakChance <= 0 || !u.Pinned || u.Suppression < c.BreakAt * c.SuppressMax || u.ChargeState != 0 || IsFleeing(u) || !CanBreak(u)) return;
        int iv = Math.Max(1, (int)MathF.Round(c.BreakInterval / Config.SimDt));
        if ((Tick + u.Id) % iv != 0) return;
        float p0 = BreakChanceOf(u);
        if (p0 <= 0f) return;
        // 달아날 곳: 시야 안 가장 가까운 적 (거리 → Id 순, 해시 순서 무관) 의 반대쪽, 없으면 바라보는 반대쪽
        UnitsInRadius(u.Pos, u.Sight, nearMissBuf);
        Unit? foe = null; float best = float.MaxValue;
        for (int i = 0; i < nearMissBuf.Count; i++)
        {
            var e = nearMissBuf[i];
            if (!e.Alive || !IsHostile(u.Owner, e.Owner)) continue;
            float d = Vector2.DistanceSquared(e.Pos, u.Pos);
            if (d < best || (d == best && foe != null && e.Id < foe.Id)) { best = d; foe = e; }
        }
        nearMissBuf.Clear();
        Vector2 dir = foe != null && best > 1e-6f ? Vector2.Normalize(u.Pos - foe.Pos) : new Vector2(-DetMath.Cos(u.Facing), -DetMath.Sin(u.Facing));
        Vector2 to = u.Pos + dir * c.BreakDistance;
        to = new Vector2(Math.Clamp(to.X, 0.5f, Map.Width - 0.5f), Math.Clamp(to.Y, 0.5f, Map.Height - 0.5f));
        to = BreakEscape(u.Pos, to, out bool cornered);   // supp2: 물 (강 · 다리) 앞에서 끊음 — 교두보 병사가 다리를 건너 되돌아가지 않는다
        if (Roll(u.Id, 0x5B2E) >= p0 * (cornered ? c.BreakCorneredMul : 1f)) return;   // 배수진 (달아날 길이 물에 막힘) = 덜 무너짐
        u.BreakTo = to; u.BrokenAt = Tick;
        u.BrokenUntil = Tick + Math.Max(1, (long)MathF.Round(c.BreakMinSec / Config.SimDt));
        u.Target = null; u.ChargeState = 0; u.MarchTarget = 0;
        if ((uint)u.Owner < MoveStats.MaxF) Stats.Breaks[u.Owner]++;
    }

    /// <summary>
    /// supp2 붕괴 도주 길: from → to 직선을 0.5칸 간격으로 따라가며 물 칸 (또는 물에 닿은 칸 — 다리 · 강둑) 을 만나면 그 앞 표본에서 끊는다 (cornered = true).
    /// 처음부터 물가 (다리 위 · 교두보 강둑) 면 제자리. 지형의 순수 함수.
    /// </summary>
    Vector2 BreakEscape(Vector2 from, Vector2 to, out bool cornered)
    {
        cornered = false;
        Vector2 d = to - from;
        float len = d.Length();
        if (len < 1e-4f) return to;
        int n = (int)MathF.Ceiling(len / 0.5f);
        Vector2 last = from;
        for (int i = 1; i <= n; i++)
        {
            Vector2 q = from + d * (i / (float)n);
            if (NearWater(q)) { cornered = true; return last; }
            last = q;
        }
        return to;
    }

    bool NearWater(Vector2 q)
    {
        var (tx, ty) = GameMap.TileOf(q);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (Map.InBounds(tx + dx, ty + dy) && Map.Get(tx + dx, ty + dy) == TerrainType.Water) return true;
        return false;
    }

    /// <summary>붕괴 확률 (한 번 굴림): breakChance × (s − breakAt)/(1 − breakAt) × max(0.2, 1 − 계급 × breakRankFactor) × (장교 근처 breakOfficerMul, 아니면 부사관 breakNcoMul) × (엄폐 breakCoverMul) × (1 + breakCasualtyWeight × 중대 사상률).</summary>
    public float BreakChanceOf(Unit u)
    {
        var c = Defs.CombatRules;
        float s = u.Suppression / (float)c.SuppressMax;
        if (c.BreakChance <= 0 || s < c.BreakAt) return 0f;
        float p = c.BreakChance * MathF.Min(1f, (s - c.BreakAt) / MathF.Max(1e-3f, 1f - c.BreakAt));
        p *= MathF.Max(0.2f, 1f - Math.Max(0, u.Rank) * c.BreakRankFactor);
        if (NearOfficer(u)) p *= c.BreakOfficerMul;
        else if (NcoLeads(u)) p *= c.BreakNcoMul;
        if (CoverSuppressMul(u) < 1f) p *= c.BreakCoverMul;
        p *= BearerMoraleMul(u, c.BearerBreakMul);   // N68 기수
        if (u.Company is { InitialStrength: > 0 } co) p *= 1f + c.BreakCasualtyWeight * Math.Clamp(1f - co.Members.Count / (float)co.InitialStrength, 0f, 1f);
        return p;
    }

    /// <summary>붕괴 중 (후퇴 명령이 아님): 쏘지 않고 BreakTo 로 걸어서 달아난다 (제압 감속 상쇄), 도착하면 엎드려 멈춤. 명령 · 그룹은 유지 → 회복하면 이어 감.</summary>
    bool BrokenFlee(Unit u, float dt)
    {
        if (!u.Broken || IsFleeing(u)) return false;
        if (u.Group is { Big: true, Inited: false } bg && !ReferenceEquals(u.FormGroup, bg)) { bg.Members!.Add(u); u.FormGroup = bg; }
        u.Target = null; u.MarchTarget = 0;
        if (Vector2.DistanceSquared(u.Pos, u.BreakTo) > 0.25f)
        {
            var c = Defs.CombatRules;
            float slow = 1f - c.SuppressMoveSlow * (u.Suppression / (float)c.SuppressMax);
            MoveToward(u, u.BreakTo, u.BreakTo, chase: true, dt, speedMul: slow > 0.05f ? 1f / slow : 1f);
        }
        return true;
    }

    /// <summary>고정 중 (후퇴 명령이 아님): 이동하지 않고 제자리에서 무기 사거리 안 적만 쏜다 (사수와 같음). 지정 공격 명령의 표적은 사거리 밖이어도 놓지 않는다.</summary>
    bool PinnedHold(Unit u, float dt)
    {
        if (!u.Pinned || IsFleeing(u) || u.Order is UnitOrder.Hold or UnitOrder.GroundFire) return false;
        if (u.Order is UnitOrder.Move && !u.Broken && Defs.CombatRules.CrawlSpeedMul > 0 && !CrossExempt(u) && !(Defs.CombatRules.ProneHoldInFormation && FormationFollower(u))) return false;   // pronemove: 개별 이동 명령 = 기어서 (UnitSpeed × crawlSpeedMul)
        if (Defs.CombatRules.ProneHoldInFormation && FormationFollower(u) && ProneHoldReleased(u) && !CrossExempt(u)) return false;   // pronemove 상한: 대열을 따라 기어감 (돌격 · 강행이면 일어나 걸음)
        if (u.Group is { Big: true, Inited: false } bg && !ReferenceEquals(u.FormGroup, bg)) { bg.Members!.Add(u); u.FormGroup = bg; }   // 큰 그룹 구성원 등록 (ReactDelay 경로와 같음)
        if (u.Target == null && u.Weapon != null && u.Def.AutoAttack) TryAcquire(u, u.Weapon.Range + u.Radius);
        if (u.Target != null && !Engage(u, u.Target, dt, chase: false, leash: 0) && u.Order != UnitOrder.Attack) u.Target = null;
        Stats.PinnedTicks++;
        return true;
    }
}

public sealed partial class World
{
    // [테스트] 내부 판정 직접 호출
    internal void NearMissPathForTest(int owner, Vector2 from, Vector2 impact, float suppress, int skipId, Vector2 skipC = default, float skipR = -1f) => NearMissPath(owner, from, impact, suppress, skipId, skipC, skipR);
    internal void NearMissRingForTest(int owner, Vector2 impact, float innerR, float suppress) => NearMissRing(owner, impact, innerR, suppress);
    internal void UpdatePinnedForTest(Unit u) => UpdatePinned(u);
    internal void UpdateBreakForTest(Unit u) => UpdateBreak(u);
    internal void UpdateProneForTest(Unit u) => UpdateProne(u);
    internal Vector2 BreakEscapeForTest(Vector2 from, Vector2 to, out bool cornered) => BreakEscape(from, to, out cornered);
}

/// <summary>[측정] 제압 출처 (MoveStats.AddSupp).</summary>
public static class SupSrc
{
    public const int Direct = 0, Spray = 1, NearMiss = 2, Blast = 3, Grenade = 4, Charge = 5, Count = 6;
    public static readonly string[] Names = { "직접", "흩뿌림", "근접", "폭발", "수류탄", "기병" };
}
