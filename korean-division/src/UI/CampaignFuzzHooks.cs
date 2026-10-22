namespace KoreanDivision.UI;

/// <summary>[T38h] UI 퍼즈용 읽기 전용 관찰 구멍 (상태를 바꾸지 않는다).</summary>
public sealed partial class CampaignMenu
{
    public string FuzzView => currentView.ToString();
    public int FuzzTab => (int)currentTab;
    public bool FuzzSlotsOpen => slotsOpen;
    /// <summary>[T47] 지도 탭 확대 관찰: 직전 뷰포트 · 배율 · 중심, 캠페인 상태 JSON (확대 · 이동이 상태를 안 바꿨는지 대조).</summary>
    public System.Numerics.Vector2 FuzzMapViewportCenter => new(mapVp.X + mapVp.Width / 2, mapVp.Y + mapVp.Height / 2);
    public System.Numerics.Vector2 FuzzMapViewportSize => new(mapVp.Width, mapVp.Height);
    public float FuzzMapZoom => mapView.Zoom;
    public System.Numerics.Vector2 FuzzMapCenter => mapView.Center;
    public string FuzzStateJson => state.ToJson();
    public static int FuzzTabCount => 15;
    /// <summary>탭 번호(1~15) 의 버튼 글씨와, 그 탭이 속한 화면 띠 버튼 글씨 앞부분.</summary>
    public static (string Tab, string Screen) FuzzTabLabels(int n)
    {
        var t = (Tab)n;
        string[] labels = { S.CmScreenLeft, S.CmScreenCenter, S.CmScreenRight };
        for (int s = 0; s < ScreenTabs.Length; s++)
            if (Array.IndexOf(ScreenTabs[s], t) >= 0) return (TabLabel(t), labels[s]);
        return (TabLabel(t), labels[1]);
    }
}
