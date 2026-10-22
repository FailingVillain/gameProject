namespace KoreanDivision.UI;

/// <summary>[T38h] UI 퍼즈용 읽기 전용 관찰 구멍 (상태를 바꾸지 않는다).</summary>
public sealed partial class CampaignMenu
{
    public string FuzzView => currentView.ToString();
    public int FuzzTab => (int)currentTab;
    public bool FuzzSlotsOpen => slotsOpen;
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
