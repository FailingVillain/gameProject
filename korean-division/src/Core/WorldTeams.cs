namespace KoreanDivision.Core;

/// <summary>
/// 정적 팀 (외교 시스템 대체, 2026-10-19). 지도 파일 factions[].team 이 같은 (0 이 아닌) 세력끼리 동맹:
/// 서로 공격하지 않고 시야를 나누며, 탄약 회복 · 공동 승리에서 아군으로 친다. 팀 정보가 없으면 모든 세력이 서로 적.
/// 팀은 게임 시작 전에 정해지고 실행 중에는 절대 바뀌지 않는다 (명령 없음). Faction.Team 에 저장 · 체크섬 포함.
/// </summary>
public sealed partial class World
{
    bool ValidFaction(int f) => f > 0 && f < Factions.Count;

    /// <summary>같은 세력이거나 같은 정적 팀 (중립 세력 0 은 누구와도 동맹이 아님).</summary>
    public bool Allied(int a, int b) =>
        a == b ? a > 0 : ValidFaction(a) && ValidFaction(b) && Factions[a].Team != 0 && Factions[a].Team == Factions[b].Team;

    /// <summary>자동 교전 · 강제 공격 · 점령 경합 대상인지: 서로 다른 (중립이 아닌) 세력이고 같은 팀이 아님.</summary>
    public bool IsHostile(int a, int b) => a != b && ValidFaction(a) && ValidFaction(b) && !Allied(a, b);

    /// <summary>게임 시작 전 설정 단계 전용 (CreateStandard · 테스트): 세력의 정적 팀 지정. 0 = 팀 없음 (모두와 적).</summary>
    internal void AssignTeam(int faction, int team)
    {
        if (ValidFaction(faction)) Factions[faction].Team = Math.Max(0, team);
    }

    /// <summary>살아남은 세력이 모두 같은 팀인지 (공동 승리 조건).</summary>
    bool AllMutuallyAllied(List<Faction> alive)
    {
        for (int i = 1; i < alive.Count; i++)
            if (!Allied(alive[0].Id, alive[i].Id)) return false;
        return true;
    }
}
