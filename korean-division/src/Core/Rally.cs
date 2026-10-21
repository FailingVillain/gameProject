using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 집결 지점 (N79a, 사용자 승인 2026-10-20): 중대마다 플레이어 (또는 전술 AI) 가 정한 후퇴 · 집결 위치.
/// 후퇴 명령 (RetreatCommand.UseRally) 은 실행 시점에 집결 지점이 있는 중대의 병사를 그 지점으로 보낸다 (없으면 명령의 목표 그대로).
/// 상태 = Company.HasRally · RallyPoint (ORG_ 끝, FormatVersion 43), 명령 SetRallyCommand (리플레이 기록, v16). 틱 · 입력의 함수 — 결정적.
/// </summary>
public sealed record SetRallyCommand(int Faction, int[] Companies, Vector2 Point, bool Clear) : GameCommand(Faction);

public sealed partial class World
{
    /// <summary>집결 지점 지정 (Clear = 해제). 자기 중대만.</summary>
    public void SetRally(int faction, IEnumerable<int> companies, Vector2 point, bool clear = false)
    {
        var ids = companies.Distinct().OrderBy(i => i).ToArray();
        if (ids.Length > 0) Issue(new SetRallyCommand(faction, ids, point, clear));
    }

    void ExecSetRally(int f, int[] companies, Vector2 point, bool clear)
    {
        bool ok = clear || (float.IsFinite(point.X) && float.IsFinite(point.Y) && Map.InBounds((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y)));
        if (!ok) return;
        foreach (int id in companies)
        {
            if (OwnCompany(f, id) is not { } c) continue;
            c.HasRally = !clear;
            c.RallyPoint = clear ? Vector2.Zero : point;
        }
    }

    /// <summary>[UI] 집결 지점이 있는 중대의 병사만 골라 그 지점으로 후퇴시킨다 (돌려주는 값 = 후퇴 명령을 받은 중대 수).</summary>
    public int RetreatToRally(int faction, IEnumerable<Unit> units)
    {
        var list = units.Where(u => u.Alive && u.Owner == faction && u.Company is { HasRally: true }).ToList();
        if (list.Count == 0) return 0;
        IssueUnits(faction, list, ids => new RetreatCommand(faction, ids, Vector2.Zero, null, true));
        return list.Select(u => u.Company!.Id).Distinct().Count();
    }

    void ExecRetreat(int f, RetreatCommand c)
    {
        var list = ResolveOrder(f, c.Units);
        if (list.Count == 0) return;
        if (!c.UseRally || !list.Exists(u => u.Company is { HasRally: true } co && co.Owner == f))
        {
            ExecGroupOrder(f, list, c.Target, UnitOrder.Move, c.Form, quick: true, retreat: true);
            return;
        }
        foreach (var grp in list.GroupBy(u => u.Company is { HasRally: true } co && co.Owner == f ? co.Id : 0).OrderBy(g => g.Key))
        {
            if (grp.Key == 0 && c.Target == Vector2.Zero) continue;   // 플레이어 '집결 지점으로 후퇴': 지점 없는 중대는 건드리지 않음
            Vector2 target = grp.Key == 0 ? c.Target : FindCompany(grp.Key)!.RallyPoint;
            ExecGroupOrder(f, grp.ToList(), target, UnitOrder.Move, c.Form, quick: true, retreat: true);
        }
    }

    // 저장 · 체크섬 (ORG_ 끝 WriteProps 뒤, FormatVersion 43)
    void WriteRally(SnapWriter w)
    {
        foreach (var c in Companies) { w.Bool(c.HasRally); w.F(c.RallyPoint.X); w.F(c.RallyPoint.Y); }
        foreach (var u in Units) { w.Byte((byte)u.GroundKind); w.I(u.GroundRounds); w.F(u.GroundDir.X); w.F(u.GroundDir.Y); w.L(u.GroundStart); }
    }

    void ReadRally(SnapReader r)
    {
        foreach (var c in Companies)
        {
            c.HasRally = r.Bool();
            c.RallyPoint = new Vector2(r.F(), r.F());
            if (!float.IsFinite(c.RallyPoint.X) || !float.IsFinite(c.RallyPoint.Y)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "rally point"));
        }
        foreach (var u in Units)
        {
            byte k = r.Byte();
            if (!Enum.IsDefined(typeof(GroundFireKind), k)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "ground fire kind"));
            u.GroundKind = (GroundFireKind)k;
            u.GroundRounds = r.I();
            u.GroundDir = new Vector2(r.F(), r.F());
            if (!float.IsFinite(u.GroundDir.X) || !float.IsFinite(u.GroundDir.Y) || u.GroundRounds < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "ground fire state"));
            u.GroundStart = r.L();
        }
    }

    void HashRally(ref Hasher h)
    {
        foreach (var c in Companies) { h.Add(c.HasRally ? 1 : 0); h.Add(c.RallyPoint.X); h.Add(c.RallyPoint.Y); }
        foreach (var u in Units) { h.Add((int)u.GroundKind); h.Add(u.GroundRounds); h.Add(u.GroundDir.X); h.Add(u.GroundDir.Y); h.Add(u.GroundStart); }
    }
}
