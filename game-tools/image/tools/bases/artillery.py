"""Artillery base: a gun (parts/guns.py) + its crew (shared infantry rig, field-grey uniform + Feldkappe).

scene(kind, frame, kit, opts) -> (prims, overlay, info) in the gun frame (origin = ground under the axle centre, z = firing
direction when deployed); the renderer rotates it for the 5 drawn directions.  Frames (anims/artillery.json):
  idle                      deployed, crew at their stations (gunner at the handwheel, loader with a shell, others at trail / ammo)
  fire_0 fire_1 fire_2      brace + lanyard pulled, FLASH + barrel / carriage recoil + flinch, barrel returning
  load_1 load_2             breech open, loader brings the shell to the chamber, rams it
  unpack_1 unpack_2         limbered -> deployed: the gun is swung round (trail crew heave the trail), crew walk to stations
                            (pack = the same two frames played backwards)
  move_1..4                 limbered gun on the march: the gun is turned round (barrel to the rear, trail forward in the travel
                            direction) and the crew pull it by drag ropes on the trail eye / push on wheels or shield
  hit dead                  crew staggering + burst at the carriage / wrecked gun (one wheel gone), crew lying
HORSE-TEAM HOOK: in the move frames the trail eye (pts["eye"], reported in info["hitch"] and written to the layout json by
artillery_unit) is where the limber / team trace attaches.  A horse-drawn variant = same move_* gun prims with the pullers replaced by
a limber + horse team at info["hitch"] (see MOVE_PULLERS / pull_crew below: skip them when opts["team"] is set).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from voxel import (build_figure, death_pose, ell, rbox, seg, tr, rx, ry, rz, pt, cyl, _prim_ymin, Prim)  # noqa: E402
from poses import walk_legs  # noqa: E402
from parts import guns  # noqa: E402
import tune  # noqa: E402

TORSO_Y = 30.2                       # torso-frame origin height of build_figure (root 26 + 4.2)
MOVE_PULLERS = {"howitzer": ("trail", "ammo"), "field_gun": ("ammo", "loader"), "infantry_gun": ("loader",)}

STAND = dict(hipL=4, hipR=-3, kneeL=6, kneeR=5, lean=3, spread=5)
KNEEL = dict(hipL=68, hipR=-4, kneeL=82, kneeR=100, lean=11, spread=8)
EARS = dict(handL=("t", (7.5, 21.5, 2.0)), handR=("t", (-7.5, 21.5, 2.0)), poleL=(0.8, -0.3, -0.6), poleR=(-0.8, -0.3, -0.6))
FREE_L = ("sw", 8, 22, 9)
FREE_R = ("sw", 8, 22, 9)


class Stn:
    """crewman station in the gun frame."""

    def __init__(self, x, z, yaw):
        self.x, self.z, self.yaw = x, z, yaw
        self.T = tr(x, 0, z) @ ry(yaw)

    def w(self, p):
        return pt(self.T, p)


def face(frm, to):
    """yaw (deg) that turns a figure at frm (x, z) to look at to (x, z)."""
    return math.degrees(math.atan2(to[0] - frm[0], to[1] - frm[1]))


def place(kit, pose, stn, hands=None):
    """crewman prims in gun coordinates. hands = {"L": gun-frame point | None, "R": ...} solved with a few IK correction passes."""
    pose = dict(pose)
    want = {}
    inv = np.linalg.inv(stn.T)
    for side, w in (hands or {}).items():
        if w is None:
            continue
        want[side] = (inv @ np.append(np.asarray(w, float), 1.0))[:3]
        pose["hand" + side] = ("t", tuple(want[side] - np.array([0.0, TORSO_Y, 0.0])))
    prims = None
    gap = 0.0
    for _ in range(5):
        prims, _o = build_figure(pose, kit, None)
        if not want:
            break
        hp = sorted((p for p in prims if p.tag in ("hand", "hand_L", "hand_R")), key=lambda p: p.tag)   # hand_L, hand_R (main tags) or legacy "hand" pair
        got = {"L": hp[0].M[:3, 3], "R": hp[-1].M[:3, 3]}
        Rt = (rx(pose.get("lean", 0)) @ ry(pose.get("twist", 0)) @ rz(pose.get("roll", 0)))[:3, :3]
        done = True
        gap = max(float(np.linalg.norm(want[side] - got[side])) for side in want)
        for side in want:
            err = want[side] - got[side]
            if np.linalg.norm(err) > 0.5:
                done = False
                pose["hand" + side] = ("t", tuple(np.array(pose["hand" + side][1]) + Rt.T @ err))
        if done:
            break
    GAP.append(gap)                      # final hand-to-target miss of this figure (tuning aid, see tools/collide.py)
    return [p.moved(stn.T) for p in prims]


GAP = []


def hands_by_side(stn, pa, pb):
    """assign two gun-frame points to the figure's L / R hand (L = his +x side)."""
    ax = (stn.T[:3, :3] @ np.array([1.0, 0, 0]))
    if np.dot(np.asarray(pa) - np.asarray(pb), ax) >= 0:
        return {"L": pa, "R": pb}
    return {"L": pb, "R": pa}


def carry_pts(stn, kind, base_l=(4.0, 25.0, 9.0), tilt=(-0.45, 0.45, 0.77)):
    """a shell carried across the chest in the crewman's local frame -> (base, tip, hands) in gun coordinates."""
    L = guns.spec(kind)["shell"]["len"]
    d = np.array(tilt, float)
    d /= np.linalg.norm(d)
    b = np.array(base_l, float)
    t = b + d * L
    bw, tw = stn.w(b), stn.w(t)
    return bw, tw, {"L": bw + (tw - bw) * 0.3, "R": bw + (tw - bw) * 0.72}


def rope(a, b, wreck=False):
    return seg(a, b, 0.45, "leather" if not wreck else "gun_burn2")


# ---------------------------------------------------------------- crew behaviour
def station(kind, role, opts):
    s = tune.get("poses", "artillery", kind)["stations"][role]
    return Stn(*s)


def phase_crew(kind, phase, kit, pts, G, wreck=False):
    """deployed crew for phases ready / brace / flinch / recover / load_a / load_b -> (prims, extras)."""
    A = tune.get("poses", "artillery", kind)
    roles = A["crew"]
    out = []
    shell_pts = []
    ammo_t = None
    for role in roles:
        s = Stn(*A["stations"][role])
        if role == "gunner":
            if phase in ("brace", "flinch"):
                back = 10.0 if kind == "howitzer" else 5.0
                s = Stn(s.x - 1.0, s.z - back, s.yaw - 8)
                L0 = pts["lanyard"]
                pull = 2.0 if phase == "flinch" else 0.0
                P = L0 + np.array([6.0 if kind != "infantry_gun" else 3.0, -6.0, -(14.0 if kind != "infantry_gun" else 11.0) - pull])
                pose = dict(KNEEL if kind == "infantry_gun" else STAND, lean=-6 if phase == "flinch" else 0, head_pitch=6, handL=("t", (6.5, 21.5, 2.0)),
                            poleL=(0.8, -0.3, -0.6))
                out += place(kit, pose, s, {"R": P})
                extras = [rope(L0, P)]
                shell_pts.append(("rope", extras))
            else:
                pose = dict(KNEEL if kind == "infantry_gun" else STAND, lean=(10 if kind == "infantry_gun" else 6), head_yaw=-10, handR=FREE_R, poleR=(-0.7, -1, -0.2))
                out += place(kit, pose, s, {"L": pts["handwheel"]})
        elif role == "loader":
            if kind == "infantry_gun":
                base = dict(KNEEL, hipL=62, kneeL=88, lean=9)
            else:
                base = dict(STAND)
            if phase == "ready":
                bw, tw, hh = carry_pts(s, kind)
                out += place(kit, dict(base, lean=base["lean"] + 5), s, hh)
                shell_pts.append(("shell", (bw, tw)))
            elif phase in ("brace", "flinch"):
                out += place(kit, dict(base, lean=(-9 if phase == "flinch" else -3), head_pitch=8, **EARS), s)
            elif phase == "recover":
                s2 = Stn(s.x, s.z + 2, s.yaw)
                out += place(kit, dict(base, lean=base["lean"], head_pitch=-2, handL=("sw", 24, 30, 8), handR=("sw", 24, 30, 8), poleL=(0.5, -1, 0.3), poleR=(-0.5, -1, 0.3)), s2)
            else:                                                # load_a / load_b
                ls = float(guns.spec(kind)["shell"]["len"])
                B = pts["B"]
                z_tip = -3.5 if phase == "load_a" else 2.5
                z_base = z_tip - ls
                tip, bas = pt(B, (0, 0, z_tip)), pt(B, (0, 0, z_base))
                s2 = Stn(s.x, s.z - (4.0 if kind == "howitzer" else 3.0) + (4.0 if phase == "load_b" else 0.0), s.yaw)
                hh = {"L": bas + (tip - bas) * 0.28 + np.array([1.5, 0, 0]), "R": bas + (tip - bas) * 0.28 - np.array([1.5, 0, 0])}
                hh = hands_by_side(s2, hh["L"], hh["R"])
                out += place(kit, dict(base, lean=base["lean"] + (14 if phase == "load_b" else 8), head_pitch=-4), s2, hh)
                shell_pts.append(("shell", (bas, tip)))
        else:                                                    # trail / ammo men
            if role == "trail":
                if phase in ("brace", "flinch"):
                    out += place(kit, dict(STAND, lean=-4, head_pitch=6, **EARS), s)
                else:
                    pa = pts["trail_L"] + np.array([0.0, 0.5, 2.6])
                    pb = pts["trail_L"] + np.array([0.0, 0.5, -2.6])
                    out += place(kit, dict(STAND, lean=8), s, hands_by_side(s, pa, pb))
            else:                                                # ammo man holding the next round
                if phase in ("brace", "flinch"):
                    out += place(kit, dict(STAND, lean=-4, head_pitch=6, **EARS), s)
                else:
                    bw, tw, hh = carry_pts(s, kind)
                    out += place(kit, dict(STAND, lean=7), s, hh)
                    shell_pts.append(("shell", (bw, tw)))
    return out, shell_pts


def pull_crew(kind, kit, pts, k, ground_bob, travel):
    """move cycle: pullers walk in front of the trail eye holding drag ropes, pushers walk behind wheel / shield.
    k = 1..4 walk phase. pts are already in the (reversed) world frame."""
    A = tune.get("poses", "artillery", kind)
    T = A["travel"]
    pull_roles = MOVE_PULLERS[kind]
    out, extra = [], []
    E = pts["eye"]
    n = len(pull_roles)
    for i, role in enumerate(pull_roles):
        sx = 0.0 if n == 1 else (T["pull_x"] if i == 0 else -T["pull_x"])
        ph = k if i % 2 == 0 else (k + 1) % 4 + 1
        L = walk_legs(ph)
        s = Stn(sx, E[2] + T["pull_z"], 0.0)
        P = np.array([sx, 26.0, s.z - 6.0])
        pose = dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"] * 0.4, lean=14, head_pitch=-6, spread=5)
        out += place(kit, pose, s, {"L": P + np.array([0.8, 0, 0]), "R": P + np.array([-0.8, 0, 0])})
        extra.append(rope(E, P))
    push_roles = [r for r in A["crew"] if r not in pull_roles]
    for j, role in enumerate(push_roles):
        side = 1 if j % 2 == 0 else -1
        ph = (k + 1) % 4 + 1 if j % 2 == 0 else k
        L = walk_legs(ph)
        has_shield = guns.spec(kind)["shield"] is not None
        if has_shield:
            hp = pts["shield_front_L"] if side > 0 else pts["shield_front_R"]
            s = Stn(hp[0] * T.get("shield_xk", 0.8), hp[2] + T["push_z"], 0.0)
            hh = {"L": hp + np.array([1.8, 0, 0]), "R": hp - np.array([1.8, 0, 0])}
            if j > 0:                               # extra pushers take a wheel instead
                hp = pts["wheel_top_L"] if pts["wheel_top_L"][0] * side > 0 else pts["wheel_top_R"]      # (the travelling gun is turned round: L / R swap)
                s = Stn(side * T["push_x"], hp[2] - 8.0, 0.0)
                hh = {"R" if side > 0 else "L": hp + np.array([0, 0, 0.0])}
        else:
            hp = pts["wheel_top_L"] if pts["wheel_top_L"][0] * side > 0 else pts["wheel_top_R"]      # (the travelling gun is turned round: L / R swap)
            s = Stn(side * T["push_x"], hp[2] + T["push_z"] - 4.0, 0.0)
            hh = {"R" if side > 0 else "L": hp + np.array([0, 0, 0.0])}      # only the wheel-side hand reaches the rim (the other one crosses his body)
        pose = dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"] * 0.4, lean=16, head_pitch=-8, spread=6)
        if len(hh) == 1:                                               # free hand swings
            free = "L" if "R" in hh else "R"
            pose["hand" + free] = ("sw", L["sw"] * (1 if free == "L" else -1), 14, 8)
        out += place(kit, pose, s, hh)
    return out, extra


def unpack_crew(kind, kit, pts, k):
    """transition crew (k = 1, 2): pullers swing the trail end (hands on the trail handles), the others walk toward their stations."""
    A = tune.get("poses", "artillery", kind)
    T = A["travel"]
    pull_roles = MOVE_PULLERS[kind]
    out = []
    end = pts["eye"]
    dirout = np.array([end[0], 0.0, end[2]])
    dirout /= max(np.linalg.norm(dirout), 1e-6)
    sidev = np.array([dirout[2], 0.0, -dirout[0]])
    for i, role in enumerate(pull_roles):
        h = pts["handle_L"] if i == 0 else pts["handle_R"]
        if len(pull_roles) == 1:
            h = (pts["handle_L"] + pts["handle_R"]) / 2
        off = sidev * (7.0 if len(pull_roles) > 1 else 0.0) * (1 if i == 0 else -1)
        pos = np.array([end[0], 0.0, end[2]]) + dirout * T.get("unpack_out", 14.0) + off
        s = Stn(pos[0], pos[2], face((pos[0], pos[2]), (end[0], end[2])))
        L = walk_legs(1 if i == 0 else 3)
        pose = dict(hipL=L["hipL"] * 0.7, hipR=L["hipR"] * 0.7, kneeL=L["kneeL"], kneeR=L["kneeR"], lean=16, head_pitch=-6, spread=6)
        out += place(kit, pose, s, hands_by_side(s, h + sidev * 1.6, h - sidev * 1.6))
    push_roles = [r for r in A["crew"] if r not in pull_roles]
    for j, role in enumerate(push_roles):
        sd = A["stations"][role]
        side = 1 if sd[0] >= 0 else -1
        a = (side * T["push_x"], T["push_z"] - 12.0, 0.0)                # where the pusher walked while marching
        t = T.get("unpack_t", (0.55, 0.2))[k - 1]                                         # fraction of the way back toward the march position
        x, z, yaw = (sd[n] * (1 - t) + a[n] * t for n in range(3))
        L = walk_legs(1 if (j + k) % 2 == 0 else 3)
        pose = dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"] * 0.5, lean=6, head_pitch=-3, spread=5,
                    handL=("sw", L["sw"], 14, 8), handR=("sw", -L["sw"], 14, 8))
        out += place(kit, pose, Stn(x, z, yaw))
    return out


# ---------------------------------------------------------------- wreck
def wreck_scene(kind, kit, hit=False):
    A = tune.get("poses", "artillery", kind)
    S = guns.spec(kind)
    W = S["wheel"]
    if hit:
        prims, ov, pts = guns.build(kind, dict(elev=A["elev_idle"], car_back=1.0, wheel_rot=3.0), wreck=False)
        burst = [ell(tr(W["x"] + 4, W["r"] * 0.8, -2), 9, 9, 9, "flash_edge"), ell(tr(W["x"] + 3, W["r"] * 0.9, -1), 6.6, 6.6, 6.6, "flash"),
                 ell(tr(W["x"] + 2, W["r"], 0), 3.8, 3.8, 3.8, "flash_core")]
        return prims, burst, pts
    prims, ov, pts = guns.build(kind, dict(elev=-5.0, recoil=-2.5, lost_wheel=1, wheel_rot=5.0), wreck=True)
    tilt = 14.0 if kind != "infantry_gun" else 18.0
    G = tr(-W["x"], 0, 0) @ rz(-tilt) @ tr(W["x"], 0, 0) @ ry(8)
    prims = [p.moved(G) for p in prims]
    ymin = min(_prim_ymin(p) for p in prims)
    L = tr(0, -ymin, 0)
    prims = [p.moved(L) for p in prims]
    # the blown-off wheel lies flat beside the gun
    m = guns.mat_fn(True)
    Mh = tr(W["x"] + 17.0, 2.4, -10.0) @ rz(90) @ rx(8)
    prims += (guns.wheel_spoked if W["style"] == "spoked" else guns.wheel_disc)(Mh, W, 12.0, m)
    prims.append(ell(tr(4, 0.3, -12), W["x"] * 1.6, 0.5, 42.0, "scorch"))
    prims.append(ell(tr(-W["x"] * 0.7, 0.3, 6), 14, 0.5, 18.0, "scorch"))
    prims += guns.ammo_box(kind, wreck=True)
    ls = guns.spec(kind)["shell"]
    for i, (x, z, a) in enumerate([(-20, -6, 20), (-30, -20, 70), (-12, -44, 40)]):
        prims += guns.shell_prims((x, ls["r"], z), (x + math.sin(math.radians(a)) * ls["len"], ls["r"], z + math.cos(math.radians(a)) * ls["len"]), kind, wreck=True)
    # no static smoke column on the wreck (the game does not animate it)
    return prims, [], pts


def fallen_crew(kind, kit, hit):
    """crew staggering (hit) or lying (dead) around the gun."""
    A = tune.get("poses", "artillery", kind)
    out = []
    W = guns.spec(kind)["wheel"]
    for i, role in enumerate(A["crew"]):
        sx, sz, syaw = A["stations"][role]
        if hit:
            variants = [dict(death_pose("hit", {}, {}, {})), dict(death_pose("hit", {}, {}, {}), lean=-20, twist=-10, head_pitch=-20, hipL=-14, hipR=10),
                        dict(death_pose("hit", {}, {}, {}), lean=10, roll=-6, head_pitch=24, kneeL=30, kneeR=26), dict(death_pose("hit", {}, {}, {}), twist=-14, roll=-5)]
            pose = variants[i % 4]
            pose["weapon"] = None
            pose.pop("handR", None); pose.pop("handL", None)
            pose.update(handL=("sw", 60, 25, 40), handR=("sw", 40, 30, 25))
            out += [p.moved(Stn(sx, sz, syaw).T) for p in build_figure(pose, kit, None)[0]]
        else:
            from bases.infantry import DEATHS
            kinds = ["dead_b", "dead_c", "dead", "dead_b"]
            nm = kinds[i % 4]
            pose = dict(DEATHS[nm]) if nm in DEATHS else dict(death_pose("dead", {}, {}, {}))
            pose["weapon"] = None
            pose.pop("drop", None)
            pr = build_figure(pose, kit, None)[0]
            # lie along the gun's z axis, head away from the gun: dead_b head at +z (yaw 180 -> head toward -z), dead_c head at -z
            pos = LYING.get(kind, {}).get(role, (sx, sz, 0.0))
            out += [p.moved(tr(pos[0], 0, pos[1]) @ ry(pos[2])) for p in pr]
    return out


LYING = {
    "howitzer": {"gunner": (31.0, -4.0, 180.0), "loader": (3.0, -34.0, 0.0), "trail": (24.0, -58.0, 160.0), "ammo": (-26.0, -26.0, 200.0)},
    "field_gun": {"gunner": (27.0, -4.0, 180.0), "loader": (4.0, -32.0, 0.0), "ammo": (-18.0, -52.0, 20.0)},
    "infantry_gun": {"gunner": (3.0, -24.0, 0.0), "loader": (-22.0, -14.0, 150.0)},
}


# ---------------------------------------------------------------- scene
FIRE_FRAMES = {"fire_0": ("brace", None, 0.0, 0.0), "fire_1": ("flinch", "a", 1.0, 1.0), "fire_2": ("recover", "b", 0.45, 0.4),
               "load_1": ("load_a", None, 0.0, 0.0), "load_2": ("load_b", None, 0.0, 0.0)}


def scene(kind, frame, kit, opts=None):
    """-> (prims, overlay, info). info = {"hitch": gun-frame point of the trail eye in this frame (move frames: the world point)}."""
    A = tune.get("poses", "artillery", kind)
    prims, ov, info = [], [], {}
    center = (opts or {}).get("center", (0.0, 0.0))
    psi = 0.0                                                # gun swing (deg) about the origin; the footprint centre turns with it
    ei, ef = A["elev_idle"], A["elev_fire"]
    if frame == "idle":
        g, ov, pts = guns.build(kind, dict(elev=ei))
        crew, sp = phase_crew(kind, "ready", kit, pts, None)
        prims = g + crew
    elif frame in FIRE_FRAMES:
        phase, fl, rec, cb = FIRE_FRAMES[frame]
        st = dict(elev=ef, recoil=rec * A["recoil"], car_back=cb * A["car_back"], flash=fl, breech_open=phase in ("load_a", "load_b"))
        g, ov, pts = guns.build(kind, st)
        crew, sp = phase_crew(kind, phase, kit, pts, None)
        prims = g + crew
        for tag, v in sp:
            if tag == "rope":
                prims += v
    elif frame in ("unhitch_1", "unhitch_2") and (opts or {}).get("tow"):
        from bases import towing
        u = int(frame[-1])
        T_ = A.get("unhitch", {"sep": [34.0, 90.0], "drop": [0.55, 1.0], "gait": [2, 4]})
        prims, tinfo = towing.tow_scene(kind, kit, T_["gait"][u - 1], opts, place, Stn, sep=T_["sep"][u - 1], drop=T_["drop"][u - 1], gun_k=1)
        info["hitch"] = tinfo["hitch"]
        center = tinfo["gun_center"]
    elif frame in ("move_1", "move_2", "move_3", "move_4") and (opts or {}).get("tow"):
        from bases import towing
        prims, tinfo = towing.tow_scene(kind, kit, int(frame[-1]), opts, place, Stn)
        info["hitch"] = tinfo["hitch"]
        psi = 0.0
        center = tinfo["gun_center"]                           # anchored on the gun: no jump between deploy and march frames
    elif frame in ("move_1", "move_2", "move_3", "move_4"):
        k = int(frame[-1])
        psi = 180.0
        bob = 0.7 if k in (2, 4) else 0.0
        g, ov, pts = guns.build(kind, dict(elev=A.get("elev_travel", 3.0), wheel_rot=12.0 * (k - 1)))
        Gr = tr(0, bob, 0) @ ry(180)
        g = [p.moved(Gr) for p in g]
        pts = {n: (pt(Gr, v) if np.ndim(v) == 1 else Gr @ v) for n, v in pts.items()}
        crew, ropes = pull_crew(kind, kit, pts, k, bob, True)
        prims = g + crew + ropes
        info["hitch"] = pts["eye"]
    elif frame in ("unpack_1", "unpack_2"):
        k = int(frame[-1])
        psi = {1: 125.0, 2: 55.0}[k]
        g, ov, pts = guns.build(kind, dict(elev={1: A.get("elev_travel", 3.0), 2: ei * 0.6}[k], wheel_rot=10.0 * k))
        Gr = ry(psi)
        g = [p.moved(Gr) for p in g]
        pts = {n: (pt(Gr, v) if np.ndim(v) == 1 else Gr @ v) for n, v in pts.items()}
        prims = g + unpack_crew(kind, kit, pts, k)
        info["hitch"] = pts["eye"]
    elif frame == "hit":
        prims, ov, pts = wreck_scene(kind, kit, hit=True)
        prims = prims + fallen_crew(kind, kit, True)
    elif frame == "dead":
        prims, ov, pts = wreck_scene(kind, kit)
        prims = prims + fallen_crew(kind, kit, False)
    else:
        raise KeyError(frame)
    if frame == "idle" or frame in FIRE_FRAMES:
        for tag, v in sp:
            if tag == "shell":
                prims += guns.shell_prims(v[0], v[1], kind)
        prims += guns.ammo_box(kind)
    # shift so the footprint centre is the model origin (= frame anchor)
    cw = pt(ry(psi), (center[0], 0.0, center[1]))
    C = tr(-cw[0], 0, -cw[2])
    prims = [p.moved(C) for p in prims]
    ov = [p.moved(C) for p in ov]
    if "hitch" in info:
        info["hitch"] = pt(C, info["hitch"])
    return prims, ov, info
