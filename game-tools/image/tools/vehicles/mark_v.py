"""Mark V heavy tank, male (WWI rhomboid; two sponson 6-pdr guns). Compressed for the RTS (hull ~64 model units long).

Hull (origin = ground centre): hexagonal rhomboid track loop on both sides (bottom run, rising nose, top run, falling
tail), flat hull body between the tracks, two side sponsons with short 6-pounder barrels, roof unditching beam, exhaust
box, rear steering-tail-less. Turret sheet = fixed roof cab / cupola (the Mark V has no turret: the game keeps it aligned
with the hull) - it also carries the sponson muzzle flashes (frames fire / fire_2) so the hull sheet stays static.
Hull frames: idle, move_1..move_4 (track stripe phase), dead. Turret frames: idle, fire, fire_2, dead.
Paint: khaki-olive (veh_od). Team colour: roof cab band + sponson plates + rear plate.
"""
import math

import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, flash_mg, scorch

RING = (0.0, 23.0, 9.0)
TURRETS = ("cupola",)
TX = 9.6
LOOP = [(-31.0, 12.5), (-19.0, 2.6), (19.0, 2.6), (31.0, 12.5), (21.0, 23.0), (-21.0, 23.0)]   # (z, y) closed loop
SPON_X = 12.6
TURRET_YAW_AWARE = True
NEAR_SIGN = -1          # which sponson (x sign) faces the camera when the model is seen from the side (yaw 90)
SPON_Y = 10.0
SPON_Z = 3.5
MUZZLE_Z = 19.0


def _track(sx, phase, m, T0, thrown):
    p = []
    stripe = [(lambda q, ph=phase: (np.floor((q[:, 2] + ph) / 3.0) % 2 == 0), m("veh_track2"))]
    n = len(LOOP)
    for i in range(n):
        (z0, y0), (z1, y1) = LOOP[i], LOOP[(i + 1) % n]
        zc, yc = (z0 + z1) / 2, (y0 + y1) / 2
        L = math.hypot(z1 - z0, y1 - y0) / 2
        ang = math.degrees(math.atan2(y1 - y0, z1 - z0))
        T = T0 @ tr(sx * TX, yc, zc) @ rx(-ang)
        if thrown:
            T = tr(sx * 2.0, -0.8, -3.0) @ T @ rz(-sx * 5)
        p.append(rbox(T, 2.3, 1.25, L + 0.6, m("veh_track"), paint=stripe, r=0.9))
        Tc = T0 @ tr(sx * TX, y0, z0) @ rz(90)
        p.append(cyl(Tc, 1.7, 2.3, m("veh_track2")))
    return p


def hull(frame):
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    phase = k * 0.75
    bob = 0.3 if k % 2 == 1 else 0.0
    sag = -0.7 if wreck else 0.0
    m = mat_fn(wreck)
    T0 = tr(0, bob + sag, 0)
    p = []
    for sx in (1, -1):
        p += _track(sx, phase, m, T0, thrown=(wreck and sx == 1))
    # hull body between the tracks + roof + nose / tail plates
    p.append(rbox(T0 @ tr(0, 12.5, 0), 8.4, 8.2, 24.0, m("veh_od"), r=1.2))
    p.append(rbox(T0 @ tr(0, 21.2, 0), 8.8, 0.9, 22.0, m("veh_od2"), r=0.6))
    p.append(rbox(T0 @ tr(0, 11.0, 24.3) @ rx(-14), 7.8, 7.0, 0.8, m("veh_od"), r=0.6))
    p.append(rbox(T0 @ tr(0, 11.0, -24.3) @ rx(14), 7.8, 7.0, 0.8, m("veh_od"), r=0.6))
    rear = [(lambda q: (q[:, 2] < -0.2) & (np.abs(q[:, 0]) < 2.4) & (np.abs(q[:, 1]) < 1.2), m("team"))]
    p.append(rbox(T0 @ tr(0, 8.5, -25.0), 3.4, 2.0, 0.4, m("veh_od2"), paint=rear, r=0.3))
    # unditching beam over the top run, exhaust box
    p.append(rbox(T0 @ tr(0, 25.3, -6.0), 1.5, 1.4, 21.0, m("veh_od2"), r=0.6))
    p.append(rbox(T0 @ tr(0, 24.3, -14.0), 3.4, 1.4, 3.0, m("veh_steel"), r=0.5))
    # sponsons with gun mantlets and short 6-pdr barrels
    for sx in (1, -1):
        sp_team = [(lambda q, s=sx: (q[:, 0] * s > 0.5) & (np.abs(q[:, 1] - 0.5) < 1.2) & (np.abs(q[:, 2]) < 2.5), m("team"))]
        p.append(rbox(T0 @ tr(sx * SPON_X, SPON_Y, SPON_Z), 4.2, 5.6, 8.5, m("veh_od"), paint=sp_team, r=1.0))
        p.append(rbox(T0 @ tr(sx * SPON_X, SPON_Y + 5.9, SPON_Z), 4.0, 0.4, 8.2, m("veh_od2"), r=0.3))
        P = T0 @ tr(sx * SPON_X, SPON_Y + 0.5, SPON_Z + 8.5)
        p.append(ell(P @ tr(0, 0, 0.2), 2.3, 2.3, 1.6, m("veh_steel")))
        p.append(along_z(P, 1.05, 0.2, MUZZLE_Z - SPON_Z - 8.5, m("veh_steel")))
        p.append(along_z(P, 1.35, MUZZLE_Z - SPON_Z - 9.6, MUZZLE_Z - SPON_Z - 8.5, m("veh_steel")))
        p.append(ell(T0 @ tr(sx * (SPON_X + 4.2), SPON_Y - 3.0, SPON_Z - 1.0), 0.4, 0.4, 0.4, m("veh_od2")))   # rivet
    ov = []
    if wreck:
        p.append(scorch(z=0.0, rx_=14.0, rz_=34.0))
    return p, ov


def turret(kind, frame, yaw=None):
    wreck = frame == "dead"
    m = mat_fn(wreck)
    p, ov = [], []
    band = [(lambda q: np.abs(q[:, 1] - 0.6) < 0.5, m("team")),
            (lambda q: (np.abs(q[:, 1] - 2.2) < 0.4) & (q[:, 2] > 3.0) & (np.abs(q[:, 0]) < 3.0), m("void"))]
    p.append(rbox(tr(0, 1.7, 0), 4.6, 1.7, 5.4, m("veh_od"), paint=band, r=0.8))             # roof cab (driver / commander)
    p.append(rbox(tr(0, 3.5, 0.4), 4.2, 0.3, 5.0, m("veh_od2"), r=0.3))
    p.append(cyl(tr(0, 4.1, -1.8), 1.5, 0.4, m("veh_od2")))                                   # hatch
    p.append(cyl(tr(0, 4.6, -1.8), 0.9, 0.2, m("veh_steel")))
    for sx in (1, -1):                                                                          # driver's visors
        p.append(rbox(tr(sx * 2.2, 2.0, 5.5), 1.0, 0.5, 0.3, m("void"), r=0.2))
    if wreck:
        K = rx(6) @ rz(-5)
        p = [q.moved(K) for q in p]
    elif frame in ("fire", "fire_2"):
        k = 1.0 if frame == "fire" else 0.6
        for sx in (1, -1):
            if yaw is not None and abs(math.sin(math.radians(yaw))) > 0.35 and sx != NEAR_SIGN * (1 if math.sin(math.radians(yaw)) > 0 else -1):
                continue                       # far-side sponson hidden behind the hull: no flash (the turret sheet has no depth for it)
            G = tr(sx * SPON_X, SPON_Y + 0.5 - RING[1], MUZZLE_Z - RING[2])
            ov += [ell(G @ tr(0, 0, 1.6 * k), 2.6 * k, 2.6 * k, 3.2 * k, "flash_edge"),
                   ell(G @ tr(0, 0, 1.3 * k), 1.9 * k, 1.9 * k, 2.4 * k, "flash"),
                   ell(G @ tr(0, 0, 0.8 * k), 1.1 * k, 1.1 * k, 1.5 * k, "flash_core")]
    return p, ov
