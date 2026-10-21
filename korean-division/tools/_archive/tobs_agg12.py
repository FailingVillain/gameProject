# 12차 집계 (추격 · 기병 측면): python -I tools/tobs_agg12.py docs/tactics_obs_raw12 M OLD P
import re, sys, glob, statistics as st
sys.stdout.reconfigure(encoding="utf-8")
d = sys.argv[1]
MIN_RIDERS = 8


def parse(p):
    t = open(p, encoding='utf-8').read()
    h = re.search(r'# \S+ seed \d+: (\w+) at ([\d.]+)s', t)
    f = {int(a): (int(b), int(c), int(e)) for a, _, b, c, e in re.findall(r'faction (\d) (.*?): spawned (\d+), lost (\d+), alive (\d+)', t)}
    won = h.group(1) == 'won'
    loser = 2 if won else 1
    sp, lost, alive = f[loser]
    r = dict(win=won, dur=float(h.group(2)), K=f[1][1], E=f[2][1], loserPct=100 * lost / sp)
    # 추격 국면 = 한쪽 50% 미만 → 끝 (생존 곡선 30초 표본에서 근사: [추격] 줄이 있으면 그것)
    m = re.search(r'\[추격\] 국면 시작 t50=(-?\d+) 길이 (\d+)s, 사살 사격 (\d+) 근접 (\d+), 추격 국면 사살 사격 (\d+) 근접 (\d+), 후퇴 중 희생 사격 (\d+) 근접 (\d+), 후퇴하는 적 돌격 보병 (\d+) 기병 (\d+), 막은 판단 (\d+), AI 추격 (\d+)/(\d+) 한계 정지 (\d+)/(\d+)', t)
    if m:
        v = list(map(int, m.groups()))
        r.update(t50=v[0], plen=v[1], kf=v[2], km=v[3], pkf=v[4], pkm=v[5], ffk=v[6], fmk=v[7], infF=v[8], cavF=v[9], blocked=v[10], pursuits=v[11] + v[12], halts=v[13] + v[14])
    m = re.search(r'\[기병AI\] 돌격 정면 (\d+) 측면 (\d+) 후방 (\d+), 측면 경유 (\d+), 소규모 보류 (\d+), 교착 물러남 (\d+)', t)
    if m:
        v = list(map(int, m.groups()))
        r.update(cfront=v[0], cflank=v[1], crear=v[2], croute=v[3], cheld=v[4], cscreen=v[5])
    # 생존 곡선 (30초 표본) 으로 추격 국면 근사 (M = [추격] 줄 없는 실행 파일)
    curves = re.findall(r'생존 곡선\(30초\): (.*)', t)
    t50 = None
    for fi, cl in enumerate(curves, start=1):
        for a, b in re.findall(r'([\d.]+):(\d+)', cl):
            if fi in f and int(b) * 2 < f[fi][0]:
                t50 = float(a) if t50 is None else min(t50, float(a)); break
    r['plen30'] = max(0.0, r['dur'] - t50) if t50 is not None else 0.0
    cav = re.findall(r'기병 돌격 #\d+(?: \(기수 (\d+)\))?: 반경 6 적 보병 중 제압>=0.25 (\d+)기 -> 8초 안 최대 (\d+)기', t)
    r['cavN'] = len(cav)
    r['cavEff'] = [int(c) - int(b) for _, b, c in cav]
    r['cavSmall'] = sum(1 for n, _, _ in cav if n and int(n) < MIN_RIDERS)
    r['cavRid'] = sum(1 for n, _, _ in cav if n)
    return r


def mean(xs):
    xs = list(xs)
    return st.mean(xs) if xs else float('nan')


for v in sys.argv[2:]:
    R = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_[ABC][1-5].txt'))]
    if not R:
        continue
    has = 'plen' in R[0]
    print(f"{v}: n={len(R)} 승 {sum(r['win'] for r in R)} K/E {sum(r['K'] for r in R)/max(1,sum(r['E'] for r in R)):.2f} 평균초 {mean(r['dur'] for r in R):.0f} 패자손실% {mean(r['loserPct'] for r in R):.0f}")
    print(f"   추격 국면 (생존 곡선 30초 표본 근사) 평균 {mean(r['plen30'] for r in R):.0f}s")
    if has:
        print(f"   추격 국면 길이 평균 {mean(r['plen'] for r in R):.0f}s (중앙 {st.median(r['plen'] for r in R):.0f}), 추격 국면 사살/판 사격 {mean(r['pkf'] for r in R):.1f} 근접 {mean(r['pkm'] for r in R):.1f}, "
              f"후퇴 중 희생/판 사격 {mean(r['ffk'] for r in R):.1f} 근접 {mean(r['fmk'] for r in R):.1f}, 전체 사살 근접 비율 {100*sum(r['km'] for r in R)/max(1,sum(r['km']+r['kf'] for r in R)):.1f}%")
        print(f"   후퇴하는 적에게 돌격 보병 {sum(r['infF'] for r in R)} 기병 {sum(r['cavF'] for r in R)} (15판 합), 막은 판단 {sum(r['blocked'] for r in R)}, AI 추격 {sum(r['pursuits'] for r in R)} 한계 정지 {sum(r['halts'] for r in R)}")
    if 'cfront' in R[0]:
        fr, fl, re_ = sum(r['cfront'] for r in R), sum(r['cflank'] for r in R), sum(r['crear'] for r in R)
        tot = max(1, fr + fl + re_)
        print(f"   기병 돌격 (AI 계수) 정면 {fr} 측면 {fl} 후방 {re_} → 측면+후방 {100*(fl+re_)/tot:.0f}%, 측면 경유 {sum(r['croute'] for r in R)}, 소규모 보류 {sum(r['cheld'] for r in R)}, 교착 물러남 {sum(r['cscreen'] for r in R)}")
    eff = [x for r in R for x in r['cavEff']]
    print(f"   기병 돌격 (관찰) {sum(r['cavN'] for r in R)}회, 효과 (반경 6 제압>=0.25 적 증가) 회당 {mean(eff):+.2f}, 소규모 (<{MIN_RIDERS}기) 돌격 {sum(r['cavSmall'] for r in R)} (기수 기록 {sum(r['cavRid'] for r in R)})")
    for sc in 'ABC':
        S = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_{sc}[1-5].txt'))]
        if not S:
            continue
        extra = f" 추격 {mean(r['plen'] for r in S):.0f}s" if has else ''
        extra += f" (30초표본 추격 {mean(r['plen30'] for r in S):.0f}s)"
        print(f"     {sc}: 승 {sum(r['win'] for r in S)}/{len(S)} {mean(r['dur'] for r in S):.0f}s K{sum(r['K'] for r in S)}/E{sum(r['E'] for r in S)} 패자손실 {mean(r['loserPct'] for r in S):.0f}%{extra} 기병돌격 {sum(r['cavN'] for r in S)}")
