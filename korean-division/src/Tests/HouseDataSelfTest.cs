using System.Text.Json;

namespace KoreanDivision.Tests;

/// <summary>
/// T55m (2026-10-22) 민가 기본 42종 데이터 검증 — 읽기 전용 (그림 · 시뮬 값은 건드리지 않음). `KD_SELFTEST_ONLY=housedata`.
/// (1) 레지스트리 assets/data/house_classes.json: 42종 (한국 21 · 만주·중국 21), 구운 변형 30 / 36, 발자국 3x3 16 · 3x4 14 · 4x4 8 · 3x5 4, id 중복 없음.
/// (2) 테마 키 building_art.house.&lt;style&gt;.&lt;id&gt;.&lt;tone&gt; 대 레지스트리: 종 · 변형이 없는 키 = 오류 (orphan). 그림 없는 변형 = 경고만 (content 가 채울 때까지 정상).
/// (3) 키가 가리키는 레이아웃 파일이 실제로 있는가 (없으면 오류).
/// 스타일 없는 레거시 키 (building_art.house.thatch 등) 는 검사 대상이 아니다.
/// </summary>
public static class HouseDataSelfTest
{
    static readonly Dictionary<string, (int Classes, int Tones)> ExpectStyle = new() { ["korean"] = (21, 30), ["manchu"] = (21, 36) };
    static readonly Dictionary<string, int> ExpectFoot = new() { ["3x3"] = 16, ["3x4"] = 14, ["4x4"] = 8, ["3x5"] = 4 };

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            string regPath = Find(Path.Combine("assets", "data", "house_classes.json"));
            string themePath = Find(Path.Combine("assets", "themes", "kd1920", "theme.json"));
            string themeDir = Path.GetDirectoryName(themePath)!;

            // (1) 레지스트리
            using var rd = JsonDocument.Parse(File.ReadAllText(regPath));
            var classes = rd.RootElement.GetProperty("classes").EnumerateArray().ToList();
            var ids = new HashSet<string>();
            var expectKeys = new List<string>();   // style.id.tone
            var styleCls = new Dictionary<string, int>();
            var styleTone = new Dictionary<string, int>();
            var foot = new Dictionary<string, int>();
            foreach (var c in classes)
            {
                string id = c.GetProperty("id").GetString()!, style = c.GetProperty("style").GetString()!;
                int tones = c.GetProperty("tones").GetInt32();
                var fp = c.GetProperty("footprint").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                ids.Add(id);
                styleCls[style] = styleCls.GetValueOrDefault(style) + 1;
                styleTone[style] = styleTone.GetValueOrDefault(style) + tones;
                string fk = $"{fp[0]}x{fp[1]}";
                foot[fk] = foot.GetValueOrDefault(fk) + 1;
                check(tones >= 1 && tones <= 8, $"housedata: {id} 변형 수 {tones} 범위 밖 (1~8)");
                for (int t = 0; t < tones; t++) expectKeys.Add($"{style}.{id}.{(char)('a' + t)}");
            }
            check(classes.Count == 42 && ids.Count == 42, $"housedata: 종 수 {classes.Count} (고유 id {ids.Count}), 기대 42");
            foreach (var kv in ExpectStyle)
            {
                check(styleCls.GetValueOrDefault(kv.Key) == kv.Value.Classes, $"housedata: {kv.Key} 종 {styleCls.GetValueOrDefault(kv.Key)}, 기대 {kv.Value.Classes}");
                check(styleTone.GetValueOrDefault(kv.Key) == kv.Value.Tones, $"housedata: {kv.Key} 구운 변형 {styleTone.GetValueOrDefault(kv.Key)}, 기대 {kv.Value.Tones}");
            }
            foreach (var kv in ExpectFoot)
                check(foot.GetValueOrDefault(kv.Key) == kv.Value, $"housedata: 발자국 {kv.Key} {foot.GetValueOrDefault(kv.Key)}, 기대 {kv.Value}");

            // (2)(3) 테마 키
            using var td = JsonDocument.Parse(File.ReadAllText(themePath));
            var sprites = td.RootElement.GetProperty("sprites");
            var present = new HashSet<string>();
            var orphans = new List<string>();
            var badFiles = new List<string>();
            foreach (var prop in sprites.EnumerateObject())
            {
                const string pre = "building_art.house.";
                if (!prop.Name.StartsWith(pre, StringComparison.Ordinal)) continue;
                string rest = prop.Name.Substring(pre.Length);
                string first = rest.Split('.')[0];
                if (first != "korean" && first != "manchu") continue;   // 레거시 키
                if (!expectKeys.Contains(rest)) { orphans.Add(prop.Name); continue; }
                present.Add(rest);
                if (prop.Value.TryGetProperty("layouts", out var lays))
                    foreach (var l in lays.EnumerateArray())
                    {
                        string f = l.GetString() ?? "";
                        if (!File.Exists(Path.Combine(themeDir, f))) badFiles.Add(f);
                    }
            }
            var missing = expectKeys.Where(k => !present.Contains(k)).ToList();
            check(orphans.Count == 0, $"housedata: 레지스트리에 없는 테마 키 {orphans.Count}개 (orphan): {string.Join(", ", orphans.Take(5))}");
            check(badFiles.Count == 0, $"housedata: 테마 키가 가리키는 레이아웃 파일 없음 {badFiles.Count}개: {string.Join(", ", badFiles.Take(5))}");
            if (missing.Count > 0)
                Console.WriteLine($"housedata: 경고 (그림 없음) {missing.Count}/{expectKeys.Count} 변형, 예: {string.Join(", ", missing.Take(3))}");
            Console.WriteLine($"housedata: 종 {classes.Count} · 변형 {expectKeys.Count} · 키 {present.Count} · 그림 없음 {missing.Count}");
        }
        catch (Exception e) { check(false, $"housedata 중 예외: {e.GetType().Name}: {e.Message}"); }
    }

    static string Find(string rel)
    {
        string a = Path.Combine(AppContext.BaseDirectory, rel);
        return File.Exists(a) ? a : Path.Combine(Directory.GetCurrentDirectory(), rel);
    }
}
