#!/usr/bin/env python3
"""Stage 3A coordination + result table (docs/TACTICAL_AI_STAGE3_REVIEW_2026-10-19.md '3A 결과').

  python tools/bn_campaign/agg_s3a.py --raw <run.py --out folder> [--raw <another> ...] [--side 1]

Reads runs/*.json.gz (BnCamp, KD_BN_OUT) and the 'plans' block (TacticalAi.PlanLog, stride in the file).
Per config: runs, attacker wins (defender loss fraction > attacker loss fraction, same as aggregate.py), attacker loss mean,
first plan per regiment of the side (F frontal / FL flank / PI pincer / SP schwerpunkt), flank+pincer coordination:
  seq_ok  = fixing element engaged (fixTick > 0) at or before the assault release (releaseTick)
  arr_ok  = manoeuvre element arrived (arriveTick > 0) within maxArrive (120 s) of the plan
  aborts  = plans ended without release (deadline / manoeuvre element lost)
"""
import argparse, glob, gzip, json, os, re
from collections import defaultdict

KN = {1: 'F', 2: 'FL', 3: 'PI', 4: 'SP'}


def load(raw):
    out = []
    for p in sorted(glob.glob(os.path.join(raw, 'runs', '*.json.gz'))):
        with gzip.open(p, 'rt', encoding='utf8') as f:
            d = json.load(f)
        m = re.match(r'(.*)_s(\d+)\.json\.gz$', os.path.basename(p))
        d['_cfg'], d['_seed'] = m.group(1), int(m.group(2))
        out.append(d)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--raw', action='append', required=True)
    ap.add_argument('--side', default='1')
    ap.add_argument('--max-arrive', type=float, default=120.0)
    a = ap.parse_args()
    rows = defaultdict(list)
    for raw in a.raw:
        tag = os.path.basename(os.path.normpath(raw))
        for d in load(raw):
            rows[(tag, d['_cfg'])].append(d)
    print('| 묶음 | 시나리오 | 판 | 공격 승 | 공격 손실 | 첫 계획 | 측면·협공 | 순서 맞음 | 도착 ≤120초 | 도착 (초) | 취소 |')
    print('|---|---|---|---|---|---|---|---|---|---|---|')
    for (tag, cfg), L in sorted(rows.items()):
        wins, loss = 0, []
        kinds = defaultdict(int); fp = 0; seq = 0; arr = 0; aborts = 0; arrs = []
        for d in L:
            s1, s2 = d['start']['1'], d['start']['2']
            l1, l2 = 1 - d['alive']['1'] / s1, 1 - d['alive']['2'] / s2
            att, de = (l1, l2) if a.side == '1' else (l2, l1)
            wins += 1 if de > att else 0
            loss.append(att)
            p = (d.get('plans') or {}).get(a.side)
            if not p: continue
            st, log = p['stride'], p['log']
            seen = set()
            for i in range(0, len(log), st):
                reg, kind, side, pt, ft, at, rt = log[i:i + 7]
                why = log[i + 10]
                if reg in seen: continue
                seen.add(reg)
                kinds[KN.get(kind, '?')] += 1
                if kind in (2, 3):
                    fp += 1
                    if why == 0 and ft > 0 and ft <= rt: seq += 1
                    if at > 0 and (at - pt) / 60 <= a.max_arrive: arr += 1; arrs.append((at - pt) / 60)
                    if why == 1: aborts += 1
            for o in p.get('open') or []:   # still running at the end (counted as not released)
                if o[0] not in seen: kinds[KN.get(o[1], '?')] += 1; fp += 1
        n = len(L)
        ml = sum(loss) / n if n else 0
        kd = ' '.join(f"{k}{v}" for k, v in sorted(kinds.items()))
        pct = lambda x: f"{x}/{fp} ({100 * x / fp:.0f}%)" if fp else '-'
        am = f"{sum(arrs) / len(arrs):.0f}" if arrs else '-'
        print(f"| {tag} | {cfg} | {n} | {wins}/{n} | {100 * ml:.1f}% | {kd} | {fp} | {pct(seq)} | {pct(arr)} | {am} | {aborts} |")


if __name__ == '__main__':
    main()
