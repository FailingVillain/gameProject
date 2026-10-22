"""Rifleman pose set (shoulder weapon held in both hands).
Frames: idle, walk_1-4, aim/fire a/b/c, idle_alt, hit/fall/dead (v0.4 16 columns) +
        charge_1-4 (bayonet run), melee_1-3 (thrust), meleeb_1-2 (butt stroke), idle_lean (rifle grounded),
        go_prone, aim_prone, fire_prone, stagger_b/kneel_b/dead_b (death to knees), stagger_c/slump_c/dead_c (death backwards).
Weapon frame: pos in torso frame (x: +left, y up, z forward), yaw (+ = toward left), pitch (+ = muzzle up).
Pose extras: attach=["bayonet"] draws the weapon extra; e_pull = model units the weapon is pulled back toward the body in
the E view (rifle runs along screen x there)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from poses import parse, walk_legs, quick_legs, STANCE  # noqa: E402
from voxel import death_pose  # noqa: E402
import tune  # noqa: E402

# tunables (port arms, shoot / charge / walk weapon placement, e_pull / e_yaw) live in tune/poses.json -> "infantry"
E_LONG = tune.get("poses", "infantry", "e_long")   # long-rifle E-view compensation: std (e_pull_extra of the standard rifle), slide, yaw


def walk_pose(i):
    L = walk_legs(i)
    passing = i in (2, 4)
    W = tune.get("poses", "infantry", "walk_weapon")
    return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"],
                twist=L["twist"], lean=3, head_pitch=-3, lift=3,
                weapon=dict(pos=(W["pos"][0], W["pos"][1] + (W["passing_dy"] if passing else 0.0), W["pos"][2]), yaw=W["yaw"],
                            pitch=(W["pitch_passing"] if passing else (W["pitch_first"] if i == 1 else W["pitch"]))),
                handR="grip", handL=("sw", L["sw"], 12, 6))


SHOULDER = dict(hand=(-8, 16, 8.0), pitch=32, yaw=180.0, bob=0.5)    # tunables of the shoulder carry (rifle on the right shoulder, butt forward-down)


def _grip_weapon(weapon, hand, yaw, pitch, dy=0.0):
    """weapon frame (yaw/pitch) whose right grip sits at `hand` (+dy bob)."""
    import numpy as np
    from voxel import _frame_mat, pt
    from parts.rifles import BOLT_RIFLE
    weapon = weapon or BOLT_RIFLE
    spec = dict(pos=(0, 0, 0), yaw=yaw, pitch=pitch)
    g = pt(_frame_mat(spec), weapon.gripR)
    h = np.array(hand) + (0, dy, 0)
    return dict(spec, pos=tuple(float(v) for v in (h - g)))


def shoulder_pose(i, weapon=None):
    """walk_shoulder_1-4: walk legs, rifle resting on the right shoulder (butt forward-down in the right hand), left arm swings."""
    L = walk_legs(i)
    S = tune.get("poses", "infantry", "shoulder") if "shoulder" in tune.get("poses", "infantry") else SHOULDER
    passing = i in (2, 4)
    return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"], lean=3, head_pitch=-3, lift=3,
                weapon=_grip_weapon(weapon, S["hand"], S["yaw"], S["pitch"] + (1.5 if passing else 0.0), S["bob"] if passing else 0.0),
                handR="grip", handL=("sw", L["sw"], 12, 6), poleR=(-0.9, -1, -0.2))


def quick_pose(i):
    """walk_quick_1-4 (quick step): longer stride, forward lean, rifle at high port across the chest in both hands (derived from walk)."""
    L = quick_legs(i)
    W = dict(tune.get("poses", "infantry", "port_arms"))
    W["pos"] = (W["pos"][0], W["pos"][1] + (0.8 if i in (2, 4) else 0.0), W["pos"][2])
    return dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=L["twist"], lean=L["lean"],
                head_pitch=L["head_pitch"], lift=L["lift"], weapon=W, handR="grip", handL="grip", poleL=(0.9, -1, 0.3), poleR=(-0.6, -1, -0.2))


def walkfire_pose(i, fire, dz=0.0, dy=0.0):
    """walk_fire_1-4 (i = gait phase 1..4 as walk_1..4, fire = muzzle flash: frames 2 and 4): rifle at the hip / low shoulder, both hands on it.
    i = 0 keeps the old two-frame behaviour (walkfire_a / walkfire_b)."""
    L = walk_legs(i if i else (1 if not fire else 3))
    W = dict(pos=(-6.2, 15.0 + dy + (0.4 if fire else 0.0), 29 + dz - (0.6 if fire else 0.0)), yaw=-4.0, pitch=2.0 + (2.5 if fire else 0.0))
    d = dict(hipL=L["hipL"], hipR=L["hipR"], kneeL=L["kneeL"], kneeR=L["kneeR"], twist=(6 if not fire else 8), lean=6 - (2 if fire else 0), head_pitch=2, head_yaw=-8,
             lift=3, weapon=W, handR="grip", handL="grip", poleL=(0.9, -1, 0.3), poleR=(-0.6, -1, -0.2), push=8)
    if i in (2, 4):
        d["lift"] = 3.6                                # passing phase: body rides up a little, as in walk_2 / walk_4
    # no baked-in muzzle flash: the game draws a flash overlay only on real shots
    return d


WALKFIRE_SWING = (-55, -15, 20)      # E view: muzzle swung toward the camera so the flash stays inside the cell at 64/32


def shoot_pose(var, fire):
    st = dict(STANCE[var])
    lean = st["lean"] - (3 if fire else 0)
    S = tune.get("poses", "infantry", "shoot")
    z = S["z_fire"] if fire else S["z_aim"]
    y = S["y_fire"] if fire else S["y_aim"]
    if var == "c":
        y -= 0.5
    d = dict(st, lean=lean, twist=8 if var != "c" else 5, head_pitch=4 - 0.7 * lean + (-1 if fire else 0), head_yaw=-9,
             weapon=dict(pos=(S["x"], y, z), yaw=S["yaw"], pitch=lean + (2 if fire else 0) + S.get("pitch", 0.0)),
             handR="grip", handL="grip", poleL=(0.9, -1, 0.2), poleR=(-0.6, -1, -0.3), push=16)
    if var == "c":
        d["poleL"] = (0.3, -0.6, 0.5)       # left elbow rests toward the raised knee
    if fire:
        d["overlay"] = "flash"
    return d


# ---------------------------------------------------------------- bayonet charge (4-phase run, longer stride than walk)
def charge_pose(i):
    # 1 contact L front, 2 flight (R swings through), 3 contact R front, 4 flight (L swings)
    legs = {1: dict(hipL=34, hipR=-30, kneeL=14, kneeR=72, twist=-7, lift=3),
            2: dict(hipL=4, hipR=22, kneeL=24, kneeR=88, twist=0, lift=6),
            3: dict(hipL=-30, hipR=34, kneeL=72, kneeR=14, twist=7, lift=3),
            4: dict(hipL=22, hipR=4, kneeL=88, kneeR=24, twist=0, lift=6)}[i]
    C = tune.get("poses", "infantry", "charge_weapon")
    bob = C["bob"] if i in (2, 4) else 0.0
    return dict(legs, lean=14, head_pitch=-10, spread=5,
                weapon=dict(pos=(C["pos"][0], C["pos"][1] + bob, C["pos"][2]), yaw=C["yaw"], pitch=C["pitch"]),        # low port, bayonet forward
                handR="grip", handL="grip", poleL=(0.8, -1, 0.3), poleR=(-0.6, -1, -0.2),
                attach=["bayonet"], push=4)


# ---------------------------------------------------------------- N57: fix bayonet (1 reach for the blade at the left hip, 2 fit it on the muzzle, 3 locked, ready)
def fixb_pose(i):
    from voxel import _frame_mat, pt
    legs = dict(hipL=8, hipR=-2, kneeL=14, kneeR=8, spread=6, lift=3)
    if i == 1:        # rifle held in the right hand at port, left hand drops to the left hip (scabbard)
        return dict(legs, lean=4, head_pitch=14, head_yaw=10, weapon=tune.get("poses", "infantry", "port_arms"),
                    handR="grip", handL=("t", (9.0, 6.0, 6.5)), poleL=(0.9, -1, 0.1), poleR=(-0.6, -1, -0.3))
    if i == 2:        # rifle tilted up-left in front of the chest, left hand at the muzzle, blade going on
        w = dict(pos=(-7.0, 9.0, 11.0), yaw=38, pitch=62)
        m = pt(_frame_mat(dict(w)), (0, 0.5, 24.0))
        return dict(legs, lean=3, head_pitch=14, head_yaw=14, weapon=w, handR="grip", handL=("t", tuple(float(v) for v in m)),
                    poleL=(0.9, -1, 0.3), poleR=(-0.6, -1, -0.3), attach=["bayonet"])
    C = tune.get("poses", "infantry", "charge_weapon")            # locked: low port, bayonet forward, both hands on the rifle
    return dict(legs, lean=6, head_pitch=4, weapon=dict(C), handR="grip", handL="grip", poleL=(0.8, -1, 0.3), poleR=(-0.6, -1, -0.2),
                attach=["bayonet"], push=4)


# ---------------------------------------------------------------- melee: bayonet thrust / butt stroke
MELEE = {
    "melee_1": dict(hipL=22, hipR=-16, kneeL=22, kneeR=14, spread=8, lean=2, twist=-16, head_pitch=2,              # wind-up
                    weapon=dict(pos=(-9.9, 7.4, 1.6), yaw=2, pitch=3.5), handR="grip", handL="grip",
                    poleL=(0.9, -1, 0.1), poleR=(-0.7, -1, -0.3), attach=["bayonet"]),
    "melee_2": dict(hipL=42, hipR=-26, kneeL=48, kneeR=8, spread=8, lean=20, twist=12, head_pitch=-4,               # thrust / lunge
                    weapon=dict(pos=(-3.3, 8.8, 24.4), yaw=0, pitch=6.7), handR="grip", handL="grip",
                    poleL=(0.9, -1, 0.3), poleR=(-0.6, -1, -0.2), attach=["bayonet"], push=10),
    "melee_3": dict(hipL=26, hipR=-18, kneeL=28, kneeR=12, spread=8, lean=8, twist=-4, head_pitch=0,               # recover
                    weapon=dict(pos=(-8.3, 8.6, 8.8), yaw=15.5, pitch=19), handR="grip", handL="grip",
                    poleL=(0.9, -1, 0.2), poleR=(-0.6, -1, -0.3), attach=["bayonet"], push=4),
    "meleeb_1": dict(hipL=18, hipR=-14, kneeL=18, kneeR=12, spread=8, lean=-4, twist=-10, head_pitch=4, roll=4,      # butt wind-up: rifle across the front, butt up-right
                     weapon=dict(pos=(-9.0, 11.0, 12.5), yaw=115, pitch=-25), handR="grip", handL="grip",
                     poleL=(0.9, -1, 0.5), poleR=(-0.8, -1, 0.4), attach=["bayonet"], push=2),
    "meleeb_2": dict(hipL=32, hipR=-22, kneeL=36, kneeR=8, spread=8, lean=12, twist=6, head_pitch=-4, roll=-4,       # butt strike: butt driven up beside the head, muzzle down-forward
                     weapon=dict(pos=(-9.0, 16.0, 14.0), yaw=0, pitch=-70), handR="grip", handL=("t", (-6.5, 13.0, 13.5)),
                     poleL=(0.9, -1, 0.5), poleR=(-0.8, -1, 0.4), attach=["bayonet"], push=6),
}

# ---------------------------------------------------------------- idle variants
IDLE_LEAN = dict(hipL=2, hipR=-3, kneeL=3, kneeR=4, spread=8, lean=-2, twist=5, head_pitch=2, head_yaw=8,
                 weapon=dict(pos=(-11.6, -11.3, 3.8), yaw=90, pitch=84.5),            # butt on the ground beside the right foot, leaning 10 deg in
                 handR=("t", (-8.0, 7.0, 3.5)), handL=("sw", 2, 8, 5), poleR=(-0.9, -0.6, 0.4))

# ---------------------------------------------------------------- prone
GO_PRONE = dict(tilt=38, lean=14, head_pitch=-26, hipL=58, hipR=22, kneeL=66, kneeR=36, spread=12,
                weapon=dict(pos=(-9.8, 1.6, 10.3), yaw=7.7, pitch=-24.5), handR="grip", handL=("sw", 115, 10, 22))


def prone_pose(fire):
    """N56 (2026-10-19): proper prone aim. Butt plate in the right shoulder pocket, rifle level just above the ground and nearly under the
    face (x close to the head line), head lowered and rolled toward the stock (cheek weld), both elbows on the ground, left hand on the
    fore-end. E view: the muzzle is swung toward the camera about the butt (tune e_swing.prone) instead of sliding the whole rifle back
    along the body (that slide made the butt look tucked under the armpit)."""
    PW = tune.get("poses", "infantry", "prone_weapon")          # {x, z, dy}: rifle x (toward the head line) / height above the chest / shift along the body
    y = 19.0 + PW["dy"]
    d = dict(tilt=90, lean=0, head_pitch=tune.get("poses", "infantry", "prone_head_pitch"), head_yaw=-6, head_roll=tune.get("poses", "infantry", "prone_head_roll"),
             hipL=2, hipR=0, kneeL=78, kneeR=98, spread=14,
             weapon=dict(pos=(PW["x"], y + 0.1, PW["z"]), yaw=-0.6, pitch=93.3 if not fire else 94.3),
             handR="grip", handL="grip", poleL=(0.9, -0.2, 1.0), poleR=(-0.9, -0.2, 1.0), e_long_only=True)
    if PW.get("scale"):
        d["wscale"] = PW["scale"]                  # shortened rifle in prone (generic: all rifles, scaled along the barrel about the stock end)
    if fire:
        d["overlay"] = "flash"
    return d


# ---------------------------------------------------------------- extra deaths (hit -> held stagger -> collapse -> dead)
DEATHS = {
    # b: knees buckle, drops to the knees, falls forward over them
    "stagger_b": dict(hipL=6, hipR=-6, kneeL=28, kneeR=30, spread=9, lean=-7, roll=4, head_pitch=26, head_yaw=6,
                      weapon=dict(pos=(-9.5, 1.8, 7.4), yaw=7.8, pitch=-37.7), handR="grip", handL=("t", (-2.0, 9.0, 7.0)), push=2),
    "kneel_b": dict(hipL=0, hipR=2, kneeL=96, kneeR=94, spread=8, lean=22, roll=-3, head_pitch=32, head_yaw=-8,
                    weapon=dict(pos=(-10.0, -2.9, 8.4), yaw=12.2, pitch=-89.4), handR="grip", handL=("t", (-1.0, 8.0, 7.0)), push=3),
    "dead_b": dict(tilt=90, lean=10, head_pitch=2, head_yaw=-26, head_roll=10, hipL=42, hipR=30, kneeL=104, kneeR=92, spread=9,
                   weapon=None, drop=dict(pos=(-14.1, 2.8, 7.8), yaw=-26.2, pitch=0),
                   handR=("sw", 62, 30, 20), handL=("sw", 132, 10, 28), blood=(2.0, 12.0), push=3),
    # c: thrown back, arms out, folds backwards, ends on the back face-up
    "stagger_c": dict(hipL=-14, hipR=10, kneeL=10, kneeR=26, spread=10, lean=-22, twist=-8, roll=-6, head_pitch=-20, head_roll=10,
                      weapon=dict(pos=(-9.0, 4.0, 6.0), yaw=20, pitch=30), handR="grip", handL=("sw", 60, 15, 55), push=3),
    "slump_c": dict(tilt=-45, lean=-10, hipL=20, hipR=36, kneeL=42, kneeR=62, spread=11, head_pitch=-16, head_roll=12,
                    weapon=dict(pos=(-10.0, 8.0, 4.0), yaw=40, pitch=50), handR="grip", handL=("sw", 92, 10, 60)),
    "dead_c": dict(tilt=-90, lean=-4, head_pitch=-6, head_yaw=-34, head_roll=16, hipL=14, hipR=30, kneeL=34, kneeR=60, spread=12,
                   weapon=None, drop=dict(pos=(-12.0, 3.0, 1.0), yaw=72, pitch=0),
                   handR=("sw", 118, 22, 24), handL=("sw", 24, 12, 30), blood=(4.0, -10.0), push=2),
}


def pose_for(frame, d="s", e_extra=0.0):
    """pose for a frame; d = direction (s/se/e/ne/n). E-view tweaks (rifle runs along screen x there):
    e_pull = weapon pulled toward the body (model units; e_extra from the unit json is added for long weapons),
    e_yaw = extra weapon yaw toward the camera (foreshortens the weapon without moving it into the body)."""
    return apply_e_view(_tuned(frame, _pose(frame)), d, e_extra)


def _tuned(frame, p):
    """e_pull / e_yaw from tune/poses.json (group keys, see the _doc there)."""
    T = tune.get("poses", "infantry")
    pull = T["e_pull"]
    if frame.startswith(("walkfire_", "walk_fire_")):
        key = "shoot"
        p["e_pull"] = 0.5
    elif frame.startswith(("aim_", "fire_")) and frame not in ("aim_prone", "fire_prone"):
        key = "shoot"
    elif frame.startswith("charge_"):
        key = "charge"
    else:
        key = frame if frame in ("melee_2", "go_prone") else None
    if key in pull and not frame.startswith(("walkfire_", "walk_fire_")):
        p["e_pull"] = pull[key]
    sw = T.get("e_swing", {}).get(key)
    if frame.startswith(("walkfire_", "walk_fire_")):
        sw = WALKFIRE_SWING
    if sw:
        p["e_swing"] = tuple(sw)
    if frame in ("aim_prone", "fire_prone") and "prone" in T["e_yaw"]:
        p["e_yaw"] = T["e_yaw"]["prone"]
    if frame in ("aim_prone", "fire_prone") and T.get("e_swing", {}).get("prone"):
        p["e_swing"] = tuple(T["e_swing"]["prone"])
        p["e_swing_field"] = "swing_z"          # prone: the rifle lies along torso y, a plain yaw only spins it about its own axis -> swing about torso z (= world vertical)
    if frame in ("aim_prone", "fire_prone"):
        if T.get("ne_slide", {}).get("prone"): p["ne_slide"] = T["ne_slide"]["prone"]
        if T.get("ne_swing", {}).get("prone"): p["ne_swing"] = tuple(T["ne_swing"]["prone"])
    if frame in ("aim_prone", "fire_prone") and T.get("e_view_yaw", {}).get("prone"):
        p["e_view_yaw"] = T["e_view_yaw"]["prone"]
    if frame in ("aim_prone", "fire_prone") and T.get("ne_view_yaw", {}).get("prone"):
        p["ne_view_yaw"] = T["ne_view_yaw"]["prone"]
    if frame in ("aim_prone", "fire_prone") and T.get("e_slide", {}).get("prone"):
        p["e_slide"] = T["e_slide"]["prone"]
    return p


def apply_e_view(p, d, e_extra=0.0):
    """e_pull: weapon slid back along z; e_yaw: extra yaw about the weapon origin; e_swing = (deg, pivot_z[, pitch]): yaw (+pitch) about a
    point on the weapon axis (pivot_z in weapon-local z, e.g. the butt) so the butt stays on the shoulder while the muzzle
    swings toward the camera (shortens the E-view silhouette without pushing the stock into the body)."""
    pull = p.pop("e_pull", 0.0); eyaw = p.pop("e_yaw", 0.0); swing = p.pop("e_swing", None)
    vyaw = p.pop("e_view_yaw", 0.0)              # prone E: whole figure turned toward the camera so the 128 px cell holds boots + muzzle flash
    if vyaw and d == "e":
        p["view_yaw"] = p.get("view_yaw", 0.0) + vyaw
    nvyaw = p.pop("ne_view_yaw", 0.0)            # prone NE (opt-in per unit via tune): same idea, for units whose long prone overflows the cell diagonally
    if nvyaw and d == "ne":
        p["view_yaw"] = p.get("view_yaw", 0.0) + nvyaw
    sfield = p.pop("e_swing_field", "yaw")
    slide = p.pop("e_slide", 0.0)                # prone: rifle slid back toward the feet in the E view (muzzle flash would leave the frame)
    long_only = p.pop("e_long_only", False)      # pose with no base pull: only rifles longer than the standard one (e_extra above E_LONG std) get pulled in
    nes = p.pop("ne_slide", 0.0); nesw = p.pop("ne_swing", None)
    if d == "ne" and p.get("weapon") and (nes or nesw):      # prone NE: the rifle (diagonal on screen) also overflows the cell -> slide back / swing about torso z
        d = "e"; pull = eyaw = 0.0; slide = nes; swing = nesw; sfield = "swing_z"; long_only = False
    if d == "e" and p.get("weapon"):
        w = dict(p["weapon"])
        if long_only and e_extra > E_LONG["std"]:
            x, y, z = w["pos"]; w["pos"] = (x, y - (e_extra - E_LONG["std"]) * E_LONG["slide"], z)   # prone rifle lies along the body (torso y): slide it back toward the feet
            w["yaw"] = w.get("yaw", 0) - (e_extra - E_LONG["std"]) * E_LONG["yaw"]
        if slide:
            x, y, z = w["pos"]; w["pos"] = (x, y - slide, z)
        if pull:
            x, y, z = w["pos"]; w["pos"] = (x, y, z - pull - e_extra)
        if eyaw:
            w["yaw"] = w.get("yaw", 0) + eyaw
        if swing and swing[0]:
            from voxel import _frame_mat, pt
            piv = (0.0, 0.0, swing[1])
            before = pt(_frame_mat(w), piv)
            w[sfield] = w.get(sfield, 0) + swing[0]
            if len(swing) > 2:                 # optional muzzle-up correction (the camera is elevated: a muzzle swung
                w["pitch"] = w.get("pitch", 0) + swing[2]   # toward it reads as pointing down on screen)
            after = pt(_frame_mat(w), piv)
            w["pos"] = tuple(float(v) for v in (w["pos"][0] + before[0] - after[0], w["pos"][1] + before[1] - after[1],
                                                 w["pos"][2] + before[2] - after[2]))
        p["weapon"] = w
    return p


def _pose(frame):
    from bases import infantry_motions as _m          # motion drafts batch 1 (idle_rest/look/adjust/stretch, salute, present_arms)
    mp = _m.pose(frame)
    if mp is not None:
        return mp
    if frame in ("idle", "spare"):
        return dict(hipL=10, hipR=-2, kneeL=18, kneeR=8, lean=5, spread=6, weapon=tune.get("poses", "infantry", "port_arms"), handR="grip", handL="grip", pole=None)
    if frame == "idle_alt":     # rifle thrust up, muzzle to the sky, right arm raised, left arm free
        return dict(hipL=3, hipR=-3, kneeL=2, kneeR=2, lean=-2, head_pitch=-5, twist=-4,
                    weapon=tune.get("poses", "infantry", "idle_alt_weapon"), handR="grip",
                    handL=("sw", 18, 30, 12), poleR=(-0.8, -1, -0.4))
    if frame == "idle_lean":
        return dict(IDLE_LEAN)
    if frame in ("hit", "fall", "dead"):
        return death_pose(frame, dict(pos=(-8.1, 5, 10.5), yaw=14.3, pitch=-7.9), dict(pos=(-9.4, -0.6, 9.0), yaw=7.2, pitch=-37.6),
                          dict(pos=(-15, 3.0, 2), yaw=25, pitch=0))
    if frame in MELEE:
        return dict(MELEE[frame])
    if frame in DEATHS:
        return dict(DEATHS[frame])
    if frame == "go_prone":
        return dict(GO_PRONE)
    if frame in ("aim_prone", "fire_prone"):
        return prone_pose(frame == "fire_prone")
    if frame.startswith("fixb_"):
        return fixb_pose(int(frame.split("_")[1]))
    if frame.startswith("charge_"):
        return charge_pose(int(frame.split("_")[1]))
    if frame.startswith("walk_quick_"):
        return quick_pose(int(frame.rsplit("_", 1)[1]))
    if frame.startswith("walk_shoulder_"):
        return shoulder_pose(int(frame.rsplit("_", 1)[1]))
    if frame in ("walkfire_a", "walkfire_b"):
        return walkfire_pose(0, frame == "walkfire_b")
    if frame.startswith("walk_fire_"):
        i = int(frame.rsplit("_", 1)[1])
        return walkfire_pose(i, i in (2, 4))
    kind, v = parse(frame)
    if kind == "walk":
        return walk_pose(v)
    if kind in ("aim", "fire"):
        return shoot_pose(v, kind == "fire")
    raise KeyError(frame)
