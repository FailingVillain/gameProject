namespace KoreanDivision.Core;

/// <summary>
/// N73 사격 통제 (2026-10-20 사용자 승인 — 매복 · 기습). 유닛마다 영구 모드 (명령 SetFireModeCommand — 선택한 유닛 · 분대 · 중대 구성원 전부, 리플레이 기록):
///  · Free 자유 사격 (기본) — 지금까지와 같음.
///  · Hold 사격 금지 — 어떤 경우에도 쏘지 않는다 (자동 · 공격 명령 · 행진 사격 · 후퇴 사격 · 자동 수류탄 · 돌격 자동 개시). 표적을 놓으므로 F1 접적 상태를 만들지 않는다 (사격 · 표적이 접적 트리거).
///  · ReturnFire 응사만 — 자기 · 같은 분대원이 fireControlReturnSec 안에 총격을 받았거나 (LastFiredAtTick), 표적이 fireControlCloseRange 칸 안이면 쏜다. 아니면 Hold 와 같다.
/// 분대 따라 쏘기 (N46) · 자동 표적 · 반격 (Retaliate) 은 모두 FireAllowed 를 거친다. 상태 = Unit.FireMode (ORG_ 끝, FormatVersion 38). 틱 · 입력의 함수 — 결정적.
/// </summary>
/// arty3 (사용자 2026-10-21 '중기관총 최후 저지선'): FinalLine 최후 저지선 — 최후 저지선 무기 (weapon.finalLine, 중기관총) 만. 사격선 (유닛 → FinalLinePoint 방향, 사거리까지) 에서
/// combat.finalLineBand 칸 안의 적만 쏜다 (그 밖은 사격 금지와 같음). 지정 = SetFinalLineCommand (지점 하나), 상태 = Unit.FinalLinePoint (FormatVersion 49).
public enum FireMode : byte { Free = 0, Hold = 1, ReturnFire = 2, FinalLine = 3 }

/// <summary>arty3 최후 저지선 지정: 최후 저지선 무기를 가진 유닛만 FireMode.FinalLine + 사격선 방향 지점 (그 밖 유닛은 그대로). 리플레이 기록.</summary>
public sealed record SetFinalLineCommand(int Faction, int[] Units, System.Numerics.Vector2 Point) : GameCommand(Faction);

/// <summary>사격 통제 지정 (유닛 Id 목록 — UI 가 분대 · 중대를 구성원으로 풀어서 냄). 리플레이 기록.</summary>
public sealed record SetFireModeCommand(int Faction, int[] Units, FireMode Mode) : GameCommand(Faction);

public sealed partial class Unit
{
    /// <summary>N73: 사격 통제 모드 (저장 · 체크섬).</summary>
    [SnapExt] public FireMode FireMode { get; internal set; }
}

public sealed partial class World
{
    public void SetFireMode(int faction, IEnumerable<Unit> units, FireMode mode) =>
        IssueUnits(faction, units, ids => new SetFireModeCommand(faction, ids, mode));

    /// <summary>arty3: 최후 저지선 지정 (최후 저지선 무기 유닛만).</summary>
    public void SetFinalLine(int faction, IEnumerable<Unit> units, System.Numerics.Vector2 point) =>
        IssueUnits(faction, units.Where(CanFinalLine), ids => new SetFinalLineCommand(faction, ids, point));

    /// <summary>[UI · 명령] 최후 저지선을 쓸 수 있는 유닛 (weapon.finalLine — 중기관총).</summary>
    public static bool CanFinalLine(Unit u) => u.Weapon is { FinalLine: true };

    void ExecSetFinalLine(int f, int[] ids, System.Numerics.Vector2 p)
    {
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Y)) return;
        foreach (var u in Resolve(f, ids))
        {
            if (!CanFinalLine(u)) continue;
            u.FireMode = FireMode.FinalLine; u.FinalLinePoint = p;
            if (u.Target != null && !OnFinalLine(u, u.Target.Pos)) u.Target = null;
        }
    }

    void ExecSetFireMode(int f, int[] ids, FireMode mode)
    {
        if (!Enum.IsDefined(typeof(FireMode), mode) || mode == FireMode.FinalLine) return;   // 최후 저지선은 지점이 필요 (SetFinalLineCommand)
        foreach (var u in Resolve(f, ids)) u.FireMode = mode;
    }

    /// <summary>[읽기] 자기 · 같은 분대원이 fireControlReturnSec 안에 총격을 받았다 (구성원 목록 순 — 결정적).</summary>
    internal bool RecentlyFiredUpon(Unit u)
    {
        long win = (long)MathF.Round(Defs.CombatRules.FireControlReturnSec / Config.SimDt);
        if (u.LastFiredAtTick > 0 && Tick - u.LastFiredAtTick <= win) return true;
        if (u.Squad is { } sq)
            foreach (var m in sq.Members) if (m.Alive && m.LastFiredAtTick > 0 && Tick - m.LastFiredAtTick <= win) return true;
        return false;
    }

    /// <summary>[읽기] 이 유닛이 이 표적 (null = 표적 무관) 을 쏴도 되는가 — 사격 통제.</summary>
    internal bool FireAllowed(Unit u, Entity? t)
    {
        if (t != null && InNeutralZone(t.Pos)) return false;   // TC-G 중립 구역 안 표적 사격 금지 (NeutralZone.cs)
        switch (u.FireMode)
        {
            case FireMode.Hold: return false;
            case FireMode.FinalLine: return t != null && OnFinalLine(u, t.Pos);   // arty3 (Arty3.cs)
            case FireMode.ReturnFire:
                if (RecentlyFiredUpon(u)) return true;
                return t != null && t.EdgeDistance(u.Pos) - u.Radius <= Defs.CombatRules.FireControlCloseRange;
            default: return true;
        }
    }

    /// <summary>[읽기] 표적 없이 (행진 사격 · 후퇴 사격 탐색) 쏠 가능성이 있는가.</summary>
    internal bool FireMayHappen(Unit u) => u.FireMode == FireMode.Free || (u.FireMode == FireMode.ReturnFire && RecentlyFiredUpon(u));

    // ---------------------------------------------------------------- 저장 · 체크섬 (ORG_ 끝, WriteContact 뒤 — FormatVersion 38; 49 arty3: 뒤에 최후 저지선 · 조정 사격 유닛 목록)

    void WriteFireControl(SnapWriter w)
    {
        foreach (var u in Units) w.Byte((byte)u.FireMode);
        int n = 0;
        foreach (var u in Units) if (Arty3Saved(u)) n++;
        w.I(n);
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            if (!Arty3Saved(u)) continue;
            w.I(i); w.Vec(u.FinalLinePoint); w.I(u.AdjustShots); w.Vec(u.AdjustAim);
        }
    }

    static bool Arty3Saved(Unit u) => u.AdjustShots != 0 || u.FinalLinePoint != default || u.AdjustAim != default;

    void ReadFireControl(SnapReader r)
    {
        foreach (var u in Units)
        {
            byte b = r.Byte();
            if (!Enum.IsDefined(typeof(FireMode), b)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "fire mode"));
            u.FireMode = (FireMode)b;
        }
        int n = r.Count(4);
        for (int k = 0; k < n; k++)
        {
            int i = r.I();
            if (i < 0 || i >= Units.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "arty3 unit"));
            var u = Units[i];
            u.FinalLinePoint = r.Vec(); u.AdjustShots = r.I(); u.AdjustAim = r.Vec();
            if (u.AdjustShots < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "adjust shots"));
        }
    }

    void HashFireControl(ref Hasher h)
    {
        foreach (var u in Units)
        {
            h.Add((int)u.FireMode);
            if (Arty3Saved(u)) { h.Add(u.Id); h.Add(u.FinalLinePoint.X); h.Add(u.FinalLinePoint.Y); h.Add(u.AdjustShots); h.Add(u.AdjustAim.X); h.Add(u.AdjustAim.Y); }   // arty3
        }
    }
}
