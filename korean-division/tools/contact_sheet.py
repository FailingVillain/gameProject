"""테마 스프라이트 검수용 접촉 시트 생성: python tools/contact_sheet.py <theme_dir> <out.png> [scale]
각 스프라이트를 (기본+팀색 합성) 으로 확대해 한 장에 배치한다."""
import sys, os, glob
from PIL import Image, ImageDraw

theme, out = sys.argv[1], sys.argv[2]
scale = int(sys.argv[3]) if len(sys.argv) > 3 else 3
TEAM = (220, 50, 50)

def tint(team):
    r, g, b, a = team.split()
    px = team.load()
    t = team.copy(); tp = t.load()
    for y in range(t.height):
        for x in range(t.width):
            R, G, B, A = px[x, y]
            tp[x, y] = (R * TEAM[0] // 255, G * TEAM[1] // 255, B * TEAM[2] // 255, A)
    return t

items = []
for p in sorted(glob.glob(os.path.join(theme, "*", "*.png"))):
    if p.endswith("_team.png") or os.sep + "terrain" + os.sep in p:
        continue
    base = Image.open(p).convert("RGBA")
    tp = p[:-4] + "_team.png"
    if os.path.exists(tp):
        base.alpha_composite(tint(Image.open(tp).convert("RGBA")))
    items.append((os.path.basename(p), base))
terr = sorted(glob.glob(os.path.join(theme, "terrain", "*.png")))
W = 1500
x = y = 4; rowh = 0
sheet = Image.new("RGBA", (W, 1400), (60, 90, 60, 255))
d = ImageDraw.Draw(sheet)
for name, im in items:
    im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    if x + im.width > W: x = 4; y += rowh + 16; rowh = 0
    sheet.alpha_composite(im, (x, y)); d.text((x, y + im.height), name, fill="white")
    x += im.width + 10; rowh = max(rowh, im.height)
x = 4; y += rowh + 24; rowh = 0
for p in terr:
    im = Image.open(p).convert("RGBA"); im = im.resize((im.width * 2, im.height * 2), Image.NEAREST)
    if x + im.width > W: x = 4; y += rowh + 4; rowh = 0
    sheet.alpha_composite(im, (x, y)); x += im.width + 2; rowh = max(rowh, im.height)
sheet.crop((0, 0, W, y + rowh + 8)).save(out)
