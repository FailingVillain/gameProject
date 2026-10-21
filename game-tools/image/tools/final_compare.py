"""Before/after-style strip: python tools/final_compare.py <unit> <out.png> [frames,comma] [dirs,comma]
Renders the unit at 128 (x2) and 64 (x4 nearest) for s/e x idle, walk_1, fire_a (team layer tinted, optional)."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE); sys.path.insert(0, os.path.dirname(HERE))
from PIL import Image
import make_unit as M
from unit_loader import load_unit
from common import composite
GRASS = (78, 110, 64, 255)


def strip(unit, res, dirs, frames, scale, team=True):
    ims = []
    for d in dirs:
        for f in frames:
            fr = unit.frame(d, M.pick_frame(unit.columns, f), res=res)
            b = fr.base.convert("RGBA")
            im = composite(b, fr.team) if team else b
            bg = Image.new("RGBA", im.size, GRASS); bg.alpha_composite(im)
            ims.append(bg.resize((im.width * scale, im.height * scale), Image.NEAREST))
    W = sum(i.width for i in ims); H = max(i.height for i in ims)
    out = Image.new("RGBA", (W, H), GRASS); x = 0
    for i in ims:
        out.paste(i, (x, 0)); x += i.width
    return out


def main():
    name, outp = sys.argv[1], sys.argv[2]
    frames = sys.argv[3].split(",") if len(sys.argv) > 3 else ["idle", "walk_1", "fire_a"]
    dirs = sys.argv[4].split(",") if len(sys.argv) > 4 else ["s", "e"]
    u = load_unit(name)
    lods = getattr(u, "lods", None) or getattr(getattr(u, "base", None), "LODS", M.LODS)
    a = strip(u, lods[0], dirs, frames, 2); b = strip(u, lods[1], dirs, frames, 4)
    img = Image.new("RGBA", (max(a.width, b.width), a.height + b.height), GRASS)
    img.paste(a, (0, 0)); img.paste(b, (0, a.height)); img.convert("RGB").save(outp)


if __name__ == "__main__":
    main()
