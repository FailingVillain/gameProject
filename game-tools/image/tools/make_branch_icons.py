"""2D branch symbol icons (simplified NATO-style) for zoomed-out company markers, drawn UNDER the faction badge.

Branches: infantry, mg, artillery, howitzer, cavalry, scout, assault, sniper, supply, armour, at
(old UnitSymbols.cs set: Infantry X, MG X+bar, AT triangle, Artillery dot, Cavalry slash, Armor ellipse, Supply wagon).

Per branch and size (12/16/20/24/32):
  branch-<name>_<size>.png       light ink (off-white) with thin dark outline, transparent background
  branch-<name>_<size>_team.png  pure white ink (game multiplies by team colour), same dark outline
Drawn at 256 px and downscaled with LANCZOS. Also writes contact_branch.png (each icon under each faction badge).

Usage:  python make_branch_icons.py [--out DIR] [--badges DIR]
Default out: <repo>/korean-division-image-out/branch_icons (untracked).
"""
import argparse
import math
import os

from PIL import Image, ImageDraw, ImageFont

SIZES = (12, 16, 20, 24, 32)
FACTIONS = ("korean", "japanese", "fengtian", "russian", "soviet")
S = 256
OUTLINE = (24, 22, 20, 255)
INK = (240, 238, 228, 255)
TEAM = (255, 255, 255, 255)
W = 26      # stroke width on master
PAD = 14    # extra outline thickness per side

# each glyph: list of ops in master coords. ('l',x0,y0,x1,y1) line, ('c',cx,cy,r,filled) circle,
# ('p',[pts]) filled polygon, ('o',pts) closed polyline, ('e',cx,cy,rx,ry) ellipse outline
def _gl():
    a, b = 50, 206
    cx = 128
    return {
        "infantry": [("l", a, a, b, b), ("l", b, a, a, b)],
        "mg": [("l", a, a + 24, b, b), ("l", b, a + 24, a, b), ("l", cx, 30, cx, 128)],
        "artillery": [("c", cx, cx, 62, True)],
        "howitzer": [("c", cx, 160, 52, True), ("l", cx, 160, cx, 30)],
        "cavalry": [("l", 52, 204, 200, 56), ("l", 40, 150, 106, 216), ("c", 214, 42, 12, True)],
        "scout": [("o", [(46, 196), (cx, 54), (210, 196)]), ("c", cx, 176, 14, True)],
        "assault": [("l", cx, 226, cx, 90), ("p", [(cx, 22), (206, 112), (50, 112)])],
        "sniper": [("e", cx, cx, 70, 70), ("l", cx, 26, cx, 230), ("l", 26, cx, 230, cx)],
        "supply": [("l", 40, 100, 216, 100), ("e", 82, 174, 34, 34), ("e", 174, 174, 34, 34)],
        "armour": [("e", cx, cx, 90, 58), ("e", cx, cx, 58, 28)],
        "at": [("o", [(cx, 40), (214, 208), (42, 208)])],
    }


GLYPHS = _gl()


def _draw(d, ops, col, extra):
    w = W + extra
    for op in ops:
        k = op[0]
        if k == "l":
            d.line(op[1:5], fill=col, width=w)
            for x, y in ((op[1], op[2]), (op[3], op[4])):
                d.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=col)
        elif k == "c":
            r = op[3] + extra / 2
            d.ellipse((op[1] - r, op[2] - r, op[1] + r, op[2] + r), fill=col)
        elif k == "p":
            d.polygon(op[1], fill=col)
            if extra:
                d.line(op[1] + [op[1][0]], fill=col, width=extra, joint="curve")
        elif k == "o":
            pts = op[1] + [op[1][0], op[1][1]]
            d.line(pts, fill=col, width=w, joint="curve")
            for x, y in op[1]:
                d.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=col)
        elif k == "e":
            cx, cy, rx, ry = op[1:5]
            d.ellipse((cx - rx - extra / 2, cy - ry - extra / 2, cx + rx + extra / 2, cy + ry + extra / 2), fill=col)
            if not (op[0] == "e" and False):
                pass


def _ellipse_ring(d, op, col, extra):
    cx, cy, rx, ry = op[1:5]
    d.ellipse((cx - rx - W / 2 - extra / 2, cy - ry - W / 2 - extra / 2, cx + rx + W / 2 + extra / 2, cy + ry + W / 2 + extra / 2), fill=col)


def render_master(name, ink):
    ops = GLYPHS[name]
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # outline pass then ink pass; ellipses are rings so they need a clear-inside step
    for col, extra in ((OUTLINE, PAD), (ink, 0)):
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        for op in ops:
            if op[0] == "e":
                cx, cy, rx, ry = op[1:5]
                e = extra / 2
                d.ellipse((cx - rx - W / 2 - e, cy - ry - W / 2 - e, cx + rx + W / 2 + e, cy + ry + W / 2 + e), fill=col)
                hole = Image.new("L", (S, S), 255)
                ImageDraw.Draw(hole).ellipse((cx - rx + W / 2 + e, cy - ry + W / 2 + e, cx + rx - W / 2 - e, cy + ry - W / 2 - e), fill=0)
                a = layer.getchannel("A")
                layer.putalpha(Image.composite(Image.new("L", (S, S), 0), a, Image.eval(hole, lambda v: 255 - v)))
            else:
                _draw(d, [op], col, extra)
        if col == OUTLINE:
            img.alpha_composite(layer)
        else:
            img.alpha_composite(layer)
    return img


def contact(out_dir, badges_dir, rendered):
    font = ImageFont.load_default()
    names = list(GLYPHS)
    sc = 3
    cw, ch = 54 * sc // 2 + 20, 66 * sc // 2 + 10
    cw, ch = 96, 118
    W_ = 90 + cw * len(names)
    sheet = Image.new("RGBA", (W_, 30 + ch * len(FACTIONS) + 70), (92, 112, 80, 255))
    d = ImageDraw.Draw(sheet)
    for i, n in enumerate(names):
        d.text((90 + i * cw + 8, 8), n, fill=(255, 255, 255, 255), font=font)
    for r, f in enumerate(FACTIONS):
        y0 = 30 + r * ch
        d.text((8, y0 + 40), f, fill=(255, 255, 255, 255), font=font)
        bp = os.path.join(badges_dir, f"badge-{f}_32.png")
        badge = Image.open(bp).convert("RGBA") if os.path.exists(bp) else None
        for i, n in enumerate(names):
            x0 = 90 + i * cw + 16
            if badge:
                sheet.alpha_composite(badge.resize((64, 64), Image.NEAREST), (x0, y0))
            ic = rendered[(n, 16, False)].resize((48, 48), Image.NEAREST)
            sheet.alpha_composite(ic, (x0 + 8, y0 + 66))
    y = 30 + ch * len(FACTIONS) + 6
    d.text((8, y + 20), "sizes 12..32 (x1)", fill=(255, 255, 255, 255), font=font)
    for i, n in enumerate(names):
        x = 90 + i * cw + 4
        for s in SIZES:
            im = rendered[(n, s, False)]
            sheet.alpha_composite(im, (x, y + 32 - s // 2 + 0))
            x += s + 2 if s < 20 else s // 2 + 4
    path = os.path.join(out_dir, "contact_branch.png")
    sheet.convert("RGB").save(path)
    return path


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "..", "korean-division-image-out"))
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(root, "branch_icons"))
    ap.add_argument("--badges", default=os.path.join(root, "badges_icons"))
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    rendered = {}
    for n in GLYPHS:
        for team, ink in ((False, INK), (True, TEAM)):
            m = render_master(n, ink)
            for s in SIZES:
                im = m.resize((s, s), Image.LANCZOS)
                rendered[(n, s, team)] = im
                im.save(os.path.join(a.out, f"branch-{n}_{s}{'_team' if team else ''}.png"))
    print(contact(a.out, a.badges, rendered))


if __name__ == "__main__":
    main()
