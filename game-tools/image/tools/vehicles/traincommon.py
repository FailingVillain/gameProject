"""Shared parts for the 1920 armoured-train blockout cars (T30d). Model units: 1 map tile = 32 units (Mark V hull = 64 = 2 tiles).
Car body is symmetric front/back along z, ~1.9 tiles long (z -30..30 + buffers), 0.6 tile wide (x +-9.5). Rail gauge x +-6.4."""
import math

import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import register_material, mat_fn, rad, ang, scorch, WRECK_MAP

register_material("veh_wood", [(60, 44, 28), (86, 64, 40), (112, 84, 54), (140, 108, 72)])
WRECK_MAP["veh_wood"] = "veh_burn"

HL = 30.0            # half body length
WX = 6.4             # wheel x (rail gauge)
WR = 3.4             # wheel radius
BOGIES = (19.0, -19.0)


def wheel(T, sx, z, rot, m, flat, r=WR):
    if flat:
        return ell(T @ tr(sx * WX, r * 0.8, z), 1.2, r * 0.8, r * 1.05, m("veh_steel"))
    paint = [(lambda q, r=r: rad(q) < r - 0.9, m("veh_track2")),
             (lambda q, k=math.radians(rot): (rad(q) < r - 0.9) & (rad(q) > 0.7) & (np.cos(3 * (ang(q) + k)) > 0.6), m("veh_steel"))]
    return cyl(T @ tr(sx * WX, r, z) @ rz(90), r, 1.0, m("veh_steel"), paint=paint)


def undercarriage(T0, m, wreck, rot):
    """frame deck + two 2-axle bogies + buffers/couplers (all cars)."""
    p = [rbox(T0 @ tr(0, 5.2, 0), 8.0, 0.9, HL + 1.0, m("veh_steel"), r=0.5)]
    for bz in BOGIES:
        p.append(rbox(T0 @ tr(0, 3.6, bz), 6.9, 0.7, 5.8, m("veh_track"), r=0.4))
        for dz in (-3.6, 3.6):
            for sx in (1, -1):
                p.append(wheel(T0 @ tr(0, 0, 0), sx, bz + dz, rot + (15 if dz < 0 else 0), m, wreck))
            p.append(cyl(T0 @ tr(0, WR, bz + dz) @ rz(90), 0.6, WX, m("veh_track")))      # axle
    for sz in (1, -1):
        for sx in (1, -1):
            p.append(along_z(T0 @ tr(sx * 4.4, 5.4, sz * (HL + 1.0)), 0.9, 0.0, sz * 2.8 if sz > 0 else -2.8, m("veh_steel")))
            p.append(cyl(T0 @ tr(sx * 4.4, 5.4, sz * (HL + 3.8)) @ rx(90), 1.7, 0.35, m("veh_steel")))
        p.append(rbox(T0 @ tr(0, 5.0, sz * (HL + 3.0)), 0.7, 0.7, 2.0, m("veh_track"), r=0.3))   # coupler
    return p


def slit_paint(m, y0, step=3.2):
    return [(lambda q: (np.abs(q[:, 1] - y0) < 0.7) & (np.floor(q[:, 2] / step) % 2 == 0), m("void"))]


def armoured_body(T0, m, wreck, y0=6.2, h=11.0, hz=HL, hx=8.6, team=True):
    """sloping-side armoured wagon box: sides tilt in 8 deg, rivet rows, firing slits, team band on the roof rim."""
    p = []
    for sx in (1, -1):
        side = slit_paint(m, 1.5) + [(lambda q: (np.abs(q[:, 1] + 3.0) < 0.4) & (np.floor(q[:, 2] / 2.5) % 2 == 0), m("veh_od2"))]
        if wreck and sx == 1:
            side = [(lambda q: (np.abs(q[:, 2] - 8) < 6) & (np.abs(q[:, 1]) < 3.2), m("void"))]      # blown-open side
        p.append(rbox(T0 @ tr(sx * (hx - 0.4), y0 + h / 2, 0) @ rz(sx * 8), 0.5, h / 2, hz, m("veh_od"), paint=side, r=0.3))
    for sz in (1, -1):
        end = [(lambda q: (q[:, 2] * sz > 0) & (np.abs(q[:, 0]) < 2.4) & (np.abs(q[:, 1] - 1.0) < 1.0), m("team"))] if team else []
        p.append(rbox(T0 @ tr(0, y0 + h / 2, sz * (hz - 0.4)), hx - 1.0, h / 2, 0.5, m("veh_od"), paint=end, r=0.3))
    roof = [(lambda q: (np.abs(np.abs(q[:, 0]) - 6.0) < 1.0) & (np.abs(q[:, 2]) < hz - 3), m("team"))] if team else []
    p.append(rbox(T0 @ tr(0, y0 + h + 0.3, 0), hx - 1.8, 0.45, hz, m("veh_od2"), paint=roof, r=0.3))
    return p


def finish(p, wreck, z=0.0, rz_=34.0):
    return p, ([])


def wreck_scorch(z=0.0):
    return scorch(z=z, rx_=14.0, rz_=36.0)
