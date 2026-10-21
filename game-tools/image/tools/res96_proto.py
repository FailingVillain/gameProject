"""PROTOTYPE (branch res96): infantry-regular at 48x60/96x120/192x240 (pxs 0.75/1.5/3), smoothing options. No game art touched.
python tools/res96_proto.py <out.png>"""
import os, sys, time
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

u = load_unit("infantry-regular")
rows = []; tm = {}
cur, tm["cur64"] = run(u, 64)
rows.append([im.resize((96, 120), Image.NEAREST) for im in cur])
a, tm["new96"] = run(u, 96); rows.append(a)
b, tm["smooth96"] = run(u, 96, ss=6, tint=True); rows.append(b)
c, tm["new48"] = run(u, 48); rows.append([im.resize((96, 120), Image.NEAREST) if False else im for im in c])
d_, tm["new192"] = run(u, 192)
W, H = 96, 120
sheet = Image.new("RGBA", (W * 20, H * 4), (78, 110, 64, 255))
for r, row in enumerate(rows):
    for i, im in enumerate(row):
        sheet.alpha_composite(im, (i * W + (W - im.width) // 2, r * H + (H - im.height) // 2))
sheet.save(sys.argv[1]); d_[0].save(sys.argv[1].replace(".png", "_192_idle_s.png"))
print({k: round(v, 2) for k, v in tm.items()}, sheet.size)
