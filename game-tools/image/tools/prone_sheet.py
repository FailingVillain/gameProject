"""review sheet for the prone frames: aim/fire x s,e,ne @128, E fire @64 (2x) / @32 (4x), crawl_2 E. Frame border drawn (clip check).
python tools/prone_sheet.py <out.png>"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
from common import composite
from contact import TEAM_RED, _font
ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "korean-division-image-out")
UNITS = ["infantry-default", "infantry-japanese", "infantry-regular", "infantry-guard", "infantry-reserve", "infantry-russian-red", "infantry-russian-white",
         "infantry-fengtian", "infantry-bandit", "infantry-sniper", "infantry-sniper-fengtian", "infantry-sniper-japanese", "infantry-sniper-russian-red", "infantry-sniper-russian-white"]
COLS = [("", "aim_prone", "s", 1), ("", "aim_prone", "e", 1), ("", "aim_prone", "ne", 1), ("", "fire_prone", "s", 1), ("", "fire_prone", "e", 1), ("", "fire_prone", "ne", 1),
        ("@64", "fire_prone", "e", 2), ("@32", "fire_prone", "e", 4), ("", "crawl_2", "e", 1)]
def find(name):
    for f in os.listdir(ROOT):
        p = os.path.join(ROOT, f, name)
        if os.path.isdir(p): return p
def cell(d, name, tag, frame, dr, z):
    L = json.load(open(os.path.join(d, f"{name}{tag}_layout.json"), encoding="utf-8"))
    fw, fh = L["frame"]; c = L["cells"][frame]; r = L["dirs"].index(dr)
    sh = Image.open(os.path.join(d, c["sheet"])).convert("RGBA"); tm = Image.open(os.path.join(d, c["sheet"].replace(".png", "_team.png"))).convert("RGBA")
    box = (c["col"] * fw, r * fh, (c["col"] + 1) * fw, (r + 1) * fh)
    im = composite(sh.crop(box), tm.crop(box), TEAM_RED)
    bg = Image.new("RGBA", (fw, fh), (96, 140, 92, 255)); bg.alpha_composite(im)
    bg = bg.resize((fw * z, fh * z), Image.NEAREST); ImageDraw.Draw(bg).rectangle([0, 0, bg.width - 1, bg.height - 1], outline=(255, 0, 255, 255))
    return bg
W, H, LAB = 128, 160, 150
img = Image.new("RGB", (LAB + W * len(COLS), 30 + H * len(UNITS)), (30, 30, 34)); dr = ImageDraw.Draw(img); font = _font()
for i, (tag, fr, d, z) in enumerate(COLS): dr.text((LAB + i * W + 4, 8), f"{fr}{tag} {d}", fill=(255, 255, 255), font=font)
for j, u in enumerate(UNITS):
    d = find(u); y = 30 + j * H; dr.text((4, y + H // 2), u, fill=(255, 255, 255), font=font)
    for i, (tag, fr, dd, z) in enumerate(COLS):
        try: c = cell(d, u, tag, fr, dd, z)
        except KeyError: continue
        img.paste(c.convert("RGB"), (LAB + i * W, y + (H - c.height)))
img.save(sys.argv[1]); print(img.size)
