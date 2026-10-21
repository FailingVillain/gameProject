"""US scout / recon (key: scout) - lighter kit, M1 carbine, radio + antenna, binoculars.
Run: python tools/vox_scout.py -> out/scout/*
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import save_sheet  # noqa: E402
from voxel import (save_unit, Kit, Weapon, box, ell, cyl, seg, along_z, tr, rx, render_figure)  # noqa: E402
import vox_infantry as inf  # noqa: E402
from poses import parse  # noqa: E402
from voxel import rbox as box  # noqa: E402


def radio_back(T):
    p = [box(T @ tr(-1.5, 10.5, -8.2), 5.6, 7.2, 3.0, "olive"),               # radio set
         box(T @ tr(-1.5, 15.2, -8.4), 4.8, 1.2, 3.1, "steel"),               # top plate
         box(T @ tr(5.8, 8.5, -7.0), 1.6, 3.2, 1.8, "canvas")]                # small pouch
    # antenna: thin cylinder pointing up-back
    a0 = (T @ [-4.0, 17.0, -9.0, 1.0])[:3]
    a1 = (T @ [-4.4, 37.0, -11.0, 1.0])[:3]
    p.append(seg(a0, a1, 0.55, "steel", flat=True))
    for s in (1, -1):
        p.append(box(T @ tr(s * 5.4, 11.0, -4.8), 1.0, 5.4, 0.8, "leather"))
    return p


def binocular_case(T):
    return [box(T @ tr(4.5, 7.0, 6.9), 2.8, 3.0, 1.6, "leather"),
            box(T @ tr(4.5, 10.2, 6.9), 0.7, 0.4, 1.7, "leather")]


def carbine_prims(M):
    return [box(M @ tr(0, -0.5, -10.5), 1.5, 2.3, 6.5, "wood"),               # stock
            box(M @ tr(0, -1.2, -17.2), 1.4, 2.8, 0.9, "wood"),
            box(M @ tr(0, -0.4, -6.4), 1.6, 2.0, 2.0, "wood"),                # wrist
            box(M @ tr(0, 0.5, -2.0), 1.6, 1.7, 3.4, "wood"),                # receiver
            box(M @ tr(0, -0.3, 6.2), 1.6, 1.6, 4.6, "wood"),                 # handguard
            box(M @ tr(0, -3.2, -0.4), 0.8, 1.8, 0.9, "wood"),               # magazine
            along_z(M, 0.95, 2.0, 17.0, "steel"),
            box(M @ tr(0, 1.6, 15.5), 0.4, 0.7, 0.5, "steel")]


def carbine_overlay(M, kind):
    if kind != "flash":
        return []
    z = 18.0
    return [ell(M @ tr(0, 0.4, z + 2.4), 3.8, 3.8, 5.0, "flash_edge"),
            ell(M @ tr(0, 0.4, z + 2.2), 2.7, 2.7, 4.0, "flash"),
            ell(M @ tr(0, 0.4, z + 1.8), 1.6, 1.6, 2.4, "flash_core")]


CARBINE = Weapon(carbine_prims, gripR=(0, -2.2, -5.0), gripL=(0, -1.8, 6.0), overlay=carbine_overlay)


def binoc_prims(M):
    p = []
    for s in (1, -1):
        p.append(along_z(M @ tr(s * 2.6, 0, 0), 1.9, -3.5, 4.0, "steel"))
        p.append(along_z(M @ tr(s * 2.6, 0, 4.0), 2.3, 0.0, 1.6, "steel"))     # objective bell
    p.append(box(M @ tr(0, 0, 0), 1.6, 0.9, 3.0, "steel"))
    return p


BINOC = Weapon(binoc_prims, gripR=(-4.4, -0.8, 0.0), gripL=(4.4, -0.8, 0.0))


def kit():
    return Kit(jacket="scoutj", trousers="khaki", legging="canvas", back=radio_back, front=binocular_case)


def frame(d, name):
    pose = inf.pose_for(name)
    if name == "idle_alt":
        pose = dict(hipL=3, hipR=-3, kneeL=2, kneeR=2, lean=-1, head_pitch=-4,
                    weapon=dict(pos=(0.0, 22.8, 8.0), yaw=0, pitch=0), handR="grip", handL="grip",
                    poleR=(-1, -0.5, -0.2), poleL=(1, -0.5, -0.2),
                    slung=dict(pos=(-9.5, 8.0, -4.5), yaw=0, pitch=82))
        return render_figure(pose, kit(), d, BINOC, slung=CARBINE)
    return render_figure(pose, kit(), d, CARBINE)


if __name__ == "__main__":
    save_unit("scout", frame)
    print("scout done")
