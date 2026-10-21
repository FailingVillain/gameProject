"""N18 병종별 1:1 교환비 측정 (측정 전용). 사용: python tools/n18_exchange.py  -> docs/n18_raw/results.jsonl
한 번에 한 프로세스씩 순차 실행. A=먼저 적은 쪽(세력 1), 좌우 교대(swap) 포함."""
import subprocess, json, re, os, sys, time
EXE = os.path.join(os.path.dirname(__file__), '..', 'bin', 'n18', 'KoreanDivision.exe')
OUT = os.path.join(os.path.dirname(__file__), '..', 'docs', 'n18_raw', 'results.jsonl')
HP = dict(infantry=100, recruit=100, veteran=100, infantry_scout=100, infantry_assault=130, infantry_guard=125,
          cavalry_default=180, heavy_machinegunner=160, heavy_maxim=340, infantry_sniper=100,
          heavy_infantry_gun=200, heavy_field_gun=300)
N = 30
def vcount(u): return max(1, round(N * 100 / HP[u]))
pairs = []  # (X, Y, group)
for y in ['infantry','recruit','veteran','infantry_scout','infantry_assault','infantry_guard','cavalry_default','heavy_machinegunner','heavy_maxim','infantry_sniper','heavy_infantry_gun','heavy_field_gun']:
    pairs.append(('infantry', y))
pairs += [('cavalry_default','infantry_assault'),('heavy_machinegunner','infantry_assault'),('heavy_maxim','infantry_assault'),
          ('infantry_guard','infantry_assault'),('heavy_maxim','heavy_machinegunner'),('cavalry_default','heavy_machinegunner'),
          ('cavalry_default','heavy_maxim'),('infantry_assault','infantry_assault')]
jobs = []
for x, y in pairs:
    for mode in ('head', 'value'):
        nx, ny = (N, N) if mode == 'head' else (vcount(x), vcount(y))
        jobs.append(dict(x=x, y=y, nx=nx, ny=ny, mode=mode, ter='flat'))
core = ['infantry','heavy_machinegunner','heavy_maxim','infantry_assault','cavalry_default','infantry_guard']
for ter in ('trench', 'forest'):
    for y in core:
        for mode in ('head',):
            jobs.append(dict(x='infantry', y=y, nx=N, ny=N, mode=mode, ter=ter))
res = open(OUT, 'a', encoding='utf-8')
t0 = time.time()
for j in jobs:
    for seed in range(1, 6):
        for swap in (0, 1):
            a, b = (j['x'], j['nx']), (j['y'], j['ny'])
            # swap=0: X=세력1(왼쪽), Y=세력2 / swap=1: 반대. 지형 변형은 세력 2 (B) 진영에 적용
            # X 가 방어(지형 안) 인 경우와 Y 가 방어인 경우 모두 측정: swap 으로 자연히 교대
            sa, sb = (a, b) if not swap else (b, a)
            env = dict(os.environ, KD_X_TER=j['ter'] if j['ter'] != 'flat' else '')
            p = subprocess.run([EXE, '--balance', 'sim', f'{sa[0]}:{sa[1]}', f'{sb[0]}:{sb[1]}', 'flat', str(seed), '1', '400'],
                               capture_output=True, text=True, env=env, encoding='utf-8', errors='replace')
            m = re.search(r'MEAS (.*)', p.stdout)
            if not m: print('FAIL', j, seed, swap, p.stdout[-300:], p.stderr[-300:]); continue
            kv = dict(t.split('=') for t in m.group(1).split())
            st = kv['start'].split('/'); le = kv['lossEnd'].split('/'); t50 = kv['t50'].split('/')
            w = int(kv['winner'])
            lx, ly = (int(le[0]), int(le[1])) if not swap else (int(le[1]), int(le[0]))
            sx, sy = (int(st[0]), int(st[1])) if not swap else (int(st[1]), int(st[0]))
            tx, ty = (t50[0], t50[1]) if not swap else (t50[1], t50[0])
            xw = (w == (2 if swap else 1)); yw = (w == (1 if swap else 2))
            rec = dict(**j, seed=seed, swap=swap, startX=sx, startY=sy, lossX=lx, lossY=ly, xWin=xw, yWin=yw, winner=w,
                       t=float(kv['end_t']), t50X=tx, t50Y=ty, hitpct=float(kv['hitpct']), meanDist=float(kv['meanDist']), hitDist=float(kv['hitDist']), shots=int(kv['shots']))
            res.write(json.dumps(rec) + '\n'); res.flush()
    print(j, round(time.time() - t0), 's', flush=True)
