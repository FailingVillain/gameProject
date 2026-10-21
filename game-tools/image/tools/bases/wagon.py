"""Horse-drawn supply wagon: two-wheel limber cart + two draught horses (cavalry horse model, no saddle) + a seated driver
(shared infantry body: field-grey tunic, Feldkappe).

Directions: the same 5 rows as the cavalry/infantry pipeline (s, se, e, ne, n; the game mirrors the western ones).
Frame (main LOD) 288x224, ground anchor = ground under the assembly's centre (ZC); LODs 144x112 and 72x56, rendered natively.
Outline (8,8,8): 3 / 2 / 1 px outer (main / half / quarter). Columns (anims/wagon.json): idle, idle_2, idle_3a/b (horses graze), move_1-4, start_1/2 (whip), supply_1-4 (crate passed down),
hit, hit_b1/b2 (horses rear), dead (cart on its side, crates spilled behind, horses down in front).

Wagon frame: z forward (towards the horses). Tunables: parts/wagon.py constants, tune/parts.json "horse" (scale per unit json),
tune/poses.json "cavalry" (horse gaits are shared with the cavalry unit).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import build_figure, make_frame, death_pose, _bbox, seg, rbox, ell, tr, pt, rx, ry, rz, _prim_ymin  # noqa: E402
import tune  # noqa: E402
from parts import horse as H  # noqa: E402
from parts import wagon as W  # noqa: E402
from bases import cavalry as C  # noqa: E402
from bases.cavalry import CavRes  # noqa: E402

ZM = 0.9                      # model zoom relative to the infantry (RTS compression: the team is ~60 units long)
XH, ZH = 11.0, 38.0            # draught horse centres (x = +-XH, barrel centre z)
ZC = 30.0                      # model z that sits over the frame anchor
OUTLINE = (8, 8, 8, 255)
OUT_PX = {2.0: 3, 1.0: 2, 0.5: 1}


class WagonRes(CavRes):
    FRAME = (288, 224)
    AY_FRAC = 0.80

    def __init__(self, pxs, fh_mult=1, extra_above=0):
        super().__init__(pxs, fh_mult, extra_above)
        self.zoom = self.zoom * ZM
        self.outline_px = OUT_PX.get(pxs) or max(1, int(round(pxs * 1.5)))
        self.line_px = max(1, int(round(pxs)))
        self.edge_pad = self.outline_px + 1
        self.edge_nudge = self.outline_px + 1

    def tall(self, k):
        h = self.fh
        r = WagonRes(self._pxs_arg, fh_mult=k, extra_above=h * (k - 1) // 2)
        r.probe = True
        return r


_K = 1.5 if __import__("os").environ.get("KD_RES96") == "1" else 1.0   # res96: LOD x1.5
LODS = [WagonRes(2.0 * _K), WagonRes(1.0 * _K), WagonRes(0.5 * _K)]          # 256, 128, 64
PREVIEW_FRAMES = ["idle", "move_1", "hit", "dead"]


def _hscale():
    return tune.get("parts", "horse").get("scale", 1.0)


# ---------------------------------------------------------------- horses + harness
def _horse(hp, x, z, side):
    """-> (prims, hb) horse with collar + pad at (x, z); side = +1 / -1 (outer flank)."""
    hb = H.build_horse(hp, tack=False)
    k = _hscale()
    Bs = hb.B @ np.diag([k, k, k, 1.0])
    pr = list(hb.prims)
    extra = [ell(Bs @ tr(0, 2.4, 16.8), 6.4, 7.4, 2.8, "leather"),                        # collar
             ell(Bs @ tr(0, 9.6, -1.0), 5.0, 1.0, 5.5, "leather"),                        # harness pad
             ell(Bs @ tr(0, 10.8, -1.0), 0.9, 1.1, 0.9, "steel"),
             ell(Bs @ tr(side * 6.0, 11.0, 15.0), 1.0, 1.0, 1.0, "steel")]                # hame knob
    for q in extra:
        q.src = "mount"
    pr += extra
    M = tr(x, 0, z)
    pr = [q.moved(M) for q in pr]
    hb.Bs = M @ Bs
    hb.M = M
    return pr, hb


def _traces(hbs, sides, hitch_y=19.0):
    out = []
    for hb, s in zip(hbs, sides):
        a = pt(hb.Bs, (s * 9.4, 3.0, 14.0))
        b = pt(hb.Bs, (s * 9.6, 3.5, -14.0))
        c = np.array([s * 13.6, hitch_y, W.HITCH[2] + 1.0])
        out += [seg(a, b, 0.42, "leather"), seg(b, c, 0.42, "leather")]
    return out


_COLLAR = {}


def _pole_end():
    k = _hscale()
    if k not in _COLLAR:
        hb = H.build_horse(C._stand(), tack=False)
        Bs = hb.B @ np.diag([k, k, k, 1.0])
        c = pt(Bs, (0, 2.4, 16.8))
        _COLLAR[k] = (0.0, float(c[1]) - 4.0, ZH + float(c[2]) - 5.5)
    return _COLLAR[k]


def _horse_poses(frame):
    """-> (left horse pose, right horse pose) for the frame."""
    st = C.pose_for("idle")["horse"]
    if frame in ("idle", "fire_1", "fire_2", "supply_1", "supply_2", "supply_3", "supply_4"):
        b = st
        if frame in ("supply_2", "supply_3"):
            b = dict(st, neck=40, head=58)
        return st, b
    if frame == "idle_2":
        return C.pose_for("idle_2")["horse"], dict(st, neck=44, head=56)
    if frame in ("idle_3a", "idle_3b"):      # grazing: heads down to the ground, chewing / tearing a mouthful
        a = dict(st, neck=-42, head=66, FL=(6, 0, 0))
        b = dict(st, neck=-34, head=58, FR=(6, 0, 0))
        if frame == "idle_3b":
            a, b = dict(a, neck=-34, head=56), dict(b, neck=-44, head=70)
        return a, b
    if frame.startswith("move_"):
        i = int(frame[-1])
        j = (i % 4) + 1
        return C.pose_for(f"walk_{i}")["horse"], C.pose_for(f"walk_{j}")["horse"]
    if frame == "start_1":                    # alert, heads up, weight set back
        a = dict(st, neck=60, head=44, tail=64, HL=(-26, 28, 0), HR=(-26, 28, 0), FL=(-2, 0, 0))
        return a, dict(a, neck=58)
    if frame == "start_2":                    # first step off
        w1, w2 = C.pose_for("walk_1")["horse"], C.pose_for("walk_2")["horse"]
        return dict(w1, neck=54, head=46, pitch=-3), dict(w2, neck=54, head=46, pitch=-3)
    if frame == "hit":
        a = C.pose_for("hit")["horse"]
        b = dict(C.pose_for("hit")["horse"], neck=56, FL=(4, 10, 6))
        return a, b
    if frame in ("hit_b1", "hit_b2"):         # rearing in fright
        k = 1 if frame == "hit_b1" else 2
        a = dict(st, pitch=(16, 30)[k - 1], neck=(62, 66)[k - 1], head=(40, 34)[k - 1], tail=60,
                 FL=(70, 105, 50), FR=(56, 96, 46), HL=(-22, 22, 0), HR=(-22, 22, 0))
        b = dict(a, pitch=(10, 22)[k - 1], FL=(50, 96, 44), FR=(66, 108, 50))
        return a, b
    raise KeyError(frame)


# ---------------------------------------------------------------- driver
def _driver_pose(frame):
    S = dict(hipL=64, hipR=64, kneeL=84, kneeR=84, spread=7, lean=4, poleL=(0.7, -1, -0.2), poleR=(-0.7, -1, -0.2), use=None)
    rein = (("t", (3.4, 3.0, 11.0)), ("t", (-3.4, 3.0, 11.0)))
    if frame in ("idle", "fire_1", "fire_2", "move_1", "move_3"):
        return dict(S, handL=rein[0], handR=rein[1], head_yaw=3, twist=-2 if frame == "move_1" else 2)
    if frame in ("idle_2", "move_2", "move_4"):
        return dict(S, handL=rein[0], handR=rein[1], head_yaw=-3, head_pitch=2, twist=2 if frame == "move_2" else -2)
    if frame in ("idle_3a", "idle_3b"):      # dozing in the seat, reins slack
        return dict(S, handL=rein[0], handR=rein[1], head_yaw=-4, head_pitch=14 if frame == "idle_3a" else 18, lean=8)
    if frame == "start_1":                    # right arm back, whip raised
        return dict(S, handL=rein[0], handR=("sw", 165, 10, 30), lean=-4, head_yaw=2, twist=-10)
    if frame == "start_2":                    # whip cracked forward over the horses
        return dict(S, handL=rein[0], handR=("sw", 70, 5, 15), lean=10, head_yaw=2, twist=-6)
    if frame == "supply_1":                   # lifts a crate from the bed
        return dict(S, twist=75, lean=14, head_yaw=-18, head_pitch=6, handL=("t", (5.0, 2.0, 7.5)), handR=("t", (-5.0, 2.0, 7.5)))
    if frame == "supply_2":                   # swings it over and hands it down
        return dict(S, twist=105, lean=6, head_yaw=-20, handL=("t", (5.6, 6.0, 8.5)), handR=("t", (-5.6, 6.0, 8.5)))
    if frame == "supply_3":                   # crate on the ground, arms still low
        return dict(S, twist=118, lean=12, head_yaw=-25, head_pitch=6, handL=("t", (5.6, 3.0, 13.5)), handR=("t", (-5.6, 3.0, 13.5)))
    if frame == "supply_4":                   # turning back to the reins
        return dict(S, twist=50, lean=4, head_yaw=-10, handL=rein[0], handR=rein[1])
    if frame in ("hit_b1", "hit_b2"):
        return dict(S, lean=-22 if frame == "hit_b1" else -28, head_pitch=-20, roll=-6, twist=-8,
                    handL=("sw", 100, 30, 50), handR=("sw", 120, 20, 40))
    if frame == "hit":
        return dict(S, lean=-16, head_pitch=-16, roll=7, twist=10, handL=("sw", 90, 20, 40), handR=("sw", 60, 30, 40))
    raise KeyError(frame)


def _driver(unit, frame, pose_override=None):
    """-> (prims placed in the wagon frame, hands, crate-frame fn or None)"""
    rp = dict(pose_override or _driver_pose(frame))
    rp.pop("use", None)
    rprims, rov = build_figure(rp, unit.kit, None, None)
    root = C._rider_root(rprims)
    G = tr(0, W.SEAT_Y + 3.0, W.SEAT_Z) @ tr(*(-root))
    rprims = [q.moved(G) for q in rprims]
    Tm = G @ tr(0, 26, 0) @ tr(0, 4.2, 0) @ rx(rp.get("lean", 0)) @ ry(rp.get("twist", 0)) @ rz(rp.get("roll", 0))
    return rprims, Tm


def _reins(rprims, hbs):
    hands = [q for q in rprims if q.tag == "hand"]
    out = []
    if len(hands) == 2:
        for h, hb, s in zip(hands, hbs, (1, -1)):
            tgt = hb.bits[0 if s > 0 else 1] + hb.M[:3, 3]
            out.append(seg(h.M[:3, 3], tgt, 0.3, "leather"))
    return out


# ---------------------------------------------------------------- tachanka (unit json "tachanka": true): gunner facing aft + Maxim on a post
def _towed_gun(frame, lying=False):
    """37 mm infantry gun (parts/guns.py) hitched behind the cart: trail forward, muzzle aft, shrunk to cart scale."""
    from parts import guns
    st = dict(elev=0.0, car_back=1.0, wheel_rot=3.0)
    if frame in ("fire_1", "fire_2"):
        st.update(recoil=-1.5 if frame == "fire_2" else 0.0, flash="a" if frame == "fire_2" else None)
    gp, _ov, _pts = guns.build("infantry_gun", st)
    K = 0.24
    M = tr(0, 0, -38.0) @ ry(180) @ np.diag([K, K, K, 1.0])
    out = []
    for q in gp:
        q = q.moved(M)
        if q.mat == "gun_wood":                 # wheel spokes/discs: lighter wood so the gun wheels read against the dark carriage
            q.mat = "wood"
        if q.paint:
            q.paint = [(f, {"gun_paint2": "canvas", "gun_tire": "steel"}.get(m_, m_)) for f, m_ in q.paint]
        out.append(q)
    return out


def _gunner_and_gun(unit, frame):
    """-> prims (gunner, ammo box, Maxim on a pintle post) in the wagon frame, gunner sits on a box behind the driver facing the tail."""
    from parts import kd_new
    S = dict(hipL=64, hipR=64, kneeL=84, kneeR=84, spread=7, lean=-3, poleL=(0.7, -1, -0.2), poleR=(-0.7, -1, -0.2), use=None,
             handL=("t", (3.0, 6.0, 10.0)), handR=("t", (-3.0, 6.0, 10.0)), head_yaw=0)
    if frame in ("hit", "hit_b1", "hit_b2"):
        S.update(lean=-14, head_pitch=-14)
    firing = frame in ("fire_1", "fire_2")
    if firing:                                  # gunner hunched over the gun, cheek down; fire_2 = recoil + flash
        S.update(lean=(-8, -9)[frame == "fire_2"], head_pitch=-6)
    rprims, _ = build_figure(S, unit.kit, None, None)
    root = C._rider_root(rprims)
    zg, yg = -6.0, W.BED_Y + 1.0 + 12.0 + 4.2
    G = tr(0, yg, zg) @ ry(180) @ tr(*(-root))
    prims = [q.moved(G) for q in rprims]
    Tm = G @ tr(0, 26, 0) @ tr(0, 4.2, 0) @ rx(S.get("lean", 0))
    gc = pt(Tm, (0, 6.0, 10.0))
    GM = tr(*gc) @ ry(180) @ tr(0, -3.6, 12.8)
    if firing:
        GM = GM @ tr(0, 0, -0.8 if frame == "fire_2" else 0.0)
    horse_gun = bool(unit.spec.get("horse_gun"))
    if not horse_gun:
        prims += kd_new.maxim_mount(GM)
    if frame == "fire_2" and not horse_gun:                       # muzzle flash at the Maxim muzzle (gun frame +z = muzzle, here pointing aft)
        from parts.guns import muzzle_flash
        prims += muzzle_flash(GM, 26.0, 0.55)
    top = W.BED_Y + 1.0
    if horse_gun:                               # horse artillery: infantry gun towed behind the cart, muzzle to the rear
        return prims + [rbox(tr(0, top + 6.0, zg), 6.0, 6.0, 3.6, "crate", r=0.6)] + _towed_gun(frame)
    prims.append(seg((gc[0], gc[1] - 5.0, gc[2] - 2.0), (gc[0], top, gc[2] - 2.0), 1.3, "steel"))          # pintle post
    prims.append(rbox(tr(0, top + 6.0, zg), 6.0, 6.0, 3.6, "crate", r=0.6))                              # ammo box seat
    prims.append(rbox(tr(8.5, top + 2.2, -18.0), 2.6, 2.2, 3.4, "olive", r=0.4))                          # spare box
    return prims


# ---------------------------------------------------------------- scenes
def _crate_on_ground(x, z, yaw=0, tilt=0.0, y=None):
    return W.crate(tr(x, (W.CR[1] if y is None else y), z) @ ry(yaw) @ rz(tilt))


def _whip(rprims, up):
    """braided lash from the driver's right hand: up = raised behind, else cracked forward."""
    hands = [q for q in rprims if q.tag == "hand"]
    if len(hands) != 2:
        return []
    h = hands[1].M[:3, 3]
    if up:
        a = h + np.array([0.0, 11.0, -3.0])
        b = a + np.array([0.0, 6.0, -9.0])
        return [seg(h, a, 0.6, "wood"), seg(a, b, 0.4, "wood")]
    a = h + np.array([0.0, 8.0, 10.0])
    b = a + np.array([0.0, -3.0, 10.0])
    c = b + np.array([0.0, 3.5, 6.0])
    return [seg(h, a, 0.6, "wood"), seg(a, b, 0.4, "wood"), seg(b, c, 0.3, "wood")]


def _scene_standing(unit, frame):
    wang = 0.0
    bob = 0.0
    penn = 0.0
    droop = frame in ("idle", "idle_2", "idle_3a", "idle_3b", "start_1") or frame.startswith("supply")
    if frame.startswith("move_"):
        i = int(frame[-1])
        wang = -(i - 1) * (360.0 / W.SPOKES) / 4.0
        bob = 0.45 if i in (2, 4) else 0.0
        penn = (-14, 6, 14, -6)[i - 1]
        droop = False
    elif frame == "idle_2":
        penn = 6
    elif frame == "start_2":
        wang, bob, penn, droop = -360.0 / W.SPOKES / 8.0, 0.3, -10, False
    elif frame == "start_1":
        penn = 4
    supply = frame.startswith("supply")
    pe = _pole_end()
    hpL, hpR = _horse_poses(frame)
    prL, hbL = _horse(hpL, XH, ZH, 1)
    prR, hbR = _horse(hpR, -XH, ZH, -1)
    tach = bool(unit.spec.get("tachanka"))
    wag = W.build_wagon(wang, supply=supply, pennant=penn, droop=droop, pole_end=pe, crates=not tach, tarp=not tach)
    drv, Tm = _driver(unit, frame)
    if tach:
        drv = drv + _gunner_and_gun(unit, frame)
    prims = prL + prR + [q.moved(tr(0, bob, 0)) for q in wag] + [q.moved(tr(0, bob, 0)) for q in drv]
    prims += _traces([hbL, hbR], (1, -1))
    prims += [q.moved(tr(0, bob, 0)) for q in _reins(drv, [hbL, hbR])]
    if frame.startswith("start_"):
        prims += [q.moved(tr(0, bob, 0)) for q in _whip(drv, frame == "start_1")]
    if frame == "supply_1":
        prims.append(W.crate(Tm @ tr(0, 5.0, 7.5)))
    elif frame == "supply_2":
        prims.append(W.crate(Tm @ tr(0, 6.0, 8.5)))
        prims.append(_crate_on_ground(26.0, -10.0, 0))
    elif frame in ("supply_3", "supply_4"):
        prims.append(_crate_on_ground(27.0, -14.5, -8))
    return prims


def _scene_hit(unit, frame="hit"):
    pe = _pole_end()
    hpL, hpR = _horse_poses(frame)
    prL, hbL = _horse(hpL, XH, ZH, 1)
    prR, hbR = _horse(hpR, -XH, ZH, -1)
    tach = bool(unit.spec.get("tachanka"))
    wag = W.build_wagon(0.0, pennant=18, droop=False, pole_end=pe, crates=not tach, tarp=not tach)
    drv, _ = _driver(unit, frame)
    if tach:
        drv = drv + _gunner_and_gun(unit, frame)
    grp = wag + drv
    if frame != "hit":                        # the cart is jerked forward: nose-up pitch about the axle
        Gt = tr(0, 0, W.AXLE_Z) @ rx(-(5.0 if frame == "hit_b1" else 9.0)) @ tr(0, 0, -W.AXLE_Z)
    else:
        P = np.array([-W.TRACK, 0.0, 0.0])
        Gt = tr(*P) @ rz(15.0) @ tr(*(-P))
    grp = [q.moved(Gt) for q in grp]
    low = min(_prim_ymin(q) for q in grp)
    grp = [q.moved(tr(0, -low, 0)) for q in grp]
    prims = prL + prR + grp + _traces([hbL, hbR], (1, -1))
    if frame == "hit" and not tach:
        prims.append(_crate_on_ground(-1.0, -44.0, 25, tilt=40, y=17.0))      # a crate sliding off the tailboard
    return prims


def _scene_dead(unit):
    """cart on its side (open bed facing -x), crates spilled out behind it, horses down in front, driver beside the wheels."""
    wag = W.build_wagon(14.0, supply=False, pennant=0.0, droop=True, crates=False, tarp=False, tailboard_down=True,
                        pole_end=(0.0, 14.5, 30.0))
    wf = [q.moved(rz(90.0)) for q in wag]
    low = min(_prim_ymin(q) for q in wf)
    wf = [q.moved(tr(0, -low, 0)) for q in wf]
    prims = list(wf)
    if unit.spec.get("horse_gun"):
        prims += [q.moved(tr(-14.0, 0.0, -3.0) @ ry(35)) for q in _towed_gun("dead")]
    elif unit.spec.get("tachanka"):
        from parts import kd_new
        prims += kd_new.maxim_mount(tr(-30.0, 4.0, -40.0) @ ry(70) @ rz(60))
    if not unit.spec.get("tachanka"):
        # a few crates still stacked against the open bed, the rest spilled out toward -x / behind (all on the ground)
        prims.append(ell(tr(-36.0, 1.4, -30.0) @ ry(12), 8.0, 1.5, 9.0, "tarp", paint=[(lambda p: np.abs(p[:, 2]) < 1.8, "team")]))
        spill = [(-26.0, -24.0, 8, 0), (-26.0, -33.0, -6, 0), (-38.0, -41.0, 35, 0), (-16.0, -44.0, 70, 0),
                 (-30.0, -50.0, -20, 0), (-44.0, -28.0, 5, 0)]
        for x, z, yw, tl in spill:
            prims.append(W.crate(tr(x, W.CR[1], z) @ ry(yw)))
        prims.append(W.crate(tr(-26.0, W.CR[1] * 3 + 0.2, -28.0) @ ry(-10) @ rx(14)))           # one tumbling off the pile
    # horses down in front: one dead on its side, one collapsed, both lined up with the broken pole
    hpA = C.pose_for("dead")["horse"]
    hpB = C.pose_for("fall")["horse"]
    prA, _ = _horse(hpA, -12.0, 46.0, 1)
    prB, _ = _horse(hpB, -29.0, 44.0, -1)
    prims += prA + prB
    prims.append(seg((-16.0, 1.0, 31.0), (-14.0, 1.2, 40.0), 0.7, "leather"))
    # driver thrown clear on the ground behind the wheels (+x side)
    r = dict(death_pose("dead", None, None, None), weapon=None, drop=None, use=None, blood=(-3.0, 14.0))
    rprims, _ov = build_figure(r, unit.kit, None, None)
    Gd = tr(14.0, 0.0, 2.0) @ ry(-50)
    prims += [q.moved(Gd) for q in rprims]
    return prims


def build(unit, frame, d):
    """-> (prims, overlay), anchor = ground under the assembly centre."""
    with unit.active():
        if frame in ("hit", "hit_b1", "hit_b2"):
            prims = _scene_hit(unit, frame)
        elif frame == "dead":
            prims = _scene_dead(unit)
        else:
            prims = _scene_standing(unit, frame)
    G = tr(0, 0, -(ZC - 12.0 if unit.spec.get("horse_gun") else ZC))   # horse artillery: gun side is long, centre the assembly
    return [q.moved(G) for q in prims], []


# ---------------------------------------------------------------- render
def _render(prims, ov, d, res, lift, dx):
    f = make_frame(prims, ov, d, res=res, lift=lift, dx_px=dx, ao=not getattr(res, "probe", False), outline=False)
    f.outline(color=OUTLINE, width=res.outline_px)
    return f


def render_unit_frame(unit, d, frame, res=None, fit=True, shift=None):
    res = res or LODS[0]
    prims, ov = build(unit, frame, d)
    lift, dx = 0.0, 0.0
    if shift is not None:
        dx += shift[0]; lift += shift[1]
    f = _render(prims, ov, d, res, lift, dx)
    if not fit:
        return f
    fw, fh = res.fw, res.fh
    pad, nudge = res.edge_pad, res.edge_nudge
    hpad = pad
    for _ in range(10):
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
