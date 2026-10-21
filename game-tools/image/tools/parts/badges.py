"""Cap badges (front of the cap, mirror-symmetric about x=0, stylised - no real national symbols).
Registry BADGES[faction] = fn(Hm, y, z, k, pitch) -> prims.
Hm = head frame (origin at the neck, y up, z forward). (y, z) = badge centre on the cap front, k = scale, pitch = tilt (deg, + = face up).
Attached by unit_loader: "badge": "<faction>" in units/<name>.json, else the HEAD_BADGE default of the headgear
(japanese_cap -> yellow star, fengtian_cap -> five-colour roundel, furazhka -> white oval cockade, budenovka -> red star ...)."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import ell, cyl, seg, tr, rx, pt  # noqa: E402

DEFAULT_POS = (7.3, 6.1)            # feldkappe crown front (y, z)


def _rz(a_deg):
    a = np.radians(a_deg); c, s = np.cos(a), np.sin(a)
    return np.array([[c, -s, 0, 0], [s, c, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1.0]])


ANGLES = [0, 72, -72, 144, -144]     # 5 petals / arms, one pointing up, symmetric about x=0


def _frame(Hm, y, z, pitch):
    return Hm @ tr(0, y, z) @ rx(-pitch)


def plum_blossom(Hm, y=DEFAULT_POS[0], z=DEFAULT_POS[1], k=1.0, pitch=0.0):
    """korean: 5 gold petals round a gold centre, thin light rim behind the petals."""
    B = _frame(Hm, y, z, pitch)
    p = [cyl(B @ tr(0, 0, -0.08 * k) @ rx(90), 1.4 * k, 0.06 * k, "badge_gold_rim")]
    for a in ANGLES:
        s, c = np.sin(np.radians(a)), np.cos(np.radians(a))
        p.append(ell(B @ tr(0.72 * k * s, 0.72 * k * c, 0) @ _rz(-a), 0.38 * k, 0.55 * k, 0.25 * k, "badge_gold"))
    p.append(ell(B @ tr(0, 0, 0.05 * k), 0.42 * k, 0.42 * k, 0.3 * k, "badge_gold"))
    return p


def _star(Hm, mat, y, z, k, pitch):
    B = _frame(Hm, y, z, pitch)
    p = [ell(B, 0.6 * k, 0.6 * k, 0.25 * k, mat)]
    for a in ANGLES:
        s, c = np.sin(np.radians(a)), np.cos(np.radians(a))
        p0 = pt(B, (0.25 * k * s, 0.25 * k * c, 0))
        p1 = pt(B, (1.45 * k * s, 1.45 * k * c, 0))
        p.append(seg(p0, p1, 0.36 * k, mat, r1=0.1 * k))                                # tapered arm -> point
    return p


def star_yellow(Hm, y=DEFAULT_POS[0], z=DEFAULT_POS[1], k=1.0, pitch=0.0):
    """japanese: yellow 5-point star."""
    B = _frame(Hm, y, z, pitch)
    return _star(Hm, "badge_yellow", y, z, k * 0.9, pitch)   # N56: red backing disc removed (matches the red-less Japanese faction icon)


def star_red(Hm, y=DEFAULT_POS[0], z=DEFAULT_POS[1], k=1.0, pitch=0.0):
    """russian-red: red 5-point star."""
    return _star(Hm, "badge_red", y, z, k, pitch)


def cockade_fengtian(Hm, y=DEFAULT_POS[0], z=DEFAULT_POS[1], k=1.0, pitch=0.0):
    """fengtian: round five-colour roundel, concentric red / yellow / blue / white / black (outer -> inner)."""
    B = _frame(Hm, y, z, pitch)
    layers = [(1.5, "badge_red"), (1.2, "badge_yellow"), (0.9, "badge_blue"), (0.6, "badge_white"), (0.3, "badge_black")]
    return [cyl(B @ tr(0, 0, 0.06 * k * i) @ rx(90), r * k, 0.07 * k, m) for i, (r, m) in enumerate(layers)]


def cockade_russian_white(Hm, y=DEFAULT_POS[0], z=DEFAULT_POS[1], k=1.0, pitch=0.0):
    """russian-white: oval cockade, concentric white / blue / red (outer -> inner), elliptic x 1.4 : y 1.2."""
    B = _frame(Hm, y, z, pitch)
    return [ell(B @ tr(0, 0, 0.1 * k * i), rx_ * k, ry_ * k, 0.12 * k, m)
            for i, (rx_, ry_, m) in enumerate([(1.4, 1.2, "badge_white"), (0.95, 0.8, "badge_blue"), (0.5, 0.4, "badge_red")])]


BADGES = {
    "korean": plum_blossom,
    "japanese": star_yellow,
    "fengtian": cockade_fengtian,
    "russian-white": cockade_russian_white,
    "russian-red": star_red,
}

# default badge per headgear: (faction, y, z, k, pitch)  -- front surface positions measured on each cap's geometry
HEAD_BADGE = {
    "japanese_cap": ("japanese", 9.2, 4.2, 1.7, 55.0),
    "japanese_officer_cap": ("japanese", 7.7, 7.15, 1.0, 0.0),
    "fengtian_cap": ("fengtian", 6.3, 6.85, 1.0, 0.0),
    "fengtian_officer_cap": ("fengtian", 7.7, 7.15, 1.0, 0.0),
    "furazhka": ("russian-white", 6.0, 6.85, 1.1, 0.0),
    "officer_cap_ru_olive": ("russian-white", 7.7, 7.15, 1.0, 0.0),
    "budenovka": ("russian-red", 6.6, 6.62, 1.2, 8.0),
    "officer_cap_ru_greybrown": ("russian-red", 7.7, 7.15, 1.0, 0.0),
    "officer_cap": ("korean", 7.7, 7.15, 0.95, 0.0),
}


def with_badge(headgear_fn, faction, headgear=None):
    """wrap a headgear fn(Hm, band) so a cap badge is appended. faction = explicit unit "badge" key (feldkappe position) or
    '' -> HEAD_BADGE default of `headgear` (no-op when the headgear has none)."""
    if faction:
        badge = BADGES[faction]
        return lambda Hm, band: headgear_fn(Hm, band) + badge(Hm)
    if headgear in HEAD_BADGE:
        f, y, z, k, pitch = HEAD_BADGE[headgear]
        badge = BADGES[f]
        return lambda Hm, band: headgear_fn(Hm, band) + badge(Hm, y, z, k, pitch)
    return headgear_fn
