using System.Text;
using System.Text.Json;

namespace MapEditor.Model;

/// <summary>
/// 미션 JSON 안의 `mapData` 객체만 바꾸는 텍스트 치환. 나머지 바이트 (// 주석, 키 순서, 들여쓰기 포함) 는 그대로 둔다.
/// 새 런타임 형식 없음 - 결과는 게임이 읽는 미션 JSON 이다.
/// </summary>
public static class MissionMapData
{
    static readonly JsonReaderOptions Opts = new() { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true };

    /// <summary>최상위 속성 `name` 의 속성 이름 시작, 값 시작, 값 끝 바이트 위치. 없으면 null.</summary>
    static (int PropStart, int ValueStart, int ValueEnd)? Find(byte[] bytes, string name)
    {
        var r = new Utf8JsonReader(bytes, Opts);
        while (r.Read())
        {
            if (r.TokenType == JsonTokenType.PropertyName && r.CurrentDepth == 1 && string.Equals(r.GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                int propStart = (int)r.TokenStartIndex;
                if (!r.Read()) return null;
                while (r.TokenType == JsonTokenType.Comment && r.Read()) { }
                int valueStart = (int)r.TokenStartIndex;
                if (r.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) r.Skip();
                return (propStart, valueStart, (int)r.BytesConsumed);
            }
        }
        return null;
    }

    static string Splice(byte[] bytes, int start, int end, string replacement) =>
        Encoding.UTF8.GetString(bytes, 0, start) + replacement + Encoding.UTF8.GetString(bytes, end, bytes.Length - end);

    /// <summary>미션 텍스트의 mapData 객체 원문 (없으면 null).</summary>
    public static string? Extract(string missionText)
    {
        var b = Encoding.UTF8.GetBytes(missionText);
        return Find(b, "mapData") is { } f ? Encoding.UTF8.GetString(b, f.ValueStart, f.ValueEnd - f.ValueStart) : null;
    }

    /// <summary>mapData 객체를 문서의 정규화 JSON 으로 치환. mapData 가 없으면 InvalidDataException.</summary>
    public static string Replace(string missionText, MapDoc doc)
    {
        var b = Encoding.UTF8.GetBytes(missionText);
        var f = Find(b, "mapData") ?? throw new InvalidDataException("미션에 mapData 가 없음 (map 참조면 EmbedReference 를 쓸 것)");
        return Splice(b, f.ValueStart, f.ValueEnd, doc.ToJson(LineIndent(b, f.PropStart)));
    }

    /// <summary>`"map": "id"` 참조를 내장 `"mapData": {...}` 로 바꾼다 (내장으로 복사). map 속성이 없으면 InvalidDataException.</summary>
    public static string EmbedReference(string missionText, MapDoc doc)
    {
        var b = Encoding.UTF8.GetBytes(missionText);
        if (Find(b, "mapData") != null) throw new InvalidDataException("이미 mapData 가 있음");
        var f = Find(b, "map") ?? throw new InvalidDataException("미션에 map 참조가 없음");
        return Splice(b, f.PropStart, f.ValueEnd, "\"mapData\": " + doc.ToJson(LineIndent(b, f.PropStart)));
    }

    /// <summary>pos 가 줄의 첫 비공백 위치면 그 들여쓰기 칸 수, 아니면 0.</summary>
    static int LineIndent(byte[] b, int pos)
    {
        int s = pos;
        while (s > 0 && b[s - 1] != '\n') s--;
        int n = 0;
        while (s + n < pos && b[s + n] == ' ') n++;
        return s + n == pos ? n : 0;
    }
}
