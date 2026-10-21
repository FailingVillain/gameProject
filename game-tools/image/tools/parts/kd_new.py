"""Korean Division batch (n63/n68/n72): papakha, volunteer cap + civilian clothes, regimental flag poles (national colours), tachanka Maxim.
Registered in unit_loader.py (HEADGEAR / UNIFORMS / WEAPONS). Built only from existing primitives; fixed colours (not team)."""
import os
import sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, ell, cyl, rbox, seg, along_z, box, tr, rz, ry, tband  # noqa: E402
from parts.bandit import _mat, FURD, FURB, BROWN  # noqa: E402

CIV_W = _mat("kd_civ_white", [(120, 118, 108), (162, 160, 148), (200, 198, 186), (228, 226, 214)])   # undyed cotton
CIV_G = _mat("kd_civ_grey", [(84, 84, 80), (116, 116, 110), (146, 146, 138), (172, 172, 164)])        # grey cotton trousers / wraps
FL_WHITE = _mat("kd_fl_white", [(170, 170, 164), (206, 206, 200), (232, 232, 226), (248, 248, 244)])
FL_RED = _mat("kd_fl_red", [(110, 16, 20), (150, 22, 28), (184, 34, 40), (208, 52, 56)])
FL_BLUE = _mat("kd_fl_blue", [(14, 38, 98), (22, 54, 130), (34, 72, 160), (48, 92, 186)])
FL_YEL = _mat("kd_fl_yellow", [(150, 112, 14), (192, 148, 20), (222, 178, 36), (240, 202, 66)])
FUR_BLK = _mat("kd_fur_black", [(6, 6, 8), (14, 14, 16), (24, 24, 26), (36, 36, 38)])   # black papakha fur
FUR_TIP = _mat("kd_fur_tip", [(18, 18, 20), (28, 28, 30), (38, 38, 40), (48, 48, 50)])   # near-black fur tufts (texture, no grey 2026-10-21)
GD_KHA = _mat("kd_gando_khaki", [(66, 58, 40), (94, 85, 60), (120, 110, 80), (144, 133, 100)])   # dyed grey-brown cotton jacket / cap
GD_DRK = _mat("kd_gando_dark", [(30, 30, 32), (44, 44, 46), (62, 62, 62), (80, 80, 78)])         # dark trousers
GD_WRP = _mat("kd_gando_wrap", [(54, 50, 38), (78, 72, 54), (100, 94, 72), (120, 112, 88)])      # brown-khaki puttees
FL_BLACK = _mat("kd_fl_black", [(10, 10, 12), (20, 20, 24), (30, 30, 34), (44, 44, 48)])


# ---------------------------------------------------------------- headgear
def papakha(Hm, band=True):
    """Cossack papakha: tall (user 2026-10-21) dark fur hat, flat top, thin team band at the base; fur texture = ring of tufts + crown clumps."""
    import math
    p = [cyl(Hm @ tr(0, 8.2, 0.3), 6.2, 4.4, FUR_BLK),
         ell(Hm @ tr(0, 12.7, 0.3), 6.0, 1.2, 6.0, FUR_BLK),
         cyl(Hm @ tr(0, 4.35, 0.3), 6.3, 0.45, tband(FUR_BLK) if band else FUR_BLK)]
    for row, (y, n, ph) in enumerate(((6.0, 10, 0.0), (8.6, 10, 18.0), (11.2, 9, 5.0))):
        for i in range(n):
            a = math.radians(ph + i * 360.0 / n)
            r = 6.35 if row != 1 else 6.45
            m = FUR_TIP if (i + row) % 2 == 0 else FUR_BLK
            p.append(ell(Hm @ tr(r * math.sin(a), y + (0.5 if i % 3 == 0 else 0.0), 0.3 + r * math.cos(a)), 1.45, 1.25, 1.45, m))
    for i in range(6):                       # tufts on top
        a = math.radians(i * 60.0 + 10)
        p.append(ell(Hm @ tr(3.4 * math.sin(a), 13.3, 0.3 + 3.4 * math.cos(a)), 1.7, 0.8, 1.7, FUR_TIP if i % 2 else FUR_BLK))
    p.append(ell(Hm @ tr(0, 13.4, 0.3), 2.4, 0.8, 2.4, FUR_TIP))
    return p


def gando_cap(Hm, band=True):
    """Volunteer: khaki military-style soft cap with a headband (team strip) and short peak."""
    return [ell(Hm @ tr(0, 6.3, 0.3), 5.6, 2.8, 5.8, GD_KHA),
            cyl(Hm @ tr(0, 4.7, 0.3), 5.95, 0.6, tband(GD_KHA) if band else GD_KHA),
            ell(Hm @ tr(0, 4.4, 5.6), 4.0, 0.5, 2.0, "leather")]       # short peak


HEADGEAR = {"papakha": dict(fn=papakha, net=False), "gando_cap": dict(fn=gando_cap, net=False)}

UNIFORMS = {"gando": dict(jacket=GD_KHA, trousers=GD_DRK, legging=GD_WRP, boot="leather", belt="canvas", hand="skin", collar=False,
                          puttees=True, puttee_seam=GD_WRP)}


# ---------------------------------------------------------------- regimental flag on a pole (weapon along +z)
def _flag(M, kind):
    p = [along_z(M, 0.5, -2.0, 33.0, "wood"), ell(M @ tr(0, 0, 33.4), 0.85, 0.85, 0.85, "brass")]
    Mf = M @ tr(0, 0, 21.0) @ rz(40) @ tr(0, 0.5, 0)            # flag plane contains the pole; tilted 40 deg so it reads from s and e
    HZ = 10.5               # height (along z), unchanged
    HY = 1.5 * HZ           # half-width along the fly: full width = 2*HY = 1.5 x height (3:2, user 2026-10-21)
    Mc = Mf @ tr(0, HY, 0)

    def cloth(mat, hz=HZ, dz=0.0, hy=HY, dy=0.0):
        return rbox(Mc @ tr(0, dy, dz), 0.3, hy, hz, mat, r=0.15)

    def disc(c, r, mat, dz=0.0, dy=0.0):
        return [cyl(Mc @ tr(sx * 0.4, dy, dz) @ rz(90), r, 0.2, mat) for sx in (1, -1)]

    if kind == "korean":                      # white field, red/blue centre disc (halves), black corner bars
        p += [cloth(FL_WHITE)]
        p += disc(0, 3.0, FL_RED, dz=1.5) + disc(0, 3.0, FL_BLUE, dz=-1.5)
        for sy in (1, -1):
            for sz in (1, -1):
                for sx in (1, -1):
                    p.append(box(Mc @ tr(sx * 0.4, sy * 6.3 * HY / 9.3, sz * 7.5), 0.15, 0.6, 1.8, FL_BLACK))   # corner bars keep their 9.3 layout fraction
    elif kind == "japanese":                  # 일장기: plain white field, one centred red disc, diameter 3/5 of flag height (height = 2*HZ along z)
        R = 0.6 * HZ
        p += [cloth(FL_WHITE)]
        p += [cyl(Mc @ tr(sx * 0.25, 0, 0) @ rz(90), R, 0.15, FL_RED) for sx in (1, -1)]    # one disc per face, embedded in the cloth
    elif kind == "fengtian":                  # five-colour flag: red / yellow / blue / white / black bands
        cols = [FL_RED, FL_YEL, FL_BLUE, FL_WHITE, FL_BLACK]
        h = HZ / 5
        p += [cloth(m, hz=h, dz=HZ - h - i * 2 * h) for i, m in enumerate(cols)]
    elif kind == "russian-white":             # white-blue-red tricolour
        h = HZ / 3
        p += [cloth(m, hz=h, dz=HZ - h - i * 2 * h) for i, m in enumerate([FL_WHITE, FL_BLUE, FL_RED])]
    else:                                     # russian-red
        p += [cloth(FL_RED)]
    return p


def _fw(kind):
    return Weapon(lambda M: _flag(M, kind), gripR=(0, -0.3, -4.0), gripL=(0, -0.3, 9.5))


WEAPONS = {"flag_" + k: _fw(k) for k in ("korean", "japanese", "fengtian", "russian-white", "russian-red")}


# ---------------------------------------------------------------- tachanka maxim (gun + shield + pintle, frame: +z = muzzle)
def maxim_mount(G, flash=0.0):
    """Maxim on a pintle post. G = gun frame at the cradle (origin = receiver axis). Returns prims."""
    p = [box(G @ tr(0, 0, -5.0), 2.5, 3.0, 5.5, "steel"),
         box(G @ tr(0, 0.3, -10.8), 2.2, 2.6, 0.5, "rifmetal"),
         box(G @ tr(0, 3.3, -4.0), 0.5, 0.6, 0.6, "steel"),
         box(G @ tr(3.3, 0.6, -3.5), 1.3, 1.6, 2.4, "rifmetal"),
         along_z(G, 2.5, 0.5, 19.0, "steel"),
         along_z(G, 2.7, 18.4, 19.4, "rifmetal"),
         along_z(G, 1.6, 19.4, 23.0, "rifmetal"),
         along_z(G, 0.9, 23.0, 26.0, "steel"),
         box(G @ tr(0, 0, 25.6), 0.4, 1.4, 0.4, "steel"),
         cyl(G @ tr(0, 2.8, 15.0), 1.1, 0.5, "rifmetal")]
    for s in (1, -1):
        p.append(seg((G @ np.array([s * 1.8, 1.0, -11.1, 1.0]))[:3], (G @ np.array([s * 2.4, 3.6, -12.8, 1.0]))[:3], 0.75, "wood"))
    p.append(box(G @ tr(0, 3.7, -13.9), 2.2, 0.4, 0.4, "steel"))
    p.append(box(G @ tr(0, -3.4, 1.5), 3.0, 0.8, 4.0, "olive"))                                                # cradle
    return p
