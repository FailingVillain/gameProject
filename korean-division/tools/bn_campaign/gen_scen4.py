#!/usr/bin/env python3
"""Defence-reserve matrix (2026-10-22, branch ai3b2): the C1/C2 defence scenarios have only 2 defending rifle companies,
so a defending battalion never holds a reserve and the 3B reserve features (reserveEvents · counterOdds · defendRepick) never fire.
DR_<terrain>_d<n>_r<ratio>: defender (side 2, fengtian) = one battalion of n rifle companies (n >= 3 -> battalion reserve),
attacker (side 1, korean) = one battalion of round(n * ratio) companies. Rifle only. No tanks, no AT riflemen.

Usage: python run.py --gen gen_scen4 --out <dir> --seeds 1-5
"""
import json, os, sys
import gen_scen2 as G2
from gen_scen2 import bn, TERRAINS

SEEDS = [1, 2, 3, 4, 5]


def mission(cfg):
    return G2.mission(cfg)


def cfgs():
    L = []
    ax, ay = 80, 100
    for terrain in ('open', 'trench', 'town'):
        for nd, r in ((3, 2.0), (4, 1.5)):
            x, y = TERRAINS[terrain]['def_at']
            na = int(round(nd * r))
            fs = [bn("f1_bn", 1, 'kr', "한국군 소총대대", (ax, ay), 0, na, n=84), bn("f2_bn", 2, 'ft', "적 소총대대", (x, y), 180, nd, n=84)]
            L.append(dict(id=f"DR_{terrain}_d{nd}_r{r:g}", kind='defence', terrain=terrain, forces=fs, p1='attack', p2='defend',
                          fac1='kr', fac2='ft', def_at=(x, y), ratio=r, seeds=SEEDS))
    return L


if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for c in cfgs():
        json.dump(mission(c), open(os.path.join(out, c['id'] + '.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    print(len(cfgs()), "configs")
