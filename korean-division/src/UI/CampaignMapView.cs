using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>
/// [T47] 작전 지도 탭 (10번) 의 확대 가능한 노선도: 휠 (커서 기준) · [-][+] · [맞춤] · 끌어서 이동 · 키 +/-/0 · 배율 표시.
/// 상태는 <see cref="MapZoomView"/> (세션 한정, 캠페인 저장 · CampaignState 불변). 거점 클릭 = 그 작전 고르기 (목록의 [출격 준비] 와 같은 조건).
/// </summary>
public sealed partial class CampaignMenu
{
    readonly MapZoomView mapView = new();
    bool mapDrag;
    float mapDragMoved;
    Rectangle mapVp;   // 직전 프레임의 지도 뷰포트 (퍼즈 관찰용)

    /// <summary>거점 번호 → 고를 수 있는 작전 id (아직 작전이 없는 거점은 null).</summary>
    static readonly string?[] NodeMission = { CampaignState.Tutorial2, CampaignState.YaluCrossing, "c1_02", null, null, null, null, null };
    const float NodeHitRadius = 14f, DragStartSlop = 5f;

    bool MissionAvailable(string id)
    {
        bool done = state.Completed.Contains(id);
        bool isNext = state.NextMission == id || (string.IsNullOrEmpty(state.NextMission) && id == CampaignState.FirstMission);
        return (done || isNext || id == CampaignState.FirstMission) && File.Exists(MissionDef.PathOf(id));
    }

    /// <summary>확인 · 스크린샷용: KD_CAMPAIGN_MAPZOOM = 배율[:거점 번호] (예 "3", "3:1").</summary>
    void ApplyMapZoomOverride()
    {
        string? v = Environment.GetEnvironmentVariable("KD_CAMPAIGN_MAPZOOM");
        if (string.IsNullOrWhiteSpace(v)) return;
        var parts = v.Trim().Split(':');
        if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z)) return;
        Vector2 c = new(MapZoomView.WorldW / 2, MapZoomView.WorldH / 2);
        if (parts.Length > 1 && int.TryParse(parts[1], out int n) && n >= 0 && n < MapZoomView.NodePos.Length) c = MapZoomView.NodePos[n];
        mapView.SetView(z, c);
    }

    void DrawMapView(Rectangle vp)
    {
        mapVp = vp;
        mapView.Clamp(vp);
        bool overlayOpen = slotsOpen || saveRetry != null;
        bool over = !overlayOpen && Ui.Contains(vp, mouse);

        // 겹침 버튼 사각형 (뷰포트 오른쪽 위): 끌기 · 거점 클릭이 버튼 위에서 시작하지 않게 한다
        const float bw = 30, bh = 24, gap = 4;
        var rFit = new Rectangle(vp.X + vp.Width - 58 - 6, vp.Y + 6, 58, bh);
        var rPlus = new Rectangle(rFit.X - gap - bw, vp.Y + 6, bw, bh);
        var rMinus = new Rectangle(rPlus.X - gap - bw, vp.Y + 6, bw, bh);
        bool onOverlay = Ui.Contains(rFit, mouse) || Ui.Contains(rPlus, mouse) || Ui.Contains(rMinus, mouse);

        // ---- 입력: 휠 (커서 기준) · 키 · 끌기
        if (over)
        {
            float wheel = Input.GetMouseWheelMove();
            if (wheel != 0 && !float.IsNaN(wheel)) mapView.ZoomAt(mouse, MathF.Pow(MapZoomView.WheelFactor, Math.Clamp(wheel, -5f, 5f)), vp);
        }
        if (!overlayOpen && currentView == ViewMode.Home)
        {
            if (Input.IsKeyPressed(KeyboardKey.Equal) || Input.IsKeyPressed(KeyboardKey.KpAdd)) mapView.ZoomCenter(MapZoomView.ButtonFactor, vp);
            if (Input.IsKeyPressed(KeyboardKey.Minus) || Input.IsKeyPressed(KeyboardKey.KpSubtract)) mapView.ZoomCenter(1f / MapZoomView.ButtonFactor, vp);
            if (Input.IsKeyPressed(KeyboardKey.Zero) || Input.IsKeyPressed(KeyboardKey.Kp0)) mapView.Reset();
        }
        if (click && over && !onOverlay) { mapDrag = true; mapDragMoved = 0; }
        int pickNode = -1;
        if (mapDrag)
        {
            if (!overlayOpen && Input.IsMouseButtonDown(MouseButton.Left))
            {
                Vector2 d = Input.GetMouseDelta();
                if (!float.IsNaN(d.X) && !float.IsNaN(d.Y)) { mapDragMoved += MathF.Abs(d.X) + MathF.Abs(d.Y); mapView.PanPixels(d, vp); }
            }
            else
            {
                mapDrag = false;
                if (!overlayOpen && mapDragMoved < DragStartSlop) pickNode = mapView.HitNode(mouse, vp, NodeHitRadius);   // 끌지 않고 뗐으면 클릭
            }
        }

        // ---- 그리기 (뷰포트 안만)
        Ui.Panel(vp, Palette.Alpha(Palette.PanelDark, 200), Palette.PanelBorder);
        Raylib.BeginScissorMode((int)vp.X, (int)vp.Y, (int)vp.Width, (int)vp.Height);
        float scale = mapView.Scale(vp);

        // 바탕 격자 (확대 · 이동 감각용): 월드 80 단위
        for (float gx = 0; gx <= MapZoomView.WorldW; gx += 80)
        {
            Vector2 a = mapView.ToScreen(new Vector2(gx, 0), vp), b = mapView.ToScreen(new Vector2(gx, MapZoomView.WorldH), vp);
            Raylib.DrawLineV(a, b, Palette.Grid);
        }
        for (float gy = 0; gy <= MapZoomView.WorldH; gy += 80)
        {
            Vector2 a = mapView.ToScreen(new Vector2(0, gy), vp), b = mapView.ToScreen(new Vector2(MapZoomView.WorldW, gy), vp);
            Raylib.DrawLineV(a, b, Palette.Grid);
        }

        string[] names = {
            S.CmNodeYalu, S.CmNodeAndong, S.CmNodeFenghuang, S.CmNodeAnshan,
            S.CmNodeLiaoyang, S.CmNodeFengtian, S.CmNodeChangchun, S.CmNodeHarbin
        };
        float lw = mapView.LineWidth();
        for (int i = 0; i < names.Length - 1; i++)
        {
            Color lineCol = RouteDone(i) ? Palette.HpGood : Palette.PanelBorder;
            Raylib.DrawLineEx(mapView.ToScreen(MapZoomView.NodePos[i], vp), mapView.ToScreen(MapZoomView.NodePos[i + 1], vp), lw, lineCol);
        }

        int hover = over && !onOverlay ? mapView.HitNode(mouse, vp, NodeHitRadius) : -1;
        float rad = mapView.MarkerRadius();
        // 라벨 우선순위: 고를 수 있는 작전 거점 → 지금 거점 → 나머지. 겹치면 그리지 않음, 작은 배율에서는 보조 라벨 숨김.
        var order = Enumerable.Range(0, names.Length).OrderBy(i => i == hover ? 0 : NodeMission[i] != null ? 1 : 2).ThenBy(i => i).ToArray();
        var placed = new List<Rectangle>();
        var labelAt = new Dictionary<int, (string Text, Vector2 Pos, Color Col)>();
        foreach (int i in order)
        {
            Vector2 p = mapView.ToScreen(MapZoomView.NodePos[i], vp);
            if (p.X < vp.X - 40 || p.X > vp.X + vp.Width + 40 || p.Y < vp.Y - 40 || p.Y > vp.Y + vp.Height + 40) continue;
            bool isDone = RouteDone(i);
            bool isCurrent = !isDone && (i == 0 || RouteDone(i - 1)) && i < RouteMissionIds.Length;
            Color circleCol = isDone ? Palette.HpGood : isCurrent ? Palette.Accent : Palette.PanelBorder;
            Raylib.DrawCircleV(p, rad, circleCol);
            Raylib.DrawCircleLinesV(p, rad, i == hover ? Palette.Accent : Palette.PanelDark);

            bool major = NodeMission[i] != null || isCurrent || i == hover;
            if (!major && scale < 0.5f) continue;   // 줌 아웃 + 작은 뷰포트: 보조 거점 라벨 숨김
            string text = names[i];
            if (mapView.Zoom >= 2.5f && NodeMission[i] is { } mid) text += " · " + MissionShortName(mid);   // 크게 확대하면 작전 이름도
            Vector2 sz = Ui.Measure(text, 12);
            var lr = new Rectangle(p.X - sz.X / 2, p.Y + rad + 4, sz.X, sz.Y);
            var padded = new Rectangle(lr.X - 3, lr.Y - 1, lr.Width + 6, lr.Height + 2);
            if (placed.Any(o => Raylib.CheckCollisionRecs(o, padded))) continue;
            placed.Add(padded);
            labelAt[i] = (text, new Vector2(lr.X, lr.Y), isCurrent || i == hover ? Palette.Accent : Palette.Text);
        }
        foreach (var kv in labelAt) Ui.Text(kv.Value.Text, kv.Value.Pos.X, kv.Value.Pos.Y, 12, kv.Value.Col);
        Raylib.EndScissorMode();

        // 거점 클릭 등록 (퍼즈 겨냥용; 작전이 있고 고를 수 있을 때만)
        for (int i = 0; i < names.Length; i++)
        {
            if (NodeMission[i] == null || !MissionAvailable(NodeMission[i]!)) continue;
            Vector2 p = mapView.ToScreen(MapZoomView.NodePos[i], vp);
            if (Ui.Contains(vp, p)) Ui.RegHit(new Rectangle(p.X - rad, p.Y - rad, rad * 2, rad * 2), names[i]);
        }

        // ---- 겹침 UI: 배율 표시 · 버튼
        string zl = string.Format(S.CmMapZoomLabel, mapView.Zoom);
        Vector2 zsz = Ui.Measure(zl, 13), hsz = Ui.Measure(S.CmMapZoomHint, 12);
        var zr = new Rectangle(vp.X + 6, vp.Y + vp.Height - 24, MathF.Min(vp.Width - 12, zsz.X + hsz.X + 26), 20);
        Raylib.DrawRectangleRec(zr, Palette.Alpha(Palette.PanelDark, 210));
        Ui.Text(zl, zr.X + 6, zr.Y + 3, 13, Palette.Accent);
        if (zr.Width > zsz.X + hsz.X + 20) Ui.Text(S.CmMapZoomHint, zr.X + zsz.X + 18, zr.Y + 4, 12, Palette.TextDim);
        if (Button(rMinus, "-", mapView.Zoom > MapZoomView.MinZoom + 0.001f, 16)) mapView.ZoomCenter(1f / MapZoomView.ButtonFactor, vp);
        if (Button(rPlus, "+", mapView.Zoom < MapZoomView.MaxZoom - 0.001f, 16)) mapView.ZoomCenter(MapZoomView.ButtonFactor, vp);
        if (Button(rFit, S.CmMapFit, !mapView.IsFit || mapView.Center != new Vector2(MapZoomView.WorldW / 2, MapZoomView.WorldH / 2), 13)) mapView.Reset();

        // ---- 거점 고르기 (끌지 않고 뗀 클릭)
        if (pickNode >= 0 && NodeMission[pickNode] is { } id)
        {
            targetMissionId = id;
            if (MissionAvailable(id)) OpenPreOp(id);
        }
    }

    static string MissionShortName(string id) => id switch
    {
        CampaignState.Tutorial2 => S.CmMissionC002Name,
        CampaignState.YaluCrossing => S.CmMissionC101Name,
        _ => S.CmMissionNextName,
    };
}
