"""Scout (recon) sprite sheet generator -- WWII US scout with M1 carbine, binoculars, backpack radio.

Figure is modelled with simple 3D points (a = right, b = forward, h = height in px) and projected
for each facing (s/se/e/ne/n); parts are depth-sorted and shaded from pixel sets (top-left light).
"""
from __future__ import annotations

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import P, Frame, save_sheet  # noqa: E402

ANG = {"s": 0, "se": 45, "e": 90, "ne": 135, "n": 180}
CX, GY, TILT = 32, 72, 0.5

# palettes: (dark, mid, light)
JACKET = (P["od"], P["od_light"], (142, 146, 98, 255))
PANTS = (P["khaki_dark"], P["khaki"], P["khaki_light"])
BOOT = (P["leather_dark"], (88, 60, 38, 255), (128, 92, 60, 255))
LEGGING = ((112, 102, 72, 255), P["canvas"], P["canvas_light"])
HELMET = (P["helmet_dark"], P["helmet"], (140, 148, 100, 255))
SKIN = (P["skin_dark"], P["skin"], P["skin_light"])
HAIR = ((34, 24, 16, 255), (58, 40, 26, 255), (84, 60, 40, 255))
STEEL = (P["steel_dark"], P["steel"], P["steel_light"])
WOOD = (P["wood_dark"], P["wood"], (156, 108, 64, 255))
RADIO = ((34, 38, 24, 255), P["od_dark"], (88, 94, 58, 255))
CANVAS = ((120, 110, 78, 255), P["canvas"], P["canvas_light"])
LEATHER = BOOT
TEAMG = (105, 170, 225)  # grayscale shades for team layer


# ---------------------------------------------------------------- pixel-set helpers
def disc(cx, cy, rx, ry):
    return {(x, y) for y in range(int(cy - ry - 1), int(cy + ry + 2)) for x in range(int(cx - rx - 1), int(cx + rx + 2))
            if ((x - cx) / max(rx, .5)) ** 2 + ((y - cy) / max(ry, .5)) ** 2 <= 1.0}


def stroke(p0, p1, w):
    n = max(int(abs(p1[0] - p0[0])), int(abs(p1[1] - p0[1])), 1) * 2
    S = set()
    for i in range(n + 1):
        x = p0[0] + (p1[0] - p0[0]) * i / n
        y = p0[1] + (p1[1] - p0[1]) * i / n
        xi, yi = round(x), round(y)
        if w == 1:
            S.add((xi, yi))
        elif w == 2:
            S |= {(xi, yi), (xi + 1, yi), (xi, yi + 1), (xi + 1, yi + 1)}
        else:
            r = w / 2 + .2
            S |= {(xi + dx, yi + dy) for dx in range(-w, w + 1) for dy in range(-w, w + 1) if dx * dx + dy * dy <= r * r}
    return S


def fill(f: Frame, S, pal, team=False):
    for (x, y) in S:
        if (x + 1, y) not in S or (x, y + 1) not in S:
            c = pal[0]
        elif (x - 1, y) not in S or (x, y - 1) not in S:
            c = pal[2]
        else:
            c = pal[1]
        f.px(x, y, c, team)


class Scene:
    def __init__(self, f: Frame, deg: int):
        th = math.radians(deg)
        self.f, self.c, self.s, self.items = f, math.cos(th), math.sin(th), []

    def pt(self, a, b, h):
        return (CX - a * self.c + b * self.s, GY - h + (a * self.s + b * self.c) * TILT, a * self.s + b * self.c)

    def add(self, depth, fn):
        self.items.append((depth, len(self.items), fn))

    def run(self):
        for _, _, fn in sorted(self.items, key=lambda t: (t[0], t[1])):
            fn()

    # --- primitives
    def cap(self, p0, p1, w, pal, bias=0.0):
        a, b = self.pt(*p0), self.pt(*p1)
        S = stroke(a, b, w)
        self.add((a[2] + b[2]) / 2 + bias, lambda: fill(self.f, S, pal))

    def blob(self, p, rx, ry, pal, bias=0.0):
        a = self.pt(*p)
        S = disc(a[0], a[1], rx, ry)
        self.add(a[2] + bias, lambda: fill(self.f, S, pal))

    def box(self, ac, cb, ah, bh, h0, h1, pal, bias=0.0, round_=False):
        """rectangular (or elliptical) slab, axis-aligned to the body."""
        S = set()
        h = h0
        while h <= h1:
            x, y, _ = self.pt(ac, cb, h)
            hw = ah * abs(self.c) + bh * abs(self.s)
            if round_:
                hw = math.hypot(ah * self.c, bh * self.s)
            for xx in range(round(x - hw + .01), round(x + hw - .01) + 1):
                S.add((xx, round(y)))
            h += .5
        d = ac * self.s + cb * self.c
        self.add(d + bias, lambda: fill(self.f, S, pal))
        return S


# ---------------------------------------------------------------- body parts
def legs(sc: Scene, pose):
    dz = pose["dz"]
    for side, (a, bf, lift) in zip((1, -1), pose["legs"]):
        a = a  # signed already
        hip = (a, 0, 27 + dz + 0.5)
        ank = (a, bf, 3 + lift)
        kb = bf * .5 + (1.6 if pose["crouch"] and bf >= 0 else .6 if bf > 0 else -.2 + (.8 if pose["crouch"] else 0))
        knee = (a, kb, 14.5 + dz * .5 + lift * .5)
        mid = ((knee[0] + ank[0]) / 2, (knee[1] + ank[1]) / 2, (knee[2] + ank[2]) / 2 + 1)
        sc.cap(hip, knee, 4, PANTS, -6)
        sc.cap(knee, mid, 4, PANTS, -6)
        sc.cap(mid, ank, 4, LEGGING, -6)
        sc.cap((a, bf - 1.3, 1.2 + lift), (a, bf + 2.8, 1.0 + lift), 3, BOOT, -6)


def torso(sc: Scene, pose):
    dz = pose["dz"]
    S, belt = set(), set()
    h = 27.0 + dz
    while h <= 42 + dz:
        t = (h - 27 - dz) / 15
        A = 4.5 + 1.0 * min(t * 1.6, 1) - 0.5 * max(t - .85, 0) * 6
        B = 3.2
        x, y, _ = sc.pt(0, 0, h)
        hw = math.hypot(A * sc.c, B * sc.s)
        row = {(xx, round(y)) for xx in range(round(x - hw + .01), round(x + hw - .01) + 1)}
        S |= row
        if h < 29.4 + dz:
            belt |= row
        h += .5
    jacket = S - belt

    def go():
        fill(sc.f, jacket, JACKET)
        fill(sc.f, belt, LEATHER)
        # chest pockets / zipper hint on the front, collar line
        if sc.c > .3:
            for sgn in (-1, 1):
                x, y, _ = sc.pt(sgn * 2.2, 2.6, 36 + dz)
                sc.f.rect(round(x) - 1, round(y), round(x), round(y) + 1, JACKET[0])
    sc.add(0, go)


def head(sc: Scene, pose):
    dz, hb, hh = pose["dz"], pose["hb"], pose["hh"]
    hz = dz + hh
    c, s = sc.c, sc.s
    fx, fy, fd = sc.pt(0, .8 + hb, 45.6 + hz)
    nx, ny, _ = sc.pt(0, hb * .5, 41.8 + dz)
    neck = {(round(nx) + i, round(ny) + j) for i in (-1, 0, 1) for j in range(0, 3)}
    face = disc(fx, fy, 3.9, 3.4)
    dx, dy, dd = sc.pt(0, .3 + hb, 50.6 + hz)
    dome = disc(dx, dy, 5.7, 4.8)
    bandy = dy + 4.8 - 2.5
    band = {p for p in dome if p[1] >= bandy}
    top = dome - band
    back = c < -.5
    eyes = []
    for sgn in (-1, 1):
        ex, ey, ed = sc.pt(sgn * 1.6, 3.0 + hb, 45.2 + hz)
        if ed > .3:
            eyes.append((round(ex), round(ey)))
    ear = None
    if abs(s) > .3:
        ear = sc.pt(math.copysign(3.7, s), -.4 + hb, 45.6 + hz)
    cheek = sc.pt(0, 3.2 + hb, 44.8 + hz) if (c < -.2 and s > .3) else None

    def go():
        fill(sc.f, neck, SKIN if not back else SKIN)
        fill(sc.f, face, HAIR if back else SKIN)
        if cheek and not back:
            pass
        if cheek:
            fill(sc.f, disc(cheek[0], cheek[1], 1.3, 1.6), SKIN)
        # shade under brim
        for (x, y) in face:
            if (x, y - 1) in dome and not back:
                sc.f.px(x, y, SKIN[0])
        for (x, y) in eyes:
            sc.f.px(x, y, P["eye"])
        if ear and ear[2] > 0:
            sc.f.px(round(ear[0]), round(ear[1]), SKIN[0])
            sc.f.px(round(ear[0]), round(ear[1]) + 1, SKIN[0])
        fill(sc.f, top, HELMET)
        # smooth highlight streak on dome
        for (x, y) in top:
            if (x - 1, y - 1) not in dome or (x, y - 1) not in dome:
                sc.f.px(x, y, HELMET[2])
        # team band: lower rows of the dome, grayscale shading
        for (x, y) in band:
            g = TEAMG[0] if ((x + 1, y) not in dome or (x, y + 1) not in dome) else \
                TEAMG[2] if ((x - 1, y) not in dome) else TEAMG[1]
            sc.f.px(x, y, g, team=True)
    sc.add(fd + 1.5, go)


def arm(sc: Scene, side, elbow, hand, pose):
    dz = pose["dz"]
    sh = (5.4 * side, 0, 40.2 + dz)
    sx, sy, sd = sc.pt(*sh)
    ex, ey, ed = sc.pt(*elbow)
    hx, hy, hd = sc.pt(*hand)
    up = stroke((sx, sy), (ex, ey), 3)
    fo = stroke((ex, ey), (hx, hy), 3)
    hnd = disc(hx, hy, 1.4, 1.4)
    px_, py_, _ = sc.pt(6.1 * side, 0, 39.2 + dz)

    def go():
        fill(sc.f, up, JACKET)
        for dx, g in ((0, TEAMG[2]), (1, TEAMG[1])):
            for dy in range(3):
                sc.f.px(round(px_) - 1 + dx, round(py_) - 1 + dy, g, team=True)
        fill(sc.f, fo, JACKET)
        fill(sc.f, hnd, SKIN)
    sc.add((sd + ed + hd) / 3 + .3, go)


def carbine(sc: Scene, B, M, pose):
    """short M1 carbine: small wood stock + short steel barrel."""
    bx, by, bd = sc.pt(*B)
    mx, my, md = sc.pt(*M)
    k = .42
    jx, jy = bx + (mx - bx) * k, by + (my - by) * k
    stock = stroke((bx, by), (jx, jy), 3)
    barrel = stroke((jx, jy), (mx, my), 2)
    sling = stroke((jx, jy), (jx + (mx - jx) * .55, jy + (my - jy) * .55), 3) - stock

    def go():
        fill(sc.f, stock, WOOD)
        fill(sc.f, sling - barrel, WOOD)
        fill(sc.f, barrel - sling, STEEL)
        # thin steel receiver over wood handguard transition
        for (x, y) in barrel:
            if (x, y) not in sling and (x - 1, y - 1) not in barrel:
                sc.f.px(x, y, STEEL[2])
    sc.add((bd + md) / 2 + .6, go)
    return (mx, my, md), (bx, by, bd)


def gear(sc: Scene, pose):
    dz = pose["dz"]
    # radio on the back + antenna
    sc.box(0, -5.2, 3.6, 2.0, 29 + dz, 40 + dz, RADIO, bias=0)
    sc.box(0, -7.3, 2.4, .35, 31 + dz, 38 + dz, STEEL, bias=-.2)  # front panel plate / handset side
    a0 = sc.pt(2.2, -5.2, 40 + dz)
    a1 = sc.pt(3.4, -7.3, 64)
    A = stroke(a0, a1, 1)
    sc.add(-4.7 * sc.c - .5, lambda: [sc.f.px(x, y, P["steel_light"]) for (x, y) in A])
    # map case on right hip
    sc.box(6.0, -.5, 1.0, 2.6, 23 + dz, 30 + dz, CANVAS, bias=.2)
    # binoculars on chest
    if not pose["binoc"]:
        sc.box(0, 3.8, 2.9, 1.2, 34 + dz, 37.5 + dz, STEEL, bias=.1)
        sc.cap((-2.4, 2.4, 40.5 + dz), (-1.9, 3.8, 37.5 + dz), 1, LEATHER, .1)
        sc.cap((2.4, 2.4, 40.5 + dz), (1.9, 3.8, 37.5 + dz), 1, LEATHER, .1)


def binoculars(sc: Scene, pose):
    for sgn in (-1, 1):
        sc.blob((sgn * 1.5, 5.4, 46.2 + pose["dz"]), 1.5, 2.0, STEEL, 3.0)
    sc.blob((0, 5.2, 46.2), .7, .8, STEEL, 3.1)


# ---------------------------------------------------------------- poses
def make_pose(fname):
    p = dict(dz=0, crouch=False, hb=0, hh=0, binoc=False, gun=None, flash=False, recoil=0)
    legs_idle = [(3.0, 0, 0), (-3.0, 0, 0)]
    p["legs"] = legs_idle
    gun_low = ((3.2, -1.5, 29), (2.2, 11, 25))
    rh, re_ = (3.8, 2.2, 28.3), (6.8, .3, 33.5)
    lh, le = (2.0, 6.2, 27.0), (-4.2, 3.2, 33.2)
    p.update(gun=gun_low, rh=rh, re=re_, lh=lh, le=le)
    if fname in ("walk_a", "walk_b"):
        sw = 1 if fname == "walk_a" else -1
        p["dz"] = -1
        p["legs"] = [(2.8, 3.6 * sw, 0 if sw > 0 else 1.6), (-2.8, -3.4 * sw, 0 if sw < 0 else 1.6)]
        # left arm swings with the carbine slightly
        sh = -sw * 1.0
        p["gun"] = ((3.2, -1.5 + sh * .6, 28), (2.2, 11 + sh * .6, 24))
        p["rh"] = (3.4, 2.2 + sh * .6, 27.3)
        p["lh"] = (1.7, 6.2 + sh * .8, 26.0)
        p["le"] = (-4.2, 3.2 + sh * .5, 32.2)
        p["re"] = (6.8, .3 + sh * .3, 32.5)
    elif fname in ("aim", "fire"):
        r = 1.0 if fname == "fire" else 0
        p.update(dz=-2, crouch=True, hb=1.0, hh=.4,
                 legs=[(2.8, -2.0, 0), (-2.8, 2.6, 0)],
                 gun=((3.0, -.5 - r, 39.5), (2.0, 13 - r, 41.0)),
                 rh=(3.2, 2.4 - r, 38.4), re=(7.0, .5 - r * .5, 34.0),
                 lh=(1.6, 8.2 - r * .7, 40.3), le=(-3.0, 4.0 - r * .5, 35.0),
                 flash=fname == "fire", recoil=r)
        if fname == "fire":
            p["hb"] = .6
    elif fname == "idle_alt":
        p.update(gun=None, binoc=True, hh=.5,
                 rh=(2.7, 4.4, 45.3), re=(7.2, 1.5, 37.6),
                 lh=(-2.7, 4.4, 45.3), le=(-7.2, 1.5, 37.6))
    return p


def flash_fx(f: Frame, sc: Scene, pose):
    (mx, my, md), (bx, by, bd) = pose["_muz"]
    dx, dy = mx - bx, my - by
    n = math.hypot(dx, dy)
    if n < 1:  # firing straight at / away from camera
        dx, dy, n = 0, 1, 1
    dx, dy = dx / n, dy / n
    behind = md < -2
    cx, cy = mx + dx * 1.5, my + dy * 1.5

    def put(x, y, c):
        x, y = round(x), round(y)
        if behind and 0 <= x < f.w and 0 <= y < f.h:
            a = f.base.getpixel((x, y))
            if a[3] and a[:3] != P["outline"][:3]:
                return
        f.px(x, y, c)
    for (x, y) in disc(cx, cy, 2.2, 2.2):
        put(x, y, P["flash_edge"])
    for (x, y) in disc(cx, cy, 1.5, 1.5):
        put(x, y, P["flash"])
    put(cx, cy, P["flash_core"])
    put(cx - 1, cy, P["flash_core"]); put(cx, cy - 1, P["flash_core"])
    for i in range(3, 5):  # short spike in the firing direction
        put(cx + dx * i, cy + dy * i, P["flash"])
    smoke = (205, 205, 205, 150)
    for (ox, oy) in ((1, -3), (-1, -5), (2, -6), (0, -7)):
        put(mx + dx * 3 + ox, my + dy * 3 + oy, smoke)


# ---------------------------------------------------------------- frame builder
def frame(dname: str, fname: str) -> Frame:
    f = Frame()
    sc = Scene(f, ANG[dname])
    pose = make_pose(fname)
    dz = pose["dz"]
    legs(sc, pose)
    gear(sc, pose)
    torso(sc, pose)
    head(sc, pose)
    arm(sc, 1, pose["re"], pose["rh"], pose)
    arm(sc, -1, pose["le"], pose["lh"], pose)
    if pose["gun"]:
        B, M = pose["gun"]
        pose["_muz"] = carbine(sc, B, M, pose)
    if pose["binoc"]:
        binoculars(sc, pose)
    sc.run()
    f.outline()
    if pose["flash"]:
        flash_fx(f, sc, pose)
    return f


if __name__ == "__main__":
    save_sheet("scout", frame)
    print("scout done")
