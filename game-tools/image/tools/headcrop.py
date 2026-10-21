"""Head close-ups: python tools/headcrop.py out.png scale unit[:dir[:frame]] ... -> crops the head area of each unit (128px frame)."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE); sys.path.insert(0, os.path.dirname(HERE))
from PIL import Image
import make_unit as M
from unit_loader import load_unit
from common import composite
GRASS = (78, 110, 64, 255)
def main():
    out, sc = sys.argv[1], int(sys.argv[2]); ims = []
    for spec in sys.argv[3:]:
        n, d, f = (spec.split(":") + ["s", "idle"])[:3] if ":" in spec else (spec, "s", "idle")
        u = load_unit(n); res = (getattr(u, "lods", None) or getattr(getattr(u, "base", None), "LODS", M.LODS))[0]
        fr = u.frame(d, M.pick_frame(u.columns, f), res=res)
        im = fr.base.convert("RGBA") if os.environ.get("NOTEAM") else composite(fr.base.convert("RGBA"), fr.team); bg = Image.new("RGBA", im.size, GRASS); bg.alpha_composite(im)
        ax, ay = fr.anchor; bx0 = int(ax - 30); by0 = int(ay - 112)
        c = bg.crop((bx0, by0, bx0 + 60, by0 + 64)); ims.append(c.resize((60 * sc, 64 * sc), Image.NEAREST))
    W = Image.new("RGBA", (sum(i.width for i in ims), ims[0].height), GRASS); x = 0
    for i in ims: W.paste(i, (x, 0)); x += i.width
    W.convert("RGB").save(out)
main()
