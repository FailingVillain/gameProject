"""Combat engineer / worker sprite sheet (key: worker). Procedural, deterministic.
Run: python tools/make_worker.py  ->  out/worker/*
Layers per frame: legs, far arm, torso(+bag), [tool+arms], head, [tool+arms if 'late'].
Each layer can be shifted (walk bob, head lean) and is merged, then outlined.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import FW, FH, P, Frame, save_sheet  # noqa: E402

# ---- extra colours (HBT fatigues are a bit greener/lighter than the rifleman jacket) ----
W, WL, WD = (104, 112, 66, 255), (134, 142, 86, 255), (72, 80, 46, 255)
T, TL, TD = (88, 94, 56, 255), (112, 118, 72, 255), (58, 62, 38, 255)
G, GL, GD = (156, 118, 66, 255), (184, 146, 88, 255), (106, 76, 42, 255)   # work gloves
SB = (196, 202, 210, 255)                                                   # bright steel
WOODL = (158, 110, 66, 255)
BAG, BAGL, BAGD = P["canvas"], P["canvas_light"], (112, 104, 74, 255)
DUST, DUST2 = (206, 190, 150, 255), (158, 140, 100, 255)

CFG = {  # shoulders a/b, far arm behind torso?, torso centre/width, hips
    "s":  dict(sa=(24, 38), sb=(40, 38), bb=False, cx=32, tw=19, ha=(28, 53), hb=(36, 53)),
    "se": dict(sa=(26, 38), sb=(38, 38), bb=True,  cx=32, tw=16, ha=(29, 53), hb=(35, 53)),
    "e":  dict(sa=(31, 38), sb=(35, 38), bb=True,  cx=32, tw=12, ha=(31, 53), hb=(33, 53)),
    "ne": dict(sa=(37, 38), sb=(28, 38), bb=True,  cx=32, tw=15, ha=(34, 53), hb=(30, 53)),
    "n":  dict(sa=(40, 38), sb=(24, 38), bb=False, cx=32, tw=19, ha=(36, 53), hb=(28, 53)),
}

# tool: butt, head, blade length, hand-a t, hand-b t
TOOLS = {
    "idle": {
        "s": ((22, 54), (45, 27), 9, .28, None), "se": ((24, 54), (43, 27), 9, .28, None),
        "e": ((41, 54), (24, 26), 9, .25, None), "ne": ((42, 52), (23, 27), 9, .25, None),
        "n": ((42, 54), (19, 27), 9, .28, None)},
    "aim": {
        "s": ((30, 44), (47, 14), 9, .2, .55), "se": ((30, 44), (45, 13), 9, .2, .55),
        "e": ((38, 46), (22, 14), 9, .2, .5), "ne": ((38, 46), (22, 14), 9, .2, .5),
        "n": ((34, 44), (17, 14), 9, .2, .55)},
    "fire": {
        "s": ((30, 32), (35, 60), 6, .3, .5), "se": ((31, 32), (40, 60), 7, .3, .5),
        "e": ((28, 34), (46, 62), 8, .25, .5), "ne": ((30, 34), (42, 56), 8, .25, .5),
        "n": ((34, 48), (34, 21), 9, .2, .45)},
    "lean": {
        "s": ((35, 40), (36, 64), 8, .05, .17), "se": ((36, 40), (38, 64), 8, .05, .17),
        "e": ((40, 41), (42, 64), 8, .05, .18), "ne": ((38, 40), (40, 64), 8, .05, .18),
        "n": ((30, 40), (29, 64), 8, .05, .18)},
}
HEAD_OFF = {"fire": {"s": (0, 2), "se": (1, 2), "e": (2, 1), "ne": (2, 0), "n": (0, 0)},
            "lean": {"s": (0, 1), "se": (1, 1), "e": (1, 1), "ne": (1, 1), "n": (0, 1)},
            "aim": {"s": (0, 0), "se": (0, 0), "e": (0, -1), "ne": (0, -1), "n": (0, 0)}}


# ------------------------------------------------------------------ helpers
def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def ipt(p):
    return (int(round(p[0])), int(round(p[1])))


def box(f, x0, y0, x1, y1, c, l, d, rnd=True):
    """Shaded box: light top/left, dark bottom/right, rounded corners."""
    f.rect(x0, y0, x1, y1, c)
    f.rect(x0, y0, x0 + 1, y1, l)
    f.rect(x0, y0, x1, y0, l)
    f.rect(x1 - 1, y0 + 1, x1, y1, d)
    f.rect(x0 + 1, y1, x1, y1, d)
    if rnd:
        for x, y in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
            f.px(x, y, (0, 0, 0, 0))


def limb(f, p0, p1, w, c, l, d):
    p0, p1 = ipt(p0), ipt(p1)
    f.line(*p0, *p1, c, w)
    f.line(p0[0] - 1, p0[1] - 1, p1[0] - 1, p1[1] - 1, l, 1) if w >= 3 else None
    f.line(p0[0] + 1, p0[1] + 1, p1[0] + 1, p1[1] + 1, d, 1) if w >= 4 else None


def poly(f, pts, color):
    ImageDraw.Draw(f.base).polygon([ipt(p) for p in pts], fill=color)


def shifted(src: Frame, dx, dy):
    out = Frame()
    for a, b in ((src.base, out.base), (src.team, out.team)):
        b.paste(a, (dx, dy))
    return out


def merge(dst: Frame, src: Frame, dx=0, dy=0):
    if dx or dy:
        src = shifted(src, dx, dy)
    dst.base.alpha_composite(src.base)
    t = src.team
    dst.team.alpha_composite(t)
    # team pixels replace base pixels (Frame.px semantics)
    ta, bp = t.split()[3].load(), dst.base.load()
    for y in range(dst.h):
        for x in range(dst.w):
            if ta[x, y]:
                bp[x, y] = (0, 0, 0, 0)


# ------------------------------------------------------------------ parts
def legs(f, d, ph, lean):
    c = CFG[d]
    side = d in ("e", "ne")
    k = 6 if d == "e" else 4
    if side:
        fa = (32 - 2 + k * ph, 71 - (1 if ph < 0 else 0))
        fb = (32 + 2 - k * ph, 71 - (1 if ph > 0 else 0))
    elif d == "se":
        fa = (28 + ph, 71 + 3 * ph)
        fb = (37 - ph, 71 - 3 * ph)
    else:
        fa = (27, 71 + 3 * ph)
        fb = (37, 71 - 3 * ph)
        if d == "n":
            fa, fb = (37, 71 + 3 * ph), (27, 71 - 3 * ph)
    if lean:
        fb = (fb[0] - 1, 71)
    order = [(c["hb"], fb, True), (c["ha"], fa, False)]   # far leg first
    if d in ("s", "se"):
        order.sort(key=lambda o: o[1][1])
    for hip, foot, far in order:
        cc, ll, dd = (TD, T, TD) if far else (T, TL, TD)
        limb(f, hip, (foot[0], foot[1] - 4), 5, cc, ll, dd)
        # canvas legging over the lower leg
        f.rect(foot[0] - 2, foot[1] - 8, foot[0] + 2, foot[1] - 4, BAGD if far else BAG)
        f.rect(foot[0] - 2, foot[1] - 8, foot[0] - 2, foot[1] - 4, BAGL)
        f.rect(foot[0] - 2, foot[1] - 8, foot[0] + 2, foot[1] - 8, BAGD)
        # boot
        bx = foot[0]
        f.rect(bx - 3, foot[1] - 3, bx + 3, foot[1], P["leather_dark"] if far else P["leather"])
        f.rect(bx - 3, foot[1] - 3, bx - 3, foot[1] - 1, GL if False else P["leather"])
        f.rect(bx - 3, foot[1], bx + 3, foot[1], P["outline"])
        if side:
            f.rect(bx + 3, foot[1] - 2, bx + 4, foot[1] - 1, P["leather_dark"])
        f.px(bx - 3, foot[1] - 3, (0, 0, 0, 0))


def torso(f, d):
    c = CFG[d]
    x0, x1 = c["cx"] - c["tw"] // 2, c["cx"] + c["tw"] // 2
    box(f, x0, 36, x1, 55, W, WL, WD)
    # shoulder yoke slope
    for x in (x0 + 1, x1 - 1):
        f.px(x, 36, (0, 0, 0, 0))
    cx = c["cx"]
    back = d in ("n", "ne")
    if d == "s":
        f.rect(cx, 37, cx, 52, WD)                           # placket
        for px0 in (26, 36):                                   # chest pockets
            f.rect(px0, 41, px0 + 3, 44, W)
            f.rect(px0, 41, px0 + 3, 41, WD)
            f.rect(px0 + 3, 41, px0 + 3, 44, WD)
            f.rect(px0, 44, px0 + 3, 44, WD)
        f.rect(29, 36, 35, 37, P["skin_dark"])                 # open collar
        f.rect(30, 38, 34, 38, P["skin_dark"])
        f.rect(28, 36, 28, 37, WL)
        f.rect(36, 36, 36, 37, WL)
    elif d == "se":
        f.rect(cx + 2, 37, cx + 2, 52, WD)
        f.rect(26, 41, 29, 44, W)
        f.rect(26, 41, 26, 44, WD)
        f.rect(26, 44, 29, 44, WD)
        f.rect(30, 36, 35, 37, P["skin_dark"])
    elif d == "e":
        f.rect(30, 41, 33, 44, W)
        f.rect(30, 41, 33, 41, WD)
        f.rect(33, 41, 33, 44, WD)
        f.rect(30, 44, 33, 44, WD)
        f.rect(33, 36, 35, 37, P["skin_dark"])
    else:                                                      # back: seam + yoke
        f.rect(cx, 38, cx, 53, WD)
        f.rect(x0 + 2, 41, x1 - 2, 41, WD)
    # tool belt
    f.rect(x0, 53, x1, 55, P["leather"])
    f.rect(x0, 55, x1, 55, P["leather_dark"])
    if d in ("s", "se"):
        bx = cx if d == "s" else cx + 1
        f.rect(bx - 1, 53, bx + 1, 55, P["steel_light"])
        f.px(bx, 54, P["steel_dark"])
        # canvas toolbag strap across the chest
        f.line(27, 37, 38, 53, BAG, 2)
        f.line(26, 37, 37, 53, BAGL, 1)
        f.line(29, 37, 40, 53, BAGD, 1) if False else None
    # shirt-tail / hip shading
    f.rect(x0, 52, x0 + 1, 52, WL)


def bag(f, d):
    """Canvas toolbag / haversack."""
    spec = {"s": (42, 47, 47, 58), "se": (39, 48, 44, 58), "e": (23, 45, 30, 56),
            "ne": (22, 44, 30, 55), "n": (25, 40, 38, 55)}[d]
    x0, y0, x1, y1 = spec
    box(f, x0, y0, x1, y1, BAG, BAGL, BAGD)
    f.rect(x0, y0 + 3, x1, y0 + 3, BAGD)                      # flap edge
    f.rect(x0 + 1, y0 + 1, x1 - 1, y0 + 2, BAG)
    f.rect(x0 + 1, y0 + 1, x1 - 1, y0 + 1, BAGL)
    f.px((x0 + x1) // 2, y0 + 4, P["leather"])                # buckle strap
    f.px((x0 + x1) // 2, y0 + 5, P["leather"])
    if d == "n":                                              # extra: rolled tarp on top
        f.rect(x0 - 1, y0 - 3, x1 + 1, y0 - 1, P["khaki"])
        f.rect(x0 - 1, y0 - 3, x1 + 1, y0 - 3, P["khaki_light"])
        f.rect(x0 - 1, y0 - 1, x1 + 1, y0 - 1, P["khaki_dark"])
        f.rect(x0 + 3, y0 - 3, x0 + 3, y0 - 1, P["leather"])
        f.rect(x1 - 3, y0 - 3, x1 - 3, y0 - 1, P["leather"])


def arm(f, d, which, hand, patch=False):
    sh = CFG[d]["sa" if which == "a" else "sb"]
    cx = CFG[d]["cx"]
    sx = -1 if sh[0] < cx - 2 else (1 if sh[0] > cx + 2 else -1)
    if d in ("e", "ne") and which == "a":
        sx = -1
    mid = lerp(sh, hand, 0.5)
    el = (mid[0] + sx * 2, mid[1] + 1.5)
    far = d in ("se", "e", "ne") and which == "b"
    c, l, dk = (WD, W, WD) if far else (W, WL, WD)
    limb(f, sh, el, 4, c, l, dk)
    f.ellipse(el[0], el[1], 2, 1.5, WL if not far else W)   # rolled cuff
    f.line(*ipt(el), *ipt(hand), (P["skin_dark"] if far else P["skin"]), 3)
    f.line(el[0] - 1, el[1], hand[0] - 1, hand[1], P["skin_light"], 1) if not far else None
    hx, hy = ipt(hand)
    f.rect(hx - 1, hy - 1, hx + 1, hy + 1, GD if far else G)
    f.rect(hx - 1, hy - 1, hx, hy - 1, GL)
    f.rect(hx - 1, hy + 1, hx + 1, hy + 1, GD)


def shoulder_patch(f, d):
    sh = CFG[d]["sa"]
    x, y = sh[0] - 2, sh[1] + 1
    if d in ("n", "ne"):
        return
    f.rect(x - 1, y - 1, x + 4, y + 4, P["outline"])
    f.rect(x, y, x + 3, y + 3, (0, 0, 0, 0))
    for yy in range(4):
        for xx in range(4):
            g = 215 - 25 * (xx + yy) // 2
            f.px(x + xx, y + yy, g, team=True)


def tool(f, butt, head, blade):
    (bx, by), (hx, hy) = butt, head
    L = math.hypot(hx - bx, hy - by)
    dx, dy = (hx - bx) / L, (hy - by) / L
    nx, ny = -dy, dx
    f.line(bx, by, hx, hy, P["wood"], 2)
    steep = abs(dy) > abs(dx)
    f.line(bx - (1 if steep else 0), by - (0 if steep else 1), hx - (1 if steep else 0),
           hy - (0 if steep else 1), WOODL, 1)
    f.line(bx, by, hx, hy, P["wood_dark"], 1)
    f.rect(bx - 2, by - 1, bx + 1, by, P["wood_dark"])         # D-grip
    f.px(bx - 2, by - 1, WOODL)
    # ferrule + blade
    c0 = (hx - dx * 1, hy - dy * 1)

    def strip(stops, color):
        pts = [(c0[0] + dx * t + nx * w, c0[1] + dy * t + ny * w) for t, w in stops]
        pts += [(c0[0] + dx * t - nx * w, c0[1] + dy * t - ny * w) for t, w in reversed(stops)]
        poly(f, pts, color)

    B = blade
    wide = 3.9 if blade >= 8 else 4.4
    strip([(0, 1.4), (2, 1.6), (3, wide - .6), (4.5, wide + .3), (B * 0.7, wide + .6), (B - 1, wide - .4), (B + .5, wide - 2)], P["steel_dark"])
    strip([(3.2, wide - 1.6), (4.5, wide - .6), (B * 0.7, wide - .3), (B - 1.2, wide - 1.6)], P["steel_light"])
    sgn = 1 if (nx * -1 + ny * -1) > 0 else -1
    pts = [(c0[0] + dx * t + nx * sgn * w, c0[1] + dy * t + ny * sgn * w)
           for t, w in ((3.6, 0.2), (B * 0.7, 0.2), (B - 1.6, 0.2),
                        (B - 1.6, wide - 2.2), (B * 0.7, wide - 1.0), (4.5, wide - 1.2))]
    poly(f, pts, (156, 162, 172, 255))
    f.line(*ipt((c0[0] + dx * 4.5 + nx * sgn * (wide - 1.6), c0[1] + dy * 4 + ny * sgn * (wide - 1.6))),
           *ipt((c0[0] + dx * (B - 2) + nx * sgn * (wide - 2.4), c0[1] + dy * (B - 2) + ny * sgn * (wide - 2.4))),
           SB, 1)
    return (c0[0] + dx * (B + 1), c0[1] + dy * (B + 1))       # blade tip


def head(f, d):
    cx = 32
    if d in ("s", "se"):
        v = 1 if d == "se" else 0
        f.rect(29 + v, 33, 35 + v, 37, P["skin_dark"])
        f.ellipse(32 + v, 30, 5.5, 5, P["skin"])
        f.rect(36 + v - 1, 29, 37 + v - 1, 33, P["skin_dark"])
        f.rect(26 + v, 31, 27 + v, 31, P["skin"]) if False else None
        f.rect(28 + v, 33, 36 + v, 34, P["skin_dark"])         # chin shadow
        f.rect(27 + v, 28, 37 + v, 28, P["skin_dark"])         # helmet brow shadow
        f.px(29 + v, 30, P["eye"])
        f.px(35 + v, 30, P["eye"])
        f.px(32 + v, 31, P["skin_light"])
        f.px(32 + v, 32, P["skin_dark"])
        f.px(31 + v, 33, P["leather_dark"])                     # chin strap
        f.px(33 + v, 33, P["leather_dark"])
        helmet(f, 32 + v, 22, 9.5, 7.5, 26, brim=(32 + v, 26, 11, 3.6))
    elif d == "e":
        f.rect(29, 33, 33, 37, P["skin_dark"])
        f.ellipse(35, 30, 4.5, 5, P["skin"])
        f.rect(33, 27, 33, 33, P["skin_dark"])
        f.px(37, 29, P["eye"])
        f.px(39, 31, P["skin_light"])
        f.rect(33, 28, 39, 28, P["skin_dark"])
        f.rect(31, 29, 32, 31, P["skin_dark"])                 # ear
        f.rect(33, 33, 37, 33, P["skin_dark"])
        f.line(32, 28, 35, 33, P["leather_dark"], 1)           # chin strap
        helmet(f, 31, 22, 9, 7.5, 26, brim=(33, 26, 10.5, 3.2))
    elif d == "ne":
        f.rect(29, 31, 34, 36, P["skin_dark"])
        f.ellipse(36, 30, 2.2, 3, P["skin_dark"])               # cheek/ear edge
        helmet(f, 32, 23, 9.5, 8, 27, brim=(32, 27, 10.8, 3.5))
    else:
        f.rect(28, 30, 36, 36, P["skin_dark"])
        f.rect(28, 30, 29, 34, P["skin"])
        helmet(f, 32, 23, 9.5, 8, 27, brim=(32, 27, 10.8, 3.5))


def helmet(f, cx, cy, rx, ry, band_y, brim):
    f.ellipse(*brim, P["helmet_dark"])
    f.ellipse(cx, cy, rx, ry, P["helmet"])
    f.ellipse(cx - 2.5, cy - 3, rx * .5, ry * .4, P["helmet_light"])
    # shadow side + net texture
    for y in range(int(cy - ry), int(cy + ry) + 1):
        for x in range(int(cx - rx), int(cx + rx) + 1):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 > 1:
                continue
            if (x - cx) > rx * .45 and y > cy - ry * .4:
                f.px(x, y, P["helmet_dark"])
            elif (x * 3 + y * 2) % 7 == 0 and f.base.getpixel((x, y)) == P["helmet"]:
                f.px(x, y, P["net"])
    # team band (grayscale shading)
    for y in (band_y - 1, band_y):
        for x in range(int(cx - rx), int(cx + rx) + 1):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                s = (x - cx) / rx
                f.px(x, y, int(225 - 90 * (s + 1) / 2) - (25 if y == band_y else 0), team=True)


def dust(f, tip, big):
    tx, ty = ipt(tip)
    offs = [(-4, 0), (4, 0), (-6, -1), (6, -1), (-2, -2), (2, -2), (0, -3), (-3, 1), (3, 1),
            (-5, 1), (5, 1), (-1, -4), (1, -5)]
    for i, (ox, oy) in enumerate(offs):
        f.px(tx + ox, ty + oy, DUST if i % 2 == 0 else DUST2)
        if big:
            f.px(tx + ox, ty + oy - 1, DUST if i % 3 == 0 else DUST2)
    for ox, oy in ((-7, -3), (7, -4), (1, -7), (-3, -5)):
        f.px(tx + ox, ty + oy, P["flash"])
    f.px(tx, ty - 1, P["flash_core"])
    f.px(tx - 1, ty, P["flash_core"])


# ------------------------------------------------------------------ frame assembly
def worker(d, fname):
    pose = {"idle": "idle", "walk_a": "idle", "walk_b": "idle", "aim": "aim", "fire": "fire",
            "idle_alt": "lean"}[fname]
    ph = {"walk_a": 1, "walk_b": -1}.get(fname, 0)
    bob = 1 if ph else 0
    c = CFG[d]
    butt, hd, blade, ta, tb = TOOLS[pose][d]
    late = pose == "aim"

    def layer():
        return Frame()

    L_legs = layer()
    legs(L_legs, d, ph, pose == "lean")

    L_far = layer()
    L_torso = layer()
    L_tool = layer()
    L_head = layer()

    # arms: far arm (b) behind torso where applicable, otherwise with the tool layer
    hands = {}
    for which, t in (("a", ta), ("b", tb)):
        if t is not None:
            hands[which] = lerp(butt, hd, t)
        else:
            sh = c["sa" if which == "a" else "sb"]
            out = 0 if d in ("e", "ne") else (-1 if sh[0] < 32 else 1)
            hand = (sh[0] + out + (0 if d in ("e", "ne") else 0), sh[1] + 13)
            if d in ("e", "ne", "se"):
                hand = (sh[0] + 4 * ph * (1 if which == "b" else -1) * (1 if d == "e" else .7) - 1, sh[1] + 13)
            else:
                hand = (hand[0], hand[1] - 2 * ph * (1 if which == "a" else -1) * (-1 if d == "s" else 1))
            hands[which] = hand
    far_behind = c["bb"]
    if far_behind:
        arm(L_far, d, "b", hands["b"])
    if d in ("s", "n"):          # bag hangs on the hip, drawn before torso for s (beside it)
        pass
    if d == "s" or d == "se":
        bag(L_far, d)
    torso(L_torso, d)
    if d not in ("s", "se"):
        bag(L_torso, d)
    tip = tool(L_tool, butt, hd, blade)
    if not far_behind:
        arm(L_tool, d, "b", hands["b"])
    arm(L_tool, d, "a", hands["a"])
    shoulder_patch(L_tool, d)
    head(L_head, d)

    hx, hy = HEAD_OFF.get(pose, {}).get(d, (0, 0))
    if pose == "idle" and False:
        pass
    f = Frame()
    merge(f, L_legs)
    merge(f, L_far, 0, bob)
    merge(f, L_torso, 0, bob)
    if not late:
        merge(f, L_tool, 0, bob)
        merge(f, L_head, hx, hy + bob)
    else:
        merge(f, L_head, hx, hy + bob)
        merge(f, L_tool, 0, bob)
    f.outline()
    if pose == "fire":
        dust(f, (tip[0], tip[1] + (2 if d in ("s", "se", "e", "ne") else -2)), d in ("e", "s"))
    return f


if __name__ == "__main__":
    save_sheet("worker", worker)
    print("worker sheet written")
