"""Hero portraits (320x320) for infantry / scout / worker. Designed on a 160x160 grid, scaled x2 (nearest).
Parts are drawn back-to-front as masks; paint() adds outline + rim-light/shade bands (light from top-left)."""
from __future__ import annotations
import os, sys
from functools import reduce
from PIL import Image, ImageDraw, ImageChops

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import P, save_portrait, PORTRAIT  # noqa: E402

S = 160
OUTL = P["outline"]


# ---------------------------------------------------------------- mask helpers
def M(): return Image.new("L", (S, S), 0)
def E(cx, cy, rx, ry):
    m = M(); ImageDraw.Draw(m).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255); return m
def PG(pts):
    m = M(); ImageDraw.Draw(m).polygon(pts, fill=255); return m
def RC(x0, y0, x1, y1):
    m = M(); ImageDraw.Draw(m).rectangle([x0, y0, x1, y1], fill=255); return m
def LN(pts, w=1):
    m = M(); ImageDraw.Draw(m).line(pts, fill=255, width=w); return m
def U(*ms): return reduce(ImageChops.lighter, ms)
def I(a, b): return ImageChops.darker(a, b)
def SUB(a, b): return ImageChops.subtract(a, b)
def sh(m, dx, dy, fill=0):
    return m.transform(m.size, Image.AFFINE, (1, 0, -dx, 0, 1, -dy), fillcolor=fill)
def dil(m): return U(m, sh(m, 1, 0), sh(m, -1, 0), sh(m, 0, 1), sh(m, 0, -1))
def bbox_px(m):
    bb = m.getbbox()
    if not bb: return
    px = m.load()
    for y in range(bb[1], bb[3]):
        for x in range(bb[0], bb[2]):
            if px[x, y]: yield x, y

def rp(*cs): return tuple(tuple(c) if len(c) == 4 else (*c, 255) for c in cs)

# ramps: (deep, dark, mid, light)
R_OD = rp((38, 42, 24), P["od_dark"][:3], P["od"][:3], P["od_light"][:3])
R_ODL = rp((50, 54, 32), P["od"][:3], P["od_light"][:3], (138, 142, 92))
R_HEL = rp((40, 44, 28), P["helmet_dark"][:3], P["helmet"][:3], P["helmet_light"][:3])
R_KHA = rp((84, 72, 48), P["khaki_dark"][:3], P["khaki"][:3], P["khaki_light"][:3])
R_CAN = rp((88, 80, 56), (118, 108, 78), P["canvas"][:3], P["canvas_light"][:3])
R_LEA = rp((40, 26, 16), P["leather_dark"][:3], P["leather"][:3], (136, 98, 62))
R_WOOD = rp((52, 32, 18), P["wood_dark"][:3], P["wood"][:3], (158, 110, 66))
R_STEEL = rp((20, 22, 26), P["steel_dark"][:3], P["steel"][:3], P["steel_light"][:3])
R_GRAY = ((70, 70, 70, 255), (110, 110, 110, 255), (165, 165, 165, 255), (225, 225, 225, 255))
R_TIE = rp((36, 30, 18), (50, 42, 26), (66, 56, 34), (82, 70, 44))
R_RIFM = rp((20, 22, 26), P["steel_dark"][:3], P["steel"][:3], P["steel_light"][:3])
R_BLK = rp((10, 10, 12), (22, 22, 26), (34, 34, 38), (60, 60, 66))
R_SHIRT = rp((120, 112, 82), (150, 142, 106), (190, 184, 148), (214, 208, 172))


class Cv:
    def __init__(self, bg):
        self.base = bg
        self.team = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.base)


def paint(cv, m, ramp, dl=3, dd=3, ol=True, team=False):
    """fill mask with ramp: outline (outside), mid, light band (top-left edge), dark band + deep rim (bottom-right)."""
    if ol:
        cv.base.paste(OUTL, mask=SUB(dil(m), m))
    tgt = cv.team if team else cv.base
    if team:
        cv.base.paste((0, 0, 0, 0), mask=m)
    deep, dark, mid, light = ramp
    tgt.paste(mid, mask=m)
    if dl: tgt.paste(light, mask=SUB(m, sh(m, dl, dl, 255)))
    if dd:
        tgt.paste(dark, mask=SUB(m, sh(m, -dd, -dd, 255)))
        tgt.paste(deep, mask=SUB(m, sh(m, -1, -1, 255)))


def tex(cv, m, fn):
    for x, y in bbox_px(m):
        c = fn(x, y)
        if c: cv.base.putpixel((x, y), c)


# ---------------------------------------------------------------- background
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]

def background(tint):
    cols = [(18, 20, 16), (28, 31, 22), (40, 44, 30), (54, 59, 40), (68, 73, 50)]
    cols = [(min(255, c[0] + tint[0]), min(255, c[1] + tint[1]), min(255, c[2] + tint[2]), 255) for c in cols]
    im = Image.new("RGBA", (S, S))
    for y in range(S):
        for x in range(S):
            d = (((x - 78) / 95) ** 2 + ((y - 66) / 100) ** 2) ** 0.5
            v = (1.0 - d) * 4.2 + (BAYER[y % 4][x % 4] - 7.5) / 16
            im.putpixel((x, y), cols[max(0, min(4, int(v + 0.5)))])
    d = ImageDraw.Draw(im)
    for y in range(124, S):
        if (y // 3) % 2 == 0:
            for x in range(0, S, 4):
                if (x + y) % 8 == 0: d.point((x, y), cols[0])
    return im


# ---------------------------------------------------------------- face / head
def eye(d, x, y, w, c):
    d.rectangle([x, y, x + w - 1, y + 2], fill=(232, 226, 214, 255))
    ix = x + 1
    d.rectangle([ix, y, ix + 2, y + 2], fill=(70, 52, 40, 255))
    d.rectangle([ix, y, ix + 1, y + 1], fill=P["eye"])
    d.point((ix + 1, y), fill=(255, 255, 255, 255) if c.get("sparkle") else (160, 150, 140, 255))
    d.line([(x - 1, y - 1), (x + w, y - 1)], fill=P["eye"])
    if c.get("lid"):
        d.line([(x, y), (x + w - 1, y)], fill=c["skin"][1])
        d.line([(x - 1, y - 1), (x + w, y - 1)], fill=P["eye"])
    d.line([(x, y + 3), (x + w - 1, y + 3)], fill=c["skin"][1])


def neck(cv, sk):
    nm = PG([(64, 100), (84, 100), (87, 128), (62, 128)])
    paint(cv, nm, sk, dl=0, dd=4)
    cv.base.paste(sk[0], mask=I(nm, RC(60, 100, 90, 112)))


def blit_head(cv, hc, dx, dy):
    cv.base.alpha_composite(hc.base, (dx, dy))
    cv.team.alpha_composite(hc.team, (dx, dy))
    m = hc.team.split()[3]
    cv.base.paste((0, 0, 0, 0), mask=sh(m, dx, dy))


def face(cv, c):
    d = cv.d
    sk = c["skin"]
    head = U(E(71, 80, 21, 26), E(69, 92, c["jaw"][0], c["jaw"][1]), PG(c["jawpoly"]))
    ear = E(92, 83, 4, 6)
    paint(cv, ear, sk, dl=1, dd=2)
    d.line([(91, 80), (93, 84)], fill=sk[0]); d.point((92, 86), fill=sk[0])
    nl = c["nose"]
    nose = PG([(53, 76), (47 - nl, 88), (51, 91), (60, 89), (60, 76)])
    paint(cv, head, sk, dl=4, dd=5)
    hair = I(U(E(71, c["hair_y"], 23, c["hair_r"]), PG([(86, 68), (95, 70), (94, 84), (89, 79)])), head)
    paint(cv, hair, c["hairc"], dl=2, dd=2, ol=False)
    paint(cv, nose, sk, dl=2, dd=3)
    cv.base.paste(sk[2], mask=I(SUB(dil(nose), nose), sh(head, -1, 0, 0)))   # remove outline inside the face
    d.line([(52, 76), (58, 76)], fill=sk[2])
    d.line([(52, 91), (57, 91)], fill=sk[0])
    for (ex, ey, w) in ((57, 80 + c["eye_dy"], 6), (77, 80 + c["eye_dy"], 7)):
        eye(d, ex, ey, w, c)
    for pts in c["brows"]:
        d.line(pts, fill=c["browc"], width=2)
    lip = (140, 78, 70, 255)
    m = c["mouth"]
    d.line(m, fill=lip, width=1)
    d.line([(x, y + 1) for x, y in m[1:-1]], fill=(186, 116, 100, 255))
    d.line([(m[0][0] - 1, m[0][1] + c["corner"]), m[0]], fill=sk[0])
    d.line([(m[-1][0] + 1, m[-1][1] + c["corner"]), m[-1]], fill=sk[0])
    d.line([(60, 102), (68, 103)], fill=sk[1])
    for ln in c.get("lines", []): d.line(ln, fill=sk[0])
    if c.get("stubble"):
        zone = SUB(I(head, RC(48, 90, 98, 110)), E(65, 97, 8, 3))
        tex(cv, zone, lambda x, y: (96, 70, 56, 255) if (x * x * 3 + y * y * 5 + x * y) % 7 == 0 else None)
    for bx, by in c.get("bags", []):
        d.line([(bx, by), (bx + 5, by)], fill=sk[1])
    for (dx, dy, rx, ry) in c.get("dirt", []):
        mm = I(E(dx, dy, rx, ry), head)
        tex(cv, mm, lambda x, y: (118, 86, 60, 255) if (x + y * 2) % 3 else (96, 68, 48, 255))
    return head


# ---------------------------------------------------------------- helmet
def helmet(cv, hx, hy, net):
    d = cv.d
    dome = I(E(74 + hx, 55 + hy, 37, 30), RC(0, 0, S, 64 + hy))
    hm = U(dome, E(70 + hx, 64 + hy, 40, 8))
    paint(cv, hm, R_HEL, dl=5, dd=6)
    if net:
        nm = I(dome, RC(0, 0, S, 61 + hy))
        def netfn(x, y):
            if (x + y) % 5 == 0 or (x - y) % 5 == 0:
                return (112, 100, 64, 255) if x + y < 128 else (58, 50, 32, 255)
            if (x // 3 + y // 3) % 7 == 0 and (x + y) % 2:
                return (46, 54, 30, 255)
            return None
        tex(cv, nm, netfn)
        d.line([(34 + hx, 62 + hy), (106 + hx, 62 + hy)], fill=P["helmet_dark"])
    bm = M()
    bd = ImageDraw.Draw(bm)
    for x in range(30, 120):
        t = ((x - 70 - hx) / 40.0) ** 2
        if t < 1.0:
            y0 = int(49 + hy + 6 * (1 - t))
            bd.line([(x, y0), (x, y0 + 4)], fill=255)
    bm = I(bm, hm)
    paint(cv, bm, R_GRAY, dl=1, dd=1, team=True)
    return hm


def strap_worn(cv, hy):
    paint(cv, LN([(90, 70 + hy), (89, 92), (78, 105), (62, 103)], 2), R_LEA, dl=0, dd=0)
    cv.d.rectangle([60, 100, 66, 105], fill=P["leather_dark"]); cv.d.point((61, 101), fill=P["steel_light"])


def strap_loose(cv, hy):
    paint(cv, LN([(91, 72 + hy), (95, 96), (94, 104)], 2), R_LEA, dl=0, dd=0)
    cv.d.rectangle([93, 103, 96, 106], fill=P["steel_light"])


def under_brim_shadow(cv, head, hm, deep):
    cv.base.paste(deep, mask=I(SUB(sh(hm, 1, 4), dil(hm)), head))


# ---------------------------------------------------------------- uniform / props
def torso(cv, ramp):
    body = PG([(10, 161), (16, 134), (34, 118), (60, 112), (88, 112), (112, 117), (134, 128), (152, 161)])
    paint(cv, body, ramp, dl=4, dd=5)
    d = cv.d
    for ln in ([(46, 120), (36, 140), (34, 156)], [(104, 122), (112, 136), (116, 154)], [(26, 140), (30, 156)]):
        d.line(ln, fill=ramp[0])
    d.line([(40, 119), (28, 142)], fill=ramp[1])
    return body


def collar(cv, shirt, tie, ramp):
    paint(cv, PG([(58, 108), (86, 108), (72, 138)]), shirt, dl=2, dd=2)
    if tie:
        paint(cv, PG([(69, 114), (75, 114), (77, 136), (72, 141), (67, 136)]), R_TIE, dl=1, dd=1)
    paint(cv, PG([(40, 121), (56, 106), (62, 109), (71, 134), (60, 139)]), ramp, dl=2, dd=3)
    paint(cv, PG([(104, 123), (89, 106), (83, 109), (73, 134), (85, 139)]), ramp, dl=2, dd=3)
    cv.d.line([(72, 142), (72, 160)], fill=ramp[0])


def patch(cv):
    pm = PG([(107, 128), (125, 128), (125, 143), (116, 152), (107, 143)])
    paint(cv, pm, R_GRAY, dl=2, dd=2, team=True)
    cv.team.paste((235, 235, 235, 255), mask=I(RC(114, 133, 118, 144), pm))
    cv.team.paste((90, 90, 90, 255), mask=I(RC(112, 131, 120, 132), pm))


def suspenders(cv):
    for pts in ([(46, 118), (49, 161)], [(102, 120), (97, 161)]):
        paint(cv, LN(pts, 6), R_CAN, dl=1, dd=2)
    for x in (46, 94):
        cv.d.rectangle([x, 141, x + 5, 146], fill=P["steel_light"]); cv.d.rectangle([x + 1, 142, x + 4, 145], fill=P["steel_dark"])
    paint(cv, RC(26, 153, 122, 161), R_CAN, dl=1, dd=1)
    for x in (56, 76):
        paint(cv, RC(x, 148, x + 15, 161), R_CAN, dl=1, dd=2)
        cv.d.line([(x + 2, 152), (x + 13, 152)], fill=R_CAN[0]); cv.d.rectangle([x + 6, 151, x + 9, 154], fill=P["steel_light"])


def sling(cv, pts, w=4):
    paint(cv, LN(pts, w), R_LEA, dl=1, dd=1)
    cv.d.rectangle([pts[0][0] - 3, pts[0][1] - 2, pts[0][0] + 2, pts[0][1] + 3], fill=(180, 150, 70, 255))


# ---------------------------------------------------------------- unit builders
SK_INF = rp((124, 82, 60), P["skin_dark"][:3], P["skin"][:3], P["skin_light"][:3])
SK_SCOUT = rp((112, 74, 54), (138, 94, 68), (180, 130, 96), (208, 160, 122))
SK_WORK = rp((130, 88, 66), (158, 112, 84), (204, 156, 120), (230, 188, 150))


def infantry():
    cv = Cv(background((0, 0, 0)))
    paint(cv, LN([(132, 134), (122, 22)], 5), R_STEEL, dl=1, dd=2)
    paint(cv, LN([(131, 120), (126, 70)], 8), R_WOOD, dl=2, dd=2)
    cv.d.rectangle([120, 16, 124, 20], fill=P["steel_dark"])
    torso(cv, R_OD)
    cfg = dict(skin=SK_INF, jaw=(19, 14), jawpoly=[(52, 90), (88, 90), (84, 104), (56, 104)], nose=1, hair_y=62, hair_r=10,
               hairc=rp((40, 28, 18), (62, 44, 30), (84, 60, 40), (108, 80, 54)), browc=(58, 40, 28, 255), eye_dy=0,
               brows=[[(52, 75), (59, 77), (64, 79)], [(73, 79), (79, 77), (86, 74)]],
               mouth=[(58, 97), (63, 98), (70, 98), (74, 96)], corner=1, lines=[[(52, 93), (54, 98)]])
    neck(cv, SK_INF)
    collar(cv, R_KHA, True, R_OD)
    suspenders(cv)
    sling(cv, [(100, 112), (80, 138), (62, 160)])
    patch(cv)
    hc = Cv(Image.new("RGBA", (S, S), (0, 0, 0, 0)))
    head = face(hc, cfg)
    hm = helmet(hc, 0, -6, True)
    under_brim_shadow(hc, head, hm, SK_INF[0])
    strap_worn(hc, -6)
    blit_head(cv, hc, 4, 4)
    return cv


def scout():
    cv = Cv(background((0, 4, 6)))
    paint(cv, LN([(124, 124), (140, 6)], 2), R_STEEL, dl=0, dd=0)
    cv.d.rectangle([138, 4, 141, 7], fill=P["steel_light"])
    paint(cv, PG([(112, 98), (138, 92), (146, 132), (116, 138)]), R_STEEL, dl=2, dd=3)
    paint(cv, RC(122, 88, 127, 95), R_LEA, dl=0, dd=1)
    cv.d.line([(118, 106), (138, 102)], fill=P["steel_dark"]); cv.d.line([(118, 112), (139, 108)], fill=P["steel_dark"])
    cv.d.rectangle([128, 118, 133, 123], fill=P["od_dark"])
    torso(cv, R_OD)
    cfg = dict(skin=SK_SCOUT, jaw=(18, 15), jawpoly=[(54, 92), (86, 92), (80, 106), (58, 106)], nose=3, hair_y=62, hair_r=10,
               hairc=rp((22, 16, 12), (36, 26, 20), (52, 38, 28), (72, 54, 40)), browc=(32, 22, 16, 255), eye_dy=0,
               brows=[[(52, 74), (57, 71), (63, 72)], [(73, 72), (80, 70), (86, 72)]],
               mouth=[(58, 98), (64, 97), (70, 97), (74, 98)], corner=0, stubble=True, sparkle=True,
               lines=[[(51, 92), (53, 97)], [(55, 85), (56, 88)]])
    neck(cv, SK_SCOUT)
    collar(cv, R_KHA, True, R_OD)
    paint(cv, LN([(36, 118), (70, 144), (108, 160)], 5), R_CAN, dl=1, dd=1)
    cv.d.rectangle([84, 150, 89, 156], fill=P["steel_light"]); cv.d.rectangle([85, 151, 88, 155], fill=P["steel_dark"])
    patch(cv)
    for pts in ([(58, 116), (62, 138)], [(87, 116), (82, 138)]):
        paint(cv, LN(pts, 3), R_LEA, dl=0, dd=1)
    for x in (56, 76):
        paint(cv, RC(x, 138, x + 12, 161), R_STEEL, dl=2, dd=3)
        paint(cv, E(x + 6, 138, 6, 3), R_BLK, dl=1, dd=1)
        cv.d.line([(x + 1, 146), (x + 11, 146)], fill=P["steel_dark"]); cv.d.line([(x + 1, 152), (x + 11, 152)], fill=P["steel_dark"])
    paint(cv, RC(66, 144, 78, 150), R_STEEL, dl=1, dd=1)
    hc = Cv(Image.new("RGBA", (S, S), (0, 0, 0, 0)))
    head = face(hc, cfg)
    hm = helmet(hc, 0, -6, False)
    under_brim_shadow(hc, head, hm, SK_SCOUT[0])
    strap_loose(hc, -6)
    paint(hc, E(92, 84, 4, 6), R_STEEL, dl=1, dd=2)
    blit_head(cv, hc, 4, 4)
    cv.d.line([(100, 92), (103, 106), (100, 122), (94, 128)], fill=P["steel_dark"], width=1)
    return cv


def worker():
    cv = Cv(background((6, 2, 0)))
    paint(cv, LN([(134, 134), (141, 36)], 4), R_WOOD, dl=1, dd=1)
    blade = PG([(133, 40), (149, 34), (154, 10), (142, 4), (130, 12)])
    paint(cv, blade, R_STEEL, dl=2, dd=3)
    tex(cv, I(blade, RC(120, 0, 160, 20)), lambda x, y: (96, 68, 44, 255) if (x * 3 + y * 5) % 7 < 2 else None)
    ramp = R_ODL
    body = torso(cv, ramp)
    paint(cv, PG([(10, 161), (14, 141), (30, 139), (40, 146), (38, 161)]), SK_WORK, dl=3, dd=3)
    paint(cv, PG([(12, 140), (16, 128), (36, 120), (42, 131), (30, 142)]), ramp, dl=2, dd=2)
    cfg = dict(skin=SK_WORK, jaw=(18, 16), jawpoly=[(54, 94), (86, 94), (80, 108), (58, 108)], nose=2, hair_y=62, hair_r=7,
               hairc=rp((70, 46, 24), (96, 66, 34), (124, 88, 46), (150, 112, 64)), browc=(86, 58, 34, 255), eye_dy=1,
               brows=[[(52, 77), (58, 75), (64, 75)], [(73, 75), (80, 74), (86, 76)]], lid=True,
               mouth=[(57, 98), (62, 100), (69, 100), (75, 97)], corner=-1, bags=[(55, 87), (76, 87)],
               lines=[[(52, 94), (53, 99)], [(86, 92), (84, 97)]],
               dirt=[(86, 100, 5, 3), (80, 88, 4, 2), (76, 66, 5, 2), (58, 107, 6, 2)])
    neck(cv, SK_WORK)
    paint(cv, PG([(56, 108), (88, 108), (72, 136)]), R_SHIRT, dl=2, dd=2)
    paint(cv, PG([(42, 122), (56, 106), (62, 109), (70, 132), (60, 138)]), ramp, dl=2, dd=3)
    paint(cv, PG([(102, 124), (89, 106), (83, 109), (74, 132), (85, 138)]), ramp, dl=2, dd=3)
    glove = U(PG([(94, 128), (98, 118), (101, 128)]), PG([(100, 128), (104, 119), (107, 129)]), RC(94, 126, 108, 134))
    paint(cv, glove, R_LEA, dl=1, dd=1)
    paint(cv, RC(88, 130, 112, 150), ramp, dl=1, dd=2)
    cv.d.line([(88, 138), (111, 138)], fill=ramp[0]); cv.d.rectangle([98, 139, 101, 142], fill=P["steel_light"])
    tex(cv, I(U(E(52, 148, 10, 5), E(116, 156, 8, 4)), body), lambda x, y: (70, 62, 38, 255) if (x + y) % 3 else (58, 50, 30, 255))
    patch(cv)
    hc = Cv(Image.new("RGBA", (S, S), (0, 0, 0, 0)))
    head = face(hc, cfg)
    hm = helmet(hc, 8, -10, False)
    under_brim_shadow(hc, head, hm, SK_WORK[0])
    strap_loose(hc, -10)
    blit_head(cv, hc, 4, 4)
    return cv


R_CAP = rp((60, 60, 44), (84, 84, 62), (112, 110, 80), (138, 136, 100))
R_CAPF = rp((52, 52, 38), (72, 72, 52), (96, 94, 68), (120, 118, 86))


def pilotka(cv, hy):
    """low soft side cap (pilotka): wedge/boat crown sitting low and slightly tilted, folded-up edge carries the team band, front badge, no visor."""
    crown = PG([(52, 56), (54, 47), (62, 41), (74, 39), (86, 42), (92, 49), (93, 55)])
    paint(cv, crown, R_CAP, dl=3, dd=4)
    cv.d.line([(60, 55), (66, 43), (76, 40)], fill=R_CAP[1])                    # soft fold line across the crown
    fold = PG([(50, 57), (93, 53), (95, 61), (51, 65)])
    paint(cv, fold, R_GRAY, dl=1, dd=1, team=True)
    cv.d.rectangle([59, 47, 64, 51], fill=P["steel_light"])                      # small badge at the front of the fold
    cv.d.point((61, 49), fill=(200, 40, 40, 255))
    return U(crown, fold)


def rifleman(cfg, bg, net=False, cap=False, prop=None, hy=-6):
    cv = Cv(background(bg))
    if prop:
        prop(cv)
    else:
        paint(cv, LN([(132, 134), (122, 22)], 5), R_RIFM, dl=1, dd=2)
        paint(cv, LN([(131, 120), (126, 70)], 8), R_WOOD, dl=2, dd=2)
        cv.d.rectangle([120, 16, 124, 20], fill=P["steel_dark"])
    torso(cv, R_OD)
    neck(cv, cfg["skin"])
    collar(cv, R_KHA, True, R_OD)
    suspenders(cv)
    if not prop:
        sling(cv, [(100, 112), (80, 138), (62, 160)])
    patch(cv)
    hc = Cv(Image.new("RGBA", (S, S), (0, 0, 0, 0)))
    head = face(hc, cfg)
    if cap:
        hm = pilotka(hc, hy)
        under_brim_shadow(hc, head, hm, cfg["skin"][0])
    else:
        hm = helmet(hc, 0, hy, net)
        under_brim_shadow(hc, head, hm, cfg["skin"][0])
        strap_worn(hc, hy)
    blit_head(cv, hc, 4, 4)
    return cv


HAIR_B = rp((40, 28, 18), (62, 44, 30), (84, 60, 40), (108, 80, 54))


def infantry():
    cfg = dict(skin=SK_INF, jaw=(19, 14), jawpoly=[(52, 90), (88, 90), (84, 104), (56, 104)], nose=1, hair_y=62, hair_r=10,
               hairc=HAIR_B, browc=(58, 40, 28, 255), eye_dy=0,
               brows=[[(52, 75), (59, 77), (64, 79)], [(73, 79), (79, 77), (86, 74)]],
               mouth=[(58, 97), (63, 98), (70, 98), (74, 96)], corner=1, lines=[[(52, 93), (54, 98)]])
    return rifleman(cfg, (0, 0, 0), net=False)


def recruit():
    cfg = dict(skin=rp((130, 88, 64), (160, 112, 84), (206, 158, 122), (232, 190, 152)), jaw=(18, 13),
               jawpoly=[(53, 90), (87, 90), (83, 103), (57, 103)], nose=0, hair_y=62, hair_r=12,
               hairc=rp((92, 66, 36), (120, 88, 50), (150, 112, 66), (178, 140, 86)), browc=(110, 78, 44, 255), eye_dy=0, sparkle=True,
               brows=[[(52, 74), (58, 74), (64, 75)], [(73, 75), (79, 74), (86, 73)]],
               mouth=[(59, 97), (64, 98), (70, 98), (74, 96)], corner=0)
    return rifleman(cfg, (4, 2, 0), cap=True)


def veteran():
    cfg = dict(skin=SK_INF, jaw=(20, 15), jawpoly=[(51, 90), (89, 90), (85, 106), (55, 106)], nose=2, hair_y=62, hair_r=9,
               hairc=rp((34, 26, 20), (56, 44, 34), (78, 62, 48), (104, 88, 70)), browc=(40, 30, 22, 255), eye_dy=1, lid=True,
               brows=[[(51, 76), (58, 75), (64, 78)], [(73, 78), (79, 75), (87, 73)]],
               mouth=[(58, 99), (63, 99), (70, 98), (75, 96)], corner=1, stubble=True,
               bags=[(54, 87), (76, 87)],
               lines=[[(51, 92), (53, 99)], [(86, 91), (84, 97)], [(60, 74), (62, 70)], [(80, 76), (84, 74)]],
               dirt=[(84, 98, 5, 3), (60, 106, 5, 2), (78, 64, 5, 2)])
    cv = rifleman(cfg, (0, 3, 0), net=True)
    # scar across the cheek
    cv.d.line([(84, 84), (90, 96)], fill=(176, 108, 90, 255))
    return cv


def at_prop(cv):
    """bazooka shouldered on the right: tube, flared muzzle, wooden grip."""
    paint(cv, LN([(118, 150), (131, 28)], 13), R_OD, dl=2, dd=3)
    paint(cv, E(133, 22, 11, 5), R_OD, dl=2, dd=2)
    cv.d.ellipse([127, 19, 139, 25], fill=(14, 14, 12, 255))
    paint(cv, LN([(120, 130), (112, 142)], 4), R_WOOD, dl=1, dd=1)
    paint(cv, RC(124, 66, 129, 76), R_STEEL, dl=1, dd=1)
    paint(cv, E(118, 150, 9, 4), R_OD, dl=1, dd=1)
    cv.d.line([(122, 90), (126, 90)], fill=P["steel_light"])


def at_infantry():
    cfg = dict(skin=SK_INF, jaw=(20, 14), jawpoly=[(51, 90), (89, 90), (85, 105), (55, 105)], nose=1, hair_y=62, hair_r=10,
               hairc=rp((50, 36, 22), (72, 52, 34), (96, 70, 46), (122, 92, 62)), browc=(46, 32, 22, 255), eye_dy=0,
               brows=[[(51, 73), (58, 75), (64, 78)], [(73, 78), (79, 75), (87, 72)]],
               mouth=[(58, 98), (64, 98), (70, 98), (75, 96)], corner=0, lines=[[(52, 92), (53, 97)], [(86, 92), (85, 96)]])
    return rifleman(cfg, (2, 2, 0), net=True, prop=at_prop)


def finish(name, cv):
    big = cv.base.resize((PORTRAIT, PORTRAIT), Image.NEAREST)
    team = cv.team.resize((PORTRAIT, PORTRAIT), Image.NEAREST)
    save_portrait(name, big, team)


if __name__ == "__main__":
    finish("infantry", infantry())
    finish("recruit", recruit())
    finish("veteran", veteran())
    finish("at_infantry", at_infantry())
    finish("scout", scout())
    finish("worker", worker())
    print("portraits done")
