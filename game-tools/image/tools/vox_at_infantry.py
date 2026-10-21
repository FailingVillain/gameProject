"""US anti-tank infantry (key: at_infantry) - rifleman kit + M1 bazooka.
Run: python tools/vox_at_infantry.py -> out/at_infantry/*
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import save_sheet  # noqa: E402
from voxel import save_unit, death_pose, Kit, Weapon, box, ell, cyl, seg, along_z, tr, rx, render_figure  # noqa: E402
import vox_infantry as inf  # noqa: E402
from poses import parse, walk_legs, STANCE  # noqa: E402
from voxel import rbox as box  # noqa: E402


def bazooka_prims(M):
    return [along_z(M, 2.5, -17.0, 22.0, "olive"),                          # tube
            along_z(M, 3.1, 18.0, 23.0, "olive"),                           # front flare
            along_z(M, 3.1, -19.0, -15.0, "olive"),                         # rear flare
            along_z(M, 2.1, 23.0, 24.0, "steel"),
            box(M @ tr(0, -4.6, -3.0), 0.9, 2.2, 1.0, "wood"),              # pistol grip
            box(M @ tr(0, -4.6, 8.0), 0.9, 2.0, 1.0, "wood"),              # fore grip
            box(M @ tr(0, -2.7, 3.5), 0.7, 0.9, 7.5, "steel"),              # trigger housing
            box(M @ tr(0, 3.4, 6.0), 0.5, 1.0, 1.2, "steel"),               # sight
            box(M @ tr(0, 0.0, -12.0), 3.0, 3.0, 0.9, "wood")]              # shoulder stop


def bazooka_overlay(M, kind):
    if kind != "blast":
        return []
    front = [ell(M @ tr(0, 0, 25.5), 4.2, 4.2, 5.8, "flash_edge"),
             ell(M @ tr(0, 0, 25.2), 3.0, 3.0, 4.6, "flash"),
             ell(M @ tr(0, 0, 24.6), 1.7, 1.7, 2.6, "flash_core")]
    back = [ell(M @ tr(0, 0.5, -21.5), 4.6, 4.2, 5.4, "smoke_c"),
            ell(M @ tr(0, 0.8, -21.0), 3.6, 3.2, 4.4, "smoke_b"),
            ell(M @ tr(0, 1.0, -20.0), 2.4, 2.2, 3.2, "smoke_a"),
            ell(M @ tr(0, 0.5, -19.0), 1.6, 1.6, 2.0, "flash")]
    return back + front


BAZOOKA = Weapon(bazooka_prims, gripR=(0, -5.4, -3.0), gripL=(0, -5.2, 8.0), overlay=bazooka_overlay)


def pose_for(name):
    low = dict(pos=(-2.0, 9.0, 9.0), yaw=30, pitch=26)
    kind, v = parse(name)
    if name == "idle":
        return dict(hipL=10, hipR=-2, kneeL=18, kneeR=8, lean=5, spread=6, weapon=low, handR="grip", handL="grip", push=4)
    if kind == "walk":
        L = walk_legs(v)
        pas = v in (2, 4)
        return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"],
                    twist=L["twist"], lean=3, head_pitch=-3, lift=3,
                    weapon=dict(pos=(-2.0, 9.0 + (1.0 if pas else 0.0), 9.0), yaw=30, pitch=26 + (8 if pas else (4 if v == 1 else -4))),
                    handR="grip", handL="grip", push=4)
    if kind in ("aim", "fire"):
        st = dict(STANCE[v])
        lean = st["lean"] - (2 if kind == "fire" else 0)
        base = dict(st, lean=lean, twist=8, head_pitch=3 - 0.7 * lean, head_yaw=-12,
                    weapon=dict(pos=(-10.5, 21.6 - (1.0 if v == "c" else 0), 9.0), yaw=0, pitch=lean),
                    handR="grip", handL="grip", poleL=(0.9, -1, 0.2), poleR=(-0.8, -1, -0.3), push=15)
        if kind == "fire":
            base["overlay"] = "blast"
        return base
    if name == "idle_alt":   # tube resting upright beside the right foot
        return dict(hipL=10, hipR=-2, kneeL=18, kneeR=8, lean=3, spread=6, head_pitch=-3,
                    weapon=dict(pos=(-14.5, -4.0, 5.0), yaw=0, pitch=90), handR=("t", (-12.5, 9.0, 5.0)),
                    handL=("sw", 4, 14, 6))
    if name in ("hit", "fall", "dead"):
        return death_pose(name, dict(pos=(-5, 6, 10), yaw=6, pitch=-10), dict(pos=(-9, 1, 10), yaw=10, pitch=-45), dict(pos=(-16, 5.0, 2), yaw=20, pitch=0))
    raise KeyError(name)


def frame(d, name):
    return render_figure(pose_for(name), inf.kit(), d, BAZOOKA)


if __name__ == "__main__":
    save_unit("at_infantry", frame)
    print("at_infantry done")
