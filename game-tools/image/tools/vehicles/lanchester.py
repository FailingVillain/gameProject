"""Lanchester armoured car (1914, 4x2): boat-nosed sloped bonnet armour, tall crew box, ONE central revolving turret with
a Vickers MG, single rear wheels. Khaki paint (veh_khaki). Hull frames: idle, move_1..4, dead. Turret: idle, fire, fire_2, dead.
Team colour: turret band, door plates, rear plate."""
import math

import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, rad, ang, flash_mg, scorch
from vehicles.austin import _wheel, _maxim

ROOF_Y = 20.6
RING = (0.0, ROOF_Y, -4.0)
TURRETS = ("mg",)
WX = 9.0
AXLES = (22.0, -14.0)


def _khaki(m0):
    return lambda n: m0({"veh_od": "veh_khaki", "veh_od2": "veh_khaki2"}.get(n, n))


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    rot = k * 20.0
    bob = 0.3 if k % 2 == 1 else 0.0
    sag = -1.0 if wreck else 0.0
    m0 = mat_fn(wreck)
    K = _khaki(m0)
    T0 = tr(0, bob + sag, 0)
    p = []
    for sx in (1, -1):
        p.append(_wheel(tr(0, bob, 0), sx, AXLES[0], rot, m0, r=5.8, flat=wreck))
        p.append(_wheel(tr(0, bob, 0), sx, AXLES[1], rot + 10, m0, r=6.2, flat=wreck))
    p.append(rbox(T0 @ tr(0, 5.4, 4.0), 6.6, 1.2, 28.0, m0("veh_od2"), r=0.8))
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * (WX + 0.3), 11.8, AXLES[0]), 2.2, 0.35, 6.5, K("veh_od"), r=0.3))
        p.append(rbox(T0 @ tr(sx * (WX + 0.3), 12.4, AXLES[1]), 2.4, 0.35, 7.0, K("veh_od"), r=0.3))
    p.append(rbox(T0 @ tr(0, 10.6, 20.0), 6.8, 4.2, 10.0, K("veh_od"), r=1.0))
    p.append(rbox(T0 @ tr(0, 13.8, 30.6) @ rx(-24), 6.4, 4.4, 0.8, K("veh_od"), r=0.5))
    slits = [(lambda q: (q[:, 2] > 0.2) & (np.abs(q[:, 0]) < 5.0) & (np.floor(q[:, 0] / 1.3) % 2 == 0), m0("void"))]
    p.append(rbox(T0 @ tr(0, 9.6, 31.3), 6.0, 3.0, 0.6, K("veh_od"), paint=slits, r=0.4))
    for sx in (1, -1):
        p.append(ell(T0 @ tr(sx * 5.6, 13.0, 31.0), 1.2, 1.2, 1.0, m0("veh_steel"), paint=[(lambda q: q[:, 2] > 0.4, m0("bright"))]))
    team = [(lambda q: (q[:, 2] < -14.5) & (np.abs(q[:, 0]) < 2.2) & (np.abs(q[:, 1] - 1.0) < 1.6), m0("team"))]
    p.append(rbox(T0 @ tr(0, 13.4, -8.0), 8.2, 6.8, 18.0, K("veh_od"), paint=team, r=1.0))
    visor = [(lambda q: (q[:, 2] > 0.0) & (q[:, 1] > 1.0) & (q[:, 1] < 3.6) & (np.abs(np.abs(q[:, 0]) - 3.2) < 1.6), m0("void"))]
    p.append(rbox(T0 @ tr(0, 14.0, 10.2) @ rx(-10), 8.0, 5.6, 0.6, K("veh_od"), paint=visor, r=0.4))
    p.append(rbox(T0 @ tr(0, 20.2, -8.0), 8.0, 0.4, 17.5, K("veh_od2"), r=0.3))
    dteam = [(lambda q: (np.abs(q[:, 0]) > 0.1) & (np.abs(q[:, 1] - 1.0) < 1.4) & (np.abs(q[:, 2]) < 1.6), m0("team"))]
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * 8.4, 12.8, 0.0), 0.35, 4.2, 4.0, K("veh_od2"), paint=dteam, r=0.3))
        for z in np.arange(-24.0, 9.0, 3.6):
            p.append(ell(T0 @ tr(sx * 8.3, 19.0, z), 0.45, 0.45, 0.45, K("veh_od2")))
    p.append(cyl(T0 @ tr(*RING), 5.4, 0.45, K("veh_od2")))
    ov = []
    if wreck:
        p.append(scorch(z=2.0, rx_=17.0, rz_=32.0))
    return p, ov


def turret(kind, frame):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    K = _khaki(m)
    p, ov = [], []
    band = [(lambda q: np.abs(q[:, 1] - 1.0) < 0.7, m("team"))]
    p.append(cyl(tr(0, 2.8, 0), 4.8, 2.8, K("veh_od"), paint=band))
    p.append(ell(tr(0, 5.6, 0), 4.6, 2.0, 4.6, K("veh_od2"), clipy=0.0))
    p.append(cyl(tr(0, 7.4, -0.8), 1.4, 0.5, K("veh_od2")))
    G = tr(0, 3.2, 4.6)
    if wreck:
        G = G @ rx(15)
    p += _maxim(G, m, length=8.0)
    if frame == "fire":
        ov += flash_mg(G, 8.0, strong=True)
    elif frame == "fire_2":
        ov += flash_mg(G, 8.0, strong=False)
    if wreck:
        Kk = rx(8) @ rz(6)
        p = [q.moved(Kk) for q in p]
    return p, ov
