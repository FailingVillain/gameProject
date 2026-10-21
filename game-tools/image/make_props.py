"""Cover props (rocks, standalone trees + broken variants), voxel pipeline same as buildings (ELEV 36, outline 3/2/1px, 8-step cell shading).

    python make_props.py [--out DIR] [--sheet PATH] [--only a,b]
Out: <out>/props/<name>/<name>[@64|@32].png + <name>_shadow*.png (separate soft ground shadow, light upper-left -> falls right/back) + layout.json
Anchor = ground point at the footprint centre; tile = 12 model units = 128/64/32 px.
"""
import argparse
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "tools"))
sys.path.insert(0, os.path.join(HERE, "tools", "buildings"))
import voxel as V  # noqa: E402
from bcommon import frame_for, make_res, render, TILE_PX, TONE, SIN  # noqa: E402
from voxel import tr, ry, ell, box, seg  # noqa: E402

GAME = r"C:\gameProject\korean-division"
MATS = {
    "granite":   [(66, 64, 62), (96, 94, 90), (130, 127, 120), (164, 160, 150)],
    "granite2":  [(58, 56, 54), (84, 82, 78), (112, 109, 103), (142, 138, 130)],
    "moss":      [(34, 56, 28), (52, 80, 38), (74, 106, 50), (100, 130, 64)],
    "crack":     [(14, 14, 14), (26, 25, 24), (40, 38, 36), (56, 54, 50)],
    "birchbark": [(150, 148, 140), (190, 188, 178), (222, 220, 210), (240, 238, 228)],
    "birchdark": [(30, 28, 26), (50, 48, 44), (74, 70, 64), (100, 96, 88)],
    "birchleaf": [(54, 80, 28), (82, 112, 38), (116, 146, 52), (150, 174, 72)],
    "larch":     [(52, 78, 30), (80, 108, 40), (112, 138, 54), (146, 166, 70)],
    "larchdry":  [(84, 78, 30), (118, 108, 40), (150, 138, 54), (178, 166, 74)],
    "pineleaf":  [(18, 44, 30), (28, 66, 40), (44, 90, 54), (66, 114, 70)],
    "pinebark":  [(60, 36, 24), (88, 54, 34), (116, 76, 48), (142, 98, 62)],
    "oakleaf":   [(40, 54, 24), (60, 78, 32), (84, 102, 44), (110, 126, 58)],
    "oakbark":   [(34, 28, 22), (52, 44, 34), (72, 62, 48), (94, 82, 64)],
    "wood_cut":  [(150, 118, 78), (182, 148, 100), (208, 176, 124), (228, 200, 150)],
}
for _n, _a in MATS.items():
    if _n not in V.MATERIALS:
        V.MATERIALS[_n] = V._ramp8([tuple(int(v * TONE) for v in c) + (255,) for c in _a])
        V.MAT_NAMES.append(_n)
        V.MAT_IDX[_n] = len(V.MAT_NAMES) - 1

I4 = np.eye(4)


def blob(x, y, z, a, b, c, mat):
    return ell(tr(x, y, z), a, b, c, mat)


def rod(p0, p1, r, mat, r1=None):
    return seg(p0, p1, r, mat, flat=True, r1=r1)


def rr(seed):
    return np.random.RandomState(seed)


def lum(x, y, z, rx_, ry_, rz_, tilt=0.0, mat="granite"):
    return ell(tr(x, y, z) @ ry(tilt), rx_, ry_, rz_, mat)


# ---------------------------------------------------------------- rocks (1 tile = 12 units)
def rock(s, seed, cracked=False):
    r = rr(seed)
    if not cracked:
        return [lum(0, 0.55 * s, 0, 3.2 * s, 2.0 * s, 2.6 * s, 15, "granite"),
                lum(-1.2 * s, 0.45 * s, 0.9 * s, 1.9 * s, 1.5 * s, 1.7 * s, -20, "granite2"),
                lum(1.5 * s, 0.35 * s, -0.8 * s, 1.5 * s, 1.1 * s, 1.4 * s, 30, "granite2"),
                lum(-0.3 * s, 1.5 * s, -0.2 * s, 1.5 * s, 0.9 * s, 1.3 * s, 10, "granite"),
                lum(-0.8 * s, 1.75 * s, 0.3 * s, 0.9 * s, 0.35 * s, 0.8 * s, 0, "moss")]
    out = [box(tr(0, 0.3, 0), 0.4 * s, 0.3, 2.1 * s, "crack"),
           lum(-1.9 * s, 0.9 * s, 0, 2.0 * s, 1.5 * s, 2.3 * s, 20, "granite"),
           lum(2.2 * s, 0.7 * s, 0.4 * s, 1.9 * s, 1.2 * s, 2.0 * s, -25, "granite2"),
           lum(-2.2 * s, 2.0 * s, -0.2 * s, 1.0 * s, 0.6 * s, 1.0 * s, 0, "granite")]
    for i in range(5):
        a = r.uniform(0, 6.28)
        d = r.uniform(3.4, 5.0) * s
        c = r.uniform(0.3, 0.7) * s
        out.append(lum(math.cos(a) * d, c * 0.5, math.sin(a) * d * 0.8, c * 1.1, c * 0.6, c, r.uniform(0, 60), "granite2" if i % 2 else "granite"))
    return out


# ---------------------------------------------------------------- trees (spiky / irregular)
def tiers(y0, y1, n, rbase, rtop, mats, seed, droop):
    """spiky conifer tiers: each tier = ring of cones pointing out/down from the trunk."""
    r = rr(seed)
    out = []
    for i in range(n):
        t = i / max(1, n - 1)
        y = y0 + (y1 - y0) * t
        rad = rbase + (rtop - rbase) * t
        k = 5 + (i % 2)
        for j in range(k):
            a = (j + 0.5 * (i % 2) + r.uniform(-0.25, 0.25)) / k * 2 * math.pi
            rl = rad * r.uniform(0.75, 1.15)
            p0 = np.array([0.0, y + r.uniform(-0.3, 0.3), 0.0])
            p1 = p0 + np.array([math.cos(a) * rl, -droop * r.uniform(0.6, 1.4), math.sin(a) * rl])
            out.append(rod(p0, p1, rad * 0.30, mats[(i + j) % len(mats)], r1=0.05))
    out.append(rod([0, y1 - 1.5, 0], [0.2, y1 + 3.5, 0], 0.9, mats[0], r1=0.05))
    return out


def birch(seed=3):
    r = rr(seed)
    out = [rod([0, 0, 0], [0.8, 17, 0.3], 0.95, "birchbark", r1=0.55)]
    for yy in (3, 6, 9, 12):
        out.append(blob(0.8 * yy / 17, yy, 0.3 * yy / 17 + 0.6, 0.55, 0.25, 0.25, "birchdark"))
    for a, y, ln in [(0.3, 9, 4.2), (2.3, 11, 4.6), (4.2, 13, 4.0), (5.4, 15, 3.0), (1.3, 16.5, 2.6)]:
        p0 = np.array([0.8 * y / 17, y, 0.3 * y / 17])
        p1 = p0 + np.array([math.cos(a) * ln, 1.6 + r.uniform(0, 2), math.sin(a) * ln])
        out.append(rod(p0, p1, 0.35, "birchbark", r1=0.12))
        for _ in range(3):
            q = p1 + np.array([r.uniform(-1.5, 1.5), r.uniform(-0.5, 1.2), r.uniform(-1.5, 1.5)])
            out.append(blob(q[0], q[1], q[2], r.uniform(1.4, 2.2), r.uniform(0.9, 1.4), r.uniform(1.4, 2.2), "birchleaf"))
    return out


def larch(seed=5):
    return [rod([0, 0, 0], [-0.3, 25, 0.2], 0.9, "pinebark", r1=0.2)] + tiers(5.5, 23, 9, 4.3, 1.1, ["larch", "larchdry", "larch"], seed, 1.0)


def pine(seed=7):
    return [rod([0, 0, 0], [0, 28, 0], 1.0, "pinebark", r1=0.35)] + tiers(4.0, 25, 8, 5.4, 1.0, ["pineleaf"], seed, 1.6)


def oak():
    out = [rod([0, 0, 0], [-0.8, 5, 0.4], 1.5, "oakbark", r1=1.0), rod([-0.8, 4.5, 0.4], [-3.8, 9.5, 1.0], 0.9, "oakbark", r1=0.5),
           rod([-0.8, 4.5, 0.4], [3.0, 10.0, -0.6], 0.95, "oakbark", r1=0.5), rod([0.3, 6, 0.2], [0.6, 12.5, 1.6], 0.7, "oakbark", r1=0.3),
           rod([3.0, 9.8, -0.6], [5.4, 9.2, -1.6], 0.4, "oakbark", r1=0.15)]
    for x, y, z, a, b, c in [(-4.2, 11.2, 1.0, 3.2, 2.0, 2.8), (3.6, 12.0, -0.8, 3.6, 2.2, 3.0), (0.6, 14.2, 1.5, 3.2, 2.0, 2.8),
                             (5.6, 9.8, -1.8, 1.8, 1.2, 1.6), (-1.5, 12.4, -1.6, 2.4, 1.5, 2.2), (-5.5, 9.4, 1.5, 1.6, 1.0, 1.4)]:
        out.append(blob(x, y, z, a, b, c, "oakleaf"))
    for x, y, z, dx, dy, dz in [(-4, 12.5, 1, -1.5, 2.4, 0.4), (3.6, 13.5, -0.8, 1.2, 2.6, -0.5), (0.6, 15.5, 1.5, 0.2, 2.8, 0.2)]:
        out.append(rod([x, y, z], [x + dx, y + dy, z + dz], 0.8, "oakleaf", r1=0.05))
    return out


def fallen(kind):
    """broken tree: jagged stump + trunk lying toward +x (away from the light) with crown tangle on the ground."""
    bark, leaf, rad, h = {"birch": ("birchbark", "birchleaf", 0.9, 17), "larch": ("pinebark", "larch", 0.85, 25),
                          "pine": ("pinebark", "pineleaf", 0.95, 28), "oak": ("oakbark", "oakleaf", 1.4, 14)}[kind]
    out = [rod([0, 0, 0], [0.1, 3.2, 0], rad * 1.05, bark, r1=rad * 0.95), rod([0.1, 3.0, 0], [0.3, 4.2, 0.2], rad * 0.6, "wood_cut", r1=0.2),
           rod([-0.1, 3.0, 0.3], [-0.3, 3.9, 0.5], rad * 0.4, "wood_cut", r1=0.15)]
    ln = h * 0.7
    out.append(rod([1.4, rad * 0.9, 0.2], [1.4 + ln * 0.55, rad * 0.8, 1.2], rad * 0.85, bark, r1=rad * 0.65))
    out.append(rod([1.4 + ln * 0.55, rad * 0.8, 1.2], [1.4 + ln, rad * 0.7, 2.4], rad * 0.65, bark, r1=rad * 0.3))
    r = rr(11)
    for i in range(8 if kind in ("larch", "pine") else 4):
        t = r.uniform(0.25, 1.0)
        x, z = 1.4 + ln * t, 0.2 + 2.2 * t
        if kind in ("larch", "pine"):
            a = r.uniform(0, 6.28)
            p0 = np.array([x, rad * 0.8, z])
            p1 = p0 + np.array([math.cos(a) * (4.2 - 3 * t) * 0.4, r.uniform(0.2, 1.0), math.sin(a) * (4.2 - 3 * t)])
            out.append(rod(p0, p1, 0.9 * (1.1 - t * 0.6), leaf, r1=0.06))
        else:
            out.append(blob(x, 1.1, z + r.uniform(-1, 1), r.uniform(1.6, 2.6), 1.1, r.uniform(1.4, 2.2), leaf))
    return out


PROPS = {  # name: (builder, bbox x0,x1,ymax,z0,z1)
    "rock_s": (lambda: rock(0.75, 1), (-3.4, 3.4, 3.4, -3, 3)),
    "rock_m": (lambda: rock(1.0, 2), (-4.2, 4.2, 4.2, -3.6, 3.6)),
    "rock_l": (lambda: rock(1.3, 3), (-5.6, 5.6, 5.6, -4.6, 4.6)),
    "rock_s_cracked": (lambda: rock(0.75, 1, True), (-5.6, 5.6, 3.4, -5, 5)),
    "rock_m_cracked": (lambda: rock(1.0, 2, True), (-7, 7, 4.2, -6, 6)),
    "rock_l_cracked": (lambda: rock(1.3, 3, True), (-9, 9, 5.6, -8, 8)),
    "tree_birch": (birch, (-6.5, 11, 21, -6, 6)),
    "tree_larch": (larch, (-6.5, 11, 29, -6, 6)),
    "tree_pine": (pine, (-7, 12, 32, -6, 6)),
    "tree_oak": (oak, (-8, 12, 18, -4, 4)),
}
for _k in ("birch", "larch", "pine", "oak"):
    PROPS[f"tree_{_k}_fallen"] = ((lambda k=_k: fallen(k)), (-3, 26, 5, -3, 7))


def shadow_for(res, name):
    """soft ground shadow: ellipse offset right/back (light upper-left); flatter for rocks/logs."""
    sh = Image.new("RGBA", (res.fw, res.fh), (0, 0, 0, 0))
    d = ImageDraw.Draw(sh)
    z = res.zoom
    b = PROPS[name][1]
    hh = b[2]
    if name.startswith("rock"):
        rxp, ryp, ox = b[1] * 0.7 * z, b[1] * 0.3 * z, b[1] * 0.25 * z
    elif "fallen" in name:
        rxp, ryp, ox = 9.0 * z, 3.0 * z * SIN, 8.0 * z
    else:
        rxp, ryp, ox = hh * 0.30 * z, hh * 0.09 * z + 3 * z * SIN, hh * 0.22 * z
    cx, cy = res.ax + ox, res.ay - ryp * 0.2
    d.ellipse([cx - rxp, cy - ryp, cx + rxp, cy + ryp], fill=(10, 14, 8, 110))
    return sh.filter(ImageFilter.GaussianBlur(max(1, res.zoom * 0.5)))


def build(name, out_root):
    fn, bbox = PROPS[name]
    geom = frame_for(bbox, 12)
    odir = os.path.join(out_root, name)
    os.makedirs(odir, exist_ok=True)
    prims = fn()
    imgs = {}
    lay = {"name": name, "tile_px": TILE_PX, "footprint_tiles": [1, 1], "lods": {}}
    for lod in (0, 1, 2):
        res = make_res(lod, geom)
        suf = ["", "@64", "@32"][lod]
        f = render(prims, res)
        f.base.save(os.path.join(odir, f"{name}{suf}.png"))
        sh = shadow_for(res, name)
        sh.save(os.path.join(odir, f"{name}_shadow{suf}.png"))
        imgs[lod] = (f.base, sh, res)
        lay["lods"][suf or "@128"] = {"frame": [res.fw, res.fh], "anchor": [res.ax, res.ay]}
    with open(os.path.join(odir, f"{name}_layout.json"), "w") as fh:
        json.dump(lay, fh, indent=1)
    return imgs[0]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=r"C:\gameProject\game-tools\korean-division-image-out")
    ap.add_argument("--sheet", default=GAME + r"\docs\art_review\cover_props_v1.png")
    ap.add_argument("--only", default="")
    a = ap.parse_args()
    root = os.path.join(a.out, "props")
    names = a.only.split(",") if a.only else list(PROPS)
    res = {n: build(n, root) for n in names}
    tile = 128
    gdir = GAME + r"\assets\themes\kd1920\terrain"
    gt = [Image.open(os.path.join(gdir, f"grass_v{i}@128.png")).convert("RGBA") for i in range(6)]
    inf = Image.open(os.path.join(a.out, "korean", "infantry-default", "infantry-default_1.png")).convert("RGBA").crop((0, 0, 128, 160))
    groups = [["rock_s", "rock_s_cracked"], ["rock_m", "rock_m_cracked"], ["rock_l", "rock_l_cracked"], ["tree_birch", "tree_birch_fallen"],
              ["tree_larch", "tree_larch_fallen"], ["tree_pine", "tree_pine_fallen"], ["tree_oak", "tree_oak_fallen"]]
    cw = ch = 3 * tile
    sheet = Image.new("RGBA", (160 + cw * 2, ch * len(groups) + 24), (30, 30, 30, 255))
    dr = ImageDraw.Draw(sheet)
    for ri, g in enumerate(groups):
        y0 = 24 + ri * ch
        for ci in range(2):
            for tx in range(3):
                for ty in range(3):
                    sheet.alpha_composite(gt[(tx * 3 + ty + ri + ci) % 6], (160 + ci * cw + tx * tile, y0 + ty * tile))
        for ci, n in enumerate(g):
            if n not in res:
                continue
            base, sh, r = res[n]
            ax = 160 + ci * cw + cw // 2 - (40 if "fallen" in n else 0)
            ay = y0 + ch // 2 + 60
            sheet.alpha_composite(sh, (ax - r.ax, ay - r.ay))
            sheet.alpha_composite(base, (ax - r.ax, ay - r.ay))
            dr.text((160 + ci * cw + 4, y0 + 2), n, fill=(255, 255, 255, 255))
        sheet.alpha_composite(inf, (16, y0 + ch // 2 + 60 - 144))
        dr.text((16, y0 + 2), "infantry (scale)", fill=(255, 255, 255, 255))
    os.makedirs(os.path.dirname(a.sheet), exist_ok=True)
    sheet.save(a.sheet)
    print("sheet", a.sheet, sheet.size)


if __name__ == "__main__":
    main()
