using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// T44 전 미션 기능 스모크 (KD_SELFTEST_ONLY=smoke). assets/missions/*.json 전수를 동적으로 열거해 한 개씩 직렬로
/// MissionDef.Load -> CreateMissionWorld -> AttachMissionAi (--mission-run 과 같은 경로) 로 끝(Winner != 0) 또는 시간 제한 + 30초까지 돌린다.
/// 기능 상태만 보고한다 (승패 · 밸런스 아님). 상태: END(winner N) / TIME_LIMIT / STALL(300 시뮬초 무진행) / TIMEOUT(벽시계) / EXC / LOAD_FAIL.
/// 환경: KD_SMOKE_ID=<id> 한 미션만, KD_SMOKE_WALL=<초> 미션당 벽시계 상한 (기본 240), KD_SMOKE_STALL=<시뮬초> (기본 300).
/// 예외 / 로드 실패 = selftest FAIL, STALL / TIMEOUT = 경고. 시뮬 코드는 읽기만 한다.
/// </summary>
public static class MissionSmoke
{
    public sealed record Row(string Id, string Status, string Detail, long Ticks, float SimSec, double Wall, ulong Checksum, bool Hard);

    static double EnvD(string k, double d) => double.TryParse(Environment.GetEnvironmentVariable(k), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : d;

    public static List<string> Enumerate()
    {
        var ids = new List<string>();
        if (Directory.Exists(MissionDef.Folder))
            foreach (var f in Directory.GetFiles(MissionDef.Folder, "*.json")) ids.Add(Path.GetFileNameWithoutExtension(f));
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    /// <summary>한 미션을 기능 스모크로 돌린다. maxSimSec &lt;= 0 이면 시간 제한 + 30초.</summary>
    public static Row RunOne(string id, float maxSimSec = 0)
    {
        double wallLimit = EnvD("KD_SMOKE_WALL", 240), stallSec = EnvD("KD_SMOKE_STALL", 300);
        var sw = Stopwatch.StartNew();
        MissionDef def; World w;
        try
        {
            def = MissionDef.Load(id);
            w = World.CreateMissionWorld(def, Defs.Current);
            World.AttachMissionAi(w, def.Player, true);
        }
        catch (Exception e) { return new Row(id, "LOAD_FAIL", e.GetType().Name + ": " + e.Message.Split('\n')[0], 0, 0, sw.Elapsed.TotalSeconds, 0, true); }
        try
        {
            if (maxSimSec <= 0) maxSimSec = (KoreanDivision.UI.MissionText.TimeLimit(def) is float tl and > 0 ? tl : 1800) + 30;
            long maxTick = (long)(maxSimSec / Config.SimDt), sampleEvery = Math.Max(1, (long)(1f / Config.SimDt));
            var last = new Dictionary<int, Vector2>();
            int lastAlive = w.Units.Count(u => u.Alive); long lastShotTick = -1;
            float lastProgress = 0;
            string status = "TIME_LIMIT", detail = "";
            while (w.Winner == 0 && w.Tick < maxTick)
            {
                w.Step();
                if (w.Tick % sampleEvery != 0) continue;
                bool progress = false;
                int alive = 0;
                foreach (var u in w.Units)
                {
                    if (!u.Alive) continue;
                    alive++;
                    if (last.TryGetValue(u.Id, out var p) && Vector2.DistanceSquared(p, u.Pos) > 0.0025f) progress = true;
                    last[u.Id] = u.Pos;
                }
                if (alive != lastAlive) { progress = true; lastAlive = alive; }
                if (w.Shots.Count > 0 && w.Shots[^1].Tick != lastShotTick) { progress = true; lastShotTick = w.Shots[^1].Tick; }
                if (progress) lastProgress = w.Time;
                else if (w.Time - lastProgress >= stallSec)
                {
                    status = "STALL"; detail = $"no sim progress {stallSec:0}s since t={lastProgress:0}" + (id.Contains("escort") ? " (합법 후보: 호송 대기)" : "");
                    break;
                }
                if ((w.Tick & 255) == 0 && sw.Elapsed.TotalSeconds > wallLimit) { status = "TIMEOUT"; detail = $"wall>{wallLimit:0}s at t={w.Time:0}"; break; }
            }
            if (w.Winner != 0) { status = "END"; detail = $"winner {w.Winner}"; }
            return new Row(id, status, detail, w.Tick, w.Time, sw.Elapsed.TotalSeconds, w.ComputeChecksum(), false);
        }
        catch (Exception e) { return new Row(id, "EXC", e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @" + (e.StackTrace ?? "").Split('\n')[0].Trim(), w.Tick, w.Time, sw.Elapsed.TotalSeconds, 0, true); }
    }

    public static string Line(Row r) =>
        $"[smoke] {r.Id} | LOAD_{(r.Status == "LOAD_FAIL" ? "FAIL" : "OK")} | {r.Status}{(r.Detail.Length > 0 ? " (" + r.Detail + ")" : "")} | ticks {r.Ticks} | sim {r.SimSec:0}s | wall {r.Wall:0.0}s | checksum {r.Checksum:X16}";

    /// <summary>지도 정의상 최대 인원 (starts 개수) 이 Config.MaxPlayers(16) 인 지도 / 미션 id 목록.</summary>
    static List<string> MaxPlayerMaps()
    {
        var hit = new List<string>();
        void Scan(string name, JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("starts", out var s) && s.ValueKind == JsonValueKind.Array && s.GetArrayLength() >= Config.MaxPlayers) hit.Add(name);
        }
        try
        {
            string maps = Path.Combine(AppContext.BaseDirectory, "assets", "maps");
            if (Directory.Exists(maps))
                foreach (var f in Directory.GetFiles(maps, "*.json").OrderBy(x => x, StringComparer.Ordinal))
                { using var d = JsonDocument.Parse(File.ReadAllText(f)); Scan("map:" + Path.GetFileNameWithoutExtension(f), d.RootElement); }
            foreach (var f in Directory.GetFiles(MissionDef.Folder, "*.json").OrderBy(x => x, StringComparer.Ordinal))
            { using var d = JsonDocument.Parse(File.ReadAllText(f)); if (d.RootElement.TryGetProperty("mapData", out var md)) Scan("mission:" + Path.GetFileNameWithoutExtension(f), md); }
        }
        catch (Exception) { }
        return hit;
    }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { }
        string? only = Environment.GetEnvironmentVariable("KD_SMOKE_ID");
        var ids = Enumerate();
        if (!string.IsNullOrEmpty(only)) ids = ids.Where(i => i == only).ToList();
        check(ids.Count > 0, $"smoke: 미션 {ids.Count}개 열거");
        var rows = new List<Row>();
        foreach (var id in ids)
        {
            var r = RunOne(id);
            rows.Add(r);
            Console.WriteLine(Line(r));
            check(!r.Hard, $"smoke {id}: 로드 · 예외 없음 ({r.Status})");
        }
        if (string.IsNullOrEmpty(only))
        {
            // 결정성 1건: 첫 미션 60 시뮬초를 같은 시드로 두 번 -> 체크섬 동일
            if (ids.Count > 0 && !rows[0].Hard)
            {
                var a = RunOne(ids[0], 60); var b = RunOne(ids[0], 60);
                check(a.Checksum == b.Checksum && a.Ticks == b.Ticks && !a.Hard, $"smoke 결정성 {ids[0]} 60s 2회 체크섬 동일 ({a.Checksum:X16} / {b.Checksum:X16})");
            }
            var big = MaxPlayerMaps();
            Console.WriteLine("[smoke] 16-player AI map: " + (big.Count == 0 ? "none" : string.Join(", ", big)));
            foreach (var r in rows.Where(x => x.Status is "STALL" or "TIMEOUT")) Console.WriteLine($"[smoke] WARN {r.Id}: {r.Status} {r.Detail}");
        }
    }
}
