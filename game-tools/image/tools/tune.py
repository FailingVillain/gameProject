"""Tuning values (colours, part sizes, pose numbers) live in image/tune/*.json -- edit numbers there, no code needed.

  tune/materials.json  "ramps": {material: 4 anchor colours dark->light}      (read once by voxel.py)
  tune/parts.json      headgear / rifles / blades / packs dimensions ...        (read when the part is built)
  tune/poses.json      render constants + pose parameters (e_pull, wave arm...) (read when the pose is built)
  units/<name>.json    optional  "tune": {"parts": {...}, "poses": {...}, "materials": {"ramps": {...}}}
                       deep-merged over the global files for that unit only (only the keys you list change).
Keys starting with "_" are comments.  BASE_ZOOM / RBOX_ROUND / STRIPE_K ("render" in poses.json) are global only.
"""
import copy
import json
import os
from contextlib import contextmanager

TUNE_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "tune")
_base = {}
_active = {}


def _load(kind):
    if kind not in _base:
        with open(os.path.join(TUNE_DIR, f"{kind}.json"), encoding="utf-8") as fh:
            _base[kind] = json.load(fh)
    return _base[kind]


def _merge(a, b):
    out = dict(a)
    for k, v in b.items():
        out[k] = _merge(out[k], v) if isinstance(v, dict) and isinstance(out.get(k), dict) else v
    return out


def _tup(v):
    """JSON lists -> tuples (recursively), so values behave like the old python literals."""
    if isinstance(v, list):
        return tuple(_tup(x) for x in v)
    if isinstance(v, dict):
        return {k: _tup(x) for k, x in v.items() if not k.startswith("_")}
    return v


def get(kind, *path):
    """tune.get("parts", "headgear", "feldkappe") -> dict (global json + active unit override, lists as tuples)."""
    node = _load(kind)
    ov = _active.get(kind)
    for k in path:
        node = node[k]
        ov = ov.get(k) if isinstance(ov, dict) else None
    if ov is not None:
        node = _merge(node, ov) if isinstance(node, dict) else ov
    return _tup(node)


def render(name):
    """global render constants (never per-unit): tune.render("BASE_ZOOM")"""
    return _load("poses")["render"][name]


@contextmanager
def active(unit_tune):
    """apply a unit's "tune" overrides while it renders (parts / poses via get(); material ramps swapped in place)."""
    global _active
    prev = _active
    _active = unit_tune or {}
    saved = {}
    ramps = ((_active.get("materials") or {}).get("ramps")) or {}
    if ramps:
        import voxel as V
        for k, v in ramps.items():
            saved[k] = V.MATERIALS[k]
            V.MATERIALS[k] = V._ramp8([tuple(c[:3]) + (255,) for c in v])
    try:
        yield
    finally:
        _active = prev
        if saved:
            import voxel as V
            V.MATERIALS.update(saved)
