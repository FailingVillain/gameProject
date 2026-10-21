"""Officer pose set on the shared infantry body: pistol (holstered unless drawn) + sabre (scabbard at the left hip).
Frames: idle (hand on hilt), walk_1-4, aim_a/fire_a (standing pistol), aim_c/fire_c (kneeling pistol), idle_alt (pointing
with the sabre), charge_1-4 (sabre raised run), melee_1-3 (slash), hit/fall/dead, stagger_b/kneel_b/dead_b, stagger_c/slump_c/dead_c.
Pose key `use` = which weapon of the unit is in hand ("pistol" | "sabre" | None); the other stays holstered / sheathed (gear)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from poses import parse, walk_legs, quick_legs, STANCE  # noqa: E402
from voxel import death_pose  # noqa: E402
import bases.infantry as inf  # noqa: E402
import tune  # noqa: E402

import math
from voxel import SHOULDER_X, SHOULDER_Y, UPPER, LOWER

REACH = (UPPER + LOWER) * 0.995                  # hand distance from the shoulder for a straight arm
GRIP_Z = 2.6                                     # sabre grip point is 2.6 behind the guard (blades.SABRE.gripR)


def _dir(elev, azim):
    """unit vector in torso coords: elev = angle above horizontal, azim = 0 forward, +90 = to the right (-x)."""
    e, a = math.radians(elev), math.radians(azim)
    return (-math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e))


def edge_dir(w):
    """cutting-edge direction (torso coords) of a sabre placed by weapon spec w. The blade curves toward local +y, so the
    convex (cutting) side faces local -y; roll spins it about the blade axis."""
    from voxel import _frame_mat, pt
    import numpy as np
    M = _frame_mat(w)
    return pt(M, (0, -1, 0)) - pt(M, (0, 0, 0))


def edge_toward(w, cut):
    """return w with roll 0 or 180 so that the cutting edge faces the cut direction `cut` (torso coords)."""
    import numpy as np
    w = dict(w, roll=0)
    if float(np.dot(edge_dir(w), np.asarray(cut, float))) < 0:
        w["roll"] = 180
    return w


def straight_arm(elev, azim, b_elev, b_azim, side=-1):
    """right arm fully extended toward (elev, azim); sabre blade pointing toward (b_elev, b_azim).
    -> weapon frame dict (pos/yaw/pitch) placing the grip exactly in the extended hand."""
    u = _dir(elev, azim); b = _dir(b_elev, b_azim)
    hand = (side * SHOULDER_X + REACH * u[0], SHOULDER_Y + REACH * u[1], REACH * u[2])
    pos = (hand[0] + GRIP_Z * b[0], hand[1] + GRIP_Z * b[1], hand[2] + GRIP_Z * b[2])
    pitch = math.degrees(math.asin(max(-1, min(1, b[1]))))
    yaw = math.degrees(math.atan2(b[0], b[2]))
    return dict(pos=pos, yaw=yaw, pitch=pitch)
PISTOL_DROP = dict(pos=(-13.0, 1.0, 4.0), yaw=40, pitch=0)


def _T():
    return tune.get("poses", "officer")           # all tunables: tune/poses.json -> "officer"


def _hilt():
    return ("t", _T()["hilt_hand"])                # left hand resting on the sabre hilt


def walk_pose(i):
    L = walk_legs(i)
    return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"],
                lean=3, head_pitch=-2, lift=3, use=None,
                handL=("sw", L["sw"], 12, 10), handR=("sw", -L["sw"], 12, 6))


def quick_pose(i):
    """walk_quick_1-4: brisk march, longer stride + forward lean, arms pumped harder (derived from walk_pose)."""
    L = quick_legs(i)
    return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"],
                lean=L["lean"], head_pitch=-4, lift=L["lift"], use=None,
                handL=("sw", L["sw"], 14, 12), handR=("sw", -L["sw"], 14, 8))


def pistol_pose(var, fire):
    st = dict(STANCE[var])
    lean = st["lean"] - (2 if fire else 0)
    P = _T()["pistol"]
    y = (P["y_stand"] if var == "a" else P["y_kneel"]) + (P["fire_dy"] if fire else 0)
    z = P["z_fire"] if fire else P["z_aim"]
    d = dict(st, lean=lean, twist=14, head_yaw=-8, head_pitch=2 - 0.5 * lean, use="pistol",
             weapon=dict(pos=(P["x"], y, z), yaw=P["yaw"], pitch=lean + (7 if fire else 1)),
             handR="grip", poleR=(-0.5, -1, -0.2), push=8,
             handL=_hilt() if var == "a" else ("t", (6.5, 1.5, 7.5)))                # kneeling: left hand on the raised knee
    if fire:
        d["overlay"] = "flash"
    return d


# cutting-edge direction per frame (torso coords): the convex side of the curve must face the way the blade cuts
CUT = {"charge": (0, 0.2, 1), "melee_1": (0, 0, 1), "melee_2": (0, -0.5, 0.85), "melee_3": (0, -0.3, 1), "idle_alt": (0, 0, 1)}


def charge_pose(i):
    legs = {1: dict(hipL=34, hipR=-30, kneeL=14, kneeR=72, twist=-7, lift=3, sw=-26),
            2: dict(hipL=4, hipR=22, kneeL=24, kneeR=88, twist=0, lift=5, sw=0),
            3: dict(hipL=-30, hipR=34, kneeL=72, kneeR=14, twist=7, lift=3, sw=26),
            4: dict(hipL=22, hipR=4, kneeL=88, kneeR=24, twist=0, lift=5, sw=0)}[i]
    sw = legs.pop("sw")
    bob = 1.0 if i in (2, 4) else 0.0
    return dict(legs, lean=10, head_pitch=-8, spread=5, use="sabre",
                weapon=edge_toward(straight_arm(*_T()["charge_arm"]), CUT["charge"]),             # arm fully extended up / a little right, edge toward the enemy
                handR="grip", poleR=(-0.9, -0.2, -0.6), handL=("sw", sw, 14, 8), push=2)


# slash from the shoulder: the sword arm stays fully extended (straight_arm) through wind-up -> slash -> recover.
# Arms [arm_elev, arm_azim, blade_elev, blade_azim] per frame: tune/poses.json -> officer.melee_arms
def _melee(frame):
    arm = _T()["melee_arms"][frame]
    w = edge_toward(straight_arm(*arm), CUT[frame])
    return {
        "melee_1": dict(hipL=18, hipR=-12, kneeL=20, kneeR=12, spread=8, lean=-4, twist=-20, head_pitch=0, use="sabre",   # wind-up: arm back-up-right
                        weapon=w, handR="grip", poleR=(-0.9, -0.3, -0.6), handL=("sw", 30, 20, 12), push=2),
        "melee_2": dict(hipL=36, hipR=-22, kneeL=40, kneeR=8, spread=8, lean=14, twist=18, head_pitch=-2, use="sabre",    # slash: arm forward-down across to the left
                        weapon=w, handR="grip", poleR=(-0.4, -1, 0.3), handL=("sw", 60, 30, 20), push=6),
        "melee_3": dict(hipL=22, hipR=-16, kneeL=26, kneeR=12, spread=8, lean=4, twist=2, head_pitch=0, use="sabre",       # recover: arm down-forward, blade low
                        weapon=w, handR="grip", poleR=(-0.7, -1, -0.2), handL=("sw", 10, 15, 8), push=2),
    }[frame]


MELEE_FRAMES = ("melee_1", "melee_2", "melee_3")

# sabre swept overhead to rally / direct troops (loop 1-2-3-4-3-2): arm fully extended up-and-back (hilt behind / above the head
# so the sabre never crosses the head graphic), blade low -> a wide, flat sweep right -> back -> left.
# The cutting edge faces the sweep direction (edge_toward). Numbers: tune/poses.json -> officer.wave / officer.wave_side.
# Back views (ne / n) use wave_side: the sweep moves out to the right side of the head so the blade never crosses it.
WAVE_FRAMES = ("wave_1", "wave_2", "wave_3", "wave_4")
WAVE_SIDE_DIRS = ("ne", "n")


def wave_pose(k, side=False):
    """sweep frame k (1-4): blade azimuth starts at az0 and advances step_deg per frame."""
    T = _T()
    W = T["wave"]
    S = T["wave_side"] if side else W
    arm = S["arm"]
    step = W["step_deg"]
    a_k = S["az0"] + step * (k - 1)
    a0, a1 = math.radians(a_k), math.radians(a_k + step)
    tangent = (-(math.sin(a1) - math.sin(a0)), 0.0, math.cos(a1) - math.cos(a0))       # tip travel direction
    elev = S["blade_elev"]
    return dict(hipL=6, hipR=-8, kneeL=6, kneeR=8, spread=9, lean=0, twist=W["twist"][k - 1], head_pitch=0, use="sabre",
                weapon=edge_toward(straight_arm(arm[0], arm[1], elev, a_k), tangent),
                handR="grip", poleR=(-0.9, -0.2, 0.4), handL=("sw", 16, 24, 14), push=2)


def _idle():
    return dict(hipL=4, hipR=-4, kneeL=4, kneeR=6, spread=7, lean=2, twist=-3, head_yaw=4, use=None,
                handL=_hilt(), handR=("sw", 0, 8, 6))

def _idle_alt():
    return dict(hipL=6, hipR=-4, kneeL=5, kneeR=5, spread=8, lean=1, twist=3, head_pitch=5, head_yaw=-3, use="sabre",   # at ease, sabre drawn, tip resting on the ground in front
                weapon=edge_toward(_T()["idle_alt_sword"], CUT["idle_alt"]), handR="grip", poleR=(-0.6, -1, 0.3),
                handL=("sw", 4, 10, 6), push=2)

# extra deaths: infantry poses with the rifle removed (pistol dropped in the final frames)
DEATHS = {
    "stagger_b": dict(inf.DEATHS["stagger_b"], use=None, weapon=None, handR=("sw", 30, 40, 16)),
    "kneel_b": dict(inf.DEATHS["kneel_b"], use=None, weapon=None, handR=("sw", 12, 30, 12)),
    "dead_b": dict(inf.DEATHS["dead_b"], use="pistol", drop=dict(pos=(-14.0, 1.0, 8.0), yaw=-22, pitch=0)),
    "stagger_c": dict(inf.DEATHS["stagger_c"], use=None, weapon=None, handR=("sw", 50, 20, 50), roll=-2),   # less roll: scabbard clears the left thigh
    "slump_c": dict(inf.DEATHS["slump_c"], use=None, weapon=None, handR=("sw", 80, 10, 55)),
    "dead_c": dict(inf.DEATHS["dead_c"], use="pistol", drop=dict(pos=(-12.0, 1.0, 1.0), yaw=72, pitch=0)),
}


def pose_for(frame, d="s", e_extra=0.0):
    if frame in WAVE_FRAMES and d in WAVE_SIDE_DIRS:
        return inf.apply_e_view(wave_pose(int(frame[-1]), side=True), d, e_extra)
    return inf.apply_e_view(_tuned(frame, _pose(frame)), d, e_extra)


def _tuned(frame, p):
    """e_pull per group (pistol aim/fire, charge, melee_2) from tune/poses.json -> officer.e_pull"""
    pull = _T()["e_pull"]
    key = "pistol" if frame.startswith(("aim_", "fire_")) else "charge" if frame.startswith("charge_") else frame if frame == "melee_2" else None
    if key in pull:
        p["e_pull"] = pull[key]
    return p


def _pose(frame):
    if frame in ("idle", "spare"):
        return _idle()
    if frame == "idle_alt":
        return _idle_alt()
    if frame in MELEE_FRAMES:
        return _melee(frame)
    if frame in WAVE_FRAMES:
        return wave_pose(int(frame[-1]))
    if frame in DEATHS:
        return dict(DEATHS[frame])
    if frame.startswith("charge_"):
        return charge_pose(int(frame.split("_")[1]))
    if frame in ("hit", "fall", "dead"):
        p = death_pose(frame, dict(pos=(-7, 9, 10), yaw=6, pitch=-20), dict(pos=(-9, 2, 10), yaw=10, pitch=-45), PISTOL_DROP)
        p["use"] = "pistol"
        return p
    if frame.startswith("walk_quick_"):
        return quick_pose(int(frame.rsplit("_", 1)[1]))
    kind, v = parse(frame)
    if kind == "walk":
        return walk_pose(v)
    if kind in ("aim", "fire"):
        return pistol_pose(v, kind == "fire")
    raise KeyError(frame)
