using System.Numerics;
using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 월드 전체 상태의 저장 · 복원 (스냅샷 본문). 파일 형식(머리말 · 압축)은 Snapshot.cs.
/// 원칙: '이후 시뮬레이션에 영향을 주는 모든 것'을 저장한다 — 체크섬이 보는 값뿐 아니라 길찾기 캐시 · 이동 그룹 · AI 내부 상태 · 대기 명령까지.
/// 저장하지 않는 것: 화면 전용 (선택 · 이펙트 · 알림), 매 틱 다시 만드는 작업 버퍼 (공간 해시 · 충돌 해소 배열).
/// 같은 상태 → 같은 바이트 (딕셔너리 · 집합은 키 순서로 저장), 그래서 '저장 → 불러오기 → 저장' 이 바이트 단위로 같고
/// 이어서 돌린 결과를 바이트 단위로 비교할 수 있다.
/// </summary>
public sealed partial class World
{
    /// <summary>[테스트 전용] 일부러 복원을 빼먹어 동등성 테스트가 실제로 어긋남을 잡아내는지 확인하는 음성 대조 (nav / ai).</summary>
    internal static string? DebugBreak;

    internal bool IsInWorld(Entity e) => byId.TryGetValue(e.Id, out var x) && ReferenceEquals(x, e);
    /// <summary>살아 있는지와 무관하게 Id 로 엔티티를 찾는다 (이번 틱에 죽었지만 아직 목록에 남은 것 포함).</summary>
    internal Entity? FindAny(int id) => byId.TryGetValue(id, out var e) ? e : null;

    /// <summary>이 월드가 스냅샷에서 시작했다면 그 파일 내용 (리플레이가 시작 지점으로 첨부한다). 없으면 null.</summary>
    public byte[]? OriginSnapshot { get; internal set; }
    /// <summary>OriginSnapshot 의 틱.</summary>
    public long OriginTick { get; internal set; }

    /// <summary>대기 중인 (아직 실행 안 된) 명령을 버린다 — 스냅샷에서 시작한 리플레이 월드는 명령을 기록에서 받는다.</summary>
    internal void ClearPendingCommands() => pending.Clear();

    /// <summary>저장 직전 정리: 맵 변경에 따른 길찾기 갱신을 마무리한다 (다음 Step 이 어차피 가장 먼저 하는 일 — 의미는 같다).</summary>
    void NormalizeForSave()
    {
        if (Map.Version != lastMapVersion) RefreshPaths();
        nav?.Sync();
    }

    // ================================================================ v0.4 확장 구역 (구현은 기능별 partial 파일)
    // 탄약 회복 · 보급 마차 · 엄폐 · 참호 (Supply*.cs · Cover.cs) / 전투 · 시야 · 은폐 · 포탑 (Combat2*.cs) / 전투 탭 설정 등 명령 쪽 상태 (CommandExt*.cs).
    // 쓰는 순서와 읽는 순서는 같아야 한다. 구현이 없으면 빈 구역이 된다.
    partial void WriteSupplyExtImpl(SnapWriter w, SnapCtx ctx);
    partial void ReadSupplyExtImpl(SnapReader r, SnapCtx ctx);
    partial void WriteCombatExtImpl(SnapWriter w, SnapCtx ctx);
    partial void ReadCombatExtImpl(SnapReader r, SnapCtx ctx);
    partial void WriteCommandExtImpl(SnapWriter w, SnapCtx ctx);
    partial void ReadCommandExtImpl(SnapReader r, SnapCtx ctx);
    void WriteSupplyExt(SnapWriter w, SnapCtx ctx) => WriteSupplyExtImpl(w, ctx);
    void ReadSupplyExt(SnapReader r, SnapCtx ctx) => ReadSupplyExtImpl(r, ctx);
    void WriteCombatExt(SnapWriter w, SnapCtx ctx) => WriteCombatExtImpl(w, ctx);
    void ReadCombatExt(SnapReader r, SnapCtx ctx) => ReadCombatExtImpl(r, ctx);
    void WriteCommandExt(SnapWriter w, SnapCtx ctx) => WriteCommandExtImpl(w, ctx);
    void ReadCommandExt(SnapReader r, SnapCtx ctx) => ReadCommandExtImpl(r, ctx);

    // ================================================================ 쓰기

    internal void WriteBody(SnapWriter o)
    {
        NormalizeForSave();
        var ctx = new SnapCtx(this, Defs);

        var map = new SnapWriter(); Map.WriteState(map);
        var wr = new SnapWriter(); WriteScalars(wr);
        var cmd = new SnapWriter(); WriteCommands(cmd, ctx);
        var ents = new SnapWriter(); WriteEntities(ents);
        var links = new SnapWriter(); WriteLinks(links, ctx);
        var org = new SnapWriter(); WriteOrganisation(org, ctx);   // 편제 (Organisation.cs) — 깃발의 그룹 · 경로가 표 번호를 받도록 WriteGroups 전에
        var grp = new SnapWriter(); WriteGroups(grp, ctx);
        var nv = new SnapWriter();
        nv.Bool(nav != null);
        nav?.WriteState(nv, ctx);
        var fog = new SnapWriter(); Fog.WriteState(fog);
        var ai = new SnapWriter(); WriteControllers(ai, ctx);
        // v0.4 확장 구역: 기능별 partial 파일이 자기 상태를 쓴다 (필드에는 [SnapExt] 표시 — 저장 완전성 감사가 인정)
        var supl = new SnapWriter(); WriteSupplyExt(supl, ctx);
        var cmb2 = new SnapWriter(); WriteCombatExt(cmb2, ctx);
        var uiex = new SnapWriter(); WriteCommandExt(uiex, ctx);
        // 표들은 모든 참조가 모인 뒤에 쓴다 (번호는 이미 정해짐)
        var routes = new SnapWriter();
        routes.I(ctx.Routes.Count);
        foreach (var r in ctx.Routes) Nav.WriteRoute(routes, r);
        var tombs = new SnapWriter(); ctx.WriteTombs(tombs);

        o.Tag("MAP_"); o.Raw(map.Buffer.Span);
        o.Tag("WRLD"); o.Raw(wr.Buffer.Span);
        o.Tag("CMDS"); o.Raw(cmd.Buffer.Span);
        o.Tag("ENTS"); o.Raw(ents.Buffer.Span);
        o.Tag("TOMB"); o.Raw(tombs.Buffer.Span);
        o.Tag("ROUT"); o.Raw(routes.Buffer.Span);
        o.Tag("GRPS"); o.Raw(grp.Buffer.Span);
        o.Tag("LINK"); o.Raw(links.Buffer.Span);
        o.Tag("NAV_"); o.Raw(nv.Buffer.Span);
        o.Tag("FOG_"); o.Raw(fog.Buffer.Span);
        o.Tag("AIS_"); o.Raw(ai.Buffer.Span);
        o.Tag("SUPL"); o.Raw(supl.Buffer.Span);
        o.Tag("CMB2"); o.Raw(cmb2.Buffer.Span);
        o.Tag("CMDX"); o.Raw(uiex.Buffer.Span);
        o.Tag("ORG_"); o.Raw(org.Buffer.Span);
        var wth = new SnapWriter(); Weather.Write(wth);
        o.Tag("WTHR"); o.Raw(wth.Buffer.Span);   // 날씨 · 시간대 (FormatVersion 12)
        o.Tag("MISN"); WriteMission(o);   // 미션 상태 (Mission/MissionState.cs, FormatVersion 11)
        o.Tag("END_");
    }

    void WriteScalars(SnapWriter w)
    {
        w.I(Factions.Count - 1);
        w.L(Tick);
        w.I(nextId);
        w.I(lastMapVersion);
        w.I(navEpoch);
        w.I(Winner);
        w.Bool(CheatsUsed);
        w.Bool(BattleMode);
        w.Str(Battle == null ? null : JsonSerializer.Serialize(Battle));
        w.Bool(RecordCommands);
        w.I(Winners.Count);
        foreach (int x in Winners) w.I(x);
        foreach (var f in Factions) f.WriteState(w);
        var la = LastAttacked.OrderBy(kv => kv.Key).ToList();
        w.I(la.Count);
        foreach (var (k, (pos, tick)) in la) { w.I(k); w.Vec(pos); w.L(tick); }
        w.I(ChecksumLog.Count);
        foreach (var c in ChecksumLog) { w.L(c.Tick); w.U64(c.Checksum); }
    }

    void WriteCommands(SnapWriter w, SnapCtx ctx)
    {
        var codec = new ObjCodec(ctx);
        w.I(pending.Count);
        foreach (var c in pending) codec.Write(w, c, typeof(GameCommand));
    }

    void WriteEntities(SnapWriter w)
    {
        w.I(Units.Count);
        foreach (var u in Units)
        {
            w.I(u.Id); w.Str(u.Def.Key); w.I(u.Owner); w.Vec(u.Pos); w.F(u.Hp); w.L(u.LastDamagedTick);
            w.Str(u.Weapon?.Key); w.Str(u.Armor?.Key);
        }
        w.I(Buildings.Count);
        foreach (var b in Buildings)
        {
            w.I(b.Id); w.Str(b.Def.Key); w.I(b.Owner); w.I(b.TileX); w.I(b.TileY); w.F(b.Hp); w.L(b.LastDamagedTick);
            w.I(b.CaptureFaction); w.F(b.CaptureProgress); w.Bool(b.Contested);
        }
    }

    void WriteLinks(SnapWriter w, SnapCtx ctx)
    {
        foreach (var u in Units)
        {
            w.Byte((byte)u.Order);
            w.Vec(u.Velocity); w.F(u.Facing);
            w.I(ctx.GroupId(u.Group)); w.I(ctx.GroupId(u.LastGroup)); w.I(ctx.GroupId(u.FormGroup));
            byte tok = 0;
            if (u.NavToken is { } t)
                tok = ReferenceEquals(t, u.Group) ? (byte)1 : ReferenceEquals(t, u.LastGroup) ? (byte)2
                    : ReferenceEquals(t, u.FormGroup) ? (byte)4 : ReferenceEquals(t, u.Target) ? (byte)5 : t is Entity ? (byte)7 : (byte)6;   // 6 = 낡은 목적 (어느 것과도 같지 않음 — 다음 길찾기에서 초기화) · 7 = 다른 엔티티 (제거된 것은 묘비로 같은 객체 복원)
            w.Byte(tok);
            if (tok == 7) ctx.WriteEntity(w, (Entity)u.NavToken!);
            w.I(ctx.RouteId(u.NavRoute));
            w.I(u.NavSeg); w.I(u.NavEpoch); w.I(u.NavRawX); w.I(u.NavRawY); w.I(u.NavGX); w.I(u.NavGY);
            w.L(u.NextLosTick); w.L(u.NextPersonalTick); w.Bool(u.LosOk); w.Vec(u.LosGoal);
            w.Vec(u.FormOff);
            ctx.WriteEntity(w, u.Target);
            w.F(u.Cooldown); w.L(u.NextAcquireTick);
            w.I(u.AimTarget); w.I(u.AimLeft); w.I(u.HoldLeft); w.L(u.EngagedTick);
            w.L(u.LastMoveTick); w.Vec(u.LeashOrigin); w.L(u.LastShotTick); w.L(u.LastOrderTick); w.I(u.ReactDelay);
            w.Byte(u.NavKind);
            w.Vec(u.ProgPos); w.L(u.ProgTick); w.L(u.ProgLast); w.L(u.NudgeUntil); w.I(u.StuckLevel); w.Vec(u.NudgeDir);
        }
    }

    void WriteGroups(SnapWriter w, SnapCtx ctx)
    {
        // 그룹 표는 번호가 모두 정해진 뒤 (Groups 는 위 WriteLinks 가 채웠다)
        w.I(ctx.Groups.Count);
        foreach (var g in ctx.Groups)
        {
            w.Vec(g.Target); w.Vec(g.Goal); w.F(g.ArriveRadius); w.I(g.MemberCount);
            w.Bool(g.Big); w.Bool(g.Inited);
            w.Bool(g.Members != null);
            if (g.Members != null) { w.I(g.Members.Count); foreach (var m in g.Members) ctx.WriteEntity(w, m); }
            ctx.WriteEntity(w, g.Leader);
            w.I(ctx.RouteId(g.Route)); w.I(ctx.RouteId(g.RouteL));
            w.Bool(g.Plan != null);
            if (g.Plan is { } p)
            {
                w.F(p.Speed); w.F(p.Facing); w.Vec(p.Dir); w.F(p.Extent);
                var off = p.Off.OrderBy(kv => kv.Key).ToList();
                w.I(off.Count);
                foreach (var kv in off) { w.I(kv.Key); w.Vec(kv.Value); }
            }
        }
    }

    void WriteControllers(SnapWriter w, SnapCtx ctx)
    {
        var instances = new List<IAiController>();
        w.I(Controllers.Count);
        foreach (var (faction, ai) in Controllers)
        {
            w.I(faction);
            int idx = instances.FindIndex(x => ReferenceEquals(x, ai));
            if (idx >= 0) { w.I(idx); continue; }
            instances.Add(ai);
            w.I(-1);
            if (AiRegistry.Create(ai.Name) is not { } fresh || fresh.GetType() != ai.GetType())
                throw new SnapshotException(string.Format(SnapMsg.UnknownAi, ai.Name));
            w.Str(ai.Name);
            new ObjCodec(ctx).WriteFields(w, ai);
        }
    }

    // ================================================================ 읽기

    internal static World ReadBody(SnapReader r, Defs defs)
    {
        var map = ReadMapSection(r);
        r.Tag("WRLD");
        int fcount = r.I();
        if (fcount < 1 || fcount > 64) throw new SnapshotException(string.Format(SnapMsg.Invalid, "faction count"));
        var world = new World(map, defs, fcount);
        var ctx = new SnapCtx(world, defs);
        world.ReadScalars(r, defs);
        r.Tag("CMDS");
        world.ReadCommands(r, ctx);
        r.Tag("ENTS");
        var units = world.ReadEntities(r, defs);
        r.Tag("TOMB");
        ctx.ReadTombsTable(r);
        r.Tag("ROUT");
        int nr = r.Count(10);
        ctx.ReadRoutes = new NavRoute[nr];
        for (int i = 0; i < nr; i++) ctx.ReadRoutes[i] = Nav.ReadRoute(r);
        r.Tag("GRPS");
        world.ReadGroups(r, ctx, defs);
        r.Tag("LINK");
        world.ReadLinks(r, ctx, units, defs);
        r.Tag("NAV_");
        if (r.Bool())
        {
            world.nav = null;
            world.Nav.ReadState(r, ctx);
            if (DebugBreak == "nav") { world.nav = null; _ = world.Nav; }   // [테스트 전용 음성 대조] 길찾기 캐시를 버리면 어긋나는지
        }
        else world.nav = null;
        r.Tag("FOG_");
        world.Fog.ReadState(r, defs);
        r.Tag("AIS_");
        world.ReadControllers(r, ctx);
        if (DebugBreak == "ai") foreach (var (f, ai) in world.Controllers.ToList()) { world.Controllers.Remove((f, ai)); world.Controllers.Add((f, AiRegistry.Create(ai.Name)!)); }
        r.Tag("SUPL"); world.ReadSupplyExt(r, ctx);
        r.Tag("CMB2"); world.ReadCombatExt(r, ctx);
        r.Tag("CMDX"); world.ReadCommandExt(r, ctx);
        r.Tag("ORG_"); world.ReadOrganisation(r, ctx, defs);
        r.Tag("WTHR"); world.Weather.Read(r);
        r.Tag("MISN"); world.ReadMission(r);
        r.Tag("END_");
        world.hashDirty = true;
        return world;
    }

    static GameMap ReadMapSection(SnapReader r)
    {
        r.Tag("MAP_");
        return GameMap.ReadState(r);
    }

    void ReadScalars(SnapReader r, Defs defs)
    {
        Tick = r.L();
        nextId = r.I();
        lastMapVersion = r.I();
        navEpoch = r.I();
        Winner = r.I();
        CheatsUsed = r.Bool();
        BattleMode = r.Bool();
        string? battle = r.Str();
        Battle = battle == null ? null : JsonSerializer.Deserialize<BattleSetup>(battle) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "battle setup"));
        RecordCommands = r.Bool();
        Winners.Clear();
        int nw = r.Count();
        for (int i = 0; i < nw; i++) Winners.Add(r.I());
        foreach (var f in Factions) f.ReadState(r);
        LastAttacked.Clear();
        int la = r.Count(4);
        for (int i = 0; i < la; i++) LastAttacked[r.I()] = (r.Vec(), r.L());
        ChecksumLog.Clear();
        int nc = r.Count(2);
        for (int i = 0; i < nc; i++) ChecksumLog.Add(new ChecksumRecord(r.L(), r.U64()));
    }

    void ReadCommands(SnapReader r, SnapCtx ctx)
    {
        var codec = new ObjCodec(ctx);
        pending.Clear();
        int n = r.Count();
        for (int i = 0; i < n; i++) pending.Add((GameCommand)(codec.Read(r, typeof(GameCommand)) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "command"))));
    }

    List<Unit> ReadEntities(SnapReader r, Defs defs)
    {
        int nu = r.Count(10);
        var units = new List<Unit>(nu);
        for (int i = 0; i < nu; i++)
        {
            int id = r.I();
            string key = r.Str() ?? "";
            int owner = r.I();
            var pos = r.Vec();
            if (!defs.Units.TryGetValue(key, out var ud)) throw new SnapshotException(string.Format(SnapMsg.MissingDef, SnapMsg.DefUnit, key));
            var u = new Unit(id, ud, owner, pos) { Hp = r.F(), LastDamagedTick = r.L() };
            u.Weapon = defs.Weapon(r.Str());
            u.Armor = defs.Armor(r.Str());
            if (byId.ContainsKey(id)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "duplicate id " + id));
            Units.Add(u);
            byId[id] = u;
            units.Add(u);  IndexInvalidate(-1);
        }
        int nb = r.Count(10);
        for (int i = 0; i < nb; i++)
        {
            int id = r.I();
            string key = r.Str() ?? "";
            int owner = r.I(), tx = r.I(), ty = r.I();
            if (!defs.Buildings.TryGetValue(key, out var bd)) throw new SnapshotException(string.Format(SnapMsg.MissingDef, SnapMsg.DefBuilding, key));
            var b = new Building(id, bd, owner, tx, ty) { Hp = r.F(), LastDamagedTick = r.L() };
            b.CaptureFaction = r.I(); b.CaptureProgress = r.F(); b.Contested = r.Bool();
            if (byId.ContainsKey(id)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "duplicate id " + id));
            Buildings.Add(b);
            byId[id] = b;
            Map.RestoreOccupant(tx, ty, bd.Size, id, bd.City);
        }
        return units;
    }

    void ReadGroups(SnapReader r, SnapCtx ctx, Defs defs)
    {
        int n = r.Count(10);
        ctx.ReadGroups = new MoveGroup[n];
        // 그룹 구성원 · 리더 참조는 엔티티가 모두 만들어진 뒤라 바로 풀 수 있다
        for (int i = 0; i < n; i++)
        {
            var target = r.Vec();
            var goal = r.Vec();
            float arrive = r.F();
            int members = r.I();
            if (members < 0 || members > 1_000_000) throw new SnapshotException(string.Format(SnapMsg.Invalid, "group size"));
            var g = new MoveGroup(this, target, members);
            g.Goal = goal; g.Field = new FlowField(goal); g.ArriveRadius = arrive;
            g.Big = r.Bool(); g.Inited = r.Bool();
            if (r.Bool())
            {
                int nm = r.Count();
                g.Members = new List<Unit>(nm);
                for (int k = 0; k < nm; k++) g.Members.Add(ctx.ReadUnit(r) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "group member")));
            }
            else g.Members = null;
            g.Leader = ctx.ReadUnit(r);
            ctx.ReadGroups[i] = g;
            // 경로 참조는 표가 이미 읽혀 있다
            g.Route = ctx.ReadRoute(r);
            g.RouteL = ctx.ReadRoute(r);
            if (r.Bool())
            {
                var p = new FormPlan { Speed = r.F(), Facing = r.F(), Dir = r.Vec(), Extent = r.F() };
                int no = r.Count(5);
                for (int k = 0; k < no; k++) p.Off[r.I()] = r.Vec();
                g.Plan = p;
            }
        }
    }

    void ReadLinks(SnapReader r, SnapCtx ctx, List<Unit> units, Defs defs)
    {
        foreach (var u in units)
        {
            byte order = r.Byte();
            if (order > (byte)UnitOrder.GroundFire) throw new SnapshotException(string.Format(SnapMsg.Invalid, "unit order"));
            u.Order = (UnitOrder)order;
            u.Velocity = r.Vec(); u.Facing = r.F();
            u.Group = ctx.ReadGroup(r); u.LastGroup = ctx.ReadGroup(r); u.FormGroup = ctx.ReadGroup(r);
            byte tok = r.Byte();
            Entity? tokEnt = tok == 7 ? ctx.ReadEntity(r) : null;
            u.NavRoute = ctx.ReadRoute(r);
            u.NavSeg = r.I(); u.NavEpoch = r.I(); u.NavRawX = r.I(); u.NavRawY = r.I(); u.NavGX = r.I(); u.NavGY = r.I();
            u.NextLosTick = r.L(); u.NextPersonalTick = r.L(); u.LosOk = r.Bool(); u.LosGoal = r.Vec();
            u.FormOff = r.Vec();
            u.Target = ctx.ReadEntity(r);
            u.Cooldown = r.F(); u.NextAcquireTick = r.L();
            u.AimTarget = r.I(); u.AimLeft = r.I(); u.HoldLeft = r.I(); u.EngagedTick = r.L();
            u.LastMoveTick = r.L(); u.LeashOrigin = r.Vec(); u.LastShotTick = r.L(); u.LastOrderTick = r.L(); u.ReactDelay = r.I();
            u.NavKind = r.Byte();
            u.ProgPos = r.Vec(); u.ProgTick = r.L(); u.ProgLast = r.L(); u.NudgeUntil = r.L(); u.StuckLevel = r.I(); u.NudgeDir = r.Vec();
            u.NavToken = tok switch
            {
                0 => null, 1 => u.Group, 2 => u.LastGroup, 4 => u.FormGroup, 5 => u.Target,
                6 => new object(), 7 => tokEnt ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "nav token")), _ => throw new SnapshotException(string.Format(SnapMsg.Invalid, "nav token")),
            };
        }
    }

    void ReadControllers(SnapReader r, SnapCtx ctx)
    {
        Controllers.Clear();
        var instances = new List<IAiController>();
        int n = r.Count(2);
        for (int i = 0; i < n; i++)
        {
            int faction = r.I();
            int idx = r.I();
            if (idx >= 0)
            {
                if (idx >= instances.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "ai instance"));
                Controllers.Add((faction, instances[idx]));
                continue;
            }
            string name = r.Str() ?? "";
            var ai = AiRegistry.Create(name) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "unknown ai " + name));
            new ObjCodec(ctx).ReadFieldsInto(r, ai);
            instances.Add(ai);
            Controllers.Add((faction, ai));
        }
    }
}
