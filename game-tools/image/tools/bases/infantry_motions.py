"""Infantry motion drafts (batch 1): idle variants + ceremony poses. Pure data/helpers, called from infantry.py `_pose`.
Frames: idle_rest_1/2 (rifle grounded, leaning on it), idle_look_1/2 (scanning, hand shading the eyes),
idle_adjust_1/2 (settling the cap), idle_stretch_1/2 (stretch, rifle grounded), salute, present_arms.
The rifle is placed by two helpers: `_stand_rifle` (butt on the ground point, muzzle up) and `_hand_on` (point on the rifle axis
-> hand target), so the grounded rifle stays on the floor whatever the body lean is."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import _frame_mat, pt, tr, rx, ry, rz  # noqa: E402

GROUND_ROOT_Y = -26.2          # ground height in the root frame (feet flat, legs near straight)
BUTT = (0.0, -1.4, -19.3)      # bolt_rifle butt-plate centre (weapon frame)
LEGS = dict(hipL=3, hipR=-3, kneeL=3, kneeR=4, spread=7)


def _to_torso(lean, twist, p_root):
    """root-frame point -> torso frame (Tm = tr(0,4.2,0) @ rx(lean) @ ry(twist))."""
    T = tr(0, 4.2, 0) @ rx(lean) @ ry(twist)
    return pt(np.linalg.inv(T), p_root)


def _stand_rifle(x, z, lean=0.0, twist=0.0, pitch=90.0, yaw=0.0, roll=0.0, sink=0.0):
    """weapon spec with the butt plate on the ground at root-frame (x, z); pitch 90 = vertical, muzzle up."""
    g = _to_torso(lean, twist, (x, GROUND_ROOT_Y + sink, z))
    spec = dict(pos=(0, 0, 0), yaw=yaw, pitch=pitch, roll=roll)
    b = pt(_frame_mat(spec), BUTT)
    spec["pos"] = tuple(float(v) for v in (g - b))
    return spec


def _hand_on(spec, z, y=0.0, x=0.0):
    """torso-frame point on the rifle at weapon-local (x, y, z)."""
    return tuple(float(v) for v in pt(_frame_mat(spec), (x, y, z)))


def _head_pt(local, head_pitch=0.0, head_yaw=0.0, head_roll=0.0):
    """torso-frame point from head-local coordinates (same chain as voxel.build_figure's Hm)."""
    Hm = tr(0, 19.6, 0.5) @ rx(head_pitch) @ ry(head_yaw) @ rz(head_roll)
    return tuple(float(v) for v in pt(Hm, local))


def rest(i):
    """idle_rest: rifle grounded in front of the right foot, both hands crossed on the muzzle, body leaning on it."""
    lean = (9, 7)[i - 1]
    w = _stand_rifle(-5.5, 11.5, lean=lean, pitch=(94, 96)[i - 1])
    hp = (10, 4)[i - 1]
    return dict(LEGS, hipL=(5, 2)[i - 1], hipR=(-4, -2)[i - 1], kneeL=(3, 7)[i - 1], kneeR=(5, 3)[i - 1], lean=lean, twist=(-4, -2)[i - 1],
                head_pitch=hp, head_yaw=(-6, 14)[i - 1], weapon=w,
                handR=("t", _hand_on(w, 22.0, 1.4, -0.4)), handL=("t", _hand_on(w, 22.0, 3.1, 0.6)),
                poleR=(-0.9, -0.5, 0.2), poleL=(0.9, -0.6, 0.3))


def look(i):
    """idle_look: right hand on the grounded rifle, left hand flat over the brow, head turning left (1) / right (2)."""
    hy = (24, -22)[i - 1]
    hp = -2
    w = _stand_rifle(-13.0, 7.0, pitch=88)
    hand = _head_pt((2.6, 6.8, 6.4), hp, hy)
    return dict(LEGS, hipL=(6, 3)[i - 1], hipR=(-3, -5)[i - 1], kneeL=5, kneeR=3, lean=2, twist=(10, -8)[i - 1], head_pitch=hp, head_yaw=hy,
                weapon=w, handR=("t", _hand_on(w, 14.0, 0.0, 0.0)), handL=("t", hand),
                poleR=(-0.9, -0.9, 0.2), poleL=(1.0, 0.1, -0.4))


def adjust(i):
    """idle_adjust: left hand grounds the rifle at the side, right hand up at the cap (1 reaching, 2 tugging the brim, head dipped)."""
    hp = (-2, 6)[i - 1]
    w = _stand_rifle(13.5, 6.0, pitch=88)
    hand = _head_pt([(-3.8, 8.4, 5.2), (-4.2, 6.6, 5.4)][i - 1], hp)
    return dict(LEGS, lean=1, head_pitch=hp, head_yaw=(4, -4)[i - 1], weapon=w,
                handL=("t", _hand_on(w, 14.0)), handR=("t", hand),
                poleL=(0.9, -0.9, 0.2), poleR=(-1.0, 0.1, -0.5))


def stretch(i):
    """idle_stretch: right hand steadies the grounded rifle, left arm stretches overhead, back arched, head back."""
    w = _stand_rifle(-13.0, 7.0, lean=(-4, -9)[i - 1], pitch=88)
    return dict(LEGS, hipL=(4, 6)[i - 1], hipR=(-4, -6)[i - 1], spread=(8, 10)[i - 1], lean=(-4, -9)[i - 1], head_pitch=(-8, -18)[i - 1],
                head_yaw=0, weapon=w, handR=("t", _hand_on(w, 14.0)), handL=("sw", (95, 160)[i - 1], (20, 6)[i - 1], (22, 14)[i - 1]),
                poleR=(-0.9, -0.9, 0.2))


def salute():
    """salute: attention, right hand to the cap brim (elbow out), left hand holds the grounded rifle at the side."""
    w = _stand_rifle(13.5, 6.0, pitch=89)
    hand = _head_pt((-4.6, 6.4, 6.6))
    return dict(hipL=1, hipR=-1, kneeL=1, kneeR=1, spread=4, lean=-1, head_pitch=0, weapon=w,
                handL=("t", _hand_on(w, 14.0)), handR=("t", hand), poleL=(0.9, -0.9, 0.2), poleR=(-1.0, 0.3, -0.4))


def present_arms():
    """present_arms: rifle upright in front of the chest, right hand at the wrist, left hand at the fore-stock."""
    w = dict(pos=(0, 0, 0), yaw=0, pitch=90.0)
    b = pt(_frame_mat(w), BUTT)
    w["pos"] = (-1.8 - b[0], -9.0 - b[1], 10.2 - b[2])
    return dict(hipL=1, hipR=-1, kneeL=1, kneeR=1, spread=4, lean=0, head_pitch=0, weapon=w,
                handR=("t", _hand_on(w, -4.5)), handL=("t", _hand_on(w, 8.0, 0.0, 0.0)),
                poleR=(-0.8, -1.0, -0.2), poleL=(0.8, -1.0, -0.3))


def surrender(i):
    """surrender_1/2 (N79b, 만세): rifle thrown down on the ground beside the right foot, both arms raised high beside the head, palms open,
    elbows out, knees slightly bent, head tipped up and back; frame 2 is a small sway (hands a touch higher, head back) so the loop
    reads as trembling. weapon=None + drop (final ground coordinates, like the death poses)."""
    hl = ((8.5, 31.5, 2.5), (9.5, 33.0, 3.0))[i - 1]
    hr = ((-8.5, 31.5, 2.5), (-9.5, 33.0, 3.0))[i - 1]
    return dict(hipL=2, hipR=-2, kneeL=(9, 12)[i - 1], kneeR=(8, 11)[i - 1], spread=6, lean=(-2, -4)[i - 1], head_pitch=(-6, -10)[i - 1], head_yaw=(0, 0)[i - 1],
                weapon=None, drop=dict(pos=(15.0, 3.0, 7.0), yaw=25, pitch=0),
                handL=("t", hl), handR=("t", hr), poleL=(1.0, 0.2, -0.35), poleR=(-1.0, 0.2, -0.35))


# ---------------------------------------------------------------- batch 2: crawl, drag_wounded, grenade_throw, trench_fire
def _grip_at(weapon, hand, yaw=0.0, pitch=0.0, roll=0.0):
    """weapon spec (yaw/pitch/roll) whose right grip sits at torso-frame `hand`."""
    spec = dict(pos=(0, 0, 0), yaw=yaw, pitch=pitch, roll=roll)
    g = pt(_frame_mat(spec), weapon.gripR)
    spec["pos"] = tuple(float(v) for v in (np.array(hand, float) - g))
    return spec


def crawl(i):
    """crawl_1-3: low crawl on the belly, rifle held in both hands along the ground in front of the shoulder (as in aim_prone), body
    wriggling forward: the opposite knee is drawn up on the side that pushes (1 left knee up, 2 stretched / chest forward, 3 right knee up);
    the hands slide the rifle ahead (y), torso twists and the head turns with each push."""
    from bases import infantry as inf
    p = inf.prone_pose(False)
    legs = {1: (46, -4, 104, 8), 2: (6, 4, 78, 90), 3: (6, 24, 82, 100)}[i]          # hipL, hipR, kneeL, kneeR
    x, y, z = p["weapon"]["pos"]
    p.update(hipL=legs[0], hipR=legs[1], kneeL=legs[2], kneeR=legs[3], twist=(-5, 0, 5)[i - 1], head_yaw=(-14, -6, 4)[i - 1],
             lean=(0, 3, 0)[i - 1], spread=12)
    p["weapon"] = dict(p["weapon"], pos=(x, y + (-1.2, 1.2, 1.5)[i - 1], z))
    return inf._tuned("aim_prone", p)             # E-view rifle yaw / slide of the prone pose (keeps the long rifle inside the cell)


def grenade(i):
    """grenade_1-3 (right-handed stick grenade): wind-up (arm cocked behind the ear, grenade head back, left arm points at the target),
    throw (weight forward, arm whips over, grenade at release), follow-through (arm swung down across the body, empty hand)."""
    from parts.grenade import STICK_GRENADE as G
    if i == 1:
        w = _grip_at(G, (-13.5, 23.5, -4.0), yaw=180, pitch=22)
        return dict(hipL=18, hipR=-14, kneeL=12, kneeR=26, spread=8, lean=-9, twist=-24, head_pitch=-4, head_yaw=16, lift=3,
                    weapon=w, handR="grip", handL=("sw", 82, 14, 14), poleR=(-1.0, -0.2, -0.7), poleL=(0.9, -1, 0.2), use="grenade")
    if i == 2:
        w = _grip_at(G, (-8.5, 25.0, 14.0), yaw=0, pitch=38)
        return dict(hipL=34, hipR=-22, kneeL=26, kneeR=30, spread=8, lean=14, twist=16, head_pitch=2, head_yaw=-8, lift=3,
                    weapon=w, handR="grip", handL=("sw", -30, 30, 26), poleR=(-1.0, 0.4, 0.0), poleL=(0.9, -1, -0.2), use="grenade", push=4)
    return dict(hipL=38, hipR=-28, kneeL=34, kneeR=18, spread=8, lean=24, twist=22, head_pitch=6, head_yaw=-12, lift=3,
                weapon=None, handR=("t", (4.0, 3.0, 16.0)), handL=("sw", -40, 36, 18), poleR=(-0.3, -1.0, 0.4), poleL=(0.9, -1, -0.2), push=6)


def trench_fire(i):
    """trench_fire_1/2 (aim, fire): crouched deep, chest well forward over the rifle that rests on the parapet line (head low)."""
    from bases import infantry as inf
    p = inf.shoot_pose("b", i == 2)
    d_lean = 12.0
    new_lean = p["lean"] + d_lean
    # keep the rifle at the same place in the world: torso frame at the old lean -> torso frame at the new lean
    T_old = tr(0, 4.2, 0) @ rx(p["lean"])
    T_new = tr(0, 4.2, 0) @ rx(new_lean)
    spec = dict(p["weapon"])
    wp = pt(np.linalg.inv(T_new) @ T_old, spec["pos"])
    spec.update(pos=tuple(float(v) for v in wp), pitch=spec.get("pitch", 0) + d_lean)
    if i == 2:
        x, y, z = spec["pos"]; spec["pos"] = (x, y, z + 4.5)                   # recoil position of the fire frame clears the deeper chest
    p.update(lean=new_lean, weapon=spec, hipL=58, hipR=26, kneeL=96, kneeR=86, spread=10, head_pitch=p["head_pitch"] - 10)
    return inf._tuned("fire_b" if i == 2 else "aim_b", p)


def drag(i):
    """drag_1/2: the puller squats back on his heels, leaning away, both fists in the collar of the comrade (shoulders hauled
    off the ground, lying on his back, head lolling) and hauls him backwards; legs alternate. Two-figure scene (build_scene)."""
    puller = dict(lean=(9, 10)[i - 1], twist=(2, -2)[i - 1], head_pitch=-22, head_yaw=0, spread=10,
                  hipL=(86, 70)[i - 1], hipR=(70, 86)[i - 1], kneeL=(142, 124)[i - 1], kneeR=(124, 142)[i - 1], lift=2,
                  weapon=None, poleL=(0.9, -1.0, 0.1), poleR=(-0.9, -1.0, 0.1), push=0,
                  )
    wounded = dict(tilt=-36, lean=0, head_pitch=-16, head_yaw=-30, head_roll=14, spread=12,
                   hipL=(84, 90)[i - 1], hipR=(54, 48)[i - 1], kneeL=(120, 124)[i - 1], kneeR=(80, 66)[i - 1],
                   weapon=None, drop=dict(pos=(17.0, 1.0, -13.0), yaw=0, pitch=0),
                   handR=("sw", -8, 14, 38), handL=("sw", 26, 22, 44))
    puller["second"] = dict(pose=wounded, place=(0.0, (24.0, 23.5)[i - 1], 0.0), scene_yaw=36.0,
                            grab=[(7.6, 19.4, 5.2), (-7.6, 19.4, 5.2)], hands=("handL", "handR"))
    return puller


DEBUG = {}


def build_scene(pose, kit, weapon=None, slung=None):
    """two-figure scene (drag_wounded): `pose` is the puller's pose + `second` = the wounded man. -> (prims, overlay).
    The puller's hands are IK targets at the collar points of the second figure (found via voxel.LAST['T']); both figures get
    Prim.fig ids (tilted poses are not tagged by build_figure) so collide.contacts_multi can treat them separately."""
    from voxel import build_figure, LAST
    pose = dict(pose)
    sec = pose.pop("second")
    wx, wz, wyaw = sec["place"]               # wx, wz: where the MIDPOINT of the two collar grab points goes (puller-relative); wyaw: wounded man's heading
    wprims, _ = build_figure(sec["pose"], kit, weapon)
    Tw = LAST["T"].copy()
    wp = sec["pose"]
    Tmw = tr(0, 26, 0) @ tr(0, 4.2, 0) @ rx(wp.get("lean", 0)) @ ry(wp.get("twist", 0)) @ rz(wp.get("roll", 0))
    place = ry(wyaw)
    mid = np.mean([pt(place @ Tw @ Tmw, g) for g in sec["grab"]], axis=0)
    place = tr(wx - mid[0], 0, wz - mid[2]) @ place
    grabs = [pt(place @ Tw @ Tmw, g) for g in sec["grab"]]
    wprims = [q.moved(place) for q in wprims]
    for q in wprims:
        q.fig = 1_000_001
    pose.pop("use", None)
    pprims, _ = build_figure(dict(pose), kit, None, None)              # first pass: only to read the figure transform
    Tp = LAST["T"].copy()
    Tmp = tr(0, 26, 0) @ tr(0, 4.2, 0) @ rx(pose.get("lean", 0)) @ ry(pose.get("twist", 0)) @ rz(pose.get("roll", 0))
    inv = np.linalg.inv(Tp @ Tmp)
    for h, g in zip(sec["hands"], grabs):
        pose[h] = ("t", tuple(float(v) for v in pt(inv, g)))
    pprims, _ = build_figure(pose, kit, None, None)
    DEBUG["grabs"] = grabs
    DEBUG["hands"] = [q.M[:3, 3].copy() for q in pprims if (q.tag or "").startswith("hand_")]
    pfig = pprims[0].fig
    for q in pprims:
        q.fig = pfig
    prims = [q.moved(ry(sec.get("scene_yaw", 0.0))) for q in pprims + wprims]     # whole scene turned: both views keep it inside the frame
    # centre the scene on the ground plane so every view direction keeps it in frame
    c = np.array([q.M[:3, 3] for q in prims])
    mx, mz = (c[:, 0].min() + c[:, 0].max()) / 2, (c[:, 2].min() + c[:, 2].max()) / 2
    G = tr(-mx, 0, -mz)
    return [q.moved(G) for q in prims], []


def wave_flag(i):
    """wave_flag_1..4 (bearer): both hands on the flag pole (z 2 / 12 on the pole axis), pole swung side to side about the right hand
    (swing_z = tilt about the forward axis): left, upright, right, upright; shoulders follow with a little twist."""
    sw = (-12, 6, 32, 6)[i - 1]
    tw = (-8, 0, 8, 0)[i - 1]
    w = dict(pos=(0, 0, 0), yaw=0, pitch=86, swing_z=sw)
    h = pt(_frame_mat(w), (0, 0, 2.0))
    tgt = (-5.0, 9.5, 11.0)   # z 9 -> 11: the cap visor showed through the cloth as a brown dash (wave_flag_1 s/se)
    w["pos"] = tuple(float(tgt[k] - h[k]) for k in range(3))
    return dict(hipL=4, hipR=-3, kneeL=4, kneeR=4, spread=6, lean=0, twist=tw, head_pitch=-4, weapon=w,
                handR=("t", _hand_on(w, 2.0)), handL=("t", _hand_on(w, 12.0)),
                poleR=(-0.8, -1.0, -0.2), poleL=(0.8, -1.0, -0.3))


def pose(frame):
    """pose dict for one of the motion frames, or None."""
    if frame.startswith("wave_flag_"):
        return wave_flag(int(frame[-1]))
    if frame.startswith("crawl_"):
        return crawl(int(frame[-1]))
    if frame.startswith("drag_"):
        return drag(int(frame[-1]))
    if frame.startswith("grenade_"):
        return grenade(int(frame[-1]))
    if frame.startswith("trench_fire_"):
        return trench_fire(int(frame[-1]))
    if frame.startswith("idle_rest_"):
        return rest(int(frame[-1]))
    if frame.startswith("idle_look_"):
        return look(int(frame[-1]))
    if frame.startswith("idle_adjust_"):
        return adjust(int(frame[-1]))
    if frame.startswith("idle_stretch_"):
        return stretch(int(frame[-1]))
    if frame.startswith("surrender_"):
        return surrender(int(frame[-1]))
    if frame == "salute":
        return salute()
    if frame == "present_arms":
        return present_arms()
    return None
