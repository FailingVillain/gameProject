"""T39i: two ruin variants per civilian house type (a = walls broken + standing remnants, b = roof pancaked / skeleton).
Same bbox, base and light as the intact houses (houses.py), so cells align. Builders return prim lists; M = placement transform."""
from __future__ import annotations
import numpy as np
from bcommon import B, slope, post, blob, rod, jars, rubble, RUIN_MAP, p_brick, p_planks, p_lattice, p_tile_ridges
from houses import I4, charred, THATCH_BBOX, TILE_BBOX, BRICK_BBOX, BARN_BBOX


def wall_run(M, z0, z1, y0, segs, mat, paint=None):
    """broken wall: segs = [(x0, x1, top_y)] boxes from y0 up to a jagged top"""
    return [B(M, xa, xb, y0, top, z0, z1, mat, paint) for xa, xb, top in segs if top > y0 + 0.2]


def heap(M, cx, cz, w, d, h, mats, seed, n=9):
    """pile of fallen material: a main mound + scattered chunks"""
    rng = np.random.RandomState(seed)
    o = [blob(M, cx, 0.4, cz, w, h, d, mats[0], clipy=0.0)]
    for i in range(n):
        x = cx + rng.uniform(-w, w) * 0.9
        z = cz + rng.uniform(-d, d) * 0.9
        s = rng.uniform(0.7, 1.5)
        ytop = h * max(0.15, 1.0 - (abs(x - cx) / w) ** 2 - (abs(z - cz) / d) ** 2) + 0.4
        o.append(blob(M, x, ytop * 0.8, z, s * 1.3, s * 0.65, s * 1.0, mats[(i + 1) % len(mats)]))
    return o


# ------------------------------------------------------------------------------------------------ tile house (3x2)
def tile_ruin(M=I4, v="a"):
    o = [B(M, -17, 17, 0, 2.4, -10.5, 11.0, "stone", [(lambda X, Y, Z: ((X % 3.0) < 0.2) | ((Y % 1.2) < 0.15), "brick_dark")]),
         B(M, -8, 8, 0, 1.0, 11.0, 13.5, "stone"), B(M, -9, 9, 0, 0.5, 13.5, 15.0, "stone")]
    y0 = 2.4
    if v == "a":   # front wall gone, back wall + left end stand broken, one roof truss leaning
        o += wall_run(M, -7.5, -5.5, y0, [(-14.5, -10.0, 11.0), (-10.0, -6.0, 8.5), (-6.0, -2.0, 5.2), (-2.0, 3.0, 7.2), (3.0, 8.0, 3.8), (8.0, 14.5, 6.2)], "plaster")
        o += [B(M, -14.5, -12.5, y0, 10.4, -7.5, 1.5, "plaster"), B(M, -14.5, -12.5, y0, 6.0, 1.5, 7.5, "plaster")]
        o += [B(M, 11.5, 14.5, y0, 4.2, -5.5, 3.0, "plaster")]                                     # right end stub
        for x, h in ((-14.5, 11.8), (-8.7, 9.6), (-2.9, 4.8), (2.9, 6.5), (8.7, 3.4)):
            o += [post(M, x, -7.8 if x < 3 else 7.8, y0, y0 + h, 0.9, "wood")]
        o += [post(M, 14.5, 7.8, y0, y0 + 2.2, 0.9, "wood"), post(M, -14.5, 7.8, y0, y0 + 7.0, 0.9, "wood")]
        # remaining roof: left third still hangs on the end wall, sagging toward the middle
        o += [slope(M, -19.5, -9.0, 11.0, 11.5, 0.0, 17.6, 1.4, "kiwa", [(p_tile_ridges, "kiwa2")]),
              slope(M, -19.5, -9.0, -11.0, 13.2, 0.0, 17.6, 1.4, "kiwa", [(p_tile_ridges, "kiwa2")]),
              B(M, -20.2, -8.0, 17.6, 18.8, -1.2, 1.2, "kiwa2")]
        o += [rod(M, (-9, 17.0, 0), (1, 6.2, 1.5), 0.7, "wood"), rod(M, (-9, 15.5, 1.0), (-1, 3.4, 6.0), 0.5, "wood"),
              rod(M, (-9, 15.5, -1.0), (0, 5.2, -5.2), 0.5, "wood")]                          # purlins dangling
        o += heap(M, 6.0, 1.5, 9.5, 6.5, 3.0, ("kiwa", "kiwa2", "char2", "plaster"), 5, 11)
        o += heap(M, -4.5, 9.0, 6.0, 3.0, 1.4, ("plaster", "kiwa", "stone"), 6, 6)
        o += [slope(M, 4.0, 15.0, 8.5, 2.6, 2.0, 4.8, 1.2, "kiwa", [(p_tile_ridges, "kiwa2")])]   # broken slab sliding out the front
        o += [post(M, 19.6, -4.0, 0, 10.0, 1.1, "brick_grey")]
    else:          # roof pancaked onto stub walls, tilted slabs, two end pillars survive
        o += wall_run(M, -7.5, -5.5, y0, [(-14.5, -8.0, 4.6), (-8.0, -3.0, 3.0), (-3.0, 4.0, 5.6), (4.0, 14.5, 3.4)], "plaster")
        o += wall_run(M, 5.5, 7.5, y0, [(-14.5, -10.0, 3.0), (-6.0, -1.0, 2.0 + 0.6), (3.0, 8.0, 4.0), (8.0, 14.5, 2.9)], "plaster")
        o += [B(M, -14.5, -12.5, y0, 4.0, -7.5, 7.5, "plaster"), B(M, 12.5, 14.5, y0, 5.2, -7.5, 7.5, "plaster")]
        o += [post(M, -14.5, -7.8, y0, y0 + 9.5, 0.9, "wood"), post(M, 14.5, 7.8, y0, y0 + 8.4, 0.9, "wood"), post(M, 0.0, -7.8, y0, y0 + 3.0, 0.9, "wood")]
        # pancaked roof: two big slabs resting on the stubs, a gap between, edge tilted
        o += [slope(M, -19.5, -1.0, 7.6, 5.4, -7.6, 8.4, 1.5, "kiwa", [(p_tile_ridges, "kiwa2")]),
              slope(M, 2.0, 19.5, 7.6, 5.0, -4.0, 5.4, 1.5, "kiwa", [(p_tile_ridges, "kiwa2")]),
              slope(M, -12.0, 4.0, 11.0, 3.2, 7.8, 4.4, 1.3, "kiwa", [(p_tile_ridges, "kiwa2")]),
              B(M, -3.0, 9.0, 4.4, 5.6, -0.6, 1.2, "kiwa2")]
        o += [rod(M, (-16, 3.8, 3), (-5, 8.6, 6), 0.6, "wood"), rod(M, (4, 5.0, 6), (16, 3.2, 9), 0.6, "wood"), rod(M, (-3, 4.6, -6.5), (7, 5.6, -5.5), 0.5, "wood")]
        o += heap(M, -13.0, 5.5, 5.5, 4.0, 2.0, ("kiwa", "char2", "kiwa2"), 8, 8)
        o += heap(M, 9.0, 10.0, 7.0, 2.6, 1.6, ("kiwa2", "stone", "plaster"), 9, 7)
        o += rubble(M, -16, 16, 8, 11, 7, mats=("kiwa", "char2", "stone"), seed=21)
        o += [post(M, 19.6, -4.0, 0, 15.0, 1.1, "brick_grey"), B(M, 18.4, 20.8, 15.0, 16.4, -5.2, -2.8, "kiwa2")]   # chimney survives
    return charred(o, keep=("stone", "brick_grey", "brick_dark", "plaster"))


# ------------------------------------------------------------------------------------------------ thatch house (2x2)
def thatch_ruin(M=I4, v="a"):
    o = [B(M, -10.5, 10.5, 0, 1.2, -8.5, 9.5, "stone")]
    y0 = 1.2
    if v == "a":   # mud walls broken to stumps, straw collapsed in a heap, chimney stands
        o += wall_run(M, -8, -6, y0, [(-10, -4, 7.0), (-4, 2, 3.4), (2, 10, 5.2)], "mud")
        o += [B(M, -10, -8, y0, 6.0, -6, 3, "mud"), B(M, 8, 10, y0, 4.0, -6, 7, "mud"), B(M, 8, 10, y0, 2.4, 7, 8, "mud")]
        o += [B(M, -10, -7, y0, 2.2, 5, 8, "mud_dark"), B(M, 2, 5, y0, 2.0, 7, 8, "mud_dark")]
        o += [post(M, -13.2, -5.0, 0, 11.0, 1.3, "mud"), post(M, -13.2, -5.0, 10.9, 11.6, 1.5, "mud_dark")]        # chimney keeps standing
        o += [blob(M, -1.0, 2.6, 0.5, 10.0, 3.6, 7.5, "thatch", clipy=0.0), blob(M, 4.5, 2.0, 4.0, 5.0, 2.0, 3.5, "thatch_old", clipy=0.0)]
        o += [rod(M, (-8, 7.0, -6), (0, 3.8, 3), 0.6, "wood"), rod(M, (8, 5.0, -5), (-2, 3.0, 4), 0.55, "wood"), rod(M, (-7, 6.0, 3), (3, 2.6, 7.5), 0.5, "wood")]
        o += heap(M, 4.0, 6.0, 5.0, 3.0, 1.2, ("thatch_old", "mud_dark", "clay"), 3, 5)
    else:          # fire gutted: charred roof frame still standing as a skeleton, walls half height, ash
        o += wall_run(M, -8, -6, y0, [(-10, -2, 3.2), (-2, 10, 2.2)], "mud")
        o += [B(M, -10, -8, y0, 3.0, -6, 8, "mud"), B(M, 8, 10, y0, 2.6, -6, 8, "mud"), B(M, -10, -3, y0, 1.8, 7.6, 8.4, "mud_dark")]
        for x in (-8.8, 0.0, 8.8):
            o += [post(M, x, -7.0, y0, y0 + 9.5, 0.55, "wood"), post(M, x, 7.0, y0, y0 + 6.0 if x else y0 + 9.5, 0.55, "wood")]
        o += [rod(M, (-8.8, 10.7, -7), (0, 15.4, -1), 0.5, "wood"), rod(M, (0, 15.4, -1), (8.8, 11.0, 6), 0.5, "wood"), rod(M, (-8.8, 10.7, 7), (0, 15.4, 0), 0.5, "wood"),
              rod(M, (0, 15.4, 0), (8.8, 7.0, 7), 0.45, "wood"), rod(M, (-9, 10.7, -7), (-9, 10.7, 7), 0.45, "wood"), rod(M, (9, 11.0, -7), (9, 8.0, 7), 0.45, "wood"),
              rod(M, (-6, 12.8, 3), (6, 12.6, -4), 0.4, "wood")]
        o += [blob(M, -4.0, 1.9, 0.0, 6.0, 1.5, 5.5, "thatch_old", clipy=0.0), blob(M, 5.0, 1.7, 1.5, 4.5, 1.2, 4.0, "mud_dark", clipy=0.0)]
        o += [post(M, -13.2, -5.0, 0, 5.0, 1.3, "mud")]
        o += rubble(M, -10, 10, -6, 9, 8, seed=17)
    return charred(o, keep=("stone", "mud", "mud_dark", "clay"))


# ------------------------------------------------------------------------------------------------ brick house (3x3)
def brick_ruin(M=I4, v="a"):
    bp = [(p_brick, "brick_dark")]
    zb0, zb1 = -16.0, -2.0
    o = [B(M, -14, 14, 0, 1.4, zb0 - 0.4, zb1 + 0.4, "stone")]
    if v == "a":   # house shell: back and gable walls up with window holes, front half down, courtyard wall broken
        o += [B(M, -14, 14, 1.4, 4.0, zb0, zb0 + 1.6, "brick_grey", bp),
              B(M, -14, -4, 4.0, 9.0, zb0, zb0 + 1.6, "brick_grey", bp), B(M, 3, 9, 4.0, 7.0, zb0, zb0 + 1.6, "brick_grey", bp),
              B(M, -14, -12, 1.4, 9.6, zb0, zb1, "brick_grey", bp), B(M, 12, 14, 1.4, 5.2, zb0, zb1 - 3, "brick_grey", bp)]
        o += [B(M, -14, -8.5, 1.4, 4.4, zb1 - 1.6, zb1, "brick_grey", bp), B(M, -3.5, 1.5, 1.4, 2.6, zb1 - 1.6, zb1, "brick_grey", bp)]
        o += [B(M, 9.6, 12.4, 0, 15.5, -13.5, -11.0, "brick_grey", bp)]                                       # chimney stays
        o += [slope(M, -13.0, -4.0, zb1 + 1.0, 3.8, zb0 + 4.0, 6.4, 1.4, "char"), slope(M, 2.0, 11.0, zb1 + 2.0, 2.0, zb0 + 5.0, 4.0, 1.4, "mud"),
              rod(M, (-9, 8.5, -9), (-1, 3.0, -2), 0.5, "char"), rod(M, (6, 1.6, -3), (11, 3.4, 4), 0.5, "char")]
        o += [B(M, -17.5, -9.0, 0, 3.4, 15.0, 16.2, "brick_grey", bp), B(M, 8.0, 17.5, 0, 2.0, 15.0, 16.2, "brick_grey", bp),
              B(M, -17.5, -16.3, 0, 3.0, zb1, 12.0, "brick_grey", bp), B(M, 16.3, 17.5, 0, 2.2, 2.0, 16.2, "brick_grey", bp),
              B(M, -4.6, -3.0, 0, 7.0, 14.6, 16.6, "brick_grey", bp), B(M, 3.0, 4.6, 0, 3.0, 14.6, 16.6, "brick_grey", bp)]
        o += heap(M, 0.0, 5.0, 9.0, 5.0, 2.0, ("brick_grey", "char2", "brick_dark", "stone"), 7, 12)
    else:          # house flattened to a brick heap, chimney + gate posts + a stub of back wall
        o += [B(M, -14, -9, 1.4, 5.4, zb0, zb0 + 1.6, "brick_grey", bp), B(M, 1, 8, 1.4, 3.2, zb0, zb0 + 1.6, "brick_grey", bp),
              B(M, 12, 14, 1.4, 4.2, zb0, zb1, "brick_grey", bp)]
        o += [B(M, 9.6, 12.4, 0, 15.5, -13.5, -11.0, "brick_grey", bp), B(M, -12.4, -10.0, 0, 7.0, -13.0, -10.6, "brick_grey", bp)]
        o += heap(M, -2.0, -8.0, 11.0, 5.0, 3.6, ("brick_grey", "char2", "brick_dark", "char"), 12, 14)
        o += [rod(M, (-10, 4.0, -9), (2, 2.8, -4), 0.55, "char"), rod(M, (4, 3.4, -11), (12, 2.0, -3), 0.5, "char"), rod(M, (-6, 3.0, -4), (-2, 1.8, 3), 0.5, "char")]
        o += [B(M, -4.6, -3.0, 0, 6.2, 14.6, 16.6, "brick_grey", bp), B(M, 3.0, 4.6, 0, 4.4, 14.6, 16.6, "brick_grey", bp),
              B(M, -17.5, -6.0, 0, 2.4, 15.0, 16.2, "brick_grey", bp), B(M, 10.0, 17.5, 0, 2.8, 15.0, 16.2, "brick_grey", bp),
              B(M, 16.3, 17.5, 0, 2.6, -2.0, 10.0, "brick_grey", bp)]
        o += [rod(M, (-3, 0.6, 15.9), (2.5, 2.0, 13.0), 0.5, "char")]
        o += rubble(M, -16, 16, 2, 14, 12, mats=("brick_grey", "char2", "brick_dark"), seed=31)
    return charred(o, keep=("stone", "brick_grey", "brick_dark"))


# ------------------------------------------------------------------------------------------------ barn (2x2)
def barn_ruin(M=I4, v="a"):
    pl = [(p_planks, "wood_dark")]
    o = []
    for x in (-9.5, 0, 9.5):
        for z in (-8, 8):
            o += [post(M, x, z, 0, 1.6, 1.2, "stone")]
    if v == "a":   # one side wall still standing with a leaning gable, floor half gone
        o += [B(M, -10.5, 4.0, 1.6, 2.4, -9.0, 9.0, "wood_dark")]
        o += [B(M, -10, -3.0, 2.4, 10.0, -8.4, -6.0, "wood", pl), B(M, -10, -9, 2.4, 11.0, -8.4, 8.4, "wood", pl),
              B(M, -10, 2.0, 2.4, 5.0, -8.4, -6.8, "wood", pl), B(M, 6.0, 10, 2.4, 3.6, -8.4, -6.0, "wood", pl)]
        o += [rod(M, (-10, 11, 8), (-1, 14.5, 0), 0.7, "wood"), rod(M, (-10, 11, -8), (-2, 15.0, 0), 0.7, "wood"),
              rod(M, (-2, 15.0, 0), (6, 6.0, 5), 0.6, "wood"), rod(M, (4, 3.4, 8), (10, 2.2, -2), 0.5, "wood")]
        o += [slope(M, -10.0, 6.0, 7.0, 4.0, 1.0, 9.6, 2.0, "thatch"), blob(M, 6.0, 1.8, 6.0, 7.0, 2.2, 5.0, "thatch", clipy=0.0)]
        o += rubble(M, -13, 13, -4, 12, 10, seed=13)
    else:          # roof dropped to the floor: a big thatch heap, door leaf lying flat, posts snapped
        o += [B(M, -10.5, 10.5, 1.6, 2.4, -9.0, 9.0, "wood_dark")]
        o += [B(M, -10, -4, 2.4, 4.4, -8.4, -6.8, "wood", pl), B(M, 5.0, 10, 2.4, 6.2, -8.4, -6.8, "wood", pl),
              B(M, 9.4, 10, 2.4, 5.2, -8.4, 4.0, "wood", pl)]
        o += [blob(M, 0.0, 3.0, 1.0, 11.0, 6.6, 8.0, "thatch", clipy=0.0, paint=[(lambda p: ((p[:, 0] % 2.2) < 0.22) | ((p[:, 1] % 3.0) < 0.2), "thatch_old")]),
              blob(M, -4.0, 3.4, 4.0, 5.0, 2.4, 4.0, "thatch_old", clipy=0.0)]
        o += [rod(M, (-11, 4, -6), (8, 8.5, 4), 0.8, "wood"), rod(M, (-8, 4.2, 7), (10, 7.2, -6), 0.8, "wood"), rod(M, (-3, 6.0, 0), (5, 2.0, 11), 0.6, "wood"),
              rod(M, (6, 4.0, -7), (13, 1.0, 3), 0.5, "wood")]
        o += [B(M, -4.0, 4.0, 0.8, 1.6, 9.2, 13.4, "wood", pl)]
        o += rubble(M, -13, 13, -9, 13, 9, seed=19)
    return charred(o, keep=("stone",))


RUINS = {   # name -> (builder, bbox); variants "a" / "b"
    "house-thatch": (thatch_ruin, THATCH_BBOX),
    "house-tile": (tile_ruin, TILE_BBOX),
    "house-brick": (brick_ruin, BRICK_BBOX),
    "house-barn": (barn_ruin, BARN_BBOX),
}
