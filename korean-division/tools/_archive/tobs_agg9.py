# 전술 관찰 9차 집계 (결정 4 · 12 + 부사관): python -I tools/tobs_agg9.py docs/tactics_obs_raw8:V0 docs/tactics_obs_raw9:V9 docs/tactics_obs_raw9:NoGate ...
# 각 인자 = 폴더:접두어 (파일 <접두어>_<A|B|C><시드 1~5>.txt). 첫 인자가 기준 (* = 기준 대비 2 표준오차 초과).
import re, sys, glob, statistics as st

SETS = [a.rsplit(':', 1) for a in sys.argv[1:]]


def num(m, i=1):
    return float(m.group(i)) if m else 0.0


def parse(path):
    t = open(path, encoding='utf-8').read()
    r = dict(win=0, dur=0, lostK=0, lostE=0, asDone=0, asRet=0, asN=0, asSup=0, asN1=0, asSup1=0,
             gSig=0, gOw=0, gFb=0, gBlk=0, man1=0, bnd1=0, none1=0, none2=0, nco1=0, off1=0, engN=0, engSup=0,
             covNum=0, covDen=0, bS=0, bT=0, bO=0)
    h = re.search(r'# \S+ seed \d+: (\w+) at ([\d.]+)s', t)
    r['win'] = 1 if h.group(1) == 'won' else 0
    r['dur'] = float(h.group(2))
    r['lostK'] = num(re.search(r'faction 1 .*?lost (\d+)', t))
    r['lostE'] = num(re.search(r'faction 2 .*?lost (\d+)', t))
    m = re.search(r'\[돌격허가\] 신호 (\d+) 엄호 (\d+) 예비타이머 (\d+) 막힘\(판단수\) (\d+)', t)
    if m:
        r['gSig'], r['gOw'], r['gFb'], r['gBlk'] = (num(m, i) for i in range(1, 5))
    for sec in t.split('## 세력')[1:]:
        fac = int(sec.split()[0])
        m = re.search(r'\[돌격결과\] 완료 (\d+): 후퇴 (\d+)', sec)
        if m:
            r['asDone'] += num(m); r['asRet'] += num(m, 2)
        for kind in ('Drill 돌격\\(Man4\\)', '상태 돌격\\(Assault/Charge\\)'):
            m = re.search(r'  ' + kind + r': (\d+)회, 평균 적 제압 [\d.]+, 우세 (\d+)회', sec)
            if m:
                r['asN'] += num(m); r['asSup'] += num(m, 2)
                if fac == 1:
                    r['asN1'] += num(m); r['asSup1'] += num(m, 2)
        m = re.search(r'지휘 능력\(중대-초\): 장교 (\d+) / 부사관 대리 (\d+) / 없음 (\d+)', sec)
        if m:
            r['none%d' % fac] += num(m, 3)
            if fac == 1:
                r['off1'] += num(m); r['nco1'] += num(m, 2)
        m = re.search(r'교전 중 \(적 가시\) 중대-초 (\d+), 우세 \(.*?\) (\d+)', sec)
        r['engN'] += num(m); r['engSup'] += num(m, 2)
        m = re.search(r'약진 중 \(적 가시\) 파트너 사격 비율 평균 ([\d.]+) \(표본 (\d+)초', sec)
        if m:
            r['covNum'] += num(m) * num(m, 2); r['covDen'] += num(m, 2)
        m = re.search(r'\[틱시작\] ([^\n]*)', sec)
        if m:
            for a, b, c in re.findall(r'(약진|기동조):(\S+?) (\d+)', m.group(1)):
                if a == '기동조' and fac == 1:
                    r['man1'] += float(c)
                if a == '약진':
                    if fac == 1: r['bnd1'] += float(c)
                    r['bS' if b in ('신호', '수우세') else 'bT' if b == '타이머' else 'bO'] += float(c)
    return r


def rat(a, b):
    return a / b if b else float('nan')


def S(R, k):
    return sum(x[k] for x in R)


M = [
    ('한국군 승 (시드당 A·B·C 3판)', lambda R: S(R, 'win')),
    ('평균 소요초', lambda R: st.mean(x['dur'] for x in R)),
    ('사상 비율 K/E', lambda R: rat(S(R, 'lostK'), S(R, 'lostE'))),
    ('교전 중 신호 참 %', lambda R: 100 * rat(S(R, 'engSup'), S(R, 'engN'))),
    ('약진 시작 수', lambda R: S(R, 'bS') + S(R, 'bT') + S(R, 'bO')),
    ('  신호 %', lambda R: 100 * rat(S(R, 'bS'), S(R, 'bS') + S(R, 'bT') + S(R, 'bO'))),
    ('약진 중 엄호 사격 비율', lambda R: rat(S(R, 'covNum'), S(R, 'covDen'))),
    ('돌격 개시 (1초 표본, 양측)', lambda R: S(R, 'asN')),
    ('  시작 때 신호 참 %', lambda R: 100 * rat(S(R, 'asSup'), S(R, 'asN'))),
    ('  그중 한국군 (공격 위주) 수', lambda R: S(R, 'asN1')),
    ('  한국군 신호 참 %', lambda R: 100 * rat(S(R, 'asSup1'), S(R, 'asN1'))),
    ('돌격 허가 (엔진 · 양측): 신호', lambda R: S(R, 'gSig')),
    ('  엄호', lambda R: S(R, 'gOw')),
    ('  예비 타이머', lambda R: S(R, 'gFb')),
    ('  신호 · 엄호 %', lambda R: 100 * rat(S(R, 'gSig') + S(R, 'gOw'), S(R, 'gSig') + S(R, 'gOw') + S(R, 'gFb'))),
    ('  막힌 판단 수', lambda R: S(R, 'gBlk')),
    ('돌격 완료 수', lambda R: S(R, 'asDone')),
    ('  40초 내 후퇴 %', lambda R: 100 * rat(S(R, 'asRet'), S(R, 'asDone'))),
    ('한국군 기동조 출발 수', lambda R: S(R, 'man1')),
    ('한국군 약진 시작 수', lambda R: S(R, 'bnd1')),
    ('지휘 없음 중대-초 (한국군)', lambda R: S(R, 'none1')),
    ('지휘 없음 중대-초 (봉천군)', lambda R: S(R, 'none2')),
    ('부사관 대리 중대-초 (한국군)', lambda R: S(R, 'nco1')),
]

data = []
for d, p in SETS:
    data.append({s: [parse(f) for f in sorted(glob.glob(f'{d}/{p}_[ABC]{s}.txt'))] for s in range(1, 6)})


def vals(i, fn):
    return [fn(data[i][s]) for s in range(1, 6) if data[i][s]]


print('| 지표 | ' + ' | '.join(f'{p} 평균 (SD)' for _, p in SETS) + ' |')
print('|---' * (len(SETS) + 1) + '|')
for name, fn in M:
    base = vals(0, fn)
    row = []
    for i in range(len(SETS)):
        x = vals(i, fn)
        if len(x) < 2 or any(v != v for v in x):
            row.append('—' if not x else f'{st.mean([v for v in x if v == v] or [float("nan")]):.2f}')
            continue
        mu = st.mean(x)
        cell = f'{mu:.2f} ({st.stdev(x):.2f})'
        if i > 0 and len(base) == len(x) and not any(v != v for v in base):
            se = ((st.variance(x) + st.variance(base)) / len(x)) ** 0.5
            if se > 0 and abs(mu - st.mean(base)) > 2 * se:
                cell += ' *'
        row.append(cell)
    print(f'| {name} | ' + ' | '.join(row) + ' |')
print('\n(각 값 = 시드 1~5 각각의 A+B+C 합 (또는 합 기반 비율) 의 5시드 평균 (표준편차). * = 첫 열 대비 2 표준오차 초과)')
print('\n시나리오별 (5시드):')
for (d, p) in SETS:
    out = []
    for sc in 'ABC':
        R = [parse(f) for f in sorted(glob.glob(f'{d}/{p}_{sc}[1-5].txt'))]
        if not R:
            continue
        out.append(f'{sc}: {int(S(R, "win"))}/5승 {st.mean(x["dur"] for x in R):.0f}s K{int(S(R, "lostK"))}/E{int(S(R, "lostE"))} '
                   f'한국 기동조 {int(S(R, "man1"))} 돌격 {int(S(R, "asDone"))} 후퇴 {int(S(R, "asRet"))} 지휘없음(한) {int(S(R, "none1"))}')
    print(p, ' | '.join(out))
