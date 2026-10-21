"""Assault-trooper pose set: Mosin M1907/1910-style carbine ("carbine", cav_carbine part; rifle-family poses): shoulder-arms carry for idle / walk (left hand on hilt), two-handed for fire, in hand for death, sabre ("sabre", shorter blade)
for charge + melee. Charge and melee REUSE the officer's poses (bases/officer.py charge_pose, MELEE incl. edge_toward /
CUT edge-direction rule); the SMG is then carried slung on the back (pose keys slung_use / slung). Sabre is otherwise
sheathed (scabbard gear). Frames: idle, walk_1-4, aim_a/fire_a (standing), aim_c/fire_c (kneeling), hit/fall/dead,
charge_1-4, melee_1-3, stagger_b/kneel_b/dead_b, stagger_c/slump_c/dead_c."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from poses import parse, walk_legs  # noqa: E402
from voxel import death_pose  # noqa: E402
import bases.infantry as inf  # noqa: E402
import bases.officer as off  # noqa: E402

SLING = dict(pos=(0.0, 11.0, -9.4), yaw=38, pitch=52)       # SMG hung muzzle-down across the back


# Carry (idle + walk): SMG at "shoulder arms" -- right hand on the wrist, receiver at the right chest, muzzle up and a little
# back so the barrel rests over the right shoulder; left hand rests on the sabre hilt (off._hilt()), so the hilt stays visible.
CARRY_HAND = (-8.6, 9.6, 8.2)       # right hand (torso coords) on the wrist grip
CARRY_PITCH, CARRY_YAW = 96, -4


def _carry_weapon(dy=0.0, dz=0.0):
    """weapon frame whose right grip (smg.MP18.gripR) sits at CARRY_HAND (+dy/dz bob)."""
    import numpy as np
    from voxel import _frame_mat, pt
    from parts.horse import CAV_CARBINE as MP18
    spec = dict(pos=(0, 0, 0), yaw=CARRY_YAW, pitch=CARRY_PITCH)
    g = pt(_frame_mat(spec), MP18.gripR)
    h = np.array(CARRY_HAND) + (0, dy, dz)
    return dict(spec, pos=tuple(float(v) for v in (h - g)))


def walk_pose(i):
    L = walk_legs(i)
    passing = i in (2, 4)
    return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"],
                lean=3, head_pitch=-3, lift=3, use="carbine", weapon=_carry_weapon(0.5 if passing else 0.0),
                handR="grip", handL=off._hilt(), poleR=(-0.6, -1, -0.3), poleL=(0.9, -1, 0.2))


def _idle():
    return dict(hipL=10, hipR=-2, kneeL=14, kneeR=8, lean=4, spread=6, head_yaw=3, use="carbine",
                weapon=_carry_weapon(), handR="grip", handL=off._hilt(),
                poleR=(-0.6, -1, -0.3), poleL=(0.9, -1, 0.2))


def shoot_pose(var, fire):
    d = inf.shoot_pose(var, fire)
    x, y, z = d["weapon"]["pos"]
    d["weapon"] = dict(d["weapon"], pos=(x, y, z - 1.0))        # carbine is a bit shorter than the rifle: keep the butt in the shoulder
    d["use"] = "carbine"
    d["e_pull"] = 0.5
    d["e_swing"] = inf.WALKFIRE_SWING
    return d


def shoulder_pose(i):
    from parts.horse import CAV_CARBINE as MP18
    d = inf.shoulder_pose(i, MP18)
    d["handL"] = off._hilt()                          # left hand on the sabre hilt (as in the walk): keeps the arm off the scabbard
    d["use"] = "carbine"
    return d


def walkfire_pose(fire, i=0):
    d = inf.walkfire_pose(i, fire, dz=-0.5, dy=0.0)           # SMG is shorter than the rifle: keep the butt at the hip/shoulder
    d["use"] = "carbine"
    d["e_pull"] = 0.5
    d["e_swing"] = inf.WALKFIRE_SWING
    return d


def charge_pose(i):
    return dict(off._tuned("charge_%d" % i, off.charge_pose(i)), slung_use="carbine", slung=SLING)


def melee_pose(frame):
    return dict(off._tuned(frame, off._pose(frame)), slung_use="carbine", slung=SLING)


DEATHS = {k: dict(v, use="carbine") for k, v in inf.DEATHS.items() if k.endswith(("_b", "_c"))}


def pose_for(frame, d="s", e_extra=0.0):
    return inf.apply_e_view(_pose(frame), d, e_extra)


def _pose(frame):
    if frame == "idle":
        return _idle()
    if frame in off.MELEE_FRAMES:
        return melee_pose(frame)
    if frame.startswith("charge_"):
        return charge_pose(int(frame.split("_")[1]))
    if frame in ("pistol_aim_a", "pistol_fire_a"):          # close range: pistol drawn from the holster, carbine slung on the back
        return dict(off.pistol_pose("a", frame.endswith("fire_a")), slung_use="carbine", slung=SLING)
    if frame.startswith("grenade_"):                       # stick grenade in hand, MP18 slung on the back (infantry_motions.grenade)
        from bases import infantry_motions as _m
        return dict(_m.grenade(int(frame[-1])), slung_use="carbine", slung=SLING)
    if frame in DEATHS:
        return dict(DEATHS[frame])
    if frame in ("hit", "fall", "dead"):
        p = death_pose(frame, dict(pos=(-9.6, 5, 12.0), yaw=14.3, pitch=-7.9), dict(pos=(-9.4, -0.6, 9.0), yaw=7.2, pitch=-37.6),
                       dict(pos=(-15, 3.0, 2), yaw=25, pitch=0))
        p["use"] = "carbine"
        return p
    if frame.startswith("walk_shoulder_"):
        return shoulder_pose(int(frame.rsplit("_", 1)[1]))
    if frame in ("walkfire_a", "walkfire_b"):
        return walkfire_pose(frame == "walkfire_b")
    if frame.startswith("walk_fire_"):
        i = int(frame.rsplit("_", 1)[1])
        return walkfire_pose(i in (2, 4), i)
    kind, v = parse(frame)
    if kind == "walk":
        return walk_pose(v)
    if kind in ("aim", "fire"):
        return shoot_pose(v, kind == "fire")
    raise KeyError(frame)
