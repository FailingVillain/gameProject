"""Austin armoured car (Russian service 1915-1918, Austin 1st/2nd series style). ~12 model units per metre.

Hull (origin = ground centre of the wheel footprint): ladder chassis, long bonnet with an armoured radiator front
(vertical louvre slits), slab-sided riveted crew box with a flat front plate and a driver's visor, big spoked wheels
(1 front axle, twin rear wheels), no fenders to speak of, small side doors with team plates.
Turrets (origin = RING): two identical cylindrical MG turrets with conical caps and a water-jacketed Maxim gun,
placed diagonally on the roof (left-front and right-rear). The hull json declares both mounts (RINGS); the
turret sheet is shared and the game draws it twice.
Hull frames: idle, move_1..move_4 (spokes rotate), dead. Turret frames: idle, fire, fire_2, dead.
Paint: Russian field green (veh_od). Team colour: door plates, rear hull stripe (no turret band).
"""
import math

import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, rad, ang, flash_mg, scorch

ROOF_Y = 24.0
RINGS = [(-5.3, ROOF_Y, 1.0), (5.3, ROOF_Y, -7.0)]     # two MG turrets staggered diagonally AND wide apart (pure side-by-side stacks in side views)
RING = RINGS[0]
WX = 9.4
WR = 6.0
AXLES = (24.0, -6.0, -20.0)                              # 3 axles = 6 wheels, single wheels, 14 apart (wheel diameter 12): no overlap
TURRETS = ("mg",)


def _wheel(T, sx, z, rot, m, r=WR, hl=1.5, flat=False):
    rr = math.radians(rot)
    if flat:
        return ell(T @ tr(sx * WX, r * 0.78, z), hl, r * 0.78, r * 1.08, m("veh_rubber"))
    spokes = (lambda q, rr=rr: (rad(q) < r - 1.3) & (rad(q) > 1.5) & (np.cos(3 * (ang(q) + rr)) > -0.55))     # 6 wide gaps
    paint = [(lambda q: rad(q) < r - 1.0, m("veh_steel")),                                                  # dark between spokes
             (spokes, m("veh_od2")),
             (lambda q: rad(q) < 1.7, m("veh_od"))]
    return cyl(T @ tr(sx * WX, r, z) @ rz(90), r, hl, m("veh_rubber"), paint=paint)


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    rot = k * 20.0
    bob = 0.3 if k % 2 == 1 else 0.0
    sag = -1.1 if wreck else 0.0
    m = mat_fn(wreck)
    T0 = tr(0, bob + sag, 0)
    p = []
    for sx in (1, -1):
        p.append(_wheel(tr(0, bob, 0), sx, AXLES[0], rot, m, flat=wreck))
        p.append(_wheel(tr(0, bob, 0), sx, AXLES[1], rot + 10, m, flat=wreck))
        p.append(_wheel(tr(0, bob, 0), sx, AXLES[2], rot + 20, m, flat=wreck))
    # chassis, thin mud guards over the front wheels, running boards
    p.append(rbox(T0 @ tr(0, 5.4, 3.0), 7.0, 1.3, 30.0, m("veh_od2"), r=0.8))
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * (WX + 0.3), WR + 1.6 + 5.2, AXLES[0]), 2.2, 0.35, 7.0, m("veh_od"), r=0.3))
        p.append(rbox(T0 @ tr(sx * (WX + 0.3), WR + 1.6 + 5.2, (AXLES[1] + AXLES[2]) / 2), 2.6, 0.35, 16.0, m("veh_od"), r=0.3))
        p.append(rbox(T0 @ tr(sx * (WX - 0.2), 7.2, 4.0), 1.6, 0.35, 8.5, m("veh_od2"), r=0.3))
    # long bonnet (z 10..37) with louvres, armoured radiator front with vertical slits, headlights
    louv = [(lambda q: (q[:, 1] > 3.0) & (np.abs(q[:, 2]) < 9.0) & (np.floor(q[:, 0] / 2.2) % 2 == 0), m("veh_od2"))]
    p.append(rbox(T0 @ tr(0, 11.4, 23.5), 7.0, 5.2, 13.5, m("veh_od"), paint=louv, r=1.0))
    slits = [(lambda q: (q[:, 2] > 0.2) & (np.abs(q[:, 0]) < 5.6) & (np.abs(q[:, 1]) < 4.8) & (np.floor(q[:, 0] / 1.4) % 2 == 0), m("void"))]
    p.append(rbox(T0 @ tr(0, 11.2, 37.4), 7.6, 5.8, 0.9, m("veh_od"), paint=slits, r=0.5))           # radiator armour plate
    p.append(rbox(T0 @ tr(0, 17.2, 37.0), 7.2, 0.4, 1.0, m("veh_od2"), r=0.2))                       # top lip
    for sx in (1, -1):
        p.append(ell(T0 @ tr(sx * 6.4, 14.0, 38.6), 1.4, 1.4, 1.1, m("veh_steel"), paint=[(lambda q: q[:, 2] > 0.5, m("bright"))]))
        p.append(rbox(T0 @ tr(sx * 7.1, 11.4, 23.5), 0.3, 4.4, 12.0, m("veh_od2"), r=0.2))          # bonnet side panel line
    # tall slab-sided crew box z -30..10, y 6.5..24
    p.append(rbox(T0 @ tr(0, 15.3, -10.0), 9.7, 8.7, 20.0, m("veh_od"), r=1.0))
    visor = [(lambda q: (q[:, 1] > 1.8) & (q[:, 1] < 4.0) & (np.abs(np.abs(q[:, 0]) - 3.4) < 1.8) & (q[:, 2] > 0.0), m("void"))]
    p.append(rbox(T0 @ tr(0, 15.8, 10.2), 9.4, 7.4, 0.55, m("veh_od"), paint=visor, r=0.4))        # flat front plate + driver's visors
    p.append(rbox(T0 @ tr(0, 24.1, -10.0), 9.5, 0.4, 19.5, m("veh_od2"), r=0.3))                    # roof
    door_team = [(lambda q: (np.abs(q[:, 0]) > 0.1) & (np.abs(q[:, 1] - 1.0) < 1.5) & (np.abs(q[:, 2]) < 1.7), m("team"))]
    for sx in (1, -1):
        if wreck:
            Td = T0 @ tr(sx * 9.9, 14.5, 4.5) @ ry(sx * 65) @ tr(0, 0, -4.2)
        else:
            Td = T0 @ tr(sx * 9.9, 14.5, 1.0)
        p.append(rbox(Td, 0.35, 5.0, 4.2, m("veh_od2"), paint=door_team, r=0.3))
        for z in np.arange(-28.0, 9.0, 3.4):                                                         # riveted seams
            p.append(ell(T0 @ tr(sx * 9.8, 23.0, z), 0.5, 0.5, 0.5, m("veh_od2")))
            p.append(ell(T0 @ tr(sx * 9.8, 8.4, z), 0.5, 0.5, 0.5, m("veh_od2")))
    p.append(rbox(T0 @ tr(0, 13.5, -30.1), 3.0, 5.2, 0.35, m("veh_od2"), r=0.3))                   # rear door
    p.append(rbox(T0 @ tr(0, 19.5, -30.3), 7.0, 2.6, 0.5, m("team"), r=0.2))                       # rear team marking (wide stripe above the door)
    for ring in RINGS:
        p.append(cyl(T0 @ tr(*ring), 4.3, 0.45, m("veh_od2")))                                       # turret rings
    ov = []
    if wreck:
        p.append(scorch(z=2.0, rx_=18.0, rz_=32.0))
    return p, ov


def _maxim(G, m, length=9.0):
    """Maxim: ball mount, fat water jacket, thin muzzle, flash hider, spade grips."""
    return [ell(G @ tr(0, 0, 0.4), 2.0, 2.0, 1.8, m("veh_steel")),
            along_z(G, 1.0, 0.8, 6.2, m("veh_steel")),                                              # water jacket
            along_z(G, 0.45, 6.2, length, m("veh_steel")),
            along_z(G, 0.7, length - 0.6, length, m("veh_steel")),
            along_z(G @ tr(0, 1.4, 0), 0.4, 0.4, 2.2, m("veh_steel"))]


def turret(kind, frame):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    p, ov = [], []
    band = [(lambda q: (np.abs(q[:, 1] - 3.2) < 0.4) & (q[:, 2] > 2.6) & (np.abs(q[:, 0]) < 1.0), m("void"))]
    p.append(cyl(tr(0, 2.6, 0), 3.7, 2.6, m("veh_od"), paint=band))                                  # drum y 0..5.2
    p.append(cyl(tr(0, 5.7, 0), 3.2, 0.5, m("veh_od2")))                                              # cap step
    p.append(cyl(tr(0, 6.5, 0), 2.4, 0.4, m("veh_od")))
    p.append(cyl(tr(0, 7.1, 0), 1.2, 0.3, m("veh_od2")))
    for a in np.arange(0, 360, 30):
        p.append(ell(tr(3.6 * math.sin(math.radians(a)), 4.9, 3.6 * math.cos(math.radians(a))), 0.38, 0.38, 0.38, m("veh_od2")))
    G = tr(0, 2.8, 3.6)
    if wreck:
        G = G @ rx(15)
    p += _maxim(G, m)
    if frame == "fire":
        ov += flash_mg(G, 9.0, strong=True)
    elif frame == "fire_2":
        ov += flash_mg(G, 9.0, strong=False)
    if wreck:
        K = rx(8) @ rz(6)
        p = [q.moved(K) for q in p]
    return p, ov
