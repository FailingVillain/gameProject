"""US self-propelled howitzer (key: artillery, M7 Priest style) - tank pipeline, no turret.
Run: python tools/vox_artillery.py -> out/artillery/*
Frame 112x112. Rows s, s_se, se, e_se, e, e_ne, ne, n_ne, n (22.5 deg steps, mirrored by the game to 16).
Columns: 0 idle, 1 move_a, 2 move_b, 3 fire (recoil + big flash + smoke), 4 wreck.
Model: x = left, y up, z forward; ground centre of the hull footprint = origin. Howitzer is fixed in the hull, offset right (x<0).
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, composite, save_portrait  # noqa: E402
import voxel as V  # noqa: E402
from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz, make_frame  # noqa: E402
from vox_tank import mk, ROWS, YAW  # noqa: E402
import vox_infantry as inf  # noqa: E402
from voxel import build_figure  # noqa: E402

FRAME = 112
ZOOM = 0.96
AX, AY = 54, 72.0
GUN = (-5.5, 27.5, 9.0)       # howitzer pivot (vehicle right of centre)
ELEVATE = 5.0
MUZZLE_Z = 36.0
KC = 0.56                         # crew drawn smaller than infantry figure scale to match the vehicle scale
KIT = inf.kit("infantry")         # netless M1 helmet, team band + shoulder patches


def sc(q):
    M = q.M.copy()
    M[:3, 3] *= KC
    paint = [((lambda c, f=f: f(c / KC)), mat) for f, mat in q.paint]
    return V.Prim(q.kind, tuple(d * KC for d in q.dims), M, q.mat, paint, None if q.clipy is None else q.clipy * KC, q.tag, q.round)


def crewman(pose, x, y, z, yaw=0.0):
    prims, _ = build_figure(pose, KIT, None)
    T = tr(x, y, z) @ ry(yaw)
    return [sc(q).moved(T) for q in prims]


def crew(col, bob):
    """two crewmen standing in the open fighting compartment (legs hidden below the walls)."""
    y0 = 15.0 + bob
    base = dict(hipL=4, hipR=-3, kneeL=6, kneeR=5, lean=4, spread=5)
    ears = dict(handL=("t", (7.5, 21.5, 2.0)), handR=("t", (-7.5, 21.5, 2.0)), poleL=(0.8, -0.3, -0.6), poleR=(-0.8, -0.3, -0.6))
    if col == 3:      # fire: gunner on the lanyard, commander hands to ears
        g = dict(base, lean=-6, head_pitch=6, handL=("t", (6, 12, -3)), handR=("t", (-4, 11, -3)), poleL=(0.4, -1, -0.5), poleR=(-0.4, -1, -0.5))
        c = dict(base, lean=-6, head_pitch=8, **ears)
    else:
        sway = 1.5 if col == 2 else 0.0
        g = dict(base, lean=6 + sway, handL=("t", (5, 13, 11)), handR=("t", (-4, 13, 11)), poleL=(0.5, -1, 0.1), poleR=(-0.5, -1, 0.1), head_yaw=-8)
        c = dict(base, lean=3 - sway, handL=("t", (2.6, 20.0, 7.0)), handR=("t", (-2.6, 20.0, 7.0)),
                 poleL=(0.9, -0.3, -0.3), poleR=(-0.9, -0.3, -0.3), head_pitch=-4, head_yaw=10)
    return crewman(g, -9.0, y0, -6.0, 0) + crewman(c, 8.0, y0, -5.0, 0)


def tracks(m, phase, wheel_rot, bob):
    T0 = tr(0, bob, 0)
    p = []
    for sx in (1, -1):
        x0 = sx * 21.0
        stripe = [(lambda q, ph=phase: (np.floor((q[:, 2] + ph) / 3.4) % 2 == 0), m("track2"))]
        p.append(rbox(T0 @ tr(x0, 8.2, -0.5), 4.0, 8.2, 40.0, m("track"), paint=stripe, r=4.0))
        xw = sx * 24.6
        for z in (-25.0, -15.0, -5.0, 5.0, 15.0, 25.0):
            wp = [(lambda q, r=wheel_rot: (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) < 2.0) |
                   ((np.cos(3 * (np.arctan2(q[:, 2], q[:, 0]) + r)) > 0.88) & (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) > 2.2)), m("tank2"))]
            p.append(cyl(T0 @ tr(xw, 6.4, z) @ rz(90), 4.6, 1.5, m("rubber"), paint=wp))
        sp = [(lambda q, r=wheel_rot: (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) > 5.3) & (np.cos(9 * (np.arctan2(q[:, 2], q[:, 0]) + r)) > 0.0), m("track2")),
              (lambda q: np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) < 2.2, m("track"))]
        p.append(cyl(T0 @ tr(xw, 9.0, 34.0) @ rz(90), 7.0, 1.8, m("tank2"), paint=sp))
        p.append(cyl(T0 @ tr(xw, 7.4, -34.5) @ rz(90), 6.0, 1.6, m("tank2")))
    return p


def body(phase=0.0, wheel_rot=0.0, bob=0.0, wreck=False, recoil=0.0, droop=0.0):
    m = mk(wreck)
    T0 = tr(0, bob, 0)
    p = tracks(m, phase, wheel_rot, bob)
    # lower hull (Sherman)
    p.append(rbox(T0 @ tr(0, 11.5, 0), 17.4, 6.5, 33.5, m("tankod"), r=2.6))
    p.append(rbox(T0 @ tr(0, 19.6, 24.0) @ rx(38), 18.0, 1.9, 11.0, m("tankod"), r=1.6))             # glacis
    p.append(rbox(T0 @ tr(0, 15.0, 31.0), 16.5, 3.4, 5.0, m("tankod"), r=2.2))                       # nose
    p.append(rbox(T0 @ tr(0, 21.2, -4.0), 19.4, 2.2, 30.0, m("tankod"), r=1.4,
                  paint=[(lambda q: (np.abs(q[:, 0]) > 18.2) & (q[:, 2] > -22) & (q[:, 2] < 10), "team")]))   # deck / sponsons (team stripe)
    p.append(rbox(T0 @ tr(0, 22.2, -29.0), 15.5, 2.4, 6.5, m("tank2"), r=1.4))                       # engine deck
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * 22.2, 18.2, 0), 3.4, 0.9, 34.0, m("tankod"), r=0.8))              # fender
        p.append(rbox(T0 @ tr(sx * 21.5, 19.4, 8.0), 1.1, 0.5, 8.5, m("track2"), r=0.4))
        p.append(rbox(T0 @ tr(sx * 18.5, 19.0, 36.5), 1.6, 1.3, 0.9, m("steel"), r=0.5))
    # open fighting compartment: floor, walls, ammunition racks
    p.append(rbox(T0 @ tr(0, 23.2, -8.0), 15.4, 0.6, 17.0, m("void"), r=0.3))                          # dark floor
    wall_team = [(lambda q: (q[:, 1] > 0.3) & (q[:, 1] < 2.0) & (q[:, 2] > -10) & (q[:, 2] < 8), "team")]
    for sx in (1, -1):
        p.append(rbox(T0 @ tr(sx * 16.4, 28.0, -8.0), 0.9, 5.2, 17.5, m("tankod"), paint=wall_team, r=0.6))
    p.append(rbox(T0 @ tr(0, 27.4, -25.0), 17.2, 4.6, 0.9, m("tankod"), r=0.6))                        # rear wall
    p.append(rbox(T0 @ tr(0, 28.0, 9.5) @ rx(-8), 17.2, 5.4, 1.2, m("tankod"), r=0.6))                 # front plate
    for z in (-22.0, -18.0):                                                                            # ammo racks
        p.append(rbox(T0 @ tr(11.0, 25.6, z), 3.6, 1.8, 1.4, m("canvas"), r=0.6))
        p.append(rbox(T0 @ tr(-11.0, 25.6, z), 3.6, 1.8, 1.4, m("canvas"), r=0.6))
    # pulpit (right front, outside the hull) with ring-mounted .50
    p.append(rbox(T0 @ tr(-19.5, 25.0, 3.0), 4.0, 1.0, 7.0, m("tankod"), r=0.7))                       # pulpit floor / sponson
    p.append(rbox(T0 @ tr(-23.2, 28.6, 3.0), 0.8, 4.0, 6.4, m("tankod"), r=0.5))                       # pulpit outer wall
    p.append(rbox(T0 @ tr(-19.5, 28.6, 9.4), 4.2, 4.0, 0.8, m("tankod"), r=0.5))                       # pulpit front wall
    p.append(cyl(T0 @ tr(-18.5, 33.2, 4.0), 3.0, 0.5, m("steel")))                                      # ring mount
    p.append(rbox(T0 @ tr(-18.5, 34.8, 4.5), 0.9, 1.0, 2.2, m("tank2"), r=0.4))                        # .50 receiver
    if not wreck:
        p.append(along_z(T0 @ tr(-18.5, 35.2, 4.5) @ rx(-6), 0.5, 1.0, 9.0, m("steel")))                # .50 barrel
    # howitzer 105 mm, fixed in the hull (offset right)
    G = T0 @ tr(*GUN) @ rx(-ELEVATE + droop)
    p.append(ell(G @ tr(0, 0, 1.0), 4.8, 4.6, 3.2, m("tankod")))                                        # mantlet
    p.append(rbox(G @ tr(0, 0, -5.0), 3.0, 2.8, 5.0, m("steel"), r=1.2))                               # breech (inside)
    p.append(along_z(G @ tr(0, 0, -recoil), 2.6, 3.0, 15.0, m("tank2")))                                # recoil sleeve
    p.append(along_z(G @ tr(0, 0, -recoil), 1.9, 14.0, MUZZLE_Z - 1.0, m("steel")))                     # tube
    p.append(along_z(G @ tr(0, 0, -recoil), 2.6, MUZZLE_Z - 3.5, MUZZLE_Z + 0.8, m("steel")))           # muzzle brake
    return p


def fire_overlay():
    G = tr(*GUN) @ rx(-ELEVATE)
    zm = MUZZLE_Z - 3.5 + 1.0
    return [ell(G @ tr(0, 1.5, zm + 9.0), 7.4, 6.4, 7.0, "smoke_c"),
            ell(G @ tr(0, 2.0, zm + 7.0), 5.6, 4.8, 5.4, "smoke_b"),
            ell(G @ tr(0, 2.0, zm + 5.0), 3.8, 3.2, 3.8, "smoke_a"),
            ell(G @ tr(0, 0, zm + 3.5), 7.2, 7.2, 9.0, "flash_edge"),
            ell(G @ tr(0, 0, zm + 3.0), 5.4, 5.4, 7.0, "flash"),
            ell(G @ tr(0, 0, zm + 1.8), 3.0, 3.0, 4.0, "flash_core"),
            ell(G @ tr(4.0, 0, zm - 3.5), 3.6, 3.0, 2.4, "flash"),           # side blast from the brake
            ell(G @ tr(-4.0, 0, zm - 3.5), 3.6, 3.0, 2.4, "flash")]


def wreck_smoke():
    out = []
    for i, (x, y, r) in enumerate([(-2.0, 30, 3.2), (-1.0, 35, 3.8), (-3.0, 40, 4.2), (-1.5, 45, 4.0), (-3.0, 50, 3.2)]):
        out.append(ell(tr(x, y, -6.0), r, r * 1.1, r, "smoke_b" if i % 2 else "smoke_c"))
    return out


def frame(row, col):
    yaw = YAW[row]
    if col == 0:
        pr, ov = body() + crew(0, 0.0), []
    elif col == 1:
        pr, ov = body(phase=0.0, wheel_rot=0.0, bob=0.5) + crew(1, 0.5), []
    elif col == 2:
        pr, ov = body(phase=1.7, wheel_rot=0.35, bob=0.7) + crew(2, 0.7), []
    elif col == 3:
        pr, ov = body(recoil=3.5, bob=-0.4) + crew(3, -0.4), fire_overlay()
    else:
        pr, ov = body(wreck=True, droop=12.0), wreck_smoke()
    return make_frame(pr, ov, yaw, fw=FRAME, fh=FRAME, ax=AX, ay=AY, zoom=ZOOM, net=False)


def main():
    d = os.path.join(OUT, "artillery")
    os.makedirs(d, exist_ok=True)
    NC = 5
    base = Image.new("RGBA", (FRAME * NC, FRAME * len(ROWS)), (0, 0, 0, 0))
    team = base.copy()
    edge = []
    for r, row in enumerate(ROWS):
        for c in range(NC):
            f = frame(row, c)
            base.paste(f.base, (c * FRAME, r * FRAME))
            team.paste(f.team, (c * FRAME, r * FRAME))
            a = np.array(f.base)[..., 3]
            if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any():
                edge.append((row, c))
    print("cells touching frame edge:", edge)
    base.save(os.path.join(d, "artillery.png"))
    team.save(os.path.join(d, "artillery_team.png"))
    json.dump({"frame": [FRAME, FRAME], "rows": ROWS, "cols": ["idle", "move_a", "move_b", "fire", "wreck"],
               "ground": [AX, AY]}, open(os.path.join(d, "artillery_anchors.json"), "w"), indent=1)
    bg = Image.new("RGBA", base.size, (78, 110, 64, 255))
    bg.alpha_composite(composite(base, team))
    bg.resize((bg.width * 2, bg.height * 2), Image.NEAREST).save(os.path.join(d, "preview_artillery.png"))
    h = bg.resize((bg.width // 2, bg.height // 2), Image.BOX)
    h.resize((h.width * 3, h.height * 3), Image.NEAREST).save(os.path.join(d, "preview_artillery_half.png"))
    frames = []
    for i in range(16):
        f = make_frame(body() + crew(0, 0.0), [], i * 22.5, fw=FRAME, fh=FRAME, ax=AX, ay=AY, zoom=ZOOM, net=False)
        im = Image.new("RGBA", (FRAME, FRAME), (78, 110, 64, 255))
        im.alpha_composite(composite(f.base, f.team))
        frames.append(im.resize((FRAME * 3, FRAME * 3), Image.NEAREST).convert("P"))
    frames[0].save(os.path.join(d, "artillery_rotate.gif"), save_all=True, append_images=frames[1:], duration=140, loop=0)
    f = make_frame(body() + crew(0, 0.0), [], 35.0, fw=320, fh=320, ax=160, ay=262, zoom=3.3, ss=2, line_thr=5.0, net=False)
    save_portrait("artillery", f.base, f.team)
    print("artillery done")


if __name__ == "__main__":
    main()
