"""Capture-point village (점령지 마을): 8x8 tiles (96x96 units), mud-brick enclosure with a south gate, central square with
a well and a flagpole, 관아 (government hall) at the north, farm houses, barn, trees and paths.
States: neutral (bare pole), owned (team-colour flag on the team layer), damaged / damaged_owned (burnt houses, wall gaps)."""
from __future__ import annotations
import numpy as np
from bcommon import B, slope, post, blob, rod, jars, rubble, tree
from houses import thatch_house, tile_house, barn, charred
from voxel import tr, ry, cyl, ell, box

I4 = np.eye(4)
W = 44.0          # half size of the enclosure (wall centre line)
WALL_H = 5.0
VILLAGE_BBOX = (-52.0, 52.0, 41.0, -50.0, 50.0)
wall_paint = [(lambda X, Y, Z: ((Y % 1.7) < 0.2) | ((((X + Z) + np.floor(Y / 1.7) % 2 * 1.5) % 3.0) < 0.2), "mud_dark")]


def _segs(a, b, step=10.0):
    n = max(1, int(round((b - a) / step)))
    e = np.linspace(a, b, n + 1)
    return list(zip(e[:-1], e[1:]))


def wall_run(M, x0, x1, z0, z1, h=WALL_H, gaps=()):
    """straight wall box run split in short pieces (render cost); gaps = [(lo, hi)] along the run axis to leave open"""
    o = []
    along_x = (x1 - x0) >= (z1 - z0)
    a, b = (x0, x1) if along_x else (z0, z1)
    for s0, s1 in _segs(a, b):
        if any(lo < (s0 + s1) / 2 < hi for lo, hi in gaps):
            hh = 1.2 if any(lo < (s0 + s1) / 2 < hi for lo, hi in gaps) else h
            if along_x:
                o += [B(M, s0, s1, 0, hh, z0, z1, "mud", wall_paint)]
            else:
                o += [B(M, x0, x1, 0, hh, s0, s1, "mud", wall_paint)]
            continue
        if along_x:
            o += [B(M, s0, s1, 0, h, z0, z1, "mud", wall_paint), B(M, s0, s1, h, h + 0.9, z0 - 0.5, z1 + 0.5, "thatch_old")]
        else:
            o += [B(M, x0, x1, 0, h, s0, s1, "mud", wall_paint), B(M, x0 - 0.5, x1 + 0.5, h, h + 0.9, s0, s1, "thatch_old")]
    return o


def gate(M, broken=False):
    o = []
    o += [B(M, -6.8, -4.4, 0, 8.6, W - 1.6, W + 1.6, "mud", wall_paint), B(M, 4.4, 6.8, 0, 8.6, W - 1.6, W + 1.6, "mud", wall_paint)]
    o += [post(M, -4.6, W + 1.8, 0, 8.4, 0.7, "wood"), post(M, 4.6, W + 1.8, 0, 8.4, 0.7, "wood")]
    if not broken:
        o += [B(M, -7.4, 7.4, 8.6, 9.6, W - 2.0, W + 2.2, "wood_dark")]
        for sg in (1, -1):
            o += [slope(M, -8.6, 8.6, sg * 4.6 + W, 9.8, W, 13.4, 1.0, "kiwa", [(lambda X, S, Z: ((X % 1.8) < 0.5) | ((S % 1.7) < 0.16), "kiwa2")])]
        o += [B(M, -8.8, 8.8, 13.2, 14.2, W - 0.7, W + 0.7, "kiwa2")]
        o += [B(M, -4.2, -0.2, 0.0, 6.0, W + 0.6, W + 1.1, "wood", [(lambda X, Y, Z: ((X % 1.1) < 0.14), "wood_dark")]),
              B(M, 0.2, 4.2, 0.0, 6.0, W + 0.6, W + 1.1, "wood", [(lambda X, Y, Z: ((X % 1.1) < 0.14), "wood_dark")])]
    else:
        o += [B(M, -7.0, -1.0, 8.6, 9.6, W - 1.6, W + 1.6, "char"), rod(M, (-4, 9.2, W + 1), (2, 2.4, W + 5), 0.7, "char"),
              rod(M, (3, 0.8, W + 4), (8, 1.0, W + 7), 0.6, "char"), B(M, -4.0, -1.6, 0, 4.2, W + 0.6, W + 1.1, "char")]
        o += rubble(M, -8, 8, W - 2, W + 6, 8, seed=21)
    return o


def well(M, x, z):
    o = [cyl(M @ tr(x, 1.5, z), 3.4, 1.5, "stone", [(lambda p: ((np.arctan2(p[:, 2], p[:, 0]) * 6) % 2.0) < 0.14, "brick_dark")]),
         cyl(M @ tr(x, 3.05, z), 2.5, 0.12, "wood_dark")]
    o += [post(M, x - 3.0, z, 3.0, 8.6, 0.45, "wood"), post(M, x + 3.0, z, 3.0, 8.6, 0.45, "wood"), B(M, x - 3.6, x + 3.6, 8.2, 8.9, z - 0.5, z + 0.5, "wood")]
    for sg in (1, -1):
        o += [slope(M, x - 4.6, x + 4.6, z + sg * 4.4, 8.0, z, 11.0, 0.8, "kiwa", [(lambda X, S, Z: ((X % 1.8) < 0.5) | ((S % 1.7) < 0.16), "kiwa2")])]
    o += [cyl(M @ tr(x + 1.8, 4.2, z + 3.6), 0.9, 0.7, "wood_dark")]
    return o


def flagpole(M, x, z, flag):
    top = 34.0
    o = [cyl(M @ tr(x, 0.6, z), 1.4, 0.6, "stone"), post(M, x, z, 0.6, top, 0.5, "wood"), blob(M, x, top + 0.8, z, 0.9, 0.9, 0.9, "wood_dark")]
    if flag:
        for i in range(4):
            x0 = x + 0.6 + i * 3.6
            ya = top - 0.2 - 0.9 * np.sin(i * 1.3)
            o.append(box(M @ tr(x0 + 1.8, ya - 4.2, z + (0.7 if i % 2 else -0.7)) @ ry(18 if i % 2 else -18), 1.9, 4.2, 0.3, "team"))
    return o


def mud_patch(M, x0, x1, z0, z1, mat="path", y=0.3):
    return B(M, x0, x1, 0, y, z0, z1, mat)


def scorch(M, x, z, rx_, rz_):
    return ell(M @ tr(x, 0.2, z), rx_, 0.35, rz_, "char")


def village(state):
    """state in neutral | owned | damaged | damaged_owned"""
    dmg = state.startswith("damaged")
    flag = state.endswith("owned")
    M = I4
    o = []
    # ground: paved square + paths
    o += [B(M, -14, 14, 0, 0.35, -9, 17, "stone", [(lambda X, Y, Z: ((X % 4.0) < 0.25) | ((Z % 4.0) < 0.25), "brick_dark")]),
          B(M, -3, 3, 0, 0.3, 17, W + 6, "path"), B(M, -29, -14, 0, 0.3, -1, 3, "path"), B(M, 14, 30, 0, 0.3, -1, 3, "path"),
          B(M, -30, -14, 0, 0.3, 22, 26, "path"), B(M, 14, 30, 0, 0.3, 22, 26, "path"), B(M, -3, 3, 0, 0.3, -18, -9, "path"),
          B(M, -22, -14, 0, 0.3, 4, 24, "path"), B(M, 14, 22, 0, 0.3, 4, 24, "path")]
    # enclosure (south wall has the gate; gaps when damaged)
    gaps_s = [(-30, -17)] if dmg else []
    gaps_e = [(-20, -6)] if dmg else []
    o += wall_run(M, -W, -6.8, W - 1, W + 1, gaps=gaps_s) + wall_run(M, 6.8, W, W - 1, W + 1)
    o += wall_run(M, -W - 1, -W + 1, -W, W, gaps=[(-34, -22)] if dmg else []) + wall_run(M, W - 1, W + 1, -W, W, gaps=gaps_e)
    o += wall_run(M, -W, W, -W - 1, -W + 1)
    o += gate(M, broken=dmg)
    # buildings
    def P(x, z):
        return tr(x, 0, z)
    o += tile_house(P(0, -28), ruin=False)
    o += thatch_house(P(-29, -6), ruin=dmg)
    o += thatch_house(P(-29, 21), ruin=False)
    o += barn(P(30, -8), ruin=dmg)
    o += thatch_house(P(30, 21), ruin=dmg)
    # square furniture
    o += well(M, -5.0, 3.0)
    o += flagpole(M, 8.5, 8.0, flag)
    o += jars(M, -12.0, 12.0, 2) if not dmg else []
    o += [blob(M, 37.0, 3.0, 2.0, 3.6, 2.8, 3.0, "thatch"), blob(M, 40.0, 2.0, 5.5, 2.8, 2.0, 2.4, "thatch_old")]       # haystacks
    o += [B(M, 10, 16, 0, 2.0, -6.5, -2.5, "wood", [(lambda X, Y, Z: ((X % 1.3) < 0.14), "wood_dark")])]                # cart / firewood
    # low fence in front of the left yard
    for zz in range(33, 42, 3):
        o += [post(M, -41.0 + (zz - 33) * 0.0, zz, 0, 2.6, 0.35, "wood")]
    o += [B(M, -41.4, -40.6, 1.2, 1.8, 32.5, 41.5, "wood")]
    # trees
    for (x, z, h, r, k) in ((-16, 34, 15, 6, 0), (17, 37, 17, 6.5, 0), (-40, -40, 16, 5.5, 1), (40, -40, 15, 5.5, 1), (-12, -8, 12, 4.5, 1), (45, 8, 13, 5, 0), (-47, 12, 14, 5, 0)):
        o += tree(M, x, z, h, r, k)
    if dmg:
        o += [scorch(M, -29, 4, 9, 6), scorch(M, 30, 8, 8, 5), scorch(M, 4, 22, 6, 3), scorch(M, -2, 8, 4, 2.5)]
        o += rubble(M, -34, -20, -14, 2, 5, seed=31) + rubble(M, 24, 40, 12, 28, 5, seed=32) + rubble(M, -24, -8, W - 6, W + 4, 6, seed=33)
    return charred(o, keep=tuple(k for k in ("stone", "mud", "mud_dark", "clay", "bark", "leaf", "leaf2", "path", "thatch_old", "thatch", "kiwa", "kiwa2", "wood", "wood_dark", "plaster", "paper")
                                 if not dmg)) if False else o


VILLAGES = {
    "village-capture": dict(
        states={s: (lambda s=s: village(s)) for s in ("neutral", "owned", "damaged", "damaged_owned")},
        bbox=VILLAGE_BBOX, footprint=(8, 8), label="점령지 마을"),
}
