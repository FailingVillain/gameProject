"""Scout pose set: the rifleman set (bases/infantry.py) with the carbine in hand (pose key use="carbine") plus the observing poses
binoc_stand / binoc_kneel: both hands raise the field binoculars to the eyes, the carbine hangs slung on the back (slung_use).
Everything else is delegated to bases.infantry, so changes there (new / reworked frames) are inherited."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from bases import infantry as inf  # noqa: E402
from poses import STANCE  # noqa: E402
from parts import scout as _scout  # noqa: E402

# frames where arms / the ground press on the chest: binoculars tucked away (TUCK) or strap hidden too (HIDE)
SHIFT = {"idle": (-1.5, -1.5, 0, 0, 0), "spare": (-1.5, -1.5, 0, 0, 0), "charge_2": (0, -1.5, 0, 0, 0), "charge_4": (0, -1.5, 0, 0, 0)}     # frame -> (dx, dy, dz, dpitch, dyaw) weapon nudge (short carbine vs the rifle the pose was built for)
TUCK = set()
HIDE_PREFIX = ('walkfire_', 'crawl_', 'melee', 'aim_prone', 'fire_prone', 'idle_rest_')   # prefixes (inherits new variants)
HIDE = {'melee_1', 'meleeb_1'}

# carbine slung diagonally on the back (torso frame): butt low on the right side, muzzle up past the left shoulder
SLING = dict(pos=(0.0, 11.0, -9.4), yaw=38, pitch=52)
BINOC_STAND = dict(pos=(0.0, 22.6, 10.0), yaw=0, pitch=0)


def binoc_pose(kneel):
    st = dict(STANCE["c"]) if kneel else dict(hipL=3, hipR=-3, kneeL=3, kneeR=3, lean=2, spread=6)
    w = dict(BINOC_STAND)
    hp = -0.7 * st["lean"]
    if kneel:
        w["pitch"] = 7.0
        w["pos"] = (0.0, 22.6, 10.0)
    return dict(st, head_pitch=hp, head_yaw=0, twist=0, weapon=w, handR="grip", handL="grip",
                poleR=(-1.0, -0.5, -0.1), poleL=(1.0, -0.5, -0.1),
                use="binoc", slung_use="carbine", slung=SLING)


def pose_for(frame, d="s", e_extra=0.0):
    if frame in ("binoc_stand", "binoc_kneel"):
        p = binoc_pose(frame == "binoc_kneel")
        _scout.RAISED = p["weapon"]["pos"]
        return p
    _scout.RAISED = None
    _scout.MODE = 2 if (frame in HIDE or frame.startswith(HIDE_PREFIX)) else 1 if frame in TUCK else 0
    p = inf.pose_for(frame, d, e_extra)
    p.setdefault("use", "carbine")
    f = SHIFT.get(frame)
    if f and isinstance(p.get("weapon"), dict):
        w = dict(p["weapon"]); x, y, z = w["pos"]
        w["pos"] = (x + f[0], y + f[1], z + f[2]); w["pitch"] = w.get("pitch", 0) + f[3]; w["yaw"] = w.get("yaw", 0) + f[4]
        p["weapon"] = w
    return p


def __getattr__(name):            # anything else (constants, helpers) comes from the rifleman base
    return getattr(inf, name)
