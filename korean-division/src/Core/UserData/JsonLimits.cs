using System.Text;
using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>JsonLimits.Check 결과 (예외 대신 코드 + 한국어 사유).</summary>
public readonly record struct JsonLimitResult(bool Ok, string Code, string Reason)
{
    public static readonly JsonLimitResult Pass = new(true, "", "");
}

/// <summary>T51a: 사용자 JSON(미션 · 지도) 을 파싱하기 전의 한도 검사. 읽기 전용, 예외 없음.
/// 크기 ≤4MB · 깊이 ≤32 · 문자열 ≤4096 · 배열 ≤20000 · 숫자 유한 · 같은 객체 안 중복 키 거부.
/// 주석 · 끝 쉼표는 게임 파서와 같이 허용한다. 키 비교는 대소문자 무시 (게임이 PropertyNameCaseInsensitive 로 읽으므로 'Id' 와 'id' 는 같은 키).</summary>
public static class JsonLimits
{
    public const int MaxBytes = 4 * 1024 * 1024, MaxDepth = 32, MaxString = 4096, MaxArray = 20000;

    public static JsonLimitResult Check(string? text)
    {
        if (text == null) return new(false, "E_JSON_EMPTY", "내용이 없습니다");
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) return new(false, "E_JSON_SIZE", $"파일이 너무 큽니다 (최대 {MaxBytes / 1024 / 1024}MB)");
        if (string.IsNullOrWhiteSpace(text)) return new(false, "E_JSON_EMPTY", "내용이 비어 있습니다");
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = MaxDepth + 8 });
            var keys = new Stack<HashSet<string>?>();   // 객체면 키 집합, 배열이면 null
            var counts = new Stack<int>();               // 배열 원소 수
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        if (keys.Count > 0 && keys.Peek() == null) Bump(counts);
                        if (keys.Count >= MaxDepth) return new(false, "E_JSON_DEPTH", $"중첩이 너무 깊습니다 (최대 {MaxDepth}단계)");
                        keys.Push(reader.TokenType == JsonTokenType.StartObject ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null);
                        counts.Push(0);
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        keys.Pop(); counts.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        string name = reader.GetString() ?? "";
                        if (name.Length > MaxString) return new(false, "E_JSON_STRING", $"키 이름이 너무 깁니다 (최대 {MaxString}자)");
                        if (!keys.Peek()!.Add(name)) return new(false, "E_JSON_DUP_KEY", $"같은 키가 두 번 나옵니다: '{Short(name)}'");
                        break;
                    case JsonTokenType.String:
                        if (keys.Count > 0 && keys.Peek() == null) Bump(counts);
                        if (reader.ValueSpan.Length > MaxString * 6 || (reader.GetString() ?? "").Length > MaxString)   // 이스케이프 최악 6바이트/문자
                            return new(false, "E_JSON_STRING", $"문자열이 너무 깁니다 (최대 {MaxString}자)");
                        break;
                    case JsonTokenType.Number:
                        if (keys.Count > 0 && keys.Peek() == null) Bump(counts);
                        if (!reader.TryGetDouble(out double d) || double.IsNaN(d) || double.IsInfinity(d))
                            return new(false, "E_JSON_NONFINITE", "유한하지 않은 숫자(NaN · Infinity · 범위 초과)가 있습니다");
                        break;
                    default: // True / False / Null
                        if (keys.Count > 0 && keys.Peek() == null) Bump(counts);
                        break;
                }
                if (counts.Count > 0 && keys.Peek() == null && counts.Peek() > MaxArray)
                    return new(false, "E_JSON_ARRAY", $"배열이 너무 큽니다 (최대 {MaxArray}개)");
            }
            return JsonLimitResult.Pass;
        }
        catch (JsonException)
        {
            // NaN · Infinity 리터럴은 표준 JSON 문법 오류로 나온다 — 사유를 구분해 준다
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<![\w""])-?(NaN|Infinity)(?![\w""])")) return new(false, "E_JSON_NONFINITE", "유한하지 않은 숫자(NaN · Infinity)가 있습니다");
            return new(false, "E_JSON_SYNTAX", "JSON 문법 오류이거나 깊이 한도를 넘었습니다");
        }
    }

    static void Bump(Stack<int> counts) { int c = counts.Pop(); counts.Push(c + 1); }
    static string Short(string s) => s.Length <= 40 ? s : s[..40] + "…";
}
