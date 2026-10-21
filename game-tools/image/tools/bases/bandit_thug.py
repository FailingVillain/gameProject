"""Bandit thug / Fengtian dadao infantry pose set: officer poses (idle, walk, hit/death, charge, melee). The dadao is carried SLUNG ON
THE BACK in idle / walk / hit / death (use=None + slung) and drawn into the hand only for charge and melee frames. No rifle, no
aim/fire frames."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import bases.officer as off  # noqa: E402

MELEE_FRAMES = tuple(off.MELEE_FRAMES)
# dadao slung diagonally across the back: hilt up over the left shoulder, tip down at the right hip (torso coords)
SLING_DADAO = dict(pos=(0.5, 4.0, -8.0), yaw=80, pitch=55, roll=0)


def _drawn(frame):
    return frame in MELEE_FRAMES or frame.startswith("charge_")


def pose_for(frame, d="s", e_extra=0.0):
    p = dict(off.pose_for(frame, d, e_extra))
    if _drawn(frame):
        p["use"] = "dadao"
    else:
        p["use"] = None
        p.pop("drop", None)
        p["slung_use"] = "dadao"
        p["slung"] = SLING_DADAO
    return p


PREVIEW_FRAMES = ["idle", "walk_1", "melee_2"]
