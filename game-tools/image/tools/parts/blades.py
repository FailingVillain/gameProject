"""Blades: curved sabre (voxel.Weapon) + scabbard gear + small map case.
Sabre frame: origin at the guard, z = blade direction, y = edge-up side; blade curves toward +y."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, ell, seg, along_z, tr, rx, pt  # noqa: E402

import math

import numpy as np

import tune  # noqa: E402
from parts._t import boxp  # noqa: E402

# sheath_guard_up and the blade polyline (segment length, pitch deg) live in tune/parts.json -> weapons.sabre / gear.scabbard


def hilt_prims(M):
    """grip + gilt guard + pommel in the sabre frame (guard at the origin, grip toward -z). Shared by the drawn sabre and
    the sheathed hilt. Guard sizes are chosen so it stays outside the gripping hand (hand sphere r 2.9 around the grip
    centre z=-2.6): quillon tips at x +-3.6, knuckle bow arcs 3.4-4.5 units out in front of the fingers."""
    # grip: near-black core + leather wrap bands (the bands are wider than the core, so the rings read at 128px)
    p = [along_z(M, 0.7, -5.2, -0.2, "gripdark")]
    for zc in (-4.6, -3.8, -3.0, -2.2, -1.4, -0.7):
        p.append(along_z(M, 0.95, zc - 0.22, zc + 0.22, "leather"))
    p += [ell(M @ tr(0, 0, -5.4), 1.0, 1.0, 0.8, "brass"),                                  # pommel (gilt)
          box(M @ tr(0, 0.2, 0.0), tune.get("parts", "weapons", "sabre")["quillon_half_width"], 0.7, 0.8, "brass", r=0.3),                         # quillon: sticks out past the hand
          seg(pt(M, (3.4, 0.2, -0.2)), pt(M, (3.9, -1.9, -1.4)), 0.45, "brass"),           # knuckle bow: D-arc from the quillon
          seg(pt(M, (3.9, -1.9, -1.4)), pt(M, (3.0, -3.4, -3.0)), 0.45, "brass"),          # tip, round the front of the fingers
          seg(pt(M, (3.0, -3.4, -3.0)), pt(M, (1.0, -3.9, -4.2)), 0.45, "brass"),          # to the pommel
          seg(pt(M, (1.0, -3.9, -4.2)), pt(M, (0.0, -2.4, -5.0)), 0.45, "brass"),
          seg(pt(M, (0.0, -2.4, -5.0)), pt(M, (0.0, -0.4, -5.3)), 0.45, "brass")]
    for q in p[:7]:
        q.tag = "hilt_grip"
    p[7].tag = "hilt_pommel"
    for q in p[8:]:
        q.tag = "hilt_guard"
    return p


def short_blade(k):
    """the tuned sabre blade (tune/parts.json weapons.sabre.blade) with every segment length scaled by k (same curve, shorter sabre)."""
    return tuple((L * k, a) for L, a in tune.get("parts", "weapons", "sabre")["blade"])


def sabre_prims(M, blade=None):
    p = hilt_prims(M)
    W = tune.get("parts", "weapons", "sabre")
    blade = blade or W["blade"]                                                           # explicit blade = shorter variants (sabre_short)
    y, z = 0.0, W["start_z"]
    for i, (L, a) in enumerate(blade):
        r = math.radians(a)
        cy, cz = y + math.sin(r) * L / 2, z + math.cos(r) * L / 2
        hy = W["half_width"] if i < len(blade) - 1 else W["tip_half_width"]                # tip narrows
        p.append(box(M @ tr(0, cy, cz) @ rx(-a), W["thickness"], hy, L / 2 + 0.6, "bright", r=0.25))  # +0.6 overlap: no gaps at the joints
        y, z = y + math.sin(r) * L, z + math.cos(r) * L
    return p


SABRE = Weapon(sabre_prims, gripR=(0, 0, -2.6), gripL=(0, 0, -2.6))
SHORT_K = 0.8                                                                                # assault-trooper sabre: 80% blade
SABRE_SHORT = Weapon(lambda M: sabre_prims(M, short_blade(SHORT_K)), gripR=(0, 0, -2.6), gripL=(0, 0, -2.6))


def scabbard(T, k=1.0):
    """steel scabbard hanging from the left hip, tip back and down (torso frame). k < 1 = shorter scabbard (same throat)."""
    G = tune.get("parts", "gear", "scabbard")
    ga, gb = np.array(G["a"], float), np.array(G["b"], float)
    gb = ga + k * (gb - ga)                                                                  # k < 1: chape closer to the throat
    a, b = pt(T, ga), pt(T, gb)
    p = [seg(a, b, G["r"], "steel", flat=True),
         ell(T @ tr(*G["a"]), *G["throat_size"], "steel"),                                   # throat / frog
         ell(T @ tr(*gb), G["chape_r"], G["chape_r"], G["chape_r"], "steel")]            # chape
    p.append(seg(pt(T, G["strap_from"]), a, G["strap_r"], "leather"))                         # hanger strap
    # sheathed hilt: same grip + gilt guard as the drawn sabre, standing up out of the throat (blade axis = scabbard axis)
    d = (b - a) / np.linalg.norm(b - a)
    x = T[:3, 0] - np.dot(T[:3, 0], d) * d
    x = x / np.linalg.norm(x)
    Mg = np.eye(4)
    Mg[:3, 0], Mg[:3, 1], Mg[:3, 2] = x, np.cross(d, x), d
    Mg[:3, 3] = a - d * G["sheath_guard_up"]
    p += hilt_prims(Mg)
    return p


def map_case(T):
    G = tune.get("parts", "gear", "map_case")
    return [boxp(T, G["case"], "leather"),
            boxp(T, G["flap"], "leather")]


WEAPONS = {"sabre": SABRE, "sabre_short": SABRE_SHORT}
GEAR = {"scabbard": scabbard, "map_case": map_case, "scabbard_short": lambda T: scabbard(T, SHORT_K)}
