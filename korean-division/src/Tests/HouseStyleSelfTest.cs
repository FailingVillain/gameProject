using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.Tests;

/// <summary>T55s 민가 양식 선택 (HouseStyles.cs): 결정성 · 이웃 반복 · 그림 없을 때 건너뜀 · 반전/색조 조합 · 아틀라스 예산 · houseStyle 필드. 창 없이 (렌더 전용 - 시뮬 무관). KD_SELFTEST_ONLY=housestyle</summary>
public static class HouseStyleSelfTest
{
    // 3x3 가옥을 1칸 띄워 격자로 칠한 지도: 14 x 14 = 196 채
    static string GridMap(string? style)
    {
        var paint = new System.Text.StringBuilder();
        for (int j = 0; j < 14; j++)
        for (int i = 0; i < 14; i++)
            paint.Append($"{{ \"terrain\": \"house\", \"rect\": [{4 + i * 4}, {4 + j * 4}, 3, 3] }},");
        string st = style == null ? "" : $"\"houseStyle\": \"{style}\",";
        return $"{{ \"name\": \"house grid\", \"width\": 64, \"height\": 64, \"base\": \"grass\", {st} \"starts\": [ [1, 1], [62, 62] ], \"paint\": [ {paint.ToString().TrimEnd(',')} ] }}";
    }

    static HashSet<string> AllKeys(HouseStyleTable t, bool withB)
    {
        var s = new HashSet<string>();
        foreach (var c in t.Classes.Keys)
            foreach (var k in HouseStyles.ClassKeys(t, c)) { s.Add(k); if (withB && t.Classes[c].Baked >= 2) s.Add(k + ".b"); }
        return s;
    }

    static int Repeats(Dictionary<long, HousePick> res)
    {
        // 격자 지도: 이웃 = 4칸 간격 왼쪽 / 위쪽 조각
        int n = 0;
        foreach (var (k, p) in res)
        {
            int x = (int)(k & 0xffffffff), y = (int)(k >> 32);
            if (res.TryGetValue(((long)y << 32) | (uint)(x - 4), out var l) && l.Key == p.Key && l.Variant == p.Variant) n++;
            if (res.TryGetValue(((long)(y - 4) << 32) | (uint)x, out var u) && u.Key == p.Key && u.Variant == p.Variant) n++;
        }
        return n;
    }

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var t = HouseStyles.Current;
        check(t.Classes.Count == 8 && t.Styles.ContainsKey("korean") && t.Styles.ContainsKey("manchu") && t.Styles.ContainsKey("mixed") && t.Tones.Count == 3,
              $"house_styles.json: 클래스 {t.Classes.Count} · 양식 {string.Join(",", t.Styles.Keys)} · 색조 {t.Tones.Count}단계");
        int plannedKo = 0, plannedZh = 0;
        foreach (var c in HouseStyles.ClassesOf(t, "korean")) plannedKo += HouseStyles.ClassKeys(t, c).Count();
        foreach (var c in HouseStyles.ClassesOf(t, "manchu")) plannedZh += HouseStyles.ClassKeys(t, c).Count();
        check(plannedKo == 21 && plannedZh == 21, $"기본 42종 등록: 한국 {plannedKo} + 만주·중국 {plannedZh}");

        // 양식 이름 해석: 생략 / legacy / 모르는 이름 = 예전 4종
        check(HouseStyles.Resolve(t, null) == null && HouseStyles.Resolve(t, "legacy") == null && HouseStyles.Resolve(t, "bogus") == null && HouseStyles.Resolve(t, " Korean ") == "korean",
              "houseStyle 생략 / legacy / 모르는 이름 = 예전 4종, 대소문자 · 공백 무시");

        // houseStyle 필드: 지도 파일 / 미션 덮어쓰기 (체크섬 · 스냅샷에 없는 표)
        var mPlain = MapFile.LoadText(GridMap(null), "hs_plain", opt.Seed);
        var mKo = MapFile.LoadText(GridMap("Korean"), "hs_ko", opt.Seed);
        MapFile.SetMissionHouseStyle(mKo, "manchu");
        var mNull = MapFile.LoadText(GridMap("korean"), "hs_ko2", opt.Seed);
        MapFile.SetMissionHouseStyle(mNull, null);
        check(MapFile.HouseStyleOf(mPlain) == null && MapFile.HouseStyleOf(mKo) == "manchu" && MapFile.HouseStyleOf(mNull) == "korean",
              $"houseStyle: 생략 = null · 미션이 지도 값을 덮어씀 ({MapFile.HouseStyleOf(mKo)}) · 미션 null 은 지도 값 유지");

        var blocks = HouseBlocks.Build(mPlain).ToList();
        check(blocks.Count == 196, $"시험 지도 가옥 {blocks.Count}채");
        var all = AllKeys(t, true);
        bool Has(string k) => all.Contains(k);

        // 결정성 + 분포 + 이웃 반복
        foreach (var style in new[] { "korean", "manchu", "mixed" })
        {
            var a = HouseStyles.Assign(t, style, 64, 64, blocks, Has);
            var b = HouseStyles.Assign(t, style, 64, 64, HouseBlocks.Build(mPlain), Has);
            bool same = a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var o) && o == kv.Value);
            check(same && a.Count == 196, $"[{style}] 같은 지도 두 번 배정이 같다 ({a.Count}채)");
            int rep = Repeats(a);
            var off = new HouseStyleTable { KeyPrefix = t.KeyPrefix, WideRatio = t.WideRatio, LargeArea = t.LargeArea, Retry = 0, Tones = t.Tones, Classes = t.Classes, Styles = t.Styles };
            int repOff = Repeats(HouseStyles.Assign(off, style, 64, 64, blocks, Has));
            int pairs = 2 * 13 * 14;
            Console.WriteLine($"  [housestyle] {style}: 이웃 반복 {rep}/{pairs} (회피 끔 {repOff})");
            check(rep * 100 <= pairs * 2 && rep <= repOff, $"[{style}] 이웃(왼쪽 · 위) 같은 그림 반복 {rep}/{pairs} <= 2% 이고 회피를 끈 {repOff} 이하");
            int nB = a.Values.Count(p => p.Variant == 1), nM = a.Values.Count(p => p.Mirror);
            int tones = a.Values.Select(p => p.Tone).Distinct().Count();
            int combos = a.Values.Select(p => (p.Key, p.Variant, p.Tone, p.Mirror)).Distinct().Count();
            check(tones == 3 && nM > a.Count * 35 / 100 && nM < a.Count * 65 / 100 && combos >= 60,
                  $"[{style}] 반전 {nM}/{a.Count} · 색조 {tones}단계 · 구운 B {nB} · 서로 다른 조합 {combos}");
            check(a.Values.All(p => Has(p.DrawKey)), $"[{style}] 고른 그림은 모두 테마에 있는 키");
        }
        var ko = HouseStyles.Assign(t, "korean", 64, 64, blocks, Has);
        var mx = HouseStyles.Assign(t, "mixed", 64, 64, blocks, Has);
        check(ko.Values.All(p => p.ClassId is "choga" or "giwa" or "heotgan" or "inn") && mx.Values.Any(p => p.ClassId is "mud" or "zhgiwa" or "earth" or "zhbarn") && mx.Values.Any(p => p.ClassId is "choga" or "giwa" or "heotgan" or "inn"),
              "korean 은 한국 클래스만, mixed 는 두 문화권 모두");

        // 그림이 일부만 / 없을 때
        var legacy4 = new HashSet<string> { "building_art.house.thatch", "building_art.house.tile", "building_art.house.brick", "building_art.house.barn" };
        var fb = HouseStyles.Assign(t, "manchu", 64, 64, blocks, legacy4.Contains);
        check(fb.Count == 196 && fb.Values.All(p => legacy4.Contains(p.Key) && p.Variant == 0), "새 그림이 없으면 클래스 fallback (예전 4종) 으로 그림 - 전부 건너뛰지 않음");
        var none = HouseStyles.Assign(t, "korean", 64, 64, blocks, _ => false);
        check(none.Count == 0 && HouseStyles.PickRuin(t, "korean", 3, 3, _ => false) == null, "그림이 하나도 없으면 아무것도 안 고름 (예외 없음)");
        var some = new HashSet<string> { "building_art.house.ko-cho-01", "building_art.house.ko-cho-02", "building_art.house.ko-giwa-03" };
        var part = HouseStyles.Assign(t, "korean", 64, 64, blocks, some.Contains);
        check(part.Count == 196 && part.Values.All(p => some.Contains(p.Key) && p.Variant == 0) && part.Values.Select(p => p.Key).Distinct().Count() == 3,
              "일부 키만 있으면 그것만 쓰고 B 변형이 없으면 A 로");
        check(HouseStyles.AnyArt(t, some.Contains) && !HouseStyles.AnyArt(t, _ => false) && HouseStyles.AnyArt(t, legacy4.Contains), "AnyArt: 새 키 · fallback 키 있으면 참, 없으면 거짓");

        // 폐허 선택: 결정적
        bool ruinSame = true;
        for (int i = 0; i < 50; i++) ruinSame &= HouseStyles.PickRuin(t, "mixed", i, 7, Has) == HouseStyles.PickRuin(t, "mixed", i, 7, Has);
        check(ruinSame, "폐허 칸 선택이 결정적");

        // 아틀라스 예산
        double ko0 = HouseStyles.AtlasBudgetMb(t, "korean", false), zh0 = HouseStyles.AtlasBudgetMb(t, "manchu", false);
        double koR = HouseStyles.AtlasBudgetMb(t, "korean", true), mxR = HouseStyles.AtlasBudgetMb(t, "mixed", true);
        double stub = HouseStyles.AtlasBudgetMb(t, "korean", true, legacy4.Contains);
        Console.WriteLine($"  [housestyle] 예산 MB: 한국 {ko0} 만주 {zh0} 한국+폐허 {koR} 혼합+폐허 {mxR} 스텁(예전 4종) {stub} · LOD0 사용 {HouseStyles.Lod0Use(t, mxR, 768):P0}");
        check(ko0 == 60 && zh0 == 72 && koR == 102 && mxR == 216 && stub == 0 && HouseStyles.Lod0Use(t, mxR, 768) < 0.25,
              "아틀라스 예산 함수: 한국 30프레임=60MB · 만주 36=72MB · 한국+폐허 102MB · 혼합+폐허 216MB (LOD0 768MB 한도의 25% 미만)");
    }
}
