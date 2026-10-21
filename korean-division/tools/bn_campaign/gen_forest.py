"""Forest-combat subset (forest branch, 2026-10-19): campaign-2 forest ambush rows + N18 forest rows, 8 seeds each.
run.py --gen gen_forest --out docs/bncamp_forest_raw   (BN_GATE = heavy gate script)"""
import gen_scen, gen_scen2

SEEDS = [1, 2, 3, 4, 5, 6, 7, 8]
_ids2 = {'AMB_F_column_r1', 'AMB_F_line_r1', 'AMB_O_column_r1', 'AMB_F_column_night'}
_ids1 = {'D_forest_r2.0_RM', 'M_forest_RM'}
_src = {}


def cfgs():
    out = []
    gen_scen2.SEEDS = SEEDS
    for c in gen_scen2.cfgs():
        if c['id'] in _ids2:
            c['seeds'] = SEEDS; out.append(c); _src[c['id']] = gen_scen2
    for c in gen_scen.cfgs():
        if c['id'] in _ids1:
            c['seeds'] = SEEDS; out.append(c); _src[c['id']] = gen_scen
    return out


def mission(c):
    return _src[c['id']].mission(c)
