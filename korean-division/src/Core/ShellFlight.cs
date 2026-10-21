using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 날아가는 포탄 (곡사포 · 직사 범위포): 발사 틱에 착탄점 (퍼짐 · 빗나감 포함) 을 정하고, 착탄 틱에 그 '지점' 에서 Explode.
/// 표적이 그 사이 움직였으면 맞지 않는다. 피해 · 제압 · 포탄 구덩이 · 폭발 기록은 모두 착탄 때.
/// 비행 시간(초) = clamp(flightBase + 거리 / shellSpeed, flightMin, flightMax), shellSpeed 0 = 즉발 (balance.json 무기마다).
/// 틱 수 = round(초 / SimDt), 0 이면 즉시 폭발. 저장 = CMB2 뒤쪽 (WriteCombat3), 체크섬 = HashCombat3.
/// </summary>
public sealed class ShellFlight
{
    public int ShooterId, Owner, TargetId;
    public WeaponDef Weapon = null!;
    public Vector2 From, Pos;
    public long FireTick, ImpactTick;
}

public sealed partial class World
{
    /// <summary>날아가는 포탄. 발사 순서대로 처리 (같은 틱 착탄은 발사 순서). 저장 · 체크섬 포함.</summary>
    public readonly List<ShellFlight> Shells = new();

    /// <summary>비행 틱 수 (0 = 즉발): clamp(flightBase + dist / shellSpeed, flightMin, flightMax) 초.</summary>
    public static int FlightTicks(WeaponDef w, float dist)
    {
        if (w.ShellSpeed <= 0) return 0;
        float s = w.FlightBase + dist / w.ShellSpeed;
        if (w.FlightMax > 0 && s > w.FlightMax) s = w.FlightMax;
        if (s < w.FlightMin) s = w.FlightMin;
        return Math.Max(0, (int)MathF.Round(s / Config.SimDt));
    }

    /// <summary>포 발사: 포구 섬광 · 발사음 기록 (Shots), 비행 시간이 있으면 포탄을 띄우고 아니면 즉시 폭발.</summary>
    void LaunchShell(Entity shooter, Vector2 from, Vector2 impact, int targetId, WeaponDef w)
    {
        Shots.Add((from, impact, shooter.Owner, Tick, true, shooter.Id));
        int ticks = FlightTicks(w, Vector2.Distance(from, impact));
        if (ticks <= 0) { Explode(shooter.Id, shooter.Owner, shooter, from, impact, targetId, w); return; }
        Shells.Add(new ShellFlight { ShooterId = shooter.Id, Owner = shooter.Owner, TargetId = targetId, Weapon = w, From = from, Pos = impact, FireTick = Tick, ImpactTick = Tick + ticks });
    }

    /// <summary>착탄 틱이 된 포탄 폭발 (UpdateBlasts 옆, 유닛 이동 뒤). 사격자가 죽었어도 폭발한다 (피해 출처 없음 → 반격 · 사살 수 없음).</summary>
    void UpdateShells()
    {
        if (Shells.Count == 0) return;
        for (int i = 0; i < Shells.Count;)
        {
            var s = Shells[i];
            if (s.ImpactTick > Tick) { i++; continue; }
            Shells.RemoveAt(i);
            var shooter = Find(s.ShooterId);
            if (shooter != null && !shooter.Alive) shooter = null;
            Explode(s.ShooterId, s.Owner, shooter, s.From, s.Pos, s.TargetId, s.Weapon);
        }
    }

    /// <summary>pos 반경 radius 안에 faction 이 쏜, 아직 날아가는 포탄이 있는가 (AI · 표시용).</summary>
    public bool ShellsInFlightNear(int faction, Vector2 pos, float radius)
    {
        float r2 = radius * radius;
        foreach (var s in Shells) if (s.Owner == faction && Vector2.DistanceSquared(s.Pos, pos) <= r2) return true;
        return false;
    }

    void WriteShells(SnapWriter w)
    {
        w.I(Shells.Count);
        foreach (var s in Shells)
        {
            w.I(s.ShooterId); w.I(s.Owner); w.I(s.TargetId); w.Str(s.Weapon.Key);
            w.F(s.From.X); w.F(s.From.Y); w.F(s.Pos.X); w.F(s.Pos.Y); w.L(s.FireTick); w.L(s.ImpactTick);
        }
    }

    void ReadShells(SnapReader r)
    {
        Shells.Clear();
        int n = r.Count(16);
        for (int i = 0; i < n; i++)
        {
            var s = new ShellFlight { ShooterId = r.I(), Owner = r.I(), TargetId = r.I() };
            string? key = r.Str();
            s.Weapon = Defs.Weapon(key) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "shell weapon"));
            s.From = new Vector2(r.F(), r.F()); s.Pos = new Vector2(r.F(), r.F());
            s.FireTick = r.L(); s.ImpactTick = r.L();
            if (s.ImpactTick < s.FireTick) throw new SnapshotException(string.Format(SnapMsg.Invalid, "shell tick"));
            Shells.Add(s);
        }
    }

    void HashShells(ref Hasher h)
    {
        h.Add(Shells.Count);
        foreach (var s in Shells)
        {
            h.Add(s.ShooterId); h.Add(s.Owner); h.Add(s.TargetId); h.Add(s.Weapon.Key);
            h.Add(s.From.X); h.Add(s.From.Y); h.Add(s.Pos.X); h.Add(s.Pos.Y); h.Add(s.FireTick); h.Add(s.ImpactTick);
        }
    }
}
