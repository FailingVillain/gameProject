using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>날씨 · 시간대 (Weather.cs). SelfTest 가 호출, 단독 KD_SELFTEST_ONLY=weather.</summary>
public static class WeatherSelfTest
{
    // 일정: 0.05분 (180틱) 에 비, 0.1분 (360틱) 에 새벽
    const string WeatherMap = """
    { "name": "weather test", "width": 96, "height": 96, "base": "grass",
      "starts": [ [10, 10], [86, 86] ],
      "weather": "fog", "time": "night",
      "schedule": [ { "at": 0.05, "weather": "rain" }, { "at": 0.1, "time": "dawn" } ],
      "paint": [ { "terrain": "forest", "rect": [40, 40, 10, 10] } ] }
    """;

    const string PlainMap = """
    { "name": "weather plain", "width": 96, "height": 96, "base": "grass",
      "starts": [ [10, 10], [86, 86] ],
      "paint": [ { "terrain": "forest", "rect": [40, 40, 10, 10] } ] }
    """;

    static World Load(string text, string id, int seed)
    {
        var w = World.CreateStandard(MapFile.LoadText(text, id, seed), Defs.Current);
        return w;
    }

    static int VisibleCount(World w, int f)
    {
        int n = 0;
        foreach (byte b in w.Fog.Grid(f)) if (b == 2) n++;
        return n;
    }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var wd = defs.Weather;
        var inf = defs.Units["infantry"];
        var off = defs.Units["officer"];

        // ---- 지도 json · 기본값
        var w = Load(WeatherMap, "weathertest", opt.Seed);
        check(w.Weather.Kind == WeatherKind.Fog && w.Weather.Time == TimeOfDay.Night && w.Weather.Schedule.Count == 2
              && w.Weather.Schedule[0].Tick == 180 && w.Weather.Schedule[1].Tick == 360, "지도 json: weather · time · schedule (분 → 틱)");
        var gen = World.CreateStandard(GameMap.Generate(64, 64, opt.Seed, 2), defs);
        check(gen.Weather.Kind == WeatherKind.Clear && gen.Weather.Time == TimeOfDay.Day && gen.Weather.Schedule.Count == 0, "생성 맵 = 맑은 낮");
        bool threw = false;
        try { WeatherState.ParseKind("hail"); } catch (InvalidDataException) { threw = true; }
        check(threw && WeatherState.ParseTime("NIGHT") == TimeOfDay.Night, "날씨 이름 해석 (모르는 이름은 오류)");

        // ---- 시야 배수 (보병 · 장교 쌍안경)
        var plain = Load(PlainMap, "weatherplain", opt.Seed);
        var a = plain.SpawnUnit(inf, 1, new Vector2(20.5f, 70.5f));
        var o = plain.SpawnUnit(off, 1, new Vector2(22.5f, 70.5f));
        int sDay = plain.SightOf(a), oDay = plain.SightOf(o);
        plain.SetupWeather(WeatherKind.Fog, TimeOfDay.Night);
        int sFN = plain.SightOf(a), oFN = plain.SightOf(o);
        float mFN = wd.SightMul(WeatherKind.Fog, TimeOfDay.Night);
        int expect = Math.Max(Math.Min(sDay, wd.MinSight), (int)MathF.Round(sDay * mFN));
        check(sFN == expect && sFN < sDay, $"안개 · 밤 보병 시야 {sDay} → {sFN} (×{mFN:0.###}, 하한 {wd.MinSight})");
        plain.SetupWeather(WeatherKind.Clear, TimeOfDay.Night);
        int sN = plain.SightOf(a), oN = plain.SightOf(o);
        check(oDay > sDay && (float)oN / sN < (float)oDay / sDay, $"밤에는 장교 쌍안경 보너스 감소 (낮 {oDay}/{sDay}, 밤 {oN}/{sN})");
        plain.SetupWeather(WeatherKind.Rain, TimeOfDay.Day);
        int sR = plain.SightOf(a);
        check(sR < sDay && sR > sFN, $"비: 시야 중간 감소 ({sR})");

        // ---- 명중 · 이동 배수 · 제압 영향 없음
        plain.SetupWeather(WeatherKind.Clear, TimeOfDay.Day);
        var b = plain.SpawnUnit(inf, 2, new Vector2(28.5f, 70.5f));
        float pDay = plain.HitChance(a, b), vDay = plain.UnitSpeedForTest(a);
        plain.SetupWeather(WeatherKind.Rain, TimeOfDay.Night);
        float pRN = plain.HitChance(a, b), vRN = plain.UnitSpeedForTest(a);
        float hm = wd.HitMul(WeatherKind.Rain, TimeOfDay.Night), mm = wd.MoveMul(WeatherKind.Rain, TimeOfDay.Night);
        check(pDay > 0 && MathF.Abs(pRN / pDay - hm) < 0.01f && hm < 1f, $"명중 배수: 비 · 밤 ×{hm:0.###} ({pDay:0.000} → {pRN:0.000})");
        check(MathF.Abs(vRN / vDay - mm) < 1e-3f, $"이동 배수: 비 · 밤 ×{mm:0.###} (비 이동 감소 없음, 2026-10-19 공격측 불리 해소)");
        plain.SetupWeather(WeatherKind.Snow, TimeOfDay.Day);
        check(MathF.Abs(plain.UnitSpeedForTest(a) / vDay - wd.MoveMul(WeatherKind.Snow, TimeOfDay.Day)) < 1e-3f, "이동 배수: 눈");
        a.Hp = 0; o.Hp = 0; b.Hp = 0; plain.Step();

        // ---- 안개(전장) 시야: 밤이 좁고, 메모가 반경을 키로 하므로 바뀐 뒤에도 새 월드와 같은 결과
        var day = Load(PlainMap, "weatherplain", opt.Seed);
        day.Fog.Update();
        int vDayTiles = VisibleCount(day, 1);
        day.SetupWeather(WeatherKind.Fog, TimeOfDay.Night);   // 같은 TerrainVision 메모를 이어 씀
        int vNightTiles = VisibleCount(day, 1);
        var fresh = Load(PlainMap, "weatherplain", opt.Seed);
        fresh.SetupWeather(WeatherKind.Fog, TimeOfDay.Night);
        check(vNightTiles < vDayTiles && vNightTiles > 0, $"안개 · 밤 보이는 칸 {vDayTiles} → {vNightTiles}");
        check(day.Fog.Grid(1).SequenceEqual(fresh.Fog.Grid(1)) && day.Fog.Grid(2).SequenceEqual(fresh.Fog.Grid(2)), "날씨 변경 후 시야 = 새로 만든 월드 (지형 시야 메모 유효)");
        day.SetupWeather(WeatherKind.Clear, TimeOfDay.Day);
        check(VisibleCount(day, 1) == vDayTiles, "맑은 낮으로 돌아오면 시야 원래대로");

        // ---- 일정: 정해진 틱에서만 바뀜 · 체크섬 반영
        ulong c0 = w.ComputeChecksum();
        while (w.Tick < 180) w.Step();
        check(w.Weather.Kind == WeatherKind.Fog, "일정 틱 전에는 그대로");
        w.Step();   // 틱 180 의 Step 이 바꾼다
        check(w.Weather.Kind == WeatherKind.Rain && w.Weather.Time == TimeOfDay.Night && w.Weather.Changes == 1, "0.05분 (틱 180): 비로 바뀜");
        while (w.Tick <= 360) w.Step();
        check(w.Weather.Time == TimeOfDay.Dawn && w.Weather.Changes == 2 && w.Weather.Next == 2, "0.1분 (틱 360): 새벽");
        check(c0 != w.ComputeChecksum(), "(전제) 체크섬 변화");
        var w2 = Load(WeatherMap, "weathertest", opt.Seed);
        while (w2.Tick < w.Tick) w2.Step();
        check(w2.ComputeChecksum() == w.ComputeChecksum(), "같은 지도 · 시드 → 같은 체크섬 (날씨 일정 결정적)");
        var w3 = Load(WeatherMap, "weathertest", opt.Seed);
        w3.Weather.Kind = WeatherKind.Snow;
        check(w3.ComputeChecksum() != Load(WeatherMap, "weathertest", opt.Seed).ComputeChecksum(), "체크섬에 날씨 포함");

        // ---- 시간 진행 (timeStep)
        var ts = Load(PlainMap, "weatherplain", opt.Seed);
        ts.SetupWeather(WeatherKind.Clear, TimeOfDay.Dusk, timeStepTicks: 60);
        for (int i = 0; i < 61; i++) ts.Step();
        check(ts.Weather.Time == TimeOfDay.Night, "timeStep: 해질녘 → 밤");
        for (int i = 0; i < 60; i++) ts.Step();
        check(ts.Weather.Time == TimeOfDay.Dawn, "timeStep: 밤 → 새벽 (순환)");

        // ---- 저장 · 불러오기
        var ws = Load(WeatherMap, "weathertest", opt.Seed);
        for (int i = 0; i < 100; i++) ws.Step();
        var loaded = Snapshot.Load(Snapshot.Save(ws, new SnapshotMeta("weather", 1, false, "none")), defs).World;
        check(loaded.Weather.Kind == ws.Weather.Kind && loaded.Weather.Time == ws.Weather.Time && loaded.Weather.Schedule.Count == 2
              && loaded.Weather.Next == ws.Weather.Next && loaded.ComputeChecksum() == ws.ComputeChecksum(), "저장 · 불러오기: 날씨 · 일정 · 체크섬");
        for (int i = 0; i < 200; i++) { ws.Step(); loaded.Step(); }
        check(loaded.Weather.Kind == WeatherKind.Rain && loaded.ComputeChecksum() == ws.ComputeChecksum(), "불러온 월드도 같은 틱에 비로 바뀜");

        // ---- 리플레이: --weather 덮어쓰기 재현
        var rw = World.CreateStandard(GameMap.Generate(64, 64, opt.Seed, 2), defs);
        rw.SetupWeather(WeatherKind.Snow, TimeOfDay.Night);
        var rf = ReplayFile.FromWorld(rw);
        var back = rf.CreateWorld(defs);
        check(back.Weather.Kind == WeatherKind.Snow && back.Weather.Time == TimeOfDay.Night && back.ComputeChecksum() == rw.ComputeChecksum(), "리플레이: 시작 날씨 (--weather · --time) 재현");
    }
}
