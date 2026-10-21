namespace KoreanDivision.Core;

/// <summary>점령 · 승패 (방향 변경 §0-1: 경제 · 건설 · 생산 없음).</summary>
public sealed partial class World
{
    public int Winner { get; private set; }          // 0 = 진행 중, -1 = 무승부, 그 외 = 승리 세력 (공동 승리면 그중 첫 번째)
    /// <summary>승리한 세력 전부 (공동 승리 포함).</summary>
    public readonly List<int> Winners = new();

    /// <summary>세력별 알림 (UI 가 소비 후 비움).</summary>
    public readonly List<(int Faction, CommandResult Result)> Notices = new();

    // ---------------------------------------------------------------- 점령

    readonly Dictionary<int, int> captureCount = new();

    void UpdateCapture(float dt)
    {
        var cap = Defs.Capture;
        foreach (var b in Buildings)
        {
            if (!b.Def.Capturable) continue;
            captureCount.Clear();
            foreach (var u in UnitsInRadius(b.Pos, cap.Radius + b.Size / 2f, queryBuffer))
                if (u.Def.CanCapture && !u.Surrendered && u.Owner != 0)
                    captureCount[u.Owner] = captureCount.GetValueOrDefault(u.Owner) + 1;

            // 경합 = 서로 적대(다른 팀)인 세력이 함께 있음. 같은 팀끼리는 경합이 아니다.
            bool contested = false;
            foreach (var x in captureCount.Keys)
            foreach (var y in captureCount.Keys)
                if (x < y && IsHostile(x, y)) contested = true;
            b.Contested = contested;
            if (contested || captureCount.Count == 0) continue;   // 아무도 없거나 경합 → 게이지 정지

            // 점령을 진행할 수 있는 세력: 소유자 자신, 또는 중립 시설이거나 소유자와 적대인 세력 (같은 팀의 시설은 못 뺏는다)
            int who = 0, n = 0;
            foreach (var (f, count) in captureCount)
            {
                if (!(f == b.Owner || b.Owner == 0 || IsHostile(f, b.Owner))) continue;
                if (count > n || (count == n && f < who)) (who, n) = (f, count);
            }
            if (who == 0) continue;
            float rate = MathF.Min(n, cap.MaxMultiplier) / MathF.Max(b.Def.CaptureTime > 0 ? b.Def.CaptureTime : cap.Time, 0.1f) * dt;
            if (b.Owner == who)
            {
                b.CaptureFaction = who;
                b.CaptureProgress = MathF.Min(1, b.CaptureProgress + rate);
            }
            else if (b.Owner != 0 || (b.CaptureFaction != who && b.CaptureProgress > 0))
            {
                // 남의 것(또는 남이 채우던 게이지)을 먼저 깎는다
                b.CaptureProgress -= rate;
                if (b.CaptureProgress <= 0)
                {
                    b.CaptureProgress = 0;
                    b.Owner = 0;
                    b.CaptureFaction = who;
                }
            }
            else
            {
                b.CaptureFaction = who;
                b.CaptureProgress += rate;
                if (b.CaptureProgress >= 1)
                {
                    b.CaptureProgress = 1;
                    b.Owner = who;
                }
            }
        }
    }

    // ---------------------------------------------------------------- 승패

    /// <summary>
    /// 패배 (방향 변경 §0-1): 도시(대도시 · 소도시)를 하나도 갖지 않거나 유닛이 전멸하면 탈락.
    /// 대도시(headquarters)가 하나도 없는 채로 start.revealDelay 가 지나면 위치 공개.
    /// </summary>
    int[] aliveCount = Array.Empty<int>();

    void UpdateVictory(float dt)
    {
        if (Winner != 0) return;
        if (BattleMode) { UpdateBattleVictory(); return; }
        if (Mission != null) { UpdateMission(dt); return; }   // 미션: 목표 · 실패 조건으로 승패 (Mission/WorldMission.cs)   // 교전 테스트: 전멸 규칙 (Battle.cs)
        if (aliveCount.Length != Factions.Count) aliveCount = new int[Factions.Count];
        Array.Clear(aliveCount);
        foreach (var u in Units) if (u.Hp > 0 && !u.Surrendered && (uint)u.Owner < (uint)aliveCount.Length) aliveCount[u.Owner]++;
        for (int i = 1; i < Factions.Count; i++)
        {
            var f = Factions[i];
            if (f.Defeated) continue;
            bool city = false, anyHq = false;
            foreach (var b in Buildings)
            {
                if (b.Owner != i) continue;
                if (b.Def.City) city = true;
                if (b.Def.Headquarters) anyHq = true;
            }
            if (!city || aliveCount[i] == 0)
            {
                f.Defeated = true;
                foreach (var u in Units)
                    if (u.Owner == i) u.Hp = 0;
                // 멸망한 세력의 점령 시설은 중립으로 (같은 팀이던 세력도 다시 점령할 수 있게)
                foreach (var b in Buildings)
                    if (b.Def.Capturable && b.Owner == i) { b.Owner = 0; b.CaptureFaction = 0; b.CaptureProgress = 0; }
                continue;
            }
            f.NoHqTime = anyHq ? 0 : f.NoHqTime + dt;
            f.Revealed = f.NoHqTime >= Defs.Start.RevealDelay;
        }
        var alive = Factions.Skip(1).Where(f => !f.Defeated).ToList();
        if (alive.Count == 1) { Winner = alive[0].Id; Winners.Add(Winner); }
        else if (alive.Count == 0) Winner = -1;   // 무승부
        else if (AllMutuallyAllied(alive))
        {
            // 공동 승리: 살아남은 세력이 모두 같은 정적 팀
            Winner = alive[0].Id;
            Winners.AddRange(alive.Select(f => f.Id));
        }
    }
}
