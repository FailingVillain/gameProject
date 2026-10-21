"""Effects: fx_explosion (6 x 96x96, artillery impact) and fx_muzzle_big (3 x 64x64). RGBA, no team layer.
Run: python tools/vox_fx.py -> out/fx_explosion/*, out/fx_muzzle_big/*
Anchor = ground point of the effect: explosion (48, 76), muzzle (32, 44). Quarter view like the units (same camera).
"""
import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT  # noqa: E402
from voxel import ell, cyl, seg, tr, make_frame  # noqa: E402

BG = (78, 110, 64, 255)


# 8 explosion variants: fire = fireball scale, fy = fireball height, fstretch = x stretch, colh/colr = dirt column height/radius scale,
# ang/spread = spray direction (rad) and spread, n/size = clump count/size, vy = launch speed, sm = smoke blob scale,
# drift = smoke lean, dark = fraction of dark smoke blobs, sticks = long debris streaks
EV = [
    dict(seed=7, fire=1.0, fy=0, fstretch=1.0, colh=1.0, colr=1.0, ang=0.0, spread=6.3, n=22, size=1.0, vy=1.0, sm=1.0, drift=0.0, dark=0.25, sticks=0),
    dict(seed=11, fire=1.5, fy=0, fstretch=1.1, colh=0.6, colr=1.2, ang=0.0, spread=6.3, n=14, size=1.1, vy=0.8, sm=1.2, drift=0.0, dark=0.8, sticks=0),
    dict(seed=21, fire=0.8, fy=0, fstretch=0.8, colh=1.5, colr=0.7, ang=0.0, spread=6.3, n=18, size=0.9, vy=1.3, sm=0.9, drift=0.0, dark=0.0, sticks=0),
    dict(seed=33, fire=0.9, fy=0, fstretch=1.0, colh=0.9, colr=0.9, ang=0.4, spread=1.2, n=24, size=1.0, vy=1.0, sm=1.0, drift=0.25, dark=0.3, sticks=3),
    dict(seed=44, fire=0.7, fy=0, fstretch=1.3, colh=0.5, colr=1.4, ang=0.0, spread=6.3, n=34, size=0.6, vy=0.7, sm=0.8, drift=0.0, dark=0.1, sticks=0),
    dict(seed=55, fire=1.2, fy=0, fstretch=0.8, colh=1.2, colr=0.8, ang=3.1, spread=2.0, n=16, size=1.2, vy=1.1, sm=1.3, drift=-0.35, dark=0.9, sticks=0),
    dict(seed=66, fire=1.1, fy=12, fstretch=1.0, colh=0.3, colr=0.6, ang=0.0, spread=6.3, n=8, size=0.8, vy=0.6, sm=1.1, drift=0.0, dark=0.0, sticks=0),
    dict(seed=77, fire=0.9, fy=0, fstretch=1.0, colh=0.8, colr=1.0, ang=1.9, spread=6.3, n=12, size=1.6, vy=1.2, sm=0.9, drift=0.15, dark=0.5, sticks=7),
]


def explosion_frame(t, v=0):
    Vp = EV[v]
    rnd = random.Random(Vp["seed"])
    prims, ov = [], []
    sr = [0, 11, 13, 14, 15, 15][t] * (0.8 + 0.25 * (v % 3))
    if sr:
        prims.append(ell(tr(0, 0.2, 0), sr, 0.5, sr * 0.9, "scorch"))
    if t >= 1:
        prims.append(ell(tr(0, 0.5, 0), sr * 0.6, 0.6, sr * 0.55, "dirt2"))
    tt = [0.0, 0.35, 0.7, 1.0, 1.35, 1.7][t]
    if t > 0:
        for i in range(Vp["n"]):
            a = Vp["ang"] + (rnd.random() - 0.5) * Vp["spread"]
            sp = 5 + rnd.random() * 14
            vy = (18 + rnd.random() * 26) * Vp["vy"]
            sz = (1.8 + rnd.random() * 2.6) * Vp["size"]
            x = math.cos(a) * sp * tt
            z = math.sin(a) * sp * tt * 0.9
            y = vy * tt - 14 * tt * tt
            if y < 0.8:
                y = 0.8
                if t < 4:
                    continue
            prims.append(ell(tr(x, y + sz, z), sz, sz * 0.85, sz, "dirt" if i % 3 else "dirt2"))
        for i in range(Vp["sticks"]):
            a = Vp["ang"] + (rnd.random() - 0.5) * 3.0
            sp = 8 + rnd.random() * 14
            vy = 24 + rnd.random() * 16
            x = math.cos(a) * sp * tt
            z = math.sin(a) * sp * tt * 0.9
            y = max(1.0, vy * tt - 14 * tt * tt)
            prims.append(seg((x, y, z), (x + math.cos(a) * 3.2, y + 2.4, z + math.sin(a) * 3.0), 0.9, "wood", flat=True))
    if 1 <= t <= 4:
        h = [0, 24, 30, 24, 14][t] * Vp["colh"]
        r = [0, 9, 10, 8, 6][t] * Vp["colr"]
        for k in range(4):
            prims.append(ell(tr(rnd.uniform(-2, 2) + Vp["drift"] * h * k / 4.0, 3 + h * k / 4.0, rnd.uniform(-2, 2)),
                             r * (1 - 0.12 * k), r * 0.8, r * (1 - 0.12 * k), "dirt"))
    n_s, ys, rs = [(0, 0, 0), (3, 8, 6), (5, 16, 8), (6, 22, 9), (8, 26, 10), (9, 28, 10)][t]
    rs = rs * Vp["sm"]
    for i in range(n_s):
        a = i * 2.4
        rr = rs * (0.55 + 0.45 * ((i * 37) % 10) / 10.0)
        hgt = ys * (i + 1) / n_s + (4 if t >= 4 else 0)
        ox = math.cos(a) * rs * 0.7 + Vp["drift"] * hgt * 1.5
        oz = math.sin(a) * rs * 0.6
        dark = (i * 0.37 % 1.0) < Vp["dark"] and t < 5
        prims.append(ell(tr(ox, 4 + hgt, oz), rr, rr * 0.85, rr, "smokeD" if dark else "smoke"))
    fr, fh = [(8, 12), (9, 14), (7, 10), (4, 6), (0, 0), (0, 0)][t]
    if fr:
        r = fr * Vp["fire"]
        fy = Vp["fy"] * (0.5 if t else 0.3)
        sx = Vp["fstretch"]
        ov += [ell(tr(0, r * 0.9 + fy, 0), r * 1.2 * sx, r, r * 1.1, "fire_r"),
               ell(tr(0, r * 0.95 + fy, 0), r * 0.85 * sx, r * 0.75, r * 0.8, "flash_edge"),
               ell(tr(0, r * 1.0 + fy, 0), r * 0.6 * sx, r * 0.5, r * 0.55, "flash"),
               ell(tr(0, r * 1.0 + fy, 0), r * 0.3, r * 0.3, r * 0.3, "fire_w")]
    return make_frame(prims, ov, 0, fw=96, fh=96, ax=48, ay=76, zoom=1.4, outline=False, ao=False, net=False)


def muzzle_frame(t, v=0):
    prims, ov = [], []
    rays = [10, 6, 14][v]
    sc_ = [1.0, 1.25, 0.85][v]
    rnd = random.Random(5 + v)
    if t == 0:
        for i in range(rays):
            a = i * math.tau / rays + 0.2 + v * 0.3
            L = (17 if i % 2 == 0 else 10) * sc_ * (0.8 + 0.4 * rnd.random())
            ov.append(seg((0, 12, 0), (math.cos(a) * L, 12 + math.sin(a) * L * 0.7, math.sin(a) * L * 0.3), 2.6, "flash_edge", r1=0.3))
        ov += [ell(tr(0, 12, 0), 8 * sc_, 7 * sc_, 8 * sc_, "flash_edge"), ell(tr(0, 12, 0), 5.8 * sc_, 5 * sc_, 5.8 * sc_, "flash"),
               ell(tr(0, 12, 0), 3 * sc_, 2.8 * sc_, 3 * sc_, "fire_w")]
    elif t == 1:
        ov += [ell(tr(2 + v, 16, 1), 6 * sc_, 5.5 * sc_, 6 * sc_, "smoke_b"), ell(tr(0, 13, 0), 9 * sc_, 8 * sc_, 9 * sc_, "smoke_c"),
               ell(tr(0, 12, 0), 7 * sc_, 6 * sc_, 7 * sc_, "flash_edge"), ell(tr(0, 12, 0), 4.6 * sc_, 4 * sc_, 4.6 * sc_, "flash"),
               ell(tr(0, 12, 0), 2.2 * sc_, 2 * sc_, 2.2 * sc_, "fire_w")]
    else:
        k = [7, 5, 9][v]
        for i in range(k):
            a = i * math.tau / k
            ov.append(ell(tr(math.cos(a) * 8 * sc_, 15 + (i % 2) * 2, math.sin(a) * 6 * sc_), 4.2 * sc_, 3.6 * sc_, 4.2 * sc_,
                          "smoke_b" if i % 2 else "smoke_a"))
        ov.append(ell(tr(0, 17, 0), 5 * sc_, 4 * sc_, 5 * sc_, "smoke_c"))
    return make_frame(prims, ov, 0, fw=64, fh=64, ax=32, ay=44, zoom=1.2, outline=False, ao=False, net=False)


def build(name, fn, n, size, rows, scale=4):
    d = os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    sheet = Image.new("RGBA", (size * n, size * rows), (0, 0, 0, 0))
    frames = {}
    for v in range(rows):
        for i in range(n):
            f = fn(i, v)
            frames[(v, i)] = f
            sheet.paste(f.base, (i * size, v * size))
    sheet.save(os.path.join(d, f"{name}.png"))
    bg = Image.new("RGBA", sheet.size, BG)
    bg.alpha_composite(sheet)
    bg.resize((bg.width * scale, bg.height * scale), Image.NEAREST).save(os.path.join(d, f"preview_{name}.png"))
    h = bg.resize((bg.width // 2, bg.height // 2), Image.BOX)
    h.resize((h.width * 4, h.height * 4), Image.NEAREST).save(os.path.join(d, f"preview_{name}_half.png"))
    for v in range(rows):
        gi = []
        for i in range(n):
            im = Image.new("RGBA", (size, size), BG)
            im.alpha_composite(frames[(v, i)].base)
            gi.append(im.resize((size * 3, size * 3), Image.NEAREST).convert("P"))
        gi[0].save(os.path.join(d, f"{name}_v{v}.gif"), save_all=True, append_images=gi[1:], duration=110, loop=0)


if __name__ == "__main__":
    build("fx_explosion", explosion_frame, 6, 96, 8)
    build("fx_muzzle_big", muzzle_frame, 3, 64, 3)
    print("fx done")
