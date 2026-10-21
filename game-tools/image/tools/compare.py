"""Build out/compare.png: per unit, 5 idle directions at 1x and 0.5x on grass, upscaled 3x (nearest).
Run after the vox_* generators:  python tools/compare.py [extra_frame_index]"""
import os, sys
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import FW, FH, OUT, composite, DIRS

GRASS = (78, 110, 64, 255)
UNITS = ["recruit", "infantry", "veteran", "scout", "worker", "at_infantry"]


def cell(sheet, team, r, c):
    b = sheet.crop((c * FW, r * FH, (c + 1) * FW, (r + 1) * FH))
    t = team.crop((c * FW, r * FH, (c + 1) * FW, (r + 1) * FH))
    bg = Image.new("RGBA", (FW, FH), GRASS)
    bg.alpha_composite(composite(b, t))
    return bg


def main(col=0, name="compare.png"):
    units = [u for u in UNITS if os.path.isfile(os.path.join(OUT, u, f"{u}.png"))]
    W = (5 * FW + 5 * FW // 2 + 8) * 3
    img = Image.new("RGBA", (W, len(units) * FH * 3), GRASS)
    for i, u in enumerate(units):
        sheet = Image.open(os.path.join(OUT, u, f"{u}.png")); team = Image.open(os.path.join(OUT, u, f"{u}_team.png"))
        row = Image.new("RGBA", (5 * FW + 5 * FW // 2 + 8, FH), GRASS)
        for r in range(5):
            c = cell(sheet, team, r, col)
            row.paste(c, (r * FW, 0))
            row.paste(c.resize((FW // 2, FH // 2), Image.BOX), (5 * FW + 8 + r * FW // 2, 0))
        img.paste(row.resize((row.width * 3, row.height * 3), Image.NEAREST), (0, i * FH * 3))
    img.save(os.path.join(OUT, name))


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 0, sys.argv[2] if len(sys.argv) > 2 else "compare.png")
