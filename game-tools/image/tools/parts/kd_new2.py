"""Korean Division batch (newunits N68/N71): Manchurian hunter (fur cap + fur collar), village militia (guapi cap, undyed cotton), shotgun.
Registered in unit_loader.py. Built only from existing primitives; fixed colours."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, ell, cyl, rbox, along_z, tr  # noqa: E402
from parts.bandit import _mat, FURB, FURD, WRAP, CHOC, BROWN, SHEEP, _fur_cap  # noqa: E402
from parts.fengtian import FENGTIAN_BG  # noqa: E402
from parts.kd_new import CIV_W, CIV_G, FL_BLACK, FL_RED  # noqa: E402
from parts.rifles import bolt_rifle_prims, _flash, _B  # noqa: E402
import tune  # noqa: E402

HUNT_J = _mat("kd_hunt_coat", [(44, 42, 26), (68, 64, 40), (96, 90, 58), (122, 114, 76)])    # moss-brown hide / wool hunting coat
HUNT_C = _mat("kd_hunt_crown", [(34, 44, 28), (52, 66, 42), (72, 90, 58), (92, 112, 74)])    # forest-green cap crown


# ---------------------------------------------------------------- headgear
def hunter_cap(Hm, band=True):
    """Hunter: brown fur cap with ear flaps, forest-green crown."""
    return _fur_cap(Hm, FURB, FURB, band)


def guapi_cap(Hm, band=True):
    """Village militia: black six-panel skullcap (guapi), small red button on top, thin band."""
    import math
    p = [ell(Hm @ tr(0, 6.0, 0.3), 5.5, 3.1, 5.6, FL_BLACK),
         cyl(Hm @ tr(0, 4.5, 0.3), 5.75, 0.7, FL_BLACK)]
    for i in range(6):                                           # panel seams
        a = math.radians(i * 60.0)
        p.append(ell(Hm @ tr(4.4 * math.sin(a), 6.5, 0.3 + 4.4 * math.cos(a)), 0.45, 2.4, 0.45, FL_BLACK))
    p.append(ell(Hm @ tr(0, 9.0, 0.3), 1.1, 1.1, 1.1, FL_RED))   # button
    if band:
        p.append(cyl(Hm @ tr(0, 4.3, 0.3), 5.9, 0.4, "leather"))
    return p


HEADGEAR = {"hunter_cap": dict(fn=hunter_cap, net=False), "guapi_cap": dict(fn=guapi_cap, net=False)}

UNIFORMS = {"hunter": dict(jacket=HUNT_J, trousers=CHOC, legging=WRAP, boot="leather", belt="leather", hand="skin", collar=False,
                           puttees=True, puttee_seam=BROWN),
            "draft_bandit": dict(jacket=FENGTIAN_BG, trousers=CHOC, legging=WRAP, boot="leather", belt="canvas", hand="skin", collar=False,
                                 puttees=True, puttee_seam=BROWN),
            "militia": dict(jacket=CIV_W, trousers=CIV_G, legging=CIV_G, boot="leather", belt="canvas", hand="skin", collar=False,
                            puttees=True, puttee_seam=CIV_G)}


# ---------------------------------------------------------------- gear (torso frame)
def fur_collar(T):
    """Hunter: thick fur collar across the shoulders."""
    return [ell(T @ tr(0, 12.0, 0.2), 8.0, 2.4, 6.0, SHEEP)]


def game_pouch(T):
    """Hunter: leather game bag on the left hip rear + powder horn."""
    return [rbox(T @ tr(7.0, -1.0, -3.4), 2.4, 3.0, 1.6, "leather", r=1.0),
            ell(T @ tr(-7.2, 0.4, -2.6), 0.9, 2.4, 0.9, "wood")]


GEAR = {"fur_collar": fur_collar, "game_pouch": game_pouch}


# ---------------------------------------------------------------- double-barrel shotgun (weapon frame: z = muzzle)
SG_Z1 = 17.0


def shotgun_prims(M):
    base = bolt_rifle_prims(M)
    keep = [base[i] for i in (0, 1, 2, 3, 11)]                    # butt stock, plate, wrist, receiver, trigger guard
    r = 0.8
    return keep + [rbox(M @ tr(0, -0.4, 8.0), 1.7, 1.3, 4.5, "wood", r=0.4),                # short fore-end
                   along_z(M @ tr(0.85, 0.4, 0), r, 2.0, SG_Z1, "steel"),
                   along_z(M @ tr(-0.85, 0.4, 0), r, 2.0, SG_Z1, "steel"),
                   rbox(M @ tr(0, 1.2, 9.0), 0.3, 0.3, 8.0, "steel", r=0.1),                 # rib
                   ell(M @ tr(0, 1.6, SG_Z1 - 0.6), 0.35, 0.35, 0.35, "brass")]              # bead sight


SHOTGUN = Weapon(shotgun_prims, gripR=_B["grip_right"], gripL=(0, -1.9, 8.0),
                 overlay=lambda M, kind: _flash(M, SG_Z1 + 0.6, y=0.5) if kind == "flash" else [], extras={})
WEAPONS = {"shotgun": SHOTGUN}
