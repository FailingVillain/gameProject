"""LMG aim check: screen angle of the gun barrel (butt plate -> muzzle) per direction vs the nominal direction.
python tools/mg_axis.py [unit] [frame...]   (default heavy-machinegunner mg_aim mg_fire_a mg_fire_b)
Screen angle: 0 = straight down (s), 90 = right (e), 180 = up (n). The game map is drawn top-down, so a sprite row for yaw Y
must show the barrel at screen angle Y exactly (target bearing on screen = world bearing)."""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import voxel  # noqa: E402
from voxel import build_figure, ry, UP  # noqa: E402
from unit_loader import load_unit  # noqa: E402


def barrel_screen(unit, frame, d):
    """-> (screen angle deg, butt (x, y) model units, muzzle (x, y)) for direction d (name or yaw)."""
    yaw = voxel.YAWS[d] if isinstance(d, str) else float(d)
    with unit.active():
        pose = unit.base.pose_for(frame, d, unit.e_pull_extra)
        prims, _ = build_figure(pose, unit.kit, unit.weapon)
    w = [p for p in prims if p.src == "weapon"]
    butt, muzzle = w[8].M[:3, 3], w[11].M[:3, 3]           # lmg_prims: [8] cooling shroud centre (z 12.5), [11] muzzle ring (z 27.5), both on the bore axis
    R = ry(yaw)[:3, :3] @ ry(pose.get("view_yaw", 0.0))[:3, :3]       # whole-figure turn (voxel.render_figure)
    def scr(p):
        q = R @ p
        return np.array([q[0], -(q @ UP)])
    b, m = scr(butt), scr(muzzle)
    v = m - b
    return math.degrees(math.atan2(v[0], v[1])), b, m


def main(argv):
    name = argv[0] if argv else "heavy-machinegunner"
    frames = argv[1:] or ["mg_aim", "mg_fire_a", "mg_fire_b"]
    u = load_unit(name)
    worst = 0.0
    for f in frames:
        for d in u.dirs:
            a, b, m = barrel_screen(u, f, d)
            nom = voxel.YAWS[d]
            err = (a - nom + 180) % 360 - 180
            worst = max(worst, abs(err))
            print(f"{f:10s} {d:4s} nominal {nom:6.1f}  barrel {a:7.2f}  err {err:+6.2f}  len {np.linalg.norm(m - b):5.1f}")
    print(f"worst |err| = {worst:.2f} deg")
    return worst


if __name__ == "__main__":
    main(sys.argv[1:])
