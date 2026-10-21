"""Fengtian improvised armoured car: a ~1918 lorry with riveted boiler-plate bolted around the cab / bed, firing slits,
mismatched patch plates (rust / olive / steel), solid rubber wheels and an open-topped MG shield box on the bed roof.
Hull frames: idle, move_1..4, dead. Turret (MG shield box): idle, fire, fire_2, dead. Team colour: cab door plates, shield band."""
import math

import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, rad, ang, flash_mg, scorch, mg_prims

RING = (0.0, 22.0, -10.0)
TURRETS = ("mg",)
WX = 9.4
WR = 6.2
AXLES = (20.0, -14.0)


def _wheel(T, sx, z, rot, m, flat):
    if flat:
        return ell(T @ tr(sx * WX, WR * 0.78, z), 2.0, WR * 0.78, WR * 1.05, m("veh_rubber"))
    paint = [(lambda q: rad(q) < WR - 2.4, m("veh_steel")),
             (lambda q, r=math.radians(rot): (rad(q) < WR - 2.4) & (rad(q) > 1.4) & (np.cos(4 * (ang(q) + r)) > 0.5), m("veh_rust")),
             (lambda q: rad(q) < 1.4, m("veh_od"))]
    return cyl(T @ tr(sx * WX, WR, z) @ rz(90), WR, 2.0, m("veh_rubber"), paint=paint)


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    rot = k * 22.5
    bob = 0.3 if k % 2 == 1 else 0.0
    sag = -1.0 if wreck else 0.0
    m = mat_fn(wreck)
    T0 = tr(0, bob + sag, 0)
    p = []
    for sx in (1, -1):
        p.append(_wheel(tr(0, bob, 0), sx, AXLES[0], rot, m, wreck))
        p.append(_wheel(tr(0, bob, 0), sx, AXLES[1], rot + 15, m, wreck))
    p.append(rbox(T0 @ tr(0, 7.6, 0), 6.4, 1.2, 33.0, m("veh_steel"), r=0.6))
    patch = [(lambda q: (np.abs(q[:, 0] - 2.0) < 2.2) & (np.abs(q[:, 1]) < 2.0), m("veh_rust"))]
    p.append(rbox(T0 @ tr(0, 12.2, 26.0), 6.0, 4.4, 8.0, m("veh_od"), paint=patch, r=0.9))
    slit = [(lambda q: (q[:, 2] > 0.1) & (np.floor(q[:, 0] / 1.2) % 2 == 0), m("void"))]
    p.append(rbox(T0 @ tr(0, 11.6, 34.3), 5.4, 3.6, 0.5, m("veh_steel"), paint=slit, r=0.3))
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * 8.4, 9.4, 22.0), 1.4, 0.35, 6.5, m("veh_od2"), r=0.3))
        p.append(ell(T0 @ tr(sx * 4.8, 13.0, 34.4), 1.1, 1.1, 0.9, m("veh_steel")))
    cab_team = [(lambda q: (np.abs(q[:, 0]) > 0.1) & (np.abs(q[:, 1] - 1.0) < 1.3) & (np.abs(q[:, 2]) < 1.8), m("team"))]
    p.append(rbox(T0 @ tr(0, 17.0, 11.0), 7.4, 6.2, 5.6, m("veh_od"), r=0.8))
    wind = [(lambda q: (q[:, 2] > 0.1) & (np.abs(q[:, 1]) < 0.9) & (np.abs(np.abs(q[:, 0]) - 3.0) < 1.6), m("void"))]
    p.append(rbox(T0 @ tr(0, 17.8, 17.0) @ rx(-12), 7.4, 5.0, 0.5, m("veh_steel"), paint=wind, r=0.3))
    p.append(rbox(T0 @ tr(0, 23.4, 11.0), 7.6, 0.4, 5.8, m("veh_od2"), r=0.3))
    for sx in (1, -1):
        side = [(lambda q: (np.abs(q[:, 2] + 6.0) < 5.0) & (np.abs(q[:, 1] - 2.0) < 3.0), m("veh_rust")),
                (lambda q: (np.abs(q[:, 2] - 4.0) < 3.5) & (np.abs(q[:, 1] + 3.0) < 2.5), m("veh_steel")),
                (lambda q: (np.abs(q[:, 1] - 5.0) < 0.6) & (np.floor(q[:, 2] / 3.2) % 2 == 0), m("void"))]
        p.append(rbox(T0 @ tr(sx * 8.2, 15.4, -11.0) @ rz(sx * 4), 0.5, 7.4, 15.0, m("veh_od"), paint=side, r=0.3))
        for z in np.arange(-24.0, 3.0, 3.0):
            p.append(ell(T0 @ tr(sx * 8.8, 21.0, z), 0.45, 0.45, 0.45, m("veh_od2")))
        p.append(rbox(T0 @ tr(sx * 5.0, 17.0, 5.4), 0.4, 4.0, 1.2, m("veh_od2"), paint=cab_team, r=0.2))
    p.append(rbox(T0 @ tr(0, 15.4, 4.2), 7.4, 7.4, 0.5, m("veh_steel"), r=0.3))
    rear = [(lambda q: (q[:, 2] < 0) & (np.abs(q[:, 0]) < 2.2) & (np.abs(q[:, 1]) < 1.3), m("team"))]
    p.append(rbox(T0 @ tr(0, 15.0, -26.3), 7.2, 7.0, 0.5, m("veh_od"), paint=rear, r=0.3))
    p.append(rbox(T0 @ tr(0, 22.5, -11.0), 8.0, 0.4, 15.4, m("veh_od2"), r=0.3))
    p.append(cyl(T0 @ tr(*RING), 4.8, 0.45, m("veh_od2")))
    ov = []
    if wreck:
        p.append(scorch(z=0.0, rx_=16.0, rz_=34.0))
    return p, ov


def turret(kind, frame):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    p, ov = [], []
    band = [(lambda q: np.abs(q[:, 1] - 1.0) < 0.6, m("team")),
            (lambda q: (np.abs(q[:, 1] - 3.0) < 0.5) & (q[:, 2] > 2.0) & (np.abs(q[:, 0]) < 1.6), m("void"))]
    p.append(rbox(tr(0, 2.6, -0.4), 4.4, 2.6, 4.2, m("veh_od"), paint=band, r=0.7))
    p.append(rbox(tr(0, 5.4, -3.0), 4.4, 0.3, 1.2, m("veh_od2"), r=0.2))
    G = tr(0, 3.8, 3.4)
    if wreck:
        G = G @ rx(14)
    p += mg_prims(G, m)
    if frame == "fire":
        ov += flash_mg(G, 9.5, strong=True)
    elif frame == "fire_2":
        ov += flash_mg(G, 9.5, strong=False)
    if wreck:
        Kk = rx(8) @ rz(-6)
        p = [q.moved(Kk) for q in p]
    return p, ov
