using System.Numerics;

namespace KoreanDivision.Core;

// T30c 화물열차 = 미션 목표. 목표에 "train": <열차 tag> 를 주면 대상이 그 열차들의 화물 객차 (TrainCarDef.Freight) 가 된다.
//  escort    = 살아 있는 화물 객차 count 량이 area 안 (기존 escort 의 count/area). 화물 객차가 count 보다 적게 남거나, 남은 열차가 모두 멈춤(기관차 파괴) → 실패.
//  capture   = 살아 있는 화물 객차 radius 칸 안에 side 의 점령 가능 유닛만 있는 시간이 seconds 초 (기존 capture 규칙). 소유 변경 없음 (Q-C1). 화물 객차 모두 파괴 → 실패.
//  eliminate = 화물 객차 percent% 파괴 (기본 100 = 모두).
// 새 저장 상태 없음: 진행 = MissionState.ObjProgress (저장 · 체크섬 이미 포함), 객차 HP = 열차 구역. 화물 보상 연결 없음 (Q-C3).

public sealed partial class MissionDef
{
    /// <summary>trains 목록 자체 (null · null 항목). 열차 경로 · 객차 종류의 지도 의존 검증은 World.SpawnTrain.</summary>
    void ValidateTrains()
    {
        Require(Trains != null, "trains 는 열차 목록이어야 함 (null)");
        for (int i = 0; i < Trains!.Count; i++) Require(Trains[i] != null, $"trains[{i}] 가 비어 있음 (null)");
    }

    void ValidateFreightObjective(MissionObjective o, Defs defs)
    {
        string id = o.Id;
        Require(!string.IsNullOrWhiteSpace(o.Train), $"목표 '{id}': train 이 비었음 (열차 tag)");
        Require(o.Type is "escort" or "capture" or "eliminate", $"목표 '{id}': train 목표 종류는 escort · capture · eliminate (받은 '{o.Type}')");
        Require(string.IsNullOrEmpty(o.Tag), $"목표 '{id}': train 과 tag 는 함께 쓸 수 없음");
        Require(o.Faction >= 0 && o.Faction <= Factions.Count, $"목표 '{id}': faction {o.Faction} 이 범위 밖 (0~{Factions.Count})");
        int trains = 0, freight = 0;
        foreach (var t in Trains)
        {
            if (t.Tag != o.Train) continue;
            trains++;
            Require(t.Cars != null, $"목표 '{id}': 열차 '{o.Train}' 의 cars 가 null");
            foreach (var k in t.Cars!)
                if (k != null && defs.Trains.Cars.TryGetValue(k, out var cd) && cd.Freight) freight++;
        }
        Require(trains > 0, $"목표 '{id}': 열차 tag '{o.Train}' 없음 (trains[].tag)");
        Require(freight > 0, $"목표 '{id}': 열차 '{o.Train}' 에 화물 객차 없음 (balance.json trains.cars 의 freight)");
        switch (o.Type)
        {
            case "escort":
                Require(Area(o.Area) != null, $"목표 '{id}': 구역 '{o.Area}' 없음");
                Require(o.Count >= 1 && o.Count <= freight, $"목표 '{id}': count {o.Count} (1~화물 객차 {freight}량)");
                break;
            case "capture":
                Require(o.Area == null, $"목표 '{id}': capture + train 은 area 없이 (화물 객차 둘레 radius)");
                Require(float.IsFinite(o.Seconds) && o.Seconds >= 0 && o.Seconds <= 36000, $"목표 '{id}': seconds {o.Seconds} (0~36000)");
                Require(float.IsFinite(o.Radius) && o.Radius >= 0.5f && o.Radius <= 32, $"목표 '{id}': radius {o.Radius} (0.5~32 칸)");
                break;
            case "eliminate":
                Require(float.IsFinite(o.Percent) && o.Percent > 0 && o.Percent <= 100, $"목표 '{id}': percent {o.Percent} (0 초과 ~ 100)");
                break;
        }
    }
}

public sealed partial class World
{
    /// <summary>열차 tag 의 화물 객차 수 (살아 있는 · 전체).</summary>
    public (int Alive, int Total) FreightCount(string? tag)
    {
        int alive = 0, total = 0;
        foreach (var t in Trains)
        {
            if (t.Tag != tag) continue;
            foreach (var c in t.Cars) if (c.Def.Freight) { total++; if (c.Alive) alive++; }
        }
        return (alive, total);
    }

    /// <summary>살아 있는 화물 객차 중심 (viewer > 0 이면 그 세력이 보는 것만). 없으면 null.</summary>
    internal Vector2? FreightCentroid(string? tag, int viewer)
    {
        Vector2 sum = Vector2.Zero; int n = 0;
        foreach (var t in Trains)
        {
            if (t.Tag != tag) continue;
            foreach (var c in t.Cars)
                if (c.Def.Freight && c.Alive && (viewer <= 0 || Fog.CanSee(viewer, c))) { sum += c.Pos; n++; }
        }
        return n > 0 ? sum / n : null;
    }

    /// <summary>UpdateMission → EvalObjective 에서 (o.Train != null). 진행 · 상태는 ObjProgress · ObjState 에만.</summary>
    void EvalFreightObjective(MissionState m, int i, MissionObjective o, int side, float dt)
    {
        switch (o.Type)
        {
            case "escort":
            {
                var a = m.Def.Area(o.Area)!;
                int alive = 0, inside = 0;
                bool canMove = false;   // 구역 밖 화물 객차를 끌고 갈 수 있는 열차가 남았나
                foreach (var t in Trains)
                {
                    if (t.Tag != o.Train) continue;
                    bool outside = false;
                    foreach (var c in t.Cars)
                    {
                        if (!c.Def.Freight || !c.Alive) continue;
                        alive++;
                        if (a.Contains(c.Pos)) inside++; else outside = true;
                    }
                    if (outside && !t.Stalled) canMove = true;
                }
                m.ObjProgress[i] = inside;
                int need = Math.Max(1, o.Count);
                if (inside >= need) m.ObjState[i] = MissionState.Done;
                else if (alive < need || !canMove) m.ObjState[i] = MissionState.Failed;
                break;
            }
            case "capture":
            {
                if (FreightCount(o.Train).Alive == 0) { m.ObjState[i] = MissionState.Failed; break; }
                float r2 = o.Radius * o.Radius;
                Vector2 lo = new(float.MaxValue), hi = new(float.MinValue);   // 살아 있는 화물 객차 상자 ± 반경 (유닛 수천 명 앞 거르기)
                foreach (var t in Trains)
                    if (t.Tag == o.Train)
                        foreach (var c in t.Cars) if (c.Def.Freight && c.Alive) { lo = Vector2.Min(lo, c.Pos); hi = Vector2.Max(hi, c.Pos); }
                lo -= new Vector2(o.Radius); hi += new Vector2(o.Radius);
                int own = 0, enemy = 0;
                foreach (var u in Units)
                {
                    if (u.Hp <= 0 || u.Pos.X < lo.X || u.Pos.X > hi.X || u.Pos.Y < lo.Y || u.Pos.Y > hi.Y || !NearFreight(o.Train, u.Pos, r2)) continue;
                    if (Allied(side, u.Owner)) { if (u.Def.CanCapture) own++; }
                    else if (IsHostile(side, u.Owner)) enemy++;
                }
                if (own > 0 && enemy == 0) m.ObjProgress[i] += dt;
                else if (enemy > 0 && own == 0) m.ObjProgress[i] = MathF.Max(0, m.ObjProgress[i] - dt);
                if (m.ObjProgress[i] >= o.Seconds && own > 0 && enemy == 0) m.ObjState[i] = MissionState.Done;
                break;
            }
            case "eliminate":
            {
                var (alive, total) = FreightCount(o.Train);
                int dead = total - alive;
                m.ObjProgress[i] = total == 0 ? 0 : dead / (float)total;
                if (total > 0 && (o.Percent >= 100 ? alive == 0 : dead * 100f / total >= o.Percent)) m.ObjState[i] = MissionState.Done;
                break;
            }
        }
    }

    bool NearFreight(string? tag, Vector2 p, float r2)
    {
        foreach (var t in Trains)
        {
            if (t.Tag != tag) continue;
            foreach (var c in t.Cars)
                if (c.Def.Freight && c.Alive && Vector2.DistanceSquared(p, c.Pos) <= r2) return true;
        }
        return false;
    }
}
