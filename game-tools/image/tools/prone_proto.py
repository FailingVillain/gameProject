"""PROTOTYPE (branch prone-proto): render prone frames of one unit with per-run tune overrides at a custom cell, no game art touched.
python tools/prone_proto.py <unit> <variant: cur|p07|new> <lod: 64|128> <out_dir> [frames,..]
cell: cur = 64x80 (@64) / 128x160; p07/new = 80x100 (@64) / 160x200 (same zoom, anchor at 9/10 height)."""
import copy, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from voxel import Res
from unit_loader import load_unit
from common import composite
from contact import TEAM_RED
from PIL import Image

class CellRes(Res):
    def __init__(self, base_w, kx=1.25, ky=1.25):
        Res.__init__(self, base_w, base_w * 5 // 4)
        self.fw = int(round(base_w * kx)); self.fh = int(round(base_w * 5 // 4 * ky))
        self.ax, self.ay = self.fw // 2, self.fh * 9 // 10
    @property
    def tag(self): return f"@{self.fw}"

P07 = {"prone_weapon": {"scale": 1.0}, "e_slide": {"prone": 0}, "ne_slide": {"prone": 0}}
NEW = {}

def merge(a, b):
    for k, v in b.items():
        if isinstance(v, dict) and isinstance(a.get(k), dict): merge(a[k], v)
        else: a[k] = v
    return a

def make(unit, variant, lod):
    u = load_unit(unit)
    t = copy.deepcopy(u.tune)
    inf = t.setdefault("poses", {}).setdefault("infantry", {})
    if variant == "p07": merge(inf, P07)
    if variant == "new": merge(inf, NEW)
    u.tune = t
    res = Res(lod, lod * 5 // 4) if variant == "cur" else CellRes(lod)
    return u, res

def render(unit, variant, lod, frames, dirs=("s", "se", "e", "ne", "n")):
    u, res = make(unit, variant, lod)
    out = {}
    for fr in frames:
        for d in dirs:
            f = u.frame(d, fr, res=res)
            im = composite(f.base, f.team, TEAM_RED)
            out[(fr, d)] = im
    return out, res

if __name__ == "__main__":
    unit, variant, lod, outd = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
    frames = (sys.argv[5] if len(sys.argv) > 5 else "aim_prone,fire_prone,go_prone").split(",")
    os.makedirs(outd, exist_ok=True)
    out, res = render(unit, variant, lod, frames)
    for (fr, d), im in out.items(): im.save(os.path.join(outd, f"{variant}_{lod}_{fr}_{d}.png"))
    print(res.size, len(out))
