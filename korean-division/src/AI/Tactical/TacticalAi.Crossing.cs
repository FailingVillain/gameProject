using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 도하 · 병목 돌파 AI 수치 (assets/data/tactics.json "crossing", 사용자 결정 2026-10-19). 밸런스 수치 아님 — AI 판단 문턱만. 없으면 내장 기본값.
/// </summary>
public sealed class CrossingAiDef
{
    /// <summary>false = 도하 계획 없음 (옛 동작: 가까운 강둑에서 사격전).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>장애물 (못 걷는 칸 띠) 이 이 칸 이상 두꺼워야 도하로 본다 (가옥 한 채 · 얇은 벽은 길찾기로 돌아감).</summary>
    public int MinObstacle { get; set; } = 3;
    /// <summary>장애물 띠가 이보다 두꺼우면 (호수 등) 도하로 다루지 않는다.</summary>
    public int MaxObstacle { get; set; } = 24;
    /// <summary>장애물이 옆으로 이 칸 이상 이어져야 (강 · 늪) 병목이다 — 작은 장애물은 돌아간다.</summary>
    public int MinBarrier { get; set; } = 16;
    /// <summary>건널 곳 (다리 · 여울) 을 찾는 옆 범위 (칸, 직선 경로 기준 ±).</summary>
    public int SearchLateral { get; set; } = 48;
    /// <summary>건널 곳 폭이 이 칸 이하면 병목 (도하 계획). 넓으면 보통 공격.</summary>
    public int MaxLaneWidth { get; set; } = 10;
    /// <summary>첫 건널 곳에서 이 칸 안에 다른 건널 곳이 있으면 둘째 건널 곳 — twoLanes = false 면 그때는 도하 계획을 하지 않는다 (보통 공격).
    /// 측정 (RIV_br2): 다리 2개가 각각 방어 중대 바로 앞일 때 계획 도하 (두 곳 동시 포함) 0/6, 보통 공격 (둑에서 사격전 → 약해진 뒤 건넘) 4/8.</summary>
    public float SecondLaneRange { get; set; } = 60f;
    public bool TwoLanes { get; set; } = false;
    /// <summary>계획 시작: 적과 접촉 (소총 사거리 × 2) 또는 앞 중대 중심이 건널 곳 입구에서 소총 사거리 × 이 값 안.</summary>
    public float TriggerRange { get; set; } = 1.5f;
    /// <summary>판단 간격 (초) — 아직 계획이 없을 때 병목 찾기.</summary>
    public float CheckSec { get; set; } = 4f;
    /// <summary>화력 기반 중대 비율 (반올림, 중대가 2개 이상이면 최소 1 · 최대 n−1): 강둑 칸이 건널 곳에서 먼 중대 — 마지막에 건넌다.</summary>
    public float FireBaseFrac { get; set; } = 0.34f;
    /// <summary>강둑 사격 칸 = 입구에서 강둑 안쪽으로 이 칸 (옆으로는 건널 곳 밖) — 모든 중대가 차례를 기다리며 여기서 사격.</summary>
    public float FireBaseBack { get; set; } = 1f;
    /// <summary>강둑 사격 칸 폭 (칸) — 중대마다 (2열 이상 횡대).</summary>
    public float BankWidth { get; set; } = 22f;
    /// <summary>강둑 사격 칸의 옆 거리 한도 (칸) — 넘으면 bankRowGap 칸 뒤 줄에 다시 안쪽부터 (바깥 칸은 적이 사거리 밖이었다).</summary>
    public float BankMaxLateral { get; set; } = 42f;
    public float BankRowGap { get; set; } = 6f;
    /// <summary>계획 뒤 이 초에 강둑 칸을 한 번 다시 고른다 (계획 때는 적이 덜 보였다).</summary>
    public float ReslotSec { get; set; } = 6f;
    /// <summary>화력 기반 국면 최소 · 최대 (초): 화력 기반이 자리 잡고 minSec 이 지나면 (포병이 있으면 준비사격이 나간 뒤, 또는 prepWaitSec 더) 아래 진척 조건으로 도하 시작. maxSec 이면 무조건.</summary>
    public float FireMinSec { get; set; } = 15f;
    public float FireMaxSec { get; set; } = 300f;
    /// <summary>화력전 진척: 출구 둘레 보이는 적 수가 progressDrop 비율 넘게 줄면 진척. stallSec 동안 진척이 없거나 (도하 병력 ≥ 그 적 × launchOdds) 이면 도하 시작.
    /// 측정 (RIV_br1_r2_fs, 곡사포 · 기관총): 시간만으로 건너면 (45초) 손실 37~79%, 옛 동작 (안 건너고 포격) 은 470~520초 승 손실 10~25%.</summary>
    public float ProgressDrop { get; set; } = 0.05f;
    public float StallSec { get; set; } = 30f;
    public float LaunchOdds { get; set; } = 3f;
    /// <summary>도하 최소 병력비: 연대 도하 대상 소총 병력 ≥ 출구 둘레 보이는 적 × 이 값일 때만 건넌다 (아니면 강둑 화력전 — 포병 · 기관총이 적을 깎으면 그 뒤에).</summary>
    public float MinOdds { get; set; } = 2.5f;
    public float PrepWaitSec { get; set; } = 20f;
    /// <summary>다음 중대 출발: 앞 중대 병력의 launchFrac 이 입구를 지났거나 launchMaxSec 지남 (최소 launchMinSec 간격).
    /// 측정 (RIV_br1_r2 6판): 한 중대씩 (0.3 · 12초) = 0승, 거의 동시 (0 · 2초) = 3~5승 — 병목 앞 적 사격이 한 중대에 몰리면 그 중대가 녹는다.</summary>
    public float LaunchFrac { get; set; } = 0f;
    public float LaunchMinSec { get; set; } = 2f;
    public float LaunchMaxSec { get; set; } = 8f;
    /// <summary>출발한 중대는 이 초 동안 스스로 후퇴 (병력 · 제압 임계) 를 미룬다 — 병목 위 · 교두보에서 돌아서면 뒤 중대와 엉켜 둘 다 무너진다.</summary>
    public float CommitSec { get; set; } = 40f;
    /// <summary>교두보: 출구에서 이 칸 앞, 중대마다 폭 bridgeheadWidth 칸 (첫 중대는 건널 곳 정면, 다음은 출구에서 가장 가까운 적 쪽으로 차례로).</summary>
    public float BridgeheadDepth { get; set; } = 7f;
    public float BridgeheadWidth { get; set; } = 14f;
    /// <summary>교두보 성립: 도하 중대 병력의 이 비율이 출구 선을 넘음 (또는 마지막 출발 뒤 bridgeheadMaxSec).</summary>
    public float BridgeheadFrac { get; set; } = 0.6f;
    public float BridgeheadMaxSec { get; set; } = 70f;
    /// <summary>교두보 성립 뒤 이 초 동안 자리를 지키며 사격 (뒤따르는 화력 기반이 오는 동안), 그 뒤 보통 공격.</summary>
    public float BridgeheadHoldSec { get; set; } = 0f;
    /// <summary>건넌 중대의 사격 진지는 출구 선에서 이 칸 앞 이상 (다시 강 건너 진지로 가지 않게).</summary>
    public float FarSideMargin { get; set; } = 1f;
}

/// <summary>
/// 도하 · 병목 돌파 (사용자 결정 2026-10-19 — docs/BATTALION_CAMPAIGN_2026-10-19.md '도하 · 역습 (crossing)').
/// 교착 원인 (캠페인 2 RIV_br1): 공격 중대의 사격 진지 (TacMap, 적에서 사거리 × 0.72) 가 강의 이쪽 둑에 떨어져 모두 둑에서 사거리 끝 사격전만 하고,
/// 약진 · 돌격 조건 (우세 · 제압 · 돌격 거리) 은 강 건너 적에게 영영 성립하지 않는다 (아무도 다리로 가라는 명령을 받지 않음).
/// 연대 층 계획 (결정적, 수치 tactics.json "crossing", 상세 docs/ENGINE_NOTES.md '도하 · 병목 돌파'):
///  1 화력 기반: 앞 중대 → 적 직선이 두꺼운 못 걷는 띠 (강) 를 지나고 옆으로 좁은 건널 곳이 하나뿐이면 계획. 모든 소총 중대가 강둑 사격 칸 (반응 거리 안 적 맞은편)
///    에서 사격, 기관총 분견대는 입구 좌우, 지원 포병은 출구 둘레 적. 화력전 진척이 멈추면 (또는 적이 약해지면) 도하.
///  2 도하: 건널 곳에 가까운 칸 중대부터 거의 동시에 속보로 출구 너머 교두보 칸으로 (병목은 길찾기가 종대로 통과), 출발 중대는 잠시 후퇴 미룸.
///  3 교두보: 도하 병력 다수가 출구 선을 넘으면 성립 → 4 계속: 건넌 중대는 보통 공격 (사격 진지는 강 건너 쪽만),
///    화력 기반 중대도 차례로 건넌다. 연대 과제가 공격이 아니게 되면 (공격 한계 · 후퇴) 계획을 지운다.
/// 상태: TacRegiment.Cross* · TacGroup.CrossStage (스냅샷 — ObjCodec).
/// </summary>
public sealed partial class TacticalAi
{
    static CrossingAiDef Cross(World w) => w.Defs.Tactics.Crossing;

    // 중대 도하 단계 (TacGroup.CrossStage)
    internal const int CsNone = 0, CsAssemble = 1, CsTransit = 2, CsBridgehead = 3, CsFireBase = 4, CsReleased = 5;
    // 연대 도하 국면 (TacRegiment.CrossPhase)
    internal const int CpNone = 0, CpFireBase = 1, CpCrossing = 2, CpBridgehead = 3, CpExploit = 4, CpDone = 5;

    /// <summary>
    /// 병목 찾기 (지형의 순수 함수): from → to 직선이 처음 만나는 못 걷는 띠의 두께 (기본 방향 = 직선에 가까운 축) 와
    /// 그 띠를 축 방향으로 끝까지 걸어서 건널 수 있는 옆 위치 (건널 곳) 중 직선에 가장 가까운 것. 병목이 아니면 false.
    /// </summary>
    internal static bool FindCrossing(GameMap m, Vector2 from, Vector2 to, CrossingAiDef C, out Vector2 entry, out Vector2 exit, out Vector2 n, out float width) =>
        FindCrossing(m, from, to, C, out entry, out exit, out n, out width, out _, out _, out _);

    /// <summary>같은 띠의 반대쪽 끝에도 좁은 건널 곳이 있으면 둘째 건널 곳 (width2 &gt; 0) — 도하를 두 곳으로 나눠 동시에 (한 곳에 몰면 적 사격이 한 병목에 모인다).</summary>
    internal static bool FindCrossing(GameMap m, Vector2 from, Vector2 to, CrossingAiDef C, out Vector2 entry, out Vector2 exit, out Vector2 n, out float width,
                                      out Vector2 entry2, out Vector2 exit2, out float width2)
    {
        entry = exit = n = entry2 = exit2 = default; width = width2 = 0;
        Vector2 d = to - from;
        float L = d.Length();
        if (L < 2f) return false;
        Vector2 u = d / L;
        var (fx, fy) = GameMap.TileOf(from);
        if (!m.IsWalkable(fx, fy)) return false;
        int bx = -1, by = -1;
        for (float t = 0.5f; t <= L; t += 0.5f)
        {
            var (x, y) = GameMap.TileOf(from + u * t);
            if (!m.InBounds(x, y)) break;
            if (!m.IsWalkable(x, y)) { bx = x; by = y; break; }
        }
        if (bx < 0) return false;
        // 축: 직선 방향의 두 기본 축 (x 부호 · y 부호) 중 막힌 칸에서 띠를 더 짧게 건너는 쪽 (강을 비스듬히 봐도 강 폭 방향)
        int nx = 0, ny = 0, ax = -1, ay = -1, len = int.MaxValue;
        foreach (var (cx, cy) in new[] { (u.X >= 0 ? 1 : -1, 0), (0, u.Y >= 0 ? 1 : -1) })
        {
            if ((cx != 0 && MathF.Abs(u.X) < 0.2f) || (cy != 0 && MathF.Abs(u.Y) < 0.2f)) continue;   // 거의 수직인 축은 건너지 않는다
            int qx = -1, qy = -1;
            for (int j = 1; j <= 3; j++) if (m.IsWalkable(bx - cx * j, by - cy * j)) { qx = bx - cx * j; qy = by - cy * j; break; }   // 띠 앞 (걸을 수 있는 마지막 칸)
            if (qx < 0) continue;
            int l = 0;
            for (int k = 1; k <= C.MaxObstacle + 1; k++) { if (m.IsWalkable(qx + cx * k, qy + cy * k)) break; l = k; }
            if (l < C.MinObstacle || l > C.MaxObstacle || !m.IsWalkable(qx + cx * (l + 1), qy + cy * (l + 1))) continue;
            if (l < len) { len = l; nx = cx; ny = cy; ax = qx; ay = qy; }
        }
        if (ax < 0) return false;
        int sx = -ny, sy = nx;
        int S = Math.Max(4, C.SearchLateral);
        bool Open(int o)
        {
            for (int k = 0; k <= len + 1; k++) if (!m.IsWalkable(ax + sx * o + nx * k, ay + sy * o + ny * k)) return false;
            return true;
        }
        // 띠의 옆 길이 (o = 0 을 품은 막힌 구간) — 작은 장애물이면 병목 아님
        int c1 = 0, c2 = 0;
        while (c1 > -S && !Open(c1 - 1)) c1--;
        while (c2 < S && !Open(c2 + 1)) c2++;
        if (c2 - c1 + 1 < C.MinBarrier) return false;
        // 건널 곳: 막힌 구간 양 끝 밖으로 이어지는 열린 구간 (가까운 쪽 우선, 같으면 음의 쪽)
        int bestLo = 0, bestHi = -1; float bestD = float.MaxValue;
        foreach (int sgn in new[] { -1, 1 })
        {
            int start = sgn < 0 ? c1 - 1 : c2 + 1;
            if (Math.Abs(start) > S || !Open(start)) continue;
            int end = start;
            while (Math.Abs(end + sgn) <= S + C.MaxLaneWidth + 1 && Open(end + sgn)) end += sgn;
            int lo = Math.Min(start, end), hi = Math.Max(start, end);
            float dc = MathF.Abs((lo + hi) * 0.5f);
            if (dc < bestD) { bestD = dc; bestLo = lo; bestHi = hi; }
        }
        if (bestHi < bestLo) return false;
        width = bestHi - bestLo + 1;
        if (width > C.MaxLaneWidth) return false;
        n = new Vector2(nx, ny);
        Vector2 side = new(sx, sy);
        Vector2 a0 = new(ax + 0.5f, ay + 0.5f);
        entry = a0 + side * ((bestLo + bestHi) * 0.5f);
        exit = entry + n * (len + 1);
        // 둘째 건널 곳: 첫 건널 곳 양옆 secondLaneRange 칸 안의 다른 열린 구간 (가까운 것, 폭 무관 — 넓으면 width2 > maxLaneWidth)
        float cc = (bestLo + bestHi) * 0.5f, best2 = float.MaxValue;
        foreach (int sgn in new[] { -1, 1 })
        {
            int o = sgn < 0 ? bestLo - 1 : bestHi + 1;
            while (MathF.Abs(o - cc) <= C.SecondLaneRange && !Open(o)) o += sgn;
            if (MathF.Abs(o - cc) > C.SecondLaneRange) continue;
            int end = o;
            while (MathF.Abs(end + sgn - cc) <= C.SecondLaneRange + C.MaxLaneWidth * 3 && Open(end + sgn)) end += sgn;
            int lo = Math.Min(o, end), hi = Math.Max(o, end);
            float dc = MathF.Abs((lo + hi) * 0.5f - cc);
            if (dc < best2) { best2 = dc; width2 = hi - lo + 1; entry2 = a0 + side * ((lo + hi) * 0.5f); exit2 = entry2 + n * (len + 1); }
        }
        return true;
    }

    /// <summary>p 에서 가장 가까운 걸을 수 있는 칸 중심 (반경 r 칸 고리 탐색, 없으면 p).</summary>
    static Vector2 WalkableNear(GameMap m, Vector2 p, int r = 6)
    {
        var (px, py) = GameMap.TileOf(p);
        if (m.IsWalkable(px, py)) return p;
        for (int k = 1; k <= r; k++)
        {
            float bd = float.MaxValue; Vector2 best = default; bool any = false;
            for (int dy = -k; dy <= k; dy++)
            for (int dx = -k; dx <= k; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != k || !m.IsWalkable(px + dx, py + dy)) continue;
                var q = new Vector2(px + dx + 0.5f, py + dy + 0.5f);
                float dd = Vector2.DistanceSquared(q, p);
                if (dd < bd) { bd = dd; best = q; any = true; }
            }
            if (any) return best;
        }
        return p;
    }

    [NonSerialized] readonly List<TacGroup> crossBuf = new();

    /// <summary>연대의 도하 대상 소총 중대 (후퇴하지 않은 대대의 편성된 소총 중대).</summary>
    void CrossCandidates(TacFaction s, TacRegiment r, List<TacGroup> into)
    {
        into.Clear();
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Line || g.Pending || g.Battalion == 0) continue;
            var b = s.Battalions.Find(x => x.Id == g.Battalion);
            if (b == null || b.Regiment != r.Id || b.Spent || g.Alive < 10) continue;   // 몇 명짜리 묶음 (분견대 · 장교 하나) 은 계획에서 뺀다
            into.Add(g);
        }
    }

    void ResetCrossing(TacFaction s, TacRegiment r)
    {
        if (r.CrossPhase == CpNone) return;
        r.CrossPhase = CpNone; r.CrossQueue.Clear(); r.CrossNext = 0;
        foreach (var g in s.Groups) if (g.CrossRegiment == r.Id) { g.CrossStage = CsNone; g.CrossRegiment = 0; g.OrderTick = -100000; }
    }

    /// <summary>
    /// 연대 도하 판단 (RegimentThink 공격 과제에서, LayOut 뒤): 계획 찾기 · 국면 진행 · 화력 표적. fc = 앞 대대 무게중심, e = 가장 가까운 보이는 적.
    /// </summary>
    void CrossingThink(World w, TacFaction s, TacRegiment r, List<TacBattalion> fronts, Vector2 fc, Unit? e, float R, bool arty)
    {
        var C = Cross(w);
        if (!C.Enabled) return;
        var cands = crossBuf;
        if (r.CrossPhase == CpNone)
        {
            if (w.Tick < r.CrossCheckTick) return;
            r.CrossCheckTick = w.Tick + Sec(C.CheckSec);
            CrossCandidates(s, r, cands);
            if (cands.Count == 0) return;
            // 직선 몇 개 (앞 중심 · 좌우 끝 중대 → 가장 가까운 적 · 연대 과제 지점) 중 처음 병목이 나오는 것
            var froms = new List<Vector2> { fc };
            Vector2 side = Perp(SafeNorm(r.Dir, s.Dir));
            TacGroup lo = cands[0], hi = cands[0];
            foreach (var g in cands) { if (Vector2.Dot(g.Center, side) < Vector2.Dot(lo.Center, side)) lo = g; if (Vector2.Dot(g.Center, side) > Vector2.Dot(hi.Center, side)) hi = g; }
            froms.Add(lo.Center); froms.Add(hi.Center);
            var tos = new List<Vector2>();
            if (e != null) tos.Add(e.Pos);
            tos.Add(r.Point);
            bool found = false; Vector2 en = default, ex = default, nn = default, en2 = default, ex2 = default; float wd = 0, wd2 = 0;
            foreach (var to in tos)
            {
                foreach (var fr in froms)
                    if (FindCrossing(w.Map, fr, to, C, out en, out ex, out nn, out wd, out en2, out ex2, out wd2)) { found = true; break; }
                if (found) break;
            }
            if (!found) return;
            if (wd2 > 0 && !C.TwoLanes) { r.CrossCheckTick = w.Tick + Sec(C.CheckSec * 4); return; }   // 건널 곳이 여럿 = 병목 아님 (보통 공격)
            if (wd2 > C.MaxLaneWidth) wd2 = 0;   // 넓은 둘째 건널 곳은 쓰지 않는다
            bool contact = e != null && Vector2.Distance(e.Pos, fc) < R * 2f;
            if (!contact && Vector2.Distance(fc, en) > R * C.TriggerRange) return;
            StartCrossing(w, s, r, cands, en, ex, nn, wd, en2, ex2, wd2);
            return;
        }
        if (r.CrossPhase == CpDone) return;
        // 국면 진행
        CrossCandidates(s, r, cands);
        long el = w.Tick - r.CrossTick;
        switch (r.CrossPhase)
        {
            case CpFireBase:
            {
                if (!r.CrossReslotted && el >= Sec(C.ReslotSec)) { r.CrossReslotted = true; AssignBankSlots(w, s, r); }   // 적이 더 보인 뒤 강둑 칸 다시 고르기 (한 번)
                bool settled = true;
                foreach (var g in s.Groups) if (g.CrossRegiment == r.Id && g.CrossStage is CsFireBase or CsAssemble && g.Kind == TacKind.Line && g.State != TacState.Engage) settled = false;
                if (settled && r.CrossFireTick == 0) r.CrossFireTick = w.Tick;
                bool prepOk = !arty || ArtilleryReady(w, s, r) || el >= Sec(C.FireMinSec + C.PrepWaitSec);
                // 화력전 진척: 출구 둘레 보이는 적이 progressDrop 넘게 줄면 진척 — 이기고 있으면 계속 쏘고 (포병 · 기관총이 건너편을 깎는 동안 건너지 않는다),
                // 적이 도하 병력 대비 약해졌거나 (launchOdds) stallSec 동안 진척이 없으면 건넌다
                int near = Grid.Stats(r.CrossExit + r.CrossDir * (R * 0.4f), R * 1.2f).Count;
                if (near > r.CrossEnemyBest) r.CrossEnemyBest = near;   // 더 보이면 기준만 올림 (진척 아님)
                else if (near < r.CrossEnemyBest * (1f - C.ProgressDrop)) { r.CrossEnemyBest = near; r.CrossProgressTick = w.Tick; }
                int crossing = 0;
                for (int qi = 0; qi < r.CrossQueue.Count - r.CrossFireBase; qi++) if (s.Groups.Find(x => x.Id == r.CrossQueue[qi]) is { } qg) crossing += qg.Alive;
                bool weak = near * C.LaunchOdds <= crossing;
                bool stalled = w.Tick - r.CrossProgressTick >= Sec(C.StallSec);
                int total = 0;
                foreach (var qg in cands) total += qg.Alive;
                bool odds = total >= near * C.MinOdds;   // 병력비가 모자라면 건너지 않고 강둑에서 계속 쏜다 (방어가 다리를 지킨다 — 소총만 2:1 은 도하 시 손실 75%로 실패했다)
                if (odds && ((settled && el >= Sec(C.FireMinSec) && prepOk && (weak || stalled)) || el >= Sec(C.FireMaxSec)))
                {
                    if (r.CrossFireTick == 0) r.CrossFireTick = w.Tick;
                    r.CrossPhase = CpCrossing; r.CrossTick = w.Tick; r.CrossStartTick = w.Tick; s.Crossings++;
                    LaunchNext(w, s, r);
                }
                break;
            }
            case CpCrossing:
            case CpExploit:
            {
                // 앞 중대가 입구를 반쯤 지나면 다음 중대
                bool more = r.CrossNext < r.CrossQueue.Count;
                if (r.CrossPhase == CpCrossing)
                {
                    // 도하 국면: 화력 기반 (대기열 끝 r.CrossFireBase 개) 전까지만
                    more = r.CrossNext < r.CrossQueue.Count - r.CrossFireBase;
                    if (!more) { r.CrossPhase = CpBridgehead; r.CrossTick = w.Tick; break; }
                }
                if (!more)
                {
                    bool left = false;
                    foreach (var g in s.Groups) if (g.CrossRegiment == r.Id && g.CrossStage is CsAssemble or CsTransit or CsBridgehead or CsFireBase) left = true;
                    if (!left) { r.CrossPhase = CpDone; r.CrossTick = w.Tick; }
                    break;
                }
                long since = w.Tick - r.CrossLaunchTick;
                if (since < Sec(C.LaunchMinSec)) break;
                var prev = s.Groups.Find(g => g.Id == r.CrossQueue[r.CrossNext - 1]);
                bool passed = prev == null || prev.CrossStage != CsTransit || PastFrac(w, s, prev, LaneEntry(r, prev.CrossLane), r.CrossDir) >= C.LaunchFrac;
                if (passed || since >= Sec(C.LaunchMaxSec)) LaunchNext(w, s, r);
                break;
            }
            case CpBridgehead:
            {
                int across = 0, all = 0;
                foreach (var g in s.Groups)
                {
                    if (g.CrossRegiment != r.Id || g.CrossStage is not (CsTransit or CsBridgehead or CsReleased)) continue;   // pronemove: 교두보에서 착검 역습으로 풀린 (Released) 중대도 건넌 병력으로 센다 (예전엔 빠져서 남은 중대 비율만 봄 — 교두보 0초)
                    Members(w, s, g, buf);
                    Vector2 lx = LaneExit(r, g.CrossLane);
                    foreach (var u in buf) { all++; if (Vector2.Dot(u.Pos - lx, r.CrossDir) >= 0) across++; }
                }
                if ((all > 0 && across >= all * C.BridgeheadFrac) || all == 0 || el >= Sec(C.BridgeheadMaxSec))
                {
                    if (r.CrossBridgeTick == 0 && all > 0 && across >= all * C.BridgeheadFrac) { r.CrossBridgeTick = w.Tick; s.Bridgeheads++; }
                    r.CrossPhase = CpExploit; r.CrossTick = w.Tick; r.CrossHoldUntil = w.Tick + Sec(C.BridgeheadHoldSec);
                }
                break;
            }
        }
        // 교두보 지키기가 끝나면 건넌 중대를 보통 공격으로
        if (r.CrossPhase >= CpExploit && w.Tick >= r.CrossHoldUntil)
            foreach (var g in s.Groups)
                if (g.CrossRegiment == r.Id && g.CrossStage == CsBridgehead && g.State == TacState.Engage) g.CrossStage = CsReleased;
        // 지원 포병: 화력 기반 · 도하 · 교두보 동안 출구 둘레 적 (없으면 출구 앞)
        if (arty && r.CrossPhase is CpFireBase or CpCrossing or CpBridgehead)
        {
            Vector2 far = r.CrossExit + r.CrossDir * (R * 0.4f);
            var st = Grid.Stats(far, R * 0.7f);
            if (st.Count > 0) { r.FireTarget = st.Sum / st.Count; r.HasFireTarget = true; r.Lifted = false; }
        }
    }

    void StartCrossing(World w, TacFaction s, TacRegiment r, List<TacGroup> cands, Vector2 entry, Vector2 exit, Vector2 n, float width, Vector2 entry2, Vector2 exit2, float width2)
    {
        r.CrossPhase = CpFireBase; r.CrossTick = w.Tick; r.CrossFireTick = 0; r.CrossStartTick = 0; r.CrossBridgeTick = 0; r.CrossHoldUntil = 0; r.CrossReslotted = false; r.CrossEnemyBest = 0; r.CrossProgressTick = w.Tick;
        r.CrossEntry = entry; r.CrossExit = exit; r.CrossDir = n; r.CrossWidth = width;
        r.CrossEntry2 = entry2; r.CrossExit2 = exit2; r.CrossWidth2 = width2;
        r.CrossQueue.Clear(); r.CrossNext = 0; r.CrossBridgeSlots = 0; r.CrossBridgeSlots2 = 0;
        s.CrossPlans++;
        // 예비 (대대 예비 중대 · 연대 예비 대대) 도 모두 도하 대열에 — 강 이쪽에 남은 예비는 나중에 다시 둑에 막힌다
        r.ReserveCommitted = true; r.ReserveBattalion = 0;
        foreach (var b in s.Battalions) if (b.Regiment == r.Id) { b.ReserveCommitted = true; b.ReserveGroup = 0; b.FlankGroup = 0; }
        Vector2 side = Perp(n);
        var lat = cands.OrderBy(g => Vector2.Dot(g.Center, side)).ThenBy(g => g.Id).ToList();
        // 건널 곳이 둘이면 (같은 띠의 양 끝) 옆 순서 앞 절반 = 옆 좌표가 작은 건널 곳, 나머지 = 큰 쪽 — 두 곳에서 동시에
        int lanes = width2 > 0 && lat.Count >= 2 ? 2 : 1;
        if (lanes == 1) r.CrossWidth2 = 0;
        int lowLane = lanes == 2 && Vector2.Dot(entry2, side) < Vector2.Dot(entry, side) ? 1 : 0;
        for (int j = 0; j < lat.Count; j++)
        {
            var g = lat[j];
            r.CrossQueue.Add(g.Id);
            g.CrossRegiment = r.Id; g.CrossLane = lanes == 1 ? 0 : (j < (lat.Count + 1) / 2 ? lowLane : 1 - lowLane);
            g.OrderTick = -100000; g.SquadBound = 0; g.State = TacState.Idle; g.CrossStage = CsAssemble;
        }
        AssignBankSlots(w, s, r);
        // 기관총 · 화력 분견대: 첫 건널 곳 입구 좌우 (근접 지원)
        var C = Cross(w);
        int mi = 0;
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Fire || g.Battalion == 0) continue;
            var b = s.Battalions.Find(x => x.Id == g.Battalion);
            if (b == null || b.Regiment != r.Id || IsGunGroupIds(w, s, g)) continue;
            int sgn = mi % 2 == 0 ? -1 : 1;
            g.CrossRegiment = r.Id; g.CrossLane = 0; g.CrossStage = CsFireBase; g.OrderTick = -100000; g.State = TacState.Idle;
            g.CrossPoint = WalkableNear(w.Map, ClampMap(w.Map, entry - n * (C.FireBaseBack + 2f) + side * (sgn * (width * 0.5f + 2f + 3f * (mi / 2)))));
            mi++;
        }
    }

    /// <summary>
    /// 강둑 사격 칸 배정 (도하 대기열의 아직 출발하지 않은 소총 중대): 건널 곳마다 후보 칸 (양옆 bankWidth + 3 간격, 옆 한도 bankMaxLateral, 줄 0 · 1) 중
    /// 반응 거리 (소총 사거리 × combat.engageRangeFrac) 안에 보이는 적이 많은 칸, 같으면 가까운 적이 가까운 칸부터 — 그 밖의 적은 쏘지 않으므로 적 무리 맞은편 강둑이어야 화력 기반이 된다.
    /// 칸은 중대의 옆 순서대로 (서로 길을 가로지르지 않게). 그 뒤 대기열 = 건널 곳에 가까운 칸부터, 가장 바깥 k 개 = 화력 기반 (마지막에 건넘).
    /// </summary>
    void AssignBankSlots(World w, TacFaction s, TacRegiment r)
    {
        var C = Cross(w);
        Vector2 n = r.CrossDir, side = Perp(n);
        float react = (s.RifleRange > 0 ? s.RifleRange : 16f) * w.Defs.CombatRules.EngageRangeFrac;
        var waiting = new List<TacGroup>();
        for (int i = r.CrossNext; i < r.CrossQueue.Count; i++)
            if (s.Groups.Find(x => x.Id == r.CrossQueue[i]) is { } g && g.CrossRegiment == r.Id && g.CrossStage is CsAssemble or CsFireBase) waiting.Add(g);
        if (waiting.Count == 0) return;
        for (int lane = 0; lane < 2; lane++)
        {
            var mine = waiting.Where(g => g.CrossLane == lane).OrderBy(g => Vector2.Dot(g.Center, side)).ThenBy(g => g.Id).ToList();
            if (mine.Count == 0) continue;
            float wd = lane == 1 ? r.CrossWidth2 : r.CrossWidth;
            Vector2 le = LaneEntry(r, lane);
            var cand = new List<(float Off, int Row, float Score)>();
            for (int row = 0; row < 2; row++)
                for (int jj = 0; ; jj++)
                {
                    float o = wd * 0.5f + 2f + C.BankWidth * 0.5f + jj * (C.BankWidth + 3f);
                    if (o > C.BankMaxLateral && jj > 0) break;
                    foreach (int sg in new[] { 1, -1 })
                    {
                        Vector2 pt = le - n * (C.FireBaseBack + row * C.BankRowGap) + side * (sg * o);
                        int inRange = Grid.Stats(pt, react).Count;
                        var ne = Grid.Nearest(pt, TacGrid.Infantry);
                        float sc = -inRange * 4f + (ne != null ? Vector2.Distance(ne.Pos, pt) : 0) + row * C.BankRowGap + o * 0.01f;
                        cand.Add((sg * o, row, sc));
                    }
                }
            var pick = cand.OrderBy(c => c.Score).ThenBy(c => c.Off).Take(mine.Count).OrderBy(c => c.Off).ThenBy(c => c.Row).ToList();
            for (int j = 0; j < mine.Count; j++)
            {
                var (off, row, _) = pick[Math.Min(j, pick.Count - 1)];
                var g = mine[j];
                var p = WalkableNear(w.Map, ClampMap(w.Map, le - n * (C.FireBaseBack + row * C.BankRowGap) + side * MathF.Round(off)));
                if (Vector2.Distance(p, g.CrossPoint) > 4f) { g.CrossPoint = p; g.OrderTick = -100000; if (g.State == TacState.Engage) g.State = TacState.Idle; }
                g.CrossSlot = MathF.Round(off) + row * 0.01f;
            }
        }
        // 대기열 (출발 전 부분) 다시 정렬: 건널 곳에 가까운 칸부터, 바깥 k 개 = 화력 기반
        var order = waiting.OrderBy(g => MathF.Abs(g.CrossSlot)).ThenBy(g => g.CrossSlot).ThenBy(g => g.Id).ToList();
        int all = r.CrossQueue.Count;
        int k = all >= 2 ? Math.Clamp((int)MathF.Round(all * C.FireBaseFrac), 1, all - 1) : 0;
        k = Math.Min(k, order.Count);
        r.CrossFireBase = k;
        r.CrossQueue.RemoveRange(r.CrossNext, all - r.CrossNext);
        for (int i = 0; i < order.Count; i++)
        {
            r.CrossQueue.Add(order[i].Id);
            order[i].CrossStage = i >= order.Count - k ? CsFireBase : CsAssemble;
        }
    }

    static Vector2 LaneEntry(TacRegiment r, int lane) => lane == 1 && r.CrossWidth2 > 0 ? r.CrossEntry2 : r.CrossEntry;
    static Vector2 LaneExit(TacRegiment r, int lane) => lane == 1 && r.CrossWidth2 > 0 ? r.CrossExit2 : r.CrossExit;

    bool IsGunGroupIds(World w, TacFaction s, TacGroup g)
    {
        Members(w, s, g, sub);
        return IsGunGroup(sub);
    }

    void LaunchNext(World w, TacFaction s, TacRegiment r)
    {
        var C = Cross(w);
        while (r.CrossNext < r.CrossQueue.Count)
        {
            int id = r.CrossQueue[r.CrossNext];
            r.CrossNext++;
            var g = s.Groups.Find(x => x.Id == id);
            if (g == null || g.CrossRegiment != r.Id || g.CrossStage is not (CsAssemble or CsFireBase)) continue;
            Vector2 side = Perp(r.CrossDir);
            // 교두보 칸: 첫 중대는 건널 곳 정면, 다음은 한쪽 (출구에서 가장 가까운 적 무리 쪽) 으로 차례로 (중대 폭 + 2) —
            // 건넌 병력을 적 한 무리에 모은다 (좌우로 나누면 양쪽 적 사격을 다 받고 각개 격파된다).
            Vector2 lx = LaneExit(r, g.CrossLane);
            int bh = g.CrossLane == 1 ? r.CrossBridgeSlots2++ : r.CrossBridgeSlots++;
            if (bh == 0)
            {
                var ne = Grid.Nearest(lx + r.CrossDir * C.BridgeheadDepth, TacGrid.Infantry);
                int sd = ne != null && Vector2.Dot(ne.Pos - lx, side) < 0 ? -1 : 1;
                if (g.CrossLane == 1) r.CrossSide2 = sd; else r.CrossSide = sd;
            }
            float off = (g.CrossLane == 1 ? r.CrossSide2 : r.CrossSide) * bh * (C.BridgeheadWidth + 2f);
            g.CrossPoint = WalkableNear(w.Map, ClampMap(w.Map, lx + r.CrossDir * C.BridgeheadDepth + side * off));
            g.CrossStage = CsTransit; g.OrderTick = -100000; g.State = TacState.Idle;
            g.NoRetreatUntil = Math.Max(g.NoRetreatUntil, w.Tick + Sec(C.CommitSec));
            r.CrossLaunchTick = w.Tick;
            s.CrossLaunches++;
            return;
        }
    }

    /// <summary>중대 병력 중 입구 선 (entry, 축 n) 을 넘은 비율.</summary>
    float PastFrac(World w, TacFaction s, TacGroup g, Vector2 entry, Vector2 n)
    {
        Members(w, s, g, buf);
        if (buf.Count == 0) return 1f;
        int k = 0;
        foreach (var u in buf) if (Vector2.Dot(u.Pos - entry, n) > 0.5f) k++;
        return k / (float)buf.Count;
    }

    /// <summary>도하에 몸을 실은 동안 (도하 · 교두보 국면, 계속 국면은 교두보 지키기 + commitSec 까지): 연대 · 대대 붕괴 후퇴를 미룬다 — 병목 위에서 돌아서면 전멸한다.</summary>
    static bool CrossCommitted(World w, TacRegiment r) =>
        r.CrossPhase is CpCrossing or CpBridgehead || (r.CrossPhase == CpExploit && w.Tick < r.CrossHoldUntil + Sec(Cross(w).CommitSec));

    TacRegiment? CrossRegimentOf(TacFaction s, TacGroup g)
    {
        if (g.CrossRegiment == 0) return null;
        foreach (var r in s.Regiments) if (r.Id == g.CrossRegiment) return r.CrossPhase != CpNone ? r : null;
        return null;
    }

    /// <summary>
    /// 소총 중대 도하 단계 행동 (LineThink 앞). 처리했으면 true. 건넌 뒤 (Released) 는 false — 보통 공격 (사격 진지만 강 건너 쪽으로 자름).
    /// </summary>
    bool LineCrossing(World w, TacFaction s, TacGroup g, float range, float supp)
    {
        if (g.CrossStage is CsNone or CsReleased || g.Task != TacTask.Attack) return false;
        var r = CrossRegimentOf(s, g);
        if (r == null) { g.CrossStage = CsNone; g.CrossRegiment = 0; return false; }
        Vector2 n = r.CrossDir, side = Perp(n);
        g.TaskDir = n;
        g.SquadBound = 0;
        var C = Cross(w);
        switch (g.CrossStage)
        {
            case CsAssemble:
            case CsFireBase:
            {
                // 강둑 사격 칸에서 서서 사격 (차례가 오면 LaunchNext 가 도하로 바꾼다)
                float wdt = MathF.Min(Frontage(g), C.BankWidth);
                if (g.State != TacState.Engage)
                {
                    if (Vector2.Distance(g.Center, g.CrossPoint) > 4f)
                    {
                        if (g.State != TacState.Advance || Due(w, g, 25f))
                        { Order(w, s, g, mem, Ord.Move, g.CrossPoint, n, wdt); g.State = TacState.Advance; g.StateTick = w.Tick; }
                        return true;
                    }
                    HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; if (g.CrossStage == CsFireBase) s.CrossFireBases++;
                }
                return true;
            }
            case CsTransit:
            {
                // 속보로 출구 너머 교두보 칸까지 (멈춰 쏘지 않는다 — 병목에서 서면 뒤가 막힘). 길찾기가 병목을 종대로 지난다.
                float wdt = MathF.Min(Frontage(g), C.BridgeheadWidth);
                if (g.State != TacState.Advance)
                {
                    Order(w, s, g, mem, Ord.Cross, g.CrossPoint, n, wdt);
                    g.State = TacState.Advance; g.StateTick = w.Tick;
                    return true;
                }
                // 교두보 도착: 거의 모두 출구 선을 넘었을 때만 (사수 명령은 내지 않는다 — 다리 위 꼬리가 그 자리에 서면 뒤 중대가 막힌다 (측정: 종대가 다리 위에서 굳어 3 중대가 엉킴).
                // 이동 명령이 끝난 병사는 교두보 칸에서 서서 쏜다)
                int across = 0;
                Vector2 lx = LaneExit(r, g.CrossLane);
                foreach (var u in mem) if (Vector2.Dot(u.Pos - lx, n) >= 1f) across++;
                if (across >= mem.Count * 0.9f && (Vector2.Distance(g.Center, g.CrossPoint) <= 6f || Due(w, g, 20f)))
                {
                    g.CrossStage = CsBridgehead; g.State = TacState.Engage; g.StateTick = w.Tick;
                    return true;
                }
                if (Due(w, g, 30f)) { Order(w, s, g, mem, Ord.Cross, g.CrossPoint, n, wdt); g.StateTick = w.Tick; }
                return true;
            }
            case CsBridgehead:
            {
                // 교두보: 자리에서 사격, 가까이 온 약한 적은 착검 역습
                var e = Grid.Nearest(g.Center, TacGrid.Infantry);
                float d = e != null ? Vector2.Distance(e.Pos, g.Center) : float.MaxValue;
                if (e != null && d <= s.Profile.Tactics.ChargeRange * K && TryAssault(w, s, g, e, d, supp, attack: true)) { g.CrossStage = CsReleased; return true; }
                if (g.State != TacState.Engage) { g.State = TacState.Engage; g.StateTick = w.Tick; }
                return true;
            }
        }
        return false;
    }

    /// <summary>건넌 중대 (Released) 의 이동 · 사격 진지를 강 건너 쪽 (출구 선 앞) 으로 자른다.</summary>
    Vector2 FarSide(TacFaction s, TacGroup g, Vector2 p, World w)
    {
        if (g.CrossStage != CsReleased) return p;
        var r = CrossRegimentOf(s, g);
        if (r == null) return p;
        float m = Cross(w).FarSideMargin;
        float dd = Vector2.Dot(p - LaneExit(r, g.CrossLane), r.CrossDir);
        return dd >= m ? p : WalkableNear(w.Map, ClampMap(w.Map, p + r.CrossDir * (m - dd)));
    }

    /// <summary>기관총 분견대 (화력 기반) 도하 단계: 입구 좌우 진지에서 사수 (자동 거치). 처리했으면 true.</summary>
    bool FireCrossing(World w, TacFaction s, TacGroup g)
    {
        if (g.CrossStage != CsFireBase) return false;
        var r = CrossRegimentOf(s, g);
        if (r == null || r.CrossPhase >= CpExploit) { g.CrossStage = CsNone; g.CrossRegiment = 0; g.OrderTick = -100000; return false; }
        g.TaskDir = r.CrossDir;
        if (Vector2.Distance(g.Center, g.CrossPoint) > 3f && g.State != TacState.Engage)
        {
            if (g.State != TacState.Advance || Due(w, g, 25f)) { Order(w, s, g, mem, Ord.Move, g.CrossPoint, r.CrossDir, MathF.Min(10f, g.Alive * 1.2f)); g.State = TacState.Advance; g.StateTick = w.Tick; }
            return true;
        }
        if (g.State != TacState.Engage) { HoldAll(w, s, g, mem); g.State = TacState.Engage; g.StateTick = w.Tick; s.FireHolds++; }
        return true;
    }
}
