"""Fengtian (봉천) infantry kit parts: light blue-grey cloth ramp + simple round field cap. Registered via unit_loader.py."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import voxel as V  # noqa: E402
from voxel import ell, cyl, rbox, tr, rx, tband  # noqa: E402

FENGTIAN_BG = "fengtian_bg"
FENGTIAN_ANCHORS = [(41, 54, 73), (61, 76, 100), (83, 100, 127), (107, 124, 151)]     # light blue-grey cloth, dark -> light (bluer, top steps darker than v1)
if FENGTIAN_BG not in V.MATERIALS:
    V.MATERIALS[FENGTIAN_BG] = V._ramp8([a + (255,) for a in FENGTIAN_ANCHORS])
    V.MAT_NAMES.append(FENGTIAN_BG)
    V.MAT_IDX[FENGTIAN_BG] = len(V.MAT_NAMES) - 1
FUR = "ft_fur"
if FUR not in V.MATERIALS:
    V.MATERIALS[FUR] = V._ramp8([a + (255,) for a in [(70, 54, 40), (104, 84, 62), (140, 116, 88), (172, 148, 116)]])
    V.MAT_NAMES.append(FUR)
    V.MAT_IDX[FUR] = len(V.MAT_NAMES) - 1

# cap geometry (head frame: origin at the neck, y up, z forward)
CAP_BAND = dict(y=5.4, r=5.8, hl=0.85)                 # thin cloth band (team colour)
CAP_DOME = dict(y=5.9, rx=5.9, ry=2.6, rz=6.1)         # low round crown (top y ~ 8.5; Feldkappe top ~ 9.5)
CAP_VISOR = dict(y=5.0, z=6.3, hx=2.3, hy=0.3, hz=1.0, pitch=12)   # tiny soft visor


def fengtian_cap(Hm, band=True):
    """Manchurian winter fur cap: round cloth crown, thick fur rim (team-colour strip at its lower edge), fur ear flaps hanging
    at the sides, small fur nape flap. Generic (no badge); badge anchor (y 7.3, z ~6.1) sits on the cloth crown front."""
    cloth = FENGTIAN_BG
    p = [ell(Hm @ tr(0, 6.7, 0.3), 5.9, 2.7, 5.9, cloth, clipy=-0.2),                       # cloth crown
         cyl(Hm @ tr(0, 5.9, 0.3), 6.5, 1.5, FUR),                                           # fur rim
         cyl(Hm @ tr(0, 4.25, 0.3), 6.55, 0.45, tband(FUR) if band else FUR)]                    # team strip, lower edge
    for s in (1, -1):
        p.append(ell(Hm @ tr(s * 6.1, 2.8, 0.0), 1.9, 3.4, 3.4, FUR))                        # ear flaps (down)
    p.append(ell(Hm @ tr(0, 3.2, -5.9), 4.6, 2.2, 1.6, FUR))                                 # nape flap
    return p


def fengtian_officer_cap(Hm, band=True):
    """officer_cap shape (parts/headgear.py) with the Fengtian cloth colour instead of field grey: identical geometry."""
    cap = FENGTIAN_BG
    return [ell(Hm @ tr(0, 5.9, 0.6), 6.4, 1.6, 6.8, tband(cap) if band else cap),
            ell(Hm @ tr(0, 7.9, 0.4), 6.3, 2.4, 6.7, cap),
            ell(Hm @ tr(0, 9.7, 0.0), 8.2, 1.3, 8.6, cap),
            ell(Hm @ tr(0, 5.2, 7.2), 5.0, 0.6, 3.2, cap),
            ell(Hm @ tr(0, 7.9, 7.0), 0.9, 1.0, 0.5, "bright")]


HEADGEAR = {"fengtian_cap": dict(fn=fengtian_cap, net=False), "fengtian_officer_cap": dict(fn=fengtian_officer_cap, net=False)}
UNIFORMS = {"fengtian": dict(jacket=FENGTIAN_BG, trousers=FENGTIAN_BG, legging="puttee", boot="leather", belt="leather", hand="skin",
                             collar=True, puttees=True)}
UNIFORMS["fengtian_officer"] = dict(jacket=FENGTIAN_BG, trousers=FENGTIAN_BG, legging=FENGTIAN_BG, boot="leather", belt="leather",
                                    hand="skin", trouser_stripe=True)       # us_officer with the Fengtian cloth colour
