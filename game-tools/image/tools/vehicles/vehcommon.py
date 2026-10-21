"""Shared vehicle helpers: resolution profiles, materials, mount/pivot maths, compositing, sheets, checks.

Conventions (same engine as the infantry: voxel.py, model units = 64x80 design scale, x = left, y up, z forward)
  hull model origin   = ground point under the centre of the track / wheel footprint -> hull frame anchor
  turret model origin = turret ring centre (RING in hull coords)                     -> turret frame pivot
  The game draws turret cell (turret facing t) with its pivot at  hull_anchor + mount[hull facing]  (mount in px,
  recorded per direction and per LOD in <name>_hull_layout.json). Pivot is the same point in every turret cell.
  Directions: 9 rendered rows at 22.5 deg steps (s .. n, clockwise seen from above = the model turning toward the
  camera's right); ssw/sw/wsw/w/wnw/nw/nnw are horizontal mirrors of sse/se/ese/e/ene/ne/nne (MIRROR), so all
  models are left/right symmetric.
"""
from __future__ import annotations

import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
import voxel as V  # noqa: E402
from voxel import Res, make_frame, tr, ry, ell, cyl, along_z  # noqa: E402
from common import BASE_FW, BASE_FH, Frame, composite  # noqa: E402

DIRS9 = ["s", "sse", "se", "ese", "e", "ene", "ne", "nne", "n"]
YAW9 = {d: i * 22.5 for i, d in enumerate(DIRS9)}
MIRROR = {"ssw": "sse", "sw": "se", "wsw": "ese", "w": "e", "wnw": "ene", "nw": "ne", "nnw": "nne"}
DIRS16 = ["s", "sse", "se", "ese", "e", "ene", "ne", "nne", "n", "nnw", "nw", "wnw", "w", "wsw", "sw", "ssw"]

# frame sizes at the main scale (pixel scale 2 = same model zoom as the 128x160 infantry); LODs are 1/2 and 1/4
HULL_FRAME = (288, 288)        # was 192 (x1.5): ground anchor (144,192)
TURRET_FRAME = (240, 240)      # was 160 (x1.5): pivot (120,132)
HULL_AY_FRAC = 2 / 3           # ground anchor: vehicles extend farther up (height + depth) than down
TURRET_AY_FRAC = 0.55          # pivot: the turret sits on top of the ring, the cannon flash + smoke reach forward/down in S view
LOD_PXS = [2.0, 1.0, 0.5]
VEH_ZOOM = 1.8                 # was 1.2 (x1.5): vehicles are drawn 1.8x the infantry model zoom (FT ~108 px tall vs 108 px rifleman);
                               # true scale would make a 2.1 m tank 130 px tall - RTS compression, like the legacy vox_tank (1.1 vs 1.06)


class VehRes(Res):
    """Res with the frame size decoupled from the pixel scale: pxs drives outline / AO / line widths like the infantry,
    zoom = infantry zoom * VEH_ZOOM, the frame is `frame_main * pxs / 2` (288 -> 144 -> 72)."""

    def __init__(self, pxs, frame_main, ay_frac):
        super().__init__(int(BASE_FW * pxs), int(BASE_FH * pxs))
        self.fw = int(round(frame_main[0] * pxs / 2))
        self.fh = int(round(frame_main[1] * pxs / 2))
        self.ax = self.fw // 2
        self.ay = int(round(self.fh * ay_frac))
        self.zoom = self.zoom * VEH_ZOOM


HULL_LODS = [VehRes(p, HULL_FRAME, HULL_AY_FRAC) for p in LOD_PXS]        # 288, 144, 72
TURRET_LODS = [VehRes(p, TURRET_FRAME, TURRET_AY_FRAC) for p in LOD_PXS]  # 240, 120, 60


# ---------------------------------------------------------------- materials (8-band ramps, registered into voxel)
def register_material(name, anchors):
    """anchors: 4 RGB tuples dark -> light (same scheme as voxel.MATERIALS). Appends to the engine tables."""
    if name in V.MATERIALS:
        return
    V.MATERIALS[name] = V._ramp8([tuple(a[:3]) + (255,) for a in anchors])
    V.MAT_NAMES.append(name)
    V.MAT_IDX[name] = len(V.MAT_NAMES) - 1


VEHICLE_MATERIALS = {
    "veh_od":     [(50, 54, 34), (72, 76, 48), (96, 100, 64), (124, 128, 86)],       # tank body paint (olive-khaki)
    "veh_od2":    [(40, 44, 30), (58, 62, 42), (78, 82, 54), (102, 106, 72)],        # darker fittings / deck
    "veh_khaki":  [(68, 60, 42), (94, 84, 58), (122, 110, 78), (150, 138, 102)],     # armored car body (khaki-brown)
    "veh_khaki2": [(52, 46, 32), (72, 64, 44), (94, 84, 58), (118, 106, 76)],
    "veh_track":  [(38, 36, 32), (54, 50, 44), (72, 66, 58), (94, 88, 78)],          # track links
    "veh_track2": [(60, 56, 48), (82, 76, 66), (106, 98, 86), (132, 124, 110)],      # link highlights / teeth
    "veh_steel":  [(34, 36, 40), (54, 56, 62), (78, 82, 90), (110, 116, 126)],
    "veh_rubber": [(26, 26, 26), (38, 38, 38), (52, 52, 52), (70, 70, 70)],
    "veh_burn":   [(30, 28, 26), (44, 42, 40), (60, 58, 54), (78, 76, 70)],          # burnt paint
    "veh_burn2":  [(22, 20, 20), (34, 32, 30), (48, 46, 42), (64, 60, 54)],
    "veh_rust":   [(54, 34, 24), (76, 46, 30), (98, 62, 40), (122, 84, 54)],
}
for _n, _a in VEHICLE_MATERIALS.items():
    register_material(_n, _a)

WRECK_MAP = {"veh_od": "veh_burn", "veh_od2": "veh_burn2", "veh_khaki": "veh_burn", "veh_khaki2": "veh_burn2",
             "veh_track": "veh_burn2", "veh_rust": "veh_burn", "veh_track2": "veh_burn", "veh_steel": "veh_burn2", "veh_rubber": "veh_burn2",
             "team": "veh_burn", "bright": "veh_burn2", "void": "void"}


def mat_fn(wreck):
    """-> m(name): material name mapper (burnt variants on the wreck frames)."""
    def m(name):
        return WRECK_MAP.get(name, name) if wreck else name
    return m


def rad(q):
    return np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2)


def ang(q):
    return np.arctan2(q[:, 2], q[:, 0])


# ---------------------------------------------------------------- shared parts
def mg_prims(G, m, z0=0.0, length=9.5):
    """Hotchkiss-like machine gun: ball mount, thin barrel, short cooling jacket. G = mount frame (muzzle along +z)."""
    return [ell(G @ tr(0, 0, z0 + 0.4), 2.0, 2.0, 1.8, m("veh_steel")),
            along_z(G, 0.5, z0 + 0.5, z0 + length, m("veh_steel")),
            along_z(G, 0.85, z0 + 0.5, z0 + 4.5, m("veh_steel")),
            along_z(G @ tr(0, 1.1, 0), 0.35, z0 + 0.8, z0 + 2.8, m("veh_steel"))]          # sight / feed


def flash_mg(G, zm, strong=True):
    """muzzle flash overlay prims for a MG; zm = muzzle z in the G frame."""
    k = 1.0 if strong else 0.65
    out = [ell(G @ tr(0, 0, zm + 1.6 * k), 2.6 * k, 2.6 * k, 3.4 * k, "flash_edge"),
           ell(G @ tr(0, 0, zm + 1.3 * k), 1.9 * k, 1.9 * k, 2.5 * k, "flash"),
           ell(G @ tr(0, 0, zm + 0.9 * k), 1.1 * k, 1.1 * k, 1.5 * k, "flash_core")]
    return out


def scorch(z=0.0, rx_=18.0, rz_=24.0):
    return ell(tr(0, 0.25, z), rx_, 0.45, rz_, "scorch")


# ---------------------------------------------------------------- mount / pivot
def mount_px(ring, yaw_deg, res):
    """ring (hull model coords) -> screen offset (dx, dy) in px from the hull ground anchor at this resolution."""
    w = (ry(yaw_deg) @ np.array([ring[0], ring[1], ring[2], 1.0]))[:3]
    return float(w[0] * res.zoom), float(-(w @ V.UP) * res.zoom)


def render_hull(prims, ov, yaw, res):
    return make_frame(prims, ov, yaw, res=res)


def render_turret(prims, ov, yaw, res, ring_y):
    """yoff = ring height so the ground darkening (world y < 9) is not applied to the turret."""
    return make_frame(prims, ov, yaw, res=res, yoff=ring_y)


def blit(dst: Image.Image, src: Image.Image, x: int, y: int):
    """alpha-composite src onto dst at (x, y), any offset (negative / overflowing allowed)."""
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(dst.width, x + src.width), min(dst.height, y + src.height)
    if x1 <= x0 or y1 <= y0:
        return
    crop = src.crop((x0 - x, y0 - y, x1 - x, y1 - y))
    dst.alpha_composite(crop, (x0, y0))


def compose(hull: Frame, turret: Frame | None, hres, tres, mount, mirror_hull=False, mirror_turret=False) -> Frame:
    """hull cell + turret cell (pivot at hull anchor + mount) -> one Frame of the hull size. mirror_* flips the cell
    (and the mount x) the way the game does for the western facings."""
    out = Frame(hres.fw, hres.fh)
    hb, ht = hull.base, hull.team
    if mirror_hull:
        hb, ht = hb.transpose(Image.FLIP_LEFT_RIGHT), ht.transpose(Image.FLIP_LEFT_RIGHT)
    out.base.alpha_composite(hb)
    out.team.alpha_composite(ht)
    if turret is not None:
        # mount = (dx, dy) or a list of them (several turrets: far / higher-on-screen ones first, like the game)
        mounts = [mount] if len(mount) == 2 and not hasattr(mount[0], "__len__") else list(mount)
        tb, tt = turret.base, turret.team
        if mirror_turret:
            tb, tt = tb.transpose(Image.FLIP_LEFT_RIGHT), tt.transpose(Image.FLIP_LEFT_RIGHT)
        for mx, my in sorted(mounts, key=lambda m: m[1]):
            if mirror_hull:
                mx = -mx
            px = int(round(hres.ax + mx - tres.ax)); py = int(round(hres.ay + my - tres.ay))
            blit(out.base, tb, px, py)
            blit(out.team, tt, px, py)
    return out


# ---------------------------------------------------------------- checks
def edge_touch(frame: Frame):
    b = np.array(frame.base); t = np.array(frame.team)
    a = b[..., 3] | t[..., 3]
    return bool(a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any())


def edge_margin(frame: Frame):
    """min distance (px) of opaque pixels from the border (0 = touching)."""
    b = np.array(frame.base); t = np.array(frame.team)
    a = (b[..., 3] | t[..., 3]) > 0
    ys, xs = np.where(a)
    if xs.size == 0:
        return None
    h, w = a.shape
    return int(min(xs.min(), ys.min(), w - 1 - xs.max(), h - 1 - ys.max()))


def shade_coverage(frames, mats):
    """-> {mat: number of its 8 ramp colours present (exact RGB) in the frames}."""
    rgb = np.concatenate([np.array(f.base)[..., :3][np.array(f.base)[..., 3] > 0] for f in frames])
    present = {tuple(c) for c in np.unique(rgb, axis=0)}
    out = {}
    for m in mats:
        out[m] = sum(1 for c in V.MATERIALS[m] if tuple(c[:3]) in present)
    return out


def team_coverage(frames):
    g = np.concatenate([np.array(f.team)[..., 0][np.array(f.team)[..., 3] > 0] for f in frames])
    return int(len(np.unique(g))), int(g.size)


def diff_px(a: Image.Image, b: Image.Image):
    """pixels where the two RGBA images differ (either alpha or colour)."""
    A = np.array(a).astype(int); B = np.array(b).astype(int)
    return int((np.abs(A - B).sum(-1) > 0).sum())
