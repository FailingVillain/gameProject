"""Gear worn on the torso. fn(Tm) -> prims. Tm = torso frame (origin at the belt line, y up, z forward, -z = back)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import rbox as box, ell, cyl, seg, tr, rx, pt  # noqa: E402
import tune  # noqa: E402
from parts._t import ellp, boxp, cylp  # noqa: E402


def field_pack(T, cloth="canvas"):
    """small field pack (pillow-soft) + rolled bedroll capsule on top, with shoulder straps.
    cloth = material of the pack body and bedroll (straps unchanged). The olive e-tool cover box was removed (green rectangle on the back)."""
    G = tune.get("parts", "gear", "field_pack")
    r = G["roll"]
    p = [ellp(T, G["body"], cloth),                                                 # pack body: oval stuffed bag
         seg(pt(T, r["p0"]), pt(T, r["p1"]), r["r"], cloth)]                        # rolled blanket (capsule)
    s = G["strap"]
    for sg in (1, -1):
        p.append(box(T @ tr(sg * s["x"], s["y"], s["z"]), *s["size"], "leather"))  # straps
    return p


def canteen(T):
    return [cyl(T @ tr(-8.4, -1.0, -4.7) @ rx(90), 3.0, 1.6, "olive")]


def ammo_pouches(T):
    """two rifle ammo pouches on the front of the belt."""
    return [box(T @ tr(s * 5.6, 0.7, 6.1), 2.4, 2.0, 1.4, "canvas", r=0.8) for s in (1, -1)]


def binocular_case(T):
    return [box(T @ tr(4.5, 7.0, 6.9), 2.8, 3.0, 1.6, "leather"),
            box(T @ tr(4.5, 10.2, 6.9), 0.7, 0.4, 1.7, "leather")]


def leather_pouches(T):
    """WWI leather cartridge pouches: two boxes each side of the belt buckle."""
    G = tune.get("parts", "gear", "leather_pouches")
    return [box(T @ tr(s * x, G["y"], G["z"]), *G["size"], "leather", r=G["round"]) for s in (1, -1) for x in G["xs"]]


def haversack(T):
    """cloth haversack on the left hip."""
    G = tune.get("parts", "gear", "haversack")
    return [boxp(T, G["bag"], "canvas"),
            boxp(T, G["flap"], "canvas")]                                        # flap


def canteen_cloth(T):
    """cloth-covered canteen on the right hip rear."""
    G = tune.get("parts", "gear", "canteen_cloth")
    return [cyl(T @ tr(*G["body"]["pos"]) @ rx(90), G["body"]["r"], G["body"]["h"], "khaki"),
            cylp(T, G["cap"], "steel")]                                          # cap


def field_pack_grey(T):
    """field pack in the tunic's field-grey family (infantry-default)."""
    return field_pack(T, cloth="feldgrau")


def pack_horseshoe(T):
    """Japanese: field pack (khaki canvas) with the greatcoat rolled in a horseshoe arch over the top and down both sides."""
    G = tune.get("parts", "gear", "field_pack")
    p = [ellp(T, G["body"], "khaki")]
    s = G["strap"]
    for sg in (1, -1):
        p.append(box(T @ tr(sg * s["x"], s["y"], s["z"]), *s["size"], "leather"))
    arch = [(-7.6, 6.0, -8.6), (-8.2, 10.0, -9.6), (-6.8, 14.6, -10.0), (-3.4, 17.2, -10.2), (0, 18.0, -10.2),
            (3.4, 17.2, -10.2), (6.8, 14.6, -10.0), (8.2, 10.0, -9.6), (7.6, 6.0, -8.6)]
    pts = [pt(T, q) for q in arch]
    p += [seg(a, b, 1.9, "dkhaki") for a, b in zip(pts, pts[1:])]
    p += [ell(T @ tr(sg * 7.6, 6.0, -8.6), 2.0, 2.0, 2.0, "dkhaki") for sg in (1, -1)]
    p.append(box(T @ tr(0, 17.6, -10.2), 0.5, 2.2, 2.2, "leather"))                            # tie strap
    return p


def bandolier_x(T, sink=0.0, t0=0.0, rad=0.7):
    """Fengtian: two cloth cartridge bandoliers crossing in an X on the chest and again on the back (replaces the pack)."""
    import numpy as np
    out = []
    for face in (1, -1):                       # front, back
        ys = np.linspace(t0 if face > 0 else 0.0, 1, 8)
        for sg in (1, -1):
            pts = []
            for t in ys:
                x = sg * (5.5 - 12.5 * t) ; y = 12.0 - 11.0 * t
                zc = 0.3 + 6.4 * np.sqrt(max(0.0, 1 - (x / 11.0) ** 2 - ((y - 12.0) / 7.0) ** 2))
                zw = 5.7 * np.sqrt(max(0.0, 1 - (x / 9.9) ** 2))
                z = max(zc, zw if y < 9 else 0)
                zz = face * (z + 0.3 - sink) if face > 0 else -(max(z - 0.6, 0.0) + 0.5)
                pts.append((x, y, zz))
            P = [pt(T, q) for q in pts]
            out += [seg(a, b, rad, "canvas", flat=True) for a, b in zip(P, P[1:])]
            if face > 0:
                out += [ell(T @ tr(*pts[i]), 0.6, 0.6, 0.4, "leather") for i in (2, 3, 4, 5, 6)]   # cartridge pockets
    return out


def bandolier_x_low(T):
    """bandolier_x front straps start at the X crossing (t0=0.25): heavy kneel / prone poses bring the left forearm across the upper chest where full front straps clipped it; back X unchanged."""
    return bandolier_x(T, t0=0.5)


def bandolier_x_tight(T):
    """Front X hugging the chest: sunk into the torso, thinner, shoulder tips trimmed 25% (t0) so the arm in kneel/prone poses clears it; the X crossing stays. Pair with weapon_fix pole/offset entries."""
    return bandolier_x(T, sink=SINK_T, rad=RAD_T, t0=0.25)


SINK_T, RAD_T = 1.5, 0.55
GEAR = {"bandolier_x_tight": bandolier_x_tight, "bandolier_x_low": bandolier_x_low, "pack_horseshoe": pack_horseshoe, "bandolier_x": bandolier_x, "field_pack": field_pack, "field_pack_grey": field_pack_grey, "canteen": canteen, "ammo_pouches": ammo_pouches, "binocular_case": binocular_case,
        "leather_pouches": leather_pouches, "haversack": haversack, "canteen_cloth": canteen_cloth}
