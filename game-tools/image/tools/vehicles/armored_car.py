"""Four-wheel armored car in the style of the 1930s Japanese army cars (generic, no insignia). ~12 model units per metre.

Hull (origin = ground centre of the wheel footprint): ladder chassis, armoured engine hood with a louvred radiator
plate and headlights, tall boxy riveted crew compartment with a sloped front plate (visor slits), side doors with
team plates, fenders, running boards with a spare wheel on each side, 4 solid-tyre disc wheels (1 front axle,
1 rear axle). Turret (origin = RING): short drum with a stepped roof, hatch and a ball-mounted machine gun.
Hull frames: idle, move_1..move_4 (wheel spokes rotate 22.5 deg per frame, light bob), dead (burnt, flat tyres,
doors open). Turret frames: idle, fire, fire_2, dead.
Team colour: turret band + plates on both side doors (+ rear plate).
"""
import math

import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, rad, ang, mg_prims, flash_mg, scorch

RING = (0.0, 23.0, -7.0)
WX = 9.6                        # wheel centre |x|
WR = 5.2                        # wheel radius
AXLES = (24.0, -14.0)           # z of the front axle and the rear axle
TURRETS = ("mg",)


def _wheel(T, sx, z, rot, m, r=WR, hl=1.9, flat=False):
    rr = math.radians(rot)
    paint = [(lambda q: rad(q) < r - 1.4, m("veh_khaki")),                                                    # disc
             (lambda q, rr=rr: (rad(q) < r - 1.6) & (rad(q) > 1.4) & (np.abs(np.cos(2 * (ang(q) + rr))) < 0.80), m("veh_khaki2")),
             (lambda q: rad(q) < 1.4, m("veh_steel"))]
    if flat:
        return ell(T @ tr(sx * WX, r * 0.78, z), hl, r * 0.78, r * 1.08, m("veh_rubber"))
    return cyl(T @ tr(sx * WX, r, z) @ rz(90), r, hl, m("veh_rubber"), paint=paint)


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    rot = k * 22.5
    bob = 0.3 if k % 2 == 1 else 0.0
    sag = -1.1 if wreck else 0.0
    m = mat_fn(wreck)
    T0 = tr(0, bob + sag, 0)
    p = []
    # ---- wheels
    for sx in (1, -1):
        for z in AXLES:
            p.append(_wheel(tr(0, bob, 0), sx, z, rot, m, flat=wreck))
    # ---- chassis + fenders + running boards + spare wheels
    p.append(rbox(T0 @ tr(0, 4.8, 3.0), 7.0, 1.3, 27.0, m("veh_khaki2"), r=0.8))
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * WX, WR + 6.4, AXLES[0]), 2.5, 0.5, 7.6, m("veh_khaki"), r=0.4))              # front fender
        p.append(rbox(T0 @ tr(sx * WX, WR + 6.4, AXLES[1]), 2.5, 0.5, 7.6, m("veh_khaki"), r=0.4))   # rear fender
        p.append(rbox(T0 @ tr(sx * (WX + 0.4), 7.0, 5.0), 1.8, 0.35, 8.0, m("veh_khaki2"), r=0.3))             # running board
        p.append(_wheel(T0 @ tr(sx * 0.3, 7.35, 0), sx, 6.0, 30.0, m, r=4.4, hl=0.9))                        # spare wheel on the running board
    # ---- engine hood + radiator armour + headlights
    hood_louv = [(lambda q: (q[:, 1] > 4.2) & (np.abs(q[:, 2]) < 7.0) & (np.floor(q[:, 0] / 2.4) % 2 == 0), m("veh_khaki2"))]
    p.append(rbox(T0 @ tr(0, 10.6, 23.5), 7.4, 4.6, 10.5, m("veh_khaki"), paint=hood_louv, r=0.9))                # hood z 13..34
    rad_slats = [(lambda q: (q[:, 2] > 0.5) & (np.abs(q[:, 0]) < 5.5) & (np.abs(q[:, 1]) < 3.6) & (np.floor(q[:, 1] / 1.3) % 2 == 0), m("veh_khaki"))]
    p.append(rbox(T0 @ tr(0, 10.8, 34.6) @ rx(-8), 7.6, 4.9, 0.9, m("veh_khaki2"), paint=rad_slats, r=0.5))
    for sx in (1, -1):
        p.append(ell(T0 @ tr(sx * 6.0, 15.9, 32.5), 1.3, 1.3, 1.1, m("veh_steel"), paint=[(lambda q: q[:, 2] > 0.6, m("bright"))]))
    p.append(rbox(T0 @ tr(0, 10.5, 10.0), 8.0, 4.5, 3.2, m("veh_khaki"), r=0.6))                                   # cowl between hood and body
    # ---- crew compartment
    body_paint = [(lambda q: (q[:, 2] < -17.0) & (np.abs(q[:, 0]) < 2.2) & (np.abs(q[:, 1] - 1.0) < 1.6), m("team"))]  # rear plate
    p.append(rbox(T0 @ tr(0, 14.2, -10.5), 8.8, 8.4, 17.5, m("veh_khaki"), paint=body_paint, r=1.0))            # z -28..7, y 5.8..22.6
    slope_ang = math.degrees(math.atan2(7.4, 6.0))
    visor = [(lambda q: (q[:, 1] > 0.5) & (np.abs(np.abs(q[:, 0]) - 3.6) < 1.9) & (np.abs(q[:, 2]) < 0.45), m("void"))]
    p.append(rbox(T0 @ tr(0, 18.9, 10.0) @ rx(slope_ang), 8.8, 0.8, 4.8, m("veh_khaki"), paint=visor, r=0.5))      # sloped front plate
    p.append(rbox(T0 @ tr(0, 22.8, -10.5), 8.4, 0.4, 17.0, m("veh_khaki2"), r=0.3))                                # roof
    p.append(rbox(T0 @ tr(0, 23.4, 3.0), 2.6, 0.3, 2.2, m("veh_khaki"), r=0.3))                                    # front roof hatch
    door_team = [(lambda q: (np.abs(q[:, 0]) > 0.1) & (np.abs(q[:, 1] - 1.2) < 1.5) & (np.abs(q[:, 2]) < 1.7), m("team"))]
    for sx in (1, -1):
        if wreck:
            Td = T0 @ tr(sx * 8.9, 13.5, 1.2) @ ry(sx * 65) @ tr(0, 0, -4.2)
        else:
            Td = T0 @ tr(sx * 8.9, 13.5, -2.0)
        p.append(rbox(Td, 0.35, 4.8, 4.2, m("veh_khaki2"), paint=door_team, r=0.3))                               # side doors
    p.append(rbox(T0 @ tr(0, 12.5, -28.1), 2.8, 4.8, 0.35, m("veh_khaki2"), r=0.3))                                # rear door
    # rivets along the body top edge and the hood edge
    for sx in (1, -1):
        for z in np.arange(-26.0, 6.0, 3.6):
            p.append(ell(T0 @ tr(sx * 8.9, 21.8, z), 0.5, 0.5, 0.5, m("veh_khaki2")))
        for z in np.arange(14.5, 33.0, 3.8):
            p.append(ell(T0 @ tr(sx * 7.5, 14.9, z), 0.45, 0.45, 0.45, m("veh_khaki2")))
    # turret ring
    p.append(cyl(T0 @ tr(*RING), 6.6, 0.5, m("veh_khaki2")))
    ov = []
    if wreck:
        p.append(scorch(z=2.0, rx_=18.0, rz_=30.0))
    return p, ov


def turret(kind, frame):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    p, ov = [], []
    band = [(lambda q: np.abs(q[:, 1] + 1.3) < 0.8, m("team")),
            (lambda q: (np.abs(q[:, 1] - 1.6) < 0.3) & (q[:, 2] > 3.2) & ((np.abs(q[:, 0]) < 0.9) | (np.abs(np.abs(q[:, 0]) - 4.0) < 0.9)), m("void"))]
    p.append(cyl(tr(0, 3.5, 0), 6.2, 3.3, m("veh_khaki"), paint=band))                      # drum y 0.2..6.8
    p.append(cyl(tr(0, 7.4, 0), 5.4, 0.7, m("veh_khaki2")))                                 # stepped roof
    p.append(cyl(tr(0, 8.5, 0), 4.4, 0.4, m("veh_khaki")))
    hatch_T = tr(0, 9.2, -1.0)
    if wreck:
        hatch_T = tr(2.5, 9.6, -3.2) @ rx(-70) @ rz(20)
    p.append(cyl(hatch_T, 2.6, 0.4, m("veh_khaki2")))                                        # hatch
    for a in np.arange(0, 360, 30):                                                           # rivet ring under the roof
        p.append(ell(tr(5.9 * math.sin(math.radians(a)), 6.6, 5.9 * math.cos(math.radians(a))), 0.4, 0.4, 0.4, m("veh_khaki2")))
    G = tr(0, 3.6, 6.4)
    if wreck:
        G = G @ rx(15)
    p += mg_prims(G, m, length=8.5)
    if frame == "fire":
        ov += flash_mg(G, 8.6, strong=True)
    elif frame == "fire_2":
        ov += flash_mg(G, 8.6, strong=False)
    if wreck:
        K = rx(8) @ rz(6)
        p = [q.moved(K) for q in p]
    return p, ov
