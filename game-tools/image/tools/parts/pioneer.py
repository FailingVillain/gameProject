"""Korean Division T32d: combat engineer (infantry_pioneer) kit. Registered in unit_loader.py. Torso frame: origin at the belt line, y up, z forward, -z = back.
wire_cutter = long-handled wire cutter strapped diagonally across the back of the field pack (steel jaws up, wooden handles).
tool_pouch  = canvas tool pouch on the left hip (replaces the haversack) with pliers handles sticking out and a pair of leather gloves tied to it."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import rbox as box, seg, tr, rx, pt  # noqa: E402


def wire_cutter(T):
    z = -13.0
    a0, a1 = (-6.0, 2.0, z), (4.2, 20.0, z)          # handle A: low-left -> high-right across the pack back
    b0, b1 = (-3.6, 2.0, z), (6.6, 20.0, z)          # handle B parallel, 2.4 apart
    p = [seg(pt(T, a0), pt(T, a1), 0.55, "wood"), seg(pt(T, b0), pt(T, b1), 0.55, "wood"),
         seg(pt(T, (-6.0, 2.0, z)), pt(T, (-5.6, 1.0, z)), 0.7, "steel"), seg(pt(T, (-3.6, 2.0, z)), pt(T, (-3.2, 1.0, z)), 0.7, "steel")]
    # jaws: two short steel blades splaying from the top of the handles, pivot rivet between
    p += [seg(pt(T, (4.2, 20.0, z)), pt(T, (3.4, 24.2, z)), 0.75, "steel"), seg(pt(T, (6.6, 20.0, z)), pt(T, (7.4, 24.2, z)), 0.75, "steel"),
          box(T @ tr(5.4, 20.4, z), 1.9, 0.9, 0.9, "steel", r=0.4)]
    for t in (0.3, 0.62):                                                    # two leather straps round the handles and the pack
        x, y = -6.0 + 10.2 * t, 2.0 + 18.0 * t
        p.append(box(T @ tr(x + 1.2, y, z + 1.6), 3.4, 0.6, 2.6, "leather", r=0.3))
    return p


def tool_pouch(T):
    x, y, z = 8.3, -0.2, -1.6
    p = [box(T @ tr(x, y, z), 2.4, 3.4, 3.0, "canvas", r=1.3, frac=0.9),         # pouch body
         box(T @ tr(x + 0.2, y + 3.0, z), 2.5, 0.9, 3.1, "canvas", r=0.5),       # flap
         box(T @ tr(x + 2.5, y + 2.3, z), 0.3, 1.0, 0.7, "brass", r=0.15)]       # buckle
    p += [seg(pt(T, (x - 0.9, y + 3.6, z - 0.8)), pt(T, (x - 1.3, y + 7.0, z - 1.2)), 0.45, "steel"),   # pliers handles out of the flap
          seg(pt(T, (x + 0.6, y + 3.6, z - 0.8)), pt(T, (x + 1.1, y + 7.0, z - 1.2)), 0.45, "steel")]
    p += [box(T @ tr(x + 1.6, y - 4.8, z + 0.6), 0.7, 2.0, 1.4, "leather", r=0.5),                    # gloves tied below the pouch
          box(T @ tr(x + 1.6, y - 3.4, z + 0.6), 0.9, 0.6, 1.5, "leather", r=0.3)]
    return p


GEAR = {"wire_cutter": wire_cutter, "tool_pouch": tool_pouch}
