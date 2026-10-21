"""Tiny builders that read a tune/parts.json element ({"pos": [x,y,z], "size": [..], "tilt": deg, "round": r}) into primitives."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import ell, rbox, cyl, tr, rx  # noqa: E402


def at(M, c):
    """M @ translate(pos) [@ rx(tilt)]"""
    m = M @ tr(*c["pos"])
    return m @ rx(c["tilt"]) if "tilt" in c else m


def ellp(M, c, mat, **kw):
    return ell(at(M, c), *c["size"], mat, **kw)


def boxp(M, c, mat, **kw):
    if "round" in c:
        kw["r"] = c["round"]
    if "frac" in c:
        kw["frac"] = c["frac"]
    return rbox(at(M, c), *c["size"], mat, **kw)


def cylp(M, c, mat, **kw):
    return cyl(at(M, c), c["r"], c["h"], mat, **kw)
