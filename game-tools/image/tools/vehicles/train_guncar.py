"""Armoured gun wagon: sloped armoured box, rotating 76 mm gun turret amidships, MG slits. Hull frames: idle, move_1..4, dead.
Turret (kind 'gun'): idle, fire, fire_2, dead. Team colour: end plates, roof rim stripes, turret band."""
import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, scorch
from vehicles.traincommon import undercarriage, armoured_body, HL

RING = (0.0, 20.4, 0.0)
TURRETS = ("gun",)


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    m = mat_fn(wreck)
    T0 = tr(0, 0.3 if k % 2 else 0.0, 0)
    p = undercarriage(T0, m, wreck, k * 22.5)
    p += armoured_body(T0, m, wreck)
    for sz in (1, -1):
        p.append(rbox(T0 @ tr(0, 18.4, sz * 21.5), 4.6, 0.6, 5.0, m("veh_od2"), r=0.4))           # MG sponson roofs
    p.append(cyl(T0 @ tr(*RING), 6.4, 0.5, m("veh_od2")))
    if wreck:
        p.append(scorch(z=0.0, rx_=14.0, rz_=36.0))
    return p, []


def turret(kind, frame):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    band = [(lambda q: np.abs(q[:, 1] + 0.6) < 0.7, m("team"))]
    p = [cyl(tr(0, 2.8, 0), 5.8, 2.6, m("veh_od"), paint=band), cyl(tr(0, 5.6, 0), 5.2, 0.4, m("veh_od2")),
         cyl(tr(0, 6.4, -1.0), 2.0, 0.8, m("veh_od"))]
    recoil = {"fire": 2.4, "fire_2": 1.0}.get(frame, 0.0)
    G = tr(0, 3.2, 5.2)
    if wreck:
        G = G @ rx(16)
    p.append(ell(G @ tr(0, 0.2, 0.2), 2.6, 2.4, 1.6, m("veh_steel")))
    Gb = G @ tr(0, 0, -recoil)
    p.append(along_z(Gb, 1.0, 0.4, 13.0, m("veh_steel")))
    p.append(along_z(Gb, 1.3, 11.6, 13.2, m("veh_steel")))
    p.append(along_z(Gb @ tr(0, 1.8, 0), 1.2, 0.2, 4.6, m("veh_steel")))
    ov = []
    if frame == "fire":
        zm = 13.0 - recoil
        ov += [ell(G @ tr(0, 0, zm + 2.4), 4.0, 4.0, 5.2, "flash_edge"),
               ell(G @ tr(0, 0, zm + 2.0), 2.9, 2.9, 3.8, "flash"),
               ell(G @ tr(0, 0, zm + 1.3), 1.7, 1.7, 2.3, "flash_core")]
    if wreck:
        K = rx(9) @ rz(-7)
        p = [q.moved(K) for q in p]
    return p, ov
