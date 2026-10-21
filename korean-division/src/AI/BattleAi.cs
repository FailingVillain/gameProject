using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 교전 테스트용 가벼운 AI (--ai battle · 교전 모드의 상대편). 경제 · 건물 없이 전투 유닛만 조작한다.
///  · 처음에 자기 병력을 위치순(세로 → 가로)으로 줄 세워 부대(squad)로 나눈다 — 진형이 가로로 길어서 부대마다 여러 종류가 섞인다.
///  · 부대는 보이는(Fog.CanSee) 가장 가까운 적을 향해 어택땅, 보이는 적이 없으면 마지막으로 본 적 무리 · 적 시작 위치로.
///  · 대전차 보병(기갑에 보너스 무기)은 근처에 보이는 기갑 적이 있으면 그 기갑을 우선 지정 공격.
///  · 체력이 크게 떨어지고(평균 30% 미만) 주변 적이 더 많은 부대는 자기 진영 쪽으로 후퇴해 재집결한 뒤 다시 전진.
/// 결정적: 부대 편성 · 순회는 모두 목록 순서, 해시 순서에 의존하지 않는다. 명령은 World 공개 API 로만.
/// </summary>
public sealed class BattleAi : IAiController
{
    public string Name => "battle";

    sealed class Squad
    {
        public readonly List<int> Ids = new();
        public int Initial;
        public Vector2 LastTarget;
        public long LastOrder = -100000, LastAtOrder = -100000, FallbackUntil, NextFallback;
        public int AtTargetId;
        public bool Fallback;
    }

    sealed class State
    {
        public bool Init;
        public readonly List<Squad> Squads = new();
        public Vector2 LastEnemyCentroid;
        public bool HaveEnemyCentroid;
        public Vector2 Rally;
    }

    readonly Dictionary<int, State> states = new();
    [ThreadStatic] static EnemyGrid? gridInst;
    static EnemyGrid grid => gridInst ??= new EnemyGrid();   // 저장 대상이 아닌 매 틱 작업 버퍼
    readonly List<Unit> enemies = new(), members = new(), plain = new(), anti = new(), tmp = new();

    public void Tick(World w, int faction)
    {
        if (!states.TryGetValue(faction, out var s)) states[faction] = s = new State();
        var antiDef = AiDefs.For(w.Defs).AntiArmor;
        if (!s.Init) { s.Init = true; Form(w, faction, s); }

        // 보이는 적 전투 유닛
        enemies.Clear();
        Vector2 sum = Vector2.Zero;
        foreach (var u in w.Units)
            if (u.Alive && u.Owner != faction && u.Weapon != null && w.IsHostile(faction, u.Owner) && w.Fog.CanSee(faction, u))
            {
                enemies.Add(u);
                sum += u.Pos;
            }
        grid.Build(w, enemies);
        if (enemies.Count > 0) { s.LastEnemyCentroid = sum / enemies.Count; s.HaveEnemyCentroid = true; }
        Vector2 fallbackTarget = s.HaveEnemyCentroid ? s.LastEnemyCentroid : EnemyStart(w, faction);

        for (int i = s.Squads.Count - 1; i >= 0; i--)
        {
            var sq = s.Squads[i];
            members.Clear();
            int keep = 0;   // 한 번에: 죽은 · 없는 id 를 걸러 내며 (순서 유지) 멤버 목록을 만든다
            for (int k = 0; k < sq.Ids.Count; k++)
                if (w.Find(sq.Ids[k]) is Unit u && u.Alive) { sq.Ids[keep++] = sq.Ids[k]; members.Add(u); }
            sq.Ids.RemoveRange(keep, sq.Ids.Count - keep);
            if (members.Count == 0) { s.Squads.RemoveAt(i); continue; }
            Think(w, faction, s, sq, members, fallbackTarget, antiDef);
        }
    }

    /// <summary>자기 병력을 위치순으로 부대로 나눈다.</summary>
    void Form(World w, int faction, State s)
    {
        tmp.Clear();
        foreach (var u in w.Units)
            if (u.Alive && u.Owner == faction && u.Weapon != null) tmp.Add(u);
        if (tmp.Count == 0) return;
        Vector2 c = Vector2.Zero;
        foreach (var u in tmp) c += u.Pos;
        s.Rally = c / tmp.Count;
        tmp.Sort((a, b) => a.Pos.Y != b.Pos.Y ? a.Pos.Y.CompareTo(b.Pos.Y) : a.Pos.X != b.Pos.X ? a.Pos.X.CompareTo(b.Pos.X) : a.Id.CompareTo(b.Id));
        int size = Math.Clamp(tmp.Count / 16, 8, 50);
        int squads = (tmp.Count + size - 1) / size;
        int per = (tmp.Count + squads - 1) / squads;   // 고르게
        for (int i = 0; i < tmp.Count; i += per)
        {
            var sq = new Squad();
            for (int k = i; k < Math.Min(tmp.Count, i + per); k++) sq.Ids.Add(tmp[k].Id);
            sq.Initial = sq.Ids.Count;
            s.Squads.Add(sq);
        }
    }

    static Vector2 EnemyStart(World w, int faction)
    {
        for (int g = 1; g < w.Factions.Count; g++)
            if (g != faction && AiHelpers.IsAlive(w, g)) return AiHelpers.StartOf(w, g);
        return new Vector2(w.Map.Width / 2f, w.Map.Height / 2f);
    }

    void Think(World w, int f, State s, Squad sq, List<Unit> mem, Vector2 fallbackTarget, UnitDef? antiDef)
    {
        Vector2 c = Vector2.Zero;
        float hpFrac = 0;
        foreach (var u in mem) { c += u.Pos; hpFrac += u.Hp / u.MaxHp; }
        c /= mem.Count;
        hpFrac /= mem.Count;

        // 가장 가까운 적 · 가장 가까운 기갑 적 · 근처 적 수
        // (적 목록 순서의 동점 규칙은 그대로: 같은 거리면 목록에서 앞선 쪽 — 격자 질의가 색인으로 비교)
        var nearest = grid.Nearest(c);
        var nearestArmor = grid.NearestArmor(c, 22);
        int near = grid.CountWithin(c, 14);

        // 후퇴 · 재집결
        if (sq.Fallback)
        {
            if (w.Tick >= sq.FallbackUntil) { sq.Fallback = false; sq.NextFallback = w.Tick + 1200; sq.LastOrder = -100000; }
            else return;
        }
        else if (hpFrac < 0.3f && near > mem.Count && w.Tick >= sq.NextFallback && sq.Initial >= 4)
        {
            sq.Fallback = true;
            sq.FallbackUntil = w.Tick + 540;
            w.Retreat(f, mem, s.Rally);   // 후퇴 = 속보 (pursuit)
            return;
        }

        Vector2 target = nearest != null ? nearest.Pos : fallbackTarget;
        bool retarget = Vector2.DistanceSquared(target, sq.LastTarget) > 12 * 12;
        bool due = w.Tick - sq.LastOrder >= 240;

        plain.Clear(); anti.Clear();
        foreach (var u in mem)
        {
            if (antiDef != null && u.Def == antiDef) anti.Add(u); else plain.Add(u);
        }

        if (nearestArmor != null && anti.Count > 0)
        {
            // 대전차 보병: 기갑을 우선 지정 공격 (표적이 바뀌었거나 3초마다 다시)
            if (sq.AtTargetId != nearestArmor.Id || w.Tick - sq.LastAtOrder >= 180)
            {
                w.Attack(f, anti, nearestArmor);
                sq.AtTargetId = nearestArmor.Id;
                sq.LastAtOrder = w.Tick;
            }
            if (plain.Count > 0 && (retarget || due)) { w.AttackMove(f, plain, target); sq.LastTarget = target; sq.LastOrder = w.Tick; }
            return;
        }
        sq.AtTargetId = 0;
        if (retarget || due)
        {
            w.AttackMove(f, mem, target);
            sq.LastTarget = target;
            sq.LastOrder = w.Tick;
            return;
        }
        // 한가해진 병사만 다시 투입
        tmp.Clear();
        foreach (var u in mem) if (u.Order == UnitOrder.Idle && u.Target == null) tmp.Add(u);
        if (tmp.Count > 0 && w.Tick - sq.LastOrder >= 60) { w.AttackMove(f, tmp, target); sq.LastOrder = w.Tick; }
    }
}

/// <summary>
/// 보이는 적 전투 유닛의 거친 격자 (8타일 칸). 분대마다 적 전체를 훑지 않고 중심 주변만 본다.
/// 질의 결과는 "적 목록을 처음부터 훑으며 d &lt; best 로 갱신"한 것과 같다: 같은 거리에서는 목록 인덱스가 작은 쪽.
/// </summary>
sealed class EnemyGrid
{
    const int Cell = 8;
    List<Unit> list = new();
    int cols, rows;
    int[] start = Array.Empty<int>(), items = Array.Empty<int>(), cellOf = Array.Empty<int>();

    int CX(float x) => Math.Clamp((int)(x / Cell), 0, cols - 1);
    int CY(float y) => Math.Clamp((int)(y / Cell), 0, rows - 1);

    public void Build(World w, List<Unit> enemies)
    {
        list = enemies;
        cols = (w.Map.Width + Cell - 1) / Cell; rows = (w.Map.Height + Cell - 1) / Cell;
        int cells = cols * rows;
        if (start.Length < cells + 1) start = new int[cells + 1]; else Array.Clear(start, 0, cells + 1);
        if (items.Length < enemies.Count) { items = new int[enemies.Count]; cellOf = new int[enemies.Count]; }
        for (int i = 0; i < enemies.Count; i++)
        {
            int c = CY(enemies[i].Pos.Y) * cols + CX(enemies[i].Pos.X);
            cellOf[i] = c; start[c + 1]++;
        }
        for (int c = 0; c < cells; c++) start[c + 1] += start[c];
        // 칸 안에서는 인덱스 오름차순 (순서대로 채움)
        var fill = new int[cells];
        for (int i = 0; i < enemies.Count; i++) items[start[cellOf[i]] + fill[cellOf[i]]++] = i;
    }

    public Unit? Nearest(Vector2 c)
    {
        if (list.Count == 0) return null;
        int cx = CX(c.X), cy = CY(c.Y), maxR = Math.Max(cols, rows);
        int bestI = -1; float bestD = float.MaxValue;
        for (int r = 0; r <= maxR; r++)
        {
            if (bestI >= 0 && r >= 2) { float bound = (r - 1) * Cell * 0.99f; if (bestD < bound * bound) break; }
            for (int y = Math.Max(0, cy - r); y <= Math.Min(rows - 1, cy + r); y++)
            for (int x = Math.Max(0, cx - r); x <= Math.Min(cols - 1, cx + r); x++)
            {
                if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != r) continue;
                int cell = y * cols + x;
                for (int k = start[cell], e = start[cell + 1]; k < e; k++)
                {
                    int i = items[k];
                    float d = Vector2.DistanceSquared(list[i].Pos, c);
                    if (d < bestD || (d == bestD && i < bestI)) { bestD = d; bestI = i; }
                }
            }
        }
        return bestI >= 0 ? list[bestI] : null;
    }

    public Unit? NearestArmor(Vector2 c, float radius)
    {
        int bestI = -1; float bestD = radius * radius;
        ForCells(c, radius, i =>
        {
            var e = list[i];
            if (!e.Def.IsArmored) return;
            float d = Vector2.DistanceSquared(e.Pos, c);
            if (d < bestD || (d == bestD && bestI >= 0 && i < bestI)) { bestD = d; bestI = i; }
        });
        return bestI >= 0 ? list[bestI] : null;
    }

    public int CountWithin(Vector2 c, float radius)
    {
        int n = 0; float r2 = radius * radius;
        ForCells(c, radius, i => { if (Vector2.DistanceSquared(list[i].Pos, c) <= r2) n++; });
        return n;
    }

    void ForCells(Vector2 c, float radius, Action<int> visit)
    {
        if (list.Count == 0) return;
        int x0 = CX(c.X - radius), x1 = CX(c.X + radius), y0 = CY(c.Y - radius), y1 = CY(c.Y + radius);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int cell = y * cols + x;
            for (int k = start[cell], e = start[cell + 1]; k < e; k++) visit(items[k]);
        }
    }
}
