"""WWI two-wheel supply wagon (limber cart) from primitives.

Wagon frame: origin = ground under the middle of the axle line, y up, z forward (towards the horses), x = left.
build_wagon(wheel_ang, load) -> list of prims; pennant / tarp / tailboard variants via keyword args.
Dimensions come from tune/parts.json "wagon" (constants below) (model units, 64x80 design scale); the team colour is a band on the canvas cover + a pennant.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import rbox, ell, seg, cyl, tr, rx, ry, rz  # noqa: E402
from parts.horse import look_frame  # noqa: E402

import tune  # noqa: E402

_D = tune.get("parts", "wagon")        # dimensions live in tune/parts.json "wagon"
WR = _D["wheel_r"]
AXLE_Y, AXLE_Z = _D["axle_y"], _D["axle_z"]
TRACK = _D["track"]                    # wheel centre x
BED_Y = _D["bed_y"]                    # bed floor centre height (top = +1)
BED_Z0, BED_Z1 = _D["bed_z"]
BED_HW = _D["bed_hw"]                  # bed half width (floor)
SIDE_H = _D["side_h"]                  # side board half height
SEAT_Y, SEAT_Z = _D["seat_y"], _D["seat_z"]
FOOT_Y = _D["foot_y"]
HITCH = tuple(_D["hitch"])             # pole root under the footboard
CR = tuple(_D["crate"])                # crate half sizes (x, y, z)
CRATE_X = _D["crate_x"]                # crate column centre x
ROWS, LAYERS = _D["rows"], _D["layers"]
SPOKES = _D["spokes"]
TARP_Y, TARP_HY = _D["tarp_y"], _D["tarp_hy"]
STAFF_H = _D["staff_h"]
ROW_PITCH = 2 * CR[2] + 0.4


def _strap(hz):
    return lambda p: (np.abs(p[:, 0]) < 0.9) & (np.abs(p[:, 2]) > hz * 0.55)


def crate(M, mat="crate"):
    return rbox(M, *CR, mat, r=0.45, paint=[(_strap(CR[2]), "wood")])


def wheel(side, ang):
    """spoked wooden wheel, wooden felloe with iron tyre, axis along x. ang = rotation (deg, spokes)."""
    cx = side * TRACK
    out = []
    n = 16
    chord = 2 * WR * math.sin(math.pi / n)
    for k in range(n):
        a = math.radians(k * 360.0 / n)
        r = np.array([0.0, math.sin(a), math.cos(a)])
        t = np.array([0.0, math.cos(a), -math.sin(a)])
        c = np.array([cx, AXLE_Y, AXLE_Z]) + r * (WR - 1.0)
        M = look_frame(c, t, up=(1, 0, 0))
        out.append(rbox(M, 1.1, 1.7, chord / 2 + 0.25, "wood", r=0.4,
                        paint=[(lambda p: p[:, 0] > 0.15, "steel")]))
    for k in range(SPOKES):
        a = math.radians(ang + k * 360.0 / SPOKES)
        r = np.array([0.0, math.sin(a), math.cos(a)])
        c = np.array([cx, AXLE_Y, AXLE_Z])
        out.append(seg(c, c + r * (WR - 1.8), 0.55, "wood"))
    Mh = tr(cx, AXLE_Y, AXLE_Z) @ rz(90)
    out.append(cyl(Mh, 2.5, 2.0, "wood"))
    out.append(ell(tr(cx + side * 2.0, AXLE_Y, AXLE_Z), 0.9, 1.9, 1.9, "steel"))
    return out


def tailboard(lowered):
    if lowered:
        return rbox(tr(0, BED_Y + 0.6, BED_Z0 - 3.0), BED_HW - 0.2, 0.8, 3.6, "wagon_gy", r=0.3)
    return rbox(tr(0, BED_Y + 1 + SIDE_H, BED_Z0 + 0.6), BED_HW - 0.2, SIDE_H + 0.2, 0.8, "wagon_gy", r=0.3)


def build_wagon(wheel_ang=0.0, supply=False, pennant=0.0, droop=False, crates=True, tarp=True, tailboard_down=None,
                pole_end=None):
    """-> prims (wagon frame). supply = canvas rolled back, tailboard down. pennant = flutter yaw (deg)."""
    out = []
    down = supply if tailboard_down is None else tailboard_down
    out += wheel(1, wheel_ang) + wheel(-1, wheel_ang)
    out.append(seg((-TRACK - 1.5, AXLE_Y, AXLE_Z), (TRACK + 1.5, AXLE_Y, AXLE_Z), 0.8, "steel"))
    zc = (BED_Z0 + BED_Z1) / 2
    hl = (BED_Z1 - BED_Z0) / 2
    out.append(rbox(tr(0, BED_Y, zc), BED_HW, 1.0, hl, "wagon_gy", r=0.4))                       # floor
    out.append(rbox(tr(0, AXLE_Y + 2.8, AXLE_Z), 9.0, 1.0, 2.4, "wood", r=0.4))                # axle bed / spring block
    for s in (1, -1):
        out.append(rbox(tr(s * (BED_HW - 0.9), BED_Y + 0.2 + SIDE_H, zc), 0.9, SIDE_H, hl, "wagon_gy", r=0.3))         # side boards
        out.append(rbox(tr(s * (BED_HW - 0.9), BED_Y + 0.2 + SIDE_H, 5.0), 1.0, SIDE_H, 4.5, "wagon_gy", r=0.3))       # seat-box sides
    out.append(tailboard(down))
    # driver's seat box + bench + footboard
    out.append(rbox(tr(0, (BED_Y + SEAT_Y) / 2 - 1.0, SEAT_Z), 12.0, (SEAT_Y - BED_Y) / 2 - 1.0, 4.2, "wagon_gy", r=0.5))
    out.append(rbox(tr(0, SEAT_Y - 0.2, SEAT_Z - 0.3), 12.4, 0.9, 4.8, "leather", r=0.5))
    out.append(rbox(tr(0, SEAT_Y + 4.5, SEAT_Z - 4.4), 11.4, 4.2, 0.7, "leather", r=0.4))       # backrest
    out.append(rbox(tr(0, FOOT_Y, 13.6), 11.4, 0.8, 4.4, "wagon_gy", r=0.3))
    for s in (1, -1):
        out.append(seg((s * 10, FOOT_Y, 11), (s * 10, BED_Y, 8), 0.6, "steel"))
    # pole (tongue) toward the horses
    pe = pole_end or (0.0, 22.0, 52.0)
    out.append(seg(HITCH, pe, 1.15, "wood", r1=0.9))
    out.append(rbox(tr(*pe) @ tr(0, 0.0, -1.0), 6.0, 0.9, 0.9, "steel", r=0.3))                 # yoke bar between the collars
    # cargo
    if crates:
        for layer in range(LAYERS):
            for zi in range(ROWS):
                for s in (1, -1):
                    if supply and layer >= 1 and zi < 2:
                        continue
                    z = BED_Z0 + 1.5 + CR[2] + zi * ROW_PITCH
                    y = BED_Y + 1.0 + CR[1] + layer * (2 * CR[1] + 0.1)
                    out.append(crate(tr(s * CRATE_X, y, z)))
    if tarp:
        z1 = 0.6      # supply: cover pulled forward, rear exposed
        z0 = (BED_Z0 + 6.0) if not supply else (BED_Z0 + 1.5 + 2 * ROW_PITCH + 1.5)
        hlt = (z1 - z0) / 2
        zt = (z0 + z1) / 2
        band_z = -hlt + 4.0

        def paint_fn(p, hlt=hlt, band_z=band_z):
            return np.abs(p[:, 2] - band_z) < 1.9

        def rope_fn(p, hlt=hlt):
            return (((p[:, 2] + hlt) % 6.5) < 0.7) & (p[:, 1] > -1)

        out.append(rbox(tr(0, BED_Y + TARP_Y, zt), BED_HW - 0.3, TARP_HY, hlt, "tarp", r=6.6, frac=0.95,
                        paint=[(rope_fn, "canvas"), (paint_fn, "team")]))
        if supply:                               # rolled-back canvas flap at the rear end of the cover
            out.append(seg((-(BED_HW - 1.5), BED_Y + TARP_Y + 0.3, z0 - 0.6), (BED_HW - 1.5, BED_Y + TARP_Y + 0.3, z0 - 0.6), 1.7, "tarp"))
    # pennant on a staff at the left rear corner
    px, pz = BED_HW - 1.2, BED_Z0 + 1.0
    top = (px, BED_Y + STAFF_H, pz)
    out.append(seg((px, BED_Y + 1.0, pz), top, 0.45, "wood"))
    out.append(ell(tr(*top) @ tr(0, 0.9, 0), 0.8, 0.8, 0.8, "brass"))
    flag = tr(*top) @ tr(0, -1.6, 0) @ ry(pennant) @ (rx(60) if droop else np.eye(4))
    out.append(rbox(flag @ tr(0, 0, -3.8), 0.3, 1.9, 3.8, "team", r=0.15))
    out.append(rbox(flag @ tr(0, -0.2, -8.2), 0.3, 1.1, 2.0, "team", r=0.15))
    return out
