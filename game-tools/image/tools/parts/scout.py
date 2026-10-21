"""Scout (정찰병) equipment: field binoculars (hand-held weapon + chest-worn gear on a neck strap) and a flat map case.
The chest binoculars are gear (torso frame); while the pose raises the glasses to the eyes (bases/infantry_scout.py sets RAISED to the
raised glasses' centre, torso frame) the chest pair is omitted and the neck strap runs up to the raised pair instead."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import Weapon, rbox as box, along_z, seg, tr, pt  # noqa: E402

BY, BZ, BL = 8.0, 4.7, 3.0   # chest-worn pair: height, depth (front of the jacket), length
MODE = 0               # 0 = pair + strap on the chest, 1 = pair tucked away (strap only), 2 = nothing (arms / ground press on the chest)
RAISED = None          # None = binoculars hang on the chest; (x, y, z) = centre of the raised pair (torso frame)


def binoc_body(M):
    """binoculars along +z (objective bells forward, eyecups at -z), ~7.5 long, barrels 4.4 apart."""
    p = []
    for s in (1, -1):
        p.append(along_z(M @ tr(s * 2.2, 0, 0), 1.45, -2.4, 3.2, "steel"))
        p.append(along_z(M @ tr(s * 2.2, 0, 0), 1.8, 3.2, 4.8, "steel"))        # objective bell
        p.append(along_z(M @ tr(s * 2.2, 0, 0), 1.1, -3.1, -2.4, "leather"))    # eyecup
    p.append(box(M @ tr(0, 0.2, 0.4), 1.8, 0.8, 3.2, "steel"))                  # bridge
    return p


BINOCULARS = Weapon(binoc_body, gripR=(-4.0, -0.9, 0.8), gripL=(4.0, -0.9, 0.8))


def binoc_chest(T):
    """binoculars hanging on the chest from a neck strap (resting against the sternum), or just the strap when raised."""
    if MODE >= 2 and RAISED is None:
        return []
    if RAISED is not None:
        cx, cy, cz = RAISED
        out = []
        for s in (1, -1):
            a, b = (s * 3.9, 17.6, 3.2), (s * 4.6, cy - 0.5, cz - 1.5)
            out.append(seg(pt(T, a), pt(T, b), 0.45, "leather", flat=True))
        return out
    out = []
    for s in (1, -1):
        a, b = (s * 3.9, 17.6, 2.6), (s * 2.0, 9.6, 4.6)
        out.append(seg(pt(T, a), pt(T, b), 0.45, "leather", flat=True))
    if MODE >= 1:
        return out
    M = T @ tr(0, BY, BZ)
    for s in (1, -1):
        out.append(along_z(M @ tr(s * 1.6, 0, 0), 1.2, 0.0, BL, "steel"))
        out.append(along_z(M @ tr(s * 1.6, 0, 0), 1.45, BL, BL + 0.9, "steel"))
    out.append(box(M @ tr(0, 0.3, BL / 2), 1.5, 0.8, BL, "steel"))
    return out


def map_case_front(T):
    """flat leather map case on the left front hip (strap over the shoulder omitted; it hangs against the hip)."""
    return [box(T @ tr(8.7, 0.2, 2.2), 0.9, 3.6, 3.4, "leather", r=0.4),
            box(T @ tr(9.1, 2.0, 2.2), 0.7, 0.9, 3.5, "leather", r=0.3)]


WEAPONS = {"binoculars": BINOCULARS}
GEAR = {"binoc_chest": binoc_chest, "map_case_front": map_case_front}
