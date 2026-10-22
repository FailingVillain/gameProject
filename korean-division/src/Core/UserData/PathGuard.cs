using System.Text.RegularExpressions;

namespace KoreanDivision.Core;

/// <summary>T51a: id 로 파일 경로를 만들 때의 경로 탈출 방지. id 는 ^[A-Za-z0-9_]{1,64}$ 만, 윈도우 예약 이름 거부,
/// 합친 전체 경로가 폴더 아래여야 한다. 동봉 미션 · 지도 id 는 모두 통과 (동작 불변).</summary>
public static class PathGuard
{
    public const int MaxIdLength = 64;
    static readonly Regex IdRx = new(@"^[A-Za-z0-9_]{1,64}\z", RegexOptions.CultureInvariant);
    static readonly Regex ReservedRx = new(@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>존재할 수 없는 파일 이름 ('?' 는 윈도우 파일 이름에 못 쓴다) — 잘못된 id 의 PathOf 결과. File.Exists 는 false, 읽기는 실패.</summary>
    public const string InvalidFileName = "?invalid-id?.json";

    /// <summary>id 형식만 검사 (한국어 사유). 통과하면 reason 은 빈 문자열.</summary>
    public static bool IsSafeId(string? id, out string reason)
    {
        if (string.IsNullOrEmpty(id)) { reason = "id 가 비어 있습니다"; return false; }
        if (id.Length > MaxIdLength) { reason = $"id 가 너무 깁니다 ({id.Length} > {MaxIdLength})"; return false; }
        if (!IdRx.IsMatch(id)) { reason = "id 에는 영문 · 숫자 · 밑줄(_) 만 쓸 수 있습니다 (경로 문자 · 점 · 공백 금지)"; return false; }
        if (ReservedRx.IsMatch(id)) { reason = $"id '{id}' 는 윈도우 예약 이름입니다"; return false; }
        reason = "";
        return true;
    }

    /// <summary>folder/id.json 을 만든다. 잘못된 id 거나 결과가 folder 밖이면 false.</summary>
    public static bool TryCombine(string folder, string? id, out string path, out string reason)
    {
        path = "";
        if (!IsSafeId(id, out reason)) return false;
        string full = Path.GetFullPath(Path.Combine(folder, id + ".json"));
        string root = Path.GetFullPath(folder);
        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetDirectoryName(full) + Path.DirectorySeparatorChar, root, StringComparison.OrdinalIgnoreCase))
        { reason = "경로가 폴더 밖으로 나갑니다"; return false; }
        path = Path.Combine(folder, id + ".json");   // 정상 id 의 결과는 예전 Path.Combine 과 문자 그대로 같다
        return true;
    }

    /// <summary>PathOf 용: 안전하면 정상 경로, 아니면 존재할 수 없는 경로 (예외 없음 — File.Exists 호출부가 안 터진다).</summary>
    public static string Resolve(string folder, string? id) =>
        TryCombine(folder, id, out var p, out _) ? p : Path.Combine(folder, InvalidFileName);
}
