using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>TC-A 브리핑 정보 3단계: 단계 문구 선택 (표시 전용) · 실제 배치와 분리 · 저장/불러오기 후 단계 유지. SaveCompatSelfTest 에서 부른다.</summary>
public static class CampaignBriefingSelfTest
{
    public static void Run(Action<bool, string> check)
    {
        var d = new MissionDef { Briefing = "기본", IntelVar = "intel.andong", BriefingTiers = new() { "약 3,000", "약 2,000", "1,700" } };
        var s = new CampaignState();
        string At(CampaignState st) => d.BriefingFor(k => st.Flags.GetValueOrDefault(k, 0));
        check(At(s) == "약 3,000", "TC-A 브리핑: 변수 없음 = 0단계 문구");
        s.Flags["intel.andong"] = 1;
        check(At(s) == "약 2,000", "TC-A 브리핑: intel.andong 1 = 정찰 뒤 문구");
        s.Flags["intel.andong"] = 2;
        var back = CampaignState.FromJson(s.ToJson());
        check(back != null && At(back) == "1,700", "TC-A 브리핑: 저장/불러오기 후 단계 유지 (확정 문구)");
        s.Flags["intel.andong"] = 9; s.Flags["x"] = -4;
        check(At(s) == "1,700" && d.BriefingFor(k => -1) == "약 3,000", "TC-A 브리핑: 범위 밖 단계는 0..끝으로 자름");
        check(d.BriefingFor(null) == "약 3,000", "TC-A 브리핑: 변수 읽기 없음(연습·단독 열람) = 0단계");
        check(new MissionDef { Briefing = "기본" }.BriefingFor(k => 2) == "기본" && new MissionDef { Briefing = "기본", BriefingTiers = new() { "a" } }.BriefingFor(null) == "a", "TC-A 브리핑: 단계 문구 없으면 Briefing 그대로 (기존 미션 영향 없음)");

        // 미션 파일 형식: briefingTiers / intelVar 는 표시용이고 배치 (forces) 는 그대로
        var def = JsonSerializer.Deserialize<MissionDef>("{\"id\":\"t\",\"briefing\":\"b\",\"intelVar\":\"intel.andong\",\"briefingTiers\":[\"p\",\"q\",\"r\"],\"forces\":[{\"faction\":2,\"name\":\"실제\"}]}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        check(def != null && def.BriefingTiers.Count == 3 && def.IntelVar == "intel.andong" && def.Forces.Count == 1 && def.BriefingFor(k => 1) == "q", "TC-A 브리핑: 미션 JSON briefingTiers/intelVar 읽기, Forces 와 독립");
    }
}
