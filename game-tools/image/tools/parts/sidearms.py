"""Sidearms: pistol (voxel.Weapon) + holster gear. Weapon frame: origin at the grip top / rear of the slide, z = muzzle."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, tr, rx  # noqa: E402
import tune  # noqa: E402
from parts._t import boxp  # noqa: E402


def pistol_prims(M):
    """M1911-style service pistol."""
    W = tune.get("parts", "weapons", "pistol")
    return [boxp(M, dict(W["slide"], round=0.3), "steel"),                               # slide
            boxp(M, dict(W["frame"], round=0.2), "steel"),                               # frame
            boxp(M, dict(W["grip"], round=0.3), "rifmetal"),                             # grip (raked back)
            boxp(M, dict(W["trigger_guard"], round=0.2), "steel")]                       # trigger guard


def pistol_overlay(M, kind):
    if kind != "flash":
        return []
    z = tune.get("parts", "weapons", "pistol")["flash_z"]
    return [ell(M @ tr(0, 0.6, z + 1.6), 2.4, 2.4, 3.0, "flash_edge"),
            ell(M @ tr(0, 0.6, z + 1.5), 1.7, 1.7, 2.3, "flash"),
            ell(M @ tr(0, 0.6, z + 1.2), 1.0, 1.0, 1.4, "flash_core")]


PISTOL = Weapon(pistol_prims, gripR=(0, -1.9, -0.9), gripL=(0, -1.9, -0.9), overlay=pistol_overlay)


def holster(T):
    """closed leather flap holster on the right hip (torso frame)."""
    G = tune.get("parts", "gear", "holster")
    return [boxp(T, G["body"], "leather"),
            boxp(T, G["flap"], "leather"),                                                                  # flap
            boxp(T, G["stud"], "steel")]                                                                    # stud


WEAPONS = {"pistol": PISTOL}
GEAR = {"holster": holster}
