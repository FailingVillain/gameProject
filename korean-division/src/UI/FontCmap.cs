namespace KoreanDivision.UI;

/// <summary>TTF 의 cmap (유니코드 -> 글리프) 에 어떤 코드포인트가 있는지만 읽는다 (형식 4 · 12). 창 없이 글꼴 커버리지 셀프테스트용 (T38k).</summary>
public sealed class FontCmap
{
    readonly List<(int Lo, int Hi)> ranges = new();

    public int Count { get; private set; }
    public bool Contains(int cp)
    {
        foreach (var (lo, hi) in ranges) if (cp >= lo && cp <= hi) return true;
        return false;
    }

    static int U16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
    static long U32(byte[] b, int o) => ((long)b[o] << 24) | ((long)b[o + 1] << 16) | ((long)b[o + 2] << 8) | b[o + 3];

    public static FontCmap Load(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int nTables = U16(b, 4), cmapOff = -1;
        for (int i = 0; i < nTables; i++)
        {
            int rec = 12 + i * 16;
            if (b[rec] == 'c' && b[rec + 1] == 'm' && b[rec + 2] == 'a' && b[rec + 3] == 'p') cmapOff = (int)U32(b, rec + 8);
        }
        if (cmapOff < 0) throw new InvalidDataException("cmap 표 없음: " + path);
        var cm = new FontCmap();
        int nSub = U16(b, cmapOff + 2);
        for (int i = 0; i < nSub; i++)
        {
            int rec = cmapOff + 4 + i * 8;
            int platform = U16(b, rec), enc = U16(b, rec + 2);
            bool unicode = platform == 0 || (platform == 3 && (enc == 1 || enc == 10));
            if (!unicode) continue;
            int sub = cmapOff + (int)U32(b, rec + 4);
            int fmt = U16(b, sub);
            if (fmt == 4)
            {
                int segX2 = U16(b, sub + 6), n = segX2 / 2;
                int endO = sub + 14, startO = endO + segX2 + 2, deltaO = startO + segX2, rangeO = deltaO + segX2;
                for (int s = 0; s < n; s++)
                {
                    int end = U16(b, endO + 2 * s), start = U16(b, startO + 2 * s), ro = U16(b, rangeO + 2 * s);
                    int delta = (short)U16(b, deltaO + 2 * s);
                    if (start == 0xFFFF) continue;
                    // 글리프 0 (없음) 으로 매핑되는 칸은 제외
                    int runStart = -1;
                    for (int c = start; c <= end; c++)
                    {
                        int g;
                        if (ro == 0) g = (c + delta) & 0xFFFF;
                        else
                        {
                            int go = rangeO + 2 * s + ro + 2 * (c - start);
                            g = go + 1 < b.Length ? U16(b, go) : 0;
                            if (g != 0) g = (g + delta) & 0xFFFF;
                        }
                        if (g != 0) { if (runStart < 0) runStart = c; }
                        else if (runStart >= 0) { cm.ranges.Add((runStart, c - 1)); runStart = -1; }
                    }
                    if (runStart >= 0) cm.ranges.Add((runStart, end));
                }
            }
            else if (fmt == 12)
            {
                long groups = U32(b, sub + 12);
                for (long g = 0; g < groups; g++)
                {
                    int o = sub + 16 + (int)g * 12;
                    long s = U32(b, o), e = U32(b, o + 4);
                    if (U32(b, o + 8) != 0) cm.ranges.Add(((int)s, (int)e));
                }
            }
        }
        foreach (var (lo, hi) in cm.ranges) cm.Count += hi - lo + 1;
        return cm;
    }
}
