"""Per-faction overview sheet: python tools/overview.py <out_root> <faction> <out.png>
Each unit: idle (first column) s + e cells from the main sheets, cropped to content, scaled to fit a 170 px tall tile, team layer tinted blue."""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
from PIL import Image, ImageDraw
from common import composite
GRASS = (78, 110, 64, 255)


def cell(d, name, col, row):
    L = json.load(open(os.path.join(d, f"{name}_layout.json")))
    W, H = L["frame"]; dirs = L["dirs"]
    cname = "idle" if "idle" in L["cells"] else next(iter(L["cells"]))
    ce = L["cells"][cname]
    b = Image.open(os.path.join(d, ce["sheet"])).convert("RGBA"); t = Image.open(os.path.join(d, ce["sheet"].replace(".png", "_team.png"))).convert("RGBA")
    x = ce["col"] * W; ys = dirs.index(row) * H
    im = composite(b.crop((x, ys, x + W, ys + H)), t.crop((x, ys, x + W, ys + H)))
    bb = im.getbbox()
    return im.crop(bb) if bb else im


def main():
    root, fac, out = sys.argv[1:4]
    fd = os.path.join(root, fac)
    units = sorted(u for u in os.listdir(fd) if os.path.isdir(os.path.join(fd, u)))
    TH = 170; tiles = []
    for u in units:
        try:
            ims = [cell(os.path.join(fd, u), u, 0, r) for r in ("s", "e")]
        except Exception as ex:
            print("skip", u, ex); continue
        k = min(1.0, TH / max(i.height for i in ims), 340 / sum(i.width for i in ims))
        ims = [i.resize((max(1, int(i.width * k)), max(1, int(i.height * k))), Image.NEAREST if k >= 1 else Image.LANCZOS) for i in ims]
        tiles.append((u, ims))
    cols = 3; tw = 360; th = TH + 22
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * tw, rows * th), GRASS); dr = ImageDraw.Draw(sheet)
    for n, (u, ims) in enumerate(tiles):
        ox, oy = (n % cols) * tw + 6, (n // cols) * th
        dr.text((ox, oy + 2), u, fill=(255, 255, 255, 255))
        x = ox
        for i in ims:
            sheet.alpha_composite(i, (x, oy + 20 + TH - i.height)); x += i.width + 6
    sheet.convert("RGB").save(out); print(out, len(tiles))


main()
