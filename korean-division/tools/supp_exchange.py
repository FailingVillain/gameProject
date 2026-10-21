"""N25 제압 재설계 전후 N18 핵심 교환 행 (측정 전용, 순차 1 프로세스).
사용: python -I tools/supp_exchange.py <exe> <out.jsonl>
행: 소총 30 대 경기관총 · 맥심 · 저격수 · 돌격보병 (같은 인원, 평지) + 소총 대 경기관총 · 맥심 (참호 = 세력 2 진영). 시드 1~SUPP_SEEDS (기본 5) x 좌우 교대."""
import subprocess, json, re, os, sys
exe, out = sys.argv[1], sys.argv[2]
N = 30
jobs = [dict(x='infantry', y=y, ter='flat') for y in ('heavy_machinegunner', 'heavy_maxim', 'infantry_sniper', 'infantry_assault')]
jobs += [dict(x='infantry', y=y, ter='trench') for y in ('heavy_machinegunner', 'heavy_maxim')]
res = open(out, 'w', encoding='utf-8')
for j in jobs:
    for seed in range(1, int(os.environ.get("SUPP_SEEDS", "5")) + 1):
        for swap in (0, 1):
            a, b = (j['x'], N), (j['y'], N)
            sa, sb = (a, b) if not swap else (b, a)
            env = dict(os.environ, KD_X_TER=j['ter'] if j['ter'] != 'flat' else '')
            p = subprocess.run([exe, '--balance', 'sim', f'{sa[0]}:{sa[1]}', f'{sb[0]}:{sb[1]}', 'flat', str(seed), '1', '400'],
                               capture_output=True, text=True, env=env, encoding='utf-8', errors='replace')
            m = re.search(r'MEAS (.*)', p.stdout)
            if not m: print('FAIL', j, seed, swap, p.stdout[-300:], p.stderr[-300:]); continue
            kv = dict(t.split('=') for t in m.group(1).split())
            st = kv['start'].split('/'); le = kv['lossEnd'].split('/')
            w = int(kv['winner'])
            lx, ly = (int(le[0]), int(le[1])) if not swap else (int(le[1]), int(le[0]))
            xw = (w == (2 if swap else 1)); yw = (w == (1 if swap else 2))
            res.write(json.dumps(dict(**j, seed=seed, swap=swap, lossX=lx, lossY=ly, xWin=xw, yWin=yw, t=float(kv['end_t']), hitpct=float(kv['hitpct']))) + '\n'); res.flush()
    print(j, flush=True)
# 요약
rows = [json.loads(l) for l in open(out, encoding='utf-8')]
for j in jobs:
    r = [x for x in rows if x['y'] == j['y'] and x['ter'] == j['ter']]
    if not r: continue
    lx = sum(x['lossX'] for x in r); ly = sum(x['lossY'] for x in r)
    print(f"{j['x']} vs {j['y']} [{j['ter']}]: rifle wins {sum(x['xWin'] for x in r)}/{len(r)}, Y wins {sum(x['yWin'] for x in r)}, loss rifle {lx} / Y {ly}, exch(Y per rifle loss) {ly / max(1, lx):.2f}, t {sum(x['t'] for x in r) / len(r):.0f}s, hit% {sum(x['hitpct'] for x in r) / len(r):.1f}")
