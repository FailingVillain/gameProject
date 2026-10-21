"""LEGACY: US rifleman tiers (recruit / infantry / veteran) via the old save_unit path -> out/<tier>/*.
The model parts now live in tools/parts/ and the poses in tools/bases/infantry.py; new units are
units/<name>.json rendered with  python make_unit.py <name>.  This file only maps the old tier names onto them
(vox_at_infantry.py / vox_towed.py import kit() and GARAND from here).
Run: python tools/vox_infantry.py [recruit infantry veteran]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from voxel import save_unit, Kit, render_figure  # noqa: E402
from parts.headgear import HEADGEAR  # noqa: E402
from parts.rifles import GARAND, garand_prims, garand_overlay  # noqa: E402, F401
from parts.packs import field_pack as pack_back, canteen  # noqa: E402
from parts.uniforms import UNIFORMS  # noqa: E402
from bases.infantry import pose_for, walk_pose, shoot_pose, LOW  # noqa: E402, F401

# tier -> headgear part   recruit = soft side cap, infantry = bare M1 helmet, veteran = M1 helmet with net
TIER_HEADGEAR = {"recruit": "side_cap", "infantry": "m1_helmet", "veteran": "m1_helmet_net"}
TIERS = {t: HEADGEAR[h]["net"] for t, h in TIER_HEADGEAR.items()}


def kit(tier="veteran"):
    hg = HEADGEAR[TIER_HEADGEAR[tier]]
    return Kit(**UNIFORMS["us_od"], headgear=hg["fn"], back=lambda T: pack_back(T) + canteen(T))


def frame(d, name, tier="veteran"):
    return render_figure(pose_for(name), kit(tier), d, GARAND, net=TIERS[tier])


if __name__ == "__main__":
    only = sys.argv[1:] or list(TIERS)
    for t in only:
        save_unit(t, lambda d, n, t=t: frame(d, n, t))
        print(t, "done")
