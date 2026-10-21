namespace KoreanDivision.Core;

/// <summary>
/// 리플레이 머리말의 미션 정보: 미션 정의 JSON 원문 (파일이 나중에 바뀌어도 재현) + 배치 단계 결과.
/// 배치는 명령이 아니라 시작 상태 — CreateMissionWorld 가 같은 목록을 다시 적용하면 같은 틱 0 상태가 된다.
/// </summary>
public sealed class MissionReplayInfo
{
    public string Id { get; set; } = "";
    public string Hash { get; set; } = "";
    public string Json { get; set; } = "";
    public List<DeployEntry> Deployment { get; set; } = new();

    public static MissionReplayInfo? From(World w) => w.Mission is not { } m ? null : new MissionReplayInfo
    {
        Id = m.Def.Id, Hash = m.Def.Hash, Json = m.Def.Json, Deployment = new List<DeployEntry>(m.Deployment),
    };

    public World CreateWorld(Defs defs, int seed) => World.CreateMissionWorld(MissionDef.Parse(Json, Id), defs, seed, Deployment);
}
