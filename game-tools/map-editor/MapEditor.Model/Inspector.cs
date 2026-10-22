namespace MapEditor.Model;

/// <summary>검사 결과 한 줄. (X, Y) = 타일 좌표 (항목을 누르면 화면이 여기로 이동).</summary>
public sealed record Issue(bool Error, string Kind, string Message, double X, double Y);

/// <summary>검사에 필요한 게임 쪽 사실을 콜백으로 받는다 (모델 프로젝트는 게임을 참조하지 않는다).</summary>
public sealed class InspectEnv
{
    /// <summary>지형상 통행 가능한 칸인가 (null = 지도를 못 만들었음 -> 통행 검사 생략).</summary>
    public Func<int, int, bool>? Walkable;
    /// <summary>소품 종류의 발자국 반지름 (칸). null = 모르는 종류.</summary>
    public Func<string, double?> PropRadius = _ => 0.5;
    /// <summary>시설 종류의 한 변 (칸). 0 이하 = 모르는 종류.</summary>
    public Func<string, int> SiteSize = _ => 3;
    public Func<string, bool> UnitExists = _ => true;
}

/// <summary>
/// 배치 검사 (헤드리스): 시작 -- 지도 안 / 통행 가능 / 갇힘 / 서로 이어짐 / 최소 거리, 시설 -- 종류 · 소유 · 겹침 · 시작 위, 소품 -- 종류 · 겹침 · 시설 위,
/// 구역 -- 모양 · 지도 밖 · 참조 끊김 · 목표 구역 도달성, 유닛 -- 종류 · 세력. 순서가 고정이라 같은 문서 -> 같은 목록.
/// 이어짐은 지형 통행 가능 칸의 8방향 연결 (모서리 가로지르기 없음) -- 게임 길찾기 호출이 아니라 같은 통행 규칙의 바닥 검사다.
/// </summary>
public static class Inspector
{
    public const int TrapCells = 25;      // 이보다 작은 연결 덩어리에 있는 시작은 갇힘
    public const double MinStartGap = 12; // 시작끼리 최소 거리 (칸)

    public static List<Issue> Run(MapDoc doc, MissionDoc? m, InspectEnv env)
    {
        var res = new List<Issue>();
        int W = doc.Width, H = doc.Height;
        bool inMap(double x, double y) => x >= 0 && y >= 0 && x < W && y < H;
        bool ok = env.Walkable != null && W >= 1 && H >= 1 && W <= MapDoc.MaxDim && H <= MapDoc.MaxDim;
        bool Walk(int x, int y) => ok && (uint)x < (uint)W && (uint)y < (uint)H && env.Walkable!(x, y);

        // 연결 덩어리 (시작 / 구역 도달성 용)
        int[]? comp = null; List<int>? compSize = null;
        if (ok)
        {
            comp = new int[W * H]; compSize = new List<int>();
            var q = new Queue<int>();
            for (int s = 0; s < comp.Length; s++)
            {
                if (comp[s] != 0 || !Walk(s % W, s / W)) continue;
                int id = compSize.Count + 1, n = 0;
                comp[s] = id; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(), cx = c % W, cy = c / W; n++;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (!Walk(nx, ny) || comp[ny * W + nx] != 0) continue;
                        if (dx != 0 && dy != 0 && (!Walk(cx + dx, cy) || !Walk(cx, cy + dy))) continue;
                        comp[ny * W + nx] = id; q.Enqueue(ny * W + nx);
                    }
                }
                compSize.Add(n);
            }
        }
        int CompAt(double x, double y) => comp != null && inMap(x, y) ? comp[(int)y * W + (int)x] : 0;

        // ---- 시작
        var st = doc.Starts;
        for (int i = 0; i < st.Count; i++)
        {
            var s = st[i];
            if (!inMap(s[0], s[1])) { res.Add(new Issue(true, "start", $"시작 {i + 1} 이 지도 밖", s[0], s[1])); continue; }
            if (ok && !Walk(s[0], s[1])) { res.Add(new Issue(true, "start", $"시작 {i + 1} 이 통행 불가 칸", s[0], s[1])); continue; }
            int c = CompAt(s[0], s[1]);
            if (c > 0 && compSize![c - 1] < TrapCells) res.Add(new Issue(true, "start", $"시작 {i + 1} 이 갇힘 (이어진 칸 {compSize[c - 1]}개)", s[0], s[1]));
        }
        if (st.Count > 0 && inMap(st[0][0], st[0][1]) && CompAt(st[0][0], st[0][1]) > 0)
            for (int i = 1; i < st.Count; i++)
            {
                var s = st[i];
                if (inMap(s[0], s[1]) && CompAt(s[0], s[1]) > 0 && CompAt(s[0], s[1]) != CompAt(st[0][0], st[0][1]))
                    res.Add(new Issue(false, "start", $"시작 {i + 1} 이 시작 1 과 걸어서 이어지지 않음 (강 · 바위로 갈림, 다리가 없으면 의도인지 확인)", s[0], s[1]));
            }
        for (int i = 0; i < st.Count; i++)
        for (int j = i + 1; j < st.Count; j++)
        {
            double dx = st[i][0] - st[j][0], dy = st[i][1] - st[j][1];
            if (dx * dx + dy * dy < MinStartGap * MinStartGap) res.Add(new Issue(false, "start", $"시작 {i + 1} 과 {j + 1} 이 {MinStartGap:0} 칸보다 가까움", st[i][0], st[i][1]));
        }
        if (st.Count < 2) res.Add(new Issue(true, "start", $"시작 위치가 {st.Count}개 (2개 이상 필요)", W / 2.0, H / 2.0));

        // ---- 시설
        var sites = doc.Sites;
        var foot = new List<(int X0, int Y0, int X1, int Y1)>();   // [X0,X1) x [Y0,Y1)
        for (int i = 0; i < sites.Count; i++)
        {
            var s = sites[i];
            int size = env.SiteSize(s.Type);
            if (size <= 0) { res.Add(new Issue(true, "site", $"시설 {i + 1} 종류 '{s.Type}' 없음 (capturable 건물만)", s.X, s.Y)); foot.Add((s.X, s.Y, s.X, s.Y)); continue; }
            int x0 = s.X - size / 2, y0 = s.Y - size / 2;
            foot.Add((x0, y0, x0 + size, y0 + size));
            if (!inMap(s.X, s.Y)) { res.Add(new Issue(true, "site", $"시설 {i + 1} ({s.Type}) 이 지도 밖", s.X, s.Y)); continue; }
            if (s.Owner < 0 || s.Owner > st.Count) res.Add(new Issue(true, "site", $"시설 {i + 1} 소유 세력 {s.Owner} 이 범위 밖 (0~{st.Count})", s.X, s.Y));
            if (x0 < 0 || y0 < 0 || x0 + size > W || y0 + size > H) res.Add(new Issue(false, "site", $"시설 {i + 1} ({s.Type}) 몸체가 지도 가장자리를 넘음", s.X, s.Y));
            if (ok && !Walk(s.X, s.Y)) res.Add(new Issue(false, "site", $"시설 {i + 1} ({s.Type}) 가운데가 통행 불가 칸 (물 · 바위 위)", s.X, s.Y));
            for (int k = 0; k < st.Count; k++)
                if (st[k][0] >= x0 - 1 && st[k][0] <= x0 + size && st[k][1] >= y0 - 1 && st[k][1] <= y0 + size)
                    res.Add(new Issue(false, "site", $"시설 {i + 1} ({s.Type}) 이 시작 {k + 1} 과 겹침", s.X, s.Y));
        }
        for (int i = 0; i < foot.Count; i++)
        for (int j = i + 1; j < foot.Count; j++)
        {
            var a = foot[i]; var b = foot[j];
            if (a.X0 < b.X1 && a.X1 > b.X0 && a.Y0 < b.Y1 && a.Y1 > b.Y0)
                res.Add(new Issue(false, "site", $"시설 {i + 1} ({sites[i].Type}) 과 {j + 1} ({sites[j].Type}) 이 겹침", sites[i].X, sites[i].Y));
        }

        // ---- 유닛 (지도 파일 units[])
        for (int i = 0; i < doc.Units.Count; i++)
        {
            var u = doc.Units[i];
            if (!env.UnitExists(u.Unit)) res.Add(new Issue(true, "unit", $"유닛 {i + 1} 종류 '{u.Unit}' 없음", u.X, u.Y));
            if (!inMap(u.X, u.Y)) res.Add(new Issue(true, "unit", $"유닛 {i + 1} ({u.Unit}) 이 지도 밖", u.X, u.Y));
            else if (ok && !Walk((int)u.X, (int)u.Y)) res.Add(new Issue(false, "unit", $"유닛 {i + 1} ({u.Unit}) 이 통행 불가 칸", u.X, u.Y));
            if (u.Faction < 1 || u.Faction > st.Count) res.Add(new Issue(true, "unit", $"유닛 {i + 1} 세력 {u.Faction} 이 범위 밖 (1~{st.Count})", u.X, u.Y));
            if (u.Count < 1) res.Add(new Issue(true, "unit", $"유닛 {i + 1} 수 {u.Count}", u.X, u.Y));
        }

        if (m == null) return res;

        // ---- 소품
        var pr = m.Props;
        var rad = new double[pr.Count];
        for (int i = 0; i < pr.Count; i++)
        {
            var p = pr[i];
            double? r = env.PropRadius(p.Type);
            rad[i] = r ?? 0.5;
            if (r == null) res.Add(new Issue(true, "prop", $"소품 {i + 1} 종류 '{p.Type}' 이 props.json 에 없음", p.X, p.Y));
            if (!inMap(p.X, p.Y)) { res.Add(new Issue(true, "prop", $"소품 {i + 1} ({p.Type}) 이 지도 밖", p.X, p.Y)); continue; }
            if (ok && !Walk((int)p.X, (int)p.Y)) res.Add(new Issue(false, "prop", $"소품 {i + 1} ({p.Type}) 이 통행 불가 칸 위 (물 · 바위)", p.X, p.Y));
            for (int k = 0; k < foot.Count; k++)
                if (p.X >= foot[k].X0 && p.X < foot[k].X1 && p.Y >= foot[k].Y0 && p.Y < foot[k].Y1)
                    res.Add(new Issue(false, "prop", $"소품 {i + 1} ({p.Type}) 이 시설 {k + 1} 몸체 위", p.X, p.Y));
            for (int k = 0; k < st.Count; k++)
            {
                double dx = p.X - (st[k][0] + 0.5), dy = p.Y - (st[k][1] + 0.5);
                if (dx * dx + dy * dy < 1.5 * 1.5) res.Add(new Issue(false, "prop", $"소품 {i + 1} ({p.Type}) 이 시작 {k + 1} 에 너무 가까움", p.X, p.Y));
            }
        }
        for (int i = 0; i < pr.Count; i++)
        for (int j = i + 1; j < pr.Count; j++)
        {
            double dx = pr[i].X - pr[j].X, dy = pr[i].Y - pr[j].Y, rr = rad[i] + rad[j];
            if (dx * dx + dy * dy < rr * rr) res.Add(new Issue(false, "prop", $"소품 {i + 1} ({pr[i].Type}) 과 {j + 1} ({pr[j].Type}) 이 겹침", pr[i].X, pr[i].Y));
        }

        // ---- 구역 / 배치 구역
        foreach (var a in m.Areas)
        {
            var (cx, cy) = a.Center;
            if (!a.IsValid) { res.Add(new Issue(true, "area", $"구역 '{a.Name}' 모양 오류 (rect [x,y,w,h] / circle [cx,cy,r], 크기 > 0)", cx, cy)); continue; }
            if (Outside(a.Rect, a.Circle, W, H)) res.Add(new Issue(false, "area", $"구역 '{a.Name}' 이 지도 밖에 있음", cx, cy));
            else if (Partly(a.Rect, a.Circle, W, H)) res.Add(new Issue(false, "area", $"구역 '{a.Name}' 일부가 지도 밖", cx, cy));
        }
        foreach (var r in m.Refs)
        {
            var a = m.Areas.FirstOrDefault(x => x.Name == r.Name);
            if (a == null) { res.Add(new Issue(true, "ref", $"{r.Where} 이 없는 구역 '{r.Name}' 을 가리킴", W / 2.0, H / 2.0)); continue; }
            if (!r.IsObjective || !a.IsValid) continue;
            var (cx, cy) = a.Center;
            if (!inMap(cx, cy)) continue;
            if (ok && !Walk((int)cx, (int)cy)) res.Add(new Issue(false, "ref", $"목표 구역 '{a.Name}' 중심이 통행 불가 칸", cx, cy));
            else if (st.Count > 0 && inMap(st[0][0], st[0][1]) && CompAt(cx, cy) > 0 && CompAt(st[0][0], st[0][1]) > 0 && CompAt(cx, cy) != CompAt(st[0][0], st[0][1]))
                res.Add(new Issue(false, "ref", $"목표 구역 '{a.Name}' 이 시작 1 에서 걸어서 닿지 않음 (다리 · 길 확인)", cx, cy));
        }
        for (int i = 0; i < m.Zones.Count; i++)
        {
            var z = m.Zones[i]; var (cx, cy) = z.Center;
            if (!z.IsValid) { res.Add(new Issue(true, "zone", $"배치 구역 {i + 1} 모양 오류", cx, cy)); continue; }
            if (m.FactionCount > 0 && (z.Faction < 1 || z.Faction > m.FactionCount)) res.Add(new Issue(true, "zone", $"배치 구역 {i + 1} 세력 {z.Faction} 이 범위 밖 (1~{m.FactionCount})", cx, cy));
            if (Outside(z.Rect, z.Circle, W, H)) res.Add(new Issue(false, "zone", $"배치 구역 {i + 1} 이 지도 밖", cx, cy));
            else if (Partly(z.Rect, z.Circle, W, H)) res.Add(new Issue(false, "zone", $"배치 구역 {i + 1} 일부가 지도 밖", cx, cy));
        }
        foreach (var s in m.LoadIssues) res.Add(new Issue(true, "file", s, W / 2.0, H / 2.0));
        return res;
    }

    static bool Outside(double[]? rect, double[]? circle, int W, int H)
    {
        if (rect is { Length: 4 } r) return r[0] >= W || r[1] >= H || r[0] + r[2] <= 0 || r[1] + r[3] <= 0;
        if (circle is { Length: 3 } c) return c[0] + c[2] < 0 || c[1] + c[2] < 0 || c[0] - c[2] >= W || c[1] - c[2] >= H;
        return false;
    }

    static bool Partly(double[]? rect, double[]? circle, int W, int H)
    {
        if (rect is { Length: 4 } r) return r[0] < 0 || r[1] < 0 || r[0] + r[2] > W || r[1] + r[3] > H;
        if (circle is { Length: 3 } c) return c[0] - c[2] < 0 || c[1] - c[2] < 0 || c[0] + c[2] >= W || c[1] + c[2] >= H;
        return false;
    }
}
