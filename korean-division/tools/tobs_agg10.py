# 10차 집계 (중대 후퇴 30~40%): python -I tools/tobs_agg10.py docs/tactics_obs_raw10 V0 V10
import re, sys, glob, statistics as st
d = sys.argv[1]
def parse(p):
    t = open(p, encoding='utf-8').read()
    h = re.search(r'# \S+ seed \d+: (\w+) at ([\d.]+)s', t)
    f = {int(a): (int(b), int(c), int(e)) for a, _, b, c, e in re.findall(r'faction (\d) (.*?): spawned (\d+), lost (\d+), alive (\d+)', t)}
    won = h.group(1) == 'won'
    loser = 2 if won else 1
    sp, lost, alive = f[loser]
    r = dict(win=won, dur=float(h.group(2)), K=f[1][1], E=f[2][1], loserPct=100 * lost / sp, annih=alive == 0, ret=0, first=[], retK=0, retE=0)
    for sec in t.split('## 세력')[1:]:
        fac = int(sec.split()[0])
        m = re.search(r'Fallbacks (\d+)', sec)
        n = int(m.group(1)) if m else 0
        r['ret'] += n
        r['retK' if fac == 1 else 'retE'] += n
        fr = [int(b) and 100 * (1 - int(a) / int(b)) for a, b in re.findall(r'->Fallback \(병력 (\d+)/(\d+)', sec)]
        r['first'] += fr[:1]   # 세력당 첫 후퇴 사상률 (로그 400줄 상한)
    return r
for v in sys.argv[2:]:
    R = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_[ABC][1-5].txt'))]
    fs = [x for r in R for x in r['first']]
    allf = st.mean(fs) if fs else float('nan')
    print(f"{v}: n={len(R)} 승 {sum(r['win'] for r in R)} K/E {sum(r['K'] for r in R)/sum(r['E'] for r in R):.2f} 평균초 {st.mean(r['dur'] for r in R):.0f} "
          f"패자손실% {st.mean(r['loserPct'] for r in R):.0f} 전멸 {sum(r['annih'] for r in R)} 철수/항복 {sum(not r['annih'] for r in R)} "
          f"후퇴수/판 {st.mean(r['ret'] for r in R):.1f} (한 {st.mean(r['retK'] for r in R):.1f} 적 {st.mean(r['retE'] for r in R):.1f}) "
          f"첫후퇴 사상률% 평균 {allf:.0f} 중앙 {st.median(fs) if fs else float('nan'):.0f} (표본 {len(fs)})")
    for sc in 'ABC':
        S = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_{sc}[1-5].txt'))]
        ff = [x for r in S for x in r['first']]
        print(f"   {sc}: 승 {sum(r['win'] for r in S)}/5 {st.mean(r['dur'] for r in S):.0f}s K{sum(r['K'] for r in S)}/E{sum(r['E'] for r in S)} 패자손실 {st.mean(r['loserPct'] for r in S):.0f}% 전멸 {sum(r['annih'] for r in S)} 후퇴 {st.mean(r['ret'] for r in S):.1f}/판 첫후퇴 {st.mean(ff) if ff else float('nan'):.0f}%")
