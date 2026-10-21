"""Sniper kit: scoped bolt-action rifle (+ sling), cloth-covered soft cap with hessian strips, shoulder scrim, small back satchel.
All dimensions in tune/parts.json (weapons.scoped_rifle, headgear.sniper_cap, gear.sniper_scrim, gear.sniper_satchel)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, cyl, seg, along_z, tr, rx, pt  # noqa: E402
import tune  # noqa: E402
from parts._t import boxp, ellp  # noqa: E402
from parts.rifles import _flash  # noqa: E402
from parts.headgear import feldkappe  # noqa: E402


def scoped_rifle_prims(M):
    W = tune.get("parts", "weapons", "scoped_rifle")
    b, S = W["barrel"], W["scope"]
    y, r = S["y"], S["r"]
    p = [boxp(M, W["butt_stock"], "wood"), boxp(M, W["butt_plate"], "wood"), boxp(M, W["wrist"], "wood"),
         boxp(M, W["receiver"], "wood"),
         box(M @ tr(-1.9, 0.6, -2.2), 0.9, 0.45, 0.45, "steel", r=0.2),                    # bolt handle (right side)
         ell(M @ tr(-2.9, 0.5, -2.2), 0.65, 0.65, 0.65, "steel"),                          # bolt knob
         boxp(M, W["fore"], "wood"), boxp(M, dict(W["magazine"], round=0.3), "steel"),
         along_z(M, b["r"], b["z0"], b["z1"], "steel"),
         box(M @ tr(0, 1.6, W["front_sight_z"]), 0.4, 0.8, 0.5, "steel"),
         box(M @ tr(0, -2.6, -3.6), 0.5, 1.0, 2.0, "wood")]
    # telescopic sight: tube, objective bell + lens, eyepiece, rings, windage/elevation knob
    p.append(along_z(M, r, S["z0"], S["z1"], "gripdark", flat=False))
    p.append(along_z(M, S["obj_r"], S["obj_z"] - S["obj_len"], S["obj_z"], "steel"))
    p.append(ell(M @ tr(0, y, S["obj_z"] + 0.1), S["obj_r"] * 0.8, S["obj_r"] * 0.8, 0.35, "lens"))
    p.append(along_z(M, S["eye_r"], S["eye_z"], S["eye_z"] + 2.4, "gripdark"))
    for z in S["ring_zs"]:
        p.append(box(M @ tr(0, y - 1.6, z), *S["ring_size"], "steel", r=0.3))
    p.append(cyl(M @ tr(0, y + r + 0.3, 1.8), S["knob_r"], 0.45, "steel"))
    # sling hanging in a shallow arc under the rifle
    L = W["sling"]
    a, c = L["butt"], L["fore"]
    mid = ((a[0] + c[0]) / 2, (a[1] + c[1]) / 2 - L["sag"], (a[2] + c[2]) / 2)
    pa, pm, pc = pt(M, a), pt(M, mid), pt(M, c)
    p.append(seg(pa, pm, L["r"], "leather"))
    p.append(seg(pm, pc, L["r"], "leather"))
    return p


def scoped_rifle_overlay(M, kind):
    return _flash(M, tune.get("parts", "weapons", "scoped_rifle")["flash_z"], y=0.4) if kind == "flash" else []


_S = tune.get("parts", "weapons", "scoped_rifle")
SCOPED_RIFLE = Weapon(scoped_rifle_prims, gripR=_S["grip_right"], gripL=_S["grip_left"], overlay=scoped_rifle_overlay)
WEAPONS = {"scoped_rifle": SCOPED_RIFLE}


def sniper_cap(Hm, band=True):
    """feldkappe (team-colour band) with hessian / leaf cover strips draped over the crown."""
    p = feldkappe(Hm, band)
    for s in tune.get("parts", "headgear", "sniper_cap")["strips"]:
        p.append(ell(_at(Hm, s), *s["size"], s["mat"]))
    return p


def _at(M, c):
    m = M @ tr(*c["pos"])
    return m @ rx(c["tilt"]) if c.get("tilt") else m


def sniper_scrim(T):
    return [box(_at(T, s), *s["size"], s["mat"], r=0.35) for s in tune.get("parts", "gear", "sniper_scrim")["strips"]]


def sniper_satchel(T):
    G = tune.get("parts", "gear", "sniper_satchel")
    s = G["strap"]
    p = [boxp(T, G["body"], "canvas"), boxp(T, G["flap"], "canvas")]
    for sg in (1, -1):
        p.append(box(T @ tr(sg * s["x"], s["y"], s["z"]), *s["size"], "leather"))
    return p


GEAR = {"sniper_scrim": sniper_scrim, "sniper_satchel": sniper_satchel}
HEADGEAR = {"sniper_cap": dict(fn=sniper_cap, net=False)}
