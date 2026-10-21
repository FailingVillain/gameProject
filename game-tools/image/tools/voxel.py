"""
voxel.py - tiny 3D primitive engine -> quarter-view pixel-art sprite renderer.

Pipeline
  1. Figure builder (`build_figure`) turns a pose (joint angles / IK targets) + kit into a list of rigid
     primitives (box / ellipsoid / capsule / cylinder) in MODEL space (x = character's left, y up, z forward,
     origin = point on the ground between the feet).
  2. `render_frame` rotates the model about the vertical axis (yaw for s/se/e/ne/n), ray-casts every primitive
     orthographically from a fixed elevation, z-buffers, cel-shades (NS = 8 bands) to the material ramp, downsamples
     by MODE (no blur), adds interior depth lines + outline (Res.outline_px), removes stray pixels, and returns a
     common.Frame (base + team layer). Output size / zoom / line widths come from a `Res` profile (RES = 128x160,
     LODS = 128 / 64 / 32); model units are tuned to the 64x80 design resolution.
World axes for the camera: x = screen right, y = up, z = toward the viewer (screen down).
"""
from __future__ import annotations

import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import FW, FH, BASE_FW, ANCHOR, P, Frame  # noqa: E402
import tune  # noqa: E402

ELEV = math.radians(36.0)
BASE_ZOOM = tune.render("BASE_ZOOM")                          # model units -> pixels at the 64x80 design resolution (1.06 until v0.5; -12% so fall/dead fit)
SS = 3                                    # supersampling (mode-downsampled, never blended)
PLACKET_HW = 0.6                          # infantry-default-old: half width of the red front placket line (model units)
COLLAR_PIPE_T = 0.55                      # red collar top-edge band thickness
CUFF_W = 1.3                              # red cuff band width along the forearm
STRIPE_K = tune.render("STRIPE_K")                          # officer trouser seam stripe: cos(half-angle) of the outer seam band (0.93 = wide; 0.985 = thin line)
NS = 8                                    # cel-shade levels per material
AO_STEP = max(1, NS // 8)                 # shade bands removed per AO / ground-darkening step
AO_RAD_BASE = 3                           # AO neighbourhood radius in pixels at the design resolution
LINE_K = tune.render("LINE_K")                            # interior depth-line darkening factor (colour x k; lower = darker)
LINE_THR = tune.render("LINE_THR")                        # depth jump (model units) that triggers an interior line
RBOX_ROUND = tune.render("RBOX_ROUND")                          # default rbox corner radius (model units) -> limbs / pelvis read rounder
RBOX_MAX_FRAC = 0.7                       # corner radius never exceeds this fraction of the smallest half-size
TORSO_ROUND = 5.0                         # waist rbox radius (with frac 0.95 -> pillow shape); chest / shoulders are ellipsoids


class Res:
    """Output resolution profile: every pixel-sized quantity derives from the frame width (design base = 64x80)."""

    def __init__(self, fw, fh):
        self.fw, self.fh = int(fw), int(fh)
        self.pxs = fw / BASE_FW                               # pixel scale vs the design resolution
        self.ax, self.ay = self.fw // 2, self.fh * 9 // 10    # feet anchor
        self.zoom = BASE_ZOOM * self.pxs                      # model units -> pixels
        self.outline_px = max(1, int(round(self.pxs)))        # outer outline: 1 px at 64/32, 2 px at 128
        self.outer_px = max(1, int(1.5 * self.pxs + 0.5))      # DEFAULT outer silhouette outline (strong): 3 px at 128, 2 at 64, 1 at 32
        self.line_px = self.outline_px                        # interior depth-line width
        self.edge_pad = max(1, int(round(2 * self.pxs)))      # auto-fit: min distance of opaque pixels from the border
        self.edge_nudge = max(1, int(round(2 * self.pxs)))    # auto-fit: extra push when actually touching the border (clipped extent unknown)
        self.ao_rad = max(1, int(round(AO_RAD_BASE * self.pxs)))
        self.ss = SS

    @property
    def size(self):
        return self.fw, self.fh

    @property
    def anchor(self):
        return self.ax, self.ay

    @property
    def tag(self):
        return f"@{self.fw}"


RES = Res(FW, FH)                                             # main output (128x160)
LODS = [RES, Res(BASE_FW, BASE_FW * 5 // 4), Res(BASE_FW // 2, BASE_FW * 5 // 8)]   # 128x160, 64x80, 32x40
if os.environ.get('KD_RES96') == '1':                    # 96x120 mode (tools/res96.py): LOD0 192x240 / 96x120 / 48x60
    LODS = [Res(BASE_FW * 3, BASE_FW * 15 // 4), Res(BASE_FW * 3 // 2, BASE_FW * 15 // 8), Res(BASE_FW * 3 // 4, BASE_FW * 15 // 16)]
    RES = LODS[0]
ZOOM = RES.zoom
OUTLINE_PX = RES.outline_px
OUTLINE_COLOR = (8, 8, 8, 255)                              # default outer outline colour (strong outline is now the default)
assert os.environ.get("KD_RES96") == "1" or RES.anchor == tuple(ANCHOR)
YAWS = {"s": 0, "se": 45, "e": 90, "ne": 135, "n": 180,
        "sse": 22.5, "ese": 67.5, "ene": 112.5, "nne": 157.5}     # 22.5 deg rows: units with "dirs": DIRS16 (LMG gunner, aim must match the target bearing)
LIGHT = np.array([-0.5, 0.8, 0.45])
LIGHT = LIGHT / np.linalg.norm(LIGHT)
CAM = np.array([0.0, math.sin(ELEV), math.cos(ELEV)])     # unit vector toward camera
UP = np.array([0.0, math.cos(ELEV), -math.sin(ELEV)])     # screen-up in world


# ---------------------------------------------------------------- materials
def _s(c, k):
    return (min(255, int(c[0] * k)), min(255, int(c[1] * k)), min(255, int(c[2] * k)), 255)


def _ramp(dark, mid_d, mid, light):
    return [dark, mid_d, mid, light]


MATERIALS: dict[str, list] = {
    "tankod":  _ramp((80, 86, 58, 255), (80, 86, 58, 255), (80, 86, 58, 255), (80, 86, 58, 255)),
    "tank2":   _ramp((64, 70, 46, 255), (64, 70, 46, 255), (64, 70, 46, 255), (64, 70, 46, 255)),
    "track":   _ramp((52, 48, 42, 255), (52, 48, 42, 255), (52, 48, 42, 255), (52, 48, 42, 255)),
    "track2":  _ramp((78, 70, 58, 255), (78, 70, 58, 255), (78, 70, 58, 255), (78, 70, 58, 255)),
    "rubber":  _ramp((42, 42, 40, 255), (42, 42, 40, 255), (42, 42, 40, 255), (42, 42, 40, 255)),
    "burn":    _ramp((60, 56, 52, 255), (60, 56, 52, 255), (60, 56, 52, 255), (60, 56, 52, 255)),
    "burn_s":  _ramp((42, 42, 46, 255), (42, 42, 46, 255), (42, 42, 46, 255), (42, 42, 46, 255)),
    "burn_t":  _ramp((36, 34, 32, 255), (36, 34, 32, 255), (36, 34, 32, 255), (36, 34, 32, 255)),
    "burn_t2": _ramp((52, 48, 42, 255), (52, 48, 42, 255), (52, 48, 42, 255), (52, 48, 42, 255)),
    "char":    _ramp((48, 36, 30, 255), (48, 36, 30, 255), (48, 36, 30, 255), (48, 36, 30, 255)),
    "void":    _ramp((14, 12, 12, 255), (14, 12, 12, 255), (14, 12, 12, 255), (14, 12, 12, 255)),
    "eye":     [P["eye"]] * 4,
    "team":    [(95, 95, 95, 255), (140, 140, 140, 255), (190, 190, 190, 255), (236, 236, 236, 255)],
    "flash_core": [P["flash_core"]] * 4,
    "flash":   [P["flash"]] * 4,
    "flash_edge": [(230, 120, 40, 255)] * 4,
    "smoke_a": [(214, 208, 192, 255)] * 4,
    "smoke_b": [(168, 162, 150, 255)] * 4,
    "smoke_c": [(124, 118, 110, 255)] * 4,
    "fire_r":  [(170, 50, 20, 255)] * 4,
    "fire_w":  [(255, 236, 170, 255)] * 4,
}
def _cl(v):
    return tuple(int(max(0, min(255, round(x)))) for x in v[:3]) + (255,)


def _ramp8(anchors, n=NS):
    """n-step cel ramp: piecewise-linear interpolation through the palette anchors (dark -> light).
    The anchors are extended by a deep (slightly blue) shadow below and a warm highlight above so the
    8 bands span about the same range as the old 5-tone ramps while the palette colours stay in the middle."""
    a = [np.array(c[:3], float) for c in anchors]
    deep = a[0] * 0.72 + np.array([2, 2, 10])
    hi = a[-1] * 1.18 + np.array([12, 9, 2])
    pts = [deep] + a + [hi]
    ts = np.linspace(0, len(pts) - 1, n)
    out = []
    for t in ts:
        i = min(int(t), len(pts) - 2)
        f = t - i
        out.append(_cl(pts[i] * (1 - f) + pts[i + 1] * f))
    return out


UNLIT = {"flash_core", "flash", "flash_edge", "smoke_a", "smoke_b", "smoke_c", "fire_r", "fire_w"}
FLASH_EPS = 0.5          # model units: body must be this much nearer than the flash to hide it (muzzle-to-barrel contact stays visible)
for _k, _v in tune.get("materials", "ramps").items():           # colour anchors live in tune/materials.json
    MATERIALS[_k] = _ramp(*[tuple(c[:3]) + (255,) for c in _v])
for _k, _v in list(MATERIALS.items()):
    if _k in UNLIT or _k == "eye":
        MATERIALS[_k] = [_v[0]] * NS
    elif _k == "team":
        MATERIALS[_k] = [_cl((g, g, g)) for g in np.linspace(60, 245, NS)]
    else:
        MATERIALS[_k] = _ramp8(_v)
_ord = tune.get("materials", "order")                         # keep the original material index order (renderer tie-breaks)
MATERIALS = {**{k: MATERIALS[k] for k in _ord if k in MATERIALS}, **MATERIALS}
MAT_NAMES = list(MATERIALS)
MAT_IDX = {n: i for i, n in enumerate(MAT_NAMES)}
TEAM_MATS = {"team"}
ALIAS: dict[str, str] = {}          # "tb:<cloth>" -> cloth (headband overlay materials)


def tband(mat):
    """headband overlay material: renders exactly like `mat` in the base sheet AND marks the pixels in the team layer
    (grey ramp) -> the game may draw the team-colour headband over the base art (same-faction battles only);
    when it does not, the base shows the plain cloth."""
    n = "tb:" + mat
    if n not in MATERIALS:
        ALIAS[n] = mat
        MATERIALS[n] = MATERIALS[mat]
        MAT_NAMES.append(n)
        MAT_IDX[n] = len(MAT_NAMES) - 1
    return n


# ---------------------------------------------------------------- matrices
def rx(a):
    a = math.radians(a); c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0, 0], [0, c, -s, 0], [0, s, c, 0], [0, 0, 0, 1.0]])


def ry(a):
    a = math.radians(a); c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s, 0], [0, 1, 0, 0], [-s, 0, c, 0], [0, 0, 0, 1.0]])


def rz(a):
    a = math.radians(a); c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0, 0], [s, c, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1.0]])


def tr(x, y=None, z=None):
    if y is None:
        x, y, z = x
    m = np.eye(4); m[:3, 3] = (x, y, z)
    return m


def pt(M, p):
    return (M @ np.array([p[0], p[1], p[2], 1.0]))[:3]


# ---------------------------------------------------------------- primitives
class Prim:
    __slots__ = ("kind", "dims", "M", "mat", "paint", "clipy", "tag", "round", "src", "fig")

    def __init__(self, kind, dims, M, mat, paint=None, clipy=None, tag=None, round_=True):
        self.kind, self.dims, self.M, self.mat = kind, dims, M, mat
        self.paint, self.clipy, self.tag, self.round = paint or [], clipy, tag, round_
        self.fig = None           # figure id (set by build_figure; collide.py groups multi-figure scenes by it)
        self.src = "body"         # body | headgear | gear | weapon  (set by build_figure; lets a later step render gear as its own layer)

    def moved(self, T):
        q = Prim(self.kind, self.dims, T @ self.M, self.mat, self.paint, self.clipy, self.tag, self.round)
        q.src = self.src
        q.fig = self.fig
        return q

    def radius(self):
        d = self.dims
        if self.kind in ("box", "rbox"):
            return float(np.linalg.norm(d[:3]))
        if self.kind == "cone":
            return d[0] + max(d[1], d[2])
        if self.kind == "ell":
            return float(max(d))
        if self.kind == "cap":
            return d[1] + d[0]
        return math.hypot(d[0], d[1])


def box(M, hx, hy, hz, mat, paint=None, tag=None):
    return Prim("box", (hx, hy, hz), M, mat, paint, tag=tag)


def ell(M, rx_, ry_, rz_, mat, clipy=None, paint=None):
    return Prim("ell", (rx_, ry_, rz_), M, mat, paint, clipy)


def cap(M, r, hl, mat, paint=None):          # along local Y, round ends
    return Prim("cap", (r, hl), M, mat, paint)


def cyl(M, r, hl, mat, paint=None):          # along local Y, flat ends
    return Prim("cyl", (r, hl), M, mat, paint)


def rbox(M, hx, hy, hz, mat, paint=None, tag=None, r=None, frac=None):
    """rounded box; r = corner radius (default RBOX_ROUND), clamped to frac (default RBOX_MAX_FRAC) of the smallest half-size.
    frac close to 1 gives a pillow / stadium shape (soft torso, pack)."""
    frac = RBOX_MAX_FRAC if frac is None else frac
    r = min(RBOX_ROUND if r is None else r, frac * min(hx, hy, hz))
    return Prim("rbox", (hx, hy, hz, r), M, mat, paint, tag=tag)


def seg(p0, p1, r, mat, paint=None, flat=False, r1=None):
    """capsule/cylinder from p0 to p1 (model space). local +y points p0 -> p1."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    v = p1 - p0
    L = float(np.linalg.norm(v))
    y = v / max(L, 1e-9)
    ref = np.array([0, 0, 1.0]) if abs(y[2]) < 0.9 else np.array([1.0, 0, 0])
    x = np.cross(ref, y); x /= np.linalg.norm(x)
    z = np.cross(x, y)
    M = np.eye(4)
    M[:3, 0], M[:3, 1], M[:3, 2], M[:3, 3] = x, y, z, (p0 + p1) / 2
    if r1 is not None:
        return Prim('cone', (L / 2, r, r1), M, mat, paint)
    return (cyl if flat else cap)(M, r, L / 2, mat, paint)


def along_z(M, r, z0, z1, mat, paint=None, flat=True):
    """cylinder along local Z from z0 to z1 (weapon barrels, tubes)."""
    Mz = M @ tr(0, 0, (z0 + z1) / 2) @ rx(90)
    return (cyl if flat else cap)(Mz, r, abs(z1 - z0) / 2, mat, paint)


# ---------------------------------------------------------------- ray casting
BIG = 1e9


def _hit_box(o, d, h):
    N = o.shape[0]
    tlo = np.full((N, 3), -BIG); thi = np.full((N, 3), BIG)
    for i in range(3):
        if abs(d[i]) < 1e-9:
            ok = np.abs(o[:, i]) <= h[i]
            tlo[:, i] = np.where(ok, -BIG, BIG)
            thi[:, i] = np.where(ok, BIG, -BIG)
        else:
            a = (-h[i] - o[:, i]) / d[i]; b = (h[i] - o[:, i]) / d[i]
            tlo[:, i] = np.minimum(a, b); thi[:, i] = np.maximum(a, b)
    te = tlo.max(1); tx = thi.min(1)
    m = te <= tx
    ax = thi.argmin(1)
    n = np.zeros((N, 3))
    sg = np.where(d[ax] >= 0, 1.0, -1.0)
    n[np.arange(N), ax] = sg
    return m, tx, n


def _hit_ell(o, d, r, clipy=None):
    r = np.asarray(r, float)
    os_ = o / r; ds = d / r
    a = ds @ ds
    b = os_ @ ds
    c = (os_ * os_).sum(1) - 1
    disc = b * b - a * c
    m = disc >= 0
    t = (-b + np.sqrt(np.maximum(disc, 0))) / a
    p = o + t[:, None] * d
    if clipy is not None:
        m &= p[:, 1] >= clipy
    n = p / (r * r)
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)
    return m, t, n


def _hit_cyl(o, d, r, hl, round_):
    N = o.shape[0]
    best = np.full(N, -BIG); n = np.zeros((N, 3)); m = np.zeros(N, bool)

    def cand(valid, t, nn):
        nonlocal best, n, m
        better = valid & (t > best)
        best = np.where(better, t, best)
        n = np.where(better[:, None], nn, n)
        m |= valid

    a = d[0] ** 2 + d[2] ** 2
    if a > 1e-12:
        b = o[:, 0] * d[0] + o[:, 2] * d[2]
        c = o[:, 0] ** 2 + o[:, 2] ** 2 - r * r
        disc = b * b - a * c
        t = (-b + np.sqrt(np.maximum(disc, 0))) / a
        y = o[:, 1] + t * d[1]
        valid = (disc >= 0) & (np.abs(y) <= hl)
        px = o[:, 0] + t * d[0]; pz = o[:, 2] + t * d[2]
        nn = np.stack([px / r, np.zeros(N), pz / r], 1)
        cand(valid, t, nn)
    for s in (1.0, -1.0):
        if round_:
            cen = np.array([0, s * hl, 0.0])
            oc = o - cen
            b = oc @ d
            c = (oc * oc).sum(1) - r * r
            disc = b * b - c
            t = -b + np.sqrt(np.maximum(disc, 0))
            p = oc + t[:, None] * d
            valid = (disc >= 0) & (p[:, 1] * s >= 0)
            cand(valid, t, p / r)
        elif abs(d[1]) > 1e-9:
            t = (s * hl - o[:, 1]) / d[1]
            px = o[:, 0] + t * d[0]; pz = o[:, 2] + t * d[2]
            valid = (px * px + pz * pz) <= r * r
            nn = np.zeros((N, 3)); nn[:, 1] = s
            cand(valid, t, nn)
    return m, best, n


def _sdf(p, X):
    if p.kind == "rbox":
        hx, hy, hz, r = p.dims
        q = np.abs(X) - np.array([hx - r, hy - r, hz - r])
        return np.linalg.norm(np.maximum(q, 0), axis=1) + np.minimum(q.max(1), 0) - r
    hl, r0, r1 = p.dims
    h = np.clip((X[:, 1] + hl) / (2 * hl), 0, 1)
    c = np.zeros_like(X); c[:, 1] = -hl + 2 * hl * h
    return np.linalg.norm(X - c, axis=1) - (r0 + (r1 - r0) * h)


def _march(p, ol, dl):
    N = ol.shape[0]
    rad = p.radius() + 0.5
    tc = -(ol @ dl)
    miss = ((ol * ol).sum(1) - tc * tc) > rad * rad
    tt = tc + rad
    hit = np.zeros(N, bool)
    act = np.where(~miss)[0]
    for _ in range(40):
        if act.size == 0:
            break
        X = ol[act] + tt[act, None] * dl
        d = _sdf(p, X)
        h = d < 0.03
        hit[act[h]] = True
        keep = ~h & (tt[act] > tc[act] - rad)
        tt[act[keep]] -= np.maximum(d[keep], 0.03)
        act = act[keep]
    n = np.zeros((N, 3))
    hi = np.where(hit)[0]
    if hi.size:
        X = ol[hi] + tt[hi, None] * dl
        e = 0.12
        for i in range(3):
            o = np.zeros(3); o[i] = e
            n[hi, i] = _sdf(p, X + o) - _sdf(p, X - o)
        n[hi] /= np.maximum(np.linalg.norm(n[hi], axis=1, keepdims=True), 1e-9)
    return hit, tt, n


# NS-1 thresholds on n.l (range -1..1); band 0 = deepest shadow, NS-1 = brightest. Bands are a bit denser around
# grazing light where the old 5-tone ramp had its steps, so the silhouette reads the same.
SHADE_THR = np.array([-0.45, -0.22, -0.02, 0.16, 0.34, 0.52, 0.72])
assert len(SHADE_THR) == NS - 1


def _shade(nw):
    l = nw @ LIGHT
    return np.digitize(l, SHADE_THR)


def raster(prims, yaw, zoom=ZOOM, ss=SS, shaded=True, shx=0.0, lift=0.0, fw=FW, fh=FH, ax=ANCHOR[0], ay=ANCHOR[1]):
    """-> (ids int array (h*ss, w*ss) with -1 = empty, depth float array)."""
    W, H = fw * ss, fh * ss
    ids = np.full((H, W), -1, np.int32)
    zb = np.full((H, W), -BIG)
    yb = np.zeros((H, W))
    Ry = ry(yaw)
    xs = ((np.arange(W) + 0.5) / ss - ax) / zoom
    ys = ((np.arange(H) + 0.5) / ss - (ay - lift)) / zoom            # screen-down, model units
    for p in prims:
        Mw = Ry @ p.M
        Rw = Mw[:3, :3]; T = Mw[:3, 3] + np.array([shx, 0, 0])
        rad = p.radius()
        cx = ax + T[0] * zoom; cy = ay - lift + (-(T @ UP)) * zoom
        rr = rad * zoom + 3                                  # pixel margin around the bounding sphere
        x0 = max(0, int((cx - rr) * ss)); x1 = min(W, int((cx + rr) * ss) + 1)
        y0 = max(0, int((cy - rr) * ss)); y1 = min(H, int((cy + rr) * ss) + 1)
        if x0 >= x1 or y0 >= y1:
            continue
        SX, SY = np.meshgrid(xs[x0:x1], ys[y0:y1])
        o = SX.reshape(-1, 1) * np.array([1.0, 0, 0]) + (-SY.reshape(-1, 1)) * UP
        ol = (o - T) @ Rw
        dl = CAM @ Rw
        if p.kind == "box":
            m, t, nl = _hit_box(ol, dl, np.array(p.dims))
        elif p.kind == "ell":
            m, t, nl = _hit_ell(ol, dl, p.dims, p.clipy)
        elif p.kind in ("rbox", "cone"):
            m, t, nl = _march(p, ol, dl)
        else:
            m, t, nl = _hit_cyl(ol, dl, p.dims[0], p.dims[1], p.kind == "cap")
        if not m.any():
            continue
        zsub = zb[y0:y1, x0:x1].reshape(-1)
        win = m & (t > zsub)
        if not win.any():
            continue
        nw = nl @ Rw.T
        pl = ol + t[:, None] * dl
        mat = np.full(len(t), MAT_IDX[p.mat], np.int32)
        for fn, mname in p.paint:
            mat = np.where(fn(pl), MAT_IDX[mname], mat)
        if shaded:
            sh = _shade(nw)
        else:
            sh = np.zeros(len(t), int)
        idv = mat * NS + sh
        isub = ids[y0:y1, x0:x1].reshape(-1)
        isub = np.where(win, idv, isub)
        zsub = np.where(win, t, zsub)
        ysub = yb[y0:y1, x0:x1].reshape(-1)
        ysub = np.where(win, (pl @ Rw.T + T)[:, 1], ysub)
        yb[y0:y1, x0:x1] = ysub.reshape(y1 - y0, x1 - x0)
        ids[y0:y1, x0:x1] = isub.reshape(y1 - y0, x1 - x0)
        zb[y0:y1, x0:x1] = zsub.reshape(y1 - y0, x1 - x0)
    return ids, zb, yb


def downsample(ids, zb, ss=SS, yb=None):
    h, w = ids.shape
    H, W = h // ss, w // ss
    blk = ids.reshape(H, ss, W, ss).transpose(0, 2, 1, 3).reshape(H, W, ss * ss)
    zbk = zb.reshape(H, ss, W, ss).transpose(0, 2, 1, 3).reshape(H, W, ss * ss)
    cover = (blk >= 0).sum(-1)
    best = np.full((H, W), -1, np.int32)
    bc = np.zeros((H, W), int)
    for u in np.unique(ids[ids >= 0]):
        c = (blk == u).sum(-1)
        m = c > bc
        best[m] = u; bc[m] = c[m]
    opaque = cover * 2 > ss * ss
    best[~opaque] = -1
    zsum = np.where(blk >= 0, zbk, 0).sum(-1)
    z = np.where(cover > 0, zsum / np.maximum(cover, 1), -BIG)
    if yb is None:
        return best, z
    ybk = yb.reshape(H, ss, W, ss).transpose(0, 2, 1, 3).reshape(H, W, ss * ss)
    y = np.where(cover > 0, np.where(blk >= 0, ybk, 0).sum(-1) / np.maximum(cover, 1), 0)
    return best, z, y


# ---------------------------------------------------------------- post-process
def _nb4(a, fill):
    pad = np.pad(a, 1, constant_values=fill)
    return [pad[1:-1, 2:], pad[1:-1, :-2], pad[2:, 1:-1], pad[:-2, 1:-1]]


def make_frame(prims, overlay, d, line_thr=None, net=True, line_k=None, push=0.0, lift=0.0, dx_px=0.0, ao=True, ao_thr=2.2,
               fw=None, fh=None, ax=None, ay=None, yoff=0.0, zoom=None, ss=None, outline=True, res: Res | None = None,
               outline_color=None, outline_width=None) -> Frame:
    """res = output profile (default RES). Explicit fw/fh/ax/ay/zoom override it (legacy vehicle / fx generators).
    lift / dx_px are in output pixels; push, line_thr, ao_thr are in model units (resolution independent)."""
    res = res or RES
    line_thr = LINE_THR if line_thr is None else line_thr
    line_k = LINE_K if line_k is None else line_k
    fw = res.fw if fw is None else fw; fh = res.fh if fh is None else fh
    ax = res.ax if ax is None else ax; ay = res.ay if ay is None else ay
    zoom = res.zoom if zoom is None else zoom; ss = res.ss if ss is None else ss
    yaw = YAWS[d] if isinstance(d, str) else float(d)
    shx = -push * math.sin(math.radians(yaw)) + dx_px / zoom
    ids, zb, yb = raster(prims, yaw, zoom=zoom, ss=ss, shx=shx, lift=lift, fw=fw, fh=fh, ax=ax, ay=ay)
    best, z, yh = downsample(ids, zb, ss, yb)
    yh = yh + yoff
    H, W = best.shape
    op = best >= 0
    # remove isolated pixels
    cnt = sum(n.astype(int) for n in _nb4(op, False))
    op &= cnt > 0
    best = np.where(op, best, -1)
    # colours
    base = np.zeros((H, W, 4), np.uint8)
    team = np.zeros((H, W, 4), np.uint8)
    is_team = np.zeros((H, W), bool)
    is_over = np.zeros((H, W), bool)                            # headband overlay pixels: base keeps the cloth colour, team layer marks them
    mat_i = np.where(op, best // NS, 0); sh = np.where(op, best % NS, 0)
    if ao:
        R = res.ao_rad                                        # screen-space AO window radius (pixels)
        zc = np.where(op, z, 0.0)
        zp = np.pad(zc, R); op_p = np.pad(op, R)
        ssum = np.zeros((H, W)); scnt = np.zeros((H, W))
        for dy_ in range(-R, R + 1):
            for dx_ in range(-R, R + 1):
                if dx_ == 0 and dy_ == 0:
                    continue
                zz = zp[R + dy_:R + dy_ + H, R + dx_:R + dx_ + W]; oo = op_p[R + dy_:R + dy_ + H, R + dx_:R + dx_ + W]
                ssum += np.where(oo, zz, 0); scnt += oo
        zm = ssum / np.maximum(scnt, 1)
        min_cnt = 0.25 * ((2 * R + 1) ** 2 - 1)               # need a quarter of the window covered (12 of 48 at R=3)
        occ = np.where(op & (scnt > min_cnt), zm - z, 0)
        sh = sh - AO_STEP * ((occ > ao_thr).astype(int) + (occ > ao_thr * 2.4).astype(int))
        sh = sh - AO_STEP * ((yh < 9.0).astype(int) + (yh < 3.0).astype(int))          # ground darkening (model units)
        sh = np.where(op, np.clip(sh, 0, NS - 1), 0)
    yy, xx = np.mgrid[0:H, 0:W]
    col = np.zeros((H, W, 4), np.uint8)
    for mi in np.unique(mat_i[op]):
        name = MAT_NAMES[mi]
        sel = op & (mat_i == mi)
        for s in range(NS):
            ss_ = sel & (sh == s)
            if not ss_.any():
                continue
            col[ss_] = MATERIALS[ALIAS.get(name, name)][s]
        if name in TEAM_MATS:
            is_team |= sel
        elif name in ALIAS:
            is_over |= sel
    if net:
        # helmet net: checker dither over the mid bands (same relative range as the old 1..3 of 5); cell = OUTLINE_PX pixels
        lo, hi = int(NS * 0.2), int(NS * 0.75)
        cell = res.outline_px
        hm = op & (mat_i == MAT_IDX["helmet"]) & (sh >= lo) & (sh <= hi) & ((((xx // cell) + (yy // cell)) % 2) == 0)
        for s in range(lo, hi + 1):
            m = hm & (sh == s)
            col[m] = MATERIALS["net"][s]
        hv = op & (mat_i == MAT_IDX["helmet_v"]) & (sh >= lo) & ((((xx // (2 * cell)) + (yy // (2 * cell))) % 2) == 0)
        lighter = np.clip(col[..., :3].astype(int) + 26, 0, 255).astype(np.uint8)
        col[..., :3] = np.where(hv[..., None], lighter, col[..., :3])
    # interior depth lines (darken the far side of big depth jumps), res.line_px wide
    dark = np.zeros((H, W), bool)
    for k in range(1, res.line_px + 1):
        zp = np.pad(z, k, constant_values=-BIG); opp = np.pad(op, k, constant_values=False)
        for zn, on in (((zp[k:-k, 2 * k:], opp[k:-k, 2 * k:])), (zp[k:-k, :-2 * k], opp[k:-k, :-2 * k]),
                       (zp[2 * k:, k:-k], opp[2 * k:, k:-k]), (zp[:-2 * k, k:-k], opp[:-2 * k, k:-k])):
            dark |= op & on & ((zn - z) > line_thr)
    # flag cloth (kd_fl_*) is flat colour: no interior depth lines there (they turned white/red into dark specks, 2026-10-21)
    dark &= ~np.isin(mat_i, [MAT_IDX[n] for n in MAT_NAMES if n.startswith("kd_fl_")])
    dc = (col[..., :3] * line_k).astype(np.uint8)
    col[..., :3] = np.where(dark[..., None], dc, col[..., :3])
    base[op & ~is_team] = col[op & ~is_team]
    base[is_over] = col[is_over]
    team[is_team] = col[is_team]
    team[is_team, 3] = 255
    if is_over.any():
        tg = np.array(MATERIALS["team"], np.uint8)[sh[is_over]]
        team[is_over] = tg
        team[is_over, 3] = 255
    f = Frame(fw, fh)
    f.base = Image.fromarray(base, "RGBA")
    f.team = Image.fromarray(team, "RGBA")
    if outline:
        # opt-in per-unit override (unit json "outline"): colour / outer width only; interior lines + net cell keep res values
        f.outline(color=outline_color or OUTLINE_COLOR, width=outline_width or res.outer_px)
    # overlay (muzzle flash / backblast): always on top, no outline
    if overlay:
        ob = np.full((fh, fw), -1, np.int32)
        oz_map = np.full((fh, fw), -BIG)
        for op_ in overlay:
            oi, oz, _oy = raster([op_], yaw, zoom=zoom, ss=ss, shaded=False, shx=shx, lift=lift, fw=fw, fh=fh, ax=ax, ay=ay)
            b1, z1 = downsample(oi, oz, ss)
            m1 = b1 >= 0
            ob = np.where(m1, b1, ob); oz_map = np.where(m1, z1, oz_map)
        # depth test against the body z-buffer: larger z = nearer the camera (same convention as raster's t > zsub).
        # Flash pixels behind a body pixel (head / cap / pack when firing away from the viewer) are hidden.
        behind = op & (z > oz_map + FLASH_EPS)
        ob = np.where(behind, -1, ob)
        arr = np.array(f.base)
        om = ob >= 0
        for mi in np.unique(ob[om] // NS):
            sel = om & (ob // NS == mi)
            arr[sel] = MATERIALS[MAT_NAMES[mi]][0]
        f.base = Image.fromarray(arr, "RGBA")
        t = np.array(f.team); t[om] = 0; f.team = Image.fromarray(t, "RGBA")
    f.anchor = (ax + shx * zoom, ay - lift)
    return f


# ---------------------------------------------------------------- figure rig
UPPER, LOWER = 10.5, 10.0         # arm segment lengths
THIGH, SHIN = 9.6, 9.6
THIGH_ROOT_SINK, THIGH_ROOT_NARROW = 2.4, 1.0   # seated: thigh start slides along the thigh / narrows so the flat root disc stays inside the pelvis
GEAR_RISE, GEAR_FWD = 0.0, 2.5   # ... and is nudged up / forward (toward the side of the hip)
GEAR_COUNTER, GEAR_LOW_Y0, GEAR_LOW_Y1 = 0.9, 0.0, 9.0   # gear below torso-height Y0 follows the pelvis fully, above Y1 follows the torso
SKIRT_COUNTER, SKIRT_SHORT, SKIRT_RISE, SKIRT_FWD = 1.0, 0.25, 0.4, 1.0   # seated-skirt compaction (see build_figure)
_B = tune.get("poses", "body")                  # slimming factors (tune/poses.json "body"): 1.0 = the old chunky body
B_LEG, B_ARM, B_TW, B_TD, B_HIP = _B["leg"], _B["arm"], _B["torso_w"], _B["torso_d"], _B["hip_x"]
SHOULDER_X, SHOULDER_Y = 11.4 * _B["shoulder_x"], 16.2


def arm_ik(S, H, a, b, pole):
    S, H = np.asarray(S, float), np.asarray(H, float)
    v = H - S
    d = float(np.linalg.norm(v))
    d = min(max(d, 1e-3), (a + b) * 0.995)
    u = v / max(np.linalg.norm(v), 1e-9)
    x = (a * a - b * b + d * d) / (2 * d)
    h = math.sqrt(max(a * a - x * x, 0))
    pole = np.asarray(pole, float)
    perp = pole - u * (pole @ u)
    if np.linalg.norm(perp) < 1e-6:
        perp = np.array([0, -1.0, 0])
    perp /= np.linalg.norm(perp)
    E = S + u * x + perp * h
    Hh = S + u * d
    return E, Hh


class Kit:
    def __init__(self, **kw):
        self.jacket = "od"; self.trousers = "khaki"; self.legging = "canvas"; self.boot = "leather"
        self.hand = "skin"; self.belt = "canvas"; self.helmet = "helmet"; self.net = True
        self.back = None          # fn(Tmat) -> prims (pack etc.)
        self.front = None         # fn(Tmat) -> prims
        self.hip = None           # fn(Tmat) -> prims
        self.helmet_extra = None
        self.headgear = "helmet"      # legacy: "helmet" | "cap"  -- new pipeline: fn(Hm, band: bool) -> prims (parts/headgear.py)
        self.belt_on = True
        self.team_band = True         # team colour on the headgear band
        self.team_patch = True        # team colour patch on the upper arms
        self.collar = False           # stand collar ring at the neck (WWI tunic)
        self.puttees = False          # legging drawn as wrapped puttees (alternating seam stripes)
        self.belt_pouches = True      # the two built-in belt pouches (gear parts may replace them)
        self.trouser_stripe = False   # team-colour stripe down the outer seam of each leg (officer)
        self.stripe_mat = "team"      # material of that outer-seam stripe (infantry-default-old: "piping_red")
        self.puttee_seam = "puttee_d"  # puttee wrap seam stripe material
        self.piping = None            # piping material name (e.g. "piping_red"): collar edge, placket, cuffs on the tunic
        self.placket = True           # False: no red front placket / button line (piping stays on collar and cuffs)
        self.__dict__.update(kw)


def _frame_mat(spec):
    """weapon frame spec: dict(pos, yaw, pitch(up deg), roll)."""
    x, y, z = spec.get("pos", (0, 0, 0))
    return tr(x, y, z) @ rz(spec.get("swing_z", 0)) @ ry(spec.get("yaw", 0)) @ rx(-spec.get("pitch", 0)) @ rz(spec.get("roll", 0))


class Weapon:
    """prims(M) -> list; grips in weapon-local coords; overlay(M, kind) -> overlay prims;
    extras = {name: fn(M) -> prims} optional attachments a pose can request with attach=[name] (e.g. bayonet)."""

    def __init__(self, prims, gripR, gripL, overlay=None, extras=None):
        self.prims, self.gripR, self.gripL, self.overlay = prims, gripR, gripL, overlay
        self.extras = extras or {}


def _src(prims, src):
    for p in prims:
        p.src = src
    return prims


def _wz_extent(p, Wi):
    """(centre z, half extent z) of a weapon prim in weapon-local coords."""
    R = Wi[:3, :3] @ p.M[:3, :3]; c = (Wi @ p.M[:, 3])[2]; d = p.dims
    if p.kind in ("box", "rbox", "ell"):
        h = sum(abs(R[2, i]) * d[i] * (np.linalg.norm(R[:, i]) and 1) for i in range(3))
    elif p.kind in ("cyl", "cap"):
        h = abs(R[2, 1]) * d[1] + d[0] * math.sqrt(max(0.0, 1 - R[2, 1] ** 2)) + (d[0] if p.kind == "cap" else 0)
    else:
        h = float(d[0])
    return c, h


def weapon_butt_z(prims, Wm):
    Wi = np.linalg.inv(Wm)
    return min(c - h for c, h in (_wz_extent(p, Wi) for p in prims))


def scale_weapon(prims, Wm, k, pivot, shape=True):
    """shorten weapon prims along the weapon-local z axis by k about z=pivot (stock end). shape=False: move only (muzzle flash)."""
    Wi = np.linalg.inv(Wm); out = []
    for p in prims:
        L = Wi @ p.M
        L2 = L.copy(); L2[2, 3] = pivot + k * (L[2, 3] - pivot)
        dims = list(p.dims)
        if shape:
            ax = int(np.argmax(np.abs(L[2, :3])))
            if abs(L[2, ax]) > 0.9:
                if p.kind in ("box", "rbox", "ell"):
                    dims[ax] *= k
                elif p.kind in ("cyl", "cap") and ax == 1:
                    dims[1] *= k
                elif p.kind == "cone" and ax == 1:
                    dims[0] *= k
        q = Prim(p.kind, tuple(dims), Wm @ L2, p.mat, p.paint, p.clipy, p.tag, p.round)
        q.src = p.src; q.fig = p.fig; out.append(q)
    return out


_FIG = 0


def build_figure(pose: dict, kit: Kit, weapon: Weapon | None = None, slung=None):
    """-> (prims, overlay prims). Every Prim carries .src in {body, headgear, gear, weapon} (body = default)."""
    g = pose.get
    prims: list[Prim] = []
    ov: list[Prim] = []
    root = tr(0, 26, 0)
    use_stripe = kit.trouser_stripe and kit.stripe_mat != "team"      # team-colour trouser stripes were removed (fixed-colour stripes still work)
    spread = g("spread", 4)
    feet = []

    # ---- legs
    for side, nm in ((1, "L"), (-1, "R")):
        sw = g("hip" + nm, 0); bend = g("knee" + nm, 0)
        H = root @ tr(side * 5.4 * B_HIP, -1, 0) @ rz(side * spread) @ rx(-sw)
        K = H @ tr(0, -THIGH, 0) @ rx(bend)
        A = K @ tr(0, -SHIN, 0)
        def stripe(r0, r1, hl, side=side):          # outer side seam: local +x = character's left for a hanging leg
            def fn(p):
                h = np.clip((p[:, 1] + hl) / (2 * hl), 0, 1)
                return side * p[:, 0] > STRIPE_K * (r0 + (r1 - r0) * h)
            return fn
        thigh_paint = [(stripe(5.2 * B_LEG, 4.0 * B_LEG, THIGH / 2), kit.stripe_mat)] if use_stripe else None
        knee_paint = [(lambda p, side=side: side * p[:, 0] > STRIPE_K * 4.1 * B_LEG, kit.stripe_mat)] if use_stripe else None
        _sl = float(np.clip((sw - 35.0) / 55.0, 0.0, 1.0))      # hip flexion 0 (<=35 deg) .. 1 (>=90 deg): sink the thigh root into the pelvis
        _H0, _K0 = pt(H, (0, 0, 0)), pt(K, (0, 0, 0))
        _H0 = _H0 + (_K0 - _H0) * (THIGH_ROOT_SINK * _sl / THIGH)
        prims.append(seg(_H0, _K0, 5.2 * B_LEG - THIGH_ROOT_NARROW * _sl, kit.trousers, r1=4.0 * B_LEG, paint=thigh_paint))
        prims.append(ell(K, 4.1 * B_LEG, 3.9 * B_LEG, 4.3 * B_LEG, kit.trousers, paint=knee_paint))                 # knee (cloth fold)
        prims[-2].tag, prims[-1].tag = "leg_" + nm, "leg_" + nm      # part tags (collide.py only, not rendered)
        shin_paint = [(lambda p: p[:, 1] > -0.3 * 5.5, kit.legging)]
        if use_stripe:
            shin_paint.append((stripe(4.0 * B_LEG, 3.1 * B_LEG, SHIN / 2), kit.stripe_mat))
        if kit.puttees:      # wrap seams: alternating darker stripes along the shin
            shin_paint.append((lambda p: (p[:, 1] > -0.3 * 5.5) & ((np.floor((p[:, 1] + 20.0) / 1.5) % 2) == 0), kit.puttee_seam))
        prims.append(seg(pt(K, (0, 0, 0)), pt(A, (0, 0, 0)), 4.0 * B_LEG, kit.trousers, paint=shin_paint, r1=3.1 * B_LEG))
        prims[-1].tag = "leg_" + nm
        shin_ang = sw - bend
        pitch = 0.5 * max(0.0, -shin_ang)
        Fm = A @ rx(pitch)
        Mf = Fm @ tr(0, -2.2, 1.6)
        prims.append(rbox(Mf @ tr(0, 0, -0.6), 3.6, 2.2, 4.2, kit.boot, tag="foot", r=1.5))
        prims.append(ell(Mf @ tr(0, -0.2, 2.6), 3.2, 2.0, 2.8, kit.boot))                 # toe cap
        prims[-1].tag = "foot"
        feet.append((Mf, (3.7, 2.2, 4.6)))
    # ---- pelvis
    _sk = float(np.clip((max(g("hipL", 0), g("hipR", 0)) - 35.0) / 55.0, 0.0, 1.0))   # hip flexion 0 (standing/walking) .. 1 (>=90 deg seated/kneeling)
    prims.append(rbox(root @ tr(0, 1.8, 0), 9.4 * B_TW, 3.8, 5.4 * B_TD, kit.trousers))              # pelvis (default RBOX_ROUND)

    # ---- torso: soft upper body (step 2) — ellipsoids + one large-radius rbox, same extents as the old boxes
    #      (width +-10.9, depth +-6.2, y -4.2 .. 19). Belt is painted onto the lower torso so it stays round.
    Tm = root @ tr(0, 4.2, 0) @ rx(g("lean", 0)) @ ry(g("twist", 0)) @ rz(g("roll", 0))
    J = kit.jacket
    belt_paint = [(lambda p: np.abs(p[:, 1] - (0.7 - 4.0)) < 1.3, kit.belt)] if kit.belt_on else None
    PIPE = kit.piping
    plk = (lambda p: (np.abs(p[:, 0]) < PLACKET_HW) & (p[:, 2] > 0)) if (PIPE and kit.placket) else None          # front placket / button line (thin red line)
    skirt_paint = [(plk, PIPE)] if plk else None
    waist_paint = (belt_paint or []) + ([(plk, PIPE)] if plk else []) or None                    # belt paint stays on top of nothing: placket drawn first
    if plk and belt_paint:
        waist_paint = [(plk, PIPE)] + belt_paint
    # seated / kneeling (large hip flexion): the forward-leaned torso swings the skirt's lower rear out behind the pelvis (the
    # "buttock sticking out") -> counter-rotate it and shorten its depth with the hip flexion (_sk = 0 standing/walking .. 1 at 90 deg)
    _lean = max(g("lean", 0), 0.0)
    prims.append(ell(Tm @ tr(0, -2.2 + SKIRT_RISE * _sk, SKIRT_FWD * _sk) @ rx(-SKIRT_COUNTER * _lean * _sk), 10.2 * B_TW, 2.6, 6.0 * B_TD * (1.0 - SKIRT_SHORT * _sk), J,
                     paint=skirt_paint))                                                       # jacket skirt below the belt
    prims.append(rbox(Tm @ tr(0, 4.0, 0), 9.9 * B_TW, 5.2, 5.7 * B_TD, J, r=TORSO_ROUND, frac=0.95, paint=waist_paint))   # waist / belly
    prims.append(ell(Tm @ tr(0, 12.0, 0.3), 11.0 * B_TW, 7.0, 6.4 * B_TD, J, paint=skirt_paint))              # chest
    for s in (1, -1):
        prims.append(ell(Tm @ tr(s * SHOULDER_X, SHOULDER_Y - 0.4, 0), 5.0 * B_ARM, 4.6 * B_ARM, 5.0 * B_ARM, J))        # round shoulders
    prims.append(cyl(Tm @ tr(0, 19.2, 0.6), 3.0, 2.0, kit.hand))
    if kit.collar:
        prims.append(cyl(Tm @ tr(0, 18.6, 0.6), 3.7, 1.1, J,
                         paint=[(lambda p: p[:, 1] > 1.1 - COLLAR_PIPE_T, PIPE)] if PIPE else None))   # stand collar (+ red top edge)
    if kit.belt_on and kit.belt_pouches:
        for s in (1, -1):
            prims.append(ell(Tm @ tr(s * 5.6, 0.7, 6.0), 2.4, 2.0, 1.5, kit.belt))               # belt pouches
    _gl = max(g("lean", 0), 0.0) * _sk * GEAR_COUNTER
    _Tin = np.linalg.inv(Tm) if _gl > 0 else None
    for fn in (kit.back, kit.front, kit.hip):
        if fn:
            gp = _src(fn(Tm), "gear")
            if _gl > 0:      # seated / kneeling: low-hanging gear (canteen, pouches, ...) follows the pelvis, not the leaned torso, so it stays tucked
                for i, q in enumerate(gp):
                    cy = (_Tin @ q.M[:, 3])[1]                              # centre height in torso coordinates
                    w = float(np.clip((GEAR_LOW_Y1 - cy) / (GEAR_LOW_Y1 - GEAR_LOW_Y0), 0.0, 1.0))
                    if w > 0:
                        gp[i] = q.moved(Tm @ tr(0, GEAR_RISE * w * _sk, GEAR_FWD * w * _sk) @ rx(-_gl * w) @ _Tin)
            prims += gp

    # ---- head
    Hm = Tm @ tr(0, 19.6, 0.5) @ rx(g("head_pitch", 0)) @ ry(g("head_yaw", 0)) @ rz(g("head_roll", 0))
    head = ell(Hm @ tr(0, 2.6, 1.0), 5.4, 5.8, 5.6, kit.hand,
               paint=[(lambda p: (p[:, 2] < -1.0) | (p[:, 1] > 4.5), 'hair')])
    head.tag = "head"
    prims.append(head)
    # (no eyes / eyebrows: the face stays a plain skin ellipsoid)
    if callable(kit.headgear):                 # new pipeline: parts/headgear.py function
        hg = kit.headgear(Hm, kit.team_band)
    else:                                      # legacy string headgear (old vox_*.py)
        from parts import headgear as _hg
        if kit.headgear == "cap":
            hg = _hg.side_cap(Hm, kit.team_band)
        elif kit.helmet == "helmet_v":
            hg = _hg.m1_helmet_net(Hm, kit.team_band)
        else:
            hg = _hg.m1_helmet(Hm, kit.team_band, mat=kit.helmet)
    if kit.helmet_extra:
        hg += kit.helmet_extra(Hm)
    prims += _src(hg, "headgear")

    # ---- weapon + arms
    Wm = None
    if weapon is not None and g("weapon") is not None:
        Wspec = g("weapon")
        Wm = Tm @ _frame_mat(Wspec)
        wp = _src(weapon.prims(Wm), "weapon")
        for ex in g("attach", ()):
            if ex in weapon.extras:
                wp += _src(weapon.extras[ex](Wm), "weapon")
        kind = g("overlay")
        wov = _src(weapon.overlay(Wm, kind), "weapon") if (kind and weapon.overlay) else []
        wk = g("wscale")
        if wk and wk != 1.0:                       # per-pose shortened weapon (prone aim/fire): scale along the barrel about the stock end
            wpiv = weapon_butt_z(wp, Wm)
            wp = scale_weapon(wp, Wm, wk, wpiv)
            wov = scale_weapon(wov, Wm, wk, wpiv, shape=False)
        prims += wp
        ov += wov
    if slung is not None and g("slung") is not None:
        Sm = Tm @ _frame_mat(g("slung"))
        prims += _src(slung.prims(Sm), "weapon")

    for side, nm in ((1, "L"), (-1, "R")):
        S = pt(Tm, (side * SHOULDER_X, SHOULDER_Y, 0))
        spec = g("hand" + nm, "free")
        pole_t = g("pole" + nm, (side * 0.7, -1.0, -0.2))
        if spec == "grip" and Wm is not None:
            gp_ = weapon.gripL if side > 0 else weapon.gripR
            if g("wscale") and g("wscale") != 1.0:
                gp_ = (gp_[0], gp_[1], wpiv + g("wscale") * (gp_[2] - wpiv))
            Htarget = pt(Wm, gp_)
            Hp = Htarget
            E, Hp = arm_ik(S, Hp, UPPER, LOWER, Tm[:3, :3] @ np.array(pole_t))
        elif isinstance(spec, tuple) and spec[0] == "t":
            E, Hp = arm_ik(S, pt(Tm, spec[1]), UPPER, LOWER, Tm[:3, :3] @ np.array(pole_t))
        else:   # FK swing: ("sw", fwd, bend, abduct)  or "free"
            fwd, bend, ab = (spec[1], spec[2], spec[3]) if isinstance(spec, tuple) else (0, 8, 6)
            A0 = Tm @ tr(side * SHOULDER_X, SHOULDER_Y, 0) @ rz(side * ab) @ rx(-fwd)
            E = pt(A0, (0, -UPPER, 0))
            B0 = A0 @ tr(0, -UPPER, 0) @ rx(-bend)
            Hp = pt(B0, (0, -LOWER, 0))
        patch = None                                                                         # team arm patches removed (headband is the only team-layer item)
        prims.append(seg(S, E, 4.1 * B_ARM, J, paint=patch, r1=3.5 * B_ARM))
        prims.append(ell(tr(E), 3.5 * B_ARM, 3.5 * B_ARM, 3.5 * B_ARM, J))                                         # elbow fold
        cuff = [(lambda p: p[:, 1] > LOWER / 2 - CUFF_W, PIPE)] if PIPE else None                 # red cuff band at the wrist end
        prims.append(seg(E, Hp, 3.4 * B_ARM, J, r1=2.7 * B_ARM, paint=cuff))
        prims[-3].tag, prims[-2].tag, prims[-1].tag = "upper_" + nm, "elbow_" + nm, "forearm_" + nm
        hand = ell(tr(Hp), 2.9, 2.9, 2.9, kit.hand)
        hand.tag = "hand_" + nm                 # hands may grip the weapon (collide.py)
        prims.append(hand)

    # ---- dropped weapon / blood pool (already in final ground coordinates, pelvis ground point = origin)
    late = []
    if g("drop") is not None and weapon is not None:
        late += _src(weapon.prims(_frame_mat(g("drop"))), "weapon")
    if g("blood"):
        bx, bz = g("blood")
        late.append(ell(tr(bx, 0.35, bz), 6.0, 0.7, 4.4, "blood"))

    # ---- tilted (falling / lying) poses: rotate about the pelvis, rest on the ground, pelvis ground point = origin
    tilt = g("tilt", 0)
    if tilt:
        P0 = tr(0, 26, 0)
        G = P0 @ rx(tilt) @ np.linalg.inv(P0)
        prims = [p.moved(G) for p in prims]
        ymin = min(_prim_ymin(p) for p in prims)
        T = tr(0, -ymin, 0)
        LAST["T"] = T @ G
        return [p.moved(T) for p in prims] + late, [p.moved(T @ G) for p in ov]      # muzzle flash of a lying gunner follows the tilt

    # ---- ground contact + centring
    ymin = 1e9; cx = [];
    for Mf, h in feet:
        for sx in (-1, 1):
            for sz in (-1, 1):
                ymin = min(ymin, pt(Mf, (sx * h[0], -h[1], sz * h[2]))[1])
        cx.append(pt(Mf, (0, 0, 0)))
    cx = np.mean(cx, axis=0)
    T = tr(-cx[0], -ymin, -cx[2] * 0.6)
    LAST["T"] = T
    global _FIG
    _FIG += 1
    out = [p.moved(T) for p in prims] + late
    for p in out:
        p.fig = _FIG
    return out, [p.moved(T) for p in ov]


LAST = {"T": np.eye(4)}      # LAST['T']: figure-space -> final transform applied by the last build_figure call (crew placement)


def _prim_ymin(p):
    M = p.M; c = M[:3, 3]; R = M[:3, :3]
    if p.kind in ("box", "rbox"):
        return float(c[1] - sum(abs(R[1, i]) * p.dims[i] for i in range(3)))
    if p.kind == "ell":
        return float(c[1] - math.sqrt(sum((R[1, i] * p.dims[i]) ** 2 for i in range(3))))
    if p.kind == "cone":
        hl, r = p.dims[0], max(p.dims[1], p.dims[2])
    else:
        r, hl = p.dims
    ay = abs(R[1, 1])
    ext = hl * ay + r * math.sqrt(max(0.0, 1 - ay * ay))
    return float(c[1] - ext - (r * (1 - 0) * 0 if p.kind == "cyl" else 0))


def death_pose(name, wpn_hit, wpn_fall, drop):
    """shared hit / fall / dead poses (weapon specs differ per unit)."""
    if name == "hit":
        return dict(hipL=-6, hipR=8, kneeL=16, kneeR=8, lean=-16, twist=12, roll=5, head_pitch=-16, head_yaw=-10,
                    spread=8, weapon=wpn_hit, handR="grip", handL=("sw", 75, 25, 30), push=2)
    if name == "fall":
        return dict(tilt=44, lean=12, head_pitch=10, hipL=52, hipR=34, kneeL=55, kneeR=75, spread=10,
                    weapon=wpn_fall, handR="grip", handL=("sw", 100, 20, 35))
    if name == "dead":
        return dict(tilt=90, lean=2, head_pitch=0, head_yaw=30, head_roll=-14, hipL=-8, hipR=-4, kneeL=38, kneeR=55,
                    spread=11, weapon=None, drop=drop, handR=("sw", 12, 6, 22), handL=("sw", 150, 12, 24),
                    blood=(-3.0, 15.0), push=3)
    raise KeyError(name)


def _bbox(f):
    a = np.array(f.base)[..., 3] | np.array(f.team)[..., 3]
    ys, xs = np.where(a > 0)
    if xs.size == 0:
        return 1, f.w - 2, 1, f.h - 2
    return int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())


def fit_shift_for_bbox(box, res: Res):
    """one translation (dx, dy) that auto-fit would apply to a bbox (x0, x1, y0, y1); used for shared fit of an animation group."""
    fw, fh = res.fw, res.fh
    pad, nudge = res.edge_pad, res.edge_nudge
    hpad = max(1, pad // 2)
    x0, x1, y0, y1 = box
    tdx = tdy = 0.0
    for _ in range(10):
        dy = dx = 0.0
        if y1 - y0 >= fh - 2 * pad: dy = (y0 + y1) / 2 - (fh - 1) / 2
        elif y1 > fh - 1 - pad: dy = y1 - (fh - 1 - pad) + (nudge if y1 >= fh - 1 else 0)
        elif y0 < pad: dy = y0 - pad - (nudge if y0 <= 0 else 0)
        if x1 - x0 >= fw - 2 * hpad: dx = (fw - 1) / 2 - (x0 + x1) / 2
        elif x0 < hpad: dx = hpad - x0 + (nudge if x0 <= 0 else 0)
        elif x1 > fw - 1 - hpad: dx = (fw - 1 - hpad) - x1 - (nudge if x1 >= fw - 1 else 0)
        if abs(dx) < 0.5 and abs(dy) < 0.5:
            break
        x0 += dx; x1 += dx; y0 -= dy; y1 -= dy; tdx += dx; tdy += dy      # lift += dy moves the figure up: image y shifts by -dy
    return tdx, tdy


def render_figure(pose, kit, d, weapon=None, slung=None, res: Res | None = None, fit=True, shift=None, **kw):
    """pose['lift'] is in design pixels (64x80 base) and is scaled by res.pxs; the auto-fit works in output pixels.
    fit=False skips the per-frame auto-fit; shift=(dx, dy) applies a fixed translation (shared fit of an anim group)."""
    res = res or RES
    prims, ov = build_figure(pose, kit, weapon, slung)
    vy = pose.get('view_yaw', 0.0)               # whole figure turned about the ground point (deg): LMG aim rows put the barrel on the row direction
    if vy:
        Rv = ry(vy)
        prims = [p.moved(Rv) for p in prims]
        ov = [p.moved(Rv) for p in ov]
    kw.setdefault('push', pose.get('push', 0.0))
    kw.setdefault('lift', (0.0 if (d == 'n' and pose.get('push')) else pose.get('lift', 3.0)) * res.pxs)
    return render_prims(prims, ov, d, res, fit, shift, **kw)


def _touches_border(f):
    a = np.array(f.base)[..., 3] | np.array(f.team)[..., 3]
    return bool(a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any())


def render_prims(prims, ov, d, res, fit=True, shift=None, **kw):
    """rasterise + auto-fit. If a fitted frame still touches the cell border (figure wider than the cell), it is re-rendered
    at a slightly smaller zoom (<= ~6%, 1.5% steps) so no opaque pixel sits on the border."""
    f = _render_prims(prims, ov, d, res, fit, shift, **dict(kw))
    if not fit or not _touches_border(f):
        return f
    for k in (0.985, 0.97, 0.955, 0.94):
        kw2 = dict(kw); kw2['zoom'] = res.zoom * k
        g = _render_prims(prims, ov, d, res, fit, shift, **kw2)
        f = g
        if not _touches_border(g):
            break
    return f


def _render_prims(prims, ov, d, res, fit=True, shift=None, **kw):
    kw['res'] = res
    if shift is not None:
        kw['lift'] = kw['lift'] + shift[1]
        kw['dx_px'] = kw.get('dx_px', 0) + shift[0]
    f = make_frame(prims, ov, d, **kw)
    if not fit:
        return f
    # auto-fit: if the figure touches the frame border, shift it inside (anchor follows, recorded in anchors json).
    # Always needed for lying / falling poses; also rescues wide aim/fire poses. A figure larger than the frame is centred.
    fw, fh = res.fw, res.fh
    pad, nudge = res.edge_pad, res.edge_nudge
    hpad = max(1, pad // 2)                       # horizontal margin (1 px even at 32x40)
    prev_dx = prev_dy = 0.0
    for _ in range(10):
        x0, x1, y0, y1 = _bbox(f)
        dy = 0.0; dx = 0.0
        if y1 - y0 >= fh - 2 * pad:                       # taller than the usable frame: centre it
            dy = (y0 + y1) / 2 - (fh - 1) / 2
        elif y1 > fh - 1 - pad: dy = y1 - (fh - 1 - pad) + (nudge if y1 >= fh - 1 else 0)
        elif y0 < pad: dy = y0 - pad - (nudge if y0 <= 0 else 0)
        if x1 - x0 >= fw - 2 * hpad:                      # wider than the usable frame: centre it (still clips both sides)
            dx = (fw - 1) / 2 - (x0 + x1) / 2
        elif x0 < hpad: dx = hpad - x0 + (nudge if x0 <= 0 else 0)
        elif x1 > fw - 1 - hpad: dx = (fw - 1 - hpad) - x1 - (nudge if x1 >= fw - 1 else 0)
        # both sides tight (the nudge overshoots and the shift flips sign): centre the visible bbox once and stop
        final = False
        if dx and prev_dx and (dx > 0) != (prev_dx > 0):
            dx = (fw - 1) / 2 - (x0 + x1) / 2; final = True
        if dy and prev_dy and (dy > 0) != (prev_dy > 0):
            dy = (y0 + y1) / 2 - (fh - 1) / 2; final = True
        if abs(dx) < 0.5 and abs(dy) < 0.5:
            break
        kw['lift'] = kw['lift'] + dy
        kw['dx_px'] = kw.get('dx_px', 0) + dx
        f = make_frame(prims, ov, d, **kw)
        if final:
            break
        prev_dx, prev_dy = dx, dy
    return f


def save_unit(name, frame_fn, out_dir=None):
    """save_sheet + <name>_anchors.json ({dir: [[x,y] x frames]}) with the true feet/pelvis ground point of every cell."""
    import json
    from common import save_sheet, DIRS, FRAMES, OUT
    out_dir = out_dir or os.path.join(OUT, name)
    anchors = {d: [None] * len(FRAMES) for d in DIRS}

    def wrap(d, fr):
        f = frame_fn(d, "idle" if fr == "spare" else fr)
        a = getattr(f, "anchor", ANCHOR)
        anchors[d][FRAMES.index(fr)] = [round(float(a[0]), 1), round(float(a[1]), 1)]
        return f
    save_sheet(name, wrap, out_dir)
    with open(os.path.join(out_dir, f"{name}_anchors.json"), "w") as fh:
        json.dump(anchors, fh)
    ax, ay = ANCHOR
    dev = max(max(abs(a[0] - ax), abs(a[1] - ay)) for v in anchors.values() for a in v)
    ddev = max(max(abs(a[0] - ax), abs(a[1] - ay)) for v in anchors.values() for a in v[12:15])
    print(name, "max anchor deviation", dev, "dead-frame max deviation", ddev)
