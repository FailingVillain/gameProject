using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>T47 작전 지도 확대: 배율 범위 · 중심 범위 · 커서 고정 · 거점 클릭 좌표 변환 (여러 배율 · 뷰포트) · 캠페인 상태 불변. 창 없이. SaveCompatSelfTest 에서 부른다.</summary>
public static class MapZoomSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        var vps = new[] { new Rectangle(300, 110, 700, 210), new Rectangle(280, 100, 720, 258), new Rectangle(10, 10, 400, 100), new Rectangle(0, 0, 1200, 600) };

        // 배율 범위: 마구 줌 인 · 아웃 (NaN · 0 · 음수 · 거대한 값 포함) 해도 [1, 8], 중심은 지도 안
        var rng = new Random(47);
        int badZoom = 0, badCenter = 0;
        foreach (var vp in vps)
        {
            var v = new MapZoomView();
            for (int i = 0; i < 400; i++)
            {
                float f = i % 50 == 0 ? float.NaN : i % 51 == 0 ? 0 : i % 52 == 0 ? -2 : i % 53 == 0 ? 1e9f : (float)(rng.NextDouble() * 2.2 + 0.4);
                v.ZoomAt(new Vector2(vp.X + (float)rng.NextDouble() * vp.Width, vp.Y + (float)rng.NextDouble() * vp.Height), f, vp);
                v.PanPixels(new Vector2(rng.Next(-300, 300), rng.Next(-300, 300)), vp);
                if (!(v.Zoom >= 1f && v.Zoom <= 8f)) badZoom++;
                if (!(v.Center.X >= 0 && v.Center.X <= MapZoomView.WorldW && v.Center.Y >= 0 && v.Center.Y <= MapZoomView.WorldH)) badCenter++;
            }
        }
        check(badZoom == 0 && badCenter == 0, $"지도 확대: 배율 [1,8] 와 중심 지도 안 유지 (무작위 1600회, NaN/0/음수/거대 인자 포함; 벗어남 {badZoom}/{badCenter})");

        // 맞춤에서는 이동해도 그대로, 최대 확대에서 가장자리 너머로 못 민다
        var fit = new MapZoomView(); var vp0 = vps[0];
        fit.PanPixels(new Vector2(500, -500), vp0);
        var tl = fit.ToScreen(Vector2.Zero, vp0); var br = fit.ToScreen(new Vector2(MapZoomView.WorldW, MapZoomView.WorldH), vp0);
        bool fitStill = fit.Center == new Vector2(MapZoomView.WorldW / 2, MapZoomView.WorldH / 2) && tl.X >= vp0.X - 0.01f && br.X <= vp0.X + vp0.Width + 0.01f && tl.Y >= vp0.Y - 0.01f && br.Y <= vp0.Y + vp0.Height + 0.01f;
        var big = new MapZoomView(); big.ZoomAt(new Vector2(vp0.X, vp0.Y), 100f, vp0); big.PanPixels(new Vector2(99999, 99999), vp0);
        var btl = big.ToScreen(Vector2.Zero, vp0); var bbr = big.ToScreen(new Vector2(MapZoomView.WorldW, MapZoomView.WorldH), vp0);
        bool edgeOk = big.Zoom == 8f && btl.X <= vp0.X + 0.01f && btl.Y <= vp0.Y + 0.01f && bbr.X >= vp0.X + vp0.Width - 0.01f && bbr.Y >= vp0.Y + vp0.Height - 0.01f;
        check(fitStill && edgeOk, $"지도 확대: 맞춤에서는 이동 무효 · 최대 확대에서도 지도 가장자리가 뷰포트를 넘어 밀리지 않음 (맞춤 {fitStill}, 가장자리 {edgeOk})");

        // 커서 고정: 커서 아래 월드 점이 확대 전후 같다 (클램프에 안 걸리는 가운데 부근)
        var cz = new MapZoomView(); var cvp = vps[3];
        cz.ZoomAt(new Vector2(cvp.X + cvp.Width / 2, cvp.Y + cvp.Height / 2), 2f, cvp);
        var cur = new Vector2(cvp.X + cvp.Width * 0.55f, cvp.Y + cvp.Height * 0.5f);
        Vector2 before = cz.ToWorld(cur, cvp);
        cz.ZoomAt(cur, 1.5f, cvp);
        Vector2 after = cz.ToWorld(cur, cvp);
        check(Vector2.Distance(before, after) < 0.05f && MathF.Abs(cz.Zoom - 3f) < 0.001f, $"지도 확대: 휠 확대는 커서 아래 월드 점을 고정 (오차 {Vector2.Distance(before, after):0.000})");

        // 클릭 좌표 변환: 여러 배율 · 뷰포트에서 거점을 뷰포트 한가운데로 가져와 (= 화면 점) 클릭하면 그 거점, 월드 ↔ 화면 왕복
        int hitBad = 0, rtBad = 0, total = 0;
        foreach (var vp in vps)
            foreach (float z in new[] { 1f, 1.5f, 2f, 4f, 8f })
                for (int n = 0; n < MapZoomView.NodePos.Length; n++)
                {
                    var v = new MapZoomView(); v.SetView(z, MapZoomView.NodePos[n]); v.Clamp(vp);
                    Vector2 p = v.ToScreen(MapZoomView.NodePos[n], vp);
                    total++;
                    // 클램프로 가운데에 못 온 경우도 있으니, 화면 안에 있을 때만 요구한다
                    bool inside = p.X >= vp.X && p.X <= vp.X + vp.Width && p.Y >= vp.Y && p.Y <= vp.Y + vp.Height;
                    if (!inside) { hitBad++; continue; }
                    int h = v.HitNode(p, vp, 14f);
                    // 맞춤 배율에서 이웃 거점이 14px 안에 겹치면 가장 가까운 쪽 = 자기 자신이어야 한다
                    if (h != n) hitBad++;
                    Vector2 w = v.ToWorld(p, vp);
                    if (Vector2.Distance(w, MapZoomView.NodePos[n]) > 0.01f) rtBad++;
                    // 뷰포트 밖 점은 선택 불가, 멀리 빗나간 점도 불가
                    if (v.HitNode(new Vector2(vp.X - 5, vp.Y - 5), vp, 1e6f) != -1) hitBad++;
                }
        check(hitBad == 0 && rtBad == 0, $"지도 확대: 거점 클릭 좌표 변환이 배율 1~8 x 뷰포트 4종 x 거점 8개 ({total}건) 에서 정확 (오판 {hitBad}, 왕복 오차 {rtBad})");

        // 맞춤 보기에서 8개 거점이 모두 뷰포트 안 (작은 뷰포트 포함)
        int outside = 0;
        foreach (var vp in vps)
        {
            var v = new MapZoomView();
            foreach (var np in MapZoomView.NodePos) { var p = v.ToScreen(np, vp); if (p.X < vp.X || p.X > vp.X + vp.Width || p.Y < vp.Y || p.Y > vp.Y + vp.Height) outside++; }
        }
        check(outside == 0, $"지도 확대: 맞춤 보기에서 거점 8개가 모든 뷰포트 안 (밖 {outside})");

        // 맞춤 복귀 · 캠페인 상태 불변: 확대 · 이동 · 복귀가 CampaignState 를 건드리지 않는다 (뷰는 상태에 접근하지 않음; JSON 동일)
        var st = new CampaignState(); st.EnsureInitialized(); st.Completed.Add(CampaignState.FirstMission); st.Day = 12;
        string j0 = st.ToJson();
        var rv = new MapZoomView(); var rvp = vps[1];
        rv.ZoomAt(new Vector2(rvp.X + 50, rvp.Y + 40), 5f, rvp); rv.PanPixels(new Vector2(-80, 30), rvp);
        bool moved = rv.Zoom > 1f;
        rv.Reset();
        check(moved && rv.IsFit && rv.Center == new Vector2(MapZoomView.WorldW / 2, MapZoomView.WorldH / 2) && st.ToJson() == j0, "지도 확대: [맞춤] 이 배율 1 · 중심으로 복귀하고 캠페인 상태 JSON 은 그대로");
    }
}
