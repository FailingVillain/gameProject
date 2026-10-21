"""Artillery pieces as primitive lists (ported from the legacy vox_towed.py M2A1 gun + vox_artillery.py tube / flash).

Reused from the legacy generators: the wheel + axle + carriage + tapered-trail + spade construction (vox_towed.gun_prims),
the shield with a team-colour top stripe and lower flap, the barrel stack breech / recoil sleeve / tube / muzzle and the
muzzle flash ellipsoid stack (flash_edge / flash / flash_core + side blast; the smoke puffs are dropped: fire is smoke-free).
New: spoked wheels from capsule spokes + rim ring (open between the spokes), box / mono / split trails, handwheel,
ammunition crate, shells, wreck / limber helpers. Dimensions come from tune/parts.json "guns".

Gun frame: origin = ground point under the axle centre, x = the gun's left, y up, z = firing direction.
build(kind, state) -> (prims, overlay, pts): pts = named gun-frame points the crew uses (handwheel, breech, lanyard, trail eye ...).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import ell, cyl, rbox, seg, along_z, tr, rx, ry, rz, pt, box  # noqa: E402
import tune  # noqa: E402

KINDS = ("howitzer", "infantry_gun", "field_gun")
WRECK = {"gun_paint": "gun_burn", "gun_paint2": "gun_burn2", "gun_steel": "gun_burn2", "gun_tire": "gun_burn2",
         "gun_wood": "gun_burn", "brass": "gun_burn", "team": "gun_burn", "canvas": "gun_burn", "wood": "gun_burn", "leather": "gun_burn2"}


def mat_fn(wreck):
    return (lambda n: WRECK.get(n, n)) if wreck else (lambda n: n)


_NOSCALE = {"spokes", "tilt", "case", "type", "style", "scale"}


def _scaled(node, k, key=None):
    if isinstance(node, dict):
        return {a: _scaled(b, k, a) for a, b in node.items()}
    if isinstance(node, (list, tuple)):
        return tuple(_scaled(b, k, key) for b in node)
    if isinstance(node, (int, float)) and not isinstance(node, bool) and key not in _NOSCALE:
        return node * k
    return node


def spec(kind):
    """tune/parts.json guns.<kind> with every length multiplied by its "scale" (the gun is drawn larger than true scale next
    to the crew, like the legacy K = 1.5; crew stations / travel offsets in tune/poses.json are in these scaled units)."""
    S = tune.get("parts", "guns", kind)
    return _scaled(S, float(S.get("scale", 1.0)))


# ---------------------------------------------------------------- generic helpers
def beam(p0, p1, hw, hh, mat, up=(0.0, 1.0, 0.0), paint=None, r=0.7):
    """rounded box from p0 to p1; width 2*hw (local x), height 2*hh (local y, kept near `up`)."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    v = p1 - p0
    L = float(np.linalg.norm(v))
    z = v / max(L, 1e-9)
    y = np.asarray(up, float) - z * float(np.dot(up, z))
    y /= max(np.linalg.norm(y), 1e-9)
    x = np.cross(y, z)
    M = np.eye(4)
    M[:3, 0], M[:3, 1], M[:3, 2], M[:3, 3] = x, y, z, (p0 + p1) / 2
    return rbox(M, hw, hh, L / 2, mat, paint=paint, r=r)


def wheel_spoked(Mh, W, rot, m):
    """spoked wheel, hub centre frame Mh (axle = local x, wheel plane = local y-z); rot = spin in degrees."""
    R, n, rim, hub = W["r"], int(W["spokes"]), W["rim"], W["hub"]
    out = []
    for i in range(n):
        a = math.radians(rot + 360.0 * i / n)
        c, s = math.cos(a), math.sin(a)
        out.append(seg(pt(Mh, (0, c * hub * 0.8, s * hub * 0.8)), pt(Mh, (0, c * (R - rim), s * (R - rim))), 0.95, m("gun_wood"), r1=0.75))
    nr = max(16, int(R * 1.05))
    Rr = R - rim
    for i in range(nr):
        a0 = math.radians(rot + 360.0 * i / nr)
        a1 = math.radians(rot + 360.0 * (i + 1) / nr)
        out.append(seg(pt(Mh, (0, Rr * math.cos(a0), Rr * math.sin(a0))), pt(Mh, (0, Rr * math.cos(a1), Rr * math.sin(a1))), rim, m("gun_tire")))
    out.append(cyl(Mh @ rz(90), hub, W["hub_w"] / 2, m("gun_steel")))
    out.append(cyl(Mh @ tr(0, 0, 0) @ rz(90), hub * 0.45, W["hub_w"] / 2 + 0.9, m("gun_tire")))
    return out


def wheel_disc(Mh, W, rot, m):
    """small pressed-steel disc wheel: wood-coloured disc, dark iron tyre, 6 lighter spoke ribs (paint on the cylinder)."""
    R = W["r"]
    rot_r = math.radians(rot)

    def rad(q):
        return np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2)

    def ang(q):
        return np.arctan2(q[:, 2], q[:, 0])
    paint = [(lambda q: rad(q) > R - 1.7, m("gun_tire")),
             (lambda q: (rad(q) > 2.6) & (np.cos(6 * (ang(q) + rot_r)) > 0.55), m("gun_paint2")),
             (lambda q: rad(q) < 2.6, m("gun_steel"))]
    return [cyl(Mh @ rz(90), R, W["thick"], m("gun_wood"), paint=paint)]


def shell_prims(base, tip, kind, wreck=False):
    """projectile (+ brass case for fixed ammunition) lying from base to tip (gun-frame points)."""
    S = spec(kind)["shell"]
    m = mat_fn(wreck)
    base, tip = np.asarray(base, float), np.asarray(tip, float)
    d = tip - base
    L = float(np.linalg.norm(d))
    case = S["case"]
    out = []
    if case > 0:
        mid = base + d * case
        out.append(seg(base, mid, S["r"], m("brass")))
        out.append(seg(mid, tip - d * 0.0, S["r"] * 0.92, m("gun_steel"), r1=S["r"] * 0.35))
    else:
        mid = base + d * 0.12
        out.append(seg(base, mid, S["r"] * 0.9, m("canvas")))                                   # charge bag / base
        out.append(seg(mid, tip, S["r"], m("gun_paint2"), r1=S["r"] * 0.45))
        out.append(seg(base + d * 0.5, base + d * 0.58, S["r"] * 1.04, m("brass")))              # driving band
    return out


def ammo_box(kind, wreck=False, shells=2, open_=True):
    """wooden crate with a few shell noses sticking out."""
    S = spec(kind)
    m = mat_fn(wreck)
    b = S["box"]
    x, y, z = b["pos"]
    hx, hy, hz = b["size"]
    out = [rbox(tr(x, y, z), hx, hy, hz, m("gun_wood"), r=0.5),
           rbox(tr(x, y + hy * 0.9, z), hx * 1.04, hy * 0.18, hz * 1.04, m("gun_paint2"), r=0.3)]
    if open_ and not wreck:
        r = S["shell"]["r"] * 0.9
        for i in range(shells):
            sx = x + (i - (shells - 1) / 2) * (r * 2.1)
            out.append(seg((sx, y + hy * 0.6, z), (sx, y + hy + S["shell"]["len"] * 0.5, z), r, m("gun_paint2"), r1=r * 0.5))
    return out


# ---------------------------------------------------------------- the gun
def build(kind, st=None, wreck=False):
    """st keys: elev (deg), recoil (barrel slide), car_back (carriage roll-back), wheel_rot (deg), flash (None | 'a'|'b' strength),
    breech_open (bool), lost_wheel (+1 left / -1 right: that wheel is omitted)."""
    st = st or {}
    S = spec(kind)
    m = mat_fn(wreck)
    elev = st.get("elev", 0.0)
    recoil = st.get("recoil", 0.0)
    C = tr(0, 0, -st.get("car_back", 0.0))
    W = S["wheel"]
    R = W["r"]
    prims, pts = [], {}
    # wheels + axle
    for sx in (1, -1):
        if st.get("lost_wheel") == sx:
            continue
        Mh = C @ tr(sx * W["x"], R, 0)
        prims += (wheel_spoked if W["style"] == "spoked" else wheel_disc)(Mh, W, st.get("wheel_rot", 0.0) * (1 if sx > 0 else -1), m)
    ax_half = W["x"] - (W.get("hub_w", 0) * 0.3 if W["style"] == "spoked" else 0)
    prims.append(cyl(C @ tr(0, R, 0) @ rz(90), S["axle_r"], ax_half, m("gun_steel")))
    ab = S["axle_box"]
    prims.append(rbox(C @ tr(0, R + ab[1] - 0.8, 0), ab[0], ab[1], ab[2], m("gun_paint2"), r=1.2))
    pts["hub_L"] = pt(C, (W["x"], R, 0))
    pts["hub_R"] = pt(C, (-W["x"], R, 0))
    pts["wheel_top_L"] = pt(C, (W["x"], R * 2 - W.get("rim", 0.0), 0))
    pts["wheel_top_R"] = pt(C, (-W["x"], R * 2 - W.get("rim", 0.0), 0))
    # cheeks (upper carriage)
    ch = S["cheek"]
    cx, cy, cz = ch["pos"]
    hxs, hys, hzs = ch["size"]
    team = [(lambda q: q[:, 1] > hys - 3.2, "team")] if not wreck else None
    for sx in (1, -1):
        prims.append(rbox(C @ tr(sx * ch["x"], cy, cz), hxs, hys, hzs, m("gun_paint"), paint=team, r=0.8))
    prims.append(rbox(C @ tr(0, cy - hys + 1.6, cz - 1.0), ch["x"], 1.6, hzs * 0.8, m("gun_paint2"), r=0.8))
    # trail
    T = S["trail"]
    s0, s1 = np.array(T["start"], float), np.array(T["end"], float)
    hw, hh = T["hw"], T["hh"]
    ty = lambda t: s0[1] + (s1[1] - s0[1]) * t           # noqa: E731
    if T["type"] == "mono":
        prims.append(beam(pt(C, s0), pt(C, s1), hw, hh, m("gun_paint2")))
        prims.append(rbox(C @ tr(0, s1[1] + 0.2, s1[2] - 0.6), hw + 1.4, 2.4, 1.0, m("gun_steel"), r=0.5))              # spade plate
        prims.append(rbox(C @ tr(0, s1[1] - 1.8, s1[2] + 2.0) @ rx(30), hw + 1.2, 0.5, 3.4, m("gun_steel"), r=0.3))    # spade blade
        for sx in (1, -1):
            prims.append(seg(pt(C, (sx * 9.0, s1[1] + 3.4, s1[2] + 1.2)), pt(C, (sx * 3.0, s1[1] + 3.0, s1[2] + 1.2)), 1.0, m("gun_steel")))   # handles
        pts["trail_L"] = pt(C, (0.0, ty(0.6) + hh, s0[2] + (s1[2] - s0[2]) * 0.6))
        pts["trail_R"] = pts["trail_L"]
        pts["handle_L"] = pt(C, (7.5, s1[1] + 3.3, s1[2] + 1.2))
        pts["handle_R"] = pt(C, (-7.5, s1[1] + 3.3, s1[2] + 1.2))
        pts["eye"] = pt(C, (0.0, s1[1] + 3.0, s1[2] - 3.0))
        prims.append(ell(C @ tr(0, s1[1] + 3.0, s1[2] - 2.6), 2.3, 2.3, 1.4, m("gun_steel")))
    else:
        for sx in (1, -1):
            a = (sx * s0[0], s0[1], s0[2])
            b = (sx * s1[0], s1[1], s1[2])
            if T["type"] == "box":
                prims.append(beam(pt(C, a), pt(C, b), hw, hh, m("gun_paint2")))
                prims.append(rbox(C @ tr(sx * s1[0], s1[1] - 1.0, s1[2] - 1.2), hw + 0.6, 2.6, 1.0, m("gun_steel"), r=0.4))
            else:                                                                           # split tubular legs
                prims.append(seg(pt(C, a), pt(C, b), hw, m("gun_paint2"), r1=hw * 0.8))
                prims.append(rbox(C @ tr(sx * s1[0], 1.2, s1[2] - 1.0), 2.0, 1.2, 1.4, m("gun_steel"), r=0.5))
        for zb in T["braces"]:
            t = (zb - s0[2]) / (s1[2] - s0[2])
            xe = s0[0] + (s1[0] - s0[0]) * t
            prims.append(rbox(C @ tr(0, ty(t) - 0.6, zb), xe, 1.7, 1.5, m("gun_paint2"), r=0.5))
        if T["type"] == "box":
            prims.append(rbox(C @ tr(0, s1[1] + 0.4, s1[2] - 1.2), s1[0] + hw, 2.2, 0.9, m("gun_steel"), r=0.4))
            for sx in (1, -1):
                prims.append(seg(pt(C, (sx * (s1[0] + hw + 5.0), s1[1] + 4.4, s1[2] + 2.0)), pt(C, (sx * (s1[0] - 1.0), s1[1] + 4.2, s1[2] + 2.0)), 1.0, m("gun_steel")))
            prims.append(ell(C @ tr(0, s1[1] + 1.0, s1[2] - 3.0), 2.4, 2.4, 1.5, m("gun_steel")))
            pts["handle_L"] = pt(C, (s1[0] + hw + 3.0, s1[1] + 4.4, s1[2] + 2.0))
            pts["handle_R"] = pt(C, (-(s1[0] + hw + 3.0), s1[1] + 4.4, s1[2] + 2.0))
            pts["eye"] = pt(C, (0.0, s1[1] + 1.0, s1[2] - 3.0))
            tt = 0.55
            pts["trail_L"] = pt(C, (s0[0] + (s1[0] - s0[0]) * tt, ty(tt) + hh, s0[2] + (s1[2] - s0[2]) * tt))
            pts["trail_R"] = pt(C, (-(s0[0] + (s1[0] - s0[0]) * tt), ty(tt) + hh, s0[2] + (s1[2] - s0[2]) * tt))
        else:
            prims.append(seg(pt(C, (s1[0], 1.6, s1[2] - 0.5)), pt(C, (-s1[0], 1.6, s1[2] - 0.5)), 0.9, m("gun_steel")))   # cross bar for the handles
            pts["handle_L"] = pt(C, (s1[0], 2.2, s1[2] - 0.5))
            pts["handle_R"] = pt(C, (-s1[0], 2.2, s1[2] - 0.5))
            pts["eye"] = pt(C, (0.0, 2.0, s1[2] - 1.5))
            prims.append(ell(C @ tr(0, 2.0, s1[2] - 1.0), 1.8, 1.8, 1.2, m("gun_steel")))
            pts["trail_L"] = pt(C, (s0[0] + (s1[0] - s0[0]) * 0.5, ty(0.5) + hh, s0[2] + (s1[2] - s0[2]) * 0.5))
            pts["trail_R"] = pt(C, (-(s0[0] + (s1[0] - s0[0]) * 0.5), ty(0.5) + hh, s0[2] + (s1[2] - s0[2]) * 0.5))
    # handwheel (left) + sight
    hx_, hy_, hz_ = S["handwheel"]
    prims.append(cyl(C @ tr(hx_, hy_, hz_) @ rz(90), 3.0 if kind != "infantry_gun" else 2.2, 0.6, m("gun_steel")))
    prims.append(seg(pt(C, (hx_ - 0.4, hy_, hz_)), pt(C, (hx_ + 1.6, hy_ + 2.4, hz_ + 1.0)), 0.5, m("gun_steel")))
    pts["handwheel"] = pt(C, (hx_ + 1.4, hy_ + 2.2, hz_ + 1.0))
    # shield
    sh = S["shield"]
    if sh:
        stripe = [(lambda q, h=sh["hh"]: q[:, 1] > h - 3.4, "team")] if not wreck else None
        Ms = C @ tr(0, sh["y"], sh["z"]) @ rx(-sh["tilt"])
        if wreck:
            Ms = Ms @ rz(14) @ rx(-14)
        prims.append(rbox(Ms, sh["hw"], sh["hh"], 0.65, m("gun_paint"), paint=stripe, r=0.4))
        prims.append(rbox(Ms @ tr(0, -sh["hh"] - sh["flap_hh"] + 0.4, -0.3), sh["hw"] * 0.72, sh["flap_hh"], 0.55, m("gun_paint"), r=0.3))
        pts["shield_L"] = pt(Ms, (sh["hw"] - 2.5, sh["hh"] - 4.0, 1.0))
        pts["shield_R"] = pt(Ms, (-(sh["hw"] - 2.5), sh["hh"] - 4.0, 1.0))
        pts["shield_front_L"] = pt(Ms, (sh["hw"] - 3.0, sh["hh"] - 4.0, 1.0))
        pts["shield_front_R"] = pt(Ms, (-(sh["hw"] - 3.0), sh["hh"] - 4.0, 1.0))
        pts["shield_back_L"] = pt(Ms, (sh["hw"] - 3.0, 0.0, -1.0))
        pts["shield_back_R"] = pt(Ms, (-(sh["hw"] - 3.0), 0.0, -1.0))
    # barrel
    tx, ty_, tz = S["trunnion"]
    Tp = C @ tr(tx, ty_, tz)
    B = Tp @ rx(-elev)
    Bk = B @ tr(0, 0, -recoil)
    bl = S["barrel"]
    prims.append(cyl(Tp @ rz(90), 2.2 if kind != "infantry_gun" else 1.3, ch["x"] + 1.6, m("gun_steel")))              # trunnion pin
    z0, z1, r = bl["breech"]
    if st.get("breech_open"):
        prims.append(rbox(Bk @ tr(r * 1.6, 0.3, (z0 + z1) / 2 - 1.2), 0.8, r, (z1 - z0) / 2, m("gun_steel"), r=0.5))     # block swung out to the left
        prims.append(along_z(Bk, r * 0.6, z0 + 0.5, z1 + 0.5, m("gun_steel")))                                          # open chamber rear
    else:
        prims.append(rbox(Bk @ tr(0, 0, (z0 + z1) / 2), r, r, (z1 - z0) / 2, m("gun_steel"), r=min(1.4, r * 0.4)))
    prims.append(ell(Bk @ tr(0, 0, z0 - 0.4), r * 0.8, r * 0.8, 1.4, m("gun_steel")))                                    # breech knob
    z0, z1, r = bl["sleeve"]
    prims.append(along_z(B, r, z0, z1, m("gun_paint2")))                                                                  # cradle / recoil sleeve (fixed)
    z0, z1, r = bl["tube"]
    prims.append(along_z(Bk, r, z0, z1, m("gun_steel")))
    z0, z1, r = bl["swell"]
    prims.append(along_z(Bk, r, z0, z1, m("gun_steel")))
    z0, z1, r = bl["recup"]
    if r > 0:
        prims.append(along_z(B @ tr(0, bl["sleeve"][2] + r * 0.9, 0), r, z0, z1, m("gun_steel")))
    prims.append(rbox(B @ tr(bl["sleeve"][2] + 1.5, 2.6, 5.0), 1.1, 2.0, 3.0, m("gun_steel"), r=0.5))                      # sight bracket
    z_muz = bl["tube"][1]
    pts["B"] = B
    pts["breech"] = pt(Bk, (0, 0, bl["breech"][0] - 1.0))
    pts["breech_mouth"] = pt(B, (0, 0, bl["tube"][0] - 0.5))
    pts["lanyard"] = pt(Bk, (bl["breech"][2] * 0.9, 0, bl["breech"][0] + 1.0))
    pts["muzzle"] = pt(Bk, (0, 0, z_muz))
    # flash (overlay prims: unlit, depth-tested against the body z-buffer by voxel.make_frame)
    ov = []
    fl = st.get("flash")
    if fl:
        k = S["flash"] * (1.0 if fl == "a" else 0.7)
        ov = muzzle_flash(Bk, z_muz, k)
    return prims, ov, pts


def muzzle_flash(B, zm, k=1.0):
    """smoke-free muzzle flash: edge / body / core ellipsoids ahead of the muzzle + a side blast pair."""
    return [ell(B @ tr(0, 0, zm + 4.2 * k), 8.0 * k, 8.0 * k, 10.0 * k, "flash_edge"),
            ell(B @ tr(0, 0, zm + 3.6 * k), 5.8 * k, 5.8 * k, 7.6 * k, "flash"),
            ell(B @ tr(0, 0, zm + 2.2 * k), 3.2 * k, 3.2 * k, 4.2 * k, "flash_core"),
            ell(B @ tr(5.0 * k, 0, zm - 1.0), 4.0 * k, 3.2 * k, 3.0 * k, "flash"),
            ell(B @ tr(-5.0 * k, 0, zm - 1.0), 4.0 * k, 3.2 * k, 3.0 * k, "flash")]
