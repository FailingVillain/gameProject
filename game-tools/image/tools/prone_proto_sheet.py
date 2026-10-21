"""python tools/prone_proto_sheet.py <unit> <out.png> : rows cur 64x80 | p07 | new (80x100 @64, x2), per frame; last row = new @128 (160x200 x1)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import prone_proto as P
from contact import _font
unit, outp = sys.argv[1], sys.argv[2]
FR = ["fire_prone", "aim_prone", "go_prone"]; DIRS = ["s", "se", "e", "ne", "n", "w"]
CW, CH, LAB = 160, 200, 170
rows = [(v, fr, 64) for fr in FR for v in ("cur", "p07", "new")] + [("new", "fire_prone", 128)]
img = Image.new("RGB", (LAB + CW * len(DIRS), 28 + CH * len(rows)), (30, 30, 34)); dr = ImageDraw.Draw(img); font = _font()
for i, d in enumerate(DIRS): dr.text((LAB + i * CW + 6, 8), d + (" (mirror of e)" if d == "w" else ""), fill=(255, 255, 255), font=font)
NAMES = {"cur": "current 64x80", "p07": "proto 07 80x100", "new": "redesign 80x100"}
cache = {}
for j, (v, fr, lod) in enumerate(rows):
    y = 28 + j * CH
    dr.text((4, y + 6), f"{NAMES[v]}{' @128 (160x200)' if lod == 128 else ''}", fill=(255, 255, 255), font=font); dr.text((4, y + 26), fr, fill=(200, 200, 200), font=font)
    key = (v, lod)
    if key not in cache: cache[key] = P.render(unit, v, lod, FR)[0]
    out = cache[key]; z = 1 if lod == 128 else 2
    for i, d in enumerate(DIRS):
        im = out[(fr, "e" if d == "w" else d)]
        if d == "w": im = im.transpose(Image.FLIP_LEFT_RIGHT)
        bg = Image.new("RGBA", im.size, (96, 140, 92, 255)); bg.alpha_composite(im)
        bg = bg.resize((bg.width * z, bg.height * z), Image.NEAREST)
        c = Image.new("RGBA", (CW, CH), (96, 140, 92, 255)); c.alpha_composite(bg, ((CW - bg.width) // 2, CH - bg.height if v == "cur" else 0) if v != "cur" else ((CW - bg.width) // 2, 0))
        ImageDraw.Draw(c).rectangle([(CW - bg.width) // 2, 0, (CW - bg.width) // 2 + bg.width - 1, bg.height - 1], outline=(255, 0, 255, 255))
        img.paste(c.convert("RGB"), (LAB + i * CW, y))
img.save(outp); print(img.size)
