"""Cavalry horse built from primitives + tack (saddle, cloth, girth, saddle bags, bedroll, bridle, carbine boot, saddle
scabbard) + the WWI cavalry carbine.

Horse body frame: origin = barrel centre, y up, z forward, x = the horse's left (same handedness as the infantry model).
Dimensions: tune/parts.json -> "horse"; pose angles: tune/poses.json -> "cavalry" (see bases/cavalry.py).

build_horse(hp) -> HorseBuild with .prims (grounded: lowest point at y = 0, barrel centre over x = z = 0 unless lying),
.B (body frame after grounding), .Hd (head frame), .bits (left / right bit rings, world) for the reins.
Horse pose hp keys:
  FL/FR/HL/HR = (swing, knee|hock, fetlock) degrees; pitch (nose up), roll (+ = falls onto its right side), neck (deg above
  horizontal), head (deg below horizontal), neck_yaw, tail (deg below horizontal of the dock segment), air (lift above
  ground), lie (bool: rolled onto the right side, legs out to the left).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox, ell, seg, cyl, along_z, tr, rx, ry, rz, pt, _prim_ymin  # noqa: E402
import tune  # noqa: E402
from parts._t import boxp  # noqa: E402
from parts.blades import hilt_prims  # noqa: E402
from parts.rifles import _flash  # noqa: E402

COAT, POINTS, HOOF = "horse", "horse_d", "hoof"


def _T():
    return tune.get("parts", "horse")


def sag(a):
    """unit vector in the sagittal plane: a deg from straight down, + = forward (z)."""
    r = math.radians(a)
    return np.array([0.0, -math.cos(r), math.sin(r)])


def _src(prims, src):
    for p in prims:
        p.src = src
    return prims


def _ellp(M, c, mat, **kw):
    return ell(M @ tr(*c["pos"]), *c["size"], mat, **kw)


def look_frame(origin, zdir, up=(0, 1, 0)):
    """4x4 frame at origin with local z along zdir and local y as close to `up` as possible."""
    z = np.asarray(zdir, float); z = z / np.linalg.norm(z)
    u = np.asarray(up, float)
    x = np.cross(u, z)
    if np.linalg.norm(x) < 1e-6:
        x = np.array([1.0, 0, 0])
    x = x / np.linalg.norm(x)
    y = np.cross(z, x)
    M = np.eye(4)
    M[:3, 0], M[:3, 1], M[:3, 2], M[:3, 3] = x, y, z, origin
    return M


# ---------------------------------------------------------------- legs (body frame)
def front_leg(side, swing, knee, fet):
    L = _T()["front_leg"]; Hf = _T()["hoof"]; slope = _T()["pastern_slope"]
    P0 = np.array([side * L["top"][0], L["top"][1], L["top"][2]])
    K = P0 + L["upper"] * sag(swing)
    b = swing - knee
    F = K + L["cannon"] * sag(b)
    c = b + slope - fet
    P = F + L["pastern"] * sag(c)
    hb = b if fet < 30 else c                      # hoof follows the cannon on the ground, the pastern when folded
    out = [seg(P0, K, L["r_upper"][0], COAT, r1=L["r_upper"][1]),                          # forearm
           ell(tr(*K), L["r_knee"] * 0.95, L["r_knee"], L["r_knee"] * 1.05, POINTS),       # knee
           seg(K, F, L["r_cannon"], POINTS),                                               # cannon
           ell(tr(*F), L["r_fetlock"] * 0.95, L["r_fetlock"], L["r_fetlock"] * 1.05, POINTS),
           seg(F, P, L["r_pastern"], POINTS),
           seg(P - 0.2 * sag(hb), P + Hf["len"] * sag(hb), Hf["r_top"], HOOF, r1=Hf["r_bottom"])]
    return out


def hind_leg(side, swing, hock, fet):
    L = _T()["hind_leg"]; Hf = _T()["hoof"]; slope = _T()["pastern_slope"]
    P0 = np.array([side * L["top"][0], L["top"][1], L["top"][2]])
    H = P0 + L["upper"] * sag(swing)
    b = swing + hock
    F = H + L["cannon"] * sag(b)
    c = b + slope - fet
    P = F + L["pastern"] * sag(c)
    hb = b if fet < 30 else c
    back = -np.array([0.0, math.sin(math.radians(b)) * 0 + 0.0, 1.0])
    out = [seg(P0, H, L["r_upper"][0], COAT, r1=L["r_upper"][1]),                          # gaskin
           ell(tr(*H), L["r_hock"] * 0.85, L["r_hock"], L["r_hock"] * 1.1, POINTS),         # hock
           ell(tr(*(H + 1.5 * back + np.array([0, 0.9, 0]))), 1.1, 1.3, 1.2, POINTS),       # point of hock
           seg(H, F, L["r_cannon"], POINTS),
           ell(tr(*F), L["r_fetlock"] * 0.95, L["r_fetlock"], L["r_fetlock"] * 1.05, POINTS),
           seg(F, P, L["r_pastern"], POINTS),
           seg(P - 0.2 * sag(hb), P + Hf["len"] * sag(hb), Hf["r_top"], HOOF, r1=Hf["r_bottom"])]
    return out


# ---------------------------------------------------------------- body, neck, head, tail (body frame)
def body_prims(hp):
    T = _T(); Bd = T["body"]; S = T["saddle"]
    cz0, cz1 = S["cloth_z"]; cy = S["cloth_y"]; gz, gw = S["girth_z"], S["girth_w"]
    barrel_paint = [(lambda p: (p[:, 1] > cy) & (p[:, 2] > cz0) & (p[:, 2] < cz1), "blanket"),       # saddle cloth
                    (lambda p: (np.abs(p[:, 2] - gz) < gw) & (p[:, 1] <= cy), "leather")]            # girth
    I = np.eye(4)
    p = [_ellp(I, Bd["barrel"], COAT, paint=barrel_paint)]
    for k in ("chest", "breast", "withers", "rump", "croup", "buttock"):
        p.append(_ellp(I, Bd[k], COAT))
    for s in (1, -1):
        for k in ("shoulder", "haunch"):
            c = dict(T[k]); c["pos"] = (s * T[k]["pos"][0], T[k]["pos"][1], T[k]["pos"][2])
            p.append(_ellp(I, c, COAT))
    return p


def neck_head(hp):
    """-> (prims, Hd head frame in body coords)"""
    T = _T(); N = T["neck"]; Hh = T["head"]; Mn = T["mane"]
    yaw = hp.get("neck_yaw", 0.0)
    Nm = tr(*N["base"]) @ ry(yaw) @ rx(-hp.get("neck", 48))
    poll = pt(Nm, (0, 0, N["len"]))
    Hd = tr(*poll) @ ry(yaw) @ rx(hp.get("head", 52))
    p = [seg(pt(Nm, (0, 0, -1.0)), poll, N["r"][0], COAT, r1=N["r"][1]),                     # neck
         seg(pt(Nm, (0, Mn["lift"][0], -1.5)), pt(Nm, (0, Mn["lift"][1], N["len"] + 0.6)), Mn["r"], POINTS),   # mane (crest)
         seg(pt(Hd, (0, 0.6, -0.4)), pt(Hd, (0, 0.2, Hh["len"])), Hh["r"][0], COAT, r1=Hh["r"][1]),     # head
         _ellp(Hd, Hh["jowl"], COAT),
         _ellp(Hd, Hh["muzzle"], POINTS),
         ell(Hd @ tr(0, 2.6, 0.8), 1.4, 1.0, 1.6, POINTS)]                                    # forelock
    E = Hh["eye"]
    for s in (1, -1):
        p.append(ell(Hd @ tr(s * E["pos"][0], E["pos"][1], E["pos"][2]), E["r"], E["r"], E["r"], "eye"))
        Ea = Hh["ear"]
        b = pt(Hd, (s * Ea["pos"][0], Ea["pos"][1], Ea["pos"][2]))
        tip = pt(Hd, (s * (Ea["pos"][0] + 0.5), Ea["pos"][1] + 0.85 * Ea["len"], Ea["pos"][2] - 0.5 * Ea["len"]))
        p.append(seg(b, tip, Ea["r"], COAT, r1=0.35))
    return p, Hd


def tail_prims(hp):
    T = _T()["tail"]
    a = hp.get("tail", 72)
    d0 = np.array([0.0, -math.sin(math.radians(a)), -math.cos(math.radians(a))])
    a2 = min(88.0, a + 14)
    d1 = np.array([0.0, -math.sin(math.radians(a2)), -math.cos(math.radians(a2))])
    p0 = np.array(T["dock"], float)
    p1 = p0 + T["len"][0] * d0
    p2 = p1 + T["len"][1] * d1
    return [seg(p0, p1, T["r"][0], POINTS, r1=T["r"][1]),
            seg(p1, p2, T["r"][1], POINTS, r1=T["r"][2]),
            ell(tr(*p2), T["r"][2], T["r"][2] * 0.8, T["r"][2], POINTS)]


# ---------------------------------------------------------------- tack (body frame)
def saddle_prims():
    S = _T()["saddle"]
    p = [boxp(np.eye(4), S["seat"], "leather"),
         _ellp(np.eye(4), S["pommel"], "leather"),
         _ellp(np.eye(4), S["cantle"], "leather")]
    k = S["skirt"]
    for s in (1, -1):
        M = tr(s * k["pos"][0], k["pos"][1], k["pos"][2]) @ rz(-s * k["tilt_z"])
        p.append(rbox(M, *k["size"], "leather", r=k["round"]))
    T = _T()
    g = T["saddle_bag"]
    for s in (1, -1):
        p.append(rbox(tr(s * g["pos"][0], g["pos"][1], g["pos"][2]), *g["size"], "leather", r=g["round"]))
    b = T["bedroll"]
    p.append(cyl(tr(*b["pos"]) @ rz(90), b["r"], b["h"], "canvas"))                 # rolled greatcoat / blanket
    return p


def bridle_prims(Hd):
    Br = _T()["bridle"]
    p = [_ellp(Hd, Br["nose"], "leather"), _ellp(Hd, Br["brow"], "leather")]
    bx, by, bz = Br["bit"]
    for s in (1, -1):
        p.append(seg(pt(Hd, (s * 3.1, 1.0, 1.3)), pt(Hd, (s * 2.9, -0.4, Br["nose"]["pos"][2])), Br["r"], "leather"))   # cheek strap
        p.append(ell(Hd @ tr(s * bx, by, bz), 0.7, 0.7, 0.7, "steel"))                              # bit ring
    return p


def bits(Hd):
    bx, by, bz = _T()["bridle"]["bit"]
    return [pt(Hd, (bx, by, bz)), pt(Hd, (-bx, by, bz))]


def carbine_boot(B, carbine=None):
    """leather carbine boot on the horse's right side (butt up beside the cantle, muzzle down); carbine inside if given."""
    C = _T()["carbine_boot"]
    butt = pt(B, C["butt"]); muz = pt(B, C["muzzle"])
    d = muz - butt
    L = float(np.linalg.norm(d)); d = d / L
    p = [seg(butt + d * L * C["cover_from"], muz + d * 0.6, C["r"], "leather", flat=True)]
    if carbine is not None:
        Wc = tune.get("parts", "weapons", "cav_carbine")
        back = -Wc["butt_plate"]["pos"][2]
        M = look_frame(butt + d * back, d, up=B[:3, 2])           # receiver along the boot; trigger side faces the rear
        p += _src(carbine.prims(M), "weapon")
    return p


def saddle_scabbard(B, sheathed=True):
    """steel sabre scabbard strapped to the left of the saddle, hilt forward-up at the throat."""
    S = _T()["sabre_scabbard"]
    a, b = pt(B, S["throat"]), pt(B, S["chape"])
    p = [seg(a, b, S["r"], "steel", flat=True),
         ell(tr(*a), 1.5, 1.0, 1.5, "steel"),
         ell(tr(*b), 1.2, 1.2, 1.2, "steel")]
    if sheathed:
        d = (b - a) / np.linalg.norm(b - a)
        Mg = look_frame(a - d * 2.2, d, up=B[:3, 0])
        p += hilt_prims(Mg)
    return p


# ---------------------------------------------------------------- assembly
class HorseBuild:
    pass


def scaled(q, k):
    """copy of prim q (body frame) uniformly scaled by k about the origin: dims, position, clip plane and paint thresholds."""
    from voxel import Prim
    dims = tuple(v * k for v in q.dims)
    M = q.M.copy(); M[:3, 3] *= k
    paint = [((lambda p, fn=fn: fn(p / k)), m) for fn, m in q.paint]
    r = Prim(q.kind, dims, M, q.mat, paint, None if q.clipy is None else q.clipy * k, q.tag, q.round)
    r.src = q.src
    return r


def build_horse(hp, carbine=None, carbine_in_boot=True, sabre_sheathed=True, tack=True):
    """carbine + carbine_in_boot -> carbine drawn in the saddle boot (empty boot when False / None).
    tack=False: no saddle / saddle scabbard (draught horse, bridle only)."""
    T = _T()
    k = T.get("scale", 1.0)
    legs = []
    for side, nm in ((1, "L"), (-1, "R")):
        legs += front_leg(side, *hp["F" + nm])
        legs += hind_leg(side, *hp["H" + nm])
    nh, Hd = neck_head(hp)
    local = body_prims(hp) + legs + nh + tail_prims(hp) + (saddle_prims() if tack else []) + bridle_prims(Hd)
    local = [scaled(q, k) for q in local]
    roll = 90.0 if hp.get("lie") else hp.get("roll", 0.0)
    B0 = tr(0, T["body_y"] * k, 0) @ rz(roll) @ rx(-hp.get("pitch", 0.0))
    prims = [q.moved(B0) for q in local]
    ymin = min(_prim_ymin(q) for q in prims)
    G = tr(0, -ymin + hp.get("air", 0.0), 0)
    B = G @ B0                                     # orthonormal body frame (scaled coordinates)
    Bs = B @ np.diag([k, k, k, 1.0])               # body frame for unscaled tune points
    prims = [q.moved(G) for q in prims]
    if T.get("carbine_carry", "slung") == "boot":
        prims += carbine_boot(Bs, carbine if carbine_in_boot else None)
    if tack:
        prims += saddle_scabbard(Bs, sabre_sheathed)
    for q in prims:
        if q.src == "body":
            q.src = "mount"
    hb = HorseBuild()
    hb.prims, hb.B = prims, B
    Hs = Hd.copy(); Hs[:3, 3] *= k                 # head frame position in scaled coordinates (bridle points scale with k)
    hb.Hd = B @ Hs
    hb.bits = [pt(Bs, pt(Hd, q)) for q in _bit_local()]
    hb.seat = B @ tr(*(np.array(T["seat_root"]) * k))
    hb.stirrup_from = [pt(Bs, (s * T["stirrup_from"][0], T["stirrup_from"][1], T["stirrup_from"][2])) for s in (1, -1)]
    hb.pommel = pt(Bs, T["saddle"]["pommel"]["pos"])
    return hb


def _bit_local():
    bx, by, bz = _T()["bridle"]["bit"]
    return [(bx, by, bz), (-bx, by, bz)]


# ---------------------------------------------------------------- WWI cavalry carbine (weapon)
def cav_carbine_prims(M):
    W = tune.get("parts", "weapons", "cav_carbine")
    b = W["barrel"]
    return [boxp(M, W["butt_stock"], "wood"),
            boxp(M, W["butt_plate"], "wood"),
            boxp(M, W["wrist"], "wood"),
            boxp(M, W["receiver"], "wood"),
            rbox(M @ tr(-1.8, -0.3, -2.0) @ rz(-35), 0.9, 0.4, 0.4, "steel", r=0.2),   # turned-down bolt handle
            ell(M @ tr(-2.4, -1.2, -2.0), 0.6, 0.6, 0.6, "steel"),
            boxp(M, W["fore"], "wood"),
            boxp(M, dict(W["magazine"], round=0.3), "steel"),
            along_z(M, b["r"], b["z0"], b["z1"], "steel"),
            rbox(M @ tr(0, 1.5, W["front_sight_z"]), 0.4, 0.8, 0.5, "steel", r=0.2),
            rbox(M @ tr(0, -2.5, -3.4), 0.5, 1.0, 1.8, "wood", r=0.3)]


def cav_carbine_overlay(M, kind):
    return _flash(M, tune.get("parts", "weapons", "cav_carbine")["flash_z"], y=0.4) if kind == "flash" else []


_C = tune.get("parts", "weapons", "cav_carbine")
CAV_CARBINE = Weapon(cav_carbine_prims, gripR=_C["grip_right"], gripL=_C["grip_left"], overlay=cav_carbine_overlay)

WEAPONS = {"cav_carbine": CAV_CARBINE}
