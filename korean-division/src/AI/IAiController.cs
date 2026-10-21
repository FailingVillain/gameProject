namespace KoreanDivision.Core;

/// <summary>
/// 교체 가능한 AI. World.Step 이 세력마다 초당 4회(15틱마다) Tick 을 호출한다.
/// 규칙: 게임 조작은 World 의 공개 명령 메서드(Move/AttackMove/Attack/Build/Train/...)로만 하고,
/// 적 정보는 world.Fog.CanSee(faction, ...) 로 보이는 것만 사용한다 (치트 금지).
/// </summary>
public interface IAiController
{
    string Name { get; }
    void Tick(World world, int faction);
}

public static class AiRegistry
{
    /// <summary>이름 → 생성자. 새 AI 는 여기에 등록하면 --ai 이름 으로 선택 가능.</summary>
    public static readonly Dictionary<string, Func<IAiController>> All = new()
    {
        ["tactical"] = () => new TacticalAi(),   // 전술 AI 1단계 (지휘관 → 대대 → 중대) — 기본. "tactical:<프로필>" 로 프로필 고정 (비교용)
        ["passive"] = () => new PassiveAi(),
        ["battle"] = () => new BattleAi(),   // 옛 교전 테스트 AI (비교용)
        ["mission-aux"] = () => new MissionAi(true),   // 전술 AI 옆 보조 (비무장 · 증원 무리만)
        ["mission"] = () => new MissionAi(),   // 미션 전용 (세력 자세: attack · defend · hold)
        ["standard"] = () => new StandardAi(),
        ["rush"] = () => new StandardAi(new StandardAiSettings("rush", WaveSize: 6, WaveGrowth: 3,
            MaxWave: 20, SquadSize: 2, GuardPerPoint: 1, ReactionDelay: 0.25f, UseScout: false)),
    };

    /// <summary>기본 AI 이름 (표준 게임 · 교전 테스트 · 빠른 교전).</summary>
    public const string Default = TacticalAi.BaseName;

    public static IAiController? Create(string name)
    {
        name = name.ToLowerInvariant();
        if (name.StartsWith(TacticalAi.BaseName + ":", StringComparison.Ordinal))
        {
            string prof = name[(TacticalAi.BaseName.Length + 1)..];
            return AiProfiles.Find(prof) != null ? new TacticalAi(prof) : null;
        }
        return All.TryGetValue(name, out var make) ? make() : null;
    }

    /// <summary>--ai 이름 검증용 (등록된 이름 또는 tactical:&lt;프로필&gt;).</summary>
    public static bool IsKnown(string name) => Create(name) != null;
}
