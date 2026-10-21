"""units/<name>.json with "kind": "artillery" -> ArtilleryUnit (same interface as unit_loader.Unit, so make_unit.py renders it).

json keys
  "kind": "artillery"
  "gun": "howitzer" | "infantry_gun" | "field_gun"       parts/guns.py + tune/parts.json "guns"
  "frame": [W, H]          main frame size in px (LODs are 1/2 and 1/4); "ay_frac": ground-anchor height fraction;
  "zoom_mul": 1.0          model zoom relative to the infantry zoom (1.0 = crew drawn exactly like the infantry sprites)
  "center": [x, z]         gun-frame point put at the anchor (footprint centre)
  "headgear" / "uniform" / "gear" / "team" / "belt"  crew kit (same keys as infantry units)
  "anims", "dirs", "frames"
Anchor = ground point under `center`, identical in every cell (frames are sized so no per-frame shift is needed).
"""
import copy
import importlib
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import DIRS  # noqa: E402
import voxel as V  # noqa: E402
from voxel import Kit, make_frame  # noqa: E402
from vehicles.vehcommon import VehRes  # noqa: E402
import unit_loader as UL  # noqa: E402
import tune  # noqa: E402
from parts import guns  # noqa: E402

_K = 1.5 if __import__("os").environ.get("KD_RES96") == "1" else 1.0   # res96: LOD x1.5
LOD_PXS = [2.0 * _K, 1.0 * _K, 0.5 * _K]


class ArtRes(VehRes):
    """VehRes (frame decoupled from the pixel scale) with the zoom relative to the infantry zoom instead of x1.8."""

    def __init__(self, pxs, frame_main, ay_frac, zoom_mul=1.0):
        super().__init__(pxs, frame_main, ay_frac)
        self.zoom = V.BASE_ZOOM * pxs * zoom_mul

    def tall(self, k=3):
        """same scale, k x taller canvas, anchor moved down by the added height / 2 (for union-bbox measuring)."""
        c = copy.copy(self)
        c.fh = self.fh * k
        c.ay = self.ay + self.fh * (k - 1) // 2
        return c


class ArtilleryUnit:
    def __init__(self, spec):
        self.spec = spec
        self.tune = spec.get("tune") or {}
        self.name = spec["name"]
        self.kind = spec["gun"]
        assert self.kind in guns.KINDS, f"unknown gun {self.kind}"
        self.base = importlib.import_module("bases.artillery")
        self.anims = UL.load_anims(spec.get("anims", "artillery"))
        self.columns = UL.columns_of(self.anims)
        hg = UL.HEADGEAR[spec.get("headgear", "feldkappe")]
        uni = spec.get("uniform", "ww1_khaki")
        mats = dict(UL.UNIFORMS[uni]) if isinstance(uni, str) else dict(UL.UNIFORMS["ww1_khaki"], **uni)
        team = set(spec.get("team", ["helmet_band", "arm_patch"]))
        gear = [UL.GEAR[g] for g in spec.get("gear", [])]
        self.kit = Kit(**mats, headgear=UL.badges.with_badge(hg["fn"], spec.get("badge"), spec.get("headgear", "feldkappe")), belt_on=spec.get("belt", True), belt_pouches=spec.get("belt_pouches", False),
                       team_band="helmet_band" in team, team_patch="arm_patch" in team,
                       back=(lambda T: [p for g in gear for p in g(T)]) if gear else None)
        self.net = bool(hg["net"])
        self.opts = {"center": tuple(spec.get("center", (0.0, 0.0))), "tow": bool(spec.get("tow")), "tow_center": float(spec.get("tow_center", 0.0))}
        d = spec.get("dirs", "all"); f = spec.get("frames", "all")
        self.dirs = list(DIRS) if d == "all" else list(d)
        self.frames = list(self.columns) if f == "all" else list(f)
        fr = spec.get("frame", [288, 288])
        ayf = spec.get("ay_frac", 0.66)
        zm = spec.get("zoom_mul", 1.0)
        self.lods = [ArtRes(p, fr, ayf, zm) for p in LOD_PXS]
        self.collide = True                           # multi-figure check (collide.contacts_multi)
        self.collide_dirs = ["s"]                     # scene is direction independent
        self.lod_cells = [("s", "idle"), ("e", "fire_1")]
        self.preview_frames = ["idle", "fire_1", "move_1", "unhitch_1"] if self.opts["tow"] else ["idle", "fire_1", "move_1"]
        self._hitch = {}

    def scene(self, name):
        with tune.active(self.tune):
            return self.base.scene(self.kind, name, self.kit, self.opts)

    def frame(self, d, name, res=None, fit=True, shift=None, **kw):
        """fit is ignored: the frames are sized so nothing needs shifting (shared_fit shifts, if ever needed, arrive via shift)."""
        res = res or self.lods[0]
        prims, ov, info = self.scene(name)
        kw["res"] = res
        kw["net"] = self.net
        kw["lift"] = 0.0
        if shift is not None:
            kw["lift"] = shift[1]
            kw["dx_px"] = shift[0]
        f = make_frame(prims, ov, d, **kw)
        if "hitch" in info:
            self._hitch[(name, d, res.fw)] = self.project(info["hitch"], d, res, shift)
        return f

    @staticmethod
    def project(p, d, res, shift=None):
        yaw = V.YAWS[d]
        w = (V.ry(yaw) @ np.array([p[0], p[1], p[2], 1.0]))[:3]
        sx, sy = (shift or (0.0, 0.0))
        return [round(float(res.ax + w[0] * res.zoom + sx), 1), round(float(res.ay - (w @ V.UP) * res.zoom - sy), 1)]

    def layout_extra(self, res):
        """layout json additions: horse-team hitch point (trail eye) per move frame / direction in px at this LOD."""
        hitch = {}
        for (name, d, fw), v in self._hitch.items():
            if fw == res.fw and name.startswith("move"):
                hitch.setdefault(name, {})[d] = v
        return {"kind": "artillery", "gun": self.kind, "anchor_meaning": "ground point under the footprint centre; same in every cell",
                "hitch_px": hitch}

    def collide_prims(self, name, d):
        return self.scene(name)[0]

    def active(self):
        return tune.active(self.tune)
