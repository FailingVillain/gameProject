"""Crew-served-style infantry weapons: bipod light machine gun and anti-tank rifle (generic WWI, no national markings).
Weapon frame as parts/rifles.py: origin at the receiver, z = muzzle direction, y up.
Both carry two bipod extras a pose can request with attach=[...]: "bipod_folded" (legs along the barrel) / "bipod_open" (legs planted;
local y = -5.4 is where the feet touch the ground when the pose puts the weapon at torso depth z = 1.2 in the prone figure)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, cyl, seg, along_z, tr, pt  # noqa: E402

FOOT_Y = -5.4


def _flash(M, z, k=1.0, y=0.4):
    return [ell(M @ tr(0, y, z + 2.4 * k), 3.8 * k, 3.8 * k, 5.0 * k, "flash_edge"),
            ell(M @ tr(0, y, z + 2.2 * k), 2.7 * k, 2.7 * k, 4.0 * k, "flash"),
            ell(M @ tr(0, y, z + 1.8 * k), 1.6 * k, 1.6 * k, 2.4 * k, "flash_core")]


def _bipod_open(M, zm, spread=4.2, dz=3.4, r=0.6, foot=None):
    foot = FOOT_Y if foot is None else foot
    p = [along_z(M, 1.5, zm - 0.7, zm + 0.7, "rifmetal")]                                  # clamp collar
    for s in (1, -1):
        p.append(seg(pt(M, (s * 0.9, -1.4, zm)), pt(M, (s * spread, foot, zm + dz)), r, "steel"))
        p.append(box(M @ tr(s * spread, foot + 0.2, zm + dz + 0.3), 0.7, 0.35, 1.0, "steel", r=0.2))   # foot
    return p


# sandbag parapet under the bipod of the kneeling LMG: world-aligned (figure space, y up), ground / top height set by bases/infantry_heavy.py
SANDBAG = dict(ground=0.0, top=20.0)


def _sandbag_wall(M, zf, layers=3, bag_hx=3.7, bag_hz=3.7):
    """stack of hessian sandbags (3 layers, running bond) centred under the bipod feet, wall runs across the barrel axis."""
    import math
    P = pt(M, (0.0, 0.0, zf)); ax = M[:3, :3] @ (0.0, 0.0, 1.0)
    yaw = math.degrees(math.atan2(ax[0], ax[2]))
    from voxel import ry
    g, top = SANDBAG["ground"], SANDBAG["top"]
    lh = (top - g) / layers
    out = []
    for i in range(layers):
        xs = (-2 * bag_hx, 0.0, 2 * bag_hx) if i % 2 == 0 else (-bag_hx, bag_hx)
        if i == layers - 1:
            xs = (-bag_hx, bag_hx)                               # top course: the two bipod feet rest on these
        for x in xs:
            Mb = tr(P[0], g + (i + 0.5) * lh, P[2]) @ ry(yaw) @ tr(x, 0.0, 0.0)
            out.append(box(Mb, bag_hx - 0.15, lh / 2 - 0.1, bag_hz, "hessian", r=lh * 0.32))
    return out


def _bipod_folded(M, zm, length, y=-2.5, r=0.55):
    return [seg(pt(M, (s * 1.0, y, zm)), pt(M, (s * 0.8, y - 0.2, zm + length)), r, "steel") for s in (1, -1)] + \
           [along_z(M, 1.5, zm - 0.7, zm + 0.7, "rifmetal")]


# ---------------------------------------------------------------- light machine gun (Lewis / Madsen-like): pan magazine, cooling shroud, wooden stock
def lmg_prims(M):
    return [box(M @ tr(0, -0.7, -11.5), 1.3, 2.3, 5.4, "wood"),                    # butt stock
            box(M @ tr(0, -1.0, -17.0), 1.5, 3.0, 0.7, "steel"),                   # butt plate
            box(M @ tr(0, 0.1, -1.0), 1.8, 2.1, 5.2, "rifmetal"),                  # receiver
            cyl(M @ tr(0, 3.6, -0.2), 4.4, 1.5, "rifmetal"),                       # pan magazine (flat drum on top)
            cyl(M @ tr(0, 5.2, -0.2), 1.3, 0.35, "steel"),                         # drum hub
            box(M @ tr(0, -3.0, -4.3), 0.7, 1.6, 0.8, "wood"),                     # pistol grip
            box(M @ tr(0, -2.3, -1.8), 0.35, 0.5, 1.5, "steel", r=0.15),           # trigger guard
            box(M @ tr(1.9, 0.4, -4.4), 0.6, 0.5, 0.5, "steel", r=0.2),            # cocking handle
            along_z(M, 2.0, 4.0, 21.0, "steel"),                                   # cooling shroud
            along_z(M, 2.2, 20.4, 21.4, "rifmetal"),                               # shroud end ring
            along_z(M, 0.95, 21.0, 28.0, "steel"),                                 # barrel stub
            along_z(M, 1.25, 27.0, 28.0, "rifmetal"),                              # muzzle ring
            box(M @ tr(0, 2.6, -5.2), 0.5, 0.5, 1.1, "steel", r=0.2),              # rear sight
            box(M @ tr(0, 1.7, 26.5), 0.4, 0.9, 0.5, "steel")]                     # front sight


def lmg_overlay(M, kind):
    return _flash(M, 28.0, 1.15) if kind == "flash" else []


LMG = Weapon(lmg_prims, gripR=(0, -3.0, -4.3), gripL=(0, -2.4, 5.5), overlay=lmg_overlay,
             extras={"bipod_folded": lambda M: _bipod_folded(M, 22.5, -9.0, y=-2.6),
                     "bipod_open": lambda M: _bipod_open(M, 22.0),
                     "bipod_sandbag": lambda M: _bipod_open(M, 22.0) + _sandbag_wall(M, 22.0 + 3.4)})   # bipod open on a low sandbag parapet (kneeling gunner)

# ---------------------------------------------------------------- Chauchat (FM Mle 1915): half-moon magazine underneath, perforated jacket, pistol + fore grip, stubby wood butt
# same weapon frame / grips / bipod attach points as lmg_prims; prims [8] (jacket centre z 12.5) and [11] (muzzle ring z 27.5) stay on the bore axis (tools/mg_axis.py)
def chauchat_prims(M):
    import math
    p = [box(M @ tr(0, -0.9, -11.5), 1.3, 2.2, 5.4, "wood"),                        # stubby wooden butt
         box(M @ tr(0, -1.0, -17.0), 1.5, 3.0, 0.7, "steel"),                       # butt plate (BUTT_LOCAL -17)
         box(M @ tr(0, 0.1, -1.0), 1.8, 2.1, 5.2, "rifmetal"),                      # receiver
         box(M @ tr(0, -2.4, 1.6), 1.0, 0.9, 1.4, "steel"),                         # magazine housing
         box(M @ tr(0, -1.0, -6.2), 0.6, 0.6, 0.9, "steel", r=0.2),                 # rear of the receiver tube
         box(M @ tr(0, -3.0, -4.3), 0.7, 1.6, 0.8, "wood"),                         # pistol grip
         box(M @ tr(0, -2.3, -1.8), 0.35, 0.5, 1.5, "steel", r=0.15),               # trigger guard
         box(M @ tr(1.9, 0.4, -4.4), 0.6, 0.5, 0.5, "steel", r=0.2),                # cocking handle
         along_z(M, 2.0, 4.0, 21.0, "steel"),                                       # perforated jacket
         along_z(M, 2.2, 20.4, 21.4, "rifmetal"),                                   # jacket end ring
         along_z(M, 0.95, 21.0, 28.0, "steel"),                                     # barrel stub
         along_z(M, 1.25, 27.0, 28.0, "rifmetal"),                                  # muzzle ring
         box(M @ tr(0, 2.6, -5.2), 0.5, 0.5, 1.1, "steel", r=0.2),                  # rear sight
         box(M @ tr(0, 1.7, 26.5), 0.4, 0.9, 0.5, "steel")]                         # front sight
    # half-moon (D-shaped) box magazine: thick (x) half-disc under the receiver, curved rim to the front-bottom, flat chord side to the rear,
    # plain steel grey ("chau_mag"); a few solid fill blobs so it reads as a box, not a wire.
    cy, cz, R = -3.2, 1.2, 4.6
    arc = [(cy - R * math.sin(math.radians(a)), cz + R * math.cos(math.radians(a))) for a in range(-90, 91, 15)]
    for (y0, z0) in arc:
        p.append(ell(M @ tr(0, y0, z0), 1.35, 1.1, 1.1, "chau_mag"))                      # curved rim
    for (y0, z0), (y1, z1) in zip(arc, arc[1:]):
        p.append(seg(pt(M, (0, y0, z0)), pt(M, (0, y1, z1)), 1.3, "chau_mag"))
    p.append(seg(pt(M, (0, cy - R, cz)), pt(M, (0, cy + R * 0.0 + 0.2, cz)), 1.3, "chau_mag"))   # flat chord side (top arm tucks into receiver)
    p.append(ell(M @ tr(0, cy, cz + 1.6), 1.3, 3.6, 2.6, "chau_mag"))                           # solid body fill
    # perforations: dark holes along both sides of the jacket
    for z in (6.0, 8.0, 10.0, 12.0, 14.0, 16.0, 18.0):
        for s in (1, -1):
            p.append(ell(M @ tr(s * 1.95, 0.6, z), 0.18, 0.55, 0.55, "gripdark"))
    # fore grip under the jacket (left hand), + stronger bipod clamp
    p.append(box(M @ tr(0, -2.4, 5.5), 0.7, 1.0, 0.9, "wood"))
    return p


def chauchat_overlay(M, kind):
    return _flash(M, 28.0, 0.9) if kind == "flash" else []


CHAUCHAT = Weapon(chauchat_prims, gripR=(0, -3.0, -4.3), gripL=(0, -2.4, 5.5), overlay=chauchat_overlay,
                  extras={"bipod_folded": lambda M: _bipod_folded(M, 22.5, -9.0, y=-2.6),
                          "bipod_open": lambda M: _bipod_open(M, 22.0),
                          "bipod_sandbag": lambda M: _bipod_open(M, 22.0) + _sandbag_wall(M, 22.0 + 3.4)})


# ---------------------------------------------------------------- anti-tank rifle (Mauser T-Gewehr-like): long barrel (longest weapon of the set), single-shot bolt, no magazine
ATR_LEN = float(os.environ.get("ATR_LEN", 32.0))     # muzzle z (butt plate at -19.2); original design 38, 30 when prone/seated had to fit the frame
_X = ATR_LEN - 9.0                                    # barrel step (heavy -> long section)


def atr_prims(M):
    L = ATR_LEN
    return [box(M @ tr(0, -0.7, -13.5), 1.4, 2.4, 6.0, "wood"),                    # butt stock
            box(M @ tr(0, -1.0, -19.2), 1.5, 3.2, 0.7, "steel"),                   # butt plate
            box(M @ tr(0, -0.8, -7.3), 1.6, 2.0, 1.6, "wood"),                     # wrist
            box(M @ tr(0, 0.3, -1.6), 1.7, 1.9, 5.2, "rifmetal"),                  # long receiver
            box(M @ tr(-2.1, 0.7, -3.4), 1.1, 0.5, 0.5, "steel", r=0.2),           # bolt handle
            ell(M @ tr(-3.4, 0.7, -3.4), 0.9, 0.9, 0.9, "steel"),                  # bolt knob
            box(M @ tr(0, -2.2, -2.0), 0.4, 0.5, 1.6, "steel", r=0.15),            # trigger guard
            box(M @ tr(0, 2.5, -4.0), 0.5, 0.5, 1.2, "steel", r=0.2),              # rear sight
            box(M @ tr(0, -0.5, 9.5), 1.5, 1.5, 5.4, "wood"),                      # fore-stock
            along_z(M, 1.2, 3.0, _X, "steel"),                                     # heavy barrel
            along_z(M, 1.0, _X, L, "steel"),                                       # barrel, long
            along_z(M, 1.6, _X - 2.6, _X - 1.0, "rifmetal"),                       # bipod collar
            along_z(M, 1.5, L - 2.0, L, "rifmetal"),                               # muzzle brake
            box(M @ tr(0, 1.9, L - 3.0), 0.4, 0.9, 0.5, "steel")]                  # front sight


def atr_overlay(M, kind):
    if kind != "flash":
        return []
    L = ATR_LEN
    return _flash(M, L, 1.4, y=0.2) + [ell(M @ tr(0, 0.3, L + 0.8), 6.0, 1.4, 2.4, "flash_edge"),      # brake blast sideways
                                       ell(M @ tr(0, 0.3, L + 0.6), 5.2, 0.9, 1.9, "flash")]


ATR = Weapon(atr_prims, gripR=(0, -2.4, -7.3), gripL=(0, -1.9, 6.0), overlay=atr_overlay,
             extras={"bipod_folded": lambda M: _bipod_folded(M, _X - 2.0, 9.0, y=-1.7),
                     "bipod_open": lambda M: _bipod_open(M, _X - 2.0, spread=4.6, dz=3.6),
                     "bipod_sandbag": lambda M: _bipod_open(M, _X - 2.0, spread=4.6, dz=3.6) + _sandbag_wall(M, _X - 2.0 + 3.6)})

WEAPONS = {"lmg": LMG, "chauchat": CHAUCHAT, "at_rifle": ATR}
