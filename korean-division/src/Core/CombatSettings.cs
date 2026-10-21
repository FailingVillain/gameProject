namespace KoreanDivision.Core;

/// <summary>세력별 전투 탭 설정 (ROADMAP §7). 변경은 SetCombatSettingCommand 로만 — 리플레이 · 저장 · 체크섬 포함.</summary>
public sealed class CombatSettings
{
    public FormType DefaultFormation = FormType.Free;
}

public sealed partial class Faction
{
    [SnapExt] public readonly CombatSettings Combat = new();
}
