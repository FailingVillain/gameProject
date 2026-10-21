using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 미션용 단순 AI (--ai mission, 미션 세력 기본값). 자세 = 병력의 posture (없으면 세력의 factions[].posture):
///  · attack — 무리마다 보이는 가장 가까운 적에게, 없으면 목표 구역 (병력 aiArea → 세력 area) · 적 시작 위치로 어택땅.
///  · defend — 처음 위치 (배치 위치) 에서 제자리 사격 (Hold · 자동 참호), 8칸 안까지 들어온 적에게만 반격, 밀려나면 제자리로.
///  · hold — 구역 (aiArea / area, 없으면 처음 위치) 을 고수: 구역 안에 들어온 적에게만 반격.
/// 무리 = 중대 (중대가 없으면 같은 병력 항목의 유닛). 비무장 (보급 마차) 무리는 적을 무시하고 목표 구역으로만 움직인다.
/// 미션 기본 AI 는 이제 전술 AI (TacticalAi + SetDirective, World.AttachMissionAi) 이고, 이 클래스는 (1) ai:"mission" 으로 고른 비교용 단독 AI,
/// (2) "mission-aux" = 전술 AI 옆에서 비무장 (마차 호송) 무리만 맡는 보조 로 쓰인다 (증원 병력은 전술 AI 2단계가 지시 그룹으로 편입).
/// 규칙: 보이는 적만 (Fog.CanSee), 명령은 World 공개 API, 순회는 목록 순서 (결정적).
/// </summary>
public sealed class MissionAi : IAiController
{
    public string Name => companion ? "mission-aux" : "mission";

    /// <summary>보조 모드 ("mission-aux"): 전술 AI 와 같은 세력에 붙어, 전술 AI 가 맡지 않는 무리만 움직인다 —
    /// 비무장 (호송 마차 등) 무리만. 나머지 무리 (증원 포함 — 전술 AI 2단계가 편입) 는 건드리지 않는다.</summary>
    bool companion;
    public MissionAi() { }
    public MissionAi(bool companion) { this.companion = companion; }

    sealed class Group
    {
        public Vector2 Home;
        public bool HomeSet;
        public Vector2 LastTarget;
        public long LastOrder = -100000;
        public bool Engaged, Holding;
    }

    /// <summary>세력 → (무리 열쇠 → 상태). 열쇠 = 중대 Id (양수), 중대 없는 유닛 = −(병력 순번 + 1), 미션 밖 유닛 = 0.</summary>
    readonly Dictionary<int, Dictionary<int, Group>> states = new();
    readonly List<Unit> enemies = new(), members = new(), idle = new();
    readonly List<int> keys = new();
    readonly Dictionary<int, int> forceOfUnit = new();   // 작업 버퍼: 유닛 Id → 병력 순번 (매 틱 다시)

    public void Tick(World w, int faction)
    {
        if (w.Mission is not { } m || faction - 1 >= m.Def.Factions.Count) return;
        var def = m.Def;
        var fdef = def.Factions[faction - 1];
        var all = def.Forces.Concat(def.Reinforcements).ToList();
        forceOfUnit.Clear();
        for (int i = 0; i < all.Count; i++)
            if (all[i].Faction == faction && m.ForceUnits.TryGetValue(all[i].Id, out var ids))
                foreach (int id in ids) forceOfUnit[id] = i;
        if (!states.TryGetValue(faction, out var groups)) states[faction] = groups = new Dictionary<int, Group>();

        enemies.Clear();
        foreach (var u in w.Units)
            if (u.Alive && w.IsHostile(faction, u.Owner) && w.Fog.CanSee(faction, u)) enemies.Add(u);

        keys.Clear();
        foreach (var u in w.Units)
        {
            if (!u.Alive || u.Owner != faction) continue;
            int k = KeyOf(u);
            if (!keys.Contains(k)) keys.Add(k);
        }
        foreach (int k in keys)
        {
            members.Clear();
            foreach (var u in w.Units)
                if (u.Alive && u.Owner == faction && KeyOf(u) == k) members.Add(u);
            if (members.Count == 0) continue;
            if (!groups.TryGetValue(k, out var g)) groups[k] = g = new Group();
            var force = forceOfUnit.TryGetValue(members[0].Id, out int fi) ? all[fi] : null;
            if (companion && (force == null || members.Exists(u => u.Weapon != null))) continue;   // 보조: 비무장 무리만 (증원은 전술 AI 2단계가 편입)
            string posture = (force?.Posture ?? fdef.Posture ?? "attack").ToLowerInvariant();
            var area = def.Area(force?.AiArea ?? fdef.Area);
            Think(w, faction, posture, area, g);
        }
    }

    int KeyOf(Unit u) => u.Company?.Id ?? u.ScoutCompany?.Id ?? (forceOfUnit.TryGetValue(u.Id, out int fi) ? -(fi + 1) : 0);

    void Think(World w, int f, string posture, MissionArea? area, Group g)
    {
        Vector2 c = Vector2.Zero;
        bool armed = false;
        foreach (var u in members) { c += u.Pos; if (u.Weapon != null) armed = true; }
        c /= members.Count;
        if (!g.HomeSet) { g.HomeSet = true; g.Home = posture == "hold" && area != null ? area.Clamp(c) : c; }

        if (!armed)
        {
            // 비무장: 목표 구역으로 (attack) 또는 제자리 (defend · hold)
            Vector2 dest = posture == "attack" ? area?.Center ?? g.Home : g.Home;
            bool shelter = false;
            if (companion && posture == "attack")
            {
                dest = BehindEscort(w, f, c, dest);
                if (Shelter(w, f, c) is Vector2 sh) { dest = sh; shelter = true; }
            }
            if (Vector2.Distance(c, dest) > 3 && (Vector2.DistanceSquared(dest, g.LastTarget) > (shelter ? 9 : 1) || w.Tick - g.LastOrder >= 600))
            {
                w.Move(f, members, dest);
                g.LastTarget = dest;
                g.LastOrder = w.Tick;
            }
            return;
        }

        Unit? nearest = null;
        float best = float.MaxValue;
        foreach (var e in enemies)
        {
            float d = Vector2.DistanceSquared(e.Pos, c);
            if (d < best) { best = d; nearest = e; }
        }

        if (posture == "attack")
        {
            Order(w, f, g, nearest?.Pos ?? area?.Center ?? EnemyStart(w, f));
            return;
        }

        // defend / hold: 제자리에서 버티며 (Hold = 제자리 사격 · 자동 참호), 지킬 곳 안까지 들어온 적에게만 반격
        Vector2 guard = posture == "hold" && area != null ? area.Center : g.Home;
        float inner = posture == "hold" && area != null ? area.Radius : 8;
        Unit? threat = null;
        float tb = float.MaxValue;
        foreach (var e in enemies)
        {
            if (Vector2.Distance(e.Pos, guard) > inner) continue;
            float d = Vector2.DistanceSquared(e.Pos, c);
            if (d < tb) { tb = d; threat = e; }
        }
        if (threat != null)
        {
            g.Engaged = true;
            g.Holding = false;
            Order(w, f, g, threat.Pos);
            return;
        }
        if (Vector2.Distance(c, g.Home) > 4)
        {
            if (g.Engaged || w.Tick - g.LastOrder >= 240)
            {
                g.Engaged = false;
                g.Holding = false;
                w.Move(f, members, g.Home);
                g.LastTarget = g.Home;
                g.LastOrder = w.Tick;
            }
            return;
        }
        g.Engaged = false;
        idle.Clear();
        foreach (var u in members) if (u.Order == UnitOrder.Idle) idle.Add(u);
        if (idle.Count > 0 && (!g.Holding || w.Tick - g.LastOrder >= 120))
        {
            w.Hold(f, idle);
            g.Holding = true;
            g.LastOrder = w.Tick;
        }
    }

    /// <summary>
    /// 보조 모드의 비무장 무리: 보이는 무장 적이 20칸 안이면 위협 반대쪽의 아군 무장 유닛 뒤로 피한다
    /// (전술 AI 2단계 실험 — 호위 중대들이 좌우 매복조와 싸우는 사이 가운데 서 있던 마차를 기마 마적이 덮쳤다). 아군이 없으면 위협 반대쪽으로.
    /// </summary>
    Vector2? Shelter(World w, int f, Vector2 c)
    {
        Unit? threat = null; float tb = 20f * 20f;
        foreach (var e in enemies)
        {
            if (e.Weapon == null) continue;
            float d = Vector2.DistanceSquared(e.Pos, c);
            if (d < tb) { tb = d; threat = e; }
        }
        if (threat == null) return null;
        Vector2 away = c - threat.Pos;
        away = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : new Vector2(-1, 0);
        Unit? best = null; float bs = float.MinValue;
        foreach (var u in w.Units)
        {
            if (!u.Alive || u.Owner != f || u.Weapon == null) continue;
            Vector2 to = u.Pos - c;
            float d = to.Length();
            if (d > 30f) continue;
            float sc = (d > 1e-3f ? Vector2.Dot(to / d, away) : 0f) * 10f - d * 0.2f;
            if (sc > bs) { bs = sc; best = u; }
        }
        Vector2 p;
        if (best != null)
        {
            Vector2 back = best.Pos - threat.Pos;
            p = best.Pos + (back.LengthSquared() > 1e-4f ? Vector2.Normalize(back) : away) * 3f;
        }
        else p = c + away * 8f;
        return new Vector2(Math.Clamp(p.X, 1f, w.Map.Width - 2f), Math.Clamp(p.Y, 1f, w.Map.Height - 2f));
    }

    /// <summary>보조 모드의 비무장 무리: 같은 편 무장 병력의 중심보다 6칸 이상 앞서 가지 않는다 (호위가 길을 열기 전에 마차만 앞서 매복에 들어가지 않게).</summary>
    static Vector2 BehindEscort(World w, int f, Vector2 c, Vector2 dest)
    {
        Vector2 sum = Vector2.Zero; int n = 0;
        foreach (var u in w.Units)
            if (u.Alive && u.Owner == f && u.Weapon != null) { sum += u.Pos; n++; }
        if (n == 0) return dest;
        if (Vector2.Distance(sum / n, dest) < 10f) return dest;   // 호위가 목적지에 닿았다 → 마차도 들어간다
        Vector2 to = dest - c;
        float dist = to.Length();
        if (dist < 1f) return dest;
        Vector2 dir = to / dist;
        float ahead = Vector2.Dot(sum / n - c, dir) - 6f;
        if (ahead >= dist) return dest;
        return ahead < 2f ? c : c + dir * ahead;
    }

    void Order(World w, int f, Group g, Vector2 target)
    {
        bool retarget = Vector2.DistanceSquared(target, g.LastTarget) > 10 * 10;
        if (retarget || w.Tick - g.LastOrder >= 300)
        {
            w.AttackMove(f, members, target);
            g.LastTarget = target;
            g.LastOrder = w.Tick;
            return;
        }
        idle.Clear();
        foreach (var u in members) if (u.Order == UnitOrder.Idle && u.Target == null) idle.Add(u);
        if (idle.Count > 0 && w.Tick - g.LastOrder >= 60) { w.AttackMove(f, idle, target); g.LastOrder = w.Tick; }
    }

    static Vector2 EnemyStart(World w, int faction)
    {
        for (int g = 1; g < w.Factions.Count; g++)
            if (w.IsHostile(faction, g) && AiHelpers.IsAlive(w, g)) return AiHelpers.StartOf(w, g);
        return new Vector2(w.Map.Width / 2f, w.Map.Height / 2f);
    }
}
