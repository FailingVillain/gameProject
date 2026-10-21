using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 스냅샷 쓰기/읽기가 공유하는 참조 표: 엔티티 참조 (살아 있는 것은 Id, 이미 제거된 것은 '묘비'), 이동 그룹, 경로 객체.
/// 쓰기 쪽은 처음 보는 순간 번호를 매기고, 표 본문은 마지막에 (번호가 모두 정해진 뒤) 파일 앞쪽에 둔다.
/// </summary>
internal sealed class SnapCtx
{
    public readonly World World;
    public readonly Defs Defs;
    public SnapCtx(World world, Defs defs) { World = world; Defs = defs; }

    // ---- 쓰기 쪽
    public readonly List<MoveGroup> Groups = new();
    readonly Dictionary<MoveGroup, int> groupIds = new(ReferenceEqualityComparer.Instance);
    public readonly List<NavRoute> Routes = new();
    readonly Dictionary<NavRoute, int> routeIds = new(ReferenceEqualityComparer.Instance);
    public readonly List<Entity> Tombs = new();
    readonly Dictionary<Entity, int> tombIds = new(ReferenceEqualityComparer.Instance);

    public int GroupId(MoveGroup? g)
    {
        if (g == null) return -1;
        if (!groupIds.TryGetValue(g, out int id)) { id = Groups.Count; Groups.Add(g); groupIds[g] = id; }
        return id;
    }

    public int RouteId(NavRoute? r)
    {
        if (r == null) return -1;
        if (!routeIds.TryGetValue(r, out int id)) { id = Routes.Count; Routes.Add(r); routeIds[r] = id; }
        return id;
    }

    /// <summary>엔티티 참조. 0 없음 · 1 월드에 있는 엔티티 (Id) · 2 제거된 엔티티 (묘비 번호).</summary>
    public void WriteEntity(SnapWriter w, Entity? e)
    {
        if (e == null) { w.Byte(0); return; }
        if (World.IsInWorld(e)) { w.Byte(1); w.I(e.Id); return; }
        if (!tombIds.TryGetValue(e, out int t)) { t = Tombs.Count; Tombs.Add(e); tombIds[e] = t; }
        w.Byte(2);
        w.I(t);
    }

    // ---- 읽기 쪽
    public MoveGroup[] ReadGroups = Array.Empty<MoveGroup>();
    public NavRoute[] ReadRoutes = Array.Empty<NavRoute>();
    public Entity[] ReadTombs = Array.Empty<Entity>();

    public Entity? ReadEntity(SnapReader r)
    {
        switch (r.Byte())
        {
            case 0: return null;
            case 1:
            {
                int id = r.I();
                return World.FindAny(id) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "missing entity " + id));
            }
            case 2:
            {
                int t = r.I();
                if ((uint)t >= (uint)ReadTombs.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "tomb index"));
                return ReadTombs[t];
            }
            default: throw new SnapshotException(string.Format(SnapMsg.Invalid, "entity ref tag"));
        }
    }

    public Unit? ReadUnit(SnapReader r) => ReadEntity(r) as Unit;
    public Building? ReadBuilding(SnapReader r) => ReadEntity(r) as Building;

    public MoveGroup? ReadGroup(SnapReader r)
    {
        int id = r.I();
        if (id < 0) return null;
        if (id >= ReadGroups.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "group index"));
        return ReadGroups[id];
    }

    public NavRoute? ReadRoute(SnapReader r)
    {
        int id = r.I();
        if (id < 0) return null;
        if (id >= ReadRoutes.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "route index"));
        return ReadRoutes[id];
    }

    /// <summary>제거된 엔티티를 흉내 내는 껍데기 (체력 0 · 월드에 없음) — 유닛이 아직 들고 있는 표적 참조를 그대로 복원한다.</summary>
    public void WriteTombs(SnapWriter w)
    {
        w.I(Tombs.Count);
        foreach (var e in Tombs)
        {
            w.Bool(e is Building);
            w.I(e.Id);
            w.Str(e is Unit u ? u.Def.Key : ((Building)e).Def.Key);
            w.I(e.Owner);
            w.Vec(e.Pos);
            w.F(e.Hp);
            if (e is Building b) { w.I(b.TileX); w.I(b.TileY); }
        }
    }

    public void ReadTombsTable(SnapReader r)
    {
        int n = r.Count(8);
        ReadTombs = new Entity[n];
        for (int i = 0; i < n; i++)
        {
            bool bld = r.Bool();
            int id = r.I();
            string key = r.Str() ?? "";
            int owner = r.I();
            var pos = r.Vec();
            float hp = r.F();
            if (bld)
            {
                int tx = r.I(), ty = r.I();
                if (!Defs.Buildings.TryGetValue(key, out var bd)) throw new SnapshotException(string.Format(SnapMsg.MissingDef, SnapMsg.DefBuilding, key));
                var b = new Building(id, bd, owner, tx, ty) { Hp = hp };
                ReadTombs[i] = b;
            }
            else
            {
                if (!Defs.Units.TryGetValue(key, out var ud)) throw new SnapshotException(string.Format(SnapMsg.MissingDef, SnapMsg.DefUnit, key));
                var u = new Unit(id, ud, owner, pos) { Hp = hp };
                ReadTombs[i] = u;
            }
        }
    }
}
