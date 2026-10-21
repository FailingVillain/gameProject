"""96x120 mode (KD_RES96=1 or make_unit.py --res96): LOD0/@64/@32 = 192x240 / 96x120 / 48x60 (pxs 3 / 1.5 / 0.75),
(d) material-tinted outline, (b) smoothed normals, 6 tones/material at 192 and 96, 4 tones at 48 (palette-preserving).
Ported from the res96 prototype (tools/res96_smooth.py). voxel.LODS follows ACTIVE; unit_loader.Unit.frame is wrapped by install()."""
import os
import numpy as np
from PIL import Image

ACTIVE = os.environ.get("KD_RES96") == "1"
KEEP_BIG = [0, 1, 3, 4, 6, 7]      # 192x240, 96x120
KEEP_SMALL = [0, 2, 5, 7]          # 48x60
SMOOTH = 0.9
OL = (8, 8, 8, 255)
_lut = {}


def tint_outline(f, k=0.32):
    """outline colour = neighbouring body pixel colour x k (not pure black)."""
    b = np.array(f.base)
    ring = (b[..., :4] == OL).all(-1)
    p = np.pad(b, ((1, 1), (1, 1), (0, 0))); pr = np.pad(ring, 1)
    acc = np.zeros(b.shape[:2] + (3,)); n = np.zeros(b.shape[:2])
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        q = p[1 + dy:1 + dy + b.shape[0], 1 + dx:1 + dx + b.shape[1]]
        ok = (q[..., 3] > 0) & ~pr[1 + dy:1 + dy + b.shape[0], 1 + dx:1 + dx + b.shape[1]]
        acc += np.where(ok[..., None], q[..., :3], 0); n += ok
    c = np.clip(acc / np.maximum(n, 1)[..., None] * k, 0, 255).astype(np.uint8)
    m = ring & (n > 0)
    b[m, :3] = c[m]
    f.base = Image.fromarray(b, "RGBA")


def _build_lut(keep):
    import voxel
    key = tuple(keep)
    if key in _lut:
        return _lut[key]
    mp = {}
    for mat in voxel.MATERIALS.values():
        cols = [tuple(int(v) for v in mat[s][:3]) for s in range(voxel.NS)]
        for s in range(voxel.NS):
            if s in keep:
                continue
            tgt = min(keep, key=lambda k: (abs(k - s), k))
            if cols[s] != cols[tgt] and cols[s] not in mp:
                mp[cols[s]] = cols[tgt]
    src = np.array([(r << 16) | (g << 8) | b for (r, g, b) in mp], np.int64)
    dst = np.array([mp[k] for k in mp], np.uint8)
    order = np.argsort(src)
    _lut[key] = (src[order], dst[order])
    return _lut[key]


def quantize(f, keep):
    src, dst = _build_lut(keep)
    for attr in ("base", "team"):
        a = np.array(getattr(f, attr))
        pk = (a[..., 0].astype(np.int64) << 16) | (a[..., 1].astype(np.int64) << 8) | a[..., 2]
        idx = np.clip(np.searchsorted(src, pk), 0, len(src) - 1)
        hit = (src[idx] == pk) & (a[..., 3] > 0)
        a[hit, :3] = dst[idx[hit]]
        setattr(f, attr, Image.fromarray(a, "RGBA"))


def postprocess(f, res):
    quantize(f, KEEP_SMALL if res is not None and res.fw <= 60 else KEEP_BIG)
    tint_outline(f)
    return f


def install(voxel, Unit):
    """smooth normals + frame post-process; call once (unit_loader does this when ACTIVE)."""
    hb = voxel._hit_box

    def smooth(o, d, h):
        m, t, nl = hb(o, d, h)
        pl = (o + t[:, None] * d) / np.array(h)
        r = np.sign(pl) * np.abs(pl) ** 3
        r /= np.maximum(np.linalg.norm(r, axis=1, keepdims=True), 1e-6)
        n = nl + SMOOTH * r
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-6)
        return m, t, np.where(m[:, None], n, nl)
    voxel._hit_box = smooth
    orig = Unit.frame

    def frame(self, d, name, res=None, **kw):
        res = res or voxel.RES
        if not getattr(res, "_ss_set", False):
            res.ss = 6
            res._ss_set = True
        return postprocess(orig(self, d, name, res=res, **kw), res)
    Unit.frame = frame
