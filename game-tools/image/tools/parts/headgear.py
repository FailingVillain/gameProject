"""Headgear parts. fn(Hm, band) -> prims. Hm = head frame (origin at the neck, y up, z forward)."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import ell, rbox, cyl, seg, tr, rx, ry, pt, tband  # noqa: E402
import tune  # noqa: E402
from parts._t import ellp, boxp, cylp  # noqa: E402


def m1_helmet(Hm, band=True, mat="helmet"):
    """US M1 steel helmet, bare shell, team band around the lower shell."""
    T = tune.get("parts", "headgear", "m1_helmet")
    ty = T["team_below_y"]
    paint = [(lambda p: (p[:, 1] < ty), tband(mat))] if band else None
    return [ellp(Hm, T["shell"], mat, clipy=T["shell"]["clip_y"], paint=paint),
            ellp(Hm, T["brim"], mat)]                                            # brim overhang


def m1_helmet_net(Hm, band=True):
    """M1 helmet with net + hanging scrim strips (darker shell material, dithered net in post)."""
    p = m1_helmet(Hm, band, mat="helmet_v")
    for k, (sx, sz) in enumerate(((-8.6, 1.0), (8.6, 1.0), (0.0, 9.4), (-5.6, -7.2), (5.8, -7.0))):
        p.append(rbox(Hm @ tr(sx, 5.2, sz), 1.2, 2.4, 1.2, "scrim", r=0.5))
        p.append(rbox(Hm @ tr(sx * 0.6, 9.6 + (k % 2), sz * 0.5), 1.4, 0.8, 1.4, "scrim", r=0.4))
    return p


def side_cap(Hm, band=True):
    """soft side cap (garrison cap): low wedge crown, turned-up folded edge (carries the team band), front badge."""
    paint = [(lambda p: p[:, 1] > -0.3, tband("capf"))] if band else None
    return [ell(Hm @ tr(0, 6.0, 0.6), 5.0, 1.7, 7.0, "capf", clipy=-0.9, paint=paint),   # folded edge / curtain
            ell(Hm @ tr(0, 7.0, 0.6), 3.9, 2.3, 6.5, "cap"),                             # soft crown
            ell(Hm @ tr(0, 8.5, 0.6), 1.3, 1.4, 5.8, "cap"),                             # centre ridge
            ell(Hm @ tr(0, 6.6, 6.6), 0.9, 0.9, 0.5, "steel")]                           # badge


def officer_cap(Hm, band=True):
    """large peaked service cap: wide saucer crown, tall cloth band (team colour), leather visor, front badge.
    Deliberately bigger than the infantry field caps so the officer reads at 64 / 32 too."""
    T = tune.get("parts", "headgear", "officer_cap")
    cap = T.get("cap_mat", "feldgrau")                                                     # same field grey as the infantry tunic / Feldkappe (unit tune may name a darker ramp)
    return [ellp(Hm, T["band"], tband(cap) if band else cap),                                  # band
            ellp(Hm, T["crown"], cap),                                                     # crown sides
            ellp(Hm, T["saucer"], cap),                                                    # wide saucer top
            ellp(Hm, T["visor"], cap),                                                     # visor: same cloth as the crown (no separate leather peak)
            ellp(Hm, T["badge"], "bright")]                                                # badge


def japanese_officer_cap(Hm, band=True):
    """officer_cap geometry in the Japanese loess khaki."""
    cap = "jkhaki"
    T = tune.get("parts", "headgear", "officer_cap")
    return [ellp(Hm, T["band"], tband(cap) if band else cap), ellp(Hm, T["crown"], cap), ellp(Hm, T["saucer"], cap),
            ellp(Hm, T["visor"], cap), ellp(Hm, T["badge"], "bright")]


def feldkappe(Hm, band=True):
    """WWI Austro-Hungarian style field cap: soft kepi body, flat top sloping a little forward, fold-down flaps buttoned up
    around the lower half (the flap band carries the team colour; two small front buttons), short visor. No badges."""
    T = tune.get("parts", "headgear", "feldkappe")
    cap = T.get("cap_mat", "feldgrau")                                                     # one unified field-grey cloth colour (crown, flaps, peak); unit tune may name a darker ramp
    flap = tband(cap) if band else cap
    bt = T["button"]
    return [cylp(Hm, T["crown"], cap),                                                      # crown sides
            ellp(Hm, T["top"], cap),                                                        # flat top, front edge lower
            cylp(Hm, T["band"], flap),                                                      # buttoned-up flaps (band)
            ell(Hm @ tr(-bt["x"], bt["y"], bt["z"]), *bt["size"], "steel"),                 # front buttons
            ell(Hm @ tr(bt["x"], bt["y"], bt["z"]), *bt["size"], "steel"),
            boxp(Hm, T["visor"], cap)]                                                      # stiff peak (same cloth; reads via shading + depth line)


def japanese_cap(Hm, band=True):
    """WWI Japanese Type 45 style field cap: flat round top that overhangs the band a little, short straight cloth sides, a
    distinct cloth band (team colour), small stiff dark leather visor angled down, thin leather chin strap across the front.
    No badge, no neck flap (unlike the Feldkappe: sloping top + buttoned flaps)."""
    cloth = "jkhaki"
    band_mat = tband(cloth) if band else cloth
    T = tune.get("parts", "headgear", "japanese_cap")
    sr = T["strap"]["r"]
    pts = [pt(Hm, q) for q in T["strap"]["points"]]
    wg = T["wing"]
    out = [cylp(Hm, T["crown"], cloth),                                                    # straight cylindrical sides
           cylp(Hm, T["top"], cloth),                                                      # flat round top, overhangs the sides
           cylp(Hm, T["band"], band_mat),                                                  # distinct cloth band (team colour)
           boxp(Hm, T["visor"], "visor")]                                                  # visor centre: short front-to-back, angled down
    for sx in (1, -1):                                                                     # wings curve back round the band
        out.append(rbox(Hm @ tr(sx * wg["x"], wg["y"], wg["z"]) @ ry(sx * wg["yaw"]) @ rx(wg["tilt"]), *wg["size"], "visor", r=wg["round"]))
    out += [seg(p0, p1, sr, "leather") for p0, p1 in zip(pts, pts[1:])]                    # thin chin strap: side -> across the front -> side
    return out


PICKEL_SPIKE_H = 5.8      # spike length above the boss (model units)
PICKEL_BAND_Y = -0.2      # skull-local y below which the thin team band is painted
CAP_PIPE_T = 0.4          # infantry-reserve: radial thickness of the red crown-edge piping (model units)
BAND_PIPE_T = 0.3         # red line along the top / bottom edge of the cap band


def japanese_cap_old(Hm, band=True):
    """Type 45 style cap (as japanese_cap) in very dark navy with thin red piping: crown top edge and both band edges.
    Team colour stays on the band."""
    cloth, pipe = "navy_old", "piping_red"
    band_mat = tband(cloth) if band else cloth
    rim = lambda r, t: [(lambda p: np.hypot(p[:, 0], p[:, 2]) > r - t, pipe)]
    T = tune.get("parts", "headgear", "japanese_cap")
    crown = cylp(Hm, T["top"], cloth, paint=rim(T["top"]["r"], CAP_PIPE_T))
    bnd = cylp(Hm, T["band"], band_mat, paint=[(lambda p: np.abs(p[:, 1]) > T["band"]["h"] - BAND_PIPE_T, pipe)])
    parts = japanese_cap(Hm, band)
    rest = parts[3:]
    for q in rest:
        if q.mat == "visor":
            q.mat = cloth                                                                  # peak in the same navy as the cap (no separate black visor)
    return [cylp(Hm, T["crown"], cloth), crown, bnd] + rest


def pickelhaube(Hm, band=True):
    """Spiked leather helmet: rounded black-leather skull, front visor + rear neck peak, steel spike on a cruciform base,
    plain brass front plate (no emblem), brass trim, thin team-colour band at the lower skull. Constants: see PICKEL_* ."""
    lea = "pickel_leather"
    paint = [(lambda p: p[:, 1] < PICKEL_BAND_Y, tband(lea))] if band else None
    top = 7.0 + 4.6                                                                        # skull apex (head-frame y)
    p = [ell(Hm @ tr(0, 7.0, 0.4), 6.0, 4.6, 6.2, lea, clipy=-1.0, paint=paint),           # skull
         rbox(Hm @ tr(0, 5.0, 6.7) @ rx(16), 4.6, 0.35, 1.9, lea, r=0.3),                  # front visor
         rbox(Hm @ tr(0, 5.3, -6.5) @ rx(-22), 3.8, 0.35, 1.6, lea, r=0.3),                # rear neck peak
         ell(Hm @ tr(0, 8.6, 6.0), 2.0, 2.4, 0.55, "brass"),                               # plain front plate
         rbox(Hm @ tr(0, top - 0.1, 0.4), 2.7, 0.4, 0.4, "brass", r=0.2),                  # cruciform base (x arm)
         rbox(Hm @ tr(0, top - 0.1, 0.4), 0.4, 0.4, 2.7, "brass", r=0.2),                  # (z arm)
         ell(Hm @ tr(0, top + 0.3, 0.4), 1.1, 0.8, 1.1, "brass"),                          # spike boss
         seg(pt(Hm, (0, top + 0.4, 0.4)), pt(Hm, (0, top + 0.4 + PICKEL_SPIKE_H, 0.4)), 0.62, "steel", r1=0.1)]   # long slim spike
    for s in (1, -1):                                                                      # brass side rosettes
        p.append(ell(Hm @ tr(s * 5.9, 6.0, 0.4), 0.5, 0.9, 0.9, "brass"))
    return p


KEPI_R = 5.55             # korean_kepi: crown radius (model units); band is a little wider
KEPI_BAND_R = 5.85
KEPI_PIPE_T = 0.45        # radial thickness of the red piping on the flat top edge


def _acc():
    """tune parts.headgear.korean_kepi: accent materials (defaults = original red/brass look)."""
    d = {"band": "band_darkred", "rim": "piping_red", "petal": "brass", "centre": "piping_red", "stripe": "brass"}
    try:
        d.update(tune.get("parts", "headgear", "korean_kepi"))
    except KeyError:
        pass
    return d


def _plum_badge(Hm, x, y, z):
    """tiny plum blossom (5 round petals + centre), brass, on the cap front. Symmetric about the cap centre line."""
    A = _acc()
    out = []
    for k in range(5):
        a = np.radians(90 + 72 * k)
        out.append(ell(Hm @ tr(x + 0.85 * np.cos(a), y + 0.85 * np.sin(a), z), 0.55, 0.55, 0.3, A["petal"]))
    out.append(ell(Hm @ tr(x, y, z + 0.1), 0.38, 0.38, 0.3, A["centre"]))
    return out


def _kepi(Hm, extra_h, officer, band=True):
    """Korean Empire (daehan jeguk-gun) style kepi: short upright cylinder, slightly tapered, FLAT top, team-colour band,
    thin red top-edge piping, short straight black-leather visor, thin chin strap, brass plum-blossom badge.
    The band is a team slot (drawn into the team layer, like helmet_band); the officer gold stripe sits on top of it."""
    A = _acc()
    cloth, red, blk = "fgrey_dark", A["rim"], "pickel_leather"
    h = extra_h
    rim = [(lambda p: np.hypot(p[:, 0], p[:, 2]) > KEPI_R - KEPI_PIPE_T, red)]
    band_mat = tband(A["band"]) if band else A["band"]      # FIXED dark-red band (not team colour); headband overlay may cover it
    out = [cyl(Hm @ tr(0, 5.45, 0.3), KEPI_BAND_R, 1.0, band_mat),                        # team-colour cap band
           cyl(Hm @ tr(0, 6.95 + h * 0.4, 0.3), KEPI_R + 0.05, 0.85 + h * 0.4, cloth),        # lower crown
           cyl(Hm @ tr(0, 8.5 + h, 0.3), KEPI_R - 0.2, 0.8 + h * 0.6, cloth),                 # upper crown (slight taper)
           cyl(Hm @ tr(0, 9.55 + h * 2, 0.3), KEPI_R - 0.2, 0.25, cloth, paint=rim),          # flat top + red edge piping
           rbox(Hm @ tr(0, 4.55, 7.3) @ rx(9), 4.5 + (0.3 if officer else 0), 0.4, 1.55, blk, r=0.3)]   # short straight visor
    for sx in (1, -1):                                                                     # thin chin strap resting on the band bottom
        pts = [(sx * 5.9, 4.6, 0.3), (sx * 4.3, 4.6, 4.5), (0.0, 4.6, 6.4)]
        out += [seg(pt(Hm, a), pt(Hm, b), 0.28, blk) for a, b in zip(pts, pts[1:])]
    out += _plum_badge(Hm, 0.0, 5.5, 6.1)
    if officer:
        out.append(cyl(Hm @ tr(0, 5.45, 0.3), KEPI_BAND_R + 0.08, 0.22, A["stripe"]))          # gold band stripe
    return out


def korean_kepi(Hm, band=True):
    return _kepi(Hm, 0.0, False, band)


def korean_kepi_officer(Hm, band=True):
    return _kepi(Hm, 0.7, True, band)


def bare(Hm, band=True):
    return []


HEADGEAR = {
    "m1_helmet": dict(fn=m1_helmet, net=False),
    "m1_helmet_net": dict(fn=m1_helmet_net, net=True),
    "side_cap": dict(fn=side_cap, net=False),
    "officer_cap": dict(fn=officer_cap, net=False),
    "feldkappe": dict(fn=feldkappe, net=False),
    "japanese_officer_cap": dict(fn=japanese_officer_cap, net=False),
    "japanese_cap": dict(fn=japanese_cap, net=False),
    "japanese_cap_old": dict(fn=japanese_cap_old, net=False),
    "pickelhaube": dict(fn=pickelhaube, net=False),
    "korean_kepi": dict(fn=korean_kepi, net=False),
    "korean_kepi_officer": dict(fn=korean_kepi_officer, net=False),
    "none": dict(fn=bare, net=False),
}
