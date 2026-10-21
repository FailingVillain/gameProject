"""N23 돌격보병 재설계 측정 (측정 전용, 순차 1 프로세스).
사용: python -I tools/n23_exchange.py <exe> <out.jsonl> [seeds=5]
 A. N18 행: 소총병 대 돌격보병 동수 (30:30) · 동가치 (30:23), 평지 · 숲 (KD_X_TER=forest = 세력 2 진영 숲), 좌우 교대 (--balance sim, 장교 없음)
 B. 장교 지휘 중대: tools/n21_scen_*.json (장교 1 + 돌격 30/23 대 장교 1 + 봉천 30, 마을 지도)
 C. 작은 대대: 한국 3개 중대 (소총 2 + 돌격 1, 대조 = 소총 3) 대 봉천 소총 3개 중대 (같은 지도)
미션 파일은 exe 옆 assets/missions/ 에 복사 · 생성한다."""
import subprocess, json, re, os, sys, copy
exe, out = os.path.abspath(sys.argv[1]), sys.argv[2]
SEEDS = int(sys.argv[3]) if len(sys.argv) > 3 else 5
here = os.path.dirname(os.path.abspath(__file__))
mdir = os.path.join(os.path.dirname(os.path.abspath(exe)), 'assets', 'missions')
tmpl = json.load(open(os.path.join(here, 'n21_scen_template.json'), encoding='utf-8'))
val = json.load(open(os.path.join(here, 'n21_scen_value.json'), encoding='utf-8'))
def save(d):
    json.dump(d, open(os.path.join(mdir, d['id'] + '.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
save(tmpl); save(val)
def bn(mid, kr):
    d = copy.deepcopy(tmpl); d['id'] = mid; d['title'] = 'N23 대대 ' + mid
    xs = (64, 100, 136)
    d['forces'] = [dict(id=f'f1_{i}', faction=1, name=f'f1_{i}', at=[x, 120], facing=0, formation='line', companies=[dict(units={'officer': 1, k: 30}, formation='line')]) for i, (x, k) in enumerate(zip(xs, kr))]
    d['forces'] += [dict(id=f'f2_{i}', faction=2, name=f'f2_{i}', at=[x, 40], facing=0, formation='line', companies=[dict(units={'officer_fengtian': 1, 'infantry_fengtian': 30}, formation='line')]) for i, x in enumerate(xs)]
    save(d)
bn('n23_bn_rifle', ('infantry', 'infantry', 'infantry'))
bn('n23_bn_assault', ('infantry', 'infantry_assault', 'infantry'))

def kvs(pat, s):
    m = re.search(pat + r' (.*)', s)
    return dict(t.split('=') for t in m.group(1).split()) if m else None

res = open(out, 'w', encoding='utf-8')
for ter in ('flat', 'forest'):
    for mode, nx, ny in (('head', 30, 30), ('value', 30, 23)):
        for seed in range(1, SEEDS + 1):
            for swap in (0, 1):
                a, b = ('infantry', nx), ('infantry_assault', ny)
                sa, sb = (a, b) if not swap else (b, a)
                env = dict(os.environ, KD_X_TER=ter if ter != 'flat' else '')
                p = subprocess.run([exe, '--balance', 'sim', f'{sa[0]}:{sa[1]}', f'{sb[0]}:{sb[1]}', 'flat', str(seed), '1', '400'], capture_output=True, text=True, env=env, encoding='utf-8', errors='replace')
                kv = kvs('MEAS', p.stdout); mk = kvs('MARCH', p.stdout) or {}
                if not kv: print('FAIL', ter, mode, seed, swap, p.stdout[-300:]); continue
                le = kv['lossEnd'].split('/'); w = int(kv['winner'])
                lx, ly = (int(le[0]), int(le[1])) if not swap else (int(le[1]), int(le[0]))
                res.write(json.dumps(dict(part='A', ter=ter, mode=mode, seed=seed, swap=swap, assaultInForest=(ter == 'forest' and swap == 0), lossR=lx, lossA=ly, aWin=(w == (1 if swap else 2)), rWin=(w == (2 if swap else 1)), t=float(kv['end_t']), march={k: int(v) for k, v in mk.items()})) + '\n'); res.flush()
for mid in ('n21_assault_vs_rifle', 'n21_assault_vs_rifle_v', 'n23_bn_rifle', 'n23_bn_assault'):
    for seed in range(1, SEEDS + 1):
        p = subprocess.run([exe, '--mission-run', mid, '--seed', str(seed), '--seconds', '600'], capture_output=True, text=True, encoding='utf-8', errors='replace', env=dict(os.environ, KD_MEAS='1'))
        kv = kvs('MEAS', p.stdout); mk = kvs('MARCH', p.stdout) or {}
        if not kv: print('FAIL', mid, seed, p.stdout[-300:]); continue
        st = kv['start'].split('/'); le = kv['lossEnd'].split('/')
        res.write(json.dumps(dict(part='B' if mid.startswith('n21') else 'C', mid=mid, seed=seed, startK=int(st[0]), startF=int(st[1]), lossK=int(le[0]), lossF=int(le[1]), winner=int(kv['winner']), t=float(kv['end_t']), march={k: int(v) for k, v in mk.items()})) + '\n'); res.flush()
res.close()

rows = [json.loads(l) for l in open(out, encoding='utf-8')]
print('A (balance sim, no officer): rows = assault wins / n, loss rifle, loss assault, exch (rifle lost per assault lost), t, march shots/moveShots/pistol/gren/melee')
for ter in ('flat', 'forest'):
    for mode in ('head', 'value'):
        for sub, f in (('all', lambda r: True), ('A in forest', lambda r: r['assaultInForest']), ('R in forest', lambda r: not r['assaultInForest'])):
            if ter == 'flat' and sub != 'all': continue
            r = [x for x in rows if x['part'] == 'A' and x['ter'] == ter and x['mode'] == mode and f(x)]
            if not r: continue
            lr = sum(x['lossR'] for x in r); la = sum(x['lossA'] for x in r)
            m = lambda k: sum(x['march'].get(k, 0) for x in r)
            print(f"  {ter:6} {mode:5} {sub:12}: A wins {sum(x['aWin'] for x in r)}/{len(r)} R wins {sum(x['rWin'] for x in r)}, loss R {lr/len(r):.1f} A {la/len(r):.1f}, exch {lr/max(1,la):.2f}, t {sum(x['t'] for x in r)/len(r):.0f}s, shots {m('shots')} move {m('moveShots')} pistol {m('pistol')} gren {m('gren')} melee {m('melee')}")
print('B/C (missions, officers): K wins / n, loss K, loss F, t')
for mid in ('n21_assault_vs_rifle', 'n21_assault_vs_rifle_v', 'n23_bn_rifle', 'n23_bn_assault'):
    r = [x for x in rows if x.get('mid') == mid]
    if not r: continue
    m = lambda k: sum(x['march'].get(k, 0) for x in r)
    print(f"  {mid:24}: K wins {sum(x['winner'] == 1 for x in r)}/{len(r)}, loss K {sum(x['lossK'] for x in r)/len(r):.1f}/{r[0]['startK']} F {sum(x['lossF'] for x in r)/len(r):.1f}/{r[0]['startF']}, t {sum(x['t'] for x in r)/len(r):.0f}s, A shots {m('shots')} move {m('moveShots')} pistol {m('pistol')} gren {m('gren')} melee {m('melee')}")
