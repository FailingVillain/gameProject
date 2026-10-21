namespace KoreanDivision.Core;

/// <summary>
/// 길찾기 캐시의 저장 · 복원.
/// 길찾기 결과는 캐시 내용에 좌우된다 (같은 경로 공유 · 틱당 작업량 한도 · 흐름장 한도 · 제거 순서). 그래서 '결과에 영향을 주는' 상태를 모두 저장한다:
///  · 구역 버전 (흐름장 유효성) · 경로 객체 전부 (공유 사전 · 대기열 · 개인 경로 · 유닛이 들고 있는 낡은 경로) · 경로 일련번호
///  · 흐름장 목록 (구역 · 목표 · 버전 · 마지막 사용 틱). 흐름장의 비용 격자는 저장하지 않고 불러올 때 다시 계산한다 (지도의 순수 함수).
/// 출입구 그래프 · 구역 안 비용은 지도에서 다시 만든다 (증분 갱신 결과 = 처음부터 만든 결과; NavSelfTest 가 보증).
/// </summary>
internal sealed partial class Nav
{
    internal static void WriteRoute(SnapWriter w, NavRoute r)
    {
        w.L(r.Key);
        w.Byte((byte)((r.Valid ? 1 : 0) | (r.Pending ? 2 : 0) | (r.Failed ? 4 : 0) | (r.Approx ? 8 : 0)));
        w.I(r.SX); w.I(r.SY); w.I(r.GX); w.I(r.GY);
        w.I(r.GoalX); w.I(r.GoalY); w.I(r.GoalSector); w.I(r.GoalRegion);
        w.I(r.Segs.Length);
        foreach (var s in r.Segs) { w.I(s.Region); w.I(s.ExitX); w.I(s.ExitY); w.I(s.PeerX); w.I(s.PeerY); }
        w.I(r.Sectors.Length);
        foreach (int s in r.Sectors) w.I(s);
        w.I(r.StartRegion);
        w.L(r.LastUsed); w.L(r.Seq); w.I(r.Work);
    }

    internal static NavRoute ReadRoute(SnapReader r)
    {
        var n = new NavRoute { Key = r.L() };
        byte f = r.Byte();
        n.Valid = (f & 1) != 0; n.Pending = (f & 2) != 0; n.Failed = (f & 4) != 0; n.Approx = (f & 8) != 0;
        n.SX = r.I(); n.SY = r.I(); n.GX = r.I(); n.GY = r.I();
        n.GoalX = r.I(); n.GoalY = r.I(); n.GoalSector = r.I(); n.GoalRegion = r.I();
        int ns = r.Count(5);
        n.Segs = new NavSeg[ns];
        for (int i = 0; i < ns; i++) n.Segs[i] = new NavSeg { Region = r.I(), ExitX = r.I(), ExitY = r.I(), PeerX = r.I(), PeerY = r.I() };
        int nc = r.Count();
        n.Sectors = new int[nc];
        for (int i = 0; i < nc; i++) n.Sectors[i] = r.I();
        n.StartRegion = r.I();
        n.LastUsed = r.L(); n.Seq = r.L(); n.Work = r.I();
        return n;
    }

    internal void WriteState(SnapWriter w, SnapCtx ctx)
    {
        Sync();
        w.I(sectors.Length);
        foreach (var s in sectors) w.I(s.Version);
        w.L(routeSeq);
        var rs = routes.OrderBy(kv => kv.Key).ToList();
        w.I(rs.Count);
        foreach (var kv in rs) { w.L(kv.Key); w.I(ctx.RouteId(kv.Value)); }
        w.I(pending.Count);
        foreach (var r in pending) w.I(ctx.RouteId(r));
        var fs = fields.OrderBy(kv => kv.Key).ToList();
        w.I(fs.Count);
        foreach (var kv in fs)
        {
            var f = kv.Value;
            w.I(f.Sector); w.I(f.TX); w.I(f.TY); w.I(f.Version); w.L(f.LastUsed);
        }
        w.Bool(large != null);
        large?.WriteState(w, ctx);
    }

    internal void ReadState(SnapReader r, SnapCtx ctx)
    {
        Sync();
        int ns = r.I();
        if (ns != sectors.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "nav sector count"));
        foreach (var s in sectors) s.Version = r.I();
        routeSeq = r.L();
        routes.Clear();
        int nr = r.Count(2);
        for (int i = 0; i < nr; i++)
        {
            long key = r.L();
            routes[key] = ctx.ReadRoute(r) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "route dictionary"));
        }
        pending.Clear();
        int np = r.Count();
        for (int i = 0; i < np; i++) pending.Enqueue(ctx.ReadRoute(r) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "route queue")));
        fields.Clear();
        int nf = r.Count(5);
        for (int i = 0; i < nf; i++)
        {
            int sid = r.I(), tx = r.I(), ty = r.I(), ver = r.I();
            long used = r.L();
            if ((uint)sid >= (uint)sectors.Length || !map.InBounds(tx, ty)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "field position"));
            var s = sectors[sid];
            var f = new NavField { Sector = sid, TX = tx, TY = ty, Version = ver, LastUsed = used, Cost = new float[SS * SS] };
            if (ver == s.Version) LocalDijkstra(s, tx, ty, f.Cost);   // 낡은 흐름장은 다시 쓰이기 전에 항상 재계산되므로 비용 격자가 필요 없다
            fields[((long)sid << 32) | (uint)(ty * map.Width + tx)] = f;
        }
        if (r.Bool()) Large.ReadState(r, ctx);
    }

    /// <summary>[테스트] 이 그래프의 캐시 현황 지문 (경로 · 흐름장 개수 · 일련번호).</summary>
    internal string CacheSummary() => $"routes={routes.Count} pending={pending.Count} fields={fields.Count} seq={routeSeq}" + (large != null ? $" | large: {large.CacheSummary()}" : "");
}
