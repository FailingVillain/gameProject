"""Extra helmet parts (kept out of headgear.py so other work on that file does not collide).
fn(Hm, band) -> prims. Hm = head frame (origin at the neck, y up, z forward).
Registered into the unit loader's headgear table via unit_loader.py (HEADGEAR = {**HEADGEAR, **HELMETS}).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import voxel as V  # noqa: E402
from voxel import ell, cyl, tr, rx, tband  # noqa: E402
import tune  # noqa: E402
from parts._t import ellp, boxp, at  # noqa: E402

# helmet_fg (steel-grey Stahlhelm paint) is a ramp in tune/materials.json
HELMET_FG = "helmet_fg"


HELMET_FG_D = "helmet_fg_d"      # darker steel: flared rim / neck guard / peak (ramp in tune/materials.json)
HELMET_FG_L = "helmet_fg_l"      # lighter steel: rolled rim edge


def stahlhelm(Hm, band=True):
    """German M1916 Stahlhelm (coal-scuttle): domed steel skull with a flattened crown, rolled rim edge all round, a stepped
    ridge where the skull meets the broad flared neck guard (kicked out behind, with its own rolled edge), short front peak
    with a lit edge line, and the two ventilation lugs at the temples. Team colour = band on the lower skull above the rim.
    Dimensions: tune/parts.json headgear.stahlhelm."""
    mat, dark, lite = HELMET_FG, HELMET_FG_D, HELMET_FG_L
    T = tune.get("parts", "headgear", "stahlhelm")
    Sk = at(Hm, T["frame"])                                               # skull frame, tilted nose-up
    ty = T["team_below_y"]
    paint = [(lambda p: p[:, 1] < ty, tband(mat))] if band else None
    prims = [ell(Sk, *T["skull"]["size"], mat, clipy=T["skull"]["clip_y"], paint=paint),   # skull, flat cut
             ellp(Sk, T["crown"], mat)]                                    # slightly fuller crown (no flat pole)
    prims.append(ellp(Sk, T["rim"], dark))                                 # flared rim all round
    prims.append(ellp(Sk, T["rim_edge"], lite))                            # rolled rim edge (lit lip)
    G = at(Sk, T["guard"])                                                 # neck guard: broad plate flaring back and down
    prims.append(ell(G, *T["guard"]["size"], dark))
    prims.append(ellp(Sk, T["ridge"], lite))                               # step / ridge where skull meets guard
    prims.append(ellp(G, T["guard"]["edge"], lite))                        # rolled rear edge of the guard
    fl = T["flap"]                                                         # ear flaps: side skirts over the ears, flaring outward
    for sx in (-1, 1):
        F = Hm @ tr(sx * fl["x"], fl["y"], fl["z"]) @ rx(fl["tilt"]) @ V.rz(sx * fl["roll"])
        prims.append(ell(F, *fl["size"], dark))
        prims.append(ell(F @ tr(sx * fl["lip"]["x"], fl["lip"]["y"], 0), *fl["lip"]["size"], lite))
        st = fl["step"]
        prims.append(ell(Hm @ tr(sx * st["x"], st["y"], st["z"]) @ rx(fl["tilt"]), *st["size"], dark))
    prims.append(boxp(Hm, T["visor"], dark))                               # short front peak over the eyes
    prims.append(boxp(Hm, T["visor_edge"], lite))                          # lit edge line on its front lip
    lg = T["lug"]
    for sx in (-1, 1):                                                     # ventilation lugs at the temples
        prims.append(ell(Hm @ tr(sx * lg["x"], lg["y"], lg["z"]), *lg["size"], dark))
        prims.append(cyl(Hm @ tr(sx * lg["cap_x"], lg["y"], lg["z"]) @ V.rz(90), lg["cap_r"], lg["cap_h"], "steel"))
    return prims


HELMETS = {
    "stahlhelm": dict(fn=stahlhelm, net=False),
}
