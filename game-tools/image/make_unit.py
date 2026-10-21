"""Render one data-driven unit (units/<name>.json) with the voxel pipeline.

    python make_unit.py <name> [--preview] [--out DIR]

Writes into <out>/<name>/ (default <game-tools>/korean-division-image-out/<name>/), for each LOD
(128x160 main -> no suffix, 64x80 -> '@64', 32x40 -> '@32'; each rendered natively from the model):
  <name>[@N].png / _team.png      sheets: rows = directions (s, se, e, ne, n), columns = pose frames (anims json order).
                                  A sheet is never wider than MAX_SHEET_W; more columns -> extra files <name>[@N]_1.png, _2.png ...
  <name>[@N]_layout.json          index: frame size, anchor, dirs, columns, anims, sheets[] and cells{frame: {sheet, col}} (row = dir index)
  <name>[@N]_anchors.json         {dir: [[x, y] per column]} true ground point of every cell
  contact_<name>.png              labeled review sheet of the 128x160 cells (team layer in red, 2x, grey background)
  contact_<name>_lod.png          idle S + walk_1 E at all three sizes side by side (each 2x)
  gif/<name>_<anim>_<dir>.gif     every multi-frame animation, 128x160
--preview: only s and e x idle, walk_1, fire_a at 128x160 (no GIFs / LODs).
"""
import argparse
import json
import os
import sys
import time

if "--res96" in sys.argv:                       # 192x240 / 96x120 / 48x60 + tinted outline + smooth + 6 tones (tools/res96.py); must be set before voxel imports
    os.environ["KD_RES96"] = "1"

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "tools"))
from common import DIRS, PREVIEW_SCALE, composite  # noqa: E402
import prone_wide  # noqa: E402
from contact import contact_sheet, save_contact, _font, BG, TEAM_RED  # noqa: E402
from unit_loader import load_unit, OUTLINE_STRONG  # noqa: E402
from outroute import route_out  # noqa: E402
import collide  # noqa: E402
from voxel import RES, LODS, Res, _bbox, fit_shift_for_bbox  # noqa: E402

GAME_TOOLS = os.path.dirname(HERE)                                    # game-tools/image -> game-tools
DEFAULT_OUT = os.path.join(GAME_TOOLS, "korean-division-image-out")
MAX_SHEET_W = 4096                                                    # hard limit for one sheet image
PREVIEW_DIRS = ["s", "e"]
PREVIEW_FRAMES = ["idle", "walk_1", "fire_a"]
GRASS = (78, 110, 64, 255)
LOD_CELLS = [("s", "idle"), ("e", "walk_1")]


def pick_frame(cols, name):
    """column named `name`, else the first column ending in '_<name>' (prefixed pose sets, e.g. mg_idle)."""
    if name in cols:
        return name
    return next((c for c in cols if c.endswith("_" + name)), name)


def save_img(img, path, **kw):
    """save; if the file is locked (OneDrive sync / viewer open) fall back to <name>.new.<ext> and warn."""
    try:
        img.save(path, **kw)
    except OSError as e:
        base, ext = os.path.splitext(path)
        alt = f"{base}.new{ext}"
        img.save(alt, **kw)
        print(f"  [warn] could not write {os.path.basename(path)} ({e.strerror}); wrote {os.path.basename(alt)} instead")


def save_gif(path, frames, ms, res=RES, scale=PREVIEW_SCALE):
    imgs = []
    for f in frames:
        bg = Image.new("RGBA", res.size, GRASS)
        bg.alpha_composite(composite(f.base, f.team, TEAM_RED))
        imgs.append(bg.resize((res.fw * scale, res.fh * scale), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
    imgs[0].save(path, save_all=True, append_images=imgs[1:], duration=ms, loop=0, disposal=2)


def _union_bbox(unit, d, frames, res):
    """union bbox (x0, x1, y0, y1) of the raw (un-fitted) frames, measured without frame clipping:
    y on a 3x taller canvas (same scale), x from renders shifted +-off px; each side is taken only from renders
    where that side is not clipped by the frame border."""
    tall = res.tall(3) if hasattr(res, "tall") else Res(res.fw, res.fh * 3)
    dy_t = tall.ay - res.ay
    off = max(1, int(round(20 * res.pxs)))
    left, right, top, bot = [], [], [], []
    for f in frames:
        for sx in (-off, 0, off):
            x0, x1, y0, y1 = _bbox(unit.frame(d, f, res=tall, fit=False, shift=(sx, 0)))   # clip test in render coords
            if x0 > 0: left.append(x0 - sx)
            if x1 < res.fw - 1: right.append(x1 - sx)
            top.append(y0 - dy_t); bot.append(y1 - dy_t)
    x0 = min(left) if left else 0
    x1 = max(right) if right else res.fw - 1
    return (x0, x1, min(top), max(bot))


def render_grid(unit, res, dirs, frames, log=print):
    """-> grid{(dir, frame): Frame} for one resolution profile.
    Anims with "shared_fit": true get ONE translation per direction (from the union bbox of their frames)
    instead of per-frame auto-fit, so the body does not slide between frames."""
    grid = {}
    t0 = time.time()
    groups = [[f for f in spec["frames"] if f in frames] for spec in unit.anims.values() if spec.get("shared_fit")]
    for d in dirs:
        shifts = {}
        for g in groups:
            if len(g) < 2:
                continue
            union = _union_bbox(unit, d, g, res)
            S = fit_shift_for_bbox(union, res)
            shifts.update({f: S for f in g})
        for fname in frames:
            if fname in shifts:
                grid[(d, fname)] = unit.frame(d, fname, res=res, fit=False, shift=shifts[fname])
            else:
                grid[(d, fname)] = unit.frame(d, fname, res=res)
        log(f"  {res.tag} {d}: {len(frames)} frames ({time.time() - t0:.1f}s)")
    return grid


def write_sheets(unit, res, grid, out, tag):
    """paste the grid into one or more sheet files (<= MAX_SHEET_W wide) + layout json + anchors json."""
    fw, fh = res.size
    cols = unit.columns
    rows = list(getattr(unit, "dirs", DIRS))       # sheet rows = the unit's directions (5, or 9 for "dirs": "16")
    per_sheet = max(1, MAX_SHEET_W // fw)
    chunks = [cols[i:i + per_sheet] for i in range(0, len(cols), per_sheet)]
    sheets, cells = [], {}
    for k, chunk in enumerate(chunks):
        fname = f"{unit.name}{tag}.png" if len(chunks) == 1 else f"{unit.name}{tag}_{k + 1}.png"
        sheet = Image.new("RGBA", (fw * len(chunk), fh * len(rows)), (0, 0, 0, 0))
        team = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
        for r, d in enumerate(rows):
            for c, col in enumerate(chunk):
                f = grid.get((d, col))
                if f is not None:
                    sheet.paste(f.base, (c * fw, r * fh))
                    team.paste(f.team, (c * fw, r * fh))
        save_img(sheet, os.path.join(out, fname))
        save_img(team, os.path.join(out, fname.replace(".png", "_team.png")))
        sheets.append({"file": fname, "team": fname.replace(".png", "_team.png"), "columns": chunk, "size": list(sheet.size)})
        for c, col in enumerate(chunk):
            cells[col] = {"sheet": fname, "col": c}
    anchors = {d: [None] * len(cols) for d in rows}
    for (d, col), f in grid.items():
        ax, ay = getattr(f, "anchor", res.anchor)
        anchors[d][cols.index(col)] = [round(float(ax), 1), round(float(ay), 1)]
    layout = {"frame": [fw, fh], "anchor": list(res.anchor), "dirs": rows, "mirror": True, "columns": cols,
              "anims": unit.anims, "sheets": sheets, "cells": cells, "anchors": f"{unit.name}{tag}_anchors.json"}
    if os.environ.get("KD_RES96") == "1":
        layout["res96"] = True                                   # tools/import_kd1920.py: theme scale = 32 / main frame width (keeps on-screen size)
    if hasattr(unit, "layout_extra"):
        layout.update(unit.layout_extra(res))
    with open(os.path.join(out, f"{unit.name}{tag}_layout.json"), "w", encoding="utf-8") as fh:
        json.dump(layout, fh, indent=1)
    with open(os.path.join(out, f"{unit.name}{tag}_anchors.json"), "w") as fh:
        json.dump(anchors, fh)
    return sheets, anchors


def lod_strip(grids, cols=(), cells_spec=None):
    """grids: list of (res, grid). idle S + walk_1 E at every size, each 2x, labeled."""
    scale, gut, lab = 2, 8, 18
    font = _font()
    cells = []
    for res, grid in grids:
        for d, fname in (cells_spec or LOD_CELLS):
            f = grid.get((d, pick_frame(cols, fname)))
            if f is None:
                continue
            bg = Image.new("RGBA", res.size, GRASS)
            bg.alpha_composite(composite(f.base, f.team, TEAM_RED))
            cells.append((bg.resize((res.fw * scale, res.fh * scale), Image.NEAREST), f"{res.fw}x{res.fh} {d.upper()}-{fname}"))
    min_w = 150                                                              # label width so small cells do not overlap
    W = gut + sum(max(c.width, min_w) + gut for c, _ in cells)
    hmax = max(c.height for c, _ in cells)
    img = Image.new("RGBA", (W, gut + hmax + lab + gut), BG)
    draw = ImageDraw.Draw(img)
    x = gut
    for c, label in cells:
        img.alpha_composite(c, (x, gut + hmax - c.height))                   # bottom-aligned
        draw.text((x, gut + hmax + 2), label, fill=(255, 255, 255, 255), font=font)
        x += max(c.width, min_w) + gut
    return img


def check(grid, anchors, res):
    """numeric sanity: max anchor deviation, cells with opaque pixels on the border, distinct colours, opaque px."""
    touching = []
    rgb_all = []
    npx = 0
    for (d, fname), f in grid.items():
        b = np.array(f.base); t = np.array(f.team)
        a = b[..., 3] | t[..., 3]
        if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any():
            touching.append(f"{d}/{fname}")
        rgb_all.append(b[..., :3][b[..., 3] > 0])
        npx += int((a > 0).sum())
    vals = [v for row in anchors.values() for v in row if v]
    dev = max(max(abs(x - res.ax), abs(y - res.ay)) for x, y in vals)
    ncol = len(np.unique(np.concatenate(rgb_all), axis=0))
    return dev, touching, ncol, npx


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("--preview", action="store_true", help="only s/e x idle, walk_1, fire_a at 128x160")
    ap.add_argument("--out", default=DEFAULT_OUT, help="output root (faction subfolder + unit folder are created inside)")
    ap.add_argument("--no-collide", action="store_true", help="skip the 3D weapon/body interpenetration check")
    ap.add_argument("--outline-strong", action="store_true", help="experiment: near-black, thicker outer outline (OUTLINE_STRONG)")
    ap.add_argument("--res96", action="store_true", help="96x120 mode: LOD0 192x240 / 96x120 / 48x60, (d) tinted outline, smooth normals, 6/4 tones")
    ap.add_argument("--team-color", default=None, help="contact sheet only: tint the team mask with this colour, e.g. '#3060c0' (default: red placeholder)")
    a = ap.parse_args()

    unit = load_unit(a.name)
    if a.outline_strong:
        unit.outline = OUTLINE_STRONG
    dirs = PREVIEW_DIRS if a.preview else unit.dirs
    base = getattr(unit, "base", None)
    frames = [pick_frame(unit.columns, f) for f in getattr(unit, "preview_frames", getattr(base, "PREVIEW_FRAMES", PREVIEW_FRAMES))] if a.preview else unit.frames
    lods = getattr(unit, "lods", None) or getattr(base, "LODS", LODS)   # bases with their own frame size (cavalry 192x224)
    main_res = lods[0]
    out = os.path.join(route_out(a.out, unit.name), unit.name)   # faction subfolder (korean/, japanese/, ...)
    os.makedirs(out, exist_ok=True)
    reses = [main_res] if a.preview else lods
    if not a.no_collide and getattr(unit, "collide", True):
        collide.report(collide.check_unit(unit, frames, dirs), unit.name)

    t0 = time.time()
    grids = []
    main_grid = None
    for res in reses:
        grid = render_grid(unit, res, dirs, frames)
        tag = "" if res is main_res else res.tag
        sheets, anchors = write_sheets(unit, res, grid, out, tag)
        dev, touching, ncol, npx = check(grid, anchors, res)
        sizes = ", ".join(f"{s['file']} {s['size'][0]}x{s['size'][1]}" for s in sheets)
        print(f"{unit.name}{tag}: {sizes}; max anchor dev {dev}; distinct colours {ncol}; opaque px {npx}")
        if touching:
            print(f"  cells touching the border ({len(touching)}): {' '.join(touching)}")
        grids.append((res, grid))
        if res is main_res:
            main_grid = grid
            contact_paths = save_contact(os.path.join(out, f"contact_{unit.name}.png"), grid, res, [d for d in getattr(unit, "dirs", DIRS) if d in dirs], unit.columns, team_color=a.team_color)
    if not a.preview and unit.name in prone_wide.WIDE_UNITS:
        prone_wide.apply(unit, out)          # wide (1.25x) prone cells: own sheet + layout/anchors patch
    save_img(lod_strip(grids, unit.columns, getattr(unit, "lod_cells", None)), os.path.join(out, f"contact_{unit.name}_lod.png"))

    if not a.preview:
        gd = os.path.join(out, "gif")
        os.makedirs(gd, exist_ok=True)
        for aname, spec in unit.anims.items():
            if len(spec["frames"]) < 2:
                continue
            for d in dirs:
                seq = [main_grid[(d, f)] for f in spec["frames"] if (d, f) in main_grid]
                if len(seq) >= 2:
                    save_gif(os.path.join(gd, f"{unit.name}_{aname}_{d}.gif"), seq, spec["ms"], res=main_res)

    print(f"done in {time.time() - t0:.1f}s")
    print("contact sheet:", ", ".join(contact_paths))
    print("lod strip:    ", os.path.join(out, f"contact_{unit.name}_lod.png"))


if __name__ == "__main__":
    main()
