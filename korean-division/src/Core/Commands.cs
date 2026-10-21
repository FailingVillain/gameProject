using Raylib_cs;
using KoreanDivision.UI;

namespace KoreanDivision.Core;

public enum TargetMode { None, Point, PointOrEntity }

/// <summary>
/// 유닛 커맨드 카드의 고정 명령. (Col, Row) = 3×3 카드 내 위치.
/// </summary>
public sealed record CommandDef(string Key, string Label, string Tooltip, char Hotkey, TargetMode Target, int Col, int Row,
    Func<UnitDef, bool> Available);

public static class Commands
{
    static bool Always(UnitDef _) => true;

    public static readonly CommandDef[] UnitCommands =
    {
        new("move", S.CmdMove, S.TipMove, 'M', TargetMode.Point, 0, 0, Always),
        new("stop", S.CmdStop, S.TipStop, 'S', TargetMode.None, 1, 0, Always),
        new("attack", S.CmdAttack, S.TipAttack, 'A', TargetMode.PointOrEntity, 2, 0, d => d.Weapon != null),
        new("quickstep", S.CmdQuickStep, S.TipQuickStep, 'Q', TargetMode.Point, 0, 1, d => d.IsInfantry && !d.Mounted),   // 속보 (스태미나)
        new("hold", S.CmdHold, S.TipHold, 'H', TargetMode.None, 1, 1, Always),
        new("grenade", S.CmdGrenade, S.TipGrenade, 'X', TargetMode.Point, 2, 1, d => d.Grenades > 0),   // 수류탄 (교전 4차)
        new("groundfire", S.CmdGroundFire, S.TipGroundFire, 'T', TargetMode.Point, 0, 2, d => d.SetupTime > 0 && Defs.Current.Weapon(d.Weapon) is { } gw && (gw.Indirect || gw.Splash > 0)),   // 지점 포격 (GroundFire.cs)
        new("chargeonly", S.CmdChargeOnly, S.TipChargeOnly, 'V', TargetMode.None, 1, 2, d => d.Mounted),   // 기병 돌격만 토글
        new("mount", S.CmdMount, S.TipMount, 'D', TargetMode.None, 0, 2, d => d.Mounted && !d.IsOfficer),   // 기병 하마/승마 토글 (N61)
        new("scoutauto", S.CmdScoutAuto, S.TipScoutAuto, 'E', TargetMode.None, 2, 1, d => d.Scout),   // 정찰병 자율 행동 토글 (Scouts.cs)
        new("charge", S.CmdCharge, S.TipCharge, 'C', TargetMode.PointOrEntity, 2, 2, d => d.Melee != null),   // 착검 돌격 (ROADMAP §2-4b)
    };

    public static CommandDef? Get(string key) => Array.Find(UnitCommands, c => c.Key == key);

    public static KeyboardKey Key(char c) => (KeyboardKey)char.ToUpperInvariant(c);
}
