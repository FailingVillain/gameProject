"""N22 집계: python -X utf8 tools/artyfx_scen/agg_n22.py <폴더> [이름] [소규모 시드 수] [C 시드 수]
run_n22.sh 출력 (c_s* · cnoig_s* · d_s* · small_*) 을 읽어 포 종류별 살상 몫 · 보병포 거리 · 제거 시험 · 첫 사격 · 관측자/정찰병 생존 · 관측 가동률 · 돌격 표적 조준 · 승패 · K/E 를 한 번에 낸다."""
import json, re, sys, os
def mean(l): return sum(l) / len(l) if l else float('nan')
def art(path):
    if not os.path.exists(path): return None
    for line in open(path, encoding='utf-8', errors='replace'):
        if line.startswith('ARTYFX '): return json.loads(line[7:])
def sections(path):
    txt = open(path, encoding='utf-8', errors='replace').read()
    parts = re.split(r'^## 세력 (\d)', txt, flags=re.M)
    return {parts[i]: parts[i + 1] for i in range(1, len(parts), 2)}
def short(k): return k.split(':')[1].replace('heavy_', '')
def scen(d, pre, n, label):
    rows = []
    for s in range(1, n + 1):
        p = f'{d}/{pre}_s{s}.txt'
        a = art(p)
        if a: rows.append((a, sections(p)))
    if not rows: return None
    wins = sum(1 for a, _ in rows if a['winner'] == 1)
    K = [a['startA'] - a['endA'] for a, _ in rows]; E = [a['startB'] - a['endB'] for a, _ in rows]
    print(f"== {label} ({len(rows)} games): KR wins {wins}/{len(rows)}, t {mean([a['t'] for a, _ in rows]):.0f}s, K {mean(K):.0f} / E {mean(E):.0f}, K/E {mean(K) / max(1e-9, mean(E)):.2f}, KR alive {mean([a['endA'] for a, _ in rows]):.0f}")
    for f in ('1', '2'):
        n20 = [a['n20'][f] for a, _ in rows]; n22 = [a.get('n22', {}).get(f, {}) for a, _ in rows]
        enemy_loss = mean(E) if f == '1' else mean(K)
        kc = {}; sh = {}; first = {}; buck = {}; far = {}; fav = {}
        for a, _ in rows:
            for k, g in a['gun'].items():
                if not k.startswith(f + ':'): continue
                kc.setdefault(k, []).append(g['killCredit']); sh.setdefault(k, []).append(g['shells'])
                first.setdefault(k, []).append(g['first'] if g['first'] >= 0 else None)
                buck.setdefault(k, [0] * 5); buck[k] = [x + y for x, y in zip(buck[k], g['bucket'])]
                far[k] = far.get(k, 0) + g.get('far', 0); fav[k] = fav.get(k, 0) + g.get('farAvoidable', 0)
        asl = []; aon = []
        for a, sec in rows:
            m = re.search(r'\[포병\] 돌격 (\d+)회 판정.*?조준점이 돌격 표적 12칸 안 (\d+)', sec.get(f, ''))
            if m: asl.append(int(m.group(1))); aon.append(int(m.group(2)))
        obs = sum(x['obs'] for x in n20); oal = sum(x['obsAlive'] for x in n20)
        sc = sum(x.get('scouts', 0) for x in n22); sca = sum(x.get('scoutsAlive', 0) for x in n22)
        print(f"  F{f}: observers alive {oal}/{obs}, company scouts alive {sca}/{sc}, spotter uptime {100 * mean([x.get('spotUp', 0) for x in n22]):.0f}%, scout-sees uptime {100 * mean([x.get('scoutUp', 0) for x in n22]):.0f}% (scouts seeing {mean([x.get('scoutSeeMean', 0) for x in n22]):.1f}), cover entries/game {mean([x.get('coverIn', 0) for x in n22]):.1f}, assault-aim<=12 {sum(aon)}/{sum(asl)} ({100 * sum(aon) / max(1, sum(asl)):.0f}%), gun moves/game {mean([x['gunMoves'] for x in n20]):.0f}")
        for k in sorted(kc):
            fs = [x for x in first[k] if x is not None]
            b = buck[k]; tot = max(1, sum(b))
            print(f"     {short(k):14s} kills {mean(kc[k]):6.1f} ({100 * mean(kc[k]) / max(1, enemy_loss):4.1f}% of enemy loss)  shells {mean(sh[k]):5.0f}  first {mean(fs) if fs else -1:5.0f}s (never {first[k].count(None)})  range% 0-20/20-40/40-60/60-80/80+ {'/'.join(str(round(100 * x / tot)) for x in b)}  far>80% {100 * far[k] / tot:.0f}% (near target available {100 * fav[k] / max(1, far[k]):.0f}%)")
    return rows
def pair(base, var, label):
    if not base or not var: return
    n = min(len(base), len(var))
    dK = [(var[i][0]['startA'] - var[i][0]['endA']) - (base[i][0]['startA'] - base[i][0]['endA']) for i in range(n)]
    dA = [var[i][0]['endA'] - base[i][0]['endA'] for i in range(n)]
    dt = [var[i][0]['t'] - base[i][0]['t'] for i in range(n)]
    se = lambda l: (sum((x - mean(l)) ** 2 for x in l) / max(1, len(l) - 1)) ** 0.5 / max(1, len(l)) ** 0.5
    print(f"  {label}: KR loss {mean(dK):+.0f} [{se(dK):.0f}], KR alive {mean(dA):+.0f} [{se(dA):.0f}], time {mean(dt):+.0f}s")
def small(d, n, label):
    print(f'== small {label} (seeds 1-{n})')
    res = {}
    for v in ['ctl', 'ig3', 'ig3eq', 'fg3', 'fg3eq', 'hw3s4', 'hw3s4eq']:
        rows = [art(f'{d}/small_{v}_s{s}.txt') for s in range(1, n + 1)]
        rows = [r for r in rows if r]
        if not rows: continue
        res[v] = rows
        wins = sum(1 for a in rows if a['winner'] == 1)
        LA = [a['startA'] - a['endA'] for a in rows]; LB = [a['startB'] - a['endB'] for a in rows]
        sc0 = sum(a['start'].get('1:infantry_scout', 0) for a in rows); sc1 = sum(a['end'].get('1:infantry_scout', 0) for a in rows)
        g = "; ".join(f"{short(k)} kills {mean([a['gun'][k]['killCredit'] for a in rows if k in a['gun']]):.1f} shells {mean([a['gun'][k]['shells'] for a in rows if k in a['gun']]):.0f} first {mean([a['gun'][k]['first'] for a in rows if k in a['gun']]):.0f} lost {mean([a['start'].get(k, 0) - a['end'].get(k, 0) for a in rows]):.1f}" for k in sorted({k for a in rows for k in a['gun'] if k.startswith('1:')}))
        print(f"  {v:8s}: A wins {wins}/{len(rows)}, t {mean([a['t'] for a in rows]):.0f}, A loss {mean(LA):.1f}, B loss {mean(LB):.1f}, A alive {mean([a['endA'] for a in rows]):.1f}, scouts alive {sc1}/{sc0}; {g}")
    for a, b in [('ig3', 'ig3eq'), ('fg3', 'fg3eq'), ('hw3s4', 'hw3s4eq')]:
        if a in res and b in res:
            n2 = min(len(res[a]), len(res[b]))
            dd = [res[a][i]['endA'] - res[b][i]['endA'] for i in range(n2)]
            print(f"  {a} - {b} (same manpower) A alive {mean(dd):+.1f}")
if __name__ == '__main__':
    d = sys.argv[1]; label = sys.argv[2] if len(sys.argv) > 2 else d; n = int(sys.argv[3]) if len(sys.argv) > 3 else 10
    nc = int(sys.argv[4]) if len(sys.argv) > 4 else 5   # tweak1: C · noKig 시드 수 (기본 5)
    c = scen(d, 'c', nc, f'C {label}')
    ci = scen(d, 'cnoig', nc, f'C-noKig {label}')
    if c and ci: pair(c, ci, 'noKig - base (KR infantry guns removed)')
    scen(d, 'd', 3, f'D {label}')
    small(d, n, label)
