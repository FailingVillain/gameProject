using KoreanDivision.Core;

namespace KoreanDivision;

/// <summary>--faction 옵션 해석: 세력 번호(1부터) 또는 지도가 정의한 id (korean · japanese · fengtian · russian · soviet, 대소문자 무시) 또는 한글 표시 이름.</summary>
public static class FactionChoice
{
    public static int Parse(string arg, IReadOnlyList<FactionDef> defs, int count)
    {
        arg = arg.Trim();
        if (int.TryParse(arg, out int n))
        {
            if (n < 1 || n > count) throw new ArgumentException($"세력 번호 {n} 는 1~{count} 이어야 함");
            return n;
        }
        for (int i = 0; i < defs.Count && i < count; i++)
            if (string.Equals(defs[i].En, arg, StringComparison.OrdinalIgnoreCase) || string.Equals(defs[i].Name, arg, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        string names = defs.Count > 0 ? string.Join(", ", defs.Select(d => d.En)) : "이 지도는 세력 이름이 없음 — 번호를 쓰세요";
        throw new ArgumentException($"알 수 없는 세력 '{arg}' (가능: 1~{count}, {names})");
    }

    /// <summary>옵션의 지도(고정 지도면 파일, 아니면 생성 맵)로 --faction 값을 해석해 opt.Faction 에 넣는다.</summary>
    public static void Resolve(GameOptions opt, string arg)
    {
        var mf = opt.MapId != null ? MapFile.Peek(opt.MapId) : null;
        opt.Faction = Parse(arg, mf?.Factions ?? new List<FactionDef>(), mf?.Starts.Count ?? opt.Players);
    }
}
