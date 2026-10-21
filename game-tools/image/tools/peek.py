"""Small crop viewer from rendered sheets: python tools/peek.py <unit_dir> <out.png> <frames,comma> [dirs,comma] [lod:@64] [scale]
Composites team layer (tinted) over base on grass; rows = dirs, cols = frames."""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
from PIL import Image
from common import composite
GRASS = (78, 110, 64, 255)


def main():
    d, outp = sys.argv[1], sys.argv[2]
    frames = sys.argv[3].split(","); dirs = (sys.argv[4] if len(sys.argv) > 4 else "s,e").split(",")
    lod = sys.argv[5] if len(sys.argv) > 5 and sys.argv[5] != "-" else ""
    scale = int(sys.argv[6]) if len(sys.argv) > 6 else 1
    name = os.path.basename(os.path.normpath(d))
    L = json.load(open(os.path.join(d, f"{name}{lod}_layout.json")))
    W, H = L["frame"]; D = L["dirs"]; cells = L["cells"] if "cells" in L else None
    sheets = {}
    def sheet(fn):
        if fn not in sheets:
            b = Image.open(os.path.join(d, fn)).convert("RGBA"); t = Image.open(os.path.join(d, fn.replace(".png", "_team.png"))).convert("RGBA")
            sheets[fn] = (b, t)
        return sheets[fn]
    out = Image.new("RGBA", (W * len(frames), H * len(dirs)), GRASS)
    for r, dr in enumerate(dirs):
        for c, f in enumerate(frames):
            ce = L["cells"][f]; b, t = sheet(ce["sheet"]); x = ce["col"] * W; y = D.index(dr) * H
            im = composite(b.crop((x, y, x + W, y + H)), t.crop((x, y, x + W, y + H)))
            bg = Image.new("RGBA", (W, H), GRASS); bg.alpha_composite(im); out.paste(bg, (c * W, r * H))
    if scale != 1:
        out = out.resize((out.width * scale, out.height * scale), Image.NEAREST)
    out.convert("RGB").save(outp)


if __name__ == "__main__":
    main()
