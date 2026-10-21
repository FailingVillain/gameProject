namespace KoreanDivision.Core;

public sealed partial class FogOfWar
{
    /// <summary>저장: 세력별 자기 시야 격자 (own) · 건물 기억. 보이는 격자 (동맹 합침) 는 불러올 때 own 에서 다시 만든다. 지형 시야 메모는 순수 함수라 저장하지 않는다.</summary>
    internal void WriteState(SnapWriter w)
    {
        w.Bool(Enabled);
        w.I(Version);
        w.L(stampEpoch);
        w.Bool(seeded);
        w.I(grids.Length);
        for (int f = 1; f < grids.Length; f++)
        {
            w.Raw(own[f]!);
            var mem = memory[f].Values.OrderBy(g => g.Id).ToList();
            w.I(mem.Count);
            foreach (var g in mem) { w.I(g.Id); w.Str(g.Def.Key); w.I(g.Owner); w.I(g.TileX); w.I(g.TileY); }
        }
    }

    internal void ReadState(SnapReader r, Defs defs)
    {
        Enabled = r.Bool();
        Version = r.I();
        stampEpoch = (uint)r.L() & 0xFFFFFF;
        seeded = r.Bool();
        int n = r.I();
        if (n != grids.Length) throw new SnapshotException(string.Format(SnapMsg.Invalid, "fog faction count"));
        for (int f = 1; f < n; f++)
        {
            r.Raw(own[f]!);
            memory[f].Clear();
            int c = r.Count(5);
            for (int i = 0; i < c; i++)
            {
                int id = r.I();
                string key = r.Str() ?? "";
                int owner = r.I(), tx = r.I(), ty = r.I();
                if (!defs.Buildings.TryGetValue(key, out var bd)) throw new SnapshotException(string.Format(SnapMsg.MissingDef, SnapMsg.DefBuilding, key));
                memory[f][id] = new BuildingGhost(id, bd, owner, tx, ty);
            }
        }
        RebuildShared();
        stampMark = Array.Empty<uint>();   // 작업용 (epoch 가 올라가면 어차피 무효)
    }
}
