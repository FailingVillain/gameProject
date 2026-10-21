"""Shoulder weapons. Each is a voxel.Weapon (prims(M), grips in weapon-local coords, muzzle-flash overlay).
Weapon frame: origin at the receiver, z = muzzle direction, y up."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, along_z, tr  # noqa: E402
import tune  # noqa: E402
from parts._t import boxp  # noqa: E402


def _flash(M, z, y=0.6):
    return [ell(M @ tr(0, y, z + 2.4), 3.8, 3.8, 5.0, "flash_edge"),
            ell(M @ tr(0, y, z + 2.2), 2.7, 2.7, 4.0, "flash"),
            ell(M @ tr(0, y, z + 1.8), 1.6, 1.6, 2.4, "flash_core")]


# ---- M1 Garand
def garand_prims(M):
    W = tune.get("parts", "weapons", "garand")
    b = W["barrel"]
    return [boxp(M, W["stock"], "wood"),                                           # stock
            boxp(M, W["butt"], "wood"),                                            # butt
            boxp(M, W["wrist"], "wood"),                                           # wrist
            boxp(M, W["receiver"], "wood"),                                        # receiver
            box(M @ tr(0, 2.6, -3.0), 0.5, 0.6, 1.8, "wood"),                      # rear sight
            boxp(M, W["handguard"], "wood"),                                       # handguard
            along_z(M, b["r"], b["z0"], b["z1"], "steel"),                         # barrel
            box(M @ tr(0, 1.8, W["front_sight_z"]), 0.4, 0.8, 0.5, "steel"),       # front sight
            box(M @ tr(0, -2.7, -3.4), 0.5, 1.0, 2.0, "wood")]                     # trigger guard


def garand_overlay(M, kind):
    return _flash(M, tune.get("parts", "weapons", "garand")["flash_z"]) if kind == "flash" else []


def bayonet(M, muzzle=None):
    """knife bayonet fixed under the muzzle (weapon local z muzzle .. muzzle+9). Only drawn when a pose asks attach=["bayonet"]."""
    B = tune.get("parts", "weapons", "bayonet")
    if muzzle is None:
        muzzle = tune.get("parts", "weapons", "garand")["bayonet_muzzle"]
    return [box(M @ tr(0, -0.7, muzzle + B["handle_dz"]), *B["handle_size"], "steel", r=0.3),      # ring / handle
            box(M @ tr(0, -0.6, muzzle + B["blade_dz"]), *B["blade_size"], "bright", r=0.3)]       # blade


_G = tune.get("parts", "weapons", "garand")      # grips are read once (global tune only)
GARAND = Weapon(garand_prims, gripR=_G["grip_right"], gripL=_G["grip_left"], overlay=garand_overlay, extras={"bayonet": bayonet})


# ---- generic long bolt-action rifle (WWI: Arisaka / Lee-Enfield / Mauser class), full wooden stock, bolt handle, long barrel
def bolt_rifle_prims(M):
    W = tune.get("parts", "weapons", "bolt_rifle")
    b = W["barrel"]
    return [boxp(M, W["butt_stock"], "wood"),                                      # butt stock
            boxp(M, W["butt_plate"], "wood"),                                      # butt plate
            boxp(M, W["wrist"], "wood"),                                           # wrist
            boxp(M, W["receiver"], "wood"),                                        # receiver / stock
            box(M @ tr(-1.9, 0.6, -2.2), 0.9, 0.45, 0.45, "steel", r=0.2),         # bolt handle (right side)
            ell(M @ tr(-2.9, 0.5, -2.2), 0.65, 0.65, 0.65, "steel"),               # bolt knob
            box(M @ tr(0, 2.3, -3.2), 0.5, 0.5, 1.6, "steel", r=0.2),              # rear sight
            boxp(M, W["fore"], "wood"),                                            # long fore-stock / handguard
            boxp(M, dict(W["magazine"], round=0.3), "steel"),                      # magazine
            along_z(M, b["r"], b["z0"], b["z1"], "steel"),                         # barrel
            box(M @ tr(0, 1.6, W["front_sight_z"]), 0.4, 0.8, 0.5, "steel"),       # front sight
            box(M @ tr(0, -2.6, -3.6), 0.5, 1.0, 2.0, "wood")]                     # trigger guard


def bolt_rifle_overlay(M, kind):
    return _flash(M, tune.get("parts", "weapons", "bolt_rifle")["flash_z"], y=0.4) if kind == "flash" else []


_B = tune.get("parts", "weapons", "bolt_rifle")
BOLT_RIFLE = Weapon(bolt_rifle_prims, gripR=_B["grip_right"], gripL=_B["grip_left"], overlay=bolt_rifle_overlay,
                    extras={"bayonet": lambda M: bayonet(M, tune.get("parts", "weapons", "bolt_rifle")["bayonet_muzzle"])})


# ---- Japanese: bolt rifle with a long sword bayonet permanently fixed (drawn in every pose, extras left empty to avoid doubling)
BAYO_CUT = 9.0           # barrel / fore-stock shortened by this much so rifle + long bayonet keep the standard overall length (E view fits the cell)


def bolt_rifle_bayo_prims(M):
    W = tune.get("parts", "weapons", "bolt_rifle")
    m = W["barrel"]["z1"] - BAYO_CUT
    base = bolt_rifle_prims(M)
    keep = [q for i, q in enumerate(base) if i not in (7, 9, 10)]               # drop long fore-stock, barrel, front sight
    return keep + [box(M @ tr(0, -0.2, 6.5), 1.6, 1.6, 4.5, "wood"),             # short fore-stock
                   along_z(M, W["barrel"]["r"], W["barrel"]["z0"], m, "steel"),
                   box(M @ tr(0, 1.6, m - 1.0), 0.4, 0.8, 0.5, "steel"),
                   box(M @ tr(0, -0.7, m + 0.6), 0.7, 0.8, 1.0, "steel", r=0.3),   # bayonet ring / handle
                   box(M @ tr(0, -0.6, m + 5.8), 0.35, 0.9, 4.4, "bright", r=0.3)]  # long blade


BOLT_RIFLE_BAYO = Weapon(bolt_rifle_bayo_prims, gripR=_B["grip_right"], gripL=_B["grip_left"],
                         overlay=lambda M, kind: _flash(M, _B["flash_z"] - BAYO_CUT, y=0.4) if kind == "flash" else [], extras={})


# ---- longer bolt rifle (infantry-guard): +~12% overall length, extra length added forward (butt / grips at the shoulder unchanged)
LONG_EXTRA = 5.0                                                                   # model units added to the muzzle end


def bolt_rifle_long_prims(M):
    e = LONG_EXTRA
    return [p for p in bolt_rifle_prims(M)][:7] + [
            box(M @ tr(0, -0.2, 9.0 + e / 2), 1.6, 1.6, 8.0 + e / 2, "wood"),       # longer fore-stock
            box(M @ tr(0, -2.4, -0.4), 0.7, 1.2, 1.6, "steel", r=0.3),
            along_z(M, 0.95, 3.0, 23.0 + e, "steel"),                               # longer barrel
            box(M @ tr(0, 1.6, 22.0 + e), 0.4, 0.8, 0.5, "steel"),
            box(M @ tr(0, -2.6, -3.6), 0.5, 1.0, 2.0, "wood")]


BOLT_RIFLE_LONG = Weapon(bolt_rifle_long_prims, gripR=(0, -2.4, -6.4), gripL=(0, -1.9, 9.5 + LONG_EXTRA / 2),
                         overlay=lambda M, kind: _flash(M, 24.0 + LONG_EXTRA, y=0.4) if kind == "flash" else [],
                         extras={"bayonet": lambda M: bayonet(M, 23.0 + LONG_EXTRA)})


# ---- M1 carbine (shorter, lighter)
def carbine_prims(M):
    return [box(M @ tr(0, -0.5, -10.5), 1.5, 2.3, 6.5, "wood"),
            box(M @ tr(0, -1.2, -17.2), 1.4, 2.8, 0.9, "wood"),
            box(M @ tr(0, -0.4, -6.4), 1.6, 2.0, 2.0, "wood"),
            box(M @ tr(0, 0.5, -2.0), 1.6, 1.7, 3.4, "wood"),
            box(M @ tr(0, -0.3, 6.2), 1.6, 1.6, 4.6, "wood"),
            box(M @ tr(0, -3.2, -0.4), 0.8, 1.8, 0.9, "wood"),                     # magazine
            along_z(M, 0.95, 2.0, 17.0, "steel"),
            box(M @ tr(0, 1.6, 15.5), 0.4, 0.7, 0.5, "steel")]


def carbine_overlay(M, kind):
    return _flash(M, 18.0, y=0.4) if kind == "flash" else []


CARBINE = Weapon(carbine_prims, gripR=(0, -2.2, -5.0), gripL=(0, -1.8, 6.0), overlay=carbine_overlay)

WEAPONS = {"garand": GARAND, "carbine": CARBINE, "bolt_rifle": BOLT_RIFLE, "bolt_rifle_long": BOLT_RIFLE_LONG, "bolt_rifle_bayo": BOLT_RIFLE_BAYO}
