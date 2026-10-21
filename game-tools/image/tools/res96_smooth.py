"""PROTOTYPE (branch res96): (d) tinted outline + (b) smoothed normals + 6 tones/material; sheet 15 + portrait 16. No game art touched.
python tools/res96_smooth.py <15.png> <16.png>"""
import os, sys, time, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image
from voxel import Res
from unit_loader import load_unit
from common import composite
from contact import TEAM_RED

FRAMES = ["idle", "fire_a", "walk_1", "fire_prone"]
DIRS = ["s", "se", "e", "ne", "n"]
OL = (8, 8, 8, 255)

def tint_outline(f, k=0.32):
    """option (d): outline colour = material colour of the neighbouring body pixel x k (not pure black)."""
    b = np.array(f.base); ring = (b[..., :4] == OL).all(-1)
    p = np.pad(b, ((1, 1), (1, 1), (0, 0))); pr = np.pad(ring, 1)
    acc = np.zeros(b.shape[:2] + (3,)); n = np.zeros(b.shape[:2])
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        q = p[1 + dy:1 + dy + b.shape[0], 1 + dx:1 + dx + b.shape[1]]
        ok = (q[..., 3] > 0) & ~pr[1 + dy:1 + dy + b.shape[0], 1 + dx:1 + dx + b.shape[1]]
        acc += np.where(ok[..., None], q[..., :3], 0); n += ok
    c = (acc / np.maximum(n, 1)[:, None] ) if False else acc / np.maximum(n, 1)[..., None]
    c = np.clip(c * k, 0, 255).astype(np.uint8)
    m = ring & (n > 0)
    b[m, :3] = c[m]
    from PIL import Image as I
    f.base = I.fromarray(b, "RGBA")

def run(u, w, ss=None, tint=False):
    res = Res(w, w * 5 // 4)
    if ss: res.ss = ss
    out = []; t = time.time()
    for fr in FRAMES:
        for d in DIRS:
            f = u.frame(d, fr, res=res)
            if tint: tint_outline(f)
            out.append(composite(f.base, f.team, TEAM_RED))
    return out, (time.time() - t) / len(out)


import voxel
KEEP = [0, 1, 3, 4, 6, 7]          # 6 of the 8 palette steps per material (palette-preserving: only existing colours are used)
SMOOTH = 0.9

_hb = voxel._hit_box
def _hit_box_smooth(o, d, h):
    m, t, nl = _hb(o, d, h)
    pl = (o + t[:, None] * d) / np.array(h)
    r = np.sign(pl) * np.abs(pl) ** 3                 # superellipse-ish radial direction, flat faces still dominate in the middle
    r /= np.maximum(np.linalg.norm(r, axis=1, keepdims=True), 1e-6)
    n = nl + SMOOTH * r
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-6)
    return m, t, np.where(m[:, None], n, nl)

def quantize(f, KEEP=KEEP):
    """snap every body/team pixel to the nearest of the KEEP steps of its own material (no colour bleed: exact palette hits only)."""
    for attr in ("base", "team"):
        a = np.array(getattr(f, attr))
        for name, mat in voxel.MATERIALS.items():
            cols = [tuple(mat[s][:3]) for s in range(voxel.NS)]
            for s in range(voxel.NS):
                if s in KEEP: continue
                tgt = min(KEEP, key=lambda k: (abs(k - s), k))
                m = (a[..., :3] == np.array(cols[s], np.uint8)).all(-1) & (a[..., 3] > 0)
                if cols[s] != cols[tgt]: a[m, :3] = cols[tgt]
        setattr(f, attr, Image.fromarray(a, "RGBA"))

def run2(u, w, smooth=True, keep=KEEP):
    res = Res(w, w * 5 // 4); res.ss = 6
    voxel._hit_box = _hit_box_smooth if smooth else _hb
    out = []; t = time.time()
    for fr in FRAMES:
        for d in DIRS:
            f = u.frame(d, fr, res=res)
            if smooth: quantize(f, keep)
            tint_outline(f)
            out.append(composite(f.base, f.team, TEAM_RED))
    return out, (time.time() - t) / len(out)

if __name__ == "__main__":
    u = load_unit("infantry-regular"); W, H = 96, 120
    a, ta = run2(u, 96, smooth=False); b, tb = run2(u, 96); c, tc = run2(u, 32, keep=[0, 2, 5, 7])
    sheet = Image.new("RGBA", (W * 20, H * 3), (78, 110, 64, 255))
    for r, row in enumerate((a, b, c)):
        for i, im in enumerate(row):
            sheet.alpha_composite(im, (i * W + (W - im.width) // 2, r * H + (H - im.height) // 2))
    from PIL import ImageDraw; dr = ImageDraw.Draw(sheet)
    for r, t_ in enumerate(('96x120 (d) only, 8 tone', '96x120 (d)+(b)+6 tone', '48x60 @32 (d)+(b)+4 tone')): dr.text((3, r * H + 2), t_, fill=(255, 255, 255, 255))
    sheet.save(sys.argv[1]); print("frame s:", round(ta, 2), round(tb, 2), round(tc, 2), sheet.size)
    # portrait 128x128: close-up bust
    pk = dict(zk=float(os.environ.get("PZK", 2.6)), ay=float(os.environ.get("PAY", 0.62)))
    ims = []
    voxel._hit_box = _hit_box_smooth
    el = math.radians(float(os.environ.get('PEL', 14)))   # portrait camera lower than the game view (36 deg)
    voxel.CAM = np.array([0.0, math.sin(el), math.cos(el)]); voxel.UP = np.array([0.0, math.cos(el), -math.sin(el)])
    for d in ("s", "se"):
        res = Res(128, 128); res.ss = 6
        res.zoom = voxel.BASE_ZOOM * 2 * pk["zk"]; res.ax = 64; res.ay = int(128 * pk["ay"] * 2.2)
        t = time.time()
        f = u.frame(d, "idle", res=res, fit=False, lift=0.0)
        quantize(f); tint_outline(f)
        im = composite(f.base, f.team, TEAM_RED)
        ims.append((im, f.team)); print("portrait", d, round(time.time() - t, 2))
    S = Image.new("RGBA", (128 * 2 + 256 * 2, 256), (214, 204, 184, 255))
    for i, (im, tm) in enumerate(ims):
        S.alpha_composite(im, (i * 128, 64)); S.alpha_composite(im.resize((256, 256), Image.NEAREST), (256 + i * 256, 0))
    S.save(sys.argv[2])
    for d, (im, tm) in zip(("s", "se"), ims):
        im.save(sys.argv[2].replace(".png", f"_{d}.png")); tm.save(sys.argv[2].replace(".png", f"_{d}_teammask.png"))
