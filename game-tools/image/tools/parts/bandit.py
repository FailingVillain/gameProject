"""Bandit (마적, honghuzi, key `bandit`) kit parts: irregular padded cotton jackets (navy / black / brown / dark green), sheepskin
and fur caps, felt hat, cloth leg wraps. No insignia, no brass. Registered via unit_loader.py."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import voxel as V  # noqa: E402
from voxel import ell, cyl, tr, tband  # noqa: E402


def _mat(name, anchors):
    if name not in V.MATERIALS:
        V.MATERIALS[name] = V._ramp8([a + (255,) for a in anchors])
        V.MAT_NAMES.append(name)
        V.MAT_IDX[name] = len(V.MAT_NAMES) - 1
    return name


NAVY = _mat("bd_navy", [(18, 26, 48), (28, 40, 70), (42, 58, 94), (58, 76, 116)])
BLACK = _mat("bd_black", [(16, 16, 18), (26, 26, 29), (38, 38, 42), (52, 52, 57)])
BROWN = _mat("bd_brown", [(40, 28, 18), (62, 44, 28), (88, 64, 42), (112, 86, 58)])
GREEN = _mat("bd_green", [(24, 32, 22), (38, 50, 34), (54, 70, 48), (72, 90, 62)])
WRAP = _mat("bd_wrap", [(70, 62, 48), (100, 90, 70), (128, 116, 92), (152, 140, 114)])        # cloth leg wraps / sash
FELT = _mat("bd_felt", [(52, 46, 40), (78, 70, 60), (104, 94, 80), (126, 116, 100)])          # grey-brown felt hat
SHEEP = _mat("bd_sheep", [(110, 96, 74), (150, 134, 106), (186, 168, 138), (212, 196, 166)])  # sheepskin, pale
TAN = _mat("bd_tan", [(78, 60, 38), (112, 88, 58), (144, 116, 80), (170, 142, 104)])             # jacket: tan-brown
CHOC = _mat("bd_choc", [(30, 20, 13), (46, 32, 21), (64, 46, 31), (82, 62, 44)])                 # trousers / leader coat: dark chocolate
FURB = _mat("bd_furbrown", [(54, 38, 24), (84, 60, 38), (116, 86, 56), (142, 108, 74)])          # shared fur hat: warm brown fur
FURD = _mat("bd_furdark", [(30, 22, 16), (50, 38, 28), (72, 56, 42), (92, 74, 56)])           # dark fur (leader)


def _fur_cap(Hm, fur, cloth, band):
    p = [ell(Hm @ tr(0, 6.7, 0.3), 5.9, 2.7, 5.9, cloth, clipy=-0.2),
         cyl(Hm @ tr(0, 5.9, 0.3), 6.5, 1.5, fur),
         cyl(Hm @ tr(0, 4.25, 0.3), 6.55, 0.45, tband(fur) if band else fur)]
    for s in (1, -1):
        p.append(ell(Hm @ tr(s * 6.1, 2.8, 0.0), 1.9, 3.4, 3.4, fur))
    p.append(ell(Hm @ tr(0, 3.2, -5.9), 4.6, 2.2, 1.6, fur))
    return p


def bandit_fur_cap(Hm, band=True):
    """All bandit rank-and-file: brown fur cap with ear flaps, dark-brown cloth crown."""
    return _fur_cap(Hm, FURB, CHOC, band)


def sheepskin_cap(Hm, band=True):
    """Rifleman: pale sheepskin cap with ear flaps, black-cloth crown."""
    return _fur_cap(Hm, SHEEP, BLACK, band)


def fur_hat_tall(Hm, band=True):
    """Leader: tall dark-fur hat (fuller crown, no flaps)."""
    return [cyl(Hm @ tr(0, 5.6, 0.3), 6.3, 2.2, FURD),
            ell(Hm @ tr(0, 8.2, 0.3), 5.6, 3.0, 5.7, FURD),
            cyl(Hm @ tr(0, 4.35, 0.3), 6.4, 0.45, tband(FURD) if band else FURD)]


def felt_hat(Hm, band=True):
    """Thug / horseman: soft round felt hat with a short brim and a cloth band (team strip)."""
    return [ell(Hm @ tr(0, 6.4, 0.3), 5.6, 3.0, 5.8, FELT),
            ell(Hm @ tr(0, 5.0, 0.4), 7.4, 0.55, 7.6, FELT),
            cyl(Hm @ tr(0, 5.5, 0.3), 5.9, 0.5, tband(FELT) if band else FELT)]


DEEL = _mat("mg_deel", [(20, 44, 78), (30, 66, 112), (44, 90, 146), (62, 112, 170)])        # Mongol deel robe: deep blue
SASH = _mat("mg_sash", [(150, 90, 10), (200, 130, 20), (236, 172, 40), (252, 208, 80)])     # bright yellow-orange sash
MHAT = _mat("mg_hat", [(40, 30, 70), (62, 48, 104), (86, 68, 136), (110, 90, 160)])         # hat: purple-blue silk
CLOTH = _mat("bd_cloth", [(150, 144, 130), (190, 184, 168), (222, 216, 200), (240, 235, 220)])  # dirty-white head towel


def mongol_hat(Hm, band=True):
    """Mongol pointed hat (loovuuz): conical crown, turned-up fur brim, button on top."""
    p = [cyl(Hm @ tr(0, 4.6, 0.3), 6.7, 1.3, FURD),
         cyl(Hm @ tr(0, 5.9, 0.3), 5.4, 1.5, MHAT),
         cyl(Hm @ tr(0, 7.4, 0.3), 4.0, 1.5, MHAT),
         cyl(Hm @ tr(0, 8.9, 0.3), 2.6, 1.5, MHAT),
         ell(Hm @ tr(0, 10.8, 0.3), 1.4, 1.6, 1.4, SASH)]
    if band:
        p.append(cyl(Hm @ tr(0, 4.25, 0.3), 6.75, 0.45, tband(FURD)))
    return p


def head_towel(Hm, band=True):
    """Bandit: light cloth tied round the head, knot at the back, no brim."""
    p = [ell(Hm @ tr(0, 6.2, 0.3), 5.9, 3.0, 6.0, CLOTH),
         cyl(Hm @ tr(0, 4.6, 0.3), 6.2, 0.9, tband(CLOTH) if band else CLOTH),
         ell(Hm @ tr(0, 4.2, 6.2), 1.8, 2.4, 1.8, CLOTH)]
    for s in (1, -1):
        p.append(ell(Hm @ tr(s * 0.8, 2.6, 6.6), 0.9, 2.6, 0.9, CLOTH))
    return p


HEADGEAR = {"mongol_hat": dict(fn=mongol_hat, net=False), "bandit_towel": dict(fn=head_towel, net=False),
            "bandit_fur": dict(fn=bandit_fur_cap, net=False),"bandit_sheepskin": dict(fn=sheepskin_cap, net=False), "bandit_fur_hat": dict(fn=fur_hat_tall, net=False),
            "bandit_felt": dict(fn=felt_hat, net=False)}


def _u(color, leg=WRAP, **kw):
    return dict(jacket=color, trousers=color, legging=leg, boot="leather", belt="canvas", hand="skin", collar=False, puttees=True,
                puttee_seam=BROWN, **kw)


UNIFORMS = {"mongol_deel": dict(jacket=DEEL, trousers=DEEL, legging="leather", boot="leather", belt=SASH, hand="skin", collar=False,
                                puttees=False),
            "bandit_tan": dict(jacket=TAN, trousers=CHOC, legging=WRAP, boot="leather", belt="canvas", hand="skin", collar=False,
                               puttees=True, puttee_seam=BROWN),
            "bandit_navy": _u(NAVY), "bandit_black": _u(BLACK), "bandit_brown": _u(BROWN), "bandit_green": _u(GREEN),
            "bandit_leader": dict(jacket=CHOC, trousers=BROWN, legging="leather", boot="leather", belt="leather", hand="skin")}
