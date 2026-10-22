"""Shared helpers for buildings: materials, resolution profile (128/64/32 px per tile), prim helpers, render.

Model units: 1 tile = 12 units (x = right, y = up, z = toward the camera, yaw 0 = fixed orientation).
Anchor = ground point under the centre of the footprint. Frame sizes are multiples of 4 so LODs are exact halves.
"""
from __future__ import annotations
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
import voxel as V  # noqa: E402
from voxel import Res, make_frame, tr, rx, ry, rz, ell, cyl, box  # noqa: E402

TILE = 12.0                       # model units per tile
TILE_PX = [128, 64, 32]           # px per tile at LOD full / half / quarter
OUTLINE = (8, 8, 8, 255)
OUT_PX = [3, 2, 1]
LINE_PX = [2, 1, 1]
AO_R = [3, 2, 1]
SIN, COS = math.sin(V.ELEV), math.cos(V.ELEV)

TONE = 0.84                       # global darkening of the building palette (keeps the lit side from washing out)
MATS = {
    "thatch":     [(92, 70, 34), (128, 100, 50), (166, 134, 70), (200, 170, 98)],
    "thatch_old": [(70, 58, 36), (98, 82, 50), (126, 106, 66), (152, 130, 84)],
    "mud":        [(96, 76, 52), (126, 102, 72), (156, 130, 96), (184, 158, 122)],
    "mud_dark":   [(70, 54, 38), (92, 72, 50), (114, 92, 66), (138, 114, 86)],
    "wood":       [(52, 36, 24), (78, 54, 34), (104, 76, 48), (130, 98, 64)],
    "wood_dark":  [(30, 22, 16), (46, 34, 24), (64, 48, 34), (84, 64, 46)],
    "plaster":    [(120, 116, 104), (158, 154, 140), (196, 192, 176), (226, 222, 206)],
    "paper":      [(150, 140, 112), (184, 174, 144), (214, 204, 172), (236, 228, 198)],
    "kiwa":       [(30, 32, 36), (46, 50, 56), (64, 70, 78), (88, 96, 106)],
    "kiwa2":      [(46, 48, 52), (68, 72, 80), (92, 98, 108), (120, 128, 140)],
    "brick_grey": [(70, 72, 74), (98, 100, 102), (126, 128, 128), (152, 154, 152)],
    "brick_dark": [(46, 48, 50), (64, 66, 68), (84, 86, 88), (106, 108, 108)],
    "stone":      [(74, 70, 64), (100, 96, 88), (128, 122, 112), (154, 148, 136)],
    "path":       [(104, 86, 60), (130, 110, 80), (152, 132, 100), (172, 152, 118)],
    "leaf":       [(28, 54, 28), (44, 80, 38), (64, 106, 50), (90, 134, 66)],
    "leaf2":      [(36, 62, 30), (58, 90, 40), (82, 116, 54), (108, 142, 72)],
    "bark":       [(40, 30, 22), (58, 44, 32), (78, 60, 44), (98, 78, 58)],
    "clay":       [(70, 40, 28), (100, 60, 38), (130, 82, 52), (158, 106, 68)],
    "char":       [(18, 16, 14), (30, 27, 24), (46, 42, 38), (64, 60, 54)],
    "char2":      [(40, 38, 34), (60, 57, 52), (82, 78, 72), (106, 102, 94)],
    # --- appended for housegen (T55g); append-only, never reorder above ---
    "earth":      [(104, 82, 56), (134, 108, 76), (162, 136, 100), (188, 162, 124)],    # rammed earth, yellow-brown
    "earth_pale": [(124, 106, 80), (154, 134, 102), (180, 160, 126), (204, 186, 152)],  # dry pale loess
    "brick_red":  [(84, 44, 36), (116, 62, 48), (146, 82, 62), (172, 104, 80)],
    "kiwa_blue":  [(28, 50, 58), (42, 76, 86), (60, 104, 114), (84, 132, 140)],         # blue-green glazed tile
    "tile_red":   [(86, 42, 32), (120, 62, 44), (152, 84, 58), (180, 108, 76)],         # terracotta tile
    "thatch_gold": [(118, 90, 40), (160, 126, 60), (198, 164, 88), (226, 196, 118)],    # fresh straw
}


def register_materials():
    for name, a in MATS.items():
        if name in V.MATERIALS:
            continue
        k = 1.0 if name.startswith('char') else TONE
        V.MATERIALS[name] = V._ramp8([tuple(int(v * k) for v in c[:3]) + (255,) for c in a])   # append-only: existing material order untouched
        V.MAT_NAMES.append(name)
        V.MAT_IDX[name] = len(V.MAT_NAMES) - 1


register_materials()

RUIN_MAP = {"thatch": "char", "thatch_old": "char", "plaster": "char2", "kiwa": "char", "kiwa2": "char2", "paper": "char",
            "wood": "char", "wood_dark": "char", "mud": "mud_dark", "clay": "mud_dark",
            "earth": "mud_dark", "earth_pale": "mud_dark", "brick_red": "brick_dark", "kiwa_blue": "char", "tile_red": "char2", "thatch_gold": "char"}


def mapper(ruin):
    return (lambda n: RUIN_MAP.get(n, n)) if ruin else (lambda n: n)


class BRes(Res):
    """px/tile driven profile with an explicit frame; line widths from tables (outline 3/2/1 px)."""

    def __init__(self, lod, fw, fh, ax, ay):
        super().__init__(fw, fh)
        self.lod = lod
        self.fw, self.fh, self.ax, self.ay = fw, fh, ax, ay
        self.zoom = TILE_PX[lod] / TILE
        self.outline_px = OUT_PX[lod]
        self.line_px = LINE_PX[lod]
        self.ao_rad = AO_R[lod]


def frame_for(bbox, margin_px=16):
    """bbox = (x0, x1, ymax, z0, z1) model units -> (fw, fh, ax, ay) at the full LOD (multiples of 4)."""
    x0, x1, ymax, z0, z1 = bbox
    z = TILE_PX[0] / TILE
    half = max(abs(x0), abs(x1)) * z + margin_px
    fw = int(math.ceil(2 * half / 4.0) * 4)
    top = (ymax * COS - z0 * SIN) * z + margin_px
    bot = (z1 * SIN) * z + margin_px
    ay = int(math.ceil(top / 4.0) * 4)
    fh = ay + int(math.ceil(bot / 4.0) * 4)
    return fw, fh, fw // 2, ay


def make_res(lod, geom):
    fw, fh, ax, ay = geom
    s = 2 ** lod
    return BRes(lod, fw // s, fh // s, ax // s, ay // s)


def render(prims, res):
    f = make_frame(prims, [], 0, res=res, outline=False, yoff=40.0, line_thr=2.5, ao_thr=1.6)
    f.outline(color=OUTLINE, width=res.outline_px)
    return f


# ------------------------------------------------------------------ prim helpers
def _wrap(paint, cx, cy, cz):
    return [((lambda f: (lambda p: f(p[:, 0] + cx, p[:, 1] + cy, p[:, 2] + cz)))(fn), m) for fn, m in (paint or [])]


def B(M, x0, x1, y0, y1, z0, z1, mat, paint=None):
    """axis-aligned box from extents; paint = [(fn(X, Y, Z), mat)] evaluated in building coordinates."""
    cx, cy, cz = (x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2
    return box(M @ tr(cx, cy, cz), (x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2, mat, _wrap(paint, cx, cy, cz))


def slope(M, x0, x1, ze, ye, zr, yr, thick, mat, paint=None):
    """roof plane (ridge along x): top surface passes through (ze, ye) and (zr, yr). paint fn gets (X, along-slope s, 0)."""
    dz, dy = zr - ze, yr - ye
    if dz < 0:
        dz, dy = -dz, -dy
    L = math.hypot(dz, dy)
    a = -math.degrees(math.atan2(dy, dz))
    cx, cz, cy = (x0 + x1) / 2, (ze + zr) / 2, (ye + yr) / 2
    cy -= thick / 2 * (dz / L)
    cz += thick / 2 * (dy / L) * (1 if zr > ze else -1) * 0
    pp = [((lambda f: (lambda p: f(p[:, 0] + cx, p[:, 2], 0 * p[:, 0])))(fn), m) for fn, m in (paint or [])]
    return box(M @ tr(cx, cy, cz) @ rx(a), (x1 - x0) / 2, thick / 2, L / 2, mat, pp)


def post(M, x, z, y0, y1, r, mat):
    return cyl(M @ tr(x, (y0 + y1) / 2, z), r, (y1 - y0) / 2, mat)


def blob(M, x, y, z, rx_, ry_, rz_, mat, clipy=None, paint=None):
    return ell(M @ tr(x, y, z), rx_, ry_, rz_, mat, clipy, paint)


def rod(M, p0, p1, r, mat):
    return V.seg(np.array(p0, float), np.array(p1, float), r, mat, flat=True).moved(M)


def tree(M, x, z, h=14.0, r=6.0, kind=0):
    out = [cyl(M @ tr(x, h * 0.3, z), 0.9, h * 0.3, "bark")]
    if kind == 0:     # round crown (zelkova)
        out += [blob(M, x, h * 0.75, z, r, r * 0.8, r, "leaf"), blob(M, x - r * 0.45, h * 0.6, z + 1.5, r * 0.7, r * 0.6, r * 0.7, "leaf2"),
                blob(M, x + r * 0.5, h * 0.62, z - 1.0, r * 0.65, r * 0.6, r * 0.65, "leaf2"), blob(M, x + 0.5, h * 0.98, z, r * 0.55, r * 0.5, r * 0.55, "leaf")]
    else:             # pine: stacked flattened ellipsoids
        out += [blob(M, x, h * 0.55, z, r * 0.9, r * 0.45, r * 0.9, "leaf2"), blob(M, x, h * 0.78, z, r * 0.7, r * 0.42, r * 0.7, "leaf"),
                blob(M, x, h * 0.98, z, r * 0.45, r * 0.4, r * 0.45, "leaf2")]
    return out


def jars(M, x, z, n=3, mat="clay"):
    out = []
    for i in range(n):
        jx = x + (i % 2) * 2.6 - 1.0 + (i // 2) * 0.8
        jz = z + (i // 2) * 2.4
        s = 1.0 + 0.18 * ((i * 7) % 3)
        out += [blob(M, jx, 1.7 * s, jz, 1.5 * s, 1.8 * s, 1.5 * s, mat), blob(M, jx, 3.5 * s, jz, 1.0 * s, 0.5 * s, 1.0 * s, "wood_dark")]
    return out


def rubble(M, x0, x1, z0, z1, n, mats=("char2", "char", "stone"), seed=1, hmax=1.6):
    rng = np.random.RandomState(seed)
    out = []
    for i in range(n):
        x = rng.uniform(x0, x1); z = rng.uniform(z0, z1); s = rng.uniform(0.7, 1.7)
        out.append(blob(M, x, s * 0.45, z, s * 1.2, min(hmax, s * 0.7), s * 1.0, mats[i % len(mats)]))
    return out


# paint patterns (X, Y, Z in building coordinates)
def p_brick(X, Y, Z):
    c = np.floor(Y / 1.1)
    return ((Y % 1.1) < 0.2) | ((((X + Z) + (c % 2) * 1.0) % 2.0) < 0.2)


def p_planks(X, Y, Z):
    return ((X + Z) % 1.3) < 0.14


def p_lattice(X, Y, Z):
    return ((X % 2.0) < 0.28) | ((Y % 2.0) < 0.28)


def p_tile_ridges(X, S, Z):
    """roof slab: X across, S = along-slope local coordinate"""
    return ((X % 1.8) < 0.5) | ((S % 1.7) < 0.16)


def p_thatch_fibre(X, Y, Z):
    return ((X % 1.6) < 0.4) | ((Y % 2.6) < 0.22)
