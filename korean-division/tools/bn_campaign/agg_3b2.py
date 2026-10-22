#!/usr/bin/env python3
"""3B2 A/B comparison: one line per (config, variant) — wins, timeouts, losses, defender-side 3B metrics.

  python tools/bn_campaign/agg_3b2.py --base docs/ai3b2_raw/base --var default --var 3b_counter ...
Each variant = <base>/<var>_c1 and <base>/<var>_c2 (run.py outputs). Defender = side whose posture is defend
(cfg p1/p2, else role_of). Metrics on the defender: sup24 = share of company samples with a friendly company within 24 tiles,
trench = share of under-fire soldiers standing in trench tiles, DA = Defend->Advance/Approach switches per company,
res = reserve commits (trouble share), ctr = Counterattacks, plug = gap/breach/flank plugs. Attacker: plan releases/aborts, manRepl, fixPush.
"""
import argparse, glob, gzip, json, os, re, statistics as st, sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import aggregate as A


def trig(text, side, name):
    parts = re.split(r'^## 세력 (\d+)', text, flags=re.M)
    for i in range(1, len(parts), 2):
        if int(parts[i]) != side: continue
        line = re.search(r'^트리거 누계.*$', parts[i + 1], flags=re.M)
        m = re.search(r'(?:: |\| )' + name + r' (\d+)', line.group(0)) if line else None
        return int(m.group(1)) if m else 0
    return 0


def defender(cfg):
    if cfg.get('p1') == 'defend': return 1
    if cfg.get('p2') == 'defend': return 2
    r = A.role_of(cfg, 1)
    return 1 if r == 'def' else 2 if r == 'att' else 0


def load_var(base, var):
    out = []
    for sub in ('c1', 'c2', 'c4'):
        raw = os.path.join(base, f'{var}_{sub}')
        if not os.path.isdir(raw): continue
        cf = A.matrix(raw)
        for r in A.load(raw):
            A.derive(r, cf[r['cid']]); out.append(r)
    return out


def metrics(r):
    cfg = r['cfg']; d = defender(cfg); a = 3 - d if d else 1
    m = {'win1': r['att_win'], 'to': 1.0 if r['dur'] >= 590 else 0.0, 'l1': r['l1'], 'l2': r['l2'], 'dur': r['dur']}
    if d:
        n = A.cnt(r, d, 'sup.n')
        if n > 0: m['sup24'] = A.cnt(r, d, 'sup.within24') / n
        rs = [x for x in r['R'] if x['side'] == d]
        uf = sum(x['underFire'] for x in rs)
        if uf > 0: m['trench'] = sum(x['cTrench'] for x in rs) / uf
        ev = [e for e in r['E'] if e['side'] == d]
        gids = {e['gid'] for e in ev}
        da = sum(1 for e in ev if e['from'] == 'Defend' and e['to'] in ('Advance', 'Approach'))
        if gids: m['DA'] = da / len(gids)
        T = r['T'].get(d, {})
        if 'reserve' in T:
            m['res'] = T['reserve']['n']
            if T['reserve']['n']: m['resTr'] = T['reserve']['trouble'] / T['reserve']['n']
        m['ctr'] = trig(r['text'], d, 'Counterattacks')
        m['plug'] = trig(r['text'], d, 'PlugGap') + trig(r['text'], d, 'PlugBreach') + trig(r['text'], d, 'PlugFlank')
    p = (r.get('plans') or {}).get(str(a), {})
    m['rel'] = p.get('releases', 0); m['abort'] = p.get('aborts', 0); m['repl'] = trig(r['text'], a, 'ManRepl'); m['push'] = trig(r['text'], a, 'FixPush')
    m['plan'] = '/'.join(k[0] for k in ('flank', 'pincer', 'schwerpunkt', 'frontal') if p.get(k, 0) > 0) or '-'
    T = r['T'].get(a, {})
    if 'assault' in T and T['assault']['done'] > 0: m['asRet'] = T['assault']['retreat'] / T['assault']['done']
    return m


def mean(xs):
    xs = [x for x in xs if x is not None]
    return st.mean(xs) if xs else None


def f(x, pct=False, d=2):
    if x is None: return '-'
    return f'{x * 100:.0f}%' if pct else f'{x:.{d}f}'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--base', required=True)
    ap.add_argument('--var', action='append', required=True)
    a = ap.parse_args()
    data = {v: load_var(a.base, v) for v in a.var}
    cids = sorted({r['cid'] for v in data.values() for r in v})
    hdr = ['cfg', 'var', 'n', 'W1', 'TO', 'L1', 'L2', 'dur', 'sup24', 'trench', 'DA/co', 'res', 'resTr', 'ctr', 'plug', 'rel', 'abort', 'repl', 'push', 'asRet', 'plans']
    print('| ' + ' | '.join(hdr) + ' |'); print('|' + '---|' * len(hdr))
    tot = defaultdict(lambda: defaultdict(list))
    for c in cids:
        for v in a.var:
            rs = [r for r in data[v] if r['cid'] == c]
            if not rs: continue
            ms = [metrics(r) for r in rs]
            g = lambda k: [m.get(k) for m in ms]
            for k in ('win1', 'to', 'l1', 'l2', 'sup24', 'trench', 'DA', 'res', 'ctr', 'plug', 'rel', 'abort', 'repl', 'push'):
                tot[v][k] += [x for x in g(k) if x is not None]
            print('| ' + ' | '.join([c, v, str(len(rs)), f'{sum(g("win1")):.0f}', f'{sum(g("to")):.0f}', f(mean(g('l1')), True), f(mean(g('l2')), True), f(mean(g('dur')), d=0),
                                      f(mean(g('sup24')), True), f(mean(g('trench')), True), f(mean(g('DA')), d=1), f(mean(g('res')), d=1), f(mean(g('resTr')), True),
                                      f(mean(g('ctr')), d=1), f(mean(g('plug')), d=1), str(sum(g('rel'))), str(sum(g('abort'))), str(sum(g('repl'))), str(sum(g('push'))),
                                      f(mean(g('asRet')), True), ','.join(sorted(set(m['plan'] for m in ms)))]) + ' |')
    print()
    for v in a.var:
        t = tot[v]
        print(f"TOTAL {v}: runs {len(t['win1'])} W1 {sum(t['win1']):.0f} TO {sum(t['to']):.0f} L1 {f(mean(t['l1']), True)} L2 {f(mean(t['l2']), True)} sup24 {f(mean(t['sup24']), True)} trench {f(mean(t['trench']), True)} DA {f(mean(t['DA']), d=2)} ctr {sum(t['ctr'])} plug {sum(t['plug'])} rel {sum(t['rel'])} abort {sum(t['abort'])} repl {sum(t['repl'])} push {sum(t['push'])}")


if __name__ == '__main__':
    main()
