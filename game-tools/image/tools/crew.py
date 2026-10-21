"""Two-man crew-served weapon units rendered as ONE sprite (units/<name>.json with "kind": "crew").

heavy-maxim: Maxim machine gun on a Sokolov-like wheeled carriage + gunner (sits behind the gun) + loader (kneels beside it).
Both men use the infantry body rig (voxel.build_figure) with the same WWI kit as infantry-default; the gun is built from prims here.

Crew model space = crew ground frame: x = left of the gun, y up, z = muzzle direction, y = 0 ground. Figures are built in their own
figure space (origin on the ground between the feet) and placed with P = tr(x, 0, z) @ ry(yaw); hands are IK-targeted at world points
(spade grips, belt, box) by converting the world target into the figure's torso frame (voxel.LAST['T'] = figure -> final transform).

Frame: CREW_FRAME main (192x192), ground anchor (96, 128) (centre x, 2/3 down), LODs 96x96 / 48x48 with the same pixel scale ratios
as the infantry (2 / 1 / 0.5) and the same model zoom, so the men are exactly as big as infantry-default.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import BASE_FW, BASE_FH, DIRS  # noqa: E402
import voxel as V  # noqa: E402
from voxel import (Res, Kit, build_figure, render_prims, tr, rx, ry, rz, box, ell, cyl, seg, along_z, pt)  # noqa: E402
from poses import walk_legs  # noqa: E402
from parts.heavy_weapons import _flash  # noqa: E402

CREW_FRAME = (192, 192)
CREW_AY_FRAC = 2 / 3
_K = 1.5 if __import__("os").environ.get("KD_RES96") == "1" else 1.0   # res96: LOD x1.5
LOD_PXS = [2.0 * _K, 1.0 * _K, 0.5 * _K]


class CrewRes(Res):
    """frame decoupled from the pixel scale (pxs drives zoom / outline / AO exactly like the infantry; frame = CREW_FRAME * pxs / 2)."""

    def __init__(self, pxs):
        super().__init__(int(BASE_FW * pxs), int(BASE_FH * pxs))
        self.fw = int(round(CREW_FRAME[0] * pxs / 2))
        self.fh = int(round(CREW_FRAME[1] * pxs / 2))
        self.ax = self.fw // 2
        self.ay = int(round(self.fh * CREW_AY_FRAC))


CREW_LODS = [CrewRes(p) for p in LOD_PXS]

GY = 11.0               # bore height above the ground
AXLE = (0.0, 7.0, -1.0)
WHEEL_R = 7.0
WHEEL_X = 7.4
GRIP_Z = -13.6
RECOIL = 1.4
LOADER_LEAN = 60
TRIPOD_X, TRIPOD_Z = 6.5, 6.0
GUN = {"kind": "maxim"}     # set by CrewUnit from spec["gun"]: "maxim" (default) | "type3" (Hotchkiss-style air-cooled gun on a tripod)


# ---------------------------------------------------------------- the gun
def _circle_pts(c, r, n, off):
    return [c + np.array([0.0, r * math.cos(off + 2 * math.pi * i / n), r * math.sin(off + 2 * math.pi * i / n)]) for i in range(n)]


def wheel(C, side, rot):
    """spoked wheel in plane x = side * WHEEL_X (frame C = carriage frame)."""
    c = np.array([side * WHEEL_X, AXLE[1], AXLE[2]])
    pts = [pt(C, p) for p in _circle_pts(c, WHEEL_R - 0.6, 14, rot)]
    out = [seg(pts[i], pts[(i + 1) % 14], 0.75, "wood") for i in range(14)]
    hub = pt(C, c)
    for i in range(7):
        a = rot + 2 * math.pi * i / 7
        out.append(seg(hub, pt(C, c + np.array([0.0, (WHEEL_R - 0.8) * math.cos(a), (WHEEL_R - 0.8) * math.sin(a)])), 0.4, "wood"))
    out.append(cyl(C @ tr(c[0] + side * 0.3, c[1], c[2]) @ rz(90), 1.5, 0.9, "steel"))
    out.append(cyl(C @ tr(c[0] + side * 0.5, c[1], c[2]) @ rz(90), 0.7, 1.0, "rifmetal"))
    return out


def gun_model(rec=0.0, wheel_rot=0.0, trail_lift=0.0, roll=0.0, flash=0.0, belt=None, wreck=False, box_at=None, hose_end=None, with_box=True):
    """-> (prims, overlay prims, info) ; info = {'feed': feed point, 'box': box centre}.
    rec: recoil slide (model units, gun slides back on the cradle); roll: carriage tipped over the right wheel (deg, wrecked);
    belt: optional list of world points the belt runs through after the feed block (default: drapes to the ammo box)."""
    pv = np.array([-WHEEL_X - 0.8, 0.0, 0.0])
    C = tr(*pv) @ rz(roll) @ tr(*(-pv)) if roll else np.eye(4)
    G = C @ tr(0, GY, -rec)
    prims = []
    t3 = GUN["kind"] == "type3"
    if t3:                                                                                     # tripod instead of the wheeled carriage
        top = pt(C, (0, 8.6, -1.5))
        for fx, fz in ((TRIPOD_X, TRIPOD_Z), (-TRIPOD_X, TRIPOD_Z), (0.0, -11.0)):
            foot = pt(C, (fx, 0.4 + (trail_lift if fz < 0 else 0.0), fz))
            prims.append(seg(top, foot, 0.9, "olive"))
            prims.append(box(tr(*foot), 0.9, 0.35, 0.9, "steel"))
        prims.append(cyl(C @ tr(0, 8.0, -1.5), 1.3, 1.2, "steel"))                              # pintle
        prims.append(box(C @ tr(0, 8.6, -1.5), 2.2, 0.9, 3.0, "olive"))                          # cradle
    else:
        for s in (1, -1):
            prims += wheel(C, s, wheel_rot if s > 0 else -wheel_rot)
        prims.append(seg(pt(C, (-WHEEL_X - 0.4, AXLE[1], AXLE[2])), pt(C, (WHEEL_X + 0.4, AXLE[1], AXLE[2])), 0.75, "steel"))
        prims.append(box(C @ tr(0, 8.6, -1.5), 2.8, 1.0, 6.0, "olive"))                           # cradle
        prims.append(box(C @ tr(0, 7.6, 3.5), 1.2, 1.0, 2.2, "olive"))
        prims.append(seg(pt(C, (0, 8.0, -5.5)), pt(C, (0, 1.0 + trail_lift, -13.0)), 1.0, "olive"))   # trail
        prims.append(box(C @ tr(0, 0.5 + trail_lift, -13.6), 2.8, 0.4, 1.5, "steel"))               # spade
    # gun
    if t3:
        prims += [box(G @ tr(0, 0, -5.0), 2.3, 2.8, 5.5, "steel"),                             # receiver
                  box(G @ tr(0, 0.3, -10.8), 2.0, 2.4, 0.5, "rifmetal"),                       # back plate
                  box(G @ tr(0, 3.2, -4.0), 0.5, 0.6, 0.6, "steel"),                           # rear sight
                  box(G @ tr(3.3, 0.6, -3.5), 1.3, 1.6, 2.4, "rifmetal"),                      # feed block (left side)
                  along_z(G, 0.9, 0.3, 24.0, "steel")]                                          # barrel
        for k in range(8):                                                                      # cooling fins
            z0 = 5.0 + k * 2.1
            prims.append(along_z(G, 2.3, z0, z0 + 0.9, "rifmetal"))
        prims += [along_z(G, 1.3, 22.0, 26.0, "rifmetal"),                                      # flash hider
                  box(G @ tr(0, 0, 25.6), 0.4, 1.4, 0.4, "steel"),
                  box(G @ tr(7.1, 0.6, -3.5), 3.0, 0.3, 1.2, "brass"),                          # 30-round feed strip entering from the side
                  box(G @ tr(7.1, 0.9, -3.5), 2.8, 0.2, 0.4, "steel")]
        for k in range(4):
            prims.append(ell(G @ tr(5.0 + k * 1.5, 1.4, -3.5), 0.45, 0.45, 0.45, "brass"))
    else:
      prims += [box(G @ tr(0, 0, -5.0), 2.5, 3.0, 5.5, "steel"),                               # receiver
              box(G @ tr(0, 0.3, -10.8), 2.2, 2.6, 0.5, "rifmetal"),                         # back plate
              box(G @ tr(0, 3.3, -4.0), 0.5, 0.6, 0.6, "steel"),                             # rear sight
              box(G @ tr(3.3, 0.6, -3.5), 1.3, 1.6, 2.4, "rifmetal"),                        # feed block
              along_z(G, 2.5, 0.5, 19.0, "steel"),                                            # water jacket
              along_z(G, 2.7, 18.4, 19.4, "rifmetal"),
              along_z(G, 2.7, 0.3, 1.0, "rifmetal"),
              cyl(G @ tr(0, 2.8, 15.0), 1.1, 0.5, "rifmetal"),                               # filler cap
              along_z(G, 1.6, 19.4, 23.0, "rifmetal"),                                        # muzzle booster
              along_z(G, 0.9, 23.0, 26.0, "steel"),
              box(G @ tr(0, 0, 25.6), 0.4, 1.4, 0.4, "steel")]
    for s in (1, -1):                                                                          # spade grips + thumb bar
        prims.append(seg(pt(G, (s * 1.8, 1.0, -11.1)), pt(G, (s * 2.4, 3.6, GRIP_Z)), 0.75, "wood"))
    prims.append(box(G @ tr(0, 3.7, -13.9), 2.2, 0.4, 0.4, "steel"))
    if not t3:
        # steam hose to the condenser can
        can = pt(C, (7.0, 3.0, 14.0)) if not wreck else np.array([-14.0, 0.8, 10.0])
        if hose_end is not None:
            can = hose_end
        start = pt(G, (2.3, 0.6, 19.0))
        prims.append(seg(start, pt(C, (3.5, 4.0, 17.0)) if not wreck else start + np.array([-2.0, -4.0, 3.0]), 0.5, "rifmetal"))
        if not wreck:
            prims.append(seg(pt(C, (3.5, 4.0, 17.0)), can + np.array([0, 1.4, 0]), 0.5, "rifmetal"))
        prims.append(cyl(tr(*can), 1.6, 1.6, "steel") if not wreck else cyl(tr(*can) @ rz(80), 1.6, 1.6, "steel"))
    feed = pt(G, (4.7, 0.9, -3.5))
    info = {"feed": feed}
    # ammo box + belt
    if box_at is None:
        bc = np.array([11.6, 2.5, -3.0])
    else:
        bc = np.asarray(box_at, float)
    info["box"] = bc
    if with_box and wreck:
        prims.append(box(tr(*bc) @ rz(72) @ ry(30), 2.2, 2.5, 3.4, "olive"))
        prims.append(box(tr(*bc) @ rz(72) @ ry(30) @ tr(0, 2.7, 0), 2.3, 0.35, 3.5, "steel"))
    elif with_box:
        prims.append(box(tr(*bc), 2.2, 2.5, 3.4, "olive"))
        prims.append(box(tr(*bc) @ tr(0, 2.7, 0), 2.35, 0.35, 3.5, "steel"))
        prims.append(box(tr(*bc) @ tr(0, 3.0, 0), 0.4, 0.5, 1.4, "steel"))
    if not t3:
        # belt: canvas strap + brass rounds along a curve through `pts`
        pts = [feed] + (belt if belt else [feed + np.array([2.5, 1.0, -0.4]), np.array([bc[0] - 0.5, bc[1] + 4.2, bc[2] - 0.5]), bc + np.array([0, 3.2, 0.0])])
        for a, b in zip(pts[:-1], pts[1:]):
            prims.append(seg(a, b, 0.55, "canvas"))
        path = [np.array(p, float) for p in pts]
        lens = [np.linalg.norm(path[i + 1] - path[i]) for i in range(len(path) - 1)]
        total = sum(lens)
        n = max(3, int(total / 1.7))
        for k in range(n):
            t = (k / n) * total
            acc = 0.0
            for i, L in enumerate(lens):
                if t <= acc + L or i == len(lens) - 1:
                    f = (t - acc) / max(L, 1e-6)
                    q = path[i] * (1 - f) + path[i + 1] * f
                    prims.append(ell(tr(*(q + np.array([0, 0.45, 0]))), 0.55, 0.55, 0.55, "brass"))
                    break
                acc += L
    ov = []
    if flash:
        ov = _flash(G, 26.0, flash, y=0.0)
    return prims, ov, info


# ---------------------------------------------------------------- the men
def make_kits(spec_gear, headgear="feldkappe", uniform="ww1_khaki", badge=None):
    import unit_loader as UL
    from parts import badges
    from parts import packs
    hg = UL.HEADGEAR[headgear]
    mats = dict(UL.UNIFORMS[uniform]) if isinstance(uniform, str) else dict(UL.UNIFORMS["us_od"], **uniform)
    hfn = badges.with_badge(hg["fn"], badge, headgear)
    out = {}
    for who, gear in spec_gear.items():
        g = [packs.GEAR[x] for x in gear]
        out[who] = Kit(**mats, headgear=hfn, belt_on=True, belt_pouches=False, team_band=True, team_patch=False,
                       back=(lambda T, g=g: [p for f in g for p in f(T)]) if g else None)
    return out


def _lerp(a, b, t):
    out = dict(a)
    for k, v in b.items():
        if k in a and isinstance(v, (int, float)) and isinstance(a[k], (int, float)):
            out[k] = a[k] + (v - a[k]) * t
    return out


SIT = dict(hipL=92, hipR=92, kneeL=12, kneeR=12, spread=17, lean=28, head_pitch=-12, head_yaw=0)
KNEEL = dict(hipL=95, hipR=-8, kneeL=97, kneeR=96, spread=8, lean=20, head_pitch=-10)


def _torso_m(p):
    return tr(0, 26, 0) @ tr(0, 4.2, 0) @ rx(p.get("lean", 0)) @ ry(p.get("twist", 0)) @ rz(p.get("roll", 0))


def place(pose, kit, P, hands=None):
    """build a figure, IK the hands to world targets {'L': xyz, 'R': xyz}, move it with P.
    -> (prims, hand world centres {'L','R'})"""
    pose = dict(pose)
    pose.setdefault("lift", 0)
    if hands:
        build_figure(dict(pose), kit)
        TT = V.LAST["T"]
        Tm = _torso_m(pose)
        for side, W in hands.items():
            if W is None:
                continue
            fin = np.linalg.inv(P) @ np.array([*W, 1.0])
            pre = np.linalg.inv(TT) @ fin
            loc = np.linalg.inv(Tm) @ pre
            pose["hand" + side] = ("t", tuple(float(v) for v in loc[:3]))
    prims, _ = build_figure(pose, kit)
    prims = [p.moved(P) for p in prims]
    hp = {p.tag[-1]: p for p in prims if (p.tag or "").startswith("hand_")}      # tags hand_L / hand_R (voxel.build_figure)
    hw = {"L": hp["L"].M[:3, 3].copy(), "R": hp["R"].M[:3, 3].copy()}
    return prims, hw


def _walk_dict(i, lean=3, **kw):
    if i == 0:
        d = dict(hipL=10, hipR=-2, kneeL=18, kneeR=8, lean=lean, spread=6, lift=0)
        d["sw"] = 0
    else:
        d = walk_legs(i)
        d["lean"] = lean
    d.update(kw)
    return d


def _grips(rec):
    z = GRIP_Z - rec
    return {"L": (2.4, GY + 3.6, z), "R": (-2.4, GY + 3.6, z)}


def _rope(a_pts, b_pts):
    return [seg(pa, pb, 0.35, "canvas") for pa, pb in zip(a_pts, b_pts)]


# ---------------------------------------------------------------- frame models
ZC = {"move": 14.0, "fire": -8.0}


def model(name, kits):
    """-> (prims, overlay) of one frame in crew model space (footprint centred on z = 0 by tr(0, 0, -zc))."""
    A, B = kits["gunner"], kits["loader"]
    prims, ov = [], []
    zc = ZC["fire"]
    if name == "hmg_idle" or name.startswith("hmg_move_"):
        i = 0 if name == "hmg_idle" else int(name.split("_")[-1])
        zc = ZC["move"]
        wheel_rot = i * 0.55
        zA = 36.0
        bob = 0.4 if i in (2, 4) else 0.0
        poseA = _walk_dict(i, lean=16 if i else 6)
        poseA.pop("sw", None)
        tgtA = {"L": (4.6, 12.5, zA - 6.5), "R": (-4.6, 12.5, zA - 6.5)}
        if i == 0:
            tgtA = {"L": (5.6, 14.5, zA - 4.5), "R": (-5.6, 14.5, zA - 4.5)}
        pA, hA = place(poseA, A, tr(0, 0, zA), tgtA)
        j = 0 if i == 0 else (i + 1) % 4 + 1
        poseB = _walk_dict(j, lean=3)
        sw = poseB.pop("sw", 0)
        poseB["handL"] = ("sw", sw * 0.25, 16, 8) if i else ("sw", 0, 14, 8)
        poseB["handR"] = ("sw", -sw, 14, 8) if i else ("sw", 0, 10, 8)
        poseB["head_yaw"] = 6
        pB, hB = place(poseB, B, tr(18.5, bob, -8.0))
        bx = hB["L"] + np.array([0.0, -4.6, 0.0])
        prims += [box(tr(*bx), 2.0, 2.4, 3.3, "olive"), box(tr(*bx) @ tr(0, 2.7, 0), 2.1, 0.35, 3.4, "steel")]      # carried ammo box
        gp, gov, info = gun_model(wheel_rot=wheel_rot, trail_lift=2.5 if i else 0.0, with_box=False,
                                  belt=[np.array([6.0, GY - 2.5, -4.0]), np.array([6.8, 5.0, -3.6])])
        # rope from the carriage axle ends to the puller's hands (rope sags toward the ground when slack)
        if GUN["kind"] == "type3":      # dragged by the front tripod feet
            ends = [pt(np.eye(4), (side * TRIPOD_X, 1.0, TRIPOD_Z)) for side in (1, -1)]
        else:
            ends = [pt(np.eye(4), (side * (WHEEL_X + 0.9), AXLE[1], AXLE[2])) for side in (1, -1)]
        hands = [hA["L"], hA["R"]]
        prims += _rope(ends, hands)
        prims += pA + pB + gp
        ov = gov
    elif name in ("hmg_set_1", "hmg_set_2", "hmg_aim", "hmg_fire_a", "hmg_fire_b"):
        fire = {"hmg_fire_a": 1.0, "hmg_fire_b": 0.35}.get(name, 0.0)
        t = {"hmg_set_1": 0.0, "hmg_set_2": 0.55}.get(name, 1.0)
        rec = RECOIL * fire
        poseA = _lerp(KNEEL, SIT, t)
        poseA["lean"] -= 2.0 * fire
        poseA["head_pitch"] -= 1.0 * fire
        zA = -30.0 + (-6.0) * t
        pA, _ = place(dict(poseA, poleL=(0.9, -1, 0.0), poleR=(-0.9, -1, 0.0)), A, tr(0, 0, zA), _grips(rec))
        bc = np.array([11.6, 2.5, -3.0])
        zB = -3.5
        poseB = dict(KNEEL, lean=LOADER_LEAN - 3 * fire, head_pitch=-8, head_yaw=0, poleL=(0.2, -1, 0.5), poleR=(0.2, -1, 0.5))
        # loader faces the gun (-x): his left hand is toward the muzzle, his right hand toward the feed block
        wob = 1.3 * math.sin(fire * 3.0) if fire else 0.0
        if t < 0.3:                     # setting the box down: hands on the box handle
            tgt = {"L": (11.6, 5.9, -1.0), "R": (11.6, 5.9, -5.0)}
            lpos = 27.5
        else:
            tgt = {"L": (9.6, 6.8, -1.2 + wob), "R": (6.4, GY - 1.2, -4.4 - wob)}
            lpos = 28.6 - 2.0 * (1 - t)
        pB, hB = place(poseB, B, tr(lpos, 0, zB) @ ry(-90), tgt)
        belt = [hB["R"], hB["L"], bc + np.array([0, 5.7, 0])] if t >= 0.3 else None
        gp, gov, info = gun_model(rec=rec, flash=(1.35 if fire == 1.0 else 0.0), belt=belt, box_at=bc)
        prims += pA + pB + gp
        ov = gov
    elif name in ("hmg_hit", "hmg_fall", "hmg_dead"):
        return _death(name, kits)
    else:
        raise KeyError(name)
    shift = tr(0, 0, -zc)
    return [p.moved(shift) for p in prims], [p.moved(shift) for p in ov]


def _death(name, kits):
    A, B = kits["gunner"], kits["loader"]
    zc = ZC["fire"]
    if name == "hmg_hit":
        poseA = dict(SIT, lean=6, head_pitch=-22, handL=("sw", 40, 30, 20), handR=("sw", 70, 40, 10), hipL=88, hipR=92)
        pA, _ = place(poseA, A, tr(0, 0, -34.0))
        poseB = dict(hipL=-6, hipR=8, kneeL=16, kneeR=8, lean=-18, twist=12, roll=5, head_pitch=-18, head_yaw=-10, spread=8,
                     handL=("sw", 75, 25, 30), handR=("sw", 40, 30, 20))
        pB, _ = place(poseB, B, tr(27.0, 0, -3.0) @ ry(-90))
        gp, gov, _ = gun_model(rec=0.6, box_at=np.array([13.0, 2.5, -3.0]))
    elif name == "hmg_fall":
        poseA = dict(tilt=-38, lean=-6, head_pitch=14, head_yaw=-18, hipL=70, hipR=84, kneeL=30, kneeR=40, spread=17,
                     handL=("sw", 30, 20, 40), handR=("sw", 60, 20, 30))
        pA, _ = place(poseA, A, tr(0, 0, -34.0))
        poseB = dict(tilt=-48, lean=-8, hipL=20, hipR=36, kneeL=42, kneeR=62, spread=11, head_pitch=-16, head_roll=12,
                     handL=("sw", 92, 10, 60), handR=("sw", 60, 22, 40))
        pB, _ = place(poseB, B, tr(27.0, 0, -4.0) @ ry(-90))
        gp, gov, _ = gun_model(rec=0.3, roll=7.0, box_at=np.array([14.0, 2.5, -2.0]))
    else:
        poseA = dict(tilt=-90, lean=-4, head_pitch=-6, head_yaw=-34, head_roll=16, hipL=60, hipR=72, kneeL=10, kneeR=34, spread=18,
                     handL=("sw", 40, 12, 40), handR=("sw", 10, 20, 30), blood=(0.0, -10.0))
        pA, _ = place(poseA, A, tr(0, 0, -30.0))
        poseB = dict(tilt=90, lean=10, head_pitch=2, head_yaw=-26, head_roll=10, hipL=42, hipR=30, kneeL=104, kneeR=92, spread=9,
                     handR=("sw", 62, 30, 20), handL=("sw", 132, 10, 28), blood=(2.0, 12.0))
        pB, _ = place(poseB, B, tr(28.0, 0, 0.0) @ ry(90))
        gp, gov, _ = gun_model(rec=0.0, roll=26.0, wreck=True, box_at=np.array([18.0, 2.6, 12.0]), trail_lift=0.0)
        gp.append(V.ell(tr(0, 0.25, 0), 18.0, 0.45, 22.0, "scorch"))
    prims = pA + pB + gp
    shift = tr(0, 0, -zc)
    return [p.moved(shift) for p in prims], [p.moved(shift) for p in gov]


# ---------------------------------------------------------------- unit
from unit_loader import load_anims, columns_of  # noqa: E402


class CrewUnit:
    kind = "crew"

    def __init__(self, spec):
        self.spec = spec
        self.name = spec["name"]
        self.gun = spec.get("gun", "maxim")
        self.anims = load_anims(spec.get("anims", self.name))
        self.columns = columns_of(self.anims)
        self.kits = make_kits(spec.get("gear", {"gunner": ["canteen_cloth"],
                                                  "loader": ["haversack", "canteen_cloth"]}),
                              spec.get("headgear", "feldkappe"), spec.get("uniform", "ww1_khaki"), spec.get("badge"))
        d = spec.get("dirs", "all"); f = spec.get("frames", "all")
        self.dirs = list(DIRS) if d == "all" else list(d)
        self.frames = list(self.columns) if f == "all" else list(f)
        self.lods = CREW_LODS
        self.lod_cells = [("s", "idle"), ("e", "move_1")]
        self._cache = {}

    collide_dirs = ["s"]                      # the scene is direction independent

    def collide_prims(self, name, d):
        GUN["kind"] = self.gun
        if name not in self._cache:
            self._cache[name] = model(name, self.kits)
        return self._cache[name][0]

    def frame(self, d, name, res=None, fit=True, shift=None, **kw):
        res = res or CREW_LODS[0]
        GUN["kind"] = self.gun
        if name not in self._cache:
            self._cache[name] = model(name, self.kits)
        prims, ov = self._cache[name]
        kw.setdefault("push", 0.0)
        kw.setdefault("lift", 0.0)
        return render_prims(prims, ov, d, res, fit, shift, **kw)
