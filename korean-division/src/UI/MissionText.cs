using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>미션 목표 · 패배 조건의 표시 문구 (파일에 text 가 있으면 그것, 없으면 종류별 기본 문구). 브리핑 · HUD · 결과 공용.</summary>
public static class MissionText
{
    public static string Clock(float seconds)
    {
        int s = Math.Max(0, (int)seconds);
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }

    public static string Objective(MissionObjective o)
    {
        if (!string.IsNullOrWhiteSpace(o.Text)) return o.Text!;
        if (o.Train != null)   // T30c 화물열차 목표
            return o.Type switch
            {
                "escort" => string.Format(S.ObjTrainEscort, o.Train, Math.Max(1, o.Count), o.Area),
                "capture" => string.Format(S.ObjTrainCapture, o.Train, (int)o.Seconds),
                "eliminate" => o.Percent >= 100 ? string.Format(S.ObjTrainDestroy, o.Train) : string.Format(S.ObjTrainDestroyPct, o.Train, (int)o.Percent),
                _ => o.Id,
            };
        return o.Type switch
        {
            "capture" => string.Format(S.ObjCapture, o.Area, (int)o.Seconds),
            "eliminate" => !string.IsNullOrEmpty(o.Tag) ? string.Format(S.ObjEliminateTag, o.Tag) : string.Format(S.ObjEliminate, (int)o.Percent),
            "defend" => o.Area != null ? string.Format(S.ObjDefendArea, (int)o.Seconds, o.Area) : string.Format(S.ObjDefend, (int)o.Seconds),
            "escort" => string.Format(S.ObjEscort, o.Tag, Math.Max(1, o.Count), o.Area),
            _ => o.Id,
        };
    }

    public static string Failure(MissionFailure f)
    {
        if (!string.IsNullOrWhiteSpace(f.Text)) return f.Text!;
        return f.Type switch
        {
            "losses" => string.Format(S.FailLosses, (int)f.Percent),
            "keyUnit" => string.Format(S.FailKeyUnit, f.Tag),
            "timeLimit" => string.Format(S.FailTimeLimit, Clock(f.Seconds)),
            _ => f.Type,
        };
    }

    /// <summary>진행 상황 짧은 글 (HUD 오른쪽).</summary>
    public static string Progress(World w, MissionState m, int i)
    {
        var o = m.Def.Objectives[i];
        if (m.ObjState[i] == MissionState.Done) return S.ObjStateDone;
        if (m.ObjState[i] == MissionState.Failed) return S.ObjStateFailed;
        return o.Type switch
        {
            "capture" => string.Format(S.MsHudCapture, m.ObjProgress[i], o.Seconds),
            "eliminate" => string.IsNullOrEmpty(o.Tag) && o.Train == null ? string.Format(S.MsHudEliminate, m.ObjProgress[i]) : string.Format(S.MsHudEliminate, m.ObjProgress[i] * 100),   // 태그 · 화물열차 = 0~1 비율
            "defend" => Clock(o.Seconds - w.Time),
            "escort" => string.Format(S.MsHudEscort, (int)m.ObjProgress[i], Math.Max(1, o.Count)),
            _ => "",
        };
    }

    /// <summary>T30c: 공개 · 진행 중인 화물열차 목표의 살아 있는 화물 객차 (표식용, 월드 · 미니맵). 표시 전용.</summary>
    public static IEnumerable<(TrainCar Car, MissionObjective O)> FreightMarkers(World w)
    {
        if (w.Mission is not { } m || w.Trains.Count == 0) yield break;
        for (int i = 0; i < m.Def.Objectives.Count; i++)
        {
            var o = m.Def.Objectives[i];
            if (o.Train == null || !m.ObjRevealed[i] || m.ObjState[i] != MissionState.Active) continue;
            foreach (var t in w.Trains)
                if (t.Tag == o.Train)
                    foreach (var c in t.Cars) if (c.Def.Freight && c.Alive) yield return (c, o);
        }
    }

    /// <summary>시간 제한 (가장 이른 timeLimit 패배 조건, 없으면 0).</summary>
    public static float TimeLimit(MissionDef d)
    {
        float t = 0;
        foreach (var f in d.Failures) if (f.Type == "timeLimit" && f.Seconds > 0 && (t == 0 || f.Seconds < t)) t = f.Seconds;
        return t;
    }

    /// <summary>병력 구성 합계 (종류 → 수, 종류 키 순).</summary>
    public static List<(string Unit, int Count)> Composition(IEnumerable<MissionForce> forces)
    {
        var sum = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in forces)
            foreach (var c in f.AllCompanies())
                foreach (var (k, n) in c.Units) sum[k] = sum.GetValueOrDefault(k) + n;
        return sum.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public static string UnitName(string key) => Defs.Current.Units.TryGetValue(key, out var d) ? d.Name : key;
}
