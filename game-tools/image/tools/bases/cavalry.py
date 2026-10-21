"""Mounted rifleman (cavalry): shared infantry body seated on a primitive horse (parts/horse.py).

Frame (main LOD): 192x224, ground anchor (96, 190) = ground point under the horse's barrel centre; LODs 96x112 (48, 95) and
48x56 (24, 48) rendered natively (same model zoom as the 128x160 infantry, so rider and infantry match in size).
Columns (anims/cavalry.json): idle, idle_2, walk_1-4, gallop_1-4, aim, fire, charge_1-4 (gallop + sabre forward),
melee_1-3 (slash from the saddle), hit, fall, dead (horse collapses with the rider), hit_b (= hit), slip_b, fall_b, dead_b
(rider falls off). hit_b is its own column so each death anim gets its own shared-fit translation.
Weapons: unit json "weapons": {"carbine": "cav_carbine", "sabre": "sabre"}; pose key `use` picks the one in hand. When not in
hand the carbine sits in the saddle boot (horse's right) and the sabre in the saddle scabbard (horse's left).
Mounted officer (units/cavalry-officer*.json): "weapons": {"pistol": "pistol", "sabre": "sabre"} and no carbine -> aim/fire use a
one-handed pistol pose (right arm out, left hand keeps the reins; tune/poses.json cavalry.pistol), no carbine on the back.

render_unit_frame(unit, d, frame, res, fit, shift) is called by unit_loader.Unit.frame (make_unit.py pipeline).
Tunables: tune/poses.json -> "cavalry" (gaits, seat, arms), tune/parts.json -> "horse" (dimensions, tack).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import B_TW, Res, build_figure, make_frame, death_pose, _bbox, seg, rbox, tr, pt, rz, ry  # noqa: E402
from common import BASE_FW, BASE_FH  # noqa: E402
import tune  # noqa: E402
from parts import horse as H  # noqa: E402
from bases.officer import straight_arm, edge_toward  # noqa: E402


# ---------------------------------------------------------------- resolution profile
class CavRes(Res):
    """Res with a 192x192-class frame at the infantry zoom: pxs drives zoom / outline / AO exactly like the infantry."""

    FRAME = (192, 224)
    AY_FRAC = 0.85
    probe = False                                       # True for the tall bbox-measuring canvas (no AO needed)

    def __init__(self, pxs, fh_mult=1, extra_above=0):
        super().__init__(int(BASE_FW * pxs), int(BASE_FH * pxs))
        self.fw = int(round(self.FRAME[0] * pxs / 2))
        h = int(round(self.FRAME[1] * pxs / 2))
        self.fh = h * fh_mult
        self.ax = self.fw // 2
        self.ay = int(round(h * self.AY_FRAC)) + extra_above
        self._pxs_arg = pxs

    def tall(self, k):
        """same zoom, k times taller canvas with (k-1)/2 frame heights of room above and below (union-bbox measuring)."""
        h = self.fh
        r = CavRes(self._pxs_arg, fh_mult=k, extra_above=h * (k - 1) // 2)
        r.probe = True
        return r

    @property
    def tag(self):
        return f"@{self.fw}"


_K = 1.5 if __import__("os").environ.get("KD_RES96") == "1" else 1.0   # res96: LOD x1.5
LODS = [CavRes(2.0 * _K), CavRes(1.0 * _K), CavRes(0.5 * _K)]          # 192, 96, 48
PREVIEW_FRAMES = ["idle", "walk_1", "gallop_3", "fire", "charge_2", "melee_2"]


def _T():
    return tune.get("poses", "cavalry")


# ---------------------------------------------------------------- horse poses
LEGS = ("FL", "FR", "HL", "HR")


def _stand(**over):
    S = _T()["stand"]
    hp = {k: tuple(S[k]) for k in LEGS}
    hp.update(neck=S["neck"], head=S["head"], tail=S["tail"], pitch=0.0)
    hp.update(over)
    return hp


def _walk_leg(phi, front, W, stand):
    """4-beat walk: stance (phi < duty) sweeps +amp -> -amp; swing lifts and brings the leg forward."""
    amp = W["amp_front"] if front else W["amp_hind"]
    duty = W["duty"]
    s0, k0, f0 = stand
    if phi < duty:
        a = amp * (1 - 2 * phi / duty)
        return (s0 + a, k0, f0)
    t = (phi - duty) / (1 - duty)
    a = -amp + 2 * amp * t
    lift = math.sin(math.pi * t)
    return (s0 + a, k0 + lift * (W["lift_knee"] if front else W["lift_hock"]), f0 + lift * W["lift_fet"])


def walk_horse(i):
    W = _T()["walk"]; S = _T()["stand"]
    hp = _stand(neck=W["neck"], head=W["head"], tail=W["tail"])
    for k in LEGS:
        phi = ((i - 1) / 4.0 + W["offsets"][k] + 0.125) % 1.0
        hp[k] = _walk_leg(phi, k[0] == "F", W, tuple(S[k]))
    hp["neck"] = W["neck"] + (W["nod"] if i in (1, 3) else -W["nod"])
    return hp


def gallop_horse(i):
    G = _T()["gallop"][str(i)]
    hp = _stand()
    for k in LEGS:
        hp[k] = tuple(G[k])
    for k in ("pitch", "air", "neck", "head", "tail"):
        hp[k] = G[k]
    return hp


# ---------------------------------------------------------------- rider poses (infantry body, seated)
def _seat(**over):
    S = _T()["seat"]
    d = dict(hipL=S["hip"], hipR=S["hip"], kneeL=S["knee"], kneeR=S["knee"], spread=S["spread"], lean=S["lean"],
             handL=("t", tuple(_T()["reins"])), handR=("t", tuple(_T()["rest_right"])),
             poleL=(0.6, -1, -0.3), poleR=(-0.6, -1, -0.3), use=None)
    d.update(over)
    return d


CUT = {"charge": (0, 0.2, 1), "melee_1": (0, 0.3, -1), "melee_2": (-0.5, -0.8, 0.3), "melee_3": (0, -0.5, 0.8)}


def _sabre(arm, cut):
    return edge_toward(straight_arm(*arm), cut)


def _shoot(fire):
    S = _T()["shoot"]
    lean = 2 - (2 if fire else 0)
    d = _seat(lean=lean, twist=10, head_yaw=-9, head_pitch=3, use="carbine",
              weapon=dict(pos=(S["x"], S["y_fire"] if fire else S["y_aim"], S["z_fire"] if fire else S["z_aim"]),
                          yaw=S["yaw"], pitch=lean + (3 if fire else 0)),
              handR="grip", handL="grip", poleL=(0.9, -1, 0.2), poleR=(-0.6, -1, -0.3))
    if fire:
        d["overlay"] = "flash"
    return d


def _pistol(fire):
    """mounted officer: one-handed pistol, left hand keeps the reins (tune/poses.json cavalry.pistol)."""
    S = _T().get("pistol") or {"x": -7.5, "y_aim": 15.5, "y_fire": 16.1, "z_aim": 18.0, "z_fire": 16.5, "yaw": 2}
    lean = 1 - (2 if fire else 0)
    d = _seat(lean=lean, twist=14, head_yaw=-8, head_pitch=2, use="pistol",
              weapon=dict(pos=(S["x"], S["y_fire"] if fire else S["y_aim"], S["z_fire"] if fire else S["z_aim"]),
                          yaw=S["yaw"], pitch=lean + (7 if fire else 1)),
              handR="grip", poleR=(-0.5, -1, -0.2))
    if fire:
        d["overlay"] = "flash"
    return d


def pose_for(frame, d="s", e_extra=0.0, arm="carbine"):
    """-> dict(horse=hp, rider=rider pose, mode='seat'|'off'|'ground', extra placement keys).
    arm = "carbine" (rifleman) | "pistol" (mounted officer: aim/fire with the pistol)."""
    T = _T()
    if frame in ("idle", "spare"):
        return dict(horse=_stand(), rider=_seat(head_yaw=4), mode="seat")
    if frame == "idle_2":
        I2 = T["idle_2"]
        return dict(horse=_stand(neck=I2["neck"], head=I2["head"], tail=I2["tail"], neck_yaw=-6),
                    rider=_seat(head_yaw=-6, head_pitch=4, lean=2), mode="seat")
    if frame.startswith("walk_"):
        i = int(frame[-1])
        return dict(horse=walk_horse(i), rider=_seat(twist=(-3 if i in (1, 2) else 3)), mode="seat")
    if frame.startswith("gallop_") or frame.startswith("charge_"):
        i = int(frame[-1])
        GR = T["gallop_rider"]
        r = _seat(lean=GR["lean"], head_pitch=GR["head_pitch"], handL=("t", tuple(GR["reins"])),
                  handR=("t", (-2.5, 1.0, 13.0)), hipL=50, hipR=50, kneeL=88, kneeR=88)
        if frame.startswith("charge_"):
            r.update(use="sabre", weapon=_sabre(T["charge_arm"], CUT["charge"]), handR="grip", poleR=(-0.9, -0.3, -0.5),
                     lean=GR["lean"] - 4, head_pitch=GR["head_pitch"] + 4)
        return dict(horse=gallop_horse(i), rider=r, mode="seat", seat_dy=GR["bob"][i - 1])
    if frame in ("aim", "fire"):
        rider = _pistol(frame == "fire") if arm == "pistol" else _shoot(frame == "fire")
        return dict(horse=_stand(neck=50 if frame == "aim" else 54, head=52), rider=rider, mode="seat")
    if frame.startswith("melee_"):
        arm = T["melee_arms"][frame]
        tw = {"melee_1": -24, "melee_2": 18, "melee_3": 6}[frame]
        ln = {"melee_1": -4, "melee_2": 14, "melee_3": 6}[frame]
        rl = {"melee_1": 4, "melee_2": -10, "melee_3": -4}[frame]
        hp = _stand(neck={"melee_1": 50, "melee_2": 42, "melee_3": 46}[frame], head=50,
                    FL=(14, 40, 30) if frame == "melee_2" else (4, 0, 0))
        r = _seat(use="sabre", weapon=_sabre(arm, CUT[frame]), handR="grip", twist=tw, lean=ln, roll=rl,
                  poleR=(-0.9, -0.3, -0.6) if frame == "melee_1" else (-0.5, -1, 0.2))
        return dict(horse=hp, rider=r, mode="seat")
    if frame in ("hit", "hit_b"):
        hp = _stand(neck=62, head=36, pitch=4, tail=60, FL=(10, 20, 10), FR=(-4, 0, 0))
        return dict(horse=hp, rider=_seat(lean=-14, head_pitch=-16, roll=6, twist=10, handR=("sw", 60, 30, 40)), mode="seat")
    if frame == "fall":       # front knees buckle, the horse rolls to its right; rider pitched forward in the saddle
        hp = _stand(FL=(-24, 112, 40), FR=(-18, 104, 40), HL=(-6, 40, 10), HR=(-2, 46, 10), pitch=-13, roll=22,
                    neck=16, head=30, tail=50)
        return dict(horse=hp, rider=_seat(lean=18, head_pitch=20, roll=-10, handL=("t", (2.0, 0.0, 12.0)),
                                          handR=("sw", 80, 20, 40)), mode="seat")
    if frame == "dead":       # horse on its right side, legs out to the left; rider thrown down behind its back
        hp = _stand(FL=(18, 30, 20), FR=(4, 50, 30), HL=(-34, 30, 10), HR=(-18, 40, 20), neck=12, head=20, tail=80, lie=True)
        r = dict(death_pose("dead", None, None, None), weapon=None, drop=None, use=None)
        r["blood"] = (-2.0, 12.0)
        return dict(horse=hp, rider=r, mode="ground", place=(-29.0, 0.0, -2.0), place_yaw=8)
    if frame == "slip_b":     # rider hit, slumps back and to the right; horse shies
        hp = _stand(neck=58, head=40, neck_yaw=10, FL=(8, 20, 10))
        r = _seat(lean=-22, roll=-24, twist=-10, head_pitch=-24, head_roll=14, hipR=40, kneeR=60,
                  handL=("sw", 70, 20, 50), handR=("sw", 50, 10, 60))
        return dict(horse=hp, rider=r, mode="seat", off_roll=4)
    if frame == "fall_b":     # rider falling off the right side
        hp = _stand(neck=54, head=44, neck_yaw=8)
        r = dict(hipL=70, hipR=20, kneeL=60, kneeR=30, spread=20, lean=-10, roll=-6, head_pitch=-10, head_roll=20,
                 handL=("sw", 120, 10, 60), handR=("sw", 140, 20, 50), use=None)
        return dict(horse=hp, rider=r, mode="seat", off_roll=62, off_x=-12.0, off_y=0.0)
    if frame == "dead_b":     # rider on the ground right of the horse, horse stands with the head low
        hp = _stand(neck=20, head=64, tail=78)
        r = dict(death_pose("dead", None, None, None), weapon=None, drop=None, use=None, blood=(-3.0, 14.0))
        return dict(horse=hp, rider=r, mode="ground", place=(-22.0, 0.0, 2.0), place_yaw=-12, riderless=True)
    raise KeyError(frame)


# ---------------------------------------------------------------- assembly + render
def _rider_root(prims):
    """rider root (pelvis frame origin) in the build_figure output coords (pelvis rbox = prim 10, root + (0, 1.8, 0))."""
    pel = prims[10]
    assert pel.kind == "rbox" and abs(pel.dims[0] - 9.4 * B_TW) < 1e-6, "unexpected rider prim order"
    return pel.M[:3, 3] - pel.M[:3, 1] * 1.8


def build(unit, frame, d):
    """-> (prims, overlay) of horse + rider in horse ground coords (anchor = origin)."""
    with unit.active():
        P = pose_for(frame, d, arm="carbine" if "carbine" in unit.weapons or "pistol" not in unit.weapons else "pistol")
        rp = dict(P["rider"])
        use = rp.pop("use", None)
        weapon = unit.weapons.get(use) if use else None
        hb = H.build_horse(P["horse"], carbine=unit.weapons.get("carbine"), carbine_in_boot=(use != "carbine"),
                           sabre_sheathed=(use != "sabre"))
        prims = list(hb.prims)
        ov = []
        mode = P["mode"]
        slung = None
        if use != "carbine" and unit.weapons.get("carbine") is not None and tune.get("parts", "horse").get("carbine_carry", "slung") == "slung":
            slung = unit.weapons.get("carbine")
            rp["slung"] = _T()["slung"]
        rprims, rov = build_figure(rp, unit.kit, weapon, slung)
        if mode == "seat":
            root = _rider_root(rprims)
            G = hb.seat @ tr(0, P.get("seat_dy", 0.0), 0)
            if P.get("off_roll"):
                G = G @ tr(P.get("off_x", 0.0), P.get("off_y", 0.0), 0) @ rz(P["off_roll"])
            G = G @ tr(*(-root))
            rprims = [q.moved(G) for q in rprims]
            rov = [q.moved(G) for q in rov]
        else:
            px, py, pz = P["place"]
            G = tr(px, py, pz) @ ry(P.get("place_yaw", 0.0))
            rprims = [q.moved(G) for q in rprims]
            rov = []
        prims += rprims
        ov += rov
        prims += _straps(hb, rprims, mode == "seat" and not P.get("off_roll"), P.get("riderless") or mode != "seat")
    return prims, ov


def _straps(hb, rprims, seated, riderless):
    """stirrup leathers + irons and reins, attached to the rider's feet / left hand when seated."""
    Tp = tune.get("parts", "horse")
    rr = Tp["bridle"]["rein_r"]
    out = []
    feet = [q for q in rprims if q.tag == "foot"]
    hands = [q for q in rprims if q.tag in ("hand", "hand_L", "hand_R")]
    for k, top in enumerate(hb.stirrup_from):
        if seated and len(feet) == 2:
            Mf = feet[k].M
            iron = pt(Mf, (0, -2.6, 0.6))
            out.append(seg(top, iron + Mf[:3, 0] * (1 if k == 0 else -1) * 0.0 + np.array([0, 1.2, 0]), 0.5, "leather"))
            out.append(rbox(Mf @ tr(0, -2.6, 0.6), 2.6, 0.35, 1.2, "steel", r=0.2))
        else:
            down = hb.B[:3, 1]
            iron = top - down * 12.0
            out.append(seg(top, iron, 0.5, "leather"))
            out.append(rbox(tr(*iron), 1.2, 1.0, 1.2, "steel", r=0.3))
    if seated and hands:
        grip = hands[0].M[:3, 3]
        for b in hb.bits:
            out.append(seg(b, grip, rr, "leather"))
    else:
        for b in hb.bits:
            mid = 0.5 * (b + hb.pommel) - hb.B[:3, 1] * 3.0
            out.append(seg(b, mid, rr, "leather"))
            out.append(seg(mid, hb.pommel, rr, "leather"))
    for q in out:
        q.src = "mount"
    return out


def _render(prims, ov, d, res, lift, dx):
    return make_frame(prims, ov, d, res=res, lift=lift, dx_px=dx, ao=not getattr(res, "probe", False))


def render_unit_frame(unit, d, frame, res=None, fit=True, shift=None):
    res = res or LODS[0]
    prims, ov = build(unit, "idle" if frame == "spare" else frame, d)
    lift, dx = 0.0, 0.0
    if shift is not None:
        dx += shift[0]; lift += shift[1]
    f = _render(prims, ov, d, res, lift, dx)
    if not fit:
        return f
    fw, fh = res.fw, res.fh
    pad, nudge = res.edge_pad, res.edge_nudge
    hpad = max(1, pad // 2)
    for _ in range(10):                          # same auto-fit rule as voxel.render_figure
        x0, x1, y0, y1 = _bbox(f)
        ddy = ddx = 0.0
        if y1 - y0 >= fh - 2 * pad: ddy = (y0 + y1) / 2 - (fh - 1) / 2
        elif y1 > fh - 1 - pad: ddy = y1 - (fh - 1 - pad) + (nudge if y1 >= fh - 1 else 0)
        elif y0 < pad: ddy = y0 - pad - (nudge if y0 <= 0 else 0)
        if x1 - x0 >= fw - 2 * hpad: ddx = (fw - 1) / 2 - (x0 + x1) / 2
        elif x0 < hpad: ddx = hpad - x0 + (nudge if x0 <= 0 else 0)
        elif x1 > fw - 1 - hpad: ddx = (fw - 1 - hpad) - x1 - (nudge if x1 >= fw - 1 else 0)
        if abs(ddx) < 0.5 and abs(ddy) < 0.5:
            break
        lift += ddy; dx += ddx
        f = _render(prims, ov, d, res, lift, dx)
    return f
