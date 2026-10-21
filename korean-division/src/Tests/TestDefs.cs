using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>테스트 전용 정의 (방향 변경 §0-1 로 건설 건물이 사라져, 길 막기 · 사선 · 안개 기억 테스트용 장애물 건물을 여기서 만든다). 저장 · 리플레이에는 쓰지 않는다.</summary>
internal static class TestDefs
{
    static readonly Dictionary<int, BuildingDef> walls = new();

    /// <summary>size×size 통행 불가 장애물 (파괴 가능, 점령 불가).</summary>
    public static BuildingDef Wall(int size = 2)
    {
        lock (walls)
        {
            if (!walls.TryGetValue(size, out var d)) walls[size] = d = new BuildingDef { Key = "test_wall" + size, Name = "벽", Size = size, Hp = 500, Sight = 2 };
            return d;
        }
    }

    /// <summary>
    /// 생성 맵의 숲 · 고지 장식 (지형 · 높이, GameMapTerrain.SprinkleForestsAndHills) 을 걷어낸다 — 지형 이전에 만든 교전 테스트는
    /// 빈 들판 · 평지 사격을 가정한다 (숲 · 잔해 · 철조망 · 참호 → 풀, 가옥 → 풀, 고지 → 평지). World 를 만들기 전에 부른다.
    /// </summary>
    public static GameMap Flat(GameMap m)
    {
        m.ClearProps();   // F6 소품도 걷는다
        for (int y = 0; y < m.Height; y++)
        for (int x = 0; x < m.Width; x++)
        {
            if (m.Get(x, y) >= TerrainType.Forest) m.Set(x, y, TerrainType.Grass);
            m.SetHigh(x, y, false);
        }
        return m;
    }

    /// <summary>
    /// 지휘 계통 지연 (balance.json "command") 을 잠시 끈다 — 지연 도입 전에 만든 이동 · 시간 측정 테스트용. using 블록이 끝나면 원래 값.
    /// </summary>
    public static IDisposable NoCommandDelay()
    {
        var c = Defs.Current.Command;
        var saved = (c.NcoMin, c.NcoMax, c.ActingMin, c.ActingMax, c.IsolatedMin, c.IsolatedMax);
        c.NcoMin = c.NcoMax = c.ActingMin = c.ActingMax = c.IsolatedMin = c.IsolatedMax = 0;
        return new Restore(() => (c.NcoMin, c.NcoMax, c.ActingMin, c.ActingMax, c.IsolatedMin, c.IsolatedMax) = saved);
    }

    sealed class Restore : IDisposable
    {
        readonly Action undo;
        public Restore(Action undo) => this.undo = undo;
        public void Dispose() => undo();
    }

    /// <summary>
    /// 표준 월드의 시작 병력 (start.force) 을 걷어낸다 — 옛 전투 테스트는 빈 들판을 가정한다. 전멸 패배를 막으려고 세력마다
    /// 점령 못 하는 비무장 1기 (없으면 보병 1기를 비무장으로) 만 남긴다. 1틱 진행한다.
    /// </summary>
    public static World Strip(World w)
    {
        var keep = new HashSet<Unit>();
        for (int f = 1; f < w.Factions.Count; f++)
        {
            var k = w.Units.Where(u => u.Owner == f && !u.Def.CanCapture && u.Weapon == null).OrderBy(u => u.Id).FirstOrDefault()
                    ?? w.Units.Where(u => u.Owner == f).OrderBy(u => u.Id).FirstOrDefault();
            if (k == null) continue;
            k.Weapon = null;
            keep.Add(k);
        }
        int scouts = w.Units.Count(u => u.Def.Scout && !keep.Contains(u));
        foreach (var u in w.Units) if (!keep.Contains(u)) u.Hp = 0;
        w.TestRewindIds(scouts);   // 시작 정찰병 (맨 마지막에 생성) 의 Id 를 되돌려 정찰병 도입 전 Id 로
        for (int f = 1; f < w.Factions.Count; f++) w.Hold(f, keep.Where(u => u.Owner == f));
        w.Step();
        return w;
    }
}
