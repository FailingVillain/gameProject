"""Chinese dadao (big knife): broad, heavy, single-edged blade that widens toward the tip, long two-hand-capable grip with a
ring pommel, simple round guard. Same weapon frame as blades.SABRE (origin at the guard, z = blade direction, grip toward -z,
cutting edge = convex side facing local -y), so bases/officer.py's edge_toward / straight_arm work unchanged.
All tunables are module constants (move to JSON at merge)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, seg, along_z, tr, rx, pt  # noqa: E402
import numpy as np  # noqa: E402
import voxel as V  # noqa: E402

# darker, greyer blade steel (the sabre's "bright" is almost white and vanishes against the light blue-grey uniform)
DADAO_STEEL_ANCHORS = [(54, 60, 70), (78, 85, 96), (102, 110, 122), (126, 134, 146)]
DADAO_EDGE_ANCHORS = [(96, 104, 116), (118, 126, 138), (140, 148, 160), (162, 170, 182)]     # slightly lighter edge line (not white)
for _n, _a in (("dadao_steel", DADAO_STEEL_ANCHORS), ("dadao_edge", DADAO_EDGE_ANCHORS)):
    if _n not in V.MATERIALS:
        V.MATERIALS[_n] = V._ramp8([c + (255,) for c in _a])
        V.MAT_NAMES.append(_n)
        V.MAT_IDX[_n] = len(V.MAT_NAMES) - 1

# blade: (segment length, pitch deg, half-width). Total ~ DADAO_LEN; spine stays straight-ish at +y, belly (edge) swells
# toward the tip then drops into a sloped point. Officer sabre = 22 long / 0.9 half-width; dadao = shorter, ~2.3x broader.
DADAO_BLADE = ((4.6, 0.0, 1.6), (4.4, 1.5, 2.1), (3.8, 3.5, 2.5), (2.8, 6.0, 2.8), (1.6, 12.0, 1.6))
DADAO_THICK = 0.65            # blade half-thickness (sabre 0.3)
DADAO_GRIP_Z = (-0.4, -7.6)   # grip span (sabre: -5.2..-0.2)
DADAO_GRIP_R = -2.6           # right-hand grip point (keeps officer straight_arm geometry valid)
DADAO_GRIP_L = -5.6           # left-hand grip point (two-hand capable)
RING_C, RING_R = -8.9, 1.3    # ring pommel centre z and radius
DADAO_LEN = sum(s[0] for s in DADAO_BLADE)
SHEATH_GUARD_UP = 1.8


def hilt_prims(M):
    z0, z1 = DADAO_GRIP_Z
    p = [along_z(M, 0.95, z1, z0, "gripdark")]            # dark core, thick enough to read at 64 px
    n = int((z0 - z1) / 0.8)
    for k in range(n):                                              # leather / cord wrap bands
        zc = z0 - 0.5 - 0.8 * k
        p.append(along_z(M, 1.2, zc - 0.25, zc + 0.25, "leather"))
    for q in p:
        q.tag = "hilt_grip"
    g = [ell(M @ tr(0, 0, 0.1), 2.3, 2.3, 0.5, "brass"),             # simple round guard disc
         ell(M @ tr(0, 0, -0.5), 1.3, 1.3, 0.8, "brass")]              # collar behind it
    for q in g:
        q.tag = "hilt_guard"
    ring = []
    N = 9
    for i in range(N):                                               # pommel ring in the blade plane (y-z)
        a0, a1 = 2 * math.pi * i / N, 2 * math.pi * (i + 1) / N
        ring.append(seg(pt(M, (0, RING_R * math.cos(a0), RING_C + RING_R * math.sin(a0))),
                        pt(M, (0, RING_R * math.cos(a1), RING_C + RING_R * math.sin(a1))), 0.45, "brass"))
    ring.append(ell(M @ tr(0, 0, z1 - 0.3), 1.0, 1.0, 0.7, "brass"))   # cap joining ring and grip
    for q in ring:
        q.tag = "hilt_pommel"
    return p + g + ring


def dadao_prims(M, blade=DADAO_BLADE):
    p = hilt_prims(M)
    y, z = 0.0, 0.5
    base_hw = blade[0][2]
    for i, (L, a, hw) in enumerate(blade):
        r = math.radians(a)
        cy, cz = y + math.sin(r) * L / 2, z + math.cos(r) * L / 2
        # keep the spine line straight: shift the box centre down by the extra half-width (edge side = -y)
        p.append(box(M @ tr(0, cy, cz) @ rx(-a) @ tr(0, -(hw - base_hw), 0), DADAO_THICK, hw, L / 2 + 0.5, "dadao_steel", r=0.3))
        p.append(box(M @ tr(0, cy, cz) @ rx(-a) @ tr(0, -(hw - base_hw) - hw + 0.3, 0), DADAO_THICK + 0.05, 0.3, L / 2 + 0.5, "dadao_edge", r=0.15))   # lighter edge line
        p.append(box(M @ tr(0, cy + base_hw * 0.9 - 0.0, cz) @ rx(-a), DADAO_THICK + 0.1, 0.32, L / 2 + 0.3, "steel", r=0.15))   # thick spine
        y, z = y + math.sin(r) * L, z + math.cos(r) * L
    return p


DADAO = Weapon(dadao_prims, gripR=(0, 0, DADAO_GRIP_R), gripL=(0, 0, DADAO_GRIP_L))


SHEATH_X = 9.8      # scabbard throat x (left hip) - was inside the thigh
SHEATH_OUT = 5    # sideways drift of the tip
SHEATH_Y = -1.6
SHEATH_Z = 5.2


def dadao_sheath(T):
    """broad leather scabbard on the left hip, tip back and down; the hilt (ring pommel) stands up out of the throat."""
    k = 0.82
    a, b = pt(T, (SHEATH_X, SHEATH_Y, SHEATH_Z)), pt(T, (SHEATH_X + SHEATH_OUT * k, SHEATH_Y - 20.9 * k, SHEATH_Z - 7.0 * k))
    p = [seg(a, b, 1.5, "leather", flat=True),
         ell(T @ tr(SHEATH_X, SHEATH_Y, SHEATH_Z), 1.9, 1.2, 1.9, "steel"),                                 # throat
         ell(T @ tr(SHEATH_X + SHEATH_OUT * k, SHEATH_Y - 20.9 * k, SHEATH_Z - 7.0 * k), 1.5, 1.5, 1.5, "steel"),  # chape
         seg(pt(T, (9.0, 0.4, 1.0)), a, 0.5, "leather")]
    d = (b - a) / np.linalg.norm(b - a)
    x = T[:3, 0] - np.dot(T[:3, 0], d) * d
    x = x / np.linalg.norm(x)
    Mg = np.eye(4)
    Mg[:3, 0], Mg[:3, 1], Mg[:3, 2] = x, np.cross(d, x), d
    Mg[:3, 3] = a - d * SHEATH_GUARD_UP
    return p + hilt_prims(Mg)


WEAPONS = {"dadao": DADAO}
GEAR = {"dadao_sheath": dadao_sheath}
