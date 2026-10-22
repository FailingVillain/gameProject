#!/usr/bin/env python3
"""[TI1] `public const string X = "한글";` -> `public static string X => KoreanDivision.Core.Loc.T("<Class>.X", "한글");`
멱등 (이미 변환된 줄은 그대로). 문자열 보간($"...")·연결(+)·다른 상수 참조는 건드리지 않고 목록으로 보고한다.
사용: python tools/i18n/convert_consts.py src/UI/StringsX.cs [...]   (--check: 파일을 쓰지 않고 미변환 const 수만 센다)
"""
import re, sys

LIT = '"(?:[^"' + chr(92) + chr(92) + ']|' + chr(92) + chr(92) + '.)*"'   # "(?:[^"\\]|\\.)*"
DECL = re.compile(r'^(\s*)public const string\s+(.*?;)(\s*(?://.*)?)$')
ONE = re.compile(r'\s*([A-Za-z_]\w*)\s*=\s*(' + LIT + r')\s*(,|;)')
CLS = re.compile(r'\b(?:class|struct)\s+(\w+)')

def convert_file(path, write=True):
    with open(path, encoding='utf-8-sig', newline='') as f:
        raw = f.read()
    bom = open(path, 'rb').read(3) == b'\xef\xbb\xbf'
    nl = '\r\n' if '\r\n' in raw else '\n'
    cls = None
    out, skipped, n = [], [], 0
    for line in raw.split(nl):
        m = CLS.search(line)
        if m and 'const' not in line: cls = m.group(1)
        d = DECL.match(line)
        if d and cls:
            indent, body, tail = d.groups()
            items, pos, ok = [], 0, True
            while pos < len(body):
                mm = ONE.match(body, pos)
                if not mm: ok = False; break
                items.append((mm.group(1), mm.group(2))); pos = mm.end()
                if mm.group(3) == ';': break
            if ok and pos == len(body) and items:
                out.append(indent + ' '.join(f'public static string {k} => KoreanDivision.Core.Loc.T("{cls}.{k}", {v});' for k, v in items) + tail)
                n += len(items); continue
            skipped.append(line.strip())
        out.append(line)
    if write and n:
        with open(path, 'w', encoding='utf-8-sig' if bom else 'utf-8', newline='') as f:
            f.write(nl.join(out))
    return n, skipped

if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    for p in args:
        n, sk = convert_file(p, '--check' not in sys.argv)
        print(f'{p}: 변환 {n}, 건너뜀 {len(sk)}')
        for s in sk: print('  skip: ' + s[:100])
