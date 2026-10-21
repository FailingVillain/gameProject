"""Fengtian rifleman pose set: infantry-default body/poses/rifle (bases/infantry.py, `use="rifle"`) except charge + melee, which REUSE
the officer's sword poses (bases/officer.py charge_pose / MELEE, edge-direction rule) with the dadao ("dadao"); the rifle is then slung
on the back (pose keys slung_use / slung). No bayonet frames and no butt-stroke (meleeb) frames.
Frames: idle, spare, walk_1-4, aim/fire a-c, idle_alt, hit/fall/dead, charge_1-4, melee_1-3, idle_lean, go_prone, aim_prone/fire_prone,
stagger_b/kneel_b/dead_b, stagger_c/slump_c/dead_c."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import bases.infantry as inf  # noqa: E402
import bases.officer as off  # noqa: E402

# rifle slung diagonally across the back, behind the field pack (torso coords; pack back surface ~ z -12.3):
# muzzle up over the left shoulder (+x), butt down at the right hip; ~35 deg from vertical (pitch 55), yaw 80 keeps it near the back plane.
# z -18: clear of the pack straps/pouches (leather) at -17 and nearer; checked 0 interpenetration in all charge/melee frames and dirs.
SLING_RIFLE = dict(pos=(0.5, 4.0, -14.0), yaw=80, pitch=55, roll=0)
MELEE_FRAMES = tuple(off.MELEE_FRAMES)


def _with_rifle(p):
    p = dict(p)
    p.pop("attach", None)
    p["use"] = "rifle"
    return p


def pose_for(frame, d="s", e_extra=0.0):
    p = _pose(frame)
    return inf.apply_e_view(p, d, e_extra if p.get("use") == "rifle" else 0.0)     # long-rifle E pull-back only for rifle poses


def _pose(frame):
    if frame in MELEE_FRAMES:
        return dict(off._tuned(frame, off._pose(frame)), use="dadao", slung_use="rifle", slung=SLING_RIFLE)
    if frame.startswith("charge_"):
        return dict(off._tuned(frame, off.charge_pose(int(frame.split("_")[1]))), use="dadao", slung_use="rifle", slung=SLING_RIFLE)
    return _with_rifle(inf._tuned(frame, inf._pose(frame)))     # _tuned: E-view pull / yaw / swing from tune/poses.json (as in infantry.pose_for)
