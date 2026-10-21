"""US M2A1 105 mm towed howitzer + 3-man crew (key: towed_artillery) - vehicle-style sheet.
Run: python tools/vox_towed.py -> out/towed_artillery/*
Frame 112x112, rows s, s_se, se, e_se, e, e_ne, ne, n_ne, n (22.5 deg; the game mirrors to 16).
Columns: 0 travel_a, 1 travel_b, 2 deployed idle, 3 fire, 4 wreck, 5 deploying.
Model: x = left, y up, z = firing direction; origin = ground point under the axle centre.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, composite, save_portrait  # noqa: E402
import voxel as V  # noqa: E402
from voxel import ell, cyl, rbox, along_z, tr, rx, ry, rz, seg, make_frame, build_figure, death_pose  # noqa: E402
import vox_tank  # noqa: E402
from vox_tank import mk, ROWS, YAW  # noqa: E402
import vox_infantry as inf  # noqa: E402
from poses import walk_legs  # noqa: E402

vox_tank.WRECK_MAP.update({"olive": "burn", "rubber": "burn_t"})

FRAME = 112
ZOOM = 0.52
K = 1.5                              # gun is drawn K x larger than figure scale (crew would swamp it otherwise)


def sc(p):
    """scale a prim by K about the origin (dims, translation, paint thresholds)."""
    M = p.M.copy()
    M[:3, 3] *= K
    paint = [((lambda q, f=f: f(q / K)), mat) for f, mat in p.paint]
    return V.Prim(p.kind, tuple(d * K for d in p.dims), M, p.mat, paint,
                  None if p.clipy is None else p.clipy * K, p.tag, p.round)


def scl(lst):
    return [sc(q) for q in lst]
AX, AY = 58, 74.0
KIT = inf.kit("infantry")          # netless M1 helmet, team band + shoulder patches


# ------------------------------------------------------------------ gun
def wheel(m, x, rot):
    pat = [(lambda q, r=rot: (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) < 7.6) &
            ~((np.cos(5 * (np.arctan2(q[:, 2], q[:, 0]) + r)) > 0.8) & (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) > 2.6)), m("olive")),
           (lambda q, r=rot: (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) < 7.6) &
            (np.cos(5 * (np.arctan2(q[:, 2], q[:, 0]) + r)) > 0.8) & (np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) > 2.6), m("steel"))]
    return cyl(tr(x, 11.0, 0) @ rz(90), 11.0, 2.2, m("rubber"), paint=pat)


def gun_prims(open_=0.0, elev=-2.0, rot=0.0, recoil=0.0, wreck=False, bob=0.0):
    """open_ 0 = trails closed, 1 = spread."""
    m = mk(wreck)
    T0 = tr(0, bob, 0)
    p = []
    for sx in (1, -1):
        p.append(wheel(m, sx * 13.5, rot).moved(T0))
        p.append(cyl(T0 @ tr(sx * 16.0, 11.0, 0) @ rz(90), 3.0, 0.6, m("steel")))              # hub cap
    p.append(cyl(T0 @ tr(0, 11.0, 0) @ rz(90), 1.6, 12.5, m("steel")))                          # axle
    p.append(rbox(T0 @ tr(0, 14.0, -1.0), 6.0, 3.6, 7.0, m("olive"), r=1.4))                   # carriage
    # split trails
    xe = 2.6 + open_ * 21.0
    ze = 52.0 - open_ * 6.0
    for sx in (1, -1):
        p.append(seg((T0 @ np.array([sx * 4.0, 9.5, -6.0, 1.0]))[:3], (T0 @ np.array([sx * xe, 2.4, -ze, 1.0]))[:3],
                     2.5, m("olive"), r1=1.7))
        p.append(rbox(T0 @ tr(sx * xe, 1.4, -ze - 1.0) @ ry(-sx * open_ * 24), 2.6, 1.4, 2.4, m("steel"), r=0.6))   # spade
    # gun shield (team stripe on the top edge)
    stripe = [(lambda q: q[:, 1] > 5.2, "team")] if not wreck else []
    p.append(rbox(T0 @ tr(0, 23.0, 7.0) @ rx(-8), 12.5, 7.4, 0.6, m("olive"), paint=stripe, r=0.4))
    p.append(rbox(T0 @ tr(0, 14.5, 6.0) @ rx(-8), 9.0, 2.6, 0.5, m("olive"), r=0.3))             # lower flap
    # tube + recoil system (pivot y=17)
    B = T0 @ tr(0, 17.5, 3.0) @ rx(-elev)
    p.append(rbox(B @ tr(0, 0, -5.0), 2.6, 2.6, 4.0, m("steel"), r=1.0))                         # breech
    p.append(along_z(B @ tr(0, 0, -recoil), 2.8, -1.0, 17.0, m("olive")))                         # recoil sleeve
    p.append(along_z(B @ tr(0, 0, -recoil), 1.8, 16.0, 45.0, m("steel")))                         # tube
    p.append(along_z(B @ tr(0, 0, -recoil), 2.2, 43.5, 46.5, m("steel")))                         # muzzle
    p.append(rbox(T0 @ tr(-8.2, 18.0, 1.0), 1.6, 2.4, 2.2, m("steel"), r=0.6))                    # sight / elevating gear
    return p, B


def fire_overlay(B, recoil):
    z = 46.5 - recoil
    return [ell(B @ tr(0, 2.5, z + 12.0), 8.0, 7.0, 7.6, "smoke_c"),
            ell(B @ tr(0, 3.0, z + 9.0), 6.0, 5.2, 5.8, "smoke_b"),
            ell(B @ tr(0, 2.5, z + 6.0), 4.2, 3.6, 4.2, "smoke_a"),
            ell(B @ tr(0, 0, z + 4.2), 8.0, 8.0, 10.0, "flash_edge"),
            ell(B @ tr(0, 0, z + 3.6), 5.8, 5.8, 7.6, "flash"),
            ell(B @ tr(0, 0, z + 2.2), 3.2, 3.2, 4.2, "flash_core"),
            ell(B @ tr(5.0, 0, z - 1.0), 4.0, 3.2, 3.0, "flash"),
            ell(B @ tr(-5.0, 0, z - 1.0), 4.0, 3.2, 3.0, "flash")]


# ------------------------------------------------------------------ crew
def crew(pose, x, z, yaw=0.0):
    prims, _ = build_figure(pose, KIT, None)
    T = tr(x * K, 0, z * K) @ ry(yaw)
    return [q.moved(T) for q in prims]


def stand(**kw):
    d = dict(hipL=4, hipR=-3, kneeL=6, kneeR=5, lean=3, spread=5, head_pitch=0)
    d.update(kw)
    return d


def walk(i, handsL=None, handsR=None):
    L = walk_legs(i)
    d = dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"] * 0.5, lean=8, head_pitch=-4, spread=5)
    if handsL:
        d["handL"] = ("t", handsL)
        d["poleL"] = (0.6, -1, 0.2)
    if handsR:
        d["handR"] = ("t", handsR)
        d["poleR"] = (-0.6, -1, 0.2)
    return d


def crew_travel(ph):
    a, b = (1, 3) if ph == 0 else (3, 1)
    out = []
    out += crew(walk(a, handsL=(2, 14, 17), handsR=(-9, 17, 17)), 22.0, 6.0, 0)                      # left crewman pushes the shield (hands to his right)
    out += crew(walk(b, handsL=(9, 17, 17), handsR=(-2, 14, 17)), -22.0, 6.0, 0)
    out += crew(walk(a, handsL=(5, 14, 14), handsR=(-5, 14, 14)), 0.0, -60.0, 0)                      # third man at the trail ends
    return out


def crew_deployed(flinch=False, lanyard=False):
    ears = ("t", (7.5, 21.5, 2.0)), ("t", (-7.5, 21.5, 2.0))
    out = []
    if flinch:
        out += crew(stand(lean=-8, head_pitch=7, handL=("t", (6, 12, -3)), handR=("t", (-4, 11, -3)), poleL=(0.4, -1, -0.5), poleR=(-0.4, -1, -0.5)),
                    -19.0, -14.0, 0)                                                              # gunner pulls the lanyard
        out += crew(stand(lean=-6, head_pitch=8, handL=ears[0], handR=ears[1], poleL=(0.8, -0.3, -0.6), poleR=(-0.8, -0.3, -0.6)), 0.0, -22.0, 0)
        out += crew(stand(lean=-6, head_pitch=8, handL=ears[0], handR=ears[1], poleL=(0.8, -0.3, -0.6), poleR=(-0.8, -0.3, -0.6)), -33.0, -24.0, 40)
    else:
        out += crew(stand(handL=("t", (6, 12, 12)), handR=("t", (-4, 12, 12)), poleL=(0.5, -1, 0.1), poleR=(-0.5, -1, 0.1), head_yaw=-8),
                    -19.0, -14.0, 0)                                                              # gunner at the sight
        out += crew(stand(lean=10, handL=("t", (4, 9, 12)), handR=("t", (-4, 9, 12)), poleL=(0.4, -1, 0.1), poleR=(-0.4, -1, 0.1)), 0.0, -22.0, 0)   # loader with a round
        out += crew(stand(handL=("t", (2.8, 20.0, 7.0)), handR=("t", (-2.8, 20.0, 7.0)), poleL=(0.9, -0.3, -0.3), poleR=(-0.9, -0.3, -0.3), head_pitch=-4),
                    -33.0, -24.0, 40)                                                             # commander with binoculars
    return out


def crew_deploying():
    out = []
    out += crew(walk(1, handsL=(5, 10, 12), handsR=(-5, 10, 12)), 16.0, -62.0, -10)
    out += crew(walk(3, handsL=(5, 10, 12), handsR=(-5, 10, 12)), -16.0, -62.0, 10)
    out += crew(stand(lean=6, handL=("t", (6, 12, 12)), handR=("t", (-4, 12, 12)), poleL=(0.5, -1, 0.1), poleR=(-0.5, -1, 0.1)), -19.0, -12.0, 0)
    return out


def ammo(m):
    """shell box + loose rounds beside the trail."""
    p = [rbox(tr(22.0, 2.0, -18.0), 3.2, 2.0, 4.4, m("canvas"), r=0.6)]
    for i in range(3):
        p.append(along_z(tr(15.0 + i * 2.4, 1.3, -18.0), 1.2, -3.0, 3.5, "capcase") if False else along_z(tr(15.0 + i * 2.4, 1.3, -18.0), 1.1, -3.0, 3.5, m("canvas")))
    return p


# ------------------------------------------------------------------ frames
def scene(col):
    pr, ov = scene0(col)
    return pr, ov


def scene0(col):
    """-> (prims, overlay)"""
    ov = []
    if col in (0, 1):
        g, B = gun_prims(0.0, -2.0, rot=0.0 if col == 0 else 0.55, bob=0.0 if col == 0 else 0.6)
        return scl(g) + crew_travel(col), ov
    if col == 2:
        g, B = gun_prims(1.0, 18.0)
        return scl(g + ammo(mk(False))) + crew_deployed(), ov
    if col == 3:
        g, B = gun_prims(1.0, 18.0, recoil=4.5, bob=0.0)
        g = [q.moved(tr(0, 0, -1.2)) for q in g]
        B2 = tr(0, 0, -1.2) @ B
        return scl(g + ammo(mk(False))) + crew_deployed(flinch=True), scl(fire_overlay(B2, 4.5))
    if col == 5:
        g, B = gun_prims(0.5, 8.0, rot=0.3)
        return scl(g) + crew_deploying(), ov
    # wreck: gun tipped over onto one wheel, one crewman slumped
    g, B = gun_prims(0.55, 4.0, wreck=True)
    G = tr(13.5, 0, 0) @ rz(-68) @ tr(-13.5, 0, 0) @ ry(12)
    g = scl([q.moved(G) for q in g])
    ymin = min(V._prim_ymin(q) for q in g)
    g = [q.moved(tr(0, -ymin, 0)) for q in g]
    dead, _ = build_figure(death_pose("dead", {}, {}, {}), KIT, None)
    dead = [q.moved(tr(-30.0 * K, 0, -22.0 * K) @ ry(50)) for q in dead]
    smoke = [ell(tr(x, y, z), r, r * 1.1, r, "smoke_b" if i % 2 else "smoke_c")
             for i, (x, y, z, r) in enumerate([(12, 20, 0, 4.2), (13, 27, 0, 4.8), (12, 35, 0, 5.2), (14, 43, 0, 5.0), (13, 51, 0, 4.2)])]
    return g + dead, smoke


def frame(row, col):
    pr, ov = scene(col)
    return make_frame(pr, ov, YAW[row], fw=FRAME, fh=FRAME, ax=AX, ay=AY, zoom=ZOOM, net=False)


def main():
    d = os.path.join(OUT, "towed_artillery")
    os.makedirs(d, exist_ok=True)
    NC = 6
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
    base.save(os.path.join(d, "towed_artillery.png"))
    team.save(os.path.join(d, "towed_artillery_team.png"))
    json.dump({"frame": [FRAME, FRAME], "rows": ROWS,
               "cols": ["travel_a", "travel_b", "idle", "fire", "wreck", "deploy"], "ground": [AX, AY]},
              open(os.path.join(d, "towed_artillery_anchors.json"), "w"), indent=1)
    bg = Image.new("RGBA", base.size, (78, 110, 64, 255))
    bg.alpha_composite(composite(base, team))
    bg.resize((bg.width * 2, bg.height * 2), Image.NEAREST).save(os.path.join(d, "preview_towed_artillery.png"))
    h = bg.resize((bg.width // 2, bg.height // 2), Image.BOX)
    h.resize((h.width * 3, h.height * 3), Image.NEAREST).save(os.path.join(d, "preview_towed_artillery_half.png"))
    # portrait: gun commander (head and shoulders)
    prims, _ = build_figure(stand(handL=("t", (2.8, 20.0, 7.0)), handR=("t", (-2.8, 20.0, 7.0)), poleL=(0.9, -0.3, -0.3), poleR=(-0.9, -0.3, -0.3)), KIT, None)
    f = make_frame(prims, [], 20.0, fw=320, fh=320, ax=160, ay=372, zoom=6.0, ss=2, line_thr=5.0, net=False)
    save_portrait("towed_artillery", f.base, f.team)
    print("towed done")


if __name__ == "__main__":
    main()
