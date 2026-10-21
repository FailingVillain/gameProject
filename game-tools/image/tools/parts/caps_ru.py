"""Russian civil-war faction caps: furazhka (Whites, peaked field cap) and budenovka (Reds, pointed cloth helmet).
Head frame: origin at the neck, y up, z forward (64x80 design scale). Registered in unit_loader.py. No national symbols."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import ell, cyl, rbox, seg, tr, rx, tband  # noqa: E402
import tune  # noqa: E402
from parts._t import ellp  # noqa: E402


def furazhka(Hm, band=True):
    """WWI Russian peaked field cap: soft khaki-olive crown a bit wider than the band, narrow team-colour band,
    small black leather visor. No cockade/badge. Lower and softer than officer_cap (no saucer, no tall band)."""
    cloth = "ru_olive"
    return [cyl(Hm @ tr(0, 5.4, 0.3), 5.85, 0.8, tband(cloth) if band else cloth),                     # narrow band
            ell(Hm @ tr(0, 6.2, 0.3) @ rx(-4), 6.3, 2.3, 6.5, cloth, clipy=-0.3),                    # soft crown, wider than band, rear sags
            rbox(Hm @ tr(0, 4.7, 6.9) @ rx(16), 3.6, 0.3, 1.5, "visor", r=0.25)]                    # small black visor


def budenovka(Hm, band=True):
    """Red Army cloth helmet: dome + pointed spire, long ear/neck flaps buttoned up at the sides, short visor, grey-brown cloth.
    Front patch / band in the team colour instead of a star."""
    cloth = "ru_greybrown"
    out = [ell(Hm @ tr(0, 6.4, 0.3), 6.1, 4.4, 6.3, cloth, clipy=-1.0)]                              # dome
    T = tune.get("parts", "headgear", "budenovka")
    out.append(seg((Hm @ [0, 9.6, 0.3, 1])[:3], (Hm @ [0, T["spire_top"], 0.3, 1])[:3], T["spire_r"], cloth, r1=0.35))   # spire cone (tune: spire_top = tip y, spire_r = base radius)
    out.append(cyl(Hm @ tr(0, 4.7, 0.3), 6.0, 0.7, tband(cloth) if band else cloth))                       # thin front-lower band all round
    for s in (1, -1):                                                                                 # ear flaps folded up at the sides
        out.append(rbox(Hm @ tr(s * 6.1, 4.6, -0.2), 0.9, 2.9, 3.9, cloth, r=0.4))
        out.append(ell(Hm @ tr(s * 7.0, 5.6, 0.0), 0.35, 0.5, 0.5, "steel"))                         # button
    out.append(rbox(Hm @ tr(0, 3.6, -5.9) @ rx(-8), 4.6, 2.6, 0.9, cloth, r=0.4))                     # neck flap
    out.append(rbox(Hm @ tr(0, 5.2, 6.9) @ rx(12), 3.4, 0.35, 1.3, cloth, r=0.3))                     # short visor
    return out


def _officer_cap(cap):
    def fn(Hm, band=True):
        """officer_cap geometry (tune headgear.officer_cap) in a faction cloth colour, no badge, black leather visor."""
        T = tune.get("parts", "headgear", "officer_cap")
        return [ellp(Hm, T["band"], tband(cap) if band else cap), ellp(Hm, T["crown"], cap), ellp(Hm, T["saucer"], cap), ellp(Hm, T["visor"], "visor")]
    return fn


_BASE = dict(legging="puttee", boot="leather", belt="leather", hand="skin", collar=True, puttees=True)
UNIFORMS = {
    "ru_white": dict(_BASE, jacket="ru_olive", trousers="ru_olive"),
    "ru_red": dict(_BASE, jacket="ru_greybrown", trousers="ru_greybrown"),
}
HEADGEAR = {
    "officer_cap_ru_olive": dict(fn=_officer_cap("ru_olive"), net=False),
    "officer_cap_ru_greybrown": dict(fn=_officer_cap("ru_greybrown"), net=False),
    "furazhka": dict(fn=furazhka, net=False),
    "budenovka": dict(fn=budenovka, net=False),
}
