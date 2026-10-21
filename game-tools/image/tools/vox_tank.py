"""US medium tank (key: tank, M4 Sherman style) - hull sheet + separate turret sheet, 16 directions via 9 rows + mirror.
Run: python tools/vox_tank.py -> out/tank/*
Frame 112x112. Rows (yaw from the camera): s, s_se, se, e_se, e, e_ne, ne, n_ne, n  (22.5 deg steps).
Hull columns: 0 idle, 1 move_a, 2 move_b, 3 wreck.  Turret columns: 0 idle, 1 fire, 2 wreck.
Model: x = left, y up, z forward, origin = ground centre of the hull footprint. Turret model origin = turret ring centre.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, composite, save_portrait, P  # noqa: E402
import voxel as V  # noqa: E402
from voxel import (Prim, ell, cyl, cap, rbox, along_z, tr, rx, ry, rz, make_frame)  # noqa: E402

FRAME = 112
ZOOM = 1.1
ROWS = ["s", "s_se", "se", "e_se", "e", "e_ne", "ne", "n_ne", "n"]
YAW = {n: i * 22.5 for i, n in enumerate(ROWS)}
RING = (0.0, 24.0, -3.0)                 # turret ring centre in hull model coords
AX, AY_TURRET = 56, 47.0                 # turret pivot in the turret frame (and mount for the hull row if centred)
GROUND_AY = AY_TURRET + RING[1] * math.cos(V.ELEV) * ZOOM   # hull ground anchor y (so a yaw-0 ring sits at AY_TURRET)

WRECK_MAP = {"tankod": "burn", "tank2": "burn", "steel": "burn_s", "track": "burn_t", "track2": "burn_t2",
             "rubber": "burn_t", "wood": "char", "canvas": "burn_t2"}


def mk(wreck):
    def f(m):
        return WRECK_MAP.get(m, m) if wreck else m
    return f


# ------------------------------------------------------------------ hull
def hull_prims(phase=0.0, wheel_rot=0.0, wreck=False, bob=0.0):
    m = mk(wreck)
    T0 = tr(0, bob, 0)
    p = []
    side_mark = [(lambda q: (np.abs(q[:, 0]) > 18.4) & (q[:, 1] > 0.2) & (q[:, 1] < 2.6) & (q[:, 2] > -22) & (q[:, 2] < 14), "team")]
    p.append(rbox(T0 @ tr(0, 11.5, 0), 17.4, 6.5, 33.5, m("tankod"), r=2.6))      # lower hull
    p.append(rbox(T0 @ tr(0, 20.4, -6), 19.2, 4.0, 26.0, m("tankod"), paint=side_mark, r=1.6))                       # upper hull / sponsons
    p.append(rbox(T0 @ tr(0, 19.6, 24.0) @ rx(38), 18.0, 1.9, 11.0, m("tankod"), r=1.6))            # sloped glacis
    p.append(rbox(T0 @ tr(0, 15.0, 31.0), 16.5, 3.4, 5.0, m("tankod"), r=2.2))                       # transmission nose
    p.append(rbox(T0 @ tr(0, 22.6, -24.0), 15.5, 2.4, 8.0, m("tank2"), r=1.6))                       # engine deck
    p.append(rbox(T0 @ tr(0, 24.7, -24.5), 8.5, 0.7, 5.2, m("steel"), r=0.6))                        # grille
    p.append(cyl(T0 @ tr(*RING), 12.8, 1.0, m("tank2")))                                              # turret ring
    # hatches
    for sx in (1, -1):
        if wreck:
            p.append(rbox(T0 @ tr(sx * 8.5, 27.2, 12.0) @ rx(-70) @ tr(0, 0, 0), 3.2, 0.7, 3.2, m("tankod"), r=0.5))
            p.append(rbox(T0 @ tr(sx * 8.5, 24.6, 15.0), 3.0, 0.5, 2.6, "void", r=0.4))
        else:
            p.append(rbox(T0 @ tr(sx * 8.5, 24.7, 15.0), 3.3, 0.9, 3.3, m("tank2"), r=0.8))
    p.append(ell(T0 @ tr(-10.0, 21.2, 26.5), 1.7, 1.7, 1.7, m("steel")))                             # bow MG ball
    p.append(along_z(T0 @ tr(-10.0, 21.2, 27.0), 0.5, 0, 4.0, m("steel")))
    # fenders / stowage
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * 22.2, 18.2, 0), 3.4, 0.9, 34.0, m("tankod"), r=0.8))              # fender
        p.append(rbox(T0 @ tr(sx * 22.0, 21.0, -17.0), 3.0, 2.2, 6.5, m("tank2"), r=1.4))             # stowage bin
        p.append(rbox(T0 @ tr(sx * 21.5, 19.4, 8.0), 1.1, 0.5, 8.5, m("track2"), r=0.4))             # tool (shovel/axe)
        p.append(rbox(T0 @ tr(sx * 18.5, 19.0, 36.5), 1.6, 1.3, 0.9, m("steel"), r=0.5))             # headlight guard
    p.append(cyl(T0 @ tr(0, 27.2, -29.0) @ rz(90), 2.3, 9.5, m("canvas")))                           # tarp roll
    p.append(rbox(T0 @ tr(-7.0, 25.2, -12.0), 4.0, 1.2, 3.0, m("tank2"), r=0.9))                     # tool box
    # tracks + suspension
    for sx in (1, -1):
        x0 = sx * 21.0
        stripe = [(lambda q, ph=phase: (np.floor((q[:, 2] + ph) / 3.4) % 2 == 0), m("track2"))]
        p.append(rbox(T0 @ tr(x0, 8.2, -0.5), 4.0, 8.2, 40.0, m("track"), paint=stripe, r=4.0))
        xw = sx * 24.6
        for z in (-25.0, -15.0, -5.0, 5.0, 15.0, 25.0):
            wp = [(lambda q, r=wheel_rot: (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) < 2.0) |
                   ((np.cos(3 * (np.arctan2(q[:, 2], q[:, 0]) + r)) > 0.88) & (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) > 2.2)), m("tank2"))]
            p.append(cyl(T0 @ tr(xw, 6.4, z) @ rz(90), 4.6, 1.5, m("rubber"), paint=wp))
        # drive sprocket (front) and idler (rear)
        sp = [(lambda q, r=wheel_rot: (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) > 5.3) & (np.cos(9 * (np.arctan2(q[:, 2], q[:, 0]) + r)) > 0.0), m("track2")),
              (lambda q: np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) < 2.2, m("track"))]
        p.append(cyl(T0 @ tr(xw, 9.0, 34.0) @ rz(90), 7.0, 1.8, m("tank2"), paint=sp))
        p.append(cyl(T0 @ tr(xw, 7.4, -34.5) @ rz(90), 6.0, 1.6, m("tank2")))
    return p


# ------------------------------------------------------------------ turret (origin = ring centre)
def turret_prims(recoil=0.0, wreck=False, droop=0.0):
    m = mk(wreck)
    p = []
    band = [(lambda q: (q[:, 1] > -2.0) & (q[:, 1] < 0.6), "team")]
    p.append(ell(tr(0, 4.2, 0.0), 13.0, 7.6, 14.4, m("tankod"), clipy=-4.6, paint=band))            # cast turret
    p.append(ell(tr(0, 3.4, -11.5), 9.8, 5.2, 6.2, m("tankod"), clipy=-3.8))                          # bustle
    p.append(rbox(tr(0, 4.4, -16.8), 7.5, 3.2, 1.6, m("tank2"), r=1.0))                               # rear stowage
    for sx in (1, -1):
        p.append(rbox(tr(sx * 9.0, 4.0, -15.5), 2.2, 2.0, 2.4, m("tank2"), r=1.0))
    # gun mount + barrel
    G = tr(0, 3.9, 12.8) @ rx(droop)
    p.append(ell(G @ tr(0, 0, -0.5), 6.0, 5.6, 3.8, m("tankod")))                                       # mantlet
    p.append(along_z(G @ tr(0, 0, -recoil), 1.75, 2.0, 24.0, m("steel")))                               # 75 mm gun
    p.append(along_z(G @ tr(0, 0, -recoil), 2.15, 22.0, 24.6, m("steel")))                              # muzzle
    p.append(along_z(G @ tr(3.6, -0.6, 0), 0.6, 1.0, 5.0, m("steel")))                                  # coax MG
    # hatches
    p.append(cyl(tr(-5.0, 10.9, -4.5), 3.6, 1.1, m("tank2")))                                          # commander cupola
    if wreck:
        p.append(rbox(tr(5.0, 12.8, -3.0) @ rx(-65), 3.4, 0.7, 3.2, m("tankod"), r=0.5))               # loader hatch open
        p.append(ell(tr(5.0, 10.4, -1.2), 3.0, 0.5, 3.0, "void"))
        p.append(ell(tr(-5.0, 11.8, -4.5), 2.3, 0.4, 2.3, "void"))
    else:
        p.append(rbox(tr(5.2, 11.0, -1.5), 3.4, 0.9, 3.2, m("tank2"), r=0.8))
        p.append(along_z(tr(-5.0, 12.6, -4.5), 0.55, 0.0, 5.0, m("steel")))                           # AA MG
    ant0 = tr(-9.0, 7.0, -15.0)
    if not wreck:
        p.append(Prim("cap", (0.45, 10.0), tr(-9.0, 17.0, -15.0), m("steel")))                        # radio antenna
    return p


def turret_overlay(kind):
    if kind != "fire":
        return []
    z = 3.9 + 0.0
    zm = 12.8 + 24.6 - 3.0 + 1.5
    return [ell(tr(0, 5.4, zm + 8.0), 6.4, 5.6, 6.0, "smoke_c"),
            ell(tr(0, 5.8, zm + 7.0), 4.8, 4.2, 4.6, "smoke_b"),
            ell(tr(0, 6.0, zm + 6.0), 3.0, 2.7, 3.0, "smoke_a"),
            ell(tr(0, 3.9, zm + 1.8), 5.6, 5.6, 8.0, "flash_edge"),
            ell(tr(0, 3.9, zm + 1.6), 4.0, 4.0, 6.0, "flash"),
            ell(tr(0, 3.9, zm + 0.8), 2.2, 2.2, 3.2, "flash_core")]


def turret_wreck_smoke():
    out = []
    for i, (x, y, r) in enumerate([(5.0, 14.5, 2.4), (6.5, 18.0, 3.0), (5.2, 22.0, 3.4), (7.0, 26.0, 3.2), (6.0, 29.5, 2.6)]):
        out.append(ell(tr(x, y, -1.5), r, r * 1.1, r, "smoke_b" if i % 2 else "smoke_c"))
    return out


# ------------------------------------------------------------------ frames
def hull_frame(row, col):
    yaw = YAW[row]
    if col == 0:
        pr = hull_prims()
    elif col == 1:
        pr = hull_prims(phase=0.0, wheel_rot=0.0, bob=0.0)
    elif col == 2:
        pr = hull_prims(phase=1.7, wheel_rot=0.35, bob=0.7)
    else:
        pr = hull_prims(wreck=True)
    if col == 1:
        pr = hull_prims(phase=0.0, wheel_rot=0.0, bob=0.5)
    f = make_frame(pr, [], yaw, fw=FRAME, fh=FRAME, ax=AX, ay=GROUND_AY, zoom=ZOOM, yoff=0.0)
    return f


def mount_point(row):
    yaw = V.ry(YAW[row])
    w = (yaw @ np.array([RING[0], RING[1], RING[2], 1.0]))[:3]
    return [round(AX + w[0] * ZOOM, 2), round(GROUND_AY + (-(w @ V.UP)) * ZOOM, 2)]


def turret_frame(row, col):
    yaw = YAW[row]
    if col == 0:
        pr, ov = turret_prims(), []
    elif col == 1:
        pr, ov = turret_prims(recoil=3.0), turret_overlay("fire")
    else:
        pr, ov = turret_prims(wreck=True, droop=15.0), turret_wreck_smoke()
    return make_frame(pr, ov, yaw, fw=FRAME, fh=FRAME, ax=AX, ay=AY_TURRET, zoom=ZOOM, yoff=RING[1] + 0.0)


def sheet(fn, ncols):
    base = Image.new("RGBA", (FRAME * ncols, FRAME * len(ROWS)), (0, 0, 0, 0))
    team = Image.new("RGBA", base.size, (0, 0, 0, 0))
    cells = {}
    for r, row in enumerate(ROWS):
        for c in range(ncols):
            f = fn(row, c)
            cells[(r, c)] = f
            base.paste(f.base, (c * FRAME, r * FRAME)); team.paste(f.team, (c * FRAME, r * FRAME))
    return base, team, cells


def composite_cell(hb, ht, tb, tt, color=(70, 140, 235)):
    out = composite(hb, ht, color)
    out.alpha_composite(composite(tb, tt, color))
    return out


def main():
    d = os.path.join(OUT, "tank")
    os.makedirs(d, exist_ok=True)
    hb, ht, hcells = sheet(hull_frame, 4)
    tb, tt, tcells = sheet(turret_frame, 3)
    hb.save(os.path.join(d, "tank.png")); ht.save(os.path.join(d, "tank_team.png"))
    tb.save(os.path.join(d, "tank_turret.png")); tt.save(os.path.join(d, "tank_turret_team.png"))
    anchors = {"frame": [FRAME, FRAME], "rows": ROWS,
               "hull": {row: {"ground": [AX, round(GROUND_AY, 2)], "mount": mount_point(row)} for row in ROWS},
               "turret": {row: {"pivot": [AX, AY_TURRET]} for row in ROWS}}
    json.dump(anchors, open(os.path.join(d, "tank_anchors.json"), "w"), indent=1)
    bg = (78, 110, 64, 255)

    def comp(r, hc, tc, scale):
        h = hcells[(r, hc)]; t = tcells[(r, tc)]
        im = Image.new("RGBA", (FRAME, FRAME), bg)
        im.alpha_composite(composite_cell(h.base, h.team, t.base, t.team))
        return im
    # 4x preview: rows x [idle, move_a, move_b, fire, wreck]
    combos = [(0, 0), (1, 0), (2, 0), (0, 1), (3, 2)]
    pv = Image.new("RGBA", (FRAME * len(combos), FRAME * len(ROWS)), bg)
    for r in range(len(ROWS)):
        for k, (hc, tc) in enumerate(combos):
            pv.paste(comp(r, hc, tc, 1), (k * FRAME, r * FRAME))
    pv.resize((pv.width * 2, pv.height * 2), Image.NEAREST).save(os.path.join(d, "preview_tank.png"))
    half = pv.resize((pv.width // 2, pv.height // 2), Image.BOX)
    half.resize((half.width * 3, half.height * 3), Image.NEAREST).save(os.path.join(d, "preview_tank_half.png"))
    # 16-direction rotation GIF (render 16 yaws, turret follows hull)
    frames = []
    for i in range(16):
        yaw = i * 22.5
        pr = hull_prims(); tp = turret_prims()
        h = make_frame(pr, [], yaw, fw=FRAME, fh=FRAME, ax=AX, ay=GROUND_AY, zoom=ZOOM)
        w = (V.ry(yaw) @ np.array([RING[0], RING[1], RING[2], 1.0]))[:3]
        mx, my = AX + w[0] * ZOOM, GROUND_AY + (-(w @ V.UP)) * ZOOM
        t = make_frame(tp, [], yaw, fw=FRAME, fh=FRAME, ax=mx, ay=my, zoom=ZOOM, yoff=RING[1])
        im = Image.new("RGBA", (FRAME, FRAME), bg)
        im.alpha_composite(composite_cell(h.base, h.team, t.base, t.team))
        frames.append(im.resize((FRAME * 3, FRAME * 3), Image.NEAREST).convert("P"))
    frames[0].save(os.path.join(d, "tank_rotate.gif"), save_all=True, append_images=frames[1:], duration=140, loop=0)
    # portrait 320x320, 3/4 view
    portrait()
    print("tank done")


def portrait():
    S = 3.3
    yaw = 35.0
    hp = hull_prims(); tp = [q.moved(tr(*RING)) for q in turret_prims()]
    allp = hp + tp
    # team layer needs everything in one frame
    f = make_frame(allp, [], yaw, fw=320, fh=320, ax=160, ay=262, zoom=S, ss=2, line_thr=5.0)
    base = Image.new("RGBA", (320, 320), (30, 34, 28, 255))
    base.alpha_composite(f.base)
    # keep a transparent version for the portrait (dark background is baked into preview only)
    save_portrait("tank", f.base, f.team)


if __name__ == "__main__":
    main()
