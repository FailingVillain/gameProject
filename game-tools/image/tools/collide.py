"""3D interpenetration check (model space, no rendering): weapon / gear primitives vs the body.

Every frame x direction is built with the unit's own pose / weapon selection. Surface samples of the "movers" are tested
against the body parts they must not enter:
    weapon (src=weapon)  -> torso, head (+headgear), legs, arms, gear (pack, scabbard ...)
    gear   (src=gear)    -> legs, arms, head          (gear vs torso is its mounting contact, not checked)
Calibration (depth histogram over the five infantry/officer units, see image/CLAUDE.md):
  * a sample counts only when it is deeper than TOL (1.5 model units ~ 3 px at 128) -> surface contact is ignored;
  * leg samples hidden inside the torso / pelvis (thigh root) are ignored;
  * a hand that holds something (pose hand spec "grip" or ("t", ...)) may wrap it: samples within GRIP_R of that hand's
    centre are ignored for that hand / forearm / elbow (fingers + forearm around the stock or the hilt).
Result per frame/dir: {"weapon>torso": max_depth, ...}.

    python tools/collide.py <unit> [frame ...] [--hist]     # compact hit list (+ depth histogram of all contacts)
make_unit.py runs it automatically. Rule: a finished unit has zero hits.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from voxel import build_figure, _sdf  # noqa: E402

TOL = 1.5                        # model units of penetration tolerated (contact / outline-level overlap)
REACH_R = 11.0                   # multi-figure scenes: arm parts ignore mount contact this close to their own hand
GRIP_R = 6.0                     # holding hand: ignore hand / forearm / elbow contact within this radius of the hand centre
CHECKS = {"weapon": ("torso", "head", "leg", "arm", "gear"), "gear": ("leg", "arm", "head")}


def part_of(p):
    """body part class of a prim: weapon | gear | head | leg | arm | torso"""
    if p.src in ("weapon", "gear"):
        return p.src
    if p.src == "headgear" or p.tag == "head":
        return "head"
    t = p.tag or ""
    if t.startswith("leg_") or t == "foot":
        return "leg"
    if t.startswith(("upper_", "elbow_", "forearm_", "hand_")):
        return "arm"
    return "torso"


# ---------------------------------------------------------------- surface samples of a prim (local coords)
def _samples(p, n_ring=10):
    k = p.kind
    out = []
    if k in ("box", "rbox"):
        hx, hy, hz = p.dims[:3]
        for u in np.linspace(-1, 1, 5):
            for v in np.linspace(-1, 1, 5):
                out += [(hx, u * hy, v * hz), (-hx, u * hy, v * hz), (u * hx, hy, v * hz), (u * hx, -hy, v * hz),
                        (u * hx, v * hy, hz), (u * hx, v * hy, -hz)]
        out.append((0.0, 0.0, 0.0))
    elif k == "ell":
        rx_, ry_, rz_ = p.dims
        n_len = 9
        for i in range(n_len):
            th = math.pi * (i + 0.5) / n_len
            for j in range(n_ring):
                ph = 2 * math.pi * j / n_ring
                out.append((rx_ * math.sin(th) * math.cos(ph), ry_ * math.cos(th), rz_ * math.sin(th) * math.sin(ph)))
        out.append((0.0, 0.0, 0.0))
    elif k in ("cyl", "cap"):
        r, hl = p.dims
        for y in np.linspace(-hl, hl, max(9, int(2 * hl / 1.2) + 1)):
            for j in range(n_ring):
                ph = 2 * math.pi * j / n_ring
                out.append((r * math.cos(ph), y, r * math.sin(ph)))
            out.append((0.0, y, 0.0))
        out += [(0.0, hl + (r if k == "cap" else 0), 0.0), (0.0, -hl - (r if k == "cap" else 0), 0.0)]
    elif k == "cone":
        hl, r0, r1 = p.dims
        for y in np.linspace(-hl, hl, max(9, int(2 * hl / 1.2) + 1)):
            r = r0 + (r1 - r0) * (y + hl) / (2 * hl)
            for j in range(n_ring):
                ph = 2 * math.pi * j / n_ring
                out.append((r * math.cos(ph), y, r * math.sin(ph)))
            out.append((0.0, y, 0.0))
    return np.array(out, float)


def _to_world(p, X):
    return X @ p.M[:3, :3].T + p.M[:3, 3]


def _to_local(p, P):
    return (P - p.M[:3, 3]) @ p.M[:3, :3]


# ---------------------------------------------------------------- penetration depth (>0 inside) of world points in a prim
def depth(p, P):
    X = _to_local(p, P)
    k = p.kind
    if k == "box":
        h = np.array(p.dims[:3])
        return (h - np.abs(X)).min(1)
    if k in ("rbox", "cone"):
        return -_sdf(p, X)
    if k == "ell":
        r = np.array(p.dims, float)
        q = np.sqrt(((X / r) ** 2).sum(1))
        d = (1 - q) * r.min()
        if p.clipy is not None:
            d = np.minimum(d, X[:, 1] - p.clipy)
        return d
    r, hl = p.dims
    rho = np.sqrt(X[:, 0] ** 2 + X[:, 2] ** 2)
    if k == "cyl":
        return np.minimum(hl - np.abs(X[:, 1]), r - rho)
    yc = np.clip(X[:, 1], -hl, hl)
    return r - np.sqrt(rho ** 2 + (X[:, 1] - yc) ** 2)


def contacts(prims, holding=()):
    """-> {"mover>part": max depth} over all contacts (depth > 0) of weapon / gear samples in forbidden parts.
    holding = sides ("L"/"R") whose hand holds something (grip exclusion)."""
    parts = [(p, part_of(p)) for p in prims]
    hands = {p.tag[-1]: p.M[:3, 3] for p in prims if (p.tag or "").startswith("hand_")}
    out = {}
    for mover, targets in CHECKS.items():
        movers = [p for p, c in parts if c == mover]
        if not movers:
            continue
        P = np.concatenate([_to_world(p, _samples(p)) for p in movers])
        hidden = None                      # samples inside the torso / pelvis: the thigh root there is not visible
        for b, c in parts:
            if c not in targets:
                continue
            near = np.linalg.norm(P - b.M[:3, 3], axis=1) < b.radius() + 1.0
            if c == "leg":
                if hidden is None:
                    hidden = np.zeros(len(P), bool)
                    for t, ct in parts:
                        if ct == "torso":
                            hidden |= depth(t, P) > 0
                near &= ~hidden
            if c == "arm":
                side = b.tag[-1]
                if side in holding and not b.tag.startswith("upper_"):
                    near &= np.linalg.norm(P - hands[side], axis=1) > GRIP_R
            if not near.any():
                continue
            dmax = float(depth(b, P[near]).max())
            if dmax > 0:
                key = f"{mover}>{c}"
                out[key] = max(out.get(key, 0.0), dmax)
    return out


# ---------------------------------------------------------------- multi-figure scenes (crew units, artillery, cavalry)
MOUNT_TARGETS = ("torso", "head", "leg", "arm")
MOUNT_IGNORE = {}          # unit name -> set of "mount>part" keys that are intended contacts (seat, mount, ...)


def contacts_multi(prims, allow=None, soft=None):
    """Scene with several figures (Prim.fig set by voxel.build_figure) plus non-figure prims (gun, carriage, horse, ropes = "mount").
    -> {"weapon>torso[1]": depth, ...}: per figure the usual weapon/gear checks, plus mount samples vs every figure's body.
    allow = {part: {mover materials}}: furniture that legitimately touches that part (saddle / stirrups vs rider legs).
    soft = {(part, mover material): tol}: contact deeper than tol only (rider's thighs gripping the horse barrel).
    Intended contacts are excluded: anything within GRIP_R of either hand of that figure (grips, handles, reins, rope),
    thigh roots hidden in the torso."""
    figs = {}
    mount = []
    for p in prims:
        (figs.setdefault(p.fig, []) if p.fig is not None else mount).append(p)
    out = {}
    order = {f: i for i, f in enumerate(sorted(figs))}
    for f, fp in figs.items():
        for k, v in contacts(fp, ("L", "R")).items():
            out[f"{k}[{order[f]}]"] = v
    if mount:
        smp = [_to_world(p, _samples(p)) for p in mount]
        P = np.concatenate(smp)
        MAT = np.concatenate([[p.mat] * len(x) for p, x in zip(mount, smp)])
        for f, fp in figs.items():
            parts = [(p, part_of(p)) for p in fp]
            hands = [p.M[:3, 3] for p in fp if (p.tag or "").startswith("hand_")]
            near_hand = np.zeros(len(P), bool)          # within GRIP_R of a hand: grip / rope end / handle
            reach_hand = np.zeros(len(P), bool)         # within REACH_R: the arm that reaches onto a wheel rim / trail handle
            for h in hands:
                dh = np.linalg.norm(P - h, axis=1)
                near_hand |= dh <= GRIP_R
                reach_hand |= dh <= REACH_R
            hidden = np.zeros(len(P), bool)
            for t, ct in parts:
                if ct == "torso":
                    hidden |= depth(t, P) > 0
            for b, c in parts:
                if c not in MOUNT_TARGETS:
                    continue
                near = np.linalg.norm(P - b.M[:3, 3], axis=1) < b.radius() + 1.0
                if c == "leg":
                    near &= ~hidden
                if c == "arm":
                    near &= ~reach_hand
                if c == "torso":
                    near &= ~near_hand                  # hand-held rope / belt / handle: the hand sits against the body
                if allow and c in allow:
                    near &= ~np.isin(MAT, list(allow[c]))
                for (sp, sm), stol in (soft or {}).items():
                    if sp == c:
                        sel = near & (MAT == sm)
                        if sel.any():
                            sd = float(depth(b, P[sel]).max())
                            near &= ~sel
                            if sd > stol:
                                key = f"mount>{c}[{order[f]}]"
                                out[key] = max(out.get(key, 0.0), sd)
                if not near.any():
                    continue
                dmax = float(depth(b, P[near]).max())
                if dmax > 0:
                    key = f"mount>{c}[{order[f]}]"
                    out[key] = max(out.get(key, 0.0), dmax)
    return out


def scan_multi(unit, frames=None, dirs=None):
    """unit.collide_prims(frame, d) -> prims of the whole scene; crew / artillery scenes do not depend on d."""
    out = []
    for fr in (frames or unit.frames):
        for d in (dirs or getattr(unit, "collide_dirs", unit.dirs)):
            out.append((fr, d, contacts_multi(unit.collide_prims("idle" if fr == "spare" else fr, d), getattr(unit, "collide_allow", None), getattr(unit, "collide_soft", None))))
    return out


def scene_contacts(prims):
    """two-figure scene without mount props: contacts_multi (weapon/gear per figure) + each figure's weapon/gear/body vs the OTHER figure:
    the other figure's prims are fed in as 'mount' (hands within GRIP_R / REACH_R of the receiving figure's own hands are ignored = collar grip)."""
    out = contacts_multi(prims)
    figs = sorted({p.fig for p in prims if p.fig is not None})
    for f in figs:
        other = []
        for p in prims:
            if p.fig == f:
                other.append(p)
            elif p.fig is not None and not (p.tag or "").startswith("hand_"):      # the other man's fists grip the collar: not a penetration
                q = p.moved(np.eye(4)); q.fig = None; other.append(q)
        for k, v in contacts_multi(other).items():
            if k.startswith("mount>"):
                key = "cross" + k[5:-3] + f"[{figs.index(f)}]"                # cross>part[n]: the other man pushes into figure n
                out[key] = max(out.get(key, 0.0), v)
    return out


def _holding(pose):
    return tuple(s for s in ("L", "R") if pose.get("hand" + s) == "grip"
                 or (isinstance(pose.get("hand" + s), tuple) and pose["hand" + s][0] == "t"))


def scan_unit(unit, frames=None, dirs=None):
    """-> [(frame, dir, {"mover>part": max depth})] for every frame/dir (all contacts, no threshold)."""
    if not hasattr(unit, "collide_prims") and hasattr(getattr(unit, "base", None), "build"):      # cavalry: horse + rider
        unit.collide_prims = lambda name, d: unit.base.build(unit, name, d)[0]
        unit.collide_allow = {"leg": {"leather", "brass", "steel"}}      # saddle, stirrup leathers + irons under the rider's legs
        unit.collide_soft = {("leg", "horse"): 2.5}                         # thighs grip the barrel
    if hasattr(unit, "collide_prims"):
        return scan_multi(unit, frames, dirs)
    out = []
    for fr in (frames or unit.frames):
        name = "idle" if fr == "spare" else fr
        for d in (dirs or unit.dirs):
            with unit.active():
                pose = unit.base.pose_for(name, d, unit.e_pull_extra)
                weapon = unit.weapons.get(pose.pop("use", None), None) if unit.weapons else unit.weapon
                hold = _holding(pose)
                if "second" in pose:                                  # two-figure scene (drag_wounded): per-figure checks + cross-figure checks
                    from bases import infantry_motions as _m
                    prims, _ov = _m.build_scene(pose, unit.kit, weapon or unit.weapon)
                    out.append((fr, d, scene_contacts(prims)))
                    continue
                prims, _ov = build_figure(pose, unit.kit, weapon)
            out.append((fr, d, contacts(prims, hold)))
    return out


def check_unit(unit, frames=None, dirs=None, tol=TOL):
    """-> [(frame, dir, {"mover>part": depth})] only for frame/dirs with a contact deeper than tol."""
    out = []
    for fr, d, c in scan_unit(unit, frames, dirs):
        real = {k: v for k, v in c.items() if v > tol}
        if real:
            out.append((fr, d, real))
    return out


def report(hits, name=""):
    if not hits:
        print(f"  interpenetration check{' ' + name if name else ''}: clean (0 hits > {TOL})")
        return
    print(f"  interpenetration check{' ' + name if name else ''}: {len(hits)} frame/dir hits (depth > {TOL} model units)")
    rows = {}                                      # identical results in several dirs -> one line "frame/s,se,ne"
    for fr, d, c in hits:
        s = ", ".join(f"{k} {v:.1f}" for k, v in sorted(c.items(), key=lambda kv: -kv[1]))
        rows.setdefault((fr, s), []).append(d)
    for (fr, s), ds in rows.items():
        print(f"    {fr}/{','.join(ds)}: {s}")


def histogram(scan, edges=(0, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 99)):
    """depth histogram of all contacts per mover>part (max depth per frame/dir)"""
    by = {}
    for _fr, _d, c in scan:
        for k, v in c.items():
            by.setdefault(k, []).append(v)
    print("  depth histogram (count of frame/dir per max-depth bin):")
    print("    " + " " * 14 + " ".join(f"{a:>4}-" for a in edges[:-1]))
    for k in sorted(by):
        h, _ = np.histogram(by[k], bins=edges)
        print(f"    {k:<14}" + " ".join(f"{n:>5}" for n in h))


if __name__ == "__main__":
    from unit_loader import load_unit
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    u = load_unit(args[0])
    if "--hist" in sys.argv:
        sc = scan_unit(u, args[1:] or None)
        histogram(sc)
        report([(f, d, {k: v for k, v in c.items() if v > TOL}) for f, d, c in sc if any(v > TOL for v in c.values())], u.name)
    else:
        report(check_unit(u, args[1:] or None), u.name)
