using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// N58 (2026-10-19) 경기관총 중대 편입: 소총중대마다 경기관총 (한국 3 · 봉천 쇼샤 2), 기관총 중대에는 없음, 일본 불가, 쇼샤 연사 x0.7.
/// </summary>
public static class LmgOrgSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            var defs = Defs.Current;
            var lmg = defs.Weapons["lmg"]; var cha = defs.Weapons["lmg_chauchat"];
            check(MathF.Abs(cha.Cooldown * 0.7f - lmg.Cooldown) < 1e-4f, $"쇼샤 간격 = 경기관총 / 0.7 ({cha.Cooldown:0.0000} vs {lmg.Cooldown:0.0000})");
            check(cha.Accuracy == lmg.Accuracy && cha.AccNear == lmg.AccNear && cha.Suppress == lmg.Suppress && cha.Damage == lmg.Damage, "쇼샤 = 경기관총과 명중 · 위력 · 제압 동일");
            var kr = defs.Units["heavy_machinegunner"]; var ft = defs.Units["heavy_machinegunner_fengtian"];
            check(kr.Weapon == "lmg" && ft.Weapon == "lmg_chauchat", "봉천 경기관총병 = 쇼샤, 한국 = 경기관총");
            check(kr.AllowedFor("korean") && !kr.AllowedFor("japanese") && !kr.AllowedFor("fengtian"), "일본 · 봉천은 일반 경기관총 편성 불가 (일본 경기관총 없음)");
            check(ft.AllowedFor("fengtian") && !ft.AllowedFor("japanese") && !ft.AllowedFor("korean"), "쇼샤병은 봉천 전용");
            check(defs.Units["heavy_maxim"].AllowedFor("japanese"), "맥심은 일본도 편성 (변경 없음)");
            int companies = 0, mgCompanies = 0;
            foreach (var f in new[] { "ai_kr_vs_ft_1000", "ai_bn_assault", "ai_bn_meeting" })
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(MissionDef.PathOf(f)));
                var ens = doc.RootElement.GetProperty("factions").EnumerateArray().Select(x => x.GetProperty("en").GetString()!).ToArray();
                foreach (var force in doc.RootElement.GetProperty("forces").EnumerateArray())
                {
                    string en = ens[force.GetProperty("faction").GetInt32() - 1];
                    string key = en == "korean" ? "heavy_machinegunner" : "heavy_machinegunner_fengtian";
                    int want = en == "korean" ? 3 : 2;
                    if (force.TryGetProperty("companies", out var cs))
                        foreach (var c in cs.EnumerateArray())
                        {
                            var u = c.GetProperty("units");
                            companies++;
                            int n = u.TryGetProperty(key, out var v) ? v.GetInt32() : 0;
                            check(n == want, $"{f} {force.GetProperty("id").GetString()}: 소총중대 경기관총 {n} (기대 {want})");
                            check(!u.TryGetProperty(en == "korean" ? "heavy_machinegunner_fengtian" : "heavy_machinegunner", out _), $"{f}: 다른 세력 경기관총 없음");
                        }
                    else if (force.TryGetProperty("units", out var us) && us.TryGetProperty("heavy_maxim", out _))
                    {
                        mgCompanies++;
                        check(!us.TryGetProperty("heavy_machinegunner", out _) && !us.TryGetProperty("heavy_machinegunner_fengtian", out _), $"{f} {force.GetProperty("id").GetString()}: 기관총 중대에 경기관총 없음");
                    }
                }
            }
            check(companies >= 11 && mgCompanies >= 8, $"소총중대 {companies} · 기관총 중대 {mgCompanies} 검사됨");
        }
        catch (Exception e) { check(false, $"경기관총 편성 테스트 중 예외: {e.GetType().Name}: {e.Message}"); }
    }
}
