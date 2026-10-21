"""Four civilian house types + ruined frames. Each builder returns a list of prims in building coordinates
(origin = footprint centre on the ground, x right, y up, z toward the camera). M is an optional placement transform."""
from __future__ import annotations
import numpy as np
from bcommon import *  # noqa: F401,F403
from bcommon import B, slope, post, blob, rod, jars, rubble, tree, RUIN_MAP, p_brick, p_planks, p_lattice, p_tile_ridges, p_thatch_fibre
from voxel import tr, rx, ry, rz, cyl, ell

I4 = np.eye(4)


def charred(prims, keep=()):
    """burn a prim list: soot materials (RUIN_MAP) on base and paint materials"""
    for p in prims:
        if p.mat in RUIN_MAP and p.mat not in keep:
            p.mat = RUIN_MAP[p.mat]
        p.paint = [(f, RUIN_MAP.get(m, m)) for f, m in p.paint]
    return prims


def paper_door(M, x0, x1, y0, y1, z, t=0.5):
    return [B(M, x0 - 0.5, x1 + 0.5, y0 - 0.4, y1 + 0.5, z - 0.2, z + t, "wood"),
            B(M, x0, x1, y0, y1, z, z + t + 0.2, "paper", [(p_lattice, "wood")])]


# ------------------------------------------------------------------------------------------------ (a) thatched farmhouse
def thatch_house(M=I4, ruin=False):
    """초가집 2x2 tiles (24x24 units): mud walls, rounded straw roof, stone footing, paper doors, kitchen opening, chimney."""
    o = []
    o += [B(M, -10.5, 10.5, 0, 1.2, -8.5, 9.5, "stone")]                                   # stone footing / porch step
    if not ruin:
        o += [B(M, -10, 10, 1.2, 9.2, -8, 8, "mud"),
              B(M, -10.2, 10.2, 1.2, 2.2, -8.2, 8.2, "mud_dark")]                          # damp lower wall band
        o += paper_door(M, -7.8, -3.2, 2.8, 8.0, 8.0) + paper_door(M, -2.6, 2.0, 2.8, 8.0, 8.0)
        o += [B(M, 4.0, 8.6, 1.2, 7.6, 8.0, 8.6, "wood_dark"), B(M, 3.6, 9.0, 7.6, 8.4, 8.0, 8.9, "wood")]   # kitchen opening
        o += [B(M, -9.4, -4.6, 0.8, 1.4, 8.0, 10.4, "wood")]                               # toenmaru step
        o += [blob(M, 0, 8.4, 0, 12.8, 6.4, 11.6, "thatch", clipy=0.0, paint=[(lambda p: ((p[:, 0] % 2.2) < 0.22) | ((p[:, 1] % 3.0) < 0.2), "thatch_old")]),
              blob(M, 0, 14.4, 0, 10.0, 1.1, 1.6, "thatch_old"),                          # rolled ridge cap
              blob(M, 0, 8.2, 6.0, 12.2, 3.0, 5.0, "thatch", clipy=0.0)]                  # eave droop (front)
        o += [post(M, -13.2, -5.0, 0, 9.5, 1.3, "mud"), post(M, -13.2, -5.0, 9.4, 10.2, 1.7, "mud_dark")]    # chimney
        o += jars(M, 10.2, 10.0, 3) + [blob(M, -10.5, 2.0, 11.0, 2.2, 2.0, 2.2, "thatch_old")]
    else:
        o += [B(M, -10, -2.5, 1.2, 5.2, -8, -4, "mud"), B(M, -10, -6, 1.2, 4.0, -4, 5, "mud"), B(M, 4, 10, 1.2, 3.2, -8, 8, "mud"),
              B(M, -3, 4, 1.2, 6.4, -8, -6, "mud"), B(M, 7, 10, 3.2, 5.5, -8, -3, "mud")]
        o += [B(M, -8, -3, 1.2, 2.3, 6, 7.5, "mud_dark")]
        o += [blob(M, -2, 2.4, 0.5, 9.0, 3.0, 7.0, "thatch", clipy=0.0), blob(M, 4, 2.0, 5.5, 4.0, 1.6, 3.0, "thatch_old", clipy=0.0)]       # collapsed straw
        o += [rod(M, (-8, 1.2, 6), (-1, 8.5, 0.5), 0.55, "wood"), rod(M, (6, 1.2, 4), (0, 6.5, -2), 0.5, "wood"), rod(M, (-4, 3.0, 7.0), (2, 2.5, 3), 0.45, "wood"),
              post(M, 7.6, 7.8, 1.2, 6.8, 0.5, "wood")]
        o += [post(M, -13.2, -5.0, 0, 6.0, 1.3, "mud")]
        o += rubble(M, -9, 9, -6, 9, 10, seed=3)
        o += jars(M, 10.2, 10.0, 1)
        charred(o, keep=("stone", "mud", "mud_dark", "clay"))
    return o


THATCH_BBOX = (-14.5, 14.5, 15.5, -9.0, 12.0)


# ------------------------------------------------------------------------------------------------ (b) tiled-roof Korean house
def _curved_roof(M, x0, x1, kiwa, ridge_y=18.0, zs=(0, 4.5, 9.0, 13.0), ys=(18.2, 14.4, 12.3, 12.6), thick=1.4):
    o = []
    for sgn in (1, -1):
        pts = [(sgn * z, y) for z, y in zip(zs, ys)]
        for (za, ya), (zb, yb) in zip(pts[:-1], pts[1:]):
            o.append(slope(M, x0, x1, zb, yb, za, ya, thick, kiwa, [(p_tile_ridges, "kiwa2")]))
    return o


def tile_house(M=I4, ruin=False):
    """기와집 3x2 tiles (36x24 units): stone base, plaster walls with wood pillars, lattice doors, curved tiled gable roof."""
    o = []
    o += [B(M, -17, 17, 0, 2.4, -10.5, 11.0, "stone", [(lambda X, Y, Z: ((X % 3.0) < 0.2) | ((Y % 1.2) < 0.15), "brick_dark")])]
    o += [B(M, -8, 8, 0, 1.0, 11.0, 13.5, "stone"), B(M, -9, 9, 0, 0.5, 13.5, 15.0, "stone")]            # entry steps
    if not ruin:
        o += [B(M, -14.5, 14.5, 2.4, 11.2, -7.5, 7.5, "plaster")]
        for i in range(6):
            x = -14.5 + i * 5.8
            o += [post(M, x, 7.8, 2.4, 11.4, 0.9, "wood")]
        o += [B(M, -15.2, 15.2, 10.2, 11.6, 7.2, 8.6, "wood")]                                          # tie beam
        for x0, x1 in ((-13.4, -9.3), (-8.4, -3.6), (-2.6, 2.6), (3.6, 8.4), (9.3, 13.4)):
            o += [B(M, x0, x1, 3.6, 10.0, 7.5, 8.2, "paper", [(p_lattice, "wood")])]
        o += [B(M, -15.4, 15.4, 2.4, 3.6, 7.4, 8.4, "wood_dark")]                                       # sill
        o += _curved_roof(M, -19.5, 19.5, "kiwa")
        o += [B(M, -20.2, 20.2, 18.2, 19.8, -1.2, 1.2, "kiwa2")]                                        # ridge cap
        o += [B(M, -19.8, 19.8, 11.6, 12.8, 11.2, 14.2, "wood_dark")]                                   # eave rafters / fascia shadow
        for sx in (-1, 1):
            o += [B(M, sx * 20.0 - 0.9, sx * 20.0 + 0.9, 12.4, 14.4, 12.5, 14.4, "kiwa2"),               # upturned eave tips
                  blob(M, sx * 20.4, 19.2, 0, 1.2, 1.6, 1.8, "kiwa2")]                                  # chimed ridge end tile
        o += [post(M, 19.6, -4.0, 0, 15.0, 1.1, "brick_grey"), B(M, 18.4, 20.8, 15.0, 16.4, -5.2, -2.8, "kiwa2")]  # brick chimney
        o += jars(M, -19.0, 9.0, 4, "clay")
    else:
        o += [B(M, -14.5, -6, 2.4, 9.0, -7.5, -5.5, "plaster"), B(M, -14.5, -12.5, 2.4, 8.0, -7.5, 7.5, "plaster"),
              B(M, 6, 14.5, 2.4, 5.5, -7.5, -5.5, "plaster"), B(M, 11, 14.5, 2.4, 7.0, -7.5, 4, "plaster"),
              B(M, 0, 6, 2.4, 11.0, -7.5, -6.5, "plaster")]
        for x, h in ((-14.5, 10.5), (-8.7, 9.0), (-2.9, 4.5), (8.7, 6.2), (14.5, 8.0)):
            o += [post(M, x, 7.8, 2.4, 2.4 + h * 0.9, 0.9, "wood")]
        o += [post(M, -14.5, -7.5, 2.4, 12.5, 0.9, "wood"), post(M, 2.9, -7.5, 2.4, 9.0, 0.9, "wood")]
        # remaining left third of roof: sagging, then fallen heap
        o += [slope(M, -19.5, -9.0, 11.0, 12.0, 0.0, 17.6, 1.4, "kiwa", [(p_tile_ridges, "kiwa2")]),
              slope(M, -19.5, -9.0, -11.0, 13.0, 0.0, 17.6, 1.4, "kiwa", [(p_tile_ridges, "kiwa2")]),
              B(M, -20.2, -8.0, 17.6, 18.8, -1.2, 1.2, "kiwa2")]
        o += [rod(M, (-12, 2.4, 5), (-1, 10.2, 0), 0.6, "wood"), rod(M, (6, 2.4, 6), (13, 7, 3), 0.5, "wood"), rod(M, (2, 2.4, -6), (9, 8.5, -2), 0.5, "wood")]
        o += [slope(M, -2.0, 11.0, 6.0, 3.6, -3.0, 7.0, 1.2, "kiwa", [(p_tile_ridges, "kiwa2")])]       # collapsed slab on the floor
        o += rubble(M, -8, 16, -8, 9, 14, mats=("kiwa", "char2", "kiwa2", "stone"), seed=5)
        o += [post(M, 19.6, -4.0, 0, 10.0, 1.1, "brick_grey")]
        charred(o, keep=("stone", "brick_grey", "brick_dark", "plaster"))
    return o


TILE_BBOX = (-21.0, 21.0, 21.0, -11.0, 15.5)


# ------------------------------------------------------------------------------------------------ (c) Manchurian brick house
def brick_house(M=I4, ruin=False):
    """만주 벽돌집 3x3 tiles (36x36): grey-brick house with a low earth roof, chimneys, walled courtyard with a gate."""
    o = []
    bp = [(p_brick, "brick_dark")]
    zb0, zb1 = -16.0, -2.0                  # house depth range
    cap = lambda x0, x1, y, z0, z1: B(M, x0, x1, y, y + 1.0, z0, z1, "kiwa")
    if not ruin:
        o += [B(M, -14, 14, 0, 9.4, zb0, zb1, "brick_grey", bp), B(M, -14.4, 14.4, 0, 1.4, zb0 - 0.4, zb1 + 0.4, "stone")]
        o += [slope(M, -15.5, 15.5, zb1 + 2.6, 9.6, zb0 - 0.8, 11.6, 1.6, "mud"),                       # low shed roof, earth + straw
              B(M, -15.5, 15.5, 9.2, 10.4, zb1 + 1.6, zb1 + 2.8, "thatch_old"),
              B(M, -15.5, 15.5, 10.8, 12.2, zb0 - 1.2, zb0 + 0.2, "brick_grey", bp)]
        for x in (-9.5, 0.0, 9.5):
            o += [B(M, x - 2.1, x + 2.1, 3.8, 7.6, zb1 - 0.1, zb1 + 0.4, "wood"), B(M, x - 1.6, x + 1.6, 4.3, 7.1, zb1 + 0.3, zb1 + 0.7, "paper", [(p_lattice, "wood")])]
        o += [B(M, -2.0, 2.0, 1.4, 7.0, zb1 - 0.1, zb1 + 0.5, "wood_dark")]                             # centre door (replaces middle window)
        o += [B(M, -2.6, 2.6, 0.0, 1.4, zb1, zb1 + 2.2, "stone")]
        o += [B(M, 9.6, 12.4, 0, 15.5, -13.5, -11.0, "brick_grey", bp), B(M, 9.2, 12.8, 15.5, 16.4, -13.9, -10.6, "kiwa")]   # chimney
        o += [B(M, -12.4, -10.0, 0, 13.0, -13.0, -10.6, "brick_grey", bp), B(M, -12.8, -9.6, 13.0, 13.8, -13.4, -10.2, "kiwa")]
        # courtyard wall
        wp = [(p_brick, "brick_dark")]
        o += [B(M, -17.5, -3.5, 0, 4.2, 15.0, 16.2, "brick_grey", wp), B(M, 3.5, 17.5, 0, 4.2, 15.0, 16.2, "brick_grey", wp),
              B(M, -17.5, -3.5, 4.2, 5.0, 14.8, 16.4, "kiwa"), B(M, 3.5, 17.5, 4.2, 5.0, 14.8, 16.4, "kiwa"),
              B(M, -17.5, -16.3, 0, 4.2, zb1, 16.2, "brick_grey", wp), B(M, 16.3, 17.5, 0, 4.2, zb1, 16.2, "brick_grey", wp),
              B(M, -17.7, -16.1, 4.2, 5.0, zb1, 16.4, "kiwa"), B(M, 16.1, 17.7, 4.2, 5.0, zb1, 16.4, "kiwa")]
        o += [B(M, -4.6, -3.0, 0, 7.6, 14.6, 16.6, "brick_grey", wp), B(M, 3.0, 4.6, 0, 7.6, 14.6, 16.6, "brick_grey", wp),
              B(M, -5.6, 5.6, 7.6, 8.4, 14.2, 17.0, "kiwa"), B(M, -3.4, 3.4, 8.4, 9.6, 14.8, 16.4, "kiwa2"),
              B(M, -3.0, -0.1, 0.8, 6.8, 15.2, 15.8, "wood"), B(M, 0.1, 3.0, 0.8, 6.8, 15.2, 15.8, "wood")]    # double gate
        o += jars(M, 10.5, 4.0, 3) + [B(M, -14.0, -9.0, 0, 2.2, 0.5, 4.0, "wood", [(p_planks, "wood_dark")]),
                                       blob(M, -6.5, 1.4, 6.0, 2.0, 1.4, 2.0, "stone"), post(M, -6.5, 6.0, 1.4, 2.4, 0.6, "wood_dark")]
    else:
        o += [B(M, -14, 14, 0, 1.4, zb0 - 0.4, zb1 + 0.4, "stone")]
        o += [B(M, -14, -4, 1.4, 8.0, zb0, zb1, "brick_grey", bp), B(M, -4, 3, 1.4, 4.2, zb0, zb1, "brick_grey", bp),
              B(M, 3, 8, 1.4, 6.4, zb0, zb0 + 1.6, "brick_grey", bp), B(M, 12, 14, 1.4, 6.0, zb0, zb1, "brick_grey", bp),
              B(M, -14, -12, 8.0, 10.0, zb0, zb0 + 1.6, "brick_grey", bp)]
        o += [B(M, 9.6, 12.4, 0, 15.5, -13.5, -11.0, "brick_grey", bp)]
        o += [slope(M, -12.0, -3.0, zb1 + 1.0, 4.0, zb0 + 4.0, 5.2, 1.4, "char"), rod(M, (-10, 8, -9), (-2, 3.0, -1), 0.5, "char"), rod(M, (6, 1.4, -4), (11, 3.2, 3), 0.5, "char")]
        o += [B(M, -17.5, -9.0, 0, 3.0, 15.0, 16.2, "brick_grey", [(p_brick, "brick_dark")]), B(M, 8.0, 17.5, 0, 2.2, 15.0, 16.2, "brick_grey", [(p_brick, "brick_dark")]),
              B(M, -17.5, -16.3, 0, 3.6, zb1, 12.0, "brick_grey", [(p_brick, "brick_dark")]), B(M, 16.3, 17.5, 0, 2.2, 0.0, 16.2, "brick_grey", [(p_brick, "brick_dark")]),
              B(M, -4.6, -3.0, 0, 6.2, 14.6, 16.6, "brick_grey", [(p_brick, "brick_dark")]), B(M, 3.0, 4.6, 0, 3.0, 14.6, 16.6, "brick_grey", [(p_brick, "brick_dark")])]
        o += [rod(M, (-3, 0.6, 15.9), (2.5, 2.0, 14.0), 0.5, "char"), rod(M, (-2, 0.5, 11), (3, 1.2, 8), 0.5, "char")]
        o += rubble(M, -16, 16, -15, 14, 22, mats=("brick_grey", "char2", "brick_dark", "stone"), seed=7)
        charred(o, keep=("stone", "brick_grey", "brick_dark"))
    return o


BRICK_BBOX = (-18.5, 18.5, 20.5, -17.5, 17.5)


# ------------------------------------------------------------------------------------------------ (d) wooden storehouse / barn
def barn(M=I4, ruin=False):
    """창고 2x2 tiles (24x24): plank barn on stone piers, steep thatch gable roof, big double door, hay and sacks."""
    o = []
    pl = [(p_planks, "wood_dark")]
    if not ruin:
        for x in (-9.5, 0, 9.5):
            for z in (-8, 8):
                o += [post(M, x, z, 0, 1.6, 1.2, "stone")]
        o += [B(M, -10.5, 10.5, 1.6, 2.4, -9.0, 9.0, "wood_dark")]
        o += [B(M, -10, 10, 2.4, 11.0, -8.4, 8.4, "wood", pl)]
        for x in (-10, 10):
            o += [B(M, x - 0.5, x + 0.5, 2.4, 11.4, 8.0, 8.9, "wood_dark")]
        o += [B(M, -4.2, -0.15, 2.9, 9.6, 8.4, 9.0, "wood", [(lambda X, Y, Z: ((X % 1.1) < 0.14) | (abs(Y - 6.2) < 0.2), "wood_dark")]),
              B(M, 0.15, 4.2, 2.9, 9.6, 8.4, 9.0, "wood", [(lambda X, Y, Z: ((X % 1.1) < 0.14) | (abs(Y - 6.2) < 0.2), "wood_dark")]),
              B(M, -4.8, 4.8, 9.6, 10.4, 8.3, 9.2, "wood_dark")]
        o += [B(M, -3.6, 3.6, 0.8, 2.4, 9.0, 12.0, "wood")] + [B(M, -3.6, 3.6, 0.0, 0.8, 11.2, 13.2, "wood_dark")]   # steps
        o += [slope(M, -12.8, 12.8, 10.2, 10.4, 0.0, 17.0, 2.2, "thatch", [(p_tile_ridges, "thatch_old")]),
              slope(M, -12.8, 12.8, -10.2, 10.4, 0.0, 17.0, 2.2, "thatch", [(p_tile_ridges, "thatch_old")]),
              blob(M, 0, 17.0, 0, 13.2, 1.2, 1.5, "thatch_old")]
        o += [blob(M, 14.0, 2.6, 6.0, 3.6, 2.6, 3.0, "thatch"), blob(M, 14.5, 2.0, 1.0, 3.0, 2.0, 2.6, "thatch_old"),
              blob(M, -14.0, 1.2, 8.0, 1.9, 1.2, 1.6, "mud"), blob(M, -12.0, 1.0, 10.0, 1.7, 1.0, 1.5, "paper"), blob(M, -15.0, 1.1, 10.4, 1.6, 1.1, 1.5, "paper")]
    else:
        for x in (-9.5, 0, 9.5):
            for z in (-8, 8):
                o += [post(M, x, z, 0, 1.6, 1.2, "stone")]
        o += [B(M, -10.5, 4.0, 1.6, 2.4, -9.0, 9.0, "wood_dark")]
        o += [B(M, -10, -3.0, 2.4, 8.0, -8.4, 8.4, "wood", pl), B(M, -3.0, 2.0, 2.4, 5.0, -8.4, 3.0, "wood", pl), B(M, 6.0, 10, 2.4, 3.6, -8.4, -2.0, "wood", pl)]
        o += [rod(M, (-10, 8, 8), (-3, 11.5, 2), 0.7, "wood"), rod(M, (-10, 8, -8), (-4, 12, -2), 0.7, "wood")]
        o += [slope(M, -12.0, 3.0, 8.0, 7.2, 0.0, 12.0, 2.0, "thatch"), rod(M, (2, 8.0, 7), (7, 2.5, 12.5), 0.9, "wood")]       # roof tilted down
        o += [blob(M, 5.0, 1.8, 6.0, 8.0, 2.4, 6.0, "thatch", clipy=0.0), blob(M, 8.5, 1.2, -1.0, 4.5, 1.4, 3.5, "thatch_old", clipy=0.0)]
        o += [rod(M, (-4, 0.8, 11), (4, 1.0, 9), 0.5, "wood"), rod(M, (1, 0.8, 12.5), (9, 0.8, 11), 0.5, "wood"), rod(M, (8, 0.6, 3), (13, 0.7, 6), 0.45, "wood")]
        o += rubble(M, -13, 13, -9, 12, 12, seed=11)
        charred(o, keep=("stone",))
    return o


BARN_BBOX = (-16.5, 16.5, 19.5, -10.5, 13.5)

HOUSES = {   # name -> (builder, bbox, footprint tiles (w, d), korean label)
    "house-thatch": (thatch_house, THATCH_BBOX, (2, 2), "초가집"),
    "house-tile":   (tile_house, TILE_BBOX, (3, 2), "기와집"),
    "house-brick":  (brick_house, BRICK_BBOX, (3, 3), "만주 벽돌집"),
    "house-barn":   (barn, BARN_BBOX, (2, 2), "창고"),
}
