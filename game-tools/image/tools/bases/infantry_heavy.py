"""Heavy-weapon infantry pose set (bipod LMG gunner "mg_*" and anti-tank-rifle gunner "at_*"); body shared with bases/infantry.py.
Frames per kind K in (mg, at):
  K_idle, K_walk_1-4        weapon carried on the right shoulder
  K_kneel                   deploy 1: kneeling, weapon held low at the hip, bipod folded
  K_down                    deploy 2: dropping to the ground, weapon pushed forward
  K_aim                     deploy 3 / fire loop base: prone, bipod planted
  K_fire_a, K_fire_b        fire loop: a = muzzle flash + recoil (weapon + shoulder back), b = flash gone, recovering
Deaths (hit/fall/dead/stagger_b/.../dead_c) are taken unchanged from bases/infantry.py (bipod folded added).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from poses import walk_legs  # noqa: E402
from voxel import _frame_mat, rz  # noqa: E402
from bases import infantry as _inf  # noqa: E402

KINDS = {
    #       local gripR (matches the weapon)   shoulder carry pitch   butt-to-receiver (prone receiver y = BUTT_Y + butt)
    "mg": dict(gripR=(0, -3.0, -4.3), butt=17.0, carry_pitch=30, flash_recoil=1.8, kick=1.0),
    "at": dict(gripR=(0, -2.4, -7.3), butt=19.2, carry_pitch=6, flash_recoil=3.8, kick=2.5),
}
BUTT_Y = 22.0           # torso-frame y of the butt plate when prone (weapon lies beside the body)
PRONE_Z = 1.2           # torso-frame depth of the receiver when prone (bipod feet reach the ground, see parts/heavy_weapons.FOOT_Y)


def _origin_for_grip(target, local_grip, yaw, pitch):
    """weapon origin (torso frame) that puts local_grip at target for a given yaw / pitch."""
    R = _frame_mat(dict(yaw=yaw, pitch=pitch))[:3, :3]
    return tuple(np.asarray(target, float) - R @ np.asarray(local_grip, float))


def _carry(k, i):
    """i: 0 idle, 1..4 walk. right hand on the weapon at the front of the right shoulder, weapon resting over the shoulder."""
    c = KINDS[k]
    if i == 0:
        L = dict(hipL=10, hipR=-2, kneeL=18, kneeR=8, lean=5, spread=6, lift=3)
        bob, pit = 0.0, 0.0
        left = "free"
    else:
        L = walk_legs(i)
        passing = i in (2, 4)
        bob = 0.8 if passing else 0.0
        pit = 3.0 if passing else 0.0
        left = ("sw", L.pop("sw"), 12, 6)
    pitch = c["carry_pitch"] + pit
    yaw = 4
    tgt = (-11.0, 16.8 + bob, 5.0)
    if k == "mg":      # N56b: LMG carried muzzle BACK (heavy bipod / barrel end behind the shoulder, stock forward-down in the hand)
        yaw, pitch = 180, 28 + pit
        tgt = (-9.5, 15.0 + bob, 7.0)
    pos = _origin_for_grip(tgt, c["gripR"], yaw, pitch)
    d = dict(L, weapon=dict(pos=pos, yaw=yaw, pitch=pitch), handR="grip", handL=left, poleR=(-0.9, -1, -0.3),
             attach=["bipod_folded"], e_pull=1.0)
    d.setdefault("head_pitch", -2)
    return d


def _kneel(k):
    c = KINDS[k]
    return dict(hipL=68, hipR=-4, kneeL=82, kneeR=100, lean=12, spread=8, twist=4, head_pitch=-4, head_yaw=-5,
                weapon=dict(pos=(-6.5, 9.0, 8.5), yaw=3, pitch=-8), handR="grip", handL="grip",
                poleL=(0.5, -1, 0.3), poleR=(-0.6, -1, -0.2), attach=["bipod_folded"], push=6, e_pull=2.0)


def _down(k):
    return dict(tilt=62, lean=16, head_pitch=-34, head_yaw=-6, hipL=40, hipR=12, kneeL=70, kneeR=60, spread=12,
                weapon=dict(pos=(-8, 32, 13), yaw=0, pitch=58), handR="grip", handL="grip",
                poleL=(0.6, -0.4, 0.8), poleR=(-0.8, -0.2, 0.6), attach=["bipod_folded"], e_pull=2.0)


def _prone(k, fire):
    """fire: None (aim) / "a" (flash, recoil) / "b" (recovering, no flash)."""
    c = KINDS[k]
    rec = {"a": c["flash_recoil"], "b": c["flash_recoil"] * 0.4}.get(fire, 0.0)
    y = BUTT_Y + c["butt"] - rec
    pitch = 90 + (c["kick"] if fire == "a" else (c["kick"] * 0.5 if fire == "b" else 0.0))
    d = dict(tilt=90, lean=-3.0 * (rec / c["flash_recoil"]) if fire else 0.0, head_pitch=-58 + (4 if fire == "a" else 0), head_yaw=-6,
             hipL=2, hipR=0, kneeL=78, kneeR=98, spread=14,
             weapon=dict(pos=(-7.0, y, PRONE_Z), yaw=0, pitch=pitch), handR="grip", handL="grip",
             poleL=(0.9, -0.2, 1.0), poleR=(-0.9, -0.2, 1.0), e_yaw=-22, attach=["bipod_open"])
    if fire == "a":
        d["overlay"] = "flash"
    return d


# ---------------------------------------------------------------- anti-tank rifle: kneeling fire (right knee on the ground, left leg up, rifle shouldered)
KNEEL_LEGS = dict(hipL=82, hipR=-8, kneeL=97, kneeR=96, spread=8)       # left shin vertical + thigh level, right thigh vertical, right knee on the ground
E_PULL_KNEEL = 0.0
E_SWING_KNEEL = (-60, -19.2)       # E view: muzzle swings toward the camera about the butt (shorter silhouette, no pull into the chest)
KNEEL_SHIFT = (-4.0, 0.0, 8.0, 0.0, -12.0)   # (dx, dy, dz, dpitch, dyaw) added to the shouldered AT rifle (set / aim / fire): butt in the shoulder pocket, not inside the chest
SET_SHIFT_W = 1.2


# per-kind kneeling-fire parameters: shift = (dx, dy, dz, dpitch, dyaw) on the shouldered weapon, swing = E-view (deg, pivot z = butt), forend = weapon-local left-hand point
KNEEL_K = {
    "at": dict(shift=KNEEL_SHIFT, swing=E_SWING_KNEEL, forend=(0, -2.0, 1.5), kick=12.0),
    "mg": dict(shift=(-4.0, 0.0, 10.2, 0.0, -12.0), swing=(-60, -17.0), forend=(0, -2.4, 5.5), kick=7.0),   # LMG butt plate at -17 (AT -19.2): origin moved forward by the difference (mg_down only)
}


# ---------------------------------------------------------------- LMG: kneeling behind a sandbag parapet, gun on its open bipod ("앉아쏴")
# Body: right knee down / left foot forward, torso leaned MG_LEAN. The gun is NOT placed by hand: it is solved in WORLD space so that
#   * the butt plate sits in the right shoulder pocket (MG_BUTT, torso frame) with the barrel almost level (MG_PITCH_W, slightly downhill),
#   * the open bipod feet then define the parapet height (heavy_weapons.SANDBAG top; ground = sole of the lowest foot, MG_GROUND).
# Recoil / E-view swing are applied to that world matrix and converted back to the torso frame, so the bipod stays on the parapet in every
# direction and every frame.
MG_LEGS = dict(hipL=80, hipR=30, kneeL=95, kneeR=118, spread=8)
MG_GROUND = 7.28         # figure-space y of the lowest foot sole for MG_LEGS (voxel.build_figure moves that to 0)
MG_LEAN = 28.0
MG_BUTT = (-7.5, 14.5, 6.8)       # butt-plate centre in the torso frame (right shoulder pocket, in front of the shoulder ball)
MG_YAW = 4.0
MG_PITCH_W = -4.0                 # world pitch of the barrel (deg, negative = muzzle lower than the butt)
MG_UNDER = (0.0, -4.4, -14.0)     # weapon-local point under the buttstock: left hand cups the stock toe, right hand holds the pistol grip (gripR)
MG_BIPOD = (0.0, -5.4, 25.4)      # weapon-local foot-pair centre (bipod mount z 22 + dz 3.4, foot y = heavy_weapons.FOOT_Y)
MG_RECOIL = dict(lean=-2.2, slide=1.0, kick=1.4)     # per unit "k" (fire_a k = 1, fire_b k = 0.4): shoulder pushed back, gun slid back, muzzle rise about the butt plate (the bipod hops ~1 unit off the sandbags)
# Aim direction (N2 / HANDOFF 4o, 2026-10-19): the game map is drawn top-down, so the sprite row for yaw Y must show the barrel at screen
# angle Y (0 = down, 90 = right, 180 = up) or the gun visibly points off the target. The old fixed side-view swings (e -80, se -42, ne -98,
# used to keep the gun inside the frame) put the barrel up to 82 deg off; the camera elevation (voxel.ELEV 36) also foreshortens a level
# barrel toward the vertical. Now the pose is the same in every direction (the s-view pose: collide 0 everywhere) and the WHOLE figure is
# turned about the ground point (pose "view_yaw", voxel.render_figure) by the angle that puts the PROJECTED barrel axis exactly on the
# row's nominal direction (_swing_to_screen, solved per direction and frame, any yaw incl. the 22.5 deg rows; body turns by <= 15 deg,
# the frame auto-fit / anchors keep the feet on the unit position). Check: python tools/mg_axis.py
MG_TWIST = {}      # optional per-direction upper-body twist (unused: gear/leg collisions), kept for tuning


def _screen_angle(ax, yaw):
    """screen angle (deg, 0 = down, 90 = right) of figure-space direction ax seen in the row of the given yaw."""
    from voxel import ry, UP
    q = ry(yaw)[:3, :3] @ np.asarray(ax, float)
    return math.degrees(math.atan2(q[0], -(q @ UP)))


def _swing_to_screen(ax, d, target=None):
    """world-vertical swing (deg) of direction ax so that its projection in row d lies on the row's nominal screen angle
    (or on `target`). Scan + bisection, |error| < 0.01 deg."""
    from voxel import YAWS, ry
    yaw = YAWS[d] if isinstance(d, str) else float(d)
    want = yaw if target is None else target
    def err(sw):
        a = _screen_angle(ry(sw)[:3, :3] @ np.asarray(ax, float), yaw)
        return (a - want + 180.0) % 360.0 - 180.0
    best = min(np.arange(-90.0, 90.01, 1.0), key=lambda sw: abs(err(sw)))
    lo, hi = best - 1.0, best + 1.0
    if err(lo) * err(hi) > 0:
        return float(best)
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if err(lo) * err(mid) <= 0:
            hi = mid
        else:
            lo = mid
    return 0.5 * (lo + hi)


def _Tm(lean, twist=0.0):
    from voxel import rx, ry, tr
    return tr(0, 30.2, 0) @ rx(lean) @ ry(twist)


def pt_(M, p):
    return (M @ np.array([p[0], p[1], p[2], 1.0]))[:3]


_MG_W = {}

# per-weapon constants for the sandbag-parapet kneel (weapon-local points): butt plate centre, point under the stock for the left hand,
# bipod foot-pair centre, recoil scale. AT rifle (units: heavy-at-rifleman*) fires from the same parapet pose as the LMG.
def _atr_bipod():
    import parts.heavy_weapons as hw
    return (0.0, hw.FOOT_Y, hw._X - 2.0 + 3.6)


SB_CFG = {"mg": dict(butt=(0.0, -1.0, -17.0), under=MG_UNDER, bipod=MG_BIPOD, kick=1.0),
          "at": dict(butt=(0.0, -1.0, -19.2), under=(0.0, -3.6, -14.0), bipod=None, kick=1.0)}


def _cfg(k):
    c = dict(SB_CFG[k])
    if c["bipod"] is None:
        c["bipod"] = _atr_bipod()
    return c


def _mg_aim_world(twist=0.0, k="mg"):
    """world matrix of the LMG at aim (torso twisted by `twist` about its own axis); the first call (twist 0) sets the parapet height,
    later calls re-solve the pitch so the bipod feet stay on the same parapet."""
    import parts.heavy_weapons as hw
    C = _cfg(k)
    Tm = _Tm(MG_LEAN, twist)
    best = None
    for th in np.arange(-45.0, 90.0, 0.1):
        spec = dict(pos=(0, 0, 0), yaw=MG_YAW, pitch=th)
        R = _frame_mat(spec)[:3, :3]
        spec["pos"] = tuple(np.array(MG_BUTT, float) - R @ np.array(C["butt"]))
        W = Tm @ _frame_mat(spec)
        if twist == 0.0:
            wp = math.degrees(math.asin((W[:3, :3] @ np.array([0.0, 0.0, 1.0]))[1]))
            e = abs(wp - MG_PITCH_W)
        else:
            e = abs(pt_(W, C["bipod"])[1] - 0.1 - hw.SANDBAG["top"])
        if best is None or e < best[0]:
            best = (e, W)
    W = best[1]
    if twist == 0.0:
        hw.SANDBAG["ground"] = MG_GROUND
        hw.SANDBAG["top"] = float(pt_(W, C["bipod"])[1]) - 0.1
    return W


def _rodrigues(axis, deg):
    a = np.asarray(axis, float); a = a / np.linalg.norm(a)
    t = math.radians(deg); c, s = math.cos(t), math.sin(t)
    K = np.array([[0, -a[2], a[1]], [a[2], 0, -a[0]], [-a[1], a[0], 0]])
    R = np.eye(4); R[:3, :3] = np.eye(3) * c + s * K + (1 - c) * np.outer(a, a)
    return R


def _about(R, P):
    """4x4 rotation R about world point P."""
    from voxel import tr
    return tr(*P) @ R @ tr(*(-np.asarray(P, float)))


def _mg_fire(fire, d="s", kind="mg"):
    """kneeling fire: fire None = aim, 'a' = flash + recoil, 'b' = recovering. kind 'at' = anti-tank rifle on the same parapet."""
    C = _cfg(kind)
    cache = _MG_W.setdefault(kind, {})
    if 0.0 not in cache:
        cache[0.0] = _mg_aim_world(0.0, kind)
    k = {"a": 1.0, "b": 0.4}.get(fire, 0.0)
    lean = MG_LEAN + MG_RECOIL["lean"] * k * C["kick"]
    tw = MG_TWIST.get(d, 0.0)
    if tw not in cache:
        cache[tw] = _mg_aim_world(tw, kind)
    Tm = _Tm(lean, tw)
    W = cache[tw].copy()
    ax = W[:3, :3] @ np.array([0.0, 0.0, 1.0])
    view = _swing_to_screen(ax, d)                                   # solved on the aim axis: fire_a / fire_b keep the same direction (recoil only)
    if k:
        side = np.cross([0.0, 1.0, 0.0], ax)                         # horizontal axis; negative angle raises the muzzle
        W = _about(_rodrigues(side, -MG_RECOIL["kick"] * k * C["kick"]), pt_(W, C["butt"])) @ W
        ax = W[:3, :3] @ np.array([0.0, 0.0, 1.0])
        W[:3, 3] -= ax * MG_RECOIL["slide"] * k
    Mt = np.linalg.inv(Tm) @ W
    w = dict(pos=tuple(float(v) for v in Mt[:3, 3]), **_spec_from_rot(Mt[:3, :3]))
    down = np.linalg.inv(Tm)[:3, :3] @ np.array([0.0, -1.0, 0.0])          # world "down" in the torso frame: elbows hang toward the knees / ground
    dd = dict(MG_LEGS, lean=lean, twist=tw, head_pitch=-(lean + MG_PITCH_W) + (3 if fire == "a" else 0), head_yaw=-8, head_roll=-10, weapon=w,
              handR="grip", handL=("t", _torso_pt(w, C["under"])),
              poleR=tuple(down + np.array([-2.0, 0, 0])), poleL=tuple(down + np.array([2.0, 0, 0])), attach=["bipod_sandbag"], push=10)
    dd["view_yaw"] = view
    if fire == "a":
        dd["overlay"] = "flash"
    return dd


def _torso_pt(spec, local):
    """weapon-local point -> torso frame (for handL=("t", ...) targets)."""
    return tuple(_frame_mat(spec)[:3, :] @ np.array([local[0], local[1], local[2], 1.0]))


def _kneel_low():
    """deploy 1: dropped to the right knee, rifle held low across the body, bipod folded."""
    return dict(KNEEL_LEGS, lean=12, twist=4, head_pitch=-4, head_yaw=-5,
                weapon=dict(pos=(-6.5, 9.0, 8.5), yaw=3, pitch=-8), handR="grip", handL="grip",
                poleL=(0.5, -1, 0.3), poleR=(-0.6, -1, -0.2), attach=["bipod_folded"], push=6, e_pull=2.0)


def _kneel_set(t, k="at"):
    """t 0..1: rifle coming up from the hip (t=0) to the shoulder (t=1); left hand on the forend."""
    lerp = lambda a, b: a + (b - a) * t
    K = KNEEL_K[k]; SH = K["shift"]
    w = dict(pos=(lerp(-6.5, -6.0) + SET_SHIFT_W * SH[0], lerp(9.0, 14.2) + SET_SHIFT_W * SH[1], lerp(8.5, 21.0) + SET_SHIFT_W * SH[2]),
             yaw=lerp(3, 2.0) + SET_SHIFT_W * SH[4], pitch=lerp(-8, 0.0) + SET_SHIFT_W * SH[3])
    return dict(KNEEL_LEGS, lean=lerp(12, 8), twist=lerp(4, 8), head_pitch=lerp(-4, 2), head_yaw=lerp(-5, -9), weapon=w,
                handR="grip", handL=("t", _torso_pt(w, K["forend"])), poleL=(0.9, -1, 0.2), poleR=(-0.6, -1, -0.3),
                attach=["bipod_folded"], push=lerp(6, 14), e_pull=E_PULL_KNEEL, e_swing=K["swing"])


def _kneel_shoot(fire, k="at"):
    """fire: None aim / 'a' flash + hard recoil (torso thrown back, rifle back and muzzle kicked up) / 'b' recovering."""
    K = KNEEL_K[k]; SH = K["shift"]
    k = {"a": 1.0, "b": 0.4}.get(fire, 0.0)
    lean = 8.0 - K["kick"] * k
    w = dict(pos=(-6.0 + SH[0], 14.2 - 0.3 * k + SH[1], 21.0 - 4.0 * k + SH[2]), yaw=2.0 + SH[4],
             pitch=7.0 * k + SH[3])
    d = dict(KNEEL_LEGS, lean=lean, twist=8, head_pitch=2 - 0.4 * lean + (3 if fire == "a" else 0), head_yaw=-9, weapon=w,
             handR="grip", handL=("t", _torso_pt(w, K["forend"])), poleL=(0.9, -1, 0.2), poleR=(-0.6, -1, -0.3),
             attach=["bipod_folded"], push=14, e_pull=E_PULL_KNEEL, e_swing=K["swing"])
    if fire == "a":
        d["overlay"] = "flash"
    return d


# side views: the long prone weapon would overflow the 64-unit frame width, so in E / NE it is pulled back along the body and
# swung outward about the vertical axis (foreshortened): (pull, swing degrees)
BUTT_LOCAL = -17.0     # weapon-local z of the butt plate (lmg_prims)
PRONE_SIDE = {"mg": {"e": (0.0, 70, 0.0), "ne": (0.0, 60, 0.0)}, "at": {}}      # AT rifle kneels instead (no twist)


def _spec_from_rot(R):
    """weapon spec (yaw, pitch, roll) whose _frame_mat rotation equals R (ry(yaw) rx(-pitch) rz(roll))."""
    sb = -R[1, 2]
    return dict(yaw=math.degrees(math.atan2(R[0, 2], R[2, 2])), pitch=-math.degrees(math.asin(sb)),
                roll=math.degrees(math.atan2(R[1, 0], R[1, 1])))


_CUR_DIR = "s"
AT_E_VIEW = 55.0     # extra view_yaw (deg) for the AT rifle on the sandbags in the E row (width 161 px at 0 -> ~110 px)

MG_DEPLOY_AIM = ("mg_kneel", "mg_down")      # deploy transition frames: barrel also turned onto the row's nominal screen direction


def _aim_deploy(p, d):
    """turn the whole figure (pose view_yaw) so the held LMG's projected axis lies on row d's nominal screen angle; the E-view
    pull / swing (frame-fit cheats that pointed the gun off-axis) are dropped, so the pose is identical in every direction."""
    from voxel import tr, rx, ry, rz
    for k in ("e_pull", "e_yaw", "e_swing", "e_slide"):
        p.pop(k, None)
    Tm = tr(0, 30.2, 0) @ rx(p.get("lean", 0)) @ ry(p.get("twist", 0)) @ rz(p.get("roll", 0))      # = voxel.build_figure torso frame
    Wm = Tm @ _frame_mat(p["weapon"])
    p["view_yaw"] = _swing_to_screen(Wm[:3, :3] @ np.array([0.0, 0.0, 1.0]), d)
    return p


def pose_for(frame, d="s", e_extra=0.0):
    global _CUR_DIR
    _CUR_DIR = d
    p = _pose(frame, "at" if e_extra >= 4.5 else "mg")
    if e_extra >= 4.5 and d == "e" and frame in ("at_aim", "at_fire_a", "at_fire_b"):
        p["view_yaw"] = p.get("view_yaw", 0.0) + AT_E_VIEW      # long rifle level in the E row is wider than the 128 cell: turn it toward the camera
    if frame in MG_DEPLOY_AIM and e_extra < 4.5:
        p = _aim_deploy(p, d)
    if "bipod_open" in p.get("attach", ()) and d in PRONE_SIDE[frame[:2]]:
        pull, swing, off = PRONE_SIDE[frame[:2]][d]
        w = dict(p["weapon"]); x, y, z = w["pos"]
        R0 = _frame_mat(w)[:3, :3]
        R = rz(swing)[:3, :3] @ R0
        butt0 = np.asarray(w["pos"], float) + R0 @ np.array([0.0, 0.0, BUTT_LOCAL])     # keep the stock on the shoulder, swing the muzzle about it
        w.update(_spec_from_rot(R))
        w["pos"] = tuple(butt0 - R @ np.array([0.0, 0.0, BUTT_LOCAL]) + np.array([off, -pull, 0.0]))
        p["weapon"] = w
        p.pop("e_yaw", None)
    return _inf.apply_e_view(p, d, e_extra)


def _pose(frame):
    if frame[:3] in ("mg_", "at_"):
        k, name = frame[:2], frame[3:]
        if name == "idle":
            return _carry(k, 0)
        if name.startswith("walk_"):
            return _carry(k, int(name.split("_")[1]))
        # both kinds fire kneeling (right knee down, weapon shouldered). mg keeps its frame names: kneel (hip carry) -> down (= weapon coming up) -> aim
        if name == "kneel":
            return _kneel_low() if k == "at" else _kneel(k)
        if name == "set" and k == "at":
            return _kneel_set(0.55)
        if name == "down" and k == "mg":
            return _kneel_set(0.55, "mg")
        if name in ("aim", "fire_a", "fire_b"):
            return _mg_fire({"aim": None, "fire_a": "a", "fire_b": "b"}[name], _CUR_DIR, k)
        raise KeyError(frame)
    p = _inf._pose(frame)                                   # hit / fall / dead / *_b / *_c  reused from the rifleman
    if p.get("weapon") is not None:
        p.setdefault("attach", ["bipod_folded"])
    return p


# reused rifleman poses were authored for a rifle whose midpoint is local z = +2.4: slide the heavier weapon back along its own axis so
# the longer one does not stick out of the frame / into the ground (units/heavy-at-rifleman.json sets e_pull_extra 7, the MG keeps 3).
CENTRE_SHIFT = {"mg": 3.1, "at": 3.0}
DROP_EXTRA = {"mg": 0.0, "at": 2.0}   # dropped weapon is slid back further (it lies flat and shows its full length)
DROP_YAW = {"mg": {}, "at": {"dead": -60, "dead_b": -60, "dead_c": -60}}              # per death frame: extra yaw of the dropped weapon (frame -> degrees)


def _slide_back(spec, k, drop=False, frame=""):
    spec = dict(spec)
    if drop:
        spec["yaw"] = spec.get("yaw", 0) + DROP_YAW[k].get(frame, 0.0)
    R = _frame_mat(spec)[:3, :3]
    spec["pos"] = tuple(np.asarray(spec["pos"], float) - R[:, 2] * (CENTRE_SHIFT[k] + (DROP_EXTRA[k] if drop else 0.0)))
    return spec


def _is_death_reuse(frame):
    return frame[:3] not in ("mg_", "at_")


_pose_raw = _pose


def _pose(frame, k="mg"):                                   # noqa: F811  (wrapper: death reuse gets the weapon recentred)
    p = _pose_raw(frame)
    if _is_death_reuse(frame):
        for key in ("weapon", "drop"):
            if p.get(key) is not None:
                p[key] = _slide_back(p[key], k, key == "drop", frame)
    return p
