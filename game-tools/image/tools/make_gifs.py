"""Example GIFs for every unit: out/<unit>/gif/<unit>_{rotate,attack,death,idle}.gif
Team colour applied, grass background, 3x nearest, looping. Run after the vox_* generators:  python tools/make_gifs.py [unit ...]
Infantry-type sheets: 5 rows (s, se, e, ne, n) mirrored to 8 directions. Vehicles: 9 rows mirrored to 16 directions.
"""
import json
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, FRAMES, composite  # noqa: E402

BG = (78, 110, 64, 255)
SCALE = 3
INF = ["recruit", "infantry", "veteran", "scout", "at_infantry", "worker"]
VEH = ["tank", "artillery", "towed_artillery"]
IDX = {n: i for i, n in enumerate(FRAMES)}


def load(unit, suffix=""):
    d = os.path.join(OUT, unit)
    return (Image.open(os.path.join(d, f"{unit}{suffix}.png")).convert("RGBA"),
            Image.open(os.path.join(d, f"{unit}{suffix}_team.png")).convert("RGBA"))


def place(img, size, canvas, anchor, target, mirror):
    """crop a cell (already extracted), optionally mirror about its anchor, paste so anchor lands on target."""
    ax, ay = anchor
    if mirror:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
        ax = size[0] - ax
    out = Image.new("RGBA", canvas, (0, 0, 0, 0))
    out.paste(img, (int(round(target[0] - ax)), int(round(target[1] - ay))))
    return out


def save_gif(path, frames, durations):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    imgs = []
    for f in frames:
        bg = Image.new("RGBA", f.size, BG)
        bg.alpha_composite(f)
        imgs.append(bg.resize((f.width * SCALE, f.height * SCALE), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
    imgs[0].save(path, save_all=True, append_images=imgs[1:], duration=durations, loop=0, disposal=2)


# ------------------------------------------------------------------ infantry-type
def inf_gifs(unit):
    base, team = load(unit)
    from common import FW, FH, DIRS
    anchors = json.load(open(os.path.join(OUT, unit, f"{unit}_anchors.json")))
    canvas, target = (FW * 3 // 2, FH * 7 // 5), (FW * 3 // 4, FH * 11 // 10)      # (96,112)/(48,88) at 64x80

    def cell(dname, fname, mirror=False):
        r = DIRS.index(dname)
        c = IDX[fname]
        box = (c * FW, r * FH, (c + 1) * FW, (r + 1) * FH)
        im = composite(base.crop(box), team.crop(box))
        return place(im, (FW, FH), canvas, anchors[dname][c], target, mirror)

    order = [("s", False), ("se", False), ("e", False), ("ne", False), ("n", False), ("ne", True), ("e", True), ("se", True)]
    d = os.path.join(OUT, unit, "gif")
    save_gif(os.path.join(d, f"{unit}_rotate.gif"), [cell(n, "idle", m) for n, m in order], 140)
    seq, dur = [], []
    for v in "abc":
        for fn, ms in ((f"aim_{v}", 450), (f"fire_{v}", 160), (f"aim_{v}", 300)):
            seq.append(cell("se", fn)); dur.append(ms)
    save_gif(os.path.join(d, f"{unit}_attack.gif"), seq, dur)
    save_gif(os.path.join(d, f"{unit}_death.gif"), [cell("se", n) for n in ("hit", "fall", "dead")], [250, 250, 1000])
    save_gif(os.path.join(d, f"{unit}_idle.gif"), [cell("se", "idle"), cell("se", "idle_alt")], [2000, 1000])
    save_gif(os.path.join(d, f"{unit}_walk.gif"), [cell("se", f"walk_{i}") for i in (1, 2, 3, 4)], 130)


# ------------------------------------------------------------------ vehicles
ROWS = ["s", "s_se", "se", "e_se", "e", "e_ne", "ne", "n_ne", "n"]
DIR16 = [(i, False) for i in range(9)] + [(i, True) for i in range(7, 0, -1)]


def veh_gifs(unit):
    FR = 112
    d = os.path.join(OUT, unit, "gif")
    if unit == "tank":
        hb, ht = load("tank")
        tb, tt = (Image.open(os.path.join(OUT, "tank", "tank_turret.png")).convert("RGBA"),
                  Image.open(os.path.join(OUT, "tank", "tank_turret_team.png")).convert("RGBA"))
        meta = json.load(open(os.path.join(OUT, "tank", "tank_anchors.json")))
        anchor = tuple(meta["hull"]["s"]["ground"])

        def cell(row, hc, tc, mirror=False):
            r = ROWS.index(row)
            hbx = (hc * FR, r * FR, (hc + 1) * FR, (r + 1) * FR)
            tbx = (tc * FR, r * FR, (tc + 1) * FR, (r + 1) * FR)
            im = composite(hb.crop(hbx), ht.crop(hbx))
            mx, my = meta["hull"][row]["mount"]
            px, py = meta["turret"][row]["pivot"]
            t = composite(tb.crop(tbx), tt.crop(tbx))
            im2 = Image.new("RGBA", (FR, FR), (0, 0, 0, 0))
            im2.alpha_composite(im)
            tmp = Image.new("RGBA", (FR, FR), (0, 0, 0, 0))
            tmp.paste(t, (int(round(mx - px)), int(round(my - py))))
            im2.alpha_composite(tmp)
            return place(im2, (FR, FR), (FR, FR), anchor, anchor, mirror)

        rotate = [cell(ROWS[i], 0, 0, m) for i, m in DIR16]
        attack = [cell("se", 0, 0), cell("se", 0, 1), cell("se", 0, 1), cell("se", 0, 0)]
        att_d = [500, 140, 140, 500]
        death = [cell("se", 0, 0), cell("se", 3, 2)]
        death_d = [600, 1000]
        idle = [cell("se", 0, 0), cell("se", 1, 0), cell("se", 2, 0)]
        idle_d = [900, 220, 220]
    else:
        cols = {"artillery": dict(idle=0, mv=[1, 2], fire=3, dead=4),
                "towed_artillery": dict(idle=2, mv=[0, 1], fire=3, dead=4, deploy=5)}[unit]
        base, team = load(unit)
        meta = json.load(open(os.path.join(OUT, unit, f"{unit}_anchors.json")))
        anchor = tuple(meta["ground"])

        def cell(row, c, mirror=False):
            r = ROWS.index(row)
            box = (c * FR, r * FR, (c + 1) * FR, (r + 1) * FR)
            im = composite(base.crop(box), team.crop(box))
            return place(im, (FR, FR), (FR, FR), anchor, anchor, mirror)

        rotate = [cell(ROWS[i], cols["idle"], m) for i, m in DIR16]
        if unit == "towed_artillery":
            attack = [cell("se", cols["deploy"]), cell("se", cols["idle"]), cell("se", cols["fire"]), cell("se", cols["idle"])]
            att_d = [700, 700, 220, 600]
        else:
            attack = [cell("se", cols["idle"]), cell("se", cols["fire"]), cell("se", cols["idle"])]
            att_d = [600, 220, 600]
        death = [cell("se", cols["idle"]), cell("se", cols["dead"])]
        death_d = [600, 1000]
        idle = [cell("se", cols["idle"])] + [cell("se", c) for c in cols["mv"]]
        idle_d = [900, 220, 220]
    save_gif(os.path.join(d, f"{unit}_rotate.gif"), rotate, 120)
    save_gif(os.path.join(d, f"{unit}_attack.gif"), attack, att_d)
    save_gif(os.path.join(d, f"{unit}_death.gif"), death, death_d)
    save_gif(os.path.join(d, f"{unit}_idle.gif"), idle, idle_d)


if __name__ == "__main__":
    units = sys.argv[1:] or INF + VEH
    for u in units:
        if not os.path.isfile(os.path.join(OUT, u, f"{u}.png")):
            print("skip", u)
            continue
        (inf_gifs if u in INF else veh_gifs)(u)
        print("gifs", u)
