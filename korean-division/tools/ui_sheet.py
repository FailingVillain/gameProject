"""T38g: build contact sheets from a manifest (lines 'WxH|screen|png'): python -I tools/ui_sheet.py manifest.txt outdir
One sheet per resolution (ui_sheet_<WxH>.png, tiles scaled to a common width, labelled with screen + real pixel size)
and ui_sheet_all.png (one row per resolution, smaller tiles)."""
import sys, os
from PIL import Image, ImageDraw
man, out = sys.argv[1], sys.argv[2]
rows = {}
for ln in open(man, encoding="utf-8-sig"):
    ln = ln.strip()
    if not ln: continue
    sz, name, p = ln.split("|", 2)
    rows.setdefault(sz, []).append((name, p))

def sheet(items, tw, cols, title):
    tiles = []
    for name, p in items:
        im = Image.open(p).convert("RGB"); w, h = im.size
        t = im.resize((tw, max(1, round(h * tw / w))), Image.LANCZOS)
        tiles.append((name, t, f"{w}x{h}"))
    th = max(t.height for _, t, _ in tiles) + 18
    nrow = (len(tiles) + cols - 1) // cols
    S = Image.new("RGB", (cols * (tw + 6) + 6, nrow * (th + 6) + 26), (30, 30, 34))
    d = ImageDraw.Draw(S); d.text((8, 6), title, fill=(230, 230, 230))
    for i, (name, t, real) in enumerate(tiles):
        x = 6 + (i % cols) * (tw + 6); y = 26 + (i // cols) * (th + 6)
        S.paste(t, (x, y + 16)); d.text((x, y), f"{name}  [{real}]", fill=(220, 200, 120))
    return S

for sz, items in rows.items():
    s = sheet(items, 480, 4, f"UI screens @ {sz}")
    s.save(os.path.join(out, f"ui_sheet_{sz}.png")); print("wrote", f"ui_sheet_{sz}.png", s.size)
# overview: first N screens per row would be too wide; stack the three sheets scaled to 1600 wide
ims = [Image.open(os.path.join(out, f"ui_sheet_{sz}.png")) for sz in rows]
W = 1600
ims = [i.resize((W, round(i.height * W / i.width)), Image.LANCZOS) for i in ims]
A = Image.new("RGB", (W, sum(i.height for i in ims)), (30, 30, 34)); y = 0
for i in ims: A.paste(i, (0, y)); y += i.height
A.save(os.path.join(out, "ui_sheet_all.png")); print("wrote ui_sheet_all.png", A.size)
