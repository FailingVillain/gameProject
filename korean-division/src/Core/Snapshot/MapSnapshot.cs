namespace KoreanDivision.Core;

public sealed partial class GameMap
{
    /// <summary>스냅샷: 지형 · 고지 · 시작 위치 · 시설 · 메타 · 통행 버전. 점유(건물 발자국)는 건물 목록에서 다시 만든다 (RestoreOccupants).</summary>
    internal void WriteState(SnapWriter w)
    {
        w.I(Width); w.I(Height); w.I(Seed);
        w.Str(Id); w.Str(DisplayName); w.Str(FileHash);
        w.Bool(Mirrored);
        w.I(Version);
        var t = new byte[Width * Height];
        for (int i = 0; i < t.Length; i++) t[i] = (byte)tiles[i];
        w.Bytes(t);
        var hb = new byte[Width * Height];   // 고지 (FormatVersion 10)
        for (int i = 0; i < hb.Length; i++) hb[i] = high[i] ? (byte)1 : (byte)0;
        w.Bytes(hb);
        w.I(StartTiles.Count);
        foreach (var (x, y) in StartTiles) { w.I(x); w.I(y); }
        w.I(ResourceSites.Count);
        foreach (var (x, y, owner, kind) in ResourceSites) { w.I(x); w.I(y); w.I(owner); w.Str(kind); }
        w.I(UnitSpawns.Count);
        foreach (var sp in UnitSpawns) { w.Str(sp.Unit); w.I(sp.Faction); w.Vec(sp.Pos); }
        var fd = MapFile.FactionsOf(this);
        w.I(fd.Count);
        foreach (var f in fd)
        {
            w.Str(f.Name); w.Str(f.En); w.Str(f.Lang);
            w.I(f.Color?.Length ?? -1);
            if (f.Color != null) foreach (int c in f.Color) w.I(c);
        }
        w.I(PropSeeds.Count);   // F6 소품 자리 (FormatVersion 42) - 막는 칸은 종류 정의에서 다시 구한다
        foreach (var ps in PropSeeds) { w.F(ps.X); w.F(ps.Y); w.Str(ps.Type); w.Bool(IsPropBlocked((int)MathF.Floor(ps.X), (int)MathF.Floor(ps.Y))); }
    }

    internal static GameMap ReadState(SnapReader r)
    {
        int w = r.I(), h = r.I(), seed = r.I();
        if (w < 1 || h < 1 || w > 4096 || h > 4096) throw new SnapshotException(string.Format(SnapMsg.Invalid, "map size"));
        var map = new GameMap(w, h, seed);
        map.Id = r.Str(); map.DisplayName = r.Str(); map.FileHash = r.Str();
        map.Mirrored = r.Bool();
        int version = r.I();
        var t = r.Bytes();
        if (t.Length != w * h) throw new SnapshotException(string.Format(SnapMsg.Invalid, "terrain size"));
        int kinds = Enum.GetValues<TerrainType>().Length;
        for (int i = 0; i < t.Length; i++)
        {
            if (t[i] >= kinds) throw new SnapshotException(string.Format(SnapMsg.Invalid, "terrain type"));
            map.Set(i % w, i / w, (TerrainType)t[i]);
        }
        var hb = r.Bytes();
        if (hb.Length != w * h) throw new SnapshotException(string.Format(SnapMsg.Invalid, "height size"));
        for (int i = 0; i < hb.Length; i++) map.SetHigh(i % w, i / w, hb[i] != 0);
        int ns = r.Count(2);
        for (int i = 0; i < ns; i++) map.StartTiles.Add((r.I(), r.I()));
        int nr = r.Count(4);
        for (int i = 0; i < nr; i++) map.ResourceSites.Add((r.I(), r.I(), r.I(), r.Str() ?? ""));
        int nu = r.Count(4);
        for (int i = 0; i < nu; i++) map.UnitSpawns.Add(new UnitSpawn(r.Str() ?? "", r.I(), r.Vec()));
        int nf = r.Count(4);
        var defs = new List<FactionDef>();
        for (int i = 0; i < nf; i++)
        {
            var f = new FactionDef { Name = r.Str() ?? "", En = r.Str() ?? "", Lang = r.Str() ?? "" };
            int cl = r.I();
            if (cl >= 0) { f.Color = new int[cl]; for (int k = 0; k < cl; k++) f.Color[k] = r.I(); }
            defs.Add(f);
        }
        if (defs.Count > 0) MapFile.AttachFactions(map, defs);
        int np = r.Count(6);
        var pd = PropDefs.Current;
        for (int i = 0; i < np; i++)
        {
            float px = r.F(), py = r.F();
            string pt = r.Str() ?? "";
            bool blocked = r.Bool();
            if (!float.IsFinite(px) || !float.IsFinite(py) || !pd.TryGet(pt, out _) || !map.InBounds((int)MathF.Floor(px), (int)MathF.Floor(py)))
                throw new SnapshotException(string.Format(SnapMsg.Invalid, "prop seed"));
            map.AddPropSeed(px, py, pt, blocked);
        }
        map.Version = version;
        return map;
    }

    /// <summary>건물 발자국 점유 복원 (통행 버전 · 변경 기록은 건드리지 않는다).</summary>
    internal void RestoreOccupant(int tx, int ty, int size, int id, bool walkOn)
    {
        for (int y = ty; y < ty + size; y++)
        for (int x = tx; x < tx + size; x++)
            if (InBounds(x, y)) { occupant[y * Width + x] = id; passable[y * Width + x] = id != 0 && walkOn; }
    }
}

public sealed partial class MapFile
{
    /// <summary>불러온 스냅샷의 세력 정의를 지도에 붙인다 (이름 · 색 · 언어).</summary>
    internal static void AttachFactions(GameMap map, List<FactionDef> defs) => factionTable.Add(map, defs);
}
