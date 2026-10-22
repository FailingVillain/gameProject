"""Parametric house generator (T55g). One JSON spec dict -> prim list, same pipeline as houses.py (bcommon.render).

    build(spec, ruin=False) -> prims          spec = normalised dict (see HOUSEGEN_SCHEMA.md)
    bbox_of(spec)           -> (x0, x1, ymax, z0, z1)   model units, for bcommon.frame_for
    REGISTRY()              -> {name: registry entry}  every house in house_specs/*.json (+ palette variants)

Content agents add houses ONLY as JSON files in tools/buildings/house_specs/ (files starting with '_' are tables, not houses).
Model units: 1 tile = 12; x right, y up, z toward the camera (front wall faces +z). Origin = footprint centre on the ground.
"""
from __future__ import annotations
import copy
import glob
import json
import math
import os

import numpy as np
from bcommon import (B, slope, post, blob, rod, jars, rubble, tree, RUIN_MAP, p_brick, p_planks, p_lattice,
                     p_tile_ridges, p_thatch_fibre)

HERE = os.path.dirname(os.path.abspath(__file__))
SPEC_DIR = os.path.join(HERE, "house_specs")
I4 = np.eye(4)

ROOF_TYPES = ("thatch_round", "gable_curved", "hip", "flat_earth", "shed", "gable_plain")
WALL_PATTERNS = ("none", "brick", "planks", "rammed", "lattice_band")
BAY_KINDS = ("paper", "plank", "arch", "lattice", "slit", "kitchen", "blank")
CHIMNEYS = ("none", "box", "jar", "flue")
PORCHES = ("none", "front", "front_side")
ADDONS = ("yard_wall", "gate", "jars", "firewood", "stakes", "hang", "haystack", "tree", "loft_window")
FOOTPRINTS = ((3, 3), (3, 4), (4, 4), (3, 5))

DEFAULTS = {
    "footprint": [3, 3],
    "wall": {"mat": "mud", "pattern": "none", "line_mat": None, "height": 8.5, "pillars": False, "pillar_mat": "wood", "lower_band": None},
    "base": {"height": 1.2, "mat": "stone"},
    "roof": {"type": "gable_plain", "mat": "thatch", "alt_mat": None, "rise": 6.0, "eave": 2.0, "curve": 0.0,
             "thickness": 1.6, "ridge_cap": True, "ridge_mat": None, "gable_overhang": 0.6},
    "chimney": {"type": "none", "side": "L", "mat": "brick_grey"},
    "front": {"bays": ["paper", "paper", "paper"], "porch": "none", "door_mat": "wood", "window_mat": "paper"},
    "addons": [],
    "seed": 1,
    "palette": None,
    "variants": {},
}
DARK = {"mud": "mud_dark", "earth": "mud_dark", "earth_pale": "earth", "plaster": "stone", "brick_grey": "brick_dark",
        "brick_red": "brick_dark", "wood": "wood_dark", "stone": "brick_dark", "thatch": "thatch_old", "thatch_gold": "thatch",
        "kiwa": "kiwa2", "kiwa_blue": "kiwa2", "tile_red": "clay"}


# ------------------------------------------------------------------------------------------ spec handling
def _merge(a, b):
    out = copy.deepcopy(a)
    for k, v in (b or {}).items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            out[k] = _merge(out[k], v)
        else:
            out[k] = copy.deepcopy(v)
    return out


def normalise(raw):
    s = _merge(DEFAULTS, raw)
    s["footprint"] = [int(s["footprint"][0]), int(s["footprint"][1])]
    n = len(s["front"]["bays"])
    if not 1 <= n <= 6:
        raise ValueError(f"{raw.get('id')}: bays must be 1..6")
    for k in s["front"]["bays"]:
        if k not in BAY_KINDS:
            raise ValueError(f"{raw.get('id')}: bay kind {k!r} not in {BAY_KINDS}")
    if s["roof"]["type"] not in ROOF_TYPES:
        raise ValueError(f"{raw.get('id')}: roof type {s['roof']['type']!r} not in {ROOF_TYPES}")
    if s["chimney"]["type"] not in CHIMNEYS:
        raise ValueError(f"{raw.get('id')}: chimney {s['chimney']['type']!r}")
    if s["front"]["porch"] not in PORCHES:
        raise ValueError(f"{raw.get('id')}: porch {s['front']['porch']!r}")
    for a in s["addons"]:
        t = a if isinstance(a, str) else a.get("type")
        if t not in ADDONS:
            raise ValueError(f"{raw.get('id')}: addon {t!r} not in {ADDONS}")
    return s


_PAL_CACHE = {}


def palettes():
    if "p" not in _PAL_CACHE:
        p = os.path.join(SPEC_DIR, "_palettes.json")
        _PAL_CACHE["p"] = {k: v for k, v in json.load(open(p, encoding="utf-8")).items() if not k.startswith("_")} if os.path.exists(p) else {}
    return _PAL_CACHE["p"]


def resolve_palette(pal):
    if not pal:
        return {}
    if isinstance(pal, str):
        if pal not in palettes():
            raise ValueError(f"unknown palette {pal!r}")
        return palettes()[pal]
    return pal


def apply_palette(prims, pal):
    pal = resolve_palette(pal)
    if not pal:
        return prims
    for p in prims:
        p.mat = pal.get(p.mat, p.mat)
        p.paint = [(f, pal.get(m, m)) for f, m in p.paint]
    return prims


# ------------------------------------------------------------------------------------------ builder context
class Ctx:
    """collects prims and the extents that drive the frame (so every helper reports what it covers)"""

    def __init__(self):
        self.o = []
        self.ext = [0.0, 0.0, 0.0, 0.0, 0.0]     # x0 x1 ymax z0 z1

    def _e(self, x0, x1, y1, z0, z1):
        e = self.ext
        e[0] = min(e[0], x0); e[1] = max(e[1], x1); e[2] = max(e[2], y1); e[3] = min(e[3], z0); e[4] = max(e[4], z1)

    def B(self, x0, x1, y0, y1, z0, z1, mat, paint=None, tag=None):
        self._e(x0, x1, y1, z0, z1)
        p = B(I4, x0, x1, y0, y1, z0, z1, mat, paint)
        self.o.append(p)
        return p

    def slope(self, x0, x1, ze, ye, zr, yr, thick, mat, paint=None):
        self._e(x0, x1, max(ye, yr) + thick, min(ze, zr), max(ze, zr))
        self.o.append(slope(I4, x0, x1, ze, ye, zr, yr, thick, mat, paint))

    def post(self, x, z, y0, y1, r, mat):
        self._e(x - r, x + r, y1, z - r, z + r)
        self.o.append(post(I4, x, z, y0, y1, r, mat))

    def blob(self, x, y, z, rx_, ry_, rz_, mat, clipy=None, paint=None):
        self._e(x - rx_, x + rx_, y + ry_, z - rz_, z + rz_)
        self.o.append(blob(I4, x, y, z, rx_, ry_, rz_, mat, clipy, paint))

    def rod(self, p0, p1, r, mat):
        self._e(min(p0[0], p1[0]) - r, max(p0[0], p1[0]) + r, max(p0[1], p1[1]) + r, min(p0[2], p1[2]) - r, max(p0[2], p1[2]) + r)
        self.o.append(rod(I4, p0, p1, r, mat))

    def add(self, prims, box_hint=None):
        self.o += prims
        if box_hint:
            self._e(*box_hint)


# ------------------------------------------------------------------------------------------ geometry layout
class Lay:
    pass


def layout(s):
    L = Lay()
    w, d = s["footprint"]
    L.hw, L.hd = w * 6.0, d * 6.0
    wl, rf = s["wall"], s["roof"]
    L.bh = float(s["base"]["height"])
    L.wh = float(wl["height"])
    L.wtop = L.bh + L.wh
    L.eave = float(rf["eave"])
    L.yard = any((a if isinstance(a, str) else a["type"]) == "yard_wall" for a in s["addons"])
    L.wx = L.hw - 3.5
    L.zb0 = -(L.hd - 3.5)
    full = 2 * L.hd - 9.0
    L.zb1 = L.zb0 + max(10.0, full * (0.58 if L.yard else 1.0))
    L.zc = (L.zb0 + L.zb1) / 2
    L.hz = (L.zb1 - L.zb0) / 2
    return L


# ------------------------------------------------------------------------------------------ walls, doors, porch
def wall_paint(s):
    pat, wl = s["wall"]["pattern"], s["wall"]
    line = wl.get("line_mat") or DARK.get(wl["mat"], "mud_dark")
    if pat == "brick":
        return [(p_brick, line)]
    if pat == "planks":
        return [(p_planks, line)]
    if pat == "rammed":
        return [(lambda X, Y, Z: (Y % 1.9) < 0.2, line)]
    if pat == "lattice_band":
        return [(lambda X, Y, Z: (((X % 3.0) < 0.2) | ((Y % 2.4) < 0.15)), line)]
    return []


def build_walls(c, s, L, rng, ruin):
    wl = s["wall"]
    paint = wall_paint(s)
    c.B(-L.wx - 0.5, L.wx + 0.5, 0, L.bh, L.zb0 - 0.5, L.zb1 + 0.5, s["base"]["mat"],
        [(lambda X, Y, Z: ((X % 3.0) < 0.18) | ((Y % 1.2) < 0.14), DARK["stone"])] if L.bh > 1.5 else None)
    if not ruin:
        c.B(-L.wx, L.wx, L.bh, L.wtop, L.zb0, L.zb1, wl["mat"], paint)
        if wl.get("lower_band"):
            c.B(-L.wx - 0.2, L.wx + 0.2, L.bh, L.bh + 1.0, L.zb0 - 0.2, L.zb1 + 0.2, wl["lower_band"])
        return
    # ruin: wall cut into 4 columns of random surviving height, back wall partly standing
    n = 4
    xs = np.linspace(-L.wx, L.wx, n + 1)
    hs = [0.95, 0.35, 0.6, 0.2]
    rng.shuffle(hs)
    for i in range(n):
        top = L.bh + L.wh * hs[i]
        c.B(xs[i], xs[i + 1], L.bh, top, L.zb0, L.zb1 if hs[i] < 0.5 else L.zb0 + 2.0, wl["mat"], paint)
    c.B(-L.wx, -L.wx + 1.6, L.bh, L.wtop, L.zb0, L.zb1, wl["mat"], paint)           # a corner stands


def bay_geometry(s, L):
    bays = s["front"]["bays"]
    n = len(bays)
    bw = 2 * L.wx / n
    return [(-L.wx + bw * (i + 0.5), bw, k) for i, k in enumerate(bays)]


def paper_door_prims(c, x0, x1, y0, y1, z, wood, paper):
    c.B(x0 - 0.5, x1 + 0.5, y0 - 0.4, y1 + 0.5, z - 0.2, z + 0.5, wood)
    c.B(x0, x1, y0, y1, z, z + 0.7, paper, [(p_lattice, wood)])


def build_front(c, s, L, ruin):
    fr = s["front"]
    zf = L.zb1
    dm, wm = fr["door_mat"], fr["window_mat"]
    y0 = L.bh + 1.4
    n_ruin_skip = {1, 3} if ruin else set()
    for i, (x, bw, kind) in enumerate(bay_geometry(s, L)):
        if i in n_ruin_skip:
            continue
        hw_ = max(1.3, bw / 2 - 0.9)
        top = L.bh + min(L.wh - 1.0, 7.0)
        if kind == "paper":
            paper_door_prims(c, x - hw_, x + hw_, y0 + 0.6, top, zf, dm, wm)
        elif kind == "plank":
            c.B(x - hw_ - 0.4, x + hw_ + 0.4, y0 - 0.2, top + 0.5, zf - 0.2, zf + 0.5, DARK[dm] if dm in DARK else dm)
            c.B(x - hw_, x + hw_, y0, top, zf, zf + 0.7, dm, [(lambda X, Y, Z: ((X % 1.1) < 0.14) | (abs(Y - (y0 + top) / 2) < 0.2), DARK.get(dm, "wood_dark"))])
        elif kind == "arch":
            r = hw_ + 0.2
            c.B(x - r - 0.3, x + r + 0.3, 0.0, top - r + 0.5, zf - 0.2, zf + 0.5, "brick_dark")
            c.B(x - r, x + r, 0.4, top - r + 0.4, zf, zf + 0.7, dm)
            c.blob(x, top - r + 0.4, zf + 0.1, r + 0.3, r + 0.3, 0.8, "brick_dark", clipy=0.0)
            c.blob(x, top - r + 0.4, zf + 0.5, r, r, 0.5, dm, clipy=0.0)
        elif kind == "lattice":
            wy0, wy1 = L.bh + 3.2, L.bh + min(L.wh - 1.0, 6.8)
            c.B(x - hw_ * 0.8 - 0.4, x + hw_ * 0.8 + 0.4, wy0 - 0.4, wy1 + 0.4, zf - 0.2, zf + 0.4, DARK.get(dm, "wood_dark"))
            c.B(x - hw_ * 0.8, x + hw_ * 0.8, wy0, wy1, zf, zf + 0.6, wm, [(p_lattice, dm)])
        elif kind == "slit":
            c.B(x - 0.8, x + 0.8, L.bh + 4.4, L.bh + 6.6, zf - 0.1, zf + 0.35, "wood_dark")
        elif kind == "kitchen":
            c.B(x - hw_, x + hw_, y0 - 0.2, top - 0.6, zf, zf + 0.6, "wood_dark")
            c.B(x - hw_ - 0.4, x + hw_ + 0.4, top - 0.6, top + 0.2, zf, zf + 0.9, dm)
    por = fr["porch"]
    if por != "none":
        px0, px1 = -L.wx + 0.8, L.wx - 0.8
        py = L.bh * 0.8 + 0.2
        c.B(px0, px1, py - 0.6, py, zf, zf + 2.6, "wood")
        c.B(px0, px1, 0, py - 0.6, zf + 0.4, zf + 2.2, "stone")
        for x in (px0 + 0.5, px1 - 0.5):
            c.post(x, zf + 2.2, py, L.wtop - 0.2, 0.55, "wood")
        if por == "front_side":
            c.B(px0 - 2.4, px0, py - 0.6, py, L.zb0 + 4, zf + 2.6, "wood")
            c.B(px0 - 2.4, px0, 0, py - 0.6, L.zb0 + 4.4, zf + 2.2, "stone")
    if s["wall"].get("pillars") and not ruin:
        bays = bay_geometry(s, L)
        xs = [bays[0][0] - bays[0][1] / 2] + [b[0] + b[1] / 2 for b in bays]
        for x in xs:
            c.post(x, zf + 0.5, L.bh, L.wtop + 0.2, 0.8, s["wall"]["pillar_mat"])
        c.B(-L.wx - 0.6, L.wx + 0.6, L.wtop - 1.2, L.wtop + 0.2, zf - 0.1, zf + 1.2, DARK.get(s["wall"]["pillar_mat"], "wood_dark"))


# ------------------------------------------------------------------------------------------ roofs
def roof_geom(s, L, ruin):
    """roof x centre/half-length (ruin keeps only the left ~45%), z centre/half-depth, eave height"""
    rf = s["roof"]
    X = L.wx + L.eave + (rf["gable_overhang"] if rf["type"] in ("gable_plain", "gable_curved", "shed") else 0.0)
    hz = L.hz + L.eave
    if ruin:
        return -X * 0.55, X * 0.45, hz
    return 0.0, X, hz


def build_roof(c, s, L, ruin):
    rf = s["roof"]
    t = rf["type"]
    xc, xh, hz = roof_geom(s, L, ruin)
    x0, x1 = xc - xh, xc + xh
    zc, rise, th, curve = L.zc, float(rf["rise"]), float(rf["thickness"]), float(rf["curve"])
    mat, alt = rf["mat"], rf["alt_mat"] or DARK.get(rf["mat"], rf["mat"])
    rmat = rf["ridge_mat"] or alt
    y0 = L.wtop                                        # wall top
    tilepaint = [(p_tile_ridges, alt)]
    if t == "thatch_round":
        ry = rise
        c.blob(xc, y0 - 0.8, zc, xh + 0.2, ry, hz, mat, clipy=0.0, paint=[(lambda p: ((p[:, 0] % 2.2) < 0.22) | ((p[:, 1] % 3.0) < 0.2), alt)])
        if rf["ridge_cap"]:
            c.blob(xc, y0 - 0.8 + ry - 0.4, zc, xh * 0.8, 1.1, 1.6, alt)
        c.blob(xc, y0 - 1.0, zc + hz * 0.55, xh - 0.6, ry * 0.45, hz * 0.45 + 1.2, mat, clipy=0.0)
        c.blob(xc, y0 - 1.0, zc - hz * 0.55, xh - 0.6, ry * 0.45, hz * 0.45 + 1.0, alt, clipy=0.0)
    elif t == "gable_plain":
        ye, yr = y0 + 1.0, y0 + 1.0 + rise
        for sg in (1, -1):
            c.slope(x0, x1, zc + sg * hz, ye, zc, yr, th, mat, tilepaint)
        if rf["ridge_cap"]:
            c.B(x0 - 0.5, x1 + 0.5, yr - 0.3, yr + 0.9, zc - 1.1, zc + 1.1, rmat)
    elif t == "gable_curved":
        ye = y0 + 1.4
        fr = (0.0, 0.34, 0.69, 1.0)
        dy = (rise, rise * 0.32, -0.3 + curve * 0.3, curve * 0.9)
        for sg in (1, -1):
            pts = [(zc + sg * hz * f, ye + d) for f, d in zip(fr, dy)]
            for (za, ya), (zb, yb) in zip(pts[:-1], pts[1:]):
                c.slope(x0, x1, zb, yb, za, ya, th, mat, tilepaint)
        if rf["ridge_cap"]:
            c.B(x0 - 0.5, x1 + 0.5, ye + rise - 0.2, ye + rise + 1.6, zc - 1.3, zc + 1.3, rmat)
            for sx in ((-1, 1) if not ruin else (-1,)):
                c.blob(xc + sx * (xh + 0.4), ye + rise + 1.0, zc, 1.2, 1.6, 1.8, rmat)
        c.B(x0 - 0.2, x1 + 0.2, y0 + 0.2, y0 + 1.4, zc + L.hz - 0.2, zc + hz - 0.4, "wood_dark")       # fascia shadow
    elif t == "hip":
        n = 10
        rr = max(1.2, xh - hz * 0.9)                      # ridge half length
        ye = y0 + 1.0
        for i in range(n):
            f = i / (n - 1)
            hx = xh + (rr - xh) * f
            hzz = hz + (1.0 - hz) * f
            if ruin:
                hx = xh * (1 - f * 0.6)
            y = ye + rise * (f ** 1.25) + (curve * (1 - f) ** 3)
            c.B(xc - hx, xc + hx, y - th, y, zc - hzz, zc + hzz, mat, [(lambda X, Y, Z: ((X % 1.8) < 0.45), alt)])
        if rf["ridge_cap"]:
            c.B(xc - rr - 0.6, xc + rr + 0.6, ye + rise - 0.2, ye + rise + 1.0, zc - 1.1, zc + 1.1, rmat)
    elif t == "flat_earth":
        yf, yb = y0 + 0.6, y0 + 1.4
        c.slope(x0, x1, zc + hz, yf, zc - hz, yb, th + 0.6, mat, [(lambda X, S, Z: ((X % 2.4) < 0.3) | ((S % 3.0) < 0.2), alt)])
        c.B(x0 - 0.2, x1 + 0.2, y0 - 0.2, y0 + 1.2, zc + L.hz, zc + hz, DARK.get(mat, "mud_dark"))          # eave soffit / earth lip
        c.B(x0 - 0.4, x1 + 0.4, yf + 0.4, yf + 1.6, zc + hz - 1.0, zc + hz + 0.2, rf["ridge_mat"] or "brick_grey", [(p_brick, "brick_dark")])  # front parapet
        c.B(x0 - 0.4, x1 + 0.4, yb + 0.3, yb + 1.9, zc - hz - 0.2, zc - hz + 1.0, rf["ridge_mat"] or "brick_grey", [(p_brick, "brick_dark")])
    elif t == "shed":
        ye, yr = y0 + 0.8, y0 + 0.8 + rise
        c.slope(x0, x1, zc + hz, ye, zc - hz, yr, th, mat, tilepaint)
        c.B(xc - xh + 0.4, xc + xh - 0.4, y0, yr + 0.2, L.zb0 - 0.2, L.zb0 + 1.4, s["wall"]["mat"], wall_paint(s))   # raised back wall


def build_roof_ruin_extra(c, s, L, rng):
    rf = s["roof"]
    xc, xh, hz = roof_geom(s, L, False)
    mat = rf["mat"]
    # collapsed roof on the floor, fallen rafters
    c.blob(xh * 0.35, L.bh + 1.0, L.zc + 1, xh * 0.4, 1.8, L.hz * 0.6, mat, clipy=0.0)
    c.slope(xh * 0.05, xh * 0.7, L.zc + 3, L.bh + 1.0, L.zc - 4, L.bh + 4.5, 1.2, mat)
    for i in range(3):
        x = rng.uniform(-L.wx * 0.7, L.wx * 0.8)
        c.rod((x, L.bh + 0.8, L.zb1 - 1), (x + rng.uniform(-3, 4), L.bh + rng.uniform(4, 7), L.zc - rng.uniform(0, 4)), 0.55, "wood")


# ------------------------------------------------------------------------------------------ chimney
def build_chimney(c, s, L, ruin):
    ch = s["chimney"]
    t = ch["type"]
    if t == "none":
        return
    side = ch["side"]
    top = L.wtop + s["roof"]["rise"] + 2.2
    if ruin:
        top = L.wtop + 1.0
    xs = {"L": -L.wx - 1.4, "R": L.wx + 1.4}.get(side)
    if side == "B":
        x, z = L.wx * 0.4, L.zb0 - 1.4
    else:
        x, z = xs, L.zb0 + 3.5
    m = ch["mat"]
    if t == "box":
        c.B(x - 1.3, x + 1.3, 0, top, z - 1.3, z + 1.3, m, [(p_brick, "brick_dark")])
        c.B(x - 1.8, x + 1.8, top, top + 0.9, z - 1.8, z + 1.8, "kiwa")
    elif t == "jar":
        c.post(x, z, 0, top - 0.5, 1.3, "mud")
        c.post(x, z, top - 0.6, top + 0.2, 1.7, "mud_dark")
    elif t == "flue":                                  # free-standing kang flue with a low duct to the wall
        fx = x + (-2.4 if side == "L" else 2.4) if side != "B" else x
        fz = z - 1.5
        c.B(fx - 1.4, fx + 1.4, 0, top + 1.5, fz - 1.4, fz + 1.4, m, [(p_brick, "brick_dark")])
        c.B(fx - 1.9, fx + 1.9, top + 1.5, top + 2.5, fz - 1.9, fz + 1.9, "kiwa")
        c.B(min(fx, x * 0.5 + fx * 0.5) - 0.2 if side != "B" else fx - 0.8, max(fx, x) if side != "B" else fx + 0.8, 0.6, 2.2,
            fz - 0.8, fz + 0.8, m, [(p_brick, "brick_dark")])


# ------------------------------------------------------------------------------------------ add-ons
def build_addons(c, s, L, rng, ruin):
    zf = L.zb1
    for a in s["addons"]:
        a = {"type": a} if isinstance(a, str) else a
        t = a["type"]
        if t == "yard_wall":
            m = a.get("mat", "mud")
            hgt = float(a.get("height", 3.6))
            cover = a.get("cover", "thatch_old" if m == "mud" else "kiwa")
            zy = L.hd - 1.2
            xw = L.hw - 1.4
            gate = a.get("gate", True)
            gx = 2.8 if gate else 0.0
            pn = [(p_brick, "brick_dark")] if m in ("brick_grey", "brick_red") else [(lambda X, Y, Z: (Y % 1.6) < 0.18, DARK.get(m, "mud_dark"))]
            hh = hgt * (0.5 if ruin else 1.0)
            segs = [(-xw, -gx), (gx, xw)] if gate else [(-xw, xw)]
            for x0, x1 in segs:
                c.B(x0, x1, 0, hh, zy - 0.6, zy + 0.6, m, pn)
                if not ruin:
                    c.B(x0 - 0.2, x1 + 0.2, hh, hh + 0.9, zy - 1.0, zy + 1.0, cover)
            for sx in (-1, 1):
                c.B(sx * xw - 0.6, sx * xw + 0.6, 0, hh, L.zb1 + 1.0, zy + 0.6, m, pn)
                if not ruin:
                    c.B(sx * xw - 1.0, sx * xw + 1.0, hh, hh + 0.9, L.zb1 + 1.0, zy + 1.0, cover)
        elif t == "gate" and not ruin:
            zy = L.hd - 1.2
            c.B(-3.4, -2.4, 0, 7.4, zy - 0.8, zy + 0.8, "wood")
            c.B(2.4, 3.4, 0, 7.4, zy - 0.8, zy + 0.8, "wood")
            c.B(-4.6, 4.6, 7.4, 8.4, zy - 1.4, zy + 1.4, a.get("roof_mat", "kiwa"))
            c.B(-3.4, -0.1, 0.8, 6.6, zy - 0.3, zy + 0.3, "wood", [(lambda X, Y, Z: (X % 1.1) < 0.14, "wood_dark")])
            c.B(0.1, 3.4, 0.8, 6.6, zy - 0.3, zy + 0.3, "wood", [(lambda X, Y, Z: (X % 1.1) < 0.14, "wood_dark")])
        elif t == "jars":
            side = a.get("side", "R")
            x = (L.wx + 3.2) if side == "R" else -(L.wx + 3.2)
            c.add(jars(I4, x, zf - 0.5, int(a.get("n", 3)) + int(rng.randint(0, 2)), a.get("mat", "clay")), (x - 3, x + 3, 6, zf - 1, zf + 6))
        elif t == "firewood":
            x = float(a.get("x", -L.wx * 0.6))
            c.B(x, x + 5.6, 0, 2.6, zf + 0.6, zf + 2.6, "wood", [(lambda X, Y, Z: ((Y % 0.9) < 0.14) | ((X % 0.9) < 0.1), "wood_dark")])
        elif t == "stakes":
            n = int(a.get("n", 6))
            x0 = -L.wx
            step = 2 * L.wx / max(1, n - 1)
            zz = zf + 3.0
            for i in range(n):
                c.post(x0 + i * step, zz, 0, 3.4, 0.5, "wood")
            c.rod((x0, 2.8, zz), (L.wx, 2.8, zz), 0.4, "wood")
            c.rod((x0, 1.4, zz), (L.wx, 1.4, zz), 0.4, "wood")
        elif t == "hang" and not ruin:
            n = int(a.get("n", 5))
            m = a.get("mat", "thatch_gold")
            for i in range(n):
                x = -L.wx * 0.8 + i * (1.6 * L.wx / max(1, n - 1))
                c.blob(x, L.wtop - 2.0 - (i % 2) * 0.8, zf + 1.3, 0.8, 1.6, 0.8, m)
        elif t == "haystack":
            x = float(a.get("x", L.wx + 3.5))
            c.blob(x, 0.0, L.zb0 + 3.0, 3.6, 4.4, 3.4, "thatch", clipy=0.0)
            c.blob(x, 4.2, L.zb0 + 3.0, 1.0, 0.6, 1.0, "thatch_old")
        elif t == "tree":
            c.add(tree(I4, float(a.get("x", -L.hw + 3)), float(a.get("z", L.zb1 - 1)), float(a.get("h", 15)), float(a.get("r", 5.5)), int(a.get("kind", 0))),
                  (float(a.get("x", -L.hw + 3)) - 6, float(a.get("x", -L.hw + 3)) + 6, float(a.get("h", 15)) + 4, 0, 0))
        elif t == "loft_window" and not ruin:
            c.B(-1.6, 1.6, s["wall"]["height"] + L.bh + 1.2, s["wall"]["height"] + L.bh + 3.8, L.zc + L.hz - 1.5, L.zc + L.hz - 0.3, "wood_dark")


# ------------------------------------------------------------------------------------------ main entry
def build(spec, ruin=False, palette=None, with_bbox=False):
    s = spec if "_norm" in spec else dict(normalise(spec), _norm=True)
    L = layout(s)
    rng = np.random.RandomState(int(s["seed"]) + (97 if ruin else 0))
    c = Ctx()
    build_walls(c, s, L, rng, ruin)
    build_front(c, s, L, ruin)
    build_roof(c, s, L, ruin)
    if ruin:
        build_roof_ruin_extra(c, s, L, rng)
    build_chimney(c, s, L, ruin)
    build_addons(c, s, L, rng, ruin)
    prims = c.o
    pal = palette if palette is not None else s.get("palette")
    apply_palette(prims, pal)
    if ruin:
        n_before = len(prims)
        rb = rubble(I4, -L.wx, L.wx, L.zb0 + 1, L.zb1 + 3, int(10 + 3 * (L.hd // 6)), mats=(s["wall"]["mat"], "char2", "stone"), seed=int(s["seed"]) + 5)
        prims += apply_palette(rb, pal)
        keep = {s["wall"]["mat"], s["base"]["mat"], "stone", "brick_grey", "brick_dark", "brick_red", "earth", "earth_pale", "mud", "mud_dark", "clay", "tile_red", "plaster"}
        keep |= set(resolve_palette(pal).values())
        keep -= {"thatch", "thatch_old", "thatch_gold"}
        for p in prims:
            if p.mat in RUIN_MAP and p.mat not in keep:
                p.mat = RUIN_MAP[p.mat]
            p.paint = [(f, RUIN_MAP.get(m, m) if m not in keep else m) for f, m in p.paint]
        c._e(-L.wx, L.wx, 6, L.zb0, L.zb1 + 3)
    if with_bbox:
        return prims, c.ext
    return prims


def bbox_of(spec):
    s = dict(normalise(spec), _norm=True)
    _, e = build(s, ruin=False, with_bbox=True)
    _, e2 = build(s, ruin=True, with_bbox=True)
    x = max(abs(e[0]), abs(e[1]), abs(e2[0]), abs(e2[1])) + 1.0
    return (-x, x, max(e[2], e2[2]) + 1.0, min(e[3], e2[3]) - 1.0, max(e[4], e2[4]) + 1.0)


# ------------------------------------------------------------------------------------------ registry (JSON files)
def load_specs(spec_dir=None):
    """-> {house_id: raw spec dict} from house_specs/*.json (not '_*.json'). `extends` inherits from another id."""
    raw = {}
    for fn in sorted(glob.glob(os.path.join(spec_dir or SPEC_DIR, "*.json"))):
        if os.path.basename(fn).startswith("_"):
            continue
        doc = json.load(open(fn, encoding="utf-8"))
        for h in doc["houses"]:
            h = dict(h)
            h.setdefault("set", doc.get("set", os.path.splitext(os.path.basename(fn))[0]))
            if h["id"] in raw:
                raise ValueError(f"duplicate house id {h['id']} ({fn})")
            raw[h["id"]] = h
    return raw


def _resolve_extends(raw, hid, seen=()):
    h = raw[hid]
    if "extends" not in h:
        return h
    if hid in seen:
        raise ValueError(f"extends cycle at {hid}")
    base = _resolve_extends(raw, h["extends"], seen + (hid,))
    out = _merge({k: v for k, v in base.items() if k not in ("id", "label", "variants")}, {k: v for k, v in h.items() if k != "extends"})
    return out


def REGISTRY(spec_dir=None, only_sets=None):
    raw = load_specs(spec_dir)
    reg = {}
    for hid in raw:
        h = _resolve_extends(raw, hid)
        if only_sets and h["set"] not in only_sets:
            continue
        spec = dict(normalise(h), _norm=True)
        bb = bbox_of(spec)
        variants = {"": spec.get("palette")}
        for vk, vp in (h.get("variants") or {}).items():
            variants["-" + vk] = vp
        for suf, pal in variants.items():
            name = hid + suf
            reg[name] = dict(states={"intact": (lambda sp=spec, p=pal: build(sp, False, p)), "ruined": (lambda sp=spec, p=pal: build(sp, True, p))},
                             bbox=bb, footprint=tuple(spec["footprint"]), label=h.get("label", hid), set=h["set"])
    return reg


if __name__ == "__main__":
    for n, d in REGISTRY().items():
        print(n, d["set"], d["footprint"], [round(v, 1) for v in d["bbox"]], d["label"])
