"""PROTOTYPE (branch face-proto, based on res96): 3 portrait expressions, pixel-drawn on the 128x128 bust. No game import.
python tools/face_proto.py <19.png>"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image, ImageDraw
import voxel
import res96_smooth as R
from voxel import Res
from unit_loader import load_unit
from common import composite
from contact import TEAM_RED

def render(u, d):
    R.run2(u, 32, keep=[0, 2, 5, 7]) if not getattr(render, 'w', 0) else None; render.w = 1
    voxel._hit_box = R._hit_box_smooth
    el = math.radians(14)
    voxel.CAM = np.array([0.0, math.sin(el), math.cos(el)]); voxel.UP = np.array([0.0, math.cos(el), -math.sin(el)])
    res = Res(128, 128); res.ss = 6
    res.zoom = voxel.BASE_ZOOM * 2 * float(os.environ.get("PZK", 2.6)); res.ax = 64; res.ay = int(128 * float(os.environ.get("PAY", 1.0)) * 2.2)
    f = u.frame(d, "idle", res=res, fit=False, lift=0.0)
    R.quantize(f); R.tint_outline(f)
    return composite(f.base, f.team, TEAM_RED)

def skin_mask(a):
    r, g, b, al = [a[..., i].astype(int) for i in range(4)]
    return (al > 0) & (r > g + 14) & (g > b + 6) & (r > 100)

def face_box(a):
    m = skin_mask(a); m[84:] = False          # head only (neck/hands below)
    ys, xs = np.nonzero(m); i = np.argmin(ys); seen = np.zeros_like(m); st = [(ys[i], xs[i])]
    while st:                                            # flood fill from the top skin pixel = face only (not fists)
        y, x = st.pop()
        if 0 <= y < 128 and 0 <= x < 128 and m[y, x] and not seen[y, x]:
            seen[y, x] = True; st += [(y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)]
    m = seen; ys, xs = np.nonzero(m)
    return m, xs.min(), xs.max(), ys.min(), ys.max()

DARK = (46, 30, 22, 255); MOUTH = (84, 40, 34, 255); MOUTH_IN = (46, 22, 20, 255); WHITE = (226, 214, 196, 255)

def draw(a, kind, d):
    a = a.copy(); m, x0, x1, y0, y1 = face_box(a)
    cx = (x0 + x1) / 2.0        # face centre shifts toward viewer-right at se
    sx = 1.0 if d == "s" else 0.8
    ey = int(y0 + (y1 - y0) * 0.24)                      # eye row
    sep = int(round((x1 - x0) * 0.22 * sx))
    mw = int(round((x1 - x0) * 0.14 * sx))
    my = int(np.nonzero(m[:, int(cx)])[0].max()) - 4       # mouth row relative to the chin
    def P(x, y, c):
        x, y = int(x), int(y)
        if 0 <= x < 128 and 0 <= y < 128 and m[y, x]: a[y, x] = c
    def top(x):                                           # first face-skin row of a column (= just under the helmet brim)
        col = np.nonzero(m[:, int(x)])[0]
        if not len(col): return ey
        t = int(col.max())
        while t - 1 >= 0 and m[t - 1, int(x)]: t -= 1     # contiguous run up from the chin
        return t
    for s in (-1, 1):
        ex = int(round(cx + s * sep)); t = top(ex); e = t + 2; inn = -s     # inn = direction toward the face centre
        if kind == "neutral":
            for dx in (0, 1): P(ex + dx, e, DARK); P(ex + dx, e + 1, DARK)           # 2x2 eye
            for k in range(-1, 2): P(ex + k, e - 1, DARK)                             # flat brow
        elif kind == "shout":
            for dx in (0, 1): P(ex + dx, e + 1, DARK)       # eyes pushed down by the brows
            for k in range(-1, 2): P(ex + k, e - 1 + (1 if k * inn > 0 else 0), DARK)  # brow low, inner end lower
        else:  # strained: inner brow ends raised, eyes squeezed to slits
            for dx in (-1, 0, 1): P(ex + dx, e + 1, DARK)
            for k in range(-1, 2): P(ex + k, e - (1 if k * inn > 0 else 0), DARK)
    # moustache (common in 1920) above mouth
    for k in range(-mw - 1, mw + 2): P(cx + k, my - 3, DARK);
    for k in range(-mw, mw + 1): P(cx + k, my - 2, DARK) if abs(k) > 0 else None
    if kind == "neutral":
        for k in range(-mw + 1, mw): P(cx + k, my, MOUTH)
    elif kind == "shout":
        for k in range(-mw, mw + 1): P(cx + k, my - 1, MOUTH)
        for k in range(-mw + 1, mw): P(cx + k, my, MOUTH_IN); P(cx + k, my + 1, MOUTH_IN)
        for k in range(-mw + 2, mw - 1): P(cx + k, my + 2, MOUTH)
    else:
        for k in range(-mw + 1, mw): P(cx + k, my, MOUTH)
        P(cx - mw, my - 1, MOUTH); P(cx + mw, my - 1, MOUTH)       # corners pulled (grimace)
        for k in range(-1, 2): P(cx + k, my + 1, MOUTH)
    return Image.fromarray(a, "RGBA")

if __name__ == "__main__":
    u = load_unit("infantry-regular")
    bases = {d: render(u, d) for d in ("s", "se")}
    for d, im in bases.items(): print(d, face_box(np.array(im))[1:])
    kinds = ("neutral", "shout", "strained"); names = {"neutral": "1 neutral", "shout": "2 shout", "strained": "3 strained"}
    cell = 128 + 256 + 16
    S = Image.new("RGBA", (cell * 3, 20 + 276 * 2), (214, 204, 184, 255)); dr = ImageDraw.Draw(S)
    for ci, k in enumerate(kinds):
        for ri, d in enumerate(("s", "se")):
            im = draw(np.array(bases[d]), k, d)
            x, y = ci * cell + 4, 20 + ri * 276
            S.alpha_composite(im, (x, y + 64)); S.alpha_composite(im.resize((256, 256), Image.NEAREST), (x + 132, y))
            dr.text((x, y - 12 if ri else 4), f"{names[k]} / {d}", fill=(0, 0, 0, 255))
            im.save(sys.argv[1].replace(".png", f"_{k}_{d}.png"))
    S.save(sys.argv[1]); print(S.size)
