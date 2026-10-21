"""Labeled contact sheet for user review: every rendered cell, team layer tinted red, scaled up, on mid grey,
label '<DIR>-<frame>' under each cell. rows = directions; the columns are split into bands so the image stays
under MAX_W px wide."""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import DIRS, composite  # noqa: E402

BG = (128, 128, 128, 255)
TEAM_RED = (210, 40, 40)
SCALE = 2
GUTTER = 6
LABEL_H = 20
MAX_W = 3900
MAX_H = 8000             # a sheet taller than this is split into parts (name_1.png, name_2.png ...), whole bands per part
MAX_CELL_W = 700         # scale cap: cell width at scale 2 must stay <= this (big artillery frames are scaled down)


def _font():
    try:
        return ImageFont.load_default(size=15)
    except TypeError:                 # old Pillow
        return ImageFont.load_default()


def parse_color(s) -> tuple:
    """'#3060c0' / '3060c0' / '48,96,192' -> (48, 96, 192)."""
    if s is None or isinstance(s, tuple):
        return s
    s = s.strip()
    if "," in s:
        return tuple(int(v) for v in s.split(","))[:3]
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def contact_sheet_parts(grid: dict, res, dirs=DIRS, columns=(), scale=SCALE, team_color=None, max_h=MAX_H) -> list:
    """Like contact_sheet but returns a list of images; bands are grouped so that no image is taller than max_h
    (a single band taller than max_h gets its own image)."""
    fw, fh = res.size
    # big frames (artillery 976x656) at 2x made a 107k px tall sheet that viewers cannot open (>65535 px limit);
    # shrink the cell scale so one band stays within MAX_CELL_W
    scale = max(1, min(scale, MAX_CELL_W // fw))
    team = parse_color(team_color) or TEAM_RED
    cw, ch = fw * scale + GUTTER, fh * scale + LABEL_H + GUTTER
    per_band = max(1, min(len(columns), (MAX_W - GUTTER) // cw))
    bands = [columns[i:i + per_band] for i in range(0, len(columns), per_band)]
    band_h = len(dirs) * ch + GUTTER
    per_img = max(1, (max_h - GUTTER) // band_h)
    groups = [bands[i:i + per_img] for i in range(0, len(bands), per_img)] or [[]]
    W = GUTTER + per_band * cw
    font = _font()
    out = []
    for group in groups:
        img = Image.new("RGBA", (W, GUTTER + band_h * len(group)), BG)
        draw = ImageDraw.Draw(img)
        y = GUTTER
        for band in group:
            for d in dirs:
                for c, fname in enumerate(band):
                    x = GUTTER + c * cw
                    draw.rectangle((x - 1, y - 1, x + fw * scale, y + fh * scale), outline=(96, 96, 96, 255))
                    f = grid.get((d, fname))
                    if f is not None:
                        cell = composite(f.base, f.team, team).resize((fw * scale, fh * scale), Image.NEAREST)
                        img.alpha_composite(cell, (x, y))
                    draw.text((x + 2, y + fh * scale + 2), f"{d.upper()}-{fname}", fill=(255, 255, 255, 255), font=font)
                y += ch
            y += GUTTER
        out.append(img)
    return out


def contact_sheet(grid: dict, res, dirs=DIRS, columns=(), scale=SCALE, team_color=None) -> Image.Image:
    """grid[(dir, frame)] = common.Frame rendered at `res` (voxel.Res); columns = frame order. One image (never split).
    team_color: '#rrggbb' (or (r,g,b)) to tint the team mask with; default = red placeholder (TEAM_RED)."""
    return contact_sheet_parts(grid, res, dirs, columns, scale, team_color, max_h=10 ** 9)[0]


def save_contact(path, grid, res, dirs=DIRS, columns=(), scale=SCALE, team_color=None, max_h=MAX_H) -> list:
    """write the contact sheet; > max_h px tall -> <stem>_1.png, <stem>_2.png ... (a stale unsplit file is removed). Returns the paths."""
    parts = contact_sheet_parts(grid, res, dirs, columns, scale, team_color, max_h)
    stem, ext = os.path.splitext(path)
    for old in [path] + [f"{stem}_{i}{ext}" for i in range(1, 40)]:
        if os.path.exists(old):
            os.remove(old)
    paths = [path] if len(parts) == 1 else [f"{stem}_{i + 1}{ext}" for i in range(len(parts))]
    for im, pth in zip(parts, paths):
        im.save(pth)
    return paths
