using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 엄폐 찾기 · 진지 점유 (engage, 2026-10-19 사용자 결정 2 · 방어 지형 활용).
///  · 엄폐 찾기 (UpdateCoverSeek, 매 틱 유닛마다 — 실제 판단은 combat.coverSeekTicks 틱에 한 번, Id 분산): 피격 중 (UnderFire) 인 대기 · 사수 보병
///    (참호 밖 · 자리 없음 · 이동 그룹 없음 · 돌격 아님 · 방열 무기 아님) 이 반경 coverSeekRadius 안의 빈 참호 자리 → 없으면 지금보다
///    명중 배수가 coverSeekGain 이상 좋은 엄폐 칸 (숲 가장자리 · 잔해 · 포탄 구덩이 · 가옥 옆, 깊은 숲 제외, 칸당 coverSeekPerTile 명) 으로 한 사람 이동.
///    사수였으면 도착 뒤 다시 사수 (Unit.CoverHold). 새 명령 (ResetOrder) 이면 취소.
///  · 진지 점유 (OccupyPositionsCommand — 전술 AI 방어 · 후퇴 · 예비): 지점 둘레 반경 안의 빈 참호 자리 → 엄폐 칸 자리를 점수
///    (거리 × (0.5 + 명중 배수) − 앞쪽 가산) 순으로 병사 수만큼 골라 (거리², Id, 자리) 탐욕 배정. 참호 자리는 맡아서 걸어가 들어가고 (사수),
///    엄폐 칸은 한 사람 이동 뒤 사수. 장교 · 남는 병사는 지점 뒤로 이동.
/// 모두 틱 · 입력의 함수 (유닛 목록 순 · 칸 순) — 결정적. 상태 = Unit.CoverHold (SUPL 참호 구역 끝, FormatVersion 28).
/// </summary>
/// <summary>중대 자세 (tweak1): 피격 엄폐 찾기를 할지. None = 정해지지 않음 (플레이어 — 사수 · 대기 병사, 단 공격 틀 행동 (Engage) 중이면 안 함), Attack = 공격 (안 함), Defend = 방어 · 예비 · 진지 (함).</summary>
public enum CompanyPosture : byte { None = 0, Attack = 1, Defend = 2 }

/// <summary>중대 자세 지정 (전술 AI 가 과제에 따라 냄 — 공격 / 방어 · 예비 · 후퇴). 리플레이 기록 (v13).</summary>
public sealed record SetPostureCommand(int Faction, int[] Companies, CompanyPosture Posture) : GameCommand(Faction);

public sealed partial class Unit
{
    /// <summary>엄폐 칸으로 가는 중 — 도착하면 사수 (엄폐 찾기 · 진지 점유). 새 명령이면 false.</summary>
    [SnapExt] public bool CoverHold { get; internal set; }
}

public sealed partial class World
{
    readonly List<Unit> coverBuf = new();

    /// <summary>엄폐 찾기 대상 보병인가 (방열 무기 · 장갑 · 기병 · 정찰병 제외).</summary>
    static bool CoverCapable(Unit u) => u.Def.CanOccupyTrench && !u.CanDeploy && u.Weapon != null;

    void UpdateCoverSeek(Unit u)
    {
        var c = Defs.CombatRules;
        if (c.CoverSeekRadius <= 0 || c.CoverSeekTicks <= 0) return;
        if ((Tick + u.Id) % c.CoverSeekTicks != 0) return;
        if (u.Order is not (UnitOrder.Idle or UnitOrder.Hold) || u.Group != null || u.Charging || u.InTrench || u.TrenchSpot >= 0) return;
        if (!CoverCapable(u) || !UnderFire(u) || u.Velocity != Vector2.Zero) return;
        if (!DefendsForCover(u)) { Stats.CoverSeekSkipAttack++; return; }   // tweak1: 방어하는 유닛만 (공격측은 맞아도 엄폐로 빠지지 않음)
        bool hold = u.Order == UnitOrder.Hold;
        int spot = FindTrenchSpot(u.Pos, c.CoverSeekRadius, -1);
        if (spot >= 0)
        {
            ResetOrder(u);
            ClaimTrenchSpot(u, spot);
            WalkToTrenchSpot(u);   // 도착하면 EnterTrench (사수)
            Stats.CoverSeeks++; Stats.CoverSeekTrench++;
            return;
        }
        float cur = CoverHitAt(u.Pos, u);
        if (cur <= Defs.Terrain.TrenchHit + 1e-3f) return;   // 이미 참호급 엄폐
        Vector2? dest = BestCoverTile(u.Pos, c.CoverSeekRadius, cur - c.CoverSeekGain, u);
        if (dest is not Vector2 d) return;
        ResetOrder(u);
        u.Group = new MoveGroup(this, d, 1);
        u.Order = UnitOrder.Move;
        u.CoverHold = hold;
        Stats.CoverSeeks++;
    }

    /// <summary>
    /// tweak1 (사용자 결정 2a): 피격 엄폐 찾기는 방어하는 유닛만 — 중대 자세 Defend (전술 AI 방어 · 예비 · 후퇴 과제) 또는 틀 방어 국면 (Defend),
    /// 자세가 정해지지 않은 중대 (플레이어) · 중대 없는 유닛은 사수 · 대기 그대로 (단 공격 틀 행동 Engage 중이면 아님). 자세 Attack 이면 안 함.
    /// 공격 이동의 가벼운 엄폐 경유 (전술 AI CoverHop) 는 그대로.
    /// </summary>
    internal static bool DefendsForCover(Unit u)
    {
        var c = u.Company;
        if (c == null) return true;
        if (c.Posture == CompanyPosture.Defend || c.Drill.Phase == DrillPhase.Defend) return true;
        if (c.Posture == CompanyPosture.Attack) return false;
        return c.Drill.Phase != DrillPhase.Engage;
    }

    /// <summary>중대 자세 지정 (전술 AI). 자기 중대만.</summary>
    public void SetPosture(int faction, IEnumerable<int> companies, CompanyPosture posture)
    {
        var ids = companies.Distinct().OrderBy(i => i).ToArray();
        if (ids.Length > 0) Issue(new SetPostureCommand(faction, ids, posture));
    }

    void ExecSetPosture(int f, int[] companies, CompanyPosture posture)
    {
        if (!Enum.IsDefined(typeof(CompanyPosture), posture)) return;
        foreach (int id in companies)
            if (OwnCompany(f, id) is { } c) c.Posture = posture;
    }

    // 저장 · 체크섬 (ORG_ 끝 WriteCloseRanks 뒤, FormatVersion 31)
    void WritePostures(SnapWriter w) { foreach (var c in Companies) w.Byte((byte)c.Posture); }

    void ReadPostures(SnapReader r)
    {
        foreach (var c in Companies)
        {
            byte b = r.Byte();
            if (!Enum.IsDefined(typeof(CompanyPosture), b)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company posture"));
            c.Posture = (CompanyPosture)b;
        }
    }

    void HashPostures(ref Hasher h) { foreach (var c in Companies) h.Add((int)c.Posture); }

    /// <summary>pos 둘레 r 칸 안에서 명중 배수 ≤ maxHit 인 가장 좋은 엄폐 칸 중심 (점수 = 배수 + 0.04 × 거리, 같으면 칸 순). 다른 유닛이 칸당 coverSeekPerTile 명 이상이면 건너뜀.</summary>
    Vector2? BestCoverTile(Vector2 pos, float r, float maxHit, Unit? self)
    {
        var (px, py) = GameMap.TileOf(pos);
        int ri = (int)MathF.Ceiling(r);
        float best = float.MaxValue, r2 = r * r;
        Vector2? res = null;
        int cap = Math.Max(1, Defs.CombatRules.CoverSeekPerTile);
        for (int dy = -ri; dy <= ri; dy++)
        for (int dx = -ri; dx <= ri; dx++)
        {
            int x = px + dx, y = py + dy;
            if (!Map.IsWalkable(x, y) || Map.IsDeepForest(x, y) || Map.IsWire(new Vector2(x + 0.5f, y + 0.5f))) continue;
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float dd = Vector2.DistanceSquared(q, pos);
            if (dd > r2) continue;
            float hit = self != null ? CoverHitAt(q, self) : Map.TerrainCover(q).Hit;
            if (hit > maxHit) continue;
            float sc = hit + 0.04f * MathF.Sqrt(dd);
            if (sc >= best) continue;
            if (TileCrowd(q, self) >= cap) continue;
            best = sc; res = q;
        }
        return res;
    }

    int TileCrowd(Vector2 q, Unit? self)
    {
        int n = 0;
        foreach (var o in UnitsInRadius(q, 0.6f, coverBuf))
            if (o != self && o.Alive && (o.Def.IsInfantry)) n++;
        return n;
    }

    /// <summary>ArriveGroup: 엄폐 칸에 도착 — 사수였으면 다시 사수.</summary>
    void CoverOnArrive(Unit u)
    {
        if (!u.CoverHold) return;
        u.CoverHold = false;
        if (u.Order == UnitOrder.Idle && !u.InTrench) u.Order = UnitOrder.Hold;
    }

    // ---------------------------------------------------------------- 진지 점유 (전술 AI 방어)

    readonly List<(float Score, int Spot, Vector2 Pos, bool Trench)> occCand = new();

    /// <summary>
    /// 진지 점유: 지점 둘레 radius 안 참호 자리 · 엄폐 칸 자리에 병사를 배정 (명령 실행). dir = 적 쪽 (앞쪽 자리 가산).
    /// 엄폐 칸은 명중 배수 ≤ coverMaxHit 인 칸만. 결과 = (참호 배정 수, 엄폐 배정 수).
    /// </summary>
    internal (int Trench, int Cover) ExecOccupyPositions(int faction, List<Unit> list, Vector2 point, Vector2 dir, float radius, float coverMaxHit)
    {
        Stats.OccupyOrders++;
        point = ClampToMap(point);
        if (dir.LengthSquared() > 1e-6f) dir = Vector2.Normalize(dir);
        var occ = new List<Unit>();
        var rest = new List<Unit>();
        foreach (var u in list) (u.Def.CanOccupyTrench && u.Weapon != null && !u.IsOfficer ? occ : rest).Add(u);   // 방열 무기 (기관총) 도 진지로
        var mine = new HashSet<int>();
        foreach (var u in list) mine.Add(u.Id);
        occCand.Clear();
        var comps = TrenchComps();
        var owners = TrenchOwners();
        int cap = TrenchPerTile, w = Map.Width, coverCap = Math.Max(1, Defs.CombatRules.CoverSeekPerTile);
        int x0 = Math.Max(0, (int)MathF.Floor(point.X - radius)), x1 = Math.Min(w - 1, (int)MathF.Floor(point.X + radius));
        int y0 = Math.Max(0, (int)MathF.Floor(point.Y - radius)), y1 = Math.Min(Map.Height - 1, (int)MathF.Floor(point.Y + radius));
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float d2 = Vector2.DistanceSquared(q, point);
            if (d2 > r2) continue;
            float fwd = Vector2.Dot(q - point, dir);
            if (comps[y * w + x] >= 0)
            {
                for (int k = 0; k < cap; k++)
                {
                    int spot = (y * w + x) * 4 + k;
                    if (owners.TryGetValue(spot, out int id) && !mine.Contains(id)) continue;
                    var sp = TrenchSpotPos(spot);
                    float sc = MathF.Sqrt(Vector2.DistanceSquared(sp, point)) * (0.5f + Defs.Terrain.TrenchHit) - 0.3f * fwd;
                    occCand.Add((sc, spot, sp, true));
                }
                continue;
            }
            if (!Map.IsWalkable(x, y) || Map.IsDeepForest(x, y) || Map.IsWire(q)) continue;
            float hit = Map.TerrainCover(q).Hit;
            if (hit > coverMaxHit) continue;
            int crowd = 0;
            foreach (var o in UnitsInRadius(q, 0.6f, coverBuf)) if (o.Alive && o.Def.IsInfantry && !mine.Contains(o.Id)) crowd++;
            for (int k = crowd; k < coverCap; k++)
            {
                var sp = q + new Vector2(k == 0 ? -0.22f : 0.22f, 0);
                float sc = MathF.Sqrt(d2) * (0.5f + hit) - 0.3f * fwd + 0.5f;   // 같은 거리면 참호 먼저
                occCand.Add((sc, -1 - (y * w + x) * 4 - k, sp, false));
            }
        }
        occCand.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Spot.CompareTo(b.Spot));
        int take = Math.Min(occ.Count, occCand.Count);
        var pairs = new List<(float D, int Id, int C, int U)>(take * occ.Count);
        for (int c = 0; c < take; c++)
            for (int i = 0; i < occ.Count; i++)
                pairs.Add((Vector2.DistanceSquared(occ[i].Pos, occCand[c].Pos), occ[i].Id, c, i));
        pairs.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.Id != b.Id ? a.Id.CompareTo(b.Id) : a.C.CompareTo(b.C));
        var unitTo = new int[occ.Count]; Array.Fill(unitTo, -1);
        var used = new bool[take];
        foreach (var p in pairs)
        {
            if (unitTo[p.U] >= 0 || used[p.C]) continue;
            unitTo[p.U] = p.C; used[p.C] = true;
        }
        // 자리를 바꾸는 병사가 남의 새 자리를 막지 않게 — 먼저 전원 해제
        for (int i = 0; i < occ.Count; i++) if (unitTo[i] >= 0) ResetOrder(occ[i]);
        int nt = 0, nc = 0;
        for (int i = 0; i < occ.Count; i++)
        {
            var u = occ[i];
            if (unitTo[i] < 0) { rest.Add(u); continue; }
            var cd = occCand[unitTo[i]];
            u.ReactDelay = ReactDelayFor(u) + ChainDelayTicks(u);
            u.LastOrderTick = Tick;
            if (cd.Trench)
            {
                ClaimTrenchSpot(u, cd.Spot);
                if (Vector2.Distance(u.Pos, cd.Pos) <= TrenchEnterDist) EnterTrench(u); else WalkToTrenchSpot(u);
                nt++;
            }
            else
            {
                u.Group = new MoveGroup(this, cd.Pos, 1);
                u.Order = UnitOrder.Move;
                u.CoverHold = true;
                nc++;
            }
        }
        Stats.OccupyTrenchSpots += nt; Stats.OccupyCoverSpots += nc;
        if (rest.Count > 0)
        {
            foreach (var u in rest) ResetOrder(u);
            ExecGroupOrder(faction, rest, ClampToMap(point - dir * 3f), UnitOrder.Move, rest.Count >= 2 ? new FormSpec((int)FormType.Box, 0, dir, 0) : null);
            foreach (var u in rest) u.CoverHold = true;   // 도착하면 사수
        }
        return (nt, nc);
    }

    /// <summary>[AI · 읽기] pos 에서 dir 쪽 (+) · 반대 (−) 로 훑어 참호 · 엄폐 칸이 radius 안에 몰린 곳 (엄폐 점수 합이 가장 큰 칸 중심). 없으면 null.</summary>
    public Vector2? CoverCluster(Vector2 centre, float radius, float sample = 3f)
    {
        var (px, py) = GameMap.TileOf(centre);
        int ri = (int)MathF.Ceiling(radius);
        float best = 0.5f;
        Vector2? res = null;
        for (int dy = -ri; dy <= ri; dy += 2)
        for (int dx = -ri; dx <= ri; dx += 2)
        {
            int x = px + dx, y = py + dy;
            if (!Map.InBounds(x, y) || dx * dx + dy * dy > ri * ri) continue;
            float s = 0;
            int si = (int)sample;
            for (int ey = -si; ey <= si; ey++)
            for (int ex = -si; ex <= si; ex++)
            {
                int qx = x + ex, qy = y + ey;
                if (!Map.IsWalkable(qx, qy) || Map.IsDeepForest(qx, qy)) continue;
                s += 1f - Map.TerrainCover(new Vector2(qx + 0.5f, qy + 0.5f)).Hit;
            }
            s -= 0.02f * MathF.Sqrt(dx * dx + dy * dy);
            if (s > best) { best = s; res = new Vector2(x + 0.5f, y + 0.5f); }
        }
        return res;
    }
}
