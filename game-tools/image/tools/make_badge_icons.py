"""2D faction badge icons for the zoomed-out map (replaces NATO company symbols).

Stylised, left-right symmetric vector drawings (not real national emblems). Motifs mirror
image/tools/parts/badges.py (plum / yellow star / five-colour roundel / white oval cockade / red star).

Per faction (map ids: korean, japanese, fengtian, russian = White, soviet = Red) and size (16/24/32/48/64):
  badge-<faction>_<size>.png       badge on transparent background, thin dark outline
  badge-<faction>_<size>_team.png  same, but the background disc is white so the game can multiply a team colour

Drawn at 256 px and downscaled with LANCZOS (supersampling). Also writes contact_badges.png.

Usage:  python make_badge_icons.py [--out DIR]
Default out: <repo>/korean-division-image-out/badges_icons (the folder is untracked; pass --out to override).
"""
import argparse
import math
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZES = (16, 24, 32, 48, 64)
FACTIONS = ("korean", "japanese", "fengtian", "russian", "soviet")
S = 256                      # master canvas
C = S / 2
R = 118                      # badge radius on master canvas
OUTLINE = (24, 22, 20, 255)  # dark outline colour
WHITE = (250, 250, 246, 255)

BRASS = (184, 137, 47, 255)
BRASS_HI = (226, 186, 96, 255)
PLUM_BASE = (46, 58, 50, 255)
YELLOW = (242, 194, 0, 255)
RED_JP = (178, 34, 34, 255)
FT_RED = (200, 40, 40, 255)
FT_YEL = (240, 200, 40, 255)
FT_BLUE = (40, 84, 170, 255)
FT_BLACK = (30, 30, 30, 255)
RU_BLUE = (40, 84, 170, 255)
RU_RED = (190, 36, 36, 255)
SOV_RED = (200, 16, 46, 255)
SOV_BASE = (58, 52, 44, 255)


def _star_pts(cx, cy, r_out, r_in):
    pts = []
    for k in range(10):
        ang = -math.pi / 2 + k * math.pi / 5
        r = r_out if k % 2 == 0 else r_in
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return pts


def _disc(img, r, col):
    d = ImageDraw.Draw(img)
    d.ellipse((C - r, C - r, C + r, C + r), fill=col)


def _plum(img, team):
    if not team:
        _disc(img, R, PLUM_BASE)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for a in (0, 72, -72, 144, -144):          # one petal up, symmetric about the vertical axis
        p = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        pd = ImageDraw.Draw(p)
        cx, cy = C, C - 0.5 * R
        pd.ellipse((cx - 0.36 * R, cy - 0.5 * R, cx + 0.36 * R, cy + 0.5 * R), fill=BRASS)
        pd.ellipse((cx - 0.18 * R, cy - 0.44 * R, cx + 0.04 * R, cy - 0.14 * R), fill=BRASS_HI)
        p = p.rotate(-a, center=(C, C), resample=Image.BICUBIC)
        layer.alpha_composite(p)
    ImageDraw.Draw(layer).ellipse((C - 0.42 * R, C - 0.42 * R, C + 0.42 * R, C + 0.42 * R), fill=BRASS_HI)
    img.alpha_composite(layer)


def _japanese(img, team):
    # no backing disc (2026-10-19): bare yellow star on transparent, like the fengtian / russian badges
    ImageDraw.Draw(img).polygon(_star_pts(C, C + 4, 0.9 * R, 0.38 * R), fill=YELLOW)


def _star_red(img, team):
    _disc(img, R, WHITE if team else SOV_BASE)
    ImageDraw.Draw(img).polygon(_star_pts(C, C + 4, 0.88 * R, 0.36 * R), fill=SOV_RED)


def _roundel(img, team):
    # five-colour roundel (outer -> inner red, yellow, blue, black); team variant turns the outer ring white
    rings = [(R, WHITE if team else FT_RED), (0.8 * R, FT_YEL), (0.6 * R, FT_BLUE), (0.4 * R, FT_BLACK)]
    d = ImageDraw.Draw(img)
    for r, col in rings:
        d.ellipse((C - r, C - r, C + r, C + r), fill=col)


def _cockade(img, team):
    # oval white / blue / red cockade, ratio 1.4 : 1.2; team variant turns the outer white ring white on white disc
    # (the disc is already white, so the team variant only differs by the white base under the motif)
    if team:
        _disc(img, R, WHITE)
    rings = [(1.0, 0.86, WHITE), (0.68, 0.57, RU_BLUE), (0.36, 0.3, RU_RED)]
    d = ImageDraw.Draw(img)
    for kx, ky, col in rings:
        rx, ry = kx * R * 0.92, ky * R * 0.92 * 1.0
        d.ellipse((C - rx, C - ry, C + rx, C + ry), fill=col)


DRAW = {
    "korean": _plum,
    "japanese": _japanese,
    "fengtian": _roundel,
    "russian": _cockade,
    "soviet": _star_red,
}


def render(faction, team, size_master=S):
    """Master-resolution badge (RGBA, transparent background) with dark outline."""
    body = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    DRAW[faction](body, team)
    # outline: dilate the alpha mask and paint it dark underneath
    mask = body.split()[3].filter(ImageFilter.MaxFilter(13))
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (S, S), OUTLINE), (0, 0), mask)
    out.alpha_composite(body)
    return out


def build(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    rendered = {}
    for f in FACTIONS:
        for team in (False, True):
            master = render(f, team)
            tag = "_team" if team else ""
            for sz in SIZES:
                icon = master.resize((sz, sz), Image.LANCZOS)
                icon.save(os.path.join(out_dir, f"badge-{f}_{sz}{tag}.png"))
            rendered[(f, team)] = master
    return rendered


def contact(out_dir, rendered):
    """Contact sheet: one row per faction, normal then team variant, native sizes shown at 2x (nearest)."""
    font = ImageFont.load_default()
    label_w = 110
    row_h = 64 * 2 + 44
    block_w = sum(sz * 2 + 8 for sz in SIZES) + 60      # one variant block: all sizes side by side
    width = label_w + 2 * block_w + 20
    height = len(FACTIONS) * row_h + 40
    sheet = Image.new("RGBA", (width, height), (120, 128, 104, 255))   # terrain-like green-grey backdrop
    d = ImageDraw.Draw(sheet)
    d.text((8, 8), "badge icons: native size x2 (normal | team tint)", fill=(255, 255, 255, 255), font=font)
    for r, f in enumerate(FACTIONS):
        y = 30 + r * row_h
        d.text((8, y + 20), f, fill=(255, 255, 255, 255), font=font)
        for block, team in enumerate((False, True)):
            x0 = label_w + block * block_w
            x = x0
            for sz in SIZES:
                im = Image.open(os.path.join(out_dir, f"badge-{f}_{sz}{'_team' if team else ''}.png")).convert("RGBA")
                im = im.resize((sz * 2, sz * 2), Image.NEAREST)
                sheet.alpha_composite(im, (x, y + 16 + (64 - sz)))
                d.text((x, y + 16 + 128 + 4), f"{sz}", fill=(255, 255, 255, 255), font=font)
                x += sz * 2 + 8
            d.text((x0, y + 0), "team" if team else "normal", fill=(230, 230, 200, 255), font=font)
    path = os.path.join(out_dir, "contact_badges.png")
    sheet.save(path)
    return path


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    default_out = os.path.join(here, "..", "..", "korean-division-image-out", "badges_icons")
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.normpath(default_out))
    a = ap.parse_args()
    rendered = build(a.out)
    print(contact(a.out, rendered))


if __name__ == "__main__":
    main()
