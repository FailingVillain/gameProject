"""python sheet.py blender_out_dir out_sheet.png : row1 voxel LOD0 160x200, row2 Blender (downscaled 3x)"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools")); sys.path.insert(0, os.path.join(HERE, ".."))
from PIL import Image
from voxel import Res
from unit_loader import load_unit
src, dst = sys.argv[1:3]
W, H = 160, 200
u = load_unit("infantry-regular"); res = Res(W, H)
names = [f"{n}_{d}" for n in ("idle", "fire_a") for d in ("s", "se", "e", "ne", "n")]
sh = Image.new("RGBA", (W * 10, H * 2), (120, 130, 120, 255))
for i, n in enumerate(names):
    fn, d = n.rsplit("_", 1)
    v = u.frame(d, fn, res=res).base.convert("RGBA")
    sh.alpha_composite(v, (i * W, 0))
    b = Image.open(os.path.join(src, n + "_hi.png")).convert("RGBA").resize((W, H), Image.LANCZOS)
    b.save(os.path.join(src, n + ".png")); sh.alpha_composite(b, (i * W, H))
    t = Image.open(os.path.join(src, n + "_team_hi.png")).convert("RGBA").resize((W, H), Image.LANCZOS)
sh.convert("RGB").save(dst); print(sh.size)
