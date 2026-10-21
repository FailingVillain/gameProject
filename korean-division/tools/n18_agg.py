import json, collections, statistics as S, sys
R=[json.loads(l) for l in open('docs/n18_raw/results.jsonl',encoding='utf-8')]
g=collections.defaultdict(list)
for r in R:
    r['def']=None if r['ter']=='flat' else ('Y' if r['swap']==0 else 'X')
    g[(r['x'],r['y'],r['mode'],r['ter'],r['def'])].append(r)
def f(rs):
    n=len(rs); xw=sum(r['xWin'] for r in rs); yw=sum(r['yWin'] for r in rs)
    sx=S.mean(r['startX']-r['lossX'] for r in rs); sy=S.mean(r['startY']-r['lossY'] for r in rs)
    lx=S.mean(r['lossX'] for r in rs); ly=S.mean(r['lossY'] for r in rs)
    return f"{rs[0]['x']} vs {rs[0]['y']}|{rs[0]['startX']}v{rs[0]['startY']}|X승 {xw}/{n} Y승 {yw} 무 {n-xw-yw}|생존 X {sx:.1f} Y {sy:.1f}|교환비(Y손실/X손실) {ly/max(lx,.1):.2f}|{S.mean(r['t'] for r in rs):.0f}s|명중거리 {S.mean(r['hitDist'] for r in rs):.1f}|명중% {S.mean(r['hitpct'] for r in rs):.0f}"
for k,rs in g.items(): print(k[2],k[3],k[4] or '', f(rs))
