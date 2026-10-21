using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 좁은 길목 통과 질서 (choke, 2026-10-21 — 사용자 '좁은 길목에서 우왕좌왕').
///  ① 길목 칸 = 가로 · 세로 중 어느 한 쪽 걸을 수 있는 연속 폭이 chokeWidth 칸 이하 (다리 · 여울 · 골목 · 숲길). 폭 선 감지 (CheckNarrow) 는 그대로 2열 종대를 만들고,
///  ② 통과 큐: 앞길에 길목이 보이는 (chokeLookahead 칸) 중대 깃발은 같은 편 · 같은 방향의 앞선 중대 (길목 입구에 더 가깝거나, 구성원이 길목 안에 있는) 가 길목을 완전히 빠질 때까지 서서 기다린다
///     (도착 순서 · 중대 단위, 최대 chokeHoldMaxTicks). 앞 중대가 꼬리까지 빠지면 다음이 들어간다.
///  ③ 길목 안에 구성원이 있는 동안 종대 해제 · 빈자리 점검 (StepGapChecks) 을 하지 않는다 — 꼬리까지 빠진 뒤에 대형을 다시 짠다 (슬롯 재배정 없음).
///  ④ 길목 칸 위의 대형 이동 병사는 겹침 해소 밀림을 chokePushScale 배로 줄인다 (World.Separate).
/// 상태: Company.ChokeHold (연속으로 선 틱, 저장 · 체크섬). 길목 칸 캐시는 지도 Version 에서 다시 계산 (작업값).
/// </summary>
public sealed partial class World
{
    byte[]? chokeCache;
    int chokeCacheVersion = -1;

    /// <summary>(x, y) 가 길목 칸인가 — 가로 · 세로 걸을 수 있는 연속 폭 (자기 칸 포함) 중 작은 쪽이 chokeWidth 이하. 걸을 수 없는 칸은 길목이 아니다.</summary>
    internal bool ChokeTile(int x, int y)
    {
        if (!Map.InBounds(x, y)) return false;
        int n = Map.Width * Map.Height;
        if (chokeCache == null || chokeCache.Length != n || chokeCacheVersion != Map.Version)
        {
            chokeCache = new byte[n];
            chokeCacheVersion = Map.Version;
        }
        int i = y * Map.Width + x;
        byte v = chokeCache[i];
        if (v != 0) return v == 2;
        bool narrow = false;
        if (Map.IsWalkable(x, y))
        {
            int cap = Defs.CombatRules.ChokeWidth;
            int h = 1, vv = 1;
            for (int d = 1; d <= cap && Map.IsWalkable(x - d, y); d++) h++;
            for (int d = 1; d <= cap && h <= cap && Map.IsWalkable(x + d, y); d++) h++;
            for (int d = 1; d <= cap && Map.IsWalkable(x, y - d); d++) vv++;
            for (int d = 1; d <= cap && vv <= cap && Map.IsWalkable(x, y + d); d++) vv++;
            narrow = h <= cap || vv <= cap;
        }
        chokeCache[i] = (byte)(narrow ? 2 : 1);
        return narrow;
    }

    internal bool ChokeAt(Vector2 p) { var (x, y) = GameMap.TileOf(p); return ChokeTile(x, y); }

    /// <summary>구성원 중 하나라도 길목 칸 위에 있나.</summary>
    internal bool CompanyInChoke(Company c)
    {
        foreach (var u in c.Members) if (u.Alive && ChokeAt(u.Pos)) return true;
        return false;
    }

    // 앞길 감지 광선 방향 (진행 방향 기준 0 · ±25° · ±50°)
    static readonly Vector2[] ChokeRays = { new(1, 0), new(0.9063f, 0.4226f), new(0.9063f, -0.4226f), new(0.6428f, 0.7660f), new(0.6428f, -0.7660f) };

    /// <summary>깃발 앞길의 가장 가까운 길목 칸까지 거리 (lookahead 안에 없으면 -1). 깃발 자신이 길목 칸이면 0.</summary>
    internal float ChokeAhead(CompanyAnchor a, out Vector2 hit)
    {
        hit = a.Pos;
        if (ChokeAt(a.Pos)) return 0f;
        float look = Defs.CombatRules.ChokeLookahead;
        Vector2 h = a.Heading, r = new(-h.Y, h.X);
        for (float d = 1f; d <= look; d += 1f)
            foreach (var ray in ChokeRays)
            {
                Vector2 p = a.Pos + h * (d * ray.X) + r * (d * ray.Y);
                if (ChokeAt(p)) { hit = p; return d; }
            }
        return -1f;
    }

    /// <summary>
    /// 통과 큐: 이 깃발이 지금 서서 기다려야 하나 (StepAnchors 가 호출). 깃발이 이미 길목 안이거나 앞에 길목이 없으면 기다림 끝 (ChokeHold 0).
    /// 기다리는 조건 = 같은 편 · 같은 방향의 이동 중 깃발 Z 가 앞에 있고, Z 의 구성원이 길목 안에 있거나 Z 가 길목 입구에 더 가깝다 (같으면 중대 Id 낮은 쪽이 먼저).
    /// </summary>
    static float EnvF(string k, float d) { var v = Environment.GetEnvironmentVariable(k); return v == null ? d : float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); }
    static readonly float ChokeQueueGap = EnvF("KD_CQ_GAP", 3f), CqSlack = EnvF("KD_CQ_SLACK", 0f), CqEnter = EnvF("KD_CQ_ENTER", 0f);

    internal bool StepChokeQueue(Company c, CompanyAnchor a)
    {
        var rules = Defs.CombatRules;
        float mine = ChokeAhead(a, out Vector2 hit);
        if (mine <= 0f) { c.ChokeHold = 0; return false; }
        if (mine > ChokeQueueGap) { c.ChokeHold = 0; return false; }   // formation: 입구 바로 앞 (ChokeQueueGap 칸) 까지는 걸어가서 줄 선다 — 14칸 앞에서 서면 앞 중대가 빠진 뒤 그만큼 빈 시간 (통과 시간 +55%)
        foreach (var z in Companies)
        {
            if (ReferenceEquals(z, c) || z.Owner != c.Owner || z.Anchor is not { Arrived: false } za) continue;
            if (Vector2.Dot(za.Heading, a.Heading) < 0.3f) continue;
            Vector2 to = za.Pos - a.Pos;
            if (to.LengthSquared() > 60f * 60f || Vector2.Dot(to, a.Heading) <= 0f) continue;
            // Z 의 꼬리 (구성원) 가 아직 길목 입구 (내 앞길 길목 지점 hit) 뒤에 남아 있으면 — 입구를 다 지나야 다음 중대가 이어서 들어간다 (꼬리까지 빠질 때까지가 아니라 입구 통과 순서)
            // (이미 길목에 들어선 Z 만 — 아직 안 들어선 Z 끼리는 아래 도착 순서 규칙으로)
            bool entered = false, tailBehind = false;
            foreach (var m in z.Members)
            {
                if (!m.Alive) continue;
                if (ChokeAt(m.Pos)) { entered = true; continue; }
                Vector2 rel = m.Pos - hit;
                if (Vector2.Dot(rel, a.Heading) < -CqSlack && rel.LengthSquared() < 30f * 30f && Vector2.Dot(m.Pos - a.Pos, a.Heading) > 0f) tailBehind = true;
            }
            bool occupying = entered && tailBehind;
            float theirs = ChokeAhead(za, out _);
            bool ahead = theirs > 0f && (theirs < mine - 1f || (MathF.Abs(theirs - mine) <= 1f && z.Id < c.Id));
            if (!occupying && !ahead) continue;
            if (c.ChokeHold >= rules.ChokeHoldMaxTicks) return false;   // 너무 오래 서 있었다 — 그냥 간다 (교착 방지)
            c.ChokeHold++;
            return true;
        }
        c.ChokeHold = 0;
        return false;
    }

    // ---------------------------------------------------------------- 저장 · 체크섬 (ORG_ 끝, FormatVersion 45)

    void WriteChoke(SnapWriter w) { foreach (var c in Companies) w.I(c.ChokeHold); }

    void ReadChoke(SnapReader r)
    {
        foreach (var c in Companies)
        {
            c.ChokeHold = r.I();
            if (c.ChokeHold < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company choke hold"));
        }
    }

    void HashChoke(ref Hasher h) { foreach (var c in Companies) h.Add(c.ChokeHold); }
}
