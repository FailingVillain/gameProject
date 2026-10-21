"""Hand grenade with a long wooden handle (WWI stick type, generic: not tied to any one army's marking).
Weapon frame: z along the handle, head at +z, origin = centre of the grip. Held in the right hand (gripR = gripL = handle middle)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, along_z, tr, rx  # noqa: E402

HANDLE = (-12.0, 4.0)       # handle z range (grip at 0 -> hand wraps the middle)
HEAD = (4.0, 11.0)          # head cylinder z range
HR = 2.3


def stick_prims(M):
    return [along_z(M, 0.75, HANDLE[0], HANDLE[1], "wood"),                                # handle
            box(M @ tr(0, 0, HANDLE[0]), 0.9, 0.9, 0.5, "steel", r=0.3),                   # pull-cord cap at the end
            along_z(M, HR, HEAD[0], HEAD[1], "gun_steel"),                                 # explosive head (sheet-metal can)
            along_z(M, HR + 0.15, HEAD[0] + 1.2, HEAD[0] + 2.0, "rifmetal"),               # seam band
            along_z(M, HR - 0.4, HEAD[1], HEAD[1] + 0.5, "steel"),                         # flat top
            ell(M @ tr(0, 0, HEAD[0] - 0.4), 1.3, 1.3, 1.1, "steel")]                      # neck collar between head and handle


STICK_GRENADE = Weapon(stick_prims, gripR=(0, 0, -3.0), gripL=(0, 0, -3.0))

WEAPONS = {"stick_grenade": STICK_GRENADE}


def belt_grenades(T):
    """two stick grenades hung on the belt (right hip, handles down, heads up against the belt)."""
    out = []
    for x, z in ((-8.4, 0.5), (-7.4, -3.2)):
        M = T @ tr(x, 0.4, z) @ rx(180)
        out += [along_z(M, 0.7, -1.0, 6.0, "wood"),
                along_z(M, 1.6, 6.0, 9.5, "gun_steel")]
    return out


GEAR = {"belt_grenades": belt_grenades}
