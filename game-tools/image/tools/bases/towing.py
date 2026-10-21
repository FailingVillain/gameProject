"""Horse-drawn travel for the heavy howitzer: gun (trail forward, barrel to the rear, trail end lifted onto the limber pintle)
+ two-wheel ammunition limber + a pair of draught horses (wagon horse model + harness, walk cycle synced to the wheels) +
the 4 crew (driver and a second man sit on the limber chest, two walk beside the gun).

tow_scene(kind, kit, k, opts) -> (prims, info); tow frame: origin = ground under the gun axle, +z = direction of travel.
Used by bases/artillery.scene for move_1..4 when the unit json has "tow": true.  Dimensions below are in gun model units.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import build_figure, rbox, ell, seg, cyl, tr, pt, rx, ry, rz  # noqa: E402
from poses import walk_legs  # noqa: E402
from parts import guns  # noqa: E402
from parts import horse as H  # noqa: E402
from bases import cavalry as C  # noqa: E402
from bases import wagon as WG  # noqa: E402  (horse + harness helpers, shared gait)
import tune  # noqa: E402

STEP = 4                                   # frames per walk cycle
PIN_Y = 17.0                               # trail eye height on the pintle
CHEST_HALF = (13.5, 6.0, 9.5)              # limber chest half sizes
SEAT_X = (5.6, -5.6)
FOOT_DROP = 19.0
SEAT_LIFT = 9.0


def _tilt_for(gun_pts, R):
    """pitch (deg) about the axle that raises the trail eye to PIN_Y (gun already turned round: eye at +z)."""
    e = gun_pts["eye"]
    lo, hi = 0.0, 25.0
    for _ in range(40):
        mid = (lo + hi) / 2
        G = tr(0, R, 0) @ rx(-mid) @ tr(0, -R, 0)
        if pt(G, e)[1] < PIN_Y:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def limber(kind, A, rot, m=lambda n: n):
    """limber prims with the axle at z = A; returns (prims, pts)."""
    S = guns.spec(kind)
    W = S["wheel"]
    R = W["r"]
    out = []
    for sx in (1, -1):
        out += guns.wheel_spoked(tr(sx * W["x"], R, A), W, rot, m)
    out.append(cyl(tr(0, R, A) @ rz(90), S["axle_r"], W["x"] - 1.4, m("gun_steel")))
    hx, hy, hz = CHEST_HALF
    yc = R + 5.0 + hy
    zc = A - 1.0
    out.append(rbox(tr(0, R + 3.0, A), 10.5, 3.0, 7.0, m("gun_paint2"), r=1.0))                           # axle bed
    out.append(rbox(tr(0, yc, zc), hx, hy, hz, m("gun_paint"), r=1.0))                                    # ammunition chest
    out.append(rbox(tr(0, yc + hy + 0.6, zc), hx + 0.7, 0.8, hz + 0.7, m("gun_paint2"), r=0.4,
                    paint=[(lambda q: q[:, 2] > hz * 0.35, "team")]))                                     # lid with a team stripe at the front
    for sx in (1, -1):
        out.append(rbox(tr(sx * (hx + 0.2), yc - 1.5, zc), 0.6, hy * 0.45, hz * 0.7, m("gun_steel"), r=0.3))   # side strap plates (below the seat)
    # pintle bracket reaching down-back to the trail eye
    pin_z = A - hz - 4.0
    out.append(seg((0, yc - hy + 0.5, zc - hz + 1.0), (0, PIN_Y + 1.5, pin_z), 1.5, m("gun_steel")))
    out.append(ell(tr(0, PIN_Y + 1.2, pin_z), 2.4, 2.0, 2.4, m("gun_steel")))
    # footboard + pole root + splinter bar
    out.append(rbox(tr(0, yc + hy - FOOT_DROP, zc + hz + 5.5), hx - 1.0, 0.8, 3.0, m("gun_paint2"), r=0.3))
    for sx in (1, -1):
        out.append(seg((sx * (hx + 0.5), yc + hy - FOOT_DROP, zc + hz + 5.5), (sx * (hx + 0.5), yc - 1.0, zc + hz - 0.5), 0.6, m("gun_steel")))
    out.append(rbox(tr(0, R + 3.0, A + 14.5), 14.0, 1.0, 1.0, m("gun_steel"), r=0.4))                    # splinter bar
    pts = {"top": yc + hy + 1.4, "zc": zc, "pin_z": pin_z, "bar_z": A + 14.5, "bar_y": R + 3.0, "pole_root": (0.0, R + 4.0, A + 8.0)}
    return out, pts


def _traces(hbs, sides, bar_y, bar_z, xoff=11.0):
    out = []
    for hb, s in zip(hbs, sides):
        a = pt(hb.Bs, (s * 9.4, 3.0, 14.0))
        b = pt(hb.Bs, (s * 9.6, 3.5, -14.0))
        c = np.array([s * (xoff + 2.0), bar_y, bar_z])
        out += [seg(a, b, 0.42, "leather"), seg(b, c, 0.42, "leather")]
    return out


def _seated(kit, pose, x, y, z):
    rp = dict(pose)
    rp.pop("use", None)
    rprims, _ = build_figure(rp, kit, None, None)
    root = C._rider_root(rprims)
    G = tr(x, y + SEAT_LIFT, z) @ tr(*(-root))
    return [q.moved(G) for q in rprims]


def _walker(kit, kind, x, z, ph, place):
    L = walk_legs(ph)
    pose = dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"] * 0.5, lean=5, head_pitch=-3, spread=5,
                handL=("sw", L["sw"], 14, 8), handR=("sw", -L["sw"], 14, 8))
    return place(kit, pose, x, z)


def tow_scene(kind, kit, k, opts, place_fn, stn_cls, sep=0.0, drop=0.0, gun_k=None):
    """-> (prims, info). place_fn = bases.artillery.place, stn_cls = bases.artillery.Stn (passed in to avoid a circular import)."""
    S = guns.spec(kind)
    W = S["wheel"]
    R = W["r"]
    A_tow = tune.get("poses", "artillery", kind).get("tow", {})
    step = 360.0 / int(W["spokes"]) / STEP
    wrot = (k - 1) * step                      # limber wheels / horse gait phase
    grot = wrot if gun_k is None else (gun_k - 1) * step      # gun wheels (unhitch: the gun stands still)
    bob = 0.7 if k in (2, 4) else 0.0
    # gun, turned round: barrel to the rear, trail end forward
    g, ov, pts = guns.build(kind, dict(elev=0.0, wheel_rot=-grot))
    Gr = ry(180)
    pts = {n: (pt(Gr, v) if np.ndim(v) == 1 else Gr @ v) for n, v in pts.items()}
    th = _tilt_for(pts, R)
    th_full = th
    th = th_full * (1.0 - drop)                # unhitch: the trail end is let down to the ground as the limber pulls away
    g, ov, pts = guns.build(kind, dict(elev=th + A_tow.get("elev_extra", 0.0), wheel_rot=-grot))
    Gt = tr(0, R, 0) @ rx(-th) @ tr(0, -R, 0) @ Gr
    Bb = tr(0, bob, 0)
    gun = [p.moved(Bb @ Gt) for p in g]
    eye = pt(Gt, pts["eye"])
    # limber (+ horses, seated crew) = A; sep = how far the team has pulled away forward from the trail
    A = float(eye[2]) + 4.0 + CHEST_HALF[2] + 4.0 + 1.0 + sep
    lp, lpts = limber(kind, A, wrot)
    lp = [q.moved(Bb) for q in lp]
    # horses
    xh = A_tow.get("horse_x", 11.0)
    zh = A + A_tow.get("horse_dz", 40.0)
    hpL, hpR = WG._horse_poses(f"move_{k}")
    prL, hbL = WG._horse(hpL, xh, zh, 1)
    prR, hbR = WG._horse(hpR, -xh, zh, -1)
    k_ = WG._hscale()
    hb0 = H.build_horse(C._stand(), tack=False)
    Bs0 = hb0.B @ np.diag([k_, k_, k_, 1.0])
    c = pt(Bs0, (0, 2.4, 16.8))
    pole_end = np.array([0.0, float(c[1]) - 4.0, zh + float(c[2]) - 5.5])
    pr = (pole_end - np.array(lpts["pole_root"]))
    prims = gun + lp + prL + prR
    prims.append(seg(lpts["pole_root"], tuple(pole_end), 1.15, "wood", r1=0.9))
    prims.append(rbox(tr(*pole_end) @ tr(0, 0, -1.0), 6.0, 0.9, 0.9, "steel", r=0.3))
    prims += _traces([hbL, hbR], (1, -1), lpts["bar_y"], lpts["bar_z"], xh)
    # crew on the limber chest: driver (reins) + second man
    S0 = dict(hipL=64, hipR=64, kneeL=84, kneeR=84, spread=3, lean=4, poleL=(0.7, -1, -0.2), poleR=(-0.7, -1, -0.2))
    rein = (("t", (3.4, 3.0, 11.0)), ("t", (-3.4, 3.0, 11.0)))
    sw = (-2, 2, 2, -2)[k - 1]
    drv = _seated(kit, dict(S0, handL=rein[0], handR=rein[1], head_yaw=3 if k % 2 else -3, twist=sw), SEAT_X[0], lpts["top"], lpts["zc"] + 8.5)
    crew2 = _seated(kit, dict(S0, handL=("t", (6.0, 2.0, 9.0)), handR=("t", (-6.0, 2.0, 9.0)), head_yaw=-8 if k in (1, 2) else -4, twist=-sw), SEAT_X[1], lpts["top"], lpts["zc"] + 8.5)
    drv = [q.moved(Bb) for q in drv]
    crew2 = [q.moved(Bb) for q in crew2]
    prims += drv + crew2
    hands = [q for q in drv if q.tag == "hand"]
    if len(hands) == 2:
        for h, hb, s in zip(hands, (hbL, hbR), (1, -1)):
            tgt = hb.bits[0 if s > 0 else 1] + hb.M[:3, 3]
            prims.append(seg(h.M[:3, 3], tgt, 0.3, "leather"))
    # two men walking beside the gun
    wx = A_tow.get("walk_x", 50.0)
    for i, (sx, z, ph) in enumerate(((wx, A_tow.get("walk_z1", 16.0), k), (-wx, A_tow.get("walk_z2", -22.0), (k + 1) % 4 + 1))):
        prims += _walker(kit, kind, sx, z, ph, lambda kit_, pose, x, zz: place_fn(kit_, pose, stn_cls(x, zz, 0.0)))
    cx, cz = (opts or {}).get("center", (0.0, 0.0))
    gc = pt(Bb @ Gt, (cx, 0.0, cz))                       # the GUN's footprint centre in the tow frame (frame anchor, same as every other frame)
    return prims, {"hitch": pt(Bb, eye), "pole_end": pole_end, "limber_a": A, "gun_center": (float(gc[0]), float(gc[2]))}
