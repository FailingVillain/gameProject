# 전술 관찰 11차 (대열 정비 2) 집계: python -I tools/tobs_close.py <dir> NOCLOSE G2
# 변형마다 A·B·C × 시드 1~5 합: 승 · K/E · 돌격 후퇴 · 대열 정비 계수 · 대열 지표 · 봉천 돌격 결과 (C)
import re, sys, glob, os, statistics as st
# parse() = tools/tobs_agg.py 와 같은 식 (그 파일은 import 하면 바로 집계를 돌림)
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



d = sys.argv[1]
vs = sys.argv[2:]


def f(m, i=1):
    return float(m.group(i)) if m else 0.0


def extra(path):
    t = open(path, encoding='utf-8').read()
    r = {}
    m = re.search(r'\[대열 정비\] 장교 명령 (\d+), 플레이어 명령 (\d+), 슬롯으로 보낸 병사 (\d+), 교전 중이라 미룸 (\d+), 빈자리 점검 재배치 (\d+)'
                  r'(?:, 공격 이동 그대로 보냄 (\d+), 돌격 감지 판단 (\d+), 돌격 감지 명령 (\d+))?', t)
    for i, k in enumerate(['ord', 'man', 'mov', 'late', 'gap', 'atk', 'chs', 'cho'], 1):
        r[k] = f(m, i) if m and m.group(i) else 0.0
    secs = t.split('## 세력')[1:]
    for side, sec in zip(('K', 'E'), secs):
        m = re.search(r'\[대열\] 교전 중 정지 중대-초 (\d+): .*?평균 ([\d.]+) 중앙 [\d.]+, 앞줄 빈 슬롯 평균 ([\d.]+), 앞줄 연속 빈 슬롯 평균 ([\d.]+), 장교 경보 켜진 중대-초 (\d+)', sec)
        n = f(m)
        r[side + 'n'] = n; r[side + 'disp'] = n * f(m, 2); r[side + 'fe'] = n * f(m, 3); r[side + 'fg'] = n * f(m, 4); r[side + 'al'] = f(m, 5)
        m = re.search(r'\[대열2\] 앞줄 슬롯 평균 ([\d.]+).*?\| 구멍 평균 ([\d.]+).*?\| 연속 구멍 평균 ([\d.]+).*?\| 뒤처짐 평균 ([\d.]+).*?판단 문턱 넘음 (\d+)/(\d+)', sec)
        m2 = f(m, 6)
        r[side + 'n2'] = m2; r[side + 'slots'] = m2 * f(m, 1); r[side + 'holes'] = m2 * f(m, 2); r[side + 'hg'] = m2 * f(m, 3); r[side + 'beh'] = m2 * f(m, 4); r[side + 'brk'] = f(m, 5)
        m = re.search(r'\[돌격결과\] 완료 (\d+): 후퇴 (\d+) 우세 (\d+) 열세 (\d+)', sec)
        r[side + 'asN'] = f(m); r[side + 'asRet'] = f(m, 2); r[side + 'asWin'] = f(m, 3)
        fl = el = 0
        for a, b in re.findall(r'돌격결과: \S+ \S+ 아군손실 (\d+) 적손실\(반경20\) (\d+)', sec):
            fl += int(a); el += int(b)
        r[side + 'asFL'] = fl; r[side + 'asEL'] = el
    return r


def rat(a, b):
    return a / b if b else float('nan')


for v in vs:
    files = sorted(glob.glob(f'{d}/{v}_[ABC][1-5].txt'))
    P = [parse(x) for x in files]
    X = [extra(x) for x in files]
    S = lambda k, L=X: sum(x[k] for x in L)
    win = sum(p['win'] for p in P)
    lk = sum(p['lostK'] for p in P); le = sum(p['lostE'] for p in P)
    asd = sum(p['asDone'] for p in P); asr = sum(p['asRet'] for p in P)
    orders = S('ord') + S('man')
    print(f'== {v} ({len(files)}판)')
    print(f'  승 {win:.0f}/15, K/E {rat(lk, le):.3f} (K {lk:.0f} / E {le:.0f}), 돌격 완료 {asd:.0f} / 40초 내 후퇴 {100 * rat(asr, asd):.0f}%')
    print(f'  대열 정비 명령 {orders:.0f} (판당 {orders / max(1, len(files)):.1f}), 보낸 병사 {S("mov"):.0f} (명령당 {rat(S("mov"), orders):.1f}), 공격 이동 그대로 {S("atk"):.0f}, 미룸 {S("late"):.0f}, 돌격 감지 판단 {S("chs"):.0f} / 명령 {S("cho"):.0f}, 빈자리 점검 {S("gap"):.0f}')
    for side, name in (('K', '한국'), ('E', '적')):
        n, n2 = S(side + 'n'), S(side + 'n2')
        print(f'  [{name}] 교전 중 정지 중대-초 {n:.0f}: 벗어난 비율 {rat(S(side + "disp"), n):.3f}, 옛 앞줄 빈 슬롯 {rat(S(side + "fe"), n):.2f} / 연속 {rat(S(side + "fg"), n):.2f}, '
              f'앞줄 슬롯 {rat(S(side + "slots"), n2):.1f}, 구멍 {rat(S(side + "holes"), n2):.2f}, 연속 구멍 {rat(S(side + "hg"), n2):.2f}, 뒤처짐 {rat(S(side + "beh"), n2):.2f}, '
              f'문턱 넘음 {100 * rat(S(side + "brk"), n2):.0f}%, 경보 중대-초 {S(side + "al"):.0f}')
    print(f'  [돌격결과 세력별] 한국 완료 {S("KasN"):.0f} 후퇴 {S("KasRet"):.0f} ({100 * rat(S("KasRet"), S("KasN")):.0f}%) 우세 {S("KasWin"):.0f} · 적 완료 {S("EasN"):.0f} 후퇴 {S("EasRet"):.0f} ({100 * rat(S("EasRet"), S("EasN")):.0f}%) 우세 {S("EasWin"):.0f}')
    C = [x for x, fn in zip(X, files) if os.path.basename(fn).split('_')[-1].startswith('C')]
    PC = [p for p, fn in zip(P, files) if os.path.basename(fn).split('_')[-1].startswith('C')]
    Sc = lambda k: sum(x[k] for x in C)
    print(f'  [C 봉천 돌격] 완료 {Sc("EasN"):.0f}: 후퇴 {Sc("EasRet"):.0f} 우세 {Sc("EasWin"):.0f}, 돌격한 봉천 손실 {Sc("EasFL"):.0f} vs 한국 손실 (반경20) {Sc("EasEL"):.0f} '
          f'(교환비 {rat(Sc("EasEL"), Sc("EasFL")):.2f}), C 승 {sum(p["win"] for p in PC):.0f}/5, C K/E {rat(sum(p["lostK"] for p in PC), sum(p["lostE"] for p in PC)):.3f}')
    for sc in 'ABC':
        R = [p for p, fn in zip(P, files) if os.path.basename(fn).split('_')[-1].startswith(sc)]
        print(f'    {sc}: {sum(p["win"] for p in R):.0f}/5승 K{sum(p["lostK"] for p in R):.0f}/E{sum(p["lostE"] for p in R):.0f}')
