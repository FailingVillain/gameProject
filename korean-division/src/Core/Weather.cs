using System.Text.Json.Serialization;

namespace KoreanDivision.Core;

/// <summary>날씨 (바이트 값 고정 — 저장 · 체크섬에 들어간다). 끝에만 추가.</summary>
public enum WeatherKind : byte { Clear, Overcast, Fog, Rain, Snow }

/// <summary>시간대 (바이트 값 고정). 시간 진행 순서 = 값 순서 (밤 다음은 새벽).</summary>
public enum TimeOfDay : byte { Dawn, Day, Dusk, Night }

/// <summary>일정 한 줄: Tick 에 날씨 · 시간대를 바꾼다 (-1 = 그대로).</summary>
public readonly record struct WeatherEvent(long Tick, sbyte Kind, sbyte Time);

/// <summary>
/// 날씨 · 시간대 상태 (시뮬레이션 상태 — 저장 WTHR · 체크섬 포함). 바뀌는 것은 World.Step 의 일정 틱에서만 (StepWeather).
/// 효과 수치는 balance.json "weather" (WeatherDef): 시야 · 명중 · 이동 배수, 시간대별 쌍안경 효과. 제압은 영향 없음.
/// </summary>
public sealed class WeatherState
{
    public WeatherKind Kind;
    public TimeOfDay Time = TimeOfDay.Day;
    /// <summary>틱 0 의 날씨 · 시간대 (리플레이 머리말에 기록).</summary>
    public WeatherKind StartKind;
    public TimeOfDay StartTime = TimeOfDay.Day;
    /// <summary>예약된 변화 (틱 오름차순) · 다음에 볼 위치.</summary>
    public readonly List<WeatherEvent> Schedule = new();
    public int Next;
    /// <summary>0 이 아니면 이 틱마다 시간대가 한 단계 진행 (새벽 → 낮 → 해질녘 → 밤 → 새벽).</summary>
    public int TimeStepTicks;
    /// <summary>날씨 · 시간대가 바뀐 횟수 (화면 · 소리가 다시 볼지 판단).</summary>
    public int Changes;

    public static WeatherKind ParseKind(string s) => s.Trim().ToLowerInvariant() switch
    {
        "clear" or "맑음" => WeatherKind.Clear,
        "overcast" or "cloudy" or "흐림" => WeatherKind.Overcast,
        "fog" or "안개" => WeatherKind.Fog,
        "rain" or "비" => WeatherKind.Rain,
        "snow" or "눈" => WeatherKind.Snow,
        _ => throw new InvalidDataException($"알 수 없는 날씨 '{s}' (clear, overcast, fog, rain, snow)"),
    };

    public static TimeOfDay ParseTime(string s) => s.Trim().ToLowerInvariant() switch
    {
        "dawn" or "새벽" => TimeOfDay.Dawn,
        "day" or "낮" => TimeOfDay.Day,
        "dusk" or "해질녘" => TimeOfDay.Dusk,
        "night" or "밤" => TimeOfDay.Night,
        _ => throw new InvalidDataException($"알 수 없는 시간대 '{s}' (dawn, day, dusk, night)"),
    };

    public static string KeyOf(WeatherKind k) => k switch
    {
        WeatherKind.Overcast => "overcast", WeatherKind.Fog => "fog", WeatherKind.Rain => "rain", WeatherKind.Snow => "snow", _ => "clear",
    };
    public static string KeyOf(TimeOfDay t) => t switch
    {
        TimeOfDay.Dawn => "dawn", TimeOfDay.Dusk => "dusk", TimeOfDay.Night => "night", _ => "day",
    };

    internal void Write(SnapWriter w)
    {
        w.Byte((byte)Kind); w.Byte((byte)Time); w.Byte((byte)StartKind); w.Byte((byte)StartTime);
        w.I(TimeStepTicks); w.I(Next); w.I(Changes);
        w.I(Schedule.Count);
        foreach (var e in Schedule) { w.L(e.Tick); w.I(e.Kind); w.I(e.Time); }
    }

    internal void Read(SnapReader r)
    {
        Kind = K(r.Byte()); Time = T(r.Byte()); StartKind = K(r.Byte()); StartTime = T(r.Byte());
        TimeStepTicks = r.I(); Next = r.I(); Changes = r.I();
        int n = r.Count(3);
        Schedule.Clear();
        for (int i = 0; i < n; i++)
        {
            long t = r.L(); int k = r.I(), tm = r.I();
            if (k < -1 || k > (int)WeatherKind.Snow || tm < -1 || tm > (int)TimeOfDay.Night) throw new SnapshotException(string.Format(SnapMsg.Invalid, "weather event"));
            Schedule.Add(new WeatherEvent(t, (sbyte)k, (sbyte)tm));
        }
        if (Next < 0 || Next > n || TimeStepTicks < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "weather"));
        static WeatherKind K(byte b) => b <= (byte)WeatherKind.Snow ? (WeatherKind)b : throw new SnapshotException(string.Format(SnapMsg.Invalid, "weather kind"));
        static TimeOfDay T(byte b) => b <= (byte)TimeOfDay.Night ? (TimeOfDay)b : throw new SnapshotException(string.Format(SnapMsg.Invalid, "time of day"));
    }
}

/// <summary>날씨 · 시간대 하나의 효과 (balance.json weather.states / weather.times 한 줄).</summary>
public sealed class WeatherEffectDef
{
    /// <summary>시야 반경 ×.</summary>
    public float Sight { get; set; } = 1;
    /// <summary>명중률 ×.</summary>
    public float Hit { get; set; } = 1;
    /// <summary>이동 속도 ×.</summary>
    public float Move { get; set; } = 1;
    /// <summary>(날씨만) 은폐 보정 %p (extendedEffects 가 켜졌을 때만, 은폐 > 0 인 유닛에 더함). 기본 0.</summary>
    public float Conceal { get; set; }
    /// <summary>(시간대만) 장교 쌍안경 보너스가 남는 비율 (1 = 전부, 0 = 없음).</summary>
    public float Binocular { get; set; } = 1;
}

/// <summary>날씨 규칙 (balance.json "weather"). 최종 배수 = 날씨 × 시간대. 없는 항목은 1.</summary>
public sealed class WeatherDef
{
    public Dictionary<string, WeatherEffectDef> States { get; set; } = new();
    public Dictionary<string, WeatherEffectDef> Times { get; set; } = new();
    /// <summary>날씨 · 시간대로 줄어든 시야의 하한 (칸). 원래 시야가 이보다 작으면 원래 값.</summary>
    public int MinSight { get; set; } = 8;
    /// <summary>날씨 × 시간대 시야 배수의 하한 (안개 + 밤이 곱해져 너무 작아지지 않게).</summary>
    public float MinSightMul { get; set; } = 0.3f;
    /// <summary>확장 효과 스위치 (기본 꺼짐): 켜면 states.&lt;종류&gt;.conceal 은폐 보정이 적용된다. 꺼짐 = 기존 동작 그대로.</summary>
    public bool ExtendedEffects { get; set; }

    /// <summary>날씨 종류의 은폐 보정 %p (스위치가 꺼져 있으면 0).</summary>
    public float ConcealAdd(WeatherKind k) =>
        ExtendedEffects && States.TryGetValue(WeatherState.KeyOf(k), out var e) ? e.Conceal : 0f;

    // 열거값 → 배수 표 (처음 물을 때 만든다 — 지역 계산이고 데이터만의 함수라 결정적)
    [JsonIgnore] float[]? sight, hit, move, bino;

    void Build()
    {
        int nk = (int)WeatherKind.Snow + 1, nt = (int)TimeOfDay.Night + 1;
        var s = new float[nk * nt]; var h = new float[nk * nt]; var m = new float[nk * nt]; var b = new float[nt];
        for (int t = 0; t < nt; t++)
        {
            var te = Times.TryGetValue(WeatherState.KeyOf((TimeOfDay)t), out var x) ? x : null;
            b[t] = te?.Binocular ?? 1;
            for (int k = 0; k < nk; k++)
            {
                var ke = States.TryGetValue(WeatherState.KeyOf((WeatherKind)k), out var y) ? y : null;
                int i = k * nt + t;
                s[i] = MathF.Max((ke?.Sight ?? 1) * (te?.Sight ?? 1), MathF.Min(1f, MinSightMul));
                h[i] = (ke?.Hit ?? 1) * (te?.Hit ?? 1);
                m[i] = (ke?.Move ?? 1) * (te?.Move ?? 1);
            }
        }
        sight = s; hit = h; move = m; bino = b;
    }

    static int Idx(WeatherKind k, TimeOfDay t) => (int)k * ((int)TimeOfDay.Night + 1) + (int)t;
    public float SightMul(WeatherKind k, TimeOfDay t) { if (sight == null) Build(); return sight![Idx(k, t)]; }
    public float HitMul(WeatherKind k, TimeOfDay t) { if (hit == null) Build(); return hit![Idx(k, t)]; }
    public float MoveMul(WeatherKind k, TimeOfDay t) { if (move == null) Build(); return move![Idx(k, t)]; }
    public float BinocularMul(TimeOfDay t) { if (bino == null) Build(); return bino![(int)t]; }
}

/// <summary>지도 파일의 날씨 설정 ("weather" · "time" · "timeStep" · "schedule").</summary>
public sealed class WeatherSetup
{
    public WeatherKind Kind;
    public TimeOfDay Time = TimeOfDay.Day;
    public int TimeStepTicks;
    public readonly List<WeatherEvent> Schedule = new();
}

public sealed class WeatherScheduleEntry
{
    /// <summary>시작 후 분 (소수 가능).</summary>
    public float At { get; set; }
    public string? Weather { get; set; }
    public string? Time { get; set; }
}

public sealed partial class World
{
    /// <summary>날씨 · 시간대 (저장 WTHR · 체크섬). 바꾸는 곳 = SetupWeather (틱 0 전) · StepWeather (일정 틱).</summary>
    public readonly WeatherState Weather = new();

    /// <summary>[시작 설정 전용 — 틱 0 전] 날씨 · 시간대 · 일정을 정한다. 시야가 바뀌므로 안개를 즉시 다시 계산.</summary>
    public void SetupWeather(WeatherKind kind, TimeOfDay time, IEnumerable<WeatherEvent>? schedule = null, int timeStepTicks = -1)
    {
        Weather.Kind = Weather.StartKind = kind;
        Weather.Time = Weather.StartTime = time;
        if (schedule != null)
        {
            Weather.Schedule.Clear();
            Weather.Schedule.AddRange(schedule.Where(e => e.Tick > 0).OrderBy(e => e.Tick));
            Weather.Next = 0;
        }
        if (timeStepTicks >= 0) Weather.TimeStepTicks = timeStepTicks;
        Fog.Update();
    }

    /// <summary>미션 사건의 날씨 · 시간대 변경 (WorldMission.OnMissionWeather 구현): 시작값은 건드리지 않고 현재 값만 바꾼다 — 리플레이 머리말과 충돌 없음.</summary>
    partial void OnMissionWeather(string? weather, string? timeOfDay)
    {
        bool changed = false;
        if (weather != null && WeatherState.ParseKind(weather) is var k && k != Weather.Kind) { Weather.Kind = k; changed = true; }
        if (timeOfDay != null && WeatherState.ParseTime(timeOfDay) is var t && t != Weather.Time) { Weather.Time = t; changed = true; }
        if (changed) { Weather.Changes++; Fog.Update(); }
    }

    /// <summary>World.Step 맨 앞: 일정 틱이면 날씨 · 시간대를 바꾼다 (명령과 같은 틱 경계 — 결정적).</summary>
    void StepWeather()
    {
        var w = Weather;
        bool changed = false;
        while (w.Next < w.Schedule.Count && w.Schedule[w.Next].Tick <= Tick)
        {
            var e = w.Schedule[w.Next++];
            if (e.Kind >= 0 && (WeatherKind)e.Kind != w.Kind) { w.Kind = (WeatherKind)e.Kind; changed = true; }
            if (e.Time >= 0 && (TimeOfDay)e.Time != w.Time) { w.Time = (TimeOfDay)e.Time; changed = true; }
        }
        if (w.TimeStepTicks > 0 && Tick > 0 && Tick % w.TimeStepTicks == 0)
        {
            w.Time = (TimeOfDay)(((int)w.Time + 1) % ((int)TimeOfDay.Night + 1));
            changed = true;
        }
        if (changed) w.Changes++;
    }

    /// <summary>날씨 · 시간대 시야 배수 (1 = 영향 없음).</summary>
    public float WeatherSightMul => Defs.Weather.SightMul(Weather.Kind, Weather.Time);
    /// <summary>날씨 · 시간대 명중 배수.</summary>
    public float WeatherHitMul => Defs.Weather.HitMul(Weather.Kind, Weather.Time);
    /// <summary>날씨 · 시간대 이동 배수.</summary>
    public float WeatherMoveMul => Defs.Weather.MoveMul(Weather.Kind, Weather.Time);

    /// <summary>날씨로 줄인 시야 (정수 반경 — 지형 시야 메모 키가 이 값이라 메모는 그대로 유효). 하한 weather.minSight (기본 8).</summary>
    internal int WeatherSight(int s, float mul)
    {
        if (mul == 1f) return s;
        int r = (int)MathF.Round(s * mul);
        if (mul < 1f && r < Defs.Weather.MinSight) r = Math.Min(s, Defs.Weather.MinSight);
        return r;
    }

    /// <summary>건물 시야 (날씨 적용).</summary>
    internal int BuildingSight(Building b) => WeatherSight(b.Sight, WeatherSightMul);

    void HashWeather(ref Hasher h)
    {
        var w = Weather;
        h.Add((int)w.Kind); h.Add((int)w.Time); h.Add((int)w.StartKind); h.Add((int)w.StartTime);
        h.Add(w.TimeStepTicks); h.Add(w.Next); h.Add(w.Schedule.Count);
    }
}
