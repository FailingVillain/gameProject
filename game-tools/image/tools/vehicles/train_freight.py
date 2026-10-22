"""Freight boxcar (wood, sliding door, peaked roof). Hull frames: idle, move_1..4, dead. Turret = small roof vent (placeholder
mount so the pipeline has a turret sheet; the game does not draw it for freight). Team colour: door stripe."""
import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, scorch
from vehicles.traincommon import undercarriage, HL

RING = (0.0, 24.0, 0.0)
TURRETS = ("vent",)


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    m = mat_fn(wreck)
    T0 = tr(0, 0.3 if k % 2 else 0.0, 0)
    p = undercarriage(T0, m, wreck, k * 22.5)
    plank = [(lambda q: np.floor(q[:, 1] / 1.6) % 2 == 0, m("veh_od2"))]
    door = [(lambda q: (np.abs(q[:, 2] - 4.0) < 7.5) & (np.abs(q[:, 1]) < 6.2), m("veh_od2")),
            (lambda q: (np.abs(q[:, 2] - 4.0) < 7.5) & (np.abs(q[:, 1] - 1.5) < 0.7), m("team"))]
    for sx in (1, -1):
        if wreck and sx == 1:
            side = [(lambda q: (np.abs(q[:, 2] + 6.0) < 9.0) & (np.abs(q[:, 1]) < 5.0), m("void"))]
        else:
            side = door + plank if sx == 1 else plank
        p.append(rbox(T0 @ tr(sx * 8.4, 12.8, 0), 0.5, 7.0, HL - 1.0, m("veh_wood"), paint=side, r=0.3))
    for sz in (1, -1):
        p.append(rbox(T0 @ tr(0, 12.8, sz * (HL - 1.2)), 8.0, 7.0, 0.5, m("veh_wood"), paint=plank, r=0.3))
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * 4.6, 20.8, 0) @ rz(-sx * 18), 5.6, 0.45, HL, m("veh_od2"), r=0.3))   # peaked roof
    p.append(rbox(T0 @ tr(0, 22.4, 0), 1.0, 0.5, HL - 1.5, m("veh_steel"), r=0.3))
    p.append(cyl(T0 @ tr(*RING), 2.0, 0.3, m("veh_od2")))
    if wreck:
        p.append(scorch(z=0.0, rx_=14.0, rz_=36.0))
    return p, []


def turret(kind, frame):
    m = mat_fn(frame == "dead")
    return [rbox(tr(0, 0.8, 0), 1.6, 0.9, 1.6, m("veh_od2"), r=0.4)], []
