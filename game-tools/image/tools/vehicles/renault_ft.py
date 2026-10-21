"""Renault FT style WWI light tank (generic, no insignia). ~14 model units per metre.

Hull (origin = ground centre of the track footprint): narrow hull between two tall track assemblies, big spoked
front idler, long sloped top run with track guards, prow-like driver's front with visor plates, engine deck sloping
back, tail skid. Turret (origin = ring centre RING): round turret with a mushroom cupola; "mg" = Hotchkiss machine
gun in a ball mount, "37mm" = short 37 mm cannon with a recoil housing above the barrel.
Hull frames: idle, move_1..move_4 (track stripes + idler/sprocket rotation + bob), dead (burnt wreck, thrown track,
engine hatch blown open). Turret frames: idle, fire (flash; recoil for the cannon), fire_2 (second flash /
recoil return), dead (burnt, knocked askew, cupola cap blown off).
Team colour: turret band + a small plate on the driver's visor plate (S) and on the rear plate (N).
"""
import math

import numpy as np

from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz
from vehicles.vehcommon import mat_fn, rad, ang, mg_prims, flash_mg, scorch

RING = (0.0, 18.4, 1.5)         # turret ring centre in hull coords
TX = 9.9                        # track centre line |x|
IDLER = (10.5, 21.0, 7.0)       # (y, z, r) front idler
SPROCKET = (5.9, -21.0, 4.6)    # rear drive sprocket
RUN_ANG = math.degrees(math.atan2(IDLER[0] + IDLER[2] - (SPROCKET[0] + SPROCKET[2]), IDLER[1] - SPROCKET[1]))  # top run slope
TURRETS = ("mg", "37mm")


def _track(sx, phase, rot, m, wreck, T0, thrown):
    p = []
    r = math.radians(rot)
    stripe = [(lambda q, ph=phase: (np.floor((q[:, 2] + ph) / 3.4) % 2 == 0), m("veh_track2"))]
    # lower run
    Tl = T0 @ tr(sx * TX, 2.8, 0)
    if thrown:
        Tl = tr(sx * 3.2, -0.9, -4.0) @ Tl @ ry(sx * 7) @ rz(-sx * 6)
    p.append(rbox(Tl, 2.4, 1.5, 21.0, m("veh_track"), paint=stripe, r=1.0))
    # idler (front, big, spoked)
    teeth = [(lambda q: rad(q) > 5.4, m("veh_track")),
             (lambda q, r=r: (rad(q) > 5.4) & (np.cos(8 * (ang(q) + r)) > 0.15), m("veh_track2")),
             (lambda q: rad(q) <= 5.4, m("veh_od2")),
             (lambda q, r=r: (rad(q) <= 5.4) & (rad(q) > 1.7) & (np.abs(np.cos(3 * (ang(q) + r))) > 0.9), m("veh_od")),
             (lambda q: rad(q) <= 1.7, m("veh_steel"))]
    Ti = T0 @ tr(sx * TX, IDLER[0], IDLER[1])
    if thrown:
        Ti = Ti @ rz(sx * 14) @ tr(sx * 1.5, -0.5, 0)
    p.append(cyl(Ti @ rz(90), IDLER[2], 2.3, m("veh_track2"), paint=teeth))
    # sprocket (rear)
    sp = [(lambda q: rad(q) > 3.2, m("veh_track")),
          (lambda q, r=r: (rad(q) > 3.2) & (np.cos(8 * (ang(q) + r)) > 0.1), m("veh_track2")),
          (lambda q: rad(q) <= 3.2, m("veh_od2")),
          (lambda q: rad(q) <= 1.3, m("veh_steel"))]
    p.append(cyl(T0 @ tr(sx * TX, SPROCKET[0], SPROCKET[1]) @ rz(90), SPROCKET[2], 2.3, m("veh_track2"), paint=sp))
    # top run (sloping from the idler top down to the sprocket top)
    y_top = (IDLER[0] + IDLER[2] + SPROCKET[0] + SPROCKET[2]) / 2
    hz = math.hypot(IDLER[1] - SPROCKET[1], IDLER[0] + IDLER[2] - SPROCKET[0] - SPROCKET[2]) / 2
    Tu = T0 @ tr(sx * TX, y_top, 0) @ rx(-RUN_ANG)
    if thrown:
        Tu = tr(sx * 0.8, -1.8, 0) @ Tu @ rz(-sx * 5)
    p.append(rbox(Tu, 2.4, 1.1, hz, m("veh_track"), paint=stripe, r=0.9))
    # bogie frame + leaf spring between the runs
    p.append(rbox(T0 @ tr(sx * TX, 6.6, -3.0), 1.9, 2.4, 15.0, m("veh_od2"), r=1.0))
    p.append(rbox(T0 @ tr(sx * TX, 9.7, -2.0), 1.3, 0.6, 11.0, m("veh_steel"), r=0.4))
    for z in (-15.0, -8.0, -1.0, 6.0, 12.0):
        p.append(cyl(T0 @ tr(sx * (TX + 1.0), 4.0, z) @ rz(90), 1.6, 1.0, m("veh_od2")))          # road wheel hubs
    # track guard over the top run (+ short front mudguard over the idler)
    Tg = T0 @ tr(sx * (TX + 0.4), y_top + 2.0, 0) @ rx(-RUN_ANG)
    if wreck and thrown:
        Tg = Tg @ rz(-sx * 9) @ tr(0, -0.6, 0)
    p.append(rbox(Tg, 3.0, 0.45, hz + 0.5, m("veh_od"), r=0.4))
    p.append(rbox(T0 @ tr(sx * (TX + 0.4), IDLER[0] + IDLER[2] + 1.0, IDLER[1] + 2.5) @ rx(22), 3.0, 0.45, 3.6, m("veh_od"), r=0.4))
    # tool box on the guard
    p.append(rbox(Tg @ tr(0, 1.4, -9.0), 2.2, 1.0, 3.2, m("veh_od2"), r=0.6))
    return p


def hull(frame):
    """frame: idle | move_1..move_4 | dead -> (prims, overlay)"""
    wreck = frame == "dead"
    k = int(frame.split("_")[1]) - 1 if frame.startswith("move_") else 0
    phase = k * 0.85                        # stripe period 3.4 -> 4 steps per link
    rot = k * 11.25                         # 8-tooth pattern period 45 deg
    bob = 0.35 if k % 2 == 1 else 0.0
    sag = -0.6 if wreck else 0.0
    m = mat_fn(wreck)
    T0 = tr(0, bob + sag, 0)
    p = []
    for sx in (1, -1):
        p += _track(sx, phase, rot, m, wreck, T0, thrown=(wreck and sx == 1))
    # ---- hull body
    p.append(rbox(T0 @ tr(0, 11.0, -1.5), 7.3, 6.5, 19.5, m("veh_od"), r=1.2))                     # main box  z -21..18
    p.append(rbox(T0 @ tr(0, 17.9, 1.5), 7.3, 0.6, 9.5, m("veh_od"), r=0.6))                       # roof plate z -8..11
    hood_ang = math.degrees(math.atan2(2.5, 7.0))
    p.append(rbox(T0 @ tr(0, 17.25, 14.5) @ rx(hood_ang), 6.8, 0.7, 3.7, m("veh_od"), r=0.5))     # driver's hood (slopes down fwd)
    visor_paint = [(lambda q: (q[:, 2] > 0.9) & (np.abs(q[:, 0]) < 2.3) & (np.abs(q[:, 1] + 0.3) < 1.3), m("team")),
                   (lambda q: (q[:, 2] > 0.9) & (np.abs(np.abs(q[:, 0]) - 4.3) < 1.3) & (np.abs(q[:, 1] - 1.3) < 0.35), m("void"))]
    p.append(rbox(T0 @ tr(0, 13.8, 19.2) @ rx(-12), 6.8, 2.4, 1.2, m("veh_od"), paint=visor_paint, r=0.5))   # visor plate
    p.append(rbox(T0 @ tr(0, 8.0, 20.0) @ rx(18), 7.0, 3.8, 1.6, m("veh_od"), r=0.8))              # lower nose (prow)
    # engine deck sloping back
    deck_ang = math.degrees(math.atan2(4.0, 13.5))
    louv = [(lambda q: (q[:, 1] > 0.5) & (np.abs(q[:, 0]) < 5.2) & (np.abs(q[:, 2] + 0.5) < 4.5) & (np.floor(q[:, 2] / 1.5) % 2 == 0), m("veh_od"))]
    if wreck:
        p.append(rbox(T0 @ tr(0, 15.5, -14.75) @ rx(-deck_ang), 7.3, 0.8, 7.0, m("veh_od2"), r=0.5,
                      paint=[(lambda q: (q[:, 1] > 0.5) & (np.abs(q[:, 0]) < 4.0) & (np.abs(q[:, 2] + 1.0) < 3.0), "void")]))
        p.append(rbox(T0 @ tr(0, 17.0, -10.5) @ rx(-70), 4.0, 0.4, 3.0, m("veh_od2"), r=0.3))      # hatch blown open
    else:
        p.append(rbox(T0 @ tr(0, 15.5, -14.75) @ rx(-deck_ang), 7.3, 0.8, 7.0, m("veh_od2"), paint=louv, r=0.5))
    # rear plate with grille + team plate
    rear_paint = [(lambda q: (q[:, 2] < -0.5) & (np.abs(q[:, 0]) < 2.3) & (np.abs(q[:, 1] + 2.6) < 1.3), m("team"))]
    p.append(rbox(T0 @ tr(0, 9.5, -21.3), 7.2, 4.8, 0.7, m("veh_od"), paint=rear_paint, r=0.6))
    grille = [(lambda q: (q[:, 2] < -0.2) & (np.floor(q[:, 1] / 0.9) % 2 == 0), m("veh_od2"))]
    p.append(rbox(T0 @ tr(0, 12.4, -22.0), 4.5, 1.7, 0.4, m("veh_steel"), paint=grille, r=0.3))
    # tail skid: two rails, cross bar, skid plate
    skid_ang = math.degrees(math.atan2(3.9, 11.7))
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * 4.6, 4.55, -27.15) @ rx(-skid_ang), 0.8, 0.9, 6.2, m("veh_od2"), r=0.5))
    p.append(cyl(T0 @ tr(0, 3.6, -30.5) @ rz(90), 0.8, 5.0, m("veh_od2")))
    p.append(rbox(T0 @ tr(0, 2.5, -32.6) @ rx(-skid_ang), 5.6, 0.6, 2.6, m("veh_od"), r=0.5))
    # turret ring (part of the hull sheet so the hull reads complete without a turret)
    p.append(cyl(T0 @ tr(*RING), 7.6, 0.5, m("veh_od2")))
    ov = []
    if wreck:
        p.append(scorch(z=-4.0, rx_=19.0, rz_=27.0))
    return p, ov


def turret(kind, frame):
    """kind: mg | 37mm; frame: idle | fire | fire_2 | dead -> (prims, overlay). Origin = ring centre."""
    wreck = frame == "dead"
    m = mat_fn(wreck)
    p, ov = [], []
    band = [(lambda q: np.abs(q[:, 1] + 1.3) < 0.9, m("team")),
            (lambda q: (np.abs(q[:, 1] - 1.7) < 0.32) & (q[:, 2] > 4.0) & ((np.abs(q[:, 0]) < 0.9) | (np.abs(np.abs(q[:, 0]) - 4.5) < 0.9)), m("void"))]
    p.append(cyl(tr(0, 4.6, 0), 7.0, 4.0, m("veh_od"), paint=band))                         # turret drum y 0.6..8.6
    p.append(cyl(tr(0, 9.0, 0), 6.4, 0.5, m("veh_od2")))                                     # top plate (taper step)
    p.append(cyl(tr(0, 10.6, -0.8), 3.3, 1.2, m("veh_od")))                                  # cupola
    cap_T = tr(0, 12.2, -0.8)
    if wreck:
        cap_T = tr(4.2, 10.6, -4.0) @ rz(40) @ rx(15)
    p.append(ell(cap_T, 4.5, 1.2, 4.5, m("veh_od2"), clipy=-0.4))                            # mushroom cap
    for sx in (1, -1):                                                                        # rear hatch doors
        Td = tr(sx * 2.0, 4.0, -6.8) @ ry(sx * 15)
        if wreck and sx == 1:
            Td = tr(sx * 3.9, 4.0, -6.6) @ ry(sx * 85)
        p.append(rbox(Td, 1.8, 2.8, 0.6, m("veh_od2"), r=0.4))
    p.append(rbox(tr(0, 3.8, 7.0), 2.8, 2.4, 1.4, m("veh_od2"), r=0.6))                       # gun mount block
    if kind == "mg":
        G = tr(0, 3.9, 7.6)
        if wreck:
            G = G @ rx(14)
        p += mg_prims(G, m)
        if frame == "fire":
            ov += flash_mg(G, 9.6, strong=True)
        elif frame == "fire_2":
            ov += flash_mg(G, 9.6, strong=False)
    else:
        recoil = {"fire": 2.4, "fire_2": 1.0}.get(frame, 0.0)
        G = tr(0, 3.6, 7.6)
        if wreck:
            G = G @ rx(16)
        p.append(ell(G @ tr(0, 0.6, 0.2), 2.7, 2.7, 1.6, m("veh_steel")))                   # mantlet
        Gb = G @ tr(0, 0, -recoil)
        p.append(along_z(Gb, 1.05, 0.4, 11.4, m("veh_steel")))                               # 37 mm barrel
        p.append(along_z(Gb, 1.3, 10.2, 11.6, m("veh_steel")))                               # muzzle ring
        p.append(along_z(Gb @ tr(0, 1.9, 0), 1.25, 0.2, 4.4, m("veh_steel")))               # recoil housing above
        if frame == "fire":
            zm = 11.4 - recoil
            ov += [ell(G @ tr(0, 0, zm + 2.4), 4.0, 4.0, 5.2, "flash_edge"),
                   ell(G @ tr(0, 0, zm + 2.0), 2.9, 2.9, 3.8, "flash"),
                   ell(G @ tr(0, 0, zm + 1.3), 1.7, 1.7, 2.3, "flash_core")]
    if wreck:
        K = rx(9) @ rz(-7)                                                                    # knocked askew about the ring centre
        p = [q.moved(K) for q in p]
    return p, ov
