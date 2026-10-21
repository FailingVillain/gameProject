"""US combat engineer (key: worker, 공병) - shovel, tool bag, no rifle.
Run: python tools/vox_worker.py -> out/worker/*
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import save_sheet  # noqa: E402
from voxel import save_unit, death_pose, Kit, Weapon, box, ell, cyl, seg, along_z, tr, rx, render_figure  # noqa: E402
from voxel import rbox as box  # noqa: E402
from poses import parse, walk_legs  # noqa: E402


def bag_back(T):
    p = [box(T @ tr(0, 10.0, -8.2), 6.8, 5.2, 2.9, "canvas"),
         box(T @ tr(0, 14.6, -8.2), 7.4, 1.4, 3.2, "canvas"),
         cyl(T @ tr(-4.0, 10.4, -11.6) @ rx(90), 1.8, 0.8, "olive")]
    for s in (1, -1):
        p.append(box(T @ tr(s * 5.2, 10.5, -4.6), 1.0, 5.2, 0.8, "leather"))
    return p


def tool_bag(T):
    return [box(T @ tr(12.4, -1.0, 1.0), 3.0, 5.2, 5.4, "canvas"),           # tool bag at hip
            box(T @ tr(12.4, 3.8, 1.0), 3.3, 1.2, 5.8, "canvas"),            # flap
            box(T @ tr(12.9, 2.8, 6.6), 0.8, 1.0, 0.6, "leather")]


def shovel_prims(M):
    p = [along_z(M, 1.05, -13.0, 13.0, "wood"),                              # haft
         box(M @ tr(0, 0, -13.5), 2.6, 0.8, 0.9, "wood"),                    # D grip
         along_z(M, 1.5, 10.0, 15.0, "steel"),                               # socket
         box(M @ tr(0, 0.2, 19.5), 4.0, 0.55, 5.0, "steel"),                 # blade
         box(M @ tr(0, 0.9, 15.8), 0.8, 0.6, 2.0, "steel")]                  # spine
    return p


SHOVEL = Weapon(shovel_prims, gripR=(0, -0.4, -11.0), gripL=(0, -0.4, -3.5))


def kit():
    return Kit(jacket="hbt", trousers="hbtp", legging="hbtp", hand="skin", belt="leather",
               back=bag_back, hip=tool_bag)


def walk_pose(i):
    L = walk_legs(i, bend=40)
    return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"],
                twist=L["twist"], lean=3, head_pitch=-3, lift=3,
                weapon=dict(pos=(-9.0, 17.0 + (1.0 if i in (2, 4) else 0.0), -2.8), yaw=180, pitch=33 + (4 if i in (2, 4) else 0)),
                handR="grip", handL=("sw", L["sw"], 12, 6))


def pose_for(name):
    kind, v = parse(name)
    if name == "idle":
        return dict(hipL=10, hipR=-2, kneeL=18, kneeR=8, lean=5, spread=6,
                    weapon=dict(pos=(-9.0, 17.0, -2.8), yaw=180, pitch=33), handR="grip", handL=("sw", 3, 14, 6))
    if kind == "walk":
        return walk_pose(v)
    both = dict(handR="grip", handL="grip", poleR=(-1, -0.4, -0.2), poleL=(1, -0.4, -0.2), spread=8)
    if (kind, v) == ("aim", "a"):      # A overhead chop: raise, shovel back over the shoulder
        return dict(both, hipL=8, hipR=-10, kneeL=6, kneeR=6, lean=-6, twist=-8, head_pitch=-4,
                    weapon=dict(pos=(-4.0, 17.0, -1.0), yaw=180, pitch=22))
    if (kind, v) == ("fire", "a"):     # strike: lean in, shovel driven down in front
        return dict(both, hipL=14, hipR=-12, kneeL=22, kneeR=8, lean=20, twist=6, head_pitch=6,
                    weapon=dict(pos=(-3.0, 2.0, 12.0), yaw=0, pitch=-42), push=6, lift=8)
    if (kind, v) == ("aim", "b"):      # B side sweep: wind up to the right, blade held out wide behind
        return dict(both, hipL=6, hipR=-8, kneeL=14, kneeR=14, lean=6, twist=-34, head_pitch=2, head_yaw=20,
                    weapon=dict(pos=(-14.0, 10.0, 2.0), yaw=-70, pitch=8), push=4)
    if (kind, v) == ("fire", "b"):     # swept across to the left
        return dict(both, hipL=12, hipR=-10, kneeL=18, kneeR=10, lean=8, twist=34, head_pitch=3, head_yaw=-20,
                    weapon=dict(pos=(4.0, 9.0, 10.0), yaw=70, pitch=-4), push=4)
    if (kind, v) == ("aim", "c"):      # C dig: blade lifted forward, crouched
        return dict(both, hipL=40, hipR=8, kneeL=64, kneeR=50, lean=12, twist=0, head_pitch=4,
                    weapon=dict(pos=(-3.0, 7.0, 16.0), yaw=0, pitch=-8), push=4)
    if (kind, v) == ("fire", "c"):     # stab the blade down into the ground ahead
        return dict(both, hipL=46, hipR=6, kneeL=74, kneeR=56, lean=22, twist=0, head_pitch=8,
                    weapon=dict(pos=(-3.0, 0.0, 17.0), yaw=0, pitch=-68), push=4)
    if name == "idle_alt":  # lean on the shovel planted in front
        return dict(hipL=4, hipR=-5, kneeL=2, kneeR=4, lean=7, twist=5, head_pitch=6, head_yaw=-6,
                    weapon=dict(pos=(-3.0, -3.0, 13.0), yaw=0, pitch=-88),
                    handR=("t", (-4.0, 12.0, 12.0)), handL=("t", (-2.0, 12.0, 12.2)),
                    poleR=(-1, -0.6, -0.2), poleL=(1, -0.6, -0.2))
    if name in ("hit", "fall", "dead"):
        return death_pose(name, dict(pos=(-8, 8, 8), yaw=10, pitch=-25), dict(pos=(-9, 2, 8), yaw=10, pitch=-50), dict(pos=(-16, 1.6, 4), yaw=20, pitch=0))
    raise KeyError(name)


def frame(d, name):
    p = pose_for(name); p.setdefault('lift', 3)
    return render_figure(p, kit(), d, SHOVEL)


if __name__ == "__main__":
    save_unit("worker", frame)
    print("worker done")
