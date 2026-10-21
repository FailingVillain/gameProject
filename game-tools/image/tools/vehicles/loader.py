"""units/<name>.json with "kind": "vehicle" -> Vehicle.

json keys
  "kind": "vehicle"
  "model": "renault_ft" | "armored_car"     module tools/vehicles/<model>.py (hull(frame), turret(kind, frame), RING, TURRETS)
  "turret": "mg"                            turret kind of that module
  "anims": "vehicle" | {...}                anims/<name>.json: {"hull": {anim: {frames, ms}}, "turret": {...}}
  "dirs": "all" | ["s", "e"]                rows (9 directions, see vehcommon.DIRS9)
"""
import importlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from vehicles.vehcommon import DIRS9, YAW9, render_hull, render_turret  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(HERE))          # image/
UNITS_DIR = os.path.join(ROOT, "units")
ANIMS_DIR = os.path.join(ROOT, "anims")


def load_anims(spec):
    if isinstance(spec, str):
        with open(os.path.join(ANIMS_DIR, f"{spec}.json"), encoding="utf-8") as fh:
            spec = json.load(fh)
    out = {}
    for part in ("hull", "turret"):
        out[part] = {}
        for name, v in spec[part].items():
            if isinstance(v, list):
                v = {"frames": v}
            out[part][name] = {"frames": list(v["frames"]), "ms": v.get("ms", 150)}
    return out


def columns_of(anims):
    cols = []
    for a in anims.values():
        for f in a["frames"]:
            if f not in cols:
                cols.append(f)
    return cols


class Vehicle:
    def __init__(self, spec):
        assert spec.get("kind") == "vehicle", "not a vehicle unit"
        self.spec = spec
        self.name = spec["name"]
        self.model = importlib.import_module("vehicles." + spec["model"])
        self.turret_kind = spec.get("turret", self.model.TURRETS[0])
        assert self.turret_kind in self.model.TURRETS, f"unknown turret {self.turret_kind}"
        self.ring = tuple(self.model.RING)
        self.rings = [tuple(r) for r in getattr(self.model, "RINGS", [self.model.RING])]   # one entry per turret mount (shared turret sheet)
        self.anims = load_anims(spec.get("anims", "vehicle"))
        self.hull_columns = columns_of(self.anims["hull"])
        self.turret_columns = columns_of(self.anims["turret"])
        d = spec.get("dirs", "all")
        self.dirs = list(DIRS9) if d == "all" else list(d)

    def hull_frame(self, d, frame, res):
        prims, ov = self.model.hull(frame)
        return render_hull(prims, ov, YAW9[d], res)

    def turret_frame(self, d, frame, res):
        if getattr(self.model, "TURRET_YAW_AWARE", False):
            prims, ov = self.model.turret(self.turret_kind, frame, YAW9[d])
        else:
            prims, ov = self.model.turret(self.turret_kind, frame)
        return render_turret(prims, ov, YAW9[d], res, self.ring[1])

    def whole_prims(self, hull_frame="idle", turret_frame="idle"):
        """hull + turret as one model (for the composition check)."""
        from voxel import tr
        hp, hov = self.model.hull(hull_frame)
        tp, tov = self.model.turret(self.turret_kind, turret_frame)
        prims, ov = list(hp), list(hov)
        for ring in self.rings:
            T = tr(*ring)
            prims += [q.moved(T) for q in tp]; ov += [q.moved(T) for q in tov]
        return prims, ov


def load_vehicle(name):
    path = name if name.endswith(".json") else os.path.join(UNITS_DIR, f"{name}.json")
    with open(path, encoding="utf-8") as fh:
        spec = json.load(fh)
    spec.setdefault("name", os.path.splitext(os.path.basename(path))[0])
    return Vehicle(spec)
