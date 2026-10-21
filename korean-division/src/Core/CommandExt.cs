namespace KoreanDivision.Core;

/// <summary>전투 탭 설정 항목.</summary>
public enum CombatSetting { DefaultFormation = 5 }   // 0~4 (자동 재보급 · 주유) 는 방향 변경 §0-1 에서 삭제

/// <summary>세력 전투 설정 한 항목 변경 (Value: bool 은 0/1, 형식은 정수값).</summary>
public sealed record SetCombatSettingCommand(int Faction, int Setting, int Value) : GameCommand(Faction);

public sealed partial class World
{
    /// <summary>전투 탭 설정 변경 (즉시 검증 후 명령 기록).</summary>
    public void SetCombatSetting(int faction, CombatSetting setting, int value) => Issue(new SetCombatSettingCommand(faction, (int)setting, value));

    void ExecSetCombatSetting(int f, int setting, int v)
    {
        var c = Factions[f].Combat;
        switch ((CombatSetting)setting)
        {
            case CombatSetting.DefaultFormation:
                if (Enum.IsDefined(typeof(FormType), v)) c.DefaultFormation = (FormType)v;
                break;
        }
    }

    partial void WriteCommandExtImpl(SnapWriter w, SnapCtx ctx)
    {
        for (int i = 1; i < Factions.Count; i++)
        {
            var c = Factions[i].Combat;
            w.I((int)c.DefaultFormation);
        }
    }

    partial void ReadCommandExtImpl(SnapReader r, SnapCtx ctx)
    {
        for (int i = 1; i < Factions.Count; i++)
        {
            var c = Factions[i].Combat;
            int df = r.I();
            if (!Enum.IsDefined(typeof(FormType), df)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "default formation"));
            c.DefaultFormation = (FormType)df;
        }
    }

    void HashCombatSettings(ref Hasher h)
    {
        for (int i = 1; i < Factions.Count; i++)
        {
            var c = Factions[i].Combat;
            h.Add((int)c.DefaultFormation);
        }
    }
}
