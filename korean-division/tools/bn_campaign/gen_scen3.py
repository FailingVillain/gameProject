#!/usr/bin/env python3
"""Crossing / counterattack measurement matrix (2026-10-19, branch crossing). Reuses gen_scen2 builders (RIV_* river+bridge, CTR_* trench retake).

Usage: python run.py --gen gen_scen3 --out <dir> (8 seeds each)
  RIV_br{1,2}_r{1.5,2}: river 6 wide, 1 or 2 bridges (4 wide), defender 2 rifle companies on the north bank, attacker 3 / 4 companies from the south.
  RIV_br1_r3: single bridge 3:1 rifle only (more force case).
  RIV_br1_r2_fs: single bridge 2:1 + MG company (maxim 5, LMG 8) + 2 howitzers + observers (fire support case).
  CTR_r{1,1.5,2}: holder (side 1) 2 companies in the trench, side 2 retakes from the north with 2 / 3 / 4 companies (one battalion).
  POSTURE (env BN_CTR_POSTURE, default 'counter'): mission posture of the retaking side. 'attack' = the pre-crossing setup (campaign 2).
No tanks, no AT riflemen.
"""
import copy, json, os, sys
import gen_scen2 as G2
from gen_scen2 import bn, TERRAINS

SEEDS = [1, 2, 3, 4, 5, 6, 7, 8]
POSTURE = os.environ.get('BN_CTR_POSTURE', 'counter')


def mission(cfg):
    return G2.mission(cfg)


def cfgs():
    L = []

    def add(id_, kind, terrain, forces, p1='attack', p2='defend', **kw):
        d = dict(id=id_, kind=kind, terrain=terrain, forces=forces, p1=p1, p2=p2, fac1='kr', fac2='ft', def_at=TERRAINS[terrain]['def_at'], seeds=SEEDS)
        d.update(kw); L.append(d)

    ax, ay = 80, 100
    for terrain, nm in (('river1', 'br1'), ('river2', 'br2')):
        x, y = TERRAINS[terrain]['def_at']
        for r, nco in ((1.5, 3), (2, 4)):
            fs = [bn("f1_bn", 1, 'kr', "한국군 소총대대", (ax, ay), 0, nco, n=84), bn("f2_bn", 2, 'ft', "적 소총대대", (x, y), 180, 2, n=84)]
            add(f"RIV_{nm}_r{r:g}", 'river', terrain, fs, ratio=float(r))
    # fire support variant (single bridge, 2:1): + MG company (maxim 5 / LMG 8) + 2 howitzers with observers
    x, y = TERRAINS['river1']['def_at']
    fs = [bn("f1_bn", 1, 'kr', "한국군 소총대대", (ax, ay), 0, 4, n=84), bn("f2_bn", 2, 'ft', "적 소총대대", (x, y), 180, 2, n=84),
          G2.mgco("f1_mg0", 1, 'kr', (ax - 3, ay + 8), 0, G2.MG2)] + G2.guns("f1", 1, 'kr', (ax, ay), 0, obs=True, how=2, ig=False, dirn=1)
    add("RIV_br1_r2_fs", 'river', 'river1', fs, ratio=2.0, support='mg+how')
    # more force (single bridge, 3:1 rifle only)
    add("RIV_br1_r3", 'river', 'river1', [bn("f1_bn", 1, 'kr', "한국군 소총대대", (ax, ay), 0, 6, n=84), bn("f2_bn", 2, 'ft', "적 소총대대", (x, y), 180, 2, n=84)], ratio=3.0)
    hx, hy = 80, 41
    holder = bn("f1_bn", 1, 'kr', "한국군 점령 중대", (hx, hy), 180, 2, n=84)
    for r, nco in ((1, 2), (1.5, 3), (2, 4)):
        fs = [copy.deepcopy(holder), bn("f2_bn", 2, 'ft', "봉천 반격대", (80, 12), 0, nco, n=84)]
        add(f"CTR_r{r:g}", 'counter', 'trench_cap', fs, p1='defend', p2=POSTURE, ratio=float(r), swap=True)
    return L


if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for c in cfgs():
        json.dump(mission(c), open(os.path.join(out, c['id'] + '.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    print(len(cfgs()), "configs")
