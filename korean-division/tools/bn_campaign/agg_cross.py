#!/usr/bin/env python3
"""Crossing / counterattack table (gen_scen3 runs, read-only) -> stdout (markdown) and <raw>/tables_cross.md.

  python tools/bn_campaign/agg_cross.py --raw <dir> [--raw2 <dir> ...]   (several dirs = before/after columns in one table)

River (RIV_*): attacker = side 1. success = side 1 won. cross time = first 2 s sample where >= 25% of side-1 company strength stands north of the river (centre y < 50).
Counter (CTR_*): attacker = side 2 (retakes the trench). success = side 1 lost. retake time = duration of the successful runs.
stalemate = run reached the time limit undecided.
"""
import argparse, os, sys, statistics as st
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aggregate as A


def ms(xs):
    xs = [x for x in xs if x is not None]
    if not xs: return '-'
    m = st.mean(xs)
    return f"{m:.0f}" if len(xs) == 1 else f"{m:.0f} ±{st.stdev(xs):.0f}"


def pct(xs):
    xs = [x for x in xs if x is not None]
    if not xs: return '-'
    m = st.mean(xs) * 100
    return f"{m:.0f}%" if len(xs) == 1 else f"{m:.0f}% ±{st.stdev(xs) * 100:.0f}"


def cross_time(r):
    by_t = defaultdict(lambda: [0, 0])
    for x in r['R']:
        if x['side'] != 1: continue
        b = by_t[x['t']]; b[0] += x['alive']
        if x['cy'] < 50: b[1] += x['alive']
    for t in sorted(by_t):
        a, n = by_t[t]
        if a > 0 and n >= 0.25 * a: return t
    return None


def rows_for(raw, label):
    cfgs = A.matrix(raw)
    runs = A.load(raw)
    out = []
    by = defaultdict(list)
    for r in runs:
        c = cfgs[r['cid']]
        s1, s2 = r['start']['1'], r['start']['2']
        r['l1'] = 1 - r['alive']['1'] / s1; r['l2'] = 1 - r['alive']['2'] / s2
        by[r['cid']].append((c, r))
    for cid in sorted(by):
        L = by[cid]; c = L[0][0]; rs = [x[1] for x in L]
        river = c['kind'] == 'river'
        ok = [1.0 if (r['result'] == 'won' if river else r['result'] == 'lost') else 0.0 for r in rs]
        stale = sum(1 for r in rs if r['result'] not in ('won', 'lost'))
        if river:
            ct = [cross_time(r) for r in rs]
            tm = ms([t for t in ct]); crossed = sum(1 for t in ct if t is not None)
            att, dfn = [r['l1'] for r in rs], [r['l2'] for r in rs]
            extra = f"{crossed}/{len(rs)}"
        else:
            tm = ms([r['dur'] for r, k in zip(rs, ok) if k]); extra = '-'
            att, dfn = [r['l2'] for r in rs], [r['l1'] for r in rs]
        out.append([label, cid, len(rs), f"{int(sum(ok))}/{len(rs)}", extra, tm, pct(att), pct(dfn), stale])
    return out


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--raw', action='append', required=True); ap.add_argument('--label', action='append')
    a = ap.parse_args()
    labels = a.label or [os.path.basename(os.path.normpath(p)) for p in a.raw]
    rows = []
    for p, lb in zip(a.raw, labels): rows += rows_for(p, lb)
    rows.sort(key=lambda r: (r[1], r[0]))
    h = ['코드', '시나리오', 'n', '성공', '도하 런', '도하 · 탈환 시각(초)', '공격 손실', '방어 손실', '교착(600초)']
    lines = ['| ' + ' | '.join(h) + ' |', '|' + '|'.join(['---'] * len(h)) + '|'] + ['| ' + ' | '.join(str(x) for x in r) + ' |' for r in rows]
    txt = '\n'.join(lines)
    print(txt)
    open(os.path.join(a.raw[-1], 'tables_cross.md'), 'w', encoding='utf8').write(txt + '\n')


if __name__ == '__main__':
    main()
