using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 세력별 경계 지도 (ROADMAP §3): 8×8 칸 구역마다 "마지막으로 적을 식별(또는 피격)한 틱". 동맹은 표시 시점에 함께 찍는다.
/// 구역이 경계 상태(마지막 식별 후 World.AlertTicks 안)면 그 안의 유닛은 최대 시야로 보고 반응한다. 저장 · 체크섬 포함.
/// </summary>
public sealed class AlertGrid
{
    public const int CellSize = 8;
    public const int Never = -1;

    readonly int cols, rows;
    readonly int[][] cells;   // [세력] → cols*rows (0 = 중립, 쓰지 않지만 자리 유지)

    public AlertGrid(int mapWidth, int mapHeight, int factions)
    {
        cols = (mapWidth + CellSize - 1) / CellSize;
        rows = (mapHeight + CellSize - 1) / CellSize;
        cells = new int[factions][];
        for (int f = 0; f < factions; f++) { cells[f] = new int[cols * rows]; Array.Fill(cells[f], Never); }
    }

    public int Cols => cols;
    public int Rows => rows;
    public int Factions => cells.Length;

    public int CellOf(Vector2 p)
    {
        int cx = Math.Clamp((int)MathF.Floor(p.X) / CellSize, 0, cols - 1);
        int cy = Math.Clamp((int)MathF.Floor(p.Y) / CellSize, 0, rows - 1);
        return cy * cols + cx;
    }

    public int CellOf(int tileX, int tileY) => Math.Clamp(tileY / CellSize, 0, rows - 1) * cols + Math.Clamp(tileX / CellSize, 0, cols - 1);

    /// <summary>구역의 마지막 식별 틱 (Never = 없음).</summary>
    public int LastDetect(int faction, int cell) => (uint)faction < (uint)cells.Length ? cells[faction][cell] : Never;

    public bool IsAlerted(int faction, int cell, long tick, int timeout)
    {
        if ((uint)faction >= (uint)cells.Length) return false;
        int v = cells[faction][cell];
        return v >= 0 && tick - v <= timeout;
    }

    internal void Mark(int faction, int cell, int tick) => cells[faction][cell] = tick;
    internal int[] Cells(int faction) => cells[faction];

    /// <summary>[렌더러 · 테스트] 경계 상태인 구역 수.</summary>
    public int CountAlerted(int faction, long tick, int timeout)
    {
        int n = 0;
        var c = cells[faction];
        for (int i = 0; i < c.Length; i++) if (c[i] >= 0 && tick - c[i] <= timeout) n++;
        return n;
    }

    internal void WriteState(SnapWriter w)
    {
        w.I(cells.Length); w.I(cols); w.I(rows);
        for (int f = 0; f < cells.Length; f++)
            foreach (int v in cells[f]) w.I(v + 1);   // Never(-1) → 0 (1바이트)
    }

    internal void ReadState(SnapReader r)
    {
        int nf = r.I(), c = r.I(), rw = r.I();
        if (nf != cells.Length || c != cols || rw != rows) throw new SnapshotException(string.Format(SnapMsg.Invalid, "alert grid size"));
        for (int f = 0; f < nf; f++)
        {
            var arr = cells[f];
            for (int i = 0; i < arr.Length; i++) arr[i] = r.I() - 1;
        }
    }
}

/// <summary>시야 2단계 · 경계 · 은폐 탐지 (ROADMAP §3). 안개 쪽(칸 가시성 · CanSee)은 FogOfWar 가 여기 메서드를 부른다.</summary>
public sealed partial class World
{
    /// <summary>경계 유지 틱 (마지막 식별 후 10초).</summary>
    public const int AlertTicks = 600;
    /// <summary>은폐 탐지 판정 간격 (틱). 유닛 Id 로 분산.</summary>
    public const int DetectInterval = 60;

    /// <summary>세력별 경계 지도 (8×8 칸 구역 · 마지막 식별 틱 · 동맹 공유). 첫 사용 때 만든다 (세력 수가 정해진 뒤).</summary>
    [SnapExt] AlertGrid? alert;
    public AlertGrid Alert => alert ??= new AlertGrid(Map.Width, Map.Height, Factions.Count);

    // ---------------------------------------------------------------- 경계 (alert)

    /// <summary>세력(과 그 동맹)의 pos 구역에 "지금 적을 식별했다" 고 표시. 같은 틱에 이미 표시된 구역은 건너뛴다 (밀집 전투에서 세력 루프 생략).</summary>
    internal void MarkAlert(int faction, Vector2 pos)
    {
        if (faction <= 0 || faction >= Alert.Factions) return;
        int cell = Alert.CellOf(pos);
        int tick = (int)Tick;
        if (Alert.LastDetect(faction, cell) == tick) return;
        Alert.Mark(faction, cell, tick);
        for (int g = 1; g < Alert.Factions; g++)
            if (g != faction && Allied(faction, g)) Alert.Mark(g, cell, tick);
    }

    /// <summary>[렌더러 · AI] 유닛이 경계 상태인지 (자기 세력 경계 지도의 현재 구역).</summary>
    public bool IsAlerted(Unit u) => Alert.IsAlerted(u.Owner, Alert.CellOf(u.Pos), Tick, AlertTicks);

    /// <summary>[렌더러 · AI] 세력이 이 위치 구역에서 경계 상태인지.</summary>
    public bool IsAlertedAt(int faction, Vector2 pos) => Alert.IsAlerted(faction, Alert.CellOf(pos), Tick, AlertTicks);

    /// <summary>유닛의 현재 시야: 평소 sightNear, 경계 상태면 sightFar (둘 다 0 이면 sight) × 역할 (장교 vision.officerSightMul · 정찰병 scoutSightMul) × 고지 (terrain.highSightMul) × 날씨 · 시간대 (weather, 쌍안경 보너스는 시간대 binocular 비율만). 안개 · 자동 교전 반경 · 자기 시야 판정이 이 값을 쓴다.</summary>
    public int SightOf(Unit u)
    {
        var d = u.Def;
        int near = d.SightNear > 0 ? d.SightNear : d.Sight;
        int far = d.SightFar > 0 ? d.SightFar : d.Sight;
        int s = far <= near ? near : IsAlerted(u) ? far : near;
        float mul = 1f;
        if (d.IsOfficer) mul *= 1f + (Defs.Vision.OfficerSightMul - 1f) * Defs.Weather.BinocularMul(Weather.Time);   // 쌍안경 (밤에는 효과 줄어듦)
        if (d.Scout) mul *= Defs.Vision.ScoutSightMul;
        if (u.Dismounted && d.DismountSightMul > 0f) mul *= d.DismountSightMul;   // N72 하마 관측
        if (Map.IsHigh(u.Pos)) mul *= Defs.Terrain.HighSightMul;   // 고지
        int r = mul == 1f ? s : (int)MathF.Round(s * mul);
        return WeatherSight(r, WeatherSightMul);   // 날씨 · 시간대 (Weather.cs) — 정수 반경이라 지형 시야 메모 키가 그대로 맞다
    }

    void HashAlert(ref Hasher h)
    {
        for (int f = 1; f < Alert.Factions; f++)
        {
            var c = Alert.Cells(f);
            for (int i = 0; i < c.Length; i++)
                if (c[i] >= 0) { h.Add(f * 65536 + i); h.Add(c[i]); }
        }
    }

    // ---------------------------------------------------------------- 은폐 (concealment)

    /// <summary>현재 은폐 계수 0~100: 유닛 속성 (+ 지형 훅), 사격 직후(RevealUntil)는 0.</summary>
    public float ConcealmentOf(Unit u) => Tick <= u.RevealUntil ? 0 : BaseConcealment(u);

    /// <summary>사격 노출을 뺀 은폐 계수 (유닛 속성 + 지형 훅). 탐지 판정은 이 값을 쓴다 — 노출 3초는 그 자체로 보이고, 노출 중 판정으로 노출이 늘어나지 않게.</summary>
    public float BaseConcealment(Unit u)
    {
        float c = u.Def.Concealment;
        if (c > 0) c += TerrainConcealment(u);   // 숲 · 고지 훅 (Combat4.cs)
        return c < 0 ? 0 : c > 100 ? 100 : c;
    }

    /// <summary>은폐 판정 대상인지 (은폐 계수가 0 보다 큰 유닛만 표를 가진다).</summary>
    bool HasConcealment(Unit u) => u.Def.Concealment > 0;

    /// <summary>
    /// faction 이 유닛 u 를 (칸이 보인다는 전제에서) 식별하는지: 은폐 0 이거나 동맹이거나 탐지 유효 틱 안. FogOfWar.CanSee 가 부른다.
    /// </summary>
    internal bool Detected(int faction, Unit u)
    {
        if (u.Owner == faction || !HasConcealment(u) || Tick <= u.RevealUntil) return true;
        if (Allied(faction, u.Owner)) return true;
        var det = u.DetectedUntil;
        return det != null && (uint)faction < (uint)det.Length && det[faction] >= Tick;
    }

    /// <summary>
    /// 은폐 유닛의 탐지 판정 (DetectInterval 틱마다, 유닛 Id 로 분산). 칸이 보이는 적대 · 중립 세력마다
    /// 확률 1 − 은폐/100 으로 해시(틱 · 대상 Id · 세력) 판정 → 성공하면 다음 판정까지 보이고 그 세력의 구역이 경계 상태가 된다.
    /// </summary>
    void UpdateDetection(Unit u)
    {
        if ((Tick + u.Id) % DetectInterval != 0) return;   // 싼 검사 먼저 (매 틱 · 전 유닛)
        if (!HasConcealment(u)) return;
        long pt0 = Profile ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        var det = u.DetectedUntil ??= new int[Factions.Count];
        float p = 1f - BaseConcealment(u) / 100f;
        int until = (int)Tick + DetectInterval;
        int tile = Fog.TileIndex(u.Pos);
        for (int f = 1; f < det.Length; f++)
        {
            if (f == u.Owner || Allied(f, u.Owner)) continue;
            if (!Fog.VisibleAt(f, tile)) continue;
            if (Roll(u.Id, -f) < p) { det[f] = until; MarkAlert(f, u.Pos); }
        }
        if (Profile) ProfDetect += System.Diagnostics.Stopwatch.GetTimestamp() - pt0;
    }
}
