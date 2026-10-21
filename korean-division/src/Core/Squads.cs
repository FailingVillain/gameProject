using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 분대 (ROADMAP §2-4c · §2-7, 2026-10-19 확정): 중대 안의 고정 편성 — 최대 combat.squadMax (12) 명, 구성원은 바뀌지 않는다.
/// 바뀌는 경우는 자동 합치기 · 재편성 (구성원이 squadMergeBelow 미만으로 줄면) 과 중대 편성 · 분할뿐. 장교는 분대에 들지 않는다 (중대 본부).
/// 분대장 = 분대 안 최고 사병 계급 (같으면 Id 작은 쪽). 분대장이 공식 부사관 (하사 이상) 이면 분대원에게 멍칫 −50% · 제압 감소 ×1.5 (장교 효과와 중첩 안 함).
/// 시뮬레이션 상태 — ORG_ 구역 저장 · 체크섬 (Organisation.cs).
/// </summary>
public sealed class Squad
{
    public int Id { get; internal set; }
    /// <summary>중대 안 번호 (제N분대, 비어 있는 가장 작은 번호).</summary>
    public int Number { get; internal set; }
    public Company Company { get; internal set; } = null!;
    /// <summary>구성원 (Id 오름차순).</summary>
    public readonly List<Unit> Members = new();
    public int Owner => Company.Owner;
    /// <summary>분대장 (부사관) 이 명령한 행진 사격이 이 틱 전까지 유지 (중대 지휘 장교가 없을 때만, MarchCommand.cs).</summary>
    internal long MarchUntil;
    /// <summary>분대 대형 (Free = 자동 — 전술 틀의 역할 기본 대형). 틀 · 포병 대형 안 배치와 분대만 따로 이동할 때 (CombatFormations.cs).</summary>
    public FormType Form { get; internal set; }

    /// <summary>분대장: 최고 사병 계급 (같으면 Id 작은 쪽). 구성원이 없으면 null. 저장하지 않는 파생값 (계급 = 사살 수의 함수).</summary>
    public Unit? Leader
    {
        get
        {
            Unit? best = null;
            foreach (var u in Members)
                if (u.Alive && (best == null || u.Rank > best.Rank)) best = u;
            return best;
        }
    }

    /// <summary>분대장이 공식 부사관인가 (분대장 효과 있음).</summary>
    public bool NcoLed => Leader is { IsNco: true };

    /// <summary>[표시] 소대 번호 (표시용 이름만 — 분대 3~4개 = 1개 소대). 중대 분대 수 k → 소대 수 ceil(k/4), 소대당 ceil(k/소대 수).</summary>
    public int Platoon
    {
        get
        {
            int k = Math.Max(1, Company.Squads.Count);
            int platoons = (k + 3) / 4;
            int per = (k + platoons - 1) / platoons;
            int idx = Company.Squads.IndexOf(this);
            return idx < 0 ? 1 : idx / per + 1;
        }
    }
}

public sealed partial class Unit
{
    /// <summary>소속 분대 (없으면 null — 중대 밖 · 장교). Squad.Members 의 역참조, ORG_ 구역에서 복원.</summary>
    public Squad? Squad { get; internal set; }
    /// <summary>[장교] 마지막 칼 흔들기 틱 (빈자리 점검 중 교전이면 33% — 애니메이션 sword_wave). 0 = 없음.</summary>
    [SnapExt] public long SwordWaveTick { get; internal set; }
}

public sealed partial class World
{
    int nextSquadId = 1;

    /// <summary>분대 최대 인원 (balance.json combat.squadMax).</summary>
    int SquadMax => Math.Max(2, Defs.CombatRules.SquadMax);

    /// <summary>
    /// 중대 구성원(장교 제외)을 분대로 나눈다: 분대 수 = ceil(n / squadMax), 인원은 고르게. 공간 순서 (넓게 퍼진 축 → 그 위 좌표, 같으면 Id) 로 잘라서 이웃끼리 한 분대.
    /// 기존 분대는 없앤다 (편성 · 분할 때만 — 평소에는 고정).
    /// </summary>
    void BuildSquads(Company c)
    {
        foreach (var s in c.Squads) foreach (var u in s.Members) if (u.Squad == s) u.Squad = null;
        c.Squads.Clear();
        var list = c.Members.Where(u => !u.IsOfficer).ToList();
        if (list.Count == 0) return;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var u in list) { minX = MathF.Min(minX, u.Pos.X); maxX = MathF.Max(maxX, u.Pos.X); minY = MathF.Min(minY, u.Pos.Y); maxY = MathF.Max(maxY, u.Pos.Y); }
        bool byX = maxX - minX >= maxY - minY;
        list.Sort((a, b) =>
        {
            float ka = byX ? a.Pos.X : a.Pos.Y, kb = byX ? b.Pos.X : b.Pos.Y;
            int r = ka.CompareTo(kb);
            return r != 0 ? r : a.Id.CompareTo(b.Id);
        });
        int k = (list.Count + SquadMax - 1) / SquadMax;
        int at = 0;
        for (int i = 0; i < k; i++)
        {
            int size = list.Count / k + (i < list.Count % k ? 1 : 0);
            var s = new Squad { Id = nextSquadId++, Number = i + 1, Company = c };
            for (int j = 0; j < size; j++) AddToSquad(s, list[at++]);
            c.Squads.Add(s);
        }
    }

    static void AddToSquad(Squad s, Unit u)
    {
        int idx = s.Members.BinarySearch(u, UnitIdOrder.Instance);
        s.Members.Insert(idx < 0 ? ~idx : idx, u);
        u.Squad = s;
    }

    sealed class UnitIdOrder : IComparer<Unit>
    {
        public static readonly UnitIdOrder Instance = new();
        public int Compare(Unit? a, Unit? b) => a!.Id.CompareTo(b!.Id);
    }

    static int FreeSquadNumber(Company c)
    {
        int n = 1;
        while (c.Squads.Exists(s => s.Number == n)) n++;
        return n;
    }

    static void SortSquads(Company c) => c.Squads.Sort((a, b) => a.Number != b.Number ? a.Number.CompareTo(b.Number) : a.Id.CompareTo(b.Id));

    /// <summary>유닛을 분대에서 뺀다 (LeaveCompany 가 부른다). 빈 분대는 제거, 줄어든 분대는 자동 합치기 · 재편성.</summary>
    void LeaveSquad(Unit u)
    {
        if (u.Squad is not { } s) return;
        s.Members.Remove(u);
        u.Squad = null;
        if (s.Members.Count == 0) { s.Company.Squads.Remove(s); return; }
        if (s.Members.Count < Defs.CombatRules.SquadMergeBelow) MergeDepleted(s);
    }

    static Vector2 Centroid(List<Unit> us)
    {
        Vector2 c = Vector2.Zero;
        foreach (var u in us) c += u.Pos;
        return us.Count > 0 ? c / us.Count : c;
    }

    /// <summary>
    /// 줄어든 분대 처리: ① 합쳐서 squadMax 이하인 같은 중대 분대가 있으면 (무게중심이 가장 가까운 분대, 같으면 번호 순) 그 분대로 합친다.
    /// ② 없으면 다른 분대들의 빈자리 합이 충분할 때 병사마다 가장 가까운 빈자리 있는 분대로 나눠 넣는다 (재편성). ③ 둘 다 안 되면 그대로.
    /// </summary>
    void MergeDepleted(Squad s)
    {
        var c = s.Company;
        int max = SquadMax;
        Vector2 sc = Centroid(s.Members);
        Squad? best = null;
        float bd = float.MaxValue;
        foreach (var o in c.Squads)
        {
            if (o == s || o.Members.Count + s.Members.Count > max) continue;
            float d = Vector2.DistanceSquared(Centroid(o.Members), sc);
            if (d < bd) { bd = d; best = o; }
        }
        if (best != null)
        {
            foreach (var u in s.Members) AddToSquad(best, u);
            s.Members.Clear();
            c.Squads.Remove(s);
            return;
        }
        int room = 0;
        foreach (var o in c.Squads) if (o != s) room += Math.Max(0, max - o.Members.Count);
        if (room < s.Members.Count) return;
        var cents = new Dictionary<int, Vector2>();
        foreach (var o in c.Squads) if (o != s) cents[o.Id] = Centroid(o.Members);
        foreach (var u in s.Members)
        {
            Squad? to = null;
            float td = float.MaxValue;
            foreach (var o in c.Squads)
            {
                if (o == s || o.Members.Count >= max) continue;
                float d = Vector2.DistanceSquared(cents[o.Id], u.Pos);
                if (d < td) { td = d; to = o; }
            }
            AddToSquad(to!, u);
        }
        s.Members.Clear();
        c.Squads.Remove(s);
    }

    /// <summary>분대장 효과를 받는가: 소속 분대장이 공식 부사관이고 (분대장 자신 포함) 분대장에서 ncoRadius 칸 안 (0 = 거리 무관).</summary>
    public bool NcoLeads(Unit u)
    {
        if (u.Squad is not { } s) return false;
        var l = s.Leader;
        if (l == null || !l.IsNco) return false;
        float r = Defs.CombatRules.NcoRadius;
        return r <= 0 || ReferenceEquals(l, u) || Vector2.DistanceSquared(l.Pos, u.Pos) <= r * r;
    }

    // ---------------------------------------------------------------- 장교 빈자리 점검 (§2-4c 12차 · 15차)

    /// <summary>
    /// 장교 빈자리 점검 (매 틱, Step 에서 StepAnchors 뒤): 중대마다 gapCheckSec 초 간격 (중대 Id 분산, 하한 gapCheckMinSec). 마지막 대형 이후 빠진 병사가 있고 (Holes)
    /// 그 중대 장교가 살아 있거나 같은 세력 장교의 지휘 범위 안이면, 지금 구성원으로 대형을 다시 짜서 (무게중심 · 마지막 방향 · 중대 대형) gapMoveMin 넘게 벗어난 대기 병사만
    /// 새 슬롯으로 보낸다 (공격 이동 — 적이 있으면 싸우며 이동). 느슨해도 된다: 교전 · 돌격 · 이동 · 사격 · 엄폐 중인 병사는 건드리지 않는다 (N12).
    /// 교전 중 (gapCombatSec) 이면 메우지 않고 장교마다 swordWaveChance 확률로 칼 흔들기 (SwordWaveTick).
    /// </summary>
    void StepGapChecks()
    {
        if (Companies.Count == 0) return;
        var rules = Defs.CombatRules;
        int iv = Math.Max(1, (int)MathF.Round(rules.GapCheckSec / Config.SimDt));
        int ivMin = Math.Max(1, (int)MathF.Round(rules.GapCheckMinSec / Config.SimDt));
        int combatTicks = (int)MathF.Round(rules.GapCombatSec / Config.SimDt);
        for (int ci = 0; ci < Companies.Count; ci++)
        {
            var c = Companies[ci];
            if (c.Holes == 0 || (Tick + c.Id * 97L) % Math.Max(ivMin, DecideInterval(c, iv)) != 0) continue;   // 경험치 많은 지휘관은 더 자주 (CommandRanks.cs) — 단 gapCheckMinSec 보다 자주는 아님 (N12)
            if (c.TA.Stage != TaStage.None) continue;               // N32 참호 돌격 중
            if (c.Anchor != null) continue;                         // 행군 · 정렬 중 — 도착한 뒤 다음 점검에서
            if (rules.ChokeEnabled && CompanyInChoke(c)) continue;  // choke: 길목 안에서는 빈자리를 메우지 않는다 (빠져나간 뒤 다음 점검에서)
            if (c.Drill.Phase != DrillPhase.Idle) continue;         // 틀 행동 중 (TacticDrill.cs) — 행동이 끝나면 정렬
            if (c.Facing == Vector2.Zero || c.Members.Count == 0) { c.Holes = 0; continue; }
            var cen = Centroid(c.Members);
            var officer = CompanyOfficer(c, cen);
            if (officer == null) continue;                          // 장교가 없으면 빈자리를 그대로 둔다 (다음 점검에 다시)
            if (CompanyInCombat(c, combatTicks))
            {
                // N12: 교전 중에는 빈자리를 메우지 않는다 (빈자리는 남겨 두고 조용해진 뒤 다음 점검에서). 장교는 칼을 흔들어 독려.
                bool any = false;
                foreach (var u in c.Members)
                    if (u.IsOfficer && u.Alive) { any = true; if (Roll(u.Id, 0x5357) < rules.SwordWaveChance) u.SwordWaveTick = Tick; }
                if (!any && Roll(officer.Id, 0x5357) < rules.SwordWaveChance) officer.SwordWaveTick = Tick;
                continue;
            }
            if (ContactLoose(c)) continue;                          // F1: 접적 · 이탈 중 — 빈자리는 접적이 끝난 뒤 (Contact.cs)
            c.Holes = 0;
            RefillCompany(c, cen);
        }
    }

    /// <summary>N12: 중대가 교전 중인가 — 구성원이 표적을 잡았거나 window 틱 안에 쐈다.</summary>
    bool CompanyInCombat(Company c, int window)
    {
        foreach (var u in c.Members) if (u.Target != null || Tick - u.LastShotTick < window) return true;
        return false;
    }

    /// <summary>
    /// N12: 대형 복귀 금지 — 표적을 잡았거나 최근 (regroupFireSec) 쏜 병사, 엄폐 (참호 안 · 참호 자리로 가는 중 · 숲 · 가옥 등 지형 명중 배수 &lt; 1) 중인 병사는 슬롯으로 돌려보내지 않는다.
    /// </summary>
    internal bool HoldsGround(Unit u)
    {
        if (u.Target != null || Tick - u.LastShotTick < (int)MathF.Round(Defs.CombatRules.RegroupFireSec / Config.SimDt)) return true;
        if (u.InTrench || u.HasTrenchSpot) return true;
        return Map.TerrainCover(u.Pos).Item1 < 0.999f;
    }

    /// <summary>
    /// 중대를 지휘할 장교: 중대 지휘관 (CommandRanks.cs — 인계 중이면 없음), 중대에 장교가 없으면 (인계가 끝난 뒤) 중대 무게중심을 지휘 범위에 두는 같은 세력 장교 (장교 목록 순).
    /// </summary>
    Unit? CompanyOfficer(Company c, Vector2 cen)
    {
        if (Tick < c.HandoverUntil) return null;   // 지휘관 인계 중
        if (c.Commander is { Alive: true } cm) return cm;
        foreach (var u in c.Members) if (u.IsOfficer && u.Alive) return u;
        foreach (var o in officers)
        {
            if (!o.Alive || o.Owner != c.Owner) continue;
            float r = o.Def.CommandRadius;
            if (Vector2.DistanceSquared(o.Pos, cen) <= r * r) return o;
        }
        return null;
    }

    /// <summary>지금 구성원으로 중대 대형을 다시 짜고 (슬롯 = 무게중심 · Facing 기준), 대기 중이면서 슬롯에서 gapMoveMin 넘게 벗어난 병사를 그 슬롯으로 보낸다.</summary>
    void RefillCompany(Company c, Vector2 cen)
    {
        var units = c.Members;
        int n = units.Count;
        if (n < 2) return;
        float spacing = OrgLayout.SpacingFor(units);
        var lay = CompanyLayout.Build(c, units, c.Form, spacing, 0, FormRules, TacRules, combat: Defs.CombatRules);   // 틀 · 역할 반영 (CombatFormations.cs), 장교 = 대열 뒤 (N12)
        var local = lay.Local;
        Vector2 dir = c.Facing, right = new(-dir.Y, dir.X);
        var world = new Vector2[n];
        for (int i = 0; i < n; i++) world[i] = cen + right * local[i].X + dir * local[i].Y;
        var assign = new int[n];
        CompanyLayout.Match(lay, units, world, null, assign);
        float min = Defs.CombatRules.GapMoveMin;
        var movers = new List<(Unit U, int Slot)>();
        for (int i = 0; i < n; i++)
        {
            var u = units[i];
            if (u.Order != UnitOrder.Idle || u.Group != null || u.Charging || u.ReactDelay > 0) continue;   // 이동 · 사수 · 돌격 중이면 그대로 (느슨해도 됨)
            if (Vector2.Distance(u.Pos, world[assign[i]]) <= min) continue;   // N12: gapMoveMin (2.75칸) 안이면 그 자리 그대로
            if (HoldsGround(u)) continue;                                      // N12: 사격 · 엄폐 중이면 대형으로 돌아가지 않음
            movers.Add((u, assign[i]));
        }
        if (movers.Count == 0) return;
        Stats.GapRefills += movers.Count;
        SendToSlots(c, cen, local, world, spacing, movers, UnitOrder.AttackMove);
    }

    /// <summary>movers 를 (cen · Facing 기준) 슬롯으로 보내는 정지 깃발 (도착 상태 — 구성원은 각자 최종 슬롯으로) 을 만든다. 빈자리 점검 · 대열 정비 (CloseRanks.cs) 공용.</summary>
    /// <remarks>gapfill2 (대열 정비): attack = 공격 이동으로 보낼 병사 Id (나머지는 order), keep = 옛 깃발 (도착) 에서 그대로 옮겨 올 병사 · 원래 최종 위치 (명령 · 목적지 유지),
    /// close = 대열 정비 깃발 (공격 이동 병사가 슬롯까지 멈추지 않고 걸어감 — WorldCombat).</remarks>
    void SendToSlots(Company c, Vector2 cen, Vector2[] local, Vector2[] world, float spacing, List<(Unit U, int Slot)> movers, UnitOrder order,
        HashSet<int>? attack = null, List<(Unit U, Vector2 Local, Vector2 Final)>? keep = null, bool close = false)
    {
        Vector2 dir = c.Facing;
        var g = new MoveGroup(this, cen, movers.Count + (keep?.Count ?? 0)) { ArriveRadius = 0.6f, Inited = true };
        g.Big = false; g.Members = null;
        UnitDef? def = null;
        bool anyLarge = movers.Exists(m => m.U.Large);
        foreach (var (u, _) in movers) if ((!anyLarge || u.Large) && (def == null || u.Def.Speed < def.Speed)) def = u.Def;
        var flag = new Unit(FlagIdBase + c.Id, def!, c.Owner, g.Goal) { Group = g };
        flag.Facing = DetMath.Atan2(dir.Y, dir.X);
        float hw = 0;
        foreach (var l in local) hw = MathF.Max(hw, MathF.Abs(l.X));
        var a = new CompanyAnchor { Pos = g.Goal, Heading = dir, Goal = g.Goal, FinalDir = dir, Arrived = true, Spacing = spacing, HalfWidth = hw + spacing * 0.5f, Group = g, Flag = flag, Close = close };
        a.Trail.Add(a.Pos);
        foreach (var (u, slot) in movers)
        {
            a.Slots[u.Id] = local[slot];
            a.FinalOff[u.Id] = ShiftSlot(u, world[slot]) - g.Goal;
            u.Order = attack != null && attack.Contains(u.Id) ? UnitOrder.AttackMove : order;   // 빈자리 점검 = 공격 이동 (적이 있으면 싸우고, 없으면 슬롯으로 — 표적 유지), 대열 정비 = 이동 (곧장 슬롯으로) · 공격 이동 병사는 공격 이동 그대로
            u.Group = g;
            FormReact(u);   // N34: 재배치 · 대열 정비 슬롯 — 병사마다 0~3틱 뒤 반응
        }
        if (keep != null)
            foreach (var (u, l, fin) in keep) { a.Slots[u.Id] = l; a.FinalOff[u.Id] = fin - g.Goal; u.Group = g; }   // 옛 깃발 구성원: 명령 · 최종 위치 그대로 새 깃발로
        c.Anchor = a;
    }
}
