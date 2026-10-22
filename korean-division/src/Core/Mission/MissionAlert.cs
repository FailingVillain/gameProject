using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// TC-B SK2 경보 상태 (잠입). 미션 사건의 세 가지 확장 — 모두 기본 꺼짐 (JSON 에 쓴 미션에서만 동작, 기존 미션 불변):
///  · 감지 경보: 사건 조건 seenBy (+ seenTag) = 그 세력이 안개 시야 · 은폐 탐지 (FogOfWar.CanSee) 로 대상 유닛을 식별함. 새 상태 없음 (EventFired 한 번만).
///  · 지연 증원: spawn 행동의 delay 초 → MissionState.ReinforcementDue 에 도착 틱을 적어 두고 UpdateMission 이 그 틱에 등장시킨다.
///  · 순찰: MissionForce.Patrol 경유점을 돌며 이동 (MissionState.PatrolLeg). 부대원이 경계 상태 (World.IsAlerted) 면 멈추고 자기 AI 에 맡긴다.
/// 새 상태 = ReinforcementDue · PatrolLeg (MISN 구역 끝, 체크섬은 값이 있을 때만 — FormatVersion 67).
/// </summary>
public sealed partial class World
{
    /// <summary>순찰 점검 간격 (틱).</summary>
    const int PatrolInterval = 30;

    /// <summary>seenBy 조건: 그 세력이 대상 유닛 중 하나라도 지금 식별하는가. 안개 꺼짐 = 거짓.</summary>
    bool SeenByFaction(MissionState m, MissionCondition c)
    {
        int f = c.SeenBy;
        if (f <= 0 || Fog.SeesAll(f)) return false;
        if (c.SeenTag != null)
        {
            foreach (int id in m.TagUnits(c.SeenTag))
                if (Find(id) is Unit { Alive: true } u && u.Owner != f && Fog.CanSee(f, u)) return true;
            return false;
        }
        foreach (var u in Units)
            if (u.Alive && u.Owner == m.Player && u.Owner != f && Fog.CanSee(f, u)) return true;
        return false;
    }

    /// <summary>spawn 행동: delay > 0 이면 예약만 (이미 등장했거나 예약됐으면 무시).</summary>
    void ScheduleReinforcement(MissionState m, int i, float delay)
    {
        if (m.ReinforcementSpawned[i] || m.ReinforcementDue[i] > 0) return;
        m.ReinforcementDue[i] = Tick + Math.Max(1, (long)MathF.Ceiling(delay / Config.SimDt));
    }

    /// <summary>예약 시각이 된 증원을 등장시킨다.</summary>
    void UpdateDueReinforcements(MissionState m)
    {
        for (int i = 0; i < m.ReinforcementDue.Length; i++)
            if (!m.ReinforcementSpawned[i] && m.ReinforcementDue[i] > 0 && Tick + 1 >= m.ReinforcementDue[i]) SpawnReinforcement(i);
    }

    /// <summary>순찰 병력: 살아 있는 부대원이 모두 한가하고 경계 상태가 아니면 다음 경유점으로 이동 명령.</summary>
    void UpdatePatrols(MissionState m)
    {
        var forces = m.Def.Forces;
        for (int i = 0; i < forces.Count; i++)
        {
            var f = forces[i];
            if (f.Patrol is not { Length: >= 2 } pts || (Tick + i) % PatrolInterval != 0) continue;
            if (!m.ForceUnits.TryGetValue(f.Id, out var ids)) continue;
            var alive = new List<int>(ids.Count);
            bool free = true;
            foreach (int id in ids)
            {
                if (Find(id) is not Unit { Alive: true } u || u.Surrendered) continue;
                if (u.Order is UnitOrder.Move or UnitOrder.AttackMove or UnitOrder.Attack || IsAlerted(u)) { free = false; break; }
                alive.Add(id);
            }
            if (!free || alive.Count == 0) continue;
            int leg = m.PatrolLeg[i] % pts.Length;
            var p = pts[leg];
            m.PatrolLeg[i] = (leg + 1) % pts.Length;
            bool e0 = queueExec, c0 = queueNoChain;
            queueExec = true; queueNoChain = true;   // 대기열 다리와 같은 방식: 지휘 계통 지연 없이 직접 실행
            try { Execute(new MoveCommand(f.Faction, alive.ToArray(), WalkablePos(new Vector2(p[0], p[1])))); }
            finally { queueExec = e0; queueNoChain = c0; }
        }
    }

    /// <summary>체크섬: 예약 · 순찰 상태 (값이 있는 것만 — 쓰지 않는 미션은 기존 체크섬 그대로).</summary>
    static void HashAlertState(MissionState m, ref Hasher h)
    {
        for (int i = 0; i < m.ReinforcementDue.Length; i++) if (m.ReinforcementDue[i] != 0) { h.Add(i); h.Add(m.ReinforcementDue[i]); }
        for (int i = 0; i < m.PatrolLeg.Length; i++) if (m.PatrolLeg[i] != 0) { h.Add(100000 + i); h.Add(m.PatrolLeg[i]); }
    }
}
