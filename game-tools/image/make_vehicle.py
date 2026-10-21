"""Render one data-driven vehicle (units/<name>.json with "kind": "vehicle"): hull sheet + separately rotating turret sheet.

    python make_vehicle.py <name> [--preview] [--out DIR]

Writes into <out>/<name>/ (default <game-tools>/korean-division-image-out/<name>/), for each LOD
(hull 192x192 / 96x96 / 48x48, turret 160x160 / 80x80 / 40x40; each rendered natively, file tag = frame width):
  <name>_hull[@N].png / _team.png        rows = 9 directions (s, sse, se, ese, e, ene, ne, nne, n; the western ones are
                                         mirrored by the game), columns = hull frames (anims/vehicle.json "hull")
  <name>_turret[@N].png / _team.png      same rows, columns = turret frames; every cell has the pivot (ring centre) at
                                         the same point (layout "anchor")
  <name>_hull[@N]_layout.json            frame, anchor (ground), dirs, mirror_map, columns, anims, sheets, cells,
                                         turret {layout, mount{dir: [dx, dy] px from the ground anchor}, ring_model}
  <name>_turret[@N]_layout.json          frame, anchor (= pivot), columns, anims, sheets, cells, hull layout name
  <name>_<part>[@N]_anchors.json         {dir: [[x, y] per column]} (vehicles never auto-shift: identical to anchor)
  contact_<name>_hull.png / _turret.png  labelled review sheets (team layer red, 2x)
  contact_<name>.png                     composed review: hull + turret at the mount for every direction
                                         (idle, move+fire, dead, turret turned +90 deg, mirrored western facing)
  contact_<name>_lod.png                 composed idle S + E at all three sizes
  gif/<name>_<anim>_<dir>.gif, gif/<name>_rotate.gif (16 facings), gif/<name>_turret_spin.gif (hull S, turret 16 facings)
--preview: s and e only, hull idle/move_1/dead + turret idle/fire at the main size, no GIFs / LODs.
"""
import argparse
import json
import os
import sys
import time

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "tools"))
from common import composite, Frame  # noqa: E402
from contact import contact_sheet, _font, BG, TEAM_RED  # noqa: E402
from vehicles.vehcommon import (DIRS9, DIRS16, MIRROR, YAW9, HULL_LODS, TURRET_LODS, mount_px, compose, edge_margin,  # noqa: E402
                                shade_coverage, team_coverage, diff_px, VEHICLE_MATERIALS)
from vehicles.loader import load_vehicle  # noqa: E402
from voxel import make_frame  # noqa: E402
from outroute import route_out  # noqa: E402

GAME_TOOLS = os.path.dirname(HERE)
DEFAULT_OUT = os.path.join(GAME_TOOLS, "korean-division-image-out")
MAX_SHEET_W = 4096
GRASS = (78, 110, 64, 255)
PREVIEW_DIRS = ["s", "e"]
PREVIEW_HULL = ["idle", "move_1", "dead"]
PREVIEW_TURRET = ["idle", "fire"]
GIF_SCALE = 2


def save_img(img, path, **kw):
    """save; OneDrive sync / a viewer may hold the file briefly -> retry, then fall back to <name>.new.<ext>."""
    for attempt in range(6):
        try:
            img.save(path, **kw)
            return
        except OSError as e:
            err = e
            time.sleep(0.5 * (attempt + 1))
    base, ext = os.path.splitext(path)
    alt = f"{base}.new{ext}"
    img.save(alt, **kw)
    print(f"  [warn] could not write {os.path.basename(path)} ({err.strerror}); wrote {os.path.basename(alt)} instead")


def save_gif(path, frames, ms, size, scale=GIF_SCALE):
    imgs = []
    for f in frames:
        bg = Image.new("RGBA", size, GRASS)
        bg.alpha_composite(composite(f.base, f.team, TEAM_RED))
        imgs.append(bg.resize((size[0] * scale, size[1] * scale), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
    imgs[0].save(path, save_all=True, append_images=imgs[1:], duration=ms, loop=0, disposal=2)


def render_grid(fn, res, dirs, frames, label, log=print):
    grid = {}
    t0 = time.time()
    for d in dirs:
        for fr in frames:
            grid[(d, fr)] = fn(d, fr, res)
        log(f"  {label} {res.fw}x{res.fh} {d}: {len(frames)} frames ({time.time() - t0:.1f}s)")
    return grid


def write_sheets(name, part, res, grid, cols, anims, out, tag, extra):
    fw, fh = res.size
    per_sheet = max(1, MAX_SHEET_W // fw)
    chunks = [cols[i:i + per_sheet] for i in range(0, len(cols), per_sheet)]
    stem = f"{name}_{part}{tag}"
    sheets, cells = [], {}
    for k, chunk in enumerate(chunks):
        fname = f"{stem}.png" if len(chunks) == 1 else f"{stem}_{k + 1}.png"
        sheet = Image.new("RGBA", (fw * len(chunk), fh * len(DIRS9)), (0, 0, 0, 0))
        team = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
        for r, d in enumerate(DIRS9):
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
    anchors = {d: [None] * len(cols) for d in DIRS9}
    for (d, col), f in grid.items():
        ax, ay = getattr(f, "anchor", res.anchor)
        anchors[d][cols.index(col)] = [round(float(ax), 1), round(float(ay), 1)]
    layout = {"part": part, "frame": [fw, fh], "anchor": list(res.anchor), "dirs": list(DIRS9), "yaw_step": 22.5,
              "mirror": True, "mirror_map": MIRROR, "columns": cols, "anims": anims, "sheets": sheets, "cells": cells,
              "anchors": f"{stem}_anchors.json"}
    layout.update(extra)
    with open(os.path.join(out, f"{stem}_layout.json"), "w", encoding="utf-8") as fh:
        json.dump(layout, fh, indent=1)
    with open(os.path.join(out, f"{stem}_anchors.json"), "w") as fh:
        json.dump(anchors, fh)
    return sheets, anchors


def lod_strip(cells):
    """cells: list of (Frame, label) already composed; each 2x, bottom-aligned, labelled."""
    scale, gut, lab = 2, 8, 18
    font = _font()
    imgs = []
    for f, label in cells:
        bg = Image.new("RGBA", f.base.size, GRASS)
        bg.alpha_composite(composite(f.base, f.team, TEAM_RED))
        imgs.append((bg.resize((f.w * scale, f.h * scale), Image.NEAREST), label))
    min_w = 150
    W = gut + sum(max(c.width, min_w) + gut for c, _ in imgs)
    hmax = max(c.height for c, _ in imgs)
    img = Image.new("RGBA", (W, gut + hmax + lab + gut), BG)
    draw = ImageDraw.Draw(img)
    x = gut
    for c, label in imgs:
        img.alpha_composite(c, (x, gut + hmax - c.height))
        draw.text((x, gut + hmax + 2), label, fill=(255, 255, 255, 255), font=font)
        x += max(c.width, min_w) + gut
    return img


def mirrored_cell(grid, d, fr):
    """(frame, mirrored?) for any of the 16 facings using the 9 rendered rows."""
    if d in MIRROR:
        return grid[(MIRROR[d], fr)], True
    return grid[(d, fr)], False


def composed(veh, hgrid, tgrid, hres, tres, hd, hf, td, tf):
    hull, mh = mirrored_cell(hgrid, hd, hf)
    tur, mt = mirrored_cell(tgrid, td, tf)
    base_hd = MIRROR.get(hd, hd)
    return compose(hull, tur, hres, tres, [mount_px(r, YAW9[base_hd], hres) for r in veh.rings], mirror_hull=mh, mirror_turret=mt)


def check_part(label, grid, res, anchors, mats):
    margins = {k: edge_margin(f) for k, f in grid.items()}
    touching = [f"{d}/{fr}" for (d, fr), mg in margins.items() if mg is not None and mg == 0]
    min_margin = min(mg for mg in margins.values() if mg is not None)
    vals = [v for row in anchors.values() for v in row if v]
    dev = max(max(abs(x - res.ax), abs(y - res.ay)) for x, y in vals)
    cov = shade_coverage(list(grid.values()), mats)
    nteam, npteam = team_coverage(list(grid.values()))
    rgb_all = np.concatenate([np.array(f.base)[..., :3][np.array(f.base)[..., 3] > 0] for f in grid.values()])
    ncol = len(np.unique(rgb_all, axis=0))
    print(f"  [{label}] anchor dev {dev}; min border margin {min_margin}px; distinct colours {ncol}; "
          f"team greys {nteam} ({npteam}px); shades " + " ".join(f"{m}={n}/8" for m, n in cov.items()))
    if touching:
        print(f"  [{label}] cells touching the border ({len(touching)}): {' '.join(touching)}")
    return dev, min_margin, cov


def composition_check(veh, hgrid, tgrid, hres, tres):
    """render hull+turret as ONE model and compare with the composed two-sheet cell: mismatch px per direction.
    Differences are expected only along the overlap seam (outline / z-fight), so the fraction should stay small."""
    worst = (0, None)
    report = []
    for d in DIRS9:
        prims, ov = veh.whole_prims("idle", "idle")
        one = make_frame(prims, ov, YAW9[d], res=hres)
        two = compose(hgrid[(d, "idle")], tgrid[(d, "idle")], hres, tres, [mount_px(r, YAW9[d], hres) for r in veh.rings])
        a1 = np.array(one.base)[..., 3] > 0; a2 = np.array(two.base)[..., 3] > 0
        n = int((a1 ^ a2).sum())                       # silhouette mismatch
        tot = int(a1.sum())
        col = diff_px(one.base, two.base)
        report.append(f"{d}:{n}/{col}")
        if n > worst[0]:
            worst = (n, d)
    print(f"  [compose] silhouette mismatch px / any-diff px per dir: {' '.join(report)}  (worst {worst[1]} {worst[0]} of ~{tot} px)")
    return worst


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--out", default=DEFAULT_OUT, help="output root (faction subfolder + unit folder are created inside)")
    a = ap.parse_args()

    veh = load_vehicle(a.name)
    dirs = PREVIEW_DIRS if a.preview else veh.dirs
    hcols = PREVIEW_HULL if a.preview else veh.hull_columns
    tcols = PREVIEW_TURRET if a.preview else veh.turret_columns
    out = os.path.join(route_out(a.out, veh.name), veh.name)   # -> <out>/vehicles/<name>/
    os.makedirs(out, exist_ok=True)
    lods = [(HULL_LODS[0], TURRET_LODS[0])] if a.preview else list(zip(HULL_LODS, TURRET_LODS))
    body_mats = [m for m in VEHICLE_MATERIALS if m in {"veh_od", "veh_od2", "veh_khaki", "veh_khaki2", "veh_track", "veh_track2", "veh_steel", "veh_rubber", "veh_burn"}]

    t0 = time.time()
    main_grids = None
    lod_cells = []
    for hres, tres in lods:
        tag = "" if hres is HULL_LODS[0] else f"@{hres.fw}"
        ttag = "" if tres is TURRET_LODS[0] else f"@{tres.fw}"
        hgrid = render_grid(veh.hull_frame, hres, dirs, hcols, "hull")
        tgrid = render_grid(veh.turret_frame, tres, dirs, tcols, "turret")
        mount = {d: [round(v, 2) for v in mount_px(veh.ring, YAW9[d], hres)] for d in DIRS9}
        hextra = {"turret": {"layout": f"{veh.name}_turret{ttag}_layout.json", "mount": mount, "ring_model": list(veh.ring),
                             "mounts": [{d: [round(v, 2) for v in mount_px(r, YAW9[d], hres)] for d in DIRS9} for r in veh.rings],
                             "rings_model": [list(r) for r in veh.rings],
                             "pivot": list(tres.anchor), "frame": list(tres.size)}}
        hsheets, hanch = write_sheets(veh.name, "hull", hres, hgrid, hcols, veh.anims["hull"], out, tag, hextra)
        tsheets, tanch = write_sheets(veh.name, "turret", tres, tgrid, tcols, veh.anims["turret"], out, ttag,
                                      {"hull": f"{veh.name}_hull{tag}_layout.json", "pivot_is_ring_centre": True})
        print(f"{veh.name}{tag}: hull " + ", ".join(f"{s['file']} {s['size'][0]}x{s['size'][1]}" for s in hsheets) +
              "; turret " + ", ".join(f"{s['file']} {s['size'][0]}x{s['size'][1]}" for s in tsheets))
        check_part(f"hull{tag}", hgrid, hres, hanch, body_mats)
        check_part(f"turret{ttag}", tgrid, tres, tanch, body_mats)
        if not a.preview:
            composition_check(veh, hgrid, tgrid, hres, tres)
        for d in ("s", "e"):
            if d in dirs:
                lod_cells.append((composed(veh, hgrid, tgrid, hres, tres, d, "idle", d, "idle"), f"{hres.fw}x{hres.fh} {d.upper()}-idle"))
        if hres is HULL_LODS[0]:
            main_grids = (hgrid, tgrid, hres, tres)
            save_img(contact_sheet(hgrid, hres, dirs, hcols), os.path.join(out, f"contact_{veh.name}_hull.png"))
            save_img(contact_sheet(tgrid, tres, dirs, tcols), os.path.join(out, f"contact_{veh.name}_turret.png"))
            # composed review sheet
            combos = [("idle", "idle", 0, "idle"), ("move_1", "fire", 0, "move+fire"), ("dead", "dead", 0, "dead"),
                      ("idle", "idle", 4, "turret+90"), ("idle", "idle", 10, "turret+225")]
            cgrid, ccols = {}, []
            for hf, tf, off, lab in combos:
                if hf not in hcols or tf not in tcols:
                    continue
                ccols.append(lab)
                for d in dirs:
                    td = DIRS16[(DIRS16.index(d) + off) % 16]
                    if (MIRROR.get(td, td), tf) in tgrid:
                        cgrid[(d, lab)] = composed(veh, hgrid, tgrid, hres, tres, d, hf, td, tf)
            if not a.preview:
                ccols.append("mirror(W side)")
                for d in dirs:
                    md = DIRS16[(-DIRS16.index(d)) % 16]            # the mirrored facing of this row (e.g. se -> sw)
                    cgrid[(d, "mirror(W side)")] = composed(veh, hgrid, tgrid, hres, tres, md, "idle", md, "idle")
            save_img(contact_sheet(cgrid, hres, dirs, ccols), os.path.join(out, f"contact_{veh.name}.png"))
    save_img(lod_strip(lod_cells), os.path.join(out, f"contact_{veh.name}_lod.png"))

    if not a.preview:
        hgrid, tgrid, hres, tres = main_grids
        gd = os.path.join(out, "gif")
        os.makedirs(gd, exist_ok=True)
        for aname, spec in veh.anims["hull"].items():
            if len(spec["frames"]) < 2:
                continue
            for d in dirs:
                seq = [composed(veh, hgrid, tgrid, hres, tres, d, f, d, "idle") for f in spec["frames"]]
                save_gif(os.path.join(gd, f"{veh.name}_{aname}_{d}.gif"), seq, spec["ms"], hres.size)
        for aname, spec in veh.anims["turret"].items():
            if len(spec["frames"]) < 2:
                continue
            for d in dirs:
                seq = [composed(veh, hgrid, tgrid, hres, tres, d, "idle", d, f) for f in spec["frames"]]
                save_gif(os.path.join(gd, f"{veh.name}_{aname}_{d}.gif"), seq, spec["ms"], hres.size)
        rot = [composed(veh, hgrid, tgrid, hres, tres, d, "idle", d, "idle") for d in DIRS16]
        save_gif(os.path.join(gd, f"{veh.name}_rotate.gif"), rot, 140, hres.size)
        spin = [composed(veh, hgrid, tgrid, hres, tres, "s", "idle", d, "idle") for d in DIRS16]
        save_gif(os.path.join(gd, f"{veh.name}_turret_spin.gif"), spin, 140, hres.size)

    print(f"done in {time.time() - t0:.1f}s")
    print("contact sheets:", os.path.join(out, f"contact_{veh.name}.png"))
    print("               ", os.path.join(out, f"contact_{veh.name}_hull.png"))
    print("               ", os.path.join(out, f"contact_{veh.name}_turret.png"))
    print("lod strip:     ", os.path.join(out, f"contact_{veh.name}_lod.png"))


if __name__ == "__main__":
    main()
