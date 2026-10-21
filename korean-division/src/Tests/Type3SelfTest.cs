using System.Numerics;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>3년식 중기관총 (2026-10-21): 무기 수치, 30발 점사 + 1.2초 교체, 일본 전용 유닛 / 미션 편성.</summary>
public static class Type3SelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            var defs = Defs.Current;
            var hmg = defs.Weapons["hmg"]; var t3 = defs.Weapons["hmg_type3"];
            check(t3.Name == "3년식 중기관총" && MathF.Abs(t3.Cooldown - 0.15f) < 1e-4f && MathF.Abs(t3.AccNear - 0.34f) < 1e-4f && MathF.Abs(t3.AccFar - 0.15f) < 1e-4f, "3년식: 간격 0.15, 명중 0.34 / 0.15");
            check(t3.Range == hmg.Range && t3.Damage == hmg.Damage && t3.Suppress == hmg.Suppress && t3.Spray == hmg.Spray && t3.Ammo == 600 && t3.Accuracy == hmg.Accuracy, "3년식: 사거리 · 위력 · 제압 · 흩뿌림 = 맥심, 탄약 600");
            check(t3.BurstMin == 30 && t3.BurstMax == 30 && MathF.Abs(t3.BurstPauseMin - 1.2f) < 1e-4f && MathF.Abs(t3.BurstPauseMax - 1.2f) < 1e-4f && t3.Bursty, "3년식: 30발 점사 + 휴지 1.2초 (고정)");
            var ux = defs.Units["heavy_type3"]; var mx = defs.Units["heavy_maxim"];
            check(ux.Weapon == "hmg_type3" && ux.Hp == mx.Hp && ux.SetupTime == mx.SetupTime && ux.PackTime == mx.PackTime && ux.Speed == mx.Speed, "3년식 팀 = 맥심 팀 (체력 · 거치 · 철수 · 속도 동일)");
            check(ux.AllowedFor("japanese") && !ux.AllowedFor("korean") && !ux.AllowedFor("fengtian"), "3년식 팀은 일본 전용");
            check(defs.Units["heavy_maxim"].AllowedFor("japanese"), "맥심 정의는 그대로");

            var w = TestDefs.Strip(World.CreateStandard(MapFile.Load("flat4", opt.Seed + 91), defs));
            w.Fog.Enabled = false;
            var c = new Vector2(64.5f, 56.5f);
            var u = w.SpawnUnit(ux, 1, c);
            var f = w.SpawnUnit(defs.Units["infantry"], 2, c + new Vector2(12, 0));
            f.Weapon = null; f.Hp = 1000; w.Hold(2, new[] { f });
            w.Hold(1, new[] { u });
            var ticks = new List<long>();
            for (int i = 0; i < 60 * 60; i++)
            {
                w.Step();
                foreach (var s in w.Shots) if (s.Tick == w.Tick - 1 && s.ShooterId == u.Id) ticks.Add(s.Tick);
                f.Hp = 1000;
            }
            var lens = new List<int>(); var pauses = new List<double>(); int run = 1;
            for (int i = 1; i < ticks.Count; i++)
            {
                double gap = (ticks[i] - ticks[i - 1]) / 60.0;
                if (gap > 0.15 * 3 + 0.3) { lens.Add(run); run = 1; pauses.Add(gap); } else run++;
            }
            check(lens.Count >= 3 && lens.All(l => l == 30), $"3년식 사격: 점사마다 정확히 30발 ({lens.Count}번: {(lens.Count > 0 ? lens.Min() : 0)}~{(lens.Count > 0 ? lens.Max() : 0)})");
            check(pauses.Count >= 3 && pauses.All(p => p >= 1.2 - 0.02 && p <= 1.2 + 0.15 * 2 + 0.05), $"3년식 사격: 교체 휴지 약 1.2초 ({(pauses.Count > 0 ? pauses.Min() : 0):F2}~{(pauses.Count > 0 ? pauses.Max() : 0):F2}초)");

            int jp = 0, other = 0;
            foreach (var m in new[] { "yalu_crossing", "ai_kr_vs_jp_trench", "trench_assault", "artillery_spotters" })
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(MissionDef.PathOf(m)), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var ens = doc.RootElement.GetProperty("factions").EnumerateArray().Select(x => x.GetProperty("en").GetString()!).ToArray();
                foreach (var force in doc.RootElement.GetProperty("forces").EnumerateArray())
                {
                    if (!force.TryGetProperty("units", out var us)) continue;
                    string en = ens[force.GetProperty("faction").GetInt32() - 1];
                    if (us.TryGetProperty("heavy_type3", out _)) { if (en == "japanese") jp++; else other++; }
                    if (en == "japanese" && us.TryGetProperty("heavy_maxim", out _)) other++;
                }
            }
            check(jp == 4 && other == 0, $"일본 미션 4곳은 3년식 사용, 일본 편성에 맥심 없음 (jp={jp}, other={other})");
        }
        catch (Exception e) { check(false, $"3년식 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }
}
