"""Render building sprites (3D primitives, fixed orientation, quarter view ELEV 36 deg).

    python make_building.py <name|all|houses|village|gen> [--lod 0,1,2] [--out DIR] [--set proto,ko,zh] [--shard i/n]
(generated houses: JSON specs in tools/buildings/house_specs/, see HOUSEGEN_SCHEMA.md; `gen` = every house of --set)

Per building -> <out>/buildings/<name>/:
  <name>.png / _team.png            full LOD (128 px per tile), frames side by side (states), same size each
  <name>@64.png / @32.png (+_team)   half / quarter LOD, each rendered natively
  <name>_layout.json                 frame size, anchor, states, footprint tiles, per-LOD sizes
  contact_<name>.png                 review sheet (team layer red, on grass, all states + LOD row)
and <out>/buildings/buildings_overview.png when 'all' (or several) are rendered.
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
sys.path.insert(0, os.path.join(HERE, "tools", "buildings"))
from bcommon import frame_for, make_res, render, TILE_PX  # noqa: E402
from common import composite  # noqa: E402
import houses  # noqa: E402
import village  # noqa: E402
import ruins  # noqa: E402
import housegen  # noqa: E402

DEFAULT_OUT = r"C:\gameProject\game-tools\korean-division-image-out"
GRASS = (78, 110, 64, 255)
TEAM_RED = (210, 40, 40)
SUFFIX = ["", "@64", "@32"]


def registry(gen_sets=None):
    """legacy houses + villages; gen_sets (list of set names, or ["*"]) additionally loads tools/buildings/house_specs/*.json"""
    reg = {}
    for n, (fn, bbox, fp, label) in houses.HOUSES.items():
        rf = ruins.RUINS[n][0]   # T39i: two ruin variants per house (ruins.py)
        reg[n] = dict(states={"intact": lambda fn=fn: fn(ruin=False), "ruined": lambda rf=rf: rf(v="a"), "ruined_b": lambda rf=rf: rf(v="b")}, bbox=bbox, footprint=fp, label=label)
    for n, d in village.VILLAGES.items():
        reg[n] = d
    if gen_sets:
        reg.update(housegen.REGISTRY(only_sets=None if "*" in gen_sets else set(gen_sets)))
    return reg


def save(img, path):
    for k in range(6):
        try:
            img.save(path)
            return
        except OSError:
            time.sleep(0.5 * (k + 1))
    img.save(path.replace(".png", ".new.png"))


def build_one(name, d, out_root, lods):
    t_all = time.time()
    geom = frame_for(d["bbox"])
    states = list(d["states"])
    odir = os.path.join(out_root, name)
    os.makedirs(odir, exist_ok=True)
    prim_sets = {s: fn() for s, fn in d["states"].items()}
    frames = {}
    times = {}
    for lod in lods:
        res = make_res(lod, geom)
        t0 = time.time()
        cols = max(1, min(len(states), 4096 // res.fw)); rows = -(-len(states) // cols)
        sheet = Image.new("RGBA", (res.fw * cols, res.fh * rows), (0, 0, 0, 0))
        team = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
        for i, s in enumerate(states):
            f = render(prim_sets[s], res)
            sheet.alpha_composite(f.base, ((i % cols) * res.fw, (i // cols) * res.fh))
            team.alpha_composite(f.team, ((i % cols) * res.fw, (i // cols) * res.fh))
            frames[(lod, s)] = f
        times[lod] = time.time() - t0
        save(sheet, os.path.join(odir, f"{name}{SUFFIX[lod]}.png"))
        save(team, os.path.join(odir, f"{name}{SUFFIX[lod]}_team.png"))
        print(f"  {name} LOD{lod} frame {res.fw}x{res.fh} x{len(states)} states: {times[lod]:.1f}s", flush=True)
    layout = {"name": name, "label": d["label"], "states": states, "footprint_tiles": list(d["footprint"]),
              "tile_px": TILE_PX, "lods": {}}
    for lod in lods:
        r = make_res(lod, geom)
        layout["lods"][SUFFIX[lod] or "@128"] = {"frame": [r.fw, r.fh], "anchor": [r.ax, r.ay], "tile_px": TILE_PX[lod],
                                                 "sheet": f"{name}{SUFFIX[lod]}.png", "team": f"{name}{SUFFIX[lod]}_team.png",
                                                 "cols": max(1, min(len(states), 4096 // r.fw)), "cells": {s: [i % max(1, min(len(states), 4096 // r.fw)), i // max(1, min(len(states), 4096 // r.fw))] for i, s in enumerate(states)}}
    layout["anchor_note"] = "anchor = ground point at the centre of the footprint; place at footprint centre in world px (tile = 32 px at base zoom = the @32 LOD)"
    with open(os.path.join(odir, f"{name}_layout.json"), "w", encoding="utf-8") as fh:
        json.dump(layout, fh, ensure_ascii=False, indent=1)
    contact(name, d, states, frames, geom, lods, odir)
    print(f"{name}: done {time.time() - t_all:.1f}s", flush=True)
    return frames, geom, times


def tinted(f):
    return composite(f.base, f.team, TEAM_RED)


def label(draw, xy, text):
    draw.text(xy, text, fill=(255, 255, 255, 255))


def contact(name, d, states, frames, geom, lods, odir):
    lod0 = min(lods)
    r0 = make_res(lod0, geom)
    sc = 1.0 if lod0 == 0 and r0.fw * len(states) <= 2400 else 0.5
    W = int(r0.fw * sc) * len(states) + 8 * (len(states) + 1)
    rows_h = int(r0.fh * sc)
    lod_row_h = make_res(max(lods), geom).fh * 2 if len(lods) > 1 else 0
    H = rows_h + 30 + (make_res(1, geom).fh + 30 if 1 in lods else 0)
    img = Image.new("RGBA", (W, H), GRASS)
    dr = ImageDraw.Draw(img)
    x = 8
    for s in states:
        im = tinted(frames[(lod0, s)])
        if sc != 1.0:
            im = im.resize((int(im.width * sc), int(im.height * sc)), Image.NEAREST)
        img.alpha_composite(im, (x, 22))
        label(dr, (x, 4), f"{name} / {s}  {r0.fw}x{r0.fh} anchor ({r0.ax},{r0.ay})")
        x += im.width + 8
    if 1 in lods:
        y = rows_h + 26
        x = 8
        for lod in lods:
            if lod == lod0:
                continue
            for s in states:
                im = tinted(frames[(lod, s)])
                img.alpha_composite(im, (x, y + 14))
                label(dr, (x, y), f"LOD{lod} {s}")
                x += im.width + 8
        # anchor marks
    save(img, os.path.join(odir, f"contact_{name}.png"))


def overview(items, out_root):
    """items: list of (name, d, frames, geom). Intact + ruined at half LOD in rows."""
    cells = []
    for name, d, frames, geom in items:
        for s in d["states"]:
            if (1, s) in frames:
                cells.append((f"{name}/{s}", tinted(frames[(1, s)])))
    cw = max(c[1].width for c in cells) + 8
    ch = max(c[1].height for c in cells) + 22
    cols = 4
    rows = (len(cells) + cols - 1) // cols
    # village frames are large: lay out with a row-packing
    img_rows, cur, curw = [], [], 8
    maxw = 3000
    for c in cells:
        if curw + c[1].width + 8 > maxw and cur:
            img_rows.append(cur); cur, curw = [], 8
        cur.append(c); curw += c[1].width + 8
    if cur:
        img_rows.append(cur)
    H = sum(max(c[1].height for c in r) + 26 for r in img_rows) + 8
    W = max(sum(c[1].width + 8 for c in r) for r in img_rows) + 8
    img = Image.new("RGBA", (W, H), GRASS)
    dr = ImageDraw.Draw(img)
    y = 4
    for r in img_rows:
        x = 8
        for lab, im in r:
            img.alpha_composite(im, (x, y + 16))
            label(dr, (x, y), lab)
            x += im.width + 8
        y += max(c[1].height for c in r) + 26
    p = os.path.join(out_root, "buildings_overview.png")
    save(img, p)
    print("overview:", p)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("--lod", default="0,1,2")
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--set", default="", help="generated-house set(s) to load, comma list (e.g. proto,ko,zh); 'gen' name then = every house of those sets")
    ap.add_argument("--shard", default="", help="i/n: render every n-th name starting at i (0-based), for parallel-safe batches")
    a = ap.parse_args()
    gen_sets = [x for x in a.set.split(",") if x] or (["*"] if a.name not in ("all", "houses", "village") and not a.name.startswith("house-") and not a.name.startswith("village") else None)
    reg = registry(gen_sets)
    if a.name == "gen":
        names = [n for n, d in reg.items() if "set" in d]
    elif a.name == "all":
        names = list(reg)
    elif a.name == "houses":
        names = list(houses.HOUSES)
    elif a.name == "village":
        names = list(village.VILLAGES)
    else:
        names = a.name.split(",")
    if a.shard:
        si, sn = (int(v) for v in a.shard.split("/"))
        names = names[si::sn]
    lods = [int(x) for x in a.lod.split(",")]
    out_root = os.path.join(a.out, "buildings")
    os.makedirs(out_root, exist_ok=True)
    items = []
    for n in names:
        frames, geom, _ = build_one(n, reg[n], out_root, lods)
        items.append((n, reg[n], frames, geom))
    if len(names) > 1 and 1 in lods:
        overview(items, out_root)


if __name__ == "__main__":
    main()
