# 전술 관찰 5차 집계: python -I tools/tobs_agg.py docs/tactics_obs_raw5 V0 V2 V6 V7
import re, sys, glob, statistics as st

d = sys.argv[1]
vs = sys.argv[2:]


def num(m, i=1):
    return float(m.group(i)) if m else 0.0


KEYS = ('engN engSup bS bP bO bV mS mP mO tbS tbT tbO tmS tmT tmO covNum covDen baseNum baseDen '
        'lagN lagPre lagSum fireSum resN resCr artN artNear asDone asRet').split()


def parse(path):
    t = open(path, encoding='utf-8').read()
    r = {k: 0.0 for k in KEYS}
    h = re.search(r'# \S+ seed \d+: (\w+) at ([\d.]+)s', t)
    r['win'] = 1 if h.group(1) == 'won' else 0
    r['dur'] = float(h.group(2))
    r['lostK'] = num(re.search(r'faction 1 .*?lost (\d+)', t))
    r['lostE'] = num(re.search(r'faction 2 .*?lost (\d+)', t))
    for sec in t.split('## 세력')[1:]:
        m = re.search(r'교전 중 \(적 가시\) 중대-초 (\d+), 우세 \(.*?\) (\d+)', sec)
        r['engN'] += num(m)
        r['engSup'] += num(m, 2)
        m = re.search(r'약진 시작: (\d+)회.*?우세 (\d+)회.*?\(인내\) (\d+)회, 그 외 (\d+)회', sec)
        if m:
            r['bV'] += num(m); r['bS'] += num(m, 2); r['bP'] += num(m, 3); r['bO'] += num(m, 4)
        m = re.search(r'\[틱시작\] ([^\n]*)', sec)
        if m:
            for a, b, c in re.findall(r'(약진|기동조):(\S+?) (\d+)', m.group(1)):
                pre = 'tb' if a == '약진' else 'tm'
                suf = 'S' if b in ('신호', '수우세') else 'T' if b == '타이머' else 'O'
                r[pre + suf] += float(c)
        m = re.search(r'약진 중 \(적 가시\) 파트너 사격 비율 평균 ([\d.]+) \(표본 (\d+)초', sec)
        if m:
            r['covNum'] += num(m) * num(m, 2); r['covDen'] += num(m, 2)
        m = re.search(r'비약진 사격조 ([\d.]+) \(표본 (\d+)초', sec)
        if m:
            r['baseNum'] += num(m) * num(m, 2); r['baseDen'] += num(m, 2)
        m = re.search(r'\[시간차\] .*?표본 (\d+), 사격 전 출발 (\d+), .*?평균 ([\d.]+)s, 출발 때 화력조 사격 비율 평균 ([\d.]+)', sec)
        if m:
            n = num(m)
            r['lagN'] += n; r['lagPre'] += num(m, 2); r['lagSum'] += n * num(m, 3); r['fireSum'] += n * num(m, 4)
        m = re.search(r'\[예비\] 투입 (\d+)회, 그중 곤경 .*? (\d+)회', sec)
        if m:
            r['resN'] += num(m); r['resCr'] += num(m, 2)
        m = re.search(r'\[포병\] 돌격 (\d+)회 판정.*?12칸 안 (\d+)', sec)
        if m:
            r['artN'] += num(m); r['artNear'] += num(m, 2)
        m = re.search(r'\[돌격결과\] 완료 (\d+): 후퇴 (\d+)', sec)
        if m:
            r['asDone'] += num(m); r['asRet'] += num(m, 2)
    return r


def rat(a, b):
    return a / b if b else float('nan')


def S(R, k):
    return sum(x[k] for x in R)


def TB(R):
    return S(R, 'tbS') + S(R, 'tbT') + S(R, 'tbO')


def TM(R):
    return S(R, 'tmS') + S(R, 'tmT') + S(R, 'tmO')


M = [
    ('승 (15판 중)', lambda R: S(R, 'win')),
    ('평균 소요초', lambda R: st.mean(x['dur'] for x in R)),
    ('한국 사상', lambda R: S(R, 'lostK')),
    ('적 사상', lambda R: S(R, 'lostE')),
    ('사상 비율 K/E', lambda R: rat(S(R, 'lostK'), S(R, 'lostE'))),
    ('교전 중 신호 참 %', lambda R: 100 * rat(S(R, 'engSup'), S(R, 'engN'))),
    ('약진 시작 (틱) 수', TB),
    ('  신호 %', lambda R: 100 * rat(S(R, 'tbS'), TB(R))),
    ('  타이머 %', lambda R: 100 * rat(S(R, 'tbT'), TB(R))),
    ('  그 외 %', lambda R: 100 * rat(S(R, 'tbO'), TB(R))),
    ('기동조 출발 (틱) 수', TM),
    ('  신호 %', lambda R: 100 * rat(S(R, 'tmS'), TM(R))),
    ('약진 시작 (1초 표본) 신호 %', lambda R: 100 * rat(S(R, 'bS'), S(R, 'bV'))),
    ('약진 중 엄호 사격 비율', lambda R: rat(S(R, 'covNum'), S(R, 'covDen'))),
    ('비약진 사격조 기준', lambda R: rat(S(R, 'baseNum'), S(R, 'baseDen'))),
    ('화력->기동 시간차 평균 s', lambda R: rat(S(R, 'lagSum'), S(R, 'lagN'))),
    ('  표본 수', lambda R: S(R, 'lagN')),
    ('  사격 전 출발 %', lambda R: 100 * rat(S(R, 'lagPre'), S(R, 'lagN'))),
    ('  출발 때 화력조 사격 비율', lambda R: rat(S(R, 'fireSum'), S(R, 'lagN'))),
    ('예비 투입 수', lambda R: S(R, 'resN')),
    ('  곤경 때 투입 %', lambda R: 100 * rat(S(R, 'resCr'), S(R, 'resN'))),
    ('포병: 돌격 판정 수', lambda R: S(R, 'artN')),
    ('  조준점 돌격 표적 12칸 안 %', lambda R: 100 * rat(S(R, 'artNear'), S(R, 'artN'))),
    ('돌격 완료 수', lambda R: S(R, 'asDone')),
    ('  40초 내 후퇴 %', lambda R: 100 * rat(S(R, 'asRet'), S(R, 'asDone'))),
]

data = {v: {s: [parse(f) for f in sorted(glob.glob(f'{d}/{v}_[ABC]{s}.txt'))] for s in range(1, 6)} for v in vs}


def vals(v, fn):
    return [fn(data[v][s]) for s in range(1, 6)]


print('| 지표 | ' + ' | '.join(f'{v} 평균 (SD)' for v in vs) + ' |')
print('|---' * (len(vs) + 1) + '|')
for name, fn in M:
    base = vals(vs[0], fn)
    row = []
    for v in vs:
        x = vals(v, fn)
        mu = st.mean(x)
        cell = f'{mu:.2f} ({st.stdev(x):.2f})'
        if v != vs[0]:
            se = ((st.variance(x) + st.variance(base)) / 5) ** 0.5
            if se > 0 and abs(mu - st.mean(base)) > 2 * se:
                cell += ' *'
        row.append(cell)
    print(f'| {name} | ' + ' | '.join(row) + ' |')
print('\n(각 값 = 시드 1~5 각각의 시나리오 A+B+C 합 (또는 합 기반 비율) 의 5시드 평균 (표준편차). * = V0 대비 차이가 2 표준오차 초과)')
print('\n시나리오별 (5시드):')
for v in vs:
    out = []
    for sc in 'ABC':
        R = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_{sc}[1-5].txt'))]
        out.append(f'{sc}: {int(S(R, "win"))}/5승 {st.mean(x["dur"] for x in R):.0f}s K{int(S(R, "lostK"))}/E{int(S(R, "lostE"))}')
    print(v, ' | '.join(out))
