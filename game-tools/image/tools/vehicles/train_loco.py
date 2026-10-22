"""Armoured steam locomotive blockout (1920 Russian/Chinese/Japanese armoured-train look): plated boiler, armoured cab at the
rear, stack + dome, cowcatcher. Hull frames: idle, move_1..4, dead. Turret (cab lookout cupola): idle, fire, fire_2, dead.
Team colour: cab band + buffer-beam plate."""
import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, scorch
from vehicles.traincommon import undercarriage, HL, WX

RING = (0.0, 25.5, -17.0)
TURRETS = ("cupola",)


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    m = mat_fn(wreck)
    T0 = tr(0, 0.3 if k % 2 else 0.0, 0)
    p = undercarriage(T0, m, wreck, k * 22.5)
    for sx in (1, -1):
        for z in (4.0, 12.0):
            p.append(cyl(T0 @ tr(sx * 8.9, 4.6, z) @ rz(90), 4.4, 0.6, m("veh_steel"),
                         paint=[(lambda q: np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) < 3.2, m("veh_track2"))]))
        p.append(rbox(T0 @ tr(sx * 9.6, 5.0, 8.0), 0.3, 0.5, 5.2, m("veh_rust"), r=0.2))        # side rod
    B = T0
    p.append(along_z(B @ tr(0, 13.2, 0), 6.6, -2.0, 28.5, m("veh_od"),
                     paint=[(lambda q: (np.floor(q[:, 2] / 6.0) % 2 == 0) & (q[:, 1] > 0), m("veh_od2"))]))
    for z in (4.0, 12.0, 20.0):
        p.append(cyl(B @ tr(0, 13.2, z) @ rx(90), 7.0, 0.45, m("veh_steel")))                     # boiler bands
    p.append(cyl(B @ tr(0, 20.5, 21.0), 2.0, 3.2, m("veh_steel")))                                 # smokestack
    p.append(cyl(B @ tr(0, 24.0, 21.0), 2.9, 0.5, m("veh_steel")))
    p.append(ell(B @ tr(0, 19.6, 8.0), 3.2, 2.0, 3.2, m("veh_khaki2")))                            # dome
    beam = [(lambda q: (q[:, 2] > 0) & (np.abs(q[:, 0]) < 2.4) & (np.abs(q[:, 1]) < 1.0), m("team"))]
    p.append(rbox(B @ tr(0, 8.0, 30.0), 8.6, 2.2, 1.0, m("veh_od"), paint=beam, r=0.4))            # buffer beam
    p.append(rbox(B @ tr(0, 6.2, 33.6) @ rx(-30), 6.4, 0.5, 3.2, m("veh_od2"), r=0.3))             # cowcatcher
    cab_team = [(lambda q: (np.abs(q[:, 1] - 2.0) < 1.2) & (np.abs(q[:, 0]) > 8.0), m("team"))]
    p.append(rbox(B @ tr(0, 15.0, -17.0), 8.6, 9.0, 12.0, m("veh_od"), paint=cab_team, r=0.9))
    slit = [(lambda q: (q[:, 2] > 0.1) & (np.abs(q[:, 1] - 1.0) < 0.7) & (np.floor(q[:, 0] / 2.0) % 2 == 0), m("void"))]
    p.append(rbox(B @ tr(0, 17.0, -5.0), 7.8, 3.0, 0.5, m("veh_steel"), paint=slit, r=0.3))
    p.append(rbox(B @ tr(0, 24.4, -17.0), 9.0, 0.6, 12.4, m("veh_od2"), r=0.4))                    # cab roof
    p.append(cyl(B @ tr(0, 24.9, -17.0), 4.8, 0.4, m("veh_od2")))
    if wreck:
        p.append(scorch(z=0.0, rx_=14.0, rz_=36.0))
    return p, []


def turret(kind, frame):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    band = [(lambda q: np.abs(q[:, 1] - 1.0) < 0.7, m("team")),
            (lambda q: (np.abs(q[:, 1] - 2.4) < 0.4) & (q[:, 2] > 2.0) & (np.abs(q[:, 0]) < 1.6), m("void"))]
    p = [cyl(tr(0, 1.8, 0), 3.8, 1.8, m("veh_od"), paint=band), cyl(tr(0, 3.8, 0), 4.0, 0.3, m("veh_od2"))]
    if wreck:
        K = rx(10) @ rz(-8)
        p = [q.moved(K) for q in p]
    return p, []
