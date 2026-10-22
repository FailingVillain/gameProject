"""Review contact sheet for generated houses: python house_sheet.py OUT.png name1,name2,... [--cols 3] [--root DIR] [--variants name,...]
Reads rendered sheets from <root>/buildings/<name>/ (make_building.py output). Per house: intact + ruined at @64 (native), @32 strip below.
Names listed in --variants are shown intact-only in a last row (palette variants)."""
import argparse, json, os, sys
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "tools"))
from common import composite  # noqa: E402

GRASS = (78, 110, 64, 255)
ROOT = r"C:\gameProject\game-tools\korean-division-image-out"


def frames(root, name, suf):
    d = os.path.join(root, "buildings", name)
    lay = json.load(open(os.path.join(d, f"{name}_layout.json"), encoding="utf-8"))
    info = lay["lods"][suf or "@128"]
    sheet = Image.open(os.path.join(d, info["sheet"])).convert("RGBA")
    team = Image.open(os.path.join(d, info["team"])).convert("RGBA")
    img = composite(sheet, team, (210, 40, 40))
    w, h = info["frame"]
    return lay, [img.crop((i * w, 0, (i + 1) * w, h)) for i in range(len(lay["states"]))]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("names")
    ap.add_argument("--cols", type=int, default=3); ap.add_argument("--root", default=ROOT)
    ap.add_argument("--variants", default="")
    a = ap.parse_args()
    cells = []
    for n in [x for x in a.names.split(",") if x]:
        lay, f1 = frames(a.root, n, "@64"); _, f2 = frames(a.root, n, "@32")
        cells.append((n, lay, f1, f2, True))
    for n in [x for x in a.variants.split(",") if x]:
        lay, f1 = frames(a.root, n, "@64"); _, f2 = frames(a.root, n, "@32")
        cells.append((n, lay, f1[:1], f2[:1], False))
    cw = max(sum(f.width for f in c[2]) + 8 * len(c[2]) for c in cells) + 8
    ch = max(c[2][0].height + c[3][0].height for c in cells) + 56
    rows = -(-len(cells) // a.cols)
    img = Image.new("RGBA", (cw * a.cols + 8, ch * rows + 8), GRASS)
    dr = ImageDraw.Draw(img)
    for i, (n, lay, f1, f2, _) in enumerate(cells):
        x0, y0 = 8 + (i % a.cols) * cw, 8 + (i // a.cols) * ch
        dr.text((x0, y0), f"{n}  {lay['label']}  fp {lay['footprint_tiles'][0]}x{lay['footprint_tiles'][1]}  @64 {f1[0].width}x{f1[0].height}", fill=(255, 255, 255, 255))
        x = x0
        for f, s in zip(f1, lay["states"]):
            img.alpha_composite(f, (x, y0 + 16)); dr.text((x, y0 + 16), s, fill=(255, 255, 160, 255)); x += f.width + 8
        x = x0
        for f in f2:
            img.alpha_composite(f, (x, y0 + 24 + f1[0].height)); x += f.width + 8
        dr.text((x, y0 + 24 + f1[0].height), "@32", fill=(255, 255, 160, 255))
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    img.save(a.out)
    print(a.out, img.size)


if __name__ == "__main__":
    main()
