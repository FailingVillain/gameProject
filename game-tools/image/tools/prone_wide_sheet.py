"""review sheet of the wide prone cells read back from a unit folder (what the game loads): aim / fire x 8 dirs (@128 1x) + @64 / @32 fire row
(upscaled), then E and W fire large. Cell borders drawn (clip check).  python tools/prone_wide_sheet.py <unit> <unit_dir> <out.png>"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
from common import composite
from contact import TEAM_RED, _font
unit, d, outp = sys.argv[1:4]
D8 = [("s", 0), ("sw", 1), ("w", 2), ("nw", 3), ("n", 4), ("ne", 3), ("e", 2), ("se", 1)]
ORDER = ["s", "se", "e", "ne", "n", "nw", "w", "sw"]
def cell(tag, frame, dr, z):
    L = json.load(open(os.path.join(d, f"{unit}{tag}_layout.json"), encoding="utf-8"))
    c = L["cells"][frame]; sh = next(s for s in L["sheets"] if s["file"] == c["sheet"]); fw, fh = sh.get("frame", L["frame"])
    flip = dr in ("sw", "w", "nw"); base = {"sw": "se", "w": "e", "nw": "ne"}.get(dr, dr); r = L["dirs"].index(base)
    im = Image.open(os.path.join(d, c["sheet"])).convert("RGBA"); tm = Image.open(os.path.join(d, c["sheet"].replace(".png", "_team.png"))).convert("RGBA")
    box = (c["col"] * fw, r * fh, (c["col"] + 1) * fw, (r + 1) * fh)
    f = composite(im.crop(box), tm.crop(box), TEAM_RED)
    bg = Image.new("RGBA", (fw, fh), (96, 140, 92, 255)); bg.alpha_composite(f)
    if flip: bg = bg.transpose(Image.FLIP_LEFT_RIGHT)
    bg = bg.resize((fw * z, fh * z), Image.NEAREST); dd = ImageDraw.Draw(bg)
    dd.rectangle([0, 0, bg.width - 1, bg.height - 1], outline=(255, 0, 255, 255))
    ax, ay = L["anchor"] if "anchor" not in sh else sh["anchor"]
    dd.line([(ax * z - 6, ay * z), (ax * z + 6, ay * z)], fill=(255, 255, 0, 255)); dd.line([(ax * z, ay * z - 6), (ax * z, ay * z + 6)], fill=(255, 255, 0, 255))
    return bg
font = _font(); CW, CH, LAB = 160, 200, 110
rows = [("aim @128", "", "aim_prone", 1), ("fire @128", "", "fire_prone", 1), ("go @128", "", "go_prone", 1), ("fire @64 x2", "@64", "fire_prone", 2), ("fire @32 x4", "@32", "fire_prone", 4)]
big = 3
H = 26 + CH * 3 + 200 + 200 + 26 + CH * big + 10
img = Image.new("RGB", (max(LAB + CW * 8, 40 + 2 * CW * big + 40), H), (30, 30, 34)); dr = ImageDraw.Draw(img)
for i, n in enumerate(ORDER): dr.text((LAB + i * CW + 6, 6), n, fill=(255, 255, 255), font=font)
y = 26
for lab, tag, fr, z in rows:
    dr.text((4, y + 8), lab, fill=(255, 255, 255), font=font)
    for i, n in enumerate(ORDER):
        c = cell(tag, fr, n, z); img.paste(c.convert("RGB"), (LAB + i * CW, y))
    y += max(CH, 0) if z == 1 else 200
dr.text((4, y + 4), "E fire (left) and W fire (right) at 3x, 160x200 cell", fill=(255, 255, 255), font=font); y += 26
for k, n in enumerate(["e", "w"]):
    img.paste(cell("", "fire_prone", n, big).convert("RGB"), (40 + k * (CW * big + 40), y))
img.save(outp); print(img.size)
