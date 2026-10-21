"""Generic WWI-style submachine gun (MP18 class): perforated barrel jacket, side (left) stick magazine, wooden stock.
Weapon frame: origin at the receiver, z = muzzle direction, y up, +x = left side (magazine side)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, along_z, tr  # noqa: E402

JY = 0.6          # barrel axis height above the receiver origin
MUZZLE = 15.0


def mp18_prims(M):
    Mj = M @ tr(0, JY, 0)
    p = [box(M @ tr(0, 0.3, -1.2), 1.35, 1.5, 4.2, "steel", r=0.4),                  # receiver
         along_z(Mj, 1.6, 3.0, 12.2, "steel"),                                        # barrel jacket
         along_z(Mj, 0.8, 12.2, MUZZLE, "steel"),                                     # bare barrel tip
         box(Mj @ tr(0, 1.9, 13.4), 0.35, 0.8, 0.4, "steel"),                         # front sight
         box(M @ tr(0, 1.9, -3.6), 0.5, 0.45, 0.8, "steel", r=0.2),                   # rear sight
         box(M @ tr(0, -0.7, -8.0), 1.45, 2.0, 4.2, "wood", r=0.6),                   # wooden stock
         box(M @ tr(0, -0.8, -4.6), 1.5, 2.0, 1.4, "wood", r=0.6),                    # wrist
         box(M @ tr(0, -1.4, -12.6), 1.3, 2.7, 1.3, "wood", r=0.5),                   # butt
         box(M @ tr(0, -2.7, -2.4), 0.4, 1.0, 1.8, "steel", r=0.2),                   # trigger guard
         # side magazine: housing + long stick sticking out to the left
         box(M @ tr(2.0, 0.4, 1.8), 0.9, 1.0, 1.5, "steel", r=0.3),
         box(M @ tr(5.6, 0.4, 1.8), 3.2, 0.8, 1.2, "rifmetal", r=0.3),
         box(M @ tr(8.9, 0.4, 1.8), 0.5, 0.9, 1.3, "steel", r=0.2)]                   # magazine floor plate
    # perforation: dark vent holes in rows along both flanks and the top of the jacket
    for k in range(6):
        z = 3.9 + 1.45 * k
        for sx in (-1, 1):
            p.append(box(Mj @ tr(sx * 1.5, 0.35, z), 0.2, 0.42, 0.42, "void", r=0.1))
        p.append(box(Mj @ tr(0, 1.5, z), 0.5, 0.2, 0.42, "void", r=0.1))
    return p


def _flash(M, z, y=0.6):
    return [ell(M @ tr(0, y, z + 2.2), 3.4, 3.4, 4.6, "flash_edge"),
            ell(M @ tr(0, y, z + 2.0), 2.4, 2.4, 3.6, "flash"),
            ell(M @ tr(0, y, z + 1.6), 1.4, 1.4, 2.2, "flash_core")]


def mp18_overlay(M, kind):
    return _flash(M, MUZZLE) if kind == "flash" else []


MP18 = Weapon(mp18_prims, gripR=(0, -2.3, -5.4), gripL=(0, -1.8, 6.6), overlay=mp18_overlay)

WEAPONS = {"mp18": MP18}
