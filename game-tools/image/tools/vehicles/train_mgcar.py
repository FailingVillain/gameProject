"""Armoured machine-gun wagon: sloped armoured box, two MG cupolas (front + rear) sharing one turret sheet (RINGS).
Hull frames: idle, move_1..4, dead. Turret (kind 'mg'): idle, fire, fire_2, dead. Team colour as the gun car."""
import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, scorch, mg_prims, flash_mg
from vehicles.traincommon import undercarriage, armoured_body, HL

RING = (0.0, 20.4, 14.0)
RINGS = [(0.0, 20.4, 14.0), (0.0, 20.4, -14.0)]
TURRETS = ("mg",)


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    m = mat_fn(wreck)
    T0 = tr(0, 0.3 if k % 2 else 0.0, 0)
    p = undercarriage(T0, m, wreck, k * 22.5)
    p += armoured_body(T0, m, wreck, hz=HL - 2.0)
    for r in RINGS:
        p.append(cyl(T0 @ tr(*r), 5.0, 0.5, m("veh_od2")))
    if wreck:
        p.append(scorch(z=0.0, rx_=14.0, rz_=34.0))
    return p, []


def turret(kind, frame):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    band = [(lambda q: np.abs(q[:, 1] - 0.4) < 0.6, m("team")),
            (lambda q: (np.abs(q[:, 1] - 2.0) < 0.4) & (q[:, 2] > 2.0) & (np.abs(q[:, 0]) < 1.4), m("void"))]
    p = [cyl(tr(0, 1.8, 0), 3.8, 1.8, m("veh_od"), paint=band), cyl(tr(0, 3.8, 0), 3.6, 0.3, m("veh_od2"))]
    G = tr(0, 2.2, 3.8)
    if wreck:
        G = G @ rx(14)
    p += mg_prims(G, m)
    ov = []
    if frame == "fire":
        ov += flash_mg(G, 9.5, strong=True)
    elif frame == "fire_2":
        ov += flash_mg(G, 9.5, strong=False)
    if wreck:
        K = rx(8) @ rz(-6)
        p = [q.moved(K) for q in p]
    return p, ov
