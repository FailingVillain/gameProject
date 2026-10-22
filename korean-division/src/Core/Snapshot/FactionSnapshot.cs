namespace KoreanDivision.Core;

public sealed partial class Faction
{
    internal void WriteState(SnapWriter w)
    {
        w.Str(Name); w.Str(Lang);
        w.Bool(IsAi); w.Bool(RevealAll); w.Bool(Defeated); w.Bool(Revealed);
        w.F(NoHqTime);
        w.I(Team);
        w.I(Morale);   // M1 세력 사기 (FV 67, Morale.cs)
    }

    internal void ReadState(SnapReader r)
    {
        Name = r.Str() ?? ""; Lang = r.Str() ?? "en";
        IsAi = r.Bool(); RevealAll = r.Bool(); Defeated = r.Bool(); Revealed = r.Bool();
        NoHqTime = r.F();
        Team = r.I();
        Morale = r.I();
        if (Morale < MoraleMin || Morale > MoraleMax) throw new SnapshotException(string.Format(SnapMsg.Invalid, "faction morale"));
    }
}
