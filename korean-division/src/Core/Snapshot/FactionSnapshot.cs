namespace KoreanDivision.Core;

public sealed partial class Faction
{
    internal void WriteState(SnapWriter w)
    {
        w.Str(Name); w.Str(Lang);
        w.Bool(IsAi); w.Bool(RevealAll); w.Bool(Defeated); w.Bool(Revealed);
        w.F(NoHqTime);
        w.I(Team);
    }

    internal void ReadState(SnapReader r)
    {
        Name = r.Str() ?? ""; Lang = r.Str() ?? "en";
        IsAi = r.Bool(); RevealAll = r.Bool(); Defeated = r.Bool(); Revealed = r.Bool();
        NoHqTime = r.F();
        Team = r.I();
    }
}
