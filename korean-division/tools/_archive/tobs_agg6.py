# 6차 집계 (포병 · 대규모): python -I tools/tobs_agg6.py docs/tactics_obs_raw5 docs/tactics_obs_raw6
import re, sys, glob, statistics as st
sys.stdout.reconfigure(encoding="utf-8")

def parse(path):
    t = open(path, encoding='utf-8').read()
    r = dict(dur=float(re.search(r'at ([\d.]+)s', t).group(1)), win=1 if 'won at' in t.splitlines()[1] else 0,
             lostK=0, lostE=0, mv=0, artS=0, shoot=0, an=0, a12=0, aim=0, aimE=0, idle=0, idleNear=0, resN=0, resCr=0,
             lagN=0, lagSum=0, cov=0, covN=0, gun=0, tgt=0, move=0, trav=0, shots=0, nt=0, preInf=0, preCon=0, effN=0, effK=0,
             firstArt=[], firstInf=[], firstCon=[], types={})
    r['lostK'] = int(re.search(r'faction 1 .*?lost (\d+)', t).group(1)); r['lostE'] = int(re.search(r'faction 2 .*?lost (\d+)', t).group(1))
    for sec in t.split('## 세력')[1:]:
        g = lambda p: re.search(p, sec)
        m = g(r'포병 이동 (\d+)회'); r['mv'] += int(m.group(1)) if m else 0
        m = g(r'포병 (\d+) 포대-초.*?최근 3초 안 사격한 포대-초 (\d+)')
        if m: r['artS'] += int(m.group(1)); r['shoot'] += int(m.group(2))
        m = g(r'\[포병\] 돌격 (\d+)회 판정.*?12칸 안 (\d+); 포병 조준점 (\d+)건 중 교전 중 보병 표적 15칸 안 (\d+)')
        if m: r['an'] += int(m.group(1)); r['a12'] += int(m.group(2)); r['aim'] += int(m.group(3)); r['aimE'] += int(m.group(4))
        m = g(r'유휴 중대.*?: (\d+) 중대-초, 그중 적 45칸 안 (\d+)')
        if m: r['idle'] += int(m.group(1)); r['idleNear'] += int(m.group(2))
        m = g(r'\[예비\] 투입 (\d+)회, 그중 곤경 .*? (\d+)회')
        if m: r['resN'] += int(m.group(1)); r['resCr'] += int(m.group(2))
        m = g(r'\[시간차\] .*?표본 (\d+), .*?평균 ([\d.]+)s')
        if m: r['lagN'] += int(m.group(1)); r['lagSum'] += int(m.group(1)) * float(m.group(2))
        m = g(r'약진 중 \(적 가시\) 파트너 사격 비율 평균 ([\d.]+) \(표본 (\d+)초')
        if m: r['cov'] += float(m.group(1)) * int(m.group(2)); r['covN'] += int(m.group(2))
        m = g(r'\[포병2\] 포-초 (\d+), 표적 보유 (\d+) .*?이동 중 (\d+), 제자리 선회 (\d+), 사격 (\d+) .*?표적없는 사격 (\d+); 첫 포병 사격 t=(-?\d+), 첫 보병 사격 t=(-?\d+), 첫 접촉.*? t=(-?\d+), 보병 첫 사격 전 포병 사격 (\d+), 접촉 전 (\d+); 착탄 12초 뒤 평가 (\d+)건: 6칸 안 적 보병 사망 (\d+)')
        if m:
            v = [int(x) for x in m.groups()]
            r['gun'] += v[0]; r['tgt'] += v[1]; r['move'] += v[2]; r['trav'] += v[3]; r['shots'] += v[4]; r['nt'] += v[5]
            if v[6] >= 0: r['firstArt'].append(v[6])
            if v[7] >= 0: r['firstInf'].append(v[7])
            if v[8] >= 0: r['firstCon'].append(v[8])
            r['preInf'] += v[9]; r['preCon'] += v[10]; r['effN'] += v[11]; r['effK'] += v[12]
        for k, n, d, far, un in re.findall(r'\[포종\] (\w+): 사격 (\d+), 평균 거리 ([\d.]+), 사거리 80% 초과 (\d+), 관측 안 되는 표적 (\d+)', sec):
            a = r['types'].setdefault(k, [0, 0.0, 0, 0]); a[0] += int(n); a[1] += int(n) * float(d); a[2] += int(far); a[3] += int(un)
    return r

def rat(a, b): return a / b if b else float('nan')
def avg(l): return sum(l) / len(l) if l else float('nan')

def show(label, R, new):
    n = len(R); S = lambda k: sum(x[k] for x in R)
    print(f'--- {label} (n={n}) ---')
    print(f'승 {S("win")}/{n}, 평균 소요 {avg([x["dur"] for x in R]):.0f}s, 사상 K {S("lostK")} E {S("lostE")} (K/E {rat(S("lostK"), S("lostE")):.2f})')
    print(f'포병 이동 {S("mv")}회 (런당 {S("mv")/n:.1f}), 런당 이동 간격 {rat(sum(x["dur"] for x in R), S("mv")):.0f}s')
    print(f'포대-초 중 3초 안 사격 {100*rat(S("shoot"), S("artS")):.1f}% ({S("shoot")}/{S("artS")}), 돌격 판정 {S("an")}, 조준점 돌격 표적 12칸 안 {100*rat(S("a12"), S("an")):.0f}%, 조준점 {S("aim")}건 중 교전 중 보병 15칸 안 {100*rat(S("aimE"), S("aim")):.0f}%')
    print(f'유휴 중대-초 {S("idle")} (적 45칸 안 {S("idleNear")}), 예비 투입 {S("resN")} (곤경 {100*rat(S("resCr"), S("resN")):.0f}%), 시간차 평균 {rat(S("lagSum"), S("lagN")):.0f}s (n {S("lagN")}), 엄호 {rat(S("cov"), S("covN")):.2f}')
    if new:
        print(f'[6차] 포-초 {S("gun")}, 표적 보유 {100*rat(S("tgt"), S("gun")):.0f}%, 이동 {100*rat(S("move"), S("gun")):.0f}%, 선회 {S("trav")} (런당 {S("trav")/n:.0f}), 사격 {S("shots")} (런당 {S("shots")/n:.0f}), 표적 보유 중 사격률 {100*rat(S("shots"), S("tgt")):.1f}%/포-초')
        print(f'  첫 포병 사격 평균 t={avg([sum(x["firstArt"])/len(x["firstArt"]) for x in R if x["firstArt"]]):.0f}, 첫 보병 사격 t={avg([sum(x["firstInf"])/len(x["firstInf"]) for x in R if x["firstInf"]]):.0f}, 첫 접촉 t={avg([sum(x["firstCon"])/len(x["firstCon"]) for x in R if x["firstCon"]]):.0f}; 보병 사격 전 포병 사격 {S("preInf")} / 접촉 전 {S("preCon")} (전체 {S("shots")})')
        print(f'  착탄 평가 {S("effN")}건, 6칸 안 적 보병 사망 합 {S("effK")} (발당 {rat(S("effK"), S("effN")):.1f}, 소총 사상 섞임: 상한)')
        T = {}
        for x in R:
            for k, a in x['types'].items():
                b = T.setdefault(k, [0, 0.0, 0, 0]); b[0] += a[0]; b[1] += a[1]; b[2] += a[2]; b[3] += a[3]
        for k, a in sorted(T.items()):
            print(f'  {k}: 사격 {a[0]} (런당 {a[0]/n:.1f}), 평균 거리 {rat(a[1], a[0]):.1f}, 사거리 80% 초과 {100*rat(a[2], a[0]):.0f}%, 관측 밖 {a[3]}')

old, new = sys.argv[1], sys.argv[2]
for sc in 'ABC':
    show(f'5차 V0 {sc}', [parse(f) for f in sorted(glob.glob(f'{old}/V0_{sc}[1-5].txt'))], False)
    show(f'6차 V0 {sc}', [parse(f) for f in sorted(glob.glob(f'{new}/V0_{sc}[1-5].txt'))], True)
show('6차 D (2000 대 2000)', [parse(f) for f in sorted(glob.glob(f'{new}/V0_D[1-3].txt'))], True)
for sc in 'ABC':
    pass
allo = [parse(f) for sc in 'ABC' for f in sorted(glob.glob(f'{old}/V0_{sc}[1-5].txt'))]
alln = [parse(f) for sc in 'ABC' for f in sorted(glob.glob(f'{new}/V0_{sc}[1-5].txt'))]
show('5차 V0 A+B+C 전체', allo, False); show('6차 V0 A+B+C 전체', alln, True)
