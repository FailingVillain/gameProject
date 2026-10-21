"""French M1915 Adrian helmet. fn(Hm, band) -> prims (Hm = head frame: origin at the neck, y up, z forward).
Registered into the unit loader's headgear table via unit_loader.py (HEADGEAR = {..., **ADRIAN})."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import voxel as V  # noqa: E402
from voxel import ell, rbox, tr, rx, tband  # noqa: E402

# horizon-blue / grey-blue helmet paint
HELMET_ADRIAN = "helmet_adrian"
_ANCH = [(52, 62, 78), (74, 88, 108), (100, 116, 136), (132, 148, 166)]
if HELMET_ADRIAN not in V.MATERIALS:
    V.MATERIALS[HELMET_ADRIAN] = V._ramp8([tuple(a[:3]) + (255,) for a in _ANCH])
    V.MAT_NAMES.append(HELMET_ADRIAN)
    V.MAT_IDX[HELMET_ADRIAN] = len(V.MAT_NAMES) - 1


# ---- tunables (move to JSON at merge). Head frame: origin at the neck; head ellipsoid spans y -3.2..8.4, forehead ~y 5-6.
ADRIAN_BASE_Y = 5.7          # helmet lower edge (v1: 2.0 -> covered the eyes/forehead)
ADRIAN_BAND_H = 1.0          # team band height above the lower edge
ADRIAN_COMB = dict(hx=0.6, hy=0.8, hz=4.6, cy=12.2, end_r=0.6)      # crest: lower + slimmer than v1 (0.95 / 1.5 / 5.6 at y 12.0)
ADRIAN_BRIM = dict(y=5.9, rx=6.9, ry=0.4, rz=7.0)                  # side brim ring, thin
ADRIAN_VISOR = dict(y=6.4, z=7.0, hx=3.6, hy=0.28, hz=1.0, pitch=0)     # front visor raised above the brow, short
ADRIAN_REAR = dict(y=6.0, z=-7.6, hx=3.8, hy=0.28, hz=1.6, pitch=-8)     # rear brim, shorter than v1


def adrian(Hm, band=True):
    """M1915 Adrian (v2): smaller details so the face stays visible. Two smooth skull segments, a low slim front-to-back comb,
    thin continuous brim (short raised front visor + short rear brim), fine rim lines; team colour band at the lower edge."""
    mat = HELMET_ADRIAN
    by = ADRIAN_BASE_Y
    paint = [(lambda p: p[:, 1] < -2.6 + ADRIAN_BAND_H, tband(mat))] if band else None
    prims = [ell(Hm @ tr(0, by + 1.9, -0.4), 6.6, 3.6, 7.0, mat, clipy=-1.9, paint=paint),      # lower skull (band zone), bottom at by
             ell(Hm @ tr(0, 8.8, -0.4), 5.4, 3.6, 5.8, mat)]                                       # dome
    c = ADRIAN_COMB
    prims.append(rbox(Hm @ tr(0, c["cy"], -0.4), c["hx"], c["hy"], c["hz"], mat, r=0.55))
    prims.append(ell(Hm @ tr(0, c["cy"] - 0.4, 4.0), c["end_r"], c["hy"], 1.2, mat))              # front end, rounded
    prims.append(ell(Hm @ tr(0, c["cy"] - 0.7, -4.8), c["end_r"], c["hy"], 1.2, mat))             # rear end, rounded
    b = ADRIAN_BRIM
    prims.append(ell(Hm @ tr(0, b["y"], -0.4), b["rx"], b["ry"], b["rz"], mat))                   # thin side brim
    v = ADRIAN_VISOR
    prims.append(rbox(Hm @ tr(0, v["y"], v["z"]) @ rx(v["pitch"]), v["hx"], v["hy"], v["hz"], mat, r=0.25))
    r = ADRIAN_REAR
    prims.append(rbox(Hm @ tr(0, r["y"], r["z"]) @ rx(r["pitch"]), r["hx"], r["hy"], r["hz"], mat, r=0.25))
    prims.append(rbox(Hm @ tr(0, v["y"] - 0.2, v["z"] + v["hz"]) @ rx(v["pitch"]), v["hx"] - 0.4, 0.15, 0.2, mat, r=0.1))    # visor rim line
    prims.append(rbox(Hm @ tr(0, r["y"] - 0.2, r["z"] - r["hz"]) @ rx(r["pitch"]), r["hx"] - 0.4, 0.15, 0.2, mat, r=0.1))   # rear rim line
    return prims


HELMETS = {"adrian": dict(fn=adrian, net=False)}
