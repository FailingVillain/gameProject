"""N21 돌격보병 재측정 (측정 전용): 소총병 대 돌격보병, 동수 / 동가치, 시드 1-5, 좌우 교대. 사용: python tools/n21_exchange.py [exe] -> docs/n18_raw/results_n21.jsonl"""
import subprocess, json, re, os, sys, time
EXE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', 'bin', 'carbine', 'KoreanDivision.exe')
OUT = os.path.join(os.path.dirname(__file__), '..', 'docs', 'n18_raw', 'results_n21.jsonl')
os.makedirs(os.path.dirname(OUT), exist_ok=True)
jobs = [('head', 30, 30), ('value', 30, 23)]   # 소총병 : 돌격보병 (N18 동일)
t0 = time.time()
with open(OUT, 'a', encoding='utf-8') as res:
    for mode, nx, ny in jobs:
        for seed in range(1, 6):
            for swap in (0, 1):
                a, b = ('infantry', nx), ('infantry_assault', ny)
                sa, sb = (a, b) if not swap else (b, a)
                p = subprocess.run([EXE, '--balance', 'sim', f'{sa[0]}:{sa[1]}', f'{sb[0]}:{sb[1]}', 'flat', str(seed), '1', '400'],
                                   capture_output=True, text=True, encoding='utf-8', errors='replace')
                m = re.search(r'MEAS (.*)', p.stdout); mm = re.search(r'MARCH (.*)', p.stdout)
                if not m: print('FAIL', mode, seed, swap, p.stdout[-200:]); continue
                kv = dict(t.split('=') for t in m.group(1).split()); mk = dict(t.split('=') for t in mm.group(1).split()) if mm else {}
                st = kv['start'].split('/'); le = kv['lossEnd'].split('/'); w = int(kv['winner'])
                lx, ly = (int(le[0]), int(le[1])) if not swap else (int(le[1]), int(le[0]))
                sx, sy = (int(st[0]), int(st[1])) if not swap else (int(st[1]), int(st[0]))
                rec = dict(mode=mode, seed=seed, swap=swap, startX=sx, startY=sy, lossX=lx, lossY=ly, xWin=(w == (2 if swap else 1)), yWin=(w == (1 if swap else 2)),
                           winner=w, t=float(kv['end_t']), meanDist=float(kv['meanDist']), hitDist=float(kv['hitDist']), shots=int(kv['shots']), march={k: int(v) for k, v in mk.items()})
                res.write(json.dumps(rec) + '\n'); res.flush()
        print(mode, round(time.time() - t0), 's', flush=True)

# B: 장교 지휘 중대 (돌격 사격 사용량 측정) - tools/n21_scen_*.json 을 bin/<빌드>/assets/missions/ 에 복사해 두고 실행. 세력 1 = 돌격 (한국군 전용이라 좌우 교대 없음)
OUTB = OUT.replace('results_n21', 'results_n21_march')
with open(OUTB, 'a', encoding='utf-8') as res:
    for mode, mid in (('head', 'n21_assault_vs_rifle'), ('value', 'n21_assault_vs_rifle_v')):
        for seed in range(1, 6):
            env = dict(os.environ, KD_MEAS='1')
            p = subprocess.run([EXE, '--mission-run', mid, '--seed', str(seed), '--seconds', '400'], capture_output=True, text=True, encoding='utf-8', errors='replace', env=env)
            m = re.search(r'MEAS (.*)', p.stdout); mm = re.search(r'MARCH (.*)', p.stdout)
            if not m: print('FAIL', mode, seed, p.stdout[-200:]); continue
            kv = dict(t.split('=') for t in m.group(1).split()); mk = dict(t.split('=') for t in mm.group(1).split())
            st = kv['start'].split('/'); le = kv['lossEnd'].split('/')
            rec = dict(mode=mode, seed=seed, startA=int(st[0]), startB=int(st[1]), lossA=int(le[0]), lossB=int(le[1]), winner=int(kv['winner']), t=float(kv['end_t']), meanDist=float(kv['meanDist']), hitDist=float(kv['hitDist']), shots=int(kv['shots']), march={k: int(v) for k, v in mk.items()})
            res.write(json.dumps(rec) + chr(10)); res.flush()
    print('march part done', round(time.time() - t0), 's')
