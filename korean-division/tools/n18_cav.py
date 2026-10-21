"""N18 기병 행만 다시 잼 (12차 pursuit): python -I tools/n18_cav.py <exe> <라벨> [행 수]  -> 표 출력 (조합당 시드 1~5 x 좌우 교대 = 10판, 순차). 행 수 = 앞에서 몇 줄만 (13차: 2 = 소총 대 기병)."""
import subprocess, re, sys, os
sys.stdout.reconfigure(encoding="utf-8")
exe, label = os.path.abspath(sys.argv[1]), sys.argv[2]
rows = [('infantry', 30, 'cavalry_default', 30, '동수'), ('infantry', 30, 'cavalry_default', 17, '동가치'),
        ('cavalry_default', 30, 'heavy_machinegunner', 30, '동수'), ('cavalry_default', 17, 'heavy_machinegunner', 19, '동가치'),
        ('cavalry_default', 30, 'heavy_maxim', 30, '동수'), ('cavalry_default', 17, 'heavy_maxim', 9, '동가치')]
if len(sys.argv) > 3: rows = rows[:int(sys.argv[3])]
print(f'## {label}')
print('| 조합 | 모드 | X 승 / Y 승 / 시간초과 | X 손실 평균 | Y 손실 평균 | 평균 결판초 | 기병 돌격 정면/측면/후방 · 경유 · 물러남 |')
print('|---|---|---|---|---|---|---|')
for x, nx, y, ny, mode in rows:
    xw = yw = to = 0; lx = ly = tt = 0.0; cav = [0] * 5; n = 0
    for seed in range(1, 6):
        for swap in (0, 1):
            a, b = (f'{x}:{nx}', f'{y}:{ny}') if not swap else (f'{y}:{ny}', f'{x}:{nx}')
            p = subprocess.run([exe, '--balance', 'sim', a, b, 'flat', str(seed), '1', '400'], capture_output=True, text=True, encoding='utf-8', errors='replace')
            m = re.search(r'MEAS (.*)', p.stdout)
            if not m: print('FAIL', x, y, seed, swap); continue
            kv = dict(t.split('=') for t in m.group(1).split())
            le = list(map(int, kv['lossEnd'].split('/')))
            w = int(kv['winner'])
            xl, yl = (le[0], le[1]) if not swap else (le[1], le[0])
            if w == 0: to += 1
            elif w == (2 if swap else 1): xw += 1
            else: yw += 1
            lx += xl; ly += yl; tt += float(kv['end_t']); n += 1
            c = re.search(r'기병AI: 돌격 정면 (\d+) 측면 (\d+) 후방 (\d+) 측면경유 (\d+) 소규모보류 \d+ 물러남 (\d+)', p.stdout)
            if c: cav = [s + int(v) for s, v in zip(cav, c.groups())]
    n = max(1, n)
    print(f'| {x} {nx} 대 {y} {ny} | {mode} | {xw} / {yw} / {to} | {lx/n:.1f} | {ly/n:.1f} | {tt/n:.0f} | {cav[0]}/{cav[1]}/{cav[2]} · {cav[3]} · {cav[4]} |', flush=True)
